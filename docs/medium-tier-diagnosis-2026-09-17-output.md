# medium_tier_diagnosis.py output (MEDIUM-tier diagnosis, session 2)

- Run at (UTC): 2026-09-17 12:30:06
- Brief: docs/medium-tier-diagnosis-brief-2026-09-16.md section 3. Rules: this script's header.
- Inputs: `backtest_data\swing-fallback-read\rescore-attribution.csv` (MD5 816853b8864456e97a5485fc8b3a2273), `backtest_data\swing-fallback-read\diagnosis-rows.csv` (MD5 e7e93f2fa66e60a3119ce09947b2e063).
- Bootstrap: 10000 resamples of whole UTC trading days, seed 20260917 + call index; readable n >= 100.
- Verdict percentages (tracked settings.json version 68): STRONG 0.70, MEDIUM 0.53, WEAK 0.35.

## 0. Self-checks

| Check | Result |
|---|---|
| Export rows | 8810 |
| Export rows without an attribution row (must be 0) | 0 |
| Attribution rows repaired (unquoted comma in the Class field) | 56 |
| Joined population rows | 8810 |
| Attribution InPopulation flag = 1 on joined rows | 8810 |
| Logged tier = tier of the logged effective score against Ceiling(MaxScore x pct) (mismatches, must be 0) | 0 |
| Rows whose re-score matches all six fields | 8749 |
| Rows with a verified VPFR profile | 8508 |
| Rows with MaxScore outside {20, 19, 15} | 0 |
| Rows with a missing main-window or carried outcome value | 0 |
| Matching rows where the trade side's breakdown points do not sum to the logged raw score (must be 0) | 0 |
| Era settings files read / magnitude groups that differ between eras (must be none) | 18 / none |

| Session | Rows | STRONG | MEDIUM | WEAK | H1 rows | H2 rows | Trading days |
|---|---|---|---|---|---|---|---|
| NY | 5417 | 342 | 1552 | 3523 | 2160 | 3257 | 49 |
| LONDON | 1665 | 131 | 485 | 1049 | 754 | 911 | 47 |
| ASIA | 1728 | 65 | 420 | 1243 | 661 | 1067 | 42 |

## 1. Baseline: tier outcomes and the tier gap per session

### 1.1 main window

| Session | Tier | n | Success % | Net EV [95 % CI] |
|---|---|---|---|---|
| NY | STRONG | 342 | 45.9 | -3.2 [-5.3, -1.3] |
| NY | MEDIUM | 1552 | 39.9 | -4.2 [-5.3, -3.1] |
| NY | WEAK | 3523 | 40.1 | -3.2 [-4.0, -2.3] |
| LONDON | STRONG | 131 | 41.2 | -1.6 [-5.8, +3.2] |
| LONDON | MEDIUM | 485 | 46.2 | -0.8 [-3.0, +1.4] |
| LONDON | WEAK | 1049 | 42.3 | -1.2 [-2.8, +0.3] |
| ASIA | STRONG | 65 (NOT READABLE) | 64.6 | +2.5 [-1.9, +6.7] |
| ASIA | MEDIUM | 420 | 48.3 | -3.6 [-5.5, -1.9] |
| ASIA | WEAK | 1243 | 46.7 | -2.2 [-3.9, -0.7] |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | MEDIUM - WEAK | -1.0 [-2.2, +0.2] (1552/3523) | -1.2 [-2.7, +0.5] (630/1393) | -0.8 [-2.5, +0.8] (922/2130) | NO DIFFERENCE SHOWN |
| NY | MEDIUM - STRONG | -1.0 [-2.7, +0.8] (1552/342) | -0.8 [-3.1, +1.5] (630/137) | -1.1 [-3.5, +1.6] (922/205) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | +0.4 [-2.0, +2.7] (485/1049) | +2.3 [-0.2, +4.9] (212/467) | -1.0 [-4.7, +2.5] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - STRONG | +0.8 [-4.3, +5.4] (485/131) | +3.8 [-0.9, +8.5] (212/75) | -2.8 [-12.9, +5.6] (273/56) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | -1.5 [-3.5, +0.6] (420/1243) | -3.4 [-6.0, -1.2] (182/461) | +0.0 [-3.1, +3.0] (238/782) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | MEDIUM - STRONG | -6.2 [-10.0, -2.0] (420/65) | -4.5 [-10.0, +0.4] (182/18) | -6.1 [-10.6, -0.9] (238/47) | NOT READABLE |

### 1.2 carried 24 h

| Session | Tier | n | Success % | Net EV [95 % CI] |
|---|---|---|---|---|
| NY | STRONG | 342 | 46.5 | -3.3 [-5.4, -1.4] |
| NY | MEDIUM | 1552 | 43.1 | -4.2 [-5.5, -3.0] |
| NY | WEAK | 3523 | 43.9 | -3.1 [-4.1, -1.8] |
| LONDON | STRONG | 131 | 44.3 | -1.8 [-6.2, +3.2] |
| LONDON | MEDIUM | 485 | 50.7 | -0.6 [-2.7, +1.5] |
| LONDON | WEAK | 1049 | 48.9 | -0.9 [-3.7, +1.7] |
| ASIA | STRONG | 65 (NOT READABLE) | 69.2 | +3.3 [-2.7, +8.9] |
| ASIA | MEDIUM | 420 | 50.5 | -3.9 [-5.9, -2.0] |
| ASIA | WEAK | 1243 | 50.8 | -2.3 [-4.2, -0.5] |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | MEDIUM - WEAK | -1.1 [-2.5, +0.2] (1552/3523) | -1.4 [-3.0, +0.5] (630/1393) | -1.0 [-2.9, +0.9] (922/2130) | NO DIFFERENCE SHOWN |
| NY | MEDIUM - STRONG | -0.9 [-2.6, +1.0] (1552/342) | -0.9 [-3.4, +1.4] (630/137) | -0.8 [-3.2, +1.8] (922/205) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK | +0.4 [-2.7, +3.6] (485/1049) | +2.1 [-0.4, +5.0] (212/467) | -1.1 [-6.0, +4.4] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - STRONG | +1.2 [-3.8, +5.7] (485/131) | +4.6 [-0.6, +10.1] (212/75) | -3.0 [-13.1, +4.8] (273/56) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK | -1.5 [-4.0, +1.0] (420/1243) | -3.9 [-7.3, -1.2] (182/461) | +0.2 [-3.2, +3.8] (238/782) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | MEDIUM - STRONG | -7.1 [-12.3, -1.7] (420/65) | -4.4 [-10.7, +1.8] (182/18) | -7.4 [-13.4, -0.7] (238/47) | NOT READABLE |

## 2. D-1: is the score monotone in quality?

### D-1 effective score share of regime max, main window

| Session | Bin | n | Success % | Net EV [95 % CI] | H1 EV | H2 EV |
|---|---|---|---|---|---|---|
| NY | < 0.45 | 1879 | 39.4 | -3.3 [-4.2, -2.2] | -3.2 | -3.3 |
| NY | 0.45-0.53 | 1644 | 40.9 | -3.1 [-4.2, -2.1] | -2.5 | -3.6 |
| NY | 0.53-0.60 | 650 | 41.5 | -3.4 [-4.8, -2.0] | -4.5 | -2.7 |
| NY | 0.60-0.70 | 902 | 38.7 | -4.8 [-6.1, -3.5] | -3.8 | -5.4 |
| NY | >= 0.70 | 342 | 45.9 | -3.2 [-5.3, -1.4] | -3.3 | -3.2 |
| LONDON | < 0.45 | 566 | 41.0 | -1.7 [-3.7, +0.2] | -3.0 | -0.7 |
| LONDON | 0.45-0.53 | 483 | 43.9 | -0.7 [-2.9, +1.4] | -1.6 | +0.2 |
| LONDON | 0.53-0.60 | 223 | 44.4 | -1.4 [-4.4, +1.2] | -0.6 | -2.2 |
| LONDON | 0.60-0.70 | 262 | 47.7 | -0.3 [-2.9, +2.4] | +0.4 | -0.7 |
| LONDON | >= 0.70 | 131 | 41.2 | -1.6 [-5.8, +3.1] | -3.8 | +1.4 |
| ASIA | < 0.45 | 706 | 44.3 | -2.5 [-4.3, -0.8] | -2.4 | -2.5 |
| ASIA | 0.45-0.53 | 537 | 49.9 | -1.8 [-3.7, +0.0] | -1.5 | -1.9 |
| ASIA | 0.53-0.60 | 218 | 48.2 | -3.3 [-6.1, -0.6] | -5.2 | -1.8 |
| ASIA | 0.60-0.70 | 202 | 48.5 | -4.0 [-5.9, -2.2] | -5.8 | -2.7 |
| ASIA | >= 0.70 | 65 (NOT READABLE) | 64.6 | +2.5 [-1.9, +6.7] | -1.0 | +3.9 |

| Session | Statistic | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | slope of net EV on effective score share of regime max (slope per 0.1) | -0.3 [-0.8, +0.2] (5417) | -0.3 [-0.8, +0.2] (2160) | -0.3 [-1.1, +0.5] (3257) | NO DIFFERENCE SHOWN |
| LONDON | slope of net EV on effective score share of regime max (slope per 0.1) | +0.1 [-0.9, +1.2] (1665) | +0.2 [-1.0, +1.5] (754) | -0.0 [-1.5, +1.8] (911) | NO DIFFERENCE SHOWN |
| ASIA | slope of net EV on effective score share of regime max (slope per 0.1) | +0.2 [-0.9, +1.2] (1728) | -0.9 [-2.4, +0.4] (661) | +0.9 [-0.5, +2.0] (1067) | NO DIFFERENCE SHOWN |

### D-1 effective margin, dominant minus opposite, main window

| Session | Bin | n | Success % | Net EV [95 % CI] | H1 EV | H2 EV |
|---|---|---|---|---|---|---|
| NY | 1-2 | 102 | 44.1 | -1.7 [-5.4, +1.8] | -1.7 | -1.6 |
| NY | 3-4 | 508 | 41.3 | -1.8 [-3.7, +0.2] | -2.3 | -1.6 |
| NY | 5-6 | 1133 | 39.7 | -3.5 [-4.7, -2.4] | -2.7 | -4.1 |
| NY | 7-9 | 2030 | 39.6 | -3.6 [-4.6, -2.7] | -3.3 | -3.9 |
| NY | >= 10 | 1644 | 41.4 | -3.9 [-5.2, -2.8] | -4.0 | -3.9 |
| LONDON | 1-2 | 20 (NOT READABLE) | 35.0 | -2.5 [-10.1, +6.4] | -4.3 | -0.6 |
| LONDON | 3-4 | 138 | 39.9 | -3.3 [-6.7, +0.2] | -5.5 | -1.3 |
| LONDON | 5-6 | 323 | 41.5 | -1.6 [-4.1, +0.7] | -3.2 | -0.3 |
| LONDON | 7-9 | 584 | 43.2 | -0.6 [-2.2, +1.0] | -0.8 | -0.4 |
| LONDON | >= 10 | 600 | 45.7 | -0.9 [-2.8, +1.2] | -1.1 | -0.6 |
| ASIA | 1-2 | 20 (NOT READABLE) | 45.0 | -0.1 [-7.4, +7.3] | +5.3 | -2.4 |
| ASIA | 3-4 | 166 | 37.3 | -5.6 [-9.1, -2.4] | -4.3 | -6.4 |
| ASIA | 5-6 | 422 | 46.7 | -1.8 [-3.7, +0.1] | -2.8 | -1.2 |
| ASIA | 7-9 | 677 | 48.3 | -2.1 [-4.0, -0.4] | -2.4 | -2.0 |
| ASIA | >= 10 | 443 | 52.1 | -2.2 [-4.3, -0.2] | -3.7 | -1.0 |

| Session | Statistic | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | slope of net EV on effective margin, dominant minus opposite (slope per point) | -0.2 [-0.3, +0.0] (5417) | -0.2 [-0.3, +0.1] (2160) | -0.2 [-0.4, +0.1] (3257) | NO DIFFERENCE SHOWN |
| LONDON | slope of net EV on effective margin, dominant minus opposite (slope per point) | +0.1 [-0.3, +0.5] (1665) | +0.2 [-0.2, +0.7] (754) | -0.0 [-0.6, +0.6] (911) | NO DIFFERENCE SHOWN |
| ASIA | slope of net EV on effective margin, dominant minus opposite (slope per point) | +0.1 [-0.2, +0.5] (1728) | -0.1 [-0.6, +0.3] (661) | +0.3 [-0.2, +0.8] (1067) | NO DIFFERENCE SHOWN |

### D-1 effective score share of regime max, carried 24 h

| Session | Bin | n | Success % | Net EV [95 % CI] | H1 EV | H2 EV |
|---|---|---|---|---|---|---|
| NY | < 0.45 | 1879 | 43.7 | -3.1 [-4.3, -1.7] | -3.4 | -2.9 |
| NY | 0.45-0.53 | 1644 | 44.2 | -3.0 [-4.3, -1.8] | -2.6 | -3.3 |
| NY | 0.53-0.60 | 650 | 45.1 | -3.3 [-4.9, -1.8] | -4.6 | -2.5 |
| NY | 0.60-0.70 | 902 | 41.7 | -4.8 [-6.3, -3.4] | -4.2 | -5.2 |
| NY | >= 0.70 | 342 | 46.5 | -3.3 [-5.4, -1.4] | -3.4 | -3.2 |
| LONDON | < 0.45 | 566 | 47.3 | -1.6 [-4.7, +1.3] | -2.8 | -0.7 |
| LONDON | 0.45-0.53 | 483 | 50.7 | -0.1 [-3.2, +2.9] | -1.4 | +1.0 |
| LONDON | 0.53-0.60 | 223 | 50.7 | -0.8 [-3.6, +1.9] | -0.6 | -0.9 |
| LONDON | 0.60-0.70 | 262 | 50.8 | -0.4 [-3.3, +2.4] | +0.6 | -1.1 |
| LONDON | >= 0.70 | 131 | 44.3 | -1.8 [-6.2, +3.2] | -4.6 | +2.0 |
| ASIA | < 0.45 | 706 | 47.9 | -2.8 [-4.6, -0.9] | -2.4 | -3.0 |
| ASIA | 0.45-0.53 | 537 | 54.7 | -1.8 [-4.1, +0.4] | -1.7 | -1.8 |
| ASIA | 0.53-0.60 | 218 | 50.0 | -3.4 [-6.4, -0.6] | -5.7 | -1.7 |
| ASIA | 0.60-0.70 | 202 | 51.0 | -4.4 [-6.5, -2.4] | -6.4 | -2.8 |
| ASIA | >= 0.70 | 65 (NOT READABLE) | 69.2 | +3.3 [-2.5, +8.8] | -1.6 | +5.2 |

| Session | Statistic | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | slope of net EV on effective score share of regime max (slope per 0.1) | -0.4 [-0.9, +0.2] (5417) | -0.4 [-0.8, +0.2] (2160) | -0.4 [-1.2, +0.4] (3257) | NO DIFFERENCE SHOWN |
| LONDON | slope of net EV on effective score share of regime max (slope per 0.1) | -0.0 [-1.3, +1.5] (1665) | +0.0 [-1.2, +1.3] (754) | +0.0 [-2.0, +2.7] (911) | NO DIFFERENCE SHOWN |
| ASIA | slope of net EV on effective score share of regime max (slope per 0.1) | +0.3 [-1.0, +1.6] (1728) | -1.2 [-2.9, +0.2] (661) | +1.2 [-0.4, +2.7] (1067) | NO DIFFERENCE SHOWN |

### D-1 effective margin, dominant minus opposite, carried 24 h

| Session | Bin | n | Success % | Net EV [95 % CI] | H1 EV | H2 EV |
|---|---|---|---|---|---|---|
| NY | 1-2 | 102 | 47.1 | -2.1 [-5.7, +1.3] | -2.3 | -2.0 |
| NY | 3-4 | 508 | 45.9 | -1.6 [-3.6, +0.5] | -2.1 | -1.4 |
| NY | 5-6 | 1133 | 43.5 | -3.5 [-4.7, -2.2] | -3.0 | -3.7 |
| NY | 7-9 | 2030 | 43.0 | -3.6 [-4.9, -2.3] | -3.6 | -3.6 |
| NY | >= 10 | 1644 | 44.3 | -3.7 [-5.2, -2.4] | -4.0 | -3.5 |
| LONDON | 1-2 | 20 (NOT READABLE) | 40.0 | -2.0 [-10.0, +6.8] | -3.4 | -0.6 |
| LONDON | 3-4 | 138 | 43.5 | -3.4 [-6.8, +0.2] | -5.0 | -2.0 |
| LONDON | 5-6 | 323 | 50.2 | -0.8 [-4.9, +3.1] | -2.6 | +0.7 |
| LONDON | 7-9 | 584 | 49.8 | -0.3 [-3.1, +2.3] | -0.8 | +0.1 |
| LONDON | >= 10 | 600 | 49.3 | -0.9 [-3.1, +1.4] | -1.5 | -0.4 |
| ASIA | 1-2 | 20 (NOT READABLE) | 50.0 | +0.4 [-7.4, +8.0] | +8.5 | -3.1 |
| ASIA | 3-4 | 166 | 38.6 | -6.6 [-10.7, -3.0] | -4.3 | -7.9 |
| ASIA | 5-6 | 422 | 50.7 | -2.2 [-4.1, -0.2] | -2.9 | -1.8 |
| ASIA | 7-9 | 677 | 52.3 | -2.0 [-4.1, -0.1] | -2.8 | -1.5 |
| ASIA | >= 10 | 443 | 55.8 | -2.1 [-4.5, +0.0] | -3.9 | -0.7 |

| Session | Statistic | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | slope of net EV on effective margin, dominant minus opposite (slope per point) | -0.1 [-0.3, +0.0] (5417) | -0.1 [-0.3, +0.1] (2160) | -0.1 [-0.4, +0.1] (3257) | NO DIFFERENCE SHOWN |
| LONDON | slope of net EV on effective margin, dominant minus opposite (slope per point) | +0.0 [-0.5, +0.6] (1665) | +0.1 [-0.4, +0.6] (754) | -0.0 [-0.9, +0.9] (911) | NO DIFFERENCE SHOWN |
| ASIA | slope of net EV on effective margin, dominant minus opposite (slope per point) | +0.2 [-0.2, +0.7] (1728) | -0.2 [-0.8, +0.2] (661) | +0.5 [-0.1, +1.2] (1067) | NO DIFFERENCE SHOWN |

## 3. D-2: net EV by exact effective score

| Session | MaxScore (regime) | Effective score | Tier | n | Success % | Net EV [95 % CI] |
|---|---|---|---|---|---|---|
| NY | 20 (TRENDING) | 7 | WEAK | 710 | 39.9 | -3.2 [-4.8, -1.5] |
| NY | 20 (TRENDING) | 8 | WEAK | 771 | 38.7 | -3.4 [-4.9, -2.0] |
| NY | 20 (TRENDING) | 9 | WEAK | 730 | 41.2 | -2.9 [-4.6, -1.2] |
| NY | 20 (TRENDING) | 10 | WEAK | 615 | 38.4 | -4.1 [-5.3, -2.8] |
| NY | 20 (TRENDING) | 11 | MEDIUM | 490 | 40.6 | -3.7 [-5.2, -2.2] |
| NY | 20 (TRENDING) | 12 | MEDIUM | 426 | 36.9 | -4.8 [-6.3, -3.3] |
| NY | 20 (TRENDING) | 13 | MEDIUM | 297 | 41.4 | -4.5 [-6.6, -2.5] |
| NY | 20 (TRENDING) | 14 | STRONG | 154 | 46.1 | -4.1 [-6.3, -2.1] |
| NY | 20 (TRENDING) | 15 | STRONG | 110 | 50.0 | -1.1 [-4.8, +2.2] |
| NY | 20 (TRENDING) | 16 | STRONG | 33 (NOT READABLE) | 45.5 | -2.4 [-10.3, +4.3] |
| NY | 20 (TRENDING) | 17 | STRONG | 14 (NOT READABLE) | 35.7 | -4.1 [-13.5, +5.0] |
| NY | 20 (TRENDING) | 18 | STRONG | 5 (NOT READABLE) | 20.0 | -16.6 [-29.3, +6.2] |
| NY | 20 (TRENDING) | 19 | STRONG | 1 (NOT READABLE) | 0.0 | -20.8 [-20.8, -20.8] |
| NY | 19 (RANGE_BOUND) | 7 | WEAK | 119 | 40.3 | -2.3 [-4.3, -0.7] |
| NY | 19 (RANGE_BOUND) | 8 | WEAK | 97 (NOT READABLE) | 32.0 | -5.0 [-8.1, -2.4] |
| NY | 19 (RANGE_BOUND) | 9 | WEAK | 91 (NOT READABLE) | 48.4 | -0.3 [-3.3, +2.0] |
| NY | 19 (RANGE_BOUND) | 10 | WEAK | 63 (NOT READABLE) | 44.4 | -1.9 [-5.2, +2.4] |
| NY | 19 (RANGE_BOUND) | 11 | MEDIUM | 57 (NOT READABLE) | 49.1 | +0.0 [-4.1, +3.7] |
| NY | 19 (RANGE_BOUND) | 12 | MEDIUM | 47 (NOT READABLE) | 46.8 | -3.4 [-7.1, +1.7] |
| NY | 19 (RANGE_BOUND) | 13 | MEDIUM | 27 (NOT READABLE) | 37.0 | -5.5 [-9.2, -1.4] |
| NY | 19 (RANGE_BOUND) | 14 | STRONG | 6 (NOT READABLE) | 33.3 | -7.6 [-17.9, +6.8] |
| NY | 19 (RANGE_BOUND) | 15 | STRONG | 4 (NOT READABLE) | 75.0 | +6.9 [+5.1, +7.6] |
| NY | 19 (RANGE_BOUND) | 16 | STRONG | 1 (NOT READABLE) | 0.0 | -10.7 [-10.7, -10.7] |
| NY | 15 (TRANSITIONAL) | 6 | WEAK | 182 | 44.5 | -2.4 [-4.6, -0.0] |
| NY | 15 (TRANSITIONAL) | 7 | WEAK | 145 | 43.4 | -3.0 [-5.4, -0.4] |
| NY | 15 (TRANSITIONAL) | 8 | MEDIUM | 103 | 41.7 | -4.0 [-6.8, -1.3] |
| NY | 15 (TRANSITIONAL) | 9 | MEDIUM | 67 (NOT READABLE) | 34.3 | -5.3 [-8.5, -1.8] |
| NY | 15 (TRANSITIONAL) | 10 | MEDIUM | 38 (NOT READABLE) | 36.8 | -6.3 [-11.3, -1.2] |
| NY | 15 (TRANSITIONAL) | 11 | STRONG | 9 (NOT READABLE) | 33.3 | -3.3 [-9.8, +3.7] |
| NY | 15 (TRANSITIONAL) | 12 | STRONG | 4 (NOT READABLE) | 25.0 | -15.3 [-27.3, -2.9] |
| NY | 15 (TRANSITIONAL) | 13 | STRONG | 1 (NOT READABLE) | 100.0 | +7.9 [+7.9, +7.9] |
| LONDON | 20 (TRENDING) | 7 | WEAK | 208 | 41.8 | -2.1 [-4.9, +0.4] |
| LONDON | 20 (TRENDING) | 8 | WEAK | 258 | 38.4 | -1.6 [-4.4, +1.2] |
| LONDON | 20 (TRENDING) | 9 | WEAK | 202 | 46.5 | +0.8 [-2.4, +4.0] |
| LONDON | 20 (TRENDING) | 10 | WEAK | 208 | 38.5 | -2.3 [-5.2, +0.7] |
| LONDON | 20 (TRENDING) | 11 | MEDIUM | 190 | 45.3 | -1.1 [-4.4, +1.7] |
| LONDON | 20 (TRENDING) | 12 | MEDIUM | 128 | 50.0 | +1.4 [-2.2, +5.0] |
| LONDON | 20 (TRENDING) | 13 | MEDIUM | 95 (NOT READABLE) | 47.4 | -0.6 [-4.4, +2.9] |
| LONDON | 20 (TRENDING) | 14 | STRONG | 50 (NOT READABLE) | 38.0 | +0.8 [-5.5, +9.1] |
| LONDON | 20 (TRENDING) | 15 | STRONG | 43 (NOT READABLE) | 41.9 | -3.7 [-9.3, +2.2] |
| LONDON | 20 (TRENDING) | 16 | STRONG | 18 (NOT READABLE) | 50.0 | +0.7 [-8.0, +9.2] |
| LONDON | 20 (TRENDING) | 17 | STRONG | 7 (NOT READABLE) | 42.9 | -4.9 [-12.4, -2.0] |
| LONDON | 20 (TRENDING) | 18 | STRONG | 2 (NOT READABLE) | 50.0 | -1.3 [-23.3, +20.7] |
| LONDON | 20 (TRENDING) | 19 | STRONG | 1 (NOT READABLE) | 0.0 | -17.1 [-17.1, -17.1] |
| LONDON | 19 (RANGE_BOUND) | 7 | WEAK | 33 (NOT READABLE) | 42.4 | -0.8 [-5.3, +4.6] |
| LONDON | 19 (RANGE_BOUND) | 8 | WEAK | 15 (NOT READABLE) | 40.0 | -1.7 [-8.2, +4.8] |
| LONDON | 19 (RANGE_BOUND) | 9 | WEAK | 15 (NOT READABLE) | 46.7 | -1.1 [-8.1, +8.4] |
| LONDON | 19 (RANGE_BOUND) | 10 | WEAK | 14 (NOT READABLE) | 64.3 | +5.5 [-3.1, +11.8] |
| LONDON | 19 (RANGE_BOUND) | 11 | MEDIUM | 10 (NOT READABLE) | 30.0 | -5.6 [-12.0, +3.3] |
| LONDON | 19 (RANGE_BOUND) | 12 | MEDIUM | 9 (NOT READABLE) | 22.2 | -7.2 [-13.4, +1.3] |
| LONDON | 19 (RANGE_BOUND) | 13 | MEDIUM | 5 (NOT READABLE) | 60.0 | +0.9 [-8.8, +11.6] |
| LONDON | 19 (RANGE_BOUND) | 14 | STRONG | 1 (NOT READABLE) | 0.0 | -13.0 [-13.0, -13.0] |
| LONDON | 19 (RANGE_BOUND) | 15 | STRONG | 1 (NOT READABLE) | 0.0 | -14.3 [-14.3, -14.3] |
| LONDON | 15 (TRANSITIONAL) | 6 | WEAK | 52 (NOT READABLE) | 50.0 | -0.9 [-4.9, +3.5] |
| LONDON | 15 (TRANSITIONAL) | 7 | WEAK | 44 (NOT READABLE) | 50.0 | -1.7 [-6.4, +3.2] |
| LONDON | 15 (TRANSITIONAL) | 8 | MEDIUM | 23 (NOT READABLE) | 43.5 | -1.9 [-7.8, +4.6] |
| LONDON | 15 (TRANSITIONAL) | 9 | MEDIUM | 14 (NOT READABLE) | 50.0 | -3.2 [-9.1, +3.4] |
| LONDON | 15 (TRANSITIONAL) | 10 | MEDIUM | 11 (NOT READABLE) | 36.4 | -8.2 [-16.4, +3.2] |
| LONDON | 15 (TRANSITIONAL) | 11 | STRONG | 5 (NOT READABLE) | 40.0 | -6.1 [-18.3, +7.8] |
| LONDON | 15 (TRANSITIONAL) | 12 | STRONG | 1 (NOT READABLE) | 100.0 | +17.3 [+17.3, +17.3] |
| LONDON | 15 (TRANSITIONAL) | 13 | STRONG | 2 (NOT READABLE) | 50.0 | -2.1 [-12.2, +8.0] |
| ASIA | 20 (TRENDING) | 7 | WEAK | 278 | 39.6 | -3.8 [-6.6, -1.3] |
| ASIA | 20 (TRENDING) | 8 | WEAK | 273 | 44.7 | -2.5 [-5.0, -0.3] |
| ASIA | 20 (TRENDING) | 9 | WEAK | 253 | 50.6 | -1.0 [-3.8, +1.6] |
| ASIA | 20 (TRENDING) | 10 | WEAK | 187 | 48.7 | -2.8 [-6.0, +0.2] |
| ASIA | 20 (TRENDING) | 11 | MEDIUM | 172 | 47.1 | -3.5 [-6.9, -0.3] |
| ASIA | 20 (TRENDING) | 12 | MEDIUM | 99 (NOT READABLE) | 51.5 | -3.7 [-6.5, -0.5] |
| ASIA | 20 (TRENDING) | 13 | MEDIUM | 70 (NOT READABLE) | 38.6 | -6.6 [-9.3, -3.6] |
| ASIA | 20 (TRENDING) | 14 | STRONG | 32 (NOT READABLE) | 62.5 | +1.5 [-2.6, +5.7] |
| ASIA | 20 (TRENDING) | 15 | STRONG | 13 (NOT READABLE) | 61.5 | +4.5 [-4.0, +15.8] |
| ASIA | 20 (TRENDING) | 16 | STRONG | 4 (NOT READABLE) | 50.0 | -11.0 [-29.1, +7.2] |
| ASIA | 20 (TRENDING) | 17 | STRONG | 2 (NOT READABLE) | 100.0 | +16.2 [+10.9, +21.6] |
| ASIA | 20 (TRENDING) | 18 | STRONG | 3 (NOT READABLE) | 100.0 | +16.6 [+11.8, +19.1] |
| ASIA | 19 (RANGE_BOUND) | 7 | WEAK | 51 (NOT READABLE) | 47.1 | +1.6 [-4.0, +7.0] |
| ASIA | 19 (RANGE_BOUND) | 8 | WEAK | 31 (NOT READABLE) | 51.6 | -2.2 [-6.9, +3.6] |
| ASIA | 19 (RANGE_BOUND) | 9 | WEAK | 23 (NOT READABLE) | 34.8 | -5.6 [-11.1, +0.7] |
| ASIA | 19 (RANGE_BOUND) | 10 | WEAK | 29 (NOT READABLE) | 51.7 | -1.8 [-8.7, +4.0] |
| ASIA | 19 (RANGE_BOUND) | 11 | MEDIUM | 13 (NOT READABLE) | 61.5 | +1.0 [-6.4, +6.8] |
| ASIA | 19 (RANGE_BOUND) | 12 | MEDIUM | 10 (NOT READABLE) | 40.0 | -4.4 [-12.3, +6.9] |
| ASIA | 19 (RANGE_BOUND) | 13 | MEDIUM | 2 (NOT READABLE) | 100.0 | +9.6 [+6.5, +12.7] |
| ASIA | 19 (RANGE_BOUND) | 14 | STRONG | 2 (NOT READABLE) | 50.0 | -1.1 [-15.2, +12.9] |
| ASIA | 15 (TRANSITIONAL) | 6 | WEAK | 73 (NOT READABLE) | 56.2 | -0.5 [-4.3, +3.1] |
| ASIA | 15 (TRANSITIONAL) | 7 | WEAK | 45 (NOT READABLE) | 57.8 | +0.0 [-4.6, +4.4] |
| ASIA | 15 (TRANSITIONAL) | 8 | MEDIUM | 33 (NOT READABLE) | 48.5 | -4.1 [-9.1, +1.6] |
| ASIA | 15 (TRANSITIONAL) | 9 | MEDIUM | 15 (NOT READABLE) | 60.0 | -0.3 [-9.8, +6.6] |
| ASIA | 15 (TRANSITIONAL) | 10 | MEDIUM | 6 (NOT READABLE) | 83.3 | +6.8 [-3.7, +14.8] |
| ASIA | 15 (TRANSITIONAL) | 11 | STRONG | 8 (NOT READABLE) | 75.0 | +4.6 [-5.8, +14.4] |
| ASIA | 15 (TRANSITIONAL) | 12 | STRONG | 1 (NOT READABLE) | 0.0 | -14.0 [-14.0, -14.0] |

Boundary comparisons (main window): the lowest integer of the upper tier minus the highest integer of the lower tier.

| Session | MaxScore | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|
| NY | 20 | MEDIUM floor 11 - WEAK top 10 | +0.4 [-1.6, +2.3] (490/615) | -1.7 [-4.0, +1.1] (191/267) | +1.8 [-0.8, +4.0] (299/348) | NO DIFFERENCE SHOWN |
| NY | 20 | STRONG floor 14 - MEDIUM top 13 | +0.4 [-2.2, +3.0] (154/297) | +1.5 [-2.7, +5.5] (65/120) | -0.4 [-3.6, +2.9] (89/177) | NO DIFFERENCE SHOWN |
| NY | 20 | MEDIUM floor 11 - WEAK floor 7 | -0.4 [-2.2, +1.4] (490/710) | -2.0 [-4.9, +1.1] (191/286) | +0.6 [-1.6, +3.0] (299/424) | NO DIFFERENCE SHOWN |
| NY | 19 | MEDIUM floor 11 - WEAK top 10 | +1.9 [-1.5, +4.6] (57/63) | +0.6 [-5.0, +5.8] (19/16) | +2.5 [-1.8, +5.9] (38/47) | NOT READABLE |
| NY | 19 | STRONG floor 14 - MEDIUM top 13 | -2.1 [-13.6, +17.4] (6/27) | +18.6 [+15.2, +24.5] (1/6) | -6.6 [-15.8, +13.0] (5/21) | NOT READABLE |
| NY | 19 | MEDIUM floor 11 - WEAK floor 7 | +2.3 [-2.0, +6.4] (57/119) | -0.2 [-5.4, +5.6] (19/39) | +3.6 [-2.5, +9.1] (38/80) | NOT READABLE |
| NY | 15 | MEDIUM floor 8 - WEAK top 7 | -1.0 [-4.7, +2.2] (103/145) | -3.0 [-11.0, +3.2] (42/57) | +0.2 [-2.7, +3.5] (61/88) | NO DIFFERENCE SHOWN |
| NY | 15 | STRONG floor 11 - MEDIUM top 10 | +3.0 [-6.3, +12.1] (9/38) | -3.7 [-11.1, +10.2] (4/20) | +9.1 [-7.0, +19.4] (5/18) | NOT READABLE |
| NY | 15 | MEDIUM floor 8 - WEAK floor 6 | -1.6 [-5.2, +1.9] (103/182) | -2.7 [-9.7, +2.9] (42/65) | -1.1 [-5.0, +3.5] (61/117) | NO DIFFERENCE SHOWN |
| LONDON | 20 | MEDIUM floor 11 - WEAK top 10 | +1.2 [-3.2, +5.1] (190/208) | +2.8 [-1.9, +7.3] (86/96) | -0.2 [-6.9, +5.8] (104/112) | NO DIFFERENCE SHOWN |
| LONDON | 20 | STRONG floor 14 - MEDIUM top 13 | +1.4 [-5.6, +10.1] (50/95) | -4.6 [-10.1, +1.1] (32/41) | +11.6 [-3.9, +28.6] (18/54) | NOT READABLE |
| LONDON | 20 | MEDIUM floor 11 - WEAK floor 7 | +1.0 [-2.6, +4.4] (190/208) | +2.4 [-2.3, +6.9] (86/103) | -0.2 [-5.6, +5.3] (104/105) | NO DIFFERENCE SHOWN |
| LONDON | 19 | MEDIUM floor 11 - WEAK top 10 | -11.2 [-19.5, -0.5] (10/14) | -4.0 [-9.0, +6.6] (5/5) | -14.2 [-28.2, -0.2] (5/9) | NOT READABLE |
| LONDON | 19 | STRONG floor 14 - MEDIUM top 13 | -13.9 [-25.6, -8.2] (1/5) | -24.6 [-24.6, -22.6] (1/2) | n/a [n/a, n/a] (0/3) | NOT READABLE |
| LONDON | 19 | MEDIUM floor 11 - WEAK floor 7 | -4.9 [-13.4, +6.0] (10/33) | -5.4 [-12.0, +10.0] (5/19) | -5.0 [-20.1, +14.0] (5/14) | NOT READABLE |
| LONDON | 15 | MEDIUM floor 8 - WEAK top 7 | -0.2 [-6.4, +5.3] (23/44) | +0.4 [-5.6, +6.4] (15/28) | -1.3 [-16.3, +11.0] (8/16) | NOT READABLE |
| LONDON | 15 | STRONG floor 11 - MEDIUM top 10 | +2.2 [-14.4, +17.2] (5/11) | +0.7 [-19.7, +18.3] (2/3) | +1.1 [-18.2, +18.5] (3/8) | NOT READABLE |
| LONDON | 15 | MEDIUM floor 8 - WEAK floor 6 | -1.1 [-8.9, +6.9] (23/52) | +5.5 [-1.9, +13.2] (15/25) | -7.3 [-22.0, +8.2] (8/27) | NOT READABLE |
| ASIA | 20 | MEDIUM floor 11 - WEAK top 10 | -0.6 [-4.2, +3.2] (172/187) | -3.1 [-7.3, +2.0] (73/67) | +1.0 [-4.2, +6.5] (99/120) | NO DIFFERENCE SHOWN |
| ASIA | 20 | STRONG floor 14 - MEDIUM top 13 | +8.1 [+2.9, +12.9] (32/70) | +9.1 [+3.2, +16.3] (11/35) | +6.9 [-1.2, +13.7] (21/35) | NOT READABLE |
| ASIA | 20 | MEDIUM floor 11 - WEAK floor 7 | +0.3 [-3.7, +4.5] (172/278) | -1.5 [-6.6, +4.0] (73/107) | +1.7 [-4.4, +7.5] (99/171) | NO DIFFERENCE SHOWN |
| ASIA | 19 | MEDIUM floor 11 - WEAK top 10 | +2.7 [-5.0, +10.1] (13/29) | +7.7 [-7.4, +17.7] (6/8) | +1.3 [-7.9, +11.5] (7/21) | NOT READABLE |
| ASIA | 19 | STRONG floor 14 - MEDIUM top 13 | -10.8 [-27.9, +6.4] (2/2) | n/a [n/a, n/a] (0/0) | -10.8 [-27.9, +6.4] (2/2) | NOT READABLE |
| ASIA | 19 | MEDIUM floor 11 - WEAK floor 7 | -0.7 [-9.6, +6.6] (13/51) | +0.4 [-16.7, +12.7] (6/14) | -0.6 [-11.2, +8.4] (7/37) | NOT READABLE |
| ASIA | 15 | MEDIUM floor 8 - WEAK top 7 | -4.1 [-11.4, +3.8] (33/45) | -6.5 [-14.8, +5.3] (15/10) | -1.4 [-11.8, +10.4] (18/35) | NOT READABLE |
| ASIA | 15 | STRONG floor 11 - MEDIUM top 10 | -2.2 [-12.8, +11.5] (8/6) | -28.6 [-28.6, -28.6] (1/1) | +1.9 [-6.2, +19.0] (7/5) | NOT READABLE |
| ASIA | 15 | MEDIUM floor 8 - WEAK floor 6 | -3.6 [-8.4, +2.3] (33/73) | -5.2 [-13.8, +1.9] (15/20) | -1.2 [-8.2, +8.5] (18/53) | NOT READABLE |

## 4. D-3: which votes lift a signal into MEDIUM, and do they help?

### 4.1 Vote composition by tier: share of rows where the vote agrees / opposes the trade side (%)

| Vote | NY STRONG agree / oppose | NY MEDIUM agree / oppose | NY WEAK agree / oppose | LONDON STRONG agree / oppose | LONDON MEDIUM agree / oppose | LONDON WEAK agree / oppose | ASIA STRONG agree / oppose | ASIA MEDIUM agree / oppose | ASIA WEAK agree / oppose |
|---|---|---|---|---|---|---|---|---|---|
| ROC | 92 / 1 | 81 / 3 | 52 / 16 | 95 / 1 | 82 / 3 | 57 / 12 | 92 / 0 | 70 / 4 | 44 / 9 |
| RSI | 94 / 0 | 90 / 0 | 62 / 8 | 94 / 0 | 91 / 0 | 69 / 5 | 100 / 0 | 91 / 0 | 70 / 4 |
| DMI | 100 / 0 | 99 / 1 | 97 / 3 | 100 / 0 | 100 / 0 | 98 / 2 | 100 / 0 | 100 / 0 | 99 / 1 |
| ADX | 93 / 0 | 78 / 0 | 80 / 0 | 92 / 0 | 85 / 0 | 84 / 0 | 83 / 0 | 81 / 0 | 80 / 0 |
| VOL | 0 / 0 | 0 / 0 | 0 / 0 | 1 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| VWAP | 78 / 0 | 80 / 1 | 82 / 6 | 80 / 0 | 81 / 3 | 81 / 8 | 77 / 0 | 72 / 1 | 78 / 4 |
| BBW_TTM | 84 / 0 | 73 / 4 | 37 / 27 | 88 / 0 | 73 / 3 | 41 / 19 | 92 / 0 | 70 / 4 | 37 / 20 |
| EMA | 99 / 0 | 96 / 0 | 85 / 2 | 98 / 1 | 96 / 0 | 92 / 1 | 100 / 0 | 93 / 0 | 91 / 1 |
| FUNDING | 23 / 13 | 27 / 20 | 27 / 26 | 31 / 24 | 29 / 24 | 29 / 30 | 22 / 14 | 16 / 13 | 19 / 24 |
| OI | 41 / 0 | 19 / 0 | 11 / 2 | 32 / 0 | 11 / 1 | 8 / 3 | 43 / 0 | 20 / 0 | 6 / 2 |
| OFI | 43 / 21 | 38 / 24 | 30 / 31 | 55 / 20 | 42 / 28 | 33 / 34 | 43 / 25 | 42 / 24 | 32 / 35 |
| CVD | 90 / 2 | 56 / 8 | 29 / 22 | 87 / 2 | 52 / 11 | 23 / 27 | 94 / 3 | 60 / 8 | 28 / 25 |
| TFI | 85 / 12 | 68 / 25 | 47 / 42 | 89 / 6 | 67 / 26 | 45 / 44 | 89 / 8 | 73 / 20 | 47 / 43 |
| MICROCVD | 52 / 6 | 37 / 23 | 32 / 43 | 53 / 6 | 40 / 23 | 29 / 49 | 48 / 6 | 40 / 18 | 32 / 44 |
| LIQ | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| SPREAD | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 2 | 0 / 0 | 0 / 0 |
| EMA200 | 95 / 5 | 87 / 13 | 83 / 17 | 96 / 4 | 93 / 7 | 85 / 15 | 95 / 5 | 93 / 7 | 87 / 13 |
| DONCHIAN | 91 / 0 | 80 / 0 | 48 / 6 | 92 / 0 | 74 / 0 | 46 / 3 | 97 / 0 | 75 / 1 | 47 / 3 |
| OBV | 78 / 5 | 64 / 8 | 55 / 11 | 82 / 2 | 55 / 3 | 45 / 6 | 60 / 3 | 48 / 9 | 38 / 12 |
| VPFR | 60 / 26 | 44 / 38 | 39 / 40 | 66 / 18 | 58 / 25 | 40 / 31 | 55 / 25 | 50 / 27 | 44 / 30 |
| REGIME_ALIGN | 87 / 0 | 49 / 0 | 19 / 3 | 82 / 0 | 45 / 0 | 16 / 1 | 83 / 0 | 50 / 0 | 22 / 1 |
| TREND_STRUCTURE | 71 / 0 | 51 / 0 | 47 / 0 | 63 / 0 | 49 / 0 | 49 / 0 | 46 / 0 | 42 / 0 | 45 / 0 |

- Mean net lean of the trade side, summed over votes, equals the logged effective margin plus the regime penalty on matching rows; see section 0 for the ledger.

### 4.2 Per vote: net EV when the vote agrees, opposes or is absent (pooled over tiers)

#### main window

| Session | Vote | n agree / oppose / absent | EV agree | EV oppose | EV absent | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | ROC | 3417 / 606 / 1394 | -4.0 | -0.7 | -3.5 | agree - oppose | -3.3 [-5.4, -1.1] (3417/606) | -2.3 [-4.5, +0.1] (1367/209) | -3.9 [-6.5, -0.9] (2050/397) | DISCOVERY ONLY (H2) (d < 0) |
| NY | ROC | 3417 / 606 / 1394 | -4.0 | -0.7 | -3.5 | agree - absent | -0.4 [-1.6, +0.7] (3417/1394) | +0.4 [-1.6, +2.3] (1367/584) | -1.0 [-2.4, +0.3] (2050/810) | NO DIFFERENCE SHOWN |
| NY | RSI | 3917 / 292 / 1208 | -4.0 | -1.3 | -2.5 | agree - oppose | -2.7 [-5.3, -0.3] (3917/292) | -5.5 [-8.0, -2.4] (1575/93) | -1.5 [-4.9, +1.5] (2342/199) | NO DIFFERENCE SHOWN |
| NY | RSI | 3917 / 292 / 1208 | -4.0 | -1.3 | -2.5 | agree - absent | -1.5 [-3.3, +0.2] (3917/1208) | -0.8 [-2.6, +0.6] (1575/492) | -2.0 [-4.7, +0.7] (2342/716) | NO DIFFERENCE SHOWN |
| NY | DMI | 5312 / 105 / 0 | -3.5 | -2.0 | n/a | agree - oppose | -1.5 [-4.5, +1.3] (5312/105) | -3.4 [-9.0, +2.2] (2129/31) | -0.8 [-4.3, +2.6] (3183/74) | NO DIFFERENCE SHOWN |
| NY | DMI | 5312 / 105 / 0 | -3.5 | -2.0 | n/a | agree - absent | n/a [n/a, n/a] (5312/0) | n/a [n/a, n/a] (2129/0) | n/a [n/a, n/a] (3183/0) | NOT READABLE |
| NY | ADX | 4356 / 0 / 1061 | -3.6 | n/a | -3.0 | agree - oppose | n/a [n/a, n/a] (4356/0) | n/a [n/a, n/a] (1798/0) | n/a [n/a, n/a] (2558/0) | NOT READABLE |
| NY | ADX | 4356 / 0 / 1061 | -3.6 | n/a | -3.0 | agree - absent | -0.6 [-2.1, +1.1] (4356/1061) | -1.0 [-3.6, +1.5] (1798/362) | -0.4 [-2.5, +1.8] (2558/699) | NO DIFFERENCE SHOWN |
| NY | VOL | 4 / 0 / 5413 | -7.3 | n/a | -3.5 | agree - oppose | n/a [n/a, n/a] (4/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (4/0) | NOT READABLE |
| NY | VOL | 4 / 0 / 5413 | -7.3 | n/a | -3.5 | agree - absent | -3.8 [-8.6, +8.9] (4/5413) | n/a [n/a, n/a] (0/2160) | -3.7 [-8.7, +9.2] (4/3253) | NOT READABLE |
| NY | VWAP | 4388 / 225 / 804 | -3.3 | -1.7 | -4.8 | agree - oppose | -1.6 [-4.9, +1.6] (4388/225) | +1.2 [-4.8, +4.8] (1789/57) | -2.7 [-6.5, +1.3] (2599/168) | NO DIFFERENCE SHOWN |
| NY | VWAP | 4388 / 225 / 804 | -3.3 | -1.7 | -4.8 | agree - absent | +1.5 [-0.3, +3.2] (4388/804) | +1.4 [-1.5, +4.3] (1789/314) | +1.5 [-0.8, +3.7] (2599/490) | NO DIFFERENCE SHOWN |
| NY | BBW_TTM | 2744 / 1023 / 1650 | -4.0 | -2.0 | -3.5 | agree - oppose | -2.1 [-3.6, -0.5] (2744/1023) | -2.0 [-4.3, +0.5] (1060/425) | -2.1 [-4.2, -0.1] (1684/598) | DISCOVERY ONLY (H2) (d < 0) |
| NY | BBW_TTM | 2744 / 1023 / 1650 | -4.0 | -2.0 | -3.5 | agree - absent | -0.5 [-1.8, +0.8] (2744/1650) | -0.5 [-2.3, +1.0] (1060/675) | -0.5 [-2.2, +1.6] (1684/975) | NO DIFFERENCE SHOWN |
| NY | EMA | 4840 / 91 / 486 | -3.5 | -2.0 | -3.6 | agree - oppose | -1.5 [-4.0, +1.0] (4840/91) | -1.5 [-6.7, +2.5] (1939/24) | -1.5 [-4.4, +1.8] (2901/67) | NOT READABLE |
| NY | EMA | 4840 / 91 / 486 | -3.5 | -2.0 | -3.6 | agree - absent | +0.1 [-2.1, +2.1] (4840/486) | -3.4 [-6.5, -0.3] (1939/197) | +2.5 [+0.3, +4.5] (2901/289) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| NY | FUNDING | 1456 / 1255 / 2706 | -3.9 | -2.4 | -3.8 | agree - oppose | -1.5 [-3.1, -0.0] (1456/1255) | -2.7 [-5.0, -0.6] (662/618) | -0.4 [-2.6, +1.6] (794/637) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| NY | FUNDING | 1456 / 1255 / 2706 | -3.9 | -2.4 | -3.8 | agree - absent | -0.1 [-2.0, +1.5] (1456/2706) | -0.5 [-3.4, +1.8] (662/880) | +0.2 [-2.2, +2.3] (794/1826) | NO DIFFERENCE SHOWN |
| NY | OI | 815 / 92 / 4510 | -4.6 | -0.6 | -3.3 | agree - oppose | -4.1 [-8.4, +0.2] (815/92) | -1.1 [-6.8, +3.6] (245/38) | -6.1 [-12.0, -0.4] (570/54) | NOT READABLE |
| NY | OI | 815 / 92 / 4510 | -4.6 | -0.6 | -3.3 | agree - absent | -1.3 [-3.4, +0.4] (815/4510) | -1.4 [-5.9, +2.6] (245/1877) | -1.2 [-3.5, +0.3] (570/2633) | NO DIFFERENCE SHOWN |
| NY | OFI | 1808 / 1527 / 2082 | -2.4 | -4.3 | -3.8 | agree - oppose | +1.8 [+0.6, +3.1] (1808/1527) | +2.6 [+0.6, +4.6] (795/608) | +1.3 [-0.3, +2.7] (1013/919) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | OFI | 1808 / 1527 / 2082 | -2.4 | -4.3 | -3.8 | agree - absent | +1.4 [+0.4, +2.5] (1808/2082) | +1.9 [+0.5, +3.3] (795/757) | +1.0 [-0.3, +2.5] (1013/1325) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | CVD | 2194 / 898 / 2325 | -3.0 | -4.5 | -3.5 | agree - oppose | +1.4 [+0.1, +2.7] (2194/898) | +1.4 [-1.0, +3.6] (880/340) | +1.5 [-0.3, +2.9] (1314/558) | NO DIFFERENCE SHOWN |
| NY | CVD | 2194 / 898 / 2325 | -3.0 | -4.5 | -3.5 | agree - absent | +0.5 [-0.4, +1.5] (2194/2325) | +0.8 [-0.7, +2.4] (880/940) | +0.3 [-0.9, +1.6] (1314/1385) | NO DIFFERENCE SHOWN |
| NY | TFI | 3013 / 1908 / 496 | -3.2 | -3.9 | -3.6 | agree - oppose | +0.7 [-0.0, +1.5] (3013/1908) | +1.7 [+0.3, +3.0] (1248/762) | +0.1 [-0.8, +1.0] (1765/1146) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | TFI | 3013 / 1908 / 496 | -3.2 | -3.9 | -3.6 | agree - absent | +0.4 [-0.9, +1.7] (3013/496) | +0.4 [-2.1, +2.7] (1248/150) | +0.3 [-1.3, +1.9] (1765/346) | NO DIFFERENCE SHOWN |
| NY | MICROCVD | 1895 / 1912 / 1610 | -3.3 | -3.4 | -3.9 | agree - oppose | +0.1 [-1.1, +1.2] (1895/1912) | +0.5 [-1.2, +2.1] (767/742) | -0.2 [-1.8, +1.4] (1128/1170) | NO DIFFERENCE SHOWN |
| NY | MICROCVD | 1895 / 1912 / 1610 | -3.3 | -3.4 | -3.9 | agree - absent | +0.6 [-0.5, +1.8] (1895/1610) | +0.3 [-1.5, +2.2] (767/651) | +0.8 [-0.7, +2.3] (1128/959) | NO DIFFERENCE SHOWN |
| NY | SPREAD | 0 / 1 / 5416 | n/a | -130.3 | -3.5 | agree - oppose | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| NY | SPREAD | 0 / 1 / 5416 | n/a | -130.3 | -3.5 | agree - absent | n/a [n/a, n/a] (0/5416) | n/a [n/a, n/a] (0/2160) | n/a [n/a, n/a] (0/3256) | NOT READABLE |
| NY | EMA200 | 4604 / 813 / 0 | -3.5 | -3.5 | n/a | agree - oppose | +0.1 [-1.8, +1.9] (4604/813) | -2.3 [-4.4, +0.4] (1839/321) | +1.6 [-0.7, +3.9] (2765/492) | NO DIFFERENCE SHOWN |
| NY | EMA200 | 4604 / 813 / 0 | -3.5 | -3.5 | n/a | agree - absent | n/a [n/a, n/a] (4604/0) | n/a [n/a, n/a] (1839/0) | n/a [n/a, n/a] (2765/0) | NOT READABLE |
| NY | DONCHIAN | 3233 / 200 / 1984 | -4.0 | -1.4 | -2.9 | agree - oppose | -2.6 [-5.2, -0.2] (3233/200) | -5.3 [-8.1, -1.7] (1370/68) | -1.3 [-4.4, +1.8] (1863/132) | NO DIFFERENCE SHOWN |
| NY | DONCHIAN | 3233 / 200 / 1984 | -4.0 | -1.4 | -2.9 | agree - absent | -1.1 [-2.5, +0.3] (3233/1984) | -0.5 [-2.2, +1.1] (1370/722) | -1.5 [-3.4, +0.5] (1863/1262) | NO DIFFERENCE SHOWN |
| NY | OBV | 3179 / 524 / 1714 | -3.5 | -3.2 | -3.5 | agree - oppose | -0.3 [-2.2, +2.0] (3179/524) | -0.6 [-3.3, +2.3] (1304/262) | +0.0 [-2.7, +3.7] (1875/262) | NO DIFFERENCE SHOWN |
| NY | OBV | 3179 / 524 / 1714 | -3.5 | -3.2 | -3.5 | agree - absent | -0.0 [-1.6, +1.7] (3179/1714) | -0.0 [-2.2, +2.4] (1304/594) | -0.0 [-2.1, +2.2] (1875/1120) | NO DIFFERENCE SHOWN |
| NY | VPFR | 2285 / 2100 / 1032 | -3.4 | -3.1 | -4.4 | agree - oppose | -0.2 [-1.5, +1.0] (2285/2100) | +0.9 [-0.7, +2.3] (920/919) | -1.1 [-2.8, +0.7] (1365/1181) | NO DIFFERENCE SHOWN |
| NY | VPFR | 2285 / 2100 / 1032 | -3.4 | -3.1 | -4.4 | agree - absent | +1.0 [-0.2, +2.3] (2285/1032) | +0.0 [-2.4, +2.3] (920/321) | +1.4 [-0.0, +3.0] (1365/711) | NO DIFFERENCE SHOWN |
| NY | REGIME_ALIGN | 1726 / 97 / 3594 | -2.9 | -3.0 | -3.8 | agree - oppose | +0.1 [-2.7, +2.8] (1726/97) | -1.8 [-6.1, +3.3] (703/38) | +1.2 [-1.9, +4.3] (1023/59) | NOT READABLE |
| NY | REGIME_ALIGN | 1726 / 97 / 3594 | -2.9 | -3.0 | -3.8 | agree - absent | +0.9 [-0.3, +2.0] (1726/3594) | +0.3 [-1.7, +2.2] (703/1419) | +1.3 [-0.1, +2.7] (1023/2175) | NO DIFFERENCE SHOWN |
| NY | TREND_STRUCTURE | 2697 / 0 / 2720 | -3.2 | n/a | -3.8 | agree - oppose | n/a [n/a, n/a] (2697/0) | n/a [n/a, n/a] (1065/0) | n/a [n/a, n/a] (1632/0) | NOT READABLE |
| NY | TREND_STRUCTURE | 2697 / 0 / 2720 | -3.2 | n/a | -3.8 | agree - absent | +0.6 [-0.7, +2.0] (2697/2720) | -0.6 [-1.8, +0.5] (1065/1095) | +1.4 [-0.7, +3.4] (1632/1625) | NO DIFFERENCE SHOWN |
| LONDON | ROC | 1126 / 140 / 399 | -1.0 | -0.7 | -1.6 | agree - oppose | -0.3 [-4.5, +4.1] (1126/140) | -1.7 [-5.9, +2.9] (480/52) | +0.7 [-5.4, +7.2] (646/88) | NO DIFFERENCE SHOWN |
| LONDON | ROC | 1126 / 140 / 399 | -1.0 | -0.7 | -1.6 | agree - absent | +0.6 [-2.0, +3.3] (1126/399) | -1.1 [-3.9, +2.0] (480/222) | +2.0 [-2.2, +6.5] (646/177) | NO DIFFERENCE SHOWN |
| LONDON | RSI | 1287 / 51 / 327 | -1.1 | -1.7 | -1.2 | agree - oppose | +0.6 [-4.9, +5.8] (1287/51) | -1.2 [-6.7, +3.8] (574/21) | +2.0 [-6.5, +9.9] (713/30) | NOT READABLE |
| LONDON | RSI | 1287 / 51 / 327 | -1.1 | -1.7 | -1.2 | agree - absent | +0.1 [-3.1, +3.1] (1287/327) | -0.6 [-3.8, +2.3] (574/159) | +0.7 [-4.8, +5.7] (713/168) | NO DIFFERENCE SHOWN |
| LONDON | DMI | 1643 / 22 / 0 | -1.1 | -3.5 | n/a | agree - oppose | +2.4 [-3.9, +8.1] (1643/22) | +1.0 [-7.9, +8.0] (738/16) | +4.9 [-3.9, +17.8] (905/6) | NOT READABLE |
| LONDON | DMI | 1643 / 22 / 0 | -1.1 | -3.5 | n/a | agree - absent | n/a [n/a, n/a] (1643/0) | n/a [n/a, n/a] (738/0) | n/a [n/a, n/a] (905/0) | NOT READABLE |
| LONDON | ADX | 1410 / 0 / 255 | -1.0 | n/a | -1.8 | agree - oppose | n/a [n/a, n/a] (1410/0) | n/a [n/a, n/a] (626/0) | n/a [n/a, n/a] (784/0) | NOT READABLE |
| LONDON | ADX | 1410 / 0 / 255 | -1.0 | n/a | -1.8 | agree - absent | +0.7 [-1.4, +2.9] (1410/255) | +1.6 [-1.9, +4.8] (626/128) | -0.3 [-2.8, +2.4] (784/127) | NO DIFFERENCE SHOWN |
| LONDON | VOL | 1 / 0 / 1664 | -12.7 | n/a | -1.1 | agree - oppose | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| LONDON | VOL | 1 / 0 / 1664 | -12.7 | n/a | -1.1 | agree - absent | -11.6 [-12.9, -10.2] (1/1664) | -10.9 [-12.7, -8.8] (1/753) | n/a [n/a, n/a] (0/911) | NOT READABLE |
| LONDON | VWAP | 1344 / 94 / 227 | -1.5 | -5.7 | +2.8 | agree - oppose | +4.3 [-3.0, +10.8] (1344/94) | -0.5 [-6.3, +5.1] (576/52) | +9.7 [-6.3, +17.7] (768/42) | NOT READABLE |
| LONDON | VWAP | 1344 / 94 / 227 | -1.5 | -5.7 | +2.8 | agree - absent | -4.2 [-10.3, +0.3] (1344/227) | -4.1 [-10.8, +1.3] (576/126) | -4.9 [-15.1, +3.3] (768/101) | NO DIFFERENCE SHOWN |
| LONDON | BBW_TTM | 898 / 213 / 554 | -0.5 | -0.9 | -2.3 | agree - oppose | +0.4 [-3.4, +4.2] (898/213) | -0.7 [-4.0, +2.9] (404/108) | +1.1 [-5.8, +8.0] (494/105) | NO DIFFERENCE SHOWN |
| LONDON | BBW_TTM | 898 / 213 / 554 | -0.5 | -0.9 | -2.3 | agree - absent | +1.8 [-1.9, +5.9] (898/554) | +0.4 [-3.0, +4.1] (404/242) | +2.8 [-3.0, +9.6] (494/312) | NO DIFFERENCE SHOWN |
| LONDON | EMA | 1557 / 14 / 94 | -0.9 | -6.4 | -3.8 | agree - oppose | +5.5 [-2.6, +11.5] (1557/14) | +3.5 [-5.7, +8.8] (701/9) | +8.4 [-13.2, +17.0] (856/5) | NOT READABLE |
| LONDON | EMA | 1557 / 14 / 94 | -0.9 | -6.4 | -3.8 | agree - absent | +2.9 [-1.1, +6.9] (1557/94) | +4.2 [+0.3, +8.8] (701/44) | +1.7 [-5.4, +8.1] (856/50) | NOT READABLE |
| LONDON | FUNDING | 485 / 457 / 723 | -1.9 | +1.9 | -2.5 | agree - oppose | -3.8 [-8.9, +1.1] (485/457) | -0.7 [-7.7, +5.8] (197/200) | -6.1 [-13.2, +0.4] (288/257) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING | 485 / 457 / 723 | -1.9 | +1.9 | -2.5 | agree - absent | +0.6 [-2.2, +3.1] (485/723) | +1.4 [-1.4, +4.3] (197/357) | -0.1 [-4.7, +3.8] (288/366) | NO DIFFERENCE SHOWN |
| LONDON | OI | 176 / 34 / 1455 | +1.0 | -0.9 | -1.4 | agree - oppose | +2.0 [-11.8, +13.3] (176/34) | -15.0 [-20.7, -9.8] (39/7) | +6.6 [-9.6, +19.6] (137/27) | NOT READABLE |
| LONDON | OI | 176 / 34 / 1455 | +1.0 | -0.9 | -1.4 | agree - absent | +2.4 [-3.1, +7.1] (176/1455) | -4.9 [-10.0, +0.3] (39/708) | +4.3 [-2.3, +10.1] (137/747) | NO DIFFERENCE SHOWN |
| LONDON | OFI | 619 / 517 / 529 | -0.7 | -1.5 | -1.3 | agree - oppose | +0.8 [-1.3, +2.8] (619/517) | +1.8 [-0.3, +3.8] (292/253) | -0.2 [-3.9, +3.3] (327/264) | NO DIFFERENCE SHOWN |
| LONDON | OFI | 619 / 517 / 529 | -0.7 | -1.5 | -1.3 | agree - absent | +0.7 [-1.5, +2.6] (619/529) | +0.7 [-1.5, +3.2] (292/209) | +0.7 [-2.9, +3.7] (327/320) | NO DIFFERENCE SHOWN |
| LONDON | CVD | 609 / 334 / 722 | -1.7 | -0.0 | -1.2 | agree - oppose | -1.7 [-5.2, +1.6] (609/334) | -1.4 [-5.2, +2.4] (287/147) | -1.8 [-7.7, +3.5] (322/187) | NO DIFFERENCE SHOWN |
| LONDON | CVD | 609 / 334 / 722 | -1.7 | -0.0 | -1.2 | agree - absent | -0.5 [-2.7, +1.6] (609/722) | -1.0 [-3.1, +1.4] (287/320) | -0.0 [-3.5, +3.3] (322/402) | NO DIFFERENCE SHOWN |
| LONDON | TFI | 918 / 596 / 151 | -1.4 | -1.3 | +0.9 | agree - oppose | -0.1 [-2.0, +1.8] (918/596) | -0.0 [-2.5, +2.2] (449/260) | -0.0 [-2.9, +2.9] (469/336) | NO DIFFERENCE SHOWN |
| LONDON | TFI | 918 / 596 / 151 | -1.4 | -1.3 | +0.9 | agree - absent | -2.3 [-5.0, +0.1] (918/151) | -5.5 [-10.9, -0.4] (449/45) | -0.6 [-3.2, +2.2] (469/106) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | 562 / 634 / 469 | -1.3 | -0.4 | -1.9 | agree - oppose | -0.9 [-3.7, +2.0] (562/634) | -0.7 [-3.5, +2.2] (267/277) | -0.9 [-5.5, +3.6] (295/357) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | 562 / 634 / 469 | -1.3 | -0.4 | -1.9 | agree - absent | +0.7 [-1.4, +2.8] (562/469) | +0.4 [-2.6, +3.7] (267/210) | +1.0 [-1.9, +3.9] (295/259) | NO DIFFERENCE SHOWN |
| LONDON | SPREAD | 0 / 2 / 1663 | n/a | -16.6 | -1.1 | agree - oppose | n/a [n/a, n/a] (0/2) | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| LONDON | SPREAD | 0 / 2 / 1663 | n/a | -16.6 | -1.1 | agree - absent | n/a [n/a, n/a] (0/1663) | n/a [n/a, n/a] (0/753) | n/a [n/a, n/a] (0/910) | NOT READABLE |
| LONDON | EMA200 | 1464 / 201 / 0 | -0.6 | -5.2 | n/a | agree - oppose | +4.7 [+0.8, +9.1] (1464/201) | +4.0 [-0.9, +8.0] (673/81) | +5.3 [-0.5, +12.8] (791/120) | NO DIFFERENCE SHOWN |
| LONDON | EMA200 | 1464 / 201 / 0 | -0.6 | -5.2 | n/a | agree - absent | n/a [n/a, n/a] (1464/0) | n/a [n/a, n/a] (673/0) | n/a [n/a, n/a] (791/0) | NOT READABLE |
| LONDON | DONCHIAN | 968 / 31 / 666 | -1.0 | -1.7 | -1.3 | agree - oppose | +0.8 [-8.1, +7.1] (968/31) | -3.9 [-19.9, +3.7] (453/9) | +3.2 [-8.7, +11.2] (515/22) | NOT READABLE |
| LONDON | DONCHIAN | 968 / 31 / 666 | -1.0 | -1.7 | -1.3 | agree - absent | +0.4 [-2.6, +4.0] (968/666) | -0.5 [-3.3, +2.4] (453/292) | +1.2 [-3.8, +7.2] (515/374) | NO DIFFERENCE SHOWN |
| LONDON | OBV | 841 / 78 / 746 | +0.0 | -2.9 | -2.2 | agree - oppose | +2.9 [-5.3, +9.3] (841/78) | +6.3 [-1.2, +10.3] (480/29) | +1.4 [-14.0, +12.5] (361/49) | NOT READABLE |
| LONDON | OBV | 841 / 78 / 746 | +0.0 | -2.9 | -2.2 | agree - absent | +2.2 [-1.4, +6.1] (841/746) | +2.7 [-1.9, +7.2] (480/245) | +2.6 [-3.3, +8.9] (361/501) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | 794 / 465 / 406 | -0.8 | -1.9 | -1.0 | agree - oppose | +1.1 [-2.2, +4.4] (794/465) | +3.1 [-0.4, +6.4] (370/221) | -0.7 [-5.6, +4.6] (424/244) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | 794 / 465 / 406 | -0.8 | -1.9 | -1.0 | agree - absent | +0.2 [-3.0, +4.0] (794/406) | -0.8 [-3.4, +2.1] (370/163) | +1.0 [-4.3, +7.1] (424/243) | NO DIFFERENCE SHOWN |
| LONDON | REGIME_ALIGN | 487 / 6 / 1172 | -1.4 | -11.9 | -1.0 | agree - oppose | +10.5 [+7.7, +13.8] (487/6) | n/a [n/a, n/a] (222/0) | +10.8 [+7.4, +14.5] (265/6) | NOT READABLE |
| LONDON | REGIME_ALIGN | 487 / 6 / 1172 | -1.4 | -11.9 | -1.0 | agree - absent | -0.5 [-2.5, +1.7] (487/1172) | +0.1 [-2.2, +2.5] (222/532) | -0.9 [-4.3, +2.7] (265/640) | NO DIFFERENCE SHOWN |
| LONDON | TREND_STRUCTURE | 830 / 0 / 835 | -1.1 | n/a | -1.1 | agree - oppose | n/a [n/a, n/a] (830/0) | n/a [n/a, n/a] (362/0) | n/a [n/a, n/a] (468/0) | NOT READABLE |
| LONDON | TREND_STRUCTURE | 830 / 0 / 835 | -1.1 | n/a | -1.1 | agree - absent | +0.1 [-3.9, +3.4] (830/835) | +1.8 [-1.8, +5.4] (362/392) | -1.5 [-8.1, +4.0] (468/443) | NO DIFFERENCE SHOWN |
| ASIA | ROC | 901 / 133 / 694 | -2.1 | -3.8 | -2.4 | agree - oppose | +1.6 [-1.6, +4.7] (901/133) | -1.6 [-6.2, +4.0] (334/47) | +3.5 [-0.0, +7.0] (567/86) | NO DIFFERENCE SHOWN |
| ASIA | ROC | 901 / 133 / 694 | -2.1 | -3.8 | -2.4 | agree - absent | +0.3 [-1.5, +2.0] (901/694) | -1.3 [-4.3, +1.9] (334/280) | +1.2 [-0.7, +3.1] (567/414) | NO DIFFERENCE SHOWN |
| ASIA | RSI | 1316 / 45 / 367 | -2.4 | -3.0 | -2.1 | agree - oppose | +0.5 [-5.9, +8.3] (1316/45) | -3.3 [-10.4, +3.9] (502/13) | +2.4 [-6.4, +12.8] (814/32) | NOT READABLE |
| ASIA | RSI | 1316 / 45 / 367 | -2.4 | -3.0 | -2.1 | agree - absent | -0.3 [-3.5, +3.9] (1316/367) | -3.9 [-6.6, +0.1] (502/146) | +2.1 [-2.6, +7.7] (814/221) | NO DIFFERENCE SHOWN |
| ASIA | DMI | 1713 / 15 / 0 | -2.4 | +5.7 | n/a | agree - oppose | -8.1 [-20.2, +3.0] (1713/15) | -4.2 [-19.7, +7.8] (656/5) | -10.0 [-26.7, +4.7] (1057/10) | NOT READABLE |
| ASIA | DMI | 1713 / 15 / 0 | -2.4 | +5.7 | n/a | agree - absent | n/a [n/a, n/a] (1713/0) | n/a [n/a, n/a] (656/0) | n/a [n/a, n/a] (1057/0) | NOT READABLE |
| ASIA | ADX | 1386 / 0 / 342 | -2.7 | n/a | -0.8 | agree - oppose | n/a [n/a, n/a] (1386/0) | n/a [n/a, n/a] (551/0) | n/a [n/a, n/a] (835/0) | NOT READABLE |
| ASIA | ADX | 1386 / 0 / 342 | -2.7 | n/a | -0.8 | agree - absent | -1.9 [-4.5, +0.9] (1386/342) | +0.2 [-3.6, +3.7] (551/110) | -2.8 [-6.3, +0.6] (835/232) | NO DIFFERENCE SHOWN |
| ASIA | VOL | 0 / 0 / 1728 | n/a | n/a | -2.4 | agree - oppose | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| ASIA | VOL | 0 / 0 / 1728 | n/a | n/a | -2.4 | agree - absent | n/a [n/a, n/a] (0/1728) | n/a [n/a, n/a] (0/661) | n/a [n/a, n/a] (0/1067) | NOT READABLE |
| ASIA | VWAP | 1323 / 59 / 346 | -1.9 | -0.0 | -4.6 | agree - oppose | -1.9 [-6.3, +3.0] (1323/59) | -8.3 [-16.0, +3.6] (478/20) | +1.5 [-2.6, +6.3] (845/39) | NOT READABLE |
| ASIA | VWAP | 1323 / 59 / 346 | -1.9 | -0.0 | -4.6 | agree - absent | +2.7 [+0.1, +5.2] (1323/346) | +0.1 [-4.0, +4.2] (478/163) | +4.6 [+1.2, +7.7] (845/183) | DISCOVERY ONLY (H2) (d > 0) |
| ASIA | BBW_TTM | 819 / 262 / 647 | -1.7 | -1.0 | -3.7 | agree - oppose | -0.7 [-4.0, +3.3] (819/262) | -1.5 [-5.4, +3.8] (304/102) | -0.2 [-5.0, +5.2] (515/160) | NO DIFFERENCE SHOWN |
| ASIA | BBW_TTM | 819 / 262 / 647 | -1.7 | -1.0 | -3.7 | agree - absent | +2.0 [-0.4, +4.3] (819/647) | +2.0 [-0.5, +5.3] (304/255) | +1.9 [-1.7, +5.3] (515/392) | NO DIFFERENCE SHOWN |
| ASIA | EMA | 1591 / 11 / 126 | -2.5 | +3.6 | -1.7 | agree - oppose | -6.1 [-15.3, +5.6] (1591/11) | -7.9 [-16.4, +4.8] (603/5) | -4.7 [-20.8, +14.8] (988/6) | NOT READABLE |
| ASIA | EMA | 1591 / 11 / 126 | -2.5 | +3.6 | -1.7 | agree - absent | -0.7 [-4.1, +2.8] (1591/126) | -2.7 [-6.1, +0.8] (603/53) | +0.6 [-4.6, +6.0] (988/73) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING | 311 / 362 / 1055 | -1.0 | -1.7 | -3.0 | agree - oppose | +0.6 [-2.9, +4.1] (311/362) | +3.5 [-1.4, +7.2] (194/130) | -1.1 [-5.7, +4.3] (117/232) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING | 311 / 362 / 1055 | -1.0 | -1.7 | -3.0 | agree - absent | +2.0 [-0.9, +4.6] (311/1055) | +2.6 [-1.2, +5.4] (194/337) | +1.5 [-2.3, +6.0] (117/718) | NO DIFFERENCE SHOWN |
| ASIA | OI | 184 / 29 / 1515 | -1.9 | -4.1 | -2.4 | agree - oppose | +2.2 [-5.0, +7.4] (184/29) | -6.9 [-19.4, +2.9] (68/8) | +6.1 [-2.2, +11.9] (116/21) | NOT READABLE |
| ASIA | OI | 184 / 29 / 1515 | -1.9 | -4.1 | -2.4 | agree - absent | +0.5 [-4.4, +4.7] (184/1515) | -1.1 [-6.0, +3.1] (68/585) | +1.4 [-6.0, +6.7] (116/930) | NO DIFFERENCE SHOWN |
| ASIA | OFI | 606 / 553 / 569 | -1.5 | -3.8 | -1.9 | agree - oppose | +2.4 [+0.5, +4.1] (606/553) | +2.0 [+0.5, +3.5] (282/200) | +2.8 [-0.2, +5.6] (324/353) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| ASIA | OFI | 606 / 553 / 569 | -1.5 | -3.8 | -1.9 | agree - absent | +0.4 [-1.9, +2.7] (606/569) | +0.9 [-1.3, +2.9] (282/179) | +0.4 [-2.9, +4.1] (324/390) | NO DIFFERENCE SHOWN |
| ASIA | CVD | 663 / 347 / 718 | -2.7 | -2.4 | -2.0 | agree - oppose | -0.3 [-2.6, +2.0] (663/347) | -1.9 [-4.8, +1.2] (254/134) | +0.6 [-2.3, +3.6] (409/213) | NO DIFFERENCE SHOWN |
| ASIA | CVD | 663 / 347 / 718 | -2.7 | -2.4 | -2.0 | agree - absent | -0.7 [-1.9, +0.6] (663/718) | -1.1 [-3.3, +1.0] (254/273) | -0.4 [-1.8, +1.3] (409/445) | NO DIFFERENCE SHOWN |
| ASIA | TFI | 950 / 625 / 153 | -2.2 | -2.2 | -4.2 | agree - oppose | -0.1 [-1.6, +1.5] (950/625) | -0.4 [-2.4, +2.1] (383/234) | +0.2 [-1.9, +2.4] (567/391) | NO DIFFERENCE SHOWN |
| ASIA | TFI | 950 / 625 / 153 | -2.2 | -2.2 | -4.2 | agree - absent | +1.9 [-0.3, +4.1] (950/153) | +1.2 [-2.2, +5.1] (383/44) | +2.5 [-0.4, +5.2] (567/109) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | 595 / 624 / 509 | -2.8 | -2.3 | -2.0 | agree - oppose | -0.6 [-2.9, +1.9] (595/624) | -1.3 [-4.7, +2.8] (227/230) | -0.1 [-3.0, +3.0] (368/394) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | 595 / 624 / 509 | -2.8 | -2.3 | -2.0 | agree - absent | -0.9 [-2.6, +0.8] (595/509) | -1.1 [-3.5, +1.4] (227/204) | -0.8 [-3.0, +1.5] (368/305) | NO DIFFERENCE SHOWN |
| ASIA | SPREAD | 0 / 1 / 1727 | n/a | +34.1 | -2.4 | agree - oppose | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| ASIA | SPREAD | 0 / 1 / 1727 | n/a | +34.1 | -2.4 | agree - absent | n/a [n/a, n/a] (0/1727) | n/a [n/a, n/a] (0/661) | n/a [n/a, n/a] (0/1066) | NOT READABLE |
| ASIA | EMA200 | 1530 / 198 / 0 | -2.3 | -3.2 | n/a | agree - oppose | +1.0 [-2.0, +3.8] (1530/198) | +3.7 [-0.6, +7.7] (593/68) | -0.4 [-4.2, +3.0] (937/130) | NO DIFFERENCE SHOWN |
| ASIA | EMA200 | 1530 / 198 / 0 | -2.3 | -3.2 | n/a | agree - absent | n/a [n/a, n/a] (1530/0) | n/a [n/a, n/a] (593/0) | n/a [n/a, n/a] (937/0) | NOT READABLE |
| ASIA | DONCHIAN | 969 / 37 / 722 | -2.4 | -7.8 | -2.0 | agree - oppose | +5.3 [-2.5, +15.5] (969/37) | -1.0 [-8.7, +4.1] (374/10) | +7.9 [-2.6, +20.2] (595/27) | NOT READABLE |
| ASIA | DONCHIAN | 969 / 37 / 722 | -2.4 | -7.8 | -2.0 | agree - absent | -0.5 [-3.0, +2.3] (969/722) | -1.2 [-6.3, +5.0] (374/277) | -0.0 [-2.3, +2.7] (595/445) | NO DIFFERENCE SHOWN |
| ASIA | OBV | 715 / 184 / 829 | -2.3 | -2.8 | -2.3 | agree - oppose | +0.5 [-3.2, +4.1] (715/184) | +0.3 [-5.1, +6.1] (270/77) | +0.6 [-4.6, +5.1] (445/107) | NO DIFFERENCE SHOWN |
| ASIA | OBV | 715 / 184 / 829 | -2.3 | -2.8 | -2.3 | agree - absent | -0.0 [-2.1, +1.8] (715/829) | -0.7 [-3.9, +3.1] (270/314) | +0.4 [-2.4, +2.5] (445/515) | NO DIFFERENCE SHOWN |
| ASIA | VPFR | 796 / 504 / 428 | -2.0 | -3.5 | -1.7 | agree - oppose | +1.5 [-1.2, +3.9] (796/504) | -0.2 [-4.0, +3.0] (334/206) | +2.8 [-1.2, +6.0] (462/298) | NO DIFFERENCE SHOWN |
| ASIA | VPFR | 796 / 504 / 428 | -2.0 | -3.5 | -1.7 | agree - absent | -0.3 [-2.6, +1.9] (796/428) | -0.6 [-4.2, +2.7] (334/121) | +0.2 [-2.8, +3.1] (462/307) | NO DIFFERENCE SHOWN |
| ASIA | REGIME_ALIGN | 533 / 9 / 1186 | -2.4 | +2.9 | -2.4 | agree - oppose | -5.3 [-18.8, +5.1] (533/9) | -1.3 [-13.6, +11.2] (188/2) | -6.1 [-33.0, +7.8] (345/7) | NOT READABLE |
| ASIA | REGIME_ALIGN | 533 / 9 / 1186 | -2.4 | +2.9 | -2.4 | agree - absent | -0.0 [-1.3, +1.3] (533/1186) | -0.9 [-2.2, +0.6] (188/471) | +0.4 [-1.4, +2.4] (345/715) | NO DIFFERENCE SHOWN |
| ASIA | TREND_STRUCTURE | 762 / 0 / 966 | -2.9 | n/a | -1.9 | agree - oppose | n/a [n/a, n/a] (762/0) | n/a [n/a, n/a] (273/0) | n/a [n/a, n/a] (489/0) | NOT READABLE |
| ASIA | TREND_STRUCTURE | 762 / 0 / 966 | -2.9 | n/a | -1.9 | agree - absent | -1.0 [-3.3, +1.1] (762/966) | +0.2 [-3.3, +3.2] (273/388) | -1.8 [-4.7, +1.0] (489/578) | NO DIFFERENCE SHOWN |

#### carried 24 h

| Session | Vote | n agree / oppose / absent | EV agree | EV oppose | EV absent | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | ROC | 3417 / 606 / 1394 | -3.8 | -0.6 | -3.5 | agree - oppose | -3.2 [-5.2, -0.9] (3417/606) | -2.0 [-4.6, +1.1] (1367/209) | -3.9 [-6.4, -0.8] (2050/397) | DISCOVERY ONLY (H2) (d < 0) |
| NY | ROC | 3417 / 606 / 1394 | -3.8 | -0.6 | -3.5 | agree - absent | -0.3 [-1.7, +1.0] (3417/1394) | +0.5 [-1.6, +2.5] (1367/584) | -0.9 [-2.7, +0.7] (2050/810) | NO DIFFERENCE SHOWN |
| NY | RSI | 3917 / 292 / 1208 | -3.8 | -1.1 | -2.5 | agree - oppose | -2.7 [-5.7, +0.2] (3917/292) | -6.0 [-9.3, -2.2] (1575/93) | -1.2 [-5.1, +2.3] (2342/199) | NO DIFFERENCE SHOWN |
| NY | RSI | 3917 / 292 / 1208 | -3.8 | -1.1 | -2.5 | agree - absent | -1.4 [-3.0, +0.3] (3917/1208) | -0.4 [-2.3, +1.2] (1575/492) | -2.0 [-4.2, +0.5] (2342/716) | NO DIFFERENCE SHOWN |
| NY | DMI | 5312 / 105 / 0 | -3.4 | -2.1 | n/a | agree - oppose | -1.3 [-4.3, +1.6] (5312/105) | -3.2 [-9.1, +2.4] (2129/31) | -0.4 [-4.0, +3.0] (3183/74) | NO DIFFERENCE SHOWN |
| NY | DMI | 5312 / 105 / 0 | -3.4 | -2.1 | n/a | agree - absent | n/a [n/a, n/a] (5312/0) | n/a [n/a, n/a] (2129/0) | n/a [n/a, n/a] (3183/0) | NOT READABLE |
| NY | ADX | 4356 / 0 / 1061 | -3.5 | n/a | -3.1 | agree - oppose | n/a [n/a, n/a] (4356/0) | n/a [n/a, n/a] (1798/0) | n/a [n/a, n/a] (2558/0) | NOT READABLE |
| NY | ADX | 4356 / 0 / 1061 | -3.5 | n/a | -3.1 | agree - absent | -0.4 [-2.2, +1.6] (4356/1061) | -1.5 [-4.1, +1.0] (1798/362) | +0.3 [-2.2, +3.0] (2558/699) | NO DIFFERENCE SHOWN |
| NY | VOL | 4 / 0 / 5413 | -7.3 | n/a | -3.4 | agree - oppose | n/a [n/a, n/a] (4/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (4/0) | NOT READABLE |
| NY | VOL | 4 / 0 / 5413 | -7.3 | n/a | -3.4 | agree - absent | -3.9 [-8.9, +8.9] (4/5413) | n/a [n/a, n/a] (0/2160) | -3.9 [-9.3, +9.1] (4/3253) | NOT READABLE |
| NY | VWAP | 4388 / 225 / 804 | -3.2 | -1.3 | -4.8 | agree - oppose | -2.0 [-5.5, +1.3] (4388/225) | +0.9 [-5.3, +4.4] (1789/57) | -2.9 [-7.1, +1.0] (2599/168) | NO DIFFERENCE SHOWN |
| NY | VWAP | 4388 / 225 / 804 | -3.2 | -1.3 | -4.8 | agree - absent | +1.6 [-0.4, +3.4] (4388/804) | +1.1 [-2.0, +4.2] (1789/314) | +1.8 [-0.7, +4.2] (2599/490) | NO DIFFERENCE SHOWN |
| NY | BBW_TTM | 2744 / 1023 / 1650 | -4.1 | -1.9 | -3.1 | agree - oppose | -2.2 [-3.8, -0.6] (2744/1023) | -1.9 [-4.3, +0.8] (1060/425) | -2.4 [-4.6, -0.2] (1684/598) | DISCOVERY ONLY (H2) (d < 0) |
| NY | BBW_TTM | 2744 / 1023 / 1650 | -4.1 | -1.9 | -3.1 | agree - absent | -1.0 [-2.4, +0.5] (2744/1650) | -0.6 [-2.3, +1.0] (1060/675) | -1.3 [-3.4, +1.1] (1684/975) | NO DIFFERENCE SHOWN |
| NY | EMA | 4840 / 91 / 486 | -3.4 | -1.8 | -3.9 | agree - oppose | -1.6 [-4.4, +1.2] (4840/91) | -3.2 [-9.0, +1.5] (1939/24) | -0.9 [-4.3, +2.7] (2901/67) | NOT READABLE |
| NY | EMA | 4840 / 91 / 486 | -3.4 | -1.8 | -3.9 | agree - absent | +0.5 [-1.7, +2.8] (4840/486) | -3.2 [-5.9, -0.4] (1939/197) | +3.1 [+0.5, +5.9] (2901/289) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| NY | FUNDING | 1456 / 1255 / 2706 | -4.3 | -2.3 | -3.4 | agree - oppose | -2.0 [-3.7, -0.3] (1456/1255) | -3.3 [-5.7, -1.1] (662/618) | -0.7 [-3.3, +1.7] (794/637) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| NY | FUNDING | 1456 / 1255 / 2706 | -4.3 | -2.3 | -3.4 | agree - absent | -0.9 [-3.2, +1.2] (1456/2706) | -0.8 [-3.8, +2.1] (662/880) | -0.7 [-4.0, +1.9] (794/1826) | NO DIFFERENCE SHOWN |
| NY | OI | 815 / 92 / 4510 | -4.3 | -0.1 | -3.3 | agree - oppose | -4.2 [-9.0, +0.2] (815/92) | -1.0 [-6.8, +3.6] (245/38) | -6.5 [-13.7, -0.4] (570/54) | NOT READABLE |
| NY | OI | 815 / 92 / 4510 | -4.3 | -0.1 | -3.3 | agree - absent | -1.0 [-3.2, +0.9] (815/4510) | -1.3 [-6.0, +2.8] (245/1877) | -0.8 [-3.2, +0.9] (570/2633) | NO DIFFERENCE SHOWN |
| NY | OFI | 1808 / 1527 / 2082 | -2.5 | -4.1 | -3.6 | agree - oppose | +1.6 [+0.3, +2.8] (1808/1527) | +2.5 [+0.5, +4.2] (795/608) | +0.9 [-0.8, +2.5] (1013/919) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | OFI | 1808 / 1527 / 2082 | -2.5 | -4.1 | -3.6 | agree - absent | +1.1 [-0.2, +2.5] (1808/2082) | +1.9 [+0.5, +3.3] (795/757) | +0.5 [-1.2, +2.5] (1013/1325) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | CVD | 2194 / 898 / 2325 | -3.0 | -4.4 | -3.3 | agree - oppose | +1.4 [-0.2, +2.9] (2194/898) | +2.1 [-0.7, +4.7] (880/340) | +1.0 [-1.0, +2.8] (1314/558) | NO DIFFERENCE SHOWN |
| NY | CVD | 2194 / 898 / 2325 | -3.0 | -4.4 | -3.3 | agree - absent | +0.3 [-0.9, +1.6] (2194/2325) | +0.5 [-0.9, +2.2] (880/940) | +0.2 [-1.7, +2.0] (1314/1385) | NO DIFFERENCE SHOWN |
| NY | TFI | 3013 / 1908 / 496 | -3.1 | -3.9 | -3.4 | agree - oppose | +0.8 [-0.1, +1.6] (3013/1908) | +1.8 [+0.4, +3.1] (1248/762) | +0.1 [-0.9, +1.0] (1765/1146) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | TFI | 3013 / 1908 / 496 | -3.1 | -3.9 | -3.4 | agree - absent | +0.3 [-1.0, +1.7] (3013/496) | +0.5 [-2.2, +3.1] (1248/150) | +0.2 [-1.5, +1.8] (1765/346) | NO DIFFERENCE SHOWN |
| NY | MICROCVD | 1895 / 1912 / 1610 | -3.2 | -3.3 | -3.8 | agree - oppose | +0.1 [-1.4, +1.5] (1895/1912) | +0.8 [-1.0, +2.4] (767/742) | -0.4 [-2.5, +1.5] (1128/1170) | NO DIFFERENCE SHOWN |
| NY | MICROCVD | 1895 / 1912 / 1610 | -3.2 | -3.3 | -3.8 | agree - absent | +0.6 [-0.7, +1.9] (1895/1610) | +0.3 [-1.6, +2.2] (767/651) | +0.8 [-0.9, +2.4] (1128/959) | NO DIFFERENCE SHOWN |
| NY | SPREAD | 0 / 1 / 5416 | n/a | -130.3 | -3.4 | agree - oppose | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| NY | SPREAD | 0 / 1 / 5416 | n/a | -130.3 | -3.4 | agree - absent | n/a [n/a, n/a] (0/5416) | n/a [n/a, n/a] (0/2160) | n/a [n/a, n/a] (0/3256) | NOT READABLE |
| NY | EMA200 | 4604 / 813 / 0 | -3.3 | -3.8 | n/a | agree - oppose | +0.4 [-1.6, +2.4] (4604/813) | -2.4 [-4.6, +0.4] (1839/321) | +2.3 [-0.2, +4.7] (2765/492) | NO DIFFERENCE SHOWN |
| NY | EMA200 | 4604 / 813 / 0 | -3.3 | -3.8 | n/a | agree - absent | n/a [n/a, n/a] (4604/0) | n/a [n/a, n/a] (1839/0) | n/a [n/a, n/a] (2765/0) | NOT READABLE |
| NY | DONCHIAN | 3233 / 200 / 1984 | -4.2 | -1.4 | -2.4 | agree - oppose | -2.8 [-5.8, +0.3] (3233/200) | -6.1 [-10.1, -1.5] (1370/68) | -1.2 [-4.6, +2.6] (1863/132) | NO DIFFERENCE SHOWN |
| NY | DONCHIAN | 3233 / 200 / 1984 | -4.2 | -1.4 | -2.4 | agree - absent | -1.8 [-3.8, +0.1] (3233/1984) | -0.5 [-2.3, +1.2] (1370/722) | -2.6 [-5.4, +0.3] (1863/1262) | NO DIFFERENCE SHOWN |
| NY | OBV | 3179 / 524 / 1714 | -3.3 | -3.6 | -3.5 | agree - oppose | +0.3 [-1.8, +2.8] (3179/524) | -0.2 [-3.2, +3.0] (1304/262) | +0.9 [-2.0, +4.7] (1875/262) | NO DIFFERENCE SHOWN |
| NY | OBV | 3179 / 524 / 1714 | -3.3 | -3.6 | -3.5 | agree - absent | +0.2 [-1.5, +2.0] (3179/1714) | -0.1 [-2.5, +2.5] (1304/594) | +0.4 [-1.8, +2.7] (1875/1120) | NO DIFFERENCE SHOWN |
| NY | VPFR | 2285 / 2100 / 1032 | -3.3 | -3.3 | -3.8 | agree - oppose | -0.0 [-1.4, +1.4] (2285/2100) | +1.4 [-0.4, +3.0] (920/919) | -1.1 [-3.1, +1.1] (1365/1181) | NO DIFFERENCE SHOWN |
| NY | VPFR | 2285 / 2100 / 1032 | -3.3 | -3.3 | -3.8 | agree - absent | +0.5 [-1.2, +2.1] (2285/1032) | +0.5 [-2.0, +2.7] (920/321) | +0.4 [-1.7, +2.4] (1365/711) | NO DIFFERENCE SHOWN |
| NY | REGIME_ALIGN | 1726 / 97 / 3594 | -2.8 | -3.9 | -3.7 | agree - oppose | +1.1 [-1.8, +4.2] (1726/97) | -1.1 [-5.8, +4.1] (703/38) | +2.6 [-0.5, +6.3] (1023/59) | NOT READABLE |
| NY | REGIME_ALIGN | 1726 / 97 / 3594 | -2.8 | -3.9 | -3.7 | agree - absent | +0.9 [-0.5, +2.3] (1726/3594) | +0.4 [-1.7, +2.5] (703/1419) | +1.3 [-0.7, +3.1] (1023/2175) | NO DIFFERENCE SHOWN |
| NY | TREND_STRUCTURE | 2697 / 0 / 2720 | -3.0 | n/a | -3.8 | agree - oppose | n/a [n/a, n/a] (2697/0) | n/a [n/a, n/a] (1065/0) | n/a [n/a, n/a] (1632/0) | NOT READABLE |
| NY | TREND_STRUCTURE | 2697 / 0 / 2720 | -3.0 | n/a | -3.8 | agree - absent | +0.8 [-0.9, +2.7] (2697/2720) | -1.0 [-2.4, +0.3] (1065/1095) | +2.0 [-0.7, +4.8] (1632/1625) | NO DIFFERENCE SHOWN |
| LONDON | ROC | 1126 / 140 / 399 | -0.7 | -1.1 | -1.2 | agree - oppose | +0.4 [-5.4, +7.2] (1126/140) | -2.6 [-7.1, +2.1] (480/52) | +2.4 [-6.1, +12.5] (646/88) | NO DIFFERENCE SHOWN |
| LONDON | ROC | 1126 / 140 / 399 | -0.7 | -1.1 | -1.2 | agree - absent | +0.4 [-2.1, +3.0] (1126/399) | -1.0 [-4.0, +2.2] (480/222) | +1.5 [-2.3, +5.3] (646/177) | NO DIFFERENCE SHOWN |
| LONDON | RSI | 1287 / 51 / 327 | -0.6 | -1.5 | -2.1 | agree - oppose | +0.9 [-4.4, +6.2] (1287/51) | -1.1 [-6.4, +3.7] (574/21) | +2.4 [-6.1, +10.3] (713/30) | NOT READABLE |
| LONDON | RSI | 1287 / 51 / 327 | -0.6 | -1.5 | -2.1 | agree - absent | +1.5 [-2.6, +6.8] (1287/327) | -0.4 [-3.5, +2.5] (574/159) | +3.1 [-4.2, +12.8] (713/168) | NO DIFFERENCE SHOWN |
| LONDON | DMI | 1643 / 22 / 0 | -0.9 | -3.2 | n/a | agree - oppose | +2.4 [-4.3, +8.6] (1643/22) | +0.7 [-8.6, +7.8] (738/16) | +5.3 [-4.0, +18.4] (905/6) | NOT READABLE |
| LONDON | DMI | 1643 / 22 / 0 | -0.9 | -3.2 | n/a | agree - absent | n/a [n/a, n/a] (1643/0) | n/a [n/a, n/a] (738/0) | n/a [n/a, n/a] (905/0) | NOT READABLE |
| LONDON | ADX | 1410 / 0 / 255 | -0.7 | n/a | -1.7 | agree - oppose | n/a [n/a, n/a] (1410/0) | n/a [n/a, n/a] (626/0) | n/a [n/a, n/a] (784/0) | NOT READABLE |
| LONDON | ADX | 1410 / 0 / 255 | -0.7 | n/a | -1.7 | agree - absent | +1.0 [-1.6, +3.6] (1410/255) | +1.7 [-2.0, +5.1] (626/128) | +0.1 [-3.4, +3.6] (784/127) | NO DIFFERENCE SHOWN |
| LONDON | VOL | 1 / 0 / 1664 | -12.7 | n/a | -0.9 | agree - oppose | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| LONDON | VOL | 1 / 0 / 1664 | -12.7 | n/a | -0.9 | agree - absent | -11.8 [-13.7, -9.8] (1/1664) | -10.9 [-13.0, -8.7] (1/753) | n/a [n/a, n/a] (0/911) | NOT READABLE |
| LONDON | VWAP | 1344 / 94 / 227 | -1.2 | -8.4 | +3.9 | agree - oppose | +7.3 [-2.5, +17.3] (1344/94) | -0.5 [-6.3, +5.1] (576/52) | +16.2 [-5.4, +29.0] (768/42) | NOT READABLE |
| LONDON | VWAP | 1344 / 94 / 227 | -1.2 | -8.4 | +3.9 | agree - absent | -5.1 [-13.9, +0.7] (1344/227) | -4.3 [-10.4, +1.2] (576/126) | -6.8 [-21.8, +4.9] (768/101) | NO DIFFERENCE SHOWN |
| LONDON | BBW_TTM | 898 / 213 / 554 | -0.2 | -1.8 | -1.7 | agree - oppose | +1.7 [-2.9, +7.2] (898/213) | -0.6 [-4.2, +3.4] (404/108) | +3.6 [-4.6, +14.0] (494/105) | NO DIFFERENCE SHOWN |
| LONDON | BBW_TTM | 898 / 213 / 554 | -0.2 | -1.8 | -1.7 | agree - absent | +1.5 [-2.9, +6.9] (898/554) | +0.0 [-3.7, +3.8] (404/242) | +2.8 [-4.7, +11.7] (494/312) | NO DIFFERENCE SHOWN |
| LONDON | EMA | 1557 / 14 / 94 | -0.7 | -6.4 | -3.5 | agree - oppose | +5.7 [-3.2, +12.0] (1557/14) | +3.6 [-5.2, +8.9] (701/9) | +8.8 [-13.6, +18.0] (856/5) | NOT READABLE |
| LONDON | EMA | 1557 / 14 / 94 | -0.7 | -6.4 | -3.5 | agree - absent | +2.8 [-1.3, +6.8] (1557/94) | +4.2 [+0.1, +8.7] (701/44) | +1.6 [-5.7, +7.9] (856/50) | NOT READABLE |
| LONDON | FUNDING | 485 / 457 / 723 | -1.9 | +2.3 | -2.2 | agree - oppose | -4.2 [-9.3, +1.0] (485/457) | -2.4 [-9.1, +4.9] (197/200) | -5.7 [-12.7, +1.2] (288/257) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING | 485 / 457 / 723 | -1.9 | +2.3 | -2.2 | agree - absent | +0.3 [-3.3, +2.9] (485/723) | +1.7 [-1.2, +4.8] (197/357) | -1.0 [-7.4, +3.1] (288/366) | NO DIFFERENCE SHOWN |
| LONDON | OI | 176 / 34 / 1455 | +0.7 | -1.7 | -1.1 | agree - oppose | +2.4 [-14.0, +16.3] (176/34) | -14.2 [-21.0, -8.5] (39/7) | +6.9 [-13.0, +22.5] (137/27) | NOT READABLE |
| LONDON | OI | 176 / 34 / 1455 | +0.7 | -1.7 | -1.1 | agree - absent | +1.7 [-4.9, +8.6] (176/1455) | -4.2 [-10.4, +1.2] (39/708) | +3.0 [-4.9, +11.9] (137/747) | NO DIFFERENCE SHOWN |
| LONDON | OFI | 619 / 517 / 529 | -0.8 | -0.5 | -1.4 | agree - oppose | -0.2 [-3.0, +2.5] (619/517) | +2.3 [+0.1, +4.2] (292/253) | -2.6 [-7.5, +2.4] (327/264) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| LONDON | OFI | 619 / 517 / 529 | -0.8 | -0.5 | -1.4 | agree - absent | +0.6 [-1.9, +3.0] (619/529) | +1.4 [-1.0, +4.0] (292/209) | +0.1 [-4.1, +3.6] (327/320) | NO DIFFERENCE SHOWN |
| LONDON | CVD | 609 / 334 / 722 | -2.1 | +0.4 | -0.5 | agree - oppose | -2.5 [-6.8, +1.7] (609/334) | -1.4 [-5.1, +2.4] (287/147) | -3.3 [-10.3, +3.4] (322/187) | NO DIFFERENCE SHOWN |
| LONDON | CVD | 609 / 334 / 722 | -2.1 | +0.4 | -0.5 | agree - absent | -1.6 [-3.9, +0.7] (609/722) | -1.6 [-4.0, +0.9] (287/320) | -1.5 [-5.2, +2.2] (322/402) | NO DIFFERENCE SHOWN |
| LONDON | TFI | 918 / 596 / 151 | -1.3 | -0.9 | +1.7 | agree - oppose | -0.3 [-2.3, +1.6] (918/596) | -0.1 [-2.6, +2.4] (449/260) | -0.4 [-3.3, +2.6] (469/336) | NO DIFFERENCE SHOWN |
| LONDON | TFI | 918 / 596 / 151 | -1.3 | -0.9 | +1.7 | agree - absent | -3.0 [-5.6, -0.4] (918/151) | -4.7 [-8.9, -0.4] (449/45) | -1.8 [-4.7, +1.2] (469/106) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | 562 / 634 / 469 | -1.6 | +0.5 | -1.9 | agree - oppose | -2.1 [-5.7, +1.0] (562/634) | -0.5 [-3.2, +2.3] (267/277) | -3.4 [-9.3, +2.1] (295/357) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | 562 / 634 / 469 | -1.6 | +0.5 | -1.9 | agree - absent | +0.3 [-2.0, +2.5] (562/469) | +0.5 [-2.7, +3.6] (267/210) | +0.1 [-3.2, +3.1] (295/259) | NO DIFFERENCE SHOWN |
| LONDON | SPREAD | 0 / 2 / 1663 | n/a | -16.6 | -0.9 | agree - oppose | n/a [n/a, n/a] (0/2) | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| LONDON | SPREAD | 0 / 2 / 1663 | n/a | -16.6 | -0.9 | agree - absent | n/a [n/a, n/a] (0/1663) | n/a [n/a, n/a] (0/753) | n/a [n/a, n/a] (0/910) | NOT READABLE |
| LONDON | EMA200 | 1464 / 201 / 0 | -0.2 | -5.8 | n/a | agree - oppose | +5.6 [+0.7, +12.0] (1464/201) | +4.0 [-1.0, +8.3] (673/81) | +6.8 [-0.5, +17.9] (791/120) | NO DIFFERENCE SHOWN |
| LONDON | EMA200 | 1464 / 201 / 0 | -0.2 | -5.8 | n/a | agree - absent | n/a [n/a, n/a] (1464/0) | n/a [n/a, n/a] (673/0) | n/a [n/a, n/a] (791/0) | NOT READABLE |
| LONDON | DONCHIAN | 968 / 31 / 666 | -0.1 | -1.2 | -2.0 | agree - oppose | +1.1 [-7.1, +7.5] (968/31) | -3.5 [-18.3, +4.0] (453/9) | +3.8 [-7.7, +11.8] (515/22) | NOT READABLE |
| LONDON | DONCHIAN | 968 / 31 / 666 | -0.1 | -1.2 | -2.0 | agree - absent | +1.9 [-2.9, +8.1] (968/666) | +0.3 [-2.7, +3.5] (453/292) | +3.3 [-5.2, +14.3] (515/374) | NO DIFFERENCE SHOWN |
| LONDON | OBV | 841 / 78 / 746 | +0.2 | -6.1 | -1.5 | agree - oppose | +6.3 [-4.4, +18.5] (841/78) | +6.4 [-1.1, +10.4] (480/29) | +6.8 [-12.2, +26.4] (361/49) | NOT READABLE |
| LONDON | OBV | 841 / 78 / 746 | +0.2 | -6.1 | -1.5 | agree - absent | +1.7 [-2.3, +6.0] (841/746) | +2.6 [-2.1, +7.4] (480/245) | +2.0 [-4.4, +8.8] (361/501) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | 794 / 465 / 406 | -0.7 | -0.9 | -1.2 | agree - oppose | +0.1 [-3.2, +3.3] (794/465) | +1.8 [-1.8, +5.4] (370/221) | -1.3 [-6.3, +3.4] (424/244) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | 794 / 465 / 406 | -0.7 | -0.9 | -1.2 | agree - absent | +0.5 [-3.5, +5.8] (794/406) | -0.8 [-3.5, +2.1] (370/163) | +1.5 [-5.0, +10.3] (424/243) | NO DIFFERENCE SHOWN |
| LONDON | REGIME_ALIGN | 487 / 6 / 1172 | -1.9 | -11.9 | -0.4 | agree - oppose | +10.1 [+7.2, +13.4] (487/6) | n/a [n/a, n/a] (222/0) | +10.1 [+6.4, +14.0] (265/6) | NOT READABLE |
| LONDON | REGIME_ALIGN | 487 / 6 / 1172 | -1.9 | -11.9 | -0.4 | agree - absent | -1.4 [-3.9, +1.1] (487/1172) | -0.1 [-2.5, +2.4] (222/532) | -2.5 [-6.6, +1.6] (265/640) | NO DIFFERENCE SHOWN |
| LONDON | TREND_STRUCTURE | 830 / 0 / 835 | -0.7 | n/a | -1.1 | agree - oppose | n/a [n/a, n/a] (830/0) | n/a [n/a, n/a] (362/0) | n/a [n/a, n/a] (468/0) | NOT READABLE |
| LONDON | TREND_STRUCTURE | 830 / 0 / 835 | -0.7 | n/a | -1.1 | agree - absent | +0.4 [-5.1, +4.8] (830/835) | +1.8 [-2.4, +5.8] (362/392) | -0.8 [-9.8, +6.3] (468/443) | NO DIFFERENCE SHOWN |
| ASIA | ROC | 901 / 133 / 694 | -2.1 | -5.3 | -2.5 | agree - oppose | +3.2 [-2.4, +9.2] (901/133) | -3.2 [-9.1, +3.6] (334/47) | +6.7 [-0.2, +13.9] (567/86) | NO DIFFERENCE SHOWN |
| ASIA | ROC | 901 / 133 / 694 | -2.1 | -5.3 | -2.5 | agree - absent | +0.4 [-1.3, +2.2] (901/694) | -1.2 [-4.1, +1.9] (334/280) | +1.4 [-0.5, +3.4] (567/414) | NO DIFFERENCE SHOWN |
| ASIA | RSI | 1316 / 45 / 367 | -2.4 | -2.2 | -2.7 | agree - oppose | -0.3 [-6.9, +7.7] (1316/45) | -6.9 [-14.8, +2.1] (502/13) | +2.8 [-5.8, +13.3] (814/32) | NOT READABLE |
| ASIA | RSI | 1316 / 45 / 367 | -2.4 | -2.2 | -2.7 | agree - absent | +0.3 [-3.5, +4.8] (1316/367) | -4.7 [-7.9, -0.0] (502/146) | +3.6 [-1.5, +9.4] (814/221) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | DMI | 1713 / 15 / 0 | -2.6 | +5.0 | n/a | agree - oppose | -7.6 [-20.1, +4.2] (1713/15) | -4.5 [-19.9, +7.6] (656/5) | -9.0 [-26.7, +6.8] (1057/10) | NOT READABLE |
| ASIA | DMI | 1713 / 15 / 0 | -2.6 | +5.0 | n/a | agree - absent | n/a [n/a, n/a] (1713/0) | n/a [n/a, n/a] (656/0) | n/a [n/a, n/a] (1057/0) | NOT READABLE |
| ASIA | ADX | 1386 / 0 / 342 | -2.9 | n/a | -0.8 | agree - oppose | n/a [n/a, n/a] (1386/0) | n/a [n/a, n/a] (551/0) | n/a [n/a, n/a] (835/0) | NOT READABLE |
| ASIA | ADX | 1386 / 0 / 342 | -2.9 | n/a | -0.8 | agree - absent | -2.1 [-5.0, +1.0] (1386/342) | -0.3 [-4.4, +3.5] (551/110) | -2.8 [-6.5, +1.1] (835/232) | NO DIFFERENCE SHOWN |
| ASIA | VOL | 0 / 0 / 1728 | n/a | n/a | -2.5 | agree - oppose | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| ASIA | VOL | 0 / 0 / 1728 | n/a | n/a | -2.5 | agree - absent | n/a [n/a, n/a] (0/1728) | n/a [n/a, n/a] (0/661) | n/a [n/a, n/a] (0/1067) | NOT READABLE |
| ASIA | VWAP | 1323 / 59 / 346 | -2.1 | -1.6 | -4.1 | agree - oppose | -0.5 [-5.9, +4.8] (1323/59) | -8.8 [-16.6, +3.1] (478/20) | +3.7 [-1.5, +9.9] (845/39) | NOT READABLE |
| ASIA | VWAP | 1323 / 59 / 346 | -2.1 | -1.6 | -4.1 | agree - absent | +2.0 [-0.8, +4.8] (1323/346) | -0.9 [-5.6, +3.3] (478/163) | +4.1 [+0.2, +7.6] (845/183) | DISCOVERY ONLY (H2) (d > 0) |
| ASIA | BBW_TTM | 819 / 262 / 647 | -1.7 | -0.5 | -4.2 | agree - oppose | -1.2 [-5.0, +2.9] (819/262) | -2.5 [-6.6, +3.2] (304/102) | -0.5 [-5.8, +5.3] (515/160) | NO DIFFERENCE SHOWN |
| ASIA | BBW_TTM | 819 / 262 / 647 | -1.7 | -0.5 | -4.2 | agree - absent | +2.5 [-0.1, +5.4] (819/647) | +2.9 [+0.1, +6.3] (304/255) | +2.2 [-1.8, +6.5] (515/392) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| ASIA | EMA | 1591 / 11 / 126 | -2.5 | -2.4 | -1.8 | agree - oppose | -0.1 [-11.1, +11.5] (1591/11) | -8.1 [-16.8, +4.6] (603/5) | +6.3 [-13.0, +15.8] (988/6) | NOT READABLE |
| ASIA | EMA | 1591 / 11 / 126 | -2.5 | -2.4 | -1.8 | agree - absent | -0.7 [-4.4, +2.9] (1591/126) | -2.7 [-5.9, +0.6] (603/53) | +0.6 [-5.1, +6.4] (988/73) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING | 311 / 362 / 1055 | -1.1 | -1.2 | -3.4 | agree - oppose | +0.1 [-3.9, +3.7] (311/362) | +3.2 [-2.7, +7.4] (194/130) | -1.9 [-6.6, +3.7] (117/232) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING | 311 / 362 / 1055 | -1.1 | -1.2 | -3.4 | agree - absent | +2.3 [-1.3, +5.3] (311/1055) | +3.3 [-1.7, +6.5] (194/337) | +1.6 [-2.5, +6.3] (117/718) | NO DIFFERENCE SHOWN |
| ASIA | OI | 184 / 29 / 1515 | -1.6 | -6.4 | -2.5 | agree - oppose | +4.7 [-5.1, +11.7] (184/29) | -8.9 [-21.9, +1.6] (68/8) | +10.5 [-0.7, +17.8] (116/21) | NOT READABLE |
| ASIA | OI | 184 / 29 / 1515 | -1.6 | -6.4 | -2.5 | agree - absent | +0.9 [-4.5, +5.5] (184/1515) | -1.7 [-7.6, +3.0] (68/585) | +2.4 [-5.6, +8.1] (116/930) | NO DIFFERENCE SHOWN |
| ASIA | OFI | 606 / 553 / 569 | -1.5 | -4.5 | -1.7 | agree - oppose | +3.0 [+1.3, +4.8] (606/553) | +2.1 [+0.8, +3.5] (282/200) | +3.8 [+1.0, +6.6] (324/353) | CONFIRMED (d > 0) |
| ASIA | OFI | 606 / 553 / 569 | -1.5 | -4.5 | -1.7 | agree - absent | +0.2 [-2.1, +2.6] (606/569) | +1.5 [-0.9, +3.8] (282/179) | -0.1 [-3.4, +3.4] (324/390) | NO DIFFERENCE SHOWN |
| ASIA | CVD | 663 / 347 / 718 | -2.5 | -2.7 | -2.4 | agree - oppose | +0.2 [-2.1, +2.4] (663/347) | -1.9 [-5.0, +1.3] (254/134) | +1.5 [-1.4, +4.3] (409/213) | NO DIFFERENCE SHOWN |
| ASIA | CVD | 663 / 347 / 718 | -2.5 | -2.7 | -2.4 | agree - absent | -0.1 [-1.4, +1.2] (663/718) | -0.8 [-3.0, +1.4] (254/273) | +0.3 [-1.1, +2.0] (409/445) | NO DIFFERENCE SHOWN |
| ASIA | TFI | 950 / 625 / 153 | -2.2 | -2.7 | -4.0 | agree - oppose | +0.5 [-1.3, +2.5] (950/625) | +0.2 [-2.2, +3.2] (383/234) | +0.8 [-1.6, +3.6] (567/391) | NO DIFFERENCE SHOWN |
| ASIA | TFI | 950 / 625 / 153 | -2.2 | -2.7 | -4.0 | agree - absent | +1.8 [-0.6, +4.3] (950/153) | +1.1 [-2.0, +4.6] (383/44) | +2.4 [-0.9, +5.8] (567/109) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | 595 / 624 / 509 | -2.5 | -2.8 | -2.1 | agree - oppose | +0.2 [-2.4, +3.2] (595/624) | +0.2 [-3.8, +4.8] (227/230) | +0.3 [-2.9, +4.3] (368/394) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | 595 / 624 / 509 | -2.5 | -2.8 | -2.1 | agree - absent | -0.4 [-2.2, +1.4] (595/509) | +0.2 [-2.3, +2.8] (227/204) | -0.8 [-3.3, +1.7] (368/305) | NO DIFFERENCE SHOWN |
| ASIA | SPREAD | 0 / 1 / 1727 | n/a | +34.1 | -2.5 | agree - oppose | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| ASIA | SPREAD | 0 / 1 / 1727 | n/a | +34.1 | -2.5 | agree - absent | n/a [n/a, n/a] (0/1727) | n/a [n/a, n/a] (0/661) | n/a [n/a, n/a] (0/1066) | NOT READABLE |
| ASIA | EMA200 | 1530 / 198 / 0 | -2.4 | -2.9 | n/a | agree - oppose | +0.5 [-2.6, +3.5] (1530/198) | +3.7 [-0.3, +7.1] (593/68) | -1.2 [-5.3, +2.8] (937/130) | NO DIFFERENCE SHOWN |
| ASIA | EMA200 | 1530 / 198 / 0 | -2.4 | -2.9 | n/a | agree - absent | n/a [n/a, n/a] (1530/0) | n/a [n/a, n/a] (593/0) | n/a [n/a, n/a] (937/0) | NOT READABLE |
| ASIA | DONCHIAN | 969 / 37 / 722 | -2.4 | -7.4 | -2.3 | agree - oppose | +5.0 [-3.1, +15.4] (969/37) | -3.0 [-9.7, +3.7] (374/10) | +8.3 [-2.7, +21.3] (595/27) | NOT READABLE |
| ASIA | DONCHIAN | 969 / 37 / 722 | -2.4 | -7.4 | -2.3 | agree - absent | -0.1 [-3.1, +3.2] (969/722) | -1.4 [-6.9, +4.9] (374/277) | +0.8 [-2.3, +4.6] (595/445) | NO DIFFERENCE SHOWN |
| ASIA | OBV | 715 / 184 / 829 | -2.7 | -2.8 | -2.2 | agree - oppose | +0.1 [-3.4, +3.3] (715/184) | +0.6 [-3.8, +5.5] (270/77) | -0.3 [-5.7, +4.2] (445/107) | NO DIFFERENCE SHOWN |
| ASIA | OBV | 715 / 184 / 829 | -2.7 | -2.8 | -2.2 | agree - absent | -0.4 [-2.7, +1.7] (715/829) | -1.1 [-4.5, +3.5] (270/314) | -0.1 [-3.0, +2.3] (445/515) | NO DIFFERENCE SHOWN |
| ASIA | VPFR | 796 / 504 / 428 | -2.6 | -3.4 | -1.3 | agree - oppose | +0.9 [-2.1, +3.5] (796/504) | -0.9 [-4.9, +2.7] (334/206) | +2.1 [-2.0, +5.4] (462/298) | NO DIFFERENCE SHOWN |
| ASIA | VPFR | 796 / 504 / 428 | -2.6 | -3.4 | -1.3 | agree - absent | -1.3 [-3.7, +1.1] (796/428) | -2.3 [-6.0, +1.6] (334/121) | -0.4 [-3.6, +2.6] (462/307) | NO DIFFERENCE SHOWN |
| ASIA | REGIME_ALIGN | 533 / 9 / 1186 | -2.1 | +0.2 | -2.7 | agree - oppose | -2.3 [-18.8, +9.7] (533/9) | -1.6 [-14.0, +11.0] (188/2) | -2.1 [-32.4, +11.5] (345/7) | NOT READABLE |
| ASIA | REGIME_ALIGN | 533 / 9 / 1186 | -2.1 | +0.2 | -2.7 | agree - absent | +0.5 [-1.1, +2.4] (533/1186) | -1.0 [-2.3, +0.6] (188/471) | +1.3 [-1.0, +4.1] (345/715) | NO DIFFERENCE SHOWN |
| ASIA | TREND_STRUCTURE | 762 / 0 / 966 | -3.4 | n/a | -1.8 | agree - oppose | n/a [n/a, n/a] (762/0) | n/a [n/a, n/a] (273/0) | n/a [n/a, n/a] (489/0) | NOT READABLE |
| ASIA | TREND_STRUCTURE | 762 / 0 / 966 | -3.4 | n/a | -1.8 | agree - absent | -1.6 [-4.9, +1.3] (762/966) | +0.1 [-4.6, +4.0] (273/388) | -2.6 [-7.3, +0.9] (489/578) | NO DIFFERENCE SHOWN |

### 4.3 Per vote: agree - oppose WITHIN tier (main window; weights = agree + oppose rows per tier)

| Session | Vote | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|
| NY | ROC | agree - oppose, within tier | -2.4 [-4.5, -0.1] (3417/606) | -0.1 [-4.2, +2.5] (1367/209) | -3.4 [-5.7, -0.7] (2050/397) | DISCOVERY ONLY (H2) (d < 0) |
| NY | RSI | agree - oppose, within tier | -3.9 [-6.2, -0.9] (3917/292) | -5.6 [-8.5, -1.8] (1575/93) | -3.2 [-5.9, +1.6] (2342/199) | NO DIFFERENCE SHOWN |
| NY | DMI | agree - oppose, within tier | -0.7 [-4.6, +2.5] (5312/105) | -3.6 [-10.6, +2.4] (2129/31) | +0.4 [-4.5, +3.7] (3183/74) | NO DIFFERENCE SHOWN |
| NY | ADX | agree - oppose, within tier | n/a [n/a, n/a] (4356/0) | n/a [n/a, n/a] (1798/0) | n/a [n/a, n/a] (2558/0) | NOT READABLE |
| NY | VOL | agree - oppose, within tier | n/a [n/a, n/a] (4/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (4/0) | NOT READABLE |
| NY | VWAP | agree - oppose, within tier | -0.7 [-3.6, +2.6] (4388/225) | +3.3 [-1.8, +6.0] (1789/57) | -2.2 [-5.5, +1.6] (2599/168) | NO DIFFERENCE SHOWN |
| NY | BBW_TTM | agree - oppose, within tier | -1.5 [-3.6, -0.2] (2744/1023) | -1.4 [-4.9, +0.9] (1060/425) | -2.3 [-4.0, -0.7] (1684/598) | DISCOVERY ONLY (H2) (d < 0) |
| NY | EMA | agree - oppose, within tier | -2.2 [-6.9, +4.5] (4840/91) | +2.4 [-6.4, +4.8] (1939/24) | -5.4 [-7.7, +1.1] (2901/67) | NOT READABLE |
| NY | FUNDING | agree - oppose, within tier | -1.4 [-3.0, +0.0] (1456/1255) | -2.6 [-5.0, -0.5] (662/618) | -0.3 [-2.5, +1.7] (794/637) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| NY | OI | agree - oppose, within tier | -5.2 [-11.5, +1.3] (815/92) | -5.8 [-9.6, +4.6] (245/38) | -5.2 [-14.2, +1.3] (570/54) | NOT READABLE |
| NY | OFI | agree - oppose, within tier | +1.9 [+0.7, +3.1] (1808/1527) | +2.8 [+0.7, +4.7] (795/608) | +1.3 [-0.3, +2.8] (1013/919) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | CVD | agree - oppose, within tier | +1.9 [-0.4, +4.1] (2194/898) | +3.8 [-0.0, +5.7] (880/340) | +1.5 [-1.5, +3.9] (1314/558) | NO DIFFERENCE SHOWN |
| NY | TFI | agree - oppose, within tier | +1.0 [+0.1, +1.9] (3013/1908) | +2.4 [+0.9, +3.8] (1248/762) | +0.2 [-0.7, +1.1] (1765/1146) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | MICROCVD | agree - oppose, within tier | +0.2 [-1.1, +1.6] (1895/1912) | +0.9 [-0.9, +2.6] (767/742) | -0.1 [-2.0, +1.7] (1128/1170) | NO DIFFERENCE SHOWN |
| NY | SPREAD | agree - oppose, within tier | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| NY | EMA200 | agree - oppose, within tier | +0.5 [-1.3, +2.5] (4604/813) | -1.3 [-4.0, +1.1] (1839/321) | +2.0 [-0.4, +4.5] (2765/492) | NO DIFFERENCE SHOWN |
| NY | DONCHIAN | agree - oppose, within tier | -2.0 [-5.9, +2.0] (3233/200) | -5.0 [-8.1, -1.4] (1370/68) | +2.1 [-3.4, +3.8] (1863/132) | NO DIFFERENCE SHOWN |
| NY | OBV | agree - oppose, within tier | -0.3 [-2.4, +2.0] (3179/524) | -0.5 [-3.4, +2.3] (1304/262) | -0.2 [-3.2, +4.3] (1875/262) | NO DIFFERENCE SHOWN |
| NY | VPFR | agree - oppose, within tier | -0.2 [-1.5, +1.0] (2285/2100) | +1.0 [-0.6, +2.4] (920/919) | -1.1 [-2.9, +0.8] (1365/1181) | NO DIFFERENCE SHOWN |
| NY | REGIME_ALIGN | agree - oppose, within tier | +6.1 [-1.3, +7.6] (1726/97) | -1.0 [-5.7, +4.1] (703/38) | +6.6 [-0.0, +8.3] (1023/59) | NOT READABLE |
| NY | TREND_STRUCTURE | agree - oppose, within tier | n/a [n/a, n/a] (2697/0) | n/a [n/a, n/a] (1065/0) | n/a [n/a, n/a] (1632/0) | NOT READABLE |
| LONDON | ROC | agree - oppose, within tier | -1.2 [-5.6, +3.5] (1126/140) | -3.0 [-8.3, +2.9] (480/52) | +0.6 [-7.5, +6.5] (646/88) | NO DIFFERENCE SHOWN |
| LONDON | RSI | agree - oppose, within tier | +4.4 [-4.1, +7.8] (1287/51) | -1.8 [-7.4, +3.1] (574/21) | +5.2 [-5.2, +10.5] (713/30) | NOT READABLE |
| LONDON | DMI | agree - oppose, within tier | +2.3 [-4.0, +8.1] (1643/22) | +0.5 [-8.8, +7.5] (738/16) | +5.1 [-3.5, +17.4] (905/6) | NOT READABLE |
| LONDON | ADX | agree - oppose, within tier | n/a [n/a, n/a] (1410/0) | n/a [n/a, n/a] (626/0) | n/a [n/a, n/a] (784/0) | NOT READABLE |
| LONDON | VOL | agree - oppose, within tier | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (1/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| LONDON | VWAP | agree - oppose, within tier | +4.3 [-2.3, +9.8] (1344/94) | -1.1 [-6.7, +5.1] (576/52) | +9.3 [-6.2, +15.6] (768/42) | NOT READABLE |
| LONDON | BBW_TTM | agree - oppose, within tier | +1.5 [-2.2, +5.3] (898/213) | -2.2 [-6.2, +2.6] (404/108) | +3.1 [-2.9, +9.8] (494/105) | NO DIFFERENCE SHOWN |
| LONDON | EMA | agree - oppose, within tier | +2.8 [-3.7, +10.1] (1557/14) | +1.2 [-6.0, +7.7] (701/9) | +7.7 [-13.4, +17.4] (856/5) | NOT READABLE |
| LONDON | FUNDING | agree - oppose, within tier | -3.8 [-8.9, +1.2] (485/457) | -0.9 [-7.7, +6.0] (197/200) | -6.2 [-13.1, +0.2] (288/257) | NO DIFFERENCE SHOWN |
| LONDON | OI | agree - oppose, within tier | +1.7 [-12.6, +12.3] (176/34) | -14.2 [-21.1, -5.3] (39/7) | +5.1 [-10.7, +16.8] (137/27) | NOT READABLE |
| LONDON | OFI | agree - oppose, within tier | +0.7 [-1.5, +2.7] (619/517) | +1.8 [-0.5, +3.9] (292/253) | -0.4 [-4.2, +3.4] (327/264) | NO DIFFERENCE SHOWN |
| LONDON | CVD | agree - oppose, within tier | -1.6 [-5.8, +2.1] (609/334) | -1.4 [-8.5, +2.5] (287/147) | -2.2 [-7.7, +3.5] (322/187) | NO DIFFERENCE SHOWN |
| LONDON | TFI | agree - oppose, within tier | -0.6 [-3.3, +1.8] (918/596) | -1.0 [-3.7, +1.9] (449/260) | +0.0 [-4.3, +4.0] (469/336) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | agree - oppose, within tier | -0.7 [-4.2, +2.6] (562/634) | -0.6 [-3.5, +2.5] (267/277) | -0.7 [-6.9, +5.4] (295/357) | NO DIFFERENCE SHOWN |
| LONDON | SPREAD | agree - oppose, within tier | n/a [n/a, n/a] (0/2) | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| LONDON | EMA200 | agree - oppose, within tier | +5.1 [+1.7, +8.9] (1464/201) | +4.5 [-0.5, +8.3] (673/81) | +5.8 [+1.1, +11.8] (791/120) | DISCOVERY ONLY (H2) (d > 0) |
| LONDON | DONCHIAN | agree - oppose, within tier | -10.6 [-15.0, +6.0] (968/31) | -12.2 [-18.5, +1.8] (453/9) | +3.3 [-9.4, +11.8] (515/22) | NOT READABLE |
| LONDON | OBV | agree - oppose, within tier | +2.9 [-5.2, +8.6] (841/78) | +6.9 [-1.8, +11.1] (480/29) | +1.1 [-14.1, +10.4] (361/49) | NOT READABLE |
| LONDON | VPFR | agree - oppose, within tier | +1.2 [-2.2, +4.5] (794/465) | +3.4 [-0.2, +6.8] (370/221) | -0.6 [-5.6, +4.4] (424/244) | NO DIFFERENCE SHOWN |
| LONDON | REGIME_ALIGN | agree - oppose, within tier | +9.5 [+5.6, +13.4] (487/6) | n/a [n/a, n/a] (222/0) | +8.8 [+3.4, +13.7] (265/6) | NOT READABLE |
| LONDON | TREND_STRUCTURE | agree - oppose, within tier | n/a [n/a, n/a] (830/0) | n/a [n/a, n/a] (362/0) | n/a [n/a, n/a] (468/0) | NOT READABLE |
| ASIA | ROC | agree - oppose, within tier | +0.7 [-2.1, +3.7] (901/133) | +1.7 [-2.9, +6.2] (334/47) | +1.6 [-1.4, +4.8] (567/86) | NO DIFFERENCE SHOWN |
| ASIA | RSI | agree - oppose, within tier | +1.2 [-4.9, +7.6] (1316/45) | -2.5 [-8.9, +4.5] (502/13) | +2.7 [-5.5, +11.5] (814/32) | NOT READABLE |
| ASIA | DMI | agree - oppose, within tier | -8.0 [-20.0, +3.3] (1713/15) | -3.3 [-19.1, +8.9] (656/5) | -10.4 [-27.1, +3.9] (1057/10) | NOT READABLE |
| ASIA | ADX | agree - oppose, within tier | n/a [n/a, n/a] (1386/0) | n/a [n/a, n/a] (551/0) | n/a [n/a, n/a] (835/0) | NOT READABLE |
| ASIA | VOL | agree - oppose, within tier | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/0) | NOT READABLE |
| ASIA | VWAP | agree - oppose, within tier | -2.8 [-7.2, +2.4] (1323/59) | -7.2 [-15.6, +4.6] (478/20) | -0.3 [-3.8, +4.4] (845/39) | NOT READABLE |
| ASIA | BBW_TTM | agree - oppose, within tier | -1.0 [-5.8, +3.6] (819/262) | -0.4 [-6.2, +5.8] (304/102) | -1.1 [-7.4, +4.8] (515/160) | NO DIFFERENCE SHOWN |
| ASIA | EMA | agree - oppose, within tier | -7.5 [-15.2, +6.0] (1591/11) | -8.5 [-17.3, +5.8] (603/5) | -5.0 [-20.3, +14.8] (988/6) | NOT READABLE |
| ASIA | FUNDING | agree - oppose, within tier | +0.7 [-2.8, +4.1] (311/362) | +3.8 [-1.1, +7.2] (194/130) | -1.3 [-5.7, +4.0] (117/232) | NO DIFFERENCE SHOWN |
| ASIA | OI | agree - oppose, within tier | +16.3 [-0.6, +20.0] (184/29) | -3.0 [-14.6, +5.5] (68/8) | +18.2 [+2.4, +22.8] (116/21) | NOT READABLE |
| ASIA | OFI | agree - oppose, within tier | +2.2 [+0.4, +4.0] (606/553) | +2.1 [+0.6, +3.5] (282/200) | +2.4 [-0.4, +5.2] (324/353) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| ASIA | CVD | agree - oppose, within tier | -1.5 [-3.7, +1.3] (663/347) | -0.1 [-3.8, +4.8] (254/134) | -1.7 [-4.4, +1.5] (409/213) | NO DIFFERENCE SHOWN |
| ASIA | TFI | agree - oppose, within tier | -0.0 [-1.8, +1.8] (950/625) | +0.7 [-1.4, +2.9] (383/234) | -0.4 [-2.9, +2.0] (567/391) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | agree - oppose, within tier | -0.6 [-3.1, +2.2] (595/624) | -0.8 [-4.2, +3.2] (227/230) | -0.6 [-4.0, +3.3] (368/394) | NO DIFFERENCE SHOWN |
| ASIA | SPREAD | agree - oppose, within tier | n/a [n/a, n/a] (0/1) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (0/1) | NOT READABLE |
| ASIA | EMA200 | agree - oppose, within tier | +1.3 [-1.6, +4.1] (1530/198) | +4.2 [-0.3, +8.2] (593/68) | -0.1 [-4.1, +3.2] (937/130) | NO DIFFERENCE SHOWN |
| ASIA | DONCHIAN | agree - oppose, within tier | +8.0 [-1.8, +20.7] (969/37) | -0.0 [-7.9, +5.2] (374/10) | +9.4 [-2.4, +23.5] (595/27) | NOT READABLE |
| ASIA | OBV | agree - oppose, within tier | +0.3 [-3.3, +3.7] (715/184) | +0.5 [-5.0, +6.6] (270/77) | +0.2 [-4.9, +4.3] (445/107) | NO DIFFERENCE SHOWN |
| ASIA | VPFR | agree - oppose, within tier | +1.6 [-1.2, +4.0] (796/504) | -0.1 [-3.9, +3.2] (334/206) | +2.8 [-1.3, +5.9] (462/298) | NO DIFFERENCE SHOWN |
| ASIA | REGIME_ALIGN | agree - oppose, within tier | -4.7 [-18.5, +6.0] (533/9) | -0.5 [-14.2, +12.9] (188/2) | -5.8 [-32.8, +7.6] (345/7) | NOT READABLE |
| ASIA | TREND_STRUCTURE | agree - oppose, within tier | n/a [n/a, n/a] (762/0) | n/a [n/a, n/a] (273/0) | n/a [n/a, n/a] (489/0) | NOT READABLE |

### 4.5 Which votes lift a row into MEDIUM (main window)

- Lifted: a MEDIUM row whose effective score, minus that vote's positive points on the trade side, falls below the MEDIUM floor. A row can be lifted by several votes (explanation sets, not first match).

| Session | Vote | MEDIUM rows lifted | % of MEDIUM | EV lifted | EV other MEDIUM | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | ROC | 506 | 32.6 | -3.4 | -4.6 | lifted - other MEDIUM | +1.2 [-0.6, +2.9] (506/1046) | +0.5 [-1.4, +2.4] (196/434) | +1.7 [-1.0, +4.2] (310/612) | NO DIFFERENCE SHOWN |
| NY | RSI | 570 | 36.7 | -3.5 | -4.6 | lifted - other MEDIUM | +1.1 [-0.4, +2.6] (570/982) | +0.1 [-1.8, +2.2] (216/414) | +1.7 [-0.4, +3.8] (354/568) | NO DIFFERENCE SHOWN |
| NY | DMI | 641 | 41.3 | -3.3 | -4.8 | lifted - other MEDIUM | +1.4 [+0.0, +2.9] (641/911) | -0.7 [-2.4, +1.1] (250/380) | +2.9 [+1.0, +4.8] (391/531) | DISCOVERY ONLY (H2) (d > 0) |
| NY | ADX | 490 | 31.6 | -3.7 | -4.4 | lifted - other MEDIUM | +0.8 [-0.5, +2.0] (490/1062) | -1.2 [-2.9, +0.5] (191/439) | +2.1 [+0.6, +3.6] (299/623) | DISCOVERY ONLY (H2) (d > 0) |
| NY | VOL | 1 | 0.1 | -11.0 | -4.2 | lifted - other MEDIUM | -6.8 [-8.0, -5.8] (1/1551) | n/a [n/a, n/a] (0/630) | -6.8 [-8.4, -5.2] (1/921) | NOT READABLE |
| NY | VWAP | 513 | 33.1 | -2.9 | -4.8 | lifted - other MEDIUM | +2.0 [+0.5, +3.4] (513/1039) | -0.6 [-2.1, +0.9] (201/429) | +3.7 [+1.7, +5.5] (312/610) | DISCOVERY ONLY (H2) (d > 0) |
| NY | BBW_TTM | 446 | 28.7 | -3.8 | -4.3 | lifted - other MEDIUM | +0.5 [-1.4, +2.5] (446/1106) | -1.2 [-3.3, +0.8] (172/458) | +1.6 [-1.1, +4.6] (274/648) | NO DIFFERENCE SHOWN |
| NY | EMA | 620 | 39.9 | -3.3 | -4.8 | lifted - other MEDIUM | +1.5 [+0.0, +3.0] (620/932) | -0.5 [-2.2, +1.4] (240/390) | +2.9 [+0.8, +4.9] (380/542) | DISCOVERY ONLY (H2) (d > 0) |
| NY | FUNDING | 86 | 5.5 | -3.1 | -4.3 | lifted - other MEDIUM | +1.2 [-3.4, +5.5] (86/1466) | -2.2 [-6.1, +1.8] (32/598) | +3.3 [-3.6, +8.8] (54/868) | NOT READABLE |
| NY | OI | 141 | 9.1 | -6.3 | -4.0 | lifted - other MEDIUM | -2.3 [-5.8, +1.6] (141/1411) | +0.6 [-5.9, +6.6] (50/580) | -3.9 [-7.8, +0.7] (91/831) | NO DIFFERENCE SHOWN |
| NY | OFI | 233 | 15.0 | -3.5 | -4.3 | lifted - other MEDIUM | +0.8 [-1.2, +2.8] (233/1319) | -0.2 [-2.6, +2.1] (100/530) | +1.5 [-1.5, +4.5] (133/789) | NO DIFFERENCE SHOWN |
| NY | CVD | 294 | 18.9 | -3.7 | -4.3 | lifted - other MEDIUM | +0.6 [-1.2, +2.4] (294/1258) | -2.1 [-5.0, +1.0] (118/512) | +2.5 [+1.0, +4.0] (176/746) | DISCOVERY ONLY (H2) (d > 0) |
| NY | TFI | 420 | 27.1 | -2.5 | -4.8 | lifted - other MEDIUM | +2.4 [+0.5, +4.2] (420/1132) | +0.5 [-1.5, +2.6] (173/457) | +3.7 [+1.0, +6.1] (247/675) | DISCOVERY ONLY (H2) (d > 0) |
| NY | MICROCVD | 147 | 9.5 | -1.4 | -4.5 | lifted - other MEDIUM | +3.1 [+0.5, +5.5] (147/1405) | +3.5 [-0.3, +6.9] (58/572) | +2.8 [-0.9, +6.0] (89/833) | NO DIFFERENCE SHOWN |
| NY | EMA200 | 559 | 36.0 | -3.2 | -4.8 | lifted - other MEDIUM | +1.6 [+0.1, +3.1] (559/993) | -0.5 [-2.5, +1.4] (218/412) | +3.0 [+1.0, +4.9] (341/581) | DISCOVERY ONLY (H2) (d > 0) |
| NY | DONCHIAN | 497 | 32.0 | -3.6 | -4.5 | lifted - other MEDIUM | +0.9 [-0.7, +2.5] (497/1055) | -0.3 [-2.4, +2.0] (196/434) | +1.7 [-0.4, +3.9] (301/621) | NO DIFFERENCE SHOWN |
| NY | OBV | 389 | 25.1 | -3.6 | -4.4 | lifted - other MEDIUM | +0.7 [-0.9, +2.4] (389/1163) | -0.3 [-3.0, +2.5] (153/477) | +1.4 [-0.4, +3.6] (236/686) | NO DIFFERENCE SHOWN |
| NY | VPFR | 267 | 17.2 | -2.7 | -4.5 | lifted - other MEDIUM | +1.8 [-0.3, +3.8] (267/1285) | -0.0 [-2.5, +2.8] (97/533) | +2.8 [+0.1, +5.5] (170/752) | DISCOVERY ONLY (H2) (d > 0) |
| NY | REGIME_ALIGN | 252 | 16.2 | -3.2 | -4.4 | lifted - other MEDIUM | +1.2 [-0.9, +3.2] (252/1300) | -1.6 [-4.8, +2.2] (100/530) | +3.0 [+0.9, +5.1] (152/770) | DISCOVERY ONLY (H2) (d > 0) |
| NY | TREND_STRUCTURE | 298 | 19.2 | -3.3 | -4.4 | lifted - other MEDIUM | +1.1 [-1.1, +3.2] (298/1254) | -2.1 [-4.1, +0.0] (117/513) | +3.2 [+0.3, +6.0] (181/741) | DISCOVERY ONLY (H2) (d > 0) |
| LONDON | ROC | 172 | 35.5 | -2.0 | -0.1 | lifted - other MEDIUM | -1.9 [-5.5, +1.7] (172/313) | -2.1 [-6.9, +2.8] (72/140) | -1.7 [-6.8, +3.5] (100/173) | NO DIFFERENCE SHOWN |
| LONDON | RSI | 198 | 40.8 | -1.8 | -0.1 | lifted - other MEDIUM | -1.6 [-5.2, +1.7] (198/287) | -1.4 [-5.6, +2.6] (93/119) | -1.9 [-7.5, +3.1] (105/168) | NO DIFFERENCE SHOWN |
| LONDON | DMI | 223 | 46.0 | -1.4 | -0.3 | lifted - other MEDIUM | -1.2 [-4.6, +2.0] (223/262) | -1.0 [-5.2, +2.6] (106/106) | -1.5 [-6.5, +3.2] (117/156) | NO DIFFERENCE SHOWN |
| LONDON | ADX | 190 | 39.2 | -1.1 | -0.6 | lifted - other MEDIUM | -0.6 [-4.1, +2.6] (190/295) | +0.2 [-5.1, +4.2] (86/126) | -1.2 [-6.0, +3.5] (104/169) | NO DIFFERENCE SHOWN |
| LONDON | VWAP | 176 | 36.3 | -2.8 | +0.3 | lifted - other MEDIUM | -3.1 [-6.1, -0.1] (176/309) | -3.1 [-7.2, +1.0] (79/133) | -3.1 [-7.6, +1.1] (97/176) | NO DIFFERENCE SHOWN |
| LONDON | BBW_TTM | 152 | 31.3 | -1.6 | -0.4 | lifted - other MEDIUM | -1.2 [-4.7, +2.3] (152/333) | -1.8 [-5.6, +2.3] (74/138) | -0.9 [-6.1, +4.7] (78/195) | NO DIFFERENCE SHOWN |
| LONDON | EMA | 215 | 44.3 | -1.4 | -0.3 | lifted - other MEDIUM | -1.1 [-4.5, +2.0] (215/270) | -0.5 [-4.7, +3.1] (102/110) | -1.8 [-6.6, +3.0] (113/160) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING | 43 | 8.9 | -2.3 | -0.7 | lifted - other MEDIUM | -1.6 [-7.2, +2.3] (43/442) | -1.1 [-10.3, +7.3] (14/198) | -1.6 [-11.7, +3.3] (29/244) | NOT READABLE |
| LONDON | OI | 29 | 6.0 | -2.3 | -0.7 | lifted - other MEDIUM | -1.6 [-6.9, +3.8] (29/456) | -2.8 [-14.7, +7.2] (5/207) | -0.9 [-6.9, +5.2] (24/249) | NOT READABLE |
| LONDON | OFI | 83 | 17.1 | +0.9 | -1.1 | lifted - other MEDIUM | +2.0 [-2.8, +6.1] (83/402) | +2.7 [-2.6, +6.8] (40/172) | +1.2 [-6.7, +8.4] (43/230) | NOT READABLE |
| LONDON | CVD | 100 | 20.6 | -1.2 | -0.7 | lifted - other MEDIUM | -0.5 [-3.9, +2.7] (100/385) | -4.9 [-9.1, -0.9] (52/160) | +3.5 [-1.5, +7.5] (48/225) | NO DIFFERENCE SHOWN |
| LONDON | TFI | 147 | 30.3 | -2.2 | -0.2 | lifted - other MEDIUM | -2.0 [-5.5, +1.2] (147/338) | -3.2 [-8.6, +2.2] (69/143) | -1.2 [-5.5, +3.0] (78/195) | NO DIFFERENCE SHOWN |
| LONDON | MICROCVD | 50 | 10.3 | -2.0 | -0.7 | lifted - other MEDIUM | -1.3 [-6.3, +3.1] (50/435) | +2.1 [-3.9, +7.2] (25/187) | -4.9 [-12.0, +2.1] (25/248) | NOT READABLE |
| LONDON | EMA200 | 200 | 41.2 | -1.1 | -0.6 | lifted - other MEDIUM | -0.5 [-4.1, +2.7] (200/285) | -0.0 [-4.6, +3.7] (94/118) | -1.1 [-6.6, +3.9] (106/167) | NO DIFFERENCE SHOWN |
| LONDON | DONCHIAN | 151 | 31.1 | -1.6 | -0.4 | lifted - other MEDIUM | -1.2 [-4.5, +2.0] (151/334) | +0.7 [-3.9, +5.2] (71/141) | -2.9 [-7.5, +1.4] (80/193) | NO DIFFERENCE SHOWN |
| LONDON | OBV | 116 | 23.9 | +0.3 | -1.1 | lifted - other MEDIUM | +1.5 [-3.4, +6.2] (116/369) | +1.8 [-3.8, +6.5] (70/142) | +0.4 [-7.6, +9.7] (46/227) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | 114 | 23.5 | -2.1 | -0.4 | lifted - other MEDIUM | -1.7 [-5.6, +2.1] (114/371) | +1.8 [-3.8, +7.1] (56/156) | -5.1 [-9.7, -0.5] (58/215) | NO DIFFERENCE SHOWN |
| LONDON | REGIME_ALIGN | 84 | 17.3 | -2.1 | -0.5 | lifted - other MEDIUM | -1.5 [-5.1, +1.7] (84/401) | -5.2 [-10.5, -0.6] (39/173) | +1.5 [-3.3, +5.7] (45/228) | NOT READABLE |
| LONDON | TREND_STRUCTURE | 106 | 21.9 | -1.0 | -0.7 | lifted - other MEDIUM | -0.3 [-4.5, +3.3] (106/379) | +1.2 [-5.6, +5.8] (52/160) | -1.8 [-7.3, +2.7] (54/219) | NO DIFFERENCE SHOWN |
| ASIA | ROC | 145 | 34.5 | -3.0 | -4.0 | lifted - other MEDIUM | +0.9 [-2.3, +4.0] (145/275) | -0.3 [-4.6, +4.1] (62/120) | +1.8 [-2.8, +6.1] (83/155) | NO DIFFERENCE SHOWN |
| ASIA | RSI | 191 | 45.5 | -3.0 | -4.2 | lifted - other MEDIUM | +1.2 [-2.8, +5.1] (191/229) | +0.1 [-3.3, +4.1] (85/97) | +2.1 [-4.8, +8.4] (106/132) | NO DIFFERENCE SHOWN |
| ASIA | DMI | 218 | 51.9 | -3.3 | -4.0 | lifted - other MEDIUM | +0.7 [-2.3, +3.8] (218/202) | +0.5 [-2.6, +4.2] (94/88) | +0.8 [-4.2, +5.6] (124/114) | NO DIFFERENCE SHOWN |
| ASIA | ADX | 172 | 41.0 | -3.5 | -3.8 | lifted - other MEDIUM | +0.3 [-3.6, +4.0] (172/248) | +0.6 [-3.4, +5.3] (73/109) | -0.0 [-6.3, +5.7] (99/139) | NO DIFFERENCE SHOWN |
| ASIA | VWAP | 158 | 37.6 | -3.9 | -3.5 | lifted - other MEDIUM | -0.4 [-3.3, +2.8] (158/262) | -2.6 [-6.4, +1.6] (63/119) | +0.9 [-3.4, +5.7] (95/143) | NO DIFFERENCE SHOWN |
| ASIA | BBW_TTM | 146 | 34.8 | -2.0 | -4.6 | lifted - other MEDIUM | +2.6 [-0.1, +5.4] (146/274) | +2.6 [-0.6, +5.9] (64/118) | +2.6 [-1.5, +7.1] (82/156) | NO DIFFERENCE SHOWN |
| ASIA | EMA | 198 | 47.1 | -3.5 | -3.8 | lifted - other MEDIUM | +0.3 [-3.1, +3.6] (198/222) | -1.0 [-4.2, +2.3] (79/103) | +0.8 [-4.7, +5.8] (119/119) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING | 25 | 6.0 | -6.9 | -3.4 | lifted - other MEDIUM | -3.5 [-13.7, +6.8] (25/395) | -1.7 [-8.9, +0.7] (15/167) | -4.6 [-23.7, +11.9] (10/228) | NOT READABLE |
| ASIA | OI | 48 | 11.4 | -6.6 | -3.3 | lifted - other MEDIUM | -3.3 [-8.6, +1.7] (48/372) | -2.0 [-9.5, +5.3] (21/161) | -4.3 [-12.8, +2.4] (27/211) | NOT READABLE |
| ASIA | OFI | 92 | 21.9 | -2.8 | -3.9 | lifted - other MEDIUM | +1.0 [-1.9, +4.5] (92/328) | +1.6 [-2.6, +6.2] (43/139) | +0.8 [-3.4, +6.3] (49/189) | NOT READABLE |
| ASIA | CVD | 99 | 23.6 | -4.7 | -3.3 | lifted - other MEDIUM | -1.4 [-4.6, +1.6] (99/321) | +0.6 [-3.4, +4.7] (49/133) | -2.7 [-7.4, +2.4] (50/188) | NOT READABLE |
| ASIA | TFI | 158 | 37.6 | -2.5 | -4.4 | lifted - other MEDIUM | +1.9 [-0.8, +4.6] (158/262) | +1.3 [-2.8, +5.8] (67/115) | +2.3 [-1.6, +5.9] (91/147) | NO DIFFERENCE SHOWN |
| ASIA | MICROCVD | 60 | 14.3 | -5.0 | -3.4 | lifted - other MEDIUM | -1.6 [-5.8, +2.4] (60/360) | +0.3 [-5.1, +5.3] (25/157) | -3.0 [-9.1, +2.9] (35/203) | NOT READABLE |
| ASIA | EMA200 | 199 | 47.4 | -2.8 | -4.4 | lifted - other MEDIUM | +1.6 [-1.6, +4.6] (199/221) | +0.8 [-2.8, +4.9] (88/94) | +2.3 [-2.6, +6.7] (111/127) | NO DIFFERENCE SHOWN |
| ASIA | DONCHIAN | 155 | 36.9 | -3.4 | -3.8 | lifted - other MEDIUM | +0.4 [-2.9, +3.7] (155/265) | -0.8 [-3.5, +1.8] (68/114) | +1.4 [-4.2, +6.8] (87/151) | NO DIFFERENCE SHOWN |
| ASIA | OBV | 99 | 23.6 | -1.4 | -4.3 | lifted - other MEDIUM | +2.9 [-0.5, +5.7] (99/321) | +1.1 [-5.2, +5.8] (41/141) | +4.2 [-0.2, +7.5] (58/180) | NOT READABLE |
| ASIA | VPFR | 95 | 22.6 | -4.0 | -3.5 | lifted - other MEDIUM | -0.5 [-4.4, +2.9] (95/325) | -0.3 [-6.3, +4.8] (47/135) | -0.1 [-6.3, +5.0] (48/190) | NOT READABLE |
| ASIA | REGIME_ALIGN | 79 | 18.8 | -3.3 | -3.7 | lifted - other MEDIUM | +0.4 [-2.7, +3.7] (79/341) | +1.7 [-2.9, +6.9] (31/151) | -0.7 [-4.9, +3.5] (48/190) | NOT READABLE |
| ASIA | TREND_STRUCTURE | 88 | 21.0 | -2.3 | -4.0 | lifted - other MEDIUM | +1.7 [-2.7, +5.7] (88/332) | +3.0 [-1.4, +7.8] (27/155) | +0.4 [-6.0, +5.7] (61/177) | NOT READABLE |

| Session | MEDIUM rows lifted by 0 / 1 / 2 / 3+ votes | Most common lift sets (top 5) |
|---|---|---|
| NY | 837 / 63 / 2 / 650 | {none} 837; {OI} 36; {TFI} 27; {ROC, RSI, DMI, ADX, VWAP, BBW_TTM, EMA, OFI, EMA200, DONCHIAN, OBV, TREND_STRUCTURE} 10; {ROC, RSI, DMI, ADX, VWAP, BBW_TTM, EMA, EMA200, DONCHIAN, OBV, TREND_STRUCTURE} 8 |
| LONDON | 245 / 17 / 0 / 223 | {none} 245; {TFI} 12; {ROC, RSI, DMI, ADX, VWAP, BBW_TTM, EMA, CVD, TFI, EMA200, DONCHIAN, REGIME_ALIGN, TREND_STRUCTURE} 5; {OI} 5; {ROC, RSI, DMI, ADX, VWAP, BBW_TTM, EMA, EMA200, DONCHIAN, OBV, VPFR, TREND_STRUCTURE} 4 |
| ASIA | 189 / 13 / 0 / 218 | {none} 189; {TFI} 7; {OI} 6; {ROC, RSI, DMI, ADX, BBW_TTM, EMA, CVD, EMA200, DONCHIAN, OBV, REGIME_ALIGN} 4; {RSI, DMI, ADX, VWAP, EMA, CVD, EMA200, DONCHIAN, OBV, VPFR, REGIME_ALIGN} 3 |

### 4.4 The VPFR score vote (priority): verified VPFR profiles only

- Rows: 8508 verified of 8810. Vote geometry (`Core/ScoringEngine_Calculate_Scoring.vb:457-458`, label producer `Core/Indicators_Structure.vb:171-185`): NEAR_HVN_SUPPORT (price just below the POC) and IN_LVN_BULL vote LONG; NEAR_HVN_RESIST (price at or just above the POC) and IN_LVN_BEAR vote SHORT.
- Verified rows where the recomputed label and the breakdown points disagree (must be 0): 0.

- Reproduces session 1's descriptive share (`docs/medium-tier-bug-hunt-2026-09-16.md` section 5): on verified population rows with a NEAR_HVN label, the vote opposes the verdict side on 2844 of 4440 (64.1 %).

| Session | Label | Side | Vote vs trade | n | Success % | Net EV main [95 % CI] | Net EV carried [95 % CI] |
|---|---|---|---|---|---|---|---|
| NY | NEAR_HVN_SUPPORT | LONG | agrees | 445 | 48.3 | +1.4 [-1.2, +3.7] | +1.8 [-1.3, +4.3] |
| NY | NEAR_HVN_SUPPORT | SHORT | opposes | 1085 | 42.1 | -3.2 [-4.5, -1.9] | -3.5 [-5.0, -2.2] |
| NY | NEAR_HVN_RESIST | LONG | opposes | 841 | 38.0 | -3.6 [-5.4, -1.8] | -3.7 [-5.7, -1.6] |
| NY | NEAR_HVN_RESIST | SHORT | agrees | 561 | 36.5 | -4.4 [-6.3, -2.4] | -4.4 [-6.5, -2.2] |
| NY | IN_LVN_BULL | LONG | agrees | 532 | 42.5 | -3.9 [-5.6, -2.2] | -3.9 [-5.6, -2.2] |
| NY | IN_LVN_BULL | SHORT | opposes | 39 (NOT READABLE) | 61.5 | +2.6 [-4.1, +6.5] | +1.3 [-5.9, +5.6] |
| NY | IN_LVN_BEAR | LONG | opposes | 25 (NOT READABLE) | 56.0 | +6.2 [-1.8, +23.2] | +12.7 [-2.7, +25.3] |
| NY | IN_LVN_BEAR | SHORT | agrees | 656 | 37.3 | -5.3 [-7.2, -3.4] | -5.5 [-7.4, -3.5] |
| NY | NEUTRAL | LONG | absent | 491 | 36.0 | -4.5 [-6.9, -2.8] | -3.1 [-6.8, -0.0] |
| NY | NEUTRAL | SHORT | absent | 508 | 42.3 | -4.3 [-6.4, -2.2] | -4.3 [-6.2, -2.3] |
| LONDON | NEAR_HVN_SUPPORT | LONG | agrees | 76 (NOT READABLE) | 52.6 | +0.5 [-5.0, +6.9] | +0.9 [-5.2, +7.2] |
| LONDON | NEAR_HVN_SUPPORT | SHORT | opposes | 270 | 39.3 | -3.1 [-5.8, -0.5] | -3.5 [-5.7, -1.3] |
| LONDON | NEAR_HVN_RESIST | LONG | opposes | 166 | 44.0 | -0.5 [-4.9, +4.2] | +1.3 [-3.7, +5.8] |
| LONDON | NEAR_HVN_RESIST | SHORT | agrees | 141 | 31.9 | -3.6 [-6.9, -0.7] | -4.1 [-7.6, -0.8] |
| LONDON | IN_LVN_BULL | LONG | agrees | 246 | 48.0 | +1.2 [-3.9, +6.1] | +1.8 [-3.7, +6.9] |
| LONDON | IN_LVN_BULL | SHORT | opposes | 10 (NOT READABLE) | 40.0 | +3.3 [-4.2, +22.6] | +30.1 [-3.0, +38.0] |
| LONDON | IN_LVN_BEAR | LONG | opposes | 5 (NOT READABLE) | 20.0 | -7.5 [-12.5, -1.4] | -10.2 [-23.6, -1.4] |
| LONDON | IN_LVN_BEAR | SHORT | agrees | 311 | 45.3 | -1.3 [-4.2, +1.4] | -1.2 [-4.6, +2.3] |
| LONDON | NEUTRAL | LONG | absent | 166 | 44.6 | -0.4 [-4.4, +3.4] | -1.3 [-8.0, +3.6] |
| LONDON | NEUTRAL | SHORT | absent | 238 | 42.9 | -1.4 [-5.2, +2.2] | -1.2 [-6.8, +4.0] |
| ASIA | NEAR_HVN_SUPPORT | LONG | agrees | 197 | 37.6 | -2.4 [-8.4, +4.4] | -2.8 [-8.8, +3.9] |
| ASIA | NEAR_HVN_SUPPORT | SHORT | opposes | 256 | 52.3 | -2.5 [-5.2, +0.4] | -2.6 [-5.4, +0.5] |
| ASIA | NEAR_HVN_RESIST | LONG | opposes | 226 | 43.4 | -4.3 [-6.4, -2.1] | -4.0 [-6.2, -1.7] |
| ASIA | NEAR_HVN_RESIST | SHORT | agrees | 176 | 45.5 | +0.8 [-2.7, +4.0] | -0.5 [-5.3, +4.4] |
| ASIA | IN_LVN_BULL | LONG | agrees | 207 | 51.2 | -2.6 [-5.2, -0.1] | -3.0 [-5.3, -0.6] |
| ASIA | IN_LVN_BULL | SHORT | opposes | 3 (NOT READABLE) | 0.0 | -21.9 [-45.2, -10.3] | -21.9 [-45.2, -10.3] |
| ASIA | IN_LVN_BEAR | LONG | opposes | 3 (NOT READABLE) | 66.7 | +7.0 [-15.1, +18.0] | +7.0 [-15.1, +18.0] |
| ASIA | IN_LVN_BEAR | SHORT | agrees | 204 | 48.0 | -3.7 [-6.6, -1.4] | -4.1 [-7.3, -1.5] |
| ASIA | NEUTRAL | LONG | absent | 199 | 49.7 | -2.8 [-5.1, -0.8] | -2.5 [-4.7, -0.5] |
| ASIA | NEUTRAL | SHORT | absent | 225 | 53.8 | -0.8 [-3.7, +1.7] | -0.3 [-3.5, +2.3] |

| Session | Family | Window | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|
| NY | NEAR_HVN | main | agree - oppose | +1.5 [-0.5, +3.4] (1006/1926) | +3.0 [+0.1, +5.5] (384/838) | +0.5 [-2.2, +3.0] (622/1088) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | NEAR_HVN | C24 | agree - oppose | +1.9 [-0.2, +4.0] (1006/1926) | +3.8 [+0.8, +6.3] (384/838) | +0.7 [-2.3, +3.4] (622/1088) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | NEAR_HVN | main | agree - oppose, within tier | +1.4 [-0.5, +3.3] (1006/1926) | +3.1 [+0.6, +5.3] (384/838) | +0.4 [-2.2, +2.9] (622/1088) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| NY | NEAR_HVN | main | agree - NEUTRAL label (no vote) | +2.5 [+0.2, +4.7] (1006/999) | +1.9 [-1.7, +4.9] (384/308) | +2.7 [-0.4, +5.5] (622/691) | NO DIFFERENCE SHOWN |
| NY | NEAR_HVN | main | oppose - NEUTRAL label (no vote) | +1.0 [-0.5, +2.6] (1926/999) | -1.1 [-3.2, +1.3] (838/308) | +2.2 [+0.3, +4.2] (1088/691) | DISCOVERY ONLY (H2) (d > 0) |
| NY | NEAR_HVN | main | agree - oppose, LONG trades only | +5.0 [+2.4, +7.2] (445/841) | +4.4 [+0.2, +7.5] (223/454) | +5.5 [+1.6, +8.7] (222/387) | CONFIRMED (d > 0) |
| NY | NEAR_HVN | main | agree - oppose, SHORT trades only | -1.3 [-3.3, +1.0] (561/1085) | +1.1 [-2.2, +4.9] (161/384) | -2.3 [-5.0, +0.6] (400/701) | NO DIFFERENCE SHOWN |
| NY | IN_LVN | main | agree - oppose | -8.7 [-14.2, -3.2] (1188/64) | -14.5 [-27.2, -4.0] (491/19) | -6.4 [-12.3, +0.8] (697/45) | NOT READABLE |
| NY | IN_LVN | C24 | agree - oppose | -10.6 [-16.9, -2.4] (1188/64) | -14.4 [-27.8, -3.8] (491/19) | -9.1 [-16.6, +3.7] (697/45) | NOT READABLE |
| NY | IN_LVN | main | agree - oppose, within tier | +3.1 [-17.3, +14.6] (1188/64) | -16.2 [-35.5, +1.5] (491/19) | +6.5 [-14.8, +20.2] (697/45) | NOT READABLE |
| NY | IN_LVN | main | agree - NEUTRAL label (no vote) | -0.3 [-1.9, +1.5] (1188/999) | -1.3 [-3.7, +1.3] (491/308) | +0.0 [-2.2, +2.4] (697/691) | NO DIFFERENCE SHOWN |
| NY | IN_LVN | main | oppose - NEUTRAL label (no vote) | +8.5 [+3.0, +14.0] (64/999) | +13.2 [+3.3, +26.3] (19/308) | +6.5 [-0.5, +12.4] (45/691) | NOT READABLE |
| NY | IN_LVN | main | agree - oppose, LONG trades only | -10.2 [-28.6, -1.5] (532/25) | -21.9 [-44.8, -5.3] (242/10) | -2.4 [-24.0, +4.6] (290/15) | NOT READABLE |
| NY | IN_LVN | main | agree - oppose, SHORT trades only | -7.9 [-12.5, -0.5] (656/39) | -6.2 [-18.4, +5.7] (249/9) | -8.7 [-13.9, +1.8] (407/30) | NOT READABLE |
| NY | ALL labels | main | agree - oppose | -0.3 [-1.6, +1.0] (2194/1990) | +0.9 [-0.6, +2.3] (875/857) | -1.1 [-2.9, +0.8] (1319/1133) | NO DIFFERENCE SHOWN |
| NY | ALL labels | C24 | agree - oppose | -0.1 [-1.6, +1.4] (2194/1990) | +1.4 [-0.3, +3.0] (875/857) | -1.1 [-3.1, +0.9] (1319/1133) | NO DIFFERENCE SHOWN |
| NY | ALL labels | main | agree - oppose, within tier | -0.2 [-1.6, +1.1] (2194/1990) | +1.0 [-0.5, +2.4] (875/857) | -1.0 [-2.9, +0.8] (1319/1133) | NO DIFFERENCE SHOWN |
| NY | ALL labels | main | agree - NEUTRAL label (no vote) | +1.0 [-0.3, +2.3] (2194/999) | +0.1 [-2.2, +2.3] (875/308) | +1.3 [-0.2, +3.0] (1319/691) | NO DIFFERENCE SHOWN |
| NY | ALL labels | main | oppose - NEUTRAL label (no vote) | +1.3 [-0.3, +2.8] (1990/999) | -0.8 [-2.9, +1.6] (857/308) | +2.4 [+0.5, +4.3] (1133/691) | DISCOVERY ONLY (H2) (d > 0) |
| NY | ALL labels | main | agree - oppose, LONG trades only | +1.8 [-0.1, +3.7] (977/866) | +1.9 [-1.0, +3.9] (465/464) | +1.7 [-1.0, +5.0] (512/402) | NO DIFFERENCE SHOWN |
| NY | ALL labels | main | agree - oppose, SHORT trades only | -1.9 [-3.8, -0.2] (1217/1124) | -0.2 [-2.0, +1.7] (410/393) | -2.8 [-5.3, -0.4] (807/731) | DISCOVERY ONLY (H2) (d < 0) |
| LONDON | NEAR_HVN | main | agree - oppose | -0.1 [-4.1, +3.8] (217/436) | +1.4 [-3.9, +6.0] (100/214) | -1.5 [-7.1, +4.4] (117/222) | NO DIFFERENCE SHOWN |
| LONDON | NEAR_HVN | C24 | agree - oppose | -0.7 [-4.7, +3.1] (217/436) | -0.3 [-5.4, +4.4] (100/214) | -1.2 [-7.3, +4.9] (117/222) | NO DIFFERENCE SHOWN |
| LONDON | NEAR_HVN | main | agree - oppose, within tier | -0.2 [-4.1, +3.8] (217/436) | +1.6 [-3.8, +6.4] (100/214) | -1.2 [-6.6, +4.7] (117/222) | NO DIFFERENCE SHOWN |
| LONDON | NEAR_HVN | main | agree - NEUTRAL label (no vote) | -1.2 [-5.4, +3.2] (217/404) | -2.3 [-7.3, +2.4] (100/163) | -0.4 [-6.7, +7.0] (117/241) | NO DIFFERENCE SHOWN |
| LONDON | NEAR_HVN | main | oppose - NEUTRAL label (no vote) | -1.1 [-4.5, +2.2] (436/404) | -3.8 [-7.2, -0.2] (214/163) | +1.2 [-4.4, +6.4] (222/241) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| LONDON | NEAR_HVN | main | agree - oppose, LONG trades only | +1.0 [-6.5, +8.3] (76/166) | +6.8 [-0.2, +12.5] (38/90) | -5.3 [-14.8, +7.2] (38/76) | NOT READABLE |
| LONDON | NEAR_HVN | main | agree - oppose, SHORT trades only | -0.5 [-4.8, +3.6] (141/270) | -1.8 [-7.7, +4.1] (62/124) | +0.4 [-5.1, +6.7] (79/146) | NO DIFFERENCE SHOWN |
| LONDON | IN_LVN | main | agree - oppose | +0.0 [-10.7, +9.5] (557/15) | +12.9 [+9.2, +16.5] (264/3) | -3.0 [-20.9, +4.5] (293/12) | NOT READABLE |
| LONDON | IN_LVN | C24 | agree - oppose | -16.5 [-32.4, +12.2] (557/15) | +12.9 [+9.2, +16.6] (264/3) | -23.5 [-35.8, +9.6] (293/12) | NOT READABLE |
| LONDON | IN_LVN | main | agree - oppose, within tier | +0.3 [-11.9, +9.5] (557/15) | +13.1 [+9.6, +16.7] (264/3) | -8.9 [-20.9, +3.1] (293/12) | NOT READABLE |
| LONDON | IN_LVN | main | agree - NEUTRAL label (no vote) | +0.8 [-2.6, +4.6] (557/404) | -0.2 [-3.4, +2.8] (264/163) | +1.5 [-4.3, +7.6] (293/241) | NO DIFFERENCE SHOWN |
| LONDON | IN_LVN | main | oppose - NEUTRAL label (no vote) | +0.7 [-8.3, +10.6] (15/404) | -13.1 [-16.9, -10.0] (3/163) | +4.4 [-3.4, +22.7] (12/241) | NOT READABLE |
| LONDON | IN_LVN | main | agree - oppose, LONG trades only | +8.7 [-0.1, +16.9] (246/5) | +11.5 [+5.5, +17.5] (132/2) | +7.8 [-1.8, +18.5] (114/3) | NOT READABLE |
| LONDON | IN_LVN | main | agree - oppose, SHORT trades only | -4.7 [-33.9, +11.6] (311/10) | +15.1 [+10.8, +18.0] (132/1) | -7.7 [-37.8, -0.3] (179/9) | NOT READABLE |
| LONDON | ALL labels | main | agree - oppose | +1.3 [-2.0, +4.5] (774/451) | +3.1 [-0.5, +6.5] (364/217) | -0.4 [-5.1, +4.7] (410/234) | NO DIFFERENCE SHOWN |
| LONDON | ALL labels | C24 | agree - oppose | +0.5 [-2.8, +3.6] (774/451) | +1.9 [-1.8, +5.6] (364/217) | -0.9 [-5.8, +3.9] (410/234) | NO DIFFERENCE SHOWN |
| LONDON | ALL labels | main | agree - oppose, within tier | +1.4 [-1.8, +4.5] (774/451) | +3.4 [-0.3, +6.8] (364/217) | -0.2 [-4.9, +4.7] (410/234) | NO DIFFERENCE SHOWN |
| LONDON | ALL labels | main | agree - NEUTRAL label (no vote) | +0.2 [-3.0, +3.8] (774/404) | -0.8 [-3.5, +2.2] (364/163) | +0.9 [-4.1, +6.8] (410/241) | NO DIFFERENCE SHOWN |
| LONDON | ALL labels | main | oppose - NEUTRAL label (no vote) | -1.1 [-4.4, +2.3] (451/404) | -3.9 [-7.4, -0.4] (217/163) | +1.4 [-4.2, +6.5] (234/241) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| LONDON | ALL labels | main | agree - oppose, LONG trades only | +1.8 [-3.9, +7.5] (322/171) | +3.8 [-0.5, +9.4] (170/92) | -0.7 [-9.3, +9.3] (152/79) | NO DIFFERENCE SHOWN |
| LONDON | ALL labels | main | agree - oppose, SHORT trades only | +0.8 [-2.3, +3.9] (452/280) | +2.4 [-2.3, +6.9] (194/125) | -0.5 [-4.4, +3.5] (258/155) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | agree - oppose | +2.4 [-1.7, +6.4] (373/482) | +0.1 [-5.8, +5.1] (153/200) | +4.0 [-1.7, +9.9] (220/282) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | C24 | agree - oppose | +1.6 [-2.8, +6.0] (373/482) | -1.3 [-7.0, +4.2] (153/200) | +3.5 [-2.8, +9.8] (220/282) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | agree - oppose, within tier | +2.4 [-1.7, +6.4] (373/482) | -0.0 [-6.0, +4.9] (153/200) | +4.1 [-1.5, +10.0] (220/282) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | agree - NEUTRAL label (no vote) | +0.8 [-2.7, +5.0] (373/424) | -0.1 [-5.1, +4.9] (153/118) | +1.7 [-3.1, +7.9] (220/306) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | oppose - NEUTRAL label (no vote) | -1.6 [-4.0, +1.0] (482/424) | -0.2 [-3.9, +3.1] (200/118) | -2.3 [-5.6, +1.4] (282/306) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | agree - oppose, LONG trades only | +1.9 [-4.8, +8.4] (197/226) | -3.6 [-11.4, +3.3] (70/83) | +4.8 [-3.6, +15.0] (127/143) | NO DIFFERENCE SHOWN |
| ASIA | NEAR_HVN | main | agree - oppose, SHORT trades only | +3.2 [-1.0, +6.6] (176/256) | +3.4 [-2.0, +8.3] (83/117) | +3.0 [-3.7, +7.6] (93/139) | NO DIFFERENCE SHOWN |
| ASIA | IN_LVN | main | agree - oppose | +4.3 [-20.0, +41.5] (411/6) | +11.6 [+8.0, +13.7] (178/1) | +3.1 [-21.6, +43.3] (233/5) | NOT READABLE |
| ASIA | IN_LVN | C24 | agree - oppose | +3.9 [-20.9, +41.2] (411/6) | +11.5 [+7.8, +13.8] (178/1) | +2.5 [-22.1, +42.5] (233/5) | NOT READABLE |
| ASIA | IN_LVN | main | agree - oppose, within tier | -2.6 [-20.9, +41.9] (411/6) | +13.4 [+7.6, +16.3] (178/1) | -2.5 [-23.6, +43.6] (233/5) | NOT READABLE |
| ASIA | IN_LVN | main | agree - NEUTRAL label (no vote) | -1.4 [-3.5, +0.6] (411/424) | -1.0 [-4.9, +2.6] (178/118) | -1.5 [-3.9, +1.0] (233/306) | NO DIFFERENCE SHOWN |
| ASIA | IN_LVN | main | oppose - NEUTRAL label (no vote) | -5.8 [-42.9, +19.1] (6/424) | -12.6 [-15.1, -9.2] (1/118) | -4.5 [-45.0, +20.2] (5/306) | NOT READABLE |
| ASIA | IN_LVN | main | agree - oppose, LONG trades only | -9.6 [-22.3, +14.6] (207/3) | +10.5 [+6.4, +14.8] (53/1) | -19.9 [-22.9, -17.8] (154/2) | NOT READABLE |
| ASIA | IN_LVN | main | agree - oppose, SHORT trades only | +18.2 [+4.6, +43.1] (204/3) | n/a [n/a, n/a] (125/0) | +17.1 [+3.0, +43.2] (79/3) | NOT READABLE |
| ASIA | ALL labels | main | agree - oppose | +1.3 [-1.5, +3.6] (784/488) | -0.3 [-4.3, +3.0] (331/201) | +2.4 [-1.5, +5.5] (453/287) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | C24 | agree - oppose | +0.7 [-2.2, +3.2] (784/488) | -1.0 [-5.1, +2.7] (331/201) | +1.8 [-2.3, +5.1] (453/287) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | main | agree - oppose, within tier | +1.3 [-1.5, +3.8] (784/488) | -0.2 [-4.3, +3.2] (331/201) | +2.4 [-1.4, +5.5] (453/287) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | main | agree - NEUTRAL label (no vote) | -0.4 [-2.6, +1.9] (784/424) | -0.6 [-4.2, +2.8] (331/118) | +0.1 [-3.0, +3.0] (453/306) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | main | oppose - NEUTRAL label (no vote) | -1.7 [-4.2, +1.0] (488/424) | -0.3 [-4.0, +3.2] (201/118) | -2.4 [-5.8, +1.3] (287/306) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | main | agree - oppose, LONG trades only | +1.6 [-2.2, +4.6] (404/229) | -2.1 [-7.9, +2.8] (123/84) | +3.2 [-1.1, +6.3] (281/145) | NO DIFFERENCE SHOWN |
| ASIA | ALL labels | main | agree - oppose, SHORT trades only | +1.1 [-3.0, +4.2] (380/259) | +0.5 [-4.7, +4.6] (208/117) | +1.3 [-5.2, +6.1] (172/142) | NO DIFFERENCE SHOWN |

#### 4.4.1 Tier lift by the VPFR vote

| Session | Rows whose tier falls one step without the agreeing VPFR vote | ... of which MEDIUM -> WEAK | ... STRONG -> MEDIUM | ... WEAK -> below WEAK | Rows whose tier rises one step without the opposing VPFR point (no vote on the trade side, vote on the other side does not change the trade side) |
|---|---|---|---|---|---|
| NY | 777 | 267 | 101 | 409 | n/a (a vote on the other side scores the other side only) |
| LONDON | 246 | 114 | 32 | 100 | n/a (a vote on the other side scores the other side only) |
| ASIA | 274 | 95 | 23 | 156 | n/a (a vote on the other side scores the other side only) |

## 5. D-4: mutations that cross a tier boundary

- Liquidation penalty: never fired (finding L-1 of docs/medium-tier-bug-hunt-2026-09-16.md): 0 rows.
- OFI momentum confirm and suppress: disabled in every era (`docs/medium-tier-bug-hunt-2026-09-16.md` section R.4 row 7): 0 rows.

| Session | Mutation | Applied delta from | Rows touched on the trade side | Crossed up into tier (from -> to: n) | Crossed down into tier (from -> to: n) |
|---|---|---|---|---|---|
| NY | BBW squeeze penalty, both sides | label points (exclusive with the TTM vote) | 500 | 0 | MEDIUM -> WEAK: 154; STRONG -> MEDIUM: 20 |
| NY | MicroCVD decel penalty | label points (exclusive with the ACCEL vote) | 704 | 0 | MEDIUM -> WEAK: 103; STRONG -> MEDIUM: 15 |
| NY | MicroCVD stall penalty | label points (FLAT: no vote) | 532 | 0 | MEDIUM -> WEAK: 84; STRONG -> MEDIUM: 3 |
| NY | CVD divergence penalty | label points (divergence sign excludes the same-side vote) | 12 | 0 | MEDIUM -> WEAK: 1 |
| NY | RSI divergence penalty | nominal -1 (label also carries the RSI vote) | 319 | 0 | MEDIUM -> WEAK: 67; STRONG -> MEDIUM: 19 |
| NY | Pass 2c alignment bonus | label points | 1726 | BELOW -> WEAK: 114; MEDIUM -> STRONG: 135; WEAK -> MEDIUM: 252 | 0 |
| NY | Pass 2c conflict penalty | label points | 96 | 0 | 0 |
| NY | Pass 2b OI x CVD bonus | nominal (label also carries the OI vote) | 311 | BELOW -> WEAK: 15; MEDIUM -> STRONG: 40; WEAK -> MEDIUM: 31 | 0 |
| NY | Pass 2b OI x CVD penalty | nominal (label also carries the OI vote) | 102 | 0 | MEDIUM -> WEAK: 12; STRONG -> MEDIUM: 1 |
| NY | aggressor-velocity upgrade | nominal (label also carries the TFI vote) | 173 | BELOW -> WEAK: 17; MEDIUM -> STRONG: 6; WEAK -> MEDIUM: 23 | 0 |
| NY | aggressor-velocity contra penalty | nominal (label also carries the TFI vote) | 2 | 0 | 0 |
| NY | spread WIDE penalty | label points | 2 | 0 | MEDIUM -> WEAK: 1 |
| NY | trend structure bonus | label points | 2697 | BELOW -> WEAK: 417; MEDIUM -> STRONG: 114; WEAK -> MEDIUM: 298 | 0 |
| NY | funding Step 3 + 3b modifiers | label points (the label carries only Step 3 and 3b) | 1887 | BELOW -> WEAK: 118; MEDIUM -> STRONG: 25; WEAK -> MEDIUM: 86 | MEDIUM -> WEAK: 342; STRONG -> MEDIUM: 120 |
| NY | Pass 2 partial upgrades (ROC, RSI, VWAP, OI, Donchian, Volume, OBV) | +1 per upgrade flag | 4731 | BELOW -> MEDIUM: 41; BELOW -> WEAK: 1461; MEDIUM -> STRONG: 269; WEAK -> MEDIUM: 1137; WEAK -> STRONG: 8 | 0 |
| NY | TRANSITIONAL ADX penalty (Step 4; the census) | logged RegimePenalty | 549 | 0 | MEDIUM -> WEAK: 220; STRONG -> MEDIUM: 66 |
| LONDON | BBW squeeze penalty, both sides | label points (exclusive with the TTM vote) | 103 | 0 | MEDIUM -> WEAK: 26; STRONG -> MEDIUM: 3 |
| LONDON | MicroCVD decel penalty | label points (exclusive with the ACCEL vote) | 242 | 0 | MEDIUM -> WEAK: 35; STRONG -> MEDIUM: 9 |
| LONDON | MicroCVD stall penalty | label points (FLAT: no vote) | 192 | 0 | MEDIUM -> WEAK: 31; STRONG -> MEDIUM: 2 |
| LONDON | CVD divergence penalty | label points (divergence sign excludes the same-side vote) | 2 | 0 | STRONG -> MEDIUM: 1 |
| LONDON | RSI divergence penalty | nominal -1 (label also carries the RSI vote) | 93 | 0 | MEDIUM -> WEAK: 12; STRONG -> MEDIUM: 6 |
| LONDON | Pass 2c alignment bonus | label points | 487 | BELOW -> WEAK: 22; MEDIUM -> STRONG: 43; WEAK -> MEDIUM: 84 | 0 |
| LONDON | Pass 2c conflict penalty | label points | 6 | 0 | 0 |
| LONDON | Pass 2b OI x CVD bonus | nominal (label also carries the OI vote) | 67 | BELOW -> WEAK: 3; MEDIUM -> STRONG: 13; WEAK -> MEDIUM: 8 | 0 |
| LONDON | Pass 2b OI x CVD penalty | nominal (label also carries the OI vote) | 35 | 0 | MEDIUM -> WEAK: 5 |
| LONDON | aggressor-velocity upgrade | nominal (label also carries the TFI vote) | 71 | BELOW -> WEAK: 7; MEDIUM -> STRONG: 4; WEAK -> MEDIUM: 15 | 0 |
| LONDON | aggressor-velocity contra penalty | nominal (label also carries the TFI vote) | 1 | 0 | 0 |
| LONDON | spread WIDE penalty | label points | 2 | 0 | MEDIUM -> WEAK: 1 |
| LONDON | trend structure bonus | label points | 830 | BELOW -> WEAK: 113; MEDIUM -> STRONG: 24; WEAK -> MEDIUM: 106 | 0 |
| LONDON | funding Step 3 + 3b modifiers | label points (the label carries only Step 3 and 3b) | 712 | BELOW -> WEAK: 24; MEDIUM -> STRONG: 11; WEAK -> MEDIUM: 43 | MEDIUM -> WEAK: 121; STRONG -> MEDIUM: 36 |
| LONDON | Pass 2 partial upgrades (ROC, RSI, VWAP, OI, Donchian, Volume, OBV) | +1 per upgrade flag | 1511 | BELOW -> MEDIUM: 7; BELOW -> WEAK: 451; MEDIUM -> STRONG: 107; WEAK -> MEDIUM: 363; WEAK -> STRONG: 3 | 0 |
| LONDON | TRANSITIONAL ADX penalty (Step 4; the census) | logged RegimePenalty | 152 | 0 | MEDIUM -> WEAK: 65; STRONG -> MEDIUM: 18 |
| ASIA | BBW squeeze penalty, both sides | label points (exclusive with the TTM vote) | 132 | 0 | MEDIUM -> WEAK: 36; STRONG -> MEDIUM: 1 |
| ASIA | MicroCVD decel penalty | label points (exclusive with the ACCEL vote) | 221 | 0 | MEDIUM -> WEAK: 35; STRONG -> MEDIUM: 4 |
| ASIA | MicroCVD stall penalty | label points (FLAT: no vote) | 211 | 0 | MEDIUM -> WEAK: 33 |
| ASIA | CVD divergence penalty | label points (divergence sign excludes the same-side vote) | 1 | 0 | 0 |
| ASIA | RSI divergence penalty | nominal -1 (label also carries the RSI vote) | 62 | 0 | MEDIUM -> WEAK: 11; STRONG -> MEDIUM: 1 |
| ASIA | Pass 2c alignment bonus | label points | 533 | BELOW -> WEAK: 50; MEDIUM -> STRONG: 33; WEAK -> MEDIUM: 79 | 0 |
| ASIA | Pass 2c conflict penalty | label points | 9 | 0 | 0 |
| ASIA | Pass 2b OI x CVD bonus | nominal (label also carries the OI vote) | 62 | BELOW -> WEAK: 4; MEDIUM -> STRONG: 11; WEAK -> MEDIUM: 8 | 0 |
| ASIA | Pass 2b OI x CVD penalty | nominal (label also carries the OI vote) | 25 | 0 | MEDIUM -> WEAK: 1 |
| ASIA | aggressor-velocity upgrade | nominal (label also carries the TFI vote) | 63 | BELOW -> WEAK: 7; MEDIUM -> STRONG: 6; WEAK -> MEDIUM: 11 | 0 |
| ASIA | aggressor-velocity contra penalty | nominal (label also carries the TFI vote) | 1 | 0 | 0 |
| ASIA | spread WIDE penalty | label points | 1 | 0 | 0 |
| ASIA | trend structure bonus | label points | 762 | BELOW -> WEAK: 162; MEDIUM -> STRONG: 15; WEAK -> MEDIUM: 88 | 0 |
| ASIA | funding Step 3 + 3b modifiers | label points (the label carries only Step 3 and 3b) | 486 | BELOW -> WEAK: 36; MEDIUM -> STRONG: 6; WEAK -> MEDIUM: 25 | MEDIUM -> WEAK: 107; STRONG -> MEDIUM: 23 |
| ASIA | Pass 2 partial upgrades (ROC, RSI, VWAP, OI, Donchian, Volume, OBV) | +1 per upgrade flag | 1447 | BELOW -> MEDIUM: 9; BELOW -> WEAK: 542; MEDIUM -> STRONG: 53; WEAK -> MEDIUM: 302; WEAK -> STRONG: 4 | 0 |
| ASIA | TRANSITIONAL ADX penalty (Step 4; the census) | logged RegimePenalty | 181 | 0 | MEDIUM -> WEAK: 75; STRONG -> MEDIUM: 11 |

Comparisons (main window). Ka: crossed rows vs untouched rows of the landing tier. Kb: crossed rows vs untouched rows of the origin tier. Only crossings with n >= 100 are labelled; the rest are NOT READABLE by construction.

| Session | Mutation | Crossing | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|
| NY | SQUEEZE_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -0.4 [-2.7, +1.9] (154/3069) | -0.6 [-5.0, +3.0] (67/1188) | -0.2 [-2.8, +2.4] (87/1881) | NO DIFFERENCE SHOWN |
| NY | SQUEEZE_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +0.7 [-1.8, +3.1] (154/1507) | +0.7 [-4.1, +4.9] (67/608) | +0.7 [-2.1, +3.3] (87/899) | NO DIFFERENCE SHOWN |
| NY | SQUEEZE_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +0.4 [-4.3, +5.4] (20/1507) | -0.1 [-6.9, +7.5] (9/608) | +0.8 [-6.0, +8.1] (11/899) | NOT READABLE |
| NY | SQUEEZE_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -0.6 [-6.0, +4.8] (20/341) | -1.1 [-8.4, +6.9] (9/136) | -0.3 [-8.3, +8.0] (11/205) | NOT READABLE |
| NY | DECEL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -0.4 [-2.9, +2.1] (103/2967) | +1.7 [-0.9, +4.0] (38/1179) | -1.5 [-5.2, +2.0] (65/1788) | NO DIFFERENCE SHOWN |
| NY | DECEL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +0.8 [-1.7, +3.3] (103/1409) | +3.0 [-0.3, +6.0] (38/586) | -0.5 [-3.7, +3.0] (65/823) | NO DIFFERENCE SHOWN |
| NY | DECEL_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -0.3 [-7.8, +7.2] (15/1409) | -13.3 [-16.4, -10.9] (3/586) | +3.0 [-5.0, +11.4] (12/823) | NOT READABLE |
| NY | DECEL_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -1.5 [-8.8, +6.0] (15/337) | -13.9 [-17.3, -10.6] (3/134) | +1.5 [-6.4, +10.0] (12/203) | NOT READABLE |
| NY | STALL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +0.8 [-2.6, +4.1] (84/3061) | +4.4 [+0.3, +8.2] (38/1211) | -2.1 [-6.7, +2.0] (46/1850) | NOT READABLE |
| NY | STALL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.9 [-1.4, +5.1] (84/1482) | +5.5 [+1.8, +8.9] (38/611) | -1.1 [-5.5, +3.2] (46/871) | NOT READABLE |
| NY | STALL_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -4.2 [-16.2, +10.2] (3/1482) | -15.9 [-17.4, -15.0] (1/611) | +1.7 [-7.6, +11.0] (2/871) | NOT READABLE |
| NY | STALL_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -5.1 [-16.9, +9.3] (3/342) | -16.6 [-18.4, -14.6] (1/137) | +0.7 [-9.2, +10.8] (2/205) | NOT READABLE |
| NY | CVD_DIV_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -12.5 [-13.3, -11.7] (1/3514) | -12.9 [-13.9, -11.6] (1/1388) | n/a [n/a, n/a] (0/2126) | NOT READABLE |
| NY | CVD_DIV_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -11.5 [-12.5, -10.3] (1/1550) | -11.6 [-12.7, -10.2] (1/629) | n/a [n/a, n/a] (0/921) | NOT READABLE |
| NY | RSI_DIV_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +1.3 [-1.8, +3.8] (67/3318) | +3.6 [+0.1, +7.1] (28/1297) | -0.5 [-5.5, +3.1] (39/2021) | NOT READABLE |
| NY | RSI_DIV_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +2.3 [-0.6, +5.1] (67/1454) | +4.9 [+0.9, +8.8] (28/582) | +0.5 [-3.9, +3.9] (39/872) | NOT READABLE |
| NY | RSI_DIV_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +2.3 [-7.4, +10.7] (19/1454) | +6.7 [-6.4, +15.8] (8/582) | -1.0 [-14.6, +11.1] (11/872) | NOT READABLE |
| NY | RSI_DIV_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +1.5 [-8.4, +9.8] (19/326) | +5.4 [-8.0, +14.6] (8/130) | -1.3 [-15.1, +10.9] (11/196) | NOT READABLE |
| NY | P2C_ALIGN | BELOW -> WEAK | Ka crossed - untouched WEAK | +1.5 [-1.1, +4.1] (114/2856) | +1.2 [-3.8, +6.1] (42/1137) | +1.7 [-1.3, +4.9] (72/1719) | NO DIFFERENCE SHOWN |
| NY | P2C_ALIGN | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +0.7 [-4.5, +6.2] (135/45) | +2.8 [-3.8, +8.6] (54/19) | -0.8 [-8.1, +8.4] (81/26) | NOT READABLE |
| NY | P2C_ALIGN | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +0.4 [-2.8, +3.3] (135/790) | +2.3 [-1.5, +6.1] (54/301) | -0.8 [-5.5, +3.4] (81/489) | NO DIFFERENCE SHOWN |
| NY | P2C_ALIGN | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +1.4 [-1.0, +3.8] (252/790) | -0.9 [-5.1, +3.6] (100/301) | +3.0 [+0.6, +5.4] (152/489) | DISCOVERY ONLY (H2) (d > 0) |
| NY | P2C_ALIGN | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +0.3 [-2.0, +2.6] (252/2856) | -2.4 [-5.9, +1.4] (100/1137) | +2.1 [-0.4, +4.8] (152/1719) | NO DIFFERENCE SHOWN |
| NY | OICVD_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | +0.1 [-9.0, +4.7] (15/3451) | -9.2 [-10.3, -8.0] (1/1382) | +1.0 [-8.7, +5.5] (14/2069) | NOT READABLE |
| NY | OICVD_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -5.9 [-13.1, +1.9] (40/219) | -4.6 [-13.3, +3.6] (17/95) | -6.8 [-16.2, +6.3] (23/124) | NOT READABLE |
| NY | OICVD_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -4.3 [-11.0, +2.9] (40/1436) | -1.7 [-9.9, +5.8] (17/588) | -6.3 [-15.2, +5.9] (23/848) | NOT READABLE |
| NY | OICVD_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -0.9 [-7.2, +4.9] (31/1436) | -2.9 [-12.1, +5.6] (9/588) | -0.2 [-8.7, +8.0] (22/848) | NOT READABLE |
| NY | OICVD_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -1.6 [-7.9, +3.9] (31/3451) | -4.3 [-13.1, +4.2] (9/1382) | -0.4 [-8.4, +7.1] (22/2069) | NOT READABLE |
| NY | OICVD_CONFLICT | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -1.5 [-10.7, +11.6] (12/3437) | +17.7 [+16.5, +18.7] (2/1372) | -5.2 [-12.5, +7.1] (10/2065) | NOT READABLE |
| NY | OICVD_CONFLICT | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -0.4 [-9.5, +12.0] (12/1536) | +19.0 [+17.6, +20.1] (2/626) | -4.3 [-11.7, +8.1] (10/910) | NOT READABLE |
| NY | OICVD_CONFLICT | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +29.8 [+28.9, +31.0] (1/1536) | n/a [n/a, n/a] (0/626) | +29.8 [+28.5, +31.6] (1/910) | NOT READABLE |
| NY | OICVD_CONFLICT | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +28.9 [+27.1, +31.0] (1/342) | n/a [n/a, n/a] (0/137) | +28.8 [+26.2, +32.3] (1/205) | NOT READABLE |
| NY | BURST_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | +2.5 [-2.0, +7.1] (17/3438) | +2.6 [-6.6, +9.9] (5/1350) | +2.5 [-3.6, +8.3] (12/2088) | NOT READABLE |
| NY | BURST_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +2.4 [-9.4, +11.2] (6/322) | +1.9 [-9.7, +13.5] (2/129) | +2.6 [-11.5, +11.2] (4/193) | NOT READABLE |
| NY | BURST_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +3.3 [-8.4, +11.9] (6/1484) | +2.5 [-8.6, +13.6] (2/604) | +3.6 [-10.8, +11.9] (4/880) | NOT READABLE |
| NY | BURST_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +1.7 [-2.9, +6.8] (23/1484) | +3.5 [-4.9, +11.8] (10/604) | +0.4 [-4.6, +6.9] (13/880) | NOT READABLE |
| NY | BURST_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +0.7 [-3.8, +5.8] (23/3438) | +2.2 [-5.4, +10.1] (10/1350) | -0.5 [-5.7, +6.1] (13/2088) | NOT READABLE |
| NY | SPREAD_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -127.1 [-128.1, -126.5] (1/3521) | n/a [n/a, n/a] (0/1392) | -126.9 [-128.4, -126.2] (1/2129) | NOT READABLE |
| NY | SPREAD_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -126.1 [-127.2, -125.1] (1/1552) | n/a [n/a, n/a] (0/630) | -126.0 [-127.7, -124.6] (1/922) | NOT READABLE |
| NY | TS_BONUS | BELOW -> WEAK | Ka crossed - untouched WEAK | +1.6 [-0.9, +4.5] (417/1852) | +0.5 [-1.9, +3.1] (162/724) | +2.4 [-1.5, +6.4] (255/1128) | NO DIFFERENCE SHOWN |
| NY | TS_BONUS | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -0.9 [-4.8, +3.1] (114/100) | -3.7 [-9.3, +2.0] (46/47) | +1.5 [-3.8, +6.9] (68/53) | NO DIFFERENCE SHOWN |
| NY | TS_BONUS | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -0.3 [-3.5, +2.9] (114/768) | -1.6 [-6.1, +2.6] (46/324) | +0.7 [-3.9, +5.1] (68/444) | NO DIFFERENCE SHOWN |
| NY | TS_BONUS | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +0.7 [-1.6, +3.1] (298/768) | -2.6 [-5.1, +0.2] (117/324) | +3.0 [-0.4, +6.2] (181/444) | NO DIFFERENCE SHOWN |
| NY | TS_BONUS | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +0.4 [-2.2, +2.9] (298/1852) | -2.9 [-4.9, -0.6] (117/724) | +2.5 [-1.2, +5.9] (181/1128) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| NY | FUNDING_STEP3_3B | BELOW -> WEAK | Ka crossed - untouched WEAK | +1.4 [-1.6, +4.5] (118/2205) | +3.5 [-2.0, +7.7] (49/774) | -0.0 [-3.3, +3.8] (69/1431) | NO DIFFERENCE SHOWN |
| NY | FUNDING_STEP3_3B | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +2.3 [-5.8, +8.5] (25/256) | +3.9 [-20.4, +12.6] (11/99) | +1.1 [-8.4, +9.1] (14/157) | NOT READABLE |
| NY | FUNDING_STEP3_3B | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +2.7 [-4.9, +8.3] (25/1069) | +4.2 [-20.3, +11.7] (11/394) | +1.6 [-7.3, +9.3] (14/675) | NOT READABLE |
| NY | FUNDING_STEP3_3B | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +1.3 [-1.4, +4.3] (342/2205) | +3.2 [-1.5, +7.4] (168/774) | -0.3 [-3.3, +3.3] (174/1431) | NO DIFFERENCE SHOWN |
| NY | FUNDING_STEP3_3B | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.6 [-1.1, +4.6] (342/1069) | +3.2 [-2.0, +7.7] (168/394) | +0.2 [-2.5, +3.5] (174/675) | NO DIFFERENCE SHOWN |
| NY | FUNDING_STEP3_3B | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +0.2 [-3.0, +3.5] (120/1069) | +3.1 [-2.3, +7.8] (58/394) | -2.5 [-7.2, +0.6] (62/675) | NO DIFFERENCE SHOWN |
| NY | FUNDING_STEP3_3B | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -0.3 [-3.9, +3.6] (120/256) | +2.9 [-3.0, +8.4] (58/99) | -3.0 [-7.0, +0.0] (62/157) | NO DIFFERENCE SHOWN |
| NY | FUNDING_STEP3_3B | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +1.1 [-3.7, +5.2] (86/1069) | -1.8 [-6.0, +2.4] (32/394) | +2.8 [-4.5, +8.4] (54/675) | NOT READABLE |
| NY | FUNDING_STEP3_3B | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +0.7 [-3.9, +5.0] (86/2205) | -1.8 [-5.5, +2.2] (32/774) | +2.3 [-5.0, +7.8] (54/1431) | NOT READABLE |
| NY | PASS2_UPGRADES | BELOW -> MEDIUM | Ka crossed - untouched MEDIUM | -4.3 [-10.0, +1.6] (41/45) | -6.7 [-14.3, +1.5] (15/17) | -3.0 [-10.5, +5.3] (26/28) | NOT READABLE |
| NY | PASS2_UPGRADES | BELOW -> WEAK | Ka crossed - untouched WEAK | -1.3 [-3.3, +0.9] (1461/634) | +0.6 [-1.6, +2.9] (588/231) | -2.4 [-5.2, +0.6] (873/403) | NO DIFFERENCE SHOWN |
| NY | PASS2_UPGRADES | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +1.2 [-14.5, +14.9] (269/7) | -16.5 [-18.8, -14.4] (109/1) | +4.7 [-10.6, +17.8] (160/6) | NOT READABLE |
| NY | PASS2_UPGRADES | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -2.6 [-6.5, +1.8] (269/45) | -2.6 [-9.2, +4.6] (109/17) | -2.5 [-7.5, +3.1] (160/28) | NOT READABLE |
| NY | PASS2_UPGRADES | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -3.9 [-7.8, +0.3] (1137/45) | -2.6 [-8.8, +4.1] (481/17) | -4.8 [-9.4, +0.6] (656/28) | NOT READABLE |
| NY | PASS2_UPGRADES | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -1.9 [-4.1, +0.6] (1137/634) | -0.6 [-2.9, +2.1] (481/231) | -2.7 [-5.8, +0.8] (656/403) | NO DIFFERENCE SHOWN |
| NY | PASS2_UPGRADES | WEAK -> STRONG | Ka crossed - untouched STRONG | -6.9 [-29.6, +12.1] (8/7) | -17.9 [-27.6, -13.1] (3/1) | -7.6 [-34.6, +24.2] (5/6) | NOT READABLE |
| NY | PASS2_UPGRADES | WEAK -> STRONG | Kb crossed - untouched WEAK | -8.7 [-27.6, +11.2] (8/634) | -1.9 [-13.1, +4.7] (3/231) | -12.7 [-41.9, +33.0] (5/403) | NOT READABLE |
| NY | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +0.2 [-2.6, +3.0] (220/3196) | +1.2 [-3.4, +6.6] (89/1271) | -0.5 [-4.0, +2.8] (131/1925) | NO DIFFERENCE SHOWN |
| NY | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.1 [-1.8, +4.1] (220/1344) | +2.3 [-2.4, +7.8] (89/547) | +0.2 [-3.3, +3.6] (131/797) | NO DIFFERENCE SHOWN |
| NY | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -1.4 [-5.1, +2.6] (66/1344) | +1.7 [-2.4, +5.6] (27/547) | -3.5 [-8.2, +2.8] (39/797) | NOT READABLE |
| NY | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -2.3 [-6.5, +2.1] (66/328) | +0.8 [-3.8, +5.1] (27/133) | -4.5 [-10.2, +2.5] (39/195) | NOT READABLE |
| LONDON | SQUEEZE_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -3.0 [-6.9, +0.7] (26/957) | +0.1 [-4.9, +4.6] (15/417) | -6.4 [-13.2, -1.3] (11/540) | NOT READABLE |
| LONDON | SQUEEZE_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -3.2 [-7.3, +0.5] (26/474) | -2.1 [-8.1, +3.2] (15/208) | -5.1 [-11.5, -0.2] (11/266) | NOT READABLE |
| LONDON | SQUEEZE_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -4.5 [-13.5, +11.9] (3/474) | +9.8 [+6.8, +12.8] (1/208) | -11.5 [-14.7, -8.5] (2/266) | NOT READABLE |
| LONDON | SQUEEZE_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -3.8 [-14.2, +14.0] (3/131) | +13.4 [+9.5, +16.5] (1/75) | -14.2 [-24.0, -6.3] (2/56) | NOT READABLE |
| LONDON | DECEL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +4.4 [-2.2, +10.7] (35/861) | +5.7 [-3.3, +12.9] (15/382) | +3.3 [-6.1, +13.1] (20/479) | NOT READABLE |
| LONDON | DECEL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +3.9 [-2.8, +10.2] (35/435) | +3.3 [-5.3, +10.8] (15/189) | +4.4 [-5.7, +14.3] (20/246) | NOT READABLE |
| LONDON | DECEL_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -1.4 [-16.9, +9.2] (9/435) | +4.4 [-17.3, +13.9] (4/189) | -6.0 [-31.2, +8.6] (5/246) | NOT READABLE |
| LONDON | DECEL_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -1.1 [-16.8, +10.0] (9/127) | +7.5 [-11.9, +17.0] (4/71) | -8.9 [-35.2, +8.3] (5/56) | NOT READABLE |
| LONDON | STALL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -0.1 [-5.7, +6.3] (31/877) | +4.5 [-2.7, +10.2] (12/406) | -3.2 [-9.8, +6.8] (19/471) | NOT READABLE |
| LONDON | STALL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -0.6 [-6.7, +5.8] (31/465) | +2.2 [-5.7, +9.1] (12/204) | -2.3 [-9.3, +7.6] (19/261) | NOT READABLE |
| LONDON | STALL_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -15.6 [-48.7, +17.6] (2/465) | n/a [n/a, n/a] (0/204) | -14.9 [-48.7, +19.1] (2/261) | NOT READABLE |
| LONDON | STALL_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -14.7 [-49.3, +19.4] (2/131) | n/a [n/a, n/a] (0/75) | -17.7 [-57.4, +19.2] (2/56) | NOT READABLE |
| LONDON | CVD_DIV_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +14.0 [+11.9, +16.1] (1/484) | n/a [n/a, n/a] (0/212) | +14.6 [+11.5, +17.3] (1/272) | NOT READABLE |
| LONDON | CVD_DIV_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +14.8 [+10.7, +19.4] (1/131) | n/a [n/a, n/a] (0/75) | +11.8 [+4.6, +20.5] (1/56) | NOT READABLE |
| LONDON | RSI_DIV_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -8.9 [-16.5, -2.1] (12/987) | -6.7 [-13.2, +0.2] (6/434) | -10.9 [-24.9, +2.7] (6/553) | NOT READABLE |
| LONDON | RSI_DIV_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -9.5 [-17.1, -2.9] (12/461) | -9.1 [-16.1, -2.1] (6/199) | -10.1 [-24.0, +3.2] (6/262) | NOT READABLE |
| LONDON | RSI_DIV_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +0.1 [-12.8, +11.1] (6/461) | +11.7 [+8.6, +14.8] (2/199) | -5.5 [-13.6, +0.3] (4/262) | NOT READABLE |
| LONDON | RSI_DIV_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +0.4 [-12.6, +10.3] (6/124) | +15.8 [+11.7, +18.1] (2/73) | -10.1 [-23.1, -1.1] (4/51) | NOT READABLE |
| LONDON | P2C_ALIGN | BELOW -> WEAK | Ka crossed - untouched WEAK | +0.0 [-8.1, +8.8] (22/886) | -0.4 [-9.0, +5.8] (12/401) | +1.1 [-13.6, +19.3] (10/485) | NOT READABLE |
| LONDON | P2C_ALIGN | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +8.7 [-2.2, +19.2] (43/24) | +5.9 [-5.2, +14.0] (27/11) | +15.5 [-6.4, +36.4] (16/13) | NOT READABLE |
| LONDON | P2C_ALIGN | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +2.6 [-5.0, +12.1] (43/268) | -4.1 [-10.6, +2.9] (27/120) | +12.9 [-2.9, +29.6] (16/148) | NOT READABLE |
| LONDON | P2C_ALIGN | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -1.5 [-5.2, +2.3] (84/268) | -5.1 [-10.4, -0.3] (39/120) | +1.5 [-3.7, +6.7] (45/148) | NOT READABLE |
| LONDON | P2C_ALIGN | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -1.1 [-4.5, +2.1] (84/886) | -1.8 [-5.8, +1.7] (39/401) | -0.4 [-5.7, +4.3] (45/485) | NOT READABLE |
| LONDON | OICVD_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | -15.4 [-23.6, -6.6] (3/1039) | -6.2 [-7.5, -3.8] (1/464) | -20.4 [-25.0, -15.7] (2/575) | NOT READABLE |
| LONDON | OICVD_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +23.1 [+4.1, +42.7] (13/96) | +19.2 [+14.7, +24.1] (1/65) | +25.2 [+1.4, +46.6] (12/31) | NOT READABLE |
| LONDON | OICVD_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +19.6 [+1.1, +39.2] (13/463) | +15.9 [+12.9, +18.9] (1/210) | +20.5 [-0.3, +40.6] (12/253) | NOT READABLE |
| LONDON | OICVD_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +0.6 [-13.2, +9.6] (8/463) | -13.9 [-16.4, -10.6] (1/210) | +3.2 [-12.8, +15.1] (7/253) | NOT READABLE |
| LONDON | OICVD_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +1.2 [-12.7, +10.5] (8/1039) | -11.5 [-13.3, -9.2] (1/464) | +2.3 [-13.5, +13.3] (7/575) | NOT READABLE |
| LONDON | OICVD_CONFLICT | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -8.0 [-24.6, +10.3] (5/1017) | +11.4 [+9.5, +13.7] (1/461) | -13.3 [-26.0, -7.5] (4/556) | NOT READABLE |
| LONDON | OICVD_CONFLICT | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -8.6 [-24.4, +9.0] (5/482) | +9.2 [+6.6, +12.4] (1/212) | -12.6 [-26.7, -5.4] (4/270) | NOT READABLE |
| LONDON | BURST_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | +3.3 [-6.8, +13.0] (7/1022) | -2.4 [-13.3, +14.2] (3/454) | +7.5 [-4.2, +18.5] (4/568) | NOT READABLE |
| LONDON | BURST_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -2.8 [-13.0, +7.9] (4/120) | -0.4 [-10.2, +10.4] (4/68) | n/a [n/a, n/a] (0/52) | NOT READABLE |
| LONDON | BURST_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -3.3 [-11.0, +6.5] (4/452) | -4.0 [-11.4, +5.4] (4/195) | n/a [n/a, n/a] (0/257) | NOT READABLE |
| LONDON | BURST_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +0.8 [-5.8, +6.1] (15/452) | +1.6 [-7.0, +7.4] (9/195) | -0.9 [-11.1, +8.8] (6/257) | NOT READABLE |
| LONDON | BURST_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +1.5 [-4.7, +6.5] (15/1022) | +4.1 [-4.7, +9.8] (9/454) | -1.6 [-11.0, +7.4] (6/568) | NOT READABLE |
| LONDON | SPREAD_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -13.8 [-15.3, -12.3] (1/1047) | n/a [n/a, n/a] (0/466) | -14.7 [-16.7, -12.7] (1/581) | NOT READABLE |
| LONDON | SPREAD_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -14.2 [-16.4, -11.9] (1/485) | n/a [n/a, n/a] (0/212) | -13.7 [-16.7, -10.6] (1/273) | NOT READABLE |
| LONDON | TS_BONUS | BELOW -> WEAK | Ka crossed - untouched WEAK | -0.8 [-5.6, +3.4] (113/540) | +0.9 [-3.1, +4.9] (58/258) | -2.4 [-11.2, +5.1] (55/282) | NO DIFFERENCE SHOWN |
| LONDON | TS_BONUS | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -3.9 [-14.0, +5.1] (24/49) | -0.9 [-7.4, +7.4] (16/32) | -9.4 [-31.3, +11.3] (8/17) | NOT READABLE |
| LONDON | TS_BONUS | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -4.0 [-10.5, +3.0] (24/246) | -5.9 [-12.3, +2.7] (16/102) | +0.1 [-14.5, +10.1] (8/144) | NOT READABLE |
| LONDON | TS_BONUS | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -0.3 [-5.1, +4.0] (106/246) | +1.8 [-5.7, +7.2] (52/102) | -2.2 [-8.6, +3.5] (54/144) | NO DIFFERENCE SHOWN |
| LONDON | TS_BONUS | WEAK -> MEDIUM | Kb crossed - untouched WEAK | +0.4 [-4.4, +4.4] (106/540) | +3.9 [-3.1, +9.7] (52/258) | -2.9 [-9.3, +2.7] (54/282) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING_STEP3_3B | BELOW -> WEAK | Ka crossed - untouched WEAK | -6.9 [-22.2, +4.5] (24/600) | -5.9 [-9.2, -2.4] (7/293) | -7.7 [-29.6, +19.6] (17/307) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -8.2 [-15.7, -2.2] (11/71) | -5.9 [-14.5, +1.0] (8/51) | -14.4 [-24.4, -3.4] (3/20) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -9.6 [-15.7, -5.3] (11/282) | -8.4 [-15.1, -4.2] (8/134) | -13.9 [-19.5, -5.9] (3/148) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +5.4 [+1.2, +9.4] (121/600) | +1.4 [-4.2, +7.9] (42/293) | +7.2 [+2.6, +12.1] (79/307) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING_STEP3_3B | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +4.8 [+0.0, +9.8] (121/282) | -0.5 [-6.3, +5.8] (42/134) | +7.8 [+1.3, +14.7] (79/148) | NO DIFFERENCE SHOWN |
| LONDON | FUNDING_STEP3_3B | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +3.3 [-6.0, +14.2] (36/282) | -1.4 [-14.8, +6.4] (13/134) | +6.1 [-8.0, +22.2] (23/148) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +4.7 [-5.5, +16.7] (36/71) | +1.1 [-12.1, +10.9] (13/51) | +5.6 [-10.6, +22.5] (23/20) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -0.7 [-6.2, +3.3] (43/282) | -0.0 [-9.8, +7.9] (14/134) | -0.8 [-10.8, +4.8] (29/148) | NOT READABLE |
| LONDON | FUNDING_STEP3_3B | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -0.1 [-5.4, +3.4] (43/600) | +1.8 [-7.6, +10.1] (14/293) | -1.4 [-11.0, +3.4] (29/307) | NOT READABLE |
| LONDON | PASS2_UPGRADES | BELOW -> MEDIUM | Ka crossed - untouched MEDIUM | -1.0 [-14.1, +16.1] (7/18) | -2.4 [-15.7, +16.3] (5/10) | +3.1 [-23.1, +29.0] (2/8) | NOT READABLE |
| LONDON | PASS2_UPGRADES | BELOW -> WEAK | Ka crossed - untouched WEAK | -4.0 [-7.9, -0.0] (451/134) | -3.8 [-8.6, +1.3] (195/66) | -4.6 [-10.2, +1.3] (256/68) | NO DIFFERENCE SHOWN |
| LONDON | PASS2_UPGRADES | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -11.4 [-15.9, -6.8] (107/2) | -12.8 [-17.7, -8.2] (61/2) | n/a [n/a, n/a] (46/0) | NOT READABLE |
| LONDON | PASS2_UPGRADES | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | -2.9 [-10.3, +5.5] (107/18) | -3.8 [-12.3, +7.2] (61/10) | -1.8 [-15.4, +12.6] (46/8) | NOT READABLE |
| LONDON | PASS2_UPGRADES | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -1.1 [-7.4, +5.6] (363/18) | +0.9 [-5.7, +10.4] (160/10) | -2.9 [-15.8, +7.5] (203/8) | NOT READABLE |
| LONDON | PASS2_UPGRADES | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -2.4 [-6.5, +1.7] (363/134) | +1.1 [-4.0, +5.8] (160/66) | -5.5 [-11.6, +0.9] (203/68) | NO DIFFERENCE SHOWN |
| LONDON | PASS2_UPGRADES | WEAK -> STRONG | Ka crossed - untouched STRONG | +19.4 [-2.2, +41.6] (3/2) | n/a [n/a, n/a] (0/2) | n/a [n/a, n/a] (3/0) | NOT READABLE |
| LONDON | PASS2_UPGRADES | WEAK -> STRONG | Kb crossed - untouched WEAK | +26.6 [+4.9, +50.0] (3/134) | n/a [n/a, n/a] (0/66) | +24.7 [+2.5, +48.5] (3/68) | NOT READABLE |
| LONDON | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -0.2 [-4.1, +3.9] (65/953) | -1.4 [-4.9, +2.0] (39/414) | +2.3 [-6.3, +10.4] (26/539) | NOT READABLE |
| LONDON | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -0.9 [-5.3, +3.8] (65/437) | -3.5 [-6.9, +0.4] (39/190) | +2.6 [-6.6, +11.6] (26/247) | NOT READABLE |
| LONDON | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -8.1 [-14.2, +1.4] (18/437) | -1.4 [-15.2, +10.5] (5/190) | -10.4 [-17.2, +1.2] (13/247) | NOT READABLE |
| LONDON | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -7.0 [-14.3, +2.3] (18/123) | +2.5 [-12.0, +13.9] (5/72) | -13.0 [-24.7, -1.2] (13/51) | NOT READABLE |
| ASIA | SQUEEZE_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +2.8 [-1.5, +6.7] (36/1117) | +0.7 [-5.7, +6.6] (19/406) | +5.2 [-0.8, +10.6] (17/711) | NOT READABLE |
| ASIA | SQUEEZE_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +4.2 [-0.8, +8.5] (36/414) | +4.0 [-3.4, +10.5] (19/179) | +5.0 [-2.1, +10.9] (17/235) | NOT READABLE |
| ASIA | SQUEEZE_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +18.9 [+17.1, +20.3] (1/414) | n/a [n/a, n/a] (0/179) | +17.5 [+15.1, +19.7] (1/235) | NOT READABLE |
| ASIA | SQUEEZE_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +12.6 [+8.1, +15.1] (1/65) | n/a [n/a, n/a] (0/18) | +11.2 [+5.7, +14.2] (1/47) | NOT READABLE |
| ASIA | DECEL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -2.0 [-8.1, +3.8] (35/1054) | +1.3 [-8.3, +9.8] (13/395) | -3.9 [-11.5, +3.4] (22/659) | NOT READABLE |
| ASIA | DECEL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -0.3 [-6.9, +6.0] (35/391) | +4.7 [-4.1, +12.6] (13/169) | -3.5 [-12.6, +4.9] (22/222) | NOT READABLE |
| ASIA | DECEL_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -12.8 [-27.0, -4.3] (4/391) | -12.4 [-14.3, -10.7] (1/169) | -13.7 [-29.8, -4.1] (3/222) | NOT READABLE |
| ASIA | DECEL_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -19.1 [-34.3, -9.8] (4/62) | -17.9 [-21.3, -13.1] (1/16) | -19.5 [-37.4, -8.5] (3/46) | NOT READABLE |
| ASIA | STALL_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +0.4 [-5.6, +6.6] (33/1050) | -2.2 [-12.8, +8.0] (11/393) | +1.8 [-5.9, +10.0] (22/657) | NOT READABLE |
| ASIA | STALL_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.7 [-4.8, +8.4] (33/402) | +1.5 [-9.0, +10.2] (11/171) | +1.4 [-7.4, +11.0] (22/231) | NOT READABLE |
| ASIA | RSI_DIV_PEN | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -0.1 [-9.8, +10.0] (11/1197) | -14.8 [-16.9, -12.1] (1/441) | +1.5 [-8.8, +12.5] (10/756) | NOT READABLE |
| ASIA | RSI_DIV_PEN | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.5 [-8.9, +11.7] (11/404) | -11.1 [-12.6, -9.3] (1/174) | +1.4 [-10.4, +12.6] (10/230) | NOT READABLE |
| ASIA | RSI_DIV_PEN | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +9.2 [+7.6, +11.2] (1/404) | +11.3 [+9.7, +13.1] (1/174) | n/a [n/a, n/a] (0/230) | NOT READABLE |
| ASIA | RSI_DIV_PEN | STRONG -> MEDIUM | Kb crossed - untouched STRONG | +3.1 [-0.9, +7.9] (1/65) | +6.6 [+2.8, +11.8] (1/18) | n/a [n/a, n/a] (0/47) | NOT READABLE |
| ASIA | P2C_ALIGN | BELOW -> WEAK | Ka crossed - untouched WEAK | +4.7 [-0.1, +9.0] (50/975) | +3.3 [-10.0, +9.6] (11/378) | +5.2 [-0.8, +10.5] (39/597) | NOT READABLE |
| ASIA | P2C_ALIGN | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -0.9 [-11.1, +11.7] (33/11) | +17.2 [+12.7, +22.5] (10/3) | -7.8 [-18.2, +2.6] (23/8) | NOT READABLE |
| ASIA | P2C_ALIGN | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +4.7 [+0.6, +9.2] (33/209) | +8.3 [+2.8, +14.9] (10/92) | +2.1 [-3.0, +7.9] (23/117) | NOT READABLE |
| ASIA | P2C_ALIGN | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -0.4 [-3.7, +3.1] (79/209) | +1.6 [-3.1, +7.0] (31/92) | -2.1 [-6.6, +2.2] (48/117) | NOT READABLE |
| ASIA | P2C_ALIGN | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -1.0 [-4.7, +2.8] (79/975) | -2.2 [-6.2, +1.9] (31/378) | -0.3 [-5.9, +5.3] (48/597) | NOT READABLE |
| ASIA | OICVD_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | +2.7 [-17.7, +22.3] (4/1237) | -8.5 [-10.7, -6.4] (1/460) | +6.5 [-19.3, +24.5] (3/777) | NOT READABLE |
| ASIA | OICVD_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -2.9 [-12.8, +5.9] (11/39) | +11.8 [+5.8, +18.4] (2/10) | -6.9 [-20.3, +2.9] (9/29) | NOT READABLE |
| ASIA | OICVD_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +4.2 [-3.4, +10.4] (11/390) | +14.4 [+12.2, +16.5] (2/171) | +1.0 [-11.3, +7.4] (9/219) | NOT READABLE |
| ASIA | OICVD_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -15.4 [-19.8, -11.6] (8/390) | -10.8 [-13.1, -7.3] (5/171) | -21.5 [-24.2, -18.5] (3/219) | NOT READABLE |
| ASIA | OICVD_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -16.3 [-20.8, -12.8] (8/1237) | -13.6 [-16.3, -9.9] (5/460) | -20.8 [-23.0, -18.4] (3/777) | NOT READABLE |
| ASIA | OICVD_CONFLICT | MEDIUM -> WEAK | Ka crossed - untouched WEAK | -56.3 [-57.9, -54.7] (1/1222) | n/a [n/a, n/a] (0/447) | -56.2 [-58.3, -54.2] (1/775) | NOT READABLE |
| ASIA | OICVD_CONFLICT | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | -54.6 [-56.3, -53.1] (1/416) | n/a [n/a, n/a] (0/181) | -55.9 [-58.2, -53.7] (1/235) | NOT READABLE |
| ASIA | BURST_CONFIRM | BELOW -> WEAK | Ka crossed - untouched WEAK | +0.1 [-18.5, +16.1] (7/1211) | +9.7 [+7.9, +12.1] (1/452) | -1.5 [-23.3, +17.2] (6/759) | NOT READABLE |
| ASIA | BURST_CONFIRM | MEDIUM -> STRONG | Ka crossed - untouched STRONG | +5.6 [-6.0, +13.6] (6/55) | n/a [n/a, n/a] (0/17) | +4.8 [-7.7, +14.2] (6/38) | NOT READABLE |
| ASIA | BURST_CONFIRM | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +11.4 [+0.3, +17.9] (6/399) | n/a [n/a, n/a] (0/173) | +10.0 [-1.0, +16.6] (6/226) | NOT READABLE |
| ASIA | BURST_CONFIRM | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +0.3 [-6.6, +8.5] (11/399) | +8.5 [-5.7, +20.9] (5/173) | -6.4 [-11.8, +0.3] (6/226) | NOT READABLE |
| ASIA | BURST_CONFIRM | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -1.2 [-8.5, +7.0] (11/1211) | +5.1 [-9.2, +18.5] (5/452) | -6.6 [-12.2, +0.9] (6/759) | NOT READABLE |
| ASIA | TS_BONUS | BELOW -> WEAK | Ka crossed - untouched WEAK | -2.4 [-5.8, +0.8] (162/689) | -2.6 [-8.2, +1.7] (55/256) | -2.3 [-6.7, +2.0] (107/433) | NO DIFFERENCE SHOWN |
| ASIA | TS_BONUS | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -2.8 [-10.3, +3.6] (15/35) | -4.9 [-17.4, +8.6] (4/11) | -2.1 [-11.8, +5.5] (11/24) | NOT READABLE |
| ASIA | TS_BONUS | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +2.5 [-4.6, +9.3] (15/242) | +1.8 [-10.4, +14.4] (4/121) | +1.2 [-7.9, +9.8] (11/121) | NOT READABLE |
| ASIA | TS_BONUS | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | +0.9 [-4.0, +5.4] (88/242) | +2.6 [-1.9, +7.9] (27/121) | -1.3 [-8.1, +5.0] (61/121) | NOT READABLE |
| ASIA | TS_BONUS | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -0.6 [-5.4, +3.7] (88/689) | -1.0 [-4.9, +3.7] (27/256) | -0.4 [-7.5, +5.6] (61/433) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | BELOW -> WEAK | Ka crossed - untouched WEAK | +2.3 [-5.1, +8.5] (36/860) | +3.5 [-7.3, +7.0] (17/308) | +0.9 [-7.2, +12.8] (19/552) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | MEDIUM -> STRONG | Ka crossed - untouched STRONG | -2.7 [-10.0, +7.0] (6/51) | -3.1 [-8.8, +3.4] (2/14) | -2.1 [-12.1, +8.4] (4/37) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +2.4 [-2.8, +10.9] (6/331) | +0.1 [-2.3, +2.2] (2/147) | +3.2 [-4.3, +10.6] (4/184) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +0.9 [-2.2, +4.0] (107/860) | +1.5 [-3.9, +6.2] (47/308) | +0.2 [-3.3, +3.6] (60/552) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING_STEP3_3B | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +1.7 [-1.2, +4.9] (107/331) | +4.7 [-0.8, +9.3] (47/147) | -0.6 [-4.0, +3.0] (60/184) | NO DIFFERENCE SHOWN |
| ASIA | FUNDING_STEP3_3B | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | -2.3 [-7.4, +2.4] (23/331) | -6.6 [-16.1, -1.3] (10/147) | +0.9 [-3.6, +9.5] (13/184) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -7.5 [-12.2, -2.9] (23/51) | -9.7 [-19.8, -1.4] (10/14) | -4.5 [-10.1, +3.8] (13/37) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -3.4 [-13.9, +6.8] (25/331) | -2.3 [-9.2, +0.2] (15/147) | -4.2 [-23.5, +12.3] (10/184) | NOT READABLE |
| ASIA | FUNDING_STEP3_3B | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -4.3 [-15.0, +6.1] (25/860) | -5.5 [-12.8, -1.9] (15/308) | -3.5 [-23.4, +13.6] (10/552) | NOT READABLE |
| ASIA | PASS2_UPGRADES | BELOW -> MEDIUM | Ka crossed - untouched MEDIUM | -4.3 [-15.9, +7.1] (9/27) | -2.3 [-19.2, +10.2] (3/10) | -5.0 [-21.4, +10.7] (6/17) | NOT READABLE |
| ASIA | PASS2_UPGRADES | BELOW -> WEAK | Ka crossed - untouched WEAK | +0.9 [-2.8, +5.0] (542/254) | -2.8 [-6.6, +3.2] (193/93) | +3.0 [-1.6, +8.0] (349/161) | NO DIFFERENCE SHOWN |
| ASIA | PASS2_UPGRADES | MEDIUM -> STRONG | Ka crossed - untouched STRONG | n/a [n/a, n/a] (53/0) | n/a [n/a, n/a] (17/0) | n/a [n/a, n/a] (36/0) | NOT READABLE |
| ASIA | PASS2_UPGRADES | MEDIUM -> STRONG | Kb crossed - untouched MEDIUM | +4.3 [-6.7, +15.2] (53/27) | -3.6 [-10.5, +4.9] (17/10) | +8.4 [-8.9, +24.0] (36/17) | NOT READABLE |
| ASIA | PASS2_UPGRADES | WEAK -> MEDIUM | Ka crossed - untouched MEDIUM | -2.1 [-12.4, +7.9] (302/27) | -7.6 [-14.0, +1.3] (136/10) | +1.5 [-14.5, +15.8] (166/17) | NOT READABLE |
| ASIA | PASS2_UPGRADES | WEAK -> MEDIUM | Kb crossed - untouched WEAK | -0.6 [-4.9, +4.3] (302/254) | -5.8 [-10.6, +1.3] (136/93) | +2.9 [-2.8, +8.9] (166/161) | NO DIFFERENCE SHOWN |
| ASIA | PASS2_UPGRADES | WEAK -> STRONG | Ka crossed - untouched STRONG | n/a [n/a, n/a] (4/0) | n/a [n/a, n/a] (0/0) | n/a [n/a, n/a] (4/0) | NOT READABLE |
| ASIA | PASS2_UPGRADES | WEAK -> STRONG | Kb crossed - untouched WEAK | +0.8 [-13.5, +21.7] (4/254) | n/a [n/a, n/a] (0/93) | +2.7 [-12.2, +23.6] (4/161) | NOT READABLE |
| ASIA | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Ka crossed - untouched WEAK | +2.7 [-1.3, +6.5] (75/1125) | -0.4 [-6.8, +7.5] (19/431) | +3.8 [-0.8, +8.3] (56/694) | NOT READABLE |
| ASIA | TRANSITIONAL_ADX_PENALTY | MEDIUM -> WEAK | Kb crossed - untouched MEDIUM | +4.2 [+0.0, +8.2] (75/366) | +3.2 [-2.5, +10.4] (19/159) | +3.7 [-1.9, +9.2] (56/207) | NOT READABLE |
| ASIA | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Ka crossed - untouched MEDIUM | +5.1 [-4.7, +13.9] (11/366) | +15.4 [+11.8, +18.7] (2/159) | +1.8 [-9.1, +12.5] (9/207) | NOT READABLE |
| ASIA | TRANSITIONAL_ADX_PENALTY | STRONG -> MEDIUM | Kb crossed - untouched STRONG | -1.4 [-11.5, +8.0] (11/56) | +8.9 [+1.8, +15.6] (2/16) | -4.0 [-14.9, +7.4] (9/40) | NOT READABLE |

## 6. D-5: clamps and caps by tier

- Method: a clamp at 0 or a cap at regime max binds when the applied points are smaller in magnitude than the nominal magnitude. Exact only where the label carries that mutation alone (squeeze, decel, stall, CVD divergence, spread, Pass 2c, trend structure, funding). The funding nominal comes from the breakdown note text (`STEP3:` / `STEP3b:` flags).

- Parser check: matching trading-week sides where the funding points differ from the note's nominal AND the final raw score is above 1 (a clamp at 0 plus a Step 3b soften cannot explain these; must be 0): 0.

| Session | Tier | Rows | Trade side: rows with a binding clamp or cap (%) | Other side: rows with a binding clamp or cap (%) | Trade-side raw score = regime max (a cap could bind) | Binding kinds, trade side | Binding kinds, other side |
|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 0 (0.0) | 116 (33.9) | 0 | none | funding clamp or cap 97; decel clamp 34; squeeze clamp 1 |
| NY | MEDIUM | 1552 | 4 (0.3) | 426 (27.4) | 0 | squeeze clamp 4 | funding clamp or cap 273; decel clamp 160; squeeze clamp 45; CVD divergence clamp 3 |
| NY | WEAK | 3523 | 22 (0.6) | 795 (22.6) | 0 | squeeze clamp 22 | squeeze clamp 453; funding clamp or cap 298; decel clamp 185; stall clamp 3; CVD divergence clamp 2 |
| LONDON | STRONG | 131 | 0 (0.0) | 74 (56.5) | 0 | none | funding clamp or cap 69; decel clamp 10 |
| LONDON | MEDIUM | 485 | 0 (0.0) | 171 (35.3) | 0 | none | funding clamp or cap 140; decel clamp 38; squeeze clamp 11 |
| LONDON | WEAK | 1049 | 11 (1.0) | 205 (19.5) | 0 | squeeze clamp 11 | funding clamp or cap 109; squeeze clamp 93; decel clamp 37; stall clamp 3; CVD divergence clamp 1 |
| ASIA | STRONG | 65 | 0 (0.0) | 19 (29.2) | 0 | none | funding clamp or cap 15; decel clamp 7; CVD divergence clamp 1 |
| ASIA | MEDIUM | 420 | 0 (0.0) | 102 (24.3) | 0 | none | funding clamp or cap 61; decel clamp 45; squeeze clamp 6 |
| ASIA | WEAK | 1243 | 6 (0.5) | 246 (19.8) | 0 | squeeze clamp 6 | squeeze clamp 126; funding clamp or cap 108; decel clamp 54; stall clamp 1 |

## 7. D-6: dead and rare votes, and the reachable score

### 7.1 Fire rate per vote (either side non-zero)

| Vote | Population, all sessions % | NY all trading-week rows % | LONDON all trading-week rows % | ASIA all trading-week rows % |
|---|---|---|---|---|
| ROC | 71.77 | 45.99 | 59.01 | 42.25 |
| RSI | 78.41 | 69.16 | 70.95 | 70.92 |
| DMI | 100.00 | 100.00 | 99.98 | 100.00 |
| ADX | 81.18 | 59.45 | 69.51 | 60.83 |
| VOL | 0.06 | 0.28 | 0.09 | 0.07 |
| VWAP | 84.37 | 91.79 | 92.19 | 84.14 |
| BBW_TTM | 67.80 | 72.79 | 69.72 | 69.70 |
| EMA | 91.99 | 77.23 | 79.23 | 78.46 |
| FUNDING | 49.91 | 54.01 | 57.87 | 49.36 |
| OI | 15.10 | 8.33 | 9.76 | 9.29 |
| OFI | 63.90 | 64.55 | 68.07 | 67.28 |
| CVD | 57.26 | 53.22 | 52.84 | 54.88 |
| TFI | 90.92 | 89.93 | 90.91 | 89.99 |
| MICROCVD | 70.62 | 74.12 | 74.43 | 74.07 |
| LIQ | 0.00 | 0.00 | 0.00 | 0.00 |
| SPREAD | 0.06 | 0.01 | 0.09 | 0.02 |
| EMA200 | 100.00 | 100.00 | 100.00 | 100.00 |
| DONCHIAN | 61.73 | 54.37 | 52.28 | 53.51 |
| OBV | 62.67 | 60.66 | 49.59 | 48.34 |
| VPFR | 78.82 | 85.86 | 77.58 | 77.64 |
| REGIME_ALIGN | 32.44 | 28.37 | 21.42 | 24.93 |
| TREND_STRUCTURE | 48.68 | 29.03 | 33.92 | 28.98 |

- All trading-week rows: 39594 (the re-score's rows, NO TRADE kept; their scores are the re-scored scores).

### 7.2 Reachable score: dominant effective score over all trading-week rows

| Session | MaxScore | Rows | WEAK / MEDIUM / STRONG floors | p50 | p90 | p99 | Max observed | Share >= WEAK % | Share >= MEDIUM % | Share >= STRONG % | Max observed raw (one side) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | 20 | 17410 | 7 / 11 / 14 | 7 | 12 | 15 | 19 | 59.9 | 16.2 | 3.03 | 19 |
| NY | 19 | 5953 | 7 / 11 / 14 | 6 | 10 | 13 | 16 | 45.1 | 9.3 | 0.60 | 16 |
| NY | 15 | 5921 | 6 / 8 / 11 | 4 | 8 | 11 | 13 | 34.3 | 13.1 | 1.06 | 15 |
| LONDON | 20 | 2999 | 7 / 11 / 14 | 8 | 12 | 16 | 19 | 71.4 | 23.3 | 5.10 | 19 |
| LONDON | 19 | 542 | 7 / 11 / 14 | 5 | 10 | 13 | 15 | 32.7 | 6.6 | 0.55 | 15 |
| LONDON | 15 | 772 | 6 / 8 / 11 | 5 | 8 | 11 | 13 | 36.7 | 12.4 | 1.55 | 14 |
| ASIA | 20 | 3649 | 7 / 11 / 14 | 8 | 12 | 15 | 18 | 69.0 | 18.8 | 2.69 | 18 |
| ASIA | 19 | 1076 | 7 / 11 / 14 | 6 | 10 | 13 | 15 | 39.3 | 7.1 | 0.37 | 15 |
| ASIA | 15 | 1272 | 6 / 8 / 11 | 4 | 7 | 11 | 12 | 29.1 | 9.8 | 1.26 | 13 |

### 7.3 Votes that can add a point but never or rarely do (population rows)

| Vote | Rows with a positive point on the trade side | % |
|---|---|---|
| ROC | 5444 | 61.79 |
| RSI | 6520 | 74.01 |
| DMI | 8668 | 98.39 |
| ADX | 7152 | 81.18 |
| VOL | 5 | 0.06 |
| VWAP | 7055 | 80.08 |
| BBW_TTM | 4449 | 50.50 |
| EMA | 7988 | 90.67 |
| FUNDING | 1131 | 12.84 |
| OI | 1175 | 13.34 |
| OFI | 3033 | 34.43 |
| CVD | 3461 | 39.28 |
| TFI | 4881 | 55.40 |
| MICROCVD | 1785 | 20.26 |
| LIQ | 0 | 0.00 |
| SPREAD | 0 | 0.00 |
| EMA200 | 7598 | 86.24 |
| DONCHIAN | 5170 | 58.68 |
| OBV | 4735 | 53.75 |
| VPFR | 3875 | 43.98 |
| REGIME_ALIGN | 2746 | 31.17 |
| TREND_STRUCTURE | 4289 | 48.68 |

## 8. D-7: is MEDIUM just a worse mix?

### 8.1 Composition per tier (% of the tier's rows)

| Session | Dimension | Value | STRONG % | MEDIUM % | WEAK % |
|---|---|---|---|---|---|
| NY | regime | RANGE_BOUND | 3.2 | 8.4 | 10.5 |
| NY | regime | TRANSITIONAL | 4.1 | 13.4 | 9.3 |
| NY | regime | TRENDING_DOWN | 55.3 | 44.1 | 40.6 |
| NY | regime | TRENDING_UP | 37.4 | 34.0 | 39.6 |
| NY | hour bucket | 13-15 | 55.6 | 53.3 | 47.9 |
| NY | hour bucket | 16-19 | 30.4 | 33.4 | 36.9 |
| NY | hour bucket | 20-23 | 14.0 | 13.3 | 15.2 |
| NY | ATR regime | R1 | 63.2 | 55.7 | 51.5 |
| NY | ATR regime | R2 | 36.8 | 44.3 | 48.5 |
| NY | side | LONG | 39.8 | 42.1 | 48.1 |
| NY | side | SHORT | 60.2 | 57.9 | 51.9 |
| NY | target type | hvn | 7.0 | 8.1 | 18.7 |
| NY | target type | none | 75.7 | 60.5 | 46.9 |
| NY | target type | swing | 17.3 | 31.4 | 34.4 |
| NY | ExecResolution | 1 | 100.0 | 100.0 | 100.0 |
| LONDON | regime | RANGE_BOUND | 1.5 | 4.9 | 7.3 |
| LONDON | regime | TRANSITIONAL | 6.1 | 9.9 | 9.2 |
| LONDON | regime | TRENDING_DOWN | 69.5 | 50.7 | 42.7 |
| LONDON | regime | TRENDING_UP | 22.9 | 34.4 | 40.8 |
| LONDON | hour bucket | 08-09 | 54.2 | 43.7 | 42.8 |
| LONDON | hour bucket | 10-11 | 13.7 | 30.3 | 35.9 |
| LONDON | hour bucket | 12 | 32.1 | 26.0 | 21.3 |
| LONDON | ATR regime | R1 | 64.9 | 50.9 | 51.2 |
| LONDON | ATR regime | R2 | 35.1 | 49.1 | 48.8 |
| LONDON | side | LONG | 24.4 | 37.1 | 45.2 |
| LONDON | side | SHORT | 75.6 | 62.9 | 54.8 |
| LONDON | target type | hvn | 2.3 | 5.8 | 9.6 |
| LONDON | target type | none | 80.9 | 66.2 | 44.2 |
| LONDON | target type | swing | 16.8 | 28.0 | 46.1 |
| LONDON | ExecResolution | 3 | 100.0 | 100.0 | 100.0 |
| ASIA | regime | RANGE_BOUND | 3.1 | 6.0 | 10.8 |
| ASIA | regime | TRANSITIONAL | 13.8 | 12.9 | 9.5 |
| ASIA | regime | TRENDING_DOWN | 52.3 | 44.0 | 37.2 |
| ASIA | regime | TRENDING_UP | 30.8 | 37.1 | 42.6 |
| ASIA | hour bucket | 00-02 | 38.5 | 46.7 | 41.4 |
| ASIA | hour bucket | 03-05 | 21.5 | 29.5 | 36.1 |
| ASIA | hour bucket | 06-07 | 40.0 | 23.8 | 22.5 |
| ASIA | ATR regime | R1 | 32.3 | 47.1 | 43.8 |
| ASIA | ATR regime | R2 | 67.7 | 52.9 | 56.2 |
| ASIA | side | LONG | 35.4 | 46.7 | 50.9 |
| ASIA | side | SHORT | 64.6 | 53.3 | 49.1 |
| ASIA | target type | hvn | 3.1 | 6.7 | 9.5 |
| ASIA | target type | none | 78.5 | 59.8 | 34.8 |
| ASIA | target type | swing | 18.5 | 33.6 | 55.8 |
| ASIA | ExecResolution | 3 | 100.0 | 100.0 | 100.0 |

### 8.2 MEDIUM re-weighted to WEAK's mix (main window, d = MEDIUM - WEAK)

| Session | Strata | Strata count | WEAK rows in covered strata % | Raw d | Re-weighted FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|
| NY | regime x side x ATR regime x target type | 36 | 99.6 | -1.0 [-2.2, +0.2] | -0.6 [-1.8, +0.5] (1552/3523) | -1.0 [-2.5, +0.5] (630/1393) | -0.3 [-1.9, +1.1] (922/2130) | NO DIFFERENCE SHOWN |
| NY | regime x hour bucket x side | 18 | 100.0 | -1.0 [-2.2, +0.2] | -0.8 [-2.0, +0.2] (1552/3523) | -1.3 [-2.7, +0.3] (630/1393) | -0.5 [-2.1, +0.8] (922/2130) | NO DIFFERENCE SHOWN |
| NY | regime x side x ATR regime x target type x ExecResolution | 36 | 99.6 | -1.0 [-2.2, +0.2] | -0.6 [-1.7, +0.4] (1552/3523) | -1.0 [-2.5, +0.5] (630/1393) | -0.3 [-2.0, +1.1] (922/2130) | NO DIFFERENCE SHOWN |
| NY | VWAP-extension tercile x side | 6 | 100.0 | -1.0 [-2.2, +0.2] | -1.0 [-2.1, +0.2] (1552/3523) | -1.2 [-2.8, +0.6] (630/1393) | -0.9 [-2.4, +0.6] (922/2130) | NO DIFFERENCE SHOWN |
| NY | RSI tercile x EMA21-extension tercile x side | 14 | 100.0 | -1.0 [-2.2, +0.2] | -1.0 [-2.4, +0.5] (1552/3523) | -1.8 [-3.3, +0.4] (630/1393) | -0.4 [-2.2, +1.5] (922/2130) | NO DIFFERENCE SHOWN |
| LONDON | regime x side x ATR regime x target type | 33 | 97.3 | +0.4 [-2.0, +2.8] | -0.1 [-2.5, +2.3] (485/1049) | +1.1 [-1.2, +3.4] (212/467) | -0.8 [-4.7, +3.1] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | regime x hour bucket x side | 18 | 100.0 | +0.4 [-2.0, +2.8] | -0.1 [-2.7, +2.2] (485/1049) | +1.2 [-1.3, +3.3] (212/467) | -1.0 [-4.8, +2.9] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | regime x side x ATR regime x target type x ExecResolution | 33 | 97.3 | +0.4 [-2.0, +2.8] | -0.1 [-2.5, +2.3] (485/1049) | +1.1 [-1.3, +3.4] (212/467) | -0.8 [-4.7, +3.0] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | VWAP-extension tercile x side | 6 | 100.0 | +0.4 [-2.0, +2.8] | +0.8 [-1.9, +3.2] (485/1049) | +2.1 [-0.9, +4.7] (212/467) | -0.3 [-4.4, +3.6] (273/582) | NO DIFFERENCE SHOWN |
| LONDON | RSI tercile x EMA21-extension tercile x side | 14 | 100.0 | +0.4 [-2.0, +2.8] | -0.2 [-2.8, +2.0] (485/1049) | +1.9 [-0.6, +4.6] (212/467) | -2.4 [-6.2, +1.0] (273/582) | NO DIFFERENCE SHOWN |
| ASIA | regime x side x ATR regime x target type | 34 | 99.4 | -1.5 [-3.5, +0.6] | -1.7 [-4.4, +0.9] (420/1243) | -5.1 [-8.5, -2.6] (182/461) | +0.3 [-3.3, +4.0] (238/782) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | regime x hour bucket x side | 18 | 100.0 | -1.5 [-3.5, +0.6] | -2.1 [-4.1, -0.1] (420/1243) | -3.9 [-6.9, -1.6] (182/461) | -0.8 [-3.7, +2.0] (238/782) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | regime x side x ATR regime x target type x ExecResolution | 34 | 99.4 | -1.5 [-3.5, +0.6] | -1.7 [-4.3, +0.9] (420/1243) | -5.1 [-8.4, -2.6] (182/461) | +0.3 [-3.2, +4.0] (238/782) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | VWAP-extension tercile x side | 6 | 100.0 | -1.5 [-3.5, +0.6] | -1.6 [-3.8, +0.5] (420/1243) | -3.9 [-6.5, -1.3] (182/461) | -0.0 [-3.3, +2.9] (238/782) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | RSI tercile x EMA21-extension tercile x side | 14 | 100.0 | -1.5 [-3.5, +0.6] | -2.0 [-4.9, +1.5] (420/1243) | -4.1 [-7.6, -1.3] (182/461) | -1.3 [-5.4, +5.4] (238/782) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |

## 9. D-8: is MEDIUM a late entry?

### 9.1 Medians at entry by tier

| Session | Feature | STRONG median | MEDIUM median | WEAK median | MEDIUM - WEAK mean d | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|---|
| NY | distance from VWAP in ATR, signed to the trade side | 6.87 | 5.70 | 5.31 | MEDIUM - WEAK | +0.6 [+0.3, +1.0] (1552/3523) | +0.7 [+0.2, +1.1] (630/1393) | +0.6 [+0.2, +1.0] (922/2130) | CONFIRMED (d > 0) |
| NY | distance from EMA21 in ATR, signed | 2.63 | 2.17 | 1.21 | MEDIUM - WEAK | +1.0 [+0.9, +1.1] (1552/3523) | +0.9 [+0.8, +1.0] (630/1393) | +1.1 [+0.9, +1.2] (922/2130) | CONFIRMED (d > 0) |
| NY | ROC, signed | 0.25 | 0.19 | 0.09 | MEDIUM - WEAK | +0.1 [+0.1, +0.1] (1552/3523) | +0.1 [+0.1, +0.1] (630/1393) | +0.1 [+0.1, +0.2] (922/2130) | CONFIRMED (d > 0) |
| NY | RSI toward the trade side (100 - RSI for shorts) | 72.79 | 68.29 | 59.77 | MEDIUM - WEAK | +8.9 [+8.0, +9.8] (1552/3523) | +7.8 [+6.8, +8.8] (630/1393) | +9.6 [+8.4, +11.0] (922/2130) | CONFIRMED (d > 0) |
| NY | extension from the opposite 5m swing extreme in ATR | 9.85 | 8.79 | 8.21 | MEDIUM - WEAK | +0.4 [-0.1, +0.9] (1536/3493) | +0.4 [-0.1, +1.0] (616/1367) | +0.4 [-0.3, +1.2] (920/2126) | NO DIFFERENCE SHOWN |
| NY | room to the same-side 5m swing extreme in ATR | -2.62 | -0.71 | 1.93 | MEDIUM - WEAK | -1.7 [-2.1, -1.3] (1536/3493) | -1.5 [-2.2, -1.0] (616/1367) | -1.8 [-2.3, -1.2] (920/2126) | CONFIRMED (d < 0) |
| NY | TTM histogram in ATR, signed | 2.96 | 2.54 | 1.35 | MEDIUM - WEAK | +1.3 [+1.1, +1.4] (1552/3523) | +1.2 [+1.0, +1.4] (630/1393) | +1.4 [+1.2, +1.6] (922/2130) | CONFIRMED (d > 0) |
| LONDON | distance from VWAP in ATR, signed to the trade side | 7.36 | 5.88 | 5.16 | MEDIUM - WEAK | +1.0 [+0.4, +1.7] (485/1049) | +1.3 [+0.3, +2.5] (212/467) | +0.8 [+0.2, +1.4] (273/582) | CONFIRMED (d > 0) |
| LONDON | distance from EMA21 in ATR, signed | 2.65 | 2.21 | 1.43 | MEDIUM - WEAK | +0.8 [+0.6, +0.9] (485/1049) | +0.8 [+0.6, +1.0] (212/467) | +0.7 [+0.5, +0.9] (273/582) | CONFIRMED (d > 0) |
| LONDON | ROC, signed | 0.26 | 0.22 | 0.13 | MEDIUM - WEAK | +0.1 [+0.1, +0.1] (485/1049) | +0.1 [+0.1, +0.1] (212/467) | +0.1 [+0.1, +0.2] (273/582) | CONFIRMED (d > 0) |
| LONDON | RSI toward the trade side (100 - RSI for shorts) | 74.12 | 69.58 | 61.76 | MEDIUM - WEAK | +7.5 [+6.3, +8.8] (485/1049) | +7.6 [+5.9, +9.4] (212/467) | +7.4 [+5.8, +9.2] (273/582) | CONFIRMED (d > 0) |
| LONDON | extension from the opposite 5m swing extreme in ATR | 6.14 | 5.47 | 4.90 | MEDIUM - WEAK | +0.5 [+0.1, +0.8] (476/1027) | +0.5 [-0.0, +1.1] (206/455) | +0.4 [+0.0, +0.8] (270/572) | DISCOVERY ONLY (H2) (d > 0) |
| LONDON | room to the same-side 5m swing extreme in ATR | -1.50 | -1.03 | 1.53 | MEDIUM - WEAK | -1.7 [-2.1, -1.3] (476/1027) | -1.8 [-2.5, -1.2] (206/455) | -1.6 [-2.2, -1.0] (270/572) | CONFIRMED (d < 0) |
| LONDON | TTM histogram in ATR, signed | 3.18 | 2.69 | 1.64 | MEDIUM - WEAK | +1.0 [+0.8, +1.2] (485/1049) | +1.0 [+0.8, +1.3] (212/467) | +1.0 [+0.8, +1.3] (273/582) | CONFIRMED (d > 0) |
| ASIA | distance from VWAP in ATR, signed to the trade side | 4.53 | 3.74 | 3.39 | MEDIUM - WEAK | +0.6 [+0.3, +0.9] (420/1243) | +0.5 [+0.2, +0.9] (182/461) | +0.6 [+0.1, +1.1] (238/782) | CONFIRMED (d > 0) |
| ASIA | distance from EMA21 in ATR, signed | 2.48 | 2.18 | 1.28 | MEDIUM - WEAK | +0.8 [+0.7, +0.9] (420/1243) | +1.0 [+0.8, +1.2] (182/461) | +0.6 [+0.5, +0.8] (238/782) | CONFIRMED (d > 0) |
| ASIA | ROC, signed | 0.32 | 0.23 | 0.11 | MEDIUM - WEAK | +0.1 [+0.1, +0.2] (420/1243) | +0.1 [+0.1, +0.2] (182/461) | +0.1 [+0.1, +0.2] (238/782) | CONFIRMED (d > 0) |
| ASIA | RSI toward the trade side (100 - RSI for shorts) | 72.36 | 69.29 | 60.87 | MEDIUM - WEAK | +7.3 [+6.3, +8.5] (420/1243) | +8.5 [+6.7, +10.6] (182/461) | +6.3 [+5.0, +7.7] (238/782) | CONFIRMED (d > 0) |
| ASIA | extension from the opposite 5m swing extreme in ATR | 5.80 | 5.72 | 4.82 | MEDIUM - WEAK | +0.9 [+0.5, +1.2] (420/1242) | +0.7 [+0.0, +1.5] (182/461) | +0.9 [+0.4, +1.3] (238/781) | CONFIRMED (d > 0) |
| ASIA | room to the same-side 5m swing extreme in ATR | -2.07 | -0.97 | 1.54 | MEDIUM - WEAK | -1.5 [-1.9, -1.2] (420/1242) | -1.8 [-2.4, -1.3] (182/461) | -1.3 [-1.6, -1.0] (238/781) | CONFIRMED (d < 0) |
| ASIA | TTM histogram in ATR, signed | 2.82 | 2.58 | 1.43 | MEDIUM - WEAK | +1.0 [+0.8, +1.2] (420/1243) | +1.3 [+1.0, +1.6] (182/461) | +0.8 [+0.6, +1.0] (238/782) | CONFIRMED (d > 0) |

### 9.2 Does extension predict outcome? Terciles cut on the session's H1 rows (main window)

| Session | Feature | H1 cut points | Top tercile MEDIUM % / WEAK % | EV bottom / middle / top | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|---|---|---|
| NY | f_vwap_atr | 3.94 / 7.81 | 31.9 / 28.4 | -2.7 / -3.5 / -4.4 | top - bottom | -1.7 [-3.5, -0.1] (1641/1888) | -0.8 [-3.9, +1.6] (719/721) | -2.4 [-4.5, -0.4] (922/1167) | DISCOVERY ONLY (H2) (d < 0) |
| NY | f_ema21_atr | 1.23 / 2.30 | 45.2 / 19.4 | -2.6 / -4.1 / -4.0 | top - bottom | -1.4 [-3.0, +0.4] (1597/2082) | -0.9 [-3.5, +1.8] (719/721) | -1.7 [-3.8, +0.6] (878/1361) | NO DIFFERENCE SHOWN |
| NY | f_roc | 0.06 / 0.19 | 49.8 / 25.3 | -2.6 / -3.6 / -4.2 | top - bottom | -1.6 [-3.2, +0.2] (1886/1799) | +0.5 [-2.1, +3.3] (719/721) | -2.9 [-4.8, -0.8] (1167/1078) | DISCOVERY ONLY (H2) (d < 0) |
| NY | f_rsi | 59.09 / 68.31 | 49.8 / 21.8 | -2.7 / -4.0 / -3.9 | top - bottom | -1.2 [-3.0, +0.7] (1767/1942) | -0.6 [-3.3, +2.3] (719/722) | -1.7 [-4.1, +0.9] (1048/1220) | NO DIFFERENCE SHOWN |
| NY | f_swing_ext_atr | 7.42 / 11.63 | 27.3 / 26.5 | -3.0 / -3.8 / -3.8 | top - bottom | -0.8 [-2.2, +0.3] (1476/2118) | -1.5 [-4.1, +0.7] (705/707) | -0.5 [-2.1, +0.7] (771/1411) | NO DIFFERENCE SHOWN |
| NY | f_room_atr | -2.27 / 2.53 | 24.4 / 41.0 | -3.6 / -4.2 / -2.7 | top - bottom | +0.8 [-1.0, +2.7] (1865/1672) | +1.9 [-1.1, +4.7] (705/707) | +0.2 [-2.1, +2.7] (1160/965) | NO DIFFERENCE SHOWN |
| NY | f_ttm_atr | 1.14 / 2.76 | 44.4 / 21.2 | -2.6 / -4.1 / -3.9 | top - bottom | -1.3 [-3.1, +0.5] (1632/1889) | -1.1 [-3.9, +1.8] (719/721) | -1.5 [-3.9, +0.9] (913/1168) | NO DIFFERENCE SHOWN |
| LONDON | f_vwap_atr | 4.93 / 8.73 | 23.7 / 19.3 | -0.6 / -1.5 / -1.5 | top - bottom | -1.0 [-4.6, +3.3] (365/685) | +1.0 [-3.3, +4.8] (251/252) | -2.3 [-8.6, +10.3] (114/433) | NO DIFFERENCE SHOWN |
| LONDON | f_ema21_atr | 1.29 / 2.42 | 42.1 / 19.7 | -1.4 / -1.5 / -0.3 | top - bottom | +1.1 [-3.4, +6.0] (491/577) | +0.2 [-4.1, +4.6] (251/252) | +2.1 [-5.1, +10.9] (240/325) | NO DIFFERENCE SHOWN |
| LONDON | f_roc | 0.09 / 0.21 | 54.2 / 32.4 | -1.2 / -2.5 / -0.2 | top - bottom | +1.0 [-2.7, +4.8] (689/512) | -1.2 [-5.5, +3.1] (251/252) | +2.3 [-3.2, +8.3] (438/260) | NO DIFFERENCE SHOWN |
| LONDON | f_rsi | 59.91 / 70.19 | 47.8 / 24.4 | -1.9 / -2.1 / +0.6 | top - bottom | +2.5 [-2.0, +7.5] (580/529) | +1.5 [-3.2, +6.6] (249/252) | +3.2 [-3.9, +11.0] (331/277) | NO DIFFERENCE SHOWN |
| LONDON | f_swing_ext_atr | 4.32 / 6.74 | 33.0 / 30.2 | -0.9 / -0.4 / -2.3 | top - bottom | -1.4 [-4.4, +1.4] (515/608) | -1.2 [-4.7, +1.7] (244/245) | -1.5 [-6.2, +3.1] (271/363) | NO DIFFERENCE SHOWN |
| LONDON | f_room_atr | -1.30 / 2.05 | 17.0 / 39.7 | -0.0 / -1.6 / -2.0 | top - bottom | -2.0 [-7.2, +2.2] (501/547) | -2.2 [-7.3, +2.5] (244/245) | -1.7 [-10.3, +4.8] (257/302) | NO DIFFERENCE SHOWN |
| LONDON | f_ttm_atr | 1.52 / 2.98 | 42.3 / 22.1 | -1.6 / -1.6 / -0.1 | top - bottom | +1.5 [-3.1, +6.6] (509/606) | +0.7 [-3.5, +4.8] (251/252) | +2.3 [-5.3, +11.1] (258/354) | NO DIFFERENCE SHOWN |
| ASIA | f_vwap_atr | 3.04 / 5.50 | 28.8 / 21.8 | -3.3 / -1.9 / -1.5 | top - bottom | +1.8 [-1.2, +4.8] (418/718) | +2.7 [-1.5, +7.3] (220/221) | +1.7 [-3.2, +6.0] (198/497) | NO DIFFERENCE SHOWN |
| ASIA | f_ema21_atr | 1.22 / 2.37 | 43.6 / 16.3 | -2.4 / -2.5 / -2.2 | top - bottom | +0.1 [-3.4, +3.7] (420/673) | -0.9 [-6.4, +5.2] (220/221) | +1.1 [-3.7, +6.1] (200/452) | NO DIFFERENCE SHOWN |
| ASIA | f_roc | 0.07 / 0.23 | 50.0 / 28.0 | -2.4 / -2.4 / -2.3 | top - bottom | +0.2 [-2.7, +3.0] (604/588) | -3.2 [-7.4, +1.7] (220/221) | +2.1 [-1.1, +5.3] (384/367) | NO DIFFERENCE SHOWN |
| ASIA | f_rsi | 59.19 / 70.62 | 45.2 / 19.8 | -2.8 / -1.9 / -2.4 | top - bottom | +0.4 [-3.2, +3.9] (477/602) | -1.5 [-7.2, +4.7] (220/221) | +1.7 [-2.4, +5.7] (257/381) | NO DIFFERENCE SHOWN |
| ASIA | f_swing_ext_atr | 4.40 / 7.11 | 29.5 / 21.7 | -1.8 / -2.8 / -2.6 | top - bottom | -0.7 [-4.6, +2.7] (414/668) | +0.7 [-3.6, +5.2] (220/221) | -2.0 [-7.8, +3.1] (194/447) | NO DIFFERENCE SHOWN |
| ASIA | f_room_atr | -1.68 / 1.91 | 16.9 / 41.9 | -3.0 / -2.4 / -1.8 | top - bottom | +1.3 [-1.9, +4.6] (596/478) | +2.5 [-3.3, +7.5] (220/221) | +0.5 [-3.8, +4.8] (376/257) | NO DIFFERENCE SHOWN |
| ASIA | f_ttm_atr | 1.18 / 2.90 | 44.3 / 19.1 | -2.1 / -2.9 / -2.1 | top - bottom | -0.0 [-3.1, +3.0] (455/618) | -1.5 [-6.7, +4.0] (220/221) | +1.1 [-2.7, +4.6] (235/397) | NO DIFFERENCE SHOWN |

## 10. D-9: which path led into MEDIUM?

| Session | Tier | Path (previous signal in the episode) | n | Success % | Net EV main [95 % CI] |
|---|---|---|---|---|---|
| NY | STRONG | first in episode | 7 (NOT READABLE) | 71.4 | +0.5 [-9.3, +7.9] |
| NY | STRONG | after WEAK | 106 | 47.2 | -4.0 [-7.6, +0.1] |
| NY | STRONG | after MEDIUM | 140 | 40.7 | -3.7 [-7.1, -0.4] |
| NY | STRONG | after STRONG | 89 (NOT READABLE) | 50.6 | -1.9 [-5.8, +1.5] |
| NY | MEDIUM | first in episode | 110 | 35.5 | -3.6 [-6.0, -1.0] |
| NY | MEDIUM | after WEAK | 655 | 41.1 | -3.6 [-4.6, -2.6] |
| NY | MEDIUM | after MEDIUM | 625 | 39.5 | -4.5 [-6.5, -2.7] |
| NY | MEDIUM | after STRONG | 162 | 39.5 | -5.7 [-7.8, -3.6] |
| NY | WEAK | first in episode | 314 | 37.6 | -3.1 [-4.1, -2.0] |
| NY | WEAK | after WEAK | 2395 | 41.0 | -3.0 [-4.0, -1.9] |
| NY | WEAK | after MEDIUM | 725 | 38.2 | -4.1 [-5.3, -2.8] |
| NY | WEAK | after STRONG | 89 (NOT READABLE) | 41.6 | -3.4 [-6.0, -1.0] |
| LONDON | STRONG | first in episode | 9 (NOT READABLE) | 33.3 | -3.3 [-10.3, +4.5] |
| LONDON | STRONG | after WEAK | 31 (NOT READABLE) | 45.2 | +4.7 [-5.4, +16.4] |
| LONDON | STRONG | after MEDIUM | 60 (NOT READABLE) | 41.7 | -3.2 [-6.9, +0.8] |
| LONDON | STRONG | after STRONG | 31 (NOT READABLE) | 38.7 | -4.2 [-9.7, +0.9] |
| LONDON | MEDIUM | first in episode | 35 (NOT READABLE) | 45.7 | -0.0 [-4.7, +4.8] |
| LONDON | MEDIUM | after WEAK | 229 | 45.4 | -1.1 [-3.3, +1.0] |
| LONDON | MEDIUM | after MEDIUM | 159 | 48.4 | +0.1 [-3.8, +4.0] |
| LONDON | MEDIUM | after STRONG | 62 (NOT READABLE) | 43.5 | -2.5 [-7.8, +3.5] |
| LONDON | WEAK | first in episode | 88 (NOT READABLE) | 50.0 | -0.6 [-3.0, +2.0] |
| LONDON | WEAK | after WEAK | 694 | 41.2 | -1.5 [-3.4, +0.4] |
| LONDON | WEAK | after MEDIUM | 232 | 42.7 | -1.2 [-4.3, +1.9] |
| LONDON | WEAK | after STRONG | 35 (NOT READABLE) | 42.9 | +3.1 [-3.9, +11.3] |
| ASIA | STRONG | first in episode | 5 (NOT READABLE) | 60.0 | +1.7 [-9.2, +12.2] |
| ASIA | STRONG | after WEAK | 22 (NOT READABLE) | 68.2 | +5.3 [-1.4, +11.6] |
| ASIA | STRONG | after MEDIUM | 25 (NOT READABLE) | 60.0 | -0.5 [-5.4, +4.3] |
| ASIA | STRONG | after STRONG | 13 (NOT READABLE) | 69.2 | +3.9 [-9.3, +10.2] |
| ASIA | MEDIUM | first in episode | 44 (NOT READABLE) | 50.0 | -3.0 [-6.7, +0.6] |
| ASIA | MEDIUM | after WEAK | 212 | 43.9 | -4.6 [-6.5, -2.7] |
| ASIA | MEDIUM | after MEDIUM | 137 | 54.0 | -2.6 [-5.9, +0.4] |
| ASIA | MEDIUM | after STRONG | 27 (NOT READABLE) | 51.9 | -2.7 [-12.3, +7.3] |
| ASIA | WEAK | first in episode | 119 | 53.8 | -0.7 [-2.9, +1.7] |
| ASIA | WEAK | after WEAK | 867 | 45.8 | -2.2 [-4.3, -0.2] |
| ASIA | WEAK | after MEDIUM | 235 | 45.5 | -3.3 [-5.8, -0.9] |
| ASIA | WEAK | after STRONG | 22 (NOT READABLE) | 59.1 | +2.9 [-4.7, +10.6] |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | first signal in episode only, MEDIUM - WEAK, main | -0.5 [-3.3, +2.4] (110/314) | -3.3 [-6.8, +0.7] (55/133) | +2.0 [-2.0, +6.1] (55/181) | NO DIFFERENCE SHOWN |
| NY | first signal in episode only, MEDIUM - WEAK, C24 | -0.1 [-3.0, +3.0] (110/314) | -2.9 [-6.7, +1.2] (55/133) | +2.6 [-1.6, +6.8] (55/181) | NO DIFFERENCE SHOWN |
| NY | MEDIUM after WEAK - MEDIUM after STRONG | +2.1 [-0.2, +4.2] (655/162) | -0.7 [-4.5, +3.2] (256/67) | +4.1 [+1.9, +6.1] (399/95) | NO DIFFERENCE SHOWN |
| NY | MEDIUM first in episode - MEDIUM continuation | +0.6 [-2.0, +3.4] (110/1442) | -1.6 [-5.0, +2.3] (55/575) | +2.8 [-0.7, +6.4] (55/867) | NO DIFFERENCE SHOWN |
| NY | episodes: 431; signals per episode mean 12.57 |  |  |  |  |
| LONDON | first signal in episode only, MEDIUM - WEAK, main | +0.6 [-4.7, +5.7] (35/88) | +2.4 [-3.5, +8.5] (18/45) | -1.3 [-10.1, +7.3] (17/43) | NOT READABLE |
| LONDON | first signal in episode only, MEDIUM - WEAK, C24 | -1.8 [-8.6, +4.4] (35/88) | +2.3 [-3.9, +8.9] (18/45) | -6.1 [-18.0, +4.6] (17/43) | NOT READABLE |
| LONDON | MEDIUM after WEAK - MEDIUM after STRONG | +1.4 [-5.1, +7.0] (229/62) | +0.7 [-5.9, +7.5] (98/33) | +2.7 [-10.0, +11.6] (131/29) | NOT READABLE |
| LONDON | MEDIUM first in episode - MEDIUM continuation | +0.8 [-4.2, +6.3] (35/450) | +0.2 [-5.5, +6.1] (18/194) | +1.2 [-6.7, +11.1] (17/256) | NOT READABLE |
| LONDON | episodes: 132; signals per episode mean 12.61 |  |  |  |  |
| ASIA | first signal in episode only, MEDIUM - WEAK, main | -2.3 [-7.0, +2.3] (44/119) | -9.5 [-15.1, -4.5] (21/42) | +3.4 [-2.9, +9.0] (23/77) | NOT READABLE |
| ASIA | first signal in episode only, MEDIUM - WEAK, C24 | -1.7 [-6.6, +3.0] (44/119) | -9.4 [-15.3, -3.9] (21/42) | +4.2 [-2.3, +10.0] (23/77) | NOT READABLE |
| ASIA | MEDIUM after WEAK - MEDIUM after STRONG | -1.9 [-13.0, +8.7] (212/27) | -6.0 [-15.3, +2.9] (84/7) | -0.4 [-15.1, +13.9] (128/20) | NOT READABLE |
| ASIA | MEDIUM first in episode - MEDIUM continuation | +0.8 [-3.4, +4.9] (44/376) | -2.3 [-7.1, +2.4] (21/161) | +3.8 [-2.4, +9.8] (23/215) | NOT READABLE |
| ASIA | episodes: 168; signals per episode mean 10.29 |  |  |  |  |

## 11. D-10: context and horizon

### 11.1 VerdictContext by tier

| Session | Tier | VerdictContext | Share of tier % | n | Success % | Net EV main [95 % CI] |
|---|---|---|---|---|---|---|
| NY | STRONG | CONFIRMED | 19.6 | 67 (NOT READABLE) | 31.3 | -6.8 [-10.8, -2.1] |
| NY | STRONG | MOMENTUM_FADING | 30.7 | 105 | 46.7 | -2.1 [-7.2, +2.0] |
| NY | STRONG | STRUCTURALLY_WEAK | 49.7 | 170 | 51.2 | -2.5 [-4.9, -0.4] |
| NY | MEDIUM | CONFIRMED | 29.3 | 454 | 37.7 | -3.6 [-5.1, -2.1] |
| NY | MEDIUM | FLOW_UNCONFIRMED | 1.7 | 26 (NOT READABLE) | 26.9 | -5.2 [-10.9, +1.6] |
| NY | MEDIUM | MOMENTUM_FADING | 34.5 | 536 | 40.7 | -4.6 [-6.2, -3.1] |
| NY | MEDIUM | STRUCTURALLY_WEAK | 34.5 | 536 | 41.6 | -4.3 [-6.0, -2.5] |
| NY | WEAK | CONFIRMED | 27.7 | 975 | 40.3 | -2.5 [-3.6, -1.3] |
| NY | WEAK | FLOW_UNCONFIRMED | 13.9 | 488 | 34.4 | -3.3 [-5.4, -1.3] |
| NY | WEAK | MOMENTUM_FADING | 34.3 | 1208 | 41.1 | -3.4 [-4.5, -2.4] |
| NY | WEAK | STRUCTURALLY_WEAK | 24.2 | 852 | 41.8 | -3.7 [-5.1, -2.2] |
| LONDON | STRONG | CONFIRMED | 12.2 | 16 (NOT READABLE) | 25.0 | -5.8 [-10.9, -0.6] |
| LONDON | STRONG | MOMENTUM_FADING | 32.1 | 42 (NOT READABLE) | 42.9 | +0.1 [-7.4, +8.9] |
| LONDON | STRONG | STRUCTURALLY_WEAK | 55.7 | 73 (NOT READABLE) | 43.8 | -1.6 [-6.6, +4.0] |
| LONDON | MEDIUM | CONFIRMED | 22.9 | 111 | 37.8 | -3.8 [-6.5, -1.3] |
| LONDON | MEDIUM | FLOW_UNCONFIRMED | 0.6 | 3 (NOT READABLE) | 33.3 | -8.3 [-8.9, -8.0] |
| LONDON | MEDIUM | MOMENTUM_FADING | 37.3 | 181 | 46.4 | +0.5 [-3.3, +4.0] |
| LONDON | MEDIUM | STRUCTURALLY_WEAK | 39.2 | 190 | 51.1 | -0.1 [-2.7, +2.2] |
| LONDON | WEAK | CONFIRMED | 29.3 | 307 | 43.0 | -1.5 [-4.9, +1.5] |
| LONDON | WEAK | FLOW_UNCONFIRMED | 11.2 | 118 | 39.8 | -2.9 [-7.5, +1.1] |
| LONDON | WEAK | MOMENTUM_FADING | 36.7 | 385 | 41.6 | -0.9 [-3.3, +1.5] |
| LONDON | WEAK | STRUCTURALLY_WEAK | 22.8 | 239 | 43.9 | -0.6 [-4.1, +3.7] |
| ASIA | STRONG | CONFIRMED | 12.3 | 8 (NOT READABLE) | 62.5 | +3.8 [-1.6, +9.9] |
| ASIA | STRONG | MOMENTUM_FADING | 26.2 | 17 (NOT READABLE) | 70.6 | +4.7 [-0.9, +12.5] |
| ASIA | STRONG | STRUCTURALLY_WEAK | 61.5 | 40 (NOT READABLE) | 62.5 | +1.4 [-4.5, +6.4] |
| ASIA | MEDIUM | CONFIRMED | 26.2 | 110 | 43.6 | -3.2 [-6.4, -0.3] |
| ASIA | MEDIUM | FLOW_UNCONFIRMED | 1.2 | 5 (NOT READABLE) | 60.0 | -3.6 [-22.4, +9.0] |
| ASIA | MEDIUM | MOMENTUM_FADING | 37.9 | 159 | 47.2 | -4.8 [-7.3, -2.5] |
| ASIA | MEDIUM | STRUCTURALLY_WEAK | 34.8 | 146 | 52.7 | -2.7 [-5.6, +0.2] |
| ASIA | WEAK | CONFIRMED | 25.9 | 322 | 46.3 | -0.7 [-3.1, +1.8] |
| ASIA | WEAK | FLOW_UNCONFIRMED | 16.6 | 206 | 41.3 | -1.9 [-4.9, +0.8] |
| ASIA | WEAK | MOMENTUM_FADING | 36.5 | 454 | 44.3 | -3.4 [-5.4, -1.1] |
| ASIA | WEAK | STRUCTURALLY_WEAK | 21.0 | 261 | 55.9 | -2.3 [-4.4, -0.2] |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | MEDIUM - WEAK within CONFIRMED | -1.1 [-3.0, +0.8] (454/975) | -2.1 [-5.1, +1.1] (186/372) | -0.5 [-2.9, +1.9] (268/603) | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK within FLOW_UNCONFIRMED | -1.9 [-8.1, +5.1] (26/488) | -6.3 [-14.1, +0.1] (11/196) | +1.3 [-8.1, +12.2] (15/292) | NOT READABLE |
| NY | MEDIUM - WEAK within MOMENTUM_FADING | -1.2 [-3.1, +0.8] (536/1208) | -1.0 [-3.4, +1.4] (234/517) | -1.3 [-4.2, +1.7] (302/691) | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK within STRUCTURALLY_WEAK | -0.6 [-2.3, +1.1] (536/852) | -0.8 [-3.1, +1.7] (199/308) | -0.5 [-2.9, +1.8] (337/544) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK within CONFIRMED | -2.3 [-6.0, +1.8] (111/307) | -2.0 [-5.3, +1.1] (49/136) | -2.5 [-8.6, +4.4] (62/171) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK within FLOW_UNCONFIRMED | -5.4 [-9.5, -1.3] (3/118) | -7.5 [-11.1, -3.6] (1/52) | -4.0 [-11.1, +1.5] (2/66) | NOT READABLE |
| LONDON | MEDIUM - WEAK within MOMENTUM_FADING | +1.3 [-2.8, +5.4] (181/385) | +4.2 [-0.7, +9.0] (66/166) | -0.7 [-6.6, +5.0] (115/219) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK within STRUCTURALLY_WEAK | +0.4 [-3.7, +4.1] (190/239) | +3.5 [+0.4, +6.8] (96/113) | -2.5 [-9.3, +4.0] (94/126) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK within CONFIRMED | -2.6 [-6.4, +0.9] (110/322) | -5.3 [-10.5, -1.3] (39/122) | -1.0 [-6.5, +4.0] (71/200) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK within FLOW_UNCONFIRMED | -1.7 [-21.7, +11.8] (5/206) | -18.6 [-22.5, -13.7] (1/69) | +2.7 [-22.5, +13.0] (4/137) | NOT READABLE |
| ASIA | MEDIUM - WEAK within MOMENTUM_FADING | -1.4 [-4.0, +1.2] (159/454) | -2.7 [-5.6, +0.7] (76/187) | -0.2 [-4.2, +3.5] (83/267) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK within STRUCTURALLY_WEAK | -0.4 [-4.1, +3.2] (146/261) | -2.7 [-8.3, +1.9] (66/83) | +1.1 [-4.1, +6.8] (80/178) | NO DIFFERENCE SHOWN |

### 11.2 Horizon: time to resolution (carried 24 h) and the gap by window

| Session | Tier | Resolved in carried 24 h % | Median minutes to resolution (resolved rows) | p90 | Resolved inside the main window % | Unresolved at main-window close that hit target later % |
|---|---|---|---|---|---|---|
| NY | STRONG | 100.0 | 3 | 10 | 97.7 | 25.0 |
| NY | MEDIUM | 100.0 | 4 | 13 | 92.7 | 43.9 |
| NY | WEAK | 100.0 | 4 | 14 | 91.9 | 47.0 |
| LONDON | STRONG | 100.0 | 9 | 32 | 93.9 | 50.0 |
| LONDON | MEDIUM | 100.0 | 9 | 40 | 92.4 | 59.5 |
| LONDON | WEAK | 100.0 | 12 | 46 | 89.8 | 64.5 |
| ASIA | STRONG | 100.0 | 6 | 30 | 92.3 | 60.0 |
| ASIA | MEDIUM | 100.0 | 9 | 34 | 94.5 | 39.1 |
| ASIA | WEAK | 100.0 | 12 | 42 | 91.8 | 50.0 |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | MEDIUM - WEAK mean minutes to resolution, carried 24 h, resolved rows | -0.5 [-0.9, -0.1] (1552/3522) | -0.2 [-0.7, +0.3] (630/1392) | -0.7 [-1.2, -0.2] (922/2130) | DISCOVERY ONLY (H2) (d < 0) |
| NY | (MEDIUM - WEAK carried) - (MEDIUM - WEAK main): does a longer horizon close the gap? | -0.1 [-0.5, +0.2] (1552/3523) | -0.1 [-0.6, +0.4] (630/1393) | -0.1 [-0.6, +0.3] (922/2130) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK mean minutes to resolution, carried 24 h, resolved rows | -2.5 [-5.7, +0.5] (485/1049) | +0.8 [-2.4, +3.8] (212/467) | -5.2 [-10.1, -0.5] (273/582) | DISCOVERY ONLY (H2) (d < 0) |
| LONDON | (MEDIUM - WEAK carried) - (MEDIUM - WEAK main): does a longer horizon close the gap? | -0.1 [-1.5, +1.7] (485/1049) | -0.1 [-0.7, +0.4] (212/467) | -0.0 [-2.7, +3.1] (273/582) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK mean minutes to resolution, carried 24 h, resolved rows | -3.4 [-5.7, -0.8] (420/1243) | -4.1 [-7.8, +0.6] (182/461) | -3.1 [-5.5, -0.8] (238/782) | DISCOVERY ONLY (H2) (d < 0) |
| ASIA | (MEDIUM - WEAK carried) - (MEDIUM - WEAK main): does a longer horizon close the gap? | -0.1 [-0.7, +0.6] (420/1243) | -0.5 [-1.4, +0.4] (182/461) | +0.2 [-0.5, +1.2] (238/782) | NO DIFFERENCE SHOWN |

## 12. D-11: direction asymmetry

| Session | Tier | Side | n | Success % | Net EV main [95 % CI] |
|---|---|---|---|---|---|
| NY | STRONG | LONG | 136 | 51.5 | -1.2 [-5.3, +2.0] |
| NY | STRONG | SHORT | 206 | 42.2 | -4.6 [-6.7, -2.8] |
| NY | MEDIUM | LONG | 653 | 40.1 | -3.7 [-5.2, -2.4] |
| NY | MEDIUM | SHORT | 899 | 39.7 | -4.5 [-6.3, -2.9] |
| NY | WEAK | LONG | 1695 | 39.8 | -2.7 [-4.0, -1.4] |
| NY | WEAK | SHORT | 1828 | 40.4 | -3.7 [-4.6, -2.8] |
| LONDON | STRONG | LONG | 32 (NOT READABLE) | 53.1 | +6.0 [-5.0, +19.2] |
| LONDON | STRONG | SHORT | 99 (NOT READABLE) | 37.4 | -4.0 [-8.1, +0.2] |
| LONDON | MEDIUM | LONG | 180 | 50.6 | +0.7 [-4.3, +5.3] |
| LONDON | MEDIUM | SHORT | 305 | 43.6 | -1.7 [-3.6, -0.1] |
| LONDON | WEAK | LONG | 474 | 44.7 | -0.2 [-3.3, +2.6] |
| LONDON | WEAK | SHORT | 575 | 40.3 | -2.1 [-4.4, -0.1] |
| ASIA | STRONG | LONG | 23 (NOT READABLE) | 60.9 | +6.7 [-2.7, +12.8] |
| ASIA | STRONG | SHORT | 42 (NOT READABLE) | 66.7 | +0.3 [-3.6, +3.7] |
| ASIA | MEDIUM | LONG | 196 | 51.0 | -2.7 [-5.9, -0.2] |
| ASIA | MEDIUM | SHORT | 224 | 46.0 | -4.5 [-6.7, -2.5] |
| ASIA | WEAK | LONG | 633 | 43.1 | -3.5 [-5.5, -1.9] |
| ASIA | WEAK | SHORT | 610 | 50.5 | -0.9 [-3.4, +1.2] |

| Session | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | MEDIUM - WEAK, LONG only | -1.0 [-2.4, +0.4] (653/1695) | -0.8 [-3.0, +1.6] (324/767) | -1.2 [-2.9, +0.3] (329/928) | NO DIFFERENCE SHOWN |
| NY | MEDIUM - WEAK, SHORT only | -0.8 [-2.7, +1.0] (899/1828) | -1.7 [-4.5, +1.8] (306/626) | -0.4 [-2.7, +1.7] (593/1202) | NO DIFFERENCE SHOWN |
| NY | LONG - SHORT within STRONG | +3.4 [-1.0, +7.1] (136/206) | +3.6 [-0.0, +8.6] (62/75) | +3.2 [-10.0, +9.2] (74/131) | NO DIFFERENCE SHOWN |
| NY | LONG - SHORT within MEDIUM | +0.8 [-1.5, +3.2] (653/899) | +1.1 [-2.9, +4.8] (324/306) | +0.6 [-2.2, +3.3] (329/593) | NO DIFFERENCE SHOWN |
| NY | LONG - SHORT within WEAK | +1.0 [-0.6, +2.6] (1695/1828) | +0.3 [-1.6, +2.4] (767/626) | +1.5 [-1.1, +3.6] (928/1202) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK, LONG only | +0.9 [-4.1, +5.4] (180/474) | +3.8 [-1.7, +8.3] (94/238) | -1.9 [-9.7, +5.6] (86/236) | NO DIFFERENCE SHOWN |
| LONDON | MEDIUM - WEAK, SHORT only | +0.3 [-1.9, +2.5] (305/575) | +1.0 [-1.0, +3.5] (118/229) | -0.1 [-3.5, +3.3] (187/346) | NO DIFFERENCE SHOWN |
| LONDON | LONG - SHORT within STRONG | +10.1 [-2.3, +24.5] (32/99) | -4.4 [-10.5, +3.1] (18/57) | +28.8 [+8.8, +49.9] (14/42) | NOT READABLE |
| LONDON | LONG - SHORT within MEDIUM | +2.5 [-2.8, +7.3] (180/305) | +2.3 [-5.1, +7.5] (94/118) | +2.3 [-5.8, +10.5] (86/187) | NO DIFFERENCE SHOWN |
| LONDON | LONG - SHORT within WEAK | +1.9 [-2.4, +6.0] (474/575) | -0.4 [-5.3, +4.1] (238/229) | +4.2 [-2.1, +10.1] (236/346) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK, LONG only | +0.8 [-2.1, +3.5] (196/633) | -1.1 [-5.8, +2.5] (69/179) | +2.1 [-1.6, +5.7] (127/454) | NO DIFFERENCE SHOWN |
| ASIA | MEDIUM - WEAK, SHORT only | -3.6 [-6.1, -0.7] (224/610) | -4.9 [-8.0, -1.5] (113/282) | -2.4 [-6.2, +2.3] (111/328) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| ASIA | LONG - SHORT within STRONG | +6.5 [-4.1, +13.6] (23/42) | -1.9 [-15.8, +12.2] (2/16) | +6.7 [-5.1, +15.2] (21/26) | NOT READABLE |
| ASIA | LONG - SHORT within MEDIUM | +1.8 [-2.0, +5.1] (196/224) | -0.9 [-7.2, +4.4] (69/113) | +3.0 [-1.6, +7.2] (127/111) | NO DIFFERENCE SHOWN |
| ASIA | LONG - SHORT within WEAK | -2.6 [-5.4, +0.3] (633/610) | -4.7 [-7.9, -0.4] (179/282) | -1.5 [-5.3, +2.5] (454/328) | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |

## 13. D-12: era edges

- ⚠ The stability read's H1/H2 split date (2026-08-10 00:00 UTC) sits 18.6 h before the v66 collector deploy (2026-08-10 18:36:01 UTC). H1 versus H2 is therefore almost the same cut as before versus after v66. A cause CONFIRMED on both halves holds across v66; a cause seen in one half only may be an era effect.

| Session | Segment | Rows | MEDIUM n | WEAK n | MEDIUM EV | WEAK EV | d = MEDIUM - WEAK [95 % CI] | Readable | Burst flag rate MEDIUM / WEAK % | OBV vote fire % | Volume vote fire % |
|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | from 2026-07-06 13:08 (v51 edge) | 505 | 159 | 320 | -2.3 | -2.4 | +0.0 [-2.0, +2.8] | yes | 0.0 / 0.0 | 72.3 | 0.0 |
| NY | from 2026-07-14 14:19 (v52 NY burst modifier armed) | 1739 | 506 | 1106 | -4.5 | -2.9 | -1.6 [-3.3, +0.5] | yes | 6.9 / 7.1 | 72.2 | 0.0 |
| NY | from 2026-08-10 18:36 (v66 OBV trend_gate 18 -> 23 (collector deploy)) | 651 | 200 | 388 | -2.9 | -1.1 | -1.8 [-3.4, +1.3] | yes | 7.0 / 5.4 | 83.4 | 0.2 |
| NY | from 2026-08-20 00:00 (ATR step 2026-08-20) | 2522 | 687 | 1709 | -4.8 | -4.0 | -0.7 [-3.0, +1.3] | yes | 4.9 / 3.1 | 61.0 | 0.1 |
| LONDON | from 2026-07-06 13:08 (v51 edge) | 303 | 75 | 197 | +0.1 | -3.7 | +3.8 [-1.4, +9.6] | NOT READABLE | 0.0 / 0.0 | 75.9 | 0.3 |
| LONDON | from 2026-07-22 17:35 (v60 LONDON burst modifier armed) | 465 | 143 | 276 | -0.2 | -1.3 | +1.0 [-1.0, +3.0] | yes | 14.7 / 9.4 | 61.1 | 0.0 |
| LONDON | from 2026-08-10 18:36 (v66 OBV trend_gate (collector deploy)) | 101 | 29 | 64 | -4.5 | -3.5 | -1.0 [-5.9, +5.3] | NOT READABLE | 17.2 / 9.4 | 48.5 | 0.0 |
| LONDON | from 2026-08-20 00:00 (ATR step 2026-08-20) | 796 | 238 | 512 | -1.0 | +0.1 | -1.0 [-5.2, +2.9] | yes | 4.2 / 3.1 | 44.7 | 0.0 |
| ASIA | from 2026-07-06 13:08 (v51 edge) | 124 | 39 | 81 | -2.9 | +0.9 | -3.8 [-7.4, -1.0] | NOT READABLE | 0.0 / 0.0 | 82.3 | 0.0 |
| ASIA | from 2026-07-22 09:35 (v58 ASIA volume multipliers -> 1.00) | 332 | 86 | 235 | -5.9 | -3.1 | -2.8 [-5.0, -0.7] | NOT READABLE | 0.0 / 0.0 | 50.0 | 0.0 |
| ASIA | from 2026-08-01 18:45 (v65 ASIA burst modifier armed) | 205 | 57 | 145 | -6.6 | -2.1 | -4.6 [-10.8, +1.7] | NOT READABLE | 15.8 / 9.7 | 38.5 | 0.0 |
| ASIA | from 2026-08-10 18:36 (v66 OBV trend_gate (collector deploy)) | 103 | 16 | 84 | -0.5 | -3.0 | +2.5 [-2.6, +7.2] | NOT READABLE | 18.8 / 14.3 | 46.6 | 0.0 |
| ASIA | from 2026-08-20 00:00 (ATR step 2026-08-20) | 964 | 222 | 698 | -2.4 | -2.2 | -0.2 [-3.6, +3.1] | yes | 4.5 / 3.9 | 52.3 | 0.0 |

## 13a. Candidate-cause explanation sets on MEDIUM rows (descriptive)

- Predicates fixed after a 200-resample pilot run of this script, before the 10,000-resample run. Descriptive only: the labels in the sections above decide CONFIRMED or not.

| Session | Candidate cause | MEDIUM rows in set | % | EV in set | EV outside set |
|---|---|---|---|---|---|
| NY | TRANSITIONAL ADX penalty applied | 208 | 13.4 | -4.8 | -4.1 |
| NY | decaying path (previous episode signal STRONG) | 162 | 10.4 | -5.7 | -4.0 |
| NY | top VWAP-extension tercile | 495 | 31.9 | -3.9 | -4.3 |
| NY | ROC vote agrees | 1256 | 80.9 | -4.2 | -4.4 |
| NY | BBW/TTM vote agrees | 1139 | 73.4 | -4.3 | -3.8 |
| NY | VPFR vote opposes | 596 | 38.4 | -3.7 | -4.5 |
| NY | lifted by Pass 2 partial upgrades | 1178 | 75.9 | -4.0 | -5.0 |
| LONDON | TRANSITIONAL ADX penalty applied | 48 | 9.9 | -3.7 | -0.5 |
| LONDON | decaying path (previous episode signal STRONG) | 62 | 12.8 | -2.5 | -0.5 |
| LONDON | top VWAP-extension tercile | 115 | 23.7 | +0.0 | -1.1 |
| LONDON | ROC vote agrees | 398 | 82.1 | -0.8 | -0.8 |
| LONDON | BBW/TTM vote agrees | 355 | 73.2 | -0.5 | -1.6 |
| LONDON | VPFR vote opposes | 119 | 24.5 | -2.8 | -0.2 |
| LONDON | lifted by Pass 2 partial upgrades | 370 | 76.3 | -0.8 | -0.8 |
| ASIA | TRANSITIONAL ADX penalty applied | 54 | 12.9 | -1.8 | -3.9 |
| ASIA | decaying path (previous episode signal STRONG) | 27 | 6.4 | -2.7 | -3.7 |
| ASIA | top VWAP-extension tercile | 121 | 28.8 | -3.3 | -3.8 |
| ASIA | ROC vote agrees | 294 | 70.0 | -3.7 | -3.5 |
| ASIA | BBW/TTM vote agrees | 294 | 70.0 | -2.6 | -6.2 |
| ASIA | VPFR vote opposes | 112 | 26.7 | -3.6 | -3.7 |
| ASIA | lifted by Pass 2 partial upgrades | 311 | 74.0 | -3.8 | -3.3 |

| Session | MEDIUM rows in 0 / 1 / 2 / 3 / 4+ candidate sets | EV by set count 0 / 1 / 2 / 3 / 4+ |
|---|---|---|
| NY | 19 / 69 / 289 / 533 / 642 | +0.9 / -5.2 / -4.2 / -4.5 / -4.0 |
| LONDON | 6 / 34 / 116 / 165 / 164 | -13.2 / -3.8 / +1.6 / +1.0 / -3.2 |
| ASIA | 21 / 44 / 79 / 138 / 138 | -10.3 / +0.4 / -7.1 / -2.6 / -3.0 |

## 14. Sensitivity: rows whose re-score matches all six fields only

| Session | Vote or statistic | Comparison | FULL d [95 % CI] (n) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|---|
| NY | tier gap | MEDIUM - WEAK | -0.9 [-2.1, +0.3] (1535/3499) | -1.2 [-2.7, +0.6] (622/1381) | -0.8 [-2.5, +0.9] (913/2118) | NO DIFFERENCE SHOWN |
| NY | VPFR | agree - oppose | -0.2 [-1.6, +1.1] (2176/1985) | +0.9 [-0.7, +2.4] (867/854) | -1.1 [-2.9, +0.8] (1309/1131) | NO DIFFERENCE SHOWN |
| LONDON | tier gap | MEDIUM - WEAK | +0.6 [-1.8, +3.0] (482/1045) | +2.4 [-0.0, +5.1] (211/465) | -0.9 [-4.6, +2.9] (271/580) | NO DIFFERENCE SHOWN |
| LONDON | VPFR | agree - oppose | +1.2 [-1.9, +4.3] (771/450) | +3.2 [-0.4, +6.5] (363/217) | -0.6 [-5.4, +4.2] (408/233) | NO DIFFERENCE SHOWN |
| ASIA | tier gap | MEDIUM - WEAK | -1.4 [-3.5, +0.7] (419/1236) | -3.4 [-6.0, -1.1] (182/458) | +0.1 [-3.0, +3.0] (237/778) | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| ASIA | VPFR | agree - oppose | +1.3 [-1.5, +3.7] (781/484) | -0.3 [-4.3, +3.0] (329/201) | +2.4 [-1.5, +5.4] (452/283) | NO DIFFERENCE SHOWN |

## 15. Every labelled comparison

- Labelled comparisons: 780. CONFIRMED 21; DISCOVERY ONLY 29; H1 FINDING, H2 CONTRADICTS OR NOT READABLE 12; H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT 22; NO DIFFERENCE SHOWN 361; NOT READABLE 335.
- Multiplicity: under no true effect, a readable comparison reaches CONFIRMED with probability about 2 x 0.025 x 0.025 = 0.00125 (independent halves). Expected false CONFIRMED over 445 readable comparisons: 0.56.

| Section | Comparison | FULL d [95 % CI] | Label |
|---|---|---|---|
| 1 baseline main ASIA | MEDIUM - WEAK | -1.5 [-3.5, +0.6] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 1 baseline C24 ASIA | MEDIUM - WEAK | -1.5 [-4.0, +1.0] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 4 D-3 main NY ROC | agree - oppose | -3.3 [-5.4, -1.1] | DISCOVERY ONLY (H2) (d < 0) |
| 4 D-3 main NY BBW_TTM | agree - oppose | -2.1 [-3.6, -0.5] | DISCOVERY ONLY (H2) (d < 0) |
| 4 D-3 main NY EMA | agree - absent | +0.1 [-2.1, +2.1] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 4 D-3 main NY FUNDING | agree - oppose | -1.5 [-3.1, -0.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 4 D-3 main NY OFI | agree - oppose | +1.8 [+0.6, +3.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 main NY OFI | agree - absent | +1.4 [+0.4, +2.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 main NY TFI | agree - oppose | +0.7 [-0.0, +1.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 main ASIA VWAP | agree - absent | +2.7 [+0.1, +5.2] | DISCOVERY ONLY (H2) (d > 0) |
| 4 D-3 main ASIA OFI | agree - oppose | +2.4 [+0.5, +4.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 C24 NY ROC | agree - oppose | -3.2 [-5.2, -0.9] | DISCOVERY ONLY (H2) (d < 0) |
| 4 D-3 C24 NY BBW_TTM | agree - oppose | -2.2 [-3.8, -0.6] | DISCOVERY ONLY (H2) (d < 0) |
| 4 D-3 C24 NY EMA | agree - absent | +0.5 [-1.7, +2.8] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 4 D-3 C24 NY FUNDING | agree - oppose | -2.0 [-3.7, -0.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 4 D-3 C24 NY OFI | agree - oppose | +1.6 [+0.3, +2.8] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 C24 NY OFI | agree - absent | +1.1 [-0.2, +2.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 C24 NY TFI | agree - oppose | +0.8 [-0.1, +1.6] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 C24 LONDON OFI | agree - oppose | -0.2 [-3.0, +2.5] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0) |
| 4 D-3 C24 ASIA RSI | agree - absent | +0.3 [-3.5, +4.8] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 4 D-3 C24 ASIA VWAP | agree - absent | +2.0 [-0.8, +4.8] | DISCOVERY ONLY (H2) (d > 0) |
| 4 D-3 C24 ASIA BBW_TTM | agree - absent | +2.5 [-0.1, +5.4] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4 D-3 C24 ASIA OFI | agree - oppose | +3.0 [+1.3, +4.8] | CONFIRMED (d > 0) |
| 4.3 D-3 within-tier main NY ROC | agree - oppose within tier | -2.4 [-4.5, -0.1] | DISCOVERY ONLY (H2) (d < 0) |
| 4.3 D-3 within-tier main NY BBW_TTM | agree - oppose within tier | -1.5 [-3.6, -0.2] | DISCOVERY ONLY (H2) (d < 0) |
| 4.3 D-3 within-tier main NY FUNDING | agree - oppose within tier | -1.4 [-3.0, +0.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 4.3 D-3 within-tier main NY OFI | agree - oppose within tier | +1.9 [+0.7, +3.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.3 D-3 within-tier main NY TFI | agree - oppose within tier | +1.0 [+0.1, +1.9] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.3 D-3 within-tier main LONDON EMA200 | agree - oppose within tier | +5.1 [+1.7, +8.9] | DISCOVERY ONLY (H2) (d > 0) |
| 4.3 D-3 within-tier main ASIA OFI | agree - oppose within tier | +2.2 [+0.4, +4.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.5 D-3 lift NY DMI | lifted - other MEDIUM | +1.4 [+0.0, +2.9] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY ADX | lifted - other MEDIUM | +0.8 [-0.5, +2.0] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY VWAP | lifted - other MEDIUM | +2.0 [+0.5, +3.4] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY EMA | lifted - other MEDIUM | +1.5 [+0.0, +3.0] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY CVD | lifted - other MEDIUM | +0.6 [-1.2, +2.4] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY TFI | lifted - other MEDIUM | +2.4 [+0.5, +4.2] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY EMA200 | lifted - other MEDIUM | +1.6 [+0.1, +3.1] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY VPFR | lifted - other MEDIUM | +1.8 [-0.3, +3.8] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY REGIME_ALIGN | lifted - other MEDIUM | +1.2 [-0.9, +3.2] | DISCOVERY ONLY (H2) (d > 0) |
| 4.5 D-3 lift NY TREND_STRUCTURE | lifted - other MEDIUM | +1.1 [-1.1, +3.2] | DISCOVERY ONLY (H2) (d > 0) |
| 4.4 VPFR NY NEAR_HVN main | agree - oppose | +1.5 [-0.5, +3.4] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.4 VPFR NY NEAR_HVN C24 | agree - oppose | +1.9 [-0.2, +4.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.4 VPFR within-tier NY NEAR_HVN | agree - oppose within tier | +1.4 [-0.5, +3.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) |
| 4.4 VPFR NY NEAR_HVN oppose-neutral | oppose - NEUTRAL | +1.0 [-0.5, +2.6] | DISCOVERY ONLY (H2) (d > 0) |
| 4.4 VPFR NY NEAR_HVN LONG-only | agree - oppose | +5.0 [+2.4, +7.2] | CONFIRMED (d > 0) |
| 4.4 VPFR NY ALL labels oppose-neutral | oppose - NEUTRAL | +1.3 [-0.3, +2.8] | DISCOVERY ONLY (H2) (d > 0) |
| 4.4 VPFR NY ALL labels SHORT-only | agree - oppose | -1.9 [-3.8, -0.2] | DISCOVERY ONLY (H2) (d < 0) |
| 4.4 VPFR LONDON NEAR_HVN oppose-neutral | oppose - NEUTRAL | -1.1 [-4.5, +2.2] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 4.4 VPFR LONDON ALL labels oppose-neutral | oppose - NEUTRAL | -1.1 [-4.4, +2.3] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 5 D-4 NY P2C_ALIGN WEAK->MEDIUM | Ka | +1.4 [-1.0, +3.8] | DISCOVERY ONLY (H2) (d > 0) |
| 5 D-4 NY TS_BONUS WEAK->MEDIUM | Kb | +0.4 [-2.2, +2.9] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 8 D-7 ASIA regime x side x ATR regime x target type | MEDIUM reweighted - WEAK (regime x side x ATR regime x target type) | -1.7 [-4.4, +0.9] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 8 D-7 ASIA regime x hour bucket x side | MEDIUM reweighted - WEAK (regime x hour bucket x side) | -2.1 [-4.1, -0.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 8 D-7 ASIA regime x side x ATR regime x target type x ExecResolution | MEDIUM reweighted - WEAK (regime x side x ATR regime x target type x ExecResolution) | -1.7 [-4.3, +0.9] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |
| 8 D-7 ASIA VWAP-extension tercile x side | MEDIUM reweighted - WEAK (VWAP-extension tercile x side) | -1.6 [-3.8, +0.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 8 D-7 ASIA RSI tercile x EMA21-extension tercile x side | MEDIUM reweighted - WEAK (RSI tercile x EMA21-extension tercile x side) | -2.0 [-4.9, +1.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 9.1 D-8 NY f_vwap_atr | MEDIUM - WEAK mean f_vwap_atr | +0.6 [+0.3, +1.0] | CONFIRMED (d > 0) |
| 9.1 D-8 NY f_ema21_atr | MEDIUM - WEAK mean f_ema21_atr | +1.0 [+0.9, +1.1] | CONFIRMED (d > 0) |
| 9.1 D-8 NY f_roc | MEDIUM - WEAK mean f_roc | +0.1 [+0.1, +0.1] | CONFIRMED (d > 0) |
| 9.1 D-8 NY f_rsi | MEDIUM - WEAK mean f_rsi | +8.9 [+8.0, +9.8] | CONFIRMED (d > 0) |
| 9.1 D-8 NY f_room_atr | MEDIUM - WEAK mean f_room_atr | -1.7 [-2.1, -1.3] | CONFIRMED (d < 0) |
| 9.1 D-8 NY f_ttm_atr | MEDIUM - WEAK mean f_ttm_atr | +1.3 [+1.1, +1.4] | CONFIRMED (d > 0) |
| 9.1 D-8 LONDON f_vwap_atr | MEDIUM - WEAK mean f_vwap_atr | +1.0 [+0.4, +1.7] | CONFIRMED (d > 0) |
| 9.1 D-8 LONDON f_ema21_atr | MEDIUM - WEAK mean f_ema21_atr | +0.8 [+0.6, +0.9] | CONFIRMED (d > 0) |
| 9.1 D-8 LONDON f_roc | MEDIUM - WEAK mean f_roc | +0.1 [+0.1, +0.1] | CONFIRMED (d > 0) |
| 9.1 D-8 LONDON f_rsi | MEDIUM - WEAK mean f_rsi | +7.5 [+6.3, +8.8] | CONFIRMED (d > 0) |
| 9.1 D-8 LONDON f_swing_ext_atr | MEDIUM - WEAK mean f_swing_ext_atr | +0.5 [+0.1, +0.8] | DISCOVERY ONLY (H2) (d > 0) |
| 9.1 D-8 LONDON f_room_atr | MEDIUM - WEAK mean f_room_atr | -1.7 [-2.1, -1.3] | CONFIRMED (d < 0) |
| 9.1 D-8 LONDON f_ttm_atr | MEDIUM - WEAK mean f_ttm_atr | +1.0 [+0.8, +1.2] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_vwap_atr | MEDIUM - WEAK mean f_vwap_atr | +0.6 [+0.3, +0.9] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_ema21_atr | MEDIUM - WEAK mean f_ema21_atr | +0.8 [+0.7, +0.9] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_roc | MEDIUM - WEAK mean f_roc | +0.1 [+0.1, +0.2] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_rsi | MEDIUM - WEAK mean f_rsi | +7.3 [+6.3, +8.5] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_swing_ext_atr | MEDIUM - WEAK mean f_swing_ext_atr | +0.9 [+0.5, +1.2] | CONFIRMED (d > 0) |
| 9.1 D-8 ASIA f_room_atr | MEDIUM - WEAK mean f_room_atr | -1.5 [-1.9, -1.2] | CONFIRMED (d < 0) |
| 9.1 D-8 ASIA f_ttm_atr | MEDIUM - WEAK mean f_ttm_atr | +1.0 [+0.8, +1.2] | CONFIRMED (d > 0) |
| 9.2 D-8 NY f_vwap_atr | top - bottom tercile | -1.7 [-3.5, -0.1] | DISCOVERY ONLY (H2) (d < 0) |
| 9.2 D-8 NY f_roc | top - bottom tercile | -1.6 [-3.2, +0.2] | DISCOVERY ONLY (H2) (d < 0) |
| 11.2 D-10 NY | MEDIUM - WEAK minutes to resolution (carried) | -0.5 [-0.9, -0.1] | DISCOVERY ONLY (H2) (d < 0) |
| 11.2 D-10 LONDON | MEDIUM - WEAK minutes to resolution (carried) | -2.5 [-5.7, +0.5] | DISCOVERY ONLY (H2) (d < 0) |
| 11.2 D-10 ASIA | MEDIUM - WEAK minutes to resolution (carried) | -3.4 [-5.7, -0.8] | DISCOVERY ONLY (H2) (d < 0) |
| 12 D-11 ASIA | MEDIUM - WEAK, SHORT | -3.6 [-6.1, -0.7] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 12 D-11 ASIA | LONG - SHORT within WEAK | -2.6 [-5.4, +0.3] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d < 0) |
| 14 sens ASIA | MEDIUM - WEAK (matching rows) | -1.4 [-3.5, +0.7] | H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) |

