# Dororong Continuous Subpixel Outline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the rejected pixel-center body-outline mechanism with a source-authoritative continuous subpixel rasterizer that proves one global hair-matched width before generating the exact 96px runtime assets.

**Architecture:** Keep the approved 225px binary body mask and freeze a continuous test-only optical authority before feasibility work. Shared raster modules remove the source background, derive exposed half-pixel contour segments and two legal occlusion continuations, build the exact source-only fill field, and precompute `8x8` subpixel distances; an independent continuous test integrates fixed normals at `1/8`-pixel spacing. Only after the `0.25..4.00` / `1/64` feasibility sweep finds a non-empty global intersection does the generator produce source/native candidates and replace the committed runtime frames.

**Tech Stack:** PowerShell 7, .NET `System.Drawing`, C# / .NET 8 / WPF, Pester-free executable PowerShell tests, Git.

**Spec:** `docs/specs/2026-08-26-dororong-m1-design.md`

**Predecessor state:** This plan supersedes Tasks 2–4 of `docs/plans/2026-08-27-dororong-source-silhouette-outline.md`. Its Task 1 mask/authority work remains approved through commit `1dbab915f3d7d6091ef199a75f93fab52fa095b9`; the rejected pixel-center implementation was never committed and its product files were restored before this plan began. Plan base is specification commit `c0fa57eaea3c8ed086f92934c43e04cfded68691`.

## Global Constraints

- The pinned 225x225 source remains byte-identical at SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- The approved 225x225 binary mask remains byte-identical at SHA-256 `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`; it is opaque 32bpp ARGB with equal RGB channels restricted to `0` or `255`.
- The pre-candidate native-open reference used only to locate hair/body normals is SHA-256 `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`; its body weight is not an acceptance target.
- Source alpha never changes. Before the final resize, RGB may change only where the mask is `255`; all mask-zero source pixels remain byte-identical.
- Dororong remains tail-less with the same three legs, two valleys, rear rim, protected parts, layer order, and legal hair/rear-ribbon occlusion endpoints.
- An exposed contour segment is a half-pixel mask edge bordering source-authoritative transparent exterior. Each legal occlusion continuation runs from the nearest exposed contour vertex to one reviewed writable endpoint; no other protected contact emits hidden outline.
- Outline coverage uses exactly `8x8` source-pixel sample centers at `((i+0.5)/8,(j+0.5)/8)` and Euclidean distance to the approved contour.
- Eligible fill seeds have source alpha `255`, mask `255`, every RGB channel `>=225`, and channel spread `<=8`. Eligible pixels retain exact RGB; other writable pixels within `8.0` source pixels of the contour use the eight nearest seeds, weight `1/(1+d^2)`, ordinal `Y,X` ties, and ties-to-even channel rounding.
- The outline color is the component-wise median of production-owned clean hair samples. The body uses one global source width; segment widths, local strokes, hand-tuned native pixels, post-resize corrections, and candidate-derived targets are forbidden.
- Continuous authority normals are literal rational source/native line endpoints fixed before feasibility. Measurement bilinearly samples alpha/RGB at `1/8`-pixel intervals and trapezoid-integrates alpha-weighted normalized Rec. 709 darkness.
- Feasibility sweeps widths `0.25..4.00` inclusive in `1/64` increments. Every source body normal must be within 10% of the frozen source hair median. An empty intersection blocks runtime candidate creation and asset commits.
- Native 96px acceptance keeps every body normal within `0.35` equivalent opaque pixel of the native hair median and body max-minus-min at `<=0.50`.
- Closed eyes are derived from the reconstructed open source frame. Open/closed body pixels are identical, and both frames pass through the existing single premultiplied high-quality whole-frame resize exactly once.
- Automated checks and repository images cannot promote actual Windows appearance. The live open-eye body is accepted by the user first; closed-eye integration is checked only after that PASS.
- Existing untracked manual-attempt 2–6 records are user/root-owned evidence. Tasks must not modify, stage, delete, or rename them.

---

### Task 1: Freeze continuous normal authority before feasibility

**Files:**
- Create: `tests/support/Dororong.ContinuousOptics.ps1`
- Create: `tools/New-ContinuousOutlineAuthority.ps1`
- Create: `tests/Dororong.App.ContinuousAuthority.Tests.ps1`
- Modify: `tests/fixtures/dororong-body-outline-authority.psd1`
- Create: `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`

**Interfaces:**
- Consumes: approved source/mask hashes, existing six categorized `HairAnchors`, fifteen named `BodyNormals`, protected points, and the current committed native open frame for hair-only native reference selection.
- Produces: test-only literal `SourceNormal` / `NativeNormal` rational endpoints and source/native fill/ink references; `Measure-DororongContinuousCoverage`; source/native authority overlays; a reviewed authority hash and report for Task 2.

- [ ] **Step 1: Add a failing continuous-authority contract without changing the fixture**

Create `tests/support/Dororong.ContinuousOptics.ps1` with test-only functions:

```powershell
function Get-DororongBilinearPremultipliedSample(
    [Drawing.Bitmap]$Bitmap, [double]$X, [double]$Y)
{
    $x0 = [Math]::Floor($X); $y0 = [Math]::Floor($Y)
    $fx = $X - $x0; $fy = $Y - $y0
    $weights = @(
        @($x0,   $y0,   (1-$fx)*(1-$fy)),
        @($x0+1, $y0,   $fx*(1-$fy)),
        @($x0,   $y0+1, (1-$fx)*$fy),
        @($x0+1, $y0+1, $fx*$fy)
    )
    $a = 0.0; $pr = 0.0; $pg = 0.0; $pb = 0.0
    foreach ($entry in $weights)
    {
        $px = [int]$entry[0]; $py = [int]$entry[1]; $weight = [double]$entry[2]
        if ($px -lt 0 -or $py -lt 0 -or $px -ge $Bitmap.Width -or $py -ge $Bitmap.Height) { continue }
        $pixel = $Bitmap.GetPixel($px,$py); $alpha = $pixel.A / 255.0
        $a += $weight * $alpha
        $pr += $weight * $alpha * ($pixel.R / 255.0)
        $pg += $weight * $alpha * ($pixel.G / 255.0)
        $pb += $weight * $alpha * ($pixel.B / 255.0)
    }
    if ($a -le 0.0) { return [pscustomobject]@{ A=0.0; R=0.0; G=0.0; B=0.0 } }
    return [pscustomobject]@{ A=$a; R=$pr/$a; G=$pg/$a; B=$pb/$a }
}

function Measure-DororongContinuousCoverage(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [Drawing.Color]$Fill,
    [Drawing.Color]$Ink,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    if ($length -le 0.0) { throw 'Continuous normal has zero length.' }
    $fillL=(0.2126*$Fill.R)+(0.7152*$Fill.G)+(0.0722*$Fill.B)
    $inkL=(0.2126*$Ink.R)+(0.7152*$Ink.G)+(0.0722*$Ink.B)
    if ($fillL -le $inkL) { throw 'Fill luminance must exceed ink luminance.' }
    $positions=[Collections.Generic.List[double]]::new()
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing) { $positions.Add($distance) }
    $positions.Add($length)
    $values=@()
    foreach ($distance in $positions)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        $luminance=255.0*((0.2126*$sample.R)+(0.7152*$sample.G)+(0.0722*$sample.B))
        $darkness=[Math]::Clamp(($fillL-$luminance)/($fillL-$inkL),0.0,1.0)
        $values += $sample.A*$darkness
    }
    $integral=0.0
    for ($index=0; $index -lt $positions.Count-1; $index++)
    { $integral += 0.5*($values[$index]+$values[$index+1])*($positions[$index+1]-$positions[$index]) }
    return $integral
}
```

The sampler interpolates premultiplied RGB and alpha from the four surrounding pixels, returns transparent black outside the bitmap, and unpremultiplies only when interpolated alpha is nonzero. The measurement walks from the first endpoint toward the second at exact `0.125`-pixel intervals, includes a final shorter interval, evaluates normalized Rec. 709 darkness clamped to `[0,1]`, and trapezoid-integrates darkness times alpha times interval length.

Create `tests/Dororong.App.ContinuousAuthority.Tests.ps1`. It pins the source/mask/native-open hashes and requires every hair/body entry to contain these exact literal fields:

```powershell
SourceNormal = @{ X1Eighth = [int]; Y1Eighth = [int]; X2Eighth = [int]; Y2Eighth = [int] }
NativeNormal = @{ X1Eighth = [int]; Y1Eighth = [int]; X2Eighth = [int]; Y2Eighth = [int] }
SourceFill = @([int]x,[int]y)
NativeFill = @([int]x,[int]y)
```

Hair anchors additionally require `SourceInk` and `NativeInk`. Test that each endpoint field is integral, the two endpoints differ, coordinates divided by eight remain in bounds, each reference coordinate is in bounds and opaque, hair anchor categories remain at least two each, all fifteen body names remain present, protected points remain mask-zero, and the production generator/module text contains no reference to this fixture or test helper.

- [ ] **Step 2: Run focused RED**

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
```

Expected: exit `1` with the first exact missing field, `Hair anchor 'LeftTempleStraight' is missing SourceNormal.` Record the command, exit, and first causal line.

- [ ] **Step 3: Add literal continuous fields by a deterministic pre-candidate rule**

For every existing source/native sample array, set the normal endpoints to the first and last legacy sample centers, encoded as coordinate times eight. Preserve the legacy arrays as provenance during this task; no coverage test may use them after the new fields exist. Preserve each existing source `Fill`/`Ink` as `SourceFill`/`SourceInk`. For every native hair anchor, select `NativeFill` as the brightest and `NativeInk` as the darkest existing native sample by Rec. 709 luminance, with ordinal `Y,X` ties. For every body normal, preserve its existing source `Fill` as `SourceFill` and select `NativeFill` as the brightest existing native sample by the same tie rule. Write all selected coordinates literally into the PSD1; the authoring script validates them but never rewrites the fixture.

- [ ] **Step 4: Validate that each continuous line is a real, single clean normal**

The focused test samples the pinned source/native reference along each line at `0.125` spacing. Hair lines must contain exactly one contiguous darkness run at threshold `0.10`, have near-zero darkness at both ends, and have a positive integral. Body source lines must contain exactly one contiguous original-body-ink run, have at least one near-white fill-side interval, and intersect the approved mask. Native body lines are location authority only at this stage; require in-bounds, nonzero length, and intersection with the committed body alpha, but do not approve their current body thickness.

The test pins the final PSD1 SHA-256 only after these semantic checks pass. It also proves a copied fixture with one moved endpoint, one swapped fill/ink reference, or a duplicate endpoint fails with a named error.

- [ ] **Step 5: Generate and inspect source-only authority overlays**

`tools/New-ContinuousOutlineAuthority.ps1` accepts `-SourcePath`, `-MaskPath`, `-NativePath`, `-AuthorityPath`, and `-EvidenceDirectory`. It validates all pinned hashes and draws only reference overlays: blue hair normals, red body normals, green fill points, black ink points, and labeled endpoints at native scale plus nearest-neighbor 4x. It must not reconstruct, erase, recolor, or write a body candidate.

Inspect the exact source/native overlays and their 4x versions. Every line must cross the named visible stroke once without crossing a junction, protected part, separate stroke, or unrelated body segment. Record concrete observations for all six hair anchors and all fifteen body normals. If a line is invalid, correct only its literal authority coordinates before any feasibility work and rerun the mutation checks.

- [ ] **Step 6: Run GREEN and commit the authority**

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
```

Expected: both exit `0`, source/mask hashes unchanged, output names the PSD1 hash, six hair anchors, fifteen body normals, and continuous-authority PASS.

Append RED/GREEN, fixture hash, literal field schema, mutation results, overlay hashes, and exact visual observations to the durable report. Commit only the five listed paths:

```powershell
git add -- tests/support/Dororong.ContinuousOptics.ps1 `
  tools/New-ContinuousOutlineAuthority.ps1 `
  tests/Dororong.App.ContinuousAuthority.Tests.ps1 `
  tests/fixtures/dororong-body-outline-authority.psd1
git add -f -- artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md
git commit -m "test: freeze continuous Dororong outline authority"
```

Task 2 is blocked until a fresh reviewer approves line semantics, source-only overlays, fixture independence, and mutation sensitivity.

---

### Task 2: Implement subpixel geometry and prove one feasible width without runtime assets

**Files:**
- Create: `tools/Dororong.SourceRaster.psm1`
- Create: `tools/Dororong.SubpixelOutline.psm1`
- Create: `tools/Dororong.SubpixelOutline.Constants.psd1`
- Create: `tests/Dororong.App.SubpixelOutline.Tests.ps1`
- Modify: `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`

**Interfaces:**
- Consumes: Task 1 reviewed continuous authority, pinned source/mask, production endpoints `(118,151)` and `(161,116)`, and production hair samples `(20,125)`, `(104,131)`, `(24,95)`, `(109,64)`, `(54,62)`, `(24,143)`.
- Produces: `Remove-DororongBoundaryBackground(Bitmap) -> Bitmap`; `Resize-DororongPremultiplied96(Bitmap) -> Bitmap`; `Import-DororongBodyMask(string) -> Bitmap`; `New-DororongVisibleContour(Bitmap,Bitmap,PointF[]) -> object`; `New-DororongFillField(Bitmap,Bitmap,object) -> Color[,]`; `New-DororongSubpixelDistanceMap(Bitmap,object,int,double) -> object`; `Get-DororongOutlineCoverage(object,int,int,double) -> double`; `Invoke-DororongSubpixelOutline(Bitmap,Bitmap,object,Color[,],object,Color,double) -> Bitmap`; and one frozen literal width in the production constants PSD1.

- [ ] **Step 1: Write synthetic and real-source failing tests first**

Create `tests/Dororong.App.SubpixelOutline.Tests.ps1`. Before modules exist, tests require the two module paths and constants file, import Task 1's test helper/fixture, and define a separate brute-force reference function for point-to-segment distance and `8x8` coverage. Include:

- a `9x9` opaque rectangle with transparent exterior to verify exposed half-pixel segments and horizontal/vertical coverage;
- a diagonal stair-step mask to verify area coverage against the independent 64-sample reference;
- an opaque protected contact that emits no contour, plus one legal continuation that ends at its exact writable endpoint;
- fill seeds that verify the eligibility rule, eight-nearest ordering, `1/(1+d^2)` interpolation, exact eligible-pixel preservation, and ties-to-even rounding;
- invalid mask, invalid endpoint, fewer-than-eight-seed, changed subpixel-factor, and changed erase-depth failures;
- full source/mask constants and a generator/module text scan proving production code does not import the test PSD1 or helper.

- [ ] **Step 2: Run causal RED**

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
```

Expected: exit `1` with `Dororong.SourceRaster.psm1 is missing.` Record that exact failure before production files exist.

- [ ] **Step 3: Implement deterministic raster and contour modules**

`Dororong.SourceRaster.psm1` moves the existing source-boundary near-white background removal and premultiplied 225-to-96 resize into shared functions without changing their algorithms. Background removal returns a 225x225 32bpp ARGB clone, keeps interior near-white pixels, and zeros RGB where alpha becomes zero.

`Dororong.SubpixelOutline.psm1` validates the exact mask; emits exposed half-pixel segments only at writable-to-transparent transitions; selects the nearest exposed vertex for each legal endpoint with ordinal `Y,X` ties; emits exactly two legal continuation segments; builds the exact eligible fill set and eight-neighbor fill field; and precomputes, for every writable pixel within erase depth, the sorted 64 Euclidean distances from its sample centers to the closest approved segment. `Get-DororongOutlineCoverage` returns the count of stored distances `<= Width` divided by `64.0`. `Invoke-DororongSubpixelOutline` restores the fill field inside the erase band, blends the one outline color by that coverage, preserves source alpha, and leaves mask-zero/far-interior RGB unchanged.

The constants PSD1 initially contains all fixed production values except `Width`:

```powershell
@{
    SubpixelFactor = 8
    EraseDepth = 8.0
    FillFloor = 225
    MaximumChroma = 8
    FillNeighborCount = 8
    WidthSweepMinimum = 0.25
    WidthSweepMaximum = 4.00
    WidthSweepStep = 0.015625
    LegalEndpoints = @(@{ X = 118; Y = 151 }, @{ X = 161; Y = 116 })
    OutlineSamples = @(@(20,125), @(104,131), @(24,95), @(109,64), @(54,62), @(24,143))
}
```

Production code imports this PSD1 but never imports test authority.

- [ ] **Step 4: Turn synthetic tests GREEN before real feasibility**

Run the focused test. Require every synthetic geometry/fill/reference comparison to pass and print exact segment count, eligible-seed count, and subpixel-factor diagnostics. Create one mutation at a time in a unique temporary copy: factor `8 -> 4`, endpoint `118,151 -> 119,151`, fill floor `225 -> 224`, erase depth `8.0 -> 7.0`, and one mask-boundary bit. Each must reach and fail its named semantic assertion; syntax/source-hash/process failures do not count.

- [ ] **Step 5: Execute the source-only feasibility gate in memory**

Using the pinned source, shared background removal, approved mask, exact contour/fill/distance map, production outline-color samples, Task 1 continuous normals, and test-local continuous measurement, evaluate all 241 widths from `0.25` through `4.00` inclusive. Construct each 225px candidate only in memory, measure every source body normal, dispose it, and never save/view a candidate in this task.

For each width, require every body measurement inside `[0.9 * sourceHairMedian, 1.1 * sourceHairMedian]`. The intersection must be non-empty. Select the passing width that minimizes the maximum absolute relative error across all body normals; if scores tie within `1e-12`, choose the smaller width. Write the selected numeric literal as `Width` in the constants PSD1, rerun the entire sweep, and assert the literal equals the deterministic selection.

If the intersection is empty, stop without product asset changes or commit and report all per-normal intervals. Do not move normals, edit the mask, change constants/tolerances, or add local corrections after seeing the sweep.

- [ ] **Step 6: Record source-only evidence and commit feasible geometry**

The report records module interfaces, source/mask/constants hashes, segment/seed counts, legal endpoint readback, source hair median, all per-normal passing intervals and chosen-width measurements, the full intersection, deterministic selection score, synthetic GREEN, and five causal mutations. State explicitly that no runtime/source candidate PNG was written or visually approved.

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
```

Expected: all exit `0`. Commit only the five listed paths:

```powershell
git add -- tools/Dororong.SourceRaster.psm1 tools/Dororong.SubpixelOutline.psm1 `
  tools/Dororong.SubpixelOutline.Constants.psd1 `
  tests/Dororong.App.SubpixelOutline.Tests.ps1 `
  artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md
git commit -m "feat: prove Dororong subpixel outline width"
```

Task 3 is blocked until a fresh reviewer approves production/test independence, synthetic geometry, fill provenance, mutation evidence, and the non-empty feasibility intersection.

---

### Task 3: Integrate the approved width and regenerate exact runtime art

**Files:**
- Modify: `tools/Generate-CanonicalArt.ps1`
- Modify: `tests/Dororong.App.ExactArt.Tests.ps1`
- Modify: `src/Dororong.App/Assets/dororong-canonical.png`
- Modify: `src/Dororong.App/Assets/dororong-closed-eyes.png`
- Modify: `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`

**Interfaces:**
- Consumes: independently approved Task 2 raster/outline modules and frozen constants/width.
- Produces: generator parameters `-SourcePath`, `-BodyMaskPath`, `-OutputDirectory`, and optional `-EvidenceDirectory`; six exact source/native evidence frames; committed 96px open/closed assets; exact-art optical and invariant evidence.

- [ ] **Step 1: Replace the old exact-art contract before generator edits**

Keep source/mask hashes, native dimensions/pixel format, alpha-zero RGB hygiene, presenter dimensions/DPI, native alpha hit testing, state-frame mapping, eye-region semantics, mouth preservation, and open/closed alpha equality. Remove the 58-coordinate hash/probe, native protection-band, no-darkening, partial-alpha immutability, `-BaselineOutputDirectory`, and subtractive PASS assertions.

Add assertions that generator text contains none of `New-NativeBodyLightenRun`, `nativeBodyLightenRuns`, `Apply-SubtractiveNativeBodyCorrection`, or the rejected `clamp(width + 0.5 - distance)` mechanism. Invoke the real generator with the approved mask/evidence directory and load:

```text
source-open-baseline.png
source-open-candidate.png
source-closed-candidate.png
native-open-baseline.png
native-open-candidate.png
native-closed-candidate.png
```

Assert source alpha equality at every coordinate, full mask-zero source pixel equality, mask confinement, exact contour/endpoints/constants diagnostics, identical open/closed body RGB outside eye regions, committed/generated hash equality, and test-helper/fixture independence. Measure source/native hair and body normals only through `tests/support/Dororong.ContinuousOptics.ps1`. Apply unchanged 10%, `0.35`, and `0.50` limits.

- [ ] **Step 2: Capture causal RED against the legacy generator**

Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: build exit `0`; exact-art exit `1` on the first superseded symbol, `Generator still contains superseded symbol 'New-NativeBodyLightenRun'.`

- [ ] **Step 3: Replace the generator pipeline without changing approved modules/constants**

Import both Task 2 modules relative to `$PSScriptRoot`, require `-BodyMaskPath`, make `-EvidenceDirectory` optional, and remove the complete old native correction array/function. After shared boundary-background removal, clone `source-open-baseline.png`, invoke the approved subpixel outline exactly once with the frozen constants/width, derive the closed source frame from that reconstructed open frame using the existing reviewed eye stencils, and resize open/closed once through `Resize-DororongPremultiplied96`.

When evidence is requested, save the six exact names. Save only native candidates to `-OutputDirectory`. Print one stable diagnostic containing source hash, mask hash, constants hash, factor, width, outline RGB, contour-segment count, fill-seed count, and legal endpoints.

- [ ] **Step 4: Prove exact-art mutation sensitivity**

In unique temporary copies, make one mutation at a time and require the real focused contract to reach its named failure:

1. constants width plus `1/64` -> width/optical failure;
2. factor `8 -> 4` -> factor/hash/coverage failure;
3. fill floor `225 -> 224` -> fill-rule/hash/provenance failure;
4. one mask-boundary bit -> mask hash/boundary failure;
5. one protected source-candidate RGB value -> protected RGB failure;
6. one source-candidate alpha value -> alpha failure;
7. one closed-frame body RGB value -> open/closed-body failure.

Generator invocation must succeed for width/factor/fill mutations before the semantic assertion fails. A syntax, pinned-source, or process failure is not accepted mutation evidence.

- [ ] **Step 5: Regenerate committed assets and run focused GREEN**

Run:

```powershell
pwsh -NoProfile -File tools/Generate-CanonicalArt.ps1 `
  -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png `
  -BodyMaskPath src/Dororong.App/Assets/dororong-body-region-mask.png `
  -OutputDirectory src/Dororong.App/Assets `
  -EvidenceDirectory artifacts/work-reports/dororong-continuous-outline-evidence
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: all exit `0`; source/mask/constants remain pinned; exact-art output names source/native medians, each body measurement, min/max/spread, and both PASS boundaries.

- [ ] **Step 6: Inspect every exact visual surface**

Inspect the pinned source first, then source baseline/candidate/open/closed, native open/closed, and nearest-neighbor 4x versions on white and RGB `(18,20,28)`. Cover the full frame and each named front/foot/valley/underside/center/rear/rim/occlusion segment. Record falsifiable observations for:

- original no-tail, three-leg/two-valley silhouette and unchanged endpoints;
- no protruding/new branch, missing span, broken valley, flattened foot, opaque light fringe, or protected-part change;
- body weight matching straight/diagonal/curve hair references without a visibly heavier/lighter segment;
- identical open/closed body and correct closed eyes/mouth.

A width-only review finding permits one fix round changing only the one global width, and the new width must remain inside Task 2's frozen feasibility intersection. A mask/authority/fill/contour defect returns to its producing task and repeats that task's independent review. No local patch is allowed.

- [ ] **Step 7: Record and commit exact implementation**

Append generator/module/constants interfaces, all hashes, width, optical measurements, seven mutation results, RED/GREEN output, exact visual surfaces/observations, and actual-Windows UNVERIFIED status to the durable report.

```powershell
git add -- tools/Generate-CanonicalArt.ps1 tests/Dororong.App.ExactArt.Tests.ps1 `
  src/Dororong.App/Assets/dororong-canonical.png `
  src/Dororong.App/Assets/dororong-closed-eyes.png `
  artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md
git commit -m "fix: render Dororong outline in subpixel space"
```

Task 4 is blocked until a fresh reviewer approves spec compliance, code/test independence, mutation evidence, and all exact visual surfaces.

---

### Task 4: Verify, document, publish, and run Windows attempt 7

**Files:**
- Modify: `README.md`
- Modify: `TASKS.md`
- Modify: `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`
- Create during user observation only: `docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-7.md`

**Interfaces:**
- Consumes: independently approved Task 3 commit, exact source/mask/constants/open/closed hashes, optical evidence, and exact visual verdict.
- Produces: clean full-suite/build/publish evidence; publish identity; accurate handoff; one-item-at-a-time actual Windows acceptance with exact PID cleanup.

- [ ] **Step 1: Update durable handoff truth without promoting Windows status**

README describes the reviewed body mask, continuous half-pixel contour, `8x8` inward coverage, continuous source-only fill field, one hair-derived width/color, one final resize, current 96-DPI limit, and run/publish commands. TASKS records attempts 5 and 6 as FAIL, the rejected pixel-center mechanism as BLOCKED/RESTORED, Tasks 1–3 evidence exactly, and attempt 7 as UNVERIFIED. WALK/click motion remains deferred.

- [ ] **Step 2: Run fresh full verification and publish**

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.Smoke.Tests.ps1 -Configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release `
  --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

Every command must exit `0`. Record test count/failures/skips, build warnings/errors, all focused PASS lines, and hashes for publish EXE/App DLL/Core DLL. Prove the published App DLL embeds the exact current open/closed PNG bytes and ProductVersion binds to the reviewed Task 3 source commit.

- [ ] **Step 3: Append verification evidence and commit documentation**

Record reviewed Task 3 HEAD, documentation HEAD boundary, worktree, Windows version, system DPI, source/mask/constants/open/closed/publish hashes, full command output summary, independent review verdicts, exact visual surfaces, and actual-Windows UNVERIFIED state.

```powershell
git add -- README.md TASKS.md artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md
git commit -m "docs: hand off continuous Dororong outline"
```

Dispatch one broad whole-branch reviewer over the new-plan base through this documentation HEAD. Fix Critical/Important findings through one reviewed fix wave before launch. Repository review cannot promote live acceptance.

- [ ] **Step 4: Launch the exact publish once from a zero exact-path baseline**

Resolve `artifacts/publish/win-x64/Dororong.App.exe`; enumerate only `Win32_Process` entries whose resolved `ExecutablePath` equals it. Do not launch if an ambiguous exact-path survivor exists. Launch once and record PID, resolved path, command line, creation/start time, responding state, documentation HEAD, embedded reviewed-source commit, and executable/source/mask/constants/open/closed hashes. Liveness proves identity only.

- [ ] **Step 5: Ask only the live open-body question first**

Ask the user whether the live Dororong preserves the exact no-tail, three-leg/two-valley silhouette without protrusion and whether the front, feet, valleys, undersides, and rear rim have the same apparent weight as the hair outline. Accept only the user's direct observation and bind any screenshot path/hash. Record PASS, FAIL, or UNVERIFIED; do not infer it from repository evidence.

Only after body PASS, ask the closed-eye integration question: eyes close without leaving the face, the body does not change, and the mouth remains correct. Stop the exact recorded PID after observation or on every abnormal exit, then read back that no exact-path survivor remains. Do not relaunch automatically after a crash or user stop.

- [ ] **Step 6: Update attempt 7 from direct evidence only**

Create the attempt-7 record only when observation begins. Bind exact artifact/PID/hash identity, the user's words, each observed item, and cleanup readback. If either live check is not observed, leave it UNVERIFIED. If body FAILS, do not ask the closed-eye question and do not mark the milestone complete.

---

## Plan completion boundary

The continuous outline work is implementation-complete only after Tasks 1–3 pass their independent task reviews and Task 4's whole-branch review, full suite, build, publish, and artifact-identity checks pass. Milestone 1 remains incomplete until the user directly accepts the live open body and then the closed-eye integration on the current 96-DPI Windows environment; every other unobserved M1 behavior remains recorded as UNVERIFIED rather than inferred.
