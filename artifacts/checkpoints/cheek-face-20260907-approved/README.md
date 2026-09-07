# Accepted cheek-face preview checkpoint

2026-09-07: user said the preview was good and explicitly requested committing this version before two further visual changes (rounded cheek tip and opposite eye/hair follow). This is that pre-change baseline, not the requested follow-up.

Product application: NOT APPLIED. This commit contains only a self-contained preview snapshot; existing accumulated product changes remain outside it.

`preview/playback.html` shows the exact accepted 41-frame animation. The PNG, APNG, HTML, checks and annotation files are copied unchanged from `artifacts/repro/cheek-face-20260907-attempt-1/candidate-reviewed`.

Original delivery ZIP SHA-256:
`FAF0DFB6CC3475D808EA3BDC6F3A61AF1D3888E067E9BF3557D724AE0D81EB1F`

The standalone tool copies the exact authoring code and accepted cheek renderer dependency. Only the project file is adjusted to compile the local dependency snapshot instead of linking an uncommitted product path. It does not build the product.

From the repository root, reproduce into a **new** directory:

```powershell
dotnet run --project artifacts/checkpoints/cheek-face-20260907-approved/proof-tool/Proof.csproj -c Release -- artifacts/checkpoints/cheek-face-20260907-approved/authority/source-native.png artifacts/repro/cheek-face-checkpoint-replay
```

Expected: 16 checks, zero failures; all 69 output files byte-identical to `preview/`. Full prior review/history remains in the exact delivery ZIP. Historical report wording inside the ZIP reflects its pre-user-feedback state, not a new ruling.

Known limitations preserved: angular cheek tip, local subpixel feature softness, authored feature masks and vacated-pixel fill. No claim of anatomical proof or product readiness.
