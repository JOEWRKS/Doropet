# Approved diagonal four-leg walking release

## Scope

User approved the four-leg preview: far fore+near hind, near fore+far hind;
two alternating diagonal pairs. Add a small occluded far hindpaw using the
existing transparent paw art. Preserve original seated PNG, other motion,
interaction, speed/direction and eye settings. The rejected seated vector
candidate is excluded. No installer, identity/backend redesign, commit or push.

## Release gates

- Frozen test fixtures moved into tools/PreviewLocomotion/fixtures/four-leg-baseline
  so comparison tests no longer depend on ignored artifacts. Includes old rig,
  old renderer, previous product bank and user-approved open-eye walk atlas.
- New `verify-four-leg-bank.cjs` observed RED against old product at phase0,
  then GREEN after regeneration. Previous bank and approved atlas hashes pinned.
- All130 idle/seated eye-state frames are byte-identical to previous product.
- All32 full-amplitude open-eye phases match the immutable approved preview
  atlas after native96 crop/premultiplication.
- All512 walking amplitude/eye frames match the current renderer. Manifest
  hashes, premultiplication and both seated PNG endpoints pass.
- Core Release239 passed; App Release747 passed,0failed/0skipped (986total).
  App tests include embedded-resource byte parity, native96 geometry, captures,
  interaction ownership, taskbar/platform and actual WPF presenter rendering.
- Fresh WPF144px left/right walking and seated proofs inspected after the full
  test completed; copies under artifacts/repro/four-leg-product-20260910.
- Independent release review found no Critical/Important findings. Its minor
  suggestion to pin the old bank hash was implemented and the gate rerun.
  Baseline-script hashes separately checked against frozen original evidence.

TRX: artifacts/repro/four-leg-product-20260910/tests/{core,app}-release.trx.
Full App suite duration2m19s. No initial failed full run in this release.

## Product and delivery identities

Native bank gzip SHA256:
03983B66D55CF2EC3B7A6B7CD0B78C5AC609FF41F442B0197909B74CDA723ECE.
Raw bank SHA256:
c069b8e90c7a6a9b8d0f4a3cb240e24b3f3940dc372231625e0a2a3dbbe366c2.

Published without rebuild from tested generic Release output to:
`C:/Users/tjdwo/Downloads/doro/four-leg-walk-20260910/runtime`.
Tested/published App SHA256:
C636C7BD8A8C2737E957C3DCF1D960758080DA952A8B437B2333810814111B51.
Core SHA256 unchanged:
70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA.

Old runtime37252 at seated-png-fix-20260910/runtime was identity/hash-checked
before stopping. Initial guard refused due path-separator spelling; no process
was changed. Normalized absolute-path check passed on retry, stopped only37252,
and started new45496 with a hidden launch. Previous directory remains intact
for rollback, App SHA D1F8712C8666E902A2C04404F424626BF33F0AB27F7E66B1BE08511E2A3EBF75.

Post-launch process/module checks establish that the new executable is running
and the tested App/Core DLLs are loaded. This is not a claim that native mouse
interaction was exercised on the live desktop this turn; rendering proof and
automated interaction regressions are the verification boundary.
