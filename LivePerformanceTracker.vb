' LivePerformanceTracker.vb
' Per-analysis success/fail rate strip — maintains an eval cache and OHLC
' cache, resolves PENDING rows as their 15-min windows complete, and returns
' per-window aggregates for display.
'
' Host-agnostic: no System.Windows.Forms references.
' Reuses FailureRateMatrix.WalkBars for barrier-hit classification.
'
' Two persistent sidecar files (same dir as analysis_log.csv):
'   analysis_eval_cache.csv  — one row per analysis run (PENDING → resolved)
'   ohlc_1m_cache.csv        — rolling 7-day 1m OHLC (shared with future features)
'
' Six display windows: Current Week, 3-Day, Today, Asia, London, NY (most-recent-block).
' Session boundaries computed in UTC+8; stored dates converted back to UTC for filtering.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks

Public Class LivePerformanceTracker

    ' -----------------------------------------------------------------------
    ' Nested types
    ' -----------------------------------------------------------------------

    Public Class EvalCacheEntry
        Public Property Timestamp   As DateTime  ' UTC
        Public Property Verdict     As String
        Public Property EntryPrice  As Double
        Public Property FavBar      As Double    ' 0 if no valid barrier
        Public Property AdvBar      As Double    ' 0 if no valid barrier
        Public Property EvalOutcome As String    ' SUCCESS / ADVERSE_HIT / AMBIGUOUS /
                                                 ' WINDOW_EXPIRED / EXCLUDED_* / PENDING
        ' [target-hit-toggle] True iff favourable barrier was touched within T+3..T+15
        ' regardless of adverse outcome. Nothing = not yet evaluated (e.g. insufficient
        ' OHLC coverage during v1→v2 migration). Written as empty string in CSV.
        Public Property TargetEverHit As Boolean?
        ' [v36] Execution resolution (minutes) the originating run was computed on.
        ' Default 1 so legacy (pre-v4) rows and any unstamped path are 1-min. Drives
        ' the (session × resolution) aggregation filter so 3-min Asia/London rows are
        ' never blended with 1-min NY (or pre-v36 1-min Asia) rows in a session rate.
        Public Property ExecResolution As Integer = 1
    End Class

    Public Class WindowAggregate
        Public Property RangeStart      As DateTime  ' UTC+8 (display)
        Public Property RangeEnd        As DateTime  ' UTC+8 (display)
        Public Property SuccessCount    As Integer   ' barrier metric numerator
        Public Property FailureCount    As Integer
        Public Property TargetHitCount  As Integer   ' [target-hit-toggle] target metric numerator
        Public Property TotalRange      As Integer   ' all rows in range (incl. PENDING/EXCLUDED) — for tooltip

        ''' <summary>
        ''' True when this aggregate's range terminates at "now" (i.e., the
        ''' block is currently running or partially-running). False when the
        ''' range is fully in the past (a completed historical block —
        ''' yesterday's NY, an earlier Asia, etc.).
        ''' Cur.Wk / 3d / Cur.Day always set this to True since they're
        ''' rolling windows anchored at "now." Session windows set it
        ''' per-block based on whether nowUtc8 falls inside the block.
        ''' Drives the perf-strip "dim inactive sessions" rendering.
        ''' </summary>
        Public Property IsActive As Boolean = True

        ''' <summary>Barrier-hit rate. Denominator excludes rows where TargetEverHit is Nothing.</summary>
        Public ReadOnly Property BarrierRatePct As Double
            Get
                Dim n = SuccessCount + FailureCount
                If n = 0 Then Return -1.0
                Return CDbl(SuccessCount) / n * 100.0
            End Get
        End Property

        ''' <summary>Target-hit rate. Same denominator as BarrierRatePct.</summary>
        Public ReadOnly Property TargetRatePct As Double
            Get
                Dim n = SuccessCount + FailureCount
                If n = 0 Then Return -1.0
                Return CDbl(TargetHitCount) / n * 100.0
            End Get
        End Property
    End Class

    ' -----------------------------------------------------------------------
    ' Module-level state (shared across calls)
    ' -----------------------------------------------------------------------

    Private Shared _evalCache     As New List(Of EvalCacheEntry)()
    Private Shared _ohlcLookup    As New Dictionary(Of DateTime, OhlcBar)()
    Private Shared _evalCachePath As String = ""
    Private Shared _ohlcCachePath As String = ""
    Private Shared _initTcs       As New TaskCompletionSource(Of Boolean)()

    ' Slack cap: trim fires only when in-memory bar count exceeds this.
    Private Shared ReadOnly _slackCap As Integer = CInt(OhlcCache.MAX_BARS * 1.05)

    ' v35 de-confound: schema v2 → v3 (min-tradeable-move floor). v36: v3 → v4 appends
    ' the ExecResolution column. The comment retains the "min-tradeable-move" substring
    ' (so IsPreV3Schema still classifies a v4 file as ≥v3) and the floor_pct=<value> tail
    ' (so a later floor change is still detected and triggers a self-healing re-eval).
    ' [D6] schema v4 → v5: the eval barriers (FavBar/AdvBar) now come from the PLACED
    ' levels — SignalEmitter.ComputeSideLevels for live rows, the logged CSV Placed*
    ' columns for backfill — instead of the raw swing stop / ATR-fallback constants.
    ' The adverse barrier was the raw 5m swing stop (median ~9×ATR away, essentially
    ' unreachable intrabar), so "failure" had collapsed to window-expiry; scoring
    ' against the executed clamped stop makes stop-outs recordable
    ' (d6-eval-placed-stop-migration-proposal.md; d6-eval-yardstick-divergence-2026-07-08.md).
    ' Every stored outcome re-bases, so a pre-v5 cache is ROTATED to .bak and rebuilt
    ' via the cold-start backfill (D2) — it is NOT re-stamped in place. The comment
    ' keeps the "min-tradeable-move" substring (so IsPreV3Schema still classifies v5
    ' as ≥v3) and adds the "placed-level" marker (the IsPreV5Schema gate); the column
    ' header is UNCHANGED — FavBar/AdvBar change MEANING, not shape (no new column), so
    ' IsPreV4Schema stays False on a v5 file. The old FAV_ATR_MULT/ADV_ATR_MULT fallback
    ' constants are gone: ComputeSideLevels owns the fallback geometry now.
    ' [F4 no-data outcome, v5→v6, 2026-07-21] EvaluateEntry now returns a distinct
    ' NO_DATA outcome when bars.Count = 0 (offline already excludes that condition;
    ' the live tracker had folded it into WINDOW_EXPIRED, biasing rates downward and
    ' invisibly — the 07-03 slice was 22/22 fabricated expiries). NO_DATA is excluded
    ' from both numerator AND denominator in AggregateRange (TotalRange still counts
    ' it, so the tooltip shows the row exists). The comment gains the "no-data outcome"
    ' marker (the IsPreV6Schema gate for the one-time reclassification sweep — a v5
    ' file's WINDOW_EXPIRED rows are re-walked so still-uncovered rows become NO_DATA
    ' and now-covered ones keep the fresh outcome; the v5 file is copied to .v5.bak
    ' first, D6 rotation-pattern for archival). The whole-second .0000000Z timestamp
    ' provenance note (F8) also lands here as a free diagnostic for future audits.
    Private Const EVAL_SCHEMA_COMMENT As String = "# schema=v6 (placed-level barriers; min-tradeable-move floor; exec resolution; no-data outcome; whole-second .0000000Z timestamps = backfilled provenance)"
    Private Const EVAL_COL_HEADER     As String = "Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome,TargetEverHit,ExecResolution"

    ' The min-tradeable-move floor the eval cache was last written with. Set from
    ' cfg.Scoring.MinTradeableMovePct at the start of Initialise/Update; embedded in
    ' the schema comment by SchemaCommentLine().
    Private Shared _floorPctInEffect As Double = 0.0008

    ' -----------------------------------------------------------------------
    ' Public: initialise from disk (eager backfill)
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Load caches from disk, perform gap OHLC fetch, backfill eval cache from
    ''' analysis_log.csv, resolve PENDING rows, compute initial window aggregates.
    ''' Always completes the internal init task so UpdateAsync never blocks forever.
    ''' Returns a human-readable summary string for console logging.
    ''' Never throws — all errors are swallowed after logging to Console.
    ''' </summary>
    Public Shared Async Function InitialiseAsync(
            evalCachePath    As String,
            ohlcCachePath    As String,
            analysisLogPath  As String,
            cfg              As EngineSettings,
            eagerBackfill    As Boolean,
            ohlcFetcher      As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar))),
            Optional statusCallback As Action(Of String) = Nothing
        ) As Task(Of String)

        _evalCachePath = evalCachePath
        _ohlcCachePath = ohlcCachePath

        If Not cfg.PerformanceDisplay.Enabled Then
            _initTcs.TrySetResult(True)
            Return "disabled"
        End If

        Try
            Dim nowUtc  As DateTime = DateTime.UtcNow
            Dim barsFetched As Integer = 0
            Dim rowsBackfilled As Integer = 0

            ' --- Step 1: Load or fetch OHLC cache ---
            If eagerBackfill Then
                Dim lastBar As DateTime? = OhlcCache.NewestBarTime(ohlcCachePath)
                Dim sevenDaysAgo As DateTime = nowUtc.AddDays(-7)

                If lastBar.HasValue AndAlso lastBar.Value >= sevenDaysAgo Then
                    ' Load existing cache then fill the gap.
                    _ohlcLookup = OhlcCache.Load(ohlcCachePath)

                    ' Self-heal duplicates left over from earlier buggy runs. The
                    ' on-disk file is allowed to be out of order (Step 1.5 gap-fill
                    ' appends an older block after newer rows), but earlier versions
                    ' of NewestBarTime mis-read out-of-order files and triggered
                    ' redundant trailing fetches that doubled the row count.
                    ' If the file has more rows than the dict has keys, rewrite it
                    ' from the dict (already de-duplicated) before the trailing fetch
                    ' compounds the bloat further.
                    Dim fileRowCount As Integer = 0
                    For Each line As String In File.ReadLines(ohlcCachePath)
                        If Not line.StartsWith("#") AndAlso
                           Not line.StartsWith("CloseTime") AndAlso
                           Not String.IsNullOrWhiteSpace(line) Then
                            fileRowCount += 1
                        End If
                    Next
                    If fileRowCount > _ohlcLookup.Count Then
                        Console.WriteLine(String.Format(
                            "[LivePerformanceTracker] OHLC cache had {0} rows vs {1} unique bars; rewriting in canonical order",
                            fileRowCount, _ohlcLookup.Count))
                        OhlcCache.WriteAll(ohlcCachePath, _ohlcLookup.Values)
                    End If

                    Dim gapStart As DateTime = lastBar.Value   ' fetch from last bar onward
                    If (nowUtc - gapStart).TotalMinutes >= 1 Then
                        Dim gapBars = Await FetchOhlcBars(ohlcFetcher, gapStart, nowUtc)
                        Dim newBars = gapBars.Where(Function(b) b.CloseTime > lastBar.Value).ToList()
                        If newBars.Count > 0 Then
                            OhlcCache.Append(ohlcCachePath, newBars)
                            For Each b In newBars : _ohlcLookup(b.CloseTime) = b : Next
                        End If
                        barsFetched = newBars.Count
                    End If
                Else
                    ' Full 7-day fetch.
                    Dim fullBars = Await FetchOhlcBars(ohlcFetcher, sevenDaysAgo, nowUtc)
                    _ohlcLookup = fullBars.ToDictionary(Function(b) b.CloseTime)
                    OhlcCache.WriteAll(ohlcCachePath, fullBars)
                    barsFetched = fullBars.Count
                End If

                ' Trim if over slack cap.
                If _ohlcLookup.Count > _slackCap Then
                    TrimOhlcLookup()
                    OhlcCache.RollingTrim(ohlcCachePath, OhlcCache.MAX_BARS)
                End If
            End If

            ' --- Step 1.5: Detect and fill interior OHLC gaps within the 7-day window ---
            ' The trailing-gap fetch above only covers (lastBar → nowUtc). Bars from
            ' earlier hours can be missing if a previous session was interrupted mid-fetch,
            ' a Deribit response was truncated, or the engine was stopped mid-day. Scan
            ' the full 7-day window for interior gaps and back-fill each one. Throttled
            ' by max_gap_fill_calls; chunked by max_gap_fill_minutes.
            If cfg.PerformanceDisplay.GapBackfillEnabled AndAlso _ohlcLookup.Count > 0 Then
                Dim rangeStart As DateTime = nowUtc.AddDays(-7)
                Dim gaps = FindGaps(_ohlcLookup, rangeStart, nowUtc)
                If gaps.Count > 0 Then
                    Dim maxCalls   As Integer = cfg.PerformanceDisplay.MaxGapFillCalls
                    Dim chunkMins  As Integer = cfg.PerformanceDisplay.MaxGapFillMinutes
                    Dim callsUsed  As Integer = 0
                    Dim gapsFilled As Integer = 0
                    Dim gapBarsAdded As Integer = 0

                    For gIdx As Integer = 0 To gaps.Count - 1
                        If callsUsed >= maxCalls Then
                            Console.WriteLine(String.Format(
                                "[LivePerformanceTracker] Gap-fill stopped at safety cap ({0} calls); {1} gap(s) remaining",
                                maxCalls, gaps.Count - gIdx))
                            Exit For
                        End If
                        Dim gap = gaps(gIdx)
                        Dim gapMinutes As Integer = CInt((gap.EndUtc - gap.StartUtc).TotalMinutes) + 1
                        Dim chunksForGap As Integer = CInt(Math.Ceiling(gapMinutes / CDbl(chunkMins)))
                        Dim callsAvailable As Integer = maxCalls - callsUsed
                        If chunksForGap > callsAvailable Then
                            Console.WriteLine(String.Format(
                                "[LivePerformanceTracker] Gap {0:yyyy-MM-ddTHH:mmZ} → {1:yyyy-MM-ddTHH:mmZ} needs {2} chunks but only {3} call(s) left; deferring",
                                gap.StartUtc, gap.EndUtc, chunksForGap, callsAvailable))
                            Exit For
                        End If

                        If statusCallback IsNot Nothing Then
                            statusCallback.Invoke(String.Format(
                                "Loading performance history... (OHLC gap fill: {0} of {1})",
                                gIdx + 1, gaps.Count))
                        End If
                        Console.WriteLine(String.Format(
                            "[LivePerformanceTracker] Gap-fill call {0} of {1}: {2:yyyy-MM-ddTHH:mmZ} → {3:yyyy-MM-ddTHH:mmZ} ({4} bars expected)",
                            gIdx + 1, gaps.Count, gap.StartUtc, gap.EndUtc, gapMinutes))

                        Dim gapBars = Await FetchGapChunked(ohlcFetcher, gap.StartUtc, gap.EndUtc, chunkMins)
                        callsUsed += chunksForGap

                        Dim freshBars = gapBars.Where(Function(b) Not _ohlcLookup.ContainsKey(b.CloseTime)).ToList()
                        Console.WriteLine(String.Format(
                            "[LivePerformanceTracker] Gap-fill call {0} received: {1} bar(s) ({2} new, {3} duplicate/already-present)",
                            gIdx + 1, gapBars.Count, freshBars.Count, gapBars.Count - freshBars.Count))

                        If freshBars.Count > 0 Then
                            For Each b In freshBars : _ohlcLookup(b.CloseTime) = b : Next
                            OhlcCache.Append(ohlcCachePath, freshBars)
                            gapBarsAdded += freshBars.Count
                        End If
                        gapsFilled += 1
                    Next

                    ' Canonicalise file order: if any gap was filled, the file now has
                    ' the gap-fill block appended after the trailing-edge block, so it's
                    ' no longer chronological. OhlcCache.RollingTrim keeps the last N
                    ' lines by FILE POSITION, not by time — out-of-order file would
                    ' make trim discard the wrong bars once the slack cap fires. Rewrite
                    ' the file in CloseTime order from the in-memory dict (de-duplicated
                    ' by construction). UpdateAsync's per-analysis Append only ever adds
                    ' bars strictly newer than maxExisting, so order is preserved after
                    ' this point.
                    If gapBarsAdded > 0 Then
                        If _ohlcLookup.Count > _slackCap Then TrimOhlcLookup()
                        OhlcCache.WriteAll(ohlcCachePath, _ohlcLookup.Values)
                    End If

                    Console.WriteLine(String.Format(
                        "[LivePerformanceTracker] Gap-fill complete: {0} gap(s) filled, {1} bar(s) added across {2} call(s)",
                        gapsFilled, gapBarsAdded, callsUsed))
                End If
            End If

            ' --- Step 2.0: [D6] pre-v5 → placed-level barrier rotation (D2) ---
            ' The eval barriers migrated from the raw swing stop / ATR-fallback constants
            ' onto the placed levels, so every stored FavBar/AdvBar (and its outcome) is
            ' now stale. A pre-v5 cache can't be re-stamped in place — rotate it to .bak
            ' and let the cold-start backfill below rebuild it fresh on placed barriers.
            ' After the move the file is gone, so the v1→v4 migration probes and the load
            ' all see a fresh install; the perf-strip history resets (documented, D2).
            If IsPreV5Schema(evalCachePath) Then RotatePreV5Cache(evalCachePath)

            ' --- Step 2: Load existing eval cache ---
            Dim needsMigration As Boolean = IsV1Schema(evalCachePath)
            ' v35: capture the floor re-eval need BEFORE any rewrite (the migrations below
            ' re-stamp the file with the v3 comment). preV3 → one-time v2→v3 forensic
            ' re-eval; a changed stored floor → self-healing re-walk on later startups.
            Dim preV3Schema As Boolean  = IsPreV3Schema(evalCachePath)
            ' [v36] capture the v3→v4 need BEFORE any rewrite below re-stamps the header.
            Dim preV4Schema As Boolean  = IsPreV4Schema(evalCachePath)
            ' [F4 no-data outcome] Capture the v5→v6 gate BEFORE any migration below
            ' re-stamps the schema comment (the sweep is one-time; once the file is
            ' rewritten with the v6 comment, IsPreV6Schema returns False on restart).
            Dim preV6Schema As Boolean  = IsPreV6Schema(evalCachePath)
            Dim storedFloor As Double?  = ReadSchemaFloorPct(evalCachePath)
            _floorPctInEffect = cfg.Scoring.MinTradeableMovePct
            _evalCache = LoadEvalCache(evalCachePath)
            Dim existingTs As New HashSet(Of DateTime)(_evalCache.Select(Function(e) e.Timestamp))

            ' --- Step 2.5: One-time v1→v2 schema migration (target-hit-toggle) ---
            ' Detect by absence of "TargetEverHit" in the v1 header line. Walks every
            ' non-EXCLUDED, non-PENDING row against _ohlcLookup to populate TargetEverHit.
            ' Rows with no OHLC coverage stay Nothing (written as empty string).
            ' Rewrites the file with the v2 schema comment + header. Idempotent on subsequent
            ' restarts (header now contains "TargetEverHit", so this block is skipped).
            If needsMigration Then MigrateV1ToV2(nowUtc)

            ' --- Step 2.6: v35 min-tradeable-move floor re-evaluation (de-confound) ---
            ' Re-base the historical cache (loaded above) against the shared floor:
            ' gate-killed directional trades become EXCLUDED_BELOW_MIN_MOVE (out of the
            ' success/fail counts, not failures); survivors keep their barrier outcome.
            ' Runs when the cache predates v3 (one-time, with the forensic SUCCESS-inflation
            ' count) or when the trader changed the floor since it was last written. New rows
            ' added by the Step 3 backfill below are floored at birth via BuildEntry.
            Dim floorChanged As Boolean = storedFloor.HasValue AndAlso
                                          Math.Abs(storedFloor.Value - _floorPctInEffect) > 0.0000001
            If preV3Schema OrElse floorChanged Then
                ReevaluateForFloor(_floorPctInEffect, nowUtc, logForensic:=preV3Schema)
            End If

            ' --- Step 2.7: v3→v4 ExecResolution column (v36) ---
            ' Legacy (v3) rows have no ExecResolution and were all 1-min; LoadEvalCache
            ' defaulted them to 1. Rewrite once to re-stamp the file with the v4 header +
            ' explicit ExecResolution column so the resolution-filtered aggregation reads
            ' a clean schema. Idempotent: if an earlier migration already rewrote (any
            ' WriteEvalCache emits the v4 header now), this is one harmless redundant pass.
            If preV4Schema Then WriteEvalCache(_evalCachePath)

            ' --- Step 2.8: [F4] v5→v6 no-data reclassification sweep ---------------
            ' The empty-bar branch of EvaluateEntry used to return WINDOW_EXPIRED, so
            ' historical WINDOW_EXPIRED rows are a mix of genuine expiries and rows
            ' whose OHLC coverage was missing at evaluation time (see the 07-03 slice,
            ' 22/22 fabricated failures). Re-walk every WINDOW_EXPIRED row against the
            ' current OHLC lookup: still-uncovered rows become NO_DATA (excluded from
            ' the strip rate); now-covered rows keep the honest fresh outcome. The
            ' pre-v6 cache is COPIED to .v5.bak first (defensive archive — the sweep
            ' is in-place, unlike the D6 pre-v5 rotation which discards). One-time,
            ' gated by the schema comment (`preV6Schema` captured above BEFORE any
            ' earlier migration re-stamps the header). WriteEvalCache at the end
            ' stamps the v6 comment so this branch is idempotent on restart.
            If preV6Schema Then
                Try
                    Dim bakPath As String = _evalCachePath & ".v5.bak"
                    If File.Exists(bakPath) Then
                        Dim tsSuffix As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                        bakPath = _evalCachePath & ".v5." & tsSuffix & ".bak"
                    End If
                    File.Copy(_evalCachePath, bakPath)
                    Dim beforeExpired As Integer =
                        Enumerable.Count(_evalCache, Function(x) x.EvalOutcome = "WINDOW_EXPIRED")
                    ReclassifyWindowExpiredForNoData(_evalCache, _ohlcLookup, nowUtc)
                    Dim afterNoData As Integer =
                        Enumerable.Count(_evalCache, Function(x) x.EvalOutcome = "NO_DATA")
                    Dim stillExpired As Integer =
                        Enumerable.Count(_evalCache, Function(x) x.EvalOutcome = "WINDOW_EXPIRED")
                    Dim recovered As Integer = beforeExpired - stillExpired - afterNoData
                    WriteEvalCache(_evalCachePath)
                    Console.WriteLine(String.Format(
                        "[LivePerformanceTracker] v5→v6 no-data sweep: {0} WINDOW_EXPIRED re-walked → {1} NO_DATA, {2} recovered (backup {3}).",
                        beforeExpired, afterNoData, Math.Max(0, recovered), bakPath))
                Catch ex As Exception
                    Console.WriteLine("[LivePerformanceTracker] v5→v6 no-data sweep error: " & ex.Message)
                End Try
            End If

            ' --- Step 3: Backfill from analysis_log.csv ---
            If eagerBackfill AndAlso File.Exists(analysisLogPath) Then
                Dim newEntries As New List(Of EvalCacheEntry)()
                Dim logRows = ParseAnalysisLog(analysisLogPath)
                For Each row In logRows
                    If existingTs.Contains(row.Timestamp) Then Continue For
                    ' [D6] Barriers from the row's logged Placed* columns (v0.8) or the
                    ' legacy swing-else-ATR formula (pre-v0.8, D3) — see ResolveBackfillBarriers.
                    Dim favLong, advLong, favShort, advShort As Double
                    ResolveBackfillBarriers(row, cfg, favLong, advLong, favShort, advShort)
                    Dim entry As EvalCacheEntry = BuildEntry(row.Timestamp, row.Verdict,
                                                             row.EntryPrice, row.ATR,
                                                             favLong, advLong, favShort, advShort,
                                                             cfg.Scoring.MinTradeableMovePct,
                                                             row.ExecResolution)
                    ' If window complete, evaluate; otherwise leave as PENDING. The
                    ' horizon is resolution-scaled (15 min 1-min / 45 min 3-min) so a
                    ' 3-min row isn't judged before its window fills — three-min-hold-window-recal §5.
                    If entry.EvalOutcome = "PENDING" AndAlso
                       row.Timestamp.AddMinutes(EvalHorizonMinutes(entry.ExecResolution)) <= nowUtc Then
                        Dim ev = EvaluateEntry(entry, row.Timestamp, nowUtc)
                        entry.EvalOutcome   = ev.outcome
                        entry.TargetEverHit = ev.targetHit
                    End If
                    newEntries.Add(entry)
                Next
                If newEntries.Count > 0 Then
                    _evalCache.AddRange(newEntries)
                    AppendEvalRows(evalCachePath, newEntries)
                    rowsBackfilled = newEntries.Count
                End If
            End If

            ' --- Step 4: Resolve any PENDING rows whose windows are now complete ---
            ResolvePendingRows(nowUtc)

            Return String.Format("ok — ohlcBars={0} evalRows={1} backfilled={2}",
                                 _ohlcLookup.Count, _evalCache.Count, rowsBackfilled)
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] InitialiseAsync error: " & ex.Message)
            Return "error: " & ex.Message
        Finally
            _initTcs.TrySetResult(True)
        End Try
    End Function

    ' -----------------------------------------------------------------------
    ' Public: per-analysis update
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Append new OHLC bars, record the current verdict as PENDING, resolve any
    ''' PENDING rows whose resolution-scaled windows have completed (15 min for 1-min
    ''' rows, 45 min for 3-min rows), recompute window aggregates.
    ''' Awaits initialisation to complete before doing any work.
    ''' Never throws.
    ''' </summary>
    Public Shared Async Function UpdateAsync(
            v          As VerdictResult,
            r          As IndicatorResults,
            candles1m  As List(Of Candle),
            nowUtc     As DateTime
        ) As Task

        ' Wait for eager backfill to complete before touching shared state.
        Await _initTcs.Task

        Dim cfg As EngineSettings = SettingsLoader.Current
        If Not cfg.PerformanceDisplay.Enabled Then Return
        If String.IsNullOrEmpty(_evalCachePath) Then Return

        ' Keep the schema-comment floor in sync (stamped when AppendEvalRows / WriteEvalCache
        ' write a fresh header) so a mid-session settings.json floor edit is reflected.
        _floorPctInEffect = cfg.Scoring.MinTradeableMovePct

        Try
            ' --- Step 1: Append new OHLC bars from this analysis run ---
            Dim maxExisting As DateTime = If(_ohlcLookup.Count > 0,
                                             _ohlcLookup.Keys.Max(),
                                             DateTime.MinValue)
            Dim newBars As New List(Of OhlcBar)()
            If candles1m IsNot Nothing Then
                For Each c In candles1m
                    Dim bar As OhlcBar = CandleToBar(c)
                    If bar.CloseTime > maxExisting Then
                        newBars.Add(bar)
                        _ohlcLookup(bar.CloseTime) = bar
                    End If
                Next
            End If
            If newBars.Count > 0 Then
                OhlcCache.Append(_ohlcCachePath, newBars)
                If _ohlcLookup.Count > _slackCap Then
                    TrimOhlcLookup()
                    OhlcCache.RollingTrim(_ohlcCachePath, OhlcCache.MAX_BARS)
                End If
            End If

            ' --- Step 2: Append current verdict to eval cache as PENDING ---
            ' [D6] Barriers = the PLACED levels emitted this run (BuildLiveEntry →
            ' SignalEmitter.ComputeSideLevels — the same target/stop the CSV Placed*
            ' columns and the bridge payload carry), so the perf strip scores the
            ' geometry the autotrader executes, not the raw ~9×ATR swing stop.
            Dim entry As EvalCacheEntry = BuildLiveEntry(v, r, cfg, nowUtc)
            _evalCache.Add(entry)
            AppendEvalRows(_evalCachePath, New List(Of EvalCacheEntry) From {entry})

            ' --- Step 3: Resolve PENDING rows whose windows are now complete ---
            ResolvePendingRows(nowUtc)

        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] UpdateAsync error: " & ex.Message)
        End Try
    End Function

    ' -----------------------------------------------------------------------
    ' Public: compute window aggregates (pure, no I/O)
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Compute the 6 display windows over the in-memory eval cache.
    ''' Returns exactly 6 WindowAggregate in order: Week, 3d, Day, Asia, London, NY.
    ''' Never throws.
    ''' </summary>
    Public Shared Function ComputeWindows(nowUtc As DateTime,
                                          cfg    As EngineSettings) As List(Of WindowAggregate)
        Dim result As New List(Of WindowAggregate)(6)
        Try
            Dim utc8Shift  As TimeSpan = TimeSpan.FromHours(8)
            Dim nowUtc8    As DateTime = nowUtc.Add(utc8Shift)
            Dim todayUtc8  As DateTime = nowUtc8.Date

            ' --- Fixed-anchor windows (UTC+8 calendar) ---
            ' Week: Monday 00:00 UTC+8 → now
            Dim daysToMon As Integer = (CInt(nowUtc8.DayOfWeek) - CInt(DayOfWeek.Monday) + 7) Mod 7
            Dim weekStartUtc8 As DateTime = todayUtc8.AddDays(-daysToMon)

            ' 3-day: D-2 00:00 UTC+8 → now
            Dim threeDayStartUtc8 As DateTime = todayUtc8.AddDays(-2)

            ' Today: 00:00 UTC+8 → now
            Dim todayStartUtc8 As DateTime = todayUtc8

            ' Convert boundaries to UTC for eval cache filtering (subtract 8h).
            result.Add(BuildAggregate(weekStartUtc8.Subtract(utc8Shift), nowUtc,
                                      weekStartUtc8, nowUtc8))
            result.Add(BuildAggregate(threeDayStartUtc8.Subtract(utc8Shift), nowUtc,
                                      threeDayStartUtc8, nowUtc8))
            result.Add(BuildAggregate(todayStartUtc8.Subtract(utc8Shift), nowUtc,
                                      todayStartUtc8, nowUtc8))

            ' --- Session windows (most-recent-block) ---
            Dim sessions = cfg.SessionVolume.Sessions
            For Each name In New String() {"ASIA", "LONDON", "NY"}
                Dim sess = sessions.FirstOrDefault(
                    Function(s) s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                If sess Is Nothing Then
                    result.Add(New WindowAggregate())
                    Continue For
                End If
                ' Convert UTC hours to UTC+8
                Dim startH As Integer = (sess.StartHour + 8) Mod 24
                Dim endH   As Integer = (sess.EndHour   + 8) Mod 24
                ' [v36] Filter to this session's configured execution resolution.
                Dim agg = ComputeSessionWindow(startH, endH, nowUtc8, utc8Shift, sess.ExecutionResolution)
                result.Add(agg)
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] ComputeWindows error: " & ex.Message)
            ' Pad to 6 on error
            Do While result.Count < 6
                result.Add(New WindowAggregate())
            Loop
        End Try
        Return result
    End Function

    ' -----------------------------------------------------------------------
    ' Public helper: convert a Deribit Candle to an OhlcBar
    ' Used by the startup hook lambda in MainForm_Layout.
    ' -----------------------------------------------------------------------

    Public Shared Function CandleToBar(c As Candle) As OhlcBar
        ' Candle.Timestamp is Unix ms (open time). CloseTime = open + 1 min.
        Return New OhlcBar() With {
            .CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(c.Timestamp).UtcDateTime.AddMinutes(1),
            .Open  = c.Open,
            .High  = c.High,
            .Low   = c.Low,
            .Close = c.Close
        }
    End Function

    ' -----------------------------------------------------------------------
    ' Private: session boundary algorithm (§2b)
    ' -----------------------------------------------------------------------

    Private Shared Function ComputeSessionWindow(
            startH    As Integer,
            endH      As Integer,
            nowUtc8   As DateTime,
            utc8Shift As TimeSpan,
            resolutionFilter As Integer) As WindowAggregate

        Dim todayUtc8   As DateTime = nowUtc8.Date
        Dim isStraddle  As Boolean  = (endH <= startH)   ' true for NY (21→07)

        Dim blockStartUtc8 As DateTime
        Dim blockEndUtc8   As DateTime
        Dim displayEndUtc8 As DateTime
        Dim isActive       As Boolean

        If isStraddle Then
            Dim h As Integer = nowUtc8.Hour
            If h < endH Then
                ' In the tail of the session (e.g. 00:00–06:59 for NY → tail of yesterday's block)
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
                displayEndUtc8 = nowUtc8                     ' partial, running
                isActive       = True
            ElseIf h >= startH Then
                ' In the head of the session (e.g. 21:00+ for NY → today's block started)
                blockStartUtc8 = todayUtc8.AddHours(startH)
                blockEndUtc8   = todayUtc8.AddDays(1).AddHours(endH)
                displayEndUtc8 = nowUtc8                     ' partial, running
                isActive       = True
            Else
                ' Between sessions (e.g. 07:00–20:59 for NY → yesterday's block completed)
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
                displayEndUtc8 = blockEndUtc8                ' fully completed
                isActive       = False
            End If
        Else
            If nowUtc8.Hour < startH Then
                ' Before today's session — use yesterday's completed block.
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddDays(-1).AddHours(endH)
                isActive       = False
            Else
                ' Today's session (may be active or already ended).
                blockStartUtc8 = todayUtc8.AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
                isActive       = (nowUtc8.Hour < endH)        ' running iff before end-of-block
            End If
            displayEndUtc8 = If(blockEndUtc8 < nowUtc8, blockEndUtc8, nowUtc8)
        End If

        ' Convert UTC+8 boundaries back to UTC for eval cache filtering.
        Dim rangeStartUtc As DateTime = blockStartUtc8.Subtract(utc8Shift)
        Dim rangeEndUtc   As DateTime = displayEndUtc8.Subtract(utc8Shift)

        ' [v36] Filter the session rate to its configured resolution so a session's
        ' rate never blends two resolutions (e.g. legacy 1-min Asia + new 3-min Asia).
        Dim agg = BuildAggregate(rangeStartUtc, rangeEndUtc, blockStartUtc8, displayEndUtc8, resolutionFilter)
        agg.IsActive = isActive
        Return agg
    End Function

    ' -----------------------------------------------------------------------
    ' Private: aggregate builder
    ' -----------------------------------------------------------------------

    Private Shared Function BuildAggregate(
            rangeStartUtc  As DateTime,
            rangeEndUtc    As DateTime,
            displayStartUtc8 As DateTime,
            displayEndUtc8   As DateTime,
            Optional resolutionFilter As Integer = 0) As WindowAggregate

        Dim agg As WindowAggregate = AggregateRange(_evalCache, rangeStartUtc, rangeEndUtc, resolutionFilter)
        agg.RangeStart = displayStartUtc8
        agg.RangeEnd   = displayEndUtc8
        Return agg
    End Function

    ''' <summary>
    ''' [v36] Pure aggregation over an entry list (no module state) so the
    ''' resolution-filtered counting is unit-testable (A14f). resolutionFilter &gt; 0
    ''' includes ONLY rows whose ExecResolution matches it — the (session × resolution)
    ''' safety net that stops a session rate from silently blending two resolutions
    ''' (e.g. pre-v36 1-min Asia rows with post-v36 3-min Asia rows). resolutionFilter = 0
    ''' applies no filter (the cross-session week/3d/today windows are mixed-resolution
    ''' by nature). RangeStart/RangeEnd are set by the caller (display fields).
    ''' </summary>
    Friend Shared Function AggregateRange(
            entries          As List(Of EvalCacheEntry),
            rangeStartUtc    As DateTime,
            rangeEndUtc      As DateTime,
            resolutionFilter As Integer) As WindowAggregate

        Dim agg As New WindowAggregate()
        If entries Is Nothing Then Return agg
        For Each e In entries
            If e.Timestamp < rangeStartUtc OrElse e.Timestamp > rangeEndUtc Then Continue For
            If resolutionFilter > 0 AndAlso e.ExecResolution <> resolutionFilter Then Continue For
            agg.TotalRange += 1
            ' [F4 no-data outcome] NO_DATA is treated like PENDING/EXCLUDED — counted
            ' in TotalRange (the tooltip sees it) but NOT in the success/failure denominator
            ' (the strip rate excludes it). Mirrors FailureRateMatrix.Compute, where an
            ' empty bar-list `Continue For`s past the per-window increment.
            Select Case e.EvalOutcome
                Case "SUCCESS"
                    agg.SuccessCount += 1
                    ' SUCCESS always implies TargetEverHit=True by construction.
                    If e.TargetEverHit.HasValue AndAlso e.TargetEverHit.Value Then agg.TargetHitCount += 1
                Case "ADVERSE_HIT", "AMBIGUOUS", "WINDOW_EXPIRED"
                    agg.FailureCount += 1
                    If e.TargetEverHit.HasValue AndAlso e.TargetEverHit.Value Then agg.TargetHitCount += 1
            End Select
        Next
        Return agg
    End Function

    ' -----------------------------------------------------------------------
    ' Private: resolve PENDING rows
    ' -----------------------------------------------------------------------

    Private Shared Sub ResolvePendingRows(nowUtc As DateTime)
        Dim dirty As Boolean = False
        For i As Integer = 0 To _evalCache.Count - 1
            Dim e = _evalCache(i)
            If e.EvalOutcome <> "PENDING" Then Continue For
            ' Window complete only once the resolution-scaled horizon has elapsed
            ' (15 min for 1-min rows, 45 min for 3-min rows) — three-min-hold-window-recal §5.
            If e.Timestamp.AddMinutes(EvalHorizonMinutes(e.ExecResolution)) > nowUtc Then Continue For
            ' Window complete — evaluate.
            Dim ev = EvaluateEntry(e, e.Timestamp, nowUtc)
            e.EvalOutcome   = ev.outcome
            e.TargetEverHit = ev.targetHit
            dirty = True
        Next
        If dirty Then WriteEvalCache(_evalCachePath)
    End Sub

    ''' <summary>
    ''' Walk OHLC bars T+3..T+horizon (horizon = EvalHorizonMinutes(res): 15 min 1-min /
    ''' 45 min 3-min) and return both the barrier-hit outcome and the target-hit boolean
    ''' in a single pass. Production path — reads the module's shared _ohlcLookup.
    ''' targetHit = Nothing when no OHLC bars are available (NO_DATA — F4 fix).
    ''' SUCCESS always implies targetHit=True by construction.
    ''' </summary>
    Private Shared Function EvaluateEntry(e         As EvalCacheEntry,
                                           ts        As DateTime,
                                           nowUtc    As DateTime) As (outcome As String, targetHit As Boolean?)
        Return EvaluateEntry(e, ts, nowUtc, _ohlcLookup)
    End Function

    ''' <summary>
    ''' [F4 no-data outcome] Overload that takes an explicit ohlcLookup so the harness
    ''' can drive the walk deterministically without touching module state. Also the
    ''' body ReclassifyWindowExpiredForNoData routes through. Empty-bars branch now
    ''' returns NO_DATA (was WINDOW_EXPIRED) — the same condition the offline matrix
    ''' already excludes from its denominator. The degenerate-barrier early-out
    ''' (FavBar=0 OrElse AdvBar=0) stays WINDOW_EXPIRED per spec §1.
    ''' Friend for the harness A33a empty-bars fixture.
    ''' </summary>
    Friend Shared Function EvaluateEntry(e         As EvalCacheEntry,
                                          ts        As DateTime,
                                          nowUtc    As DateTime,
                                          ohlcLookup As Dictionary(Of DateTime, OhlcBar)
                                         ) As (outcome As String, targetHit As Boolean?)
        If e.FavBar = 0 OrElse e.AdvBar = 0 Then Return ("WINDOW_EXPIRED", Nothing)
        Dim bars = GetEligibleBars(ts, nowUtc, e.ExecResolution, ohlcLookup)
        If bars.Count = 0 Then Return ("NO_DATA", Nothing)
        Dim isLong As Boolean = IsLongVerdict(e.Verdict)
        Dim barrierOutcome As String  = FailureRateMatrix.WalkBars(bars, e.FavBar, e.AdvBar, isLong)
        Dim targetHit      As Boolean = FailureRateMatrix.TargetHitWalk(bars, e.FavBar, isLong)
        Return (barrierOutcome, targetHit)
    End Function

    ''' <summary>
    ''' One-time v1→v2 schema migration. Walks every non-EXCLUDED, non-PENDING row
    ''' in _evalCache against _ohlcLookup, populating TargetEverHit where bars are
    ''' available. Rows with no OHLC coverage stay Nothing. Rewrites the cache file
    ''' with the v2 schema comment + header so subsequent restarts skip this block.
    ''' </summary>
    Private Shared Sub MigrateV1ToV2(nowUtc As DateTime)
        Try
            Dim backfilled As Integer = 0
            Dim blank      As Integer = 0
            For Each e In _evalCache
                If e.EvalOutcome = "PENDING" Then Continue For
                If e.EvalOutcome IsNot Nothing AndAlso e.EvalOutcome.StartsWith("EXCLUDED") Then Continue For
                ' SUCCESS always implies TargetEverHit=True (favourable barrier hit, by definition).
                If e.EvalOutcome = "SUCCESS" Then
                    e.TargetEverHit = True
                    backfilled += 1
                    Continue For
                End If
                If e.FavBar = 0 Then
                    blank += 1
                    Continue For
                End If
                Dim bars = GetEligibleBars(e.Timestamp, nowUtc, e.ExecResolution)
                If bars.Count = 0 Then
                    blank += 1
                    Continue For
                End If
                Dim isLong As Boolean = IsLongVerdict(e.Verdict)
                e.TargetEverHit = FailureRateMatrix.TargetHitWalk(bars, e.FavBar, isLong)
                backfilled += 1
            Next
            WriteEvalCache(_evalCachePath)
            Console.WriteLine(String.Format(
                "[LivePerformanceTracker] v1→v2 migration: {0} rows backfilled, {1} blank (no OHLC)",
                backfilled, blank))
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] MigrateV1ToV2 error: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Detect v1 schema by reading the header (first non-comment line) and checking
    ''' for the absence of "TargetEverHit". Returns False if the file does not exist
    ''' (fresh installs start as v2).
    ''' </summary>
    Private Shared Function IsV1Schema(path As String) As Boolean
        If Not File.Exists(path) Then Return False
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") OrElse String.IsNullOrWhiteSpace(line) Then Continue For
                If line.StartsWith("Timestamp") Then
                    Return Not line.Contains("TargetEverHit")
                End If
                ' First non-comment, non-header data row — no header present, treat as v1.
                Return True
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] IsV1Schema error: " & ex.Message)
        End Try
        Return False
    End Function

    ' -----------------------------------------------------------------------
    ' Private: v35 min-tradeable-move floor (de-confound)
    ' -----------------------------------------------------------------------

    ''' <summary>Schema comment line with the floor the cache was last computed with embedded.</summary>
    Private Shared Function SchemaCommentLine() As String
        Return EVAL_SCHEMA_COMMENT & " floor_pct=" & _floorPctInEffect.ToString("R", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>
    ''' True when the eval cache file exists but its schema comment predates v3 (the
    ''' min-tradeable-move floor). Triggers the one-time v2→v3 re-evaluation. Returns
    ''' False when the file does not exist (fresh installs start at v3).
    ''' </summary>
    Private Shared Function IsPreV3Schema(path As String) As Boolean
        If Not File.Exists(path) Then Return False
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") Then Return Not line.Contains("min-tradeable-move")
                ' First non-comment line with no schema comment above it → pre-v3.
                Return True
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] IsPreV3Schema error: " & ex.Message)
        End Try
        Return False
    End Function

    ''' <summary>
    ''' [v36] True when the eval cache exists but its column header lacks "ExecResolution"
    ''' (a pre-v4 file). Triggers the one-time v3→v4 re-stamp (Step 2.7). False when the
    ''' file does not exist (fresh installs start at v4).
    ''' </summary>
    Private Shared Function IsPreV4Schema(path As String) As Boolean
        If Not File.Exists(path) Then Return False
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") OrElse String.IsNullOrWhiteSpace(line) Then Continue For
                If line.StartsWith("Timestamp") Then Return Not line.Contains("ExecResolution")
                ' First non-comment data row with no header above it → pre-v4.
                Return True
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] IsPreV4Schema error: " & ex.Message)
        End Try
        Return False
    End Function

    ''' <summary>
    ''' [D6] True when the eval cache exists but its schema comment predates v5 (the
    ''' placed-level barrier migration) — i.e. the comment lacks the "placed-level"
    ''' marker. Triggers the one-time rotate-and-rebuild (Step 2.0). Detection is on the
    ''' COMMENT line (not the column header, which is unchanged v4→v5). Returns False when
    ''' the file does not exist (fresh installs start at v5). Friend for harness A27d.
    ''' </summary>
    Friend Shared Function IsPreV5Schema(path As String) As Boolean
        If Not File.Exists(path) Then Return False
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") Then Return Not line.Contains("placed-level")
                ' First non-comment line with no schema comment above it → pre-v5.
                Return True
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] IsPreV5Schema error: " & ex.Message)
        End Try
        Return False
    End Function

    ''' <summary>
    ''' [D6] Rotate a pre-v5 eval cache to a ".v4.bak" sidecar (timestamp-suffixed if one
    ''' already exists) so the cold-start backfill rebuilds it fresh on placed barriers.
    ''' The raw analysis_log.csv + its own .bak history are untouched — only this derived
    ''' eval sidecar rotates (D2 raw-book safety). Friend for harness A27d.
    ''' </summary>
    Friend Shared Sub RotatePreV5Cache(path As String)
        Try
            If Not File.Exists(path) Then Return
            Dim bakPath As String = path & ".v4.bak"
            If File.Exists(bakPath) Then
                Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                bakPath = path & ".v4." & ts & ".bak"
            End If
            File.Move(path, bakPath)
            Console.WriteLine("[LivePerformanceTracker] D6 eval-barrier migration: rotated pre-v5 eval cache → " &
                              bakPath & " (rebuilding on placed-level barriers; perf-strip history resets)")
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] RotatePreV5Cache error: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' [F4 no-data outcome] True when the eval cache exists but its schema comment
    ''' predates v6 (the no-data outcome) — i.e. the comment lacks the "no-data outcome"
    ''' marker. Triggers the one-time WINDOW_EXPIRED → NO_DATA reclassification sweep
    ''' (Step 2.8). Detection is on the COMMENT line (the column header is unchanged
    ''' v5→v6). Returns False when the file does not exist (fresh installs start at v6).
    ''' Friend for the harness NO_DATA fixtures.
    ''' </summary>
    Friend Shared Function IsPreV6Schema(path As String) As Boolean
        If Not File.Exists(path) Then Return False
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") Then Return Not line.Contains("no-data outcome")
                ' First non-comment line with no schema comment above it → pre-v6.
                Return True
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] IsPreV6Schema error: " & ex.Message)
        End Try
        Return False
    End Function

    ''' <summary>
    ''' [F4 no-data outcome] Re-walk every WINDOW_EXPIRED row against a supplied OHLC
    ''' lookup: rows whose bars are still empty become NO_DATA (excluded from strip
    ''' success/failure rates); rows with coverage keep their honest fresh outcome
    ''' (SUCCESS / ADVERSE_HIT / AMBIGUOUS / WINDOW_EXPIRED). Degenerate-barrier rows
    ''' (FavBar=0 OrElse AdvBar=0) are left as WINDOW_EXPIRED — the spec keeps those
    ''' early-outs as they are. Pure (list + dict + time in, list mutated in place),
    ''' so the harness can drive it deterministically without touching module state.
    ''' Friend for the harness A33c v5→v6 sweep fixture.
    ''' </summary>
    Friend Shared Sub ReclassifyWindowExpiredForNoData(entries As List(Of EvalCacheEntry),
                                                        ohlcLookup As Dictionary(Of DateTime, OhlcBar),
                                                        nowUtc As DateTime)
        If entries Is Nothing OrElse ohlcLookup Is Nothing Then Return
        For Each e In entries
            If e.EvalOutcome <> "WINDOW_EXPIRED" Then Continue For
            ' Degenerate-barrier early-out: EvaluateEntry keeps this branch as
            ' WINDOW_EXPIRED (proposal §1 — only the empty-bars branch changes).
            If e.FavBar = 0 OrElse e.AdvBar = 0 Then Continue For
            Dim ev = EvaluateEntry(e, e.Timestamp, nowUtc, ohlcLookup)
            e.EvalOutcome   = ev.outcome
            e.TargetEverHit = ev.targetHit
        Next
    End Sub

    ''' <summary>Parse "floor_pct=&lt;x&gt;" from the schema comment; Nothing if absent (pre-v3).</summary>
    Private Shared Function ReadSchemaFloorPct(path As String) As Double?
        If Not File.Exists(path) Then Return Nothing
        Try
            For Each line As String In File.ReadLines(path)
                If Not line.StartsWith("#") Then Return Nothing   ' past the comment block
                Dim idx As Integer = line.IndexOf("floor_pct=", StringComparison.OrdinalIgnoreCase)
                If idx >= 0 Then
                    Dim raw As String = line.Substring(idx + "floor_pct=".Length).Trim()
                    Dim v As Double
                    If Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then Return v
                    Return Nothing
                End If
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] ReadSchemaFloorPct error: " & ex.Message)
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' v35 de-confound (eval-metric-deconfound-proposal.md §3). Re-evaluate every matured
    ''' directional entry against the min-tradeable-move floor:
    '''   - effective target distance |FavBar − EntryPrice| &lt; floor  → EXCLUDED_BELOW_MIN_MOVE
    '''     (a trade the live v35 gate would NO-TRADE — out of the success/fail counts, NOT
    '''     re-scored as a failure). Mirrors the gate exactly.
    '''   - survivors (&gt;= floor) are re-walked against OHLC, so rows previously excluded at a
    '''     higher floor are recovered when the floor is lowered (self-healing). FavBar/AdvBar
    '''     are unchanged, so a survivor's outcome is identical to its pre-floor value.
    ''' Non-directional / ATR-invalid exclusions and PENDING rows are left untouched.
    ''' When logForensic (the one-time v2→v3 migration), reports how many directional trades
    ''' were excluded and how many of those were SUCCESS under the old confounded barrier.
    ''' Rewrites the cache file with the v3 comment + the floor it was computed with.
    ''' </summary>
    Private Shared Sub ReevaluateForFloor(floorPct As Double, nowUtc As DateTime, logForensic As Boolean)
        Try
            Dim excluded            As Integer = 0
            Dim excludedWereSuccess As Integer = 0
            Dim recovered           As Integer = 0
            For Each e In _evalCache
                If e.EvalOutcome = "PENDING" Then Continue For
                ' Only directional rows with a real barrier are gate-relevant; leave
                ' EXCLUDED_NO_PREDICTION / EXCLUDED_ATR_INVALID as-is.
                If Not IsEligibleVerdict(e.Verdict) Then Continue For
                If e.FavBar = 0 Then Continue For

                Dim floorDist As Double = floorPct * e.EntryPrice
                If Math.Abs(e.FavBar - e.EntryPrice) < floorDist Then
                    ' Gate-killed: reclassify as EXCLUDED (mirror the live gate).
                    If e.EvalOutcome <> "EXCLUDED_BELOW_MIN_MOVE" Then
                        excluded += 1
                        If e.EvalOutcome = "SUCCESS" Then excludedWereSuccess += 1
                    End If
                    e.EvalOutcome = "EXCLUDED_BELOW_MIN_MOVE"
                Else
                    ' Survivor — re-walk so rows previously excluded at a higher floor
                    ' are recovered. FavBar/AdvBar unchanged → identical outcome on first pass.
                    If e.EvalOutcome = "EXCLUDED_BELOW_MIN_MOVE" Then recovered += 1
                    Dim ev = EvaluateEntry(e, e.Timestamp, nowUtc)
                    e.EvalOutcome   = ev.outcome
                    e.TargetEverHit = ev.targetHit
                End If
            Next
            WriteEvalCache(_evalCachePath)
            If logForensic Then
                Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
                    "[LivePerformanceTracker] v2→v3 min-tradeable-move floor ({0:P3} of price): " &
                    "{1} directional trade(s) EXCLUDED as below-min-move; {2} of those were SUCCESS under the old confounded barrier (forensic — inflation now removed).",
                    floorPct, excluded, excludedWereSuccess))
            Else
                Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
                    "[LivePerformanceTracker] min-tradeable-move floor re-eval ({0:P3}): {1} excluded, {2} recovered.",
                    floorPct, excluded, recovered))
            End If
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] ReevaluateForFloor error: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' The live eval horizon in minutes for a row's execution resolution — the largest
    ''' offline hold window (AnalysisConstants.HoldWindowsForResolution(res).Max()), so the
    ''' live perf strip and the offline failure matrix judge a trade over the SAME window
    ''' length. res=1 → 15, res=3 → 45 (three-min-hold-window-recalibration-proposal.md §5).
    ''' </summary>
    Private Shared Function EvalHorizonMinutes(execResolution As Integer) As Integer
        Return AnalysisConstants.HoldWindowsForResolution(execResolution).Max()
    End Function

    ''' <summary>
    ''' Returns the eligible bars (T+3 min through T+horizon min, using CloseTime), where
    ''' horizon = EvalHorizonMinutes(execResolution) — 15 min for 1-min rows (13 bars),
    ''' 45 min for 3-min rows. Bar CloseTime is open_time + 1 min, so CloseTime in
    ''' (ts+2min, ts+horizon].
    ''' </summary>
    Private Shared Function GetEligibleBars(ts As DateTime, nowUtc As DateTime, execResolution As Integer) As List(Of OhlcBar)
        Return GetEligibleBars(ts, nowUtc, execResolution, _ohlcLookup)
    End Function

    ''' <summary>[F4] Overload with explicit lookup so the reclassification sweep + harness
    ''' fixtures can bypass module state. Semantics identical to the shared-state version.</summary>
    Private Shared Function GetEligibleBars(ts As DateTime, nowUtc As DateTime,
                                            execResolution As Integer,
                                            ohlcLookup As Dictionary(Of DateTime, OhlcBar)) As List(Of OhlcBar)
        Dim t3   As DateTime = ts.AddMinutes(2)                                  ' CloseTime > t3 means ≥ T+3
        Dim tEnd As DateTime = ts.AddMinutes(EvalHorizonMinutes(execResolution)) ' CloseTime ≤ tEnd means ≤ T+horizon
        If ohlcLookup Is Nothing Then Return New List(Of OhlcBar)()
        Return ohlcLookup.Values.
               Where(Function(b) b.CloseTime > t3 AndAlso b.CloseTime <= tEnd).
               OrderBy(Function(b) b.CloseTime).
               ToList()
    End Function

    ' -----------------------------------------------------------------------
    ' Private: barrier construction
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' [D6] Build the eval entry for a LIVE run from the run's placed levels — the SAME
    ''' SignalEmitter.ComputeSideLevels arbitration the CSV Placed* columns and the bridge
    ''' payload read, so the perf strip scores the geometry the autotrader executes (placed
    ''' target as the favourable barrier, placed stop as the adverse), not the raw ~9×ATR
    ''' swing stop. Friend so harness A27a can pin barriers ≡ ComputeSideLevels outputs.
    ''' </summary>
    Friend Shared Function BuildLiveEntry(v As VerdictResult,
                                          r As IndicatorResults,
                                          cfg As EngineSettings,
                                          nowUtc As DateTime) As EvalCacheEntry
        Dim plLong  As SideLevels = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim plShort As SideLevels = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
        Return BuildEntry(nowUtc, v.Verdict, r.CurrentPrice, r.ATR,
                          plLong.Target, plLong.StopPx, plShort.Target, plShort.StopPx,
                          cfg.Scoring.MinTradeableMovePct, r.ExecResolution)
    End Function

    ''' <summary>
    ''' [D6] Build an EvalCacheEntry from PRE-RESOLVED per-side barriers. Callers supply the
    ''' placed target (favourable) and placed stop (adverse) for each side: BuildLiveEntry
    ''' sources them from ComputeSideLevels; the backfill sources them from the CSV Placed*
    ''' columns (v0.8) or the legacy swing-else-ATR formula (pre-v0.8, D3). BuildEntry owns
    ''' the side selection + exclusion rules; it no longer computes any fallback geometry.
    ''' </summary>
    Private Shared Function BuildEntry(
            ts              As DateTime,
            verdict         As String,
            entryPrice      As Double,
            atr             As Double,
            favBarLong      As Double,
            advBarLong      As Double,
            favBarShort     As Double,
            advBarShort     As Double,
            minMovePct      As Double,
            execResolution  As Integer) As EvalCacheEntry

        Dim e As New EvalCacheEntry() With {
            .Timestamp      = ts,
            .Verdict        = verdict,
            .EntryPrice     = entryPrice,
            .ExecResolution = execResolution
        }

        ' Excluded: non-directional verdicts
        If Not IsEligibleVerdict(verdict) Then
            e.EvalOutcome = "EXCLUDED_NO_PREDICTION"
            Return e
        End If
        ' Excluded: degenerate ATR
        If atr <= 0 Then
            e.EvalOutcome = "EXCLUDED_ATR_INVALID"
            Return e
        End If

        Dim isLong As Boolean = IsLongVerdict(verdict)

        If isLong Then
            e.FavBar = favBarLong
            e.AdvBar = advBarLong
        Else
            e.FavBar = favBarShort
            e.AdvBar = advBarShort
        End If

        ' v35 de-confound backstop: mirror the live min-tradeable-move gate. A directional
        ' entry whose favourable barrier (the placed target) can't clear the minimum
        ' tradeable move is EXCLUDED — a trade the engine won't take, not a prediction
        ' failure. Post-D6 FavBar IS the placed target, so this measures exactly the value
        ' the live Step 5c gate checks. Post-gate it rarely fires (the engine already
        ' NO-TRADEs these); it backstops historical / edge rows.
        If minMovePct > 0 AndAlso Math.Abs(e.FavBar - entryPrice) < minMovePct * entryPrice Then
            e.EvalOutcome = "EXCLUDED_BELOW_MIN_MOVE"
            Return e
        End If

        e.EvalOutcome = "PENDING"
        Return e
    End Function

    ' -----------------------------------------------------------------------
    ' Private: verdict helpers
    ' -----------------------------------------------------------------------

    Private Shared Function IsEligibleVerdict(verdict As String) As Boolean
        If String.IsNullOrEmpty(verdict) Then Return False
        Dim v = verdict.Trim().ToUpperInvariant()
        Return v = "STRONG LONG"  OrElse v = "LONG"  OrElse v = "WEAK LONG"  OrElse
               v = "STRONG SHORT" OrElse v = "SHORT" OrElse v = "WEAK SHORT"
    End Function

    Private Shared Function IsLongVerdict(verdict As String) As Boolean
        If String.IsNullOrEmpty(verdict) Then Return False
        Return verdict.Trim().ToUpperInvariant().Contains("LONG")
    End Function

    ' -----------------------------------------------------------------------
    ' Private: eval cache I/O
    ' -----------------------------------------------------------------------

    Private Shared Function LoadEvalCache(path As String) As List(Of EvalCacheEntry)
        Dim result As New List(Of EvalCacheEntry)()
        If Not File.Exists(path) Then Return result
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") OrElse line.StartsWith("Timestamp") OrElse
                   String.IsNullOrWhiteSpace(line) Then Continue For
                Dim e = ParseEvalLine(line)
                If e IsNot Nothing Then result.Add(e)
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] LoadEvalCache failed: " & ex.Message)
        End Try
        Return result
    End Function

    Private Shared Function ParseEvalLine(line As String) As EvalCacheEntry
        Try
            Dim p = line.Split(","c)
            If p.Length < 6 Then Return Nothing
            ' AdjustToUniversal: honour Z suffix and convert to UTC, setting Kind=Utc.
            ' AssumeUniversal: if a future serialiser ever drops the Z, still treat as UTC.
            ' Bug fixed 2026-05-13: previously DateTime.Parse without these flags
            ' returned Kind=Local with a shifted value; SpecifyKind(Utc) re-labelled
            ' without correcting the shift, leaving cached rows ~8h in the future
            ' relative to true UTC. All windows therefore showed 0% after restart.
            Dim ts As DateTime = DateTime.Parse(
                p(0).Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
            Dim entry As New EvalCacheEntry() With {
                .Timestamp   = ts,
                .Verdict     = p(1).Trim(),
                .EntryPrice  = Double.Parse(p(2), CultureInfo.InvariantCulture),
                .FavBar      = Double.Parse(p(3), CultureInfo.InvariantCulture),
                .AdvBar      = Double.Parse(p(4), CultureInfo.InvariantCulture),
                .EvalOutcome = p(5).Trim()
            }
            ' [target-hit-toggle] v2 7th column. Empty string = Nothing (not yet evaluated).
            If p.Length >= 7 Then
                Dim raw As String = p(6).Trim()
                If raw = "1" Then
                    entry.TargetEverHit = True
                ElseIf raw = "0" Then
                    entry.TargetEverHit = False
                End If
            End If
            ' [v36] v4 8th column ExecResolution. Absent (v3 row) ⇒ keep the default 1
            ' (1-min legacy). Re-stamped to v4 explicitly by the Step 2.7 rewrite.
            If p.Length >= 8 Then
                Dim parsedRes As Integer
                If Integer.TryParse(p(7).Trim(), parsedRes) AndAlso parsedRes > 0 Then
                    entry.ExecResolution = parsedRes
                End If
            End If
            Return entry
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Sub AppendEvalRows(path As String, entries As List(Of EvalCacheEntry))
        Try
            Dim needsHeader As Boolean = Not File.Exists(path) OrElse
                                         New FileInfo(path).Length = 0
            Using sw As New StreamWriter(path, append:=True)
                If needsHeader Then
                    sw.WriteLine(SchemaCommentLine())
                    sw.WriteLine(EVAL_COL_HEADER)
                End If
                For Each e In entries
                    sw.WriteLine(FormatEvalEntry(e))
                Next
            End Using
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] AppendEvalRows failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Rewrite the entire eval cache file (used after in-place PENDING resolves).</summary>
    Private Shared Sub WriteEvalCache(path As String)
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(SchemaCommentLine())
                sw.WriteLine(EVAL_COL_HEADER)
                For Each e In _evalCache
                    sw.WriteLine(FormatEvalEntry(e))
                Next
            End Using
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] WriteEvalCache failed: " & ex.Message)
        End Try
    End Sub

    Private Shared Function FormatEvalEntry(e As EvalCacheEntry) As String
        Dim targetCol As String = ""
        If e.TargetEverHit.HasValue Then
            targetCol = If(e.TargetEverHit.Value, "1", "0")
        End If
        Return String.Format(CultureInfo.InvariantCulture,
            "{0:o},{1},{2:F2},{3:F2},{4:F2},{5},{6},{7}",
            e.Timestamp, e.Verdict, e.EntryPrice, e.FavBar, e.AdvBar, e.EvalOutcome, targetCol, e.ExecResolution)
    End Function

    ' -----------------------------------------------------------------------
    ' Private: analysis_log.csv parser (backfill only)
    ' -----------------------------------------------------------------------

    Private Structure LogRow
        Public Timestamp    As DateTime
        Public Verdict      As String
        Public EntryPrice   As Double
        Public ATR          As Double
        Public SwingStopLong  As Double
        Public SwingStopShort As Double
        Public ExecResolution As Integer   ' [v36] 1 when absent (pre-v0.7 CSV)
        ' [D6] v0.8 placed levels — the barriers the engine emitted for this row
        ' (SignalEmitter.ComputeSideLevels at log time). HasPlaced is False for pre-v0.8
        ' rows that lack the columns; those fall back to the legacy formula.
        Public PlacedTargetLong  As Double
        Public PlacedStopLong    As Double
        Public PlacedTargetShort As Double
        Public PlacedStopShort   As Double
        Public HasPlaced         As Boolean
    End Structure

    ''' <summary>
    ''' [D6] Resolve the placed favourable/adverse barriers for a backfilled analysis_log
    ''' row. v0.8 rows carry the logged Placed* columns (computed at log time by the SAME
    ''' ComputeSideLevels arbitration) — use them directly. Pre-v0.8 rows have no logged
    ''' placed levels, so keep the legacy swing-else-ATR-fallback formula (D3: no
    ''' fabrication), sourcing the fallback multipliers from cfg (the deleted local
    ''' FAV/ADV consts were stale duplicates of these). In practice the live
    ''' analysis_log.csv is entirely v0.8 (yardstick §1), so the legacy branch is
    ''' defensive — it never fires on the live corpus.
    ''' </summary>
    Private Shared Sub ResolveBackfillBarriers(row As LogRow, cfg As EngineSettings,
                                               ByRef favLong As Double, ByRef advLong As Double,
                                               ByRef favShort As Double, ByRef advShort As Double)
        If row.HasPlaced Then
            favLong  = row.PlacedTargetLong
            advLong  = row.PlacedStopLong
            favShort = row.PlacedTargetShort
            advShort = row.PlacedStopShort
        Else
            Dim favMult  As Double = cfg.Scoring.AtrTargetMultiplier
            Dim stopMult As Double = cfg.Scoring.AtrStopMultiplier
            favLong  = row.EntryPrice + favMult * row.ATR
            advLong  = If(row.SwingStopLong > 0, row.SwingStopLong, row.EntryPrice - stopMult * row.ATR)
            favShort = row.EntryPrice - favMult * row.ATR
            advShort = If(row.SwingStopShort > 0, row.SwingStopShort, row.EntryPrice + stopMult * row.ATR)
        End If
    End Sub

    ''' <summary>
    ''' Parse analysis_log.csv for the columns needed by backfill. Columns are
    ''' resolved by HEADER NAME (same approach as ForwardWindowJoiner), never by
    ''' fixed index — schema bumps that shift column positions cannot silently
    ''' corrupt the eval cache. Rows with parse errors are silently skipped;
    ''' a header missing any required column yields an empty result.
    ''' </summary>
    Private Shared Function ParseAnalysisLog(path As String) As List(Of LogRow)
        Dim result As New List(Of LogRow)()
        Dim hasPlacedSchema As Boolean = False   ' [D6] v0.8 Placed* columns present in the header
        Try
            Dim colIdx As Dictionary(Of String, Integer) = Nothing
            For Each line As String In File.ReadLines(path)
                If colIdx Is Nothing Then
                    colIdx = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                    Dim header = line.Split(","c)
                    For i As Integer = 0 To header.Length - 1
                        colIdx(header(i).Trim()) = i
                    Next
                    For Each required As String In {"Timestamp", "Price", "Verdict", "ATR", "SwingStopLong", "SwingStopShort"}
                        If Not colIdx.ContainsKey(required) Then
                            Console.WriteLine("[LivePerformanceTracker] ParseAnalysisLog: column '" & required & "' missing from header — no rows parsed")
                            Return result
                        End If
                    Next
                    ' [D6] The four placed-level columns arrive together at the v0.8 rotation.
                    hasPlacedSchema = colIdx.ContainsKey("PlacedTargetLong") AndAlso
                                      colIdx.ContainsKey("PlacedStopLong") AndAlso
                                      colIdx.ContainsKey("PlacedTargetShort") AndAlso
                                      colIdx.ContainsKey("PlacedStopShort")
                    Continue For
                End If
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split(","c)
                Try
                    Dim row As LogRow
                    ' Timestamp stored as UTC in AnalysisLogger ("yyyy-MM-dd HH:mm:ss" — no
                    ' timezone indicator). AssumeUniversal treats unsuffixed strings as UTC;
                    ' AdjustToUniversal ensures Kind=Utc on output. Defensive against future
                    ' logger format changes that might add a Z suffix.
                    row.Timestamp = DateTime.Parse(
                        p(colIdx("Timestamp")).Trim(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
                    row.Verdict       = p(colIdx("Verdict")).Trim()
                    row.EntryPrice    = Double.Parse(p(colIdx("Price")), CultureInfo.InvariantCulture)
                    row.ATR           = Double.Parse(p(colIdx("ATR")), CultureInfo.InvariantCulture)
                    row.SwingStopLong  = Double.Parse(p(colIdx("SwingStopLong")), CultureInfo.InvariantCulture)
                    row.SwingStopShort = Double.Parse(p(colIdx("SwingStopShort")), CultureInfo.InvariantCulture)
                    ' [v36] ExecResolution is OPTIONAL (not in the required set) — legacy
                    ' (pre-v0.7) CSV rows lack it and were all 1-min, so default to 1.
                    Dim execResIdx As Integer
                    Dim parsedRes  As Integer
                    If colIdx.TryGetValue("ExecResolution", execResIdx) AndAlso execResIdx < p.Length AndAlso
                       Integer.TryParse(p(execResIdx).Trim(), parsedRes) AndAlso parsedRes > 0 Then
                        row.ExecResolution = parsedRes
                    Else
                        row.ExecResolution = 1
                    End If
                    ' [D6] v0.8 placed levels — parsed only when the schema carries them.
                    row.HasPlaced = hasPlacedSchema
                    If hasPlacedSchema Then
                        row.PlacedTargetLong  = ParseColD(p, colIdx, "PlacedTargetLong")
                        row.PlacedStopLong    = ParseColD(p, colIdx, "PlacedStopLong")
                        row.PlacedTargetShort = ParseColD(p, colIdx, "PlacedTargetShort")
                        row.PlacedStopShort   = ParseColD(p, colIdx, "PlacedStopShort")
                    End If
                    result.Add(row)
                Catch
                    ' Skip malformed row
                End Try
            Next
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] ParseAnalysisLog failed: " & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>[D6] Parse one double column by header name; 0.0 when absent/unparseable.</summary>
    Private Shared Function ParseColD(p As String(), colIdx As Dictionary(Of String, Integer), key As String) As Double
        Dim idx As Integer
        If Not colIdx.TryGetValue(key, idx) Then Return 0.0
        If idx >= p.Length Then Return 0.0
        Dim v As Double
        If Double.TryParse(p(idx).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, v) Then Return v
        Return 0.0
    End Function

    ' -----------------------------------------------------------------------
    ' Private: OHLC helpers
    ' -----------------------------------------------------------------------

    ''' <summary>Fetch bars from the OHLC fetcher delegate, returning OhlcBars. Never throws.</summary>
    Private Shared Async Function FetchOhlcBars(
            fetcher  As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar))),
            startUtc As DateTime,
            endUtc   As DateTime) As Task(Of List(Of OhlcBar))
        Try
            Dim bars = Await fetcher(startUtc, endUtc)
            Return If(bars IsNot Nothing, bars, New List(Of OhlcBar)())
        Catch ex As Exception
            Console.WriteLine("[LivePerformanceTracker] FetchOhlcBars failed: " & ex.Message)
            Return New List(Of OhlcBar)()
        End Try
    End Function

    ''' <summary>
    ''' Find contiguous runs of minute timestamps within [rangeStart, rangeEnd]
    ''' that are NOT present in the OHLC lookup. Returns list of (start, end) tuples
    ''' inclusive on both ends. Bar CloseTime semantics: a bar covering 14:00–14:01
    ''' has CloseTime 14:01:00 — the lookup is keyed by CloseTime, so the same
    ''' convention is used for the cursor here. rangeStart is truncated up to the
    ''' next whole minute via TruncateToMinute(rangeStart).AddMinutes(1) is NOT
    ''' applied; gaps start at the truncated minute itself.
    ''' </summary>
    Private Shared Function FindGaps(
            lookup     As Dictionary(Of DateTime, OhlcBar),
            rangeStart As DateTime,
            rangeEnd   As DateTime
        ) As List(Of (StartUtc As DateTime, EndUtc As DateTime))

        Dim gaps As New List(Of (StartUtc As DateTime, EndUtc As DateTime))()
        Dim cursor As DateTime = TruncateToMinute(rangeStart)
        Dim endUtc As DateTime = TruncateToMinute(rangeEnd)
        Dim gapStart As DateTime? = Nothing

        While cursor <= endUtc
            If Not lookup.ContainsKey(cursor) Then
                If Not gapStart.HasValue Then gapStart = cursor
            Else
                If gapStart.HasValue Then
                    gaps.Add((gapStart.Value, cursor.AddMinutes(-1)))
                    gapStart = Nothing
                End If
            End If
            cursor = cursor.AddMinutes(1)
        End While

        If gapStart.HasValue Then
            gaps.Add((gapStart.Value, endUtc))
        End If

        Return gaps
    End Function

    ''' <summary>Round down to the nearest whole UTC minute (drop seconds + ticks).</summary>
    Private Shared Function TruncateToMinute(t As DateTime) As DateTime
        Return New DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc)
    End Function

    ''' <summary>
    ''' Fetch a gap interval from Deribit, chunked so no single request exceeds
    ''' chunkMinutes (Deribit caps responses at ~5000 bars). Caller is responsible
    ''' for filtering already-present bars before persisting.
    ''' </summary>
    Private Shared Async Function FetchGapChunked(
            fetcher      As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar))),
            gapStart     As DateTime,
            gapEnd       As DateTime,
            chunkMinutes As Integer) As Task(Of List(Of OhlcBar))

        Dim result As New List(Of OhlcBar)()
        Dim cursor As DateTime = gapStart
        While cursor <= gapEnd
            Dim chunkEnd As DateTime = cursor.AddMinutes(chunkMinutes - 1)
            If chunkEnd > gapEnd Then chunkEnd = gapEnd
            Dim bars = Await FetchOhlcBars(fetcher, cursor, chunkEnd)
            If bars IsNot Nothing AndAlso bars.Count > 0 Then result.AddRange(bars)
            cursor = chunkEnd.AddMinutes(1)
        End While
        Return result
    End Function

    ''' <summary>Drop the oldest bars from _ohlcLookup until count == MAX_BARS.</summary>
    Private Shared Sub TrimOhlcLookup()
        If _ohlcLookup.Count <= OhlcCache.MAX_BARS Then Return
        Dim sorted = _ohlcLookup.Keys.OrderBy(Function(k) k).ToList()
        Dim toRemove = sorted.Count - OhlcCache.MAX_BARS
        For i As Integer = 0 To toRemove - 1
            _ohlcLookup.Remove(sorted(i))
        Next
    End Sub

End Class
