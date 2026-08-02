# Weekday scope — RULING, 2026-08-03

**From:** the Opus orchestrator/ruling seat, on the trader's direction (2026-08-03): *"I do not trade weekends at all — would prefer if weekend data is not taken into account… This ruling should encompass anything that touches data collection, scoring, etc."*
**Corrects:** **J-C** as ratified in [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) §2, which required the natural-silence baseline be *"extended to include a weekend."* **That requirement is retired** — see §3. Same class of correction as [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md).
**Folds into:** **C1 / D4** ([`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) §9).

---

## 1. The principle, and the distinction that carries it

> **The trader does not trade weekends. Therefore weekend rows are out of scope for anything that JUDGES, TUNES, or REPORTS ON engine performance.**
>
> **CAPTURE IS NOT EVALUATION, and capture does not change.** The collector runs 24/7 and the store keeps filling on Saturday and Sunday exactly as before. Nothing stops recording. What changes is what gets *scored, tuned on, aggregated, or flagged as a defect.*

That distinction is the whole ruling. Weekend tape costs ~2.4 MB/day and is unrecoverable if not captured; weekend *evaluation* costs false signal in every instrument the trader reads. Keep the cheap irreversible thing, drop the expensive misleading one. **If weekend data is ever wanted for a study, it is on disk — it simply was not guarded, tuned on, or counted.**

---

## 2. Surface inventory — VERIFIED IN THE TREE 2026-08-03, not assumed

A repo-wide search for `DayOfWeek|Saturday|Sunday|weekday|IsWeekend` across all `.vb` returns **five hits in two files.** The headline:

> **Weekday-only is enforced in CODE in exactly ONE place. Everywhere else it is an analyst convention applied by hand when freezing a pooled book — and conventions do not travel to tools that run unattended.**

| Surface | Filters weekends today? | Consequence | Priority |
|---|---|---|---|
| **`tools/AutoTweaker`** | ❌ **No** | **It is the only surface that WRITES `settings.json`.** Unfiltered, it would tune the engine on sessions the trader never trades. It has **never fired live** (data-gated on a >40 %-failure NY×1 window), so this is fixable *before* it can ever matter — but if that window arrives first, it tunes on contaminated data and the result is a settings write | **HIGHEST** |
| **`LivePerformanceTracker`** | ❌ **No** — `:560` computes a Monday **week-start anchor**, which is not a filter | The 3-day and week windows on the perf strip **include Saturday and Sunday**. The success rate the trader reads daily is mixed with sessions never traded | **HIGH** (daily visibility) |
| **`analysis/AnalysisRunner`** + `FailureRateMatrix` | ❌ No code filter | Correct only because every published derivation pre-filtered by hand. Nothing enforces it; a `report --csv` on an unfiltered book silently includes weekends | MEDIUM |
| **`tools/WhatIfRunner`** | ❌ No code filter | Same shape — overlay sweeps would score weekend rows | MEDIUM |
| **`tools/CeilingAudit`** | ✅ **Yes** — `CsvFeatureBuilder.vb:199-200` | Already correct. **The existing precedent to copy** | — |
| **Coverage report (C1)** | n/a — unbuilt | Gets it by construction, per §3 | — |
| **Capture** (`DeribitWsFeed`, `TradeStoreGapRepair`) | n/a | **Unchanged — 24/7, deliberately** | — |

**The auto-tweaker is the sharp one and it is worth stating plainly:** every other surface produces a *number a human reads and can discount*. The tweaker produces a *settings diff*. It is the only path by which weekend data could silently change what the engine does on a Tuesday.

---

## 3. Fold-in to C1 / D4

**D4's re-anchor requirement is DISCHARGED BY SCOPE rather than by data.**

- **The coverage report evaluates weekday hours only.** Weekend hours classify `out-of-scope-weekend` — a sixth class alongside the five in the J-B clause, and like those it is *reported*, never silently folded into another.
- **J-C's "extended to include a weekend" is retired.** Its entire purpose was to stop the report firing false defects across quiet weekend hours. If weekend hours are not evaluated, the requirement has no work left to do.
- **The 300,000 ms default is therefore already correctly anchored, and stops being provisional.** It was derived at 1.85× the observed 2m42s maximum on a **Wednesday→Thursday** window — i.e. a weekday sample, which is exactly the basis a weekday-scoped report wants. It moves from *"provisional pending a weekend"* to **confirmed on a weekday basis**, subject to routine re-check as weekday tape accumulates.
- **Sensitivity improves.** Including weekends would have *raised* the threshold to tolerate quieter periods, blunting weekday detection. Weekday-only keeps it tight where the data is used.
- **The time-critical REST fetch is cancelled.** It existed only to satisfy J-C's weekend clause. Nothing expires.

**Part B (the live `TAPE STORE` element) stays UNCONDITIONAL.** An app that dies on Friday night is still dead on Monday morning. Liveness detection is not performance evaluation and does not take weekends off. **This is the one place the ruling deliberately does not apply.**

---

## 4. What changes, and what is merely recorded

**Ruled now, no build:** C1 is weekday-scoped (§3). D4 confirmed. J-C's weekend clause retired.

**Ruled in principle, build NOT authorised here** — each needs its own slot, and none is a ⚠ dataset boundary because none changes scoring:

1. **Auto-tweaker weekday filter** — the one that must not be left. It is display-neutral, settings-neutral, and cheap; it belongs with the existing Phase-2a population filter, alongside the session × resolution partition already there.
2. **`LivePerformanceTracker` weekday filter** — note the real cost: excluding weekends **shrinks n**, and the strip already gates on `performance_display.min_sample_for_render`. Expect more `NO_DATA`/suppressed cells, not fewer. That is honest rather than regrettable, but it should be expected rather than discovered.
3. **`AnalysisRunner` / `WhatIfRunner`** — make the convention structural instead of manual.

**Explicitly NOT changed:** capture cadence, the WS feed, gap repair, the store, `analysis_log.csv` row writing. **The engine keeps running and logging on weekends.** Weekend rows remain in the book, correctly, and are excluded at *read* time.

---

## 5. What I did not rule

- **Market-state instruments may warrant a carve-out.** The **funding calm-week re-read** is about market conditions, not trade performance, and funding accrues over weekends. I have not ruled whether it is weekday-only; whoever runs it should decide explicitly rather than inherit this ruling by default.
- **No historical re-statement.** Published figures were already weekday-only by convention (F1, W6-1, W6-4, the ASIA burst derivation all state it), so nothing needs recomputing. **The perf strip's historical numbers were not** — but they are a live readout, not a published result.
- **I did not measure the size of the contamination.** How much the perf strip's rate moves when weekends are excluded is unmeasured. It is bounded by the weekend share of rows (~2/7 of calendar time, less in practice since weekends are quieter), but the direction is unknown.
- **The auto-tweaker's live-fire gate is unchanged** — still data-gated. This ruling does not accelerate or delay it; it only says the filter must exist before it fires.
