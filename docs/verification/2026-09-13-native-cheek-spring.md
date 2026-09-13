# Native cheek release spring — 2026-09-13

## Approved scope

User reported recoil was absent from the installed product, then requested it
also when interacting during hunting butt-wiggle. Apply the reviewed release
timing to actual captured cheeks; keep cheek carry disabled. No new eye closure,
art replacement, browser changes, commit/push or public release.

## Implementation and evidence

- Native positive captured cheek release uses a 440 ms damped curve with two
  crossings of rest. Inward cheek press retains its previous 220 ms release.
- Light pull produces a .6 DIP horizontal visual kick; maximum pull produces
  2 DIPs opposite the pull. Whole-character drag/carry is not restored.
- Hunt capture preserves its frame/gaze/head pose and now renders negative pull
  compression rather than discarding it. This is native spring timing and visual
  recoil, **not** the complete browser v7 torso/head deformation renderer.
- New real-controller and actual captured presenter regressions: RED7 then
  GREEN7. Focused cheek/hunt/seated/follow suite: 174/174, zero skipped, final
  `tests/Dororong.App.Tests/TestResults/native-cheek-spring-focused3.trx`.
- Independent scoped review caught visual displacement leaking into foot/bounds
  measurement. RED5 reproduced plain geometry drift; a further RED5 reproduced
  drift after platform squash/sway. The final physics transform removes the
  displacement through inverse capture and current overlay transforms, retaining
  all physical geometry. Review rechecked this and reported no remaining
  critical/important issue. Taskbar runtime regression verifies every release
  tick stays supported/stationary through completion.
- Existing positive release completion expectations deliberately changed to
  440 ms; held pixels and inward press expectations remain unchanged.
- `git diff --check` exit0 (existing LF/CRLF warnings only).
- Full Release win-x64 App suite: 987/987 PASS, zero skipped, 3m52s;
  `tests/Dororong.App.Tests/TestResults/native-cheek-spring-full.trx`.

## Candidate identity

- Product: `artifacts/product-shell/candidate-20260913-native-cheek-spring-01`
- App SHA256: `288A7FDC4CE69EEE0EF715E41AC5DD0FFEE6D081FF576A553173CC6A57A0BEFC`
- Core SHA256: `BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42`
- ZIP SHA256: `B16C98556D4E8418344E0B0037CFC3B8CB71720D09A1CF68990BB26645616AF9`
- Installer: `artifacts/installer/candidate-20260913-native-cheek-spring-01/Dororong-Setup-0.1.0-win-x64.exe`
- Setup SHA256: `BC2530ACEB35C01B9D6041B1F90F8390B1BB8FAACAF8046F1087926C2D7485CB`
- Self-contained8.0.31, exact tested RID App/Core parity, ZIP round-trip and
  isolated-desktop native smoke PASS. Smoke PID50504 exited normally; duplicate
  exit0. Installer payload/refusal/build checks PASS; unsigned internal0.1.0.

## Host handoff

User confirmed normal tray exit; empty product process inventory confirmed.
`artifacts/installer/host-update-20260913-native-cheek-spring-01/Update-Verified.ps1`
backed up 470 installed files, one data file, registration and Start shortcut
with byte identity verified. Normal installer exited0; verify checked467 payload
hashes, ownership/registration/Start shortcut and unchanged original data. Launch
verified installed PID13228, duplicate47616 exit0, original data prefix retained
and no new failure diagnostic. Tested, packaged and installed App DLL hashes
match the identity above. Live Windows feel/visual acceptance remains UNVERIFIED; automated
renderer tests and process liveness do not substitute for user observation.
