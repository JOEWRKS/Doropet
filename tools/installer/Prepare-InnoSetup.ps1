param([Parameter(Mandatory)][string]$OutputPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NoTraversalSyntax([string]$Path)
{
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Toolchain output path is empty.' }
    foreach ($component in @($Path -split '[\\/]'))
    {
        if ($component -in @('.', '..'))
        { throw "Toolchain output path contains traversal syntax and is not canonical: $Path" }
    }
}

function Assert-NoReparsePointComponents([string]$ExistingPath)
{
    $current = Get-Item -LiteralPath $ExistingPath -Force
    while ($null -ne $current)
    {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        { throw "Toolchain output has a reparse-point ancestor: $($current.FullName)" }
        $parentPath = Split-Path -Parent $current.FullName
        if ([string]::IsNullOrEmpty($parentPath) -or $parentPath -eq $current.FullName) { break }
        $current = Get-Item -LiteralPath $parentPath -Force
    }
}

function Get-VerifiedSigner([string]$Path, [string]$Label)
{
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid)
    { throw "$Label Authenticode signature is not valid: $($signature.StatusMessage)" }
    $signer = $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
    if ($signer -ne 'Pyrsys B.V.')
    { throw "$Label signer is '$signer', not the required Pyrsys B.V." }
    return $signer
}

Assert-NoTraversalSyntax -Path $OutputPath
$resolved = [IO.Path]::GetFullPath($OutputPath)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$installerArtifacts = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/installer'))
if (-not (Test-Path -LiteralPath $installerArtifacts -PathType Container))
{ throw "Installer artifact root does not exist: $installerArtifacts" }
Assert-NoReparsePointComponents -ExistingPath $installerArtifacts
if (-not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetDirectoryName($resolved), $installerArtifacts))
{ throw "Toolchain output must be a direct child of $installerArtifacts" }
$leaf = [IO.Path]::GetFileName($resolved)
if (-not $leaf.StartsWith('toolchain-', [StringComparison]::Ordinal) -or $leaf.Length -le 10)
{ throw 'Toolchain output name must begin with toolchain- and include a unique suffix.' }
if (Test-Path -LiteralPath $resolved)
{
    $existing = Get-Item -LiteralPath $resolved -Force
    if (($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    { throw "Toolchain output is an existing reparse point: $resolved" }
    throw "Toolchain output already exists: $resolved"
}

$version = '7.1.0'
$releasePage = 'https://jrsoftware.org/isdl.php'
$downloadUri = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe'
$portableSourceUri = 'https://raw.githubusercontent.com/jrsoftware/issrc/is-7_1_0/isportable.iss'
$setupSourceUri = 'https://raw.githubusercontent.com/jrsoftware/issrc/is-7_1_0/setup.iss'

New-Item -ItemType Directory -Path $resolved | Out-Null
$sourceDirectory = Join-Path $resolved '.source'
New-Item -ItemType Directory -Path $sourceDirectory | Out-Null
$installerPath = Join-Path $sourceDirectory 'innosetup-7.1.0-x64.exe'
$portableSourcePath = Join-Path $sourceDirectory 'isportable.iss'
$setupSourcePath = Join-Path $sourceDirectory 'setup.iss'

Invoke-WebRequest -Uri $downloadUri -OutFile $installerPath -UseBasicParsing
Invoke-WebRequest -Uri $portableSourceUri -OutFile $portableSourcePath -UseBasicParsing
Invoke-WebRequest -Uri $setupSourceUri -OutFile $setupSourcePath -UseBasicParsing

$portableSource = Get-Content -LiteralPath $portableSourcePath -Raw
$setupSource = Get-Content -LiteralPath $setupSourcePath -Raw
if ($portableSource -notmatch 'Uninstallable=not PortableCheck' -or
    $portableSource -notmatch "\{param:portable\|0\}.*=.*'1'" -or
    $portableSource -notmatch 'NoIconsCheck\.Checked := True')
{ throw 'Pinned isportable.iss does not prove portable uninstall and icon suppression semantics.' }
if ($setupSource -notmatch 'AppVersion=7\.1\.0' -or
    $setupSource -notmatch 'fileassoc.*Check: not PortableCheck' -or
    $setupSource -notmatch '\[Icons\]')
{ throw 'Pinned setup.iss does not prove versioned portable association and icon semantics.' }

$installerSigner = Get-VerifiedSigner -Path $installerPath -Label 'Inno Setup installer'
$installerVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($installerPath).ProductVersion
if ($installerVersion -notlike '7.1.0*')
{ throw "Inno Setup installer product version is '$installerVersion', not 7.1.0." }

$arguments = @(
    '/CURRENTUSER',
    '/PORTABLE=1',
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/SP-',
    '/NORESTART',
    '/NOICONS',
    "/DIR=`"$resolved`"")
$process = Start-Process -FilePath $installerPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0)
{ throw "Portable Inno Setup preparation failed with exit code $($process.ExitCode)." }

$compilerPath = Join-Path $resolved 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf))
{ throw "Portable Inno Setup compiler was not produced: $compilerPath" }
$compilerSigner = Get-VerifiedSigner -Path $compilerPath -Label 'Inno Setup compiler'
$compilerVersion = (@(& $compilerPath '--version')) -join "`n"
$compilerExitCode = $LASTEXITCODE
$compilerVersion = $compilerVersion.Trim()
if ($compilerExitCode -ne 0)
{ throw "Inno Setup compiler version probe failed with exit code $compilerExitCode." }
if ($compilerVersion -ne $version)
{ throw "Inno Setup compiler reported version '$compilerVersion', not $version." }

$manifest = [ordered]@{
    source = [ordered]@{
        releasePage = $releasePage
        downloadUri = $downloadUri
        portableSourceUri = $portableSourceUri
        setupSourceUri = $setupSourceUri
    }
    version = $version
    hashes = [ordered]@{
        installerSha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
        compilerSha256 = (Get-FileHash -LiteralPath $compilerPath -Algorithm SHA256).Hash
        portableSourceSha256 = (Get-FileHash -LiteralPath $portableSourcePath -Algorithm SHA256).Hash
        setupSourceSha256 = (Get-FileHash -LiteralPath $setupSourcePath -Algorithm SHA256).Hash
    }
    signer = [ordered]@{
        installer = $installerSigner
        compiler = $compilerSigner
    }
    compilerPath = $compilerPath
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolved 'toolchain.json') -Encoding utf8NoBOM

Write-Output "TOOLCHAIN_PATH=$resolved"
Write-Output "COMPILER_PATH=$compilerPath"
