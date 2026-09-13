# Pointer tracking / preparation range expansion

User approved outer tracking distance2x and inner preparation distance1.5x.
These are nested ellipses, not additive250% distance or area.

## Scope

Only two production lines in DororongPresenter.Hunting.cs change:
- Outer upright tracking radii155/100 ->310/200 DIP.
- Inner preparation radii77.5/50 ->116.25/75 DIP.

Stable presenter center(72,81),20% exit hysteresis (139.5/90 DIP),1.5s continuous
launch dwell, immediate preparation cancellation,3s post-landing tracking, jump
distance/arc, head/whole-eye/facing behavior, art and prior perch fixes stay intact.

## Test-first verification

- RED: unchanged production fails16 of24 focused cases;8 controls pass.
  Failure includes missing outer tracking and upright pose at new inner targets.
- GREEN:48/48 focused presenter and real-loop cases pass after the radius change.
- New12-case theory checks cardinal/diagonal ellipse boundaries and actual
  preparation/launch after continuous dwell, rather than reading private constants.
- Updated fixtures retain four-direction upright tracking without launch, immediate
  exit/reentry reset, boundary hysteresis and supported left/right body tracking.
- Read-only scoped code review: no actionable findings.
- Full App suite962/962 PASS (3m23s), zero skipped. Core DLL unchanged from
  the previously verified perch-handoff build.
- Frozen portable candidate: artifacts/product-shell/candidate-20260913-pointer-range-01.
  Strict package/native smoke PASS: exact tested App/Core RID hash parity,
  self-contained Core/Desktop/host8.0.31, archive roundtrip, metadata/icon/apphost.
  Isolated-desktop owned45552 normal WM_CLOSE exit0; duplicate exit0.
  AppSHA256:945F9F8ED4598C61F07C2D160DE5D8441CFA636C485F3EF5AADC7ECE61C18B6E.
  CoreSHA256:BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42.
  ZIP SHA256:EBA9394647B33FD1A8CA00248479BCF4D51DA02C480A70AD7B0336ACCCD57589.
- Installer payload/path-safety and build-refusal checks PASS; refreshed only
  four payload identity pins across builder and its two tests. Compiler unchanged.

TRX evidence in tests/Dororong.App.Tests/TestResults:
pointer-range-expansion-red-20260913.trx,
pointer-range-expansion-focused-20260913.trx,
pointer-range-expansion-full-20260913.trx.

## Rollout

User confirmed normal tray exit; process inventory empty. Prior installed AppSHA256:
9F8C27410D8B266351321C39A50903D86E1F356BC23620F0643AFC6CE2230DCC.
Prior installer/perch-handoff-01 artifacts remain retained.

Installer build PASS:467payload files, unsigned62769454bytes, compiler7.1.0
unchanged/Valid signature. Builder revalidated package/native smoke: owned37948
normal exit0, duplicate0. Setup:
artifacts/installer/candidate-20260913-pointer-range-01/Dororong-Setup-0.1.0-win-x64.exe.
SHA256:FF904FF9BDC8FC393314C7423AF490AE47BB573960D626BA9243D21EB75FAC87.

Normal per-user same-version update completed with the established helper, only
four identity-pin substitutions. Verified backup:470installed files,1data file,
HKCU registration and Start shortcut. Installer40864 exit0 at16:34:15KST.
Installed467payload hashes, ownership, registration and Start link PASS; data
unchanged before launch. Installed49932 started16:34:54KST; duplicate49692 exit0,
original diagnostic prefix retained with only a new Started event, no faults.
Running path:C:/Users/tjdwo/AppData/Local/Programs/JOEWRKS/Dororong/Dororong.exe.
Backup/evidence:artifacts/installer/host-update-20260913-pointer-range-01.
User live acceptance of new range remains pending. Guest/cross-version/failure-path,
signing and rights gates remain separate; no claims of completing those gates.
No force stop, commit/push, signing or public release.
