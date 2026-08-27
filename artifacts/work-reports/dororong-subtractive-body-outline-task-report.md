# Dororong subtractive native-96 body correction — Task 1 report

## Result

**Repository native-asset visual verdict: PASS.** The exact regenerated open and closed 96px runtime PNGs use one reviewed subtractive mask over the deterministic source-derived resize. All 58 changed coordinates are fully opaque baseline pixels, all RGB changes are channel-wise lightening, the open and closed correction sets and corrected RGB values are identical, and partial-alpha pixels are byte-identical to the baseline.

This verdict is limited to exact repository assets at native 96px and nearest-neighbor inspection. It does not replace or upgrade actual-Windows attempt 5; `docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-5.md` remains frozen FAIL evidence.

## Implementation

- Replaced the rejected Bézier redraw, supersampled path masks, neighborhood fill search, and repaint loop with `$nativeBodyLightenRuns` and `Apply-SubtractiveNativeBodyCorrection`.
- The correction clones the unmodified native baseline before sampling fill colors, validates opaque run and fill pixels, rejects any fill sample that would darken a channel, and linearly blends only the literal reviewed run coordinates.
- Kept the pinned source, boundary transparency extraction, `Resize-ToNative96`, closed-eye derivation, WPF presenter arrangement, alpha hit testing, state mapping, and all behavior code unchanged.
- Replaced attempt-5 topology/redraw assertions with a subtractive contract over the real generator output and its `BaselineOutputDirectory` output.
- Retained source identity/dimensions, alpha-zero hygiene, partial-alpha byte identity, eye semantics, clean-hair references, WPF arrangement, hit testing, and state mapping.
- Removed `Test-InBodyTopologyBand`, connected-component support checks, spur probes, the 13 attempt-5 body profiles, and the `correction >= 120` redraw requirement.

## Test-driven evidence

The production change named by the new test is: a body correction that redraws or darkens any deterministic native baseline channel must fail.

Two preliminary test-only scan/order attempts were discarded before production edits because the opacity guard fired first on rejected partial-alpha changes at `(75,58)` and `(17,73)`. Production and committed runtime assets were unchanged. The accepted full-frame channel pass below then produced the required causal RED.

### RED build

Command:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
```

Exit code: `0`

Output:

```text
복원할 프로젝트를 확인하는 중...
D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\src\Dororong.Core\Dororong.Core.csproj을(를) 130밀리초 동안 복원했습니다.
D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\src\Dororong.App\Dororong.App.csproj을(를) 133밀리초 동안 복원했습니다.
Dororong.Core -> D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\src\Dororong.Core\bin\Release\net8.0\Dororong.Core.dll
Dororong.App -> D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\src\Dororong.App\bin\Release\net8.0-windows\Dororong.App.dll

빌드했습니다.
    경고 0개
    오류 0개

경과 시간: 00:00:03.16
```

### Genuine RED exact-art test

Command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Exit code: `1`

Output:

```text
Exception: D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\tests\Dororong.App.ExactArt.Tests.ps1:12
Line |
  12 |      if (-not $Condition) { throw $Message }
     |                             ~~~~~~~~~~~~~~
     | Open body correction darkened a baseline channel at (18,70).
```

This is the expected causal failure against the unchanged rejected generator.

### Candidate regeneration

Command:

```powershell
pwsh -NoProfile -File tools/Generate-CanonicalArt.ps1 -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png -OutputDirectory src/Dororong.App/Assets
```

Exit code: `0`

Output:

```text
Generated deterministic native-96 open and closed Dororong runtime frames.
```

### Final GREEN exact-art test

Command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Exit code: `0`

Output:

```text
NATIVE96 PIXEL EVIDENCE: correction=58, bounds=18,67..72,85.
EXACT ART PASS: exact 225px authority, deterministic native-96 frames, subtractive body invariants, independent body protection, identical eye-state correction, alpha hygiene, clean-hair references, eye semantics, 96-DPI one-to-one presenter, native alpha hit testing, and state mapping passed.
```

## Fresh completion verification

Command:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
git diff --check
```

Exit code: `0`

Decisive output:

```text
빌드했습니다.
    경고 0개
    오류 0개
NATIVE96 PIXEL EVIDENCE: correction=58, bounds=18,67..72,85.
EXACT ART PASS: exact 225px authority, deterministic native-96 frames, subtractive body invariants, independent body protection, identical eye-state correction, alpha hygiene, clean-hair references, eye semantics, 96-DPI one-to-one presenter, native alpha hit testing, and state mapping passed.
FINAL VERIFICATION EXIT 0
```


## Artifact identity and pixel evidence

| Artifact | Size / format | Bytes | SHA-256 |
|---|---:|---:|---|
| Approved source `dororong-canonical-source.png` | 225x225 | 45,681 | `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504` |
| Uncorrected open native-96 baseline | 96x96, 32bpp ARGB | 10,044 | `3B3D171D2C62134284915D7D162D43F36263761D4EA6344F4AC8BCEA730C5B59` |
| Uncorrected closed native-96 baseline | 96x96, 32bpp ARGB | 10,009 | `A8D0A075451BA253700D8373A0FD212821F97EDFEB3F88448809844353CF78BC` |
| Candidate open `dororong-canonical.png` | 96x96, 32bpp ARGB | 10,029 | `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3` |
| Candidate closed `dororong-closed-eyes.png` | 96x96, 32bpp ARGB | 9,995 | `B2ADEC262AA15E9BE86D4DFC9A2087CE5514C06145A337715C145BF2671B12E4` |

- Literal run entries: `46`.
- Unique changed coordinates: open `58`, closed `58`.
- Coordinate-set identity: `True`; corrected RGB equality at all correction coordinates passed.
- Bounds: `(18,67)..(72,85)`.
- Partial-alpha changes: open `0`, closed `0`.
- Alpha changes: `0`.
- Per-channel deltas over changed pixels: `+8..+115`; no channel darkened.
- The run-coordinate total is also `58`, proving the literal runs do not overlap.
- Pinned source hash remained unchanged.

## Reviewed native body lighten runs

Every fill sample below is baseline alpha `255`. Unless an RGB value is written beside it, its RGB is `255,255,255`.

| Segment | Literal runs: `y xStart..xEnd <- fill(x,y), blend` |
|---|---|
| Lower rear rim | `67 72..72 <- (71,67), .55`; `68 71..71 <- (70,68), .55`; `69 70..70 <- (69,69) RGB253,253,253, .55`; `70 70..70 <- (69,70), .55`; `71 69..69 <- (68,71), .55`; `72 69..69 <- (68,72) RGB252,252,252, .55`; `73 68..68 <- (67,73), .55`; `74 68..68 <- (67,74), .55` |
| Front outer | `72 18..18 <- (19,72), .55`; `73 18..18 <- (19,73), .55`; `74 19..19 <- (20,74) RGB251,251,251, .55`; `75 19..19 <- (20,75), .55`; `77 20..20 <- (21,77), .55`; `78 21..21 <- (22,78), .55`; `79 22..22 <- (23,79) RGB249,249,249, .55` |
| Front foot | `80 23..24 <- (23,79) RGB249,249,249, .50` |
| Front inner | `76 25..25 <- (24,76), .55`; `77 25..25 <- (24,77), .55`; `78 25..25 <- (24,78), .55`; `79 24..24 <- (23,79) RGB249,249,249, .55` |
| First valley | `74 28..28 <- (28,73), .55` |
| First underside | `75 29..34 <- (31,74), .45` |
| Center outer | `76 35..35 <- (36,76), .55`; `77 35..35 <- (36,77), .55`; `79 36..36 <- (37,79), .55`; `80 36..36 <- (37,80) RGB245,245,245, .55`; `81 37..37 <- (38,81), .55`; `82 38..38 <- (39,82), .55`; `83 39..39 <- (40,83), .55`; `84 41..41 <- (42,84), .55` |
| Center foot | `85 42..45 <- (42,84), .50` |
| Center inner | `77 46..46 <- (45,77) RGB254,254,254, .55`; `83 46..46 <- (45,83), .55`; `84 46..46 <- (45,84), .55` |
| Second valley | `75 52..52 <- (52,74), .55` |
| Second underside / rear outer | `74 60..60 <- (61,74), .55`; `75 60..60 <- (61,75), .55`; `76 60..60 <- (61,76), .55`; `78 61..61 <- (62,78), .55`; `79 62..62 <- (63,79), .55`; `80 62..62 <- (63,80), .55`; `81 63..63 <- (64,81), .55` |
| Rear foot | `82 64..67 <- (65,81), .50` |
| Rear inner | `78 69..69 <- (68,78), .55`; `79 69..69 <- (68,79), .55`; `81 68..68 <- (67,81), .55` |
| Upper rear rim | No correction selected: source-derived native inspection showed one fully opaque support plus antialiasing, not a visibly doubled fully opaque run relative to the fixed clean-hair references. |

No broad rectangle or inferred curve is present. Each selected coordinate is the inside-most fully opaque ink in its reviewed local run, and the outer source-derived dark support is unchanged.

## Native visual inspection

### Approved source inspected alone before baseline/candidate

Authority: `src/Dororong.App/Assets/dororong-canonical-source.png`, SHA-256 `F96E...6504`.

Frozen identity anchors:

1. No tail.
2. Pink hair overlaps the front body at the neck.
3. The rose sits above the dark bow, with the two pale ribbon shapes behind them.
4. Three separated leg silhouettes remain, with two open negative-space valleys.
5. The rear body rim flows into the rear leg; the outer body edge is source-derived rather than a newly drawn contour.

### Surfaces inspected

- Uncorrected open baseline alone at native 96px on white.
- Uncorrected open baseline alone at native 96px on RGB `(18,20,28)`.
- The same baseline at 8x nearest-neighbor on both backgrounds.
- Exact candidate open and closed frames at native 96px on both backgrounds.
- Exact candidate open and closed frames at 8x nearest-neighbor on both backgrounds.
- Candidate body crop at 16x nearest-neighbor on both backgrounds for segment/endpoint inspection.

### Check record

| Check | Expected observable | Concrete observed fact | Verdict |
|---|---|---|---|
| Source identity anchors | No tail; hair-neck overlap; rose/bow/ribbon order; three legs/two valleys; source-derived rear rim | All five were visible in the approved source before any baseline or candidate was opened. | PASS |
| Baseline translation | Native resize preserves the five source anchors | At native 96px and 8x on both backgrounds, the baseline had no tail, retained the hair-neck overlap and accessory order, and showed three separated legs with two open valleys and an unshifted rear silhouette. | PASS |
| Front outer / inner / foot | Lighter inner mass without shifted edge, missing span, or new endpoint | The unchanged outer support remains continuous from the hair occlusion down the front outer edge, around the foot, and up the inner edge. The foot remains closed and readable on white and dark. | PASS |
| First valley / underside | Valley remains open; underside remains a single connected contour | The gap between front and center legs remains visibly open at native scale. The underside is lighter internally but retains one continuous outer support with no dotted gap or branch. | PASS |
| Center outer / foot / inner | Center leg stays distinct and continuous | The center leg remains the lowest of the three silhouettes, its foot stays closed, and both sides remain connected at native and enlarged scale. No new endpoint is exposed. | PASS |
| Second valley / underside | Second gap remains open; no spur or bridge is created | The center/rear gap remains open on both backgrounds. The source-derived underside support is continuous and has no added branch, bridge, or isolated knot. | PASS |
| Rear outer / foot / inner | Rear leg and foot retain a continuous outer silhouette | The rear leg remains distinct from the center leg; the foot is closed; the inner and outer supports meet without a missing span or white fringe. | PASS |
| Upper / lower rear rim | Upper rim unchanged; lower rim lighter but continuous into rear leg | The upper rim bytes are untouched. The lower rim retains its same alpha edge and endpoint, and visually flows into the rear leg on both backgrounds. | PASS |
| Legal occlusion endpoints | Hair-neck and ribbon/rear-rim contacts stay in their approved order | Hair still occludes the body at the neck. Rose, bow, and pale ribbons keep their ordering; no body change reaches those pixels. | PASS |
| Protected parts | No damage to face, hair, rose, bow, ribbons, eyes, or mouth | Changed bounds and literal coordinates are confined to the body band. Native and 8x inspection shows no change to protected parts; open/closed eye semantics remain distinct. | PASS |
| Alpha edge / surface response | No shifted edge, new fringe, or partial-alpha repaint on white or dark | Alpha is byte-identical everywhere; partial-alpha change count is zero. White and dark surfaces show the same silhouette with no newly exposed white fringe. | PASS |
| Open / closed state consistency | Body correction is identical across eye states | Both sets contain exactly 58 identical coordinates, and RGB is identical at each corrected body coordinate; visual body contours match between open and closed frames. | PASS |
| Unexpected high-salience defects | No new branch, endpoint, shifted edge, fringe, missing span, broken valley, or protected-part damage | Full-frame native and enlarged inspection found none of those defects in either state or background. | PASS |

Overall for the exact repository native assets: **PASS**.

Boundary: actual-Windows rendering/user acceptance was not rerun by this task and remains governed by the frozen attempt-5 FAIL record. Repository asset PASS cannot upgrade that layer.

## Files

Task commit paths:

- `tools/Generate-CanonicalArt.ps1`
- `tests/Dororong.App.ExactArt.Tests.ps1`
- `src/Dororong.App/Assets/dororong-canonical.png`
- `src/Dororong.App/Assets/dororong-closed-eyes.png`
- `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`

Coordination report, deliberately outside the five-path task commit:

- `.superpowers/sdd/2026-08-27-dororong-subtractive-body-outline/task-1-report.md`

No `docs/verification/*manual-attempt*.md` file was edited or staged.

## Self-review

- Confirmed the old redraw symbols are absent: `New-NativeBodyContour`, `New-PathMask`, `Get-LocalBodyFill`, and `Apply-NativeBodyCorrection`.
- Confirmed both eye-state paths call only `Apply-SubtractiveNativeBodyCorrection`.
- Confirmed all 46 constructor entries contain real reviewed coordinates; there is no dummy entry.
- Confirmed the 46 runs expand to 58 unique coordinates and do not overlap.
- Confirmed every fill/run pixel is opaque and every fill channel is at least the corresponding original channel.
- Confirmed no resize, transparency, eye stencil/lid data, presenter, hit-testing, state mapping, or behavior code changed.
- Confirmed source hash is still pinned and unchanged.
- Confirmed the exact-art test exercises the real generator rather than source-text matching or mocks.
- Confirmed the final focused output is clean and exits `0`.
- Confirmed manual-attempt files remain untracked and unstaged.

## Concerns

- Actual-Windows attempt 5 remains frozen FAIL evidence and was not rerun. The completed claim is intentionally limited to deterministic repository assets at native 96px.

## Review fix round 1 — freeze the reviewed mask and outer supports

### Finding addressed

The first task commit allowed any future fully opaque, channel-wise lightening inside the broad body protection rectangles. That could admit unreviewed drift at preserved outer supports such as `(17,72)` and `(46,85)`.

This fix changes only the exact-art regression and reports. The visually approved generator and open/closed assets remain byte-identical to commit `40b87ee582c29f16ffd265162e16dcb04a8cde94`.

### Exact reviewed mask fixture

Canonicalization is independent of generator source structure:

1. Enumerate the actual generated-open versus uncorrected-open changed-coordinate set.
2. Convert it to a string array.
3. Sort with `[StringComparer]::Ordinal`.
4. Join `x,y` strings with a single LF (`\n`) and no trailing LF.
5. Hash the UTF-8 bytes with SHA-256.

The required diagnostic was printed before pinning:

```text
NATIVE96 MASK DIAGNOSTIC: count=58, sha256=B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB.
NATIVE96 PIXEL EVIDENCE: correction=58, bounds=18,67..72,85; mask=B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB.
EXACT ART PASS: exact 225px authority, deterministic native-96 frames, pinned subtractive mask, frozen segment/outer-support fixtures, independent body protection, identical eye-state correction, alpha hygiene, clean-hair references, eye semantics, 96-DPI one-to-one presenter, native alpha hit testing, and state mapping passed.
```

Pinned values:

- Coordinate count: `58`.
- SHA-256: `B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB`.
- The test uses a direct `Assert-Equal` against both values; broad-band membership cannot admit an added or removed coordinate.

### Exact-mask mutation RED

A temporary test-only mutation replaced reviewed coordinate `19,75` with frozen outer support `17,72` in both observed correction sets. Count remained `58`, so the exact mask assertion—not the count—had to catch the drift. The synthetic lines were removed immediately after this run.

Command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Exit code: `1`.

Output:

```text
The reviewed subtractive native body correction mask changed. Expected
'B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB', observed
'DF04D297CF384A95DF03A8F67011EA2FBAB43DA04121F0A9CA9033AD9223A86A'.
```

### Literal reviewed correction probes

These expectations are handwritten from the approved subtractive candidate rather than derived from `$nativeBodyLightenRuns`. Both open and closed correction sets must contain each coordinate.

| Named segment | Reviewed correction probe |
|---|---:|
| Front outer | `19,75` |
| Front inner | `25,77` |
| Front foot | `23,80` |
| First valley | `28,74` |
| First underside | `31,75` |
| Center outer | `36,79` |
| Center foot | `43,85` |
| Center inner | `46,84` |
| Second valley | `52,75` |
| Second underside | `60,75` |
| Rear outer | `62,79` |
| Rear foot | `66,82` |
| Rear inner | `69,79` |
| Lower rear rim | `69,72` |

The upper rear rim intentionally has no correction in the approved candidate and is protected by the outer-support fixture below.

### Literal frozen outer-support and occlusion fixture

For every coordinate, both generated open and generated closed RGBA must remain byte-identical to their corresponding uncorrected baselines.

| Named segment / endpoint | Coordinate | Frozen uncorrected-open RGBA |
|---|---:|---|
| Front outer | `17,72` | `A255/RGB11,11,11` |
| Front inner | `26,77` | `A255/RGB32,32,32` |
| Front foot | `22,80` | `A255/RGB20,20,20` |
| First valley | `28,75` | `A247/RGB8,8,8` |
| First underside | `32,76` | `A255/RGB43,43,43` |
| Center outer | `35,79` | `A255/RGB9,9,9` |
| Center foot | `46,85` | `A255/RGB13,13,13` |
| Center inner | `47,83` | `A255/RGB15,15,15` |
| Second valley | `52,76` | `A255/RGB28,28,28` |
| Second underside | `59,75` | `A255/RGB40,40,40` |
| Rear outer | `61,79` | `A255/RGB22,22,22` |
| Rear foot | `66,83` | `A255/RGB20,20,20` |
| Rear inner | `70,78` | `A253/RGB30,30,30` |
| Upper rear rim | `74,62` | `A255/RGB52,52,52` |
| Lower rear rim | `70,72` | `A240/RGB42,42,42` |
| Hair-neck legal occlusion endpoint | `17,69` | `A255/RGB118,116,117` |
| Ribbon/rear-rim legal occlusion endpoint | `74,57` | `A255/RGB49,49,49` |

### Final focused verification

Command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Exit code: `0`.

Output:

```text
NATIVE96 PIXEL EVIDENCE: correction=58, bounds=18,67..72,85; mask=B52663439383D7B9E52D0664F0248E8A084EC21D041F1B7D565D2D4FA2EF11EB.
EXACT ART PASS: exact 225px authority, deterministic native-96 frames, pinned subtractive mask, frozen segment/outer-support fixtures, independent body protection, identical eye-state correction, alpha hygiene, clean-hair references, eye semantics, 96-DPI one-to-one presenter, native alpha hit testing, and state mapping passed.
```

### Scope and remaining boundary

- Changed implementation path: `tests/Dororong.App.ExactArt.Tests.ps1`.
- Updated durable report: `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`.
- Updated ignored coordination report: `.superpowers/sdd/2026-08-27-dororong-subtractive-body-outline/task-1-report.md`.
- Source, generator, open asset, closed asset, and all manual-attempt files are unchanged.
- The independent native visual PASS stands; actual-Windows attempt 5 remains frozen FAIL evidence and is not upgraded.
