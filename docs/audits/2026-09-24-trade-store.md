# Trade-store audit — 2026-09-24

**Scope.** `Core/TradeStoreWriter.vb` · `TradeStoreGapRepair.vb` · `Core/StoreFiles.vb` ·
`Core/CaptureMarkerLog.vb` · `Core/RepairStatusLog.vb` · `Core/VenueStatusLog.vb` ·
`Core/WsHealthLog.vb` · `Core/WsFeedLog.vb` · `tools/BacktestRunner/HistoricalStore.vb`.
Read against the call sites in `DeribitWsFeed.vb` (`ApplyTrades`, `SeedAsync`, the flush timer,
`GetTradeStoreStatus`) and `UI/MainForm_TapeStoreStatus.vb`.

**Commit audited:** `6e74181`.

**Files not covered:** none. All nine files in scope were read. Two notes on how:

- `VenueStatusLog.vb`, `WsHealthLog.vb` and `WsFeedLog.vb` were read with comment lines
  filtered out. Their executable code was read in full, their comments were not.
- `CaptureMarkerLog.vb`, and the candle half of `StoreFiles.vb`, produced no findings.

**Excluded as known and specced (per the audit brief):** the same-ms page skip, the
cross-month leading gap and the scan-failure rewrite. All three are built and pending deploy.
F1 overlaps a residual that `docs/gap-repair-scan-failure-spec.md:99` records as
out of scope. It's reported here because that doc doesn't characterise the consequences
shown below.

**Proof code:** [`proofs/trade-store/`](proofs/trade-store/README.md), which holds the run
command and its output.

**Edits from the chat report, and nothing else changed.** The report below is the report as
delivered, with six edits:

- F7's "note at `:743`" now reads `TradeStoreWriter.vb:742-743`. The bare line number sat under
  a `HistoricalStore.vb` location and pointed at the wrong file.
- F5's "this container" now reads "the audit container".

- The evidence paragraph points at the committed proof directory instead of the scratchpad.
- The claim that the probe can't be re-run is replaced, because the probe is now committed.
- The closing sentence "I haven't changed any repo files, and nothing is committed" is
  dropped. It's no longer true.
- F2's `PASS_CLEAN` comes from a restatement of the `RepairStatusLog` state rule, not from
  `RepairStatusLog` itself. The window state it derives from is production output. See the
  README.

---

## LOGIC TRACE

**Setup.** Mid-month on a live box. The October file is about 100 MB. Streaming holds about 200 trades in `_pending`. A repair pass fires at the same time.

**1. Flush with a 4 s WS stall.** The socket goes quiet for 4 s, then the backlog arrives as a burst of notifications. `ApplyTrades` buffers each trade under `_pending`, and every trade passes the identity guard. The burst crosses 500 and the count trigger calls `store.Flush()` on the receive thread. `Flush` snapshots the buffer, releases `_pending`, then waits on `_appendLock`, which repair's `AppendRows` holds for one 1,000-row page (milliseconds). Meanwhile the 30 s timer's `Flush` takes the next snapshot and can reach `_appendLock` first. Batches then land B before A. That's tolerated, because every reader sorts. Heartbeat is 30 s, so a 4 s stall costs nothing unless Deribit drops the socket. **No loss on this path unless the append fails, and when it fails, the failure is misreported (F2, F3).**

**2. Reconnect whose REST seed fails.**
- `SeedAsync` calls `ResetBufferState` first. It flushes under `_pending` plus `_appendLock` and clears the window. The F2 fix holds.
- The candle and trade seeds then retry through `ExecuteWithRetry` (15 s timeout plus 1 retry, per call) and return `Nothing`, so nothing throws.
- But `SubscribeAsync` only runs after the whole seed finishes. The WS is connected and unsubscribed for the entire retry budget, and **streaming captures nothing for that window (F9)**.
- After the subscribe, the first `Buffer` calls `EnsureSeeded`. That runs `ReadTradeFileTail` over the whole month file on the receive thread while holding `_pending`. The UI `TapeStore` tick calls `LastFlushUtc` and `TotalRowsWritten`, which take `_pending`, so the UI thread blocks for the whole scan (F5).
- `ReadTradeFileTail` opens with `FileShare.Read`. If repair's `StreamWriter` has the file open at that moment, one of the two opens fails on Windows (F4).

**3. Month boundary at 00:00 UTC.**
- A streaming batch that straddles midnight is split correctly by `AppendRows`. The guard window rolls across unchanged.
- Repair at 00:00:05: September's tail stops at `StopAfterMs` = 23:59:59.999. October's file has streamed rows ≥ 00:00 and no bracket, so F-1 seeds from September's newest row. That row is read after September's tail ran, so there's no double window.
- October's tail re-fetches whatever sits unflushed in `_pending` (known, A79g).
- If a reconnect lands at 00:00:05, `EnsureSeeded` reads the new October file (near-empty), so any WS replay of September prints gets re-admitted. These are duplicates, in the documented safe direction.
- **One thing is not safe. If the September file holds a torn row, either September's tail window disappears for the rest of that month or the scan manufactures a hole ~3×10¹⁵ wide (F1).**

**4. Concurrent repair pass.**
- Lock order is always `_tradeStoreLock` → `_pending` → `_appendLock`, and nothing takes them in reverse. No deadlock.
- `ScanForRepair` opens with `ReadWrite` sharing and a last-LF bound, which is fine for the file's *final* line.
- The repair outcome is computed from what the venue *served*, never from what `AppendRows` *committed*. A page whose append fails still reports `HOLE_REPAIRED` / `PASS_CLEAN` (F2).

---

**Evidence.** An isolated probe compiles the real `Core/TradeStoreWriter.vb`, `Core/StoreFiles.vb` and `tools/BacktestRunner/HistoricalStore.vb` against stubs, on .NET 8. It's committed at [`proofs/trade-store/`](proofs/trade-store/README.md) with its run command and output, so you can re-run it. Linux doesn't enforce `FileShare` between readers and writers, so F4 is reasoned from code and from the B-3 note in `TradeStoreWriter.vb`, not reproduced.

---

### F1 — Torn row in the middle of a file becomes a phantom hole, a silent permanent loss, or a disabled tail
- **SEVERITY:** HIGH
- **LOCATION:** `Core/TradeStoreWriter.vb:487-512` (`TryParseRow` accepts any `parts.Length >= 5`); `:1339-1389` (`AppendRows` appends to a file whose last byte may not be a line feed); `:1056`, `:1111`
- **DOWNSTREAM IMPACT:** Any calibration that reads the store (CVD, TFI, MicroCVD, liquidation) gets silently missing or duplicated trades. The repair log goes permanently noisy or goes blind.
- **FAILURE SCENARIO:** A kill, power loss or full disk lands between two 4 KB `StreamWriter` chunk writes. The file now ends mid-row, and the next append glues a new row onto it. Probe results, 10 trades sent each time:
  - **Seq torn** (`…,500000005,300`): the row parses with `seq=3001790816400006`. The walk emits `Hole first=300000005 last=3001790816400005 missing=3.0e15`. That hole wins the `MaxHolesPerPass` ranking and evicts the smallest real hole. Its fetch asks from seq 300000005 onward, so every pass re-appends the whole served range from there to now as duplicates, and reports `PASS_LOSS` with a not-served count of ~3×10¹⁵. That repeats 4× a day until the row ages out of the lookback.
  - **Id torn, or only the line feed missing:** the merged row parses with `seq=-1`, which breaks the walk. **Trade 300000006 is gone and no window is emitted.** The comment at `:1052` says breaking "can only ever MISS a hole in legacy ground". That's false: this is post-2026-08-10 ground.
  - **Timestamp torn** (`17908`): the row parses as `ts=179081790816400006`, far in the future. It sorts to the end of the scan, so `newest.TsMs <= segEndInclMs` fails and **no Tail window is emitted for that month again**. Under `transport=rest` or a dead WS, repair is the only capture path, so that means total capture loss until month end.
- **ANALYTICAL CRITIQUE:** `docs/gap-repair-scan-failure-spec.md:99` records this as "out of scope" and treats it as a one-row problem. It isn't one row. The parser amplifies it, because one lenient `< 5` check turns byte corruption into a well-typed record with a plausible-looking seq or timestamp. Three changes would close it: require exactly 5 or 7 columns; validate the timestamp range and that `Direction` is `buy` or `sell`; and have `AppendRows` write a line feed first whenever the file's last byte isn't one.

### F2 — Repair outcome ignores what was actually committed
- **SEVERITY:** HIGH
- **LOCATION:** `tools/BacktestRunner/HistoricalStore.vb:473`, `:487-504`
- **DOWNSTREAM IMPACT:** `repair_status.log` is the only record that repair works (GR-4). It can report success over data that never reached disk.
- **FAILURE SCENARIO:** In the probe, a hole of 4 seqs is fully served and the commit returns 0. Result: `state=HOLE_REPAIRED committed=0 notServed=0 isFailure=False` → `PASS_CLEAN`. The live triggers are a share violation (F4), a full disk (F3) or a locked file. If that's the last pass before the hole ages past Deribit's ~24 h retention, **the loss is permanent and logged clean.**
- **ANALYTICAL CRITIQUE:** Served is not stored. Compare `commit(kept)` against `kept.Count`, and add a `COMMIT_FAILED` state inside `IsFailure`. This is the exact "silent hole" class the repo already rejects.

### F3 — `AppendRows` counts rows written that never reached disk
- **SEVERITY:** MEDIUM-HIGH
- **LOCATION:** `Core/TradeStoreWriter.vb:1376-1379` (`written += 1` after the buffered `WriteLine`); consumed at `:286-291`
- **DOWNSTREAM IMPACT:** The TAPE STORE status shows NORMAL on a full disk. `TotalRowsWritten` inflates. Repair's `Committed` is overstated.
- **FAILURE SCENARIO:** In the probe, the trade file is symlinked to `/dev/full` and one 500-row batch is appended. It returns **86** while the disk accepted 0. Because 86 > 0, `Flush` stamps `_lastFlushUtc`, and `ClassifyTapeStoreTier` stays NORMAL on every 30 s flush indefinitely. That defeats the dead-path escalation added at `:366-373` specifically for "full disk".
- **ANALYTICAL CRITIQUE:** Count only after `Dispose` succeeds (all-or-nothing per month group), or return 0 for the group on any exception. A health signal that reads green during the failure it exists to catch is worse than no signal.

### F4 — The B-3 share-mode fix was applied to one reader out of four
- **SEVERITY:** MEDIUM (Windows production; not reproducible on Linux)
- **LOCATION:** `ReadTradeFileTail` `:617`, `ReadTradeFile` `:586`, `LastTradeTimestamp` `:647`, all plain `StreamReader(path)` = `FileShare.Read`; compare `OpenStoreForScan` `:1240`
- **DOWNSTREAM IMPACT:** Each collision drops either a streamed batch or a repair page, and F2 reports the repair case as clean.
- **FAILURE SCENARIO:** A reconnect puts `EnsureSeeded` into a ~1.5 s scan with `FileShare.Read`. Repair's `StreamWriter` open fails with a sharing violation, the page is logged and dropped, and the window reports `HOLE_REPAIRED`. The reverse order fails too: the reader open fails, the guard window stays empty, and replays get admitted. Running `CoverageReport` or `LoadTradeRange` against the live store directory on the box has the same effect on streaming flushes.
- **ANALYTICAL CRITIQUE:** The fix note at `:1236-1239` names both halves of the mechanism, then fixes one call site. Route every store read through `OpenStoreForScan`.

### F5 — Full-month scan on the WS receive thread, under the lock the UI thread polls
- **SEVERITY:** MEDIUM
- **LOCATION:** `Core/TradeStoreWriter.vb:211-213` → `:345-352`; UI read at `DeribitWsFeed.vb:181-183` from a WinForms timer (`UI/MainForm_TapeStoreStatus.vb:47`)
- **DOWNSTREAM IMPACT:** On every reconnect, the receive thread stalls and the UI thread freezes. The cost grows linearly through the month.
- **FAILURE SCENARIO:** In the probe, a 1.8M-row, 103 MB file took **1,517 ms and 1,610 ms** for `ReadTradeFileTail` on the audit container. A burstable EC2 box will be slower. The 2026-09-21 incident note in `DeribitWsFeed.vb` says a stalled UI thread is what parked the pool threads, and this puts a new UI stall on the reconnect path.
- **ANALYTICAL CRITIQUE:** The comment at `:606-610` says seeding costs "no more file I/O than the guard it replaced", which is true and doesn't matter. The cost is *where* the scan runs, not how many bytes it reads. Options: seek-from-end, move the scan off the receive thread, or make the status properties lock-free (`Interlocked`/`Volatile`).

### F6 — Funding coverage check counts each stored sample twice
- **SEVERITY:** MEDIUM
- **LOCATION:** `tools/BacktestRunner/HistoricalStore.vb:685-688`
- **DOWNSTREAM IMPACT:** A funding month counts as covered at 50 % fill and is never refetched, which is the 2026-07-31 "frozen partial month" defect back in a new form. Backtests and calibration of the Step 3/3b funding modifier run over missing hours.
- **FAILURE SCENARIO:** In the probe, 24 samples were expected. With 12 stored, it returned in 2 ms with no fetch. With 11 stored, it attempted a fetch. The threshold is exactly half.
- **ANALYTICAL CRITIQUE:** `CountFundingInRange` was extracted and the old inline loop was left in place. `CoverageReport.vb:1127` uses the helper alone, so the two surfaces now disagree about the same file.

### F7 — `LoadTradeRange` sorts by timestamp only, with an unstable sort
- **SEVERITY:** MEDIUM
- **LOCATION:** `tools/BacktestRunner/HistoricalStore.vb:625`
- **DOWNSTREAM IMPACT:** Order-sensitive replays (last-N TFI window, MicroCVD early/late split, cascade windows) depend on file layout, and 62 % of trades share a millisecond (per the code's own note at `TradeStoreWriter.vb:742-743`). Two stores holding identical trades can replay differently.
- **FAILURE SCENARIO:** A repair block appends same-ms siblings after the streamed ones. `List.Sort` (introsort) permutes them, so the boundary trade of a 500-trade window changes.
- **ANALYTICAL CRITIQUE:** `ResolveRepairWindowsCore:1026-1029` already states why a timestamp-only sort is wrong, one file over. The reader needs the same (ts, seq) comparator.

### F8 — `ReadTradeFile` silently returns a truncated month on a mid-read failure
- **SEVERITY:** LOW-MEDIUM
- **LOCATION:** `Core/TradeStoreWriter.vb:585-599`, used by `LoadTradeRange`
- **FAILURE SCENARIO:** An IOException partway through a month file (a lock, or F4) returns the rows read so far. Calibration then runs on a partial month with no signal. The scan path fixed exactly this as DUP-1 and SF-4, and the reader path didn't get the fix.

### F9 — A slow or failing REST seed blocks the WS subscribe, and with it all streaming capture
- **SEVERITY:** LOW-MEDIUM
- **LOCATION:** `DeribitWsFeed.vb:214-218`, `:321-330`
- **FAILURE SCENARIO:** REST times out on every reconnect. Each seed call costs up to 2×15 s plus the retry delay, all before `SubscribeAsync`. Capture is dark for that stretch, and only the next repair pass (up to 6 h later) recovers it, if REST is back by then. Store capture doesn't need the seed. Subscribing first and seeding second (or in parallel) decouples them.

### F10 — Health logs record a state transition before the write succeeds
- **SEVERITY:** LOW
- **LOCATION:** `Core/WsHealthLog.vb` and `Core/VenueStatusLog.vb`, `LogTransition` (`_lastState = state` then `TryAppend`, whose return value is ignored); `RecordOkIfRecovery` has the same pattern
- **FAILURE SCENARIO:** One failed append of `WS_DOWN` means the transition is never retried, and the log reads the previous state indefinitely. The coverage report's scope inputs then lie.

### F11 — `WsFeedLog` loses suppressed repeats
- **SEVERITY:** LOW
- **LOCATION:** `Core/WsFeedLog.vb`, `WriteAt`
- **FAILURE SCENARIO:** 50 identical errors in 5 minutes, then silence, leaves one line in the log. The "repeated Nx" suffix only prints when the same message recurs after the window, and clearing the map at 200 entries discards every pending count.

### F12 — Enabling gap repair by hot-reload never starts it
- **SEVERITY:** LOW
- **LOCATION:** `TradeStoreGapRepair.vb:63-76`
- **FAILURE SCENARIO:** The app boots with `gap_repair_enabled:false`, so `Start()` returns before creating `_timer`. Flipping the setting to true later does nothing, and nothing gets written to `repair_status.log`. The docstring claims the change "takes effect at the next interval".

---

**Fix order:** F1, F2 and F3 first. Together they mean a disk-level fault can corrupt the store and the repair log still reads clean, which is the one combination this store can't afford. F6 is a one-line deletion.
