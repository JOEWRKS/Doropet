# Hanging / perched ribbon pixel matching — 2026-09-13

User accepted the previous chin/hair fix and supplied two broken ribbon crops,
with a third ordinary-posture crop as the requested pixel reference.

## Evidence and change

Registered canonical ribbon coordinate (67,50) in authored08 is PBGRA
7/9/9/55 versus ordinary209/208/209/255. Perch outer tail (68,51) has alpha0.
The authored cutout loses the diagonal tail stroke and some near-edge fill;
the old eight-pixel white-loop protection does not repair this outline.

`AuthoredRibbonContour` hand-isolates the ordinary ribbon with row spans in
canonicalX60..70,Y36..61, excluding the rose and reference rump. It runs on
runtime copies through the already shared head registration: perch(+8,-6),
all8 authored keys, and all113 dense frames before caching.

- Upper loops replace RGBA, including translucent and transparent outer texels;
  source-over would incorrectly thicken their existing edge (A102 became163).
- The tail was drawn on white torso in the reference. Only its hand-selected
  outer texels are unmatted using ink70 (ordinary tipB71); white recomposition
  preserves the original colour. Interior remains ordinary opaque pixels.
- Tail alpha is composited over the posed source so early poses retain torso
  behind the ribbon. A transparent sprite rectangle is never pasted over body.
- Bilinear sampling follows existing head translations; no pose/timing changes.
- Authored resources, frozen gzip bank, accepted chin/hair fix remain unchanged.

## Tests and review

New ribbon regressions failed3/3 on the old output. A separate upper-alpha
regression failed on doubled coverage before replacement was corrected.
Ribbon+Supplied tests32/32 then passed, including all113 dense-frame PBGRA validity
and inner texture agreement; both sides of all key boundaries differ by<=1.
Early torso support and ordinary translucent-loop alpha also checked.

Existing original-file/bank hash checks remain. Old total foreground counts
cannot remain equal when adding missing ribbon pixels; they were replaced by
exact frozen-bank parity outside independently bounded art corrections for all
eight keys, in addition to existing per-pixel original RGB/matting checks.
The old white-loop test now compares the requested canonical RGB (rather than
JPEG254/255 noise) and verifies transparent background beyond the restored edge.

Scoped read-only reviewer: no actionable findings. Full App tests run before
publish, sequentially. No forced termination or changes to Codex settings.

Ignored WPF proof: `artifacts/repro/ribbon-20260913/`, `final-flat.png` compares
ordinary/perch/dangling/near-end dense output at native-ish and enlarged sizes
on white/black. `keys-final-flat.png` covers all8 keys. The intermediate
`final.png` has a nested-canvas diagnostic layout omission on the black row;
the flattened-layout proof supersedes it, not a runtime rendering change.

## Delivery

Live prior candidate PID25112 at start. Normal tray exit requested before
replacement; user confirmed, process absent and Stopped04:33:40KST verified.

- Full App919/919 PASS2m19s, `verification/app-ribbon.trx` in the repro directory.
- Fresh `candidate-20260913-ribbon-match-01` built only after full suite completed.
- Strict package/native PASS: exact App/Core RID parity, bundled8.0.31 runtime,
  apphost/icon/metadata/archive. Isolated owned10068 normalWM_CLOSE exit0;
  duplicate0. No forced user process stop.
- ZIP SHA256 `3C64B377C41F09E09523D2D7431217A1BD65DCB3D22EE49940C957B298011322`.
- App SHA256 `EA93C5684114A4911B7A689E7A486692447A1CBC034208968A615A63D579DD8D`.
- Core unchanged `2AC994181A0498B57D210D42AFD86ADBEBA7FC345A8E0916E2C7DBE50494B420`.
- Fresh empty inventory before normal launch29496 at04:35:57KST; exact candidate
  executable path and Started04:35:58KST verified.
- Raw09/raw08/gzip SHA256 remain identical to the preceding accepted candidate.

No installer promotion, commit, push or public release. New ribbon live user
acceptance is not inferred from automated checks and remains open.

Follow-up: user explicitly accepted with “아주 좋아 다음”. Ribbon live acceptance
is complete; the next step prepares an installer from this exact payload.
