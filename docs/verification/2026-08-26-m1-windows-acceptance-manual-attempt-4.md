# Dororong M1 actual-Windows manual acceptance — attempt 4

## Current result

**FAIL.** The exact running artifact still renders the body outline with visibly inconsistent weight. This attempt is frozen as the original-failure evidence for the next body-outline correction and does not reuse or overwrite attempt 3.

## Exact artifact and target

- Source commit: `549521292aca4bcf71ac5f698e718ff7b5d9e378`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Executable SHA-256: `8116153A822AC44BD73E809E9300606F5EAD69B5981BC9159D2D83F18123C5D2`
- Application DLL SHA-256: `6D90EE0C71B3C4A3248883D7D9FA79F9B80F9791D6EBC717B20E4B0D91E1BAFD`
- Canonical production asset SHA-256: `F18E1F1C6FE6CDBC073EDBEFCE367E0A17E2A5AA01E92C110931C9E33C2E0DF7`
- Closed-eye asset SHA-256: `658CD15BAF6705A67BFA481421FAA1919C72778AA9EF387A09C3AE7254FD1355`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, x64
- Launch identity: PID `43104`, executable path as above, command line `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe"`, start time `2026-08-26T23:14:04.0778264+09:00`
- Observation method: the user directly observes the exact running artifact on the current Windows desktop, one check at a time.

## Fresh pre-launch verification

- `dotnet restore DororongDesktopPet.sln`: exit `0`.
- Release tests: `78` passed, `0` failed, `0` skipped; exit `0`.
- Release build: `0` warnings, `0` errors; exit `0`.
- Generator-backed exact-art contract: PASS; exit `0`.
- Actual-`PetLoop` runtime-composition regression: PASS; exit `0`.
- DRAGGED angle regression: PASS; exit `0`.
- Framework-dependent `win-x64` publish: exit `0` and produced the exact artifact above.
- Independent review of the corrected repository assets and regression tests: CLEAN.

These checks establish source, generated-asset, build, and in-process integration properties only. They do not establish the exact live Windows rendering or user acceptance.

## Manual check 1 — body outline weight

- Expected observable: the white body, rear rim, and leg contours no longer read substantially thicker than the pink hair contour; the silhouette remains continuous and no body parts disappear.
- Observed: the user reported that the body remains somewhat thick and, more importantly, that the body-line thickness does not look consistent. The user supplied exact runtime evidence at `C:\Users\tjdwo\AppData\Local\Temp\codex-clipboard-455ce401-cb9b-49cc-b9ec-a6b4a82541d2.png`, SHA-256 `5559568D1AEAB909255E09848EE631C4789314E54EAD4A1644FFA6F17983DA56`.
- Result: **FAIL**.

The recorded PID `43104` was identity-rechecked against its exact executable path and start time, stopped after the observation, and no exact-path process remained.

## Remaining checks in this correction attempt

- Closed-eye/blink integration: **UNVERIFIED**; it was not requested after the body check failed and must not be inferred from repository-asset inspection.
- WALK and CLICK_REACTION motion: explicitly deferred by the user during attempt 3; not part of this two-defect correction verdict.
- All other Milestone-1 actual-Windows acceptance items remain separately **UNVERIFIED** unless recorded by an exact-artifact observation.

## Automation boundary

The earlier GUI-automation failure remains separate from product behavior. No manual item is promoted from automation, build, test, asset inspection, or process liveness alone.
