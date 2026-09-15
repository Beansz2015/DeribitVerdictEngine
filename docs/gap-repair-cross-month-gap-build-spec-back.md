# Gap repair — cross-month leading gap (`F-1`) — build spec-back

**Written:** 2026-09-14 (UTC) by the build seat. **For:** the orchestrator seat `deribitverdictengine-a3`. **Spec:** [`gap-repair-cross-month-gap-spec.md`](gap-repair-cross-month-gap-spec.md) (`2b57411`). **Record:** commit `165f780` and the extended gap-repair row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15. **Handles pinned to:** `165f780`.

**Review recommendation**
Model / effort: **Opus · high.** Live tape path, and the no-double-write property rests on the pass order rather than on local code.

**IDs, defined once:**

- `F-1` — the orchestrator's finding: a leading gap at the start of a month file is not repaired when an outage crosses 00:00 UTC on the 1st.
- `CF-1`–`CF-5` — decision rows in [`gap-repair-cross-month-gap-spec.md`](gap-repair-cross-month-gap-spec.md) §3. `X-1`–`X-4` — its implementer traps (§0).
- `A79h`–`A79k` — the harness fixtures for this fix. `MA`, `MB2`, `MC2`, `MD` — this packet's mutation runs (§1, `E-2`).
- "The ORDER INVARIANT" — the repair pass resolves months in ascending order, one after another, so the previous month's tail has run before the current month's seed is read.

---

## 1. Ranked verification handles

⭐ **If you only run one, run `H-1`.**

| # | Run this | Proves | Output at `165f780` |
|---|---|---|---|
| **H-1** | `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release` then `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build`, grep `A79` and `ALL PASS` | The guard and every edge fixture pass; the whole harness is green | 393 `PASS`, `ALL PASS`; `A79a`–`A79k` all `PASS` |
| **H-2** | `git diff --stat 6c87e19 165f780` | The change set: no do-not-touch file, no settings, no log format | `Core/TradeStoreWriter.vb` · `TradeStoreGapRepair.vb` (comment only) · `tools/BacktestRunner/HistoricalStore.vb` · `verify/ordercheck/Program.vb` · `docs/DeribitIndicatorProject.md` |
| **H-3** | `git diff 6c87e19 165f780 -- Core/TradeStoreWriter.vb` and grep the changed lines for `Function ScanForRepair`, `Function AppendRows`, `Function DedupTrades`, `Function ResolveResumeCursorMs` | No do-not-touch function changed | *(empty)*. `ScanForRepair` is CALLED for the seed, unchanged |
| **H-4** | `powershell -File tools/checks/verify-gate.ps1` | Build, harness, display parity, rotation riders | `GATE PASSED` — AutoTweaker, WhatIfRunner, CeilingAudit, BacktestRunner, OrderCheck built; harness `ALL PASS`; no snapshot/card drift; no header rotation (run after `165f780`) |
| E-1 | The `A79h` guard run against the resolver at `6c87e19` (`edd4539`'s code), before the fix | **Fail-first** | `FAIL  A79h … p1=False(windows=True sepRows=5 gapFound=0 states=TAIL_OK,TAIL_OK) p2=True(sepRows=14 gapFound=9 anchorCalls=1)` |
| E-2 | Mutations from a scratch backup of `Core/TradeStoreWriter.vb`; restored, `RESTORED: md5 identical` both rounds | Each fixture admits its failure | Table below |
| E-3 | Release builds of `OrderCheck`, `BacktestRunner`, `DeribitVerdictEngine.sln` | Every project linking a changed file compiles | 0 errors, 0 warnings each |

**E-2 — mutation runs.**

| Run | Mutation | Result |
|---|---|---|
| MA | skip the seed's `rows.Add` | `A79h` FAIL — sepRows=5 gapFound=0 (also `A79j`) |
| MB | skip `Seq < 0` inside the seed loop | ⚠ **`A79i` PASSED — the mutation was a no-op.** `ScanForRepair` returns only the previous file's single newest row, so the loop never sees an older seq row |
| MB2 | replace the seed read with the previous file's seq-carrying rows | `A79i` FAIL — p2 n=2 firstKind=Hole |
| MC | flip the seed loop's `>` to `<` | ⚠ **`A79j` PASSED — a no-op, for the same reason** |
| MC2 | replace the seed read with the previous file's OLDEST row | `A79j` FAIL — p1 aug=7 (August's repaired trades written twice), p2 notServed=3, p3 committed=21 (also `A79c`, `A79i`) |
| MD | seed despite truncation | `A79k` FAIL — cut n=2 firstKind=Hole firstSeq=960001 |

⚠ **`MB` and `MC` were the first mutations named in the spec, and both passed.** The fixture comments now name `MB2` and `MC2`, and record why the loop-level flips cannot fail. **Consequence for review:** traps `X-2` and `X-3` are closed structurally by reusing `ScanForRepair` for the seed read, not by the loop. A future edit that replaces that call reopens both — `A79i` part 2 and `A79j` part 1 catch it.

---

## 2. Decisions taken, and decisions queued

### 2.1 Decisions taken — one line each

- ⛔ **Design revised before any commit: the `CommitFromMs` partition was built, then removed.** Ascending month order already makes the cross-month hole start where the previous month's tail stopped. The partition guarded a double-write that cannot happen in the pass, and when the previous month's tail failed it would have withheld trades no other window commits. The spec records the revision at its top and in `CF-4`.
- **The ORDER INVARIANT is stated at both ends:** at the seed in `ResolveRepairWindowsCore`, and at the month loop in `TradeStoreGapRepair.RepairOnceAsync` ("do not parallelise this loop").
- **`A79j` part 5 is a hazard pin, added beyond the spec:** resolving September before August's tail runs writes 3 rows twice. It makes the invariant's load visible from the caller's side.
- **`A79j` part 4 added:** August's tail fails, and the cross-month hole repairs all 6 trades itself, once.
- **The seed read reuses `ScanForRepair` unchanged** — on the do-not-touch list; called, not modified.
- **`ResolveRepairWindows` gains `Optional previousMonthPath`; the cap-parameter overload is `ResolveRepairWindowsCore`** — the `ScanForRepair` precedent, so `A79k` reaches truncation with 16 rows.
- **`A79c` part 4 unchanged at two windows:** August's tail repairs first, so September's seed leaves no hole.
- **`DeribitIndicatorProject.md` §15:** the existing gap-repair row extended (one item, one row). Summary cell 923 B, measured with `wc -c`; it was 1,192 B by the same method before this edit.
- **Commits:** spec `2b57411` (`[no-engine-change]`), code `165f780` (engine change, no token).

### 2.2 Queued for the orchestrator

**None.** No `CF` row was reserved (spec §3). No settings key, no CSV or schema change, no do-not-touch signature change, no `repair_status.log` format change.

---

## 3. Feedback on the spec

- ⭐ **The spec's first design was wrong, and reading the caller caught it, not the fixtures.** The partition passed its own fixtures. Only asking "who else writes this range in the same pass" showed it was redundant and occasionally harmful. A fix touching a range another window also touches needs the caller's order stated before the design.
- **Two named mutations could not fail** (`MB`, `MC`), because they targeted a loop whose input is already one row. A mutation must target the decision that is actually taken — here, the seed READ.
- **The orchestrator's edge (3) wording, "must not widen the scan", maps to one concrete property:** the previous file's internal gaps are never emitted. `A79j` part 3 asserts it.

---

## 4. What I did not verify

- **The cost of the extra previous-month scan on the box** — about 5 passes a month, about 1.86 M rows each. Not measured.
- **`TradeStoreGapRepair.RepairOnceAsync` itself** — no fixture links it. The ORDER INVARIANT is pinned through the same ascending loop in `BackfillTradeMonthCoreAsync`.
- **The `StreamReader` collision class (B-3)** when reading the previous file right after 00:00 — pre-existing in `ScanForRepair`, named, not changed.
- **A real month rollover on the live box.** Fixtures only.
- **Not deployed.** The S2 deploy holds for this build.
