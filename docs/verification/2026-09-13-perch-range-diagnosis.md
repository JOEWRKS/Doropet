# Taskbar perch reachability diagnosis

## Scope

User reports inconsistent/reduced taskbar perch detection. No production fix or
range expansion in this diagnosis. Running candidate remains PID35480 at
`artifacts/product-shell/candidate-20260913-contextmenu-01/runtime/Dororong.exe`.

## Reproduction

`dotnet run --project artifacts/repro/perch-range-20260913/Probe.csproj -c Release`
completed exit0. The helper references the running candidate's App/Core DLLs,
constructs WPF presenters without showing windows, and renders18 combinations of
three head-grab points, both facings and requested swing angles-20/0/+20 degrees.
The presenter applies its own actual angle cap. It measures production foot/perch
contacts and applies the existing GetMovementArea bottom-bound formula, then calls
the actual Core EdgePerch.CanBegin selector for reachable and unclamped controls.
It does not simulate the complete live input loop or operate the user's mouse.

## Findings

- Perch contact is measured from neutral image09, gripY94 regardless of held tilt.
- Upright held silhouette soleY127; maximum hostY1080-127=953.
- Taskbar top1040 requires hostY946..966 for the existing0..20DIP below-edge band.
- Thus the upright reachable interval is946..953: only7DIP, not20.
- Sampled tilt/grab changes soleY to120.69..135.05; reachable interval width
  varies0..13.31DIP. At grab25,38 and the corresponding mirrored20-degree tilt,
  hostY is capped at944.949, above the minimum946. Actual selector returnsfalse;
  without the bottom clamp, the control at956 returnstrue in all18 cases.

The conflict is between pose-dependent bottom clamping and fixed neutral-perch
contact, not a randomly changing MaximumGripDistance. The separate80px full-carry
gate also remains relevant to short drags but was not altered or blamed for these
fully-held synthetic cases.

## Native snapshot

Three read-only metadata samples at01:52:28KST saw two40px bottom taskbars at1040
on1080px-high monitors. Both full-width surfaces appeared after the adapter's
expected80ms initial visibility gate; no intersecting occluders. No titles or
input content captured. The first sample's missing support is helper initialization,
not evidence that the long-running pet lost its taskbar.

This snapshot does not establish the state at the user's failed attempt or rule
out other transient metadata/input failures. The independent deterministic result
does establish a concrete cause of pose-dependent reduced taskbar reachability.

## Next boundary

Choose a bounded correction that reconciles eligibility and bottom clamping without
allowing invisible/off-screen dragging or arbitrarily widening all window perches.
Require failing regression, full-loop verification and live user acceptance before
claiming a fix. Source/art/process/installer unchanged; no commit or push.
