# Dororong Stage A Eye Geometry and Blink Recovery Plan

**Goal:** Replace the failed symmetric lid-table treatment with independently fitted left/right closed eyes, then remove the abrupt open/closed swap with one deterministic half-closed frame.

**Current boundary:** Exact attempt-3 closed asset `D0C0BE25471A92D65948132CE6154BA8AF56FF1F59FCDB8D0C4EDF64B206747A` remains `FAIL`. Do not treat its hash, coordinate tests, static review, or captured frame as visual acceptance.

## Evidence-backed diagnosis

- The canonical eyes are asymmetric. Their visible color centers are approximately source `53.845 / 93.524`; their native vertical centers are approximately `51.21 / 54.02`.
- The failed closed asset uses identical row geometry for both eyes and rigid pair translation. Its centers are source `52 / 94`, so the two eyes are wrong in different directions and cannot be repaired by another equal shift.
- At 96px, each current lid is only about six pixels wide with an optical dip near `0.45 / 0.55` pixel. It reads as a short antialiased dash rather than a clear `⌣`.
- IDLE swaps directly from open to fully closed for about 0.12 seconds with no transition frame. This amplifies the visual jump but is not a substitute explanation for the static geometry failure.
- Existing tests protect hashes, alpha, resize count, and non-eye pixels, but their rounded x-center assertion does not establish natural per-eye position, width, height, or readable curvature.

## Constraints

- Preserve the exact canonical open frame and every non-eye/alpha pixel.
- Keep the source-first deterministic art pipeline; no image-generation redraw and no independently painted runtime-only PNG.
- Treat viewer-left and viewer-right eyes as separate shapes with separate anchors, widths, heights, and curve controls.
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
