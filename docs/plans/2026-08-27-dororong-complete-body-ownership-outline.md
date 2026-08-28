# Dororong Complete Body Ownership Outline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Reconstruct every visible body-owned outline pixel and render one silhouette-faithful inward body stroke whose frozen geometric support and calibrated optical coverage are tied to the unchanged head/hair reference.

**Architecture:** Preserve the current reviewed body mask as an immutable seed, expand it by deterministic component ownership, and freeze the reviewed final mask before deriving geometry. Production and independent tests then canonicalize the same exposed contour and two legal continuations; a fresh test-only authority fixes perpendicular body normals before the one completed historical sweep. The all-pixel smooth fill, fixed 8x8 E/C support, controller-selected F optical transfer, deterministic resize-only proxy, runtime frames, and Windows artifact are admitted in that order.

**Tech Stack:** PowerShell 7, .NET System.Drawing, C# / .NET 8 / WPF, executable PowerShell tests without Pester, Git.

**Spec:** docs/specs/2026-08-26-dororong-m1-design.md

**Approved recovery revision:** 2026-08-28

**Plan base:** f09599bf8501fdcb3e2042fb548b48fd7b1df4f1

**Supersedes:** Tasks 2 through 4 of docs/plans/2026-08-27-dororong-continuous-subpixel-outline.md. Its empty sweeps remain failure evidence, not calibration input. Commits cd03851, 2169440, and 645a650 remain historical authority work; this plan reauthors body normals only after the new ownership and contour are frozen.

## Global Constraints

- The canonical 225x225 source remains byte-identical at SHA-256 F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504.
- The current mask is preserved byte-for-byte as tests/fixtures/dororong-body-region-seed.png at SHA-256 E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 before the production mask changes.
- The final mask is a 225x225 opaque 32bpp ARGB binary PNG. Every RGB channel is 0 or 255, all three channels agree, and every final mask-255 coordinate has processed-source alpha exactly 255.
- Ownership uses the alpha-255, seed-zero four-connected graph before any neutral-color validation. All component and contact relationships are four-neighbor relationships.
- The selected expansion is exactly 82 components and 167 pixels. Every selected pixel has channel spread at most 8. The final mask is the cleaned visible seed union those complete components.
- The 18 protected anchors, no-tail exterior, head, hair, face, mouth, eyes, rose, bow, ribbons, and both occlusion sides stay outside body reconstruction. The old endpoint markers (112,151) and (157,116) are removed from authority.
- The only legal continuation endpoints are front hair (118,151) and rear ribbon (161,116).
- Source alpha never changes. Delivered source-225 evidence changes RGB only inside the reviewed final mask; every mask-zero pixel remains byte-identical in that evidence. The sole exception is an unsaved, pre-resize proxy clone defined below; it is not a source-evidence or post-resize edit.
- Eligible fill samples require processed alpha 255, cleaned seed 255, ordinary unweighted distance greater than 8.0 from the final E-union-C contour, all RGB channels at least 225, and channel spread at most 8. They contribute exact source RGB samples, but every final-mask pixel—including eligible-sample coordinates—is assigned the nearest-eight `1/(1+d^2)` smooth interpolation with ordinal Y,X ties and ties-to-even. No retain-seed output exception exists.
- The frozen support and outline color come from unchanged authority: one-sided exposed `E` support uses `W = 2.20898670201159`, centered continuation `C` support uses `W/2 = 1.104493351005795`, and outline RGB is `(26,2,10)`. Segment-specific support widths, moved support, local strokes, per-coordinate patches, post-resize body edits, and candidate-derived targets are forbidden.
- Every final mask-255 pixel evaluates exactly 64 samples at (x-0.5+(i+0.5)/8, y-0.5+(j+0.5)/8), i,j in 0..7. `Ecoverage` is the fraction with actual nearest canonical-E distance at most `W`; `Ccoverage` is the fraction with actual nearest canonical-C distance at most `W/2`. Candidate F uses `finalCoverage = max(clamp(Ecoverage * 2.5, 0, 1), clamp(Ccoverage * 0.125, 0, 1))`; this transfer never expands or moves geometric support.
- Body normal authoring may read only the pinned source, frozen seed/final masks, frozen contour, existing named regions, and reference overlays. It may not read candidate pixels, passing intervals, sweep scores, or the failed Task 2 report.
- The completed 0.25-through-4.00 source feasibility sweep and its definitive empty fifteen-normal 10% intersection remain diagnostic evidence. The source intersection is not an automated release requirement, does not forbid the user-directed recovery, and must not be rerun.
- The completed native `0.35` hair-match and `0.50` body-spread checks contradicted the exact visually selected F candidate. They remain printed diagnostics, not release gates; exact hashes, invariant checks, visual inspection, runtime observation, and user acceptance cannot be silently replaced by those metrics.
- Immediately before the single resize only, an unsaved clone reconstructs every enclosed four-connected mask-zero neutral component that does not touch the frame, has size at most 7, and chroma at most 8 from the same smooth field. Canonical proxy membership sorts pixels within each selected component ordinally by `(Y,X)`, encodes each record as `C|count|Y,X;...`, sorts records by their first `(Y,X)`, joins them with LF and no trailing LF, and hashes the UTF-8 bytes. The pinned input yields exactly 6 proxy components and 12 pixels, membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`, and the 7-pixel gray island at bounds (144,159)-(147,160). Selection is topology/color based and coordinate-independent; the proxy never changes alpha and is not saved or applied after resize.
- Open and closed frames share identical reconstructed body pixels, source alpha, and resize-proxy inputs outside the eye regions. Both pass through one premultiplied high-quality 225-to-96 resize.
- Existing untracked manual acceptance attempts 2 through 6 are user-owned evidence. No task may modify, stage, delete, or rename them.
- The four untracked files from the rejected second sweep are task-owned failed work, not approved evidence. A task may replace their contents only when its brief names the file, and must capture its own RED before relying on the replacement.

---

### Task 1: Freeze complete visible body ownership

**Files:**
- Create: tests/fixtures/dororong-body-region-seed.png
- Create: tools/Dororong.BodyOwnership.Constants.psd1
- Create: tools/Dororong.BodyOwnership.psm1
- Create or replace failed untracked file: tools/Dororong.SourceRaster.psm1
- Modify: tools/New-BodyRegionMask.ps1
- Modify: tests/Dororong.App.BodyMask.Tests.ps1
- Modify: src/Dororong.App/Assets/dororong-body-region-mask.png

**Interfaces:**
- Consumes: pinned source PNG, immutable predecessor seed, and the 18 literal protected anchors from the specification.
- Produces: Remove-DororongBoundaryBackground(Bitmap) -> Bitmap; Resize-DororongPremultiplied96(Bitmap) -> Bitmap; Import-DororongBinaryMask(string) -> Bitmap; Get-DororongBodyOwnership(Bitmap,Bitmap,hashtable) -> object with Mask, SelectedComponents, MembershipRecords, MembershipHash, ProtectedComponents; final reviewed body mask and ownership overlays.

- [ ] **Step 1: Preserve the predecessor input before changing production output**

Copy the current production mask bytes to tests/fixtures/dororong-body-region-seed.png:

    Copy-Item -LiteralPath src/Dororong.App/Assets/dororong-body-region-mask.png -Destination tests/fixtures/dororong-body-region-seed.png

Assert both files initially hash to E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 and that the source hashes to F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504.

- [ ] **Step 2: Write the independent ownership RED**

Extend tests/Dororong.App.BodyMask.Tests.ps1 with local implementations that do not import the production ownership module. The test must:

    $expectedAnchors = @(
        @(52,68), @(99,72), @(23,116), @(106,139),
        @(39,132), @(84,145), @(63,142), @(78,140),
        @(52,122), @(92,124), @(137,84), @(143,89),
        @(135,105), @(150,108), @(159,98), @(151,128),
        @(181,127), @(180,163)
    )

Remove boundary-connected near-white background, clear seed coordinate (138,174), build all alpha-255/seed-zero four-connected components, select components that four-touch both cleaned seed and alpha-zero exterior and contain no anchor, and only afterward require every selected pixel spread to be at most 8. Canonically encode component membership as C|count|Y,X;... with pixels sorted Y,X and components sorted by first Y,X.

Require exactly 82 selected components, 167 selected pixels, maximum selected spread 4, one anchored protected component of 11,988 pixels, and final mask equality with the independent union. Before the production mask changes, run:

    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1

Expected: exit 1 because the current production mask omits the selected component membership. Record the first mismatch coordinate and observed final count.

- [ ] **Step 3: Implement production ownership from the immutable seed**

tools/Dororong.SourceRaster.psm1 exports the existing boundary-connected near-white removal as Remove-DororongBoundaryBackground and the existing premultiplied high-quality 225-to-96 resize as Resize-DororongPremultiplied96. Background removal returns a 225x225 32bpp ARGB bitmap with alpha-zero RGB cleared to zero; the resize remains behavior-identical to the current generator implementation.

tools/Dororong.BodyOwnership.Constants.psd1 contains the pinned source/seed hashes, exact 18 anchor records, ExpectedSelectedComponentCount = 82, ExpectedSelectedPixelCount = 167, MaximumSelectedChroma = 8, and InvalidSeedCoordinate = @{ X = 138; Y = 174 }.

tools/Dororong.BodyOwnership.psm1 validates source, seed, dimensions, pixel format, binary channels, alpha conditions, four-neighbor component/contact rules, anchor exclusion, neutral qualification, exact counts, and canonical membership encoding. Get-DororongBodyOwnership returns a new mask; it never overwrites its seed input.

tools/New-BodyRegionMask.ps1 becomes the command wrapper:

    param(
        [Parameter(Mandatory=$true)][string]$SourcePath,
        [Parameter(Mandatory=$true)][string]$SeedPath,
        [Parameter(Mandatory=$true)][string]$OutputPath,
        [Parameter(Mandatory=$true)][string]$EvidenceDirectory
    )

It saves the final binary mask plus source and nearest-neighbor 4x overlays that distinguish cleaned seed, selected ownership, protected anchored component, and transparent exterior. It prints source hash, seed hash, selected count, selected pixels, membership hash, final mask hash, and bounds.

- [ ] **Step 4: Add causal ownership mutations**

In unique temporary directories, prove that each mutation reaches its named assertion:

1. restore seed bit (138,174) -> writable-alpha failure;
2. remove one selected component -> component membership failure;
3. add one pixel from the anchored component -> protected-anchor failure;
4. change four-neighbor contact to diagonal-only -> contact failure;
5. change one anchor coordinate -> protected component or anchor-set failure.

A source-hash, syntax, or missing-file error is not accepted as a mutation result.

- [ ] **Step 5: Generate and inspect the final mask before freezing its hash**

Run:

    pwsh -NoProfile -File tools/New-BodyRegionMask.ps1 -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png -SeedPath tests/fixtures/dororong-body-region-seed.png -OutputPath src/Dororong.App/Assets/dororong-body-region-mask.png -EvidenceDirectory .superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/ownership-evidence
    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1

Inspect source and 4x overlays on white and RGB (18,20,28). Confirm all three legs, both valleys, front underside, rear rim, and visible antialias support are selected; head/hair/face/eyes/mouth/rose/bow/ribbons/no-tail exterior remain protected; the two occlusion cuts do not expose a new branch. Freeze the observed final mask and membership SHA-256 literals only after this inspection passes.

- [ ] **Step 6: Run GREEN and commit**

Run BodyMask tests twice, regenerating the output between runs, and require identical mask/membership hashes and exit 0 both times. Commit only the seven listed paths:

    git add -- tests/fixtures/dororong-body-region-seed.png tools/Dororong.BodyOwnership.Constants.psd1 tools/Dororong.BodyOwnership.psm1 tools/Dororong.SourceRaster.psm1 tools/New-BodyRegionMask.ps1 tests/Dororong.App.BodyMask.Tests.ps1 src/Dororong.App/Assets/dororong-body-region-mask.png
    git commit -m "feat: own the complete Dororong body outline"

Task 2 is blocked until independent review approves ownership semantics, both canonical hashes, all mutations, and every mask overlay.

---

### Task 2: Freeze one canonical production contour

**Files:**
- Create or replace failed untracked file: tools/Dororong.SubpixelOutline.Constants.psd1
- Create or replace failed untracked file: tools/Dororong.SubpixelOutline.psm1
- Create or replace failed untracked file: tests/Dororong.App.SubpixelOutline.Tests.ps1

**Interfaces:**
- Consumes: Task 1 final reviewed mask and immutable seed.
- Produces: Import-DororongBodyMask(string) -> Bitmap; New-DororongVisibleContour(Bitmap,Bitmap,PointF[]) -> object; Get-DororongCanonicalContourRecords(object) -> string[]; Get-DororongCanonicalContourHash(object) -> string.

- [ ] **Step 1: Write independent synthetic and real-contour RED tests**

The test defines literal endpoints:

    $legalEndpoints = @(@{ X=118; Y=151 }, @{ X=161; Y=116 })

Independently derive exposed half-pixel edges only at final-mask-255 to processed-alpha-zero boundaries. Add a 9x9 synthetic rectangle, stair-step boundary, opaque protected contact, raw reversal/permutation case, duplicate case, and both real endpoints. Canonical records use doubled integer coordinates, Y2/X2 endpoint orientation, exact duplicate removal, records kind|startY2|startX2|endY2|endX2, and ordinal five-field sorting.

Before production geometry is replaced, run:

    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -GeometryOnly

Expected: exit 1 on production/independent contour hash mismatch or missing canonical interface.

- [ ] **Step 2: Implement only geometry and constants**

The constants file contains no Width field and exactly:

    @{
        SubpixelFactor = 8
        FillDistance = 8.0
        FillFloor = 225
        MaximumChroma = 8
        FillNeighborCount = 8
        WidthSweepMinimum = 0.25
        WidthSweepMaximum = 4.00
        WidthSweepStep = 0.015625
        LegalEndpoints = @(@{ X=118; Y=151 }, @{ X=161; Y=116 })
        OutlineSamples = @(@(20,125), @(104,131), @(24,95), @(109,64), @(54,62), @(24,143))
    }

The module validates the Task 1 final mask hash. New-DororongVisibleContour emits E segments for writable-to-transparent edges, no hidden edge at writable-to-protected contacts, and exactly two C segments from nearest exposed vertex to front hair and rear ribbon endpoints using ordinal Y,X ties. Each endpoint is alpha 255, mask 255, and on the final mask boundary.

- [ ] **Step 3: Prove production/test contour identity and mutations**

Require identical production and independent canonical records/hash for synthetic and real geometry. Mutations:

1. endpoint 118,151 to 119,151 -> endpoint/hash failure;
2. one final-mask boundary bit -> contour hash failure;
3. protected contact emitted as E -> protected-contact failure;
4. continuation removed -> continuation-count/hash failure;
5. raw reversal and permutation -> same canonical hash and pass;
6. geometry or kind changed -> canonical hash failure.

- [ ] **Step 4: Generate contour-only overlays and commit**

Add a GeometryOnly evidence mode to the focused test or a small command path in the module test harness. Render E segments blue, front C green, rear C orange, endpoints labeled, at source scale and nearest-neighbor 4x. Inspect the full body plus both junctions and confirm there is no hidden protected edge, broken exposed span, wrong endpoint, or new branch.

Run:

    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -GeometryOnly

Expected: exit 0 with final mask hash, component hash, contour hash, exposed count, continuations=2, and endpoint readback. Commit only the three listed paths:

    git add -- tools/Dororong.SubpixelOutline.Constants.psd1 tools/Dororong.SubpixelOutline.psm1 tests/Dororong.App.SubpixelOutline.Tests.ps1
    git commit -m "feat: freeze the Dororong body contour"

Task 3 is blocked until independent review approves the canonical records, production/test identity, mutations, and contour overlays.

---

### Task 3: Reauthor body normals against the final contour

**Files:**
- Modify: tests/fixtures/dororong-body-outline-authority.psd1
- Modify: tests/Dororong.App.ContinuousAuthority.Tests.ps1
- Modify: tools/New-ContinuousOutlineAuthority.ps1

**Interfaces:**
- Consumes: Task 2 frozen contour and the unchanged six clean hair anchors.
- Produces: fifteen literal source/native body normals, new authority hash, and source/native plus 4x overlays. Hair anchor fields and hair medians remain byte-identical.

- [ ] **Step 1: Add final-contour and perpendicularity RED**

The focused test independently reads the final mask and derives the canonical contour without importing production geometry. Remove LegalEndpoint-FrontOcclusion and LegalEndpoint-RearOcclusion from ProtectedPoints; add a separate literal LegalEndpoints field with (118,151) and (161,116).

For each named body normal require:

- one unique crossing strictly inside its named E or C segment;
- no vertex, junction, neighboring segment, or second canonical segment crossing;
- stored direction from final-mask body side to non-body side;
- absolute dot of unit normal and unit tangent at most 0.0871557427476582;
- first and final 1/8-pixel samples on opposite required ownership sides;
- existing single darkness run and light-interval checks.

Run:

    pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1

Expected: exit 1 on the first stale body normal or historical endpoint marker. Record the exact named failure.

- [ ] **Step 2: Reauthor from geometry only**

Use a fresh authoring pass that may read only the pinned source, seed/final masks, canonical contour, current named contour windows, current fixture, and reference overlays. It must not read or run the feasibility sweep, old task report, candidate art, or passing intervals.

For every failing body normal, select literal rational eighth-pixel endpoints that meet the unique-crossing and 5-degree rule. Reversing endpoints to body-to-non-body order is allowed. Source/native fill points remain source-reference selections, never candidate-derived. The six hair anchors, their line endpoints, fill/ink points, and measured medians remain byte-identical.

- [ ] **Step 3: Generate and inspect authority overlays**

tools/New-ContinuousOutlineAuthority.ps1 validates final mask/contour hashes and draws named normals, unique crossing points, tangent ticks, legal continuations, fill points, and protected anchors. Produce source, source-4x, native, and native-4x overlays.

Inspect all fifteen body normals and six hair anchors. Each line must cross only its named visible segment, be visibly near-perpendicular, avoid corners and protected parts, and start/end on opposite sides. Reject an overlay with a shared neighboring crossing even if numeric optical output passes.

- [ ] **Step 4: Add mutation sensitivity and commit**

Require named failures for one endpoint moved to a vertex, one line rotated beyond 5 degrees, one line reversed, one line extended across a neighbor, one old protected endpoint reintroduced, and one hair-anchor byte changed.

Run:

    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -GeometryOnly
    pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1

Expected: all exit 0 and output the unchanged hair medians, fifteen body names, new authority hash, final mask hash, and contour hash. Commit only:

    git add -- tests/fixtures/dororong-body-outline-authority.psd1 tests/Dororong.App.ContinuousAuthority.Tests.ps1 tools/New-ContinuousOutlineAuthority.ps1
    git commit -m "test: bind Dororong normals to final ownership"

Task 4 is blocked until independent review approves geometry-only authoring, overlay relationships, hair immutability, and all mutations.

---

### Task 4: Reconstruct fill and prove one feasible global width

**Files:**
- Modify: tools/Dororong.SubpixelOutline.psm1
- Modify: tools/Dororong.SubpixelOutline.Constants.psd1
- Modify: tests/Dororong.App.SubpixelOutline.Tests.ps1

**Interfaces:**
- Consumes: Task 1 final mask, Task 2 E-union-C contour, and Task 3 frozen normals/hair target.
- Produces: New-DororongFillField(Bitmap,Bitmap,Bitmap,object) -> Color[,]; New-DororongSubpixelDistanceMap(Bitmap,object,int) -> object; Get-DororongOutlineCoverage(object,int,int,double) -> double; Invoke-DororongSubpixelOutline(Bitmap,Bitmap,Color[,],object,Color,double) -> Bitmap; one frozen Width literal.

- [ ] **Step 1: Write fill and raster RED before implementation**

Extend the focused test with:

- a synthetic final mask containing cleaned seed interior, newly owned edge pixels, at least eight valid distant fill seeds, one dark seed candidate, and one protected contact;
- independent eight-nearest ordering, weight 1/(1+d^2), ordinal Y,X ties, and ties-to-even rounding;
- independent 64-sample coverage using coordinates x-0.5+(i+0.5)/8 and minimum distance to E union C;
- assertions that newly owned pixels never seed, any source channel below 225 never seeds, every non-seed final pixel is reconstructed, source alpha is unchanged, and mask-zero RGB is unchanged.

Run:

    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1

Expected: exit 1 on the first missing fill or distance-map interface.

- [ ] **Step 2: Implement fill reconstruction and all-pixel 8x8 coverage**

At the historical Task 4 gate, eligible fill seeds satisfied every then-active Global Constraint and retained exact source RGB in output; non-seed final-mask pixels received interpolation from the nearest eight eligible seeds, with no far-interior preserve-original branch. It built 64 distances for every final mask-255 pixel, not a band subset, and used `count(distance <= Width) / 64.0` coverage. The rasterizer blended one fill field and one outline RGB, preserved source alpha, and left final mask-zero pixels byte-identical. Task 5A supersedes only the retain-seed output exception and optical transfer while preserving this record of the completed gate.

- [ ] **Step 3: Turn synthetic tests GREEN and prove mutations**

Require named failures for:

1. factor 8 to 4;
2. sample origin shifted by +0.5;
3. E-only distance that omits C;
4. newly owned fill seed admitted;
5. fill floor 225 to 224;
6. fill distance 8.0 to 7.0;
7. one protected RGB change;
8. one source alpha change.

Run the focused test with -SyntheticOnly and require exit 0 before any real-source sweep.

- [ ] **Step 4: Run the terminal source-only feasibility gate**

Measure the unchanged six hair anchors first. Evaluate exactly 241 widths in memory from 0.25 through 4.00 inclusive. For every width, reconstruct a 225px candidate only in memory, measure all fifteen frozen body normals, dispose the bitmap, and never save or inspect the candidate.

A width passes only when every body measurement is inside 0.9 to 1.1 times the unchanged source hair median. Select the passing width minimizing maximum absolute relative error; score ties within 1e-12 choose the smaller width. Write that exact numeric Width literal to the constants file, rerun all 241 widths, and assert the literal equals the deterministic selection.

If the intersection is empty, stop this plan without changing runtime assets, mask, normals, endpoints, fill rules, tolerance, or width by segment. Record all intervals and report the approved ownership/geometry design as failed.

- [ ] **Step 5: Run GREEN and commit feasible geometry**

Run:

    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -GeometryOnly
    pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1

Expected: all exit 0. Output final mask/component/contour/authority/constants hashes, eligible seed count, source hair median, all fifteen intervals, non-empty intersection, selected width, score, and measurements. Commit only:

    git add -- tools/Dororong.SubpixelOutline.psm1 tools/Dororong.SubpixelOutline.Constants.psd1 tests/Dororong.App.SubpixelOutline.Tests.ps1
    git commit -m "feat: prove one Dororong body outline width"

Task 5 is blocked until independent review approves production/test independence, fill provenance, 8x8 coordinates, all mutations, and the non-empty intersection.

---

## User-directed recovery after Task 4

Task 4 remains a completed historical gate with a definitive `EMPTY` result: `CenterOuter` admitted `1.203125..1.609375`, while `FrontOuter` and `LowerRearRim` began at `2.0625`. Its implementation history, bound invocation, intervals, and no-asset-change outcome are preserved. The user directly rejected the unchanged live result and explicitly required the body to follow the thin head/hair line. This recovery supersedes only Task 4's terminal prohibition on a runtime candidate; it does not alter Tasks 1–4 or authorize another feasibility sweep.

### Task 4B: Implement one visible width with continuation side compensation

**Files:**
- Modify: tools/Dororong.SubpixelOutline.psm1
- Modify: tools/Dororong.SubpixelOutline.Constants.psd1
- Modify: tests/Dororong.App.SubpixelOutline.Tests.ps1

**Interfaces:**
- Consumes: Tasks 1–3 frozen ownership, canonical `E`/`C` geometry, endpoints, fill authority, `8x8` sampling, hair median/color rule, and Task 4 diagnostic evidence.
- Produces: fixed `Width = 2.20898670201159` as the intended full rendered thickness, `E -> W` one-sided distance coverage, `C -> W/2` centered two-sided distance coverage, and independent synthetic proof of that side-count rule.

- [ ] **Step 1: Write causal side-compensation RED before production edits**

Extend the independent synthetic test with separate exposed and continuation fixtures. Prove that an exposed edge receives inward thickness `W`, while an internal continuation receives the same full visible thickness by covering both sides to radius `W/2`. Capture a causal RED against the current one-radius implementation.

Add independent named mutations that (1) omit `C` from distance coverage, (2) apply `W` on both sides of `C`, and (3) apply `W/2` inward to `E`. Each mutation must fail its own rendered-full-thickness or continuation-coverage assertion rather than a process, input-hash, or unrelated geometry check.

- [ ] **Step 2: Implement fixed optical target and side-count coverage**

Add the literal `Width = 2.20898670201159` to the reviewed constants. Keep the component-wise median outline-color rule; the representative attempt-2 readback is RGB `(26,2,10)`. Update distance coverage so canonical kind `E` uses inward radius `Width` and kind `C` uses centered radius `Width / 2`. Do not change the final mask, canonical segment records or kinds, endpoint pair, fill field, sample factor or locations, alpha/protected-pixel behavior, source identity, or resize boundary. Do not add segment overrides, local strokes, or post-resize corrections.

- [ ] **Step 3: Bind the implementation to frozen candidate evidence**

For the historical Task 4B gate, require deterministic source-open and native-open candidate checks to reproduce the then-approved attempt-2 representative evidence before any runtime write:

- full rendered thickness `2.20898670201159`;
- median outline RGB `(26,2,10)`;
- source-225 SHA-256 `8302307105F76A99C531AA8FD61908B58FF1C15537B703F6B6EFC20E895DCBE3`;
- native-96 SHA-256 `4329C62523C9E9BC0D3223037506E06160DB32B31F5876B7EBC68C95BE4F560B`.

Those Task 4B candidate hashes are preserved representative evidence, not runtime-fix or live-acceptance evidence. At that gate, a deterministic mismatch required an exact report and stop without rule relaxation or a second feasibility sweep. Task 5A now withdraws their visual PASS and supersedes them as active integration targets without erasing this history.

- [ ] **Step 4: Run focused GREEN and commit Task 4B**

Run the geometry, authority, full subpixel, and candidate-binding checks. Require every new mutation to fail causally and the unmutated implementation to pass. Output exact module/test/constants hashes, `Width`, `Width/2`, outline RGB, contour counts, endpoints, source/native candidate hashes, and the preserved Task 4 diagnostic intervals. Commit only the three listed Task 4B files after independent review.

Task 5A's independent production/test reviews approved the required independence, mutations, exact candidate reproduction, and unchanged authority boundaries. The 2026-08-28 Task 5A decision below supersedes the attempt-2 hashes as active targets while retaining them as Task 4B history; its automated integration gate is complete, while Task 6, whole-branch review, and live Windows acceptance remain pending.

---

### Task 5A: Integrate controller-selected F refinement into exact runtime art

**Files:**
- Modify: tools/Dororong.SubpixelOutline.psm1
- Modify: tools/Dororong.SubpixelOutline.Constants.psd1
- Modify: tests/Dororong.App.SubpixelOutline.Tests.ps1
- Modify: tools/Generate-CanonicalArt.ps1
- Modify: tests/Dororong.App.ExactArt.Tests.ps1
- Modify: tests/Dororong.App.ContinuousAuthority.Tests.ps1
- Modify: tools/New-ContinuousOutlineAuthority.ps1
- Modify: src/Dororong.App/Assets/dororong-canonical.png
- Modify: src/Dororong.App/Assets/dororong-closed-eyes.png

**Interfaces:**
- Consumes: Tasks 1 through 4B frozen source, seed, final mask, contour/kinds, endpoints, alpha, silhouette, protected art, support radii, outline RGB, and historical evidence; controller-selected F calibration; reviewed eye and resize boundaries.
- Produces: all-pixel smooth fill, fixed-support F coverage transfer, deterministic resize-only proxy, six evidence frames, exact committed 96px open/closed frames, and exact-art invariant/visual evidence.
- Supersedes: the attempt-2 source/native candidate binding and its withdrawn visual PASS. The old hashes remain historical Task 4B evidence and are not active Task 5A targets.

- [x] **Step 1: Write the F refinement RED before production edits**

Keep source identity, ownership/mask, contour records and kinds, endpoints, support `W = 2.20898670201159` / `W/2 = 1.104493351005795`, factor and sample locations, outline RGB `(26,2,10)`, source alpha, silhouette, protected delivered-source art, 96x96 dimensions/pixel format, alpha-zero RGB hygiene, presenter 96-DPI arrangement, native alpha hit testing, state mapping, eye confinement, mouth preservation, open/closed alpha equality, and one shared resize.

Before production edits, extend independent synthetic and exact-art tests to require all of the following behavior rather than source-text patterns:

- every final-mask pixel, including eligible-sample coordinates, equals an independently calculated nearest-eight `1/(1+d^2)` interpolation with ordinal Y,X ties and ties-to-even;
- independent raw `Ecoverage` and `Ccoverage` over the unchanged supports combine as `max(clamp(Ecoverage * 2.5, 0, 1), clamp(Ccoverage * 0.125, 0, 1))`;
- delivered source-225 alpha is unchanged and mask-zero RGB remains byte-identical;
- an independent four-connected component oracle selects exactly 6 enclosed neutral resize-proxy components and 12 pixels, including the diagnosed 7-pixel component with bounds `(144,159)-(147,160)`, with no coordinate allow-list, and independently applies the canonical encoding to require membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`;
- the proxy exists only on an unsaved resize-input clone, is identical for open/closed outside eye regions, and each frame is resized once; and
- direct and generator pipelines reproduce source-225 F SHA-256 `AB5E0F0990980F0393CF02FFDCDC15329EE7BC8F770D70D3DD68FCB775AD5A5A` and native-96 F SHA-256 `F4C9B2CCE253522345F12D29F6CC634ACD0DE1C3151D7C923460E3D5EBA73B9A`.

Invoke the real generator through its required `SourcePath`, `BodyMaskPath`, `OutputDirectory`, and `EvidenceDirectory` interface. Require it to emit:

    source-open-baseline.png
    source-open-candidate.png
    source-closed-candidate.png
    native-open-baseline.png
    native-open-candidate.png
    native-closed-candidate.png

Run:

    dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1

Observed causal RED: build exited `0`; the independent smooth-fill test exited `1` at `(11,11)`, expected `-1907739` and observed `-1973275`, because the old production path retained an eligible seed instead of assigning the required nearest-eight smooth result. This was the accepted F-specific behavioral RED, not a syntax, parameter-binding, process, pinned-input, or diagnostic-native-metric failure.

- [x] **Step 2: Implement the smooth field, F transfer, and resize proxy**

Require `SourcePath`, `BodyMaskPath`, `OutputDirectory`, and optional `EvidenceDirectory`. Import the reviewed source-raster and subpixel modules. Add exact constants for E multiplier `2.5`, C multiplier `0.125`, proxy maximum size `7`, proxy maximum chroma `8`, expected proxy component count `6`, expected proxy pixel count `12`, and expected proxy membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`. Change fill output so eligible seeds remain exact source RGB inputs but no longer bypass interpolation; assign the nearest-eight smooth field to every final-mask pixel. Keep ordinary unweighted center distance to `E union C` for sample eligibility.

Retain the exact E/C distance support, calculate separate raw coverage fractions, and apply the fixed F multipliers `E=2.5` and `C=0.125` before the max/0..1 clamp. Do not change support, geometry, endpoints, mask, alpha, or outline RGB. After boundary background removal, reconstruct delivered open and closed source frames from that field and prove source alpha, mask-zero delivered RGB, body equality outside eye regions, and exact source F identity.

For each source frame, create one unsaved resize-input clone. On that clone only, find four-connected mask-zero components and reject frame-touching, size-over-7, or chroma-over-8 components. Within each selected component sort pixels ordinally by `(Y,X)` and encode `C|count|Y,X;...`; sort component records by their first `(Y,X)`, join with LF and no trailing LF, and hash the UTF-8 bytes. Require exact pinned identity `6 components / 12 pixels` and membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`, then replace each selected pixel with the same smooth field. Apply the identical proxy set to open and closed, resize each clone exactly once through the shared premultiplied high-quality 225-to-96 function, and dispose it. Do not save the proxy clone or apply any native/post-resize correction.

Save six evidence images only when requested and save only native candidates to `OutputDirectory`. Print source, seed, mask, ownership, contour, authority, constants, module/test/generator, delivered open/closed source, proxy-input open/closed, and native open/closed hashes; factor; `W`; `W/2`; F multipliers; outline RGB; segment counts; eligible sample count; proxy component/pixel counts and canonical membership hash, which must equal `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`; resize counts; and legal endpoints.

Completed output: the generator emits exactly the six contract evidence PNGs, reproduces source-open F SHA-256 `AB5E0F0990980F0393CF02FFDCDC15329EE7BC8F770D70D3DD68FCB775AD5A5A`, runtime-open SHA-256 `F4C9B2CCE253522345F12D29F6CC634ACD0DE1C3151D7C923460E3D5EBA73B9A`, and runtime-closed SHA-256 `9B5411E88C031CA7D6542F6A1B3FD8E8C080BC061D96D4246B82011B9BF5A0BA`. Independent proxy evidence is exactly 6 components/12 pixels with membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`; reviewed eye/lid membership is independently 945 coordinates, and each eye state performs exactly one resize from its unsaved proxy clone.

- [x] **Step 3: Prove exact-art contracts and mutations**

Assert source alpha equality, delivered final-mask-zero source RGB equality, no dark/newly-owned sample admission, no retain-seed output branch, exact all-pixel smooth interpolation, frozen support/geometry, exact F coverage transfer, exact proxy selection and confinement, exact independently encoded proxy membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`, exact source/native F reproduction, generated/committed hash equality, open/closed source-body equality, open/closed proxy equality outside eye regions, open/closed native-body equality, and one resize per frame. Report the fifteen-normal source measurements, native `0.35` hair-match, and native `0.50` spread only as diagnostics; none may fail or pass the release gate.

The main-controller F visual PASS now applies to the exact integrated repository assets after deterministic generator/direct-pipeline equality and invariant checks passed. Its calibration provenance remains admissible only when all bindings match: evidence root `.superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/task-5a-visual-calibration/second-set`, manifest SHA-256 `95FC6C493C371563D0BA24C97B443A456275C2745C7C3F4121FD3A296D03BE36`, source-225 SHA-256 `AB5E0F0990980F0393CF02FFDCDC15329EE7BC8F770D70D3DD68FCB775AD5A5A`, runtime-open SHA-256 `F4C9B2CCE253522345F12D29F6CC634ACD0DE1C3151D7C923460E3D5EBA73B9A`, and runtime-closed SHA-256 `9B5411E88C031CA7D6542F6A1B3FD8E8C080BC061D96D4246B82011B9BF5A0BA`. Live WPF inspection and user acceptance remain `UNVERIFIED`; a mismatch is never silently promoted through a diagnostic measurement or substituted evidence set, manifest, hash, or automated result.

Unique temporary mutations must reach named semantic failures for: `W + 1/64`; factor 4; E multiplier `2.5 -> 1.0`; C multiplier `0.125 -> 1.0`; reintroducing exact retain-seed output; fill floor `225 -> 224`; final-mask boundary bit; eight-connected proxy discovery; proxy size limit `7 -> 6`; changing one selected proxy pixel's chroma to `9`; omitting one proxy component; changing one proxy membership record while preserving counts; admitting one frame-touching component; proxying only one eye state; delivered protected RGB; candidate alpha; a second resize; and closed-frame body RGB. Each mutation must fail its named behavioral/hash invariant, not a process, pinned-input, or source-text check.

Three test-only repairs kept those failures causal without changing production art authority: the fill-floor mutation uses an independently eligible in-memory clone at `(158,124)` to prove the `225 -> 224` admission; the continuous-authority test keeps literal frozen body `NativeFill`, normals, provenance, opacity, and bounds rather than reselecting targets from the candidate, while hair selection remains authoritative and body brightest reselection is diagnostic only; and the authority test/tool current-native identity was refreshed to the exact F runtime-open hash without changing the frozen fixture, contour, endpoints, normals, or mutations.

- [x] **Step 4: Generate assets and inspect every visual surface**

Run the generator into a temporary candidate directory first. Require exact source-open F hash `AB5E0F0990980F0393CF02FFDCDC15329EE7BC8F770D70D3DD68FCB775AD5A5A`, exact native-open F hash `F4C9B2CCE253522345F12D29F6CC634ACD0DE1C3151D7C923460E3D5EBA73B9A`, exact native-closed F hash `9B5411E88C031CA7D6542F6A1B3FD8E8C080BC061D96D4246B82011B9BF5A0BA`, exact proxy membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`, and exact open/closed body/proxy equality before visual review. Bind the controller verdict to evidence root `.superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/task-5a-visual-calibration/second-set` and manifest SHA-256 `95FC6C493C371563D0BA24C97B443A456275C2745C7C3F4121FD3A296D03BE36` together with those exact F hashes. Inspect source baseline/candidate, native baseline/candidate, source/native closed candidate, nearest-neighbor 4x versions, and white/dark composites. Cover the full frame, front outer/foot/inner, both valleys/undersides, center outer/foot/inner, rear outer/foot/inner, upper/lower rear rim, both continuation junctions, and all six proxy-component locations.

Fail on any new branch, expanded or displaced support, protruding antialias support, missing span, broken valley, flattened foot, pale outer body line, over-dark continuation, gray body speckle, protected-part change, opaque light fringe, or body segment visibly heavier/lighter than the unchanged hair reference. Confirm the body and proxy input are identical between open and closed frames outside eye regions and the eyes/mouth remain correct. Completed inspection: after exact reproduction and asset integration, the main controller passed the exact repository open/closed hashes at native 96px and nearest-neighbor 4x on white and RGB `(18,20,28)` backgrounds; none of these failure conditions was observed. The generator-updated runtime assets are the exact hashes recorded above.

- [x] **Step 5: Run focused GREEN and complete the automated integration gate**

Run:

    dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -SyntheticOnly
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -GeometryOnly
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1 -ReloadOnly
    pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release

Observed final state: build, BodyMask, ContinuousAuthority, Subpixel SyntheticOnly/full/GeometryOnly/ReloadOnly, and full ExactArt each exited `0`; ExactArt reported `EXACT ART PASS`, and the final automated suite passed. It reproduced the exact F source/open/closed hashes, proxy 6/12 membership SHA-256 `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`, reviewed eye membership 945, and one open plus one closed resize. Retained diagnostic metrics are source hair median `2.20898670201159`, native hair median `2.00980392156863`, native body minimum `1.07473031908363`, maximum `1.45131698364633`, and spread `0.376586664562707`; they do not replace hashes or visual acceptance. The invocation-bound ignored report is `.superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/task-5-runtime-integration/report.md`, SHA-256 `446B5BEEFDAAAD70FA7DB8AB77D98D5B8F371564989844F7AAD9EE0EA83E2061`. The tested file identities were `tests/Dororong.App.ExactArt.Tests.ps1` SHA-256 `22413D2D7FF5A9D4C605DBFE5EDEC4DE309370A0E1C76330DF130DD8B8498B59` and `tests/Dororong.App.ContinuousAuthority.Tests.ps1` SHA-256 `70640382D0C650FF53ABC9F2BB48D3D4AB70EC814D07873D5C3BC20B03BA1D8B`; every listed final command exited `0`.

The completed Task 5A implementation set awaiting whole-branch review is limited to `tools/Dororong.SubpixelOutline.psm1`, `tools/Dororong.SubpixelOutline.Constants.psd1`, `tests/Dororong.App.SubpixelOutline.Tests.ps1`, `tools/Generate-CanonicalArt.ps1`, `tests/Dororong.App.ExactArt.Tests.ps1`, `tests/Dororong.App.ContinuousAuthority.Tests.ps1`, `tools/New-ContinuousOutlineAuthority.ps1`, `src/Dororong.App/Assets/dororong-canonical.png`, and `src/Dororong.App/Assets/dororong-closed-eyes.png`.

Task 5A's automated integration gate is complete. Task 6 and whole-branch review remain pending, and actual live Windows/WPF rendering and user acceptance remain `UNVERIFIED`.

---

### Task 6: Verify, hand off, and publish the reviewed artifact

**Files:**
- Modify: README.md
- Modify: TASKS.md

**Interfaces:**
- Consumes: the reviewed Task 5A implementation set and exact asset/authority hashes.
- Produces: full automated evidence, publish identity, and accurate handoff with actual-Windows items still UNVERIFIED.

- [ ] **Step 1: Update documentation without promoting live status**

README explains the immutable seed, complete ownership mask, canonical E-union-C contour, all-pixel smooth fill with no retain-seed exception, fixed 8x8 E/C support, F optical transfer, deterministic resize-only proxy, one final resize, 96-DPI baseline, and exact commands. TASKS records attempts 5 and 6 as failures, both prior sweep mechanisms and attempt-2 candidate binding as superseded evidence, Tasks 1 through 5A by exact evidence, and the next live attempt as UNVERIFIED.

- [ ] **Step 2: Run fresh full verification and publish**

Run:

    dotnet restore DororongDesktopPet.sln
    dotnet test DororongDesktopPet.sln --configuration Release --no-restore
    dotnet build DororongDesktopPet.sln --configuration Release --no-restore
    pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
    pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
    pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
    pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
    dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64

Every command must exit 0. Record test count, failures/skips, build warnings/errors, focused PASS lines, and SHA-256 for publish EXE/App DLL/Core DLL. Prove the published App DLL embeds the exact current open/closed PNG bytes and binds to the reviewed source commit.

- [ ] **Step 3: Bind the publish to reviewed inputs**

Record SHA-256 for publish EXE/App DLL/Core DLL, source, immutable seed, final mask, constants, subpixel module, generator, open frame, and closed frame. Prove the published App DLL embeds the exact current open/closed PNG bytes and reports the reviewed Task 5A source commit. Any identity mismatch fails before documentation is committed.

- [ ] **Step 4: Commit truthful handoff**

    git add -- README.md TASKS.md
    git commit -m "docs: hand off complete Dororong body outline"

Task 6 is complete only after its task reviewer approves documentation truth and artifact identity. The controller then dispatches the broad whole-branch reviewer from plan base f09599b through the documentation HEAD. One reviewed fix wave addresses all Critical or Important findings. Repository review and successful publishing do not promote actual Windows observations.

---

## Controller-owned actual Windows acceptance

This phase is not dispatched to an implementation subagent because the direct observations and replies belong to the user in the root task.

1. Resolve artifacts/publish/win-x64/Dororong.App.exe and inspect only processes whose resolved ExecutablePath exactly matches. If an ambiguous exact-path survivor exists, do not launch.
2. Start the exact publish once and record PID, path, command line, creation time, documentation HEAD, executable/DLL/source/seed/mask/constants/open/closed hashes, and embedded commit. Liveness proves identity only.
3. Ask only whether the live Dororong preserves the original no-tail, three-leg/two-valley silhouette with no protrusion and whether front, feet, valleys, undersides, and rear rim have one apparent weight matching the thin hair outline.
4. Only after direct body PASS, ask whether closed eyes stay on the face while body pixels and mouth remain visually unchanged. Bind the user's exact observation and any screenshot hash.
5. Stop only the recorded task-owned PID after observation or abnormal exit, then verify no exact-path survivor remains. Do not relaunch automatically after a crash or user stop.
6. Create docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-7.md only after observation begins. Record exact artifact/PID/hash identity, user wording, each PASS/FAIL/UNVERIFIED result, and cleanup readback. A body FAIL stops closed-eye questioning and leaves Milestone 1 incomplete.

---

## Completion boundary

The outline implementation is complete only when Tasks 1 through 5A pass task-level specification and quality reviews, Task 6 passes the whole-branch review, every automated command exits 0, and the exact published artifact is identity-bound. The controller's pre-production F visual PASS does not satisfy this boundary. Milestone 1 remains incomplete until the user directly accepts the live open body and then the closed-eye integration on the current Windows 96-DPI environment. Every unobserved behavior remains UNVERIFIED rather than inferred.
