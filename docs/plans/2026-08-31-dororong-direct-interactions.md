# Dororong Direct Interactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a smooth, happy body-click animation, a sketch-derived cat-like hanging body drag, and independently pressable/pullable left and right cheeks without changing Dororong's approved identity or interfering with ordinary Windows work.

**Architecture:** Preserve the existing `PetState` enum and core behavior priority. Add a small app-side direct-interaction controller that locks one visible-frame target for each press, feeds only the existing body click/drag events plus one local-interaction suppression flag into the core, and supplies presentation-local progress to the presenter. Render every approved multi-image family through one premultiplied `Pbgra32` surface at the existing nominal 16 ms tick; do not stack translucent character layers or introduce a general game/animation engine.

**Tech Stack:** C# / .NET 8 / WPF, xUnit, PowerShell 7, WPF `RenderTargetBitmap`, SHA-256 asset identity checks, image generation/editing only for bounded candidate production, and actual Windows manual observation.

**Spec:** `docs/specs/2026-08-31-dororong-direct-interaction-design.md`

**Roadmap:** `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`

## Global Constraints

- Work only on `feature/dororong-m1-expression-animation`; do not modify the frozen phase-1 branch.
- Preserve the approved canonical Dororong identity: head, face placement, hair, rose, bow, ribbons, no-tail body, proportions, outline character, and transparent background.
- Preserve `IDLE`, `WALK`, `CURIOUS`, `STARTLED`, `CLICK_REACTION`, `DRAGGED`, and `SLEEP`; do not add cheek-specific `PetState` values.
- Preserve behavior priority `DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP / IDLE / WALK`.
- Classify exactly one `Body`, `LeftCheek`, or `RightCheek` target at pointer-down and keep it locked through release or cancellation.
- Use the current Windows system drag threshold in one consistent DIP coordinate space; no hanging frame appears before the body threshold is crossed.
- A cheek press/pull never moves the window and never emits body click or body drag.
- Direct interaction wakes `SLEEP`, resets inactivity, and suppresses proximity reactions while its local presentation is active.
- Body click targets roughly 500 ms; cheek release targets roughly 220 ms; cheek effective pull is clamped near 20 DIPs.
- Every production key is a complete 96x96 transparent character image. Eye-only, face-only, cheek-only, and body-only overlays are forbidden.
- Author each next image from its immediate neighbor with onion-skin comparison. More frames do not excuse head, ornament, limb, anchor, alpha, or outline drift.
- Candidate frame count starts at body click 8–10, drag entry 6–8, drag settle 4–6, cheek pull 5–7 per side, and cheek spring 4–6 per side. Counts change only when visual evidence shows a continuity gap or redundant noise.
- Before product integration, show native frames, a nearest-neighbor enlarged strip, and normal-speed playback. Root visual review precedes user review; user approval is exact-family-specific.
- Interpolate only adjacent approved keys on one premultiplied `Pbgra32` surface at the nominal 16 ms presentation tick. Do not crossfade whole WPF controls.
- Transparent pixels remain click-through, keyboard focus remains with the user's work, topmost behavior and work-area clamping remain unchanged, and Exit remains explicit.
- Build, unit tests, image hashes, render captures, and process liveness do not prove actual Windows interaction. Each manual acceptance item stays `UNVERIFIED` until the user observes it.
- Do not publish into `artifacts/publish/win-x64`; every product observation uses a fresh attempt-specific directory.
- Do not push, create a PR, or merge unless the user separately requests it.

## File and Responsibility Map

- `src/Dororong.App/Interaction/DirectInteractionTarget.cs`: locked anatomical target enum.
- `src/Dororong.App/Interaction/DirectInteractionSnapshot.cs`: presentation-local phase, strength, release, and capture metadata.
- `src/Dororong.App/Interaction/DirectInteractionController.cs`: one-press lifetime, cheek pull math, body entry/settle timing, and cancellation.
- `src/Dororong.App/Controls/FrameInteractionDescriptor.cs`: canonical-space cheek regions, alpha-gated body fallback, and mirrored target mapping.
- `src/Dororong.App/Controls/PremultipliedFrame.cs`: cached 96x96 `Pbgra32` pixels for one full-character frame.
- `src/Dororong.App/Controls/PremultipliedFrameSequence.cs`: exact endpoint selection and adjacent-key interpolation on one surface.
- `src/Dororong.App/Controls/AlphaHitTestImage.cs`: public-to-assembly opaque source-point mapping used by both WPF hit testing and target classification.
- `src/Dororong.App/Controls/DororongPresenter.xaml.cs`: current descriptor exposure, press event, and final state/direct-interaction presentation mapping.
- `src/Dororong.App/PetLoop.cs`: queued target press consumption, controller update, core input, capture cleanup, and combined render snapshot.
- `src/Dororong.Core/Behavior/PetInput.cs` and `PetBrain.cs`: one `LocalInteractionActive` input bit that wakes, resets inactivity, and suppresses proximity without adding a state.
- `tests/Dororong.App.Tests/`: deterministic app-side target, controller, sequence, and loop tests.
- `tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs`: core wake/suppression and existing click/drag priority regression.
- `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`: dimensions, alpha, identity, ordering, protected-region, and neighbor-continuity checks.
- `tests/Dororong.App.DirectInteractionRender.Tests.ps1`: exact WPF state/phase rendering, one-surface opacity, and frame ordering.
- `src/Dororong.App/Assets/Interactions/`: only user-approved runtime key images.
- `src/Dororong.App/Assets/frame-sources/interactions/`: exact durable approved authored keys used to regenerate/runtime-compare the assets.
- `artifacts/candidates/direct-interactions/`: ignored candidate sheets, extracted frames, enlarged strips, metrics, and playback; never silently promoted to product assets.

## Design-to-Visual Check Contract

| ID | Observable | Evidence layer | Result boundary |
|---|---|---|---|
| DI-INPUT-1 | One opaque pointer-down locks exactly one anatomical cheek or body target until release/cancel. | deterministic app tests | logic only |
| DI-INPUT-2 | Transparent pixels classify nothing and remain click-through. | alpha tests plus actual Windows control-behind check | Windows item remains `UNVERIFIED` until observed |
| DI-CLICK-1 | Short body click reads press → hop → four-leg dangle → land/recover with a happy expression. | approved exact frame family and normal-speed playback | visual family-specific |
| DI-DRAG-1 | Threshold crossing, never pending press, begins the sketch-derived long hanging silhouette without grab-point jump. | core/app tests, approved frame family, Windows drag | layered verdicts kept separate |
| DI-CHEEK-1 | Each cheek presses/pulls outward independently, clamps near 20 DIPs, springs back near 220 ms, and never moves the window. | app math/render tests and Windows observation | layered verdicts kept separate |
| DI-ID-1 | Every frame preserves protected hair, rose, bow, ribbons, face placement, outline character, no-tail identity, and non-target anatomy. | exact asset comparison and root visual check | user approval required before integration |
| DI-MOTION-1 | 16 ms playback has no alpha dip, double image, one-frame protrusion, missing/extra limb, anchor jump, or independently redrawn ornament. | rendered sequence analysis plus normal-speed playback | cannot prove Windows feel alone |
| DI-CORE-1 | Existing click/drag priority, work-area clamping, sleep wake, and proximity suppression remain deterministic. | xUnit and app tests | logic only |
| DI-WIN-1 | Interaction does not steal focus, block transparent background clicks, escape bounds, or leak capture. | current-PC manual observation | user-reported only |

---

### Task 1: Preserve the accepted attempt-38 sleep crossfade checkpoint

**Files:**
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Modify: `tests/Dororong.App.SleepPose.Tests.ps1`
- Modify: `TASKS.md`
- Modify: `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`
- Create: `docs/verification/2026-08-31-m1-stage-a-sleep-crossfade-attempt-38.md`
- Preserve exactly: all files under `src/Dororong.App/Assets/`

**Interfaces:**
- Consumes: current working-tree single-surface `ApplySleepCrossfade` implementation and attempt-38 render evidence.
- Produces: one product/test checkpoint commit plus one docs-only evidence commit; neither claims that all Stage A routes or overall M1 pass.

- [ ] **Step 1: Confirm the bounded checkpoint diff**

  Run:

  ```powershell
  git diff --check
  git diff -- src/Dororong.App/Controls/DororongPresenter.xaml.cs tests/Dororong.App.SleepPose.Tests.ps1
  git status --short
  ```

  Expected: the product diff adds only cached `Pbgra32` source frames and adjacent-frame interpolation to the presenter; the focused test adds real WPF alpha/endpoint checks. Do not stage the stat-only `DororongPresenter.xaml` entry if `git diff --` remains empty.

- [ ] **Step 2: Re-run fresh checkpoint verification**

  Run:

  ```powershell
  dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
  Get-ChildItem tests/Dororong.App.*.Tests.ps1 | Sort-Object Name | ForEach-Object {
      & pwsh -NoProfile -File $_.FullName -Configuration Release
      if ($LASTEXITCODE -ne 0) { throw "App test failed: $($_.Name)" }
  }
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  ```

  Expected: Core `80/80`, every current App PowerShell suite exit `0`, and Release build has zero warnings/errors. Stop on the first real failure; do not reclassify attempt 38 from process liveness.

- [ ] **Step 3: Recheck the existing attempt-38 visual evidence without rebuilding it**

  Inspect:

  ```text
  artifacts/repro/stage-a-sleep-wake-attempt-38/verification/rendered-sleep-crossfade-16ms-1/sleep-playback-selected-nearest-2x.png
  artifacts/repro/stage-a-sleep-wake-attempt-38/verification/rendered-sleep-crossfade-16ms-1/sleep-playback.gif
  artifacts/repro/stage-a-sleep-wake-attempt-38/verification/rendered-sleep-crossfade-16ms-1/render-manifest.json
  ```

  Record the already established 36-frame, 0–560 ms, fixed-body-Y, no-alpha-dip result and the user's exact observation `나쁘지 않다`. Classify this only as an acceptable sleep-transition checkpoint for continuing work; keep remaining Stage A routes and overall M1 unchanged.

- [ ] **Step 4: Commit only the product and focused regression**

  ```powershell
  git add -- src/Dororong.App/Controls/DororongPresenter.xaml.cs tests/Dororong.App.SleepPose.Tests.ps1
  git diff --cached --check
  git commit -m "feat: smooth Dororong sleep transition"
  ```

  Verify `git show --name-status --oneline HEAD` contains exactly those two paths.

- [ ] **Step 5: Record the checkpoint boundary and commit docs only**

  Write the exact product commit, attempt-38 runtime path, known render evidence, fresh verification commands/results, user wording, and the boundary that slow/fast/drag wake plus broader M1 remain `UNVERIFIED`/`PARTIAL`. Update `TASKS.md` and the roadmap only to reflect this checkpoint and the newly approved direct-interaction spec.

  ```powershell
  git add -- TASKS.md docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md docs/verification/2026-08-31-m1-stage-a-sleep-crossfade-attempt-38.md
  git diff --cached --check
  git commit -m "docs: checkpoint sleep crossfade attempt 38"
  ```

### Task 2: Add the app test seam and direct-interaction data model

**Files:**
- Create: `tests/Dororong.App.Tests/Dororong.App.Tests.csproj`
- Create: `tests/Dororong.App.Tests/Interaction/DirectInteractionControllerTests.cs`
- Create: `src/Dororong.App/Interaction/DirectInteractionTarget.cs`
- Create: `src/Dororong.App/Interaction/DirectInteractionSnapshot.cs`
- Create: `src/Dororong.App/Interaction/DirectInteractionController.cs`
- Modify: `src/Dororong.App/AssemblyInfo.cs`
- Modify: `DororongDesktopPet.sln`

**Interfaces:**
- Produces: `DirectInteractionTarget`, `DirectInteractionPhase`, `DirectInteractionSnapshot`, and `DirectInteractionController` for later input and presentation tasks.
- Does not yet change WPF event routing, core state, product art, or visible behavior.

- [ ] **Step 1: Create the failing controller tests and test project**

  The test project targets `net8.0-windows`, sets `<UseWPF>true</UseWPF>`, references `src/Dororong.App/Dororong.App.csproj`, and uses the same xUnit package versions as `Dororong.Core.Tests`.

  Define tests for these exact cases:

  ```csharp
  [Fact] public void Begin_locks_target_until_release_or_cancel();
  [Theory] [InlineData(DirectInteractionTarget.LeftCheek, -1)]
           [InlineData(DirectInteractionTarget.RightCheek, 1)]
  public void Cheek_pull_uses_locked_screen_outward_sign(DirectInteractionTarget target, double sign);
  [Fact] public void Cheek_pull_clamps_at_twenty_dips_and_damps_vertical_motion();
  [Fact] public void Inward_cheek_motion_remains_press_instead_of_crossing_face();
  [Fact] public void Cheek_release_returns_from_current_strength_in_approximately_220ms();
  [Fact] public void Body_stays_pending_until_core_reports_dragged();
  [Fact] public void Drag_release_enters_local_settle_without_retaining_capture();
  [Fact] public void Cancel_clears_target_capture_and_release_progress();
  ```

  Run:

  ```powershell
  dotnet sln DororongDesktopPet.sln add tests/Dororong.App.Tests/Dororong.App.Tests.csproj
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release
  ```

  Expected RED: compile failure because the interaction types do not exist. Package restore/build failure is not the required RED.

- [ ] **Step 2: Add the minimal immutable contracts**

  ```csharp
  internal enum DirectInteractionTarget { None, Body, LeftCheek, RightCheek }

  internal enum DirectInteractionPhase
  {
      None, BodyPending, BodyDragEntry, BodyDragHold, BodyDragSettle,
      CheekPress, CheekPull, CheekRelease
  }

  internal readonly record struct DirectInteractionSnapshot(
      DirectInteractionTarget Target,
      DirectInteractionPhase Phase,
      PointD PressOrigin,
      PointD PointerPosition,
      double Strength,
      double ReleaseProgress,
      bool RequiresCapture)
  {
      internal static DirectInteractionSnapshot None { get; } = new(
          DirectInteractionTarget.None, DirectInteractionPhase.None,
          default, default, 0, 1, false);

      internal bool SuppressesProximity => Phase != DirectInteractionPhase.None;
  }
  ```

  Add `[assembly: InternalsVisibleTo("Dororong.App.Tests")]` to `AssemblyInfo.cs`.

- [ ] **Step 3: Implement the bounded controller**

  Use these constants and public-to-assembly methods:

  ```csharp
  internal sealed class DirectInteractionController
  {
      internal const double MaximumCheekPull = 20;
      internal static readonly TimeSpan CheekReleaseDuration = TimeSpan.FromMilliseconds(220);
      internal static readonly TimeSpan DragEntryDuration = TimeSpan.FromMilliseconds(140);
      internal static readonly TimeSpan DragSettleDuration = TimeSpan.FromMilliseconds(180);

      internal DirectInteractionSnapshot Current { get; private set; } = DirectInteractionSnapshot.None;
      internal void Begin(DirectInteractionTarget target, PointD pointerPosition, double outwardSign);
      internal DirectInteractionSnapshot Advance(
          TimeSpan delta,
          PointerSample pointer,
          bool primaryButtonDown,
          PetState previousState,
          PetState currentState);
      internal void Cancel();
  }
  ```

  Cheek strength is `Clamp((Max(0, deltaX * outwardSign) + 0.25 * Abs(deltaY)) / 20, 0, 1)`. Negative horizontal travel remains `CheekPress`. Release stores the displayed strength at button-up and applies smoothstep decay over 220 ms. Body presentation changes to `BodyDragEntry` only when `currentState == PetState.Dragged`; it reaches `BodyDragHold` after 140 ms and enters a capture-free 180 ms settle when the core leaves `DRAGGED`.

- [ ] **Step 4: Run tests and commit**

  ```powershell
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  git add -- DororongDesktopPet.sln src/Dororong.App/AssemblyInfo.cs src/Dororong.App/Interaction tests/Dororong.App.Tests
  git diff --cached --check
  git commit -m "test: define direct interaction controller"
  ```

### Task 3: Classify the visible target and integrate input/capture

**Files:**
- Create: `src/Dororong.App/Controls/FrameInteractionDescriptor.cs`
- Create: `tests/Dororong.App.Tests/Controls/FrameInteractionDescriptorTests.cs`
- Create: `tests/Dororong.App.Tests/Runtime/PetLoopDirectInteractionTests.cs`
- Modify: `src/Dororong.App/Controls/AlphaHitTestImage.cs`
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Modify: `src/Dororong.App/MainWindow.xaml.cs`
- Modify: `src/Dororong.App/PetLoop.cs`
- Modify: `src/Dororong.App/Runtime/PetLoopRuntime.cs`
- Modify: `src/Dororong.Core/Behavior/PetInput.cs`
- Modify: `src/Dororong.Core/Behavior/PetBrain.cs`
- Modify: `tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs`
- Modify: `tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs`

**Interfaces:**
- Consumes: Task 2 controller and snapshot types.
- Produces: alpha-gated, mirror-aware target press events; `PetInput.LocalInteractionActive`; combined `Render(PetSnapshot, DirectInteractionSnapshot)` delivery; deterministic capture cleanup.

- [ ] **Step 1: Write target, wake, loop, and capture tests first**

  Add exact tests for:

  ```csharp
  // Descriptor
  Assert.Equal(DirectInteractionTarget.RightCheek,
      descriptor.Classify(new PointD(30, 56), FacingDirection.Right, opaque: true));
  Assert.Equal(DirectInteractionTarget.LeftCheek,
      descriptor.Classify(new PointD(56, 56), FacingDirection.Right, opaque: true));
  Assert.Equal(DirectInteractionTarget.None,
      descriptor.Classify(new PointD(56, 56), FacingDirection.Right, opaque: false));
  Assert.Equal(DirectInteractionTarget.RightCheek,
      descriptor.Classify(new PointD(56, 56), FacingDirection.Left, opaque: true));

  // Core
  [Fact] public void Local_cheek_interaction_wakes_sleep_and_suppresses_pointer_reaction();
  [Fact] public void Local_cheek_release_never_emits_click_or_drag();

  // Loop
  [Fact] public void Cheek_press_captures_without_moving_window();
  [Fact] public void Body_press_does_not_capture_before_core_drag_threshold();
  [Fact] public void Release_cancel_dispose_and_fault_each_release_capture_once();
  [Fact] public void Render_receives_core_and_direct_snapshots_from_the_same_tick();
  ```

  Run both test projects. Expected RED: missing descriptor, input bit, and combined-render signatures.

- [ ] **Step 2: Expose alpha-gated source coordinates and descriptor classification**

  `AlphaHitTestImage` adds:

  ```csharp
  internal bool TryGetOpaqueSourcePoint(Point controlPoint, out PointD sourcePoint);
  ```

  It reuses the existing `TryMapToSourcePixel` and cached BGRA alpha; `HitTestCore` delegates to it so target classification and WPF click-through cannot disagree.

  `FrameInteractionDescriptor.Canonical` uses complete-frame coordinates:

  ```csharp
  internal static FrameInteractionDescriptor Canonical { get; } = new(
      leftCheek: new RectD(48, 48, 18, 17),
      rightCheek: new RectD(22, 48, 18, 17));

  internal DirectInteractionTarget Classify(
      PointD sourcePoint,
      FacingDirection facing,
      bool opaque);

  internal double GetScreenOutwardSign(
      DirectInteractionTarget target,
      FacingDirection facing);
  ```

  Cheeks are tested before the opaque body fallback. Facing-left mirrors X around the 96-pixel frame and swaps screen-side mapping while preserving anatomical target identity.

- [ ] **Step 3: Replace the body-only press event with a locked target event**

  ```csharp
  internal sealed class DirectInteractionPressEventArgs : EventArgs
  {
      internal DirectInteractionPressEventArgs(
          DirectInteractionTarget target,
          PointD windowLocalPosition,
          PointD framePosition,
          double outwardSign)
      {
          Target = target;
          WindowLocalPosition = windowLocalPosition;
          FramePosition = framePosition;
          OutwardSign = outwardSign;
      }

      internal DirectInteractionTarget Target { get; }
      internal PointD WindowLocalPosition { get; }
      internal PointD FramePosition { get; }
      internal double OutwardSign { get; }
  }
  ```

  `DororongPresenter` classifies against the descriptor active on pointer-down, raises one event, and marks the mouse event handled. `MainWindow` forwards the exact event to `PetLoop.NotifyDirectInteractionPressed`.

- [ ] **Step 4: Add the one-bit core suppression contract**

  Insert `bool LocalInteractionActive` into `PetInput` with this exact order:

  ```csharp
  public readonly record struct PetInput(
      TimeSpan Delta,
      RectD WorkArea,
      SizeD PetSize,
      PointerSample Pointer,
      bool PrimaryButtonDown,
      bool LocalInteractionActive,
      PointD? BodyPressPosition,
      SizeD DragThreshold);
  ```

  In `PetBrain.ProcessDirectInteraction`, process an actual body press first; otherwise, while `LocalInteractionActive` is true, reset inactivity, wake `SLEEP` to `IDLE`, and return handled so the pointer detector receives `isDirectInteractionPending: true`. After pointer detection and before the autonomous-state loop, return `Current` while `LocalInteractionActive` remains true; this freezes state progress and prevents a proximity or autonomous transition during cheek press/pull/release. Do not alter body click/drag threshold code or `PetState`.

- [ ] **Step 5: Integrate controller, render, and capture lifecycle**

  `PetLoop` consumes one queued direct press, begins the controller before the core update, sends a global body press to the core only for `Body`, then advances the controller using the previous/current core states. Change the host render delegate to:

  ```csharp
  Action<PetSnapshot, DirectInteractionSnapshot> render
  ```

  Capture is required when either the core is `DRAGGED` or the direct snapshot says `RequiresCapture`. `CheekPress` and `CheekPull` require capture; `CheekRelease` and all settled phases do not. Release on button-up, cancellation, dispose, and fault. During cheek phases, assert every recorded window position equals the pre-press position.

- [ ] **Step 6: Verify and commit**

  ```powershell
  dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore
  pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  git add -- src/Dororong.App/Controls/AlphaHitTestImage.cs src/Dororong.App/Controls/DororongPresenter.xaml.cs src/Dororong.App/Controls/FrameInteractionDescriptor.cs src/Dororong.App/MainWindow.xaml.cs src/Dororong.App/PetLoop.cs src/Dororong.App/Runtime/PetLoopRuntime.cs src/Dororong.Core/Behavior/PetInput.cs src/Dororong.Core/Behavior/PetBrain.cs tests/Dororong.App.Tests/Controls/FrameInteractionDescriptorTests.cs tests/Dororong.App.Tests/Runtime/PetLoopDirectInteractionTests.cs tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs tests/Dororong.App.RuntimeComposition.Tests.ps1
  git diff --cached --check
  git commit -m "feat: route locked direct interaction targets"
  ```

### Task 4: Extract the one-surface frame sequence renderer

**Files:**
- Create: `src/Dororong.App/Controls/PremultipliedFrame.cs`
- Create: `src/Dororong.App/Controls/PremultipliedFrameSequence.cs`
- Create: `tests/Dororong.App.Tests/Controls/PremultipliedFrameSequenceTests.cs`
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Modify: `tests/Dororong.App.SleepPose.Tests.ps1`

**Interfaces:**
- Consumes: attempt-38 exact endpoint and alpha behavior.
- Produces: `PremultipliedFrameSequence.Sample(double progress)` used by sleep and every later interaction family.

- [ ] **Step 1: Write sequence RED tests**

  Test exact frame object reuse at progress 0 and 1, segment selection across three synthetic frames, smoothstep midpoint interpolation of all premultiplied BGRA channels, equal dimensions/stride validation, and alpha preservation for disjoint incoming/outgoing pixels. Expected RED: sequence types absent.

- [ ] **Step 2: Implement the focused utility**

  ```csharp
  internal sealed class PremultipliedFrame
  {
      internal BitmapSource Source { get; }
      internal byte[] Pixels { get; }
      internal int Stride { get; }
      internal static PremultipliedFrame From(BitmapSource source);
  }

  internal sealed class PremultipliedFrameSequence
  {
      internal PremultipliedFrameSequence(IReadOnlyList<PremultipliedFrame> frames);
      internal BitmapSource Sample(double progress);
  }
  ```

  `Sample` clamps progress, maps it to adjacent indices, smoothsteps only the local fraction, interpolates every premultiplied byte into one 96x96 `Pbgra32` buffer, freezes the bitmap, and returns exact endpoint sources without allocation.

- [ ] **Step 3: Refactor sleep crossfade without changing its rendered evidence**

  Replace the presenter-private `CrossfadeFrame` loop with five two-key `PremultipliedFrameSequence` instances. Re-run the attempt-38 midpoint alpha, exact endpoints, fixed Body Y, and settlement assertions. Any changed expected pixel or alpha sum is a regression, not a new baseline.

- [ ] **Step 4: Verify and commit**

  ```powershell
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore
  pwsh -NoProfile -File tests/Dororong.App.SleepPose.Tests.ps1 -Configuration Release
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  git add -- src/Dororong.App/Controls/PremultipliedFrame.cs src/Dororong.App/Controls/PremultipliedFrameSequence.cs src/Dororong.App/Controls/DororongPresenter.xaml.cs tests/Dororong.App.Tests/Controls/PremultipliedFrameSequenceTests.cs tests/Dororong.App.SleepPose.Tests.ps1
  git diff --cached --check
  git commit -m "refactor: share premultiplied frame sequencing"
  ```

### Task 5: Produce and approve the body-click multi-image family

**Files:**
- Create after approval: `src/Dororong.App/Assets/frame-sources/interactions/body-click/body-click-00-press.png` through `body-click-08-recover.png`
- Create after approval: matching files under `src/Dororong.App/Assets/Interactions/BodyClick/`
- Create: `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`
- Modify: `src/Dororong.App/Dororong.App.csproj`
- Candidate only: `artifacts/candidates/direct-interactions/body-click-v1/`

**Interfaces:**
- Consumes: canonical frame identity and the approved 80/150/100/170 ms click timeline.
- Produces: nine approved complete-character keys: press, compress, lift, rise, apex, dangle, fall, land, recover.

- [ ] **Step 1: Write asset checks before adding product files**

  Require nine ordered 96x96 RGBA assets; nonzero alpha; no tail; exactly four readable legs at apex/dangle; canonical hair/rose/bow/ribbons outside the intended moving silhouette; stable head scale/anchor; and no one-frame protrusion, missing contour, transparent RGB fringe, or ornament redraw. Expected RED: body-click product directory absent.

- [ ] **Step 2: Create one coherent candidate sheet**

  Use the `imagegen` skill with the exact canonical PNG as identity reference and the approved timeline as motion reference. Request one horizontal nine-panel transparent sprite sheet so all poses are solved together, not nine independent generations. The happy expression is a squint/wink; the silhouette shows press → hop → four dangling legs → land and never reads as `STARTLED`.

  If the generated sheet changes protected identity, reject it before extraction. One evidence-based edit may correct the demonstrated defect; a second failed method stops the candidate and asks the user rather than silently integrating drift.

- [ ] **Step 3: Extract sequentially and perform root visual check**

  Extract to 96x96 cells in the candidate directory, then produce:

  ```text
  body-click-native-strip.png
  body-click-nearest-4x.png
  body-click-500ms.gif
  body-click-metrics.json
  ```

  Compare each image with its immediate neighbor using onion-skin/difference views. Root rejects anatomy, anchor, alpha, timing, or identity failures before showing anything to the user.

- [ ] **Step 4: Obtain exact-family user approval before integration**

  Show the enlarged strip and normal-speed 500 ms playback. Record approval or the exact failing image/transition. Do not copy candidate files into `src/` while approval is absent.

- [ ] **Step 5: Promote the approved exact bytes and commit assets**

  Copy the approved nine images byte-for-byte into both source and runtime directories, add explicit WPF `Resource` entries, run the asset test, record each SHA-256, and commit:

  ```powershell
  git add -- src/Dororong.App/Assets src/Dororong.App/Dororong.App.csproj tests/Dororong.App.DirectInteractionAssets.Tests.ps1
  git diff --cached --check
  git commit -m "art: add approved body click frames"
  ```

### Task 6: Integrate and observe body click

**Files:**
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Create: `tests/Dororong.App.DirectInteractionRender.Tests.ps1`
- Create after observation: `docs/verification/2026-08-31-m1-direct-interaction-body-click-attempt-1.md`

**Interfaces:**
- Consumes: approved Task 5 sequence and `PetState.ClickReaction` phase.
- Produces: subtle pending press plus exact 500 ms press/lift/apex/dangle/descent/land/recover playback.

- [ ] **Step 1: Write WPF RED tests**

  Sample pending press and `CLICK_REACTION` at every 16 ms tick. Require the ordered nine-key family, exact endpoint assets, full image opacity 1, one visible character surface, stable protected head/ornament regions, and no hanging drag key before threshold. Expected RED: current presenter only translates the canonical frame upward.

- [ ] **Step 2: Map click phase to the approved sequence**

  Pending body press samples the first subtle press key without advancing. `CLICK_REACTION` maps phase across approximately 80/150/100/170 ms and samples adjacent approved keys through `PremultipliedFrameSequence`. Keep window position unchanged and use no new `PetState`.

- [ ] **Step 3: Run automated and rendered verification**

  Run Core, App xUnit, asset, direct-render, sleep, and Release build checks. Render a native strip, nearest-neighbor strip, and production-speed GIF from the exact product resource. Root visual-check must compare it to the approved Task 5 hashes.

- [ ] **Step 4: Publish once and request the body-click Windows observation**

  Publish to `artifacts/repro/direct-interaction-body-click-attempt-1/runtime/`, record executable/DLL/assets hashes, and ask the user to check only: no stretch before release, happy hop/dangle/land readability, no focus theft, and transparent click-through on a known control behind the character. Keep any unobserved item `UNVERIFIED`.

- [ ] **Step 5: Commit implementation and evidence separately**

  Commit code/tests as `feat: animate Dororong body click`; after the user report, commit the exact evidence file as `docs: record body click acceptance attempt`.

### Task 7: Produce and approve the sketch-derived body-drag family

**Files:**
- Create after approval: seven `body-drag-entry-00-press.png` through `body-drag-entry-06-hang.png` source/runtime keys
- Create after approval: five `body-drag-settle-00-hang.png` through `body-drag-settle-04-recover.png` source/runtime keys
- Modify: `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`
- Modify: `src/Dororong.App/Dororong.App.csproj`
- Candidate only: `artifacts/candidates/direct-interactions/body-drag-v1/`

**Interfaces:**
- Consumes: the durable user sketch and canonical identity.
- Produces: seven coherent threshold-to-hang keys plus five coherent release-settle keys.

- [ ] **Step 1: Extend asset RED checks**

  Require complete 96x96 transparent keys, fixed top/grab anchor, continuous downward torso extension, four stable/countable legs, no tail, and continuous release back to canonical. Expected RED: drag family absent.

- [ ] **Step 2: Generate one entry/settle contact sheet from both exact references**

  Use the `imagegen` skill with `dororong-canonical.png` and `docs/specs/assets/2026-08-31-dororong-direct-interaction-user-sketch.png`. Request one sheet containing all twelve ordered poses so the long cat/scruff silhouette follows the user's sketch while hair, face, rose, bow, ribbons, and outline remain the same Dororong.

- [ ] **Step 3: Extract, onion-skin, play, and root-review**

  Produce native and 4× strips plus normal-speed entry/hold/release playback. Reject any frame that jumps the head/grab anchor, invents a tail, changes leg count, tears the outline, or redraws ornaments. Apply the same bounded one-edit rule as Task 5.

- [ ] **Step 4: Obtain user approval, promote exact bytes, and commit**

  No product copy occurs before the user approves the exact strip/playback. After approval, record hashes, run the extended asset checks, and commit as `art: add approved body drag frames`.

### Task 8: Integrate and observe body drag

**Files:**
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Modify: `tests/Dororong.App.DirectInteractionRender.Tests.ps1`
- Modify: `tests/Dororong.App.Tests/Interaction/DirectInteractionControllerTests.cs`
- Create after observation: `docs/verification/2026-08-31-m1-direct-interaction-body-drag-attempt-1.md`

**Interfaces:**
- Consumes: Task 3 threshold/core state, Task 7 approved art, Task 2 entry/settle progress.
- Produces: responsive pointer-following window with local 140 ms entry, stable hang hold, and 180 ms release settle.

- [ ] **Step 1: Add render/controller RED coverage**

  Require canonical/pending art below threshold, first drag key only after core `DRAGGED`, continuous seven-key entry while window movement begins immediately, hold on exact full-hang key, release at the clamped location, five-key settle, and capture release on button-up/fault/dispose.

- [ ] **Step 2: Map direct phases to approved sequences**

  `BodyDragEntry` samples the seven entry keys; `BodyDragHold` uses the exact final hang; `BodyDragSettle` samples the five settle keys. Do not delay `_host.SetWindowPosition(current.Position)` while entry art plays. Preserve the original `GrabOffset` and work-area clamp.

- [ ] **Step 3: Verify, visually gate, publish once, and observe**

  Run all logic/asset/render tests and Release build. Root verifies exact product playback. Publish to `artifacts/repro/direct-interaction-body-drag-attempt-1/runtime/`; ask the user to check threshold timing, no grab/window jump, responsive bounded drag, hang likeness, release location, settle, focus, and background click-through.

- [ ] **Step 4: Commit implementation and evidence separately**

  Commit code/tests as `feat: animate Dororong body drag`; after observation, commit exact evidence as `docs: record body drag acceptance attempt`.

### Task 9: Produce and approve both cheek families

**Files:**
- Create after approval: six left-pull and five left-release source/runtime keys under `src/Dororong.App/Assets/frame-sources/interactions/cheeks/left/` and `src/Dororong.App/Assets/Interactions/Cheeks/Left/`
- Create after approval: six right-pull and five right-release source/runtime keys under `src/Dororong.App/Assets/frame-sources/interactions/cheeks/right/` and `src/Dororong.App/Assets/Interactions/Cheeks/Right/`
- Modify: `tests/Dororong.App.DirectInteractionAssets.Tests.ps1`
- Modify: `src/Dororong.App/Dororong.App.csproj`
- Candidate only: `artifacts/candidates/direct-interactions/cheeks-v1/`

**Interfaces:**
- Consumes: canonical identity, durable cheek markings in the user sketch, 20-DIP clamp, and 220 ms spring.
- Produces: exact independent left/right complete-character sequences.

- [ ] **Step 1: Extend asset RED checks**

  Require each side's press/quarter/half/three-quarter/full pull progression and release family. Only the selected cheek plus a deliberately small whole-face response may change; opposite cheek, hair, ornaments, body, window anchor, and alpha silhouette stay stable.

- [ ] **Step 2: Generate left and right as two coherent sheets**

  Use the `imagegen` skill with the canonical and user sketch references. Generate one ordered sheet per anatomical side, not one image per pose. The pulled cheek extends outward; inward travel remains a compression; vertical response is small and damped. No large diagonal face distortion is allowed.

- [ ] **Step 3: Extract and validate side identity and continuity**

  Produce native/4× strips and 220 ms release playback for each side. Check that left never swaps to right under any adjacent frame, the opposite cheek is unchanged, and normal-speed release reads elastic rather than torn/snapped. Apply the bounded one-edit rule.

- [ ] **Step 4: Obtain per-side approval, promote, and commit**

  Show both exact families. Approval of one side does not approve the other. After both are approved, record hashes, run checks, and commit as `art: add approved cheek interaction frames`.

### Task 10: Integrate and observe cheek press/pull/release

**Files:**
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Modify: `src/Dororong.App/Controls/FrameInteractionDescriptor.cs`
- Modify: `tests/Dororong.App.DirectInteractionRender.Tests.ps1`
- Modify: `tests/Dororong.App.Tests/Interaction/DirectInteractionControllerTests.cs`
- Modify: `tests/Dororong.App.Tests/Runtime/PetLoopDirectInteractionTests.cs`
- Create after observation: `docs/verification/2026-08-31-m1-direct-interaction-cheeks-attempt-1.md`

**Interfaces:**
- Consumes: Task 2 strength/release metadata, Task 3 locked anatomical target, Task 9 exact frames.
- Produces: two separately controlled cheek interactions with no body result or window motion.

- [ ] **Step 1: Add exact render/input RED tests**

  Require selected side/strength mapping, mirrored facing classification, inward press-only behavior, damped vertical contribution, 20-DIP clamp, release from current displayed strength, approximately 220 ms return, one visible surface, no body `CLICK_REACTION`, no `DRAGGED`, and invariant window coordinates.

- [ ] **Step 2: Map cheek metadata to the approved side family**

  `CheekPress` selects the first compression key. `CheekPull` samples the locked side's six pull keys from `Strength`. `CheekRelease` samples the matching five-key spring family from the captured release strength and `ReleaseProgress`. The current frame descriptor tracks the displayed face and keeps the original anatomical target locked.

- [ ] **Step 3: Verify, visually gate, publish once, and observe one side at a time**

  Run full logic/asset/render/build verification and root visual-check. Publish to `artifacts/repro/direct-interaction-cheeks-attempt-1/runtime/`. Ask first for left press/pull/release and window immobility; record it. Then ask for right. Finally ask whether sleep wakes, no proximity reaction interrupts, focus stays put, and transparent background clicks still reach a known control.

- [ ] **Step 4: Commit implementation and evidence separately**

  Commit code/tests as `feat: add Dororong cheek interactions`; after both side observations, commit exact evidence as `docs: record cheek interaction acceptance attempt`.

### Task 11: Full regression, non-interference acceptance, and handoff

**Files:**
- Modify: `TASKS.md`
- Modify: `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`
- Create: `docs/verification/2026-08-31-m1-direct-interaction-final-acceptance.md`
- Create: `docs/handoff/2026-08-31-dororong-direct-interactions-handoff.md`

**Interfaces:**
- Consumes: exact accepted click, drag, and cheek commits/artifacts/evidence.
- Produces: truthful final slice verdict and continuation context; no PR/push/merge.

- [ ] **Step 1: Run the complete fresh automated suite**

  ```powershell
  dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --configuration Release --no-restore
  dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --no-restore
  Get-ChildItem tests/Dororong.App.*.Tests.ps1 | Sort-Object Name | ForEach-Object {
      & pwsh -NoProfile -File $_.FullName -Configuration Release
      if ($LASTEXITCODE -ne 0) { throw "App test failed: $($_.Name)" }
  }
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  ```

  Expected: every fresh command exits `0`; no expected count is copied from an older run.

- [ ] **Step 2: Run final root visual-check against exact approved hashes**

  Re-render every interaction at 16 ms, compare asset hashes to each approved family, inspect native and enlarged strips, and play at production speed. Reject any alpha dip, ghost, one-frame anatomy/anchor jump, wrong side, tail, extra/missing limb, or identity drift.

- [ ] **Step 3: Perform the ordered actual-Windows non-interference checks**

  On one exact final attempt-specific publish, record separately: body click, body drag, left cheek, right cheek, sleep wake, proximity suppression, transparent click-through in canonical and every interaction family, keyboard focus preservation, topmost behavior, work-area bounds, capture cleanup, and explicit Exit. Never infer unobserved rows.

- [ ] **Step 4: Record verdict and handoff without overstating M1**

  Mark the direct-interaction slice `PASS` only if every required exact-family automated/render check and every required actual-Windows row passes. Otherwise use `PARTIAL` with individual `FAIL`/`UNVERIFIED` rows. Overall M1 stays governed by the broader roadmap and is not automatically complete.

- [ ] **Step 5: Commit docs-only final records**

  ```powershell
  git add -- TASKS.md docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md docs/verification/2026-08-31-m1-direct-interaction-final-acceptance.md docs/handoff/2026-08-31-dororong-direct-interactions-handoff.md
  git diff --cached --check
  git commit -m "docs: hand off direct interaction checkpoint"
  ```

  Stop without push, PR, or merge.
