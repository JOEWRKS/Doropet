# Dororong Milestone 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify a lightweight Windows desktop pet that behaves autonomously and responds distinctly to approach, click, drag, inactivity, and wake interactions without blocking work through its transparent area.

**Architecture:** Keep all product behavior in a deterministic `Dororong.Core` state machine and keep Windows input, transparent-window mechanics, and vector presentation in a thin `Dororong.App` WPF shell. Drive the core with immutable per-tick input and render only the returned snapshot, so replacing vector art with sprites does not change behavior logic.

**Tech Stack:** C#, `net8.0` core and xUnit tests, `net8.0-windows` WPF app, built with the installed .NET SDK on Windows x64; no third-party runtime framework.

**Spec:** `docs/specs/2026-08-26-dororong-m1-design.md`

## Global Constraints

- Target Windows only with C# / .NET 8 / WPF; the Release artifact is framework-dependent `win-x64` and requires the .NET 8 Desktop Runtime.
- Keep `Dororong.Core` free of WPF and Win32 references; `Dororong.App` alone owns OS input, focus, transparency, mouse capture, and rendering.
- Keep the solution to `Dororong.Core`, `Dororong.App`, and `Dororong.Core.Tests`; add no game engine, MVVM framework, dependency-injection container, or generic plugin system.
- Keep all initial behavior constants in one `BehaviorTuning` record and all screen coordinates passed into the core in WPF device-independent pixels.
- Treat `MaxDelta` as the simulation/state/inactivity/cooldown cap only. Pointer closing speed uses the actual positive observation interval, with elapsed-time smoothing equivalent to `alpha=0.35` at 100 ms; a long valid gap is one average-speed observation over that full gap, while unavailable samples reset the speed baseline.
- Preserve direct-interaction priority: `DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP / IDLE / WALK`.
- Begin mouse capture only after the core enters DRAGGED; the body-down event queues its screen position while global button state resolves click versus drag, and proximity reactions remain suppressed until that direct interaction resolves. While a press remains pending, freeze the interrupted lower-priority state's position and elapsed time; a release resolved in the same tick advances CLICK_REACTION normally, and DRAGGED follows `pointer - grabOffset`.
- Alpha-zero pixels must pass input to other processes, and ordinary body click or drag must not take keyboard focus from the active work application.
- Use the primary monitor work area only; multi-monitor refinement, sound, settings UI, auto-start, persistence, and polished art are outside Milestone 1.
- Implement core behavior test-first: write one failing behavior test, confirm the expected failure, write the smallest production change, then keep the full suite green.
- Do not treat build or tests as visual/runtime acceptance. Before a completion claim, run the produced executable on Windows and apply the project `visual-check` gate to the required states and interactions.

---

## File map

- `DororongDesktopPet.sln`: solution containing the three approved projects.
- `.gitignore`: excludes .NET build output, publish artifacts, IDE state, and user-specific files.
- `src/Dororong.Core/Geometry/PointD.cs`: framework-neutral point value.
- `src/Dororong.Core/Geometry/SizeD.cs`: framework-neutral size value.
- `src/Dororong.Core/Geometry/RectD.cs`: work-area bounds and top-left clamping.
- `src/Dororong.Core/Behavior/PetState.cs`: seven approved behavior states.
- `src/Dororong.Core/Behavior/FacingDirection.cs`: left/right presentation direction.
- `src/Dororong.Core/Behavior/BehaviorTuning.cs`: all timing, distance, speed, and cooldown defaults.
- `src/Dororong.Core/Behavior/IRandomSource.cs`: deterministic random-number seam.
- `src/Dororong.Core/Behavior/SeededRandomSource.cs`: production random source.
- `src/Dororong.Core/Behavior/PointerSample.cs`: optional global pointer position.
- `src/Dororong.Core/Behavior/PetInput.cs`: immutable tick input, including direct-interaction signals.
- `src/Dororong.Core/Behavior/PetSnapshot.cs`: state, position, facing, phase, and drag presentation output.
- `src/Dororong.Core/Behavior/PointerReactionDetector.cs`: closing-speed filter, near-zone hysteresis, and reaction cooldown decisions.
- `src/Dororong.Core/Behavior/PetBrain.cs`: autonomous, triggered, direct-interaction, sleep, and wake state machine.
- `tests/Dororong.Core.Tests/TestSupport/SequenceRandomSource.cs`: deterministic test-only random source.
- `tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs`: literal input factory and fixed tuning values used by behavior tests.
- `tests/Dororong.Core.Tests/Geometry/RectDTests.cs`: boundary and clamp behavior.
- `tests/Dororong.Core.Tests/Behavior/PetBrainAutonomyTests.cs`: IDLE/WALK timing, movement, and phase.
- `tests/Dororong.Core.Tests/Behavior/PetBrainPointerReactionTests.cs`: CURIOUS/STARTLED classification, hysteresis, and cooldown.
- `tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs`: click, drag, capture boundary, and direct-interaction priority.
- `tests/Dororong.Core.Tests/Behavior/PetBrainSleepTests.cs`: inactivity and every approved wake path.
- `src/Dororong.App/Interop/NativeMethods.cs`: minimal Win32 declarations and constants.
- `src/Dororong.App/Interop/DesktopInput.cs`: cursor pixel-to-DIP conversion and global primary-button state.
- `src/Dororong.App/Interop/WindowStyleManager.cs`: no-activation/tool-window extended style application.
- `src/Dororong.App/Controls/DororongPresenter.xaml`: alpha-zero vector surface and visible character shapes.
- `src/Dororong.App/Controls/DororongPresenter.xaml.cs`: state-to-pose rendering and body/Exit events.
- `src/Dororong.App/PetLoop.cs`: timer, input snapshot, core update, window movement, capture lifetime, and failure cleanup.
- `src/Dororong.App/Runtime/PetLoopRuntime.cs`: narrow delegate adapters for the clock, timer, input/window host, presenter, and capture boundary used by production and the STA integration regression.
- `src/Dororong.App/App.xaml`: application resources only; startup does not auto-create a window through `StartupUri`.
- `src/Dororong.App/MainWindow.xaml`: transparent, borderless, always-on-top WPF host.
- `src/Dororong.App/MainWindow.xaml.cs`: window-loop composition, failure display, and orderly shutdown.
- `src/Dororong.App/App.xaml.cs`: one-shot manual MainWindow construction/show, dispatcher fatal fallback, and exit-code-1 startup cleanup.
- `tests/Dororong.App.RuntimeComposition.Tests.ps1`: GUI-free STA regression that executes the production startup boundary and the real `PetLoop` wiring.
- `README.md`: exact build, test, run, publish, interaction, and known-limit instructions.
- `docs/verification/2026-08-26-m1-windows-acceptance.md`: actual executable observations, created only when the checks are performed.
- `TASKS.md`: JOENESS milestone state and evidence pointers, updated only at the implementation/acceptance boundary.

---

### Task 1: Scaffold the solution and geometry contract

**Files:**
- Create: `.gitignore`
- Create: `DororongDesktopPet.sln`
- Create: `src/Dororong.Core/Dororong.Core.csproj`
- Create: `tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj`
- Create: `tests/Dororong.Core.Tests/Geometry/RectDTests.cs`
- Create: `src/Dororong.Core/Geometry/PointD.cs`
- Create: `src/Dororong.Core/Geometry/SizeD.cs`
- Create: `src/Dororong.Core/Geometry/RectD.cs`

**Interfaces:**
- Consumes: approved primary-work-area boundary rule from the specification.
- Produces: `PointD`, `SizeD`, and `RectD.ClampTopLeft(PointD, SizeD)` for all later behavior and WPF tasks.

- [ ] **Step 1: Create empty project scaffolding without behavior code**

Run:

```powershell
dotnet new sln --name DororongDesktopPet
dotnet new classlib --name Dororong.Core --output src/Dororong.Core --framework net8.0
dotnet new xunit --name Dororong.Core.Tests --output tests/Dororong.Core.Tests --framework net8.0
Remove-Item -LiteralPath src/Dororong.Core/Class1.cs
Remove-Item -LiteralPath tests/Dororong.Core.Tests/UnitTest1.cs
dotnet sln DororongDesktopPet.sln add src/Dororong.Core/Dororong.Core.csproj tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj
dotnet add tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj reference src/Dororong.Core/Dororong.Core.csproj
```

Create `.gitignore` with exactly these repository-local exclusions:

```gitignore
**/bin/
**/obj/
.vs/
artifacts/
*.user
*.suo
```

- [ ] **Step 2: Write the failing work-area clamp test**

```csharp
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Geometry;

public sealed class RectDTests
{
    [Theory]
    [InlineData(-10, 20, 0, 20)]
    [InlineData(50, -5, 50, 0)]
    [InlineData(760, 550, 680, 500)]
    [InlineData(300, 220, 300, 220)]
    public void ClampTopLeft_keeps_the_entire_pet_inside_the_work_area(
        double x, double y, double expectedX, double expectedY)
    {
        var workArea = new RectD(0, 0, 800, 600);

        var actual = workArea.ClampTopLeft(new PointD(x, y), new SizeD(120, 100));

        Assert.Equal(new PointD(expectedX, expectedY), actual);
    }
}
```

- [ ] **Step 3: Run the test and verify the intended RED state**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~RectDTests
```

Expected: FAIL with `CS0234` or `CS0246` because `Dororong.Core.Geometry` and its value types do not exist.

- [ ] **Step 4: Implement the three geometry values**

Use immutable record structs with these exact public contracts:

```csharp
namespace Dororong.Core.Geometry;

public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD point, PointD offset) =>
        new(point.X + offset.X, point.Y + offset.Y);

    public static PointD operator -(PointD point, PointD offset) =>
        new(point.X - offset.X, point.Y - offset.Y);
}

public readonly record struct SizeD(double Width, double Height);

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public PointD ClampTopLeft(PointD topLeft, SizeD size) => new(
        Math.Clamp(topLeft.X, X, Math.Max(X, Right - size.Width)),
        Math.Clamp(topLeft.Y, Y, Math.Max(Y, Bottom - size.Height)));
}
```

- [ ] **Step 5: Verify GREEN and the clean solution build**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~RectDTests
dotnet build DororongDesktopPet.sln --configuration Debug
```

Expected: the four theory rows pass; the solution build exits 0 with no warnings.

- [ ] **Step 6: Commit the scaffold and geometry contract**

```powershell
git add .gitignore DororongDesktopPet.sln src/Dororong.Core tests/Dororong.Core.Tests
git commit -m "build: scaffold Dororong behavior core"
```

---

### Task 2: Implement deterministic IDLE and WALK behavior

**Files:**
- Create: `src/Dororong.Core/Behavior/PetState.cs`
- Create: `src/Dororong.Core/Behavior/FacingDirection.cs`
- Create: `src/Dororong.Core/Behavior/BehaviorTuning.cs`
- Create: `src/Dororong.Core/Behavior/IRandomSource.cs`
- Create: `src/Dororong.Core/Behavior/SeededRandomSource.cs`
- Create: `src/Dororong.Core/Behavior/PointerSample.cs`
- Create: `src/Dororong.Core/Behavior/PetInput.cs`
- Create: `src/Dororong.Core/Behavior/PetSnapshot.cs`
- Create: `src/Dororong.Core/Behavior/PetBrain.cs`
- Create: `tests/Dororong.Core.Tests/TestSupport/SequenceRandomSource.cs`
- Create: `tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs`
- Create: `tests/Dororong.Core.Tests/Behavior/PetBrainAutonomyTests.cs`

**Interfaces:**
- Consumes: `PointD`, `SizeD`, `RectD` from Task 1.
- Produces: the final public core API used by every later task:

```csharp
public interface IRandomSource
{
    double NextUnit();
}

public readonly record struct PointerSample(bool IsAvailable, PointD Position)
{
    public static PointerSample Unavailable => new(false, default);
}

public readonly record struct PetInput(
    TimeSpan Delta,
    RectD WorkArea,
    SizeD PetSize,
    PointerSample Pointer,
    bool PrimaryButtonDown,
    PointD? BodyPressPosition,
    SizeD DragThreshold);

public readonly record struct PetSnapshot(
    PetState State,
    PointD Position,
    FacingDirection Facing,
    double Phase,
    bool IsDirectInteractionPending,
    PointD? GrabOffset);

public sealed class PetBrain
{
    public PetBrain(BehaviorTuning tuning, IRandomSource random, PointD initialPosition);
    public PetSnapshot Current { get; }
    public PetSnapshot Update(PetInput input);
}
```

- [ ] **Step 1: Write failing autonomous behavior tests**

Use a test tuning record whose IDLE and WALK ranges are fixed to one second and a `SequenceRandomSource` whose queued literal values control the IDLE/WALK choice and walk heading. The production change each test catches is a missing transition, frame-rate-dependent movement, or unclamped position.

```csharp
[Fact]
public void Update_enters_walk_only_after_the_idle_duration_finishes()
{
    var brain = PetTestInput.CreateBrain(
        new SequenceRandomSource(0.0, 0.0, 0.0),
        new PointD(100, 100));

    Assert.Equal(PetState.Idle, brain.Update(PetTestInput.At(0.9)).State);
    Assert.Equal(PetState.Walk, brain.Update(PetTestInput.At(0.1)).State);
}

[Fact]
public void Update_moves_by_speed_times_elapsed_time_while_walking()
{
    var brain = PetTestInput.CreateBrain(
        new SequenceRandomSource(0.0, 0.0, 0.0),
        new PointD(100, 100));
    brain.Update(PetTestInput.At(1.0));

    var actual = brain.Update(PetTestInput.At(0.5));

    Assert.Equal(PetState.Walk, actual.State);
    Assert.Equal(121, actual.Position.X, precision: 6);
    Assert.Equal(100, actual.Position.Y, precision: 6);
}

[Fact]
public void Update_clamps_a_walk_at_the_right_work_area_edge()
{
    var brain = PetTestInput.CreateBrain(
        new SequenceRandomSource(0.0, 0.0, 0.0),
        new PointD(675, 100));
    brain.Update(PetTestInput.At(1.0));

    var actual = brain.Update(PetTestInput.At(0.5));

    Assert.Equal(680, actual.Position.X, precision: 6);
    Assert.Equal(FacingDirection.Left, actual.Facing);
}

[Fact]
public void Update_clamps_an_oversized_frame_to_the_configured_max_delta()
{
    var tuning = BehaviorTuning.Default with
    {
        MaxDelta = TimeSpan.FromSeconds(0.1),
        IdleMin = TimeSpan.FromSeconds(0.1),
        IdleMax = TimeSpan.FromSeconds(0.1),
        WalkMin = TimeSpan.FromSeconds(2),
        WalkMax = TimeSpan.FromSeconds(2),
        WalkSpeed = 42,
        IdleToWalkProbability = 1
    };
    var brain = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.0), new PointD(100, 100));
    brain.Update(PetTestInput.At(0.1));

    var actual = brain.Update(PetTestInput.At(1.0));

    Assert.Equal(104.2, actual.Position.X, precision: 6);
}

[Fact]
public void Update_ignores_a_negative_delta()
{
    var brain = PetTestInput.CreateBrain(
        new SequenceRandomSource(0.0, 0.0, 0.0),
        new PointD(100, 100));
    var before = brain.Current;

    var actual = brain.Update(PetTestInput.At(-1.0));

    Assert.Equal(before, actual);
}
```

`PetTestInput.At` must use literal defaults: work area `(0,0,800,600)`, pet size `(120,100)`, unavailable pointer, primary button up, no body press, and drag threshold `(4,4)`.

- [ ] **Step 2: Run the autonomy tests and confirm RED**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~PetBrainAutonomyTests
```

Expected: FAIL with missing `PetBrain`, `PetState`, and test-support types.

- [ ] **Step 3: Add the state, tuning, random, input, and snapshot types**

Define the state enums exactly:

```csharp
public enum PetState { Idle, Walk, Curious, Startled, ClickReaction, Dragged, Sleep }
public enum FacingDirection { Left, Right }
```

Define `BehaviorTuning` as a sealed record with these defaults:

```csharp
public sealed record BehaviorTuning
{
    public static BehaviorTuning Default { get; } = new();
    public TimeSpan MaxDelta { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan IdleMin { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan IdleMax { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan WalkMin { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan WalkMax { get; init; } = TimeSpan.FromSeconds(7);
    public double WalkSpeed { get; init; } = 42;
    public double IdleToWalkProbability { get; init; } = 0.65;
    public TimeSpan CuriousDuration { get; init; } = TimeSpan.FromSeconds(1.6);
    public TimeSpan StartledDuration { get; init; } = TimeSpan.FromSeconds(0.75);
    public TimeSpan ClickReactionDuration { get; init; } = TimeSpan.FromSeconds(0.5);
    public TimeSpan SleepDelay { get; init; } = TimeSpan.FromSeconds(90);
    public double NearEnterDistance { get; init; } = 150;
    public double NearExitDistance { get; init; } = 210;
    public double StartleReactionDistance { get; init; } = 220;
    public double StartleClosingSpeed { get; init; } = 650;
    public TimeSpan CuriousCooldown { get; init; } = TimeSpan.FromSeconds(4);
    public TimeSpan StartledCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public double StartleRetreatDistance { get; init; } = 72;
}
```

Implement `SeededRandomSource` with constructor `SeededRandomSource(int? seed = null)` and `NextUnit()` as `Random.NextDouble()`. Implement `SequenceRandomSource(params double[] values)` only in test support; it dequeues literal values and throws `InvalidOperationException` when a test consumes more randomness than declared.

Define these exact test helper signatures:

```csharp
internal static class PetTestInput
{
    public static PetInput At(
        double seconds,
        PointD? pointer = null,
        bool primaryDown = false,
        PointD? bodyPressPosition = null,
        RectD? workArea = null);

    public static PetBrain CreateBrain(IRandomSource random, PointD initialPosition);
    public static PetBrain CreateBrainAt(PointD initialPosition);
    public static PetBrain CreateReactionBrain();
    public static PetBrain CreateSleepBrain();
    public static PetBrain CreateSleepingBrain();
}
```

`At` creates an available `PointerSample` only when `pointer` has a value and otherwise uses `PointerSample.Unavailable`. `CreateBrain` uses the one-second autonomous tuning described in Step 1 and sets `MaxDelta` to one second so its literal test intervals are not clamped. `CreateBrainAt` supplies enough zero-valued random samples for that tuning. `CreateReactionBrain` fixes IDLE and sleep above ten minutes and also sets `MaxDelta` to one second for the explicit slow-approach sample. `CreateSleepBrain` fixes IDLE above ten seconds, sleep at one second, and `MaxDelta` at 100 ms. `CreateSleepingBrain` creates `CreateSleepBrain` and advances it by ten exact 100 ms updates before returning it.

- [ ] **Step 4: Implement the minimal autonomous state machine**

`PetBrain` starts in IDLE, selects an IDLE duration uniformly between `IdleMin` and `IdleMax`, and advances state time using `min(input.Delta, MaxDelta)` with negative time treated as zero. A range whose minimum equals its maximum returns that value without consuming randomness. At the end of IDLE, `NextUnit() < IdleToWalkProbability` enters WALK; otherwise it starts a fresh IDLE interval. WALK selects a uniformly distributed heading from one unit random value, moves by `WalkSpeed * deltaSeconds`, and selects a duration between `WalkMin` and `WalkMax`.

**Intentional correctness correction — boundary reflection (2026-08-26):** The earlier clamp-after-move wording discarded the distance remaining after a collision and made one long frame disagree with equivalent split frames. Integrate WALK independently on each axis as mirrored motion across the valid top-left interval, consuming all distance and supporting multiple reflections in one update. The final heading component must match the final mirrored segment, including exact-boundary landings. Keep `RectD.ClampTopLeft` for normalization, drag release, and non-WALK containment. Keep `Phase` in `[0,1)`: IDLE uses a repeating two-second cycle, WALK uses a repeating 0.6-second gait cycle, SLEEP uses a repeating 2.4-second breathing cycle, DRAGGED uses zero, and CURIOUS/STARTLED/CLICK_REACTION use elapsed divided by their finite response duration.

- [ ] **Step 5: Verify autonomy GREEN and run the complete core suite**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj
```

Expected: all geometry and autonomy tests pass with no warnings.

- [ ] **Step 6: Commit autonomous behavior**

```powershell
git add src/Dororong.Core/Behavior tests/Dororong.Core.Tests
git commit -m "feat: add autonomous idle and walk behavior"
```

---

### Task 3: Classify CURIOUS and STARTLED pointer approaches

**Files:**
- Create: `src/Dororong.Core/Behavior/PointerReactionDetector.cs`
- Modify: `src/Dororong.Core/Behavior/PetBrain.cs`
- Create: `tests/Dororong.Core.Tests/Behavior/PetBrainPointerReactionTests.cs`

**Interfaces:**
- Consumes: `PointerSample`, pet center derived from `PetSnapshot.Position + PetInput.PetSize / 2`, and Task 2 tuning values.
- Produces: internal `PointerReactionDetector.Update` decisions consumed only by `PetBrain`; public API remains `PetBrain.Update(PetInput)`.

- [ ] **Step 1: Write failing pointer-reaction tests**

Use a fixed long IDLE duration and fixed pet top-left `(100,100)` with size `(120,100)`, giving center `(160,150)`. Feed a baseline sample before the approach sample so closing speed is derived from two real snapshots.

```csharp
[Fact]
public void Slow_new_entry_into_the_near_zone_enters_curious()
{
    var brain = PetTestInput.CreateReactionBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

    var actual = brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

    Assert.Equal(PetState.Curious, actual.State);
}

[Fact]
public void Fast_closing_motion_inside_the_reaction_zone_enters_startled()
{
    var brain = PetTestInput.CreateReactionBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

    var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));

    Assert.Equal(PetState.Startled, actual.State);
    Assert.True(actual.Position.X < 100);
}

[Fact]
public void Fast_motion_away_from_the_pet_does_not_startle()
{
    var brain = PetTestInput.CreateReactionBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(360, 150)));

    var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(370, 150)));

    Assert.NotEqual(PetState.Startled, actual.State);
}

[Fact]
public void A_cursor_that_remains_near_does_not_retrigger_curious()
{
    var brain = PetTestInput.CreateReactionBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
    brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

    for (var index = 0; index < 30; index++)
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));

    Assert.Equal(PetState.Idle, brain.Current.State);
}
```

- [ ] **Step 2: Run the reaction tests and confirm RED**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~PetBrainPointerReactionTests
```

Expected: assertions fail because `PetBrain` remains IDLE and does not retreat.

- [ ] **Step 3: Implement pointer reaction detection**

`PointerReactionDetector` stores the previous valid distance, a filtered closing speed, a `nearLatched` flag, and independent CURIOUS/STARTLED cooldowns.

**Intentional correctness correction — observation time (2026-08-26):** Keep the clamped delta for simulation, state time, inactivity, and cooldowns, but calculate pointer closing speed from the actual positive input interval. Replace the fixed per-sample smoothing step with elapsed-time smoothing `alpha(dt) = 1 - (1 - 0.35)^(dt / 100 ms)`, preserving the original 100 ms response while removing sampling-cadence dependence. A long valid gap is deliberately treated as one average-speed observation over the full elapsed gap rather than compressed to `MaxDelta`; unavailable pointer data resets the speed baseline, and nonpositive input time does not create an observation.

On each valid sample:

1. compute distance from pointer to pet center;
2. compute raw closing speed as `(previousDistance - currentDistance) / actualObservationSeconds` when the input interval is positive;
3. update filtered speed with the elapsed-time alpha above;
4. enter the near latch at `distance <= NearEnterDistance` and clear it only at `distance >= NearExitDistance`;
5. return STARTLED when distance is within `StartleReactionDistance`, filtered closing speed is at least `StartleClosingSpeed`, and its cooldown is zero;
6. otherwise return CURIOUS only on a new near-latch entry with its cooldown at zero;
7. return no reaction for unavailable pointer data, pending direct interaction, or movement away.

Decrement cooldowns by clamped delta. On STARTLED, calculate the normalized vector from pointer toward pet center, move along it over `StartledDuration` for a total `StartleRetreatDistance`, and clamp the result. On CURIOUS completion, return to IDLE. Do not allow a stationary latched cursor to generate another entry.

- [ ] **Step 4: Verify GREEN and mutation-sensitive behavior**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj
```

Expected: all tests pass. Mentally verify that reversing the closing-speed subtraction breaks the STARTLED test, removing the latch breaks the no-retrigger test, and removing the retreat vector breaks the position assertion.

- [ ] **Step 5: Commit pointer reactions**

```powershell
git add src/Dororong.Core/Behavior tests/Dororong.Core.Tests/Behavior/PetBrainPointerReactionTests.cs tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs
git commit -m "feat: react to slow and fast pointer approaches"
```

---

### Task 4: Add click, drag, sleep, and wake transitions

**Files:**
- Modify: `src/Dororong.Core/Behavior/PetBrain.cs`
- Create: `tests/Dororong.Core.Tests/Behavior/PetBrainDirectInteractionTests.cs`
- Create: `tests/Dororong.Core.Tests/Behavior/PetBrainSleepTests.cs`
- Modify: `tests/Dororong.Core.Tests/TestSupport/PetTestInput.cs`

**Interfaces:**
- Consumes: queued `BodyPressPosition`, global `PrimaryButtonDown`, current pointer position, system `DragThreshold`, and Task 3 proximity reactions.
- Produces: final approved state priority, `IsDirectInteractionPending`, DRAGGED position with `GrabOffset`, inactivity tracking, and all four SLEEP wake routes.

- [ ] **Step 1: Write failing direct-interaction tests**

```csharp
[Fact]
public void Confirmed_body_click_outranks_a_simultaneous_fast_approach()
{
    var brain = PetTestInput.CreateReactionBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
    var pressed = brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(260, 150), primaryDown: true,
        bodyPressPosition: new PointD(260, 150)));

    var released = brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(260, 150), primaryDown: false));

    Assert.True(pressed.IsDirectInteractionPending);
    Assert.Equal(PetState.ClickReaction, released.State);
}

[Fact]
public void Body_press_released_before_the_next_tick_is_still_a_click()
{
    var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));

    var actual = brain.Update(PetTestInput.At(
        0.033,
        pointer: new PointD(160, 160),
        primaryDown: false,
        bodyPressPosition: new PointD(160, 160)));

    Assert.Equal(PetState.ClickReaction, actual.State);
}

[Fact]
public void Moving_past_the_system_threshold_enters_dragged_with_the_original_grab_offset()
{
    var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
    brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(160, 160), primaryDown: true,
        bodyPressPosition: new PointD(160, 160)));

    var actual = brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(400, 300), primaryDown: true));

    Assert.Equal(PetState.Dragged, actual.State);
    Assert.Equal(new PointD(340, 240), actual.Position);
    Assert.Equal(new PointD(60, 60), actual.GrabOffset);
}

[Fact]
public void Drag_release_clamps_the_pet_inside_the_work_area()
{
    var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
    brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(160, 160), primaryDown: true,
        bodyPressPosition: new PointD(160, 160)));
    brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(900, 700), primaryDown: true));

    var actual = brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(900, 700), primaryDown: false));

    Assert.Equal(PetState.Idle, actual.State);
    Assert.Equal(new PointD(680, 500), actual.Position);
}
```

- [ ] **Step 2: Run direct-interaction tests and confirm RED**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~PetBrainDirectInteractionTests
```

Expected: assertions fail because pending click, CLICK_REACTION, and DRAGGED are not implemented.

- [ ] **Step 3: Implement pending click and DRAGGED behavior**

When `BodyPressPosition` has a value, save that event position and `pressPosition - petTopLeft` grab offset, set `IsDirectInteractionPending=true`, reset inactivity, and skip pointer reaction detection. Do this even if the global button is already up at the next 33 ms tick, so a fast click cannot disappear between ticks. While pending, compare the current valid pointer position with the saved press position. Crossing either threshold while the button is down enters DRAGGED immediately. A button-up sample below both thresholds enters CLICK_REACTION for `ClickReactionDuration`; a button-up sample beyond the threshold settles to IDLE without fabricating a click.

**Intentional correctness correction — pending press priority (2026-08-26):** While the button remains down below threshold, freeze the interrupted WALK, STARTLED, or other lower-priority state's position and `_stateElapsed`; simulation-based cooldown and inactivity accounting continue with the clamped delta. If press and release are observed in the same tick, resolve CLICK_REACTION first and consume that tick normally so its phase advances. If the threshold is crossed, DRAGGED immediately applies `pointer - grabOffset` from the frozen press position, preventing a jump caused by lower-priority movement during the hold.

While DRAGGED and the button remains down, set top-left to `pointer - grabOffset` without autonomous movement. On release, clamp top-left, clear the pending/grab fields, and enter IDLE. A confirmed CLICK_REACTION interrupts STARTLED; a pending press prevents a new STARTLED or CURIOUS transition.

- [ ] **Step 4: Verify direct interaction GREEN**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~PetBrainDirectInteractionTests
```

Expected: all direct-interaction tests pass.

- [ ] **Step 5: Write failing sleep and wake tests**

Use test tuning with `SleepDelay=1 second` and IDLE duration fixed above two seconds so sleep can begin from IDLE without waiting in real time.

```csharp
[Fact]
public void Inactivity_enters_sleep_only_after_the_sleep_delay()
{
    var brain = PetTestInput.CreateSleepBrain();

    for (var index = 0; index < 9; index++)
        brain.Update(PetTestInput.At(0.1));
    Assert.Equal(PetState.Idle, brain.Current.State);

    brain.Update(PetTestInput.At(0.1));
    Assert.Equal(PetState.Sleep, brain.Current.State);
}

[Fact]
public void Click_without_drag_wakes_sleep_into_click_reaction()
{
    var brain = PetTestInput.CreateSleepingBrain();
    brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(160, 150), primaryDown: true,
        bodyPressPosition: new PointD(160, 150)));

    var actual = brain.Update(PetTestInput.At(
        0.1, pointer: new PointD(160, 150), primaryDown: false));

    Assert.Equal(PetState.ClickReaction, actual.State);
}

[Theory]
[InlineData(false, PetState.Curious)]
[InlineData(true, PetState.Startled)]
public void New_pointer_approach_wakes_sleep_into_the_matching_reaction(
    bool fast, PetState expected)
{
    var brain = PetTestInput.CreateSleepingBrain();
    brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

    var actual = brain.Update(PetTestInput.At(
        fast ? 0.1 : 1.0,
        pointer: fast ? new PointD(260, 150) : new PointD(300, 150)));

    Assert.Equal(expected, actual.State);
}
```

Add a separate assertion that crossing the drag threshold from SLEEP enters DRAGGED and that all four wake routes reset inactivity rather than immediately returning to SLEEP.

- [ ] **Step 6: Run sleep tests and confirm RED**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj --filter FullyQualifiedName~PetBrainSleepTests
```

Expected: assertions fail because inactivity and SLEEP transitions do not exist.

- [ ] **Step 7: Implement inactivity and wake routing**

Increment inactivity by clamped delta. Reset it on a new near-zone entry, STARTLED trigger, body press, confirmed click, or drag activity. When inactivity reaches `SleepDelay`, defer sleep until the state is IDLE, then enter SLEEP. In SLEEP, preserve the pending direct-interaction rules and allow a new slow or fast approach to enter CURIOUS or STARTLED. Do not let a stationary near-latched cursor repeatedly reset inactivity.

- [ ] **Step 8: Run the complete core suite and commit**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj
```

Expected: every geometry, autonomy, approach, direct-interaction, and sleep test passes with no warnings.

Commit:

```powershell
git add src/Dororong.Core/Behavior/PetBrain.cs tests/Dororong.Core.Tests
git commit -m "feat: add direct interaction and sleep behavior"
```

---

### Task 5: Create the non-activating transparent WPF shell

**Files:**
- Create: `src/Dororong.App/Dororong.App.csproj`
- Modify: `DororongDesktopPet.sln`
- Modify: `src/Dororong.App/App.xaml`
- Modify: `src/Dororong.App/App.xaml.cs`
- Modify: `src/Dororong.App/MainWindow.xaml`
- Modify: `src/Dororong.App/MainWindow.xaml.cs`
- Create: `src/Dororong.App/Interop/NativeMethods.cs`
- Create: `src/Dororong.App/Interop/DesktopInput.cs`
- Create: `src/Dororong.App/Interop/WindowStyleManager.cs`

**Interfaces:**
- Consumes: `PointD` from Core and the actual WPF `HwndSource`.
- Produces: `DesktopInput.TryGetPointerInDips(HwndSource, out PointD)`, `DesktopInput.IsPrimaryButtonDown()`, and `WindowStyleManager.ApplyNoActivateToolWindow(IntPtr)` for Task 7.

- [ ] **Step 1: Scaffold the WPF project and reference Core**

Run:

```powershell
dotnet new wpf --name Dororong.App --output src/Dororong.App --framework net8.0
dotnet sln DororongDesktopPet.sln add src/Dororong.App/Dororong.App.csproj
dotnet add src/Dororong.App/Dororong.App.csproj reference src/Dororong.Core/Dororong.Core.csproj
```

Confirm `Dororong.App.csproj` targets `net8.0-windows`, sets `<UseWPF>true</UseWPF>`, and contains no package reference.

- [ ] **Step 2: Make the shell depend on the not-yet-created adapters and confirm the compile failure**

Set `MainWindow.xaml` to the exact window contract:

```xml
<Window x:Class="Dororong.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Dororong"
        Width="144" Height="144"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        ShowActivated="False"
        Topmost="True">
    <Grid Background="{x:Null}" />
</Window>
```

In `MainWindow.SourceInitialized`, reference `WindowStyleManager.ApplyNoActivateToolWindow`. Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj
```

Expected: FAIL with `CS0103` because `WindowStyleManager` does not exist.

- [ ] **Step 3: Implement minimal Win32 interop**

`NativeMethods` contains only these APIs and constants:

```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool GetCursorPos(out POINT point);

[DllImport("user32.dll")]
internal static extern short GetAsyncKeyState(int virtualKey);

[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
internal static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool SetWindowPos(
    IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

internal const int GwlExStyle = -20;
internal const long WsExToolWindow = 0x00000080L;
internal const long WsExNoActivate = 0x08000000L;
internal const int VkLButton = 0x01;
internal const uint SwpNoSize = 0x0001;
internal const uint SwpNoMove = 0x0002;
internal const uint SwpNoZOrder = 0x0004;
internal const uint SwpNoActivate = 0x0010;
internal const uint SwpFrameChanged = 0x0020;
```

`DesktopInput.TryGetPointerInDips` calls `GetCursorPos`, then converts the physical screen point using `source.CompositionTarget.TransformFromDevice`. Return `false` and `default` without throwing when the native call or composition target is unavailable. `IsPrimaryButtonDown` returns whether the high bit of `GetAsyncKeyState(VkLButton)` is set.

`WindowStyleManager.ApplyNoActivateToolWindow` reads the current extended style, bitwise-adds `WsExToolWindow | WsExNoActivate`, and writes it back without removing any existing WPF style bits. It then calls `SetWindowPos` with `SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged` so the changed extended style takes effect without moving or activating the window.

- [ ] **Step 4: Build the shell**

Run:

```powershell
dotnet build DororongDesktopPet.sln --configuration Debug
```

Expected: build exits 0 with no warnings. Do not launch the shell in this task: it is intentionally fully transparent until Task 6 supplies the presenter, so a launch would provide no visual acceptance evidence and no user-facing Exit surface.

This task does not assert Windows-owned P/Invoke behavior through mocks. Cross-process click-through and foreground-focus behavior remain required actual-Windows checks in Task 8.

- [ ] **Step 5: Commit the WPF shell**

```powershell
git add DororongDesktopPet.sln src/Dororong.App
git commit -m "feat: add transparent non-activating WPF shell"
```

---

### Task 6: Build the replaceable vector presenter

**Files:**
- Create: `src/Dororong.App/Controls/DororongPresenter.xaml`
- Create: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- Create: `tests/Dororong.App.DraggedAngle.Tests.ps1`
- Modify: `src/Dororong.App/MainWindow.xaml`

**Interfaces:**
- Consumes: `PetSnapshot.State`, `Facing`, `Phase`, `IsDirectInteractionPending`, and `GrabOffset`.
- Produces: `DororongPresenter.Render(PetSnapshot)`, `BodyPrimaryPressed` with local press coordinates, and `ExitRequested` for Task 7.

**Intentional correction — presenter-local DRAGGED rotation (2026-08-26):** `BodyPrimaryPressed` reports `e.GetPosition(this)`, so `GrabOffset.X` is expressed in the 144×144 presenter coordinate system. The earlier `-54` formula incorrectly treated it as body-local. Compute the rotation origin from `Canvas.GetLeft(BodyGroup) + BodyGroup.Width / 2` (currently `18 + 108 / 2 = 72`) so a center grab is 0 degrees and equal left/right offsets produce equal, opposite angles. This corrects the coordinate mapping without changing the approved product-design meaning.

- [ ] **Step 1: Add the presenter host before the control exists and confirm the compile failure**

Replace the empty `MainWindow` grid with:

```xml
<Grid Background="{x:Null}">
    <controls:DororongPresenter x:Name="Presenter" />
</Grid>
```

Add `xmlns:controls="clr-namespace:Dororong.App.Controls"`, then run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj
```

Expected: FAIL with `MC3074` because `DororongPresenter` does not exist.

- [ ] **Step 2: Create an alpha-zero vector surface and body-only input source**

Create a 144×144 `UserControl` whose root and canvas have `Background="{x:Null}"`. Use one 108×96 body group built from filled WPF `Ellipse` and `Path` elements. Put the left eye, right eye, pupils, mouth, and any appendages inside that body group. Do not place a transparent filled rectangle, separate shadow, or decorative pixel outside the body group.

Attach left-button handling to the visible body group so bubbled events originate from a filled shape. Put a WPF `ContextMenu` on that same group with one `MenuItem Header="Exit"`. Every visible shape must belong to the interactive body group so no alpha-visible decoration can consume a cross-process click without producing a Dororong interaction.

Expose exactly:

```csharp
public sealed class BodyPressEventArgs(PointD localPosition) : EventArgs
{
    public PointD LocalPosition { get; } = localPosition;
}

public event EventHandler<BodyPressEventArgs>? BodyPrimaryPressed;
public event EventHandler? ExitRequested;
public void Render(PetSnapshot snapshot);
```

The primary handler calls `e.GetPosition(this)`, converts that WPF point to `PointD`, raises `BodyPrimaryPressed`, and sets `e.Handled=true`. The Exit item raises `ExitRequested`.

- [ ] **Step 3: Implement distinct state poses from normalized phase**

Reset every transform and eye property at the start of `Render`, then apply these formulas where `p = Math.Clamp(snapshot.Phase, 0, 1)`:

```csharp
var cycle = Math.Sin(p * Math.PI * 2);
var bounce = Math.Sin(p * Math.PI);
```

- IDLE: `ScaleY = 1 + 0.025 * cycle`; reduce eye scale while `p` is between `0.66` and `0.72` to produce one brief blink in the breathing cycle.
- WALK: `TranslateY = -4 * Math.Abs(cycle)`; set horizontal scale to `-1` only for left facing.
- CURIOUS: rotate `+7` degrees when facing right and `-7` when facing left; move both pupils two DIPs toward facing.
- STARTLED: for the first half use `ScaleX = 1 + 0.18 * bounce` and `ScaleY = 1 - 0.14 * bounce`; eyes remain fully open.
- CLICK_REACTION: `TranslateY = -10 * bounce`; eyes remain fully open.
- DRAGGED: `ScaleY = 1.12`; set `bodyCenterX = Canvas.GetLeft(BodyGroup) + BodyGroup.Width / 2`, then rotate by `Math.Clamp((snapshot.GrabOffset?.X ?? bodyCenterX) - bodyCenterX, -8, 8)` degrees.
- SLEEP: `ScaleY = 0.82`, `TranslateY = 8`, collapse pupils, reduce eyes to one-DIP horizontal lines, and use the slow body cycle for breathing.
- Pending direct interaction from SLEEP: render eyes opening at half height before click or drag resolution.

- [ ] **Step 4: Build the presenter mappings**

Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj
```

Expected: build exits 0. Confirm from the XAML and `Render` implementation that each `PetState` branch assigns the approved pose and that no filled element exists outside the body group. This is a code-structure check only; rendered-state and click-through acceptance remains in Task 8 and cannot pass here.

Run the focused presenter-coordinate regression check:

```powershell
pwsh -NoProfile -STA -File tests/Dororong.App.DraggedAngle.Tests.ps1
```

Expected: exit 0 after observing center `0`, symmetric points `-4/+4`, and clamps `-8/+8` from the actual WPF presenter.

- [ ] **Step 5: Commit the presenter**

```powershell
git add src/Dororong.App/Controls src/Dororong.App/MainWindow.xaml tests/Dororong.App.DraggedAngle.Tests.ps1 docs/plans/2026-08-26-dororong-m1-implementation.md
git commit -m "feat: add Dororong vector state presentation"
```

---

### Task 7: Compose the update loop, drag capture, and cleanup

**Files:**
- Create: `src/Dororong.App/PetLoop.cs`
- Create: `src/Dororong.App/Runtime/PetLoopRuntime.cs`
- Create: `tests/Dororong.App.RuntimeComposition.Tests.ps1`
- Modify: `src/Dororong.App/App.xaml`
- Modify: `src/Dororong.App/MainWindow.xaml.cs`
- Modify: `src/Dororong.App/App.xaml.cs`

**Interfaces:**
- Consumes: `PetBrain`, `DesktopInput`, `DororongPresenter`, `SystemParameters.WorkArea`, and the real WPF window.
- Produces: a running pet loop with delayed drag capture, clean release, error propagation, and deterministic shutdown.

**Intentional correctness correction — startup and integration seams (2026-08-26):** Remove `StartupUri` so WPF cannot create `MainWindow` outside the fatal boundary. `App.OnStartup` uses a one-shot production sequence to create, assign, and show exactly one MainWindow; construction or show failure makes one ordinary error attempt and reaches `Shutdown(1)` from a finally-equivalent boundary. `DispatcherUnhandledException` enters the same one-shot fatal path. `PetLoop` receives narrow clock, timer, input/window, presenter, and capture delegates through production adapters; the normal WPF constructor builds those adapters, while the STA script drives the same real `PetLoop.Start`, tick, fault, and Dispose wiring without a GUI. The seam is not a second runtime or a helper-only substitute.

- [ ] **Step 1: Wire MainWindow against the missing PetLoop and confirm the compile failure**

`MainWindow` must subscribe to `Loaded`, `Closed`, `Presenter.BodyPrimaryPressed`, and `Presenter.ExitRequested`. On Loaded, construct `_loop`, subscribe to `_loop.Faulted`, and call `Start`. On body press call `NotifyBodyPressed(e.LocalPosition)`; on Exit call `Close`; on Closed call `Dispose`.

Run:

```powershell
dotnet build src/Dororong.App/Dororong.App.csproj
```

Expected: FAIL with `CS0246` because `PetLoop` does not exist.

- [ ] **Step 2: Implement PetLoop lifecycle and initial placement**

Expose exactly:

```csharp
internal sealed class PetLoop : IDisposable
{
    public event EventHandler<Exception>? Faulted;
    public PetLoop(Window window, DororongPresenter presenter, DesktopInput input);
    public void Start();
    public void NotifyBodyPressed(PointD localPosition);
    public void Dispose();
}
```

`Start` reads `RectD` and `SizeD` through the production host adapter, and creates `PetBrain(BehaviorTuning.Default, new SeededRandomSource(), initialPosition)` at 32 DIPs from the work area's lower-right edge. The normal WPF adapter owns one `DispatcherTimer` with a 33 ms interval and one `Stopwatch`; the injected adapters expose only elapsed time, attach/detach/start/stop, frame input, window position, render, and capture operations.

`NotifyBodyPressed` converts the supplied local DIP position to screen DIPs by adding the host's current window position, then stores that point in one queued nullable field. Each tick reads pointer and global primary-button state, consumes that queued press position exactly once, builds `PetInput`, calls `PetBrain.Update`, applies the resulting window position, and then renders the same snapshot.

- [ ] **Step 3: Enforce capture and failure boundaries**

Compare the previous and current snapshots each tick:

- on first entry to DRAGGED, call `Presenter.CaptureMouse()`;
- on any exit from DRAGGED, call `Presenter.ReleaseMouseCapture()`;
- never capture while only `IsDirectInteractionPending` is true;
- if pointer sampling is unavailable, send `PointerSample.Unavailable` while continuing the timer;
- wrap each tick so any unexpected exception stops the timer, releases capture, raises `Faulted` once, and does not restart.

`Dispose` is idempotent: stop and detach the timer, stop the clock, and release capture if held. Cleanup attempts every step after one injected failure, preserves the failure, and does not repeat cleanup side effects on a second Dispose. `MainWindow` handles `Faulted` by disposing the loop, showing one ordinary error message, and calling `Application.Current.Shutdown(1)`.

- [ ] **Step 4: Run automated tests and the full Debug build**

Run:

```powershell
dotnet test tests/Dororong.Core.Tests/Dororong.Core.Tests.csproj
dotnet build DororongDesktopPet.sln --configuration Debug
pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Debug
```

Expected: all core tests pass, the complete app builds with no warnings, and the STA regression executes the production startup boundary plus actual `PetLoop` start/tick/input/brain/window/render/capture/fault/dispose wiring.

- [ ] **Step 5: Perform the first integrated interaction smoke check**

Launch the exact Debug executable with `Start-Process -PassThru`, record its PID, resolved path, command, and start time, then observe IDLE/WALK, one body click, one drag beyond the system threshold, and right-click Exit. Confirm capture begins only after visible movement and is released on mouse-up. The Exit action must end the recorded PID. If it does not, stop only that recorded task-owned PID after rechecking its path and start time; do not relaunch through the same failed mechanism until the cause is understood.

- [ ] **Step 6: Commit runtime composition**

```powershell
git add src/Dororong.App
git commit -m "feat: run and interact with Dororong on Windows"
```

---

### Task 8: Publish, verify the actual Windows experience, and hand off

**Files:**
- Create: `README.md`
- Create after performing the checks: `docs/verification/2026-08-26-m1-windows-acceptance.md`
- Modify: `TASKS.md`

**Interfaces:**
- Consumes: the complete solution, Section 10 acceptance checks in the spec, and the JOENESS ledger rule.
- Produces: documented run/publish commands, a source-backed Windows acceptance record, and an accurate milestone state.

- [ ] **Step 1: Write README with exact commands and product boundaries**

Document these commands verbatim from repository root:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
dotnet run --project src/Dororong.App/Dororong.App.csproj --configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

README must explain left click, drag, slow/fast approach, sleep/wake, right-click Exit, the .NET 8 Desktop Runtime requirement, primary-monitor limitation, temporary vector art, and the absence of settings, sound, auto-start, persistence, and polished multi-monitor behavior.

- [ ] **Step 2: Run fresh automated Release verification**

Run in this order and read every exit code:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/publish/win-x64
```

Expected final evidence is zero failed tests, zero build errors, a passing lifecycle/cleanup/input/capture runtime-composition check and DRAGGED presenter-coordinate check against the just-built Release assemblies, a successful runtime-specific publish, and `artifacts/publish/win-x64/Dororong.App.exe` present.

- [ ] **Step 3: Launch the exact published executable safely**

Resolve `artifacts/publish/win-x64/Dororong.App.exe`, start it with `Start-Process -PassThru`, and record the returned PID, resolved executable path, command line, and start time before interaction. Do not identify or terminate the app by process name alone. If the app exits unexpectedly, preserve the first error/crash evidence and do not automatically relaunch by the same mechanism.

- [ ] **Step 4: Execute the complete actual-Windows acceptance matrix**

Use the produced Release executable and apply the `visual-check` skill before any readiness claim. Record a falsifiable observation and PASS, FAIL, or UNVERIFIED for each exact check:

1. borderless alpha-transparent pet remains above an ordinary application;
2. autonomous IDLE and WALK both occur without input;
3. the whole 144×144 pet window remains inside the primary work area and taskbar boundary;
4. slow new approach produces CURIOUS and fast closing approach produces the distinct STARTLED retreat;
5. one body click produces CLICK_REACTION and does not become DRAGGED;
6. movement beyond the system threshold enters DRAGGED, preserves grab offset, clamps on release, and releases mouse capture;
7. approximately 90 seconds without meaningful input enters SLEEP;
8. SLEEP wakes separately through slow approach, fast approach, click without drag, and drag;
9. during IDLE, WALK, SLEEP, and one changing reaction frame, clicking at least four alpha-zero corner/margin points reaches a known control in a different process underneath, while clicking visible body pixels reaches Dororong and not that control;
10. after ordinary body click and drag, typing continues in the previously active work application, proving keyboard focus was not stolen;
11. right-click Exit ends the exact recorded PID and no task-started pet process survives;
12. a continuous nearby cursor does not repeatedly retrigger CURIOUS or visibly flap states.

Visual evidence must come from the executable's rendered states. Automated core tests may support state logic but cannot substitute for checks 1, 3–12.

- [ ] **Step 5: Handle acceptance failures without widening scope**

For a core behavior failure, write the smallest failing regression test that reproduces the observed symptom, confirm RED, make one behavior fix, then rerun the full core suite and the failed Windows check. For a transparency, focus, motion, or rendered-state failure, record the exact failed observation, settle the intended design from the approved spec, make one bounded implementation change, and rerun the failed check plus any dependent checks. Do not add settings, a tray application, an animation engine, or multi-monitor behavior as a workaround.

- [ ] **Step 6: Write the acceptance record from observed facts**

Create `docs/verification/2026-08-26-m1-windows-acceptance.md` only after the checks. Name the Git commit, published executable path and SHA-256, Windows version, .NET runtime, and exact observation for each of the twelve checks above. Overall result is FAIL if any required check fails, UNVERIFIED if none fail but any remains unobserved, and PASS only if all twelve pass.

- [ ] **Step 7: Reconcile README and TASKS at the milestone boundary**

Update `TASKS.md` only from the fresh evidence: mark M1 complete only for an overall PASS; otherwise leave it in progress and name the decisive failed or unverified check once. Link the specification, this implementation plan, README, and acceptance record without copying their full contents. Ensure README's commands exactly match the commands that actually succeeded.

- [ ] **Step 8: Run final repository verification and commit the handoff**

Run:

```powershell
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
dotnet build DororongDesktopPet.sln --configuration Release --no-restore
pwsh -NoProfile -STA -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
git diff --check
git status --short
```

Read the full output and confirm the status contains only the intended README, acceptance record, source/test fixes made from acceptance, and `TASKS.md` update. Then commit:

```powershell
git add README.md TASKS.md docs/verification src tests DororongDesktopPet.sln .gitignore
git commit -m "release: verify Dororong milestone one"
```

After the commit, rerun `git status --short --branch` and verify the worktree is clean before reporting the actual milestone result.
