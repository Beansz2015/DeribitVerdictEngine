# Trade store — September duplicate rows — read

**Written:** 2026-09-15 (UTC) by the gap-repair build seat. **For:** the orchestrator (Task 2, measurement only; no code change). **Source:** the AWS copy-back `aws_fetch/20260913-153704/` (fetched 2026-09-13; gitignored, read-only): `backtest_data/trades_2026-09.csv`, `ws_health.log`, `capture_marker.log`, plus [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a. **Supersedes** the task chip raised 2026-09-14 ("Investigate duplicate rows in the September trade store").

---

## 0. Verdict

> ⛔ **STILL LIVE — not fixed by `edd4539` or `165f780`.** Every one of the 298,934 duplicate rows is attributed by file position and timing:
>
> | Writer | Rows | Share | After the S2 deploy |
> |---|---|---|---|
> | **The gap-repair pass rewrote its WHOLE 20 h lookback**, 3 times (passes 35, 37, 40) | **298,932** | 99.999 % | ⛔ **Still live.** Same trigger, same outcome; the rewrite can reach ~24 h |
> | Repair tail overlapping unflushed streaming trades (the `A79g` mechanism) | **2** | 0.001 % | Still live, by design of `edd4539`; left to this task |
>
> **Readers dedupe, so none of this is data loss.** The trigger for the whole-lookback rewrite — why the pass's store scan came back empty — is **inferred from code, not observed**: nothing on the box records it (§3).

**IDs used here:**

- **Pass number** — gap-repair passes since `InstanceId` `3fe57c53…` started at 2026-09-01 15:49:09 UTC. One pass runs at start, then one every 6 h: pass *k* runs at 15:49:09 + 6 h × *k*.
- `A79g` — the harness fixture (`edd4539`) that measures a repair tail double-writing trades the streaming writer holds unflushed.
- **B-3** — the file-sharing collision named in [`venue-check-schedule-plan.md`](venue-check-schedule-plan.md): a plain `StreamReader` fails beside an open writer, and while it holds the file the writer's own open fails.
- `DUP-1`, `DUP-2` — this read's decision rows (§7). `DUP` avoids the `DR-1`–`DR-3` IDs of the downtime-repair follow-up briefs.
- **High-water mark** — the newest trade timestamp in the file above a given row. The store is append-only, so it approximates the wall-clock time the row was appended.

---

## 1. Where the duplicates are

**September, one process:** `3fe57c53…` ran from 2026-09-01 15:49:09 to the copy-back, with no restart (`capture_marker.log` has no later entry). `trades_2026-09.csv`: 1,534,183 rows, 1,235,249 distinct `trade_id`s, 298,934 duplicate rows.

**File-order blocks of duplicate rows** (`H-1`; consecutive duplicate rows no more than 2 lines apart form one block):

| Lines | Duplicate rows | Trades dated | Appended at (high-water) | Lag | Pass number |
|---|---|---|---|---|---|
| 142,506–142,507 | 2 | 09-02 09:49:08 | 09-02 09:49:08 | 0.0 min | 3.0000 |
| 952,139–1,048,214 | **96,076** | 09-09 13:49:00 → 09-10 09:48:56 | 09-10 09:48:56 | **1,199.9 min** | **34.9994** |
| 1,115,766–1,209,745 | **93,980** | 09-10 01:49:03 → 09-10 21:48:45 | 09-10 21:48:45 | **1,199.7 min** | **36.9989** |
| 1,315,313–1,424,189 | **108,876** | 09-10 19:49:02 → 09-11 15:48:57 | 09-11 15:48:57 | **1,199.9 min** | **39.9995** |

**What the shape proves:**

- **Each large block is one repair pass.** Its append time sits on a pass boundary (pass number ≈ an integer, the last trade 12–24 s before the pass fired).
- **Each large block covers exactly the 20 h lookback** (`gap_repair_lookback_hours` = 20): the lag from its oldest trade to its append time is 1,199.7–1,199.9 min.
- **Each is contiguous in the file.** The largest holds one non-duplicate row out of 108,877 (line 1,368,345, a trade the store genuinely lacked).
- **The overlaps confirm it:** 29,868 trades have 3 copies — the 8 h where the lookbacks of passes 35 and 37 overlap, plus the 2 h where passes 37 and 40 overlap.
- **No reconnect explains them.** `ws_health.log` has no transition within a day of any large block (September's only feed events: 09-05 09:04 → 09:36 and 09-06 15:43:43 → 15:43:59).
- **3 of the 48 passes up to the copy-back (passes 0–47) did this**, clustered between 09-10 09:49 and 09-11 15:49; none before or after.

## 2. Why a pass rewrites its whole lookback

Verified by reading the code the box ran. `3fe57c53…` started 2026-09-01. Since 2026-08-20 only three commits touched the scan, repair or feed files — `e082844` (2026-09-08), `edd4539` and `165f780` — all after the start, so the running code matches `2d52fb8` for these functions (`H-3`).

1. **`ScanForRepair` swallows every exception.** It logs to `Console` and returns what it had read so far — and then still adds its bracket row (`2d52fb8:Core/TradeStoreWriter.vb:1011`–`1019`).
2. **The store's old rows come first in the file.** The 20 h window is the last ~6 % of the file by position, so an exception at almost any point leaves either no rows or only an old bracket.
3. **`ResolveRepairWindowsMs` then puts the tail at `segStartMs`** — for no rows, and for an old bracket clamped up to `segStartMs` (`:913`–`:921`).
4. **The pager fetches the whole 20 h again, and `AppendRows` writes every row.** Nothing dedups repair commits against the store.

**A missing file would do the same.** The September file existed throughout.

## 3. ⚠ What is NOT known: the exception

- **No record exists.** The `ScanForRepair failed` line goes to `Console`, which nothing in the tree captures.
- **Leading candidate — B-3, a file-sharing `IOException`.** `ScanForRepair` opens the store with a plain `StreamReader` (share mode Read). That open fails if a streaming flush holds the file for writing at that instant. **Not verified**, and one observation fits poorly: the flush write lasts milliseconds, so a random collision at 3 of 46 pass starts is unlikely. The flush timer and the repair timer both start at process start, so their phase may drift into alignment — that would explain the clustering. Not measured.
- **Other candidates, not verified:** memory pressure on the box during the scan; an antivirus or indexer handle on a 90 MB file being appended.

## 4. After the S2 deploy (`edd4539` + `165f780`)

- **`ScanForRepair` is unchanged** — it was on the do-not-touch list for both builds.
- **An empty scan still yields a whole-lookback fetch:** `ResolveRepairWindows` returns an anchored tail at `segStartMs` (HEAD `Core/TradeStoreWriter.vb:1062`).
- **A scan left with only an old bracket is WORSE after the deploy:** it becomes a `trade_seq` tail from that bracket, unclamped by design, so the venue serves from its ~24 h retention edge — roughly 115,000 rows instead of ~95,000.
- ⚠ **`repair_status.log` will NOT flag it.** An anchored tail or a tail that commits the whole lookback reads `TAIL_OK`, and the pass reads `PASS_CLEAN` with a large `committed`. A scan failure is still invisible; only the size gives it away.

## 5. Is a loss path hiding here?

- **The rewrite itself loses nothing:** it re-fetches the whole lookback, so any real gap in it is refilled too.
- **The other half of B-3 can lose tape:** while `ScanForRepair` holds the file open, a streaming flush's open fails, and `AppendRows` logs and DROPS that batch (up to 30 s of trades). Inferred from code; half-reproduced by `A78d`.
- **September shows no sign of it** (`H-2`):
  - **91** "late first copies" — trades written more than 2 min behind the high-water mark, i.e. genuine gaps later repaired — in 15 small clusters.
  - **All 15 were appended at pass times;** the store held **0** `trade_seq` holes at copy-back.
  - **None has the signature of a batch dropped at a pass start** (a ≤ 30 s span ending near :49, repaired 6 h later).
  - One cluster (13 trades, 09-06 15:42:58–15:43:31) matches the feed drop `DEGRADED` 15:43:43, repaired at the 15:49 pass.
- **So no September loss is attributable to this path.** That is not proof it cannot happen.

## 6. The small writer: repair tail beside streaming

**2 rows**, appended at pass 3 (09-02 09:49:08) with 0 lag — the `A79g` mechanism. The build spec-back for `edd4539` estimated up to ~34 rows a pass. **Measured over September: 2 rows in 48 passes.** The estimate was an upper bound, not a rate.

---

## 7. Needs a ruling

| # | Question | Options | My read |
|---|---|---|---|
| **DUP-1** | Fix the whole-lookback rewrite, and does the S2 deploy wait for it? | **(a)** spec a fix now; S2 waits · **(b)** spec a fix now; S2 does not wait · **(c)** leave it | **(b).** It is not data loss, readers dedupe, and the deploy makes nothing worse except the ~24 h size. The fix touches `ScanForRepair`, which the gap-repair builds froze, so it needs its own spec. ⚠ Holding the deploy would record more — S2 would not ship with a known silent rewrite — so this read is the cheaper option; flagged as the reserved pattern |
| **DUP-2** | The fix direction | **(i)** a scan exception fails the pass loudly (a `SCAN_FAILED` line in `repair_status.log`, no fetch) instead of reading as an empty store · **(ii)** open the scan with share mode ReadWrite, as `CoverageReport.ReadStoreWindow` already does · **(iii)** both | **(iii).** (ii) removes the leading candidate trigger; (i) makes any remaining failure truthful, and without it the new log still reads `PASS_CLEAN` |

A queue row carries this: [`trader-tick-queue.md`](trader-tick-queue.md) §2, "Repair pass rewrites its whole lookback when its store scan fails".

## 8. Handles — run 2026-09-15 (UTC), output pasted

**H-1 and H-2 — duplicates, blocks, late first copies.** Needs the copy-back folder in the main checkout.

```bash
T=$(mktemp -d)
F=/c/Dev/DeribitVerdictEngine/aws_fetch/20260913-153704/backtest_data/trades_2026-09.csv
awk -F, 'NR>1 { id=$6; ts=$1+0
  if (id in first) { print NR","ts","hw } else { first[id]=NR }
  if (ts>hw) hw=ts }' "$F" > "$T/dups.csv"
echo "duplicate rows: $(wc -l < "$T/dups.csv")"
awk -F, 'function flush(){ if(n>0) printf "lines %d..%d dup_rows=%d trades=%s..%s appended_at=%s lag_min=%.1f pass_no=%.4f\n", bs, be, n, strftime("%m-%dT%H:%M:%S",int(tmin/1000),1), strftime("%m-%dT%H:%M:%S",int(tmax/1000),1), strftime("%m-%dT%H:%M:%S",int(hw0/1000),1), (hw0-tmin)/60000, (hw0-1788277749000)/21600000 }
{ ln=$1+0; ts=$2+0; hw=$3+0
  if (n>0 && ln-be<=2) { be=ln; n++; if(ts<tmin)tmin=ts; if(ts>tmax)tmax=ts }
  else { flush(); bs=ln; be=ln; n=1; tmin=ts; tmax=ts; hw0=hw } }
END{flush()}' "$T/dups.csv"
awk -F, 'NR>1 { id=$6; ts=$1+0
  if (!(id in seen)) { seen[id]=1; if (hw>0 && hw-ts>120000) n++ }
  if (ts>hw) hw=ts } END { print "late first copies (>120 s behind the file high-water mark): " n+0 }' "$F"
rm -rf "$T"
```

```text
duplicate rows: 298934
lines 142506..142507 dup_rows=2 trades=09-02T09:49:08..09-02T09:49:08 appended_at=09-02T09:49:08 lag_min=0.0 pass_no=3.0000
lines 952139..1048214 dup_rows=96076 trades=09-09T13:49:00..09-10T09:48:56 appended_at=09-10T09:48:56 lag_min=1199.9 pass_no=34.9994
lines 1115766..1209745 dup_rows=93980 trades=09-10T01:49:03..09-10T21:48:45 appended_at=09-10T21:48:45 lag_min=1199.7 pass_no=36.9989
lines 1315313..1424189 dup_rows=108876 trades=09-10T19:49:02..09-11T15:48:57 appended_at=09-11T15:48:57 lag_min=1199.9 pass_no=39.9995
late first copies (>120 s behind the file high-water mark): 91
```

Identity: 2 + 96,076 + 93,980 + 108,876 = **298,934**. The 3-copy count (29,868) and the 15 late-copy clusters came from one-off variants of this script, not kept (`E`-class).

**H-3 — the code the box ran.**

```bash
git show 2d52fb8:Core/TradeStoreWriter.vb | sed -n '1011,1019p;913,921p'
git log --since=2026-08-20 --format='%h %cI %s' -- Core/TradeStoreWriter.vb TradeStoreGapRepair.vb DeribitWsFeed.vb tools/BacktestRunner/HistoricalStore.vb
```

Output: the tail rule (`If rows.Count = 0 Then tailStart = segStartMs … clamped to segStartMs`), the swallowed exception (`ScanForRepair failed`), and `If haveBracket AndAlso Not truncated Then inWindow.Add(bracket)` after the `Try`. The log lists `165f780`, `edd4539` and `e082844` (2026-09-08) — all after `3fe57c53…` started.

## 9. Not verified

- **Which exception fired**, or that one did — no durable log exists on the box.
- **The flush-timer phase alignment** suggested in §3.
- **That `3fe57c53…` ran exactly `2d52fb8`'s scan code** — inferred from the commit dates against the process start; the binary was not inspected.
- **Whether any streaming batch was ever dropped by B-3 on the box.** Only the absence of its signature in September is shown.
- **The ~24 h / ~115,000-row size after the deploy** — arithmetic from the measured retention edge (2026-09-14) and September's trade rate.
