# Shared standing/tracking proportions

User observed different body/size in ordinary idle, walking and tracking. Equal
native96, neutral-gaze proof confirmed canonical/rest width72 (x4..75), tracking
width74 (x4..77); height63 identical. Heads derive from canonical without a size
transform; split-head resampling and pose rotation affect perceived sharpness.
Idle breathing adds up to2.4% width/1.2% height but tracking previously reset it.

## Changes

- Upright rear Bezier follows the original edge within1native pixel and width72.
  It blends continuously into the unchanged fully crouched curve at amount1.
  The shape is a fitted connection, not byte-identical source raster; hair/head
  layers, approved gait, fourth leg, head angle and source assets are untouched.
- Tracking retains ordinary breathing. Pounce composes the existing image
  transform including its pivot before authored squash/rotation; otherwise
  launch and Landing-to-Track introduced a new scale pop (found in review).

## Evidence

- RED5 shared silhouette/breathing; GREEN in focused44.
- Reviewer found pounce transform replacement, reproduced with RED2 (visible
  point x mismatch0.508386 at launch/late landing, idle phase.4).
- Proof: artifacts/repro/skin-comparison-20260913/after-fix.png and
  artifacts/repro/shared-skin-20260913/poses/contact.png (30pose/gaze combinations).
  No newly disconnected neck observed in proof; existing neck tests pass.
- Initial app1051/core241 PASS. Candidate shared-skin-01 not installed, superseded
  by pounce transform composition. Final focused50 PASS; re-review clear.
- Candidate02 ZIP: `661F7897AD47F807F182909BC57B178E6F6F1CB25D8086E9114D106166C56AF0`.
  Final app1053 PASS (3m57s, zero failures/skips); core241 PASS.
- Package/native smoke PASS (PID41992 normal exit0, duplicate exit0), exact
  tested RID App/Core parity and archive round-trip. Installer gates PASS.
- App: `B7D92A3629C29089F3849A21D92CE9919AB1340A1BFF4460363B2982C537AB48`.
  Installer: `8B7506E577E8819450D5E9744A26352A50FD1B859F9E62FE1F185244D3EFC2D1`.
- Existing product already closed. Verified470file installation backup plus
  1data file/registration/Start shortcut in
  artifacts/installer/host-update-20260913-shared-skin-02/.
  Install exit0;467payload hashes/ownership/registration/Start link and unchanged
  user data verified. Installed PID35520 launched, duplicate20992 exit0;
  one instance, original diagnostic data prefix retained without new errors.

No public release/commit/push. Browser preview unchanged. Actual user desktop
gesture/subjective acceptance is not claimed by static proof or automated tests.
