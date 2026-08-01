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
