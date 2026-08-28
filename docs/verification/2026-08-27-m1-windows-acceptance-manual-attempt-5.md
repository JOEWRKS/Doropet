# Dororong M1 actual-Windows manual acceptance — attempt 5

## Current result

**FAIL.** The exact native-96 runtime artifact visibly renders body-contour strokes protruding into/out of the silhouette. This attempt is frozen as failure evidence and must not be overwritten by a later correction.

## Exact artifact and target

- Source commit: `226f616496059cfada59a6591e09e2dce9ad7bf3`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Executable SHA-256: `589C3AEFE9B5B6CDFDD8BDBA09A4C65D338CBF1FE83C07ED932E9A5D570CC78A`
- Application DLL SHA-256: `49DC703E4AC7993420B61F4BC55478BCE31988DF45CA7E0E097627A17A4B7655`
- Core DLL SHA-256: `AB9A9BFEC6AD0B59C1DE6CA71961CC031CE43F032AA4242BF19C375BE88DE32F`
- Exact 225x225 source asset SHA-256: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- Native-96 open-eye asset SHA-256: `4BCE82AADCCF34E72139AF2D2D98099309BE5FFF242EDBBCAAC6A0562870442D`
- Native-96 closed-eye asset SHA-256: `483E32259ED69AD362C19ED4685AE31BC21A343AB4107DD8C9D98EA1613697C6`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, x64, 96 DPI / 100% display scale baseline
- Launch identity: PID `22160`, executable path as above, command line `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe"`, start time `2026-08-27T14:23:27.9515607+09:00`
- Relaunch note: the previously recorded PID `61764` had already exited; an exact-path scan found no survivor before this fresh launch.
- Process readback: the exact-path process was responding when checked after launch.
- Observation method: the user directly observes the exact running artifact on the current Windows desktop, one check at a time.

## Fresh pre-launch verification

- `dotnet restore DororongDesktopPet.sln`: exit `0`.
- Release tests: `78` passed, `0` failed, `0` skipped; exit `0`.
- Release build: `0` warnings, `0` errors; exit `0`.
- Generator-backed exact-art contract: PASS; exit `0`.
- Exact-art native-96 evidence: `393` corrected pixels in bounds `x=17..76`, `y=58..87`; all named body profiles stayed within the pinned optical-width bands; exit `0`.
- Actual-`PetLoop` runtime-composition regression: PASS; exit `0`.
- DRAGGED angle regression: PASS; exit `0`.
- Framework-dependent `win-x64` publish: exit `0` and produced the exact artifact above.
- Independent review after the residual valley-spur correction: APPROVED. The reviewer confirmed one connected visible body contour, background-sensitive white/dark compositing checks, and no new critical or important issue in the scoped correction.

These checks establish source, generated-asset, build, and in-process integration properties only. They do not establish the exact live Windows rendering or user acceptance.

## Manual check 1 — body outline weight

- Expected observable: the white body, back, and leg contours read at a consistently thin weight comparable to the pink hair contour; the silhouette stays continuous, with no isolated dark spur in the leg valley and no missing body part.
- Observed: the user reported that body strokes visibly protrude and supplied exact runtime evidence at `C:\Users\tjdwo\AppData\Local\Temp\codex-clipboard-bdcb0341-4196-4320-884a-2a4e2eceb326.png`, SHA-256 `994CBDB7BD8F55D0105A589752C165F0F1ACE9503AEDDAE38A356877A258F6CB`. Source/baseline/corrected pixel comparison also found `57` newly darkened pixels whose complete 3x3 neighborhoods remain opaque in the resize baseline, including vertical clusters at `x=18..19,y=70..74`, `x=43..46,y=76..85`, and `x=72..73,y=60..66`; the current test suite did not reject those off-boundary/interior stroke clusters.
- Result: **FAIL**.

The recorded PID `22160` was identity-rechecked against its exact executable path and start time, stopped after the failure report, and no exact-path process remained.

## Remaining checks in this correction attempt

- Closed-eye/blink integration: **UNVERIFIED**; it will be requested only after the body-outline check is recorded.
- WALK and CLICK_REACTION motion: explicitly deferred by the user during attempt 3; not part of this two-defect correction verdict.
- All other Milestone-1 actual-Windows acceptance items remain separately **UNVERIFIED** unless recorded by an exact-artifact observation.

## Automation boundary

The earlier GUI-automation failure remains separate from product behavior. No manual item is promoted from automation, build, test, asset inspection, or process liveness alone.
