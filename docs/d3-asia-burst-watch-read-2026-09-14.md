# D3 / v65 ASIA aggressor-velocity arming watch — SECOND READ (2026-09-14 UTC)

**From:** a scoped seat on [`asia-burst-watch-read-brief-2026-09-14.md`](asia-burst-watch-read-brief-2026-09-14.md). **Template:** [`d3-asia-burst-watch-read-2026-08-10.md`](d3-asia-burst-watch-read-2026-08-10.md) (the first read). **Instrument:** [`tools/ops/asia-burst-watch-read.ps1`](../tools/ops/asia-burst-watch-read.ps1). **Effort:** Opus, started at medium, escalated to high. The trigger was the brief's own escalation row in `asia-burst-watch-read-brief-2026-09-14.md` §0: the fire rate landed outside 8–14 %.

---

## 0. Verdict — ⛔ MISS on the fire rate. Same-side PASSES. T-5 fires: a re-derivation READ is owed

| Slice | Weekday session-days | Pop rows | Fire rate | Same-side | Band 8–14 % | Verdict |
|---|---:|---:|---:|---:|---|---|
| **(b) after the first read's window — 2026-08-11 … 09-11. THE VERDICT SLICE** | **23** | 3,653 | **7.75 %** | **89.75 %** | ⛔ below the floor | **MISS** |
| (a) all post-v65 — 2026-08-03 … 09-11 | 28 | 4,445 | 8.50 % | 89.68 % | inside, 0.5 pp above the floor | PASS |

- **Verdict rests on slice (b).** Slice (a) re-counts the five weekdays the first read already used. It passes only by borrowing them. Decision logged in section 7 below.
- **Tolerance used:** the rulings T-1 to T-5 in `d3-asia-burst-watch-read-2026-08-10.md` §10 (reference 11.0 %, band 8–14 %, ≥ 10 weekday session-days, same-side ≥ 85 %).
- **What the miss triggers (T-5):** a **re-derivation READ**. **No threshold change.** `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` stays 5.5. Any `settings.json` move is reserved to the trader.

⭐⭐ **The pooled number hides the real finding: the rate is BIMODAL, and it tracks ATR.** Neither regime sits near 11 %.

| Sub-slice of (b) | Days | Fire rate | Daily sd | Mean daily median ATR | Same-side |
|---|---:|---:|---:|---:|---:|
| 2026-08-11 … 08-19 | 6 | **15.97 %** | 1.07 pp | 26.7 | 88.82 % |
| 2026-08-20 … 09-11 | 17 | **4.85 %** | 2.26 pp | 74.4 | 90.84 % |

- Across all 23 days, Pearson r(daily median ATR, daily fire rate) = **−0.72**. Across the 28 days of slice (a) it is −0.73.
- Inside each sub-slice the correlation is weak (−0.02 and −0.19). The data reads as **a step between two regimes, not a smooth dose-response**. That is an inference from 23 points, not a test.
- The step lands **inside one process**. `e3781e57…` ran 2026-08-17 16:23 → 08-22 15:37 UTC without a restart. It logged 16.25 % and 17.09 % on 08-18/19, then 2.52 % and 2.52 % on 08-20/21. No deploy, restart or settings edge sits on it (section 5 below).
- **This is the question the re-derivation read must answer:** a fixed `burst_ratio_threshold` on a ratio of fast to 120-second-norm gross flow does not hold a stable fire rate across volatility regimes. Whether that is a defect or the intended behaviour is a derivation question. **This read does not decide it.**

---

## 1. Population and method — unchanged from the template

| Item | Value |
|---|---|
| Source | `aws_fetch/20260913-153704/analysis_log.csv.v0.7.bak` (33,911 rows, 2026-07-22 16:24:54 → 09-01 15:48:01) **+** `analysis_log.csv` (11,023 rows, 09-01 15:50:01 → 09-13 15:36:07). No overlap. 0 duplicate ASIA timestamps |
| Columns | Located by header name. The `.bak` has 111 columns and the live file 116. 0 rows with a wrong field count, 0 unparsed timestamps |
| Session | ASIA = UTC hours 00–07 inclusive. Read at run time from tracked `settings.json` v68, `session_volume.sessions[ASIA]` |
| Weekday | UTC day-of-week, Mon–Fri |
| Population | `AggrVelBurstRatio` non-empty |
| Fire / same-side / contra | As the template: `BURST_BUY` ↔ `BUY PRESSURE`, `BURST_SELL` ↔ `SELL PRESSURE`, TFI `NEUTRAL` counts as neither |
| Coverage rule | First ASIA row ≤ 00:06, last ≥ 07:54, no gap between consecutive ASIA rows > 6 min |
| `ExecResolution` | 3 on every ASIA row in the armed window (`res!=3` column = 0 on every day) |

**Reproduction check against the first read — passed.** The script's rows and fires for 2026-08-02 … 08-08 match `d3-asia-burst-watch-read-2026-08-10.md` §2 on every day (for example 08-03: 159 / 17; 08-05: 157 / 24). Its slice (b) baseline figure, 10.97 % at n = 1,905, is carried from that doc §10 and was not re-counted.

---

## 2. Instrument output — pasted verbatim

Command, run 2026-09-14 against HEAD `3fe3305` plus the uncommitted script:

```
powershell -NoProfile -File tools/ops/asia-burst-watch-read.ps1 -FetchFolder aws_fetch\20260913-153704 -SplitAt 2026-08-20
```

```
settings.json version=68 ASIA burst_ratio_threshold=5.5 ASIA hours=0-7
FILE analysis_log.csv.v0.7.bak rows=33911 badShape=0 unparsed=0 span=2026-07-22 16:24:54 -> 2026-09-01 15:48:01
FILE analysis_log.csv rows=11023 badShape=0 unparsed=0 span=2026-09-01 15:50:01 -> 2026-09-13 15:36:07
spans do not overlap (.bak ends 2026-09-01 15:48:01, live starts 2026-09-01 15:50:01)
duplicate ASIA timestamps skipped: 0

--- InstanceIds (all hours) ---
4325cb7e-c21e-444d-b6c4-b355178776cf  2026-07-22 16:24:54 -> 2026-07-22 19:24:00  rows=181  (analysis_log.csv.v0.7.bak)
fb908147-0312-4c55-b9d1-a23be310256e  2026-07-22 19:25:14 -> 2026-07-30 16:33:01  rows=7196  (analysis_log.csv.v0.7.bak)
0efcda74-6b75-4d5f-af04-f3875b5afd8e  2026-07-30 16:41:46 -> 2026-08-01 17:45:02  rows=1907  (analysis_log.csv.v0.7.bak)
5a3afd99-6db4-461c-886e-dddcca3d8c62  2026-08-01 17:50:12 -> 2026-08-01 18:58:01  rows=69  (analysis_log.csv.v0.7.bak)
09c747f8-1efb-4ffe-8716-ec8cedfa54c6  2026-08-01 19:02:49 -> 2026-08-07 16:01:02  rows=5346  (analysis_log.csv.v0.7.bak)
ec487909-940f-492b-8d1d-ee15f2ddcca0  2026-08-07 16:02:46 -> 2026-08-08 08:33:26  rows=650  (analysis_log.csv.v0.7.bak)
ffced26c-aaab-4cbc-b7e8-a0f1882dd3b3  2026-08-10 10:00:01 -> 2026-08-10 14:07:05  rows=129  (analysis_log.csv.v0.7.bak)
d8678d2b-94c4-4308-adc1-a88a51c2feea  2026-08-10 14:08:43 -> 2026-08-10 18:34:09  rows=267  (analysis_log.csv.v0.7.bak)
3be7f4c9-8a85-46c8-b3be-f5b820c82278  2026-08-10 18:36:01 -> 2026-08-11 17:18:01  rows=826  (analysis_log.csv.v0.7.bak)
a5d701ad-eea1-4ba0-97a5-2ea05274c8c5  2026-08-11 17:18:41 -> 2026-08-13 13:10:04  rows=1595  (analysis_log.csv.v0.7.bak)
e551f15e-b245-4392-8e71-89e749636f1c  2026-08-13 13:10:36 -> 2026-08-15 16:11:19  rows=2022  (analysis_log.csv.v0.7.bak)
e3781e57-f08c-480a-b79f-c87fb6e8285c  2026-08-17 16:23:13 -> 2026-08-22 15:37:03  rows=4532  (analysis_log.csv.v0.7.bak)
03a60e32-37f0-4236-af15-f4991ccdc96c  2026-08-22 16:03:11 -> 2026-09-01 15:48:01  rows=9191  (analysis_log.csv.v0.7.bak)
3fe57c53-5c32-4cdd-87fa-4f6f64901c1a  2026-09-01 15:50:01 -> 2026-09-13 15:36:07  rows=11023  (analysis_log.csv)

--- per ASIA session-day from 2026-08-02 (UTC) ---
day        dow asia pop fires rate%  same contra neut res!=3 fireBelow normalAbove minFire ATRmed maxGap first    last     covered weekday ids
2026-08-02 Sun  160 160    21 13.13    17      2    2      0         0           0   5.536   20.8    3.2 00:00:02 07:57:01 True    False   09c747f8
2026-08-03 Mon  160 159    17 10.69    16      0    1      0         0           0   5.707   40.7    3.4 00:00:02 07:57:05 True    True    09c747f8
2026-08-04 Tue  160 160    24 15.00    20      0    4      0         0           0   5.604   39.7    3.6 00:00:02 07:57:13 True    True    09c747f8
2026-08-05 Wed  160 157    24 15.29    23      0    1      0         0           0   5.603   37.1    3.9 00:00:02 07:57:01 True    True    09c747f8
2026-08-06 Thu  160 157    13  8.28    13      0    0      0         0           0   5.845   31.8    3.1 00:00:02 07:57:01 True    True    09c747f8
2026-08-07 Fri  160 159    17 10.69    13      2    2      0         0           0   5.603   35.4    3.3 00:00:03 07:57:03 True    True    09c747f8
2026-08-08 Sat  160 160    14  8.75    14      0    0      0         0           0   6.967    8.6    3.6 00:00:03 07:57:01 True    False   ec487909
2026-08-11 Tue  160 160    25 15.63    22      2    1      0         0           0   5.965   17.7    3.3 00:00:03 07:57:00 True    True    3be7f4c9
2026-08-12 Wed  160 160    26 16.25    22      3    1      0         0           1   5.556   26.1    3.9 00:00:01 07:57:02 True    True    a5d701ad
2026-08-13 Thu  160 157    26 16.56    23      0    3      0         0           0   5.818   31.3    3.0 00:00:01 07:57:02 True    True    a5d701ad
2026-08-14 Fri  159 157    22 14.01    19      3    0      0         0           0   5.598   29.7    3.2 00:03:06 07:57:00 True    True    e551f15e
2026-08-15 Sat  160 160    30 18.75    26      4    0      0         0           1   5.866   13.6    3.4 00:00:05 07:57:24 True    False   e551f15e
2026-08-18 Tue  160 160    26 16.25    24      2    0      0         0           0   5.553   29.4    3.9 00:00:02 07:57:13 True    True    e3781e57
2026-08-19 Wed  160 158    27 17.09    25      1    1      0         0           0   6.090   25.8    3.1 00:00:02 07:57:01 True    True    e3781e57
2026-08-20 Thu  160 159     4  2.52     3      1    0      0         0           0   5.937   76.0    3.3 00:00:07 07:57:01 True    True    e3781e57
2026-08-21 Fri  160 159     4  2.52     4      0    0      0         0           0   5.988  140.7    3.4 00:00:01 07:57:01 True    True    e3781e57
2026-08-22 Sat  160 160     7  4.38     7      0    0      0         0           1   5.595  115.2    3.7 00:00:02 07:57:01 True    False   e3781e57
2026-08-23 Sun  160 160    13  8.13    13      0    0      0         0           0   5.678   57.4    3.2 00:00:03 07:57:01 True    False   03a60e32
2026-08-24 Mon  160 160    13  8.13    13      0    0      0         0           0   5.767   91.5    3.4 00:00:03 07:57:01 True    True    03a60e32
2026-08-25 Tue  159 159     6  3.77     6      0    0      0         0           0   5.501  120.0    3.2 00:03:04 07:57:06 True    True    03a60e32
2026-08-26 Wed  160 155    10  6.45    10      0    0      0         0           0   6.128   73.9    3.2 00:00:03 07:57:02 True    True    03a60e32
2026-08-27 Thu  160 160    15  9.38    11      1    3      0         0           0   5.902   57.2    3.4 00:00:01 07:57:01 True    True    03a60e32
2026-08-28 Fri  160 158     5  3.16     5      0    0      0         0           0   5.549   69.9    3.7 00:03:01 07:57:00 True    True    03a60e32
2026-08-29 Sat  160 160    18 11.25    17      0    1      0         0           0   5.658   34.9    3.1 00:00:01 07:57:01 True    False   03a60e32
2026-08-30 Sun  160 160    14  8.75    10      2    2      0         0           0   5.683   28.3    3.4 00:00:02 07:57:04 True    False   03a60e32
2026-08-31 Mon  160 159     9  5.66     8      0    1      0         0           1   5.597   81.2    3.2 00:00:01 07:57:09 True    True    03a60e32
2026-09-01 Tue  160 158     4  2.53     3      0    1      0         0           1   5.566   61.3    3.5 00:00:02 07:57:01 True    True    03a60e32
2026-09-02 Wed  160 158    12  7.59    11      1    0      0         0           0   6.031   66.7    3.3 00:00:01 07:57:00 True    True    3fe57c53
2026-09-03 Thu  160 160     8  5.00     7      0    1      0         0           0   5.791   73.3    3.6 00:00:01 07:57:01 True    True    3fe57c53
2026-09-04 Fri  160 160     3  1.88     2      1    0      0         0           0   5.714   67.0    3.6 00:00:01 07:57:01 True    True    3fe57c53
2026-09-05 Sat  160 160    13  8.13    13      0    0      0         0           0   6.057   26.3    3.4 00:00:01 07:57:04 True    False   3fe57c53
2026-09-06 Sun  160 159    19 11.95    11      5    3      0         0           0   5.539   34.7    3.8 00:00:02 07:57:02 True    False   3fe57c53
2026-09-07 Mon  160 160     7  4.38     7      0    0      0         0           1   5.509   61.0    3.1 00:00:01 07:57:04 True    True    3fe57c53
2026-09-08 Tue  160 159    11  6.92    10      1    0      0         0           1   5.826   61.5    3.3 00:00:02 07:57:01 True    True    3fe57c53
2026-09-09 Wed  160 160     4  2.50     3      1    0      0         0           0   6.028   52.7    3.5 00:00:02 07:57:01 True    True    3fe57c53
2026-09-10 Thu  160 159     9  5.66     9      0    0      0         0           0   5.763   55.2    3.5 00:03:05 07:57:01 True    True    3fe57c53
2026-09-11 Fri  159 158     7  4.43     7      0    0      0         0           0   5.617   56.4    3.1 00:03:01 07:57:01 True    True    3fe57c53
2026-09-12 Sat  160 160    17 10.63    13      2    2      0         0           2   5.596   23.8    3.6 00:00:02 07:57:04 True    False   3fe57c53
2026-09-13 Sun  160 160    20 12.50    15      3    2      0         0           0   5.566   18.5    4.0 00:00:02 07:57:03 True    False   3fe57c53

arming check (armed days, all ASIA population rows incl. weekend): fireBelow=0 (must be 0)  normalAbove=9 (lean-floor gate, expected)

=== (a) all post-v65 covered weekday session-days ===
days=28 (2026-08-03 .. 2026-09-11)  pop rows=4445  rows/day=158.8
fires=378  FIRE RATE=8.50%  band 8%-14%  ref 11.0%
same=339  SAME-SIDE=89.68%  (bar 85%)  contra=19 (5.03%)  neutral=20 (5.29%)  contra/day=0.68
daily rate mean=8.51% sd=5.26pp  day-level t vs ref=-2.51 on 27 df  row-level z vs ref=-5.32
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=-2.47pp two-proportion z=-3.11
min fire ratio=5.501 (threshold 5.5)  mean daily ATRmed=57.5  Pearson r(daily ATRmed, daily rate)=-0.73
criteria: rateInBand=True sameSide=True length=True  => PASS

=== (b) covered weekday session-days after 2026-08-07 ===
days=23 (2026-08-11 .. 2026-09-11)  pop rows=3653  rows/day=158.8
fires=283  FIRE RATE=7.75%  band 8%-14%  ref 11.0%
same=254  SAME-SIDE=89.75%  (bar 85%)  contra=17 (6.01%)  neutral=12 (4.24%)  contra/day=0.74
daily rate mean=7.75% sd=5.37pp  day-level t vs ref=-2.90 on 22 df  row-level z vs ref=-6.28
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=-3.22pp two-proportion z=-4.01
min fire ratio=5.501 (threshold 5.5)  mean daily ATRmed=62.0  Pearson r(daily ATRmed, daily rate)=-0.72
criteria: rateInBand=False sameSide=True length=True  => MISS

=== (split) covered weekday session-days after 2026-08-07, before 2026-08-20 ===
days=6 (2026-08-11 .. 2026-08-19)  pop rows=952  rows/day=158.7
fires=152  FIRE RATE=15.97%  band 8%-14%  ref 11.0%
same=135  SAME-SIDE=88.82%  (bar 85%)  contra=11 (7.24%)  neutral=6 (3.95%)  contra/day=1.83
daily rate mean=15.96% sd=1.07pp  day-level t vs ref=11.38 on 5 df  row-level z vs ref=4.90
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=+5.00pp two-proportion z=3.79
min fire ratio=5.553 (threshold 5.5)  mean daily ATRmed=26.7  Pearson r(daily ATRmed, daily rate)=-0.02
criteria: rateInBand=False sameSide=True length=False  => MISS

=== (split) covered weekday session-days after 2026-08-07, from 2026-08-20 ===
days=17 (2026-08-20 .. 2026-09-11)  pop rows=2701  rows/day=158.9
fires=131  FIRE RATE=4.85%  band 8%-14%  ref 11.0%
same=119  SAME-SIDE=90.84%  (bar 85%)  contra=6 (4.58%)  neutral=6 (4.58%)  contra/day=0.35
daily rate mean=4.85% sd=2.26pp  day-level t vs ref=-11.20 on 16 df  row-level z vs ref=-10.22
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=-6.12pp two-proportion z=-7.82
min fire ratio=5.501 (threshold 5.5)  mean daily ATRmed=74.4  Pearson r(daily ATRmed, daily rate)=-0.19
criteria: rateInBand=False sameSide=True length=True  => MISS
```

⚠ The sub-slice lines print `MISS` because the script applies the watch criteria to every slice. **Only slice (b) carries the verdict.** The sub-slices are diagnostic.

---

## 3. Like-for-like comparison — the same device as `d3-asia-burst-watch-read-2026-08-10.md` §3

| Window | Box | Weekday session-days | Rows | Rows/day | Fire rate @ 5.5 |
|---|---|---:|---:|---:|---:|
| 2026-07-22 … 08-07 (first read, carried) | AWS only | 12 | 1,905 | ~159 | **10.97 %** |
| 2026-08-11 … 09-11 (this read, slice (b)) | AWS only | 23 | 3,653 | 158.8 | **7.75 %** |

- Same box, same coverage (~159 rows/day both), same classifier threshold.
- Shift **−3.22 pp**. Two-proportion z = −4.01, row-level.
- **Day-level test, which the template preferred because bursts cluster inside a day:** mean 7.75 %, sd 5.37 pp, **t = −2.90 on 22 df against the 11.0 % reference.** Significant at conventional levels. This is not the sampling noise that explained the first read's +1.75 pp.
- **Design effect has jumped.** The first read measured a daily sd of 2.71 pp against 2.47 pp binomial-only (design effect 1.20). Slice (b) shows 5.37 pp against ~2.1 pp binomial at p = 7.75 %, n ≈ 159. **Day-to-day variation is no longer mostly sampling noise.** The regime step in section 0 is the reason.
- **Consequence for ruling T-2 (the ±3 pp band):** that band was sized on design effect 1.20. At the variance seen here, T-2's false-alarm arithmetic in `d3-asia-burst-watch-read-2026-08-10.md` §10 no longer holds. Flagged for the re-derivation read, not re-ruled here.

---

## 4. Exclusions — every weekday in the window, and why it is in or out

| Weekday session-day(s) | Status | Reason |
|---|---|---|
| 2026-08-10 Mon | ⛔ Excluded | **No ASIA rows.** Inside the intentional weekend instance stop (`d3-asia-burst-watch-read-2026-08-10.md` §8). AWS returned 10:00:01 UTC |
| 2026-08-17 Mon | ⛔ Excluded | **No ASIA rows.** `e551f15e…` last row 2026-08-15 16:11:19; `e3781e57…` first row 2026-08-17 16:23:13. A ~48 h analysis-CSV hole. ⚠ **Its cause is not recorded** — the deploy ledger row for `e3781e57…` says so too |
| All other weekdays 2026-08-11 … 09-11 | ✅ Included, 23 days | Every present day passed the coverage rule: first row ≤ 00:03:06, last ≥ 07:57:00, max gap ≤ 3.9 min |
| 2026-09-14 onward | Not in the copy | The fetch ends Sunday 2026-09-13 15:36 UTC |

- Zero present weekdays failed the coverage rule. The only losses are two whole missing days.
- Weekend days are shown in the section 2 output and excluded from every figure.

---

## 5. Traps from `asia-burst-watch-read-brief-2026-09-14.md` §0 and §3 — each addressed

| Trap | How it was handled |
|---|---|
| Count only the live CSV and miss the `.bak` | **Both counted.** Spans printed and checked for overlap: none. 0 duplicate ASIA timestamps |
| Filter on one `InstanceId` | **No id filter.** 14 distinct ids in the pair, listed in section 2. Slice (b) spans 6 ids: `3be7f4c9…`, `a5d701ad…`, `e551f15e…`, `e3781e57…`, `03a60e32…`, `3fe57c53…` |
| An `InstanceId` missing from the deploy ledger | **None missing.** All 14 were diffed by eye against `aws-collector-deploy-checklist.md` §5a "Version ↔ InstanceId ledger" (line 255): 12 are table rows, and `4325cb7e…` and `fb908147…` sit in that doc's prose at line 251. ⚠ Two ids carry **settings "NOT VERIFIED"** in that ledger: `e3781e57…` and `03a60e32…` |
| Arming, independent of the ledger | **fireBelow = 0** over every armed ASIA population row (weekends included). Minimum fire ratio per day ranges 5.501–6.967, and 5.501 appears on 08-25 under `03a60e32…`. **So the effective ASIA threshold is 5.5 under both ledger-unverified ids.** That is evidence about this one key, not about their whole settings version |
| The 9 `NORMAL` rows at ratio ≥ 5.5 | **Not a disarm.** `IndicatorEngine.ClassifyAggressorBurst` (`Core/Indicators_OrderFlow.vb:174`) returns `NORMAL` at ratio ≥ threshold when \|lean\| < `direction_lean_floor` (`settings.json` 0.2). ⚠ The first read reported exactly zero disagreements over 1,112 rows. Its check was stricter than the classifier, and it never met a lean-floor row. `lean` is not logged, so these 9 cannot be confirmed as lean-gated row by row |
| GMT+8 day-of-week | `date -u` run first (Mon 2026-09-14 14:13 UTC). Day-of-week comes from the UTC CSV timestamp, parsed with `InvariantCulture` |
| The derivation's 9.7 % and "~106 rows/day" | Not used. Reference is 11.0 % (T-1). Rows/day measured at 158.8 |
| The wrong heading numbered §5a | Ledger read at `aws-collector-deploy-checklist.md` line 255, not the v64 deploy record at line 324 |
| A deploy or settings edge on 2026-08-20 | **None.** `git log -- settings.json`: v67 commit `613cf1e` is dated 2026-08-20 14:10 UTC, after that day's ASIA session closed. v68 commit `aa0e6e7` is dated 08-21 16:20 UTC. Neither v66, v67 nor v68 changes a `burst`, `norm_window` or `fast_window` line. `Core/AggressorVelocityAccumulator.vb` has **no commit since v65**. The step sits inside one process, `e3781e57…` |

---

## 6. Standing facts unchanged

- **Same-side holds in both regimes** (88.82 % and 90.84 %). The burst still agrees with TFI when it fires.
- **The contra arm is still near-dead:** 0.74 contra fires per day in slice (b), 0.35/day in the high-ATR regime. `asia-burst-threshold-derivation-2026-08-01.md` §3 stands: on res-3 this modifier is in practice upgrade-only.
- **This is a distributional read, not an outcome read.** Nothing is joined to forward returns. The only outcome read on this knob is still the W6-4 ceiling audit's `AggrVelBurstRatio` AUC 0.5179 (n = 217), carried from `d3-asia-burst-watch-read-2026-08-10.md` §0.

---

## 7. Decisions taken under the auto-proceed rule (`CLAUDE.md` "AUTO-PROCEED ON YOUR OWN RECOMMENDATION")

| Decision | Options | Picked | Why |
|---|---|---|---|
| Which slice carries the verdict | (a) all post-v65, PASS at 8.50 % · (b) new days only, MISS at 7.75 % | **(b)** | (b) is the more truthful option. (a) passes only by re-counting five days already read. There is no trade to reserve: (b) gives up nothing |
| Add sub-slices and ATR to the committed script | Keep the script to the brief's two slices · add `-SplitAt` and a per-day ATR column | **Add them** | Records more. Without them the pooled 7.75 % reads as a mild drift, when the data is two regimes at 16 % and 5 % |
| Next watch read date | See the queue edit | 10 fresh weekdays after 2026-09-11 | Ruling T-3 |

---

## 8. What the re-derivation read should take up — questions, not answers

1. Is the ASIA burst fire rate **volatility-conditional by construction**? The 120-second norm rises with sustained high flow, and a 5-second burst then reads as a smaller multiple. ⚠ This is a mechanism hypothesis, not a finding. It was not tested.
2. Does the 11.0 % reference (T-1) describe any regime? It was measured over 12 days at median ATR ~31–50 (per-day ATR from the diagnostic run; not recorded in the first read). The low-ATR days here read 16 %, the high-ATR days 5 %.
3. Is the ±3 pp band (T-2) still correctly sized, given the design effect in section 3?
4. Does the regime step also show in LONDON and NY? Not read here.

⛔ **Reserved, not proposed:** any `settings.json` change, including `burst_ratio_threshold` or a volatility-conditioned threshold. It is a live scoring change.

---

## 9. What I did not verify

- **The cause of the 2026-08-20 regime step.** ATR rose from ~26–30 to 76–141 within two sessions. I did not check market events, funding or open interest.
- **The ATR column's resolution and units.** Taken as logged. Treated only as a relative regime marker.
- **The lean-floor explanation row by row** for the 9 `NORMAL` rows at ratio ≥ 5.5. `lean` is not in the CSV.
- **Settings version of `e3781e57…` and `03a60e32…`.** Only the effective ASIA threshold (5.5) was recovered from the data.
- **The cause of the 2026-08-15 → 08-17 CSV hole.**
- **Outcomes.** Not measured, as in the first read.
- **The first read's 12-day baseline** (10.97 %, n = 1,905). Carried, not re-counted. The five weekdays it shares with slice (a) were re-counted and match.
- **Anything on the AWS box itself.** All facts come from the 2026-09-13 fetch folder.
