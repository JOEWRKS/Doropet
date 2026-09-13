# Build, test and package

Run from the repository root on Windows x64 with a .NET 8-compatible SDK.
Run build/test commands sequentially to avoid WPF assembly locks.
저장소 루트에서 실행하며, WPF 파일 잠금 방지를 위해 빌드·검사는 순서대로 실행합니다.

## Source and tests

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test tests/Dororong.Core.Tests --configuration Release --no-restore
dotnet restore tests/Dororong.App.Tests/Dororong.App.Tests.csproj --runtime win-x64 -p:RuntimeFrameworkVersion=8.0.31 -p:TargetLatestRuntimePatch=false
dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --runtime win-x64 --no-restore -p:SelfContained=true -p:RuntimeFrameworkVersion=8.0.31 -p:TargetLatestRuntimePatch=false
```

To run from source (not a packaging prerequisite):

```powershell
dotnet run --project src/Dororong.App/Dororong.App.csproj --configuration Release
```

Source builds use the internal apphost `Dororong.App.exe`; packaged builds use
`Dororong.exe`. Exit an existing pet through its tray before launching a source
build. Absence from the taskbar does not mean the pet has stopped.

## Portable package

The explicit self-contained App test above is a publishing prerequisite.
The publisher verifies App/Core DLLs against those tested RID outputs.
Do not rebuild reference DLLs between testing, publishing and validation.
Choose a new, unused candidate directory.

```powershell
pwsh -NoProfile -File tools/Publish-Product.ps1 -OutputPath artifacts/product-shell/candidate-NEW
pwsh -NoProfile -File tests/Dororong.ProductPackage.Tests.ps1 -PackagePath artifacts/product-shell/candidate-NEW/runtime -ArchivePath artifacts/product-shell/candidate-NEW/Dororong-win-x64.zip
```

The complete runtime folder is required; distributing only the EXE will not work.
Runtime and native host versions are pinned to 8.0.31 by the packaging tools.

## Installer and verification

The latest locally verified candidate is `candidate-20260914-idle-blink-01`.
See [delivery evidence](verification/2026-09-14-idle-blink-rump.md) for hashes,
test counts and installed update/startup results. Local `artifacts/` outputs
are intentionally not committed and are not downloadable release assets.

Installer builds require the verified payload archive and pinned Inno Setup
compiler. Existing toolchain/payload paths are local prerequisites, not files
included by cloning this repository. The builder pins the payload SHA256 and
refuses an existing output directory.

```powershell
pwsh -NoProfile -File tools/Build-Installer.ps1 -PackagePath artifacts/product-shell/candidate-20260914-idle-blink-01/runtime -ArchivePath artifacts/product-shell/candidate-20260914-idle-blink-01/Dororong-win-x64.zip -CompilerPath artifacts/installer/toolchain-inno-7.1.0-x64-20260912-02/ISCC.exe -OutputPath artifacts/installer/candidate-NEW
```

Exit the pet before package/installer validation: it includes a strict
single-instance application smoke test. Historical Sandbox handoffs retain
older candidate pins and do not validate the current build.

## Installation, support and release limits

- Per-user install: `%LOCALAPPDATA%\Programs\JOEWRKS\Dororong`.
- Local diagnostics: `%LOCALAPPDATA%\JOEWRKS\Dororong\logs`; no automatic upload.
- Start-menu shortcut is installed; desktop shortcut and launch-after-install
  are opt-in. Silent installation does not launch the pet.
- Exit normally before replacing or uninstalling. Retain the previous portable
  folder for rollback. Portable copies are not imported or removed by the installer.
- Same-version installed update, payload parity, original-data preservation,
  startup and duplicate prevention were verified for the latest candidate.
- The installer is unsigned. Clean-machine, real cross-version and failure-path
  acceptance, mixed-DPI/live Windows coverage and rights review remain release gates.
  No power-loss rollback guarantee is made.
- For support, provide the candidate hash, Windows version, reproduction steps
  and reviewed logs. Installer logging: `/LOG="C:\your-folder\dororong-setup.log"`.

설치·테스트 성공은 모든 Windows 환경의 동작이나 캐릭터 재배포 권한을 보장하지 않습니다.
공개 배포 전 남은 검증과 권리 확인이 필요합니다.
