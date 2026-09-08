# `S-4` batch summary — eval-cache backfill dedup keyed on identity

**Audience:** trader (relayed). For verification handles and the working-document detail, see [`s4-spec-back.md`](s4-spec-back.md).

**Spec:** [`s4-eval-cache-identity-proposal.md`](s4-eval-cache-identity-proposal.md) — D-table RULED IN FULL 2026-09-08 (D-1 (b), D-2 (b), D-3 (a)). Built from its **§4b**, not its §4.

**Status: BUILT. Not committed** — per the spec's own §8 (*"Run any git write command — commit is the orchestrator's"*), no commit was made. `git status -sb` shows two modified files: `LivePerformanceTracker.vb`, `verify/ordercheck/Program.vb`, plus the two doc edits (`DeribitIndicatorProject.md` §15, and these two report files).

---

## 1. What was wrong

`LivePerformanceTracker`'s eval-cache backfill built its dedup set once from the loaded cache, then never updated it while looping over new `analysis_log.csv` rows. A batch containing two rows that shared an identity was therefore admitted **differently depending on whether the process had just restarted**:

| Cache state | Old behaviour |
|---|---|
| Cold (empty cache) | Both rows admitted |
| Warm (one already cached) | Only one admitted |

Same input, different output — a defect with no correctness signal from a static build, only from actually running the two paths.

The fix required two things landing **together** (D-1 (b), overturning the orchestrator's original "ship the loop fix alone" read — see §3.1 of the spec for why):

1. The dedup key becomes the **`(InstanceId, SignalId)` identity pair**, not a bare `Timestamp` (D-2 (b)). `SignalId` alone was explicitly rejected — it is a per-process counter that restarts at 1, so two different process runs can collide on it.
2. The dedup set is updated **inside** the loop as each row is admitted, so the same batch behaves identically whether the cache started warm or cold.

Half of this is a code fix; the other half is a **schema change** — `analysis_eval_cache.csv` moves from v6 to v7 to carry the new identity columns. D-3 (a) rules that legacy rows written before those columns existed fall back to `Timestamp` — the only fallback that isn't circular (matching back to `analysis_log.csv` on `Timestamp` would be trusting the very key this change exists to distrust).

## 2. What changed

| File | What |
|---|---|
| `LivePerformanceTracker.vb` | `EvalCacheEntry`/`LogRow` gain `InstanceId`/`SignalId` (nullable, no default). `KeyFor` — the identity-pair-with-Timestamp-fallback function. Backfill loop extracted to a pure `Friend` function (`BackfillNewEntries`) so it's directly testable. `analysis_eval_cache.csv` schema v6→v7: two columns appended, a plain re-stamp — no existing row's outcome is touched. |
| `verify/ordercheck/Program.vb` | Four new fixtures, `A69a`–`A69d` (5 `Check()`s total — `A69d` carries two). |
| `docs/DeribitIndicatorProject.md` | §15 entry added (this is a persisted-schema change; **not** tagged `[no-engine-change]`). |
| `settings.json` | **Untouched** — `S-4` dates to v26 but needs no key and no version bump. Verified: `git diff --stat -- settings.json` is empty. |

## 3. Verification

| Check | Result |
|---|---|
| `verify/ordercheck` build | `Build succeeded`, 0 errors (1 pre-existing warning, unrelated to this change — see spec-back §4) |
| Harness | **339 → 344** `Check()`s, `ALL PASS` |
| `verify-gate.ps1 -Mode local-fast` | **GATE PASSED** — solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck all `Build succeeded` 0/0 Release; display-parity clean; version-bump nudge clean |
| Mutation-proof (5 mutations run, each reverted after) | Every fixture fails under its targeted mutation and nothing else — see spec-back §1 for the full table |
| `settings.json` diff | Empty |

## 4. What this is NOT

- Not a settings change — no key added, no version bump.
- Not a re-walk of history — the v6→v7 migration adds two empty columns to every existing row and changes nothing else. `A69d`'s second `Check()` proves this byte-for-byte over four representative outcome shapes (`SUCCESS`, `ADVERSE_HIT`, `PENDING`, `NO_DATA`).
- Not yet observable in production numbers — the current tape has zero same-second duplicate timestamps (carried from the spec's own §2.4 measurement, not re-run this session), so the fix is correct but currently dormant. See spec-back §4 for exactly what that means and what wasn't checked.

## 5. Next step

Compile-and-test gate: build the solution, confirm the harness and `verify-gate.ps1` still pass on your machine, then commit. Nothing is blocking — no open decision, no escalation trigger fired.
