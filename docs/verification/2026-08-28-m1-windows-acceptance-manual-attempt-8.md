# M1 Windows manual acceptance — attempt 8

- Observation date: 2026-08-28 (Asia/Seoul)
- Repository HEAD before launch: `34dd95b707b96f8b90ebf640114a7ecff670c1aa`; the E-only correction is an uncommitted, identity-bound working-tree change and is not misreported as this commit.
- Executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Executable SHA-256: `C0EB20CCEED12E2430F499D6274D219515940C91DED5EB33EBD207580C2A3533`
- Application DLL SHA-256: `F606F2EB8D46F541EF71298A1AD73C8F658DA41CC08E32A30D82116B6E0930FE`
- Core DLL SHA-256: `E0B24FCCA21D23B073A6E569646CF551B2685B6A929B2DDCEA586917BEB24BAE`
- Embedded/runtime open-frame SHA-256: `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`
- Embedded/runtime closed-frame SHA-256: `F48AB174F6DEE6C92F04E7363F854CC92AA1ED53504A728F7F69E8F1D0A0167E`
- Outline constants SHA-256: `73085C56D2910C6DA81DC2A9B62FE8BDA64A472B978752733883DFAF718098AD` (`W=1.5`, E gain `2.5`, C gain `0.0`)
- Launch PID: `15004`
- Launch creation time: `2026-08-28T19:15:25.7559410+09:00`
- Launch readback: exact executable path and PID survived the initial 1.2-second identity check.

## Ordered user observation

The user supplied a live screenshot and said `좋다`, accepting the body-outline correction. The same reply explicitly moved closed-eye refinement and richer situation-specific expression/motion animation into the next phase.

1. Live body outline: original no-tail, three-leg/two-valley silhouette; no protruding stroke; outer body line has one apparent weight matching the thin head/hair line; former internal chest/shoulder continuation lines are completely absent rather than pale — **PASS**, by the user's direct `좋다` verdict on the launched exact artifact.
2. Current closed-eye frame: the supplied screenshot shows the eyes located on the face without the earlier detached-eye failure, but the user explicitly requested that the way Dororong closes its eyes be revised in the next phase. Basic placement is observed; refined expression/animation acceptance is **UNVERIFIED** and must not be inferred from this still.
3. Remaining interaction and non-interference acceptance items — **UNVERIFIED**.

Evidence image: `docs/verification/evidence/attempt-8-user-phase-1-acceptance-and-closed-eyes-next.png`

- SHA-256: `5DC4871E60F4A5AC7EFC0D9A33392AF19C825A7A51AEC349746E73E4268DCCE1`
- Dimensions: `127x152`
- Target/state: exact attempt-8 published runtime, closed-eye/sleep presentation on the current Windows PC.

## Current result

- Attempt 8 body-outline scope: **PASS**.
- Attempt 8 broader M1 scope: **PARTIAL**; closed-eye expression/motion refinement is deferred by explicit user direction, and the remaining interaction/non-interference observations are still **UNVERIFIED**.
- Cleanup: after the observation, recorded PID `15004` was matched by exact path and creation time, stopped, and read back with `0` exact-path survivors. The application was not relaunched.
