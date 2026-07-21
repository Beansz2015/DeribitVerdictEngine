# Offline Matrix — Placed-Target Migration · Spec-Back

**Built:** 2026-07-21 · **Spec:** `offline-matrix-placed-target-proposal.md` (APPROVED, M1–M5 ticked 2026-07-18)
**Type:** offline-analysis semantics — zero scoring impact, no ⚠ dataset boundary, no settings keys, no settings-version bump (stays v54).
**State:** local commit; builds 0/0; harness ALL PASS incl. new A32a–d; verify-gate `prepush` GATE PASSED. Trader tests + pushes.

> **⚠ §8 carries five findings the trader has flagged for the ORCHESTRATOR** — tier-ladder ordering unverified
> (F1), success/failure orientation mismatch between strip and report (F2), the strip counting WEAK rows that
> are never traded (F3), empty bar-lists recorded as failures by the live tracker while the offline matrix
> excludes them (F4), and four different names for the same three confidence bands (F12). **None is caused by
> this migration**; putting both surfaces on the same geometry is what made them legible. F4 is a correctness
> bug and should go first — every other measurement inherits it.

---

## 1. What was built

The favourable barrier joined the adverse on placed geometry. D6 moved the *stop* side onto the logged
`PlacedStop{Long,Short}`; this moves the *target* side onto the logged `PlacedTarget{Long,Short}`. The offline
matrix now measures the same thing as the live tracker, the D4 re-walk and the what-if runner — the geometry the
engine actually emitted and the autotrader executes.

The per-tier ATR grid (`StrongAtrThresholds {0.5,0.8}` / `MediumAtrThresholds {0.3,0.5}`) retires, and with it the
threshold axis: **cell space is (tier × window)**, 12 cells at res=1 where there were 24. The **window dimension
survives** — how long to hold is a geometry-independent question.

Pre-v0.8 rows carry neither placed level, keep the legacy formula on **both** sides, and stay in their
`LEGACY_YARDSTICK`-labelled population. The live corpus is all-v0.8, so that split stays empty in practice.

---

## 2. Files touched

| File | Change |
|---|---|
| `analysis/AnalysisConstants.vb` | The two threshold arrays deleted. `HoldWindowsForResolution` / `MinSamplesPerCell` unchanged. `FavBarAbsFloorPct` + `EngineTargetAtrMultiplier` retained with their roles re-scoped to legacy rows and re-commented. |
| `analysis/FailureRateMatrix.vb` | New public `ResolveFavourableBarrier` (mirrors D6's `ResolveAdverseBarrier`) + new public `GateTargetDistance`. `ThresholdsFor` deleted. `counts` collapsed from `(tier)(window)(threshold)` to `(tier)(window)`. Both barriers now resolve **once per row**; the window loop only varies the horizon. `Compute` gained two ByRef counters. `AppendPickedCell` lost its threshold parameter. |
| `analysis/AnalysisReport.vb` | `FailureCellResult.AtrThreshold` deleted. `PopulationReport` gained `PlacedTargetRows` / `LegacyFavourableRows`. |
| `analysis/AnalysisRunner.vb` | Both `Compute` call sites updated; `ComputeContextOutcomes` takes `cfg` and routes its favourable barrier through `ResolveFavourableBarrier` instead of borrowing the recommended cell's threshold (it now borrows only the **window**). |
| `analysis/MarkdownReportWriter.vb` | Threshold column removed from every grid (§2 matrix, before/after, decomposition, recommended, hold-window, pending, summary CSV). Sections renumbered. Dated re-base note added. Barrier-diagnostics table gained the favourable pair. |
| `analysis/FundingMomentumDiagnostic.vb` | M4 rider — canned recommendation text refreshed (see §5). |
| `tools/AutoTweaker/AutoTweakerCore.vb` | Pick space `(window, threshold)` → `(window)`. `BuildPickedCellsJson` dropped `thr`. |
| `tools/AutoTweaker/TweakerState.vb` | `PickedCellEntry.AtrThreshold` → `Double?` + `JsonIgnore(WhenWritingNull)`. |
| `tools/AutoTweaker/PromptBuilder.vb` | Matrix table single-column; picked-cell history renders `—` for post-migration picks. |
| `tools/WhatIfRunner/WhatIfReport.vb` | Compile-forced: the two `Compute` calls, the `k` render column, and `CellKey`. |
| `tools/WhatIfRunner/WhatIfRunner.vbproj` | **Unrelated pre-existing fix** — see §6. |
| `verify/ordercheck/Program.vb` | A27c updated for the renamed section + threshold-free cell; new A32a–d. |

---

## 3. Decisions the spec left to the implementer

Three calls the D-table didn't cover. All three follow from M1 rather than extending it, but they are decisions and
are recorded as such.

**(a) The placed target is used UNFLOORED.** The v35 de-confound floors the favourable barrier at
`MinTradeableMovePct × entry`. Applying that to a placed target would push low-ATR rows back onto a shared floor
price — precisely the column-collapse this migration exists to remove. The live Step 5c gate already evaluated
that exact price before the row was logged as directional, so the floor has already been enforced upstream.
The floor still binds on the **legacy** fallback, where nothing upstream vetted it.

**(b) The min-move EXCLUDE test is now EXACT on v0.8+ rows.** It measures `|PlacedTarget − entry|` against the
floor instead of `engineTargetMult × ATR`. The eval-metric-deconfound spec (§3) states the approximation existed
*because the CSV lacked the adjusted target value* — the CSV now carries it. This is not cosmetic: at ATR≈30 with a
$80 floor, the approximation reads `2.0 × 30 = 60 < 80` and **excludes the row**, while its real placed target
might be $400 away. That would have silently deleted exactly the low-ATR rows the migration makes readable.
Pinned by A32d. On the live all-v0.8 book the exact test should exclude ~0 rows (the live gate already refused
those), so the practical effect is protective rather than visible.

**(c) `FailureCellResult.AtrThreshold` retired rather than left at 0.0.** With one cell per (tier × window) the
field has nothing left to mean, and a vestigial always-zero field is what the v15 cleanup pass existed to remove.
This is what forced the `WhatIfReport` render touchpoint (§6).

---

## 4. Additions beyond the §2 inventory

**Favourable-side routing counters.** `PlacedTargetRows` / `LegacyFavourableRows`, mirroring D6's
`StructuralStopRows` / `AtrFallbackRows`, surfaced as a new column in the §1 barrier-diagnostics table.

Rationale: populations split on `HasPlaced`, which is a **schema-level** flag — it says the four columns are
present in the header, not that this row's side carries a non-zero level. A PLACED population can therefore contain
rows that silently fall back to the legacy favourable formula. D6 made "no silent mixing" the governing invariant
for the adverse side; leaving the favourable side uncounted would have read as an oversight. Cost: two ByRef params
on `Compute` (5 call sites, all updated) and one report column.

---

## 5. M4 riders

**Funding diagnostic text.** The canned recommendations said "defer threshold tuning to WebSocket migration" and
attributed a flat |FundingDelta| distribution to a "genuine REST-cadence ceiling". Both are two eras stale: the WS
cutover shipped at v42 (2026-06-24), and v53 re-cut the momentum window from a 3-sample count to a 5-minute **time**
anchor. That second change matters for how the diagnostic should be read — anchored deltas are cadence-independent
but run *smaller* than the old count-window deltas (the funding premium oscillates at short horizons and partially
cancels), so **a percentile table from this book is not comparable with a pre-v53 one**. All three recommendation
branches now say that, and advise judging a threshold move by the resulting non-FLAT rate and Step 3b engagement
rather than the percentile alone, re-read across a calm stretch.

Deliberately *not* encoded: the roadmap's current "funding bands regime-deferred, re-read across a CALM week"
watch state. That is roadmap state, not report state, and baking it into engine text is how the previous text
went stale.

**Section renumbering.** Render order was `Global Summary / 2 / D6 / 3 / 4a / 4 / 8 / 9 / Global Diagnostics(5,6,7)`.
Now sequential: `1` Global Summary · `2` Failure-Rate Matrix · `3` Placed-Geometry Migration (before/after) ·
`4` Recommended hold window · `5` Barrier-Hit Decomposition · `6` Verdict Context × Outcome · `7` Hold Window
Selection Stats · `8` Pending data · `9` Global Diagnostics (9.1 funding / 9.2 OFI / 9.3 OI×CVD).

The old `## D6.` heading became `## 3. Placed-Geometry Migration` — the section now compares *both* barrier sides,
so "Placed-Stop Migration" had become the wrong name. `BuildD4Section` keeps its method name (harness entry point).

---

## 6. Unrelated pre-existing breakage fixed in passing

`tools/WhatIfRunner/WhatIfRunner.vbproj` was missing `Core/LevelAbsorptionTracker.vb`. When #6 landed
(`ae6678c`, v54), `Indicators_OrderFlow.ClassifyAbsorption` began consuming `AbsorptionSnapshot` /
`AbsorptionRead`, and that project has not compiled since:

```
Core/Indicators_OrderFlow.vb(198,55): error BC30002: Type 'AbsorptionSnapshot' is not defined.
Core/Indicators_OrderFlow.vb(201,73): error BC30002: Type 'AbsorptionRead' is not defined.
```

Confirmed pre-existing by stashing this change and rebuilding — identical errors. Fixed with one `Compile Include`
because §5 acceptance requires a 0/0 build and `WhatIfReport.vb` is a touchpoint of this migration; it could not be
verified otherwise. **Worth a look separately:** the verify gate builds AutoTweaker + OrderCheck + the solution, and
WhatIfRunner is in none of those, which is why a dead project sat unnoticed for four days. Adding it to the gate
would close that hole.

---

## 7. Acceptance (spec §5)

| Requirement | Result |
|---|---|
| Builds 0/0 | Solution (Debug **and** Release), WhatIfRunner, OrderCheck — all **0 errors / 0 warnings**, built in place. |
| Harness unregressed | **162 checks, ALL PASS**, 0 failures. One assertion updated: **A27c** pinned the literal `"D6. Placed-Stop Migration"` heading and constructed cells with `.AtrThreshold` — both retired by this spec. |
| verify-gate | `prepush` mode (the strict one: full solution Release build + parity fail-without-token) — **GATE PASSED**, exit 0, including `version-bump: OK — engine path changed but [no-engine-change] token present`. |
| Placed-favourable routing (v0.8 vs legacy row) | **A32a** — placed target returned verbatim *and unfloored* (target inside the floor distance), short column read for shorts, legacy fallback at `engineTargetMult × ATR`, legacy floored at low ATR, `AdverseBarrierMode.Legacy` forcing the legacy formula on a placed row, routing counters, and exact-vs-approximate gate distance. |
| Tweaker window-only pick + old-history parse | **A32b** — a pre-migration `state.json` round-trips with its `atr_threshold` intact while a new pick omits the key entirely (one occurrence in the file, not two); and every cell `Compute` emits is a distinct `(tier × window)` key, 12 total, no duplicates. |
| D4 single-column render | **A32c** — header is `Window` + one data column with no ATR caption; the data row has 3 pipes (the retired 2-threshold grid rendered 4). |
| Floored-grid impossibility | **A32d** — two rows at ATR=30 (floor distance $80, so the whole retired grid would have been sub-floor) with placed targets 120 and 400 away produce **distinct outcomes** in the same cell: n=2, 1 success, 1 window-expiry. Under the old grid both barriers floored to the same price and both rows read identically. Second check: `belowMinMoveExcluded = 0` — the retired approximation (`2.0 × 30 = 60 < 80`) would have dropped **both**. |
| Report regeneration sanity-checked vs the perf strip's `[B]` rates | **DONE 2026-07-21 — PASS on geometry, with the spec's framing corrected. See §7a.** |

### 7a. The `[B]`-rate cross-check (2026-07-21)

Run against the 20260720_180842 report + the strip reading `[B] Cur.Wk 23% · 3d 23% · Cur.Day 15% · Asia --% · London 0% · NY 30%`.

**Strip reproduced exactly.** Replicating `ComputeWindows` / `ComputeSessionWindow` boundaries against
`analysis_eval_cache.csv` yields **23 / 23 / 15 / --  / 0 / 30** — all six cells, so the comparison below rests on
verified mechanics.

**Geometry parity is EXACT — this is the acceptance result that matters.** Joining the eval cache to
`analysis_log.csv` on timestamp and comparing the tracker's `FavBar`/`AdvBar` against the logged
`PlacedTarget*`/`PlacedStop*` for the row's side: **1335 / 1335 rows identical to <$0.005, zero mismatches.**
Both surfaces now walk the same barriers on the same rows.

**The spec's "should agree within window/precision differences" was WRONG and is withdrawn.** The two rates cannot
agree numerically, for four structural reasons — none of them a migration defect:

1. **Inverted orientation.** The strip is a SUCCESS rate (`fgColor = If(rate > 50, ACC_STRONG_LONG, ACC_SHORT)` —
   green above 50); the report is a FAILURE rate.
2. **Different populations.** `LivePerformanceTracker.IsEligibleVerdict` admits **WEAK LONG / WEAK SHORT**; the
   matrix excludes them from the denominator. In the sampled NY block WEAK is **62.5% of the strip's denominator**
   (50 of 80). Directional-only = 33.3%, WEAK = 28.0%, blended (displayed) = 30.0%.
3. **Different spans.** Session cells are most-recent-**block** only, not the book. `London: 0%` is a genuine
   0-for-26 in the 07-20 block (`min_sample_for_render` = 4), not a null and not a contradiction of the book-wide
   London rates.
4. **Different OHLC provenance.** The tracker freezes an outcome against cached candles at evaluation time; the
   report re-fetches fresh Deribit OHLC on every regeneration.

Same-population comparison (NY hours, res=1, directional, whole book): eval cache **33.6%** success vs report
**38.5%**.

**Finding — one bad eval-cache slice.** Expiry rate by day: **2026-07-03 is 22/22 = 100% `WINDOW_EXPIRED`** for NY
directional rows (other days 5–19%). Implausible as market behaviour; those rows carry valid barriers and
`.0000000Z` whole-second timestamps (the backfill signature), and 07-03 is the #5 build + CSV-rotation day — so
that slice was backfilled without forward-bar coverage. Backfilled rows in aggregate are healthy (37.5% success /
14.9% expiry vs live 36.2% / 11.5%), so this is one slice, not a systemic backfill fault. Excluding 07-03 the
same-population gap narrows to **35.8% vs 38.5%** (2.7pp) — consistent with the provenance difference. **The
offline report is the more trustworthy surface on historical rows**, since it re-walks fresh data rather than
carrying a frozen verdict. The currently-displayed strip cells span 07-18 onward and are unaffected.

**Interpretive trap worth recording:** the strip's headline is a WEAK-inclusive blend. Read as "how good are my
tradeable signals" it understates the directional band (33.3% vs the displayed 30%). Not a bug — the tracker was
specified WEAK-inclusive — but the two surfaces answer different questions and a reader moving between them
should know which.

---

## 8. Findings for the orchestrator

Everything the migration + cross-check surfaced. **F1–F4 and F12 are the ones the trader flagged for
escalation** (F12 raised separately, after the first four).
None of them is caused by this migration — the migration made them *visible* by putting both surfaces on the
same geometry, which is what removing a confound is supposed to do.

---

### ⭐ F1 — The tier ladder carries no measurable ordering (STRONG ≯ MEDIUM ≯ WEAK)

Two independent observations converged here: the trader noticed **STRONG reading worse than MEDIUM**, and the
cross-check turned up **MEDIUM reading worse than WEAK**. Both are real in the cells. Neither is statistically
established — and neither is the *correct* ordering.

**Per-cell, at each population's tracker horizon** (res-1 → 15m, res-3 → 45m), success rates from report §5:

| session | side | STRONG | MEDIUM | delta | |
|---|---|---|---|---|---|
| NY | LONG | 52.2% (n=46) | 38.4% (n=172) | +13.8pp | ok |
| NY | SHORT | 30.0% (n=20) | 34.5% (n=113) | −4.5pp | **INVERTED** |
| LONDON | LONG | 16.7% (n=6) | 50.0% (n=28) | −33.3pp | **INVERTED** |
| LONDON | SHORT | 52.6% (n=19) | 47.6% (n=21) | +5.0pp | ok |
| ASIA | LONG | n=0 | 50.0% (n=6) | — | no data |
| ASIA | SHORT | 100% (n=1) | 45.5% (n=22) | +54.5pp | ok (n=1) |

**Full ladder including WEAK** (eval cache — the report excludes WEAK; 07-03 slice removed per F4):

| band | succ | n | success% | 95% Wilson CI |
|---|---|---|---|---|
| STRONG | 38 | 89 | 42.7% | [32.9% – 53.1%] |
| MEDIUM | 131 | 361 | 36.3% | [31.5% – 41.4%] |
| WEAK | 331 | 844 | 39.2% | [36.0% – 42.6%] |

**Observed order is STRONG > WEAK > MEDIUM** — MEDIUM is the worst band. But every pairwise test fails:

| comparison | delta | z | |
|---|---|---|---|
| STRONG vs MEDIUM | +6.4pp | +1.12 | not significant |
| WEAK vs MEDIUM | +2.9pp | +0.96 | not significant |
| STRONG vs WEAK | +3.5pp | +0.64 | not significant |

All three CIs overlap heavily. **The honest reading: nothing about tier ordering is established in either
direction on this book.** The inversions the trader spotted are small-n noise — and so is the apparent pooled
correctness. The binding constraint is sample: **STRONG has only ~90 evaluable rows book-wide**, and the two
inverted cells sit at n=20 and n=6.

**Recommendation:** do not act on the inversions, and do not assume the ladder works either. This is a
data-sufficiency problem before it is a model problem. Concretely — (a) treat "STRONG is better than MEDIUM" as
*unverified*, not as a design invariant, in anything downstream (notably W6-3 Kelly CAL, which wants per-tier
empirical win rates and would currently be fitting noise); (b) set a sample gate before re-reading, ~n≥150 at
STRONG; (c) when re-read, use the offline report rather than the eval cache (F4 explains why); (d) if the
ordering still fails to appear at n≥150, that is a genuine scoring-quality finding and belongs in its own spec.

---

### ⭐ F2 — Orientation mismatch: strip shows success, report shows failure

Confirmed in code. The strip renders a **success** rate — `MainForm_Layout.vb:1384`,
`fgColor = If(rate > 50, ACC_STRONG_LONG, ACC_SHORT)` (green above 50), fed by
`WindowAggregate.BarrierRatePct = SuccessCount / n`. The offline report renders a **failure** rate throughout.

**Trader's call: unify on SUCCESS.** Agreed — the mental inversion buys nothing, and it is exactly the kind of
friction that produces a misread under time pressure. Success is also the better default: it is the direction a
reader intuitively wants ("how often did this work"), and green-is-good needs no explanation.

**One trap that must not be missed if this is implemented.** The auto-tweaker's trigger is
*failure*-oriented: `AutoTweakerCore` compares `aggregateRatePct < config.FailureRateThresholdPct` to decide
BELOW_THRESHOLD, where `aggregateRatePct` is built from `cell.Failures / cell.SampleSize`. Flipping the
*display* must not flip that comparison, or the tweaker silently inverts — it would start firing when things are
going *well*. Safest shape: keep `FailureCellResult.Failures`/`FailureRate` as the internal truth (the tweaker,
the `IsMostProfitable` pick, and the CI maths all read it), and convert to success **only at the render
boundary**. Surfaces to change together: report §2/§3/§4/§5/§6/§7/§8 render text, the summary CSV column name,
the `MarkdownReportWriter` interpretation blurb, and `PromptBuilder`'s matrix table (the LLM prompt — flipping
this one without updating the surrounding prose would actively mislead the tweaker's reasoning).

Not implemented here: it is a cross-surface display-semantics change with a live scoring-adjacent dependency, so
it wants its own small spec per the spec-first rule. Mechanically it is perhaps an hour.

---

### ⭐ F3 — The strip should exclude WEAK (show only what will actually be traded)

Currently `LivePerformanceTracker.IsEligibleVerdict` (`:1042`) admits `WEAK LONG` / `WEAK SHORT` alongside
STRONG/MEDIUM. In the sampled NY block **WEAK was 62.5% of the strip's denominator** (50 of 80 rows), so the
displayed `NY: 30%` is mostly measuring a band that is never traded. Directional-only was 33.3%.

**Trader's call: exclude WEAK.** Agreed, and the frozen bridge contract already backs it — the consumer's
default tier gate refuses WEAK (`refused: policy`), so WEAK signals are by construction *not trades*. A strip
that answers "how are my signals doing" should answer it about the signals that become orders.

**Bonus: it makes the two surfaces directly comparable.** The matrix population is exactly STRONG+MEDIUM. Align
the strip and the §5 cross-check becomes a like-for-like number instead of the four-way caveat in §7a.

**Implementation notes.** This is a *display-time filter*, not a data change — the eval cache can keep storing
WEAK rows (they cost nothing and preserve the option to show a WEAK band separately). So it is reversible with
no cache rotation. Two consequences to decide on: **(a)** the denominator drops ~2.6× (NY block 80 → 30), so
session cells will show `--%` more often — `min_sample_for_render` is currently **4**, which is very low for a
rate that now moves in ~3pp steps; worth raising at the same time. **(b)** If WEAK is dropped from the headline,
consider whether it is worth a separate dimmed cell rather than discarded — F1 shows WEAK is currently
*out-performing* MEDIUM, which is information, even if it is not yet significant.

---

### ⭐ F4 — The 2026-07-03 expiry anomaly, explained

**What was seen.** Expiry rate by day for NY directional rows in the eval cache:

```
2026-07-03    22 / 22  = 100%     <-- every single row
2026-07-07    10 / 53  =  19%     2026-07-13     2 / 38 =  5%
2026-07-08     4 / 43  =   9%     2026-07-17     7 / 79 =  9%
2026-07-09     1 / 11  =   9%     2026-07-20     2 / 15 = 13%
```

**Why 100% is impossible as market behaviour.** `WINDOW_EXPIRED` means price touched *neither* the target *nor*
the stop for the entire window. With placed geometry the stop is ~1.6×ATR and the target ~1.75×ATR or a
structural level; at ATR≈45 on BTC over 15 one-minute bars, one barrier is normally reached. For 22 consecutive
signals across a whole NY session to touch neither, the market would have to have been effectively frozen for
hours. It was not.

**The mechanism.** `WalkBars` iterates the bars it is *given*. `LivePerformanceTracker.EvaluateEntry:689` reads:

```vb
Dim bars = GetEligibleBars(ts, nowUtc, e.ExecResolution)
If bars.Count = 0 Then Return ("WINDOW_EXPIRED", Nothing)
```

So **an empty bar list is recorded as a failure.** "No data" and "no movement" produce the same stored outcome
and are afterwards indistinguishable.

**This is the sharp part — the offline matrix already handles it correctly.** `FailureRateMatrix.Compute`, same
condition:

```vb
If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
    Continue For   ' no data for this window — exclude from denominator
End If
```

**Same condition, opposite handling: offline excludes from the denominator, live counts it as a failure.** That
asymmetry is a bug class, not a rounding difference — it biases every live rate *downward* by however many rows
lacked coverage, and it does so invisibly.

**Why 07-03 specifically.** Those rows carry valid barriers (they matched the CSV 100% in the §7a join), and
their timestamps end `.0000000Z` — whole seconds. Live entries carry genuine sub-second fractions
(`...T18:08:08.2802248Z`) because they are stamped from `DateTime.UtcNow` at run time; whole-second stamps are
reconstructed from the CSV's second-resolution `Timestamp` column, i.e. **backfilled**. The D6 eval-cache v4→v5
rotation (2026-07-14) forced a full cold-start rebuild, so everything before that date in the current cache is
backfill, sourced from `OhlcCache` — which evidently had no 07-03 coverage. 07-03 is also the #5 build + CSV
v0.7→v0.8 rotation day, i.e. a restart boundary, which is a plausible reason the cache has a hole there.

**Scope — deliberately not overstated.** Backfilled rows *in aggregate* are healthy (37.5% success / 14.9%
expiry, vs live 36.2% / 11.5%), so this is **one bad slice, not a systemic backfill fault**. The currently
displayed strip windows span 07-18 onward and are **unaffected**. Excluding 07-03 narrows the same-population
gap from 4.9pp to 2.7pp.

**Why it still matters.** It is latent and silent. Any future whole-cache read — a signal-health audit, the F1
tier-ladder study, W6 calibration — inherits 22 fabricated failures with nothing marking them as suspect.

**Recommended fix:** give "could not evaluate" its own outcome (e.g. `NO_DATA` / `UNEVALUABLE`) and exclude it
from the denominator, mirroring what the offline side already does. That fixes 07-03 retroactively on the next
rebuild *and* closes the asymmetry permanently. A targeted re-backfill of the slice is the tactical alternative
but leaves the bug in place.

---

### ⭐ F12 — The three confidence bands have four different names (and `LONG` means two things)

Trader-raised. Confirmed: the same three-rung ladder is spelled differently on every surface, and the engine
assigns two of the spellings **on the same line** (`ScoringEngine_Calculate_Verdict.vb:153–168`).

| band | verdict string (UI / snapshot / CSV / card) | `confidence` (payload — the R1 action key) | matrix tier (offline report) | settings key |
|---|---|---|---|---|
| top | `STRONG LONG` / `STRONG SHORT` | `HIGH` | `STRONG_LONG` / `STRONG_SHORT` | `verdict_strong_pct` |
| middle | **`LONG` / `SHORT`** — no qualifier | `MEDIUM` | `MEDIUM_LONG` / `MEDIUM_SHORT` | `verdict_med_pct` |
| bottom | `WEAK LONG` / `WEAK SHORT` | `LOW` | *(excluded from the matrix)* | `verdict_weak_pct` |

**Two concrete hazards, not just inconsistency:**

**(a) The middle band is never called "MEDIUM" on any surface the trader reads.** The screen says bare `LONG`.
"MEDIUM" exists only in the bridge payload, the offline report, and a settings key. This already cost us a
round-trip in this very session: the trader reported *"STRONG LONG reads worse than LONG"* while F1 reports
*"MEDIUM is the worst band"* — **the same finding, in two vocabularies**, and it took a table to see that.

**(b) `LONG` is overloaded across fields in the frozen contract.** In `verdict` it is a tier+side compound
(middle band, long side); in `direction` it is side only (`"LONG" | "SHORT" | "NONE"`). A consumer testing
`verdict == "LONG"` to mean "this is a long" silently drops `STRONG LONG` and `WEAK LONG` — the top and bottom
bands. The contract already routes actionability through `direction` + `confidence` (§4 R1), so the safe path is
specified; the trap is for anyone who parses `verdict` because it looks self-explanatory.

**One near-miss worth stating so it is not "fixed" by mistake:** `scoring.tier_floor` uses
`high_/med_/low_threshold` + `_floor`, which *looks* like a fifth spelling of the same ladder. **It is not.** It
is a separate three-band split of the **raw score** (12/9/6) used to floor the effective score after the
TRANSITIONAL ADX penalty (`_Verdict.vb:76–77`), unrelated to the verdict ladder's percentage-of-regimeMax
thresholds. Renaming it to match would create a false equivalence. If anything it should be renamed *away* —
e.g. `penalty_floor.*` — to stop it reading as a tier vocabulary at all.

**Recommendation.** Do not re-spell the wire format: `confidence` is `HIGH|MEDIUM|LOW`, verified verbatim
against 8,025 live rows and **frozen** in the schema v1 contract — changing it breaks the consumer and costs a
`schema_version` bump for cosmetics. Instead, pick **one** vocabulary for human-facing text and docs and make
the rest explicitly derived:

1. **Adopt HIGH / MEDIUM / LOW as the canonical band names** (it is the frozen one, and the only one that names
   the middle band at all).
2. **Surface the band next to the verdict** where a human reads it — the middle verdict rendering as bare
   `LONG` is the root of (a). `LONG [MEDIUM]` or similar costs one label and kills the ambiguity.
3. **Keep the matrix tier identifiers as-is** (`MEDIUM_LONG` is already correct under HIGH/MEDIUM/LOW) and add a
   one-line legend to the report mapping tier ↔ verdict string, since the report is read alongside the app.
4. **Rename `scoring.tier_floor`** to something that does not read as tier vocabulary — a settings-key rename
   with a POCO/tweaker-fence follow-through, so it wants its own small spec rather than a drive-by.

Cost is documentation + one UI label + one settings rename. No wire change, no scoring change.

---

### Other findings (not escalated, recorded for completeness)

**F5 — Geometry parity is exact.** The migration's core claim, verified: tracker `FavBar`/`AdvBar` equal the
logged `PlacedTarget*`/`PlacedStop*` on **1335/1335** joined rows, <$0.005, zero mismatches. Both surfaces walk
the same barriers on the same rows.

**F6 — The strip was reproduced exactly.** All six cells (23/23/15/--/0/30) replayed from
`analysis_eval_cache.csv` using the tracker's own `ComputeWindows` / `ComputeSessionWindow` boundary logic
before any comparison was drawn, so §7a rests on verified mechanics rather than inference.

**F7 — Session cells are most-recent-*block*, not book-wide.** `London: 0%` is a genuine 0-for-26 in the 07-20
London block, not a null and not a contradiction of the book-wide London rates. Easy to misread as "London is
broken"; worth a tooltip.

**F8 — The `.0000000Z` backfill signature is a reusable diagnostic.** Whole-second eval-cache timestamps ⇒
backfilled; sub-second ⇒ live. Combined with the eval-cache ↔ CSV timestamp join (replace `T`, truncate to 19
chars) this is a ready-made parity/provenance instrument for any future audit.

**F9 — `RoundStatsBuilder` is now the last synthetic-target measurement in the repo.** Private
`FavAtrThreshold = 0.5`, calls `WalkBars` directly, not a `Compute` consumer, deliberately out of this spec's
inventory. Needs its own decision: migrate for consistency, or document why the round-stats display wants a
fixed yardstick.

**F10 — `WhatIfRunner` is not in the verify gate.** The gate builds the solution + AutoTweaker + OrderCheck.
That is why a project broken by #6 sat unnoticed for four days (§6). One line to add; would have caught it.

**F11 — `UserManual.md` §18 still quotes the retired threshold constants** (~line 1861). Left to the active
manual fold-in lane per the implementer orders.

---

## 9. Open / follow-ups

1. ~~Live report regeneration + `[B]`-rate cross-check~~ — **DONE 2026-07-21, §7a.** Two follow-ups fell out of it:
   it produced §7a and the §8 findings. **Everything actionable that fell out of it is in §8** — F1–F4 are
   flagged by the trader for the orchestrator; F5–F11 are recorded.
2. **Failure rates are not comparable across the re-base.** The header note says so, but any watch reading rates
   against a pre-2026-07-21 report needs re-basing. Expect movement in both directions: the stop side already
   re-based at D6, and now targets move from a fixed ATR fraction to real structural placements — closer targets
   raise success, farther ones lower it, per row.
3. **Auto-tweaker first-fire readings re-base again.** The >40% trigger semantics are unchanged, but the aggregate
   rate it reads is now computed on placed-vs-placed. Same caution D6 carried. See also **F2** — if the
   success/failure orientation is ever unified, the tweaker's comparison must NOT flip with the display.
4. **Nothing in §8 is caused by this migration.** Putting both surfaces on the same geometry is what made the
   tier-ladder question (F1), the orientation mismatch (F2), the WEAK-blend (F3) and the empty-bars asymmetry
   (F4) legible. They were all present before; the confound was hiding them.

**Orchestrator hand-off, in dependency order.** F4 first — it is a correctness bug (empty bars recorded as
failures) and every other measurement inherits it, including F1's re-read. Then F3 (population) and F2
(orientation), which are small and make the two surfaces directly comparable. **F12 (vocabulary) pairs naturally
with F2** — both are display-semantics passes over the same two surfaces, and doing them together avoids
touching the report's render text twice. F1 last, because it needs the others fixed *and* more STRONG rows
before it can be answered at all.
