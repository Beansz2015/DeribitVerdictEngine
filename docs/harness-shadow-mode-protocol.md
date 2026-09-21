# Jev harness shadow-mode protocol

**Created 2026-09-21 (UTC).** ⭐ **TRADER-RULED 2026-09-21 (UTC): every Jev harness gets a HAND-WRITTEN baseline on its FIRST real run, written BEFORE the detector runs, and the two are compared and recorded.**

**Scope: every harness in the Jev programme**, not the rider check alone. A seat building the second or fifth harness must find this rule without having read the first one's spec.

---

## 0. The ruling, and why it is not optional

> **Write by hand and then measure the detector against your pass — definitely need to do this on the 1st run.** — trader, 2026-09-21 (UTC)

⛔ **The first real run is the ONLY chance to measure a harness.** Once a seat has seen the detector's output on a body of data, it can never again produce an uncontaminated read of that same data. There is no second first run. A harness that skips this is not "validated later" — it is **unvalidated for ever**, and it then sits in `tools/` looking like a check.

⭐ **This is the whole reason shadow mode was chosen over dedicated test suites.** The trade accepted was: give up purpose-built labelled datasets, and buy the measurement back from the first live run instead. **Skipping the baseline does not save time — it forfeits the thing the design was paying for.**

---

## 1. ⚠ `N=1` is one OCCASION, not one row

A correction worth carrying, because it changes what run 1 is worth:

**Most of these harnesses judge a large population in a single run, so run 1 IS a real measurement.**

| Harness | Rows in one run | What run 1 yields |
|---|---|---|
| Rider travel | 8 riders — but that is the entire population | Adequate for the decision it feeds |
| Commit walker | about 200 commits, roughly 24 positives | **Real catch rate** |
| Fixture parser | 425 fixtures | **Real catch rate** |
| Doc scanner | about 380 docs, many pairs | **Real catch rate** |
| Doc re-ranker | one query | ⚠ Genuinely one data point. Stays a trial, judged over sessions |

So the baseline is cheap relative to what it buys on four of the five.

---

## 2. The protocol

1. **Build the harness so it REFUSES to call the API without a baseline file.** Structural, not a convention — a safety held by a comment is the defect class this repo already rejects. Worked example: [`rider-travel-check-spec.md`](rider-travel-check-spec.md) §4.4, decision `D-4`.
2. **The harness prints its candidate list first, with no judgments.** The seat needs to know what is being judged without seeing any answer.
3. **The seat writes its own read, by hand, to the baseline file.** Same input the detector gets, same answer vocabulary. ⛔ **Guessing to fill the file is worse than useless — it manufactures agreement.** Where the seat genuinely cannot tell, it records `unsure`, and `unsure` rows are excluded from the score rather than counted either way.
4. **Run the detector.**
5. **Record the comparison** (§3) in the harness's spec-back before acting on anything.
6. ⭐ **Run 1 SETS the threshold. It does not apply one.** There is no fitted threshold on a first run, so the harness emits the full ranked list with probabilities and the threshold comes OUT of the comparison. `docs.typesafe.ai/confidence.md` is explicit that thresholds must be fitted to your own data.

---

## 3. What gets recorded, every first run

- Population size, and the harness's own coverage counts — candidates found, candidates with evidence, candidates skipped.
- The seat's calls, the detector's calls, and **every disagreement, itemised**.
- Where ground truth exists: both scored against it. Where it does not: the disagreements listed and adjudicated by the seat, with the adjudication reasoned, not asserted.
- **Actual `usage.input_tokens`, cost and wall time.** Measured, never estimated.
- ⛔ **What the harness could NOT see.** In the 2026-09-21 validation run this was the finding that mattered most.

---

## 4. The measured precedent

The protocol comes from a run on 2026-09-21 (UTC): stale-row detection over [`trader-tick-queue.md`](trader-tick-queue.md) §2, scored against the 11 rows closed by commit `0767245`. 42 rows, two arms, 84 API calls, **$0.0046 and about 9 seconds**.

| Input given to both judges | Seat | Detector |
|---|---|---|
| Row text alone | 1 of 8 | 1 of 8 |
| Row text plus git evidence gathered in code | 6 of 8 | 5 of 8 |

⭐⭐ **Three findings, and the first two only exist BECAUSE a hand baseline was written first.**

1. **Text alone is unsolvable — for both judges.** Without the seat's own 1-of-8, the detector's 1-of-8 would have read as the model being weak. It was the task being impossible. **That is a design finding, and it redirected the whole programme toward code-supplied evidence.**
2. **The detector slightly UNDER-performs the seat** (5 against 6). Worth knowing before anything is allowed to hold a verdict. ⛔ **Nothing in this programme holds a verdict. Every harness reports to the seat.**
3. ⛔ **The binding constraint was evidence gathering, not judgment.** Both rows missed by both judges were rows where the enumeration code found nothing to reason over. **The next improvement on any harness is almost always better candidate enumeration, not a better question.**

---

## 5. ⛔ What invalidates a first run

- **The seat saw any detector output before writing its baseline.** The run is then a demonstration, not a measurement, and must be labelled as one.
- **The baseline was filled with guesses** rather than `unsure` where the seat could not tell.
- **The harness reported zero candidates and was read as a pass.** A zero count is an error exit, not a clean bill. The repo's own ruling: a counter reading 0 is the tripwire, not waste.

---

## 6. The harnesses, and what triggers each one's first run

| Order | Harness | Covers | First real use |
|---|---|---|---|
| 1 | Rider travel — [`rider-travel-check-spec.md`](rider-travel-check-spec.md) | rider arrival | The absorption S2 header rotation |
| 2 | Commit walker | engine-change tag audit · display-string parity | Next audit of what owes a `DeribitIndicatorProject.md` §15 entry |
| 3 | Fixture parser | fixture-literal provenance · fixture name against assertion | Next fixture review. ⭐ **Keep the mutation step here** — it is the only route to positives for the name check, and this repo already requires fixtures be mutation-proven |
| 4 | Doc scanner | version rot · identifier collision · cross-doc contradiction | Next state read or handover |
| 5 | Doc re-ranker | finding which doc answers a question | Continuous; judged on whether it speeds the seat up |

⚠ **A harness that is never triggered is never validated.** If one sits unused for long enough that its trigger stops arriving, say so and either retire it or give it a dedicated test — do not let it accumulate in `tools/` as an unmeasured check.

---

## 7. What is NOT verified

- **That any of these harnesses generalise.** One task has been measured. One task is not a benchmark.
- **That the threshold from one run holds on the next.** It is fitted to one population and must be re-checked.
- **Harnesses 2 to 5 have not been built.** Their row in §6 is a plan, not a record.
