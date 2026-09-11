param(
    [Parameter(ParameterSetName = 'Package', Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(ParameterSetName = 'Package', Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(ParameterSetName = 'PublisherRefusal', Mandatory = $true)]
    [string]$PublisherPath,

    [Parameter(ParameterSetName = 'PublisherRefusal', Mandatory = $true)]
    [switch]$VerifyExistingOutputRefusal,

    [Parameter(ParameterSetName = 'MissingReferences', Mandatory = $true)]
    [string]$MissingReferencesPublisherPath,

    [Parameter(ParameterSetName = 'MissingReferences', Mandatory = $true)]
    [switch]$VerifyMissingReferencesRefusal
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

function Assert-Equal($Expected, $Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Get-RequiredFile([string]$Root, [string]$RelativePath)
{
    $path = Join-Path $Root $RelativePath
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Required package file is missing: $RelativePath"
    return (Resolve-Path -LiteralPath $path).Path
}

function Get-Hash([string]$Path)
{
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-FileHashEqual([string]$ExpectedPath, [string]$ActualPath, [string]$Message)
{
    Assert-Equal (Get-Hash $ExpectedPath) (Get-Hash $ActualPath) $Message
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

function Invoke-ExistingOutputRefusal
{
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $artifactRoot = Join-Path $repositoryRoot 'artifacts/product-shell'
    $existingOutput = Join-Path $artifactRoot "candidate-refusal-test-$([Guid]::NewGuid().ToString('N'))"
    $sentinel = Join-Path $existingOutput 'do-not-overwrite.txt'
    New-Item -ItemType Directory -Path $existingOutput | Out-Null
    Set-Content -LiteralPath $sentinel -Value 'preserve me' -NoNewline

    try
    {
        $powershell = (Get-Process -Id $PID).Path
        $result = & $powershell -NoLogo -NoProfile -NonInteractive -File $PublisherPath -OutputPath $existingOutput 2>&1
        $exitCode = $LASTEXITCODE
        Assert-True ($exitCode -ne 0) 'Publisher accepted a pre-existing candidate directory.'
        Assert-Equal 'preserve me' (Get-Content -LiteralPath $sentinel -Raw) 'Publisher altered the existing candidate sentinel.'
        Assert-Equal 1 @(Get-ChildItem -LiteralPath $existingOutput -Force).Count 'Publisher added files to a refused candidate directory.'
        Write-Output "PRODUCT PUBLISH REFUSAL PASS: existing output was preserved and rejected (exit $exitCode)."
        $global:LASTEXITCODE = 0
    }
    finally
    {
        $resolvedArtifactRoot = (Resolve-Path -LiteralPath $artifactRoot).Path
        $resolvedExisting = (Resolve-Path -LiteralPath $existingOutput -ErrorAction SilentlyContinue).Path
        if ($null -ne $resolvedExisting -and $resolvedExisting.StartsWith($resolvedArtifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
        {
            Remove-Item -LiteralPath $resolvedExisting -Recurse -Force
        }
    }
}

function Invoke-MissingReferencesRefusal
{
    $testRoot = Join-Path ([IO.Path]::GetTempPath()) "DororongMissingReferences-$([Guid]::NewGuid().ToString('N'))"
    $isolatedRepository = Join-Path $testRoot 'repository'
    $isolatedTools = Join-Path $isolatedRepository 'tools'
    $isolatedArtifactRoot = Join-Path $isolatedRepository 'artifacts/product-shell'
    $isolatedPublisher = Join-Path $isolatedTools 'Publish-Product.ps1'
    $candidate = Join-Path $isolatedArtifactRoot 'candidate-missing-references-test'
    try
    {
        New-Item -ItemType Directory -Path $isolatedTools, $isolatedArtifactRoot | Out-Null
        Copy-Item -LiteralPath $MissingReferencesPublisherPath -Destination $isolatedPublisher
        $powershell = (Get-Process -Id $PID).Path
        $result = & $powershell -NoLogo -NoProfile -NonInteractive -File $isolatedPublisher -OutputPath $candidate 2>&1
        $exitCode = $LASTEXITCODE
        Assert-True ($exitCode -ne 0) 'Publisher accepted an isolated repository without tested RID reference DLLs.'
        Assert-True (-not (Test-Path -LiteralPath $candidate)) 'Publisher created candidate output before rejecting missing tested RID reference DLLs.'
        Assert-True (($result | Out-String) -match 'Dororong.App.dll') 'Publisher failure did not identify the missing tested App DLL prerequisite.'
        Write-Output "PRODUCT PUBLISH PREREQUISITE PASS: missing tested RID references were rejected before candidate creation (exit $exitCode)."
        $global:LASTEXITCODE = 0
    }
    finally
    {
        if (Test-Path -LiteralPath $testRoot)
        {
            $resolvedTestRoot = (Resolve-Path -LiteralPath $testRoot).Path
            $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            Assert-True ($resolvedTestRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) `
                "Refusing recursive cleanup outside the temporary directory: $resolvedTestRoot"
            Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
        }
    }
}

function Add-NativeDesktopHarness
{
    if ('Dororong.PackageTests.NativeDesktopHarness' -as [type])
    {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Dororong.PackageTests
{
    public sealed class NativeDesktopHarness : IDisposable
    {
        private const uint DesktopCreateWindow = 0x0002;
        private const uint DesktopEnumerate = 0x0040;
        private const uint DesktopReadObjects = 0x0001;
        private const uint DesktopWriteObjects = 0x0080;
        private const uint WmClose = 0x0010;
        private const uint WaitObject0 = 0;
        private const uint WaitTimeout = 258;

        private readonly IntPtr desktop;
        private readonly string name;
        private readonly Dictionary<int, IntPtr> processes = new Dictionary<int, IntPtr>();

        private NativeDesktopHarness(IntPtr desktop, string name)
        {
            this.desktop = desktop;
            this.name = name;
        }

        public static NativeDesktopHarness Create(string name)
        {
            var access = DesktopCreateWindow | DesktopEnumerate | DesktopReadObjects | DesktopWriteObjects;
            var desktop = CreateDesktopW(name, IntPtr.Zero, IntPtr.Zero, 0, access, IntPtr.Zero);
            if (desktop == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktopW failed.");
            }

            return new NativeDesktopHarness(desktop, name);
        }

        public int Start(string executablePath)
        {
            var startup = new StartupInfo();
            startup.cb = Marshal.SizeOf<StartupInfo>();
            startup.lpDesktop = name;
            if (!CreateProcessW(executablePath, null, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero,
                    System.IO.Path.GetDirectoryName(executablePath), ref startup, out var processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessW failed.");
            }

            CloseHandle(processInfo.hThread);
            var processId = unchecked((int)processInfo.dwProcessId);
            processes.Add(processId, processInfo.hProcess);
            return processId;
        }

        public bool IsRunning(int processId)
        {
            var wait = WaitForSingleObject(processes[processId], 0);
            if (wait == WaitTimeout) return true;
            if (wait == WaitObject0) return false;
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
        }

        public int WaitForExit(int processId, int timeoutMilliseconds)
        {
            var handle = processes[processId];
            var wait = WaitForSingleObject(handle, unchecked((uint)timeoutMilliseconds));
            if (wait == WaitTimeout)
            {
                throw new TimeoutException("Process " + processId + " did not exit in time.");
            }
            if (wait != WaitObject0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
            }
            if (!GetExitCodeProcess(handle, out var exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed.");
            }
            return unchecked((int)exitCode);
        }

        public void Terminate(int processId)
        {
            if (IsRunning(processId) && !TerminateProcess(processes[processId], 199))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TerminateProcess failed.");
            }
        }

        public int CloseWindows(int processId)
        {
            var windows = new List<IntPtr>();
            EnumDesktopWindows(desktop, (window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == unchecked((uint)processId))
                {
                    windows.Add(window);
                }
                return true;
            }, IntPtr.Zero);

            foreach (var window in windows)
            {
                PostMessageW(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            }
            return windows.Count;
        }

        public void Dispose()
        {
            foreach (var process in processes.Values)
            {
                CloseHandle(process);
            }
            processes.Clear();
            if (!CloseDesktop(desktop))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CloseDesktop failed.");
            }
        }

        private delegate bool EnumDesktopWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktopW(string desktop, IntPtr device, IntPtr devmode,
            uint flags, uint desiredAccess, IntPtr securityAttributes);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr desktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumDesktopWindows(IntPtr desktop, EnumDesktopWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessW(string applicationName, string commandLine,
            IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags,
            IntPtr environment, string currentDirectory, ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }
    }
}
'@
}

function Invoke-NativeSmoke([string]$ExecutablePath)
{
    Add-NativeDesktopHarness
    $desktopName = "DororongPackageTest-$([Guid]::NewGuid().ToString('N'))"
    $desktop = $null
    $ownedProcessIds = [Collections.Generic.List[int]]::new()
    try
    {
        try
        {
            $desktop = [Dororong.PackageTests.NativeDesktopHarness]::Create($desktopName)
        }
        catch [ComponentModel.Win32Exception]
        {
            Write-Warning "PRODUCT NATIVE SMOKE UNVERIFIED: isolated desktop creation was unavailable: $($_.Exception.Message)"
            return 'UNVERIFIED'
        }

        $firstId = $desktop.Start($ExecutablePath)
        $ownedProcessIds.Add($firstId)
        Start-Sleep -Milliseconds 2500
        Assert-True $desktop.IsRunning($firstId) 'Renamed apphost exited during startup.'
        $runningProcess = [Diagnostics.Process]::GetProcessById($firstId)
        try
        {
            $loadedCoreClr = @($runningProcess.Modules | Where-Object ModuleName -eq 'coreclr.dll')
            Assert-Equal 1 $loadedCoreClr.Count 'Packaged process did not expose exactly one loaded coreclr.dll.'
            $expectedCoreClr = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $ExecutablePath) 'coreclr.dll'))
            Assert-Equal $expectedCoreClr $loadedCoreClr[0].FileName 'Packaged process loaded coreclr.dll from outside the candidate.'
            $actualRuntimeHash = Get-Hash $loadedCoreClr[0].FileName
        }
        finally
        {
            $runningProcess.Dispose()
        }

        $duplicateId = $desktop.Start($ExecutablePath)
        $ownedProcessIds.Add($duplicateId)
        $duplicateExitCode = $desktop.WaitForExit($duplicateId, 15000)
        Assert-Equal 0 $duplicateExitCode 'Duplicate packaged launch returned the wrong exit code.'
        Assert-True $desktop.IsRunning($firstId) 'Duplicate launch terminated the owning packaged process.'

        $windowCount = $desktop.CloseWindows($firstId)
        Assert-True ($windowCount -gt 0) 'No owned window was found for WM_CLOSE on the isolated desktop.'
        $firstExitCode = $desktop.WaitForExit($firstId, 20000)
        Assert-Equal 0 $firstExitCode 'Packaged owner returned the wrong exit code after WM_CLOSE.'
        Write-Output "PRODUCT NATIVE SMOKE PASS: renamed apphost loaded Dororong.App.dll and candidate coreclr SHA256 $actualRuntimeHash on an isolated desktop; duplicate exited 0; owned PID $firstId exited 0 via WM_CLOSE."
        return 'PASS'
    }
    finally
    {
        foreach ($processId in $ownedProcessIds)
        {
            if ($desktop.IsRunning($processId))
            {
                $desktop.Terminate($processId)
                $null = $desktop.WaitForExit($processId, 10000)
            }
        }
        if ($null -ne $desktop)
        {
            $desktop.Dispose()
        }
    }
}

if ($PSCmdlet.ParameterSetName -eq 'PublisherRefusal')
{
    Invoke-ExistingOutputRefusal
    return
}

if ($PSCmdlet.ParameterSetName -eq 'MissingReferences')
{
    Invoke-MissingReferencesRefusal
    return
}

$packageRoot = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = (Resolve-Path -LiteralPath $ArchivePath).Path

$apphost = Get-RequiredFile $packageRoot 'Dororong.exe'
$appDll = Get-RequiredFile $packageRoot 'Dororong.App.dll'
$coreDll = Get-RequiredFile $packageRoot 'Dororong.Core.dll'
$runtimeConfigPath = Get-RequiredFile $packageRoot 'Dororong.App.runtimeconfig.json'
$depsPath = Get-RequiredFile $packageRoot 'Dororong.App.deps.json'
$coreClr = Get-RequiredFile $packageRoot 'coreclr.dll'
$hostFxr = Get-RequiredFile $packageRoot 'hostfxr.dll'
$presentationFramework = Get-RequiredFile $packageRoot 'PresentationFramework.dll'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Dororong.App.exe'))) 'Development apphost Dororong.App.exe remains in the product package.'

$productExecutables = @(Get-ChildItem -LiteralPath $packageRoot -File -Filter '*.exe' | Where-Object {
    [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).CompanyName -eq 'JOEWRKS'
})
Assert-Equal 1 $productExecutables.Count 'Product package must contain exactly one JOEWRKS product executable.'
Assert-Equal 'Dororong.exe' $productExecutables[0].Name 'The product executable has the wrong name.'

$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($apphost)
Assert-Equal '도로롱 (Dororong)' $version.ProductName 'Product metadata name changed.'
Assert-Equal '도로롱 (Dororong)' $version.FileDescription 'Product executable file description changed.'
Assert-Equal 'Dororong.App.dll' $version.InternalName 'Product executable internal name changed.'
Assert-Equal 'Dororong.App.dll' $version.OriginalFilename 'Product executable original filename changed.'
Assert-Equal 'JOEWRKS' $version.CompanyName 'Product metadata company changed.'
Assert-Equal '0.1.0' $version.ProductVersion 'Product metadata version changed.'
Assert-Equal '0.1.0.0' $version.FileVersion 'Product file version changed.'

Add-Type -AssemblyName System.Drawing.Common
$icon = [Drawing.Icon]::ExtractAssociatedIcon($apphost)
try
{
    Assert-True ($null -ne $icon) 'Dororong.exe has no extractable associated icon.'
}
finally
{
    if ($null -ne $icon) { $icon.Dispose() }
}

$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($appDll)
Assert-Equal 'Dororong.App' $assemblyName.Name 'Internal application assembly identity changed.'
$apphostBytes = [IO.File]::ReadAllBytes($apphost)
$dllMarker = [Text.Encoding]::UTF8.GetBytes('Dororong.App.dll')
$markerFound = $false
for ($offset = 0; $offset -le $apphostBytes.Length - $dllMarker.Length; $offset++)
{
    $matches = $true
    for ($index = 0; $index -lt $dllMarker.Length; $index++)
    {
        if ($apphostBytes[$offset + $index] -ne $dllMarker[$index]) { $matches = $false; break }
    }
    if ($matches) { $markerFound = $true; break }
}
Assert-True $markerFound 'Renamed apphost is not bound to Dororong.App.dll.'

$runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
$frameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
Assert-Equal 2 $frameworks.Count 'Self-contained runtime configuration did not include both required frameworks.'
$coreFramework = @($frameworks | Where-Object name -eq 'Microsoft.NETCore.App')
$desktopFramework = @($frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')
Assert-Equal 1 $coreFramework.Count 'Microsoft.NETCore.App was not bundled exactly once.'
Assert-Equal 1 $desktopFramework.Count 'Microsoft.WindowsDesktop.App was not bundled exactly once.'
Assert-Equal $RuntimeVersion $coreFramework[0].version 'Bundled Core runtime patch changed.'
Assert-Equal $RuntimeVersion $desktopFramework[0].version 'Bundled Desktop runtime patch changed.'

$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
Assert-True ($deps.runtimeTarget.name -like '*/win-x64') 'Dependency manifest is not targeted to win-x64.'
$libraries = @($deps.libraries.PSObject.Properties.Name)
Assert-True ($libraries -contains "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/$RuntimeVersion") 'Dependency manifest does not identify the pinned Core runtime pack.'
Assert-True ($libraries -contains "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/$RuntimeVersion") 'Dependency manifest does not identify the pinned Desktop runtime pack.'

$nugetRoot = Get-NuGetRoot
$runtimePack = Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion/runtimes/win-x64/native"
$desktopPack = Join-Path $nugetRoot "microsoft.windowsdesktop.app.runtime.win-x64/$RuntimeVersion/runtimes/win-x64/lib/net8.0"
$hostPack = Join-Path $nugetRoot "microsoft.netcore.app.host.win-x64/$RuntimeVersion/runtimes/win-x64/native"
Assert-FileHashEqual (Get-RequiredFile $runtimePack 'coreclr.dll') $coreClr 'Packaged coreclr.dll does not match the pinned Core runtime pack.'
Assert-FileHashEqual (Get-RequiredFile $runtimePack 'hostfxr.dll') $hostFxr 'Packaged hostfxr.dll does not match the pinned Core runtime pack.'
Assert-FileHashEqual (Get-RequiredFile $desktopPack 'PresentationFramework.dll') $presentationFramework 'Packaged PresentationFramework.dll does not match the pinned Desktop runtime pack.'
$hostPackApphost = Get-RequiredFile $hostPack 'apphost.exe'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$matchingRidOutput = Join-Path $repositoryRoot 'tests/Dororong.App.Tests/bin/Release/net8.0-windows/win-x64'
$referenceApp = Get-RequiredFile $matchingRidOutput 'Dororong.App.dll'
$referenceCore = Get-RequiredFile $matchingRidOutput 'Dororong.Core.dll'
Assert-FileHashEqual $referenceApp $appDll 'Published Dororong.App.dll differs from the matching win-x64 test output.'
Assert-FileHashEqual $referenceCore $coreDll 'Published Dororong.Core.dll differs from the matching win-x64 test output.'

$artifactRoot = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot 'artifacts/product-shell')).Path
$candidateRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $packageRoot)).Path
$relativeCandidate = [IO.Path]::GetRelativePath($artifactRoot, $candidateRoot)
$candidateSegments = $relativeCandidate -split '[\\/]'
Assert-True ($candidateSegments.Count -eq 1 -and $candidateSegments[0].StartsWith('candidate-', [StringComparison]::Ordinal)) `
    'Validated package must be the runtime directory of a direct artifacts/product-shell/candidate-* child.'
Assert-Equal (Join-Path $candidateRoot 'runtime') $packageRoot 'Validated package directory must be named runtime.'

Add-Type -Path (Get-HostModelAssemblyPath)
$hostProof = Join-Path $candidateRoot ".hostproof-$([Guid]::NewGuid().ToString('N'))"
try
{
    New-Item -ItemType Directory -Path $hostProof | Out-Null
    $expectedApphost = Join-Path $hostProof 'Dororong.exe'
    [Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost(
        $hostPackApphost,
        $expectedApphost,
        'Dororong.App.dll',
        $true,
        $appDll,
        $false,
        $false,
        $null)
    Assert-FileHashEqual $expectedApphost $apphost 'Product apphost does not exactly match the pinned 8.0.31 host pack with candidate resources.'
}
finally
{
    if (Test-Path -LiteralPath $hostProof)
    {
        $resolvedHostProof = (Resolve-Path -LiteralPath $hostProof).Path
        $hostProofRelative = [IO.Path]::GetRelativePath($candidateRoot, $resolvedHostProof)
        $hostProofSegments = $hostProofRelative -split '[\\/]'
        Assert-True ($hostProofSegments.Count -eq 1 -and $hostProofSegments[0].StartsWith('.hostproof-', [StringComparison]::Ordinal)) `
            "Refusing recursive cleanup of unexpected host-proof path: $resolvedHostProof"
        Remove-Item -LiteralPath $resolvedHostProof -Recurse -Force
    }
}

$roundTrip = Join-Path $candidateRoot ".roundtrip-$([Guid]::NewGuid().ToString('N'))"
try
{
    Expand-Archive -LiteralPath $archive -DestinationPath $roundTrip
    $packageFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | ForEach-Object {
        [pscustomobject]@{ Relative = [IO.Path]::GetRelativePath($packageRoot, $_.FullName); Hash = Get-Hash $_.FullName }
    } | Sort-Object Relative)
    $roundTripFiles = @(Get-ChildItem -LiteralPath $roundTrip -File -Recurse | ForEach-Object {
        [pscustomobject]@{ Relative = [IO.Path]::GetRelativePath($roundTrip, $_.FullName); Hash = Get-Hash $_.FullName }
    } | Sort-Object Relative)
    Assert-Equal $packageFiles.Count $roundTripFiles.Count 'Archive round-trip changed the package file count.'
    for ($index = 0; $index -lt $packageFiles.Count; $index++)
    {
        Assert-Equal $packageFiles[$index].Relative $roundTripFiles[$index].Relative 'Archive round-trip changed a relative path.'
        Assert-Equal $packageFiles[$index].Hash $roundTripFiles[$index].Hash "Archive round-trip changed $($packageFiles[$index].Relative)."
    }
}
finally
{
    if (Test-Path -LiteralPath $roundTrip)
    {
        $resolvedRoundTrip = (Resolve-Path -LiteralPath $roundTrip).Path
        $roundTripRelative = [IO.Path]::GetRelativePath($candidateRoot, $resolvedRoundTrip)
        $roundTripSegments = $roundTripRelative -split '[\\/]'
        Assert-True ($candidateRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) `
            'Round-trip cleanup candidate parent escaped artifacts/product-shell.'
        Assert-True ($roundTripSegments.Count -eq 1 -and $roundTripSegments[0].StartsWith('.roundtrip-', [StringComparison]::Ordinal)) `
            "Refusing recursive cleanup of unexpected round-trip path: $resolvedRoundTrip"
        Remove-Item -LiteralPath $resolvedRoundTrip -Recurse -Force
    }
}

$nativeEvidence = @(Invoke-NativeSmoke $apphost)
Assert-True ($nativeEvidence.Count -gt 0) 'Native smoke produced no status.'
if ($nativeEvidence.Count -gt 1)
{
    $nativeEvidence[0..($nativeEvidence.Count - 2)] | Write-Output
}
$nativeStatus = $nativeEvidence[-1]
Assert-Equal 'PASS' $nativeStatus 'Required isolated native smoke did not pass; final product validation is unverified.'
$archiveHash = Get-Hash $archive
Write-Output "PRODUCT PACKAGE PASS: win-x64 self-contained Core/Desktop/host $RuntimeVersion; exact App/Core RID hash parity; metadata/icon/apphost binding; archive SHA256 $archiveHash round-trip; native smoke $nativeStatus."
