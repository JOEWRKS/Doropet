# Task 9 report — cheek art blocked before promotion

Status: `BLOCKED` at the art gate. Left and right cheek art, product integration, runtime behavior, and user acceptance are not delivered and remain `UNVERIFIED`. Task 10 was not started.

## Frozen contract

Each side required a separate complete-character family with six pull keys and five release keys. Only the selected anatomical cheek and a very small immediately adjacent face response could change. Head/hair roots, eyes, mouth, ornaments, body, opposite cheek, no-tail silhouette, anchor, and all unrelated pixels had to remain stable. The outward contour had to stay rounded, attached, and readable at native size and nearest-neighbor enlargement.

## Attempt 1 — primary image generation

Exact candidate paths:

- `artifacts/candidates/direct-interactions/cheeks-v1/attempt-1/primary/left-sheet-imagegen.png` — SHA-256 `BB324128380D5B44960594CB85B0BBA4929307CAE8B4D6E874D0B9F09644B554`.
- `artifacts/candidates/direct-interactions/cheeks-v1/attempt-1/primary/right-sheet-imagegen.png` — SHA-256 `C2AFC1EF8726C7B420BC8B58D36E97CC1CFF18F783045B18F276E26756D3DD3F`.

Result: rejected before promotion. The sheets baked an opaque checkerboard into the raster and redrew character scale, body, legs, and protected identity. They were not eligible to become product frames.

## Attempt 1 — materially different deterministic fallback

Exact inputs and outputs:

- Builder: `artifacts/candidates/direct-interactions/cheeks-v1/attempt-1/Build-CheekCandidates.py`.
- Focused evidence builder: `artifacts/candidates/direct-interactions/cheeks-v1/attempt-1/Build-CheekFocusedEvidence.py`.
- Frames: `artifacts/candidates/direct-interactions/cheeks-v1/attempt-1/fallback/frames/left/` and `.../right/`.
- Verification root: `artifacts/verification/direct-interactions/cheeks-v1/attempt-1/`.
- Manifest SHA-256: `49B3E39727D2B3BA25CF41626FDD1BBDF106EA05DB6AF97CF4EB3379B15C1B63`.

The package contains native, nearest-neighbor 4×, selected-region 12×, onion-skin, difference, and 220 ms release playback evidence for each side.

Result: rejected. Protected pixels were preserved, but the 12× evidence showed the viewer-right long hair/body boundary or viewer-left pink hair rim moving. It did not read as the selected cheek being pressed or pulled. A strengthened candidate check later confirmed that the left attempt-1 family had `0` selected-cheek skin pixels changed.

## Attempt 2 — single evidence-based recovery

The recovery changed the method based on the demonstrated occlusion: redraw the selected lower-face lobe as an attached rounded skin contour and allow only the immediately touching side-hair tip to follow by a few pixels. Roots, bangs, face, ornaments, opposite cheek, and body remained protected.

Exact paths:

- Builder: `artifacts/candidates/direct-interactions/cheeks-v1/attempt-2/Build-CheekCandidates.py`.
- Frames: `artifacts/candidates/direct-interactions/cheeks-v1/attempt-2/frames/left/` and `.../right/`.
- Verification root: `artifacts/verification/direct-interactions/cheeks-v1/attempt-2/`.
- Manifest SHA-256: `5116A87AC1B1F2F0452B848130B7665A4142989F3A2FC0B6D154B5EF93BF7F5C`.

The strengthened test correctly produced RED against attempt 1. The first recovery GREEN attempt then failed at `42` hair-involved pixels against the frozen `40`-pixel tip-only bound.

Root and implementer independently inspected the exact native/4×/12× package and rejected the same family:

- the viewer-right extended lobe reads as a detached paw or ribbon laid over the body rather than an attached cheek;
- the viewer-left contour contains checker-like dark artifacts and does not preserve a clean attached cheek edge.

This was a visual contract failure, not a product runtime failure. The bounded recovery was not retried.

## Promotion and repository boundary

- No attempt-1 or attempt-2 cheek frame was copied into `src/Dororong.App/Assets/` or `src/Dororong.App/Assets/frame-sources/`.
- No WPF resource entry was added.
- No presenter mapping or Task 10 implementation was created.
- No cheek app launch, publish, Windows observation, or user approval occurred.
- The temporary RED test was restored to committed HEAD.
- No Task 9 product/art commit exists.
- Existing rejected evidence remains immutable and must not be relabeled as approved.

Task 9 can resume only after a new art direction or explicit user decision changes the demonstrated failure boundary. Until then, both cheek directions remain not delivered and `UNVERIFIED`.
