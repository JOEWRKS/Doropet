# Dororong Desktop Pet — Milestone 1 Design

- Status: approved
- Approved: 2026-08-26
- Subtractive body-outline revision approved: 2026-08-27
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

The generator keeps all source-authority work at 225x225: it removes only near-white background connected to the image boundary, derives the closed-eye state from the reviewed explicit source-scale eye stencils and lids, and then resizes both transparent frames through one deterministic high-quality premultiplied-alpha-aware raster path. Alpha-zero output pixels carry zero RGB so a transparent white or gray fringe cannot leak into later composition. The committed `dororong-canonical.png` and `dororong-closed-eyes.png` are separate 96x96, 32bpp ARGB runtime frames; a 225px production frame must not be committed or left for WPF to resize.

Body-outline normalization occurs only after that resize and is strictly subtractive. Actual-Windows attempt 5 rejected the hand-authored native-96 Bezier redraw because it displaced the approved source contour and introduced dark stroke clusters inside the body; that redraw, its round-cap endpoints, and coordinate-by-coordinate auxiliary clear paths are no longer permitted. The generator must start from the uncorrected deterministic 96px resize and may only lighten selected existing body-outline ink on the body-interior side through one reviewed, explicit native-96 edit mask. It must never add a pixel darker than the corresponding uncorrected baseline pixel, move or replace the outer silhouette, or change alpha.

Every body-correction coordinate itself must be fully opaque in the uncorrected native-96 baseline and must belong to a reviewed mask of existing baseline body ink. All partially transparent pixels and a separately frozen fixture of source-derived outer-edge RGB support are immutable. A replacement color must be a deterministic blend from that coordinate's baseline RGB toward an explicitly reviewed local body-fill RGB; each corrected RGB channel must be greater than or equal to its baseline channel, and no RGB coordinate outside the reviewed mask may change. The reviewed mask and fill samples are fixed only after native-scale inspection on both white and dark backgrounds and are applied identically to open- and closed-eye frames. Hair, head, face, mouth, eyes, rose, bow, ribbons, no-tail geometry, part connectivity, and layer order receive no local redraw; their only permitted production difference from the 225px authority is the deterministic whole-frame resize and, for the closed frame, the reviewed eye derivation.

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

Canonical source identity, real-generator/committed hash equality, native-96 dimensions and alpha-zero RGB hygiene, exact body-mask confinement, channel-wise zero body darkening relative to the deterministic resize baseline, reviewed body-fill blend provenance, full-opacity eligibility of every body change, immutable partial-alpha and frozen outer-edge pixels, alpha preservation, identical open/closed body correction, fixed clean-hair optical references, explicit scaled-eye confinement, forbidden eye-patch absence, closed-lid and mouth preservation, 96-DPI presenter arrangement, native-coordinate alpha hit testing, and presenter state-to-frame mapping are automated contracts. The attempt-5 Bezier body-profile coordinates and width limits are superseded and must not constrain the subtractive candidate; new body probes may be frozen only after the exact subtractive mask passes native visual review against the source-derived resize baseline.

A corrected candidate must be inspected against that baseline at native 96px and nearest-neighbor enlargement on white and dark backgrounds. The comparison must cover each exposed outer, foot, underside, valley, and rear-rim segment plus the legal occlusion endpoints under the hair and ribbon; any new or newly exposed branch or endpoint relative to the approved source/baseline contour, displaced edge, opaque light fringe, missing span, broken leg/valley contour, or damage to protected character parts is a failure. This source-relative branch and endpoint judgment is visual rather than inferred from connected-component count alone. Repository checks and asset inspection do not prove the exact live Windows rendering or user acceptance: the body-outline item must return to PASS only after the user directly observes the newly published exact artifact on the current 96-DPI Windows target. Cross-process click-through and focus preservation likewise remain actual-Windows observations.

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
11. no rapid state flapping or repeated response while the cursor condition remains unchanged.

Motion and rendering acceptance is checked from the produced executable, not inferred from XAML or tests. Any required behavior not observed remains unverified and prevents a complete milestone claim.

## 11. Deliverables and handoff

The repository will contain the solution and three projects above, this specification, the JOENESS `TASKS.md` ledger, project `AGENTS.md` managed through the installed setup procedure, and a concise `README.md` with build, test, run, publish, behavior, and known-limit information.

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
