# Dororong source silhouette outline — Task 1 report

## Result

The canonical 225x225 source was preserved byte-for-byte and a source-derived,
binary body-region authority was frozen without producing or inspecting any
reconstructed body candidate.

- Canonical source: `src/Dororong.App/Assets/dororong-canonical-source.png`
- Source SHA-256: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- Source dimensions/pixel format: `225x225`, `Format24bppRgb`
- Fixed source seed: `(160,114)`, source RGB `240,240,240`
- Seed component: `4,259` near-white pixels; largest other internal
  near-white component: `803` pixels
- Reviewed mask: `src/Dororong.App/Assets/dororong-body-region-mask.png`
- Mask SHA-256: `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`
- Mask dimensions/pixel format: `225x225`, `Format32bppArgb`
- Writable pixels/bounds: `5,453`, `(38,110)-(177,204)` inclusive
- Mask encoding: alpha `255`; RGB channels equal and limited to `0/255`
- Writable topology: one four-connected component

The final literal runs retain the fixed-seed body fill and exposed body ink /
antialias coverage. Source-contour pixels on the occluder side of the lower
hair/face contact and both white ribbon contacts were removed before the mask
was frozen. Dororong has no tail; the rear selection ends on the body/back and
rear-foot silhouette.

## TDD evidence

The focused test was created before the mask or PSD1 authority artifact.

RED command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
```

Exit code: `1`

Exact first causal failure after the canonical-source hash assertion passed:

```text
Exception: D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\tests\Dororong.App.BodyMask.Tests.ps1:13
Line |
  13 |      { throw $Message }
     |        ~~~~~~~~~~~~~~
     | Reviewed body-region mask is missing.
```

Final generation command:

```powershell
pwsh -NoProfile -File tools/New-BodyRegionMask.ps1 `
  -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png `
  -OutputPath src/Dororong.App/Assets/dororong-body-region-mask.png `
  -EvidenceDirectory artifacts/work-reports/dororong-body-mask-authority
```

Exit code: `0`

```text
SOURCE BODY COMPONENT hash=F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504 seed=160,114 count=4259 largestOther=803
BODY MASK GENERATED hash=E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 count=5453 bounds=38,110-177,204
SOURCE-ONLY EVIDENCE directory=D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\work-reports\dororong-body-mask-authority
```

GREEN command:

```powershell
pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
```

Exit code: `0`

```text
BODY MASK PASS hash=E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779 count=5453 bounds=38,110-177,204
```

The GREEN test pins both source and mask hashes, checks the mask's exact pixel
format and binary channels, verifies one connected writable component, rejects
every protected point, and verifies every fill sample is an opaque near-white
source pixel selected by the mask.

## Independent fixture inventory

`tests/fixtures/dororong-body-outline-authority.psd1` is literal data only and
was selected from the canonical source before any candidate work.

- Hair anchors: `6` total — `2 Straight`, `2 Diagonal`, `2 Curve`
- Body normals: `15` — `FrontOuter`, `FrontFoot`, `FrontInner`,
  `FirstValley`, `FirstUnderside`, `CenterOuter`, `CenterFoot`,
  `CenterInner`, `SecondValley`, `SecondUnderside`, `RearOuter`,
  `RearFoot`, `RearInner`, `UpperRearRim`, and `LowerRearRim`
- Protected points: `20` — two each for head, hair, face, mouth, eyes, rose,
  bow, and ribbons; two no-tail rear checks; and both legal occlusion endpoints
- Fill samples: `8` — two each across front, center, rear, and rear-rim regions
- Generator independence: `tools/New-BodyRegionMask.ps1` contains no fixture,
  PSD1, authority, or `Import-PowerShellDataFile` reference

## Source-only visual evidence

Authoritative evidence directory:
`artifacts/work-reports/dororong-body-mask-authority`

- Source alone: `dororong-canonical-source.png`
  (`F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`)
- Native-scale red overlay: `dororong-body-region-mask-overlay.png`
  (`8B2D7D8B8468926EE9F51653EEC5CC183BE97891264E96E066ECD3AAEABB6B1B`)
- 4x nearest-neighbor red overlay: `dororong-body-region-mask-overlay-4x.png`
  (`B73332292737B618A31E761DBCF0C1BCAECE7C01D25C535281FAA5207A5AA45A`)
- Mask only: `dororong-body-region-mask.png`
  (`E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`)
- 4x nearest-neighbor mask: `dororong-body-region-mask-4x.png`
  (`9BD56A17BBE3684835F1C08A2D73BFBB226D0CA285DD9EC613EE24F6C95320C4`)
- Source contour capture: `dororong-source-body-contour-capture.png`
  (`7942E2E9A73B6FC605C1EB4A7A103CF233A574F40680FC72DA89A1E33720EEA4`)
- 4x contour capture: `dororong-source-body-contour-capture-4x.png`
  (`404CA851D45ADC445C4290EB1FAF001E8501C9D54F2589BC40E513953EAEDA15`)

Visual completion claim: the exact pinned mask selects the visible source body
and exposed body ink while excluding every named protected part. Covered
surfaces were the exact 225px overlay, the exact mask-only PNG, the 4x
nearest-neighbor overlay, and the 4x contour capture. Acceptance source was the
Task 1 brief plus the pinned canonical source.

- **V1 — protected-part exclusion: PASS.** At 225px and 4x, head, pink hair,
  face, mouth, both eyes, rose, purple bow, and both white ribbons remain
  unred. The black lower-hair/face contact and both ribbon borders also remain
  unselected.
- **V2 — exposed body coverage: PASS.** Red selection continuously covers the
  visible white body, upper/lower rear rim, front/center/rear outer edges,
  all three feet, both valleys, both underside spans, and their exposed dark
  ink/antialias pixels. The mask-only view shows one connected shape.
- **V3 — legal occlusion endpoints: PASS.** The front protected coordinate
  `(112,151)` and rear protected coordinate `(157,116)` are mask zero;
  adjacent visible body runs begin without crossing into hair or ribbon.
- **V4 — no-tail rear: PASS.** The rounded back/rear-leg silhouette ends at
  source X `177`; no mask appears at the explicit upper/lower no-tail probes
  `(181,127)` and `(180,163)`.
- **V5 — artifact identity: PASS.** The source copy and mask-only evidence
  hashes equal the canonical source and committed mask hashes above.

Overall source-only visual verdict: **PASS**.

## Committed path set and self-review

Commit subject: `feat: freeze Dororong body outline authority`

The exact five-path allowlist is:

1. `tools/New-BodyRegionMask.ps1`
2. `src/Dororong.App/Assets/dororong-body-region-mask.png`
3. `tests/Dororong.App.BodyMask.Tests.ps1`
4. `tests/fixtures/dororong-body-outline-authority.psd1`
5. `artifacts/work-reports/dororong-source-silhouette-outline-task-report.md`

Self-review confirmed that the canonical source hash remains pinned, the
generator validates source hash/dimensions and the largest fixed-seed
near-white component, the final writer uses literal reviewed source-row runs,
the fixture is test-only and independent, and no reconstructed candidate was
created or viewed. Existing manual-attempt files remained unmodified and
unstaged. No existing generator, native runtime asset, exact-art test,
README/TASKS/spec/plan, or manual-attempt record was edited.

## Concern / downstream boundary

Task 1 authority generation is complete. Per the brief, Task 2 remains blocked
until a fresh reviewer approves the mask overlay, fixture independence, and
protected-part coverage; no reviewer was dispatched in this task.
