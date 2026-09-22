# Fixture parser — THE CLEAN FIRST RUN, 2026-09-22 (UTC)

**Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md). **Spec:** [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md). **Baseline:** [`fixture-parser-20260922T144739Z-baseline.json`](fixture-parser-20260922T144739Z-baseline.json).

⭐⭐ **The first measurement in this programme that satisfies the protocol in full.** Seat-written baseline, written before any detector output existed on this population, same inputs to both judges, `unsure` available and unused because nothing was genuinely unreadable.

---

## 1. Result

| | |
|---|---|
| Items judged | **25** — 19 provenance sites, 6 fixture names |
| **Agreement** | **23 of 25 (92%)** |
| Disagreements | **2** |
| Cost / time | 185,248 input tokens · **$0.0078** · 62.8 s |

---

## 2. ⭐⭐ THE FINDING: every disagreement flagged itself, and STABILITY is what separates them

| Class | Count | Self-consistency |
|---|---|---|
| Agreements | 23 | **All STABLE — 5 of 5 samples, every one** |
| Disagreements | 2 | **Both UNSTABLE — 4 of 5** |

⭐ **Zero confident-and-wrong rows. The detector disagreed with the seat only where it had already told us it was wobbling.**

⛔ **And the separating signal is STABILITY, not the probability band.** `A4_TfiWindowFromEnd`'s name check agreed with the seat at `mean_top_prob` **0.434** — lower than one of the disagreements (0.452). **Top probability overlaps across the two classes; the agreement rate does not.**

**That is a direct vindication of revision 2.** Self-consistency was added because the detector flipped a verdict on identical input; it turns out to be the load-bearing signal, and the `$MIN_TOP_PROBABILITY = 0.60` band would have mis-sorted these rows on its own.

---

## 3. The two disagreements, adjudicated

### 3.1 `A43b_SliceTradesAscendingAndLastN#7407#tfiWindowSize` — **the seat is right**

| | |
|---|---|
| Seat | `undeclared` |
| Detector | `shipped_declared_ok`, agreement **0.8**, `mean_top_prob` **0.40** |
| Evidence | literal `30`, ever-shipped `{30}` — **equals shipped** |
| The comment | *"tfiWindowSize 30 — Pre-existing (NOT one of session 2's 26), swept for context: same band [1, 30] as A4, 31 breaks it. Left untouched."* |

**The block header says `[A54a S2, MECHANISM …]` but this parameter is explicitly excluded from that declaration — "NOT one of session 2's 26".** So the comment gives a band and a history and **never states a class**. Under `CLAUDE.md`'s rule a literal that equals a shipped value and carries no declaration is exactly the confusable case the rule exists to catch. **`undeclared` is the correct and more useful answer.**

⭐ The detector was wrong — **and said so**, at 0.8 agreement and a top probability of 0.40, far below the band.

### 3.2 `A6_ObvNormalisation` name check — **the detector is probably right**

| | |
|---|---|
| Seat | `name_matches` |
| Detector | `name_overclaims`, agreement **0.8**, `mean_top_prob` **0.452** |

The assertion is *"A6 OBV normalisation (first-pair-equal not dead)"* and the body checks `trendA = "RISING" AndAlso trendA = trendB` — **an equality-of-two-paths test. Nothing computes or verifies a normalisation.**

⚠ **The seat recorded its own hesitation on this row at baseline time**, weighing `name_matches` against `ambiguous`. **Both judges wobbled on the same row**, which is the more interesting fact than who won it.

---

## 4. What the agreements were not

⚠ **Not trivial.** Two of the 23 agreed rows have `equals_shipped=True` — `A3#986#microWindowSize` and `A4#1021#tfiWindowSize`, both `30`/`50` against identical shipped values. **Those are the maximally confusable case**, and both judges independently read the comments' explicit "the equality is INCIDENTAL" declarations correctly.

⭐ The detector also reproduced measured value-histories exactly where the comments state them — `trend_gate` `{0.001, 10, 18, 23}` and `slope_pct_of_value` `{0.01, 0.05, 0.1}` both match the 87-revision walk.

---

## 5. ⛔ Declared conflict — read this before quoting the 92%

**The seat reworded 4 of these comment blocks earlier the same day** (`A3` accelThreshold/floorPct, `A4` threshold, `A6` divergenceGate, `A43b` threshold), under the trader-approved past-tense rewording. **So the seat judged prose it had written.**

The detector judged the *same* text, so the comparison stays symmetric — but **4 of 19 provenance rows are not independent of the seat**, and the agreement on those rows is worth less than the others. Declared in the baseline file itself, not discovered afterwards.

---

## 6. What this does and does not establish

**Does:**

- On 25 items with a protocol-clean baseline, seat and detector agree **92%**.
- **Every disagreement was self-flagged as unstable.** For triage — the only role this programme gives a harness — that is the property that matters, because a confident-and-wrong row is the one that would cost something.
- **Stability separates; probability does not.** Use the agreement rate as the gate, not `$MIN_TOP_PROBABILITY`.

**Does not:**

- ⚠ **Generalise from 25 items.** Two disagreements is not a rate.
- ⚠ Establish anything about the **89 out-of-scope sites**. `FP-Q1` excluded them and only the five named proofs were inspected.
- ⚠ Clear `FP-Q1`'s own judgments. Its threshold-or-input calls were never baselined — **that is a second, unmeasured detector inside this harness.**
- ⚠ Say the detector is safe to trust unsupervised. It was wrong on `A43b#7407#tfiWindowSize`, a real rule violation, and only its instability flagged it.

---

## 7. Owed

| # | Item |
|---|---|
| 1 | ⛔ **`A43b#7407#tfiWindowSize` is a genuine finding, not just a test row.** A literal equalling a shipped value with no class declaration — exactly what the fixture-literal provenance rule forbids. Fix the comment or the literal |
| 2 | Consider gating on **agreement rate** rather than `$MIN_TOP_PROBABILITY`, per §2 |
| 3 | Baseline `FP-Q1`'s own scope judgments — the unmeasured detector in §6 |
| 4 | `upgradeBonus` remains excluded as a fixture-builder param though `indicators.OiCvd.upgrade_bonus` is a real key. One site, still unjudged |
