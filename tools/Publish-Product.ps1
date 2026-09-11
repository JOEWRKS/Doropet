param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$RuntimeVersion = '8.0.31'

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    {
        throw $Message
    }
}

function Get-NuGetRoot
{
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES))
    {
        return [IO.Path]::GetFullPath($env:NUGET_PACKAGES)
    }

    return [IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'), '.nuget', 'packages')
}

function Get-HostModelAssemblyPath
{
    $dotnetExecutable = (Get-Command dotnet -CommandType Application).Source
    $sdkVersion = (& $dotnetExecutable --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion))
    {
        throw 'Unable to resolve the active .NET SDK version.'
    }

    $path = Join-Path (Split-Path -Parent $dotnetExecutable) "sdk/$sdkVersion/Microsoft.NET.HostModel.dll"
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Active SDK HostModel assembly is missing: $path"
    return (Resolve-Path -LiteralPath $path).Path
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/product-shell'))
$candidateRoot = [IO.Path]::GetFullPath($OutputPath)
$relativeCandidate = [IO.Path]::GetRelativePath($artifactRoot, $candidateRoot)
$candidateSegments = $relativeCandidate -split '[\\/]'
Assert-True ($candidateSegments.Count -eq 1 -and $candidateSegments[0].StartsWith('candidate-', [StringComparison]::Ordinal)) `
    "OutputPath must be a direct artifacts/product-shell/candidate-* directory: $candidateRoot"
Assert-True (-not (Test-Path -LiteralPath $candidateRoot)) `
    "Refusing to overwrite or clean an existing candidate path: $candidateRoot"

$projectPath = Join-Path $repositoryRoot 'src/Dororong.App/Dororong.App.csproj'
$runtimePath = Join-Path $candidateRoot 'runtime'
$archivePath = Join-Path $candidateRoot 'Dororong-win-x64.zip'

New-Item -ItemType Directory -Path $candidateRoot | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $runtimePath `
    --nologo `
    -p:RuntimeFrameworkVersion=$RuntimeVersion `
    -p:TargetLatestRuntimePatch=false
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code $LASTEXITCODE. Partial output remains at $candidateRoot for inspection."
}

$resolvedRuntime = (Resolve-Path -LiteralPath $runtimePath).Path
Assert-True ([string]::Equals($resolvedRuntime, [IO.Path]::GetFullPath($runtimePath), [StringComparison]::OrdinalIgnoreCase)) `
    "dotnet publish resolved to an unexpected output directory: $resolvedRuntime"
Assert-True ($resolvedRuntime.StartsWith($candidateRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) `
    "Resolved publish output escaped the fresh candidate directory: $resolvedRuntime"

$developmentApphost = Join-Path $resolvedRuntime 'Dororong.App.exe'
$productApphost = Join-Path $resolvedRuntime 'Dororong.exe'
$appDll = Join-Path $resolvedRuntime 'Dororong.App.dll'
$runtimeConfigPath = Join-Path $resolvedRuntime 'Dororong.App.runtimeconfig.json'
$depsPath = Join-Path $resolvedRuntime 'Dororong.App.deps.json'
Assert-True (Test-Path -LiteralPath $developmentApphost -PathType Leaf) 'Resolved publish output is missing Dororong.App.exe.'
Assert-True (Test-Path -LiteralPath $appDll -PathType Leaf) 'Resolved publish output is missing Dororong.App.dll.'
Assert-True (Test-Path -LiteralPath $runtimeConfigPath -PathType Leaf) 'Resolved publish output is missing Dororong.App.runtimeconfig.json.'
Assert-True (Test-Path -LiteralPath $depsPath -PathType Leaf) 'Resolved publish output is missing Dororong.App.deps.json.'
Assert-True (-not (Test-Path -LiteralPath $productApphost)) 'Resolved publish output unexpectedly already contains Dororong.exe.'

$runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
$includedFrameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
$coreVersion = @($includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App' | ForEach-Object version)
$desktopVersion = @($includedFrameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App' | ForEach-Object version)
Assert-True ($coreVersion.Count -eq 1 -and $coreVersion[0] -eq $RuntimeVersion) `
    "Resolved publish output does not bundle Microsoft.NETCore.App $RuntimeVersion."
Assert-True ($desktopVersion.Count -eq 1 -and $desktopVersion[0] -eq $RuntimeVersion) `
    "Resolved publish output does not bundle Microsoft.WindowsDesktop.App $RuntimeVersion."

$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$libraries = @($deps.libraries.PSObject.Properties.Name)
Assert-True ($deps.runtimeTarget.name -like '*/win-x64') 'Resolved publish output dependency target is not win-x64.'
Assert-True ($libraries -contains "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/$RuntimeVersion") `
    "Resolved publish output does not use the Core runtime $RuntimeVersion pack."
Assert-True ($libraries -contains "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/$RuntimeVersion") `
    "Resolved publish output does not use the Desktop runtime $RuntimeVersion pack."
$projectAssetsPath = Join-Path $repositoryRoot 'src/Dororong.App/obj/project.assets.json'
$projectAssets = Get-Content -LiteralPath $projectAssetsPath -Raw | ConvertFrom-Json
$hostResolution = @($projectAssets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -eq 'Microsoft.NETCore.App.Host.win-x64')
Assert-True ($hostResolution.Count -eq 1 -and $hostResolution[0].version -eq "[$RuntimeVersion, $RuntimeVersion]") `
    "Resolved publish output does not use the apphost $RuntimeVersion pack."

$hostPackRoot = Join-Path (Get-NuGetRoot) "microsoft.netcore.app.host.win-x64/$RuntimeVersion/runtimes/win-x64/native"
$hostPackApphost = Join-Path $hostPackRoot 'apphost.exe'
Assert-True (Test-Path -LiteralPath $hostPackApphost -PathType Leaf) "Pinned apphost pack file is missing: $hostPackApphost"
Add-Type -Path (Get-HostModelAssemblyPath)
$pinnedApphost = Join-Path $candidateRoot ".pinned-apphost-$([Guid]::NewGuid().ToString('N')).exe"
try
{
    [Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost(
        $hostPackApphost,
        $pinnedApphost,
        'Dororong.App.dll',
        $true,
        $appDll,
        $false,
        $false,
        $null)
    Assert-True (Test-Path -LiteralPath $pinnedApphost -PathType Leaf) 'Pinned HostWriter did not create the product apphost.'
    Move-Item -LiteralPath $pinnedApphost -Destination $developmentApphost -Force
}
finally
{
    if (Test-Path -LiteralPath $pinnedApphost)
    {
        Remove-Item -LiteralPath $pinnedApphost -Force
    }
}

Move-Item -LiteralPath $developmentApphost -Destination $productApphost
Assert-True (Test-Path -LiteralPath $productApphost -PathType Leaf) 'Product apphost rename did not create Dororong.exe.'
Assert-True (-not (Test-Path -LiteralPath $developmentApphost)) 'Development apphost remains after product rename.'

Compress-Archive -Path (Join-Path $resolvedRuntime '*') -DestinationPath $archivePath -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
$matchingRidOutput = Join-Path $repositoryRoot 'tests/Dororong.App.Tests/bin/Release/net8.0-windows/win-x64'
$referenceAppDll = (Resolve-Path -LiteralPath (Join-Path $matchingRidOutput 'Dororong.App.dll')).Path
$referenceCoreDll = (Resolve-Path -LiteralPath (Join-Path $matchingRidOutput 'Dororong.Core.dll')).Path

Write-Output "CANDIDATE_PATH=$candidateRoot"
Write-Output "PACKAGE_PATH=$resolvedRuntime"
Write-Output "ARCHIVE_PATH=$archivePath"
Write-Output "ARCHIVE_SHA256=$archiveHash"
Write-Output "REFERENCE_APP_DLL=$referenceAppDll"
Write-Output "REFERENCE_CORE_DLL=$referenceCoreDll"
