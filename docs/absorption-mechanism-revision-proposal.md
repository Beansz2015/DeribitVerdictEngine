# Book absorption — mechanism revision (E5 Path B)

**Status:** PROPOSED. **§6 D-table awaits a trader tick.** No code, no settings change.
**Authorised by:** E5 → **Path B**, ticked 2026-08-12. Anchors HOLD at v61; this is the mechanism revision Path B bought.
**Supersedes as the active question:** [`absorption-anchor-rederivation-2026-07-30.md`](absorption-anchor-rederivation-2026-07-30.md) — its §5a V-table is **rejected** and banner-marked as dead text.
**Author:** the orchestrator seat that opened on [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md), 2026-08-14.

> ## ⚠⚠ EVERY NUMBER BELOW IS UNVERIFIED BY ANYONE BUT ITS AUTHOR.
>
> **One seat wrote the handover, the measurement scripts and this proposal. There is no independent eye anywhere in the chain.** [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md) **§0 commissions a BLIND re-derivation** — a fresh seat measures the population from the raw books and writes its numbers down **before** opening this file, then compares.
>
> **Do not treat §2 or §3 as settled until that has run.** Where the two disagree, **the independent number wins** until one method is shown wrong.
>
> ⚠ **The four load-bearing claims, listed so they are attacked rather than skimmed:** the ~10 % ratio's stability across books · the funnel's biggest loss being at the observation stage · the **falsified** `pullFrac`-piles-at-1.000 hypothesis (if I am wrong there, the D-3 recommendation inverts) · that §8's residual is not diagnosable from logged data at all.

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

For scale: the tape's own measured rate is ~31–60 trades/min at a mean trade around 3,000 USD, so a 10-second window spans roughly 5–10 prints ≈ 15,000–30,000 USD **if every one landed in the band**. Recording 220 USD says almost nothing lands in the band inside 10 seconds.

**That is `window_sec = 10` behaving exactly as the 2026-07-23 diagnostic predicted: aggressive prints separated by more than 10 s never sum, so a slow grind against a 5m swing is invisible.**

### 3.2 The D8 loss — the veto kills 64 % of everything that qualifies

Of the 14 episodes clearing both anchors, **9 are vetoed** by `pullFrac > 0.75`. Across all episodes, **66.1 % exceed 0.75**.

⚠⚠ **But I must report a hypothesis of my own that the data DOES NOT support.** The 2026-07-23 diagnostic suggested D8's visibility mask fails to exclude ladder-window shifts, which would show as `pullFrac` piling up at exactly 1.000. **It does not:**

| `pullFrac` | p10 | p25 | p50 | p75 | p90 | p99 |
|---|---:|---:|---:|---:|---:|---:|
| n = 2,880 | 0.000 | 0.003 | **0.906** | 1.204 | 2.682 | 22.180 |

**Exactly 0.000: 24.8 %.** Within 0.001 of 1.000: **only 3.8 %.**

✅ **That is a plausible spoof-metric shape, not an artefact signature** — a quarter of episodes show a sitting defender with no provable pulls, and there is a long right tail. **I went looking for evidence that D8 is broken and did not find it.** What the data shows is narrower and weaker: **`max_pull_frac` = 0.75 sits near the 40th percentile, so the veto is calibrated to reject most episodes.** Whether that is *correct* is a threshold question — and Path B explicitly declined to answer questions by moving thresholds.

---

## 4. The three candidate revisions

### 4.1 `window_sec` — episode-cumulative pressing, not a 10-second rolling sum ⭐ the primary

**The change:** accumulate `aggrUsd` **over the episode**, from proximity-entry to episode end, rather than over a rolling 10-second window.

**Why this and not "make `window_sec` bigger".** A longer rolling window is still a *tolerance* — it picks a duration and hopes it matches how long price presses a level, which varies by session and by regime. **An episode already has natural boundaries the tracker enforces** (proximity entry, break-through, level re-map, reconnect), and those boundaries are exactly the span over which "was this level defended?" is the question. ⚠ **This is the same lesson DR-1 applied one seam over: prefer the structural boundary to the time tolerance.**

⚠ **The trap, and it is the one that makes this Opus/high:** `absorbRatio = aggrUsd / max(sizeStart − sizeMin, floor)`. The numerator becomes episode-cumulative; **the denominator is already episode-scoped** (`sizeStart` at episode start, `sizeMin` over the episode). ✅ **So this change makes the two consistent — today they are not.** A 10-second numerator over an episode-long denominator is dimensionally incoherent, and it biases the ratio **down**, which is the direction that suppresses the signal.

**Expected effect: unquantified, deliberately.** Episode duration is not logged (§5), so the multiplier cannot be estimated from the book. **Do not let anyone put a projected flag rate on this without that instrumentation.**

### 4.2 D8 `pullFrac` on sparse `postLB` — instrument before touching

**The named residual:** `pullFrac = pullLB / max(postLB, depletion_floor_usd)` inflates when `postLB` is small.

⚠⚠ **This is NOT diagnosable from the book, and that is itself the finding.** The CSV logs `AbsorptionPullFrac` — the **ratio** — and neither `pullLB` nor `postLB`. **So the one hypothesis §8 names cannot be tested against 2,880 logged episodes.** §5 proposes the fix.

**Do not change `max_pull_frac`.** That is a threshold move, which Path B rejected, and §3.2 shows the metric's shape is not obviously broken.

### 4.3 The proximity gate — measure it, do not assume it

⚠ **Named because the funnel points at it and nobody has looked.** The 90 % observation loss is currently attributed entirely to `window_sec`, but an episode that goes ACTIVE and records nothing could equally be a **proximity gate that is too wide** — arming on approaches that were never going to become a test.

**2,880 ACTIVE episodes against 5,918 directional rows is a ~49 % arming rate.** For a feature whose modal state is meant to be NONE by construction, that is high. **The instrumentation in §5 separates the two explanations; do not guess between them.**

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

| # | Decision | My recommendation |
|---|---|---|
| **D-1** | Ship the §5 instrumentation **first, alone**, and re-read after ~2 weekday-weeks | ✅ **Yes.** Every other decision here is unmeasurable without it, and it is display-only |
| **D-2** | `window_sec` → **episode-cumulative pressing** (§4.1) | ✅ **Yes** — but **after D-1 reads**, so the effect is measured rather than projected |
| **D-3** | Leave `max_pull_frac` and every other anchor **untouched** | ✅ **Yes.** §3.2 found no evidence D8 is broken, and moving it is the re-tuning Path B rejected |
| **D-4** | Treat the ~49 % arming rate as an **open question**, not a defect | ✅ **Yes** — §4.3. Do not narrow the proximity gate on a hunch |
| **D-5** | Keep `scoring_enabled = false` and the §5.1/§5.2 activation gates **unchanged** | ✅ **Yes.** The n ≥ 30 flagged-evaluated-rows gate stands; this proposal does not touch it |

⚠ **If D-1 is declined, the honest consequence is that §4.1 ships blind** — buildable, but with no way to say afterwards whether it worked. Say so explicitly rather than proceeding quietly.

---

## 7. What I did not verify

- ⛔ **Episode duration, anywhere.** It is not logged. **Every statement in §4.1 about *why* 10 seconds is short is arithmetic from tape rate and band membership, not a measurement of how long episodes actually run.**
- ⛔ **Whether the 90 % observation loss is `window_sec` or the proximity gate.** §4.3 exists because I could not separate them, and §5 is what would.
- ⛔ **`pullLB` / `postLB` behaviour.** Not logged. §4.2's residual is untested — I can only report that its *symptom* (piling at 1.000) is absent.
- ⛔ **Any outcome linkage.** This proposal measures engagement only. **The §5.2 outcome gradient — a ≥10 pp worse success rate on n ≥ 30 flagged evaluated rows — has never had a population to run against, and nothing here changes that.**
- ⛔ **The tape era split.** The counts above come from `analysis_log.csv`, which is unaffected by the trade-store eras. ⚠ **But any future absorption work that re-derives pressing from the raw tape must split on 2026-08-11 17:18** — before it, roughly half the tape is missing and biased toward dropping the later legs of sweeps, which is exactly the flow absorption measures.
- ⛔ **Anything live.** No run was performed; all figures are from the two logged books.
