# Smooth tracking handoff — 2026-09-13

User reported snapping into tracking on approach and instantly restoring the head
on departure. Requested smooth entry and exit without changing the behavior scope.

## Cause and changes

- The existing gaze lag was bypassed on exit: ApplyHunting stopped rendering and
  advancing gaze as soon as the outer tracking predicate became false. Its state
  then remained stale for later entry. New presenter tests reproduced three failures.
- Update now relaxes inactive gaze even without a render; the presenter displays
  that recovery until near-neutral. The behavior return and immediate preparation
  cancellation are unchanged. Active head lag remains230ms; inactive relax120ms.
  Near-rest state is snapped to exact zero below0.005 normalized channels.
- Native WPF inspection also found different ordinary/tracking neutral rasters.
  Two pixel-continuity regressions failed despite the first gaze-only fix.
  A120ms entry bridge (gaze scaled by the source mix) and160ms final source bridge
  now remove that asset pop. Exit source blending starts only after gaze is at
  rest, not while a large rotated head would expose two outlines.
- Bridge creates one premultiplied raster, preserving exact original source
  identity at each endpoint. No PNG/source artwork, pounce timing, proximity
  radii, preparation cancellation or post-pounce cooldown changed.
- Direct interaction wins immediately; partial bridge cheek capture uses exact
  displayed pixels. Fully tracked cheek/readiness keeps the existing layers.

## Verification

- Gaze regressions RED3/GREEN3, priority control passed; source bridge RED2/GREEN2.
  Initial handoff pixel jumps245568(entry),150720(exit) exceeded independent
  proportional limits. New tests require bounded first and final frame changes.
- Focused60 PASS including hunt motion, exact250ms authored crouch endpoint,
  cheek/readiness, pointer departure, priority, sleep wake and platform support.
  At32ms, the old pure frame17 expectation intentionally became60.563% source
  mixture; pose/frame timing remains unchanged and frame42 is still exact at250ms.
- Read-only review initially requested source-handoff repair; re-review approved
  with no remaining findings. Native WPF strips inspected:
  `artifacts/repro/tracking-transition-20260913/entry-bridge-final.png` and
  `artifacts/repro/tracking-transition-20260913/exit-bridge-final.png`.
  The bridge briefly softens edges but no distinct doubled contour was observed
  at proof scale. Live subjective acceptance remains user-observed.
- Candidate01 (gaze only) had full1042 PASS but was not installed due to source
  pop. Candidate02 full initially passed1042 with2 old zone tests still demanding
  immediate raster replacement; those now render real preparation and assert
  immediate upright frame156/no launch, then neutral source after visual recovery.
  Focused zone/transition27 PASS. Final full1044/1044 PASS (4m02s), zero failures
  or skips: `tests/Dororong.App.Tests/TestResults/tracking-bridge-full-verified.trx`.

## Candidate02

- ZIP `38899B09EBDA512DF862615BB4FB4948EA3CE59245B34B51C33D7AA9FEF3E248`
- App `F83D44134FAAC8498024CC4D70D81D85DC330D96D605DE6FCF12709AF3B8EED8`
- Core unchanged `19CBF7BA2085FFF980654D96B816B22C5B0205739FAFB70A17F4D840B08FD8D0`
- Core241 PASS; installer payload/refusal/build/native smoke PASS. Exact tested
  RID App/Core parity and archive round-trip PASS. Native smoke PID37756 exited
  normally; duplicate exit0. Installer SHA256:
  `D1EF8DC2177EE1C6311DF453ECDEA91783F25C323D9BA2ED4DD97B31F212C7BB`.
- Existing product was already closed. Verified backup470installation files,
  1data file, registration and Start shortcut:
  `artifacts/installer/host-update-20260913-tracking-transition-02/`.
  Installer exit0,467installed payload hashes/ownership/registration/Start link
  verified. Original data unchanged before launch. Installed PID49456 launched;
  duplicate47108 exit0, exactly one product instance, original diagnostic prefix
  retained with no new failure events. User live feel remains unverified.
- No commit/push, browser preview edits or public release.
