# Plan

- Goal / release: Milestone 1 — a runnable Windows desktop pet that lives autonomously and reacts naturally without obstructing normal work; authoritative design: `docs/specs/2026-08-26-dororong-m1-design.md`.
- Milestones: M1 behavior experience and Windows acceptance — in progress; polished Dororong artwork and broader platform behavior — provisional future work, not M1 scope.
- Now:

  | Outcome | Acceptance | Status |
  |---|---|---|
  | Persist approved M1 design and implementation plan | Current spec and implementation plan inspected; JOENESS contract linked | Complete |
  | Implement and verify M1 | Automated core checks, Release publish, and all actual-Windows acceptance checks in the spec pass | In progress — documentation and automated publish are complete, but GUI acceptance is blocked; check 1 is UNVERIFIED because the required Windows UI tool could not capture rendered frames or obtain coordinate-input geometry on this target |

- Blockers / decisions / links: actual rendered Windows acceptance needs a target where the required UI tool can capture frames and inject coordinates; approved choices remain WPF with a separated behavior core, curious-and-slightly-timid personality, primary-monitor scope, replaceable vector placeholder, and direct clicks taking priority over inferred STARTLED reactions. Links: [specification](docs/specs/2026-08-26-dororong-m1-design.md), [implementation plan](docs/plans/2026-08-26-dororong-m1-implementation.md), [README](README.md), and [Windows acceptance record](docs/verification/2026-08-26-m1-windows-acceptance.md).
- Evidence / reviewed: 2026-08-26 — fresh Release restore, 68/68 tests, zero-warning/zero-error build, runtime-composition check, DRAGGED presenter-angle check, and framework-dependent `win-x64` publish all exited 0; exact SHA-256-bound executable launched and remained alive for 178.7 seconds; the acceptance record preserves the decisive environment boundary and keeps M1 incomplete.
