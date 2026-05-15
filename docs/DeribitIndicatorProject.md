# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-05-15 | Current version: settings.json v27 — OHLC cache gap-backfill on startup. Layered on top of v26 (live-performance-display strip), the settings-snapshot-history bundle, Bundle 2 (auto-tweaker), Bundle 3 (d1 trend structure + d2 volume-weighted pivots). Bundle 1 (csv-expansion-v0.4 + analysis script) shipped 2026-05-05.**

This document is the authoritative handover for any new AI conversation continuing this project.

**Session start checklist:** Read this file + `docs/architecture.md`. Do NOT read individual `.vb` files unless a specific edit is required.

> ⛔ **PROHIBITED:** - NEVER call load_skill() under any circumstances during this project. It is not relevant to this codebase and consumes context budget without benefit.

---

## 1. Project Purpose

A Windows Forms (VB.NET / .NET 8) desktop application that connects to the Deribit REST API,
calculates a set of technical indicators on live BTC-PERPETUAL data, scores them via a
weighted multi-tier engine, and emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE /
WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

The latest completed feature set (v16–v17) adds six indicator and configuration enhancements
shipped across two sessions:

- **Bid-ask spread microstructure signal** (Spec #1) — `SpreadBps` from the live order book is
  scored as an entry-side penalty on WIDE spread (Tier 2). Prevents entering during flush events.
- **OFI Momentum** (Spec #2) — `OFIMomentum` (RISING/FALLING/FLAT) modifier on the existing OFI
  level signal. Pattern matches FundingMomentum: ring buffer in `MainForm_Layout._ofiHistory`.
- **Dynamic MicroCVD accelThreshold** (Spec #3) — self-scaling threshold (`totalWindowUsd × pct`
  with static-anchored floor) closes the noise/quiet-session classification gap.
- **VPFR-lite v2** (Spec #4) — adds VAH/VAL, nearest HVN/LVN walls, and `VPFRNearestHvnAbove` /
  `VPFRNearestHvnBelow` fields used by the 3-tier target cap.
- **Swing pivot detection** (Spec #5) — `CalcSwingPivots()` on 5m (primary) and 15m (context)
  candles. Adds structural entry/stop/target rows to the ATR display. Layer 1.5 structural-break
  exit in `CalcHoldStatus`. 3-tier target cap in Step 5b: swing target → nearest HVN → POC.
  `STRUCTURALLY_WEAK` context tag fires when swing data exists but no clean target+stop pair.
- **Settings exposure pass** (Spec #6) — lifts 19 hardcoded scoring literals to `settings.json`.
  Closes the audit prerequisite for Section 16 auto-tweaking. No behaviour change at defaults.

The shipping base also includes **OI × CVD cross-confirm** (Pass 2b), **session-aware volume
norms** (DynamicNorms + `session_volume` config), and a Pass 2c **regime-alignment gate**. These
are all part of the scoring pipeline and described in detail below.

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
| `settings.json` | v18 — all tunable parameters incl. `indicators.funding`, `indicators.oi_cvd_cross`, `session_volume`, `regime_weights`, `indicators.swing`, scoring sub-blocks, and new `network` block (v18). v15 was a cleanup pass; v16 added swing pivot config; v17 lifted 19 hardcoded literals to settings; v18 added API resilience config. |
| `MainForm.Designer.vb` | Auto-generated WinForms designer (do not edit) |
| `MainForm.resx` | Form resources |

### Core/

| File | Purpose |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. AdjustedLongTarget, AdjustedShortTarget, TargetCapReason, VerdictContext, Kelly fields), PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore (reads cfg `scoring.regime_max_score`), Threshold, TierFloor (reads cfg `scoring.tier_floor`), AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus (Layer 1 microstructure, **Layer 1.5 structural-break exit**, Layer 2 OBV divergence, Layer 3 RSI/ROC) |
| `Core/ScoringEngine_Calculate_Scoring.vb` | `AppendLean()`, `CalcVerdictContext()` (incl. swing-data structural-target check), `RunScoringPipeline()` — Steps 2 / Pass 2 / Pass 2b / Pass 2c / 3 / 3b: signal scoring, partial upgrades, OI×CVD cross-confirm, regime alignment, funding modifiers, breakdown note rows |
| `Core/ScoringEngine_Calculate_Verdict.vb` | `Calculate()` entry point — Step 4 regime veto / TRANSITIONAL penalty, Step 4b MTF gate veto, Step 5 verdict generation, **Step 5b 3-tier target cap** (swing target → nearest HVN → POC) |
| `Core/ScoringEngine_Kelly.vb` | `CalcKellySizing()` — display-only Kelly sizing. Called from `MainForm_Render_Header`, not from `ScoringEngine.Calculate()`. Zero scoring impact. See `docs/kelly-criterion-proposal.md`. |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields incl. `FundingMomentum`, `SpreadBps`, `OFIMomentum`, VPFR-v2 fields (`VPFRNearestHvnAbove`, `VPFRNearestHvnBelow`, `VPFRVAH`, `VPFRVAL`), and swing pivot fields (`LastSwingHigh5m`, `LastSwingLow5m`, `LastSwingHigh15m`, `LastSwingLow15m`, `SwingTargetLong/Short`, `SwingStopLong/Short`) |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcRSI, CalcRSISeries, CalcRSIDivergence, CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW (seriesWindowMultiplier + squeezePercentile from cfg), CalcTTMSqueeze (smaPeriod + linRegPeriod from cfg) |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcOFIMomentum, CalcLiquidations, CalcCVD (lateSegmentWeight + earlySegmentWeight from cfg), CalcMicroCVD (dynamic accelThreshold), CalcTFI, CalcFundingMomentum |
| `Core/Indicators_Structure.vb` | CalcDonchian (quartilePct from cfg), CalcOBV, CalcVPFRLite v2 (VAH/VAL + nearest HVN/LVN, exp decay), **CalcSwingPivots** (5m + 15m confirmed pivot scan), CalcMTFGate |
| `Core/Settings/EngineSettings.vb` | Strongly-typed POCO for settings.json incl. KellySettings, FundingSettings, OiCvdSettings, SessionVolumeSettings, RegimeWeightSettings, **SwingSettings**, **RegimeMaxScoreSettings**, **TierFloorSettings**, **ContextTagThresholds** |
| `Core/Settings/SettingsLoader.vb` | JSON deserialisation, SettingsLoader.Current singleton, FileSystemWatcher hot-reload |

### UI/

| File | Version | Purpose |
|---|---|---|
| `UI/MainForm_Layout.vb` | Shared fields, constructor, resize helpers; owns `_fundingHistory` (FundingHistoryMax=10) and **`_ofiHistory`** (OFIHistoryMax=10) |
| `UI/MainForm_AutoRun.vb` | Auto-run timer lifecycle |
| `UI/MainForm_Analysis.vb` | RunAnalysisAsync() — full data fetch + indicator + scoring pipeline; appends funding history and computes `FundingMomentum` |
| `UI/MainForm_Render_Header.vb` | RTF helpers, CalibrationReport/log helpers, and `RenderOutputHeader()` for VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME / LAST PRICE / HOLD STATUS / ATR levels / **Long+Short structural rows (swing pivot R:R)** / KELLY block |
| `UI/MainForm_Render_Sections.vb` | `RenderOutput()` entry point + all indicator sections, funding section, signal breakdown table, verdict label update |

### Docs

| File | Purpose |
|---|---|
| `docs/DeribitIndicatorProject.md` | This handover document |
| `docs/architecture.md` | Codebase structure, data flow, design decisions |
| `docs/trader-profile.md` | Trader style, indicator preferences, collaboration preferences |
| `docs/verdict-context-tag-proposal.md` | Spec: Verdict Sub-Context Tag — ✅ IMPLEMENTED |
| `docs/kelly-criterion-proposal.md` | Spec: Kelly Criterion position sizing — ✅ IMPLEMENTED |
| `docs/bid-ask-spread-proposal.md` | Spec: Bid-ask spread microstructure signal — ✅ IMPLEMENTED |
| `docs/ofi-momentum-proposal.md` | Spec: OFI Momentum modifier — ✅ IMPLEMENTED |
| `docs/dynamic-microcvd-accel-proposal.md` | Spec: Dynamic MicroCVD accelThreshold — ✅ IMPLEMENTED |
| `docs/vpfr-lite-v2-proposal.md` | Spec: VPFR-lite v2 (VAH/VAL + nearest HVN/LVN) — ✅ IMPLEMENTED |
| `docs/swing-pivot-proposal.md` | Spec: Swing pivot detection (5m + 15m) — ✅ IMPLEMENTED |
| `docs/settings-exposure-pass-proposal.md` | Spec: Settings exposure pass (19 literals → settings.json) — ✅ IMPLEMENTED |
| `docs/api-resilience-pass-proposal.md` | Spec: API resilience pass (retry + skip-on-failure) — ✅ IMPLEMENTED |
| `docs/v19-calibration-tuning-pass-proposal.md` | Spec: v19 calibration tuning — funding/OI/ROC thresholds + liq window — ✅ IMPLEMENTED |
| `docs/v20-rsi-roc-algorithm-fixes-proposal.md` | Spec: v20/v21 RSI divergence semantic rewrite + ROC slope_sensitivity split — ✅ IMPLEMENTED |
| `docs/settings-snapshot-history-proposal.md` | Spec: settings snapshot history + round stats + configurable diff cap — ✅ IMPLEMENTED |
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
| ROC(9) | CalcROCSeries | `cfg.Indicators.ROC.SlopeDeltaThreshold` (0.05) — bar-to-bar delta for RISING/FALLING/FLAT classification. `cfg.Indicators.ROC.MagnitudeThreshold` (0.1) — gates partial ROC scoring and Pass 2c ROC-active check. Split in v21 (was single `SlopeSensitivity`). |
| RSI(9) | CalcRSI | `Overbought` (60) / `Oversold` (40) / `PartialOverbought` (55) / `PartialOversold` (45) — v14 widened the 45–55 neutral dead-band to stop RSI voting on every tick off 50 |
| RSI Divergence | CalcRSIDivergence | −1 long: BEARISH + RSI > `DivPenaltyRsiHigh` (65); −1 short: BULLISH + RSI < `DivPenaltyRsiLow` (35). `PivotWing` (2), `LookbackBars` (20) from cfg. **v21 semantic rewrite:** fires BEARISH only when current price is AT OR ABOVE prior pivot (canonical higher-high pattern); prior pivot must have been overbought (`DivergenceOverboughtThreshold` ≥ 65); most-recent pivot used rather than highest in lookback. `DivergenceRsiDelta` raised 2.0 → 5.0. Expected NONE rate rises from ~20% to ~80-90%. |
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
| Bid-Ask Spread | order book (best bid/ask) | `cfg.Indicators.Spread.WidePenaltyThresholdBps` — WIDE-spread entry-side penalty when spread exceeds threshold. Uses live `orderBook` already fetched; `SpreadBps` field on `IndicatorResults`. |
| OFI | CalcOFI | `cfg.Indicators.OFI.BookDepth` (5); `BuyDominantRatio` (2.0) / `SellDominantRatio` (0.5) |
| OFI Momentum | CalcOFIMomentum | RISING/FALLING/FLAT modifier on the OFI level signal. Ring buffer `_ofiHistory` (OFIHistoryMax=10). `cfg.Indicators.OFI.MomentumWindow`, `MomentumThreshold`. |
| Liquidations | CalcLiquidations | `cfg.Indicators.Liquidations.DominanceRatio` (2.0); penalty magnitudes from cfg |
| CVD | CalcCVD | 3-segment weighted slope. Weights from cfg: `cfg.Indicators.CVD.LateSegmentWeight` (2.0) / `EarlySegmentWeight` (1.0). −1 on divergence. `SlopeMinUsd` (12000). |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty. Window=50. Dynamic accelThreshold: `totalWindowUsd × pct` with static-anchored floor (`cfg.Indicators.MicroCVD.AccelThreshold`). |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30, threshold=0.15 via cfg. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below |

### Tier 3
| Indicator | Method | Config keys |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial (`cfg.Indicators.Donchian.QuartilePct`=0.25) + NONE mid-channel note |
| OBV | CalcOBV | Trend + divergence gate from cfg. Adverse divergence blocks cross-category upgrade. |
| VPFR-lite v2 | CalcVPFRLite | POC proximity; VAH/VAL; `VPFRNearestHvnAbove` / `VPFRNearestHvnBelow` for 3-tier target cap. Exp decay (base=0.985). `numBuckets` (50) from cfg. |
| Swing Pivots | CalcSwingPivots | 5m primary (`PivotWing5m`=3, `LookbackBars5m`=30) + 15m context (`PivotWing15m`=2, `LookbackBars15m`=20). Confirmed pivot = N bars left and right all strictly lower/higher. Direction-aware bookkeeping: `SwingTargetLong/Short`, `SwingStopLong/Short`. Config: `cfg.Indicators.Swing.*`. |

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

`SettingsLoader.Initialise()` called in `MainForm.New()`. `SettingsLoader.Current` returns the singleton. Current file version: **v21** (RSI divergence algorithm fix + ROC slope_sensitivity split).

```
settings.json (v21)
  indicators:
    rsi:           { period (9), overbought (60), oversold (40),
                     partial_overbought (55), partial_oversold (45),
                     divergence_price_gate, divergence_rsi_delta (5.0),  ← v21: was 2.0
                     divergence_overbought_threshold (65.0),             ← v21: new key
                     divergence_oversold_threshold (35.0),               ← v21: new key
                     div_penalty_rsi_high (65), div_penalty_rsi_low (35),
                     pivot_wing (2), lookback_bars (20),
                     pass2c_midline (50.0) }            ← v17: was hardcoded 50
    roc:           { period (9),
                     slope_delta_threshold (0.05),       ← v21: replaces slope_sensitivity
                     magnitude_threshold (0.1),          ← v21: new key for partial + Pass 2c
                     series_lookback (3) }
    adx:           { period (9), trend_threshold (25), range_threshold (20) }
    vwap:          { session2_start_hour (13), session2_start_minute (30),
                     warmup_candles (15) }
    bbw:           { period (20), std_dev (2.0),
                     series_window_multiplier (5),     ← v17: was hardcoded period×5
                     squeeze_percentile (0.20) }        ← v17: was hardcoded 0.20
    ema:           { fast (9), mid (21), slow (50) }
    donchian:      { period (20),
                     quartile_pct (0.25) }              ← v17: was hardcoded 0.25
    obv:           { trend_gate (0.001), divergence_gate (0.001) }
    atr:           { period (7), static_ref (115.0),
                     scale_min (0.25), scale_max (4.0) }
    ofi:           { book_depth (5), buy_dominant_ratio (2.0),
                     sell_dominant_ratio (0.5),
                     momentum_window (3),               ← OFI momentum (spec #2)
                     momentum_threshold (0.1) }
    spread:        { wide_penalty_threshold_bps (5.0) } ← bid-ask spread (spec #1)
    volume:        { sma_period (9), static_high (3.0), static_mid (2.0),
                     dynamic_high_clamp_min (2.0), dynamic_high_clamp_max (6.0),
                     dynamic_mid_clamp_min (1.5), dynamic_mid_clamp_max (4.0) }
    vwapDynamic:   { dev_clamp_min (0.30), dev_clamp_max (3.0),
                     static_fallback (1.5) }
    liquidations:  { large_liq_size (200), dominance_ratio (2.0) }
    oi:            { neutral_band_pct (0.05), change_threshold_pct (0.002) }  ← v20: was 0.01→0.003 (v19), then 0.002 (v20)
    dmi:           { period (9) }
    cvd:           { slope_min_usd (12000), slope_pct_of_value (0.01),
                     divergence_price_gate (0.0005), divergence_penalty (1),
                     late_segment_weight (2.0),         ← v17: was hardcoded 2.0
                     early_segment_weight (1.0) }       ← v17: was hardcoded 1.0
    tfi:           { window_size (30), threshold (0.15) }
    microCvd:      { window_size (50), accel_threshold (10000),
                     accel_pct_of_window (0.01),        ← dynamic accel (spec #3)
                     decel_penalty (1) }
    ttm:           { flat_threshold (0.5),
                     sma_period (20),                   ← v17: was hardcoded 20
                     lin_reg_period (7) }               ← v17: was hardcoded 7
    vpfr:          { num_buckets (50) }
    swing:         { pivot_wing_5m (3), lookback_bars_5m (30),  ← spec #5
                     pivot_wing_15m (2), lookback_bars_15m (20) }
    funding:       { momentum_enabled (true), momentum_window (3),
                     momentum_threshold (0.000005),         ← v19: was 0.0001 (10 bp → 0.5 bp)
                     momentum_amplify (1), momentum_soften (1) }
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
    funding_high_positive (0.00003) / funding_low_positive (0.000005)    ← v19: was ±3bp/±0.5bp
    funding_high_negative (-0.00003) / funding_low_negative (-0.000005)
    bbw_squeeze_penalty (2)
    liq_standard_penalty (1) / liq_large_penalty (2)
    funding_high_penalty (2) / funding_high_boost (1) / funding_low_penalty (1)
    atr_target_multiplier (2.0) / atr_stop_multiplier (1.2)
    context_tag_structural_min (3) / context_tag_flow_max (1)
    hold_roc_take_profit_long/short (±0.6)
    hold_rsi_hold_long/short (60/40)
    hold_rsi_evaluate_long/short (40/60)
    regime_max_score:                ← v17: was hardcoded in RegimeMaxScore()
      { trending (19), range_bound (18), transitional (15) }
    tier_floor:                      ← v17: was hardcoded in TierFloor()
      { high_threshold (12), high_floor (9),
        med_threshold (9),  med_floor (6),
        low_threshold (6),  low_floor (3) }
    context_tag_thresholds:          ← v17: was hardcoded in CalcVerdictContext()
      { momentum_fading_decay_ratio (0.5),
        momentum_fading_count_min (2),
        structurally_weak_struct_min (2),
        structurally_weak_flow_min (2) }
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
  network:                          ← v18: API resilience config
    request_timeout_seconds (15)    ← per-call HttpClient timeout (was 10s hardcoded)
    retry_count (1)                 ← additional retries on 5xx / timeout / network drop
    retry_backoff_ms (1000)         ← delay between retries in ms
```

**v21 notes (RSI divergence + ROC split).** `CalcRSIDivergence` semantically rewritten: BEARISH now fires when current price is AT OR ABOVE prior pivot (canonical higher-high pattern), prior pivot's RSI must have been ≥ `DivergenceOverboughtThreshold` (65) to qualify as exhaustion, and the most-recent confirmed pivot is used rather than the highest in the lookback. Mirror fix for BULLISH. `DivergenceRsiDelta` raised 2.0 → 5.0 to require meaningful RSI compression. Expected effect: NONE rate rises from ~20% to ~80–90%. `ROC.slope_sensitivity` split into `slope_delta_threshold` (0.05, for RISING/FALLING/FLAT bar-to-bar classification) and `magnitude_threshold` (0.1, for partial ROC scoring and Pass 2c ROC-active check). Old key removed from `RocSettings` and `settings.json`. `settings.json` bumped v20 → v21.

**v20 notes (OI threshold recalibration).** Post-v19 499-row dataset showed `OISignal` still 100% NEUTRAL: the effective threshold (0.003 × 100 = 0.3%) was above the observed 15m OI peak of ~0.23%. `indicators.OI.change_threshold_pct` lowered 0.003 → 0.002 (effective 0.2%). Quiet-session noise ~0.01–0.015% → 10:1 signal/noise separation. `settings.json` bumped v19 → v20.

**v19 notes (calibration tuning pass).** Six classifiers stuck on a single value across 618 rows due to threshold mismatches vs observed BTC-PERPETUAL scale. Recalibrated: `scoring.funding_high_positive/negative` ±3 bp → ±0.3 bp; `scoring.funding_low_positive/negative` ±0.5 bp → ±0.05 bp; `indicators.funding.momentum_threshold` 10 bp → 0.5 bp; `indicators.OI.change_threshold_pct` 1.0% → 0.3%; `indicators.ROC.slope_sensitivity` 0.1 → 0.05. `GetRecentTradesAsync(100)` → `GetRecentTradesAsync(500)` to widen liquidation detection window from ~60 s to ~5 min. `settings.json` bumped v18 → v19.

**v18 notes (API resilience pass).** `DeribitClient` wraps all five `GetXxxAsync` methods with `ExecuteWithRetry`: retry-once with 1s backoff on transient failures (5xx, timeout, network drop); return `Nothing` on hard failure (4xx, JSON parse, retries exhausted). `GetFundingRateAsync` return type changed `Double` → `Double?`; `GetBookSummaryAsync` value tuple → nullable value tuple. `RunAnalysisAsync` validates all required fetches after `Task.WhenAll`; if any are `Nothing`, renders `ANALYSIS SKIPPED: <reason>`, increments `_skipCount`, and returns without scoring or writing a CSV row. 15m cache preserved on fetch failure — stale data kept for MTF gate. Skip counter shown in status bar when > 0. No scoring change. No CSV schema change.

**v17 notes (settings-exposure pass).** All new keys default to exactly the previously-hardcoded values — no behavioural change. Keys added: `rsi.pass2c_midline`, `bbw.series_window_multiplier`, `bbw.squeeze_percentile`, `donchian.quartile_pct`, `cvd.late_segment_weight`, `cvd.early_segment_weight`, `ttm.sma_period`, `ttm.lin_reg_period`, `scoring.regime_max_score`, `scoring.tier_floor`, `scoring.context_tag_thresholds`.

**v16 notes (swing pivot spec).** Added `indicators.swing` block: `pivot_wing_5m`, `lookback_bars_5m`, `pivot_wing_15m`, `lookback_bars_15m`.

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

- **MaxScore:** base values read from `cfg.Scoring.RegimeMaxScore.*` — TRENDING (19), RANGE_BOUND (18), TRANSITIONAL (15). With RegimeWeights.Enabled (default), TRENDING → 20 and RANGE_BOUND → 19 (base + AlignmentBonus). TRANSITIONAL unchanged.
- **Verdict thresholds:** `Math.Ceiling(regimeMax * pct)` — no hardcoded magic numbers
- **Step 2:** Score signals into ScoreState → all thresholds from cfg. Includes bid-ask spread WIDE-spread penalty and OFI momentum modifier.
- **Pass 2:** Upgrade partials on cross-category confirmation; OBV upgrade blocked on adverse divergence
- **Pass 2b:** OI × CVD cross-confirm gate — if OI full signal (or upgraded partial) and CVD direction+sign agree, apply `UpgradeBonus`; if full OI directly conflicts with CVD, apply `ConflictPenalty`; partial OI conflict is non-penalising
- **Pass 2c:** Regime alignment gate — suppressed in TRANSITIONAL and when LongScore=ShortScore. TRENDING checks EMA ribbon + ROC (active when Abs(ROC)≥`MagnitudeThreshold`) + CVD slope+sign. RANGE_BOUND checks VWAP dev (active only outside warmup) + RSI(9) vs `cfg.Indicators.RSI.Pass2cMidline` (50) + Donchian(20). All active aligned → `+AlignmentBonus` (capped at regimeMax). All conflict → `-ConflictPenalty`.
- **Step 3:** Baseline funding-rate modifier
- **Step 3b:** Funding-momentum modifier — can soften crowding penalty when momentum is falling, or amplify it when momentum is rising into crowding
- **Step 4:** Regime veto / TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate veto → NO TRADE
- **Step 5:** Threshold comparison → verdict
- **Step 5b (3-tier target cap):** Priority: (1) swing target (`SwingTargetLong/Short` from 5m pivot — closest to entry wins), (2) nearest HVN above/below (`VPFRNearestHvnAbove/Below`), (3) POC fallback. Winner = closest cap below raw ATR target (long) or above raw target (short). Sets `AdjustedLongTarget` / `AdjustedShortTarget` and `TargetCapReason` with tier label.
- **Step 5b (VerdictContext):** FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED. Structural check fires STRUCTURALLY_WEAK when swing data exists but no clean target+stop pair. Decay ratio, fading count min, struct/flow weak thresholds all from `cfg.Scoring.ContextTagThresholds.*`. See `docs/verdict-context-tag-proposal.md`.
- **Step 6 (CalcHoldStatus — layered exit):**
  - Layer 1: 2+ adverse microstructure signals → fast EXIT
  - **Layer 1.5 (new):** structural break exit — price closed through prior swing low (long) or swing high (short)
  - Layer 2: OBV divergence exit
  - Layer 3: RSI divergence evaluate; single adverse microstructure warning; RSI/ROC structural assessment
- **Step 7:** ATR target/stop from `cfg.Scoring.AtrTargetMultiplier` / `AtrStopMultiplier`. **Structural rows** rendered in UI: long structural stop/entry/target + R:R (shown when SwingTargetLong > 0 or SwingStopLong > 0); mirror for short.
- **TierFloor():** reads `cfg.Scoring.TierFloor.*` — HighThreshold (12)/HighFloor (9)/MedThreshold (9)/MedFloor (6)/LowThreshold (6)/LowFloor (3). Previously hardcoded.
- **CalcKellySizing():** called from RenderOutput after ATR levels are computed; populates Kelly fields on VerdictResult (display-only, zero scoring impact). See `docs/kelly-criterion-proposal.md`.

For the full annotated Calculate() pipeline with per-step implementation detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`
- **Last transacted price** = `recentTrades(0).Price` — displayed above ATR block, not used as entry
- Long: Stop = price − (ATR × scale × AtrStopMultiplier), Target = price + (ATR × scale × AtrTargetMultiplier)
- Short: mirrored. R:R = 1:1.7 at current settings (1.2 stop / 2.0 target)
- **3-tier cap:** if `v.AdjustedLongTarget > 0` (or Short), raw target shown dimmed; capped target shown in amber bold with reason label (e.g. `CAPPED @ 95200.0 (SWING_HIGH_5M)`, `NEAREST_HVN_ABOVE`, or `POC`).
- **Multipliers read from cfg** — label and R:R display are dynamic, not hardcoded.
- **Structural rows** (below ATR block): `Long structural: Stop X | Entry X | Target X  R:R 1:N  (risk X / rwd X)` in cyan when both target and stop exist; dim text when only one side is available. Mirror for short.
- **Kelly Sizing block** rendered immediately after ATR levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. Display carries an advisory label noting the R:R is ATR-basis, not structural. EST mode only — CAL mode will be reinstated after the backtesting module is built. Suppressed when KellyF = 0 (no edge).
- **Funding display** now includes both the raw rate/bias row and a separate momentum row showing `RISING` / `FALLING` / `FLAT` plus enabled/soften/amplify config values.

---

## 9. Open Position Guidance (CalcHoldStatus)

Priority order: (1) 2+ adverse microstructure signals → fast EXIT; **(1.5) structural break exit — price closed at/below prior swing low (long) or at/above prior swing high (short) → EXIT with level cited;** (2) OBV divergence exit; (3) RSI divergence evaluate; (4) single adverse microstructure warning; (5) RSI/ROC structural assessment.
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
| VerdictContext CSV logging | ✅ Shipped 2026-04-29 (AnalysisLogger v0.3). `VerdictContext` column added to `analysis_log.csv`. CalibrationReport gains `VERDICT CONTEXT DISTRIBUTION` aggregate section. Per-tag accuracy correlation deferred until 300+ rows. | Done |
| FundingMomentum CSV logging | ✅ Shipped 2026-04-29 (AnalysisLogger v0.3). `FundingMomentum` column added to `analysis_log.csv`. CalibrationReport gains `FUNDING MOMENTUM DISTRIBUTION` aggregate section. | Done |
| OI×CVD CSV logging | ✅ Shipped 2026-04-29 (AnalysisLogger v0.3). `OiCvdOutcome` column added (NONE / CONFIRMED_LONG / CONFIRMED_SHORT / CONFLICT_LONG / CONFLICT_SHORT). `VerdictResult.OiCvdOutcome` property set in all four Pass 2b branches. CalibrationReport gains `OI x CVD PASS 2b OUTCOMES` aggregate section. | Done |
| Bid-ask spread threshold | `wide_penalty_threshold_bps` default TBD from live data. Review after 50+ runs to calibrate the BTC-PERPETUAL normal spread distribution. | Low |
| OFI momentum thresholds | `ofi.momentum_window` and `ofi.momentum_threshold` — review hit rate after 50+ live runs; confirm whether OFI momentum shift is a leading vs lagging signal for OFI reversals. | Low |
| Swing pivot wing/lookback | `pivot_wing_5m=3` / `lookback_bars_5m=30` — review false-positive rate for swing detection: are confirmed pivots close enough to relevant structure? May need widening to 4–5 on low-volatility sessions. | Low |
| Dynamic MicroCVD accel pct | `microCvd.accel_pct_of_window` — review the dynamic floor vs static `accel_threshold` split after 50+ runs across quiet and active sessions. Ensure the dynamic ceiling does not underfire during genuine bursts. | Low |
| VPFR v2 HVN nearest walls | `VPFRNearestHvnAbove` / `VPFRNearestHvnBelow` — review whether nearest-HVN cap fires more often than POC cap and whether it reduces over-targeting vs prior 2-tier cap. | Low |

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
| Dynamic MicroCVD accelThreshold | Static 10000 USD threshold (v14) is noise during high-volume sessions and a too-high bar during quiet hours. Scale dynamically against total window USD flow with a static-anchored floor. | ✅ IMPLEMENTED 2026-04-29 (spec #3) |
| RSI divergence on 5m candles | Current divergence is 1m only. A confirmed divergence on both 1m and 5m simultaneously would be a stronger penalty signal and reduce false penalties on 1m micro-noise. Requires `CalcRSIDivergence` called on `candles5m` and a combined gate in scoring pipeline. | 🔍 Deferred — see `docs/post-websocket-post-calibration-backlog.md` D3 |
| Donchian × BBW state cross-reference | Wide channel breakout is meaningfully different from a tight-channel breakout. Cross-reference BBW squeeze state (ACTIVE / RELEASING / NONE) when scoring Donchian to up-weight breakouts from compression. | 🔍 Deferred — see `docs/post-websocket-post-calibration-backlog.md` D4 |

### Fine-Tuning (marginal gains, run after calibration data available)

| Item | Description | Status |
|---|---|---|
| Bid-ask spread microstructure signal | `orderBook` depth is already fetched. Spread between best bid and best ask is an unused fast microstructure signal — sudden widening often precedes a flush. Add `SpreadBps` to `IndicatorResults` and a penalty trigger in Tier 2. | ✅ IMPLEMENTED 2026-04-29 (spec #1) |
| Auto-tuning from CSV log | Once `CalibrationReport` reaches READY (≥300 rows, ≥3 sessions, ≥3 regimes, ≥2 liq events), build a pass that correlates each signal's vote with subsequent price direction and adjusts `settings.json` weights automatically. | 🔍 Requires calibration data first; see also Section 16 (frontier-LLM auto-tweaking is the longer-arc plan) |

### Completed Specs (2026-04-29)

All six specs drafted on 2026-04-27 are now **IMPLEMENTED**.

| Spec | Scope | Status |
|---|---|---|
| `docs/bid-ask-spread-proposal.md` | SpreadBps + WIDE-spread entry-side penalty | ✅ IMPLEMENTED 2026-04-29 |
| `docs/ofi-momentum-proposal.md` | OFIMomentum (RISING/FALLING/FLAT) modifier on OFI level signal | ✅ IMPLEMENTED 2026-04-29 |
| `docs/dynamic-microcvd-accel-proposal.md` | Self-scaling MicroCVD acceleration threshold | ✅ IMPLEMENTED 2026-04-29 |
| `docs/vpfr-lite-v2-proposal.md` | VAH/VAL + nearest HVN/LVN walls; 3-tier Step 5b cap arbitration | ✅ IMPLEMENTED 2026-04-29 |
| `docs/swing-pivot-proposal.md` | 5m + 15m swing structure; structural ATR display rows; Layer 1.5 exit; 3-tier cap; sharper STRUCTURALLY_WEAK | ✅ IMPLEMENTED 2026-04-29 |
| `docs/settings-exposure-pass-proposal.md` | 19 hardcoded scoring literals → `settings.json`; closes auto-tweaking audit prerequisite | ✅ IMPLEMENTED 2026-04-29 |

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
| **2026-05-15** | [ohlc-gap-backfill] `settings.json` bumped v26 → v27. Spec: `docs/ohlc-gap-backfill-proposal.md`. New **Step 1.5** in `LivePerformanceTracker.InitialiseAsync` sits between Step 1 (load + trailing-gap fetch) and Step 2 (load eval cache): scans `_ohlcLookup` for interior gaps within the 7-day window via new private shared helpers `FindGaps` / `TruncateToMinute` / `FetchGapChunked`, then fetches each gap from Deribit (chunked to `max_gap_fill_minutes`) and appends fresh bars to both `_ohlcLookup` and the on-disk OHLC cache. Idempotent: filters by `Not _ohlcLookup.ContainsKey(b.CloseTime)` before append. Throttled by `max_gap_fill_calls` safety cap (defers gaps that would exceed the remaining budget). New optional `statusCallback As Action(Of String)` parameter on `InitialiseAsync` updates `lblLogInfo.Text` mid-backfill (`"Loading performance history... (OHLC gap fill: K of N)"`); per-call `Console.WriteLine` diagnostics. **PerformanceDisplaySettings POCO** gains `GapBackfillEnabled` (true), `MaxGapFillCalls` (10), `MaxGapFillMinutes` (5000). `MainForm_Layout` startup `Task.Run` builds the status callback lambda. Closes the failure mode where interior bars went missing after interrupted sessions / truncated responses, leaving Spec 2 target-hit re-walk and the auto-tweaker with incomplete OHLC coverage. |
| **2026-05-13** | [live-performance-display] `settings.json` bumped v25 → v26. Spec: `docs/live-performance-display-proposal.md`. Activates P7. **New host-agnostic files:** `OhlcCache.vb` (rolling 7-day 1m OHLC cache — Load, Append, WriteAll, RollingTrim, NewestBarTime; slack-cap batch trim at 10,584 bars) and `LivePerformanceTracker.vb` (eval cache + window aggregator — `InitialiseAsync`, `UpdateAsync`, `ComputeWindows`; reuses `FailureRateMatrix.WalkBars` verbatim; `EvalCacheEntry` + `WindowAggregate` nested types; backfill reads analysis_log.csv cols 0/1/2/63/81/82). **New sidecar files** (gitignored): `analysis_eval_cache.csv` (one row per verdict, schema v1) + `ohlc_1m_cache.csv` (schema v1). **`EngineSettings`** gains `PerformanceDisplaySettings` class (4 keys: `enabled`, `min_sample_for_render`, `eager_backfill_on_startup`, `session_block_semantic`). **MainForm_Layout:** six `Label` fields (`lblPerfWeek/3d/Day/Asia/London/Ny`), `_perfTip` (shared ToolTip), factory `MakePerfLabel`, `UpdatePerformanceLabels()` (colour + tooltip), startup fire-and-forget `Task.Run` that calls `InitialiseAsync` with a `Func(DateTime,DateTime,Task(Of List(Of OhlcBar)))` fetcher built from `DeribitClient.GetCandlesAsync`; `ResizeControls()` cascades the six label positions right of `btnStartStop`. **MainForm_Analysis hook:** `Await LivePerformanceTracker.UpdateAsync(verdict, r, candles1m, DateTime.UtcNow)` + `UpdatePerformanceLabels()` after `RenderOutput`. **Session block algorithm (§2b):** most-recent-block for Asia/London/NY with straddle-aware NY (three sub-cases: tail, head, between). **Settings:** `performance_display` block with 4 keys. **Docs:** `TraderGuide.md` §17 "Live Performance Strip" subsection; `UserManual.md` §21 eight sub-sections; `.gitignore` four cache paths (Debug + Release). PDFs regenerated. |
| **2026-05-12** | [settings-snapshot-history] No `settings.json` version change (all knobs live in `tweaker_config.json`). Spec: `docs/settings-snapshot-history-proposal.md`. **New host-agnostic files in `tools/AutoTweaker/`:** `CompositeScorer.vb` (snapshot ranking formula `(100 - AvgFailureRatePct) + clamp(StreakLength, StreakLengthClamp) × StreakWeight`), `ConditionsExtractor.vb` (CSV slice → regime/atr/funding/vwap/spread/ofi mix + condition bucket), `SnapshotManager.vb` (Create / AccumulateConditions / Finalise + bucket rotation against ACTIVE per-bucket champion), `RoundStatsBuilder.vb` (async per-tier accuracy via `FailureRateMatrix.WalkBars`; covers ALL directional verdicts incl. WEAK_*). All four are also referenced from the main `DeribitVerdictEngine.vbproj` via explicit `Compile Include` so the WinForms host can render Round Stats; zero new WinForms refs in `tools/AutoTweaker`. **TweakerState** gains `CurrentBelowThresholdStreak`, `ActiveSnapshotFilename`, `ActiveSnapshotCreatedIso`, `LastSuccessfulRoundIso`, and `RoundHistory` (50-cap list of `RoundSummary` entries: outcome, window row span, aggregate failure rate, picked-cells JSON, diff summary, reasoning excerpt). **TweakerConfig** gains `MaxKeysPerProposal` (default 3, previously hard-coded), `SnapshotStreakX` (3), `StreakWeight` (1.5), `StreakLengthClamp` (20), `SnapshotsDir` (`settings_snapshots`), `ManifestPath` (`settings_snapshots/manifest.csv`). **SettingsDiffApplier.Validate** now takes `maxKeysPerProposal` as a parameter (legacy 2-arg overload preserved at default 3); new `ApplyRevert(snapshotPath, settingsPath, reasoning)` exempts the key-cap but still runs rejected-pattern/disabled-gate validation on the snapshot's content. `ParseDiff` return tuple extended with `Action` / `RevertTarget` fields. **PromptBuilder** signature extended with `manifestActiveRows`, `conditions`, `maxKeysPerProposal`; system message exposes the configurable cap and an explicit REVERT action contract; composite-score formula reproduced verbatim. **AutoTweakerCore** records a `RoundSummary` for every evaluable round, drives `HandleStreakAdvance` / `HandleStreakInterrupt`, calls `SnapshotManager.Create` at streak==X, `AccumulateConditions` while ACTIVE, and `Finalise` (with `FinalisedIso` = last successful round's timestamp) on interruption. REVERT branch wired through `ApplyRevert` (auto-commit) or `proposed_diffs/<ts>_revert.json` (manual). **New WinForms file:** `UI/RoundStatsForm.vb` (non-modal RichTextBox with Refresh / Close, async build via `RoundStatsBuilder.BuildAsync`). **TweakSettingsForm** gains `txtSnapshotStreakX`, `txtMaxKeysPerProposal`, `txtStreakWeight`, `lblActiveSnapshot` (live `Streak: N/X` + filename), `btnShowRoundStats`, `btnOpenSnapshotsDir`. Save writes the new fields via `TweakerConfig.Save`. `.gitignore` adds `settings_snapshots/`. Docs: `TraderGuide.md` §17 adds the "Settings Snapshots" subsection; `UserManual.md` adds §20 (eight sub-sections covering concepts, streak tracking, creation, conditions, composite score, manifest schema, revert mechanism, round stats, state persistence) plus updated §19 control table and §17 companion-log note. PDFs regenerated. |
| **2026-05-12** | [output-dump-bug-audit] No `settings.json` version change (display-side fixes only). **B1 — `TargetCapReason` field split.** `VerdictResult.TargetCapReason` (single property, overwritten by short-side cap block) replaced by `TargetCapReasonLong` / `TargetCapReasonShort` pair. Write sites in `ScoringEngine_Calculate_Verdict.vb` Step 5b updated: long cap block writes `TargetCapReasonLong`, short cap block writes `TargetCapReasonShort`; both initialised to `""` at block entry. Render sites in `MainForm_Render_Header.vb` updated: Long row reads `TargetCapReasonLong`, Short row reads `TargetCapReasonShort`. `AnalysisLogger.LogRun` now picks the direction-appropriate field for CSV column 84: long-side verdicts write `NormaliseCapReason(v.TargetCapReasonLong)`, short-side verdicts write `NormaliseCapReason(v.TargetCapReasonShort)`, unqualified NO TRADE prefers the non-empty side (Long if both non-empty, `"none"` if both empty). CalibrationReport unchanged — reads CSV column `"TargetCapReason"` by name. Bug evidence: Long row was showing SHORT-side cap label when both HVN-above and HVN-below caps fired in the same run (≥20 instances in 184-run dump). **B2 — CONTEXT: line always rendered.** Guard at `MainForm_Render_Header.vb:448` changed from `If v.VerdictContext <> "" AndAlso v.VerdictContext <> "CONFIRMED" Then` → `If v.VerdictContext <> "" Then`. CONFIRMED verdicts now render `CONTEXT: CONFIRMED` in green (C_VALUE Case Else branch was already correct, just unreachable). Restores the 2026-04-14 documented fix that was regressed. Bug evidence: 33 of 184 dump runs (~18%) had CONTEXT: line silently dropped; affected every verdict tier. Source: `docs/output-dump-bug-audit-2026-05-12.md`. |
| **2026-05-11** | [output-dump] `settings.json` bumped v24 → v25. Spec: `docs/output-dump-proposal.md`. Persistent markdown record of full rendered analysis text per run, capturing display strings and breakdown notes that the structured CSV doesn't. **New file:** `AnalysisOutputDump.vb` (host-agnostic helper — `Append`, `Clear`, `CountRuns`, rolling `TrimToMaxRuns`). **New form:** `UI/OutputDumpSettingsForm.vb` (non-modal; Enabled toggle, max-runs textbox, file path + size labels, Clear + Save + Close buttons). **Status-bar UI:** `lnkOutputDump` ("Output Dump") opens the dump file in OS default handler; `lnkOutputDumpSettings` ("⚙") opens the settings dialog. Both added to `MainForm_Layout`. **Hook:** `RenderOutput` in `MainForm_Render_Sections` calls `AnalysisOutputDump.Append(v.Timestamp, txtOutput.Text, ...)` after the breakdown table. **`VerdictResult.Timestamp`** added; set to `DateTime.Now` in `RunAnalysisAsync` after `ScoringEngine.Calculate()`; `RenderOutputHeader` TIME: line now sources from `v.Timestamp` (correct local timezone via `TimeZoneInfo.Local`, replacing hardcoded +8 offset). **New settings block:** `analysis_logging.output_dump_enabled` (true) + `analysis_logging.output_dump_max_runs` (3000). Rolling-trim: after each append, if count > max_runs, drop oldest until count == max_runs; max_runs = 0 = unlimited. Dump file gitignored (Debug + Release paths). |
| **2026-05-07** | [Failure Definition v2 — barrier-hit with adverse stop] No `settings.json` version change (behaviour change is entirely in `analysis/` and `tools/AutoTweaker/`). Spec: `docs/failure-definition-v2-proposal.md`. **Core semantic change:** v1 measured fixed-horizon adverse return at T+W; v2 walks 1m OHLC bars and classifies as SUCCESS (favourable barrier wick hit before adverse barrier) or FAILURE (adverse hit first, OR window expired, OR both barriers in same 1m bar — conservative-bias ambiguous-bar rule). **OHLC data source:** new `analysis/DeribitOhlcFetcher.vb` (self-contained HttpClient, no DeribitClient or SettingsLoader dependency) bulk-fetches 1m candles from Deribit for the full CSV time range in one call. **ForwardWindowJoiner replaces ForwardReturnJoiner:** new `analysis/ForwardWindowJoiner.vb` defines `OhlcBar` and updated `CsvRow` (adds `SwingStopLong`, `SwingStopShort`, `ForwardBars` dict); `Load()` just parses the CSV; `PopulateForwardBars()` slices the OHLC map per row per window. Old `ForwardReturnJoiner.vb` deleted. **Adverse barrier:** structural stop (`SwingStopLong`/`SwingStopShort`) where logged (>0); falls back to `entry ± AdverseFallbackAtrMultiplier × ATR` (constant 1.2, matching `cfg.Scoring.AtrStopMultiplier` default). **Eligible bars:** closes at row.Timestamp + 3 min through + W min (bars at T+1 and T+2 excluded — too quick to execute). **ATR ≤ 0 rows excluded** from denominator; counted in `AtrInvalidExcluded` and reported in Section 1. **Threshold swap (AnalysisConstants):** STRONG `{0.5, 0.8}`, MEDIUM `{0.3, 0.5}` — swapped from v1 to preserve "STRONG = harder bar" under barrier-hit direction. **FailureRateMatrix.Compute rewrite:** new `WalkBars()` public shared function (reused by AnalysisRunner context cross-tab); decomposition counters added to `FailureCellResult` (Successes, AdverseHitFails, WindowExpiryFails, AmbiguousFails). **Schema v2 rotation:** `AppendPickedCell` checks first line of `picked_cell_history.csv`; if not `# schema=v2`, renames to `.v1.bak` and starts fresh. `AppendPickedCell` now writes FailureRate/SampleSize/CiLow/CiHigh columns. **AnalysisRunner.Run now async** (`Task(Of AnalysisReport)`); click handler updated to `Async Sub` with "Fetching OHLC…" status and link disable/re-enable. Error path writes banner only if OHLC fetch fails. **MarkdownReportWriter:** interpretation hint blockquote added at top, Section 1 expanded (ATR-invalid, adverse-barrier source, forward-data source), Section 2 clarification note, new Section 4a Barrier-Hit Decomposition. **AutoTweakerCore:** OHLC fetch after eligibility checks; `ForwardWindowJoiner` replaces `ForwardReturnJoiner`; `ForwardBars` populated before Compute; updated `AppendPickedCell` call signature. **AutoTweaker.vbproj:** `ForwardWindowJoiner.vb` + `DeribitOhlcFetcher.vb` in place of `ForwardReturnJoiner.vb`. **DeribitClient:** time-range overload of `GetCandlesAsync(resolution, startMs, endMs)` added. Trigger threshold default 40% unchanged; recalibrate after 200+ v2 rows accumulate. |
| **2026-05-06** | [Bundle 3 — d1 + d2] `settings.json` bumped v23 → v24. Specs: `docs/d1-trend-structure-proposal.md`, `docs/d2-volume-weighted-pivots-proposal.md`. **D1 — HH/HL/LH/LL trend structure.** New `ClassifyTrendStructure()` in `Indicators_Structure.vb`, `TrendStructure` enum (UPTREND / DOWNTREND / EXPANSION / CONTRACTION / UNDEFINED). Pass 2c integration: separate `StructureBonus` (default 1) capped at regimeMax, applied only when structure agrees with dominant side, suppressed in TRANSITIONAL. New `indicators.trend_structure` settings block (4 keys). CSV column 87 `TrendStructure5m`. **D2 — Volume-weighted pivots (display-only v1).** `CalcSwingPivots` extended with `BestPivotByVolume5m`, `BestPivotVolumeRatio5m`, `BestPivotIsHigh5m` (volume aggregated across wing window). CSV columns 85–86 populated (reserved in v0.4). v2 cap-arbitration promotion parked as observation P1 (§16.6). **B1 stub left untouched** — `b1-per-indicator-regime-weights-proposal.md` carries `Status: PROPOSED — BLOCKED` until Bundle 1 produces empirical hit-rate data. |
| **2026-05-06** | [Bundle 2 — auto-tweaker pipeline] No `settings.json` version change. Spec: `docs/auto-tweaker-pipeline-proposal.md`. New separate .NET 8 console project at `tools/AutoTweaker/AutoTweaker.vbproj` targeting `net8.0` (no `-windows` suffix — Linux-portable, zero WinForms references). 7 console-app classes: `AutoTweakerProgram`, `AutoTweakerCore`, `PromptBuilder`, `ClaudeApiClient`, `SettingsDiffApplier`, `TweakerConfig`, `TweakerState`. Latest-Opus model resolution via `/v1/models` with `created_at` desc sort. ANTHROPIC_API_KEY from env var only. Dry-run mode writes API payload to `.txt` for manual handling. `--apply-manual <path>` flag for diff application after manual review. SettingsDiffApplier enforces 3-key scope cap, hard rejection list (8 banned path fragments + 2 disabled-gated paths), stale-value check, version-monotonicity bump. New WinForm `UI/TweakSettingsForm.vb` — non-modal dialog with auto-commit / dry-run toggles, window size / failure threshold / cooldown textboxes, status polling tied to new `MainForm.AnalysisCompleted` event + 30s timer fallback, "Run Tweaker Now" button disabled unless status is `Ready`. Spec also covers failure-definition (`docs/failure-definition-proposal.md`): 3 windows × 3 ATR thresholds, STRONG=tight (0.3, 0.5) / MEDIUM=loose (0.5, 0.8), Wilson 95% CI cell-stability picker, picked-cell history CSV. Constants in `analysis/AnalysisConstants.vb` shared between Bundle 1 analysis script and the auto-tweaker. `.gitignore` updated for tweaker config + state + payload dirs + picked-cell history. |
| **2026-05-05** | [Bundle 1 — csv-expansion-v0.4 + analysis script] `settings.json` bumped v22 → v23. Spec: `docs/csv-expansion-v0.4-proposal.md`. **CSV schema bump v0.3 → v0.4.** Added 18 columns at positions 69–86: `SpreadBps`, `OFIMomentum`, `FundingDelta`, `VPFRVAH`, `VPFRVAL`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow`, `LastSwingHigh5m`, `LastSwingLow5m`, `LastSwingHigh15m`, `LastSwingLow15m`, `SwingTargetLong`, `SwingTargetShort`, `SwingStopLong`, `SwingStopShort`, `TargetCapReason`, `BestPivotByVolume5m` (reserved), `BestPivotVolumeRatio5m` (reserved). `AnalysisLogger.EnsureLogFile()` rotates old log to `analysis_log.csv.<schema-tag>.bak` on header mismatch. `r.FundingDelta` computed in `MainForm_Analysis.RunAnalysisAsync` after `_fundingHistory.Add`. New CalibrationReport sections: SPREAD / OFI MOMENTUM / TARGET CAP REASON DISTRIBUTION. **Analysis script.** New `analysis/` folder with 9 host-agnostic VB.NET classes (`AnalysisRunner`, `ForwardReturnJoiner`, `FailureRateMatrix`, `FundingMomentumDiagnostic`, `OutlierAudit`, `MarkdownReportWriter`, `AnalysisReport`, `AnalysisConstants`) + `AnalysisReportForm.vb` (the only file in the folder with `System.Windows.Forms` reference, by design). Joins each row to T+5/T+10/T+15 forward returns via subsequent CSV rows; produces failure-rate matrix per verdict tier × hold window × ATR threshold with 95% Wilson CI; outputs markdown + summary CSV. Reachable from MainForm via new `lnkAnalysisReport` link. Spec: `docs/analysis-script-proposal.md`. |
| **2026-05-01** | [v22 funding calibration] `settings.json` bumped v21 → v22. Spec: `docs/v22-funding-calibration-pass-proposal.md`. Regime-aware funding band recalibration based on Deribit's 1m/7d/8h funding rate charts: low ±0.0001 → ±0.0001 (kept) / high ±0.0008 → ±0.0008. Funding momentum threshold: 0.0001 (1 bp). Spec acknowledges 60s polling cadence misses sub-minute spikes; the long-term fix is WebSocket migration. After 2460-row v0.3 calibration audit on 2026-05-04, `FundingMomentum` still showed 100% FLAT — see `analysis-script-proposal.md` for empirical investigation pathway. |
| **2026-04-30** | [RSI divergence + ROC split] `settings.json` bumped v20 → v21. Spec: `docs/v20-rsi-roc-algorithm-fixes-proposal.md`. **Fix 1 — CalcRSIDivergence semantic rewrite.** Three bugs corrected: (a) direction inverted — BEARISH now requires current price AT OR ABOVE prior pivot (higher-high pattern), not below it; (b) overbought/oversold gate added — prior pivot RSI must have been ≥ 65 / ≤ 35; (c) most-recent pivot scan replaces highest/lowest-in-lookback. `DivergenceRsiDelta` raised 2.0 → 5.0. Expected NONE rate rises from ~20% to ~80–90%. New cfg keys: `RSI.divergence_overbought_threshold` (65), `RSI.divergence_oversold_threshold` (35). **Fix 2 — ROC slope_sensitivity split.** `SlopeSensitivity` replaced by `SlopeDeltaThreshold` (0.05, slope classification) and `MagnitudeThreshold` (0.1, partial scoring + Pass 2c activation). Old key removed. |
| **2026-04-30** | [OI threshold recalibration] `settings.json` bumped v19 → v20. Post-v19 499-row dataset showed `OISignal` 100% NEUTRAL — effective threshold 0.3% was above the observed 15m peak (~0.23%). `indicators.OI.change_threshold_pct` 0.003 → 0.002 (effective 0.3% → 0.2%). Single value change, no spec. |
| **2026-04-30** | [Calibration tuning pass] `settings.json` bumped v18 → v19. Spec: `docs/v19-calibration-tuning-pass-proposal.md`. Six CSV columns stuck on a single value across 618 rows. Recalibrated all to observed BTC-PERPETUAL scale: funding band thresholds ×10 lower (±3 bp → ±0.3 bp / ±0.5 bp → ±0.05 bp); `funding.momentum_threshold` 10 bp → 0.5 bp; `OI.change_threshold_pct` 1.0% → 0.3%; `ROC.slope_sensitivity` 0.1 → 0.05. `GetRecentTradesAsync(100)` → `(500)` to widen liq detection window. No scoring logic changes. No CSV schema change. |
| **2026-04-30** | [API resilience pass] `settings.json` bumped v17 → v18. Spec: `docs/api-resilience-pass-proposal.md`. Added `ExecuteWithRetry` private helper to `DeribitClient`: retry-once with 1s backoff on transient failures (HTTP 5xx, `TaskCanceledException`, network errors); return `Nothing` on hard failure (4xx, JSON parse, retries exhausted). VB.NET `Await`-in-`Catch` restriction handled via `needsDelay` flag set inside catch, awaited after. All 5 public `GetXxxAsync` methods wrapped. `GetFundingRateAsync` return type `Double` → `Double?`; `GetBookSummaryAsync` value tuple → nullable value tuple. `HttpClient.Timeout` now reads `cfg.Network.RequestTimeoutSeconds` (default 15s; was 10s hardcoded). `RunAnalysisAsync`: 15m cache preserved on fetch failure (stale MTF data preferred over cold-start Nothing); skip validation block after `Task.WhenAll` — if any required fetch is `Nothing`, renders `ANALYSIS SKIPPED: <reason>`, increments `_skipCount`, returns without scoring or CSV write; `fundingRate.Value` / `bookSummary.Value.*` unwrapped post-skip-check. `_skipCount` field added to `MainForm_Layout`; `UpdateLogInfo` shows `Skipped: N` suffix when > 0. `NetworkSettings` class + `Network` property added to `EngineSettings`. `network` block added to `settings.json`: `request_timeout_seconds` (15), `retry_count` (1), `retry_backoff_ms` (1000). No scoring change. No CSV schema change. No indicator change. Smoke test passed 2026-04-30. |
| **2026-04-29** | [settings-exposure pass] `settings.json` bumped v16 → v17. Spec #6. Lifted 19 hardcoded scoring literals to `settings.json` — zero behaviour change at defaults. New keys: `rsi.pass2c_midline` (50.0), `bbw.series_window_multiplier` (5), `bbw.squeeze_percentile` (0.20), `donchian.quartile_pct` (0.25), `cvd.late_segment_weight` (2.0), `cvd.early_segment_weight` (1.0), `ttm.sma_period` (20), `ttm.lin_reg_period` (7), `scoring.regime_max_score.*`, `scoring.tier_floor.*`, `scoring.context_tag_thresholds.*`. Updated call sites: `CalcBBW`, `CalcTTMSqueeze`, `CalcCVD` all take new Optional params wired from cfg. `RegimeMaxScore()` and `TierFloor()` in `ScoringEngine_Helpers` now read from cfg instead of hardcoded literals. `CalcVerdictContext()` decay ratio, fading count, struct/flow weak thresholds all from `cfg.Scoring.ContextTagThresholds`. Pass 2c RSI midline from `cfg.Indicators.RSI.Pass2cMidline`. Closes the auto-tweaking audit prerequisite (Section 16.3 item 2). |
| **2026-04-29** | [swing pivot detection] `settings.json` bumped v15 → v16. Spec #5. Added `CalcSwingPivots()` to `Indicators_Structure.vb`: confirmed pivot requires N bars left and right all strictly lower/higher; walks backward from scanEnd to find most recent pair. Added 8 swing pivot fields to `IndicatorResults` (`LastSwingHigh/Low5m/15m`, `SwingTargetLong/Short`, `SwingStopLong/Short`). Called from `MainForm_Analysis` for 5m (primary) and 15m (context) candles; direction-aware bookkeeping computed inline. **Layer 1.5** in `CalcHoldStatus`: structural-break exit when price closes at/below prior swing low (long) or at/above prior swing high (short). **3-tier Step 5b target cap** in `Calculate()`: swing target → nearest HVN → POC (winner = closest to entry). **`CalcVerdictContext` structural check**: fires STRUCTURALLY_WEAK when swing data exists but no clean target+stop pair. **Structural rows** added to `RenderOutputHeader()`: long and short stop/entry/target R:R display (cyan for full pair, dim for partial). Config: `indicators.swing` block. |
| **2026-04-29** | [specs #1–#4] Spec #1: bid-ask spread microstructure signal. `SpreadBps` added to `IndicatorResults`; WIDE-spread entry-side penalty in Step 2 from `cfg.Indicators.Spread.WidePenaltyThresholdBps`; `indicators.spread` block in `settings.json`. Spec #2: OFI momentum. `OFIMomentum` (RISING/FALLING/FLAT) added to `IndicatorResults`; `CalcOFIMomentum()` added to `Indicators_OrderFlow`; `_ofiHistory` ring buffer (OFIHistoryMax=10) in `MainForm_Layout`; modifier applied in Step 2 alongside OFI level signal; `ofi.momentum_window` / `ofi.momentum_threshold` in `settings.json`. Spec #3: dynamic MicroCVD accelThreshold. `CalcMicroCVD` now computes `totalWindowUsd × accel_pct_of_window` with `accel_threshold` as static-anchored floor; `microCvd.accel_pct_of_window` added to `settings.json`. Spec #4: VPFR-lite v2. `CalcVPFRLite` updated to compute VAH/VAL (70% of total volume within the profile) and nearest HVN/LVN walls; adds `VPFRVAH`, `VPFRVAL`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow` to `IndicatorResults`; 2-tier Step 5b cap replaced by 3-tier arbitration (prerequisite for spec #5). |
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

1a. **Indicator/feature specs shipped.** ✅ All six (bid-ask-spread, OFI momentum, dynamic MicroCVD, VPFR-lite v2, swing pivots, settings-exposure) shipped 2026-04-29.
1b. **Accuracy plateau.** ⏳ Engine verdict accuracy must stabilise across 100+ live runs before further structural changes. Calibration accumulation begins after the v0.3 CSV expansion ships (`docs/analysis-log-csv-expansion-proposal.md`).
2. **Settings exposure pass complete.** ✅ `settings-exposure-pass-proposal.md` shipped 2026-04-29 (v17). All 19 formerly-hardcoded scoring literals now reachable through `settings.json`. Auto-tweaker has the full surface area to operate on.
3. **CalibrationReport READY.** ≥300 rows, ≥3 sessions, ≥3 regimes covered, ≥2 liquidation events. Without this, the failure-rate trigger is computed on too small a sample to be trustworthy.
4. **CSV columns expanded.** `VerdictContext`, `FundingMomentum`, `OiCvdPass2bOutcome` columns added (currently in Section 12 backlog). The auto-tweaker reads these to diagnose failure modes.
5. **Failure definition specced.** Concrete answer to "what counts as failure" — see Section 16.1 open questions.

Items 1–4 are tracked in active proposals or Section 12 backlog. Item 5 is the gating spec for 16.1 itself.

### 16.4 Long-Arc Architectural Ceiling

`docs/post-websocket-post-calibration-backlog.md` documents the architectural ceiling for the engine: **WebSocket migration** is the single highest-impact non-indicator upgrade, gating an entire class of microstructure improvements (real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip detection, fine-grained VPFR profile-shape work).

WebSocket itself isn't documented as a future-direction item *here* because it's a foundation rebuild, not a feature addition. It's the binding latency constraint behind several Section A items in the backlog — fix it when (a) the indicator backlog is exhausted and the latency floor becomes the next bottleneck, **or** (b) one of the post-WebSocket items becomes a priority.

The engine should be developed assuming WebSocket arrives eventually. Specifically: avoid hardcoded REST-cadence assumptions in scoring logic. Score thresholds should remain meaningful at higher poll rates. They currently are.

### 16.5 Active Spec Bundle (2026-05-05)

CalibrationReport reached threshold-with-caveats on 2026-05-05 (2460 rows, 4 regimes covered; 0 liquidation events — accepted as rare-event blocker, not gating). User authorised proceeding with the full backlog under explicit priority order:

**Bundle 1 (foundation) — first.**
- `csv-expansion-v0.4-proposal.md` — adds 18 columns (SpreadBps, OFIMomentum, FundingDelta, VPFR-v2 fields, swing fields, TargetCapReason, BestPivotByVolume reservations). Bumps schema. Rotates log.
- `analysis-script-proposal.md` — VB.NET host-agnostic offline analyser at `analysis/`. Forward-return joiner, failure-rate matrix, funding-momentum diagnostic, OFI outlier audit, OI×CVD asymmetry audit. Reachable from MainForm via `lnkAnalysisReport`.

**Bundle 3 (structural refinements) — second.**
- `d1-trend-structure-proposal.md` — HH/HL/LH/LL classification, Pass 2c integration via separate `StructureBonus` (default 1).
- `d2-volume-weighted-pivots-proposal.md` — display-only volume-weighted pivot ranking. v2 cap arbitration parked as observation (see 16.6).
- `b1-per-indicator-regime-weights-proposal.md` — STUB. Blocked on Bundle 1 output for empirical hit rates.

**Bundle 2 (auto-tweaker) — third.**
- `failure-definition-proposal.md` — ATR-based forward-return failure, 3 windows × 3 thresholds (STRONG=tight, MEDIUM=loose), Wilson-CI cell-stability picker.
- `auto-tweaker-pipeline-proposal.md` — VB.NET console app at `tools/AutoTweaker/`, Linux-portable, dry-run mode + manual-apply path, hard rejection list + 3-key scope cap, latest-Opus auto-discovery.

Bundles 4 (small refinements per B4 item) and 5 (multi-session VPFR / anchored VWAP, C1/C2) and 6 (Smart OBV / MFI replacement) deferred until Bundles 1–3 ship and stabilise. Section A (post-WebSocket) and the WebSocket migration itself remain post-Bundle-2 in priority.

### 16.6 Parked Observations (Watch For)

Items not currently scheduled but with concrete promotion conditions to track:

**P1. Promote BestPivotByVolume to cap arbitration (D2 v2).**
*Condition:* in CalibrationReport `BEST VOLUME PIVOT DISTRIBUTION`, the "best is also most-recent" rate falls below 50% AND auto-tweaker output shows volume-weighted pivots correlate with subsequent target-hit rate. Both required.
*Action when triggered:* re-spec `d2-volume-weighted-pivots-v2-proposal.md`. Promote to a 4th cap tier above swing: `best-volume-swing > most-recent-swing > HVN > POC`. Same closest-wins rule.

**P2. Funding momentum threshold v23+ tuning.**
*Condition:* offline analysis FundingMomentumDiagnostic shows FundingDelta percentiles such that a threshold below 1 bp would meaningfully change the RISING/FALLING/FLAT distribution.
*Action when triggered:* simple settings-only `vNN-funding-calibration-pass-N` follow-up. If percentiles show the 1 bp threshold is genuinely above all observed deltas at REST cadence (not just above current sample), accept it as a polling-cadence ceiling and defer fix to WebSocket migration.

**P3. OI×CVD asymmetry — RESOLVED 2026-05-08.**
*Diagnosis:* the asymmetry was upstream of Pass 2b. `MainForm_Analysis.vb`'s `priceUp` computation compared `r.CurrentPrice > bookSummary.Value.MarkPrice * 0.9999` — but `MarkPrice` is the current snapshot of mid + smoothing, tracking the last-traded within ~1bp at any moment. The `* 0.9999` factor introduced a 1bp bias on the threshold, so `priceUp` was True almost always. The classification branches `NEW SHORTS` (OI rose + price fell) and `CAPITULATION` (OI fell + price fell) almost never fired, starving Pass 2b's CONFIRMED_SHORT path.
*Fix:* commit `<TBD>` 2026-05-08. `priceUp` now compares `r.CurrentPrice` against `candles1m(count - 16).Close` — the actual close 15 minutes ago, matching the window of `OIChange15m`. Pass 2b downstream logic was already symmetric; no changes needed there.
*Watch for:* in subsequent CalibrationReports, OI×CVD CONFIRMED_LONG / CONFIRMED_SHORT ratio should normalise toward the regime mix. If TRENDING_DOWN periods now produce more CONFIRMED_SHORT than CONFIRMED_LONG, the fix is validated.

**P4. STRONG/MEDIUM tier collapse in failure-rate matrix.**
*Condition:* after 1000+ tier-eligible rows, both STRONG and MEDIUM matrices pick (window, threshold) combinations within 1 cell of each other.
*Action when triggered:* revise `failure-definition-proposal.md` to a single tier-agnostic matrix. Simpler auto-tweaker, same accuracy.

**P5. Liquidation count window (Section 5c of 2026-05-01 handover).**
*Condition:* CalibrationReport still shows 0 liquidation events 1000+ rows after Bundle 1 ships.
*Action when triggered:* small spec re-introducing `cfg.Indicators.Liquidations.TradeCount` removed in v15. Test 1000-trade window.

**P6. STRUCTURAL_RR_LOW context tag.**
*Condition:* a directional verdict (LONG / STRONG_LONG / SHORT / STRONG_SHORT) fires with the verdict-direction structural R:R below a threshold (default candidate: 1:1). Currently the engine only fires `STRUCTURALLY_WEAK` when no clean target+stop pair can be placed at all — it does not flag the case where both can be placed but the geometry is unfavourable. Observed 2026-05-08: a MEDIUM LONG verdict at 80242 with structural R:R 1:0.5 (risk 452 / reward 211.5) fired without any structural-quality warning, while the contra side (SHORT) had R:R 1:2.1 — directional indicators favoured long, structural geometry favoured short.
*Action when triggered:* spec `structural-rr-low-context-tag-proposal.md`. Add a new VerdictContext value `STRUCTURAL_RR_LOW` (display-only, no scoring impact, parallel to existing tags). Threshold (1:1 default) reads from `cfg.Scoring.ContextTagThresholds`. Precedence vs other tags: fires after `STRUCTURALLY_WEAK` (which is the no-pair case), before `MOMENTUM_FADING` and `FLOW_UNCONFIRMED` (which are flow-quality tags). Defer until auto-tweaker output validates whether low structural R:R correlates with elevated failure rate strongly enough to warrant the tag.
*Watch for:* in the analysis report's Verdict-Context-Tag × Outcome section, runs where structural R:R was below 1:1 (computable from CSV cols 80–83) and the verdict failed under v2 barrier-hit semantics. If the failure rate is materially higher than the population average, promote.

**P7. Live per-analysis success/fail display (configurable windows + sessions).** ✅ RESOLVED 2026-05-13
*Condition:* trader confirmed need 2026-05-12 — settings-snapshot work showed that round-level stats (every ~10–15 min) are insufficient for real-time trading-style adaptation. User wants a panel showing current settings' success/fail % over multiple rolling windows (e.g., last 30 min, last 60 min, current session) updated on every analysis run, not just on auto-tweaker firing.
*Resolution:* implemented per `docs/live-performance-display-proposal.md`. Six-window strip (Cur.Wk / 3d / Cur.Day / Asia / London / NY) in `MainForm`, updated on every `RunAnalysisAsync`. Session most-recent-block semantics (§2b). Reuses `FailureRateMatrix.WalkBars` verbatim. Two gitignored sidecar caches. Settings block `performance_display` added to v26.

**P8. Live performance display — WEAK tier filtering.**
*Condition:* after ~1 week of live data accumulation, if the inclusion of `WEAK LONG` / `WEAK SHORT` verdicts in the success-rate calculation produces visibly different headline rates vs. a STRONG+MEDIUM-only filter, AND the trader observes the WEAK-included rate is misleading (e.g., consistently lower than felt accuracy), revisit the eligibility rule.
*Action when triggered:* small follow-up spec changing `LivePerformanceTracker`'s eligibility filter from "all directional" to "STRONG_* + MEDIUM_*". Optionally expose as a setting `performance_display.tier_filter` with values `all_directional` | `actionable_only`. Add to spec as a one-session amendment.

### 16.7 Portability Constraint Reaffirmed

The Linux CLI port (16.2) is the long-term target. All new code under `analysis/` and `tools/` MUST be host-agnostic — no WinForms references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers (`AnalysisReportForm`, `TweakSettingsForm`) are allowed but must be thin wrappers around host-agnostic core classes. The auto-tweaker console app (`tools/AutoTweaker/AutoTweaker.csproj`) builds as a separate .NET project with **zero WinForms references** by design — it must run unmodified under `dotnet AutoTweaker.dll` on Linux.

This is enforced in `CLAUDE.md` Collaboration Rules and is a hard PR-review check.

The port itself happens **after** auto-tweaker ships AND analysis accuracy reaches a plateau. WebSocket migration is independent — may or may not happen before the port.
