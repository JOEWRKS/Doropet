# Normal non-elevated host installer trial — 2026-09-12

**Follow-up:** the empty-directory issue was subsequently fixed and verified in
[2026-09-13 regression results](2026-09-13-installer-directories.md). This report
retains the original failing candidate05 evidence unchanged.

User explicitly authorized backup followed by normal installation, unchanged
same-version reinstallation and uninstallation on this account. No failure
injection, file corruption, forced process termination, BIOS/Windows feature
changes, elevation, restart or guest-gate bypass was used.

## Candidate and baseline

- Candidate05: `artifacts/installer/candidate-20260912-task1002-05/Dororong-Setup-0.1.0-win-x64.exe`.
- SHA256: `AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D`.
- Evidence and backup: `artifacts/installer/host-normal-20260912-221548/`.
- Before trial: product directory, user/machine product registration and Start-menu/
  desktop links absent; no Dororong process. One existing product-data/log file
  copied to `original-data`, verified against its original SHA256 and preserved.
- Current token was non-elevated. Setup/uninstall logs report administrative
  install mode No, user privileges None, HKCU and no restart required. This is
  evidence for this non-elevated account, not every standard-user configuration.

## Observed results

| Normal scenario | Result |
|---|---|
| First install, default desktop task unchecked | Exit0; all467 payload files and ownership-manifest hashes match; product0.1.0/publisher/location and Start-menu link target correct; desktop absent; original data unchanged; no auto-launched product |
| Same-version reinstall, no damaged files or task change | Exit0; the same byte/identity/link/data checks pass |
| Uninstall after that reinstall | Exit0; product files, registration and Start-menu link removed; original data unchanged; no product process or operation lock. **Directory cleanup incomplete:14 empty subdirectories plus product root remain** |
| Control: fresh install then uninstall without reinstall | Both exit0; installed bytes verified; after uninstaller completion/retry the product root is absent; original data and registration/link checks pass |

Evidence: `install-process.json`, `reinstall-process.json`, `uninstall-process.json`,
`installed.json`, `reinstalled.json`, `after-reinstall-removed.json`, matching logs,
`control-install-process.json`, `control-uninstall-process.json`,
`control-installed.json`, `directory-comparison.json` and final `removed.json`.

The first residual directories were enumerated without following reparses, checked
against the known payload directories and the absent pretrial root, and recorded
in `empty-directory-residue.json`. Only those15 empty directories were deleted
bottom-up using nonrecursive Directory.Delete(path,false). No file was deleted by
that cleanup. The control then began from a genuinely absent product root.

## Open issue and interpretation

After-reinstall uninstall log contains zero directory-deletion entries; fresh
install/uninstall control contains16 (including a root retry). Both remove payload
files and registration. The current `UninstallLogMode=overwrite` and absence of a
separate retained directory-ownership list are a likely cause of the difference,
not yet confirmed by a corrective regression. Do not simply switch to append:
the prior review identified stale-file ownership risks with appended logs.
No installer/source change or new candidate was made during this validation turn.
Safe retained-directory ownership/empty-directory cleanup needs a separate fix.

Final observed state: product root, product registration, owned Start-menu link,
optional desktop link, product processes and operation lock absent; original
product-data file retains its original hash. Backup and evidence remain local.

## Not established

This normal host trial does not establish clean-guest compatibility, visual
first-run/tray behavior, selected-desktop-task lifecycle, real version upgrades/
downgrades, running-app/other-session handling, cancellation, injected failures,
disk-full/power-loss recovery, signing or distribution rights. Those gates remain
open. The guest-only harness was not weakened or run on the host.
