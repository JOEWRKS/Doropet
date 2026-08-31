# Dororong M1 direct interactions — final partial acceptance

## Outcome

The delivered direct-interaction scope is `PARTIAL`; overall M1 remains `PARTIAL`.

- Phase-1 body outline remains `PASS`.
- Body click is implemented with exact canonical open-eye artwork. The user-observed canonical-eye property remains `PASS`; the current deterministic 16 ms render/mapping layer also passes. Motion feel, focus behavior, and click-through remain `UNVERIFIED`.
- Body drag implementation and deterministic mapping pass. The attempt-2 art family, exact runtime feel, and user acceptance remain `PROVISIONAL / UNVERIFIED`.
- Left and right cheek art did not pass visual review, was not promoted, and was not integrated. Both directions are not delivered and remain `UNVERIFIED`.
- The complete fresh PowerShell-suite loop does not have an available invocation-bound final exit because its session identifier was lost. It is therefore `UNVERIFIED`, even though its separately rerun Core/App projects and the five corrected focused harnesses passed.
- No actual-Windows result is inferred from deterministic renders, publish identity, or process liveness.

The direct-interaction slice is not `PASS` because required art, complete-suite, and actual-Windows rows remain unresolved.

## Exact product and evidence checkpoint

- Product base entering Task 11: `df9669fd4ad11e1e5d512b61621e6e95c547b078`.
- Legacy harness compatibility commit: `fe1dac3bfb0d7ed6bd547742c1f64f07abcd0545` (`test: update legacy presenter harnesses`).
- Canonical frame SHA-256: `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78`.
- Fresh deterministic render root: `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/`.
- Render manifest: `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/render-manifest.json`.
- Fresh runtime publish: `artifacts/repro/direct-interactions-overnight-attempt-2/runtime/`.
- Runtime executable SHA-256: `AFB74F04BC88E0D0FBF5B5DE2DD49ECE72E23A4B79A8EA1ED7D585AE39517EEC`.

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
| Work-area bounds | Deterministic logic exists; live result not observed | `UNVERIFIED` on Windows |
| Capture cleanup | Deterministic logic exists; live result not observed | `UNVERIFIED` on Windows |
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

The app is intentionally left running for user morning inspection. Its presence proves only that the process launched; it does not mark any actual-Windows row `PASS`.
