# Batch summary — DR-3 (downtime-repair follow-up)

**Source brief:** [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §3 (DR-3), §4 (common to all three). **Build under repair:** `c6c6942` (hole-derived repair windows, Part A). **This session carries DR-3 alone, per the brief's own §0.1 split — DR-1 + DR-2 were a separate conversation and are already merged as working-tree fixes** (see [`downtime-repair-followups-spec-back.md`](downtime-repair-followups-spec-back.md), reviewed and accepted 2026-08-13).

**Model / effort used:** Sonnet, medium, matching the brief's own recommendation. The escalation trigger ("if the two callers genuinely need different return semantics … stop and put the question back") did **not** fire — see below, it turned out there is only one real caller.

**Status: code + fixture + gate done. NOT committed** — this session does not commit per CLAUDE.md's "only commit when the user asks" rule; the user asked for implementation + spec-back, not a commit.

---

## Step 1 — what `BackfillAllAsync` does with the return value (this decided the scope)

**File:** [`tools/BacktestRunner/HistoricalStore.vb`](../tools/BacktestRunner/HistoricalStore.vb):575 (pre-edit line number; unchanged by this session's diff).

```vb
For Each m In EnumerateMonths(warmupStart, toUtc)
    Await BackfillTradeMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
Next
```

**The return value is not captured at all** — no assignment, no logging, nothing. Contrast with the candle backfill three lines above it in the same function, which *does* capture and log its count (`Dim n As Integer = Await BackfillCandleMonthAsync(...)` → `Console.WriteLine(... n ...)`). The trade backfill call was written differently and discards the value outright.

**Consequence for scope:** the brief's escalation trigger — "if the two callers genuinely need different return semantics" — presumes a real second consumer. There isn't one. `TradeStoreGapRepair.RepairOnceAsync` is the only caller that reads the return value at all. So the fix did not need a mode flag, a split entry point, or a decision put back to the trader — the brief's own step 1 instruction (read the call site before touching the contract) resolved the only open question by itself.

---

## What changed

### `tools/BacktestRunner/HistoricalStore.vb`

- `BackfillTradeMonthAsync`'s empty-window early return: `Return CountDataRows(path)` → `Return 0`, with a comment recording the step-1 finding above so a future reader does not have to re-derive it.
- Deleted the now-unread `Private Shared Function CountDataRows` — its only caller was the line just changed. Confirmed zero other references (`grep -rn "CountDataRows"` — no `.vb` hits outside this file, and none there after the edit) and confirmed by a clean 0-warning build (an unused-but-still-called-elsewhere function would not have produced a build warning either way in VB.NET, so the grep is the check that matters, not the warning count).

### `TradeStoreGapRepair.vb` — **not edited**

Per the brief's own trap 2 ("fixing the LOG STRING instead of the VALUE"): checked `TotalRowsRepaired`'s summary (*"Total rows appended by repair passes this process"*) and `RepairOnceAsync`'s log line (*"{0} row(s) appended to {1}"*) by reading them directly. **Both already describe the correct thing** — they were only ever wrong because the number flowing into them was wrong. Fixing the value at its source (`BackfillTradeMonthAsync`) makes both correct with no text change, so none was made. Verification handle 3 in the spec-back is the read that confirms this rather than assuming it.

### `verify/ordercheck/Program.vb`

Per the brief's suggestion ("check whether `A48d` can carry the first assertion before adding a new fixture") — **`A48d` was extended, no new fixture added.** `A48d` already builds a 5-trade covered store and computes `c1 = -1` (case 1: repairing the exact captured window resolves to "nothing to fetch" via `ResolveResumeCursorMs`). The new case (4) reuses that exact store and window, but drives it through the *real* entry point — `HistoricalStore.BackfillTradeMonthAsync` itself, called with `.GetAwaiter().GetResult()` (the established pattern in this file for driving an `Async` production function from a synchronous fixture `Sub`) — and asserts the return is `0`.

This is safe to run live (no mock, no stub): with `clampToSegStart:=True` and the exact covered window, `ResolveResumeCursorMs` returns `-1` internally, `windows.Count` stays `0`, and the function returns before the fetch loop — so no network call ever fires. Confirmed by reading the function body, not assumed.

The existing case (4) (double-write / dedup) was renumbered to (5) so the file's in-source case numbers stay in the same order they execute — the new case was inserted before it, since running it after would leave 10 rows on disk (from the double-write) instead of 5, changing the pre-fix bug's demonstrated number without changing what it demonstrates.

---

## Mutation proof

| State | `A48d` result |
|---|---|
| Pre-fix (`Return CountDataRows(path)`) | **FAILS**: `n5=5(want 0)` — the sole failure in the entire 100+-fixture run, confirming the new assertion doesn't disturb anything else |
| Post-fix (`Return 0`) | **PASSES** |

Both runs are captured directly, not hand-traced: `dotnet bin/Debug/net8.0/OrderCheck.dll` was run against the tree before the `HistoricalStore.vb` edit, then again after.

---

## Gate tail (`tools/checks/verify-gate.ps1 -Mode prepush`, run after the fix)

```
build DeribitVerdictEngine.sln            OK
build tools/AutoTweaker/...vbproj          OK
build tools/WhatIfRunner/...vbproj         OK
build tools/CeilingAudit/...vbproj         OK
build tools/BacktestRunner/...vbproj       OK
build verify/ordercheck/OrderCheck.vbproj  OK
harness                                    OK  harness ALL PASS
display-parity                             OK  no snapshot/card drift detected
version-bump                               OK  no engine-path change
result                                     GATE PASSED
```

All six project builds Release, 0 warnings / 0 errors. **The version-bump / display-parity sections diff `base..HEAD` (the committed range), not this session's uncommitted working tree** — same caveat the DR-1+DR-2 review packet raised (its §5.2, C-1) — so "no engine-path change" describes the *previous* commits, not this session's edit to `HistoricalStore.vb`. What I verified directly instead: `tools/BacktestRunner/` is not one of the gate script's `$enginePrefixes` (`Core/`, `DynamicNorms.vb`, `analysis/`, read from `tools/checks/verify-gate.ps1` line 126) — a static fact about the script, true independent of commit state, not something the live gate run this session confirmed.

`git diff --stat -- settings.json` — empty, no keys added or changed.

---

## §15 row

Added per CLAUDE.md's hard rule and the brief's own §4 ("every commit that changes engine behaviour gets a §15 row … add the row, do not do the trim inside a store commit"). **Correction to an earlier draft of this summary:** I initially wrote that the DR-1+DR-2 commit (`91942d6`) skipped its §15 row in favour of the `[no-engine-change]` commit-message tag. That was wrong — checked by reading the row directly (`git show 91942d6 -- docs/DeribitIndicatorProject.md`), not assumed: it added one. The tag governs `verify-gate.ps1`'s version-bump nudge only; it says nothing about the §15 obligation, which is separate and still applies. Kept substantially shorter than that row (~350 words vs. ~700) — the DR-1+DR-2 review packet's own N-4 nit flagged that row's length as making an already-overdue archive trim more overdue, and this row does not need to repeat DR-1/DR-2's content to make its own point.

## Not done this session

- **DR-1 + DR-2** — already merged as working-tree fixes from the prior conversation; untouched here.
- **No commit.**
