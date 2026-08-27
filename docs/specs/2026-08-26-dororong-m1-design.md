# Dororong Desktop Pet — Milestone 1 Design

- Status: approved
- Approved: 2026-08-26
- Source-silhouette inward-outline revision approved: 2026-08-27
- Continuous subpixel-outline revision approved: 2026-08-27
- Target: Windows desktop, C# / .NET 8 / WPF
- Product personality: curious and slightly timid

## 1. Product outcome

Dororong is a small desktop companion that lives autonomously above ordinary Windows applications and responds naturally to meaningful mouse interaction without disrupting normal computer work.

Milestone 1 is successful only when a user can run the application on Windows and directly observe Dororong wandering, reacting differently to slow and fast mouse approaches, responding to a click, being dragged, sleeping after inactivity, and waking naturally. Source code or a successful build alone is not completion.

## 2. Scope

Milestone 1 includes:

- a lightweight Windows-only WPF application targeting `net8.0-windows`;
- a borderless, transparent, always-on-top character window hidden from the taskbar and Alt+Tab;
- autonomous IDLE and WALK behavior within the primary monitor work area;
- distinguishable IDLE, WALK, CURIOUS, STARTLED, CLICK_REACTION, DRAGGED, and SLEEP states;
- slow-approach, fast-approach, click, drag, inactivity, and wake interactions;
- transparent pixels that do not block input to applications underneath;
- a right-click character menu with an explicit Exit action;
- the exact user-supplied canonical Dororong raster art, with deterministic transparent and closed-eye production frames;
- a framework-dependent `win-x64` Release publish output for the .NET 8 Desktop Runtime;
- concise run, verification, limitation, and continuation documentation.

The following are out of scope: progression systems, multiple pets, accounts, cloud storage, AI conversation, weather or external services, a complex settings screen, auto-start, sound, large animation production, polished multi-monitor behavior, and a general game or plugin engine.

## 3. Solution structure

The solution has three projects and no third-party runtime framework:

- `src/Dororong.Core`: behavior state, transitions, movement, screen-boundary decisions, cooldowns, and deterministic timing/randomness seams. It does not reference WPF.
- `src/Dororong.App`: the WPF window, global cursor sampling, click/drag input, animation presentation, process lifetime, and minimal Win32 interop.
- `tests/Dororong.Core.Tests`: deterministic unit tests for the behavior core.

The UI layer owns operating-system mechanics; the core owns product behavior. The canonical raster presenter maps a core presentation result to WPF visuals without requiring changes to state-transition logic.

No MVVM framework, dependency-injection container, game engine, generic plugin system, or speculative animation framework is introduced.

## 4. Update loop and data flow

`Dororong.App` runs a UI-thread update at a nominal 33 ms interval. Each update gathers elapsed time, the primary work area, the current global cursor position when available, and any queued body input. It sends one immutable input snapshot to `Dororong.Core`.

The core returns the current state, screen position, facing direction, and presentation phase. The app applies only that result to the WPF window and character presenter. Elapsed time drives movement so a delayed frame does not change long-term speed. A single update delta is clamped to 100 ms to prevent a debugger pause or temporary stall from causing a large jump.

Click and drag events do not choose visual behavior directly. They become core input events. Random choices are supplied through a small injectable random source so production remains varied and tests remain repeatable.

## 5. State model and priority

The behavior priority is:

`DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP / IDLE / WALK`

A higher-priority event may interrupt a lower-priority autonomous state. Otherwise, a state observes its minimum duration before changing. Each triggered response consumes its event and has a cooldown so the same continuing cursor condition cannot retrigger it every update.

### Decision record — direct interaction priority (2026-08-26)

An earlier conversational draft placed STARTLED before CLICK_REACTION. During specification self-review, direct interaction was deliberately moved ahead of the automatic approach reaction and the user approved that refinement. A confirmed body click expresses clearer user intent than STARTLED, which is inferred from the same cursor movement; allowing STARTLED to win could make a deliberate click produce only a startled response. Therefore CLICK_REACTION interrupts STARTLED once release confirms a click, and a pending primary body press suspends new proximity reactions until it resolves as click or drag. With no direct body interaction, fast approach still enters STARTLED normally.

### IDLE

Dororong pauses for a randomly selected 2–5 seconds, breathes, and occasionally blinks. At the end it chooses WALK or another IDLE interval.

### WALK

Dororong walks for a randomly selected 3–7 seconds at a calm speed. It faces its direction of travel and uses a small body bob. Approaching a work-area boundary turns the heading inward; the final position is always clamped inside the work area.

### CURIOUS

When the cursor newly enters the near zone at a non-startling closing speed, Dororong looks toward it and tilts for about 1.2–2 seconds. The near zone uses separate entry and exit distances to prevent oscillation. A cursor that simply remains nearby does not repeatedly trigger CURIOUS.

### STARTLED

When the cursor closes distance quickly enough inside the reaction zone, Dororong briefly squashes, opens its eyes, and retreats a short distance away from the approach direction. The response lasts about 0.6–0.9 seconds and then resolves to IDLE. Cursor speed away from Dororong does not trigger this state.

### CLICK_REACTION

A body press followed by release without crossing the system drag threshold produces a roughly 0.5-second wakeful bounce and then IDLE. Right-click is reserved for the context menu and does not trigger this reaction.

### DRAGGED

A body press that moves beyond the Windows system drag threshold enters DRAGGED. Dororong follows the cursor while preserving the original grab offset and appears to hang slightly toward the grab point. While captured, ordinary proximity reactions are suppressed. Release ends capture, clamps the character inside the work area, plays a brief settling motion, and returns to IDLE.

### SLEEP

After 90 seconds without meaningful interaction, Dororong waits for the current autonomous action to finish and enters SLEEP from IDLE. It lowers its body, closes its eyes, and breathes slowly.

Wake transitions are explicit:

- slow new approach: `SLEEP -> CURIOUS -> IDLE`;
- fast approach: `SLEEP -> STARTLED -> IDLE`;
- press and release below the drag threshold: `SLEEP -> CLICK_REACTION -> IDLE`;
- press and movement beyond the drag threshold: `SLEEP -> DRAGGED -> IDLE`.

On mouse-down during SLEEP, the wake pose begins immediately. Release or threshold-crossing determines whether the final interaction is a click or drag. Every meaningful interaction resets the inactivity timer.

## 6. Interaction classification and tuning

Initial behavior constants live in one `BehaviorTuning` value owned by the core, not in WPF code. They include autonomous duration ranges, walk speed, near-entry and near-exit distances, fast-closing threshold, response durations, retreat distance, cooldowns, and the 90-second sleep delay.

Approach is based on the filtered rate at which the cursor-to-character distance decreases, not cursor speed alone. The initial near and fast thresholds are implementation calibration values rather than user-facing settings. They may be tuned during actual Windows observation without changing the state model or scope.

The Windows system drag distance determines click versus drag. A primary body press starts one pending direct interaction and resets the inactivity timer. While that interaction is pending, proximity reactions are suspended: crossing the drag threshold enters DRAGGED immediately, while release below the threshold emits CLICK_REACTION. This guarantees that a deliberate body click is not replaced by a simultaneous approach reaction.

## 7. Window behavior and non-interference

The WPF window uses `WindowStyle=None`, `AllowsTransparency=true`, a transparent client surface, `Topmost=true`, and tool-window behavior. It does not appear in the taskbar or Alt+Tab. It uses no filled rectangular background behind the character.

The layered window surface keeps all pixels outside the visible character at alpha zero so Windows passes input through those pixels to the application underneath. Only visible character body pixels are intentionally interactive. WPF visual hit testing alone is not treated as proof of cross-process click-through.

The window must not take keyboard focus from the active work application during ordinary body click or drag. Minimal Win32 no-activation behavior may be used for this purpose. Right-clicking the visible character opens a small WPF context menu containing Exit. Choosing Exit stops updates, releases any mouse capture, closes the window, and terminates the process.

Click-through and focus preservation are release checks on actual Windows, including transparent corners and margins while the character is in IDLE, WALK, SLEEP, and a changing animation frame.

Milestone 1 uses only the primary monitor work area and respects the taskbar boundary. Refined multi-monitor behavior is explicitly deferred.

## 8. Presentation contract

The core exposes state, facing, and normalized phase; it does not know about images, storyboards, or WPF controls. `Dororong.App` maps these values to a replaceable character presenter.

The presenter uses the exact user-supplied canonical Dororong source. Dororong has no tail; the white shapes behind the rose and bow are ribbons. The persisted `dororong-canonical-source.png` is the visual authority and remains byte-identical at 225x225 with pinned SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`; it must not be regenerated, redrawn, beautified, recolored, widened, or otherwise reinterpreted.

The generator keeps all source-authority work at 225x225: it removes only near-white background connected to the image boundary, reconstructs the body outline as specified below, derives the closed-eye state from the reviewed explicit source-scale eye stencils and lids, and then resizes both transparent frames through one deterministic high-quality premultiplied-alpha-aware raster path. Alpha-zero output pixels carry zero RGB so a transparent white or gray fringe cannot leak into later composition. The committed `dororong-canonical.png` and `dororong-closed-eyes.png` are separate 96x96, 32bpp ARGB runtime frames; a 225px production frame must not be committed or left for WPF to resize.

### Decision record — replace both failed body-outline mechanisms (2026-08-27)

Actual-Windows attempt 5 rejected a hand-authored native-96 Bezier replacement because it displaced the approved contour and introduced dark interior stroke clusters. Attempt 6 then rejected the conservative follow-up: that implementation lightened only 58 fully opaque native-96 inner-ink pixels while freezing every partially transparent and outer-support pixel. It prevented a new protruding branch, but the live body outline still read heavier and less uniform than the hair outline around the legs and valleys. Repository asset inspection had passed the narrower branch/protrusion checks, so it did not override the user's exact live failure.

Both mechanisms are superseded. The generator must not use a hand-authored replacement curve, native-96 coordinate lightening runs, per-coordinate clear patches, or a post-resize body correction. The approved replacement is a source-silhouette-derived inward outline: reconstruct the visible body contour at the 225px authority scale, rasterize one globally consistent optical stroke entirely inside the approved body silhouette, and then perform the existing single whole-frame resize to 96px. The user approved this direction as option 1 and authorized rebuilding the body-art pipeline from the source when necessary.

### Decision record — replace the rejected pixel-center distance field (2026-08-27)

The first source-silhouette implementation attempt treated selected writable pixel centers as boundary seeds and applied `clamp(width + 0.5 - distance)` at the 225px pixel grid. A contract-first implementation and an independent audit both found that no single global width could satisfy the frozen source-scale optical checks. An exhaustive `0.001..6.000` sweep had no intersection: `FirstValley` admitted only `0.001..0.184`, while the adjacent `FirstUnderside` required `1.049..1.144`. Matching the fill and outline references ideally still left disjoint intervals of `0.200..0.401` and `1.157..1.257`. The failure therefore rejected the pixel-center distance and integer-pixel normal-sum mechanism, not merely its initial width value. No candidate from that mechanism was approved or committed, and all product files were restored to the independently approved mask-authority HEAD.

The user selected the replacement direction: preserve one global source-scale width, but compute both raster coverage and optical measurement in continuous subpixel space. The approved mechanism uses the binary mask and source-derived visible contour as geometric authority, evaluates an `8x8` deterministic subpixel grid inside each affected 225px pixel, and measures fixed hair/body normals by continuous line integration. It may not fall back to the rejected pixel-center seeds, relax the accepted optical tolerances, or introduce segment-specific widths, local corrective strokes, post-resize edits, or candidate-derived reference targets.

The first continuous-subpixel feasibility sweep also returned an empty intersection, but an independent implementation reproduced the raster exactly and isolated a different authority defect: the frozen `SecondValley` measurement line never reached the approved half-pixel contour, beginning `4.057455` source pixels from it and ending `0.5` pixel away. It therefore measured retained source ink rather than a clean normal through the reconstructed stroke. The user reaffirmed that the product must use one global width derived from the head/hair outline; neither the hair target nor the body width may be varied by segment to accommodate a defective measurement line.

A body normal is valid only if it geometrically crosses the approved source-derived half-pixel contour or one legal continuation at its named visible segment, in addition to the existing single-run, light-interval, mask, junction, and protected-part checks. Crossing legacy source ink without reaching that production contour is insufficient. When independent evidence proves a pre-candidate normal invalid, the authority must return to its pre-feasibility stage: a fresh authoring pass selects replacement endpoints from the pinned source, mask, and contour only, without reading candidate pixels, passing-width intervals, or sweep scores; the corrected authority receives a new hash, overlay evidence, and independent review before any feasibility sweep is repeated. This recovery may correct measurement authority but may not change the hair median, mask, contour endpoints, fill rule, tolerance, or one-global-width requirement. If a repeated sweep using reviewed contour-crossing normals still has no intersection, no runtime candidate is permitted and the reconstruction geometry must be reconsidered rather than introducing local width corrections.

### Source-derived body reconstruction contract

The repository contains a reviewed 225x225 binary body-region mask at `src/Dororong.App/Assets/dororong-body-region-mask.png`, derived from the pinned source. Mask value `255` means that the corresponding source pixel belongs to the writable visible body region; `0` means protected or exterior, and intermediate values are forbidden. The mask is a production input with a frozen content hash in the exact-art test. It identifies only the visible white body, its existing exposed outline coverage, and the legal endpoints where that outline disappears under the hair or rear ribbon. It excludes the head, hair, face, mouth, eyes, rose, bow, ribbons, transparent exterior, and every unrelated source pixel. The mask may not invent a tail, change the three-leg/two-valley silhouette, join separated parts, expose a hidden contour, or move an occlusion endpoint.

Within that mask, the generator removes the source's variable-width body ink through a deterministic source-derived fill field and reconstructs the outline from the approved visible body boundary. Source alpha is authoritative: reconstruction may change RGB inside the reviewed body mask, including RGB carried by partially transparent body-edge pixels, but it may not change alpha at any coordinate. The body boundary and legal occlusion endpoints remain those of the pinned source; the new stroke is clipped to the body mask and grows only inward. Protected source pixels outside the mask remain byte-identical before the final whole-frame resize and are composited over the reconstructed body in their original layer order.

The subpixel contour is derived without a hand-authored body curve. A half-pixel mask edge contributes an exposed contour segment only when it separates a writable mask pixel from source-authoritative transparent exterior after boundary-background removal. Each of the two hair and rear-ribbon occlusion continuations is a straight segment from the nearest exposed contour vertex to its production-owned reviewed endpoint, with ordinal `Y,X` tie-breaking when vertices are equally near. The endpoint must be writable, source-opaque, on the mask boundary, and independently checked by the test authority. Other mask-zero contacts under protected parts do not emit hidden outline, and a continuation stops exactly at its endpoint. For every writable 225px pixel within the outline band, the generator evaluates the fixed `8x8` sample centers at offsets `((i+0.5)/8, (j+0.5)/8)`. The fraction whose Euclidean distance to the approved contour is within the one global width is that pixel's outline coverage. This area coverage blends one fill RGB with one outline RGB while preserving the original source alpha; coverage is never inferred from a candidate raster.

The body fill field comes only from the pinned source and is fixed before width calibration. An eligible seed has source alpha `255`, mask value `255`, every RGB channel at least `225`, and `max(R,G,B)-min(R,G,B) <= 8`; protected pixels and the original dark body ink are therefore ineligible. A writable pixel already meeting that rule retains its exact source RGB. Every other writable pixel within `8.0` source pixels of the approved contour receives a deterministic inverse-distance interpolation of the nearest eight eligible seeds, using weight `1/(1+d^2)`, ordinal `Y,X` tie-breaking, and nearest-integer ties-to-even channel rounding. Generation throws before writing evidence if fewer than eight eligible seeds exist, if a selected seed is invalid, or if any pixel inside the erase band cannot be assigned eight seeds. Pixels farther than `8.0` source pixels from the contour retain their exact source RGB. This preserves local white-body variation without copying one segment's fill across another or sampling the transparent background or a protected character part. The outline RGB remains the component-wise median of production-owned clean hair-outline samples. Hair, head, face, mouth, eyes, rose, bow, ribbons, no-tail geometry, part connectivity, and layer order receive no reconstruction.

Before any runtime candidate is written, a reviewed test-only authority fixes continuous normal line segments for at least six clean hair anchors and every named exposed body segment. Hair anchors cover straight, diagonal, and curved strokes without junctions or occlusions. Each body source normal geometrically crosses its named approved half-pixel contour segment or legal continuation; a line that only crosses retained legacy ink is invalid. Each normal is stored as literal rational source/native endpoints selected from the approved reference, not emitted by the generator. The test bilinearly samples alpha and RGB along each line at `1/8`-pixel spacing and integrates alpha-weighted normalized Rec. 709 luminance darkness, multiplied by sample spacing, so the result is an equivalent opaque-pixel width independent of line angle and integer-pixel phase. Source and native hair medians remain the body targets. Candidate pixels cannot define their own target, normal, fill reference, outline reference, or permitted variation.

The continuous authority and fill/contour constants are reviewed and frozen before feasibility work. A source-only feasibility gate then sweeps one global width from `0.25` through `4.00` source pixels in `1/64`-pixel increments using the exact `8x8` area-coverage rule. It must find a non-empty intersection in which every source body normal is within the existing 10% hair-median tolerance. If the intersection is empty, no runtime candidate or asset commit is permitted. If it is non-empty, one value from that intersection is frozen before candidate visual review; native tolerances and all later checks still apply. The feasibility gate may not alter normals, mask, contour endpoints, fill rules, or tolerances in response to its output.

The source-scale eye stencils remove both open eyes with gradients derived from pinned local face pixels and add the reviewed two-row lids before downsampling. Opaque `#FADCE0` fill ellipses, detached patches, lower open-eye oval remnants, and mouth changes are forbidden. Open and closed runtime alpha remains identical, and native closed-eye RGB differences remain inside the scaled eye regions plus the fixed high-quality filter support.

On the current 96-DPI / 100%-scale Windows target, WPF arranges the 96x96 resource at exactly 96x96 DIPs, centered in the unchanged 108x96 `BodyGroup`; this is a one-source-pixel-to-one-device-pixel baseline with no additional 225-to-96 bitmap resample. Other Windows DPI/scaling targets remain outside the verified M1 rendering baseline and must not inherit this 1:1 claim without target-specific inspection.

Existing presenter transforms make states visibly distinct through pose and motion:

- IDLE: breathing and blinking;
- WALK: facing and body bob;
- CURIOUS: head tilt;
- STARTLED: squash/stretch and retreat;
- CLICK_REACTION: a short bounce;
- DRAGGED: hanging stretch toward the grab point;
- SLEEP: lowered body, closed eyes, and slow breathing.

Canonical source identity, body-mask identity, real-generator/committed hash equality, native-96 dimensions and alpha-zero RGB hygiene, exact body-mask confinement, source-alpha preservation, source-boundary and occlusion-endpoint preservation, protected-pixel equality, continuous source-derived fill provenance, fixed `8x8` subpixel raster coverage, a single global body-outline width/color rule, fixed continuous clean-hair optical references, pre-candidate feasibility, identical open/closed body reconstruction, explicit scaled-eye confinement, forbidden eye-patch absence, closed-lid and mouth preservation, 96-DPI presenter arrangement, native-coordinate alpha hit testing, and presenter state-to-frame mapping are automated contracts.

The exact-art test independently integrates optical stroke coverage along the literal continuous normals for every exposed front, foot, underside, valley, center, rear-leg, and rear-rim segment. Those normals and the clean-hair anchors are fixed from the approved reference before feasibility work and are not emitted or imported by the generator. At 225px, every body sample must be within 10% of the frozen source-scale hair median. At native 96px, every body sample must be within `0.35` equivalent opaque pixel of the frozen native hair median, and the difference between the heaviest and lightest body samples must not exceed `0.50` equivalent opaque pixel. A count-only, connected-component-only, integer-pixel-sum-only, or candidate-derived test is insufficient. Tests also prove that changing the subpixel factor, global width, mask boundary, fill rule, alpha, protected RGB, or open/closed body equality causes the named contract to fail.

A candidate must be inspected source-first at the 225px production scale, at native 96px, and at nearest-neighbor enlargement on white and RGB `(18,20,28)` backgrounds. The comparison covers the full frame plus focused views of each exposed outer, foot, underside, valley, rear-rim, and legal occlusion segment. Any new or newly exposed branch or endpoint, displaced silhouette, opaque light fringe, missing span, broken leg/valley contour, protected-part damage, or visibly heavier/lighter body segment relative to the frozen hair anchors is a failure. Open and closed-eye frames receive separate native checks, and their body pixels must be identical.

Repository checks and asset inspection do not prove the exact live Windows rendering or user acceptance. The body-outline item returns to PASS only after the user directly observes the newly published exact artifact on the current 96-DPI Windows target and accepts both silhouette fidelity and stroke-weight consistency. Closed-eye integration is checked only after the live open-eye body passes. Cross-process click-through and focus preservation likewise remain actual-Windows observations.

## 9. Failure and cleanup behavior

If global cursor sampling fails for one update, the app omits pointer-derived reactions for that update and continues autonomous behavior using the last valid character state. It does not retry outside the normal next update or crash solely for that failure.

Application shutdown and unexpected UI-loop failure stop the update timer and release mouse capture in a `finally`-equivalent cleanup path. A fatal startup or rendering error is shown as an ordinary error and exits; the application must not leave an invisible topmost input window running.

The app stores no account data, external service state, or persistent progression in this milestone.

## 10. Verification strategy

### Automated core verification

Tests cover:

- deterministic IDLE/WALK timing and seeded random choices;
- minimum state duration and priority interruption;
- slow versus fast approach classification;
- near-zone hysteresis and reaction cooldowns;
- fast motion away from Dororong not causing STARTLED;
- click versus drag classification using a supplied drag threshold;
- direct click priority over a simultaneous approach reaction;
- every SLEEP wake path, including click without drag;
- drag release clamping at all four work-area edges;
- no movement beyond each work-area boundary;
- invalid or oversized elapsed-time handling.

Tests use real core objects. Operating-system input is isolated at the app boundary rather than mocked inside the behavior model.

### Actual Windows acceptance

A Release build is run on the current Windows x64 environment. Acceptance requires direct observation of:

1. a borderless transparent character above ordinary windows;
2. autonomous IDLE and WALK without continuous user input;
3. containment inside the primary work area;
4. visibly different CURIOUS and STARTLED responses to slow and fast approach;
5. a click response distinct from drag;
6. dragging with grab-offset preservation and clean release;
7. SLEEP after approximately 90 seconds and natural wake by slow approach, fast approach, click, and drag;
8. clicks on transparent pixels reaching a known control in the application underneath while body clicks do not;
9. ordinary body click and drag not stealing keyboard focus from the work application;
10. right-click Exit ending the process cleanly;
11. no rapid state flapping or repeated response while the cursor condition remains unchanged;
12. the live open-eye body outline preserving the original no-tail, three-leg/two-valley silhouette without protrusion and maintaining the frozen hair-reference optical weight across the body, followed only after that PASS by a closed-eye integration check with identical body rendering.

Motion and rendering acceptance is checked from the produced executable, not inferred from XAML or tests. Any required behavior not observed remains unverified and prevents a complete milestone claim.

## 11. Deliverables and handoff

The repository will contain the solution and three projects above, the reviewed 225px body-region mask at `src/Dororong.App/Assets/dororong-body-region-mask.png`, this specification, the JOENESS `TASKS.md` ledger, project `AGENTS.md` managed through the installed setup procedure, and a concise `README.md` with build, test, run, publish, behavior, and known-limit information.

Generated publish artifacts are not source-controlled. The documented Release publish command produces a framework-dependent `win-x64` executable for machines with the .NET 8 Desktop Runtime.

At the milestone boundary, `TASKS.md` records the actual status and evidence references. The handoff identifies implemented behavior, verified checks, remaining limitations, and shallow next candidates without adding them to Milestone 1 scope.

## 12. Definition of done

Milestone 1 is complete only when all of the following are true:

- the full automated test command exits successfully with zero failures;
- the Release build and documented `win-x64` publish command exit successfully;
- the published executable launches and exits cleanly on the current Windows environment;
- every required actual-Windows acceptance observation in section 10 is recorded as passing;
- the implementation still matches this approved scope and non-goals;
- the README and JOENESS ledger accurately describe current run commands, verified behavior, and limitations;
- no required check is unknown, stale, or inferred from build success alone.
