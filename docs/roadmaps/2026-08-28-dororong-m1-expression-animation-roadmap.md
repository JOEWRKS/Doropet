# Dororong M1 Expression and Animation Roadmap

## 1. Purpose

This roadmap fixes the order and acceptance boundary for the remaining Dororong M1 visual-behavior work. It is not a claim that the full milestone is complete, and it does not reopen the closed phase-1 body-outline or provenance investigation.

The target experience remains:

> Dororong lives autonomously on the Windows desktop and reacts with character-preserving expressions and motion to mouse approach, fast approach, click, drag, inactivity, and waking, without obstructing ordinary computer work.

`TASKS.md` remains the single status ledger. This file records the durable sequence, scope, and completion criteria so later workers do not have to reconstruct them from conversation history.

## 2. Starting boundary

- Working branch: `feature/dororong-m1-expression-animation`.
- Branch point: frozen phase-1 checkpoint `cc04e67e8cc330d9afe0af607b66188527a42cd4`.
- Phase-1 body-outline scope: `PASS`.
- Overall Dororong M1: `PARTIAL`.
- Attempt-8 visual asset identity at the checkpoint: `PRESERVED`.
- Exact attempt-8 runtime binary provenance: `UNVERIFIED`; cause: `UNVERIFIED`.
- The bounded provenance investigation is closed. Do not rebuild or investigate further for that purpose.
- Draft PR #1 preserves the phase-1 checkpoint and must not be treated as a full-M1 completion PR or merged as part of this work.

## 3. Product invariants

Every stage below must preserve all of the following:

- the user-approved, no-tail Dororong identity and phase-1 body outline;
- the existing head, face placement, hair, rose, bow, ribbons, proportions, and single-weight outline character;
- existing state names, behavior priority, timing, and click-versus-drag rules unless a separately approved behavior change is required;
- direct-click priority: `DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP / IDLE / WALK`;
- transparent-window alpha hit testing, click-through outside visible pixels, no focus theft, topmost presentation, and work-area bounds;
- asset replacement that does not require rebuilding the behavior system.

This roadmap does not add progression, feeding, affection, experience, accounts, cloud features, AI conversation, sound, auto-start, a large settings screen, multiple characters, or polished multi-monitor support.

## 4. Delivery order

### Stage A — Closed-eye and sleep foundation

Goal: fix the detached or misaligned closed-eye appearance before adding more frames.

Deliverables:

- a character-preserving closed-eye frame whose eyelids stay aligned with the canonical face;
- no stray or leftover eye pixels when eyes are closed;
- a restrained SLEEP pose and slow breathing motion;
- a natural wake visual for slow approach, fast approach, click without drag, and drag.

Acceptance:

- static open/closed comparison passes repository visual inspection;
- protected character regions and the approved body silhouette are unchanged outside the intended face edit;
- actual Windows observation confirms closed eyes remain attached and readable throughout SLEEP and wake;
- click-only wake visibly becomes `CLICK_REACTION`, not a drag or proximity-only response.

### Stage B — Presentation mapping and regression protection

Goal: keep frame selection and pose calculation explicit without introducing a game engine or broad animation framework.

Deliverables:

- one small presentation mapping from `PetSnapshot` state/phase to frame and transforms;
- frame choice separated from scale, rotation, and translation calculations enough to test them independently;
- deterministic tests for each state mapping and phase boundary;
- unchanged core behavior timing and priority tests.

Acceptance:

- every M1 state has one inspectable visual contract;
- unknown states fail visibly rather than silently using an unrelated pose;
- Release build and automated tests pass from a clean working tree.

### Stage C — Direct interaction motion

Goal: make deliberate user interaction feel immediate and distinct.

Deliverables:

- `CLICK_REACTION`: a short characterful dangling/bounce response with a clear return to rest;
- `DRAGGED`: a hanging pose that responds to the grab point while preserving the original pointer offset;
- release settling that does not create a new behavior state.

Acceptance:

- click and drag are visually distinguishable;
- a click below the Windows drag threshold never becomes DRAGGED;
- dragging remains responsive, bounded, and free of visible outline/frame corruption;
- actual Windows observation passes for click, drag, and release.

### Stage D — Mouse-approach reactions

Goal: make slow and fast approaches readable without making Dororong constantly react.

Deliverables:

- `CURIOUS`: a small attention shift or head/body tilt toward the cursor;
- `STARTLED`: a brief surprise expression plus squash/stretch or retreat;
- cooldown and hysteresis behavior remain controlled by the existing core rules.

Acceptance:

- slow and fast approaches produce visibly different responses;
- fast motion away from Dororong does not trigger STARTLED;
- a cursor that remains nearby does not repeatedly retrigger CURIOUS;
- actual Windows observation passes one reaction at a time.

### Stage E — Autonomous-life polish

Goal: make unprompted behavior enjoyable to watch while remaining quiet during normal work.

Deliverables:

- `IDLE`: subtle breathing/blinking with no frame detachment;
- `WALK`: a readable step/bob cycle and correct facing direction;
- transitions among IDLE, WALK, and SLEEP that do not visibly snap without reason.

Acceptance:

- the pet can be watched without interaction and still appears alive;
- idle motion is not distracting or continuously attention-seeking;
- movement stays inside the work area;
- actual Windows observation passes for autonomous transitions.

### Stage F — Ordered Windows acceptance

Goal: validate the product itself on the current Windows PC, not infer success from build output or automation.

Run observations in this order, requesting only one small group from the user at a time:

1. closed eyes, SLEEP, and every wake path;
2. click, drag, and release;
3. CURIOUS and STARTLED;
4. IDLE, WALK, boundaries, and autonomous transitions;
5. transparent-area click-through, no focus theft, topmost behavior, and explicit exit.

Each item is recorded as `PASS`, `FAIL`, or `UNVERIFIED` from direct evidence. Automation failure remains separate from product failure, and no unobserved item is promoted to PASS.

### Stage G — M1 closure and handoff

Goal: close the milestone only when the full acceptance boundary is met.

Deliverables:

- final state of implementation, decisions, known limits, and exact verification evidence;
- a clean Release build/publish identity for the accepted phase-2 result;
- an independent review of the completed change set;
- a completion PR only after all required M1 product observations are PASS.

Acceptance:

- all required automated checks pass;
- all required actual-Windows observations pass;
- non-interference checks pass;
- no required result remains `FAIL` or `UNVERIFIED`.

Until then, overall M1 remains `PARTIAL`.

## 5. Working and verification rules

- Work in the smallest state group that can be visually judged; do not change every state at once.
- For visual changes, compare against the canonical frame and inspect both transparent and contrasting backgrounds before asking for Windows observation.
- Preserve exact approved assets when they are not the target of the current edit.
- A build, test, process-liveness check, screenshot name, or written narration alone is not proof of live Windows behavior.
- If a stage fails, keep its evidence, fix the demonstrated cause, and recheck the original failure before moving on.
- Do not push, create a new PR, merge, or modify the frozen phase-1 branch unless the user explicitly requests it.

## 6. Current checkpoint and immediate next unit

Stage A closed-eye art, breathing, and sleep crossfade remain accepted checkpoints. The direct-interaction input/controller infrastructure and transform-only body click are now implemented on `feature/dororong-m1-expression-animation`.

- the exact user-authored open/squint/closed frame family and accepted sleep-wake bridge remain preserved;
- attempt 38 (`c883b7872dcdb162dd395c26b09353d570c178a5`) remains the accepted sleep-crossfade checkpoint;
- body-click product checkpoint `92dd706bfcaf51def1d148670dfb787ef40ec1d0` removes all newly drawn click-pose art and keeps awake clicks on the exact canonical open-eye image;
- body-click attempt 2 passed direct user observation for canonical-eye preservation;
- pending-press subtlety, the complete hop/apex/land feel, focus preservation, and transparent-area click-through remain `UNVERIFIED` for the exact attempt-2 runtime;
- slow-approach, fast-approach, and drag wake routes also remain `UNVERIFIED`;
- overall M1 remains `PARTIAL`.

The immediate next unit is to finish body-click Windows acceptance one observable at a time. Do not infer the remaining non-interference properties from automated tests.

After that boundary is closed, proceed to direct-interaction Task 7: create the body-drag entry/hang/release candidate family from the canonical asset and durable user sketch. Candidate frames must be root-reviewed and shown as native frames, a nearest-neighbor enlarged strip, and normal-speed playback before any bytes are promoted into product assets.
