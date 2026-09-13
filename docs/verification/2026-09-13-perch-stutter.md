# Repeated taskbar preview stutter — 2026-09-13

## Report and reproduction

User confirms the cue appears in front of the taskbar, but the first two or three
flutter cycles stutter on repeated visits. Ordinary windows are smooth. Running
candidate was `candidate-20260913-perch-preview-layer-01`, PID34600; it was not
restarted or instrumented during diagnosis.

Ignored diagnostic project `artifacts/repro/perch-stutter-20260913/Probe.csproj`
references the exact running candidate DLLs. It drives real PetLoop, presenter,
head swing and platform runtime against fake desktop/input boundaries, without
showing windows or issuing native z-order writes. It holds a fling-clamped pet
at an800x600 monitor's bottom taskbar for16016ms ticks. The selector-time sole is
captured before rendering, rather than substituting the next pose's contact.
`selection.jsonl` retains the evidence; previous diagnostic output is preserved.

- Taskbar horizontal flings in both directions produce false readiness samples
  at entry distances `20.000000000000057` / `20.000000000000114`.
- Corresponding ordinary-window controls and the no-fling control have no misses.
- Movement-area rectangle reconstruction adds a few floating-point ulps to the
  clamp boundary. The exact20DIP comparison rejects it for a frame.
- PerchReadinessPresentation resets its elapsed flutter phase when not ready.
  This creates apparent animation stalls/restarts during swing decay; once the
  pose settles onto integer coordinates, it stops.

This proves a cue-reset cause independent of native taskbar ordering. It does not
prove absence of every possible live-shell performance issue.

## Bounded change and regression

Only the bottom-touching taskbar entry comparison with measured HeldSoleY gets
the existing1e-6DIP geometry-roundoff tolerance. Ordinary-window comparisons stay
exact; the nominal20DIP band, full-body screen clamp, attachment target, layer
guard and art remain unchanged. Preview and release share this comparison.

The new four-case real-WPF loop test covers both facings and both fling directions,
requires readiness every frame for160 ticks, then verifies release enters perch.
An initial test fixture assigned its scene after Start, producing an unrelated
tick0 cache failure. Corrected the scene setup before Start and reran with the fix
disabled: all four cases failed at tick6 or8. Restoring only the numeric tolerance
made the focused24 tests pass. The earlier fixture failure is not counted as the
numeric regression's RED evidence.

Scoped independent read-only review reported no findings. Existing tests cover
outside-band rejection, exact attachment, scaled monitor mapping and other edges.
Core241/241 and full self-contained win-x64 App872/872 passed (App2m44s).
Strict package/reference/archive/runtime/icon validation and native smoke passed
(ownedPID34084 normal WM_CLOSE exit0; duplicate0). Test results are in
`artifacts/repro/perch-stutter-20260913/verification`.

## Delivery boundary

Fresh candidate published at
`artifacts/product-shell/candidate-20260913-perch-stutter-01`.
Both App/Core DLLs exactly match the final tested RID binaries.

- ZIP SHA256: `C05B803D653FABA580BB4C495FFA14BDB2C3D2C7C852DEE958B7719F9828B27A`
- App DLL: `4D64BF57C267D013128FF24B92EDC4D1BBEBB99941AF0758341CB6A2AED9FDE8`
- Core DLL: `2AC994181A0498B57D210D42AFD86ADBEBA7FC345A8E0916E2C7DBE50494B420`

User normally exited the current pet (ㅇㅇ); empty process inventory and PID34600
Stopped event at02:31:58KST verified before package-native testing. No force-kill,
installer promotion, asset change, commit or push. Live repeated-taskbar test is
still pending, separate from the deterministic automated regression.

After successful validation and a fresh empty pet-process inventory, normally
launched the exact candidate as PID15784 at02:34:26KST. Executable path and Started
event at02:34:27KST verified. User can now repeat the taskbar visit/swing test;
live acceptance remains open.

Subsequent user confirmation: "ㅇㅋ 수정됨". Repeated taskbar stutter is accepted.
The user separately reports flutter paw occlusion and perched outline defects;
those do not reopen the numeric readiness-reset diagnosis and are tracked in TASKS.md.
