# Task 11 report — truthful partial final checkpoint

Status: `PARTIAL`. The delivered body-click/body-drag scope has deterministic evidence, but the complete PowerShell loop, cheek families, and required actual-Windows observations are not complete. No full direct-interaction or M1 pass is claimed.

Base: `df9669fd4ad11e1e5d512b61621e6e95c547b078`.

Harness compatibility commit: `fe1dac3bfb0d7ed6bd547742c1f64f07abcd0545` (`test: update legacy presenter harnesses`).

## Complete-suite attempts and harness root cause

The requested fresh sequence was:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore
Get-ChildItem tests/Dororong.App.*.Tests.ps1 | Sort-Object Name | ForEach-Object {
    & pwsh -NoProfile -File $_.FullName -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw "App test failed: $($_.Name)" }
}
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
```

Initial fresh results:

- Core: `82 passed, 0 failed, 0 skipped`; exit `0`.
- App: `53 passed, 0 failed, 0 skipped`; exit `0`.
- PowerShell loop stopped at `tests/Dororong.App.BlinkRecovery.Tests.ps1:109`, where the legacy harness directly invoked the former one-argument public `DororongPresenter.Render`.

Commit `004934c` had changed that API to an internal two-argument render method. The product reached the failure point correctly; the stale harness failed before the intended behavior. BlinkRecovery was migrated to the established reflection/`DirectInteractionSnapshot.None` pattern, and its focused recheck exited `0`. A full rerun then stopped at the same stale pattern in `BlinkSequence:50`, demonstrating an incomplete repository-wide harness migration rather than a product defect.

A read-only static scan then found the remaining stale direct calls in exactly four files:

- `Dororong.App.BlinkSequence.Tests.ps1:50`;
- `Dororong.App.IdlePose.Tests.ps1:148,150,184,220,277,283`;
- `Dororong.App.ExactArt.Tests.ps1:165`;
- `Dororong.App.DraggedAngle.Tests.ps1:41`.

One batch test-harness-only migration updated those four files and retained the BlinkRecovery fix. Each script resolves the internal two-argument method and `DirectInteractionSnapshot.None` once, then invokes it consistently. No product code or behavior changed.

Post-fix evidence:

- static stale direct-call count: `0` outside harnesses that intentionally install a ScriptMethod adapter;
- focused BlinkRecovery: exit `0`;
- focused BlinkSequence: exit `0`;
- focused DraggedAngle: exit `0`;
- focused ExactArt: exit `0`;
- focused IdlePose: exit `0`;
- fresh Core: `82/82`, exit `0`;
- fresh App: `53/53`, exit `0`.

The final exact full sorted PowerShell loop was started once. Its invocation-bound tool session identifier was not retained, so its final output and final exit are unavailable. Partial output and later process absence are insufficient evidence. Per the explicit checkpoint rule, the final complete loop is `UNVERIFIED` and is not described as green. It was not rerun.

Release build, run once after that boundary:

```powershell
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
```

Result: exit `0`; build succeeded with `0` warnings and `0` errors.

## Deterministic delivered-family render check

Focused render contract:

```powershell
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
```

Result: exit `0`; `234` assertions.

The exact final production mapping was rendered to:

`artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/`

Manifest: `render-manifest.json`, generated from HEAD `fe1dac3bfb0d7ed6bd547742c1f64f07abcd0545`.

Body click:

- canonical SHA-256 `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78`;
- 34 total frames: canonical rest, 0–496 ms at 16 ms, and the exact 500 ms endpoint;
- every frame source is the exact canonical URI; opacity min/max `1`;
- native grid SHA-256 `1E01F7FD452372E355F6589D3C87628EF6B47214B74C3845169ABF7EB2D52892`;
- nearest-4× grid SHA-256 `3E4A81F7016FD69537134283C1ADB18C3316EA424C77C68B0ED3662011B7616B`.

Body drag:

- 140 ms entry at 16 ms samples plus endpoint, one hold, 180 ms settle at 16 ms samples plus endpoint;
- Bgra32/Pbgra32 complete-character sources; opacity min/max `1`;
- Task 7 attempt-2 metrics SHA-256 `57D32D0685E65679F995AFA0CBC1389F7937F58E9C282280D0F8B5B8FE685A08`;
- native grid SHA-256 `F138773D03E18D968406EDAADCA41CCFD46A5C48699BA62D4977A1494E40242F`;
- nearest-4× grid SHA-256 `73DFC8D27D9A3201770983845B7449E608ED435DB3421F66BB0B50D772B9FE8C`.

Native and enlarged grids were opened and inspected. Current body click preserved canonical open eyes, anatomy, ornaments, and alpha continuity. Current body drag preserved the fixed head/face/ornaments and showed continuous lower-body elongation/return with a four-leg, no-tail read. These are deterministic appearance/mapping results only. Body-drag art, actual runtime feel, and user acceptance remain `PROVISIONAL / UNVERIFIED`.

Historical `body-click-transform-v3` evidence contained superseded squint frames and was explicitly excluded. Cheek attempts 1 and 2 were rejected evidence only; neither was rendered as an approved/delivered family.

## Publish attempt 1 — preserved failure

The required first target was confirmed absent:

`artifacts/repro/direct-interactions-overnight-attempt-1/runtime/`

Command:

```powershell
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/repro/direct-interactions-overnight-attempt-1/runtime --no-restore
```

Result: exit `1`, `NETSDK1047`. Read-only inspection showed that `obj/project.assets.json` contained `net8.0-windows7.0` but not `net8.0-windows7.0/win-x64`. The attempt-1 runtime path remained absent. It was not deleted, reused, or overwritten.

## Publish recovery — one changed-method attempt

Explicit restore:

```powershell
dotnet restore src/Dororong.App/Dororong.App.csproj --runtime win-x64
```

Result: exit `0`; the assets target `net8.0-windows7.0/win-x64` was then present.

The new target was confirmed absent:

`artifacts/repro/direct-interactions-overnight-attempt-2/runtime/`

Recovery publish, run once:

```powershell
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/repro/direct-interactions-overnight-attempt-2/runtime --no-restore
```

Result: exit `0`; the exact executable exists.

| File | Bytes | SHA-256 |
|---|---:|---|
| `Assets/dororong-canonical-source.png` | 45,681 | `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504` |
| `Dororong.App.deps.json` | 872 | `33645B94AC7BD58D1245115937354C18F35AFD3BEA7B7D2BB49946F0CC805988` |
| `Dororong.App.dll` | 271,360 | `12735F5436EAABFBA0DCAD7105E4C05B338E71FEB372C6ABAF4508793CDB32C4` |
| `Dororong.App.exe` | 150,016 | `AFB74F04BC88E0D0FBF5B5DE2DD49ECE72E23A4B79A8EA1ED7D585AE39517EEC` |
| `Dororong.App.pdb` | 33,828 | `6FEC4BFB3CD893351412300CEBCC44684E6A274D6373A4295D5D840548BEC2BC` |
| `Dororong.App.runtimeconfig.json` | 515 | `89AD1EA5C9C20B6B266547EF27C0AE3840CAB5642D3C2AEDF06B7026245671DD` |
| `Dororong.Core.dll` | 32,768 | `51DF81C66E04259FE5F2727A63A500644820C57C916D101485DDE753D551C0B0` |
| `Dororong.Core.pdb` | 18,468 | `6F1A81EF450ADBB80D433C136A5C2CF5A6FB7D222B80D815D958EE773D941C93` |

## Exact process handling

Old task-owned PID `46668` was read before any process change:

- executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interaction-body-click-attempt-2\runtime\Dororong.App.exe`;
- command: that exact quoted executable path;
- start: `2026-09-01T00:51:50.3779040+09:00`;
- parent PID `40792` was absent, so no further live lineage existed.

After the new publish succeeded, path, command, and start time were reread and all three still matched. PID `46668` alone was stopped; readback then confirmed it absent.

The new exact attempt-2 executable was launched once, without retry or relaunch:

- PID `45432`;
- parent PID `27480`;
- executable path `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation\artifacts\repro\direct-interactions-overnight-attempt-2\runtime\Dororong.App.exe`;
- command line: the same exact path, quoted;
- start `2026-09-01T04:30:37.8050600+09:00`;
- executable SHA-256 `AFB74F04BC88E0D0FBF5B5DE2DD49ECE72E23A4B79A8EA1ED7D585AE39517EEC`.

The app is left running for morning user inspection. Liveness is not used as actual-Windows interaction evidence.

## Final boundary and hygiene

- Phase-1 body outline remains `PASS`.
- Body-click canonical-eye preservation remains user-observed `PASS`; remaining feel/focus/click-through rows remain `UNVERIFIED`.
- Body-drag implementation/deterministic mapping is `PASS`; art/runtime/user feel remains `PROVISIONAL / UNVERIFIED`.
- Left/right cheeks remain not delivered and `UNVERIFIED`; Task 9 is blocked and Task 10 was not started.
- All unobserved actual-Windows rows remain `UNVERIFIED`.
- Complete PowerShell loop final exit remains unavailable/`UNVERIFIED`.
- Direct-interaction slice and overall M1 remain `PARTIAL`.
- No push, PR, merge, phase-2 branch, or publish to `artifacts/publish/win-x64` occurred.
- The pre-existing unrelated `src/Dororong.App/Controls/DororongPresenter.xaml` EOL/stat-only working-tree modification was never edited, staged, or committed.
