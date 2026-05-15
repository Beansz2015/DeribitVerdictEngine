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
    End Class                                    ' WINDOW_EXPIRED / EXCLUDED_* / PENDING

    Public Class WindowAggregate
        Public Property RangeStart    As DateTime  ' UTC+8 (display)
        Public Property RangeEnd      As DateTime  ' UTC+8 (display)
        Public Property SuccessCount  As Integer
        Public Property FailureCount  As Integer
        Public Property TotalRange    As Integer   ' all rows in range (incl. PENDING/EXCLUDED) — for tooltip
        Public ReadOnly Property RatePct As Double
            Get
                Dim n = SuccessCount + FailureCount
                If n = 0 Then Return -1.0
                Return CDbl(SuccessCount) / n * 100.0
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

    Private Const EVAL_SCHEMA_COMMENT As String = "# schema=v1 (live-performance-display)"
    Private Const EVAL_COL_HEADER     As String = "Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome"
    Private Const FAV_ATR_MULT        As Double = 2.0   ' fallback favourable barrier
    Private Const ADV_ATR_MULT        As Double = 1.2   ' fallback adverse barrier

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

            ' --- Step 2: Load existing eval cache ---
            _evalCache = LoadEvalCache(evalCachePath)
            Dim existingTs As New HashSet(Of DateTime)(_evalCache.Select(Function(e) e.Timestamp))

            ' --- Step 3: Backfill from analysis_log.csv ---
            If eagerBackfill AndAlso File.Exists(analysisLogPath) Then
                Dim newEntries As New List(Of EvalCacheEntry)()
                Dim logRows = ParseAnalysisLog(analysisLogPath)
                For Each row In logRows
                    If existingTs.Contains(row.Timestamp) Then Continue For
                    Dim entry As EvalCacheEntry = BuildEntry(row.Timestamp, row.Verdict,
                                                             row.EntryPrice, row.ATR,
                                                             row.SwingStopLong, row.SwingStopShort,
                                                             adjLongTarget:=0.0,
                                                             adjShortTarget:=0.0)
                    ' If window complete, evaluate; otherwise leave as PENDING.
                    If entry.EvalOutcome = "PENDING" AndAlso
                       row.Timestamp.AddMinutes(15) <= nowUtc Then
                        entry.EvalOutcome = EvaluateEntry(entry, row.Timestamp, nowUtc)
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
    ''' PENDING rows whose 15-min windows have completed, recompute window aggregates.
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
            Dim entry As EvalCacheEntry = BuildEntry(
                nowUtc, v.Verdict, r.CurrentPrice, r.ATR,
                r.SwingStopLong, r.SwingStopShort,
                v.AdjustedLongTarget, v.AdjustedShortTarget)
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
                Dim agg = ComputeSessionWindow(startH, endH, nowUtc8, utc8Shift)
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
            utc8Shift As TimeSpan) As WindowAggregate

        Dim todayUtc8   As DateTime = nowUtc8.Date
        Dim isStraddle  As Boolean  = (endH <= startH)   ' true for NY (21→07)

        Dim blockStartUtc8 As DateTime
        Dim blockEndUtc8   As DateTime
        Dim displayEndUtc8 As DateTime

        If isStraddle Then
            Dim h As Integer = nowUtc8.Hour
            If h < endH Then
                ' In the tail of the session (e.g. 00:00–06:59 for NY → tail of yesterday's block)
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
                displayEndUtc8 = nowUtc8                     ' partial, running
            ElseIf h >= startH Then
                ' In the head of the session (e.g. 21:00+ for NY → today's block started)
                blockStartUtc8 = todayUtc8.AddHours(startH)
                blockEndUtc8   = todayUtc8.AddDays(1).AddHours(endH)
                displayEndUtc8 = nowUtc8                     ' partial, running
            Else
                ' Between sessions (e.g. 07:00–20:59 for NY → yesterday's block completed)
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
                displayEndUtc8 = blockEndUtc8                ' fully completed
            End If
        Else
            If nowUtc8.Hour < startH Then
                ' Before today's session — use yesterday's completed block.
                blockStartUtc8 = todayUtc8.AddDays(-1).AddHours(startH)
                blockEndUtc8   = todayUtc8.AddDays(-1).AddHours(endH)
            Else
                ' Today's session (may be active or already ended).
                blockStartUtc8 = todayUtc8.AddHours(startH)
                blockEndUtc8   = todayUtc8.AddHours(endH)
            End If
            displayEndUtc8 = If(blockEndUtc8 < nowUtc8, blockEndUtc8, nowUtc8)
        End If

        ' Convert UTC+8 boundaries back to UTC for eval cache filtering.
        Dim rangeStartUtc As DateTime = blockStartUtc8.Subtract(utc8Shift)
        Dim rangeEndUtc   As DateTime = displayEndUtc8.Subtract(utc8Shift)

        Return BuildAggregate(rangeStartUtc, rangeEndUtc, blockStartUtc8, displayEndUtc8)
    End Function

    ' -----------------------------------------------------------------------
    ' Private: aggregate builder
    ' -----------------------------------------------------------------------

    Private Shared Function BuildAggregate(
            rangeStartUtc  As DateTime,
            rangeEndUtc    As DateTime,
            displayStartUtc8 As DateTime,
            displayEndUtc8   As DateTime) As WindowAggregate

        Dim agg As New WindowAggregate() With {
            .RangeStart = displayStartUtc8,
            .RangeEnd   = displayEndUtc8
        }
        For Each e In _evalCache
            If e.Timestamp < rangeStartUtc OrElse e.Timestamp > rangeEndUtc Then Continue For
            agg.TotalRange += 1
            Select Case e.EvalOutcome
                Case "SUCCESS"
                    agg.SuccessCount += 1
                Case "ADVERSE_HIT", "AMBIGUOUS", "WINDOW_EXPIRED"
                    agg.FailureCount += 1
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
            If e.Timestamp.AddMinutes(15) > nowUtc Then Continue For
            ' Window complete — evaluate.
            e.EvalOutcome = EvaluateEntry(e, e.Timestamp, nowUtc)
            dirty = True
        Next
        If dirty Then WriteEvalCache(_evalCachePath)
    End Sub

    ''' <summary>Walk OHLC bars T+3..T+15 and return WalkBars outcome string.</summary>
    Private Shared Function EvaluateEntry(e         As EvalCacheEntry,
                                           ts        As DateTime,
                                           nowUtc    As DateTime) As String
        If e.FavBar = 0 OrElse e.AdvBar = 0 Then Return "WINDOW_EXPIRED"
        Dim bars = GetEligibleBars(ts, nowUtc)
        If bars.Count = 0 Then Return "WINDOW_EXPIRED"
        Dim isLong As Boolean = IsLongVerdict(e.Verdict)
        Return FailureRateMatrix.WalkBars(bars, e.FavBar, e.AdvBar, isLong)
    End Function

    ''' <summary>
    ''' Returns the 13 eligible bars (T+3 min through T+15 min, using CloseTime).
    ''' Bar CloseTime is open_time + 1 min, so CloseTime in (ts+2min, ts+15min].
    ''' </summary>
    Private Shared Function GetEligibleBars(ts As DateTime, nowUtc As DateTime) As List(Of OhlcBar)
        Dim t3  As DateTime = ts.AddMinutes(2)   ' CloseTime > t3  means ≥ T+3
        Dim t15 As DateTime = ts.AddMinutes(15)  ' CloseTime ≤ t15 means ≤ T+15
        Return _ohlcLookup.Values.
               Where(Function(b) b.CloseTime > t3 AndAlso b.CloseTime <= t15).
               OrderBy(Function(b) b.CloseTime).
               ToList()
    End Function

    ' -----------------------------------------------------------------------
    ' Private: barrier construction
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Build an EvalCacheEntry. adjLongTarget/adjShortTarget = 0 during backfill
    ''' (not in analysis_log CSV), meaning the ATR fallback is always used then.
    ''' </summary>
    Private Shared Function BuildEntry(
            ts              As DateTime,
            verdict         As String,
            entryPrice      As Double,
            atr             As Double,
            swingStopLong   As Double,
            swingStopShort  As Double,
            adjLongTarget   As Double,
            adjShortTarget  As Double) As EvalCacheEntry

        Dim e As New EvalCacheEntry() With {
            .Timestamp  = ts,
            .Verdict    = verdict,
            .EntryPrice = entryPrice
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
            e.FavBar = If(adjLongTarget > 0, adjLongTarget, entryPrice + FAV_ATR_MULT * atr)
            e.AdvBar = If(swingStopLong > 0, swingStopLong, entryPrice - ADV_ATR_MULT * atr)
        Else
            e.FavBar = If(adjShortTarget > 0, adjShortTarget, entryPrice - FAV_ATR_MULT * atr)
            e.AdvBar = If(swingStopShort > 0, swingStopShort, entryPrice + ADV_ATR_MULT * atr)
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
            Return New EvalCacheEntry() With {
                .Timestamp   = ts,
                .Verdict     = p(1).Trim(),
                .EntryPrice  = Double.Parse(p(2), CultureInfo.InvariantCulture),
                .FavBar      = Double.Parse(p(3), CultureInfo.InvariantCulture),
                .AdvBar      = Double.Parse(p(4), CultureInfo.InvariantCulture),
                .EvalOutcome = p(5).Trim()
            }
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
                    sw.WriteLine(EVAL_SCHEMA_COMMENT)
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
                sw.WriteLine(EVAL_SCHEMA_COMMENT)
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
        Return String.Format(CultureInfo.InvariantCulture,
            "{0:o},{1},{2:F2},{3:F2},{4:F2},{5}",
            e.Timestamp, e.Verdict, e.EntryPrice, e.FavBar, e.AdvBar, e.EvalOutcome)
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
    End Structure

    ''' <summary>
    ''' Parse the analysis_log.csv (v0.4.1 schema) for the columns needed by backfill.
    ''' Columns: 0=Timestamp, 1=Price, 2=Verdict, 63=ATR, 81=SwingStopLong, 82=SwingStopShort.
    ''' Rows with parse errors are silently skipped.
    ''' </summary>
    Private Shared Function ParseAnalysisLog(path As String) As List(Of LogRow)
        Dim result As New List(Of LogRow)()
        Try
            Dim first As Boolean = True
            For Each line As String In File.ReadLines(path)
                If first Then
                    first = False
                    Continue For  ' skip header row
                End If
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split(","c)
                If p.Length < 83 Then Continue For
                Try
                    Dim row As LogRow
                    ' Timestamp stored as UTC in AnalysisLogger ("yyyy-MM-dd HH:mm:ss" — no
                    ' timezone indicator). AssumeUniversal treats unsuffixed strings as UTC;
                    ' AdjustToUniversal ensures Kind=Utc on output. Defensive against future
                    ' logger format changes that might add a Z suffix.
                    row.Timestamp = DateTime.Parse(
                        p(0).Trim(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
                    row.Verdict       = p(2).Trim()
                    row.EntryPrice    = Double.Parse(p(1), CultureInfo.InvariantCulture)
                    row.ATR           = Double.Parse(p(63), CultureInfo.InvariantCulture)
                    row.SwingStopLong  = Double.Parse(p(81), CultureInfo.InvariantCulture)
                    row.SwingStopShort = Double.Parse(p(82), CultureInfo.InvariantCulture)
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
