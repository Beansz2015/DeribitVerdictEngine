# F1 — tier-ladder re-read at n≥150 STRONG (2026-08-01)

**From:** the incoming orchestrator seat. **Run because the blocker was already gone** — the systematic sweep found the pooled-file report runner shipped 2026-07-30 with the stated purpose *"unblock the F1 §9 read"*, while the queue still carried E1 as blocked on that mechanics decision.
**Instrument:** `BacktestRunner report --csv <pooled>` — the shipped `analysis/AnalysisRunner` pipeline, §9 band ladder, which exists specifically so this re-read *"has a place to live off the offline report."*
**Input:** pooled AWS-preferred book (2026-07-31 dedup ruling), frozen, 14,104 rows, 2026-07-03 → 2026-07-31. Placed-vs-placed barriers, per-population tracker horizon (res-1 → 15 m, res-3 → 45 m), LONG+SHORT pooled per band — a ladder read, not a direction read.

> ## VERDICT: the count gate is met (203 pooled STRONG ≥ 150) and **the ladder does not separate.**
> STRONG edges MEDIUM pooled by 4.6 pp, **MEDIUM and WEAK are indistinguishable (42.2 % vs 42.7 %)**, every CI overlaps, and **LONDON inverts at the top** — MEDIUM 53.0 % beats STRONG 44.8 %. This is a *no* to F1's question, and it lands directly on Kelly CAL and the P5 tier values.

---

## 1. The ladder

**POOLED · per-row horizon** — the gate population:

| Band | n | Success | 95 % CI |
|---|---:|---:|---|
| STRONG | **203** | **46.8 %** | [40–54] |
| MEDIUM | 785 | 42.2 % | [39–46] |
| WEAK | 1,805 | 42.7 % | [40–45] |

Per session:

| | STRONG | MEDIUM | WEAK | shape |
|---|---|---|---|---|
| **NY×1** (15 m) | 44.6 % (n=121) | **37.9 %** (n=509) | 42.1 % (n=1131) | non-monotonic — MEDIUM is the *worst* band |
| **LONDON×3** (45 m) | 44.8 % (n=67) | **53.0 %** (n=151) | 40.4 % (n=356) | **inverted at the top** |
| **ASIA×3** (45 m) | 73.3 % (n=15) | 46.4 % (n=125) | 47.2 % (n=318) | STRONG n=15, CI [48–89] — unusable |

**What survives:** STRONG > MEDIUM pooled (+4.6 pp) and in NY (+6.7 pp). That is the only part of the ladder that behaves as designed, and its CI still overlaps MEDIUM's.

**What does not:** MEDIUM vs WEAK is a coin flip pooled (42.2 vs 42.7, CIs nested). NY's MEDIUM is *below* its WEAK. LONDON's MEDIUM is *above* its STRONG. ASIA's STRONG is 15 rows.

---

## 2. Consequences — this is why F1 gated two things

**Kelly CAL (E1 → W6-3/L4).** CAL mode exists to replace the EST tier-scale with **empirical per-tier win rates**. Those rates are the table above. Feeding them in would assign p(win) ≈ 0.47 / 0.42 / 0.43 across STRONG / MEDIUM / WEAK — i.e. **near-identical sizing across tiers**, with the ordering partly inverted per session. That is not a calibration; it is noise encoded as signal, and it would be *worse* than the current EST ladder because it carries false authority.

**My read: Kelly CAL should not ship on this book.** The honest options are (a) keep EST and say why, (b) collapse to a single pooled p(win) and stop pretending tiers size differently, or (c) wait for separation to appear. **This is a decision for the trader and it should be taken explicitly rather than by CAL quietly not shipping.**

### 2.1 The arithmetic, worked — it collapses those three options (added 2026-08-02)

`CalcKellySizing` uses `b = atr_target_multiplier / atr_stop_multiplier` = **1.75 / 1.6 = 1.0938**, and `f* = (b·p − q)/b` with a silent block at `f* ≤ 0`. So **breakeven is p = 1/(1+b) = 47.76 %**.

| source | p | f* | applied |
|---|---:|---:|---|
| EST HIGH | 0.650 | +0.3300 | half 0.165 → **capped 5 %** |
| EST MEDIUM | 0.550 | +0.1386 | half 0.069 → **capped 5 %** |
| EST LOW | 0.450 | −0.0529 | suppressed |
| **F1 STRONG measured** | 0.468 | **−0.0184** | **suppressed** |
| **F1 MEDIUM measured** | 0.422 | **−0.1065** | **suppressed** |
| **F1 pooled STRONG+MED** | 0.431 | **−0.0892** | **suppressed** |

**Two consequences.**

1. **EST's tier ladder is already invisible in the output.** HIGH and MEDIUM both exceed the 5 % cap after halving, so they produce **identical applied sizing**; the tier survives only in the `KellyCapped` flag. EST today is effectively *"5 % on anything HIGH or MEDIUM, nothing on LOW"* — so options (a) and (b) are less different than they sound.
2. **Every measured rate is below breakeven, so any honest calibration blanks the block** — permanently, on every tier. "Collapse to one measured p" and "turn Kelly off" are the same thing at current numbers.

**Is `b` the problem? Checked, and no.** I hypothesised the hardcoded ATR-basis `b` understates the placed geometry. Measured from logged `PlacedTarget*`/`PlacedStop*` (post-07-08, weekday, directional, n=2,512): **pooled b_p50 = 1.094 — essentially identical to the hardcoded 1.0938.** The hypothesis does not hold and the conclusion stands on the geometry the engine actually places.

**But `b` varies by session while Kelly uses one global constant, and that is worth its own look:**

| | n | b_p50 | b_mean | breakeven p |
|---|---:|---:|---:|---:|
| NY | 1,500 | 1.094 | 1.266 | 47.8 % |
| LONDON | 556 | **1.250** | 1.371 | 44.4 % |
| **ASIA** | 456 | **0.781** | 1.154 | **56.1 %** |

**ASIA's placed R:R is below 1 at the median** — the median ASIA setup risks more than it stands to make, needing a 56 % hit rate to break even. That is independent of the Kelly decision and flagged on its own account. (Mean exceeds median everywhere — right-skewed by a few far structural targets.)

At the realised per-session `b`, **no session's measured rate clears breakeven except ASIA at n=15**, which is unusable. So this is not a choice between three probabilities; it is that **the book does not currently demonstrate a Kelly-sizeable edge on the ATR-basis payoff Kelly uses.**

### 2.2 Does the `b` result settle option (c), "wait for separation"? **No — and the two tests do not share an n** (added 2026-08-02, trader question)

They are different halves of the same fraction: **`b` is the denominator (payoff), the ladder is the numerator (win rate).** The `b` test rests on **n=2,512** placed-geometry rows and is settled. The ladder rests on **n=203** evaluable STRONG with a ±7 pp CI and is not. Twelve-fold difference in evidence, and the larger number belongs to the question that was already answerable.

**What the `b` test did do is close the cheap escape.** Had `b` been understated, Kelly would have been fixable by correcting the payoff — no waiting required. That route is gone, so option (c)'s value now rests entirely on the win rates themselves moving.

**And they might, because the CI spans the entire decision space.** f* across F1 STRONG's interval, at b = 1.0938:

| p | f* | half-Kelly | applied |
|---:|---:|---:|---|
| 0.400 *(CI low)* | −0.1486 | — | SUPPRESSED |
| **0.468** *(point est.)* | **−0.0184** | — | **SUPPRESSED** |
| 0.478 | +0.0007 | 0.0004 | breakeven |
| 0.490 | +0.0237 | 0.0119 | 1.19 % |
| 0.500 | +0.0429 | 0.0214 | 2.14 % |
| 0.520 | +0.0811 | 0.0406 | 4.06 % |
| 0.540 *(CI high)* | +0.1194 | 0.0597 | **CAPPED 5 %** |

**The interval runs from "suppressed" to "capped at 5 %" — the whole range.** So the measurement cannot presently distinguish *no edge* from *full edge*. That is **underpowered, not null**, and it is the same shape as W6-4's INCONCLUSIVE: the honest verdict is that the instrument can't resolve the question yet, not that the answer is known.

**But note how steep the curve is just above breakeven.** The point estimate sits **1.0 pp below** breakeven, and even a favourable resolution buys little: 49 % → 1.2 %, 50 % → 2.1 %. **You need ≈54 % — the very top of the CI — to justify the 5 % EST displays today.** So the realistic good outcome from waiting is a *small* Kelly fraction, not a vindication of the current number.

**Which is what actually decides it.** Waiting is defensible on information grounds. **Waiting while EST displays 5 % is not** — 5 % is the extreme optimistic end of the plausible range, and the point estimate says suppressed. **Refined recommendation: take option (c) for the measurement and fix the display now** — either render the assumption honestly (`p assumed, not measured`) or suppress with a stated reason. The two are separable decisions and only the first needs more data.

**P5 tier values (order-app session policy).** P5 selects which tiers the consumer acts on. On this evidence, tier-based selection has no demonstrated basis pooled, and **the LONDON inversion actively contradicts a STRONG-only policy for that session.** The prior [D7 re-read](d7-confirmed-reread-2026-07-22.md) already established that the *context-tag* inversion was an artifact; this is the *tier* ladder, and it is a different measurement that does not resolve the same way.

---

## 3. This is the third instrument saying the same thing

Recorded together because they were produced independently, within a day, on different code paths:

- **W6-4 ceiling audit** (2026-08-01): the engine's own score ranks outcomes at **AUC 0.5407** on NY — barely above chance — and an L2 logistic with every logged feature could not beat it.
- **F1 §9** (this doc): the tier bands *derived from that score* do not separate.
- **W6-1 / ceiling informational column**: `TargetCapReason (structural fired)` univariate AUC **0.4683 / 0.5000 / 0.3221** — structural placement is not predictive either.

**One coherent picture: the pipeline's discrimination is weak on the placed-geometry yardstick, at both the score level and the geometry level.** None of the three is individually decisive — all are small-n, touch-based, no-slippage — but they are consistent, and they were not designed to corroborate each other.

**What this does NOT say.** Not that the engine is unprofitable — success here is favourable-barrier-before-adverse on touch, over *all* directional rows, and the trader acts discretionarily on a subset. Not that the tiers are meaningless live — the bridge refuses WEAK and the strip excludes it, so the traded population is STRONG+MEDIUM, where the pooled gap is +4.6 pp in the right direction. It says the *evidence for the ladder* is thin, which is exactly the question F1 asked.

---

## 4. What I did not verify

- **No re-run at other horizons.** The §9 method fixes horizon per population (15 m / 45 m). Whether the ladder separates at a different hold horizon is §4's question, not this one, and sweeping horizons to find separation would be the multiple-comparisons failure the W6-4 method guards against.
- **B1's fix is visible and clean here:** `No-data excl. = 0` on all three populations, so no fabricated expiries contaminate these rates. That is the F4 fix working, confirmed incidentally.
- **Not joined to realised trades.** No slippage, no queue position, no fills — the standing caveat on every offline surface in this project.
- **ASIA STRONG is 15 rows** and is reported only for completeness; nothing should be concluded from 73.3 % [48–89].
- **I did not re-run the D7 context-tag read.** It resolved as an artifact on 2026-07-22 and nothing here re-opens it; the tier ladder and the context tag are different axes.
