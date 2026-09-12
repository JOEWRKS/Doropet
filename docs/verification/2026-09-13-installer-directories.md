# Reinstall directory-ownership fix — 2026-09-13

## Scope and cause

The [previous normal-host trial](2026-09-12-host-normal-installer.md) is the RED
regression: same-version reinstall followed by uninstall left 14 empty payload
directories and the app root. A fresh-install/uninstall control did not.

Inno 7.1 [MakeDir](https://raw.githubusercontent.com/jrsoftware/issrc/is-7_1_0/Projects/Src/Setup.Install.pas)
does not register existing directories unless explicitly requested.
`UninstallLogMode=overwrite` therefore dropped their earlier creation records.
The fix preserves this log mode (avoiding stale file ownership), and separately
retains only installer-created directory identities in per-user previous data.

- Capture missing incoming parent directories and root before native creation.
- Preserve existing ownership across reinstall, including old payload directories.
- Never claim pre-existing directories without an earlier ownership record.
- Validate bounded relative paths, duplicates and reparse components.
- Remove only recorded empty child directories, longest path first, without
  recursion/enumeration. Nonempty parents and user-added empty folders survive.
- Re-register an owned root with native empty-only deletion/retry after uninstall
  metadata is removed; do not manually delete the live uninstaller.
- Require successful directory and shortcut metadata persistence before reporting
  installation success. Inno catches RegisterPreviousData exceptions, so the
  explicit postinstall success guard is necessary.

Legacy candidates without the directory record conservatively leave existing
directories unclaimed. This is not a claim that reinstalling over a legacy
candidate repairs its already-lost creation history. Fresh installations with
this candidate retain that history correctly.

## Immutable candidate

- `artifacts/installer/candidate-20260913-directory-02/Dororong-Setup-0.1.0-win-x64.exe`
- SHA256 `5BEAA5294FC9B73C527CB06404425C87320B421BD225223DAA9B8D253B13E61E`
- Version0.1.0; unsigned local candidate, not a public release.
- Unchanged frozen ZIP SHA256
  `7BB45700A174D98123383B52FB39C913CA2F9EDA924827CBD14D97C136E20BC6`.
- All467 original payload files plus ownership manifest; no character/art changes.
- Builder provenance includes the new `InstallerDirectories.iss` source.

## Evidence

Compiled shared-policy tests cover persistence/reinstall, nested empty cleanup,
pre-existing directories, user files/empty subdirectories, absent root capture,
root delegation, malformed/duplicate/traversal records and reparse refusal before
any cleanup. Existing file/shortcut/locking tests also pass. Builder refusal,
package validation, native private-desktop smoke and final build assertions pass.
Payload inventory/path/reparse/sentinel safety suite also passes.

Non-installing acceptance All passes: Windows PowerShell5.1 process-count checks,
ordinary-host refusal, pure guest gate predicates, restricted mappings/config,
existing-output refusal and three compiled-only version fixtures. The new shared
directory include is copied and hash-bound in the handoff. Current prepared-only
configuration is
`artifacts/installer/sandbox-test-3696c3f7e6024f5caa22afd560fecf4c/acceptance.wsb`;
it was not launched. The old candidate05 pins/configuration are superseded.

To prepare another fresh handoff (without launching it):

```powershell
pwsh -NoProfile -File tools/installer/New-InstallerSandbox.ps1 -CandidatePath artifacts/installer/candidate-20260913-directory-02 -PreparedToolchainPath artifacts/installer/toolchain-inno-7.1.0-x64-20260912-02 -OutputPath artifacts/installer/sandbox-NEW-UNIQUE-NAME
```

Read-only scoped review identified one persistence-success issue; the final
candidate includes the fix and its scoped rereview found no further findings.

Final normal-host evidence and backup:
`artifacts/installer/host-normal-20260913-directory-02/`.
Fresh install, unchanged same-version reinstall and uninstall all exited0.
Both installed-state snapshots verified all467 payload hashes, ownership manifest,
the exact14-child/root ownership record, HKCU identity and Start-menu link target;
optional desktop link absent and no product auto-launch. Final removed.json
confirms root absent, zero remaining files, registration/shortcuts/lock absent and
original data unchanged. No manual directory cleanup was needed. The script waits
for the root to disappear rather than trusting only the outer uninstaller exit.

## Intermediate trial and data preservation

Directory01 was installed and normally removed while a final rebuild was being
prepared. Its removal completed and root disappeared. Its strict data comparison
failed because the build's native smoke (PID4452) appended Started/Stopped events
to the existing diagnostic log. The original5353 bytes remain an identical prefix
of the resulting5657 bytes. No original content was removed/restored/rewritten.
Timestamps and PID match candidate02's package-validation log. The intermediate
trial is not reported as a byte-identical lifecycle PASS.

After all builds completed, a new backup/baseline was taken for the final trial;
no application smoke/build runs overlap that trial. Both backups and trial logs
remain local. No user log was truncated to manufacture a passing comparison.

## Still unverified

Clean-guest compatibility, visual first-run/tray behavior, selected desktop-task
lifecycle, real version upgrades/downgrades, failure injection, cancellation,
other-session/running-app handling, signing and distribution rights remain open.
Normal host checks do not substitute for these. No BIOS/Windows feature changes,
elevation, forced process termination, restart or guest-gate bypass were used.
