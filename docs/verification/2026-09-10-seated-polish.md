# Seated outline, local cheek turn, and walking polish

Approved scope: preserve the authored seated pose and head; clean the exterior
body matte, keep seated cheek play stationary while turning toward the pull,
retain head/body carry unlock, match walking facing to travel, halve speed.
No commit or push.

## Implementation

- The JPEG exterior flood left white-matted grey edge pixels opaque. Recover
  coverage from adjacent neutral ink only at exterior body pixels. Keep the
  protected head/ribbon, interior, silhouette support and original JPEG exact.
- Seated cheek capture uses the local pull path, never a carry session. Reflect
  the frozen capture about its source center after a 4 DIP horizontal dead zone;
  retain its final orientation on release. Platform support stays owned.
- `RenderDesktop` converts screen-facing simulation output to the existing
  authored mirror-parity convention for autonomous Idle/Walk only. Captured
  direct transforms retain their existing meaning.
- Default travel is 21 DIP/s, previously 42. Gait remains distance-driven,
  so its cadence also halves without a second speed multiplier.

## Evidence

- Initial RED: both walking directions faced opposite travel; long seated
  cheek pull moved the window. Focused GREEN after the implementation.
- Supported/non-platform seated pull tests cover large left/right reversals,
  constant window position and support phase, fixed source center, final
  facing after release, and retained seated pose. Head/paw carries still unlock.
- Import regression: 73 exterior body samples recovered; 5,936 protected
  head/ribbon samples exact. White-background composite differs by at most
  two levels; no added/deleted silhouette support or changed interior.
- Native regression: edge Pbgra 149,149,149,195 becomes 18,18,18,64.
  Standing and all 512 walking frames exact; 758,528 protected head/ribbon
  texels exact across 128 seated frames. All 642 frame/source hashes verified.
- All 65 authored bank frames match, exact endpoints and reverse ordering;
  maximum mean adjacent step 0.258/255. Second full export byte-exact.
- Black/white native comparison inspected at
  `artifacts/repro/manual-sit-polish/matte-recovery/seated-native-before-after-black-white-4x.png`.
  The JPEG head's existing fringe is deliberately unchanged.
- Independent scoped review approved; requested supported-surface coverage
  was added and re-reviewed. No remaining findings.
- First full App run: 706 pass, one old 4.2 DIP/100 ms speed expectation failed.
  Updated to the approved 2.1 DIP/100 ms; focused 8/8 pass.
- Core Release 239/239 pass; explicit App Release build has zero warnings/errors.

## Artifact identity

- Immutable original and copied `10-2.jpg`:
  `177B2CCA3BC119B2D42686274A3871A72A0B7075FE62C16A6CA5D9BA34ED9771`.
- Product gzip:
  `C5F617EE324B3878AA698919454D8B38CA255F2635176893BE9A9EDEE39EDFFD`.
- Tested and built App DLL:
  `F6AEEFA5981C90E91D950BB31CBB439A6849477CE1F0A66B1D22C385226B798E`.
- Tested and built Core DLL:
  `70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA`.

## Final delivery

Final App Release 708/708 pass (1m56s), Core239/239: 947 total. Build0warnings/
0errors. Fresh separate publish succeeded at
`C:/Users/tjdwo/Downloads/doro/seated-polish-20260910/runtime`.
Test/build/publish DLL hashes match the values above.

Old PID20868 executable path and App hash were revalidated before shutdown.
CloseMainWindow returned false, so only that exact process was stopped. Its
manual-sit runtime directory remains unchanged for rollback. New PID42020 was
launched hidden; loaded module identity and liveness were checked separately.
Automated WPF/menu/loop and process identity do not claim native mouse acceptance.
