# Dororong continuous subpixel outline — Task 1 authority report

Date: 2026-08-27

Task 1 freezes reference-only continuous line authority before any feasibility sweep or runtime body reconstruction. No reconstructed body candidate was generated or inspected during this task. Native body lines are location authority only; this report does not approve the committed native body's current stroke weight.

## Bound identity

- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- Starting committed HEAD: `fa0c2e0c0815ed4da9369fb54f7f3ecca90212ff`
- Approved predecessor authority: `1dbab91`
- Canonical source SHA-256: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- Body-region mask SHA-256: `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`
- Committed native-open SHA-256: `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`
- Reviewed continuous-authority fixture SHA-256: `D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639`

The source, mask, and native-open hashes were revalidated by the focused test and overlay tool. The authoring tool validates the fixture but never rewrites it.

## Literal authority schema

Every hair anchor and body normal retains its legacy source/native sample arrays as provenance and now contains literal integer-eighth endpoints:

```powershell
SourceNormal = @{ X1Eighth = [int]; Y1Eighth = [int]; X2Eighth = [int]; Y2Eighth = [int] }
NativeNormal = @{ X1Eighth = [int]; Y1Eighth = [int]; X2Eighth = [int]; Y2Eighth = [int] }
SourceFill = @([int]x,[int]y)
NativeFill = @([int]x,[int]y)
```

Hair anchors additionally contain literal `SourceInk` and `NativeInk` points. Source fill/ink points preserve the earlier `Fill`/`Ink` values. Native points are the brightest or darkest opaque legacy sample by Rec. 709 luminance, with ordinal `Y,X` ties. Coverage uses only the continuous fields after they exist.

Fix round 1 resolved an ambiguity in the original unqualified extrema wording. Four native hair legacy arrays contain a transparent absolute-darkest sample, while Task 1 separately requires every selected reference coordinate to be opaque. Because transparent RGB contributes no visible optical luminance, `brightest` and `darkest` are evaluated only over opaque legacy native samples. This clarification matches the implemented test contract and did not change any frozen authority coordinate or fixture byte.

## RED and GREEN evidence

Required RED, before any fixture field was added:

```text
command: pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
exit: 1
first causal line: Hair anchor 'LeftTempleStraight' is missing SourceNormal.
```

The fixture had no diff when that RED was captured.

The final focused GREEN is:

```text
command: pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
exit: 0
output: CONTINUOUS AUTHORITY PASS hash=D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639 hair=6 body=15
```

The focused test proves premultiplied bilinear sampling, the final-short-interval trapezoid rule, literal integral eighth coordinates, endpoint/reference bounds and opacity, six categorized hair anchors, all fifteen body names, protected mask-zero points, source/native hair one-run semantics, source body one-run/near-white/mask semantics, native body alpha location, production independence, mutation sensitivity, and the pinned final fixture hash.

The three authoring-tool mutations were exercised inside the focused GREEN and rejected with their required named errors:

- moved first source endpoint by `1/8` pixel: `Continuous authority hash changed.`
- swapped source fill and ink: `Hair anchor 'LeftTempleStraight' source fill luminance must exceed ink luminance.`
- duplicated the first source endpoint: `Hair anchor 'LeftTempleStraight' SourceNormal has duplicate endpoints.`

## Reference-derived line corrections

The deterministic first/last legacy centers were the starting rule. The following literal endpoints were corrected only after inspecting the pinned source/native references and exact `0.125`-pixel profiles:

- `LeftTempleStraight` source ends at `(28,126)` and native ends at `(10.5,54)`, before the separate inner fringe stroke.
- `RightFringeStraight` source starts at `(100,132)` and native starts at `(41,56)`, on the fill side rather than inside the antialiased run.
- `UpperLeftDiagonal` native ends at `(11,41)`, before unrelated darker hair pixels.
- `RightCrownDiagonal` source is `(105,76) -> (116,67)` and native is `(44,35) -> (51,30)`. Each crosses the same descending crown seam once, away from its upper junction and the separate right stroke. Exact runs are one; endpoint darkness is `0 / 0` at source and `0 / 0.031` at native.
- `FirstValley` source is `(70,168) -> (70,184)`, crossing the first exposed valley stroke once with an opaque near-white interval and mask intersection.
- `FirstValley` native is `(29,70) -> (29,80)`, crossing the matching location once after the front-inner junction.
- `FirstUnderside` source is `(75,170) -> (75,188)` and native is `(31,71) -> (31,81)`, crossing the adjacent underside before its center-leg turn instead of tracking along the stroke.
- `SecondValley` source ends at `(138,172)`, in the visible gap before the separate vertical leg. Native is `(55,70) -> (58,74)`, crossing the matching diagonal once and stopping before the vertical leg junction.

No tolerance, run threshold, source/mask/native hash, fill/ink selection rule, protected point, category, or required body name changed to admit these lines.

## Overlay evidence

Command:

```text
pwsh -NoProfile -File tools/New-ContinuousOutlineAuthority.ps1 -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png -MaskPath src/Dororong.App/Assets/dororong-body-region-mask.png -NativePath src/Dororong.App/Assets/dororong-canonical.png -AuthorityPath tests/fixtures/dororong-body-outline-authority.psd1 -EvidenceDirectory artifacts/verification/2026-08-27-continuous-authority-task1-attempt-2
```

Result: exit `0`.

- `artifacts/verification/2026-08-27-continuous-authority-task1-attempt-2/dororong-continuous-authority-source.png`: `FCD979C0184E4BD3EFBC9B726F08226D151953C3163FFB50EED5CCDE152CD1AC`
- `artifacts/verification/2026-08-27-continuous-authority-task1-attempt-2/dororong-continuous-authority-source-4x.png`: `4308A4DEB4CCB156E30707D7E51900F2CF8E56FF015F705A68B376E4D6B79E55`
- `artifacts/verification/2026-08-27-continuous-authority-task1-attempt-2/dororong-continuous-authority-native.png`: `2E7DEC40AC1F17E3DE08124B7E9F27ACFBF2FE766C2B6B60AC9C1842D79B3C5C`
- `artifacts/verification/2026-08-27-continuous-authority-task1-attempt-2/dororong-continuous-authority-native-4x.png`: `C7F21D719C332999E59540718621C814AF611546C99573DBEBE1702C8D759AEC`

The tool draws only blue hair normals, red body normals, green fill points, black hair-ink points, and endpoint labels over cloned pinned references. It writes no reconstructed, erased, recolored, or candidate body image. Attempt 1 was preserved and rejected after focused review because source `FirstUnderside` and native `FirstValley`, `FirstUnderside`, and `SecondValley` tracked a stroke or entered a junction; attempt 2 contains the corrected reviewed authority.

## Exact visual observations

The full source at 225 px, source nearest-neighbor 4x, full native at 96 px, and native nearest-neighbor 4x were opened and inspected. Each line also received a focused reference-only view. The expected observable for every entry was one crossing of its named visible stroke with no junction, protected part, separate stroke, or unrelated body segment.

Hair anchors:

- `LeftTempleStraight`: source and native cross the outer temple stroke once; both stop in the light gap before the separate inner fringe.
- `RightFringeStraight`: source and native begin on the fill side, cross the straight fringe stroke once, and end before the distinct farther-right hair edge.
- `UpperLeftDiagonal`: source and native cross the upper-left diagonal hair edge once; neither reaches the lower-left branch or eye region.
- `RightCrownDiagonal`: source and native cross the descending crown seam once below its upper junction; neither reaches the top crown ridge or separate right stroke.
- `CrownCurve`: source and native vertical lines cross the top crown curve once, from exterior/background side to pink fill side, without touching the adjacent crown seam.
- `LeftLowerCurve`: source and native cross the lower-left hair curve once and stop before the neighboring diagonal fringe branch.

Body normals:

- `FrontOuter`: source and native cross the front leg's outer side once; the upper neighboring stroke remains outside the line.
- `FrontFoot`: source and native cross the front foot bottom arc once, with clear space beyond the exposed contour.
- `FrontInner`: source and native cross the front leg's inner side once and remain below the separate upper valley/hair stroke.
- `FirstValley`: source and native vertical lines cross the first sloped valley segment once, clear of the front-inner vertical junction.
- `FirstUnderside`: source and native vertical lines cross the adjacent underside once before its center-leg turn; neither follows the stroke.
- `CenterOuter`: source and native cross the center leg's outer side once and do not enter the first-valley branch.
- `CenterFoot`: source and native cross the center foot bottom arc once.
- `CenterInner`: source and native cross the center leg's inner side once with no second segment on the line.
- `SecondValley`: source and native cross the rising diagonal valley stroke once and stop in light space before the separate vertical rear-leg stroke.
- `SecondUnderside`: source and native vertical lines cross the adjacent sloped underside once without touching the second-valley endpoint junction.
- `RearOuter`: source and native cross the rear leg's outer side once.
- `RearFoot`: source and native cross the rear foot bottom arc once, clear of its side strokes.
- `RearInner`: source and native cross the rear leg's inner side once; the separate upper-left segment remains outside the line.
- `UpperRearRim`: source and native cross the upper right body rim once without touching the protected ribbon/rose cluster.
- `LowerRearRim`: source and native cross the lower right body rim once without reaching the rear foot curve.

Overall visual verdict for the Task 1 authority overlays: PASS. This PASS covers only reference-line semantics and the exact overlay artifacts listed above. It does not establish source-only global-width feasibility, candidate stroke weight, packaged/runtime rendering, actual 96-DPI WPF rendering, or user acceptance. Task 2 remains blocked until this committed authority is independently reviewed at its handoff boundary.
