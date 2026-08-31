# M1 Stage A sleep crossfade — attempt 38 checkpoint

- Date: 2026-08-31
- Product checkpoint: `c883b7872dcdb162dd395c26b09353d570c178a5` (`feat: smooth Dororong sleep transition`)
- Runtime evidence: `artifacts/repro/stage-a-sleep-wake-attempt-38/runtime`
- Render evidence: `artifacts/repro/stage-a-sleep-wake-attempt-38/verification/rendered-sleep-crossfade-16ms-1/`

## Checkpoint result

The product caches each sleep-entry source as premultiplied `Pbgra32` pixels and interpolates adjacent approved frames into one displayed surface. It does not crossfade two semi-transparent WPF character controls or reduce the image control's opacity.

The existing attempt-38 render evidence includes `sleep-playback-selected-nearest-2x.png`, `sleep-playback.gif`, and `render-manifest.json`. The manifest records 36 samples from 0 through 560 ms, one body-Y position (`0`), and zero maximum adjacent body-Y delta. Existing rendered playback inspection established no whole-character alpha dip during the 16 ms crossfade samples. The user’s exact observation was `나쁘지 않다`.

This is an acceptable sleep-transition checkpoint for continuing work. It is not a claim that all Stage A routes pass, that the evidence is a new actual-Windows acceptance, or that broader M1 is complete.

## Fresh verification

At the product checkpoint, the following fresh checks passed:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
# PASS: 80/80

Get-ChildItem tests/Dororong.App.*.Tests.ps1 | Sort-Object Name | ForEach-Object {
    & pwsh -NoProfile -File $_.FullName -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw "App test failed: $($_.Name)" }
}
# PASS: all 14 current App PowerShell suites

dotnet build DororongDesktopPet.sln --configuration Release --no-restore
# PASS: 0 warnings, 0 errors
```

## Boundary and handoff

Slow approach (`CURIOUS`), fast approach (`STARTLED`), and drag wake (`DRAGGED` with preserved grab offset) remain `UNVERIFIED`. Broader M1 remains `PARTIAL`.

The next staged work is the approved [direct-interaction design](../specs/2026-08-31-dororong-direct-interaction-design.md), beginning with deterministic input-target metadata and tests only. Body-click, body-drag, and cheek art or actual-Windows interaction acceptance are not established by this checkpoint.
