# Dororong Desktop Pet

Dororong is a Windows desktop companion with walking, sitting, head/cheek interactions, window-edge perching and short mouse-triggered pounces.

**Status:** validated portable product candidate, not a finished installer or public release. The packaged executable is **Dororong.exe**; source builds retain the internal **Dororong.App.exe** apphost. Dororong uses a tool window, so absence from the taskbar does not mean it is stopped; use its tray menu or Task Manager's **Details** tab.

## Requirements

- Windows x64
- No separately installed .NET runtime is required for the self-contained Windows x64 candidate.
- .NET 8-compatible SDK tooling is required to restore, test, build, run from source, or create a candidate.

## Build, test, run, and publish

Run these commands from the repository root:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test tests/Dororong.Core.Tests --configuration Release --no-restore
dotnet restore tests/Dororong.App.Tests/Dororong.App.Tests.csproj --runtime win-x64 -p:RuntimeFrameworkVersion=8.0.31 -p:TargetLatestRuntimePatch=false
dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --runtime win-x64 --no-restore -p:SelfContained=true -p:RuntimeFrameworkVersion=8.0.31 -p:TargetLatestRuntimePatch=false
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
dotnet run --project src/Dororong.App/Dororong.App.csproj --configuration Release
& tools/Publish-Product.ps1 -OutputPath artifacts/product-shell/candidate-YYYYMMDD-HHMMSS
& tests/Dororong.ProductPackage.Tests.ps1 -PackagePath artifacts/product-shell/candidate-YYYYMMDD-HHMMSS/runtime -ArchivePath artifacts/product-shell/candidate-YYYYMMDD-HHMMSS/Dororong-win-x64.zip
```

The explicit self-contained win-x64 App test at .NET 8.0.31 is a required publishing prerequisite: it produces the tested `Dororong.App.dll` and `Dororong.Core.dll` under `tests/Dororong.App.Tests/bin/Release/net8.0-windows/win-x64`. Run it after the final source change, then do not rebuild those reference DLLs before publishing and validating. The product publisher checks both references before creating any candidate output, refuses an existing output directory, and creates a complete self-contained `runtime` folder plus `Dororong-win-x64.zip` under a fresh `artifacts/product-shell/candidate-*` directory. It pins the bundled Core, Windows Desktop and native host runtime to 8.0.31, retains `Dororong.App.dll` and its pack-resource identity, and names only the product apphost `Dororong.exe`. The validator requires the package App/Core bytes to match those exact tested RID references.

Run test/build commands sequentially to avoid locked WPF test assemblies. Keep the complete output folder together, including DLLs and runtime configuration. Exit any older pre-shell development copy before the first candidate launch; the current shell then limits Dororong to one process for the same user and login session, and a duplicate launch exits successfully without changing the existing window.

This is a portable candidate, not an installer. To replace it, use **종료** from the tray or character menu, extract a newer archive into a separate folder, and retain the old folder for rollback. There is no automatic update or auto-start registration.

## Interactions

- Left-click Dororong's visible body for a short click reaction.
- Grab the **head** to lift/carry and release to drop; local cheek pulling remains available. Arm, belly and rump presses do not start body dragging.
- Right-click and choose **앉아** to sit and hold position. An actual head drag releases the hold; a mere click does not. Pulling the cheek while seated stretches it and can turn the pet without moving the body.
- Bring the pointer near the pet to crouch and track it with the head, whole eyes and body direction. This replaces the older curious/startled retreat and also wakes a sleeping pet.
- Stay nearby continuously for **1.5 seconds** to trigger a short pounce. The jump travels at most 28 DIPs with a 12-DIP arc, followed by landing and **3 seconds** of upright tracking before a fresh preparation period.
- Head dragging can attach to a compatible window/taskbar edge when the foreleg readiness animation appears. Attached pets blink, allow local cheek interaction and can be picked up again by the head.
- Walking and blinking are autonomous; ordinary inactivity permits sleep.
- Right-click the visible body and choose **Exit** to close Dororong.
- The tray menu shows **도로롱 0.1.0**, opens the local log folder, and exits through the same normal window-close path.

Transparent parts of Dororong's window are intended to pass pointer input through to the application underneath. Normal interaction avoids keyboard focus changes; the elevated-foreground input recovery path may explicitly activate the pet on an otherwise unreadable left press. Bounded privacy-filtered diagnostics are kept under `%LOCALAPPDATA%\JOEWRKS\Dororong\logs` and are not transmitted.

## Release boundaries

- App startup/fatal cleanup, platform/input, product identity, duplicate-instance, tray, diagnostic-log and self-contained package regression tests exist.
- Installation, upgrade/uninstall registration, code signing and public delivery remain release gaps. A validated portable ZIP does not satisfy them.
- Native window/taskbar support and coordinate mapping exist, but game startup, Windows Search overlays and mixed-DPI monitor transitions require separate live acceptance. Automated checks do not establish all Windows configurations.
- No repository-wide license or distribution-permission record is provided; public distribution requires an explicit rights/provenance review.

There are no settings screen, sound, auto-start, saved preferences or progression, accounts, cloud persistence, or network updater.

Current plan and verification status: [TASKS.md](TASKS.md). The approved motion study is documented in [POUNCE.md](tools/PreviewLocomotion/POUNCE.md). Earlier visual decisions and observations remain in `docs/`; they describe historical checkpoints rather than the current release status. The character's white shapes behind the rose and bow are ribbons, not a tail.
