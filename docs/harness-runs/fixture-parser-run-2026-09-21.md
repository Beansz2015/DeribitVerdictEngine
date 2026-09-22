# Fixture parser — first run record, 2026-09-21 (UTC)

**Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md). **Spec:** [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md).

⛔ **Read §3 before quoting the headline.** The raw comparison reads 12 of 15, and **at least one of the three disagreements is the detector being RIGHT and the baseline being WRONG.**

---

## 1. ⛔ A protocol gap — who writes the baseline

**[`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §4.4 said WHERE the first-run baseline goes and never said WHO writes it.** The implementer reasonably wrote one itself, ran the comparison, and reported it.

**Two consequences:**

1. **The seat is now contaminated** on all 20 items and can never produce the intended baseline for them. Same failure as [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4a, in a **new form** — §4a covers an acceptance run reporting verdicts; it does not cover the implementer *writing the baseline itself*.
2. ⚠ **What was measured is inter-reader agreement between two models, not detector-versus-seat.** That is a real measurement of something. It is **not** the one the protocol specifies.

⭐ **Rule to add: a build spec names WHO writes the baseline, and for a first run it is always the seat. An implementer that finds no baseline stops and says so** — it must not supply one.

---

## 2. Coverage and cost

| | |
|---|---|
| Revisions walked | **87** — trap 1 satisfied |
| `indicators.OBV.trend_gate` resolved | **`{0.001, 10, 18, 23}`** ⭐ the trap-1 proof; a 40-revision walk returns only `{18, 23}` |
| Literal call sites | **118** (see §4) |
| Items judged | 20 — 15 provenance sites, 5 fixture names |
| Tokens / wall / cost | 221,454 input · 76.3 s · **$0.0093** |

---

## 3. ⭐⭐ The adjudication — the detector beat the baseline

**Three disagreements, all 5-of-5 self-consistency STABLE, so none is model noise.** One adjudicated by the seat against the tree:

**`A3` · `accelThreshold`**

| Evidence | |
|---|---|
| The literal | `accelThreshold:=1234.0` |
| Ever-shipped set for `indicators.MicroCVD.accel_threshold`, all 87 revisions | **`{5000.0, 10000.0}`** — 1234.0 **never shipped** |
| The comment | Declares MECHANISM, and explains the value was made synthetic **because** the previous one equalled shipped |
| **Detector** | `mechanism_declared_ok` — ✅ **CORRECT** |
| **Baseline** | `declared_but_contradicted` — ❌ **WRONG** |

⭐ **The baseline read the past-tense clause — *"accelThreshold and floorPct EQUALLED shipped, so both are now obviously synthetic"* — as a present-tense claim about the current literal.** The comment is describing the fix that was already applied. The detector read it correctly.

⚠⚠ **And the implementer diagnosed the cause of its own error correctly while still recording the row as a disagreement in its own favour.** Its write-up names the past-tense phrasing as the trigger and attributes it to *"a real ambiguity in the codebase's comment prose"*. **The prose is dense; the reading error was the baseline's.**

**So 12 of 15 understates the detector.** The other two disagreements are not yet adjudicated.

---

## 4. ⛔ My own measurement was wrong, and in the class my own trap warns about

[`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §2 states **120 literal call sites. It is 118.**

The extra two are `wide:=1.0` / `tight:=2.0` at `verify/ordercheck/Program.vb:12534` — **prose inside a MECHANISM comment block**, not code. My raw regex counted comment text as call sites. Verified: 120 unfiltered, 118 excluding comment lines.

⛔ **This is trap 4 — the trap I wrote — manifesting as comment-versus-code rather than line-anchoring.** And the comment I miscounted is a *correct provenance declaration*, i.e. **I counted the rule's own worked example as a violation candidate.**

⚠ **The `< 60` escalation trigger was derived from the wrong 120.** Harmless in direction; the reference value should read 118.

---

## 5. Three further spec defects the build found

| # | Defect |
|---|---|
| **1** | ⛔ **§2's "44 MECHANISM / 18 SHIPPED" measure a DIFFERENT population than named-argument literal passes.** Confirmed by the build: a MECHANISM comment sits above an object-initializer assignment (`.RocMagnitudeThreshold = 0.50`), not a `:=` call. `SITES_WITH_PROVENANCE_COMMENT` can never reproduce 62 — the counts are of different things, and the spec implied they were the same |
| **2** | ⛔ **`FP-D2` is a Decision with no Question.** §4.3 fully specifies `FP-1` and `FP-2`; the key-resolution call `FP-D2` needs was left for the implementer to design from a one-line decision. It invented a third question type. **A decision that requires a new question must carry that question's criteria** |
| **3** | ⚠ **"The comment block above the call site" is ambiguous when one block covers several consecutive statements.** `A6_ObvNormalisation` calls `CalcOBV` twice under one shared comment; the parser attaches it only to the first, silently excluding the second call's literals. ⭐ **Correctly flagged rather than guessed** — how far a block's coverage extends is a real design call |

---

## 6. What was NOT verified

- **Two of the three disagreements.** Only `A3` was adjudicated against the tree.
- **`FP-D2`'s key matching.** On one site it resolved to no key despite a comment plausibly naming one. Out of scope per the spec's own §6, but now a known imperfection.
- **Any UNSTABLE row.** Every row across all runs returned 5-of-5 agreement, so that path is built but unexercised on real data.
- **The `SETTINGS_REVISIONS_WALKED < 80` branch** of `PARSER_SUSPECT`.
- ⚠ **Whether 20 judged items generalises.** It does not. This is a small population and the measurement inherits that.

---

## 7. Owed

| # | Item |
|---|---|
| **1** | ⛔ **Protocol rule: a build spec names who writes the baseline; for a first run it is the seat.** An implementer finding none stops |
| **2** | Correct §2's 118, and the trigger's reference value |
| **3** | Separate the provenance-comment population from the named-argument population in §2 |
| **4** | Give `FP-D2` a real question with criteria |
| **5** | Rule how far a shared comment block extends |
| **6** | Adjudicate the other two disagreements |
| **7** | ⭐ **Consider rewording the past-tense provenance comments.** The `A3` prose misled a careful reader. The detector handled it; a human did not |
