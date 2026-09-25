# Audit: live performance / eval pipeline (2026-09-24)

**Scope (all seven files covered — none skipped):** `LivePerformanceTracker.vb`, `OhlcCache.vb`, `AnalysisLogger.vb`,
`AnalysisOutputDump.vb`, `analysis/ForwardWindowJoiner.vb`, `analysis/FailureRateMatrix.vb`, `analysis/BandLadder.vb`.
All seven were read in full. I also read the call sites and dependencies these files rely on:
`UI/MainForm_Analysis.vb` (run pipeline), `UI/MainForm_Layout.vb` (perf-strip render and startup init),
`UI/MainForm_AutoRun.vb`, `Core/SignalEmitter.ComputeSideLevels`, `Core/ExecutionResolution.vb`,
`Core/BarCloseDetector.vb`, `DeribitClient.GetCandlesAsync`, `WsMarketDataSource.GetCandlesAsync`,
`analysis/AnalysisConstants.vb` and `settings.json` v68.

**Mode:** hostile review. No source files were changed. Proofs are isolated harnesses that link the shipped sources:
[`proofs/live-performance-eval-pipeline/`](proofs/live-performance-eval-pipeline/README.md) (run command and
full output there). Tests are referenced below as T1–T11.

**Reviewer:** Claude Code session, commissioned audit.

---

**Summary.** Eleven proofs, run against the shipped `.vb` sources, reproduce the problems below. The headline:
in NY the live perf strip does not measure the trade, because every bar it checks is a roughly one-second stub of
the minute's first trade. Even with clean bars, the strip shows a hit rate that turns green at 51%, while this
geometry needs about 78% just to break even after fees.

--- LOGIC TRACE ---

Setup: Thursday 2026-09-24, NY session (1-minute bars), WebSocket feed, on_close trigger. The 13:59 bar closes and
the watcher fires at 14:00:01.2Z.

1. **Entry.** `r.CurrentPrice = candlesExec.Last().Close` (`UI/MainForm_Analysis.vb:219`) reads the forming 14:00
   bar, about one second old: 59,003.50. The stop implies ATR = 120.23 (1.6 × ATR = 192.37). That ATR is itself
   ~14% low because of the stub.
2. **Levels.** `ComputeSideLevels` places the target at SWING_HIGH_5M 59,079.15 (+75.65, 12.82 bps) and the stop
   at FALLBACK_ATR 58,811.13 (−192.37, 32.60 bps). Neither is on the 0.5 tick grid. The min-move gate passes:
   12.82 bps against an 8 bps floor (47.20 USD). The harness printed exactly these levels.
3. **CSV.** `LogRun` writes `2026-09-24 14:00:01,59003.50,STRONG LONG,…,59079.15,58811.13,…`.
4. **PENDING.** `UpdateAsync(…, UtcNow≈14:00:01.4)`. Step 1 only appends bars with CloseTime > `maxExisting`.
   `maxExisting` is 14:00, which is the stub of the 13:59 bar stored at the 13:59:01 run. The now-complete 13:59
   bar fails `>` and is discarded, and the new 14:00 stub (O=H=L=C=59,003.5) goes in. The row is appended as
   PENDING with Fav 59,079.15 / Adv 58,811.13.
5. **Resolution.** The row is judged on the first run at or after ts+15m. `GetEligibleBars` keeps CloseTime in
   (14:02:01.4, 14:15:01.4], which is 13 bars covering 14:02–14:15. The first 1m58s of the trade are never
   examined, and all 13 bars are stubs.
6. **The bar that touches both levels.** The exchange's 14:04 bar is O 59,020 / H 59,085 / L 58,790 / C 58,950,
   through both levels. The cache holds it as 59,020/59,020/59,020/59,020.
   - `WalkBars` sees no touch, so the result is WINDOW_EXPIRED with TargetEverHit=False.
   - On the exchange's own bars, the same row is AMBIGUOUS with TargetEverHit=True.
   - `WriteEvalCache` then rewrites the whole eval cache file on the UI thread.
7. **Aggregates.** The row is counted in Cur.Wk, 3d, Cur.Day and the NY block (13:00–23:00Z, 1-minute filter,
   weekday, not WEAK): FailureCount +1, TargetHitCount +0.
8. **Strip.** The NY cell appears once n ≥ 10 and is green when rate > 50 (`UI/MainForm_Layout.vb:1688`). The
   output dump is fully read and rewritten, and only then does `EmitBridgeSignal` run
   (`MainForm_Analysis.vb:733`).
9. **What the trade actually did.** Both levels printed inside the 14:04 bar. In a flush the low most likely came
   first, so the trade was stopped out. By the project's own performance definitions
   (`docs/DeribitIndicatorProject.md` §5a), net EV for this row is −32.60 − 3 = −35.6 bps. The strip books
   "1 failure", exactly as it would book a timeout at +10 bps. Net breakeven for this geometry is
   (32.60 + 3) / (12.82 + 32.60) = **78.4%**.

---

### 1. CRITICAL — Forming-bar stubs are frozen into the live OHLC cache
- **LOCATION:** `LivePerformanceTracker.vb:536-548` (the "newer than maxExisting" append), `:270-279` (the startup
  trailing fetch uses the same rule), `:341` (gap-fill only adds missing keys). The stubs come from
  `DeribitClient.vb:167` (end=now) and `WsMarketDataSource.GetCandlesAsync`.
- **DOWNSTREAM IMPACT:** Every live EvalOutcome and TargetEverHit, every strip cell, and `ohlc_1m_cache.csv`.
  - NY: all 13 bars the eval checks are stubs.
  - Asia/London: one bar in three.
  - Interval mode: partial bars of random age.
  - A restart doesn't fix it: the stubs stay in the file and nothing re-walks resolved rows.
- **FAILURE SCENARIO:** T1 (38 on_close runs through the shipped `UpdateAsync`):
  - 37 of 37 cached bars have High=Low.
  - The bar touching both levels scores WINDOW_EXPIRED; on the real bars it is AMBIGUOUS.
  - A bar that only wicks the target scores WINDOW_EXPIRED; on the real bars it is SUCCESS.
- **ANALYTICAL CRITIQUE:** The live check is "did the minute's first print cross the level". All touches collapse
  into WINDOW_EXPIRED, so the NY rate is really a timeout rate. The offline matrix walks complete bars, so the
  strip and the matrix disagree on the same rows by construction. I found no doc flagging this;
  `ohlc-gap-backfill-proposal.md:19` describes the rule as intended design.

### 2. CRITICAL — Nothing in these files computes net EV, and the colour rule implies an edge that isn't there
- **LOCATION:**
  - `AggregateRange` / `WindowAggregate` in `LivePerformanceTracker.vb`
  - `FailureRateMatrix.Compute`
  - `BandLadder.Compute`
  - The consumer at `MainForm_Layout.vb:1688`
- **DOWNSTREAM IMPACT:** The strip, the failure matrix, the band ladder re-read, the auto-tweaker inputs.
- **FAILURE SCENARIO:** With the trace geometry, a green 60% gives 0.6 × (12.82 − 3) − 0.4 × (32.60 + 3) =
  **−8.4 bps per trade**, even if every failure were a clean stop. Timeouts are scored as full failures whatever
  the price did.
- **ANALYTICAL CRITIQUE:**
  - The eval cache stores entry and both levels but no exit mark. OHLC is kept for 7 days, so after a week the
    timeout mark §5a needs can't be rebuilt.
  - Green at 50% assumes 1:1 reward:risk; this geometry is 1:0.39.
  - The target-hit toggle **[T]** counts a trade that was stopped out and later touched the target as a hit.
  - `BandLadder` compares raw success rates across bands whose placed distances differ. That makes
    "WEAK beats MEDIUM" confounded by geometry, which is exactly the case §5a rule 4 warns about.

### 3. HIGH — n counts runs, not independent trades
- **LOCATION:**
  - `FailureRateMatrix.WilsonCI` (`:362-372`) and the MinSamplesPerCell gate (`:342`)
  - The CIs in `BandLadder`
  - The strip's `min_sample_for_render=10`
- **DOWNSTREAM IMPACT:**
  - IsRecommended → `AutoTweakerCore.vb:434` and `picked_cell_history.csv`
  - The ladder's "n≥150 STRONG" re-read gate
- **FAILURE SCENARIO:** With 1-minute runs and a 15-minute window, each bar sits in up to 13 rows' walks.
  `docs/medium-tier-diagnosis-read-2026-09-17.md:177` measured **12.6 NY signals per episode**. So one flush
  produces about 13 correlated failures, a 30-sample cell can be 2–3 episodes, and the strip's n=10 can be a
  single episode.
- **ANALYTICAL CRITIQUE:** The repo's own SwingFallbackRead uses cluster-robust CIs for this reason; the matrix and
  ladder don't.

### 4. HIGH — No fill model, and a 2-minute blind spot
- **LOCATION:** `GetEligibleBars` (`:1203-1213`), `ForwardWindowJoiner.PopulateForwardBars` (`:262-282`),
  `WalkBars`.
- **FAILURE SCENARIO:** T11 — a wick to 58,780 in the 14:01–14:02 bar goes through the stop, then the target prints
  at 14:09. The eval scores SUCCESS; the real trade was stopped out. The "T+3" label is also wrong: the first bar
  checked starts at about T+2m.
- **ANALYTICAL CRITIQUE:**
  - Entry is priced at T0 but the price path starts about 2 minutes later.
  - The fee model assumes a maker entry, but the eval assumes every signal fills. A bid that never fills during a
    rally is still scored as a win, which is adverse selection in the optimistic direction.
  - A touch counts as a fill even for off-grid levels. The payload doesn't round levels to the tick
    (SignalEmitter uses `TickSize` only for noise floors).
  - Stop slippage and the stop's trigger type aren't modelled. I can't see how the order app handles either
    (it's a different repo).
  - SwingFallbackRead starts its walk at the bar containing the signal, a third convention for the same row.

### 5. HIGH — Changing the floor while the engine is running disables the self-healing re-evaluation
- **LOCATION:**
  - `:530-532`: `UpdateAsync` copies the live floor.
  - `:946-948` and `:1577-1589`: every rewrite stamps that floor into the file.
  - `:418-422`: the restart check compares the stamp with settings.
- **FAILURE SCENARIO:** T3 —
  1. Row R1 (6.0 bps target) is excluded at the 8 bps floor.
  2. The floor is cut to 4 bps while the engine runs.
  3. One PENDING row resolves, and the file is restamped `floor_pct=0.0004` with R1 still excluded.
  4. On restart the stamp equals settings, so no re-evaluation runs and R1 stays EXCLUDED.

  Control: a fresh cache built at 4 bps scores R1 as SUCCESS.
- **ANALYTICAL CRITIQUE:** `min_net_move_pct` is documented as UI-adjustable and hot-reloadable, so this is the
  normal edit path. The file ends up mixing two floors under a header that names only one.

### 6. MEDIUM — A floor change at startup turns every row older than 7 days into NO_DATA, and reports nothing
- **LOCATION:** `ReevaluateForFloor` (`:1136-1179`) re-walks against `_ohlcLookup`, which is capped at 10,080 bars
  (7 days).
- **FAILURE SCENARIO:** T4 — a 10-day-old SUCCESS and a 9-day-old ADVERSE_HIT both become NO_DATA with
  TargetEverHit null. The console prints "0 excluded, 0 recovered".
- **DOWNSTREAM IMPACT:** The eval cache is the only persisted record of how the live path judged each row, and
  SwingFallbackRead's join drops NO_DATA rows.
- **ANALYTICAL CRITIQUE:** The one-time v5→v6 sweep (`:444-467`) used the same walk. So it likely reclassified
  genuine old expiries as NO_DATA. That is inferred from the code; I can't see the production file.

### 7. MEDIUM — Session cells drop the last hour of every session
- **LOCATION:** `ComputeSessionWindow` (`:656-716`) treats `end_hour` as exclusive. The engine's session matching
  (`Core/ExecutionResolution.vb:44`) treats it as inclusive.
- **FAILURE SCENARIO:** T2 — rows at 07:30, 12:30 and 23:30 UTC are missing from their session cells. At 12:45Z
  London shows IsActive=False and its block ends at 12:00; NY behaves the same at 23:45Z.
- **ANALYTICAL CRITIQUE:** 12:00–12:59 UTC is the 08:30 ET US-data hour during EDT. The London cell excludes its
  most volatile hour: 1/5 of London rows, 1/8 of Asia, 1/11 of NY.

### 8. MEDIUM — Partial bar coverage is scored as if complete
- **LOCATION:** `EvaluateEntry :871` (NO_DATA only when there are zero bars), `FailureRateMatrix.vb:288`,
  `BandLadder.vb:442-445`.
- **FAILURE SCENARIO:** T10 — 1 of 13 bars present scores WINDOW_EXPIRED, i.e. a failure.
- **ANALYTICAL CRITIQUE:** The earlier fix for fabricated expiries closed only the empty case; the same fabrication
  survives with 12 of 13 bars missing.

### 9. MEDIUM — Display bookkeeping runs before the bridge signal
- **LOCATION:** `MainForm_Analysis.vb:678 → 684 → 733`.
- **FAILURE SCENARIO:**
  - A full eval-cache rewrite of about 11 MB took 0.1–0.6 s per resolution here.
  - At the 3,000-run cap, every run reads and rewrites the whole 16.9 MB dump: 307 ms per run here, on Linux with
    no antivirus.
  - Both finish before `EmitBridgeSignal`.
  - This is the same UI-thread I/O class the `GetRowCount` fix (`AnalysisLogger.vb:132-150`) addressed. The dump
    trim is a bigger read plus a write.
- **Already known, but the blast radius:** with performance display off at startup, `btnAnalyze` stays disabled and
  `RunAutoAnalysis` returns on its first line. That means one run, then no CSV rows and no bridge signals, silently.

### 10. MEDIUM — Eval cache and dump rewrites aren't atomic
- **LOCATION:** `WriteEvalCache` opens with `append:=False`; `TrimToMaxRuns` uses `File.WriteAllLines`.
  `OhlcCache.WriteAll` already does it correctly (write to temp, then move).
- **FAILURE SCENARIO:** T5 — a fault at row 40,000 leaves 40,000 of 75,000 rows, and the good file is gone. The
  2026-09-18 box crash is exactly this kind of kill mid-write.

### 11. MEDIUM — `LogRun` drops rows silently
- **LOCATION:** `AnalysisLogger.vb:208-210` and `:369-371`.
- **FAILURE SCENARIO:** T8 — with the CSV locked, rows before=1, after=1, and nothing on the console.
- **DOWNSTREAM IMPACT:** The eval cache still gets the row but the CSV doesn't, so the strip and the offline tools
  see different populations with no counter.
- **Untested:** that Excel's lock on Windows triggers this.

### 12. MEDIUM — IsMostProfitable is a constant
- **LOCATION:** `FailureRateMatrix.vb:342-354`.
- **FAILURE SCENARIO:** T7 — across 200 random-walk books × 4 tiers, 800 of 800 flagged cells were the 15-minute
  window.
- **ANALYTICAL CRITIQUE:** With fixed levels and nested windows, a longer window can only turn a timeout into a
  success. The failure rate never rises with window length, so the flag always says "hold the full window" and
  says nothing about profit.

### 13. LOW — The CSV timestamp depends on the machine's locale
- **LOCATION:** `AnalysisLogger.vb:237`.
- **FAILURE SCENARIO:** T9 — 22 locales change the format. th-TH writes the year 2569, which both parsers accept as
  a real date. None of the UTC+8 locales I checked are affected.

### 14. LOW — A doc comment is false
- **LOCATION:** `LivePerformanceTracker.vb:114`.
- The comment says rows with TargetEverHit=Nothing are excluded from the denominator. Rows with a degenerate level
  and TargetEverHit=Nothing are counted as failures (`:809-815`).

---

**Proof code:** [`proofs/live-performance-eval-pipeline/`](proofs/live-performance-eval-pipeline/README.md). It was
originally run from the session scratchpad. It links the shipped sources and writes only to `/tmp` and its own bin
folder. I installed the .NET 8 SDK in the audit container through apt. No source files were changed.

**Not verified:**
- How the order app rounds levels and triggers stops.
- The real width of production stubs; the WebSocket stub may hold several trades rather than one.
- Excel's lock behaviour.
- The production cache contents.

Of the session-start docs, I read only §5, §5a, §8 and §10 of `docs/DeribitIndicatorProject.md`; I didn't read
`architecture.md` or `trader-profile.md`.
