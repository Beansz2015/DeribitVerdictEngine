# Audit 2026-09-24: MainForm_Layout, MainForm_TapeStoreStatus, MainForm_Calibration

Scope: `UI/MainForm_Layout.vb`, `UI/MainForm_TapeStoreStatus.vb`, `UI/MainForm_Calibration.vb`, with `MainForm.Designer.vb` as a read-only reference.

**Files not fully covered:** `MainForm.Designer.vb` was used only as a reference. I grepped it for the `nudMinutes`/`nudSeconds` Minimum/Maximum lines and did not read it end to end. The other three files were read in full.

**Method:** Everything here comes from reading the code at `6e74181`. I executed nothing. The container has no .NET SDK, and the egress proxy refused the SDK download. The proof code in `docs/audits/proofs/mainform-layout-tapestore-calibration/` has not been run. I didn't read the CLAUDE.md session-start docs; I went straight to the code.

The worst problem is a path that stops the collector with no dialog and no log line. The rest are gates that let a trade through when they should block, or give way when data is missing, and they appear exactly in an outage-then-flush sequence.

--- LOGIC TRACE ---

**The constructor, before the window exists** (`MainForm_Layout.vb:318–515`), using the tracked config: `transport=ws`, `trigger_mode=on_close`, `start_engaged=true`, 1-minute interval.

1. `SettingsLoader.Initialise` starts two FileSystemWatchers. They fire on pool threads and don't marshal to the UI.
2. `InitMarketDataSources` starts the WS feed loop and `TradeStoreGapRepair` (one pass now, then every 6h). Both run in the background with no UI marshalling. The repair pass hits REST 5xx immediately.
3. With `start_engaged` on, `StartAutoRun` runs and starts two 1 Hz `Threading.Timer`s: the countdown and the on-close watcher. Both are gated and use `BeginInvoke`. `OnCloseModeActive()` is true because `_wsFeed` has been constructed, even though it isn't connected. The backstop is `max(60 s, (execRes+1) min)`: 4 minutes at 3-minute resolution, 2 minutes in NY.
4. Three `Forms.Timer`s start on the UI thread with no marshalling: exit guard (3 s), live strip (2 s), tape store (30 s).
5. The performance tracker starts `Task.Run(InitialiseAsync)`. It reaches the UI with a **blocking `Me.Invoke`** once per gap-fill status update, plus once at the end.
6. Until the window handle exists, every `Threading.Timer` tick is dropped by its `IsHandleCreated` check. That's correct.

**During the outage:**
- WS is down or reconnecting, so `IsDegraded()` is true.
- The backstop fires the first run. `ResolveSource` falls back to REST, which returns 5xx after retries.
- The run skips with "1m candles unavailable", and `EmitBridgeSkipped` sends a SKIPPED payload.
- Skipped runs don't touch `_fundingHistory`, `_oiHistory`, `_ofiHistory`, `_prevRegime` or the 15m cache.
- `InitialiseAsync` keeps retrying: the trailing fetch plus up to `max_gap_fill_calls=10`, each up to 2 × 15 s + 1 s.

**The −1.5% flush.** The WS feed reconnects after its backoff. The first bar roll after reconnect is only adopted and doesn't fire, so the first successful run lands on the next roll or the backstop. That's up to about one bar late, during the flush itself. The first payload is built from:
- `_fundingHistory` with one sample, so funding momentum reads FLAT.
- One OI snapshot, so there's no OI delta baseline.
- `_prevRegime = ""`, so there's no hysteresis grace.
- `_mtfCandles15m` from this run's fetch. If that one fetch failed, it's `Nothing`, and the 15m gate lets the trade through (finding 3).

The run then goes LogRun → cards → **`Await LivePerformanceTracker.UpdateAsync`, which waits on `InitialiseAsync`** → `EmitBridgeSignal`. `_autotradeArmed` is **false** after a fresh boot, so an unattended cold-start payload can't trade. That limits the cold-start-only findings (2, and the cold arm of 3). It does nothing for findings 1, 4 and 5, or for mid-life outages where the trader has armed.

---

### 1. CRITICAL: the collector freezes silently when `performance_display.enabled` is false at boot

- **Location:** `MainForm_Layout.vb:479` (`InitialiseAsync` only starts if enabled). The consequence lands at `LivePerformanceTracker.vb:524`: `Await _initTcs.Task` comes *before* the `Enabled` check at line 527. `_initTcs` is completed only inside `InitialiseAsync`.
- **Downstream impact:** On the first non-skipped run, the CSV row is logged and the cards bind. Then `RunAnalysisAsync` waits forever at `MainForm_Analysis.vb:678`. `EmitBridgeSignal` at line 733 never runs, and `btnAnalyze` is never re-enabled. From then on every auto-run fire exits at `RunAutoAnalysis`'s `If Not btnAnalyze.Enabled Then Return`. There's no MessageBox and no log line. The WS tape keeps capturing, so the box *looks* healthy.
- **Failure scenario:** Someone disables the performance strip to save memory on the 1 GiB box, or a hot-reload flips it false→true mid-session (`InitialiseAsync` is never launched after boot). Result: one CSV row, zero OK payloads, a dead collector.
- **Analytical critique:** Two files each assume the other one gates. The tracked config ships `enabled:true`, so this is latent. It's worse than the known MessageBox stall because nothing surfaces it. The fix is to check `Enabled` before the await, or to complete `_initTcs` when init isn't launched. (Proof P1.)

### 2. MEDIUM: the first OK payload waits behind the performance backfill and carries an emit-time timestamp

- **Location:** Same await, `MainForm_Analysis.vb:678`, with `generated_at_utc = DateTime.UtcNow` stamped at emit time (`MainForm_SignalBridge.vb:74`, `SignalEmitter.vb:215`).
- **Downstream impact:** The verdict, entry and levels are computed before the wait. The payload is stamped when the wait ends, so it looks fresh. During the wait all auto-run fires are dropped.
- **Failure scenario:** Cold start during an outage. The venue recovers while `InitialiseAsync` is still working through timeouts: 11 fetches at up to 31 s each is about 5.7 minutes worst case; fast 5xx failures take about 20 s. These bounds are derived from settings, not measured. An order-app staleness check keyed on `generated_at_utc` passes a price from before or early in the flush.
- **Analytical critique:** This is medium only because ARM is off after boot. The ordering is still wrong: a display-only tracker controls when the trading signal goes out. `EmitBridgeSignal` should run before the tracker update.

### 3. HIGH: the 15m "hard veto" lets trades through when it has no data, and the 15m cache never expires

- **Location:** The state is declared at `MainForm_Layout.vb:72–74`. `Core/Indicators_Structure.vb:453` defaults `gatePassLong = gatePassShort = True`, and the no-data branch returns without changing them. On a failed fetch, `MainForm_Analysis.vb:94–100` keeps the old cache with no age limit. The skip chain (lines 127–158) checks 1m, 5m and the execution stack but never 15m.
- **Downstream impact:** A directional OK payload goes out without the 15m confirmation that CLAUDE.md calls a hard veto. The CSV logs these rows as normal directional rows.
- **Failure scenario:**
  - Mid-life (trader has armed): a 30-minute outage, then the flush. 1m and 5m succeed on retry, but the 15m call fails its two attempts. The gate then judges the flush using a 15m series from before the outage, with no age limit.
  - Cold start: the cache is `Nothing`, so both sides pass.
- **Analytical critique:** The comment "stale data is better than no data" is exactly the tolerate-a-bad-state pattern the project's own rulings reject. Given the conservative-false-positive rule, both no data and data older than the gate's lookback should block or skip. (Proof P4.)

### 4. HIGH: an overflow in the status line stops the collector behind a modal dialog

- **Location:** `MainForm_Layout.vb:1987`: `CInt(Math.Max(0, (DateTime.UtcNow - _wsFeed.LastFrameUtc).TotalSeconds))`. `_lastFrameUtc` starts at `DateTime.MinValue` (`DeribitWsFeed.vb:67`). `_connected = True` is set at line 222, before `ReceiveLoopAsync` stamps the first frame (line 390).
- **Downstream impact:** The call throws `OverflowException` (about 6.4e10 s doesn't fit an Int32, and integer checks are on in the vbproj). It escapes `UpdateLogInfo`, which is called from inside `RunAnalysisAsync` at lines 167, 644 and 738. That produces the modal `btnAnalyze_Click` MessageBox, and the collector stops.
- **Failure scenario:** A degraded venue accepts the socket and the process's first subscribe completes, but no reply frame arrives. The window then stays open until the first frame. The backstop fires into it and even the skip path at line 167 throws.
- **Analytical critique:** It needs the process's first connect plus a silent server, so it's rare. But the cost is a total stall and the fix is one guard (`LastFrameUtc = MinValue` → "no frames yet"). (Proof P3.)

### 5. HIGH: typing "NaN" into MIN NET MOVE % disables the min-move gate in memory

- **Location:** `MainForm_Layout.vb:1570–1588`.
  - `Double.TryParse("NaN", Float, Invariant)` succeeds, and NaN passes both `< 0` and `> 0.01`.
  - Line 1584 writes the value into the **live singleton** before `Save`.
  - `JsonSerializer` rejects NaN, so `Save` throws. The catch shows "save failed", but the in-memory value stays NaN.
- **Downstream impact:**
  - `ScoringEngine_Calculate_Verdict.vb:316`: `floorDist > 0` is false for NaN, so the gate never fires. Every directional verdict goes out, including targets under the fee floor.
  - `_floorPctInEffect` becomes NaN in the eval cache.
  - Every later `Save` against the same singleton also throws: StartAutoRun's interval save, PersistMetricMode, trigger mode, the tweaker. `PersistMetricMode` has no try/catch, and neither does the click path into `StartAutoRun`, so those surface as unhandled-exception dialogs. `Program.vb` installs no `ThreadException` handler.
- **Failure scenario:** One mistyped entry means below-fee trades are emitted to the order app until the next restart or hot-reload. The same "mutate first, then save" order also leaves an unsaved floor live after any ordinary save failure, such as the file being locked.
- **Analytical critique:** This is a scoring change with no record in the file, which is one of CLAUDE.md's reserved classes. It needs a `Double.IsFinite` check, and the value should be applied to a copy that only replaces the live settings after `Save` succeeds. (Proof P2.)

### 6. MEDIUM: the OFI momentum history counts runs, not time, and carries across outages

- **Location:** `_ofiHistory` and `OFIHistoryMax` at `MainForm_Layout.vb:37–38`. `CalcOFIMomentum` (`Indicators_OrderFlow.vb:563`) compares against an index offset. The result feeds scoring at `ScoringEngine_Calculate_Scoring.vb:310–320`.
- **Downstream impact:** The first run after an outage compares OFI during the flush against a sample taken before the outage. That produces FALLING, which boosts the sell leg. The window's length also changes with cadence: 10 runs is 10 minutes at 1-minute runs and 30 minutes at 3-minute runs.
- **Failure scenario:** A mid-life outage followed by a flush inflates the short-side score. `_prevRegime` carries the pre-outage regime across the gap in the same way (hysteresis grace built on a regime from before the outage).
- **Analytical critique:** This is the cadence dependence v53 removed for funding momentum. The OFI history wasn't moved to timestamps with it.

### 7. MEDIUM: the performance-init callbacks still use blocking `Me.Invoke`

- **Location:** `MainForm_Layout.vb:498` and `:508`. The second one is outside any try.
- **Downstream impact:** Only one thread at a time, so this isn't the 11,630-thread pattern. But `_initTcs` completion now depends on the UI thread staying alive. If the UI thread stalls during a gap-fill status update, init never completes and finding 1's hang follows even with the strip enabled. A call to line 508 before the window exists faults the task silently.
- **Analytical critique:** It breaks the rule written into `MainForm_AutoRun.vb:17–28`. It should use `BeginInvoke`.

### 8. MEDIUM: hiding ARM AUTOTRADE doesn't disarm it

- **Location:** The checkbox is built at `MainForm_Layout.vb:771–784`. `SyncArmToggleVisibility` (`MainForm_SignalBridge.vb:47`) toggles only visibility.
- **Failure scenario:** Arm, then flip `signal_bridge.enabled` false and back to true by editing the file or the overlay. Armed payloads resume with no fresh arming step.
- **Analytical critique:** The v68 claim "ARM is off by construction" holds per process, not per enable. The fix is to force `Checked = False` whenever the checkbox is hidden.

### 9. MEDIUM: the TAPE STORE strip is blind on a REST-transport box that is capturing

- **Location:** `MainForm_TapeStoreStatus.vb:87` hides the strip when `_wsFeed Is Nothing`, but `MainForm_Layout.vb:544–548` says the repair pass alone keeps the store on REST.
- **Downstream impact:** A box that is capturing shows no capture-health readout. Even on WS, the strip never reports whether gap repair succeeded. Its header comment ("hidden when `trade_store.enabled` is false") is incomplete.

### 10. LOW: closing the form leaves the auto-run timers running

- **Location:** `OnFormClosing` (`MainForm_Layout.vb:559–583`) never calls `StopAutoRun` or `StopOnCloseWatcher`, although the comment at line 50 says it does.
- **Downstream impact:** A queued fire can start a REST-fallback run after `_wsFeed.Stop()`. That can emit a payload during shutdown, or be killed at exit partway through `LogRun`, leaving a partial CSV row.

### 11. LOW: the boot path can rewrite `settings.json`, or crash the process

- **Location:** `InitAutoRunControls` clamps the interval into the up-down controls (0–60 minutes, 0–59 seconds). `StartAutoRun`, called from the constructor, then sees a mismatch with the config.
- **Downstream impact:** An unattended boot writes `settings.json` with a different cadence: a reserved class, done with no human involved. If that write fails, the exception escapes the `MainForm` constructor and the process dies at startup.

### 12. LOW: the calibration report and the LOG line

- **Location:** `MainForm_Calibration.vb:30` and `UpdateLogInfo` (`AnalysisLogger.GetRowCount`).
- **What's wrong:**
  - The calibration report reads the whole CSV into memory with `File.ReadAllLines` on the UI thread. On a 1 GiB box that's a large allocation, and while it runs any queued fire is dropped.
  - The readiness verdict counts weekend rows and rows from different dataset boundaries (v66 and others) together. "READY FOR RECALIBRATION" can therefore be met with data the project's own rulings exclude.
  - The `TryParse` calls depend on the machine's culture.
  - `UpdateLogInfo` re-counts every CSV line three times per run on the UI thread, so the cost grows with the file size.

**Not verified:** the actual duration of `InitialiseAsync` on the box; what `WsMarketDataSource` returns for 15m after a failed reconnect seed; whether the order app checks `generated_at_utc` at all.

**Priority order:** 1 (one-line fix, silent total stop), 4 (one-line fix), then 3 and 5 (both let trades through unguarded on the live signal).
