# SwingFallbackRead output: --mode rescore (re-score reconstruction)

- Run at (UTC): 2026-09-16 19:33:42
- Pooled log: C:\Dev\DeribitVerdictEngine\AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv
- Box logs: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv.v0.7.bak + C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv
- Eval cache: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_eval_cache.csv
- settings.json version 68, sha256 A059DEC578D8B4C7 (copy identical)
- Session ASIA: hours 0-7 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 1.25xATR
- Session LONDON: hours 8-12 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 2xATR
- Session NY: hours 13-23 UTC inclusive, execution_resolution 1, windows 5/10/15 min, fallback target 1.75xATR
- Trading week: Monday 00:00 UTC to Friday 24:00 UTC (exclusive)
- Fees: style maker_maker, round trip 3.00 bps (maker 1.5, taker 3.5). Taker-stop case: 3.00 bps on a target hit, 5.00 bps on a stop hit or a marked exit

- Brief: docs/medium-tier-diagnosis-brief-2026-09-16.md section 2.1. Read: docs/medium-tier-bug-hunt-2026-09-16.md.
- LiqSignal is taken as logged (finding L-1, orchestrator ruling D-7).

## 1. Row funnel

| Step | Rows |
|---|---|
| Merged rows (pooled book + box live log, first timestamp wins) | 51421 |
| Before the v51 geometry boundary | 314 |
| Non-collector instance rows inside the collector era | 12 |
| Outside the trading week | 11501 |
| No session bucket | 0 |
| No raw columns or no logged VPFRVAH/VPFRVAL | 0 |
| No candle window | 0 |
| **Rows re-scored** | 39594 |
| ... of which swing read population rows | 8810 |
| Swing read population, for reference | 8810 |

Candle coverage:

| Resolution | Store files read | Bars from the store | Bars from this instrument's cache | Bars fetched this run | Bars missing in the needed span |
|---|---|---|---|---|---|
| 1m | candles_1m_2026-07.csv | 42937 | 62184 | 0 | 0 of 97392 |
| 3m | candles_3m_2026-07.csv | 14313 | 20728 | 0 | 0 of 32638 |

## 2. Settings eras

| Version | Commit | Era edge (commit time, UTC) | Rows assigned | Norms settings group | structural_levels.enabled | value_area_scoring_enabled |
|---|---|---|---|---|---|---|
| v51 | 9ab3f04 | 2026-07-06 13:08:51 | 2574 | 1 | True | False |
| v52 | 3bc2a1e | 2026-07-14 14:19:52 | 561 | 1 | True | False |
| v53 | 1811b8d | 2026-07-15 15:07:43 | 974 | 1 | True | False |
| v54 | ae6678c | 2026-07-17 13:38:39 | 763 | 1 | True | False |
| v55 | 4eef0d8 | 2026-07-21 14:24:00 | 304 | 1 | True | False |
| v56 | 8c497e3 | 2026-07-21 16:54:33 | 36 | 1 | True | False |
| v57 | 5f0b6b6 | 2026-07-21 17:30:08 | 303 | 1 | True | False |
| v58 | bd31a1a | 2026-07-22 09:35:58 | 6 | 2 | True | False |
| v59 | 46e7614 | 2026-07-22 09:54:17 | 340 | 2 | True | False |
| v60 | 631a3f5 | 2026-07-22 17:35:47 | 104 | 2 | True | False |
| v61 | e9e6407 | 2026-07-22 19:19:50 | 2505 | 2 | True | False |
| v62 | ce4ce37 | 2026-07-27 15:01:55 | 1931 | 2 | True | False |
| v63 | 74f77a0 | 2026-07-29 16:30:51 | 1047 | 2 | True | False |
| v64 | 1229a30 | 2026-07-30 18:43:32 | 1237 | 2 | True | False |
| v65 | 970087b | 2026-08-01 18:45:00 | 4973 | 2 | True | False |
| v66 | fd52299 | 2026-08-10 18:06:57 | 6622 | 2 | True | False |
| v67 | 613cf1e | 2026-08-20 14:10:09 | 1050 | 2 | True | False |
| v68 | aa0e6e7 | 2026-08-21 16:20:29 | 14264 | 2 | True | False |

## 3. Input provenance (every IndicatorResults field ScoringEngine.Calculate reads)

| Field | Source |
|---|---|
| CurrentPrice | logged `Price` (F2) |
| ROC, RSI, PlusDI, MinusDI, ADX, VolumeRatio | logged, same names (F4 / F2) |
| ROCSlope, RSIDivergence, Regime, SqueezeStatus, TTMSignal, TTMDirection, EMAAlignment, FundingBias, FundingMomentum, OISignal, OFISignal, OFIMomentum, CVDSlope, CVDDivergence, TFISignal, AggrVelSignal, MicroCVDSignal, MicroCVDMomentum, LiqSignal, DonchianSignal, OBVTrend, OBVDivergence, MTF15mTrend | logged labels, same names |
| TrendStructure | logged `TrendStructure5m` |
| MTFGatePassLong, MTFGatePassShort | logged True / False |
| VWAP, VWAPSigma1/2 Upper/Lower, VWAPSessionCandles, EMA200_5m | logged (F2 / integer) |
| FundingRate | logged (F8) |
| CVDValue, MicroCVDEarly/Mid/Late | logged (F0) |
| LiqLongSize, LiqShortSize | logged (F2); all zero with LiqSignal NONE (finding L-1) |
| ATR, swing levels, VPFRNearestHvnAbove/Below, BestPivotByVolume5m | logged (F4 / F2); read by the placed-level arbitration and Step 5c |
| ExecResolution | logged integer |
| SpreadStatus | DERIVED: shipped `ClassifySpread` on logged `SpreadBps` with the era's thresholds |
| RocMagnitudeThreshold | DERIVED: shipped `ExecutionResolution.ResolveRocMagnitudeForHour` on the row's UTC hour |
| SessionUtcHour | DERIVED: the logged timestamp's hour (boundary rows tested with the previous hour) |
| VPFRSignal, VPFRPoc | RECOMPUTED: shipped `CalcVPFRLite` on exchange candles, verified against four logged VPFR fields |
| DynamicNorms (VolHighThreshold, VolMidThreshold) | RECOMPUTED: shipped `DynamicNorms.Compute` on the same candle window under the era's settings |
| VPFRValueAreaSignal, VPFRNearestLvnAbove/Below | RECOMPUTED with VPFRSignal (value-area scoring is disabled in every era: section 2) |
| LastTwoHighs5m, LastTwoLows5m, MTFGateDetails | NOT SET: read only into breakdown notes and MTFGateReason text, never into points |

- VPFR profile verified on 38665 of 39594 rows (97.7 %); population 8508 of 8810. Unverified rows use the first window variant's recompute.

## 4. Match rate per field (primary re-score: the row's era, recomputed VPFR and norms)

| Field | Population rows matching | Population % | All rows matching | All rows % |
|---|---|---|---|---|
| LongScore | 8773 of 8810 | 99.58 | 39438 of 39594 | 99.61 |
| ShortScore | 8777 of 8810 | 99.63 | 39445 of 39594 | 99.62 |
| EffectiveLongScore | 8772 of 8810 | 99.57 | 39424 of 39594 | 99.57 |
| EffectiveShortScore | 8775 of 8810 | 99.60 | 39425 of 39594 | 99.57 |
| RegimePenalty | 8808 of 8810 | 99.98 | 39572 of 39594 | 99.94 |
| Verdict | 8792 of 8810 | 99.80 | 39562 of 39594 | 99.92 |
| **All six** | 8749 of 8810 | 99.31 | 39331 of 39594 | 99.34 |

| Secondary field | All rows matching | All rows % |
|---|---|---|
| MaxScore | 39594 of 39594 | 100.00 |
| VerdictContext | 39570 of 39594 | 99.94 |
| OiCvdOutcome | 39594 of 39594 | 100.00 |
| Placed levels | 39591 of 39594 | 99.99 |
- Ledger guard (CheckLedger) mismatches in the primary re-score: 0.

| Secondary mismatch | Rows | ... with all six fields matching | ... population rows | ... VPFR profile verified |
|---|---|---|---|---|
| MaxScore | 0 | 0 | 0 | 0 |
| VerdictContext | 24 | 2 | 0 | 22 |
| OiCvdOutcome | 0 | 0 | 0 | 0 |
| Placed levels | 3 | 2 | 0 | 3 |

| Placed-level mismatch (UTC) | Six match | Population | VPFR verified | Recomputed label | Logged long target / stop | Re-scored long target / stop | Logged short target / stop | Re-scored short target / stop |
|---|---|---|---|---|---|---|---|---|
| 2026-07-06 13:09:12 | yes | no | yes | NEAR_HVN_RESIST | 61784.99 / 61617.01 | 61771.86 / 61596.01 (FALLBACK_ATR) | 61653.58 / 61742.99 | 61653.58 / 61763.99 (NEAREST_HVN_BELOW) |
| 2026-07-23 22:24:06 | yes | no | yes | NEAR_HVN_RESIST | 65140.50 / 65068.71 | 65178.00 / 65068.71 (SWING_HIGH_5M) | 65068.80 / 65137.29 | 65068.80 / 65137.29 (NEAREST_HVN_BELOW) |
| 2026-08-14 18:05:19 | no | no | yes | NEAR_HVN_RESIST | 63023.84 / 62959.06 | 63023.84 / 62959.06 (FALLBACK_ATR) | 62956.16 / 63020.94 | 62922.32 / 63020.94 (NEAREST_HVN_BELOW) |

| Session | Era | Rows | All six match | % |
|---|---|---|---|---|
| NY | v51 | 2181 | 2143 | 98.26 |
| NY | v52 | 408 | 407 | 99.75 |
| NY | v53 | 632 | 628 | 99.37 |
| NY | v54 | 604 | 600 | 99.34 |
| NY | v55 | 304 | 301 | 99.01 |
| NY | v56 | 36 | 36 | 100.00 |
| NY | v57 | 215 | 213 | 99.07 |
| NY | v59 | 278 | 275 | 98.92 |
| NY | v60 | 104 | 104 | 100.00 |
| NY | v61 | 1725 | 1709 | 99.07 |
| NY | v62 | 1411 | 1398 | 99.08 |
| NY | v63 | 787 | 782 | 99.36 |
| NY | v64 | 977 | 974 | 99.69 |
| NY | v65 | 3613 | 3592 | 99.42 |
| NY | v66 | 4849 | 4812 | 99.24 |
| NY | v67 | 790 | 786 | 99.49 |
| NY | v68 | 10370 | 10314 | 99.46 |
| LONDON | v51 | 290 | 289 | 99.66 |
| LONDON | v52 | 83 | 83 | 100.00 |
| LONDON | v53 | 197 | 197 | 100.00 |
| LONDON | v54 | 133 | 133 | 100.00 |
| LONDON | v57 | 32 | 32 | 100.00 |
| LONDON | v58 | 6 | 6 | 100.00 |
| LONDON | v59 | 62 | 62 | 100.00 |
| LONDON | v61 | 300 | 298 | 99.33 |
| LONDON | v62 | 200 | 200 | 100.00 |
| LONDON | v63 | 100 | 99 | 99.00 |
| LONDON | v64 | 100 | 99 | 99.00 |
| LONDON | v65 | 560 | 559 | 99.82 |
| LONDON | v66 | 654 | 650 | 99.39 |
| LONDON | v67 | 100 | 99 | 99.00 |
| LONDON | v68 | 1496 | 1490 | 99.60 |
| ASIA | v51 | 103 | 103 | 100.00 |
| ASIA | v52 | 70 | 70 | 100.00 |
| ASIA | v53 | 145 | 142 | 97.93 |
| ASIA | v54 | 26 | 26 | 100.00 |
| ASIA | v57 | 56 | 56 | 100.00 |
| ASIA | v61 | 480 | 477 | 99.38 |
| ASIA | v62 | 320 | 320 | 100.00 |
| ASIA | v63 | 160 | 160 | 100.00 |
| ASIA | v64 | 160 | 160 | 100.00 |
| ASIA | v65 | 800 | 796 | 99.50 |
| ASIA | v66 | 1119 | 1111 | 99.29 |
| ASIA | v67 | 160 | 159 | 99.38 |
| ASIA | v68 | 2398 | 2381 | 99.29 |

## 5. Mismatch explanations

Every single-cause test runs on every mismatching row; a test EXPLAINS the row when it reproduces all six fields. The primary class is the first explaining kind in this precedence:

| Code | Kind |
|---|---|
| P | logged precision (one input moved by half its print unit) |
| H | SessionUtcHour boundary (run started in the previous UTC hour) |
| F | VPFR forming-bar volume (the live forming bar carried volume; the variant still verifies the four logged VPFR fields) |
| V | unlogged VPFR label (another label, no verification) |
| N | unlogged volume thresholds |
| VN | unlogged VPFR label and volume thresholds together |
| EA | adjacent settings era within 72 h of its edge (deploy or hot-reload timing) |
| B | burst modifier not applied (AggrVelSignal read as NORMAL) |
| E | other settings era, not adjacent |

### 5.1 Primary class

| Primary class | Population rows | All rows | Examples (UTC, detail) |
|---|---|---|---|
| VPFR forming-bar volume (the live forming bar carried volume; the variant still verifies the four logged VPFR fields) | 39 | 160 | 2026-07-07 14:19:03 volume share 0.25, high/low open and Price, label NEAR_HVN_SUPPORT -> NEAR_HVN_RESIST; 2026-07-07 14:59:01 volume share 0.25, high/low open and Price, label IN_LVN_BULL -> NEUTRAL; 2026-07-07 19:02:02 volume share 0.25, high/low open and Price, label IN_LVN_BEAR -> NEUTRAL; 2026-07-07 19:55:01 volume share 0.25, high/low open and Price, label NEUTRAL -> IN_LVN_BULL |
| unlogged VPFR label (another label, no verification) | 16 | 56 | 2026-07-07 17:30:01 unverified profile, vote LONG -> none; 2026-07-07 17:35:02 unverified profile, vote LONG -> none; 2026-07-07 17:36:04 unverified profile, vote LONG -> none; 2026-07-07 18:10:05 unverified profile, vote SHORT -> LONG |
| logged precision (one input moved by half its print unit) | 6 | 47 | 2026-07-13 19:20:12 ADX (F2); 2026-07-20 17:05:04 ADX (F2); 2026-07-22 20:34:02 RSI (F2); 2026-07-24 13:30:01 Price (F2) |

### 5.2 Explanation sets (which kinds explain the same row)

| Explaining kinds | Population rows | All rows |
|---|---|---|
| F + V | 31 | 128 |
| V | 12 | 50 |
| P | 4 | 32 |
| F + V + B + E | 3 | 16 |
| F + V + N | 4 | 11 |
| P + N | 1 | 6 |
| P + V | 1 | 6 |
| F + V + N + B + E | 0 | 4 |
| V + N | 3 | 4 |
| P + B + E | 0 | 2 |
| V + B + E | 1 | 2 |
| F + V + N + EA + E | 1 | 1 |
| P + VN + B + E | 0 | 1 |

| Kind | Rows it explains (all rows) | ... and no input-uncertainty kind (P, H, F, V, N, VN) explains them |
|---|---|---|
| P | 47 | 0 |
| H | 0 | 0 |
| F | 160 | 0 |
| V | 222 | 0 |
| N | 26 | 0 |
| VN | 1 | 0 |
| EA | 1 | 0 |
| B | 25 | 0 |
| E | 26 | 0 |

### 5.3 Burst-modifier rows against the rest

A burst-modifier row is one where the re-score's TFI breakdown note shows the burst modifier applied (confirm or contra).

| Rows | Re-scored | Mismatching | % | Explained by F | Explained by V | Explained by B |
|---|---|---|---|---|---|---|
| Burst modifier applied | 3101 | 76 | 2.45 | 59 | 68 | 25 |
| No burst modifier | 36493 | 187 | 0.51 | 101 | 154 | 0 |

### 5.4 Rows no input-uncertainty kind explains (the defect candidates)

- None.

## 6. Attribution file for session 2

- Path: C:\Dev\DeribitVerdictEngine\backtest_data\swing-fallback-read\rescore-attribution.csv (gitignored). One line per re-scored row: identity, era, VPFR verification, logged and re-scored verdict, match and class, the six scores, signed long and short points per breakdown label (from the primary re-score), and mutation flags read from the breakdown notes.
- Rows written: 39594.

