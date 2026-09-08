# `S-4` spec-back — eval-cache backfill dedup keyed on identity

**Audience:** the reviewing seat. Per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Pairs with [`s4-batch-summary.md`](s4-batch-summary.md) (outcome record).

**Spec:** [`s4-eval-cache-identity-proposal.md`](s4-eval-cache-identity-proposal.md), built from its §4b. Baseline `a6cfb8d`; re-verified `git diff a6cfb8d..HEAD -- LivePerformanceTracker.vb` was empty before editing, so the spec's line numbers (`:372`, `:445`, `:1448`) were trusted rather than re-derived.

---

## 1. Ranked verification handles

All five are `H-n` — every one is a command the reader can run today against the working tree; no scratchpad instrument was used anywhere in this build.

**If you only run one: `H-3`.** It subsumes `H-1`/`H-2` and additionally covers the four standalone tool projects and the full solution build's Release configuration for `OrderCheck`.

| # | Handle | Expected | Covers |
|---|---|---|---|
| `H-1` | `dotnet build DeribitVerdictEngine.sln -c Release` | `Build succeeded`, 0 errors (1 pre-existing `BC42109` warning at `LivePerformanceTracker.vb` — present in the original file at the same code shape before this change; unrelated to `S-4`, not introduced by it) | AC-1's solution half |
| `H-2` | `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build \| grep A69` | 5 `PASS` lines (`A69a`, `A69b`, `A69c`, `A69d`×2), 0 `FAIL` | The four new fixtures |
| `H-3` | `powershell -File tools/checks/verify-gate.ps1 -Mode local-fast` | `GATE PASSED` — AutoTweaker/WhatIfRunner/CeilingAudit/BacktestRunner/OrderCheck all `Build succeeded`; harness `ALL PASS`; display-parity clean; version-bump nudge clean | AC-2, AC-4, most of AC-1 |
| `H-4` | `grep -n "existingTs" LivePerformanceTracker.vb` | Shows the set built inside `BackfillNewEntries` from `existingCache`, tested per-row, and `.Add`ed **inside** the loop | AC-5, verbatim |
| `H-5` | `git diff --stat -- settings.json` | Empty | AC-7 |

**Arithmetic identity:** harness count 339 (pre-change baseline, independently confirmed by running the harness before any `A69` fixture existed) + 5 new `Check()` calls (`A69a`=1, `A69b`=1, `A69c`=1, `A69d`=2) = 344 (post-change, `H-2`/`H-3`). If this doesn't reconcile, a fixture was silently dropped or double-counted.

**Mutation proof — all five run and reverted, none left in the tree:**

| Mutation | Failed | Passed |
|---|---|---|
| `KeyFor` reverted to `Timestamp` alone (AC-9's named mutation) | `A69b`, `A69c` | `A69a`, `A69d`×2 |
| `KeyFor` reverted to `SignalId` alone | `A69b`, `A69c` | `A69a`, `A69d`×2 |
| D-1 (b) in-loop `existingTs.Add` removed | `A69a` (`coldTotal=2 warmTotal=1` — the exact pre-fix asymmetry from spec §2.2), `A69b` | `A69c`, `A69d`×2 |
| D-3 (a) fallback replaced with a fresh GUID per call | `A69d` part 1 only | `A69a`, `A69b`, `A69c`, `A69d` part 2 |
| `FormatEvalEntry` forced to always write `TargetEverHit="0"` | `A69d` part 2 only | `A69a`, `A69b`, `A69c`, `A69d` part 1 |

Each mutation failed exactly the fixture(s) it should have and nothing else — no fixture is inert, none over-fires.

---

## 2. Decisions queued

**None.** The D-table was ruled in full 2026-09-08, before this build began, and nothing surfaced during the build that reopens it. Three implementation choices were made that the spec's build list didn't spell out — not decisions needing a ruling, but worth a reviewer's eyes since they widen the code's surface area beyond what §4b literally names:

- **`LogRow`, `ParseAnalysisLog`, `LoadEvalCache`, `WriteEvalCache` promoted `Private` → `Friend`.** Required so the harness — which links the real source, not a compiled assembly — can drive the backfill and the migration directly. This is the same pattern already used for `IsPreV5Schema`/`IsPreV6Schema`/`BuildLiveEntry`/`AggregateRange`/`EvaluateEntry`'s Friend overload elsewhere in this file; no new *external* surface is created since `Friend` stays assembly-internal and the harness project already sits inside that boundary by design.
- **`WriteEvalCache`'s signature gained an `entries` parameter** (was implicit-`_evalCache`, now explicit). Every internal call site was updated to pass `_evalCache` explicitly; behaviour is unchanged, but it's a signature change to a function five other call sites depend on, worth a second pair of eyes on the diff.
- **The backfill loop was extracted into a new pure `Friend Shared Function BackfillNewEntries`.** Not named in the spec's build list, which described the fix as edits to the existing inline loop. I chose extraction because a prior fixture comment in this same file states driving the real `InitialiseAsync` "needs a live OHLC fetch" and was avoided for that reason — extraction was the only way to get `A69a`–`A69c` onto the real production code path rather than a re-implementation of it. `InitialiseAsync`'s Step 3 now calls this function instead of running the loop inline; the observable behaviour is identical.

---

## 3. Spec-back proper

**What the spec got right, specifically:**

- The line numbers at baseline (`:372`, `:445`, `:1448`) were exact — confirmed by an empty diff between the spec's baseline and this build's starting `HEAD`, so no re-derivation was needed.
- §0.1 Trap 2 (`SignalId` restarts per `InstanceId`) was specific enough to design directly into `A69c`: same `SignalId`, different `InstanceId`, one fixture that fails under *both* wrong keys at once rather than needing two separate fixtures.
- §4b step 3's instruction to mirror the v3→v4 pattern turned out to be exactly right, and more valuable than it first read: once followed, the v6→v7 "migration" collapsed to a one-line gate-and-rewrite with **no re-walk code at all**, because `LoadEvalCache`/`FormatEvalEntry` already round-trip every field they don't explicitly touch. The migration risk the spec was most worried about (§0, "a migration that mis-handles legacy rows silently corrupts history") turned out not to exist as a separate code path to get wrong — there's no re-walk to mis-handle.

**Which assumptions broke:**

- `AC-2` predicted harness count 339→343 (one `Check()` per fixture). Actual is 339→344. `A69d` carries two `Check()`s — the D-3 (a) fallback claim and the AC-8 migration-byte-identity claim — because folding them into one compound assertion would make a failure ambiguous about which half broke, and the spec's own §7 separately calls AC-8 "THE LOAD-BEARING ONE," which reads as wanting it distinguishable on its own. Flagged rather than forced to match the predicted count.

**Constraint that nearly conflicted:**

- `AC-5`'s literal grep target (`existingTs`) versus the instinct to rename the variable now that it holds a composite string key rather than a bare `DateTime`. Resolved by keeping the name `existingTs` (with a comment explaining why) and reading `AC-5` literally, since it's phrased as an exact command with exact expected output. Worth confirming this is the intended reading — a renamed variable with a correspondingly updated `AC-5` command would have been equally defensible if that had been the intent.

---

## 4. What was not verified, and cannot be from this session

- **§6.1's rendered-count effect against a real book.** Not measured — no live `analysis_log.csv`/`analysis_eval_cache.csv` with a genuine same-second identity collision exists in this session's environment (no AWS copy-back available). The synthetic fixtures (`A69a`–`A69c`) are the substitute proof of what the count-shape change looks like; the production corpus, per the spec's own §2.4 figure (carried, not re-measured this session), currently has zero such collisions, so the change is correct but dormant on the live tape today.
- **The duplicate-timestamp rate itself (§2.4).** Carried from the spec, not re-measured — stated plainly rather than silently reused as if fresh.
- **`SignalId`'s per-`InstanceId` reset (§0.1 Trap 2) — this one WAS re-verified**, by reading `Core/ProcessIdentity.vb` directly: `_signalId` is a single `Shared Long`, minted once per process, touched only via `Interlocked.Increment`/`Interlocked.Read`. Named here because the convention asks which claims were checked and how, not just which were carried.
- **Live end-to-end `InitialiseAsync` run against a real OHLC fetcher.** Not exercised — consistent with the rest of this file's fixture family, which drives the pure extracted functions instead (see §2's third bullet).
