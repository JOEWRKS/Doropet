# Dororong M1 Windows Acceptance — Stage A Attempt 2

## Scope and exact identity

- Date: 2026-08-29, Asia/Seoul.
- Branch: `feature/dororong-m1-expression-animation`.
- Exact clean source HEAD: `40b624c173aca6bf722a51bb962b0d9afc4a1611`.
- Publish path: `artifacts/repro/stage-a-closed-eye-sleep-attempt-2/`.
- `Dororong.App.exe`: `C54D12BE80E2C12A3141616B447966931402C6B3FCA3CC965B60ED8299EC3145`.
- `Dororong.App.dll`: `E880B7E614FB984924FB6C5D8965685210228EB857BFB46616DE4A52A5954E01`.
- `Dororong.Core.dll`: `5D472D0987B30D58385A1C1908907DF5F0ED48E9BB6EF41D31F3AEDFEC9E426A`.
- Embedded/runtime canonical frame: `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`.
- Embedded/runtime corrected closed frame: `014470FC4DEDD9C2FE6B5B24E254624774A5AE0F1F46662761E186143C6D68BF`.
- Controlled launch: PID `56288`, start `2026-08-29T02:02:45.9783607+09:00`, exact executable path matched the publish path above.

## Automated and asset layers

At the exact HEAD, a fresh Release build completed with zero warnings/errors, the core suite passed 78 / 0 / 0, and all required focused Stage-A checks passed. The publish was created exactly once in the attempt-specific path. Static native and 8x inspection by the implementing agent, primary agent, and an independent reviewer found two balanced shallow `⌣` lids; the canonical open frame and protected non-eye/alpha pixels remained exact. These layers did not by themselves establish live rendering.

## Direct actual-Windows screen observation

The user explicitly directed the primary agent to judge the visible result. The visible transparent Dororong window was resolved by exact task-owned PID to window handle `726738`, title `Dororong`. A first 120-frame screen sequence covered WALK and contained no blink, so it was not used as positive eye evidence. One materially different bounded capture waited until the window position was stable, then recorded 160 frames at approximately 20 ms intervals.

That IDLE sequence captured repeated closed-eye frames followed by the canonical open frame. Direct inspection found:

| Check | Verdict | Evidence boundary |
|---|---|---|
| Correct live `⌣` eyelid direction and placement | `FAIL` | Although the curves point in the required direction, the user directly observed that the pair is positioned too far right and must move left as a whole. |
| Return to open frame without residue | `PASS` | The captured return frame shows the canonical open eyes without lingering lid fragments. |
| IDLE blink timing/readability | `PASS` | The approximately 0.12-second duration is too brief for an ordinary manual screenshot but was readable in the captured sequence; this does not override the failed horizontal eye placement. |
| Gentle SLEEP breathing | `UNVERIFIED` | The captured frames prove IDLE blinking, not a sustained SLEEP cycle. |
| Click-only wake reaction | `UNVERIFIED` | No click-only wake observation was performed in this attempt yet. |

Preserved attempt-specific evidence:

- `artifacts/repro/stage-a-closed-eye-sleep-attempt-2/verification/live-blink-closed-frame.png`: `54E64018C88D57E4F55D339D1F1FEF29FEDAFC94D063F9F5C5B31AC24704663A`.
- `artifacts/repro/stage-a-closed-eye-sleep-attempt-2/verification/live-blink-open-return-frame.png`: `018C1B1F6E73334C0E8B845385845728A4D5A7B8381E8D6FCD50D9364BB6B589`.
- `artifacts/repro/stage-a-closed-eye-sleep-attempt-2/verification/live-idle-sequence-contact-sheet.png`: `2094874579C3CBE27154AD40306DBC6F059D53F8B437A721D4DA30C4857248B2`.

The two unique temporary capture directories were moved to the Windows Recycle Bin after the selected evidence was preserved, and both original temporary paths were confirmed absent.

After seeing the exact running attempt, the user rejected the horizontal placement: `아니 눈이 병싄아 이상하잖아. 왼쪽으로 가야겠구만 전체적으로`. This direct user signal supersedes the primary-agent and independent static/live acceptance judgment. The exact task-owned PID `56288` was then matched by path and millisecond start time, stopped, and confirmed absent. A subsequent correction must preserve the accepted shallow `⌣` shape and spacing while translating both lids left together.

## Result

- Stage A attempt 1 closed-eye `FAIL` remains historical evidence and is not upgraded.
- Stage A attempt 2 protected-art/curve-direction checks and blink timing: `PASS`.
- Stage A attempt 2 live horizontal eyelid placement: `FAIL`.
- Stage A attempt 2 SLEEP breathing and click-only wake: `UNVERIFIED`.
- Overall Dororong M1: `PARTIAL`.
- Phase-1 body-outline `PASS` and provenance boundaries are unchanged.
