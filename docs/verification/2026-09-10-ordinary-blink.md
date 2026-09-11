# Ordinary open/closed blink

User prefers the walking blink and requests the same ordinary idle behavior,
without the half-closed frame. Standing, seated and walking now share one
elapsed eye clock: first blink2s,360ms closed,5s period. Idle/walk changes do not
restart it. Breathing keeps its own phase. Sleep/wake squint imagery and perch
expressions are untouched; no image resources were edited.

Ordinary idle switches directly from canonical open to authored closed eyes.
Locomotion bank selection uses the same closed flag. Frozen cheek interaction
pauses this clock so the captured closed-eye endpoint survives overlay retirement;
other noneligible direct/platform/sleep ownership resets it open.

## Verification

- RED: idle still showed squint; seated blink ran3times instead of2 across the
  inspected11s. Existing walking case remained green.
- GREEN:27 focused tests, including all3 ordinary poses, exact blink-capture
  release continuity and cleaned-rise body provenance.
- Blink capture setup now advances real elapsed time to2000ms, not core phase.
  All protected-pixel/captured-rest equality assertions remain unchanged.
- Rise regression expects the closed-eye bank at2300ms and keeps that same
  frame on a zero-time transition back to idle, proving eye-clock continuity.
- Final App711/711 passed (2m30s); Core239/239:950total. Scoped read-only review
  approved with no actionable findings. Diffcheck passed.

Published separately to
`C:/Users/tjdwo/Downloads/doro/ordinary-blink-20260910/runtime`.
Previous walking-blink runtime remains intact for rollback. No commit/push.
Automated frame and runtime-identity checks do not claim native visual acceptance.

Exact old PID44440/path/App hash verified before shutdown. CloseMainWindow false;
stopped only that process. New hidden PID34812 responds and is sole pet process.
Loaded App/Core module paths match the new runtime, hashes match tested/published:

- App: `D6F7B012B1564210EF980D8A4932D077CD0E9FD947BD68BE685DA7AD15B8F015`
- Core: `70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA`
