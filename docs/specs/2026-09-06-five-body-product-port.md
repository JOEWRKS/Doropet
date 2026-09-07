# Five-region body pull — approved product port

Status: implementation authorized in chat on 2026-09-06; Windows feel and visual acceptance remain UNVERIFIED.

## Authority

The user approved porting the five-zone prototype into the actual WPF application, then explicitly approved preserving the existing product artwork and head/body-drag path while remapping the five regions to the product's 96×96 coordinates. This is not permission to replace the canonical asset with the opaque 100×100 prototype image, integrate the separate eight-JPG head prototype, draw new bridge art, or commit/push/PR.

Reference: `C:/Users/tjdwo/Downloads/doro/five-zone-soft-prototype/` (read-only). Its `source.png` SHA-256 is `7A326475A80D34A49B5123300C8752874A2B0FDD83130F914115E0A378AC6F11`, 100×100, all alpha 255. Product `src/Dororong.App/Assets/dororong-canonical.png` is 96×96 and has SHA-256 `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78`.

## Scope and architecture

Add exactly five local targets: screen-left small paw, central large paw, screen-right lower paw, oblique belly patch behind the hair, and upper-right rump. No separate tiny fourth-paw target. Keep existing head/cheek classification and body-drag sequence behavior outside these targets. Alpha-zero pixels remain ineligible. Canonical-shaped awake frames can use the new map; existing noncanonical sleep/hang frames retain their current routes rather than guessing a five-part anatomy for those poses.

Use a C# local pull session and a transparent runtime deformation renderer. Adapt the prototype's mask coordinates to the actual product pixels; do not scale the source image or assume the two canvases are registered. Disclose authored boundary/occlusion assumptions in a diagnostic overlay. The image bytes, head/face/hair/rose/ribbon pixels and scale remain unchanged except for whole-window movement and existing whole-character presentation. No new PNG assets or reconstructed animation family are authorized.

The input owner classifies once on press and retains target through release/settle. Freeze the visible canonical-shaped source and its source-to-window transform for the interaction so mirroring, walking transforms and breathing cannot make the grabbed region jump. Use the same transform (including mirroring) for pointer deltas, rendering and grab offset. Never derive it from logical facing alone.

Keep the softened prototype response: 0.8 source-pixel radial hysteresis, two cascaded 55 ms filters, 4 ms integration, at most 250 ms input backlog, 18 source-pixel radial carry threshold, 75 ms window follower, maximum limb root-to-tip reach 32 source pixels, 200 ms local settle. Direction remains dynamic after carrying. Negative/outside-window finite captured coordinates are valid. Belly/rump use the actual click anchor and a bounded local deformation; paws use their mapped root/tip, as in the accepted prototype. Clamp the real window through existing work-area rules and reconcile the session with the clamped position; release/cancel must not snap to a stale brain position.

The old Body target still uses its existing system threshold, 140 ms entry, 180 ms settle and `Sample()` sequence sampling. Do not modify `PremultipliedFrameSequence.cs`. Do not save or normalize `DororongPresenter.xaml`; padding/layout changes needed for deformed pixels must be scoped programmatically to the new presentation and restored afterwards. A single transparent premultiplied surface must be used, not an opaque-white prototype bitmap. Current 144×144 window geometry and non-interference behavior must be respected; do not enlarge the desktop interaction rectangle without evidence that alpha hit testing and work-area bounds remain correct.

## Verification and acceptance

- Baseline: Core 86/86, App 53/53 Release tests; approved assets 13/13 in both locations. HEAD `29750fe74967e611e267fa6cd3367a5928902b4c`. Existing XAML status is dirty but normalized blob equals HEAD `c742547165a90bc832458afccb58b405ad0f7793`; raw SHA `98529AB73340AA1C6DF24B6FA8C8F7DAC7E529EAA28B676D9A14E20F0B9D95CE` must remain unchanged.
- Tests first for real five-region classification, protected/transparent misses, captured input outside bounds, smoothing, reversals after carry, release/regrab, clamping, cancellation/capture failure, and no stale-position snapback.
- Render tests on real product pixels: exact idle and protected foreground, transparent background, useful movement for each target, no duplicated original paw contour, no clipping at supported pulls, mirrored target correspondence. Numerics are not visual approval.
- Existing Core/App tests, approved-asset regression, relevant DirectInteraction asset/render harnesses, Release build and diff check remain required. No weakening old contracts to accommodate the new target route.
- Produce diagnostic ownership overlay, native/nearest enlarged runtime renders and normal-speed deterministic playback from the actual C# renderer. Clearly distinguish these from a live Windows capture.
- Deliver a new local attempt-specific runtime package only after tests/review; do not overwrite an old publish. No automatic native app launch or claim of live Windows acceptance. User opens the package for actual observation.

## Exclusions and stop conditions

No commits, staging, push, PR, reset, checkout, clean, old artifact deletion, prototype edits, asset edits, XAML EOL changes, unrelated refactor or JOENESS changes. Preserve failed evidence. Stop with the actual limitation if the five-region renderer cannot preserve head identity or meaningful region deformation; do not call a numeric-only result visually approved.

The plan/progress authority is root `TASKS.md`. Review reports are evidence, not competing task ledgers.
