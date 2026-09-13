[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][string]$CompilerPath,
    [Parameter(Mandatory)][string]$OutputPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'installer/InstallerPayload.psm1') -Force
$root = Split-Path -Parent $PSScriptRoot
$output = Assert-InstallerOutputPath -OutputPath $OutputPath
$payload = Get-InstallerPayload -PackagePath $PackagePath
$archive = (Resolve-Path -LiteralPath $ArchivePath).Path
$archiveHash = (Get-FileHash -LiteralPath $archive).Hash
if ($archiveHash -cne 'D32CE2FEA6304EA7BFEEB85922649A693BDB7FB118EE85FC5EAD9D01455ABA6A') {
    throw 'Archive hash does not match the approved 0.1.0 candidate.'
}
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$compilerHash = (Get-FileHash -LiteralPath $compiler).Hash
if ($compilerHash -cne 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A') {
    throw 'Compiler hash is not the pinned Inno Setup 7.1.0 compiler.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $compiler
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Pyrsys B\.V\.(,|$)') {
    throw 'Compiler Authenticode trust failed.'
}
$compilerRoot = Split-Path -Parent $compiler
$provenance = Get-Content -LiteralPath (Join-Path $compilerRoot 'toolchain.json') -Raw | ConvertFrom-Json
if ($provenance.version -cne '7.1.0' -or $provenance.hashes.compilerSha256 -cne $compilerHash) {
    throw 'Compiler provenance does not match the pinned release.'
}
$compilerVersion = (& $compiler --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $compilerVersion -cne '7.1.0') { throw 'Compiler version probe failed.' }
function InventoryKey($inventory) {
    return ($inventory.Files | ForEach-Object { '{0}|{1}|{2}' -f $_.RelativePath,$_.Length,$_.Sha256 }) -join "`n"
}
$initialKey = InventoryKey $payload
# The approved validator owns its strict private desktop smoke and bounded cleanup.
# Its exit code is mandatory; validation runs before output creation.
$validation = @(& (Get-Command pwsh).Source -NoProfile -File (Join-Path $root 'tests/Dororong.ProductPackage.Tests.ps1') -PackagePath $payload.Root -ArchivePath $archive 2>&1)
$validationExit = $LASTEXITCODE
if ($validationExit -ne 0) { throw "Product package validator failed (exit $validationExit): $($validation -join [Environment]::NewLine)" }
if ((InventoryKey (Get-InstallerPayload -PackagePath $payload.Root)) -cne $initialKey) { throw 'Payload changed during validation.' }
if ((Get-FileHash $archive).Hash -cne $archiveHash) { throw 'Archive changed during validation.' }
# Revalidate the fresh output immediately before the first build write.
$output = Assert-InstallerOutputPath -OutputPath $output
New-Item -ItemType Directory -Path $output | Out-Null
$stage = Join-Path $output 'payload'
New-Item -ItemType Directory -Path $stage | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$entries = [Collections.Generic.List[string]]::new()
$paths = [Collections.Generic.List[string]]::new()
$hashes = [Collections.Generic.List[string]]::new()
foreach ($file in $payload.Files) {
    $relative = $file.RelativePath.Replace('/', '\')
    # The inventory contract rejects quote/semicolon/control syntax. Braces need
    # escaping in Inno parameter fields; Pascal literals separately escape apostrophes.
    $destination = Join-Path $stage $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $payload.Root $relative) -Destination $destination
    $source = $destination.Replace('{','{{')
    $subdir = [IO.Path]::GetDirectoryName($relative).Replace('{','{{')
    $destDir = '{app}'
    if ($subdir) { $destDir += '\' + $subdir }
    $entries.Add(('Source: "{0}"; DestDir: "{1}"; Flags: ignoreversion' -f $source,$destDir))
    $paths.Add($relative)
    $hashes.Add($file.Sha256)
}
$ownership = Join-Path $output 'dororong-owned-files.txt'
$paths.Add('dororong-owned-files.txt')
[IO.File]::WriteAllLines($ownership, $paths, $utf8)
$hashes.Add((Get-FileHash $ownership).Hash)
$entries.Add(('Source: "{0}"; DestDir: "{{app}}"; Flags: ignoreversion' -f $ownership.Replace('{','{{')))
[IO.File]::WriteAllLines((Join-Path $output 'payload-files.iss'), $entries, $utf8)
$code = [Collections.Generic.List[string]]::new()
$code.Add('procedure LoadIncoming(var Paths, Hashes: TArrayOfString);')
$code.Add('begin')
$code.Add(('  SetArrayLength(Paths, {0}); SetArrayLength(Hashes, {0});' -f $paths.Count))
for ($i=0; $i -lt $paths.Count; $i++) {
    $code.Add(("  Paths[{0}] := '{1}'; Hashes[{0}] := '{2}';" -f $i,$paths[$i].Replace("'","''"),$hashes[$i]))
}
$code.Add('end;')
[IO.File]::WriteAllLines((Join-Path $output 'payload-policy.iss'), $code, $utf8)
$payload | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'payload.json') -Encoding utf8NoBOM
$validation | Set-Content -LiteralPath (Join-Path $output 'package-validation.log') -Encoding utf8NoBOM
if ((InventoryKey (Get-InstallerPayload -PackagePath $stage)) -cne $initialKey) { throw 'Staging hash parity failed.' }
# Use the frozen apphost's embedded product icon, not mutable artwork sources.
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path $stage 'Dororong.exe'))
$iconStream = [IO.File]::Create((Join-Path $output 'dororong.ico'))
try { $icon.Save($iconStream) } finally { $iconStream.Dispose(); $icon.Dispose() }
$sourceHashes = @('installer/Dororong.iss','installer/InstallerPolicy.iss','installer/InstallerDirectories.iss','tools/Build-Installer.ps1','tools/installer/InstallerPayload.psm1','tests/Dororong.ProductPackage.Tests.ps1') | ForEach-Object {
    [pscustomobject]@{ path=$_; sha256=(Get-FileHash (Join-Path $root $_)).Hash }
}
$generatedHashes = @('payload-files.iss','payload-policy.iss','dororong-owned-files.txt','dororong.ico') | ForEach-Object {
    [pscustomobject]@{ path=$_; sha256=(Get-FileHash (Join-Path $output $_)).Hash }
}
$compileLog = @(& $compiler /Qp "/DBuildRoot=$output" (Join-Path $root 'installer/Dororong.iss') 2>&1)
$compileExit = $LASTEXITCODE
[IO.File]::WriteAllLines((Join-Path $output 'compile.log'), [string[]]$compileLog, $utf8)
if ($compileExit -ne 0) { throw "Installer compilation failed (exit $compileExit): $($compileLog -join [Environment]::NewLine)" }
if ((InventoryKey (Get-InstallerPayload -PackagePath $stage)) -cne $initialKey -or
    (InventoryKey (Get-InstallerPayload -PackagePath $payload.Root)) -cne $initialKey) { throw 'Payload changed during compilation.' }
if ((Get-FileHash $compiler).Hash -cne $compilerHash -or (Get-FileHash $archive).Hash -cne $archiveHash) { throw 'Compiler/archive changed during compilation.' }
foreach ($source in $sourceHashes) {
    if ((Get-FileHash (Join-Path $root $source.path)).Hash -cne $source.sha256) { throw 'Installer source changed during compilation.' }
}
foreach ($generated in $generatedHashes) {
    if ((Get-FileHash (Join-Path $output $generated.path)).Hash -cne $generated.sha256) { throw 'Generated input changed during compilation.' }
}
$installer = Join-Path $output 'Dororong-Setup-0.1.0-win-x64.exe'
$installerInfo = (Get-Item -LiteralPath $installer).VersionInfo
if ($installerInfo.ProductVersion.Trim() -cne '0.1.0' -or $installerInfo.CompanyName.Trim() -cne 'JOEWRKS') { throw 'Installer metadata parity failed.' }
[pscustomobject]@{
    schemaVersion=1; createdUtc=[datetime]::UtcNow.ToString('o'); version='0.1.0'
    packageRoot=$payload.Root; archivePath=$archive; archiveSha256=$archiveHash
    payloadFileCount=$payload.Files.Count; stagedPayloadRoot=$stage; payloadParity='verified before and after compile'
    compilerPath=$compiler; compilerVersion=$compilerVersion; compilerSha256=$compilerHash
    compilerSignature=[string]$signature.Status; compilerProvenance=$provenance
    sourceFiles=$sourceHashes; generatedInputs=$generatedHashes; validatorExitCode=$validationExit; compilerExitCode=$compileExit
    installerPath=$installer; installerSha256=(Get-FileHash $installer).Hash
    signatureState=[string](Get-AuthenticodeSignature $installer).Status
    lifecycleValidation='NOT RUN: no host product installation; guest acceptance required'
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'build-evidence.json') -Encoding utf8NoBOM
Write-Output "INSTALLER_PATH=$installer"
