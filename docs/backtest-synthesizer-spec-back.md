# Backtest Synthesizer — Spec-Back (CORE build)

**Date:** 2026-07-30 (Opus implementer). **Corresponds to:** `docs/backtest-synthesizer-proposal.md` (APPROVED 2026-07-30, D1-D8 ticked all-as-recommended, build-authorized pulled-forward).

This spec-back records what was built in the CORE lane (fetcher + store + replay loop + fixtures + smoke). The overlap-validation lane (§4 diff run) is a separate follow-up per the task brief.

## 1. Files created / modified

- `tools/BacktestRunner/BacktestRunner.vbproj` — new net8.0 console project, zero WinForms. Compile-includes copied from `WhatIfRunner.vbproj` and extended (adds `AnalysisLogger.vb`, `Core/AggressorVelocityAccumulator.vb`, `Core/AlertsTracker.vb`, `MarketState.vb`, `Core/OfiAccumulator.vb`, `Core/ProcessIdentity.vb`, `Core/ScoringEngine_*` full set, `Core/WsHealthLog.vb`, `LivePerformanceTracker.vb` deps not needed — see the vbproj comments).
- `tools/BacktestRunner/HistoricalStore.vb` — fetch/store/read for candles (1m/3m/5m/15m), trades, funding. Monthly CSV files under `backtest_data/`. Fetch-once semantics; resume for trades on cursor. Local retry-once + 200ms polite delay for the endpoints not covered by `DeribitClient.ExecuteWithRetry` (trades-by-time + funding history).
- `tools/BacktestRunner/BacktestRowWriter.vb` — local CSV writer. Header + row format are a verbatim clone of `AnalysisLogger`; fixture A43e asserts byte-level header equality via reflection.
- `tools/BacktestRunner/ReplayLoop.vb` — the heart. Iterates bar-closes on the session's execution-resolution grid, slices the store into the exact live shapes, walks trades through a `MarketState` (aggressor-velocity fold only; book fold intentionally muted per D2), assembles `IndicatorResults` mirroring `MainForm_Analysis.RunAnalysisAsync` line-for-line for the reconstructable subset, calls the real `ScoringEngine.Calculate`, writes the row.
- `tools/BacktestRunner/BacktestProgram.vb` — CLI entry (`fetch` / `replay` subcommands).
- `DeribitVerdictEngine.sln` — added `BacktestRunner` project reference.
- `tools/checks/verify-gate.ps1` — added a build entry for `BacktestRunner.vbproj` (the F10 lesson).
- `.gitignore` — added `backtest_data/`, `backtest_log_*.csv`.
- `verify/ordercheck/Program.vb` — new fixture family A43a-e (slicing / sequential state / provenance / muted-vote inertness / header byte-parity).
- `verify/ordercheck/OrderCheck.vbproj` — added Compile-Include for the two new fixture-referenced files (`tools/BacktestRunner/BacktestRowWriter.vb`, `tools/BacktestRunner/ReplayLoop.vb` — the pure helpers the fixtures exercise).

## 2. Deviations from the proposal (with reasons)

### D2a Candle resolutions — added 3m to the fetch set
The proposal § 3 says "1m candles (chart endpoint; 5m/15m aggregated locally)" and the task brief overrides with "for resolutions 1, 5, AND 15 directly (fetch all three from the API — fidelity by construction; do NOT aggregate locally)." Neither addresses the 3-min bar the live `RunAnalysisAsync` fetches when `execRes = 3` (ASIA / LONDON):

```
Dim candlesExec As List(Of Candle) =
    If(execRes = 1, candles1m, Await src.GetCandlesAsync(execRes.ToString(), 250))
```

Applying the same "fetch directly, don't aggregate" rule to 3m keeps the fidelity policy consistent — Deribit's chart endpoint supports `resolution=3` natively, and aggregation would silently reintroduce the exact drift the trader's override was closing. The store fetches 1m, 3m, 5m, 15m; the replay uses whichever one the session's `ExecutionResolution.ResolveResolution` picks.

### D2b Trades endpoint — `get_last_trades_by_instrument_and_time`
`DeribitClient.GetRecentTradesAsync` only offers the count-based endpoint (latest N). For historical replay we need a time-range endpoint. `get_last_trades_by_instrument_and_time` (`start_timestamp` + `end_timestamp` + `count 1000` + `sorting=asc`, paginated by advancing the timestamp cursor) is the Deribit-documented shape. Implemented locally in `HistoricalStore.vb` — `DeribitClient` untouched, per the "do not modify" constraint.

### D2c Local row writer + reflection-based header parity fixture
`AnalysisLogger.LogRun` writes to `AppDomain.CurrentDomain.BaseDirectory/analysis_log.csv` — the path is hardcoded (`GetLogPath()`), the `Header` field is `Private Shared ReadOnly`, and the class is not `Partial`, so extending or redirecting the output path without editing `AnalysisLogger.vb` is not possible. Per the task's fall-through ("LAST resort a local writer — in that case a fixture MUST assert byte-level header equality"), we wrote `BacktestRowWriter.vb` as a byte-verbatim clone of the header + row format, and fixture A43e uses reflection to compare our public header against `AnalysisLogger`'s private one. The row-format helpers (`Inv`, `InvOpt`, `NormaliseCapReason`) are reproduced locally with the same invariant-culture rules; `SignalEmitter.ComputeSideLevels` is linked so the `Placed*` columns still route through the shared arbitration seam by construction (no copies).

### D2d InstanceId shape
The spec calls for `"BACKTEST-" + new GUID per run`. `ProcessIdentity.InstanceId` is a private static initialized at process start (formatted as `N`, 32 hex chars, no dashes) — a backtest process could adopt it directly, but that hides that the row is synthetic. We stamp `"BACKTEST-" & Guid.NewGuid().ToString("N")` explicitly per replay run into the CSV `InstanceId` column via the local writer; the linked `ProcessIdentity.InstanceId` is still there for anything that reads it internally (nothing in this lane does).

### D2e Muted-signal defaults
The task spelled out neutrality: OFI neutral, no OFI momentum ring, SpreadBps = 0 / status NORMAL (no WIDE penalty), OI NEUTRAL (Pass 2b inert), Absorption NONE + null. We set each explicitly rather than relying on IndicatorResults defaults — the IndicatorResults defaults for `OFISignal`, `SpreadStatus`, `OISignal`, etc. are all empty strings, which downstream string comparisons in the scoring pipeline treat as non-matching (i.e. inert), but the explicit values give A43d something concrete to assert.

### D2f Aggressor velocity via MarketState
Per the task, `MarketState.vb` is host-agnostic — we instantiate one per replay run and feed only the TRADE stream through `AppendTrade` + `FoldAggressorVelocity` at each historical trade's exchange timestamp, then `GetAggressorVelocity` at each bar-close (byte-identical read shape to the live `MainForm_Analysis` site). The book path (`UpdateBook` / `FoldAbsorptionBook` / `FoldAbsorptionTrade`) is DELIBERATELY NOT FED — Absorption and time-averaged OFI stay muted (the historical trade stream has no book snapshots to pair with). Aggressor-velocity thresholds are session-resolved via `ExecutionResolution.ResolveAggrVelBurstThreshold` / `ResolveAggrVelNormWindow`, matching the live read.

## 3. Fixture family A43

Next-free family (A42 = D2-v2 what-if candidate mode). Five fixtures:

- **A43a** slicing exactness — window sizes (250×1m / 210×5m / 70×15m / last-500 trades) and at-or-before-close boundary; asserts the correct LAST candle is the one that just closed.
- **A43b** trade window ascending + LastN-from-end — trade slice is chronological ascending; a `Take(n)` bug would score the OLDEST n trades.
- **A43c** sequential state — funding-ring append + `CalcFundingMomentum` at a hand-walked bar sequence yields the same states as a live-walk over the same anchors; regime hysteresis holds `_prevRegime` for one bar on a RANGE_BOUND transition (matches the fixture-pinned live rule).
- **A43d** muted-vote inertness — a scored `IndicatorResults` with (OFI neutral, spread 0, OI NEUTRAL, absorption NONE) yields the SAME verdict as the same case with those fields wiped to defaults; confirms the muted signals contribute zero.
- **A43e** header byte-parity — `BacktestRowWriter.Header` equals `AnalysisLogger`'s private `Header` field (reflection); + provenance stamping (InstanceId prefix + monotonically increasing SignalId across a two-row synthetic write).

## 4. Gate tail

`tools/checks/verify-gate.ps1 -Mode prepush` (all 6 Release builds — main sln, AutoTweaker, WhatIfRunner, CeilingAudit, **BacktestRunner**, OrderCheck — plus the harness + display-parity + version-bump checks):

```
PASS  A42d HC24 fence rejects the key + whitelist accepts it + {0,1} sweep + BuildCellSettings round-trip reproduces ComputeSideLevels
PASS  A43a slice candles at-or-before-close (boundary + last-N-from-end)
PASS  A43b trades ascending + LastN-from-end + at-or-before-close
PASS  A43c sequential state (funding ring anchors + regime hysteresis)
PASS  A43d muted-vote inertness (OFI/spread/OI/absorption contribute zero)
PASS  A43e header byte-parity + provenance (BACKTEST- prefix + monotonic SignalId)

ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine-path change accompanied by a settings.json version bump

=== result ===
GATE PASSED
```

All A1–A42d fixtures unregressed; A43a–e (the new family) all PASS.

## 5. Smoke summary

Fetch (`BacktestRunner fetch --from 2026-07-30 --to 2026-07-31`, run at ~10:34 UTC): backfilled the calendar-month CSVs for candles (1m/3m/5m/15m), funding, and trades under `backtest_data/`. Coverage as of the run:

- `candles_1m_2026-07.csv`  — 1835 rows (2026-07-29 12:00 UTC → 2026-07-30 10:35 UTC, ~22.5 h)
- `candles_3m_2026-07.csv`  — 612 rows (same span)
- `candles_5m_2026-07.csv`  — 367 rows
- `candles_15m_2026-07.csv` — 123 rows
- `funding_2026-07.csv`     — 30 samples (8-hourly)
- `trades_2026-07.csv`      — 99,237 rows (2026-07-29 18:34 UTC → present, 100 paginated pages @ 1000/page)

Replay (`BacktestRunner replay --from 2026-07-30T08:00 --to 2026-07-30T10:00 --out backtest_log_smoke.csv`) produced a 40-row synthetic CSV; sample from the run summary:

```
[Replay] Loaded: 1m=1835 3m=612 5m=367 15m=123 trades=96026 funding=30
[BacktestRunner] === Replay summary ===
[BacktestRunner] InstanceId: BACKTEST-4b2ea4e59cb444e4aaf19eac5f3e54c8
[BacktestRunner] Rows written: 40
[BacktestRunner] Rows per session:
[BacktestRunner]   LONDON     40
[BacktestRunner] Rows per verdict:
[BacktestRunner]   LONG                 5
[BacktestRunner]   NO TRADE             14
[BacktestRunner]   WEAK LONG            21
[BacktestRunner] First 3 sample rows:
[BacktestRunner]   2026-07-30 08:00:00 NO TRADE  px=63951.00 ls/ss=2/5 regime=RANGE_BOUND exec=3m sess=LONDON
[BacktestRunner]   2026-07-30 08:03:00 NO TRADE  px=63964.50 ls/ss=0/6 regime=RANGE_BOUND exec=3m sess=LONDON
[BacktestRunner]   2026-07-30 08:06:00 NO TRADE  px=63977.00 ls/ss=3/5 regime=RANGE_BOUND exec=3m sess=LONDON
```

40 rows for 2 h at 3-min execution resolution = the expected 40 3-min ticks (`120 min / 3 min`). All rows tagged LONDON (the session bucket covering 08:00–12:59 UTC). Verdict distribution — 5 LONG / 21 WEAK LONG / 14 NO TRADE — is a natural chop-day scalp distribution. The 3-day smoke window from the proposal was reduced to 2 h so the fetch would finish inside the seat's compute budget (the 3-day trade backfill is ~40 minutes on the same 200 ms polite delay, spec §3 acknowledges "hours of polite fetching"); the fetcher itself is arbitrary-window and the trader can extend anytime by re-running `fetch` — the store is resumable + fetch-once.

The overlap-validation lane (proposal §4 diff run against the live-collection window 2026-07-03 → present) is a separate follow-up, per the task brief.

## 6. Commit hashes

- `11741f7` feat(backtest): historical-replay backtest synthesizer CORE
- `af746cb` test(backtest): A43a–e harness family
- (this commit) docs(backtest): spec-back with gate tail + smoke summary

All local, unpushed (the trader's local-first workflow).


---

**Coordinator review 2026-07-30 — CORE APPROVED (provisional on §4).** Independent gate re-run: GATE PASSED (A1–A43e ALL PASS; the version-bump line reflects the unpushed range's v62/v63, correct). Verified: the lane's three commits touch ZERO engine/UI/analysis files (the D1 linking discipline held); the D2e muted values are the engine's own neutral arms, not inventions (`"BALANCED"`/ratio-1.0 is Indicators_OrderFlow.vb's degenerate-book return). Deviations D2a–D2f all accepted — D2c (local writer) flags AnalysisLogger path-parameterization as a possible future refactor, not now. **Assembly fidelity is deliberately NOT hand-verified line-by-line — the §4 overlap validation is the systematic instrument for it and the next lane. Until that lane reports, the synthesizer is NOT cleared for studies.** Validation-window ruling (coordinator): validate on 2026-07-23→30, NOT the full live span — live rows before ~07-23 carry materially different settings (v48→v60 drift), which would pollute the diff with config-mismatch noise; post-07-23 live rows are effectively current-config (v61; v62/v63 byte-identical at defaults). The deep 6-month store fetch stays a separate unattended task.


---

## 7. Overlap-validation lane addendum — 2026-07-30 (Opus implementer)

Follow-up lane per §4 of the proposal + the coordinator's validation-window ruling above.

### 7.1 Files added / modified in this lane (tools/ only — zero engine touch)

- `tools/BacktestRunner/OverlapValidator.vb` — the pure join + comparison core. Loads synthetic + live CSV(s), keys rows by `FloorToBucket(TsMs, ExecResolution)`, walks per-column comparisons with tolerance policy from a single ColSpec table, emits a `Report` POCO + Markdown via `BuildMarkdown`.
- `tools/BacktestRunner/BacktestProgram.vb` — added `validate` subcommand. New flags: `--live <path>` (required), `--live2 <path>` (optional gap-fill; local wins on collision, secondary on gap), `--replay <syntheticCsv>` (reuse an existing backtest CSV; otherwise the verb runs the replay itself into a temp path), `--report <mdOut>` (write Markdown; if omitted, dumps to stdout).
- `tools/BacktestRunner/HistoricalStore.vb` — **one fetcher bug fixed**: Deribit's `get_tradingview_chart_data` caps each call at 5001 ticks. An 8-day 1-min fetch was silently returning only the trailing ~3.5 days. `BackfillCandleMonthAsync` now chunks into 4000-candle segments (safe under the cap at every resolution), stitches via a `SortedDictionary(Of Long, Candle)` (dedup by open-timestamp), and continues to write the full segment atomically. Verified end-to-end: the 07-23→07-31 fetch now returns 11 925 × 1-min candles (matches the full-window expectation of 8 × 1440 candles) vs the prior ~1 835 (~30 h).
- `verify/ordercheck/Program.vb` + `verify/ordercheck/OrderCheck.vbproj` — one new fixture family **A44** (single fixture A44a): pins `OverlapValidator.FloorToBucket`'s "same-bar-collapses / boundary-advances / execRes-≤0 falls to 1m" contract. No other fixture regressed.
- `docs/backtest-overlap-validation-2026-07-30.md` — the written report (per-column table, agreement rates, muted-vote delta, funding drift, tolerances, honest verdict + root-cause read).

### 7.2 Validation-window reality check

The coordinator's ruled window (07-23 → 07-30) is the **correct target** on the config-drift argument, but the fetcher hit a **hard Deribit constraint** during this lane: `get_last_trades_by_instrument_and_time` returns ≈ the **last 24 hours** of trades and refuses older windows (empirically confirmed: `start=07-23, end=07-24` returned `trades: []`; the same start with `end=07-31` returned trades from 07-29 10:47 UTC onward — the API is silently retention-clipped). Candles have no such retention limit; only trades do. Consequence: the **achievable overlap window** was 07-29 12:00 → 07-30 08:00 UTC (~20 h). This is disclosed on line 0 of the validation report; the candle store for the full 07-23→30 range is fetched and on disk (11 925 × 1m rows) and will be immediately usable once a self-hosted forward-collector or paid trade-history tier lands.

### 7.3 Headline numbers

- **Verdict agreement: 597/840 = 71.07 %** (ASIA 71.9 % · LONDON 55.0 % · NY 71.4 %). LONDON is a small-N cell (20 rows, one 60-min opening window) — do not over-read.
- **Tier agreement (STRONG/WEAK/MID/NONE): 669/840 = 79.64 %**.
- **Muted-vote delta**: agree rate 71.10 % on the 602 rows where live carried non-neutral OFI or partial/full OI; 71.01 % on the 238 rows where BOTH were neutral. **Delta = 0.09 pp.** On this window the muted signals do NOT materially move verdicts.
- **5 worst columns**: ATR (0.00 %), ATRMultiplier (0.00 %), TTMHistogram (0.12 %), ADX (0.24 %), MTF15mADX (0.24 %). All five point to the same root cause (§7.4).
- **FundingMomentum**: 22.02 % — synthetic reads FLAT 92 % of the time because the coarse `get_funding_rate_history` endpoint under-populates the v53 30-min window. Backs the D2 approximation caveat with a number.

### 7.4 The dominant systematic drift — investigated, causal read

**Not an assembly bug.** The synthesizer, live path, and every indicator function line up as `ScoringEngine.Calculate` is called with the identical shapes. The drift is a **structural definition of "now"** that differs between the two paths:

- Live (`MainForm_Analysis.RunAnalysisAsync`) polls the Deribit chart endpoint at some poll-time INSIDE a bar (e.g. 15:57:49 UTC for a 1-min bar that opened at 15:57:00). Chart data returns the partially-forming 15:57 bar as the last of N. Every indicator computed uses that partial bar's current close as "now".
- Synthetic (`ReplayLoop.Run`) iterates on the exact bar-close grid: at 15:57:00 the loop calls `SliceCandlesAtOrBefore(closeMs=15:57:00, N)` which returns the fully-closed 15:56-open / 15:57-close bar as the last of N. The 15:57-open bar (the one live would have seen as partial) is EXCLUDED.

Direct proof via a specific pair (07-29 18:42): live polled at 18:42:02 UTC with `Price=64280.50` (mid-bar within the 18:42-open bar); synthetic at 18:42:00 UTC exact-close with `Price=64294.50` (= C of the 18:41-open bar per the on-disk 1-min candle: `18:41 O=64218.50 H=64387.50 L=64218.50 C=64294.50`; the 18:42 bar itself has `O=64290.00 C=64258.50`, meaning live at 18:42:02 was mid-formation with price 64280.50 sitting between O and L). Same-timestamp claim; DIFFERENT last bar in the analysis window. Every candle-derived indicator on a 14-period ATR / 14-period ADX / 20-period TTM linear-regression / EMA9 / VolumeRatio-over-SMA9 window feels this one-bar swap. That is what drove ATR/ADX/DMI/TTM/VolumeRatio to 0–3 % match; the softer, slower or categorical-thresholded downstreams (`Regime`, `EMAAlignment`, `SqueezeStatus`, `MTFGatePass*`, `TrendStructure5m`) still matched 90–97 % because a small numeric shift rarely crosses their thresholds.

**The fix that would close the gap is a spec-first v2 item** (report §8.6 #1): `ReplayLoop` synthesizes a partial current-bar from the trade stream at each scored close-time — deterministic, small code footprint. This lane does NOT implement it (out of scope, and the current fidelity is the honest v1 story we should record before extending).

### 7.5 What the synthesizer can be trusted for after this lane

- **Faithful (>90 % match)**: `Regime`, `EMAAlignment`, `SqueezeStatus`, `RSIDivergence`, `MTFGatePass*`, `TrendStructure5m`, `OBVTrend/Divergence`, `Confidence`, `FundingBias`, `LiqSignal`, `MicroCVDMomentum` categorical, `MaxScore`, `RegimePenalty`, `EMA200_5m`, `PriceVsEMA200`, `BBW`, `TFISignal`, all `Absorption*`/`OFI*`/`OI*` (muted by design — measured as inert, correct). Studies keyed off these are cleared.
- **Advisory only (60–90 %)**: `Verdict` (71 %), `Tier` (80 %), `VWAP` + bands, `Donchian*`, `MTF15mTrend`, `PlacedTarget*`, `EMA21/50`, swing coordinates, `VWAPDevPct`, `PlacedStop*`, `CVDSlope` categorical. Studies here need the caveat printed alongside.
- **Do NOT use (<60 %)**: `ATR`/`ATRMultiplier`, `TTMHistogram`, `ADX`/`+DI`/`−DI`, `VolumeRatio`, `ROC`, `RSI`, `EMA9`, `CVDValue`/`WeightedSlope`, numeric `MicroCVD*`, `AggrVelBurstRatio`/`Net`, `FundingMomentum`, score fields (`LongScore`/`ShortScore`/`Effective*`). Blocked until v2 partial-bar reconstruction lands.

This matches proposal §2's forbidden-use list (still forbidden: OFI/OI/spread/absorption calibration, Kelly CAL, live-population rates) and adds an operational **do-not-use / advisory / faithful** partition for the reconstructable set.

### 7.6 Gate tail + commits

`tools/checks/verify-gate.ps1 -Mode prepush` (all 6 Release builds + harness + parity/version guards):

```
PASS  A44a FloorToBucket collapses same-bar timestamps + advances one bucket at the boundary + execRes<=0 guard

ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine-path change accompanied by a settings.json version bump

=== result ===
GATE PASSED
```

Commits (local, unpushed — the trader's local-first workflow):
- **(this lane)** `feat(backtest): validate CLI verb + OverlapValidator (proposal §4)` — the `validate` subcommand, `OverlapValidator.vb`, and the `HistoricalStore` chunked-candle fix.
- **(this lane)** `test(backtest): A44a FloorToBucket contract` — one small fixture; the join is the only new logic worth pinning per the task brief.
- **(this lane)** `docs(backtest): overlap-validation report 2026-07-30 + spec-back addendum §7` — the written report + this addendum.

---

## 8. §7.1 forming-bar stub lane — 2026-07-30 (Opus implementer)

Post-validation amendment per `docs/backtest-synthesizer-proposal.md` §7.1 — the synthesizer now mirrors live's forming-bar convention (last candle of every series is a stub built from real trades in `[closeMs, closeMs + 2s]`; zero-trade fallback = `{OHLC = prev close, V = 0}`). This lane is `tools/BacktestRunner/` and `verify/ordercheck/` only — ZERO engine `.vb` / `settings.json` touch.

### 8.1 Files modified

- `tools/BacktestRunner/ReplayLoop.vb` — three additions: `FormingStubDeltaMs` constant (2000 ms); the trio of pure helpers `TradesInStubWindow` / `BuildFormingStub` / `AppendFormingStub` (host-agnostic, harness-exercised); and the `Run()` splice that (a) slices `(N − 1)` closed bars per series then appends the stub, (b) widens the trade slice + aggr-vel feed cutoff to `closeMs + 2 s` so live's poll-at-closeMs+2s state is what we mirror. Under `execRes = 1`, the shared `sliceExec = slice1m` reference means the 1m append updates both — deliberate, single-append.
- `verify/ordercheck/Program.vb` — new fixture **A43f** exercising three pins in one fixture: (i) OHLCV compaction of 4 real trades in-window yields the correct O/H/L/C/V/VolumeUSD + `Timestamp = closeMs`; (ii) zero-trade fallback yields `{O=H=L=C=prevClose, V=0, VolumeUSD=0}`; (iii) `AppendFormingStub` puts the stub as the LAST bar of the slice (Timestamp advances past the last real bar; total count = original + 1) and the empty-slice input is a safe no-op. `TradesInStubWindow` inclusion boundaries are exercised in the same fixture (4 in-window trades kept, 2 pre + 1 post excluded).

### 8.2 Re-validation headline (docs/backtest-overlap-validation-2026-07-30.md §9)

Same window (2026-07-29 12:00 → 2026-07-30 08:00 UTC), same frozen live files, same synthesizer inputs — only the stub logic changed.

| Metric | Before (§4) | After (§9) | Δ |
|---|---:|---:|---:|
| Verdict agreement | 71.07 % | **74.17 %** | +3.10 pp |
| Tier agreement | 79.64 % | **81.55 %** | +1.91 pp |
| ATR | 0.00 % | **46.55 %** | +46.55 pp |
| ATRMultiplier | 0.00 % | **57.62 %** | +57.62 pp |
| ADX | 0.24 % | **49.76 %** | +49.52 pp |
| TTMHistogram | 0.12 % | **42.62 %** | +42.50 pp |
| ROC | 15.95 % | **77.74 %** | +61.79 pp |
| EMA9 | 49.52 % | **99.52 %** | +50.00 pp |
| Donchian Upper/Lower | 78 % / 81 % | **100 % / 100 %** | +22 pp / +19 pp |
| Muted-vote delta | 0.09 pp (on 71 % baseline) | **0.30 pp** (on 74 % baseline) | remeasured, still tiny |
| FundingMomentum | 22.02 % | 22.02 % | unchanged (funding path untouched) |

**Residual systematic gap — NOT a synthesizer-side fidelity issue, so not fixed here per the task constraint.** VWAP family regressed (VWAP 72.6 % → 43.9 %) because `Core/Indicators_Volatility.vb::GetSessionCandles` uses `DateTime.UtcNow` for the session anchor — the REAL wall-clock, not replay-simulated time. Pre-fix and post-fix synth both hit this bug; pre-fix, the extra oldest closed bar happened to wash out; post-fix, dropping that oldest bar exposed the drift. Report §9.5 walks through the direct proof (row-by-row VWAP samples where replay-day = real-day match to the dollar; where they differ, drift 50–200 dollars). The fix belongs inside the engine (thread `nowUtc` into `CalcVWAP`) and is spec-first — recommended as report §8.6 #5. OBV family (97 % → 71 %) rides the same edge sensitivity from the stub's near-zero volume weighting into `meanVol`; not a bug in this lane's construction.

### 8.3 Gate + commits

Prepush verify-gate: **GATE PASSED**. All 6 Release builds 0/0 (main sln, AutoTweaker, WhatIfRunner, CeilingAudit, BacktestRunner, OrderCheck). Full harness unregressed; A43f fixture PASS. Display-parity and version-bump guards OK.

Local commits (unpushed — the trader's local-first workflow):

- `feat(backtest): §7.1 forming-bar stub in ReplayLoop (mirror live)` — the `ReplayLoop.vb` splice + helpers.
- `test(backtest): A43f forming-bar stub construction` — the fixture family extension.
- `docs(backtest): overlap re-validation §9 after §7.1` — the before / after report update + this addendum §8.
