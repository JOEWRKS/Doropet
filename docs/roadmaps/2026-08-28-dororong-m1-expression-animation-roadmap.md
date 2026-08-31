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

Stage A closed-eye art, breathing, and sleep crossfade remain accepted checkpoints. Direct-interaction infrastructure, the transform-only body click, and a provisional complete-character body-drag mapping are implemented on `feature/dororong-m1-expression-animation`. This is a truthful `PARTIAL` checkpoint, not completion of the direct-interaction slice.

- the exact user-authored open/squint/closed family and accepted sleep-wake bridge remain preserved;
- attempt 38 (`c883b7872dcdb162dd395c26b09353d570c178a5`) remains the accepted sleep-crossfade checkpoint;
- body-click product checkpoint `92dd706bfcaf51def1d148670dfb787ef40ec1d0` keeps awake clicks on the exact canonical open-eye image, and body-click attempt 2 passed direct user observation for canonical-eye preservation (`ㅇㅇ 유지 됨`);
- the current body-click mapping uses the canonical image throughout and passes a fresh 16 ms native/enlarged deterministic render inspection; pending-press subtlety, full hop/apex/land feel, focus preservation, and transparent-area click-through remain `UNVERIFIED`;
- body-drag entry/hold/settle implementation is deterministically covered and the current 140 ms / 180 ms production mapping passes a fresh native/enlarged render inspection. Final-review fix `8fa1a96385025f775abf57d4f72a7c77a1586912` also clamps the threshold-crossing tick and every subsequent held tick at all work-area edges while preserving the original grab offset, and makes pending/cheek/drag capture metadata match actual loop ownership. Fresh Core `86/86`, App `53/53`, a zero-warning/zero-error build, and scoped re-review pass this deterministic fix; the Task 7 attempt-2 art family, actual Windows boundary/motion feel, and user acceptance remain `PROVISIONAL / UNVERIFIED`;
- Task 9 generated and preserved two rejected cheek attempts. The primary image-generated sheets redrew identity and baked a checkerboard; the deterministic fallback did not read as cheek motion; the bounded recovery produced detached-paw/ribbon and checker-like contour defects. Root and implementer independently rejected the recovery family. No cheek art was promoted, and Task 10 was not started;
- left-cheek and right-cheek art, integration, runtime behavior, and user acceptance are therefore not delivered and remain `UNVERIFIED`;
- the final exact PowerShell-suite loop has no available final exit because its invocation session was lost. Separate original Task 11 evidence (Core `82/82`, App `53/53`, five focused migrated-harness exits, zero stale direct calls, and zero-warning/zero-error build) and final-review-fix evidence (Core `86/86`, App `53/53`, zero-warning/zero-error build) remain valid, but the complete loop itself is `UNVERIFIED`;
- the current morning-inspection publish is `artifacts/repro/direct-interactions-overnight-attempt-3/runtime/`, with `Dororong.App.exe` SHA-256 `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A`. It was launched exactly once as PID `47088`. Attempt 2 and PID `45432` are historical process evidence only. Process liveness is not actual-Windows acceptance;
- slow/fast/drag wake observations, focus, click-through, topmost, bounds, capture cleanup, explicit Exit, autonomous interactions, and broader M1 remain `UNVERIFIED` or `PARTIAL` as recorded in the final acceptance matrix.

The immediate next unit is the ordered morning observation of delivered body click and provisional body drag, one observable at a time, without inferring unobserved properties. Cheeks require a new art direction or explicit user choice before Task 9 can resume; do not integrate either rejected family. The complete PowerShell-suite loop also needs a future fresh invocation-bound run before any full-regression claim. Overall M1 stays `PARTIAL` until all roadmap acceptance rows are directly evidenced.
