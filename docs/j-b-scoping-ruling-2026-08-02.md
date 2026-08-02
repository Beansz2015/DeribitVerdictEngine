# J-B scoping clause — RULING, 2026-08-02

**From:** the Opus orchestrator/ruling seat.
**Status:** **RULED.** This is the clause [`trader-tick-queue.md`](trader-tick-queue.md) §0a listed as owed by *"a ruling seat, not the trader."* It **unblocks C1** ([`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) D1–D7), which the seat-close handover ruled the **precondition instrument for every data-gated item**.
**Corrects:** the scoping gap in **J-B** as ratified in [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) §2. **J-B's substance is upheld in full and not reopened** — only its *domain of application* is pinned.

---

## 1. The ruling in one paragraph

**J-B's "residual ambiguity ⇒ defect" default applies only to hours in which the box was *positively recorded* as capturing. A box with no capture record for a process life is OUT OF SCOPE for that life — neither `defect` nor `expected-missing`, but a third, explicitly reported classification: `not-capturing`.** Scope is established by a **positive record** (D7's per-process marker line), never by an uptime baseline and never by reading the current config. Where no such record exists for a historical window, the report must say so and classify those hours `unknown-scope`, not silently pick a side.

---

## 2. Why the question as posed was the wrong question

The flag was raised as: *"J-B needs a **per-box expected-uptime** scoping clause. Unscoped it classifies most of the local box's existence as defect."*

**The observation is correct. The proposed instrument is not, and adopting it would have contradicted the proposal's own founding finding.**

§0 of the coverage proposal exists to record that **statistical baselines are meaningless on this instrument** — a 2m42s inter-trade silence is normal on a weekday, and per-hour volume runs **1,555 trades in hour 06 against 15,602 in hour 07, a 10× spread**. The proposal's stated conclusion is *"stop trying to infer coverage from the market, and cross-reference the app's own uptime record instead."*

An "expected-uptime baseline" is the same mistake one layer up. It would:

- **require weeks of history** before it meant anything, exactly like the per-hour median the proposal already rejected;
- **encode "this box is usually down" as a statistical expectation**, which is unfalsifiable — a box that dies permanently converges to its own baseline and reports healthy;
- **mask a real outage** on any box whose baseline had absorbed enough downtime, which is the precise failure J-B was ratified to prevent.

S1 already got this right by replacing an unanswerable question with an answerable one. The scoping clause has to do the same thing, not reintroduce the instrument S1 was built to avoid.

> **Generalisation worth keeping:** when scoping a defect rule, scope it by a **positive record of intent**, never by a statistical expectation derived from the behaviour being judged. A baseline built from observed behaviour cannot, even in principle, flag that behaviour as wrong.

---

## 3. The clause, as it must be built

### 3.1 Three classifications, not two

The report emits, per hour per box:

| Class | Meaning | Trigger |
|---|---|---|
| `captured` | tape present as expected | store rows present in the hour |
| **`defect`** | **the box was recorded capturing and the hour is short or ambiguous** | J-B in full, unchanged — including the trailing window and cross-GUID gaps |
| `expected-missing` | box recorded capturing, but recorded not-up for the hour | S1's uptime join |
| **`not-capturing`** | **box recorded NOT capturing for that process life** | D7's marker line says capture was off |
| **`unknown-scope`** | **no capture record exists for the window** | pre-marker history, or a copy-back that dropped the marker |

`not-capturing` and `unknown-scope` are **new and both are required.** Collapsing either into `expected-missing` is what would have produced the false-alarm storm — and collapsing `unknown-scope` into `not-capturing` would silently absolve exactly the windows least able to defend themselves.

### 3.2 Scope is a positive record, never the current config

**Binding, and it is D7's own reasoning carried into J-B:** the report must not answer "was this box capturing?" by reading `trade_store.enabled` at run time. That gives the flag's value *now*, not during the historical window, so a single flip misreads all of history.

This is sharper than it looks now that the overlay ships. `trade_store.enabled` on the local box lives in `settings.local.json`, so a naive read of the tracked base sees `true` and reports **every local up-hour as a defect** — the exact false alarm at issue. D7 already flags this; the clause makes it binding rather than advisory.

**Therefore: J-B's scoping requires D7 = (a), the per-process marker line.** If the trader instead takes D7 = **(c)** (scope the verb to AWS copy-backs and say so), the clause is satisfied differently but honestly: scope is then declared by the operator rather than recorded by the app, and every non-AWS box is `unknown-scope` by construction. **D7 = (b) — reading the settings file — is incompatible with this clause and should not be chosen.**

### 3.3 The trailing-interval rule travels with it

From the same session that raised the flag, and it belongs in the built report: **a trailing interval on a copied file is bounded by the copy time, not by now** — otherwise every AWS copy-back reads as a fresh death. Applies to `analysis_log.csv`, `ws_health.log` and the store alike.

---

## 4. What this means for the local box today, concretely

Under D1's **AWS-only** ruling the local box does not capture, expressed through the overlay since 2026-08-02. So under this clause:

- **Every local hour classifies `not-capturing`**, provided D7's marker records it. Zero defects, zero false alarms, and the report stays silent about a box that owes it nothing.
- **The local box's `analysis_log.csv` rows are unaffected.** They are a different book with a different purpose — they pool into the CSV reads and they serve as S1's uptime evidence. **Nothing here touches them**, and the row-count disparity that prompted the flag (local 567/392/268 vs AWS 921/921/914 on 07-28/29/30) is *not* a coverage question at all. It was cited as evidence of the problem; it is actually evidence that the two books measure different things.
- **If capture is ever enabled on the local box, no clause changes.** Its marker flips to capturing and J-B applies to it in full, from that process life forward. That is the property an uptime baseline could not have delivered.

---

## 5. What I did not rule, and what stays with the trader

- **D1–D7 remain the trader's.** This clause constrains **D7** (rules out option (b), and states what (a) and (c) each imply) and otherwise leaves the D-table untouched.
- **I did not re-open J-B's substance.** Ambiguity still resolves toward defect, on the ratified reasoning that a false defect costs one human dismissal while the other error costs tape, unmarked.
- **I did not rule D5** (Part B, the live `TAPE STORE` element), though it interacts: D7 = (a) costs a version bump, and the proposal notes that cost is **zero if D5 is (a)**. That trade is the trader's.
- **No fixture is specified here.** If the builder wants one, the natural addition to the **A49** family is an arm pinning that a `not-capturing` process life yields zero defects while a capturing one with the same silence yields defects — the inversion trap, in the same shape as A49i.

---

## 6. Effect on the queue

**C1 is unblocked.** Its §0a gate — *"needs the J-B clause below settled first"* — is discharged by this document. C1 is now a clean trader tick on D1–D7, with D7 carrying the constraint in §3.2 above.
