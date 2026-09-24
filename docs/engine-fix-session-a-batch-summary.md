# Batch summary — engine-fix Session A (`D-1` POC-tier gate, `D-2` manual lines)

**Written:** 2026-09-24 (UTC), `date -u` = `Thu Sep 24 13:29:01 UTC 2026` at start. **Seat:** Seat 2 of the engine-fix build, Opus, high.
**Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §3 (Session A). **Brief:** [`docs/engine-fix-sessions-a-c-b2-brief.md`](engine-fix-sessions-a-c-b2-brief.md).
**Review packet:** [`docs/engine-fix-session-a-spec-back.md`](engine-fix-session-a-spec-back.md). **Format:** [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md).

## 0. Read this first

- ✅ **Built and committed locally, NOT pushed, NOT deployed.** Commit `a6b33fe` on `master`.
- ⛔ **This is a live scoring-outcome change and a dataset boundary.** It becomes live only at the next collector deploy. Under ruling `EF-1` (a) in `docs/engine-fix-build-spec-2026-09-21.md` §6, it rides the one combined deploy.
- ✅ **The escalation trigger did NOT fire.** The bug-hunt handle `H-3` (from `docs/medium-tier-bug-hunt-spec-back.md` §1) reproduced 143 of 8,508 and 1,288 of 38,665 exactly, at the base commit.
- ⭐ **The fix equals the counterfactual, row for row.** After the fix, the same instrument reproduces the logged `Placed*` values on 37,374 rows. That is 38,662 − 1,288 exactly.
- ⚠ **Two things the spec did not name were changed**, both inside `verify/ordercheck/Program.vb`: two legacy-twin fixtures were added, and one `A82b` mirror site was made producer-consistent. See `docs/engine-fix-session-a-spec-back.md` §2.

## 1. State at start

| Fact | Value | How checked |
|---|---|---|
| UTC now | 2026-09-24 13:29 | `date -u` |
| Base commit | `a3c9079` (`master`, clean, 2 ahead of `origin`) | `git status -sb`, `git rev-parse HEAD` |
| Concurrent commit | `7711e74` landed on `master` during the build (orchestrator, docs only: `docs/trader-tick-queue.md`, `docs/liquidation-probe-run-2026-09-21.md`) | `git log`, `git show --stat 7711e74` |
| Settings version | 68 | tracked repo-root `settings.json` line 2 |
| Harness baseline | **425 PASS, `ALL PASS`**, 2 SKIP (`A80b`, `A81b`) | `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` |
| POC-gate literals before the fix | inverted, as the spec described | read `Core/SignalEmitter.vb` and `Core/ScoringEngine_Calculate_Verdict.vb` |

## 2. What changed — commit `a6b33fe`

| File | Change |
|---|---|
| `Core/SignalEmitter.vb` | `pocGated` in `ComputeStructuralSideLevels`: long opens on `NEAR_HVN_SUPPORT` or `IN_LVN_BEAR`; short opens on `NEAR_HVN_RESIST` or `IN_LVN_BULL`. Comment now states the producer geometry |
| `Core/ScoringEngine_Calculate_Verdict.vb` | Legacy `enabled:false` twin: `hvnAbove` = `NEAR_HVN_SUPPORT` or `IN_LVN_BEAR`; `hvnBelow` = `NEAR_HVN_RESIST` or `IN_LVN_BULL`. The names are now truthful |
| `verify/ordercheck/Program.vb` | `A80b` always-on; new `A80c`, `A83a`, `A83b`; `A26b` POC sub-case label derived from the producer; one `A82b` site corrected; dispatcher updated |
| `docs/UserManual.md` | The VPFR step 5b gate line and the `NEAR_HVN_RESIST` interpretation line rewritten to the code's geometry |
| `docs/DeribitIndicatorProject.md` | One §15 row (898 characters in its summary cell) |

- **Not touched:** `settings.json` (stays v68), `AnalysisLogger.Header`, `docs/csv-rotation-riders.md`, `tools/WsTradeProbe/`, `docs/trader-tick-queue.md`.
- **Render surfaces:** no line added, removed, renamed or re-formatted. Both surfaces render the placed target generically. No card edit was needed.

## 3. Fixture outcome

| Fixture | Before | After |
|---|---|---|
| `A80a` | PASS ×2 | PASS ×2, unchanged |
| `A80b` | SKIP (known-defect repro) | **PASS ×2, always-on** |
| `A80c` (new) | — | PASS ×2 — legacy twin, `NEAR_HVN_*` half |
| `A83a` (new) | — | PASS ×4 — producer pin ×2, live gate `IN_LVN_*` half ×2 |
| `A83b` (new) | — | PASS ×2 — legacy twin, `IN_LVN_*` half |
| `A26b` | PASS ×3 (POC sub-case on a producer-impossible label) | PASS ×3 (label derived from `CalcVPFRLite`) |
| `A82b` / `A82c` | PASS | PASS (one site now producer-consistent) |
| **Total** | **425 PASS** | **435 PASS, `ALL PASS`** |

- `ORDERCHECK_KNOWN_DEFECTS=1`: 435 PASS, 2 FAIL, both `A81b` (Session B2 owns it). As the spec requires.
- Solution build (`dotnet build DeribitVerdictEngine.sln`): 0 warnings, 0 errors.
- `tools/checks/verify-gate.ps1 -Mode prepush`: `GATE PASSED` (display-parity OK, rotation-riders OK).

## 4. Mutation results (run on the working tree, then reversed with the inverse edit)

| Mutation | Fixtures that FAIL |
|---|---|
| Four-way swap in both copies (trap `EFT-1`) | `A83a` long, `A83a` short, `A83b` long, `A83b` short — **and nothing else** |
| Live copy fixed, legacy twin left inverted (trap `EFT-2`) | `A80c` long, `A80c` short |
| Live copy left inverted, legacy twin fixed | `A26b` POC sub-case, `A80b` long, `A80b` short, `A82c` (`target:POC` not reached) |

- The first row confirms the spec's trap claim: before `A83`, a four-way swap passed the whole harness.

## 5. Measured effect (bug-hunt handle `H-3`, pooled book `AWS-copybacks/pooled-book-2026-09-09`)

| Run | Placed-level reproduction | Population rows flipped | Placed targets moved |
|---|---|---|---|
| At base `a3c9079` (old gate) | 38,662 of 38,665 (99.99 %) | 143 of 8,508 (1.68 %) — STRONG 5, MEDIUM 34, WEAK 104 | 1,288 of 38,665 (3.33 %) |
| At `a6b33fe` (fixed gate) | **37,374** of 38,665 (96.66 %) | (the counterfactual is now the old gate) | 1,288 |

- **Identity:** 38,662 − 1,288 = 37,374. The fixed code departs from the logged history on exactly the rows the counterfactual predicted.
- **Stops:** the counterfactual moved 0 stops in both runs.

## 6. Commits

| Commit | Content |
|---|---|
| `a6b33fe` | Code, fixtures, manual lines, §15 row |
| (next) | This summary and the spec-back |
