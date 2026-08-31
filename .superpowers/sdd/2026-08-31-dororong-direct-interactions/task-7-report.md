# Task 7 report — provisional body-drag family

- Status: `DONE_WITH_CONCERNS`
- Acceptance state: `PROVISIONAL / UNVERIFIED`; this report does not claim root or user approval.
- Implementation/artifact commit: `c8a136b` (`art: stage provisional body drag frames`)
- Branch/worktree: `feature/dororong-m1-expression-animation` in `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation`
- Authority: `task-7-brief.md`; `docs/specs/2026-08-31-dororong-direct-interaction-design.md` sections 2, 3.4, 5.1–5.5, 6.2, and 8.2; `docs/specs/2026-08-26-dororong-m1-design.md` canonical identity section.

## Files and exact paths

Product-staged source frames:

- `src/Dororong.App/Assets/frame-sources/body-drag-entry-00-press.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-01-lengthen.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-02-drop.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-03-stretch.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-04-dangle.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-05-near-hang.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-entry-06-hang.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-settle-00-hang.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-settle-01-lift.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-settle-02-gather.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-settle-03-land.png`
- `src/Dororong.App/Assets/frame-sources/body-drag-settle-04-recover.png`

Product-staged runtime frames use the same twelve names directly under `src/Dororong.App/Assets/`. Every source/runtime pair is byte-identical. `src/Dororong.App/Dororong.App.csproj` packages the twelve runtime files as WPF `Resource` items.

Test:

- `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`

Exact candidate path:

- `artifacts/candidates/direct-interactions/body-drag-v1/attempt-1/`
- Generated references: `generated-sheet-attempt-1.png`, `generated-sheet-attempt-2.png`
- Reproducible candidate generator: `Build-BodyDragCandidate.ps1`
- Exact staged candidate frames: `frames/` with the twelve product names

Exact verification path:

- `artifacts/verification/direct-interactions/body-drag-v1/attempt-1/`
- Native strip: `body-drag-native-strip.png`
- Nearest-neighbor 4× strip on RGB `(18,20,28)`: `body-drag-nearest-4x-strip.png`
- Onion-skin evidence: `body-drag-onion-skin-strip.png`
- Neighbor-difference evidence: `body-drag-difference-strip.png`
- Normal-speed entry/hold/release playback: `body-drag-entry-hold-release-800ms.gif`
- Machine-readable provenance: `body-drag-metrics.json`

The pre-existing stat-only `src/Dororong.App/Controls/DororongPresenter.xaml` modification was neither edited nor staged. No running application process was stopped or relaunched.

## Tool mode and exact prompts

Mode: built-in `image_gen`, using the canonical runtime PNG and durable user sketch as local image references. The first call was an identity-preserving generation. One bounded targeted edit followed to correct leg count. No CLI/API fallback was used.

Initial prompt:

```text
Use case: identity-preserve
Asset type: provisional 2D desktop-pet animation key-pose contact sheet
Primary request: Create one coherent sprite contact sheet containing exactly twelve ordered, complete-character poses of Dororong transitioning into and out of a cat-like scruff-held body hang. Arrange as a clean 4-column by 3-row grid, read left-to-right then top-to-bottom: 1 press, 2 slight lengthen, 3 drop, 4 stretch, 5 dangle, 6 near-hang, 7 full hang, 8 same full hang, 9 lift, 10 gather, 11 land, 12 exact visual recovery. Use Image 2's first sketched figure only for the long downward silhouette and fixed grab-point idea.
Input images: Image 1 is the exact canonical identity and style authority; Image 2 is silhouette/interaction intent only.
Scene/backdrop: genuinely transparent background in every cell; no checkerboard and no colored backing.
Subject: exactly one complete Dororong per cell. The head/top stays fixed at the same grab anchor. The torso extends progressively downward. At hang, exactly four clearly separated legs hang downward, with no tail.
Style/medium: match Image 1's small clean hand-drawn anime desktop-pet raster illustration, palette, line weight, shading, proportions, and transparency.
Composition/framing: every character centered consistently in an equal square cell with generous clear separation; same scale and head position in all cells; no cropping or overlap; no labels, dividers, numbers, captions, or text.
Constraints: preserve the same rounded pink hair/head silhouette, paired purple eyes, tiny centered mouth, right-side rose, bow, white ribbons, outline character, ornament connectivity and layer order from Image 1. Do not redesign the face, hair, rose, bow, ribbons, or expression. Entry and release must be sequential neighboring edits with continuous body mass and leg paths. Every cell must show a complete character, never an overlay or isolated body part.
Avoid: tail; extra or missing limbs; merged legs; anatomy changes between cells; head/grab-anchor jumps; ornament redraws; broken outline; smears; alpha fringes; ghosts; motion blur; duplicated characters within a cell; guide marks; red sketch marks; text; watermark.
```

Bounded one-edit prompt:

```text
Use case: precise-object-edit
Asset type: provisional 2D desktop-pet animation key-pose contact sheet
Primary request: Change only the leg anatomy in the twelve-pose sheet: every pose from lengthening through hanging and gathering must show exactly four clean, countable, downward legs with three transparent valleys between them. Keep the four legs attached to one continuous torso and make their changes continuous from cell to neighboring cell.
Constraints: preserve the existing 4-by-3 layout, cell order, character scale, fixed head position, face, pink hair, purple eyes, tiny mouth, rose, bow, white ribbons, outline, palette, pose progression, and genuinely transparent background exactly as they are. Do not redraw or move the head or ornaments. No labels, dividers, numbers, captions, text, or watermark.
Avoid: two-leg or three-leg poses; merged legs; fifth limb; tail; leg swapping; body smears; broken outline; checkerboard baked into pixels; any change outside the lower body and legs.
```

The initial sheet kept the broad Dororong read but contained merged/two/three-leg hanging cells. The one allowed edit corrected the visible sheet to four legs. Both built-in results nevertheless contain an opaque light checkerboard, so neither generated sheet was promoted directly. The provisional product frames use the revised sheet only as the silhouette direction; `Build-BodyDragCandidate.ps1` reconstructs the long lower body and copies the exact canonical head/face/hair/ornament pixels back into every authored frame. This preserves identity and produces genuine transparent product files, but it is a material concern against a literal “extract the sheet” reading.

## TDD record

Named break: a missing or malformed body-drag family, source/runtime divergence, changed protected identity, moving top anchor, discontinuous downward extension/recovery, missing/merged hanging legs, tail-like right protrusion, dirty transparent RGB, missing WPF resource packaging, or non-canonical final recovery.

First test invocation exposed a test-harness XML traversal error before reaching the intended contract:

```powershell
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1
```

```text
exit 1
The property 'Resource' cannot be found on this object.
```

The XML query was corrected to select actual `Resource` nodes. Intended RED, before any body-drag product files existed:

```powershell
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1
```

```text
exit 1
Missing runtime body-drag key: body-drag-entry-00-press.png
```

GREEN after implementation and readback fixes:

```powershell
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1
```

```text
exit 0
DIRECT INTERACTION ASSETS PASS: 12 complete 96x96 transparent body-drag keys, fixed protected identity, continuous extension/recovery, four separated hang legs, no tail protrusion, clean alpha, source/runtime parity, and exact canonical recovery passed.
```

The unchanged GREEN test caught and caused correction of an actual semi-transparent-pixel defect: decoding/resaving the press frame changed canonical pixel `(29,24)`. The generator now byte-copies both canonical endpoint frames and reads every saved PNG back before packaging evidence.

Additional verification:

```text
dotnet test DororongDesktopPet.sln --configuration Release --no-build
Core: 82/82 passed; App: 51/51 passed.

pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
166 assertions passed.

dotnet build DororongDesktopPet.sln --configuration Release --no-restore
Build succeeded; 0 warnings; 0 errors.

git diff --check
exit 0 (only existing LF-to-CRLF warnings for the pre-existing XAML and touched csproj working files).
```

## Visual-check record

Overall verdict: `UNVERIFIED`. The exact product family is implemented and inspected as a provisional asset family. Root review and user acceptance are intentionally outstanding, and Task 8 runtime integration/actual-Windows behavior is outside this task.

Frozen source-first identity observables:

1. rounded pink hair/head silhouette;
2. paired purple eyes and small centered mouth;
3. right-side rose, purple bow, and white ribbon stack in the same order/connectivity;
4. hair overlapping the body at the neck;
5. no-tail lower silhouette.

| Check | Source | Expected observable | Exact observation | Result |
|---|---|---|---|---|
| Complete keys / alpha | Direct-interaction spec 5.1 and 8.2 | Twelve complete 96×96 transparent images, no overlays or transparent RGB fringe | All source/runtime files are 96×96; every frame contains 3,371–3,999 alpha-positive pixels; transparent RGB count is zero; source/runtime hashes match | PASS |
| Fixed head/grab anchor | Spec 3.4 and 5.2 | Head/top remains at the original anchor | Alpha top is row 24 for all twelve frames; protected rows 0–54 are pixel-exact canonical in every frame | PASS |
| Protected identity | Spec 2, 5.1, 5.2 | Face, hair, rose, bow, ribbons, and outline do not redraw | Native and 4× inspection shows one unchanged head/face/ornament stack. Automated readback proves exact rows 0–54 and exact right ornament area `x=64..95, y=55..76` | PASS |
| Downward extension | Spec 3.4 and 6.2 | Torso lengthens continuously after press | Entry alpha bottoms are `88,89,90,92,94,95,95`; no adjacent bottom change exceeds 2 rows; native and onion strips show one downward path | PASS |
| Four legs | Brief Step 1; spec 3.4 and 5.2 | Four stable/countable downward legs | Every lengthen-through-hang and non-final settle frame has alpha-positive leg centers near `x=31,44,57,73` and transparent valleys at `x=38,51,65` on the lower readback row; native/4× strips show four legs | PASS |
| No tail / clean contour | Spec 2, 5.2, and 8.2 | No tail, extra limb, broken outline, or detached protrusion | Max opaque X is 76 canonical and 80–81 during deformation, below the test boundary of 84; native/4× inspection shows no separate tail or fifth limb; difference strip confines change to the lower body plus permitted connecting contour | PASS |
| Neighbor continuity | Spec 5.2 | No one-frame anatomy jump or identical accidental key | Internal entry neighbor changes are `474,563,609,594,358` pixels; settle internal changes are `594,633,571`; each adjacent authored key is distinct except the intentional entry-hang/settle-hang hold reuse | PASS internally; CONCERN at endpoints |
| Release to canonical | Spec 3.4 and 6.2 | Continuous settle returns to wakeful baseline | Settle bottoms are `95,94,92,90,88`; final runtime/source/candidate hash is exact canonical `699348…9A78` | PASS |
| Normal-speed package | Spec 5.5 and 8.2 | Entry/hold/release playback with positive production-intent timing | GIF decodes as 12 frames with delays `5,5,5,5,5,5,8,16,6,6,6,8` centiseconds, total 800 ms, loop block present. Exact still sequence was inspected; root/user motion-feel verdict remains pending | UNVERIFIED for subjective motion feel |
| User approval | Spec 5.5 | User approves this exact strip/playback | No approval was requested or inferred in this branch-only overnight run | UNVERIFIED boundary |

The larger endpoint topology changes are 1,135 changed pixels from canonical press to first four-leg lengthen and 1,188 from final four-leg land to exact canonical recovery; internal neighbors range 358–633. The shapes remain bounded and ordered, but root review should specifically judge whether these endpoint transitions pop at 800 ms playback.

## SHA-256 binding

The candidate frame, product source frame, and product runtime frame share each listed hash:

| Key | SHA-256 |
|---|---|
| entry-00-press | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |
| entry-01-lengthen | `AFAB5A6E3224003937A5E0D130DA93DDA0A29C847720F2EB47E4F46BBAAE8782` |
| entry-02-drop | `E482F791BACD6EA8168E1A0CEA6081982C02B161C40CD543C13567424EA8CFA9` |
| entry-03-stretch | `40F28DE4AA4F5899295420ED6B17620E276D05DEC8D9D8FEDCF55C1E7F923F43` |
| entry-04-dangle | `FEA4EDBC9C177DB50BCF3110B2DB0AA50BA43A865554947096B1AC9907C78EB4` |
| entry-05-near-hang | `F8A5A773A014A2E37DEFAE2C1A231EB9C5858D45A63928B63ECEDCFBC4489936` |
| entry-06-hang | `7716E8684BB04A7FEB56BD3CBCEF5AE7E81FEC67D4BD240B5B8892C52E5A409E` |
| settle-00-hang | `7716E8684BB04A7FEB56BD3CBCEF5AE7E81FEC67D4BD240B5B8892C52E5A409E` |
| settle-01-lift | `30E32D846DE18E6612DFB2A71DE2247627263A5EE35747666B29CABC34362857` |
| settle-02-gather | `40F28DE4AA4F5899295420ED6B17620E276D05DEC8D9D8FEDCF55C1E7F923F43` |
| settle-03-land | `20D139FDE3368185056C6E7BA98547DEDAC4E19BEBB8F6F3E84161C113FF7991` |
| settle-04-recover | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |

Reference/evidence hashes:

| Artifact | SHA-256 |
|---|---|
| generated sheet attempt 1 | `E59E308F550CBF847B857E00621E0C9DA0D60931317E254C92263FFC34608933` |
| generated sheet attempt 2 | `A01188C35421680EF80FDCD14D36486FF36806B38E928FDEB7920B2FC88F445F` |
| native strip | `E292CD3979D08A780E7CB51531E1A5A635DCA213F1FCBA1ABFC4433CB3FAB0BE` |
| nearest 4× strip | `A24354BAB83B7343030B36C993DDEDDD84110993EF21EE5025C47827EF08EBDE` |
| onion-skin strip | `9A790354E5BBAEF1D02F8D4CF2FD0695328096FB12A8DE8ECA1B09D04A20CC31` |
| difference strip | `3EC67A904E168CD11BABCFEEE4CB754CEE0CF48D05C5FA6940948C5F935E9402` |
| 800 ms playback | `16DA8D7E6B415D7122784FDC95A2083E81D91CC002B83890C7735728DD00512B` |
| metrics JSON | `44AC454D47837673116558C81F2012BED7DA8E99BFC066556817CD8B4D9C2A2E` |

## Concerns and self-review

- This is deliberately not called approved. Root review and user approval of the exact native/4×/800 ms package remain outstanding.
- The image generator failed true transparency in both contact sheets and changed identity/scale between cells; direct sheet extraction would have violated the authoritative asset contract. The deterministic post-process preserves the canonical identity but means the product bytes are guided by, not literal crops from, the generated sheet.
- The lower body is intentionally simple and sketch-like. It reads clearly as a long scruff hang at 96 px, but the canonical-to-four-leg topology change is larger than internal changes and may pop in normal-speed runtime interpolation. Task 8 should not present this as accepted art unless root/user review explicitly clears that motion.
- The 96 px frame can extend only from canonical bottom row 88 to row 95, a seven-pixel absolute bound extension. Most of the long-body read therefore comes from redistributing white body mass upward-to-downward inside the fixed frame, not from increasing the window size.
- The normal-speed GIF is mechanically verified as 12 frames/800 ms and its still sequence was inspected, but subjective motion feel remains `UNVERIFIED` for root/user observation.
- Self-review confirmed only Task 7 files were committed. The unrelated presenter XAML working-tree modification remains unstaged.

---

## Fix round 1 — immutable attempt 2

Status: `PROVISIONAL / UNVERIFIED`. This round fixes the reviewer-observed endpoint topology jump and rectangular/comb-like lower body without claiming root or user approval. Product staging remains branch-only; there was no push, merge, app stop, or app relaunch.

Implementation commit: `86386e9` (`art: smooth provisional body drag endpoints`).

### Files and exact paths

- Strengthened regression contract: `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`.
- New immutable candidate package: `artifacts/candidates/direct-interactions/body-drag-v1/attempt-2/`.
- Candidate generator: `artifacts/candidates/direct-interactions/body-drag-v1/attempt-2/Build-BodyDragCandidate.ps1`.
- Candidate frames: `artifacts/candidates/direct-interactions/body-drag-v1/attempt-2/frames/` (twelve complete 96×96 PNGs).
- New verification package: `artifacts/verification/direct-interactions/body-drag-v1/attempt-2/`.
- Product runtime paths: `src/Dororong.App/Assets/body-drag-*.png`.
- Product source paths: `src/Dororong.App/Assets/frame-sources/body-drag-*.png`.
- The product names did not change, so `src/Dororong.App/Dororong.App.csproj` required no edit.
- Attempt 1 was not overwritten: `git diff --quiet HEAD^ -- artifacts/candidates/direct-interactions/body-drag-v1/attempt-1 artifacts/verification/direct-interactions/body-drag-v1/attempt-1` returned exit `0` after the implementation commit.

### Prompt and tool mode

No new image-generation call was made. The existing generated sheet `artifacts/candidates/direct-interactions/body-drag-v1/attempt-1/generated-sheet-attempt-2.png` (`A01188C35421680EF80FDCD14D36486FF36806B38E928FDEB7920B2FC88F445F`) remained a silhouette reference. Attempt 2 was authored locally and deterministically with PowerShell/System.Drawing by isolating the accepted canonical body through `src/Dororong.App/Assets/dororong-body-region-mask.png`, applying a top-anchored progressive vertical warp, shifting the four original rounded lower lobes by different amounts, and opening three rounded U valleys. Protected head/face/hair/rose/bow/ribbon areas are then copied back pixel-exactly from canonical. This method preserves identity more aggressively than another generative edit.

Warp progress is `0, 0.25, 0.40, 0.55, 0.70, 0.85, 1.0, 1.0, 0.84, 0.60, 0.25, 0`; full-hang leg target bottoms are intentionally unequal. Product files are promoted only after the complete candidate and verification package exists.

### TDD RED and GREEN evidence

The asset test was strengthened before attempt-2 product generation. It now checks both full-pixel and silhouette endpoint deltas (`<=750` and `<=300`), all non-hold neighbor deltas (`<=850`), four separated opaque runs, at least three distinct full-hang leg bottoms with no more than four rows of spread, and at most three rows of per-leg movement between checked neighbors.

RED against attempt 1:

```text
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1
Exception: ... First changed entry key replaces too much of the rounded canonical body: 1135 pixels.
exit 1
```

Attempt-1 diagnostic binding: entry-00→entry-01 was `1135` full changed pixels / `488` silhouette pixels; settle-03→settle-04 was `1188` / `536`.

GREEN against attempt 2:

```text
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1
DIRECT INTERACTION ASSETS PASS: 12 complete 96x96 transparent body-drag keys, fixed protected identity, continuous extension/recovery, four separated hang legs, no tail protrusion, clean alpha, source/runtime parity, and exact canonical recovery passed.
exit 0
```

Fresh covering checks:

```text
dotnet test DororongDesktopPet.sln --configuration Release --no-build
Core: 82 passed, 0 failed, 0 skipped.
App: 51 passed, 0 failed, 0 skipped.

pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
DIRECT INTERACTION RENDER PASS: 166 assertions covered exact accepted sources, one opaque surface, pending compression, continuous 500ms hop, and exact rest recovery.

dotnet build DororongDesktopPet.sln --configuration Release --no-restore
Build succeeded; 0 warnings; 0 errors.

git diff --cached --check
exit 0
```

### Measured continuity and timing

Full changed/silhouette-changed pixels for the exact non-hold sequence are:

| Neighbor | Full | Silhouette |
|---|---:|---:|
| entry-00 → entry-01 | 615 | 166 |
| entry-01 → entry-02 | 590 | 114 |
| entry-02 → entry-03 | 629 | 117 |
| entry-03 → entry-04 | 688 | 130 |
| entry-04 → entry-05 | 764 | 132 |
| entry-05 → entry-06 | 820 | 119 |
| settle-00 → settle-01 | 832 | 128 |
| settle-01 → settle-02 | 842 | 192 |
| settle-02 → settle-03 | 810 | 280 |
| settle-03 → settle-04 | 615 | 166 |

The full-hang leg bottoms read `95,94,92,91`: four non-parallel tips, four rows maximum spread, and four distinct lengths. The normal-speed GIF decodes as twelve frames in the expected neighbor order with positive delays `5,5,5,5,5,5,8,16,6,6,6,8` centiseconds, totaling exactly `800 ms`. Encoded timing and sequence are verified; subjective motion feel remains explicitly `UNVERIFIED` pending root/user runtime observation.

### Visual-check observations

Sources inspected at native scale and nearest-neighbor 4× were the attempt-2 native strip, 4× strip, onion strip, difference strip, and 800 ms playback.

| Invariant | Exact observation | Result |
|---|---|---|
| Complete character / protected identity | Every key is a complete sprite. The canonical upper/head/face/hair/rose/bow/ribbons remain visually fixed; automated protected-pixel checks are exact. | PASS |
| Slight first extension | Entry-01 retains the canonical rounded torso read and changes `615/166`, rather than attempt 1's replacement-like `1135/488`. | PASS |
| Rounded continuous contour | Native and 4× strips show the original body mass lengthening downward through one contour family. Onion and difference strips confine motion to the permitted lower-body connection and show no detached paws or horizontal torso seam. | PASS |
| Four countable legs / no comb | Three U-shaped valleys open gradually; four lower lobes remain countable. Tip rows `95,94,92,91` and visibly different widths/lengths avoid a parallel baseline. | PASS |
| No tail / no fifth limb | No separate right-side protrusion appears in native or 4× inspection; the existing ornament/ribbon remains canonical. | PASS |
| Gradual settle and exact recovery | Settle mirrors the same rounded contour family, lands at the same slight-extension key, then recovers byte-exactly to canonical. The final transition is `615/166`, not attempt 1's `1188/536`. | PASS |
| Playback feel | Frame order and positive 800 ms timing are proven and the GIF was inspected. Runtime motion feel has not been approved by root/user. | UNVERIFIED |

### SHA-256 binding

Candidate, product source, and product runtime copies share these hashes:

| Key | SHA-256 |
|---|---|
| entry-00-press | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |
| entry-01-lengthen | `862C1EA03B2368FBF2163F0C07E3B3A717389A6580EF2C282F65B86EA99B5879` |
| entry-02-drop | `0DDC8554E1E8F589C6A83230712B30160A38524C5E25773788D13C195E3519EE` |
| entry-03-stretch | `319311A9452C188DB4E1BD7F91C6FDD78FB6812FACE227CB2A9DF1743F9EE8F3` |
| entry-04-dangle | `4619F665B8AD71C4D629A7D5B43F8362110CD6ECB38F7E3A57E68E290F5472C9` |
| entry-05-near-hang | `C28E4E6E4E6C6060885C26E3D807D3CE77A2D563EE5F94477C21D984080BE4EA` |
| entry-06-hang | `6F9968FE05EA9E92D657ACD9E0B74333C408493D811A2F42CAE3707D53DCE94A` |
| settle-00-hang | `6F9968FE05EA9E92D657ACD9E0B74333C408493D811A2F42CAE3707D53DCE94A` |
| settle-01-lift | `BAA74DE388AE97E2A125C9E1B0AC4A9F4C7CC74E3A75C50F1CEB185045316C24` |
| settle-02-gather | `4BCEE1ED2480BB9D3083A122C57BF7F585596376E5749AC8E5535A901C2C40D3` |
| settle-03-land | `862C1EA03B2368FBF2163F0C07E3B3A717389A6580EF2C282F65B86EA99B5879` |
| settle-04-recover | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |

Verification artifacts:

| Artifact | SHA-256 |
|---|---|
| difference strip | `8F0C3B0E4D67A4E0C3F0B380641BD8BD0995C5C2CC6E32C203FBFE8322D50B83` |
| 800 ms playback | `1E0816EE72674A860D81F853B2C8E3A7FB19CD7B5B4E20021319151AFF9BC85E` |
| metrics JSON | `57D32D0685E65679F995AFA0CBC1389F7937F58E9C282280D0F8B5B8FE685A08` |
| native strip | `F244B7465A9CAF1D9EF116023BB5E14E75D099F9304671AB17C444DFDECD2CE4` |
| nearest 4× strip | `3229FB5E4547A3860EFA463A087CD06DF4E08C66B94110023446E926F3D3D3D3` |
| onion strip | `E15D6D0CA55F229DAD9A353ED798C9CB6A6059B50D5B3BED3478AE5AF5777A37` |

### Concerns and self-review

- The exact attempt-2 package is still provisional and not root/user approved. Motion feel remains runtime-unverified even though encoded timing and sequence are verified.
- The 96×96 canvas still limits absolute downward travel to the bottom edge; this family expresses hang primarily by reshaping canonical lower-body mass within that fixed frame.
- Attempt 2 intentionally reuses the existing generated silhouette reference but not its changed identity or opaque contact-sheet background; deterministic canonical-mask warping is the final raster method.
- Self-review confirmed attempt 1 remains intact, product/source bytes match, accepted canonical/sleep/click assets were not overwritten, no app process was touched, and the pre-existing `src/Dororong.App/Controls/DororongPresenter.xaml` modification remains unstaged.
