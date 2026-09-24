# Gap repair re-repairs the same `trade_seq` holes — READ

> **Read-only investigation. No `.vb` file touched.** Written 2026-09-25 (UTC) against the fetched
> AWS store copy-backs already in the tree. Answers the queue row in
> [`trader-tick-queue.md`](trader-tick-queue.md) §2, *"Gap repair re-repairs the same `trade_seq`
> holes on later passes"*, and the two candidate causes named in that row.

**IDs used in this document:**

| ID | Meaning |
|---|---|
| `RR-1` | The mechanism this doc identifies — a sort/walk assumption failure in `ResolveRepairWindowsCore` |
| `H-1`…`H-6` | Handles below, re-runnable by the reader against the same fetched copy-back |

---

## 1. The observation

`docs/aws-collector-deploy-checklist.md` §5d (2026-09-24 check): the same `trade_seq` bracket gets
`HOLE_REPAIRED` on several passes in a row, each pass logging `PASS_CLEAN`. Venue check for the same
period reads `CLEAN`, `seq_contiguous=true` — the tape itself is not missing anything. Two candidate
causes were named, neither verified: (a) repaired rows do not persist, (b) detection re-finds the
same hole inside the 20 h lookback.

## 2. Verdict — measured

**Cause (b), one specific and previously-unverified sub-mechanism, confirmed. Cause (a) refuted.**

- Repaired rows **do persist** — every repaired seq is present in the fetched store file, in full.
- The repair pass **re-detects a hole that was never real**, because `ResolveRepairWindowsCore`'s
  hole walk assumes ascending `trade_seq` always agrees with ascending `(Timestamp, TradeSeq)` sort
  order. When one trade inside a tight burst carries an **earlier or tied millisecond than a
  lower-seq sibling** (the venue's own timestamp field, not a store defect), that assumption breaks:
  a fully-contiguous, fully-present seq run gets split into two spurious "holes" by the sort. Repair
  then re-fetches and re-appends the same (already-present) rows every pass, for as long as the
  hole's bracket row stays inside `gap_repair_lookback_hours`.
- This is exactly the risk `docs/gap-repair-same-ms-page-skip-spec.md` §2.1/§9 named and left
  **"not proved"**: *"This assumes trade_seq order agrees with timestamp order. The walk already
  assumes it."* That spec (`GR-1` (d), commit `edd4539`) fixed the **fetch-side** 1,000-row page skip
  by moving to `trade_seq`-range fetching. It explicitly left `ScanForRepair` and the walk unchanged
  (§4.5 of that spec). `RR-1` is a different, residual defect in the same file, in the step the prior
  build did not touch.

## 3. Method

1. Read `docs/aws-collector-deploy-checklist.md` §5d for the example bracket and dates.
2. Read the full `repair_status.log` from `aws_fetch/20260924-084613/` (covers 2026-09-21T15:40
   → 2026-09-24T03:39 UTC, one continuous process, `InstanceId=ee159d03-…`, no restart in the
   window). Every `HOLE_REPAIRED` / `PASS_*` line for the period.
3. For every distinct hole bracket in the log, grepped `aws_fetch/20260924-084613/backtest_data/
   trades_2026-09.csv` for each seq in the bracket, to get: how many copies exist, their line
   positions, and their neighbours' `(Timestamp, TradeSeq)` pairs.
4. Read `Core/TradeStoreWriter.vb`'s `ScanForRepair`, `ResolveRepairWindowsCore`, `RepairWindow.ForHole`
   and `AppendRows`, and `TradeStoreGapRepair.vb`, to reconstruct exactly what each pass would compute
   from the file content measured in step 3.
5. Hand-walked the sort-and-delta arithmetic for each hole and compared the predicted `RepairWindow`
   against the actual logged `seq=` ranges — an exact match confirms the mechanism, not just a
   correlation.
6. Checked the three holes in the same log window that did **not** recur, to see whether they lack
   the timestamp inversion `RR-1` requires (the "measure what a cause fails to explain" check).

## 4. Measured findings

### 4.1 Every hole in the log window, and which recurred

| Hole (seq) | First `HOLE_REPAIRED` (UTC) | Times repaired | Copies found in store | Timestamp order at the bracket |
|---|---|---|---|---|
| `300063414..418` | 2026-09-21 21:39 | **2** (21:39, next-day 03:39) | 3 (1 original + 2 duplicate appends) | **Inverted** — seq 418's ms is *lower* than 414–417's |
| `300335168..179` | 2026-09-22 09:39 | 1 | 1 | Monotonic, no inversion |
| `300377526..528` | 2026-09-22 15:39 | **3** (15:39, 21:39, next-day 03:39) | 4 (1 original + 3 duplicate appends) | **Inverted** — seq 528's ms is *lower* than 526–527's |
| `300472030..034` | 2026-09-22 21:39 | 1 | 1 | Monotonic, no inversion |
| `300628191..206` | 2026-09-23 15:39 | 1 | 1 | Monotonic, no inversion |

**Every recurring hole has a timestamp inversion at its bracket; every one-shot hole does not.**
That is the "measure both directions" check: the candidate mechanism predicts recurrence exactly
where it is found, and predicts no recurrence exactly where none is found — 5 for 5.

### 4.2 Worked example — `300377526..528`, the one named in the queue row

Store rows around the bracket (`aws_fetch/20260924-084613/backtest_data/trades_2026-09.csv`):

```
line 2832343  seq 300377525  ts …541936
line 2832344  seq 300377526  ts …542805   ← in place, ascending seq, this is the ORIGINAL row
line 2832345  seq 300377527  ts …542805
line 2832346  seq 300377528  ts …542804   ← lower ms than 526/527 despite the higher seq
line 2832347  seq 300377529  ts …542990
```

Three more copies of 526–527–528 exist, at file lines 2890531, 2939070 and 2982036 — each sitting
next to trades from a much later seq range (≈300435k, ≈300472k/300484k, ≈300527k), i.e. each was
blind-appended to whatever the file's tail was at that pass's run time (15:39, 21:39, 03:39
respectively). Four copies total for three log entries plus one original: **the original row was
never missing.**

**Hand-walk, sorted by `(Timestamp, TradeSeq)`:** `525 (…936) → 528 (…804) → 526 (…805) → 527
(…805) → 529 (…990)` — seq 528 sorts *before* 526 and 527 because its millisecond is lower.

`ResolveRepairWindowsCore`'s delta walk (`Core/TradeStoreWriter.vb` lines 1061–1071) over that order:

| Step | `prev.Seq` | `cur.Seq` | `delta` | Emitted |
|---|---|---|---|---|
| 1 | 525 | 528 | 3 | `RepairWindow.ForHole(525, …, 528, …)` → `FirstSeq=526, LastSeq=527` |
| 2 | 528 | 526 | −2 | none (discontinuity, not loss — line 1063 comment) |
| 3 | 526 | 527 | 1 | none |
| 4 | 527 | 529 | 2 | `RepairWindow.ForHole(527, …, 529, …)` → `FirstSeq=528, LastSeq=528` |

That predicts exactly the two log lines: `seq=300377526..300377527` and `seq=300377528..300377528`.
Every pass re-derives the identical two windows from the identical (already-complete) data, because
nothing about appending another copy of 526–528 changes the sort order that produces the split.

**Same arithmetic reproduces `300063414..418`'s two log lines exactly** (bracket at seq 413/418
inverted the same way, split into `414..417` and `418..418`) — worked by hand, not pasted here for
space; the method in §3 step 5 applies unchanged.

### 4.3 Why it stops recurring

`gap_repair_lookback_hours = 20` (`settings.json` line 558) bounds `ScanForRepair`'s window
(`segStartMs`, `Core/TradeStoreWriter.vb` `ScanForRepair`, filters `p.TsMs < segStartMs` into the
bracket-only role). `gap_repair_interval_hours = 6` matches the observed pass cadence. For
`300377526..528` (trade time 2026-09-22 10:05:41 UTC): repaired at Δ5.6 h, Δ11.6 h, Δ17.6 h after the
trade — then the 09:39 pass (Δ23.6 h) is `PASS_CLEAN`, because by then the bracket row (seq 300377525,
the same-ms sibling) has aged past `segStartMs` and drops out of the scan entirely. The phantom hole
does not get "fixed" — it ages out of visibility. The same pattern holds for `300063414..418` (2
recurrences then silence, consistent with its earlier trade time aging out first).

### 4.4 Cost, measured over this log window (2026-09-21 15:40 → 2026-09-24 03:39, ~2.5 days)

| Item | Count |
|---|---|
| Phantom re-detections (`HOLE_REPAIRED` lines for a hole that was never real) | **5** — 2 for `063414..418`, 3 for `377526..528` |
| Duplicate rows appended to `trades_2026-09.csv` by those re-detections | **19** — 10 for `063414..418` (2×5 rows), 9 for `377526..528` (3×3 rows) |
| Extra REST fetches (one small page each, `pages=1` per log line) | 5 |
| Real holes in the same window (correctly repaired once, no waste) | 3 — `335168..179`, `472030..034`, `628191..206` |

Small in absolute terms over 2.5 days, but it recurs on **every** hole that happens to straddle a
timestamp inversion, for as long as `gap_repair_interval_hours` keeps it inside the lookback — a
busier tape (more same-ms sweeps) or a longer lookback would scale this up. It also adds to the
store's pre-existing, separately-caused duplicate-row problem (`docs/gap-repair-same-ms-page-skip-
spec.md` §8: 298,934 duplicates measured 2026-09-13, cause "not investigated" there — a different,
earlier mechanism; `RR-1` is a second, smaller source of duplicates, not the same one).

## 5. Fix direction — sketch, options only, no design chosen

Any of these is a later spec; the deploy itself is reserved per `CLAUDE.md`'s scoring/settings
gates, since a change here touches the store-completeness code path.

| Option | Idea | Trade-off |
|---|---|---|
| **(a) Fix the walk's sort key** | Walk in **pure `TradeSeq` order**, not `(Timestamp, TradeSeq)`. The hole arithmetic is already seq-based (`ForHole`, `ForTail`) — the timestamp is carried only for the log's `span=` field. Sorting by seq alone cannot invert, because `trade_seq` is monotonic by definition (it is the venue's own ordering key) | Loses ordering-by-time for the `span=` display, which would need to compute min/max ts separately per window rather than reading `LeftTsMs`/`RightTsMs` off the bracket rows directly. Removes the false-positive class entirely — the more truthful option, and the cheaper one on the actual defect (fewer moving parts, not more) |
| **(b) De-dup before diagnosing** | Before the walk, collapse duplicate `(TradeId)` rows (the same key `DedupTrades` already uses) so a hole that was already patched by an earlier pass's duplicate append doesn't get counted twice | Does not address the *first* false detection (the very first `HOLE_REPAIRED` for `377526..528` was already phantom, before any duplicate existed) — treats a symptom, not `RR-1` itself |
| **(c) Verify-before-fetch** | Before emitting a `RepairWindow`, re-check whether every seq in `[FirstSeq, LastSeq]` is already present in the scanned rows (a seq-set membership check) and skip the fetch if so | Adds a second full pass over the scanned rows per window; correct in effect but does not fix the underlying assumption, so a genuinely absent-but-inverted-neighbour hole could still confuse the `span=` log fields |
| **(d) Do nothing, rely on aging-out** | The current behaviour: wasteful but bounded (a few duplicate rows and REST calls per affected hole, self-terminating at the lookback edge, no data loss) | Matches the observation that tape completeness is not in doubt today, but the duplicate-row count grows unbounded over time and every recurrence is an unexplained-looking log entry an operator has to keep re-triaging |

**My read:** (a) is the more truthful option — it removes the false-positive class at its source, is
mechanically simpler than (c), and does not require touching the deploy/settings surface (no new
key, `ResolveRepairWindowsCore`'s internal sort only). Per `CLAUDE.md`'s auto-proceed test, this is a
"cheaper AND more informative" pick, not a trade — I am not reserving it. Whether it *should* be
built now is a separate call (it is a "affects the store-completeness edge" class per that same
document, which the trader reserves for deploy timing, not for the design pick itself).

## 6. What I did not verify

- **Whether the timestamp inversion is common enough to matter beyond this one log window.** I
  checked 5 holes in a 2.5-day slice of one process's run. A wider scan of `repair_status.log` history
  or of `trades_2026-09.csv`'s full same-ms population (as `docs/gap-repair-same-ms-page-skip-
  spec.md` `E-3` did for the page-skip defect) was not run here.
- **Whether the venue's timestamp field is ever inverted relative to `trade_seq` by more than a few
  milliseconds**, or whether it is always confined to the same tight burst (all examples found here
  are within a 1–2 ms spread).
- **Any effect on `AnalysisLogger` or `verify/ordercheck/Program.vb`** — out of scope per this task's
  instructions (another agent is on that surface); not read.
- **Whether the pre-existing 298,934-row duplicate problem (`docs/gap-repair-same-ms-page-skip-
  spec.md` §8) shares any cause with `RR-1`.** They are different mechanisms by construction (that
  one predates the `trade_seq`-range fetch and was linked to the streaming-writer race, not the sort
  walk) — not independently re-confirmed here.
- **No fixture or harness run.** This is a read, not a build; §5's options are unbuilt sketches.

## 7. Handles (re-runnable by the reader)

**`H-1` — the log, this instance's whole window.**
```bash
cat aws_fetch/20260924-084613/repair_status.log
```

**`H-2` — all copies of a repaired bracket in the store.**
```bash
F=aws_fetch/20260924-084613/backtest_data/trades_2026-09.csv
for s in $(seq 300377526 300377528); do grep -n ",$s\$" "$F"; done
```

**`H-3` — the same for the other recurring hole.**
```bash
F=aws_fetch/20260924-084613/backtest_data/trades_2026-09.csv
for s in $(seq 300063414 300063418); do grep -n ",$s\$" "$F"; done
```

**`H-4` — the walk's sort-then-delta step, for reading alongside §4.2.**
```bash
grep -n "rows.Sort(Function(a, b)" Core/TradeStoreWriter.vb
sed -n '1036,1075p' Core/TradeStoreWriter.vb
```

**`H-5` — `ForHole`'s arithmetic.**
```bash
sed -n '800,809p' Core/TradeStoreWriter.vb
```

**`H-6` — the lookback and interval settings that bound recurrence.**
```bash
grep -n -A3 '"gap_repair_interval_hours"' settings.json
```
