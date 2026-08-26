# Dororong M1 actual-Windows acceptance — 2026-08-26

## Overall result

**UNVERIFIED.** The fresh Release automation passed, the exact published process launched and remained alive for 178.7 seconds, and exact-identity cleanup left no task-started process. However, the required rendered and interactive observations could not be performed because `@oai/sky` could neither capture a Windows 10 window nor obtain coordinate-input geometry. No visual or interaction item below is inferred from source or automated tests.

## Evidence identity and scope

- Artifact source commit: `2ead4c3630bea8e13a5d39a5855813841f112271`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- SHA-256: `10AA0A8F8D1A2BE798AF0091901BD63A04FFF2C4A73E2E1730B0E89E552EC030`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, build `19045`, x64
- Desktop runtime: `Microsoft.WindowsDesktop.App 8.0.19` at `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App`
- Acceptance authority: [approved design, section 10](../specs/2026-08-26-dororong-m1-design.md#10-verification-strategy) and [Task 8 brief, Step 4](../../.superpowers/sdd/2026-08-26-dororong-m1-implementation/task-8-brief.md)
- Claim surfaces: the complete 144×144 transparent topmost pet window and a known control in a different process underneath
- Required states: IDLE, WALK, CURIOUS, STARTLED, CLICK_REACTION, DRAGGED, SLEEP, and one changing reaction frame

Before launch, no process with the exact published executable path was running. `Start-Process -PassThru` launched:

- PID: `57452`
- Resolved path: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Command line: `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe" `
- Start time: `2026-08-26T19:39:40.541976+09:00`

At `2026-08-26T19:42:39.2282794+09:00`, the same PID, resolved path, command line, and start time were still present, for 178.7 seconds of process lifetime. Because the Exit UI was unreachable, cleanup used `Stop-Process` only after all four identity fields were re-read and matched. Readback found PID `57452` absent and no process using the exact artifact path.

## Fresh Release automation

Commands were run from the repository root in this order, with each reported exit code read separately:

| Command | Exit | Observed result |
|---|---:|---|
| `dotnet restore DororongDesktopPet.sln` | 0 | All projects were up to date. |
| `dotnet test DororongDesktopPet.sln --configuration Release --no-restore` | 0 | 68 passed, 0 failed, 0 skipped. |
| `dotnet build DororongDesktopPet.sln --configuration Release --no-restore` | 0 | 0 warnings and 0 errors. |
| `pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release` | 0 | `RUNTIME COMPOSITION PASS`; one-shot lifecycle, cleanup, fatal boundary, input consumption, and capture transition checks passed. |
| `pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release` | 0 | `DRAGGED ANGLE PASS`; center, symmetric presenter coordinates, and clamps passed. |
| `dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64` | 0 | Runtime-specific framework-dependent publish completed and the exact executable above was present. |

## Windows observation boundary

The required `computer-use` path was initialized through `node_repl` and `@oai/sky`. A fresh Notepad window was returned by `list_apps`, and screenshot-free accessibility inspection identified its focused edit control as `1 편집 텍스트 편집 ID: 15`.

Native screenshot capture failed first for that Notepad window with `SetIsBorderRequired failed: 해당 인터페이스를 지원하지 않습니다. (0x80004002)`. After refreshing the returned app/window selection, the single allowed retry failed identically. The materially different fallback against a returned File Explorer window also failed identically. Screenshot-free accessibility remained available, but the first coordinate input failed before injection with `coordinate input geometry is unavailable`. The no-activate Dororong tool window was not returned by either `list_apps` or `list_windows`. Therefore no stale coordinates, unsupported UI mechanism, terminal UI, or inferred rendering evidence was used.

## Twelve-check matrix

### 1. Borderless transparent pet above an ordinary application

- Expected observable: a full rendered frame shows a borderless character with alpha-transparent margins remaining visually above an ordinary application.
- Observed: the exact published process launched and survived for 178.7 seconds, but `@oai/sky` returned no targetable Dororong window and failed to capture both primary and fallback ordinary application windows. No rendered pet frame was observed.
- Result: **UNVERIFIED**.

### 2. Autonomous IDLE and WALK

- Expected observable: representative playback without user input visibly includes both stationary breathing/blinking and walking translation/bob.
- Observed: no rendered playback or frames could be captured; process lifetime does not identify behavior state.
- Result: **UNVERIFIED**.

### 3. Primary-work-area containment

- Expected observable: the entire 144×144 window stays within all primary work-area edges, including the taskbar boundary, while moving and after interaction.
- Observed: no frame exposed the pet-window edges relative to the work-area or taskbar edges.
- Result: **UNVERIFIED**.

### 4. Slow CURIOUS versus fast STARTLED

- Expected observable: a slow new approach produces the tilted/gaze CURIOUS pose, while a fast closing approach produces a distinct squash and retreat.
- Observed: coordinate input geometry was unavailable before an approach path could be injected, and no reaction frames were captured.
- Result: **UNVERIFIED**.

### 5. Click reaction without drag

- Expected observable: one visible-body press and release below the system drag threshold produces the bounce of CLICK_REACTION and never shows the hanging DRAGGED pose.
- Observed: the first coordinate action failed before injection; no body click or resulting frames were observed.
- Result: **UNVERIFIED**.

### 6. Drag threshold, offset, clamp, and capture release

- Expected observable: movement beyond the Windows threshold enters DRAGGED, preserves the initial grab offset, clamps the complete window on release, and leaves subsequent pointer input uncaptured.
- Observed: `@oai/sky` could not obtain coordinate geometry, so no drag was injected and no release behavior was observed. Automated angle/capture checks are recorded above only as supporting non-GUI evidence.
- Result: **UNVERIFIED**.

### 7. SLEEP after about 90 seconds

- Expected observable: after about 90 seconds without meaningful input and completion of the current autonomous action, the rendered pet lowers its body, closes its eyes, and breathes slowly.
- Observed: the exact process remained alive for 178.7 seconds without successful pet input, but no rendered state was observable; lifetime alone does not prove SLEEP.
- Result: **UNVERIFIED**.

### 8. Four separate SLEEP wake routes

- Expected observable: independent SLEEP baselines wake through slow approach to CURIOUS, fast approach to STARTLED, click without drag to CLICK_REACTION, and drag to DRAGGED.
- Observed: SLEEP could not be visually established, and coordinate input failed before any of the four routes could be injected.
- Result: **UNVERIFIED**.

### 9. Alpha-zero cross-process input pass-through and visible-body blocking

- Expected observable: for IDLE, WALK, SLEEP, and one changing reaction frame, at least four alpha-zero corner/margin points each activate a known control in a different process underneath, while a visible-body pixel reaches Dororong and does not activate that control.
- Observed: a separate Notepad process and its edit control were identified, but no pet frame or coordinate geometry was available. No alpha-zero or visible-body point could be selected or clicked.
- Result: **UNVERIFIED**.

### 10. Foreground keyboard focus preservation

- Expected observable: after an ordinary body click and after a drag, typing continues in the previously focused work application.
- Observed: Notepad's edit control was focused before interaction, but neither body click nor drag could be injected, so the required before/after focus comparison was not performed.
- Result: **UNVERIFIED**.

### 11. Right-click Exit ends the exact PID

- Expected observable: right-clicking the visible body opens the Exit menu, choosing Exit ends recorded PID `57452`, and no task-started pet process remains.
- Observed: the context menu could not be reached because Dororong was not a returned target window and coordinate geometry was unavailable. Exact-identity forced cleanup ended PID `57452` and readback found no exact-path survivor, but that does not verify the Exit command.
- Result: **UNVERIFIED**.

### 12. No repeated CURIOUS or visible state flapping

- Expected observable: a cursor held continuously nearby triggers at most one CURIOUS response until it exits/re-enters and does not visibly flap among reaction states.
- Observed: no nearby cursor condition could be injected and no representative motion was captured.
- Result: **UNVERIFIED**.

## Release boundary

No application behavior failure was observed, so no source or test fix was justified. All twelve actual-Windows items remain unverified, which prevents an M1-complete or release-ready claim. A future run needs a Windows target on which the required UI tool can capture rendered frames and inject coordinate input into the exact published artifact; that run must repeat all twelve checks, including four independent SLEEP wake baselines.
