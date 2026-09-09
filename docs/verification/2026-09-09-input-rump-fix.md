# Task Manager input and rump raster correction

## Scope

Correct two reported regressions without changing artwork assets, interaction
physics, reach, head-pull frames, landing or perch behavior. No elevation,
UIAccess flag, security-policy change, global input hook or injected input.

## Evidence

`artifacts/repro/rump-taskmgr-20260909/input-trace.log` records the original
published DLLs under a read-only diagnostic host. At 12:02:50Z, Task Manager
(PID 28272, high integrity) is foreground. WM_LBUTTONDOWN and the WPF routed
down arrive, but GetAsyncKeyState returns zero throughout; capture never starts.
With ordinary PID 2696 foreground, down is -32768, capture starts, and up is
zero. Thus the global button poll mistakes inaccessible state for release.

The first candidate returned MA_ACTIVATE instead of MA_NOACTIVATE in the
blocked case. `input-fixed.log` proves this did not recover polling on the
WS_EX_NOACTIVATE window. It is not the final implementation.

The final candidate keeps normal WM_MOUSEACTIVATE handling unchanged and
requests SetForegroundWindow for **the pet's own window**, only on a delivered
WM_LBUTTONDOWN whose global button sample is unavailable. It leaves that
message unhandled so WPF receives the same initiating click. No activation is
requested on hover, release, right-click or an ordinarily readable left press.
The request is not retried from the frame loop and never fabricates held state.

Microsoft references:

- [GetAsyncKeyState zero on UIPI failure](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate)
- [WS_EX_NOACTIVATE and explicit activation](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [SetForegroundWindow and last-input eligibility](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)

## Rump sampling

Rendering the original published BodyPullRenderer with secondary motion zero
reproduced thick, jagged rear-foot ink. Integer sampling selected the darkest
of five texels, effectively dilating outlines. RenderFlow now interpolates
premultiplied color and coverage at pixel-center-correct source coordinates.
Only the shared rump/belly sampler changed. Exact protected head/ribbon
restoration, zero pose, primary geometry and the paw path remain unchanged.

RED: complementary black/white stripe sums were zero rather than 255; a real
artwork pull from 8 to 8.01 pixels changed a channel by 150. These regressions
pass with continuous sampling. Focused body tests: 38 passed. Before/after
atlases are `rump-primary.png` and `rump-filtered.png`. User confirmed the
corrected rump outline in the desktop trial. Removing dilation makes the thin
upper outline lighter; no new outline was procedurally painted.

## Verification and delivery state

- Input regression observed RED (missing explicit activation callback), then
  GREEN; input/layer/capture subset 56 passed.
- Core 237 passed; ExactArt passed; diff whitespace check passed.
- Final App full regression: 687 passed (1m50s), combined with Core 237 = 924.
  Tested, build and published App SHA256 identities match. User confirmed the
  second candidate works. Its trace at 12:18:39Z shows WM_LBUTTONDOWN with
  async=0 / foreground Task Manager 28272, then routed-down with async=-32768 /
  foreground pet 23868. At 12:18:42Z up arrives with capture owned, followed by
  capture release. This confirms the original failing boundary now recovers.
- Independent raster review was interrupted by the review agent's usage
  limit after its preliminary checks. It is not a completed approval.
- Final candidate App SHA256:
  `9B2449014E00165E23C763EEA051320B557EF2BFAB9EE20646CFDDE3A113E74E`.
- Core SHA256:
  `C6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4`.
- Candidate directory:
  `C:/Users/tjdwo/Downloads/doro/input-rump-fixed-20260909/runtime-v2`.
- After user approval, diagnostic host PID 23868 was stopped by verified
  command-line identity. Normal `Dororong.App.exe` PID 35788 now runs the same
  runtime-v2 DLLs without the diagnostic wrapper or input logging.
- Previous runtime directories and diagnostic traces retained. No commit/push.
