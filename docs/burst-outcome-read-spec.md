# Burst outcome read — pre-registered spec (written 2026-09-28 UTC)

**Owed by:** `docs/trader-tick-queue.md` §4, the AVR-1/AVR-2 line and its DATED line ("outcome read step 1"). **Ruling it serves:** `AVR-2` = (a), ruled by the trader 2026-09-26 in [`aggr-vel-burst-rederivation-read-2026-09-26.md`](aggr-vel-burst-rederivation-read-2026-09-26.md) §5: no threshold change until an outcome read exists, then re-open `AVR-2` option (c), a volatility-conditional threshold, with its result.

**Status:** step 1 done (power count and this spec). **No outcome has been computed.** The read runs on the dated trigger in `docs/burst-outcome-read-spec.md` §6.

⛔ **This is a READ spec. It changes no setting and no scoring.** It does not decide option (c). `docs/burst-outcome-read-spec.md` §7 states which results argue for (c), against (c), or leave it open.

---

## 0. Model and effort for the seat that RUNS the read

**Build (session 1): Opus, medium.**
- **Why that tier:** two small, mechanical tool changes with in-repo templates (`docs/burst-outcome-read-spec.md` §5.2). The statistics are copied from `tools/ops/medium_tier_diagnosis.py`, not designed.
- **Where it slips:** (1) the concurrent-instance rule in `tools/ops/SwingFallbackRead/SwingFallbackRead.vb` drops every row of a book whose `InstanceId` is not in the box-log set. The rotated 116-column book carries the only rows of ids `3fe57c53…` and `ee159d03…`. Add it to the box-log set, or the whole 2026-09-01 → 09-24 span vanishes silently. (2) The arm definitions must reproduce the power-count arm counts exactly (handle `H-2` below). A mismatch means the arms drifted.
- **Escalate** (stop, move to Opus high, report) if the parity handle `H-2` fails by more than the rows the export legitimately drops, or if the default mode of `SwingFallbackRead` stops reproducing its committed output.

**Run (session 2): Opus, high.**
- **Why that tier:** a pre-registered read is only as good as its refusal to adapt. The work is holding the rules below against the data.
- **Where it slips:** (1) re-choosing cells, arms or the fifth grouping after seeing outcomes; (2) reading the fifth-level cells (descriptive only) as findings; (3) treating NOT READABLE as "no effect".
- **Escalate** if any rule in `docs/burst-outcome-read-spec.md` §5 cannot be applied as written. Record the deviation before looking at the affected numbers.

---

## 1. The mechanism, from the code

`Core/ScoringEngine_Calculate_Scoring.vb` lines 336–374 (Step 2, the v52 wire-in):

- TFI votes first: `BUY PRESSURE` → long, `SELL PRESSURE` → short (`AddFull`, Microstructure category).
- The modifier runs only when `scoring_enabled`, TFI is directional, and the session carries an explicit `burst_ratio_threshold` (`ExecutionResolution.HasExplicitAggrVelBurstThreshold`).
- Burst on the TFI side → `+upgrade_bonus` (1) to that side's score, capped at `regimeMax`. Burst against the TFI side → `−contra_penalty` (1). `NORMAL` → no change.
- **So the modifier acts on the TFI side's score, not on the verdict.** A row is burst-upgraded **for its verdict side X** only when TFI is on X and the burst is on X. The arms below are defined from that.

Current values (`settings.json` v69, read at run time by the count script): thresholds NY 4.5, LONDON 5.5, ASIA 5.5; `upgrade_bonus` 1; `contra_penalty` 1.

---

## 2. Step 1 — the power count (counts only, no outcome)

**Instrument:** [`tools/ops/burst-outcome-power-count.ps1`](../tools/ops/burst-outcome-power-count.ps1), committed with this spec, read-only. It reads signal-time fields only. It never opens `analysis_eval_cache.csv` and never reads a candle. Its loader is the loader of `tools/ops/burst-watch-read.ps1` (books, order, first-wins dedup on Timestamp before any filter, weekday rule, session rule, population flag), extended by extra columns.

### 2.1 Handles

| Handle | Command (from the repo root) | Expected |
|---|---|---|
| `H-1` | `powershell -NoProfile -File tools/ops/burst-outcome-power-count.ps1 -FetchFolder aws_fetch\20260928-121255 -RunDate 2026-11-02` | The output in `docs/burst-outcome-read-spec.md` §2.2, line for line (PowerShell writes CRLF; strip `\r` before a diff). The data are frozen; nothing depends on the clock. Checked for this spec: `diff` of the run output against the pasted block printed nothing |
| `H-2` | Same command, run by the build seat after the outcome instrument exists | The new analysis script's arm counts equal the output's section 1 table (`docs/burst-outcome-read-spec.md` §2.2) per session × fifth, minus rows the export drops (which it must count and print) |

`H-1` was run for this spec at the commit that adds it. `H-2` is a forward acceptance criterion; it cannot run yet.

### 2.2 Output of `H-1` (pasted, not edited)

```
settings.json version=69  sessions: ASIA h0-7  LONDON h8-12  NY h13-23
fixed thresholds: ASIA=5.5 LONDON=5.5 NY=4.5  upgrade_bonus=1 contra_penalty=1 scoring_enabled=True
band pct (current settings, used for CROSSED only): strong=0.7 med=0.53 weak=0.35
BOOK analysis_log.csv.v0.7.bak weekday session rows kept=25951  directional valid armed rows=4972
BOOK analysis_log.csv.116col-83564b1b.20260924_184606.bak weekday session rows kept=14508  directional valid armed rows=3796
BOOK analysis_log.csv weekday session rows kept=1474  directional valid armed rows=323
weekday session rows=41933  population (ratio non-empty, ATR parses)=41725
excluded from population: NO TRADE=26332  NO TRADE lean forms=5973  ASIA before arming=329  invalid placed levels=0
directional valid armed rows (the count population)=9091  first=2026-07-22 16:31:01  last=2026-09-28 12:12:01

=== 1. Counts, all armed eras pooled: session x ATR fifth x arm (bands pooled; S/M/W split for A and B) ===
session fifth    A (S/M/W)        B (S/M/W)          C    O  Oup | A_keep A_drop S_add B_lean | crossed bandMismatch
ASIA    1    13 (1/4/8)       59 (1/12/46)          0   23    3 |     10      3     0      0 |       6     0
ASIA    2    18 (0/7/11)      139 (5/31/103)        0   99    7 |     18      0     5      0 |       6     0
ASIA    3    18 (2/7/9)       220 (12/73/135)       1  156    4 |     18      0    13      1 |       6     0
ASIA    4    18 (5/6/7)       234 (14/76/144)       1  230    4 |     18      0    19      0 |       6     0
ASIA    5    12 (3/4/5)       291 (28/104/159)      0  321    5 |     12      0    21      0 |       5     0
ASIA    all  79 (11/28/40)    943 (60/296/587)      2  829   23 |     76      3    58      1 |      29     0
LONDON  1    19 (2/7/10)      95 (7/30/58)          0   58    3 |     14      5     0      0 |       8     0
LONDON  2    32 (6/18/8)      200 (31/66/103)       0  165   11 |     31      1     0      1 |      16     0
LONDON  3    9 (0/5/4)        169 (13/58/98)        1  150    6 |      9      0    11      0 |       1     0
LONDON  4    14 (3/5/6)       166 (20/61/85)        0  180    4 |     14      0    14      1 |       3     0
LONDON  5    3 (0/2/1)        215 (28/85/102)       1  209    2 |      3      0    20      0 |       0     0
LONDON  all  77 (11/37/29)    845 (99/300/446)      2  762   26 |     71      6    45      2 |      28     0
NY      1    6 (0/1/5)        12 (0/4/8)            0   16    1 |      2      4     0      0 |       1     0
NY      2    35 (2/14/19)     211 (12/61/138)       0  149   14 |     35      0     1      0 |       9     0
NY      3    67 (13/28/26)    624 (50/193/381)      1  528   19 |     67      0    35      0 |      23     0
NY      4    41 (3/21/17)     840 (67/302/471)      0  737   20 |     41      0    64      0 |      15     0
NY      5    20 (3/4/13)      1175 (136/420/619)    0 1090   15 |     20      0   114      1 |       2     0
NY      all  169 (21/68/80)   2862 (265/980/1617)    1 2520   69 |    165      4   214      1 |      50     0

=== 2. Era split: arm A / arm B per session x ATR fifth x era ===
   E1 armed->v66 2026-08-10 18:36:01 | E2 ->2026-08-20 | E3 ->POC fix 2026-09-24 18:46:06 | E4 POC fix->
session fifth  E1 A/B      E2 A/B      E3 A/B      E4 A/B      | S_add E1/E2/E3/E4
ASIA    1     5/26        7/29        1/4         0/0         | 0/0/0/0
ASIA    2     10/50       4/24        4/61        0/4         | 1/1/3/0
ASIA    3     2/21        0/1         15/190      1/8         | 0/0/13/0
ASIA    4     2/4         0/0         15/195      1/35        | 0/0/17/2
ASIA    5     0/1         0/0         12/267      0/23        | 0/0/20/1
ASIA    all   19/102      11/54       47/717      2/70        | 1/1/53/3
LONDON  1     14/72       4/23        1/0         0/0         | 0/0/0/0
LONDON  2     23/134      5/21        4/44        0/1         | 0/0/0/0
LONDON  3     1/29        0/4         8/126       0/10        | 5/0/6/0
LONDON  4     1/3         0/0         13/148      0/15        | 0/0/13/1
LONDON  5     1/5         0/0         2/199       0/11        | 0/0/20/0
LONDON  all   40/243      9/48        28/517      0/37        | 5/0/39/1
NY      1     3/8         3/4         0/0         0/0         | 0/0/0/0
NY      2     19/107      5/35        10/67       1/2         | 1/0/0/0
NY      3     23/189      15/102      29/323      0/10        | 10/7/18/0
NY      4     9/182       5/50        27/580      0/28        | 12/3/46/3
NY      5     2/82        1/94        16/979      1/20        | 13/12/88/1
NY      all   56/568      29/285      82/1949     2/60        | 36/22/152/4

=== 3. Accrual from 2026-08-20 (weekday days with >= 1 directional valid row in the session) ===
ASIA: weekdays=27  directional rows/day=57.4
  fifth   A/day  B/day  S_add/day  A_keep/day  A_drop/day
  1       0.04   0.15      0.00       0.00       0.04
  2       0.15   2.41      0.11       0.15       0.00
  3       0.59   7.33      0.48       0.59       0.00
  4       0.59   8.52      0.70       0.59       0.00
  5       0.44  10.74      0.78       0.44       0.00
LONDON: weekdays=26  directional rows/day=43.2
  fifth   A/day  B/day  S_add/day  A_keep/day  A_drop/day
  1       0.04   0.00      0.00       0.04       0.00
  2       0.15   1.73      0.00       0.15       0.00
  3       0.31   5.23      0.23       0.31       0.00
  4       0.50   6.27      0.54       0.50       0.00
  5       0.08   8.08      0.77       0.08       0.00
NY: weekdays=26  directional rows/day=144.6
  fifth   A/day  B/day  S_add/day  A_keep/day  A_drop/day
  1       0.00   0.00      0.00       0.00       0.00
  2       0.42   2.65      0.00       0.42       0.00
  3       1.12  12.81      0.69       1.12       0.00
  4       1.04  23.38      1.88       1.04       0.00
  5       0.65  38.42      3.42       0.65       0.00

=== 4. Power: published sigma, projection to the run date, MDE ===
ASIA published none  n=  734 CI width=4.1 -> SE=1.046 sigma_eff=28.3 bps
ASIA published swing n=  846 CI width=4.6 -> SE=1.173 sigma_eff=34.1 bps
ASIA sigma used (max) = 34.1 bps
LONDON published none  n=  891 CI width=4.5 -> SE=1.148 sigma_eff=34.3 bps
LONDON published swing n=  642 CI width=4.3 -> SE=1.097 sigma_eff=27.8 bps
LONDON sigma used (max) = 34.3 bps
NY published none  n= 2852 CI width=2.4 -> SE=0.612 sigma_eff=32.7 bps
NY published swing n= 1758 CI width=2.9 -> SE=0.740 sigma_eff=31.0 bps
NY sigma used (max) = 32.7 bps
multiplier: cell (alpha 0.05 two-sided, power 0.80) = 2.8016; session primary (alpha 0.0167 two-sided) = 3.2356
data end 2026-09-28 12:12:01; run date 2026-11-02; weekdays ahead (after the data-end day) = 25
reference median ATR (bps of price), directional valid rows before 2026-09-12: ASIA 9.0  LONDON 8.2  NY 7.3

cell       nA_now nB_now  A/day  nA_run nB_run  date nA>=100   MDE_run bps  ATR scale  MDE_run scaled | S_add_now S_add_run date S_add>=100
ASIA   f1         13     59   0.04      14     63  2035-09-28        28.3       0.49           13.8 |         0         0 never
ASIA   f2         18    139   0.15      22    199  2028-11-10        21.6       0.70           15.1 |         5         8 2030-01-07
ASIA   f3         18    220   0.59      33    403  2027-04-09        17.4       0.85           14.7 |        13        25 2027-06-08
ASIA   f4         18    234   0.59      33    447  2027-04-09        17.3       1.04           18.1 |        19        37 2027-03-09
ASIA   f5         12    291   0.44      23    560  2027-07-01        20.3       1.41           28.7 |        21        40 2027-02-17
ASIA   low12      31    198   0.19      36    262  2028-03-02        17.1       0.64           10.9 |         5         8 2030-01-07
ASIA   mid3       18    220   0.59      33    403  2027-04-09        17.4       0.85           14.7 |        13        25 2027-06-08
ASIA   high45     30    525   1.04      56  1,006  2026-12-31        13.1       1.21           15.8 |        40        77 2026-11-24
ASIA   all        79    943   1.81     124  1,672  2026-10-14        10.3       0.97            9.9 |        58       110 2026-10-27
LONDON f1         19     95   0.04      20     95  2034-10-24        23.6       0.53           12.4 |         0         0 never
LONDON f2         32    200   0.15      36    243  2028-06-07        17.2       0.72           12.4 |         0         0 never
LONDON f3          9    169   0.31      17    300  2027-11-16        24.1       0.97           23.5 |        11        17 2028-03-21
LONDON f4         14    166   0.50      27    323  2027-05-26        19.4       1.21           23.4 |        14        27 2027-05-10
LONDON f5          3    215   0.08       5    417  2031-07-29        43.5       1.83           79.8 |        20        39 2027-02-19
LONDON low12      51    295   0.19      56    338  2027-09-20        13.9       0.67            9.3 |         0         0 never
LONDON mid3        9    169   0.31      17    300  2027-11-16        24.1       0.97           23.5 |        11        17 2028-03-21
LONDON high45     17    381   0.58      31    740  2027-04-16        17.5       1.45           25.3 |        34        67 2026-12-08
LONDON all        77    845   1.08     104  1,378  2026-10-28        11.3       1.00           11.3 |        45        83 2026-11-17
NY     f1          6     12   0.00       6     12  never             45.8       0.38           17.4 |         0         0 never
NY     f2         35    211   0.42      46    277  2027-04-30        14.6       0.54            7.8 |         1         1 never
NY     f3         67    624   1.12      95    944  2026-11-09         9.9       0.70            7.0 |        35        52 2027-02-05
NY     f4         41    840   1.04      67  1,425  2026-12-16        11.5       0.93           10.7 |        64       111 2026-10-26
NY     f5         20   1175   0.65      36  2,136  2027-03-18        15.3       1.50           22.9 |       114       200 met
NY     low12      41    223   0.42      52    289  2027-04-12        13.8       0.53            7.3 |         1         1 never
NY     mid3       67    624   1.12      95    944  2026-11-09         9.9       0.70            7.0 |        35        52 2027-02-05
NY     high45     61   2015   1.69     103  3,560  2026-10-30         9.1       1.22           11.1 |       178       311 met
NY     all       169   2862   3.23     250  4,794  met                6.9       0.99            6.8 |       214       364 met

secondary H2 (option (c) upgraded set in fifths 4-5): (A + S_add) vs (B - S_add), projected to the run date, alpha 0.0167 two-sided
session  n1_run n2_run  MDE_run bps  MDE_run scaled
ASIA        133    929        10.2            12.4
LONDON       98    673        12.0            17.3
NY          414  3,250         5.5             6.7
```

Reading the output: "secondary H2" in its last table is `BO-H2` of `docs/burst-outcome-read-spec.md` §4. "date nA>=100" projects at the post-2026-08-20 weekday rate from the data end. Run time about 2 minutes.

### 2.3 What the count says

| Reading | Value | Consequence for the design |
|---|---|---|
| Count population | 9,091 directional valid armed weekday rows, 2026-07-22 → 2026-09-28 12:12 UTC | — |
| Arm A (burst-upgraded) per session, all eras | ASIA 79 · LONDON 77 · NY 169 | Only NY is readable (n ≥ 100) today |
| Arm A per ATR fifth | 3 to 67 per cell; **no cell reaches 100 today** | Only NY fifths 3 and 4 reach 100 before 2027. Fifth-level cells are **descriptive only** |
| Arm A accrual since 2026-08-20 | ASIA 1.81 · LONDON 1.08 · NY 3.23 per weekday | Session-pooled A reaches 100: ASIA 2026-10-14, LONDON 2026-10-28, NY already |
| Arm A in fifths 1–2 since 2026-08-20 | 0.19 per weekday in ASIA and LONDON, 0.42 in NY; NY fifth 1 is 0.00 | Low-ATR data is almost all pre-2026-08-20. Low-ATR cells will not grow |
| Shadow arm `A_drop` (option (c) would remove these upgrades) | 13 rows in total | (c) mostly **adds** upgrades in the current regime. The removal side cannot be read |
| Shadow arm `S_add` (option (c) would add these upgrades) | ASIA 58 · LONDON 45 · NY 214; 5.3/weekday in NY fifths 4–5 | `S_add` accrues faster than A in the high fifths. It carries the (c) question |
| `B_lean` (ratio over the fixed threshold, lean floor rejected) | 4 rows in all sessions | The unlogged lean floor rarely binds at the fixed threshold. `S_add` is a superset only by a small margin **at that threshold**; at the lower shadow thresholds this is not measured |
| Contra arm C | 5 rows in all sessions | Contra is dead on directional rows in every session, not only res-3. No contra hypothesis |
| `crossed` (the +1 moved the row up a band, or into directional) | ASIA 29 · LONDON 28 · NY 50 of the A rows | About one A row in three exists at its band only because of the burst. Too few for a label; descriptive |
| `bandMismatch` | 0 | The count's band rule reproduces the logged band on every A row |
| POC-fix era E4 | 2 · 0 · 2 A rows so far | Post-edge rows are ~0 today and ~30 % of A by 2026-11-02 |

---

## 3. Power

- **Variance source, published only.** `docs/swing-vs-fallback-target-read-2026-09-15.md` §4.1 (tier ALL, main window, maker/maker) prints n and a 95 % CI of net EV per trade. Its CIs are cluster-robust by session-day. σ_eff = CI width ÷ (2 × 1.96) × √n. The count script derives it from the quoted literals and uses the larger of the `none` and `swing` rows: **ASIA 34.1, LONDON 34.3, NY 32.7 bps.**
- ⚠ **These σ values are conservative for arm A.** They carry the design effect of dense rows (NY about 110 per trading day in that read: 5,417 NY rows over 49 trading days, summing the `none`, `swing` and `hvn` ALL rows of `docs/swing-vs-fallback-target-read-2026-09-15.md` §4.1 against the 49 days of `docs/tier-order-stability-read-2026-09-15.md` §3). Arm A is sparse (1–3 per weekday), so its own clustering is weaker. An optimistic bound, hand-computed from published values: σ ≈ √(p(1−p)) × (target + stop). With the same §4.1 success rates and median target/stop multiples, and the count script's reference median ATR (ASIA 9.0, LONDON 8.2, NY 7.3 bps of price): **ASIA ≈ 12.8, LONDON ≈ 14.7, NY ≈ 12.1 bps.** It ignores timeouts and the spread of distances. It is a bound, not a measurement. ⚠ The ATR medians are signal-time values from this count, not from a published read.
- **MDE** = (z₁₋α/₂ + z₀.₈) × σ × √(1/n_A + 1/n_B). Cells: α 0.05 two-sided (2.80). Session primary: Holm worst case α 0.05/3 (3.24).
- **"MDE_run scaled"** multiplies by the cell's median ATR (bps) over the session reference median. Targets and stops are ATR multiples, so σ should scale with ATR. This is an assumption, not a measurement.

**MDE at the run-1 date (2026-11-02), from `H-1`:**

| Test | ASIA | LONDON | NY |
|---|---:|---:|---:|
| `BO-H1` session-pooled A vs B: n_A · MDE (σ conservative) | 124 · 10.3 bps | 104 · 11.3 bps | 250 · 6.9 bps |
| same, σ optimistic (× σ_opt ÷ σ_cons) | ≈ 3.9 bps | ≈ 4.8 bps | ≈ 2.5 bps |
| `BO-H2` fifths 4–5, (A + `S_add`) vs (B − `S_add`): n₁ · MDE (σ conservative, ATR-scaled) | 133 · 12.4 bps | 98 · 17.3 bps | 414 · 6.7 bps |

**Plain reading.** The engine's largest published tier gap is +2.6 bps (NY STRONG vs MEDIUM, `docs/tier-order-stability-read-2026-09-15.md` §1). A burst effect of that size is below the MDE in ASIA and LONDON even on the optimistic σ, and near it in NY. **The read can detect a large effect and bound a small one. It will most likely not detect a plausible one.** That is the expected outcome, stated before the run.

---

## 4. Hypotheses

`BO-H1` to `BO-H3` and `BO-D1` are new IDs for this spec ("burst outcome"), checked free in `docs/`, `Core/`, `verify/` and `tools/` on 2026-09-28.

| ID (this spec) | Statement | Role |
|---|---|---|
| `BO-H1` | Per session: net EV per trade of arm A minus arm B, B re-weighted to A's stratum mix, is not zero | **Primary.** Three tests, Holm, familywise α 0.05 |
| `BO-H2` | Per session, ATR fifths 4–5 only: net EV per trade of the option-(c) upgraded set (A ∪ `S_add`) minus the rest of the TFI-on-side rows (B ∖ `S_add`), re-weighted, is not zero | **Secondary, the (c) test.** Holm across the sessions that are readable |
| `BO-H3` | Descriptive: the same A − B difference in the fifth groups low (1–2), mid (3), high (4–5); `S_add` vs B ∖ `S_add` alone; `crossed` A rows vs uncrossed A rows | Descriptive. CIs printed, no significance claim |

All tests are two-sided. "Not zero" in either sign is a result.

---

## 5. Population, arms, outcome

| Item | Rule |
|---|---|
| Rows | The outcome instrument's population (`SwingFallbackRead`): directional verdicts STRONG / MEDIUM / WEAK (bare `LONG`/`SHORT` = MEDIUM), valid placed levels, trading week (= UTC weekday for these sessions), the three fetch books of the run date, first-wins dedup on Timestamp |
| Exclusions | Every NO TRADE form, including lean forms `NO TRADE [WEAK LONG]` (the `analysis/BandLadder.vb` contract). `AggrVelBurstRatio` empty (warm-up or REST fallback: the burst could not fire). ATR not parseable. ASIA rows before 2026-08-01 19:02:31 UTC (ASIA armed by v65 at that instance; before it the logged burst used threshold 2.5 and did not score) |
| Arms (verdict side X) | **A:** TFI on X and `AggrVelSignal` on X. **B:** TFI on X and `NORMAL`. **C:** TFI on X, burst on the other side (not tested; 5 rows). **O:** TFI not on X (not tested) |
| Shadow arms | Shadow threshold = `AggrVelBurstRatio` p90 of the row's session × ATR fifth (the frozen values in the count script, from `tools/ops/aggr-vel-regime-read.ps1` on `aws_fetch\20260925-085341`). **`S_add`:** a B row with ratio ≥ its shadow threshold and `AggrVelNet` sign on X. **`A_drop`:** an A row with ratio below it |
| ATR fifth | The RULED edges of `tools/ops/burst-watch-read.ps1` (`AVR-1`), never re-binned |
| Strata for re-weighting | ATR fifth × band (STRONG / MEDIUM / WEAK) × POC era (before / from 2026-09-24 18:46:06) × daylight-saving seam era (before / from the session's seam, §6.2; added 2026-09-29 pre-outcome, ruling `AT-2`). B is re-weighted to A's stratum mix over strata where both have rows; strata without B rows are dropped and counted (the `docs/medium-tier-diagnosis-read-2026-09-17.md` D-7 method, `rw_run` in `tools/ops/medium_tier_diagnosis.py`) |
| Outcome | **Net EV per trade, bps**, `docs/DeribitIndicatorProject.md` §5a: +target − fee on success · −stop − fee on a stop hit or same-bar ambiguity · mark − fee on a timeout. Placed levels (`Price`, `PlacedTarget*`, `PlacedStop*`). Fee = `scoring.trade_costs` maker/maker round trip (3 bps at v69). Window: NY 15 min, LONDON and ASIA 45 min (`AnalysisConstants.HoldWindowsForResolution`, max window). Success rate, breakevens and edges are printed to explain it; **net EV per trade decides** |
| CI | 95 % percentile bootstrap of whole UTC trading days, fixed seed, 10,000 resamples (the house rule). Holm-adjusted levels for `BO-H1` and `BO-H2` |
| Readable | n ≥ 100 in every group a statistic uses, else NOT READABLE (the census rule). **Clarified 2026-09-29 before any outcome was seen (orchestrator, tools review):** for a re-weighted A − B test, "uses" means rows in **covered strata** (strata holding both A and B rows); A rows in a stratum with no B row are dropped by the statistic and do not count toward readability. `tools/ops/burst_outcome_read.py` `rw_run` implements this (marked deviation from the verbatim copy) |
| Label | The census rule of `tools/ops/medium_tier_diagnosis.py`, first match wins: NOT READABLE → CONFIRMED (both chronological halves readable, both CIs exclude 0, same sign, and the full-sample Holm-adjusted CI excludes 0) → H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT → H1 FINDING, H2 CONTRADICTS OR NOT READABLE → DISCOVERY ONLY (H2) → NO DIFFERENCE SHOWN. Halves: first ⌊D/2⌋ UTC trading days. **Added 2026-09-29 before any outcome was seen (`TB-D5` = (b), orchestrator):** when both halves are significant with the same sign but the full-sample Holm-adjusted CI includes 0, the label is **"NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0 / d < 0)"**, placed right after CONFIRMED. Without it, first-match-wins falls through to "H2 SAME SIGN NOT SIGNIFICANT", which would state something false. Holm mechanics as built: `TB-D4` in [`burst-outcome-read-tools-spec-back.md`](burst-outcome-read-tools-spec-back.md) §2 |

### 5.1 Cells and fallback

- **Fifth-level cells are descriptive.** Only NY fifth 3 (projected 2026-11-09) and NY fifth 4 (2026-12-16) reach n_A ≥ 100 before 2027; 12 of 15 cells need until 2027-04 or later (output section 4 in `docs/burst-outcome-read-spec.md` §2.2). Fifth cells are printed with CIs and never labelled, so the label count stays fixed across sessions.
- **Fallback groups, fixed now:** low = fifths 1–2, mid = fifth 3, high = fifths 4–5. `BO-H2` uses high only. `BO-H3` prints all three.
- **Pooled fallback:** `BO-H1` is session-pooled (all fifths, stratified). There is no cross-session pool for a label; the three sessions run at two resolutions.

### 5.2 Instrument for the outcome — what exists and what must be built

| Piece | Status |
|---|---|
| Candle walk over placed levels, main window, net EV per trade | **Exists:** `SwingFallbackRead --mode diagexport` (`tools/ops/SwingFallbackRead/MediumTierDiagnosisExport.vb`). It writes `MainNetEv`, `Tier`, `Side`, `Atr`, `EffectiveLongScore`, `AggrVelSignal` per population row |
| Reading the rotated `analysis_log.csv.*col-*.bak` books | ⛔ **Must be built.** The loader reads `--pooled` plus the fetch's `analysis_log.csv.v0.7.bak` and `analysis_log.csv` only. Change: glob every `analysis_log.csv.*col-*.bak` in the fetch folder into the box-log set (`bak.Concat(live)` → all three) and into the merge. **Parity:** default mode on `aws_fetch/20260924-084613` (no rotated book) reproduces its committed output byte for byte apart from the run timestamp |
| `TFISignal`, `AggrVelBurstRatio`, `AggrVelNet` per row | Not in the export. The analysis script joins them from the three books by Timestamp (the `tools/ops/q1d_tier_geometry.py` `--logs` pattern). No VB change |
| Arms, strata, bootstrap, labels | ⛔ **Must be built:** `tools/ops/burst_outcome_read.py`, Python 3 standard library. Copy `boot`, `compare`, `label`, `rw_run` from `tools/ops/medium_tier_diagnosis.py`; do not re-derive them. It must print arm counts before any outcome column is read and pass `H-2` |
| Pre-registration order | The script is **committed before its first run on outcome columns.** The run commit adds only the output and the read doc |

---

## 6. Run date and eras

### 6.1 Run date — dated trigger

> ⛔ **RE-PLANNED 2026-09-28 (UTC), before any outcome was seen: the trader is away 2026-10-14 → 2026-11-25.** The dates below are superseded by this box. The original lines stay for the record.
>
> | Step | New date (UTC) | Model + effort |
> |---|---|---|
> | Build the read tools (§0, §5) | **Before 2026-10-14**, ideally the week of 2026-10-05 | Opus 5.5, medium |
> | **Run 1** | **First seat after 2026-11-25, by 2026-12-04.** Data cut **fixed now at 2026-11-25 00:00:00 UTC**: run 1 reads rows before it, whenever it runs. The ≥ 100 arm-A gate is checked on those rows | Opus 5.5, high |
> | Decide from run 1 (§7), then write the (c) spec if §7 argues for it | Right after run 1 | Trader + orchestrator |
> | **Run 2** (replication) | **On or after 2027-01-27**: rows from 2026-11-25 00:00:00 UTC onward, about 9 weekday-weeks, the span the original plan gave run 2 | Opus 5.5, high |
> | Build and ship (c) | Only after run 2 replicates | Build Opus 5.5, high; deploy by the trader |
>
> Why the fixed cut: the run date now depends on when a seat opens after the holiday. Fixing the cut keeps the two runs' populations pre-registered and disjoint, whatever the run date turns out to be.

- **Run 1: on or after Monday 2026-11-02 (UTC).** Gate: `H-1` re-run on the fetch of that day shows session-pooled arm A ≥ 100 in all three sessions. Projection: ASIA 2026-10-14, LONDON 2026-10-28, NY already met. If the gate fails, re-check weekly. **Latest 2026-11-30:** run anyway and label the short session NOT READABLE.
- **Run 2 — forward replication: on or after Monday 2027-01-04.** Rows strictly after run 1's data end, same code, no change. A run-1 finding counts as replicated when run 2 gives the same sign. Run 2 is also the first read where ASIA and LONDON `S_add` in fifths 4–5 pass 100 (projected 2026-11-24 and 2026-12-08, cumulative).
- ⚠ **If option (c) ships before run 2,** run 2 becomes the before-and-after read: rows after the (c) edge are a new population, read separately with the same arms re-defined against the new threshold. That is reason 3 of the `AVR-2` ruling.

### 6.2 Eras

| Edge | Time (UTC) | Handling |
|---|---|---|
| Armed | NY and LONDON armed on every row of these books (collector from 2026-07-22 16:24; v60 LONDON commit 2026-07-22 17:35; first LONDON row 2026-07-23). ASIA from 2026-08-01 19:02:31 | ASIA rows before it excluded |
| v66 OBV scoring edge | 2026-08-10 18:36:01 (first v66 row) | Pooled. Per-era A − B printed descriptively (E1 vs later) |
| ATR regime step | 2026-08-20 00:00 | Not a scoring edge. Absorbed by the ATR-fifth strata |
| **POC-gate fix** | **2026-09-24 18:46:06** (first row of id `25951567…`) | **Pooled, with the POC era as a stratum, plus a mandatory pre-edge-only sensitivity run.** If the sensitivity flips the sign of a labelled result, the label gains "EDGE-SENSITIVE" |
| **Daylight-saving seam** (added 2026-09-29, before any outcome was seen; trader ruling `AT-2` (a), [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §4) | Per session: **ASIA and LONDON 2026-10-25 01:00:00** (Europe leaves summer time) · **NY 2026-11-01 06:00:00** (the US leaves summer time) | **Pooled, with a seam era (before / from the session's seam) as a fourth stratum dimension, plus a mandatory pre-seam-only sensitivity run.** If it flips the sign of a labelled result, the label gains "SEAM-SENSITIVE". Why: session buckets are fixed UTC (`session_volume`: ASIA 0–7, LONDON 8–12, NY 13–23; VWAP second anchor 13:30). The London open moves 07:00 → 08:00 UTC, which changes what ASIA's hour 7 and LONDON's first hour hold; the US cash open moves 13:30 → 14:30 UTC, an hour after the fixed VWAP anchor. Run 1's data cut (2026-11-25) spans both seams; run 2 is wholly post-seam. ⛔ **Must be built before run 1:** `tools/ops/burst_outcome_read.py` — a `SEAM` table beside `POC_EDGE`, the seam era in `strat_key`, the pre-seam sensitivity run beside the pre-edge one, and the seam era counts in the era printout |

**Why pooled plus a stratum (auto-proceeded, see `docs/burst-outcome-read-spec.md` §8):** the fix has two measured effects, both small (`DeribitIndicatorProject.md` §15, row dated 2026-09-24): it moves the placed target on **3.33 %** of rows (1,288 of 38,665 verified rows), and it turns **1.68 %** of directional population rows into NO TRADE through the Step 5c minimum-move gate (143 of 8,508). Both act through the VPFR POC tier. *(Corrected by the orchestrator review, 2026-09-28: this line first quoted 1.68 % as the placed-target share; the two figures measure different things.)* It does not read the burst state. So it shifts both arms alike and cancels in A − B inside a stratum. Dropping post-edge rows would discard about 30 % of arm A at run 1. The sensitivity run shows whether that assumption fails. Run 2 is wholly post-edge, so it is the post-edge replication too.

---

## 7. What the result feeds — re-opening `AVR-2` option (c)

When run 1 is done, `AVR-2` option (c) re-opens with this result. **This spec does not decide (c).**

| Result | Reading for (c) |
|---|---|
| `BO-H2` CONFIRMED > 0 in at least one readable session, and no session CONFIRMED < 0 on `BO-H1` or `BO-H2` | **Argues for (c).** Upgrading more high-ATR rows adds net EV per trade. The `BO-H3` groups show which fifths to shape it to |
| `BO-H2` CONFIRMED < 0 in any readable session | **Argues against (c).** (c) would add upgrades exactly where they lose |
| `BO-H1` CONFIRMED < 0 in any session | **Argues against (c), and raises a different question:** the modifier loses at the fixed threshold, so the next question is retiring it, not re-thresholding it. That question needs its own spec |
| Everything else, including NOT READABLE and NO DIFFERENCE SHOWN | **Undecided on outcome evidence.** Record the CIs: they bound the effect. A (c) decision then rests on the engagement argument alone, and the read says so. `BO-H3` descriptives may inform the shape but not the decision |

Run 2 upgrades or downgrades the run-1 reading by the replication rule in `docs/burst-outcome-read-spec.md` §6.1.

---

## 8. Decisions I took (auto-proceed, one line each)

| Decision | Options | Pick | Why |
|---|---|---|---|
| Directional population | (a) STRONG + MEDIUM (tradeable) · (b) STRONG + MEDIUM + WEAK · (c) also NO TRADE lean forms | **(b)**, S+M as a descriptive split | The modifier acts on the score in every band, and (b) is the outcome instrument's own population. (a) holds A = 39 / 48 / 89 rows: NOT READABLE everywhere. (c) is excluded by the `analysis/BandLadder.vb` contract |
| Cells | (a) fifths · (b) fifth groups low/mid/high · (c) session pooled only | **(b) for labels, (a) descriptive** | Step 3 of the three-step test: (a) is mechanically unreadable in 13 of 15 cells before 2027. (a) is still printed with CIs, so no information is dropped |
| Shadow arm `S_add` | (a) omit · (b) add | **(b)** | Records more. It is the only arm that answers the (c) question directly. Its lean-floor superset is named |
| Variance for power | (a) cluster-robust σ from published CIs · (b) binary-outcome σ bound | **Both; (a) is the headline** | (a) is published and conservative. (b) shows how optimistic the MDE could be. Neither uses new outcome data |
| POC-gate edge | (a) pre-edge primary, post-edge replication · (b) pool with era stratum + pre-edge sensitivity · (c) pool silently | **(b)** | Records more than (a) and is more truthful than (c). Reason in `docs/burst-outcome-read-spec.md` §6.2 |
| Multiplicity | (a) none · (b) Holm per family · (c) Bonferroni over all | **(b)** | Standard, and less conservative than (c) at equal guarantee |
| Validation | (a) chronological halves · (b) forward run | **Both** | Halves are the house label rule. The forward run is the only truly unseen data |
| Minimum n | 100 per group | The census rule | Same readability rule as the three prior outcome reads; keeps labels comparable |
| Run date | (a) 2026-11-02 trigger · (b) wait for fifth-level n (2027+) | **(a), plus run 2** | Waiting gives up nothing that run 2 does not recover. Fifth cells stay descriptive either way |

## 9. Decisions for the trader

| ID (this spec) | Question | Options | My read |
|---|---|---|---|
| `BO-D1` | Which run re-opens `AVR-2` option (c)? | (a) Run 1 (2026-11-02 trigger) · (b) Run 2 (2027-01-04, after replication) | **(a).** Run 1 is what `AVR-2` ruled on. Run 2 can still veto: a run-1 finding that does not replicate is downgraded before any (c) spec ships. ⚠ (a) is the faster option; (b) waits for more information. I pick (a) because (c) needs its own spec and review before it ships, and run 2 lands inside that time. Not blocking: the spec is written to serve either. ✅ **RULED 2026-09-28 (trader): (a) run 1.** Sequence of record: build (by ~2026-10-26) → run 1 (on or after 2026-11-02) → if §7 argues for (c), write the (c) spec while run 2 accrues → run 2 (on or after 2027-01-04) must replicate before the (c) build ships |

No choice here touches `settings.json`, scoring, a rendered value or a CSV schema.

---

## 10. Pre-registration record — what the author saw

- **No burst-arm outcome was computed or read.** The count script reads no outcome column.
- While finding the variance source, I read `docs/swing-vs-fallback-target-read-2026-09-15.md` §4.1 and `docs/tier-order-stability-read-2026-09-15.md` §1 and §5. These are tier outcomes, not burst outcomes.
- A grep of `docs/medium-tier-diagnosis-read-2026-09-17.md` for "burst" printed two lines: its D-4 table puts burst tier crossings at "< 10" rows, and an NY era segment "v52 NY burst modifier armed → v66" carries a difference of −1.6 [−3.3, +0.5] bps. **That segment is an era of a tier comparison, not a burst-arm comparison.** I did not read further.
- The only earlier outcome-linked burst number is the W6-4 AUC 0.5179 (n = 217), quoted in the `AVR-2` ruling.

## 11. What I did not verify

- **σ for arm A itself.** No published read has it. Both σ values in `docs/burst-outcome-read-spec.md` §3 are proxies.
- **That σ scales with ATR.** Assumed from the ATR-multiple geometry.
- **The lean floor at the shadow thresholds.** `lean` is not logged; `B_lean` measures it only at the fixed threshold.
- **The `crossed` count ignores the `regimeMax` cap** and uses the current band percentages for every era. It is descriptive.
- **Accrual rates assume the post-2026-08-20 regime continues.** A volatility change moves arm A's rate by fifth (that is the finding of the re-derivation read).
- **Settings eras v67 (thin-trade skip gate) and v68/v69** were read from the `change_log` first lines as not touching scoring of written rows. Not traced in code.
- **The `SwingFallbackRead` concurrent-instance trap** is read from the code (`SwingFallbackRead.vb` lines 255–288), not demonstrated by a run.
- **The POC-fix share.** ~~I quote 1.68 % from the deploy ledger; the brief quoted 3.33 %.~~ Resolved in review: both are right, for different effects (3.33 % placed target moved; 1.68 % flipped to NO TRADE). Neither re-measured.
