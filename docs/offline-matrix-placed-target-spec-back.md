# Offline Matrix — Placed-Target Migration · Spec-Back

**Built:** 2026-07-21 · **Spec:** `offline-matrix-placed-target-proposal.md` (APPROVED, M1–M5 ticked 2026-07-18)
**Type:** offline-analysis semantics — zero scoring impact, no ⚠ dataset boundary, no settings keys, no settings-version bump (stays v54).
**State:** local commit; builds 0/0; harness ALL PASS incl. new A32a–d; verify-gate `local-fast` GATE PASSED. Trader tests + pushes.

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

## 8. Open / follow-ups

1. ~~Live report regeneration + `[B]`-rate cross-check~~ — **DONE 2026-07-21, §7a.** Two follow-ups fell out of it:
   **(a)** the 2026-07-03 eval-cache slice is 100% `WINDOW_EXPIRED` (22 NY directional rows) from a backfill without
   forward-bar coverage — a targeted re-backfill or an explicit exclusion would stop it poisoning any future
   whole-cache read; the displayed strip windows are unaffected. **(b)** the strip's headline is a WEAK-inclusive
   blend while the matrix excludes WEAK — worth a tooltip or a manual line so the two aren't read as one number.
2. **Failure rates are not comparable across the re-base.** The header note says so, but any watch reading rates
   against a pre-2026-07-21 report needs re-basing. Expect movement in both directions: the stop side already
   re-based at D6, and now targets move from a fixed ATR fraction to real structural placements — closer targets
   raise success, farther ones lower it, per row.
3. **`docs/UserManual.md` §18 still quotes the retired constants** (~line 1861). Left untouched: the manual
   fold-in lane was actively editing the manuals and PDFs, and the implementer orders say to serialize with it.
   Needs a pass when that lane is clear.
4. **Auto-tweaker first-fire readings re-base again.** The >40% trigger semantics are unchanged, but the aggregate
   rate it reads is now computed on placed-vs-placed. Same caution D6 carried.
5. **`RoundStatsBuilder` was left alone** — deliberately. It computes its own display-only tier accuracy with a
   private `FavAtrThreshold = 0.5` const and calls `WalkBars` directly; it is not a `FailureRateMatrix.Compute`
   consumer and is not in the §2 inventory. It is now the last surviving synthetic-target measurement in the repo,
   so it is worth a decision — migrate it for consistency, or document why the round-stats display wants a fixed
   yardstick. Not touched here because that call belongs in a spec, not in an implementer's discretion.
