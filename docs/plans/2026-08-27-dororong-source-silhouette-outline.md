# Dororong Source-Silhouette Inward Outline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace both rejected native-96 body fixes with a deterministic source-scale reconstruction that preserves Dororong's exact silhouette while rendering one hair-matched body-outline width entirely inward.

**Architecture:** Freeze a reviewed binary 225px body-region mask and source-only optical authority before producing a candidate. A focused PowerShell module removes the old variable-width body ink inside that mask, restores it from fixed local body-fill samples, and rasterizes a single-width, source-colored band from the authoritative transparent boundary inward; the existing generator then derives the eye state and performs one premultiplied whole-frame resize to native 96px. Independent tests own separate hair/body normals and prove mask identity, alpha/protected-pixel preservation, width uniformity, mutation sensitivity, open/closed equality, and presenter behavior; exact-file and actual-Windows observation remain separate gates.

**Tech Stack:** PowerShell 7, `System.Drawing`, C# / .NET 8, WPF, xUnit solution tests, Git.

**Spec:** `docs/specs/2026-08-26-dororong-m1-design.md`

## Global Constraints

- Work only in `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1` on `feature/dororong-m1`.
- Preserve `src/Dororong.App/Assets/dororong-canonical-source.png` byte-for-byte at 225x225 and SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- Dororong has no tail; the white shapes behind the rose and bow are ribbons. Do not change the head, hair, face, mouth, eyes, rose, bow, ribbons, part connectivity, or layer order.
- Create `src/Dororong.App/Assets/dororong-body-region-mask.png` as a 225x225 binary production input: every RGB channel is `0` or `255`, alpha is `255`, and intermediate mask values are forbidden.
- Preserve the source-derived alpha value at every source-scale coordinate. RGB may change only where the reviewed body mask is `255`; protected pixels outside it remain byte-identical before the final resize.
- Remove and do not reuse `New-NativeBodyLightenRun`, `$nativeBodyLightenRuns`, `Apply-SubtractiveNativeBodyCorrection`, the attempt-5 Bezier/path mechanism, per-coordinate clear patches, or any post-resize body correction.
- The reconstructed body outline uses one global source-scale width and one source-derived outline-color rule. No segment-specific widths, hand-tuned 96px pixels, local corrective strokes, or candidate-derived acceptance targets are permitted.
- Keep `dororong-canonical.png` and `dororong-closed-eyes.png` as deterministic 96x96 32bpp ARGB resources presented at 96x96 DIPs on the current 96-DPI target.
- Preserve manual attempts 5 and 6 as exact FAIL evidence. Subagents must not edit or stage any root-owned untracked `docs/verification/*manual-attempt*.md` file.
- Repository tests, asset inspection, assembly identity, and process liveness cannot upgrade actual-Windows acceptance. The user observes one live item at a time.

---

### Task 1: Freeze the source-derived body mask and independent optical authority

**Files:**
- Create: `tools/New-BodyRegionMask.ps1`
- Create: `src/Dororong.App/Assets/dororong-body-region-mask.png`
- Create: `tests/Dororong.App.BodyMask.Tests.ps1`
- Create: `tests/fixtures/dororong-body-outline-authority.psd1`
- Create: `artifacts/work-reports/dororong-source-silhouette-outline-task-report.md`

**Interfaces:**
- Consumes: the pinned 225x225 canonical source and the boundary-connected near-white background rule (`RGB >= 225`).
- Produces: `New-BodyRegionMask.ps1 -SourcePath [string] -OutputPath [string] -EvidenceDirectory [string]`; the exact binary mask; a test-only PSD1 with `HairAnchors`, `BodyNormals`, `ProtectedPoints`, and `FillSamples`; source-first mask overlays and mask hash for Task 2.

- [ ] **Step 1: Write the mask/authority test before creating either authority artifact**

Create `tests/Dororong.App.BodyMask.Tests.ps1` with local `Assert-Equal` / `Assert-True` helpers. It must pin the source hash, require the fixture and mask paths, import the PSD1, and fail before reading pixels when either file is missing:

```powershell
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$authorityPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash 'Canonical source changed.'
Assert-True (Test-Path -LiteralPath $maskPath) 'Reviewed body-region mask is missing.'
Assert-True (Test-Path -LiteralPath $authorityPath) 'Independent body-outline authority is missing.'
```

After loading the artifacts, assert mask size `225x225`, 32bpp ARGB, alpha `255`, and `R=G=B` with channel value only `0` or `255`. Assert one connected writable component; all literal `FillSamples` are writable opaque near-white source pixels; every literal `ProtectedPoint` is mask `0`; at least six `HairAnchors` cover `Straight`, `Diagonal`, and `Curve`; and `BodyNormals` name every front outer/inner/foot, first valley/underside, center outer/foot/inner, second valley/underside, rear outer/foot/inner, and upper/lower rear-rim segment.

- [ ] **Step 2: Run the new test and capture genuine RED**

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
```

Expected: exit `1` with `Reviewed body-region mask is missing.` The source hash check must pass first. Record the exact command, exit code, and first causal failure in the task report.

- [ ] **Step 3: Author a deterministic mask builder from the source alone**

`tools/New-BodyRegionMask.ps1` accepts the three interface parameters, validates the pinned source hash and 225x225 dimensions, flood-fills the internal near-white body component from the fixed source seed `(160,114)`, asserts that it is the largest non-background near-white component, and emits a diagnostic contour-capture overlay before any runtime candidate exists. Expand from that interior only far enough to capture the existing source body ink and exposed antialias coverage; encode the final reviewed writable selection as literal source-row runs in the script so regeneration is deterministic. The final writer uses this shape:

```powershell
function ConvertFrom-MaskRun([string]$Run)
{
    if ($Run -notmatch '^(?<Y>\d+):(?<StartX>\d+)-(?<EndX>\d+)$')
    { throw "Invalid body-mask run '$Run'." }
    return @{ Y=[int]$Matches.Y; StartX=[int]$Matches.StartX; EndX=[int]$Matches.EndX }
}

$mask = [Drawing.Bitmap]::new(225,225,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y=0; $y -lt 225; $y++)
{
    for ($x=0; $x -lt 225; $x++)
    { $mask.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,0,0,0)) }
}
foreach ($encodedRun in $bodyMaskRuns)
{
    $run = ConvertFrom-MaskRun $encodedRun
    for ($x=$run.StartX; $x -le $run.EndX; $x++)
    { $mask.SetPixel($x,$run.Y,[Drawing.Color]::FromArgb(255,255,255,255)) }
}
```

The evidence directory contains the source alone, a red 45%-opacity mask overlay on the source, and a mask-only image. Do not create or view a reconstructed body candidate in this task.

- [ ] **Step 4: Freeze independent source-only fixtures before candidate work**

Create `tests/fixtures/dororong-body-outline-authority.psd1` as literal data only. Select at least six clean hair normals from the source, with at least two each classified as straight, diagonal, and curved and none crossing a junction or occlusion. Select independent body normals for every named segment, at least two protected points for each of head/hair/face/mouth/eyes/rose/bow/ribbons plus both no-tail rear checks and both legal occlusion endpoints, and opaque near-white fill samples covering front, center, rear, and rim regions.

Use these exact field names so Task 2 can consume the fixture without renaming them. Each `HairAnchors` entry has string `Name`, enum-like `Kind` (`Straight`, `Diagonal`, or `Curve`), literal `SourceSamples` and `NativeSamples` point arrays, and literal `Fill` and `Ink` points. Each `BodyNormals` entry has string `Name`, literal source/native point arrays, and a literal fill point. Each `ProtectedPoints` entry has `Name`, `X`, and `Y`. Each `FillSamples` entry has `Name`, `X`, `Y`, and inclusive `Region` bounds ordered as minimum X, minimum Y, maximum X, maximum Y. Every committed coordinate must be an inspected real source/native coordinate; the generator must not import this test fixture.

- [ ] **Step 5: Generate the mask, pin its exact hash, and turn the test GREEN**

Run:

```powershell
pwsh -NoProfile -File tools/New-BodyRegionMask.ps1 `
  -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png `
  -OutputPath src/Dororong.App/Assets/dororong-body-region-mask.png `
  -EvidenceDirectory artifacts/work-reports/dororong-body-mask-authority
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
```

The first run prints the mask SHA-256 and writable count. Add a direct expected-hash assertion to the test in the same commit, rerun, and require exit `0` with a concise `BODY MASK PASS` line containing the same hash/count. Inspect the exact mask overlay at source scale and nearest-neighbor enlargement. Reject any mask pixel on a protected part, transparent exterior, or white ribbon; reject any missing exposed body-ink span or shifted legal endpoint.

- [ ] **Step 6: Record source-only evidence and commit the authority task**

Record source/mask hashes, writable count/bounds, seed, fixture counts/categories, overlay paths, inspection observations, commands, and RED/GREEN output in `artifacts/work-reports/dororong-source-silhouette-outline-task-report.md`. State explicitly that no reconstructed candidate was produced.

```powershell
git add -- tools/New-BodyRegionMask.ps1 `
  src/Dororong.App/Assets/dororong-body-region-mask.png `
  tests/Dororong.App.BodyMask.Tests.ps1 `
  tests/fixtures/dororong-body-outline-authority.psd1
git add -f -- artifacts/work-reports/dororong-source-silhouette-outline-task-report.md
git commit -m "feat: freeze Dororong body outline authority"
```

Expected: only the five listed paths are committed; manual-attempt files remain unstaged. Task 2 does not start until a fresh reviewer approves the mask overlay, fixture independence, and protected-part coverage.

---

### Task 2: Reconstruct one inward body-outline width at source scale

**Files:**
- Create: `tools/Dororong.BodyOutline.psm1`
- Modify: `tools/Generate-CanonicalArt.ps1:1-408`
- Modify: `tests/Dororong.App.ExactArt.Tests.ps1:29-383`
- Modify: `src/Dororong.App/Assets/dororong-canonical.png`
- Modify: `src/Dororong.App/Assets/dororong-closed-eyes.png`
- Modify: `artifacts/work-reports/dororong-source-silhouette-outline-task-report.md`

**Interfaces:**
- Consumes: Task 1 mask/hash, source-only fixture, and approved mask review.
- Produces: `Import-DororongBodyMask([string]) -> Bitmap`; `New-DororongBoundaryDistanceField(Bitmap frame, Bitmap mask) -> double[,]`; `Invoke-DororongInwardOutline(Bitmap frame, Bitmap mask, hashtable[] fillSamples, Point[] outlineSamples, double width) -> Bitmap`; generator parameters `-BodyMaskPath` and optional `-EvidenceDirectory`; exact open/closed 225px evidence frames and committed 96px runtime assets.

- [ ] **Step 1: Replace the subtractive assertions with a failing reconstruction contract**

Keep source hash, native dimensions, alpha-zero hygiene, eye semantics, presenter sizing, hit testing, and state mapping. Remove `Test-InBodyProtectionBand`, the 58-coordinate/hash/probe assertions, partial-alpha RGB immutability, channel-wise no-darkening, and the old subtractive PASS text.

Before changing production code, add assertions that the generator text contains none of `New-NativeBodyLightenRun`, `nativeBodyLightenRuns`, or `Apply-SubtractiveNativeBodyCorrection`; invoke it with Task 1's mask and evidence directory; load `source-open-baseline.png`, `source-open-candidate.png`, `source-closed-candidate.png`, and the native frames; then assert:

```powershell
Assert-Equal $baselineSource.GetPixel($x,$y).A $candidateSource.GetPixel($x,$y).A `
    "Source alpha changed at ($x,$y)."
if ($mask.GetPixel($x,$y).R -eq 0)
{
    Assert-Equal $baselineSource.GetPixel($x,$y).ToArgb() $candidateSource.GetPixel($x,$y).ToArgb() `
        "Protected source pixel changed at ($x,$y)."
}
Assert-Equal $openSource.GetPixel($x,$y).ToArgb() $closedSource.GetPixel($x,$y).ToArgb() `
    "Open/closed body reconstruction differs at ($x,$y)."
```

Limit the last assertion to mask pixels outside the eye regions. Implement test-local luminance/coverage helpers from the spec formula; read only the independent PSD1; assert every 225px body normal is within 10% of the frozen source hair median, every native body normal is within `0.35` equivalent opaque pixel of the native hair median, and native body max-minus-min is at most `0.50`.

- [ ] **Step 2: Run the exact-art test and capture causal RED**

Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: build exits `0`; exact-art exits `1` because the old generator still contains `New-NativeBodyLightenRun` (or the first earlier scan-order superseded symbol). Record the exact failure and do not weaken the test.

- [ ] **Step 3: Add the focused body-outline module**

Create `tools/Dororong.BodyOutline.psm1`. `Import-DororongBodyMask` validates dimensions, ARGB format, alpha `255`, binary channels, and at least one writable pixel. `New-DororongBoundaryDistanceField` treats a writable opaque frame pixel with an eight-neighbor alpha-zero pixel as a source-authoritative exposed boundary seed; for every writable pixel it stores the Euclidean distance to the nearest seed and uses ordinal `Y,X` tie-breaking. It throws when no boundary seed exists.

`Invoke-DororongInwardOutline` clones the input, validates every fill/outline sample is opaque and inside its allowed source region, restores only mask pixels within the fixed erase depth of an exposed boundary from the nearest eligible fill sample, and composites the single outline color using:

```powershell
$coverage = [Math]::Clamp($Width + 0.5 - $distance[$x,$y], 0.0, 1.0)
$red   = [Math]::Round(($fill.R * (1.0-$coverage)) + ($outline.R * $coverage))
$green = [Math]::Round(($fill.G * (1.0-$coverage)) + ($outline.G * $coverage))
$blue  = [Math]::Round(($fill.B * (1.0-$coverage)) + ($outline.B * $coverage))
$result.SetPixel($x,$y,[Drawing.Color]::FromArgb($original.A,$red,$green,$blue))
```

The outline color is the component-wise median of production-owned clean hair samples. The module never reads the test PSD1 and never changes alpha.

- [ ] **Step 4: Replace the generator pipeline at 225px**

Replace `-BaselineOutputDirectory` with mandatory `-BodyMaskPath` and optional `-EvidenceDirectory` parameters. Import the module relative to `$PSScriptRoot`. Delete the complete native-96 run array and subtractive function.

Immediately after boundary-background transparency, clone the uncorrected 225px baseline, load the mask, and call `Invoke-DororongInwardOutline` once on the open production frame. Keep production-owned fill regions/samples and outline-color samples as literal source coordinates distinct from the test fixture. Determine one global width by matching the preselected production hair target; print the diagnostic value, replace it with one literal `$sourceBodyOutlineWidth` in the same commit, and do not expose a per-segment override.

Derive the closed-eye frame from the reconstructed open frame so body pixels are identical. When `-EvidenceDirectory` is present, save uncommitted evidence files with exact names:

```text
source-open-baseline.png
source-open-candidate.png
source-closed-candidate.png
native-open-baseline.png
native-open-candidate.png
native-closed-candidate.png
```

Resize the reconstructed 225px open/closed frames once through the existing premultiplied path and save only the native candidates to the requested output directory.

- [ ] **Step 5: Prove the test detects causal mutations before accepting GREEN**

In a unique temporary directory, copy the module/generator and exact generated evidence without touching the checkout. Make one mutation at a time: add `0.75` to the copied generator's literal width; flip one copied mask-boundary pixel; alter one protected RGB pixel in a copied source-scale candidate; alter one alpha value in another copied candidate; and alter one closed-frame body pixel. Invoke the generator only for the width/mask mutations and invoke the same test-local invariant/coverage functions on every mutated output. Require each mutation to produce its named width, mask-hash, protected-RGB, alpha, or open/closed-body failure. Record commands and first causal messages; a source-hash, syntax, or process failure is not mutation evidence.

- [ ] **Step 6: Regenerate exact assets and run focused GREEN**

```powershell
pwsh -NoProfile -File tools/Generate-CanonicalArt.ps1 `
  -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png `
  -BodyMaskPath src/Dororong.App/Assets/dororong-body-region-mask.png `
  -OutputDirectory src/Dororong.App/Assets `
  -EvidenceDirectory artifacts/work-reports/dororong-source-outline-evidence
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: all exit `0`; source hash/mask hash are unchanged; the exact-art output names the frozen width, source/native hair medians, body min/max/spread, and source/native PASS boundaries.

- [ ] **Step 7: Inspect the exact candidate on every required surface**

Inspect the source alone first, then source-scale baseline/candidate, native open/closed, and nearest-neighbor enlargements on white and RGB `(18,20,28)`. Cover the full frame and every named body segment/occlusion endpoint. Reject any tail, displaced silhouette, protruding/new branch, missing span, light fringe, flattened/broken leg valley, protected-part difference, closed-eye body difference, or body segment visibly heavier/lighter than the frozen hair anchors.

If a fresh reviewer reports a concrete width-only mismatch, allow one fix round changing only the single global width. If it reports a mask/protection defect, return to Task 1's mask builder and repeat its source-only review before regenerating. Do not add local patches. If the same mechanism fails again, stop and report the architecture as rejected rather than applying another coordinate correction.

- [ ] **Step 8: Record evidence and commit the implementation**

Append module/generator interfaces, literal production samples, mask/source/asset hashes, width, optical measurements, five mutation results, RED/GREEN output, exact visual observations, and remaining actual-Windows boundary to the task report.

```powershell
git add -- tools/Dororong.BodyOutline.psm1 tools/Generate-CanonicalArt.ps1 `
  tests/Dororong.App.ExactArt.Tests.ps1 `
  src/Dororong.App/Assets/dororong-canonical.png `
  src/Dororong.App/Assets/dororong-closed-eyes.png `
  artifacts/work-reports/dororong-source-silhouette-outline-task-report.md
git commit -m "fix: reconstruct Dororong body outline from source"
```

Expected: only listed paths are committed and manual-attempt records remain unstaged. A fresh reviewer must approve spec compliance, code/test independence, mutation evidence, and exact visuals before Task 3.

---

### Task 3: Update durable handoff and publish the reviewed artifact

**Files:**
- Modify: `README.md:36-42`
- Modify: `TASKS.md:3-13`
- Modify: `artifacts/work-reports/dororong-source-silhouette-outline-task-report.md`

**Interfaces:**
- Consumes: independently approved Task 2 commit, mask/source/asset hashes, frozen width/coverage evidence, and exact visual verdict.
- Produces: truthful handoff state, complete fresh verification output, and a framework-dependent `win-x64` publish bound by exact hashes.

- [ ] **Step 1: Replace obsolete subtractive documentation**

README describes the reviewed 225px binary body mask, source-alpha-preserving inward reconstruction, one hair-derived width/color, one final resize, protected-part boundary, and current 96-DPI limit. TASKS records attempts 5 and 6 as FAIL, the new implementation/review state exactly, and attempt 7 as UNVERIFIED. WALK/click motion remains deferred.

- [ ] **Step 2: Run every fresh automated verification command once**

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release `
  --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

Expected: every command exits `0`; record the exact test count, failures/skips, warnings/errors, mask/optical output, and focused PASS lines. Publish must contain `Dororong.App.exe`, `Dororong.App.dll`, and `Dororong.Core.dll`.

- [ ] **Step 3: Bind artifact identity and limits**

Record reviewed source HEAD, documentation HEAD boundary, worktree, Windows version, system DPI, source/mask/open/closed hashes, publish EXE/App DLL/Core DLL hashes, test/build output, independent review verdict, visual surfaces, and actual-Windows UNVERIFIED state. Confirm the published app DLL embeds the exact current open/closed PNG bytes.

- [ ] **Step 4: Commit documentation only**

```powershell
git add -- README.md TASKS.md artifacts/work-reports/dororong-source-silhouette-outline-task-report.md
git commit -m "docs: record source-derived body verification"
```

Expected: exactly three documentation paths are committed; no source/asset/test/manual-attempt path is staged. A task reviewer checks truthfulness and scope, followed by one broad branch review from this plan's base to documentation HEAD.

---

### Task 4: Run actual-Windows manual attempt 7 one item at a time

**Files:**
- Create without staging during the live exchange: `docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-7.md`

**Interfaces:**
- Consumes: Task 3 exact published executable/hash set and zero exact-path process baseline.
- Produces: direct user PASS/FAIL/UNVERIFIED for live open-eye silhouette/weight first and closed-eye integration only after body PASS.

- [ ] **Step 1: Establish a safe exact-process baseline and launch once**

Resolve `artifacts/publish/win-x64/Dororong.App.exe`; enumerate only `Win32_Process` entries whose resolved `ExecutablePath` equals it. Do not launch if any ambiguous exact-path survivor exists. Launch once, then record PID, path, command line, creation/start time, responding state, documentation HEAD, embedded reviewed-source commit, and executable/source/mask/open/closed hashes. Process liveness proves identity only.

- [ ] **Step 2: Create attempt 7 as UNVERIFIED before asking**

Record exact artifact/target identity, automated evidence, independent visual review, attempts 5/6 FAIL boundary, and manual/automation separation. Keep body and closed-eye checks UNVERIFIED.

- [ ] **Step 3: Ask only the open-eye body question**

Ask whether the live body preserves the original no-tail, three-leg/two-valley silhouette without protrusion and whether the front, feet, valleys, underside, and rear rim all maintain the same apparent weight as the hair outline. Accept only the user's observation; bind any screenshot and SHA-256.

- [ ] **Step 4: Gate the closed-eye question on body PASS**

If body FAILS, preserve attempt 7 as FAIL, identity-check and stop only the recorded PID, and return to diagnosis. If body PASSES, update the record and ask only whether blinking/sleeping closes the eyes without detachment while the approved body remains unchanged.

- [ ] **Step 5: Clean up the task-owned process**

Recheck PID against exact path, command line, and creation time. Stop only the exact matching process, verify zero exact-path survivors, and record cleanup. Never stop by name or relaunch after a failed attempt.
