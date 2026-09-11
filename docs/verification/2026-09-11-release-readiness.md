# Release-readiness validation — 2026-09-11

Historical pre-shell checkpoint. Current product-shell evidence is in
[2026-09-12-product-shell.md](2026-09-12-product-shell.md); the observations below
describe the stated18b6e4a baseline, not the later branded candidate.

## Scope and decision

Baseline: `18b6e4ad8b262be5a3bc1c9b19337c358b7475ad` on
`feature/dororong-m1-expression-animation`, already pushed before this work.
This pass adds regression coverage and refreshes documentation. It does not
change approved production code, art, timing or the running desktop process.

**Public-release gate: NOT READY.** A working, tested executable is not yet
an installed, branded and supportable product. The next batch is the product
shell described below. An internal portable candidate does not close that gate.

## Interaction regression coverage

Existing suites cover head/cheek routing, Sit, perch input, sleep/hunting,
platform ownership, taskbar disappearance, activation recovery and cleanup.
New `PounceReadinessTests` exercise the real pounce-enabled WPF presenter,
PetLoop and platform runtime with a controlled native scene:

- Sleeping pet wakes for a nearby pointer, completes a pounce and returns to
  sleep after departure and inactivity.
- Pending and carried midflight head grabs can be explicitly canceled. Check
  immediate brain state and capture callbacks, then tick with the button still
  held so ordinary button release cannot conceal stale ownership.
- Six alternating cycles retain the landing/tracking support identity and
  visible sole, travel28 DIPs per jump, wait through3s tracking, require fresh
  dwell and have no duplicate host position writes in any internal substep.

This harness exercises production components, not production-window factory
wiring or a human manipulating the desktop. Capture callbacks are instrumented
test boundaries, not a live Windows mouse-capture test.

Initial test-authoring corrections were not product bugs: a missing test
namespace import; a100ns double-to-TimeSpan subdivision loss at exactly.62s;
and an incorrect assumption that pending head presses capture before crossing
the drag threshold. The sleep test now observes10ms after landing; sampler
tests retain exact phase-boundary checks. Read-only review strengthened
immediate/next-tick cancellation, every-substep write counting and support
identity assertions. No runtime change was needed for those corrections.

Raw test reports are retained locally in `artifacts/release-readiness/`.
General Release suites passed Core241/App820; the final assertion-only review
addition also passed its4 covering tests. Browser controller8 passed. Scoped
re-review approved all strengthened cancellation/write-count assertions.
Candidate-specific counts and identity are recorded in TASKS.md after verification.

## Window-environment evidence

Executed existing probes in separate PowerShell processes:

```powershell
pwsh -NoProfile -File tools/Verify-WindowPlatforms.ps1 -ProbeOnly
pwsh -NoProfile -STA -File tools/Verify-WindowPlatforms.ps1 -WpfProbeOnly
```

Both probe branches succeeded. Do not run the script's historical full-run
branch as a current release gate: it refers to old evidence directories/PIDs.

- Native metadata:21/21 successful captures; mean7.24ms, maximum39.48ms
  including first-sample JIT. This is capture timing, not animation frame time.
- Current configuration: two1920×1080 monitors at(0,0) and(1920,0), both96DPI.
- Invisible, non-activating probe windows on both monitors agreed exactly with
  native client origin, DPI-scaled axes and cursor coordinate mapping.
- No game, Search panel or user window was opened/changed; no screenshot,
  window title or input text was collected. The running pet was not restarted.
- Evidence: `native-capture.json` and `wpf-coordinate.json` in the local report
  directory. These reports are generated diagnostics, not committed fixtures.

Still unverified: fresh SMAPI→game startup, Windows Search overlay activation,
mixed-DPI transitions, monitor connect/disconnect, taskbar auto-hide changes,
clean-machine installation and long-duration desktop soak. Existing synthetic
regressions do not substitute for these live acceptance scenarios.

## Product-shell and delivery audit

| Area | Current evidence | Release requirement |
|---|---|---|
| Process identity | Native `Dororong.App.exe`; Product/Company/Description `Dororong.App`; file version1.0.0.0 | Explicit product identity/version/icon |
| Startup and exit | AppStartupSequence, fatal boundary and window-close cleanup exist | Retain these paths when adding shell ownership |
| Duplicate instances | No cross-process ownership guard in startup | A second launch must not create a second pet |
| Tray | No NotifyIcon or management menu | Management-only entry point; preserve right-click Sit behavior |
| Diagnostics | Fatal errors show a message and shut down; no durable app log | Bounded local error logs without pointer/input content |
| Install/update/uninstall | Framework-dependent publish only | User-local install, controlled replacement/rollback and registered uninstall |
| Signature | Running apphost reports NotSigned | Explicit signing/distribution decision before public handoff |
| Rights/provenance | No root license/distribution authorization record | Owner review before public distribution; no permission inferred |
| Automation | No .github workflow or current reusable release pipeline | Reproducible clean build/test/package verification |

Proposed next implementation boundary, matching the earlier product-shell
proposal in TASKS.md: display name Dororong / 도로롱, executable Dororong.exe,
explicit version, single instance, management-only tray, bounded local errors,
user-local installation and Start menu/uninstaller entries. Upgrade by running
a newer installer. No background service, network updater or automatic startup
registration. Installer format, dependency bundling and signing need an explicit
delivery design before implementation; the shell must not alter pet behavior.

## Candidate discipline

The local candidate must use a fresh directory, preserve the entire
framework-dependent runtime layout, and include clear run/rollback instructions.
Compare the packaged App/Core assemblies against the test-loaded assemblies and
compare every archive entry against its source file. Record SHA256 separately.
Do not launch, install, upload, sign or label the candidate a public release.

The ordinary non-RID App test DLL did not match the `win-x64` publish DLL.
Their dependency targets differ (`.NETCoreApp,Version=v8.0` versus its `/win-x64`
variant); source and assembly version were unchanged. The candidate was held
while the complete App suite was rebuilt/run with `-r win-x64
-p:SelfContained=false`. Only comparison to that matching test output qualifies
the package; no binaries were swapped to conceal the first mismatch.

That matching win-x64 suite passed820/820. Both packaged DLL hashes matched its
test-loaded outputs. The ZIP was reopened and all10 file entries matched their
source hashes. Candidate was not launched or installed.

Local archive: `artifacts/release-candidates/Dororong-validation-20260911-01-win-x64.zip`

ZIP SHA256: `514DE0B2A21A355E369864ADDA595C3A01F78279E0192968FEF569C96C8D0E1F`

App DLL SHA256: `37CECE76E793298B7D4022C479FD9346588FC2166D3F6358CD218F7B94382972`

Core DLL SHA256: `55683D0679C22ECBC20777AFC1384BEA0249ED29AE8FD78B93A83AF201FD3EC8`

Art hashes remain unchanged:

| Asset | SHA256 |
|---|---|
| hunting.pbgra.gz | A64461C244D073822EEE4B2517D7E3B218A4CE88C8DC15F7CAE7A8DD2869212E |
| locomotion.pbgra.gz | 03983B66D55CF2EC3B7A6B7CD0B78C5AC609FF41F442B0197909B74CDA723ECE |
| layered-pull.pbgra.gz | B2EEEDF800C66D47648346A51C69E1B484021475FC69B968CC6C8B92BD8E5893 |
