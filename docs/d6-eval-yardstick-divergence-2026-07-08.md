# D6 — Eval Yardstick vs Placed Geometry: Divergence Analysis

**Date:** 2026-07-08 · **Author:** Opus seat · **Type:** analysis pass (report only — no code changed)
**Roadmap:** Q8 D6 (B4b follow-up) — *"analysis pass first, code second; do it before trusting any post-v51 failure-rate reading too precisely."*
**Question:** now that v51 places structural-first levels and the bridge/autotrader executes them, does the eval stack (live perf-strip + offline failure matrix) still measure outcomes against the same geometry the engine places? If not, by how much, and what does it mean for post-v51 failure rates?

**Headline:** No — and the gap is large, entirely on the **stop side**. Both eval surfaces score the adverse barrier against the **raw 5m swing stop (median ~9×ATR away)**, while the engine places and the autotrader executes a stop **clamped to 1.6×ATR**. The eval's adverse barrier is ~5.5× wider than the executed stop and is unreachable intrabar in 93% of rows, so it essentially never records a stop-out. Post-v51 failure rates therefore understate the stop-out risk of the executed geometry; "failure" collapses to window-expiry. The **target side is aligned** (the eval uses the placed target), so this is a stop-side-only migration.

---

## 1. Data corpus (rotation / run-location audit)

Past analyses have undercounted rows because the CSV rotates and the exe has been run from more than one location. Full on-disk inventory (2026-07-08):

| File | Schema | Has `Placed*`? | Role |
|---|---|---|---|
| `bin/Debug/net8.0-windows/analysis_log.csv` — **925 rows, 07-03 15:57 → 07-08 13:47 UTC** | **v0.8** | **yes** | The D6 corpus |
| `…/analysis_log.csv.v0.7.bak` (6.3 MB) | v0.7 | no | Pre-07-03 book; unusable for placed-vs-eval geometry |
| `…/.v0.6 / .v0.5 / .v0.4 / .v0.3.bak` | older | no | Prior schemas |
| `data-archive/pre-orderfix-20260611/**` | v0.3-era | no | Archived June book + calibration summaries |

**Confirmed:** there is **no `bin/Release` output dir and no project-root `analysis_log.csv`**, so there is no VS-launch-vs-exe split of the live book — the single Debug file is the *complete* v0.8 corpus. The `Placed*` columns exist only in v0.8, so the geometry comparison is inherently confined to this one file, but it is not a partial slice.

**Eligible rows:** 267 directional (STRONG/standard/WEAK LONG/SHORT); 658 non-directional (NO TRADE / NO TRADE [WEAK]) are eval-excluded by design. 266 directional rows carry a valid ATR and enter the stats below.

**v50 → v51 boundary is clean in the data** (mean placed-stop distance in ATR, by UTC day):

| Day | Directional n | mean placed-stop (×ATR) | Era |
|---|---|---|---|
| 07-03 | 49 | 1.20 | v50 (legacy 1.2× stop) |
| 07-06 | 10 | 1.20 | v50 |
| 07-07 | 156 | 1.60 | **v51** (structural clamp 1.6×) |
| 07-08 | 51 | 1.60 (p50) | v51 |

---

## 2. Method

For each directional row, using the dominant side's columns: entry = `Price`(2), `ATR`(65), raw swing stop = `SwingStop{Long,Short}`(83/84), placed stop = `PlacedStop{Long,Short}`(107/109), placed target = `PlacedTarget{Long,Short}`(106/108). Distances expressed in ATR units. No OHLC re-walk (that is the code phase); this pass is pure barrier geometry.

The eval barriers are read from `LivePerformanceTracker.vb:929-935`:
- `FavBar = If(adjTarget > 0, adjTarget, entry ± 2.0×ATR)` — favourable = the **placed/adjusted target** when present (it always is here).
- `AdvBar = If(swingStop > 0, swingStop, entry ∓ 1.2×ATR)` — adverse = the **raw swing stop** when present (100% of rows).

Per UserManual §18, the offline `FailureRateMatrix` shares the same swing-else-`1.2×ATR` adverse convention (its favourable side is a per-tier ATR threshold, a separate matter — see §5).

---

## 3. Findings

**Stop side (the divergence):**

| Metric (266 directional rows) | p10 | p50 | p90 | max |
|---|---|---|---|---|
| **Placed stop** distance (×ATR) — executed | 1.20 | **1.60** | 1.60 | 1.60 |
| **Raw swing stop** distance (×ATR) — eval adverse | 3.9 | **9.3** | 18.3 | 27.3 |

- **100%** of directional rows have a swing stop → the eval **always** uses the raw swing as its adverse barrier (the `1.2×ATR` fallback never fires).
- **~100%** of rows: the eval adverse barrier is **wider** than the executed placed stop (evalWider 100% on 07-03/06/07, 78% on 07-08).
- **93%** of rows: raw swing stop is **>3×ATR** from entry — unreachable within a T+3..T+15 (or T+45 on 3-min) window in normal flow, so the adverse barrier essentially never binds.
- **v51-only (207 rows):** raw swing p50 **8.9×ATR** vs placed stop p50 **1.60×ATR** → the eval measures risk against a barrier **5.5× wider** than the one executed.

**Target side (aligned — no action):**

| Metric | p10 | p50 | p90 |
|---|---|---|---|
| Placed target distance (×ATR) | 1.30 | **1.75** | 2.42 |
| Executed R:R (placed target / placed stop) | 0.86 | **1.09** | 1.67 |

The eval favourable barrier = the placed target (median 1.75×ATR, the v51 fallback-target multiplier; session variants + structural placements spread p10-p90). The 2.0×ATR fallback constant never triggers because a placed target is always present. Executed R:R p50 = 1.09 matches the documented ~1:1.1 fallback R:R — a cross-check that the column mapping is correct.

---

## 4. Interpretation

The eval's adverse barrier is the **discretionary trader's** structural swing stop (median ~9×ATR away), which was the correct choice when execution was discretionary and stops were placed at swings. Because that barrier is 3–27×ATR from entry, it is almost never touched inside the eval window, so:

1. **"Failure" ≈ window-expiry, not stop-out.** This directly explains the roadmap W6 header's *"adverse-stop failures at 0.3%"* — the adverse barrier physically can't bind. The failure-rate matrix is really a *"did price reach target within the window"* metric on the downside-unconstrained path.
2. **Post-v51 failure rates say nothing about executed stop-out risk.** The engine now places, and the bridge/autotrader now executes, a stop clamped to 1.6×ATR (5.5× tighter than the eval's barrier). An autotrader running the placed stop would stop out far more often than the ~0.3% the eval implies. The metric is **optimistic on the downside** for the executed geometry.
3. **This is not new at v51 — but it now matters.** The eval always used the raw swing; what changed at v51 is that the *executed* stop became a defined tight 1.6×ATR the machine acts on. The measurement–execution gap went from academic to material the moment the bridge unlocked live-at-min-size.

Net: **trust post-v51 SUCCESS rates as a target-reach signal, but do not read them as win/loss for the executed (placed-stop) trade.** That is exactly the D6 caveat now documented in UserManual §21c / TraderGuide perf-strip.

---

## 5. Recommendation (code phase — separate, spec-first)

**Migrate the eval adverse barrier from the raw swing stop onto the logged placed stop** (`PlacedStop{Long,Short}`, CSV 107/109), so outcomes are scored against the geometry actually executed:

- `LivePerformanceTracker.vb:931/934` — `AdvBar` uses `swingStop{Long,Short}` → change to the placed clamped stop.
- Offline `FailureRateMatrix` adverse barrier (analysis/, §18 swing-else-1.2 convention) — same migration; pin the exact site at build time.
- Re-sync the fallback constants to the v51 placement fallbacks: `LivePerformanceTracker.FAV_ATR_MULT` 2.0→1.75 and `ADV_ATR_MULT` 1.2→1.6 (`:105/106`); `AnalysisConstants.EngineTargetAtrMultiplier` 2.0→1.75, `AdverseFallbackAtrMultiplier` 1.2→1.6 — for the rare fallback rows.

**Then re-walk OHLC** (`ohlc_1m_cache.csv`) to re-score outcomes and measure the failure-rate shift. The eval cache stores the verdict, not the intrabar path, so the flip count (SUCCESS/WINDOW_EXPIRED → ADVERSE_HIT) **requires the OHLC walk** — hence "code second." Directional expectation: **failure rates rise materially** once the 1.6×ATR stop begins to bind.

**Two decisions for the trader before building (spec-first):**
1. **Replace or dual-track?** The swing-based metric answers *"is the discretionary swing-stop trade good?"*; the placed-stop metric answers *"is the executed autotrade good?"*. With the autotrader now the high-stakes consumer, placed-stop should be primary — but the swing view could be retained as a secondary column rather than discarded.
2. **Eval-cache boundary.** Changing the barrier changes every stored outcome → the `analysis_eval_cache.csv` must be rotated/rebuilt, and the live perf-strip history resets. Not a scoring/verdict change (no ⚠ signal boundary), but it is an eval-semantics boundary that wants a note in `change_log` and a fresh eval-cache schema tag.

---

## Appendix — reproduction

Corpus: `bin/Debug/net8.0-windows/analysis_log.csv` (v0.8). Directional filter: `Verdict ~ /LONG|SHORT/ && !/NO TRADE/`. Per-row emit (side-selected cols) → distances in ATR → percentiles via `sort -n`. Full command set in the 2026-07-08 session transcript. Raw swing = cols 83/84; placed stop = 107/109; placed target = 106/108; entry = col 2; ATR = col 65; ExecResolution = col 94.

---

## Addendum — coordinator re-verification, 2026-07-13 (Fable seat)

Independent re-run on a **frozen copy** of the corpus (now 2,246 data rows, 07-03 → 07-10 20:35 UTC; the CSV has no quoted fields, so the comma-split parse is safe). Three slices:

| Slice | n (directional) | raw swing p50 (×ATR) | placed stop p50 (×ATR) | swing >3×ATR |
|---|---|---|---|---|
| **A — rows ≤ 07-08 13:47 (the original window)** | 264 | 9.33 | 1.60 | 93% |
| **B — full corpus (through 07-10)** | 530 | 8.32 | 1.60 | 93% |
| **C — STRONG+standard only (the offline matrix's population)** | 178 | 8.79 | 1.60 | **98%** |

**Verdict: the finding REPRODUCES and STRENGTHENS.** Slice A matches the committed figures (the n=266/264 delta is live-file drift — the original ran against an appending collector; stats are identical). Slice B doubles the sample and holds. Slice C matters most: the offline `FailureRateMatrix` excludes WEAK rows, and on *its* population the adverse barrier is >3×ATR away on 98% of rows — the divergence conclusion holds for **both** eval surfaces' populations.

**Corrections to the 07-08 body (cosmetic, none change the conclusion):**
1. "925 rows" → the file had 920 data rows at the 07-08 13:47 stamp (live-file drift; all stats re-pinned on the frozen copy above). **Discipline note for future passes: freeze the CSV before computing stats — the collector appends mid-analysis.**
2. "100% of directional rows have a swing stop" → **99.4%** on the fuller book (3 of 530 rows lack one) — the 1.2×ATR eval fallback *can* fire, rarely. Immaterial.
3. Placed-target p90 moves 2.42 → 2.87×ATR on the full corpus (more structural placements landing); p50 stays 1.75.

**Offline adverse-barrier sites — now pinned exactly** (replacing §5's "pin at build time"):
- `analysis/FailureRateMatrix.vb:158-170` — the Compute walk's `advBar` (raw `SwingStopLong/Short`, else `AdverseFallbackAtrMultiplier`).
- `analysis/AnalysisRunner.vb:241-245` — the runner's own `advBar` derivation (same convention).
- `analysis/AnalysisConstants.vb:28` — `AdverseFallbackAtrMultiplier = 1.2` (+ `EngineTargetAtrMultiplier = 2.0` at `:44`).
- Live tracker: `LivePerformanceTracker.vb:929-935` (as per the body), constants at `:105-106`.

§5's migration recommendation and the two trader decisions stand unchanged.
