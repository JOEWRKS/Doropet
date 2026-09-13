param([string]$PreparedToolchainPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -cne $Actual)
    { throw "$Message Expected <$Expected>; actual <$Actual>." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Assert-ThrowsLike([scriptblock]$Action, [string]$Pattern, [string]$Message)
{
    try { & $Action | Out-Null }
    catch
    {
        if ($_.Exception.Message -notmatch $Pattern)
        { throw "$Message Wrong failure: $($_.Exception.Message)" }
        return
    }
    throw "$Message No failure was raised."
}

function Get-TreeState([string]$Path)
{
    return @(
        Get-ChildItem -LiteralPath $Path -File -Recurse |
            Sort-Object -Property FullName |
            ForEach-Object {
                '{0}|{1}|{2}' -f (
                    [IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')),
                    $_.Length,
                    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            })
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $repositoryRoot 'tools/installer/InstallerPayload.psm1'
$prepareScript = Join-Path $repositoryRoot 'tools/installer/Prepare-InnoSetup.ps1'
$installerArtifacts = Join-Path $repositoryRoot 'artifacts/installer'
$candidateRuntime = Join-Path $repositoryRoot 'artifacts/product-shell/candidate-20260914-idle-blink-01/runtime'
$testRoot = Join-Path $installerArtifacts ('.task-1001-test-' + [Guid]::NewGuid().ToString('N'))
$sentinelRoot = $null
$toolchainSentinel = $null

Import-Module -Name $modulePath -Force

New-Item -ItemType Directory -Path $testRoot | Out-Null
try
{
    $package = Join-Path $testRoot 'package'
    $nested = Join-Path $package 'nested'
    New-Item -ItemType Directory -Path $nested | Out-Null
    foreach ($required in @(
        'Dororong.exe',
        'Dororong.App.dll',
        'Dororong.App.deps.json',
        'Dororong.App.runtimeconfig.json'))
    {
        Copy-Item -LiteralPath (Join-Path $candidateRuntime $required) -Destination (Join-Path $package $required)
    }
    [IO.File]::WriteAllText((Join-Path $nested 'known.txt'), 'abc', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $package 'z-last.txt'), 'z', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $package 'A-first.txt'), 'a', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $package 'a-lower.txt'), 'lower', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $package 'B-upper.txt'), 'upper', [Text.UTF8Encoding]::new($false))

    $before = @(Get-TreeState $package)
    $inventory = Get-InstallerPayload -PackagePath $package
    $after = @(Get-TreeState $package)

    Assert-Equal '0.1.0' $inventory.Version 'The payload product version changed.'
    Assert-Equal '0.1.0.0' $inventory.FileVersion 'The payload file version changed.'
    Assert-Equal ([IO.Path]::GetFullPath($package)) $inventory.Root 'The payload root was not canonicalized.'
    Assert-Equal ($before -join "`n") ($after -join "`n") 'Inventory modified its input tree.'

    $known = @($inventory.Files | Where-Object RelativePath -eq 'nested/known.txt')
    Assert-Equal 1 $known.Count 'The known real file was not inventoried exactly once.'
    Assert-Equal 3L ([long]$known[0].Length) 'The known real file length changed.'
    Assert-Equal 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD' `
        $known[0].Sha256 'The inventory returned the wrong literal SHA-256.'

    $relativePaths = @($inventory.Files.RelativePath)
    $ordinalPaths = @($relativePaths)
    [Array]::Sort($ordinalPaths, [StringComparer]::Ordinal)
    Assert-Equal ($ordinalPaths -join "`n") ($relativePaths -join "`n") `
        'Payload files were not returned in stable ordinal order.'
    Assert-True ([Array]::IndexOf($relativePaths, 'B-upper.txt') -lt [Array]::IndexOf($relativePaths, 'a-lower.txt')) `
        'The mixed-case fixture did not discriminate ordinal ordering from culture sorting.'
    Assert-True (-not ($relativePaths -match '\\')) 'A payload relative path used a backslash.'

    $preservedInventory = Get-InstallerPayload -PackagePath $candidateRuntime
    Assert-Equal '0.1.0' $preservedInventory.Version 'Actual preserved candidate product metadata was not read.'
    Assert-Equal '0.1.0.0' $preservedInventory.FileVersion 'Actual preserved candidate file metadata was not read.'
    Assert-True (@($preservedInventory.Files).Count -gt 0) 'The actual preserved candidate produced an empty inventory.'

    Assert-ThrowsLike {
        Get-InstallerPayload -PackagePath (Join-Path $package '..\package')
    } 'traversal|canonical' 'Payload traversal was accepted.'

    $unsafePackage = Join-Path $testRoot 'unsafe-package'
    Copy-Item -LiteralPath $package -Destination $unsafePackage -Recurse
    [IO.File]::WriteAllText((Join-Path $unsafePackage 'ambiguous; Flags ignoreversion.txt'), 'unsafe')
    Assert-ThrowsLike {
        Get-InstallerPayload -PackagePath $unsafePackage
    } 'unsafe|Inno|semicolon' 'An ambiguous Inno filename was accepted.'

    $junctionTarget = Join-Path $testRoot 'junction-target'
    $junctionPackage = Join-Path $testRoot 'junction-package'
    New-Item -ItemType Directory -Path $junctionTarget | Out-Null
    New-Item -ItemType Junction -Path $junctionPackage -Target $package | Out-Null
    Assert-ThrowsLike {
        Get-InstallerPayload -PackagePath $junctionPackage
    } 'reparse' 'A reparse-point package root was accepted.'

    $nestedJunction = Join-Path $package 'nested-junction'
    New-Item -ItemType Junction -Path $nestedJunction -Target $junctionTarget | Out-Null
    Assert-ThrowsLike {
        Get-InstallerPayload -PackagePath $package
    } 'reparse' 'A nested reparse-point directory was accepted.'
    Remove-Item -LiteralPath $nestedJunction -Force

    $sentinelRoot = Join-Path $installerArtifacts ('candidate-existing-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $sentinelRoot | Out-Null
    $sentinel = Join-Path $sentinelRoot 'sentinel.txt'
    [IO.File]::WriteAllText($sentinel, 'retain me', [Text.UTF8Encoding]::new($false))
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
    Assert-ThrowsLike {
        Assert-InstallerOutputPath -OutputPath $sentinelRoot
    } 'exist' 'An existing candidate output directory was accepted.'
    Assert-Equal $sentinelHash (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash `
        'Existing-output refusal modified the sentinel.'
    Assert-Equal 1 @(Get-ChildItem -LiteralPath $sentinelRoot -Force).Count `
        'Existing-output refusal added files beside the sentinel.'

    $freshOutput = Join-Path $installerArtifacts ('candidate-test-' + [Guid]::NewGuid().ToString('N'))
    $resolvedOutput = Assert-InstallerOutputPath -OutputPath $freshOutput
    Assert-Equal ([IO.Path]::GetFullPath($freshOutput)) $resolvedOutput `
        'A safe fresh candidate output did not resolve canonically.'
    Assert-True (-not (Test-Path -LiteralPath $freshOutput)) 'Output validation created the output directory.'

    Assert-ThrowsLike {
        Assert-InstallerOutputPath -OutputPath (Join-Path $installerArtifacts 'candidate-a\..\candidate-b')
    } 'traversal|canonical' 'Output traversal was accepted.'
    Assert-ThrowsLike {
        Assert-InstallerOutputPath -OutputPath (Join-Path $installerArtifacts 'candidate-a\nested')
    } 'direct child' 'A non-direct-child candidate output was accepted.'

    $toolchainSentinel = Join-Path $installerArtifacts ('toolchain-existing-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $toolchainSentinel | Out-Null
    $toolchainSentinelFile = Join-Path $toolchainSentinel 'sentinel.txt'
    [IO.File]::WriteAllText($toolchainSentinelFile, 'retain toolchain')
    $toolchainSentinelHash = (Get-FileHash -LiteralPath $toolchainSentinelFile -Algorithm SHA256).Hash
    Assert-ThrowsLike {
        & $prepareScript -OutputPath $toolchainSentinel
    } 'exist' 'Toolchain preparation accepted an existing output.'
    Assert-Equal $toolchainSentinelHash `
        (Get-FileHash -LiteralPath $toolchainSentinelFile -Algorithm SHA256).Hash `
        'Toolchain existing-output refusal modified the sentinel.'
    Assert-Equal 1 @(Get-ChildItem -LiteralPath $toolchainSentinel -Force).Count `
        'Toolchain existing-output refusal added files beside the sentinel.'

    if (-not [string]::IsNullOrWhiteSpace($PreparedToolchainPath))
    {
        $preparedRoot = [IO.Path]::GetFullPath($PreparedToolchainPath)
        $manifestPath = Join-Path $preparedRoot 'toolchain.json'
        $compilerPath = Join-Path $preparedRoot 'ISCC.exe'
        Assert-True (Test-Path -LiteralPath $manifestPath -PathType Leaf) `
            'Successful toolchain preparation did not produce toolchain.json.'
        Assert-True (Test-Path -LiteralPath $compilerPath -PathType Leaf) `
            'Successful toolchain preparation did not produce ISCC.exe.'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        Assert-Equal '7.1.0' $manifest.version 'The toolchain manifest version changed.'
        Assert-Equal 'Pyrsys B.V.' $manifest.signer.installer 'The toolchain installer signer changed.'
        Assert-Equal 'Pyrsys B.V.' $manifest.signer.compiler 'The toolchain compiler signer changed.'
        Assert-Equal (Get-FileHash -LiteralPath $compilerPath -Algorithm SHA256).Hash `
            $manifest.hashes.compilerSha256 'The recorded compiler hash does not match the compiler.'
        Assert-Equal (Get-FileHash -LiteralPath (Join-Path $preparedRoot '.source/innosetup-7.1.0-x64.exe') -Algorithm SHA256).Hash `
            $manifest.hashes.installerSha256 'The recorded installer hash does not match the retained installer source.'
        Assert-Equal (Get-FileHash -LiteralPath (Join-Path $preparedRoot '.source/isportable.iss') -Algorithm SHA256).Hash `
            $manifest.hashes.portableSourceSha256 'The recorded portable-source hash does not match the retained source.'
        Assert-Equal (Get-FileHash -LiteralPath (Join-Path $preparedRoot '.source/setup.iss') -Algorithm SHA256).Hash `
            $manifest.hashes.setupSourceSha256 'The recorded setup-source hash does not match the retained source.'
        $reportedVersion = (& $compilerPath '--version' | Out-String).Trim()
        Assert-Equal 0 $LASTEXITCODE 'The prepared compiler version probe failed.'
        Assert-Equal '7.1.0' $reportedVersion 'The prepared compiler reported the wrong engine version.'
    }
}
finally
{
    if (Test-Path -LiteralPath $testRoot)
    { Remove-Item -LiteralPath $testRoot -Recurse -Force }
    foreach ($ownedOutput in @($sentinelRoot, $toolchainSentinel))
    {
        if (-not [string]::IsNullOrWhiteSpace($ownedOutput) -and (Test-Path -LiteralPath $ownedOutput))
        { Remove-Item -LiteralPath $ownedOutput -Recurse -Force }
    }
}

Write-Output 'INSTALLER PAYLOAD PASS: inventory, metadata, path safety, reparse refusal, and sentinel preservation verified.'
