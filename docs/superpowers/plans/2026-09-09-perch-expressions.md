# Perch Expressions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax; TASKS.md remains the sole plan ledger.

**Goal:** Preserve visible facing across lifting/perching and add pinned entry motion, blink, and attached-only cheek pull.
**Architecture:** One integrated production task owns the shared presenter/input/perch state boundaries; separate evidence/release task consumes its tested immutable output. Extend existing semantic input and presentation components, not a second physics owner.
**Tech Stack:** C#/.NET8 WPF, xUnit, PowerShell artifact gates.
**Spec:** docs/superpowers/specs/2026-09-09-perch-expressions-design.md

## Global Constraints

- Keep current attachment eligibility exactly: whole carry release, neutral grip0..20DIP below eligible visible edge, both paws contained, headroom,160ms entry and500ms expiry.
- Source9 and all existing assets remain byte-exact; existing ground interactions and accepted low-head touchdown stay unchanged.
- Actual displayed facing at press persists through lift/carry/perch; mouse motion does not flip it.
- Cheek pull while attached retains owner/grip and never escalates to carry; blink paused open until cheek release ends.
- No commits/staging/push/reset/cleanup or overwriting old runtime. Parent owns TASKS.md, independent review and deployment.

## Task 1: Integrated facing, perch expression and local-input lifecycle

**Files:** Modify narrow sections of src/Dororong.App/Controls/DororongPresenter.xaml.cs, Controls/EdgePerchPresentation.cs, Interaction/DirectInteractionController.cs, Interaction/DirectInteractionSnapshot.cs, the existing DirectInteractionPressEventArgs definition, PetLoop.cs, Runtime/PetPlatformRuntime.cs and Runtime/PetLoopRuntime.cs as necessary. New focused Controls/PerchExpressionFrames.cs and Controls/PerchExpressionMotion.cs may hold registered image copies and visual-clock sampling. New tests in tests/Dororong.App.Tests/Controls/PerchExpressionTests.cs and Runtime/PerchLocalInteractionTests.cs; existing integration harnesses remain reusable. Avoid Core changes unless a demonstrated semantic boundary requires one; report before changing Core.

**Interfaces:** Carry actual visible press-facing and local-attached-cheek intent as optional/default-preserving data on existing press/snapshot types. Existing constructors/reflection contracts remain compatible. Perch presentation must expose enough production state to select current visible source; use a separate local interaction flag to preserve position ownership, not a target-name heuristic. Explicit cancel/reset clears all local state.

- [ ] Read the binding spec, current presenter press/capture path, Core direction updates and runtime release/position ownership before editing. Record exact files and capture baseline hashes in parent snapshot.
- [ ] Add runnable RED through existing real presenter/full-loop harness: starting in each actual visible facing, press→pending→hold→release/perch retains same orientation. Assert the composed mirror/landmark and carried facing rather than private string matches. A left-facing press must not become the source-default orientation during pending.
- [ ] Add RED for pinned presentation sampling and eyes: at0/80/180/320ms entry, sample source grip remains at same world edge while an upper-head landmark moves and returns; advancing the attached clock yields distinct eye states with non-eye/paw pixels unchanged; negative/long delta stays bounded. Literal invariant example:
```csharp
Assert.Equal(gripBefore, gripAfter);
Assert.NotEqual(openEyePixels, closedEyePixels);
Assert.Equal(originalPawPixels, blinkingPawPixels);
```
- [ ] Add RED attached cheek flow using real press classification/capture: both facings, pull20 and100DIP, retained owner and unchanged window position after stable support, visible source body/paws unchanged, release restores image09 with no hidden/duplicate canonical. Moving support translates once; loss of owner cancels local cheek and falls once; non-cheek regrab detaches. Ordinary ground cheek still carries beyond its existing threshold.
- [ ] Run focused RED `dotnet test tests/Dororong.App.Tests -c Release --filter "FullyQualifiedName~PerchExpression|FullyQualifiedName~PerchLocalInteraction" --logger trx --results-directory artifacts/repro/perch-expressions-20260909/red`; retain assertion failures, not only missing-symbol errors.
- [ ] Implement semantic press facing before any ResetPose/default state can overwrite it; keep that facing in the carry/attachment handoff. Implement one visual lifecycle for entry/blink/local cheek, preserving physical grip and resetting owned clip/visibility at the right seam. Register image09 onto the existing96px cheek coordinate system only on a runtime copy with affine compensation; preserve every nontransparent source pixel. Reuse authored eye differences aligned to image09, no new artwork.
- [ ] Run focused GREEN and old edge/partial-head/cheek tests; produce real WPF native/enlarged sequence proofs for both facings, entry/blink/pull/release. Render source identity and outside-eye/unchanged-body assertions; report any art/registration ambiguity to parent.
- [ ] Freeze source after focused GREEN for task review, then run full App/Core serially once. Write report with RED/GREEN paths, exact changed files, choices, current hashes and limitations. No publish/restart by worker.

## Task 2: Evidence, independent review and normal product handoff

**Files:** artifacts/repro/perch-expressions-20260909/REPORT.md, preservation/review packages; TASKS.md. No production edits by this task; fixes go back to Task1 implementer.
**Interfaces:** consumes frozen Task1 files, tests and render sheets; produces verified new normal runtime with exact tested/published identity.

- [ ] Parent inspect real WPF proof images and run existing RuntimeComposition, DirectInteractionRender, ExactArt and BodyDragApprovedAssets PS1 gates against final source.
- [ ] Independent Task1 spec/quality review against before snapshot; return concrete issues to the same worker, then scoped re-review. Preserve all evidence.
- [ ] Verify all prior embedded resources/source assets unchanged and no Core eligibility change; read full test counters and compare final tested DLLs to publish.
- [ ] Final whole-feature review on most capable model; distinguish native capture failure0x80004002 from rendered/controlled input tests.
- [ ] Publish new runtime `C:/Users/tjdwo/Downloads/doro/perch-expressions-20260909/runtime`. Validate exact currently running pet/diagnostic PID/path before stopping only it; start normal candidate hidden, verify responding/module paths. Preserve previous runtime and no commit/push.
- [ ] Record evidence/review/PID/hash and hand off for user's live trial, without claiming physical-pointer testing that Computer Use could not perform.
