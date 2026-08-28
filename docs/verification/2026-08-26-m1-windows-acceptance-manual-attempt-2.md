# Dororong M1 actual-Windows manual acceptance — attempt 2

## Current result

**UNVERIFIED.** Manual observation on the current Windows PC has started. Check 1 passed for the window properties that it covers. Checks 2–12 have not yet been observed. The displayed brown character is explicitly classified as temporary mechanics-validation art, not as the intended NIKKE-derived Dororong character, so no character-identity or final-art claim is made.

## Evidence identity and target

- Artifact source commit: `055f2dd989c72c1c9f82425a923376744c799c9f`
- Verification-session repository HEAD: `307e69231a60a695344bba5f908c2ebcf289b737`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- SHA-256: `B3FEAB79AC87D7E3C5956C947159D519594B58582F851F0CADF4B84D81690602`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, x64
- Launch identity: PID `52616`, executable path as above, command line `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe"`, start time `2026-08-26T21:13:41.2173939+09:00`
- Observation method: the user directly observed and operated the exact running artifact on the named Windows desktop.

This attempt is separate from the earlier automation attempt. The `@oai/sky` Windows 10 capture/input failure remains an automation-environment limitation and is not treated as a product PASS or FAIL. Manual observations below are the only GUI acceptance evidence for this attempt.

## Check results

### 1. Borderless transparent pet above an ordinary application

- Expected observable: a full rendered frame shows a visible character with no rectangular background, title bar, or ordinary window border, and the character remains visually above an ordinary application.
- Observed: on the exact artifact, the user reported `보임 / 배경·테두리 없음 / 일반 창 위에 유지됨` and supplied a runtime screenshot showing the temporary character rendered over another application without a visible rectangular window surface or chrome.
- Result: **PASS** for visibility, borderless/transparent presentation, and topmost presentation.
- Boundary: the rendered character is the temporary brown placeholder. This check does not accept it as the intended Dororong identity or final character art.

### 2–12. Remaining actual-Windows behavior and interaction checks

- Observed: not yet performed in this manual attempt.
- Result: **UNVERIFIED**.

## Product-identity correction raised during this attempt

The user clarified that “Dororong” means the web-popular derivative character based on *Goddess of Victory: NIKKE*, not a newly invented brown mascot. The current vector character remains useful only for mechanics verification. The intended character-art source and acceptable degree of original redraw versus supplied authorized asset must be settled before final visual acceptance and before this attempt continues as a final Milestone 1 acceptance run.
