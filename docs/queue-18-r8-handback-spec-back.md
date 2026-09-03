# Spec-back — queue item 18, R8 handback

**Date:** 2026-09-04. **Audience:** the reviewing seat (the orchestrator). **Delta reviewed against:** [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) §8 — R8, ruled (c) 2026-09-03, on top of the R7 handback (accepted, see the review response inline in §7).
**Not yet committed.** The change is in the working tree only, alongside the uncommitted R7 fix.

---

## No fixture was possible, and here is why — not that none was needed

**The path R8 guards cannot be made to fail today, and I did not try to make it fail.** The brief's own first paragraph said so before anything else, and it held: `ClassifySpan` has seven `Return` statements and none of them emits `HourClass.OutOfScopeWeekend` — that value is produced in exactly one place in the whole file, `ClassifyHour`'s pre-split path at `:630`, which the split path never calls into. So every span the combine ever sees carries one of the six classes the `Else` chain already handles, `spans.Where(Classification = finalCls)` is therefore never empty, and the guard I added can never fire against any input reachable through `ClassifyHour`'s public surface.

Constructing a red case would require editing `ClassifySpan` itself to emit a value it structurally cannot produce today — which is exactly the trap the brief named: *"if you find yourself editing `ClassifySpan` to manufacture a red, stop."* I didn't. There is no fixture for this delta, and the reason isn't "the case is rare" (that was `A61f`'s situation, and a fixture was still possible and still cheap there) — it's that the case is **provably unreachable given the current producer**, which is a different and stronger statement. A fixture that had to first break `ClassifySpan` to exercise the guard would be testing my own sabotage, not the guard.

---

## Acceptance, item by item

| | Check | Result |
|---|---|---|
| 1 | Harness reads exactly `317 PASS, 0 FAIL, ALL PASS` — unchanged | ✅ **Confirmed.** `dotnet run --project verify/ordercheck/OrderCheck.vbproj --no-build` → `PASS: 317  FAIL: 0`, last line `ALL PASS`. No fixture added, none removed, none moved — a pure parity result on the same 317. |
| 2 | The named-failure guard is present, and `InstanceId` still resolves as before | ✅ **Present:** `winner = spans.FirstOrDefault(Function(s) s.Classification = finalCls)` followed by `If winner.Classification <> finalCls Then Throw ... "finalCls=" & finalCls.ToString() ...`. **`InstanceId` unchanged:** `result.InstanceId = winner.InstanceId` is the same line, reading the same field off the same `winner` value — all 317 fixtures passing unchanged is the parity evidence for this, since several of them (`A49o`–`A49w`, `F1d`, `A61e`, `A61f`) assert on split-hour `InstanceId`/`Classification` values that would have moved if `FirstOrDefault`'s behavior differed from `First`'s on any reachable input. |
| 3 | R7's `TrailingMsForHour` line is **UNCHANGED** | ✅ **Confirmed by diff**, not by inspection alone. `git diff tools/BacktestRunner/CoverageReport.vb` shows the entire R8 edit as one contiguous hunk ending *before* the `TrailingMsForHour` assignment — the line itself does not appear in the diff at all. Re-grepped it directly post-edit: `result.TrailingMsForHour = spans.Where(Function(s) s.Classification = finalCls).` / `Select(Function(s) s.TrailingMs).Max()` — byte-identical text to the R7-committed version, just shifted down in line number by the guard block inserted above it. I did not re-point it at `winner`; it still filters `spans` independently, exactly as R7 left it. |
| 4 | `tools/checks/verify-gate.ps1` green | ✅ `GATE PASSED` — harness `ALL PASS`, display-parity `no snapshot/card drift detected`, version-bump `no engine-path change`. |
| 5 | No new fixture | ✅ None added. Reason stated above, not "none was needed" but "none was possible without manufacturing the precondition myself." |

---

## What I did not verify, and cannot

- **Whether the guard's error message reads well in practice.** It has never fired and — per the brief — cannot fire against any input `ClassifySpan` can currently produce. Its wording is reviewed by eye only.
- **Whether a future change to `ClassifySpan` that adds an `OutOfScopeWeekend` return would actually hit this guard rather than some other unhandled path first.** R8's own finding traces the current producer; I did not audit every future-hypothetical way `ClassifySpan` could be extended, only confirmed today's seven `Return`s are exactly as R8 described.
- **Committing or pushing** — this sits on top of the uncommitted R7 fix in the working tree; neither has been committed.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
