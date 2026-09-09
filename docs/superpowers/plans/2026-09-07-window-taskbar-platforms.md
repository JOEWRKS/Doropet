# Window and Taskbar Platforms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. TASKS.md remains the single progress ledger; this file specifies execution, not a second status history.

**Goal:** Let Dororong stand/walk on visible window and taskbar tops, follow a supporting window, and fall/squash-land when support disappears, without changing approved direct interactions.

**Architecture:** A read-only native collector publishes immutable physical-pixel scene snapshots at a bounded cadence. Pure geometry and a support/fall controller consume snapshots in one coordinate space; the existing loop arbitrates position ownership and reconciles the brain. An optional presenter layer measures visible contact and applies sole-anchored correction, without editing images or replacing approved interaction renderers.

**Tech Stack:** Existing .NET 8, WPF, C#, xUnit and PowerShell; user32/dwmapi/shell32 read-only queries. No new package dependencies.

**Spec:** `docs/specs/2026-09-07-window-taskbar-platforms.md` (approved by user `ㄱㄱ`).

## Global Constraints

- Worktree: `D:/JOEWRKS/.worktrees/DororongDesktopPet-m1-expression-animation`; branch `feature/dororong-m1-expression-animation`; baseline HEAD `87e7bb20aee925abcee135703bde46482d136f49`.
- 기존 일반 엉덩이 당김, 볼·머리·다리 상호작용과 원본 이미지는 유지한다.
- 드래그/기존 놓기 모션의 소유권이 끝난 뒤 발판 지지/낙하로 인계하며 두 동작이 동시에 위치를 갱신하지 않는다.
- 창 제목/내용은 수집·저장하지 않는다.
- 일반 앱의 이동·크기·포커스·최소화 상태를 변경하지 않는다. 기존 캐릭터 불투명 영역 외에 새 클릭 차단 영역을 만들지 않는다. 자동 숨김 설정도 변경하지 않는다.
- 창 내용 읽기, 앱별 반응, 자율 점프/창 타고 오르기, 가장자리 빼꼼 모션, 창끼리 물리 충돌, 매달림 버전 재도입은 이번 단계에서 하지 않는다.
- 합성 장면 테스트와 실제 Windows에서의 창 이동·최소화·자동 숨김 테스트는 별도로 기록한다.
- Preserve saved runtimes/ZIPs and prior evidence. Do not replace either running pet during implementation/testing. New runtime delivery is a separate announced boundary.
- No commit, push, PR, reset, checkout, clean, unrelated refactor, XAML EOL normalization, source-art change or old-preview rewrite.
- Use apply_patch. Run WPF tests/builds serially to avoid shared build-output locks. Compiler errors alone are not sufficient RED evidence; use runnable minimal stubs where necessary.

## File / responsibility map

| Unit | Files | Responsibility |
|---|---|---|
| Scene contracts and geometry | `src/Dororong.Core/Platforms/DesktopScene.cs`, `PlatformGeometry.cs` | Immutable data, filtering, occlusion, monitor clipping, swept contact |
| Native acquisition | `src/Dororong.App/Interop/DesktopSceneNative.cs`, `src/Dororong.App/Runtime/DesktopSceneSource.cs`, `DesktopCoordinateMap.cs` | Read-only Win32 boundary, capture health, bounded polling, physical-to-loop conversion |
| Motion | `src/Dororong.Core/Platforms/PlatformMotion.cs` | Supported/falling/landing/lifting state, relative support movement, finite floor fallback |
| Presentation | `src/Dororong.App/Controls/PlatformContactPresentation.cs`, narrow hooks in `DororongPresenter.xaml.cs` | Actual raster contact, sole-anchored squash and correction, restore on direct input |
| Composition | `src/Dororong.App/Runtime/PetPlatformRuntime.cs`, `PetLoopRuntime.cs`, `PetLoop.cs`; `src/Dororong.Core/Behavior/PetBrain.cs`, `PetInput.cs` | Single position writer, optional injection, brain reconciliation, correct bounds |
| Verification | New Core/App platform tests; `tools/Verify-WindowPlatforms.ps1` | Synthetic, native metadata, regression and preservation evidence separated |

## Task 1: Scene geometry and first-contact selection

**Files:** Create Core files in the map and `tests/Dororong.Core.Tests/Platforms/PlatformGeometryTests.cs`.

**Interfaces:** All Core platform types below live in `Dororong.Core.Platforms`; public because App consumes Core. Physical pixels for raw scene; geometry accepts the same shapes after the runtime maps all coordinates together. Never mix spaces within a call.

```csharp
public enum PlatformKind { Window, Taskbar, Floor }
public readonly record struct SurfaceKey(long Handle, uint ProcessId, long Generation);
public sealed record DesktopMonitor(long Id, RectD Bounds);
public sealed record DesktopWindow(SurfaceKey Key, RectD Bounds, int ZOrder,
    bool Visible, bool Minimized, bool Cloaked, bool Excluded,
    bool CanSupport, bool CanOcclude, bool Taskbar, bool HorizontalTaskbar);
public sealed record DesktopScene(long Revision, TimeSpan CapturedAt,
    IReadOnlyList<DesktopMonitor> Monitors, IReadOnlyList<DesktopWindow> Windows);
public readonly record struct PlatformSurface(SurfaceKey Key, long MonitorId,
    PlatformKind Kind, double Left, double Right, double Top, PointD OwnerOrigin);
public readonly record struct FootContact(double Left, double Right,
    double SoleY, double VisibleTop);
public static class PlatformGeometry
{
    public static IReadOnlyList<PlatformSurface> Build(DesktopScene scene, double bodyHeight);
    public static PlatformSurface? FirstCrossing(IReadOnlyList<PlatformSurface> surfaces,
        FootContact before, FootContact after);
}
```

- [ ] Write runnable RED cases for occlusion and swept landing with literal expected intervals. Example scene: monitor `(0,0,1000,800)`, rear window `(100,400,600,300)` at z=1; front window `(250,300,200,300)` at z=0. Rear top must split into `[100,250)` and `[450,700)` at y400. Hidden/minimized/cloaked/self windows must neither support nor occlude. A visible non-supporting popup can still occlude.

```csharp
[Fact]
public void Fast_fall_hits_first_top_not_lower_top()
{
    var upper = new PlatformSurface(new(1,1,1),1,PlatformKind.Window,0,300,300,new(0,300));
    var lower = new PlatformSurface(new(2,1,1),1,PlatformKind.Window,0,300,500,new(0,500));
    var hit = PlatformGeometry.FirstCrossing(new[] { lower, upper },
        new(100,120,250,160), new(100,120,650,560));
    Assert.Equal(upper, hit);
}
```

- [ ] Run `dotnet test tests/Dororong.Core.Tests -c Release --filter FullyQualifiedName~PlatformGeometryTests`; retain actual failing assertions in fresh evidence.
- [ ] Implement rectangle intersection and interval subtraction, retaining owner identity across splits. Top must belong to the actual window, not a monitor-clipped rectangle's artificial top. Reject surfaces with less than bodyHeight space above within that monitor. Add one floor per actual monitor, never one virtual-desktop bounding-box floor. Taskbar supports only horizontal visible top with enough headroom; a hidden edge strip is not a platform.

```csharp
// Subtract [occluderLeft, occluderRight) from [left,right).
if (occluderLeft > left) remaining.Add((left, Math.Min(right, occluderLeft)));
if (occluderRight < right) remaining.Add((Math.Max(left, occluderRight), right));
// Only process an occluder when its vertical range covers the candidate top.
// Swept contact: evaluate X overlap at the crossing time, not only the end point.
double t = (surface.Top - before.SoleY) / (after.SoleY - before.SoleY);
double l = before.Left + (after.Left - before.Left) * t;
double r = before.Right + (after.Right - before.Right) * t;
bool overlaps = r > surface.Left && l < surface.Right;
```

- [ ] Add negative-monitor-origin, disconnected-monitor gap, diagonal sweep, same-height z-order, zero/negative delta-Y, malformed/NaN rectangles, offscreen top, tiny segment and vertical-taskbar cases. Require at least 2 coordinate units contact overlap so one antialias fringe cannot carry the body.
- [ ] Rerun focused tests GREEN and full Core tests. Review independently testable geometry; no commit.

## Task 2: Read-only native scene source and coordinate contract

**Files:** Create `src/Dororong.App/Interop/DesktopSceneNative.cs`, `src/Dororong.App/Runtime/DesktopSceneSource.cs`, `src/Dororong.App/Runtime/DesktopCoordinateMap.cs`; `tests/Dororong.App.Tests/Runtime/DesktopSceneSourceTests.cs`, `tests/Dororong.App.Tests/Runtime/DesktopCoordinateMapTests.cs`. The internal IDesktopSceneNative interface can live with DesktopSceneSource and is implemented by the Interop unit.

**Consumes:** Task 1 records. **Produces:**

```csharp
internal enum SceneReadHealth { Fresh, Cached, TemporarilyUnavailable, Expired }
internal sealed record SceneRead(DesktopScene? Scene, SceneReadHealth Health);
internal interface IDesktopSceneNative { DesktopScene? Capture(TimeSpan now); }
internal sealed class DesktopSceneSource
{
    internal DesktopSceneSource(IDesktopSceneNative native);
    internal SceneRead Read(TimeSpan now);
}
internal readonly record struct DesktopCoordinateMap(PointD PhysicalOrigin,
    PointD LogicalOrigin, double ScaleX, double ScaleY)
{
    internal PointD ToLogical(PointD p) => new(
        LogicalOrigin.X + (p.X - PhysicalOrigin.X) / ScaleX,
        LogicalOrigin.Y + (p.Y - PhysicalOrigin.Y) / ScaleY);
    internal PointD ToPhysical(PointD p) => new(
        PhysicalOrigin.X + (p.X - LogicalOrigin.X) * ScaleX,
        PhysicalOrigin.Y + (p.Y - LogicalOrigin.Y) * ScaleY);
}
```

- [ ] RED test source throttling with an injected native adapter returning actual scene records: reads at 0/16/32/48ms must publish one capture; 80ms must capture again. Successful empty scene is Fresh and removes support; null capture at80ms retains prior scene as TemporarilyUnavailable. At >=500ms since last success, return Expired, not Fresh. Exception at native boundary is converted to failed capture, not app shutdown. Preserve failure diagnostics without window titles.
- [ ] RED coordinate case, independently derived:

```csharp
[Fact]
public void Negative_origin_and_nonuniform_scale_keep_contact_in_pet_space()
{
    var map = new DesktopCoordinateMap(new(-1500,300),new(-1000,200),1.5,2);
    Assert.Equal(new PointD(-900,250), map.ToLogical(new(-1350,400)));
    Assert.Equal(new PointD(-1350,400), map.ToPhysical(new(-900,250)));
}
```

- [ ] Run focused App tests RED. Implement polling with monotonic elapsed time, no timer thread racing WPF. Reject invalid scales; make scene snapshots immutable copies. For a new/reappearing HWND increment generation, include owner PID, and never carry stale identity across successful absence. Null whole-scene read is different from a successfully queried invisible window.
- [ ] Implement `DesktopSceneNative.Capture`: enumerate top-level HWNDs, read geometry/visibility/minimized/cloaked, owner PID and class only for classification; no title/text APIs. Exclude current pet HWND, desktop shell surfaces, and other Dororong instances (identify executable/class, do not broadly exclude all WPF). Menus/tooltips that cannot support can still occlude. Preserve top-to-bottom z-order; bound any handle traversal and handle mid-read destruction. Failed cloaking/frame query is uncertainty, never proof that a surface vanished.
- [ ] Prefer DWM visible frame bounds to avoid invisible resize borders; collect monitor physical rectangles through EnumDisplayMonitors/GetMonitorInfo in a known DPI context and restore thread context in finally. Use ABM_GETTASKBARPOS only for system taskbar metadata; enumerate primary/secondary shell taskbar HWNDs and query per-monitor auto-hide appbar metadata. Class-name detection is a documented heuristic/fallback, not a universal OS guarantee. Use current clipped HWND geometry for visibility; ABM auto-hide flag alone is not current hidden state. Hide when exposed thickness <=2 physical pixels; require stable >2px visible thickness before restoring support. Never call appbar SET messages.
- [ ] Runtime coordinate mapping is anchored to actual presenter screen origin (`PointToScreen`) and viewport logical origin, with actual screen displacement of one local DIP as scale. Do not assume global physicalX/currentDPI conversion. Rebuild map when host position/DPI changes and rebase all scene/contact positions together. Characterize pointer-vs-map agreement and physical/logical roundtrip at the native boundary. If cross-monitor WPF virtualization breaks this affine contract, stop runtime delivery and repair the adapter; do not claim mixed-DPI support from one-monitor synthetic tests or change DPI manifest silently.
- [ ] GREEN tests include clock rollback, two failed reads followed by recovery, destroyed HWND, handle reuse, taskbar hide/show, secondary monitor taskbar, and error-vs-empty. Record read-only current-desktop enumeration as metadata evidence only. No user-window manipulation or runtime replacement.

Native references to read before coding P/Invokes:

- [GetWindowRect and physical DWM bounds](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect)
- [DWM attribute definitions](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
- [EnumWindows](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows)
- [ABM_GETTASKBARPOS](https://learn.microsoft.com/en-us/windows/win32/shell/abm-gettaskbarpos)
- [ABM_GETAUTOHIDEBAREX](https://learn.microsoft.com/en-us/windows/win32/shell/abm-getautohidebarex)
- [PhysicalToLogicalPointForPerMonitorDPI semantics](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-physicaltologicalpointforpermonitordpi)

## Task 3: Supported movement, swept falling and taskbar lifting

Controller interface clarifications: Task5 handles monitor-topology change/full-visible-body clamp using mapped monitor rectangles, calls Reset(actual clamped host position), then Advance. Do not infer missing monitor bounds from floors. Add non-positional nullable `PlatformSurface.Bottom { get; init; }`; narrow changes to PlatformModels.cs, PlatformGeometry.cs and PlatformGeometryTests.cs are authorized to populate clipped taskbar bottom and test it. Existing constructors stay compatible. Taskbar lift requires actual foot overlap with its known [Top,Bottom] and same monitor/lower support; null bottom cannot trigger lift. Include a floating-bar counterexample where feet below its rectangle are not lifted.

**Files:** Create `src/Dororong.Core/Platforms/PlatformMotion.cs`, `tests/Dororong.Core.Tests/Platforms/PlatformMotionTests.cs`.

**Consumes:** `PlatformSurface`, `FootContact`. **Produces:**

```csharp
public enum PlatformPhase { Suspended, Supported, Falling, Landing, Lifting }
public readonly record struct PlatformPose(PlatformPhase Phase, PointD Position,
    SurfaceKey? Support, double Squash, double Sway);
public readonly record struct PlatformMotionInput(TimeSpan Delta, PointD DisplayedPosition,
    PointD DesiredPosition, FootContact LocalContact,
    IReadOnlyList<PlatformSurface> Surfaces, bool DirectOwnsPosition, bool SceneReliable);
public sealed class PlatformMotion
{
    public PlatformPose Current { get; }
    public PlatformPose Advance(PlatformMotionInput input);
    public void Reset(PointD displayedPosition);
}
```

- [ ] RED movement cases with literal contact `(40,80,110,20)`: position `(100,190)` is supported on top300. Owner moves `(20,-30)` → position `(120,160)` (feet at270). A later desired walking X delta5 → `(125,160)`; retain sole height, not brain's proposed Y. Walking beyond support or resize that removes overlap starts falling at the last displayed location, not original startup position.
- [ ] RED fall case: release at sole250 with platforms300/500; using a 250ms tick still lands on300. Previous support removed in a reliable scene starts positive downward displacement, but a single unreliable scene does not create a fall. First frame of direct ownership clears support and returns DesiredPosition exactly; no gravity or squash.
- [ ] Implement integration in <=8ms substeps, total delta capped250ms and no accumulating stale backlog. Use acceleration1800DIP/s² and downward speed cap1200DIP/s as initial tuning. FirstCrossing handles swept intervals. On hit set `Position.Y = surface.Top - LocalContact.SoleY`, velocity0 and Landing. Landing lasts240ms; squash `0.12*sin(pi*p)*(1-p)` for p in[0,1], with a brief <=0.03 rebound in the second half. Keep soles pinned throughout. These are first-implementation tuning, not user-approved feel.

```csharp
double seconds = Math.Clamp(input.Delta.TotalSeconds, 0, .25);
double nextVelocity = Math.Min(1200, velocity + 1800 * seconds);
double nextY = position.Y + (velocity + nextVelocity) * .5 * seconds;
// Run this integration per bounded substep, then FirstCrossing before committing nextY.
// Follow support translation from owner origin, not clipped segment's left boundary.
PointD ownerDelta = newOwnerOrigin - previousOwnerOrigin;
```

- [ ] Implement taskbar reappearance only when a visible taskbar newly intersects current feet on that same monitor and current support is lower floor/no higher window. Lift from last actual Y to `bar.Top - soleY` over160ms using smoothstep `p*p*(3-2*p)`. Track moving destination while visible; bar disappearing during lift starts fall at current interpolated position. Never pull a pet down from a higher window.
- [ ] At unreliable scene <=500ms freeze support relation, not jump. At Expired the runtime supplies monitor floors only; release stale window support without indefinite floating. On reappearance after expiry reacquire by actual contact/crossing, not stale handle. Monitor removal uses nearest surviving monitor with visible-body clamp once, recorded separately as topology change, not ordinary support loss.
- [ ] GREEN cases: close/minimize/occlude; four movement directions; resizing from both edges; walking off; taskbar hide/show mid-lift; floor fallback; NaN/nonpositive deltas; upper surface recovery; direct regrab during fall/landing. Full Core regression. No commit.

## Task 4: Visible sole measurement and non-destructive presentation

Producer/consumer convention: PlatformPose.Sway is radians, bounded by the current producer to +/-0.03. Convert exactly once using Sway*180/Math.PI and clamp to +/-2 degrees at the presenter; test the nonzero mapping explicitly. The producer's original file has no units comment, so this recorded interface clarification is authoritative.

Contact-width convention: after transforming alpha>=128 opaque pixel quadrilaterals, compute actual SoleY and clip them to the presenter-space [SoleY-2,SoleY] support band; exclude zero-area band-top touches. Left/Right is that band envelope, not whole-head/body width and not a source-bottom-row-only assumption. VisibleTop is full silhouette minimum. Test wide upper body/narrow sole and rotation with independent literals. Full visible-body X bounds for monitor clamp are separate. Current DTO is a single envelope, not a multi-contact-island solver.

**Files:** Create `src/Dororong.App/Controls/PlatformContactPresentation.cs`, `tests/Dororong.App.Tests/Controls/PlatformContactPresentationTests.cs`; narrow optional hooks in `src/Dororong.App/Controls/DororongPresenter.xaml.cs`, no XAML edits.

**Consumes:** `PlatformPose`, rendered BitmapSource and actual image-to-presenter transform. **Produces:**

```csharp
internal sealed class PlatformContactPresentation
{
    internal FootContact Measure(BitmapSource source, GeneralTransform imageToPresenter);
    internal void Apply(FrameworkElement body, FootContact current,
        double targetSoleY, double squash, double sway);
    internal void Restore();
}
// Presenter hooks, existing Render overloads remain behavior-identical by default:
internal FootContact? MeasurePlatformContact();
internal void ApplyPlatformPose(PlatformPose? pose, double? targetSoleY = null);
```

- [ ] RED STA tests use a literal small Pbgra bitmap: opaque body at x2..5,y2..6, faint alpha10 fringe at y7. With translate(10,20), contact sole is27 (bottom boundary after last alpha>=128 row), not image-height28 and not logical presenter144. Track transformed opaque pixel corners, not only an untransformed bounding box. Cache source contact rows/pixels for immutable frames; no screen capture needed.
- [ ] Render current idle/walk/curious/sleep frames through real presenter and measure transformed sole. Apply platform correction and verify world-space sole equals top within <=0.5DIP for phase0/.25/.5/.75 and both facings. Use 336 host with144 presenter to catch gutter mistakes. Lower support envelope of the current frame is the contact for sleep; do not invent hidden feet.
- [ ] Implement platform transform as a separately owned composition layer. Task5 passes the same local contact SoleY used by motion as explicit targetSoleY; a new pose alone does not contain the contact target. Apply squash/sway around contact center, then translate Y to cancel measured sole drift. Bounded sway <=2degrees, no foot sliding. For direct transitions, classify and capture the ACTUAL visible transform first, then retire the platform layer without double-applying or dropping the captured offset. Existing head-anchor capture occurs near the beginning of Render and must still see the previous displayed pose; existing body/cheek capture occurs at the press handler. Restore base transforms after required capture, before base pose/overlay rendering, on cancellation/disposal and explicit clear. Resetting before hit classification/capture is forbidden because it would move the target under the pointer. No rectangular input overlay.

```csharp
double correctionY = targetSoleY - measuredAfterPose.SoleY;
// Append correction after scale/rotation; do not modify source pixels or the
// approved BodyPullPresentation/CheekPullPresentation output.
var correction = new TranslateTransform(0, correctionY);
```

- [ ] Verify no-overlay output is byte-identical to baseline samples, and begin-drag while corrected preserves grabbed screen point on first frame. Include body/cheek early-return rendering branches so a stale platform transform cannot leak into direct motion.
- [ ] Run focused GREEN and existing presenter/direct-interaction regressions serially. Report synthetic rendered-contact assertions, not visual approval.

## Task 5: Loop ownership, brain reconciliation and production wiring

Integration carry-ins: Task2 Expired has Scene=null; retain/refetch monitor-floor geometry independently, never treat null as a successful monitorless scene. Task5 owns monitor-removal detection, nearest surviving monitor/full-visible-body clamp and Reset from actual host position. Resolve Task2's deferred off-axis-NaN sample validation with a failing finite-sample regression before changing the adapter. Carry the native mid-query destruction coverage limitation to final review if this boundary is not extended here.

Task4 full-bounds integration: narrowly expose full opaque min/max X/Y from PlatformContactPresentation's existing transformed-corner pass and a presenter combined contact/bounds measurement hook. Do not duplicate bitmap scans in runtime or use FootContact.Left/Right as full-body bounds. Preserve existing measurement/application hooks and capture ordering; cover combined bounds with independent literal tests. These narrow additions to the helper/presenter/test are authorized Task5 integration work. Time native capture separately before relying on synchronous UI collection; a prior test took70ms including JSON/test overhead, not a collector benchmark.

Startup/expired fallback: Task5 may narrowly extract/reuse monitor-only enumeration in DesktopMetadataReader so failed full-window snapshots cannot remove the startup floor or hide monitor topology changes. Preserve DPI restoration and bounded enumeration/error semantics; cover fallback deterministically and avoid duplicating native enumeration logic.

**Files:** Create `src/Dororong.App/Runtime/PetPlatformRuntime.cs`, `tests/Dororong.App.Tests/Runtime/PetLoopPlatformTests.cs`, `tests/Dororong.Core.Tests/Behavior/PlatformPositionTests.cs`; modify `src/Dororong.App/Runtime/PetLoopRuntime.cs`, `src/Dororong.App/PetLoop.cs`, `src/Dororong.Core/Behavior/PetBrain.cs`, `src/Dororong.Core/Behavior/PetInput.cs` and narrowly `src/Dororong.App/Runtime/PetWindowViewport.cs` only if coordinate characterization requires it. Extend explicit-source script harness lists if their compilation needs new files, without weakening assertions.

**Consumes:** Tasks1–4. **Produces:**

```csharp
internal sealed class PetPlatformRuntime
{
    internal bool SuspendsAutonomousMotion { get; }
    internal RectD GetMovementArea(PointD position, FootContact contact, SizeD petSize);
    internal PlatformPose Advance(TimeSpan elapsed, TimeSpan delta, PointD displayed,
        PointD proposed, FootContact contact, bool directOwnsPosition);
    internal void Reset(PointD displayed);
}
// Optional property on PetLoopHost keeps all existing constructor callers valid.
internal PetPlatformRuntime? Platforms { get; init; }
// Core explicit physics reconciliation, accepts finite position; preserves state,
// facing, phase and direct-grab offset. Only invoked when platform owns position.
public PetSnapshot ApplyPlatformPosition(PointD position);
// Add trailing optional PetInput bool SuspendAutonomousMotion = false.
```

- [ ] RED core test: `ApplyPlatformPosition(new(120,160))` then next idle Update must not snap back to old position. Nonfinite input throws ArgumentOutOfRangeException without changing state. Suspension still processes direct presses/capture transitions but does not advance autonomous walking/retreat position. Existing default false behavior unchanged.
- [ ] RED real PetLoop harness cases: supporting window moves +20X/-30Y, verify host position and emitted core snapshot both move exactly once; next tick no snapback. Head, five body targets and captured cheek drag must retain old position/capture behavior while a support moves/disappears under them. Existing release/HeadLanding must finish before any platform gravity, including quick release and loss-of-capture path.
- [ ] Implement ordering: sample actual host position and mapped scene → process queued direct press → run current direct/controller/brain pipeline → resolve complete ownership including previous/current target, pending press, Dragged, HeadLanding, body/cheek settle → if direct owns, suspend platform motion and let Task4 retire its layer AFTER the visible-pose capture required by the interaction → otherwise Advance platform, ApplyPlatformPosition, write final host position once → base Render once → apply optional platform presentation with the same local SoleY used by motion and measure actual next contact. Do not clear the visual layer before Render's existing head-anchor capture sees the previous display. Reconcile an OS-moved host into brain before autonomous Update when platform-owned; previous snapshot position is not a substitute for actual host position. Preserve last actual displayed contact across handoff, not an assumed canonical sole from before release.

```csharp
// Capture displayed from _host.GetWindowPosition() at tick entry, before position writes.
bool directOwnsPosition = current.State == PetState.Dragged ||
    current.IsDirectInteractionPending || queuedPress is not null ||
    directCurrent.Target != DirectInteractionTarget.None ||
    directCurrent.HeadLanding is not null;
// Also retain ownership for a session active at tick entry but ending this tick.
// On the following tick seed platform from the last actual rendered contact.
if (!directOwnsPosition && _host.Platforms is { } platforms)
    current = brain.ApplyPlatformPosition(platforms.Advance(elapsed, delta,
        displayed, current.Position, contact, false).Position);
```

- [ ] Platform-enabled movement area uses selected monitor full bounds, not primary SystemParameters.WorkArea. Convert visible-bounds constraints into the existing ClampTopLeft contract: minX=monitor.Left-visibleLeft, maxX=monitor.Right-visibleRight; minY=monitor.Top-visibleTop, maxY=monitor.Bottom-soleY; return RectD(minX,minY,maxX-minX+petWidth,maxY-minY+petHeight). Do not create a bounding rectangle bridging monitor gaps. During pointer-driven carry select monitor from actual pointer/visible pet and reconcile mapping before clamp. With Platforms=null preserve old GetWorkArea path exactly.
- [ ] Wire production native reader and presenter hooks; no platform work for legacy host tests unless opt-in. On disposal/cancel/fault clear physics/presentation. Check no double release, no stale correction on immediate regrab, no input interception outside current alpha. Cache collection at80ms; support movement can be observed with a bounded one-HWND query between full enumerations if latency warrants it, never enumerate all windows every16ms.
- [ ] GREEN all new loop tests plus App/Core regressions. Update compile-linked harness dependency lists only where execution shows missing new dependencies. No running-app replacement or commit.

## Task 6: Verification, scoped review and trial handoff

**Files:** Create `tools/Verify-WindowPlatforms.ps1`; write fresh ignored evidence under `artifacts/repro/window-taskbar-platforms-20260907-attempt-1/`; update TASKS.md at milestone only.

- [ ] Before implementation, record exact HEAD/branch/status and SHA256 for current src/tests/tools/assets, selected checkpoint records and both saved version folders/ZIPs. Inventory all earlier artifact paths/size/mtime; report this older-artifact check as metadata preservation, not exhaustive byte identity. Runner must refuse overwriting an existing evidence run directory. Use a new numbered run for retries; preserve RED/failure logs.
- [ ] Execute serial fresh tests with recorded exit codes:

```powershell
dotnet test tests/Dororong.Core.Tests -c Release --logger 'trx;LogFileName=core.trx' --results-directory artifacts/repro/window-taskbar-platforms-20260907-attempt-1/final-tests
dotnet test tests/Dororong.App.Tests -c Release --logger 'trx;LogFileName=app.trx' --results-directory artifacts/repro/window-taskbar-platforms-20260907-attempt-1/final-tests -- xUnit.MaxParallelThreads=1 xUnit.ParallelizeTestCollections=false
pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.BodyDragApprovedAssets.Tests.ps1
git diff --check
git status --short
```

- [ ] Inspect source diff, freeze current changed files and get scoped code review using requesting-code-review skill. Review the final frozen diff and test evidence, not an earlier mutable draft. Address blockers test-first and rerun affected plus final suites. No review verdict implies user visual acceptance.
- [ ] Record native read-only current scene, API failures, collection duration and coordinate roundtrip. Native probes may create/close their own test windows for movement/minimize cases in a separately described run; never move/minimize user windows. Do not toggle user taskbar auto-hide setting. If physical desktop automation is unavailable, mark actual window-follow/auto-hide/compositor observations UNVERIFIED and provide manual trial steps.
- [ ] Trial steps: place pet above a normal window and release; move support left/right/up/down; shrink edge past feet; minimize/close support; walk off; drop through two stacked tops; use taskbar visible/hide/reappear; test second monitor/different DPI; regrab during fall; check cheek/head/paws/plain rump. Verify feet vs top and no old-release/platform double drop independently.
- [ ] Recheck approved assets and saved ZIP hashes; only intended source/test/plan paths may differ. Preserve browser preview and running PIDs. Build/publish into a new trial directory only after tests/review; compare runtime App/Core DLL hashes with tested binaries. Announce exact old PID/path and new runtime before any separately authorized replacement; avoid claiming install merely from a build.
- [ ] Final report states implementation/test counts, byte preservation, native metadata coverage, actual-desktop UNVERIFIED rows, runtime applied/not applied and no commit/push. Link trial/verification files. Stop at user trial, not broad final M1 approval.

## Plan self-review

- Spec coverage: visible-window filtering/occlusion and monitor floors Task1; read-only acquisition/DPI/auto-hide/failure health Task2; support loss/follow/lift/swept fall Task3; visible contact and squash Task4; direct ownership and old behavior compatibility Task5; preservation, separate synthetic/native evidence and handoff Task6.
- Producer/consumer names above are shared contracts. Task5 owns the integration boundary; coordinate space is converted once per snapshot before Core consumption.
- No stage/commit/push step is included because that authorization applied to the completed ordinary-rump checkpoint, not this feature.
- Open validation risks are explicit tests/gates, not assumed successes: mixed-DPI WPF mapping, native taskbar visibility heuristics, protected/unsupported windows, compositor feel, and measured contact during pose handoff.
