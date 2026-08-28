# Dororong M1 Stage A Closed-Eye and Sleep Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the detached-looking closed-eye treatment with a character-preserving resting expression and give SLEEP a restrained breathing pose with an immediate clean wake into the existing reaction states.

**Architecture:** Keep the approved canonical open frame and body-art pipeline authoritative. Change only the deterministic source-eye lid runs used to derive the closed frame, then adjust only the existing WPF presenter transforms for SLEEP; do not add a new behavior state or animation framework. Core state timing and transitions remain unchanged.

**Tech Stack:** C# / .NET 8 / WPF, PowerShell 7, System.Drawing-based deterministic art generator, xUnit core tests, PowerShell WPF integration tests.

**Spec:** `docs/specs/2026-08-26-dororong-m1-design.md`

**Roadmap:** `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`

## Global Constraints

- Work only on `feature/dororong-m1-expression-animation`, which started at frozen checkpoint `cc04e67e8cc330d9afe0af607b66188527a42cd4`.
- Preserve the exact canonical open-frame bytes and the user-approved no-tail body outline.
- Preserve head shape, hair, rose, bow, ribbons, mouth, proportions, transparent background, and all non-eye pixels.
- Use the existing source-to-96px generator; do not hand-replace the 96px runtime sprite with a separately redrawn image.
- The one built-in image-edit candidate created during planning was rejected because it redrew protected regions. Do not copy it into the project or repeat that method for this task.
- Do not change PetState, PetBrain, behavior priority, timings, click-versus-drag rules, alpha hit testing, click-through behavior, focus behavior, topmost behavior, or work-area bounds.
- SLEEP continues to use the existing 2.4-second repeating phase from `PetBrain`.
- A body press while asleep continues to wake immediately; release below the drag threshold becomes CLICK_REACTION and threshold crossing becomes DRAGGED.
- Do not publish into `artifacts/publish/win-x64`; use a new Stage-A-specific output path.
- Build, test, asset inspection, and process liveness do not prove actual Windows rendering. Required user observations remain UNVERIFIED until directly reported.
- Do not push, create a PR, merge, or modify the frozen phase-1 branch.

## Design-to-Visual Check Contract

| ID | Expected observable | evidenceLayer | applicability | semantics |
|---|---|---|---|---|
| A-EYE-1 | The canonical open frame and every pixel outside the reviewed source/native eye support remain unchanged. | source/runtime asset identity | always: open and closed assets | acceptance |
| A-EYE-2 | Both closed eyelids form smooth shallow `⌣` curves: their center sits gently below their outer endpoints and aligns horizontally with the corresponding open-eye center, at matching visual height, with the open iris/pupil/lower oval fully removed. | exact 96x96 asset at native and nearest-neighbor enlarged scale | closed-eye frame | acceptance |
| A-EYE-3 | Mouth, face boundary, hair, decorations, body silhouette, thin outline, and no-tail reading match the approved canonical frame. | exact 96x96 asset comparison | closed-eye frame | acceptance |
| A-SLEEP-1 | SLEEP always uses the closed-eye frame, remains gently lowered, and changes both scale and vertical position over its 2.4-second phase without severe squash. | WPF presenter state/phase | SLEEP at phases 0.25 and 0.75 | acceptance |
| A-WAKE-1 | Rendering a wake reaction after SLEEP clears the sleep transforms and uses the canonical open frame. | WPF presenter transition | SLEEP to CURIOUS, STARTLED, CLICK_REACTION, and DRAGGED | acceptance |
| A-RUNTIME-1 | On the current Windows PC, the eyes remain attached and readable during sleep motion and click-only wake visibly produces CLICK_REACTION. | actual Windows runtime and user observation | packaged Stage-A build, SLEEP and click-only wake | acceptance |
| A-BOUNDARY-1 | Exact attempt-8 runtime binary equivalence remains UNVERIFIED and the closed provenance investigation remains closed. | phase-1 evidence history | phase-1 checkpoint | boundary |

---

### Task 1: Deterministic resting-eye asset

**Files:**
- Modify: `tools/Generate-CanonicalArt.ps1`
- Modify: `tests/Dororong.App.ExactArt.Tests.ps1`
- Modify: `src/Dororong.App/Assets/dororong-closed-eyes.png`
- Preserve exactly: `src/Dororong.App/Assets/dororong-canonical.png`

**Interfaces:**
- Consumes: `New-ClosedEyeFrame([Drawing.Bitmap]$Open, [Drawing.Bitmap]$FaceSource)` and the existing reviewed eye stencil in `Generate-CanonicalArt.ps1`.
- Produces: a new deterministic `dororong-closed-eyes.png` with the same 96x96 dimensions and alpha as the open frame, and updated exact closed-frame identities in the art test.

- [ ] **Step 1: Add the resting-curve regression checks before changing the generator**

  In `Assert-EyeAndMouthContract`, retain all current eye-removal, alpha, support, mouth, and protected-art checks. Require dark source endpoints on row 124 and a connected dark center on row 126, while clearing the rejected cap centers, old deep centers, and old wide endpoints. At native size, require balanced endpoint anchors and measure the vertical optical-ink centroid across rows 52–55. Each center must sit `0.25..0.85` pixel below its endpoints so the production resize retains a gentle visible `⌣` rather than a cap, flat line, V, or deep bowl. Also compare horizontal lid centers to the corresponding canonical open-eye centers; the pair must not retain attempt 2's approximately one-native-pixel rightward displacement.

- [ ] **Step 2: Run the exact-art test and verify RED**

  Run:

  ```powershell
  pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
  ```

  Expected: FAIL on the horizontal center-alignment assertion while the generator still uses attempt 2's right-shifted `⌣` lid runs. A syntax, missing-file, or build error is not the required RED result.

- [ ] **Step 3: Replace only the lid runs**

  Keep both eye-removal stencils and `New-ClosedEyeFrame` face-color restoration unchanged. Measure the canonical open-eye horizontal centers, then translate every x coordinate in `$leftLidRuns` and `$rightLidRuns` left by the same integer source-pixel offset required to align the native lid centers. Preserve every row, run width, inter-eye spacing, and the existing shallow `⌣` depth. The expected correction is approximately two source pixels, producing about one native pixel of left movement; the test, not an unrelated shape adjustment, must establish the exact offset.

  Keep the lid color sampled from the canonical source outline. Do not edit the canonical source PNG, body mask, open runtime frame, or source/body outline code.

- [ ] **Step 4: Generate to an attempt-specific scratch path and verify the eye-only contract**

  Use a fresh directory under `.superpowers/sdd/2026-08-28-dororong-m1-stage-a-closed-eye-sleep/task-1/`. Run the real generator once with both `-OutputDirectory` and `-EvidenceDirectory`, then run the test's `-EyeOnlyOpenPath`, `-EyeOnlyClosedPath`, `-EyeOnlyNativeOpenPath`, and `-EyeOnlyNativeClosedPath` mode against those exact outputs.

  Expected: the canonical open hash remains `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`; protected-art, alpha, mouth, eye-removal, resting-curve, and single-resize checks pass.

- [ ] **Step 5: Update the committed closed asset and exact closed identities**

  Copy only the generated `dororong-closed-eyes.png` into `src/Dororong.App/Assets/`. Record the generated `sourceClosed` and `nativeClosed` SHA-256 values as `expectedHashes.SourceClosed` and `expectedHashes.NativeClosed` in the exact-art test. Do not change any pinned open/input/contour identity.

- [ ] **Step 6: Run Task 1 verification**

  Run:

  ```powershell
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
  pwsh -NoProfile -File tests/Dororong.App.BodyMask.Tests.ps1
  pwsh -NoProfile -File tests/Dororong.App.ContinuousAuthority.Tests.ps1
  pwsh -NoProfile -File tests/Dororong.App.SubpixelOutline.Tests.ps1
  ```

  Expected: all commands exit `0`; the open asset hash remains exact; the new closed asset hash equals the generator's `nativeClosed` value.

- [ ] **Step 7: Commit Task 1**

  ```powershell
  git add -- tools/Generate-CanonicalArt.ps1 tests/Dororong.App.ExactArt.Tests.ps1 src/Dororong.App/Assets/dororong-closed-eyes.png
  git commit -m "fix: redraw Dororong resting eyelids"
  ```

### Task 2: Restrained SLEEP breathing and clean wake reset

**Files:**
- Create: `tests/Dororong.App.SleepPose.Tests.ps1`
- Modify: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`

**Interfaces:**
- Consumes: `DororongPresenter.Render(PetSnapshot snapshot)`, the existing `BodyScaleTransform`, `BodyRotateTransform`, `BodyTranslateTransform`, and state frame mapping.
- Produces: a SLEEP pose with `ScaleX = 1 + 0.012 * cycle`, `ScaleY = 0.90 + 0.012 * cycle`, and `TranslateY = 6 - cycle`, where `cycle = sin(phase * 2π)`; all wake states start from `ResetPose()` and therefore clear these values.

- [ ] **Step 1: Write the focused WPF test before changing the presenter**

  Create an STA PowerShell test following the construction pattern in `tests/Dororong.App.DraggedAngle.Tests.ps1`. Build `DororongPresenter`, render SLEEP at phase `0.25`, record the frame name and three transforms, render SLEEP at `0.75`, and assert:

  ```powershell
  Assert-Near 1.012 $highScaleX 0.000001 'SLEEP expansion phase ScaleX changed.'
  Assert-Near 0.912 $highScaleY 0.000001 'SLEEP expansion phase ScaleY changed.'
  Assert-Near 5.0 $highTranslateY 0.000001 'SLEEP expansion phase vertical position changed.'
  Assert-Near 0.988 $lowScaleX 0.000001 'SLEEP contraction phase ScaleX changed.'
  Assert-Near 0.888 $lowScaleY 0.000001 'SLEEP contraction phase ScaleY changed.'
  Assert-Near 7.0 $lowTranslateY 0.000001 'SLEEP contraction phase vertical position changed.'
  ```

  Assert both SLEEP phases use `dororong-closed-eyes.png`. Then render CURIOUS, STARTLED, CLICK_REACTION, and DRAGGED snapshots and assert each uses `dororong-canonical.png`, has no retained `TranslateY = 5..7` sleep offset, and receives only its own documented transform.

- [ ] **Step 2: Build and run the focused test to verify RED**

  Run:

  ```powershell
  dotnet build DororongDesktopPet.sln --configuration Release --no-restore
  pwsh -NoProfile -File tests/Dororong.App.SleepPose.Tests.ps1 -Configuration Release
  ```

  Expected: FAIL because the current presenter keeps SLEEP `ScaleY` at `0.82` and `TranslateY` at `8` instead of applying the planned vertical breathing cycle.

- [ ] **Step 3: Implement the minimal SLEEP pose**

  Change only the SLEEP case in `DororongPresenter.Render`:

  ```csharp
  case PetState.Sleep:
      BodyScaleTransform.ScaleX = 1 + (0.012 * cycle);
      BodyScaleTransform.ScaleY = 0.90 + (0.012 * cycle);
      BodyTranslateTransform.Y = 6 - cycle;
      DororongImage.Source = ClosedEyesFrame;
      break;
  ```

  Do not add timers, storyboards, a wake state, or new frame-selection abstraction in Stage A.

- [ ] **Step 4: Run Task 2 verification**

  Run:

  ```powershell
  pwsh -NoProfile -File tests/Dororong.App.SleepPose.Tests.ps1 -Configuration Release
  pwsh -NoProfile -File tests/Dororong.App.ExactArt.Tests.ps1 -Configuration Release
  pwsh -NoProfile -File tests/Dororong.App.DraggedAngle.Tests.ps1 -Configuration Release
  pwsh -NoProfile -File tests/Dororong.App.RuntimeComposition.Tests.ps1 -Configuration Release
  dotnet test DororongDesktopPet.sln --configuration Release --no-restore
  ```

  Expected: every command exits `0`; xUnit reports `78 passed / 0 failed / 0 skipped`; no core behavior file changes.

- [ ] **Step 5: Commit Task 2**

  ```powershell
  git add -- src/Dororong.App/Controls/DororongPresenter.xaml.cs tests/Dororong.App.SleepPose.Tests.ps1
  git commit -m "feat: refine Dororong sleep breathing"
  ```

### Task 3: Exact Stage-A artifact and ordered Windows observation

**Files:**
- Create after observation: `docs/verification/2026-08-29-m1-windows-acceptance-manual-stage-a-attempt-2.md`
- Update: `TASKS.md`
- Preserve historical verdicts in all earlier evidence files; a short correction addendum may link superseding design direction without changing those verdicts.

**Interfaces:**
- Consumes: reviewed Task 1 and Task 2 commits.
- Produces: one clean Stage-A Release artifact in `artifacts/repro/stage-a-closed-eye-sleep-attempt-2/`, exact hashes, captured IDLE-blink evidence, and direct user verdicts for the remaining small observation group.

- [ ] **Step 1: Verify the reviewed source state**

  Require a clean worktree and record exact HEAD. Re-run Release test/build and the focused Stage-A tests from Tasks 1–2. Stop if any command fails.

- [ ] **Step 2: Publish once to the Stage-A-specific path**

  Run:

  ```powershell
  dotnet publish src/Dororong.App/Dororong.App.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/repro/stage-a-closed-eye-sleep-attempt-2
  ```

  Record SHA-256 for `Dororong.App.exe`, `Dororong.App.dll`, `Dororong.Core.dll`, `dororong-canonical.png`, and `dororong-closed-eyes.png`. Confirm the packaged open/closed asset hashes equal the reviewed runtime asset hashes.

- [ ] **Step 3: Perform the visual-check gate before a product claim**

  The completion agent opens the exact canonical and closed assets independently at native 96x96 scale and nearest-neighbor enlargement. It records A-EYE-1 through A-EYE-3 and A-SLEEP-1/A-WAKE-1 separately. Asset inspection may pass those layers but cannot pass A-RUNTIME-1.

- [ ] **Step 4: Launch the exact packaged executable and request only the first observation group**

  Launch the exact Stage-A executable once and retain its exact PID/path/start-time identity. Ask the user to observe only:

  1. Dororong enters SLEEP with both eyes visually attached and no remaining open-eye fragments;
  2. breathing is gentle rather than heavily squashed;
  3. a simple click without dragging wakes Dororong and shows the click reaction.

  Record each as `PASS`, `FAIL`, or `UNVERIFIED` only from the user's report. Do not infer transparent click-through or other later-roadmap checks from this observation.

- [ ] **Step 5: Record evidence without overstating M1**

  Add the attempt-2 verification file with exact commit/artifact identity, commands, observed facts, and verdicts. Overall M1 remains `PARTIAL`; any unobserved Stage-A or later-roadmap behavior remains `UNVERIFIED`.

- [ ] **Step 6: Commit the verification record only after user observation**

  ```powershell
  git add -- docs/plans/2026-08-28-dororong-m1-stage-a-closed-eye-sleep.md docs/verification/2026-08-29-m1-windows-acceptance-manual-stage-a-attempt-1.md docs/verification/2026-08-29-m1-windows-acceptance-manual-stage-a-attempt-2.md TASKS.md
  git commit -m "docs: record Stage A Windows observation"
  ```

  Do not push, open a PR, or merge.
