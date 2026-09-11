# Short pounce preview

Preview originally added after accepted product checkpoint `719d300`.
Reuses the approved hunting curves and proof atlas. Native integration was
subsequently approved; this browser page remains an independent motion study.

## Native integration

The desktop host now opts into `UpdateHuntingWithPounce` and reads its
`PouncePose`. Horizontal travel has one window-position writer; the existing
platform owner supplies the hop's vertical offset and natural support-loss fall.
Head/cheek carry takes priority. Sit and body clicks cancel the hop from its
displayed height, including a click released between simulation ticks.
Rendering reuses the hunt atlas with forepaw-pivot rotation/stretch, then the
upright frame during post-landing tracking. No new raster assets are introduced.

Native controller/real-WPF tests: `PounceSessionTests` and `PounceLoopTests`.
Native black/white contact sheets: `artifacts/repro/pounce-product-20260911/`.
Current delivery/verification status is recorded in the top section of `TASKS.md`.

## Browser study

Run from the repository:

```powershell
node tools/PreviewLocomotion/serve-pounce.cjs --port=2800
node --test tools/PreviewLocomotion/verify-pounce.cjs
node tools/PreviewLocomotion/verify-pounce-browser.cjs
node tools/PreviewLocomotion/verify-pounce-tracking-browser.cjs
```

Open http://127.0.0.1:2800/ . Select mouse mode and stay near the pet for1.5s.
Leaving early resets dwell. After landing/recovery completes, a separate
tracking state holds the upright pose and position for3s. Head angle, whole
eyes and body facing follow an available pointer, including outside the
preparation radius. Then a new preparation may start if the mouse is nearby;
a fresh continuous1.5s dwell is required. No departure is required to rearm.
Jump direction locks at takeoff. Travel is capped at28 native pixels and stops
8 pixels short of the target center; the arc is12 pixels high (raised from9
on user request, with the existing easing and timing unchanged).

Automatic mode repeats a separate demonstration every8.5s (this resets the
stage, not a gameplay return slide). The scrubber is for this automatic clip.
Both the main view and small view show the same simulation. Black background
helps inspect outline and alpha edges. The browser test saves white/black
flight/landing frames under `artifacts/repro/pounce-preview-20260911/`.

Dependencies: existing hunt proof textures in
`artifacts/repro/hunt-preview-20260910/{body-atlas.png,head.png}`. On a fresh
checkout generate them with `node tools/PreviewLocomotion/export-hunt-proof.cjs`.
Renderer/export/browser tests use the local bundled Canvas/Playwright runtime,
as do the other motion-study tools. This is not a portable product launcher.
