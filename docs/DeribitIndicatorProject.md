# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-08 | Current version: v0.36**

This document is the authoritative handover for any new AI conversation continuing this project.
It takes precedence over `indicator-spec.md` wherever the two conflict.

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

## 3. File Inventory & Current Versions

| File | Version | Notes |
|---|---|---|
| `MainForm.vb` | v0.36 | UI, RunAnalysisAsync, RenderOutput, MTF gate wiring, ATR display |
| `Indicators.vb` | v0.36 | All indicator calculations incl. CalcMTFGate |
| `ScoringEngine.vb` | v0.32 | Verdict scoring, MTF veto, OBV scoring fix |
| `DynamicNorms.vb` | current | ATR/Vol/VWAP norm computation |
| `DeribitClient.vb` | current | All Deribit REST calls incl. 15m candles |
| `AnalysisLogger.vb` | current | CSV logging + CalibrationReport |
| `Core/Settings/EngineSettings.vb` | v0.33 | Strongly-typed POCO for settings.json incl. MTFGateSettings |
| `SettingsLoader.vb` | current | JSON deserialisation, `SettingsLoader.Current` singleton |
| `OiSnapshot.vb` | current | OI ring-buffer helper |
| `Program.vb` | current | Entry point |
| `settings.json` | current | All tunable parameters (see Section 6) |
| `docs/DeribitIndicatorProject.md` | this file | Handover |
| `docs/trader-profile.md` | current | User trading profile |

---

## 4. Architecture Summary

```
MainForm.vb
  └─ RunAnalysisAsync()
       ├─ DeribitClient  → fetches candles1m(250), candles5m(210), candles15m(100),
       │                   funding, bookSummary, orderBook(depth10), recentTrades(100)
       ├─ IndicatorEngine → fills IndicatorResults (r)
       │    ├─ CalcATR, CalcROCSeries, CalcRSI, CalcRSIDivergence, CalcVolumeSMA
       │    ├─ CalcDMI, CalcVWAP (session params from cfg), CalcVWAPBands
       │    ├─ CalcBBW (incl. TTM Squeeze momentum direction)
       │    ├─ CalcEMA (1m ribbon + 5m EMA200)
       │    ├─ CalcOFI  (top-3 weighted bid/ask imbalance)
       │    ├─ CalcLiquidations
       │    ├─ CalcCVD  (cumulative volume delta + slope + divergence)
       │    ├─ CalcDonchian, CalcOBV
       │    ├─ CalcMTFGate (15m DMI/ADX + EMA alignment confluence gate)
       │    └─ DynamicNorms.Compute
       ├─ ScoringEngine.Calculate(r, posState, norms, cfg) → VerdictResult
       │    └─ MTF veto: forces NO TRADE if MTFGatePass = False
       ├─ AnalysisLogger.LogRun
       └─ RenderOutput → txtOutput + lblVerdict colour
```

---

## 5. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Notes |
|---|---|---|
| ROC(9) | CalcROCSeries | Lookback from cfg.Indicators.ROC.SeriesLookback |
| RSI(9) | CalcRSI | Period from cfg.Indicators.RSI.Period |
| RSI Divergence | CalcRSIDivergence | Price gate + RSI delta gate from settings; displayed in breakdown |
| DMI/ADX | CalcDMI | 5m candles, period from settings |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Session boundary times from cfg.Indicators.VWAP; warmup guard |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED logic when price between bands |
| BBW / TTM Squeeze | CalcBBW | BBW status (ACTIVE/RELEASING/NONE) + TTM histogram direction (RISING/FALLING) and signal (BULL_BUILDING / BULL_FADING / BEAR_BUILDING / BEAR_FADING) |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor |
| Funding Rate | GetFundingRateAsync | Info-only; modifies long/short scores in Step 3 |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL |

### Tier 2
| Indicator | Method | Notes |
|---|---|---|
| OFI | CalcOFI | Top-3 depth levels, bid/ask imbalance, weights 3/2/1; shows weighted bid vol, ask vol, ratio; BUY/SELL/NEUTRAL |
| Liquidations | CalcLiquidations | Penalty-only; large threshold from cfg |
| CVD | CalcCVD | Net delta + slope (RISING/FALLING/FLAT) + divergence (BULLISH/BEARISH/NONE); −1 penalty on divergence |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | LONG/SHORT/NONE breakout |
| OBV | CalcOBV | Trend gate + divergence gate from cfg; full score when trend aligned + divergence non-adverse |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK only — not a score contributor; forces NO TRADE on BLOCK |

---

## 6. settings.json Structure

All scoring and indicator gate parameters are externalised here.
`SettingsLoader.Initialise()` is called in `MainForm.New()` and loads
`settings.json` from the exe directory. `SettingsLoader.Current` returns
the singleton `EngineSettings` instance.

Key sections:
```
settings.json
  indicators:
    rsi:           { period, divergencePriceGate, divergenceRsiDelta }
    roc:           { period, seriesLookback }
    adx:           { trendThreshold }                               ← used by MTF gate for ADX min
    vwap:          { devThresholdPct,
                     session1StartHour, session1StartMinute,
                     session2StartHour, session2StartMinute,
                     warmupCandles }
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize }
    cvd:           { slopeMinUsd, slopePctOfValue,
                     divergencePriceGate, tradeLookback }
  mtfGate:
    enabled        -- bool; if false the gate is bypassed
    dmiPeriod      -- DMI/ADX period for 15m calc
    requiredConfirms -- number of bull/bear confirms required to pass
    candleCount    -- 15m candle lookback
  scoring:
    verdictStrongPct / verdictMedPct / verdictWeakPct
    fundingHighPositive / fundingLowPositive
    fundingHighNegative / fundingLowNegative
  regimeGates:
    transitionalAdxPenaltyLow / Mid / High
    transitionalPenaltyLow / Mid
```

---

## 7. ScoringEngine Logic Summary

- **MaxScore** = 17 (TRENDING), 16 (RANGE_BOUND), 13 (TRANSITIONAL)
- Verdict thresholds: `Math.Ceiling(regimeMax * pct)` using `verdictStrong/Med/WeakPct`
- **Weighted scoring:** each signal category contributes a defined max to the long or short
  score pool. Partial scores are awarded when only one side of a cross-category confirm
  exists; full score on alignment (e.g. RSI partial + EMA partial → full long point).
- **Step 2:** Score each signal into a `ScoreState` (long pts / short pts)
- **Pass 2:** Upgrade partials when cross-category confirmation exists
- **Step 3:** Funding modifier adjusts L/S scores
- **Step 4:** Regime veto (TRENDING blocks counter-trend) or TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate check — if `cfg.MTFGate.Enabled` and `r.MTFGatePass = False`,
  proposed direction is vetoed → verdict forced to NO TRADE; gate reason added to breakdown
- **Step 5:** Compare effectiveLS/SS to thresholds → Verdict + Confidence
- **Step 6:** CalcHoldStatus for open position guidance
- CVD divergence penalty: −1 before liquidation penalty
- Liquidation penalty: −1 standard, −2 large
- OBV scoring fix (v0.32): RISING/FALLING trend + non-adverse divergence = full score;
  adverse divergence = partial-upgrade only

---

## 8. ATR Entry / Stop / Target Display

Displayed at the top of every analysis run output:
- ATR value (from CalcATR on 1m candles) and current ATR scale factor (vs DynamicNorms ref ATR)
- Long scenario: Stop = price − (ATR × scale × stopMult), Entry = current price,
  Target = price + (ATR × scale × targetMult)
- Short scenario: mirrored
- R:R ratio and absolute risk/reward in USD displayed
- All multipliers (stopMult, targetMult) from settings.json

---

## 9. DynamicNorms

Computed from the last 250 × 1m candles on each analysis run.
Provides live adaptive thresholds for Volume and VWAP deviation,
and an ATR scale factor. Falls back to static defaults if insufficient data.

---

## 10. AnalysisLogger

Appends one CSV row per `Analyze Now` click to `analysis_log.csv` (next to exe).
Calibration Readiness Report checks:
- ≥ 300 total rows
- ≥ 3 sessions (days)
- ≥ 3 regimes with ≥ 50 rows each
- ≥ 2 liquidation events

---

## 11. Documentation Requirements (for AI continuations)

When continuing this project in a new conversation, the AI **must** follow these rules:

### a. Always read this file first
Fetch `docs/DeribitIndicatorProject.md` from GitHub at the start of every session.
This file overrides `indicator-spec.md` on all points of conflict.

### b. Update this file at the end of every session
After any code change is pushed to GitHub, update this handover doc with:
- Bumped version number in the header
- Updated version in the File Inventory table (Section 3)
- A summary of what changed and why in a new entry at the bottom (Section 13)

### c. Condition tracking
For any feature that requires a live condition to be observed before it can be
called complete, add it to Section 14 (Pending Observations) with status `WATCHING`.
Mark it `COMPLETE` once confirmed.

### d. GitHub push method
If `push_files` fails, fall back to `create_or_update_file` one file at a time.
Always read the current file SHA before updating (use `get_file_contents`).

### e. Version numbering
- `MainForm.vb`: mirrors the app version shown in `Me.Text`
- `ScoringEngine.vb`: independent minor version (currently v0.32)
- `Indicators.vb`: independent minor version (currently v0.36)
- `EngineSettings.vb`: currently v0.33
- When any file changes, bump only that file's version

---

## 12. Known Gaps / Not Yet Built

- Automated backtesting harness (manual log review only for now)
- Alert/notification system (no audio or push alerts)
- Multi-instrument support (BTC-PERPETUAL only)
- Settings UI (edit settings.json manually)

---

## 13. Change Log

| Version | Date | Changes |
|---|---|---|
| v0.01–v0.20 | (prior sessions) | Initial build: core indicators, DMI, VWAP, EMA ribbon, OI, OFI, Donchian, BBW, calibration log |
| v0.21–v0.25 | (prior sessions) | DynamicNorms, dual-score engine, regime veto, partial upgrade logic, MaxScore regime-aware |
| v0.26 | (prior sessions) | OBV + RSI divergence + CVD added to Indicators; CVD scoring block in ScoringEngine |
| v0.27 (ScoringEngine) | 2026-04-06 | All verdict/penalty thresholds now read from EngineSettings |
| v0.28 (MainForm, Indicators) | 2026-04-06 | OFI call site updated for top-3 weighted bid/ask imbalance signature; display shows weighted volumes |
| v0.29 (MainForm) | 2026-04-06 | CalcCVD call site added; CVD display line in TIER 2; SettingsLoader.Initialise() in constructor |
| v0.30 (MainForm, EngineSettings, Indicators) | 2026-04-06 | CalcOBV/CalcRSIDivergence/CalcROCSeries pass settings-driven gate params; ScoringEngine.Calculate passes cfg as 4th arg; EngineSettings CvdSettings class added |
| v0.31 (MainForm, Indicators) | 2026-04-06 | CalcVWAP captures sessionCandleCount via ByRef; CalcVWAPBands added (σ1/σ2); VWAP display shows bands, session candle count, warmup tag |
| v0.32 (MainForm, Indicators, EngineSettings, settings.json) | 2026-04-07 | VWAP session boundary times and warmup threshold moved to settings.json; VwapSettings class expanded; CalcVWAP/CalcVWAPBands parameterised |
| v0.33 (EngineSettings) | 2026-04-08 | MTFGateSettings class added (Enabled, DmiPeriod, RequiredConfirms, CandleCount); MTFGate property on EngineSettings |
| v0.34–v0.35 (MainForm, Indicators) | 2026-04-08 | CalcMTFGate implemented (15m DMI/ADX + EMA alignment); MTFGatePass/MTFGateReason fields added to IndicatorResults; MainForm fetches 15m candles, computes proposed direction, calls CalcMTFGate |
| v0.36 (MainForm, Indicators) | 2026-04-08 | Build fixes: MTFGateSettings property names corrected (DmiPeriod, RequiredConfirms, CandleCount); ADX threshold mapped to cfg.Indicators.ADX.TrendThreshold; EMA scoring block syntax fixed |
| v0.32 (ScoringEngine) | 2026-04-08 | MTF gate veto block added; OBV scoring fix (aligned trend + non-adverse divergence = full score) |

---

## 14. Pending Observations / Calibration Watchlist

| Feature | Condition to observe | Status |
|---|---|---|
| CVD divergence penalty | See a live BEARISH or BULLISH divergence and confirm −1 penalty appears in scoring breakdown | WATCHING |
| Transitional ADX penalty | Run during a TRANSITIONAL regime and confirm tiered penalty applied correctly | WATCHING |
| Calibration log saturation | Accumulate 300+ rows across 3+ sessions to trigger READY FOR RECALIBRATION | WATCHING |
| MTF weak-15m pass | Review whether PASS should require stricter confirmation when 15m ADX is below trend-strength threshold (currently passes on absence of bearish evidence, not presence of bullish evidence) | WATCHING |
