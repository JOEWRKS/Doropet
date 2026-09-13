# Cheek carry readiness / neck gap correction and preview v7

User approved fixing both diagnosed native rendering bugs, plus a separate v6-based
light-pull recoil and maximum-pull backward bounce preview. No new eye closing.

## Native rendering changes

- Hunt-captured cheek overlays retain the captured frame/gaze/eye state and their
  source-space render callback through facing changes. Readiness requests a
  body-only, pose-mapped foreleg flutter before compositing the head.
- Body boundary ink is finished before the head. The previous final-composite
  distance field could redraw dark ink across the lowered opaque cheek.
- A fill-only interior neck quad joins head/body under their existing ink. No
  new outline or outer silhouette expansion. Drag/perch acquisition logic unchanged.

## Evidence

- RED: original readiness path failed3/4 cases, changing97–131opaque head texels.
  Neck fixture failed4/10 with alpha22 at the diagnosed center(54,60).
- Initial focused GREEN14/14. Added zero/light pull and closed-eye captures:
  win-x64 focused17/17, zero skipped. Readiness still animates visible paws;
  leaving readiness restores the exact captured cheek rendering.
- Full non-RID App976/976, Core241/241 PASS. Final win-x64 full run recorded
  separately in `cheek-neck-full-20260913.trx`:979/979 PASS,3m32s,zero skipped.
- Offscreen27poses ×16flutter phases: zero fully opaque pulled-head texels
  changed. Evidence `artifacts/repro/cheek-perch-neck-20260913-fix-02`.
  Mask follows the actual pulled head and the renderer's two-stage sampling.
  Earlier diagnosis used an unpulled head mask; these are different measurements.
- Independent read-only image comparison:49alpha increases only within
  x49..56/y55..69 at downward rest; no alpha decreases or outer row extent change.
  Neutral/upward rest byte-identical; crouch alpha unchanged. No enclosed
  transparent holes across27poses. No blocking code-review findings.
- Review noted double rendering per readiness tick as a profiling opportunity,
  not an established performance defect. Native live acceptance remains separate.

## Preview only

V7 reuses preview-06 banks and the same cheek length/clean contour/open eyes.
Release-only horizontal recoil adds0.6nativepx at25% pull,2px atmaximum. Mirrored,
continuous, bounded under repeated regrabs; no hold kick. Buttons for light/max
are live at the existing2801tab. Node11/11 and standalone C# preservation
verification PASS. Max-bound assertions tightened following review.
No preview renderer changes were incorporated into the native package.

## Candidate and rollout boundary

Portable `artifacts/product-shell/candidate-20260913-cheek-neck-01`:
- AppSHA256 A8B6ACBADDE03009E5F855248616B3C546961810EC56BCDD0408821E4AA14411
- CoreSHA256 BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42
- ZIP SHA256 86BFBBEF29DA52B15A28A7695120F63594896FF8BDE3D7A35FB227B037246966

Installer pins changed only to the new immutable payload/hash; pinned compiler
and lifecycle unchanged. Payload/refusal suites PASS. User confirmed normal tray
exit and empty product process inventory verified before preparation.
Installer validation/build, backup/update/launch and user live acceptance are
separate gates; append exact completion evidence below. No force-stop, public
release, signing, commit or push.

## Completed normal host update

- Installer `artifacts/installer/candidate-20260913-cheek-neck-01/`
  `Dororong-Setup-0.1.0-win-x64.exe` SHA256
  `54894471E06E57D4CA28B3CE7C900DAB01C6CDD2C090AE903624BE64E8401500`.
  Unsigned0.1.0; policy/compiler unchanged. Strict smoke: owned32980 normal
  WM_CLOSE exit0 and duplicate0 on isolated desktop; exact RID hash parity PASS.
- Verified backup470installed files,1data file, registration and Start link.
  Retained in `artifacts/installer/host-update-20260913-cheek-neck-01`.
  Helper reused with four reviewed candidate/old-payload/hash substitutions.
- Normal current-user installer exit0 at18:37:46KST, no Windows restart.
  All467installed files and ownership match; original data unchanged before launch;
  Start link and uninstall registration valid, no desktop shortcut/autolaunch.
- Installed11268 Started18:38:35KST; duplicate29056 exit0. Only Started appended
  to original diagnostic log, no fault events, original data prefix preserved.

The installed product now contains the two bug fixes. V7 recoil remains preview
only, with buttons and exact settled rest verified at2801. User live visual
acceptance remains distinct from automated/offscreen verification.
