# Dororong direct interactions — partial checkpoint handoff

## Current state

This branch contains delivered body-click behavior, deterministic body-drag behavior with provisional art, and interaction groundwork for cheeks. It does not contain acceptable cheek art or cheek presentation integration. The direct-interaction slice and overall M1 are both `PARTIAL`.

Historical verdicts must remain intact:

- body outline: `PASS`;
- awake body-click canonical eyes: `PASS`, based on the user's exact attempt-2 observation `ㅇㅇ 유지 됨`;
- body-click motion feel, focus, and transparent click-through: `UNVERIFIED`;
- body-drag implementation, deterministic production mapping, and held-drag work-area clamp: `PASS`;
- body-drag art, actual Windows boundary/motion feel, and user acceptance: `PROVISIONAL / UNVERIFIED`;
- left/right cheeks: not delivered and `UNVERIFIED`;
- every unobserved actual-Windows row: `UNVERIFIED`;
- overall M1: `PARTIAL`.

## Read first

1. `docs/verification/2026-08-31-m1-direct-interaction-final-acceptance.md`
2. `.superpowers/sdd/2026-08-31-dororong-direct-interactions/task-11-report.md`
3. `.superpowers/sdd/2026-08-31-dororong-direct-interactions/task-9-report.md`
4. `.superpowers/sdd/2026-08-31-dororong-direct-interactions/task-8-report.md`
5. `.superpowers/sdd/2026-08-31-dororong-direct-interactions/task-7-report.md`
6. `docs/specs/2026-08-31-dororong-direct-interaction-design.md`
7. `docs/plans/2026-08-31-dororong-direct-interactions.md`

## Exact executable left for morning inspection

- Product checkpoint: `8fa1a96385025f775abf57d4f72a7c77a1586912` (`fix: clamp held body drag to work area`).
- Publish root: `artifacts/repro/direct-interactions-overnight-attempt-3/runtime/`.
- Executable SHA-256: `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A`.
- PID at handoff: `47088`.
- Parent PID: `44420`.
- Start: `2026-09-01T05:23:46.6142130+09:00`.
- Exact path: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-3\runtime\Dororong.App.exe`.
- Command line: the same exact path, quoted.

The process was launched once and must not be treated as accepted merely because it remains alive. If it must later be stopped, first read back PID, exact resolved executable path, command line, and start time; stop it only if all still match this record. If identity is ambiguous or the PID is absent, leave it alone and report that state.

Attempt-2 PID `45432` is historical: its exact path, quoted command, CIM start time, and executable hash were matched before it alone was stopped, and absence was confirmed before PID `47088` was launched.

## Morning observation order

Observe only delivered families. Record each row separately as `PASS`, `FAIL`, or `UNVERIFIED`; do not combine impressions into a slice-level pass.

1. Body click: canonical eyes, pending-press subtlety, hop/apex/land feel, and return to rest.
2. Body drag: threshold transition, original grab offset, entry/hang/settle feel, release position, and absence of visual corruption.
3. Sleep wake and proximity suppression while using the delivered body targets.
4. Transparent click-through for canonical, body-click, and body-drag frames against another application.
5. Keyboard focus preservation, topmost behavior, work-area bounds, capture cleanup, and explicit Exit.

Do not try to judge left or right cheeks from this runtime; no cheek family was promoted or integrated.

## Deterministic evidence that is already complete

- Original Task 11 fresh Core invocation: `82/82`, exit `0`.
- Original Task 11 fresh App invocation: `53/53`, exit `0`.
- Five focused corrected WPF harnesses: each exit `0`.
- Static stale direct-presenter call count: `0` outside intentional adapters.
- Direct-interaction render suite: `234` assertions, exit `0`.
- Release build: `0` warnings, `0` errors, exit `0`.
- Fresh body-click and body-drag native/4× production-mapping grids: `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/`.
- Final-review fix `8fa1a96`: focused four-edge held-drag `4/4`, controller/loop `2/2`, capture ownership `3/3`, fresh Core `86/86`, fresh App `53/53`, Release build `0` warnings / `0` errors, and scoped re-review `PASS` with no new findings.
- Capture metadata now declares body pending false, cheek true, core drag true, and drag settle/release false; actual Windows capture cleanup remains `UNVERIFIED`.

The complete sorted PowerShell-suite loop must still be treated as `UNVERIFIED`: the command was run, but its invocation-bound session identifier was lost and therefore its final output/exit cannot be cited. Do not upgrade it from later process absence or partial output. A future worker may run a fresh complete loop when authorized, but this Task 11 checkpoint does not rerun it.

## Cheek blocked boundary

Task 9 exhausted its bounded methods:

- image generation redrew the character and baked an opaque checkerboard;
- the deterministic fallback preserved protected bytes but moved hair/body boundaries rather than producing readable cheek motion;
- the one evidence-based recovery exceeded its hair-tip bound and was independently rejected by root and implementer because one lobe read as a detached paw/ribbon and the other showed checker-like contour artifacts.

Both attempts are immutable rejected evidence under `artifacts/candidates/direct-interactions/cheeks-v1/` and `artifacts/verification/direct-interactions/cheeks-v1/`. No file from either attempt may be promoted. Task 10 was not started because it requires an approved Task 9 family. Resuming cheeks requires a new art direction or an explicit user choice, not another silent retry.

## Repository hygiene and boundaries

- Harness compatibility commit: `fe1dac3bfb0d7ed6bd547742c1f64f07abcd0545`.
- Final-review product fix: `8fa1a96385025f775abf57d4f72a7c77a1586912`; it changes only held-drag clamping and capture ownership metadata/consumption, with no presenter, art, render, timing, settle, or release change.
- `src/Dororong.App/Controls/DororongPresenter.xaml` contains a pre-existing unrelated EOL/stat-only working-tree modification. It was not edited, staged, or committed by this work and must remain untouched.
- The attempt-1 publish failure path and attempt-2 historical publish remain preserved; no publish used `artifacts/publish/win-x64`.
- No push, PR, merge, or phase-2 branch was created.
