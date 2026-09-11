# Manual sitting command

Approved interaction: right-click menu `앉아` requests sitting and suspends
autonomous travel. Idle time no longer triggers sitting. There is no unlock
menu item; actual head drag or whole-character paw/body/cheek carry clears the
request. A mere click or local cheek tug retains it. No art or physics changes.

Implementation uses existing menu event -> MainWindow -> PetLoop wiring, with
one request flag and an optional host callback to the existing presenter.
The core autonomous clock is suspended while held; click-reaction time still
advances so a click cannot leave the core stuck. Seated blinking has its own
3.2s presentation clock; sitting still uses the approved650ms transition.
Platform/direct rendering takes priority. Hidden seated progress is retained
through non-carry local play, including platform Lifting, so overlay retirement
does not reveal a standing frame. Actual carry clears the request before render
and keeps the prior direct reset/recovery path.

## Verification

- TDD initial RED: idle10s incorrectly seated; menu had only Exit (2 failures).
  GREEN2/2 after removal of automatic sitting and addition of the menu command.
- Real-loop RED: active walk kept moving after command; click did not retain
  a seated pose. GREEN with request/suspension, drag release and event wiring.
- Focused15/15; first full App Release702/702; Core Release237/237.
- Independent review found local overlay retirement reset the seated progress.
  First-resumed-frame regression observed RED, then corrected hidden-state pause.
- Final focused18/18: menu, idle, command/position/blink, click retention,
  head/paw/cheek carry release, local-cheek first resumed frame, approved frame
  hashes/capture, direct reset and platform render priority.
- Independent focused rereview approved; no remaining Critical/Important findings.
- Final full App Release705/705 (2m3s), Core237/237,942 total. Explicit Release
  build0 warnings/0 errors; publish succeeded. Test/build/publish hashes match.

## Delivery

Previous running executable: `C:/Users/tjdwo/Downloads/doro/locomotion-20260910/runtime/Dororong.App.exe`,
PID22444. Its path and hash were revalidated immediately before shutdown;
CloseMainWindow returned false, so only that exact process was stopped. Its
folder remains unchanged for rollback. Published to a separate
`C:/Users/tjdwo/Downloads/doro/manual-sit-20260910/runtime` directory and launched
`Dororong.App.exe` hidden. New PID20868 is responding and is the sole pet process.
Loaded App/Core modules have the verified candidate paths and hashes:

- App: `2A3D97304B73E86B88CE4D3E3668C9B9947E7D1E3A60A1F9AAEA6005FF7BD5FC`.
- Core: `96A04EFF4075EF73743A394B7B3C7C1D45C25AC17DF1DF00FE21968BEE35C980`.

No commit/push. Native no-activate pet window remains outside Computer Use's
window inventory; verification uses actual WPF/menu/loop tests and loaded-module
identity, not a claim of live native mouse testing.
