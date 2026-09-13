# Two-zone pointer tracking and immediate preparation cancellation

User approved outer upright tracking plus immediate cancellation on preparation
departure. Existing head/whole-eye artwork, inner proximity hysteresis, launch arc,
1.5-second continuous dwell, and three-second landing cooldown are retained.

## Implementation and tests

- Outer ellipse radii: 155 horizontal / 100 vertical DIPs, centered at presenter
  (72,81). Upright frame156 reuses the post-landing tracking renderer. Existing
  brain facing/suspension handles direction without autonomous movement.
- Inner ellipse remains77.5/50 with20% exit hysteresis. Outside it the presenter
  resets preparation immediately; the pounce session already resets launch dwell.
  Reentry starts lowering/dwell fresh. Flight/landing/cooldown are not reset by exit.
- RED: all11 new tests failed against original behavior (missing outer activation,
  wrong facing/posture, delayed exit). GREEN: App930/930,2m33s. Two old sleep tests
  assumed delayed recovery; adjusted observation to the unchanged sleep delay.
- Review suggested strengthening walking suppression and hysteresis coverage.
  Added short2second idle/forced walk and exit-resume assertion; hysteresis now
  checks actual preparation frames. Rebuilt focused73/73 PASS (13s), same App DLL.
- Review follow-up found no remaining issues. No production changes after full run.
- Test results: tests/Dororong.App.Tests/TestResults/pointer-zones-20260913.trx and
  pointer-zones-focused-20260913.trx.

## Frozen candidate

Portable: artifacts/product-shell/candidate-20260913-pointer-zones-01.
AppSHA256: DCDFDAE2CFA1EE24251F6BA14E7E5D724CCED5EFE1E037911151968B6C391221.
ZIP SHA256: 3C7B529F1BA4D2BA6FA12944BC158E99460F9C90771359A7F8647B933133DBCA.

Strict package validation PASS: tested RID App/Core parity, runtime8.0.31,
archive roundtrip, apphost identity, isolated-desktop startup/duplicate/WM_CLOSE.
First smoke owned19716 normal exit0; builder smoke owned4328 normal exit0.

Installer: artifacts/installer/candidate-20260913-pointer-zones-01/
Dororong-Setup-0.1.0-win-x64.exe (unsigned,62766061bytes).
SHA256: 2A77E45A0FDC9FC6D51B0E0326E70C42E01F5ECF7A45F6FC8C27B2716EBCD31C.
Refusal, payload inventory/path safety, installer Build phase PASS;467 payload files.
Only four payload pin literals changed across builder/build-test/payload-test;
compiler pin and installer policy untouched. Previous candidates preserved.

## Installed-product rollout

Prior installed47392 exited normally at15:27:23KST; no user process terminated.
Existing installed App hash matched ribbon-match-01 before backup. Rollout helper
is the previously verified normal-update helper with only candidate/old manifest/
old App/new installer identity pins changed. Backup/rollout evidence directory:
artifacts/installer/host-update-20260913-pointer-zones-01.

Host update PASS: byte-verified backup470installed files,1data file, HKCU registry
and Start shortcut. Installer21408 exit0 at15:38:12KST, non-admin/current-user,
no reboot. Verification matched467payload hashes, ownership, registration and
Start link; existing data unchanged before launch. Installed8632 started, duplicate
48364 exit0, original log prefix retained, no new failure diagnostics. The running
path is C:/Users/tjdwo/AppData/Local/Programs/JOEWRKS/Dororong/Dororong.exe.

User visual acceptance remains pending. This is not public release, signing,
clean-guest or cross-version/failure-path acceptance. Old Sandbox pins still refer
to directory02 and are not evidence for this candidate.
