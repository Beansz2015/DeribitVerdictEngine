# SwingFallbackRead output: --mode pocgate (POC-tier gate defect, share of rows affected)

- Run at (UTC): 2026-09-16 09:17:24
- Pooled log: C:\Dev\DeribitVerdictEngine\AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv
- Box logs: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv.v0.7.bak + C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv
- Eval cache: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_eval_cache.csv
- settings.json version 68, sha256 A059DEC578D8B4C7 (copy identical)
- Session ASIA: hours 0-7 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 1.25xATR
- Session LONDON: hours 8-12 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 2xATR
- Session NY: hours 13-23 UTC inclusive, execution_resolution 1, windows 5/10/15 min, fallback target 1.75xATR
- Trading week: Monday 00:00 UTC to Friday 24:00 UTC (exclusive)
- Fees: style maker_maker, round trip 3.00 bps (maker 1.5, taker 3.5). Taker-stop case: 3.00 bps on a target hit, 5.00 bps on a stop hit or a marked exit

- Read: docs/medium-tier-bug-hunt-2026-09-16.md. Stop rule: docs/medium-tier-diagnosis-brief-2026-09-16.md section 0.
- Tolerance for every logged-versus-recomputed comparison: 0.006 USD (the CSV prints F2).
- Step 5c floor: scoring.trade_costs EffectiveMinMovePct = 0.0007999999999999999 of price.

## 1. Row funnel

| Step | Rows |
|---|---|
| Merged rows (pooled book + box live log, first timestamp wins) | 51421 |
| Before the v51 geometry boundary (2026-07-06 13:08:51 UTC) | 314 |
| Non-collector instance rows inside the collector era | 12 |
| Outside the trading week | 11501 |
| No session bucket | 0 |
| No parseable logged VPFRVAH/VPFRVAL | 0 |
| Execution resolution other than 1 or 3 | 0 |
| Fewer than 250 closed bars before the forming bar | 0 |
| **Rows recomputed** | 39594 |
| ... of which swing read population rows (trading-week directional, valid placed levels) | 8810 |
| Swing read population, for reference | 8810 |

## 2. Candle coverage

| Resolution | Store files read | Bars from the store | Bars from this instrument's cache | Bars fetched this run | Bars missing in the needed span |
|---|---|---|---|---|---|
| 1m | candles_1m_2026-07.csv | 42937 | 62184 | 0 | 0 of 97392 |
| 3m | candles_3m_2026-07.csv | 14313 | 20728 | 0 | 0 of 32638 |

## 3. Profile verification (recomputed VAH, VAL, nearest HVN above and below all equal the logged values)

| Window variant | Rows this variant verifies (each variant counted on its own) | Rows where it is the first variant that verifies |
|---|---|---|
| 249 closed + zero-volume forming bar at Price | 38194 | 38194 |
| 250 closed + zero-volume forming bar at Price | 35080 | 43 |
| 250 closed, no forming bar | 34943 | 0 |
| 249 closed + final exchange bar at the forming open | 29549 | 424 |
| 250 closed + final exchange bar at the forming open | 27315 | 4 |

| Session | Kind | Rows recomputed | Verified | Verified % |
|---|---|---|---|---|
| NY | DIRECTIONAL | 5417 | 5183 | 95.7 |
| NY | BELOW_MIN_MOVE | 6676 | 6484 | 97.1 |
| NY | OTHER | 17191 | 16843 | 98.0 |
| LONDON | DIRECTIONAL | 1665 | 1629 | 97.8 |
| LONDON | BELOW_MIN_MOVE | 620 | 614 | 99.0 |
| LONDON | OTHER | 2028 | 2011 | 99.2 |
| ASIA | DIRECTIONAL | 1728 | 1696 | 98.1 |
| ASIA | BELOW_MIN_MOVE | 1158 | 1135 | 98.0 |
| ASIA | OTHER | 3111 | 3070 | 98.7 |
| ALL | population rows | 8810 | 8508 | 96.6 |
| ALL | all kinds | 39594 | 38665 | 97.7 |

| UTC month | Rows recomputed | Verified | Verified % |
|---|---|---|---|
| 2026-07 | 12685 | 12285 | 96.8 |
| 2026-08 | 18625 | 18271 | 98.1 |
| 2026-09 | 8284 | 8109 | 97.9 |

## 4. Shipped path reproduction on verified rows

- Verified rows with logged placed levels: 38665. ComputeSideLevels as shipped reproduces all four logged Placed* values: 38662 (99.99 %).
- Rows where the counterfactual gate moved a STOP (must be 0; the gate reads targets only): 0.
- Directional verified rows whose shipped placed target already fails Step 5c (must be 0): 0.
- BELOW_MIN_MOVE verified rows whose shipped placed target passes Step 5c (must be 0): 1.

## 5. Recomputed VPFR label on verified rows

| Session | Verified rows | NEAR_HVN_SUPPORT | NEAR_HVN_RESIST | IN_LVN_BULL | IN_LVN_BEAR | NEUTRAL |
|---|---|---|---|---|---|---|
| NY | 28510 | 10751 (37.71 %) | 10099 (35.42 %) | 1748 (6.13 %) | 1861 (6.53 %) | 4051 (14.21 %) |
| LONDON | 4254 | 1237 (29.08 %) | 1155 (27.15 %) | 404 (9.50 %) | 493 (11.59 %) | 965 (22.68 %) |
| ASIA | 5901 | 1970 (33.38 %) | 1769 (29.98 %) | 401 (6.80 %) | 435 (7.37 %) | 1326 (22.47 %) |
| ALL | 38665 | 13958 (36.10 %) | 13023 (33.68 %) | 2553 (6.60 %) | 2789 (7.21 %) | 6342 (16.40 %) |

## 6. POC placements under the SHIPPED gate, by recomputed label (verified rows)

| Side | Label | Rows whose placed target is the POC |
|---|---|---|
| LONG | NEAR_HVN_SUPPORT | 0 |
| LONG | NEAR_HVN_RESIST | 0 |
| LONG | IN_LVN_BULL | 0 |
| LONG | IN_LVN_BEAR | 0 |
| LONG | NEUTRAL | 0 |
| SHORT | NEAR_HVN_SUPPORT | 0 |
| SHORT | NEAR_HVN_RESIST | 0 |
| SHORT | IN_LVN_BULL | 0 |
| SHORT | IN_LVN_BEAR | 0 |
| SHORT | NEUTRAL | 0 |
- Logged TargetCapReason = poc in the whole swing read population (all months, verified or not): 0 of 8810.

## 7. The gate its spec describes (NEAR_HVN labels swapped), against the shipped gate

### 7.1 Population rows (trading-week directional, verified)

| Session | Tier | Verified rows | Verdict-side target moves | ... to a POC target | Step 5c would flip to NO TRADE | Flip % of verified rows |
|---|---|---|---|---|---|---|
| NY | STRONG | 310 | 5 | 5 | 5 | 5 (1.61 %) |
| NY | MEDIUM | 1472 | 22 | 22 | 22 | 22 (1.49 %) |
| NY | WEAK | 3401 | 87 | 87 | 87 | 87 (2.56 %) |
| NY | ALL | 5183 | 114 | 114 | 114 | 114 (2.20 %) |
| LONDON | STRONG | 125 | 0 | 0 | 0 | 0 (0.00 %) |
| LONDON | MEDIUM | 469 | 7 | 7 | 7 | 7 (1.49 %) |
| LONDON | WEAK | 1035 | 10 | 10 | 10 | 10 (0.97 %) |
| LONDON | ALL | 1629 | 17 | 17 | 17 | 17 (1.04 %) |
| ASIA | STRONG | 61 | 0 | 0 | 0 | 0 (0.00 %) |
| ASIA | MEDIUM | 410 | 5 | 5 | 5 | 5 (1.22 %) |
| ASIA | WEAK | 1225 | 7 | 7 | 7 | 7 (0.57 %) |
| ASIA | ALL | 1696 | 12 | 12 | 12 | 12 (0.71 %) |
| ALL | STRONG | 496 | 5 | 5 | 5 | 5 (1.01 %) |
| ALL | MEDIUM | 2351 | 34 | 34 | 34 | 34 (1.45 %) |
| ALL | WEAK | 5661 | 104 | 104 | 104 | 104 (1.84 %) |
| ALL | ALL | 8508 | 143 | 143 | 143 | 143 (1.68 %) |

### 7.2 NO TRADE rows vetoed by Step 5c (VerdictContext BELOW_MIN_MOVE, verified)

| Session | Tier before the veto | Verified rows | Dominant-side target moves | Step 5c would pass: row becomes directional |
|---|---|---|---|---|
| NY | STRONG | 232 | 7 | 0 |
| NY | MEDIUM | 1468 | 31 | 0 |
| NY | WEAK | 4784 | 87 | 0 |
| NY | ALL | 6484 | 125 | 0 |
| LONDON | STRONG | 35 | 1 | 0 |
| LONDON | MEDIUM | 153 | 0 | 0 |
| LONDON | WEAK | 426 | 2 | 0 |
| LONDON | ALL | 614 | 3 | 0 |
| ASIA | STRONG | 48 | 0 | 0 |
| ASIA | MEDIUM | 289 | 5 | 0 |
| ASIA | WEAK | 798 | 11 | 0 |
| ASIA | ALL | 1135 | 16 | 0 |
| ALL | STRONG | 315 | 8 | 0 |
| ALL | MEDIUM | 1910 | 36 | 0 |
| ALL | WEAK | 6008 | 100 | 0 |
| ALL | ALL | 8233 | 144 | 0 |

### 7.3 Every verified row (all verdicts): any placed target that moves

- Verified rows: 38665. Rows where the long or the short placed target moves: 1288 (3.33 %). These values reach the CSV Placed* columns and the bridge payload levels.

## 8. VPFR vote against the verdict side (verified population rows, descriptive only)

The verdict already contains the VPFR vote, so these shares are not a test of the vote.

| Session | Label | Rows | Vote on the verdict side | Vote against the verdict side |
|---|---|---|---|---|
| NY | NEAR_HVN_SUPPORT | 1530 | 445 | 1085 |
| NY | NEAR_HVN_RESIST | 1402 | 561 | 841 |
| NY | IN_LVN_BULL | 571 | 532 | 39 |
| NY | IN_LVN_BEAR | 681 | 656 | 25 |
| LONDON | NEAR_HVN_SUPPORT | 346 | 76 | 270 |
| LONDON | NEAR_HVN_RESIST | 307 | 141 | 166 |
| LONDON | IN_LVN_BULL | 256 | 246 | 10 |
| LONDON | IN_LVN_BEAR | 316 | 311 | 5 |
| ASIA | NEAR_HVN_SUPPORT | 453 | 197 | 256 |
| ASIA | NEAR_HVN_RESIST | 402 | 176 | 226 |
| ASIA | IN_LVN_BULL | 210 | 207 | 3 |
| ASIA | IN_LVN_BEAR | 207 | 204 | 3 |
| ALL | NEAR_HVN_SUPPORT | 2329 | 718 | 1611 |
| ALL | NEAR_HVN_RESIST | 2111 | 878 | 1233 |
| ALL | IN_LVN_BULL | 1037 | 985 | 52 |
| ALL | IN_LVN_BEAR | 1204 | 1171 | 33 |

