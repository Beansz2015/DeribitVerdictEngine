# D3 / v65 ASIA aggressor-velocity arming watch — THIRD READ (2026-09-26 UTC)

**Template:** [`d3-asia-burst-watch-read-2026-09-14.md`](d3-asia-burst-watch-read-2026-09-14.md) (the second read). **Instrument:** [`tools/ops/asia-burst-watch-read.ps1`](../tools/ops/asia-burst-watch-read.ps1). **Tolerance:** rulings T-1 to T-5 in [`d3-asia-burst-watch-read-2026-08-10.md`](d3-asia-burst-watch-read-2026-08-10.md) §10 (reference 11.0 %, band 8–14 %, at least 10 weekday session-days, same-side at least 85 %). **Effort:** Opus 5.5, medium.

---

## 0. Verdict — ⛔ MISS on the fire rate, and the miss is LOCKED. Same-side PASSES. Length is 8 of 10

| Slice | Weekday session-days | Pop rows | Fires | Fire rate | Same-side | Mean daily median ATR | Verdict |
|---|---:|---:|---:|---:|---:|---:|---|
| **(b) after the second read's window — 2026-09-14 … 09-25. THE VERDICT SLICE** | **8** | 1,269 | 42 | **3.31 %** | **92.86 %** | 71.7 | ⛔ **MISS** (rate below the floor; length 8 < 10) |
| (a) all post-v65 — 2026-08-03 … 09-25 | 36 | 5,714 | 420 | 7.35 % | 90.00 % | 60.7 | MISS (rate below the floor) |

- **Length is short by 2 days.** The collector outage of 2026-09-18 → 09-21 cost two weekday sessions: 09-18 is partial (41 rows, ends 02:00:07, not covered) and 09-21 has no ASIA rows.
- ⭐ **The miss does not depend on the 2 missing days.** To reach the 8 % floor over 10 days, the 2 extra days would need about 85 fires on about 318 rows, a rate of about 27 %. The highest daily rate in the whole armed window is 18.75 % (2026-08-15, a Saturday). Two days at that rate give about 6.4 % for the slice. **No plausible 2 days lift the slice into band.** This is arithmetic on the observed daily maximum, not a forecast.
- **What the miss triggers:** nothing new. T-5 already fired on 2026-09-14, and the re-derivation READ it owes is still owed. **No threshold change.** `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` stays 5.5 (read from tracked `settings.json` v69). Any `settings.json` move is reserved to the trader.

⭐⭐ **The low regime found on 2026-09-14 has not ended, and it has gone lower.**

| Regime | Days | Fire rate | Mean daily median ATR |
|---|---:|---:|---:|
| 2026-08-11 … 08-19 (from the second read) | 6 | 15.97 % | 26.7 |
| 2026-08-20 … 09-11 (from the second read) | 17 | 4.85 % | 74.4 |
| **2026-09-14 … 09-25 (this read)** | **8** | **3.31 %** | **71.7** |

- Pearson r(daily median ATR, daily fire rate): **−0.74** over the 36 days of slice (a), −0.72 over the 8 days of slice (b).
- The three post-outage weekdays 2026-09-22 … 09-24 read **1.91 %, 1.88 %, 1.88 %** (3 fires each), at ATR 80–94.
- The ASIA burst modifier now fires on about 5 bars per session. On res-3 it is upgrade-only (contra 0.13/day in slice (b)), so its scoring effect has shrunk to about one upgrade per 30 ASIA bars.

---

## 1. Population and method — unchanged from the template, one instrument change

| Item | Value |
|---|---|
| Source | `aws_fetch/20260925-085341/`: `analysis_log.csv.v0.7.bak` (33,911 rows, 2026-07-22 16:24:54 → 09-01 15:48:01) **+** `analysis_log.csv.116col-83564b1b.20260924_184606.bak` (18,179 rows, 09-01 15:50:01 → 09-24 18:43:03) **+** `analysis_log.csv` (493 rows, 09-24 18:46:06 → 09-25 08:51:01). No overlaps. 0 duplicate ASIA timestamps. 0 bad-shape and 0 unparsed rows in all three |
| Session, weekday, population, fire / same-side / contra, coverage rule | As `d3-asia-burst-watch-read-2026-09-14.md` §1 |
| `ExecResolution` | 3 on every ASIA row (`res!=3` = 0 on every day) |
| Arming check | `fireBelow` = 0 (must be 0); `normalAbove` = 11 (the lean-floor gate, expected) |

**Instrument change (this read).** The script read exactly two hardcoded files. The 2026-09-24 S2 rotation added a third book, `analysis_log.csv.116col-….bak`, which holds 2026-09-01 → 09-24. Without the change the script would have thrown on the missing live-file span, or, on an older fetch, silently read only part of the window. It now pools `analysis_log.csv.v0.7.bak`, then every `analysis_log.csv.*col-*.bak` sorted by its rotation stamp, then the live file, and checks every adjacent pair for overlap.

**Reproduction check — passed on spot values.** Per-day rows and fires for 2026-08-03 (159 / 17) and 2026-08-05 (157 / 24) match `d3-asia-burst-watch-read-2026-09-14.md` §1 and the first read. The per-day table for 2026-08-02 … 09-11 in §2 below was not diffed line by line against the second read's §2.

---

## 2. Instrument output — the verdict slice, pasted verbatim

Command, run 2026-09-26 05:49 UTC against `949460a` plus the script change committed with this doc:

```
powershell -NoProfile -File tools/ops/asia-burst-watch-read.ps1 -FetchFolder 'aws_fetch\20260925-085341' -FirstReadEnd 2026-09-11 -SplitAt 2026-08-20
```

```
settings.json version=69 ASIA burst_ratio_threshold=5.5 ASIA hours=0-7
FILE analysis_log.csv.v0.7.bak rows=33911 badShape=0 unparsed=0 span=2026-07-22 16:24:54 -> 2026-09-01 15:48:01
FILE analysis_log.csv.116col-83564b1b.20260924_184606.bak rows=18179 badShape=0 unparsed=0 span=2026-09-01 15:50:01 -> 2026-09-24 18:43:03
FILE analysis_log.csv rows=493 badShape=0 unparsed=0 span=2026-09-24 18:46:06 -> 2026-09-25 08:51:01
duplicate ASIA timestamps skipped: 0

day        dow asia pop fires rate%  same contra neut res!=3 fireBelow normalAbove minFire ATRmed maxGap first    last     covered weekday ids
2026-09-14 Mon  160 160     5  3.13     4      0    1      0         0           0   5.630   72.1    3.2 00:00:01 07:57:01 True    True    3fe57c53
2026-09-15 Tue  160 159     8  5.03     8      0    0      0         0           1   5.558   56.3    3.5 00:00:01 07:57:02 True    True    3fe57c53
2026-09-16 Wed  160 159    10  6.29    10      0    0      0         0           0   6.210   61.3    3.3 00:00:02 07:57:01 True    True    3fe57c53
2026-09-17 Thu  160 156     4  2.56     4      0    0      0         0           0   6.476   59.0    3.7 00:00:01 07:57:01 True    True    3fe57c53
2026-09-18 Fri   41  41     2  4.88     2      0    0      0         0           0  10.529   49.6    3.5 00:00:04 02:00:07 False   True    3fe57c53
2026-09-22 Tue  160 157     3  1.91     3      0    0      0         0           0   8.570   93.5    3.3 00:00:02 07:57:05 True    True    ee159d03
2026-09-23 Wed  160 160     3  1.88     3      0    0      0         0           1   6.935   81.3    3.4 00:00:02 07:57:01 True    True    ee159d03
2026-09-24 Thu  160 160     3  1.88     3      0    0      0         0           0   5.665   80.1    3.5 00:00:02 07:57:01 True    True    ee159d03
2026-09-25 Fri  160 158     6  3.80     4      1    1      0         0           0   6.921   70.0    3.5 00:00:02 07:57:03 True    True    25951567

arming check (armed days, all ASIA population rows incl. weekend): fireBelow=0 (must be 0)  normalAbove=11 (lean-floor gate, expected)

=== (a) all post-v65 covered weekday session-days ===
days=36 (2026-08-03 .. 2026-09-25)  pop rows=5714  rows/day=158.7
fires=420  FIRE RATE=7.35%  band 8%-14%  ref 11.0%
same=378  SAME-SIDE=90.00%  (bar 85%)  contra=20 (4.76%)  neutral=22 (5.24%)  contra/day=0.56
daily rate mean=7.35% sd=5.16pp  day-level t vs ref=-4.24 on 35 df  row-level z vs ref=-8.82
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=-3.62pp two-proportion z=-4.97
min fire ratio=5.501 (threshold 5.5)  mean daily ATRmed=60.7  Pearson r(daily ATRmed, daily rate)=-0.74
criteria: rateInBand=False sameSide=True length=True  => MISS

=== (b) covered weekday session-days after 2026-09-11 ===
days=8 (2026-09-14 .. 2026-09-25)  pop rows=1269  rows/day=158.6
fires=42  FIRE RATE=3.31%  band 8%-14%  ref 11.0%
same=39  SAME-SIDE=92.86%  (bar 85%)  contra=1 (2.38%)  neutral=2 (4.76%)  contra/day=0.13
daily rate mean=3.31% sd=1.64pp  day-level t vs ref=-13.29 on 7 df  row-level z vs ref=-8.76
like-for-like vs first-read AWS baseline 10.97% (n=1905): diff=-7.66pp two-proportion z=-7.83
min fire ratio=5.558 (threshold 5.5)  mean daily ATRmed=71.7  Pearson r(daily ATRmed, daily rate)=-0.72
criteria: rateInBand=False sameSide=True length=False  => MISS
```

The full output also lists every InstanceId and every session-day from 2026-08-02. It is reproducible with the command above.

---

## 3. Eras inside the verdict slice

| Days | InstanceId | Build |
|---|---|---|
| 09-14 … 09-18 | `3fe57c53…` | pre-gap-repair build |
| 09-22 … 09-24 | `ee159d03…` | gap-repair deploy of 2026-09-21 |
| 09-25 | `25951567…` | engine-fix A + C and absorption S1 + S2 deploy of 2026-09-24 |

- None of these deploys touches aggressor velocity, TFI or the ASIA threshold. The rate is low on both sides of each edge (09-17: 2.56 %; 09-22: 1.91 %; 09-25: 3.80 %).
- ⚠ Engine-fix A (the POC-tier gate) is a live scoring-outcome change from 2026-09-24 18:46 UTC. It moves placed targets, not the burst classifier, so it cannot move this read's numbers. Stated, not tested.

---

## 4. For the re-derivation read (owed since 2026-09-14) — new facts only

- The low regime has now held over **25 covered weekdays** since 2026-08-20. 23 of them read below the 8 % floor; 2 read just inside the band (2026-08-24 8.13 %, 2026-08-27 9.38 %). No weekday since 2026-08-20 reached the 11 % reference.
- The regime still tracks ATR (r = −0.74 over 36 days). A fixed ratio threshold on fast gross flow over a 120-second norm gives a fire rate that falls as volatility rises. The re-derivation read must say whether that is a defect or the intended behaviour, as `d3-asia-burst-watch-read-2026-09-14.md` §8 asks.
- Same-side stays high in both regimes (92.86 % here). The fires that remain are still directionally clean; there are just fewer of them.

---

## 5. What I did not verify

- The 2026-08-02 … 09-11 per-day table was not diffed line by line against the second read. Two days were spot-checked.
- Whether the 2026-09-18 → 09-21 outage biases slice (b). It removes two sessions and adds none. Nothing links it to the rate.
- The claim in §0 that no plausible 2 days lift the slice rests on the observed daily maximum (18.75 %), not on any model of future days.
- The effect of this rate on outcomes. The watch measures fire rate and same-side share only.
