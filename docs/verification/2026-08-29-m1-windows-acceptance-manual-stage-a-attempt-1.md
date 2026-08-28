# Dororong M1 Windows Acceptance — Stage A Attempt 1

## Scope and exact identity

- Date: 2026-08-29, Asia/Seoul.
- Branch: `feature/dororong-m1-expression-animation`.
- Exact clean source HEAD: `09868681066e6669b3122595610309b5f3ec58b2`.
- Publish path: `artifacts/repro/stage-a-closed-eye-sleep/`.
- `Dororong.App.exe`: `7DB50AC1471234C1FC9B97C0EA759D8FA4FF4C74AAC4F8BBF5196CD5603C87B3`.
- `Dororong.App.dll`: `D50E038F805BEE66767DDC713A8B85B8EEFDB0A3B1910ED82E2376B8BE890E86`.
- `Dororong.Core.dll`: `2F984D8F49945B6B877A33B5425B7300D8B707AD030BCCA587AE506A26CAE549`.
- Embedded/runtime canonical frame: `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`.
- Embedded/runtime attempted closed frame: `CBBAAB05DC907B8CC2B9D863348F74AA5A0EA6E820B2EC9EC7FBC62FE337060A`.
- Controlled launch: PID `49104`, start `2026-08-29T00:43:05.3185280+09:00`, exact executable path matched the publish path above.

## Automated and asset layers

At the exact HEAD, fresh Release build, 78 / 0 / 0 core tests, ExactArt, BodyMask, ContinuousAuthority, SubpixelOutline, SleepPose, DraggedAngle, RuntimeComposition, and a single Stage-A-specific publish passed. The open frame remained exact and the attempted closed frame changed only the reviewed eye region. Those checks do not prove live Windows appearance.

## Direct user observation

The user reported:

> 눈이 이상한데. 근데 너무 빨리 지나가서 캡쳐할 수가 없음

Verdicts:

| Check | Verdict | Evidence boundary |
|---|---|---|
| Closed-eye live appearance | `FAIL` | The user directly rejected the visible eye appearance as strange. |
| Gentle SLEEP breathing | `UNVERIFIED` | The visible closed state passed too quickly for the user to capture or judge. |
| Click-only wake reaction | `UNVERIFIED` | No direct observation was reported. |

The exact moment could not be captured, so whether the fleeting frame was the roughly 0.12-second IDLE blink or a SLEEP frame immediately followed by wake remains `UNVERIFIED`. Source inspection confirms the attempted asset used `⌣` curves whose centers sat below their endpoints. That exact closed-frame treatment is rejected and will be replaced by lowered shallow `⌒` caps before another live launch.

## Capture and process boundary

Windows Computer Use listed applications and windows but did not return a targetable Dororong window, so it could not capture the transparent pet window. This automation limitation is separate from the product verdict and does not upgrade any unobserved item.

The exact task-owned process was stopped after the failure. The first stop precondition used second-level time and made no change; one evidence-based retry used the read-back millisecond start time, matched PID/path/start time, stopped PID `49104`, and confirmed it absent.

## Result

- Stage A attempt 1 closed-eye appearance: `FAIL`.
- Stage A attempt 1 breathing and click-only wake: `UNVERIFIED`.
- Overall Dororong M1: `PARTIAL`.
- Phase-1 body-outline `PASS` and provenance boundaries are unchanged.
