# AutoTweaker Weekday-Only Row Filter — Implementer Spec-Back

**Date:** 2026-08-25
**Seat:** implementer (Sonnet 5, effort medium, per the spec's own §0 recommendation).
**Build spec:** [`autotweaker-weekday-filter-proposal.md`](autotweaker-weekday-filter-proposal.md) — D-1–D-5 all ticked as recommended, trader-directed 2026-08-25; §4's D-table is the decision of record.
**Status:** **BUILT — local, uncommitted.** Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build **0/0** Release (each run separately); harness **ALL PASS**, A1–A58c unregressed + A59a–e; `tools/checks/verify-gate.ps1 -Mode local-fast` **GATE PASSED** (harness ALL PASS, display-parity clean, no engine-path change). **Routes to the orchestrator seat for independent review before commit/push.**

**Three things to put your eyes on** (§3): (1) the weekday filter is unconditional on the **no-population-filter path too**, not just the configured-population path the spec's §3.1 snippet shows in isolation; (2) `CountPopulationRows` and its call site went further than a literal "mirror the term" — the `pop Is Nothing` early-return that used to skip the UI's row count entirely is gone; (3) the existing `A15e` fixture's literal expected key changed as a forced consequence of D-1, not as a new fixture — flagging it as a deviation rather than silently editing another item's test.

---

## 1. What was built (every D-table item)

| D-table item | Ruled | Done | Where |
|---|---|---|---|
| **D-1** unconditional weekday filter, no new settings key | (a) UNCONDITIONAL | ✅ | [`AutoTweakerCore.vb:120-136`](../tools/AutoTweaker/AutoTweakerCore.vb) — new `MatchesWeekday` folded into the load-time `Where`; `populationKey` gains `"\|WD"` at [`:164`](../tools/AutoTweaker/AutoTweakerCore.vb) |
| **D-2** the weekend-gap burn on `CrossesSessionBoundary` | (a) ACCEPT, record the observable | ✅ accepted, untouched | `CrossesSessionBoundary` unchanged; §15 row states the raised P9 trigger rate |
| **D-3** the `ConditionsExtractor` leak | (a) FIX IT HERE | ✅ | [`ConditionsExtractor.vb:86-92,132-149`](../tools/AutoTweaker/ConditionsExtractor.vb) — re-derives the weekday test from its own `Timestamp` column before counting a row, fail-closed on a missing column (§10, F1) |
| **D-4** telemetry convention | (a) LOCAL COUNTER + CONSOLE | ✅ | `weekdayExcludedCount` extends the existing population line, [`AutoTweakerCore.vb:125-141`](../tools/AutoTweaker/AutoTweakerCore.vb), scoped to the population per §10 F2 — no new `LoadStats` object |
| **D-5** version bump / §15 row | No bump, ONE §15 row, no display-parity obligation | ✅ | [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15, new top row; `verify-gate.ps1` confirms "no engine-path change" |
| UI lockstep (§3.3) | Mirror the key term + the weekday test in `CountPopulationRows` | ✅, broader than literal — see §3.2 | [`TweakSettingsForm.vb:128-131,281-330`](../UI/TweakSettingsForm.vb) |
| Fixtures `A59a`–`A59e`, mutation-proven for `A59c`/`A59e` | Family A59, next free | ✅ | [`Program.vb:68-72,1389-1560`](../verify/ordercheck/Program.vb) |

**Engine untouched.** No `settings.json` change, no scoring change — this alters which CSV rows the tweaker's own tooling reads, never a vote.

---

## 2. Mechanism as built

`AutoTweakerCore.RunAsync`, fixed mode: `filtered = allRows.Where(Function(r) MatchesWeekday(r) AndAlso (pop Is Nothing OrElse MatchesPopulation(r, pop, settings)))`. Both predicates are `Friend Shared` so the harness exercises them directly.

- **`MatchesWeekday(r As CsvRow)`** — `MinValue` guard first (`DateTime.MinValue.DayOfWeek` is Monday, so an unparsed row cannot silently pass as a valid weekday), then Saturday/Sunday exclusion. Matches `CsvFeatureBuilder.vb:198-203`'s guard order deliberately — one convention across the three surfaces named in the weekday-scope ruling.
- **`populationKey`** gains a `"|WD"` suffix on both branches (`"NY|1"` → `"NY|1|WD"`, `"none"` → `"none|WD"`), so the string changing alone trips the pre-existing re-seed-on-change gate once, moving `LastEvaluatedRowIndex` to the post-filter `filtered.Count` without evaluating that round — the same mechanism the (session × resolution) filter used at its own introduction (`A15e`'s original scenario).
- **`ConditionsExtractor.Extract`** re-reads raw CSV lines by absolute index inside `[round.WindowStartRow+1, round.WindowEndRow+1]`; it does not inherit `filtered`'s exclusion. It now resolves its own `Timestamp` column once (`hasTimestampCol`) and applies the identical `TryParseExact`/`MinValue`-first/weekend test per line before counting the row into any aggregate — closing the leak D-3 named, and incidentally closing the pre-existing (session × resolution) population-filter leak in the same loop, since that filter had the same blind spot and nobody had noticed. **Fails closed** if the header carries no `Timestamp` column at all (§10, F1) — no row is admitted unfiltered.
- **Telemetry** — the population console line no longer gates on `pop IsNot Nothing`; it always prints, now carrying a `weekdayExcludedCount` **scoped to the population-eligible rows** (§10, F2) alongside the existing kept/total pair.

Confirmed live in the `A59b` fixture output path (same shape as the original `A15e` confirmation; wording below is post-§10 F2):
```
[AutoTweaker] population filter NY×1m, weekday-only — 3/5 rows in population (2 of the otherwise-eligible rows excluded as weekend/unparsed).
[AutoTweaker] INFO — population filter changed ('NY|1' → 'NY|1|WD'); re-seeding
              LastEvaluatedRowIndex to filtered.Count=3. ...
```

---

## 3. Decisions / deviations worth your eyes

### 3.1 The weekday filter is unconditional including the no-population-filter path

The spec's §3.1 code snippet shows the predicate in isolation; §3.1's prose offers folding it "into `MatchesPopulation`… or add a sibling applied in the same `Where`." Folding into `MatchesPopulation` was not available, because `MatchesPopulation` is only ever called inside the `pop IsNot Nothing` branch of the original ternary (`filtered = If(pop Is Nothing, allRows, allRows.Where(MatchesPopulation)...)`) — doing that would leave the no-filter path unfiltered by weekday, contradicting D-1's own word "unconditionally."

**My read:** D-1's ruling text — *"the weekday-scope ruling is unconditional; a key on the one surface that writes `settings.json` is a switch that can be flipped back"* — settles this: the filter must apply whether or not a population filter happens to be configured. I restructured the whole `filtered` assignment (§2 above) so both branches of the old ternary collapse into one `Where` with the weekday test always active, and changed the `"none"` key branch to `"none|WD"` for the same re-seed reason. **This is the correct reading of D-1, but it is my inference — the spec's own snippet does not show it, and I flag it because it means a config with no `population_filter` block (never seen live; `state.json` currently shows `"NY|1"`) now behaves differently than it did before this change, not only the configured NY×1 path the spec's worked examples all use.**

### 3.2 `CountPopulationRows` — mirrored broader than "the term," to keep the UI answering the same question as the core

§3.3 asks to "mirror the term into `TweakSettingsForm.vb:126-128`" (the key) "and the weekday test into `CountPopulationRows`." The function's pre-existing shape returned `CountCsvRows()` immediately when `pop Is Nothing` — a raw, weekday-unaware count. Given §3.1 above, that early return would now silently disagree with a `filtered.Count` that the core computes as weekday-filtered even in the no-pop case.

**What I did:** removed the `pop Is Nothing` early return; `CountPopulationRows` now applies the weekday test unconditionally and applies the (session × resolution) narrowing only `If pop IsNot Nothing`. The call site's `popRowCount` (`TweakSettingsForm.vb:128`) changed from `If(pop Is Nothing, currentRowCount, CountPopulationRows(pop))` to always `CountPopulationRows(pop)`. **This is broader than a literal word-for-word mirror — orchestrator ask: confirm this reading is what "or the UI shows Re-seed pending permanently and its row count disagrees with the core" was asking for**, since the no-pop UI path was not previously exercised against a live config and I have no observed regression to point to, only the reasoning above.

**One thing I deliberately did NOT touch, in the same spot:** the re-seed-pending banner itself (`TweakSettingsForm.vb:146`, `If pop IsNot Nothing AndAlso (state.PopulationFilterKey <> popKey OrElse ...)`) still gates on `pop IsNot Nothing`. So in the no-pop case, the one-time `"none"` → `"none|WD"` re-seed the core performs on first run after this ships will not show as "Re-seed pending" in the UI — the accumulating-rows count will just look like it moved. Given `pop` is always configured in the live config today, this has no current effect, and touching that gate risked widening the change into logic the spec did not name. **Recorded rather than silently left**, in case a future no-filter config makes it visible.

### 3.3 `A15e`'s literal expected key — updated, not left broken

D-1's own text says plainly *"the key STRING changes."* The pre-existing `A15e` fixture (`Program.vb:1362`, before this change) asserted `st.PopulationFilterKey = "NY|1"` verbatim — a direct, unavoidable casualty of the `"|WD"` suffix. Its five CSV rows are all on 2026-01-01 (Thursday), so `filtered.Count` is unaffected — only the key string moves. **I updated the assertion to `"NY|1|WD"`, updated its `Check` label, and added a comment naming why**, rather than leave a fixture that fails without redress or silently rewrite it without a trace. This is a different act than writing a *new* fixture — it is required maintenance forced by this spec's own D-1 — but I flag it because it means the acceptance criterion "A1–A58c unregressed" is true only *after* this one-line update, not against the fixture as it stood before this commit.

### 3.4 `A59c`'s design — asserting on the absolute row span, not just the outcome string, to make it mutation-sensitive

§6's fixture table asks `A59c` to assert "a window spanning a weekend emits `SKIPPED_SESSION_BOUNDARY`." An assertion on the outcome string alone would pass even if the weekday filter were reverted — with the CSV I built (Fri/Sat/Sun/Mon, one row per day, same hour), removing the weekday exclusion still produces a `SKIPPED_SESSION_BOUNDARY` on a *different* adjacent pair (Fri→Sat, still ≥24h), so an outcome-only check would not be mutation-sensitive to the guard it claims to test. I additionally asserted the exact `RoundSummary.WindowStartRow`/`WindowEndRow` (the absolute CSV indices of the Friday and Monday rows specifically), which only match when the weekend rows are actually excluded from the window slice — and confirmed by reverting the guard and watching this specific assertion fail (§5).

### 3.5 Telemetry line is now unconditional (minor)

D-4 says "extend the existing line." The existing line was itself gated on `pop IsNot Nothing`. Since the filter is now unconditional (§3.1), I removed that gate too, so the population line prints on every fixed-mode run, including the previously-silent no-filter case. Cosmetic — one more console line per run in a path that was already the deprecated/unused one — but it is a behavior change beyond the letter of "extend," named for completeness.

### 3.6 Doc updates beyond the spec's named acceptance criteria

D-5 asks for exactly one `DeribitIndicatorProject.md` §15 row, which I added. I additionally updated [`trader-tick-queue.md`](trader-tick-queue.md)'s "Weekday filters — 3 surfaces" row to mark the `AutoTweaker` surface done, leaving `LivePerformanceTracker` and `AnalysisRunner`/`WhatIfRunner` open — not named in the spec's own §7 acceptance list, done per `CLAUDE.md`'s standing instruction to keep that queue accurate rather than let it drift stale (the file's own history records a sweep that found stale rows before). **Orchestrator ask: confirm the queue-row wording is acceptable**, since it is the one edit in this build not directly traceable to a D-table line.

---

## 4. Mutation-proof verification (§6's hard requirement)

For `A59c` and `A59e`, §6 requires reverting the guard under test and confirming the fixture fails. I did this for both named fixtures plus `A59d` (not required, done for extra confidence given D-3 was the spec's own "does not achieve its own purpose without it" item), then restored the correct code and re-ran the full harness to confirm `ALL PASS` again.

| Fixture | Guard reverted | Result before revert | Result after revert | Restored, re-ran full harness |
|---|---|---|---|---|
| `A59b` (incidental — same revert as `A59c`) | `MatchesWeekday(r) AndAlso` removed from `filtered`'s `Where` | PASS | **FAIL** — `idx=5` instead of `3` | ✅ ALL PASS |
| `A59c` | same revert | PASS | **FAIL** — `end=1` instead of `3` (window slid onto Fri/Sat, not Fri/Mon) | ✅ ALL PASS |
| `A59d` | the weekday re-derivation block removed from `ConditionsExtractor`'s loop | PASS | **FAIL** — `RegimeMix='UP:50\|DN:0\|RB:50\|TR:0'` instead of `UP:100\|...` | ✅ ALL PASS |
| `A59e` | `MinValue` guard removed from `MatchesWeekday` (dow computed first) | PASS | **FAIL** — malformed row admitted as Monday (`row1Match=True`) | ✅ ALL PASS |

Each revert was a temporary in-place edit to the shipped file, rebuilt, run, then restored from a backup copy before the next mutation — no mutation logic is left in the shipped code or the fixtures themselves.

---

## 5. Acceptance results

```
dotnet build DeribitVerdictEngine.sln                    → Build succeeded, 0/0
dotnet build tools/AutoTweaker/AutoTweaker.vbproj         → Build succeeded, 0/0
dotnet build tools/WhatIfRunner/WhatIfRunner.vbproj       → Build succeeded, 0/0
dotnet build tools/CeilingAudit/CeilingAudit.vbproj       → Build succeeded, 0/0
dotnet build tools/BacktestRunner/BacktestRunner.vbproj   → Build succeeded, 0/0
dotnet build verify/ordercheck/OrderCheck.vbproj          → Build succeeded, 0/0
dotnet run --project verify/ordercheck                    → ALL PASS
tools/checks/verify-gate.ps1 -Mode local-fast             → GATE PASSED
  (build ×5 OK · harness ALL PASS · display-parity: no snapshot/card drift · version-bump: no engine-path change)
```

**A59 coverage:**
- **A59a** — 5 rows spanning Thu–Mon; `MatchesWeekday` keeps exactly 3, no Sat/Sun survivor.
- **A59b** — pre-seeds `state.json` with the **live pre-change key** `"NY|1"` (not a synthetic stale key); the `"|WD"` change re-seeds `LastEvaluatedRowIndex` to `filtered.Count=3` exactly once, `INELIGIBLE`, nothing evaluated.
- **A59c** — a Fri/Sat/Sun/Mon CSV with `WindowSizeVerdicts=2`; filtered-adjacency between Friday and Monday trips `SKIPPED_SESSION_BOUNDARY`, pinned via the absolute `WindowStartRow=0`/`WindowEndRow=3` span (§3.4).
- **A59d** — `ConditionsExtractor.Extract` over a 4-row span containing weekend lines; regime mix reads `UP:100` (2 kept rows), not `UP:50|RB:50` (4 rows including weekend).
- **A59e** — one well-formed Monday row + one row with an ISO `T`-separator timestamp `TryParseExact` rejects; the malformed row parses to `DateTime.MinValue` and `MatchesWeekday` excludes it, not admits it as Monday.

**`A15e` re-verified** under its updated assertion (`"NY|1|WD"`) — see §3.3.

---

## 6. Out of scope (confirmed not touched — proposal §8)

- **P9** (`SKIPPED_SESSION_BOUNDARY` advancing by a full window rather than to the boundary) — untouched; this build raises its trigger rate, stated in the §15 row per the proposal's own requirement.
- **`CrossesSessionBoundary` itself** — byte-identical; D-2(a).
- **The `TryParseExact` vs `TryParse` split** between `AutoTweakerCore`/`ConditionsExtractor` and `TweakSettingsForm.CountPopulationRows` — both sides now run the weekday test, each keeping its own pre-existing parse path, per the proposal's explicit "do not unify them in this spec."
- **`FailureRateMatrix.Compute` called without the `resolution` argument** — unrelated, flagged not fixed, per the proposal.
- **`LivePerformanceTracker` and `AnalysisRunner`/`WhatIfRunner`** — the other two weekday surfaces named in [`trader-tick-queue.md`](trader-tick-queue.md)'s row; not opened here.
- **The local-time question (§2.9)** — settled REFUTED before this build started; nothing owed to this spec from it.
- **The `TryParseExact`/`Unspecified` vs `TryParse`/`AssumeUniversal|AdjustToUniversal` divergence now reaches DAY, not just HOUR (N1, orchestrator review 2026-08-25).** Before this build the split between `AutoTweakerCore`/`ConditionsExtractor` and `TweakSettingsForm.CountPopulationRows` could only disagree on session *hour*; the weekday test now rides the same parse, so a timestamp carrying an offset could in principle land on a different *day* in the UI than in the core. **Unreachable with the current writer** (`AnalysisLogger.vb:180` emits a bare `yyyy-MM-dd HH:mm:ss`, no offset), named here so the next seat sees it before assuming otherwise. Same "do not unify them in this spec" boundary as the item above.

---

## 7. Orchestrator asks (summary)

1. **§3.1** — confirm the unconditional-including-no-pop reading of D-1 (the `filtered` `Where` restructure and the `"none"` → `"none|WD"` key change).
2. **§3.2** — confirm `CountPopulationRows`/`popRowCount` going beyond a literal "mirror the term" is the intended fix, and sign off on leaving the re-seed-pending banner gated on `pop IsNot Nothing` untouched (currently invisible in practice, since `pop` is always configured live).
3. **§3.3** — confirm updating `A15e`'s literal key in place (rather than leaving it failing, or writing around it) is the correct handling of an existing fixture broken by this spec's own D-1.
4. **§3.6** — confirm the `trader-tick-queue.md` row wording is acceptable as an out-of-D-table addition.

---

## 8. What I did not verify, and cannot from here

- **No live/supervised run.** Everything above is fixture- and gate-verified against synthetic CSVs and temp `state.json`/`settings.json` files; no real `analysis_log.csv` with genuine historical weekend rows was run through the tweaker, and the tweaker is data-gated and has never fired live (per the proposal's own §1, `last_run_outcome=SKIPPED_INSUFFICIENT_TIER`). D-2's own observable — the count of `SKIPPED_SESSION_BOUNDARY` entries after the first weeks of running — cannot be produced synthetically and is explicitly deferred to a post-ship read, not this build.
- **No WinForms UI screenshot/interaction check.** `TweakSettingsForm.vb`'s status label and `CountPopulationRows` were read and reasoned about, and the harness's fixtures don't reach WinForms-coupled code (same boundary every other AutoTweaker spec-back in this repo notes) — the actual rendered status string after this change was not visually confirmed against a real settings/state pair.
- **`prepush`/`ci` gate modes not run.** Only `local-fast`; `prepush` needs a committed diff range this uncommitted work does not have (same constraint noted in the v68 §15 row for the prior build). The version-bump check that ran is `local-fast`'s own ("no engine-path change"), not the stricter committed-diff check.
- **Did not re-derive or re-measure anything named in the proposal's own §2** (the file:line map, the UTC-timestamp refutation) — carried forward from the spec as already verified there, not independently re-checked here.

---

## 9. ✅ ORCHESTRATOR REVIEW — **ACCEPTED, clear to commit.** Opus, effort high, 2026-08-25

**Verdict: ACCEPT.** Three findings, all LOW severity, **none blocking**. The escalation trigger correctly did not fire — `RoundSummary`'s schema is untouched and `CrossesSessionBoundary` is byte-identical.

### 9.1 Independently verified — re-run, not read

| Claim | How I checked it | Result |
|---|---|---|
| Builds 0/0 Release | Ran `OrderCheck.vbproj -c Release` | ✅ 0 errors |
| Harness ALL PASS | Ran it | ✅ **300 checks, 0 failures** |
| ⭐ **A59a–e actually EXECUTED** | `grep -cE '^PASS +A59'` | ✅ **5 of 5.** ⚠ **Asserted rather than assumed — a harness that ran zero A59 fixtures also prints ALL PASS** |
| `A15e` runs under the updated key | grep | ✅ `PASS A15e … ASIA\|3 → NY\|1\|WD` |
| **A59e's mutation claim** | **Deleted the `MinValue` guard myself, rebuilt, re-ran** | ✅ **FAIL `row1Match=True`** — the trap reproduces exactly |
| `CrossesSessionBoundary` untouched (D-2) | `git diff` | ✅ byte-identical |
| §15 row names P9 and states the parity exemption | grep | ✅ P9 twice, parity three times |
| Tree restored after my mutations | diffstat + md5 | ✅ identical to pre-review |

⚠⚠ **A correction on my own process, recorded because it is this session's own lesson.** My first attempt at the `A59e` mutation used a `perl` substitution that **silently did not apply** — the guard was still in place and `A59e` passed. **I nearly reported that as independent confirmation.** It was a check reporting a result it never performed. The re-run asserts the mutation LANDED before building. **Assert the check ran — including your own.**

### 9.2 Findings

**F1 — the two halves of the D-3 fix have OPPOSITE failure directions.** LOW.
`AutoTweakerCore.MatchesWeekday` fails **closed** (`MinValue` → exclude). `ConditionsExtractor`'s new block is wrapped in `If idx.Timestamp >= 0 AndAlso idx.Timestamp < parts.Length Then` and fails **open** — if the column cannot be resolved, the guard is skipped and weekend rows re-enter, which is the leak D-3 exists to close.
⭐ **I predicted this was uncovered and TESTED it — I was wrong.** Renaming the header case to `"TimestampZZ"` makes `A59d` **FAIL** (`RegimeMix='UP:50|DN:0|RB:50|TR:0'`). **The harness pins it.** The residual is the production asymmetry only. **Fix if convenient:** resolve the column once outside the loop and fail closed, rather than per-row and fail open. **Not a blocker.**

**F2 — `weekdayExcludedCount` is computed over the wrong denominator.** LOW, console-only.
`allRows.Where(Function(r) Not MatchesWeekday(r)).Count()` counts weekend rows across the **whole file**, not within the population. The line reads `{filtered}/{allRows} rows in population ({excluded} excluded as weekend/unparsed)` — on a live `NY|1` config those three numbers do not reconcile (e.g. 100 total, 40 filtered, 20 weekend: `100 − 20 ≠ 40`). ⚠ **The fixture is structurally blind to this** — its rows are all in-population, so the two definitions coincide exactly, the same shape as the `A48` family being blind to the same-millisecond drop. **Either scope the count to the population, or reword the line so it does not read as a decomposition.**

**F3 — the queue row you edited still carries the claim this spec corrects.** TRIVIAL.
`trader-tick-queue.md`'s "Weekday filters — 3 surfaces" row still reads *"**verified never fired** (no tweaker state file, `settings_snapshots/` empty)"*. **There is a state file** — the spec's own §1 corrects this. You appended the DONE marker to that row without touching the stale clause beside it.

**N1 — a note, not a finding: the parse-path divergence now reaches DAY, not just HOUR.**
Core uses `TryParseExact`/`Unspecified`; the UI uses `TryParse`/`AssumeUniversal|AdjustToUniversal`. **You correctly did not unify them — the spec forbade it.** But before this change the divergence affected only the session *hour*; the weekday test now rides on it, so a timestamp carrying an offset could land on a **different day** in the UI than in the core. **Unreachable with the current writer** (`AnalysisLogger.vb:180` emits bare `yyyy-MM-dd HH:mm:ss`). Worth adding to the out-of-scope list so the next seat sees it.

**N2 — same fail-open class in the UI.** `CountPopulationRows` returns `CountCsvRows()` (the **raw** count) when `tsIdx < 0`, while the core would return **0** in that state (every timestamp `MinValue` ⇒ every row excluded). Maximum disagreement, in the reassuring direction. Unreachable in practice; named for symmetry with F1.

### 9.3 Your four asks — answered

1. **§3.1 unconditional-including-no-pop — ✅ CONFIRMED, correct reading.** D-1's ruling text is *"unconditional"*; folding into `MatchesPopulation` would have left the no-pop path unfiltered, contradicting it. The `"none"` → `"none|WD"` key change is required for the same reason. ⭐ **A consequence you did not claim but which is real: when `pop Is Nothing`, `filtered` used to be the SAME OBJECT as `allRows`; it is now always a new list.** That removes an aliasing hazard — an improvement, not a regression.
2. **§3.2 `CountPopulationRows` broader mirror — ✅ CONFIRMED.** If the core filters unconditionally, the UI must too, or the status line lies about a number the trader reads to decide whether to run. **Leaving the re-seed banner gated on `pop IsNot Nothing` is ACCEPTED** — widening it would have entered logic the spec did not name, and it is invisible while `pop` is always configured. See N2 for the residual.
3. **§3.3 `A15e` updated in place — ✅ CONFIRMED, and correctly flagged.** ⭐ **The distinction that makes this legitimate is worth stating: `A15e` guards the POPULATION FILTER's re-seed, not this change's behaviour**, and its five rows are all Thursday so `filtered.Count` is untouched — only the literal key moves, which D-1 explicitly says changes. ⚠ **Flagging it was right** — "an implementer editing the fixture that guards their own change" is this project's named hazard, and the way through it is exactly what you did: say so, name why, verify it still passes. I re-ran it; it does.
4. **§3.6 queue-row wording — ✅ ACCEPTED**, subject to F3.

### 9.4 What I did NOT verify

- **The `A59c` and `A59b`/`A59d` mutation rows in §4** — I reproduced `A59e`'s and `A59d`'s independently; **`A59c`'s is taken on your report.**
- **`verify-gate.ps1` in any mode** — not run by me. Your `local-fast` result is taken on report.
- **No live or supervised run**, same boundary you named. D-2's observable — the `SKIPPED_SESSION_BOUNDARY` count in `round_history` — remains a post-ship read.
- **The WinForms status line** was not rendered or observed, same as you.

---

## 10. Response to review — F1/F2/F3 fixed, N1 recorded, N2 acknowledged (2026-08-25)

**F1 FIXED.** `ConditionsExtractor.vb`: `idx.Timestamp >= 0` is now resolved **once**, before the round loop (`hasTimestampCol`), and the per-row guard is `If Not hasTimestampCol OrElse idx.Timestamp >= parts.Length Then Continue For` — fails **closed** on both an absent header column and a too-short row, matching `MatchesWeekday`'s own MinValue-first philosophy instead of contradicting it. **Re-verified with the reviewer's own repro**: renamed the `"Timestamp"` header case to `"TimestampZZ"`, rebuilt, re-ran — `A59d` now fails with `RegimeMix=''` (zero rows admitted) rather than the old fail-open `'UP:50|DN:0|RB:50|TR:0'`. Restored, rebuilt clean.

**F2 FIXED.** `AutoTweakerCore.vb`: `weekdayExcludedCount` is now scoped to `(pop Is Nothing OrElse MatchesPopulation(r, pop, settings)) AndAlso Not MatchesWeekday(r)`, not `allRows`, so `filtered.Count + weekdayExcludedCount` = the population-eligible count before the weekday cut — a real decomposition, not two independent denominators that happen to share a numerator. The console line's parenthetical is reworded from *"excluded as weekend/unparsed"* to *"of the otherwise-eligible rows excluded as weekend/unparsed"* so the scoping is stated, not just implied.

**F3 FIXED.** `trader-tick-queue.md`'s "Weekday filters — 3 surfaces" row: the stale *"verified never fired (no tweaker state file, `settings_snapshots/` empty)"* clause is struck in place and replaced with the proposal §1 correction (there IS a state file; it ran, skipped, cursor advanced to 81; LIVE not dormant) — quote-and-label, per this file's own convention for correcting an embedded claim rather than silently deleting it.

**N1 RECORDED** in §6 above, in the reviewer's own words on the mechanism (parse divergence now reaches day, not just hour) and scope (unreachable with the current writer).

**N2 ACKNOWLEDGED, not changed** — same class as N1 (fail-open in the reassuring direction, unreachable while the writer emits offset-free timestamps), named for symmetry with F1 rather than requiring its own fix; left as-is per the reviewer's own framing.

**Re-verified after all three fixes:** solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build **0/0** Release (each run separately); harness **ALL PASS** — same fixture set, no fixture edits were needed (F1/F2 were implementation-robustness fixes the existing fixtures already exercised or were indifferent to, not fixture gaps); `tools/checks/verify-gate.ps1 -Mode local-fast` **GATE PASSED** again (harness ALL PASS, display-parity clean, no engine-path change).

Nothing else in the review is actionable from this seat — §9.1's independent re-verification, §9.3's four confirmations, and §9.4's own unverified list stand as written.
