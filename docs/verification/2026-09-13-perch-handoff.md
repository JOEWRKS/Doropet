# Taskbar advertised readiness / release geometry handoff

User approved correcting the diagnosed40/120 failed visible-ready releases.
Original investigation:2026-09-13-perch-cue-release-diagnosis.md.

## Implementation

Only the production loop's explicit advertise:true call stores readiness. Pure
eligibility queries remain side-effect-free. EdgePerchRuntime retains that cue's
contact, continuous position, facing, selected surface and monitor. The next
release attempt consumes it. False/blocked advertised frames and teardown clear it.
Only taskbar cues use this handoff; ordinary window behavior remains unchanged.

Release revalidates current scene selection with the advertised contact/position;
changed monitor, replaced/missing/moved/clipped winner and unavailable input/scene
cannot preserve a stale target. Real movement/facing change does not reuse the cue.
Current geometry can still be evaluated normally when there is no matching cue.

Core.FindCandidate exposes the existing shared selector's surface without owner
mutation. No entry-band constant, screen clamp, attachment grip, art or timing change.

## Verification and review

- Reconfirmed initial RED using unchanged product: original120swing-case test fails.
- Initial fix: focused22/22; App944/944 (3m), Core241/241.
- Review found exact-position comparison vulnerable to quantized host readback.
  New harness0/1/0.6666667DIP readback theory RED2failed/1pass; fractional mode
  failed100/120 visible-ready releases. Test captures real loop and renderer;
  quantization is injected at host readback, not claimed as physical mouse testing.
- Corrected using the platform's existing one-physical-pixel envelope divided by
  map scale. Revalidation and acquisition both use the advertised continuous origin.
  This does not expand the global20DIP entry/search band.
- Expanded focused19/19 PASS (27s):360swing scenarios, pure-query isolation,
  consumed/cleared token, changed position/facing/owner/monitor/coverage, reliability,
  and100/150% physical-pixel tolerance controls. All120cases in each swing mode must
  advertise, so hiding the cue cannot make the regression pass.
- Re-review: no remaining findings. Final App950/950 PASS (3m7s), Core241/241.
- Frozen portable candidate: artifacts/product-shell/candidate-20260913-perch-handoff-01.
  Strict package/native smoke PASS: App/Core exactly match tested RID references,
  self-contained Core/Desktop/host8.0.31, archive roundtrip, metadata/icon/apphost,
  owned50092 normal WM_CLOSE exit0 and duplicate exit0 on isolated desktop.
  AppSHA256:9F8C27410D8B266351321C39A50903D86E1F356BC23620F0643AFC6CE2230DCC.
  CoreSHA256:BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42.
  ZIP SHA256:1BDD94AADBE522921D1A5B81707A37DACEFE719C6F25D10D3B70365AD1EE9879.

Evidence TRX files in tests/Dororong.App.Tests/TestResults:
perch-handoff-20260913.trx, perch-readback-red-20260913.trx,
perch-handoff-final-20260913.trx.

## Rollout boundary

User normally exited installed49400; diagnostic Stopped16:02:58KST, inventory empty.
Installer build/payload/refusal gates PASS,467payload files, unsigned62770608bytes.
Installer:artifacts/installer/candidate-20260913-perch-handoff-01/Dororong-Setup-0.1.0-win-x64.exe.
SHA256:4A7ED26A5BB954FB68FB35D9647B0D41AFEB05A053E94C3D5015EADB60EC40EF.
Builder private-desktop smoke owned49968 normal exit0, duplicate0.

Normal same-version installed update completed after byte-verified backup470files,
existing data, HKCU registration and Start shortcut. Helper is the prior verified
normal-update helper with only four identity pins changed. Installer48640 exit0
16:12:04KST;467payload/ownership/link/registration checks PASS; user data unchanged
before launch. Installed49392 started16:12:44KST, duplicate48224 exit0, original log
prefix retained, no new failures. Running path is
C:/Users/tjdwo/AppData/Local/Programs/JOEWRKS/Dororong/Dororong.exe.
Evidence/backup:artifacts/installer/host-update-20260913-perch-handoff-01.
User live acceptance pending; prior pointer-zones-01 installer/payload retained.
No force termination, public release, signing, guest/failure injection or commit/push.
