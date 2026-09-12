# Character context-menu outside-click dismissal — 2026-09-13

## Report and scope

After confirming installed relaunch and tray behavior, the user reported that
the character's own right-click menu remained open when clicking elsewhere.
This is separate from the working notification-area tray menu.

The no-activate tool window previously stayed outside the foreground input queue
when WPF opened its context menu. The scoped fix requests foreground activation
on delivered WM_RBUTTONUP before WPF opens that menu, without consuming the input.
Ordinary hover/left drag and the existing elevated-foreground left-press recovery
remain unchanged. No global mouse hook, polling, art or behavior changes added.

WPF's [PopupControlService](https://source.dot.net/PresentationFramework/System/Windows/Controls/PopupControlService.cs.html)
opens a context menu on unhandled right-button release. The public HWND hook runs
before WPF input processing. Read-only code review confirmed this ordering and
found no actionable issues in the patch. Actual dismissal is still a live check;
activation can be denied by Windows and callback tests alone cannot prove UI.

## Verification

- RED: two new right-release activation cases failed Expected1/Actual0 against
  the old implementation; the six existing activation tests passed.
- GREEN: focused activation, manual-sitting and shared perched-menu tests27/27.
- Initial full RID test invocation required an uninstalled shared .NET8.0.31 and
  aborted before tests ran; rerun uses self-contained8.0.31 to match the product.
- Full self-contained .NET8.0.31 win-x64 App suite854/854 passed in2m18s.
- Strict package test passed: runtime8.0.31, tested RID App/Core parity, icon and
  renamed-apphost metadata, archive round-trip. Private-desktop native smoke
  PID2024 started, duplicate exited0, original WM_CLOSE exited0.

## Local candidate and live handoff

`artifacts/product-shell/candidate-20260913-contextmenu-01/runtime/Dororong.exe`

- ZIP SHA256 `29B2110323AE5DBF10C40EF1E39AF0456690B8A9541AD7BBBC40E2C4F93BAD7F`
- App DLL SHA256 `7C75E9721A34C8E9C73358DA3F046B8B491429587FEFBEB588EF5E3EF9002171`
- Core DLL SHA256 `C3896D090C75620FE0A6FB63F3DE7A7061B42D3EE968D02BC4FF115C4878B1C4`

After the user normally exited the installed app, the verified portable candidate
was launched as PID35480 and emitted Started. Exact running path is recorded in
candidate `manual-launch.json`. Existing installation and installer pins remain
unchanged. User asked to test outside-click dismissal and reopen/repeat. Live UI
confirmation and installed-package promotion are still pending; do not describe
automated activation-boundary tests as actual outside-click verification.

The user normally exited the installed PID14528 to allow testing the revised
candidate. Do not overwrite the reviewed directory02 installer or old frozen
payload. Installed-package promotion and autonomous outside-click verification
must not be claimed before actually performed.
