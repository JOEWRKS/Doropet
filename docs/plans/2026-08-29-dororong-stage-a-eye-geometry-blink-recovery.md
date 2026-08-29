# Dororong Stage A Eye Geometry and Blink Recovery Plan

**Goal:** Replace the failed symmetric lid-table treatment with independently fitted left/right closed eyes, then remove the abrupt open/closed swap with one deterministic half-closed frame.

**Current boundary:** Exact attempt-3 closed asset `D0C0BE25471A92D65948132CE6154BA8AF56FF1F59FCDB8D0C4EDF64B206747A` and the later attempt-1 blink artifact at clean HEAD `612f252f7f75a301c275212f4dd4185d6b75023f` remain `FAIL`. Do not treat their hashes, coordinate tests, static review, or captured frames as visual acceptance.

## Evidence-backed diagnosis

- The canonical eyes are asymmetric. Their visible color centers are approximately source `53.845 / 93.524`; their native vertical centers are approximately `51.21 / 54.02`.
- The failed closed asset uses identical row geometry for both eyes and rigid pair translation. Its centers are source `52 / 94`, so the two eyes are wrong in different directions and cannot be repaired by another equal shift.
- At 96px, each current lid is only about six pixels wide with an optical dip near `0.45 / 0.55` pixel. It reads as a short antialiased dash rather than a clear `⌣`.
- IDLE swaps directly from open to fully closed for about 0.12 seconds with no transition frame. This amplifies the visual jump but is not a substitute explanation for the static geometry failure.
- Existing tests protect hashes, alpha, resize count, and non-eye pixels, but their rounded x-center assertion does not establish natural per-eye position, width, height, or readable curvature.
- Attempt-1 proved that the eye stencils are not pure eye layers. They also contain foreground bang pixels that overlap the eyes. The current blanking pass replaces those hair pixels with interpolated face color, and the lid pass can then draw where foreground hair should occlude it. The right outer bang is visibly damaged in half/full-close frames; changed source examples include `(99,114)`, `(100,114)`, `(101..103,115)`, `(102,116)`, and `(106,125..132)`.
- Attempt-1 also reintroduced equal treatment in the half-close layer: the same source `-3.0px` vertical offset is applied to both asymmetric eyes. At native size it retains `6/24 = 25%` of the viewer-left purple iris but `18/40 = 45%` of the viewer-right iris, so the two eyes do not close at the same normalized progress.
- The captured attempt-1 half frames persisted for only about `40ms` while closing and `50ms` while opening around an `83ms` full-close dwell. The existing phase test proves frame selection, not that an intermediate frame remains readable for at least two 33ms render ticks.

## Constraints

- Preserve the exact canonical open frame and every non-eye/alpha pixel.
- Keep the source-first deterministic art pipeline; no image-generation redraw and no independently painted runtime-only PNG.
- Treat viewer-left and viewer-right eyes as separate shapes with separate anchors, widths, heights, and curve controls.
- Treat the canonical front hair as a foreground occlusion layer over every eye state. Any reviewed bang-fill, bang-outline, or antialias pixel inside an eye stencil must remain byte-identical to the canonical open frame after eye blanking, lid drawing, proxy substitution, and resize.
- Judge candidates at native 96x96 first. Enlargement is diagnostic only.
- Do not change core behavior states, priority, SLEEP timing, click/drag semantics, window input behavior, or phase-1 provenance.
- Do not publish into `artifacts/publish/win-x64`, push, create a PR, or merge.

## Task 1 — Bounded static candidate batch

Create exactly three scratch-only candidates from the same clean base. Use a deterministic subpixel curve raster with separate per-eye control data rather than literal symmetric row runs.

All three candidates must:

- follow each canonical eye's visible aperture and hair occlusion independently;
- cover roughly the visible eye width rather than the former six-pixel dash;
- retain a readable shallow `⌣` at native size with about one native pixel of center dip;
- preserve the eye-removal stencil boundary, alpha, mouth, face, hair, decorations, and body exactly;
- include native open/closed and 8x nearest-neighbor evidence in one labeled comparison sheet.

Complete the three-candidate batch before changing any candidate variable. The primary agent selects or rejects a candidate using the frozen observables above. No production file changes, publish, or launch occur during this task.

## Task 2 — Integrate the selected closed-eye curve

Use TDD. Replace the rounded symmetric-center assertion with per-eye contracts that measure the final native artifact:

- horizontal and vertical alignment against each eye's visible canonical aperture;
- final visible width and connectedness;
- final center dip sufficient to read as `⌣` without a V or deep bowl;
- left/right controls and measurements are independent;
- exact non-eye, alpha, open-frame, resize, and protected-art invariants remain enabled.

Integrate only the selected deterministic curve data/raster method, generate one production closed asset, run the focused eye test plus ExactArt, BodyMask, ContinuousAuthority, SubpixelOutline, and Release build, then obtain independent review. Static visual acceptance remains separate from live acceptance.

## Task 3 — Add one half-closed transition frame

After the full closed frame passes static review, generate one deterministic `dororong-half-closed-eyes.png` from the same per-eye geometry. It must show the lids descending while retaining a small lower iris crescent; it must not be a whole-frame opacity blend.

Update the presenter so IDLE uses a short sequence `open → half → closed → half → open` over roughly 0.16–0.20 seconds. SLEEP continues to use the full closed frame. Add focused presenter tests for phase-to-frame mapping and preserve all wake-state reset checks.

## Task 4 — Exact artifact and live observation

From the reviewed clean HEAD, run fresh Release build/tests and one attempt-specific publish. Launch that exact executable once, bind it to PID/path/start time, and capture a stable-IDLE sequence. Judge separately:

- closed-eye static geometry;
- open/half/closed continuity;
- clean return to the canonical open frame;
- sustained SLEEP breathing;
- click-only wake reaction.

Any direct user rejection overrides automated, static, reviewer, or primary-agent PASS. Overall M1 remains `PARTIAL` until the remaining roadmap acceptance checks are observed.

## Task 5 — Freeze foreground-hair ownership and corrected scratch candidates

Work from clean rejected HEAD `612f252f7f75a301c275212f4dd4185d6b75023f` without tracked production/test/document edits. Derive a deterministic reviewed foreground-hair mask from the canonical open source, limited to bang fill, outline, and antialias pixels that visibly continue into hair outside the two eye stencils. Freeze the exact coordinate runs and a content hash before generating candidates. The mask must not claim iris, sclera, face, cheek, or mouth pixels.

Generate exactly three scratch-only half-close candidates in one batch. Every candidate must use the accepted independent full-close curves, blank/draw only behind the frozen foreground hair, and restore the frozen hair pixels from canonical open after lid drawing. Use these predeclared independent source offsets without changing them mid-batch:

- candidate A: viewer-left `-4.0px`, viewer-right `-2.0px`;
- candidate B: viewer-left `-3.75px`, viewer-right `-2.25px`;
- candidate C: viewer-left `-3.5px`, viewer-right `-2.5px`.

For each candidate, record normalized remaining-purple ratios per eye, the ratio difference, exact foreground-hair preservation at source and native layers, connected lid geometry, alpha/non-eye invariants, and native open/half/full plus 8x eye diagnostics. Rank at native size first. A selectable candidate must keep each eye's normalized remaining-purple ratio between `0.30` and `0.42`, keep the left/right ratio difference at or below `0.08`, and show no lid ink over a frozen foreground-hair coordinate. The primary agent selects or rejects after inspecting the complete batch.

## Task 6 — Integrate occlusion-correct composition and readable timing

Use TDD. Before production changes, make the rejected current generator fail because it mutates frozen foreground-hair coordinates and its half-close ratios differ by `0.20`. Integrate the selected Task 5 offsets and frozen foreground-hair ownership into the source-first generator. Both half and full close must follow the same compositing order: canonical face/eye base, state-specific eye blank/lid, then exact foreground-hair restoration before the existing proxy-aware single resize.

Update IDLE to a `0.26s` sequence at the existing two-second phase: half-close for `0.08s`, full-close for `0.10s`, and half-open for `0.08s`. Focused tests must prove each half state spans at least two 33ms render ticks, SLEEP remains full closed, and all other states reset to canonical. Preserve the accepted open frame and body art exactly. Run the focused hair/half/timing checks plus ExactArt, BodyMask, ContinuousAuthority, SubpixelOutline, SleepPose, RuntimeComposition, Core, and Release build, then obtain an independent task review.

## Task 7 — New exact artifact and live occlusion observation

From the reviewed clean Task 6 HEAD, run fresh verification and one new attempt-specific publish; never overwrite the rejected attempt-1 artifact or evidence. Launch the exact new executable once and capture a stable-IDLE sequence. Inspect the full frame and an eye crop for: unchanged foreground hair across open/half/full states; lids remaining visually behind hair; balanced half-close progress; readable half-frame dwell; full-close `⌣` geometry; and clean open return. User acceptance, sustained SLEEP breathing, and click-only wake remain separate verdicts.
