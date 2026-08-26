# Dororong M1 actual-Windows acceptance — 2026-08-26

## Overall result

**UNVERIFIED.** The corrected source passed fresh Release automation, including 78 core tests and a production-seam STA regression that executes the actual `PetLoop`. The exact new published process also launched, retained the same PID/path/command/start identity for three seconds, and was removed with no exact-path survivor. This run deliberately did not perform rendered or coordinate-input acceptance, so no GUI item is inferred from source, tests, build, publish, or process lifetime. All twelve actual-Windows checks remain UNVERIFIED; Task 8 and Milestone 1 remain incomplete.

## Evidence identity and scope

- Artifact source commit: `055f2dd989c72c1c9f82425a923376744c799c9f`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- SHA-256: `B3FEAB79AC87D7E3C5956C947159D519594B58582F851F0CADF4B84D81690602`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, build `19045`, x64
- Desktop runtime: `Microsoft.WindowsDesktop.App 8.0.19` at `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App`
- Acceptance authority: [approved design, section 10](../specs/2026-08-26-dororong-m1-design.md#10-verification-strategy) and [implementation plan, Task 8](../plans/2026-08-26-dororong-m1-implementation.md#task-8-publish-verify-the-actual-windows-experience-and-hand-off)
- Claim surfaces: the complete 144×144 transparent topmost pet window and a known control in a different process underneath
- Required states: IDLE, WALK, CURIOUS, STARTLED, CLICK_REACTION, DRAGGED, SLEEP, and one changing reaction frame

The exact-path pre-launch baseline was zero. `Start-Process -PassThru` launched one process:

- PID: `39656`
- Resolved path: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Command line: `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe" `
- Start time: `2026-08-26T20:35:51.7303810+09:00`

After three seconds, the same PID, resolved path, command line, and start time were present. No GUI claim was made from that liveness check. Cleanup re-read and matched all four identity fields before stopping PID `39656`; readback found that PID absent and zero process using the exact artifact path.

## Fresh Release automation

Commands ran from the repository root in the documented order. Every exit code was read separately.

| Command | Exit | Observed result |
|---|---:|---|
| `dotnet restore DororongDesktopPet.sln` | 0 | All projects were up to date. |
| `dotnet test DororongDesktopPet.sln --configuration Release --no-restore` | 0 | 78 passed, 0 failed, 0 skipped. |
| `dotnet build DororongDesktopPet.sln --configuration Release --no-restore` | 0 | 0 warnings and 0 errors. |
| `pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release` | 0 | The production startup boundary created/set/showed once; fatal cleanup/message/`Shutdown(1)` stayed one-shot; the actual `PetLoop` passed timer/tick, queued input, brain/window/render ordering, capture, fault, restart rejection, and idempotent continuing-cleanup checks. |
| `pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release` | 0 | `DRAGGED ANGLE PASS`; center, symmetric presenter coordinates, and clamps passed. |
| `dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64` | 0 | Runtime-specific framework-dependent publish completed and produced the SHA-256-bound executable above. |

The added core regressions cover exact mirrored boundary overshoot and split-frame equivalence, actual-time pointer observation with cadence-aware smoothing, a non-startling 500 ms/200 DIP approach, and pending WALK/STARTLED press-hold-drag continuity. These are automated implementation evidence only.

## GUI observation boundary

No GUI acceptance mechanism was run against the new artifact. The last attempted required Windows UI path remains the earlier documented environment boundary: `@oai/sky` could not capture returned Windows 10 windows (`SetIsBorderRequired ... 0x80004002`), coordinate input failed because geometry was unavailable, and the no-activate Dororong tool window was not targetable. Repeating that same failed mechanism would not provide new evidence. Selecting a materially different Windows GUI mechanism or target remains a user decision.

Because the new executable was not rendered, interacted with, or compared in representative playback, automation and launch liveness cannot upgrade any visual or interaction item.

## Twelve-check matrix

### 1. Borderless transparent pet above an ordinary application

- Expected observable: a full rendered frame shows a borderless character with alpha-transparent margins remaining visually above an ordinary application.
- Observed: the exact new process launched, but no rendered frame was inspected.
- Result: **UNVERIFIED**.

### 2. Autonomous IDLE and WALK

- Expected observable: representative playback without user input visibly includes stationary breathing/blinking and walking translation/bob.
- Observed: no rendered playback or frames were inspected; process lifetime and core tests do not identify the displayed state.
- Result: **UNVERIFIED**.

### 3. Primary-work-area containment

- Expected observable: the entire 144×144 window stays within all primary work-area edges, including the taskbar boundary, while moving and after interaction.
- Observed: mirror and clamp logic passed automated regressions, but no frame exposed pet-window edges relative to the work area or taskbar.
- Result: **UNVERIFIED**.

### 4. Slow CURIOUS versus fast STARTLED

- Expected observable: a slow new approach produces the tilted/gaze CURIOUS pose, while a fast closing approach produces a distinct squash and retreat.
- Observed: actual-time/cadence core classification passed, but no approach path was injected and no reaction frame was inspected.
- Result: **UNVERIFIED**.

### 5. Click reaction without drag

- Expected observable: one visible-body press and release below the system drag threshold produces the bounce of CLICK_REACTION and never shows the hanging DRAGGED pose.
- Observed: the actual `PetLoop` automation delivered one queued fast click to the brain, but no visible-body click or resulting frame was observed.
- Result: **UNVERIFIED**.

### 6. Drag threshold, offset, clamp, and capture release

- Expected observable: movement beyond the Windows threshold enters DRAGGED, preserves the initial grab offset, clamps the complete window on release, and leaves subsequent pointer input uncaptured.
- Observed: actual-loop automation verified one capture on DRAGGED entry, grab-offset position application, and release on exit/fault/dispose; no real pointer drag or rendered window was observed.
- Result: **UNVERIFIED**.

### 7. SLEEP after about 90 seconds

- Expected observable: after about 90 seconds without meaningful input and completion of the current autonomous action, the rendered pet lowers its body, closes its eyes, and breathes slowly.
- Observed: the new process was observed for only three seconds and no rendered state was inspected.
- Result: **UNVERIFIED**.

### 8. Four separate SLEEP wake routes

- Expected observable: independent SLEEP baselines wake through slow approach to CURIOUS, fast approach to STARTLED, click without drag to CLICK_REACTION, and drag to DRAGGED.
- Observed: no rendered SLEEP baseline or real input route was exercised against the new executable.
- Result: **UNVERIFIED**.

### 9. Alpha-zero cross-process input pass-through and visible-body blocking

- Expected observable: for IDLE, WALK, SLEEP, and one changing reaction frame, at least four alpha-zero corner/margin points each activate a known control in a different process underneath, while a visible-body pixel reaches Dororong and does not activate that control.
- Observed: no underlying control, frame-specific alpha-zero coordinate, or visible-body coordinate was exercised against the new executable.
- Result: **UNVERIFIED**.

### 10. Foreground keyboard focus preservation

- Expected observable: after an ordinary body click and after a drag, typing continues in the previously focused work application.
- Observed: no body click, drag, or before/after foreground comparison was performed against the new executable.
- Result: **UNVERIFIED**.

### 11. Right-click Exit ends the exact PID

- Expected observable: right-clicking the visible body opens the Exit menu, choosing Exit ends recorded PID `39656`, and no task-started pet process remains.
- Observed: the Exit menu was not exercised. PID `39656` was stopped only after exact PID/path/command/start revalidation, and cleanup readback found no survivor; forced exact-identity cleanup does not verify the Exit command.
- Result: **UNVERIFIED**.

### 12. No repeated CURIOUS or visible state flapping

- Expected observable: a cursor held continuously nearby triggers at most one CURIOUS response until it exits/re-enters and does not visibly flap among reaction states.
- Observed: automated latch/cooldown tests passed, but no nearby cursor condition or representative rendered motion was observed.
- Result: **UNVERIFIED**.

## Release boundary

The five Important code findings are covered by automated RED→GREEN regressions and the new artifact is source/hash/launch-bound. That does not establish any GUI acceptance item. All twelve items remain UNVERIFIED, Task 8 and Milestone 1 remain incomplete, and the same user-decision blocker remains: choose a materially different allowed Windows GUI mechanism or target that can capture the exact rendered artifact and inject coordinate input, then repeat all twelve checks, including four independent SLEEP wake baselines.
