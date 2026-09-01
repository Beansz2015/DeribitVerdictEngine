# Book absorption — mechanism revision (E5 Path B)

**Status:** PROPOSED. **§6 D-table awaits a trader tick.** No code, no settings change.
**Authorised by:** E5 → **Path B**, ticked 2026-08-12. Anchors HOLD at v61; this is the mechanism revision Path B bought.
**Supersedes as the active question:** [`absorption-anchor-rederivation-2026-07-30.md`](absorption-anchor-rederivation-2026-07-30.md) — its §5a V-table is **rejected** and banner-marked as dead text.
**Author:** the orchestrator seat that opened on [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md), 2026-08-14.

> ## ✅ THE BLIND RE-DERIVATION HAS RUN — 2026-08-19. TWO OF THE FOUR LOAD-BEARING CLAIMS DID NOT SURVIVE.
>
> **Independent pass:** [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md), by the incoming orchestrator seat, measured and **committed at `8485173` before this file was opened.**
>
> | Claim | Outcome |
> |---|---|
> | ~10 % ratio stable across books | ✅ **CONFIRMED** independently, +0.03 pp on a second ratio (`active/rows` 14.68 % vs 14.71 %) this pass did not use |
> | biggest loss at the observation stage | ⚠ **TRUE in absolute rows only** — the anchor stages together are proportionally worse (1.7 % vs 10.2 %), and §3.1's *reason* is corrected below |
> | `pullFrac` does **not** pile at 1.000 | ❌ **FALSE — the point mass is there at 200–380× local density on both books.** See §3.2 |
> | §8's residual not diagnosable from the book | ❌ **FALSE — two tests run on the shipped book.** See §4.2 |
>
> ⚠ **Also corrected: §4.3's "~49 % arming rate" is a category error (11.8 %), and §7's "could not separate `window_sec` from the proximity gate" is SUPERSEDED — they separate from logged data, ≈ 77 % geometric.**
>
> **Corrections are recorded in place below, quoted-and-labelled rather than rewritten.** ⚠ **§6 remains UNTICKED — the D-table is still the trader's call.** The independent seat was directed not to tick it and did not.
>
> *Superseded header text follows, per the quote-and-label convention:*
>
> ⚠⚠ **EVERY NUMBER BELOW IS UNVERIFIED BY ANYONE BUT ITS AUTHOR.** One seat wrote the handover, the measurement scripts and this proposal. There is no independent eye anywhere in the chain. [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md) §0 commissions a BLIND re-derivation — a fresh seat measures the population from the raw books and writes its numbers down **before** opening this file, then compares. **Do not treat §2 or §3 as settled until that has run.** Where the two disagree, **the independent number wins** until one method is shown wrong. The four load-bearing claims, listed so they are attacked rather than skimmed: the ~10 % ratio's stability across books · the funnel's biggest loss being at the observation stage · the **falsified** `pullFrac`-piles-at-1.000 hypothesis (if I am wrong there, the D-3 recommendation inverts) · that §8's residual is not diagnosable from logged data at all.

> ⚠ **SESSION BUCKETS — read before comparing anything per-session.** This pass uses the **shipped** buckets: **ASIA 0–7 · LONDON 8–12 · NY 13–23**, matching `settings.json` and the 2026-07-30 pass. ⚠⚠ **The 2026-07-23 derivation used ASIA 22–07 / LONDON 07–13 / NY 13–22**, so **its per-session figures are not comparable to anything here.** That doc's §6 flags the divergence itself; the boundary hours differ, not the whole scheme. **Pooled and per-book totals are unaffected.**

---

## 0. Implementer brief — model, effort, and where it slips

> **Nothing here is buildable yet.** This proposal ends at a D-table. **Do not open an implementer session against it until §6 is ticked** — the whole point of Path B was that the previous pass re-tuned when it should have re-mechanised.
>
> **When it is ticked, the build is: Opus, effort high, one session for §4.1 + §4.3, a separate session for §4.2.**
>
> **Why that tier.** Two of the three revisions change what the tracker *observes*, not what it *scores* — a wrong change is invisible because the failure mode is silence, which is also the correct output most of the time. That is the same shape as the write-guard defect.
>
> ⚠ **Where it will slip.** The tracker is **dual-fed** — folded from `UpdateBook` (~100 ms snapshots) *and* `AppendTrade` (each print), under one `MarketState` lock. **Any window change touches both fold paths and their reset discipline in `SeedAsync`.** A change that widens the trade-side window without widening the book-side trajectory produces a ratio whose numerator and denominator cover different spans — which reads as a *stronger* signal and is pure artefact.

---

## 1. What this proposal is

E5 ruled that **no anchor set reachable from the observable population lands NY in the 3–8 % design band**, so the honest failure is *"the tracker does not observe enough real pressing"*, not *"the thresholds are one tick too high"*. Path B commits to fixing the observation, not the thresholds.

The proposal's own §8 named two residuals. This document **re-measures both on a sample 14× larger than the pass that named them**, adds a third the prior passes did not have, and reports one hypothesis the data **does not** support.

⚠ **`scoring_enabled` stays `false` throughout.** Absorption remains display/CSV-only. Nothing here touches Step 2, so nothing here is a scoring change or a dataset boundary.

---

## 2. The re-count — the population is 14× larger and the finding is unchanged

**Method:** both books, weekday only, UTC, **since v61 shipped (2026-07-23)** because pre-v61 geometry is a different shell. Reported **per book, never pooled** — the two boxes run different session coverage, and pooling would hide which contributed. Episode row = the tracker produced a level.

| | AWS book | Local book | Prior pass (2026-07-30) |
|---|---:|---:|---:|
| Weekday rows since v61 | 14,680 | 5,023 | — |
| Directional | 4,385 | 1,533 | 1,569 |
| **Proximity-ACTIVE episodes** | **2,143** | **737** | **208** |
| **Actually pressed (`aggr > 0`)** | **210** | **80** | **21** |
| **pressed / active** | **9.8 %** | **10.9 %** | 10.1 % |

> ## ⚠⚠ The mechanism ratio is STABLE at ~10 % across a 14× larger sample and two independent boxes.
>
> **2026-07-23: 143 active / 18 pressed = 12.6 %. 2026-07-30: 208 / 21 = 10.1 %. Now: 2,880 / 290 = 10.1 %.**
>
> **This kills the "collect more and it will resolve" hope, which is the only reason Path A was ever arguable.** The under-engagement is **structural**. §5c of the re-derivation warned its 208 rows over 6 weekday days might not be representative — **they were representative, and that is the bad outcome, not the good one.**

---

## 3. The funnel — where it actually dies

Both books pooled **for the funnel only** (a stage-loss decomposition, not a rate), weekday, since v61, against the **shipped v61 anchors** (`absorb_ratio` 1.5 · `min_aggr_usd` 20,000 · `max_pull_frac` 0.75 · `depletion_floor_usd` 5,000):

| Stage | Count | Survives |
|---|---:|---|
| Proximity-ACTIVE episodes | **2,880** | — |
| … actually pressed (`aggr > 0`) | **290** | **10.1 %** |
| … clears `min_aggr_usd` | **28** | |
| … clears `absorb_ratio` | **23** | |
| … clears **both** | **14** | |
| … survives the D8 pull veto | ⚠ **5** | **0.17 % of active** |
| … **killed by D8 despite clearing both anchors** | ⚠ **9** | **64 % of qualifiers** |

**Two losses dominate, and they are at different stages and want different fixes.**

### 3.1 The observation loss — 90 % of episodes never record any pressing

**2,880 → 290.** Nine episodes in ten see the tracker go ACTIVE and record **zero** aggressive volume.

⚠ **The decisive number is not the count, it is the size.** Among rows that *did* press, `aggrUsd` runs **p50 = 220 USD**, p90 = 15,390, max 176,220 — against a `min_aggr_usd` of **20,000**. **The median "pressed" episode records about 1 % of the threshold.**

> ## ⚠⚠ CORRECTED 2026-08-19 — THE SCALE ARGUMENT BELOW USES A MEAN WHERE THE MEDIAN IS 15× SMALLER.
>
> **The rate and the mean check out** (independently measured: ~40 trades/min, mean ≈ 2 900 USD). **The inference from them does not**, because the trade-size distribution is far too skewed for a mean to describe a 10-second window. Measured directly from the `trade_seq`-verified-complete tape era — total market flow, all prints, both directions, all prices, in the 10 s before each of 364 reads:
>
> | p10 | p25 | **p50** | p75 | p90 | p99 |
> |---:|---:|---:|---:|---:|---:|
> | 0 | 10 USD | **1 020 USD** | 17 300 | 64 080 | 210 080 |
>
> **The median 10-second window holds ~1 020 USD, not 15 000–30 000, and a quarter of them are effectively empty.** So a band-and-direction-filtered **220 USD is roughly 22 % of everything that traded in that window** — which is not *"almost nothing lands in the band"*.
>
> ✅ **The conclusion survives; the reason does not.** The real reason is stronger and simpler: **on this tape, 20 000 USD of single-sided in-band flow is not reachable inside 10 seconds except in the top decile of windows.** ⚠ **Anyone repeating the mean-based version will also conclude the band is leaking, and will go on to widen the wrong shell.** Evidence: [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §6.6(b).
>
> ⚠ **A second correction to this subsection: observation is not the only ~90 % stage.** Observation survives **10.2 %** pooled; the `min_aggr_usd` anchor survives **9.5 %**; and the anchor stages *together* take 296 → 5, a **1.7 %** survival — proportionally worse than observation. The absolute-row reading (2 598 lost at observation vs 291 across all anchors) is correct and is the one this section gives; **the proportional reading is the one it omits.** Same source, §6.6(a).
>
> *Superseded text follows, per the quote-and-label convention:*
>
> For scale: the tape's own measured rate is ~31–60 trades/min at a mean trade around 3,000 USD, so a 10-second window spans roughly 5–10 prints ≈ 15,000–30,000 USD **if every one landed in the band**. Recording 220 USD says almost nothing lands in the band inside 10 seconds.
>
> **That is `window_sec = 10` behaving exactly as the 2026-07-23 diagnostic predicted: aggressive prints separated by more than 10 s never sum, so a slow grind against a 5m swing is invisible.**

### 3.2 The D8 loss — the veto kills 64 % of everything that qualifies

Of the 14 episodes clearing both anchors, **9 are vetoed** by `pullFrac > 0.75`. Across all episodes, **66.1 % exceed 0.75**.

⚠⚠ **But I must report a hypothesis of my own that the data DOES NOT support.** The 2026-07-23 diagnostic suggested D8's visibility mask fails to exclude ladder-window shifts, which would show as `pullFrac` piling up at exactly 1.000. **It does not:**

| `pullFrac` | p10 | p25 | p50 | p75 | p90 | p99 |
|---|---:|---:|---:|---:|---:|---:|
| n = 2,880 | 0.000 | 0.003 | **0.906** | 1.204 | 2.682 | 22.180 |

**Exactly 0.000: 24.8 %.** Within 0.001 of 1.000: **only 3.8 %.**

> ## ❌ FALSIFIED IN TURN, 2026-08-19. THE POINT MASS IS THERE. THIS SUBSECTION'S CONCLUSION IS WITHDRAWN.
>
> **The percentiles above are confirmed** — the independent pass reproduces 24.95 % at exactly 0.000 and p50/p75/p90 of 0.90/1.21/2.66. **The reading of "3.8 %" is the error: it was compared against 100 %, when the meaningful comparison is against the local density of the continuum it sits in.**
>
> | | LOCAL | AWS |
> |---|---:|---:|
> | rows at **exactly** `1.0000` | 21 (2.86 %) | 71 (3.29 %) |
> | rows in [0.99, 1.01] **excluding** that exact point | 20 | 64 |
> | ⇒ continuum density per 0.0001 cell | 0.100 | 0.320 |
> | ⭐ **the exact cell holds** | **210×** the local density | **222×** |
> | against the wider [0.90, 1.10] neighbourhood | **328×** | **381×** |
>
> **Both books independently. In a 4-decimal continuous metric, one value holding ~200× its neighbours' density is a point mass, not a shape.** It is also the **second-most-frequent exact logged value on both books** (after `0.0000`), at ~10× the next non-zero mode, and it is **spread rather than clustered** — 9 days / 20 distinct level prices on LOCAL, 15 days / 65 on AWS, against a mean episode-run length of 1.13 rows.
>
> **The mechanism is the one §8 named.** Per conservation interval **only one accumulator increments**. A single visible-size round trip — a block leaving the visibility mask and returning, i.e. **a ladder shift** — writes `−X` to `pullLB` and `+X` to `postLB`, giving **exactly 1.0000 whenever X ≥ 5 000**, an ordinary band size.
>
> ⚠ **What this does and does not change.** The artefact is ~3 % of active rows, so it accounts for only ~3 pp of the veto's ~59 % rejection rate on the population — **so "leave `max_pull_frac` alone" survives as an ACTION.** But *"I went looking for evidence that D8 is broken and did not find it"* is **false**, and at the gate — where it decides flags — **4 of the 10 vetoed rows sit in [0.9990, 1.0250], on the point mass.** ⚠ **This subsection also omits how close the vetoed rows are: one is `0.7615`, rejected by 0.0115.** Pooled what-if on the fifteen rows reaching the gate: `≤0.75` → 5 flags · `≤1.00` → 10 · **`≤1.05` → 13 (2.6×)** — and 13 is still ~0.24 % of directional runs, an order of magnitude below the 3–8 % floor. **It is not the fix; it is not free either, and saying only the first half makes D-3 look better-supported than it is.**
>
> ✅ **D-3 must be re-grounded** on *"the artefact is real but too small to be the fix, and the right response is instrumentation rather than a threshold move"* — same action, from a premise that is true. Evidence: [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §4, §6.3, §6.4.
>
> *Superseded text follows, per the quote-and-label convention:*
>
> ✅ **That is a plausible spoof-metric shape, not an artefact signature** — a quarter of episodes show a sitting defender with no provable pulls, and there is a long right tail. **I went looking for evidence that D8 is broken and did not find it.** What the data shows is narrower and weaker: **`max_pull_frac` = 0.75 sits near the 40th percentile, so the veto is calibrated to reject most episodes.** Whether that is *correct* is a threshold question — and Path B explicitly declined to answer questions by moving thresholds.

---

## 4. The three candidate revisions

### 4.1 `window_sec` — episode-cumulative pressing, not a 10-second rolling sum ⭐ the primary

**The change:** accumulate `aggrUsd` **over the episode**, from proximity-entry to episode end, rather than over a rolling 10-second window.

**Why this and not "make `window_sec` bigger".** A longer rolling window is still a *tolerance* — it picks a duration and hopes it matches how long price presses a level, which varies by session and by regime. **An episode already has natural boundaries the tracker enforces** (proximity entry, break-through, level re-map, reconnect), and those boundaries are exactly the span over which "was this level defended?" is the question. ⚠ **This is the same lesson DR-1 applied one seam over: prefer the structural boundary to the time tolerance.**

⚠ **The trap, and it is the one that makes this Opus/high:** `absorbRatio = aggrUsd / max(sizeStart − sizeMin, floor)`. The numerator becomes episode-cumulative; **the denominator is already episode-scoped** (`sizeStart` at episode start, `sizeMin` over the episode). ✅ **So this change makes the two consistent — today they are not.** A 10-second numerator over an episode-long denominator is dimensionally incoherent, and it biases the ratio **down**, which is the direction that suppresses the signal.

**Expected effect: unquantified, deliberately.** Episode duration is not logged (§5), so the multiplier cannot be estimated from the book. **Do not let anyone put a projected flag rate on this without that instrumentation.**

### 4.2 D8 `pullFrac` on sparse `postLB` — instrument before touching

**The named residual:** `pullFrac = pullLB / max(postLB, depletion_floor_usd)` inflates when `postLB` is small.

> ## ❌ CORRECTED 2026-08-19 — IT IS DIAGNOSABLE, AND IT IS DIAGNOSED. TWO TESTS RAN ON THE SHIPPED BOOK.
>
> **Test 1 — the ladder-shift half.** The point mass at exactly `1.0000` is directly visible in the logged ratio, at 200–380× local density on both books. See §3.2's correction box.
>
> **Test 2 — the sparse-`postLB` half, and this one is decisive.** When `postLB < 5 000` the denominator pins to the constant, so `pullFrac` becomes `pullLB / 5000` — **an absolute quantity wearing a fraction's clothes, which is precisely the named residual.** Book sizes and fills on this instrument are multiples of 10 USD, so `pullLB` is too, so **a floored row must land on an exact multiple of 0.0020**; a genuine ratio of two independent sums has no reason to.
>
> | | LOCAL | AWS | chance |
> |---|---:|---:|---:|
> | non-zero `pullFrac` rows | 534 | 1 638 | — |
> | …an exact multiple of **0.0020** | **147 (27.5 %)** | **437 (26.7 %)** | **5 %** |
> | …an exact multiple of 0.0010 | 169 (31.6 %) | 496 (30.3 %) | 10 % |
>
> **A 5.4× enrichment over chance on both books**, and 88 % of the multiples-of-0.0010 are also multiples of 0.0020 where half would be by chance. ⭐ **The floor binds on roughly a quarter of non-zero rows — so for a quarter of the population `pullFrac` is not a fraction at all, and comparing it to `max_pull_frac` = 0.75 is comparing it to a fixed 3 750 USD of pulls.**
>
> ⚠ **This is the one place D-1 changes.** §5's instrumentation is still worth shipping — raw `pullLB`/`postLB` turn a fingerprint into a direct reading, and `AbsorptionEpisodeSec` has no substitute. **But D-1 is no longer the precondition for knowing whether this residual is real. It is real, and it is measured on the shipped book.** Evidence: [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §6.5.
>
> *Superseded text follows, per the quote-and-label convention:*
>
> ⚠⚠ **This is NOT diagnosable from the book, and that is itself the finding.** The CSV logs `AbsorptionPullFrac` — the **ratio** — and neither `pullLB` nor `postLB`. **So the one hypothesis §8 names cannot be tested against 2,880 logged episodes.** §5 proposes the fix.

**Do not change `max_pull_frac`.** That is a threshold move, which Path B rejected. ⚠ **The second half of that sentence — *"and §3.2 shows the metric's shape is not obviously broken"* — is withdrawn 2026-08-19; the shape IS broken in two identified ways. The instruction stands on the first half alone.**

### 4.3 The proximity gate — measure it, do not assume it

⚠ **Named because the funnel points at it and nobody has looked.** The 90 % observation loss is currently attributed entirely to `window_sec`, but an episode that goes ACTIVE and records nothing could equally be a **proximity gate that is too wide** — arming on approaches that were never going to become a test.

> ## ⭐ ANSWERED 2026-08-19 — AND THE ARMING-RATE FIGURE BELOW IS AN ARITHMETIC ERROR.
>
> **(a) The separation did not need §5's instrumentation.** `AbsorptionLevel`, `Price` and `ATR` are all already logged, so `|level − price| / ATR` is computable on every active row today. **`proximity_atr_frac` 0.30 admits an episode; `band_atr_frac` 0.10 is what a print must land inside to count. The admission shell is 3× the measurement shell.**
>
> | `abs(level − price) / ATR` | LOCAL share / press rate | AWS share / press rate |
> |---|---:|---:|
> | **≤ 0.10 — inside the pressing band** | 29.7 % / **31.7 %** | 29.9 % / **30.3 %** |
> | (0.10, 0.30] — inside proximity, outside the band | 45.0 % / 2.7 % | 47.0 % / 1.6 % |
> | > 0.30 | 25.3 % / 0.7 % | 23.1 % / 0.8 % |
>
> **77.3 % (LOCAL) / 76.8 % (AWS) of unpressed active rows had price outside the band.** ⭐ **≈ 77 % of the observation loss is GEOMETRIC, on both books independently.** Only the remaining ≈ 23 % is a case `window_sec` can address.
>
> **(b) A third cause, larger than `window_sec`, that was on neither list.** Replaying the tracker's own predicate over the verified-complete tape era (134 204 rows; alignment validated to +2 s, all 22 logged-pressed rows reproduced, 12 of 22 to within 1 USD): **the engine logs pressing on 22 rows where its own band-and-window definition admits flow on 72 — it counts 31 % of what it should.** Only 42 % of the 50-row gap has a break-through print to justify it, and **13 of those 50 carried ≥ 20 000 USD**, against just 19 rows clearing `min_aggr_usd` in the whole 17-day AWS book. **The side is not `Active` when the flow arrives.** ⚠ **Consequence for D-2: an episode-cumulative numerator accumulates over the same gap, so §4.1 is worth less than a widening table suggests.**
>
> **(c) ⚠⚠ "~49 % arming rate" is a category error.** 2 880 is every active episode across **all 19 679 rows**; 5 918 is a ~30 % **subset** of those rows. The quotient is not a rate of anything. Computed consistently: **active / all rows = 14.71 %**, or **active-and-directional / directional = 11.78 % (386 / 3 276)**. ⚠ **The denominator is wrong a second way** — 5 918 counts `NO TRADE [WEAK LONG]` / `NO TRADE [WEAK SHORT]` / `NO TRADE [TIE]` as directional; the strict count is 3 276. *(The checking seat made the same mistake on its first pass and records it in its own §1.)*
>
> ✅ **D-4's action still stands — do not narrow the proximity gate on a hunch — but not for D-4's reason.** The arming rate is unremarkable. **The proximity gate's problem is not that it arms too often; it is that it arms 3× wider than the shell that measures.** Those want different fixes. Evidence: [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §5, §6.7, §6.8.
>
> *Superseded text follows, per the quote-and-label convention:*
>
> **2,880 ACTIVE episodes against 5,918 directional rows is a ~49 % arming rate.** For a feature whose modal state is meant to be NONE by construction, that is high. **The instrumentation in §5 separates the two explanations; do not guess between them.**

---

## 5. What must be logged before any of this can be judged

⚠ **This is the smallest and highest-value piece of work in the proposal, and it should ship first.**

| Field | Why |
|---|---|
| `AbsorptionEpisodeSec` | ⚠ **The single most valuable missing number.** Without it, §4.1's effect cannot be estimated and §4.3 cannot be separated from §4.1 |
| `AbsorptionPullLB` · `AbsorptionPostLB` | Makes §4.2's named residual testable. Today only their ratio survives |
| `AbsorptionSizeStart` · `AbsorptionSizeMin` | The ratio's denominator, currently invisible — so a low ratio cannot be attributed to weak pressing vs deep replenishment |

⚠ **A CSV column addition is a rotation and it has a cost.** The trade-identity build established that appending is backward-compatible where the reader guards on `<` rather than `=`; **`analysis_log.csv` is a different reader and that property must be re-verified, not assumed.**

---

## 6. D-table — awaits a trader tick

⚠ **STILL UNTICKED. The 2026-08-19 blind check did NOT tick this table** — it was directed not to, and did not. **The rows below carry the author's recommendation plus, where the check moved it, a second column.** The right-hand column is the checking seat's read; it is not a decision.

| # | Decision | Author's recommendation | ⚠ After the 2026-08-19 check |
|---|---|---|---|
| **D-1** | Ship the §5 instrumentation **first, alone**, and re-read after ~2 weekday-weeks | ✅ **Yes.** Every other decision here is unmeasurable without it, and it is display-only | ⚠ **The premise is now partly false — it was NOT all unmeasurable.** ≈ 77 % of the observation loss and both halves of §4.2's residual were measured from the shipped book (§4.3, §4.2 boxes). **`AbsorptionEpisodeSec` still has no substitute** and raw `pullLB`/`postLB` still upgrade a fingerprint to a reading, so the row survives — but **not as a blocker on the other rows** |
| **D-2** | `window_sec` → **episode-cumulative pressing** (§4.1) | ✅ **Yes** — but **after D-1 reads**, so the effect is measured rather than projected | ⚠ **Do not tick this as though the span were the whole problem.** The engine counts only **31 %** of the in-band flow its own 10 s window already admits (§4.3 box (b)) — a cumulative numerator accumulates over the same gap. **The span is the third-largest of three causes**, behind geometry and episode state |
| **D-3** | Leave `max_pull_frac` and every other anchor **untouched** | ✅ **Yes.** §3.2 found no evidence D8 is broken, and moving it is the re-tuning Path B rejected | ⚠ **ACTION SURVIVES, REASON DOES NOT.** There *is* evidence D8 is broken — a 200–380× point mass at exactly 1.000 plus a floored `postLB` on ~27 % of non-zero rows. **Re-ground on: the artefact is real but too small to be the fix (≈3 pp of a 59 % veto), so the response is instrumentation, not a threshold move.** ⚠ Note the veto costs more than §3.2 shows: **10 of the 15 gate-reaching rows, one by 0.0115** |
| **D-4** | Treat the ~49 % arming rate as an **open question**, not a defect | ✅ **Yes** — §4.3. Do not narrow the proximity gate on a hunch | ⚠ **ACTION SURVIVES, PREMISE IS AN ARITHMETIC ERROR.** The rate is **11.78 %**, not 49 % (§4.3 box (c)). **The gate's problem is not how often it arms — it is that it arms 3× wider than the shell that measures.** Those want different fixes, and this row should say which one it is deferring |
| **D-5** | Keep `scoring_enabled = false` and the §5.1/§5.2 activation gates **unchanged** | ✅ **Yes.** The n ≥ 30 flagged-evaluated-rows gate stands; this proposal does not touch it | ✅ **Unchanged by the check.** ⚠ Worth stating plainly alongside it: because `scoring_enabled` is false and the failure mode is **silence**, a build that underperforms will not announce it — the write-guard shape this proposal's own §0 warns about, pointed at itself |
| ⚠ **D-6** | ⚠ **NEW, raised by the 2026-08-19 check — reconcile `proximity_atr_frac` (0.30) with `band_atr_frac` (0.10), or state why a 3× mismatch is intended** | *(no author recommendation — the author did not have this measurement)* | ⚠ **This is the largest single leak in the funnel and it is not currently on the table.** ≈ 77 % of the observation loss is episodes admitted into a shell three times wider than the shell that measures. **Not a threshold re-tune** — it is a shape question, the same class as §4.1 |


⚠ **If D-1 is declined, the honest consequence is that §4.1 ships blind** — buildable, but with no way to say afterwards whether it worked. Say so explicitly rather than proceeding quietly.

> ## ⛔ D-6 HAS A SPEC-BACK — READ IT BEFORE TICKING THAT ROW. Added 2026-09-01.
>
> **[`absorption-d6-spec-back.md`](absorption-d6-spec-back.md)** — raised by the trader's question *"can this measurement be done?"* against D-6's *"the author did not have this measurement"*.
>
> **Nothing in the D-table is rewritten by it. Nothing is ticked by it.** Its three load-bearing claims, so they are attacked rather than skimmed:
>
> | | Claim |
> |---|---|
> | **1** | **D-6 is three questions with three different verdicts, so no single tick can be right.** One is already measured, one is answerable from [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) with no measurement at all, one is not measurable from any data this project holds |
> | **2** | ⛔ **D-6's right-hand column claim — "the largest single leak in the funnel" — is DISPUTED.** The annulus (0.10, 0.30] presses at 2.6–2.8 % on both books, so collapsing the shells recovers almost nothing. **The ≈ 77 % is a share of unpressed active rows, not of recoverable pressing** |
> | **3** | ⭐ **The 0.30 / 0.10 pair is arm-early / measure-tight BY CONSTRUCTION.** The episode arms on proximity (`:251`) but `SizeStart` samples the band (`:295`). **The wide shell captures the band's depth before price arrives.** So "a 3× mismatch" is a misnomer, and D-6's second branch can be answered today |
>
> ✅ **§4.3 box (a)'s geometry table was independently reproduced on BOTH books** by that pass, on longer books than this one used. **That table stands.**
>
> ⚠ **§5's "no substitute for `AbsorptionEpisodeSec`" was tested and SURVIVES** — 89.3 % (AWS) / 89.6 % (LOCAL) of reconstructed episodes are a single CSV row, so the book cannot resolve episode life at all.

---

## 7. What I did not verify

- ⛔ **Episode duration, anywhere.** It is not logged. **Every statement in §4.1 about *why* 10 seconds is short is arithmetic from tape rate and band membership, not a measurement of how long episodes actually run.**
- ~~⛔ **Whether the 90 % observation loss is `window_sec` or the proximity gate.** §4.3 exists because I could not separate them, and §5 is what would.~~ ⭐ **SEPARATED 2026-08-19 from logged data — ≈ 77 % geometric, plus a third cause (episode state) larger than the window.** See §4.3's box.
- ~~⛔ **`pullLB` / `postLB` behaviour.** Not logged. §4.2's residual is untested — I can only report that its *symptom* (piling at 1.000) is absent.~~ ❌ **BOTH HALVES OF THIS ARE NOW WRONG.** The symptom is **present** (§3.2 box) and the residual **is tested** (§4.2 box). ⚠ **The raw values are still not logged**, so the ladder-shift mechanism is inference from a signature rather than a direct reading — that much of the caveat stands.
- ⛔ **Any outcome linkage.** This proposal measures engagement only. **The §5.2 outcome gradient — a ≥10 pp worse success rate on n ≥ 30 flagged evaluated rows — has never had a population to run against, and nothing here changes that.**
- ⛔ **The tape era split.** The counts above come from `analysis_log.csv`, which is unaffected by the trade-store eras. ⚠ **But any future absorption work that re-derives pressing from the raw tape must split on 2026-08-11 17:18** — before it, roughly half the tape is missing and biased toward dropping the later legs of sweeps, which is exactly the flow absorption measures.
- ⛔ **Anything live.** No run was performed; all figures are from the two logged books.
