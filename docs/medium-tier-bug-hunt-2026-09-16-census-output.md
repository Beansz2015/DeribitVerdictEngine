# SwingFallbackRead output: --mode census (tier-demotion census)

- Run at (UTC): 2026-09-16 19:50:28
- Pooled log: C:\Dev\DeribitVerdictEngine\AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv
- Box logs: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv.v0.7.bak + C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv
- Eval cache: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_eval_cache.csv
- settings.json version 68, sha256 A059DEC578D8B4C7 (copy identical)
- Session ASIA: hours 0-7 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 1.25xATR
- Session LONDON: hours 8-12 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 2xATR
- Session NY: hours 13-23 UTC inclusive, execution_resolution 1, windows 5/10/15 min, fallback target 1.75xATR
- Trading week: Monday 00:00 UTC to Friday 24:00 UTC (exclusive)
- Fees: style maker_maker, round trip 3.00 bps (maker 1.5, taker 3.5). Taker-stop case: 3.00 bps on a target hit, 5.00 bps on a stop hit or a marked exit

- Brief: docs/medium-tier-diagnosis-brief-2026-09-16.md section 2.4. Read: docs/medium-tier-bug-hunt-2026-09-16.md. Rules fixed before the first run: this instrument's header.
- Thresholds: Ceiling(MaxScore x 0.7 / 0.53 / 0.35). Tier floor: raw >= 12 -> 9, >= 9 -> 6, >= 6 -> 3. TRANSITIONAL penalty 2 below ADX 22.5, 1 below ADX 25.
- Bootstrap: 10000 resamples of whole UTC trading days, seed 20260916 + group index, percentile 95 % CI. Readable: n >= 100 in both cells.

## 1. Self-checks

| Check | Rows | Result |
|---|---|---|
| Effective tier from the logged effective score equals the logged verdict tier | 8810 directional rows | 0 mismatches |
| Verdict side: effective = raw - RegimePenalty (demotion is the ADX penalty alone) | 8810 directional rows | 0 rows differ |
| TRANSITIONAL rows where the TierFloor arm wins with a floor above zero (either side) | 7965 TRANSITIONAL rows | 0 |
| TRANSITIONAL side scores below the penalty, held at 0 (raw < penalty, TierFloor 0: the Max(.., 0) arm) | 7965 TRANSITIONAL rows | 2235 side scores |
| TRANSITIONAL side scores where effective matches neither raw - penalty, nor the floor, nor 0 | 7965 TRANSITIONAL rows | 0 side scores |
| Non-TRANSITIONAL directional rows with RegimePenalty <> 0 | 7928 | 0 |
| Directional rows outside the six classes (raw tier below the effective tier, or unclassifiable) | 8810 | 0 |
| Population rows matched to a logged row | 8810 population signals | 8810 matched |

- Tier-floor arithmetic: the floor binds only when TierFloor(raw) > raw - penalty. TierFloor(raw) <= raw - 3 for every raw >= 6 (12 -> 9, 9 -> 6, 6 -> 3, and the gap grows inside each band), and the penalty is at most 2. So at the tracked values the floor cannot bind, and the demotion is the ADX penalty alone.

## 2. Census counts: every trading-week directional row

| Session | Regime | Rows | native STRONG | demoted-to-MEDIUM | native MEDIUM | demoted-to-WEAK | native WEAK | demoted two tiers | Unchanged | Demoted share % |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | TRENDING_UP | 2051 | 128 | 0 | 528 | 0 | 1395 | 0 | 2051 | 0.0 |
| NY | TRENDING_DOWN | 2305 | 189 | 0 | 685 | 0 | 1431 | 0 | 2305 | 0.0 |
| NY | RANGE_BOUND | 512 | 11 | 0 | 131 | 0 | 370 | 0 | 512 | 0.0 |
| NY | TRANSITIONAL | 549 | 14 | 66 | 142 | 220 | 107 | 0 | 263 | 52.1 |
| LONDON | TRENDING_UP | 625 | 30 | 0 | 167 | 0 | 428 | 0 | 625 | 0.0 |
| LONDON | TRENDING_DOWN | 785 | 91 | 0 | 246 | 0 | 448 | 0 | 785 | 0.0 |
| LONDON | RANGE_BOUND | 103 | 2 | 0 | 24 | 0 | 77 | 0 | 103 | 0.0 |
| LONDON | TRANSITIONAL | 152 | 8 | 18 | 30 | 65 | 31 | 0 | 69 | 54.6 |
| ASIA | TRENDING_UP | 705 | 20 | 0 | 156 | 0 | 529 | 0 | 705 | 0.0 |
| ASIA | TRENDING_DOWN | 681 | 34 | 0 | 185 | 0 | 462 | 0 | 681 | 0.0 |
| ASIA | RANGE_BOUND | 161 | 2 | 0 | 25 | 0 | 134 | 0 | 161 | 0.0 |
| ASIA | TRANSITIONAL | 181 | 9 | 11 | 43 | 75 | 43 | 0 | 95 | 47.5 |

| Session | Demoted out of WEAK into NO TRADE by the penalty alone (not in the population, counts only) |
|---|---|
| NY | 1376 |
| LONDON | 183 |
| ASIA | 297 |

## 3. Census headline: the swing read population

- Every trading-week directional row (8810) is in the population, so the section 2 counts are the population counts.

| Session | Rows | TRANSITIONAL rows | native STRONG | demoted-to-MEDIUM | native MEDIUM | demoted-to-WEAK | native WEAK | demoted two tiers | Demoted share of all rows % | Demoted share of TRANSITIONAL rows % |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | 5417 | 549 | 342 | 66 | 1486 | 220 | 3303 | 0 | 5.3 | 52.1 |
| LONDON | 1665 | 152 | 131 | 18 | 467 | 65 | 984 | 0 | 5.0 | 54.6 |
| ASIA | 1728 | 181 | 65 | 11 | 409 | 75 | 1168 | 0 | 5.0 | 47.5 |
| ALL (counts only, never pooled for outcomes) | 8810 | 882 | 538 | 95 | 2362 | 360 | 5455 | 0 | 5.2 | 51.6 |

## 4. Outcomes per class (net EV per trade, bps, maker/maker; 95 % bootstrap CI by trading day)

- Split date 2026-08-10 00:00 UTC: H1 = 24 trading days, H2 = 25 (the stability read's rule).

### 4.1 main window

| Session | Sub-sample | Class | Scope | n | Net EV [95 % CI] |
|---|---|---|---|---|---|
| NY | FULL | native STRONG | every regime | 342 | -3.2 [-5.3, -1.4] |
| NY | FULL | demoted-to-MEDIUM | every regime | 66 (NOT READABLE) | -5.5 [-9.5, -1.4] |
| NY | FULL | native MEDIUM | every regime | 1486 | -4.1 [-5.3, -3.1] |
| NY | FULL | demoted-to-WEAK | every regime | 220 | -3.0 [-5.5, -0.4] |
| NY | FULL | native WEAK | every regime | 3303 | -3.2 [-4.1, -2.3] |
| NY | FULL | native STRONG | TRANSITIONAL | 14 (NOT READABLE) | -5.9 [-12.0, -0.3] |
| NY | FULL | demoted-to-MEDIUM | TRANSITIONAL | 66 (NOT READABLE) | -5.5 [-9.4, -1.3] |
| NY | FULL | native MEDIUM | TRANSITIONAL | 142 | -4.6 [-7.3, -1.8] |
| NY | FULL | demoted-to-WEAK | TRANSITIONAL | 220 | -3.0 [-5.6, -0.4] |
| NY | FULL | native WEAK | TRANSITIONAL | 107 | -1.9 [-5.4, +1.5] |
| NY | H1 | native STRONG | every regime | 137 | -3.3 [-5.2, -1.5] |
| NY | H1 | demoted-to-MEDIUM | every regime | 27 (NOT READABLE) | -2.4 [-6.4, +1.0] |
| NY | H1 | native MEDIUM | every regime | 603 | -4.2 [-5.5, -2.8] |
| NY | H1 | demoted-to-WEAK | every regime | 89 (NOT READABLE) | -1.8 [-6.1, +3.5] |
| NY | H1 | native WEAK | every regime | 1304 | -2.9 [-4.3, -1.7] |
| NY | H1 | native STRONG | TRANSITIONAL | 4 (NOT READABLE) | -7.7 [-13.7, +5.8] |
| NY | H1 | demoted-to-MEDIUM | TRANSITIONAL | 27 (NOT READABLE) | -2.4 [-6.3, +0.9] |
| NY | H1 | native MEDIUM | TRANSITIONAL | 56 (NOT READABLE) | -4.9 [-10.5, +0.3] |
| NY | H1 | demoted-to-WEAK | TRANSITIONAL | 89 (NOT READABLE) | -1.8 [-6.2, +3.5] |
| NY | H1 | native WEAK | TRANSITIONAL | 33 (NOT READABLE) | +0.5 [-5.9, +6.3] |
| NY | H2 | native STRONG | every regime | 205 | -3.2 [-6.6, -0.4] |
| NY | H2 | demoted-to-MEDIUM | every regime | 39 (NOT READABLE) | -7.5 [-12.6, -0.9] |
| NY | H2 | native MEDIUM | every regime | 883 | -4.1 [-5.8, -2.7] |
| NY | H2 | demoted-to-WEAK | every regime | 131 | -3.9 [-6.7, -1.2] |
| NY | H2 | native WEAK | every regime | 1999 | -3.4 [-4.6, -2.1] |
| NY | H2 | native STRONG | TRANSITIONAL | 10 (NOT READABLE) | -5.2 [-14.2, +1.9] |
| NY | H2 | demoted-to-MEDIUM | TRANSITIONAL | 39 (NOT READABLE) | -7.5 [-12.8, -0.7] |
| NY | H2 | native MEDIUM | TRANSITIONAL | 86 (NOT READABLE) | -4.3 [-7.4, -1.2] |
| NY | H2 | demoted-to-WEAK | TRANSITIONAL | 131 | -3.9 [-6.8, -1.1] |
| NY | H2 | native WEAK | TRANSITIONAL | 74 (NOT READABLE) | -2.9 [-7.0, +0.8] |
| LONDON | FULL | native STRONG | every regime | 131 | -1.6 [-5.9, +3.2] |
| LONDON | FULL | demoted-to-MEDIUM | every regime | 18 (NOT READABLE) | -8.5 [-14.2, +0.3] |
| LONDON | FULL | native MEDIUM | every regime | 467 | -0.5 [-2.8, +1.7] |
| LONDON | FULL | demoted-to-WEAK | every regime | 65 (NOT READABLE) | -1.4 [-5.5, +3.1] |
| LONDON | FULL | native WEAK | every regime | 984 | -1.2 [-2.7, +0.2] |
| LONDON | FULL | native STRONG | TRANSITIONAL | 8 (NOT READABLE) | -2.1 [-16.5, +8.8] |
| LONDON | FULL | demoted-to-MEDIUM | TRANSITIONAL | 18 (NOT READABLE) | -8.5 [-14.0, +0.3] |
| LONDON | FULL | native MEDIUM | TRANSITIONAL | 30 (NOT READABLE) | -0.8 [-5.7, +4.5] |
| LONDON | FULL | demoted-to-WEAK | TRANSITIONAL | 65 (NOT READABLE) | -1.4 [-5.4, +3.0] |
| LONDON | FULL | native WEAK | TRANSITIONAL | 31 (NOT READABLE) | -1.0 [-5.8, +4.0] |
| LONDON | H1 | native STRONG | every regime | 75 (NOT READABLE) | -3.8 [-7.7, +0.1] |
| LONDON | H1 | demoted-to-MEDIUM | every regime | 5 (NOT READABLE) | -1.3 [-15.4, +10.4] |
| LONDON | H1 | native MEDIUM | every regime | 207 | 0.0 [-3.2, +2.9] |
| LONDON | H1 | demoted-to-WEAK | every regime | 39 (NOT READABLE) | -3.4 [-7.0, +0.1] |
| LONDON | H1 | native WEAK | every regime | 428 | -2.2 [-4.5, -0.3] |
| LONDON | H1 | native STRONG | TRANSITIONAL | 3 (NOT READABLE) | -4.5 [-18.3, +16.9] |
| LONDON | H1 | demoted-to-MEDIUM | TRANSITIONAL | 5 (NOT READABLE) | -1.3 [-15.8, +10.1] |
| LONDON | H1 | native MEDIUM | TRANSITIONAL | 17 (NOT READABLE) | -1.7 [-7.2, +4.8] |
| LONDON | H1 | demoted-to-WEAK | TRANSITIONAL | 39 (NOT READABLE) | -3.4 [-7.0, +0.1] |
| LONDON | H1 | native WEAK | TRANSITIONAL | 14 (NOT READABLE) | -7.5 [-11.8, -2.3] |
| LONDON | H2 | native STRONG | every regime | 56 (NOT READABLE) | +1.4 [-6.7, +12.0] |
| LONDON | H2 | demoted-to-MEDIUM | every regime | 13 (NOT READABLE) | -11.3 [-17.4, 0.0] |
| LONDON | H2 | native MEDIUM | every regime | 260 | -0.9 [-4.2, +2.2] |
| LONDON | H2 | demoted-to-WEAK | every regime | 26 (NOT READABLE) | +1.7 [-7.1, +10.4] |
| LONDON | H2 | native WEAK | every regime | 556 | -0.4 [-2.4, +1.6] |
| LONDON | H2 | native STRONG | TRANSITIONAL | 5 (NOT READABLE) | -0.7 [-19.1, +14.0] |
| LONDON | H2 | demoted-to-MEDIUM | TRANSITIONAL | 13 (NOT READABLE) | -11.3 [-17.2, -0.8] |
| LONDON | H2 | native MEDIUM | TRANSITIONAL | 13 (NOT READABLE) | +0.3 [-8.4, +10.4] |
| LONDON | H2 | demoted-to-WEAK | TRANSITIONAL | 26 (NOT READABLE) | +1.7 [-7.1, +10.1] |
| LONDON | H2 | native WEAK | TRANSITIONAL | 17 (NOT READABLE) | +4.4 [-1.7, +9.6] |
| ASIA | FULL | native STRONG | every regime | 65 (NOT READABLE) | +2.5 [-2.0, +6.7] |
| ASIA | FULL | demoted-to-MEDIUM | every regime | 11 (NOT READABLE) | +1.2 [-8.8, +10.4] |
| ASIA | FULL | native MEDIUM | every regime | 409 | -3.8 [-5.6, -2.1] |
| ASIA | FULL | demoted-to-WEAK | every regime | 75 (NOT READABLE) | +0.3 [-3.3, +3.8] |
| ASIA | FULL | native WEAK | every regime | 1168 | -2.4 [-4.1, -0.8] |
| ASIA | FULL | native STRONG | TRANSITIONAL | 9 (NOT READABLE) | +2.5 [-8.6, +13.0] |
| ASIA | FULL | demoted-to-MEDIUM | TRANSITIONAL | 11 (NOT READABLE) | +1.2 [-8.5, +10.5] |
| ASIA | FULL | native MEDIUM | TRANSITIONAL | 43 (NOT READABLE) | -2.6 [-7.8, +2.7] |
| ASIA | FULL | demoted-to-WEAK | TRANSITIONAL | 75 (NOT READABLE) | +0.3 [-3.4, +3.7] |
| ASIA | FULL | native WEAK | TRANSITIONAL | 43 (NOT READABLE) | -1.3 [-6.7, +4.2] |
| ASIA | H1 | native STRONG | every regime | 18 (NOT READABLE) | -1.0 [-6.1, +3.6] |
| ASIA | H1 | demoted-to-MEDIUM | every regime | 2 (NOT READABLE) | +9.7 [+7.1, +12.3] |
| ASIA | H1 | native MEDIUM | every regime | 180 | -5.7 [-7.3, -3.9] |
| ASIA | H1 | demoted-to-WEAK | every regime | 19 (NOT READABLE) | -2.5 [-8.3, +4.6] |
| ASIA | H1 | native WEAK | every regime | 442 | -2.0 [-4.3, +0.2] |
| ASIA | H1 | native STRONG | TRANSITIONAL | 2 (NOT READABLE) | -15.2 [-16.3, -14.0] |
| ASIA | H1 | demoted-to-MEDIUM | TRANSITIONAL | 2 (NOT READABLE) | +9.7 [+7.1, +12.3] |
| ASIA | H1 | native MEDIUM | TRANSITIONAL | 21 (NOT READABLE) | -5.4 [-12.8, +2.4] |
| ASIA | H1 | demoted-to-WEAK | TRANSITIONAL | 19 (NOT READABLE) | -2.5 [-8.1, +4.5] |
| ASIA | H1 | native WEAK | TRANSITIONAL | 11 (NOT READABLE) | -1.5 [-10.2, +6.4] |
| ASIA | H2 | native STRONG | every regime | 47 (NOT READABLE) | +3.9 [-2.0, +9.0] |
| ASIA | H2 | demoted-to-MEDIUM | every regime | 9 (NOT READABLE) | -0.7 [-11.9, +10.5] |
| ASIA | H2 | native MEDIUM | every regime | 229 | -2.3 [-5.0, 0.0] |
| ASIA | H2 | demoted-to-WEAK | every regime | 56 (NOT READABLE) | +1.2 [-2.9, +5.3] |
| ASIA | H2 | native WEAK | every regime | 726 | -2.5 [-4.9, -0.3] |
| ASIA | H2 | native STRONG | TRANSITIONAL | 7 (NOT READABLE) | +7.6 [-0.6, +16.8] |
| ASIA | H2 | demoted-to-MEDIUM | TRANSITIONAL | 9 (NOT READABLE) | -0.7 [-11.7, +10.3] |
| ASIA | H2 | native MEDIUM | TRANSITIONAL | 22 (NOT READABLE) | +0.1 [-6.6, +7.4] |
| ASIA | H2 | demoted-to-WEAK | TRANSITIONAL | 56 (NOT READABLE) | +1.2 [-3.0, +5.2] |
| ASIA | H2 | native WEAK | TRANSITIONAL | 32 (NOT READABLE) | -1.2 [-7.7, +5.8] |

Comparisons (d = EV(first) - EV(second)):

| Session | Comparison | FULL d [95 % CI] (n first / n second) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | K1 demoted-to-WEAK vs native WEAK | +0.2 [-2.6, +3.0] (220 / 3303) | +1.1 [-3.6, +6.6] (89 / 1304) | -0.5 [-3.9, +2.6] (131 / 1999) | NO DIFFERENCE SHOWN |
| NY | K2 demoted-to-MEDIUM vs native MEDIUM | -1.3 [-5.1, +2.5] (66 / 1486) | +1.8 [-2.1, +5.1] (27 / 603) | -3.4 [-8.1, +2.7] (39 / 883) | NOT READABLE |
| NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | -1.2 [-5.4, +3.0] (220 / 107) | -2.3 [-10.3, +5.2] (89 / 33) | -1.0 [-5.7, +4.0] (131 / 74) | NO DIFFERENCE SHOWN |
| NY | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | -0.9 [-5.4, +3.3] (66 / 142) | +2.5 [-1.3, +6.9] (27 / 56) | -3.2 [-9.6, +3.2] (39 / 86) | NOT READABLE |
| LONDON | K1 demoted-to-WEAK vs native WEAK | -0.2 [-4.0, +3.9] (65 / 984) | -1.2 [-4.8, +2.1] (39 / 428) | +2.1 [-6.4, +10.5] (26 / 556) | NOT READABLE |
| LONDON | K2 demoted-to-MEDIUM vs native MEDIUM | -8.0 [-14.2, +1.2] (18 / 467) | -1.2 [-15.0, +10.7] (5 / 207) | -10.5 [-17.5, +1.3] (13 / 260) | NOT READABLE |
| LONDON | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | -0.4 [-6.4, +5.8] (65 / 31) | +4.1 [-2.4, +9.8] (39 / 14) | -2.7 [-11.9, +8.1] (26 / 17) | NOT READABLE |
| LONDON | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | -7.7 [-16.0, +2.5] (18 / 30) | +0.4 [-15.3, +13.2] (5 / 17) | -11.6 [-23.9, +0.8] (13 / 13) | NOT READABLE |
| ASIA | K1 demoted-to-WEAK vs native WEAK | +2.6 [-1.2, +6.4] (75 / 1168) | -0.4 [-6.8, +7.4] (19 / 442) | +3.7 [-0.8, +8.3] (56 / 726) | NOT READABLE |
| ASIA | K2 demoted-to-MEDIUM vs native MEDIUM | +4.9 [-4.7, +13.7] (11 / 409) | +15.3 [+11.6, +19.0] (2 / 180) | +1.6 [-9.3, +12.0] (9 / 229) | NOT READABLE |
| ASIA | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | +1.5 [-5.0, +7.6] (75 / 43) | -1.0 [-7.9, +9.3] (19 / 11) | +2.4 [-6.2, +10.2] (56 / 32) | NOT READABLE |
| ASIA | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | +3.7 [-6.8, +14.0] (11 / 43) | +15.1 [+5.7, +24.8] (2 / 21) | -0.8 [-14.8, +11.0] (9 / 22) | NOT READABLE |

### 4.2 carried 24 h

| Session | Sub-sample | Class | Scope | n | Net EV [95 % CI] |
|---|---|---|---|---|---|
| NY | FULL | native STRONG | every regime | 342 | -3.3 [-5.5, -1.4] |
| NY | FULL | demoted-to-MEDIUM | every regime | 66 (NOT READABLE) | -7.1 [-12.3, -1.6] |
| NY | FULL | native MEDIUM | every regime | 1486 | -4.1 [-5.3, -2.9] |
| NY | FULL | demoted-to-WEAK | every regime | 220 | -2.9 [-5.3, -0.3] |
| NY | FULL | native WEAK | every regime | 3303 | -3.1 [-4.2, -1.7] |
| NY | FULL | native STRONG | TRANSITIONAL | 14 (NOT READABLE) | -8.1 [-15.3, -2.5] |
| NY | FULL | demoted-to-MEDIUM | TRANSITIONAL | 66 (NOT READABLE) | -7.1 [-12.1, -1.6] |
| NY | FULL | native MEDIUM | TRANSITIONAL | 142 | -4.8 [-7.8, -1.8] |
| NY | FULL | demoted-to-WEAK | TRANSITIONAL | 220 | -2.9 [-5.3, -0.3] |
| NY | FULL | native WEAK | TRANSITIONAL | 107 | -1.4 [-5.0, +2.0] |
| NY | H1 | native STRONG | every regime | 137 | -3.4 [-5.4, -1.6] |
| NY | H1 | demoted-to-MEDIUM | every regime | 27 (NOT READABLE) | -2.8 [-7.4, +0.7] |
| NY | H1 | native MEDIUM | every regime | 603 | -4.4 [-6.0, -3.0] |
| NY | H1 | demoted-to-WEAK | every regime | 89 (NOT READABLE) | -1.6 [-5.6, +3.5] |
| NY | H1 | native WEAK | every regime | 1304 | -3.1 [-4.5, -1.8] |
| NY | H1 | native STRONG | TRANSITIONAL | 4 (NOT READABLE) | -7.7 [-13.7, +5.8] |
| NY | H1 | demoted-to-MEDIUM | TRANSITIONAL | 27 (NOT READABLE) | -2.8 [-7.6, +0.8] |
| NY | H1 | native MEDIUM | TRANSITIONAL | 56 (NOT READABLE) | -4.6 [-10.2, +0.5] |
| NY | H1 | demoted-to-WEAK | TRANSITIONAL | 89 (NOT READABLE) | -1.6 [-5.6, +3.7] |
| NY | H1 | native WEAK | TRANSITIONAL | 33 (NOT READABLE) | +1.2 [-5.1, +6.7] |
| NY | H2 | native STRONG | every regime | 205 | -3.2 [-6.9, -0.3] |
| NY | H2 | demoted-to-MEDIUM | every regime | 39 (NOT READABLE) | -10.1 [-16.3, -1.2] |
| NY | H2 | native MEDIUM | every regime | 883 | -3.8 [-5.7, -2.1] |
| NY | H2 | demoted-to-WEAK | every regime | 131 | -3.9 [-6.8, -1.1] |
| NY | H2 | native WEAK | every regime | 1999 | -3.0 [-4.6, -1.1] |
| NY | H2 | native STRONG | TRANSITIONAL | 10 (NOT READABLE) | -8.2 [-19.1, -1.4] |
| NY | H2 | demoted-to-MEDIUM | TRANSITIONAL | 39 (NOT READABLE) | -10.1 [-16.3, -1.5] |
| NY | H2 | native MEDIUM | TRANSITIONAL | 86 (NOT READABLE) | -5.0 [-8.6, -1.3] |
| NY | H2 | demoted-to-WEAK | TRANSITIONAL | 131 | -3.9 [-6.8, -1.2] |
| NY | H2 | native WEAK | TRANSITIONAL | 74 (NOT READABLE) | -2.6 [-7.1, +1.3] |
| LONDON | FULL | native STRONG | every regime | 131 | -1.8 [-6.2, +3.2] |
| LONDON | FULL | demoted-to-MEDIUM | every regime | 18 (NOT READABLE) | -8.5 [-14.3, +0.2] |
| LONDON | FULL | native MEDIUM | every regime | 467 | -0.3 [-2.5, +1.9] |
| LONDON | FULL | demoted-to-WEAK | every regime | 65 (NOT READABLE) | -1.5 [-5.7, +3.0] |
| LONDON | FULL | native WEAK | every regime | 984 | -0.9 [-3.8, +1.9] |
| LONDON | FULL | native STRONG | TRANSITIONAL | 8 (NOT READABLE) | -2.1 [-16.4, +9.0] |
| LONDON | FULL | demoted-to-MEDIUM | TRANSITIONAL | 18 (NOT READABLE) | -8.5 [-14.1, +0.4] |
| LONDON | FULL | native MEDIUM | TRANSITIONAL | 30 (NOT READABLE) | -0.5 [-5.4, +4.7] |
| LONDON | FULL | demoted-to-WEAK | TRANSITIONAL | 65 (NOT READABLE) | -1.5 [-5.7, +2.9] |
| LONDON | FULL | native WEAK | TRANSITIONAL | 31 (NOT READABLE) | -1.0 [-5.8, +3.9] |
| LONDON | H1 | native STRONG | every regime | 75 (NOT READABLE) | -4.6 [-9.4, -0.1] |
| LONDON | H1 | demoted-to-MEDIUM | every regime | 5 (NOT READABLE) | -1.3 [-15.4, +10.4] |
| LONDON | H1 | native MEDIUM | every regime | 207 | 0.0 [-3.2, +3.1] |
| LONDON | H1 | demoted-to-WEAK | every regime | 39 (NOT READABLE) | -3.7 [-7.5, +0.2] |
| LONDON | H1 | native WEAK | every regime | 428 | -2.0 [-4.6, +0.3] |
| LONDON | H1 | native STRONG | TRANSITIONAL | 3 (NOT READABLE) | -4.5 [-18.3, +16.9] |
| LONDON | H1 | demoted-to-MEDIUM | TRANSITIONAL | 5 (NOT READABLE) | -1.3 [-14.2, +10.5] |
| LONDON | H1 | native MEDIUM | TRANSITIONAL | 17 (NOT READABLE) | -1.0 [-6.7, +5.2] |
| LONDON | H1 | demoted-to-WEAK | TRANSITIONAL | 39 (NOT READABLE) | -3.7 [-7.5, 0.0] |
| LONDON | H1 | native WEAK | TRANSITIONAL | 14 (NOT READABLE) | -7.5 [-11.9, -2.1] |
| LONDON | H2 | native STRONG | every regime | 56 (NOT READABLE) | +2.0 [-6.0, +12.4] |
| LONDON | H2 | demoted-to-MEDIUM | every regime | 13 (NOT READABLE) | -11.3 [-17.4, -0.2] |
| LONDON | H2 | native MEDIUM | every regime | 260 | -0.5 [-3.6, +2.4] |
| LONDON | H2 | demoted-to-WEAK | every regime | 26 (NOT READABLE) | +1.7 [-7.3, +10.3] |
| LONDON | H2 | native WEAK | every regime | 556 | 0.0 [-4.8, +4.2] |
| LONDON | H2 | native STRONG | TRANSITIONAL | 5 (NOT READABLE) | -0.7 [-18.7, +17.3] |
| LONDON | H2 | demoted-to-MEDIUM | TRANSITIONAL | 13 (NOT READABLE) | -11.3 [-17.2, +0.1] |
| LONDON | H2 | native MEDIUM | TRANSITIONAL | 13 (NOT READABLE) | +0.3 [-8.6, +10.4] |
| LONDON | H2 | demoted-to-WEAK | TRANSITIONAL | 26 (NOT READABLE) | +1.7 [-7.1, +9.9] |
| LONDON | H2 | native WEAK | TRANSITIONAL | 17 (NOT READABLE) | +4.4 [-1.7, +9.8] |
| ASIA | FULL | native STRONG | every regime | 65 (NOT READABLE) | +3.3 [-2.6, +8.9] |
| ASIA | FULL | demoted-to-MEDIUM | every regime | 11 (NOT READABLE) | +2.1 [-8.0, +11.1] |
| ASIA | FULL | native MEDIUM | every regime | 409 | -4.0 [-6.1, -2.2] |
| ASIA | FULL | demoted-to-WEAK | every regime | 75 (NOT READABLE) | +0.5 [-3.4, +4.1] |
| ASIA | FULL | native WEAK | every regime | 1168 | -2.5 [-4.5, -0.6] |
| ASIA | FULL | native STRONG | TRANSITIONAL | 9 (NOT READABLE) | +2.5 [-9.0, +13.1] |
| ASIA | FULL | demoted-to-MEDIUM | TRANSITIONAL | 11 (NOT READABLE) | +2.1 [-7.9, +11.2] |
| ASIA | FULL | native MEDIUM | TRANSITIONAL | 43 (NOT READABLE) | -2.9 [-8.2, +2.6] |
| ASIA | FULL | demoted-to-WEAK | TRANSITIONAL | 75 (NOT READABLE) | +0.5 [-3.4, +4.2] |
| ASIA | FULL | native WEAK | TRANSITIONAL | 43 (NOT READABLE) | -0.9 [-6.1, +4.4] |
| ASIA | H1 | native STRONG | every regime | 18 (NOT READABLE) | -1.6 [-7.9, +3.6] |
| ASIA | H1 | demoted-to-MEDIUM | every regime | 2 (NOT READABLE) | +9.7 [+7.1, +12.3] |
| ASIA | H1 | native MEDIUM | every regime | 180 | -6.2 [-8.3, -4.2] |
| ASIA | H1 | demoted-to-WEAK | every regime | 19 (NOT READABLE) | -1.1 [-7.9, +7.7] |
| ASIA | H1 | native WEAK | every regime | 442 | -2.2 [-4.9, +0.6] |
| ASIA | H1 | native STRONG | TRANSITIONAL | 2 (NOT READABLE) | -15.2 [-16.3, -14.0] |
| ASIA | H1 | demoted-to-MEDIUM | TRANSITIONAL | 2 (NOT READABLE) | +9.7 [+7.1, +12.3] |
| ASIA | H1 | native MEDIUM | TRANSITIONAL | 21 (NOT READABLE) | -6.0 [-13.7, +1.7] |
| ASIA | H1 | demoted-to-WEAK | TRANSITIONAL | 19 (NOT READABLE) | -1.1 [-7.6, +7.4] |
| ASIA | H1 | native WEAK | TRANSITIONAL | 11 (NOT READABLE) | -1.5 [-10.1, +6.2] |
| ASIA | H2 | native STRONG | every regime | 47 (NOT READABLE) | +5.2 [-2.6, +12.1] |
| ASIA | H2 | demoted-to-MEDIUM | every regime | 9 (NOT READABLE) | +0.4 [-11.1, +11.7] |
| ASIA | H2 | native MEDIUM | every regime | 229 | -2.3 [-5.2, +0.2] |
| ASIA | H2 | demoted-to-WEAK | every regime | 56 (NOT READABLE) | +1.1 [-3.5, +5.3] |
| ASIA | H2 | native WEAK | every regime | 726 | -2.7 [-5.4, -0.1] |
| ASIA | H2 | native STRONG | TRANSITIONAL | 7 (NOT READABLE) | +7.6 [0.0, +17.6] |
| ASIA | H2 | demoted-to-MEDIUM | TRANSITIONAL | 9 (NOT READABLE) | +0.4 [-11.2, +11.4] |
| ASIA | H2 | native MEDIUM | TRANSITIONAL | 22 (NOT READABLE) | +0.1 [-6.4, +7.3] |
| ASIA | H2 | demoted-to-WEAK | TRANSITIONAL | 56 (NOT READABLE) | +1.1 [-3.5, +5.1] |
| ASIA | H2 | native WEAK | TRANSITIONAL | 32 (NOT READABLE) | -0.6 [-6.9, +5.8] |

Comparisons (d = EV(first) - EV(second)):

| Session | Comparison | FULL d [95 % CI] (n first / n second) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |
|---|---|---|---|---|---|
| NY | K1 demoted-to-WEAK vs native WEAK | +0.1 [-2.8, +3.0] (220 / 3303) | +1.6 [-2.9, +6.8] (89 / 1304) | -0.9 [-4.9, +2.5] (131 / 1999) | NO DIFFERENCE SHOWN |
| NY | K2 demoted-to-MEDIUM vs native MEDIUM | -3.0 [-8.0, +2.1] (66 / 1486) | +1.7 [-2.8, +5.3] (27 / 603) | -6.3 [-12.0, +2.0] (39 / 883) | NOT READABLE |
| NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | -1.5 [-5.7, +2.5] (220 / 107) | -2.7 [-10.2, +4.2] (89 / 33) | -1.3 [-6.2, +4.2] (131 / 74) | NO DIFFERENCE SHOWN |
| NY | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | -2.3 [-7.2, +2.8] (66 / 142) | +1.8 [-2.0, +6.1] (27 / 56) | -5.1 [-11.3, +2.6] (39 / 86) | NOT READABLE |
| LONDON | K1 demoted-to-WEAK vs native WEAK | -0.6 [-5.3, +4.1] (65 / 984) | -1.7 [-5.6, +1.9] (39 / 428) | +1.7 [-7.6, +11.1] (26 / 556) | NOT READABLE |
| LONDON | K2 demoted-to-MEDIUM vs native MEDIUM | -8.3 [-14.5, +1.0] (18 / 467) | -1.3 [-15.3, +10.2] (5 / 207) | -10.8 [-17.2, +1.4] (13 / 260) | NOT READABLE |
| LONDON | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | -0.5 [-6.5, +5.8] (65 / 31) | +3.9 [-3.1, +9.7] (39 / 14) | -2.7 [-12.1, +7.9] (26 / 17) | NOT READABLE |
| LONDON | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | -8.1 [-16.4, +2.0] (18 / 30) | -0.2 [-15.5, +12.8] (5 / 17) | -11.6 [-24.0, +1.7] (13 / 13) | NOT READABLE |
| ASIA | K1 demoted-to-WEAK vs native WEAK | +3.0 [-1.2, +7.0] (75 / 1168) | +1.0 [-6.7, +11.0] (19 / 442) | +3.8 [-1.2, +8.4] (56 / 726) | NOT READABLE |
| ASIA | K2 demoted-to-MEDIUM vs native MEDIUM | +6.1 [-3.6, +14.9] (11 / 409) | +15.9 [+12.0, +19.9] (2 / 180) | +2.7 [-8.4, +13.1] (9 / 229) | NOT READABLE |
| ASIA | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | +1.4 [-4.9, +7.5] (75 / 43) | +0.4 [-7.4, +12.2] (19 / 11) | +1.7 [-6.3, +9.1] (56 / 32) | NOT READABLE |
| ASIA | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | +5.0 [-5.7, +15.2] (11 / 43) | +15.7 [+6.4, +25.7] (2 / 21) | +0.3 [-13.6, +11.8] (9 / 22) | NOT READABLE |

## 5. Labels

- main window | NY | K1 demoted-to-WEAK vs native WEAK | NO DIFFERENCE SHOWN
- main window | NY | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- main window | NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NO DIFFERENCE SHOWN
- main window | NY | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE
- main window | LONDON | K1 demoted-to-WEAK vs native WEAK | NOT READABLE
- main window | LONDON | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- main window | LONDON | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NOT READABLE
- main window | LONDON | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE
- main window | ASIA | K1 demoted-to-WEAK vs native WEAK | NOT READABLE
- main window | ASIA | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- main window | ASIA | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NOT READABLE
- main window | ASIA | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE
- carried 24 h | NY | K1 demoted-to-WEAK vs native WEAK | NO DIFFERENCE SHOWN
- carried 24 h | NY | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- carried 24 h | NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NO DIFFERENCE SHOWN
- carried 24 h | NY | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE
- carried 24 h | LONDON | K1 demoted-to-WEAK vs native WEAK | NOT READABLE
- carried 24 h | LONDON | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- carried 24 h | LONDON | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NOT READABLE
- carried 24 h | LONDON | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE
- carried 24 h | ASIA | K1 demoted-to-WEAK vs native WEAK | NOT READABLE
- carried 24 h | ASIA | K2 demoted-to-MEDIUM vs native MEDIUM | NOT READABLE
- carried 24 h | ASIA | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NOT READABLE
- carried 24 h | ASIA | K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only | NOT READABLE

