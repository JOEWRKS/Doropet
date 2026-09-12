Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NoTraversalSyntax
{
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Label)

    if ([string]::IsNullOrWhiteSpace($Path)) { throw "$Label is empty." }
    foreach ($component in @($Path -split '[\\/]'))
    {
        if ($component -in @('.', '..'))
        { throw "$Label contains traversal syntax and is not canonical: $Path" }
    }
}

function Assert-NoReparsePointComponents
{
    param([Parameter(Mandatory)][string]$ExistingPath, [Parameter(Mandatory)][string]$Label)

    $current = Get-Item -LiteralPath $ExistingPath -Force
    while ($null -ne $current)
    {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        { throw "$Label contains a reparse-point component: $($current.FullName)" }

        $parentPath = Split-Path -Parent $current.FullName
        if ([string]::IsNullOrEmpty($parentPath) -or $parentPath -eq $current.FullName) { break }
        $current = Get-Item -LiteralPath $parentPath -Force
    }
}

function Assert-SafeRelativePath
{
    param([Parameter(Mandatory)][string]$RelativePath)

    if ([IO.Path]::IsPathRooted($RelativePath))
    { throw "Payload relative path is rooted: $RelativePath" }
    Assert-NoTraversalSyntax -Path $RelativePath -Label 'Payload relative path'

    foreach ($component in @($RelativePath -split '/'))
    {
        if ([string]::IsNullOrEmpty($component))
        { throw "Payload relative path contains an empty component: $RelativePath" }
        if ($component.EndsWith(' ', [StringComparison]::Ordinal) -or
            $component.EndsWith('.', [StringComparison]::Ordinal))
        { throw "Payload filename has an unsafe trailing character for Inno Setup: $RelativePath" }
        if ($component.IndexOfAny([char[]]@(';', '"')) -ge 0 -or
            $component.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
            @($component.ToCharArray() | Where-Object { [char]::IsControl($_) }).Count -gt 0)
        { throw "Payload filename contains an unsafe Inno Setup character: $RelativePath" }

        $stem = [IO.Path]::GetFileNameWithoutExtension($component)
        if ($stem -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$')
        { throw "Payload filename is an ambiguous Windows device name: $RelativePath" }
    }
}

function Resolve-InstallerOutputPath
{
    param(
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$RequiredPrefix)

    Assert-NoTraversalSyntax -Path $OutputPath -Label 'Installer output path'
    $resolved = [IO.Path]::GetFullPath($OutputPath)
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $installerArtifacts = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/installer'))
    if (-not (Test-Path -LiteralPath $installerArtifacts -PathType Container))
    { throw "Installer artifact root does not exist: $installerArtifacts" }
    Assert-NoReparsePointComponents -ExistingPath $installerArtifacts -Label 'Installer artifact root'

    $parent = [IO.Path]::GetDirectoryName($resolved)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($parent, $installerArtifacts))
    { throw "Installer output must be a direct child of $installerArtifacts" }

    $leaf = [IO.Path]::GetFileName($resolved)
    if (-not $leaf.StartsWith($RequiredPrefix, [StringComparison]::Ordinal) -or
        $leaf.Length -le $RequiredPrefix.Length)
    { throw "Installer output name must begin with $RequiredPrefix and include a unique suffix." }

    if (Test-Path -LiteralPath $resolved)
    {
        $existing = Get-Item -LiteralPath $resolved -Force
        if (($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        { throw "Installer output is an existing reparse point: $resolved" }
        throw "Installer output already exists: $resolved"
    }
    return $resolved
}

function Get-InstallerPayload
{
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackagePath)

    Assert-NoTraversalSyntax -Path $PackagePath -Label 'Package path'
    $root = [IO.Path]::GetFullPath($PackagePath)
    if (-not (Test-Path -LiteralPath $root -PathType Container))
    { throw "Payload package directory does not exist: $root" }
    Assert-NoReparsePointComponents -ExistingPath $root -Label 'Payload package path'

    $entries = @(Get-ChildItem -LiteralPath $root -Force -Recurse)
    foreach ($entry in $entries)
    {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        { throw "Payload contains a reparse point: $($entry.FullName)" }
    }

    $requiredPaths = @(
        'Dororong.exe',
        'Dororong.App.dll',
        'Dororong.App.deps.json',
        'Dororong.App.runtimeconfig.json')
    $presentPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $files = [Collections.Generic.List[object]]::new()
    foreach ($file in @($entries | Where-Object { -not $_.PSIsContainer }))
    {
        $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        Assert-SafeRelativePath -RelativePath $relativePath
        [void]$presentPaths.Add($relativePath)
        $files.Add([pscustomobject]@{
            RelativePath = $relativePath
            Length = [long]$file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        })
    }
    foreach ($requiredPath in $requiredPaths)
    {
        if (-not $presentPaths.Contains($requiredPath))
        { throw "Payload is missing required product file: $requiredPath" }
    }
    if ($files.Count -eq 0) { throw 'Payload inventory is empty.' }

    $files.Sort([Comparison[object]]{
        param($left, $right)
        return [StringComparer]::Ordinal.Compare(
            [string]$left.RelativePath,
            [string]$right.RelativePath)
    })

    $executable = Get-Item -LiteralPath (Join-Path $root 'Dororong.exe')
    $version = [string]$executable.VersionInfo.ProductVersion
    $fileVersion = [string]$executable.VersionInfo.FileVersion
    if ($version -ne '0.1.0')
    { throw "Payload Dororong.exe has unsupported product version '$version'." }
    if ($fileVersion -ne '0.1.0.0')
    { throw "Payload Dororong.exe has unsupported file version '$fileVersion'." }

    return [pscustomobject]@{
        Version = $version
        FileVersion = $fileVersion
        Root = $root
        Files = @($files)
    }
}

function Assert-InstallerOutputPath
{
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$OutputPath)

    return Resolve-InstallerOutputPath -OutputPath $OutputPath -RequiredPrefix 'candidate-'
}

Export-ModuleMember -Function Get-InstallerPayload, Assert-InstallerOutputPath
