# Cheek return and hop overlap — 2026-09-13

User corrected the preceding rest-synchronized design and approved launching at
80ms, just before the first return crossing (~92ms), rather than waiting for all
face oscillation to settle at440ms. Keep280ms hop, maximum8DIP backward/4DIP lift,
weak pull2/1, displaced landing, stationary hold and repaired outlines.

## Implementation and evidence

- Only production timing helper `CheekSpringMotion` changed. Face spring curve
  remains unchanged. Separate hop start80ms and duration280ms; land at360ms.
  Horizontal offset stays at its endpoint while lift stays zero after landing.
  Local capture retires at440ms after remaining face oscillation settles.
- Controller regression RED2: at90ms cheek is still positive (~2% remaining),
  but old code has no backward/lift displacement. GREEN2 after timing change.
  Test also checks80ms no hop (~11% remaining),220ms apex,360ms landing,
 430ms retained endpoint/no second lift and440ms retirement.
- Updated native captured hunt/sway tests to sample the new220ms apex. Runtime
  tests retain mirrored/no-platform/ceiling/taskbar landing and cancellation,
  no repeated sprite offset and no return to origin. Related146/146 PASS.
- User confirmed tray exit; product process inventory empty before package gate.
- Published candidate: `candidate-20260913-cheek-early-hop-01`.
  ZIP SHA256 `C36FD6FBA76E01CAA3C921DBADAFE7D7D9DF9098D24FE78F8FE03D7BE901B8DE`.
- Installer payload/refusal/build gates PASS. Full RID suite1006/1006 PASS,
  zero failures/skips,3m45s (`cheek-early-hop-full.trx`). Read-only reviewer found
  no actionable issues. Native isolated-desktop smoke/duplicate exit0, archive
  round-trip and exact tested App/Core hash parity PASS.
- App SHA256 `4CB4D7D2FB4ABAB395EF1183AB7A535EB2E1901F99B4DF92CA3088D8A2340B8F`.
- Setup SHA256 `11EC56A71F8EDEBF52E5F4661E862A1C56A9B4C3731B2098B84BF836DE7ED53F`.
- No public release, commit/push, preview/art changes.
- Installed-host update PASS. Recoverable backup and evidence:
  `artifacts/installer/host-update-20260913-cheek-early-hop-01/`.
  Backup470installed files,1data file, registration and Start link. Installer
  exit0;467payload hashes, ownership, registration and original data verified.
  Registered installed executable launched asPID22792; duplicate5944 exit0.
  Original diagnostic prefix preserved; no new failure events. User visual
  acceptance remains pending; no live animation quality claim from test results.
