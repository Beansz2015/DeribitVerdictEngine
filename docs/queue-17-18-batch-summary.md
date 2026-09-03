# Batch summary — queue items 17 and 18

**Date:** 2026-09-03. **Author:** the implementing seat. **Audience:** the trader, who relays this.
**Specs built:** [`fixture-literal-provenance-a14b-spec.md`](fixture-literal-provenance-a14b-spec.md) (item 17) · [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) (item 18).
**Reviewing-seat packet:** [`queue-17-18-batch-spec-back.md`](queue-17-18-batch-spec-back.md) — read that one, not this one, before touching the code.

---

## 0. Top line

Both items were **rulings hiding in build rows** ([`trader-tick-queue.md`](trader-tick-queue.md) §0a). Both rulings landed from the trader the same day (2026-09-03) and both builds are **done the same day**: harness `ALL PASS`, `tools/checks/verify-gate.ps1` **GATE PASSED**, on the working tree — **not yet committed.**

| | Item 17 | Item 18 |
|---|---|---|
| Defect | `A14b` pinned a fixture literal (`0.21`/`0.105`) that half-matched shipped `settings.json` (`0.21`/`0.06`) and half didn't, with nothing saying which half was deliberate | `BuildResult` measured a split hour's trailing gap against the WHOLE HOUR's last trade, even when the deciding (TrailingEdge) span was not the last span — could report a gap measured past a span that had legitimately stopped capturing |
| Ruling | MECHANISM, option (c): keep the literal, add a provenance comment, make the values obviously synthetic | PER-SPAN: `ClassifyHour` already selects the deciding span: return its own bounded figure structurally, `BuildResult` maxes over it |
| Files touched | `verify/ordercheck/Program.vb` only | `tools/BacktestRunner/CoverageReport.vb` + `verify/ordercheck/Program.vb` |
| New fixtures | none (by design — the seven existing consumers are the coverage) | `A61a`–`A61e` |

---

## 1. Item 17 — `BuildResolutionCfg` fixture literals

**Change**, `verify/ordercheck/Program.vb`:

- `:1084-1090` — `BuildResolutionCfg`'s ROC-override literals for the `"3"` resolution profile: `RocMagnitudeThreshold` `0.21`→**`0.50`**, `RocSlopeDeltaThreshold` `0.105`→**`0.25`**. A six-line comment at the call site declares MECHANISM and says why (shipped is `0.21`/`0.06`; these must never be read as a claim about it).
- `:1121-1125` — `A14b`'s check name and its two resolution-3 assertions updated to match (trap T2: the printed name must match what's asserted).
- **Untouched, by design:** the resolution-1 literals (`0.1`/`0.05`) — POCO defaults, a different class, explicitly scoped out (R4).

**Verification:**

- `grep -n "0.105" verify/ordercheck/Program.vb` → no matches.
- All seven consumers (`A14a`, `A14b`, `A14d`, `A14e`, `A14i`, `A14j`, `A15a`) re-run and pass unchanged.
- Harness `ALL PASS`, gate `PASSED`.

---

## 2. Item 18 — `ObservedLongestTrailingMs` per-span

**Change**, `tools/BacktestRunner/CoverageReport.vb`:

- `HourResult` gains one nullable field: **`TrailingMsForHour As Long?`** — the deciding span's own trailing gap, `Nothing` unless `Classification = TrailingEdge`.
- `ClassifySpan`'s return tuple gains **`TrailingMs As Long?`** — it already computed the span-bounded gap internally (for the `Reason` string); this surfaces it structurally instead (T1: never scrape a display string).
- `ClassifyHour` propagates `TrailingMs` into `HourResult.TrailingMsForHour` on both the non-split path and the split path (the split path via the winning span in the worst-of combine — unchanged precedence, F1 D-5.1).
- `BuildResult`'s trailing-figure block (`:1071-1094` pre-change) now maxes over `hr.TrailingMsForHour` instead of recomputing from the whole-hour `HourStoreStats`. The misleading comment claiming parity with the sibling counters is replaced with the actual reasoning.

**New fixtures**, `verify/ordercheck/Program.vb` (family **A61**):

| Fixture | Level | Proves |
|---|---|---|
| `A61a` | integration, via `BuildResult` | The defect is real and the fix is correct: buggy value `3,299,999ms` (to hour end) vs. fixed `1,499,999ms` (to span 0's own end) |
| `A61b` | unit, via `ClassifyHour` | `HourResult.TrailingMsForHour` reports the span's own figure directly |
| `A61c` | unit | R4's guard — an UNSPLIT hour's per-span figure equals today's whole-hour figure exactly |
| `A61d` | unit | The D-2/D-3(c) nullable-`LastTsMs` guard holds per-span, same as it always did whole-hour |
| `A61e` | unit | F1 D-5.1 precedence untouched — a trailing span still beats a clean span, never laundered to Captured |

**Before/after proof (acceptance item 2):** temporarily reverted only the `BuildResult` block (kept the structural field), rebuilt, ran — **`A61a` FAILED**, reporting `observedLongestTrailingMs=3299999` against the correct `1499999`. Restored the fix, rebuilt — **`A61a` PASSED**. Full harness re-run `ALL PASS` after restoring.

**Real-data check (acceptance item 7):** ran `coverage --evidence-dir` against the 2026-09-01 AWS copy-back (`AWS-copybacks/aws-copyback-2026-09-01/aws_fetch/20260901-153838`), full captured range 2026-07-20→2026-09-01, before and after the fix.

- **Result: no change.** `longest trailing 3598.7s (1 trailing-edge hour(s))`, identical both times.
- **Why:** the sole trailing-edge hour in that window (2026-08-18 09:00) is not a split hour (its `Reason` carries no `split@` prefix). The dataset does contain split hours elsewhere (e.g. 2026-08-07 16:02:41), but none of them happen to also be the trailing-edge hour. Per the spec's own §6: *"no split hours in the sample means no change, which is evidence the guard works, not evidence the fix is absent."*

**T3 (rendered-surface check):** confirmed by diff inspection — the change never touches `BuildConsoleSummary` or `BuildMarkdown` (both untouched hunks in the diff), so no rendered byte moves. Not asserted by a new fixture; the diff itself is the evidence.

---

## 3. Verification run (both items, combined)

```
dotnet build verify/ordercheck/OrderCheck.vbproj        → Build succeeded, 0 warnings, 0 errors
dotnet run   verify/ordercheck/OrderCheck.vbproj         → ALL PASS
tools/checks/verify-gate.ps1                             → GATE PASSED
  harness ALL PASS
  display-parity: no snapshot/card drift detected
  version-bump: no engine-path change
```

No settings key added, no `settings.json` version bump, no `docs/DeribitIndicatorProject.md` §15 entry — both items are test-only / tools-only per their own R5/R6.

---

## 4. Docs updated in this batch

- [`trader-tick-queue.md`](trader-tick-queue.md) — state banner, the §0a "two decisions hiding" box, and item 17's own §2 row all marked closed, original text kept for the record.
- [`fixture-literal-provenance-a14b-spec.md`](fixture-literal-provenance-a14b-spec.md) and [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) — status lines updated SPEC → BUILT.

**Not done:** no commit, no push. The working tree carries both changes uncommitted, pending review.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
