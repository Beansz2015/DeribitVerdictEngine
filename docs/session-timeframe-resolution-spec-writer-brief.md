# Session-Timeframe Resolution — Spec-Writer Coordination Brief

**Date:** 2026-06-15
**From:** Opus seat that ran the Phase-0 study + drafted the proposal.
**To:** spec-writer seat.
**Purpose:** finalize and **coordinate** the session-conditional execution-timeframe feature as a NEW feature — sequence it against the in-flight v35 / auto-tweaker / calibration arc, resolve the open design questions, and produce the implementer hand-off. The design is drafted in [`session-timeframe-resolution-proposal.md`](session-timeframe-resolution-proposal.md); **you finalize + coordinate it, not start from scratch.**

This is a coordination brief, not a re-spec. The trader has signed off on the *what* (below); your job is the *how-it-fits* and the *how-it-builds*.

---

## 1. Context (one paragraph)

The v35 pair (min-tradeable-move gate + eval de-confound, commits `3705d92` + `db050a5`, local-unpushed) is live. The gate (0.08% floor) correctly suppresses 1-min Asia/London verdicts whose target can't clear slippage. The trader is moving their **execution to 3-min charts in Asia/London** and wants the engine to analyze the same timeframe it advises on; NY stays 1-min. A 28-day ATR/price study confirmed the picks.

## 2. Locked decisions — do NOT re-litigate

| Decision | Value | Note |
|---|---|---|
| Approach | **Path B** (coherent: the *whole execution stack* moves to the session resolution) | Path A (ATR-only swap) rejected — a 1-min direction with a 3-min target is incoherent and pollutes the book with tradeable-size / wrong-direction trades. |
| Resolutions | **ASIA = 3, LONDON = 3, NY = 1** | Data-backed (§3). Trader-confirmed 2026-06-15. |
| Weekend overlay | **OUT** | Trader trades rarely on weekends; weekends stay 3-min and partly gated (acceptable). |
| Toggle semantics | **Configurable-but-stable** | Hot-reloadable key, but a change is a re-baseline boundary, not a daily toggle. |
| Per-row stamp | **Required** | `ExecResolution` on every CSV + eval-cache row so resolutions never pool. |
| Regime / MTF | **UNCHANGED** (5-min DMI/ADX regime; 15-min MTF) | Bounds the change; valid HTF context above a 3-min chart. |
| Auto-tweaker surface | **OFF** | `execution_resolution` joins `min_tradeable_move_pct` / `kelly.*` in PromptBuilder HARD CONSTRAINT 11. |

## 3. Evidence — Phase 0 (DONE, don't re-run)

28-day native Deribit BTC-PERPETUAL 1/3/5-min OHLC (40,321 × 1-min), ATR(7)/price bucketed by session × resolution × weekday/weekend. Gate clears at `ATR/price ≥ 0.04%` (i.e. `2×ATR ≥ 0.08%×price`). Median `2×ATR` target as % / % of bars clearing the floor:

| Session | 1m WKDY | 3m WKDY | 5m WKDY | 1m WKND | 3m WKND | 5m WKND |
|---|---|---|---|---|---|---|
| ASIA   | 0.089% / 56% | **0.191% / 95%** | 0.268% / 99.8% | 0.045% / 30% | 0.105% / 66% | 0.146% / 88% |
| LONDON | 0.079% / 50% | **0.174% / 91%** | 0.244% / 99% | 0.038% / 28% | 0.087% / 55% | 0.130% / 78% |
| NY     | **0.127% / 71%** | 0.262% / 96% | 0.357% / 99.6% | 0.069% / 43% | 0.154% / 85% | 0.213% / 97% |

Key findings: (1) **weekday Asia/London 1-min is marginal, not dead** (~50–56%, median target on the floor) — the "0 predictions" was largely a **weekend** artifact (28–30%), the same weekday/weekend confound that bit v34's ASIA `session_volume`; (2) **3-min is the right lift** (Asia 56→95%, London 50→91%); 5-min overkill; (3) NY 1-min adequate (71%); (4) **ATR scales ~2.1× (1→3-min), ~2.9× (1→5-min)** — the threshold seed factor; (5) weekends stay partly gated at 3-min (accepted). Method is reproducible from the proposal §1 (native `public/get_tradingview_chart_data`; ATR/price is price-regime-independent; volume not needed).

## 4. Coordination tasks — the core of this brief

### 4.1 Sequencing vs the v35 auto-tweaker first fire (most important)

The supervised first fire was gated on v35 (now satisfiable). **Reconciliation — they're more orthogonal than they look:** NY stays 1-min under v36, and the first fire is meant to run on a **NY weekday window** (per the project §12 sequencing note). So **the v35 first fire can proceed on NY/1-min independent of v36** — v36 does not change NY. What v36 adds is the requirement that the tweaker **never pool 3-min Asia/London rows** once they exist (§4.4). Recommendation: let the v35 first fire validate mechanics on NY/1-min as planned; ship v36; then the tweaker's resolution-awareness (§4.4) is the precondition for it to ever consider Asia/London. Confirm this ordering with the trader; don't block v36 on the first fire or vice-versa.

### 4.2 Reconcile with the v34 weekday-ASIA `session_volume` re-verify (WATCHING item)

This Phase-0 study **already produced the weekday-vs-weekend ASIA volatility split** the v34 re-verify needed (weekday Asia ≈ 2× weekend). The v34 ASIA `high/mid_mult` 1.10/1.05 was set on a 100%-Saturday sample → weekend-biased. Two coordination points:
- Fold the v34 weekday-ASIA re-verify into this work's data pass rather than running it twice (the deeper historical fetch covers both).
- **At 3-min, the `session_volume` multipliers + volume thresholds were calibrated on 1-min volume** — they may not transfer. Decide whether the 3-min profile (§4.5) overrides volume thresholds / session multipliers, or whether `session_volume` stays 1-min-calibrated and is re-verified separately. Flag the interaction; don't silently inherit.

### 4.3 Schema bumps — batch them

v36 needs: `analysis_log.csv` **v0.6 → v0.7** (`ExecResolution` column, appended — header-name readers tolerate it), eval cache **v3 → v4** (`ExecResolution` field), settings **v35 → v36**. Coordinate with other pending CSV work so the book doesn't churn twice: the v34-flagged **"log weightedSlope next CSV bump"**, and the **Spec C SC/TOTAL parity** item (from the UI-reskin arc). If those are near, batch them into the v0.7 bump.

### 4.4 Auto-tweaker resolution-awareness (precondition for post-v36 fires)

It's already **session-blind**; v36 adds **resolution-blind**. Before it tunes on any post-v36 data it must filter the failure-rate matrix + CSV rows by `(session × resolution)` so it never pools 3-min Asia/London with 1-min NY. The per-row `ExecResolution` stamp makes this possible. Spec the filter; note it compounds the existing session-blind gap (both want fixing before un-gating).

### 4.5 Threshold-profile design (biggest open design decision)

The proposal §4 sketches a `resolution_profiles` block keyed by `"1"`/`"3"`/`"5"`, `"1"` = current globals, others overriding only the timeframe-sensitive keys (everything else inherits 1-min). You must finalize: the config taxonomy, exactly which keys scale and how, and the Phase-1-seed vs Phase-2-rebaseline boundary. Seed factor from the study: **~2.1× for 3-min** on magnitude-type keys (ROC magnitude/slope-delta, MicroCVD static floor, CVD slope-min-usd). Bar-count windows (ATR period, RSI, Volume SMA, BBW, Donchian, DynamicNorms baselines) — **decide: keep the bar count (they span N× wall-clock) vs rescale**; default keep-count, but call it explicitly (a mini-study may be warranted). Honest caveat to carry into the spec: seeded 3-min thresholds lean aggressive on the magnitude-gated signals until Phase-2 re-baseline.

### 4.6 Smaller but mandatory implementation details

- **Session resolver must use the ENGINE bucket, not the display label.** `ApplySessionVolume` buckets ASIA = UTC hour 0–7 inclusive; the display `ResolveSessionLabel` has the v34-flagged hour-7 off-by-one (`<7`). The resolution boundary MUST equal the gate/eval session boundary — reuse the engine path or fix the label.
- **Candle-freshness gate (D5 `IndicatorEngine.IsFresh`, "stale if older than 2× resolution").** It must take the *execution* resolution — a 3-min bar is fresh up to ~6 min, not 2. Today it's wired with the 1m/5m literals; v36 must pass the active resolution.
- **Fetch path:** `RunAnalysisAsync` fetches the execution stack via `GetCandlesAsync("3"/"5", N)` in those sessions (replacing the 1-min execution fetch); 5m regime + 15m MTF fetches stay. Eval/perf barrier walks still use 1-min OHLC (price walk, timeframe-independent) — only the `FavBar` *distance* changes.
- **Display-parity rule (CLAUDE.md, hard):** if the resolution surfaces in any rendered line (e.g. an "ATR (3m)" tag in the ATR-levels header), the card binding **and** `BuildPlaintextSnapshot` must change in the same commit.
- **Eval-cache `BuildEntry` / `ReevaluateForFloor`** already key off `r.ATR`; once ATR is resolution-correct they inherit it — but the stamp + resolution-filtered aggregation in `LivePerformanceTracker.BuildAggregate` / perf strip must be added so per-session rates aren't blended.

## 5. Open design questions you (spec-writer) own — not the trader

1. Config taxonomy: extend `session_volume.sessions[]` with `execution_resolution` (DRY — single session-bucket source of truth; **recommended**) vs a new top-level block.
2. `resolution_profiles` exact structure + key list + inheritance model.
3. Bar-count windows: keep-count vs rescale (§4.5).
4. Phase-1 minimal seed set vs full profile populated.
5. A14 acceptance fixtures (resolution selection; ATR-on-3-min; gate flips for a setup that failed at 1-min; NY byte-identical; per-row stamp; regime/MTF unchanged).
6. Whether `session_volume` volume thresholds belong in the resolution profile (§4.2).

## 6. Out of scope (state in the spec so it isn't re-opened)

Weekend resolution overlay (trader: out); profile ATR-band recalibration (Low<80 set for $80–100k — independent housekeeping); any native-5-min use (5-min isn't selected in v1).

## 7. Constraints to respect (trader profile + CLAUDE.md)

- **Spec-first + approval-gated** — scoring-affecting; the proposal is the approval artifact, but re-confirm the final threshold-profile design with the trader before code (it changes verdict behaviour in Asia/London).
- **No double-counting, no non-directional padding, structural targets preferred, ADX-proximity (not flat) penalties** — the higher-timeframe stack must preserve these; don't let a rescale reintroduce a removed pattern.
- **Host-agnostic** for `analysis/` + `tools/` (Linux-port rule). The fetch/session-resolver lives UI-side (`MainForm_Analysis`) which is fine, but any shared helper stays WinForms-free.
- **Local commits only; trader tests + pushes.** Per-version §15 entry + `settings.json` change_log on the v36 bump.

## 8. Recommended sequence (one line)

v35 first-fire (NY/1-min, mechanics validation) → finalize v36 threshold-profile design w/ trader → **Phase 1** (config + per-session fetch + execution-stack-on-resolution + ATR + `ExecResolution` stamp + resolution-filtered perf/eval + seeded 3-min profile; regime/MTF untouched) → accumulate 3-min Asia/London data → **Phase 2** (per-resolution re-baseline + auto-tweaker resolution-awareness before any fire on mixed data).
