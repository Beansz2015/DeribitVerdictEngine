# Backtest Synthesizer — Proposal (historical replay through the pure engine core)

**Date:** 2026-07-30 (Fable coordinator; trader-initiated 2026-07-30 — the data-collection-time concern). **Status:** PROPOSED — D1–D8 await trader tick. **Build:** Opus month, ~1–2 weeks of lanes; usable ~mid-Aug.
**Class:** new host-agnostic console project `tools/BacktestRunner/` (own .vbproj, zero WinForms — the AutoTweaker/WhatIfRunner pattern). **ZERO engine changes, zero settings keys, no ⚠, no dataset boundary** — the engine binary, the live collectors, and every live surface are untouched.

## 0. The design inversion that makes it cheap

The expensive version of this idea — a mock Deribit API feeding the live app at accelerated pace — needs the headless run-state extraction (Q6, trader-deferred to LAST) plus a clock abstraction through every `UtcNow` site. **This proposal does neither.** The engine's load-bearing core is already pure: every `Indicators_*` function takes candle/trade lists as arguments; `ScoringEngine.Calculate` takes `(r, posState, norms, cfg)`; `ComputeSideLevels` is the one seam; `ExecutionResolution` takes a UTC hour; the v53 funding ring takes explicit timestamps. The ordercheck harness already drives the real `Calculate` this way; WhatIfRunner established link-don't-copy. The backtester is the same move one level deeper: **fetch raw history, assemble `IndicatorResults` per virtual bar-close, run the real pipeline, write a synthetic book.** Q6-last stands unreversed.

## 1. Scope v1 — what reconstructs, what mutes (the fidelity policy)

**Reconstructed at full fidelity (candle/trade-derived):** ROC, RSI + divergence, DMI/ADX + regime (+ hysteresis, replayed sequentially), ATR, Volume SMA, VWAP + bands (dual-session anchors from timestamps), BBW/TTM, EMA ribbon + 200_5m, Donchian, OBV, VPFR-lite v2, swing pivots + BestPivot, TrendStructure, MTF gate (15m candles), CVD, MicroCVD, TFI, **aggressor velocity** (trades-only), **liquidations** (historical trades carry the liq flag), DynamicNorms + session volume (computed from the synthetic candle window).

**Approximate (documented):** funding — `get_funding_rate_history` is coarser than live per-run sampling; the 5-min-anchored momentum window over interpolated points will read FLAT more often than live. Step 3/3b run, states labelled approximate; the validation diff (§4) quantifies the drift.

**MUTED (no free historical source — votes never fire):** OFI + OFI momentum, SpreadBps penalty, OI (`OISignal` NEUTRAL ⇒ Pass 2b inert), absorption (inert live anyway). Consequence: synthetic scores skew conservative vs live. **This is disclosed, not patched** — no synthetic stand-ins, no fabricated book state. Tier-2 (own later decision, not this spec): paid L2/OI history (Tardis-class) closes the gap at real fidelity.

## 2. Population discipline (non-negotiable)

- Output is its **own file** (`backtest_log_<runid>.csv`, v0.8 schema + a `BACKTEST` InstanceId prefix) in its own directory — **never appended to, pooled with, or placed beside `analysis_log.csv` as if live.** The LEGACY_YARDSTICK precedent: a labelled population, joined only deliberately.
- Legitimate uses: indicator-level threshold sweeps (which What-If cannot do — it replays logged scores), geometry/session studies at depth (W6-1, v56 modes, D2-v2), ASIA/LONDON derivations for trades-derived signals (§5.2-class), D3–D6 evaluations, W6-4-style modeling on the reconstructable feature subset.
- **Forbidden uses:** calibrating the muted signals (OFI/OI/spread/absorption); Kelly CAL win rates; anything whose deliverable is a live-population rate. Live confirmation via What-If remains the ladder's second rung before any live change.

## 3. Mechanics

- **Historical store:** fetch-once local cache (`backtest_data/`) — 1m candles (chart endpoint; 5m/15m aggregated locally from 1m, matching live aggregation), raw trades (`get_last_trades_by_instrument` pagination; months = millions of rows, hours of polite fetching, resumable), funding history. Store format: plain CSV/binary per month; fetcher respects rate limits (the `ExecuteWithRetry` discipline).
- **Replay loop:** iterate bar-closes at the session's execution resolution (1-min NY / 3-min Asia-London via the same `session_volume` cfg); per close, slice the store into the exact live shapes (250×1m, 210×5m, 70×15m, last-500 trades) → assemble `r` → `DynamicNorms.Compute` → `RunScoringPipeline`/`Calculate` → `ComputeSideLevels` → row out. Sequential state (regime hysteresis, funding ring, VWAP anchors) carried in plain locals — no engine state objects needed. `posState = None` throughout (hold/exit not exercised in v1).
- **Config:** one pinned `settings.json` path per run (defaults to the repo file). No grid machinery in v1 — sweeps are multiple runs compared by the existing offline tools; the What-If grid/overfit guard-rails are not duplicated.
- **Consumption:** the synthetic book reads through the SAME offline surfaces — which is the convergence bonus: the **pooled-file report runner** (the F1 §9-on-pooled-book affordance already queued for the Aug-1 session) is the identical small console shape; one tool serves both.

## 4. Acceptance — the overlap validation IS the instrument

Build acceptance is not "it runs"; it is the **overlap re-run**: backtest the exact live-collection window (2026-07-03 → present) and diff synthetic vs live rows per bar-close:
- Reconstructable indicator columns within tolerance (report per-column match rates; investigate any systematic drift — it means the assembly differs from live plumbing).
- **Verdict agreement rate reported honestly, with the muted-vote delta quantified** — this measurement is independently valuable: it is the first empirical read of how much OFI/OI/spread actually move verdicts (a W6-4-adjacent result for free).
- Funding-state drift quantified (the §1 approximation, measured not assumed).
- Fixture family (next-free at build): assembly slicing exactness (window sizes/order — the `LastN` contract), sequential-state replay (hysteresis/funding ring vs a hand-walked case), provenance stamping, muted-vote inertness.
Plus the standing gates: 5 projects + BacktestRunner build 0/0 Release; harness unregressed; verify-gate prepush; Release-only builds; local-first.

## 5. What this does NOT touch

The live collectors (still irreplaceable for book/OI/fills/population), the What-If runner (stays the live-book confirmation instrument and the order-app counterfactual), the tweaker (fires on live windows only), the bridge, Q6 (unreversed), and every live surface.

## 6. D-table (await trader)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Architecture | **Link the pure core in `tools/BacktestRunner/`** (no API mock, no engine changes, Q6-last stands) |
| D2 | v1 signal scope | **§1 as written** — full candle/trade set; funding approximate; OFI/OI/spread/absorption muted, disclosed |
| D3 | Population policy | **§2 as written** — BACKTEST-prefixed own file, forbidden-use list binding |
| D4 | Acceptance | **The §4 overlap validation** with quantified verdict-agreement + muted-vote delta |
| D5 | Sweep machinery in v1 | **None** — one cfg per run; comparison via existing offline tools; What-If guard-rails not duplicated |
| D6 | History depth for the first store | **6 months** (balances fetch time vs study value; extend later — the store is append-forward) |
| D7 | Tier-2 paid L2/OI history | **Defer** — revisit only after the free tier proves value on ≥2 real studies |
| D8 | Roadmap slot | **Early Opus month, after the absorption mechanism spec** — it accelerates W6-1/v56/D2-v2/§5.2 and every future re-baseline; the Aug-1 handover names it |
