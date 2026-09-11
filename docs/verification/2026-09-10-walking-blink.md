# Walking blink timing

User clarified both frequency and duration are too fast specifically while
walking. Seated blinking stays unchanged.

Cause: `PetBrain.GetPhase` repeats WALK every600ms. The locomotion eye bank used
phase .69.. .73, causing a24ms closed-eye flash every600ms. This reused a gait
clock as an eye clock.

Fix: a presenter walking-only elapsed clock, reset when walking is interrupted.
First blink after2s, closed360ms, repeats after5s of uninterrupted walking.
Bank selection no longer follows WALK phase. No changes to seated/idle blink,
head/art, walking displacement, breathing, sleep, perch or direct interactions.

Regression renders actual walking frames across11s with rapidly cycling core
phases. Before the fix:20 flashes instead of2. After:2 closed intervals with
320..400ms duration and4800..5200ms spacing. Focused18/18 passed; independent
scoped read-only review found no actionable findings. Core239/239 passed.

Candidate published separately to
`C:/Users/tjdwo/Downloads/doro/walk-blink-20260910/runtime`.
Tested/published module hashes match:

- App: `03315E6A0D0D2DB40AAD05C13C21478224BF58391646F0E6127A6691FCB28C5B`
- Core: `70B0C79196D22B093E93D3F3BDF6E0176D0C957B0AA8341FC4CC18F9874CB1CA`

Final App709/709 passed (2m15s), Core239/239:948 tests total. Verified exact old
PID42020/path/App hash before stop; CloseMainWindow returned false, so stopped
only that process. Previous runtime folder intact. New hidden PID44440 responds
and is the sole pet process; loaded App/Core paths and hashes match tested DLLs.
No commit/push.
Automated render tests do not claim live native mouse/visual acceptance.
