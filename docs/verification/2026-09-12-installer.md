# Installer candidate and isolated acceptance — 2026-09-12

**Superseded candidate:** the directory-ownership fix and normal-host regression
results are in [2026-09-13 verification](2026-09-13-installer-directories.md).
The hashes and commands below describe the historical candidate05 and handoff;
current acceptance tools intentionally pin the newer reviewed candidate instead.

The local unsigned installer is compiler-built and reviewed. Isolated guest
lifecycle rows remain **UNVERIFIED** because this host has Windows Sandbox disabled,
no WindowsSandbox.exe and no available existing VM. No Sandbox launch, feature
enabling or account creation was performed.

A subsequently authorized normal non-elevated host trial exercised installation,
same-version reinstallation and removal: payload/registration/link/data checks
passed, but **empty directories remain after reinstall then uninstall**. Fresh
install/uninstall without reinstall removes the root. This open cleanup issue and
the safely restored host state are documented in
[normal host trial](2026-09-12-host-normal-installer.md). Historical non-installing
test evidence below is unchanged and must not be confused with the newer host run.

## Immutable deliverable

- Candidate: `artifacts/installer/candidate-20260912-task1002-05/Dororong-Setup-0.1.0-win-x64.exe`
- Setup SHA256: `AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D`
- Frozen ZIP: `artifacts/product-shell/candidate-20260912-1751/Dororong-win-x64.zip`
- ZIP SHA256: `7BB45700A174D98123383B52FB39C913CA2F9EDA924827CBD14D97C136E20BC6`
- 467 original payload files plus generated ownership manifest; product version 0.1.0.
- Product ID: `{928A9BC7-3A87-4C52-9161-1E5D08DB410A}`.
- Inno 7.1.0 compiler SHA256: `D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A`.
- Local provenance: candidate `build-evidence.json`, `payload.json`, and
  `artifacts/installer/task-1002-report.md`. No product rebuild was needed for this handoff.

## Observed automated evidence

Final installer-wide review and its single scoped fix re-review found no remaining
implementation findings. Core 241/241 and App 852/852 regressions passed on unchanged
tested binaries; compiled shared-policy checks 37/37 and the strengthened payload/
non-installing acceptance suites passed. These do not establish live installer
acceptance. Current opt-in handoff: `artifacts/installer/sandbox-20260912-task1003-04/acceptance.wsb`.

`pwsh -NoProfile -File tests/installer/InstallerAcceptance.Tests.ps1 -Phase All`
passes ordinary-host refusal, a generated-artifact replay that reaches only the
initial fixed guest-path guard, separate inert checks of the production gate's
nonce, configuration/input/setup/compiler hashes, mapping, and identity predicates,
restricted configuration checks, existing-output refusal, and compilation of three
distinct fixture versions. The predicate checks use synthetic values and do not
create guest paths or invoke the mutation section. No fixture executable is run by
this test. The host snapshots
compare product directory metadata/immediate children, operation-gate path, logs
directory metadata/immediate children, Start/desktop link hashes, and HKCU/HKLM
product registration before/after these non-installing checks.

The boundary's RED scaffold exited 0 without performing operations; the test failed
because host acceptance must return `UNVERIFIED`/exit 2. With the gate implemented,
the same test passed. Configuration RED similarly detected a no-output generator;
GREEN validated the resulting XML and refusal side effects. The pure predicate seam
was first missing (structural RED); after implementation, deliberately removing its
nonce equality check made the synthetic mismatch regression fail, and restoring the
guard returned it to GREEN. This is inert predicate evidence, not a successful guest
gate replay. Guest lifecycle tests have not been executed, and have no live RED/GREEN claim.

The unchanged strict product-package validator passed fresh: self-contained
Core/Desktop/host 8.0.31, exact tested App/Core RID hash parity, metadata/icon/apphost
binding, ZIP round-trip, and its existing private-desktop native smoke. That smoke
is not an installer test. Generator validation additionally compares every original
payload/staged file hash and the build source/generated-input hashes to retained
evidence. Candidate and ZIP hashes above remain identical.

## Opt-in Sandbox handoff

Generate a fresh handoff from the repository root:

```powershell
pwsh -NoProfile -File tools/installer/New-InstallerSandbox.ps1 -CandidatePath artifacts/installer/candidate-20260912-task1002-05 -PreparedToolchainPath artifacts/installer/toolchain-inno-7.1.0-x64-20260912-02 -OutputPath artifacts/installer/sandbox-NEW-UNIQUE-NAME
```

The generated `acceptance.wsb` disables networking, clipboard, audio/video input,
printers and vGPU. Three inputs (small handoff/scripts, exact candidate, prepared
compiler) map read-only. Only that handoff's new `results` directory maps writable.
No workspace root, user profile or Downloads directory is mapped. Paths refer to
this host; regenerate on any different prepared host. Never reuse an output or
results directory. Generation does not launch or enable Sandbox.

The current prepared handoff is
`artifacts/installer/sandbox-20260912-task1003-04/acceptance.wsb`. It contains the
final self-reviewed guest gate and supersedes the intermediate `-03` and prior `-02`;
all older handoff artifacts remain preserved. It still binds the unchanged candidate
05 and toolchain 02 hashes above.

After a separate deliberate handoff on a host where Sandbox is already available,
opening the `.wsb` runs built-in Windows PowerShell 5.1 through its LogonCommand.
The copied guest script has a UTF-8 BOM, including Korean shortcut paths. It does
not assume PowerShell 7, downloads or external network availability in the guest.
The exact guest invocation, including fresh nonce, is embedded in the generated XML.

Before **any** result, fixture, installer, registry or shortcut mutation, the script
requires fixed guest mapping/script paths, then calls the side-effect-free predicate
seam for the handoff nonce, generated configuration hash and matching four mapping
declarations, disabled channels, hashed inputs,
reviewed setup and compiler; empty results; different host/guest MachineGuid, user
SID, computer name and system UUID; the WDAGUtilityAccount identity/profile; and
Microsoft virtual-machine/hypervisor evidence. It also refuses directly accessible
host output paths. Missing/unreadable evidence returns `UNVERIFIED`/exit 2 before
writing results. These combined checks prevent accidental ordinary-host execution;
they are not cryptographic attestation against a hostile administrator. A guest
whose evidence differs must be investigated, not accommodated by bypassing the gate.

`guest-verified.json` and `token.txt` retain the actual SID/groups/admin-token
evidence. A Sandbox account with the Administrators SID does not establish standard
user acceptance, including when its current token is filtered. No account is created
or privilege altered by the harness.

## Guest cases and remaining gates

| Package | Generated guest case | Live result |
|---|---|---|
| Reviewed product 0.1.0 | First install, per-user identity/location, all payload hashes, Start menu target, unchecked desktop and no silent product launch | UNVERIFIED |
| Reviewed product 0.1.0 | Repair a damaged owned file, preserve user extra, select desktop | UNVERIFIED |
| Reviewed product 0.1.0 | Individually lock real Start/desktop links without delete sharing; refuse repair, desktop deselection and uninstall with full payload/registration/link bytes unchanged | UNVERIFIED |
| Reviewed product 0.1.0 | Owned executable handle lock: repair/uninstall refuse; invalid target directory refuses before changes | UNVERIFIED |
| Reviewed product 0.1.0 | Cancel English wizard through UI Automation Cancel/Yes before installation; state unchanged | UNVERIFIED |
| Reviewed product 0.1.0 | Unlocked desktop deselection, reselection, normal uninstall removes owned files/links/registration, preserves logs/user extra/Downloads sentinel and no remaining product process | UNVERIFIED |
| Separate fixture | Real fixture installs 1.0.0 then 1.10.0; 1.2.0 refuses using shared `VersionAllowed`; injected postinstall failure is nonzero, followed by repair/removal | UNVERIFIED — compilation only passed |

Fixture ID `{D9BAFA26-9406-49E2-8C81-C8F41DD7C033}`, display name **Dororong
Acceptance Fixture ONLY**, and target `%LOCALAPPDATA%\DororongAcceptanceFixture`
are distinct from the product. Its inputs are tiny version-labeled text files and
the actual shared `InstallerPolicy.iss`. No product version override, rebuilt
product or modified distributed payload is used. Fixture PASS can only establish
the fixture lifecycle and shared version primitive; real higher/lower product
packages and product integration remain open.

Each executed guest case records package identity, status, process command/hash/
exit code and setup logs; locked cases also save the original full state snapshot.
`summary.json` remains PARTIAL/exit 2 even if all generated cases pass, because real
product upgrade/downgrade, a running product process/other login session (as distinct
from a file handle), mid-copy/disk-full/power-loss recovery, standard-user proof and
visual/tray/localization acceptance remain open. A failed case returns 1 and leaves
the disposable guest available for inspection. No automatic recovery is claimed.
Do not infer a successful live run from XML generation, compilation or absent output.

Configuration format reference: [Microsoft Windows Sandbox configuration](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).
