# Attached paw shoulder seam

User reported a dark line through the shoulder while pulling an attached paw.
The same artifact reproduced on a black WPF proof background. The local raster
sampler treated fixed shoulder row63 as transparent padding. Output row64 maps
between source rows63 and64 during stretch: at18px pull its alpha fell to112
instead of255. This was a coverage hole, not a second underlying skin outline.

The sampler now includes row63 as an interpolation apron. Clearing/rendering
still starts at64; fixed head and opposite-paw preservation are unchanged.
Hit testing uses the same corrected sampling. No source assets were repainted.

## Evidence

- Regression RED2 (opaque join expected255, actual238 at1px pull); GREEN in
  focused38. Both paws/open+closed frames and1/6/12/18px pulls are exercised.
- Proof: `artifacts/repro/perch-paw-seam-20260913/before.png` and `after.png`.
  Both paws, downward/outward pull and positive/negative wave samples inspected
  over black; horizontal shoulder gap removed.
- Scoped read-only review: no Critical/Important/Minor findings.
- Core241 and full app1046 PASS (zero failures/skips, app3m44s).
- Installer payload/refusal/build PASS; isolated native smoke PID47224 normal
  exit0, duplicate exit0. Tested RID App/Core parity and ZIP round-trip PASS.

## Delivery

Candidate: `candidate-20260913-perch-paw-seam-01`.
ZIP SHA256: `171B5C241D6DC0392E13AE09CBE1B0AC62A7E91C4488F49FED5BC9A0EAA278F2`.
App SHA256: `B5DE4566C10A3B740BD56EC1285DCC6E45789FF5EB0CAA34F6B14F68E9ACAB8B`.
Installer SHA256: `40E8FB26878A13C2BF952A1B7EA1DF29D9F59F13618AE9370088E69B157CC9DC`.

User closed previous app. Verified backup470installation files,1data file,
registration and Start shortcut in
`artifacts/installer/host-update-20260913-perch-paw-seam-01/`.
Installer exit0;467installed payload hashes, ownership, Start link, registration
and original data verified. Installed PID40096 launched; duplicate19532 exit0,
one product instance, original diagnostic prefix retained without new errors.

Native desktop live gesture acceptance remains with the user; WPF proof is not
a claim of physical mouse automation. Browser preview unchanged. No commit/push
or public release.
