# Dororong M1 Direct Interaction — Body Click Attempt 2

## Outcome

Attempt 2 is `PARTIAL` for the complete body-click Windows boundary.

- Awake body-click canonical-eye preservation: `PASS` from direct user observation.
- Exact canonical artwork selection and transform-only implementation: `PASS` at the automated/rendered layer.
- Pending-press subtlety, full hop/apex/land feel, keyboard-focus preservation, and transparent-area click-through: `UNVERIFIED` because the user did not explicitly judge those properties in this attempt.
- Broader direct interactions and overall M1 remain `PARTIAL`.

## Exact artifact

- Source checkpoint: `92dd706bfcaf51def1d148670dfb787ef40ec1d0` (`fix: keep canonical expression during body click`).
- Publish path: `artifacts/repro/direct-interaction-body-click-attempt-2/runtime/`.
- Observed process: PID `46668`, exact executable path under the attempt-2 publish directory, started `2026-09-01 00:51:50 +09:00`.
- `Dororong.App.exe`: `A3FF05D01FC9B3C4F66F2E6DC751035051723A5F2B567F33BDE5304C96A92804`.
- `Dororong.App.dll`: `99FF6BE0ACD13885A034A0D26D3FE8C7BD4D22DC61FD90C8C4A057CC7D41782A`.
- `Dororong.Core.dll`: `15A07012A23254AC4CE76C6350E0A448488C5F687B2A43FFB9D2D734D83D6E64`.
- Canonical source asset: `src/Dororong.App/Assets/dororong-canonical.png`, `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78`.

The attempt-2 publish was produced from the product working-tree content immediately before it was checkpointed by `92dd706`; no product source changed between publish and that commit.

## Defect and correction

Attempt 1 deliberately selected `dororong-blink-squint.png` during body-click progress `0.18 < phase < 0.92`. The user observed that the artwork did not break but rejected the unexplained half-closed eyes.

The correction removed expression selection from `BodyClickTransformSampler`. An awake body click now always displays the exact canonical open-eye image while the sampler controls only whole-character scale and vertical translation. The separately accepted sleep-wake bridge remains independent and may appear only before a sleep-origin hop reaches the canonical frame.

## Verification evidence

- Regression test RED: `Awake_body_click_keeps_the_canonical_expression_through_the_entire_hop` failed against attempt-1 source because the rendered image ended in `dororong-blink-squint.png`.
- Regression test GREEN: the same focused test passed after the correction.
- Release solution tests: Core `82/82`, App `51/51`.
- Direct-interaction render suite: `166` assertions passed.
- Sleep-pose suite: `251` assertions passed.
- Release build: `0` warnings, `0` errors.
- User observation on the exact attempt-2 runtime: `ㅇㅇ 유지 됨`, confirming that the base eyes remain throughout the tested awake body click.

No unobserved non-interference property is promoted from these results.
