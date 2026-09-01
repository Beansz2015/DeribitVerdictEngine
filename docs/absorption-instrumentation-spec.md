# Absorption instrumentation — build spec

**Status:** SPEC. Buildable. **Authorised by:** **D-1** in the D-table at [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 — **ticked by the trader 2026-09-01**, whose definition of a tick is *"follow as recommended"*. **D-1 through D-5 are ticked. D-6 was split: D-6a and D-6b RULED 2026-09-01, D-6c OPEN, D-6d raised-not-ruled.**

⛔ **D-6c being open does NOT gate this build.** `AbsorptionSizeStart` and `AbsorptionSizeMin` are in the §5 field list regardless of how D-6c lands — **do not wait for it, and do not treat its absence as a missing input.**

**Implements:** [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5, which D-1 rules must ship **first and alone**.

**Author:** the seat that wrote [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md), 2026-09-01.

⚠ **This is the ONLY buildable item the ticked D-table produces.** D-2 is gated behind this build's data (~2 weekday-weeks). D-3, D-4 and D-5 are all *"leave it alone"* decisions and carry no work. **Do not extend this session into any of them.**

---

## 0. Implementer brief — model, effort, and where it slips

> ### Model: **Sonnet.** Effort: **HIGH.**
>
> **Why that tier.** **Every design decision is ruled below — there is no judgment left to make.** Each of the five fields threads through the same four types that five existing fields already thread through, so every mechanical piece has a working in-repo template two lines away from where you type. **That is what makes it Sonnet.**
>
> **Why HIGH and not medium.** The surface is wide and the failure is silent. **A CSV schema rotation touches 17 files that read the book**, and one of the writers is a twin that must move in lockstep. Nothing crashes when you miss it — the column simply carries wrong data forever, and the first person to notice is reading a study six weeks from now.
>
> **Where it will slip — three named traps, not a general warning:**
>
> | | Trap |
> |---|---|
> | **T1** | ⛔ **`tools/BacktestRunner/BacktestRowWriter.vb` is a SCHEMA TWIN of `AnalysisLogger.vb`.** It writes the same header and the same absorption columns. **Update it in the same commit or the two books stop being the same format.** It is in a different directory and will not appear in a search for the file you are editing |
> | **T2** | ⛔ **`tools/BacktestRunner/OverlapValidator.vb` carries a hardcoded `ColSpec` list** — a third copy of the schema, at lines 169–171 for the absorption columns. Its `ColIndex(name)` resolves against that list, **not** against the file's header. **Miss it and the overlap check silently compares the wrong columns** |
> | **T3** | ⚠ **You write the fixtures too.** A misunderstanding of `ReadSide` propagates into the test that was supposed to catch it. ⛔ **A60c's trap description in §5 was WRONG and is corrected there — do not trust a "build this input, watch it fail" instruction without mutating the code to confirm the input can fail.** Mutation-test every A60 fixture: break the thing it guards, watch it go red, restore |
>
> ⚠ **The fixtures cannot be relied on to catch T1 or T2 unless you write A60e**, which exists solely to compare the two writers' headers. **Write A60e first.**
>
> **Escalation trigger — stop and move to Opus/high if any of these appear:**
>
> - A reader in §4's audit list indexes columns **positionally past column 105** and cannot be made safe by R1's append rule.
> - `ReadSide` cannot get `nowMs` without changing a call site outside `Core/LevelAbsorptionTracker.vb`.
> - The header change makes any existing fixture fail. **That means an existing column moved, which R1 forbids** — do not "fix" the fixture.
>
> **Session plan: ONE session.** The build is wide but shallow, and splitting it separates the writers from the fixture that proves they agree. **Order within the session is fixed by dependency: §3 (types) → §4 (writers + audit) → §5 (fixtures), and A60e before the rest.**

---

## 1. What this builds

**Five new columns on `analysis_log.csv`. Nothing else.**

| Column | Source | Why — from [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5 |
|---|---|---|
| `AbsorptionEpisodeSec` | **NEW state** — see R2 | ⚠ **The single most valuable missing number.** §4.1's effect cannot be estimated without it. ⭐ **It may also be the instrument that diagnoses D-6d** — see the note below |
| `AbsorptionPullLB` | `SideState.PullLB` | Makes §4.2's residual testable. Today only their ratio survives |
| `AbsorptionPostLB` | `SideState.PostLB` | As above |
| `AbsorptionSizeStart` | `SideState.SizeStart` | The ratio's denominator, currently invisible |
| `AbsorptionSizeMin` | `SideState.SizeMin` | As above |

✅ **Four of the five already exist as live state** in [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:80-84`. **They are computed every fold and thrown away at the read boundary.** This build stops throwing them away.

⚠ **`AbsorptionEpisodeSec` is the only one that needs new state.** See R2.

> ## ⭐ `AbsorptionEpisodeSec` GOT MORE VALUABLE ON 2026-09-01. Do not treat it as the low-stakes column.
>
> **Measured that day:** `AbsorptionLevel` changes between **50.4 %** of adjacent ACTIVE row pairs, driven by HVN churn (`VPFRNearestHvnAbove` moves on 30.1 % of consecutive runs; `LastSwingHigh5m` on only 3.7 %). **Every re-map fires `CloseEpisode()` at [`../Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:265` and discards the whole episode.**
>
> ⭐ **So `EpisodeSec` will not merely describe episodes — it should EXPOSE whether they are being destroyed at run boundaries.** If the logged values cluster below one auto-run interval, that is the churn showing itself.
>
> ⛔ **This does NOT add work to this build, and you must not chase it.** Build the column exactly as specced. **It is flagged so the study that reads it knows what to look for**, and because it is a live candidate mechanism for the unruled D-6d — see [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §0.3.

---

## 2. Rulings — decisions this spec makes that §5 does not

### R1 ⭐ APPEND AT THE END OF THE HEADER. Do not group with the absorption siblings.

The five new columns go **after `SignalId`**, at positions 112–116. **Not** after `AbsorptionPullFrac` at 105.

⛔ **This is the most important ruling in the spec and it is counter-intuitive.** Grouping them with `AbsorptionSignal…AbsorptionPullFrac` reads better and **is wrong**: inserting at 106 shifts `PlacedTargetLong`, `PlacedStopLong`, `PlacedTargetShort`, `PlacedStopShort`, `InstanceId` and `SignalId` by five positions. **Any reader indexing those positionally breaks silently.**

✅ **Appending at the end cannot move an existing column.** Header readability loses; the 17 readers win.

### R2 `AbsorptionEpisodeSec` needs one new field and one signature change

- Add `Public EpisodeStartMs As Long = 0` to `SideState` in [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:75`.
- Set it at episode open, beside `side.LevelPrice = lvl` at `:294`. **The fold already has `tsMs` in scope.**
- Clear it in `CloseEpisode()` — the reset block at `:104` — `EpisodeStartMs = 0`, beside the other resets.
- ⚠ **`ReadSide` must take `nowMs`.** It is `Private Shared` with one call site (`Snapshot`, `:420-421`), and `Snapshot` already receives `nowMs`. **Thread it through; do not read a clock inside `ReadSide`.**
- `EpisodeSec = (nowMs - EpisodeStartMs) / 1000.0`.

### R3 All five are CSV-only. The live strip does not change.

⚠ **The engine display-string parity rule in `CLAUDE.md` applies and is satisfied by statement, not by an edit.** Absorption **does** have a rendered surface — `ComposeAbsorption` in [`UI/MainForm_LiveStrip.vb`](../UI/MainForm_LiveStrip.vb) `:270-273` renders `ABS↑ <level> (<ratio>×)`.

⛔ **Do not add any of the five to it.** These are diagnostic quantities for a study, not trader-facing signal. **State this reason in the commit message**, as the rule requires.

### R4 Empty when no episode

Follow the existing discipline exactly: the numerics are `Double?` on `IndicatorResults` and written through `InvOpt`, so they render empty when `HasEpisode` is false. ⚠ **`AbsorptionSideRead`'s own comment at `:436` is binding — `Active=False ⇒ every numeric is meaningless and must not be surfaced.**

### R5 The schema twin and the column-spec list move in the same commit

| File | What |
|---|---|
| [`AnalysisLogger.vb`](../AnalysisLogger.vb) `:108` header, `:295-299` values | The live writer |
| [`tools/BacktestRunner/BacktestRowWriter.vb`](../tools/BacktestRunner/BacktestRowWriter.vb) `:54` header, `:207-209` values | ⛔ **The twin. Same header, same order** |
| [`tools/BacktestRunner/OverlapValidator.vb`](../tools/BacktestRunner/OverlapValidator.vb) `:169-171` | ⛔ **Add five `ColSpec` entries, `ColKind.Muted`** — matching the existing absorption entries |
| [`tools/BacktestRunner/ReplayLoop.vb`](../tools/BacktestRunner/ReplayLoop.vb) `:470` | Sets absorption fields to `Nothing` on the replay path. **Add the five** |

### R6 Version history YES. `settings.json` bump NO.

- **No config keys are added or changed**, so no `settings.json` version bump. `CLAUDE.md` conditions the bump on config keys, and there are none.
- ✅ **A `docs/DeribitIndicatorProject.md` §15 entry IS required** — the logged output changes.
- ⚠ **State in that entry that this IS a schema rotation but NOT a comparability boundary.** Existing columns keep their positions and their meaning; the five new ones are empty for every pre-build row. **Rows before and after remain fully comparable** — unlike the v64 entry, which could claim `analysis_log.csv` was untouched and cannot be copied here.

### R7 Fixture family **A60**. No new hard constraint.

**A60 is next free — verified against `verify/ordercheck/Program.vb` this session (highest existing family is A59).**

⛔ **No `HC` is needed and none should be invented.** Hard constraints fence *settings prefixes* from the auto-tweaker. **This build adds no settings keys, so there is nothing to fence.** HC29 stays free.

### R8 ⭐ Add ONE comment while you are in the file — D-6a's ruling, at the line it explains

**D-6a was ruled 2026-09-01: the 0.30 / 0.10 pair is INTENDED, arm-early / measure-tight.** The write-up lives at [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3a.

⭐ **You are already editing `SideState` and the episode-open block. Put the reason where it prevents the recurrence**, immediately below the existing comment at [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:292`:

```
' Episode opens on the first in-proximity snapshot.
' [D-6a, ruled 2026-09-01] SizeStart samples the BAND (band_atr_frac) at the
' PROXIMITY (proximity_atr_frac) instant — arm-early / measure-tight, so the
' baseline is captured before price arrives. Collapsing the two shells shrinks
' the depletion denominator and inflates absorbRatio. See the proposal §4.3a.
```

⚠ **Why this is in the spec and not left to judgement.** The 2026-08-14 author wrote *"the author did not have this measurement"* and the 2026-08-19 check went looking for one. **There was never a measurement to find — the answer was this code path, and nobody re-read it.** A comment here is the cheapest possible guard against a fourth pass making the same move.

⛔ **Comment only. Do NOT change `proximity_atr_frac`, `band_atr_frac`, or any behaviour at that line.**

### R9 Reader audit is part of the build, not a follow-up

**17 files touch the book.** R1 makes appending safe *by construction* for positional readers, **but §5 of the proposal explicitly says this property "must be re-verified, not assumed."** See §4 below for the list and the check.

---

## 3. The build — types, in dependency order

The chain, verified this session:

```
SideState (private)
  → ReadSide()            Core/LevelAbsorptionTracker.vb:424
  → AbsorptionSideRead    :438
  → AbsorptionSnapshot    :449
  → IndicatorEngine.ClassifyAbsorption()   Core/Indicators_OrderFlow.vb:198
  → AbsorptionRead        :457
  → r.Absorption*         UI/MainForm_Analysis.vb:480-485
  → IndicatorResults      Core/IndicatorResults.vb:93-95
  → AnalysisLogger        :108 / :295-299
```

**Add the five to every structure in that chain.** ⚠ **`AbsorptionSideRead` and `AbsorptionRead` are `Public Structure` — adding properties is source-compatible, but every `New … With { … }` initialiser must be checked**, because an omitted field silently defaults to 0 rather than failing to compile.

⭐ **Follow `PullFrac` as your template.** It is already threaded end-to-end through all eight steps, and it is the closest analogue: a `Double`, episode-scoped, meaningless when idle.

---

## 4. Reader audit — required, and here is the list

**These 17 files read `analysis_log.csv`.** Confirm each either (a) indexes by header name, or (b) indexes positionally only at columns ≤ 111.

```
analysis/ForwardWindowJoiner.vb      Core/Settings/SettingsLoader.vb
AnalysisLogger.vb                     LivePerformanceTracker.vb
tools/AutoTweaker/ConditionsExtractor.vb   tools/AutoTweaker/TweakerConfig.vb
tools/BacktestRunner/BacktestProgram.vb    tools/BacktestRunner/BacktestRowWriter.vb
tools/BacktestRunner/CoverageReport.vb     tools/CeilingAudit/CeilingAuditProgram.vb
tools/CeilingAudit/CsvFeatureBuilder.vb    tools/WhatIfRunner/WhatIfProgram.vb
UI/MainForm_Calibration.vb            UI/MainForm_Layout.vb
UI/TweakSettingsForm.vb               UI/WhatIfLauncherForm.vb
verify/ordercheck/Program.vb
```

⚠ **A partial sample was taken this session and every guard found used `<`, not `=`** — `ForwardWindowJoiner.vb` `:230`, `ConditionsExtractor.vb` `:140`, `CoverageReport.vb` `:292-293`. ⛔ **That is a SAMPLE, not the audit. Do not treat it as coverage.** The proposal's §5 warning exists because this exact property was assumed once before.

**Record the audit result in the spec-back, one line per file.**

---

## 5. Fixtures — family A60

⚠ **Write A60e FIRST.** It is the only one that catches trap T1 and trap T2, and both are silent.

| | Fixture | Asserts |
|---|---|---|
| **A60e** | ⭐ **Writer-header parity** | `AnalysisLogger`'s header string and `BacktestRowWriter`'s header string are **identical**. ⛔ **Compare the strings, not the column counts** — two schemas can have equal counts and different order |
| **A60a** | Round-trip | All five values travel `SideState` → CSV row unchanged. **Assert on the values, not on the column being present** |
| **A60b** | Idle ⇒ empty | With no active episode, all five render as empty strings, not `0` |
| **A60c** | `EpisodeSec` measures and resets | ⛔ **CORRECTED 2026-09-01 — see the box below. The trap described here DOES NOT EXIST.** Assert the reset **directly**, by reading `EpisodeStartMs` after `CloseEpisode()`. ~~The input that makes this fail: open an episode, advance `nowMs`, close it via `CloseEpisode()`, re-open at the SAME level, read again. A build that stores `EpisodeStartMs` but forgets the reset returns the FIRST episode's elapsed time.~~ |
| **A60d** | Existing columns did not move | The header's index of `InstanceId` and of `SignalId` are **unchanged** from their pre-build values. **This is R1's guard** |

> ## ⛔ CORRECTION 2026-09-01, BY THE SPEC'S OWN AUTHOR — A60c's TRAP CANNOT OCCUR.
>
> **I wrote: *"a build that forgets the reset returns the FIRST episode's elapsed time."* It does not.**
>
> [`../Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) assigns `side.EpisodeStartMs = tsMs` **unconditionally** inside the episode-open block. **A re-open always overwrites any stale value, so the re-open leg is structurally blind to a missing reset.** ⛔ **A60c as originally specced would have PASSED a build with no reset at all.**
>
> ✅ **Proven by mutation, not argued** — with the reset deleted, the re-open leg still read the correct `0.5 s`; only a direct assertion on `EpisodeStartMs` fired. The implementing seat found this and widened the fixture.
>
> ⚠⚠ **This is the SECOND error of the same class in this spec** — acceptance item 4 in §7 is the other. **Both times I asserted a behaviour without reading the code path that implements it.** That is the exact failure this spec's own parent packet criticised in the proposal's author: *"the author searched for a number when the answer was a code path."* **Naming it here so the next spec I write gets the code read first.**
>
> ⭐ **The general lesson, and it is in my memory already: a trap description must name an input the trap can actually fail on.** Writing "build this input, watch it fail" gives the instruction the authority of a tested claim when it is only a guess.

⚠ **Fixture-literal provenance rule applies** (`CLAUDE.md`, RULED 2026-08-11). A60c passes a `nowMs` and a `depletion_floor_usd`. **Declare at each call site which you are asserting** — `depletion_floor_usd` is SHIPPED BEHAVIOUR and must be derived from `cfg`; the `nowMs` values are MECHANISM and a literal is correct, so say so and say why.

---

## 6. Out of scope — do not build these

- ⛔ **Any change to `window_sec` or to episode-cumulative pressing.** That is D-2, and D-2 is gated behind ~2 weekday-weeks of this build's data.
- ⛔ **Any change to `proximity_atr_frac`, `band_atr_frac`, `max_pull_frac`, or any other anchor.** D-3 and D-4 both rule them untouched.
- ⛔ **Any change to `scoring_enabled`.** D-5 keeps it `false`.
- ⛔ **Order-book capture.** Out of scope since v64 and unchanged.
- ⛔ **The `docs/absorption-mechanism-revision-proposal.md` §4.3 box (b) counting gap.** It is real and it is larger than the geometry, but it has no D-table row — see [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2 D-6d. **It is Opus/high work and a different session.**

---

## 7. Acceptance

| | Check |
|---|---|
| 1 | `dotnet build` clean. ⚠ If it fails naming a locked `.exe`, that is the running app, not a compile error — build `verify/ordercheck/OrderCheck.vbproj` instead |
| 2 | Harness green, including the five new A60 fixtures |
| 3 | `tools/checks/verify-gate.ps1` green |
| 4 | ⭐ **One live run, then read the new columns in the row.** ⛔ **CORRECTED 2026-09-01 — see the box below. The original condition was WRONG and would reject correct code.** Confirm they are empty when **`HasEpisode` is false** and populated when it is true. ⚠ **`AbsorptionSignal = NONE` with all five columns POPULATED is correct** — that is a D8-vetoed episode |
| 5 | The reader audit's 17 lines recorded |
| 6 | `docs/DeribitIndicatorProject.md` §15 entry written, carrying R6's rotation-not-a-boundary statement |

> ## ⛔ CORRECTION 2026-09-01, BY THE SPEC'S OWN AUTHOR — ACCEPTANCE ITEM 4 NAMED THE WRONG GATE.
>
> **It said `AbsorptionSignal`. The gate is `HasEpisode`** — [`../UI/MainForm_Analysis.vb`](../UI/MainForm_Analysis.vb) `:481`, `If absRead.HasEpisode Then`. **The two differ on exactly the case this build exists to instrument: a D8-vetoed episode is `NONE` AND populated.**
>
> ⛔ **A reviewer following the original wording would have REJECTED CORRECT CODE.** Caught by the implementing seat, not by me.
>
> ⭐ **AND ITEM 4 IS STILL NOT ENOUGH ON ITS OWN. One populated row does not discriminate all five columns.** The 2026-09-01 live row read `PostLB=0` and `SizeMin=0`:
>
> | | Why the row cannot discriminate it |
> |---|---|
> | **`SizeMin`** | It is 0, so `max(SizeStart − SizeMin, floor)` collapses to `max(SizeStart, floor)`. **A `SizeMin` stuck at zero is indistinguishable from a correct one** |
> | **`PostLB`** | It is 0, so `max(PostLB, floor)` returns the floor whatever the value is. **A `PostLB` stuck at zero is indistinguishable too** |
> | **`EpisodeSec`** | **No pre-existing derived column uses it.** There is nothing to cross-check it against |
>
> ✅ **`PullLB` and `SizeStart` ARE genuinely cross-checked** by that row, through `AbsorptionPullFrac`'s numerator and `AbsorptionRatio`'s denominator.
>
> ⛔ **So require a live row with `SizeMin > 0` AND `PostLB > 0` before calling item 4 met for all five.** ⚠ **No fixture can substitute** — a fixture builds both sides of the comparison from the same input. **This is waiting for data, not work.**

**Report back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).** ⚠ **Two documents** — this is a spec'd build handed to a reviewing seat, which is exactly the case that convention covers.

---

## 8. What this spec does not know

- ⛔ **Whether all 17 readers are safe.** §4 is a sample plus a list, not an audit. **The audit is the implementer's job and it is the likeliest place this build goes wrong.**
- ⛔ **How long the data wait is in practice.** D-1 says ~2 weekday-weeks. **That figure is the proposal's, carried over, not re-derived here.**
- ⛔ **Whether `EpisodeSec` will be usable once logged.** [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §0.3 measured that 89.3 % / 89.6 % of episodes occupy a single CSV row. ⚠ **A per-row `EpisodeSec` reads the episode's age AT THE POLL INSTANT, which is a real measurement and is not affected by that finding** — but **the distribution of episode LIFETIMES still cannot be recovered**, because the poll rarely catches an episode twice. **Say this in the study, not after it.**
