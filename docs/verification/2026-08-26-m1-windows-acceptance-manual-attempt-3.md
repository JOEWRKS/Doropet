# Dororong M1 actual-Windows manual acceptance — attempt 3

## Current result

**UNVERIFIED.** This is the first manual attempt against the canonical user-supplied Dororong art build. No actual-Windows GUI item has yet been accepted for this exact artifact. The earlier brown-placeholder attempt remains separate historical evidence and is not reused for character identity or final rendering.

## Exact artifact and target

- Source commit: `673fea49488073d2d5777254c0962e567516abac`
- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Published executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- SHA-256: `8D53D1107B978325438D908CB8F3D56CB0D41C31AEE6C7664628A5D40D05F051`
- Target: Microsoft Windows 10 Pro, version `10.0.19045`, x64
- Launch identity: PID `58388`, executable path as above, command line `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe"`, start time `2026-08-26T22:16:28.6265590+09:00`
- Observation method: the user directly observes and operates the exact running artifact on the named Windows desktop, one check at a time.

## Fresh pre-launch automation

- `dotnet restore DororongDesktopPet.sln`: exit `0`.
- Release tests: `78` passed, `0` failed, `0` skipped; exit `0`.
- Release build: `0` warnings, `0` errors; exit `0`.
- Exact canonical-art and alpha-aware body-hit contract: PASS; exit `0`.
- Actual-`PetLoop` runtime-composition regression: PASS; exit `0`.
- DRAGGED angle regression: PASS; exit `0`.
- Framework-dependent `win-x64` publish: exit `0` and produced the exact artifact above.

These results establish source, asset, build, and in-process integration properties only. They do not establish actual rendered appearance, cross-process click-through, focus preservation, or end-to-end input behavior.

## Manual checks

### A. Canonical character identity and no-tail constraint

- Expected observable: the running pet is the same Dororong character as the user-supplied canonical reference, with the original pink hair, face, rose, purple bow, white ribbons, white body, and three separated visible leg silhouettes; no newly invented tail or redesigned body/hair/accessory geometry appears.
- Observed: awaiting user observation.
- Result: **UNVERIFIED**.

### Remaining actual-Windows acceptance

- Observed: not yet performed for this exact artifact.
- Result: **UNVERIFIED**.

## Automation-tool boundary

The earlier `@oai/sky` Windows 10 capture/input failure remains an automation-environment limitation. It is neither a product PASS nor a product FAIL and does not replace the direct observations in this attempt.

## User-observed outcome — attempt frozen

The user directly observed the running exact artifact and supplied `C:\Users\tjdwo\AppData\Local\Temp\codex-clipboard-ad0930a7-ef6c-46fe-a41b-670d8fc2c789.png` as focused evidence.

- Canonical character identity: **PASS** for the base character form; the user reported that the character's original form was represented correctly.
- Body/head outline consistency: **FAIL**. The user observed that the hair stroke is thin while the body outline remains visibly thicker and requested the body outline be normalized to the hair stroke weight.
- Closed-eye placement: **FAIL**. The user observed the closed-eye rendering visibly separating from the face in the supplied runtime frame.
- WALK and CLICK_REACTION motion quality: **UNVERIFIED** in this attempt; the user explicitly deferred those observations while the two static/frame defects are corrected.

Overall attempt-3 result: **FAIL** because the exact rendered artifact has two required visible-presentation defects. This attempt is frozen as the original-failure evidence and must not be promoted by source, build, or a replacement artifact.
