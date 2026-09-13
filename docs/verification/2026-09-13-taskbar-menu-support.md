# Taskbar context menu support — 2026-09-13

User reported pet falling at right side of taskbar when its right-click menu
opens. Approved preserving support/attachment while that menu is open without
ignoring real window occlusion or taskbar disappearance.

## Cause and scope

- Existing regression explicitly expected `#32768` menu overlapping the bar's
  top to remove that segment and switch Supported to Falling. Re-ran existing
  six-case theory before implementation to confirm code-level behavior.
- Native capture now retains `IsTransientMenu` on DesktopWindow. Geometry skips
  only transient-menu occluders over a taskbar with the same nonzero process ID.
  Menus still cannot support the pet and still occlude ordinary windows. Other
  process menus and same-process ordinary windows still cut taskbar segments.
- Matching Explorer-process shell menus share this taskbar-only exception; no
  general window-class exclusion, stale surface latch or position freeze added.
  Existing record-with coordinate mapping preserves classification.

## Verification

- New literal geometry tests RED3: repeated right-side menu opens, entering grip,
  attached grip. All fail under old behavior; three negative controls pass.
- Focused40/40 GREEN including taskbar disappearance, unrelated-menu and ordinary
  window controls. Core full241/241 PASS; read-only scoped review no findings.
- User confirmed normal tray exit; product process inventory empty.
- Candidate `candidate-20260913-taskbar-menu-01` published:
  - ZIP `142AFD1D490278A3D17FDD4A9CA0870B5E9CD525858CC25C4DD4598C57BA2D3B`.
  - App `4D2F18CAA0643B6C1364C043EA30EC6FBF7C43A0C1131663D82CF5ACFF7311F5`.
  - Core `19CBF7BA2085FFF980654D96B816B22C5B0205739FAFB70A17F4D840B08FD8D0`.
- Full app1012/1012 PASS, zero failures/skips (3m47s); core241/241 PASS.
  Installer payload/refusal/build and isolated-desktop native smoke PASS;
  archive round-trip and exact tested App/Core RID hash parity PASS.
  Setup SHA256 `408FCBCF9BB54E70A6C29B8DD327540668066490D8421986F13A504F141C14EB`.
- Backup470installed files+data/registration PASS; installer exit0 and467installed
  hashes/ownership/registration/Start link verified. Original data unchanged.
  Installed PID42104 launched; duplicate26888 exit0, existing log prefix retained,
  no failure events. Backup and evidence:
  `artifacts/installer/host-update-20260913-taskbar-menu-01/`.
- Native live menu
  acceptance remains user-observed; synthetic geometry tests are not live UI proof.
- No animation, artwork, browser preview, commit/push or public release changes.
