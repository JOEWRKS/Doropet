# Dororong native-96 runtime-art task report

## Outcome

**Repository/runtime-art PASS.** The exact 225x225 canonical source remains the byte-identical authority, while the committed open and closed runtime frames are now deterministic 96x96, 32bpp ARGB assets presented one-to-one at 96x96 DIPs on the current 96-DPI target. Body normalization is confined to a final-resolution body band and produces one continuous hair-comparable contour with identical corrections in both eye states.

- Chosen direction: retain source-scale transparency and reviewed eye derivation, then downsample once through a premultiplied-alpha-aware path before any body correction.
- Material delta: replace the 225px production/body-fixture contract with a hand-owned native-96 body band and continuous 1.35px optical contour, while preserving alpha and all RGB outside the band.
- Implementation constraint: the one-device-pixel claim applies only to the current Windows 96-DPI / 100%-scale target; other DPI/scaling targets are not verified by this task.

Actual-Windows manual attempt 4 remains **FAIL**. A new actual-Windows attempt 5 and user acceptance remain **UNVERIFIED**; repository and presenter evidence do not promote either boundary.

## Scope and implementation

- Base commit: `549521292aca4bcf71ac5f698e718ff7b5d9e378`.
- Source authority: `src/Dororong.App/Assets/dororong-canonical-source.png`, 225x225, 45,681 bytes, SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- The generator performs the existing boundary-connected near-white transparency conversion at 225x225.
- The reviewed left/right eye stencils, bilinear face fill, and 38+38 source-scale lid pixels derive the closed frame at 225x225 before resizing. They were not redesigned.
- Both transparent 225px frames are resized to 96x96 through one explicitly configured `Format32bppPArgb`, high-quality bicubic, `SourceCopy`, half-pixel, `TileFlipXY` path. The result is copied to 32bpp ARGB and alpha-zero RGB is forced to zero.
- At 96px only, one continuous cubic contour covers the exposed front leg, undersides/valleys, center leg, rear leg, and rear rim. A 3.5px round edit band clears prior ink to the brightest nearby opaque body fill from the uncorrected frame. A 1.35px round-join/round-cap stroke is rasterized through deterministic 4x coverage sampling; sub-0.16 alpha-adjusted fringe coverage is cleared so ordinary curves never form a third supported physical pixel.
- The same correction is applied independently to the open and closed downsample baselines and produces byte-identical body RGB at the same 387 coordinates.
- `DororongPresenter` keeps the 108x96 `BodyGroup` and all state transforms unchanged. Its image is 96x96 DIPs at `Canvas.Left=6`, so the native resource is centered without another 225-to-96 bitmap resample.
- No behavior/core code, motion, state decision, input/hit-test implementation, window behavior, hair/head/face/mouth, rose, bow, ribbons, no-tail geometry, or layer ordering changed.
- Root-owned untracked `docs/verification/*manual-attempt*.md` evidence was not edited or staged.

## TDD evidence

The real exact-art test runs `Generate-CanonicalArt.ps1` into unique temporary output and baseline directories, compares generated and committed hashes, inspects the real generated bitmaps, builds fixed native profiles, and instantiates the real WPF presenter.

1. Required clean-base RED: before generator or asset implementation, `pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release` exited `1` against base `5495212`: `The runtime production frame is not native 96px wide. Expected '96', observed '225'.`
2. First native candidate retained a visibly light/dotted body rhythm. The fixed hair references measured widths `1.236` and `1.793`; its body median was `0.923524659746252`. The added regression exited `1`: `The body stroke rhythm is materially lighter/heavier than fixed clean hair` against the frozen `>=0.95` median floor.
3. A direct native GDI+ width change from `1.25` to `1.50` produced byte-identical assets, proving that fractional pen coverage was quantized by that raster mechanism rather than correcting the visual defect.
4. Deterministic 4x coverage sampling made fractional width observable. A 1.50px supersampled stroke correctly failed the existing non-joint knot gate; reducing the optical width to 1.35px removed the heavy ordinary-curve support.
5. The remaining center-foot/rear-outer third support was a low-alpha fringe (`0.123, 0.907, 0.279` at the rear-outer normal). Applying the coverage floor after multiplying by preserved source alpha removed that fringe without weakening the test or changing the contour.
6. Final GREEN: focused exact-art exited `0`; all fixed profiles, connected-component, protection, alpha, eye, hash, presenter, hit-test, and state checks passed.

The production mutations protected by the test are: a stale generated asset; a non-96 runtime dimension; source drift; a different resize or alpha-zero fringe; any alpha change; RGB changes outside the independent body protection band; a missing/different eye-state body correction; a faint, broken, dotted, disconnected, isolated, or over-heavy body contour; a three-pixel ordinary-curve knot; excessive joint mass; open-eye remnants; missing lids; mouth changes; `#FADCE0`; wrong WPF size/DPI arrangement; broken native-coordinate alpha hit testing; and wrong state-to-frame mapping.

## Exact artifact and pixel evidence

| Artifact | Dimensions / format | Bytes | SHA-256 |
|---|---|---:|---|
| `dororong-canonical-source.png` | 225x225 authority | 45,681 | `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504` |
| `dororong-canonical.png` | 96x96, 32bpp ARGB | 9,890 | `21647C6D5842C5E6B9D5F98E11B48FA68CF5DBD7B42B798641478FC897765761` |
| `dororong-closed-eyes.png` | 96x96, 32bpp ARGB | 9,856 | `C89F891BBC4161FA9ADC2252E9BBEF9315EC7A6ADBCB883CA34DCC6B1F1CBE7F` |

- Open body correction: 387 pixels, bounds `x=17..76,y=58..87`.
- Closed body correction: 387 pixels, same coordinate mask and same corrected RGB at every body coordinate.
- Body correction mask mismatch: 0.
- Open/closed alpha differences: 0.
- Alpha changes against each uncorrected 96px baseline: 0.
- Corrected RGB changes outside the independent native-96 protection band: 0.
- Alpha-zero pixels with nonzero RGB: 0 in both frames.
- Open/closed eye-state RGB differences: 308 pixels, bounds `x=16..47,y=47..62`; changes remain inside the two scaled eye regions plus fixed bicubic support.
- Fixed light/dark body profiles: 13 normals per background, covering both rear-rim levels, every exposed leg side, all foot bottoms, undersides/valleys, and joints.
- Final body profile width range: `0.824..1.380`; peak range: `0.748..0.911`; every profile has one support run.
- Every non-joint profile has at most two physical support pixels. Joint widths remain no more than 1.35 times the clean body median.
- Visible corrected body support is one 8-connected component with no isolated dark component or knot.

## Visual-check record

Claim: the exact repository open/closed hashes above preserve the approved Dororong identity and reviewed closed-eye state while replacing the failed scaled body line with a continuous, uniform native-96 contour on the named 96-DPI presenter target.

Sources: the approved 225px source and SHA above; `artifacts/work-reports/dororong-runtime96-task-brief.md`; the retained attempt-4 failure boundary in `artifacts/work-reports/dororong-body-uniformity-task-report.md`; and the real `DororongPresenter` state mapping/layout.

| Check | Expected observable | Exact observed fact | Verdict |
|---|---|---|---|
| Native open on white | Dororong remains unaidedly readable; body line is continuous and hair-comparable without faint spans or knots. | The exact 96px open hash showed one continuous dark body rhythm from front leg through rear rim. No span vanished; ordinary curves did not show a 2–3px dark mass; hair/body balance was stable. | PASS |
| Native open on dark | Alpha/silhouette and internal body rhythm remain intact without white/gray fringe contamination. | The exact open hash retained a clean transparent silhouette; no white/gray alpha-zero halo appeared, and the leg/valley/rear-rim contour remained continuous against RGB `(24,24,28)`. | PASS |
| Native closed on white/dark | Both eyes read as integrated closed lids; no detached patch, lower oval, mouth/accessory damage, or body-state mismatch. | Both lids were visible and centered; open-eye lower ovals were absent; mouth, hair, rose, bow, and ribbons stayed intact. The body contour matched the open frame on both backgrounds. | PASS |
| Nearest-neighbor open/closed enlargement | Pixel rhythm has no broken/dotted span, isolated dark island, or ordinary-curve three-pixel knot. | The enlarged exact files showed a one-to-two-support-pixel antialiased rhythm around all legs, bottoms, valleys, joints, and rear rim. No detached dark island or heavy ordinary-curve block was visible. | PASS |
| Real WPF open state at 96 DPI | Idle presents the exact 96px resource at 96x96 DIPs without a second bitmap resize. | A fresh Release build rendered real `DororongPresenter` Idle at 144x144/96 DPI. The resource was centered at presenter coordinates `(24,24)`, remained crisp and undamaged, and the automated layout check observed 96 physical pixels, 96 DIPs, and DPI scale 1.0. | PASS |
| Real WPF closed state at 96 DPI | Sleep uses the closed resource through actual state transforms without eye/body damage. | The real Sleep render visibly retained both integrated lids, mouth/accessories, and the continuous body contour under the intended 0.82 Y-scale and +8 DIP translation. | PASS |
| Actual-Windows attempt 5 / user acceptance | A new application attempt confirms the runtime result in the user-observed environment. | No attempt 5 or user verdict was performed in this repository task. Attempt 4 remains FAIL. | UNVERIFIED |

Overall repository/native-96/presenter visual verdict: **PASS** for the exact hashes and 96-DPI target above. Actual-Windows attempt 5, user acceptance, and non-96-DPI rendering remain **UNVERIFIED**.

## Verification

All commands ran in `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1` against the final files:

| Check | Result |
|---|---|
| `pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release` | exit 0; native-96 hash, protection, profiles, connected contour, eye semantics, presenter, alpha-hit-test, and state checks passed |
| `dotnet test DororongDesktopPet.sln --configuration Release --no-restore` | exit 0; 78 passed, 0 failed, 0 skipped |
| `dotnet build DororongDesktopPet.sln --configuration Release --no-restore` | exit 0; 0 warnings, 0 errors |
| `pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release` | exit 0; real startup/PetLoop composition passed |
| `pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release` | exit 0; center, symmetric, and clamp angles passed |
| documented framework-dependent `dotnet publish ... --runtime win-x64 --self-contained false --output artifacts/publish/win-x64` | exit 0; `Dororong.App.exe`, 150,016 bytes, SHA-256 `8116153A822AC44BD73E809E9300606F5EAD69B5981BC9159D2D83F18123C5D2` |

## Remaining boundaries

- This task does not change the retained actual-Windows attempt-4 FAIL.
- Actual-Windows attempt 5 and user acceptance are still required before claiming the user-visible defect fixed or M1 accepted.
- The one-to-one presenter result is verified only at 96 DPI / 100% scale; other Windows scaling configurations remain unverified.
- The publish result proves packaging only; it does not upgrade actual-Windows visual acceptance.
