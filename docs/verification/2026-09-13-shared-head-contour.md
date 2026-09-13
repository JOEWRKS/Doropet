# Shared lower-head repair

User confirms both dangling hollow hair and perched double chin remain defects,
then approves fixing both. Screenshot `codex-clipboard-7ea34e0e-58f6-4cab-bfd3-9d182f4c9831.png`
matches the remaining lower-chin location. Prior trials are explicitly rejected.

## Root cause and change

The previous PerchExpressionFrames repair stopped at source09 row59. AtX38,
rows60/61 remained B78/188; registered canonical has181/255. The prior fix left
the dark lower contour immediately below its patch. It also affected only09:
BodyDragHold uses authored08 while fractional entry/release uses a separate
113-frame embedded bank. Neither consumed the previous contour correction.

`AuthoredHeadContour` owns one bounded lower-chin and right-hair patch. Source09
uses offset(+8,-6). Eight authored drag head offsets against canonical are
(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16), measured by upper
pink texture matching (mean RGB error below5). Intermediate frames interpolate
these offsets. Bilinear mask sampling keeps boundaries continuous without
re-registering on the UI thread. The source copy's old ink is replaced by the
canonical lower head composited onto the white chest, not overlaid with a second
head. The repair is applied once to prepared keys and loaded dense-bank frames.

The mask spans canonical chinX20..41/Y59..67 and tapered right-hairX39..52/Y58..71.
Source formats remain BGRA/PBGRA as appropriate so unedited pixels do not acquire
round-trip quantization. Source resources and old generator inputs remain unchanged.
Body movement, eye timing, foot alignment, grips, pointing and native window logic
are outside this change. No GPU or Codex settings are modified.

## Verification

- New literal chin/hair regressions RED4 before production change; then pass.
- Existing nearly-opaque-white/upper-face controls caught an initial format
  conversion regression (one-level outside RGB change); format preservation fixed it.
- Old whole-frame hash assertions intentionally fail after requested art edits.
  Tests still verify the original embedded hashes, then compare every pixel
  outside independent repair bounds; original file hashes, sole registration,
  coverage count, entry/reverse/hold agreement remain checked.
- Focused Perch/Flutter/Head/Supplied/Recovery/Surrounding180/180 passed26s.
- Additional all8key/dense-bank hair tests and two-sided boundary continuity:
  17/17 passed. Scoped independent read-only review found no issues.
- Ignored small WPF diagnostic `artifacts/repro/hanging-contours-20260913/`
  compares ordinary, perch, dangling08, near08 and flutter on black/white.
  Its `before.png` path is a working comparison, updated after the correction.
- Full App verification runs sequentially before packaging, not concurrently.

Raw09 SHA256: `3CE4BE5308759D35BA828237208521A085378180AAD035CA0474DA3E551F57C2`.
Raw08 SHA256: `DAF9726E9DBA7A2274EF421828826BD56CF556B9DC6801B2428D6D74BBCF2E56`.
Frozen bank SHA256: `B2EEEDF800C66D47648346A51C69E1B484021475FC69B968CC6C8B92BD8E5893`.

## Delivery boundary

Product inventory was empty during this work; no user process was stopped or
restarted yet. Previous Codex hang has a Windows event but no established causal
link to Dororong. User visual acceptance remains necessary and is not inferred
from passing tests. No installer promotion, commit, push or public release.

Final sequential full App912/912 passed2m18s; evidence:
`artifacts/repro/hanging-contours-20260913/verification/app-shared-head-contour.trx`.
Fresh `candidate-20260913-shared-head-contour-01` then built and passed strict
package/runtime/archive/RID parity and native smoke (owned40880 normalWM_CLOSE
exit0; duplicate0). ZIP SHA256
`8B7D165657F0E0CAA2FA43B68FF30BBC185EECC65DDBB08FD95A8708C46406CF`.
App SHA256 `5AE0DF12089E933B826F98D776CD105849BDAF19FBDF698B7FDD78F828555034`.
Core unchanged `2AC994181A0498B57D210D42AFD86ADBEBA7FC345A8E0916E2C7DBE50494B420`.
Fresh empty process inventory before normal launch25112 at04:14:34KST; exact
executable path and Started event verified. Live visual acceptance remains open.

Subsequent user confirmation: “수정 잘 됨”. Chin/hair correction accepted;
the new hanging/perched ribbon pixel report is a separate follow-up scope.
