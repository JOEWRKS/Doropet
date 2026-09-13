# Taskbar readiness visible but release rejected

Scope: investigate reported uncertain trigger. No product fix or rollout yet.

## Reproduction

Real DororongPresenter, PetLoop, platform runtime/engine and direct head interaction;
deterministic scene800x600 with bottom taskbar top560. Cursor stays inside screen
(maximumY599), both facings, lateral fling-60/0/+60 and release after1..20ticks.
No pointer movement or scene change between visible-ready frame and release.

- Slow vertical approach sweep: PASS, no failed visible-ready release.
- Fresh swing sweep:40/120 visible-ready releases FAIL. Each failure also reports
  CanBeginPerch=false immediately after the visible cue was rendered.
- Example Left/fling-60/hold1: windowY473, new heldSole127.422439. Its new bottom
  limit is472.577561, so Y473 is outside the band although the prior geometry
  allowed it. Release phase remainsNone. Same pattern mirrored on the other side.
- Some later misses are subpixel changes larger than1e-6, not only large swings.

Commands (Release win-x64, no restore): tests/Dororong.App.Tests project filters
Displayed_taskbar_cue_still_attaches and Bottom_cue_during_fresh_swing.
Results: tests/Dororong.App.Tests/TestResults/perch-cue-diagnosis-20260913.trx and
perch-swing-release-diagnosis-20260913.trx. The second intentionally fails as a
regression capture. No full-suite green claim after adding this known failure.

## Root cause

EdgePerch.CanBegin and TryBegin both call SelectCandidate; there are not two
independent threshold constants. The bottom-taskbar band depends on HeldSoleY.
PetLoop computes IsPerchReady with the prior measured pose, then renders the new
head swing/readiness frame. PetPlatformRuntime.AfterRender measures that new pose.
On the release tick BeginFrame/EdgePerchRuntime.Measure consume the new sole at
the still-displayed position. The previously advertised eligibility can therefore
be rejected. The scene, pointer availability and carry-release gates are valid in
the reproduction. Earlier tests held160ticks before releasing and missed the
transient; unchanged shared-selector tests also cannot expose changing geometry.

This establishes a real code path matching the report, not proof that every live
miss has this cause. Being forced near the screen bottom is additionally explained
by the existing20DIP entry band translated relative to the full held silhouette;
changing that UX range is separate from correcting contradictory feedback.

## Proposed correction boundary

Make displayed readiness and next release share a coherent geometry decision,
while still revalidating real pointer/position departure, owner/surface disappearance,
occlusion, scene/input reliability and carry eligibility. Do not simply expand
the global20DIP tolerance or blindly latch a stale target. Preserve screen clamps,
ordinary-window semantics, artwork and existing installed pointer-zone changes.

Implementation approval pending. Running/installed product not touched.
