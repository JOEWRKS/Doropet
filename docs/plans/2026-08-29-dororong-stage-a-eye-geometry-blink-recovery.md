# Dororong Stage A Eye Geometry and Blink Recovery Plan

**Goal:** Replace the failed symmetric lid-table treatment with independently fitted left/right closed eyes, then remove the abrupt open/closed swap with one deterministic half-closed frame.

**Current boundary:** Exact attempt-3 closed asset `D0C0BE25471A92D65948132CE6154BA8AF56FF1F59FCDB8D0C4EDF64B206747A`, the later attempt-1 blink artifact at clean HEAD `612f252f7f75a301c275212f4dd4185d6b75023f`, Task 7's first live occlusion artifact at HEAD `9ee97edc8dd71d183bcd48a23196014383d14929`, and Task 9's forced-left runtime remain `FAIL`. The user rejected Task 9 because its screen-right lid and the central bang still read as a hooked/merged stroke and the half-close shifted the iris sideways. Task 10 removes all eye-translation code and composes stationary per-eye facial states. The selected native full-close frame has a separate screen-right `⌣` at bounds `(38,53)-(43,56)` with `0.80394px` center dip, while half-close retains balanced iris ratios `10/24` and `17/40` without horizontal content translation. Static inspection, independent review, focused checks, full ExactArt, Core `78/78`, and Release build pass. Clean commit `19cf6c48937076522db38a3a1c81d8b6d72fa606` is published at `artifacts/repro/stage-a-natural-face-attempt-1/` and runs as PID `56644`. Computer Use could not expose the transparent WPF window as a target, so automatic live capture and direct user acceptance remain `UNVERIFIED`.

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
- Treat the canonical front hair as a foreground occlusion layer over every eye state. Any reviewed bang-fill, bang-outline, or antialias pixel inside an eye stencil must be restored byte-identically from the canonical open source after eye blanking and lid drawing, before the existing single resize. At native size, require visible foreground continuity and no lid ink over that hair; do not require byte-identical projected pixels after resampling changed neighboring eye pixels.
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

The initial A/B/C batch was invalid because PowerShell produced three-element point arrays instead of adding the requested Y offsets. One bounded correction rerun proved the fixed math reached the requested offsets, but all three corrected candidates remained too open: A measured `0.7083 / 0.5750`, while B and C measured `0.6667 / 0.6250`. Preserve both rejected batches and do not select from them.

Generate exactly three new scratch-only half-close candidates in one fixed D/E/F batch. Every candidate must use the accepted independent full-close curves, blank/draw only behind the frozen foreground hair, and restore the frozen hair pixels from canonical open after lid drawing. The observed direction is now established: a negative offset moves the lid upward and retains more iris; a positive offset moves it downward and retains less. Use these predeclared narrowed source offsets without changing them mid-batch:

- candidate D: viewer-left `-0.75px`, viewer-right `+0.50px`;
- candidate E: viewer-left `-1.00px`, viewer-right `+0.75px`;
- candidate F: viewer-left `-1.25px`, viewer-right `+1.00px`.

For each candidate, record normalized remaining-purple ratios per eye, the ratio difference, exact foreground-hair preservation at the source layer, visible foreground continuity at native size, connected lid geometry, alpha/non-eye invariants, and native open/half/full plus 8x eye diagnostics. Rank at native size first. A selectable candidate must keep each eye's normalized remaining-purple ratio between `0.30` and `0.42`, keep the left/right ratio difference at or below `0.08`, preserve every frozen source-hair coordinate exactly, and show no lid ink over the hair in the native image. The primary agent selects or rejects after inspecting the complete batch.

## Task 6 — Integrate occlusion-correct composition and readable timing

Use TDD. Before production changes, make the rejected current generator fail because it mutates frozen foreground-hair coordinates and its half-close ratios differ by `0.20`. Integrate the selected Task 5 D/E/F offset pair and frozen foreground-hair ownership into the source-first generator. Both half and full close must follow the same compositing order: canonical face/eye base, state-specific eye blank/lid, then exact foreground-hair restoration before the existing proxy-aware single resize. Task 8 later narrows the lid span while retaining those vertical offsets; its updated half-close ratios supersede the original D counts without changing the compositing order or timing.

Update IDLE to a `0.26s` sequence at the existing two-second phase: half-close for `0.08s`, full-close for `0.10s`, and half-open for `0.08s`. Focused tests must prove each half state spans at least two 33ms render ticks, SLEEP remains full closed, and all other states reset to canonical. Preserve the accepted open frame and body art exactly. Run the focused hair/half/timing checks plus ExactArt, BodyMask, ContinuousAuthority, SubpixelOutline, SleepPose, RuntimeComposition, Core, and Release build, then obtain an independent task review.

## Task 7 — New exact artifact and live occlusion observation

From the reviewed clean Task 6 HEAD, run fresh verification and one new attempt-specific publish; never overwrite the rejected attempt-1 artifact or evidence. Launch the exact new executable once and capture a stable-IDLE sequence. Inspect the full frame and an eye crop for: unchanged foreground hair across open/half/full states; lids remaining visually behind hair; balanced half-close progress; readable half-frame dwell; full-close `⌣` geometry; and clean open return. User acceptance, sustained SLEEP breathing, and click-only wake remain separate verdicts.

## Task 8 — Inset closed lids from the hair silhouette

Task 7 live inspection exposed a stricter defect than byte-preserving hair restoration: the full-close curves still ran from one dark hair boundary to the other, so antialiasing joined each lid to the surrounding silhouette. Treat the user's rejection as authoritative. Add a failing source-space regression at the four hair-adjacent lid endpoints, then inset each independent curve from its surrounding hair while keeping its final native center aligned, preserving the established shallow `⌣`, vertical asymmetry, foreground-hair bytes, one-resize pipeline, non-eye/alpha invariants, and blink timing.

The corrected source controls are viewer-left `P0=(46.00,118.80)`, `P1=(52.750,124.40)`, `P2=(59.50,119.40)` and viewer-right `P0=(89.00,125.60)`, `P1=(94.250,131.55)`, `P2=(99.50,126.50)`. The four clearance probes `(44,119)`, `(62,120)`, `(87,127)`, and `(103,126)` must remain below `0.60` optical ink in the full-close source. Final native curves must remain connected, no wider than their hair-framed apertures, and retain a `0.75..1.15px` downward-center dip. The shortened half layer is accepted when both eyes remain balanced and readable; its deterministic native purple counts are `10/24` and `17/40`.

Fresh working-tree evidence records source/native closed hashes `FBF8FAE02A6D8A80498A687C204A52F6991E6E6EE9A5A4ADBDCA1CD0507CAB2F` / `BF723702A880AF155E68945D109AB414F5984D63B21240F9D11851C9D959D54C` and source/native half hashes `5EA7B851B59D1E93F557C223F31CF09EBB584BD8BF7723CAF756C4FEBA988FA6` / `6BA677D7F6E78F349816740FEE0A734D7FAC21409D90A5EB9FE564F4761AD45C`. ExactArt measured connected native widths `8 / 7` and dips `0.9024 / 0.9175`, and exited `0`. A fresh exact executable was captured at frames `79 → 82 → 87 → 91 → 92`; primary inspection sees clean open/half/full/half/open continuity and visible gaps between both lids and the surrounding hair. Direct user acceptance, sustained SLEEP breathing, and click-only wake remain `UNVERIFIED`.

## Task 9 — Move the screen-right eye as a face-region replacement

The user rejected further independent eye-overlay adjustment and prescribed the exact correction: treat the animation as a whole face/frame operation and move the screen-right closed eye left by `3px` at the final native `96x96` size. The runtime already swaps complete character PNG frames; the actual generator defect was that its lid curve was clipped to the old eye-stencil membership, so changing only the curve coordinates could not produce the requested final movement.

Use TDD against the rejected native center `39.8180538802584`. The requested target is `36.8180538802584`. An independent review rejected the initial `0.15px` measurement tolerance because its `36.7198822798639` result represented a `3.0982px` movement while the documents called it exact. Tighten the final acceptance tolerance to `0.01px`, make that initial result fail, and retain the exact measured value in the record. For the full-close frame, blank the original screen-right eye as part of the complete frame, draw the translated eye/lid behind the hair, and restore all reviewed foreground-hair coordinates from canonical open last. Calibrate the source-space translation only from measured native output. For half-close, move the retained iris, eyelid, and eye region together to an intermediate position so the sequence is `0px → intermediate → 3px → intermediate → 0px`, rather than moving only the lid or jumping the entire `3px` at half-close.

The selected deterministic implementation uses translated source masks at `-8px` for full close and `-4px` for half close, with calibrated subpixel curve controls inside those regions. After the one-resize pipeline and foreground occlusion, the measured final screen-right closed-curve center is `36.8253641221354`. That is a `2.9926897581230px` left movement and misses the mathematical `3px` target by `0.0073102418770px`, inside the frozen `0.01px` final-native measurement bound. The screen-left eye remains unchanged. All `97` reviewed foreground-hair source coordinates are restored byte-identically after eye drawing. The canonical open frame remains hash `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`; final native full/half hashes are `1F8A50A5907D6BD926ECC4F93CD83064D8F1C862FCA457773E48FB3AFE172331` and `44339755E917FED67A41F8D6D2D9116EA8367106FA2E664BBC38F2E421AC0682`.

Focused occlusion, half-close, and eye-geometry checks pass, as do the full ExactArt suite, Release build, Core `78/78`, BlinkSequence, SleepPose, RuntimeComposition, BodyMask, ContinuousAuthority, and SubpixelOutline checks. Primary inspection of final native open/half/full frames passes for progressive position, face continuity, and hair-over-eye occlusion. The initial attempt-1 process was stopped after the stricter calibration made it stale. A new attempt-specific framework-dependent publish at `artifacts/repro/stage-a-right-eye-left-3px-attempt-2/` records EXE/App/Core hashes `3491D098...C293` / `1E476F29...A9F0` / `152D004A...1646` and is running as PID `22088`. Its live IDLE blink and direct user acceptance remain `UNVERIFIED`; the earlier attempt-1 240-frame capture observed autonomous WALK and is not reused as evidence for this final asset.

Task 9 was later rejected by direct live user observation. Its exact `3px` position target is no longer an acceptance rule and its runtime is retained only as failed historical evidence.

## Task 10 — Replace forced translation with stationary natural facial states

The user-directed correction is a complete facial-state redraw: keep the canonical open face fixed, blank and redraw only the eye content inside that face, preserve foreground hair in front, and do not translate the eye or iris between frames. TDD first proved the rejected Task 9 asset placed the screen-right closed curve outside its canonical aperture and moved the half-close lower-iris centroid by about `1.55px`.

The initial A/B/C position batch and D/E/F depth batch were rejected at native `96x96` because the central bang and screen-right lid still read as a joined hook. Candidate G separated the strokes but was too flat (`0.59775px` dip); H also failed (`0.58438px`). Column-level optical measurements identified a six-column raster-boundary effect. Candidate I keeps the endpoints clear of the bang and uses source controls `P0=(90.75,125.90)`, `P1=(96.10,133.80)`, `P2=(101.00,126.30)`. Its final screen-right curve is one connected six-column component at `(38,53)-(43,56)` with `0.80394px` downward-center dip, inside the canonical aperture `(36,50)-(43,57)`. The screen-left curve remains eight columns with `0.90243px` dip.

Remove the translated-coordinate mask, `-8/-4` offsets, content-copy branch, and the old expanded source/native test allowances. The exact reviewed source eye-change membership is `860` coordinates. Half-close keeps both eyes stationary, leaves `10/24` and `17/40` purple pixels respectively, and constrains the screen-right lower-iris centroid change to at most `0.50px`; observed change is about `0.34px`. Preserve the canonical open hash `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511` and the existing foreground-hair ownership.

Selected deterministic identities are source/native full-close `20331CACA2C171BC51A90CA4BD776AE2CA16B9E6D599AC1A3D46F2BAC1470213` / `62840545A2B6E241CF6FCE0C551617E9C8B40D35C170E7D3DDB40C9385A8F811` and source/native half-close `582A84AFDC446FEE4B0EACF662A02869570A2C9263267A390F2AC41806D59839` / `37200E62A1E2B37B00FE799FD03EB06E12D2D3906944F4F1B2B9AA00EDBF0152`. Clean commit `19cf6c48937076522db38a3a1c81d8b6d72fa606` received one framework-dependent publish at `artifacts/repro/stage-a-natural-face-attempt-1/`; EXE/App/Core SHA-256 are `5073528B...D3C7` / `FD2FB32E...EF2A` / `BEFD7272...AAB4`, and embedded open/half/full bytes match the selected runtime asset hashes. Stale PID `22088` was stopped only after exact PID/path/start/command/parent readback. The new exact executable runs as PID `56644`; its manifest is `artifacts/repro/stage-a-natural-face-attempt-1/verification/task-10-runtime-manifest.json`. Computer Use reported that the transparent WPF app did not expose a targetable window, so automatic capture is `UNVERIFIED` and no automated PASS is inferred. Directly observe a stable IDLE `open → half → full → half → open` sequence next. Static or automated PASS does not replace direct user acceptance.
