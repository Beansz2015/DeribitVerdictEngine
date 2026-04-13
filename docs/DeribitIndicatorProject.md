# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-13 | Current version: v0.49 (Commit 5 complete)**

This document is the authoritative handover for any new AI conversation continuing this project.
It takes precedence over `indicator-spec.md` wherever the two conflict.

**Session start checklist:** Read this file + `docs/architecture.md`. Do NOT read individual `.vb` files unless a specific edit is required.

---

## 1. Project Purpose

A Windows Forms (VB.NET / .NET 8) desktop application that connects to the Deribit REST API,
calculates a set of technical indicators on live BTC-PERPETUAL data, scores them via a
weighted multi-tier engine, and emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE /
WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

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
| `DynamicNorms.vb` | ATR/Vol/VWAP norm computation |
| `AnalysisLogger.vb` | CSV logging + CalibrationReport |
| `OiSnapshot.vb` | OI ring-buffer helper |
| `AutoRunTimer.vb` | IAutoRunTimer interface + WinFormsAutoRunTimer impl |
| `Program.vb` | Entry point |
| `SettingsLoader.vb` | JSON deserialisation, SettingsLoader.Current singleton |
| `settings.json` | v6 — all tunable parameters (see Section 6) |
| `MainForm.Designer.vb` | Auto-generated WinForms designer (do not edit) |
| `MainForm.resx` | Form resources |

### Core/

| File | Purpose |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. AdjustedLongTarget, AdjustedShortTarget, TargetCapReason), PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore, Threshold, TierFloor, AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus |
| `Core/ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() pipeline (Commit 5 current) |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcEMAList, CalcRSI, CalcRSISeries, CalcRSIDivergence, CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcLiquidations, CalcCVD, CalcMicroCVD, CalcTFI |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite, CalcMTFGate |
| `Core/Settings/EngineSettings.vb` | v0.37 — strongly-typed POCO for settings.json |

### UI/

| File | Version | Purpose |
|---|---|---|
| `UI/MainForm_Layout.vb` | v0.47 | Constants, constructor, resize helpers; shared fields: colour palette, _oiHistory, auto-run state, MTF TTL cache, `_prevRegime` |
| `UI/MainForm_AutoRun.vb` | v0.47 | Auto-run timer lifecycle |
| `UI/MainForm_Analysis.vb` | Commit 5 | RunAnalysisAsync() — full data fetch + indicator + scoring pipeline |
| `UI/MainForm_Render.vb` | v0.46 | RenderOutput(), RTF helpers, CalibrationReport, log helpers |

### Docs

| File | Purpose |
|---|---|
| `docs/DeribitIndicatorProject.md` | This handover document |
| `docs/architecture.md` | Codebase structure, data flow, design decisions |
| `docs/trader-profile.md` | Trader style, indicator preferences, collaboration preferences |
| `docs/verdict-context-tag-proposal.md` | Spec: Verdict Sub-Context Tag (FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK) |
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
| ROC(9) | CalcROCSeries | `cfg.Indicators.ROC.PartialThreshold` (0.1) |
| RSI(9) | CalcRSI | `Overbought` (60) / `Oversold` (40) / `PartialOverbought` (50) / `PartialOversold` (50) |
| RSI Divergence | CalcRSIDivergence | −1 long: BEARISH + RSI > `DivPenaltyRsiHigh` (65); −1 short: BULLISH + RSI < `DivPenaltyRsiLow` (35). `PivotWing` (2), `LookbackBars` (20) from cfg. |
| DMI/ADX | CalcDMI | 5m candles. `cfg.Indicators.ADX.TrendThreshold` (25) |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms. Mid-tier directional partial via cross-confirm. |

### Tier 1
| Indicator | Method | Config keys |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. `cfg.Indicators.VWAP.WarmupCandles` (15) |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED when price between bands |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | `cfg.Scoring.BbwSqueezePenalty` (1); `cfg.Indicators.TTM.FlatThreshold` (0.5) |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor |
| Funding Rate | GetFundingRateAsync | Step 3 deltas from cfg |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL |

### Tier 2
| Indicator | Method | Config keys |
|---|---|---|
| OFI | CalcOFI | `cfg.Indicators.OFI.BookDepth` (3); dominance thresholds from cfg |
| Liquidations | CalcLiquidations | `cfg.Indicators.Liquidations.DominanceRatio` (1.0); penalty magnitudes from cfg |
| CVD | CalcCVD | 3-segment weighted slope (late×2 − early×1). −1 on divergence. |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty. Window=50 via cfg. |
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

`SettingsLoader.Initialise()` called in `MainForm.New()`. `SettingsLoader.Current` returns the singleton. Current file version: **v6**.

```
settings.json
  indicators:
    rsi:           { period, overbought (60), oversold (40),
                     partial_overbought (50), partial_oversold (50),
                     div_penalty_rsi_high (65), div_penalty_rsi_low (35),
                     divergencePriceGate, divergenceRsiDelta,
                     pivot_wing (2), lookback_bars (20) }
    roc:           { period, seriesLookback, partial_threshold (0.1) }
    adx:           { trendThreshold (25), rangeThreshold (20) }
    vwap:          { devThresholdPct, session1/2 times, warmupCandles (15) }
    ofi:           { bookDepth (3), buyDominantRatio (1.2), sellDominantRatio (0.833) }
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize, dominance_ratio (1.0) }
    vpfr:          { num_buckets (50) }
    ttm:           { flat_threshold (0.5) }
    cvd:           { slopeMinUsd, slopePctOfValue, divergencePriceGate, tradeLookback }
    tfi:           { window_size (30), threshold (0.15) }
    microCvd:      { window_size (50), accel_threshold (5000) }
  scoring:
    verdictStrongPct / verdictMedPct / verdictWeakPct
    fundingHighPositive / fundingLowPositive
    fundingHighNegative / fundingLowNegative
    bbw_squeeze_penalty (1)
    liq_standard_penalty (1) / liq_large_penalty (2)
    funding_high_penalty (2) / funding_high_boost (1) / funding_low_penalty (1)
    atr_target_multiplier (2.0) / atr_stop_multiplier (1.2)
    context_tag_structural_min (3) / context_tag_flow_max (1)
  mtfGate:
    enabled, dmiPeriod, requiredConfirms, candleCount
  regimeGates:
    transitionalAdxPenaltyLow / Mid / High
    transitionalPenaltyLow / Mid
```

---

## 7. ScoringEngine — Key Behaviours

- **MaxScore:** 19 (TRENDING), 18 (RANGE_BOUND), 15 (TRANSITIONAL)
- **Verdict thresholds:** `Math.Ceiling(regimeMax * pct)` — no hardcoded magic numbers
- **Step 2:** Score signals into ScoreState → all thresholds from cfg
- **Pass 2:** Upgrade partials on cross-category confirmation; OBV upgrade blocked on adverse divergence
- **Step 3:** Funding modifier
- **Step 4:** Regime veto / TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate veto → NO TRADE
- **Step 4c:** VPFR HVN cap → sets AdjustedLongTarget / AdjustedShortTarget
- **Step 5:** Threshold comparison → verdict
- **Step 5b:** Verdict sub-context tag → sets VerdictContext (FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED). See `docs/verdict-context-tag-proposal.md`.
- **Step 6:** CalcHoldStatus (hold/exit/flip guidance for open positions)
- **Step 7:** ATR target/stop from cfg multipliers

For the full annotated Calculate() pipeline with per-step implementation detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`
- **Last transacted price** = `recentTrades(0).Price` — displayed above ATR block, not used as entry
- Long: Stop = price − (ATR × scale × AtrStopMultiplier), Target = price + (ATR × scale × AtrTargetMultiplier)
- Short: mirrored. R:R = 1:1.7 at current settings (1.2 stop / 2.0 target)
- **HVN cap:** if `v.AdjustedLongTarget > 0` (or Short), raw target shown dimmed; POC-capped target shown in amber bold with reason
- **Multipliers read from cfg** — label and R:R display are dynamic, not hardcoded.

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

---

## 11. DynamicNorms

`DynamicNorms.Compute(candles1m, r.ATR)` computes per-run:
- `ATRScaleFactor` — current ATR vs reference; scales stop/target distances
- `VolHighThreshold` / `VolMidThreshold` — regime-adjusted volume thresholds
- `VWAPDevThreshold` — dynamic VWAP deviation threshold (clamped from settings)

---

## 12. WATCHING / Calibration Backlog

| Item | Description | Priority |
|---|---|---|
| TFI threshold | Evaluate threshold=0.15 vs 0.10 for BTC-PERPETUAL tick size after live data | Low |
| MicroCVD accelThreshold | Default 5000 USD; consider dynamic scaling vs VolumeSMA on quiet sessions | Low |
| AtrTargetMultiplier | Currently 2.0; review against logged R:R after 50+ trades | Low |
| OFI ratio | BuyDominantRatio=1.2 / SellDominantRatio=0.833; review against OFI hit rate in CalibrationReport | Low |
| TTM flatThreshold | Default 0.5; review FLAT vs RISING/FALLING against 1m candle range distribution | Low |
| VPFR numBuckets | Default 50; higher = more POC resolution at cost of sparse buckets on quiet sessions | Low |
| Liq dominanceRatio | Default 1.0; review false signals; consider raising to 1.2–1.5 | Low |
| ContextTag thresholds | ContextTagStructuralMin (3) / ContextTagFlowMax (1) — review FLOW_UNCONFIRMED hit rate after 50+ trades | Low |

---

## 13. Future Upgrades

Ranked by expected accuracy / reliability gain. Items marked ✅ are approved for implementation
when the backlog is clear. Items marked 🔍 require a spec decision before coding begins.

### High-Impact (still meaningful gains — implement next)

| Item | Description | Status |
|---|---|---|
| **Verdict Sub-Context Tag** | A WEAK LONG currently cannot distinguish between three structurally different causes: (1) FLOW_UNCONFIRMED — structural signals aligned but order flow not yet backing the move; (2) MOMENTUM_FADING — RSI extended, MicroCVD decelerating, TTM fading — late-stage entry; (3) STRUCTURALLY_WEAK — no dominant driver, genuine noise. Add a Step 5b `CalcVerdictContext()` pass that inspects already-computed `ScoreState` + `IndicatorResults` and sets `VerdictResult.VerdictContext`. Displayed as `CONTEXT:` line in the verdict block. No new indicators, no new data fetches. **Spec:** `docs/verdict-context-tag-proposal.md` | 🔍 Spec ready — awaiting open question resolution (Section 9 of spec) |
| Adaptive scoring weights by regime | `MaxScore` is regime-adjusted but per-indicator weights are fixed. In TRENDING, EMA ribbon + DMI should carry more weight; in RANGE_BOUND, VWAP bands + Donchian should dominate. Static weights over-score weak signals for the current regime context. Requires per-regime weight multipliers in `EngineSettings` and `ScoringEngine_Calculate`. | 🔍 Spec needed |
| Session-aware volume norms | `VolHighThreshold` / `VolMidThreshold` don't account for time-of-day liquidity cycles. BTC Saturday 04:00 UTC behaves completely differently from London/NY overlap. Segment `DynamicNorms.Compute` by UTC hour bucket to reduce false volume signals during thin sessions. | 🔍 Spec needed |
| Funding rate momentum | Absolute funding rate used as flat step modifier. Funding momentum (rate increasing vs decreasing) is a higher-quality signal — a rising rate approaching the high threshold signals crowding *before* the penalty fires. Add a `FundingMomentum` field (RISING/FALLING/FLAT) to `IndicatorResults` and incorporate into Step 3 modifier logic. | 🔍 Spec needed |
| OI × CVD cross-confirm | OI (NEW LONGS/SHORTS) and CVD direction scored independently. Pairing them as a confirming multiplier (NEW LONGS + CVD RISING = full score; NEW LONGS + CVD FALLING = half score) would sharpen Tier 1 without adding new data sources. | 🔍 Spec needed |

### Moderate-Impact (diminishing returns territory)

| Item | Description | Status |
|---|---|---|
| Dynamic MicroCVD accelThreshold | Hardcoded 5000 USD default is noise during high-volume sessions. Scale as `accelThreshold = VolumeSMA * 0.03` to self-calibrate. Low-risk change — single field in `DynamicNorms.Compute` or at call site. | 🔍 Spec needed |
| RSI divergence on 5m candles | Current divergence is 1m only. A confirmed divergence on both 1m and 5m simultaneously would be a stronger penalty signal and reduce false penalties on 1m micro-noise. Requires `CalcRSIDivergence` called on `candles5m` and a combined gate in `ScoringEngine_Calculate`. | 🔍 Spec needed |
| Donchian × BBW state cross-reference | Wide channel breakout is meaningfully different from a tight-channel breakout. Cross-reference BBW squeeze state (ACTIVE / RELEASING / NONE) when scoring Donchian to up-weight breakouts from compression. | 🔍 Spec needed |

### Fine-Tuning (marginal gains, run after calibration data available)

| Item | Description | Status |
|---|---|---|
| Bid-ask spread microstructure signal | `orderBook` depth is already fetched. Spread between best bid and best ask is an unused fast microstructure signal — sudden widening often precedes a flush. Add `SpreadBps` to `IndicatorResults` and a penalty trigger in Tier 2. | 🔍 Spec needed |
| Auto-tuning from CSV log | Once `CalibrationReport` reaches READY (≥300 rows, ≥3 sessions, ≥3 regimes, ≥2 liq events), build a pass that correlates each signal's vote with subsequent price direction and adjusts `settings.json` weights automatically. | 🔍 Requires calibration data first |

### Accuracy Ceiling Note

The engine is approaching its natural accuracy limit for a **single-instrument 1m scalping system
using REST polling**. By the time a signal is computed, the 1m candle is closed and partially
acted on by faster participants. The three risk thresholds to watch before adding more upgrades:

1. **Overfit risk** — ~25+ tunable parameters already exist. Tuning 6+ of them against the same
   50–100 trade CSV log risks optimising for the past month's regime, not general market behaviour.
2. **Signal redundancy** — OFI + TFI + CVD + MicroCVD already cover order flow from four angles.
   More order flow inputs with high correlation add noise to scoring weight, not signal.
3. **Interpretability** — when a STRONG LONG fires and you can't quickly reason through *why*
   from the breakdown, the engine becomes a black box and you lose the ability to apply
   discretionary override on ambiguous setups.

The highest-impact non-code upgrade at this stage is a **Websocket feed** (real-time order book
and trade stream vs. REST snapshot polling), which would remove the fundamental latency constraint.

---

## 14. Backlog

*(cleared — all Commit 1–5 items shipped)*

---

## 15. Version History

| Version | Key Changes |
|---|---|
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
