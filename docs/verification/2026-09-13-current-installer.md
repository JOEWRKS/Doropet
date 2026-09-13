# Accepted product → installer refresh — 2026-09-13

The user accepted the ribbon correction (“아주 좋아 다음”) and asked to continue
release preparation. The current portable candidate contains the accepted
context-menu dismissal, taskbar readiness/stutter and chin/hair/ribbon fixes;
the installed directory and previous installer still carry the older payload.

## Scope and inputs

Refresh the local installer from the immutable, tested and visually accepted
`candidate-20260913-ribbon-match-01` portable package. Do not rebuild the App/Core
assemblies, change character behavior or installer lifecycle policy, install on
the host, or publish/sign. The existing installed product is preserved.

Archive SHA256: `3C64B377C41F09E09523D2D7431217A1BD65DCB3D22EE49940C957B298011322`.
App SHA256: `EA93C5684114A4911B7A689E7A486692447A1CBC034208968A615A63D579DD8D`.
Core SHA256: `2AC994181A0498B57D210D42AFD86ADBEBA7FC345A8E0916E2C7DBE50494B420`.
Previous-turn verification: App919/919, strict self-contained8.0.31 package and
private-desktop native smoke passed; user accepted the live artwork separately.

Pinned Inno Setup7.1.0 compiler remains unchanged. Fresh Authenticode status
Valid, signerPyrsys B.V. Only build archive pin and test input paths are refreshed;
no arbitrary archive/hash override is introduced.

## Evidence boundary

Old builder refusal reproduced before output creation; four literal replacements
made across builder/archive pin and two test input paths. Focused Refusal and
Payload suites passed. Implementation report is retained at
`artifacts/installer/ribbon-promotion-20260913/task-1101-report.md`.

Scoped spec/quality review PASS, no severity findings. Normal user exit of portable
PID29496 at04:45:01KST verified; no product process remained before build.
Strict package validation uses the single-instance product on an isolated desktop.
No force-stop was performed. Read-only installed
App hash remains `A1797AA20FD189FEACF6BE4C57B2B1A0ECF4CB8B52578B7AAAC701B14D7BCE5A`;
HKCU registration still identifies Dororong0.1.0 at its established user-local path.

Prior host install/reinstall/uninstall and first-run evidence belongs to the
directory02 installer. It establishes historical policy behavior, not new-payload
acceptance. Guest test scripts/handoff still pin that older installer, and must
not be presented as validation of this new installer. BIOS/Sandbox remain unchanged.

## Completed candidate

Command: `pwsh -NoProfile -File tests/Dororong.InstallerBuild.Tests.ps1 -Phase Build
-CompilerPath artifacts/installer/toolchain-inno-7.1.0-x64-20260912-02/ISCC.exe
-CandidatePath artifacts/installer/candidate-20260913-ribbon-match-01`.

- Exit0; metadata, signature state, explicit inventory and installer hash PASS.
- Strict package/RID/Core/Desktop8.0.31/apphost/archive parity PASS.
- Private-desktop smoke: owned18156 normalWM_CLOSE exit0; duplicate0.
- Fresh manifest recheck: all467 staged files match accepted payload hashes.
- Installer62,765,471 bytes; SHA256
  `C87A42CE206380690041C37FFE06ACABB5968C303C88006019701FD95E3E83D2`.
- SignatureNotSigned. Product version remains0.1.0, identity/lifecycle unchanged.
- Build evidence, staged payload and package-validation.log retained inside
  `artifacts/installer/candidate-20260913-ribbon-match-01`.
- Installed App hash rechecked unchanged after build; no host installation ran.
- Same accepted portable executable relaunched12136 at04:47:56KST following an
  empty inventory check; exact path and Started event verified.

No App/Core rebuild, source art edits, public release, commit or push. Next step
requires the separate normal installation/reinstall handoff for this new candidate;
it is not yet installed or lifecycle-accepted merely because compilation passed.

## Authorized normal host update — completed follow-up

User approved actual installed-product update and normal launch (ㄱㄱ). Evidence
and retained backups: `artifacts/installer/host-update-20260913-ribbon-01/`.

- Normal CloseMainWindow on the known portable12136 returnedFalse. No force-close;
  user then exited through the tray, and the process inventory was empty.
- Backed up and hash-verified470 old installed files,1 existing data file, Start
  shortcut and full HKCU product registration before installation.
- The first one-off backup helper included PowerShell provider metadata in JSON;
  serialization warned and stalled before baseline save. Canceled only that helper
  with Ctrl+C, leaving already copied backups and the unchanged installation.
  Resumed by verifying identical backups and serializing plain registry values;
  backup passed. This was not an installer/application failure.
- Exact reviewed installer45236 exited0. Log confirms current-user/non-admin mode,
  successful installation and no restart. No install auto-launch or desktop link.
- All467 installed payload hashes match the new manifest, ownership matches,
  Start shortcut target/working directory and uninstall registration are correct,
  directory ownership is preserved, operation lock removed. Existing data was
  byte-identical before the first launch; user-added installed files were checked.
- Installed `C:\Users\tjdwo\AppData\Local\Programs\JOEWRKS\Dororong\Dororong.exe`
  started as47392 (Started14:08:13KST). Duplicate24900 exited0, leaving only47392.
  Original data bytes remain an exact prefix; only Started was appended and no
  failure diagnostics were observed.

Current state: latest installed product running; portable copies/backups retained.
User visual confirmation is still separate. This proves normal same-version update
on this account, not clean-guest or cross-version upgrade/failure-injection coverage.
No signing, public release, source behavior change, commit or push.
