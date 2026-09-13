# Cheek spring preview only

V7 preview refinement of the v6 visual study: retain v5 reach2.1x and the separate contour/fill pass,
add gentle grounded torso compression and head/recoil response. NO eye closing.
Keep root width without vertical widening,
snap back quickly with two diminishing rebounds. This supersedes v1's width1.3x
and bounce0.65/perceived400ms study. No product integration, installer, restart
or commit.

Preview.csproj links (does not modify) the current product's four accepted cheek
renderers. Program exports v5 baseline and v6 body-reaction160px padded121pose banks. New
local inverse sampling affects exterior x<12 only, preserves original rows and
the cheek root, leaves the entire selected eye
exact during enlargement, and keeps body/ribbon exact. Negative rebound uses
bounded secondary eye/top-and-side-hair translations. This is a canonical-pose
preview; seated/perched/rotated-head/carry routing needs separate integration.
Raster banks use0.25DIP input steps. No AI image generation or canonical asset edits.

CheekContour reuses the accepted analytic cheek boundary, applies the same hair
and exterior transforms to geometry, then computes signed distance for independent
ink/coverage instead of stretching ink pixels. Source ink(12,54) and skin(18,57)
colours are used only in the exterior x<12/y53..63 patch. Row64 retains the original
lower hair attachment. The patch feathers into
the original root and fades in smoothly; rest and inward rebound remain untouched.
Its ink core is approximately1 source pixel with a subpixel coverage fringe.

CheekReaction is a separate final preview pass. Above source y64 the head/cheek
are translated rigidly, not squashed or given a new eye expression. Full pull
leans2px toward the pulled cheek and lowers the head1px. The y64..88 torso field
smoothly fades to zero, shortening24px height by1px (~4.2%); rows y88+ remain exact.
Negative spring lobes lean opposite the pull (about1.6px on the first recoil).
The inverse continuous field uses premultiplied bilinear sampling, so fractional
poses have normal subpixel filtering; no separate cut-out layers or native motion.

V2 uses a custom exp(-7.5t)*cos(17t) response: first rest crossing about92ms,
larger recoil about160ms and smaller recoil about345ms. A smooth fade from360ms
to440ms prevents a third lobe and restores the exact rest pose. This is not an
exact Anime.js .65/400 spring. Comparison panes share both input and easing,
isolating the additional body reaction at the same pull and accepted cheek contour.
The prototype couples follow offsets to the pose bank; it does not yet preserve
the product's separate held eye55ms/hair90ms filters or implement native carrying.

V7 keeps the v6 raster banks and signed cheek curve unchanged. A release-only
canvas translation makes light-pull recoil readable: the 25% demo adds a 0.6px
native backward pulse (about 1px with the existing head recoil). The final 75–100%
of pull strength smoothly raises this pulse to 2px at maximum. A sine-squared
envelope begins at zero, peaks at150ms and returns to zero at300ms; the existing
two-lobe cheek response settles at440ms. Holding a fresh pull has no pulse.
Both preview scales and mirrored direction use the same native displacement.
Regrab captures the current pose and displacement; another release smoothly
clears that offset while remaining bounded to2px even after repeated regrabs.
This translates the rendered character, including the feet, within the canvas;
it does not move the native pet window or modify eye expression, cheek length,
outline, torso rig or bank dimensions.

## Run

From repository root:
```powershell
dotnet run --project tools/CheekSpringPreview/Preview.csproj -c Release -- --verify
node --test tools/CheekSpringPreview/motion.test.mjs tools/CheekSpringPreview/input.test.mjs
dotnet run --project tools/CheekSpringPreview/Preview.csproj -c Release -- artifacts/repro/cheek-spring-20260913-preview-06
node tools/CheekSpringPreview/server.cjs
```

The server binds127.0.0.1:2801 with an explicit six-file route allowlist, no writes
or external requests. Browser controls: drag horizontally, max hold, release,
light25% demo, maximum demo, mirror, dark background, amount slider. Installed pet is untouched.
V7 reuses preview-06 banks; preview-01..06 banks remain preserved without regeneration. The 32px
padding and dimension-aware browser crop retain native image scale at both sizes.

## Evidence

- V7 browser-input tests RED for missing light response, max backward pulse and
  mirror/regrab continuity, then GREEN after the temporal canvas offset.
- Repeated max-release regrab test separately RED for accumulated displacement,
  then GREEN with bounded pulse composition. All11 Node tests pass, including
  both scales, zero-input/no-hold kick, release/settle continuity and light signed recoil.
- V6 marker/rig tests RED on clone-only baseline, GREEN after continuous field:
  held rigid head(-2,+1), compressed torso, opposite recoil, exact zero/rest and
  full-pull head/eye texels, all121signed-pose grounded feet and clear padding.
- Prior cheek-only root/body preservation assertions still test the upstream
  cheek pass, not the intentionally moving torso in the final reaction pass.
- V5 reach fixture RED then GREEN: fixed-root ten-pixel tip maps to21px instead
  of28px. Actual contour tip agrees with the shortened fill; ink width, skin and
  all prior root/eye/attachment/body checks pass.
- V2 timing/regrab and no-vertical-inflation tests failed against v1 before changes.
- C# checks pass: >=3px extra tip reach, exact rest,
 81positive-pose cheek-root/whole-eye preservation, negative eye follow,121signed-pose
  boundary clearance/body/ribbon identity.
- Prior six Node tests cover: dimension-aware bank sampling, rapid snap, exactly two diminishing extrema, continuous
  settling, mid-release regrab preserving displayed stretch, release/capture loss.
- V3 RED then GREEN: a synthetic tip ten pixels outside the fixed root reaches
  28px instead of v2's14px. Root/eye and all121pose boundary checks still pass.
- V4 actual maximum-tip ink width failed on v3 before the contour pass; now keeps
  one to two dark texels across its normal, with opaque skin inward of the line.
- V4 review caught row64 hair attachment coverage being erased; integrated81pose
  attachment preservation test RED then GREEN after excluding that row.
- Read-only reviewer caught eye overlap; corrected and regression tested.
- Re-review caught negative hair sampling replacing cheek compression. Added
  hand-derived overlapping-contour sample RED then GREEN after sampling a padded
  cheek-stage snapshot. Final scoped re-review: no remaining important issue.
- Browser max hold and release observed; real drag produced full stretch and
  entered spring release. White/dark/mirror visual checks are local preview only.
- V2 scoped review: no remaining important issues; five Node tests independently
  passed and generated comparison checked for horizontal reach without vase bulge.

User visual acceptance remains pending. Not a claim of native application parity.
