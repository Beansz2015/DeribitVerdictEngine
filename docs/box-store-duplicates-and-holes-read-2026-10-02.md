# Box store — duplicate rows and holes — READ

**Written:** 2026-10-02 (UTC) by the orchestrator seat, on the trader's request after history comparison #1. **Read-only:** no `.vb` file and no store touched.
**Source:** fetch `aws_fetch/20260928-121255/` (gitignored; copied back 2026-09-28 12:12 UTC) and the dev history store `C:\DeribitData\history\` (21-month backfill, 2026-10-02). **Window:** 2026-07-23 → 2026-09-28 (exclusive), the window of comparison #1.
**Instrument:** [`audits/proofs/box-store-dups-holes-2026-10-02/read.py`](audits/proofs/box-store-dups-holes-2026-10-02/read.py), run at `1e20db3`; full output in [`output.txt`](audits/proofs/box-store-dups-holes-2026-10-02/output.txt) beside it.

**What triggered it.** Comparison #1 ([`history-store-backfill-runbook.md`](history-store-backfill-runbook.md) step e3) passed, but printed two large box-store counts:
- 6,205,563 box rows − 5,242,633 distinct seqs − 281,573 seq-less rows = **681,357 duplicate rows**;
- **1,029,591 trades only in the dev store** (box holes).

The trader asked why the duplicates exist, and whether the holes come from the venue outage.

**IDs used here:**

| ID | Source and kind | Meaning |
|---|---|---|
| `DUP-1` · `DUP-2` | [`trade-store-duplicate-rows-read-2026-09-15.md`](trade-store-duplicate-rows-read-2026-09-15.md) §7, decision rows | Fix the whole-lookback rewrite · fix direction. Both shipped in `584c616`, deployed 2026-09-21 15:38 UTC (instance `ee159d03…`) |
| `A79g` | Harness fixture (`edd4539`) | Measures a repair tail double-writing trades the streaming writer holds unflushed |
| `GR-1` | [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md), decision row | Repair by `trade_seq` range. Fixes the 1,000-row page skip. Shipped in the same 2026-09-21 deploy |
| `BSR-1` | This read, an auto-proceeded decision | The runbook's comparison window starts 2026-08-01 from now on (section 4) |
| `H-1` | This read, a handle | `python docs/audits/proofs/box-store-dups-holes-2026-10-02/read.py` |

---

## 0. Verdict

> ✅ **Both counts are fully explained. Each class sums exactly to its total. No new defect.**
>
> - **Duplicates:** 99.94 % come from one known defect, the whole-lookback rewrite (`DUP-1`). It was fixed on 2026-09-21. **0 duplicate rows were written under instance `a19acc4d…`** (2026-09-25 13:29 → the fetch).
> - **Holes:** ⚠ **the venue outage explains only 256,110 of 1,029,591 (24.9 %).** The largest class, 594,024 trades, is **not a hole**: it falls before box capture began. The rest are known, dated, and since fixed or recorded.
> - **After 2026-08-17 16:23 UTC, the box store's only hole is the 2026-09-18 → 09-20 outage.**
> - **Neither count touches the dev store.** It has 0 duplicates and 0 missing seqs (pull step e1, deep status).

---

## 1. Duplicate rows — 681,357

| Class | Rows | Share | When | Status |
|---|---:|---:|---|---|
| **Whole-lookback rewrite** (`DUP-1`): a repair pass whose store scan failed re-fetched and re-appended its whole 20 h lookback | **680,967** | 99.94 % | 7 passes under `3fe57c53…`: 09-10 09:48 · 09-10 21:48 · 09-11 15:48 · 09-16 21:48 · 09-17 03:48 · 09-17 09:48 · 09-17 21:48 | Fixed in `584c616`, deployed 2026-09-21 |
| **Repair tail over unflushed streaming rows** (the `A79g` mechanism) | **327** | 0.05 % | 2 passes under `ee159d03…`: 09-23 15:39 (167 rows) · 09-24 03:39 (160 rows) | Live by design of `edd4539`. Readers dedupe. 0 under `a19acc4d…` |
| Small blocks, under 100 rows each (20 blocks) | **63** | 0.01 % | 6 instances, 08-17 → 09-25 | Mostly the `RR-1` repeat fills ([`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md)); fixed 2026-09-25. Not attributed row by row |
| **Total** | **681,357** | | | |

- **The 2026-09-15 read counted only the first 3 rewrite passes.** Its source fetch ended 2026-09-13. **The 4 later passes (09-16 → 09-17, 382,035 rows; with the first 3 passes' 298,932, that is 680,967) are new instances of the same defect, not a new defect.** Their shape is identical: each block's oldest trade sits 1,199.7–1,199.9 min before its append time (`gap_repair_lookback_hours` = 20), and each append time falls on the instance's 6 h pass grid (start 15:49:09).
  - Two passes split into two blocks each (631 + 110,990 rows; 9,761 + 97,422 rows). The split is a few rows the store genuinely lacked, the same shape the 2026-09-15 read saw.
- **The overlaps explain the multiple copies.** Copies per seq: 2 copies × 307,816 seqs · 3 × 87,545 · 4 × 66,145 · 5 × 4. Consecutive 20 h lookbacks on a 6 h grid overlap by 14 h, so 09-16 → 09-17 holds up to 4 copies.
- **The tail-overlap blocks, from `repair_status.log`:** on 09-23 the pass committed 183 = a 16-row hole + a 167-row tail. On 09-24 it committed 160 = a 160-row tail and no hole. Every tail row was already in the store. Each block's trades span the last ~30 s before the pass.
- **Not data loss.** Comparison #1 checked every box row, duplicates included: 0 field mismatches against the dev store, so every copy is identical.
- ⚠ **Why the 09-16 → 09-17 scans failed is still unknown**, the same gap the 2026-09-15 read names in its §3. Those passes ran in the hours before the 2026-09-18 thread-leak box-down. That timing is suggestive; I did not verify it.

## 2. Holes — 1,029,591 trades

| Class | Trades | Share | Span (UTC) | Evidence | Status |
|---|---:|---:|---|---|---|
| **Before box capture began** | **594,024** | 57.7 % | 07-23 00:00 → 07-31 21:49 | The box July file starts 07-31 21:49:57, unchanged in every fetch since 09-09. That is 20 h before the capture deploy (instance `5a3afd99…`, 08-01 17:49), so the first repair pass backfilled it | **Not a hole.** Comparison #1's window starts too early (section 4) |
| **WPAD venue outage** | **256,110** | 24.9 % | 09-18 02:01 → 09-20 15:38 | `repair_status.log` 09-21 15:40: `TAIL_PAST_RETENTION … not_served_before=256110` — an **exact** match | Lost on the box for good. Past the venue's repair retention |
| **Same-millisecond write-guard drop** | **138,817** | 13.5 % | 08-01 → 08-11 17:18, in 54,114 runs | Every trade sits in a single-millisecond run. For 138,810 of them the box kept another trade of that millisecond | Fixed by instance `a5d701ad…`, 2026-08-11 17:18 ([`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md)) |
| **Weekend EC2 stop** | **26,165** | 2.5 % | 08-08 08:34 → 08-09 13:59 | Trader-confirmed intentional stop ([`d3-asia-burst-watch-read-2026-08-10.md`](d3-asia-burst-watch-read-2026-08-10.md)). It records the same tape bounds | Intentional |
| **2026-08-15 → 08-17 outage** | **14,405** | 1.4 % | 08-15 16:12 → 08-16 20:22 | Same count in [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) ("The one large hole") | ⚠ Cause still unrecorded |
| **Repair page skip** | **70** | 0.01 % | 08-16 21:40 → 08-17 16:23, 16 runs | The same 16 misses, row for row, as that spec's handle output | Fixed by `GR-1`, deployed 2026-09-21 |
| **Total** | **1,029,591** | | | | |

- **The trader's question, answered: no.** The venue outage is one class of six, and 24.9 % of the total. It is the only box hole after 2026-08-17 16:23.
- **The dev store closes every one of these holes**, the outage's 256,110 trades included. Comparison #1 found 0 dev missing seqs.

## 3. Method

1. Box store: read every row of `trades_2026-07/08/09.csv` in file order, and keep the rows dated in the window. A duplicate is a row whose `trade_seq` appeared earlier. A seq-less row (5 fields, written before identity shipped 08-10 14:07) is keyed on (timestamp, price, amount, direction), as `HistoryCompare.vb` does.
2. Group duplicates into blocks: duplicate rows no more than 2 file lines apart, the 2026-09-15 read's method. A block's append time is the file's high-water timestamp at its first row; the store is append-only. Map append times to instance starts (`capture_marker.log`) and to `PASS_*` lines (`repair_status.log`, which begins 2026-09-21).
3. Holes: walk the dev store in seq order. Take trades whose seq is absent from the box and that no seq-less box row matches. Cut them into runs of consecutive dev trades, then class each run by its span and its millisecond signature.
4. Reconcile with the comparison: the script's counts equal comparison #1's (`box_rows`, distinct, seq-less, `only_in_dev`) exactly. The class totals sum exactly to both headline counts.

## 4. Auto-proceeded decision

- **`BSR-1`: future comparisons start 2026-08-01, not 2026-07-23.** Options: (a) keep 07-23 · (b) 08-01, the first complete box day. Picked **(b)** because it is more truthful. With (a), every comparison reports 594,024 phantom "box holes" from days the box never captured. Applied to [`history-store-backfill-runbook.md`](history-store-backfill-runbook.md) step e3. Ledger row 1 keeps its 07-23 window; `only_in_dev` is never a failure, so the stage-4 pass rule does not change.

## 5. Handles

**`H-1`** — re-runs the whole read. Needs the fetch folder in the main checkout and the dev store; about 2 min.

```bash
python docs/audits/proofs/box-store-dups-holes-2026-10-02/read.py
```

Pasted output (head; full output in [`output.txt`](audits/proofs/box-store-dups-holes-2026-10-02/output.txt)):

```text
BOX rows_in_window=6205563 distinct_seqs=5242633 seqless=281573 dup_rows=681357 first_row=07-31T21:49:57
copies per seq: {1: 4781123, 2: 307816, 3: 87545, 4: 66145, 5: 4}
DUP BLOCKS: 31
duplicate rows appended under a19acc4d (from 09-25 13:29:42): 0
HOLES: trades=1029591 runs=54134
runs < 10,000, before the write-guard fix (08-11 17:18:42): runs=54114 trades=138817 in_single_ms_runs=138817 every_ms_has_a_kept_box_row=138810
runs < 10,000, after the write-guard fix: runs=16 trades=70 in_single_ms_runs=70 every_ms_has_a_kept_box_row=70
```

## 6. ⚠ What I did not verify

| Claim | Status |
|---|---|
| The current build (`bb14dc8`, the collector-halt fixes build, deployed 2026-09-29) writes no duplicates | ✅ **Checked 2026-10-02 on fetch `aws_fetch/20261002-121123`:** 0 duplicate rows appended after the 2026-09-29 20:51 deploy (~2.6 days; same block method, inline script). Comparison #2 (09-25 → 09-30): 0 holes, 0 field mismatches, 5 duplicate rows, all before the deploy |
| Why the 7 rewrite passes' store scans failed | Unknown, as in the 2026-09-15 read's §3. The fix makes a failed scan loud (`SCAN_FAILED`) instead of silent |
| The 20 small duplicate blocks are all `RR-1` repeat fills | Not attributed row by row; 63 rows |
| 7 of the 138,817 same-ms trades have no kept sibling in their millisecond | Not examined; their class is assumed from the era |
| Seq-less matching can over-credit when two trades share every field | Possible in 08-01 → 08-10 14:07. It would make the box look *more* complete, never less |
