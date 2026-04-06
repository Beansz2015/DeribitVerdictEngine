# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-07 | Current version: v0.32**

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
| `MainForm.vb` | v0.32 | UI, RunAnalysisAsync, RenderOutput |
| `Indicators.vb` | v0.32 | All indicator calculations |
| `ScoringEngine.vb` | v0.27 | Verdict scoring, reads all thresholds from settings |
| `DynamicNorms.vb` | current | ATR/Vol/VWAP norm computation |
| `DeribitClient.vb` | current | All Deribit REST calls |
| `AnalysisLogger.vb` | current | CSV logging + CalibrationReport |
| `Core/Settings/EngineSettings.vb` | v0.32 | Strongly-typed POCO for settings.json |
| `SettingsLoader.vb` | current | JSON deserialisation, `SettingsLoader.Current` singleton |
| `OiSnapshot.vb` | current | OI ring-buffer helper |
| `Program.vb` | current | Entry point |
| `settings.json` | v3 | All tunable parameters (see Section 6) |
| `docs/DeribitIndicatorProject.md` | this file | Handover |
| `docs/trader-profile.md` | current | User trading profile |

---

## 4. Architecture Summary

```
MainForm.vb
  └─ RunAnalysisAsync()
       ├─ DeribitClient  → fetches candles1m(250), candles5m(210), funding,
       │                   bookSummary, orderBook(depth10), recentTrades(100)
       ├─ IndicatorEngine → fills IndicatorResults (r)
       │    ├─ CalcATR, CalcROCSeries, CalcRSI, CalcVolumeSMA
       │    ├─ CalcDMI, CalcVWAP (session params from cfg), CalcVWAPBands
       │    ├─ CalcBBW, CalcEMA
       │    ├─ CalcOFI  (top-3 weighted bid/ask)
       │    ├─ CalcLiquidations
       │    ├─ CalcCVD  (cumulative volume delta + slope + divergence)
       │    ├─ CalcDonchian, CalcOBV, CalcRSIDivergence
       │    └─ DynamicNorms.Compute
       ├─ ScoringEngine.Calculate(r, posState, norms, cfg) → VerdictResult
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
| DMI/ADX(9) | CalcDMI | 5m candles, period 9 |
| Volume | CalcVolumeSMA | SMA-9; thresholds from DynamicNorms |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Session boundary times from cfg.Indicators.VWAP; warmup guard from WarmupCandles |
| VWAP Bands | CalcVWAPBands | σ1/σ2 bands; session params from cfg |
| BBW Squeeze | CalcBBW | Period 20, StdDev 2.0; ACTIVE/RELEASING/NONE |
| EMA Ribbon | CalcEMA | 9/21/50; BULL/BEAR/MIXED |
| Funding Rate | GetFundingRateAsync | Info-only; modifies score in Step 3 |
| OI Change | ring buffer | 15m + 60m delta; NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL |

### Tier 2
| Indicator | Method | Notes |
|---|---|---|
| OFI | CalcOFI | Top-3 depth levels, weights 3/2/1; BUY/SELL/NEUTRAL |
| Liquidations | CalcLiquidations | Penalty-only; large threshold from cfg |
| CVD | CalcCVD | Net delta + slope + divergence flag; penalty -1 on divergence |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | LONG/SHORT/NONE |
| OBV | CalcOBV | TrendGate+DivergenceGate from cfg |

---

## 6. settings.json Structure (v3)

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
    vwap:          { devThresholdPct,
                     session1StartHour, session1StartMinute,   ← daily session reset (00:00 UTC)
                     session2StartHour, session2StartMinute,   ← US session reset (13:30 UTC)
                     warmupCandles }                           ← min candles before VWAP is scored
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize }
    cvd:           { slopeMinUsd, slopePctOfValue,
                     divergencePriceGate, tradeLookback }
  scoring:
    verdictStrongPct   -- fraction of regimeMax to trigger STRONG (e.g. 0.70)
    verdictMedPct      -- fraction for MED verdict
    verdictWeakPct     -- fraction for WEAK verdict
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
- **Step 2:** Score each signal FULL (+1 each side) into a `ScoreState`
- **Pass 2:** Upgrade partials when cross-category confirmation exists
- **Step 3:** Funding modifier adjusts L/S scores
- **Step 4:** Regime veto (TRENDING blocks counter-trend) or TRANSITIONAL ADX penalty
- **Step 5:** Compare effectiveLS/SS to thresholds → Verdict + Confidence
- **Step 6:** CalcHoldStatus for open position guidance
- CVD divergence penalty: -1 applied before liquidation penalty
- Liquidation penalty: -1 or -2 (large) depending on `cfg.Indicators.Liquidations.LargeLiqSize`

---

## 8. DynamicNorms

Computed from the last 250 × 1m candles on each analysis run.
Provides live adaptive thresholds for Volume and VWAP deviation,
and an ATR scale factor. Falls back to static defaults if insufficient data.

---

## 9. AnalysisLogger

Appends one CSV row per `Analyze Now` click to `analysis_log.csv` (next to exe).
Calibration Readiness Report (bottom-left link) reads the CSV and checks:
- ≥ 300 total rows
- ≥ 3 sessions (days)
- ≥ 3 regimes with ≥ 50 rows each
- ≥ 2 liquidation events

---

## 10. Documentation Requirements (for AI continuations)

When continuing this project in a new conversation, the AI **must** follow these rules:

### a. Always read this file first
Fetch `docs/DeribitIndicatorProject.md` from GitHub at the start of every session.
This file overrides `indicator-spec.md` on all points of conflict.

### b. Update this file at the end of every session
After any code change is pushed to GitHub, update this handover doc with:
- Bumped version number in the header
- Updated version in the File Inventory table (Section 3)
- A summary of what changed and why in a new entry at the bottom (Section 11)

### c. Condition tracking
For any feature that requires a live condition to be observed before it can be
called complete (e.g. "verify CVD divergence penalty fires correctly on a real
bearish divergence"), add it to Section 12 (Pending Observations) with status
`WATCHING`. Mark it `COMPLETE` once the condition has been seen and confirmed.

### d. GitHub push method
If `push_files` fails, fall back to `create_or_update_file` one file at a time.
Always read the current file SHA before updating (use `get_file_contents`).

### e. Version numbering
- `MainForm.vb`: mirrors the app version shown in `Me.Text`
- `ScoringEngine.vb`: independent minor version (currently v0.27)
- `Indicators.vb`: independent minor version (currently v0.32)
- `EngineSettings.vb`: mirrors Indicators version (currently v0.32)
- When any file changes, bump only that file's version

---

## 11. Change Log

| Version | Date | Changes |
|---|---|---|
| v0.01–v0.20 | (prior sessions) | Initial build: core indicators, DMI, VWAP, EMA ribbon, OI, OFI, Donchian, BBW, calibration log |
| v0.21–v0.25 | (prior sessions) | DynamicNorms, dual-score engine, regime veto, partial upgrade logic, MaxScore regime-aware |
| v0.26 | (prior sessions) | OBV + RSI divergence + CVD added to Indicators; CVD scoring block in ScoringEngine |
| v0.27 (ScoringEngine) | 2026-04-06 | All verdict/penalty thresholds now read from EngineSettings (settings.json) |
| v0.28 (MainForm, Indicators) | 2026-04-06 | OFI call site updated for top-3 weighted signature; OFI display shows bid/ask weighted volumes |
| v0.29 (MainForm) | 2026-04-06 | CalcCVD call site added; CVD display line in TIER 2; SettingsLoader.Initialise() in constructor |
| v0.30 (MainForm, EngineSettings, Indicators) | 2026-04-06 | CalcOBV/CalcRSIDivergence/CalcROCSeries pass settings-driven gate params; ScoringEngine.Calculate passes cfg as 4th arg; EngineSettings CvdSettings class added |
| v0.31 (MainForm, Indicators) | 2026-04-06 | CalcVWAP captures sessionCandleCount via ByRef; CalcVWAPBands added (σ1/σ2); VWAP display shows bands, session candle count, warmup tag |
| v0.32 (MainForm, Indicators, EngineSettings, settings.json) | 2026-04-07 | VWAP session boundary times (session2Hour/Minute) moved from hardcoded 13:30 to settings.json; warmup threshold moved from hardcoded 15 to settings.json; VwapSettings class expanded with Session1/2StartHour/Minute + WarmupCandles; CalcVWAP and CalcVWAPBands accept these as parameters; MainForm reads from cfg.Indicators.VWAP |

---

## 12. Pending Observations

| Feature | Condition to observe | Status |
|---|---|---|
| CVD divergence penalty | See a real BEARISH or BULLISH divergence fire in live output and confirm -1 penalty appears in signal breakdown | WATCHING |
| Transitional ADX penalty | Run analysis during a TRANSITIONAL regime and confirm effective score reduced correctly | WATCHING |
| Calibration log saturation | Accumulate 300+ rows across 3+ sessions to trigger READY FOR RECALIBRATION | WATCHING |

---

## 13. Known Gaps / Not Yet Built

- Automated backtesting harness (manual log review only for now)
- Alert/notification system (no audio or push alerts)
- Multi-instrument support (BTC-PERPETUAL only)
- Settings UI (edit settings.json manually)
