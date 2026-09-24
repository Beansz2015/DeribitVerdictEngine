# Seat handover — 2026-09-24 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-23.md`](seat-handover-2026-09-23.md), superseded for state. Its first-actions table is fully closed or moved below. [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §1 (what Jev is) still binds.

⛔ **Run `date -u` first.** This seat ran from 2026-09-23 08:47 UTC to 2026-09-24 ~13:15 UTC. The workstation shows GMT+8.

**State at close:**
- ✅ **`master` = `origin/master` at `49fb10d`. The trader pushed.** Nothing unpushed.
- Settings **v68**, untouched. **No engine code changed.** Every commit is `[no-engine-change]`.
- **The Jev programme is CLOSED** under the trader's close rule ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6a). ⭐ **The engine queue resumes now.**
- Collector: **HEALTHY** at the 2026-09-24 decision-point check ([`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5d).
- Nothing is running: no agent, no watcher, no scheduled task.

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Why |
|---|---|---|---|
| **1** | ⭐ **Engine-fix build**, per [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md): Session A (the POC-gate fix), Session B (the liquidation flag, measure first), Session C (`D-8`/`D-9` display fixes) | Opus 5.5, high, per that spec | Fully ruled and untouched for six sessions. Scoring and rendered values are **RESERVED** (`CLAUDE.md`): each change comes to the trader |
| **2** | Absorption S1/S2 with its eight riders, then ONE deploy | Opus 5.5, high | [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md). `T-7` is the only gate. ⚠ Harness 1's first run is TRIGGERED by the S2 rotation (§2 below) |
| **3** | `Q-1` (d): target and stop geometry by tier | Opus 5.5, high | [`medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md) |
| **4** | Investigate the repeated gap-repair fills (new, 2026-09-24) | Sonnet 5, medium | [`trader-tick-queue.md`](trader-tick-queue.md) §2 row. Tape complete; cost is repeat fetches |
| **5** | On each engine session, use the doc re-ranker for "where was this decided?" questions and log it (§2) | — | Harness 5's trial needs at least 5 sessions of real use |

⚠ **Usage:** the account hit its session limit four times in this seat. Run **at most one heavy agent at a time**; two parallel Opus agents tripped it. Agents resume cleanly from their transcripts; work in the tree survives.

---

## 1. What happened — the Jev close list, all six done

| Step | Result | Record |
|---|---|---|
| 1 · Harness 4, doc scanner | Built, armed. Pairing ruling `DS-A` (g): nothing dropped, noise labelled. Spec premise corrected: 8 of 13 "parser errors" were name errors | [`doc-scanner-build-spec-back.md`](doc-scanner-build-spec-back.md) |
| 2 · Harness 6, decision-bias tripwire (new) | 150 trader-ruled decisions, blind seat baseline first. Jev flags 9 of 22 overrules at 36 % precision against a 15 % base rate; Jev (stable) OR the keyword match catches 10 of 22. **About half the overrules went to an option nobody listed**, which no judgment on the recommendation can predict | [`harness-runs/decision-bias-pilot-run-2026-09-23.md`](harness-runs/decision-bias-pilot-run-2026-09-23.md) |
| 3 · Arm harnesses 1 and 2 | Firewall blocks no longer abort; transient-only retries; OK-class verdicts counted | [`harnesses-1-2-arming-spec-back.md`](harnesses-1-2-arming-spec-back.md) |
| 4 · Harness 3 batch | Receiver-typed signature lookup, marker labels, JSON array walk, `FP-1v2` beside `FP-1`, `FP-2` mutation 6 of 6 | [`harness3-batch-spec-back.md`](harness3-batch-spec-back.md) |
| 5 · Harness 5, doc re-ranker (trial) | On 18 answerable reserved questions: BM25 plus Jev top-1 7/18 and top-10 13/18, against 3/18 and 11/18 for BM25 alone. Four of five misses were outside the shortlist | [`harness-runs/doc-reranker-reserved-run-2026-09-24.md`](harness-runs/doc-reranker-reserved-run-2026-09-24.md) |
| 6 · Second reader | 13 findings, 0 HIGH, no recorded number changed. Unwrap sweep, spent-query ledger, `JEV_MODEL` recorded everywhere. Follow-ups `SR-D1`, `SR-D2`, `SR-D6` built | [`jev-harnesses-second-reader-2026-09-24.md`](jev-harnesses-second-reader-2026-09-24.md), [`second-reader-followups-spec-back.md`](second-reader-followups-spec-back.md) |

Also this seat: the `rider-travel.ps1` single-element unwrap fix (a lone non-travelling rider was silently dropped), `976729c`. Three Jev ideas parked behind the engine queue, plus `J-8`/`J-14` and `J-11` ([`trader-tick-queue.md`](trader-tick-queue.md) §2).

---

## 2. ⭐ The trader's rulings this seat — they bind

1. **The close rule** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6a): anything that can be done ON THE GO closes when built and armed, and is shadow-tested when its circumstance arises. Only one-offs no development event will trigger are done up front.
2. **Harness 5 counts as closed when built and in trial.** Harnesses 1 and 2 close when built and armed.
3. **`jev-latest` always.** Every harness requests it, and every harness now records the resolved version (`JEV_MODEL`). `jev-1.13.0` answered every call from 2026-09-24. The version behind any earlier run is unknown.

**Armed, run when the trigger arrives (shadow-test under the protocol: seat baseline first):**

| Harness | Trigger |
|---|---|
| 1 · rider travel | The absorption S2 header rotation (first action 2). One-shot |
| 2 · commit walker | A `-Since` window of commits no seat has seen |
| 4 · doc scanner | The next state read or handover. Run at `-Rev cbc2c91`; the 65 fixture-meaning labels are written then |
| 5 · doc re-ranker | Real "where was this decided?" questions in engine sessions; verdict after at least 5 sessions. All 21 reserved questions are SPENT (ledger `docs/harness-runs/doc-reranker-spent-queries.json`) |
| 6 · decision-bias tripwire | A new recommendation: seat labels its own pick first, then runs it. A flag means the decision goes to the trader |
| 3 · `FP-1` v1 vs v2, `FP-2`/`FP-2t` | New fixtures, the next fixture review |

---

## 3. Lessons — each one cost something this seat

| Lesson | Where it bit |
|---|---|
| **A handle that calls Jev can spend a reserved item.** Name reserved items in every brief, and give the tool a spent ledger | Harness 5's build packet spent reserved `Q11` in its own `H-4` |
| **Measure a claim before writing it**, even a small one | "Every keyword catch is also a Jev catch" was false; caught before commit. "`Q29` unreported because of the template" was a guess and wrong |
| **A mutation test must mutate everything that carries the answer** | `FP-2`'s first mutation left the original names in the `Check` titles; with them renamed it dropped to 5 of 6 |
| **PowerShell 5.1 single-element unwrap is everywhere in `tools/checks`** — and `return ,$list` plus a caller's `@()` nests the list | Four sites in `rider-travel.ps1`, more in the sweep |
| **Blind labelling is only as blind as the seat's memory.** Declare recognised items and score with and without them | 46 of 150 decision rulings were recognised |
| **A `cd` inside a Bash call changes the harness working directory** | Twice this seat; no file affected. Use absolute paths |

---

## 4. ⚠ What I did NOT verify

- Whether the thread-leak re-entrancy gates work under load. The collector check was clean, but no scan produced a stall to test them.
- The cause of the repeated gap-repair fills.
- Whether harness 6's flag works on decisions written AFTER this seat saw what Jev flags.
- The current price of `jev-latest`. All costs quote the cookbook's $0.042 per million input tokens.
- Harness 5's firewall-blocked sections in the two earlier runs: they were not counted then, so it is unknown whether either run was affected.
