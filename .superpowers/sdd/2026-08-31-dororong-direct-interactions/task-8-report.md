# Task 8 — body-drag runtime integration

Status: implementation complete; visual/runtime/user feel remains **PROVISIONAL / UNVERIFIED**. No app process was started, stopped, relaunched, or observed. No publish, push, merge, roadmap update, or Windows acceptance was performed.

Base: `c6c906f3776d076a77001693fbea880fde9eae0c`

Implementation commit: `a27fd88c0310a200d34b7c29c85709dcda8a7c75` (`feat: animate Dororong body drag`)

## Delivered behavior

- `DirectInteractionController` now exposes normalized body-entry progress over exactly 140 ms, holds the exact entry endpoint, then exposes normalized settle progress over exactly 180 ms before returning to `None`.
- `DororongPresenter` maps `BodyDragEntry` through all seven provisional Task 7 entry keys, `BodyDragHold` to the exact full-hang endpoint, and `BodyDragSettle` through all five provisional settle keys.
- Adjacent keys are sampled through the existing `PremultipliedFrameSequence`, producing one frozen `Pbgra32` source on the existing single `AlphaHitTestImage`. Whole-character opacity remains 1; no overlay or second character surface was added.
- The visible facing active at drag entry is locked through entry, hold, and settle. The old procedural `ScaleY = 1.12` and grab-offset rotation are reset while complete-character drag frames are displayed, so the authored fixed head/top anchor is not moved by an additional presentation transform.
- The existing `PetLoop` production ordering was retained: core pointer processing and `_host.SetWindowPosition(current.Position)` happen on the same threshold-crossing tick before render. Focused loop coverage proves the original `(60,50)` grab offset, immediate movement, release-time work-area clamp, capture acquisition, button-up release, and local settle at the clamped release position.
- Pending body art remains canonical/subtly compressed below threshold; exact body-click, sleep/wake, idle breathing, cheek targeting, capture cleanup, and core priority behavior remain on their existing paths.

## Exact provisional asset binding

The runtime order matches Task 7 attempt 2 exactly:

| Runtime key | SHA-256 |
|---|---|
| `body-drag-entry-00-press.png` | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |
| `body-drag-entry-01-lengthen.png` | `862C1EA03B2368FBF2163F0C07E3B3A717389A6580EF2C282F65B86EA99B5879` |
| `body-drag-entry-02-drop.png` | `0DDC8554E1E8F589C6A83230712B30160A38524C5E25773788D13C195E3519EE` |
| `body-drag-entry-03-stretch.png` | `319311A9452C188DB4E1BD7F91C6FDD78FB6812FACE227CB2A9DF1743F9EE8F3` |
| `body-drag-entry-04-dangle.png` | `4619F665B8AD71C4D629A7D5B43F8362110CD6ECB38F7E3A57E68E290F5472C9` |
| `body-drag-entry-05-near-hang.png` | `C28E4E6E4E6C6060885C26E3D807D3CE77A2D563EE5F94477C21D984080BE4EA` |
| `body-drag-entry-06-hang.png` | `6F9968FE05EA9E92D657ACD9E0B74333C408493D811A2F42CAE3707D53DCE94A` |
| `body-drag-settle-00-hang.png` | `6F9968FE05EA9E92D657ACD9E0B74333C408493D811A2F42CAE3707D53DCE94A` |
| `body-drag-settle-01-lift.png` | `BAA74DE388AE97E2A125C9E1B0AC4A9F4C7CC74E3A75C50F1CEB185045316C24` |
| `body-drag-settle-02-gather.png` | `4BCEE1ED2480BB9D3083A122C57BF7F585596376E5749AC8E5535A901C2C40D3` |
| `body-drag-settle-03-land.png` | `862C1EA03B2368FBF2163F0C07E3B3A717389A6580EF2C282F65B86EA99B5879` |
| `body-drag-settle-04-recover.png` | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |

## TDD record

Named controller break: body phases existed but exposed no progress, so presentation could not sample entry/settle continuously.

Focused RED before production changes:

```text
dotnet test tests/Dororong.App.Tests/Dororong.App.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectInteractionControllerTests.Body_drag_exposes_continuous_entry_hold_and_settle_progress"

FAIL: expected halfway entry Strength 0.5, observed 0.
1 failed, 0 passed; exit 1.
```

Named render break: `DRAGGED` still used the old procedural body scale/rotation and did not select the Task 7 full-character sequence.

Focused RED before production changes:

```text
pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release

FAIL: Body drag entry progress 0 did not select body-drag-entry-00-press.png.
exit 1.
```

Focused GREEN after the minimal implementation:

```text
Controller body-progress filter: 1 passed, 0 failed.
PetLoop immediate-movement/grab-offset/clamp filter: 1 passed, 0 failed.
Direct-interaction render suite: 234 assertions passed.
```

The render assertions independently cover the seven exact entry sources, an interpolated midpoint with `PixelFormats.Pbgra32`, whole-character opacity 1, one character surface, zero extra stretch/translation/breathing transforms, exact full-hang hold, locked left facing, five exact settle sources, canonical recovery, and resumption of idle breathing.

## Fresh verification

```text
dotnet test DororongDesktopPet.sln --configuration Release --no-restore
Core: 82 passed, 0 failed, 0 skipped.
App: 53 passed, 0 failed, 0 skipped.

Sequential project rerun:
Core: 82 passed, 0 failed, 0 skipped.
App: 53 passed, 0 failed, 0 skipped.

pwsh -NoProfile -File tests/Dororong.App.DirectInteractionAssets.Tests.ps1 -Configuration Release
PASS: 12 complete 96x96 keys, protected identity, continuity, four separated legs, clean alpha, source/runtime parity, canonical recovery.

pwsh -NoProfile -File tests/Dororong.App.DirectInteractionRender.Tests.ps1 -Configuration Release
PASS: 234 assertions.

dotnet build DororongDesktopPet.sln --configuration Release --no-restore
Build succeeded; 0 warnings; 0 errors.

git diff --check
exit 0; only existing LF-to-CRLF notices were printed.
```

An additional out-of-scope sweep of every historical `Dororong.App.*.Tests.ps1` stopped at `Dororong.App.BlinkRecovery.Tests.ps1:109`: that legacy harness directly calls the now-internal `DororongPresenter.Render` method and fails before Task 8 behavior is exercised. It was not retried or modified. The two Task 8-relevant PowerShell suites above were then run sequentially and passed.

## Visual-check boundary

Overall verdict: **UNVERIFIED**. The implemented mapping and exact source/key identity pass deterministic checks; actual Windows interaction and subjective motion feel were intentionally not observed.

Sources: direct-interaction spec sections 2, 3.4, 5.1–5.5, and 6.2; plan Task 8; Task 7 attempt-2 report and its immutable native/4×/800 ms package.

Frozen approved-source observables before inspecting the provisional family:

1. rounded pink hair/head silhouette;
2. paired purple eyes and centered small mouth;
3. right-side rose, bow, and white-ribbon stack in the same order/connectivity;
4. hair overlap at the neck;
5. readable four-leg, no-tail body identity.

| Check | Expected observable | Concrete observation | Result |
|---|---|---|---|
| Exact complete-character keys | Seven entry and five settle 96x96 sources, no overlay family | Asset hashes match Task 7; runtime/direct-render tests select every source in the stated order; the visual tree contains exactly one `AlphaHitTestImage` | PASS at source/render-contract layer |
| Protected identity | Frozen head/face/hair/ornament/no-tail observables remain stable | Native and nearest-neighbor 4× attempt-2 strips were reopened: the head, paired eyes/mouth, rose/bow/ribbons, and neck overlap remain fixed while only lower-body length changes; automated protected pixels are exact | PASS for the exact asset family |
| Anchor and opacity | No extra whole-character motion, opacity dip, or ghost | Every direct-render drag key has opacity 1, body `ScaleY=1`, translation Y=0, breathing scale 1; the interpolated midpoint is one `Pbgra32` source | PASS at deterministic render layer |
| Entry/hold/settle order | Canonical/pending, seven-key entry, exact full hang, five-key settle, exact recovery | Controller progress and direct-render source assertions cover the full order; hold selects `entry-06`; settle starts with the identical hang hash and ends with the canonical hash | PASS at deterministic timing/mapping layer |
| Runtime motion feel | Responsive 140 ms stretch and 180 ms recovery with no perceived pop | No app/window playback was launched or observed; the existing 800 ms asset-only GIF is not the exact 140/180 ms runtime interpolation | UNVERIFIED |
| Windows interaction | No jump, bounded release, focus retention, click-through, capture cleanup | Logic proves same-tick position, exact grab offset, release clamp, and capture release; keyboard focus, click-through, DPI behavior, and user feel were not observed on Windows | UNVERIFIED |
| User acceptance | User approves this exact runtime result | No user observation or approval was requested or inferred | UNVERIFIED |

## Exact changed files

- `src/Dororong.App/Controls/DororongPresenter.xaml.cs`
- `src/Dororong.App/Interaction/DirectInteractionController.cs`
- `tests/Dororong.App.DirectInteractionRender.Tests.ps1`
- `tests/Dororong.App.Tests/Interaction/DirectInteractionControllerTests.cs`
- `tests/Dororong.App.Tests/Runtime/PetLoopDirectInteractionTests.cs`

This report is the only additional file in the separate report commit. The pre-existing unrelated working-tree modification to `src/Dororong.App/Controls/DororongPresenter.xaml` was not edited, staged, or committed.

## Concerns and self-review

- Task 7 attempt 2 remains provisional and unapproved. Runtime mapping does not upgrade that asset family to accepted art.
- The existing 96x96 family expresses most of the hang by redistributing lower-body mass inside the canvas. Whether it feels sufficiently long and cat-like at 140 ms is subjective and unverified.
- The controller returns to `None` at 180 ms; because the final settle key is byte-identical to canonical, the next ordinary idle render recovers the same source without a visual endpoint mismatch. Actual breathing resumption feel is unverified.
- The core preserves immediate pointer tracking and clamps on release; the focused integration test records the transient pre-release window position outside the work-area when the pointer is outside, matching existing core behavior. This task did not change that core boundary.
- Self-review checked that phase progress is monotonic and bounded, entry/hold/settle endpoints are deterministic, unavailable pointer samples retain the last pointer, facing locks at drag entry, direct frames override sleep/click/breathing transforms only while active, and no new general animation engine or overlay path was introduced.
- No production file outside Task 8 was modified. No asset bytes, `PetLoop` production code, core code, XAML, docs/spec, roadmap, or task ledger were changed.
