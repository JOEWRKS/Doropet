# Closed idle rump donor correction

## Reproduction

After20walk ticks and20settling idle ticks, the presenter remains idle with no
tracking/direct input. At the next ordinary blink it selects closed standing
bank frame321. This frame was rebuilt from stationaryBody, which retained old
upper-rump seam donors; open standing instead used the exact bitmap fast path.
The resulting dark spur appears briefly on each blink, not on the breathing
scale itself. Native71,47 alpha rose from21open to207closed.

## Fix and scope

The exact standing rendering path now handles both eye states. It draws the
same cleaned input and uses the existing seated eye clip to overlay closed
eyes. No C# runtime behavior or original art PNG changed. Regenerated the
locomotion bank and generator provenance manifest.

## Evidence

- Node generator RED: closed standing changes pixels outside the eye region.
- C# RED3: bank parity plus full5s post-walk idle in both facing directions.
- GREEN: generator eyes-only validator and bank comparison PASS. Only frame321
  changed; all641other standing/walking/sitting frames byte-identical. Closed
  standing differs from open in224eye texels, nowhere else.
- Focused58 PASS; Core241 PASS; FullApp1072 PASS (0failed,0skipped).
- Actual WPF presenter audit rerun:626frames (2modes x313frames,16ms sampling),
  fresh idle and post-walk idle, true4s breathing phase. Both open/closed now
  have identical rump pixels, including71,47 Pbgra1,1,1,21 afterwalking.
- Before proof artifacts/repro/idle-rump-audit-20260914-v2/source-comparison.png;
  after artifacts/repro/idle-blink-fix-20260914/after/source-comparison.png.
  Expanded source comparison inspected; no blink-only protrusion remains.
  These are actual renderer captures, not live Windows interaction recording.

Previous bank/manifest backed up in artifacts/repro/idle-blink-fix-20260914/.
Scoped read-only review approved; independent Node validators also passed.
Minor test limitation: eye changed-pixel count is not a full eyelid-art identity
check; source comparison was visually inspected for intact closed-eye artwork.

Candidate candidate-20260914-idle-blink-01 published with tested App hash parity:
App51A0FA8906B1F772CAD6EF8F6B660D3F1CF06BB3D9DDD8993EB735390FDD535F.
ZIP D32CE2FEA6304EA7BFEEB85922649A693BDB7FB118EE85FC5EAD9D01455ABA6A.
Setup368C3B8EF8A90BB4913E915DDBE632EB280499BAE435A8915D5FF27DDF48EFBF.
Installer payload/refusal/build PASS; git diff whitespace check PASS.

## Installed delivery

Actual pet was already closed. Guarded helper in
artifacts/installer/host-update-20260914-idle-blink-01/Update-Verified.ps1
verified the previous upright-rump candidate before backing up470installed
files,1data file, uninstall registration and Start link. Non-elevated silent
installer exit0. Verified467new payload hashes, ownership and shortcuts;
original data unchanged before launch. Installed PID54124 remained running;
duplicate53896 exited0. Original data prefix retained, no new failure events.
All four phases recorded JSON evidence beside the helper. Backup retained.
Live Windows visual acceptance is not claimed; the626-frame proof uses the
actual WPF renderer and the installed App hash matches the tested build.
No commit/push or public release.
