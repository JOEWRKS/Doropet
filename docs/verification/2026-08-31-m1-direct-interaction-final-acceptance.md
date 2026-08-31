# Dororong M1 direct interactions — final partial acceptance

## Outcome

The delivered direct-interaction scope is `PARTIAL`; overall M1 remains `PARTIAL`.

- Phase-1 body outline remains `PASS`.
- Body click is implemented with exact canonical open-eye artwork. The user-observed canonical-eye property remains `PASS`; the current deterministic 16 ms render/mapping layer also passes. Motion feel, focus behavior, and click-through remain `UNVERIFIED`.
- Body drag implementation and deterministic mapping pass. Final-review fix `8fa1a96` additionally makes threshold-crossing and later held-drag work-area bounds deterministic and aligns capture metadata with loop ownership. The attempt-2 art family, actual Windows boundary/motion feel, and user acceptance remain `PROVISIONAL / UNVERIFIED`.
- Left and right cheek art did not pass visual review, was not promoted, and was not integrated. Both directions are not delivered and remain `UNVERIFIED`.
- The complete fresh PowerShell-suite loop does not have an available invocation-bound final exit because its session identifier was lost. It is therefore `UNVERIFIED`, even though its separately rerun Core/App projects and the five corrected focused harnesses passed.
- No actual-Windows result is inferred from deterministic renders, publish identity, or process liveness.

The direct-interaction slice is not `PASS` because required art, complete-suite, and actual-Windows rows remain unresolved.

## Exact product and evidence checkpoint

- Product base entering Task 11: `df9669fd4ad11e1e5d512b61621e6e95c547b078`.
- Legacy harness compatibility commit: `fe1dac3bfb0d7ed6bd547742c1f64f07abcd0545` (`test: update legacy presenter harnesses`).
- Final-review fix commit: `8fa1a96385025f775abf57d4f72a7c77a1586912` (`fix: clamp held body drag to work area`); scoped re-review `PASS`, no new findings.
- Canonical frame SHA-256: `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78`.
- Fresh deterministic render root: `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/`.
- Render manifest: `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/render-manifest.json`.
- Current runtime publish: `artifacts/repro/direct-interactions-overnight-attempt-3/runtime/`.
- Current runtime executable SHA-256: `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A`.

The older `body-click-transform-v3` strips contained superseded squint frames. They were inspected only to identify that historical mismatch and are excluded from this checkpoint's final render verdict.

## Automated verification boundary

| Invocation or check | Fresh result | Verdict |
|---|---|---|
| `dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore` | `82 passed, 0 failed, 0 skipped`; exit `0` | `PASS` for that invocation |
| `dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore` | `53 passed, 0 failed, 0 skipped`; exit `0` | `PASS` for that invocation |
| Static legacy-call scan after migration | Zero stale direct `$presenter.Render(...)` call sites outside intentional ScriptMethod adapters | `PASS` |
| Focused corrected harnesses | BlinkRecovery, BlinkSequence, DraggedAngle, ExactArt, and IdlePose each exited `0` in one focused run | `PASS` for those five invocations |
| Complete sorted `Dororong.App.*.Tests.ps1` loop | Final output and final exit unavailable after the invocation-bound session identifier was lost | `UNVERIFIED`; not called green |
| `pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release` | `234` assertions; exit `0` | `PASS` at deterministic render-contract layer |
| `dotnet build DororongDesktopPet.sln --configuration Release --no-restore` | Build succeeded; `0` warnings; `0` errors; exit `0` | `PASS` |
| Final-review held-drag/capture RED/GREEN checks | Original four-edge threshold failures and pending-capture mismatch reproduced; focused fixes passed `4/4`, `2/2`, and `3/3` | `PASS` for the corrected deterministic contract |
| Final-review fresh Core/App projects | Core `86/86`, App `53/53`; each exit `0` | `PASS` for those invocations |
| Final-review Release build and scoped re-review | Build `0` warnings / `0` errors; review `PASS` with no new findings | `PASS` |

The original stale harness break was caused by commit `004934c`, which changed `DororongPresenter.Render` from a public one-argument method to an internal two-argument method. The corrected harnesses now resolve that internal method and `DirectInteractionSnapshot.None`, matching existing WPF harness patterns. No product behavior was changed to satisfy the stale harnesses.

## Deterministic visual/render inspection

The final render was sampled at the production 16 ms cadence, then inspected at native size and nearest-neighbor 4× enlargement. The approved reference was the exact canonical image for body click and the provisional Task 7 attempt-2 family for body drag. Cheek attempts were rejected evidence and were not eligible references.

| Family | Exact evidence | Observation | Verdict |
|---|---|---|---|
| Body click | 34 total frames: canonical rest, 0–496 ms at 16 ms, and the exact 500 ms endpoint; native grid `1E01F7FD452372E355F6589D3C87628EF6B47214B74C3845169ABF7EB2D52892`; 4× grid `3E4A81F7016FD69537134283C1ADB18C3316EA424C77C68B0ED3662011B7616B` | Every sample selects the exact canonical source, opacity remains 1, and the whole-character press/hop/land transform preserves eyes, anatomy, ornaments, and alpha continuity | `PASS` at deterministic appearance/mapping layer |
| Body drag | 140 ms entry sampled at 16 ms plus endpoint, one hold, and 180 ms settle sampled at 16 ms plus endpoint; native grid `F138773D03E18D968406EDAADCA41CCFD46A5C48699BA62D4977A1494E40242F`; 4× grid `73DFC8D27D9A3201770983845B7449E608ED435DB3421F66BB0B50D772B9FE8C`; attempt-2 metrics `57D32D0685E65679F995AFA0CBC1389F7937F58E9C282280D0F8B5B8FE685A08` | Fixed head/face/ornaments, continuous lower-body elongation and return, four-leg/no-tail read, one Bgra32/Pbgra32 complete-character surface, opacity 1 | Deterministic appearance/mapping `PASS`; art/runtime/user feel remains `PROVISIONAL / UNVERIFIED` |
| Left cheek | Rejected attempt-1 and attempt-2 packages under `artifacts/verification/direct-interactions/cheeks-v1/` | No eligible visual family exists | Not delivered; `UNVERIFIED` |
| Right cheek | Rejected attempt-1 and attempt-2 packages under `artifacts/verification/direct-interactions/cheeks-v1/` | No eligible visual family exists | Not delivered; `UNVERIFIED` |

The body-drag attempt-2 asset-only reference remains separately preserved under `artifacts/verification/direct-interactions/body-drag-v1/attempt-2/`; its native strip, 4× strip, 800 ms playback, and metrics hashes are recorded in the Task 7/8 reports. The present inspection validates the actual production mapping, not subjective desktop feel.

## Acceptance matrix

| Observable | Evidence | Result |
|---|---|---|
| Phase-1 body outline | Existing accepted attempt-8 record | `PASS` |
| Awake body click keeps canonical open eyes | Exact attempt-2 runtime; user said `ㅇㅇ 유지 됨` | `PASS` |
| Body-click canonical source and 16 ms mapping | Fresh final deterministic render | `PASS` |
| Pending-press subtlety and complete hop/apex/land feel | Not explicitly judged on the current runtime | `UNVERIFIED` |
| Body-drag controller/presenter mapping | Deterministic controller/render coverage and fresh final render | `PASS` |
| Held body-drag work-area bounds | `8fa1a96` four-edge regression covers threshold tick, subsequent held tick, preserved `(60,60)` grab offset, and clamped release | Deterministic `PASS`; actual Windows boundary feel `UNVERIFIED` |
| Capture ownership metadata | Body pending false, cheek true, core drag true, settle/release false; loop consumes the metadata consistently | Deterministic `PASS`; actual Windows cleanup `UNVERIFIED` |
| Body-drag art quality | Provisional attempt-2 family only | `PROVISIONAL / UNVERIFIED` |
| Body-drag Windows responsiveness, grab feel, release feel, and user acceptance | Not observed | `UNVERIFIED` |
| Left-cheek art and integration | Rejected candidates; no product family | Not delivered; `UNVERIFIED` |
| Right-cheek art and integration | Rejected candidates; no product family | Not delivered; `UNVERIFIED` |
| Sleep wake on the current runtime | Not observed | `UNVERIFIED` |
| Proximity suppression during local interaction | Deterministic logic exists; live behavior not observed | `UNVERIFIED` on Windows |
| Transparent click-through — canonical family | Not observed against another application | `UNVERIFIED` |
| Transparent click-through — body-click family | Not observed against another application | `UNVERIFIED` |
| Transparent click-through — body-drag family | Not observed against another application | `UNVERIFIED` |
| Transparent click-through — left/right cheek families | Families not delivered | `UNVERIFIED` |
| Keyboard focus preservation | Not observed | `UNVERIFIED` |
| Topmost behavior | Not observed | `UNVERIFIED` |
| Actual Windows work-area boundary feel | Not observed on the final-fix runtime | `UNVERIFIED` |
| Actual Windows capture cleanup | Not observed on the final-fix runtime | `UNVERIFIED` |
| Explicit Exit | Not observed on the current runtime | `UNVERIFIED` |
| Complete fresh PowerShell regression loop | Final invocation-bound exit unavailable | `UNVERIFIED` |
| Overall direct-interaction slice | Required cheek, complete-suite, and Windows rows remain unresolved | `PARTIAL` |
| Overall M1 | Broader roadmap rows remain unresolved | `PARTIAL` |

## Publish and process record

The initial attempt-1 publish path was confirmed absent. Publishing there with `--no-restore` failed once with `NETSDK1047` because `obj/project.assets.json` lacked the `net8.0-windows7.0/win-x64` target; the path remained absent and was not overwritten. A read-only inspection confirmed the missing target. One explicit `win-x64` restore exited `0`, after which one publish to the new immutable attempt-2 path exited `0`.

Before process changes, old PID `46668` was read back as the exact body-click attempt-2 executable, with the previously recorded command line and start time `2026-09-01T00:51:50.3779040+09:00`; its parent PID was no longer present. After the new publish succeeded, path, command, and start-time identity still matched, so only PID `46668` was stopped and absence was read back.

The new exact executable was launched once:

- PID: `45432`.
- Parent PID: `27480`.
- Start: `2026-09-01T04:30:37.8050600+09:00`.
- Command: `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-2\runtime\Dororong.App.exe"`.
- Executable path: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-2\runtime\Dororong.App.exe`.

That attempt-2 process record is now historical. After final-review fix `8fa1a96`, the new target `artifacts/repro/direct-interactions-overnight-attempt-3/runtime/` was confirmed absent and current HEAD was published there exactly once without restore:

```powershell
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/repro/direct-interactions-overnight-attempt-3/runtime --no-restore
```

The command exited `0`. Exact attempt-3 files are:

| File | Bytes | SHA-256 |
|---|---:|---|
| `Assets/dororong-canonical-source.png` | 45,681 | `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504` |
| `Dororong.App.deps.json` | 872 | `33645B94AC7BD58D1245115937354C18F35AFD3BEA7B7D2BB49946F0CC805988` |
| `Dororong.App.dll` | 270,848 | `AA4D381620D6FFD44A99AEC25C774BC0ACE3CB98D31796E2CE0C24CDD53C1DAA` |
| `Dororong.App.exe` | 150,016 | `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A` |
| `Dororong.App.pdb` | 33,832 | `3F134B96E383E03263D35A352805ADDF0EEC57FAC0F1657A8E0806EFAAD7ED6A` |
| `Dororong.App.runtimeconfig.json` | 515 | `89AD1EA5C9C20B6B266547EF27C0AE3840CAB5642D3C2AEDF06B7026245671DD` |
| `Dororong.Core.dll` | 32,768 | `9D3D36241A9FA7CC1EDA831EFECF328A6865054434ABD82366AF21E71A67B852` |
| `Dororong.Core.pdb` | 18,468 | `22FB0786664AA8D74E8C0B2EE90FE546CBE2A1A3C5953DD38AC2E853C1A67587` |

Before replacement, PID `45432` was reread as the exact attempt-2 executable. Its path, quoted command, CIM creation time `2026-09-01T04:30:37.8050600+09:00`, and executable hash `AFB74F04BC88E0D0FBF5B5DE2DD49ECE72E23A4B79A8EA1ED7D585AE39517EEC` matched the historical record. (`Get-Process` exposed an additional sub-microsecond precision digit; the same CIM source used for the recorded time matched exactly.) Only PID `45432` was stopped and absence was confirmed.

The attempt-3 executable was launched once:

- PID: `47088`.
- Parent PID: `44420`.
- Start: `2026-09-01T05:23:46.6142130+09:00`.
- Command: `"D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-3\runtime\Dororong.App.exe"`.
- Executable path: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-3\runtime\Dororong.App.exe`.
- Executable SHA-256: `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A`.

The attempt-3 app is intentionally left running for user morning inspection. Its presence proves only that the process launched; it does not mark actual Windows boundary feel, interaction, or non-interference rows `PASS`.
