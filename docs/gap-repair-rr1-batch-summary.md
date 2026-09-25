# RR-1 gap-repair sort-order fix — batch summary

> Outcome record for the trader. Reviewer working document: [`gap-repair-rr1-spec-back.md`](gap-repair-rr1-spec-back.md).
> Built against [`gap-repair-rr1-seq-order-spec.md`](gap-repair-rr1-seq-order-spec.md), `SO-1` = (a).
> Model: Sonnet 5 · Effort: HIGH · One session.

## Top-line result

- **Fix shipped, local commit only, NOT deployed and NOT pushed.** Commit `72f262e`.
- Escalation trigger from `gap-repair-rr1-seq-order-spec.md` §0: **not hit.** See §3 below.
- Two deviations from the spec's own fixture tables, both logged inline and in the spec-back:
  `A91b`, `A91c`. Neither is a design deviation — both are corrections to the spec's own worked
  examples, needed to make the fixtures test what they claim to test.

## 1. What changed

| File | Change |
|---|---|
| `Core/TradeStoreWriter.vb` | `ResolveRepairWindowsCore`'s row-sort comparator (`:1030`–`1035` pre-fix): now sorts a pair of seq-carrying rows by `TradeSeq`; a seq-less row still sorts by `Timestamp` against its neighbours. `TRAP 1` comment block rewritten to describe the new comparator and cite `RR-1`. |
| `verify/ordercheck/Program.vb` | Fixtures `A91a`–`A91c` added (call sites + bodies), placed beside the `A56` family. No existing fixture body edited. |
| `docs/DeribitIndicatorProject.md` §15 | New row, settings-untouched, this session. |
| `docs/trader-tick-queue.md` §2 | `RR-1` row moved from SPEC WRITTEN to BUILT, commit `72f262e`, not deployed. |

No `settings.json` change. No scoring file touched. No `AnalysisLogger.vb` change. No rendered
card or `BuildPlaintextSnapshot` line changes — this is a repair-path fix; the display-string
parity rule does not apply (named explicitly per the spec's acceptance list).

## 2. Harness, build, gate

| Check | Before | After |
|---|---|---|
| `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` | 483 PASS, 1 SKIP (`A81b`), 0 FAIL | **486 PASS, 1 SKIP (`A81b`), 0 FAIL — ALL PASS** |
| `dotnet build DeribitVerdictEngine.sln -c Release` | — | 0 Warnings, 0 Errors |
| `tools/checks/verify-gate.ps1` | — | **GATE PASSED** (harness, display-parity, version-bump advisory, rotation-riders all OK) |

## 3. Escalation trigger — checked, not hit

`gap-repair-rr1-seq-order-spec.md` §0 names three stop-and-escalate conditions:

| Trigger | Hit? | Evidence |
|---|---|---|
| An EXISTING fixture (`A56*`, `A79*`, any other) needs its expected value changed to pass | **No** | `A56c`/`A56d` ran unchanged, before and after the fix, with identical assertions and identical PASS results |
| `List.Sort` throws `InvalidOperationException` on any input | **No** | Never observed, across the shipped comparator and both named mutants |
| Fix needs a `settings.json` key, or touches `ScanForRepair`/`AppendRows`/`DedupTrades`/the bracket-selection logic | **No** | `git diff --stat` for commit `72f262e` shows only the sort comparator changed inside `ResolveRepairWindowsCore`; confirmed by reading the full diff |

One judgment call, flagged for the trader: **my own two NEW fixtures (`A91b`, `A91c`) needed
their asserted expected values corrected from my first draft**, because that first draft copied
the spec's own row tables literally and those tables don't produce the result the spec's prose
claims (§4 of the spec-back). The trigger's wording names *"EXISTING"* fixtures; `A91b`/`A91c`
were authored this session, so I read this as not a trigger hit and fixed them in place rather
than stopping. Flagged rather than assumed — see the spec-back for the trader's call if this
reading is wrong.

## 4. Fixture results (fail-first + mutation-proved)

| Fixture | Fail-first (pre-fix) | Post-fix | Named mutation | Mutation result |
|---|---|---|---|---|
| `A91a` | FAILED — 2 phantom holes `[9001,9002]`/`[9003,9003]`, exactly `RR-1`'s shape | PASS | Revert to old `(Timestamp, TradeSeq)` sort | **FAILS** — this IS the pre-fix run, same output |
| `A91b` | FAILED — 2 phantom holes | PASS (after redesign, see §2 of spec-back) | Category-partition comparator (§4.1 of the spec) | **FAILS** — phantom `Hole[7001,7499]` |
| `A91c` | FAILED — wrong bracket (`firstSeq=9002` vs correct `9004`) | PASS (after redesign, see §2 of spec-back) | No-tie-break comparator + a legacy/identified millisecond-tie data variant (temporary, not committed) | **FAILS** — construction order 1 finds `Hole[5001,5004]`; order 2 silently misses it |

`A56c`/`A56d`: PASS before and after, assertions byte-identical (not re-worded, not re-run with
different expected values).

## 5. What was NOT verified

- Whether a post-cutover row can ever fail `TryParseRow`'s seq field and become indistinguishable
  from legacy (the spec's named residual risk, `SO-2`). Not reproduced.
- Whether `trade_seq` is ever non-monotonic across a genuine venue-side reset. Carried forward,
  still open (pre-existing, code comment at `Core/TradeStoreWriter.vb`).
- How often the `RR-1` inversion shape occurs beyond the one 2.5-day window the read measured.
- Deploy. Reserved to the trader per `CLAUDE.md` — this change writes to the trade store.

Full detail, decisions queued, and spec feedback: [`gap-repair-rr1-spec-back.md`](gap-repair-rr1-spec-back.md).
