# Doc re-ranker (harness 5) — measurement plan

**Created 2026-09-23 (UTC).** Harness 5 of the Jev programme ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6). **A plan, not a build spec.** The build spec follows the measurement below, the order harness 4 used ([`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §5).

---

## 0. The ruling

**RULED 2026-09-23 (UTC), trader: option (a).**

- Build harness 5 now, as a TRIAL that runs alongside the engine work.
- **For harness 5, "closed" means built and in trial, not validated.** It can only be judged over sessions of real use ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §1), and the engine sessions are that use.
- **Its main query type: "where was this decided?"** This absorbs the parked settled-decision guard ([`trader-tick-queue.md`](trader-tick-queue.md) §2) and the previous orchestrator's `J-10`.

---

## 1. The job

Given a question such as *"where was the MTF TTL constant ruled?"*, return the doc sections most likely to hold the answer, and say when no section does.

**Shape, from `docs.typesafe.ai/cookbooks/rerank_typesafe.md`:** code builds a keyword shortlist; one Jev Noul per (query, section) pair; code sorts by the noul. The cookbook's own numbers: top-1 5 % to 18 %, top-10 38 % to 62 %, on legal text. **Not measured here.**

⛔ **The re-ranker cannot add a section the shortlist missed.** The cookbook's shortlist held the answer for 40 of 40 queries. **Ours is unmeasured, and it is the binding constraint** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4, finding 3).

---

## 2. Measurement — in this order

| Step | Who | What | Output |
|---|---|---|---|
| **M1** | **The seat** | Write a query set BEFORE any tool output exists: at least 25 real questions, each with the expected doc path and section, checked by reading. Include at least 3 queries whose answer is "nowhere" (for example `J-10`, which is in no repo file). Split it into an acceptance subset and a reserved subset ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a) | `docs/harness-runs/doc-reranker-<stamp>-queries.json`, tracked |
| **M2** | Code only | Split the docs into sections (by heading). Measure the shortlist's hit rate for the expected section at k = 10 and k = 30, over two shortlist methods: plain keyword ranking (BM25) and the seat's own habit (grep on the query's distinctive terms). No Jev call | Hit rate per method and per k |
| **M3** | Decide | If the shortlist misses the answer on more than about a third of queries, fix enumeration before any Jev work (section size, archive scope, query terms) | A go / no-go line |
| **M4** | Implementer, then seat | Jev re-rank on the ACCEPTANCE subset only (implementer). The seat runs the RESERVED subset, scored against M1 | Top-1 / top-5 / top-10, both methods, with and without Jev |

**Sources for real queries (M1):** questions seats actually asked. This session alone asked: where the 39 / 8 bias count is recorded; whether `FP-2` was ever run on a known positive; why the semantic display-parity check was deferred; where the MTF TTL constant was ruled; where `J-10` is.

---

## 3. Design constraints, from the Jev docs and this repo

- **State = the query plus ONE section.** Never a whole doc: 5 docs exceed Jev's 32k-token state limit, and a large state of irrelevant detail costs accuracy (`docs.typesafe.ai/model-jaggedness/jev-1.13.md`, failure mode 5).
- **"Newest ruling wins" is decided in code, never by Jev.** Dates are text to Jev (failure mode 3). Code orders candidates that answer the query by commit date.
- **A no-answer outcome is required.** Use a separate Noul, "does any candidate answer the query?" (the skill-suggestion cookbook's pattern). Never infer "nowhere" from low scores alone.
- ⚠ **This repo's docs argue for their own importance** (`⛔ CURRENT STATE READ` banners). That is the "adversarial content" failure mode 6. Test at least one banner-heavy section against a plain one that holds the real answer.
- **Scope:** `docs/` including the archives, because rulings get archived verbatim. Rank archive sections, but label them.
- **Advisory only.** Never wired into a gate. It returns a ranked list; the seat reads the docs.

---

## 4. The trial (after the build)

- Log each real query in engine sessions: the query, the top 5 returned, and whether the seat used a returned section.
- Judge after at least 5 sessions: did it find the ruling faster than grep, or find one grep missed?
- A trial with no logged queries is not a pass ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6: *"a harness that is never triggered is never validated"*).

---

## 5. Model and effort

- **M1 (query set):** the seat. Opus 5.5, high. Labelling is the measurement.
- **M2 and the build:** Sonnet 5, medium. The judgment design is small and `tools/checks/lib/InvokeJev.ps1` is the shared call site. Escalate to Opus if the section splitter or the shortlist needs design choices beyond this plan.

---

## 6. Not verified

- The shortlist's hit rate on this repo. That is M2.
- Whether Jev's relevance judgment transfers from legal passages to these docs.
- That real "where was this decided?" queries arise often enough in engine sessions to judge the trial.
