# Rest-synchronized cheek hop and outline repair — 2026-09-13

User approved repairing visible contour breaks, launching when cheek recovery
finishes and landing behind without returning. Held cheek does not carry the pet.

## Movement

- Face spring and eye/hair recovery finish at440ms. Hop follows for280ms; full
  local interaction retires at720ms. Weak pull2DIP horizontal/1DIP lift; max8/4.
- Horizontal displacement is monotonic smoothstep and is applied to actual
  brain/window coordinates using cumulative deltas. The final fractional delta
  is consumed exactly once on controller retirement. It is never reversed.
- Vertical arc uses the existing supported-offset platform seam. The legacy
  no-platform path uses a retained Y baseline. At the ceiling, clipped ascent
  cannot accumulate a false downward landing offset.
- Render snapshot offsets are cleared only at the final render handoff, so the
  visible sprite cannot double the physical displacement. Held grip on an edge
  is retained: attached cheeks recover but do not jump away from attachment.
- Normal/mirrored legacy and real taskbar landing cases RED3 then GREEN3;
  review reproduced double-render RED2 and ceiling drift RED4, corrected and
  re-reviewed. Cancelled hop retains reachedX and falls back to support; its
  assertion allows subpixel landing/breathing motion after confirmed support.

## Outline

- Actual installed-DLL proofs reproduced missing front-neck ink at crouch entry
  frame28/positive roll and pale forepaw boundary under lowered head frame42.
- A fixed nativeY65 join mask erased the raised connection. The authored head's
  white neck backing was also painted after the body outline pass, hiding ink.
- Restore final visible-boundary RGB-only ink, using static-body reference below
  the head during flutter so moving paws cannot rewrite the captured head.
  Retain the raised joining stroke to the authored morph's paw root.
- Eight literal actual-render regressions RED8, then GREEN in focused191 suite.
  Before/after native-render sheets inspected by parent and reviewer:
  `artifacts/repro/hunt-outline-20260913/before/contact.png`, `after/contact.png`.
  No source asset or eye-expression edits. New tests and probe are authored
  diagnostic work, not screenshots of live Windows behavior.
- Implementer subsequently identified a5DIP endpoint jump on the first crouch
  frame from a conditional root switch (frame15→16 channel delta15645 vs old525).
  Candidate `candidate-20260913-cheek-landing-01` is NOT eligible for delivery.
  Independent probe with assertion exited1 on that DLL. Interpolating the old
  endpoint to the morph root with the existing body blend reduced the delta to523
  (source-linked probe assertion PASS), while rest/full crouch were unchanged.
  Final proof `artifacts/repro/hunt-outline-20260913/final-source/contact.png`.
  Final published candidate probe also reports523 and passes the entry assertion.

## Gates

- Focused192/192 PASS before additional entry-continuity case; zero skipped.
  `tests/Dororong.App.Tests/TestResults/cheek-landing-focused4.trx`.
- User confirmed normal tray exit; empty product process inventory verified.
- Final full RID suite1006/1006 PASS, zero failed/skipped (3m39s):
  `tests/Dororong.App.Tests/TestResults/cheek-landing-final-full.trx`.
- Final read-only review: no actionable findings. Installer payload/refusal/build
  tests PASS; isolated desktop native smoke PASS, duplicate exit0. Product ZIP
  round-trip and exact App/Core RID hash parity PASS.
- Only final candidate `candidate-20260913-cheek-landing-02` is deliverable:
  - ZIP SHA256 `FE406C15FAE06A6E95097584EFE0B90BFE514199780468C7BB37E208CA60FD43`.
  - App SHA256 `3ADF1E2C2E056ACF695CB9B878FA852027E3DB085D3845213ED69181760B7011`.
  - Core SHA256 `BC0205183B828B534A2D949BF66FFB9B404C842CCD3BF991BC4139C5C7BEEA42`.
  - Setup SHA256 `1FA5A8D209E0A400D9A3855FD034AA66EAFD76E45D931168341E33954E0EEE46`.
- Normal installed-host update PASS. Evidence and recoverable original backup:
  `artifacts/installer/host-update-20260913-cheek-landing-02/`.
  Backup verifies470installed files,1data file, registration and Start link.
  Installer exit0;467payload hashes, ownership, registration, shortcut and original
  data verified. Installed PID31428 launched from the registered install directory;
  duplicate47840 exited0. Existing diagnostic prefix retained, no failure events.
- No force termination, source-art replacement, browser preview change,
  commit/push or public release. Live acceptance remains UNVERIFIED.
