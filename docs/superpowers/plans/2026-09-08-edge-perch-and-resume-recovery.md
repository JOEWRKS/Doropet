# Edge Perch and Resume Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task-by-task. TASKS.md is the sole progress ledger; checkboxes here describe steps, not a second completion record.

**Goal:** Add intentional forepaw edge perching while separately recovering unintended taskbar penetration after display interruptions.

**Architecture:** Pure edge attachment state consumes visible surfaces and explicit carry-release intent. A separate recovery policy validates ordinary contact after interruptions; presentation uses explicit paw anchors and transparent body masking, never hidden alpha as a foot. PetLoop remains the sole position writer.

**Tech Stack:** Existing .NET8/C#, WPF, xUnit; existing desktop metadata adapter; HTML/Canvas only for a separate rendering preview if useful. No new package dependencies.

**Spec:** `docs/superpowers/specs/2026-09-08-edge-perch-and-resume-recovery-design.md` (approved by user `ㄱㄱㄱ`).

## Global Constraints

- Work in existing linked worktree `D:/JOEWRKS/.worktrees/DororongDesktopPet-m1-expression-animation`, branch `feature/dororong-m1-expression-animation`, HEAD `87e7bb20aee925abcee135703bde46482d136f49`, preserving pre-existing uncommitted work.
- No stage/commit/push/PR/reset/checkout/clean; no old-evidence or approved Assets/frame-sources/saved-runtime edits. No XAML/EOL cleanup.
- Carry-release intent is required. Local-only cheek/body pulls and interrupted input never create perching. Ordinary feet physics must not own a perched position.
- Horizontal visible window/taskbar tops only, initial band0..20 DIP inclusive, entry160ms. Hidden taskbar uses monitor bottom, never an invisible perch.
- Head/face/hair/rose/ribbon source identity stays locked. One pose preview, explicit mask/anchors, separate visual approval before product application. Do not infer anatomical truth from region names.
- Visible alpha and hit testing agree; hidden body must not block clicks. Do not move/resize/activate other apps or read their contents/titles.
- Display-resume original trigger remains unverified until actually observed. Do not force sleep/display-off to test. No synthetic impact energy from recovery translation.
- Tests/builds run serially; WPF tests use `-- xUnit.MaxParallelThreads=1 xUnit.ParallelizeTestCollections=false`. Worker must coordinate build ownership with parent.
- Parent owns baseline, TASKS.md and immutable review packages. Workers do not spawn agents. Existing user choice is subagent implementation plus separate review, not another execution-choice question.
- Evidence root `artifacts/repro/edge-perch-20260908/`; task briefs/reports/review packages `.superpowers/sdd/2026-09-08-edge-perch-and-resume-recovery/`. Preserve both; no cleanup-on-finish because there are no task commits.

## Task 1: Pure edge attachment engine

**Files:** Create `src/Dororong.Core/Platforms/EdgePerch.cs`, `tests/Dororong.Core.Tests/Platforms/EdgePerchTests.cs`. Do not modify existing motion or app integration yet.

**Interfaces:**

```csharp
public enum EdgePerchPhase { None, Entering, Attached }
public readonly record struct PerchContact(double Left, double Right, double GripY, double VisibleTop);
public readonly record struct PerchSurface(PlatformSurface Surface, int ZOrder);
public readonly record struct EdgePerchPose(EdgePerchPhase Phase, PointD Position, SurfaceKey? Owner, long? MonitorId);
public sealed class EdgePerch {
    public EdgePerchPose Current { get; }
    public bool TryBegin(PointD displayed, PerchContact contact, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors, bool carryReleased);
    public EdgePerchPose Advance(TimeSpan delta, PointD displayed, PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces, IReadOnlyList<DesktopMonitor> monitors, bool sceneReliable);
    public void Release(PointD displayed);
}
```

- [ ] Write assertion RED with runnable neutral stubs, not just missing symbols. Literal fixture: monitor(0,0,800,600), window surface key(1,1,1), x100..500/top200, contact(20,50,70,10), displayed(150,140). With carryReleased=true, enter; at160ms position(150,130), ownerkey retained. Without release, no entry. A world grip199.999 or220.001 is rejected;200 and220 accepted.
- [ ] Run `dotnet test tests/Dororong.Core.Tests -c Release --filter FullyQualifiedName~EdgePerchTests --logger trx --results-directory artifacts/repro/edge-perch-20260908/task1-red` and retain assertion failures.
- [ ] Implement finite ordered contact validation; require complete paw interval containment in one surface segment, visible head above monitor top, surface notFloor, valid monitor intersection, and fresh scene via caller. Select by grip-to-top distance, ZOrder, then stable key components. Freeze displayed entry Y and owner-relative X; smoothstep from entry to grip-aligned Y over160ms. Each tick rebuild target from current owner geometry, not previous interpolated target. Release on missing generation/monitor/segment/insufficient headroom; unreliable scene freezes without advancing entry. Caller expires after500ms and explicitly Release.
- [ ] Add concrete tests: move owner+30X/-10Y => target+30X/-10Y once, same key different generation detaches, resize loses either paw =>detach, disjoint segments cannot bridge, Floor rejected, NaN/infinity/contact reversal rejected, invisible top clearance rejected, unavailable scene freezes, regrab Release preserves displayed position, release never produces Landing/Squash. At80ms above fixture Y135, at160Y130. Negative delta does not rewind; long delta completes capped entry without overshoot.
- [ ] Run focused GREEN and Core suite once. Self-review and write task1 report with commands, RED/GREEN counts and exact files. No commit. Parent freezes diff for independent spec+quality review before next implementer.

## Task 2: Independent recovery policy and regression proof

**Files:** Create `src/Dororong.Core/Platforms/TaskbarRecovery.cs`, `tests/Dororong.Core.Tests/Platforms/TaskbarRecoveryTests.cs`. No PetLoop/MainWindow/PlatformMotion modifications in this task.

**Interfaces:**

```csharp
public static class TaskbarRecovery {
    public static PlatformSurface? FindPenetration(PointD displayed, FootContact contact,
        IReadOnlyList<PlatformSurface> surfaces, bool directOwnsPosition, bool explicitlyPerched);
}
```

- [ ] RED test with method returning null: foot(140,165,1080,1000) through visible bar x0..1920/top1040/bottom1080 selects bar. Identical input explicitlyPerched=true or directOwnsPosition=true returns null. Foot exactly1040 is normal contact, not penetration.
- [ ] Run `dotnet test tests/Dororong.Core.Tests -c Release --filter FullyQualifiedName~TaskbarRecoveryTests --logger trx --results-directory artifacts/repro/edge-perch-20260908/task2-red`.
- [ ] Implement pure finite validation, require horizontal valid Taskbar/knownBottom and >=2DIP foot overlap; sole strictly below top+tolerance(.001) and <=bottom+.001. Select highest penetrating valid top, deterministic key tie. Ignore Floor/Window, missing bottom, invalid ranges. No scene queries or state mutation here.
- [ ] Add reproduction consuming actual PlatformMotion: create old-existing-bar condition from diagnosis where foot1080 stays onFloor; FindPenetration selects1040; caller Reset/displayed correction to1040-contact.SoleY then Advance yields Supported/Taskbar with zeroSquash and no later bounce. Add already supported above bar, other-X monitor, hidden bar absent, NaN and bottom tolerance tests. Policy does not claim actual resume trigger known.
- [ ] Run focused GREEN and Core suite; self-review/report task2. Parent task review; no runtime application until Task4.

## Task 3: One head-locked forepaw rendering preview

**Files:** Create only under `artifacts/repro/edge-perch-20260908/pose-preview/`: `index.html`, `preview.js`, `pose-definition.json`, copied exact `canonical.png`, `README.md`. Optional deterministic .NET render-proof harness under this directory, never product Assets.

**Interfaces:** `pose-definition.json` declares source SHA, head mask/source coordinates, two proposed paw regions, GripY and Left/Right contact anchors on96x96 source grid. It is authored geometry, not anatomy verification. Later `PerchContact` is derived through displayed affine transform from this definition, not invented separately.

- [ ] Inspect approved canonical source and existing BodyRegionMap before selecting masks. Copy exact source once and record SHA; do not redraw protected head pixels.
- [ ] Render a browser/code prototype with current source head pixels, proposed paw pose and transparent lower body. Keep one candidate; provide toggleable light/dark/checker backing and a window-edge line, native and nearest enlarged views of that same candidate. No auto-open/start of current desktop pet. Rendering code must disclose all authored shapes and masks.
- [ ] Use explicit source-over alpha composition, equivalent to `drawImage(source,0,0)` within a disclosed source-head clip, then forepaw layer; body outside visible head/paws remains alpha0. Never put an opaque background into exported sprite. Paw tip must remain rounded, with an authored lip over the edge; no arbitrary rectangle cut through paws.
- [ ] Validate protected-head source correspondence under translation, no rescale, alpha0 hidden body, finite anchors and both paws contacting the drawn edge. Inspect actual native/enlarged preview rather than claiming visual correctness from metadata. Any missing source mask decision is reported; do not label author assumptions verified anatomy.
- [ ] Present this single candidate for user art approval. Task3 delivery is review-ready preview, not art-approved. Task4 product presentation/application waits for this explicit choice; pure Tasks1/2 can finish independently. This is an approved spec gate, not an optional execution pause.

## Task 4: Runtime ownership, masked presentation and resume lifecycle integration

**Files:** Create `src/Dororong.App/Runtime/EdgePerchRuntime.cs`, `src/Dororong.App/Runtime/DisplayRecovery.cs`, `src/Dororong.App/Controls/EdgePerchPresentation.cs`, corresponding tests `tests/Dororong.App.Tests/Runtime/EdgePerchRuntimeTests.cs`, `DisplayRecoveryTests.cs`, `Controls/EdgePerchPresentationTests.cs`. Modify narrowly `PetLoop.cs`, `Runtime/PetPlatformRuntime.cs`, `Runtime/DesktopSceneSource.cs`, `MainWindow.xaml.cs`, `Controls/DororongPresenter.xaml.cs`. No XAML edits. This task starts only after Task3 art approval.

**Interfaces:** EdgePerchRuntime owns Task1 engine and approved pose contact; DisplayRecovery consumes Task2 policy. Perch/recovery outputs are consumed at the existing single PetLoop position-write point. Expose `RequestRevalidation()` on recovery and an optional structured diagnostic sink; no filesystem logging in pure Core.

- [ ] RED full-loop tests using existing injectable PetLoopHost pattern: release after a latched head/body/cheek carry can perch; local-only release cannot; unavailable pointer/capture cancellation is not intentional release; repeated queued release consumed once. Regrab starts at actual rendered anchor. Perch entry never also invokes PlatformMotion fall/landing.
- [ ] Implement input intent from controller's actual carried/whole-drag state, not target name alone. Determine current approved neutral perch contact in display coordinates; choose candidate only on release. Hold gravity/autonomy while attached. Build surface candidates with same scene and mapped coordinates; enforce full approved headroom rather than ordinary full-body clearance. Ordinary paths remain unchanged if no candidate.
- [ ] RED WPF render tests compare explicit head pixels, hidden body alpha/hit tests, both facing directions and entry transition. Implement one transparent overlay with approved frozen pose geometry, Restore on release/dispose, no stale clipping of subsequent canonical sprite. Keep physics grip separate from visible-alpha foot measurement. On exit transfer full-pose restoration through bounded animation while retaining visible anchor, then initialize ordinary gravity once.
- [ ] RED recovery tests call RequestRevalidation and simulate supported/perched/direct/falling cases. Revalidate fresh scene at resume/display/topology notification or longgap (>1s), clear stale release intent; do not force a jump for a gap alone. In ordinary verified taskbar penetration use Task2 to align feet, Reset energy and reacquire support, zero artificial Squash. Retain explicit perch only after validating owner+geometry. Short failures freeze;500ms expires and releases stale attachment.
- [ ] Implement lifecycle event subscription with paired cleanup; use current MainWindow HwndSource hook or equivalent bounded notifier. Diagnose before selecting exact Windows event constants using official references. No forced power changes. Emit bounded fields: time/reason/health/host mapping/actual contact/owner/phase/correction and own-window order, never window title/content. Use injectable no-activate own-window order operation only on verified attachment/recovery transitions; avoid continuous Topmost resetting.
- [ ] Run focused tests then full App/Core serially. Cover regrab during entry, taskbar hidden/reappears, moving/resizing/occluding window, scene expiry, monitor removal/mixed scale, fractional HWND readback, no focus steal, hidden body click-through, existing cheek/body/swing/height-landing behavior. Self-review/report and separate task review.

## Task 5: Final evidence and user-trial handoff

**Files:** Evidence `artifacts/repro/edge-perch-20260908/REPORT.md`, `preservation-after.json`, proof renders/testTRX/review records; update TASKS.md only at handoff boundary.

- [ ] Run serial commands:

```powershell
dotnet test tests/Dororong.Core.Tests -c Release
dotnet test tests/Dororong.App.Tests -c Release -- xUnit.MaxParallelThreads=1 xUnit.ParallelizeTestCollections=false
pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
pwsh -NoProfile -File tests/Dororong.App.BodyDragApprovedAssets.Tests.ps1
git diff --check
```

- [ ] Compare before/after approved art and saved runtime hashes. Freeze all task source/test files and reports in one final review package; request whole-feature review on most capable available model, passing the unchanged-runtime and art-approval boundaries. Fix concrete findings through original implementer then scoped re-review.
- [ ] If art accepted and automated gates pass, prepare a separate Release trial runtime, confirm built/tested/published identities. Before launch, report target and verification; replace only exact identified old pet process as separately authorized. Never replace an unrelated process by name.
- [ ] Report simulated vs actual Windows checks separately. Actual display-resume acceptance requires user reproduction with diagnostics and cannot be inferred from gap simulation. No merge/commit/push. Stop at user trial; preserve evidence.
