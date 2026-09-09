# Contour-preserving pull motion

## Scope

Replace translucent body/foot crossfades in the selected layered pull motion.
Keep original eight key poses, full isolated head/ribbon output, input thresholds,
swing, platforms, perch interactions, release timing and landing physics.
The user approved product integration after fixing intermediate art quality.

## Implementation

`tools/GenerateLayeredPull/contour.js` interpolates signed silhouette distance,
then transports source colors to the corresponding contour depth. The result has
one moving opaque silhouette instead of two half-transparent ones. Spatial
transport is bounded and accepts only residual-reducing steps.

The small far hindpaw owns a separate source patch and hip/tip motion. Composition
is rear paw, trunk, far foreleg, near foreleg, head. Foreleg silhouettes use the
same contour-preserving sampling. No changes were made to original PNG artwork.

All expensive contour calculations run during offline bank generation. The WPF
product uses its existing embedded 113-frame loader and dense-frame release path.
It does not recompute distance fields while dragging.

## Regression evidence

- Translated rectangle RED: previous location retained alpha128. GREEN: old edge
  absent, new interior opaque, one dark outline without old interior strokes.
- Frame25 exposed far-foreleg RED: five translucent edge pixels. GREEN: at most
  two in the measured band, retaining antialiasing.
- Three actual hindpaw interiors RED: no separate ownership. GREEN: owned by
  rear paw, absent from torso layer.
- Reviewer found frame41 source lookup escaping to y106.11. Uniform-ink fixture
  RED: ink70 turned163 from white fallback. GREEN: preserves ink70 after bounded
  transport/backtracking.
- All113 frames match their embedded bank, are valid premultiplied BGRA, unique
  and identical under reverse sampling. All8 authored keys and both-sided limits
  are preserved. Frozen prior head-only hashes match all113 frames.
- Real rear-paw marker follows its separate hip/tip correspondence.
- Actual presenter parity checks cover six selected frames and captured41->25
  recovery. Zero-time release raster checks cover every interval in both facings.

## Visual review boundary

Native-scale browser comparison and enlarged critical-interval atlases show the
former translucent duplicate replaced by a single rear paw and narrower outlines.
Independent read-only review found no blocking issue after the transport fix.
This remains small raster artwork: it is not a vector redraw, and screenshot or
test success does not substitute for the user's assessment of live motion feel.

## Product delivery

Release App676/Core237 tests and ExactArt passed. Independent review's transport
escape was reproduced, fixed and rechecked. Published App SHA256
`1D44E24ACA15979C290315E59F3941E40AC6B62F688E94544D8E53035EB602EA`
matches the tested assembly. Core identity is unchanged.

Runtime: `C:/Users/tjdwo/Downloads/doro/contour-pull-20260909/runtime`.
Old process21044 was replaced by40884; old runtime directory is preserved.
Loaded App/Core paths and responding status verified. No commit/push performed.
