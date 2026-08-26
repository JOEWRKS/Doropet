# Dororong body-stroke and closed-eye correction report

## Outcome

The exact 225x225 production assets now use an explicit 185-pixel body-outline fixture to reduce the audited rear-rim and clean leg-side dark cores to two pixels, and the sleep/blink frame uses explicit left/right eye stencils with source-derived face gradients and centered two-pixel closed lids. The pinned source PNG remains byte-identical.

Asset-only visual result: PASS for the two exact repository PNGs at native 225x225 and WPF `HighQuality` 96x96 rendering. Actual-Windows visual acceptance remains UNVERIFIED pending manual attempt 4.

## TDD evidence

- Initial RED: `dotnet build src/Dororong.App/Dororong.App.csproj -c Release` exited 0 with 0 warnings and 0 errors; then `pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release` exited 1 against the old assets because `(99,110)` changed outside the new explicit eye stencil. This proved the old broad closed-eye patch was caught before generator changes.
- Expanded-body RED: after adding the clean leg-side fixture and probes to the real-file test, `pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release` exited 1 against the rear-only intermediate asset: `Expected '185', observed '49'`.
- GREEN: `pwsh -NoProfile -File tools/Generate-CanonicalArt.ps1 -SourcePath src/Dororong.App/Assets/dororong-canonical-source.png -OutputDirectory src/Dororong.App/Assets` and the focused exact-art test both exited 0 after the fixed fixture implementation.
- One experimental row-local eye-color lookup exited 1 at `(86,116)` and, after one evidence-based cutoff adjustment, again at `(85,117)`. That lookup mechanism was abandoned and is not retained. The materially different fixed pinned-anchor bilinear face mapping regenerated successfully and passed the focused check.

## Exact artifacts and pixel counts

| Artifact | SHA-256 |
|---|---|
| `src/Dororong.App/Assets/dororong-canonical-source.png` | `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504` |
| `src/Dororong.App/Assets/dororong-canonical.png` | `F18E1F1C6FE6CDBC073EDBEFCE367E0A17E2A5AA01E92C110931C9E33C2E0DF7` |
| `src/Dororong.App/Assets/dororong-closed-eyes.png` | `4825430B45D7396E97859EB7BDA13B49CB88907B10E3D45984417E89D9CCB71F` |

- Source to transparent production: 33,187 changed pixels total: 33,002 alpha-only boundary-background changes plus exactly 185 RGB body-fixture changes.
- Corrected production to closed-eye frame: exactly 857 changed pixels: 423 in the left stencil and 434 in the right stencil; 76 are changed dark-core lid pixels.
- Production RGB changes outside the exact 185-coordinate body fixture: 0.
- Closed-frame changes outside the two explicit eye stencils: 0.
- Open/closed alpha differences: 0.
- `#FADCE0` pixels in either eye ROI: 0.
- `git diff --exit-code -- src/Dororong.App/Assets/dororong-canonical-source.png` exited 0.

## Visual-check record

Claim scope: asset-only correction of `dororong-canonical.png` and `dororong-closed-eyes.png` at exact 225x225 file rendering and runtime-representative WPF `HighQuality` 96x96 rendering. Sources are the approved source PNG, `artifacts/work-reports/dororong-stroke-eye-correction-brief.md`, and section 8 of `docs/specs/2026-08-26-dororong-m1-design.md`.

| Check | Expected observable | Observed fact | Verdict |
|---|---|---|---|
| Source identity | Outer silhouette, hair/body layer order, no-tail rear profile, ribbon/rose geometry, and open-eye centers remain anchored to the approved source. | The source hash is unchanged; production alpha follows the same boundary mask; RGB differs only at the body fixture. Both final native frames retain the no-tail profile, rose/ribbons, hair geometry, and face placement. | PASS |
| Body dark-core weight | Rear rim and clean leg-side contours read with the roughly two-pixel hair-core weight while the exterior alpha fringe and joints remain intact. | Eleven literal probes report exactly two dark-core pixels. At native size the rear rim and six clean leg sides are visibly lighter and continuous; the 96x96 render retains a connected silhouette without moving the exterior edge. | PASS |
| Closed-eye integration | Closed lids are centered at the original eye locations, remain part of the face, and show no opaque flat-color discs. | Both lids sit within the original left/right eye spans and use two-pixel stepped arcs. The face reconstruction varies through pinned source-derived gradients; `#FADCE0` count is zero. At native and 96x96 sizes the pose reads as closed eyes without detached overlays. | PASS |
| State-family invariants | Open and closed frames retain identical alpha and all non-fixture pixels, including bangs and hair borders. | Pixel audit found 0 alpha differences and 0 closed-frame changes outside the explicit eye stencils; the final native and 96x96 renders preserve hair/bang boundaries and accessory order. | PASS |
| Actual-Windows acceptance boundary | A new rendered application attempt must confirm the correction in the live target before Windows visual acceptance. | No manual attempt 4 was performed in this task. | UNVERIFIED boundary |

## Fresh final verification

All commands ran from `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1` after the final generator write.

| Command | Result |
|---|---|
| `dotnet restore DororongDesktopPet.sln` | exit 0; all projects up to date |
| `dotnet test DororongDesktopPet.sln --configuration Release --no-restore` | exit 0; 78 passed, 0 failed, 0 skipped |
| `dotnet build DororongDesktopPet.sln --configuration Release --no-restore` | exit 0; 0 warnings, 0 errors |
| `pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release` | exit 0; exact-art PASS |
| `pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release` | exit 0; runtime-composition PASS |
| `pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release` | exit 0; dragged-angle PASS |

## Scope and limitations

- `WALK`, `CLICK_REACTION`, presenter transforms, alpha-aware hit testing, and behavior-core logic were not changed.
- The exact source file was not modified.
- The two pre-existing untracked manual-attempt-2 and manual-attempt-3 reports were left untouched and uncommitted.
- This work verifies the repository assets and automated runtime contracts only. It does not upgrade any actual-Windows GUI item or complete Milestone 1; manual attempt 4 is still required.
