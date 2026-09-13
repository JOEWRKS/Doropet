# Upright rump source registration

User approved sharing the ordinary authored rear silhouette across idle,
walking and upright tracking, and investigating a transient black idle spur.

## Cause and bounded change

Tracking replaced the original rump with a Bezier; its head-relative top anchor
also changed the rear curve under negative gaze. The previous width correction
did not preserve original ink. UprightRumpSource now restores authored rear
texels before the rotated head is composed. Exact 2x texel replication avoids
another bilinear round trip. A narrow upper/body join retains its smooth blend;
the correction fades out over crouch amount0..0.2 and is a no-op thereafter.
Walking departure still supplies moving feet after this body correction.

Fresh ordinary idle previously selected the raw bitmap, while locomotion rest
already removed disconnected low-alpha specks. The fresh open/closed sources
now use that same bounded cleanup. Exactly two source texels change:74,70 and
74,71. All other BGRA bytes are retained. The raw assets remain untouched.
Cheek capture accepts the new immutable sources and preserves their pixels.

## Evidence and limitations

- Original RED:6 rear-ink mismatch cases plus1 fresh-idle fringe failure.
- Expanded tests caught unwanted Pbgra/Bgra conversion rounding; preserve
  Bgra32 in ordinary sources. Exact source equality outside the two measured
  pixels now passes. Cheek fixture exclusions are those two coordinates only;
  direct raw-input fixture expectations remain unchanged.
- Test ordinary-source type checks must use explicit source identity, not
  BitmapImage/non-BitmapImage: both ordinary and tracking are runtime images.
- Focused174 PASS, Core241 PASS. Initial full suite1058pass/11fail: stale
  BitmapImage/type/URI checks and raw two-speck recovery endpoints. Updated
  identity checks preserve transition/eye timing contracts; the2300ms pointer
  departure endpoint correctly expects CLOSED ordinary eyes. Fresh focused55
  PASS. Final full rerun1069 PASS (4m6s), zero failures/skips.
- Inspected actual WPF renderer sheets under
  artifacts/repro/rump-repeat-20260913/: shared-rump-v2.png,
  transitions-v2.png (24 poses, +/-20degrees, upright/crouch boundaries),
  breathing-final.png (fresh idle and settled post-walk across8breathing phases).
  These are deterministic renderer captures, not live desktop recordings.
- Neutral rear threshold boundary matches original in all27rows48..74.
  Authored ink tests check the exposed rear below rotated ribbon, not just bbox.
- Scoped read-only reviewer found no remaining Critical/Important issue;
  its minor active-hunt identity assertion correction was applied.
- The reported momentary black spur was NOT reproduced. Two faint disconnected
  source specks were proven and removed; this is not proof they caused the
  reported black artifact. Live user observation remains required.

Candidate candidate-20260914-upright-rump-01: published App exactly matches
tested App SHA256 C4788877BC545F63E0D61CE436ED5F1A862185B413534A02FE8B14B481901754.
ZIP C14477A9AEB913C5CBE787F7BC6E17E6A93CC011A6D8BE13D5A959D777E5CE6C.
Installer payload/refusal/build gates PASS; native package PID12968 exited0
normally on isolated desktop, duplicate exit0, ZIP round-trip and RID parity PASS.
Setup C90F7D852BBC891AF9BBAB62C5508EE9FCC69CE3EC7296B1B8208DCD4CFB6F3B.
User confirmed normal tray exit.
Verified backup470installation files+1data file, registration and Start link:
artifacts/installer/host-update-20260914-upright-rump-01/.
Installer exit0;467payload hashes, ownership, registration, Start link and
unchanged original data verified. Installed PID7276 launched, duplicate47264
exit0, original diagnostic prefix retained, no new failure events.
No public release or commit/push. Browser preview unchanged. Live user
acceptance of rump matching and transient-spur behavior remains unverified.
