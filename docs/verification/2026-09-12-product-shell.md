# Product shell verification — 2026-09-12

Scope: the approved product-management shell, not installation or public release.
Behavior baseline remains the accepted pounce/head-cheek-only interaction build.
The implementation plan and progress ledger are in TASKS.md only.

## Implemented identity and lifecycle

- Product: 도로롱 (Dororong), publisher JOEWRKS, version0.1.0.
- Product candidate apphost name: Dororong.exe; internal assembly remains
  Dororong.App.dll with existing pack URIs.
- Same-user/same-Windows-session single-instance ownership; duplicates exit0
  without a new window, tray or dialog.
- Management tray: disabled 도로롱0.1.0 label, 로그 폴더 열기, 종료.
- Logs: LocalApplicationData/JOEWRKS/Dororong/logs, UTF-8 JSONL,
  at most1MiB per file, current plus2 rotated files. Records exclude exception
  messages, user paths, window/input content and pointer coordinates.
- No installer, service, autostart registration, network updater or upload.

## Verification evidence

- Task901 commit edab47d: compiled-stub behavioral RED6 failures/7 tests,
  identity RED1/1; focused Product8/8; full App828/828 Release. Independent
  task review approved spec and quality, with two non-blocking hygiene/coverage
  observations tracked in TASKS.md.
- Task902 commit bfa7d7d: compiled-stub RED9 failures/21 tests; exact PE version
  follow-up RED1/1; final focused23/23, Core241/241, App843/843 Release.
- RuntimeComposition Release All passed the existing startup/fatal and
  PetLoop timer/input/render/capture/cleanup seams.
- Task902 scoped review fix65f7aed staged tray acquisition and independent
  cleanup, and moved shell construction inside startup error handling.
  RED3 failures/17, GREEN18/18 and RuntimeComposition PASS; scoped re-review
  approved all four findings. Published apphost naming remains the packaging gate.
- Task902 PE metadata: ProductVersion0.1.0, FileVersion0.1.0.0, JOEWRKS,
  도로롱 (Dororong). The icon conversion is deterministic and derives from
  the canonical PNG without editing that source.

## Frozen art

SHA256 values remained unchanged through Task902:

| Asset | SHA256 |
|---|---|
|dororong-canonical.png|699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78|
|hunting.pbgra.gz|A64461C244D073822EEE4B2517D7E3B218A4CE88C8DC15F7CAE7A8DD2869212E|
|locomotion.pbgra.gz|03983B66D55CF2EC3B7A6B7CD0B78C5AC609FF41F442B0197909B74CDA723ECE|
|layered-pull.pbgra.gz|B2EEEDF800C66D47648346A51C69E1B484021475FC69B968CC6C8B92BD8E5893|

## Candidate boundary

Task903 commit24831ff created candidate
`artifacts/product-shell/candidate-20260911-155342/runtime` and sibling
`Dororong-win-x64.zip` (SHA256
`2A6D5ED1617DB790AD438F09A24F1430797ED0CF944B794D474BDB9A2E899011`).
Core241/241 and ordinary App848/848 passed. Explicit self-contained win-x64
.NET8.0.31 App849/849 passed, with a testhost loaded-coreclr path/hash assertion;
this is separate from the ordinary installed-runtime test run.

The controller independently reran the complete package validator on this
candidate: exact product metadata/icon/apphost binding, Core/Desktop/host8.0.31,
App/Core byte-for-byte parity with RID test output, ZIP round-trip paths/hashes,
and private-desktop native startup/duplicate/WM_CLOSE all passed. Owned test
PID27656 exited0; the duplicate exited0 and the existing PID14064 was preserved.
Loaded candidate coreclr SHA256:
`BBBCAEDB369617695280CAAA60CDA1ABA4FA3C5B557532A89D9F4A9174A5B692`.
No candidate process remained after the controller's check.

Task903 scoped fix9e525ea tightened final validation: runtime overrides and smoke
bypass are rejected; both reference DLLs come from the matching RID test output;
the apphost is recreated and independently compared against the pinned host pack.
All four findings passed scoped re-review. Updated candidate:
`artifacts/product-shell/candidate-20260911-161048/runtime`, sibling ZIP SHA256
`827E6BAAD473902E7A911943F026949DD3E4D0B08FA0808674AFFA2ACCB38662`.
A fresh explicit8.0.31 full App849/849 run covered its exact shipped DLLs:
App`AEACE912148D29DFF2EC3C9E060803E09600CE714EB635E2592D367C6E751B40`,
Core`F4104BA071D88D200008DA1C92B501A85F4578D7AEA633C604B7ACFBC48C3C99`.
Native and archive checks passed again after that packaging correction.

Package test output is intentionally local; no files were installed. Final
integration review is still pending in this record. FileDescription currently
remains Dororong.App despite the correct ProductName; final review will evaluate
the user-facing process-label implication.
The bundled .NET8 patch is pinned to8.0.31, verified against
[Microsoft's release page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
and the official NuGet runtime/desktop/host package indexes. This does not
install or update the computer's system runtime.

The existing development pet PID14064 was preserved and responding during the
checks. No second visible pet or tray was launched for the unit tests.
Real NotifyIcon tests used Visible=false. Visible tray/menu behavior, Explorer
restart recovery, post-exit icon ghosting, and prior game/Search/mixed-DPI
acceptance remain live checks. Automated tests do not substitute for them.

Installation/upgrade/uninstall, signing, and distribution-rights review remain
separate release gates. No remote push or public release was performed.
