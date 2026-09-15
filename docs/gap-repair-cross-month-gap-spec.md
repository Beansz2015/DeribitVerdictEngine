# Gap repair — cross-month leading gap (`F-1`) — SPEC

> ✅ **RULED 2026-09-14 (UTC), trader via the orchestrator (`deribitverdictengine-a3`): option (a), fix now; the S2 deploy holds for it.** Written and built in one session, as the orchestrator allowed when no decision row below is reserved. Build record: [`gap-repair-cross-month-gap-build-spec-back.md`](gap-repair-cross-month-gap-build-spec-back.md). Code handles pinned to `6c87e19`.
>
> ⚠ **Revised during the build, before any commit:** the first draft added a `CommitFromMs` partition for edge (4). Reading the pass order showed it redundant — the pass repairs months in ascending order, so the two ranges are already disjoint — and it would withhold trades when the previous month's tail fails. §2 and `CF-4` below describe the built design; the build spec-back records the revision.

**IDs, defined once:**

- `F-1` — the orchestrator's finding in its review of `edd4539`: a leading gap at the start of a month file is not repaired when an outage crosses 00:00 UTC on the 1st. Queue row: [`trader-tick-queue.md`](trader-tick-queue.md) §2, "Cross-month leading gap not repaired".
- `CF-1`–`CF-5` — this spec's decision rows (§3). `CF` = cross-month fix. `X-1`–`X-4` — this spec's implementer traps (§0).
- `GT-3` — the trap in [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §0: a tail must stop at its segment end, or a month-boundary pass double-writes.
- `A79h` — the harness fixture that pinned `F-1` (`e13e7cf`). `A79i`–`A79k` — new fixtures.
- "Decision 2" — [`gap-repair-same-ms-page-skip-spec-back.md`](gap-repair-same-ms-page-skip-spec-back.md) §5.3 row 2: hole seq ranges get no time clamp.

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH · one session.** Live tape path, same class as `edd4539`.

**Why that tier:** the change is one bracket seed, but a wrong seed invents a phantom hole that re-fetches rows already stored, and the no-double-write property rests on the pass order, not on local code. Neither shows in a single-file fixture.

**Where it slips:**

| # | Trap | Input that exposes it |
|---|---|---|
| X-1 | Seeding the bracket after a truncated scan. The dropped rows sit between the seed and the retained rows, so the walk reports ground the store holds as a hole | `A79k`: small scan cap, September truncated, August newest row present |
| X-2 | Taking the previous file's newest SEQ-CARRYING row instead of its newest row. A newer legacy row then gets bracketed across | `A79i` part 2 |
| X-3 | Seeding from any row but the previous file's NEWEST. The hole then reaches back over trades the previous month's tail just repaired, and writes them twice | `A79j` part 1 |
| X-4 | Counting a gap twice: once in the previous month's tail, once in the cross-month hole | `A79j` part 2: pass `not_served` must be 2, not 3 or 4 |

**Escalation:** stop if the fix needs a change to `ScanForRepair`, `AppendRows`, `DedupTrades` or `ResolveResumeCursorMs`, a settings key, or any `repair_status.log` format change.

---

## 1. The defect — verified

`ScanForRepair` reads ONE month file. Its bracket is the newest row below `segStartMs` in that file. The September file never holds an August row, so when streaming has written September's first rows, the range from August's last stored seq to September's first stored seq is a hole in neither scan. August's tail stops at August's end (`GT-3`). September's tail starts after September's newest row. Pre-existing — verified by reading `2d52fb8`. Pinned by `A79h` part 1: 0 of 9 leading-gap trades repaired.

## 2. The fix

In `TradeStoreWriter.ResolveRepairWindows` (new optional `previousMonthPath`; the cap-parameter core is `ResolveRepairWindowsCore`):

1. **Seed** — when all of these hold: the scan was not truncated, the current file has at least one row at or after `segStartMs`, it has no row below `segStartMs`, and a previous-month path was given → add the previous file's newest row (by timestamp, then seq) as the bracket. It is read by calling `ScanForRepair(previousMonthPath, segStartMs, …)` unchanged: every previous-month row sits below `segStartMs`, so it returns exactly that one bracket row.
2. **Walk** as today. A legacy seed breaks the walk, as trap 2 does today.
3. **No partition.** ⛔ **The ORDER INVARIANT:** a repair pass resolves months in ascending order, one after another (`HistoricalStore.EnumerateMonths`, walked sequentially by `TradeStoreGapRepair.RepairOnceAsync`). The previous month's tail has already committed when the seed is read, so the seed is that month's newest row AFTER repair, and the cross-month hole starts exactly where that tail stopped. If the previous month's tail failed, the seed is its unrepaired newest row, and the hole commits those trades itself, once. Stated in comments at the seed and at the month loop ("do not parallelise"); pinned by `A79j` parts 1 and 4.

`HistoricalStore.BackfillTradeMonthCoreAsync` passes the previous month's file path on the repair path only.

**The five edges the orchestrator named:**

| Edge | Handling |
|---|---|
| (1) Previous file absent, or no row | `ScanForRepair` returns nothing → no seed → today's behaviour |
| (2) Previous file's newest row is legacy | The seed carries `AbsentSeq` → the walk breaks → no hole, as trap 2 |
| (3) Lookback clamp | Only ONE previous-file row enters the walk, so the scan is not widened and the previous file's internal gaps are not emitted; the hole's seq range stays unclamped (decision 2) |
| (4) No double-write against the previous month's tail | The ORDER INVARIANT: the seed is read after the previous month's tail ran, so the ranges are disjoint and each seq is fetched once |
| (5) Truncation | No seed after a truncated scan, exactly as a same-file bracket is dropped |

## 3. Decision table

| # | Question | Options | Read | Class |
|---|---|---|---|---|
| **CF-1** | Where does the month-boundary gap get fetched? | **(a)** a cross-month hole in the CURRENT month's scan, seeded from the previous file · **(b)** the previous month's tail gets `end_seq` = the current file's first stored seq − 1 and loses its `GT-3` time stop | **(a).** (b) does nothing when the lookback starts inside the current month — the previous month is then not in the pass at all. Mechanism | Step 3, taken |
| **CF-2** | Which previous-file row is the seed? | **(a)** its newest row, legacy or not · **(b)** its newest seq-carrying row | **(a).** (b) brackets across a newer legacy row and invents a phantom hole over ground the store holds — trap 2's own reasoning. Mechanism | Step 3, taken |
| **CF-3** | Seed when the current file is EMPTY? | **(a)** no — keep the anchored tail at `segStartMs` · **(b)** yes | **(a).** The anchored tail already fetches every trade from `segStartMs` (`A79h` part 2: 9 of 9). No information is traded | Step 1 — no richer option, taken |
| **CF-4** | How is double-writing against the previous month's tail prevented? | **(a)** rely on the ascending month order, stated and pinned · **(b)** a `CommitFromMs` partition: the cross-month hole commits only current-month trades | **(a).** Under (b), when the previous month's tail fails, the partition withholds trades no other window commits in that pass — less recovery — and it guards a double-write that ascending order already prevents. Mechanism | Step 3, taken. ⚠ The invariant lives in a loop, not in the resolver; it is named at both |
| **CF-5** | When the previous month is NOT in the pass, commit the cross-month hole's previous-month trades? | **(a)** yes — the whole seq range, like any unclamped hole · **(b)** no | **(a).** The richer option, and consistent with decision 2; no window in the pass would commit them otherwise | Step 1 — richer option, taken |

**No row is reserved.** No settings key, no CSV or schema change, no do-not-touch signature change, no `repair_status.log` format change.

## 4. Fixtures

| ID | Asserts | Mutation that must fail it |
|---|---|---|
| **`A79h`** (flipped from pin to guard) | September already written: the cross-month hole `[S+1, S+9]` repairs 9 of 9, none written twice · September empty: 9 of 9 | Skip the seed's `rows.Add` → 0 of 9. **Fail-first: run against `6c87e19`'s resolver** |
| **`A79i`** | Edge (1): previous file absent → tail only. Edge (2): previous file's newest row is legacy → no hole | Replace the seed read with the previous file's seq-carrying rows (`ReadTradeFile(…).Where(HasSeq)`) → part 2 reports a phantom hole |
| **`A79j`** | Edge (4): August's tail repairs 3, September's hole starts after it and repairs 3, every seq once · one missing seq each side → pass `not_served` = 2 · August's tail fails → the hole repairs all 6, once · edge (3): lookback inside September → hole unclamped, all 19 committed, August's internal gap not emitted · ⚠ part 5 hazard pin: resolving September before August's tail runs writes 3 rows twice | Replace the seed read with the previous file's OLDEST row → August's repaired trades written twice |
| **`A79k`** | Edge (5): a truncated current scan is not seeded → no phantom hole (control: untruncated and contiguous → no hole either) | Seed despite truncation → a phantom hole over rows the cap dropped |
| `A79c` part 4 | Unchanged: two windows. August's tail repairs up to 920010 first, so September's seed is 920010 and there is no hole | — |

## 5. Not verified before the build

- **The cost of reading the previous month file.** One extra full scan per pass, only while the lookback straddles a month start (about 5 passes a month); about 1.86 M rows for August. Not measured on the box.
- **`ScanForRepair` opens files with a plain `StreamReader`.** Reading the previous month file right after 00:00 can collide with a late streaming flush — the pre-existing B-3 class in `CoverageReport.ReadStoreWindow`'s note. `ScanForRepair` is on the do-not-touch list, so this is named, not changed.
- **The ORDER INVARIANT has no fixture on `TradeStoreGapRepair.RepairOnceAsync` itself** — `OrderCheck` does not link that file. The fixtures drive the same ascending loop through `BackfillTradeMonthCoreAsync`.
