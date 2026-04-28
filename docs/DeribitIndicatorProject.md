# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-27 | Current version: v15 — dead-code cleanup pass on top of v14 calibration**

This document is the authoritative handover for any new AI conversation continuing this project.

**Session start checklist:** Read this file + `docs/architecture.md`. Do NOT read individual `.vb` files unless a specific edit is required.

> ⛔ **PROHIBITED:** - NEVER call load_skill() under any circumstances during this project. It is not relevant to this codebase and consumes context budget without benefit.

---

## 1. Project Purpose

A Windows Forms (VB.NET / .NET 8) desktop application that connects to the Deribit REST API,
calculates a set of technical indicators on live BTC-PERPETUAL data, scores them via a
weighted multi-tier engine, and emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE /
WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

The latest completed feature set adds **session-aware volume norms** as a first-class adaptive
normalisation layer: `DynamicNorms` now applies time-of-day session volume buckets (ASIA /
LONDON / NY) so volume thresholds better reflect BTC liquidity regime by UTC hour, and the
config/UI/docs surface has been updated to support that behaviour. The already-implemented
**OI × CVD cross-confirm** feature is also now considered part of the shipped scoring pipeline:
`RunScoringPipeline()` includes a Pass 2b gate that rewards confirmed OI/CVD alignment and
penalises direct conflict for full OI signals.

In addition, the scoring pipeline now includes a Pass 2c **regime-alignment gate**:
when `cfg.RegimeWeights.Enabled` and the dominant side is non-zero, all active
regime-key signals are checked for alignment (TRENDING: EMA+ROC+CVD;
RANGE_BOUND: VWAP+RSI+Donchian). Full alignment earns `AlignmentBonus`; full
conflict incurs `ConflictPenalty`. TRANSITIONAL and tied scores bypass the gate.

---

## 2. Repository

- **GitHub:** https://github.com/Beansz2015/DeribitVerdictEngine
- **Branch:** `master`
- **Solution file:** `DeribitVerdictEngine.sln`
- **Target framework:** .NET 8, Windows Forms

---

## 3. File Inventory

### Root

| File | Purpose |
|---|---|
| `DeribitClient.vb` | All Deribit REST calls incl. 15m candles, recentTrades |
| `DynamicNorms.vb` | ATR/Vol/VWAP norm computation; now includes session-aware volume threshold adjustment |
| `AnalysisLogger.vb` | CSV logging + CalibrationReport |
| `OiSnapshot.vb` | OI ring-buffer helper |
| `AutoRunTimer.vb` | IAutoRunTimer interface + WinFormsAutoRunTimer impl |
| `Program.vb` | Entry point |
| `settings.json` | v15 — all tunable parameters incl. `indicators.funding`, `indicators.oi_cvd_cross`, `session_volume`, and `regime_weights` blocks. v15 is a cleanup pass that strips silently-ignored keys (no behavioural change). |
| `MainForm.Designer.vb` | Auto-generated WinForms designer (do not edit) |
| `MainForm.resx` | Form resources |

### Core/

| File | Purpose |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. AdjustedLongTarget, AdjustedShortTarget, TargetCapReason, VerdictContext, Kelly fields), PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore, Threshold, TierFloor, AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus |
| `Core/ScoringEngine_Calculate_Scoring.vb` | `AppendLean()`, `CalcVerdictContext()`, `RunScoringPipeline()` — Steps 2 / Pass 2 / Pass 2b / 3 / 3b: signal scoring, partial upgrades, OI×CVD cross-confirm, funding modifiers, breakdown note rows |
| `Core/ScoringEngine_Calculate_Verdict.vb` | `Calculate()` entry point — Step 4 regime veto / TRANSITIONAL penalty, Step 4b MTF gate veto, Step 5 verdict generation, ATR target cap |
| `Core/ScoringEngine_Kelly.vb` | `CalcKellySizing()` — display-only Kelly sizing. Called from `MainForm_Render_Header`, not from `ScoringEngine.Calculate()`. Zero scoring impact. See `docs/kelly-criterion-proposal.md`. |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields incl. `FundingMomentum` |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcRSI, CalcRSISeries, CalcRSIDivergence, CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcLiquidations, CalcCVD, CalcMicroCVD, CalcTFI, CalcFundingMomentum |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite, CalcMTFGate |
| `Core/Settings/EngineSettings.vb` | Strongly-typed POCO for settings.json incl. KellySettings, FundingSettings, OiCvdSettings, SessionVolumeSettings, and RegimeWeightSettings |
| `Core/Settings/SettingsLoader.vb` | JSON deserialisation, SettingsLoader.Current singleton, FileSystemWatcher hot-reload |

### UI/

| File | Version | Purpose |
|---|---|---|
| `UI/MainForm_Layout.vb` | Shared fields, constructor, resize helpers; now also owns `_fundingHistory` and `FundingHistoryMax` |
| `UI/MainForm_AutoRun.vb` | Auto-run timer lifecycle |
| `UI/MainForm_Analysis.vb` | RunAnalysisAsync() — full data fetch + indicator + scoring pipeline; appends funding history and computes `FundingMomentum` |
| `UI/MainForm_Render_Header.vb` | RTF helpers, CalibrationReport/log helpers, and `RenderOutputHeader()` for VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME / LAST PRICE / HOLD STATUS / ATR levels / KELLY block |
| `UI/MainForm_Render_Sections.vb` | `RenderOutput()` entry point + all indicator sections, funding section, signal breakdown table, verdict label update |

### Docs

| File | Purpose |
|---|---|
| `docs/DeribitIndicatorProject.md` | This handover document |
| `docs/architecture.md` | Codebase structure, data flow, design decisions |
| `docs/trader-profile.md` | Trader style, indicator preferences, collaboration preferences |
| `docs/verdict-context-tag-proposal.md` | Spec: Verdict Sub-Context Tag — ✅ IMPLEMENTED |
| `docs/kelly-criterion-proposal.md` | Spec: Kelly Criterion position sizing — ✅ IMPLEMENTED |
| `docs/bbw-scoring-proposal.md` | Historical |
| `docs/bbw-scoring-response.md` | Historical |
| `docs/dual-scoring-fix-proposal.md` | Historical |
| `docs/dual-scoring-fix-response.md` | Historical |

For the full annotated directory tree and data flow diagram, see `docs/architecture.md`.

---

## 4. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Config keys |
|---|---|---|
| ROC(9) | CalcROCSeries | `cfg.Indicators.ROC.SlopeSensitivity` (0.1) — gates partial scoring and Pass 2c ROC-active check |
| RSI(9) | CalcRSI | `Overbought` (60) / `Oversold` (40) / `PartialOverbought` (55) / `PartialOversold` (45) — v14 widened the 45–55 neutral dead-band to stop RSI voting on every tick off 50 |
| RSI Divergence | CalcRSIDivergence | −1 long: BEARISH + RSI > `DivPenaltyRsiHigh` (65); −1 short: BULLISH + RSI < `DivPenaltyRsiLow` (35). `PivotWing` (2), `LookbackBars` (20) from cfg. |
| DMI/ADX | CalcDMI | 5m candles. `cfg.Indicators.ADX.TrendThreshold` (25) |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms, now session-adjusted via `session_volume` config. Mid-tier directional partial via cross-confirm. |

### Tier 1
| Indicator | Method | Config keys |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. `cfg.Indicators.VWAP.WarmupCandles` (15) |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED when price between bands |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | `cfg.Scoring.BbwSqueezePenalty` (2); `cfg.Indicators.TTM.FlatThreshold` (0.5) |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor |
| Funding Rate | GetFundingRateAsync | Step 3 baseline funding modifier from cfg thresholds |
| Funding Momentum | CalcFundingMomentum | `cfg.Indicators.Funding.MomentumEnabled`, `MomentumWindow`, `MomentumThreshold`, `MomentumAmplify`, `MomentumSoften` |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL |
| OI × CVD Cross-Confirm | Pass 2b in `RunScoringPipeline()` | `cfg.Indicators.OiCvd.Enabled`, `UpgradeBonus`, `ConflictPenalty` |

### Tier 2
| Indicator | Method | Config keys |
|---|---|---|
| OFI | CalcOFI | `cfg.Indicators.OFI.BookDepth` (5); `BuyDominantRatio` (2.0) / `SellDominantRatio` (0.5) — v14 relaxed from 3.0/0.333 |
| Liquidations | CalcLiquidations | `cfg.Indicators.Liquidations.DominanceRatio` (2.0); penalty magnitudes from cfg |
| CVD | CalcCVD | 3-segment weighted slope (late×2 − early×1). −1 on divergence. `SlopeMinUsd` (12000) v14 — raised from 1000 noise floor. |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty. Window=50, `AccelThreshold` (10000) v14 — raised from 5000; dynamic scaling deferred until backtesting ships. |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30, threshold=0.15 via cfg. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below |

### Tier 3
| Indicator | Method | Config keys |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial + NONE mid-channel note |
| OBV | CalcOBV | Trend + divergence gate from cfg. Adverse divergence blocks cross-category upgrade. |
| VPFR-lite | CalcVPFRLite | POC proximity; HVN wall triggers target cap. Exp decay (base=0.985). `numBuckets` (50) from cfg. |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK; forces NO TRADE on BLOCK. TTL cache 60s. 1-bar regime hysteresis. |

---

## 5. Verdict Levels

| Verdict | Meaning |
|---|---|
| STRONG LONG | High-confidence long |
| LONG | Standard long |
| WEAK LONG | Low-confidence long |
| NO TRADE | Insufficient signal or MTF block |
| WEAK SHORT | Low-confidence short |
| SHORT | Standard short |
| STRONG SHORT | High-confidence short |

---

## 6. settings.json Structure

`SettingsLoader.Initialise()` called in `MainForm.New()`. `SettingsLoader.Current` returns the singleton. Current file version: **v15** (cleanup pass — silently-ignored keys removed; no behavioural change vs v14).

```
settings.json
  indicators:
    rsi:           { period (9), overbought (60), oversold (40),
                     partial_overbought (55), partial_oversold (45),
                     divergence_price_gate, divergence_rsi_delta,
                     div_penalty_rsi_high (65), div_penalty_rsi_low (35),
                     pivot_wing (2), lookback_bars (20) }
    roc:           { period (9), slope_sensitivity (0.1), series_lookback (3) }
    adx:           { period (9), trend_threshold (25), range_threshold (20) }
    vwap:          { session2_start_hour (13), session2_start_minute (30),
                     warmup_candles (15) }
    bbw:           { period (20), std_dev (2.0) }
    ema:           { fast (9), mid (21), slow (50) }
    donchian:      { period (20) }
    obv:           { trend_gate (0.001), divergence_gate (0.001) }
    atr:           { period (7), static_ref (115.0),
                     scale_min (0.25), scale_max (4.0) }
    ofi:           { book_depth (5), buy_dominant_ratio (2.0),
                     sell_dominant_ratio (0.5) }
    volume:        { sma_period (9), static_high (3.0), static_mid (2.0),
                     dynamic_high_clamp_min (2.0), dynamic_high_clamp_max (6.0),
                     dynamic_mid_clamp_min (1.5), dynamic_mid_clamp_max (4.0) }
    vwapDynamic:   { dev_clamp_min (0.30), dev_clamp_max (3.0),
                     static_fallback (1.5) }
    liquidations:  { large_liq_size (200), dominance_ratio (2.0) }
    oi:            { neutral_band_pct (0.05), change_threshold_pct (0.01) }
    dmi:           { period (9) }
    cvd:           { slope_min_usd (12000), slope_pct_of_value (0.01),
                     divergence_price_gate (0.0005), divergence_penalty (1) }
    tfi:           { window_size (30), threshold (0.15) }
    microCvd:      { window_size (50), accel_threshold (10000),
                     decel_penalty (1) }
    ttm:           { flat_threshold (0.5) }
    vpfr:          { num_buckets (50) }
    funding:       { momentum_enabled (true), momentum_window (3),
                     momentum_threshold (0.0001), momentum_amplify (1),
                     momentum_soften (1) }
    oi_cvd_cross:  { enabled (true), upgrade_bonus (1), conflict_penalty (1) }
  session_volume:
    enabled: true
    sessions: [                      (ordered list; first hour match wins)
      { name: "ASIA",   start_hour: 0,  end_hour: 7,
        high_multiplier: 0.80, mid_multiplier: 0.85 },
      { name: "LONDON", start_hour: 8,  end_hour: 12,
        high_multiplier: 1.00, mid_multiplier: 1.00 },
      { name: "NY",     start_hour: 13, end_hour: 23,
        high_multiplier: 1.15, mid_multiplier: 1.10 }
    ]
  mtf_gate:        { enabled (true), candle_lookback (60),
                     adx_period (9), adx_min (20.0), min_of (2) }
  auto_run:        { enabled (false), interval_minutes (1), interval_seconds (0) }
  scoring:
    verdict_strong_pct (0.70) / verdict_med_pct (0.53) / verdict_weak_pct (0.35)
    funding_high_positive (0.0003) / funding_low_positive (0.00005)
    funding_high_negative (-0.0003) / funding_low_negative (-0.00005)
    bbw_squeeze_penalty (2)
    liq_standard_penalty (1) / liq_large_penalty (2)
    funding_high_penalty (2) / funding_high_boost (1) / funding_low_penalty (1)
    atr_target_multiplier (2.0) / atr_stop_multiplier (1.2)
    context_tag_structural_min (3) / context_tag_flow_max (1)
    hold_roc_take_profit_long/short (±0.6)
    hold_rsi_hold_long/short (60/40)
    hold_rsi_evaluate_long/short (40/60)
  kelly:
    account_size_usd (1000.0)
    use_half_kelly (true)
    max_risk_fraction (0.05)
    contract_face_usd (10.0)
    est_prob_floor (0.45)
    est_prob_scale (0.20)
  regime_gates:
    transitional_adx_penalty_low (20.0) / mid (22.5) / high (25.0)
    transitional_penalty_low (2) / mid (1)
  regime_weights:
    enabled: true
    trending:    { alignment_bonus (1), conflict_penalty (1) }
    range_bound: { alignment_bonus (1), conflict_penalty (1) }
```

**v15 cleanup notes.** The following keys existed in earlier `settings.json` files but had no consumer in the engine (silently ignored by the JSON deserialiser); removed in v15 with no behavioural change:

- `scoring.long_threshold` / `short_threshold` / `strong_long_threshold` / `strong_short_threshold` / `medium_long_threshold` / `medium_short_threshold` (superseded by `verdict_*_pct` since v0.30)
- `scoring.weights` block (`ScoringWeights` class deleted long ago)
- `scoring.transitional_penalty_enabled` (TRANSITIONAL ADX penalty applies unconditionally)
- `regime_gates.suppress_long_in_trending_down` / `suppress_short_in_trending_up` (Step 4 regime veto applies unconditionally)
- `indicators.EMA200` block (`CalcEMA(candles5m, 200)` is hardcoded)
- `indicators.OBV.lookback`, `indicators.CVD.trade_lookback`, `indicators.VWAP.dev_threshold_pct`, `indicators.VWAP.session1_start_hour` / `session1_start_minute`, `indicators.BBW.releasing_roc_threshold`, `indicators.Liquidations.long_liq_threshold` / `short_liq_threshold`, `indicators.ATR.ref_period`
- `mtf_gate.ema_period_fast` / `ema_period_slow` (`CalcMTFGate` hardcodes EMA 9/21/50)

Session volume norms scale volume thresholds by UTC session bucket so `VolHighThreshold` / `VolMidThreshold` are less likely to over-fire during thin Asian hours or under-react during London/NY participation peaks. The OI × CVD cross-confirm block lets Pass 2b be tuned independently of base OI/CVD scoring: aligned OI/CVD can earn an extra bonus, while full OI signals that directly oppose CVD can be penalised without changing the underlying indicator methods.

---

## 7. ScoringEngine — Key Behaviours

- **MaxScore:** base 19 (TRENDING), 18 (RANGE_BOUND), 15 (TRANSITIONAL). With RegimeWeights.Enabled (default), TRENDING → 20 and RANGE_BOUND → 19 (base + Trending/RangeBound AlignmentBonus). TRANSITIONAL unchanged.
- **Verdict thresholds:** `Math.Ceiling(regimeMax * pct)` — no hardcoded magic numbers
- **Step 2:** Score signals into ScoreState → all thresholds from cfg
- **Pass 2:** Upgrade partials on cross-category confirmation; OBV upgrade blocked on adverse divergence
- **Pass 2b:** OI × CVD cross-confirm gate — if OI full signal (or upgraded partial) and CVD direction+sign agree, apply `UpgradeBonus`; if full OI directly conflicts with CVD, apply `ConflictPenalty`; partial OI conflict is non-penalising
- **Pass 2c:** Regime alignment gate — suppressed in TRANSITIONAL and when LongScore=ShortScore. TRENDING checks EMA ribbon + ROC (active when Abs(ROC)≥SlopeSensitivity) + CVD slope+sign. RANGE_BOUND checks VWAP dev (active only outside warmup) + RSI(9) vs 50 + Donchian(20). All active aligned → `+AlignmentBonus` (capped at regimeMax). All conflict → `-ConflictPenalty`. RegimeMaxScore() adds the bonus to the ceiling when enabled so verdict % thresholds auto-adjust.
- **Step 3:** Baseline funding-rate modifier
- **Step 3b:** Funding-momentum modifier — can soften crowding penalty when momentum is falling, or amplify it when momentum is rising into crowding
- **Step 4:** Regime veto / TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate veto → NO TRADE
- **Step 4c:** VPFR HVN cap → sets AdjustedLongTarget / AdjustedShortTarget
- **Step 5:** Threshold comparison → verdict
- **Step 5b:** Verdict sub-context tag → sets VerdictContext (FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED). See `docs/verdict-context-tag-proposal.md`.
- **Step 6:** CalcHoldStatus (hold/exit/flip guidance for open positions)
- **Step 7:** ATR target/stop from cfg multipliers
- **CalcKellySizing():** called from RenderOutput after ATR levels are computed; populates Kelly fields on VerdictResult (display-only, zero scoring impact). See `docs/kelly-criterion-proposal.md`.

For the full annotated Calculate() pipeline with per-step implementation detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`
- **Last transacted price** = `recentTrades(0).Price` — displayed above ATR block, not used as entry
- Long: Stop = price − (ATR × scale × AtrStopMultiplier), Target = price + (ATR × scale × AtrTargetMultiplier)
- Short: mirrored. R:R = 1:1.7 at current settings (1.2 stop / 2.0 target)
- **HVN cap:** if `v.AdjustedLongTarget > 0` (or Short), raw target shown dimmed; POC-capped target shown in amber bold with reason
- **Multipliers read from cfg** — label and R:R display are dynamic, not hardcoded.
- **Kelly Sizing block** rendered immediately after ATR levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. Display carries an advisory label noting the R:R is ATR-basis, not structural. EST mode only — CAL mode will be reinstated after the backtesting module is built. Suppressed when KellyF = 0 (no edge).
- **Funding display** now includes both the raw rate/bias row and a separate momentum row showing `RISING` / `FALLING` / `FLAT` plus enabled/soften/amplify config values.

---

## 9. Open Position Guidance (CalcHoldStatus)

Priority order: (1) 2+ adverse microstructure signals → fast EXIT; (2) OBV divergence exit;
(3) RSI divergence evaluate; (4) single adverse microstructure warning; (5) RSI/ROC structural assessment.
All RSI/ROC thresholds read from cfg (`HoldRoc*`, `HoldRsi*` fields).

---

## 10. CSV Logging & Auto-Run

- `AnalysisLogger.LogRun(r, verdict)` → `analysis_log.csv` in exe directory
- `CalibrationReport` summarises recent directional accuracy
- Auto-run timer driven by `MainForm_AutoRun.vb`; interval configurable from UI (min 10s)
- Funding-momentum is currently **display/scoring only**; no dedicated CSV column has been added yet.
- OI × CVD Pass 2b is currently reflected in the **`OI Delta` breakdown note** but has no dedicated CSV column yet.

---

## 11. DynamicNorms

`DynamicNorms.Compute(candles1m, r.ATR)` computes per-run:
- `ATRScaleFactor` — current ATR vs reference; scales stop/target distances
- `VolHighThreshold` / `VolMidThreshold` — regime-adjusted volume thresholds
- `VWAPDevThreshold` — dynamic VWAP deviation threshold (clamped from settings)
- `ApplySessionVolume()` — session-aware post-adjustment that applies ASIA / LONDON / NY bucket multipliers from `SessionVolumeSettings` so volume thresholds better reflect expected liquidity by UTC session

---

## 12. WATCHING / Calibration Backlog

| Item | Description | Priority |
|---|---|---|
| Funding momentum thresholds | Review `momentum_threshold=0.0001`, `momentum_window=3`, soften/amplify values after 50+ live runs; especially check whether BTC funding changes are too sticky for 3-sample lookback | Low |
| Session volume multipliers | Review ASIA / LONDON / NY `vol_high_mult` and `vol_mid_mult` values after 50+ live runs; verify reduced false positives in thin hours without underweighting genuine expansion | Low |
| OI × CVD gate tuning | Review `upgrade_bonus=1` and `conflict_penalty=1` after 50+ live runs; confirm whether full-signal conflict is strong enough and whether upgraded partial OI confirmations improve hit rate without over-rewarding covering/capitulation cases | Low |
| TFI threshold | Evaluate threshold=0.15 vs 0.10 for BTC-PERPETUAL tick size after live data | Low |
| MicroCVD accelThreshold | Default raised to 10000 USD in v14 (was 5000); consider dynamic scaling vs VolumeSMA on quiet sessions | Low |
| AtrTargetMultiplier | Currently 2.0; review against logged R:R after 50+ trades | Low |
| OFI ratio | BuyDominantRatio=3.0 / SellDominantRatio=0.333; review against OFI hit rate in CalibrationReport | Low |
| TTM flatThreshold | Default 0.5; review FLAT vs RISING/FALLING against 1m candle range distribution | Low |
| VPFR numBuckets | Default 50; higher = more POC resolution at cost of sparse buckets on quiet sessions | Low |
| Liq dominanceRatio | Default 2.0; review false signals; consider raising/lowering after live observations | Low |
| ContextTag thresholds | ContextTagStructuralMin (3) / ContextTagFlowMax (1) — review FLOW_UNCONFIRMED hit rate after 50+ trades | Low |
| Kelly est_prob_floor/scale | Default 0.45 / 0.20 — review against actual win rates once CalibrationReport reaches READY | Low |
| VerdictContext CSV logging | When CalibrationReport approaches READY (≥300 rows, ≥3 sessions, ≥3 regimes): add `VerdictContext` column to `analysis_log.csv` in `AnalysisLogger.LogRun()`, and update CalibrationReport to correlate each context tag with subsequent directional accuracy. | Low — deferred until CalibrationReport READY |
| FundingMomentum CSV logging | If funding-momentum proves useful, add `FundingMomentum` and maybe raw delta/history depth to CSV for post-run validation of Step 3b effectiveness. | Low |
| OI×CVD CSV logging | If Pass 2b proves decision-useful, add explicit `OiCvdPass2bOutcome` / bonus / penalty columns so confirmation-vs-conflict effects can be validated directly from `analysis_log.csv` instead of inferred from breakdown text. | Low |

---

## 13. Future Upgrades

Ranked by expected accuracy / reliability gain. Items marked ✅ are approved for implementation
when the backlog is clear. Items marked 🔍 require a spec decision before coding begins.

### High-Impact (still meaningful gains — implement next)

| Item | Description | Status |
|---|---|---|
| **Verdict Sub-Context Tag** | Adds a Step 5b `CalcVerdictContext()` pass that classifies FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED. Displayed as `CONTEXT:` line — always shown (green for CONFIRMED, amber/red/dim for warnings). No scoring changes. **Spec:** `docs/verdict-context-tag-proposal.md` | ✅ IMPLEMENTED 2026-04-14 |
| **Kelly Criterion Sizing** | Display-only position sizing advisory block below ATR entry levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. EST mode only — CAL mode will be reinstated after the backtesting module is built. Display carries an advisory label noting the R:R is ATR-basis, not structural. No scoring changes. **Spec:** `docs/kelly-criterion-proposal.md` | ✅ IMPLEMENTED 2026-04-14 |
| **Funding rate momentum** | Funding momentum now implemented end-to-end: `FundingMomentum` field on `IndicatorResults`, `CalcFundingMomentum()` in `Indicators_OrderFlow`, funding history accumulation in `MainForm_Analysis`, config surface in `EngineSettings` and `settings.json`, Step 3b modifier in `ScoringEngine_Calculate_Scoring`, and UI display row in `MainForm_Render_Sections`. | ✅ IMPLEMENTED 2026-04-20 |
| **Session-aware volume norms** | `DynamicNorms` now applies UTC session buckets (ASIA / LONDON / NY) via `ApplySessionVolume()`, backed by `SessionVolumeSettings` in `EngineSettings` and `session_volume` in `settings.json`, so `VolHighThreshold` / `VolMidThreshold` adapt to time-of-day liquidity instead of using a single global expectation. | ✅ IMPLEMENTED 2026-04-21 |
| **OI × CVD cross-confirm** | `RunScoringPipeline()` now includes Pass 2b after partial upgrades: when OI and CVD direction/sign confirm, the relevant side gets `UpgradeBonus`; when full OI directly conflicts with CVD, the relevant side gets `ConflictPenalty`; upgraded partial OI signals can confirm but partial conflict does not penalise. Backed by `OiCvdSettings` / `indicators.oi_cvd_cross`, and surfaced in the `OI Delta` breakdown note. | ✅ IMPLEMENTED 2026-04-21 (docs sync) |
| Adaptive scoring weights by regime | Regime-aware alignment bonus/penalty implemented as Pass 2c in `RunScoringPipeline()`. Spec: `docs/adaptive-regime-weights-proposal.md`. Per-indicator weight multipliers remain static; only a single alignment/conflict scalar is applied per regime. A full per-indicator weighting scheme would require a further spec. | ✅ IMPLEMENTED 2026-04-21 (Pass 2c) |

### Moderate-Impact (diminishing returns territory)

| Item | Description | Status |
|---|---|---|
| Dynamic MicroCVD accelThreshold | Static 10000 USD threshold (v14) is noise during high-volume sessions and a too-high bar during quiet hours. Scale dynamically against total window USD flow with a static-anchored floor. | ✅ Specced — see `docs/dynamic-microcvd-accel-proposal.md` |
| RSI divergence on 5m candles | Current divergence is 1m only. A confirmed divergence on both 1m and 5m simultaneously would be a stronger penalty signal and reduce false penalties on 1m micro-noise. Requires `CalcRSIDivergence` called on `candles5m` and a combined gate in scoring pipeline. | 🔍 Deferred — see `docs/post-websocket-post-calibration-backlog.md` D3 |
| Donchian × BBW state cross-reference | Wide channel breakout is meaningfully different from a tight-channel breakout. Cross-reference BBW squeeze state (ACTIVE / RELEASING / NONE) when scoring Donchian to up-weight breakouts from compression. | 🔍 Deferred — see `docs/post-websocket-post-calibration-backlog.md` D4 |

### Fine-Tuning (marginal gains, run after calibration data available)

| Item | Description | Status |
|---|---|---|
| Bid-ask spread microstructure signal | `orderBook` depth is already fetched. Spread between best bid and best ask is an unused fast microstructure signal — sudden widening often precedes a flush. Add `SpreadBps` to `IndicatorResults` and a penalty trigger in Tier 2. | ✅ Specced — see `docs/bid-ask-spread-proposal.md` |
| Auto-tuning from CSV log | Once `CalibrationReport` reaches READY (≥300 rows, ≥3 sessions, ≥3 regimes, ≥2 liq events), build a pass that correlates each signal's vote with subsequent price direction and adjusts `settings.json` weights automatically. | 🔍 Requires calibration data first; see also Section 16 (frontier-LLM auto-tweaking is the longer-arc plan) |

### Active Specs Awaiting Implementation (2026-04-27)

Five indicator/feature specs and one settings-exposure pass were drafted on 2026-04-27. All are **PROPOSED** status pending user approval; flip to `APPROVED` in the file header before implementing.

| Spec | Scope | Implementation Order |
|---|---|---|
| `docs/bid-ask-spread-proposal.md` | New SpreadBps + WIDE-spread entry-side penalty. Smallest, validates the workflow | 1 |
| `docs/ofi-momentum-proposal.md` | OFIMomentum (RISING/FALLING/FLAT) modifier on the existing OFI level signal. Pattern matches FundingMomentum | 2 |
| `docs/dynamic-microcvd-accel-proposal.md` | Self-scaling MicroCVD acceleration threshold (`totalWindowUsd × pct` with static-anchored floor). Closes the noise/quiet-session classification gap. Small spec, low risk | 3 |
| `docs/vpfr-lite-v2-proposal.md` | VAH/VAL + nearest HVN/LVN walls. Updates Step 5b cap arbitration. Required by swing-pivot-proposal | 4 |
| `docs/swing-pivot-proposal.md` | 5m + 15m swing structure. Adds structural row to ATR display, 3-tier Step 5b cap, structural-break exit in CalcHoldStatus, sharper STRUCTURALLY_WEAK | 5 |
| `docs/settings-exposure-pass-proposal.md` | Lift 19 hardcoded scoring constants to `settings.json` (RegimeMaxScore base values, TierFloor breakpoints, VerdictContext thresholds, BBW/TTM/CVD/Donchian internals, Pass 2c RSI midline). No behavioural change — closes the audit prerequisite for Section 16 auto-tweaking | 6 (after the five indicator/feature specs) |

Deferred items (need WebSocket migration or CalibrationReport READY) are recorded in `docs/post-websocket-post-calibration-backlog.md` so they survive context-window rollovers.

### Accuracy Ceiling Note

The engine is approaching its natural accuracy limit for a **single-instrument 1m scalping system
using REST polling**. By the time a signal is computed, the 1m candle is closed and partially
acted on by faster participants. The three risk thresholds to watch before adding more upgrades:

1. **Overfit risk** — the number of tunable parameters continues to rise, so session-bucket multipliers should be validated against forward runs, not a tiny historical slice.
2. **Signal redundancy** — OFI + TFI + CVD + MicroCVD already cover order flow from four angles. Session-aware volume norms should remain a threshold-normalisation layer, not become a hidden directional signal.
3. **Interpretability** — session volume adjustments should remain easy to reason about from DynamicNorms and settings. If traders cannot quickly tell which bucket is active and why a threshold moved, the engine becomes harder to trust.

The highest-impact non-code upgrade at this stage is a **Websocket feed** (real-time order book
and trade stream vs. REST snapshot polling), which would remove the fundamental latency constraint.

---

## 14. Backlog

*(cleared — verdict-context, Kelly sizing, funding-momentum, session-volume-norms, and OI×CVD cross-confirm are shipped; remaining items are calibration review or future spec work)*

---

## 15. Version History

| Version | Key Changes |
|---|---|
| **2026-04-27** | [cleanup pass] `settings.json` bumped v14 → v15. No behavioural change; strips silently-ignored keys and unused config fields so the file matches the engine's actual surface area. **Code:** removed dead fields `IndicatorResults.OI_Prev15m`/`OI_Prev60m`/`ATRAvg20d`; removed three never-called `DynamicNorms.StaticVol*` properties; deleted `Ema200Settings` (entire class) and dead properties on `VwapSettings` (`DevThresholdPct`, `Session1StartHour`, `Session1StartMinute`), `BbwSettings` (`ReleasingRocThreshold`), `ObvSettings` (`Lookback`), `AtrSettings` (`RefPeriod`), `LiquidationSettings` (`LongLiqThreshold`, `ShortLiqThreshold`), `CvdSettings` (`TradeLookback`), `MTFGateSettings` (`EmaPeriodFast`, `EmaPeriodSlow`), `ScoringSettings` (`TransitionalPenaltyEnabled`), `RegimeGateSettings` (`SuppressLongInTrendingDown`, `SuppressShortInTrendingUp`); removed dead `Public Const ScoringEngine.MaxScore = 19` (replaced by `RegimeMaxScore()` long ago) and unused `SettingsLoader.Reload()`. Aligned remaining default values with v14 calibration so an absent `settings.json` no longer spawns stale defaults. **Streamlining:** extracted `IndicatorEngine.GetSessionCandles()` to dedupe the VWAP session-boundary calculation between `CalcVWAP` and `CalcVWAPBands`; cleaned a meaningless dummy initialiser in `CalcKellySizing`. **Bug fixes:** `MainForm_Render_Sections` BBW status colour compared against `"SQUEEZE"` (indicator emits `"ACTIVE"` / `"RELEASING"` / `"NONE"`) so the warn colour never fired; TTM direction colour compared against `"UP"` / `"DOWN"` (indicator emits `"RISING"` / `"FALLING"` / `"FLAT"`) so the green/red tints never fired. Both display-only — zero scoring impact. |
| **2026-04-22** | [calibration pass] `settings.json` bumped v13 → v14. Value-only tuning sweep (no schema changes): OFI `buy/sell_dominant_ratio` 3.0/0.333 → 2.0/0.5; RSI `partial_overbought/partial_oversold` 50/50 → 55/45; `funding_high_positive/negative` ±0.0001 → ±0.0003; CVD `slope_min_usd` 1000 → 12000; Volume `dynamic_high_clamp_min`/`dynamic_mid_clamp_min` 1.5/1.2 → 2.0/1.5; ATR `static_ref` 150 → 115; MicroCVD `accel_threshold` 5000 → 10000; Kelly `account_size_usd` confirmed 1000 (placeholder pending trader input). Also added funding-history dedup in `MainForm_Analysis.vb`: `_fundingHistory` now only appends when the funding rate actually changed from the previous sample, so Step 3b momentum is computed over genuinely distinct values rather than 1m snapshots of a rate that only updates every 8h. |
| **2026-04-22** | [defect fixes — Batch 1] M1: MTF-gate JSON-to-POCO key binding repaired. `EngineSettings.MTFGateSettings` JsonPropertyNames renamed `candle_count` → `candle_lookback`, `dmi_period` → `adx_period`, `required_confirms` → `min_of`, and new `AdxMin` property added (`"adx_min"`, default 20.0). `settings.json` mtf_gate block trimmed to 5 keys (dropped `mode` / `block_action` / `timeframe_minutes` — none had code consumers). `MainForm_Analysis.vb` call site now wires `adxMin:=cfg.MTFGate.AdxMin` (was silently borrowing `cfg.Indicators.ADX.TrendThreshold` = 25, so the 15m soft gate now actually uses the intended 20). M2: Kelly display carries a two-line advisory label immediately below the `KELLY SIZING` header — "Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets. Treat as directional bias indicator only." No suppression logic changed; block still renders at every verdict level where `KellyPWin > 0`. M3: CAL-mode dead code removed. `MinCalibrationSamples` property deleted from `EngineSettings.KellySettings`; `min_calibration_samples` key removed from `settings.json`; `[{0}]` PMode tag removed from both `KELLY SIZING` UI headers (`[CAPPED]` retained). `KellyPMode` still assigned `"EST"` internally and will be reinstated when a backtesting module ships empirical per-tier win rates. |
| **2026-04-21** | [adaptive-regime-weights] Pass 2c regime-alignment gate shipped. Added `RegimeWeightSettings` to `EngineSettings`; added `regime_weights` top-level block to `settings.json` (v13). `RegimeMaxScore()` now takes `cfg` and returns baseMax + AlignmentBonus for TRENDING/RANGE_BOUND when enabled, so TRENDING ceiling goes 19→20 and RANGE_BOUND 18→19 by default. Verdict % thresholds auto-adjust. Spec: `docs/adaptive-regime-weights-proposal.md`. |
| **2026-04-21** | Documentation sync: OI × CVD cross-confirm marked as shipped after code review. Handover updated to note Pass 2b in `RunScoringPipeline()`, add `OiCvdSettings` / `indicators.oi_cvd_cross` to config surface, update future upgrades/backlog status, and add calibration/logging follow-up notes for the feature. |
| **2026-04-21** | Session-volume-norms feature fully documented as shipped. Handover updated to settings v12, `DynamicNorms.ApplySessionVolume()` note added, `SessionVolumeSettings` and `session_volume` config documented, and Section 13 status updated to mark session-aware volume norms as implemented. |
| **2026-04-20** | Refactor split completed: `Core/ScoringEngine_Calculate.vb` replaced by `ScoringEngine_Calculate_Scoring.vb` + `ScoringEngine_Calculate_Verdict.vb`; `UI/MainForm_Render.vb` replaced by `MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb`. Docs updated to reflect new structure. |
| **2026-04-20** | Funding-momentum feature fully shipped. Added `FundingMomentum` to `IndicatorResults`; added `CalcFundingMomentum()` in `Indicators_OrderFlow`; added `_fundingHistory` / `FundingHistoryMax` in `MainForm_Layout`; appended funding history + computed momentum in `MainForm_Analysis`; added Step 3b funding-momentum modifier in `ScoringEngine_Calculate_Scoring`; added `FundingSettings` to `EngineSettings`; added `indicators.funding` block to `settings.json` v10; added funding momentum row to `MainForm_Render_Sections`. |
| **2026-04-20** | `settings.json` updated to v10 with `indicators.funding` block: `momentum_enabled`, `momentum_window`, `momentum_threshold`, `momentum_amplify`, `momentum_soften`. |
| **2026-04-14** | [UI] CONTEXT: line now always rendered — CONFIRMED shown in green (C_GOOD) instead of being silent. Removes ambiguity between "no tag" and "confirmed aligned". `MainForm_Render.vb` bumped to v0.49. |
| **2026-04-14** | Kelly Criterion sizing fully implemented: `CalcKellySizing()` in `ScoringEngine_Calculate.vb`; Kelly fields on `VerdictResult`; `KellySettings` in `EngineSettings`; KELLY SIZING block rendered in `MainForm_Render.vb` after ATR levels. `[CAPPED]` tag retained; CAL mode removed (EST only until backtesting ships). Half-Kelly, 5% cap, $1,000 account, $10 contract face. |
| **2026-04-14** | Verdict Sub-Context Tag implemented: Step 5b `CalcVerdictContext()` added to `ScoringEngine_Calculate.vb`; `VerdictContext` property on `VerdictResult`; `CONTEXT:` line rendered in `MainForm_Render.vb`; settings.json bumped to v7 (`context_tag_structural_min`, `context_tag_flow_max`). CSV logging deferred to CalibrationReport READY — see Section 12 backlog. |
| **2026-04-13** | ATR label fix: stop/target multipliers read from cfg in RenderOutput (no hardcoded 1.5/3.0). atr_stop_multiplier updated to 1.2, atr_target_multiplier confirmed 2.0. Verdict Sub-Context Tag spec committed (`docs/verdict-context-tag-proposal.md`). |
| **Commit 5** | [T2-C] Donchian NONE mid-channel note. [T3-A] VPFR numBuckets from cfg. [T3-B] RSI pivotWing + lookbackBars from cfg. [T3-C] TTM flatThreshold from cfg. [T3-D] CalcLiquidations dominanceRatio from cfg. |
| **Commit 4** | [T1-B] Regime ADX hysteresis 1-bar grace (`_prevRegime`). [T2-A] MicroCVD FLAT stall penalty. [T2-B] OFI BookDepth injectable; dynamic descending weight array. |
| v0.49 | [P8] RSI zones/div penalty → cfg. [P9] ADX threshold + VWAP warmup → scoring. [P10] ROC dead-band + OFI dominance → cfg. [P11] ATR multipliers externalised. [P12] BBW/Liq/Funding penalties externalised. EngineSettings v0.37. settings.json v6. |
| v0.48 | [P4] TFI window separated from MicroCVD. TfiSettings + MicroCvdSettings added (EngineSettings v0.36). |
| v0.47 | [P1] MTF TTL cache. [P2] RSI div penalty. [P3] CVD 3-seg slope. [P4] Donchian quartile. [P5] volMid partial. [P6] OBV div block. [P7] VPFR exp decay. |
| v0.46 | RenderOutput refactor; VPFR HVN target cap display; last transacted price block |
| v0.45 | MicroCVD sign-aware penalty; CVD divergence penalty fix |
| v0.44 | VPFR-lite HVN cap in ScoringEngine; AdjustedLongTarget/ShortTarget |
| v0.43 | CalcVPFRLite added; POC proximity scoring |
| v0.42 | OBV adverse divergence gate; cross-category upgrade logic |
| v0.41 | Donchian quartile signal scaffolding |
| v0.40 | DynamicNorms volume thresholds; volMid partial scoring |
| v0.39 | Dual-session VWAP; warmup guard |
| v0.38 | MicroCVD 3-segment; BULL/BEAR_ACCEL/DECEL |
| v0.37 | CalcRSIDivergence added |
| v0.36 | AutoRunSettings added |
| v0.35 | Auto-run timer UI |
| v0.34 | MTFGate RSI fields removed |
| v0.33 | MTFGateSettings + CalcMTFGate + 15m TTL fetch |
| v0.32 | VWAP session timing in settings |
| v0.31 | CVDSettings in EngineSettings |
| v0.30 | RSI div gates; OBV gates; ScoringWeights |

---

## 16. Future Direction — Auto-Tweaking & Dual Interface

This section documents the longer-arc plans for the engine. **All items here are KIV** while the team is in the accuracy-optimization phase (the four indicator specs + settings-exposure pass listed in Section 13 ship first). Recording them here so the architectural prerequisites stay visible and groundwork can be laid opportunistically when other work touches relevant code paths.

### 16.1 Auto-Tweaking via Frontier-LLM API

**Purpose.** When the engine's analysis fails to predict outcomes successfully at a defined rate, the engine sends `settings.json` (plus a window of the recent `analysis_log.csv`) to a frontier-LLM API. The model returns a tweaked `settings.json` which replaces the local file; subsequent runs use the new tuning. This is the long-arc replacement for the manual calibration sweeps recorded in `change_log` — automated, data-driven, runs unattended.

**Trigger condition (TBD — needs spec).** Failure rate within a sliding window of analyses. Open questions for that future spec:

- **What counts as "failure"?** Candidates: STRONG verdict followed by adverse 5–15 min price action; NO TRADE that would have been a clean STRONG; WEAK verdict that whipped within hold window. Each definition implies different CSV columns and different trigger thresholds.
- **Window size.** 50 analyses? 200? Calibration-bound.
- **Failure-rate threshold.** 25%? 40%? Calibration-bound.
- **Cooldown.** Minimum interval between auto-tweaks to avoid thrash. Probably tied to CalibrationReport row counts.

**Audit prerequisite.** Every scoring-affecting parameter must be reachable through `settings.json`. This is the explicit gate. The `settings-exposure-pass-proposal.md` spec (Section 13) closes the gap; until it ships, the engine has hardcoded scoring constants the tweaker cannot reach (`RegimeMaxScore`, `TierFloor`, `VerdictContext` thresholds, several indicator internals). Do **not** deploy auto-tweaking until that pass is complete.

**Display-only fields are NOT exposed for tweaking.** The audit explicitly excludes display-only display variables (e.g., colour palettes, render formatting, calibration-report thresholds, MTF cache TTL). The tweaking surface is scoring decisions only.

**Status.** KIV. Not specced. Not scheduled.

### 16.2 Dual Interface — CLI (Linux) + WinForm (Windows)

**Purpose.** Two host targets sharing the same engine code:

- **CLI (Linux server, headless)** — runs on a remote host, drives the auto-tweaking loop unattended. Polls Deribit, writes CSV, runs analysis on a schedule, evaluates failure rate, calls the LLM tweak API when triggered. No UI, no WinForms dependencies.
- **WinForm (Windows desktop)** — the existing manual-usage interface. User clicks Analyse Now, reads the rendered output, makes discretionary decisions.

**Architecture implications — already partly satisfied.** The codebase has been moving toward host-agnostic core for some time:

- `IAutoRunTimer` interface in `AutoRunTimer.vb` was explicitly designed for this — `WinFormsAutoRunTimer` is the WinForms implementation; a future `LinuxCliAutoRunTimer` would implement the same interface without `Control.Invoke` marshalling. Documented in the file header.
- Scoring engine (`Core/ScoringEngine_*.vb`), indicators (`Core/Indicators_*.vb`), settings (`Core/Settings/*.vb`), and the `DeribitClient` are **already host-agnostic** — pure functions and HTTP calls, no WinForms dependency.
- `DynamicNorms`, `AnalysisLogger`, `OiSnapshot` are similarly clean.

**What still needs work for CLI:**

- Output rendering. `MainForm_Render_*.vb` is RTF-based (RichTextBox.AppendText). A CLI host needs a parallel renderer (likely ANSI-coloured plaintext or structured JSON for the auto-tweak feed).
- State plumbing. `_oiHistory`, `_fundingHistory`, `_ofiHistory` (when OFI momentum ships), MTF cache, `_prevRegime` all live as `Private` fields on `MainForm`. A CLI host needs an equivalent state container — likely a small `EngineState` class shared by both hosts. This is a future refactor, not in scope today.
- Settings I/O. `SettingsLoader` is already host-agnostic (uses `System.IO`, `System.Text.Json`, `System.Threading`). No change needed.
- Auto-run scheduling. `WinFormsAutoRunTimer` uses `Control.Invoke`; the CLI variant uses straight `System.Threading.Timer` callbacks. Already designed for this split.

**Status.** KIV. The architectural groundwork (interface separation, host-agnostic core) is **opportunistically maintained** — when any change touches `MainForm_*.vb`, the engineer should resist adding new WinForms-only state into the partial class. State that's needed by both hosts goes into a host-agnostic class. The forthcoming OFI momentum ring buffer is a good test case: its natural location per existing pattern is `MainForm_Layout._ofiHistory`, but if a follow-up CLI port is anticipated, parking it on a shared `EngineState` instead is the more durable choice. Decide per-PR.

### 16.3 KIV Prerequisites

Before either 16.1 or 16.2 should be specced and scheduled:

1. **Accuracy plateau.** The four indicator specs (bid-ask-spread, OFI momentum, VPFR-lite v2, swing pivots) ship and the engine's verdict accuracy stabilises across 100+ live runs. Auto-tweaking before the engine is structurally sound just optimises noise.
2. **Settings exposure pass complete.** `settings-exposure-pass-proposal.md` shipped. Auto-tweaker has the full surface area to operate on.
3. **CalibrationReport READY.** ≥300 rows, ≥3 sessions, ≥3 regimes covered, ≥2 liquidation events. Without this, the failure-rate trigger is computed on too small a sample to be trustworthy.
4. **CSV columns expanded.** `VerdictContext`, `FundingMomentum`, `OiCvdPass2bOutcome` columns added (currently in Section 12 backlog). The auto-tweaker reads these to diagnose failure modes.
5. **Failure definition specced.** Concrete answer to "what counts as failure" — see Section 16.1 open questions.

Items 1–4 are tracked in active proposals or Section 12 backlog. Item 5 is the gating spec for 16.1 itself.

### 16.4 Long-Arc Architectural Ceiling

`docs/post-websocket-post-calibration-backlog.md` documents the architectural ceiling for the engine: **WebSocket migration** is the single highest-impact non-indicator upgrade, gating an entire class of microstructure improvements (real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip detection, fine-grained VPFR profile-shape work).

WebSocket itself isn't documented as a future-direction item *here* because it's a foundation rebuild, not a feature addition. It's the binding latency constraint behind several Section A items in the backlog — fix it when (a) the indicator backlog is exhausted and the latency floor becomes the next bottleneck, **or** (b) one of the post-WebSocket items becomes a priority.

The engine should be developed assuming WebSocket arrives eventually. Specifically: avoid hardcoded REST-cadence assumptions in scoring logic. Score thresholds should remain meaningful at higher poll rates. They currently are.
