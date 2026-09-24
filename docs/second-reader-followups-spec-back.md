# Second-reader follow-ups — spec-back (`SR-D1`, `SR-D2`, `SR-D6`)

**Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md), single document (the brief named one file, not the usual pair).

**Implementer:** Sonnet 5 (`claude-sonnet-5`), effort medium, per the brief's own recommendation.
**Start commit:** `f7da21c`. **Population:** the three `SR-D1`/`SR-D2`/`SR-D6` decisions in [`jev-harnesses-second-reader-2026-09-24.md`](jev-harnesses-second-reader-2026-09-24.md) §6, all RULED YES 2026-09-24. **Not pushed.**

**Jev spend, this session only:** 60 calls (`FP-2`/`FP-2t` mutation acceptance, §1 `H-3`) + 151 calls (`Q29` no-answer exception, §1 `H-4`) = **211 keyed calls**, all against items the brief named. Zero unjudged fixture sites, zero reserved queries other than `Q29`.

---

## 0. Outcome, per item

| Item | Built as | Result |
|---|---|---|
| `SR-D1` | `FP-D32` in `tools/checks/fixture-parser.ps1` (revision 5) | `FP-Q1` item-level baseline gate, the `FP-D24` pattern. Proved with synthetic input + transport seam, no key |
| `SR-D6` | `FP-D33` in `tools/checks/fixture-parser.ps1` (revision 5) | `FP-2t` (title-stripped) beside `FP-2`. Acceptance: 5 of 6 flagged on both arms, same five; `A23a` NOT flagged by either (`FP-2t` UNSTABLE, weakest row) |
| `SR-D2` | `-NoAnswerOnly` (Mode 3) in `tools/checks/doc-reranker.ps1` | `Q29`: `noul=0.07` — no doc answers it, matching the expected trap answer |

---

## 1. Ranked verification handles

All pinned to the working tree at this session's close (commit hashes below once created). **If you run only one, run `H-1`.**

**`H-1`** — all four self-tests, no key, no network:
```
powershell -NoProfile -File tools/checks/selftest/fixture-parser-selftest.ps1
powershell -NoProfile -File tools/checks/selftest/decision-bias-runner-selftest.ps1
powershell -NoProfile -File tools/checks/selftest/doc-reranker-selftest.ps1
powershell -NoProfile -File tools/checks/selftest/invoke-jev-selftest.ps1
```
Every one must print `SELFTEST PASSED`. Covers `SR-D1` (checks E1/E2), `SR-D6`'s call-count arithmetic (checks B/D, now 20/15 not 15/10 — the load-bearing identity: `FP-2` item cost doubled, `FP-1` items unaffected), and `SR-D2` (checks G/H/I/J) in one run, offline.

**`H-2`** — the arithmetic identity for `SR-D1`'s gate: `62 candidates x 5 samples = 310` must equal `SCOPE_JEV_CALLS` in self-test check E2, and the SAME 62 must produce `SCOPE_UNBASELINED_ITEMS=1` in check E1 when 61 are covered. A gate that silently widened or narrowed the candidate set would move one number without the other.

**`H-3`** — `FP-2` vs `FP-2t`, the mutation acceptance run: `docs/harness-runs/fixture-parser-fp2t-mutation-2026-09-24.md` §1. Load-bearing values: `FP2_BAD_VERDICTS=5` (unchanged from the title-rename run — `FP-2t`'s presence must not move `FP-2`'s own exit-code arithmetic) AND `FP2T_ITEMS_SAME_PLURALITY_AS_FP2=6 of 6` (every row's plurality matches, `A23a` included — the counter compares plurality-to-plurality, not confidence, so it staying `6 of 6` while `A23a`'s agreement rate visibly drops to `0.6` is not a contradiction; both numbers are quoted in the record together on purpose).

**`H-4`** — `Q29`'s ledger exception: `docs/harness-runs/doc-reranker-spent-queries.json` must show `Q29` in `spent` exactly once (unchanged) AND in a new `exceptions` array exactly once. Two arrays, one `Q29` in each — a duplicate in `spent` would mean the exception path re-triggered the ordinary spend path by mistake.

**`E-1`** (evidence, not re-runnable without spending new calls) — the real `Q29` run's console output, pasted in `docs/harness-runs/doc-reranker-reserved-run-2026-09-24.md` §3: `noul=0.07`, 151 calls, resolved model `jev-1.13.0`. Re-running it would need a second `-AllowSpentOnce` exception, which the ledger now already carries one of — re-spending is possible (the gate is a logged exception, not a hard block) but pointless and costly; don't.

---

## 2. Decisions queued, with my read

None. All three items were already RULED YES with a settled design (`(a)` in every case); nothing here required a fresh call. Two small design choices were made within the ruled shape, logged per `CLAUDE.md`'s auto-proceed obligation:

- **`SR-D1`'s gate scope.** The ruling says "refuse the run when any scope candidate lacks a seat line." I read this as scoped to a baseline that WAS supplied and found (not to the pre-existing, separately-designed "-ScopeBaselinePath never supplied → code-only, non-fatal" degrade from `FP-D15`). **Why:** `FP-D15`'s non-fatal degrade for "no baseline at all" is a distinct, already-shipped, already-reasoned design choice (the scope filter is a filter, not a primary detector); collapsing it into a hard failure would be a SECOND, unruled behaviour change riding on this one. Reversible either way (one revert), so this is exactly the auto-proceed class, and I named it in the `FP-D32` comment block rather than silently picking a reading.
- **`SR-D2`'s `-AllowSpentOnce` semantics.** I made it refuse loudly (`ALLOW_SPENT_ONCE_UNUSED`) when passed for an id that is NOT already spent, rather than silently ignoring the flag. **Why:** the richer, more-informative option (a flag that only ever means what it says) beats the cheaper one (a flag that's a no-op half the time) — the `CLAUDE.md` "measured bias" test names exactly this trade, and here the richer option cost nothing extra to build.

---

## 3. Spec-back — feedback on the brief

- **What the brief got right, specifically:** naming the exact in-repo template for each item (`FP-D24` for `SR-D1`, `FP-1v2` for `SR-D6`, the harness-5 ledger for `SR-D2`) meant every design question was pre-answered; the only work was mechanical translation into each harness's own shape. The "escalation trigger: stop if a design decision the packet doesn't settle" never fired, because it genuinely didn't need to.
- **Where the brief was narrower than its own words:** it didn't anticipate the synthetic fixture file's Filler padding (`A903`, `f00`..`f59`) as a hidden scope-candidate population. `SR-D1`'s acceptance ("prove it with a synthetic input") reads as if the existing synthetic file's two ambiguous-callee params (`minCoverageSec`, `currentATR`) were the whole scope population; in fact the file carries 62 scope candidates once `-ScopeBaselinePath` is supplied, because the same padding that satisfies `PARSER_SUSPECT`'s row-count floor also lacks a derivable settings key. I found this only by probing `SCOPE_CANDIDATES` directly rather than assuming; a future seat extending this self-test should do the same before writing an expected count.
- **A constraint pair that nearly conflicted:** `SR-D6`'s "same state" instruction and the actual mechanics of `FP-1v2`'s pattern (a second QUESTION in the SAME call, sharing one `state`) look like they license reusing that exact shape for `FP-2t`. They don't: `FP-2t`'s difference (title-stripped body) lives IN the state (`sub_body`), not in the criteria text, so it needs a SEPARATE call with a different state, not a second question in the same one. The brief's own wording ("Same question and same state, except...") already flags this if read literally — "except sub_body" is a state change — but it's easy to misread against the `FP-1v2` precedent's shape. Naming this explicitly for whoever reads `FP-D33` next.

---

## 4. What I did not verify, and cannot

- **Whether `FP-Q1`'s scope classification would answer any of the 62 synthetic candidates differently under a real key.** Every self-test run (including the new `E1`/`E2`) uses the `-TestTransportOverride` seam with synthetic answers; the gate's CORRECTNESS (never calls when incomplete, calls all 62 when complete) is proved, not the classifier's real judgment on `minCoverageSec`/`currentATR` themselves — that was never in scope for `SR-D1`.
- **Whether `FP-2t`'s weaker showing on `A23a` (agreement 0.6, mean top prob 0.404) would replicate on a second run.** One run each side, per the record's own §3. `A65c` moved between STABLE/UNSTABLE across the two 2026-09-23/24 runs already, so single-run stability claims on this population are known to be fragile.
- **The dollar cost of this session's 211 Jev calls.** Reported in input/output tokens only, per every other record in this programme; no verified price list.
- **Whether the reformatted `doc-reranker-spent-queries.json` (PowerShell's `ConvertTo-Json` re-serializes the WHOLE file on every write, changing indentation/quoting style even though only one array gained an entry) causes any downstream tool or doc to break on exact-byte assumptions.** Checked: valid JSON (`ConvertFrom-Json` round-trips it, exercised live by the selftest and by `doc-reranker.ps1` itself), no control bytes (`grep -nP '[\x00-\x08\x0B\x0C\x0E-\x1F]'` — zero hits). Not checked: any doc that might quote a byte range or hash of this file specifically (none found by grep, but not exhaustively ruled out).
