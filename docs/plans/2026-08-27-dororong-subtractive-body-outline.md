# Dororong Subtractive Body Outline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the rejected hand-authored native-96 body redraw with a source-preserving subtractive thinning mask that cannot introduce darker pixels or move Dororong's outer silhouette.

**Architecture:** Keep the pinned 225x225 source, boundary transparency, closed-eye derivation, premultiplied resize, native-96 resources, and WPF presenter unchanged. Replace only the post-resize body correction: select reviewed fully opaque pixels on the inner half of existing body ink and blend them toward explicit local body-fill samples. Tests compare generated frames to the uncorrected deterministic resize baseline and reject darkening, alpha changes, partial-alpha edits, mask drift, protected-art changes, stale assets, and presenter regressions; exact native visual review and actual-Windows user observation remain separate gates.

**Tech Stack:** PowerShell 7, `System.Drawing`, C# / .NET 8, WPF, xUnit solution tests, Git.

**Spec:** `docs/specs/2026-08-26-dororong-m1-design.md`

## Global Constraints

- Work only in `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1` on `feature/dororong-m1`.
- Preserve `src/Dororong.App/Assets/dororong-canonical-source.png` byte-for-byte at 225x225 and SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- Keep `dororong-canonical.png` and `dororong-closed-eyes.png` as deterministic 96x96 32bpp ARGB runtime frames presented at 96x96 DIPs on the current 96-DPI / 100%-scale target.
- Do not use image generation, redraw the character, add a tail, reinterpret the white ribbons, change eyes/mouth/hair/rose/bow/ribbons, or change behavior/motion.
- Remove and do not reuse `New-NativeBodyContour`, `New-PathMask`, the 1.35px stroke, round caps, or `valleyClearPath`.
- Body correction may only lighten reviewed existing fully opaque baseline body-ink pixels toward reviewed local body-fill RGB. It may not darken any channel, change alpha, touch partial-alpha pixels, or change RGB outside the exact reviewed mask.
- Preserve the failed exact-artifact evidence in `docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-5.md`; do not overwrite it.
- Subagents must not edit or stage root-owned untracked `docs/verification/*manual-attempt*.md` files.
- Automated or repository visual PASS cannot upgrade actual-Windows acceptance; the user verifies one live item at a time.

---

### Task 1: Replace the rejected redraw with a subtractive native-96 correction

**Files:**
- Modify: `tests/Dororong.App.ExactArt.Tests.ps1:29-336`
- Modify: `tools/Generate-CanonicalArt.ps1:152-329`
- Modify: `src/Dororong.App/Assets/dororong-canonical.png`
- Modify: `src/Dororong.App/Assets/dororong-closed-eyes.png`
- Create: `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`

**Interfaces:**
- Consumes: `Resize-ToNative96([System.Drawing.Bitmap]) -> System.Drawing.Bitmap`, the existing `BaselineOutputDirectory` output, and the unchanged source-scale eye derivation.
- Produces: `$nativeBodyLightenRuns`, a literal reviewed list of native-96 runs; `Apply-SubtractiveNativeBodyCorrection([System.Drawing.Bitmap])`; deterministic open/closed runtime PNGs; initial RED/GREEN and native-inspection evidence for Task 2.

- [ ] **Step 1: Replace the rejected body assertions with a failing subtractive contract**

Keep source identity, dimensions, alpha-zero hygiene, eye semantics, clean-hair references, WPF arrangement, hit testing, and state mapping. Remove `Test-InBodyTopologyBand`, the attempt-5 `visibleBodySupport` connected-component assertion, the three `spurProbe` assertions, the 13 attempt-5 `$bodyProfiles`, and the `correction >= 120` requirement.

For every generated/baseline pixel pair, add the following assertions before accepting a changed body coordinate:

```powershell
if ($openPixel.ToArgb() -ne $openBasePixel.ToArgb())
{
    Assert-True (Test-InBodyProtectionBand $x $y) `
        "Open RGB changed outside the body protection band at ($x,$y)."
    Assert-Equal 255 $openBasePixel.A `
        "Open body correction touched a non-opaque baseline pixel at ($x,$y)."
    Assert-True ($openPixel.R -ge $openBasePixel.R -and
        $openPixel.G -ge $openBasePixel.G -and
        $openPixel.B -ge $openBasePixel.B) `
        "Open body correction darkened a baseline channel at ($x,$y)."
    [void]$openCorrection.Add("$x,$y")
}
```

Repeat the same checks for closed versus `baselineClosed`. Require `openCorrection.Count -gt 0`, identical open/closed correction coordinate sets, and identical corrected RGB at every body-correction coordinate. Assert every partial-alpha baseline coordinate is byte-identical in the corrected frame.

- [ ] **Step 2: Run the exact-art test and capture genuine RED**

Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj --configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: build exits `0`; exact-art exits `1` against the unchanged rejected generator. The first causal failure must be the channel-wise darkening check, with the current artifact reproducibly darkening baseline `(18,70)` from ARGB `255,254,254,254` to `255,102,95,98` or an earlier scan-order coordinate with the same violation. Record the exact command, exit code, message, and coordinate in the task report.

- [ ] **Step 3: Produce the uncorrected reference and select one explicit inner-ink mask**

Run the real generator with both output directories, then inspect the uncorrected open frame alone at native 96px and nearest-neighbor enlargement on white and RGB `(18,20,28)` backgrounds. Freeze these identity anchors before viewing a candidate: no tail; hair overlaps the body at the neck; the rose/bow/ribbons retain their order; three separated leg silhouettes and their open valleys remain; the outer body edge follows the source-derived resize.

For each exposed segment—front outer/inner/foot, first valley/underside, center outer/foot/inner, second valley/underside, rear outer/foot/inner, upper/lower rear rim—select only the inside-most fully opaque baseline dark-ink pixel when a multi-pixel run is visibly heavier than the fixed clean-hair references. Leave the outermost source-derived dark support unchanged. Each selection records an explicit fully opaque body-fill sample and a blend factor; do not create broad rectangles or infer a new curve.

Encode selections as literal hashtables in `$nativeBodyLightenRuns`. Use this constructor so every final entry has the same validated shape:

```powershell
function New-NativeBodyLightenRun(
    [int]$Y, [int]$StartX, [int]$EndX,
    [int]$FillX, [int]$FillY, [double]$Blend)
{
    if ($StartX -gt $EndX -or $Blend -le 0.0 -or $Blend -gt 1.0)
    {
        throw 'Invalid native body lighten run.'
    }
    return @{ Y=$Y; StartX=$StartX; EndX=$EndX; FillX=$FillX; FillY=$FillY; Blend=$Blend }
}
```

Populate the final array exclusively with calls containing the reviewed real body coordinates. A fill sample must have alpha `255`, each fill RGB channel must be at least the corresponding baseline channel for every run pixel, and the selected pixel itself must have baseline alpha `255`.

- [ ] **Step 4: Implement the minimal subtractive correction and delete the redraw mechanism**

Delete `New-NativeBodyContour`, `New-PathMask`, `Get-LocalBodyFill`, and `Apply-NativeBodyCorrection`. Add this implementation shape, using a clone so fill samples always come from the unmodified baseline:

```powershell
function Apply-SubtractiveNativeBodyCorrection([System.Drawing.Bitmap]$Bitmap)
{
    $baseline = $Bitmap.Clone()
    try
    {
        foreach ($run in $nativeBodyLightenRuns)
        {
            $fill = $baseline.GetPixel($run.FillX, $run.FillY)
            if ($fill.A -ne 255) { throw "Body fill sample is not opaque at ($($run.FillX),$($run.FillY))." }

            for ($x = $run.StartX; $x -le $run.EndX; $x++)
            {
                $original = $baseline.GetPixel($x, $run.Y)
                if ($original.A -ne 255) { throw "Body correction is not opaque at ($x,$($run.Y))." }
                if ($fill.R -lt $original.R -or $fill.G -lt $original.G -or $fill.B -lt $original.B)
                {
                    throw "Body fill sample darkens ($x,$($run.Y))."
                }

                $red = [Math]::Round($original.R + (($fill.R - $original.R) * $run.Blend))
                $green = [Math]::Round($original.G + (($fill.G - $original.G) * $run.Blend))
                $blue = [Math]::Round($original.B + (($fill.B - $original.B) * $run.Blend))
                $Bitmap.SetPixel($x, $run.Y, [System.Drawing.Color]::FromArgb(255, $red, $green, $blue))
            }
        }
    }
    finally
    {
        $baseline.Dispose()
    }
}
```

Replace both calls to `Apply-NativeBodyCorrection` with `Apply-SubtractiveNativeBodyCorrection`. Do not alter resize, transparency, eyes, paths, presenter, or behavior code.

- [ ] **Step 5: Regenerate the exact open and closed assets**

Run:

```powershell
pwsh -NoProfile -File tools/Generate-CanonicalArt.ps1 `
  -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png `
  -OutputDirectory src/Dororong.App/Assets
```

Expected: exit `0`; both runtime frames are exactly 96x96 ARGB; the pinned source hash is unchanged.

- [ ] **Step 6: Run the focused test to GREEN**

Run:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: exit `0`; every body change is fully opaque, channel-wise non-darkening, alpha-preserving, confined to the body band, and identical across eye states; all retained eye/presenter/hit-test checks pass.

- [ ] **Step 7: Inspect the exact candidate and write attempt-specific evidence**

Inspect the exact source first, then baseline and candidate at native 96px and nearest-neighbor enlargement on white and RGB `(18,20,28)`. Record separate observations for all named body segments and legal occlusion endpoints. Reject the candidate if it shows any new/newly exposed branch, endpoint, shifted edge, white fringe, missing span, broken valley, or protected-part damage. Record exact source/open/closed hashes, changed-coordinate count and bounds, selected fill samples/blends, RED/GREEN output, and the actual visual verdict in `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`. Repository visual approval remains distinct from actual Windows.

- [ ] **Step 8: Commit the implementation task without manual-attempt files**

```powershell
git add -- tools/Generate-CanonicalArt.ps1 tests/Dororong.App.ExactArt.Tests.ps1 `
  src/Dororong.App/Assets/dororong-canonical.png `
  src/Dororong.App/Assets/dororong-closed-eyes.png `
  artifacts/work-reports/dororong-subtractive-body-outline-task-report.md
git commit -m "fix: thin Dororong body from source ink"
```

Expected: the commit contains only the five listed paths. `docs/verification/*manual-attempt*.md` remains unstaged.

---

### Task 2: Independently review and freeze the accepted native body mask

**Files:**
- Modify: `tests/Dororong.App.ExactArt.Tests.ps1`
- Modify: `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`
- Modify only if a concrete review defect requires it: `tools/Generate-CanonicalArt.ps1`
- Modify only with generator changes: `src/Dororong.App/Assets/dororong-canonical.png`
- Modify only with generator changes: `src/Dororong.App/Assets/dororong-closed-eyes.png`

**Interfaces:**
- Consumes: Task 1 exact source, uncorrected baseline, candidate PNGs, literal correction runs, focused test output, and visual report.
- Produces: independent approval or concrete defect coordinates; a frozen correction-coordinate hash; a frozen source-derived outer-edge fixture; post-review body probes based on the accepted subtractive candidate; reviewed commit.

- [ ] **Step 1: Dispatch a fresh reviewer with no write authority**

The reviewer must inspect the source alone first and freeze the identity anchors, then inspect baseline and candidate open/closed frames at native and nearest scale on both backgrounds. Review every named body segment and legal occlusion endpoint, the attempt-5 failure locations `(18..19,70..74)`, `(43..46,76..85)`, `(72..73,60..66)`, the eyes, mouth, rose, bow, ribbons, and no-tail rear profile. It must also audit the generator/test diff for any new darkening path, partial-alpha edit, broad mask, self-referential test, or reuse of rejected attempt-5 body profiles.

Expected: `APPROVED`, or a list of concrete Critical/Important findings with exact coordinates/evidence. No dependent documentation or publish work starts on a rejection.

- [ ] **Step 2: If review finds a concrete defect, perform one evidence-bound fix round**

Send the findings back to the Task 1 implementer. Change only the literal subtractive mask/fill/blend entries and the generated PNGs needed for those coordinates; do not introduce another curve, path, dilation, or dark stroke. Re-run the focused exact-art test and the same native visual surfaces. If the same mechanism still fails after this single fix round, stop and report the candidate as rejected instead of adding another coordinate patch.

- [ ] **Step 3: Freeze independent regression fixtures only after visual approval**

In the exact-art test, hash the ordinal-sorted UTF-8 correction-coordinate list and pin the accepted SHA-256. Add literal source-derived outer-edge coordinates covering each named segment and legal occlusion endpoint, and assert those RGBA values remain byte-identical to the baseline. Add new body probe definitions sampled from the approved subtractive candidate; retain the fixed clean-hair references, but do not copy the rejected 13 attempt-5 coordinates or thresholds.

The coordinate hash code must use this stable form:

```powershell
$orderedCoordinates = @($openCorrection | Sort-Object)
$coordinateBytes = [Text.Encoding]::UTF8.GetBytes(($orderedCoordinates -join "`n"))
$coordinateHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($coordinateBytes))
Write-Output "REVIEWED BODY MASK SHA256: $coordinateHash"
```

After the independent approval run prints the hash, replace the diagnostic output with a direct `Assert-Equal` whose expected argument is that exact 64-character value in the same commit. Record the identical value and coordinate count in the report.

- [ ] **Step 4: Run focused verification after fixture freeze**

```powershell
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Expected: exit `0`; stale asset, mask drift, darkening, alpha/edge changes, eye regression, presenter sizing, and hit testing are all covered.

- [ ] **Step 5: Commit the reviewed freeze/fix**

```powershell
git add -- tests/Dororong.App.ExactArt.Tests.ps1 `
  artifacts/work-reports/dororong-subtractive-body-outline-task-report.md `
  tools/Generate-CanonicalArt.ps1 `
  src/Dororong.App/Assets/dororong-canonical.png `
  src/Dororong.App/Assets/dororong-closed-eyes.png
git commit -m "test: freeze reviewed Dororong body thinning"
```

Stage only paths actually changed. Do not stage manual-attempt records.

---

### Task 3: Update durable handoff and produce a fresh release artifact

**Files:**
- Modify: `README.md:38-42`
- Modify: `TASKS.md:3-13`
- Modify: `artifacts/work-reports/dororong-subtractive-body-outline-task-report.md`

**Interfaces:**
- Consumes: independently approved Task 2 commit, frozen hashes/fixtures, exact focused-test result.
- Produces: current user-facing architecture/limitations, JOENESS task state, full verification evidence, and a fresh framework-dependent `win-x64` publish artifact bound by hashes.

- [ ] **Step 1: Replace obsolete redraw wording without changing scope**

Update README to say that native-96 body normalization only lightens reviewed fully opaque inner body-ink pixels from the deterministic resize baseline; it never draws a replacement contour, changes alpha/outer-edge pixels, or edits protected character parts. Keep the exact source, no-tail/ribbon, eye derivation, 96-DPI, and actual-Windows boundaries.

Update `TASKS.md` so attempt 5 is recorded as FAIL, the subtractive implementation/repository review status is exact, and actual-Windows attempt 6 remains UNVERIFIED until user observation. Remove the stale claim that a new automation target is required; manual observation on the current PC is the chosen method.

- [ ] **Step 2: Run fresh full automated verification from the reviewed commit**

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release `
  --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

Expected: every command exits `0`; Release test count and any warnings/skips are recorded exactly; publish produces `Dororong.App.exe`, `Dororong.App.dll`, and `Dororong.Core.dll`.

- [ ] **Step 3: Bind the exact release artifact and current limits**

Record HEAD, worktree, Windows version, 96-DPI target, source/open/closed hashes, publish executable/application/core DLL hashes, test counts, build warnings/errors, independent review verdict, and visual surfaces in the task report. State that WALK/click motion remains deferred by the user and that actual-Windows body/eye/user acceptance remains UNVERIFIED until Task 4.

- [ ] **Step 4: Commit documentation only**

```powershell
git add -- README.md TASKS.md artifacts/work-reports/dororong-subtractive-body-outline-task-report.md
git commit -m "docs: record subtractive body verification"
```

Expected: documentation matches fresh outputs; no manual-attempt file is staged.

---

### Task 4: Run actual-Windows manual attempt 6 one item at a time

**Files:**
- Create: `docs/verification/2026-08-27-m1-windows-acceptance-manual-attempt-6.md`

**Interfaces:**
- Consumes: Task 3 exact published executable/hash set and zero exact-path process baseline.
- Produces: user-observed PASS/FAIL/UNVERIFIED evidence for the corrected body first, then closed-eye integration only if the body passes.

- [ ] **Step 1: Establish a safe exact-process baseline and launch once**

Resolve `artifacts/publish/win-x64/Dororong.App.exe`, enumerate only processes whose `ExecutablePath` equals that resolved path, and do not launch if an ambiguous survivor exists. Launch the exact artifact once, then record PID, path, command line, creation/start time, responding state, HEAD, and hashes. Process liveness proves identity only, not rendering success.

- [ ] **Step 2: Create the attempt-6 record as UNVERIFIED before asking for observation**

Record exact artifact/target identity, fresh automated evidence, independent repository visual review, the attempt-5 failure boundary, and the manual/automation separation. Keep body and closed-eye checks UNVERIFIED.

- [ ] **Step 3: Ask only the body-outline question**

Ask the user whether the live body/back/leg line follows the original shape without any protruding stroke and reads consistently near the hair-line weight. Accept only the user's direct observation. Record PASS or FAIL with any supplied screenshot/hash; do not infer from assets, tests, or process state.

- [ ] **Step 4: Continue only when the body passes**

If body FAILS, identity-check and stop the recorded PID, preserve the exact attempt as FAIL, and return to diagnosis. If body PASSES, update the record and ask only the closed-eye/blink integration question next. WALK/click motion stays deferred. Milestone 1 remains incomplete until all separately required actual-Windows items are observed.

- [ ] **Step 5: Clean up the task-owned process when this correction attempt ends**

Before stopping, re-check the recorded PID against exact path, command line, and start time. Stop only that matching process, verify no exact-path survivor remains, and record the cleanup result. Never stop by process name.
