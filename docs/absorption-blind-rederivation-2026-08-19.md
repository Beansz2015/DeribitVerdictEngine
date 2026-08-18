# Absorption population — BLIND re-derivation, 2026-08-19

**Seat:** the incoming orchestrator seat (Opus / high). **Commissioned by:** the trader, per
[`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md) §0, as an *attack* on
[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) — not a review of it.

> ## ⛔ PART 1 WAS WRITTEN AND COMMITTED BEFORE THE PROPOSAL WAS OPENED.
>
> Everything in §1–§5 below was measured from the raw books and the tape store with the proposal
> **unread**. §6 (the comparison) was added afterwards. The commit history is the evidence: §1–§5
> land in their own commit, ahead of any read of that file.

**Sources.** `bin\Debug\net8.0-windows\analysis_log.csv` (LOCAL, 11,812 data rows) ·
`bin\Debug\net8.0-windows\analysis_log_aws.csv` (AWS, 18,984 data rows) ·
`backtest_data\trades_2026-08.csv` (tape store, 441,279 August rows) ·
tracked repo-root `settings.json` (**v66**, line 2).

---

## 0. Method — stated so it can be attacked in turn

**Scope.** Weekday only (Mon–Fri), UTC, `Timestamp >= 2026-07-23 00:00:00`. Reported **per book**;
pooled only where labelled.

**Why that boundary is safe, verified rather than assumed.** v61 is commit `e9e6407`, stamped
2026-07-23T03:19:50+08:00 = **2026-07-22 19:19:50 UTC**. The books carry the deploy edge themselves:
the AWS book changes `InstanceId` at **2026-07-22 19:25:14** (5½ minutes after the commit) and the
LOCAL book has **no rows at all** between 2026-07-22 17:43:14 and 2026-07-23 14:53:39. So a
00:00 UTC cut on 07-23 excludes every pre-v61 row on both books with room to spare.
**Sensitivity:** moving the AWS cut back to the instance edge adds 275 rows and moves
pressed/active from 10.05 % to 10.00 %. The boundary is not load-bearing.

**Column indices** (0-based, verified against the header — both books' headers are byte-identical,
111 fields, zero quotes, every row exactly 111 fields):
`Price` 1 · `Verdict` 2 · `ATR` 64 · `AbsorptionSignal` 100 · `AbsorptionLevel` 101 ·
`AbsorptionRatio` 102 · `AbsorptionAggrUsd` 103 · `AbsorptionPullFrac` 104 · `InstanceId` 109.

**Definitions.** **Proximity-ACTIVE episode row** = `AbsorptionLevel` non-empty (the tracker produced
a level; `HasEpisode = True`). **Pressed** = `AbsorptionAggrUsd > 0`.
**Session buckets — the SHIPPED ones**: ASIA 0–7, LONDON 8–12, NY 13–23, read from
`settings.json` `session_volume.sessions[]`. *(The 2026-07-23 derivation used 22–07 / 07–13 / 13–22;
its per-session figures are not comparable to these and are not compared.)*

**The shipped v61 anchors, read from tracked `settings.json` `indicators.absorption`** — not restated
from any document:

| Key | Shipped value |
|---|---|
| `proximity_atr_frac` | 0.30 |
| `band_atr_frac` | 0.10 |
| `window_sec` | 10 |
| `break_tol_atr_frac` | 0.05 |
| `absorb_ratio` | 1.5 |
| `depletion_floor_usd` | 5 000 |
| `max_pull_frac` | 0.75 |
| `default.min_aggr_usd` | 20 000 |
| `penalty` / `scoring_enabled` | 1 / **false** |

All three `sessions` blocks (`NY`, `LONDON`, `ASIA`) are **empty**, so every session inherits
`default.min_aggr_usd = 20 000`. There is no per-session anchor live.

**Gate reconstruction, and its one control.** The flag is
`Active AND aggrUsd >= 20000 AND ratio >= 1.5 AND pullFrac <= 0.75`
(`IndicatorEngine.ClassifyAbsorption`, `Core/Indicators_OrderFlow.vb:198-237`). The CSV carries the
**primary** side's numerics only, so a row-level reconstruction is an approximation when both sides
are active. **Control: the reconstruction reproduces the logged `AbsorptionSignal` exactly on both
books** — 3 = 3 on LOCAL, 2 = 2 on AWS. Nothing below depends on an unchecked reconstruction.

---

## 1. The population, per book

| | **LOCAL** | **AWS** |
|---|---:|---:|
| weekday days covered | 15 | 17 |
| rows | 4 999 | 14 680 |
| **proximity-ACTIVE episode rows** | **734** | **2 160** |
| active / rows | **14.68 %** | **14.71 %** |
| **pressed** (`aggrUsd > 0`) | **79** | **217** |
| **pressed / active** | **10.76 %** | **10.05 %** |
| flagged (`AbsorptionSignal ≠ NONE`) | 3 | 2 |
| flag rate, all rows | 0.060 % | 0.014 % |
| flag rate, **directional runs only** | 0.130 % (2/1 540) | 0.022 % (1/4 541) |

**Pooled** (stage-loss only, and labelled as such): 19 679 rows · 2 894 active (14.71 %) ·
296 pressed (**10.23 %** of active) · **5 flagged** — 0.025 % of rows, 0.17 % of active,
0.049 % of directional runs. The design band is **3–8 % of directional runs**
(`book-absorption-proposal.md` §8 header note at line 89). **We are 60–160× below its floor.**

**The two books agree independently and to within noise on both headline ratios** —
active/rows 14.68 % vs 14.71 %, pressed/active 10.76 % vs 10.05 % — on populations differing
by 2.9×. That is the strongest single result in this pass.

### Per session (shipped buckets)

| Book | Session | rows | active | active/rows | pressed | pressed/active | flagged |
|---|---|---:|---:|---:|---:|---:|---:|
| LOCAL | ASIA | 37 | 6 | 16.22 % | 2 | 33.33 % | 1 |
| LOCAL | LONDON | 335 | 59 | 17.61 % | 9 | 15.25 % | 1 |
| LOCAL | NY | 4 627 | 669 | 14.46 % | 68 | 10.16 % | 1 |
| AWS | ASIA | 2 559 | 479 | 18.72 % | 62 | 12.94 % | 1 |
| AWS | LONDON | 1 551 | 223 | 14.38 % | 31 | 13.90 % | 1 |
| AWS | NY | 10 570 | 1 458 | 13.79 % | 124 | 8.50 % | **0** |

⚠ **LOCAL's ASIA cell is 6 active rows.** Its 33 % is one row either way and must not be quoted.
**NY is still the barren session** — 15 197 pooled NY rows, 2 127 active, **one flag**.

### Episode identity — the row counts are not inflated

Contiguous same-level ACTIVE runs: LOCAL 647 runs over 734 rows (mean **1.13**), AWS 1 898 over
2 160 (mean **1.14**), p50 run length = 1 row on both. **Active rows are near-independent
observations**, so no count above is one long episode read many times.

---

## 2. The funnel against the shipped anchors

| Stage | LOCAL | survives | AWS | survives |
|---|---:|---:|---:|---:|
| proximity-ACTIVE | 734 | — | 2 160 | — |
| → pressed (`aggrUsd > 0`) | 79 | **10.8 %** | 217 | **10.0 %** |
| → `aggrUsd ≥ 20 000` | 9 | **11.4 %** | 19 | **8.8 %** |
| → `AND ratio ≥ 1.5` | 4 | 44.4 % | 11 | 57.9 % |
| → `AND pullFrac ≤ 0.75` = **FLAG** | **3** | **75.0 %** | **2** | **18.2 %** |

**Pooled:** 2 894 → 296 → 28 → 15 → **5**.

**Two stages lose ~90 % each, not one.** Observation (active→pressed) survives 10.2 % pooled;
the `min_aggr_usd` anchor (pressed→20 000) survives 9.5 % pooled. In **absolute** rows observation
is much the larger loss (2 598 rows vs 268). In **proportional** terms they are equal, and the
anchor stages *taken together* are worse than observation: 296 → 5 is 1.7 % survival against
observation's 10.2 %. **Which stage is "biggest" depends entirely on which of those two questions
you asked.** Both readings are in the table; neither is hidden.

**Gate-alone attrition on the ACTIVE population** (each anchor tested on its own):
`ratio ≥ 1.5` passes 6 / 734 and 18 / 2 160 · `pullFrac ≤ 0.75` passes 309 / 734 (42.1 %) and
872 / 2 160 (40.4 %). **The pull veto rejects ~59 % of every active row on both books.**

---

## 3. Percentiles

### `AbsorptionAggrUsd`

| Population | book | p25 | p50 | p75 | p90 | p95 | p99 | max |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ACTIVE | LOCAL | 0 | **0** | 0 | **10** | 640 | 31 910 | 255 540 |
| ACTIVE | AWS | 0 | **0** | 0 | **10** | 200 | 15 150 | 176 220 |
| PRESSED | LOCAL | 30 | **250** | 3 510 | 31 910 | 55 160 | 69 340 | 255 540 |
| PRESSED | AWS | 30 | **200** | 2 910 | 15 150 | 35 950 | 64 260 | 176 220 |

⚠ **The p90 of `aggrUsd` on active rows is 10 USD — one minimum-size print — on both books.**
The median **pressed** row carries 200–250 USD against a 20 000 anchor: **80–100× short.**
`min_aggr_usd` sits at roughly the **p89–p91** of the pressed population.

### `AbsorptionRatio`

| Population | book | p50 | p75 | p90 | p95 | p99 | max |
|---|---|---:|---:|---:|---:|---:|---:|
| ACTIVE | LOCAL | 0 | 0 | 0 | 0.03 | **1.20** | 51.11 |
| ACTIVE | AWS | 0 | 0 | 0 | 0.02 | **1.20** | 20.03 |
| PRESSED | LOCAL | 0.02 | 0.36 | **1.20** | 1.97 | 13.87 | 51.11 |
| PRESSED | AWS | 0.02 | 0.19 | **1.20** | 3.00 | 12.11 | 20.03 |

`absorb_ratio = 1.5` sits just above the p90 of pressed rows on both books. Note the mechanical
consequence of the depletion floor: when `sizeStart − sizeMin ≤ 5 000` the ratio collapses to
`aggrUsd / 5 000`, so `aggrUsd ≥ 20 000` **implies** `ratio ≥ 4.0` and the ratio gate cannot bind.
It only bites when the band lost **more than 13 333 USD** of size — i.e. on episodes where the level
was genuinely being eaten. That is the anchor behaving correctly, not a defect.

---

## 4. `AbsorptionPullFrac` — the DISTRIBUTION SHAPE

**It is not unimodal, and its median is not the interesting part.** Two discrete point masses sit on
a broad continuum:

| Band | LOCAL n (share) | AWS n (share) |
|---|---:|---:|
| **exactly 0.0000** | **200 (27.25 %)** | **522 (24.17 %)** |
| (0, 0.25) | 34 (4.63 %) | 110 (5.09 %) |
| [0.25, 0.5) | 32 (4.36 %) | 87 (4.03 %) |
| [0.5, 0.75] | 43 (5.86 %) | 153 (7.08 %) |
| (0.75, 1) | 135 (18.39 %) | 401 (18.56 %) |
| **exactly 1.0000** | **21 (2.86 %)** | **71 (3.29 %)** |
| (1, 2] | 175 (23.84 %) | 517 (23.94 %) |
| (2, 5] | 51 (6.95 %) | 168 (7.78 %) |
| (5, 10] | 21 (2.86 %) | 60 (2.78 %) |
| > 10 | 22 (3.00 %) | 71 (3.29 %) |

Percentiles: p50 **0.8868 / 0.9088** · p75 1.1805 / 1.2155 · p90 2.4392 / 2.7280 ·
p95 6.4180 / 6.6040 · max 61.1 / 72.7. **≤ 0.75 on 42.1 % / 40.4 %.**

> ### ⚠ THE PILE AT EXACTLY 1.0000 EXISTS. It is small, but it is real and it is not an artefact.
>
> Ranked by frequency of the **exact 4-dp logged value**, the ordering is identical on both books:
>
> | rank | LOCAL | AWS |
> |---|---|---|
> | 1 | `0.0000` × 200 | `0.0000` × 522 |
> | 2 | **`1.0000` × 21** | **`1.0000` × 71** |
> | 3 | *(three-way tie)* × 2 | `2.0000` × 7 |
>
> **`1.0000` is the second-most-frequent exact value on both books, at ~10× the next non-zero
> mode.** Nothing else in the continuum forms a point mass at all.
>
> **It is spread, not clustered:** the 21 LOCAL rows fall on **9 distinct days and 20 distinct
> level prices**; the 71 AWS rows on **15 distinct days and 65 distinct level prices**. With a mean
> run length of 1.13 rows, these are ~90 separate observations, not one episode logged repeatedly.
>
> **The mechanism that produces exactly 1.0000.** `pullFrac = pullLB / max(postLB, 5 000)`, and per
> interval **only one of the two accumulators increments** (`PullLB += max(0, −net)`,
> `PostLB += max(0, net)`). Exact equality therefore needs the pulls to sum to precisely the posts —
> which is what a **single visible-size round trip** produces: one interval shows `−X`, another `+X`.
> **A block leaving the visibility mask and returning is exactly that.** So the point mass is the
> ladder-shift signature, and it is present.
>
> ⚠ **What this does NOT establish.** 3 % of active rows is not "piles at 1.000" in the sense of
> dominating the distribution — the median is 0.89–0.91 and the largest mass by far is the
> `0.0000` spike. **A test of the median, or of "is 1.000 the mode", correctly returns NO.** A test
> for a *point mass* returns YES. Both are true statements about the same distribution and they
> support opposite conclusions, so the test that was run has to be named.

### What the pull veto actually costs — measured, not argued

Fifteen rows pooled reach the pull gate (already clearing `aggrUsd ≥ 20 000` **and**
`ratio ≥ 1.5`). **Ten of them are killed there.** Their logged `pullFrac`:

| Book | pullFrac at the gate | outcome |
|---|---|---|
| LOCAL | 0.0000 · 0.2000 · 0.4867 | ✅ flagged (3) |
| LOCAL | 0.9536 | ❌ vetoed |
| AWS | 0.0749 · 0.4309 | ✅ flagged (2) |
| AWS | **0.7615** · 0.8272 · 0.9140 · **1.0000** · **1.0013** · **1.0220** · **1.0247** · 1.1447 · 10.4100 | ❌ vetoed (9) |

⚠ **One is `0.7615` — vetoed by 0.0115.** Four more sit inside [0.9990, 1.0250], i.e. on the
point mass. Counted as a what-if on the pooled fifteen:

| `max_pull_frac` | pooled flags |
|---|---:|
| **0.75 (shipped)** | **5** |
| 0.85 | 7 |
| 0.95 | 8 |
| 1.00 | 10 |
| **1.05** | **13** |
| 1.20 | 14 |

**This one anchor is worth 2.6× on flag count.** It is also the anchor E5 ruled must hold, and
**13 flags is still ~0.13 % of directional runs — two orders of magnitude below the 3–8 % band.**
Both facts are true at once: the pull veto is materially more expensive than a median-based read
suggests, **and** relaxing it does not come close to fixing the headline. It is not the fix; it is
also not free.

---

## 5. ⭐ SEPARATING THE OBSERVATION LOSS — the thing the previous seat could not do

The question: of the ~90 % loss between ACTIVE and PRESSED, how much is **`window_sec` too short**
and how much is **a too-wide proximity gate**? Both are testable from data already on disk.

### 5.1 The proximity gate is 3× the pressing band, and that alone explains three-quarters of it

`proximity_atr_frac = 0.30` opens an episode. `band_atr_frac = 0.10` is what a print must land
inside to count as pressing (`FoldTradeSide`: `price >= lvl - band` above, mirrored below).
**The admission shell is three times the measurement shell.** `AbsorptionLevel`, `Price` and `ATR`
are all logged, so `|level − price| / ATR` is directly computable on every active row.

| `|level − price| / ATR` | LOCAL rows (share) | LOCAL press rate | AWS rows (share) | AWS press rate |
|---|---:|---:|---:|---:|
| **≤ 0.10 — inside the pressing band** | 218 (29.7 %) | **31.65 %** | 646 (29.9 %) | **30.34 %** |
| (0.10, 0.20] | 174 (23.7 %) | 4.02 % | 561 (26.0 %) | 2.32 % |
| (0.20, 0.30] | 156 (21.3 %) | 1.28 % | 453 (21.0 %) | 0.88 % |
| (0.30, 0.50] | 112 (15.3 %) | 0.00 % | 282 (13.1 %) | 1.06 % |
| (0.50, 1.0] | 68 (9.3 %) | 1.47 % | 178 (8.2 %) | 0.56 % |
| > 1.0 | 6 (0.8 %) | 0.00 % | 40 (1.9 %) | 0.00 % |

**The press rate collapses by an order of magnitude the moment price leaves the band, and only
~30 % of active rows are ever in it.** Decomposing the unpressed population:

| | LOCAL | AWS |
|---|---:|---:|
| unpressed active rows | 655 | 1 943 |
| …of which price was **inside** the band | 149 (**22.7 %**) | 450 (**23.2 %**) |
| …of which price was **outside** the band | 506 (**77.3 %**) | 1 493 (**76.8 %**) |

> ⭐ **≈ 77 % of the observation loss is GEOMETRIC, on both books independently.** The episode is
> open, the tracker is measuring, and price is sitting somewhere no trade it makes can land in the
> band. Only the remaining **≈ 23 %** is a case where price was genuinely at the level and the
> 10-second window still saw nothing — which is the only part `window_sec` can address.

⚠ **Caveat, stated rather than buried:** `Price` is `candles1m.Last().Close`, not the `bestAsk` /
`bestBid` the gate actually uses. Spread is negligible at this scale (`SpreadBps` p95 = 0.08 bps
≈ 5 cents, against a band of 0.10 × ATR ≈ 2.4–2.7 USD at the median ATR of 24–27 USD), but the
1-minute close and the ~100 ms book fold are not the same instant. §5.2 removes this concern
entirely by validating against the tape.

⚠ **Rows beyond 0.30 ATR exist (≈ 24 % of active) and the proximity gate should have excluded
them.** The tracker also requires the level to sit inside the *visible top-10 ladder span*, and
takes `min(proximity, visible)` — but this shows a level can be carried at 0.5–2.0 ATR from the
1-minute close while still inside the visible span at fold time. Fast price movement between the
fold and the close is the ordinary explanation and I did **not** confirm it. Recording it because
it is not what the §8 "min(proximity, visible)" note predicts.

### 5.2 Tape replay — the window test, run against verified-complete data

The store's third era (after `a5d701ad…`, **2026-08-11 17:18:42Z**) is `trade_seq`-verified
100.000 % complete. I sliced it: **134,204 rows**, 2026-08-11 17:19:21 → 2026-08-14 08:24:30 —
matching the handover's stated count exactly. Against it, the AWS book's **364 weekday active rows**
in that era (84 of them in-band). For each row I recomputed what `aggrUsd` *would* be, using the
tracker's own predicate (`buy` at `price ≥ level − band` above; mirrored below) over widening
windows.

**Alignment control, run first.** Scanning the read-time offset from −30 s to +20 s, agreement peaks
sharply at **+2 s** (the read precedes `LogRun`): all **22** logged-pressed rows are reproduced
(zero logged-only), **21 of 22** satisfy `logged ≤ 1.05 × replay`, and **12 of 22 match to within
1 USD**. Off-peak offsets degrade immediately (at −10 s: 4 of 22). **The replay reproduces the
engine's own arithmetic**, which is what licenses everything below.

| window | rows pressed / 364 | | in-band pressed / 84 | | rows ≥ 20 000 USD |
|---|---:|---:|---:|---:|---:|
| **10 s (shipped)** | **72** | 19.8 % | **43** | 51.2 % | **18** |
| 30 s | 103 | 28.3 % | 52 | 61.9 % | 31 |
| 60 s | 140 | 38.5 % | 67 | 79.8 % | 51 |
| 120 s | 203 | 55.8 % | 74 | 88.1 % | 93 |
| 300 s | 282 | 77.5 % | 79 | 94.0 % | 171 |

Two separate findings fall out.

**(a) `window_sec` is genuinely short against this tape.** Total market flow — *all* prints, both
directions, all prices — in the 10 s before each read has p25 = 10 USD, **p50 = 1 020 USD**,
p75 = 17 300, p90 = 64 080. **A quarter of 10-second windows are effectively empty.** Asking for
20 000 USD of *single-direction, in-band* flow inside 10 s asks for roughly a p80 window in total
volume with all of it pointed the right way. Widening to 60 s takes in-band coverage from 51 % to
80 % and the ≥ 20 000 count from 18 to 51.

**(b) ⚠ A third cause neither candidate anticipated, and it is bigger than the window.** At the
validated alignment the engine logged `aggrUsd > 0` on **22** rows while the tape shows qualifying
in-band flow in the same 10 seconds on **72**. **The engine counted 31 % of the flow its own
definition admits.** The 50-row gap is not window length — it is the same window — so the episode
was not `Active` when that flow printed.

I ran the obvious control, because the honest alternative is that the engine was *right* to
discard: a break-tolerance print (`0.05 × ATR` past the level) closes the episode instantly and
correctly.

| group | rows | had a break-tol print in the window |
|---|---:|---:|
| A. logged > 0 | 22 | 6 (27 %) |
| **B. logged = 0, tape shows in-band flow** | **50** | **21 (42 %)** |
| C. logged = 0, tape shows none | 292 | 20 (7 %) |

**29 of the 50 had no break-tol print at all** — the level held for the whole window and the
tracker still recorded nothing. And group B's unlogged flow is not small: p50 **6 800 USD**,
p75 18 910, p90 51 280, and **13 of the 50 rows carried ≥ 20 000 USD** — against just **19 rows
clearing `min_aggr_usd` in the entire 17-weekday-day AWS book.**

> ⭐ **So the answer to "`window_sec` or proximity gate" is: mostly neither, and both.**
> Ranked by size on the ACTIVE population:
>
> 1. **Geometry — the proximity shell is 3× the measurement band.** ≈ 77 % of the observation loss.
>    Costs nothing to test; already visible in the CSV.
> 2. **Episode state — flow arrives while the side is not `Active`.** The engine misses ~69 % of the
>    in-band flow its own 10 s window admits, and only ~42 % of those rows have a break-through to
>    justify it. This is a **new** candidate; it was not one of the two on offer.
> 3. **`window_sec` = 10 s against a tape whose median 10-second window holds ~1 020 USD.** Real,
>    and the cheapest of the three to change, but it is the smallest of the three.
>
> ⚠ **(2) has a consequence for the D-table that (1) and (3) do not:** widening `window_sec` on a
> tracker that is not `Active` for the flow **buys less than the replay table suggests**, because
> the replay ignores episode state. **The replay table is an upper bound on what a window change
> can deliver, not a forecast.** Anyone reading row "60 s → 140 pressed" as a prediction will be
> disappointed by roughly the factor in (2).

### 5.3 What I did NOT measure

- **Episode duration directly.** It is still not logged. §5.2(b) bounds it indirectly and no more.
- **`pullLB` / `postLB` separately.** Only their ratio is logged; the exact-1.0000 mechanism in §4
  is derived from the code's accumulator structure, not observed in the two raw values.
- **Anything on the AWS box.** All AWS facts come from the copied-back book and store.
- **The order in which a break-tol print and the in-band flow arrived** within a window (§5.2's
  group-B control is presence, not sequence) — so 42 % is an **upper** bound on how much of group B
  the break rule legitimately explains.
- **Whether widening the proximity gate's *twin* — the band — is safe.** Not in scope here; it is a
  measurement-shell change and belongs in a spec.

---

## 6. Part 2 — comparison against the proposal

*(Added after §1–§5 were committed.)*
