# Dororong exact canonical-art implementation report

## Outcome

The exact user-supplied 225×225 canonical Dororong PNG is persisted in the repository, deterministically converted to true-alpha production art by removing only boundary-connected neutral near-white pixels, and presented by the existing 144×144 WPF presenter. The canonical character has no tail; the white shapes behind the rose and bow remain the source ribbons. All non-sleep states use the canonical production frame, while sleep and the IDLE blink use a deterministic frame whose changes are confined to the two eye regions.

Implementation commit: `41e66b4256ec8f4b31741fe1ecf319faa05c4fd2` (`Replace placeholder with canonical Dororong art`).

The pre-existing untracked manual acceptance record at `docs/verification/2026-08-26-m1-windows-acceptance-manual-attempt-2.md` was preserved and was not staged or modified.

## TDD record

Initial focused RED command:

```powershell
pwsh -NoProfile -File tests\Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Observed exit: `1`.

Expected failure:

```text
The persisted canonical source asset is missing.
```

The first implementation run correctly exposed a mistaken test fixture: source coordinate `(112,200)` is exterior white connected to the image boundary, not enclosed body white. The hand-checked enclosed-body fixture was corrected to `(100,175)` without changing production behavior.

The first enlarged visual inspection then exposed neutral near-white fringe pixels around the silhouette. The focused contract's independently defined boundary threshold was tightened from 245 to 225 before production was changed. That regression RED exited `1` with:

```text
Production alpha differs from the boundary-connected background mask at (49,56). Expected '0', observed '255'.
```

After changing the deterministic generator to the same documented near-white floor and regenerating from the unchanged canonical source, the focused GREEN command was:

```powershell
pwsh -NoProfile -File tests\Dororong.App.ExactArt.Tests.ps1 -Configuration Release
```

Observed exit: `0`.

```text
EXACT ART PASS: source identity/dimensions, edge alpha and retained pixels, bounded eyes, presenter state mapping, and BodyGroup placement passed.
```

The test exercises real PNG files and the built WPF assembly. It verifies the canonical SHA-256 and 225×225 dimensions; boundary-connected alpha mask; alpha-zero margin and opaque character/body points; every production pixel's retained source RGB and coordinate; no added geometry; closed-eye changes only inside the two bounded eye regions; canonical versus sleep/blink presenter mapping; and unchanged `BodyGroup` size/placement. The existing dragged-angle script separately preserves center, symmetric, and clamp behavior.

## Fresh final Release verification

Command sequence:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -File tests\Dororong.App.ExactArt.Tests.ps1 -Configuration Release
dotnet test DororongDesktopPet.sln --configuration Release --no-build --no-restore
pwsh -NoProfile -File tests\Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests\Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
```

All commands exited `0`:

- restore: all projects current;
- Release build: 0 warnings, 0 errors;
- exact-art focused contract: PASS;
- core tests: 78 passed, 0 failed, 0 skipped;
- runtime composition: PASS for startup, actual `PetLoop` wiring, input/brain/render ordering, capture, fault, and disposal;
- dragged angle: PASS for center `0`, symmetric `-4/+4`, and clamps `-8/+8`.

`git diff --cached --check` also exited `0` before the implementation commit.

## Canonical asset identity

- `src/Dororong.App/Assets/dororong-canonical-source.png`: SHA-256 `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`.
- `src/Dororong.App/Assets/dororong-canonical.png`: SHA-256 `AA1E5A712958E5ABA09FCD12B47534A48D27D851D6C8CD18D3CA35BF6FF30CA2`.
- `src/Dororong.App/Assets/dororong-closed-eyes.png`: SHA-256 `ADE4599145B426EA395930296C1BC745DD944B38FC832B7E5E3AAEA1C5A61610`.

The generator validates the canonical source hash before writing output. It uses four-neighbor flood fill from all image edges, treats RGB channels of 225 or above as neutral near-white, writes alpha 0 only for that connected exterior, and retains source RGB at every coordinate. The closed-eye frame clones that production frame and modifies only the explicit left/right eye bounds.

## Asset-level visual verification

Claim: the repository production PNGs preserve the approved canonical character identity and only apply the authorized transparency and bounded eye changes. Covered surfaces are the exact source, canonical production frame, and closed-eye production frame at 225×225 native scale and 4× nearest-neighbor inspection scale. Authority is `artifacts/work-reports/dororong-exact-art-brief.md` and the canonical source hash above.

Frozen source observables before candidate inspection:

1. no tail; rounded white body ends in the rear leg rather than a tail;
2. pink bobbed hair surrounds the face on the left/front and overlaps the white body;
3. the upper-right decoration orders rose, purple bow, then white ribbon elements behind it;
4. two large purple eyes, small mouth, white rounded body, and four visible legs define the character mass;
5. the character occupies the lower-left/middle of a square source with exterior white negative space.

Checks:

- Expected: source identity is exact. Observed: file is 225×225 and its SHA-256 is the binding `F96E...6504`. Verdict: PASS.
- Expected: exterior background becomes true alpha without changing the character. Observed: native and 4× inspection show a clean exterior with the earlier neutral fringe removed; exhaustive pixel checks found alpha 0 exactly on the boundary-connected mask and identical retained RGB/coordinates elsewhere. Verdict: PASS.
- Expected: no tail or geometry is added and the source structure is preserved. Observed: no tail appears; pink hair, face, four-leg white body, rose, bow, and white ribbons retain their source order, contact, scale, and orientation. Verdict: PASS.
- Expected: the sleep/blink derivative changes only eye regions. Observed: the eye areas contain two dark closed-eye arcs; exhaustive comparison found every changed pixel inside the named eye bounds and every outside pixel identical to the canonical production frame. Verdict: PASS.

Overall asset-level visual verdict: PASS for the exact files in implementation commit `41e66b4256ec8f4b31741fe1ecf319faa05c4fd2`.

## Files changed

- `src/Dororong.App/Assets/dororong-canonical-source.png`
- `src/Dororong.App/Assets/dororong-canonical.png`
- `src/Dororong.App/Assets/dororong-closed-eyes.png`
- `tools/Generate-CanonicalArt.ps1`
- `src/Dororong.App/Dororong.App.csproj`
- `src/Dororong.App/Controls/DororongPresenter.xaml`
- `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- `tests/Dororong.App.ExactArt.Tests.ps1`
- `README.md`
- `TASKS.md`
- `docs/specs/2026-08-26-dororong-m1-design.md`

## Remaining boundary

No actual application-window capture or coordinate-injection GUI acceptance was performed. Cross-process click-through, focus preservation, motion in the real 144×144 window, and the existing twelve-item Windows acceptance remain UNVERIFIED. The earlier automation limitation is preserved; automated asset, assembly, build, and runtime-composition results do not upgrade those GUI checks or complete Milestone 1.
