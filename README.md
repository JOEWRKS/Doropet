# Dororong Desktop Pet

Dororong is a small Windows desktop companion with a curious, slightly timid personality. It wanders above ordinary applications, reacts to mouse approaches, can be clicked and dragged, and goes to sleep after inactivity.

## Requirements

- Windows x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run the framework-dependent published application
- .NET 8 SDK to restore, test, build, run from source, or publish

## Build, test, run, and publish

Run these commands from the repository root:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
dotnet run --project src/Dororong.App/Dororong.App.csproj --configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

The publish command produces `artifacts/publish/win-x64/Dororong.App.exe`. The output is framework-dependent and therefore requires the .NET 8 Desktop Runtime on the target computer.

## Interactions

- Left-click Dororong's visible body for a short click reaction.
- Press the visible body and move beyond the Windows drag threshold to drag Dororong. The original grab point is preserved, and release keeps the whole pet inside the primary work area.
- Approach slowly from outside the nearby reaction zone to make Dororong curious.
- Approach quickly toward Dororong to startle it and make it retreat.
- Leave Dororong without meaningful interaction for about 90 seconds to let it sleep. A new slow approach, fast approach, click, or drag wakes it through the matching reaction.
- Right-click the visible body and choose **Exit** to close Dororong.

Transparent parts of Dororong's window are intended to pass pointer input through to the application underneath. Ordinary left-click and drag interactions are intended not to take keyboard focus away from the active work application.

## Milestone 1 boundaries

Milestone 1 uses the primary monitor's work area only; polished multi-monitor behavior is not included. The exact user-supplied 225x225 Dororong PNG remains the byte-identical visual authority. That character has no tail: the white shapes behind the rose and bow are ribbons. The generator removes only boundary-connected background, rebuilds the complete body-owned region from a reviewed source mask and smooth source-derived fill, and draws one inward exposed outline with source radius `1.5` and optical gain `2.5`. The two internal continuation records remain structural occlusion authority but render with gain `0`, so they leave no pale line inside the body. Head, face, hair, rose, bow, ribbons, eyes, mouth, alpha, and the original three-leg/two-valley silhouette are protected. Open and reviewed closed-eye frames each receive one deterministic premultiplied-alpha-aware downsample to separate native 96x96 runtime PNGs. On the current 96-DPI / 100%-scale target, WPF presents those resources at exactly 96x96 DIPs without another bitmap resize; other DPI/scaling targets remain unverified for this one-to-one rendering claim.

There are no settings screen, sound, auto-start, saved preferences or progression, accounts, cloud persistence, or polished multi-monitor behavior in this milestone.

See the [approved design](docs/specs/2026-08-26-dororong-m1-design.md), [body-outline implementation plan](docs/plans/2026-08-27-dororong-complete-body-ownership-outline.md), [attempt-8 Windows observation](docs/verification/2026-08-28-m1-windows-acceptance-manual-attempt-8.md), and [phase-1 conversation and work history](docs/handoff/2026-08-28-dororong-phase-1-conversation-and-work-history.md) for the exact boundary. Attempt 7 remains frozen **FAIL** evidence for candidate F. The replacement E-only body outline passed repository verification and the user's direct actual-Windows observation in attempt 8. Phase 1 therefore closes the body-outline correction as **PASS**, but broader Milestone 1 remains **PARTIAL**: the closed-eye expression and richer state-specific expression/motion work are the next phase, and the remaining interaction/non-interference observations stay **UNVERIFIED** until directly checked.
