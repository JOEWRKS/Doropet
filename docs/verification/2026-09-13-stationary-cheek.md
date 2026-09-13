# Cheek stretch without whole-character carry

User explicitly removed cheek-driven character following after another live
distortion screenshot. Only local cheek stretch should remain. Head drag and
perch through head drag stay supported. No previewv7 integration or artwork change.

## Boundary change

PetLoop routes ordinary captured cheek presses to the existing BeginCheekPull
instead of BeginCheekCarry. Queued/current left/right cheeks are local for
platform ownership, including release recovery. No cheek carry session means
IsWholeCarry stays false, so neither readiness nor release-to-perch can begin.

The existing seated/attached paths remain unchanged. The pet remains on its
support: moving windows still carry their supported pet, and disappearing support
still starts gravity. Pointer motion does not move the pet. Legacy carry helpers
are retained but no longer entered by the product's cheek press route.

## Test evidence

- Test-first RED3/3: long left/right pull displaced the window from(100,100);
  taskbar long pull displaced X/Y immediately.
- Taskbar fixture corrected to capture the actual presenter SourceToWindow rather
  than identity registration, plus floating-point tolerance for support correction.
- GREEN34/34 covers long/short pulls, both directions, taskbar readiness suppression,
  no attach, signed reversal, offscreen pointer, release/lost pointer, cleanup,
  seated cheek, support movement/removal, and release gravity.
- Existing assertions requiring cheek carry intentionally updated to the new
  local-only contract. Other head/body carry cases remain.
- Independent read-only scoped review: no actionable findings. Full suite result
  recorded in tests/Dororong.App.Tests/TestResults/stationary-cheek-full.trx.

## Release preparation

Portable: artifacts/product-shell/candidate-20260913-stationary-cheek-01.
ZIP SHA256:38FD746A6A9FBF4E78D94FD2645D56715C4B427FDB801D78BA87691081700555.
Payload/refusal checks PASS. Installer hash/path pins only refreshed for this
candidate; compiler and lifecycle unchanged. User confirmed normal tray exit.
Installation must follow full tests, verified backup and normal installer update.
No force-stop, source art edits, preview modifications, commit/push/public release.

Candidate AppSHA256:89EAB22AE3A4E53C54CB3B9E6124BE6906E6E793E591CAD3FAD1B336EF2F001B.
CoreSHA256 unchanged:BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42.
Installer `artifacts/installer/candidate-20260913-stationary-cheek-01/`
`Dororong-Setup-0.1.0-win-x64.exe` SHA256:
4C522AD668D3A59A84AD2B61504F078B2C5C15017E637D428C8940D35F0D38F4.
Strict package/hash/RID/self-contained8.0.31 check PASS; native smoke owned47964
normal WM_CLOSE exit0 and duplicate0 on isolated desktop. Installer build PASS.
Verified backup470installed files,1data file, Start link and registry in
`artifacts/installer/host-update-20260913-stationary-cheek-01`.

## Completed normal update

- Full win-x64 App980/980 PASS,3m37s,zero skipped; TRX above.
- Same-version current-user installer exit0; no Windows restart. All467installed
  payload hashes/ownership/Start link/uninstall registration verified. Data
  unchanged before launch, no unexpected desktop shortcut or auto-launch.
- Installed52112 Started19:03:47KST at established installed path; duplicate50052
  exited0. Original diagnostic bytes retained with only Started appended, no
  faults. Previous installation/data backups remain available.
- User live acceptance remains separate. Preview2801/v7 unchanged.
