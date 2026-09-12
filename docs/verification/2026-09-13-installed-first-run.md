# Installed first-run and tray verification — 2026-09-13

Candidate: directory02, version0.1.0, SHA256
`5BEAA5294FC9B73C527CB06404425C87320B421BD225223DAA9B8D253B13E61E`.
Evidence/backup: `artifacts/installer/host-first-run-20260913-01/`.

The user approved the installed first-run/tray/normal-exit check. The current
non-elevated account began without an installed product, registration, links or
running product. Its existing data file was backed up before installation.
No character code, payload, Windows settings or automatic startup was changed.

## Observed results

- Installation exited0; all467 payload files and ownership manifest verified;
  correct per-user registration/Start shortcut, no optional desktop shortcut,
  no installer auto-launch, original data unchanged before first run.
- Installed Dororong.exe launched as PID4292 with a Started diagnostic event.
  The user supplied screenshots showing the character and its context menu,
  then the actual notification-area tray menu: 도로롱0.1.0, 로그 폴더 열기, 종료.
- Duplicate launch PID39080 exited0, leaving only the original PID4292.
- User confirmed the log-folder command worked and normal tray Exit removed
  both the character and notification-area icon.
- Independent final checks found no Dororong.exe/Dororong.App.exe process and a
  Stopped event for PID4292 at2026-09-13T01:13:38.9525898+09:00. No new failure
  events were recorded. Original data bytes remain an identical prefix; only
  Started/Stopped events were appended. All467 installed payload hashes match.
- Final state: installed, stopped. No uninstall, force-close or log truncation.

## Evidence limits

Computer Use launched the application but its window inventory excluded the
transparent character and notification-area menus. No guessed handles, input
fallback or fabricated screenshots were used. Menu interaction/visual outcomes
are user-assisted evidence; process identity, payload/data hashes and diagnostic
events were checked directly. The initial exit watcher timed out before the
user's later Exit action, so the original process exit code was not captured.
Do not infer exit0 solely from Stopped and process disappearance.

Relaunch after normal exit is still untested in this installed-host trial. This
does not establish clean-guest, failure-injection, every DPI/multi-monitor/session,
signing or distribution-rights readiness. Existing character context-menu label
Exit differs from Korean tray 종료; this is a recorded polish observation, not a
functional change made during verification.
