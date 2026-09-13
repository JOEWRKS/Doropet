# Walking during tracking recovery

User reported sliding before legs start after pointer tracking departure.
UpdateHunting releases autonomous hold immediately, but ApplyHunting previously
covered the already-computed walking frame with a standing hunt raster until
gaze recovery and source blending ended. Presenter diagnostic showed moving
positions with unchanged feet through480ms and gait only visible by800ms.

## Fix

- During departure only (Walk, no active tracking/hunt/pounce), render the live
  gait lower body under the independently recovering head. Native74..76 uses a
  short smooth join; below76 gait fully owns the body. Draw head last to retain
  downward-looking hair/face foreground coverage. Hunt/session timing unchanged.
- At least the first authored1/8 gait level is visible; otherwise the first
  post-crouch180ms ramp tick rounds back to a standing frame.
- Cheek capture retains the frozen gait and its callbacks. Readiness flutter
  animates this same gait rather than restoring standing hindlegs. Normal cheek
  BGRA vs readiness premultiplied conversions differ by at most1rounding byte.

## Verification

- Outer tracking RED2/GREEN2; preparing exit RED2/GREEN2, both facings, first
  movement tick and retained nonzero head roll.
- Review found captured readiness losing hindlegs; RED1 alpha113->6, fixed.
  Existing head-mask checks and rear-region continuity pass across16phases.
- Focused50 PASS, core241 PASS, full app1058 PASS (3m58s; zero failures/skips).
- Proof `artifacts/repro/tracking-gait-20260913/exit-final.png`; first16ms lower
  body difference95138 from standing (previously0), continuing at160..640ms.
  Input position steps supplied by diagnostic; real PetLoop hold wiring inspected.
- Scoped reviewer approved revised source. No live mouse automation claim;
  user desktop feel still requires observation after replacement.

Candidate `candidate-20260913-tracking-gait-01`; package/native smoke/installer
gates PASS. Smoke PID50744 normal exit0, duplicate exit0; exact tested RID hash
parity and ZIP round-trip PASS.

- ZIP `18E1B71B89E97FBE0BBE99A5609C7E4ADF9EFFB2F5DFB2B1521404F457839664`
- App `3F4CFA9F00C0990E0076F361E7591EAA9F9DD7CF63E8877166FC4FA5DE7247C6`
- Setup `A345EE26540D8D6832491D9CE9FA92B82F8202AA139E80FF738F5E210BA00084`

Product was already closed. Verified backup470installation files,1data file,
registration and Start shortcut in artifacts/installer/host-update-20260913-tracking-gait-01/.
Installer exit0;467payload hashes/ownership/registration/Start link and unchanged
data verified. Installed PID42444 launched; duplicate53944 exit0, one instance,
original diagnostic prefix retained without new failure events.

No public release, commit/push, or browser preview change.
