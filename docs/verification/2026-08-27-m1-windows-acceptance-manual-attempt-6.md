# M1 Windows manual acceptance — attempt 6

## Scope and status

- Purpose: verify the subtractive native-96 body correction on the current Windows PC.
- Body outline: **FAIL** — the user rejected the live stroke-weight consistency against the hair outline.
- Closed-eye integration: **UNVERIFIED** — ask only after the body outline passes.
- WALK / click motion: deferred by the user and outside this correction attempt.
- Attempt 5 remains **FAIL** for the rejected replacement-contour artifact.

Repository tests, asset inspection, process identity, and process responsiveness do not prove live rendering or user acceptance.

## Exact artifact and launch identity

- Documentation HEAD: `b2d782f83deab92f189cd36789fe0c589bd3a64b`.
- Embedded reviewed-source commit: `54485bb9498b59b55b8626325e85b0c3eb677dda`.
- Executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`.
- Executable SHA-256: `C52492395120EB6F863F2694DCC0D6FF5E13B0F9E11BE3388A1CD49CF014DDCA`.
- Exact-path process baseline before launch: `0`.
- Launched PID: `11504`.
- Executable path readback: exact match.
- Command line: `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe"`.
- Creation/start time: `2026-08-27T15:55:39.7793540+09:00` / `2026-08-27T15:55:39.7793542+09:00`.
- Initial responding readback: `True`.

## Asset identity

- Pinned 225px source SHA-256: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- Open native-96 SHA-256: `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`.
- Closed-eye native-96 SHA-256: `B2ADEC262AA15E9BE86D4DFC9A2087CE5514C06145A337715C145BF2671B12E4`.
- Reviewed body mask: `58` coordinates; SHA-256 `B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB`.

## Pre-launch verification evidence

- Release tests: `78` passed, `0` failed, `0` skipped.
- Release build: `0` warnings, `0` errors.
- Exact-art, runtime-composition, and DRAGGED-angle focused checks: PASS.
- Independent exact-asset visual review: PASS at native size and nearest-neighbor enlargement on white and RGB `(18,20,28)` backgrounds.
- Frozen-mask scoped re-review: all findings addressed; no new Critical/Important breakage.
- Final broad review: ready for manual Windows acceptance; no Critical/Important/Minor findings.

## User observations

### 1. Body outline

Status: **FAIL**

Question: Does the live body/back/leg outline follow the original shape without any stroke protruding outside, and does its thickness look reasonably consistent with the hair outline?

- User evidence: `C:\Users\tjdwo\AppData\Local\Temp\codex-clipboard-2a2fc932-8868-46f8-84a7-25e996cd1eb1.png`.
- Screenshot SHA-256: `4C18CD6E3060FEE28DA466A336D64423A7B678C49DE8E6909B36237C38475458` (`10,225` bytes).
- Observed in the exact live artifact: the body outline still reads heavier and less uniform than the hair outline, especially around the leg/valley curves. The user asked why the body could not be drawn with the same maintained thickness as the hair line.
- This live failure overrides the narrower repository visual PASS for body stroke-weight acceptance. It does not imply that the prior protruding-branch defect returned.

### 2. Closed-eye integration

Status: **UNVERIFIED**

This item is gated on a body-outline PASS and has not yet been asked.

## Cleanup

Status: complete after body-outline FAIL.

- PID `11504` was rechecked against the exact executable path, command line, and creation time; all three matched the recorded launch identity.
- Only PID `11504` was stopped.
- Exact-path survivors after cleanup: `0`.
