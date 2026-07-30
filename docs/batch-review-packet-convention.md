# Convention — reporting a multi-lane batch back to a reviewing seat

**Established:** 2026-07-31, after the pre-Aug-1 batch. **Confirmed by the Fable seat:** *"the ranked verification handles and the 'decisions queued, here's my read where I have one' structure is exactly what a double-check wants, and it's the shape future batch summaries should copy."*

Applies whenever an implementer seat executes a multi-item batch and hands the result to a different seat for review — the pattern this project uses whenever an orchestrator runs a spec someone else authored.

---

## The rule: two documents, not one

| Document | Audience | Answers |
|---|---|---|
| **`<batch>-summary.md`** | the trader, who relays it | *What happened?* Per-item outcome, raw tables, commit hashes, gate tails. |
| **`<batch>-spec-back.md`** | the reviewing seat, who works from it | *What should I check, what must I decide, and where was the spec wrong?* |

Collapsing them into one document produces a file that is too long for a reviewer with limited time and too editorialised for a record. Keep the summary a **record** and the packet a **working document**. Cross-reference; never duplicate.

The summary carries findings that change how it reads at the **top**, ahead of the per-item sections — a reviewer should not have to reach §4 to learn that §1 is superseded.

---

## The packet's four sections

### 1. Ranked verification handles

For each substantive claim, **the one cheap check that confirms it**, ordered by how much of the batch it covers. No re-runs.

- Name the **load-bearing** value, not just the headline. *"mean AND max |Δ| both zero"* — the match-rate alone would tolerate off-by-one.
- Prefer checks that are **one command or one grep**.
- Include the **arithmetic identities** that would expose a silent error (e.g. `kept + dropped = raw`). A reviewer can verify a whole pipeline in a line.
- Say which single check to run **if they only run one**.

### 2. Decisions queued, with your read where you have one

State each as a decision with options. Then, per decision:

- **Give a read when you have one, and label it a hypothesis.** Showing your reasoning lets the reviewer disagree cheaply instead of re-deriving.
- **Say "I have no read here" when the criterion is theirs.** Fake balance wastes a turn; so does a confident answer to a question you can't ground.
- **Flag when two decisions share a root** — ruling them together is usually cheaper and avoids contradictory outcomes.
- Include **scoping information** (what the narrowest version of a change would touch) without recommending it. That is the reviewer's most expensive missing input and the cheapest thing for the implementer to supply.

### 3. Spec-back proper — feedback on the spec itself

The part that makes it a *spec-back* rather than a status report.

- **What the spec got right, specifically.** Not flattery — the wording that did real work is worth reusing. ("report, don't chase" converted a missed prediction into a reportable result rather than an invitation to keep working.)
- **Which assumptions broke.** The highest-value item in the whole packet. A spec that asks for a comparison the data cannot support needs to be *said*, along with what you substituted and what that costs.
- **Where the spec was narrower than its own words** — a fixture requirement that reads end-to-end but can only be met offline, say. Record the gap where it will be found, not where it will be discovered later.
- **Constraint pairs that nearly conflicted.** If three rules together looked like a deadlock and only didn't because of an escape hatch you had to find, name the hatch so the next batch author writes it into the spec.

### 4. What you did not verify, and cannot

Stated plainly so nothing is assumed covered. Unverifiable-by-nature items (GC lifetime, anything needing the live app) belong here with the reason, not omitted.

---

## Ruling and superseding

When rulings come back, record them **where the open question lived** — the doc that said "recorded, not ruled" is the doc that must now say what was ruled. Then add a response section to the packet.

If evidence later overturns a ruling — including one you argued for — **supersede it in place, at the top of the affected section, and name your own error as directly as the other seat's.** The pre-Aug-1 batch has the worked example: a coordinator ruling and the implementer's own hypothesis were both wrong in the same direction, because both assumed the inputs were sound and argued about how to score them. The superseding note says exactly that.

---

## Worked example

- Packet: [`pre-aug1-batch-spec-back.md`](pre-aug1-batch-spec-back.md)
- Summary: [`pre-aug1-batch-summary.md`](pre-aug1-batch-summary.md) — see §0 for a top-placed superseding finding
- Spec that was reported against: [`pre-aug1-opus-batch-2026-07-31.md`](pre-aug1-opus-batch-2026-07-31.md)
