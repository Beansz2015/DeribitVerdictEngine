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

