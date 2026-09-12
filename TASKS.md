# Plan

## User-local installer implementation plan — 2026-09-12

> Execute with subagent-driven-development: sequential implementers, scoped task
> reviews, one final integration review. TASKS.md is the sole plan/progress ledger.

**Goal:** Build a validated local Inno Setup installer candidate with non-admin
installation, safe upgrade/refusal and owned-file-only uninstall behavior.
**Architecture:** Verified immutable portable payload -> inventory and pinned
compiler -> generated explicit Inno file table plus handwritten lifecycle policy
-> installer artifact and isolated acceptance harness. No custom updater/service.
**Tech Stack:** PowerShell7, Inno Setup7.1.0 x64 portable compiler, Windows APIs,
existing .NET8.0.31 self-contained payload. No character-engine changes.
**Spec:** docs/superpowers/specs/2026-09-12-user-installer-design.md, user approved.
**Base:** eece06b; source payload074b643; candidate-20260912-1751 preserved.

### Installer global constraints

- Product 도로롱 (Dororong),0.1.0,JOEWRKS; Dororong.exe/internal App DLL unchanged.
- Default %LocalAppData%/Programs/JOEWRKS/Dororong; HKCU only, no elevation.
- Stable AppId JOEWRKS.Dororong; same-version reinstall/higher upgrade/lower refusal.
- Start menu and uninstall registration; desktop optional unchecked; autostart absent.
- No force-close, process-name kill, reboot replacement, downloads during installation.
- Retain user logs and added files; no recursive/wildcard whole-install deletion.
- No host product install/registry/shortcut mutation in this implementation turn.
  Prepare/run isolated tests only when an actual isolated environment is available;
  missing live evidence stays UNVERIFIED, never PASS. No public release/signing/push.
- No rebuilding payload or modifying App/Core/art without explicit gate escalation.
- Sequential builds/tests; preserve unrelated tests/Dororong.App.Tests/TestResults.
- Artifacts/briefs/reports under artifacts/installer/, not a second progress ledger.

### Task 1001: Pinned toolchain and safe payload inventory

Files: tools/installer/InstallerPayload.psm1; tools/installer/Prepare-InnoSetup.ps1;
tests/Dororong.InstallerPayload.Tests.ps1. Report task-1001-report.md in artifacts/installer.
Produces functions:
```powershell
Get-InstallerPayload -PackagePath <directory>
# object: Version ('0.1.0'), FileVersion ('0.1.0.0'), Root (absolute),
# Files array of { RelativePath (forward slash), Length (Int64), Sha256 (uppercase) }
Assert-InstallerOutputPath -OutputPath <fresh artifacts/installer/candidate-*>
# returns resolved absolute root; refuses existing, non-direct child, reparse ancestors
# Prepare-InnoSetup.ps1: -OutputPath <fresh artifacts/installer/toolchain-*>
# outputs TOOLCHAIN_PATH, COMPILER_PATH and toolchain.json (source/version/hashes/signer)
```
- [ ] RED executable PowerShell tests: tiny real temp file inventory has literal
      known SHA256; traversal/reparse/ambiguous or unsafe Inno filename refusal;
      input never modified; existing-output sentinel retained. Tests run exports,
      not grep source. Metadata test reads actual preserved candidate PE.
```powershell
$inventory = Get-InstallerPayload -PackagePath $candidateRuntime
if ($inventory.Version -ne '0.1.0') { throw 'Wrong product version' }
if (@($inventory.Files).Count -eq 0) { throw 'Empty payload accepted' }
# Seed a controlled existing output with sentinel; call Assert-InstallerOutputPath
# and assert failure, unchanged sentinel hash and no added files.
```
- [ ] GREEN strict canonical path and per-component reparse rejection, stable
      ordinal inventory, relative-name validation/escaping contract; require
      Dororong.exe product metadata and internal DLL/deps/runtimeconfig presence.
      This inventory is not a substitute for the existing strict package validator.
- [ ] Obtain fixed official Inno Setup7.1.0 x64 from the release linked by
      https://jrsoftware.org/isdl.php. Verify Authenticode Valid/Pyrsys B.V. before
      execution, record download SHA256 and verify compiler version/signature.
      Official tag is-7_1_0/setup.iss + isportable.iss confirm /PORTABLE=1 disables
      uninstall registration, file associations and shortcuts. Use /CURRENTUSER,
      /PORTABLE=1 /VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOICONS,
      explicit fresh worktree /DIR; Start-Process Hidden. No winget/system install.
      Reuse only proven matching tools; no delete/overwrite existing toolchain.
- [ ] Run focused tests and actual compiler version probe; self-review/commit
      owned files only. Record RED/GREEN commands/output and trust provenance.

### Task 1002: Installer policy, compilation and refusal tests

Files: installer/Dororong.iss; installer/InstallerPolicy.iss;
tools/Build-Installer.ps1; tests/Dororong.InstallerBuild.Tests.ps1;
tests/installer/PolicyHarness.iss. Consume Task1001 exported contracts.
Produces:
```powershell
& tools/Build-Installer.ps1 -PackagePath <verified runtime> -ArchivePath <matching zip> -CompilerPath <pinned ISCC.exe> -OutputPath <fresh artifacts/installer/candidate-*>
# outputs INSTALLER_PATH; writes payload.json, payload-files.iss and build-evidence.json
# final name Dororong-Setup-0.1.0-win-x64.exe; no version override in product builder
```
- [ ] RED builder refuses missing/mutated input, foreign compiler and existing
      output without creating artifacts. Characterize actual compiled policy via
      a separate no-install harness which includes the SAME InstallerPolicy.iss;
      numeric version comparison0.1.10 >0.1.2, equal allow, lower reject, malformed
      installed version fail closed. Harness aborts before file/registry/icon work.
- [ ] GREEN builder invokes tests/Dororong.ProductPackage.Tests.ps1 in a child
      process and requires exit0 before creating output. Inventory all files,
      generate explicit quoted [Files] entries, stage exact bytes, compare hashes
      before/after compile. No wildcard compile of mutable original payload.
      Fail build on compiler error; record source/hash/version/ISCC provenance.
- [ ] Inno [Setup] uses PrivilegesRequired=lowest, stable AppId, x64-compatible
      architecture, fixed default user-local root enforced against /DIR overrides,
      no previous arbitrary directory reuse, exact version/publisher/icon,
      CloseApplications=no, RestartApplications=no, no restartreplace flags.
      Korean and English wizard when official translation available. No fake license.
      [Icons] user-start-menu entry; desktop [Tasks] unchecked; [Run] optional
      postinstall/skipifsilent. No autostart/service/HKLM entries.
- [ ] Numeric previous installed version read from own registered product/PE;
      refuse downgrade or unreadable conflicting identity before writes. Same and
      higher allowed. Setup/uninstall share a per-user operation gate across
      sessions (exclusive file handle outside payload with close-time cleanup);
      errors fail closed. Before writes/removal inspect target-owned files for
      in-use/sharing conflicts across sessions, not only Local instance mutex.
      Known locked files => nonzero refusal; do not kill. Recheck at installation
      transition; concurrent launch/I/O failure never reported as success.
      Avoid claiming atomic/power-loss rollback; surface repair need on failure.
- [ ] Uninstall owns only explicit installed files/registration/shortcuts. No
      [UninstallDelete] broad root deletion and no user-log deletion. Obsolete
      owned-file cleanup only from validated relative ownership manifest. Reject
      malicious/reparse paths; never delete user-added files outside manifest.
- [ ] Compile actual product candidate; metadata/signature-state and inventory
      parity checks; policy harness tests run without host install. Self-review
      and commit owned files. No actual host installer execution beyond harness.

### Task 1003: Isolated acceptance harness and delivery evidence

Files: tests/installer/Invoke-InstallerAcceptance.ps1;
tools/installer/New-InstallerSandbox.ps1; docs/verification/2026-09-12-installer.md;
README.md. Consume builder output and evidence contract; no app implementation.
- [ ] RED boundary tests reject running acceptance on ordinary host; harness
      requires explicitly isolated sandbox proof/config and output directory.
      Missing isolation is UNVERIFIED/nonzero, never successful acceptance.
```powershell
# In ordinary host, invocation must fail BEFORE any install/registry/shortcut writes.
& tests/installer/Invoke-InstallerAcceptance.ps1 -CandidatePath $candidateRoot
if ($LASTEXITCODE -eq 0) { throw 'Host accepted as isolated test environment' }
```
- [ ] GREEN generate opt-in .wsb + guest script with networking disabled,
      read-only mapped payload/tool inputs and one narrow writable result mapping.
      Do not enable Windows features or launch/install on host without handoff.
      Guest tests install/reinstall/higher test-fixture upgrade/lower refusal,
      running-file lock refusal, failure/cancel and uninstall. Use real registry,
      shortcuts, file hashes and process outcomes; literal expected ownership.
      Preserve log/user-extra sentinels and verify no unexpected target deletion.
      Test upgrades use separate clearly labeled fixture ID/version, not product
      version overrides or modified distributed payload. Results identify exactly
      which package/config each test exercised; no fixture-to-product PASS leap.
- [ ] If existing Sandbox/VM is safely available, run within approved isolated
      scope and read results. Otherwise leave runtime installer acceptance open
      and deliver ready-to-run harness, compiler-built candidate and honest gates.
- [ ] Run all new non-installing script tests, strict candidate package validation,
      asset/payload hash preservation; update README build/install/remove/support
      instructions and evidence, no public-release claim. Commit owned files.
- [ ] Final integration review and single consolidated fix wave if needed.

Preflight coverage:
|Pair/task|Interface/consistency check|Result|
|---|---|---|
|1001→1002|Inventory+fresh output and compiler trust feed builder|Explicit named fields; full package validator remains separate|
|1002→1003|Installer/evidence feed guest acceptance|No host install; fixtures clearly separate from product|
|1001→1003|Toolchain reused read-only in guest input|No global tool installation|
|1001|Real file/refusal tests match read-only inventory/preparation scope|Consistent|
|1002|Compiler success vs install behavior gates|Separate shared-policy harness and actual guest lifecycle|
|1003|Unavailable isolation cannot produce PASS|Spec permits unverified live handoff|

Ruling: retain TASKS.md as sole plan/ledger with artifacts/installer briefs/reports —
AGENTS.md outranks default extra plan/progress files. Cost if wrong: bookkeeping
must be reorganized; no product effect.
Ruling: use official Inno Setup7.1.0 x64 portable compiler in worktree — verified
release and tagged portable-mode source permit no host registration. Cost if wrong:
toolchain replacement and package recompilation; no purchases/system install.
Ruling: reuse immutable source074b643 payload rather than rebuild for documentation
commits — exact tested App/Core bytes retained. Cost if app lifecycle must change:
fresh full regression/payload generation before packaging.
Execution: linked worktree confirmed, no superproject. Core baseline241/241 and
App baseline852/852 on the unchanged pinned8.0.31 RID bytes passed (no rebuild).
Task 1001: complete (commits 49fe56c..c7e2623, spec/quality review approved).
Focused inventory/refusal/toolchain tests exit0; parser errors0. Official signed
compiler7.1.0 verified via --version (PE ProductVersion is0.0.0.0).
Use only toolchain-inno-7.1.0-x64-20260912-02; first incomplete output preserved.
Controller resolved provenance check: official isdl.php x64 link31 points exactly
to the pinned GitHub is-7_1_0 asset. Source payload ZIP hash remains unchanged.
Task 1001: minor (deferred): use case-sensitive SHA assertions, ordinal expected
sorting, and verify every manifest source hash against .source artifacts in tests.
Task1002 next: policy/builder/non-installing harness; brief in artifacts/installer.
Environment gate: Windows10Pro, Sandbox feature InstallState2 and executable
absent; standard VirtualBox/VMware command paths absent. No feature enabled.
Task1003 live acceptance remains unavailable unless an isolated guest is provided;
details in artifacts/installer/environment-evidence.md. Continue non-installing work.

## User-local installer design — 2026-09-12

User approved continuing production-readiness work after the verified product shell.
New installation subsystem: architectural brainstorming path; no installer code yet.
- [x] Inspect repository/current candidate and installed compiler availability.
- [x] Compare ordinary Inno Setup EXE, MSIX, and custom installer responsibilities;
      verify official non-admin/run-state semantics. Recommend Inno Setup.
- [x] Draft installation/update/uninstall ownership and validation policy in
      docs/superpowers/specs/2026-09-12-user-installer-design.md; self-review.
- [x] User review of written design, including non-forced running-app handling,
      default per-user path, numeric downgrade refusal and retained user logs/files.
- [x] After approval: implementation plan in this ledger, then TDD/package work.
Current baselinee49aedb; approved portable candidate source074b643 remains intact.
No compiler installation/download, host product installation, registry/shortcut
mutation, app behavior edits, signing, purchases or push in this design step.

## Product shell implementation plan — 2026-09-11

> Execute with subagent-driven-development; one implementer at a time, scoped
> spec/quality reviews and final integration review. TASKS.md is the only ledger.

**Goal:** Implement the approved identity/instance/tray/log shell and deliver a
self-contained local candidate without changing character behavior.
**Architecture:** Product primitives feed an App-owned ProductShell; existing
startup/cleanup boundaries remain authoritative. Package only the native apphost
under its product name, retaining internal assembly/resource identities.
**Tech Stack:** .NET8 WPF/Windows Forms NotifyIcon, Windows named mutex, xUnit,
PowerShell packaging and test-only subprocess probes.
**Spec:** docs/superpowers/specs/2026-09-11-product-shell-design.md, explicitly
approved by the user's latest ㄱㄱ. BASE=3bea598. Earlier dirty readiness files
belong to the existing task and must not be staged by implementers.

### Global constraints

- 도로롱 (Dororong), Dororong.exe,0.1.0,JOEWRKS; keep Dororong.App.dll/pack URIs.
- Same-user/same-login-session single instance; second launch is silent exit0.
- Tray only: disabled version label, 로그 폴더 열기, 종료. No motion controls.
- Log under LocalApplicationData/JOEWRKS/Dororong/logs, UTF-8,1MiB/file,3files.
- No exception Message/ToString, input, paths, window titles, coordinates or uploads.
- Preserve art, timing, input, Sit, perch, physics and running PID14064.
- No service/network updater/autostart, user installation, signing, push or release.
- Existing startup/fatal/cleanup semantics remain one-shot and failure-safe.
- Build/test sequentially; never compile App while another App test host runs.

### Task 901: Product identity, ownership and bounded diagnostics

Create src/Dororong.App/Product/{ProductIdentity,SingleInstanceLease,DiagnosticLog}.cs;
tests/Dororong.App.Tests/Product/{SingleInstanceLeaseTests,DiagnosticLogTests}.cs;
test-only subprocess support under tests/support if needed. No App wiring yet.

Interfaces produced (internal unless a test helper needs otherwise):
```csharp
// ProductIdentity: DisplayName, Version, Publisher, ExecutableName,
// LogDirectory and version-independent InstanceName (Local + current SID).
SingleInstanceLease? SingleInstanceLease.TryAcquire(string name);
// null = another process owns it; IDisposable, no window dependency.
enum DiagnosticEvent { Started, Stopped, StartupFailure, LoopFailure,
    DispatcherFailure, CleanupFailure, TrayCommandFailure }
new DiagnosticLog(string directory);
void DiagnosticLog.Write(DiagnosticEvent code, Exception? error = null);
```
- [x] RED using compiled minimal stubs, not only missing-type errors: separate
      helper processes assert one owner/one denied, graceful reentry after exit,
      abandoned-owner recovery and safe non-owner handling. Test unique mutex
      names, not the real running user's production lease.
- [x] RED real temp-directory logs: sensitive sentinel in Message/Data never
      appears; only allowed metadata fields; repeated writes rotate within3
      files each<=1048576bytes; concurrent writes remain well-formed; a path
      occupied by a file cannot throw from Write. Example expectations:
```csharp
log.Write(DiagnosticEvent.LoopFailure, new Exception("PRIVATE_SENTINEL"));
Assert.DoesNotContain("PRIVATE_SENTINEL", File.ReadAllText(logFile));
Assert.All(Directory.GetFiles(dir), p => Assert.InRange(new FileInfo(p).Length,0,1048576));
```
- [x] GREEN use WaitOne(0), abandoned ownership handling and same-thread lease
      release; enum-based log fields, sanitized method names without file/args,
      serialized capped writes and graceful I/O failure. No raw exception text.
- [x] Focused Product tests, then full App suite once; self-review and commit
      only owned files. Report RED/GREEN commands/output and interface deviations.

### Task 902: App-owned shell, tray and metadata integration

Create Product/{ProductShell,TrayService}.cs, tests/Product/{ProductShellTests,
TrayServiceTests}.cs; modify App.xaml.cs/MainWindow.xaml.cs/project properties
and DesktopSceneNative executable exclusion with corresponding existing tests.
Create separate icon from existing canonical PNG with reproducible conversion
script if required. Do not mutate source PNG or animation banks.

Consumes Task901 APIs. ProductShell owns one lease and tray, writes lifecycle
events and disposes all acquired resources. Use narrow injected factories for
failure tests; real OS mutex and filesystem already covered by Task901.
- [x] RED lifecycle scenarios: duplicate creates0 window/0 tray; initialization
      failure cleans owned resources; tray exit closes the same window; repeated
      cleanup releases once; logging failure does not suppress fatal shutdown.
      Use a factory trace and observable exit result, not source-text tests.
- [x] RED tray construction/menu callbacks/disposal and both executable names
      excluded from platform scenes while ordinary WPF windows stay eligible.
- [x] GREEN integrate existing AppStartupSequence/FatalBoundary. Hook log events
      at existing once-only fatal boundaries. Keep legacy parameterless seams
      used by RuntimeComposition tests working. Metadata0.1.0/JOEWRKS/displayname;
      WinForms global using isolation; product icon compile resource.
- [x] Focused tests plus RuntimeComposition harness; full Core/App Release,
      unchanged asset hashes. Self-review, commit owned files, report evidence.

### Task 903: Reproducible product-name candidate and delivery checks

Create tools/Publish-Product.ps1 and tests/Dororong.ProductPackage.Tests.ps1;
update README current shell behavior without losing earlier readiness edits.
Output under a fresh artifacts/product-shell/candidate-* directory only.
- [x] RED execute package validator against baseline publishing: expected
      Dororong.exe and product metadata absent. Test script inputs/outputs and
      existing-output refusal; no grep-based script tests.
- [x] GREEN publish win-x64 self-contained, rename native apphost only, omit
      development apphost from candidate, keep full dependency layout. Validate
      exact resolved output inside chosen fresh directory before rename/remove.
      Do not overwrite or clean a preexisting candidate. No install/autostart.
      Pin bundled RuntimeFrameworkVersion8.0.31 (not cached8.0.19); validate
      Core/Desktop/host patch in output and run candidate-runtime verification.
- [x] Actual renamed apphost startup/duplicate/exit smoke must use a controlled
      test process/desktop boundary without replacing PID14064 or altering user
      windows; if unavailable report live check unverified, not a fabricated pass.
      No production hidden diagnostic CLI added only to make testing easier.
- [x] Full release/targeted RID tests, artifact hash parity, PE metadata/icon,
      archive round-trip verification. Record genuine
      remaining live tray/Explorer and installer gates. Commit owned scripts/docs
      only after checks; no push or installation.
- [x] Global final integration review and bounded final fix-wave validation.
  Final review of18b6e4a..7de64a9 found three Important issues: cross-writer
  log cap, FileDescription branding, clean-checkout RID prerequisite/preflight;
  also carry both deferred minors (identity disposal, sanitizer boundaries).
  One consolidated fix wave dispatched from BASE7de64a9; requirements/report:
  artifacts/product-shell/final-fix-{brief,report}.md. Scoped re-review follows.
  Final fixes committed3fdb7d2; focused8/8 and both publisher refusal gates pass.
  README exact command correction074b643 follows SDK MSB1001 before test start.
  Worker/reviewer were interrupted by usage limit; user requested continuation.
  Controller resumes final verification (no further production edits); replacement
  /root/product_fix_review_resumed continues the same single scoped re-review
  against7de64a9..074b643, not another broad review. Core241/241 TRX inspected.
  Scoped re-review complete: all five findings ADDRESSED, no new breakage or
  out-of-scope observations. Reviewer independently checked Core TRX and RID PE
  description/internal names; full final RID/candidate gates remain controller-owned.
  COMPLETE: controller full self-contained win-x64/.NET8.0.31 App852/852,
  Core241/241, Release solution0warnings/errors, RuntimeComposition PASS.
  Candidate candidate-20260912-1751 (source074b643), ZIP7BB45700...:
  strict metadata/icon/pinned-host/RID-byte parity/archive/native smoke all PASS;
  ownedPID8908 exited0, duplicate exited0. Tested RID DLL hashes captured before
  packaging and unchanged afterward; AppA1797AA2... / Core472029E7....
  Four frozen art hashes unchanged. No product behavior edits, installation or push.
  Final process inventory contains no Dororong or test probe; oldPID14064 no longer
  exists after the interruption. Controller never stopped/replaced it; do not claim
  it remains running. Visible tray/Explorer and installer/live acceptance stay open.

Preflight review:
| Pair/task | Interface or constraint check | Result |
|---|---|---|
|901→902|Lease null means duplicate; enum log contract feeds shell|Compatible; no UI in primitives|
|902→903|Internal App assembly stable; new apphost name allowed in exclusion|Package renames only executable|
|901→903|Tests need fresh unique lease; real existing pet stays running|No kill-by-name or production lease test|
|901|Filesystem/mutex tests exercise real boundaries; code creates no windows|Self-consistent|
|902|Shell cleanup wraps existing one-shot boundaries, no PetLoop modifications|Self-consistent|
|903|Self-contained candidate is not installer/public release|Self-consistent; live proof separate|

Ruling: use this TASKS section as sole progress ledger and isolated
artifacts/product-shell/ briefs/reports, not the skill's second progress.md —
AGENTS.md requires one ledger and older TASKS-named scratch belongs to prior
work. Cost if wrong: manual artifact bookkeeping; no product behavior impact.
Ruling: this approved slice stops at product shell/candidate; installer mutation
remains the next design slice — follows approved spec. Cost: installation is
not available at this handoff, explicitly disclosed.
Ruling: pin the candidate's bundled .NET8 runtime to8.0.31 — Microsoft's
2026-09-08 security release and all three NuGet runtime/desktop/host packs were
verified available; local cached8.0.19 is outdated. Cost if wrong: patch
compatibility rework/download, covered by candidate tests; no system install.
Source: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

Execution: existing linked worktree verified (not a submodule), baseline
Core241/App820 Release passed (artifacts/product-shell/baseline-*.trx).
Task901: complete (commits3bea598..edab47d, spec/quality review approved).
Evidence: compiled-stub RED6/7 failures plus identity1/1 failure; focused8/8;
full App828/828 Release. Reviewer /root/product_primitives_review.
Task901 minor (deferred): ProductIdentity.cs:21 dispose WindowsIdentity wrapper.
Task901 minor (deferred): DiagnosticLogTests.cs:48 add punctuation/length sanitizer case.
Reviewer cross-task checks: App silent exit0 and one-shot integration feed902;
asset hashes unchanged and PID14064 responding confirmed by controller. Existing
behavior and assembly resources were untouched; App828 regression evidence stands.
Task902 in progress: fresh implementer, BASEedab47d; no concurrent App builds.
Task902 initial commit bfa7d7d: focused23/Core241/App843 and RuntimeComposition
passed. Review /root/product_shell_review requires partial tray-allocation
cleanup and ProductShell construction inside startup try. Fix round1/5 starts
at bfa7d7d, original /root/product_shell_integration resumed; covering shell/tray
tests and RuntimeComposition must rerun.
Task902 minor (deferred): TrayService.Dispose hide/menu detach must not prevent
remaining disposal; related cleanup test seams may cover this with allocation fix.
Ruling: Dororong.exe output remains Task903's packaging responsibility, not a
Task902 project-assembly rename — spec section2 explicitly preserves development
Dororong.App.exe and renames only published apphost. Reviewer naming finding is
carried into903, not dismissed. Cost if wrong: candidate naming gate will fail
before delivery; no change to internal assembly or pack URIs.
Task902: fix round1/5 (4 addressed,0 open; commitsbfa7d7d..65f7aed).
Re-review /root/product_shell_review approved all findings; related minor hide/
detach cleanup and acquisition tests resolved. Focused18/18, RuntimeComposition
PASS after fix; matching candidate full regression remains903.
Task902: complete (commitsedab47d..65f7aed, review clean; naming delegated903).
Task903 in progress: fresh implementer, BASE65f7aed. Published naming,8.0.31
runtime, fresh candidate, matching RID/helper paths and no visible pet replacement.
Task903 initial commit24831ff, candidate-20260911-155342: Core241, App848,
self-contained8.0.31 App849 all passed; controller independently passed full
package/native smoke (owned PID27656), exact RID App/Core parity and ZIP hash
2A6D5ED1617DB790AD438F09A24F1430797ED0CF944B794D474BDB9A2E899011.
Task903 review /root/product_candidate_review: fix round1/5 starts24831ff.
Open gates: reject runtime-pin override; distinguish skipped/unavailable native
smoke from final success; validate actual apphost8.0.31 provenance instead of
ambient restore metadata alone; enforce both parity references as matching RID
test outputs (publisher printed non-RID Core path). Original implementer resumed.
Runtime/asset/live evidence was independently confirmed; visible tray/Explorer
acceptance remains explicitly unverified, not a package-structure pass.
Final-review observation: controller inspected candidate161048 PE metadata;
ProductName is 도로롱(Dororong), but FileDescription is still Dororong.App.
Evaluate this against the user-facing program identity requirement while keeping
InternalName/OriginalFilename assembly identity unchanged. No fix applied yet.
Task903: fix round1/5 (4 addressed,0 open; commits24831ff..9e525ea).
Scoped re-review approved fixed pin, mandatory native smoke, deterministic actual
host provenance and both matching RID references. Fresh self-contained App849/849
uses exact candidate161048 AppAEACE912.../CoreF4104BA0... bytes; ZIP827E6BAA...
Task903: complete (commits65f7aed..9e525ea, review clean).
Final integration gate pending: entire product-shell change series and preserved
readiness tests/docs; deferred901 identity-disposal/sanitizer coverage and observed
FileDescription branding are supplied to final reviewer. No push/install/launch
replacement is authorized by this checkpoint.

## Product-shell design boundary — 2026-09-11

User's latest ㄱㄱ accepts the recommendation: user-local installed application,
bundled runtime, automatic startup off, upgrades by newer installer. First
implementation slice is product identity/single instance/tray/local logs,
without character behavior changes. Installer file replacement is a subsequent
design slice, not bundled into this first lifecycle implementation.
- [x] Reinspect App startup/MainWindow fatal cleanup, runtime composition,
      project metadata and executable-name exclusion. Existing source identifies
      only Dororong.App.exe; new product name must also be excluded explicitly.
- [x] Record architectural design and self-review constraints/responsibility,
      privacy, failure cleanup, mutex scope and verification boundaries in
      docs/superpowers/specs/2026-09-11-product-shell-design.md.
- [x] User review of written design (brainstorming skill architecture gate).
- [x] Implementation plan/TDD after written-design approval (execution above).
This step changes documentation only, does not restart or install the app, and
preserves all readiness-pass dirty files. Commit only the design document;
no push, release or hidden product implementation at the review boundary.
Design-only local commit:3bea598; staged scope was exactly one spec file and
diff whitespace validation passed. Runtime tests were not rerun for prose-only
changes; this step makes no fresh runtime-verification claim.

## Release readiness implementation plan — 2026-09-11

**Goal:** Stabilize the accepted desktop behavior, verify window integration,
then prepare a reproducible local release candidate without claiming public release.
**Architecture:** Exercise the real presenter/loop/platform integration first;
run non-activating native coordinate probes second; audit and package the same
verified code third. Retain existing assets and production interaction settings.
**Tech Stack:** .NET 8 WPF, xUnit, PowerShell, native Windows metadata probes.
**Spec:** User-approved sequence in this conversation: interaction regression,
window-environment stability, deployment readiness. Baseline 18b6e4a was pushed
and verified equal to origin/feature/dororong-m1-expression-animation.
**Execution:** Sequential in the existing isolated worktree. This is a bounded
validation pass; new installer/update architecture is a separate implementation
decision if the audit finds it missing. No public release, signing, installation,
auto-start change, or shared-branch push is authorized by this pass.

### Global constraints

- Preserve approved art, motion curves, head/cheek drag and Sit semantics.
- Report automated/synthetic evidence separately from live Windows acceptance.
- Do not repeatedly ask the user to reproduce a known issue before using local tests.
- Do not restart the running pet, launch the game, or change display settings.
- Keep generated reports and candidate artifacts under ignored artifacts/.
- TASKS.md is the sole plan ledger; do not create a second plan/progress ledger.

### Task 1: Interaction regression gate

Files: tests/Dororong.App.Tests/Runtime/PounceLoopTests.cs and a focused
PounceReadinessTests.cs using its real-WPF Harness. Production fixes only after
a failing regression and root-cause inspection.
- [x] Extend the harness with optional BehaviorTuning and observable capture/write counters; test nearby-pointer
      sleep wake through UpdateHuntingWithPounce, capture cancellation during
      flight followed by normal falling, and repeated complete pounce cycles.
      Assert observable state, sole continuity, finite positions, no stale carry,
      and at most one host position write per tick. Preserve the existing1.5s
      dwell,340ms flight,280ms landing,3s tracking and12DIP peak.
- [x] Run focused tests, then both full Release suites sequentially and the8
      browser controller tests. If a regression fails, record RED, correct only
      its root cause and rerun the covering tests before the full gate.

### Task 2: Window environment gate

Files inspected: tools/Verify-WindowPlatforms.ps1; Interop/WindowActivationGuard.cs,
DesktopMetadataReader and Runtime desktop/platform tests. No display mutation.
- [x] Run existing taskbar/disappearance/activation/coordinate tests via the full
      App suite. Run the existing script with -ProbeOnly and -WpfProbeOnly in
      separate processes; these branches do not invoke the obsolete full-run
      artifact/PID assumptions. Store raw outputs under artifacts/release-readiness/.
- [x] Record actual monitor/DPI coverage and native-capture success without
      equating it to game-launch/Search-panel live reproduction.

### Task 3: Candidate and release-gap audit

Files: README.md; docs/verification/2026-09-11-release-readiness.md; existing
App.xaml.cs/MainWindow.xaml.cs/project properties. Output: ignored local candidate.
- [x] Inspect current lifecycle, process identity, duplicate-instance handling,
      logging, installation/update support and existing delivery scripts. Report
      missing behavior as release gaps; do not silently add a new product system.
- [x] Update README to current verified interactions and explicit limitations.
- [x] Publish the verified source to a fresh local framework-dependent win-x64
      candidate directory, archive it with license/docs as available, record
      hashes and compare packaged assemblies/assets with tested outputs.
      No installation, launch, remote publishing or signing in this gate.
- [x] Write an evidence-backed readiness report with passed gates, unverified
      live scenarios and concrete blockers for the next implementation batch.

Validation-pass outcome (not public-release approval): Core241/App820 Release,
browser8, RuntimeComposition startup/cleanup passed. Four new regression cases
passed after correcting test-authoring assumptions (namespace import;100ns
TimeSpan subdivision; pending head presses intentionally have no capture).
Read-only review requested stronger immediate and held-tick cancellation,
every-substep write counting and support identity checks; all addressed and
scoped re-review approved. Final covering4 passed. No production code/art edit.

Native probes:21/21 metadata captures; two1920x1080 monitors, both96DPI; origin,
axes and cursor map errors zero. Games/Search/mixed-DPI/live soak remain unverified.
Raw reports: artifacts/release-readiness/. Report:
docs/verification/2026-09-11-release-readiness.md. Existing PID14064 remained
responsive at its original pounce-arc runtime path; not restarted.

Candidate: artifacts/release-candidates/Dororong-validation-20260911-01-win-x64.zip.
The first non-RID test DLL comparison rejected the RID publish App DLL.
Reran the complete App suite with -r win-x64 -p:SelfContained=false:820 passed;
candidate App/Core then matched those test-loaded DLLs exactly. Archive10/10
file contents matched source hashes. No launch, install, signing or upload.
ZIP SHA256:514DE0B2A21A355E369864ADDA595C3A01F78279E0192968FEF569C96C8D0E1F
App SHA256:37CECE76E793298B7D4022C479FD9346588FC2166D3F6358CD218F7B94382972
Core SHA256:55683D0679C22ECBC20777AFC1384BEA0249ED29AE8FD78B93A83AF201FD3EC8

Next boundary: new product-shell/installer architecture. Existing shell proposal
was not implemented. User asked for my deployment recommendation after being
offered installed vs portable. Recommend user-local installed/self-contained,
Start menu/uninstall, no auto-start by default, installer-driven upgrades,
no service/network updater. Await delivery-design selection; no assumption of
installer permission or public rights. This pass remains uncommitted/unpushed.

## Accepted pounce save boundary — 2026-09-11

User accepted the higher inverted-U version ("좋네 이대로 커밋 푸시") and
requested committing and pushing the current feature branch. This supersedes
the pending visual acceptance below; no further behavior changes are included.
- [x] Fresh pre-commit verification: Core241/App816 Release tests and browser
      controller8 passed; diff whitespace check clean. App report:
      tests/Dororong.App.Tests/TestResults/commit-pounce-accepted.trx.
- [x] Scope reviewed: native pounce, head/cheek-only drag routing, preview
      sources/docs and regression tests. Generated TestResults stay local.
- Delivery: commit and push on feature/dororong-m1-expression-animation,
  including the two preceding local checkpoints. No merge, PR, runtime restart
  or worktree cleanup requested. Confirm remote commit parity after push.

## Higher inverted-U trajectory — 2026-09-11

User requested changing only jump trajectory, keeping the accepted easing.
Raise arc9→12DIP in native/browser samplers; preserve horizontal easing/travel,
340ms flight, body tilt/scale, landing, trigger and3s tracking.12DIP stays within
the existing platform hop limit, so no physics/input changes are needed.
- [x] Native sampler/both-facing product peak tests observed3 RED failures;
      browser quarter/peak/end test observed1 RED failure before changing height.
- [x] Focused/native full verification, inspect regenerated WPF contact sheet,
      publish new runtime and replace exact PID46680; preserve prior runtime.
      Focused14 native/browser8 GREEN, full Core241/App816 GREEN; black WPF
      sheet inspected; diff check clean. Running PID14064 from
      C:/Users/tjdwo/Downloads/doro/pounce-arc-20260911/runtime with responsive
      process and both loaded module paths verified. App/Core match tested DLLs.
      App SHA256:D85ED2271CD052D201F7ED155B5908358702B76B9DC4DA18B0AE6E2780DC7647
      Core SHA256 unchanged:6F86FB89DCD3CBBD0350BEDE7FD9877977871B7356571B234213A1D735971847
      Previous pounce runtime retained. No commit/push; user visual feel pending.

## Native short-pounce implementation plan — 2026-09-11

**Goal:** Promote the approved preview into the running desktop product.
**Architecture:** A deterministic PounceSession in Controls owns watch/flight/
landing/track timing and per-tick horizontal travel. Presenter composes the
existing hunt atlas/head/eyes with the approved forepaw pivot. PetLoop applies
horizontal travel and the retained-support vertical offset through platform
physics, not a second window-position writer. Direct input/Sit/perch/unsupported
physics cancel preparation and take priority. No new raster assets or packages.
**Spec:** tools/PreviewLocomotion/POUNCE.md plus the approved v3 preview.
**Constraints:**1.5s continuous near dwell;340ms flight;280ms landing;3s upright
tracking before fresh dwell;28DIP maximum travel stopping8DIP short;9DIP arc;
direction locked during jump; preserve head/cheek-only dragging. Existing linked
worktree and prior dirty changes retained; no commit/push requested.

### Task 1 — Native timing and motion sampler

Files: create src/Dororong.App/Controls/PounceSession.cs and
tests/Dororong.App.Tests/Controls/PounceSessionTests.cs.
Interface: Advance(seconds, near, targetDeltaX, facing, blocked=false) returns
PouncePose with Phase, DeltaX, OffsetY, Direction, Age, TrackingRemaining,
AngleRadians, ScaleX/Y. Reset retires timers without resetting world position.
- [x] RED: Advance1.49s remains Watch; another.01s enters Flight;170ms has
      DeltaX14/OffsetY-9 for distant right prey. Departure resets dwell;
      direction changes cannot reverse flight. Landing enters Track for full3s;
      pointer may stay nearby but fresh1.5s required; blocked resets without travel.
- [x] GREEN: port preview state boundary consumption and analytic pose equations;
      guard nonfinite delta/target. Run dotnet test tests/Dororong.App.Tests -c
      Release --filter FullyQualifiedName~PounceSessionTests.

### Task 2 — Product integration and delivery

Files: DororongPresenter.Hunting.cs, PetLoop.cs, Runtime/PetLoopRuntime.cs;
tests/Runtime/HuntingLoopTests.cs and new Runtime/PounceLoopTests.cs;
new Controls/PouncePresentationTests.cs. Add a host GetPouncePose callback for
opt-in native integration; legacy hunt-only test hosts remain explicit.
- [x] RED: actual presenter/loop nearby pointer produces positive forward travel
      and a raised sole, lands on the same support, stays upright/fixed during
      tracking, then repeats only after fresh dwell. Both directions; head input,
      Sit and owner loss interrupt without snapback. Existing head/cheek tests pass.
- [x] GREEN: advance controller from accepted near geometry; supply pose via
      host callback; apply one bounded position update through platform support.
      Render flight hunt-age interpolation to2.05, landing to2.6, upright track
      frame156; keep head/eyes/body tracking active outside prep radius in Track.
      Apply approved forepaw-pivot angle/scale before platform sole correction.
      Evidence:4 controller failures and3 loop failures observed before their
      implementation, then5 controller/6 real-WPF loop tests GREEN. Both-facing
      contact sheets inspected at artifacts/repro/pounce-product-20260911.
      Review follow-up: mid-flight Sit/body ClickOnly reproduced2 RED failures
      (9DIP instant drop). Clearing the supported hop offset retained its owner;
      now that cancellation releases support from displayed height into falling,
      without altering direct head/cheek carry or real owner-loss physics.
      A quick ClickOnly press+release between ticks reproduced one further RED;
      its queued click now triggers the same support release. Focused5 controller/
      9 loop tests GREEN. Scoped review approved the corrected guard. An obsolete
      full run was stopped (545 tests completed) after a concurrent test rebuild
      hit its DLL lock; that aborted run is not a pass. Final frozen suite reruns
      use native-pounce-verified.trx and run without concurrent test builds.
- [x] Verify Core/App Release suites; capture actual WPF flight/landing/track
      frames on black/white backgrounds; inspect outlines and frame transitions.
      Independent read-only review; fix concrete blockers with RED/GREEN evidence.
      Final Core241/App816 GREEN (1057 total), preview controller7 GREEN, diff
      check clean, hunt/locomotion asset hashes unchanged. Scoped final review
      approved after the three cancellation regressions; native contact sheets
      show both directions and upright tracking. This is WPF automation, not a
      claim of live Windows pointer-feel acceptance.
- [x] Publish a new runtime directory, compare DLLs to tested assemblies, replace
      exact PID12640 only after green verification, verify modules/responsiveness.
      Keep prior runtime as rollback; report live user feel separately.
      Running PID46680 from C:/Users/tjdwo/Downloads/doro/pounce-20260911/runtime.
      Both loaded DLL paths and responsiveness verified; prior runtime retained.
      Tested/published App SHA256:4B9C736948B90FD395B678B3939A07C8ED1E5DFC07CAF82A61A39CE53D4B8337
      Core SHA256:6F86FB89DCD3CBBD0350BEDE7FD9877977871B7356571B234213A1D735971847
      No commit/push. Live user acceptance remains pending.

## Approved head/cheek-only product dragging — 2026-09-11

User approved removing arm/front-paw, belly and rump drags from the desktop
product. Preserve head carry, cheek pulling, normal click reaction and Sit;
disabled body regions must not fall through to head carry. Pounce stays preview-only.
- [x] Product press boundary emits ClickOnly for body regions, without creating
      a body-pull capture. Perch/sleep/splat fallback is limited to head regions.
      The archived region renderer/session and their diagnostics remain intact.
- [x] Click-only button ownership cannot enter carry; threshold movement cancels
      the click even if the pointer returns. Short clicks remain normal clicks.
      Freeze the displayed posture during the hold; keep sit/perch ownership.
- [x] Expected RED failures observed for actual body routing, loop ownership,
      core carry, seated-pose replacement and landing-body fallback; focused
      GREEN checks now pass, including head/cheek preservation and both facings.
- [x] Full Release Core241/App802 passed; scoped read-only review found no
      blocking issues. Diff check passed; hunt/locomotion assets unchanged.
      Published C:/Users/tjdwo/Downloads/doro/head-cheek-only-20260911/runtime,
      verified App/Core DLL parity with tested assemblies, replaced exact old
      PID16804 with PID12640. Responsive process and both loaded module paths
      verified. Old hunt-wake runtime retained; no commit/push or pounce port.
      App SHA256:7F51E8A233B2F5A9DF4E6D5B959920FF348ECDA19788CFF309711BE6F4F4788C
      Core SHA256:548A14167294D44887839268D6CF6F796E2EA5DE7507A6FF88C0EBCB09B9EB42
- [ ] Live user interaction acceptance (automated WPF tests are not live input).

## Approved post-pounce tracking state — 2026-09-11

User approved a separate upright tracking state after landing: hold position,
track head angle/whole eyes/body facing for3s, then prepare if still nearby.
Fresh1.5s dwell required before another hop; departure cancels preparation.
This supersedes the earlier one-shot/departure-latch rearm rule below.
- [x] Explicit watch/flight/landing/track controller with boundary-aware time
      consumption. Preserve existing hop geometry, no product modifications.
      Auto preview/scrubber extended to show tracking and the second jump.
- [x]4 expected RED failures before implementation, then7/7 controller tests
      GREEN. Browser test verifies upright frame, fixed pose/position, body
      flip, changing head/eye pixels,3s wait and fresh dwell without leaving.
      Prior hunt checks/syntax/diff checks pass. Inspected tracking-black.png
      under artifacts/repro/pounce-preview-20260911. Opened2800/?v=3.
- [x] User approved native integration; delivered by the native plan above.
      Checkpoint719d300 and prior hunt-wake/head-cheek runtimes preserved.

## Approved short-pounce preview — 2026-09-11

User approved the short forward-hop design, adding sustained nearby mouse as
the trigger. Preview only; desktop hunt-wake runtime remains PID16804.
Checkpoint before this work:719d300 (local only).
- [x] Separate pounce controller:1.5s continuous dwell, reset on departure,
     28px maximum native travel capped short of target,9px low arc,340ms
      flight and280ms forepaw-pivot landing/recovery. One shot per approach;
      rearm after leaving350ms and a1.2s post-landing rest. Lock flight facing.
- [x] New preview at127.0.0.1:2800, PID16752; reuse approved hunt atlas/curves
      without modifying existing preview2799 or native assets. Automatic demo,
      real mouse mode, slow playback, pause/scrub, both directions/black ground.
- [x]4 controller tests observed RED before implementation then GREEN; prior
      hunt checks pass. Headless Chrome real-pointer test passes dwell/reset,
      one-shot/rearm with no page errors. Inspected flight/landing white/black
      proof at artifacts/repro/pounce-preview-20260911/contact-sheet.png.
- [x] Independent review found cooldown swallowing an earlier departure;
      latched the qualifying exit. Regression RED then5/5 GREEN; rereview clear.
      Updated preview opened at2800/?v=2; native assets/process unchanged.
- [ ] User visual approval before product port.

## Save checkpoint / next short pounce — 2026-09-11

User requested saving the accepted product through hunting wake-up, then a
short pounce. Checkpoint current product, assets, preview sources and regression
tests in local Git; no push. Last full verification: App794/Core239 GREEN.
Keep hunt-wake-20260911/runtime running unchanged while the next motion is
designed. Next boundary: approve a preview-only crouch -> short forward hop ->
soft landing sequence before implementation and later product integration.

## Hunting wake-up regression — 2026-09-11

User reports a sleeping pet does not react to nearby mouse. Legacy proximity
reactions are suppressed, but hunting entry excluded Sleep. Permit sleep in
host eligibility and wake to Idle on the accepted hunting hold in Core;
reset inactivity and face pointer without restoring retreat. Keep all direct,
sit and platform blockers and the accepted animation unchanged.
- [x] Real loop/presenter tests RED for both approach sides (Sleep instead of
      Idle), then focused71 GREEN; far/invalid pointer stays asleep, near wakes
      without translation, sustained response and eventual sleep verified.
      Core239 tests pass.
- [x] Full App794/Core239 pass (1033 total). Published hunt-wake-20260911/runtime
      without rebuild; App/Core DLL hashes match tested outputs. Replaced
      identity-checked21764 with responsive16804; module paths verified.
      Previous immediate-crouch runtime preserved. App SHA256
      88CE996275EC0F4C657B7CA39BF5052F339C93FE0123DC345637A3B431FC2F2E.

## Reaction latency correction — 2026-09-11

User reports reaction still too slow after faster lowering. The retained250ms
notice pause delayed visible response. Skip that pause in display-frame time;
lower on the next presentation tick, complete250ms after detection. Keep art,
gaze, proximity, sway and recovery behavior unchanged.
- [x] Timing and native rendered-frame regressions:3 expected RED failures
      (including16ms entry and250ms endpoint), then69 focused hunting/sit/
      facing/locomotion tests GREEN. No full-suite rerun for this narrow remap.
- [x] Published immediate-crouch-20260911/runtime without rebuild; App/Core
      DLL hashes match tested outputs. Replaced identity-checked PID22076;
      prior fast-crouch runtime preserved for rollback.
      App SHA256 A8627511B8A8B6579E305F8D1EF134BC1821C2465FCABE6DA1DAEEBA053D3413.

## Approved faster crouch — 2026-09-11

User approved initial lowering450ms ->250ms; retain notice pause, art, wiggle
and standing-up timing. Compress only the displayed entry-frame interval,
hold its endpoint until the original wiggle starts; session/recovery clock
unchanged. Verify timing contract and native hunting/interaction regressions;
publish separate tested runtime and replace only identified current pet.
- [x] Timing and actual WPF full-crouch frame tests RED/GREEN. Focused68
      hunting/manual-sit/facing/locomotion tests pass. No full-suite rerun for
      this isolated frame-time remap; preceding full1028 baseline remains.
- [x] Published fast-crouch-20260911/runtime without rebuild, App/Core DLLs
      match test outputs. Identity-checked14696 replaced with responsive22076;
      loaded module paths verified. Previous mouse-response runtime preserved.
      App SHA25677DD909DC607A5206F9FCA6A0FB78A933F9E560FCD77396FFD6B28EC0896EAA2.

## Approved mouse-response correction — 2026-09-11

User approved body-facing tracking, replacing old proximity retreat/curious
with hunting, stable boundaries and immediate smooth re-entry. Keep sit/direct
and platform priority. No art changes or perpetual pursuit.
- [x] Reproduce old reaction interception, missing body turn, boundary chatter
      and delayed re-entry in real presenter/loop/session tests.
- [x] Suppress legacy pointer reactions only for hunting-enabled hosts; track
      screen facing with central deadband; hysteretic proximity and continuous
      re-entry. Preserve direct input and post-hunt walk-facing consistency.
      Initial5 behavioral REDs and stale-click-facing RED verified. Review
      found early-tail sway discontinuity in first re-entry fix;4 RED cases
      at1.75/1.9/2.0/2.3s now GREEN via authored-tail reversal. Focused56 tests
      pass, including alternating direction while supported on taskbar.
      Independent rereview has no remaining findings.
- [x] Full Release regressions/review; publish separate runtime, verified
      DLL parity, exact-process replacement with previous build preserved.
      Core239/App789 pass (1028 total). Published mouse-response-20260911/runtime
      without rebuild; both DLLs match test outputs. Replaced identity-checked
      PID26352 with14696; process response and loaded module paths verified.

## Approved hunting product integration — 2026-09-11

User approved2799 with "이대로 적용". Port this visible behavior into the
existing WPF pet, retaining walking, manual sit, blink, drag, platform/z-order
and identity behavior. No pursuit, jump, installer, commit or push.
Existing isolated feature worktree verified; dirty changes are preserved.
Planning/execution ledger remains this file per AGENTS.md (no second ledger).

Architecture: embed exact preview body layers, port the small dynamic
head/eye/neck/outline compositor to WPF, and gate a numeric hunting session
at the existing pre-core suspend seam. Direct/platform/manual-sit ownership
preempts hunting. Pointer coordinates remain DIPs and are converted to native
96px presentation coordinates; facing mirrors gaze once at the boundary.

### Task 1: Numeric motion and session port

Create Controls/HuntMotion.cs and Controls/HuntingSession.cs plus focused
tests in tests/Dororong.App.Tests/Controls/HuntMotionTests.cs. Full spec is
tools/PreviewLocomotion/{hunt-motion,gaze-motion}.js; copy constants verbatim.
Interfaces: HuntPose(double Amount,double HeadBob,double HeadRoll,double Sway)
with static At(double seconds), instance Head(double x,double y) and
Map(double x,double y) returning PointD. HuntingSession.Advance(double seconds,
bool near,bool blocked=false), AgeSeconds, IsActive, Reset(). First nearby
advance starts age0; sustained near loops1.25..1.65; departure settles2.6.
HuntGaze.Advance(double seconds,PointD? target,bool flip,bool active) returns
HuntGazePose(double EyeX,double EyeY,double Roll); Reset(). Reject nonfinite
or negative delta and map invalid pointer targets to neutral like JS.
- [x] RED runnable inactive baseline against crouch, frame-rate-independent
      gaze lag,120s sustained loop, smooth departure, block/reset/re-entry.
- [x] GREEN literal JS parity cases, fixed toes, mirrored gaze and invalids.
- [x] Independent scoped spec/quality review before integration completion.

### Task 2: Embedded layers and native compositor

Create preview export-product-hunt.cjs and Controls/HuntFrames.cs,
HuntRenderer.cs. Extend JS compositor with an explicitly requested layer-only
export path; normal preview output remains exact. Embed157 body frames and
two native masked head eye states. WPF independently rotates head, shifts
whole eyes, draws existing joining curves and repairs final forebody ink.
Preserve native96 registration and transparent hit testing; no browser/runtime
dependency. Tests exercise real renderer, clear/closed eyes, native geometry,
left/right presentation and output comparisons to frozen2799 proof images.
- [x] RED missing embedded resource assertion. Real renderer/parity tests were
      added with implementation; no claim of a full visual-test RED baseline.
- [x] GREEN render/alpha/parity tests and WPF white/black proof inspection.

### Task 3: Runtime integration and verified local delivery

Modify PetLoopHost/PetLoop pre-core hook and a presenter partial, with minimal
existing presenter touchpoints. Pre-core samples nearby pointer and holds
autonomy while hunting; render uses current facing, shared blink clock and
exclusive eligible ownership. Reset for unavailable/blocked input, sit,
direct presses/carry/settling and platform motion; proximity exit settles.
Add runtime/presenter tests proving hold/no walking drift, direct preemption,
manual sit, pointer unavailable recovery and no tracking while blocked.
- [x] RED actual presenter inactive baseline (2 assertions), then GREEN
      integration; real-loop hold/resume/drag/sit and taskbar support tests.
      Review caught lowered cheek being classified as a front paw: runnable
      RED (19.11,71.01), then inverse-head classification and head-only captured
      cheek deformation. Zero-pull capture and rear-body pixels remain exact.
- [x] Independent integration rereview: no Critical/Important findings.
      Sustained120 WPF render + loop/platform ticks mean9.55ms, fixed worldsole.
- [x] Core239/App778 Release tests pass (1017 total); broad integration
      rereview clear and white/black native WPF proof inspected.
- [x] Publish to a new local runtime directory, verify tested DLL parity,
      identify only the existing pet process, restart to new tested runtime.
      Preserve previous directory for rollback. No live-interaction PASS
      claim without direct observation.
      Delivered hunt-product-20260911/runtime, PID26352. No previous pet process
      was running, so no process was stopped. Previous runtimes preserved.
      Computer Use window inventory does not expose the transparent tool window;
      live pointer interaction is not claimed. Process/module checks plus actual
      WPF rendering and loop/platform tests define this delivery's evidence.

## Hunting-preparation preview — 2026-09-10

User approved preview only: notice nearby cursor, lower forebody with planted
paws, briefly wiggle the pelvis, return to rest. No pursuit/jump, no desktop
integration, no changes to approved walking/seated art or interaction logic.

- [x] Isolated hunt-motion.js + exporter/page using the existing original-art
      renderer through a proof-only body-field override. 157 frames at 60fps.
- [x] TDD RED idle baseline lacks crouch; GREEN forebody lowering, raised rump,
      planted paws, positive mesh Jacobian, temporal continuity, alternating
      sway and proximity latch/cooldown. Initial overly deep compression folded
      short foreleg cells; reduce head lowering to 4.2 native pixels, test GREEN.
- [x] White/black contact sheets inspected; browser pause/scrub72, black,
      actual nearby pointer activation checked. Narrow-panel screenshot exposed
      whole-canvas downscaling; responsive backing width now retains sprite size.
- [x] Preview http://127.0.0.1:2788/ (server PID43504), automatic demo + mouse
      proximity mode, native/2x sizes, mirror, half speed and frame scrubbing.
- [ ] Await user visual feedback. Do not promote to product without approval.

### Independent eye/head gaze addition (preview only)

2026-09-11 visible connection-ink correction: user reported broken/thin ink
under the chin and between fore/hind feet in2798. Measured actual composite
edge contrast: native x22=0.094, x26=0.052, x48=0.015 (normalized ink), despite
opaque-alpha tests passing. Neck/head white coverage extends past hidden
strokes; the replacement feather also averages misaligned ink into white.
Resolve outline ownership at the final composite: derive subpixel alpha
boundary distances inside two narrow forebody windows, restore only missing
body-neutral ink, preserve all alpha and skip pink/colored hair/ribbon.
Feather repair into unaffected ink and fade with crouch entry; exact rest,
source assets and movement remain untouched. RED visible-edge contrast,
GREEN six measured edge columns plus all nine focused suites. Browser2799
(PID12060), HTTP200, white paused60 and black mirrored playback verified.
Extreme-gaze contact proof is still study-only (430 covered channels), not
a no-overlap pass. Preview only; awaiting user visual feedback.

2026-09-11 independent foreleg-curve trial: user rejected2797's thin-root/
bulbous-tip appearance and approved separate curves for both forelegs.
The preview compositor now replaces only the lower forebody domain with
two rounded cubic limb contours and a shared belly connection; control
points interpolate from the old pose into the authored hunting curves.
An early-entry blend preserves exact rest; the right-hand body attachment
uses complementary premultiplied coverage (lighter after destination-out),
not double source-over alpha. RED actual composed far-wrist gap; a second
RED isolated-body alpha seam was hidden by the hair in whole-image checks.
Both GREEN plus the eight existing focused suites (nine total), preserved
130 seated/standing poses, loop endpoints and separate forefeet.
HTTP200/browser2798 (PID45696), paused60 white, paused30 black/mirrored
and resumed playback inspected. Existing extreme head/foreleg occlusion
remains412 covered channels, disclosed study-only. No source PNG edit or
product integration; user visual approval pending.

2026-09-11 forepaw wrist correction, user approved immediate fix after the
2796 crop showed hooked ankles. Root cause: the 11px reach was concentrated
over a 5px wrist (local horizontal shear 2.7–3.4), so toe-mass checks alone
missed the hooked connection. Distribute reach over an 11px foreleg span,
offset independently for the near/far roots. Keep the toe volume, original
11px reach/floor anchors, continuous wiggle and gaze behavior. Added a
local-shear regression (RED against old field); the initial 13px span tucked
the root too far forward and failed the existing diagonal-extension check,
so the final 11px/root-offset geometry preserves both requirements. Eight
focused checks pass, including 130 unchanged seated/standing frames.
Preview2797 (PID26316) verified HTTP200 and browser white/black, mirrored,
paused frame60 and continued playback. Extreme-gaze overlap remains214
covered channels (study only, not a clean-overlap PASS). No product update
or source-image edit; user visual acceptance pending.

2026-09-11 approved plump-forepaw refinement: user requests blunt thick cute
forepaws, retaining the extended reach. Reach deformation now finishes over
the upper5px wrist rather than shearing through the entire8px pad; compress
the pointed leading ankle corner while preserving the two toe-center anchors.
Add rounded pad volume with a destination-space monotone vertical warp,
not source-space inflation before the wrist shear (that draft folded cells).
Limit the volume field before the far hindpaw so all hind anchors remain fixed.
No raster source replacement; existing authored ink is reshaped continuously.
RED old pointed far-toe cross-section; GREEN plump toe mass, original11px
reach/floor anchors, exact loop endpoints, positive Jacobian, head/eye behavior
and130 preserved seated/idle poses. Eight focused checks pass. White/black
extreme proof inspected: wider rounded pads rather than the previous fins.
Near-toe measurement follows its bottom connected interval so the raised top
is not accidentally clipped by a fixed y80 measurement window. Existing
extreme-gaze hair/forepaw overlap is188 covered channels (study only).
Latest preview2796; no desktop product, original PNG, commit or push changes.

2026-09-11 approved extended-foreleg stalking pose: user approved forepaws
reaching forward on the floor with a lower chest/head, raised rump and all
existing gaze/sustained wiggle behavior retained. Forepaws reach11 native px
during eased entry and retract during recovery; held-loop soles stay fixed.
Head lowering6.3px (previous4.2), pitch-.07rad (previous-.055). Head pose values
now come from hunt.sample for body field, exported whole frames and live
head/neck composition, avoiding mismatched renderer constants.
Compression begins at torso y60 rather than only y70 to avoid collapsing the
short foreleg. Root-specific reach starts at each foreleg root; normalize
over its own8px length, not the interpolated hindfoot floor (that draft
inverted cells near45,81). RED absent extension; GREEN two forward diagonals,
deeper head/chest, floor contact, held forepaw stability, positive mesh and
continuous loop/departure. Fresh seven focused tests pass including130
preserved seated/idle poses. White/black standing/crouch extremes inspected.
Deeper head increases the pre-existing extreme-gaze overlap study count to214
covered channels; this is disclosed preview debt, not a strict composite PASS.
Latest preview2795/server44972; no desktop product or source-art edits.

2026-09-11 approved sustained hunt refinement: user confirmed bounded design
for a softer rounded rump,1.5x wiggle, continued nearby hunting and smooth
departure. Broaden the apex vertically; upward-looking ribbon now occludes
the rounded shoulder instead of dragging it down into a pointed nub.
Sway amplitude .95 ->1.425 native px. Session loops only the settled source
span1.25..1.65s; entry is not replayed. Departure joins the eased tail at the
next matching zero-velocity peak, then releases the crouch. Returning before
that peak cancels the pending departure; returning during recovery finishes
settling before a fresh approach. Auto mode demonstrates continuous holding.
RED old nearby session returned idle; old high apex failed broadness test.
GREEN long holds (120s), bidirectional amplitude,20s loop continuity,
six departure phases, mesh orientation/planted paws and actual loop raster
endpoints. All seven focused checks pass, including130 preserved seated/idle
frames. Native-density curves retain original softened ink. White/black
extreme proof inspected; unchanged62-channel hair/forepaw overlap disclosed.
Latest preview2794/server41248; browser nearby hold stays in wiggle beyond
the old one-shot duration; moving to the heading outside the canvas returns
to rest/frame156, directly observed. No product integration or source-art edits.

2026-09-11 whole-haunch correction: user rejects2792 as still angular.
Earlier edits rounded only the cap but retained the flat original side,
so that partial-boundary approach did not satisfy the requested silhouette.
Replace the preview-only rear contour from ribbon to hindleg (native y74)
with two tangent-continuous arcs around one outward apex. Preserve the
authored hindleg below, all head/eye/neck behavior and production rendering.
RED/GREEN real-composite haunch bulge regression catches the former flat side.
Fresh seven focused tests pass, including130 preserved seated/idle frames.
White/black neutral and both rotation extremes inspected: full rear reads as
a rounded volume, not a rounded cap attached to a straight side. Latest2793,
server22540. Await user visual feedback; preview only. Previously disclosed
62-channel extreme-angle hair/forepaw overlap remains outside this change.

Latest front-neck/curve follow-up: user reports a left-side cut on upward gaze
and a straight-looking rear join. The left cut is exposed missing torso under
the rotating head, not a canvas crop. BodyOnly replaces only the old front cap
above native y73 with a moving, filled/inked neck connection behind the head;
the original forepaw below stays intact. Rear controls now bow outward into a
rounded shoulder rather than the earlier diagonal chord. Both joins are sampled
at native art density then enlarged, matching the original softened ink.
RED/GREEN: rounded-back coverage point fails before curve tuning; left-neck
interior coverage fails before front join. Fresh seven focused checks pass;
added real-composite coverage through7 hunt stages and3 upward angles passes.
White/black extreme contact sheet and mirrored browser frame72 inspected.
Latest2792/PID35876,20deg/eyes/timing unchanged; preview only, no product changes.
The previously disclosed front-neck gap is addressed. The separate62-channel
hair/forepaw overlap at extreme angles remains an explicitly reported study
limitation. Await user visual acceptance; do not promote this to product yet.

Latest upper-rump cleanup: user approved20deg and requested removal of the
tail-like spur plus the moving back's broken outline. Layer inspection showed
stationary-body donor pixels surviving at the old ribbon attachment, while
the isolated body had no contour to the rotated attachment. Preview-only
bodyOnly now omits that cap; compositor joins the current ribbon to the pelvis
with a short filled/inked underlay, behind the original head/ribbon and body.
No source PNG or product bank changes. RED: exterior alpha214 instead of0;
after removing cap, RED missing back fill; GREEN no spur + filled/inked back.
Fresh hunt/gaze/whole-eye/neck/layer/rump and four-leg render tests pass,
including130 preserved seated/idle poses. Neck marker test now isolates green
excess instead of accidentally measuring the new white underlay's green channel.
White/black standing/crouch extremes and browser playback/mirroring inspected.
Latest preview2791/serverPID172; old previews remain separate.20deg, eye travel,
timing and accepted walking unchanged. Front-neck separation/62 lower-body
overlap channels at extreme20deg remain the disclosed study limitation, not
a claim of production readiness. Await feedback; desktop product untouched.

20-degree amplitude study (latest): user explicitly requests trying max20deg.
Controller limit now PI/9, original horizontal/vertical ratio and eye travel
unchanged. RED/GREEN requested range; neck-pivot and whole-eye tests pass.
Full-crouch downward cap stays-.08rad; standing allows the full requested range.
Known study limitations: extreme head angles expose the neck seam and overlap
the lower-body area (62 differing covered channels in composed contact sheet).
Strict export-gaze-proof still asserts on overlap; explicit --study-overlap
exports/reports it for this comparison, not a product verification pass.
Visible page labels this20deg comparison and limitation. Same2790/server35236,
reloads scripts on refresh. No product integration; neck support needs work
before this amplitude could be shipped.

Latest amplitude follow-up: user finds head turn too subtle. Double standing
rotation coefficients to .07/- .09 (up to .16rad, about9.2deg); whole-eye travel
and timing unchanged. Lowered hunt pose retains downward clearance by halving
only negative gaze angles at full crouch; upward and standing range stay doubled.
RED/GREEN larger visible turn; fixed neck, unchanged eye behavior and actual
visible lower-body pixels pass. Corrected composite test to distinguish empty
space beside a paw (hair may rotate there) from actual existing body coverage.
White/black extremes inspected; preview2790, PID35236, tab6 reused/look-only.
Allowlisted preview scripts now reload from disk on request for future tuning;
no desktop product changes or restarts.

Latest user correction supersedes the iris-only/translated-head design below:
move each WHOLE EYE (including outline), and ROTATE the head about its neck.
Implemented original-texture eye translation plateaus with local skin falloff;
removed fabricated sclera and independent iris displacement. Head translations
are zero; gaze rotation pivots at native(43,66), bounded to .08rad. Upward targets
raise the left-facing front; mirroring preserves vertical response.
RED/GREEN for missing whole upper-eye outline motion and unwanted head position
shift; actual compositor neck-marker fixture stays pinned. Eye alpha, mouth,
outside regions, rest restoration, lower-body pixels and prior gait tests pass.
Browser verified look-only and upward pointer response on NEW preview2789,
server PID39144, existing tab6 reused. Old server32236 was left running after
the requested restart command was rejected; no product process was stopped.
Product integration still awaits user approval.

User approved adding subtle cursor-following iris and head separately, layered
over the hunt motion. No new feature authorization for the desktop product.

- [x] Separate head/hair/ribbon and body layers. Optional bodyOnly proof output
      added to preview renderer; default walking/seated rendering remains exact
      in the existing 32 walking / 130 idle+seated regression comparison.
- [x] Fixed iris-interior masks with subpixel texture displacement and pale
      revealed sclera. No artwork files overwritten. Exact neutral restoration,
      unchanged alpha and outside-eye pixels verified against actual rendering.
- [x] Independent time-based lag: eyes75ms, head230ms; iris bounded to .85/.65
      native pixels, head .8/.65px and .035rad. Near-zone only, smoothly returns
      when cursor exits; horizontal target transforms correctly under mirroring.
- [x] RED then GREEN: missing gaze reaction, body layer containing old face,
      inert iris rendering. Existing hunt and four-leg render tests pass.
- [x] Real composed standing/crouched extremes preserve all tested lower-body
      pixels; white/black contact sheets inspected. Browser eye-only, combined,
      mirrored and black-background controls checked. New tab6 on same2788 URL
      replaces tab5's connection-refused page during server restart. PID32236.
- [ ] User visual review before any product integration. New controls include
      eye tracking/head tracking toggles and look-only mode for isolated testing.

## Four-leg walking follow-up — 2026-09-10

User rejects the seated vector proof because line style differs; retain the
existing PNG seated product. No rollback needed: vector was preview-only.
Do not integrate or further polish that rejected candidate without a new request.

User requests independent walking leg timing plus the fourth, occluded leg
slightly visible while walking. Read-only inspection finds rig.pose currently
uses offsets[0,.5,.5] for three legs, synchronizing one foreleg with the hindleg.
User approved with a correction: DIAGONAL PAIRS, not four staggered phases.
Front-left+rear-right together, front-right+rear-left together, opposite pair
half a cycle apart. Reference `C:/Users/tjdwo/Downloads/doro/1-2.jpg` shows the
subtle fourth paw. Use its placement/proportion as reference and the existing
transparent paw texture as donor, avoiding JPEG matte or a new stroke style.
Preserve idle/seated PNG, direction, speed, blinking and drag/landing behavior.
Implement rig/render changes with pair/visibility/continuity regression tests;
verify preview visually before promoting any regenerated product bank.

- [x] TDD: missing fourth joint RED, diagonal pairing GREEN; actual fourth-paw
      visibility RED(0/32) then GREEN(32/32). Pair offsets[0,.5,0,.5].
- [x] Rear-paw donor under body, not in body deformation field. Max newly visible
      alpha area32.33 native pixels; max44 pixels with alpha gain>40. All changes
      remain in the checked lower-body region; no JPEG or new vector stroke.
- [x] Preserve all130 idle/seated open/closed frames byte-exact versus frozen
      pre-change rig/renderer. Existing raster, joint ink, outline, rump fringe,
      seated PNG-bank and continuity regressions pass. All-four attachment and
      planted-foot checks added following independent review and pass.
- [x] Browser comparison on2787 (nodePID45468), baseline/new both32-phase2x
      renders; pause, half-speed, mirror, black/white checked. Fixed floating
      rounding selecting22 instead of23; actual slider now selects23 correctly.
- [x] User visual approval: "좋다 이대로 가자 너무 좋네."
- [x] Package frozen regression fixtures, verify approved-preview bank parity,
      rebake without touching sit/idle, run desktop tests, publish and relaunch.
      Baseline runtimePID37252, product bank E1D7249C...394, seated PNG C0C19BC...A5B7.

Released approved gait on2026-09-10. New bank SHA03983B66D55CF2EC3B7A6B7CD0B78C5AC609FF41F442B0197909B74CDA723ECE;
130idle/sit frames unchanged,32approved full-amplitude phases exact, all512walk
eye/amplitude frames exact to approved renderer. Bank gate RED oldproduct,
GREEN newproduct; goldens now packaged under fixtures/four-leg-baseline.
Core239 + App747 Release tests passed (986total,0failed/0skipped). Manifest,
premultiplication, seatedPNG endpoints pass. Fresh WPF left/right walk and sit
proofs inspected after full test completed. Independent release review no blockers.
Published --no-build from tested generic Release output to
`C:/Users/tjdwo/Downloads/doro/four-leg-walk-20260910/runtime`.
App C636C7BD8A8C2737E957C3DCF1D960758080DA952A8B437B2333810814111B51;
Core remains70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
Guarded oldPID37252 stop; newPID45496 launched. Old directory retained.
No C# behavior changes, commit or push. Evidence: docs/verification/2026-09-10-four-leg-walk.md.

At the earlier preview checkpoint: no Critical/Important reviewer findings. No WPF build, bank
rebake, product restart, commit or push. Frozen baseline evidence and test-only
harness remain local to this proof; preserve/package fixtures before shipping
these comparison tests to a fresh checkout. See four-leg proof REPORT.md.

## Topology-preserving seated vector comparison — 2026-09-10

User approved the revised curve/display-resolution approach. Execute inline
in the existing worktree; no release until visual approval of the new shape.
Goal: keep the front-paw cleft and head/ribbon while eliminating native96
rebaking from the proof's body outline. Spec: latest user feedback above and
the rejected comparison below. Tech: Node Canvas, scalable SVG, HTML preview.

- [x] Add `tools/PreviewLocomotion/verify-seated-vector.cjs`: actual rendered
      alpha regression in front-paw cleft, body alpha area, protected head,
      and sharp display-resolution boundary at 1x/2x/4x. Run against rejected
      proof and observe failures before adding replacement.
- [x] Add `tools/PreviewLocomotion/seated-vector.cjs`: explicit body Bezier
      curves with anatomical cleft control points instead of averaging; expose
      `createSvg(before)` using original head/ribbon and vector body/seam.
      Test the exported SVG itself, not only internal geometry.
- [x] Add `tools/PreviewLocomotion/export-seated-vector.cjs`: self-contained
      side-by-side HTML/SVG proof and black/white comparison. Render at target
      resolution; no 96px down-bake. Inspect proof before user handoff.
- [x] Re-run tests and record limits. Do not change product bank, motion,
      executable, installer, or source PNG; do not commit/push.

Static candidate REJECTED by user for mismatched line style. Never promote.
Alpha-contour fitting was abandoned: source partly-transparent body ink made
that trace jagged and detached the cleft stem. Final proof explicitly traces
the visible body/stem with cubic curves. This redraws the rear internal seam
as well; do not claim its original pixels are preserved. Body area -0.46%,
sampled cleft passes, protected head unchanged within2-channel-unit tolerance,
4x rump 10-90% edge widths1.27..1.58devicepx versus4.54..6.37 in rejectedproof.
These gates do not establish every local curve/stroke or topology is identical.
Independent read-only review: no blocker for static comparison; visual signoff
still needed. Browser verified both loaded at192x192 (fixed initial narrow-panel
aspect-ratio error), black/white switch,384x384 with accessible horizontal scroll.
Proof served locally on2786 by nodePID44844; no product restart or bank rebake.
Evidence: `artifacts/repro/seated-vector-20260910/REPORT.md`.

## Seated static edge stair-stepping — 2026-09-10

Latest visual feedback: candidate NOT approved. User identifies the front-leg
gap becoming joined/muddied and questions returning curves to low-res pixels.
Read-only ROI probe confirms alpha fills in the cleft, e.g. native(33,81)
88->202 and (31,80)126->255. Gaussian contour averaging has no protected
concave-gap landmarks; global ink matching does not preserve local anatomy.
The proof draws at8x but averages back to96x96, retaining coarse sampling.
Do not promote this candidate. Proposed next approach (not implemented):
preserve explicit paw-gap landmarks and render body curves at actual display
resolution, retaining head/ribbon art; assess loader/layout consumers before
changing bank resolution. No product changes for this diagnosis.

User clarifies this is the stationary seated contour, not sit/rise timing.
Current runtime is still seated-png-fix PID37252. No product change this turn.
Both walking and sitting use HighQuality WPF scaling and native96 bank frames;
raising pose count cannot change the held endpoint. Source seated PNG100x100
is registered without redesign and its endpoint is Canvas-scaled directly;
walking body uses deformed bilinear mesh sampling. Static source contour and
endpoint rasterization need visual comparison, not another timing adjustment.

- [x] Clarify spatial vs temporal symptom and trace both rendering paths.
- [x] Compare a seated-body-only contour antialiasing candidate against current
      source, preserving face/ribbon, pose, stroke weight and walking frames.
      Avoid whole-image blur or claiming simple upscaling creates new detail.
- [ ] Obtain visual approval before promoting a modified authored contour.

User approved comparison. Isolated proof tools only; no product pipeline hook.
Final candidate protects6152 head/ribbon pixels exactly;361 body-band pixels
change, area+0.391%, total ink-.003%. Editable lower rump(y69..78) curvature
jitter .22807->.11502. Initial y59..78 metric included the protected attachment
and remains slightly worse(.22373->.23285); explicitly retained in diagnostics.
Corrected ROI baseline RED/candidate GREEN. Do not equate ink-mass matching with
identical local stroke width or claim every contour is smoother. Independent
review verifies rendered parity and no blocker for static visual comparison.
`artifacts/repro/seated-contour-proof-20260910/comparison-compact.png` delivered
for visual approval; details REPORT.md there. Product remains PID37252 and bank
E1D7249C2427C0B021175C9B83B99D01F858CCCE7383BBD86FEFF239321DB394 unchanged.

## Transparent seated PNG integration — 2026-09-10

User supplied `C:/Users/tjdwo/Downloads/doro/10-2.png` in response to replacing
the seated JPG. Use this exact transparent source; no redraw or substitution
of head/body. Product-shell design remains paused; no motion/physics changes.

- [x] Validate source100x100: alpha0=6546, partial=491, opaque=2963; integer
      registration(+3,-12) retained. Source copied byte-exact with SHA256
      C0C19BC79337A9628E5B301153788928D9346357DD4284EA6167AE9DFF76A5B7.
- [x] TDD: 3 RED (border white paint deleted, actual PNG changed by old import,
      stale active JPG endpoint), then 3 GREEN. Authored-alpha sources bypass
      legacy opaque/JPEG matting, preserving exact registered endpoint.
- [x] Rebake65 poses/642-frame product; verify rise, alpha, source provenance,
      exact standing/walking preservation and black/white visual comparison.
- [x] Independent review, final product tests, guarded publish/relaunch.

Baseline banks/importer/manifest retained in
`artifacts/repro/seated-png-20260910/`. Legacy JPG remains for regression only.
All65 reversible poses match their bank (maximum step0.254/255); both seated
eye endpoints match independent PNG exports; all514 standing/walking frames
byte-exact to baseline. New bank test RED old-JPG endpoint then GREEN. Legacy
matte, active endpoint, rise-feature/color, manifest and preview delivery checks
pass. Independent review has no findings; reviewer additionally verified all128
seated native frames against atlas/eye patch. First App run746pass/1 live-native
ZOrderChanged failure; exact binaries passed focused probe and full rerun747/747.
Core239 passed. Preview server refreshed PID13924. Guarded oldPID27044 stop and
new sole respondingPID37252 under
`C:/Users/tjdwo/Downloads/doro/seated-png-fix-20260910/runtime`.
Tested/published/loaded AppD1F8712C8666E902A2C04404F424626BF33F0AB27F7E66B1BE08511E2A3EBF75;
Core70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
Rollback retained; no commit/push. See `artifacts/repro/seated-png-20260910/REPORT.md`.

## Seated outline follow-up diagnosis — 2026-09-10

User reports dirty seated outline on black. Pause product-shell design while
investigating this feedback. No production art/code/runtime changes this turn.

- [x] Reproduce from current embedded-bank source on black and white; compare
      canonical PNG, standing product and sitting product at native pixels.
- [x] Trace to authored JPEG import: previous matte fix explicitly excludes
      protected head/ribbon pixels, leaving JPEG white-composited edges there.
      Example native(35,24): canonicalRGBA0,0,0,67; standing58,49,52,84;
      JPEG187,189,188,255; sitting181,177,178,223. This visibly bright head fringe
      exists before WPF rendering. 51 head-edge pixels meet measured increased
      coverage/brightness criterion; diagnostic threshold is not a fix rule.
- [ ] Proposed correction: retain approved seated body geometry, reuse clean
      canonical transparent head/ribbon rather than JPEG head; verify join,
      eye states and all sit/stand transition frames before delivery.

Existing matte regression passes because it checks body edge(67,85) and
intentionally preserves the JPEG head. This does not establish whole-outline
quality. Enlargement/downsampling also softens standing and sitting, but does
not explain the much brighter seated-only samples above. Evidence:
`artifacts/repro/seated-edge-followup-20260910/source-standing-sitting.png`,
`fringe-samples.json`, `inspect.cjs`. Runtime remains PID27044, unchanged.

## Windows product shell and distribution — 2026-09-10

User authorizes progressing product identity, lifecycle and distribution work;
explicit constraint: do not change character functionality. Architectural
brainstorming applies because tray, single-instance ownership and installation
are new subsystems, not existing settings flows.

- [x] Inspect executable metadata and app lifecycle: current native process is
      Dororong.App.exe; description/product/company use development defaults.
      WPF tool window is intentionally absent from taskbar. Existing startup,
      fatal cleanup and window-close paths must remain intact.
- [ ] Agree distribution approach and product-shell design in chat.
- [ ] Write/review focused design; obtain written-design approval.
- [ ] Implementation plan, regression tests, isolated shell integration.
- [ ] Verify unchanged motion assets/behavior, install/upgrade/uninstall,
      single-instance/tray/exit and deliver with rollback preserved.

Proposed first release: user-local installed desktop app, display name
Dororong / 도로롱, executable Dororong.exe, existing art for product icon,
explicit version metadata, single instance, management-only tray, local error
logs, Start menu entry and registered uninstaller. Upgrade by running a newer
installer; no unattended network updater, service or automatic startup change.
Portable-only is smaller but leaves installation management incomplete;
server-backed auto-update is separate work requiring hosting/signing decisions.
No source/runtime mutation or restart during this design step. No character
art, gait, physics, interaction thresholds, sitting behavior or size changes.

## Walking upper-rump pixel spur diagnosis — 2026-09-10

User supplied walking vs idle closeups showing a tiny upper-rump protrusion.
Read-only bank contact sheet and in-memory renderer ablation identify the
body donor extension into protected foreground (`render.js`, chosen>=0 parts3
copy): it extends opaque/dark body texels beyond the actual source silhouette
near the ribbon/rump seam. Walking uses layered rendering; standing endpoint
uses direct input. Disabling only that donor copy in a diagnostic VM removes
the visible spur over sampled phases. This is NOT the local joint-ink repair.
- [x] Bank reproduction and one-variable donor-copy ablation.
- [x] User authorized implementation: constrain donor support to legitimate
      foreground coverage; verify seam continuity and head/ribbon preservation
      over gait amplitudes, both eyes and directions before rebaking product.
Diagnostics: `artifacts/repro/walk-rump-20260910/inspect.cjs`, `isolate.cjs`,
`bank-rump.png`, `donor-isolation.png`. No source-art, rig, renderer or product
changes in this diagnostic turn. Current product remains PID25400 direct-fix.

Implementation underway: confirmed exterior source(71,47)alpha0 receiving
bodyalpha255. Narrow donor gate rightof(69,48)corner now requires solid original
foreground, keeps premultipliedcoveragebounded and preserves otherdonors.
New verify-walk-rump RED255!=0 then GREEN. Full rebake comparison caught a
shifted 2px mesh lattice after donor trimming; retain its original origin.
It then caught closed-standing frame321 changing; retain the old donor texture
for all non-walking rendering. Final bank audit passes: all130 sit frames exact,
all512 walking frames change only within x69..74/y45..49 (9878 pixels total),
no new alpha coverage and solid joins retained. Raster, ink, walk outline,
product matte checks pass; visual bank sheet inspected and independent review
has no findings. Final Release App747/Core239 = 986 tests pass. Published and
restarted sole responding PID27044 under
`C:/Users/tjdwo/Downloads/doro/walk-rump-fix-20260910/runtime`.
Tested/published/loaded App8F9B6D3E1FD403FEA9E586A548FFBD219B114A1CB38B2128B6311EE5C62979FA;
Core unchanged70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
Old PID25400 exact path/hash guarded stop; rollback retained. No commit/push.
Evidence: `artifacts/repro/walk-rump-20260910/REPORT.md`.

## Independent live taskbar recovery — 2026-09-10

User authorizes fixing both-list false absence and product delivery. Native
relation trace during Ctrl+Escape found omitted primarybar still root/self,
parent0, with directly queried previous/next HWNDs present in enumeration.
Use remembered handle/PID only as discovery hints; refresh all metadata and
recover current sibling slot from validated live anchors. Never cache bounds
or invent z-order. True hidden/destroyed/occluded behavior must remain intact.
- [x] TDD regression: both-list omission, fresh geometry, native ordering,
      hidden/destroyed handling and unavailable ordering. RED5fail9pass ->14pass.
- [x] Native candidate capture and expanded regression checks.
- [x] Independent review, full tests, versioned publish and guarded relaunch.
- [x] Record delivery evidence and remaining live-acceptance limits.

Also added class-specific bounded discovery for cold start: knownhandle-only
candidate missedbar whenstartedmidomission, so primary/secondary firstcapture
tests RED2fail ->GREEN. Reviewer found inactivebar append couldpolluteactive
anchors: addedRED1fail16pass then deferredinactiveappend, rereviewapproved.
Final focused59pass, full App747/Core239=986pass, clean scoped diffcheck.
Final native candidate probe209/209successful; separate206/206nativevalidation
withoutomission. Published/restarted sole respondingPID25400 under
`C:/Users/tjdwo/Downloads/doro/taskbar-direct-fix-20260910/runtime`.
Tested/published/loaded App91C891000FC54360544F5A6A4882FF1F5D3A7A154BA309FAF1BFEC7CAF82C640;
Core70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
OldPID22132 exactpath/hashguardedstop; rollbackretained. Postlaunch156/156
nativecapturespassed, petY833throughout, no missingbar. Thosefinalsamplesdidnot
exercisebothlistomission; actualSearch/gamepostfixacceptanceremainspending.
All diagnosticprocessesfinished. No commit/push. Details:
`artifacts/repro/taskbar-direct-20260910/REPORT.md`.

## Intermittent window-switch dip — 2026-09-10

User now reports occasional dip during window switching on final overlay build.
Systematic-debugging: inspect actual runtime health/support, not merely an
independent native observer. No further physics/expiry workaround without cause.
- [x] Run unchanged delivered App/Core via bounded internal observer after each
      existing loop tick: scene health/age/failure, support, native pet/bar bounds.
- [x] Correlate user reproduction; distinguish source expiry, raw omission,
      occlusion and external HWND relocation before choosing a fix.
- [x] If cause confirmed, regression/test/review/deliver; otherwise record exact
      remaining evidence gap. Restore normal product after diagnostic run.

Internal trace confirms false published taskbar absence while direct shell
bounds/visibility remain unchanged, with actual pet Y833->873. No scene expiry
or NVIDIA occlusion. User narrows trigger to early SMAPI->game startup.
Bounded acquisition fix admits a currently traversed shell taskbar omitted by
the earlier EnumWindows snapshot, preserving z-order and fresh native checks.
The trace does NOT distinguish which acquisition list omitted it: this is a
reviewed candidate for that gap, not verified end-to-end resolution.
RED3fail4pass -> focused49pass; full App737/Core239=976pass; native21/21.
Independent review: no code blocker, controlled live trial ready.
Delivered sole responding normal PID22132 from
`C:/Users/tjdwo/Downloads/doro/taskbar-acquisition-fix-20260910/runtime`.
Tested/published/loaded AppCBB3FC223F8E027B0E39C0E8F7FEF4200AFF4AD6E66C9BD3397003A26ADABF47;
Core70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
Diagnostic39724 stopped after exact path/hash guard; rollback retained.
See `artifacts/repro/window-switch-internal-20260910/REPORT.md`.
- [ ] Live game-start acceptance of acquisition candidate; if it recurs,
      instrument both acquisition memberships to discriminate remaining cause.
No commit/push. No diagnostic recorder left active.

Post-delivery user reports two brief dips when activating Stardew Valley from
active SMAPI, then no further recurrence. Acceptance FAILED for complete fix;
current PID22132 still runs the candidate. No recorder was active for those
two events, so their cause cannot be assigned from the earlier trace.
Systematic-debugging pause: do not stack another behavior patch. Reassess the
snapshot-to-support contract and capture EnumWindows membership, z-order
membership, native classification, published support and pet position in the
same acquisition/tick before deciding another change. No restart this turn.

Windows Search activation report: separate45s read-only probe against delivered
candidate captured452successful reads and actual pet Y833->873 repeatedly.
50samples omit primarybar from native metadata;47 subsequent membership checks
omit it from BOTH EnumWindows and z traversal, while direct HWND read remains
visible/uncloaked at Y1040. Current one-list reconciliation cannot cover this.
Evidence: `artifacts/repro/search-activation-20260910/REPORT.md`.
Reassess treating top-level-list absence as physical taskbar disappearance;
direct verified shell state needs an independent acquisition contract, with
explicit ordering/occlusion handling rather than an invented z-order.
No additional product fix/restart; recorder finished. Search action timing was
not explicitly confirmed by user; this is a parallel native observer, not an
in-process source-boundary trace. Game/search exact common trigger unproven.

## Walking outline / SMAPI launch dip — 2026-09-10

User reports reinforced moving-body strokes thicker than rest and temporary
taskbar dip when SMAPI opens while another app's input is active. Keep separate
causal checks: no unverified blanket z-order/taskbar workaround.
Current product ordinary-blink runtime PID34812, unchanged during diagnosis.

- [x] Native-frame walking outline comparison and bounded seam-safe correction.
- [x] Read-only native launch trace: pet position, class/bounds/z-order, scene
      read health and derived taskbar segments; no titles, input or app content.
- [x] Review/verification and explicitly distinguish confirmed fixes from any
      unreproduced launch trigger before versioned runtime handoff.

Evidence: `artifacts/repro/window-launch-dip-20260910/REPORT.md`. User narrows
trigger to actual game appearing after SMAPI loading. Capture2 reproduced123
ProcessIdentity:5 failures: optional executable-path denial discarded whole
scene and expired taskbar support. Narrow access-denied fallback implemented;
other metadata failures unchanged. Capture1 separately recorded successful-read
primary-taskbar absence and real40px dip; that path is NOT claimed resolved.
Walking deficiency-only joint repair preserves130 sit frames and all512 walking
alpha/head/ribbon pixels. Independent review found no blocking defect. Full
Release tests App719/Core239=958 passed; actual native reads21/21 passed. New
native stroke/seam tests and baked642-frame/source-manifest checks passed.
Full8x32 joint sweep found767/768 profiles>=95%; one94.750517% is byte-equivalent
to the prior reinforcement at that sample, not a newly introduced loss.
Delivered new `C:/Users/tjdwo/Downloads/doro/walk-outline-launch-fix-20260910/runtime`
as sole responding PID19880 after guarded retirement of34812. Tested/published/
loaded App SHA1986C9BB5C49E742C8B5246E4A057DD0BDFFE5B4CCBD6B964C0BDFCF21B90541;
Core70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
GzipB039668E58645BBFBA102C7011540C417DB321B2522ED01AA9D90752D3659B27.
Previous runtime retained for rollback. No commit/push. Live post-fix game-launch
acceptance pending; metadata recorder `launch-20260910-081600.jsonl` started.
That45s recording finished:461/462successful reads, oneZOrderChanged; no identity
failure or missing primary taskbar and pet Y833 throughout. User confirmation
of actual game-start transition during that interval still pending.

User reports first delivery still drops. Longer081758 trace confirms NVIDIA
Overlay.exe HWND198370/CEF-OSC-WIDGET at0,0,1919,1080 front of the unchanged
taskbar: occlusion removes support, pet833->873; overlaydisappears13.2s later,
petreturns833. Exactclass/exe/layered+transparent-style nonphysical classification
fix in progress. Separate~0.4s filteredbarabsence remains under enhanced raw/
DWMcloak recording; do not claim all drops fixed from overlay case alone.

Final bounded handoff: exactNVIDIAclass+exe+layered/transparentflags classified
nonphysical; true appwindows stayordinary. RED3fail6pass -> focused40pass;
independent rereview no blockers; full App728/Core239=967pass. Final sole
respondingPID34372 from `C:/Users/tjdwo/Downloads/doro/window-overlay-fix-20260910/runtime`.
Tested/published/loaded App43D2A20E679F92E8D5CE4CF9BDC44BDA2FF8F0AE5581A535F31A54E6D1901170;
Coreunchanged70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.
PreviousPID19880 guardedstop androllback retained. All diagnosticrecordersdone.
UserclarifiesAltTabalone doesnottrigger; actualgame-start finalbuild acceptance
pending. Extra shortabsence/mismatch not claimed solved. No commit/push.

## Ordinary two-frame blink — 2026-09-10

User prefers walking's open/closed-only blink and requests identical ordinary
idle behavior: remove squint from ordinary blinking, use5s period/360ms closed
for idle, seated and walking. Keep sleep/wake and perch/direct art unchanged.
Use one continuous eligible idle/walk eye clock independent of motion phase.

- [x] Render-test RED/GREEN open/closed-only art and common timing, regression suite.
- [x] Review and versioned publish/relaunch, retain rollback, no commit/push.

Delivered: App711/711 + Core239/239 =950passed; scoped review approved.
Canonical idle, walking and seated open/closed-only timing verified; closed-eye
cheek capture/release pixel continuity retained by pausing the eye clock there.
No art changes. New ordinary-blink-20260910/runtime PID34812 replaces exactly
verifiedPID44440; old folder intact. Tested/published/loaded identities match.
Details: docs/verification/2026-09-10-ordinary-blink.md. No commit/push.

## Calmer walking blinking — 2026-09-10

User confirms both frequency and blink duration feel too fast, specifically
WHILE WALKING. Decouple walking bank eye timer from core phase; target5s
interval/360ms closed. Keep seated blinking, art, pose, breathing, interaction
and sleep/perch rendering unchanged.
Test actual rendered bank selection against cycling core phases, then publish
and relaunch a separate runtime without commit/push.

- [x] RED/GREEN timed walking eye tests and regression verification.
- [x] Versioned product publish, runtime identity check, preserve rollback.

Delivered: actual cause WALK600ms phase reused for24ms eye flashes. Walking-only
elapsed clock now360ms closed every5s, first blink2s after walking starts; idle/
seated blinking unchanged. Focused18/18; App709/709 + Core239/239 =948passed.
Scoped review approved, assets unchanged. New hidden runtime walk-blink-20260910
PID44440 replaces verifiedPID42020; old folder preserved. Module identities match
tested binaries. No commit/push; docs/verification/2026-09-10-walking-blink.md.

## Seated edge / cheek turning / walking polish — 2026-09-10

Approved: clean seated body exterior matte without changing authored head/pose;
seated cheek pull never carries/unpins, turns toward pull while stretching;
head/body carry still releases sitting. Correct desktop walk art direction and
halve speed42→21DIP/s (distance-driven gait therefore also halves). No commit/push.
Independent work: source-import matte agent owns JS/banks; speed agent owns Core
tuning/tests; root owns seated cheek and desktop-facing boundary/App tests.

- [x] RED/GREEN matte/protected head, pinned cheek long pull and reversal,
      physical facing vs travel, half-speed displacement.
- [x] Independent review, full tests, black/white raster checks, fresh versioned
      product publish and verified relaunch; retain current runtime as rollback.

Delivered: App Release708/708 + Core239/239 =947 passed; build0warnings/0errors.
Supported-surface seated long-pull/reversal/release and retained facing verified.
Independent scoped review approved, including added real-platform coverage.
73 body-edge matte samples recovered; protected head/ribbon and512walking frames
byte-exact. Original10-2 JPEG unchanged; native black/white proof inspected.
New runtime `C:/Users/tjdwo/Downloads/doro/seated-polish-20260910/runtime`
PID42020 replaces exactly verified oldPID20868; old directory retained unchanged.
No commit/push; see docs/verification/2026-09-10-seated-polish.md for module hashes,
full evidence and native-mouse verification limits.

## Manual sit command — 2026-09-10

Approved: context menu label `앉아`; stop autonomous travel and sit immediately
on this command, not on idle timeout. No separate unlock command: actual dragging
releases the hold, a mere click does not. Keep blinking, local cheek play,
existing carry/fall/landing/perch priority. No art, commit or push changes.
Bounded implementation in existing menu/presenter/loop and locomotion controller.

- [x] RED/GREEN command, no automatic sitting, stable position/blinking,
      click keeps hold, actual head/body/cheek carry releases hold.
- [x] Preserve direct recovery/source continuity and existing platform behavior;
      full tests, independent review, new versioned runtime and relaunch.

Delivered: final App Release705/705 + Core237/237 =942 passed; Release build
0 warnings/0 errors. Independent review approved after first-resumed local-cheek
frame regression (RED then GREEN). Hidden sit progress pauses through local
play/platform ownership; renderer/physics priority unchanged, actual carry resets.
New runtime `C:/Users/tjdwo/Downloads/doro/manual-sit-20260910/runtime/Dororong.App.exe`
PID20868 responding, sole pet process. Verified oldPID22444/path/hash before stop;
CloseMainWindow unavailable, stopped that exact process. Previous runtime intact.
App test/build/publish/loaded SHA256:
`2A3D97304B73E86B88CE4D3E3668C9B9947E7D1E3A60A1F9AAEA6005FF7BD5FC`.
Core: `96A04EFF4075EF73743A394B7B3C7C1D45C25AC17DF1DF00FE21968BEE35C980`.
No commit/push. Native mouse verification not claimed; actual menu/loop/WPF tests
and process/module identity verified. See docs/verification/2026-09-10-manual-sit.md.

## Approved locomotion product integration — 2026-09-10

Goal: apply the approved walking and final10-2 sitting/rising preview to the
desktop executable and relaunch the updated product. Spec: latest user
"ㅇㅋ 이걸로 적용해" approves the current preview, including feature transport,
continuous knee ink and two-pixel rump fringe cleanup.
Architecture: bake approved Canvas output into an embedded native96 Pbgra32
bank; a small presentation controller advances walking amplitude/distance and
reversible sitting amount. Existing direct interaction, sleep, falling/landing
and perch keep ownership; no new artwork or changes to their physics.
Tech: current .NET8 WPF, Node Canvas exporter, xUnit. Existing linked worktree
verified on feature/dororong-m1-expression-animation, HEAD212eb1a.
Execution: one tightly coupled implementation subtask with independent review;
controller validates runtime/package and performs the user-authorized deployment.
Global constraints: keep final10-2 JPEG unchanged, preserve all existing drag,
cheek, landing, window/taskbar and perch behavior; no commit/push or new tray
feature in this request. Runtime image96x96 is required by capture/hit pipelines.
Standing and walking retain facing; idle sit delay1s, transition650ms, smooth
rise before walking, gait start/stop blend180ms. Idle means resting on a support,
not airborne/perched. If interrupted by direct interaction reset locomotion.

### Task 1: Embedded approved motion and runtime presentation

Files: new tools/PreviewLocomotion/export-product.cjs,
src/Dororong.App/Assets/locomotion.pbgra.gz,
src/Dororong.App/Controls/LocomotionFrames.cs and LocomotionPresentation.cs;
modify Dororong.App.csproj, DororongPresenter.xaml.cs, PetLoop.cs and
Runtime/PetLoopRuntime.cs. Add focused xUnit tests under Controls/Runtime.
Consumed: current renderer/harness, approved65 PNG bank; fixed original logical
96x96 coordinate system. Produces frozen BitmapSource at96x96. Sit has65 frames
and optional open/squint/closed eyes, walk has32 periodic phase samples and8
amplitude levels plus standing. Crop renderer256 at32,32,192,192 and downsample
to96 with smoothing before premultiplication, avoiding padded-frame scaling.
Do not rerun morphing at runtime or allocate bitmaps each UI tick. Verify exact
decoded product bytes against exported native frame fixtures/hash manifest.

- [x] Add regression tests (bank/controller/presenter RED/GREEN; loop tests after seam):
      assets load and have correct geometry/frozen state;
      sit endpoints/reverse/finite validation, moving limb raster differs;
      rest1s then650ms sit, reverse smoothly when walk requested, no walking
      phase drift while stationary, interruption/airborne blocks motion;
      existing cheek/body capture accepts new native96 motion frames.
- [x] Implement exporter/loader using the real approved renderer and fixed
      baked bank, not independently redesigned art. Record format/count and
      write deterministic metadata for verification. Stand endpoint includes
      approved two-pixel fringe cleanup. Full65 original PNGs stay untouched.
- [x] Integrate after normal pose reset and before direct-presentation overrides;
      only Idle/Walk when not blocked/direct. New walk replaces old whole-body
      sine bounce. Keep active facing and native96 captures/cheek hit eligibility.
      Preserve blinking while seated. Keep motion state outside the giant presenter.
- [x] Add PetLoopHost optional callbacks to block locomotion when platform owns
      motion and hold autonomous walking while seated/rising. Core direct presses
      still run; do not suspend direct reaction or platform falling/landing.
- [x] Run focused tests then full App/Core suites and Release build. Self-review
      and separate spec+quality review required. No commit/push.

### Task 2: Validate and deploy approved build

- [x] Verify built DLL bank and render/capture paths match approved frames;
      inspect representative actual WPF frames, walking/sit/rise and interruption.
- [x] Publish to a new versioned directory under Downloads/doro; preserve current
      runtime-v2 unchanged for rollback. Resolve current PID/executable before
      graceful stop, start new executable hidden, check process/window alive,
      no startup exception. Do not stop unrelated processes or preview servers.
- [x] Record hashes, old/new executable paths and PID in this ledger and give
      concise user handoff. No commit/push unless separately requested.

Delivery 2026-09-10: final App Release697/697, Core Release237/237 (934 total),
Release build0 warnings/0 errors. Independent review approved the final sample
activation guard. Initial canonical/direct recovery stays exact; only nonzero
baked samples activate motion, while post-rise cleaned standing persists.
Actual WPF standing/walk/sit/rise/blink proof inspected. Native no-activate
window is not exposed by Computer Use, so no live mouse/visual claim is made.
Test/build/publish/loaded App SHA256:
`24932CF1F3B7A6FCD662E1289D6DAE436DAA30440A18DE7A49549C6E36A5DD4D`.
Core SHA256: `96A04EFF4075EF73743A394B7B3C7C1D45C25AC17DF1DF00FE21968BEE35C980`.
New executable: `C:/Users/tjdwo/Downloads/doro/locomotion-20260910/runtime/Dororong.App.exe`,
PID22444 responding and loading these DLLs. Verified old PID35788 had no graceful
main-window close; stopped only that process. Old `input-rump-fixed-20260909/runtime-v2`
folder unchanged as rollback (App SHA9B244901...E74E). No commit/push.

Preflight: Task1 produces native96 frozen frames/controller and consumes current
preview; Task2 consumes its published exe. No conflicting file ownership.
Task1 test geometry matches capture assumptions. Current preview remains available.

## Walk / sit comparison preview — approved 2026-09-09

## Authored 10-2 sitting transition — 2026-09-10

Follow-up: user reports unnatural rise and tail-like rump exterior after rising.
Reproduced missing interior feature motion: initial SDF only transported contour
depth while deep interior colors dissolved at fixed positions. New landmark
correspondence moves toes, clefts, fold and rump interior before contour sampling;
head stays pinned and final drawing unchanged. Feature-transport regression RED
(marker missing at intermediate location), then GREEN. Source canonical contains
faint outboard rump pixels at74,70 and74,71; cleanup excludes weak specks without
an adjacent visible boundary, retains connected antialias and original files.
Fringe regression RED(alpha26), corrected path GREEN. Changed exactly two
outboard pixels(74,70) alpha26 and(74,71) alpha30; all other source pixels retained.
User capture requested to confirm whether these are the exact reported tail.
Final batch: authored endpoint/head/white-body, moving-feature, fringe, walking
ink,65-frame bank parity and delivery tests GREEN. Frozen-map mutation fails
the moving-feature probe. Max adjacent bank change0.269/255. Reviewer checked
all44 triangles: minimum doubled area5 across analytic extrema, total area9216,
no gaps/folds; cleanup changes only the two pixels above. Browser midrise and
standing endpoint inspected on black/white, page reports normal playback.
Console contained old extension message-channel errors, no new renderer error.
Server40112 delivers regenerated bank. New ZIP authored-10-2-frames-v2.zip
retains the previous ZIP separately. Preview only; no product update or source
JPEG changes. Exact user-reported tail appearance remains unconfirmed by capture.
Final review also reproduced a129-level RGB pop at mapped interior texel-cell
boundary(t=.7097415384), independent of tail. Added failing real-renderer
regression, replaced rounded-cell boolean with bilinear depth and smooth
interior/contour blend(depth1..4); regression GREEN, final bank regenerated.
Narrow reviewer recheck confirms color-pop resolved. Final bank parity max step
0.267/255 and delivered-five-image verification GREEN; server43176 restarted,
browser reloaded and rise playing. v2 ZIP refreshed with final65 frames+metadata.

Goal: use the user's final10-2.jpg without pose redesign; generate reversible
standing/sitting frames and show in the existing preview. Product remains untouched.
Architecture: preserve a byte-identical JPEG copy; extract only border-connected
white background; register with integer translation(+3,-12), measured head RGB
error0.815/255. Build a separate authored transition module using the existing
contour-depth sampler, exact endpoint branches, and body feature correspondence.
Tech: existing Canvas2D/Node runtime, no new dependency or image generation.
Spec: latest user explicitly approved10-2.jpg as final and requested transitions.
Execution: one tightly coupled renderer task performed in this session; no
parallel implementation. No commit/push/product integration in this preview scope.

- [x] Add endpoint/white-body/registration tests in verify-authored-sit.cjs;
      demonstrate current renderer cannot reach the supplied endpoint.
- [x] Add authored-sit.js prepare/create APIs; source JPEG in assets is unchanged.
      Preserve opaque interior RGB, remove exterior only, register without scale.
- [x] Interpolate correspondence and contour depth, not raw image opacity;
      start/end return source rasters exactly; stand traverses the same bank back.
- [x] Wire optional authored endpoint into preview/raster harness and embedded
      delivery; leave walking path unchanged. Export65PNG frames and metadata.
- [x] Check endpoints, bounded frame differences, no missing body/ribbon, render
      atlas and direct browser playback. Show preview and saved frame directory.

Authored10-2 result:65 PNGs plus reverse-order animation.json exported under
artifacts/repro/locomotion/authored-10-2; immutable snapshots copied into a
2048x2304 bank (avoids mutable-canvas atlas aliasing). Fixed-coordinate head
color prevents contour transport smearing hair/face. Independent review found
scale2 exact-endpoint coverage discontinuity(max alpha62); regression RED then
GREEN after first/last6%-amount premultiplied coverage handoff. Reviewer recheck:
zero near-endpoint alpha/RGB difference, no further actionable finding.
Authored endpoint/interior tests,65-frame bank/export parity and reverse order,
walking joint ink(96.0%), and five-image embedded delivery all pass.
Maximum adjacent full-canvas premultiplied change0.268/255 at frame3.
Preview server40656 restarted; browser sit/stand, mid/final pose and black
background inspected; console errors empty. Supplied JPEG has noisy pale edge
pixels on black, retained rather than redesigning final drawing. No product
restart/integration, no commits/push. Historical procedural notes below are
superseded for sitting by the user's final authored pose.

2026-09-10 refinement: user approves belly connection but asks for deeper rear
fold and level front/rear feet. Raster measured soles81.75/87.25/83.75: source
front feet had never been aligned to the sleeping rear. Added seated-only
continuous lower-body vertical alignment to shared83.75, preserving the head
texture and all walk rendering. Crease compressed35% vertically around the
rear floor and tucked1.5px inward; accepted rump outline stays unchanged.
Preview settles the whole pose7outputpx to keep the aligned floor grounded.
New sole-level regression RED at old heights then GREEN at83.75/83.75/83.75.
Replaced superseded exact-forepaw-pixel preservation with shared-floor test;
head preservation and belly-join tests remain. Browser partial/fullsit inspected;
101-frame lower-body continuity GREEN(max pixel step0.767/255),1001-sample
topology and delivery GREEN. Preview only, no product updates/restart.

Latest narrow correction: rear convex volume is explicitly accepted; ONLY the
angular belly join should change. Root cause: sleeping rear still pinned to
standing belly endpoint(49.5,77.73), creating a deep notch beside foreleg.
Extended the source contour to the back of the planted forepaw(48.5,83.85),
and joined it to the unchanged sleeping rear with a tangent-aware shallow
curve. Cleared obsolete standing-root ink inside the new fill; paw sole stays
unchanged. Belly-notch/old-root-ink raster regression RED then GREEN. Raster,
sleeping-shape, walking-ink and1001-sample topology checks GREEN;101-frame
continuity GREEN(max rear pixel step0.733/255). Browser partial/fullsit inspected,
no residual vertical root stroke. Preview updated; awaiting visual approval.
No rear-volume reduction, no head/front-foot relocation, preview only.

Latest user correction: discard the invented seated haunch; directly take the
existing lying/sleeping rear and match stroke/join only. Implemented source
`dororong-sleep.png` alpha-boundary extraction and copied diagonal knee ink,
translated3px down to the forefoot floor. Removed hand-authored seated Beziers.
Only short hidden top/forebody joins are adjusted. Head/front paws/walking
unchanged, source assets unchanged, preview only. Sleeping-source silhouette
regression RED9.8% mismatch then GREEN (<4% tolerance at sampled rear pixels).
Topology1001 samples GREEN; knee copied ink reinforced and alpha-clipped to
prevent crop-box residue. All3 textures embedded in delivered HTML; delivery
RED missing sourceSleep then server40300 restart/GREEN. Browser endpoint and
source-pair raster compared. Continuity101 frames GREEN (<=2px contour steps,
worst premultiplied pixel step0.708/255); geometry/raster/ink GREEN. Comparison
artifact: artifacts/repro/locomotion/sleep-source-comparison.png. Delivered
preview only, awaiting user shape acceptance. Earlier invented-shape acceptance
wording is superseded.

Current revision after user rejected both seated endpoint and transition:
walking alpha-boundary-only ink reinforcement keeps tested moving joints at
96.0% of resting integrated ink; independent100-pose A/B review found no alpha,
upper-head or RGB-brightening changes. The earlier SDF seated shape and later
pure-mesh flat/pointy rear were rejected after direct playback, not accepted.
Rear redraw now uses corresponding silhouette landmarks for a round tucked paw,
fills underneath the preserved forebody/head (fixing a diagonal torso hole),
and keeps the ink width stable. A first12%-amount premultiplied ink handoff fixes
the entry raster-style pop (new alpha-entry regression RED0.407 then GREEN).
Reviewer caught a near-endpoint self-crossing paw join; controls corrected and
1001-sample topology regression GREEN (old-controls mutation fails at0.856).
Raster/geometry/ink/delivery checks GREEN; final101-frame continuity GREEN
(<=2px contour steps, worst rear premultiplied step0.527/255). Browser partial/
full sit, stand, mirror and black/white inspected; sequence restored for handoff.
Visual shape remains a preview for user approval. Original head/ribbon/
front feet retained. Generated sitting reference used for anatomy only because
its face changed and checkerboard was baked in; generated bitmap is not shipped.
Preview-only server42336/tab2, product PID35788 unchanged. No commit/push.

Previous iteration notes below are historical, superseded by the revision above.

Approved refinement: repair walking body/paw tears; sitting should borrow the
sleeping haunch/tucked hindleg shape but redraw its thin stroke to standing-art
weight. Preview only. Attachment-width regression RED proved independent cut
rotation separates roots; replaced with one continuous body texture/field.
Raster RED caught half-source-pixel start offset, fixed texel-center sampling.
Seated shape now a round redrawn rear with tucked crease, signed-silhouette
interpolation (no bitmap crossfade), existing head/front feet preserved.
Geometry/raster/delivery GREEN; 20 full-amplitude gait attachment checks,
sit-quarter head/forepaw preservation, fullsit shape/stroke and halftexel entry
regressions pass. Independent review performance finding fixed (zero-sit
distance shortcut, shared vertex map, bounded mesh/empty-SDF skips), rechecked
with no actionable issues. Host Canvas uncached samples improved from27–33ms
to9–19ms; browser both directions, black/white, partial/fullsit and sequence
rechecked with no console errors. Server42336, tab2 re-delivered; source assets
and desktop product unchanged. Await visual approval of redrawn seated shape.

Delivery repair: user screenshot showed Image.decode failure/blank canvases.
Live root PNG responses were200/exact source bytes; local browser reload worked,
so the specific failed delivery origin remains unconfirmed. Removed dynamic
root-relative image fetch dependency: server embeds both exact PNGs in HTML,
preview decodes those DOM images. verify-delivery RED for missing embedded
textures then GREEN, geometry/syntax/diff checks pass. Reloaded browser confirms
both96x96 decoded, both canvases visible and no console errors. Preview server
replaced34300 with17344; product untouched. Re-delivered tab2 for user check.

User approved a separate walk → stop → sit → stand comparison preview. Scope:
existing canonical artwork articulated into alternating forelegs/hindleg,
distance-driven foot cadence and modest rigid head/body follow; sitting bends
hindleg and lowers rump while forepaws stay planted. Preview only, no product
integration, restart, new source art, tray/startup/settings mutation or push.
Stable product remains normal PID35788 at checkpoint212eb1a. Geometry contact,
transition continuity and valid raster tests first; inspect actual browser
motion and both directions, then hand over for visual approval. Tray work waits
for the selected sitting pose. Anatomical hidden closures are explicit preview
assumptions, not newly authored source images.

Preview handoff: `tools/PreviewLocomotion/`, loopback http://127.0.0.1:2785/
(server34300), browser tab2. Existing open/closed-eye textures, separate limbs,
planted front paws during sit, lowered/folded rear, speed/mirror/black-background
controls and an 8-second walk-stop-sit-stand loop. Startup discontinuity reproduced
RED at actual timeline t=8, then fixed with a 0.4s ramp and integrated travel;
contact/sit/gait/timeline regressions GREEN. JS syntax and diff checks pass.
Independent review findings fixed and rechecked; browser scrub/mode highlight,
both facings and loop restart checked, console has no errors. Rear fold/root
closures remain provisional art for visual selection. Product source/executable
unchanged; no commit/push or tray integration. Await user's motion feedback.

## Stable executable checkpoint / tray planning — approved 2026-09-09

Checkpoint gate passed: fresh App687/Core237 tests, generator contour and
113-frame parity/continuity/head-preservation checks, and diff check. Normal
PID35788 is responding and loaded App/Core module paths match runtime-v2.

User's pause preference is a new sitting pose: hindlegs bent, front legs
standing. Treat as the next bounded animation design using existing articulated
body rendering; propose front-paw contact preservation and rump lowering,
with blink/manual interaction retained. Confirm entry/exit behavior before
implementation. Tray integration follows the selected sitting motion.

User approved normal-executable handoff and committing the accepted current
version before tray controls. Published App hash rechecked against accepted
9B244901...E74E; exact diagnostic host 23868 stopped, normal runtime-v2 EXE
35788 launched. Preserve all old runtime directories. Checkpoint includes prior
accepted perch regrab/alternating forelegs, ribbon protection, continuous
113-frame head pull, contour fixes and Task Manager/rump corrections. No push
requested. Fresh App/Core and generator verification precede commit.

Tray/settings are a new control subsystem (architectural brainstorming), not
an existing settings flow. Proposed scope: native tray menu, pause/resume,
size, opt-in startup and exit, with persistent user settings. Asked whether
pause should stop only autonomous travel (recommended, preserving blink,
manual interaction and physics) or freeze all behavior. No tray code or
startup registration changes until design approval. Installer/updater and
broader endurance testing remain subsequent work, not implicitly included.

## Task Manager input / rump raster fix — approved 2026-09-09

Live acceptance complete: user says second input candidate works. input-fixed-v2.log 12:18:39Z records incoming WM_LBUTTONDOWN async0/fg28272 -> routed-down async-32768/fg23868, then held capture and release at12:18:42Z. Rump previously accepted. Next handoff is stopping diagnostic host23868 and launching identical normal runtime-v2 executable, then release/checkpoint planning; no restart or commit performed in this status-only turn.

Final automated verification: App687/Core237 = 924 passed, ExactArt passed, tested/build/published App hashes match 9B244901...E74E. Diagnostic host 23868 is responding, awaiting the user's second-candidate Task Manager drag; no native success claimed yet. Its input-fixed-v2.log currently contains ready only. Normal executable handoff remains after live confirmation; user-approved rump correction is retained. No commit/push.

Live diagnosis confirmed WM_LBUTTONDOWN/routed down arriving with async=0 under Task Manager PID 28272; ordinary foreground PID 2696 gives -32768 and capture. First candidate's MA_ACTIVATE response did not override WS_EX_NOACTIVATE in practice (input-fixed.log); user confirmed failure, so it was replaced rather than claimed complete. Current candidate requests SetForegroundWindow only for the pet's own HWND on a delivered left-down with async unavailable, preserving normal no-activate and forwarding that click. Explicit-request regression RED1 then GREEN; related 56 pass. Rump approved by user. Final candidate published to input-rump-fixed-20260909/runtime-v2, App 9B2449014E00165E23C763EEA051320B557EF2BFAB9EE20646CFDDE3A113E74E; diagnostic PID 23868 logs input-fixed-v2.log. Await final App run and live test before replacing diagnostic host with ordinary executable. Core237 and ExactArt pass. Independent reviewer hit usage limit after preliminary raster checks; review not falsely counted as complete. See docs/verification/2026-09-09-input-rump-fix.md.

User approved both fixes. Preserve interaction physics, head/ribbon, source PNGs and previous runtime; no elevation or security-policy changes. Rump/belly RenderFlow regression RED: complementary strokes sum 0 instead of 255 (dark dilation), real-art 0.01px pull step changes a channel by 150. Center-correct premultiplied bilinear sampling removes darkest-neighbor bias; focused 38 tests pass and before/after published-code atlases reviewed. Independent read-only raster review and full suite pending. Original process 40884 replaced by diagnostic PowerShell host 34744 loading the same published App/Core DLLs, with window-message/routed input/async button/capture logging only. Await user reproduction to prove the exact Task Manager input boundary before fixing it. Final release requires both regression checks, review and verified runtime identity; no commit/push requested.

## Task Manager input / rump raster feedback — diagnosis 2026-09-09

User confirmed that dragging works after clicking another window while Task Manager remains open. The failure is foreground-dependent, not caused by Task Manager merely existing. Medium/high integrity mismatch and the no-activate/global polling path remain the leading explanation; an actual button/capture trace is still needed to identify the precise failing API. No product change made during diagnosis.

User reports drag unavailable whileTaskManager open and malformedrump/rightpaw screenshot40ff2ac8. Read-only published40884 DLL render in artifacts/repro/rump-taskmgr-20260909/rump-primary.png reproduces jagged/thickenedrump/rightpaw strokes withSurroundingPullRenderer followzero, soprimaryBodyPullRenderer.RenderFlow is sufficient; flow samples integertexels andselects darkestof5neighbor taps, overlayNearestNeighbor. This is separatefromnew113frameheadpullbank, not proofthenewbankcausedit. Tokensqueriedreadonly: Dororong40884mediumRID8192,Taskmgr28272highRID12288. AppMA_NOACTIVATE + GetAsyncKeyState-onlybuttonpolling means foregroundpermissionfailurecanbeinterpretedasbutton-up; Microsoftdocsconfirm0canmeanfailure, but actualinputfalse/capturetrace notyetmeasured. AskedwhetherotherwindowforegroundwithTaskManagerstillopenrestoresdrag. Product/code/runtimeunchangedthisdiagnosticturn; noadministratorlaunch/securitysettingschanges. Inputtriggerconfirmationthenboundedfixdesign/regressionsneeded.

## Contour-preserving body transition — approved 2026-09-09

User rejects remaining intermediate body/foot ghosting and approves separating body/rear-foot ownership, continuous single-contour movement and explicit occlusion, preserving authored keys/head/ribbon. Scope is existing pull renderer and its production bank/release path, not new app features or distribution services. Investigate each interval, RED contour/opacity/occlusion regression, repair generation, inspect forward/reverse atlases, regenerate production bank and parity tests, full regression/review/publish/live trial. Keep current21044 and old trial folder until verified replacement. No commit/push requested.

Delivered 2026-09-09 20:02KST: contour.js signed silhouette correspondence + bounded/backtracked source-color transport; body and both forearms use single-contour sampling. Separate far-hindpaw hip/tip layer, depth rear/trunk/fararm/neararm/head. RED ghostsalpha128,5pxforearmsoftband,missingrearownership and revieweruniformink163escape nowGREEN.113unique/Pbgra/bankparity/reversal,8exactkeys/bothsidelimits,113frozenheadhashes/rearmarker pass. Actualpresenter hashes updated from reviewedcandidate; zero-time release expanded toall7intervals/bothfacings. App676/Core237,ExactArt,diffcheckpass. Independent review P2lookupescape fixed/reverified, noremainingblockingfindings; atlas25/41/57 singlepaw/noobviousduplicates. Browserblack41 comparison andmirroredautoplaychecked. PublishedApp1D44E24ACA15979C290315E59F3941E40AC6B62F688E94544D8E53035EB602EA/CoreC6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4 matches testedbuild. Exactold21044/pathstopped; new40884 respondingwithverifiedmodules in `C:/Users/tjdwo/Downloads/doro/contour-pull-20260909/runtime`. Previousruntimesretained; sourcePNGs/inputphysicsunchanged. No commit/push. Report docs/verification/2026-09-09-contour-pull.md. Nativefeel awaitsuserfeedback; this is notavectoredartreplacement orclaimthatallfuturevisualissuesareimpossible.

## Layered desktop trial — 2026-09-09

User requests running the selected layered preview in the desktop product. Scope: package the exact 113 approved comparison rasters, connect entry and release without reverting intermediate poses to the eight-key path, preserve source art and all other interactions. Preserve old published runtime; publish a separate trial folder and replace only its live process after tests. Known hindfoot/body ghosting remains disclosed. Plan: presenter parity RED (5 failures, exact key passes), embedded bank + continuous release bridge, full App/Core regressions and review, verified publish identity and launch. No commit/push requested.

Delivered desktop trial: LayeredPullFrames embedded 113-frame bank, dense subframe interpolation and continuous head registration; CreateRecovery retraces same bank then existing pose01/canonical bridge. Intermediate upright sampling matches rest to prevent zero-time button-up filter switch (existing regression RED2, fixed). Anchored pose41->25 literal browser hash passes and fails when presenter is mutated back to old recovery. Full Release App662/Core237, ExactArt and diff checks pass. Independent scoped review no actionable defects; all113 regenerated buffers match embedded bank, uncompressed SHA199C267EBB98CC1832F31C85563B7F643B300316DF53F1DFFE098C0ABC5EB37A. Published build identity verified App5C11D18FA1D7EC641CBE608767C2C59B10237710CBB6B7FDFA46094F9FADCC51 / CoreC6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4. Exact old34828 stopped, new21044 launched from `C:/Users/tjdwo/Downloads/doro/layered-pull-trial-20260909/runtime`; old ribbon-preserved folder retained. Original source art untouched; no commit/push. Live interaction feel remains user trial, not proven by process liveness. Known tinyhindfoot/body ghosting remains.

## Perch live correction — investigation, 2026-09-09

Layered comparison revision3,2026-09-09: user approves fullhead/hair/ribbon isolation and independently articulatedforelegs, previewonly. Added layered.js andinbetweens-layered.json, retainedv1/v2banks/options; frameinputsupports35..49. REDframe41pink238/227/255 inbody fixed; REDnear-keycompositionmax188fixedbyclosingonlybehindopaqueoriginalforeground; all8exact/near<=1byte. Independentreview identified31literalstrandedforelegink and5stolenhindfoot locations; scopedpolygon/rowownership fixes pass plusbothforelegcoloredmarkerpaths,nohairbody/arms,113savedbank/Pbgra/reversibility. Head usesexistingv2detail/cubic isolatedtexture viaheadOnly rigidmapping (defaultv2unchanged); bodyretainsexistingmesh. Runtime34828/path/responding checkedunchanged; no sourceart/productcode/restarts/commits. Reviewer approvescomparisononly, notcompletevisualacceptance: smallhindpaw/bodyghost remains25/41/57. UI explicitlydisclosesremaininglimitation. Finalbrowserreload confirms41/113 at35.7% onblack withlayereddefault; independentheadOnly mesh-perturbation check unchangedatsevenmidpoints andall113Pbgra/v2paritypass. Final layered/refined/legacy tests, previewsyntax anddiffcheckpass. Next usercomparison; rear-bodyocclusion improvement remains separate followup, no productpromotion.

Refined-preview user rejection / root diagnosis,2026-09-09: user reports hair appearing from below aroundframes35..late40s and awkward foreleg transitions throughout. Current113-frame bank audited at35..49 (`transition-35-49-audit.png`, diagnostic-only `audit-transition.ps1`). Exactframe41 is source03↔04 midpoint: pixel(49,61) samples source03(50.4,66.59) white body and source04(47.6,55.41) pinkhair through the body mesh. `audit-provenance.cjs` proves same bad ownership in v1 and v2; cubic accentuates detached pink patch, while detailgrid has already fadedout at headcentroidY+24. Thus a horizontal centroid gate is not actual head/hair ownership. Foreleg sweep shows two fading contours because shared-body texture blending has no separate limb visibility/occlusion layer despite coarse landmarks. More frames or sharper filtering cannot fix this. Proposed next scope: explicit fullhead/hair/ribbon masks isolated frombodywarp plus independent foreleg articulation/depth in comparison-only path; requires revised design choice, no implementation/restart this diagnostic turn. Product34828 ribbonfix andpreviewbank remain unchanged.

Ribbon fix / refined comparison delivered,2026-09-09: user approved bounded proposal. Product change only SuppliedBodyDragFrames source08 interior ownership8texels, preserving originalRGB/noassetedit. RED8literal presenter entry/hold samples alltransparent;GREEN19related, fullApp656/Core237,ExactArt and13approvedassets pass. Independent read-only review nofindings. Tested/publishedAppSHA866660B768AF3BF900B39D33E326629B35D31242DCB9D3052CA26E56595BB7F3 andCoreC6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4 match; exactold38220 stopped, new34828 responding with verifiedloadedmodules at `C:/Users/tjdwo/Downloads/doro/ribbon-preserved-20260909/runtime`. Old runtime retained. Separately preview2784 nowdefaults113precomputedrasters(8exact+105intermediates),4px head-detail registration within2px,cubicPbgra reconstruction, originalbodymesh retained. Still registeredtwo-textureblend/notnewart; someintermediatebodysoftness remains. Frozenv1 selectable, blackbackground toggle, source08ribbon fixedbothsides. REDfine-registration error156487;GREEN0oncontrolledtranslatedtexture,113unique/exactkeys/Pbgra/nonfiniteclamp/browserbankparity;v1C#11referenceparityretained. Independentpreviewreview no blockers, requestedbankparitytestadded/passes. Browser checked50%,100%ribbonblack,selector,mirror,40pxdrag50→70%;tabdeliverable retained. Sourceassets/otherpendingchanges untouched; no commit/push. User visualselection and actualdesktopconfirmation next; newmotionnotportedtoproduct.

Continuous-pull refinement / product ribbon diagnosis,2026-09-09: user requests finer sharper inbetweens and confirms ribbon middle disappears on desktop pose08 against black. Read-only published-DLL export reproduces missing white middle loop before interpolation: raw08 versus prepared08 atlas `artifacts/repro/continuous-pull-preview-20260909/ribbon-source-prepared-audit.png`. SuppliedBodyDragFrames.Prepare edge-connected RGB>=240 flood crosses the open light outline into ribbon interior; RemoveExteriorWhiteMatte does not erase white pixels, so loss precedes its pass. Preview registered two-texture interpolation also softens detail independently. Only diagnostic exporter/poses/atlases updated; product code/assets/live runtime unchanged. Bounded design approval next: preserve authored ribbon interior during cutout (not fill an arbitrary rectangle), regression against literal08 middle-loop pixels + all8 key silhouettes; refine anatomical feature registration / reduce mixed edges and materialize denser intermediate frames in separate comparison before production promotion. Existing pending regrab/flutter code retained, no commits or restarts.

Continuous pull comparison experiment,2026-09-09: user approves separate8-key versus continuous preview, not product integration. Isolated artifact `artifacts/repro/continuous-pull-preview-20260909` exports prepared original01..08 and existing anatomical correspondence from read-only published App DLL, browser evaluates arbitrary drag progress with registered neighboring texture interpolation. Product source/assets/runtime untouched. RED baseline only8unique samples; GREEN141/141unique,8exact keys,11C#reference frames within1byte rounding, premultiplied alpha and return-to-position identity. Browser UI verified slider50%, actual70pxup drag50→85%, mirror and auto-play/pause; narrow sidebar shows both canvases. Served localhost2784 by Python40740, deliverable tab retained. Runtime38220 unchanged/responding; AppSHA346A7377ABA2323941949161B9CF5DA2C6EFE55DC235E01AC571EB90A575247C. Known preview limitation: aligned texture blending/subpixel sampling softens intermediate contours; this is not yet a single-head/art-rig production replacement. User comparison/selection next, no commit/push.

Alternating forelegs delivered for user trial: second paw125ms behind first with same4Hz52±13degree motion; masks/contour/pin/head/body/eligibility unchanged. Fresh App648/Core237 and4PS gates (including219351render assertions,13approved assets) pass; independent review Critical0/Important0/Minor0.32embedded resources unchanged and actual test-output/published App/Core DLL parity verified. Exact old34684/path stopped; normal38220 started from `C:/Users/tjdwo/Downloads/doro/alternating-forelegs-20260909/runtime`. App346A7377ABA2323941949161B9CF5DA2C6EFE55DC235E01AC571EB90A575247C/CoreC6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4. Earlier regrab08 work/runtime retained; no new commits/push. Live user acceptance pending; evidence artifacts/repro/alternating-forelegs-20260909.

Alternating forelegs approved,2026-09-09: user asks separate paw flutter and approves proposed offset beat with fast motion and fixed head/torso. Bounded change to existing renderer only: second paw half a250ms cycle behind first, retaining52±13degrees, cutout/outline/pin/eligibility. TDD real green/blue interior markers prove existing second paw rises with first (RED2/2fail at96and160width); opposite travel required. Existing uncommitted perch-regrab08 work preserved. Evidence artifacts/repro/alternating-forelegs-20260909; no commits or source art/native changes. Runtime34684 remains until verification/review.

Perch regrab08 delivered for user trial: attached non-cheek pending uses exact supplied08, normal deadzone then fullhold; original pointer anchor/facing retained. Regression caught and removed pending-click sole correction16DIP displacement for this path only. App646/Core237, RuntimeComposition,219351render assertions, ExactArt,13approved assets and diffcheck pass; independent reviewer Critical0/Important0/Minor0. Actual test-output/published DLL parity and32embedded resource identity pass. Exact40880/path replaced by normal34684 at `C:/Users/tjdwo/Downloads/doro/perch-regrab-eight-20260909/runtime`; prior runtime retained. App171499D9E863E6BF2F67A5EB4B6F1227FE00DA843BBC3ED2D9FD424E2D06EC6B/CoreC6DD972BD263002A92F5C936C87B344278D229354FE88F7E8D5EF4F1F9B2BDD4. No asset/Core source/native changes or new commit/push. Live user acceptance pending.

Perch regrab08 request,2026-09-09: user requests already-perched dragging to start from supplied08 rather than01. Scoped implementation: capture attached non-cheek press origin context, retain08 during pending and enter full hold after existing drag threshold; preserve facing/grab anchoring, local-only attached cheeks, ordinary ground extension and release recovery. Tests first reproduce pending wrong bitmap and short-drag BodyDragEntry in both facings (4fail/4pass). Evidence artifacts/repro/perch-regrab-eight-20260909. Existing accepted runtime40880 and commit6e59727 retained until verification; no new commit/push requested.

Accepted checkpoint,2026-09-09: user says `좋다 지금 버전으로 커밋 푸시 먼저`, accepting current outline/foreleg flutter trial and explicitly authorizing a current-version commit and push. Checkpoint includes accumulated window/taskbar platforms, landing/recovery fixes, perching, facing/blink/local cheek interactions and readiness foreleg contours, with source/tests/specs/verification tool. Target existing feature/dororong-m1-expression-animation branch and its origin upstream; no merge/PR, runtime restart, artifact upload or worktree cleanup. Fresh precommit suites required; prior no-commit constraints are superseded only for this requested checkpoint.

Checkpoint verification: fresh precommit App641/641 and Core237/237 pass; approved13assets, accepted frozen contour source parity and staged whitespace check pass.88scoped files staged, including user-authored09asset; ignored binaries/diagnostics excluded. origin fetched and HEAD/upstream matched before commit. Commit/push delivery result is verified against remote branch after execution.

Outline correction delivered for user trial: retained250ms flutter/angles/body/facing/pin/native/eligibility, corrected detached donor ink, static torso versus fingertip AA ownership, and blank row52 chin gap. Independent scoped review Critical0/Important0/Minor0. Fresh App641/Core237 plus RuntimeComposition,219351render assertions, ExactArt,13approved assets and diffcheck passed. Frozen-fringe-fix source parity and actual test-output/published DLL parity passed;32embedded resources unchanged. App5572FB62FAF1198F5D4080F6F057EC8B4E5C8B098F97238556B444D4B94EE5DD/Core49332B8DB9BAEE03824D0196F869AE4FF3562BAEA5F090AD3DDB1AFC85E6BD22. Exact old36060/path stopped; new normal40880 launched from `C:/Users/tjdwo/Downloads/doro/perch-flutter-outline-20260909/runtime`. Previous runtimes/evidence/source art retained, no commit/stage/push. Evidence and review under artifacts/repro/perch-flutter-outline-20260909. Live user visual acceptance remains pending.

Outline correction approved (`ㄱㄱ`): bounded readiness contour correction active. Before snapshot artifacts/repro/perch-flutter-outline-20260909/before; tests added for chin-gap and stranded interior ink. Source inspection identifies straight row70 donor torso ink transplanted into upper arm holes, plus blanket y53/55 head restoration and cutout borders. Keep250ms clock/angles/pin/body/native/eligibility; no source art edits or commits. Existing runtime36060 preserved until review and verification.

Flutter outline feedback: user accepts motion but reports residual fingertip strokes and arm-line termination a few pixels below/outside chin. Read-only source/proof inspection confirms rigid cutout+hard y53/55 foreground restoration can truncate rotated shoulder ink; current proof also shows small residual interior marks. A source/mask comparison found retained ink at37,68 and38,68, but these coincide with torso join, not sufficient alone to classify fingertip residual cause. Proposed bounded correction is precise limb/AA ownership plus shoulder contour continuation hidden only by actual head silhouette, without speed/pose/body/native changes. Design approval pending; no product/test/runtime edits in this feedback turn.

Isolated flutter delivered for user trial,2026-09-09 16:52:27KST: only4source/test changes (independent foreleg renderer + presentation +2tests), no Core/native/eligibility/assets changes. Fresh App630/Core237,4PS gates incl219351render assertions passed; independent review Critical0/Important0/Minor0.32embedded resources unchanged; tested-output/published AppE0D61F1F0A0D1289EF44C1BF10C658A357B6F3CAE53E290865683106CE3B283B/Core49332B8DB9BAEE03824D0196F869AE4FF3562BAEA5F090AD3DDB1AFC85E6BD22 parity. Exact32024/path stopped, normal36060 launched from `C:/Users/tjdwo/Downloads/doro/perch-foreleg-flutter-20260909/runtime`, responding and loaded modules verified. Source audit unchanged after freeze. Prior runtime/evidence retained, no commit/push/stage/art change. Renderer borrows stationary hidden-body texture only within vacated masks; original image never warped as a whole. User live acceptance remains next; evidence artifacts/repro/perch-foreleg-flutter-20260909/REPORT.md.

Flutter revision approved (`ㄱㄱㄱ`), bounded implementation active. Parent owns only readiness rendering/tests; head/body transform, eligibility, native taskbar guard stay unchanged. TDD observed old wave altering the former rectangular region and failing4Hz repetition. New evidence root artifacts/repro/perch-foreleg-flutter-20260909, full source/test before snapshot preserved. Runtime32024 remains until verified/reviewed replacement. No source-art edits, commits or push.

User-trial feedback,2026-09-09: current cue is cute but deforms torso along with forelegs (screenshot212cd7f5). Read-only code confirms rectangular wave field has no anatomical foreleg mask; prior tests protect outside rectangle, not torso within it. User wants independent arms extended forward with faster flutter. Bounded revision proposed: static torso/face plus isolated original-art forelegs, shoulder-rooted forward reach and roughly4Hz flutter, preserve facing/pin/eligibility and native guard. Brainstorming short-design approval pending; no implementation, test or running product changes in this feedback turn.

Delivery complete for user trial,2026-09-09 16:16:53KST: gentle held-eligibility forepaw wave + bounded active-perch taskbar-front ordering. Final frozen16-file delta independently reviewed Critical0/Important0/Minor0, parent fresh App624/Core237 and all4PS gates pass (DirectInteraction219351assertions). No asset edits/deletions;32embedded resources unchanged. New normal runtime `C:/Users/tjdwo/Downloads/doro/perch-readiness-wave-20260909/runtime`, AppF8CC4EB3B7E6B8F12C483C25F90A3827CD6237CCCE48F52F36A29E05A61E53A6/Core49332B8DB9BAEE03824D0196F869AE4FF3562BAEA5F090AD3DDB1AFC85E6BD22 matches test-output DLLs. Exact prior27724/path verified/stopped; new32024 responding, loaded module paths verified. Old runtime retained. Frozen code identity checked after launch, no commit/stage/push. Both-facing readiness and actual taskbar paw visibility need user live acceptance; native controlled tests are not that acceptance. Evidence artifacts/repro/perch-readiness-wave-20260909/REPORT.md and final-review-verdict.md. No further work in this delivery absent user feedback.

Readiness implementation milestone: frozen-task-1, fresh serial App619/619 and Core237/237 pass; full-app initial PackagePart failures retained, test-only collection serialization addresses concurrent WPF resource loading. User confirmed occluder is taskbar. Independent native metadata at16:06:20 shows taskbar Z33 above own pet Z34, both topmost and overlapping; this establishes actual native order inversion, not clipping. Bounded native guard now assigned to same diagnosis agent: only active perch, conditionally reassert own HWND above overlapping taskbar, no move/resize/focus or unrelated-window mutation, controlled transparent test windows only. Final combined review/full gates/publish pending; old27724 remains active.

User approved specific cue: forelegs gently wave while held at an eligible edge. Implementation now active, parent owns readiness feature; /root/perch_occlusion_diagnosis independently investigates native hiding read-only. Cue must not snap/move body or widen band, stops outside range/release, both facings; use same nonmutating eligibility selector as release. New tests before behavior edits. Before snapshot artifacts/repro/perch-readiness-wave-20260909/before. Existing runtime remains27724 until reviewed candidate. No art edits/commits/staging/push; previous per-plan evidence preserved.

User reports attached front paws/body hidden behind edge in screenshot88dd6fdd, and clarifies requested motion is a PRE-RELEASE eligibility cue, not post-attachment contact bounce. Previous offscreen/review delivery is not live acceptance. This turn source/runtime unchanged. Read-only trace: attached EdgePerchPresentation explicitly sets Image.Clip=null and keeps complete source09; MainWindow.Topmost=true; WindowStyleManager preserves z-order; release-only TryBegin gate provides no held eligibility preview. Screenshot alone does not establish native overlap versus host clipping cause; next fix must establish that boundary, not shift coordinates blindly. Proposed behavior awaiting user confirmation: visible ready-to-perch gesture while held at an actually eligible edge; immediately retire outside it; no snap until release; identical eligibility and preserved facing/eyes/local cheek. Also restore visible forepaws in front of the supporting edge without moving unrelated windows or stealing focus. Keep original attachment range unchanged.

## Perch expressions — implementation, 2026-09-09

Plan: `docs/superpowers/plans/2026-09-09-perch-expressions.md`; binding spec linked there. Task1 in progress with `/root/perch_implementation`; Task2 evidence/review/normal runtime handoff pending. BASE `87e7bb20aee925abcee135703bde46482d136f49`; no commits authorized. Before snapshot `artifacts/repro/perch-expressions-20260909/before`; baseline App588/588 passed. Existing worktree verified. This section is the sole progress ledger; per-plan briefs/reports live in `.superpowers/sdd/2026-09-09-perch-expressions`.

| Preflight | Producer / consumer or internal agreement | Result |
|---|---|---|
| Task1 self | Visible press-facing and local attachment data feed presenter/controller/loop; test composed visuals and real ownership | Consistent; integrated owner avoids concurrent source edits |
| Task2 self | Frozen source/test evidence consumed by review and new normal runtime | Consistent; no production changes except worker fix loop |
| Task1 / Task2 | Frozen files and test/proof evidence feed preservation, review, publish identity | Consistent; worker freezes before review, parent runs gates after full tests |

Scope locked: keep0..20DIP attachment band,160ms physical entry,500ms expiry, all old art/ground interactions/low-head landing. Add pinned contact motion, existing-eye blink, facing continuity, local-only attached cheek. Preserve old runtime; no cleanup/staging/commit/push. Current diagnostic process will only be replaced by a verified normal candidate at handoff.

Task1 complete: frozen-task-1 nine intended files, HEAD unchanged/no commits, independent Astra-high review Approved with Critical0/Important0. Focused137, isolated full App610, Core233 and four parent PS gates passed. Source assets/Core/platform eligibility unchanged. Task1 minor(deferred): real facing/expanded lifecycle tests first ran GREEN rather than requested observed RED; initial5 assertion-failure RED remains valid for presentation/classification. Task1 minor(deferred): first full App609/1 PackagePart exception remains unexplained; isolated610/0 retry retained without claiming root cause. Evidence/report/review paths under per-plan workspace and artifacts. Task2 final integration review and new normal runtime handoff pending; actual native trial not claimed.

Task2 complete: final Astra-high integration review Ready for normal user-trial delivery Yes, Critical0/Important0, same2 nonblocking minors retained. New normal runtime `C:/Users/tjdwo/Downloads/doro/perch-expressions-20260909/runtime` published from tested files; App FBD5E6AC0F550282363F4EC981FCF5A8AC696F3DB2CAA2F944CD384673661812/Core8BDCD85E0B01B0D77C266A07A7517DA11CF74B4B557F2865B35E0876F2ADADC6;32 embedded resources unchanged. Exact diagnostic12848 verified then stopped; normal27724 started, responding and loaded-module paths verified. Previous runtime/evidence/worktree preserved, no staging/commits/push/cleanup. User physical-pointer trial remains next acceptance boundary; normal process liveness is not native visual acceptance. Detailed evidence: artifacts/repro/perch-expressions-20260909/REPORT.md.

## Window and taskbar platforms — design, 2026-09-07

- Perch live trace comparison,2026-09-09: user reports failure then success during same unchanged diagnostic12848. Saved reproduction-snapshot.jsonl. First recorded success278987ms Entering→279161ms Attached, viewport953→946 / grip94 / taskbar1040: release approximately7DIPbelowedge, whole-carry preceding phase BodyDragHold. Earlier full-hold releases place neutral-grip4–14DIPabove1040 and reject under strict0..20belowband; other releases at+4/+6occur in BodyDragEntry with wholecarryfalse and reject. Cached/Fresh scene contains validtaskbar inbothcases. This supports strict activation-band/carrygate usability as cause of these attempts, not missingimage orunloadedfeature. No production logic edits/restart; diagnosticstillrunning, normal restoration/user preference pending. Other runtime behaviors not universallyvalidated.

- Perch Computer Use diagnostic,2026-09-09: user approves targetable temporaryhost with same product. Artifact perch-cua-20260909 references exact published App170C84FE/Core8BDCD85E; excludes MainWindow noactivate/toolwindow SourceInitialized hook, enables ShowInTaskbar/ShowActivated only, product DLLs unmodified. Initial helper AmbiguousMatchException corrected with DeclaredOnly. Exactnormal4432 stopped; diagnostic12848 launched by officialsky, returnedtargetid1248252. Capture twicefails SetIsBorderRequired E_NOINTERFACE0x80004002; accessibility-only exposes Presenter/DororongImage but no validateddragcoordinates, so no input issued. Bounded10min/16MiB metadata trace running in artifact bin/Release/net8.0-windows/trace.jsonl; no releaseobserved yet. Need userrelease reproduction or functioning capture; no actualdrag/fixclaim. Restore normal exactruntime afterdiagnosis; oldruntime preserved.

- Image09 live-trial failure,2026-09-09: user reports no perching after latest normal runtime restarted as PID4432. Requested Computer Use; refreshed bundled skill26.901.51231 and official sky.list_windows/list_apps return no Dororong target despite responding exact edge-perch09 process. No guessed HWND, unrelated-window input, or physical drag performed. Read-only release/contact path inspection does not establish actual failure cause. Automated821pass is not native acceptance. Product/source behavior unchanged; a targetable diagnostic host or actual release-state trace is required before claiming reproduction/fix.

- Image09 edge-perch product trial deployed,2026-09-08 23:38:41KST: exact whole9asset integrated; existing29embeddedPNG/Core preserved. Task4 regrab findings corrected through2RED/GREEN rounds and independently approved; finalwholefeature review noCritical/Important. FinalApp588/Core233pass,4PS1gatespass (finalDirect219351/RuntimeCompositionrerun),diffcheckpass. Tested/published App170C84FE830759ABBDA6370AFE4DEB1964801BAED41844D8170DCE0F1B5C337D/Core8BDCD85E parity verified. Exactold65304/path stopped; newnormal44988 responding with modules from Downloads/doro/edge-perch09-20260908/runtime; oldruntime retained. Samewindow wholecarryrelease band0..20belowtop,160msentry, wholeunclipped9withforepaws, onepositionowner, ownerfollow/detach and regrab implemented. RealWPFproof + controlledscene/input tests, not actualdesktopdragacceptance; usertrial next. Separate resume/hover recovery unchanged/outofscope. Evidence edge-perch-product-20260908/REPORT.md; no commit/push/cleanup.

- Edge-perch Task4 review boundary,2026-09-08: V2 fullApp580/Core233 and four PS1 gates pass;29old embedded assets/Core preserved, exact9only addition. Independent review finds2Important regrab gaps: hidden-canonical anchor capture while perched, and natural-exit wipe/hit selection survives phaseNone regrab. Same worker fixing with RED/GREEN; source/art scope unchanged, actual product65304 not replaced. Task4 approval/Task5 final review/deployment pending; frozen V2 and review report retained.

- Edge-perch Task4 presentation checkpoint,2026-09-08: initial real-loop RED2 confirms no attached owner/exact asset absent. Parent preservation check existing52asset files unchanged,new09exact. First presentation release fade was rejected from real-WPF proof for visible opacity dip; parent read-only source registration found09(-8,+6) aligns head mask IoU.99858/1362of1366opaque pink pixels exact. Worker replaces fade with registered opaque complementary reveal; latest native/4x proof at edge-perch-product-20260908/render-proof inspected bothfacings and0/30/60/90/120ms: opaque head, no double exposure, gradual body restoration. Approved PNG itself unchanged; initial placement changed to registered coordinates. Runtime integration/tests still in progress; no product replacement or user visual PASS claim.

- Edge-perch Task4 preflight,2026-09-08: fresh App553/Core233 pass; exact prior source/tests snapshot complete. Source9 hash3CE4BE5308759D35BA828237208521A085378180AAD035CA0474DA3E551F57C2,100x100 verified, main body cutoff64/forepaws76; low-alpha original pixels through88 preserved, not cleaned. Worker `/root/perch09_integration` owns implementation/builds under updated Task4 image09 brief. Parent owns validation/review/deployment; accepted runtime65304 still unchanged. Existing worktree isolation confirmed; no second worktree/commit.

- Edge-perch image09 approved / Task4 resumed,2026-09-08: user supplies9.png then explicitly approves using it whole. Ruling: supersede all composited Task3 candidates with exact100x100 image09, no source reshaping; source grip64/paw interval28..58 initial registration reflects its body cutoff/forepaws, to verify in real presenter. Ruling: current request resumes intentional perch only, not independent resume-repair/hover changes; cost is those separate issues remain unresolved. Existing pure Task1/2 stay complete; art gate now satisfied. Task4 runtime/presentation + Task5 validation/trial pending. SDD Task4 brief records current constraints; before src/tests snapshot under edge-perch-product-20260908, existing linked worktree verified. Plan interface check: Task1 contact state compatible with new100px source after affine mapping; Task3 old96px crop/Grip77 obsolete; Task4 position ownership overlaps accepted partial rebase and must preserve it; Task5 old asset count becomes29preserved+1new. No commit/push or old runtime overwrite.

- Edge-perch visual correction,2026-09-08: user rejects isolated forepaw pose and requires entire upper body visible down to edge plus uncut forelegs. Separate `pose-preview-full-upper` renderer now retains canonical rows above GripY77 beneath the existing exact frame08 foreleg overlay; prior candidate preserved. Literal torso-omission RED1 corrected, final6/6 tests and head/body/upper-source export parity pass; light8x inspected. Initial transparent-head-mask hypothesis did not reproduce and was discarded without mask edits. Preview remains unapproved/product unwired; accepted desktop65304 unchanged. No new art generation, runtime changes, commit/push.

- Partial-touchdown trial accepted / edge-perch resumed,2026-09-08: user says '굳 좋네' and requests edge-perch integration. Preserve accepted running product65304 and partial presentation handoff. Existing approved edge-perch spec/plan and pure Tasks1/2 remain applicable; do not reopen the separately unresolved hover/resume issue as part of this request. Task3 frame08-forelegs-and-chest candidate retrieved and visually inspected again; recorded explicit pose approval was still pending. Show that existing candidate and obtain its visual approval before Task4 product wiring, per the agreed art gate. No source/assets/runtime change at this boundary; no commit/push.

- Partial-touchdown presentation handoff trial deployed,2026-09-08 21:57:26KST: user reproduced intermediate-pose release stutter in3actual own-window diagnostic clips; all show late7/11/15px host rebase with opposite local-image compensation. A mixed compositor frame is a hypothesis, not directly captured. Only released-partial head presentation now normalizes incrementally while preserving world contact/landing ownership; source art/Core/taskbar handling unchanged. RED3 late-host-step failures; expanded grounded-recovery/sampling34pass, fullApp553/Core233pass,29embedded PNG parity and tested/replay/publish App4C1AE881/Core8BDCD85E verified. Reviewer feedback incorporated. Valid `after-final` replay eliminates accumulated exit step; earlier `after`/`after-verified` probes invalid due to old-DLL MSBuild candidate resolution, corrected with explicit items and hash verification. Sequential timing not a speedup claim; extra geometry/cold-start latency remains a risk. Exact diagnostic64300 stopped; normal product65304 responding and loaded paths verified in `Downloads/doro/partial-touchdown-fix-20260908/runtime`. Prior releases preserved; no commit/push. Evidence `artifacts/repro/partial-touchdown-20260908/REPORT.md`. Live visual acceptance pending; intermittent taskbar sinking remains separate/unresolved.

- Partial release anchor correction deployed, 2026-09-08 21:27:33: user authorized residual intermediate-image flicker fix; separate intermittent taskbar sinking remains unresolved/out of scope. Reproduced rounded re-detected anchor1pixel jumps in4partial keys (RED4); generated partial frames now carry continuous authored registration metadata, preserving measured ink bounds/full-release/held anchors and source pixels. Real-presenter subframe max translation ~1DIP→.016–.091DIP across4keys; zero-time button-up composed comparison already passed, so no sampling change. Expanded focused37, fullApp549 + Core233=782pass. All29embedded PNGs/Core unchanged, tested-published App2CC29BD8/Core8BDCD85E parity verified. Reviewer no actionable findings; surrounding-motion warp center intentionally follows corrected anchor, not universal composed pixel parity. Exact old27120/path validated and stopped; new70324 normal product started in `Downloads/doro/release-anchor-flicker-fix-20260908/runtime`, previous runtime retained. No commit/push. Evidence `artifacts/repro/release-anchor-flicker-20260908/REPORT.md`; live user visual acceptance pending.

- Live trial clarification, 2026-09-08: user reports low head-lift/release now produces a very brief flicker rather than the previous longer stutter; this clarification is NOT about taskbar sinking. Partial visual improvement, not acceptance. Separately reported taskbar occlusion recurrence remains unresolved. Product27120/exact anatomical runtime verified responding; no product edits/restart. Metadata-only hover sampling was based on assistant's mistaken linkage and cannot diagnose release-frame flicker. Renderer still changes sampling on release and exits180ms recovery into canonical/presentation rebase; these are inspection candidates, not established causes. Need actual rendered transition-frame evidence before another fix; prior contour tests do not prove full composed visual continuity.

- Anatomical partial recovery product trial deployed, 2026-09-08 21:06:12: fullApp535/535 + Core233/233 (768) pass, all29embedded PNGs unchanged, Core hash unchanged, diffcheck pass. Reviewer approved after actual-chain contour and refinement fixes. Tested/published AppBE94762E/Core8BDCD85E parity verified. Separate `Downloads/doro/anatomical-partial-release-20260908/runtime`; exact old6752 path validated/stopped, new27120 launched. Prior product retained, no commit/push. First launch attempt safely aborted on slash-format path comparison before touching oldprocess; normalized literal retry succeeded. Evidence/report `artifacts/repro/anatomical-release-20260908`. Live user visual acceptance pending; current ease/physics/full-extension path retained.

- Anatomical partial recovery implementation, 2026-09-08: user approved anatomy-aware continuation. Added explicit front/middle/rear paw + torso landmark strips with piecewise affine triangle registration of neighboring supplied keys, preserving original textures/head upper40rows/endpoints. Partial-only source-key opt-in; full drag recovery and Core physics/ease unchanged. First direct-to-rest candidate had height jumps; retained neighboring-key order fixes these. Original alpha-threshold test invalidated by legitimate AA; replaced with real white-composite contour-count REDold2→GREENnew1 plus actual chain3sample RED→GREEN. Reviewer nearest-landmark bug RED75vs74→GREENfixed using immutable authored edge. Focused29/29 (all8keys/densehead/PGBA, short12/24/40/60/78DIP real loop) pass; review approves scoped trial pending fullsuite/performance. Warmed synthetic short-release96samples mean8.878→7.675ms,max18.411→15.391ms (sequential process comparison, not desktopFPS). FullApp/Core/asset checks pending; original product6752 still unchanged. Evidence `artifacts/repro/anatomical-release-20260908`.

- Partial fix rollback verified: source text equality against pre-turn copies true for HeadRecoveryFrame/presenter; restored baseline continuity regressions11/11 pass. Read-only reviewer confirms visible radial regression and no candidate safe to ship. Original product retained; implementation remains incomplete pending anatomy-aware approach discussion.

- Partial head-release fix attempt NOT SHIPPED, 2026-09-08: user authorized change. Synthetic contour REDalpha128 instead255; column, signed-distance, and radial candidates passed narrow tests but each failed real-art visual inspection (side streaks, erased interior strokes, radial-concavity ghosts). Per systematic-debugging three-failure gate, stop global-registration patches and discuss anatomical correspondence/transition-art approach. Production HeadRecoveryFrame/presenter restored to pre-turn content; candidate helper/test removed after preserving artifact copies; baseline regression rerun pending. Running spring-impact-click PID6752 unchanged, no publish/restart/asset changes/commit/push. Evidence `artifacts/repro/partial-head-release-20260908/REPORT.md`; read-only reviewer dispatched.

- Partial head-release visual investigation, 2026-09-08: user accepts current ease and reports image stutter before full extension on low lift/release. Deployed-DLL real-loop/presenter probe lifts12/24/40/60DIP against unchanged taskbar; source strip visibly shows double/fading paw contours in registered adjacent-frame recovery. Short12/24DIP drops contact while180ms source recovery remains active. Height-only/full220DIP integration coverage missed this contour case. Sampling switch and CPU contribution not isolated; no claim of complete live symptom attribution. Recommend scoped partial-recovery silhouette correction while locking current physics/ease. No production edits/restart; evidence `artifacts/repro/partial-head-release-20260908/REPORT.md`.

- Landing onset investigation, 2026-09-08: user requested improvement analysis only. Probe against deployed spring-impact-click Core DLL confirms first returned contact pose has squash/spread 0 at 8/16/33 ms ticks; 16 ms later extreme spread .840023. Collision resets phaseSeconds and discards remaining tick time; ordinary entry separately remains 80 ms smoothstep. Recommend strong deformation on actual collision frame plus consuming post-impact remainder, keeping approved maximum/one-second hold/recovery. No production code/binary/process change. Evidence and proposed regression boundary: `artifacts/repro/impact-onset-20260908/REPORT.md`.

- Spring impact + click hop product update completed, 2026-09-08 20:11:58: fullApp517/517 and Core233/233 pass; protected13assets/diffcheck pass. Published `C:/Users/tjdwo/Downloads/doro/spring-impact-click-fix-20260908/runtime`; tested/published DLL hash parity AppCCE554A7/Core8BDCD85E verified. Exact old65596 validated/stopped, normal newproduct6752 started. Old releases retained. Reviewer ceiling finding fixed with RED→GREEN and re-reviewed, no remaining Critical/Important findings. Both requested changes included; desktop visual acceptance pending. No commit/push. Evidence/report `artifacts/repro/spring-impact-20260908`.

- Spring impact + ordinary click implementation, 2026-09-08: user approved spring update and explicitly answered 일반 클릭 복원도 함께. Extreme entry now35ms cubic ease-out to unchanged .574 maximum,6%recoil by65ms, settle100ms; full1second hold and32DIP recovery unchanged. Spring TDD RED1→Core233pass. Click fix sends existing authored TranslationY through supported physical pose, rebuilding support baseline each frame rather than allowing renderer sole correction to cancel it; no accumulated offset, loss falls from visible hop position. Pending press now pins its.025DIP pivot-induced sole movement when platform target exists, preventing a tap from starting below a windowedge. Real-presenter click TDD REDsink→GREEN12DIP hop; window/taskbar/floor covered. Reviewer identified ceiling-clamp owner reset: reproduced Supported→Falling, added narrow Supported+negative-click-offset exemption at both clamps, GREEN4/4 covers5DIP headroom, moving window and real close. FullApp suite running; assets13/diffcheck pass. Old product65596 unchanged until verification completes. Evidence `artifacts/repro/spring-impact-20260908`; App before copies retained. Deployment pending, no commit/push.

- Spring-like impact timing request, 2026-09-08: user supplied spring-curve screenshot and wants immediate outward splat with easing. Bounded timing change to existing extreme landing. Context inspected: current entry uses symmetric Smooth(t/.10) for both compression/spread. Proposed fast initial expansion over30–40ms, small damped rebound into current .574 compression/unchanged final leg spread, then full1second hold and existing recovery boing. Avoid restoring the previous too-flat maximum. Brainstorming context/design complete, approval pending; no implementation or product restart. Separate normal-click12DIP restoration remains diagnosis-only and not included implicitly.

- Ordinary-click hop diagnosis, 2026-09-08: user reports weak normal-click boing. No production changes/restart. Offscreen probe against actual published70pct DLLs shows clickphase.56 raw sole111→99 (authored12DIP lift) but ApplyPlatformPose(Supported,targetSole111) restores sole111 (visible hop0); sprite height63 unchanged. Same probe against pre-extreme tooltip-shadow-fix DLLs gives identical result, so70% tuning did not introduce it. Source trace: BodyClickTransformSampler confirmed-click TranslationY=-12, presenter applies it, then PlatformContactPresentation correctionY=targetSoleY-soleAfterPose cancels it. Platform release tick also resets/acquires support and targetSole comes from previous presentation; do not fix by blindly skipping all contact correction, which risks grounding/fall regressions. Proposed next change: explicit separation of click visual-hop offset from support contact, preserving12DIP authored click while retaining world support and existing fall landing. Requires implementation request; current product65596 remains unchanged. Probe artifacts `artifacts/repro/click-hop-analysis-20260908`, both real published versions built/run exit0, no desktop UI/input exercised.

- Extreme compression tuned to70% at user request, 2026-09-08: prior product accepted except too-flat pose. Peak compression .82→.574 (remaining height18%→42.6%); compression-to-extension blend updated continuously, while leg spread,80% threshold,1second hold,32DIP recovery hop and lower-fall behavior unchanged. Production change only PlatformMotion constant/three curve branches. TDD updated hold assertions:3expected failures+1control before→18focused App tests pass after; Core231 pass; offscreen updated full-splat.png inspected, diffcheck pass. FullApp suite not rerun for this parameter-only adjustment (previous513 remains historical). Published `C:/Users/tjdwo/Downloads/doro/extreme-landing-70pct-20260908/runtime`, tested/published hashes App23B0E366/Core8BA048BD match. Exact old65184 stopped, new65596 started19:55:16; previous releases retained. No asset edits/commit/push. Evidence `artifacts/repro/extreme-landing-70pct-20260908`; user visual acceptance pending.

- Extreme landing shipped for user trial, 2026-09-08 19:36:46: fullApp513/513, Core231/231, protected13asset identity, diffcheck pass. Published `C:/Users/tjdwo/Downloads/doro/extreme-landing-20260908/runtime`, tested/published DLL hashes equal App8CCB41E2/Core1B84C546. Exact old product52928 validated and stopped; new product65184 launched. Old hover-fixed release retained. Automated threshold/hold/recovery/contact/pickup/viewport evidence and offscreen full-splat rendering verified; actual desktop/user visual acceptance pending. No commit/push.

- Extreme landing implementation, 2026-09-08: approved design plus explicit recovery boing. PlatformMotion uses actual cumulative fall distance >=80% of landing monitor full logical height, reaches .82 squash/.100s, holds fully flat1second, then32DIP supported recovery hop and elastic settle by1.8seconds. Existing lower-fall curve unchanged. Temporary source-art deformation spreads front/middle paws forward and rear backward, freezes source/transform through hold, restores on finish/pickup; presenter high-compression limit enabled only for extreme pose. TDD physics3 expected failures+1control→4pass; render2fail→2pass. New integration/viewport/paw pickup14 total pass; fixtures corrected for80ms scene cache and post-hold spread near1 (not production bugs). Real presenter tests show sole stays on floor through hold and only one supported hop; monitor2@200%DPI threshold, extended opaque paw pickup, both-facing336DIP actual product viewport covered. Core231 and protected13artwork checks passed. Independent read-only reviewer no Critical/Important findings, ready for trial after fullApp suite. FullApp running; deployment pending. Evidence `artifacts/repro/extreme-landing-20260908`, before snapshots retained. No commit/push or source asset edits.

- Hover fix accepted by user, 2026-09-08: user reports 이제 안떨어지네. 좋아 on deployed tooltip-shadow-fix product. This closes user-visible hover acceptance; transient HWND class attribution remains inferred, not directly captured. New bounded request: extreme landing for a fall of at least80% screen height, flatten fully with legs extended front/back and hold1second. Brainstorming checklist: existing PlatformMotion fallDistance/landing timer and presenter contact flow inspected; short design awaiting approval; implementation/tests/deployment not started. Proposed actual fall-distance threshold relative to landing monitor full height, existing subthreshold bounce unchanged, strong compress→1second full-pose hold→smooth recovery, direct pickup interrupts; validate threshold boundaries, hold timing, contact continuity and drag interruption.

- Actual hover fall captured, 2026-09-08 18:54:38–18:55:01: supersedes previous stable-trace inference. User reports 발생 while live2 records. Onset18:54:38.930: sceneReliable=true/noLastFailure, tooltip65890 support/occlusion false, adjacent5px-larger HWND48893604 PID8556 at1707,1039,98x25 remains support/occlusion true ahead of taskbar1040. Foot1710.95..1720.16 loses support and falls1040→1080, then lifts on reappearance. Physical fall confirmed, not merely Z/compositor; old stable traces missed onset. Extra transient HWND destroyed before class query; tooltip class tooltips_class32 verified. SysShadow suspected from decoration geometry, exact class still unconfirmed. New exclusion SysShadow support+occlusionfalse has RED2expected Falling/5controls pass→GREEN27source/disappearance/background. Added16step paired/independent-shadow regression (independent ending is synthetic coverage, not observed sequence). Code reviewer no behavioral findings, evidence gap explicit; unsupported outlives-owner comment corrected. FullApp499/Core231/assets13/diffcheck passed. Post-comment rebuild focused default-parallel run exposed DPI-context test failure (24592→24593); prescribed serial settings rerun27/27 passed; no test suppression or unrelated DPI fix. Published candidate at `C:/Users/tjdwo/Downloads/doro/tooltip-shadow-fix-20260908/runtime`; tested/published DLL hash parity AppEB00C381/Core110BB84E. Exact old diagnostic67396 stopped, normal product52928 started19:09:08, correct modules loaded, Responding=true, visible/uncloaked HWND3476940 at1532,833. Old product retained. Real hover acceptance and offending class identification remain unverified; do not claim incident resolved. Evidence/report in `artifacts/repro/tooltip-shadow-fix-20260908`.

- Hover trace follow-up: user answered 발생했어 for diagnostic reproduction, but response arrived after3minute recorder ended, so exact event/trace correlation is not established. Trace2200+ samples shows stable worldsole1040 afterstartup, pet Z ahead of taskbar, one retained-support ZOrderChanged failure; cannot assign actual screenshot to physics or compositor yet. Computer-use skill loaded and official sky.list_windows attempted; transparent/tool pet HWND is not targetable, so no window capture/input performed (only returned Brackets matched path text and was NOT selected). Investigated official WPF dirty-rectangle/monitor-clipping references as hypotheses, not diagnoses. Same unmodified product DLLs now in `artifacts/repro/hover-source-regression-20260908/live2` diagnostic host, bounded30minute/64MiB recorder includes wall-clock and pointer position every100ms; prior65840 exactpath stopped. Need user reproduce now and report promptly; no production fix or acceptance claim. Next inspect live2/internal-trace2.jsonl and restore normal product executable when diagnostic session ends.

- Internal hover observation,2026-09-08: user supplied network/input-method tooltip screenshots with lower body hidden. Product source unchanged. Exact App42AE3201/Core110BB84E DLL parity verified for diagnostic host; old60724 exactpath stopped, same App/MainWindow/production loop launched under diagnostic host65840 at18:42:32KST. Diagnostic reads actual loop contact/motion/source cache/host origin/Z metadata by reflection, without changing behavior; native capture is NOT duplicated. Three-minute bounded trace `artifacts/repro/hover-source-regression-20260908/internal/bin/Release/net8.0-windows/internal-trace.jsonl`: after initial startup lift, hostTop833, localsole111, worldsole1040, Supported on primary taskbar throughout. One transient ZOrderChanged:0 freezes support and recovers without falling. Hover symptom during trace not confirmed; screenshots do not prove which physics/render/Z boundary failed. Timer stopped after3minutes; diagnostic app65840 remains running same product DLLs, no ongoing logging. User asked whether symptom reproduces in this same-code restart; response pending. No fix, rollback, source-art change, commit or push. Next must reproduce actual transition; do not present stationary internal evidence as disproving the screenshots.

- Hover regression investigation,2026-09-08: user reports pet falls in front on taskbar-icon hover after CPU release. Only runtime60724 verified; no rollback/new product edit yet. Systematic-debugging inspection found no evidence yet implicating background expiry. Separate40second scene observer853samples had0failure/0expired samples, petHWND Top833 throughout and taskbar1040 continuously available after observer startup. This is NOT proof of no symptom: observer is not product-internal state, and user confirmation whether the symptom occurred during capture is pending. Need distinguish actual physics/local-render motion from taskbar z-order reveal and capture triggering icon/popup before changing code again. Evidence `artifacts/repro/hover-source-regression-20260908/bin/Release/net8.0-windows/trace.json`; screenshot retained by user. No new fix or acceptance claim.

- Landing CPU corrections deployed for trial,2026-09-08: supersedes pending entry below. Comparable warmed12cycle synthetic release ticks12.1095→8.6481ms; initial4cycle39.16ms is not the comparable baseline. Affine snapshot removes repeated WPF transform-tree traversal. Second measured blocker: synchronous native scene acquisition on animation thread; production now seeds startup synchronously then uses single-flight metadata-only background capture, detached snapshots, request-time500ms expiry and sanitized failure recovery. Actual native-read comparison29samples each: UI read mean21.387→0.360ms,max87.164→4.686ms (sequential samples under concurrent test load, not compositor FPS). Background RED2→GREEN15, final extra failure/lifetime coverage17pass; fullApp495/495 before two test-only additions,Core231/231,13protected assets and diffcheck pass. Independent reviewer approved scoped trial with no blockers. New publish `Downloads/doro/contact-frame-cost-fix-20260908/runtime` matches tested App42AE3201/Core110BB84E; old32348 exactpath stopped, new60724 launched18:19:49KST, loaded DLLs/visibleuncloaked336px HWND31526470 verified. Prior publish retained. Evidence `artifacts/repro/frame-time-root-cause-20260908/REPORT.md`. Live head-drop visual acceptance remains unverified; synchronous startup/expired monitor fallback remain, no60FPS guarantee. No art/physics/perch/commit/push changes this turn.

- Landing stutter CPU root-cause work,2026-09-08: user confirms head-release landing (not tooltip). Actual published loop/presenter benchmark finds39.16ms mean recovery ticks (16ms budget fail), contact Measure+Apply≈30.77ms; prior art tests never measured this. ONLY PlatformContactPresentation now flattens known affine WPF chains once permeasurement rather than traversing dependency-property transforms for every occupied corner. Unknown/nonaffine uses originalpath; mutabletransforms re-snapshotted. Initial36geometry/regression pass; reviewer emptygroup semantic issue RED2→GREEN4 corrected. Improved warmed12cycle run releasefall9.27ms/landing7.77ms; initial4cycle timing confounded by JITwarmup, retained. Separate native desktop acquisition≈13ms remains outside synthetic-scene loop measurement; no full-desktop frame guarantee. Fullsuite/comparablewarmbaseline/review/publish gates pending. Product32348 unchanged; noart/physics/commit/push.

- Taskbar tooltip fix deployed,2026-09-08: exact tooltips_class32 support+occlusion exclusion; native/taskbar/disappearance23 and Core231pass,13assets intact. Scoped read-only review nofindings. Tested/published AppD89AC2DC/Core110BB84E exact. Old66948 exactpath stopped; new32348 launched17:58:06KST from Downloads/doro/taskbar-tooltip-fix-20260908/runtime; priorruntime preserved. Evidence `artifacts/repro/taskbar-tooltip-fix-20260908/REPORT.md`. Native exact hover class was not captured, live speaker-tooltip trial remains unverified; nofullAppsuite rerun claimed,noart/physics/perch/commit/push.

- Taskbar tooltip follow-up,2026-09-08: user reports drop at volume hover tooltip. Reader already denies tooltips_class32 support, but adapter still marks it occluding; adds exact tooltip class to existing hover-overlay support/occlusion exclusion. Does not blanket-ignore all nonsupporting windows; #32768 menus and ordinary windows retain occlusion. RED1tooltip failure/4controls pass, GREEN23native/taskbar/disappearance regressions and Core231pass,13assets unchanged. Live snapshot did not catch tooltip(still exactruntime66948 healthy), so actual tooltip-HWND trigger unverified; deterministic realadapter/physics reproduction confirmed. Independent review/new separate publish pending; noart/physics/perch/commit/push.

- Continuous head recovery deployed,2026-09-08: supersedes in-progress entry below. App487/487 serial (parallel runs exposed WPF PackagePart/XAML loading races, retained),Core231,focused41 pass;13protected assets unchanged. Independent reviewer noCritical/Important, minor faint paw contour crossfade remains disclosed. Captured release uses registered adjacent keys and no touchdownCancel; max de-squashed height step3DIP at12/80/220gaps vs prior4..14,contact times unchanged. Tested/proof/publish exact App98236C31/Core110BB84E. Verifiedold35748 stopped; new66948 launched17:54:05KST from Downloads/doro/continuous-head-landing-20260908/runtime. Oldruntime retained,noart/perch/commit/push. Evidence `artifacts/repro/landing-continuous-fix-20260908/REPORT.md`. Implementation and runtime update complete for user trial; final live visual acceptance unverified.

- Continuous head-recovery correction in progress,2026-09-08: user explicitly says fix without repeated reporting. Removed forced touchdownCancel, captured head release now registers/morphs adjacent supplied poses continuously through physical impact and finishes at exact canonical. Original held/noanchor legacy unchanged. New consecutive visible-height contract RED6 failures4..14DIP/1pass; GREEN7/7. A direct held→canonical blend was rejected by parent for ghost legs, preserved under landing-continuous-fix/rejected-direct-morph; revised adjacent-key path viewed and tested. Focused41/41 includes bothfacings/full/partial exactrelease endpoints, legacyasset contracts and anchoring. New read-only review dispatched; fullsuite/render/performance/publish gates pending. Running35748 unchanged until verified replacement; noart/perch/commit/push.

- Head low-drop user trial FAILED,2026-09-08: user still sees short-drop discontinuity. Actual runningPID35748 verified as head-landing-pop-fix runtime, not staleversion. Reinspection of its identical-DLL gap12 proof exposes112→128ms forced recovery cutoff: height73→63.00025DIP, worldtop774.26846→784.97861 while worldsole changes only0.7104DIP and firstLandingSquash=0. Last fix removed DURING-impact art cycling but introduced/retained a10DIP posture snap AT touchdown. Tests checked canonical stability after contact, not cross-contact continuity; prior completion claim was insufficient for reported symptom. No additional product changes/restart in this diagnosis turn. Next fix must preserve displayed shape across contact and continuously recover without abrupt canonical substitution; source identity/phase-only checks cannot establish smoothness. Actual frame-time stalls remain unmeasured.

- Head low-drop product update delivered,2026-09-08: supersedes pending gate below. App481/Core231 and12focused tests pass; original13asset identities preserved. Old sampling assertions deliberately migrated to release rest-mode continuity plus real smooth-raster comparison (held tests unchanged). Read-only review no Critical/Important/actionable Minor findings. Exact same replay has0landing recovery overlaps/1canonical source at12/80/220DIP; contact times128/304/496ms unchanged. Tested/proof/published DLL parity verified AppB4B51185/Core110BB84E. New Downloads/doro/head-landing-pop-fix-20260908/runtime launched17:32:32KST asPID35748 after exactold64108 stopped; responding/visible nativewindow and loadedDLLpath verified, priorruntime preserved. Report `artifacts/repro/landing-pop-fix-20260908/REPORT.md`. Product fix applied; live low-drop feel remains user trial, not visual PASS. No art/perch/commit/push.

- Head low-drop landing-pop product fix,2026-09-08: user explicitly requests completion through product replacement. Bounded patch finishes only released head recovery at physical touchdown and retains rest sampling through release; held poses/art/physics remain unchanged. RED2 assertion failures/3pass (12DIP overlap, release sampling), GREEN5/5 across0/12/80/220DIP. Separate read-only review requested; full tests/render proof pending. Current desktop64108 remains unchanged until verified publish. No commit/push/perch integration.

- Low-drop landing-pop diagnosis,2026-09-08: user says disappearance seems fixed(tentative), reports momentary different-art/stutter especially head release from low height. Published-DLL deterministic replay finds12DIP landing128ms overlaps head recovery until208ms(5landing ticks,6source hashes);80/220DIP controls finish recovery before landing(1source). At208ms key-family→canonical plus NearestNeighbor→HighQuality handoff. Worldfoot is continuous; unrelated sleep/startled art absent in fixture. Final world-anchored V3 sheets/trace at `artifacts/repro/landing-pop-analysis-20260908/`, REPORT explains initial blank/local-coordinate sheet limitations. Diagnosis only; actual frame-time stalls unmeasured, productPID64108 unchanged, no new art/product patch/commit/push. Next is coordinating recovery/contact and stable sampling without undoing prior rebase fix.

- Active disappearance fix,2026-09-08: user approved2 then explicitly added1. Scope now known shell-preview support loss + completed-direct coordinate rebase; no artwork/perch integration. Evidence/plan `artifacts/repro/taskbar-disappearance-fix-20260908/README.md`,4before-source copies preserve existing edits. RED3assertion failures/1pass, GREEN4/4. Independent read-only review dispatched per requesting-code-review skill while parent runs full suites and physics continuity tests. Product runtime not yet replaced; commit/push not authorized.
- Disappearance fix delivered for user trial,2026-09-08: both fixes complete with two review-discovered regressions reproduced/corrected (retain squash/sway contact during rebase; select monitor using rebased FramePosition). Scoped re-review approved,no remaining Critical/Important findings. Final App476/Core231 pass,6focused tests pass,14-cycle proof retains3517+visiblepixels and localsole≈111/worldfoot1040. Assets13identities unchanged;260baseline audit only4intended source diffs; diffcheck0. New Downloads/doro/taskbar-disappearance-fix-20260908/runtime published, tested-DLL parity verified. Old exactPID10824 stopped, newPID64108 launched16:59:23KST; old runtime preserved. Actual user drag/hover acceptance pending; no perch integration,commit/push. This supersedes preceding runtime-not-yet-replaced status.

- Taskbar recovery/causal capture,2026-09-08: user approved same-version restart; old verified PID45212 replaced with identical runtime PID10824 at16:16:45KST, user confirms visible. Live373samples captured actual TaskListThumbnailWnd bottom1041 ahead of taskbar1040 → primary support absent → nativeY776→782→778→776, user confirmed occurrence. Separate actual-DLL repeated head-carry/release harness reproduces cumulative localsole111→127→143→159→175→191 in5cycles while worldfoot1040 remains correct;14-cycle extension progressively clips raster3538→2972→1913→821→11→0pixels. Late synthetic presses bypass native clipped hit testing; do not claim exact14-cycle user reproduction. Historical total disappearance attribution remains inferential without original internal-state capture. Details/evidence in `artifacts/repro/taskbar-hover-analysis-20260908/REPORT.md`.260baselinefiles unchanged; published hashes unchanged,diffcheck0; no product patch/build/update,commit/push; pending perch remains gated. Earlier restart/trigger statuses below are historical.

- Taskbar invisibility follow-up: user confirms currently invisible. Published Presenter1800frame isolated checks (walking/mixed/squash) stay nonempty with stable sole111; simple presenter accumulation not reproduced. Actual HWND still visible/notminimized/notcloaked and movingX atY635. Region/layer-alpha queries unavailable, not empty/alpha0 evidence. Full disappearance root remains UNVERIFIED; state preserved and same-version restart requires user direction. Diagnostic report extended; no product modification/application.

- Taskbar hover diagnosis,2026-09-08: user reports preview→nonrunning-icon dip/return, then bottom-up total disappearance while preparing capture. Read-only artifacts `artifacts/repro/taskbar-hover-analysis-20260908/REPORT.md`. Actual running DLL simulation reproduces dip/return when nonsupporting occluder crosses bar top; control above top does not. Original shell trigger still UNVERIFIED. Live361samples after disappearance:allhealthy,pet nativeY635constant/Xmoving,bars full after observerwarmup; visible/notminimized/notcloaked. Current total disappearance not explained by native downward motion/hide; render/compositor diagnosis pending user's pointer-away result/fresh onset capture.260baselinefiles unchanged; no app update,restart,commit or push. Perch preview remains unconnected.

- Edge-perch Task3 user revision,2026-09-08: user requests existing dangling frame8 forelegs+chest instead of newly authored paws; approved bounded preview replacement `ㄱㄱㄱ`. New single candidate `artifacts/repro/edge-perch-20260908/pose-preview-frame08/` reuses transparency-cleaned registration-key-08-presenter.png via integer(-11,+17) crop only; protected head2323pixels unchanged,383body pixels exact, no new outline. RED3fail/2pass, GREEN5/5 and export/source/nearest checks pass. Native/light and8x/dark inspected locally; no new independent review claimed. GripY77 replaces old proposal70 for this candidate only, runtime not wired. Browser tab4/127.0.0.1:2785 opened; helperPID35484. Prior candidate,260baselinefiles and source artwork unchanged. User visual acceptance pending; Task4 remains gated, product NOT APPLIED.

- [ ] Edge-perch + resume recovery,2026-09-08: 상세 설계 `ㄱㄱㄱ` 승인. Plan `docs/superpowers/plans/2026-09-08-edge-perch-and-resume-recovery.md`. Existing linked worktree verified; preflight Core176/App470 pass. Source/test baseline copied+hashed under `artifacts/repro/edge-perch-20260908/`. Execution uses existing user-selected subagent implementation/review. Tasks1/2 complete; Task3 preview delivered/reviewed, user art acceptance pending. STOP at approved art gate; Task4 runtime integration and Task5 final validation/handoff remain pending. No product runtime replacement/commit/push.
- Edge-perch execution constraints: TASKS.md remains the sole ledger; task steps stay in plan and task reports stay in per-plan evidence directory. Hash-frozen review packages replace commit-bound diffs; no evidence deletion. Art approval boundary controls over generic continuous-execution guidance. This preserves user auditability at the cost of retaining local artifacts.
- Edge-perch Task1 complete: implementer `/root/edge_perch_engine`; reviewer `/root/edge_perch_engine_review` spec compliant / quality Approved, no findings. RED18 assertion failures, GREEN32/32, Core208/208; immutable task1-review-v1 two source/test hashes exact. Baseline existing260 files unchanged. Reviewer caller-expiry/freshness/orientation items belong explicitly to Task4; stub source absent from final diff but runnable RED TRX retained. Pure logic only, not app-connected. Task2 next.
- Edge-perch Task2 complete: implementer `/root/edge_perch_recovery`; reviewer `/root/edge_recovery_review` spec compliant / quality Approved, no findings. Required runnable RED1fail/3pass, GREEN19/19, Core227/227; frozen task2-review-v1 hashes match. Actual PlatformMotion persistence and correction/reset regression has zero squash/later bounce. Runtime freshness/orientation/correction application remains Task4; original resume trigger UNVERIFIED. Extra mistyped RED evidence folder retained; invalid GREEN argument attempt is not counted as test evidence. Task3 single pose preview next; no product wiring.
- Edge-perch Task3 complete as review-ready preview, not art approval: author `/root/edge_perch_pose`, reviewer `/root/edge_pose_review` spec compliant / quality Approved, no findings. One candidate at `artifacts/repro/edge-perch-20260908/pose-preview/`; native PNG SHA256 `B16A0B9C27F9B01AC79BBB505A91291E5E1D464352BDC4BB0CEB2860BFABA10A`. Retained2323visible source pixels0RGBA mismatch; hidden919body pixels0alpha violations; nearest/translated/protected overlap checks0mismatch. Parent and reviewer viewed actual native/dark8x. Authored head/body mask remains art assumption. New IAB tab3 serves127.0.0.1:2784 via static helperPID57616, page renderer success observed; live toggle/raw browser parity not exercised. Existing playback2783, petPID45212/DLLs and260baselinefiles unchanged. Parent verification recorded in `parent-preview-check.md`. STOP for explicit pose approval before Task4; no app publish/replacement or complete-resume-fix claim.

| Edge-perch preflight item | Check / outcome |
|---|---|
| Task1 internal | Grip-relative160ms entry, release boolean and full paw containment agree with literal tests; no existing physics edit. |
| Task2 internal | Taskbar-only penetration selector returns a surface; caller Reset/translation demonstrated without changing old policy globally. |
| Task3 internal | One head-locked preview, disclosed geometry and native inspection; visual approval remains separate from computed identity. |
| Task4 internal | Single loop writer, intentional release latch, lifecycle recovery, alpha-aware input; requires approved Task3 pose. |
| Task5 internal | Tests/preservation/review before separate user trial; actual power-resume never inferred from simulations. |
| Task1→Task4 | PerchContact/PerchSurface and owner identity consumed by EdgePerchRuntime; ordinary sole never substitutes grip. |
| Task2→Task4 | FindPenetration consumes mapped surfaces and explicit direct/perched flags; only recovery path aligns sole/reset energy. |
| Task3→Task4 | Frozen mask/anchors become display-space grip; no numeric contact invented independently. Gate prevents unapproved art application. |
| Task1→Task5 | Pure-engine tests and source hashes included in final review. |
| Task2→Task5 | Recovery proof is policy evidence, not physical-resume acceptance. |
| Task3→Task5 | Head identity and one candidate included; final runtime requires explicit art acceptance. |
| Task4→Task5 | Runtime/integration hashes match tested output before new executable launch. |
- [ ] Display-resume clipping analysis,2026-09-08: user reports drag restores clipping. Six runtime simulation cases recover normally (gap/bar disappearance/capture expiry/OS displacement/monitor resize/contact epsilon). Isolated existing-bar Core policy retains foot1080 below bar1040 for300ticks; Reset recovers1040. Live5samples show primary taskbar ahead of pet although both Topmost. Persistent-state mechanism confirmed in constructed case; actual resume trigger UNVERIFIED. Report `artifacts/repro/display-resume-analysis-20260908/REPORT.md`. Reviewed product/test10 hashes unchanged, PID45212 not restarted. Next requires symptom retained before drag or explicitly authorized instrumentation, not unproven Topmost repair.
- [x] Height-scaled dramatic landing applied, 2026-09-08: actual downward travel drives one450ms contact sequence, peak30% squash/15% widening,10% extension/18-DIP real hop (300DIP saturation,12% minimum gentle response), no drag-distance contribution. Core176/App470 and four PS checks pass. Independent v1 real-sprite edge-reset finding reproduced in two RED cases, fixed, v2 no remaining findings; reviewed10 files match. Protected artwork/saved runtimes72 unchanged. Tested/published/loaded AppF3ACE961/Core6D4D27A7 exact; verified oldPID53956 replaced by45212 at12:01:58+09:00, responding. Evidence `artifacts/repro/height-scaled-landing-20260908/REPORT.md`. No commit/push; live feel/mixed-DPI/abrupt-window-move clipping UNVERIFIED. STOP for user trial.
- [x] Surface-only revision applied, 2026-09-08: user specifies hidden taskbar => monitor bottom edge. Active runtime uses horizontal supported walking/retreat and immediate release-to-gravity with concurrent shape recovery; real visible overlay contact receives the sole correction/squash. Core169/App463 + four PS checks pass; native capture21/21; protected art/saved runtime72 exact. Independent reviewer v1 ownership/facing findings reproduced (8 RED), fixed (11 focused GREEN), v2 no must-fix findings; final reviewed10 source/test hashes exact. Published and loaded App1B98B8ED/CoreCB664D6E match tested. Exact prior PID24792 replaced with53956 at11:37:07+09:00, responding. Evidence `artifacts/repro/surface-only-release-20260908/REPORT.md`. No commit/push, art/old runtimes unchanged; user feel/mixed-DPI acceptance UNVERIFIED. STOP for user trial.
- [x] Surface-only design intake, 2026-09-08: legacy36px timed landing preceded platform fall; free X/Y proposals remained. Bounded revision approved with user correction: hidden taskbar uses screen bottom edge, not remembered bar height. Implementation and verified user-trial launch recorded above.
- [x] Fallfeedbackfix2026-09-08 complete/applied: CoreRED2/AppRED4→focused8/40/44GREEN; fullCore166/App438 and4PSchecks pass. Stableonephysicalpixel readback envelope+continuousposition beforebrainUpdate preservesvelocity/touchdown; autonomousstate/facing/phase frozen without blockingdirectinput. ActualHWND+presenterloop bothfacingsrenderedsquash; externalmove/DPIreset regressions; independentreview noissues. Reuseddiagnostic afterfix400DIP→Landing at672simulatedms,15landingticks/peak.069282 vsprior80DIP/3.84s andzeroLanding. Freshruntime `C:/Users/tjdwo/Downloads/doro/window-taskbar-fall-fix-20260908/runtime`, loadedAppA346E275/Core964ADC6B matchtested. ExactoldPID49348 replacedby24792 at11:07:25+09:00, responding;72art/savedfilesexact. Oldruntime/evidencepreserved; no commit/push. Evidence/report `artifacts/repro/platform-fall-feedback-20260908-fix-1/REPORT.md`. Livefeel/mixedDPIacceptanceUNVERIFIED; STOPforusertrial.
- [x] Live feedback diagnosis2026-09-08: slowfall/directionflip/missinglanding reproduced against publishedDLLs in isolated ownWPFhost. Realmap239resettriggers over240x16ms,80DIPdrop vs400DIPcontrol reachingLanding at672ms; nearfloor realhost skipsLanding(peakSquash0), memorycontrol15Landing ticks/peak.069282. SuspendAutonomousMotion leaves state/facing transitions active (deterministicIdleRight→WalkLeft). Diagnosis `artifacts/repro/platform-fall-feedback-20260908-diagnosis/DIAGNOSIS.md`; no product fix/replacement, usertrialPID49348 unchanged. Prior static-coordinate/first-tick tests missed moving-HWND rounding. Repair awaits direction.
- [x] User requests execution,2026-09-08: verified oldordinaryPID52024 path/start, stopped only that process; launched publishedtrialPID49348 at09:55:08+09:00. Process responding; loadedAppA4C21A49/Core7FBA7EED match tested hashes. Savedversions/browser/source unchanged. Launch evidence `artifacts/repro/window-taskbar-platforms-20260907-attempt-1/launch-20260908-095508.json`. Runtime now APPLIED for usertrial; actualvisual/interaction acceptance UNVERIFIED, no commit/push.
- [x] User approves starting with standing on window tops / falling when support disappears, and explicitly adds the taskbar. Existing code only supplies work-area/pointer input; classify as a new architectural subsystem on ordinary-rump checkpoint87e7bb2.
- [x] Drafted `docs/specs/2026-09-07-window-taskbar-platforms.md`: shared read-only platform detection, visible feet as contact reference, movement/occlusion/support-loss handling, taskbar auto-hide proposal, direct-input priority, no window contents or external-window mutations. Draft checked for scope/contradictions; detailed user review pending.
- [x] User `ㄱㄱ` approves the detailed design including taskbar auto-hide fall/reappearance lift. Implementation plan: `docs/superpowers/plans/2026-09-07-window-taskbar-platforms.md`; scene geometry, native acquisition, support motion, actual-sole presentation, loop ownership and separate verification gates are specified.
- [x] User selects fresh subagent implementation plus separate task reviews. Existing linked worktree verified, no new worktree. Artifacts: `.superpowers/sdd/2026-09-07-window-taskbar-platforms/`; baseline evidence: `artifacts/repro/window-taskbar-platforms-20260907-attempt-1/`.
- Ruling: retain review evidence and use this ledger, not a second SDD progress ledger or per-task commits — repository single-ledger rule and this feature's preservation/no-commit boundary control; cost is hash-bound review packages instead of commit-bound packages.
- Ruling: Task6 preservation/baseline step executes before Task1 — it is preflight, despite its location in verification task; otherwise original-state evidence would be lost.
- Ruling: no silent DPI-manifest change; native coordinate agreement is a delivery gate — current WPF behavior must not regress across direct interactions; mixed-DPI delivery may require adapter repair if the measured contract fails.
- Ruling: byte-hash current source/tests/tools, selected checkpoint records and both saved runtimes; inventory all older artifact paths/size/mtime instead of hashing every historical binary/image — broad54k-file read had not reached tests after several minutes; cost is that untouched historical artifacts receive metadata-preservation evidence, not an exhaustive byte-identity claim. Scoped incomplete read-only collector stopped; no evidence/source/runtime was changed by it.

| Preflight pair/task | Shared contract / consistency | Finding or resolution |
|---|---|---|
| 1 internal | Build vs FirstCrossing; physical or consistently mapped scene | Tests use literal intervals; 2-unit overlap applies after mapping; native 2px taskbar visibility belongs to Task2 |
| 2 internal | Native raw scene vs polling/map | Failed capture distinct from successful empty scene; generation tracked across successful absence |
| 3 internal | Direct suspension vs support/fall/lift | Suspended returns desired position; first resumed tick seeded from actual displayed contact |
| 4 internal | Bitmap contact vs transform restoration | Sole is opaque pixel bottom edge, not 144-DIP box; restore before capture/base render |
| 5 internal | Brain update vs single host position writer | ApplyPlatformPosition must reconcile actual brain; optional injection retains legacy behavior |
| 6 internal | Preservation before implementation vs final verification | Execute preflight first (ruling above); final tests/review/delivery remain last |
| 1 ↔ 2 | DesktopScene/Window/Monitor contracts | Native physical snapshots, map once before Core use; never mix coordinates |
| 1 ↔ 3 | Surfaces, FootContact, FirstCrossing | Same owner key across occlusion splits; generation prevents stale reattachment |
| 1 ↔ 4 | FootContact | Presenter supplies local transformed visible contact; runtime adds position |
| 1 ↔ 5 | Geometry construction | Full monitor floors, no virtual-desktop gap floor; image-derived body height |
| 2 ↔ 3 | Scene reliability interpreted by runtime | Brief failure freezes; expiry supplies floor-only scene, not stale window support |
| 2 ↔ 4 | Coordinate mapping/contact | Actual screen origin and displayed image transform must agree |
| 2 ↔ 5 | Polling/source/map | Bounded80ms scene reads; runtime owns conversion and monotonic clock |
| 3 ↔ 4 | PlatformPose | Presenter-only squash/sway; position comes only from loop |
| 3 ↔ 5 | PlatformMotionInput/Advance | Previous actual position plus brain desired delta, not two independent writers |
| 4 ↔ 5 | Presenter hooks/loop Render | Restore before render/input capture; head/cheek/body early returns covered |
| 1–5 ↔ 6 | Tests, source snapshots, runtime | Serial tests; immutable art/saved versions; native metadata is not desktop visual acceptance |

- [x] Preflight: fresh Release Core96/App354, both exit0; 283 current source/checkpoint/saved files SHA256 recorded, historical artifact metadata inventory recorded. Baseline logs/result under current attempt. No source/runtime mutation during preflight.
- [x] Task1: complete, no commits; `/root/platform_geometry` (Sol) implemented3newCore/test files, frozen task-1-review-v1; `/root/platform_geometry_review` spec/quality approved. New27/fullCore123pass. Code unchanged across scoped evidence correction/re-review; original compile-only TDD deviation disclosed, mutation replay14assertionfailures and production27GREEN demonstrate supplementary sensitivity.
- Task1 review: `/root/platform_geometry_review` reports spec compliant / quality approved on frozen3-file package; parent checked3/3 SHA parity and actual Core123 log. Parent evidence correction open: original RED was compilation-only, contrary to required assertion RED. Implementer preparing controlled copied-code mutation replay and unchanged-product GREEN; no claim of retroactive strict TDD.
- Task1 gate closure: scoped re-review marked evidence correction ADDRESSED, no new issue. Parent original283-file hash check reports0changes and HEAD87e7bb2 unchanged. Reviewer cross-task coordinate item is assigned to Tasks2/5, not an omitted Core conversion; native/window-to-loop verification remains pending there.
- Ruling: floor key is SurfaceKey(0,0,monitor.Id), origin=(monitor.Left,monitor.Bottom), with Kind.Floor — gives stable per-monitor synthetic support without colliding with nonzero native HWNDs; downstream must retain Kind and monitor identity, otherwise reattachment could be wrong.
- [x] Task2: complete, no commits; `/root/platform_native_source` implemented5files, frozen task-2-review-v1, parent hash parity5/5. `/root/platform_native_review` (Astra) spec compliant/quality approved, no Critical/Important.20focused and374serialReleaseApppass. Native metadata only, no loop/render wiring or mixed-DPI visual approval.
- Task2 implementation reported ready:20focused assertion-RED/GREEN tests, full serialReleaseApp374/374; earlier default-run373/374 with WPF loader error preserved. Read-only native probe saw2monitors/2taskbar windows and restored thread DPI context. Frozen5-file package task-2-review-v1; `/root/platform_native_review` (Astra) reviewing spec+quality. No runtime wiring or mixed-DPI visual approval.
- Task2 integration convention: Expired scene read has Scene=null; Task5 must preserve/refetch monitor-floor information separately rather than treating this as a valid monitorless desktop. Task2 full App test reported WPF PackagePart resource-load failure; failed run retained, baseline-matching disabled-xUnit-parallelism rerun authorized. Cause not yet established.
- Task2 minor (deferred): native invalid-handle test covers only HWND0, not deterministic mid-read destruction/live-query failure; final review to triage, extend when adapter boundary is touched.
- Task2 minor (deferred to Task5 adapter integration): reject nonfinite off-axis samples before coordinate-map alignment checks (DesktopCoordinateMap.cs:11). Task5 carries this narrow validation/test fix.
- [x] Task3: complete, no commits; `/root/platform_motion` (Astra),62affected/158fullCorepass, assertionRED31 plus self-reviewRED4.5-file task-3-review-v1 includes delta to frozenTask1 for Bottom metadata. `/root/platform_motion_review` (Astra) spec/quality approved, no Critical/Important. Expiry/topology remains Task5; visual feel remains unverified.
- Ruling: Sway is radians, +/-0.03 maps once to about+/-1.719degrees, presenter cap2degrees — explicit implementer clarification resolves formerly undocumented units; wrong conversion would make sway imperceptible or excessive. Task4 tests this mapping.
- Ruling: Task5 owns monitor-removal nearest-monitor/full-visible-body clamp, then resets motion from actual host position — Task3 surfaces lack monitor top/full bounds; guessing there could put the pet offscreen. Task3 does not implement invented monitor bounds.
- Ruling: carry nullable clipped taskbar Bottom on PlatformSurface and use actual rectangle overlap for lift — floor height cannot stand in for a floating taskbar's bottom; otherwise reappearance could pull the pet upward across empty space. Task3 may narrowly amend models/Build/geometry tests, maintaining existing constructors.
- Ruling: Task4 hook accepts explicit targetSoleY supplied by Task5 from the same contact used by motion — PlatformPose has no contact-target field; guessing after base rendering could let walking/breathing lift the feet off a platform. Task5 also samples actual host position for OS-move reconciliation, not only its stale previous snapshot.
- Ruling: capture/classify the visible transformed pose before retiring platform presentation — existing body/cheek capture happens at press and head anchor at early Render; resetting first would shift the grab point. Replace the plan's earlier contradictory restore-before-capture instruction; cost if wrong is a first-frame drag jump, covered by Task4/5 regressions.
- [ ] Task4 actual-sole presentation: implementation reported complete, focused15/full serialReleaseApp389 pass; parent read actual389/0 TRX counters. Frozen3-file task-4-review-v1; `/root/platform_contact_review` (Astra) reviewing spec/quality including capture ordering, support band and squash/rebound producer contract. No native/loop wiring, art/XAML edits or runtime replacement.
- Ruling: support width is the envelope of alpha>=128 transformed pixel quads clipped to the lowest2DIP band, excluding zero-area touches — full head/body X bounds falsely support an overhang; source-bottom-row alone becomes wrong under rotation. VisibleTop remains full silhouette; full-body X clamp is separate. Single envelope cannot model gaps between multiple simultaneous contact islands, retained as DTO limitation rather than a multi-contact redesign.
- [x] Task4: signed rebound fixed after3 assertionRED failures, focused18GREEN; same reviewer scoped task-4-fix-v1 re-review ADDRESSED/no new Critical/Important. Earlier fullApp389 predates one-line fix; final suite assigned Task5/6. Native/visual acceptance not claimed.
- [ ] Task5 implementation reported complete with concerns: App430/Core164 full serial pass, focusedApp63/Core6; parent parsed actual full TRX counts. Frozen13-file task-5-review-v1 against prior-task base; `/root/platform_loop_review` (Astra) reviewing. Synchronous collector8-16ms warm sample and actual mixed-DPI/native feel remain explicit trial gates. No runtime replacement or commits.
- Task4 review fix pending: Important signed-squash mismatch; presenter dropped producer's negative rebound. Implementer assigned assertion-RED negative extension/sole regression and narrow clamp fix. Pre-existing verbose native test diagnostics deferred to final review as Minor; no pass-count invalidation.
- [x] Task5: review spec/quality approved, no Critical/Important; parent frozen13/13 parity. Remaining Minor map-invalidation while falling/supported test coverage and synchronous collector cost carried to final review/Task6 measurement.
- [x] Task6 complete: fresh Core164/App430 and4PSchecks pass; native21/21 and currenttwo96DPI WPF/nativezeroerror; source278/283exact(5intendedchanges), saved20exact, historical54979metadataexact. FinalAstra review no productCritical/Important; runner exit fix RED6/7fail→GREEN7/7, scopedrereview ADDRESSED. Failed native JSON harness and publication-v1 ledger-drift preflight retained; successful finalprobes-v2/publication-v2 evidence separate.
- [x] User-trial package published, NOT APPLIED: `C:/Users/tjdwo/Downloads/doro/window-taskbar-platforms-trial-20260908/runtime` and sibling ZIP SHA256 AE4B6B2F0E296B3B229B8259BDC840D8272568813DA6CFC32FDBDA857953D802. Published App/Core match testedA4C21A49/7FBA7EED; ZIP9/9byteparity, reviewed23/23exact(excludes parent-owned ledger), protected83/83exact. OldordinaryPID52024/browser/savedversions unchanged. No launch/commit/push/PR. STOP for usertrial; actualwindowfollow/autohide/compositor/mixedDPI/useracceptance UNVERIFIED. Deferred map-invalidationphysics/midquerydestruction coverage and measured synchronouscapturecost remain documented.
- Task5 integration checkpoint: first24focused pass, not final. Authorized narrow monitor-only reader extraction for startup/expired-source floors and topology detection; reuses existing native API with DPI restoration and bounded errors. Actual-presenter handoff/regrab and full regressions still pending.

## Selected baseline: ordinary rump pull — 2026-09-07

- [x] User prefers the saved ordinary rump-pull build and explicitly authorizes commit/push on current `feature/dororong-m1-expression-animation`; no PR/merge or runtime replacement requested. Authority: `cheek-hit-routing-20260907-attempt-1/source-after.json`,254 exact src/test/tool files; executable App SHA256 FDD595114C7E044877CF259DCBBF8B0B9FC63942F19C2FC5979597B3030137AC.
- [x] Preserved complete274-file hanging source snapshot; moved20 untracked hanging-only product/test files into recoverable `artifacts/repro/plain-rump-checkpoint-20260907/archived-hanging-only`; restored6 shared files to exact plain authority. Both saved runtime ZIPs and running instances remain untouched. This deliberately selects historical plain behavior, not a new hybrid with later rump upgrades.
- [x] Fresh App354+Core96 tests and four script checks exit0; rebuilt/tested App DLL is exact to saved ordinary build. Staged scope contains145 files: selected source/tests/assets/tools plus checkpoint/spec/ledger, no hanging code. The frozen ApprovedSurroundingPullRenderer test reference retains its pre-existing EOF blank line; default staged whitespace check reports only that line, check with blank-at-eof disabled exits0. User-authorized delivery: commit this checkpoint and fast-forward push current branch to origin, including36 preceding unpushed local commits. No force push/PR/merge. Delivery commit/ref verification is recorded in local `plain-rump-checkpoint-20260907/delivery.json` and Git history.

## Save two rump runtime versions — 2026-09-07

- [x] User requested separate saved pull-only and dangling versions. Archived existing runtimes without new implementation: pre-hanging `cheek-hit-routing-20260907-attempt-1/runtime` and latest `rump-visible-fall-20260907-attempt-1/runtime`. Saved folders + individual ZIPs under `C:/Users/tjdwo/Downloads/doro/saved-versions-20260907`; manifest and Korean README disclose historical-versus-latest differences (not a same-build toggle).
- [x] Both8-file copies and ZIP entry bytes match authority SHA256; App hashes FDD595114C7E044877CF259DCBBF8B0B9FC63942F19C2FC5979597B3030137AC / E3E8A47C27F1D60DC667B53BB3D32A135C9C68CBE832DFCE50484B82540DD1D1.274 source/test/tool files, HEAD/index unchanged; live PID37592 path/start unchanged. No new build/test/launch/commit/push. Packaging script: artifacts/repro/rump-two-saved-versions-20260907/Save-Versions.ps1. STOP.

## Visible rump fall and lower render cost — 2026-09-07

- [x] User approves fixing world-space visible fall (pose restoration currently cancels36DIP container drop) and reducing per-frame deformation cost. Preserve art, stretch/gravity shape/rest/sway; no commit/push. Evidence: artifacts/repro/rump-visible-fall-20260907-attempt-1.
- [x] Frozen269 source/test/tool files and721 preceding evidence files. RED world-space fall reproduced for both facings; corrected release reference compensation. Exact renderer parity retained with conservative bounds and up-to-four synchronous row workers. Targeted19 tests passed; measured components22.86→11.06ms median. Extended final-idle regression exposed13px mirrored-facing jump; preserving rump facing into idle. Full final rerun pending.
- [x] Read-only review found no remaining blocker after clamp/facing fixes. Final App438+Core96 and four script checks exit0. Renderer parity remains exact in tested poses; final component median22.845→11.993ms, p9528.601→27.066ms (tail remains variable; live cadence UNVERIFIED). Both facings fall35.740px by224ms, settle37.85px below release; final X drift<.46px in synthetic raster+window test. Preserved262/269 baseline files (7 scoped changes),721 prior evidence files, all assets/index/HEAD. Desktop replaced PID50076→37592 at22:40:09+09:00; loaded App E3E8A47C27F1D60DC667B53BB3D32A135C9C68CBE832DFCE50484B82540DD1D1 matches tested/published. No browser change/commit/push. Physical mouse/compositor cadence/user acceptance UNVERIFIED. STOP for user trial; evidence.json and REPORT.md.

## Rump live feedback: invisible drop and jitter — 2026-09-07

- [x] User rejects live behavior: drop not visible, movement jitters. Verified exact latest PID50076 and loaded DLL identity. Product unchanged during diagnosis. Existing final normal proof combined with its release curve shows alpha centroid207.645→209.601 (only1.956px down) and upward motion mid-release: pose return offsets36DIP window fall. Previous separate window/pose tests missed world-space visual trajectory. Details: artifacts/repro/rump-live-feedback-20260907-diagnosis/DIAGNOSIS.md.
- [ ] Jitter cause needs live/update-cadence measurement; synchronous offscreen renderer median18.11/p9520.74ms exceeds16ms timer budget and is a supported suspect, not proven sole cause. Proposed visible-anchor fall compensation and rendering-cost reduction await user direction. No runtime change/commit/push.

## Rump stronger stretch, gravity and drop landing — 2026-09-07

- [x] User approves rump-only roughly1.5x visible stretch, existing head-style fall/squash/rebound on carried release, clearly stronger screen-down three-paw gravity. Preserve rump-up rest, head/cheek/other regions/art; no commit/push. Evidence: artifacts/repro/rump-stretch-landing-20260907-attempt-1.
- [x] Frozen264 source/test/tool and546 prior evidence files. RED5 new behaviors, partial-release and delayed-button-up RED retained. Rump-only flow velocity/envelope1.5x; gravity targets8/10/9 with original lag, widened pins and16-step forward/reverse velocity integration after one-step fold failures. First release preserves displayed position/pose; full/partial drop uses unchanged head220msfall+220mssquash/rebound, zero press nofall, pose returns200ms, last overlay retained to440ms. Actual PetLoop capture-release continuity/bottom clamp/cancel tests pass.
- [x] Final serial App428+Core96=524pass; four script checks exit0, render219351 assertions and13approved assets exact. Read-only reviewer issues resolved; new-strength sampled halfpixel/fullangle/nonuniform inverse and viewport coverage pass. Final normal/mirrored offscreen static/sequence sheets inspected; actual live three-paw naturalness/feel UNVERIFIED, prior strong primary contour roughness not claimed fixed. Offscreen median≈18ms/p95≈20.8ms, not sustained60fps evidence.
- [x]257/264baseline files exact (sixproduct andoneprior test intentionally changed), five new test files;546prior evidence exact; HEAD/index unchanged. Separate publication matches tested/loaded App/Core; exact oldPID11748 replaced by50076, responding, oldruntime retained. Browser/art unchanged; no commit/push/PR. REPORT/proof-final/evidence at attempt root. STOP for desktop user trial.

## Rump-up / head-down carried orientation — 2026-09-07

- [x] User approves changing only rump carry's resting orientation: rump above/head below, smooth entry, speed-driven sway around that hanging basis, existing grab/three-paw gravity/release retained. No art/hit-map/other gesture changes, commit/push. Evidence: artifacts/repro/rump-head-down-20260907-attempt-1.
- [x] Frozen261 source/test/tool files and540 prior delivery files. Actual carried pose RED2/2 showed head above rump. Rest axis uses captured canonical head reference34,48 toward held rump;320ms smooth entry and200ms frozen release, existing physical-speed relative sway preserved. Reviewer identified newly inverted paw-field fold; reproduced RED, widened source-up transition and damped inverse64/.65. Final geometry includes mirror/full-angle/half-pixel grids, nonuniform lag and partial entry, early release; full-angle boundary-grab viewport and offscreen raster proof pass.
- [x] Final serial App404+Core96=500pass; four script checks exit0 including219351 render assertions and13approved assets exact.257/261baseline files exact: only3product files and1existing test changed,3new test files.540prior evidence exact; HEAD/index unchanged. Read-only reviewer no remaining finding after final fix. Final normal/mirrored pose and controller sequence sheets inspected; art unchanged, naturalness/physical mouse acceptance UNVERIFIED. Offscreen median14.9–15.3ms, p95 17.5–18.5ms; not a sustained60fps claim.
- [x] Published App/Core match tested binaries. Exact oldPID47160 replaced by11748; responding, previous runtime retained. Browser unchanged; no commit/push/PR. REPORT/proof-final/evidence under attempt root. STOP for desktop user trial.

## Rump carry swing and three-paw gravity response — 2026-09-07

- [x] User approves bounded extension: unchanged initial rump pull, speed-driven whole-character swing about the held rump when carried; three existing paws droop screen-down with delayed response and bounded reach, restore on release. Existing head/cheek/other four body-region gestures and source artwork locked. No commit/push. Evidence: artifacts/repro/rump-hang-20260907-attempt-1.
- [x] Frozen255source/test/tool files (includes initial new RED test) and609prior evidence. Real controller→Presenter5RED then5GREEN: rump carry swing about deformed grab and three lower paw soles. Existing position/carry/release reference remains exact for all5body regions. New rump-only motion reuses physical-speed spring, screen-axis gravity is inverse-mapped after rotation; 3.2/4/3.6 targets with90/120/150ms delay,200msrelease. Other targets/art/hit maps untouched.
- [x] Final serial App390+Core96=486pass; fresh renamed proof2/2; runtime/render219351/exact-art/approved13 exit0. Numeric mirrored screen gravity/root pins, release/reset/clock, sampled inverse residual <=.02px and actual boundary-grab viewport cases checked. Static/motion contact sheets inspected; no obvious new paw-root break in those samples. Reviewer no blocker after coverage additions/evidence label corrections. Curious/Startled facing inputs are not all mirrored displays. Existing primary rump contour roughness remains; offscreen timing has outliers, not a sustained60fps claim.
- [x]251/255baseline files exact; only3existing product integrations plus newly frozen test differ,2product helpers/4further tests added.609prior evidence exact; HEAD/index unchanged. Separate published tested App/Core identities checked; exact oldPID37380 stopped, newPID47160 launched/responding. Old runtime retained, browser unchanged; no commit/push/PR. REPORT/proof-final/evidence under attempt root. Physical mouse/live-feel/user acceptance UNVERIFIED; STOP for user desktop trial.

## Cheek hit-area / head-fallback routing correction — 2026-09-07

- [x] User approves red-marked selected eye/cheek/mouth area as cheek, five body regions as body, crown/upper hair only as head, no ambiguous-boundary head fallback. Reproduced real Presenter awake samples: eye24,52;mouth28,59;white under-chin23,67;excluded connector30,75 all previously Body (head-drag target). Existing narrow16,58 cheek and distal23,77 body work. No art/motion redesign, commit/push. Sleep/noncanonical wake routing remains separate and unchanged. Evidence: artifacts/repro/cheek-hit-routing-20260907-attempt-1.
- [x] Frozen250source/test/tool and619prior evidence files. Initial routing RED21/31, actual blink RED4/4; input-only polygon expands selected cheek82→487opaque pixels, nearby neutral-white body hit proxy adds82pixels without changing renderer masks. Normal canonical awake upper-only head gate replaces lower unassigned fallback. Original captures produce identical motion bytes at expanded points.
- [x] Final serial App354+Core96=450pass; runtime/render219351/exact-art/approved13 exit0. All-source-pixel, mirrors, padded scale/rotation and synthetic Presenter→PetLoop branch checks pass. Reviewer no production defect; Idle mirror coverage corrected using real completed-capture facing retention and explicit facing/matrix assertions. Old eye/mouth-prohibition tests updated to newly approved input contract.246/250baseline files unchanged: only Presenter and3tests modified;1input helper/3tests added.619prior evidence exact, art/motion/Core/viewport immutable, HEAD/index unchanged.
- [x] Separate published DLLs match tested output; exact oldPID48916 stopped, newPID37380 launched/responding with loaded App/Core identity confirmed. Browser not changed; no commit/push/PR. REPORT/proof/evidence/checksums under attempt root. Physical WPF mouse dispatch and live feel not performed/approved; STOP for user desktop trial.

## Physical cursor speed / wide head swing — 2026-09-07

- [x] User approved screen-speed-driven up to ±88 degrees, padded transparent display, unchanged grab/size/smooth sampling. Supersedes the small 144px viewport cap, not prior art or other drag behavior. Existing runtime retained until verified replacement. No commit/push. Evidence: artifacts/repro/head-speed-wide-swing-20260907-attempt-1.
- [x] Frozen245source/test/tool files and313prior evidence files. RED8 for simulation35/display19–25 cap, RED5 for physical input/viewport, RED2 for nonfinite optional physical input. Initial setup/proof assertion errors retained in TRX; opacity0 HWND initializes actual production host, gutter occupancy required across sweep rather than in both signs of off-centre grab.
- [x] Screen-horizontal raw cursor px/s ×.08, target/clamp±88; existing60ms filter/spring/release retained. Logical144 presenter centered in336 window with96-DIP gutter; grab/input/brain size mapping remains144. Actual controller→raster both facings yields500px/s≈40°,1000≈80°,1500=88°. Alpha mass within0.011%, gutter actually drawn; no outer-edge pixels in sampled proof. Final serial App314+Core96=410pass. RuntimeComposition/DirectInteractionRender219351/ExactArt/Approved13 exit0. Old small-preview speed probes adjusted for doubled gain; no art edits.
- [x] Reviewer source-only no blockers after finite-input guard.238/245existing files exact; seven scoped changes, one new runtime helper/four tests;313prior evidence exact, HEAD/index unchanged. Separate publish DLLs match tested ones; only exact oldPID48628 stopped, newPID48916 launched/responding, loaded App/Core identities checked. No commit/push/PR, old runtime preserved. REPORT/proof-final/evidence under attempt root. Physical mouse feel, OS click-through and mixed-monitor trial not performed; STOP for user live check.

## Left/right swing symmetry fix — 2026-09-07

- [x] User accepts tilt smoothing and requests only unequal left/right swing angles fixed. Confirmed symmetric speed law but asymmetric per-direction viewport cap (+23.464/-35 at crown). Evidence: artifacts/repro/swing-symmetry-20260907-attempt-1; approved smoothing, raster art/fields, input timing, grab, prior deliveries locked. No asset/XAML edits, commit/push or broad refactor.
- [x] RED12paired cases; first current-box common cap left moving6fail. Stable registration support ±4 with y>=84 pinned resolves motion-dependent asymmetry. Crown±19.696, othergrabs±22.336/24.669 stationary/moving bothfacings; smaller5/12/18degree requests unchanged. Common cap intentionally reduces larger prior tilt. Existing release/anchor/viewport/sampling checks retained;16newcases plus old focused tests pass. Actual offscreen opposite-direction proof inspected, not physical mouse approval.
- [x] Final serial App296+Core96=392pass; runtime/render219351/exactart/approved13 exit0. Reviewer no blocker.242/244existing source exact, two authorized source changes, one newtest;274prior evidence exact; HEAD/index unchanged. Separate tested runtime; exact oldPID39972 stopped, newPID48628 launched/responding. Old runtime retained. No commit/push/PR. STOP for user live trial; report/proof/identity records under attempt root.

## Tilt-only smooth sampling trial — 2026-09-07

Goal/spec: user accepts current cheek and asks to implement the proposed tilt-only smoothing for comparison. WPF display sampling only; source96px art, geometry, primary/secondary motion, capture, carry, swing angle and release/landing remain locked. Current worktree is already isolated; no new worktree, commit or push. Single task, executed inline. Evidence: artifacts/repro/tilt-sampling-20260907-attempt-1.

- [x] Freeze source and previous delivery hashes. Added HeadTiltSamplingTests real raster/cancel/release tests; observed RED6/7 with upright pass before product changes.242existing source/test/tool and111previous delivery files frozen.
- [x] Only Presenter sampling override/flag changed: final actual nonzero angle uses Linear, zero retains NearestNeighbor. Focused7/7GREEN; no geometry/XAML changes.
- [x]122same-source/same-transform A/B pairs and61-frame exact-payload APNG exported, native/4x inspected. Final serial App280+Core96=376pass, runtime/render219351/exact-art/approved13 exit0. Read-only reviewer no blocker.241/242prior source exact, only Presenter modified,2newtests;111prior evidence exact; HEAD/index unchanged. Smoother but softer sampled contours observed; physical mouse/visual acceptance unverified.
- [x] Separate runtime tested App/Core DLL identities verified; exact prior PID51684 stopped, newPID39972 launched/responding. Old runtime retained. No commit/push/PR or browser-preview change. Comparison proof and REPORT in attempt folder; STOP for user live comparison.

## Approved cheek product omission fix — 2026-09-07

- [x] User's desktop screenshot identifies the old pointed cheek. Confirmed omission: the running surrounding-motion build still calls the original CheekPullRenderer; the accepted facial/rounded/follow/outline chain remains preview-only. Apply that already accepted chain to the live captured cheek path with real elapsed time, preserve head/body integration and all primary input/carry/release rules. Freeze prior evidence and source; test real Presenter output against immutable approved PNGs before porting. Separate publish and exact-process replacement after verification; no redesign, asset edits, commit or push. Evidence: artifacts/repro/cheek-approved-product-20260907-attempt-1.
- [x] Four disconnected-product RED tests then approved chain connected; actual Presenter41/41 animation frames and steady rest/half/max match frozen accepted PNGs. First-release clock, source capture, mirror/rotation/cancel and blink variants checked. Four copied class bodies exact to approved preview. Final serial Core96+App272=368pass; runtime/render219351/exact-art/approved13 checks exit0.188/192 prior source exact with four authorized source/test changes,1554prior evidence exact; original assets/head/body/controller/carry untouched. Fresh read-only review no blocker; scratch-allocation optimization deferred. Separate runtime tested DLL identities confirmed; only exact oldPID49948 stopped and newPID51684 launched/responding. Physical mouse feel/user acceptance still UNVERIFIED. No commit/push/PR. REPORT.md and actual-presenter comparison document the correction, not an independent desktop visual approval.

## Surrounding-motion visible-result report — 2026-09-07

- [x] User reports the desktop pet still looks like the cheek-only version (explicitly not the browser preview). Rechecked single live PID49948, new runtime path and App SHA CFBB305376E7F1FD87624A913E6DFC46EC92F2EDB252AB56AE0858972067A8DF match launched build. This establishes executable identity, not successful physical drag input. Native Computer Use does not expose the pet's tool window, so no real mouse test was performed. Read-only source review confirms deliberately small secondary fields (head near cap14 ×.12, body near cap18 ×.10), exact protected head restoration during body pulls, and carried head response decaying when stopped. These can explain a weak visual difference but do not establish the user's specific failure. No product changes or restarts in this diagnostic turn; distinguish missing primary head/body drag from missing visible secondary response before choosing a fix. Prior integration's user-visible acceptance remains unverified.

## Surrounding-motion product integration — 2026-09-07

- [x] User explicitly requests applying the reviewed head/body preview (`적용해봐`). Same bounded motion design now authorized for product connection and a local runnable build, not a redesign or cheek-preview integration. Preserve original assets, primary input/carry/swing/landing laws, prior evidence and unrelated changes. No commit/push requested. Work evidence: artifacts/repro/surrounding-pull-product-20260907-attempt-1.
- [x] Product integration complete: five existing source files changed plus three secondary-motion helpers; original assets/controllers/cheek source remain exact. TDD7 connection failures then reviewer regressions fixed (carry reversal before origin, actual inverse swing/mirror, first body-release elapsed, continuous face pin). Raw clock forwarded through explicit timed Render while preserving reflected legacy overloads. Actual raster support cap preserves existing sway; optimized15body/3crown samples byte-exact to preview. Final serial Release Core96+App265=361passed, zero failures; approved13/ExactArt/RuntimeComposition/DirectInteractionRender219351 checks exit0.240body reach/192head crop samples and30actual offscreen Presenter frames checked; not physical drag approval.180/185oldsource unchanged, five authorized code changes;8994prior evidence exact, HEAD/index unchanged. Separate runtime published and verified against tested DLL; exact previous PID38412 stopped after graceful close unavailable; new pet launched (launched.json/process-health.json). Browser preview remains unchanged. No commit/push. User live-feel acceptance pending; full scope/results/limitations in attempt REPORT.md.

## Body/head surrounding-motion intake — 2026-09-07

- [x] User accepts the latest outline-only cheek preview (`좋다 이대로 가고`) and asks for surrounding-part dynamic response when pulling body/head. This accepts the preview direction, not evidence of product integration: the broader cheek/rounded-follow/outline chain remains isolated and not applied. Preserve all prior preview/checkpoint evidence.
- [x] Read existing BodyPullRenderer/Geometry/Presentation/Session, BodyRegionMap, supplied head-drag frames, head anchoring and presenter swing/landing path. Paws currently deform selected geometry with an exact foreground restore; belly/rump already have localized flow. Head pull selects the eight supplied pose frames and uses anchored whole-sprite rotation, not local surrounding-part response. Existing filters, grab/carry law, source artwork, foot registration, swing/drop/squash remain constraints.
- [x] User approved the short bounded design with `ㄱㄱ`: add weak, delayed adjacent motion around the selected pull while preserving the primary grab behavior and feature design. Separate head-first and five-body-region preview units, not a new sprite set or product replacement. New isolated attempt: artifacts/repro/surrounding-pull-20260907-attempt-1. Product source/assets/input/carry/swing/landing and prior evidence remain immutable; standalone proof compilation permitted, no product build/app replacement/commit.
- [x] Isolated head + five-body-region candidate-reviewed ready for user motion review. No new sprite art; frozen original keys/primary render plus small secondary displacement,75/120ms near/far cascade,200ms exact release. RED12missing-response checks; GREEN45/45 including64head/40body directional cases. Export RED9/15 caught release clock/metadata issues; corrected15/15. Independent source/static reviewer closes both; faint MiddlePaw alpha24 texel is inherited and dispersed to alpha1–14, not newly opaque. Parent live browser found/fixed first-rAF negative elapsed crash and narrow-panel canvas overflow; actual replay/control selection and339px no-overflow verified. Final1481/1481fresh exports exact,912embedded PNGs/456pairs/18nearestpairs/532APNG payloads/76body strips exact;8prepared keys match prior runtime proof.185source/test/tool,798prior evidence,21frozen dependency/input identities unchanged; HEAD/index unchanged. Product/app/assets/carry/swing/landing untouched, no product build/launch/commit. Reports/audits in attempt folder. Crown representative preview only, not arbitrary grab/mirrored/runtime/performance validation; naturalness/user acceptance UNVERIFIED. STOP for user visual review before integration.

## Rounded-cheek outline-only correction intake — 2026-09-07

- [x] User accepts rounded-tip/opposite-eye/hair-follow motion and requests only the broken stretched outline be fixed. Read current renderer and inspect approved max/half4×. Max native rows57–58 have no dark opaque ink at the tip under the diagnostic luma<170/alpha>=128 rule: x2 is partially transparent, adjacent interior remains light. RoundTip centres ink on the coverage boundary and clips/mixes it there; subsequent hair inverse sampling can further soften that ink. This explains the missing visible stroke without requiring a shape redesign.185source/test/tool files verified unchanged; HEAD778ffa2896bd0cea7902595802fea0e808d62ca3.
- [x] Bounded proposal sent: preserve approved alpha/silhouette, eye/mouth/hair motion and timing; repair only RGB ink in a narrow interior boundary corridor after deformation. Compare all approved frame alpha planes/protected pixels and rendered-ink continuity; prior candidate/checkpoint immutable; isolated preview only, no product apply or commit.
- [x] User explicitly confirmed: `외곽선만 이 방식으로 수정`. New isolated work authorized in artifacts/repro/cheek-round-outline-20260907-attempt-1; actual alpha and animation shape remain locked, RGB-only narrow boundary correction. Product integration and new commit remain excluded.
- [x] New isolated candidate-reviewed delivered: RGB-only post-warp interior stroke, source ink(95,61,72),0.60px inward centre, approved alpha/geometry/motion retained. RED2max-ink failures and58/243broken baseline poses; GREEN29/29,0breaks/alpha/nonoutline differences in405poses and41actual motion frames. Inherited renderer only exposes Edge; motion and other dependencies unchanged. Final97/97re-export,5nearestpairs,82embeddedPNGs/APNG41payloads exact;41prior-approved frame alpha/nonoutline planes and complete timing JSON exact.185source/test/tool,654prior rounded evidence,79listed checkpoint hashes unchanged; live logs/bin/obj excluded. Read-only reviewer no correction/preview blocker; their copied-audit warning closed by parent retargeting before execution, verification exit0. HEAD778ffa2/index unchanged; product/build/launch/newcommit absent. REPORT/review/evidence in new attempt. STOP for user outline review before product integration.

## Accepted facial-cheek checkpoint and next polish intake — 2026-09-07

- [x] User likes the current preview and explicitly requests committing it before rounding the cheek tip and adding opposite-eye/hair follow. Saved self-contained accepted preview checkpoint at artifacts/checkpoints/cheek-face-20260907-approved. Commit778ffa2896bd0cea7902595802fea0e808d62ca3 contains only80checkpoint files; no accumulated product changes included. Copied original renderer/input dependencies; standalone replay16/16checks and69/69outputs exact. Original delivery ZIP SHA FAF0DFB6CC3475D808EA3BDC6F3A61AF1D3888E067E9BF3557D724AE0D81EB1F retained.185source/test/tool and1283prior carry evidence files unchanged; empty index after commit; no push or product application. Checkpoint -text attributes preserve evidence bytes; initial staging normalization mismatch caught and corrected in index only. Staged whitespace check explicitly treats retained CRLF as line endings; no product EOL edits.
- [x] Read existing FacialCheekRenderer/CheekPullRenderer for bounded follow-up. Proposed: retain reach, round the tip using a continuous contour, opposite eye follows more weakly than pulled side, nearby bangs/side hair follow with still smaller delayed motion. Preserve feature design, rose/ribbon/body, neutral restoration and existing carry/input behavior; independent new preview only. Current tip-envelope equality and opposite-eye/hair locks would intentionally change; replacement checks must disclose those changed criteria, not retain misleading old claims.
- [x] User approved the short motion design with `ㄱㄱㄱ`. New isolated work authorized in artifacts/repro/cheek-round-follow-20260907-attempt-1; checkpoint preserved, product integration still excluded. Test-first changes explicitly replace old fixed-envelope/opposite-eye/hair criteria with rounded-tip and weaker delayed follow checks.
- [x] Isolated candidate-reviewed ready for user preview. Rounded tip retains reach, near-tip native section3→5pixels; opposite iris max(-.60,+.65), hair max(-.80,+.35),55/90ms follow and220msrelease. InitialRED4new-behavior failures; follow/mouthRED5; v3near-eye1pixel+iris half-LSB tie failures retained and corrected. Final23/23checks; near-eye87/mouth retained,48iris-interior bilinear samples exact; protected81strength sweep. Reviewer: no open preview blocker after active-cancel precondition/timeline correction; only static/code inspection, no product trial or naturalness approval. Re-export97/97,nearest5pairs,embedded82PNGs,APNG41frame payloads exact.185source/test/tool,781prior face evidence and79listed checkpoint hashes unchanged; prior live logs/bin/obj explicitly excluded. HEAD778ffa2/index unchanged, no product source/assets/build/launch/newcommit. REPORT/review/evidence under new attempt. STOP for user visual review; product integration remains gated.

## Broader cheek-side facial deformation intake — 2026-09-07

User asks for cheek-side face to stretch dynamically with the cheek, not just the cheek tip. Bounded visual extension of the existing captured-cheek renderer. User approved one-side isolated proof with `ㄱㄱ`; no product integration or app replacement. Latest cheek-carry185source hashes verified unchanged; same HEAD29750fe74967e611e267fa6cd3367a5928902b4c/feature branch, empty index. New artifact-only work at artifacts/repro/cheek-face-20260907-attempt-1; source baseline and prior carry evidence frozen.

- [x] Read current renderer/capture/carry and inspect the accepted maximum-pull image. Current renderer works on12narrow rows y53..64 with fixed per-row inner roots, preserving eyes/mouth; that local support limits deformation propagation into the rest of the face.
- [x] Prepare bounded proposal: broaden selected-side displacement through cheek flesh, under-eye and mouth-adjacent skin with a smooth falloff toward the face center. Eye/mouth shapes should remain recognizable and unscaled; slight coordinated feature translation would intentionally relax the earlier exact stationary eye/mouth lock and requires user approval. Hair/rose/ribbon, opposite side and body remain protected; no global head scaling/redraw. Retain current signed horizontal input, carry gate/following and220ms restoration. First create one-side isolated animated proof from the actual renderer, with neutral/max/release native comparisons and displacement/locked-region evidence; do not claim fidelity/naturalness from hashes alone. Product integration waits for visual review. Detailed editable masks/feature displacement are not yet authored or approved.
- [x] User approved this visual scope, including small eye/mouth follow motion rather than the old fully stationary lock. Current app/carry/artwork untouched; implement isolated C# renderer proof, not a new generated sprite or Creative Production board.
- [x] Isolated one-side proof delivered for user preview only. Eye87/mouth45 selected pixels share unscaled max(-1,+2) transfer; broadened interior with accepted exterior preserved. RED baseline4 failures, v1 feature collision, v3 contour83 differences and later bang-edge196 differences retained; shared motion, geometric collar and vertical-first path resolve the sampled regressions. Final16/16 checks;81strength samples; neutral/release/cancel exact. Authored feature masks/reconstructed vacated colours disclosed; intermediate softness and angular tip remain, no naturalness claim. Read-only reviewer: no open preview blocker; no product/live trial. Final re-export68files exact vs v5 except annotation documentation; nearest11pairs/embedded41PNGs/APNG41compressed frames exact.185source/test/tool and1283prior carry evidence files unchanged; HEAD/index unchanged; diff-check exit0. REPORT/review/evidence and candidate-reviewed/playback.html under new attempt; current app untouched, no product build/integration/commit/push/PR. STOP for user visual direction/strength review.

## Cheek stretch-to-carry intake — 2026-09-07

User requests that stretching the cheek should also carry the pet like the legs. Bounded extension of existing captured-cheek/controller and five-body local-position runtime flow; design approved with `ㄱㄱ` and implemented for local trial below. Preflight latest delivered182source hashes verified unchanged; same feature worktree/HEAD29750fe74967e611e267fa6cd3367a5928902b4c and empty index.

- [x] Read current BodyPullSession, captured-cheek input/render/release and PetLoop local-position handoff. Body uses55ms two-stage filtering,0.8source-pixel tremor deadband,18source-pixel carry entry (front-paw reach cap may enter earlier),75ms carry following and work-area reconciliation. Captured cheek currently projects total press delta, clamps-10..20DIP and cannot supply window position.
- [x] Prepare short in-chat proposal: selected implemented cheek only; initially stretch with the accepted raster, then latch carry after20DIP filtered outward pull. Use leg-like damping/carry following; compute remaining cheek pull from pointer relative to the moved window rather than freezing the entry shape. Once carried, whole-window motion can follow any direction; cheek raster stays signed horizontal with gain0.5/-10..20 limits (no new vertical cheek art). Avoid transition/reversal jumps, hold the original source/facing, clamp to work area; button-up stops translation at the current position and restores cheek over220ms, with no head fall. Opposite cheek, head and five-body behaviors unchanged. Proposed scoped session/controller/snapshot/PetLoop changes through the existing local-position seam, no Core/host/public contract or renderer redesign; preserve legacy standalone capture API where feasible. Test entry/hold/reversal/both facings/release/edge clamp/cancel/fault/dispose plus full serial regressions and actual Presenter proof.
- [x] User approved the bounded design with `ㄱㄱ`. TDD implementation now authorized in the existing worktree; new evidence under artifacts/repro/cheek-carry-20260907-attempt-1. Existing182source baseline and previous cheek-product evidence frozen before edits. No new artwork or opposite-cheek behavior.
- [x] TDD actual-loop RED10/10 then GREEN10; exact20boundary RED1/4passing controls, sub-.001DIP settled-target snap resolves asymptotic threshold. New CheekCarrySession and scoped Capture/Controller/PetLoop handoff only; no snapshot/Core/host/renderer redesign.55ms two-stage input/0.8DIP deadband preserving full accepted target,20DIP gate,75ms residual follower with signed overflow and work-area reconciliation. Release starts from displayed position/pull,220ms once. One historical instant20/no-carry test migrated to damped subthreshold12 behavior; first full failure preserved. Final full serial Release Core96/App245=341passed; runtime/render219351/approved13 scripts exit0. Fresh read-only reviewer: no code blocker, two coverage notes closed after tremor and3rotated/scaled cases; no reviewer test/live execution.
- [x] Actual PetLoop→Controller→Presenter synthetic proof-v2:168states with actual visible captured facings checked;0protected pixel differences and0core/window mismatches; release position/pull preserved. Parent inspected both orientations and full scene. Proof-v1 invalid as mirrored evidence because Idle enforces rest-facing; discovered in sheet inspection, preserved, corrected harness only to select Walk before capture and assert actual facing/carry. Baseline182:4scoped existing changes/178unchanged,3additions=185current;584prior evidence files exact. Published App/Core match tested DLLs;341proof files copied exactly. ZIP360files/359listed hashes match, SHA BCF4B5D56A382EA9E667B189CD3D79C094E057C39E0B98CE14DC3CA40787F002. New trialPID38412 InputIdle=True/Responding=True; only exact-path priorPID38564 stopped after new liveness. REPORT/review/DELIVERY/package/final-audit in new attempt. Source artwork/XAML/EOL/HEAD/index preserved, diff-check exit0; no commit/push/PR. User runtime acceptance UNVERIFIED; STOP for local trial feedback.

## One-side cheek product connection — approved 2026-09-07

User accepted the outline preview with `이대로 적용`, then explicitly approved the bounded input/capture design with `ㄱㄱ`. Use the exact approved raster algorithm; one anatomical RightCheek (screen-left in unmirrored source) only, including mirrored presentation. Freeze pressed awake source and actual affine transform; selected skin hit region excludes eye/mouth/hair, old selected-side broad rectangle falls back to ordinary body/head route. Opposite LeftCheek legacy behavior unchanged; sleep/noncanonical selected-side clicks use existing body wake route, never canonical deformation on displaced eyes. Horizontal outward/inward only, gain0.5/max20DIP and220ms restore; no new vertical deformation or window carry. Preserve source assets, head80DIP/swing/drop, five-body, legacy constructors and prior artifacts. No XAML/EOL cleanup or commit/push/PR. New artifacts/repro/cheek-product-20260907-attempt-1 only; single ledger, existing linked worktree.

- [x] TDD renderer/hit RED15fail/1pass then GREEN16; captured connection RED6 then GREEN6; loop handoff RED5 then integrated27passed. Exact approved raster connected through immutable clicked source/affine capture, precise skin classification, signed horizontal pull,220ms release and cancellation/fault/disposal cleanup. Opposite legacy behavior retained. Two obsolete selected-side descriptor expectations migrated to approved skin/wake routing.
- [x] Full final serial Release Core96/App226=322passed; runtime/render219351/approved13 checks exit0. Actual Controller-to-Presenter synthetic proof82states/bothfacings:0approved source mismatches; parent opened rest/max/release images. Fresh scoped read-only reviewer found no actionable issue, no reviewer tests or live trial. Baseline166:6existing scoped changes/160unchanged,3newproduction+2newtests+11fixtures=182current;1045prior evidence hashes exact. Separate published App/Core match tested DLLs;166proof files copied exactly. ZIP185files/184listed checksums exact, SHA3116535961B99FA82A8F5E62DAC71BECDAF39D58A65ADE30251584D4D6DF4724. New trialPID38564 InputIdle=True/Responding=True; only exact-path oldPID41444 stopped after new liveness. REPORT.md, independent-review.md, DELIVERY.json, package-audit.json and final-delivery-audit.json under new task artifact. HEAD/index unchanged, diff-check exit0; no commit/push/PR or XAML/EOL cleanup. Approved angular/integer-stepped raster preserved; no new visual quality claim. User runtime acceptance UNVERIFIED; STOP for local trial feedback.

Preflight:166source/test/tool baseline files exact; HEAD29750fe74967e611e267fa6cd3367a5928902b4c. Two baseline default-parallel runs each failed one different old test in WPF PackagePart.CleanUpRequestedStreamsList/List.RemoveAt; unrelated race is not fixed by this task. Serial xUnit override is being used to establish the baseline and final checks; original failure logs retained.

## Cheek follow-up intake — 2026-09-07

### Outline-only follow-up — approved 2026-09-07

User likes the first-side direction but reports broken outline; `ㄱㄱ` approves the proposed bounded outline-only correction. Root cause verified read-only: row-wise copied contour bands have 0/1/4/8 disconnected adjacent-row pairs at 0/4/10/20 DIP. Existing alpha-component tests do not detect broken ink. Preserve accepted envelope, pull law, source pixels outside selected cheek, eyes/mouth/roots, and exact release/cancel. Work only in new artifacts/repro/cheek-outline-20260907-attempt-1 proof source/output; prior proof and product remain untouched. TDD adds a real rendered-ink connectivity regression before a continuous constant-width local stroke correction; explicitly disclose any new blended edge colors. No opposite cheek, live input/product integration, app launch, commit/push/PR. Approval is for this correction, not final visual acceptance.

- [x] Isolated correction and one-side preview handoff complete. RED2ink failures;1.3px attempt left4/302disconnected sampled states,1.5px local coverage correction closes them. Reviewer found one missed quantized state; expanded frozen-fixture boundary/interval sweep without changing renderer:22/22checks,581inputs/55contour vectors,0measured disconnections/protected/root changes. Actual prior41PNG alpha planes identical; neutral/release/cancel exact. Final replay73/73, nearest13/13, embedded41/41exact.166source and615prior files preserved;2running-server logs explicitly excluded after hash-lock errors. Fresh read-only reviewer found no open issue for user-preview delivery; their initial PowerShell diagnostic false negative was retracted after actual rendered bytes and independent JS graph confirmed33/33connected pixels. Visible diagonal gaps filled, but original band variation/angular shape/integer stepping remain; no uniform-perceived-width, naturalness or product approval. New local preview only; product untouched. Report/review/evidence under new task directory. STOP for user review, no opposite cheek or product integration.

User confirmed longer landing `ㅇㅇ 잘 됐다` and requested proper cheek integration. Current166source hashes still match that delivery. Read-only intake: existing descriptor/controller handles cheek presses/strength/release, but Presenter has no cheek deformation route. Prior isolated LocalCheekRenderer writes only inside enclosed skin rows; lower face outline and all complement pixels stay locked. Saved StageA result19/20, newExteriorPixels0; parent reopened its mask overlay and canonical source. This explains why reconnecting the old prototype alone cannot supply a visible cheek pull.

Bounded next unit proposed: revise the existing one-side isolated proof before any product integration. Need explicit art-direction approval for moving selected cheek outline and permitting temporary cheek-over-side-hair occlusion (source hair design unchanged, but visible hair pixels may be covered). No final masks, new proof, renderer edits or live cheek route authorized by this intake record. Eyes/mouth/opposite cheek/body stay protected; head pull/landing/five-body untouched. Existing failed evidence remains preserved. Brainstorming design approval pending; implementation not started.

- [x] Inspect current cheek input/render flow, prior failure, actual mask/source and delivery baseline.
- [x] User approved revised selected-cheek outline motion and temporary side-hair occlusion with `ㄱㄱ`. Only the screen-left isolated proof starts; no opposite side/product integration yet. Source166baseline unchanged. New code/checks/output under artifacts/repro/cheek-occlusion-20260907-attempt-1; previous tool/evidence and live landing trial remain unchanged. TDD first records missing exterior extension against the old enclosed-skin renderer, then implements destination expansion with original texel sampling. Exact protected eyes/mouth/opposite cheek/body and unaffected hair; selected side-hair may be covered but not redesigned. Native/nearest evidence and source/destination masks are mandatory; no claim of naturalness from hashes alone.
- [x] First-side isolated proof produced and inspected by parent and fresh read-only reviewer; user-review gate remains OPEN. Old enclosed renderer RED3/15 (no exterior/contour/occlusion); new sampler V1mechanical15/15 but onepixel tip observed. Added adjacency RED1 then one-row envelope correction yields28exterior/146changedpixels,16/16checks. Reviewer still sees an angular tapered wedge at max/early release: unresolved visual issue, NOT naturalness PASS. Evidence label narrowed and all12literal roots tested after reviewer feedback; no artwork change for this evidence fix. candidate-reviewed images/playback equal V2; independent final replay70/70, nearest12/12 and embedded PNG41/41exact. Source166 and prior62files unchanged. Product/opposite side/live press affine/vertical NOT IMPLEMENTED. Nominal1:1scale and0.5gain are isolated proof assumptions; current horizontal/inward/220msrelease only. Report/annotations/checks in artifacts/repro/cheek-occlusion-20260907-attempt-1. Localhost preview helper only; accepted petPID41444 untouched. STOP for user's first-side direction/shape review, no product integration.

## Longer head-release drop — 2026-09-07

User feedback on preceding trial: `귀엽다`, requests more fall. Approved bounded tuning:24→36DIP maximum distance,180→220ms fall; unchanged40ms squash/80ms rebound/100ms recovery, total440ms. Keep old180ms body/anchor return, source art, held swing and all other interactions. Existing bottom clamp and partial scaling retained.166/166 prior source hashes match; same branch/HEAD, empty index. Evidence artifacts/repro/head-landing-longer-20260907-attempt-1. No new design file, commit/push/PR or XAML/EOL change.

- [x] Updated real-loop expectations first: RED10failed/27passed against old behavior; production change only HeadLandingMotion, GREEN37/37. Contact-relative40/80/100ms impact stages preserved; total440ms and400ms-still-active checked.
- [x] Full Release Core96/App197=293passed; RuntimeComposition, DirectInteractionRender219351assertions and approved13asset identity exit0. Parent viewed real-PetLoop366sample/6case proof: exact button-up, sole drift0, no sampled clipping.16native held/contact/squash/rebound comparisons equal prior pixels at shifted times. Fresh scoped read-only code reviewer found no issues; no reviewer tests or visual inspection.166baseline:4scoped files changed(1production,3tests),162unchanged;4749prior artifacts exact. Published735proof files match tested; App/Core parity and8embedded/16legacy outputs verified. Report/evidence in this new attempt directory.
- [x] New trialPID41444 Responding=True/InputIdle=True; only exact-path priorPID41156 stopped after new liveness. DELIVERY.json records hashes/path/handoff. HEAD/index unchanged, no commit/push/PR. New tuning USER VISUAL ACCEPTANCE UNVERIFIED; STOP for feedback.

Subsequent user feedback: `ㅇㅇ 잘 됐다` — longer landing accepted for this tuning; now move to cheek intake. Prior delivery/evidence records remain immutable historical observations.

## Short head-release drop and foot-anchored landing squash — 2026-09-07

**Approved bounded design:** Drop near the release location, not to desktop bottom; brief foot-anchored squash, one small elastic rebound, then rest. Initial tuning:24DIP maximum drop over180ms, scaled by displayed stretch for partial releases;40ms compression,80ms rebound,100ms recovery(total400ms). Limit drop to available work-area space and reduce impact when travel is limited. No new artwork; temporary whole-sprite scale only at landing. Preserve80DIP pull law, held swing/anchor, five-body behavior and historical inputs. New release behavior supersedes runtime head's180ms-only return; standalone legacy/reflection controller behavior remains available.

**Implementation scope:** PetLoop already supports brain-authoritative LocalInteractionPosition. Controller owns optional head-landing snapshot, initialized only after real release clears DRAGGED; before later brain ticks, provide time-sampled downward position through that existing seam. No new core/host API. Controller settle stays active through400ms only when landing is enabled; it clears capture immediately. Presenter retains existing180ms return/swing correction, then scales around source sole edge(48,87) at landing. Cancel/fault/dispose must clear landing without jump; final position persists in the brain. No XAML/EOL edits, source-art edits, commits/push/PR/reset/checkout/clean. Baseline164 hashes match, HEAD29750fe74967e611e267fa6cd3367a5928902b4c. New evidence artifacts/repro/head-landing-20260907-attempt-1.

- [x] Real-loop/Presenter RED6missing-behavior failures/2preservation passes, initial GREEN8/8. Added fault/dispose/large-tick tests and mirrored final-idle-facing RED1fail/1pass found during parent actual-code visual inspection; corrected optional landing facing latch, GREEN13/13. Old failed logs/proof preserved.
- [x] Scoped optional runtime landing implemented; legacy standalone controller180ms retained. Superseded runtime position/duration assertions migrated explicitly. Final Core96/App197=293passed; RuntimeComposition explicit Release, DirectInteractionRender219351assertions and approved13assets script exit0. Contact fixture adjusted from.411(one TimeSpan tick short) to.412 seconds. No source-art/Core/host/XAML edit.
- [x] Same read-only reviewer initial/facing-follow-up: no actionable findings, no independent tests/visual trial. Parent actual-PetLoop336samples/6cases: button-up exact, landing sole drift0, no sampled clipping, left-facing final idle preserved. Published675files match tested proof; App/Core parity and8embedded assets/16legacy outputs verified. Body90PNGs and held-swing2491PNGs+HTML unchanged. Source baseline164:6existingchanges,158unchanged,2new;166frozencurrent; older1684/separate760 evidence exact. New trialPID41156 Responding=True/InputIdle=True; only exact-path prior assistant-owned PID34904 stopped after liveness. Details artifacts/repro/head-landing-20260907-attempt-1/REPORT.md and DELIVERY.json. No commit/push/PR. Existing settle gate ignores presses for up to400ms; cancellation then regrab tested. USER VISUAL ACCEPTANCE UNVERIFIED; STOP for trial feedback.

## Speed-driven hanging head swing — 2026-09-07

**Approved bounded design:** After full80DIP extension, rotate the existing whole sprite about the captured head point. Signed horizontal pointer speed drives lag in the opposite direction; stopping/reversal retains damped angular inertia. Filter tremor, cap excessive rotation (initial35degrees, further constrained if the144px viewport requires it). No new artwork, head/body split, rescale, source edits, altered80DIP law, five-body behavior or window-follow timing. Existing180ms release eases held angle to zero with the anchor. User liked the preceding trial; new swing still needs user trial.

**Implementation boundary:** Existing controller Advance already receives real elapsed time and pointer samples. Add an isolated head-swing session and default-zero snapshot angle; leave seven-argument constructor and PetLoop/host contracts intact. Presenter rotates first, then reuses head-point registration. Invalid samples/gaps must not manufacture velocity; cancel/regrab resets. Retain normal sampling restore. Scoped files: controller, snapshot, presenter code-behind, anchoring helper; new swing session and tests. No XAML/EOL or prior artifact changes, stage/commit/push/PR/reset/checkout/clean. Baseline162 source hashes match previous delivery; HEAD29750fe74967e611e267fa6cd3367a5928902b4c. New evidence only artifacts/repro/head-swing-20260907-attempt-1.

- [x] TDD: initial test fixture PointD.Length compile error corrected;13 real controller→Presenter tests RED because rotation remained0. GREEN20 including7 previous anchor tests. Real elapsed-time horizontal speed→filtered target angle plus damped inertia; reset/regrab/gaps/invalid handling.35degree simulation cap and tighter per-pose viewport cap retain head point/source scale.
- [x] Independent reviewer found saturated-release overshoot: visible23.464→24.428degrees after16ms. Both mirrored capped cases reproduced RED(after fixture direction correction). Presenter now latches actual displayed angle at release, eases monotonically to0 and clears fields on new/non-Body interaction. Same reviewer closed Important finding, no further actionable issues; no reviewer test/visual execution. Focused22passed. Full final Core96/App184=280passed on identical rerun after historical WPF PackagePart/List.RemoveAt resource-loading race; race NOT FIXED. Initial RuntimeComposition command accidentally used stale Debug output, corrected explicitRelease passed without product changes. Final render219351/runtime/approved13 checks exit0.
- [x] Actual-DLL motion proof-v2:2490frames/2286held samples,125/375/1200DIP/s×bothfacings, max estimated head-coordinate error1.4785436552731435E-13DIP, held source mismatches0, foreground bounds and monotonic release checked. Parent opened speed/stop/reverse contact sheets; no anatomical or live visual approval claim. All90 previous body PNGs exact. Baseline162→4 scoped changes/158unchanged/+2new; frozen164,older1684/prior760 hashes match. XAML raw SHA unchanged. Published App/Core parity plus2493 motion-proof files exact;8 embedded PNGs/16 legacy Presenter samples verified. New separate trialPID34904 InputIdle=True/Responding=True; exact previous assistant-ownedPID26596 stopped only after new liveness. REPORT.md/independent-review.md/DELIVERY.json recorded. HEAD/index unchanged, no commit/push/PR. New swing user acceptance UNVERIFIED; STOP for user trial.

## Slower head extension and captured head anchoring — 2026-09-07

**User-approved spec:** Increase full head extension40→80DIP and keep the originally grabbed head location under the pointer through supplied8 pose changes. Existing eight image bytes, five-body controls, paw seam fix, reach/damping,180ms head release and immediate OS-deadzone window following remain unchanged. Do not redraw, resize, resample source pixels or modify XAML/EOL. Current160source hashes match seam-fix/source-after.json; existing feature worktree HEAD29750fe74967e611e267fa6cd3367a5928902b4c, no stage/commit/push/PR/reset/checkout/clean.

**Architecture/plan (writing-plans/TDD):** C#/.NET8/WPF, execute inline. One coupled input/presentation correction. Core HeadPullDistance.FullExtension=80, same OS deadzone/radial law. Capture precise clicked source point and its window-local position before pending compression; express source point relative to a rounded pink-hair centroid landmark. Landmarks are positional estimates, not anatomical/pixel-correspondence proof; independently measured canonical(35,42), supplied8[(35,41),(35,39),(36,35),(37,31),(39,29),(41,27),(43,27),(46,26)]. Cache immutable frame landmarks. Presenter translates the full selected frame so this head-relative point maps to the captured window point, mirrored using captured facing; no new art or scaling. Brain retains immediate pointer-minus-original-offset window following; work-area clamp remains authoritative. Thus source soles stay registered but their displayed location may move as necessary to hold the head. Release starts with same correction then eases it to zero over existing180ms. Capture missing => legacy/reflection previews retain zero translation. Reset/cancel clears capture; no cumulative offsets. Nearest-neighbor for anchored display avoids fractional-placement filtering; normal rendering mode restored afterward.

**Narrowed implementation boundary:** Existing Render input already carries global press and last displayed window position. Presenter can capture the precise point from its last visible transform before pending squash/first-tick overshoot; no new event/controller/snapshot/loop plumbing is needed. _headPressOrigin tracks one capture per interaction and resets on non-Body. Capture missing => existing preview behavior. This reduces scope without changing requested behavior.

**Actual files:** HeadPullDistance.cs, new Controls/HeadPullAnchoring.cs, Presenter.xaml.cs. Tests: HeadPullDistanceTests.cs, DirectInteractionControllerTests.cs, PetLoopDirectInteractionTests.cs, DororongPresenterInteractionDescriptorTests.cs half-strength distance fixture, new HeadPullAnchoringTests.cs. tools/HeadPullProof updates80DIP metadata/sample distances and initializes an actual visible pose before press. Product controller/loop/snapshot are unchanged. New proof/evidence artifacts/repro/head-anchor-20260907-attempt-1; old evidence immutable.

- [x] RED distance4fail/5pass before production edit; actual Presenter anchor7/7fail with visible point drift/no correction. Independently measured literal landmark shifts drive assertions, not helper-derived expected coordinates.
- [x] GREEN distance9/9, anchor7/7 including3points×2facings×8keys, awake transforms and within144foreground bounds; release starts from held correction and eases to zero. Core96/App169=265passed after migrating one remaining obsolete22DIP half-strength fixture. Product controller/loop/snapshot unchanged; head80DIP tests include immediate follow, stationary/reversal/latch/invalid/cancel/release. Direct render219351/runtime/approved13 exit0. Pink centroid is rounded positional registration, not exact semantic pixel identity.
- [x] Verification complete: full Release265passed, render219351/runtime/approved13 exit0; supplied8 embedded identity and16 published legacy/reflection samples verified. All90 PNGs in44-case body proof match previous seam-fix outputs. Current162/older1684/prior760 hashes match;153 previous source files unchanged,7 scoped changes and2 additions. Actual-DLL marker proof168frames/72active samples,3grabpoints×2facings; independent literal landmark-coordinate error16.486975429886307DIP before→0after. Parent opened both contact sheets; this is estimated head registration, not anatomical correspondence or user visual approval. Release and clipping tests passed. Fresh read-only reviewer found no actionable issue; alternate sleep/rotation starts not directly covered by new tests. Published App/Core DLLs match tested,338 published marker files byte-identical. New separate trial PID26596 InputIdle=True/Responding=True; only exact-path previous assistant-owned PID4776 stopped after new liveness. REPORT.md/independent-review.md/DELIVERY.json in task artifact directory. HEAD/index unchanged, no commit/push/PR; user visual acceptance UNVERIFIED. STOP for user trial.

## Paw attachment seam correction — 2026-09-07

**Goal/spec:** User authorizes the diagnosed cut-like attachment line fix. Remove procedural ink at connected proximal attachment only; retain the actual silhouette and distal folded overlap. No new artwork, shape/length/head/timing changes. C#/.NET8/WPF. Existing feature worktree HEAD29750fe74967e611e267fa6cd3367a5928902b4c;160/160 latest source hashes match. Previous trial PID40204 exact path/liveness rechecked before replacement (initial mixed-object table obscured process fields).

**Plan (writing-plans/TDD; single ledger per AGENTS):** one bounded renderer correction. Modify src/Dororong.App/Controls/BodyPullRenderer.cs and tests/Dororong.App.Tests/Controls/BodyPullRendererTests.cs. New ignored evidence only artifacts/repro/paw-seam-fix-20260907-attempt-1. Preserve all other sources, XAML raw EOL, all prior evidence, HEAD/index. No stage/commit/push/PR/reset/checkout/clean. Execute inline; separate read-only final review.

- [x] Traced torso alpha/provenance and frozen baseline. RED-v2:4 seam failures at actual dark(80,74,92,255) attachment,2 preservation checks pass. Initial exterior fixture selected a faint-alpha pixel incorrectly; corrected to inspected(24,74) before production edit; no test weakening. Trace-only patch first missed a Mode condition and a reporting command had syntax error; neither changed product.
- [x] Final-texel distal provenance plus combined torso/paw4-neighbor coverage(alpha>=128 torso) guard suppresses only proximal procedural ink. Geometry/alpha/source texels/timing unchanged. Focused29passed.44 old actual-DLL renders exact against baseline copy;44 new renders preserve alpha/protected head,138 total lightening-only pixel changes,16Belly/Rump exact. Nearby reproduced cuts:7/7/6/2 changed pixels respectively, at attachment only. Native dark/white/grass-like and8direction composites inspected by parent and fresh reviewer; no user acceptance claimed.
- [x] Full initial Core96pass/App161pass+1fail: previously observed WPF PackagePart.CleanUpRequestedStreamsList List.RemoveAt race during historical Presenter resource load. Identical command rerun without code change Core96/App162=258passed; race NOT FIXED. DirectInteractionRender219351, RuntimeComposition and approved13bothlocations exit0.158/160 before sources unchanged;160current,1684older and760prior-evidence hashes match. Fresh read-only reviewer: no actionable findings, no independent test execution or interactive trial. Published91proof files match tested outputs, App/Core DLL hashes match tested,8embedded resources/16actual Presenter head outputs unchanged. Separate trial delivery-v1/runtime launchedPID4776 Responding=True/InputIdle=True; only exact-path old assistant-owned PID40204 stopped after newprocess liveness. Final160source hashes match, diff-check exit0, HEAD/index unchanged. No commit/push/PR. User visual acceptance UNVERIFIED; STOP for user trial.

## Paw attachment seam diagnosis — 2026-09-07

**User report:** screenshot `codex-clipboard-2ad0d8a0-fe0b-452e-a93c-28682e8679dd.png` shows a cut-like line near the mirrored front-paw attachment. Diagnosis only this turn; product source/App/runtime not modified or relaunched.

**Evidence:** new isolated diagnostic harness in artifacts/repro/paw-seam-diagnosis-20260907-attempt-1. Baseline4/4 outputs byte-identical to actual published front-paw trial renderer. Tested source pulls(-8,0),(-12,0),(-16,0),(-12,5), displayed mirrored to match screenshot. Disable only procedural paw-boundary ink versus disable only closure ink; no production patch. Paw ink changes14/16/19/12 pixels respectively and removes the reproduced cut-like attachment line. At(-12,0), source-coordinate(18..21,72),(22,73) are dark Pbgra(80,74,92,255) in baseline, near-white248/249 when that ink pass is disabled. Closure-ink removal changes0/0/2/0 pixels, not the main contributor. Exact screenshot input timing unknown; nearby matching pose reproduction, not pixel-exact desktop recreation.

**Cause / proposed scope:** RenderPaw marks boundaries from the moved-paw coverage alone, without distinguishing a connected attachment inside the combined torso+paw silhouette. Temporary mesh/attachment boundary receives exterior ink and reads as a cut. Fix should distinguish the connected root from true silhouette and intended folded overlap, not globally remove all ink. Broad no-paw-ink diagnostic is NOT a proposed production fix. Head/source/length cap need not change.160/160 prior product/source hashes verified unchanged. No tests/build of product, commit/push/PR or new trial. Diagnosis harness only was built/run. A PowerShell reporting command initially had a pipeline syntax error; corrected for reading existing evidence, with no product effect.

## Shorten the pictured front paw — 2026-09-06

**Goal/spec:** User screenshot identifies the screen-left front paw as too long. Scope announced: front paw maximum root-to-tip reach32→22 source pixels (31.25% shorter cap); other four body regions, head8, edge cleanup and artwork unchanged. Keep55ms filter,0.8 tremor rejection,75ms carry following,200ms release,360-degree direction. If the shorter front-paw cap is reached before the normal18px carry threshold, hand excess distance to the window and latch Carried; do not let an uncapped pulling frame overshoot or detach the grab by shrinking only the renderer.

**Plan (writing-plans/TDD):** one bounded C# session-policy adjustment in existing feature worktree. Modify src/Dororong.App/Interaction/BodyPullSession.cs and tests/Dororong.App.Tests/Interaction/BodyPullTests.cs only. Renderer/geometry, Controller/Core/PetLoop/XAML and original files immutable.160source preflight hashes match prior edge-cleanup delivery. New ignored evidence artifacts/repro/front-paw-length-20260906-attempt-1; prior artifacts preserved. HEAD29750fe74967e611e267fa6cd3367a5928902b4c/index fixed, no commit/push/PR/reset/checkout/clean.

- [x] RED9/9failed at old reach, including8directions and early-cap affine accounting. Focused GREEN30passed. Existing small-pull/filter/rate/reversal/release fixtures retained. Actual old-DLL before-proof-v3 recorded; initial harness friend/URI setup failures preserved and corrected only in harness.
- [x] Front22/other32 cap in SyncPull; excess handed to window and front carry latches even before18 threshold. Full initial run had1 historical WPF PackagePart.CleanUpRequestedStreamsList race; identical rerun without code changes96Core+156App=252passed. Race observed, NOT FIXED. Runtime/render219351/approved13/original40 geometry parity passed. Identical-input before/after proof: other32 trajectories andPNGhashes exact; front8 peaks<=22. Leftward sample peak23.23→20.96; cap32→22 does not mean every visible pose shrinks31%. PublishedApp/Core hashes match tested;42published proof files match tested proof and8embedded/16corrected-head outputs unchanged. Parent inspected both direction strips.
- [x] Fresh read-only review: no actionable findings;160/160 after hashes match,158/160 baseline unchanged. All960frontsamples<=22 and global accounting error<=1.18e-13. Previous edge-cleanup387package files and ZIP unchanged; HEAD/index unchanged, diff-check exit0. Published App SHA256 EF4AFB5B8743EB9A7972A3B8DC0F4212AFD2D680DD6A78289890DA4FD75F44FF equals tested DLL. Separate trial `artifacts/repro/front-paw-length-20260906-attempt-1/delivery-v1/runtime/Dororong.App.exe` launched PID40204 Responding=True; only exact-path assistant-owned PID32876 stopped after newprocess liveness. No commit/push/PR. STOP for user trial; user visual acceptance UNVERIFIED. Completion2026-09-07 local; attempt directory retains start date2026-09-06.

## Supplied-eight exterior matte correction — 2026-09-06

**Goal/spec:** User reports dirty outlines and authorizes correction (`수정 ㄱ`). Remove the white-background fringe of the actual supplied8 display frames, without changing source assets, head/body interior, dimensions, position or interaction behavior. Previous darker-texel-exact guarantee is superseded ONLY on the one-pixel exterior boundary; source files remain byte-exact. JPEG alpha is unavailable: estimated matte correction, not a claim of recovering the original transparent drawing.

**Architecture / tech:** C#/.NET8/WPF, existing SuppliedBodyDragFrames.Prepare pipeline. Keep existing border-connected white mask and integer registration. After registration, correct only non-white opaque pixels adjacent (8-neighbor) to transparency, estimating local outline darkness from the immutable radius2 neighborhood. If no darker outline sample exists, leave unchanged. Choose alpha from white-matte equation and subtract the same white contribution from all premultiplied color channels, so compositing on white exactly reconstructs each retained source pixel. Do not erode alpha support or recolor protected interior. No resizing, thresholds broadening, blur, new art, controller/timing/five-body/canonical/XAML/generic Sample changes.

**Files / one bounded task:** modify src/Dororong.App/Controls/SuppliedBodyDragFrames.cs; extend tests/Dororong.App.Tests/Controls/SuppliedBodyDragFramesTests.cs and migrate only obsolete exterior-RGBA assertions in tests/Dororong.App.DirectInteractionRender.Tests.ps1; update tools/HeadPullProof/Program.cs proof metadata and background comparison. New evidence only artifacts/repro/supplied-edge-20260906-attempt-1. Existing160 files matched supplied-eight final snapshot;760 prior supplied-eight evidence files frozen. Preserve old evidence/13 assets/JPEG8/PNG8, index and HEAD29750fe74967e611e267fa6cd3367a5928902b4c. No commit/push/PR/reset/checkout/clean. Work inline in existing feature worktree; user requested correction, no separate plan approval needed.

- [x] RED10failed/1pass: real Prepare gray/colored half-coverage fixtures and actual8 binary-opacity regression, before production change. Literal source/support/offset checks retained.
- [x] GREEN11: exterior-only white-unmatte after original registration, immutable neighborhood. All8 protected interior/support/positions unchanged; runtime white-composite identity exact; saved PNG roundtrip maximum1level error. Partial-alpha counts250/274/283/288/281/269/280/290. No new keys, scale or fractional translation.
- [x] Native and nearest dark/white/teal comparisons inspected; ragged bright fringe reduced. Release96Core+147App=243, render219351 assertions, RuntimeComposition, approved13 bothlocations, five-body40 parity all passed.170 playback state/position/timing rows unchanged,156active rows corrected keys,14other rows unchanged. Exactly4/160 scoped files changed;156sources and760prior evidence unchanged. Fresh read-only reviewer reports no actionable findings, independently checked scope/hashes and all8 images; did not rerun tests. User visual acceptance UNVERIFIED.
- [x] Separate trial published; App/Core tested DLL hashes equal published hashes.8 embedded source SHA and16 published Presenter rasters verified through identical PNG roundtrip. Source JPEG8/PNG8 and1684 older evidence files rechecked unchanged. ZIP `artifacts/repro/supplied-edge-20260906-attempt-1/Dororong-edge-cleanup-trial-20260906-v1.zip`, SHA256 `D19206296AA2B0DC53EDDFFC54C71963C5CFDE360A27D7F8C317B4C5F36C3B2E`,7270898bytes,388files; internal SHA256SUMS387/387 verified. New trial PID32876 Responding=True; only exact-path assistant-owned prior PID34520 stopped after liveness. All160 reviewed source hashes still match, diff-check exit0 with pre-existing EOL advisories. HEAD/index unchanged; no commit/push/PR. STOP for user trial; user visual acceptance UNVERIFIED.

## Connect the actual supplied eight frames — 2026-09-06

**Authority/spec:** User corrects wrong-source delivery and explicitly orders correct connection of Downloads/doro/1.jpg..8.jpg. Earlier 13-asset runtime selection is SUPERSEDED, not permission to overwrite those historical files. Source hashes were confirmed8/8; eight-frame-preview/frames/1.png..8.png are lossless decoded JPEG copies, also used by drag-distance-prototype. Current Windows executable instead embeds/plays legacy13. No new head composition or authored art.

**Plan (writing-plans/TDD; sole ledger here per AGENTS):** one scoped source-routing fix in existing feature worktree HEAD29750fe74967e611e267fa6cd3367a5928902b4c. Keep immediate mouse follow,40DIP pose progress,180ms release, five-body, cheek state, XAML/rawEOL, generic Sample implementation, original13/old evidence, and Git index/HEAD unchanged. No commit/push/PR. New outputs artifacts/repro/supplied-eight-20260906-attempt-1 only.

**Architecture:** add8 exact100x100 opaque source PNGs under Assets/user-body-drag/01.png..08.png and resource declarations. New SuppliedBodyDragFrames loader: retain originalRGB, remove only edge-connected near-white background (allRGB>=240), align visible soles to canonicalY86 with integer translation; fixedX+3 puts first source at canonical left edge, no per-frame X recentering or resize. Reframe transparent canvas to96x96 with no foreground clipping. Render a single supplied key using nearest discrete selection (as supplied prototype, no generated/interpolated head). Entry1..8; hold8; full recovery8..1; partial recovery same displayed progression. Legacy13 remain preserved but are not the selected head-drag sequence. Idle/sleep/click/five-body remain unchanged. Transparency mask is display-only; raw embedded PNG bytes exact. Stop if any source foreground clipped or visible body erased.

**Files:** new SuppliedBodyDragFrames.cs; Presenter.xaml.cs body-drag loader/sample only; csproj8resources;8sourcecopies; new supplied-frame Presenter/identity tests; migrate obsolete old13 runtime assertions to supplied8 contracts; HeadPullProof uses actual8 raw/prod comparison. Existing BodyDragFootAlignment utility retained for integerY registration. Inputs match supplied preview SHA map. Checks must assert selected keys, full non-white sourceRGB preservation, retained white interior count, literal translations, no interpolation, hold/release identity and real-loop movement preservation.

- [x] Frozen150 source files and both supplied source maps checked. RED9/9 actual-Presenter source/selection tests failed on legacy13 before production change; focused GREEN9/9 after routing supplied8.
- [x] Added8 exact source PNG resources and display-only preparation; entry1→8, hold8, full reverse8→1 and partial recovery use single authored keys. All8 native outputs inspected; no head redraw or resize. Only border-connected near-white background is removed for display, followed by integer translation; enclosed white and darker source RGBA preserved. Original JPEG bright edge fringe remains a disclosed limitation.
- [x] Release Core96/App145=241 passed; DirectInteractionRender200471 assertions and RuntimeComposition exit0. Historical13 identities preserved in both locations; supplied8 raw/embedded hashes match. Five-body40 native poses unchanged. Scope5 modified/+10 new files;145/150 baseline files and1684 prior evidence files unchanged. Frozen final160 files independently matched. All170 proof rows retain controller/timing/position values;156 active rows match the supplied8 prepared keys,14 idle/pending rows intentionally remain canonical. Independent scoped review: no actionable findings; offscreen evidence is not live user approval. Diff-check exit0.
- [x] Published fresh trial and verified8 embedded PNG hashes plus16 actual published-Presenter entry/reverse renders; published App/Core DLLs match tested Release assemblies. ZIP `artifacts/repro/supplied-eight-20260906-attempt-1/Dororong-supplied-eight-trial-20260906-v1.zip`, SHA256 `3375BCA15E50DD0AAE134C7371322696FD22028AD3A90B06A74A95AFDB21ABFE`,5287475 bytes,385 files; internal SHA256SUMS384/384 independently checked. Current JPEG8 and product PNG8 identities rechecked before launch. New trial PID34520 alive; only path-validated assistant-owned prior trial PID40708 stopped after new-process liveness. No commit/push/PR. Naturalness/user acceptance UNVERIFIED; STOP for user trial.

## Foot-anchored head-drag frames — 2026-09-06 user correction

**Goal/spec:** User reports frame positioning around 3/4 and requests feet as the anchor. Align vertical visible sole height across the existing body-drag keys, independently from immediate cursor-following window movement. No horizontal recentering, resizing, new art or anatomy inference. Existing source files remain exact.

**Evidence:** Native alpha>=128 bottom rows: entry[86,86,77,80,83,86,89,92], settle[92,87,82,77,86]. Presenter currently loads all raw96x96 keys without registration, samples them, and sets translationY=0. Therefore key2→3 jumps9source pixels up, then3→4 drops3. Use canonical visible sole row86 as the reference; alpha128 excludes faint antialias fringes. This is vertical registration only, not a promise to eliminate crossfade ghosting or different endpoint anatomy.

**Architecture/constraints:** TDD in this existing feature worktree; single bounded bugfix. New BodyDragFootAlignment helper translates each immutable premultiplied key by an integer Y offset before constructing existing PremultipliedFrameSequence; Sample, order/count, head size and colors remain unchanged. Keep all nontransparent texels, reject clipping/invalid references. Preserve XAML/raw EOL, PetBrain immediate following, controller40DIP pose/180ms release, five-body, cheek NOT APPLIED, assets/frame-sources, old evidence. No commits/push/PR or broad cleanup; new output only artifacts/repro/foot-anchor-20260906-attempt-1. Tech: C#/.NET8/WPF/xUnit/PowerShell.

**Files/interfaces:** Create src/Dororong.App/Controls/BodyDragFootAlignment.cs (`Align(PremultipliedFrame frame, PremultipliedFrame reference)`); modify only body-drag frame loader in DororongPresenter.xaml.cs. Create tests/Dororong.App.Tests/Controls/BodyDragFootAnchorTests.cs (real Presenter key/segment/raster assertions and helper edge cases). Migrate DirectInteractionRender.Tests.ps1 raw-URI drag assertions to exact shifted-source pixels using independently measured offsets[0,0,9,6,3,0,-3,-6]/[-6,-1,4,9,0]. Extend HeadPullProof only for foot-registration proof metadata/static keys if necessary; leave prior outputs immutable.

- [x] RED: actual Presenter samples must have soleY86; key raster must equal original with the literal Y shift, with no scale/X/channel changes. Initial14 tests:10fail/4pass before production edit; example expected86, actual77.
- [x] GREEN: translate complete rows after finding alpha>=128 baseline; no interpolation/resizing, offset0 reuses original. No nonzero-alpha clipping.13keys exact integer shifts; interpolated threshold-alpha sole85..86 because disjoint antialias edges already vary within the unchanged first two keys; exact50% blend asserted, no post-blend registration.2synthetic fringe/clipping/empty tests added.
- [x] Verify: Release96Core/136App=232; approved13 exact both locations; DirectInteractionRender5356 assertions and RuntimeComposition exit0; five-body40 unchanged; current-task3 changed existing files/145 unchanged/+2newfiles;1684 older evidence unchanged; prior deliveredZIP unchanged;170 playback rows keep all position/timing/strength/capture values. Offscreen proof-v2 and comparison inspected; no human visual approval. Initial full command used nonexistent Dororong.sln (corrected DororongDesktopPet.sln); initial proofreflection saw inherited Render overload (corrected DeclaredOnly); both failure logs preserved, no production changes for either.
- [x] Deliver separate trial ZIP after scoped code review. Fresh read-only reviewer found no Critical/Important/Minor issues in the5file scoped diff. ZIP `artifacts/repro/foot-anchor-20260906-attempt-1/Dororong-foot-anchor-trial-20260906-v1.zip`, SHA256 `7662C2C9C5246499BD12980784560CCC6A430B89ADA2B2483DF83A4E7F1C825C`,4980908bytes,385files; internal checksum384/384 and tested/published App/Core DLL parity verified. Following the user's preceding direct-run request, started new trial PID40708 and stopped only exact assistant-owned prior PID36604 after path validation and new-process liveness check. Other processes untouched. Naturalness/user visual acceptance UNVERIFIED; no commits/push/PR. STOP for user trial.

## Immediate head following — 2026-09-06 user correction

Latest authority: user rejects the fixed-position stretch phase and explicitly requests immediate mouse following. Supersedes prior40DIP movement gate only. Pose strength stays distance-based, full extension40DIP,180ms partial-safe release, exact13 assets/arrays/Sample, five-body and cheek status unchanged. Keep small OS click deadzone; once drag starts, position is pointer minus ORIGINAL grab offset at every distance, including below/at/above40. No40DIP waiting or offset subtraction.

Scope: narrow PetBrain movement fix, corresponding real-loop/runtime-composition regression migration and truthful proof metadata. No art/Presenter/controller/timing/five-body/cheek changes; no XAML/EOL edits. No commit/push/PR, old evidence overwrite, app auto-launch or cleanup. Output new artifacts/repro/head-follow-20260906-attempt-1 only. TDD then fresh Release/asset/render/parity checks and independent scoped code review; separate trial ZIP.

Preflight: HEAD29750fe74967e611e267fa6cd3367a5928902b4c unchanged.148 source/test/tool files match previous final snapshot exactly (`head-follow-before-frozen-files`); old dirty work preserved. Root cause: FollowBodyDrag returns while distance<40, then adds Extension to grab offset. Remove both, retain finite-distance validation and original grab offset. Execution: regression RED next, no production edits yet.

Completion: immediate following implemented after the small OS click deadzone, preserving original grab offset at 5/22/39/40/41/60 DIPs and reversal. Full extension remains distance40; partial hold/release180ms unchanged. RED reproduced fixed-position failure before production change; GREEN focused24/24, Release Core96/App120 (216 total), RuntimeComposition, render240 assertions, approved13 exact in both locations, five-body40-frame parity, and diff-check exit0. RuntimeComposition initially hit the existing screen clamp with a rightward fixture; corrected test direction to stay inside the work area and reran successfully, without production clamp changes.

Scope/preservation: exactly4 source/test/tool files changed relative to this task's148-file frozen baseline (PetBrain.cs, PetLoopDirectInteractionTests.cs, RuntimeComposition.Tests.ps1, HeadPullProof/Program.cs); remaining144 and1684 prior evidence files unchanged. All340 new proof PNGs equal prior images by frame index; only global placement changes. Independent fresh scoped review reports no findings; no live Windows input or naturalness approval. Existing Sample ghosting remains out of scope; cheek remains NOT APPLIED. XAML/raw EOL, renderer, controller/timing, artwork and five-body behavior preserved.

Delivery: `artifacts/repro/head-follow-20260906-attempt-1/Dororong-head-follow-trial-20260906-v1.zip`, SHA256 `2A6B5EBFAB8F6CD06F75680CD932E40D3A150993946E291BB0882A6B88722ED7`,3906843 bytes,367 files; independent internal SHA256SUMS audit366/366. Published App/Core DLLs exact match tested Release binaries. HEAD unchanged29750fe74967e611e267fa6cd3367a5928902b4c, staged diff empty. No commit/push/PR or automatic app launch. Code/tests/package checks complete; actual user feel and visual acceptance UNVERIFIED. STOP for user trial.

## Distance-driven head pull and local cheeks — 2026-09-06

**Goal / authority:** Latest user `ㄱㄱㄱ` approves head stretch controlled by pull distance, followed by original-pixel cheek pulling. Preserve delivered five-body interaction. This section supersedes only the earlier task's time-driven 140 ms entry requirement.
**Workflow:** TDD, sequential fresh implementers and independent review using subagent-driven-development. Existing linked worktree and HEAD `29750fe74967e611e267fa6cd3367a5928902b4c`. This TASKS file remains the sole execution ledger; ignored review artifacts are retained, not committed or deleted.
**Spec:** Approved in-chat design: slight pull gives partial pose, stationary pointer holds it, sufficient pull starts carrying; approximately 40 DIPs for full extension. Then local original-cheek deformation, one side proven before mirroring to the other. No new art.

### Current global constraints

- Preserve all source artwork / approved 13 exact bytes, original head identity and delivered five-body map, physics and rendering. Never edit DororongPresenter.xaml or PremultipliedFrameSequence.cs.
- Preserve source sequence ordering and Sample() blending; change distance progression, not assets. Keep 180 ms recovery where applicable, with no jump to full stretch on partial release.
- No stage/commit/push/PR, reset/checkout/clean, old evidence modifications, JOENESS changes or automatic app launch. New local trial output only under artifacts/repro/head-distance-cheek-20260906-attempt-1/.
- Native/nearest offscreen evidence is not live Windows approval. Visual/user acceptance stays UNVERIFIED until observed.

### Preflight / interface scan

| Tasks | Producer / consumer or internal consistency | Finding |
|---|---|---|
| 2 internal | Distance state, brain position, Presenter Strength, release mapping | Must test actual loop, stationary partial pull and continuous carry threshold; timer cannot finish entry |
| 2 → 3 | Shared controller, snapshot, Presenter, capture lifetime | Sequential edits; cheek task preserves head tests and newly verified mapping |
| 3 internal | Frozen original pixels / cheek-only mask / affine coordinate mapping | Broad cheek hit rectangles are not permission to deform eyes/hair/mouth; exact protected-pixel regressions required |
| 2 + 3 | Prior five-body feature and legacy click/sleep/clamp | Fresh baseline Core87/App106 green; existing tests retained except explicit obsolete time-driven body entry assertions |

Execution: baseline Core87/App106 and approved13 in both locations passed. Frozen pre-task source/test/tool snapshot141 files plus prior evidence/prototype/delivery snapshot1684 files. Task 2 dispatched to `/root/head_distance_impl` (Astra-high), TDD in progress. Product head movement uses explicit distance-mode plumbing while legacy default caller contracts remain compatible; carry latches after threshold and follows only excess distance. No Task 3 product edits yet.

Task 2 implementation checkpoint: worker RED/GREEN and frozen report at `head-distance-report.md`; parent independently ran Release96Core/119App, approved13 asset harness, and current renderer→40 previous five-body PNG parity, all exit0. Source/evidence audit1684 unchanged,0protected changes. Head proof170ordered pairs lives under new attempt/head-proof-v1;64 stationary frames have one exact native hash. Parent viewed quarter/half/full and release-start native images: quarter Sample interpolation visibly ghosts; this is not visual naturalness approval. Sol-high task review running. Additional RuntimeComposition test exposed an obsolete immediate-window-follow assertion (expected653,468 vs new local-stretch648,468); needs narrowly migrated distance/carry coverage before gate closes. No output published.

Task 2 review/fix round1: Sol-high found production diff sound, one Important obsolete RuntimeComposition contract. Same implementer migrated5/40/60DIP checks. Harness then reached a second old cleanup-trace expectation excluding the newly required cleared render. Ruling: migrate only this trace and require cleared Idle/no-grab/no-direct snapshot, while preserving stop/detach/clock/release order, exactly-once cleanup and exception aggregation — head cancel/fault cleanup now intentionally renders its cleared state; if wrong, cleanup trace compatibility needs rework. Production code is not changed by this test migration. Scoped re-review will cover both changes.

### Task 2: Distance-driven head/body pull

**Read first:** relevant Controller, PetLoop, PetBrain, Presenter and their direct-interaction tests. Work only in this existing worktree. Parent owns TASKS; worker must not edit it. Do not spawn subagents.

**Requirements:**
- Replace automatic 140 ms entry with pointer-distance progression. Existing small OS drag deadzone distinguishes click; full extension / carry starts at 40 DIPs from press. Map remaining distance after the deadzone monotonically to Strength 0..1. Use a single consistent finite-distance definition for progress and carry.
- Small movement yields partial stretch; stationary pointer does not increase it even after 1 second; reversal before carry reduces it. No arbitrary timer can finish entry.
- Window initially stays at press position; crossing 40 DIPs begins whole-window following without a 40-DIP jump. Preserve grab-offset continuity, clamping and target lock. Carry may latch once acquired; record the choice. No changes to five-body local/carry implementation.
- Partial release returns from the displayed entry progress to rest without showing full hang; keep 180 ms settle. Fully extended release can retain the existing settle sequence. Preserve 8-entry/5-settle assets, exact arrays and Sample() API. Do not redraw or modify XAML / PremultipliedFrameSequence.
- Preserve normal short click, sleeping wake, input outside window, unavailable pointer behavior, lost capture/fault/cancel cleanup and compatibility with existing snapshot constructors.
- Add meaningful RED then GREEN tests for stationary partial hold, distance monotonicity/reversal, threshold carry no jump, overshoot, partial release/no full-hang jump, actual loop position/no snapback, finite/clamped state, capture and legacy five-body behavior. Updating tests asserting old 140 ms automatic progression is authorized; do not weaken unrelated invariants.
- Focused units may be added under Interaction or Core/Behavior if needed. Modify only narrow Controller/Snapshot/PetLoop/Presenter/Core plumbing and associated tests. Add a small deterministic proof tool under tools/HeadPullProof using actual production controller/Presenter; output ordered partial-hold/full/carry/release native PNGs and normal-speed playback metadata, without launching app. Disclose offscreen capture.
- Run focused tests during iteration, full Release once before report; approved 13 regression, relevant render harness and diff check. Record failures honestly; no publish by worker.

**Report:** `.superpowers/sdd/TASKS/head-distance-report.md`, include RED command/failure, GREEN results, files, actual constants/mapping, proof paths, concerns. Self-review; return DONE / DONE_WITH_CONCERNS / BLOCKED / NEEDS_CONTEXT, no commits.

### Task 3: Local cheek pulling, staged one side then opposite

**Stage A isolation:** Keep the initial renderer and its executable self-checks within tools/CheekPullProof (or this attempt's proof source folder), outside the App project. Do not change shared Presenter/controller/input or production tests before the first-side visual gate. A blocked Stage A must leave no new cheek code compiled into the product.

**Boundary:** Begins only after Task 2 review. Implement original-source-pixel runtime local deformation, no new art. Preserve Task 2 and five-body behavior. Frozen pressed source/visible transform; selected cheek only; eyes, mouth, hair, ornaments and opposite cheek exact. No whole-window motion. Target locked, outward max 20 DIPs, inward compression, damped vertical displacement, smooth 220 ms release and cancel restore. First one-side renderer/native proof and protection checks, then opposite side using same method. Existing broad hit rectangles must not become broad deformation masks. If cheek-local deformation cannot work without protected feature edits, report blocker rather than redraw/expand scope.

**Read first:** `.superpowers/sdd/TASKS/cheek-preflight-notes.md`, current Controller/Presenter/FrameInteractionDescriptor and existing cheek tests. Parent owns TASKS. No subagents, no commits, no artwork edits, no XAML or PremultipliedFrameSequence edits, no previous evidence modifications. Same source identity and preservation boundaries as Task 2.

**Stage A (first side before routing):** Build one focused original-pixel local cheek deformation unit and a deterministic C# offscreen proof, initially unconnected to live Presenter/input. Begin with the larger exposed canonical screen-left cheek (anatomical RightCheek). Author and disclose a precise skin/selected-face mask from actual texels; old broad descriptor rectangles are NOT editable masks. Support rest, small outward, half, max20DIP input, inward and release. Do not redraw source or invent missing anatomy. Maximum input distance need not mean rigidly translating every cheek texel by20sourcepixels; smooth local attenuation is appropriate, but visually invisible skin recoloring is not a successful pull. Exact eye/mouth/hair/ornament/opposite-cheek/body pixels remain unchanged. If a connected visible cheek cannot be pulled under those constraints, preserve proof and report BLOCKED, with no product route/opposite-side work. Send parent Stage A native/nearest output paths and mask disclosure for visual gate before proceeding.

**Stage B (only after parent checks Stage A):** Integrate target-locked frozen source and affine transform at actual press, local original-pixel deformation, no window carry, smooth220ms release, inward compression and damped vertical movement. Retain existing legacy constructors. Add counterpart using same tested method and separate smaller-cheek source mask. Update actual canonical cheek hit testing to match visible skin instead of eyes/hair; provide explicit fixtures, and preserve transparent click-through and body-five classification. Do not leave head/hair presses swallowed by an old invisible-cheek rectangle. Sleep/wake handling must either use the captured source coherently or an existing safe wake route, never apply a canonical skin mask to differently positioned sleep eyes. Report any unsupported state explicitly rather than silently swallowing its input.

**Tests:** RED/GREEN meaningful renderer changes and exact non-target pixel preservation, no detached patch/alpha hole, visible cheek displacement, neutral/release exact restore; then mirrored transformed press, actual loop no movement/no ClickReaction, quick release, cross-target lock, missing pointer, loss of capture/fault/stop and regrab. Focused tests while iterating, full Release once before report; retain Task 2 and five-body tests. Worker does not publish or launch app. Output only new `artifacts/repro/head-distance-cheek-20260906-attempt-1/cheek-proof-*` paths; never overwrite a failed proof.

**Files:** focused renderer/capture/presentation units under Controls/Interaction as needed; narrow Presenter/Controller/Snapshot/PetLoop integration in Stage B only; associated tests and a tool under tools/CheekPullProof. Do not refactor five-body units to share a framework.

**Report:** `.superpowers/sdd/TASKS/cheek-local-report.md`, with Stage A observed facts versus interpretation, masks/protected fixtures, RED/GREEN logs, artifact paths, modified files and limitations. After Stage B, self-review and independent task review. All visual/user acceptance remains UNVERIFIED.

Execution: Task 2 complete for bounded behavioral trial; independent round1 re-review approved test migration, parent RuntimeComposition/render240/diffcheck exit0. Naturalness remains UNVERIFIED with observed quarter-strength ghosting. Task 3 Stage A now authorized for one-side isolated proof; no product integration before parent visual gate.

Task 3 Stage A gate: BLOCKED — first-side proof is not a visible cheek pull. Worker reports19/20 checks,0new exterior pixels; parent directly opened native contact sheet, max8x and mask overlay and observed skin shading movement only. Stage B/opposite side/product routing NOT AUTHORIZED. Failed proof preserved at `cheek-proof-stage-a-002-bounded`. The proof uses an assumed1.5DIP/source-pixel scale (not a measured product transform) and its destination mask is enclosed by construction; it proves failure of this bounded method, not impossibility of all pixel-based cheek designs. Source/raster protection must not be misreported as naturalness. Continue only head-trial handoff; future cheek design needs a separately agreed occlusion/feature boundary.

### Task 4: Current-change final verification and isolated trial

Handoff complete: Astra-high final current-task integration review found no Critical/Important/Minor blocker for HEAD-ONLY USER-TRIAL (`head-cheek-final-verdict.md`). Parent fresh Core96/App119, RuntimeComposition,render240,assets13,diffcheck all exit0; head proof audit and five-body40parity confirmed. Cheek Stage A remains BLOCKED19/20; no product integration/opposite side. Final frozen source148 files,10existing code/test changes from latest baseline;1684priorfiles unchanged/protectedsource0change. No commits or app auto-launch.

Delivery: `artifacts/repro/head-distance-cheek-20260906-attempt-1/Dororong-head-distance-trial-20260906-v1.zip`, SHA256 `0FB529D8900D4FC08908CDC79E9043594306F6AD8FDEBEB2DB84F7ECF9C6C2AB`. ZIP406/406 byte parity; independent internal checksum405/405 (manifest excludes itself). Published App/Core DLL hashes equal freshly tested DLLs. Old trial/evidence untouched. Distance behavior is ready for manual trial, not visual approval; quarter-strength ghosting remains. Overall PARTIAL; next: user head-trial observation and a separately agreed cheek occlusion/feature boundary. STOP; do not start a new cheek variant, sampling/art change, commit or cleanup.

Parent: after task gates, broad independent review of current-task diff against the frozen five-body baseline; fresh Release tests, relevant DirectInteraction harnesses, protected13, five-body40-render parity, source/evidence audit, diff check. Create a new local framework-dependent trial ZIP only after checks, without overwriting old runtime or launching Windows. If cheek Stage A is blocked, report/deliver head-only and keep cheeks NOT APPLIED; do not imply both were completed. Record exact package hash and test evidence; no commit or cleanup. Live/user acceptance stays UNVERIFIED.

## Five-region product port — 2026-09-06

> For agentic workers: use superpowers:subagent-driven-development for implementation and independent review; TDD applies. No commits or deletion of review workspaces. This section is the sole execution ledger.

**Goal:** Port the approved three-paw/belly/rump interaction to the WPF product while retaining product artwork and existing head/body drag.
**Architecture:** New focused region-map/session/renderer units, with narrow Presenter/PetLoop integration and an explicit clamped-position handoff to the brain. Preserve legacy interaction routes.
**Tech Stack:** C# / .NET 8 / WPF / xUnit / PowerShell; no new dependencies.
**Spec:** `docs/specs/2026-09-06-five-body-product-port.md` (faithful record of the approved in-chat design).

### Global Constraints

- Worktree `D:/JOEWRKS/.worktrees/DororongDesktopPet-m1-expression-animation`, branch `feature/dororong-m1-expression-animation`, base HEAD `29750fe74967e611e267fa6cd3367a5928902b4c`.
- Source artwork and all 13 approved assets remain exact bytes. No new art/bridge frames; no prototype edits. No modifications to `DororongPresenter.xaml` or `PremultipliedFrameSequence.cs`.
- Existing Body entry/settle stays 140 ms / 180 ms, same arrays and `Sample()` semantics. Existing cheek, sleep, click, grab-offset, target lock and clamp tests are not weakened.
- Exactly five body targets. Head/face/hair/rose/ribbon protected. Original five-zone prototype directory is read-only.
- No stage/commit/push/PR, reset/checkout/clean, evidence deletion, unrelated changes or JOENESS changes.
- Automated/rendered proof is not live Windows acceptance; visual/user acceptance remains UNVERIFIED.

### Preflight and decisions

- Fresh baseline Release test: Core 86/86, App 53/53, exit 0. Approved-asset regression: 13/13 in Assets and frame-sources. Only pre-existing dirty path is Presenter XAML; normalized blob matches HEAD. No product writes at this checkpoint.
- Decision: the user's latest `ㄱㄱ` approves the product-coordinate remapping design; proceed with the recorded design without another equivalent approval request.
- Decision: use this ledger instead of the skill's second progress ledger, and retain all review artifacts; the repository's single-ledger rule and user's preservation/no-commit constraints take precedence.

| Check | Producer / consumer or consistency check | Result |
|---|---|---|
| Task 1 internal | Region map and renderer share canonical coordinates; session and loop share window position and captured transform | Required explicit interfaces below; tests must exercise both directions |
| Task 1 legacy integration | New target route vs existing Body/cheek routing and PetBrain position | Old constructor calls and behavior remain compatible; position handoff cannot be render-only |
| Task 1 scope | Artwork preservation vs changed runtime image | Source bytes immutable; runtime deformation only; protected foreground restored exact |

### Task 1: Product five-region interaction and verification

**Read first:** the Spec above, then the prototype `body-regions.js`, `base-model.js`, `model.js`, `body-session.js`, `deform.js`, `middle-deform.js`, `right-deform.js`, `body-deform.js` and its tests. Reference root: `C:/Users/tjdwo/Downloads/doro/five-zone-soft-prototype/`.

**Files:**
- Create focused production units under `src/Dororong.App/Interaction/` for the region enum/map and softened session; under `src/Dororong.App/Controls/` for transparent deformation and scoped five-region presentation. Separate numerical deformation, raster sampling, and WPF binding if one file becomes hard to review.
- Modify only as needed: `src/Dororong.App/Controls/DororongPresenter.xaml.cs`, `src/Dororong.App/Interaction/DirectInteractionTarget.cs`, `DirectInteractionSnapshot.cs`, `DirectInteractionController.cs`, `src/Dororong.App/PetLoop.cs`, and `src/Dororong.Core/Behavior/PetInput.cs` / `PetBrain.cs` for a minimal local-position handoff. Runtime host/MainWindow changes only if needed for capture/transform lifetime; do not change XAML.
- Create behavior tests in `tests/Dororong.App.Tests/Interaction/`, `Controls/`, `Runtime/` and a narrow core regression if core input changes. Existing tests remain intact unless adding cases.
- Create deterministic proof harness under `tools/` and output only to `artifacts/repro/five-body-product-port-20260906-attempt-1/`. Worker report: `.superpowers/sdd/TASKS/task-1-report.md`.

**Interfaces (internal; exact names may be refined consistently in one implementation):**

```csharp
internal enum BodyRegion { None, FrontPaw, MiddlePaw, RightPaw, Belly, Rump }
// Map source-local point on canonical-shaped visible raster; alpha gate is mandatory.
// Session Begin captures immutable region/root/tip/actual click and source↔window transform.
// Move accepts finite global pointer positions; Tick advances the softened model.
// Snapshot contains Region, Phase, WindowPosition, PullSource, AnchorSource and capture requirement.
// Renderer consumes the frozen 96x96 source and snapshot, returns a single padded Pbgra surface.
// Loop passes the clamped session position into the brain, not just into host.SetWindowPosition.
```

- [x] Write and run RED tests against missing behavior (runnable stubs if needed; compiler errors alone do not prove RED). Literal hand-checked 96×96 region points must be independently derived from actual pixels, not obtained by calling the classifier itself. Protect representative face, rose, ribbon, transparent and tiny-fourth-paw points.
- [x] Implement the map and session. Preserve 0.8 px hysteresis, two 55 ms filters, 4 ms stepping / 250 ms cap, 18 px carry, 75 ms follow, 32 px reach and 200 ms settle. Use exact tests such as:

```csharp
// A direction reversal must eventually reverse the reported pull while the carried latch remains true.
// Release must settle to exact zero while retaining the clamped released WindowPosition.
// Input points outside the window (including negative screen coordinates) remain accepted after capture.
// Under-threshold movement must deform locally without moving the real window.
```

- [x] Port the three-paw folding/occlusion and local belly/rump flow to transparent product coordinates. Do not use the broad flow as a substitute for paw folding or paste opaque white rectangles. Body mask/hidden closure coordinates are authored runtime geometry and must be disclosed. Broad flow keeps a smooth attachment gate (minimum 2 px), 24 midpoint integration steps, radial tanh cap 10 px and region-specific footprints. Use premultiplied alpha for blends, nearest texture for meaningful pulls, and exact foreground restoration; preserve the source palette where possible. Check no exposed primitive seams or copied head texels.
- [x] Integrate canonical-shaped-frame hit classification, visible-transform capture, mirrored deltas, presentation padding/restoration, local/carry/settle input ownership, work-area clamping and brain-position reconciliation. Noncanonical sleep/hang input retains its old routing. Existing Body target timings/sequences are untouched.
- [x] Run focused GREEN tests and record commands/output for map, session, renderer, loop and core handoff. Required actual behavior includes both facing directions, quick release before the next tick, loss of capture, capture failure, release/cancel/fault cleanup, regrab, no autonomous motion during a held local interaction, no position snapback and no transparent-background hit.
- [x] Run full Release tests once, the 13-asset identity regression, DirectInteraction asset/render PowerShell tests, Release build and `git diff --check`; preserve truthful failures for review rather than declaring completion.
- [x] Generate product-coordinate boundary overlay and native/nearest 8-direction renders for all five targets using the actual C# renderer, plus a deterministic normal-speed direction-change/carry/release playback. Report the capture method accurately. No live app launch or visual PASS claim.
- [x] Self-review, write report with RED/GREEN evidence and known visual limitations, then return to parent for independent task review. Do not spawn agents, stage, commit, publish, change TASKS.md or edit the spec.

Execution status: Task 1 implementation, independent reviews and separate trial delivery COMPLETE. Current frozen code: `task-1-fix-1-frozen-files` (21files), parent fresh Core87/App106 and unchanged legacy240 assertions. HEAD/index unchanged. Delivery is USER-TRIAL, not visual acceptance; live Windows/user observation remains UNVERIFIED. Chronological failure/correction evidence follows and is preserved.

Draft visual gate: parent opened ownership and all five eight-direction sheets. Rejected draft for belly interior pinholes and non-nearest proof enlargement (five sheets have 515,893–529,427 nonmatching pixels each vs exact 3× replication). Upward paw fragments/root strokes also require inspection. Original draft retained; parent exact-pixel diagnostics are `.superpowers/sdd/TASKS/draft-visual-audit/`. Findings sent to implementer before delivery; no visual PASS.

Corrected proof gate: v2 removed the reproduced pinholes/root strokes and parent measured exact 3× replication for all five sheets. v3 corrects synthetic playback work-area geometry only; parent verified all 40 native poses byte-identical to v2 and opened all five v3 native sheets. Upward folds are heavily occluded and sometimes read as flat cuts; body contour sampling can thicken ink. These remain disclosed visual limitations, not final acceptance. Latest deterministic playback is `artifacts/repro/five-body-product-port-20260906-attempt-1/proof-v3/playback.html`; it is not live Windows capture. Pre-review preservation audit found only the eight intended existing source/test changes among 1,129 baseline files; all assets, XAML, PremultipliedFrameSequence, prior evidence and the reference prototype retained exact hashes.

Independent task gate: NEEDS FIXES. Review found (1) missing legacy four-argument DirectInteractionPressEventArgs constructor, independently reproduced by unchanged RuntimeComposition harness; (2) row-wide occluder erases upward distal paws, so the flat-cut behavior above is a delivery blocker, not merely accepted polish debt. Fix round1 dispatched to the same implementer for shaped torso/depth occlusion plus visible-distal regressions and the constructor overload. Also correct accumulated250 ms backlog cap (3 ms remainder case). Review verdict preserved at `.superpowers/sdd/TASKS/task-1-review-verdict.md`; previous proofs preserved. Runtime package withheld until fixes and scoped re-review.

Fix round1 gate: ADDRESSED by fresh scoped Sol-high re-review (`task-1-fix-1-verdict.md`), no new Critical/Important findings. Upward rendering uses a documented smooth compressive fold below immutable head; raw session/carry remains unchanged. Old white-pixel test replaced by stronger distal/proximal provenance and torso-alpha invariants after an actual alpha193 regression. Parent fresh frozen-code verification: Core87/App106, RuntimeComposition, protected13assets, legacy240render, DraggedAngle, diffcheck all exit0. Parent verified current renderer equals40 immutable v4 poses and all5nearest sheets have0mismatch; Belly/Rump16native poses unchanged fromv3. Source/evidence audit still only8intended existing code/test changes among1,129files. Task implementation/re-review complete; final integration review and trial-package delivery remain. No visual/live acceptance, no commits.

Final handoff gate: Astra-high integration review found no Critical/Important blocker and approved bounded USER-TRIAL delivery (`final-integration-verdict.md`). Parent published framework-dependent Release into the new attempt's `delivery-v1/runtime` only; App/Core DLLs are exact copies of tested DLLs. ZIP `artifacts/repro/five-body-product-port-20260906-attempt-1/Dororong-five-body-product-trial-20260906-v1.zip` SHA256 `61AE1E283743FF1AEF14278BB0C1C33EF063B078B9B0FA1C4D50580B8C3DF2D5`, internal archive parity675/675, manifest674files excludingitself. Contains runtime, v4 deterministic playback, native/nearest proofs and verification/review records. No app auto-launch, old publish overwrite, source-art change, stage, commit, push or PR. Upward fold styling, broad-render cadence, Windows feel and DPI/continuous-transform coverage remain UNVERIFIED. Next is user's manual trial; no further code/art work starts in this delivery.

## Existing M1 ledger (preserved)

- Goal / release: Milestone 1 — a runnable Windows desktop pet that lives autonomously and reacts naturally without obstructing normal work; authoritative design: `docs/specs/2026-08-26-dororong-m1-design.md`.
- Milestones: M1 behavior experience and Windows acceptance — in progress; broader platform behavior remains provisional future work, not M1 scope.
- Now:

  | Outcome | Acceptance | Status |
  |---|---|---|
  | Persist approved M1 design and implementation plan | Current spec and implementation plan inspected; JOENESS contract linked | Complete |
  | Implement and verify M1 | Automated core checks, Release publish, and all actual-Windows acceptance checks in the spec pass | In progress — the replacement E-only artifact uses `W=1.5`, E gain `2.5`, and C gain `0`. Source/native generation, causal thin-outline regression, repository visual inspection, independent review, fresh automation, framework-dependent publish, and attempt-8 actual-Windows body-outline observation pass. Closed-eye expression/motion refinement and remaining interaction/non-interference observations remain UNVERIFIED, so broader M1 is PARTIAL. |
  | Close phase-1 body-art correction | Exact E-only artifact preserves the canonical no-tail silhouette, matches the thin head/hair line, contains no protruding or internal continuation ink, and receives direct Windows user acceptance | Complete — user said `좋다` on attempt 8; exact evidence and cleanup are recorded. |
  | Design and implement the next visual behavior phase | Character-preserving closed-eye revision plus distinct expressions and motion for mouse/situation states, with actual-Windows observation | In progress — attempt 28 preserves the exact user-authored open/squint/closed frames, and attempts 35/38 remain accepted breathing/sleep-crossfade checkpoints. The body-click transform and provisional body-drag family are implemented. Body-click attempt 2 preserves canonical open eyes by direct user observation; the current click/drag production mapping passes deterministic 16 ms render inspection. Final-review fix `8fa1a96` clamps threshold-crossing and subsequent held body-drag ticks to the work area while preserving grab offset, and aligns capture metadata with actual loop ownership; the scoped re-review passed with no new findings. Body-drag art, actual Windows boundary/motion feel, and user acceptance remain `PROVISIONAL / UNVERIFIED`. Both cheek directions are not delivered: Task 9 exhausted its bounded primary, fallback, and recovery attempts without a visually acceptable family, so Task 10 was not started. The direct-interaction checkpoint and broader M1 remain `PARTIAL`. |

- Blockers / decisions / links: cheek art is blocked pending a new art direction or explicit user decision; neither rejected attempt is eligible for promotion. No new Windows environment is required. Repository tests, asset inspection, assembly identity, deterministic renders, and process liveness still do not prove live Windows interaction. The active body outline is accepted, and direct clicks continue to outrank inferred STARTLED reactions. The approved staged direct-interaction contract is [here](docs/specs/2026-08-31-dororong-direct-interaction-design.md); the truthful final checkpoint is [here](docs/verification/2026-08-31-m1-direct-interaction-final-acceptance.md). Links: [attempt 38](docs/verification/2026-08-31-m1-stage-a-sleep-crossfade-attempt-38.md), [direct-interaction plan](docs/plans/2026-08-31-dororong-direct-interactions.md), [expression/animation roadmap](docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md), [M1 specification](docs/specs/2026-08-26-dororong-m1-design.md), [body-outline plan](docs/plans/2026-08-27-dororong-complete-body-ownership-outline.md), and [README](README.md).
- Evidence / reviewed: 2026-09-01 — the original Task 11 layer passed fresh Core `82/82`, App `53/53`, five focused migrated presenter harnesses, a zero stale-call scan, and a zero-warning/zero-error Release build. Final-review fix `8fa1a96` then passed its evidence-bound RED/GREEN checks, fresh Core `86/86`, App `53/53`, and another zero-warning/zero-error Release build; scoped re-review reported `PASS` with no new findings. The final complete PowerShell-suite loop still has no available invocation-bound final exit because its session identifier was lost, so that full loop remains `UNVERIFIED` and is not called green. Existing deterministic click/drag render evidence remains under `artifacts/repro/direct-interactions-overnight-attempt-1/verification/final-rendered-16ms/`; no presenter/art/timing change required a rerender. The current HEAD publish is `artifacts/repro/direct-interactions-overnight-attempt-3/runtime/` (`Dororong.App.exe` SHA-256 `4AFC145876F2F3CBC5C15D450655E3E5EC966DB9BECA3D4017A3D53BB771AE3A`); PID `47088` was launched exactly once for morning inspection. Liveness does not promote any actual-Windows row. The user-confirmed body-click wording remains `ㅇㅇ 유지 됨`; boundary feel, focus, click-through, motion feel, drag acceptance, both cheeks, and all other unobserved rows remain `UNVERIFIED`.
