# Walk / sit motion study

Preview renderer and deterministic frame export for the desktop product.

## Current: final authored 10-2 transition

The browser uses the user's transparent `assets/seated-final-10-2.png`, copied
without byte changes. Its authored alpha and white body paint are preserved;
integer registration (+3,-12) aligns its head without rescaling or redesigning
the pose. Legacy opaque/JPEG import alone uses white-background matte recovery.
65 baked poses connect canonical standing to this endpoint, and standing up
uses the same bank in reverse. Head color stays in registered coordinates;
body contour depth interpolates between endpoints. A narrow premultiplied
coverage handoff matches exact Canvas-scaled endpoint pixels without an alpha pop.
Rise refinement: `sit-correspondence.js` maps matching toe, cleft, fold and rump
landmarks before sampling both contour and interior color. This replaces the
fixed-coordinate interior dissolve that made knee/leg lines smear during rise.
Interior/contour sampling blends over continuously sampled depth; it must not
switch on rounded texel cells, which caused a one-frame dark knee-ink pop.
Standing render preparation also discards faint rump specks with no adjacent
visible boundary (alpha>=40), retaining connected antialias pixels. Canonical
files on disk and the supplied seated PNG remain unchanged.
Walking remains on the existing renderer. The browser does no live morph baking.

```powershell
node tools/PreviewLocomotion/verify-authored-sit.cjs
node --test tools/PreviewLocomotion/verify-seated-png.cjs
node tools/PreviewLocomotion/verify-rise-features.cjs
node tools/PreviewLocomotion/verify-rise-color-continuity.cjs
node tools/PreviewLocomotion/verify-rump-fringe.cjs
node tools/PreviewLocomotion/export-authored-sit.cjs
node tools/PreviewLocomotion/verify-authored-bank.cjs
node tools/PreviewLocomotion/verify-ink.cjs
node tools/PreviewLocomotion/verify-delivery.cjs
```

Frames and reverse-order metadata: `artifacts/repro/locomotion/authored-10-2/`.
Bank: `assets/seated-final-10-2-bank.png`. Duration is650ms;65 uniformly sampled
pose amounts are selected with smoothstep playback timing. `export-product.cjs`
exports the native96 product bank with open/closed eyes. The old JPEG is kept
only as a legacy matte-regression fixture, not an active art source.

## Historical procedural sitting fallback

The following describes the superseded procedural sitting renderer and its
legacy tests, not the authored10-2 bank used in the current browser preview.

Run from the repository:

```powershell
node tools/PreviewLocomotion/verify.cjs
node tools/PreviewLocomotion/verify-raster.cjs
node tools/PreviewLocomotion/verify-ink.cjs
node tools/PreviewLocomotion/verify-sit-continuity.cjs
node tools/PreviewLocomotion/verify-sit-topology.cjs
node tools/PreviewLocomotion/verify-sleep-shape.cjs
node tools/PreviewLocomotion/verify-belly-join.cjs
node tools/PreviewLocomotion/verify-seated-floor.cjs
node tools/PreviewLocomotion/verify-delivery.cjs # while preview server is running
node tools/PreviewLocomotion/server.cjs
```

Open http://127.0.0.1:2785/ . The left/upper reference is the canonical image
translated without pose deformation; it is not a recording of the old product.
The new pose uses three limb influences on one continuous body mesh and a
separate rigid head/ribbon, sampled in premultiplied color at 2x. No cut-out
limb seams are composited. Source textures are embedded in the served HTML.

Controls: connected sequence, treadmill walk, sit, stand, pause/scrub, speed,
mirror, black background and authored foot-contact markers. The sequence turns
at its resting endpoints; mirroring reverses both art and travel direction.
Canonical closed eyes provide periodic blinking. Stopped standing uses the
original bitmap directly. Protected head attachment remains pinned when sitting.

Regression checks exercise fixed world-space stance, planted forefeet, tucked
rear, attachment width, and actual timeline/cycle continuity. Raster checks use
the real renderer with bundled `@napi-rs/canvas` (override its module path with
`LOCOMOTION_CANVAS` if needed): 20 full-amplitude walking phases, first-motion
alignment, preserved head/forefeet at four sit amounts, opaque rounded haunch,
no protruding paw, filled torso and visible redrawn stroke. Walking ink is
reinforced inside its existing alpha boundary, never brightening existing ink;
three sampled joints retain at least95% of resting integrated ink over a gait.

The seated rear is extracted directly from `dororong-sleep.png`: its actual
outer alpha boundary and diagonal knee ink, translated down3px to the forefoot
floor. The user accepted the convex rear volume. A shallow tangent-aware belly
bridge joins the back of the planted front paw to that unchanged rear, replacing
the deep standing-root notch; obsolete internal root ink is cleared. This
replaces the rejected invented seated curves, signed-distance blend and
flattened/sheared-foot trial. A source-shape regression detects a substituted
haunch instead of the sleeping contour. The outer
stroke clips a2.8-source-pixel round stroke inside the body (1.4px visible).
The thin sleeping knee ink is strengthened and alpha-clipped to the rear fill.
Latest seated refinement compresses that crease35% and tucks it1.5px inward.
The lower front legs continuously align to the rear's local83.75px floor:
their authored sole heights81.75/87.25 were previously unequal. The preview
settles7outputpx during sit to keep that new shared floor on the stage ground.
This intentionally supersedes exact sitting-forepaw pixel preservation; head
preservation and walking rendering remain unchanged. A raster floor test covers
all three visible soles and the lower, more compact hindleg crease.
No generated artwork is used. Head/ribbon/front feet keep their original assets.
For the first12% of sit amount, a premultiplied coverage/ink handoff between the
moving mesh and moving contour avoids an abrupt pixel-to-vector style change.
There is no standing-to-seated endpoint-image dissolve. A101-frame regression
checks bounded contour movement and pixel change, including entry; topology
checks reject self-crossing paw joins. The former green-marker transport probe
was removed because the approved rear redraw intentionally replaces that texture.
Source asset files are unmodified.

Limitations: this is a visual trial pending approval of the redrawn seated rear.
Its fixed stroke is calibrated by visual comparison with standing art, not an
assertion of identical hand-drawn ink at every subpixel. Only
steady gait contact is mathematically locked; start/stop blends gather feet.
Raster cache quantization is for the browser preview, not a production timing
contract. App physics/tray/pause integration still needs separate approval.
