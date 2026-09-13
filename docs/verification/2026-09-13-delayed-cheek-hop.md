# Delayed cheek-release hop — 2026-09-13

User approved delaying recoil until80–90% cheek return and increasing maximum
movement to8DIPs backward/4DIPs upward. Weak pull is smaller; captured hunt sway
poses use the same reaction. Held cheek carry remains disabled. No eye expression,
art or browser preview changes; no force-close, commit/push or public release.

## Implementation

The unchanged440ms cheek spring leaves16.6% of the original stretch at75ms.
Both visual recoil axes stay zero until this moment. Horizontal movement peaks
at225ms (light2/max8DIPs), vertical lift at195ms (light1/max4DIPs). Lift settles
at315ms and horizontal movement at375ms, before release teardown. The differing
peaks/landings distinguish the hop from a single diagonal translation.

RecoilLift is passed through the actual controller/snapshot/presenter route.
PhysicsTransform removes the full two-axis recoil vector after capture,
platform and ancestor transforms; underlying HWND/support position is unchanged.

## Verification

- Updated real-behavior tests: RED7 (immediate onset/insufficient displacement),
  then focused176/176 PASS, zero skipped. `cheek-hop-red.trx` and
  `cheek-hop-focused.trx` in `tests/Dororong.App.Tests/TestResults/`.
- Tests cover onset at83% return, first movement, peak lift, settled offsets,
  actual hunt frames0/60/99, mirrored movement, nonzero platform squash/sway
  geometry equivalence, plus existing stationary taskbar/runtime cases.
- Independent read-only scoped reviewer found no actionable issue. This does
  not constitute live Windows motion acceptance.
- `git diff --check` exit0.
- Full Release win-x64 App suite989/989 PASS, zero skipped,3m52s;
  `tests/Dororong.App.Tests/TestResults/cheek-hop-full.trx`.
- Initial installer build aborted before output creation: native smoke exited
  during startup while installed PID13228 was still running. ProductIdentity's
  per-user session mutex is shared across isolated desktops; duplicate startup
  returns before Started logging. User then exited normally. Empty inventory
  verified; unmodified candidate rerun passed native smoke (PID49884, normal
  WM_CLOSE exit0, duplicate exit0). No product code or validation bypass used.

## Artifact / handoff

- Product `artifacts/product-shell/candidate-20260913-cheek-hop-01`.
- App SHA256 `C1A100498C02A662F1D083554459A4B815FBE1F86E01C82CA3ED0063B877A5EB`.
- Core SHA256 `BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42`.
- ZIP SHA256 `5EEE396B87068050EA71A18636CD121853BBC9BAF4D74155FAE6E045861461FA`.
- Self-contained8.0.31, tested RID hash parity, archive round-trip and native
  smoke PASS. Installer payload/refusal/build PASS.
- Installer `artifacts/installer/candidate-20260913-cheek-hop-01/Dororong-Setup-0.1.0-win-x64.exe`
  SHA256 `B09C501E1DA41E2A43911712E611086160028C80F1D2EA54EF1C2CD5077450AC`.
  No validation bypass or forced product close.
- Host helper `artifacts/installer/host-update-20260913-cheek-hop-01/Update-Verified.ps1`:
  backed up470installed files, one data file, registration and Start shortcut;
  normal installer exit0;467installed hashes/ownership/registration/settings
  verified. Installed PID25328 launched; duplicate49940 exit0; original data
  prefix retained and no new failure diagnostic. Installed App equals tested
  and packaged SHA256 above.
- Live feel and user acceptance UNVERIFIED.
