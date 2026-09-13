# Cheek carry readiness and downward gaze — diagnosis only

User supplied four screenshots: first three show corrupted facial/limb geometry
while carrying by cheek and entering a taskbar/window-top readiness zone; fourth
shows a black seam when looking fully downward. No production fixes made here.

## Reproduction against installed renderer

`tools/RenderingRegressionProbe/Probe.csproj` references installed App/Core DLLs,
not the source project. Friend assembly name grants test-only internal access.
It creates offscreen WPF bitmaps; it neither launches nor stops the installed pet.

Run from repository root:

```powershell
dotnet run --project tools/RenderingRegressionProbe/Probe.csproj -c Release -- artifacts/repro/cheek-perch-neck-20260913-diagnosis
```

Installed App SHA256:
`945F9F8ED4598C61F07C2D160DE5D8441CFA636C485F3EF5AADC7ECE61C18B6E`.

## 1. Cheek + readiness corruption

Presenter Render chooses the current cheek overlay, then applies
PerchReadinessPresentation. The latter only checks square96/160 size before
running ForelegFlutterFrame with canonical/hanging polygons. A cheek capture
from tracking/hunting retains its captured head angle and crouch geometry via
CaptureHuntingCheek; there is no matching pose metadata passed to the flutter.

The canonical foreleg masks and fixed `y<67` head foreground rule therefore
intersect rotated/lowered head pixels. Head/cheek pixels can be removed from
the base and resampled as raised paws. This is not solved by changing readiness
entry distance or by the new isolated cheek preview.

Installed-renderer sweep,16 flutter phases with transformed cheek pin:

| Frame / head roll | Max changed opaque head pixels (>16 channel difference) |
| --- | ---: |
| Upright0 / -20deg |106|
| Upright0 /0deg |13|
| Upright0 /+20deg |0|
| Crouched60 / -20deg |139|
| Crouched60 /0deg |111|
| Crouched60 /+20deg |13|

Head coverage is independently rendered from HuntFrames.Head and HeadTransform.
The corresponding before/after images reproduce displaced face fragments.
Numbers are a renderer-level reproduction, not a recording of the user's window.

Proposed fix: carry pose/foreground information through the capture/readiness
boundary and animate forelegs in that same coordinate system, or render the
readiness limbs before compositing the independently transformed head. Do not
disable perch acquisition or replace the captured head with a canonical pose.

## 2. Downward gaze neck gap

At upright frame0, source pixel(54,60) is fully opaque with neutral head roll.
At -20deg it becomes nearly transparent. Independent layer alpha values:

| Layer | Neutral | -20deg |
| --- | ---: | ---: |
| Head |255|0|
| Body |4|4|
| Joins |0|0|
| Final native render |255|22|

The rotated head uncovers a region not covered by the body or existing join
geometry. Dark desktop background is visible through that gap; it is not a new
black painted contour. `_maskedJoins` is read via reflection only for this
offscreen diagnosis. Existing outer-join patches do not supply this inner neck.

Proposed fix: extend an interior neck backing surface under the rotating head,
without drawing a second visible jaw line or changing outer body silhouette.
Verify both roll extremes and preparation/recovery phases.

## Handoff

Diagnosis complete; product correction/installation not performed. Previewv6
remains separate at2801. All prior dirty product changes and preview banks remain.
