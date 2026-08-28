# M1 Windows manual acceptance — attempt 7

- Observation date: 2026-08-28 (Asia/Seoul)
- Source commit: `34dd95b` (`fix: integrate Dororong candidate F`)
- Executable: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1\artifacts\publish\win-x64\Dororong.App.exe`
- Executable SHA-256: `C0EB20CCEED12E2430F499D6274D219515940C91DED5EB33EBD207580C2A3533`
- Application DLL SHA-256: `4F69440F7D7FDDC62CF332E1756B1DE9281FE8DA47EF7FC7724C9EE6590237F4`
- Embedded open-frame source SHA-256: `F4C9B2CCE253522345F12D29F6CC634ACD0DE1C3151D7C923460E3D5EBA73B9A`
- Embedded closed-frame source SHA-256: `9B5411E88C031CA7D6542F6A1B3FD8E8C080BC061D96D4246B82011B9BF5A0BA`
- Launched PID: `18708`
- Process start: `2026-08-28T17:50:01.4582412+09:00`
- Cleanup: the exact task-owned PID/path/start tuple was verified and PID 18708 was stopped after the FAIL observation; readback found no survivor.

## User observation

The user rejected the live open-body rendering:

> 몸 선은 그대로고 몸 안에 있는 선도 지금 옅어지기만 했지 아직 남아있잖아.

Evidence image: `docs/verification/evidence/attempt-7-user-open-body-fail.png`

- Dimensions: `148x94`
- SHA-256: `354459F44576375D03C313CD5DBE93F23C3C7AF8DBBC0BF9D68B0DC75FE75E31`

## Verdict

- Open body outer stroke matches the thin head/hair stroke: **FAIL**. The body outline still reads materially heavier in the actual Windows target.
- Internal body/chest continuation is absent: **FAIL**. The line remains visible; lowering its opacity did not remove it.
- Closed-eye integration and all later interaction checks: **UNVERIFIED** for this attempt because the first required open-body check failed.
- Attempt 7 overall: **FAIL**.

Repository/native-file visual checks and automated tests do not override this actual-Windows user rejection. Candidate F is retained only as failed historical evidence and must not be used as an acceptance baseline for the two failed properties.
