# Task 1 completion — continuous normal authority

Status: complete and committed within the Task 1 boundary.

- Commit: `cd038519bd247bb51aa8cf1484096fea2ac9b7c5`
- Final fixture SHA-256: `D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639`
- Canonical source remains `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- Reviewed mask remains `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`.
- Committed native-open reference remains `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`.
- `pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1`: exit `0`, `BODY MASK PASS`, count `5453`, bounds `38,110-177,204`.
- `pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1`: exit `0`, `CONTINUOUS AUTHORITY PASS`, hair `6`, body `15`.
- Moved endpoint, swapped fill/ink, and duplicate endpoint mutations all fail with their required named errors.
- Attempt-2 source/native and nearest-neighbor 4x overlays were opened and inspected; all six hair anchors and fifteen body normals cross one named visible stroke on both surfaces without a junction, protected part, separate stroke, or unrelated segment.
- Final overlay hashes: source `FCD979C0184E4BD3EFBC9B726F08226D151953C3163FFB50EED5CCDE152CD1AC`; source-4x `4308A4DEB4CCB156E30707D7E51900F2CF8E56FF015F705A68B376E4D6B79E55`; native `2E7DEC40AC1F17E3DE08124B7E9F27ACFBF2FE766C2B6B60AC9C1842D79B3C5C`; native-4x `C7F21D719C332999E59540718621C814AF611546C99573DBEBE1702C8D759AEC`.
- Durable details: `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`.
- No reconstructed body candidate was generated or inspected. Native body lines remain location-only authority; feasibility, candidate appearance, runtime rendering, and user acceptance remain outside Task 1.
- Manual-attempt 2–6 files remain unmodified and unstaged with their baseline hashes.

## Fix round 1/5 — opaque native extrema clarification

- Reviewer finding: the persistent plan said brightest/darkest existing native sample, while the implemented contract filters to opaque legacy samples and four native hair arrays contain a transparent absolute-darkest sample.
- Controller ruling: select the brightest/darkest **opaque** legacy native sample by Rec. 709 luminance, with ordinal `Y,X` ties. The separate reference-opacity requirement makes unqualified absolute extrema unsatisfiable, and transparent RGB has no visible optical luminance.
- Clarified only `docs/plans/2026-08-27-dororong-continuous-subpixel-outline.md` and the durable Task 1 report. The design spec, tests, helper, tool, fixture, and every frozen authority coordinate remain unchanged. The deferred `Spacing` validation minor was not changed in this round.
- `pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1`: exit `0`; `BODY MASK PASS hash=E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 count=5453 bounds=38,110-177,204`.
- `pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1`: exit `0`; `CONTINUOUS AUTHORITY PASS hash=D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639 hair=6 body=15`.
- Exact unchanged hashes: source `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`; mask `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`; native `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`; fixture `D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639`.
- Fix commit: `141417423eb3cb1c8116ff89783fd53ca71e8235` (`docs: clarify opaque native authority samples`).

## Historical bounded checkpoint

# Task 1 bounded checkpoint — continuous normal authority

Status: partial and deliberately uncommitted. The fixture is not semantically GREEN, so no authority hash, overlay evidence, visual verdict, Task 2 handoff, or completion claim exists.

## Repository boundary

- Worktree: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- BASE / current committed HEAD at start: `fa0c2e0c0815ed4da9369fb54f7f3ecca90212ff`
- Approved predecessor authority: `1dbab91`
- Pinned source: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- Pinned mask: `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`
- Pinned committed native-open location authority: `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`
- Current, non-final fixture hash: `C6EF81FEED47977EF034120DE07D7223821A89B72068180D97BE6B200DDCB7E4`

No reconstructed body candidate was created or inspected. Native body data was not used as body-weight acceptance.

## Implemented checkpoint

- Created `tests/support/Dororong.ContinuousOptics.ps1` with premultiplied bilinear sampling and 0.125-pixel trapezoid coverage integration.
- Created `tests/Dororong.App.ContinuousAuthority.Tests.ps1` with pinned source/mask/native hashes, literal field schema checks, reference opacity and bounds, category/name/protected-point checks, source/native continuous semantics, production independence checks, three authoring-tool mutations, and a final fixture-hash placeholder.
- Added literal `SourceNormal`, `NativeNormal`, `SourceFill`, and `NativeFill` fields to all six hair anchors and fifteen body normals, plus `SourceInk` and `NativeInk` for hair.
- Preserved all legacy sample arrays and source `Fill` / `Ink` fields as provenance.
- Selected native reference coordinates only from opaque legacy samples, by Rec. 709 luminance with ordinal `Y,X` ties, because the contract separately requires every selected reference coordinate to be opaque.

The supplied nested-array weight expression required a PowerShell-only structural correction before the contract could run: a typed list retains the four three-value entries, and additional parentheses retain the four exact numeric weight formulas. No sampling value or integration rule changed.

## Required RED

Command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
```

Result: exit `1` with the required first causal line:

```text
Hair anchor 'LeftTempleStraight' is missing SourceNormal.
```

The fixture had no diff when this RED was captured.

## Reference-only semantic corrections already applied

These changes were based only on the pinned source/native references and 0.125-pixel profiles; they are not feasibility results.

- `LeftTempleStraight` source: second endpoint `29,126` to `28,126`. The first-to-last line crossed the intended temple stroke and then the separate fringe stroke at the legacy last sample. The trimmed line has one run and endpoint darkness `0 / 0.033`.
- `LeftTempleStraight` native: second endpoint `12,54` to `10.5,54`. The derived line crossed the outer temple edge and the separate inner fringe stroke at `12,54`. The trimmed line has one run and endpoint darkness `0 / 0.056`.
- `RightFringeStraight` source: first endpoint `102,132` to `100,132`. The legacy endpoint began inside the antialiased run; the reference-light point at `100,132` restores a fill-side endpoint. The current line has one run and endpoint darkness `0.024 / 0`.
- `RightFringeStraight` native: first endpoint `44,56` to `41,56`. The legacy endpoint was inside the stroke. The current line has one run and endpoint darkness `0.053 / 0`.
- `UpperLeftDiagonal` native: second endpoint `14,43` to `11,41`. The first-to-last path continued through unrelated darker hair pixels after the named diagonal edge. The current line has one run and endpoint darkness `0 / 0`.
- `SecondValley` source: second endpoint `144,176` to `138,172`. The first-to-last path crossed the diagonal valley stroke and then a separate vertical leg stroke; the new endpoint is in the visible white gap between them. This change has not yet reached the full focused test because the unresolved hair line fails first.

## Exact current failure and unresolved coordinate

Freshest focused invocation on the current fixture: exit `1`.

```text
Hair anchor 'RightCrownDiagonal' source normal does not contain exactly one darkness run at threshold 0.10.
Expected '1', observed '3'.
```

Current `RightCrownDiagonal` source remains the unreviewed first-to-last line `99,62 -> 117,74`; its runs are at distances `0.625..1.750`, `2.000..3.250`, and `9.250..12.500`. A reference-only trial `102,64 -> 117,74` produced one run with endpoint darkness `0 / 0`, but it was not written because native-scale and 4x overlay inspection has not confirmed that it crosses only the named stroke.

Current `RightCrownDiagonal` native was provisionally trimmed to `44,27 -> 48,31`, but it is not acceptable: measured endpoint darkness is `0.466 / 0` and it has two runs. It must be replaced from source/native inspection, not relaxed or guessed. No native coordinate trial was accepted.

## Not yet implemented or verified

- `tools/New-ContinuousOutlineAuthority.ps1`
- Final semantic pass for all six hair and fifteen body lines
- Named moved-endpoint, swapped-fill/ink, and duplicate-endpoint mutation executions
- Final fixture SHA-256 pin (test still contains `__FINAL_AUTHORITY_SHA256__`)
- Source/native authority overlays and nearest-neighbor 4x files
- Required visual inspection and per-line observations
- Body-mask regression GREEN
- Tracked `artifacts/work-reports/dororong-continuous-subpixel-outline-task-report.md`
- The specified five-path commit

Task 2 remains blocked.

## Preserved unrelated state

The five pre-existing untracked manual-attempt documents remain unmodified and unstaged. Their readback hashes still match the baseline:

- attempt 2: `3FA63FC2847A5D270907D6A6803D183894D1BDE285F3767FD7A925DA7C0EBCEF`
- attempt 3: `CD885EE415B836B4E8791494AFEAE9620F2E5B8B81C67A746176A0CEA9ED0B84`
- attempt 4: `B6756E57BE85EE3EF0FD648AFB6E464ED8E2D0591A9D4C8CB158D0844F491F39`
- attempt 5: `6E60C673E2F167907D34CF0D32F6E40B8CEBDBFB5EC3D705DA43505F4ABEB6F9`
- attempt 6: `2B66C63DA1FFE4C531DC4230856B3FB7D71CEFEAAFA5BA78F6700663B1C79E92`

<!-- CONTOUR-AUTHORITY-FIX:BEGIN -->
## Contour-authority fix — approved half-pixel crossing

Date: 2026-08-27

This bounded correction removes the defective `SecondValley` measurement authority identified after the first continuous-subpixel sweep. It does not tune any body width to a sweep. The source and mask remain the geometric authority, the six hair anchors and their source/native medians remain frozen, and one global body width tied to the head/hair outline remains mandatory.

### Bound identity and isolation

- BASE: `d005f407764c15b612494328d3bed2b7dee92f15`
- Canonical source SHA-256 remained `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- Body-region mask SHA-256 remained `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`.
- Committed native-open SHA-256 remained `611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3`.
- Corrected continuous-authority fixture SHA-256: `87D0311B043368E0E21C2FD3E17EE7AC79FE8D217BEF5F231C4B3D1CD341490B`.
- No reconstructed body candidate was generated, saved, or viewed. No feasibility interval, candidate pixel, per-normal passing interval, or sweep score participated in the assertion or endpoint selection.
- The Task 2 test, source-raster module, outline module, constants file, Task 2 report, sweep outputs, and candidates were not read; no Task 2 test was run. Manual-attempt 2–6 documents were not touched or staged.

### Independent contract and required RED

Before the fixture changed, the focused test gained a test-local derivation of source half-pixel contour segments from the pinned source and binary mask. It does not import production geometry. Literal named-segment windows bind each of the fifteen body names to its approved local contour; neutral source pixels exclude colored protected-part contacts; the two frozen legal endpoints receive deterministic nearest-vertex continuations. Segment intersections are computed geometrically, and two contiguous stair-step edges sharing one hit vertex deduplicate to one crossing.

The production change this test catches is a body `SourceNormal` that still measures legacy ink or another mask boundary but stops before its named approved contour.

```text
command: pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
exit: 1
causal line: Body normal 'SecondValley' SourceNormal does not intersect the approved contour.
fixture at RED: D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639
```

Every earlier body normal reached its named derived contour before this exact failure. The failure therefore reproduced the expected authority defect rather than a schema, source, mask, optical-profile, or unrelated-normal problem.

### Minimum authority correction

Only the failing `SecondValley` entry changed; all other body normals and every hair anchor/reference remained byte-for-byte unchanged.

- Source old: eighths `(1056,1344) -> (1104,1376)`, pixels `(132,168) -> (138,172)`.
- Source new: eighths `(1104,1376) -> (1152,1408)`, pixels `(138,172) -> (144,176)`.
- Native old: eighths `(440,560) -> (464,592)`, pixels `(55,70) -> (58,74)`.
- Native new: eighths `(464,592) -> (504,592)`, pixels `(58,74) -> (63,74)`.

The old source segment crossed the retained center-side legacy ink and ended in the light valley gap `0.5` pixel before the named rear-side half-pixel contour. The new source segment begins in that gap, intersects the named contour once at `(138.5,172.333333...)`, crosses one rear-side dark run, and reaches opaque near-white body fill. The corresponding native line begins in the downsampled gap and crosses the matching rear-side location horizontally into body fill. Source/native fill references, legacy provenance arrays, mask, protected points, legal endpoints, thresholds, tolerances, and the one-width rule did not change.

After the coordinate change but before pinning the new hash, the focused test reached only the intentional final hash guard:

```text
exit: 1
final guard: Continuous authority fixture changed. Expected 'D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639', observed '87D0311B043368E0E21C2FD3E17EE7AC79FE8D217BEF5F231C4B3D1CD341490B'.
```

That bounded checkpoint proves the source/body semantics and the three embedded mutations completed before the fixture hash was updated.

### GREEN and mutation sensitivity

```text
command: pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
exit: 0
output: BODY MASK PASS hash=E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 count=5453 bounds=38,110-177,204

command: pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
exit: 0
output: CONTINUOUS AUTHORITY PASS hash=87D0311B043368E0E21C2FD3E17EE7AC79FE8D217BEF5F231C4B3D1CD341490B hair=6 body=15
```

The focused GREEN executed all three authoring mutations and rejected them with their named causal errors:

- moved endpoint: `Continuous authority hash changed.`
- swapped fill/ink: `fill luminance must exceed ink luminance.`
- duplicate endpoint: `has duplicate endpoints.`

### Attempt-3 reference-only overlay evidence

The overlay tool change is the minimum required evidence binding: only its literal expected authority hash changed from `D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639` to `87D0311B043368E0E21C2FD3E17EE7AC79FE8D217BEF5F231C4B3D1CD341490B`. Its source/mask/native pins, validation, drawing, labels, output set, and no-candidate behavior are unchanged.

Command exited `0`:

```text
pwsh -NoProfile -File tools/New-ContinuousOutlineAuthority.ps1 -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png -MaskPath src/Dororong.App/Assets/dororong-body-region-mask.png -NativePath src/Dororong.App/Assets/dororong-canonical.png -AuthorityPath tests/fixtures/dororong-body-outline-authority.psd1 -EvidenceDirectory artifacts/verification/2026-08-27-continuous-authority-task1-contour-fix-attempt-3
```

- `artifacts/verification/2026-08-27-continuous-authority-task1-contour-fix-attempt-3/dororong-continuous-authority-source.png`: `6C71071269C0FB6B4C688D617DD2B53040E8E72145333910D24AEDF50EE8B5F8`
- `artifacts/verification/2026-08-27-continuous-authority-task1-contour-fix-attempt-3/dororong-continuous-authority-source-4x.png`: `80EA7E6E06BD69382CEEA1794B27FD7FA0824D67E71A00E9FDCA79FD99038D92`
- `artifacts/verification/2026-08-27-continuous-authority-task1-contour-fix-attempt-3/dororong-continuous-authority-native.png`: `A7BF11D33F0D1913928395122FB969B593E6E05FBD13384C3939C3BB5B371031`
- `artifacts/verification/2026-08-27-continuous-authority-task1-contour-fix-attempt-3/dororong-continuous-authority-native-4x.png`: `2FF3E029971925E1E4451161BB3BD216BF6E7F995EDDB26FD11442C09D112F0E`

All four exact files were opened. The full source/native files establish the whole-frame regression boundary; their nearest-neighbor 4x versions establish the changed relation without interpolation.

- Source `SecondValley`: `(138,172)` is opaque neutral gap RGB `(243,243,243)`; the red line crosses the rear-side approved contour once, passes through the single dark band visible at `(139,173)` RGB `(11,11,11)` through `(141,174)` RGB `(39,39,39)`, and reaches opaque white fill at `(142,175)` through `(144,176)`. It does not reach the center-side stroke, the rear-foot junction, hair, ribbon, or another body segment. Result: PASS.
- Native `SecondValley`: `(58,74)` is the almost-transparent gap (`A=1`); the horizontal red line crosses the one alpha-bearing dark band at `(59,74)` (`A=194`, RGB `(15,15,15)`) and its light support at `(60,74)`, then reaches opaque white body pixels `(61..63,74)`. It does not touch the separate left diagonal center stroke or lower rear-leg run. Result: PASS as native location authority only.
- Full-frame regression boundary: compared with attempt 2, source overlay differences are confined to 76 pixels inside `131,161-154,173` of the 225x225 frame, and native differences are confined to 40 pixels inside `54,65-71,73` of the 96x96 frame. Those bounds contain only the moved `SecondValley` line, endpoints, and labels; all six hair anchors and the other fourteen body-normal overlay relations remain unchanged. Result: PASS.
- Artifact identity: the tool revalidated the pinned source, mask, native-open, and new fixture hashes before writing the four overlays, and it draws on cloned references only. Result: PASS.

Overall visual verdict: PASS for the corrected Task 1 source/native authority-overlay relation and its full-frame regression boundary. This verdict does not approve source-only feasibility, a reconstructed body, candidate stroke weight, packaged/runtime rendering, actual Windows rendering, or user acceptance. Task 2 may consume the new authority only after the fresh Task 1 review/commit boundary; this report contains no Task 2 result.
<!-- CONTOUR-AUTHORITY-FIX:END -->
