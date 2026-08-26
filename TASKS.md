# Plan

- Goal / release: Milestone 1 — a runnable Windows desktop pet that lives autonomously and reacts naturally without obstructing normal work; authoritative design: `docs/specs/2026-08-26-dororong-m1-design.md`.
- Milestones: M1 behavior experience and Windows acceptance — in progress; broader platform behavior remains provisional future work, not M1 scope.
- Now:

  | Outcome | Acceptance | Status |
  |---|---|---|
  | Persist approved M1 design and implementation plan | Current spec and implementation plan inspected; JOENESS contract linked | Complete |
  | Implement and verify M1 | Automated core checks, Release publish, and all actual-Windows acceptance checks in the spec pass | In progress — the five Important correctness findings are fixed and the new source/hash-bound publish passed automation and exact-identity launch cleanup, but all 12 GUI checks remain UNVERIFIED because no materially different rendered-capture/coordinate-input mechanism or target has been selected |

- Blockers / decisions / links: actual rendered Windows acceptance still needs a target where the required UI tool can capture frames and inject coordinates; the earlier automation limitation remains and no GUI result is upgraded by asset or assembly checks. Approved choices remain WPF with a separated behavior core, curious-and-slightly-timid personality, primary-monitor scope, the exact user-supplied canonical no-tail Dororong art (white shapes behind the rose/bow are ribbons), exact identity without redesign, and direct clicks taking priority over inferred STARTLED reactions. Links: [specification](docs/specs/2026-08-26-dororong-m1-design.md), [implementation plan](docs/plans/2026-08-26-dororong-m1-implementation.md), [README](README.md), and [Windows acceptance record](docs/verification/2026-08-26-m1-windows-acceptance.md).
- Evidence / reviewed: 2026-08-26 — artifact source `055f2dd989c72c1c9f82425a923376744c799c9f`; fresh Release restore, 78/78 tests, zero-warning/zero-error build, actual-`PetLoop` runtime-composition regression, DRAGGED presenter-angle regression, and framework-dependent `win-x64` publish all exited 0. SHA-256 `B3FEAB79AC87D7E3C5956C947159D519594B58582F851F0CADF4B84D81690602` launched once from an exact-path baseline of zero, retained the same PID/path/command/start identity for three seconds, and exact-identity cleanup left no survivor. The acceptance record keeps all 12 GUI items UNVERIFIED and M1 incomplete.
