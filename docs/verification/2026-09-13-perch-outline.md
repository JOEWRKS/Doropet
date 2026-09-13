# Flutter foreground coverage and perched-art cleanup — 2026-09-13

User approved the diagnosed fixes after accepting the prior numeric stutter fix.
The original source09 asset and user files are unchanged.

## Flutter coverage

Four real-image regressions sample the exposed front paw at source(30,54), below
the left hair tip, for16/64/180/240ms. Original alpha there is0; the rotated paw
must cover it. All four fail with the old rectangular head restore, then pass
with the coverage-aware composition. The fix clears the base protected-head copy
before compositing arms and applies actual head alpha once, so transparent space
reveals the paw and opaque head texels remain exact. Pointer-pin restoration is
still last. The arm destination scan extends four rows upwards to avoid truncation.

Focused Flutter/PerchReadiness tests32/32 passed. Existing face/hair preservation,
torso-contour, alpha-premultiplication, pin and cycle checks remain unchanged.
Scoped independent read-only review found no issues. Full self-contained8.0.31
win-x64 App876/876 passed in2m29s (`verification/app-flutter-mask-green.trx`).
Black/white before and after comparisons were inspected from these ignored probes:

- `artifacts/repro/perch-outline-20260913/` references the frozen running candidate.
- `artifacts/repro/perch-outline-after-20260913/` references the tested new DLLs.

## Source09 candidate, not adopted

Built-in imagegen edit used `C:/Users/tjdwo/Downloads/doro/9.png` as target and
`9.jpg` as the pre-extraction supporting reference. Prompt requested surgical
removal of stray white/gray fringe and doubled-looking chin/paw outlines, repair
of the hair-tip gap, genuine transparent alpha, original100x100 registration,
and preservation of face/eyes/hair/ribbon/proportions/native stroke/intentional
right-side ledge cutoff; explicitly prohibited redesign/new body/legs/tail/text.

Output is `C:/Users/tjdwo/.codex/generated_images/01a03970-f3b0-7e43-8f9d-62ec267475ae/exec-e5777221-f062-4814-9650-56704287bc50.png`.
Inspection found changed stroke weight and accessory shape. Rejected: no product
reference, source overwrite or package inclusion. The user was asked whether to
switch to direct original-pixel/alpha cleanup instead of AI regeneration.

## Delivery boundary

At the mask-only handoff, candidate PID15784 remained untouched. No installer
promotion, commit or push. Source09/live acceptance was not inferred from tests.

## Follow-up: user-approved direct pixel / alpha repair

User explicitly approved original-pixel/alpha processing instead of imagegen.
`PerchExpressionFrames.CleanOpen` operates on an immutable copy of the existing
embedded100x100 source, with no static image replacement or redraw:

- Fill only zero-alpha texels in the small x49..51/y59..63 lower-hair/chest patch.
- Remove faint white matte and recover lower-contour ink colour/coverage from
  nearby opaque stroke texels, preserving luminance on white while eliminating
  the pale halo on black. Nearly opaque white body pixels remain intact.
- All source alpha255 pixels and all rows above57 remain byte-identical.
  Open, blink variants, and registered cheek capture share the corrected copy.
  Dimensions, facing, registered grip and uppermost visible extent are unchanged.

After WPF fixture initialization was corrected (initial pack-URI error was not
counted as RED), nine literal hole/fringe regressions failed and the preservation
control passed. Cleanup fixed them. A nearly-opaque-white negative control then
failed (original251,251,251,253 was cleared); limiting white removal to alpha<128
fixed that edge case. Focused Perch/Flutter112/112 passed. An unnecessary probe
for a supposedly missing dark chin texel passed against existing code and was
removed; that texel already exists in the embedded source, so no ink redraw made.

Registration's old hard-coded raw2849-pixel count was replaced with input/output
coverage equality; the per-pixel mapping and blink-difference checks remain.
The raw resource's SHA256 preservation test remains unchanged.

Scoped independent read-only review reported no findings. The final black/white,
native/enlarged comparison at `artifacts/repro/perch-pixel-cleanup-20260913/bin/Release/net8.0-windows/win-x64/before-after.png`
was inspected:117 texels changed,61cleared,7filled,0opaque-source texels changed.
This is bounded matte/hole cleanup, not a claim of complete live visual acceptance.

User normally exited15784 (ㄱㄱ); empty inventory and Stopped03:16:59KST verified.
Fresh candidate built at `artifacts/product-shell/candidate-20260913-perch-outline-01`.
ZIP SHA256 `3824988C509A8C40B26FE6F270302608E167EABD99B35C1D8F1C0E47626F1CF2`.
Full self-contained8.0.31 win-x64 App887/887 passed in2m48s. Strict package/native
smoke passed (owned32240 normalWM_CLOSE exit0; duplicate0); exact tested RID parity.
App DLL SHA256 `7745949AA5A825796BA0A968DF271903FAF269524CB350BBD85C2F4A8CEA77A6`.
Core DLL unchanged `2AC994181A0498B57D210D42AFD86ADBEBA7FC345A8E0916E2C7DBE50494B420`.
Raw source09 hash unchanged `3CE4BE5308759D35BA828237208521A085378180AAD035CA0474DA3E551F57C2`.

Fresh empty pet inventory before normal launch as PID30328 at03:20:33KST;
exact executable path and Started03:20:34KST verified. User live visual trial is
pending. Previous candidates, installed directory, original source art preserved;
no installer promotion, commit or push.

## Rejected live trial: residual contour and blink parity

User reports the defect persists with screenshot
`codex-clipboard-cb81b252-e54b-4080-834e-877e683a4bd2.png`, and requests that
perched blinking omit the intermediate eye frame and match ordinary blinking.
The previous live trial is not accepted. The previous all-opaque-texels-preserved
repair was insufficient; white-only hole filling also missed real hair colour.

PerchExpressionMotion had retained a separate3600ms cycle with70ms squint stages
and90ms closed stage. It now exposes only EyesClosed on a5000ms cycle with
closed2000..2360ms, matching the standing/sitting/walking clock. Perched squint
generation and selection are removed; entry bob and cheek eye-reset unchanged.
Actual WPF source-selection regression covers every millisecond of two cycles in
both facings: RED2 then GREEN. Proof labels updated to actual new boundaries.

Ignored diagnostic `artifacts/repro/perch-residual-20260913/` compares the screenshot,
source09, registered canonical, supplied JPG and repeated Render+ApplyEdgePerch
frames. The lower-head shape remains present in source09, not introduced only by
repeated presentation. Canonical has different lower-chin and inner-hair-tip ink.
The repair replaces only x28..49/y53..59 chin and a tapered x48..60/y52..65 hair
patch with registered canonical texels, flattened once on the white chest. It
does not overlay an additional head. Old white-only hole filling is superseded.
Upper face, accessory, other opaque outlines, grip geometry and raw asset remain
unchanged. Source09 itself is not overwritten.

Three independent registered-reference texels RED3/3 then GREEN; former hole
fixtures preserve coverage without incorrectly requiring hair to be white.
Focused Perch/Flutter117/117 passed. Independent scoped read-only review reports
no findings. Rendering inspection is not a substitute for user live acceptance.
Full verification and fresh candidate packaging are in progress;30328 untouched.

Final enlargement exposed an additional residual old tip at(47,64), RGB125 where
registered canonical hasRGB252. Fourth literal fixture RED1/3passed; extend only
the lower patch's minimum x48 to47 for y60..65. Focused118/118 passed; follow-up
read-only review no findings. Previous full892/892 passed3m5s, but final full suite
is rerun after this final change. Candidate01 is superseded, not launched.
User normal exit30328 confirmed by Stopped03:35:52KST and empty process inventory.
Candidate02 App DLL matches final test RID hash
`BB474123C814F933E711E4E19FB1D9AAA8B5AE50783C90321306EF442D5C9DEB`.

Final full App893/893 passed3m16s; TRX is
`artifacts/repro/perch-residual-20260913/verification/app-perch-blink-contour-final.trx`.
Strict package/reference/runtime/archive/native smoke passed: owned34800 normal
WM_CLOSE exit0; duplicate0. Archive SHA256
`C547CC925E379C4D5251867B5180E3C0CDEC0DE512A526A6E45E5647201BD6B3`.
Core and raw source09 hashes remain unchanged. Fresh empty process inventory
before normal launch of candidate02 as11816 at03:42:43KST. User visual acceptance
remains open; no installer promotion, commit, push or original-source overwrite.
