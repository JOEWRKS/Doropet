# Bottom-taskbar perch reachability fix

## Approved scope

User approved implementation after the clamp conflict diagnosis. Keep the20DIP
entry interval, but translate it upward only for taskbars touching their monitor
bottom when the held silhouette cannot reach the existing interval. Do not relax
the screen clamp, change ordinary window edges, alter artwork or change the neutral
image09 attachment position. Preview and release use the same eligibility rule.

## Implementation

The platform runtime supplies the measured held sole in presenter-local DIPs with
the existing perch contact. For each bottom taskbar, the selector uses an interval
ending at the lesser of the original band end and monitorBottom-heldSole. Its width
remains20DIP. Occlusion, horizontal paw containment, headroom, reliable input/scene
and whole-carry gates remain in place. Acquired poses retain the original gripY and
owner-relative X. No host position or presentation changes during readiness.

## Verification sequence

- RED:5 failed/3passed before source changes. Three runtime silhouette cases and
  two real WPF both-facing loop cases failed on absent eligibility, not setup.
- GREEN:8/8 after correction. Focused expanded suite165/165 and Core241/241.
- Initial full App865/865 passed before the later numerical review correction.
- Read-only review identified exact-equality sensitivity after150% coordinate
  mapping. New integer1920x1080 scene test failed with real bottom contact;
  one-physical-pixel gap control passed. RED1failed/1passed.
- Corrected bottom-touch comparison to1e-6DIP roundoff tolerance; no physical-pixel
  tolerance introduced. Focused167/167 passed. Re-review: no Critical/Important/
  Minor findings; trial recommended only after final verification.
- Final Core241/241 and self-contained8.0.31 win-x64 App867/867 passed
  (App2m32s). Strict package/reference/runtime/icon/archive checks passed.
- Private-desktop native smoke: ownedPID8688, duplicate exit0, original normal
  WM_CLOSE exit0. This is not live pointer/perch acceptance.

Coverage includes translated/negative monitor coordinates, fractional DPI,
actual WPF head-grab silhouettes with requested-20/0/+20degree tilt in both
facings, full20DIP interval endpoints and rejection just outside, no mutation from
readiness, normal window/non-bottom taskbar controls, blocked surfaces, unreliable
pointer/local carry rejection, and actual loop release to unchanged registered grip.

Final test records are under `artifacts/repro/perch-range-20260913/verification`.
The diagnostic probe from the previous turn remains frozen against the old build.

## Delivery boundary

User normally exited candidate PID35480 (함); fresh inventory showed no Dororong
process and Stopped event at02:03:06KST. Fresh candidate published at
`artifacts/product-shell/candidate-20260913-perch-range-01`.

- ZIP SHA256: `2AFBE87EEC13D0D624F003731F453D8AA7E675349DA5289BA69FE694D9B8EF10`
- App DLL: `7E77010E3FA12FDB16629CB36E4F172154BFB37B18A6C819CDDEFF764BB821CE`
- Core DLL: `B5024D5D0F88B90583542F0FBD68D1F2296974B17663C9C9EA7FD9E68BF5DE68`

App/Core exactly match the final tested RID DLLs. After package validation and
fresh empty pet inventory, launched candidate normally as PID45648 at02:10:51KST;
verified exact executable path and Started event at02:10:52KST. User asked to
exercise head drag/swing near taskbar and release. Live result remains pending.
Existing installer, installed directory, previous candidates and assets preserved.
No commit/push. Live Windows interaction acceptance is separate from automation.

## Follow-up: held cue hidden behind taskbar

User reports the candidate's held cue cannot be seen behind the taskbar. This is
not a passed live range/visibility gate. The pre-existing layer-maintenance call
covered only Entering/Attached; preview rendering uses IsPerchReady while phase
is stillNone. Extend the same post-render maintenance condition to IsPerchReady.
No native flag, eligibility, art or host-position logic changed in this follow-up.

- New real-loop regression RED: readiness true but callback Expected1/Actual0.
- GREEN: focused86/86 including actual synthetic taskbar foreground/geometry/style
  preservation, repeated-shell-raise repair, no redundant writes, and controls.
- Existing active-perch/attached-cheek test updated to ready-or-active contract;
  new test verifies after-render preview, repeated held checks and stopping outside
  the eligible band/release. Scoped read-only reviewer found no issues.
- User normally exited45648 (ㅇㅇㅇ), and inventory was empty before publication.
- Fresh candidate: `artifacts/product-shell/candidate-20260913-perch-preview-layer-01`.
  Full App868/868 passed in2m42s; strict package/reference/icon/archive/native
  smoke passed (owned16424 normalWM_CLOSE exit0, duplicate0). Fresh empty process
  inventory before normal launch as34600. User visual confirmation pending.
- ZIP `F4D0A8C4B9E453AFCD422D13BECBE326DCDFA610683C39B0CFC6CD5D18381106`.
  AppDLL `8C88D847015B01066B4FD542B5F142C56246FE3722F70A4FDD5F31132ED213F3`.
  CoreDLL unchanged from the range candidate above.
