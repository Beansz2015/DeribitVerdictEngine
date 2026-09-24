# q1d_tier_geometry.py output (Q-1 option (d): target and stop distance by tier)

- Run at (UTC): 2026-09-24 18:00:49
- Inputs: `backtest_data\q1d-tier-geometry\diagnosis-rows-20260924.csv` (MD5 219dac4a5ecbb065554f4e4d61f0421d); `backtest_data\swing-fallback-read\diagnosis-rows.csv` (MD5 e7e93f2fa66e60a3119ce09947b2e063); `backtest_data\swing-fallback-read\rescore-attribution.csv` (MD5 816853b8864456e97a5485fc8b3a2273).
- Logs for the stop label: `AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv` (MD5 e8418846838ff97f3c90f782a95b3523); `aws_fetch\20260924-084613\analysis_log.csv` (MD5 1107825023f998055fca3fdcc09a4fbf)
- settings.json version 68: fee 3.00 bps round trip (maker/maker), stop bound 1.60 x ATR, stop floor 2.0 USD, fallback stop 1.60 x ATR.
- Bootstrap: 10000 resamples of whole UTC trading days, seed 20260924 + call index; readable n >= 100 per group.

## 0. Self-checks

| Check | Result |
|---|---|
| Rows in --rows | 10520 |
| P0 rows (in --prior-rows) / prior export rows (must be equal) | 8810 / 8810 |
| P0 rows whose fields differ from the prior export, Half excluded (must be 0) | 0 |
| H3 rows (from 2026-09-14) | 1710 |
| Rows with no log row for the stop label (must be 0) | 0 |
| Stop label reproduces the logged placed stop (must be all) | 10520 of 10520 |
| Target-hit rows where net EV = T - fee (must be all) | 4437 of 4437 |
| Stop-hit and same-bar rows where net EV = -S - fee (must be all) | 5279 of 5279 |
| Largest absolute residual of the net EV identity over session x tier cells, bps (must be ~0) | 2.66e-15 |
| Tier = tier of the share (STRONG iff share >= 0.70, MEDIUM iff 0.53 <= share < 0.70), mismatches (must be 0) | 0 |
| POC-fix slice rows in P0 (a superset of the 143 moved population rows) | 176 |

| Segment | Session | Rows | STRONG | MEDIUM | WEAK | Trading days | H1 rows | H2 rows |
|---|---|---|---|---|---|---|---|---|
| P0 | NY | 5417 | 342 | 1552 | 3523 | 49 | 2160 | 3257 |
| P0 | LONDON | 1665 | 131 | 485 | 1049 | 47 | 754 | 911 |
| P0 | ASIA | 1728 | 65 | 420 | 1243 | 42 | 661 | 1067 |
| H3 | NY | 1009 | 65 | 291 | 653 | 7 | 0 | 0 |
| H3 | LONDON | 234 | 12 | 64 | 158 | 7 | 0 | 0 |
| H3 | ASIA | 467 | 21 | 142 | 304 | 8 | 0 | 0 |

## 1. Geometry by session and tier, P0 (placed levels, bps of entry)

| Session | Tier | n | Target bps p10/p25/p50/p75/p90 | Mean T | Stop bps p10/p25/p50/p75/p90 | Mean S | Sum T / sum S | Median T/S | Median T, ATR | Median S, ATR | Median ATR, bps |
|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 9.2/10.6/14.2/19.7/27.2 | 16.6 | 8.1/9.6/12.9/18.7/24.5 | 15.3 | 1.08 | 1.09 | 1.75 | 1.60 | 8.0 |
| NY | MEDIUM | 1552 | 8.8/10.3/13.4/18.1/24.9 | 15.4 | 7.7/8.9/11.7/15.9/21.6 | 13.5 | 1.14 | 1.09 | 1.75 | 1.60 | 7.3 |
| NY | WEAK | 3523 | 8.7/10.0/12.8/18.2/24.8 | 15.4 | 7.3/8.5/11.1/15.5/20.4 | 12.9 | 1.19 | 1.09 | 1.75 | 1.60 | 7.0 |
| NY | ALL | 5417 | 8.8/10.1/13.1/18.2/25.0 | 15.5 | 7.4/8.7/11.4/15.8/21.1 | 13.2 | 1.17 | 1.09 | 1.75 | 1.60 | 7.1 |
| LONDON | STRONG | 131 | 9.6/11.3/14.8/20.9/29.9 | 18.6 | 7.8/9.2/13.3/17.5/23.9 | 15.4 | 1.21 | 1.25 | 2.00 | 1.60 | 8.3 |
| LONDON | MEDIUM | 485 | 9.4/11.3/15.2/21.0/30.6 | 18.7 | 7.6/9.3/13.0/17.4/25.7 | 15.7 | 1.19 | 1.25 | 2.00 | 1.60 | 8.2 |
| LONDON | WEAK | 1049 | 9.2/11.1/15.2/21.5/33.7 | 19.4 | 7.0/8.9/12.4/16.7/27.0 | 15.3 | 1.27 | 1.25 | 2.00 | 1.60 | 8.0 |
| LONDON | ALL | 1665 | 9.3/11.2/15.2/21.1/33.0 | 19.1 | 7.2/9.1/12.7/17.0/26.4 | 15.4 | 1.24 | 1.25 | 2.00 | 1.60 | 8.1 |
| ASIA | STRONG | 65 | 9.0/9.8/13.2/18.0/24.9 | 15.3 | 10.5/12.3/16.5/20.4/31.9 | 19.0 | 0.81 | 0.78 | 1.25 | 1.60 | 10.3 |
| ASIA | MEDIUM | 420 | 8.6/9.8/12.3/16.1/23.4 | 14.9 | 10.0/11.5/13.8/18.5/26.8 | 16.8 | 0.89 | 0.78 | 1.25 | 1.60 | 8.6 |
| ASIA | WEAK | 1243 | 8.8/10.4/13.5/18.7/28.0 | 16.7 | 8.1/10.8/13.4/17.3/22.8 | 15.1 | 1.10 | 0.94 | 1.49 | 1.60 | 8.5 |
| ASIA | ALL | 1728 | 8.8/10.3/13.1/18.1/27.3 | 16.2 | 8.5/11.0/13.6/17.6/23.9 | 15.6 | 1.03 | 0.78 | 1.25 | 1.60 | 8.5 |

## 2. Target type and stop label by session and tier, P0

| Session | Tier | n | Target: ATR fallback (none) % | swing % | HVN % | POC % | Stop: SWING_STOP % | STOP_CLAMPED % | FALLBACK_ATR % | Mean T by target type: none / swing / HVN |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 75.7 | 17.3 | 7.0 | 0.0 | 0.0 | 99.4 | 0.6 | 16.6 (259) / 16.7 (59) / 16.1 (24) |
| NY | MEDIUM | 1552 | 60.5 | 31.4 | 8.1 | 0.0 | 0.3 | 99.2 | 0.5 | 14.9 (939) / 16.2 (488) / 16.1 (125) |
| NY | WEAK | 3523 | 46.9 | 34.4 | 18.7 | 0.0 | 1.1 | 98.0 | 0.9 | 14.5 (1654) / 16.9 (1211) / 14.7 (658) |
| LONDON | STRONG | 131 | 80.9 | 16.8 | 2.3 | 0.0 | 0.0 | 97.7 | 2.3 | 18.5 (106) / 19.3 (22) / 18.2 (3) |
| LONDON | MEDIUM | 485 | 66.2 | 28.0 | 5.8 | 0.0 | 3.7 | 94.8 | 1.4 | 19.9 (321) / 16.6 (136) / 15.2 (28) |
| LONDON | WEAK | 1049 | 44.2 | 46.1 | 9.6 | 0.0 | 7.8 | 90.7 | 1.5 | 20.4 (464) / 18.9 (484) / 17.6 (101) |
| ASIA | STRONG | 65 | 78.5 | 18.5 | 3.1 | 0.0 | 0.0 | 100.0 | 0.0 | 15.2 (51) / 15.0 (12) / 22.0 (2) |
| ASIA | MEDIUM | 420 | 59.8 | 33.6 | 6.7 | 0.0 | 2.4 | 97.6 | 0.0 | 13.0 (251) / 18.0 (141) / 17.2 (28) |
| ASIA | WEAK | 1243 | 34.8 | 55.8 | 9.5 | 0.0 | 6.7 | 92.5 | 0.8 | 12.6 (432) / 19.1 (693) / 17.5 (118) |

### 2.1 Why the swing tier did not place, by tier, P0 (from the logged SwingTarget, Price and ATR)

| Session | Tier | n | No swing target % | Swing target behind entry % | Swing target beyond the 3.5 x ATR bound % | Swing target qualifies % | Target ATR fallback % | Median swing target distance when it qualifies, ATR |
|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 71.9 | 0.0 | 10.8 | 17.3 | 75.7 | 2.20 |
| NY | MEDIUM | 1552 | 55.9 | 0.0 | 12.4 | 31.8 | 60.5 | 2.21 |
| NY | WEAK | 3523 | 40.8 | 0.0 | 24.2 | 35.0 | 46.9 | 2.50 |
| LONDON | STRONG | 131 | 80.2 | 0.0 | 3.1 | 16.8 | 80.9 | 1.78 |
| LONDON | MEDIUM | 485 | 66.2 | 0.0 | 4.9 | 28.9 | 66.2 | 1.78 |
| LONDON | WEAK | 1049 | 40.0 | 0.0 | 13.3 | 46.6 | 44.2 | 2.19 |
| ASIA | STRONG | 65 | 81.5 | 0.0 | 0.0 | 18.5 | 78.5 | 1.29 |
| ASIA | MEDIUM | 420 | 63.3 | 0.0 | 2.4 | 34.3 | 59.8 | 1.76 |
| ASIA | WEAK | 1243 | 37.0 | 0.0 | 6.8 | 56.2 | 34.8 | 2.16 |

| Session | Comparison | Metric | FULL d [95 % CI] (n) | H1 d [95 % CI] | H2 d [95 % CI] | Label |
|---|---|---|---|---|---|---|
| NY | STRONG - WEAK | share with no swing target, pp | +31.2 [+24.7, +37.6] (342/3523) | +35.5 [+25.4, +43.9] | +28.3 [+19.8, +37.0] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | share with an ATR fallback target, pp | +28.8 [+23.2, +34.6] (342/3523) | +26.6 [+17.7, +36.8] | +30.3 [+23.1, +37.3] | CONFIRMED (d > 0) |
| NY | MEDIUM - WEAK | share with no swing target, pp | +15.1 [+11.6, +18.7] (1552/3523) | +13.3 [+7.9, +18.8] | +16.3 [+11.7, +21.0] | CONFIRMED (d > 0) |
| NY | MEDIUM - WEAK | share with an ATR fallback target, pp | +13.6 [+10.2, +16.9] (1552/3523) | +10.9 [+6.0, +16.6] | +15.3 [+11.3, +19.2] | CONFIRMED (d > 0) |
| LONDON | STRONG - WEAK | share with no swing target, pp | +40.1 [+28.2, +50.4] (131/1049) | +43.3 [+28.3, +54.7] | +36.2 [+18.1, +53.6] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | share with an ATR fallback target, pp | +36.7 [+25.8, +46.1] (131/1049) | +38.7 [+27.4, +48.1] | +32.7 [+13.6, +50.3] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | share with no swing target, pp | +26.1 [+20.4, +32.2] (485/1049) | +29.9 [+21.5, +39.1] | +23.2 [+15.5, +31.4] | CONFIRMED (d > 0) |
| LONDON | MEDIUM - WEAK | share with an ATR fallback target, pp | +22.0 [+15.9, +28.3] (485/1049) | +26.0 [+16.6, +36.0] | +18.9 [+11.4, +26.7] | CONFIRMED (d > 0) |
| ASIA | STRONG - WEAK | share with no swing target, pp | +44.5 [+33.9, +54.7] (65/1243) | +51.6 [+37.6, +66.3] | +41.9 [+27.6, +54.7] | NOT READABLE |
| ASIA | STRONG - WEAK | share with an ATR fallback target, pp | +43.7 [+33.0, +54.1] (65/1243) | +53.7 [+40.5, +68.1] | +39.9 [+24.8, +52.9] | NOT READABLE |
| ASIA | MEDIUM - WEAK | share with no swing target, pp | +26.3 [+21.3, +31.5] (420/1243) | +31.9 [+23.3, +39.1] | +22.0 [+15.9, +28.5] | CONFIRMED (d > 0) |
| ASIA | MEDIUM - WEAK | share with an ATR fallback target, pp | +25.0 [+18.4, +31.4] (420/1243) | +29.1 [+17.9, +39.0] | +21.8 [+13.5, +30.1] | CONFIRMED (d > 0) |

## 3. Outcome ledger by session and tier, P0, main window (NY 15 min, LONDON and ASIA 45 min)

| Session | Cell | n | Success % | Stop hit % | Timeout % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Dist-weighted success % | Dist-weighted gross edge pp | Mean T+S bps | W x weighted edge | Timeout term | Fee | Net EV bps [95 % CI] |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 45.9 | 51.8 | 2.3 | 48.1 | 57.5 | -2.2 | -11.6 | 45.0 | -3.1 | 31.9 | -0.99 | +0.74 | -3.00 | -3.2 [-5.2, -1.4] |
| NY | MEDIUM | 1552 | 39.9 | 52.8 | 7.3 | 46.7 | 57.1 | -6.8 | -17.2 | 38.2 | -8.5 | 28.9 | -2.46 | +1.27 | -3.00 | -4.2 [-5.3, -3.1] |
| NY | WEAK | 3523 | 40.1 | 51.7 | 8.1 | 45.7 | 56.3 | -5.6 | -16.2 | 39.9 | -5.8 | 28.3 | -1.63 | +1.43 | -3.00 | -3.2 [-4.0, -2.4] |
| LONDON | STRONG | 131 | 41.2 | 52.7 | 6.1 | 45.3 | 54.1 | -4.1 | -12.9 | 43.0 | -2.4 | 34.0 | -0.80 | +2.22 | -3.00 | -1.6 [-5.8, +3.2] |
| LONDON | MEDIUM | 485 | 46.2 | 46.2 | 7.6 | 45.6 | 54.3 | +0.6 | -8.2 | 46.8 | +1.2 | 34.3 | +0.40 | +1.80 | -3.00 | -0.8 [-3.0, +1.3] |
| LONDON | WEAK | 1049 | 42.3 | 47.5 | 10.2 | 44.1 | 52.8 | -1.8 | -10.4 | 40.7 | -3.4 | 34.7 | -1.19 | +2.97 | -3.00 | -1.2 [-2.8, +0.3] |
| ASIA | STRONG | 65 (NOT READABLE) | 64.6 | 27.7 | 7.7 | 55.3 | 64.0 | +9.3 | +0.6 | 65.3 | +10.0 | 34.3 | +3.44 | +2.10 | -3.00 | +2.5 [-2.0, +6.7] |
| ASIA | MEDIUM | 420 | 48.3 | 46.2 | 5.5 | 52.9 | 62.3 | -4.5 | -14.0 | 47.8 | -5.1 | 31.7 | -1.60 | +0.95 | -3.00 | -3.6 [-5.4, -2.0] |
| ASIA | WEAK | 1243 | 46.7 | 45.1 | 8.2 | 47.5 | 57.0 | -0.8 | -10.2 | 44.8 | -2.7 | 31.8 | -0.86 | +1.66 | -3.00 | -2.2 [-3.8, -0.7] |

- Identity per row: net EV = W x weighted edge + timeout term - fee (section 0 residual). W = mean(T + S).

## 4. Outcome ledger by score-share bin, P0, main window (the prior read's bins; >= 0.70 is STRONG, 0.53-0.70 is MEDIUM)

| Session | Cell | n | Success % | Stop hit % | Timeout % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Dist-weighted success % | Dist-weighted gross edge pp | Mean T+S bps | W x weighted edge | Timeout term | Fee | Net EV bps [95 % CI] |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | share < 0.45 | 1879 | 39.4 | 51.8 | 8.7 | 45.3 | 56.1 | -5.9 | -16.7 | 39.1 | -6.2 | 27.8 | -1.71 | +1.45 | -3.00 | -3.3 [-4.2, -2.2] |
| NY | share 0.45-0.53 | 1644 | 40.9 | 51.6 | 7.5 | 46.1 | 56.5 | -5.3 | -15.6 | 40.8 | -5.3 | 29.0 | -1.54 | +1.41 | -3.00 | -3.1 [-4.2, -2.1] |
| NY | share 0.53-0.60 | 650 | 41.5 | 50.3 | 8.2 | 46.6 | 56.8 | -5.0 | -15.3 | 40.6 | -6.0 | 29.2 | -1.74 | +1.35 | -3.00 | -3.4 [-4.8, -2.0] |
| NY | share 0.60-0.70 | 902 | 38.7 | 54.5 | 6.8 | 46.8 | 57.3 | -8.1 | -18.6 | 36.4 | -10.4 | 28.6 | -2.98 | +1.21 | -3.00 | -4.8 [-6.1, -3.5] |
| NY | share >= 0.70 | 342 | 45.9 | 51.8 | 2.3 | 48.1 | 57.5 | -2.2 | -11.6 | 45.0 | -3.1 | 31.9 | -0.99 | +0.74 | -3.00 | -3.2 [-5.3, -1.4] |
| LONDON | share < 0.45 | 566 | 41.0 | 49.3 | 9.7 | 43.5 | 52.5 | -2.5 | -11.5 | 39.2 | -4.3 | 33.6 | -1.44 | +2.75 | -3.00 | -1.7 [-3.7, +0.2] |
| LONDON | share 0.45-0.53 | 483 | 43.9 | 45.3 | 10.8 | 44.8 | 53.1 | -0.9 | -9.2 | 42.3 | -2.5 | 36.0 | -0.89 | +3.23 | -3.00 | -0.7 [-2.9, +1.4] |
| LONDON | share 0.53-0.60 | 223 | 44.4 | 46.2 | 9.4 | 45.4 | 54.7 | -1.0 | -10.4 | 43.9 | -1.5 | 32.2 | -0.49 | +2.07 | -3.00 | -1.4 [-4.3, +1.2] |
| LONDON | share 0.60-0.70 | 262 | 47.7 | 46.2 | 6.1 | 45.7 | 54.0 | +2.0 | -6.3 | 48.9 | +3.2 | 36.2 | +1.15 | +1.58 | -3.00 | -0.3 [-2.8, +2.4] |
| LONDON | share >= 0.70 | 131 | 41.2 | 52.7 | 6.1 | 45.3 | 54.1 | -4.1 | -12.9 | 43.0 | -2.4 | 34.0 | -0.80 | +2.22 | -3.00 | -1.6 [-5.9, +3.2] |
| ASIA | share < 0.45 | 706 | 44.3 | 48.3 | 7.4 | 45.9 | 55.6 | -1.6 | -11.3 | 42.5 | -3.4 | 31.0 | -1.06 | +1.56 | -3.00 | -2.5 [-4.2, -0.8] |
| ASIA | share 0.45-0.53 | 537 | 49.9 | 40.8 | 9.3 | 49.6 | 58.7 | +0.3 | -8.8 | 47.8 | -1.8 | 32.8 | -0.59 | +1.81 | -3.00 | -1.8 [-3.8, -0.0] |
| ASIA | share 0.53-0.60 | 218 | 48.2 | 46.3 | 5.5 | 52.4 | 62.1 | -4.2 | -14.0 | 48.7 | -3.7 | 30.8 | -1.15 | +0.85 | -3.00 | -3.3 [-6.0, -0.6] |
| ASIA | share 0.60-0.70 | 202 | 48.5 | 46.0 | 5.4 | 53.3 | 62.5 | -4.8 | -14.0 | 47.0 | -6.4 | 32.7 | -2.09 | +1.06 | -3.00 | -4.0 [-5.9, -2.2] |
| ASIA | share >= 0.70 | 65 (NOT READABLE) | 64.6 | 27.7 | 7.7 | 55.3 | 64.0 | +9.3 | +0.6 | 65.3 | +10.0 | 34.3 | +3.44 | +2.10 | -3.00 | +2.5 [-2.0, +6.7] |

## 5. Tier comparisons, P0, main window: d = first tier - second tier, day bootstrap, halves H1 / H2

- Two-sided: the label names the sign it found. A nearer-target hypothesis reads d < 0 on target distance; the opposite reads d > 0.

| Session | Comparison | Metric | FULL d [95 % CI] (n) | H1 d [95 % CI] | H2 d [95 % CI] | Label |
|---|---|---|---|---|---|---|
| NY | STRONG - WEAK | mean target distance, bps | +1.2 [-0.5, +2.8] (342/3523) | -0.0 [-0.9, +1.1] | +2.0 [-0.6, +4.1] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | mean stop distance, bps | +2.4 [+0.6, +4.1] (342/3523) | +1.1 [+0.5, +1.8] | +3.2 [+0.4, +5.5] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | payoff ratio sum T / sum S | -0.11 [-0.15, -0.06] (342/3523) | -0.11 [-0.18, -0.04] | -0.10 [-0.15, -0.04] | CONFIRMED (d < 0) |
| NY | STRONG - WEAK | mean target distance, ATR | -0.2 [-0.3, -0.1] (342/3523) | -0.2 [-0.3, -0.1] | -0.2 [-0.3, -0.1] | CONFIRMED (d < 0) |
| NY | STRONG - WEAK | mean ATR, bps of entry | +1.5 [+0.4, +2.5] (342/3523) | +0.7 [+0.3, +1.1] | +2.0 [+0.2, +3.4] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | gross breakeven rate, pp | +2.4 [+1.3, +3.3] (342/3523) | +2.4 [+0.9, +3.8] | +2.3 [+0.8, +3.4] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | success rate, pp | +5.8 [+0.2, +10.8] (342/3523) | +3.5 [-4.8, +10.8] | +7.4 [-0.8, +14.0] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | gross edge, pp | +3.4 [-2.2, +8.7] (342/3523) | +1.1 [-7.3, +8.2] | +5.1 [-3.0, +12.3] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | distance-weighted success rate, pp | +5.0 [-0.5, +10.3] (342/3523) | +4.2 [-3.4, +11.0] | +5.5 [-2.5, +12.9] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | distance-weighted gross edge, pp | +2.7 [-3.0, +8.4] (342/3523) | +1.8 [-5.9, +8.9] | +3.2 [-4.7, +11.1] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | timeout share, pp | -5.8 [-7.5, -3.9] (342/3523) | -5.4 [-8.2, -2.1] | -6.1 [-8.2, -3.7] | CONFIRMED (d < 0) |
| NY | STRONG - WEAK | timeout term, bps | -0.7 [-1.1, -0.3] (342/3523) | -0.9 [-1.2, -0.5] | -0.6 [-1.2, +0.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| NY | STRONG - WEAK | net EV per trade, bps | -0.0 [-1.8, +1.6] (342/3523) | -0.4 [-2.4, +1.5] | +0.2 [-2.6, +2.7] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | mean target distance, bps | +0.0 [-0.5, +0.6] (1552/3523) | +0.0 [-0.6, +0.6] | +0.1 [-0.7, +0.9] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | mean stop distance, bps | +0.5 [+0.0, +1.1] (1552/3523) | +0.6 [+0.2, +1.2] | +0.5 [-0.3, +1.4] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.05 [-0.07, -0.02] (1552/3523) | -0.06 [-0.10, -0.02] | -0.04 [-0.07, -0.00] | CONFIRMED (d < 0) |
| NY | MEDIUM - WEAK | mean target distance, ATR | -0.1 [-0.1, -0.1] (1552/3523) | -0.1 [-0.2, -0.1] | -0.1 [-0.1, -0.0] | CONFIRMED (d < 0) |
| NY | MEDIUM - WEAK | mean ATR, bps of entry | +0.3 [-0.0, +0.7] (1552/3523) | +0.4 [+0.1, +0.7] | +0.3 [-0.2, +0.9] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | MEDIUM - WEAK | gross breakeven rate, pp | +1.0 [+0.4, +1.5] (1552/3523) | +1.3 [+0.5, +2.0] | +0.9 [+0.1, +1.5] | CONFIRMED (d > 0) |
| NY | MEDIUM - WEAK | success rate, pp | -0.2 [-4.4, +3.9] (1552/3523) | -2.2 [-8.3, +4.5] | +1.2 [-4.5, +6.5] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | gross edge, pp | -1.2 [-5.4, +2.9] (1552/3523) | -3.5 [-9.2, +2.9] | +0.3 [-5.4, +5.5] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | distance-weighted success rate, pp | -1.8 [-6.2, +2.9] (1552/3523) | -3.0 [-9.6, +4.9] | -1.0 [-7.1, +4.7] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | distance-weighted gross edge, pp | -2.7 [-7.3, +1.8] (1552/3523) | -4.3 [-10.8, +3.4] | -1.9 [-7.9, +3.9] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | timeout share, pp | -0.8 [-2.0, +0.4] (1552/3523) | -0.2 [-2.0, +1.5] | -1.2 [-2.7, +0.4] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | timeout term, bps | -0.2 [-0.5, +0.1] (1552/3523) | -0.1 [-0.4, +0.2] | -0.2 [-0.6, +0.2] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | net EV per trade, bps | -1.0 [-2.2, +0.2] (1552/3523) | -1.2 [-2.7, +0.5] | -0.8 [-2.5, +0.8] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | mean target distance, bps | +1.2 [-0.3, +2.7] (342/1552) | -0.0 [-0.8, +0.9] | +1.9 [-0.4, +3.9] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | mean stop distance, bps | +1.8 [+0.3, +3.4] (342/1552) | +0.5 [-0.2, +1.2] | +2.7 [+0.4, +4.7] | DISCOVERY ONLY (H2) (d > 0) |
| NY | STRONG - MEDIUM | payoff ratio sum T / sum S | -0.06 [-0.10, -0.02] (342/1552) | -0.05 [-0.12, +0.02] | -0.06 [-0.10, -0.02] | DISCOVERY ONLY (H2) (d < 0) |
| NY | STRONG - MEDIUM | mean target distance, ATR | -0.1 [-0.2, -0.1] (342/1552) | -0.1 [-0.2, -0.0] | -0.1 [-0.2, -0.0] | CONFIRMED (d < 0) |
| NY | STRONG - MEDIUM | mean ATR, bps of entry | +1.1 [+0.2, +2.1] (342/1552) | +0.3 [-0.1, +0.8] | +1.7 [+0.3, +2.9] | DISCOVERY ONLY (H2) (d > 0) |
| NY | STRONG - MEDIUM | gross breakeven rate, pp | +1.4 [+0.5, +2.1] (342/1552) | +1.1 [-0.4, +2.5] | +1.4 [+0.4, +2.4] | DISCOVERY ONLY (H2) (d > 0) |
| NY | STRONG - MEDIUM | success rate, pp | +6.0 [+0.1, +11.5] (342/1552) | +5.7 [-4.3, +14.9] | +6.2 [-1.0, +13.5] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | gross edge, pp | +4.7 [-1.1, +10.4] (342/1552) | +4.6 [-4.9, +13.5] | +4.8 [-2.5, +12.2] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | distance-weighted success rate, pp | +6.8 [+1.0, +12.6] (342/1552) | +7.2 [-2.5, +16.3] | +6.6 [-0.8, +14.3] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | distance-weighted gross edge, pp | +5.4 [-0.2, +11.4] (342/1552) | +6.1 [-2.7, +14.6] | +5.1 [-2.3, +13.3] | NO DIFFERENCE SHOWN |
| NY | STRONG - MEDIUM | timeout share, pp | -5.0 [-6.6, -3.3] (342/1552) | -5.2 [-7.6, -2.0] | -4.9 [-7.1, -2.9] | CONFIRMED (d < 0) |
| NY | STRONG - MEDIUM | timeout term, bps | -0.5 [-1.0, +0.0] (342/1552) | -0.8 [-1.1, -0.4] | -0.4 [-1.1, +0.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| NY | STRONG - MEDIUM | net EV per trade, bps | +1.0 [-0.8, +2.7] (342/1552) | +0.8 [-1.6, +3.1] | +1.1 [-1.6, +3.5] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | mean target distance, bps | -0.8 [-3.5, +1.8] (131/1049) | +0.5 [-1.1, +2.3] | +0.5 [-3.6, +5.1] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | mean stop distance, bps | +0.1 [-2.1, +2.2] (131/1049) | +1.2 [-0.1, +2.6] | +1.3 [-2.0, +4.9] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | payoff ratio sum T / sum S | -0.06 [-0.11, -0.01] (131/1049) | -0.09 [-0.15, -0.04] | -0.05 [-0.12, +0.03] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | mean target distance, ATR | -0.1 [-0.1, -0.0] (131/1049) | -0.1 [-0.2, -0.1] | -0.0 [-0.2, +0.1] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | mean ATR, bps of entry | -0.2 [-1.6, +1.1] (131/1049) | +0.6 [-0.3, +1.5] | +0.4 [-1.7, +2.7] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | gross breakeven rate, pp | +1.2 [+0.2, +2.2] (131/1049) | +1.8 [+0.9, +2.9] | +1.1 [-0.6, +2.7] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | success rate, pp | -1.1 [-14.2, +12.0] (131/1049) | -0.6 [-17.6, +16.1] | -1.5 [-20.8, +19.8] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | gross edge, pp | -2.3 [-15.1, +11.0] (131/1049) | -2.4 [-20.0, +14.4] | -2.6 [-21.3, +18.7] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | distance-weighted success rate, pp | +2.2 [-15.2, +20.5] (131/1049) | -4.5 [-21.6, +14.8] | +7.0 [-20.2, +34.3] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | distance-weighted gross edge, pp | +1.1 [-16.2, +18.9] (131/1049) | -6.3 [-23.6, +13.0] | +6.0 [-20.1, +32.4] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | timeout share, pp | -4.1 [-9.7, +1.9] (131/1049) | -1.3 [-6.4, +5.0] | -5.9 [-15.9, +4.3] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | timeout term, bps | -0.8 [-3.3, +1.9] (131/1049) | +0.1 [-0.7, +1.5] | -0.9 [-5.9, +4.6] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | net EV per trade, bps | -0.4 [-4.7, +4.7] (131/1049) | -1.5 [-6.1, +3.3] | +1.8 [-6.6, +13.0] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | mean target distance, bps | -0.7 [-3.3, +1.6] (485/1049) | +1.0 [+0.1, +2.1] | -2.2 [-6.3, +1.6] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| LONDON | MEDIUM - WEAK | mean stop distance, bps | +0.3 [-1.7, +2.1] (485/1049) | +1.4 [+0.6, +2.3] | -0.6 [-3.6, +2.3] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| LONDON | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.07 [-0.13, -0.03] (485/1049) | -0.07 [-0.11, -0.03] | -0.08 [-0.16, -0.02] | CONFIRMED (d < 0) |
| LONDON | MEDIUM - WEAK | mean target distance, ATR | -0.1 [-0.2, -0.0] (485/1049) | -0.1 [-0.2, -0.0] | -0.1 [-0.2, -0.0] | CONFIRMED (d < 0) |
| LONDON | MEDIUM - WEAK | mean ATR, bps of entry | +0.0 [-1.2, +1.1] (485/1049) | +0.8 [+0.2, +1.4] | -0.7 [-2.6, +1.1] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| LONDON | MEDIUM - WEAK | gross breakeven rate, pp | +1.5 [+0.6, +2.6] (485/1049) | +1.3 [+0.5, +2.1] | +1.6 [+0.3, +3.3] | CONFIRMED (d > 0) |
| LONDON | MEDIUM - WEAK | success rate, pp | +3.9 [-2.5, +9.9] (485/1049) | +8.5 [+0.6, +16.2] | +0.2 [-8.9, +8.7] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| LONDON | MEDIUM - WEAK | gross edge, pp | +2.4 [-3.8, +8.2] (485/1049) | +7.2 [-0.6, +14.7] | -1.4 [-10.6, +7.1] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | distance-weighted success rate, pp | +6.1 [-1.6, +13.7] (485/1049) | +9.6 [+0.8, +18.5] | +4.2 [-7.1, +14.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| LONDON | MEDIUM - WEAK | distance-weighted gross edge, pp | +4.6 [-3.4, +12.2] (485/1049) | +8.2 [-0.5, +16.9] | +2.6 [-8.2, +12.7] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | timeout share, pp | -2.6 [-6.2, +0.9] (485/1049) | +0.4 [-3.0, +3.8] | -5.0 [-10.5, +0.4] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | timeout term, bps | -1.2 [-3.0, +0.3] (485/1049) | +0.1 [-0.7, +0.8] | -2.2 [-5.1, +0.4] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | net EV per trade, bps | +0.4 [-2.0, +2.7] (485/1049) | +2.3 [-0.2, +4.9] | -1.0 [-4.8, +2.5] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | mean target distance, bps | -0.1 [-2.3, +2.4] (131/485) | -0.5 [-2.0, +1.2] | +2.7 [-1.2, +7.3] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | mean stop distance, bps | -0.3 [-1.9, +1.6] (131/485) | -0.2 [-1.3, +1.0] | +1.8 [-1.2, +5.4] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | payoff ratio sum T / sum S | +0.01 [-0.05, +0.07] (131/485) | -0.02 [-0.07, +0.02] | +0.03 [-0.07, +0.13] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | mean target distance, ATR | +0.0 [-0.0, +0.1] (131/485) | -0.0 [-0.1, +0.0] | +0.1 [-0.1, +0.2] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | mean ATR, bps of entry | -0.2 [-1.3, +0.9] (131/485) | -0.2 [-0.9, +0.6] | +1.1 [-0.8, +3.3] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | gross breakeven rate, pp | -0.3 [-1.5, +1.0] (131/485) | +0.5 [-0.4, +1.5] | -0.6 [-2.6, +1.6] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | success rate, pp | -5.0 [-18.3, +8.1] (131/485) | -9.1 [-26.0, +7.8] | -1.8 [-21.3, +19.1] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | gross edge, pp | -4.7 [-17.2, +8.2] (131/485) | -9.6 [-26.4, +7.1] | -1.2 [-19.8, +19.4] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | distance-weighted success rate, pp | -3.8 [-20.0, +12.0] (131/485) | -14.1 [-33.1, +6.4] | +2.9 [-21.2, +25.5] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | distance-weighted gross edge, pp | -3.5 [-18.8, +11.9] (131/485) | -14.5 [-33.2, +5.2] | +3.4 [-19.2, +25.7] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | timeout share, pp | -1.5 [-6.4, +3.9] (131/485) | -1.7 [-7.6, +5.4] | -0.9 [-9.2, +7.7] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | timeout term, bps | +0.4 [-1.3, +2.5] (131/485) | +0.1 [-1.0, +1.5] | +1.3 [-2.2, +5.8] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - MEDIUM | net EV per trade, bps | -0.8 [-5.3, +4.2] (131/485) | -3.8 [-8.4, +1.0] | +2.8 [-5.8, +13.1] | NO DIFFERENCE SHOWN |
| ASIA | STRONG - WEAK | mean target distance, bps | -1.3 [-3.5, +0.7] (65/1243) | -3.4 [-4.5, -2.1] | -1.1 [-3.5, +0.8] | NOT READABLE |
| ASIA | STRONG - WEAK | mean stop distance, bps | +3.9 [+0.7, +7.2] (65/1243) | +1.3 [-0.5, +3.5] | +4.3 [+0.5, +7.6] | NOT READABLE |
| ASIA | STRONG - WEAK | payoff ratio sum T / sum S | -0.30 [-0.35, -0.24] (65/1243) | -0.36 [-0.45, -0.28] | -0.27 [-0.33, -0.20] | NOT READABLE |
| ASIA | STRONG - WEAK | mean target distance, ATR | -0.5 [-0.6, -0.3] (65/1243) | -0.6 [-0.7, -0.5] | -0.4 [-0.5, -0.2] | NOT READABLE |
| ASIA | STRONG - WEAK | mean ATR, bps of entry | +2.2 [+0.2, +4.3] (65/1243) | +0.7 [-0.5, +2.0] | +2.4 [+0.1, +4.6] | NOT READABLE |
| ASIA | STRONG - WEAK | gross breakeven rate, pp | +7.8 [+6.2, +9.1] (65/1243) | +9.6 [+7.5, +12.0] | +7.2 [+5.1, +8.6] | NOT READABLE |
| ASIA | STRONG - WEAK | success rate, pp | +17.9 [+4.3, +30.5] (65/1243) | +14.5 [-12.9, +38.9] | +19.2 [+3.5, +33.6] | NOT READABLE |
| ASIA | STRONG - WEAK | gross edge, pp | +10.1 [-3.4, +22.8] (65/1243) | +4.9 [-22.8, +28.2] | +11.9 [-3.8, +27.2] | NOT READABLE |
| ASIA | STRONG - WEAK | distance-weighted success rate, pp | +20.5 [+5.7, +31.1] (65/1243) | +17.2 [-14.0, +43.8] | +21.3 [+4.3, +32.7] | NOT READABLE |
| ASIA | STRONG - WEAK | distance-weighted gross edge, pp | +12.7 [-2.3, +23.4] (65/1243) | +7.6 [-22.3, +32.9] | +14.1 [-2.5, +25.6] | NOT READABLE |
| ASIA | STRONG - WEAK | timeout share, pp | -0.5 [-7.1, +6.3] (65/1243) | -4.0 [-12.2, +8.9] | +1.1 [-7.1, +9.1] | NOT READABLE |
| ASIA | STRONG - WEAK | timeout term, bps | +0.4 [-1.2, +2.0] (65/1243) | -0.8 [-2.1, +1.0] | +0.9 [-1.3, +2.9] | NOT READABLE |
| ASIA | STRONG - WEAK | net EV per trade, bps | +4.7 [+0.0, +9.1] (65/1243) | +1.1 [-4.8, +6.5] | +6.2 [+0.1, +11.3] | NOT READABLE |
| ASIA | MEDIUM - WEAK | mean target distance, bps | -1.7 [-2.6, -0.8] (420/1243) | -1.2 [-1.9, -0.5] | -1.6 [-2.9, -0.2] | CONFIRMED (d < 0) |
| ASIA | MEDIUM - WEAK | mean stop distance, bps | +1.7 [+0.2, +3.5] (420/1243) | +1.1 [+0.1, +2.1] | +2.6 [+0.3, +5.5] | CONFIRMED (d > 0) |
| ASIA | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.21 [-0.26, -0.16] (420/1243) | -0.18 [-0.26, -0.12] | -0.23 [-0.28, -0.17] | CONFIRMED (d < 0) |
| ASIA | MEDIUM - WEAK | mean target distance, ATR | -0.3 [-0.4, -0.2] (420/1243) | -0.3 [-0.4, -0.2] | -0.3 [-0.4, -0.3] | CONFIRMED (d < 0) |
| ASIA | MEDIUM - WEAK | mean ATR, bps of entry | +0.9 [-0.1, +2.0] (420/1243) | +0.5 [-0.1, +1.2] | +1.5 [+0.1, +3.2] | DISCOVERY ONLY (H2) (d > 0) |
| ASIA | MEDIUM - WEAK | gross breakeven rate, pp | +5.3 [+4.1, +6.5] (420/1243) | +4.4 [+2.9, +6.1] | +5.9 [+4.3, +7.1] | CONFIRMED (d > 0) |
| ASIA | MEDIUM - WEAK | success rate, pp | +1.6 [-5.8, +8.6] (420/1243) | -3.8 [-14.5, +5.3] | +5.7 [-5.0, +15.7] | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | gross edge, pp | -3.7 [-10.8, +3.2] (420/1243) | -8.2 [-19.3, +0.9] | -0.1 [-10.3, +9.4] | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | distance-weighted success rate, pp | +3.0 [-6.3, +10.8] (420/1243) | -6.2 [-16.7, +3.6] | +7.9 [-4.3, +17.9] | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | distance-weighted gross edge, pp | -2.4 [-11.3, +5.3] (420/1243) | -10.7 [-22.1, -0.7] | +2.1 [-9.6, +12.2] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | MEDIUM - WEAK | timeout share, pp | -2.7 [-5.3, -0.1] (420/1243) | -4.0 [-7.6, -0.1] | -2.0 [-5.4, +1.6] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | MEDIUM - WEAK | timeout term, bps | -0.7 [-1.6, -0.0] (420/1243) | -0.7 [-1.3, -0.1] | -0.7 [-2.0, +0.4] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | MEDIUM - WEAK | net EV per trade, bps | -1.5 [-3.6, +0.6] (420/1243) | -3.4 [-6.0, -1.1] | +0.0 [-3.1, +3.0] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | STRONG - MEDIUM | mean target distance, bps | +0.4 [-1.6, +2.2] (65/420) | -2.2 [-3.5, -0.7] | +0.4 [-1.4, +2.0] | NOT READABLE |
| ASIA | STRONG - MEDIUM | mean stop distance, bps | +2.2 [-0.0, +4.3] (65/420) | +0.3 [-1.1, +1.8] | +1.6 [-0.3, +3.5] | NOT READABLE |
| ASIA | STRONG - MEDIUM | payoff ratio sum T / sum S | -0.08 [-0.14, -0.04] (65/420) | -0.18 [-0.25, -0.11] | -0.05 [-0.11, +0.00] | NOT READABLE |
| ASIA | STRONG - MEDIUM | mean target distance, ATR | -0.2 [-0.3, -0.0] (65/420) | -0.3 [-0.4, -0.2] | -0.1 [-0.2, +0.1] | NOT READABLE |
| ASIA | STRONG - MEDIUM | mean ATR, bps of entry | +1.3 [-0.0, +2.6] (65/420) | +0.1 [-0.7, +1.1] | +0.9 [-0.3, +2.2] | NOT READABLE |
| ASIA | STRONG - MEDIUM | gross breakeven rate, pp | +2.4 [+1.1, +4.0] (65/420) | +5.2 [+3.4, +7.2] | +1.3 [-0.1, +3.1] | NOT READABLE |
| ASIA | STRONG - MEDIUM | success rate, pp | +16.3 [+4.4, +27.9] (65/420) | +18.3 [-4.2, +40.6] | +13.4 [-1.3, +27.5] | NOT READABLE |
| ASIA | STRONG - MEDIUM | gross edge, pp | +13.8 [+1.9, +25.3] (65/420) | +13.1 [-8.8, +34.8] | +12.1 [-2.5, +26.1] | NOT READABLE |
| ASIA | STRONG - MEDIUM | distance-weighted success rate, pp | +17.5 [+5.1, +29.0] (65/420) | +23.5 [-1.9, +48.6] | +13.4 [-0.9, +26.3] | NOT READABLE |
| ASIA | STRONG - MEDIUM | distance-weighted gross edge, pp | +15.1 [+2.7, +25.6] (65/420) | +18.3 [-6.7, +41.9] | +12.0 [-2.2, +24.6] | NOT READABLE |
| ASIA | STRONG - MEDIUM | timeout share, pp | +2.2 [-4.5, +8.8] (65/420) | +0.1 [-8.5, +11.6] | +3.0 [-6.3, +11.6] | NOT READABLE |
| ASIA | STRONG - MEDIUM | timeout term, bps | +1.1 [-0.8, +2.9] (65/420) | -0.1 [-1.3, +1.5] | +1.5 [-1.2, +3.8] | NOT READABLE |
| ASIA | STRONG - MEDIUM | net EV per trade, bps | +6.2 [+2.1, +10.0] (65/420) | +4.5 [-0.4, +10.0] | +6.1 [+0.9, +10.6] | NOT READABLE |

## 6. Geometry-controlled tier gaps, P0, main window

- Strata: per-signal breakeven S / (T + S) quintile x (T + S) tercile; cut points from the session's H1 rows, all tiers.
- d_raw = A - B. d_within = A re-weighted to B's stratum mix - B on the strata A covers. Geometry share = d_raw - d_within.

| Session | Comparison | Outcome | d_raw [95 % CI] | d_within FULL [95 % CI] (n) | H1 d_within | H2 d_within | Label (d_within) | B rows covered % |
|---|---|---|---|---|---|---|---|---|
| NY | STRONG - WEAK | success rate, pp | +5.8 [+0.3, +10.8] | -2.3 [-9.3, +4.2] (342/3523) | -6.5 [-15.1, +3.8] | -2.6 [-10.7, +5.4] | NO DIFFERENCE SHOWN | 100.0 |
| NY | STRONG - WEAK | timeout share, pp | -5.8 [-7.5, -3.9] | -4.3 [-6.8, -1.5] (342/3523) | -3.4 [-8.0, +0.2] | -3.1 [-7.6, +2.4] | NO DIFFERENCE SHOWN | 100.0 |
| NY | STRONG - WEAK | net EV per trade, bps | -0.0 [-1.8, +1.6] | -1.9 [-4.0, +0.1] (342/3523) | -2.1 [-4.4, +0.3] | -2.4 [-5.4, +0.7] | NO DIFFERENCE SHOWN | 100.0 |
| NY | MEDIUM - WEAK | success rate, pp | -0.2 [-4.5, +3.9] | -1.1 [-5.3, +3.1] (1552/3523) | -3.9 [-9.5, +2.5] | +0.5 [-4.8, +5.9] | NO DIFFERENCE SHOWN | 100.0 |
| NY | MEDIUM - WEAK | timeout share, pp | -0.8 [-1.9, +0.4] | +0.1 [-1.3, +1.4] (1552/3523) | +0.5 [-1.8, +2.6] | +0.1 [-1.9, +2.2] | NO DIFFERENCE SHOWN | 100.0 |
| NY | MEDIUM - WEAK | net EV per trade, bps | -1.0 [-2.2, +0.3] | -0.8 [-1.9, +0.4] (1552/3523) | -1.1 [-2.5, +0.5] | -0.6 [-2.1, +1.0] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | STRONG - WEAK | success rate, pp | -1.1 [-14.1, +11.6] | -7.0 [-19.1, +7.1] (131/1049) | -2.8 [-22.5, +18.4] | -2.4 [-20.9, +16.5] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | STRONG - WEAK | timeout share, pp | -4.1 [-9.8, +2.0] | -0.5 [-9.5, +6.9] (131/1049) | +5.0 [-5.8, +11.1] | -5.5 [-16.7, +10.2] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | STRONG - WEAK | net EV per trade, bps | -0.4 [-4.7, +4.8] | -1.8 [-5.8, +3.4] (131/1049) | -1.2 [-5.8, +3.3] | +0.2 [-7.1, +10.1] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | MEDIUM - WEAK | success rate, pp | +3.9 [-2.3, +9.8] | +0.4 [-5.9, +6.5] (485/1049) | +1.9 [-7.2, +11.3] | -2.2 [-11.7, +6.6] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | MEDIUM - WEAK | timeout share, pp | -2.6 [-6.2, +0.9] | -0.8 [-5.0, +4.2] (485/1049) | +3.9 [-0.9, +11.4] | -4.2 [-10.0, +3.1] | NO DIFFERENCE SHOWN | 100.0 |
| LONDON | MEDIUM - WEAK | net EV per trade, bps | +0.4 [-1.9, +2.7] | -0.6 [-2.6, +1.5] (485/1049) | +0.9 [-1.0, +3.0] | -1.9 [-5.4, +1.4] | NO DIFFERENCE SHOWN | 100.0 |
| ASIA | STRONG - WEAK | success rate, pp | +17.9 [+4.3, +30.5] | +6.0 [-10.2, +23.6] (65/1243) | +17.4 [-15.0, +38.4] | +2.3 [-14.5, +23.9] | NOT READABLE | 73.9 |
| ASIA | STRONG - WEAK | timeout share, pp | -0.5 [-7.1, +6.3] | +6.7 [-4.6, +18.3] (65/1243) | +2.3 [-7.6, +18.2] | +8.4 [-5.7, +22.8] | NOT READABLE | 73.9 |
| ASIA | STRONG - WEAK | net EV per trade, bps | +4.7 [-0.0, +9.1] | +4.0 [-0.3, +8.7] (65/1243) | +4.1 [-2.8, +9.0] | +4.1 [-1.1, +11.0] | NOT READABLE | 73.9 |
| ASIA | MEDIUM - WEAK | success rate, pp | +1.6 [-6.0, +8.5] | -2.1 [-10.7, +5.6] (420/1243) | -7.5 [-20.1, +3.7] | +0.7 [-11.1, +11.3] | NO DIFFERENCE SHOWN | 100.0 |
| ASIA | MEDIUM - WEAK | timeout share, pp | -2.7 [-5.3, -0.1] | +0.2 [-3.7, +4.1] (420/1243) | -2.7 [-7.3, +2.6] | +1.4 [-3.2, +5.8] | NO DIFFERENCE SHOWN | 100.0 |
| ASIA | MEDIUM - WEAK | net EV per trade, bps | -1.5 [-3.6, +0.6] | -1.5 [-3.9, +1.0] (420/1243) | -3.7 [-6.6, -1.0] | -0.2 [-3.9, +3.9] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) | 100.0 |

| Session | Breakeven quintile cut points (H1) | T + S tercile cut points, bps (H1) | Occupied strata (P0) |
|---|---|---|---|
| NY | 0.389 / 0.478 / 0.478 / 0.478 | 20.4 / 27.9 | 15 of 15 |
| LONDON | 0.401 / 0.444 / 0.444 / 0.444 | 19.6 / 26.5 | 15 of 15 |
| ASIA | 0.401 / 0.491 / 0.561 / 0.561 | 21.6 / 26.8 | 15 of 15 |

- Tied cut points: most rows carry the ATR fallback target and the clamped stop, so they share one per-signal breakeven (NY 1.6 / 3.35, LONDON 1.6 / 3.6, ASIA 1.6 / 2.85). Tied quintiles merge into fewer strata; the count above is what the re-weighting uses.

### 6.1 Tier gaps inside one target type, P0, main window

- The H3 column is the forward segment (no halves, no label).

| Session | Target type | Comparison | Metric | FULL d [95 % CI] (n) | H1 d [95 % CI] | H2 d [95 % CI] | Label | H3 d [95 % CI] (n) |
|---|---|---|---|---|---|---|---|---|
| NY | none | STRONG - WEAK | mean target distance, ATR | -0.0 [-0.0, +0.0] (259/1654) | -0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | -0.0 [-0.0, -0.0] (43/321) |
| NY | none | STRONG - WEAK | gross breakeven rate, pp | +0.0 [+0.0, +0.1] (259/1654) | +0.0 [+0.0, +0.1] | +0.0 [+0.0, +0.1] | CONFIRMED (d > 0) | +0.0 [+0.0, +0.0] (43/321) |
| NY | none | STRONG - WEAK | success rate, pp | +10.2 [+1.2, +18.4] (259/1654) | +6.3 [-4.7, +17.0] | +12.7 [-0.6, +23.3] | NO DIFFERENCE SHOWN | +10.7 [-9.6, +26.9] (43/321) |
| NY | none | STRONG - WEAK | timeout share, pp | -5.3 [-6.9, -3.5] (259/1654) | -4.6 [-7.2, -1.6] | -5.7 [-7.9, -3.7] | CONFIRMED (d < 0) | -7.2 [-11.8, -3.2] (43/321) |
| NY | none | STRONG - WEAK | gross edge, pp | +10.1 [+1.5, +18.1] (259/1654) | +6.2 [-4.5, +16.7] | +12.6 [-1.1, +23.2] | NO DIFFERENCE SHOWN | +10.7 [-9.4, +27.0] (43/321) |
| NY | none | STRONG - WEAK | net EV per trade, bps | +2.0 [-0.8, +4.6] (259/1654) | +1.3 [-1.2, +3.9] | +2.5 [-2.2, +6.1] | NO DIFFERENCE SHOWN | +4.6 [-3.3, +9.8] (43/321) |
| NY | none | MEDIUM - WEAK | mean target distance, ATR | -0.0 [-0.0, +0.0] (939/1654) | -0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | -0.0 [-0.0, -0.0] (178/321) |
| NY | none | MEDIUM - WEAK | gross breakeven rate, pp | +0.0 [+0.0, +0.1] (939/1654) | +0.0 [+0.0, +0.1] | +0.0 [-0.0, +0.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) | +0.0 [+0.0, +0.0] (178/321) |
| NY | none | MEDIUM - WEAK | success rate, pp | +0.6 [-4.7, +5.7] (939/1654) | +1.1 [-6.1, +7.9] | +0.2 [-7.3, +7.1] | NO DIFFERENCE SHOWN | +8.0 [-1.0, +16.3] (178/321) |
| NY | none | MEDIUM - WEAK | timeout share, pp | -1.4 [-2.8, +0.1] (939/1654) | -0.3 [-2.5, +2.0] | -2.1 [-3.9, -0.2] | DISCOVERY ONLY (H2) (d < 0) | -5.5 [-10.9, -1.1] (178/321) |
| NY | none | MEDIUM - WEAK | gross edge, pp | +0.5 [-4.9, +5.6] (939/1654) | +1.1 [-6.0, +7.9] | +0.2 [-7.6, +7.1] | NO DIFFERENCE SHOWN | +8.0 [-1.1, +16.4] (178/321) |
| NY | none | MEDIUM - WEAK | net EV per trade, bps | -0.5 [-2.1, +0.9] (939/1654) | -0.0 [-1.8, +1.8] | -0.9 [-3.1, +1.1] | NO DIFFERENCE SHOWN | +2.2 [-2.5, +5.8] (178/321) |
| NY | swing | STRONG - WEAK | mean target distance, ATR | -0.2 [-0.4, +0.1] (59/1211) | -0.2 [-0.6, +0.1] | -0.1 [-0.4, +0.3] | NOT READABLE | +0.1 [-0.4, +0.7] (13/221) |
| NY | swing | STRONG - WEAK | gross breakeven rate, pp | +1.8 [-1.9, +5.0] (59/1211) | +1.1 [-2.6, +6.0] | +2.2 [-3.7, +6.0] | NOT READABLE | -1.2 [-7.1, +4.5] (13/221) |
| NY | swing | STRONG - WEAK | success rate, pp | -14.1 [-25.6, -1.1] (59/1211) | -10.0 [-27.8, +9.5] | -17.3 [-31.4, +1.3] | NOT READABLE | +8.1 [-26.9, +31.5] (13/221) |
| NY | swing | STRONG - WEAK | timeout share, pp | -3.3 [-9.1, +4.7] (59/1211) | -4.3 [-12.9, +5.9] | -2.5 [-9.6, +10.2] | NOT READABLE | -9.5 [-13.9, -6.3] (13/221) |
| NY | swing | STRONG - WEAK | gross edge, pp | -16.0 [-28.8, -1.4] (59/1211) | -11.1 [-29.0, +7.8] | -19.5 [-36.1, +2.6] | NOT READABLE | +9.3 [-22.4, +31.2] (13/221) |
| NY | swing | STRONG - WEAK | net EV per trade, bps | -6.5 [-12.2, -1.2] (59/1211) | -4.4 [-9.3, +1.0] | -8.0 [-15.9, +0.3] | NOT READABLE | +2.0 [-11.8, +10.0] (13/221) |
| NY | swing | MEDIUM - WEAK | mean target distance, ATR | -0.2 [-0.2, -0.1] (488/1211) | -0.2 [-0.3, -0.0] | -0.2 [-0.3, +0.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) | +0.0 [-0.3, +0.2] (80/221) |
| NY | swing | MEDIUM - WEAK | gross breakeven rate, pp | +2.1 [+0.5, +3.4] (488/1211) | +1.8 [+0.4, +3.1] | +2.3 [-0.1, +4.2] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) | +0.9 [-2.3, +5.3] (80/221) |
| NY | swing | MEDIUM - WEAK | success rate, pp | -2.6 [-9.6, +5.5] (488/1211) | -5.0 [-15.2, +7.3] | -0.1 [-9.6, +10.8] | NO DIFFERENCE SHOWN | +7.0 [-0.3, +19.6] (80/221) |
| NY | swing | MEDIUM - WEAK | timeout share, pp | +1.2 [-2.4, +4.2] (488/1211) | -0.2 [-6.5, +4.8] | +2.2 [-1.9, +6.0] | NO DIFFERENCE SHOWN | -4.5 [-11.3, +1.3] (80/221) |
| NY | swing | MEDIUM - WEAK | gross edge, pp | -4.6 [-11.6, +3.2] (488/1211) | -6.8 [-17.1, +5.6] | -2.4 [-11.7, +8.2] | NO DIFFERENCE SHOWN | +6.1 [-0.0, +17.5] (80/221) |
| NY | swing | MEDIUM - WEAK | net EV per trade, bps | -1.6 [-3.6, +0.7] (488/1211) | -2.1 [-4.9, +1.0] | -1.1 [-3.9, +2.0] | NO DIFFERENCE SHOWN | +1.0 [-1.2, +5.0] (80/221) |
| LONDON | none | STRONG - WEAK | mean target distance, ATR | -0.0 [-0.0, +0.0] (106/464) | -0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | -0.0 [-0.0, +0.0] (9/65) |
| LONDON | none | STRONG - WEAK | gross breakeven rate, pp | +0.1 [+0.0, +0.2] (106/464) | +0.0 [-0.0, +0.1] | +0.2 [+0.0, +0.3] | NO DIFFERENCE SHOWN | +0.7 [+0.0, +2.0] (9/65) |
| LONDON | none | STRONG - WEAK | success rate, pp | -1.6 [-15.8, +13.7] (106/464) | -1.7 [-20.8, +18.8] | -3.0 [-23.1, +22.1] | NO DIFFERENCE SHOWN | +5.6 [-12.6, +41.2] (9/65) |
| LONDON | none | STRONG - WEAK | timeout share, pp | -3.7 [-9.9, +2.8] (106/464) | +0.4 [-3.6, +6.5] | -6.3 [-17.9, +5.4] | NO DIFFERENCE SHOWN | +0.0 [+0.0, +0.0] (9/65) |
| LONDON | none | STRONG - WEAK | gross edge, pp | -1.7 [-16.4, +13.5] (106/464) | -1.7 [-20.9, +18.1] | -3.1 [-23.1, +22.8] | NO DIFFERENCE SHOWN | +4.9 [-13.8, +40.3] (9/65) |
| LONDON | none | STRONG - WEAK | net EV per trade, bps | -1.0 [-5.8, +4.3] (106/464) | -1.6 [-6.7, +3.9] | +0.7 [-8.4, +13.9] | NO DIFFERENCE SHOWN | +3.3 [-3.1, +20.4] (9/65) |
| LONDON | none | MEDIUM - WEAK | mean target distance, ATR | -0.0 [-0.0, +0.0] (321/464) | -0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | -0.0 [-0.0, +0.0] (41/65) |
| LONDON | none | MEDIUM - WEAK | gross breakeven rate, pp | +0.1 [+0.0, +0.2] (321/464) | -0.0 [-0.1, +0.0] | +0.2 [+0.0, +0.3] | DISCOVERY ONLY (H2) (d > 0) | +0.7 [+0.0, +2.0] (41/65) |
| LONDON | none | MEDIUM - WEAK | success rate, pp | +6.1 [-1.4, +13.5] (321/464) | +10.4 [+1.0, +20.1] | +2.1 [-9.6, +13.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) | +16.2 [+0.7, +31.7] (41/65) |
| LONDON | none | MEDIUM - WEAK | timeout share, pp | -2.2 [-7.1, +2.4] (321/464) | +1.1 [-2.2, +5.0] | -5.0 [-13.1, +2.7] | NO DIFFERENCE SHOWN | +7.3 [+0.0, +14.6] (41/65) |
| LONDON | none | MEDIUM - WEAK | gross edge, pp | +6.0 [-1.5, +13.3] (321/464) | +10.4 [+1.4, +20.6] | +1.9 [-9.6, +13.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) | +15.5 [-1.4, +31.6] (41/65) |
| LONDON | none | MEDIUM - WEAK | net EV per trade, bps | +0.9 [-2.5, +4.0] (321/464) | +2.8 [+0.1, +5.9] | -0.8 [-6.4, +4.4] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) | +6.1 [+0.9, +10.9] (41/65) |
| LONDON | swing | STRONG - WEAK | mean target distance, ATR | -0.2 [-0.6, +0.3] (22/484) | -0.2 [-1.0, +0.4] | -0.2 [-0.6, +0.5] | NOT READABLE | -0.3 [-0.6, +0.3] (3/86) |
| LONDON | swing | STRONG - WEAK | gross breakeven rate, pp | +3.8 [-2.3, +9.5] (22/484) | +2.8 [-5.2, +15.4] | +4.4 [-4.5, +10.3] | NOT READABLE | +6.1 [-0.2, +8.6] (3/86) |
| LONDON | swing | STRONG - WEAK | success rate, pp | -6.0 [-31.4, +16.0] (22/484) | -19.4 [-45.3, +15.4] | +3.2 [-40.2, +33.4] | NOT READABLE | -8.5 [-46.1, +69.5] (3/86) |
| LONDON | swing | STRONG - WEAK | timeout share, pp | +3.7 [-12.3, +27.3] (22/484) | +14.1 [-12.9, +59.8] | -3.6 [-16.9, +18.3] | NOT READABLE | +53.9 [-11.3, +89.6] (3/86) |
| LONDON | swing | STRONG - WEAK | gross edge, pp | -9.8 [-33.2, +10.4] (22/484) | -22.2 [-51.5, +7.5] | -1.1 [-36.0, +29.2] | NOT READABLE | -14.6 [-53.4, +68.3] (3/86) |
| LONDON | swing | STRONG - WEAK | net EV per trade, bps | -1.9 [-8.3, +3.2] (22/484) | -4.8 [-14.1, +2.8] | +0.0 [-9.5, +7.4] | NOT READABLE | +5.2 [-1.3, +18.6] (3/86) |
| LONDON | swing | MEDIUM - WEAK | mean target distance, ATR | -0.2 [-0.4, -0.1] (136/484) | -0.1 [-0.3, +0.2] | -0.3 [-0.5, -0.1] | NO DIFFERENCE SHOWN | -0.3 [-0.5, +0.2] (21/86) |
| LONDON | swing | MEDIUM - WEAK | gross breakeven rate, pp | +4.1 [+1.5, +6.7] (136/484) | +1.1 [-2.0, +3.5] | +5.5 [+2.2, +8.7] | NO DIFFERENCE SHOWN | +5.9 [+1.1, +7.7] (21/86) |
| LONDON | swing | MEDIUM - WEAK | success rate, pp | -0.4 [-10.6, +8.9] (136/484) | -9.0 [-24.9, +5.9] | +4.2 [-8.4, +15.6] | NO DIFFERENCE SHOWN | +10.5 [-24.6, +51.4] (21/86) |
| LONDON | swing | MEDIUM - WEAK | timeout share, pp | -0.4 [-5.8, +6.1] (136/484) | +8.2 [-0.5, +17.0] | -5.5 [-11.7, +1.8] | NO DIFFERENCE SHOWN | +6.3 [-12.2, +10.7] (21/86) |
| LONDON | swing | MEDIUM - WEAK | gross edge, pp | -4.5 [-14.4, +4.6] (136/484) | -10.0 [-25.6, +5.3] | -1.2 [-13.8, +10.5] | NO DIFFERENCE SHOWN | +4.7 [-27.0, +46.8] (21/86) |
| LONDON | swing | MEDIUM - WEAK | net EV per trade, bps | -0.7 [-3.7, +2.2] (136/484) | -1.3 [-5.2, +2.2] | -0.5 [-4.4, +4.0] | NO DIFFERENCE SHOWN | +2.3 [-7.7, +11.9] (21/86) |
| ASIA | none | STRONG - WEAK | mean target distance, ATR | +0.0 [-0.0, +0.0] (51/432) | +0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NOT READABLE | -0.0 [-0.0, +0.0] (14/129) |
| ASIA | none | STRONG - WEAK | gross breakeven rate, pp | +0.0 [-0.0, +0.0] (51/432) | +0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NOT READABLE | +0.2 [+0.0, +0.4] (14/129) |
| ASIA | none | STRONG - WEAK | success rate, pp | +11.1 [-3.6, +25.6] (51/432) | +1.9 [-21.7, +28.8] | +15.1 [-3.1, +32.5] | NOT READABLE | +17.0 [-11.7, +54.4] (14/129) |
| ASIA | none | STRONG - WEAK | timeout share, pp | +1.9 [-4.0, +8.0] (51/432) | +0.7 [-9.9, +15.4] | +2.8 [-3.8, +8.9] | NOT READABLE | -2.3 [-5.0, +0.0] (14/129) |
| ASIA | none | STRONG - WEAK | gross edge, pp | +11.1 [-3.5, +25.6] (51/432) | +1.9 [-21.4, +28.0] | +15.1 [-2.4, +32.7] | NOT READABLE | +16.8 [-13.1, +54.3] (14/129) |
| ASIA | none | STRONG - WEAK | net EV per trade, bps | +5.0 [-0.5, +10.1] (51/432) | +0.6 [-4.5, +7.1] | +7.0 [-0.3, +13.5] | NOT READABLE | +4.2 [-5.1, +15.1] (14/129) |
| ASIA | none | MEDIUM - WEAK | mean target distance, ATR | +0.0 [-0.0, +0.0] (251/432) | +0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | -0.0 [-0.0, +0.0] (96/129) |
| ASIA | none | MEDIUM - WEAK | gross breakeven rate, pp | +0.0 [-0.0, +0.0] (251/432) | +0.0 [-0.0, +0.0] | -0.0 [-0.0, +0.0] | NO DIFFERENCE SHOWN | +0.2 [+0.0, +0.4] (96/129) |
| ASIA | none | MEDIUM - WEAK | success rate, pp | -5.0 [-13.5, +3.9] (251/432) | -4.7 [-16.1, +7.6] | -4.8 [-16.9, +7.8] | NO DIFFERENCE SHOWN | +5.8 [-7.4, +17.1] (96/129) |
| ASIA | none | MEDIUM - WEAK | timeout share, pp | +0.4 [-2.8, +4.1] (251/432) | -1.3 [-7.8, +5.6] | +1.5 [-1.5, +6.5] | NO DIFFERENCE SHOWN | -0.2 [-2.3, +2.7] (96/129) |
| ASIA | none | MEDIUM - WEAK | gross edge, pp | -5.0 [-13.7, +3.9] (251/432) | -4.8 [-16.3, +7.3] | -4.8 [-16.9, +8.2] | NO DIFFERENCE SHOWN | +5.7 [-7.4, +16.9] (96/129) |
| ASIA | none | MEDIUM - WEAK | net EV per trade, bps | -1.2 [-3.8, +1.7] (251/432) | -2.3 [-5.3, +0.9] | -0.2 [-4.1, +4.4] | NO DIFFERENCE SHOWN | +1.5 [-2.2, +4.6] (96/129) |
| ASIA | swing | STRONG - WEAK | mean target distance, ATR | -0.5 [-0.9, -0.1] (12/693) | -1.1 [-1.4, -0.9] | -0.4 [-0.8, +0.0] | NOT READABLE | +0.2 [-0.0, +0.5] (5/159) |
| ASIA | swing | STRONG - WEAK | gross breakeven rate, pp | +10.6 [+5.0, +14.7] (12/693) | +18.9 [+12.9, +23.9] | +9.2 [+2.9, +13.0] | NOT READABLE | -2.5 [-4.8, -1.0] (5/159) |
| ASIA | swing | STRONG - WEAK | success rate, pp | +25.7 [-2.6, +58.5] (12/693) | +58.4 [+48.0, +69.3] | +19.4 [-12.0, +57.0] | NOT READABLE | -44.7 [-53.5, -32.9] (5/159) |
| ASIA | swing | STRONG - WEAK | timeout share, pp | +5.0 [-12.5, +24.5] (12/693) | -12.9 [-18.8, -7.7] | +9.0 [-11.4, +30.9] | NOT READABLE | +35.0 [-7.7, +98.2] (5/159) |
| ASIA | swing | STRONG - WEAK | gross edge, pp | +15.1 [-13.4, +47.9] (12/693) | +39.6 [+29.0, +52.2] | +10.2 [-22.9, +49.2] | NOT READABLE | -42.1 [-52.0, -28.9] (5/159) |
| ASIA | swing | STRONG - WEAK | net EV per trade, bps | +8.0 [+2.2, +15.5] (12/693) | +7.8 [+5.0, +10.9] | +8.0 [+0.9, +17.0] | NOT READABLE | -6.6 [-21.9, +15.3] (5/159) |
| ASIA | swing | MEDIUM - WEAK | mean target distance, ATR | -0.3 [-0.4, -0.2] (141/693) | -0.1 [-0.3, +0.1] | -0.4 [-0.5, -0.2] | NO DIFFERENCE SHOWN | -0.3 [-0.7, -0.1] (39/159) |
| ASIA | swing | MEDIUM - WEAK | gross breakeven rate, pp | +4.2 [+2.5, +5.6] (141/693) | +2.1 [-0.6, +5.3] | +5.2 [+3.5, +6.9] | NO DIFFERENCE SHOWN | +5.0 [+1.4, +10.0] (39/159) |
| ASIA | swing | MEDIUM - WEAK | success rate, pp | +0.9 [-11.7, +12.3] (141/693) | -19.8 [-36.9, -3.4] | +14.0 [-1.6, +29.5] | NO DIFFERENCE SHOWN | +9.2 [-12.8, +40.3] (39/159) |
| ASIA | swing | MEDIUM - WEAK | timeout share, pp | -3.9 [-8.5, +1.1] (141/693) | -3.9 [-11.9, +5.4] | -4.0 [-9.4, +1.5] | NO DIFFERENCE SHOWN | +7.8 [-4.2, +20.8] (39/159) |
| ASIA | swing | MEDIUM - WEAK | gross edge, pp | -3.3 [-15.1, +7.8] (141/693) | -21.9 [-38.9, -6.8] | +8.8 [-6.6, +23.5] | NO DIFFERENCE SHOWN | +4.2 [-17.5, +33.0] (39/159) |
| ASIA | swing | MEDIUM - WEAK | net EV per trade, bps | -2.3 [-5.5, +0.6] (141/693) | -7.1 [-11.5, -3.3] | +0.9 [-3.3, +5.0] | NO DIFFERENCE SHOWN | +1.9 [-8.7, +13.5] (39/159) |

| Session | Target type | Tier | n | Success % | Timeout % | Gross BE % | Gross edge pp | Net EV bps |
|---|---|---|---|---|---|---|---|---|
| NY | none | STRONG | 259 | 51.4 | 0.8 | 47.8 | +3.6 | -1.8 |
| NY | none | MEDIUM | 939 | 41.7 | 4.7 | 47.8 | -6.0 | -4.3 |
| NY | none | WEAK | 1654 | 41.2 | 6.0 | 47.7 | -6.5 | -3.8 |
| NY | swing | STRONG | 59 (NOT READABLE) | 22.0 | 6.8 | 43.0 | -20.9 | -9.2 |
| NY | swing | MEDIUM | 488 | 33.6 | 11.3 | 43.2 | -9.6 | -4.3 |
| NY | swing | WEAK | 1211 | 36.2 | 10.1 | 41.1 | -5.0 | -2.8 |
| NY | hvn | STRONG | 24 (NOT READABLE) | 45.8 | 8.3 | 59.8 | -13.9 | -4.6 |
| NY | hvn | MEDIUM | 125 | 50.4 | 12.0 | 51.6 | -1.2 | -2.7 |
| NY | hvn | WEAK | 658 | 44.7 | 9.9 | 49.2 | -4.5 | -2.5 |
| LONDON | none | STRONG | 106 | 41.5 | 4.7 | 44.4 | -2.9 | -1.3 |
| LONDON | none | MEDIUM | 321 | 49.2 | 6.2 | 44.4 | +4.8 | +0.6 |
| LONDON | none | WEAK | 464 | 43.1 | 8.4 | 44.3 | -1.2 | -0.3 |
| LONDON | swing | STRONG | 22 (NOT READABLE) | 36.4 | 13.6 | 46.5 | -10.2 | -4.1 |
| LONDON | swing | MEDIUM | 136 | 41.9 | 9.6 | 46.8 | -4.9 | -2.9 |
| LONDON | swing | WEAK | 484 | 42.4 | 9.9 | 42.7 | -0.4 | -2.1 |
| LONDON | hvn | STRONG | 3 (NOT READABLE) | 66.7 | 0.0 | 60.3 | +6.4 | +6.0 |
| LONDON | hvn | MEDIUM | 28 (NOT READABLE) | 32.1 | 14.3 | 54.6 | -22.4 | -6.8 |
| LONDON | hvn | WEAK | 101 | 38.6 | 19.8 | 49.5 | -10.9 | -1.2 |
| ASIA | none | STRONG | 51 (NOT READABLE) | 66.7 | 5.9 | 56.1 | +10.5 | +2.6 |
| ASIA | none | MEDIUM | 251 | 50.6 | 4.4 | 56.1 | -5.5 | -3.5 |
| ASIA | none | WEAK | 432 | 55.6 | 3.9 | 56.1 | -0.6 | -2.4 |
| ASIA | swing | STRONG | 12 (NOT READABLE) | 66.7 | 16.7 | 53.1 | +13.6 | +6.5 |
| ASIA | swing | MEDIUM | 141 | 41.8 | 7.8 | 46.7 | -4.9 | -3.8 |
| ASIA | swing | WEAK | 693 | 41.0 | 11.7 | 42.5 | -1.6 | -1.5 |
| ASIA | hvn | STRONG | 2 (NOT READABLE) | 0.0 | 0.0 | 47.7 | -47.7 | -23.0 |
| ASIA | hvn | MEDIUM | 28 (NOT READABLE) | 60.7 | 3.6 | 57.3 | +3.4 | -4.0 |
| ASIA | hvn | WEAK | 118 | 48.3 | 3.4 | 49.7 | -1.4 | -5.5 |

## 7. Era segments (no halves): the key tier gaps inside each settings or code era

- E1 = v51 edge to the v66 collector deploy (2026-08-10 18:36:01); E2 = v66 to the ATR step (2026-08-20); E3 = ATR step to 2026-09-11; H3 = 2026-09-14 to 2026-09-24 08:45 (forward, v68, the 2026-09-21 15:38 code deploy inside it).

| Session | Comparison | Metric | E1 d [95 % CI] (n) | E2 d [95 % CI] (n) | E3 d [95 % CI] (n) | H3 d [95 % CI] (n) |
|---|---|---|---|---|---|---|
| NY | STRONG - WEAK | mean target distance, bps | -0.3 [-1.2, +0.8] (153/1426) | +3.4 [+0.1, +5.4] (63/388) | +2.0 [-0.5, +4.2] (126/1709) | +1.9 [-1.3, +3.5] (65/653) |
| NY | STRONG - WEAK | payoff ratio sum T / sum S | -0.11 [-0.17, -0.04] (153/1426) | -0.10 [-0.29, +0.05] (63/388) | -0.10 [-0.17, -0.03] (126/1709) | -0.05 [-0.15, +0.15] (65/653) |
| NY | STRONG - WEAK | gross breakeven rate, pp | +2.2 [+0.8, +3.6] (153/1426) | +2.4 [-1.0, +6.4] (63/388) | +2.1 [+0.7, +3.7] (126/1709) | +1.1 [-2.9, +3.5] (65/653) |
| NY | STRONG - WEAK | success rate, pp | +5.7 [-3.0, +13.0] (153/1426) | +10.6 [-9.1, +25.1] (63/388) | +3.3 [-6.0, +12.9] (126/1709) | +7.4 [-19.0, +27.6] (65/653) |
| NY | STRONG - WEAK | gross edge, pp | +3.4 [-5.4, +10.8] (153/1426) | +8.2 [-10.2, +24.2] (63/388) | +1.2 [-8.3, +11.3] (126/1709) | +6.3 [-16.2, +25.3] (65/653) |
| NY | STRONG - WEAK | net EV per trade, bps | -0.0 [-2.0, +1.7] (153/1426) | -0.3 [-4.1, +2.4] (63/388) | -0.6 [-4.4, +3.4] (126/1709) | +3.4 [-4.9, +9.3] (65/653) |
| NY | MEDIUM - WEAK | mean target distance, bps | -0.0 [-0.6, +0.6] (665/1426) | -0.7 [-1.7, +1.6] (200/388) | +0.4 [-0.5, +1.3] (687/1709) | +1.3 [+0.3, +2.2] (291/653) |
| NY | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.06 [-0.09, -0.02] (665/1426) | -0.07 [-0.14, -0.01] (200/388) | -0.03 [-0.07, +0.00] (687/1709) | -0.02 [-0.06, +0.05] (291/653) |
| NY | MEDIUM - WEAK | gross breakeven rate, pp | +1.1 [+0.4, +1.9] (665/1426) | +1.6 [+0.2, +2.9] (200/388) | +0.7 [-0.1, +1.5] (687/1709) | +0.4 [-1.0, +1.4] (291/653) |
| NY | MEDIUM - WEAK | success rate, pp | -2.2 [-8.0, +4.3] (665/1426) | +4.8 [-4.8, +13.1] (200/388) | +0.2 [-7.2, +6.8] (687/1709) | +4.0 [-4.7, +12.4] (291/653) |
| NY | MEDIUM - WEAK | gross edge, pp | -3.4 [-8.9, +2.7] (665/1426) | +3.2 [-6.1, +11.1] (200/388) | -0.5 [-7.8, +6.2] (687/1709) | +3.6 [-3.9, +11.6] (291/653) |
| NY | MEDIUM - WEAK | net EV per trade, bps | -1.2 [-2.7, +0.5] (665/1426) | -1.8 [-3.3, +1.3] (200/388) | -0.7 [-3.0, +1.3] (687/1709) | +0.7 [-3.2, +4.0] (291/653) |
| LONDON | STRONG - WEAK | mean target distance, bps | +0.4 [-1.1, +2.2] (77/473) | +1.0 [-0.8, +1.7] (8/64) | +1.4 [-2.4, +5.5] (46/512) | -1.6 [-5.4, +6.2] (12/158) |
| LONDON | STRONG - WEAK | payoff ratio sum T / sum S | -0.10 [-0.15, -0.05] (77/473) | -0.03 [-0.15, +0.01] (8/64) | -0.05 [-0.13, +0.04] (46/512) | -0.19 [-0.35, -0.03] (12/158) |
| LONDON | STRONG - WEAK | gross breakeven rate, pp | +1.9 [+0.9, +2.9] (77/473) | +0.6 [-0.3, +2.7] (8/64) | +1.1 [-0.8, +2.9] (46/512) | +3.5 [+0.6, +6.1] (12/158) |
| LONDON | STRONG - WEAK | success rate, pp | -1.2 [-18.0, +15.5] (77/473) | -20.3 [-31.0, -10.6] (8/64) | +3.5 [-19.5, +25.9] (46/512) | -1.5 [-26.4, +24.8] (12/158) |
| LONDON | STRONG - WEAK | gross edge, pp | -3.1 [-20.3, +13.6] (77/473) | -20.9 [-31.4, -11.5] (8/64) | +2.4 [-19.4, +25.0] (46/512) | -5.0 [-32.4, +22.6] (12/158) |
| LONDON | STRONG - WEAK | net EV per trade, bps | -1.7 [-6.2, +2.8] (77/473) | -6.3 [-8.6, -3.5] (8/64) | +3.9 [-6.1, +16.1] (46/512) | +2.3 [-3.8, +16.0] (12/158) |
| LONDON | MEDIUM - WEAK | mean target distance, bps | +0.9 [-0.0, +2.0] (218/473) | -0.3 [-1.2, +0.7] (29/64) | -2.4 [-6.7, +1.9] (238/512) | -0.8 [-2.0, +1.6] (64/158) |
| LONDON | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.07 [-0.11, -0.03] (218/473) | -0.01 [-0.07, +0.05] (29/64) | -0.08 [-0.17, -0.02] (238/512) | -0.20 [-0.33, -0.04] (64/158) |
| LONDON | MEDIUM - WEAK | gross breakeven rate, pp | +1.4 [+0.6, +2.1] (218/473) | +0.1 [-1.0, +1.3] (29/64) | +1.7 [+0.4, +3.5] (238/512) | +3.6 [+0.7, +5.7] (64/158) |
| LONDON | MEDIUM - WEAK | success rate, pp | +8.6 [+0.7, +16.2] (218/473) | +5.1 [-23.4, +39.8] (29/64) | -0.6 [-10.1, +7.9] (238/512) | +10.5 [+1.6, +25.2] (64/158) |
| LONDON | MEDIUM - WEAK | gross edge, pp | +7.2 [-0.1, +14.6] (218/473) | +5.0 [-22.2, +39.3] (29/64) | -2.3 [-12.3, +5.9] (238/512) | +6.9 [-3.6, +23.6] (64/158) |
| LONDON | MEDIUM - WEAK | net EV per trade, bps | +2.2 [-0.2, +4.7] (218/473) | -1.0 [-5.8, +5.4] (29/64) | -1.0 [-5.2, +2.9] (238/512) | +3.2 [-0.2, +8.9] (64/158) |
| ASIA | STRONG - WEAK | mean target distance, bps | -3.4 [-4.5, -2.1] (18/461) | -1.2 [-2.3, -0.4] (3/84) | -1.5 [-3.8, +0.3] (44/698) | -2.1 [-4.1, +0.9] (21/304) |
| ASIA | STRONG - WEAK | payoff ratio sum T / sum S | -0.36 [-0.45, -0.28] (18/461) | -0.29 [-0.59, +0.03] (3/84) | -0.27 [-0.32, -0.19] (44/698) | -0.16 [-0.34, +0.02] (21/304) |
| ASIA | STRONG - WEAK | gross breakeven rate, pp | +9.6 [+7.5, +11.9] (18/461) | +5.7 [-0.8, +11.1] (3/84) | +7.1 [+4.9, +8.6] (44/698) | +4.0 [-0.3, +9.7] (21/304) |
| ASIA | STRONG - WEAK | success rate, pp | +14.5 [-12.5, +38.8] (18/461) | +31.0 [-45.6, +76.5] (3/84) | +17.8 [+1.9, +33.0] (44/698) | -2.2 [-32.5, +17.6] (21/304) |
| ASIA | STRONG - WEAK | gross edge, pp | +4.9 [-22.6, +27.4] (18/461) | +25.2 [-46.7, +66.0] (3/84) | +10.7 [-5.3, +26.5] (44/698) | -6.2 [-35.4, +14.6] (21/304) |
| ASIA | STRONG - WEAK | net EV per trade, bps | +1.1 [-4.8, +6.5] (18/461) | +4.0 [-9.8, +11.5] (3/84) | +6.3 [-0.4, +11.9] (44/698) | -0.6 [-13.2, +10.1] (21/304) |
| ASIA | MEDIUM - WEAK | mean target distance, bps | -1.2 [-1.9, -0.5] (182/461) | -0.9 [-1.8, -0.3] (16/84) | -2.0 [-3.3, -0.7] (222/698) | -2.8 [-4.1, -1.2] (142/304) |
| ASIA | MEDIUM - WEAK | payoff ratio sum T / sum S | -0.18 [-0.25, -0.12] (182/461) | -0.19 [-0.49, +0.14] (16/84) | -0.22 [-0.27, -0.16] (222/698) | -0.24 [-0.31, -0.15] (142/304) |
| ASIA | MEDIUM - WEAK | gross breakeven rate, pp | +4.4 [+2.9, +6.1] (182/461) | +3.6 [-2.1, +8.6] (16/84) | +5.8 [+4.2, +7.0] (222/698) | +6.0 [+3.8, +7.8] (142/304) |
| ASIA | MEDIUM - WEAK | success rate, pp | -3.8 [-14.2, +5.2] (182/461) | +20.5 [-13.1, +52.5] (16/84) | +4.1 [-7.1, +14.4] (222/698) | +7.0 [-6.3, +18.5] (142/304) |
| ASIA | MEDIUM - WEAK | gross edge, pp | -8.2 [-19.5, +1.1] (182/461) | +16.9 [-11.2, +44.4] (16/84) | -1.7 [-12.0, +8.4] (222/698) | +1.0 [-13.9, +13.9] (142/304) |
| ASIA | MEDIUM - WEAK | net EV per trade, bps | -3.4 [-5.9, -1.1] (182/461) | +2.5 [-2.6, +7.1] (16/84) | -0.2 [-3.6, +3.0] (222/698) | +0.8 [-5.4, +6.2] (142/304) |

## 8. Forward segment H3 (2026-09-14 to 2026-09-24): geometry

| Session | Tier | n | Target bps p10/p25/p50/p75/p90 | Mean T | Stop bps p10/p25/p50/p75/p90 | Mean S | Sum T / sum S | Median T/S | Median T, ATR | Median S, ATR | Median ATR, bps |
|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 65 | 9.1/11.8/14.0/22.3/31.6 | 17.9 | 7.4/11.3/13.8/17.6/29.5 | 16.1 | 1.11 | 1.09 | 1.75 | 1.60 | 8.6 |
| NY | MEDIUM | 291 | 9.2/10.7/14.5/20.7/27.8 | 17.3 | 8.0/9.7/13.1/17.3/25.1 | 15.2 | 1.14 | 1.09 | 1.75 | 1.60 | 8.2 |
| NY | WEAK | 653 | 8.9/10.5/13.4/17.6/27.1 | 16.0 | 7.8/9.1/12.1/16.1/22.0 | 13.8 | 1.16 | 1.09 | 1.75 | 1.60 | 7.6 |
| NY | ALL | 1009 | 9.0/10.7/13.6/18.9/27.7 | 16.5 | 7.8/9.4/12.5/16.6/22.8 | 14.4 | 1.15 | 1.09 | 1.75 | 1.60 | 7.8 |
| LONDON | STRONG | 12 | 12.9/13.1/16.1/19.7/28.2 | 18.7 | 9.9/10.3/12.9/15.3/22.5 | 15.0 | 1.25 | 1.25 | 2.00 | 1.60 | 8.1 |
| LONDON | MEDIUM | 64 | 12.7/14.7/17.6/24.1/30.2 | 19.6 | 11.6/12.7/14.3/17.9/24.4 | 15.8 | 1.24 | 1.25 | 2.00 | 1.60 | 8.9 |
| LONDON | WEAK | 158 | 10.1/14.2/19.7/25.4/30.7 | 20.3 | 7.9/11.2/13.7/17.0/21.0 | 14.1 | 1.44 | 1.25 | 2.00 | 1.60 | 8.8 |
| LONDON | ALL | 234 | 10.5/14.5/18.7/25.2/30.2 | 20.1 | 8.8/11.6/14.0/17.4/21.4 | 14.6 | 1.37 | 1.25 | 2.00 | 1.60 | 8.8 |
| ASIA | STRONG | 21 | 9.1/10.0/12.1/14.8/25.6 | 14.6 | 11.7/12.8/15.3/16.9/18.2 | 15.4 | 0.95 | 0.78 | 1.25 | 1.60 | 9.7 |
| ASIA | MEDIUM | 142 | 9.0/10.0/12.7/16.0/20.4 | 14.0 | 11.4/12.7/15.3/18.7/21.8 | 15.9 | 0.88 | 0.78 | 1.25 | 1.60 | 9.6 |
| ASIA | WEAK | 304 | 9.0/10.8/14.1/19.7/28.5 | 16.7 | 9.7/11.9/14.4/17.5/22.5 | 15.0 | 1.12 | 0.84 | 1.26 | 1.60 | 9.1 |
| ASIA | ALL | 467 | 9.0/10.4/13.4/19.0/26.3 | 15.8 | 10.3/12.2/14.9/17.7/22.0 | 15.3 | 1.03 | 0.78 | 1.25 | 1.60 | 9.4 |

### 8.1 Forward segment H3: outcome ledger, main window

| Session | Cell | n | Success % | Stop hit % | Timeout % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Dist-weighted success % | Dist-weighted gross edge pp | Mean T+S bps | W x weighted edge | Timeout term | Fee | Net EV bps [95 % CI] |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 65 (NOT READABLE) | 44.6 | 55.4 | 0.0 | 47.4 | 56.2 | -2.8 | -11.6 | 50.4 | +3.0 | 34.0 | +1.04 | +0.00 | -3.00 | -2.0 [-9.4, +3.1] |
| NY | MEDIUM | 291 | 41.2 | 55.7 | 3.1 | 46.7 | 55.9 | -5.5 | -14.7 | 40.2 | -6.5 | 32.5 | -2.10 | +0.44 | -3.00 | -4.7 [-8.4, -1.8] |
| NY | WEAK | 653 | 37.2 | 54.1 | 8.7 | 46.3 | 56.3 | -9.1 | -19.1 | 33.5 | -12.8 | 29.9 | -3.83 | +1.51 | -3.00 | -5.3 [-6.6, -4.0] |
| LONDON | STRONG | 12 (NOT READABLE) | 33.3 | 50.0 | 16.7 | 44.5 | 53.4 | -11.2 | -20.1 | 38.8 | -5.7 | 33.7 | -1.93 | +3.16 | -3.00 | -1.8 [-9.7, +10.8] |
| LONDON | MEDIUM | 64 (NOT READABLE) | 45.3 | 43.8 | 10.9 | 44.6 | 53.1 | +0.7 | -7.7 | 43.4 | -1.2 | 35.3 | -0.42 | +2.52 | -3.00 | -0.9 [-4.8, +4.7] |
| LONDON | WEAK | 158 | 34.8 | 57.6 | 7.6 | 41.0 | 49.7 | -6.2 | -14.9 | 33.6 | -7.4 | 34.5 | -2.56 | +1.50 | -3.00 | -4.1 [-8.6, -0.1] |
| ASIA | STRONG | 21 (NOT READABLE) | 42.9 | 42.9 | 14.3 | 51.2 | 61.2 | -8.4 | -18.4 | 35.5 | -15.7 | 30.0 | -4.70 | +3.83 | -3.00 | -3.9 [-14.0, +3.4] |
| ASIA | MEDIUM | 142 | 52.1 | 40.1 | 7.7 | 53.3 | 63.3 | -1.2 | -11.2 | 49.3 | -4.0 | 29.9 | -1.19 | +1.72 | -3.00 | -2.5 [-6.3, +0.5] |
| ASIA | WEAK | 304 | 45.1 | 51.0 | 3.9 | 47.3 | 56.7 | -2.2 | -11.7 | 42.9 | -4.4 | 31.7 | -1.39 | +1.16 | -3.00 | -3.2 [-7.7, +1.2] |

## 9. POC-fix sensitivity, P0: the key comparisons with the POC-fix slice removed

| Session | Slice rows: STRONG / MEDIUM / WEAK | Comparison | Metric | With slice (FULL d) | Without slice FULL d [95 % CI] | Label without slice |
|---|---|---|---|---|---|---|
| NY | 5 / 24 / 115 | STRONG - WEAK | mean target distance, bps | +1.2 | +1.1 [-0.5, +2.8] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | gross breakeven rate, pp | +2.4 | +2.4 [+1.3, +3.4] | CONFIRMED (d > 0) |
| NY | 5 / 24 / 115 | STRONG - WEAK | success rate, pp | +5.8 | +5.3 [-0.6, +10.5] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | gross edge, pp | +3.4 | +2.9 [-3.0, +8.2] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | net EV per trade, bps | -0.0 | -0.2 [-2.0, +1.6] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | MEDIUM - WEAK | mean target distance, bps | +0.0 | -0.0 [-0.5, +0.5] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | MEDIUM - WEAK | gross breakeven rate, pp | +1.0 | +1.0 [+0.5, +1.6] | CONFIRMED (d > 0) |
| NY | 5 / 24 / 115 | MEDIUM - WEAK | success rate, pp | -0.2 | -0.3 [-4.4, +3.7] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | MEDIUM - WEAK | gross edge, pp | -1.2 | -1.4 [-5.4, +2.8] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | MEDIUM - WEAK | net EV per trade, bps | -1.0 | -1.0 [-2.2, +0.2] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | STRONG - WEAK | mean target distance, bps | -0.8 | -0.8 [-3.5, +1.8] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | STRONG - WEAK | gross breakeven rate, pp | +1.2 | +1.2 [+0.2, +2.2] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | STRONG - WEAK | success rate, pp | -1.1 | -1.2 [-14.3, +12.0] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | STRONG - WEAK | gross edge, pp | -2.3 | -2.4 [-15.2, +10.4] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | STRONG - WEAK | net EV per trade, bps | -0.4 | -0.4 [-4.7, +4.6] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | MEDIUM - WEAK | mean target distance, bps | -0.7 | -0.8 [-3.4, +1.6] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| LONDON | 0 / 7 / 12 | MEDIUM - WEAK | gross breakeven rate, pp | +1.5 | +1.5 [+0.6, +2.6] | CONFIRMED (d > 0) |
| LONDON | 0 / 7 / 12 | MEDIUM - WEAK | success rate, pp | +3.9 | +4.0 [-2.4, +10.0] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | MEDIUM - WEAK | gross edge, pp | +2.4 | +2.5 [-3.9, +8.5] | NO DIFFERENCE SHOWN |
| LONDON | 0 / 7 / 12 | MEDIUM - WEAK | net EV per trade, bps | +0.4 | +0.5 [-1.9, +2.8] | NO DIFFERENCE SHOWN |
| ASIA | 0 / 5 / 8 | STRONG - WEAK | mean target distance, bps | -1.3 | -1.4 [-3.5, +0.6] | NOT READABLE |
| ASIA | 0 / 5 / 8 | STRONG - WEAK | gross breakeven rate, pp | +7.8 | +7.8 [+6.3, +9.1] | NOT READABLE |
| ASIA | 0 / 5 / 8 | STRONG - WEAK | success rate, pp | +17.9 | +17.8 [+4.1, +30.3] | NOT READABLE |
| ASIA | 0 / 5 / 8 | STRONG - WEAK | gross edge, pp | +10.1 | +10.0 [-3.5, +22.7] | NOT READABLE |
| ASIA | 0 / 5 / 8 | STRONG - WEAK | net EV per trade, bps | +4.7 | +4.7 [-0.2, +9.2] | NOT READABLE |
| ASIA | 0 / 5 / 8 | MEDIUM - WEAK | mean target distance, bps | -1.7 | -1.8 [-2.7, -0.9] | CONFIRMED (d < 0) |
| ASIA | 0 / 5 / 8 | MEDIUM - WEAK | gross breakeven rate, pp | +5.3 | +5.3 [+4.1, +6.5] | CONFIRMED (d > 0) |
| ASIA | 0 / 5 / 8 | MEDIUM - WEAK | success rate, pp | +1.6 | +1.2 [-6.3, +8.1] | NO DIFFERENCE SHOWN |
| ASIA | 0 / 5 / 8 | MEDIUM - WEAK | gross edge, pp | -3.7 | -4.2 [-11.2, +2.8] | NO DIFFERENCE SHOWN |
| ASIA | 0 / 5 / 8 | MEDIUM - WEAK | net EV per trade, bps | -1.5 | -1.6 [-3.7, +0.4] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |

## 10. Carried 24 h (secondary), P0: success rate, gross breakeven rate and net EV by tier

| Session | Tier | n | Success % | Timeout % | Gross BE % | Gross edge pp | Net EV bps [95 % CI] |
|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 46.5 | 0.0 | 48.1 | -1.6 | -3.3 [-5.4, -1.4] |
| NY | MEDIUM | 1552 | 43.1 | 0.0 | 46.7 | -3.6 | -4.2 [-5.6, -3.0] |
| NY | WEAK | 3523 | 43.9 | 0.0 | 45.7 | -1.8 | -3.1 [-4.1, -1.8] |
| LONDON | STRONG | 131 | 44.3 | 0.0 | 45.3 | -1.0 | -1.8 [-6.2, +3.1] |
| LONDON | MEDIUM | 485 | 50.7 | 0.0 | 45.6 | +5.1 | -0.6 [-2.7, +1.5] |
| LONDON | WEAK | 1049 | 48.9 | 0.0 | 44.1 | +4.8 | -0.9 [-3.8, +1.6] |
| ASIA | STRONG | 65 | 69.2 | 0.0 | 55.3 | +13.9 | +3.3 [-2.5, +9.0] |
| ASIA | MEDIUM | 420 | 50.5 | 0.0 | 52.9 | -2.4 | -3.9 [-5.9, -2.0] |
| ASIA | WEAK | 1243 | 50.8 | 0.0 | 47.5 | +3.3 | -2.3 [-4.2, -0.5] |

| Session | Comparison | Metric | FULL d [95 % CI] (n) | H1 d [95 % CI] | H2 d [95 % CI] | Label |
|---|---|---|---|---|---|---|
| NY | STRONG - WEAK | success rate, carried 24 h, pp | +2.6 [-3.2, +7.6] (342/3523) | +0.3 [-8.3, +7.9] | +4.0 [-3.7, +10.4] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | gross edge, carried 24 h, pp | +0.2 [-5.4, +5.4] (342/3523) | -2.1 [-10.6, +5.6] | +1.8 [-6.0, +8.9] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | net EV per trade, carried 24 h, bps | -0.3 [-2.0, +1.4] (342/3523) | -0.4 [-2.4, +1.5] | -0.1 [-3.0, +2.4] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | success rate, carried 24 h, pp | -0.8 [-5.2, +3.5] (1552/3523) | -3.4 [-9.9, +3.5] | +0.9 [-4.9, +6.3] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | gross edge, carried 24 h, pp | -1.8 [-6.1, +2.3] (1552/3523) | -4.7 [-10.8, +1.9] | +0.1 [-5.5, +5.4] | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK | net EV per trade, carried 24 h, bps | -1.1 [-2.4, +0.2] (1552/3523) | -1.4 [-3.0, +0.5] | -1.0 [-2.9, +0.9] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | success rate, carried 24 h, pp | -4.6 [-16.7, +8.1] (131/1049) | -3.4 [-21.2, +14.2] | -4.8 [-22.1, +14.8] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | gross edge, carried 24 h, pp | -5.8 [-18.3, +6.8] (131/1049) | -5.2 [-22.8, +12.2] | -5.8 [-22.4, +13.4] | NO DIFFERENCE SHOWN |
| LONDON | STRONG - WEAK | net EV per trade, carried 24 h, bps | -0.9 [-5.9, +5.4] (131/1049) | -2.4 [-7.5, +2.7] | +1.9 [-7.3, +14.6] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | success rate, carried 24 h, pp | +1.8 [-5.1, +8.7] (485/1049) | +8.7 [-0.5, +17.3] | -3.6 [-13.3, +5.9] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | gross edge, carried 24 h, pp | +0.3 [-6.5, +7.0] (485/1049) | +7.3 [-1.5, +15.9] | -5.2 [-14.9, +4.2] | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | net EV per trade, carried 24 h, bps | +0.4 [-2.8, +3.7] (485/1049) | +2.1 [-0.4, +4.9] | -1.1 [-5.9, +4.4] | NO DIFFERENCE SHOWN |
| ASIA | STRONG - WEAK | success rate, carried 24 h, pp | +18.4 [+3.4, +31.7] (65/1243) | +9.9 [-19.7, +34.9] | +21.7 [+4.6, +37.1] | NOT READABLE |
| ASIA | STRONG - WEAK | gross edge, carried 24 h, pp | +10.6 [-4.2, +23.7] (65/1243) | +0.3 [-27.9, +23.7] | +14.5 [-2.8, +29.8] | NOT READABLE |
| ASIA | STRONG - WEAK | net EV per trade, carried 24 h, bps | +5.6 [-0.7, +11.6] (65/1243) | +0.5 [-7.7, +6.9] | +7.6 [-0.6, +15.0] | NOT READABLE |
| ASIA | MEDIUM - WEAK | success rate, carried 24 h, pp | -0.4 [-8.0, +6.9] (420/1243) | -7.8 [-19.6, +2.2] | +5.2 [-5.1, +15.1] | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | gross edge, carried 24 h, pp | -5.7 [-13.4, +1.6] (420/1243) | -12.2 [-24.8, -1.9] | -0.6 [-10.4, +9.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | MEDIUM - WEAK | net EV per trade, carried 24 h, bps | -1.5 [-4.0, +1.0] (420/1243) | -3.9 [-7.3, -1.1] | +0.2 [-3.2, +4.0] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |

## 11. What the Kelly EST mode assumes against what the book shows, P0, main window (descriptive)

- Kelly EST (Core/ScoringEngine_Kelly.vb): p from the confidence tier, b = atr_target_multiplier / atr_stop_multiplier = 1.094 for every tier. f* = (b p - (1 - p)) / b.

| Session | Tier | Kelly p | Kelly b | Kelly f* | Measured success rate | Measured payoff sum T / sum S | f* at measured p and b |
|---|---|---|---|---|---|---|---|
| NY | STRONG | 0.65 | 1.094 | +0.330 | 0.459 | 1.081 | -0.041 |
| NY | MEDIUM | 0.55 | 1.094 | +0.139 | 0.399 | 1.142 | -0.128 |
| NY | WEAK | 0.45 | 1.094 | -0.053 | 0.401 | 1.188 | -0.103 |
| LONDON | STRONG | 0.65 | 1.094 | +0.330 | 0.412 | 1.207 | -0.075 |
| LONDON | MEDIUM | 0.55 | 1.094 | +0.139 | 0.462 | 1.193 | +0.011 |
| LONDON | WEAK | 0.45 | 1.094 | -0.053 | 0.423 | 1.266 | -0.032 |
| ASIA | STRONG | 0.65 | 1.094 | +0.330 | 0.646 | 0.808 | +0.208 |
| ASIA | MEDIUM | 0.55 | 1.094 | +0.139 | 0.483 | 0.892 | -0.096 |
| ASIA | WEAK | 0.45 | 1.094 | -0.053 | 0.467 | 1.103 | -0.015 |

## 12. Label census (sections 2.1, 5, 6, 6.1, 9, 10; section 9 labels are not counted)

| Label | Count |
|---|---|
| NO DIFFERENCE SHOWN | 121 |
| NOT READABLE | 58 |
| CONFIRMED | 29 |
| H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT | 15 |
| DISCOVERY ONLY | 7 |
| H1 FINDING, H2 CONTRADICTS OR NOT READABLE | 7 |

- Labelled comparisons: 237; readable: 179. At a 5 % false-positive rate per half, the chance that a null comparison reads CONFIRMED is about 0.05 x 0.05 / 2 = 0.00125, so about 0.22 false CONFIRMED labels are expected.

