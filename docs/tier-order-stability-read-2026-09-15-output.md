# SwingFallbackRead output: --mode stability (tier-order stability check, Part A)

- Run at (UTC): 2026-09-15 17:54:39
- Pooled log: C:\Dev\DeribitVerdictEngine\AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv
- Box logs: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv.v0.7.bak + C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv
- Eval cache: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_eval_cache.csv
- settings.json version 68, sha256 A059DEC578D8B4C7 (copy identical)
- Session ASIA: hours 0-7 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 1.25xATR
- Session LONDON: hours 8-12 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 2xATR
- Session NY: hours 13-23 UTC inclusive, execution_resolution 1, windows 5/10/15 min, fallback target 1.75xATR
- Trading week: Monday 00:00 UTC to Friday 24:00 UTC (exclusive)
- Fees: style maker_maker, round trip 3.00 bps (maker 1.5, taker 3.5). Taker-stop case: 3.00 bps on a target hit, 5.00 bps on a stop hit or a marked exit

- Brief: docs/tier-order-stability-check-brief-2026-09-15.md, Part A. Interpretations: docs/tier-order-stability-read-2026-09-15.md section M.
- Reproduction reference: C:\Dev\DeribitVerdictEngine\docs\swing-vs-fallback-target-read-2026-09-15-output.md
- Population: 8810 signals. Fees: maker/maker 3.00 bps round trip. No slippage case.
- Bootstrap: 10000 resamples of whole UTC trading days per session x target type x sub-sample x outcome mode; seed 20260915 + group index; percentile 95 % CI. Readable cell: n >= 100.

## 1. Reproduction check against the swing read's committed output

Reference: section 6 (main window) and section 8 (carried 24 h) of the swing read output, printed to 0.1 bps. Pass: n equal and |this run - reference| <= 0.1 bps on every row.

| Mode | Session | Target set by | Tier | n (reference) | n (this run) | Net EV bps (reference) | Net EV bps (this run) | Delta bps |
|---|---|---|---|---|---|---|---|---|
| main window | NY | none | ALL | 2852 | 2852 | -3.8 | -3.79 | +0.01 |
| main window | NY | none | S+M | 1198 | 1198 | -3.8 | -3.77 | +0.03 |
| main window | NY | none | STRONG | 259 | 259 | -1.8 | -1.76 | +0.04 |
| main window | NY | none | MEDIUM | 939 | 939 | -4.3 | -4.32 | -0.02 |
| main window | NY | none | WEAK | 1654 | 1654 | -3.8 | -3.80 | 0.00 |
| main window | NY | swing | ALL | 1758 | 1758 | -3.4 | -3.42 | -0.02 |
| main window | NY | swing | S+M | 547 | 547 | -4.9 | -4.85 | +0.05 |
| main window | NY | swing | STRONG | 59 | 59 | -9.2 | -9.22 | -0.02 |
| main window | NY | swing | MEDIUM | 488 | 488 | -4.3 | -4.32 | -0.02 |
| main window | NY | swing | WEAK | 1211 | 1211 | -2.8 | -2.77 | +0.03 |
| main window | NY | hvn | ALL | 807 | 807 | -2.6 | -2.61 | -0.01 |
| main window | NY | hvn | S+M | 149 | 149 | -3.0 | -3.02 | -0.02 |
| main window | NY | hvn | STRONG | 24 | 24 | -4.6 | -4.57 | +0.03 |
| main window | NY | hvn | MEDIUM | 125 | 125 | -2.7 | -2.72 | -0.02 |
| main window | NY | hvn | WEAK | 658 | 658 | -2.5 | -2.52 | -0.02 |
| main window | LONDON | none | ALL | 891 | 891 | -0.1 | -0.07 | +0.03 |
| main window | LONDON | none | S+M | 427 | 427 | +0.1 | +0.14 | +0.04 |
| main window | LONDON | none | STRONG | 106 | 106 | -1.3 | -1.28 | +0.02 |
| main window | LONDON | none | MEDIUM | 321 | 321 | +0.6 | +0.61 | +0.01 |
| main window | LONDON | none | WEAK | 464 | 464 | -0.3 | -0.26 | +0.04 |
| main window | LONDON | swing | ALL | 642 | 642 | -2.4 | -2.37 | +0.03 |
| main window | LONDON | swing | S+M | 158 | 158 | -3.0 | -3.04 | -0.04 |
| main window | LONDON | swing | STRONG | 22 | 22 | -4.1 | -4.07 | +0.03 |
| main window | LONDON | swing | MEDIUM | 136 | 136 | -2.9 | -2.87 | +0.03 |
| main window | LONDON | swing | WEAK | 484 | 484 | -2.1 | -2.15 | -0.05 |
| main window | LONDON | hvn | ALL | 132 | 132 | -2.2 | -2.21 | -0.01 |
| main window | LONDON | hvn | S+M | 31 | 31 | -5.6 | -5.59 | +0.01 |
| main window | LONDON | hvn | STRONG | 3 | 3 | +6.0 | +6.00 | 0.00 |
| main window | LONDON | hvn | MEDIUM | 28 | 28 | -6.8 | -6.84 | -0.04 |
| main window | LONDON | hvn | WEAK | 101 | 101 | -1.2 | -1.17 | +0.03 |
| main window | ASIA | none | ALL | 734 | 734 | -2.4 | -2.43 | -0.03 |
| main window | ASIA | none | S+M | 302 | 302 | -2.5 | -2.50 | 0.00 |
| main window | ASIA | none | STRONG | 51 | 51 | +2.6 | +2.61 | +0.01 |
| main window | ASIA | none | MEDIUM | 251 | 251 | -3.5 | -3.54 | -0.04 |
| main window | ASIA | none | WEAK | 432 | 432 | -2.4 | -2.37 | +0.03 |
| main window | ASIA | swing | ALL | 846 | 846 | -1.8 | -1.78 | +0.02 |
| main window | ASIA | swing | S+M | 153 | 153 | -3.0 | -2.96 | +0.04 |
| main window | ASIA | swing | STRONG | 12 | 12 | +6.5 | +6.52 | +0.02 |
| main window | ASIA | swing | MEDIUM | 141 | 141 | -3.8 | -3.77 | +0.03 |
| main window | ASIA | swing | WEAK | 693 | 693 | -1.5 | -1.52 | -0.02 |
| main window | ASIA | hvn | ALL | 148 | 148 | -5.5 | -5.45 | +0.05 |
| main window | ASIA | hvn | S+M | 30 | 30 | -5.3 | -5.28 | +0.02 |
| main window | ASIA | hvn | STRONG | 2 | 2 | -23.0 | -23.03 | -0.03 |
| main window | ASIA | hvn | MEDIUM | 28 | 28 | -4.0 | -4.01 | -0.01 |
| main window | ASIA | hvn | WEAK | 118 | 118 | -5.5 | -5.50 | 0.00 |
| carried 24 h | NY | none | ALL | 2852 | 2852 | -3.9 | -3.92 | -0.02 |
| carried 24 h | NY | none | S+M | 1198 | 1198 | -3.9 | -3.89 | +0.01 |
| carried 24 h | NY | none | STRONG | 259 | 259 | -1.8 | -1.80 | 0.00 |
| carried 24 h | NY | none | MEDIUM | 939 | 939 | -4.5 | -4.46 | +0.04 |
| carried 24 h | NY | none | WEAK | 1654 | 1654 | -3.9 | -3.95 | -0.05 |
| carried 24 h | NY | swing | ALL | 1758 | 1758 | -3.3 | -3.32 | -0.02 |
| carried 24 h | NY | swing | S+M | 547 | 547 | -5.0 | -5.03 | -0.03 |
| carried 24 h | NY | swing | STRONG | 59 | 59 | -10.0 | -10.00 | 0.00 |
| carried 24 h | NY | swing | MEDIUM | 488 | 488 | -4.4 | -4.42 | -0.02 |
| carried 24 h | NY | swing | WEAK | 1211 | 1211 | -2.5 | -2.54 | -0.04 |
| carried 24 h | NY | hvn | ALL | 807 | 807 | -1.7 | -1.68 | +0.02 |
| carried 24 h | NY | hvn | S+M | 149 | 149 | -1.5 | -1.45 | +0.05 |
| carried 24 h | NY | hvn | STRONG | 24 | 24 | -3.1 | -3.07 | +0.03 |
| carried 24 h | NY | hvn | MEDIUM | 125 | 125 | -1.1 | -1.14 | -0.04 |
| carried 24 h | NY | hvn | WEAK | 658 | 658 | -1.7 | -1.74 | -0.04 |
| carried 24 h | LONDON | none | ALL | 891 | 891 | +0.4 | +0.42 | +0.02 |
| carried 24 h | LONDON | none | S+M | 427 | 427 | +0.8 | +0.76 | -0.04 |
| carried 24 h | LONDON | none | STRONG | 106 | 106 | -0.6 | -0.64 | -0.04 |
| carried 24 h | LONDON | none | MEDIUM | 321 | 321 | +1.2 | +1.22 | +0.02 |
| carried 24 h | LONDON | none | WEAK | 464 | 464 | +0.1 | +0.10 | 0.00 |
| carried 24 h | LONDON | swing | ALL | 642 | 642 | -2.4 | -2.39 | +0.01 |
| carried 24 h | LONDON | swing | S+M | 158 | 158 | -3.8 | -3.82 | -0.02 |
| carried 24 h | LONDON | swing | STRONG | 22 | 22 | -8.3 | -8.33 | -0.03 |
| carried 24 h | LONDON | swing | MEDIUM | 136 | 136 | -3.1 | -3.09 | +0.01 |
| carried 24 h | LONDON | swing | WEAK | 484 | 484 | -1.9 | -1.92 | -0.02 |
| carried 24 h | LONDON | hvn | ALL | 132 | 132 | -2.4 | -2.36 | +0.04 |
| carried 24 h | LONDON | hvn | S+M | 31 | 31 | -7.4 | -7.40 | 0.00 |
| carried 24 h | LONDON | hvn | STRONG | 3 | 3 | +6.0 | +6.00 | 0.00 |
| carried 24 h | LONDON | hvn | MEDIUM | 28 | 28 | -8.8 | -8.84 | -0.04 |
| carried 24 h | LONDON | hvn | WEAK | 101 | 101 | -0.8 | -0.81 | -0.01 |
| carried 24 h | ASIA | none | ALL | 734 | 734 | -2.4 | -2.41 | -0.01 |
| carried 24 h | ASIA | none | S+M | 302 | 302 | -2.6 | -2.61 | -0.01 |
| carried 24 h | ASIA | none | STRONG | 51 | 51 | +3.0 | +2.99 | -0.01 |
| carried 24 h | ASIA | none | MEDIUM | 251 | 251 | -3.8 | -3.75 | +0.05 |
| carried 24 h | ASIA | none | WEAK | 432 | 432 | -2.3 | -2.26 | +0.04 |
| carried 24 h | ASIA | swing | ALL | 846 | 846 | -2.1 | -2.06 | +0.04 |
| carried 24 h | ASIA | swing | S+M | 153 | 153 | -3.0 | -2.97 | +0.03 |
| carried 24 h | ASIA | swing | STRONG | 12 | 12 | +8.9 | +8.86 | -0.04 |
| carried 24 h | ASIA | swing | MEDIUM | 141 | 141 | -4.0 | -3.98 | +0.02 |
| carried 24 h | ASIA | swing | WEAK | 693 | 693 | -1.9 | -1.86 | +0.04 |
| carried 24 h | ASIA | hvn | ALL | 148 | 148 | -5.4 | -5.40 | 0.00 |
| carried 24 h | ASIA | hvn | S+M | 30 | 30 | -5.6 | -5.57 | +0.03 |
| carried 24 h | ASIA | hvn | STRONG | 2 | 2 | -23.0 | -23.03 | -0.03 |
| carried 24 h | ASIA | hvn | MEDIUM | 28 | 28 | -4.3 | -4.33 | -0.03 |
| carried 24 h | ASIA | hvn | WEAK | 118 | 118 | -5.4 | -5.36 | +0.04 |

- Rows compared: 90 (reference rows: 90). Failing rows: 0. Largest |delta|: 0.049 bps.
- **REPRODUCTION PASSED.**

## 2. Splits

- Trading days (distinct UTC dates in the population): 49, from 2026-07-07 to 2026-09-11.
- **Split date: 2026-08-10 00:00 UTC.** H1 = 24 days (2026-07-07 to 2026-08-07); H2 = 25 days (2026-08-10 to 2026-09-11).
- Regime edge: 2026-08-20 00:00 UTC. R1 = 32 trading days; R2 = 17 trading days.

| Session | Target set by | Sub-sample | STRONG n | MEDIUM n | WEAK n | Days in pool |
|---|---|---|---|---|---|---|
| NY | none | FULL | 259 | 939 | 1654 | 49 |
| NY | none | H1 | 101 | 366 | 657 | 24 |
| NY | none | H2 | 158 | 573 | 997 | 25 |
| NY | none | R1 | 159 | 510 | 866 | 32 |
| NY | none | R2 | 100 | 429 | 788 | 17 |
| NY | swing | FULL | 59 | 488 | 1211 | 49 |
| NY | swing | H1 | 25 | 231 | 527 | 24 |
| NY | swing | H2 | 34 | 257 | 684 | 25 |
| NY | swing | R1 | 40 | 292 | 633 | 32 |
| NY | swing | R2 | 19 | 196 | 578 | 17 |
| LONDON | none | FULL | 106 | 321 | 464 | 45 |
| LONDON | none | H1 | 64 | 154 | 218 | 21 |
| LONDON | none | H2 | 42 | 167 | 246 | 24 |
| LONDON | none | R1 | 73 | 181 | 252 | 28 |
| LONDON | none | R2 | 33 | 140 | 212 | 17 |
| LONDON | swing | FULL | 22 | 136 | 484 | 45 |
| LONDON | swing | H1 | 9 | 49 | 209 | 22 |
| LONDON | swing | H2 | 13 | 87 | 275 | 23 |
| LONDON | swing | R1 | 10 | 57 | 239 | 28 |
| LONDON | swing | R2 | 12 | 79 | 245 | 17 |
| ASIA | none | FULL | 51 | 251 | 432 | 37 |
| ASIA | none | H1 | 16 | 117 | 162 | 18 |
| ASIA | none | H2 | 35 | 134 | 270 | 19 |
| ASIA | none | R1 | 17 | 120 | 171 | 20 |
| ASIA | none | R2 | 34 | 131 | 261 | 17 |
| ASIA | swing | FULL | 12 | 141 | 693 | 42 |
| ASIA | swing | H1 | 2 | 55 | 255 | 19 |
| ASIA | swing | H2 | 10 | 86 | 438 | 23 |
| ASIA | swing | R1 | 4 | 65 | 323 | 25 |
| ASIA | swing | R2 | 8 | 76 | 370 | 17 |

- Bootstrap groups: 66. Resample-statistics skipped because a tier drew zero signals (all groups, all tiers and comparisons): 18339.
- Of those, skips inside READABLE comparisons: 0.

## 3. Verdict: C1-C3 x session x target type

Label from the main window (primary). Carried 24 h label is secondary. Signs: H1; H2; R1; R2 (n/r = not readable). Difference = EV(first tier) - EV(second tier), net EV per trade in bps.

| Session | Target set by | Comparison | Label, main window | Signs, main window | Full-sample difference [CI], main window | Full CI excludes 0 | Label, carried 24 h | Signs, carried 24 h | Full-sample difference [CI], carried 24 h |
|---|---|---|---|---|---|---|---|---|---|
| NY | none | C1 WEAK vs MEDIUM | **UNSTABLE** | H1 +; H2 +; R1 -; R2 + | +0.5 [-0.9, +2.0] | no | STABLE | H1 +; H2 +; R1 +; R2 + | +0.5 [-0.9, +2.1] |
| NY | none | C2 STRONG vs MEDIUM | **STABLE** | H1 +; H2 +; R1 +; R2 + | +2.6 [0.0, +4.9] | no | STABLE | H1 +; H2 +; R1 +; R2 + | +2.7 [0.0, +5.1] |
| NY | none | C3 WEAK vs STRONG | **UNSTABLE** | H1 -; H2 -; R1 -; R2 + | -2.0 [-4.6, +0.8] | no | STABLE | H1 -; H2 -; R1 -; R2 - | -2.1 [-4.7, +0.6] |
| NY | swing | C1 WEAK vs MEDIUM | **STABLE** | H1 +; H2 +; R1 +; R2 + | +1.6 [-0.7, +3.6] | no | STABLE | H1 +; H2 +; R1 +; R2 + | +1.9 [-0.6, +4.1] |
| NY | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -4.9 [-10.2, +0.6] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -5.6 [-10.9, -0.2] |
| NY | swing | C3 WEAK vs STRONG | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +6.5 [+1.1, +12.1] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +7.5 [+1.9, +13.4] |
| LONDON | none | C1 WEAK vs MEDIUM | **UNSTABLE** | H1 -; H2 +; R1 -; R2 + | -0.9 [-4.0, +2.5] | no | UNSTABLE | H1 -; H2 +; R1 -; R2 + | -1.1 [-5.2, +3.2] |
| LONDON | none | C2 STRONG vs MEDIUM | **INCONCLUSIVE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -1.9 [-6.9, +3.7] | no | INCONCLUSIVE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -1.9 [-6.6, +3.6] |
| LONDON | none | C3 WEAK vs STRONG | **INCONCLUSIVE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +1.0 [-4.5, +5.8] | no | INCONCLUSIVE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +0.7 [-6.0, +6.3] |
| LONDON | swing | C1 WEAK vs MEDIUM | **INCONCLUSIVE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +0.7 [-2.3, +3.6] | no | INCONCLUSIVE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +1.2 [-2.8, +4.6] |
| LONDON | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -1.2 [-7.8, +4.2] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -5.2 [-13.1, +1.1] |
| LONDON | swing | C3 WEAK vs STRONG | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +1.9 [-3.2, +8.3] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +6.4 [-0.8, +15.3] |
| ASIA | none | C1 WEAK vs MEDIUM | **STABLE** | H1 +; H2 +; R1 +; R2 + | +1.2 [-1.6, +3.8] | no | STABLE | H1 +; H2 +; R1 +; R2 + | +1.5 [-1.4, +4.2] |
| ASIA | none | C2 STRONG vs MEDIUM | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +6.2 [+1.7, +10.0] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +6.7 [+0.8, +11.6] |
| ASIA | none | C3 WEAK vs STRONG | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -5.0 [-10.2, +0.4] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -5.3 [-11.3, +1.4] |
| ASIA | swing | C1 WEAK vs MEDIUM | **INCONCLUSIVE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +2.3 [-0.6, +5.4] | no | INCONCLUSIVE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +2.1 [-1.2, +5.8] |
| ASIA | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +10.3 [+4.7, +17.7] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | +12.8 [+7.3, +20.8] |
| ASIA | swing | C3 WEAK vs STRONG | **NOT READABLE** | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -8.0 [-15.4, -2.1] | n/r | NOT READABLE | H1 n/r; H2 n/r; R1 n/r; R2 n/r | -10.7 [-19.0, -4.2] |

## 4. Per-sub-sample tier cells, main window

| Session | Target set by | Sub-sample | Tier | n | Days | Readable | Success rate % | Gross BE % | Gross edge pp | Net EV bps [CI] |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | none | FULL | STRONG | 259 | 49 | yes | 51.4 | 47.8 | +3.6 | -1.8 [-4.9, +1.2] |
| NY | none | FULL | MEDIUM | 939 | 49 | yes | 41.7 | 47.8 | -6.0 | -4.3 [-6.0, -2.6] |
| NY | none | FULL | WEAK | 1654 | 49 | yes | 41.2 | 47.7 | -6.5 | -3.8 [-4.8, -2.8] |
| NY | none | H1 | STRONG | 101 | 24 | yes | 47.5 | 47.8 | -0.2 | -2.7 [-5.1, 0.0] |
| NY | none | H1 | MEDIUM | 366 | 24 | yes | 42.3 | 47.8 | -5.4 | -4.0 [-5.8, -2.0] |
| NY | none | H1 | WEAK | 657 | 24 | yes | 41.2 | 47.7 | -6.5 | -4.0 [-5.1, -3.0] |
| NY | none | H2 | STRONG | 158 | 25 | yes | 53.8 | 47.8 | +6.0 | -1.2 [-6.4, +3.0] |
| NY | none | H2 | MEDIUM | 573 | 25 | yes | 41.4 | 47.7 | -6.4 | -4.5 [-7.0, -2.1] |
| NY | none | H2 | WEAK | 997 | 25 | yes | 41.1 | 47.7 | -6.6 | -3.7 [-5.3, -2.1] |
| NY | none | R1 | STRONG | 159 | 32 | yes | 56.0 | 47.8 | +8.2 | -0.1 [-3.6, +3.1] |
| NY | none | R1 | MEDIUM | 510 | 32 | yes | 43.7 | 47.8 | -4.0 | -3.4 [-5.3, -1.4] |
| NY | none | R1 | WEAK | 866 | 32 | yes | 41.1 | 47.7 | -6.6 | -3.5 [-4.8, -2.2] |
| NY | none | R2 | STRONG | 100 | 17 | yes | 44.0 | 47.8 | -3.8 | -4.4 [-9.7, +0.7] |
| NY | none | R2 | MEDIUM | 429 | 17 | yes | 39.4 | 47.7 | -8.4 | -5.4 [-8.3, -2.8] |
| NY | none | R2 | WEAK | 788 | 17 | yes | 41.2 | 47.7 | -6.5 | -4.2 [-5.7, -2.7] |
| NY | swing | FULL | STRONG | 59 | 49 | no | 22.0 | 43.0 | -20.9 | -9.2 [-14.4, -4.0] |
| NY | swing | FULL | MEDIUM | 488 | 49 | yes | 33.6 | 43.2 | -9.6 | -4.3 [-6.2, -2.3] |
| NY | swing | FULL | WEAK | 1211 | 49 | yes | 36.2 | 41.1 | -5.0 | -2.8 [-4.4, -1.1] |
| NY | swing | H1 | STRONG | 25 | 24 | no | 24.0 | 41.6 | -17.6 | -6.9 [-11.5, -1.7] |
| NY | swing | H1 | MEDIUM | 231 | 24 | yes | 29.0 | 42.3 | -13.3 | -4.6 [-7.1, -1.9] |
| NY | swing | H1 | WEAK | 527 | 24 | yes | 34.0 | 40.5 | -6.6 | -2.5 [-5.3, +0.3] |
| NY | swing | H2 | STRONG | 34 | 25 | no | 20.6 | 43.7 | -23.1 | -10.9 [-18.2, -2.8] |
| NY | swing | H2 | MEDIUM | 257 | 25 | yes | 37.7 | 43.8 | -6.0 | -4.1 [-6.7, -1.1] |
| NY | swing | H2 | WEAK | 684 | 25 | yes | 37.9 | 41.5 | -3.6 | -3.0 [-4.9, -1.1] |
| NY | swing | R1 | STRONG | 40 | 32 | no | 20.0 | 43.0 | -23.0 | -9.9 [-16.6, -3.0] |
| NY | swing | R1 | MEDIUM | 292 | 32 | yes | 30.8 | 43.0 | -12.2 | -4.4 [-6.6, -2.0] |
| NY | swing | R1 | WEAK | 633 | 32 | yes | 34.9 | 40.5 | -5.6 | -2.1 [-4.5, +0.3] |
| NY | swing | R2 | STRONG | 19 | 17 | no | 26.3 | 42.9 | -16.6 | -7.8 [-14.2, -0.7] |
| NY | swing | R2 | MEDIUM | 196 | 17 | yes | 37.8 | 43.4 | -5.6 | -4.2 [-7.5, -0.7] |
| NY | swing | R2 | WEAK | 578 | 17 | yes | 37.5 | 41.7 | -4.1 | -3.5 [-5.7, -1.5] |
| LONDON | none | FULL | STRONG | 106 | 45 | yes | 41.5 | 44.4 | -2.9 | -1.3 [-5.9, +4.4] |
| LONDON | none | FULL | MEDIUM | 321 | 45 | yes | 49.2 | 44.4 | +4.8 | +0.6 [-2.3, +3.4] |
| LONDON | none | FULL | WEAK | 464 | 45 | yes | 43.1 | 44.3 | -1.2 | -0.3 [-3.1, +2.4] |
| LONDON | none | H1 | STRONG | 64 | 21 | no | 43.8 | 44.4 | -0.7 | -3.2 [-7.5, +1.1] |
| LONDON | none | H1 | MEDIUM | 154 | 21 | yes | 55.8 | 44.4 | +11.5 | +1.2 [-2.8, +5.2] |
| LONDON | none | H1 | WEAK | 218 | 21 | yes | 45.4 | 44.4 | +1.0 | -1.6 [-4.8, +1.3] |
| LONDON | none | H2 | STRONG | 42 | 24 | no | 38.1 | 44.4 | -6.3 | +1.6 [-7.6, +16.0] |
| LONDON | none | H2 | MEDIUM | 167 | 24 | yes | 43.1 | 44.4 | -1.3 | +0.1 [-4.1, +3.9] |
| LONDON | none | H2 | WEAK | 246 | 24 | yes | 41.1 | 44.3 | -3.2 | +0.9 [-3.5, +5.0] |
| LONDON | none | R1 | STRONG | 73 | 28 | no | 39.7 | 44.4 | -4.7 | -4.0 [-7.9, +0.1] |
| LONDON | none | R1 | MEDIUM | 181 | 28 | yes | 53.6 | 44.4 | +9.2 | +0.4 [-3.0, +3.9] |
| LONDON | none | R1 | WEAK | 252 | 28 | yes | 42.1 | 44.4 | -2.3 | -2.2 [-5.1, +0.4] |
| LONDON | none | R2 | STRONG | 33 | 17 | no | 45.5 | 44.4 | +1.0 | +4.8 [-6.8, +21.2] |
| LONDON | none | R2 | MEDIUM | 140 | 17 | yes | 43.6 | 44.4 | -0.9 | +0.8 [-4.0, +5.0] |
| LONDON | none | R2 | WEAK | 212 | 17 | yes | 44.3 | 44.3 | +0.1 | +2.1 [-2.8, +6.6] |
| LONDON | swing | FULL | STRONG | 22 | 45 | no | 36.4 | 46.5 | -10.2 | -4.1 [-10.3, +0.1] |
| LONDON | swing | FULL | MEDIUM | 136 | 45 | yes | 41.9 | 46.8 | -4.9 | -2.9 [-5.7, -0.4] |
| LONDON | swing | FULL | WEAK | 484 | 45 | yes | 42.4 | 42.7 | -0.4 | -2.1 [-4.8, +0.1] |
| LONDON | swing | H1 | STRONG | 9 | 22 | no | 22.2 | 44.6 | -22.4 | -7.5 [-17.2, +0.2] |
| LONDON | swing | H1 | MEDIUM | 49 | 22 | no | 32.7 | 42.9 | -10.2 | -4.0 [-7.8, -0.9] |
| LONDON | swing | H1 | WEAK | 209 | 22 | yes | 41.6 | 41.8 | -0.2 | -2.7 [-5.8, -0.1] |
| LONDON | swing | H2 | STRONG | 13 | 23 | no | 46.2 | 47.5 | -1.4 | -1.7 [-10.4, +3.2] |
| LONDON | swing | H2 | MEDIUM | 87 | 23 | no | 47.1 | 48.6 | -1.5 | -2.2 [-6.3, +1.3] |
| LONDON | swing | H2 | WEAK | 275 | 23 | yes | 42.9 | 43.1 | -0.2 | -1.7 [-6.0, +1.6] |
| LONDON | swing | R1 | STRONG | 10 | 28 | no | 20.0 | 43.8 | -23.8 | -8.0 [-16.4, -1.3] |
| LONDON | swing | R1 | MEDIUM | 57 | 28 | no | 31.6 | 42.3 | -10.7 | -4.1 [-7.6, -0.9] |
| LONDON | swing | R1 | WEAK | 239 | 28 | yes | 41.0 | 41.5 | -0.5 | -2.4 [-5.1, 0.0] |
| LONDON | swing | R2 | STRONG | 12 | 17 | no | 50.0 | 48.1 | +1.9 | -0.8 [-10.0, +4.1] |
| LONDON | swing | R2 | MEDIUM | 79 | 17 | no | 49.4 | 49.3 | +0.1 | -2.0 [-6.3, +1.8] |
| LONDON | swing | R2 | WEAK | 245 | 17 | yes | 43.7 | 43.4 | +0.3 | -1.9 [-6.7, +1.7] |
| ASIA | none | FULL | STRONG | 51 | 37 | no | 66.7 | 56.1 | +10.5 | +2.6 [-2.5, +7.0] |
| ASIA | none | FULL | MEDIUM | 251 | 37 | yes | 50.6 | 56.1 | -5.5 | -3.5 [-5.7, -1.4] |
| ASIA | none | FULL | WEAK | 432 | 37 | yes | 55.6 | 56.1 | -0.6 | -2.4 [-4.9, 0.0] |
| ASIA | none | H1 | STRONG | 16 | 18 | no | 56.3 | 56.1 | +0.1 | -1.8 [-6.8, +3.2] |
| ASIA | none | H1 | MEDIUM | 117 | 18 | yes | 49.6 | 56.1 | -6.6 | -4.7 [-7.6, -2.0] |
| ASIA | none | H1 | WEAK | 162 | 18 | yes | 54.3 | 56.1 | -1.8 | -2.4 [-6.0, +0.9] |
| ASIA | none | H2 | STRONG | 35 | 19 | no | 71.4 | 56.1 | +15.3 | +4.6 [-2.4, +10.0] |
| ASIA | none | H2 | MEDIUM | 134 | 19 | yes | 51.5 | 56.1 | -4.6 | -2.5 [-5.7, +0.6] |
| ASIA | none | H2 | WEAK | 270 | 19 | yes | 56.3 | 56.1 | +0.2 | -2.3 [-5.7, +0.9] |
| ASIA | none | R1 | STRONG | 17 | 20 | no | 58.8 | 56.1 | +2.7 | -1.4 [-6.1, +3.5] |
| ASIA | none | R1 | MEDIUM | 120 | 20 | yes | 50.0 | 56.1 | -6.1 | -4.7 [-7.6, -2.0] |
| ASIA | none | R1 | WEAK | 171 | 20 | yes | 53.2 | 56.1 | -2.9 | -2.7 [-6.1, +0.6] |
| ASIA | none | R2 | STRONG | 34 | 17 | no | 70.6 | 56.1 | +14.4 | +4.6 [-3.0, +10.0] |
| ASIA | none | R2 | MEDIUM | 131 | 17 | yes | 51.1 | 56.1 | -5.0 | -2.5 [-5.8, +0.6] |
| ASIA | none | R2 | WEAK | 261 | 17 | yes | 57.1 | 56.1 | +0.9 | -2.2 [-5.7, +1.1] |
| ASIA | swing | FULL | STRONG | 12 | 42 | no | 66.7 | 53.1 | +13.6 | +6.5 [+0.2, +14.7] |
| ASIA | swing | FULL | MEDIUM | 141 | 42 | yes | 41.8 | 46.7 | -4.9 | -3.8 [-7.2, -0.6] |
| ASIA | swing | FULL | WEAK | 693 | 42 | yes | 41.0 | 42.5 | -1.6 | -1.5 [-3.9, +0.7] |
| ASIA | swing | H1 | STRONG | 2 | 19 | no | 100.0 | 60.5 | +39.5 | +6.2 [+6.1, +6.3] |
| ASIA | swing | H1 | MEDIUM | 55 | 19 | no | 21.8 | 43.8 | -22.0 | -8.8 [-12.7, -5.2] |
| ASIA | swing | H1 | WEAK | 255 | 19 | yes | 41.6 | 41.7 | -0.1 | -1.7 [-4.8, +1.3] |
| ASIA | swing | H2 | STRONG | 10 | 23 | no | 60.0 | 52.1 | +7.9 | +6.6 [-1.2, +17.3] |
| ASIA | swing | H2 | MEDIUM | 86 | 23 | no | 54.7 | 48.1 | +6.6 | -0.6 [-5.1, +3.5] |
| ASIA | swing | H2 | WEAK | 438 | 23 | yes | 40.6 | 42.9 | -2.3 | -1.4 [-4.6, +1.7] |
| ASIA | swing | R1 | STRONG | 4 | 25 | no | 75.0 | 52.5 | +22.5 | +2.3 [-11.1, +7.8] |
| ASIA | swing | R1 | MEDIUM | 65 | 25 | no | 27.7 | 43.8 | -16.1 | -7.4 [-10.9, -4.1] |
| ASIA | swing | R1 | WEAK | 323 | 25 | yes | 39.9 | 41.3 | -1.4 | -1.9 [-4.5, +0.6] |
| ASIA | swing | R2 | STRONG | 8 | 17 | no | 62.5 | 53.3 | +9.2 | +8.7 [+0.8, +22.9] |
| ASIA | swing | R2 | MEDIUM | 76 | 17 | no | 53.9 | 48.4 | +5.6 | -0.7 [-5.9, +3.7] |
| ASIA | swing | R2 | WEAK | 370 | 17 | yes | 41.9 | 43.2 | -1.3 | -1.2 [-4.9, +2.5] |

## 5. Per-sub-sample tier cells, carried 24 h

| Session | Target set by | Sub-sample | Tier | n | Days | Readable | Success rate % | Gross BE % | Gross edge pp | Net EV bps [CI] |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | none | FULL | STRONG | 259 | 49 | yes | 51.4 | 47.8 | +3.6 | -1.8 [-4.9, +1.1] |
| NY | none | FULL | MEDIUM | 939 | 49 | yes | 43.3 | 47.8 | -4.4 | -4.5 [-6.2, -2.7] |
| NY | none | FULL | WEAK | 1654 | 49 | yes | 43.3 | 47.7 | -4.4 | -3.9 [-5.1, -2.8] |
| NY | none | H1 | STRONG | 101 | 24 | yes | 47.5 | 47.8 | -0.2 | -2.8 [-5.2, -0.2] |
| NY | none | H1 | MEDIUM | 366 | 24 | yes | 43.4 | 47.8 | -4.3 | -4.3 [-6.3, -2.3] |
| NY | none | H1 | WEAK | 657 | 24 | yes | 44.0 | 47.7 | -3.7 | -4.2 [-5.4, -3.1] |
| NY | none | H2 | STRONG | 158 | 25 | yes | 53.8 | 47.8 | +6.0 | -1.2 [-6.4, +3.0] |
| NY | none | H2 | MEDIUM | 573 | 25 | yes | 43.3 | 47.7 | -4.5 | -4.5 [-7.2, -2.0] |
| NY | none | H2 | WEAK | 997 | 25 | yes | 42.9 | 47.7 | -4.8 | -3.8 [-5.5, -2.0] |
| NY | none | R1 | STRONG | 159 | 32 | yes | 56.0 | 47.8 | +8.2 | -0.2 [-3.7, +3.0] |
| NY | none | R1 | MEDIUM | 510 | 32 | yes | 45.5 | 47.8 | -2.3 | -3.6 [-5.5, -1.5] |
| NY | none | R1 | WEAK | 866 | 32 | yes | 44.7 | 47.7 | -3.0 | -3.5 [-5.0, -1.9] |
| NY | none | R2 | STRONG | 100 | 17 | yes | 44.0 | 47.8 | -3.8 | -4.4 [-9.7, +0.7] |
| NY | none | R2 | MEDIUM | 429 | 17 | yes | 40.8 | 47.7 | -7.0 | -5.5 [-8.5, -2.8] |
| NY | none | R2 | WEAK | 788 | 17 | yes | 41.9 | 47.7 | -5.8 | -4.5 [-6.2, -2.9] |
| NY | swing | FULL | STRONG | 59 | 49 | no | 23.7 | 43.0 | -19.2 | -10.0 [-15.3, -4.7] |
| NY | swing | FULL | MEDIUM | 488 | 49 | yes | 39.1 | 43.2 | -4.0 | -4.4 [-6.6, -2.1] |
| NY | swing | FULL | WEAK | 1211 | 49 | yes | 41.9 | 41.1 | +0.8 | -2.5 [-4.5, -0.7] |
| NY | swing | H1 | STRONG | 25 | 24 | no | 28.0 | 41.6 | -13.6 | -7.1 [-12.0, -1.1] |
| NY | swing | H1 | MEDIUM | 231 | 24 | yes | 34.6 | 42.3 | -7.7 | -4.8 [-8.0, -1.6] |
| NY | swing | H1 | WEAK | 527 | 24 | yes | 39.5 | 40.5 | -1.0 | -2.8 [-5.8, +0.2] |
| NY | swing | H2 | STRONG | 34 | 25 | no | 20.6 | 43.7 | -23.1 | -12.2 [-19.4, -4.3] |
| NY | swing | H2 | MEDIUM | 257 | 25 | yes | 43.2 | 43.8 | -0.6 | -4.1 [-7.2, -0.8] |
| NY | swing | H2 | WEAK | 684 | 25 | yes | 43.9 | 41.5 | +2.3 | -2.4 [-4.7, -0.1] |
| NY | swing | R1 | STRONG | 40 | 32 | no | 22.5 | 43.0 | -20.5 | -10.0 [-16.6, -2.9] |
| NY | swing | R1 | MEDIUM | 292 | 32 | yes | 37.0 | 43.0 | -6.0 | -4.4 [-7.0, -1.6] |
| NY | swing | R1 | WEAK | 633 | 32 | yes | 41.2 | 40.5 | +0.7 | -2.1 [-4.9, +0.6] |
| NY | swing | R2 | STRONG | 19 | 17 | no | 26.3 | 42.9 | -16.6 | -10.0 [-17.2, -2.7] |
| NY | swing | R2 | MEDIUM | 196 | 17 | yes | 42.3 | 43.4 | -1.0 | -4.5 [-8.3, -0.4] |
| NY | swing | R2 | WEAK | 578 | 17 | yes | 42.7 | 41.7 | +1.1 | -3.1 [-5.7, -0.7] |
| LONDON | none | FULL | STRONG | 106 | 45 | yes | 45.3 | 44.4 | +0.8 | -0.6 [-5.1, +4.8] |
| LONDON | none | FULL | MEDIUM | 321 | 45 | yes | 53.9 | 44.4 | +9.5 | +1.2 [-1.8, +4.0] |
| LONDON | none | FULL | WEAK | 464 | 45 | yes | 48.7 | 44.3 | +4.4 | +0.1 [-3.4, +3.9] |
| LONDON | none | H1 | STRONG | 64 | 21 | no | 45.3 | 44.4 | +0.9 | -3.5 [-8.2, +0.9] |
| LONDON | none | H1 | MEDIUM | 154 | 21 | yes | 59.1 | 44.4 | +14.7 | +1.5 [-2.5, +5.6] |
| LONDON | none | H1 | WEAK | 218 | 21 | yes | 46.8 | 44.4 | +2.4 | -1.4 [-4.7, +1.5] |
| LONDON | none | H2 | STRONG | 42 | 24 | no | 45.2 | 44.4 | +0.8 | +3.7 [-4.8, +17.5] |
| LONDON | none | H2 | MEDIUM | 167 | 24 | yes | 49.1 | 44.4 | +4.7 | +1.0 [-3.3, +5.2] |
| LONDON | none | H2 | WEAK | 246 | 24 | yes | 50.4 | 44.3 | +6.1 | +1.5 [-4.5, +7.7] |
| LONDON | none | R1 | STRONG | 73 | 28 | no | 41.1 | 44.4 | -3.4 | -4.3 [-8.6, -0.2] |
| LONDON | none | R1 | MEDIUM | 181 | 28 | yes | 57.5 | 44.4 | +13.1 | +0.9 [-2.7, +4.4] |
| LONDON | none | R1 | WEAK | 252 | 28 | yes | 44.8 | 44.4 | +0.4 | -1.9 [-4.9, +0.7] |
| LONDON | none | R2 | STRONG | 33 | 17 | no | 54.5 | 44.4 | +10.1 | +7.5 [-2.8, +23.1] |
| LONDON | none | R2 | MEDIUM | 140 | 17 | yes | 49.3 | 44.4 | +4.8 | +1.7 [-3.3, +6.4] |
| LONDON | none | R2 | WEAK | 212 | 17 | yes | 53.3 | 44.3 | +9.0 | +2.5 [-4.3, +9.4] |
| LONDON | swing | FULL | STRONG | 22 | 45 | no | 36.4 | 46.5 | -10.2 | -8.3 [-16.6, -2.3] |
| LONDON | swing | FULL | MEDIUM | 136 | 45 | yes | 46.3 | 46.8 | -0.5 | -3.1 [-6.1, -0.5] |
| LONDON | swing | FULL | WEAK | 484 | 45 | yes | 48.8 | 42.7 | +6.0 | -1.9 [-5.8, +1.5] |
| LONDON | swing | H1 | STRONG | 9 | 22 | no | 22.2 | 44.6 | -22.4 | -11.3 [-20.7, -2.1] |
| LONDON | swing | H1 | MEDIUM | 49 | 22 | no | 40.8 | 42.9 | -2.1 | -4.6 [-8.2, -1.5] |
| LONDON | swing | H1 | WEAK | 209 | 22 | yes | 47.8 | 41.8 | +6.0 | -2.3 [-5.6, +0.7] |
| LONDON | swing | H2 | STRONG | 13 | 23 | no | 46.2 | 47.5 | -1.4 | -6.3 [-19.9, +1.2] |
| LONDON | swing | H2 | MEDIUM | 87 | 23 | no | 49.4 | 48.6 | +0.8 | -2.2 [-6.6, +1.5] |
| LONDON | swing | H2 | WEAK | 275 | 23 | yes | 49.5 | 43.1 | +6.3 | -1.7 [-8.2, +3.9] |
| LONDON | swing | R1 | STRONG | 10 | 28 | no | 20.0 | 43.8 | -23.8 | -11.4 [-19.4, -3.0] |
| LONDON | swing | R1 | MEDIUM | 57 | 28 | no | 38.6 | 42.3 | -3.7 | -4.9 [-8.2, -1.7] |
| LONDON | swing | R1 | WEAK | 239 | 28 | yes | 49.0 | 41.5 | +7.5 | -2.0 [-5.0, +0.8] |
| LONDON | swing | R2 | STRONG | 12 | 17 | no | 50.0 | 48.1 | +1.9 | -5.8 [-21.8, +2.6] |
| LONDON | swing | R2 | MEDIUM | 79 | 17 | no | 51.9 | 49.3 | +2.6 | -1.8 [-6.3, +2.1] |
| LONDON | swing | R2 | WEAK | 245 | 17 | yes | 48.6 | 43.4 | +5.1 | -1.9 [-9.4, +4.2] |
| ASIA | none | FULL | STRONG | 51 | 37 | no | 68.6 | 56.1 | +12.5 | +3.0 [-3.6, +8.6] |
| ASIA | none | FULL | MEDIUM | 251 | 37 | yes | 52.2 | 56.1 | -3.9 | -3.8 [-6.0, -1.6] |
| ASIA | none | FULL | WEAK | 432 | 37 | yes | 57.9 | 56.1 | +1.7 | -2.3 [-4.8, +0.2] |
| ASIA | none | H1 | STRONG | 16 | 18 | no | 56.3 | 56.1 | +0.1 | -2.6 [-8.8, +3.0] |
| ASIA | none | H1 | MEDIUM | 117 | 18 | yes | 50.4 | 56.1 | -5.7 | -4.8 [-7.8, -2.3] |
| ASIA | none | H1 | WEAK | 162 | 18 | yes | 56.8 | 56.1 | +0.7 | -2.6 [-6.1, +0.8] |
| ASIA | none | H2 | STRONG | 35 | 19 | no | 74.3 | 56.1 | +18.1 | +5.6 [-3.4, +12.3] |
| ASIA | none | H2 | MEDIUM | 134 | 19 | yes | 53.7 | 56.1 | -2.4 | -2.8 [-6.1, +0.4] |
| ASIA | none | H2 | WEAK | 270 | 19 | yes | 58.5 | 56.1 | +2.4 | -2.1 [-5.4, +1.1] |
| ASIA | none | R1 | STRONG | 17 | 20 | no | 58.8 | 56.1 | +2.7 | -2.1 [-8.0, +3.2] |
| ASIA | none | R1 | MEDIUM | 120 | 20 | yes | 50.8 | 56.1 | -5.3 | -4.8 [-7.6, -2.1] |
| ASIA | none | R1 | WEAK | 171 | 20 | yes | 55.6 | 56.1 | -0.6 | -2.8 [-6.2, +0.4] |
| ASIA | none | R2 | STRONG | 34 | 17 | no | 73.5 | 56.1 | +17.4 | +5.5 [-4.1, +12.6] |
| ASIA | none | R2 | MEDIUM | 131 | 17 | yes | 53.4 | 56.1 | -2.7 | -2.8 [-6.3, +0.4] |
| ASIA | none | R2 | WEAK | 261 | 17 | yes | 59.4 | 56.1 | +3.2 | -1.9 [-5.3, +1.4] |
| ASIA | swing | FULL | STRONG | 12 | 42 | no | 83.3 | 53.1 | +30.2 | +8.9 [+2.4, +17.4] |
| ASIA | swing | FULL | MEDIUM | 141 | 42 | yes | 45.4 | 46.7 | -1.3 | -4.0 [-7.7, -0.6] |
| ASIA | swing | FULL | WEAK | 693 | 42 | yes | 46.6 | 42.5 | +4.1 | -1.9 [-4.7, +0.9] |
| ASIA | swing | H1 | STRONG | 2 | 19 | no | 100.0 | 60.5 | +39.5 | +6.2 [+6.1, +6.3] |
| ASIA | swing | H1 | MEDIUM | 55 | 19 | no | 21.8 | 43.8 | -22.0 | -10.4 [-13.7, -6.9] |
| ASIA | swing | H1 | WEAK | 255 | 19 | yes | 47.5 | 41.7 | +5.8 | -1.9 [-5.6, +2.1] |
| ASIA | swing | H2 | STRONG | 10 | 23 | no | 80.0 | 52.1 | +27.9 | +9.4 [+1.9, +20.6] |
| ASIA | swing | H2 | MEDIUM | 86 | 23 | no | 60.5 | 48.1 | +12.4 | +0.1 [-4.4, +4.1] |
| ASIA | swing | H2 | WEAK | 438 | 23 | yes | 46.1 | 42.9 | +3.2 | -1.9 [-5.5, +2.0] |
| ASIA | swing | R1 | STRONG | 4 | 25 | no | 75.0 | 52.5 | +22.5 | +2.3 [-11.1, +7.8] |
| ASIA | swing | R1 | MEDIUM | 65 | 25 | no | 27.7 | 43.8 | -16.1 | -8.7 [-11.9, -5.3] |
| ASIA | swing | R1 | WEAK | 323 | 25 | yes | 44.6 | 41.3 | +3.3 | -2.3 [-5.4, +1.0] |
| ASIA | swing | R2 | STRONG | 8 | 17 | no | 87.5 | 53.3 | +34.2 | +12.2 [+4.5, +27.1] |
| ASIA | swing | R2 | MEDIUM | 76 | 17 | no | 60.5 | 48.4 | +12.2 | +0.1 [-5.2, +4.4] |
| ASIA | swing | R2 | WEAK | 370 | 17 | yes | 48.4 | 43.2 | +5.2 | -1.4 [-5.8, +2.9] |

## 6. Per-sub-sample differences, main window

| Session | Target set by | Comparison | Sub-sample | n first / n second | Readable | Difference bps [CI] | Difference bps, 2 dp | CI excludes 0 | Bootstrap SE |
|---|---|---|---|---|---|---|---|---|---|
| NY | none | C1 WEAK vs MEDIUM | FULL | 1654 / 939 | yes | +0.5 [-0.9, +2.0] | +0.53 | no | 0.76 |
| NY | none | C1 WEAK vs MEDIUM | H1 | 657 / 366 | yes | 0.0 [-1.8, +1.8] | 0.00 | no | 0.92 |
| NY | none | C1 WEAK vs MEDIUM | H2 | 997 / 573 | yes | +0.9 [-1.1, +3.1] | +0.86 | no | 1.09 |
| NY | none | C1 WEAK vs MEDIUM | R1 | 866 / 510 | yes | 0.0 [-1.5, +1.5] | -0.03 | no | 0.76 |
| NY | none | C1 WEAK vs MEDIUM | R2 | 788 / 429 | yes | +1.2 [-1.2, +4.0] | +1.22 | no | 1.34 |
| NY | none | C2 STRONG vs MEDIUM | FULL | 259 / 939 | yes | +2.6 [0.0, +4.9] | +2.56 | no | 1.26 |
| NY | none | C2 STRONG vs MEDIUM | H1 | 101 / 366 | yes | +1.3 [-1.3, +4.2] | +1.30 | no | 1.39 |
| NY | none | C2 STRONG vs MEDIUM | H2 | 158 / 573 | yes | +3.4 [-0.8, +6.5] | +3.37 | no | 1.85 |
| NY | none | C2 STRONG vs MEDIUM | R1 | 159 / 510 | yes | +3.3 [+0.4, +5.6] | +3.30 | yes | 1.32 |
| NY | none | C2 STRONG vs MEDIUM | R2 | 100 / 429 | yes | +1.0 [-3.4, +5.7] | +1.03 | no | 2.37 |
| NY | none | C3 WEAK vs STRONG | FULL | 1654 / 259 | yes | -2.0 [-4.6, +0.8] | -2.04 | no | 1.38 |
| NY | none | C3 WEAK vs STRONG | H1 | 657 / 101 | yes | -1.3 [-3.9, +1.2] | -1.30 | no | 1.29 |
| NY | none | C3 WEAK vs STRONG | H2 | 997 / 158 | yes | -2.5 [-6.1, +2.1] | -2.51 | no | 2.12 |
| NY | none | C3 WEAK vs STRONG | R1 | 866 / 159 | yes | -3.3 [-5.7, -0.5] | -3.34 | yes | 1.36 |
| NY | none | C3 WEAK vs STRONG | R2 | 788 / 100 | yes | +0.2 [-4.9, +5.3] | +0.20 | no | 2.61 |
| NY | swing | C1 WEAK vs MEDIUM | FULL | 1211 / 488 | yes | +1.6 [-0.7, +3.6] | +1.56 | no | 1.08 |
| NY | swing | C1 WEAK vs MEDIUM | H1 | 527 / 231 | yes | +2.1 [-1.0, +5.0] | +2.11 | no | 1.52 |
| NY | swing | C1 WEAK vs MEDIUM | H2 | 684 / 257 | yes | +1.1 [-2.1, +3.8] | +1.09 | no | 1.52 |
| NY | swing | C1 WEAK vs MEDIUM | R1 | 633 / 292 | yes | +2.4 [-0.5, +4.9] | +2.35 | no | 1.38 |
| NY | swing | C1 WEAK vs MEDIUM | R2 | 578 / 196 | yes | +0.6 [-2.9, +3.8] | +0.65 | no | 1.70 |
| NY | swing | C2 STRONG vs MEDIUM | FULL | 59 / 488 | NOT READABLE | -4.9 [-10.2, +0.6] | -4.90 | no | 2.77 |
| NY | swing | C2 STRONG vs MEDIUM | H1 | 25 / 231 | NOT READABLE | -2.3 [-7.4, +3.1] | -2.27 | no | 2.69 |
| NY | swing | C2 STRONG vs MEDIUM | H2 | 34 / 257 | NOT READABLE | -6.9 [-14.3, +1.5] | -6.88 | no | 4.15 |
| NY | swing | C2 STRONG vs MEDIUM | R1 | 40 / 292 | NOT READABLE | -5.5 [-12.0, +1.3] | -5.48 | no | 3.48 |
| NY | swing | C2 STRONG vs MEDIUM | R2 | 19 / 196 | NOT READABLE | -3.6 [-11.4, +4.3] | -3.60 | no | 4.00 |
| NY | swing | C3 WEAK vs STRONG | FULL | 1211 / 59 | NOT READABLE | +6.5 [+1.1, +12.1] | +6.45 | yes | 2.87 |
| NY | swing | C3 WEAK vs STRONG | H1 | 527 / 25 | NOT READABLE | +4.4 [-0.8, +9.4] | +4.38 | no | 2.57 |
| NY | swing | C3 WEAK vs STRONG | H2 | 684 / 34 | NOT READABLE | +8.0 [-0.2, +15.8] | +7.96 | no | 4.31 |
| NY | swing | C3 WEAK vs STRONG | R1 | 633 / 40 | NOT READABLE | +7.8 [+0.5, +15.4] | +7.84 | yes | 3.94 |
| NY | swing | C3 WEAK vs STRONG | R2 | 578 / 19 | NOT READABLE | +4.3 [-2.3, +10.5] | +4.25 | no | 3.25 |
| LONDON | none | C1 WEAK vs MEDIUM | FULL | 464 / 321 | yes | -0.9 [-4.0, +2.5] | -0.86 | no | 1.67 |
| LONDON | none | C1 WEAK vs MEDIUM | H1 | 218 / 154 | yes | -2.8 [-5.9, -0.1] | -2.76 | yes | 1.49 |
| LONDON | none | C1 WEAK vs MEDIUM | H2 | 246 / 167 | yes | +0.8 [-4.4, +6.4] | +0.84 | no | 2.78 |
| LONDON | none | C1 WEAK vs MEDIUM | R1 | 252 / 181 | yes | -2.6 [-5.4, -0.2] | -2.64 | yes | 1.31 |
| LONDON | none | C1 WEAK vs MEDIUM | R2 | 212 / 140 | yes | +1.2 [-4.7, +7.5] | +1.23 | no | 3.12 |
| LONDON | none | C2 STRONG vs MEDIUM | FULL | 106 / 321 | yes | -1.9 [-6.9, +3.7] | -1.88 | no | 2.72 |
| LONDON | none | C2 STRONG vs MEDIUM | H1 | 64 / 154 | NOT READABLE | -4.4 [-10.1, +0.9] | -4.37 | no | 2.79 |
| LONDON | none | C2 STRONG vs MEDIUM | H2 | 42 / 167 | NOT READABLE | +1.5 [-8.1, +15.7] | +1.55 | no | 5.99 |
| LONDON | none | C2 STRONG vs MEDIUM | R1 | 73 / 181 | NOT READABLE | -4.4 [-9.4, +0.1] | -4.45 | no | 2.43 |
| LONDON | none | C2 STRONG vs MEDIUM | R2 | 33 / 140 | NOT READABLE | +3.9 [-8.0, +20.4] | +3.94 | no | 7.32 |
| LONDON | none | C3 WEAK vs STRONG | FULL | 464 / 106 | yes | +1.0 [-4.5, +5.8] | +1.02 | no | 2.59 |
| LONDON | none | C3 WEAK vs STRONG | H1 | 218 / 64 | NOT READABLE | +1.6 [-4.0, +6.7] | +1.61 | no | 2.73 |
| LONDON | none | C3 WEAK vs STRONG | H2 | 246 / 42 | NOT READABLE | -0.7 [-14.4, +8.1] | -0.71 | no | 5.77 |
| LONDON | none | C3 WEAK vs STRONG | R1 | 252 / 73 | NOT READABLE | +1.8 [-3.3, +6.4] | +1.81 | no | 2.48 |
| LONDON | none | C3 WEAK vs STRONG | R2 | 212 / 33 | NOT READABLE | -2.7 [-18.2, +8.9] | -2.71 | no | 6.97 |
| LONDON | swing | C1 WEAK vs MEDIUM | FULL | 484 / 136 | yes | +0.7 [-2.3, +3.6] | +0.73 | no | 1.48 |
| LONDON | swing | C1 WEAK vs MEDIUM | H1 | 209 / 49 | NOT READABLE | +1.3 [-2.3, +5.1] | +1.28 | no | 1.88 |
| LONDON | swing | C1 WEAK vs MEDIUM | H2 | 275 / 87 | NOT READABLE | +0.5 [-4.1, +4.5] | +0.53 | no | 2.21 |
| LONDON | swing | C1 WEAK vs MEDIUM | R1 | 239 / 57 | NOT READABLE | +1.7 [-1.7, +5.2] | +1.74 | no | 1.77 |
| LONDON | swing | C1 WEAK vs MEDIUM | R2 | 245 / 79 | NOT READABLE | +0.1 [-5.1, +4.4] | +0.06 | no | 2.39 |
| LONDON | swing | C2 STRONG vs MEDIUM | FULL | 22 / 136 | NOT READABLE | -1.2 [-7.8, +4.2] | -1.20 | no | 3.01 |
| LONDON | swing | C2 STRONG vs MEDIUM | H1 | 9 / 49 | NOT READABLE | -3.5 [-13.1, +4.5] | -3.53 | no | 4.42 |
| LONDON | swing | C2 STRONG vs MEDIUM | H2 | 13 / 87 | NOT READABLE | +0.6 [-9.1, +7.7] | +0.56 | no | 4.08 |
| LONDON | swing | C2 STRONG vs MEDIUM | R1 | 10 / 57 | NOT READABLE | -3.9 [-12.4, +3.1] | -3.87 | no | 3.93 |
| LONDON | swing | C2 STRONG vs MEDIUM | R2 | 12 / 79 | NOT READABLE | +1.2 [-9.5, +9.1] | +1.17 | no | 4.45 |
| LONDON | swing | C3 WEAK vs STRONG | FULL | 484 / 22 | NOT READABLE | +1.9 [-3.2, +8.3] | +1.93 | no | 2.92 |
| LONDON | swing | C3 WEAK vs STRONG | H1 | 209 / 9 | NOT READABLE | +4.8 [-2.6, +14.0] | +4.81 | no | 4.15 |
| LONDON | swing | C3 WEAK vs STRONG | H2 | 275 / 13 | NOT READABLE | 0.0 [-7.4, +9.6] | -0.03 | no | 4.14 |
| LONDON | swing | C3 WEAK vs STRONG | R1 | 239 / 10 | NOT READABLE | +5.6 [-1.1, +13.7] | +5.61 | no | 3.71 |
| LONDON | swing | C3 WEAK vs STRONG | R2 | 245 / 12 | NOT READABLE | -1.1 [-9.2, +9.0] | -1.11 | no | 4.44 |
| ASIA | none | C1 WEAK vs MEDIUM | FULL | 432 / 251 | yes | +1.2 [-1.6, +3.8] | +1.17 | no | 1.38 |
| ASIA | none | C1 WEAK vs MEDIUM | H1 | 162 / 117 | yes | +2.3 [-0.9, +5.2] | +2.32 | no | 1.57 |
| ASIA | none | C1 WEAK vs MEDIUM | H2 | 270 / 134 | yes | +0.2 [-4.4, +4.1] | +0.16 | no | 2.15 |
| ASIA | none | C1 WEAK vs MEDIUM | R1 | 171 / 120 | yes | +2.0 [-1.1, +4.8] | +1.96 | no | 1.53 |
| ASIA | none | C1 WEAK vs MEDIUM | R2 | 261 / 131 | yes | +0.4 [-4.2, +4.4] | +0.37 | no | 2.20 |
| ASIA | none | C2 STRONG vs MEDIUM | FULL | 51 / 251 | NOT READABLE | +6.2 [+1.7, +10.0] | +6.15 | yes | 2.13 |
| ASIA | none | C2 STRONG vs MEDIUM | H1 | 16 / 117 | NOT READABLE | +2.9 [-2.2, +9.3] | +2.89 | no | 2.86 |
| ASIA | none | C2 STRONG vs MEDIUM | H2 | 35 / 134 | NOT READABLE | +7.1 [+1.7, +11.0] | +7.14 | yes | 2.34 |
| ASIA | none | C2 STRONG vs MEDIUM | R1 | 17 / 120 | NOT READABLE | +3.3 [-1.6, +9.4] | +3.29 | no | 2.82 |
| ASIA | none | C2 STRONG vs MEDIUM | R2 | 34 / 131 | NOT READABLE | +7.1 [+1.3, +11.1] | +7.12 | yes | 2.44 |
| ASIA | none | C3 WEAK vs STRONG | FULL | 432 / 51 | NOT READABLE | -5.0 [-10.2, +0.4] | -4.98 | no | 2.67 |
| ASIA | none | C3 WEAK vs STRONG | H1 | 162 / 16 | NOT READABLE | -0.6 [-7.3, +4.5] | -0.58 | no | 2.95 |
| ASIA | none | C3 WEAK vs STRONG | H2 | 270 / 35 | NOT READABLE | -7.0 [-13.5, +0.2] | -6.98 | no | 3.48 |
| ASIA | none | C3 WEAK vs STRONG | R1 | 171 / 17 | NOT READABLE | -1.3 [-7.7, +3.6] | -1.33 | no | 2.89 |
| ASIA | none | C3 WEAK vs STRONG | R2 | 261 / 34 | NOT READABLE | -6.8 [-13.3, +0.8] | -6.76 | no | 3.61 |
| ASIA | swing | C1 WEAK vs MEDIUM | FULL | 693 / 141 | yes | +2.3 [-0.6, +5.4] | +2.25 | no | 1.53 |
| ASIA | swing | C1 WEAK vs MEDIUM | H1 | 255 / 55 | NOT READABLE | +7.1 [+3.3, +11.5] | +7.11 | yes | 2.14 |
| ASIA | swing | C1 WEAK vs MEDIUM | H2 | 438 / 86 | NOT READABLE | -0.9 [-4.8, +3.2] | -0.87 | no | 2.06 |
| ASIA | swing | C1 WEAK vs MEDIUM | R1 | 323 / 65 | NOT READABLE | +5.5 [+1.9, +9.5] | +5.47 | yes | 1.93 |
| ASIA | swing | C1 WEAK vs MEDIUM | R2 | 370 / 76 | NOT READABLE | -0.5 [-5.0, +4.1] | -0.50 | no | 2.28 |
| ASIA | swing | C2 STRONG vs MEDIUM | FULL | 12 / 141 | NOT READABLE | +10.3 [+4.7, +17.7] | +10.29 | yes | 3.32 |
| ASIA | swing | C2 STRONG vs MEDIUM | H1 | 2 / 55 | NOT READABLE | +15.0 [+11.3, +18.8] | +14.95 | yes | 1.89 |
| ASIA | swing | C2 STRONG vs MEDIUM | H2 | 10 / 86 | NOT READABLE | +7.2 [+0.4, +16.5] | +7.16 | yes | 4.13 |
| ASIA | swing | C2 STRONG vs MEDIUM | R1 | 4 / 65 | NOT READABLE | +9.6 [-1.0, +16.9] | +9.64 | no | 4.44 |
| ASIA | swing | C2 STRONG vs MEDIUM | R2 | 8 / 76 | NOT READABLE | +9.3 [+2.2, +22.6] | +9.34 | yes | 5.22 |
| ASIA | swing | C3 WEAK vs STRONG | FULL | 693 / 12 | NOT READABLE | -8.0 [-15.4, -2.1] | -8.04 | yes | 3.35 |
| ASIA | swing | C3 WEAK vs STRONG | H1 | 255 / 2 | NOT READABLE | -7.8 [-10.8, -4.9] | -7.84 | yes | 1.54 |
| ASIA | swing | C3 WEAK vs STRONG | H2 | 438 / 10 | NOT READABLE | -8.0 [-17.3, -0.9] | -8.03 | yes | 4.19 |
| ASIA | swing | C3 WEAK vs STRONG | R1 | 323 / 4 | NOT READABLE | -4.2 [-10.8, +7.9] | -4.16 | no | 4.49 |
| ASIA | swing | C3 WEAK vs STRONG | R2 | 370 / 8 | NOT READABLE | -9.8 [-22.7, -2.5] | -9.84 | yes | 5.11 |

## 7. Per-sub-sample differences, carried 24 h

| Session | Target set by | Comparison | Sub-sample | n first / n second | Readable | Difference bps [CI] | Difference bps, 2 dp | CI excludes 0 | Bootstrap SE |
|---|---|---|---|---|---|---|---|---|---|
| NY | none | C1 WEAK vs MEDIUM | FULL | 1654 / 939 | yes | +0.5 [-0.9, +2.1] | +0.51 | no | 0.78 |
| NY | none | C1 WEAK vs MEDIUM | H1 | 657 / 366 | yes | +0.1 [-2.1, +2.3] | +0.14 | no | 1.11 |
| NY | none | C1 WEAK vs MEDIUM | H2 | 997 / 573 | yes | +0.8 [-1.2, +3.0] | +0.76 | no | 1.09 |
| NY | none | C1 WEAK vs MEDIUM | R1 | 866 / 510 | yes | +0.1 [-1.6, +1.8] | +0.09 | no | 0.88 |
| NY | none | C1 WEAK vs MEDIUM | R2 | 788 / 429 | yes | +1.1 [-1.3, +3.8] | +1.06 | no | 1.32 |
| NY | none | C2 STRONG vs MEDIUM | FULL | 259 / 939 | yes | +2.7 [0.0, +5.1] | +2.66 | no | 1.29 |
| NY | none | C2 STRONG vs MEDIUM | H1 | 101 / 366 | yes | +1.5 [-1.2, +4.6] | +1.52 | no | 1.50 |
| NY | none | C2 STRONG vs MEDIUM | H2 | 158 / 573 | yes | +3.4 [-0.7, +6.6] | +3.39 | no | 1.86 |
| NY | none | C2 STRONG vs MEDIUM | R1 | 159 / 510 | yes | +3.4 [+0.4, +5.7] | +3.37 | yes | 1.34 |
| NY | none | C2 STRONG vs MEDIUM | R2 | 100 / 429 | yes | +1.2 [-3.4, +5.8] | +1.16 | no | 2.40 |
| NY | none | C3 WEAK vs STRONG | FULL | 1654 / 259 | yes | -2.1 [-4.7, +0.6] | -2.14 | no | 1.37 |
| NY | none | C3 WEAK vs STRONG | H1 | 657 / 101 | yes | -1.4 [-4.1, +1.2] | -1.38 | no | 1.37 |
| NY | none | C3 WEAK vs STRONG | H2 | 997 / 158 | yes | -2.6 [-6.3, +1.9] | -2.62 | no | 2.08 |
| NY | none | C3 WEAK vs STRONG | R1 | 866 / 159 | yes | -3.3 [-5.6, -0.4] | -3.28 | yes | 1.33 |
| NY | none | C3 WEAK vs STRONG | R2 | 788 / 100 | yes | -0.1 [-5.1, +4.9] | -0.10 | no | 2.59 |
| NY | swing | C1 WEAK vs MEDIUM | FULL | 1211 / 488 | yes | +1.9 [-0.6, +4.1] | +1.88 | no | 1.18 |
| NY | swing | C1 WEAK vs MEDIUM | H1 | 527 / 231 | yes | +2.0 [-1.1, +4.9] | +2.02 | no | 1.52 |
| NY | swing | C1 WEAK vs MEDIUM | H2 | 684 / 257 | yes | +1.7 [-2.0, +5.0] | +1.72 | no | 1.80 |
| NY | swing | C1 WEAK vs MEDIUM | R1 | 633 / 292 | yes | +2.3 [-0.7, +5.0] | +2.32 | no | 1.46 |
| NY | swing | C1 WEAK vs MEDIUM | R2 | 578 / 196 | yes | +1.4 [-2.6, +5.1] | +1.42 | no | 2.00 |
| NY | swing | C2 STRONG vs MEDIUM | FULL | 59 / 488 | NOT READABLE | -5.6 [-10.9, -0.2] | -5.58 | yes | 2.75 |
| NY | swing | C2 STRONG vs MEDIUM | H1 | 25 / 231 | NOT READABLE | -2.3 [-7.7, +3.8] | -2.26 | no | 2.94 |
| NY | swing | C2 STRONG vs MEDIUM | H2 | 34 / 257 | NOT READABLE | -8.1 [-15.5, -0.1] | -8.08 | yes | 4.02 |
| NY | swing | C2 STRONG vs MEDIUM | R1 | 40 / 292 | NOT READABLE | -5.6 [-12.0, +1.5] | -5.63 | no | 3.55 |
| NY | swing | C2 STRONG vs MEDIUM | R2 | 19 / 196 | NOT READABLE | -5.5 [-13.5, +2.1] | -5.51 | no | 3.95 |
| NY | swing | C3 WEAK vs STRONG | FULL | 1211 / 59 | NOT READABLE | +7.5 [+1.9, +13.4] | +7.46 | yes | 2.95 |
| NY | swing | C3 WEAK vs STRONG | H1 | 527 / 25 | NOT READABLE | +4.3 [-1.5, +9.6] | +4.28 | no | 2.80 |
| NY | swing | C3 WEAK vs STRONG | H2 | 684 / 34 | NOT READABLE | +9.8 [+1.5, +18.0] | +9.80 | yes | 4.37 |
| NY | swing | C3 WEAK vs STRONG | R1 | 633 / 40 | NOT READABLE | +7.9 [+0.1, +15.8] | +7.95 | yes | 4.14 |
| NY | swing | C3 WEAK vs STRONG | R2 | 578 / 19 | NOT READABLE | +6.9 [+0.3, +14.0] | +6.92 | yes | 3.48 |
| LONDON | none | C1 WEAK vs MEDIUM | FULL | 464 / 321 | yes | -1.1 [-5.2, +3.2] | -1.12 | no | 2.13 |
| LONDON | none | C1 WEAK vs MEDIUM | H1 | 218 / 154 | yes | -3.0 [-6.1, -0.3] | -2.96 | yes | 1.47 |
| LONDON | none | C1 WEAK vs MEDIUM | H2 | 246 / 167 | yes | +0.5 [-6.7, +8.1] | +0.51 | no | 3.80 |
| LONDON | none | C1 WEAK vs MEDIUM | R1 | 252 / 181 | yes | -2.8 [-5.5, -0.3] | -2.80 | yes | 1.32 |
| LONDON | none | C1 WEAK vs MEDIUM | R2 | 212 / 140 | yes | +0.8 [-7.6, +9.7] | +0.83 | no | 4.41 |
| LONDON | none | C2 STRONG vs MEDIUM | FULL | 106 / 321 | yes | -1.9 [-6.6, +3.6] | -1.86 | no | 2.58 |
| LONDON | none | C2 STRONG vs MEDIUM | H1 | 64 / 154 | NOT READABLE | -5.0 [-11.5, +0.7] | -5.03 | no | 3.10 |
| LONDON | none | C2 STRONG vs MEDIUM | H2 | 42 / 167 | NOT READABLE | +2.8 [-5.3, +15.3] | +2.79 | no | 5.24 |
| LONDON | none | C2 STRONG vs MEDIUM | R1 | 73 / 181 | NOT READABLE | -5.2 [-10.8, -0.2] | -5.18 | yes | 2.70 |
| LONDON | none | C2 STRONG vs MEDIUM | R2 | 33 / 140 | NOT READABLE | +5.8 [-4.2, +21.0] | +5.80 | no | 6.38 |
| LONDON | none | C3 WEAK vs STRONG | FULL | 464 / 106 | yes | +0.7 [-6.0, +6.3] | +0.74 | no | 3.16 |
| LONDON | none | C3 WEAK vs STRONG | H1 | 218 / 64 | NOT READABLE | +2.1 [-3.8, +7.7] | +2.08 | no | 2.92 |
| LONDON | none | C3 WEAK vs STRONG | H2 | 246 / 42 | NOT READABLE | -2.3 [-17.7, +8.1] | -2.28 | no | 6.61 |
| LONDON | none | C3 WEAK vs STRONG | R1 | 252 / 73 | NOT READABLE | +2.4 [-3.0, +7.4] | +2.39 | no | 2.65 |
| LONDON | none | C3 WEAK vs STRONG | R2 | 212 / 33 | NOT READABLE | -5.0 [-22.7, +7.2] | -4.98 | no | 7.77 |
| LONDON | swing | C1 WEAK vs MEDIUM | FULL | 484 / 136 | yes | +1.2 [-2.8, +4.6] | +1.17 | no | 1.88 |
| LONDON | swing | C1 WEAK vs MEDIUM | H1 | 209 / 49 | NOT READABLE | +2.4 [-1.3, +6.0] | +2.39 | no | 1.82 |
| LONDON | swing | C1 WEAK vs MEDIUM | H2 | 275 / 87 | NOT READABLE | +0.6 [-5.9, +5.8] | +0.56 | no | 2.98 |
| LONDON | swing | C1 WEAK vs MEDIUM | R1 | 239 / 57 | NOT READABLE | +2.9 [-0.4, +6.2] | +2.92 | no | 1.69 |
| LONDON | swing | C1 WEAK vs MEDIUM | R2 | 245 / 79 | NOT READABLE | -0.1 [-7.4, +5.6] | -0.08 | no | 3.33 |
| LONDON | swing | C2 STRONG vs MEDIUM | FULL | 22 / 136 | NOT READABLE | -5.2 [-13.1, +1.1] | -5.24 | no | 3.67 |
| LONDON | swing | C2 STRONG vs MEDIUM | H1 | 9 / 49 | NOT READABLE | -6.6 [-15.2, +2.2] | -6.64 | no | 4.37 |
| LONDON | swing | C2 STRONG vs MEDIUM | H2 | 13 / 87 | NOT READABLE | -4.1 [-17.3, +4.6] | -4.07 | no | 5.51 |
| LONDON | swing | C2 STRONG vs MEDIUM | R1 | 10 / 57 | NOT READABLE | -6.5 [-14.2, +1.2] | -6.51 | no | 3.90 |
| LONDON | swing | C2 STRONG vs MEDIUM | R2 | 12 / 79 | NOT READABLE | -4.0 [-19.4, +5.8] | -3.99 | no | 6.17 |
| LONDON | swing | C3 WEAK vs STRONG | FULL | 484 / 22 | NOT READABLE | +6.4 [-0.8, +15.3] | +6.42 | no | 4.11 |
| LONDON | swing | C3 WEAK vs STRONG | H1 | 209 / 9 | NOT READABLE | +9.0 [-0.2, +17.8] | +9.03 | no | 4.51 |
| LONDON | swing | C3 WEAK vs STRONG | H2 | 275 / 13 | NOT READABLE | +4.6 [-6.2, +19.6] | +4.63 | no | 6.42 |
| LONDON | swing | C3 WEAK vs STRONG | R1 | 239 / 10 | NOT READABLE | +9.4 [+1.5, +17.1] | +9.43 | yes | 3.97 |
| LONDON | swing | C3 WEAK vs STRONG | R2 | 245 / 12 | NOT READABLE | +3.9 [-8.4, +20.6] | +3.91 | no | 7.22 |
| ASIA | none | C1 WEAK vs MEDIUM | FULL | 432 / 251 | yes | +1.5 [-1.4, +4.2] | +1.49 | no | 1.42 |
| ASIA | none | C1 WEAK vs MEDIUM | H1 | 162 / 117 | yes | +2.3 [-1.0, +5.4] | +2.27 | no | 1.61 |
| ASIA | none | C1 WEAK vs MEDIUM | H2 | 270 / 134 | yes | +0.7 [-3.9, +4.9] | +0.73 | no | 2.25 |
| ASIA | none | C1 WEAK vs MEDIUM | R1 | 171 / 120 | yes | +1.9 [-1.2, +4.9] | +1.92 | no | 1.56 |
| ASIA | none | C1 WEAK vs MEDIUM | R2 | 261 / 131 | yes | +1.0 [-3.8, +5.1] | +0.95 | no | 2.27 |
| ASIA | none | C2 STRONG vs MEDIUM | FULL | 51 / 251 | NOT READABLE | +6.7 [+0.8, +11.6] | +6.75 | yes | 2.81 |
| ASIA | none | C2 STRONG vs MEDIUM | H1 | 16 / 117 | NOT READABLE | +2.2 [-3.5, +8.8] | +2.22 | no | 3.13 |
| ASIA | none | C2 STRONG vs MEDIUM | H2 | 35 / 134 | NOT READABLE | +8.4 [+1.0, +13.3] | +8.36 | yes | 3.22 |
| ASIA | none | C2 STRONG vs MEDIUM | R1 | 17 / 120 | NOT READABLE | +2.7 [-2.9, +9.0] | +2.66 | no | 3.01 |
| ASIA | none | C2 STRONG vs MEDIUM | R2 | 34 / 131 | NOT READABLE | +8.4 [+0.5, +13.4] | +8.37 | yes | 3.33 |
| ASIA | none | C3 WEAK vs STRONG | FULL | 432 / 51 | NOT READABLE | -5.3 [-11.3, +1.4] | -5.25 | no | 3.31 |
| ASIA | none | C3 WEAK vs STRONG | H1 | 162 / 16 | NOT READABLE | +0.1 [-6.9, +6.1] | +0.05 | no | 3.27 |
| ASIA | none | C3 WEAK vs STRONG | H2 | 270 / 35 | NOT READABLE | -7.6 [-15.2, +1.4] | -7.63 | no | 4.31 |
| ASIA | none | C3 WEAK vs STRONG | R1 | 171 / 17 | NOT READABLE | -0.7 [-7.6, +4.9] | -0.74 | no | 3.20 |
| ASIA | none | C3 WEAK vs STRONG | R2 | 261 / 34 | NOT READABLE | -7.4 [-15.2, +2.1] | -7.42 | no | 4.44 |
| ASIA | swing | C1 WEAK vs MEDIUM | FULL | 693 / 141 | yes | +2.1 [-1.2, +5.8] | +2.12 | no | 1.81 |
| ASIA | swing | C1 WEAK vs MEDIUM | H1 | 255 / 55 | NOT READABLE | +8.5 [+4.0, +13.4] | +8.50 | yes | 2.45 |
| ASIA | swing | C1 WEAK vs MEDIUM | H2 | 438 / 86 | NOT READABLE | -2.0 [-6.4, +2.5] | -1.97 | no | 2.29 |
| ASIA | swing | C1 WEAK vs MEDIUM | R1 | 323 / 65 | NOT READABLE | +6.4 [+2.2, +10.8] | +6.40 | yes | 2.23 |
| ASIA | swing | C1 WEAK vs MEDIUM | R2 | 370 / 76 | NOT READABLE | -1.5 [-6.5, +3.7] | -1.54 | no | 2.57 |
| ASIA | swing | C2 STRONG vs MEDIUM | FULL | 12 / 141 | NOT READABLE | +12.8 [+7.3, +20.8] | +12.84 | yes | 3.48 |
| ASIA | swing | C2 STRONG vs MEDIUM | H1 | 2 / 55 | NOT READABLE | +16.6 [+13.2, +19.8] | +16.56 | yes | 1.73 |
| ASIA | swing | C2 STRONG vs MEDIUM | H2 | 10 / 86 | NOT READABLE | +9.3 [+2.9, +19.3] | +9.28 | yes | 4.24 |
| ASIA | swing | C2 STRONG vs MEDIUM | R1 | 4 / 65 | NOT READABLE | +11.0 [-0.6, +17.9] | +10.99 | no | 4.64 |
| ASIA | swing | C2 STRONG vs MEDIUM | R2 | 8 / 76 | NOT READABLE | +12.1 [+5.7, +26.5] | +12.07 | yes | 5.33 |
| ASIA | swing | C3 WEAK vs STRONG | FULL | 693 / 12 | NOT READABLE | -10.7 [-19.0, -4.2] | -10.72 | yes | 3.71 |
| ASIA | swing | C3 WEAK vs STRONG | H1 | 255 / 2 | NOT READABLE | -8.1 [-11.7, -4.2] | -8.06 | yes | 1.98 |
| ASIA | swing | C3 WEAK vs STRONG | H2 | 438 / 10 | NOT READABLE | -11.2 [-21.8, -3.8] | -11.25 | yes | 4.57 |
| ASIA | swing | C3 WEAK vs STRONG | R1 | 323 / 4 | NOT READABLE | -4.6 [-11.4, +7.3] | -4.59 | no | 4.61 |
| ASIA | swing | C3 WEAK vs STRONG | R2 | 370 / 8 | NOT READABLE | -13.6 [-28.0, -5.9] | -13.61 | yes | 5.55 |

## 8. HVN target rows, full sample, main window (context only, never a test)

| Session | Tier | n | Days | Success rate % | Gross edge pp | Net EV bps [CI] |
|---|---|---|---|---|---|---|
| NY | STRONG | 24 | 49 | 45.8 | -13.9 | -4.6 [-12.2, +1.6] |
| NY | MEDIUM | 125 | 49 | 50.4 | -1.2 | -2.7 [-6.0, +0.4] |
| NY | WEAK | 658 | 49 | 44.7 | -4.5 | -2.5 [-4.1, -1.0] |
| LONDON | STRONG | 3 | 32 | 66.7 | +6.4 | +6.0 [-21.8, +33.8] |
| LONDON | MEDIUM | 28 | 32 | 32.1 | -22.4 | -6.8 [-12.9, +0.1] |
| LONDON | WEAK | 101 | 32 | 38.6 | -10.9 | -1.2 [-4.0, +2.5] |
| ASIA | STRONG | 2 | 32 | 0.0 | -47.7 | -23.0 [-27.9, -18.2] |
| ASIA | MEDIUM | 28 | 32 | 60.7 | +3.4 | -4.0 [-15.7, +7.5] |
| ASIA | WEAK | 118 | 32 | 48.3 | -1.4 | -5.5 [-12.3, +0.9] |

## 9. Part B candidates: STABLE comparisons and the forward sample each would need

Required signals (both cells) = (n first + n second) x (z x SE / |d|)^2, SE = full-sample bootstrap SE. Forward trading days = required signals / R2 rate (signals of both cells per R2 trading day). z 1.96 = the forward estimate just reaches significance if d is the true gap (50 % power); z 2.80 = 80 % power, two-sided 5 %.

| Mode | Session | Target set by | Comparison | d bps | SE bps | n first + n second | R2 rate per trading day | Required signals z 1.96 | Forward trading days z 1.96 | Required signals z 2.80 | Forward trading days z 2.80 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| carried 24 h | NY | none | C1 WEAK vs MEDIUM | +0.51 | 0.78 | 2593 | 71.6 | 22888 | 320 | 46711 | 652 |
| main window | NY | none | C2 STRONG vs MEDIUM | +2.56 | 1.26 | 1198 | 31.1 | 1111 | 36 | 2266 | 73 |
| carried 24 h | NY | none | C2 STRONG vs MEDIUM | +2.66 | 1.29 | 1198 | 31.1 | 1086 | 35 | 2216 | 71 |
| carried 24 h | NY | none | C3 WEAK vs STRONG | -2.14 | 1.37 | 1913 | 52.2 | 2995 | 57 | 6111 | 117 |
| main window | NY | swing | C1 WEAK vs MEDIUM | +1.56 | 1.08 | 1699 | 45.5 | 3158 | 69 | 6445 | 142 |
| carried 24 h | NY | swing | C1 WEAK vs MEDIUM | +1.88 | 1.18 | 1699 | 45.5 | 2592 | 57 | 5290 | 116 |
| main window | ASIA | none | C1 WEAK vs MEDIUM | +1.17 | 1.38 | 683 | 23.1 | 3636 | 158 | 7419 | 322 |
| carried 24 h | ASIA | none | C1 WEAK vs MEDIUM | +1.49 | 1.42 | 683 | 23.1 | 2366 | 103 | 4829 | 209 |

