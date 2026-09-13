# Absorption episode-age read — 2026-09-13 (UTC)

> ## ⚠ PRELIMINARY — 8 OF THE RULED ~10 WEEKDAY-DAYS.
>
> **This is `D-1`'s post-ship read for `D-2`** ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6: *"re-read after ~2 weekday-weeks"*). `D-1` went live 2026-09-01 15:49 UTC; the 10th full weekday-day closes at the end of **Tue 2026-09-15**. **Today is Sunday, so no further weekday data exists yet.**
>
> ⭐ **The final read is ONE command from 2026-09-16 (UTC) onward** — fetch, then run §0's handle against the new fetch.

---

## 0. Handles

| # | Handle | Result at this read |
|---|---|---|
| **H-1** | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/absorption-episode-age-read.ps1 -Path aws_fetch\20260913-153704\analysis_log.csv` | **Exit 0.** First line: `CALIBRATION PASSED - reads=840/840 over=218/218 aggrAll=909850/909850 aggrOver=442070/442070` |
| **H-2** | The same script, `-Mode Calibrate` | Reproduces the 2026-09-11 Python read exactly, on the dated fetch `aws_fetch\20260909-143922\analysis_log.csv` (MD5 `c20cbb37…`) |

⚠ **Who can run these.** The instrument is committed. **The input is not** — `aws_fetch/` is untracked, so a fresh clone must fetch first (`tools/ops/collector.ps1 fetch`) and **cannot calibrate at all**, because the calibration reference is the 2026-09-09 fetch. That is the same limit `tools/ops/kelly-trigger-read.ps1` states.

⛔ **Why calibration is pinned to a DATED directory, measured this session:** `collector.ps1 fetch` copies every new book over the root `analysis_log_aws.csv` with `-Force`. **The 2026-09-11 numbers were measured on that root file, and this session's fetch overwrote it** (root MD5 now `6644c196…`, dated copy `c20cbb37…`). A root-path reference would have silently calibrated against a different book.

---

## 1. The book

| Fact | Value |
|---|---|
| Fetch | `aws_fetch/20260913-153704`, all transfers size-verified, read-only on the box |
| MD5 | `6644C196A6C899A4F441B3F7DEA9EB2D` |
| Rows | **11,023** · weekday **7,854** · weekend 3,169 · unparsed **0** · short 0 · quoted-field rows **0** |
| Weekday span | **2026-09-01 15:50:01 → 2026-09-11 23:59:07 UTC** |
| Processes | **ONE** — `3fe57c53-5c32-4cdd-87fa-4f6f64901c1a` for every read. No version edge inside the span |
| `window_sec` | **10**, read from tracked `settings.json` v68 |
| Absorption-active reads | **1,159** — rows carrying a non-empty `AbsorptionEpisodeSec` |

⚠ **Zero quoted fields matters:** the instrument splits on commas, where the 2026-09-11 read used Python's `csv` module. On a book with a quoted comma the two would disagree silently. The instrument counts quoted rows and prints the count so this stays checked on every future book.

---

## 2. Results

| Statistic | Value |
|---|---|
| `AbsorptionEpisodeSec` p25 · **p50** · **p75** | 0.70 s · **2.20 s** · **10.80 s** |
| p90 · p95 · p99 · max | 26.90 s · 40.50 s · 64.49 s · 253.10 s |
| mean | **9.21 s** |
| ⭐ **Reads on an episode older than `window_sec`** | **307 / 1,159 = 26.49 %** |
| Pressing on those reads | **625,810 / 1,720,860 USD = 36.37 %** |
| `D-2` span multiplier there (age ÷ 10) | p50 **2.20×** · p75 3.50× · p90 5.14× · max 25.31× |
| Reads carrying any pressing | 230 of 1,159 (over-window: 93 of 307) |
| ⛔ **Top 10 reads' share of ALL logged pressing** | **56.35 %** |
| **Day-block bootstrap 95 %, over-window read share** | **21.5 – 32.2 %** |
| **Day-block bootstrap 95 %, pressing share on over-window reads** | **25.8 – 55.4 %** |

*(Bootstrap: 9 UTC-weekday blocks, 2,000 resamples, seed 20260913. ⚠ Nine blocks is crude, and the first is a partial day.)*

---

## 3. What held and what did not

| | 6-day read (2026-09-11) | **8-day read (this)** | Day-block 95 % |
|---|---:|---:|---:|
| Absorption-active reads | 840 | **1,159** | |
| p50 · p75 · mean | 2.15 · 10.38 · 9.50 s | **2.20 · 10.80 · 9.21 s** | |
| ✅ **Over-window read share** | 26.0 % | **26.5 %** | **21.5 – 32.2 %** |
| ⛔ **Pressing share on those reads** | 48.6 % | **36.4 %** | **25.8 – 55.4 %** |
| Span multiplier p50 | 2.25× | **2.20×** | |

⚠ **The two books OVERLAP — the 8-day book contains the 6-day one.** This table shows how far two more weekday-days moved each pooled number; it is not two independent samples.

- ✅ **The read share is stable.** It moved half a point, and its interval is tight.
- ⛔ **The pressing share is NOT a rate — it is a heavy-tailed SUM.** Ten reads out of 1,159 (0.9 %) carry **56 %** of all logged pressing, so one large print on a long or a short episode swings the pooled figure. Per-day values run **11.5 % → 93.8 %**. **Both 48.6 % and 36.4 % sit inside the interval. Quote the interval; never a point.**

⛔ **The error this corrects, stated plainly:** the 2026-09-11 docs quoted *"26 % of reads carrying 48.6 % of all logged pressing"* and called that split *"load-bearing."* **The 48.6 % was a 6-day point on a lumpy sum.** Every copy is corrected in place to the interval, in the same commit as this read.

---

## 4. Segments

**By UTC weekday**

| Day | Reads | Over | Over % | Press % on over |
|---|---:|---:|---:|---:|
| 2026-09-01 Tue *(partial)* | 79 | 14 | 17.7 % | 82.6 % |
| 2026-09-02 Wed | 159 | 26 | 16.4 % | 70.7 % |
| 2026-09-03 Thu | 164 | 29 | 17.7 % | 12.3 % |
| 2026-09-04 Fri | 107 | 32 | 29.9 % | 50.7 % |
| 2026-09-07 Mon | 127 | 53 | 41.7 % | 93.8 % |
| 2026-09-08 Tue | 152 | 47 | 30.9 % | 11.5 % |
| 2026-09-09 Wed | 127 | 37 | 29.1 % | 33.4 % |
| 2026-09-10 Thu | 125 | 33 | 26.4 % | 55.9 % |
| 2026-09-11 Fri | 119 | 36 | 30.3 % | 26.3 % |

**By session** (buckets from `settings.json` v68, first-match inclusive — verified against `MatchSessionBucket` at [`../Core/ExecutionResolution.vb`](../Core/ExecutionResolution.vb)`:43`)

| Session | Reads | Over | Over % | Press % on over |
|---|---:|---:|---:|---:|
| ASIA (00–07, res 3) | 202 | 74 | 36.6 % | 90.9 % |
| LONDON (08–12, res 3) | 113 | 31 | 27.4 % | 38.0 % |
| NY (13–23, res 1) | 844 | 202 | 23.9 % | 20.9 % |

⚠ **NY is 73 % of all reads, so every pooled figure above is NY-weighted.** The standing lesson applies — segment before pooling.

⚠ **Two patterns are recorded and NOT interpreted:**
- **The first three weekdays read ~16–18 % over-window; the six after read 26–42 %.** Volatility regime is the obvious candidate. **ATR was not joined, so that is a guess.**
- **ASIA runs higher on both columns than NY.** ASIA and LONDON execute on 3-minute bars and NY on 1-minute. **This read cannot separate a session effect from a resolution effect.**

---

## 5. What this read changes

| Question | Answer |
|---|---|
| **Does `D-2`'s premise survive?** | ✅ **YES, and it did not need this read.** The press queue is cleared at every episode open and close ([`../Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:117`, `:315`), so episode-cumulative is a **superset** of what ships. **It has no downside branch.** This read only sizes it |
| **Is `D-2` worth building?** | ⭐ **On 8 of 10 days: yes.** It binds on **21.5–32.2 %** of absorption-active reads, and those reads hold a material share of all pressing (**25.8–55.4 %**). Median span multiplier **2.2×** |
| **Does `D-6d.3` = (c) change?** | **No.** Its argument is mechanical — `D-6d` Stage 2 without `D-2` makes `absorbRatio` fall — not a magnitude. **The earlier claim that the 26 / 48.6 split was load-bearing for it was wrong; neither number ever was** |
| **What would the 2026-09-16 read have to show to change anything?** | The over-window read share collapsing toward zero. **Its lower 95 % bound is 21.5 % on 8 days.** Two more weekday-days cannot plausibly get there, **and the committed build spec does not wait on it beyond the date gate** |

---

## 6. ⚠ What I did NOT verify

- ⚠ **8 of ~10 weekday-days.** The final read runs from 2026-09-16 UTC.
- ⚠ **`AbsorptionEpisodeSec` is age AT THE READ, not episode lifetime.** It is the right variable for `D-2` and the wrong one for "how long does an episode live". **Do not quote §2's percentiles as lifetimes.**
- ⚠ **The pressing share is computed on `AbsorptionAggrUsd` AS LOGGED**, which is capped at 10 seconds of pressing on exactly the over-window reads. What `D-2` would actually accumulate there is **larger** — **argued from the code, not measured.**
- ⚠ **The bootstrap is crude** — 9 day-blocks, the first partial — and the 6-day and 8-day books overlap.
- ⚠ **ATR was not joined** (§4), so the day-to-day drift in over-window share is recorded, not explained.
- ⚠ **`D-1`'s read also covers `AbsorptionPullLB`, `AbsorptionPostLB`, `AbsorptionSizeStart` and `AbsorptionSizeMin`** ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5). **This read covers `AbsorptionEpisodeSec` only.** Those four answer §4.2's `pullFrac` residual and the denominator attribution, and remain unread.

### 6.1 Found on the way — the deploy ledger was three processes behind

[`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a — the *"only place a version straddle can be reconstructed from"* — had **no AWS row after `e551f15e…` (2026-08-13)**. The box's own `ws_health.log` in this fetch shows three later processes: `e3781e57…` (2026-08-17), `03a60e32…` (2026-08-22, where the store directory changes to `C:\DeribitEngine\`), and **`3fe57c53…` (2026-09-01) — the process behind this entire book.** **Back-filled in the same commit**, with the settings version marked **not verified** on the two rows where it is not. ⛔ **This matters now, not later: the next deploy — the `D-2` + rotation build — is a dataset boundary, and its row lands in that ledger.**
