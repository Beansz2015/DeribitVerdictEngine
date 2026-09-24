# Adversarial audit — the live order path (2026-09-24)

**Scope:** every line of code that sits between Deribit and `verdict_signal.json`, the file the order app trades from. **Base commit:** `6e74181` (2026-09-22). **Settings:** tracked `settings.json` v68 (`signal_bridge.enabled: true`, `transport: ws`, `trigger_mode: on_close`). **Method:** line-level read, then every finding that can run was turned into a proof against the shipped sources (`verify/auditproofs/`, 22 cases, all confirmed). Findings that need WinForms or a live socket are marked **code-read** and cite exact lines.

**Status: REPORT ONLY.** No engine source was changed. Every fix below is a recommendation; the scoring-affecting and payload-affecting ones fall in the CLAUDE.md reserved classes and need trader rulings.

---

## 0. Pre-flight gate decision

The whole tree is **65,043 lines in 128 `.vb` files**. That does not fit one line-level adversarial pass, so this audit does **not** claim the whole tree. It claims the live order path in full, and hands the rest to follow-up sessions (§7).

| Audited line by line (8,905 lines by `wc -l`) | Read in part (≈ 1,300 lines) | Not audited → §7 |
|---|---|---|
| `Core/SignalEmitter.vb`, `UI/MainForm_SignalBridge.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_AutoRun.vb`, `Core/ScoringEngine_*.vb` (all 5), `Core/IndicatorResults.vb`, `Core/Indicators_*.vb` (all 4), `Core/ExecutionResolution.vb`, `Core/ProcessIdentity.vb`, `Core/BarCloseDetector.vb`, `Core/OfiAccumulator.vb`, `Core/AggressorVelocityAccumulator.vb`, `Core/Settings/SettingsLoader.vb`, `DeribitClient.vb`, `DeribitWsFeed.vb`, `MarketState.vb`, `WsMarketDataSource.vb`, `RestMarketDataSource.vb`, `IMarketDataSource.vb`, `MtfRefreshPolicy.vb`, `OiSnapshot.vb`, `DynamicNorms.vb` | `LivePerformanceTracker.vb` (init + update path), `UI/MainForm_PlaintextSnapshot.vb` (header + Kelly), `UI/MainForm_Layout.vb` (startup wiring), `tools/AutoTweaker/SettingsDiffApplier.vb` (Validate/Apply/ApplyRevert), `tools/AutoTweaker/AutoTweakerCore.vb` (apply branch), `Core/Settings/EngineSettings.vb` (trade costs, structural levels), `AnalysisLogger.vb` (exception handling) | UI cards/layout/controls, exit guard, live strip, alerts, absorption tracker, tape store + repair, eval/perf, offline analysis, backtest/what-if/ceiling tools, the 16,764-line fixture harness, and the consumer repo |

---

## 1. Three premises corrected before the findings

1. **"Fees are not implemented" is half true.** v62 models Deribit fees (`scoring.trade_costs`: maker 1.5 bps, taker 3.5 bps) — but **only** in the target-side minimum-move floor (maker/maker round trip + 5 bps net = 8 bps). Nothing prices the loss path (maker in + taker stop = 5 bps), Kelly, or reward-to-risk. The gap is real and it lands in AUD-01 and AUD-15.
2. **Double vs Decimal is not a defect here.** Every BTC price on the 0.5 USD tick grid is exact in binary64 below 2^52, trade USD sums are integer-exact below 2^53, and inverse-contract risk per contract (`face × Δ / entry`) carries ~1e-16 relative error. The real precision hazards are elsewhere: `Math.Ceiling` on a float product at an integer boundary (AUD-21) and prices never rounded to the tick grid (AUD-10). Swapping in `Decimal` would fix neither.
3. **Engine sizing never reaches the exchange in bridge v1.** Kelly is advisory (`signal-bridge-v1-proposal.md` §8 D5: "never sizing in v1"). It still rides in the payload, so its defects are payload-integrity defects (AUD-15), not position-sizing breaches.

---

## 2. `--- LOGIC TRACE ---`

A synthetic but realistic NY 1-min tape: 240 calm bars around 60,000 (ATR(7) 29.9 = 5.0 bps), then a 3-bar liquidation flush of −1.5 % (−900 USD), then the `on_close` run firing on the new bar. All numbers below are printed by `verify/auditproofs` case `TRACE` against the shipped code.

**2.1 Loop mechanics and state updates**

1. **Feed thread (continuous).** `DeribitWsFeed.RouteSubscription` applies each frame under `MarketState`'s single lock: `ApplyChartTick` replaces the forming bar in place and appends on a roll (`MarketState.vb:90-113`); every trade is appended to the 5,000-trade ring and folded into the aggressor-velocity, absorption and alerts accumulators (`DeribitWsFeed.vb:464-533`); every book frame replaces `_book` and folds the OFI EMA (`:536-581`). The fold is stamped with receive time and weights each arriving frame by the gap before it, so any lull or delivery burst during the flush skews the average (AUD-12).
2. **Trigger (1 Hz timer).** `OnCloseWatcherTick` sees the 1m forming-bar open time advance and marshals `RunAutoAnalysis` (`MainForm_AutoRun.vb:261-314`). The run starts ≈1-2 s into the new bar. If a previous run is still in flight, `btnAnalyze.Enabled` is `False` and the fire is dropped (`:149`) — no payload, no SKIPPED.
3. **Fetch.** On WS every getter is an in-memory copy; book/ticker are gated at 10 s, **trades are not age-gated at all** (AUD-02), candles are gated only by `IsFresh` (last bar ≤ 2 bars old), the 15m series by nothing (AUD-07).
4. **Run-scoped state mutated per run** (`MainForm_Analysis.vb`): `_prevRegime` (1-run hysteresis, AUD-18), `_fundingHistory` (30-min age ring), `_oiHistory` (70-min ring, oldest-in-window anchor), `_ofiHistory` (10-sample ring), `_mtfCandles15m` (kept on failure, no max age).

**2.2 Indicators, levels and sizing under the flush**

| Quantity | Value | Where it comes from |
|---|---|---|
| Price after flush | 59,003.5 | `r.CurrentPrice = candlesExec.Last().Close` — the 2-second-old stub bar |
| ATR(7) on closed bars | 140.0 | `CalcATR` |
| ATR(7) the run actually uses | **120.2 (14.1 % low)** | the stub's ~0 true range enters Wilder's last step (known K6) |
| 5m swings | high 59,972 / low 59,907.5, **both above price** | `CalcSwingPivots`; `SwingStopLong` = 0 and `SwingTargetShort` = 0 |
| VPFR | `IN_LVN_BEAR`, POC 59,921, HVN above 59,079 | `CalcVPFRLite` |
| LONG levels | stop 58,811.13 `FALLBACK_ATR` / target 59,079.15 `NEAREST_HVN_ABOVE` | risk 192.4, reward **75.7 → R:R 0.39**, passes the 47.2 USD floor |
| SHORT levels | stop 59,195.87 `STOP_CLAMPED` / target 58,793.09 `FALLBACK_ATR` | risk 192.4, reward 210.4 |
| Min-move floor | 47.2 USD (8 bps) | `TradeCosts.EffectiveMinMovePct × price` — target side only |
| Stops on closed-bar ATR | 224.0, not 192.4 | the stub shrinks every stop, target and bound by 14 % |
| Kelly (MEDIUM, as the snapshot calls it) | f* 0.139 → 5 % cap → **500 contracts, $5,000 notional, leverage-capped** | fixed b = 1.75/1.6, no fee term, no link to the placed stop |

**Inverse-contract arithmetic.** Per-contract USD loss at the stop is `10 × Δ / entry` — exact when valued at the exit price. What the engine omits is the collateral: on a BTC-margined account a long's USD loss is `(A + N)·Δ/P`, a short's is `(N − A)·Δ/P` (A = account USD, N = notional USD). At the 5× leverage cap a long carries **1.2×** the risk Kelly reports and a short **0.8×** (AUD-15).

**2.3 Score to payload**

1. `ScoringEngine.Calculate` → Step 2 votes → Pass 2/2b/2c → Steps 3/3b → Step 4 regime veto → Step 4b MTF veto (**fails open** without 15m data, AUD-07) → Step 5 tier → Step 5b `ComputeSideLevels` → **Step 5c min-move gate on the target only** (AUD-01).
2. `LogRun` writes the CSV row, `BuildPlaintextSnapshot` runs Kelly, eleven card binds run, then `Await LivePerformanceTracker.UpdateAsync` — which first awaits the startup backfill task (AUD-04, AUD-06) — then the output dump, then `EmitBridgeSignal` (`MainForm_Analysis.vb:643-733`).
3. `BuildOk` stamps `generated_at_utc = DateTime.UtcNow` **at emission** (`MainForm_SignalBridge.vb:74`). No field records when the candles, trades or book were observed (AUD-06). `TryWrite`'s return value is discarded (AUD-11).
4. The consumer acts on `(instance_id, signal_id)` once, if `now − generated_at_utc ≤ 2.5 × exec_resolution_min` minutes (7.5 min in ASIA/LONDON), with entry slippage capped at 0.6 × ATR against `levels.entry`. In the flush that is 72 USD of tolerated slippage against a long stop that is itself tick-unaligned (AUD-10).

---

## 3. Findings

Severity: **S0** can put a wrong-side, unprotected or unbounded order on the exchange under shipped config · **S1** systematic negative-EV or stale-data orders, or a silent total outage from a supported state · **S2** realistic trigger, bounded impact, or a contract gap that moves risk to the consumer · **S3** edge case, latent path, or advisory inconsistency · **S4** nit.

Evidence: **H-n** = a case in `verify/auditproofs` the reader can run · **code-read** = WinForms or live-socket path, lines cited.

No S0 was found. Several S1s become S0 depending on consumer behaviour this audit could not see (§6).

### AUD-01 · S1 · The minimum-move gate prices the target and nothing else

- **SEVERITY:** S1
- **LOCATION:** `Core/ScoringEngine_Calculate_Verdict.vb:311-321` (Step 5c); `Core/SignalEmitter.vb:386-407` (ladder places the first structural tier within 3.5 × ATR), `:476-489` (stop = min(swing, 1.6 × ATR)).
- **DOWNSTREAM IMPACT:** STRONG/MEDIUM verdicts with `verdict_context: CONFIRMED` reach the order app with reward-to-risk far below 1. The gate's only test is `|placed target − entry| ≥ 8 bps`; stop distance, R:R, loss-path fees and net EV are never evaluated before emission.
- **FAILURE SCENARIO:** Volatility expands (ATR 30 → 200 at 60,000). A 5m swing high sits 49 USD above entry — just over the 48 USD floor — and the ladder places it because it is inside 3.5 × ATR. The structural stop is wider than 1.6 × ATR, so it clamps to 1.6 × ATR. **H-P03**, all STRONG LONG / CONFIRMED:

  | ATR | stop | target | R:R | gross breakeven | net breakeven (maker in/TP, taker SL) |
  |---|---|---|---|---|---|
  | 30 | 59,952 | 60,049 | 1.02 | 49.5 % | 71.6 % |
  | 60 | 59,904 | 60,049 | 0.51 | 66.2 % | 80.3 % |
  | 100 | 59,840 | 60,049 | 0.31 | 76.6 % | 86.0 % |
  | 200 | 59,680 | 60,049 | **0.15** | 86.7 % | **91.9 %** |

  The engine's own measured STRONG success rate is 47.1 % (`DeribitIndicatorProject.md` §12). Every row above is a certain loser at that rate, and the pathology grows exactly when volatility rises, because the stop scales with ATR and the minimum target does not. The TRACE shows the same shape on a real flush (long R:R 0.39).
- **ANALYTICAL CRITIQUE:** Step 5c must gate on the geometry that gets traded, not on one leg of it. Compute net EV per signal from the placed levels and the fee model — `target − fee(win path)` against `stop + fee(loss path)` — and require a minimum net R:R or a net EV at an explicit assumed hit rate. At minimum, add a stop-side check (placed target distance ≥ k × placed stop distance). The ladder's "structure wins even when farther" rule needs the mirror rule: structure must not place a target *closer* than a fraction of the stop. This is a scoring change and a dataset boundary; it belongs in a spec.

### AUD-02 · S1 · Staleness is gated per channel inconsistently, and trades are not gated at all on a live socket

- **SEVERITY:** S1
- **LOCATION:** `WsMarketDataSource.vb:91-106` (trades gated on health only); `UI/MainForm_Layout.vb:537-538` (health = `IsConnected AndAlso Not IsCoolingDown`); `DeribitWsFeed.vb:607-616` (`IsDegraded` requires book **and** trades **and** ticker stale); `MainForm_Analysis.vb:141-149` (thin-trade gate counts, never ages).
- **DOWNSTREAM IMPACT:** On a connected socket whose trades channel has stopped delivering, TFI, CVD, MicroCVD, Pass 2b (OI × CVD) and Pass 2c's CVD leg are scored every run from a frozen ring. The votes are directional and identical run after run; each run is a new `signal_id`, so the consumer sees a stream of fresh, `ws: OK`, directionally biased signals.
- **FAILURE SCENARIO:** A reconnect re-subscribes; Deribit silently drops the trades subscription (the known unverified-subscribe defect, K4 — its queue row describes only the *book* case, which skips; the *trades* case does not). The ring holds the 500 REST seed trades plus whatever streamed before. **H-P08:** a ring whose newest trade is 119 minutes old is served in full (500 trades) and scores **TFI 0.60 BUY PRESSURE, CVD 300,000 RISING**, while a book of the same age is correctly refused. Book and ticker keep flowing, so `IsDegraded()` stays `False` and no REST fallback happens. Nothing ends this until the next reconnect.
- **ANALYTICAL CRITIQUE:** "Trades legitimately go quiet" (P3 §3) is true for minutes on BTC-PERPETUAL only in extreme lulls; a stalled stream and a quiet market must be told apart, not conflated. Cross-check the ring against a live reference: the ticker's `last_price`/`timestamp` or the 1m chart bar's volume advancing while the ring does not is a stall. Gate each stream on its own age with a stream-appropriate bound (trades: e.g. 60-120 s against a moving ticker), and make `IsDegraded` fire on **any** primary stream stale, not all. Surface a per-stream age in the payload (see AUD-06).

### AUD-03 · S1 · No invariant check exists anywhere between `settings.json` and the order payload

- **SEVERITY:** S1
- **LOCATION:** `Core/Settings/SettingsLoader.vb:372-500` (load/hot-reload publishes any value that deserialises); `tools/AutoTweaker/SettingsDiffApplier.vb:88-278` (`Validate` fences key paths, never values); `Core/SignalEmitter.vb:118-182`, `:309-508` (emits whatever geometry it computes).
- **DOWNSTREAM IMPACT:** One bad number in a hot-reloaded file reaches a live payload within ~200 ms with `confidence: HIGH`. The emitter will publish a long whose stop is above its target, a stop at entry, or a veto that blocks only one side.
- **FAILURE SCENARIO:** **H-P05:** `SettingsDiffApplier.Validate` returns `IsValid=True` for `scoring.atr_stop_multiplier 1.6 → -1.6`, `scoring.structural_levels.stop_max_atr_mult 1.6 → 0`, `mtf_gate.min_of 2 → 0` and `indicators.ATR.period 7 → 0`. The edited file then loads through the real `SettingsLoader` with `LastLoadError = ''`, and the next run emits **STRONG LONG, entry 60,000, stop 60,096, target 60,090** — a stop above the target. `stop_max_atr_mult 0` places the stop **at entry** (`STOP_CLAMPED`). `min_of 0` makes a 15m crash read `BULL`, so every short is vetoed and every long passes — the "never disable" rule on `mtf_gate.enabled` is bypassed by a sibling key. `period 0` throws `InvalidOperationException` inside the run (→ AUD-09's modal halt). Today the tweaker cannot write at all (AUD-16), so the live exposure is a hand edit — which is exactly how `settings.json` changes are made.
- **ANALYTICAL CRITIQUE:** Two independent layers are missing. (1) A declarative range/sign table per key, enforced in `SettingsLoader` on every load — reject the whole file and keep last-good on any violation, and surface it as a skip reason rather than a console line. The same table should back `SettingsDiffApplier.Validate`. (2) An emission-boundary invariant in `SignalEmitter.BuildOk`: for the direction being signalled, `stop < entry < target` (mirror for short), both finite, stop distance ≥ floor, target distance ≥ floor, and `stop/target` within sane ATR bounds; on violation emit `SKIPPED` with a reason. The emitter is the last point the engine controls; it should refuse to publish geometry that cannot be a valid bracket.

### AUD-04 · S1 · `performance_display.enabled: false` at startup deadlocks every run before emission

- **SEVERITY:** S1
- **LOCATION:** `UI/MainForm_Layout.vb:479` (`InitialiseAsync` launched only when enabled); `LivePerformanceTracker.vb:154`, `:228-231`, `:500-502` (the only code that completes `_initTcs`), `:524-527` (`UpdateAsync` awaits `_initTcs` **before** its own `Enabled` check); `UI/MainForm_Analysis.vb:678` (called unconditionally, before `EmitBridgeSignal` at `:733`).
- **DOWNSTREAM IMPACT:** The first run writes one CSV row, then hangs forever at `Await _initTcs.Task`. No bridge payload is ever written, `ws_health.log` stops (it is written from the emit paths), `btnAnalyze` stays disabled so every later bar-close fire returns at `MainForm_AutoRun.vb:149`. The order app sees silence and stands down after its max-age; the collector stops collecting.
- **FAILURE SCENARIO:** `performance_display` is one of the six blocks the per-box `settings.local.json` overlay admits (`SettingsLoader.vb:76-83`). A box that turns it off — the obvious move on the 1 GiB collector while `_evalCache` is unbounded — and restarts goes dark after one row. **H-P14:** `UpdateAsync` without a prior `InitialiseAsync` has not completed after 3 s (it never will). The v68 "two rows" deploy gate catches a scripted deploy; an unscripted restart has no such gate.
- **ANALYTICAL CRITIQUE:** Check `Enabled` before awaiting, or complete `_initTcs` unconditionally at startup when the feature is off. More fundamentally, the bridge emission must not be sequenced behind a display-only subsystem (AUD-06): emit immediately after `Calculate` + `LogRun`, and let rendering and the performance strip follow.

### AUD-05 · S1 · Stop geometry is incompatible with the consumer's own tolerances

- **SEVERITY:** S1 (S0 if the consumer places the stop after a fill that already crossed it and does not handle the rejection)
- **LOCATION:** `Core/SignalEmitter.vb:454-489` (SWING_STOP accepted down to `stop_min_floor_ticks × 0.5` = 2 USD); `UI/MainForm_Analysis.vb:581-584` (structural stop = the pivot price itself); `signal-bridge-v1-proposal.md` §3 (consumer slippage cap 0.6 × ATR against `levels.entry`).
- **DOWNSTREAM IMPACT:** The engine emits a directional signal whose stop is closer to the reference entry than the entry slippage the consumer itself accepts. A fill anywhere in the tolerated band can be on the wrong side of the stop at the moment the stop order is placed. Deribit rejects a sell-stop above the market (or triggers it at once), which leaves either an unprotected position or an immediate taker loss plus fees.
- **FAILURE SCENARIO:** **H-P16:** ATR 40, swing low 2 USD under entry → **STRONG LONG with a `SWING_STOP` 2 USD (0.33 bps) away**, while the consumer tolerates 24 USD of entry slippage. Separately, every `SWING_STOP` sits exactly on the pivot low, the most-tested price on the chart: a single print at the prior low (a routine double-bottom retest) triggers it, which is the trader's own rule broken — "below the previous swing low" (`trader-profile.md` §5). `stop_buffer_pct` cannot fix this: it scales the distance by a percentage, so on a 4-tick stop even +10 % is less than a tick.
- **ANALYTICAL CRITIQUE:** Floor the stop distance in units that match the execution risk, not in ticks: `max(k₁ × ATR, consumer slippage cap + spread + tick)`. Offset structural stops beyond the pivot by an absolute amount (ticks or a fraction of ATR), not a percentage of distance. Put the minimum stop distance and the slippage cap in the same contract document so neither side can move one without the other.

### AUD-06 · S2 · The payload cannot express data age, and emission is sequenced behind display work

- **SEVERITY:** S2
- **LOCATION:** `Core/SignalEmitter.vb:205-227` (only clock is `generated_at_utc`); `UI/MainForm_SignalBridge.vb:74` (stamped `DateTime.UtcNow` at emission); `UI/MainForm_Analysis.vb:643-733` (LogRun → snapshot → 11 binds → `Await UpdateAsync` → dump → emit).
- **DOWNSTREAM IMPACT:** The consumer's only staleness defence (max-age on `generated_at_utc`, 2.5 × exec resolution = up to 7.5 min) measures how long ago the engine wrote the file, not how old the market data was. Any delay between fetch and emit is invisible and lands inside `levels.entry`.
- **FAILURE SCENARIO:** **H-P10:** the payload's keys contain exactly one time field, `generated_at_utc`. Code-read: on the first run after a restart `UpdateAsync` awaits the startup backfill — the trailing-gap (or, with no recent cache, full 7-day) OHLC fetch plus up to `max_gap_fill_calls` (10) chunked REST calls, each up to 31 s under venue errors (15 s timeout, retry once, 1 s backoff). A run that fetched its candles at T emits at T + minutes with a fresh stamp. On the REST fallback path in 3m sessions the same happens at up to ~62 s per run (parallel fetch, then a sequential exec-resolution fetch). On a stalled 3m chart stream `IsFresh` tolerates a bar opened 6 minutes ago, so data age plus the 7.5-minute max-age can exceed 13 minutes on a 2-15 minute strategy.
- **ANALYTICAL CRITIQUE:** Add `data_as_of_utc` (oldest of the last candle update, last trade, book and ticker times used by the run) and per-stream ages to the payload, and make the consumer's gate key on data age. Emit immediately after `Calculate`; nothing display-only should sit on the path. This is a schema change (v2) and a coordinated cross-repo pass.

### AUD-07 · S2 · The 15m "hard veto" fails open, keeps stale data forever, and is directionally biased on short series

- **SEVERITY:** S2
- **LOCATION:** `Core/Indicators_Structure.vb:452-459` (no-data ⇒ both pass flags `True`), `:475-480` with `Core/Indicators_Momentum.vb:116` (`CalcEMA` returns 0 below its period); `UI/MainForm_Analysis.vb:92-108` (cache kept on failure, no age), `:127-159` (skip gate never checks 15m).
- **DOWNSTREAM IMPACT:** The veto the trader profile calls a hard gate silently disappears on missing data, silently runs on hours-old data, and on a 21-49-bar series can block shorts but never longs. `mtf_blocked: false` goes out either way.
- **FAILURE SCENARIO:** **H-P01:** with no 15m candles, or 10 bars of a −3,000 USD crash, the gate returns `passLong True, passShort True`. **H-P02:** with 30-31 bars, the uptrend reads `BULL` (shorts blocked) and its mirror-image downtrend reads `FLAT` (longs pass), because `emaBear` needs `ema21 < ema50` and `ema50` is 0; at 60 bars the pair is symmetric (`BULL`/`BEAR`). Triggers: a failed 15m REST seed on (re)connect leaves the WS series growing from one bar (≈12 hours to reach 50); on REST the first fetch failing at startup; on either transport a fetch failure keeps a cache of any age.
- **ANALYTICAL CRITIQUE:** A veto must fail closed: no data or insufficient data ⇒ block directional verdicts (or emit SKIPPED), with the reason in `mtf_blocked`/`skip_reason`. Give the 15m cache a maximum age and include 15m in the freshness gate. `CalcEMA` returning 0 as "no value" is the root: return `Double?` (or NaN) and make every comparison handle absence explicitly.

### AUD-08 · S2 · A failed REST re-seed after reconnect leaves a hole the stream appends across

- **SEVERITY:** S2
- **LOCATION:** `DeribitWsFeed.vb:321-330` (a `Nothing` seed is skipped, the old series and trade ring are kept); `MarketState.vb:90-113` (`ApplyChartTick` appends any newer bar); `Core/Indicators_Momentum.vb:28-35` (`IsFresh` inspects only the last bar).
- **DOWNSTREAM IMPACT:** After an outage where REST is still failing when the socket reconnects — the usual shape of a venue incident — every indicator is computed across a gap as if the bars were contiguous. ATR inflates, ROC and RSI compare prices an outage apart, and CVD mixes pre- and post-gap flow.
- **FAILURE SCENARIO:** **H-P09:** a 3m series ending 30 minutes ago plus the first post-reconnect bar: last-two-bar gap **30 min**, `IsFresh = True`, **ATR(7) 40.0 → 94.7**, so the 1.6 × ATR stop goes from 64 to 151 USD; ROC reads 0.685 % across the hole. The v67 thin-trade gate does not help: the old ring is full.
- **ANALYTICAL CRITIQUE:** On reconnect, clear the series and ring before seeding (so a failed seed produces the existing "unavailable" skip rather than stale data), retry the seed, and add a continuity check (`Timestamp[i] − Timestamp[i−1] = resolution` over the indicator lookback) to the freshness gate.

### AUD-09 · S2 · Any exception in a run raises a modal MessageBox that halts the unattended collector

- **SEVERITY:** S2
- **LOCATION:** `UI/MainForm_Analysis.vb:29-50` (`MessageBox.Show` in `btnAnalyze_Click`'s `Catch`; `btnAnalyze.Enabled` restored only in `Finally` after the dialog closes); `UI/MainForm_AutoRun.vb:148-160` (every auto-run fire goes through `btnAnalyze_Click` and returns early while the button is disabled). Code-read.
- **DOWNSTREAM IMPACT:** Signal generation stops until a human clicks OK on a box nobody watches. No SKIPPED payload, no health line, nothing in any log file (the message goes to a dialog, not to disk).
- **FAILURE SCENARIO:** A hand-edited `period: 0` (H-P05 shows the `InvalidOperationException`), a Kelly `CInt` overflow (H-P04c), or any fault in the 3,684-line card-binding code throws out of `RunAnalysisAsync`. The dialog opens; all later bar-close fires hit `If Not btnAnalyze.Enabled Then Return`. The v68 fix that removed this dialog from unattended start (`StartAutoRun(silentOnInvalid:=True)`) did not cover the run path.
- **ANALYTICAL CRITIQUE:** The auto-run path must never block on UI. Catch in `RunAnalysisAsync`, emit `SKIPPED` with `skip_reason: "run exception: <type>"`, write the exception durably (the `WsFeedLog` pattern), and restore `btnAnalyze` in all cases. Keep the dialog only for manual clicks.

### AUD-10 · S2 · Emitted prices are not on the tick grid and the contract names no rounding owner

- **SEVERITY:** S2
- **LOCATION:** `Core/SignalEmitter.vb:415`, `:432-437`, `:472`, `:483`, `:487`, `:497` (levels are raw ATR arithmetic; `TickSize` is used only for floors); `signal-bridge-v1-proposal.md` §3 (no rounding rule).
- **DOWNSTREAM IMPACT:** Deribit validates order prices against the instrument's tick size. If the consumer passes levels through, the target or — worse — the protective stop is rejected after the entry fills. If it rounds, the rounding direction decides whether the 4-tick floor survives.
- **FAILURE SCENARIO:** **H-P15:** ATR 37.3 emits `59940.32`, `60065.275`, `60059.68`, `59934.725` — all four off the 0.5 grid.
- **ANALYTICAL CRITIQUE:** Round in the engine, once, with a stated direction: stops away from entry, targets toward entry. Re-check the stop floor and the min-move gate after rounding. Write both into the frozen contract.

### AUD-11 · S2 · A failed write leaves the previous signal live, with no retry and no stand-down

- **SEVERITY:** S2
- **LOCATION:** `UI/MainForm_SignalBridge.vb:75-76`, `:98-99` (`TryWrite`'s `Boolean` is discarded); `Core/SignalEmitter.vb:584-597`.
- **DOWNSTREAM IMPACT:** When the rename fails — the consumer's reader holding the file without delete sharing, antivirus, a full disk — the new payload is lost and the old one stays on disk. If the lost payload was a `SKIPPED` stand-down, the consumer keeps treating the previous directional signal as current until max-age.
- **FAILURE SCENARIO:** Code-read. The consumer's contract says it reads with "retry-once-on-share-violation", so concurrent reads are expected. A read overlapping `File.Move(overwrite)` makes the move throw; the catch logs to the console and the run carries on.
- **ANALYTICAL CRITIQUE:** Retry the rename with a short backoff; on final failure record it durably and try to write a SKIPPED payload. Consider a monotonic sequence file or named pipe for v2 so a missed write is detectable by the consumer.

### AUD-12 · S3 · The time-averaged OFI gives each book update the weight of the gap before it

- **SEVERITY:** S3
- **LOCATION:** `Core/OfiAccumulator.vb:83-91`.
- **DOWNSTREAM IMPACT:** The OFI vote (one of ~20) is meant to be spoof-resistant. The EMA instead lets a transient that arrives after any lull — or after a TCP delivery burst, since the fold stamp is receive time — dominate the average for seconds.
- **FAILURE SCENARIO:** **H-P06:** 10 s of a balanced book, a 3 s lull, one 20:1 top-of-book pulled 100 ms later. Shipped ratio **2.205 → BUY DOMINANT**; the correct piecewise-constant EMA gives 1.030 → BALANCED. The shipped value stays BUY DOMINANT for **5.3 s** of perfectly balanced book.
- **ANALYTICAL CRITIQUE:** For a state signal the weight of interval dt belongs to the value in force during it: fold the **previous** sample with `alpha(dt)`, then store the new one. Stamp folds with exchange time if the book channel carries it (the feed's comment says it does not; worth re-checking against a raw frame).

### AUD-13 · S3 · The VWAP session fallback defeats its own warmup guard

- **SEVERITY:** S3
- **LOCATION:** `Core/Indicators_Volatility.vb:38` (no bar in session ⇒ whole list), `:53` (that full count is reported as the session count).
- **DOWNSTREAM IMPACT:** In the window between a session anchor (00:00, 13:30 UTC) and the first bar of the new session, VWAP votes and Pass 2c's VWAP leg run on a multi-hour VWAP labelled "session", with the warmup suppression switched off.
- **FAILURE SCENARIO:** **H-P07:** a run at 13:30:00.5 whose newest bar opened 13:29 reports `VWAPSessionCandles = 250`, warmup `False`, VWAP 59,498 against price 59,996 — an 8 bps "deviation" that is really a 4-hour drift. Reachable on backstop/interval fires and on REST data that has not yet produced the new bar.
- **ANALYTICAL CRITIQUE:** Return an empty session (count 0, VWAP unset) and let the existing warmup path suppress the votes.

### AUD-14 · S3 · Swing pivots are "confirmed" by the forming 5m bar and repaint inside the bar

- **SEVERITY:** S3
- **LOCATION:** `Core/Indicators_Structure.vb:287` (`scanEnd = Count − 1 − wing` puts the forming bar in the right wing), `:302-315`.
- **DOWNSTREAM IMPACT:** Structural stops and targets, the STRUCTURALLY_WEAK tag and Layer 1.5 of the hold logic all change between runs inside one 5m bar with no market structure changing — only the unfinished bar.
- **FAILURE SCENARIO:** **H-P13:** the same forming bar at 59,990 yields `LastSwingLow5m = 59,950`; 40 seconds later at 59,930 it yields 59,800. A long's stop moves 150 USD between two runs in the same bar.
- **ANALYTICAL CRITIQUE:** Scan only closed bars (`scanEnd = Count − 2 − wing`). This moves placed levels on every row — a dataset boundary.

### AUD-15 · S3 · Kelly fields contradict the payload's own stops (extends known K5)

- **SEVERITY:** S3 (advisory in v1)
- **LOCATION:** `UI/MainForm_PlaintextSnapshot.vb:45-49`, `:149` (Kelly gets `ATR × atr_stop_multiplier`, not the placed stop); `Core/ScoringEngine_Kelly.vb:78` (global b), `:104`, `:111-122` (`max_leverage ≤` face/account silently disables the cap).
- **DOWNSTREAM IMPACT:** `kelly.contracts`/`risk_usd` and `levels.*.stop` in one file describe different trades. Any consumer or human sizing from it is wrong in both directions.
- **FAILURE SCENARIO:** **H-P11:** placed `SWING_STOP` 20 USD away; payload says 500 contracts, `risk_usd 50.00`; those contracts risk **$1.67** at the payload's stop (plus a 20 % collateral delta on a BTC-margined long). **H-P04b:** `max_leverage 0` gives 3,125 contracts (31× a $1,000 account) instead of 500. **H-P04d:** ASIA places targets at 1.25 × ATR, so real b = 0.78 and f* = −0.026, yet Kelly shows f* 0.139 and sizes 500 contracts. **H-P04e:** at ATR 60 net-of-fee b = 0.690 gives f* −0.102 while Kelly sizes at the 5 % cap.
- **ANALYTICAL CRITIQUE:** Feed Kelly the placed stop and the placed target net of fees (the parked `kelly-placed-payoff-proposal.md` is the right vehicle); treat `max_leverage ≤ 0` as "no position", not "no cap"; model the collateral currency explicitly or state USD-equity in the contract.

### AUD-16 · S3 · The auto-tweaker cannot apply or revert on .NET 8, and burns an API call per retry

- **SEVERITY:** S3 (fail-safe for trading; the feature and its rollback are dead)
- **LOCATION:** `tools/AutoTweaker/SettingsDiffApplier.vb:346` + `:349` (Apply), `:432` + `:435` (ApplyRevert); `tools/AutoTweaker/AutoTweakerCore.vb:683-698` (failure returns without advancing `LastEvaluatedRowIndex`).
- **DOWNSTREAM IMPACT:** The first live tweak throws before writing (safe). The round records ERROR and does not advance the window, so every later run re-sends the same window to the Claude API and fails the same way. The revert path — the tweaker's safety net — fails identically.
- **FAILURE SCENARIO:** **H-P18:** a benign, validated diff (`verdict_weak_pct 0.35 → 0.36`): `Apply` and `ApplyRevert` both throw `InvalidOperationException: JsonSerializerOptions instance must specify a TypeInfoResolver setting before being marked as read-only`. `JsonArray.Add(String)` binds the generic `Add(Of T)`, which creates a `JsonValueCustomized(Of String)`; `ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})` then calls `MakeReadOnly()` on options with no resolver. Same assembly on Windows. No fixture calls either method.
- **ANALYTICAL CRITIQUE:** Add a `JsonValue.Create(summary)` node (non-generic overload) or pass options with `TypeInfoResolver = New DefaultJsonTypeInfoResolver()`. Add fixtures for `Apply` and `ApplyRevert` against a temp copy, and fix AUD-03's value validation before the tweaker is ever enabled.

### AUD-17 · S3 · Missing fields are defaulted into data that looks real

- **SEVERITY:** S3
- **LOCATION:** `DeribitWsFeed.vb:452-455` + `MarketState.vb:226-235` (a ticker frame without `open_interest`/`mark_price` writes 0.0); `DeribitClient.vb:279-282` (missing `funding_8h` returns 0.0, not `Nothing`); consumed at `UI/MainForm_Analysis.vb:313-352`.
- **DOWNSTREAM IMPACT:** A malformed or partial frame becomes a market event. OI 0 produces `OIChange15m = −100 %` → CAPITULATION/COVERING (upgrade-eligible, Pass 2b input), then the zero sample becomes the oldest in-window anchor and OI goes blind (0 %) for 15 minutes and OI60 for an hour. Missing funding reads as NEUTRAL instead of skipping.
- **FAILURE SCENARIO:** Code-read. One ticker notification without `open_interest` at run time.
- **ANALYTICAL CRITIQUE:** Parse into nullables and keep the last good value (as `funding8h` already does), or fail the stream's freshness. Never substitute 0.0 for "absent" on a field that is divided or differenced downstream.

### AUD-18 · S3 · Run-count state machines make behaviour depend on cadence

- **SEVERITY:** S3
- **LOCATION:** `UI/MainForm_Analysis.vb:266-274` (regime hysteresis = one run), `:343-352` (OI windows anchored on the oldest sample inside 15/61 minutes of *completed* runs).
- **DOWNSTREAM IMPACT:** The same market produces different regimes and OI signals at 1m and 3m cadence and after skip streaks — the class v53 already fixed for funding momentum.
- **FAILURE SCENARIO:** Code-read. The documented "1-bar grace" on a 5m ADX lasts one 1-minute run in NY (1/5 of a bar) and one 3-minute run elsewhere (3/5). After 20 minutes of skips, `OIChange15m` measures a ~1-minute change against a 15-minute threshold.
- **ANALYTICAL CRITIQUE:** Anchor both in time: hold the regime until the next 5m bar closes; compute OI deltas against the sample nearest to *now − 15 min* and mark the signal unavailable when no sample is old enough.

### AUD-19 · S3 · The aggressor-velocity burst is read as of the last trade, not as of now

- **SEVERITY:** S3
- **LOCATION:** `Core/AggressorVelocityAccumulator.vb:127-150` (no decay from the last fold to read time).
- **DOWNSTREAM IMPACT:** In NY the burst modifies the TFI vote (±1). A burst that ended stays at full strength through any following silence.
- **FAILURE SCENARIO:** Code-read. A 5-second sell burst, then 20 s with no prints: the run reads the undecayed fast-horizon sums and classifies `BURST_SELL`.
- **ANALYTICAL CRITIQUE:** Apply `exp(−(now − lastFold)/tau)` to both horizons inside `Snapshot`.

### AUD-20 · S3 · Freshness compares exchange time with an unmonitored local clock

- **SEVERITY:** S3
- **LOCATION:** `Core/Indicators_Momentum.vb:28-35`; no `public/get_time` or skew check anywhere in the tree.
- **DOWNSTREAM IMPACT:** A Windows clock running behind makes stale bars look fresh (and shifts the consumer's max-age the same way, since both read the same box clock); running ahead makes every run skip.
- **FAILURE SCENARIO:** Code-read. Clock 90 s slow: a 1m bar opened 3 minutes ago measures 1.5 minutes old and passes.
- **ANALYTICAL CRITIQUE:** Measure skew against Deribit (`public/get_time` or ticker `timestamp`) and skip when it exceeds a bound.

### AUD-21 · S4 · Tier thresholds use `Ceiling` on a float product

- **LOCATION:** `Core/ScoringEngine_Helpers.vb:86-88`.
- **FAILURE SCENARIO / EVIDENCE:** **H-P17:** `Threshold(20, 0.3) = 6`, `Threshold(20, 0.1 + 0.2) = 7`. Any computed percentage (a tweaker round-trip, a UI slider) can move a tier by a whole point.
- **ANALYTICAL CRITIQUE:** `Ceiling(x − 1e-9)` or integer percentages.

### AUD-22 · S4 · Kelly's null guard is dead and its contract count can overflow before the cap

- **LOCATION:** `Core/ScoringEngine_Kelly.vb:36-46`, `:107`.
- **EVIDENCE:** **H-P04a:** `CalcKellySizing(Nothing, …)` throws `NullReferenceException` at line 36. **H-P04c:** a 1e-8 stop throws `OverflowException` from `CInt` before the leverage cap applies (latent: the shipped min-move floor keeps MEDIUM/HIGH off this path).
- **ANALYTICAL CRITIQUE:** Move the guard first; compute in `Double`/`Long` and cap before converting.

### AUD-23 · S4 · `MarketState.GetBook` returns the live object despite the class contract

- **LOCATION:** `MarketState.vb:6-7` (promise), `:254-258`.
- **EVIDENCE:** **H-P12:** same reference on consecutive calls; a reader clearing it empties MarketState's book. No current reader mutates it.
- **ANALYTICAL CRITIQUE:** Return a copy or make `OrderBookSnapshot` immutable.

### AUD-24 · S4 · Smaller items

- `DynamicNorms.vb:28`, `:122`, `:142` and `DeribitClient.vb:108` read `SettingsLoader.Current` instead of the run's captured `cfg`: a hot reload mid-run mixes two settings versions in one row stamped with one.
- `DeribitClient.vb` never disposes its `JsonDocument`s (pooled buffers not returned) on a 1 GiB box.
- `verdict.Timestamp = DateTime.Now` (`MainForm_Analysis.vb:627`) is local time; display-only today.
- WS stream ages use receive time, so a delayed backlog delivered at once reads as fresh.

---

## 4. Known and tracked defects confirmed still present

Not re-sold as discoveries. Each was checked in the tree at `6e74181`.

| ID | Defect | Where | Status here |
|---|---|---|---|
| K1 | POC-tier gate reads the VPFR labels inverted | `Core/SignalEmitter.vb:331`, `ScoringEngine_Calculate_Verdict.vb:223` | Still present; specced (`engine-fix-build-spec-2026-09-21.md` §3) |
| K2 | Liquidation flag never delivered by the WS stream; `M` booked to the wrong side; USD vs BTC unit | `DeribitWsFeed.vb:499`, `Core/Indicators_OrderFlow.vb:266-272` | Still present; specced §4 |
| K3 | `DeriveWsHealth` `OK` does not reflect trade flow | `Core/SignalEmitter.vb:101-109` | Still present; AUD-02 is its scoring consequence |
| K4 | WS subscribe reply never verified | `DeribitWsFeed.vb:219` (was `:208`) | Still present; AUD-02 shows the trades-channel case does not skip |
| K5 | Kelly fixed b, assumed p, no fees | `Core/ScoringEngine_Kelly.vb:64-82` | Parked (`kelly-placed-payoff-proposal.md`); AUD-15 adds the ASIA sign flip and the placed-stop contradiction |
| K6 | Forming-bar stub at on_close fires | every candle indicator | Status quo pending a ruling; the TRACE measures ATR 14.1 % low on a flush |
| K7 | Score does not rank outcomes | — | Research finding, not a code defect |

---

## 5. Checked and sound

`DeriveDirection` tests the `NO TRADE` prefix before the direction words, so lean tags never become actionable. `TryWrite` renames a same-directory temp file (atomic on NTFS and ext4). The trade-order contract (REST reversed, WS appended, windows via `LastN`) is consistent. OFI ratios are bounded to [1e-3, 1e3]. Step 4b consults the dominant side's flag and Step 5c evaluates the placed target, as documented. `ProcessIdentity` is monotonic and thread-safe. `face × Δ / entry` is the exact inverse-contract USD loss at exit. `ExecuteWithRetry` is bounded and `UseProxy = False` closes the WPAD hang. The accumulators are folded and read only under MarketState's lock. `SignalEmitter.Serialize` works on .NET 8 (the payload uses non-generic `JsonValue` nodes; H-P10 writes a sample file).

---

## 6. Verification handles

**H-1 (run all):** `dotnet run -c Release --project verify/auditproofs/AuditProofs.vbproj` from the repo root. Run at `6e74181` on .NET 8.0.31 (Linux); the harness is plain `net8.0` and links the same sources as `verify/ordercheck`, so it also runs on Windows. Actual final line:

```
=== 22 defect(s) confirmed, 0 not reproduced ===
```

**H-2 (one case):** append `-- P05` (any case id) to H-1. `P14` runs last on purpose: it leaves the static init task pending.

Code-read findings (AUD-09, AUD-11, AUD-17 to AUD-20, parts of AUD-06) have no runnable handle: they need WinForms or a live socket. Their line references are the evidence.

---

## 7. Modular prompts for the unaudited remainder

Each prompt is standalone. Paste it, then paste (or open, in Claude Code) the files it names.

### 7.0 Model, effort and sittings (CLAUDE.md brief rule)

Line counts are `wc -l` at `45e9c4d`. Set effort explicitly: Opus 5.5's API default is `medium`, below every row here. Nothing below is measured model-vs-model on this codebase; tiers come from task shape and from the repo's use of a Fable seat for senior review.

**Order by what each review unblocks.** Do the review of this report and M9 first, because they re-rank every severity above. Then M1, because it writes the live config. Then M4 and M7b, because every future change is judged through them. The rest in any order.

| Review | Lines | Model, effort | Sittings | Why this tier | Where it will slip | Escalate when |
|---|---|---|---|---|---|---|
| **This report (PR review)** | report + 786-line proof harness | Fable 5.1, high | 1 | Independence from the author's model is the point; this is the cheapest place to catch a wrong severity before it orders the fix queue | Accepting a proof that exercises the mechanism on synthetic inputs the live engine never produces; not running H-1 | — (top tier). Stop and ask if a proof passes for a reason other than the one its case id claims |
| **M9** consumer | order-app repo, unmeasured | Fable 5.1, xhigh | 1, in a session with that repo attached | Decides whether AUD-05/06/10/11 are S0. Needs Deribit order semantics (trigger type, stop-limit gap-through, reduce-only, post-only rejects) and the fill and reject state machine across two repos | Stating exchange behaviour from memory instead of the app's code and Deribit's docs; treating a gate's existence as its correctness | If it cannot name the rounding owner or the stop-trigger type from code, it stops and asks the trader; it must not assume |
| **M1** AutoTweaker | 3,647 | Opus 5.5, xhigh | 1 | The one module that writes live `settings.json`. State machine (window cursor, streak, snapshots) plus JSON-serializer traps that only show at runtime, so findings need repros | Reading the path fences as validation; asserting Apply/Revert behaviour without running it; judging `ClaudeApiClient.vb`'s model id and request shape against its own training prior instead of current docs | A state or Apply/Revert finding it cannot back with a runnable case after two attempts → Fable 5.1 |
| **M4** outcome measurement | 3,538 | Opus 5.5, xhigh | 2: `LivePerformanceTracker.vb` alone · the other six | Every "is there edge" answer comes from here, and its errors are silent and bias every later decision | A bar that touches both levels; UTC vs UTC+8 windows; pooled fees (Σnet/n vs net of Σ); `.bak` rotation dropping rows | Its fee arithmetic disagrees with `DeribitIndicatorProject.md` §5a's net-EV definition → Fable 5.1 |
| **M7b** backtest, what-if, ceiling | 7,295 | Opus 5.5, xhigh (sitting 2: Sonnet 5, high) | 3: ReplayLoop + RowWriter + BacktestProgram + OverlapValidator (2,119) · CoverageReport (2,052) · WhatIf + CeilingAudit (3,124) | Replay-vs-live parity is the hardest reasoning left: forming-bar stub, injectable clock, look-ahead. A bias here ships as a scoring change, the reserved class | Look-ahead via a closed bar the live run had not seen; split-half leakage; `L2Logistic` numerics. CoverageReport is heavily specced, hence the lighter tier | A look-ahead claim it cannot show as a timestamp pair; for sitting 2, any finding outside coverage accounting → Opus 5.5 |
| **M3** exit guard, strip, alerts, absorption | 1,951 | Opus 5.5, xhigh | 1 | Concurrency against one `MarketState` lock and a live `GetBook` reference, plus microstructure math a human exits on | Reading each file alone instead of against `MarketState`'s lock; rating display-only as low severity when a person trades off it | A race it cannot pin to two named thread entry points → Fable 5.1 |
| **M6a** UI shell and startup | 2,628 | Opus 5.5, high | 1 | Timer and marshalling hazards; the 2026-09-18 thread-exhaustion incident shows this class is live and expensive | Missing a blocking `Invoke` reached from a timer; assuming the Designer's wiring instead of reading it | Any cross-thread hazard it cannot trace from timer start to UI marshal → Fable 5.1 |
| **M2** settings contract | 2,772 | Opus 5.5, high | 1 | Mostly tracing, but its range table becomes the spec for the AUD-03 validator, so a wrong range becomes code | Inventing ranges without citing the consuming line; flagging deliberate POCO-vs-JSON differences the A62 drift guard already polices | Any range cell without a consumer `file:line` is rejected and re-derived |
| **M7a** offline analysis | 1,786 | Opus 5.5, high | 1 | Denominators, population filters and fee paths decide which settings the trader changes | Taking "net EV" on trust without checking both fee paths (5 bps loss, 3 bps target); counting NO TRADE lean rows (EVAL-1 class) | A headline number it cannot recompute by hand from a 10-row synthetic CSV |
| **M5** tape store and repair | 3,290 | Opus 5.5, high | 2: `TradeStoreWriter.vb` alone · the rest | Concurrency plus month rollover, but heavily specced and fixture-covered (A55/A56), so the new-finding yield is lower | Re-reporting built-but-undeployed items (same-ms page skip, cross-month gap, scan-failure rewrite) as new | A write-guard finding that the A55/A56 fixtures contradict: it runs them before it reports |
| **M8** fixture harness | 16,764 | Sonnet 5, high | 3 at ~5,600 lines each (ranges in the prompt) | Mostly mechanical provenance and coverage checks | Checking literals against today's `settings.json` only (the F3 trap: it must be every tracked revision, `git log -p settings.json`); claiming a wrong assertion without running the harness | Any "this fixture asserts the wrong property" claim → Opus 5.5 re-check before it is reported |
| **M6b** render surfaces | 5,985 | Sonnet 5, high | 2: `MainForm_Render_Cards.vb` (3,684) · PlaintextSnapshot + Controls (2,301) | Parity, null and format handling; the one severity-critical fact (an exception here suppresses the payload) is already AUD-09 | Flagging behaviour that `architecture.md` "Display Behaviour Clarifications" documents; asserting parity drift without diffing both surfaces | Any claim that a render path changes a value reaching the payload or Kelly → Opus 5.5 |
| **M10** ops readers and probes | 5,158 | Sonnet 5, high | 2: SwingFallbackRead + MediumTierRescore + DiagnosisExport + TierDemotionCensus (2,628) · PocGateDefect + TierOrderStability + LiquidationFlagCheck + WsTradeProbe (2,530) | One-off readers; the rulings they fed are already made | Pooled counts that skip `.bak` rotations; re-deriving a statistic differently and calling the difference a bug | A finding that would overturn a recorded trader ruling → Opus 5.5 re-check first, since it re-opens a settled decision |

Nothing here needs Haiku 4.5.

### M1 — AutoTweaker (highest priority: it writes the live `settings.json`)

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: the engine trades BTC-PERPETUAL on Deribit only — an INVERSE contract (1 contract = 10 USD; P&L and margin in BTC; long P&L_BTC = N·10·(1/entry − 1/exit)). Execution bars are 1-min (NY) and 3-min (ASIA/LONDON), regime on 5m, a 15m MTF veto; holds are 2–15 min. The verdict is written to verdict_signal.json and an automated order app places live orders from it. Fees (settings v68: maker 1.5 bps, taker 3.5 bps) are modelled ONLY in the target-side minimum-move floor (maker/maker round trip + 5 bps net = 8 bps); nothing prices the loss path, Kelly or reward-to-risk. SettingsLoader hot-reloads settings.json into the live engine within ~200 ms with NO value validation.
Files to audit (paste them): tools/AutoTweaker/AutoTweakerCore.vb, PromptBuilder.vb, ClaudeApiClient.vb, SettingsDiffApplier.vb, SnapshotManager.vb, ConditionsExtractor.vb, RoundStatsBuilder.vb, CompositeScorer.vb, TweakerConfig.vb, TweakerState.vb, AutoTweakerProgram.vb.
State to keep in mind: TweakerState.LastEvaluatedRowIndex (fixed-window cursor), CurrentBelowThresholdStreak, snapshot files used by ApplyRevert, RejectedPathPrefixes / DisabledGatedPaths (path fences only), the load-time population filter (NY × 1-min, weekday). The engine reads analysis_log.csv columns written by AnalysisLogger (Verdict, Price, PlacedTarget*/PlacedStop*, ExecResolution, InstanceId, SignalId).
Already found — extend, do not repeat: Validate never checks values (a negative atr_stop_multiplier or mtf_gate.min_of 0 is accepted); Apply and ApplyRevert throw on .NET 8 at the change_log JsonArray.Add(String) + ToJsonString(options without TypeInfoResolver); a failed Apply returns without advancing the window, so every run re-bills the same API call.
Before any finding, write a scratchpad titled "--- LOGIC TRACE ---": simulate a high-volatility week (ATR(7) 30 → 140 during a −1.5 % flush) flowing through the failure-rate trigger, the prompt, the diff, validation, apply, hot-reload and the next live payload's stop/target. Then report every flaw as: SEVERITY (S0–S4) / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M2 — Settings contract and defaults

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL on Deribit only (inverse; 10 USD contracts; BTC-margined). 1-min NY / 3-min ASIA+LONDON execution, 5m regime, 15m veto, 2–15 min holds. The engine's verdict file drives live orders. Fees are modelled only in the target-side 8 bps floor (maker 1.5 / taker 3.5 bps); the loss path, Kelly and R:R ignore fees.
Files to audit: Core/Settings/EngineSettings.vb (all 1,452 lines), settings.json (tracked, v68), and for reference Core/Settings/SettingsLoader.vb.
State to keep in mind: EngineSettings POCO defaults are what the engine runs on when settings.json fails to parse at startup; SettingsLoader keeps "last good" on a hot-reload parse error; settings.local.json overlays six admitted blocks (trade_store, signal_bridge, live_strip, exit_guard, performance_display, analysis_logging) plus four network keys and auto_run.start_engaged. Derived values: TradeCostSettings.EffectiveMinMovePct = RoundTripFeePct + MinNetMovePct; SignalEmitter.TickSize = 0.5; structural_levels (target_max_atr_mult 3.5, stop_max_atr_mult 1.6, stop_min_floor_ticks 4).
Already found: no value validation anywhere (negative multipliers, zero periods, min_of 0 all load); performance_display.enabled=false at startup deadlocks every run; CalcEMA returns 0 below its period.
Before any finding, write "--- LOGIC TRACE ---": for a high-volatility flush, trace which settings keys shape the stop, target, min-move gate, MTF veto and Kelly fields in the payload, and what each POCO default does if the JSON block is missing. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Produce the missing per-key range/sign table as part of the critique. Look beyond these categories.
```

### M3 — Exit guard, live strip, alerts, absorption (what a human uses mid-trade)

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL on Deribit only (inverse, 10 USD contracts, BTC-margined). 1-min NY / 3-min ASIA+LONDON bars; 2–15 min holds. An automated order app trades the engine's verdict file; these modules are display/alert-only but are what the trader uses to exit. Fees: maker 1.5 / taker 3.5 bps, modelled only in the entry-side 8 bps floor.
Files to audit: ExitGuardEvaluator.vb, UI/MainForm_ExitGuard.vb, LiveMicrostructureEvaluator.vb, UI/MainForm_LiveStrip.vb, Core/AlertsTracker.vb, Core/LevelAbsorptionTracker.vb.
State to keep in mind: MarketState (single lock; trade ring 5,000, top-10 book replaced per ~100 ms frame, accumulators reset on reconnect; GetBook returns the LIVE object, not a copy); ScoringEngine.ComputeFastExitPrimitives (shared adverse-signal definitions); the carried levels set once per full run via SetAbsorptionLevels / SetAlertsLevels (5m swings and nearest HVNs — the 5m swing pivot repaints because its right wing includes the forming bar); the posState radio buttons (manual, not the real position); ws trades have NO age gate on a connected socket; liquidation flags are never delivered on the WS trades channel.
Before any finding, write "--- LOGIC TRACE ---": simulate a −1.5 % liquidation flush while a long is declared, tick by tick through the 3 s exit-guard timer, the debounce, the strip, the cascade alarm and the level-approach alerts. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M4 — Outcome measurement: eval cache, OHLC cache, CSV logger

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL on Deribit (inverse, 10 USD contracts). 1-min NY / 3-min ASIA+LONDON bars; eval windows 15 min (res 1) / 45 min (res 3); 2–15 min holds. Every decision about whether the engine has an edge comes from these files. Fees: maker 1.5 / taker 3.5 bps; "net EV per trade after fees" is the headline metric (DeribitIndicatorProject.md §5a).
Files to audit: LivePerformanceTracker.vb (1,870 lines), OhlcCache.vb, AnalysisLogger.vb, AnalysisOutputDump.vb, analysis/ForwardWindowJoiner.vb, analysis/FailureRateMatrix.vb, analysis/BandLadder.vb.
State to keep in mind: static _evalCache (unbounded; WriteEvalCache rewrites the file from it), _ohlcLookup, _initTcs (UpdateAsync awaits it BEFORE checking Enabled — already found to deadlock when performance_display is off at startup), barriers = SignalEmitter.ComputeSideLevels placed levels, floor = TradeCosts.EffectiveMinMovePct, UTC+8 display windows, weekday filter, CSV Timestamp is UTC. Placed levels are not on the 0.5 tick grid; ATR is ~14 % low at on_close fires (forming-bar stub).
Before any finding, write "--- LOGIC TRACE ---": push one STRONG LONG emitted during a flush (entry 59,003.5, stop 58,811.13, target 59,079.15) through PENDING → resolution against 1m OHLC → aggregates → the perf strip, including a bar that touches both levels. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M5 — Tape capture and gap repair

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL public trades (~60k/day; amounts in USD; trade_id string, trade_seq monotonic Long). Deribit serves only ~24 h of public trades, so this store is the only source for re-deriving trade-based signals. The live engine does not read it, but every calibration of CVD/TFI/MicroCVD/liquidation thresholds does.
Files to audit: Core/TradeStoreWriter.vb (1,391 lines), TradeStoreGapRepair.vb, Core/StoreFiles.vb, Core/CaptureMarkerLog.vb, Core/RepairStatusLog.vb, Core/VenueStatusLog.vb, Core/WsHealthLog.vb, Core/WsFeedLog.vb, tools/BacktestRunner/HistoricalStore.vb.
State to keep in mind: DeribitWsFeed.ApplyTrades is the store's only streaming inbound path (Buffer + flush every 30 s or 500 trades, flush also runs on the WS receive thread); SeedAsync calls ResetBufferState on every reconnect; the identity-keyed write guard (20,000-trade window, RecentWindowCapacity); monthly file rollover; repair by trade_seq ranges every 6 h over 20 h. Known and specced: same-ms page skip, cross-month leading gap, scan-failure rewrite (all built, deploy pending).
Before any finding, write "--- LOGIC TRACE ---": simulate a flush with a 4 s WS stall, a reconnect whose REST seed fails, a month boundary at 00:00 UTC and a concurrent repair pass. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M6a — UI shell and startup (MainForm_Layout)

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8 WinForms). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: this form hosts an unattended collector on a 1 GiB AWS box and emits verdict_signal.json, which an automated order app trades (BTC-PERPETUAL, inverse). Any exception escaping RunAnalysisAsync shows a MODAL MessageBox and leaves btnAnalyze disabled, which silently stops all later auto-run fires (already found). A 2026-09-18 incident: blocking Control.Invoke from 1 Hz timers parked 11,630 threads and took the box down.
Files to audit: UI/MainForm_Layout.vb (2,158 lines), UI/MainForm_TapeStoreStatus.vb, UI/MainForm_Calibration.vb, MainForm.Designer.vb (read-only reference).
State to keep in mind: form fields _oiHistory, _fundingHistory, _ofiHistory, _mtfCandles15m/_mtfLastFetchTime, _prevRegime, _wsFeed/_marketState/_wsSource/_restSource, _autotradeArmed (runtime-only, never persisted); InitialiseAsync launched only when performance_display.enabled; ResolveSource() and the WS health delegate (IsConnected AndAlso Not IsCoolingDown).
Before any finding, write "--- LOGIC TRACE ---": cold start during a venue outage (REST 5xx, WS reconnecting) followed by a −1.5 % flush: which timers start, what each marshals to the UI thread, and what reaches the first payload. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M6b — Render surfaces (cards + plaintext snapshot)

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8 WinForms). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: the verdict drives live orders on BTC-PERPETUAL (inverse). The card binds run inside RunAnalysisAsync BEFORE the bridge payload is emitted, so any exception here suppresses the payload and raises a modal MessageBox that halts auto-run. The display-string parity rule requires the snapshot and the cards to render the same values. Kelly (advisory) is computed inside BuildPlaintextSnapshot from ATR × atr_stop_multiplier, not from the placed stop.
Files to audit: UI/MainForm_Render_Cards.vb (3,684 lines), UI/MainForm_PlaintextSnapshot.vb (506 lines), UI/Controls/*.vb.
State to keep in mind: VerdictResult and IndicatorResults fields (nullable absorption/aggressor numerics, VPFR arrays, SwingTarget/Stop zero-as-unset), SignalEmitter.ComputeSideLevels (the one placed-level seam), VerdictResult.FormatVerdictForDisplay, _lastSuccessful* capture for the SKIPPED overlay.
Before any finding, write "--- LOGIC TRACE ---": render a run during a −1.5 % flush with ATR 120, empty absorption fields, a NaN-free but zero VPFR histogram and a STRONG LONG with R:R 0.39. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M7a — Offline analysis (report generation)

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL (inverse). The offline report decides which settings the trader changes and whether signals are traded at all. Fees: maker 1.5 / taker 3.5 bps; the loss path costs 5 bps round trip, the target path 3 bps; net EV per trade after fees is the headline metric. Placed levels come from SignalEmitter.ComputeSideLevels and are not tick-rounded; 1m candles cannot order a bar that touches both target and stop.
Files to audit: analysis/AnalysisRunner.vb, analysis/MarkdownReportWriter.vb, analysis/AnalysisReport.vb, analysis/OutlierAudit.vb, analysis/FundingMomentumDiagnostic.vb, analysis/DeribitOhlcFetcher.vb, analysis/AnalysisConstants.vb, analysis/AnalysisReportForm.vb.
State to keep in mind: analysis_log.csv schema (116 columns, header rotations to .bak, InstanceId/SignalId identity), ForwardWindowJoiner.IsWeekdayRow, the LEGACY_YARDSTICK population, session × resolution segmentation. Known: NO TRADE lean rows counted as trades in one VerdictContext table (EVAL-1).
Before any finding, write "--- LOGIC TRACE ---": follow 100 rows from a flush day through the join, the tier × window matrix and the headline numbers, including fees on both paths. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M7b — Backtest, what-if and ceiling tools

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see: BTC-PERPETUAL (inverse). These tools replay history to justify live settings changes; a bias here ships as a live scoring change. Live rows are computed with a forming-bar stub as the last candle (ATR ~14 % low at on_close fires); replay must match that convention or say it does not. Fees: maker 1.5 / taker 3.5 bps.
Files to audit (split across two sittings if needed): tools/BacktestRunner/ReplayLoop.vb, BacktestRowWriter.vb, OverlapValidator.vb, CoverageReport.vb (2,052 lines), BacktestProgram.vb; tools/WhatIfRunner/*.vb; tools/CeilingAudit/*.vb.
State to keep in mind: the replay re-derives placed levels through the shipped SignalEmitter.ComputeSideLevels; the what-if overlay whitelist; split-half validation; the VWAP session anchor uses an injectable nowUtc (live uses wall clock).
Before any finding, write "--- LOGIC TRACE ---": replay a −1.5 % flush hour through the synthesizer and the what-if EV ranking, comparing each computed field with what the live engine would have emitted. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M8 — The fixture harness itself (three sittings: lines 1–5,600 · 5,601–11,200 · 11,201–16,764)

```text
You are a hostile senior quant developer auditing the test harness of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files.
Context you cannot see: this harness (verify/ordercheck/Program.vb) is the only automated check before code that drives live BTC-PERPETUAL orders ships. It links the shipped sources; it cannot link any MainForm_* file, so WinForms orchestration (RunAnalysisAsync, the signal-bridge glue, auto-run) is untested by construction. Fees: maker 1.5 / taker 3.5 bps.
Files to audit: verify/ordercheck/Program.vb, lines <RANGE>; verify/ordercheck/OrderCheck.vbproj.
State to keep in mind: the CLAUDE.md fixture-literal provenance rule (SHIPPED BEHAVIOUR must derive from cfg; MECHANISM literals must be off every EVER-shipped value); known coverage gaps: SettingsDiffApplier.Apply and ApplyRevert are never called (both throw on .NET 8), SignalEmitter geometry invariants are never asserted, the 15m MTF fail-open is never asserted as a hazard.
Before any finding, write "--- LOGIC TRACE ---": pick the three fixtures closest to the order payload and trace what they would NOT catch during a high-volatility flush. Then report each gap or wrong assertion as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M9 — The consumer (DeribitOrderPlacementApp) against this report

```text
You are a hostile senior quant developer auditing the order-placement app that trades DeribitVerdictEngine's verdict_signal.json (schema v1) on BTC-PERPETUAL (inverse; 10 USD contracts; BTC-margined; tick 0.5). Do not praise, do not rewrite files; you may write isolated test code to prove failures.
Context you cannot see (engine side, verified 2026-09-24 in docs/adversarial-audit-2026-09-24.md): levels are NOT tick-rounded; a SWING_STOP can be 2 USD from entry while the contract lets you slip 0.6 × ATR from levels.entry; with a bad settings value the engine can emit a long whose stop is above entry; generated_at_utc is emission time, not data time; kelly.* is computed against a different stop than levels.*; the file can be left stale if the engine's rename fails; ws: OK does not mean trades are flowing.
Files to audit: the signal reader/watcher, the gate chain, order construction (entry, stop-limit, TP), rounding, position/fill handling and rejection handling in the order app.
Questions that decide severity for the engine findings: Who rounds prices, and in which direction? Is stop side validated against the actual fill before placement? What happens when Deribit rejects the stop after the entry fills? Which trigger (mark/index/last) do stops use? How does the reader open the file (share modes)? Does anything read kelly.*?
Before any finding, write "--- LOGIC TRACE ---": take a STRONG LONG emitted during a −1.5 % flush (entry 59,003.5, stop 58,811.13, target 59,079.15, ATR 120.2) and a second one with a 2 USD SWING_STOP, and trace each through every gate to the exchange, including fees (maker 1.5 / taker 3.5 bps) on both paths. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

### M10 — Low-priority ops readers and probes

```text
You are a hostile senior quant developer auditing part of DeribitVerdictEngine (VB.NET, .NET 8). Do not praise, do not rewrite files.
Context you cannot see: BTC-PERPETUAL (inverse). These are one-off research readers whose outputs have justified trader rulings (e.g. MEDIUM-tier diagnosis, swing-vs-fallback reads); a wrong number here can ship as a live change later. Fees: maker 1.5 / taker 3.5 bps.
Files to audit: tools/ops/SwingFallbackRead/*.vb, tools/WsTradeProbe/*.vb.
State to keep in mind: the CSV schema and rotation to .bak files (pooled counts must include them); WsTradeProbe parses trades with its own readers (five known divergences, S-1 ruled).
Before any finding, write "--- LOGIC TRACE ---": follow one flush-hour row through each reader's statistic. Then report each flaw as SEVERITY / LOCATION / DOWNSTREAM IMPACT / FAILURE SCENARIO / ANALYTICAL CRITIQUE. Look beyond these categories.
```

---

## 8. What this audit did not verify

- **The consumer.** AUD-05, AUD-06, AUD-10 and AUD-11 are S1/S2 on the engine side; whether any becomes an S0 depends on order-app code this session cannot see (M9).
- **Live frequency.** No `analysis_log.csv`, tape or eval cache is in the clone, so no finding here states how often its trigger fires in production.
- **Windows specifics.** `MoveFileEx` sharing behaviour against the consumer's reader (AUD-11) and the WinForms modal loop (AUD-09) are reasoned from code, not run.
- **Deribit channel semantics.** Whether the 100 ms book channel is change-driven affects how often AUD-12 fires, not whether it is wrong.
- **The collector's overlay.** Whether any box runs `performance_display.enabled: false` (AUD-04's trigger) is not visible from the repo.
