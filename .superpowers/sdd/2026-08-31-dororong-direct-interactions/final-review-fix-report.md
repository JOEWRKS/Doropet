# Final-review fix report

Status: complete for the requested deterministic fix. Actual-Windows observation remains outside this bounded pass.

Base: `c872164c78bc506e511ff6541bf78bd30f93c6b3`.

## Root cause and correction

`PetBrain.ProcessDirectInteraction` normalized autonomous positions and drag release, but both held-drag assignments wrote `pointer - grabOffset` without applying the current work-area/pet-size clamp. This allowed the window to escape on the threshold-crossing tick and every later held tick, then snap back only on release.

Both assignment sites now pass the pointer-derived top-left through `input.WorkArea.ClampTopLeft(..., input.PetSize)`. The pointer is still applied on the threshold-crossing tick, and the unchanged `_grabOffset` remains exposed throughout the held drag. Release keeps the existing normalization and settle behavior.

The capture metadata also contradicted the direct-interaction plan: `BodyPending` reported `RequiresCapture=true`, while `PetLoop` ignored that field and hard-coded cheek phases. `BodyPending` now reports false on begin and pending advances. `PetLoop.UpdateMouseCapture` now uses exactly `current.State == PetState.Dragged || directInteraction.RequiresCapture`, leaving cheek capture, core drag capture, and release/settle cleanup under their declared contracts.

## RED evidence against the pre-fix production code

Command:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PetBrainDirectInteractionTests.Held_drag_clamps_threshold_and_subsequent_ticks_at_each_work_area_edge"
```

Exit `1`: all four cases failed on the threshold tick. Expected left/right/top/bottom top-left extrema were respectively `X=0`, `X=680`, `Y=0`, and `Y=500`; production returned raw out-of-range coordinates `X=-70`, `X=840`, `Y=-70`, and `Y=640`.

Command:

```powershell
dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DirectInteractionControllerTests.Body_stays_pending_until_core_reports_dragged|FullyQualifiedName~PetLoopDirectInteractionTests.Body_drag_moves_on_the_threshold_tick_with_original_offset_then_settles_at_clamped_release"
```

Exit `1`: the controller snapshot reported pending-body capture as true, and the loop held the dragged window at `(840,650)` instead of the permitted `(680,500)`.

## GREEN evidence

- Focused four-edge Core regression, same command as RED: exit `0`, `4/4` passed. Each case checks the threshold tick, a subsequent held tick, exact work-area/pet-size extrema, unchanged `(60,60)` grab offset while held, and clamped release with cleared grab metadata.
- Focused controller/loop regression, same command as RED: exit `0`, `2/2` passed.
- Capture-focused command:

  ```powershell
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DirectInteractionControllerTests.Cheek_pull_clamps_at_twenty_dips_and_damps_vertical_motion|FullyQualifiedName~PetLoopDirectInteractionTests.Cheek_press_captures_without_moving_window|FullyQualifiedName~PetLoopDirectInteractionTests.Body_press_does_not_capture_before_core_drag_threshold"
  ```

  Exit `0`, `3/3` passed. Together with the controller/loop drag test, snapshots and loop observations require body-pending false, cheek true, drag true, and drag settle/release false.
- Full Core project:

  ```powershell
  dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
  ```

  Exit `0`, `86/86` passed.
- Full App project:

  ```powershell
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore
  ```

  Exit `0`, `53/53` passed.
- Release build, run once:

  ```powershell
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  ```

  Exit `0`, 0 warnings, 0 errors.

## Exact files

Production:

- `src/Dororong.Core/Behavior/PetBrain.cs`
- `src/Dororong.App/Interaction/DirectInteractionController.cs`
- `src/Dororong.App/PetLoop.cs`

Tests:

- `tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs`
- `tests/Dororong.App.Tests/Interaction/DirectInteractionControllerTests.cs`
- `tests/Dororong.App.Tests/Runtime/PetLoopDirectInteractionTests.cs`

Evidence:

- `.superpowers/sdd/2026-08-31-dororong-direct-interactions/final-review-fix-report.md`

## Self-review and boundaries

The clamp uses the work area and pet size supplied on each tick, clamps both axes through the existing geometry contract, and is applied only where a held body drag updates position. No presenter, render, art, timing, settle, or release logic changed. The tests derive literal extrema from the fixed `800x600` work area and `120x100` pet (`0..680`, `0..500`) rather than reusing production clamp logic.

The complete PowerShell loop, publish, render, GUI, and process actions were not run. PID `45432` was not inspected or touched. Actual-Windows feel remains unverified by this deterministic pass. The pre-existing `src/Dororong.App/Controls/DororongPresenter.xaml` EOL/stat-only working-tree change was not edited and must remain unstaged.
