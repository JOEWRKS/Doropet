# Mouse-response correction

User approved replacing conflicting retreat/curious approach behavior with
hunting, whole-body cursor-facing, stable proximity and responsive re-entry.

## Causes and changes

The old pointer detector could start Startled at220DIP or Curious at150DIP,
before the smaller hunting ellipse was entered. Hunting only permits idle/walk,
so an earlier legacy reaction blocked hunting. Hunting-enabled hosts now
suppress legacy reactions without removing pointer input from direct gestures.
Other hosts retain the old detector behavior for compatibility.

The core applies cursor-facing while the hunting hold owns idle/walk, with an
8DIP center deadband. Heading updates with facing so resumed walking does not
go backward. The presenter uses that facing rather than stale click/cheek
overrides. Old facing overrides retire once tracking takes ownership.

Proximity keeps the existing77.5x50DIP entry ellipse, with20% wider exit radii.
Re-entry reverses the existing recovery tail from the exact current pose,
including both crouch depth and fading rump offset. It does not wait for the
standing endpoint. A second departure continues forward from that same pose.

No image assets, walking/seated banks, blink timing or direct gesture parameters
changed. The old2799 preview remains a visual reference; these corrections are
in the native product behavior.

## Evidence

- Initial5 RED assertions: legacy Startled interception (two approaches), no
  body turn, entry-boundary chatter, continuing recovery on re-entry.
- Separate old-click-facing RED caught the final idle snap back.
- Review caught a partial fix preserving depth but not rump sway. Four tail
  ages1.75/1.9/2.0/2.3 now have RED/GREEN full-pose continuity regressions.
- Focused56 tests pass, including slow/fast approaches, left/right/center
  jitter, unavailable pointer exit, second departure, drag/sit precedence,
  resumed travel direction and120 supported ticks alternating body facing.
- Independent rereview: no remaining findings.

Native production UI input is not claimed as manually exercised. Evidence is
the real WPF presenter, actual PetLoop/core/platform tests and running binary
identity checks, not a browser-only simulation.

## Release

Final Core239/App789 Release tests pass:1028 passed,0 failed,0 skipped.
TRX: artifacts/repro/mouse-response-20260911/tests/{core,app}-release.trx.
Published --no-build into
C:/Users/tjdwo/Downloads/doro/mouse-response-20260911/runtime.
App SHA2560504668A3B1BCF4301F6BB6F8B3BAD87032DF13D9E125205932AE733A860E20F.
Core SHA256CF38696E05EE7AC3F68E28ED3CFFBB74785A92194DAD56D0DE660565D9797880.
Both DLLs match the actual test output copies.

Verified the previous process26352 path before stopping only that pet and
launched new14696. New responsive process and App/Core module paths checked.
Previous hunt-product-20260911 directory is preserved for rollback. No commit,
push, installer, artwork or preview-server changes.
