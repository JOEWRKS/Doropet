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

Task 10 was later rejected by direct live user observation. The user classified both the stationary full-close face and the closing/opening motion as `FAIL`; its exact artifact remains historical evidence and is not an accepted baseline.

## Task 11 — Whole-face blink states and raster-stable playback

Replace the repeatedly failed clipped-eye treatment without changing the canonical open frame, body outline, behavior-state priority, input behavior, or phase-1 provenance.

Static facial-state requirements:

- Restore the user's explicit screen-right-eye correction: the character's left closed eye is placed `3px` left of the rejected native center `39.8180538802584`, targeting `36.8180538802584` at `96x96` within the previously frozen `0.01px` measurement tolerance.
- Build each expression as a complete face state derived from the canonical source. Draw each complete eyelid as a downward-center `⌣` behind the foreground bang, then restore the canonical foreground hair last. Do not clip or inset the eyelid merely to avoid the hair silhouette.
- The two final visible lids must use the same visual language: comparable stroke weight, readable curvature, and comparable width after their different hair occlusion. The screen-right result must not collapse into the current six-pixel V-like mark.
- Preserve the canonical open frame, mouth, face boundary, hair, decorations, body, alpha, and non-eye pixels. Do not use image generation or a separately painted runtime-only PNG.

Motion requirements:

- Replace the single reused half-close asset with two distinct whole-face transition states representing approximately `70%` and `25%` openness.
- Keep both irises stationary in the face; the transition masks them vertically while independently positioned eyelids descend. Do not translate or copy the eye content horizontally.
- IDLE plays `open → 70% → 25% → closed → 25% → 70% → open`; the closed state remains for two `33ms` ticks and each intermediate is distinct and observable for at least one tick.
- Remove runtime fractional vertical scaling from IDLE and SLEEP because WPF `HighQuality` scaling resamples the one-pixel facial strokes. Preserve subtle breathing with bounded vertical translation instead. WALK, CURIOUS, STARTLED, CLICK_REACTION, and DRAGGED behavior remain unchanged.
- SLEEP continuously uses the same corrected closed face and the raster-stable breathing motion.

Verification requirements:

- Use TDD: the current Task 10 implementation must first fail focused checks for the screen-right target, complete behind-hair construction, whole-eye centroid stability, two distinct transition assets, the seven-step mapping, and absence of IDLE/SLEEP fractional vertical scaling.
- Generate native `96x96` and nearest-neighbor enlarged open/70%/25%/closed evidence, inspect the exact produced assets, then run the focused art/motion tests, existing protected-art suites, Core tests, and Release build.
- Obtain independent task review. Publish only once to a new attempt-specific path from the reviewed clean product commit, launch only after exact process identity checks, and inspect the actual WPF result. Direct user acceptance remains the final visual authority; no automated/static result can upgrade a live rejection.

Task 11 stopped without a commit after every bounded procedural candidate failed the combined visible-lid, hair-separation, and placement checks. No Task 11 runtime artifact was published or launched. The user approved replacing procedural eye generation with directly authored final-resolution face states.

## Task 12 — Direct native whole-face sprite states

Treat the final `96x96` facial sprites themselves as the editable source of truth. Do not use image generation, source-scale Bézier fitting, translated masks, optical-centroid calibration, or a separately generated runtime derivative.

Static candidate stage:

- Start from the exact canonical open `96x96` sprite. Every candidate is a complete full-sprite clone whose only changed pixels are inside the two reviewed eye/face repair regions; hair, mouth, face boundary, decoration, body, alpha, and every other pixel remain byte-identical.
- Author exactly three closed-face candidates directly on the native pixel grid. Use the user's `3px left` instruction as a geometric pixel placement for the screen-right eyelid, not as a weighted visible-ink calculation.
- Each eye must read as a shallow downward-center `⌣` at native size. The screen-right eye may be partially covered by the canonical bang, but its exposed stroke must remain readable, must not touch the hair in 8-neighbor connectivity, and must use the same stroke weight and visual language as the screen-left eye.
- Produce a frozen native full-sprite sheet and nearest-neighbor eye crops. Complete all three candidates before selection. Do not modify tracked product assets or presenter code during the candidate stage.
- Primary visual inspection selects one candidate. If none is selectable, stop without a fourth candidate.

Integration stage after selection:

- Replace the procedural closed-eye product asset with the selected native whole-face sprite source.
- Author distinct native `70%` and `25%` whole-face transition sprites from the canonical open and selected closed states. Preserve stationary iris coordinates, balanced normalized openness, canonical foreground hair, and unchanged non-eye pixels.
- Keep the Task 11 seven-step IDLE mapping and raster-stable whole-pixel IDLE/SLEEP breathing changes, while removing obsolete procedural eye-generation and old half-close assets/tests.
- Use TDD for asset identity, protected-region invariants, transition balance, seven-step frame mapping, and absence of fractional IDLE/SLEEP scale. Native appearance remains a separate visual gate.
- Run the focused art/motion tests, protected-art suites, Core tests, Release build, independent review, one new attempt-specific publish, exact runtime identity checks, and actual WPF observation. Direct user acceptance remains required.

Task 12's first direct native A/B/C batch was rejected because exact three-column-left placement plus unchanged canonical hair reduced the screen-right closed eye to a J/hook. The user selected natural visible-eye shape over the fixed `3px` value. The numerical left-shift target is no longer an acceptance requirement.

## Task 13 — Natural native closed-face selection

Create a new bounded direct-native batch with natural visible shape as the authority.

- Preserve the exact canonical open sprite, hair, mouth, face boundary, decoration, body, alpha, and all pixels outside the reviewed eye repair regions.
- Remove every open-iris remnant inside both repaired eye regions; no purple pixel may survive in a fully closed state.
- Author exactly three complete `96x96` closed-face candidates directly on the final native grid. Candidate positions may vary by at most one native column around the canonical visible screen-right eye mass; there is no fixed `3px` or optical-centroid target.
- Both final eyes must read as shallow downward-center `⌣` marks at native size, use comparable widths, depths, and stroke weight, remain disconnected from foreground hair in 8-neighbor connectivity, and preserve a face-color gap where the hair ends and lid begins.
- Freeze all three pixel plans before rendering. Produce native full-sprite and nearest-neighbor eye-crop sheets, complete the batch before ranking, and make no tracked product changes during selection.
- Primary native-first visual inspection selects one candidate. If none is selectable, stop without a fourth candidate.
- After selection, a separate integration task authors the native 70%/25% states, integrates the seven-step mapping and whole-pixel breathing, removes procedural eye-generation remnants, and performs the full verification/review/runtime sequence.

Task 13's batch was rejected. It incorrectly treated purple open-iris pixels near `(37,54)-(38,55)` as protected hair and anchored the right lid around `x=34..35`. Exact canonical measurement establishes the visible right iris at `x=36..43`, center `39.65`; the retained purple cluster is eye content, not hair.

## Task 14 — Corrected native eye ownership and anchor batch

Run one corrected direct-native A/B/C batch using the measured canonical eye rather than the obsolete broad hair heuristic.

- Classify every canonical purple pixel inside the right repair region as eye content and remove it in the closed state. Foreground-hair protection includes only canonical hair pixels that are non-purple and structurally connected to hair outside the repair region.
- Anchor the natural screen-right lid around the measured visible iris bounds `x=36..43`, center `39.65`, with candidate visible spans limited to `x=37..45`. Preserve at least one face-color pixel of separation from canonical hair.
- Freeze exactly three shallow one-native-pixel-dip `⌣` plans before rendering: candidates differ only by right-lid start/width within the allowed span. The screen-left closed eye stays fixed across the batch.
- Preserve alpha and every pixel outside the two reviewed eye repair regions exactly; remove all purple eye remnants; require one connected visible component per lid and no 8-neighbor hair contact.
- Produce native full-sprite and nearest-neighbor eye-crop sheets. Primary native-first inspection selects one or stops without another candidate. No tracked product changes occur during the batch.

Task 14 candidate B is the primary-selected closed face. At native size its right lid remains a readable shallow `⌣`, keeps a face-color gap from the foreground bang, and avoids the point-like result in A and the left-crowded spacing in C. Structural evidence records zero purple residue, zero protected-hair/outside-region/alpha changes, one connected component per lid, and no hair contact. Candidate B is the only authoritative Task 14 output; A and C remain rejected scratch evidence.

## Task 15 — Integrate selected native face states and raster-stable blink

Use strict TDD and integrate Task 14 candidate B as the native `96x96` closed-face source of truth. Do not reuse Task 11's rejected closed geometry or restore the retired exact `3px` target.

- Preserve the canonical open sprite and every protected pixel outside the reviewed eye repair regions byte-identically.
- Author distinct native whole-face `70%` and `25%` open sprites between the canonical open face and selected B. Keep both irises horizontally stationary; closure is vertical masking/lid progression only. Preserve canonical foreground hair in front and remove purple residue only where the closing eyelid covers eye content.
- Map IDLE to `open → 70% → 25% → closed → closed → 25% → 70% → open` at 33ms ticks. SLEEP continuously uses the selected closed face.
- Remove fractional IDLE/SLEEP vertical scaling; breathing may use whole-pixel vertical translation only. WALK, CURIOUS, STARTLED, CLICK_REACTION, DRAGGED, state priority, input behavior, and body artwork remain unchanged.
- Remove obsolete half-close resources and procedural closed-eye generation remnants. Keep only deterministic native authored runtime assets and tests that check their exact/protected identities, distinctness, iris stability, sequence mapping, SLEEP mapping, and no fractional IDLE/SLEEP scaling.
- Before publish or launch, run focused tests, protected-art suites, Core tests, and Release build, then obtain an independent review against base `a94a1b4012d9f5134e63762f2d1175eb32e70c90`. Any reviewer finding is fixed and re-reviewed. Only a clean review permits one new attempt-specific publish and exact runtime observation.

Task 15 was later rejected by direct user observation of exact runtime commit `2ea39edb864c7b939e97880a9fc40408d9dc7236`, published at `artifacts/repro/stage-a-native-blink-attempt-1/` and run as PID `36744`. A bounded 240-frame actual-desktop capture preserved under `verification/live-blink-user-fail-diagnostic-1/` reproduces the defect repeatedly. The 25%-open frame leaves disconnected purple horizontal remnants, and the closed screen-right lid sits four native rows below the screen-left lid, so the two eyes read as different expressions. Static/test PASS is withdrawn for motion acceptance; the exact live blink verdict is `FAIL`.

## Task 17 — Author coherent native blink-family candidates

Replace the failed construction method at scratch-candidate level before touching tracked product assets.

- Start from the exact canonical `96x96` open face. Freeze exactly three complete `open / 70% / 25% / closed` native candidate families before rendering; no fourth candidate or one-by-one tuning is allowed.
- Do not create intermediate faces by copying all closed pixels through a horizontal Y cutoff. Every state has an explicit whole-face eye plan: eyelid curve, face fill, eye white, iris, and hair occlusion are authored together on the native grid.
- Preserve canonical hair, mouth, face boundary, decoration, body, alpha, and every pixel outside the two reviewed eye repair regions byte-identically. Canonical foreground hair remains in front of the eyes.
- The two eyelids use the same shallow downward-center `⌣` language and comparable visible width/depth. Their final native vertical centers differ by no more than two rows, replacing the rejected four-row split while respecting the canonical open-eye offset.
- At 70%, any retained purple eye content remains a compact connected eye mass at least two pixels tall, not a detached one-row stripe. At 25%, no purple iris residue remains; the near-closed state is a coherent pair of lids one native row above or lighter than the final closed pair.
- Across open → 70% → 25% → closed, neither eye moves horizontally, lid motion is monotonic downward, foreground-hair contact does not appear, and no frame introduces a second disconnected dark eye mark.
- Produce native full-sprite sequence sheets and nearest-neighbor face crops for all three complete families. Primary inspection selects one complete family or stops without integration. No tracked product/test/presenter changes occur in this task.

## Task 18 — Integrate, review, and verify the selected blink family

The user approved retiring iris-bearing `70% / 25%` interpolation after every Task 17 family reproduced a detached purple block/dot at native size. Use strict TDD against the rejected Task 15 assets before production changes.

- The failing regression must catch the actual defect: both rejected transition assets retain purple eye content, the rejected closed pair has a four-row vertical-center split, and the old playback requires two iris-bearing intermediate resources.
- Remove `dororong-eyes-70-open.png` and `dororong-eyes-25-open.png` plus their row-cutoff authoring path. Add exactly one complete native `dororong-blink-squint.png` face and one corrected complete native closed face.
- Squint and closed contain no purple iris residue and no eye-white islands. Each eye contains one connected shallow downward-center `⌣` lid, with no second dark mark or 8-neighbor foreground-hair contact. The final left/right lid vertical centers differ by no more than two native rows. Squint uses the same fixed horizontal anchors as closed and sits exactly one native row above it.
- Preserve the canonical open sprite and every pixel outside the reviewed eye repair regions byte-identically. Restore canonical foreground hair last so it remains visibly in front.
- IDLE playback becomes `open → squint → squint → closed → closed → squint → squint → open` at 33ms probes, keeping the overall blink window readable while removing malformed interpolation. SLEEP continuously uses the same corrected closed face. Horizontal eye movement, fractional IDLE/SLEEP scaling, and unrelated behavior/input/body changes remain forbidden.
- Generate exact native and nearest-neighbor `open / squint / closed` evidence and inspect it before commit. Run focused art/motion tests, protected-art suites, Core tests, Release build, and independent task review. Only after a clean review may one new attempt-specific publish replace PID `36744`, following exact PID/path/start/command/lineage checks.
- Capture the actual desktop sequence again and inspect the exact runtime at native context and enlarged face crop. The fix is not accepted unless the original purple remnant, mismatched lid height, and malformed reverse-opening are absent; direct user acceptance remains required.

Task 18 was rejected at the live-runtime visual gate. Exact commit `d6b85cd43e0d076327e6430c5764f3ec3a2d9966` was published once to `artifacts/repro/stage-a-lid-only-blink-attempt-2/` and launched as PID `28880`. A 360-frame actual-desktop capture under `verification/live-blink-primary-inspection-1/` shows that the static one-row-deep curves do not survive as a coherent pair in context: the viewer-left lid reads as a short horizontal dash, the viewer-right lid reads as a lower hook beside the bang, and the one-row squint/closed offset is not a useful visible transition. Source/static PASS is retained only for its former contract; live blink acceptance is `FAIL` and no user acceptance request is permitted for this result.

## Task 19 — Re-author the complete lid-only face pair from the blank face

Replace Task 18's insufficient lid geometry rather than retiming or translating it.

- Use the exact canonical full sprite, the exact eye-free full-face reference from Task 17, and the exact canonical foreground-hair membership. Do not erase Task 18 lid pixels by local paint-over and do not use image generation.
- Freeze exactly three scratch-only complete `squint / closed` face pairs before rendering. Both eyes in every closed face use the same explicit pixel geometry, exact native vertical center, visible width, two-row downward-center depth, core/soft stroke language, and face-color clearance from the foreground bang. The viewer-right lid may shift horizontally only enough to preserve the hair gap and natural pair spacing.
- Squint is a separately authored, visibly distinct face state above the closed state; it must remain lid-only and iris/white-free. A simple one-row translation that looks identical at runtime is not sufficient.
- Preserve canonical hair, mouth, face boundary, decoration, body, alpha, and every pixel outside the two reviewed eye regions byte-identically. Restore the canonical foreground hair after drawing each eye state.
- Generate native full-sprite and nearest-neighbor eye-crop evidence for all three pairs. Primary visual inspection must reject any flat dash, hook/J shape, unequal height, hair collision, or imperceptible squint/closed distinction. If no complete pair passes, stop without product edits.
- After primary selection, integrate exactly one pair with TDD, update the literal art contracts, run the affected and protected suites plus Core and Release build, obtain independent review, and publish once to a new attempt-specific path only from the reviewed clean commit.
- Replace live PID `28880` only after exact PID/path/start/command/lineage readback. Capture the actual desktop sequence again. Do not report completion or request user acceptance unless primary live inspection sees a coherent `open → squint → closed → squint → open` with two aligned `⌣` lids and no hair collision or facial residue.

Task 19 scratch attempt 1 stopped at deterministic preflight because candidate B's viewer-right squint endpoint was Chebyshev-adjacent to protected bang hair. The preserved attempt was not overwritten. One evidence-based attempt-2 batch removed that contact and completed all three frozen pairs. Primary native/full-context and `8x`/`12x` inspection selected pair B: both squint lids read as matched compact downward curves, both closed lids read as matched seven-pixel-wide, two-row-deep `⌣` marks on the same native center line, and the viewer-right eye retains the largest stable face-color gap from the bang. Pair A has less hair clearance; pair C's soft endpoints lose contrast at native size. Only pair B is authoritative for integration.

## Task 20 — Integrate selected Task 19 pair and re-run the live gate

- Start with a focused RED that rejects the exact Task 18 squint/closed hashes and its unequal/one-row geometry, then integrate Task 19 attempt-2 pair B byte-for-byte as the only new product-art input.
- Update exact asset, full-region membership, hair protection, and sequence tests to require the selected pair identities, equal left/right native vertical centers, seven-pixel visible width, two-row closed depth, zero purple/white, and a distinct compact squint. Preserve canonical open, protected hair, alpha, outside-region pixels, presenter timing, SLEEP mapping, unrelated state/input/body behavior, and the absence of old 70%/25% resources.
- Run focused blink/art tests, affected protected-art suites, Core tests, Release build, `git diff --check`, and independent review. Resolve any concrete finding and re-review before runtime work.
- Publish exactly once to a new attempt-specific path from the reviewed clean commit. Read back package resource identities and exact process identity, then replace PID `28880` safely.
- Primary must capture the actual desktop blink and inspect native context plus enlarged ordered stages. Any flat dash, J/hook, vertical mismatch, hair collision, residue, or indistinguishable squint/closed transition is a live `FAIL` and must not be handed to the user as complete.

Task 20 source/static integration commit `c41691bd1600e1065433d3f6bd8f440acf733c96` passed focused/protected tests, Core `78/78`, Release build, and independent review. Publish attempt 3 is rejected evidence only: a generic Release build followed by RID publish reuse left the `win-x64` resource assembly on Task 18 bytes. One evidence-based RID-specific publish retry produced attempt 4 with exact canonical/squint/closed identities `238AC7...B511` / `615C758D...4721` / `319C3E93...DFBB` and no retired 70%/25% resources.

The exact attempt-4 runtime is PID `49424`, started `2026-08-30T00:48:38.5829190+09:00` from the attempt-4 EXE SHA-256 `D7C45FC8...D37F1D`. Primary initially marked a 360-frame capture `PASS`, but direct user observation rejected that ruling and a fresh 300-frame challenge capture reproduced the problem clearly. Frames `100..112` show the squint as two tiny `∨` marks and the closed state as a deep, square-cornered `U` pair that sits too low in the face. The former primary `PASS` is withdrawn; exact attempt-4 live blink verdict is `FAIL`.

Task 20 source/static integration consumes only Task 19 attempt-2 pair B. Product squint `615C758D82F745F41D22547B1B42DB16AAF6ACA900229F55823DFD82A6584721` and closed `319C3E931C8D9D2D32CB172B1AB7617EB7362700FC832182F9B187B0BAB9DFBB` are byte-identical to the selected scratch inputs; canonical open remains `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`. Strict RED reproduced Task 18's unequal `53.5 / 55.5` final centers, widths `6 / 5`, and one-row depth before replacement. The exact full-region contract requires equal final centers, width `7`, depth `2`, distinct compact squint, zero purple/eye-white, unchanged alpha/outside-region pixels, and preserved canonical hair. Those source/static checks remain factually GREEN, but the exact live result proves that their selected visual targets were wrong; they do not constitute acceptance.

## Task 21 — Replace the geometric eye morph with one natural authored blink face

Remove the artificial squint pose instead of trying to refine its tiny `∨` geometry.

- Reconstruct complete closed-face candidates from the eye-free full-face reference and restore canonical foreground hair last. Do not paint over Task 20 pixels and do not use image generation.
- Freeze exactly three scratch-only closed faces. Each uses one thin, shallow, rounded `⌣` per eye with equal optical height and stroke weight, no squared vertical sides, no V-shaped center, and no more than one native row of core-ink depth. Soft endpoint pixels may extend the curve without making it look thick.
- Raise the closed-eye pair relative to Task 20 so the marks occupy the natural open-eye line rather than the lower cheek area. Keep the viewer-right eye separated from the foreground bang and preserve a readable curve after hair restoration.
- Preserve canonical open, mouth, face boundary, hair, decoration, body, alpha, and all pixels outside the eye repair regions exactly. Closed candidates contain zero iris/eye-white residue and one visible component per lid.
- Primary inspects native full sprites and enlarged eye crops and selects one or stops. Reject any dash, deep U, V, hook/J, unequal height, hair collision, or low cheek placement.
- Integration removes `dororong-blink-squint.png` and its presenter/generator/test references. IDLE uses a short single authored closed-face interval and otherwise canonical open; SLEEP uses the same authored closed face. No other state/input/body behavior changes.
- After TDD, protected checks, Core, Release build, independent review, and one new attempt-specific publish, replace PID `49424` by exact identity and capture the actual desktop. Primary and user acceptance are both required; static geometry cannot upgrade a live rejection.

Task 21 produced no selectable closed face and made no product change. All three shallow-lid candidates passed their structural pixel checks, but exact native inspection showed the viewer-right line visually merging with the nearby canonical bang boundary. Candidate A is least bad but reads flat on the right; B is too high and dash-like; C extends into a longer horizontal mark. The failure is contextual grouping, not literal hair overwrite, and proves that preserving every bang pixel exactly prevents a natural thin closed eye in this face.

## Task 22 — Author the closed face and foreground bang together

Apply the user's earlier requirement that hair occlusion be handled as part of the complete closed face.

- Freeze exactly three scratch-only complete closed faces from canonical plus the eye-free face reference. Each candidate may change a minimal, explicit set of lower foreground-bang pixels inside the reviewed face overlap area in addition to the eye regions.
- Replace removed bang pixels with the exact underlying face-fill reference, then draw two shallow rounded `⌣` lids and restore every non-authorized canonical hair pixel. The goal is a real face-color separation gap, not drawing the eye on top of hair.
- Keep the hair edit visually silent during a fast blink: no chopped tip, hole, color seam, or apparent hair jump at native full-sprite size. Candidates vary only the smallest one-row/two-row clearance notch and right-lid anchor required to prevent visual grouping.
- Use one authored closed face only; no squint/intermediate resource. Preserve open, mouth, face contour, decoration, body, alpha, and all pixels outside the two eye regions plus the explicitly frozen bang-clearance coordinates exactly.
- Produce native open/closed alternating sheets, full-sprite sheets, and enlarged face/eye crops. Primary rejects any dash, U, V, hook, hair merge, low placement, or visible bang twitch. If none passes, stop without integration.
- A later integration task must add RED coverage for the exact Task 20 live failure, remove the squint resource/mapping, pin the selected closed face and the exact authorized bang delta, and pass independent static and actual-runtime gates before user review.

Task 22 stopped `UNVERIFIED` without rendering a candidate or changing product files. Exactly three coordinate plans were frozen, but attempt 1 failed before PNG output on an unsupported `HashSet` copy constructor and the single allowed evidence-based retry failed before output on a wrapper quoting error. No third renderer execution occurred. The authorized closed-face-plus-bang method remains untested; no candidate, visual verdict, or integration result exists.
