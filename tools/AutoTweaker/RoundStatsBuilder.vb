' tools/AutoTweaker/RoundStatsBuilder.vb
' Renders a human-readable summary of the last N auto-tweaker rounds for the
' RoundStatsForm display (settings-snapshot-history-proposal.md §3k).
'
' For each round we present:
'   - Outcome / aggregate failure rate / window row span
'   - Per-verdict-tier accuracy using the v2 barrier-hit logic, applied to
'     ALL directional verdicts (including WEAK_*) per spec §3k+ §7.
'   - NO_TRADE rows: informational count only, no success/fail evaluation.
'
' OHLC bars for the row windows are fetched once via DeribitOhlcFetcher and
' shared across rounds. The barrier walk reuses FailureRateMatrix.WalkBars
' directly so we share the canonical SUCCESS / ADVERSE_HIT / WINDOW_EXPIRED /
' AMBIGUOUS semantics.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks

Public Class RoundStatsBuilder

    Private Const FavWindowMin    As Integer = 10        ' default eval window for tier rows
    Private Const FavAtrThreshold As Double = 0.5        ' middle threshold for both STRONG / MEDIUM

    Public Shared Async Function BuildAsync(state As TweakerState,
                                             csvPath As String,
                                             snapshotsDir As String,
                                             manifestPath As String,
                                             config As TweakerConfig,
                                             n As Integer) As Task(Of String)
        Dim sb As New StringBuilder()
        sb.AppendLine(New String("="c, 72))
        sb.AppendLine(String.Format("  ROUND STATISTICS — last {0} auto-tweaker rounds", n))
        sb.AppendLine(New String("="c, 72))
        sb.AppendLine()

        ' Streak / snapshot header
        sb.AppendLine(String.Format("  Current streak: {0} successful round(s) (need {1} for snapshot — {2})",
            state.CurrentBelowThresholdStreak,
            config.SnapshotStreakX,
            If(String.IsNullOrEmpty(state.ActiveSnapshotFilename), "INACTIVE", "ACTIVE")))
        If Not String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then
            sb.AppendLine("  Active snapshot: " & state.ActiveSnapshotFilename)
            Dim bucket = LookupBucketForActiveSnapshot(state.ActiveSnapshotFilename, manifestPath)
            If bucket.Length > 0 Then sb.AppendLine("  Snapshot bucket: " & bucket)
        End If
        sb.AppendLine()

        If state.RoundHistory Is Nothing OrElse state.RoundHistory.Count = 0 Then
            sb.AppendLine("  (No rounds in history yet)")
            Return sb.ToString()
        End If

        ' Last N rounds (most-recent last)
        Dim rounds = state.RoundHistory.
            Skip(Math.Max(0, state.RoundHistory.Count - n)).
            ToList()

        ' Load CSV once for all rounds
        Dim lines As String() = If(File.Exists(csvPath), File.ReadAllLines(csvPath), New String() {})
        Dim header As String() = If(lines.Length >= 1, lines(0).Split(","c), New String() {})
        Dim cols = ResolveColumns(header)

        ' Fetch OHLC for the entire span covered by these rounds
        Dim ohlcMap As Dictionary(Of DateTime, OhlcBar) = Nothing
        Dim spanStart, spanEnd As DateTime
        If TryComputeSpan(lines, cols, rounds, spanStart, spanEnd) Then
            Try
                ohlcMap = Await DeribitOhlcFetcher.FetchOhlcRange(spanStart, spanEnd.AddMinutes(FavWindowMin + 5))
            Catch ex As Exception
                sb.AppendLine("  [OHLC fetch failed: " & ex.Message & " — accuracy panels suppressed]")
                sb.AppendLine()
            End Try
        End If

        ' Render rounds — most recent first feels more useful day-to-day
        rounds.Reverse()
        For Each round In rounds
            sb.Append(RenderRound(round, lines, cols, ohlcMap))
            sb.AppendLine()
        Next

        Return sb.ToString()
    End Function

    ' ── Per-round rendering ─────────────────────────────────────────────────

    Private Class CsvCols
        Public Property Timestamp As Integer = -1
        Public Property Price     As Integer = -1
        Public Property Verdict   As Integer = -1
        Public Property Atr       As Integer = -1
        Public Property StopLong  As Integer = -1
        Public Property StopShort As Integer = -1
    End Class

    Private Shared Function ResolveColumns(header As String()) As CsvCols
        Dim c As New CsvCols()
        For i As Integer = 0 To header.Length - 1
            Select Case header(i).Trim()
                Case "Timestamp"      : c.Timestamp = i
                Case "Price"          : c.Price = i
                Case "Verdict"        : c.Verdict = i
                Case "ATR"            : c.Atr = i
                Case "SwingStopLong"  : c.StopLong = i
                Case "SwingStopShort" : c.StopShort = i
            End Select
        Next
        Return c
    End Function

    Private Shared Function TryComputeSpan(lines As String(),
                                            cols As CsvCols,
                                            rounds As List(Of RoundSummary),
                                            ByRef startUtc As DateTime,
                                            ByRef endUtc As DateTime) As Boolean
        If cols.Timestamp < 0 OrElse lines.Length < 2 Then Return False
        startUtc = DateTime.MaxValue
        endUtc   = DateTime.MinValue
        For Each round In rounds
            Dim sIdx As Integer = Math.Max(1, round.WindowStartRow + 1)
            Dim eIdx As Integer = Math.Min(lines.Length - 1, round.WindowEndRow + 1)
            For r As Integer = sIdx To eIdx
                Dim parts As String() = lines(r).Split(","c)
                If parts.Length <= cols.Timestamp Then Continue For
                Dim ts As DateTime
                If DateTime.TryParseExact(parts(cols.Timestamp).Trim(),
                                          "yyyy-MM-dd HH:mm:ss",
                                          CultureInfo.InvariantCulture,
                                          DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                          ts) Then
                    If ts < startUtc Then startUtc = ts
                    If ts > endUtc Then endUtc = ts
                End If
            Next
        Next
        Return startUtc <> DateTime.MaxValue AndAlso endUtc <> DateTime.MinValue
    End Function

    Private Shared Function RenderRound(round As RoundSummary,
                                         lines As String(),
                                         cols As CsvCols,
                                         ohlcMap As Dictionary(Of DateTime, OhlcBar)) As String
        Dim sb As New StringBuilder()
        Dim displayIso As String = round.RoundIso
        Try
            Dim parsed As DateTime
            If DateTime.TryParse(round.RoundIso, Nothing,
                                  DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                  parsed) Then
                displayIso = parsed.ToString("yyyy-MM-dd HH:mm:ss") & " UTC"
            End If
        Catch
        End Try

        sb.AppendLine(String.Format("  --- Round {0}   ({1})  --------------------------------",
                                     displayIso, round.Outcome))

        sb.AppendLine(String.Format("    Aggregate failure rate: {0:F1}%",
                                     round.AggregateFailureRatePct))
        sb.AppendLine(String.Format("    Window: rows {0}..{1}",
                                     round.WindowStartRow, round.WindowEndRow))

        If round.Outcome = "APPLIED" OrElse round.Outcome = "PROPOSED" OrElse
           round.Outcome = "DRY_RUN_WRITTEN" Then
            If Not String.IsNullOrEmpty(round.DiffSummary) Then
                sb.AppendLine("    Settings change: " & round.DiffSummary)
            End If
            If Not String.IsNullOrEmpty(round.Reasoning) Then
                Dim excerpt As String = round.Reasoning
                If excerpt.Length > 240 Then excerpt = excerpt.Substring(0, 240) & "..."
                sb.AppendLine("    Reasoning excerpt: " & excerpt)
            End If
        End If

        ' Verdict accuracy panel — requires OHLC + CSV cols
        If ohlcMap IsNot Nothing AndAlso cols.Timestamp >= 0 AndAlso cols.Verdict >= 0 AndAlso
           cols.Price >= 0 AndAlso cols.Atr >= 0 Then
            sb.AppendLine()
            sb.AppendLine("    Verdict accuracy (this round's window):")
            For Each line In RenderAccuracy(round, lines, cols, ohlcMap)
                sb.AppendLine("      " & line)
            Next
        End If

        Return sb.ToString()
    End Function

    ' Returns the per-tier accuracy lines for a single round.
    Private Shared Function RenderAccuracy(round As RoundSummary,
                                            lines As String(),
                                            cols As CsvCols,
                                            ohlcMap As Dictionary(Of DateTime, OhlcBar)) As List(Of String)
        Dim out As New List(Of String)()
        Dim tallies As New Dictionary(Of String, Integer())()  ' tier -> {correct, wrong}
        Dim noTradeCount As Integer = 0

        Dim sIdx As Integer = Math.Max(1, round.WindowStartRow + 1)
        Dim eIdx As Integer = Math.Min(lines.Length - 1, round.WindowEndRow + 1)
        For r As Integer = sIdx To eIdx
            Dim parts As String() = lines(r).Split(","c)
            If parts.Length <= cols.Verdict Then Continue For

            Dim verdict As String = parts(cols.Verdict).Trim().ToUpper()
            If verdict = "NO TRADE" Then
                noTradeCount += 1
                Continue For
            End If
            If Not IsDirectional(verdict) Then Continue For

            Dim ts As DateTime
            If Not DateTime.TryParseExact(parts(cols.Timestamp).Trim(),
                                          "yyyy-MM-dd HH:mm:ss",
                                          CultureInfo.InvariantCulture,
                                          DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                          ts) Then Continue For

            Dim entry, atr, stopL, stopS As Double
            If Not Double.TryParse(parts(cols.Price).Trim(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, entry) Then Continue For
            If Not Double.TryParse(parts(cols.Atr).Trim(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, atr) OrElse atr <= 0 Then Continue For
            If cols.StopLong >= 0 AndAlso cols.StopLong < parts.Length Then
                Double.TryParse(parts(cols.StopLong).Trim(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, stopL)
            End If
            If cols.StopShort >= 0 AndAlso cols.StopShort < parts.Length Then
                Double.TryParse(parts(cols.StopShort).Trim(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, stopS)
            End If

            Dim isLong As Boolean = verdict.EndsWith("LONG")
            Dim favBar As Double = If(isLong, entry + FavAtrThreshold * atr,
                                                 entry - FavAtrThreshold * atr)
            Dim advBar As Double
            If isLong Then
                advBar = If(stopL > 0, stopL, entry - 1.2 * atr)
            Else
                advBar = If(stopS > 0, stopS, entry + 1.2 * atr)
            End If

            Dim bars As New List(Of OhlcBar)()
            Dim rowMin As New DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, 0, DateTimeKind.Utc)
            For closeMin As Integer = 3 To FavWindowMin
                Dim key As DateTime = rowMin.AddMinutes(closeMin)
                Dim bar As OhlcBar = Nothing
                If ohlcMap.TryGetValue(key, bar) Then bars.Add(bar)
            Next
            If bars.Count = 0 Then Continue For

            Dim outcome As String = FailureRateMatrix.WalkBars(bars, favBar, advBar, isLong)
            Dim ok As Boolean = (outcome = "SUCCESS")

            Dim tier As String = TierKey(verdict)
            Dim tally As Integer() = Nothing
            If Not tallies.TryGetValue(tier, tally) Then
                tally = New Integer() {0, 0}
                tallies(tier) = tally
            End If
            If ok Then tally(0) += 1 Else tally(1) += 1
        Next

        Dim displayOrder As String() = {
            "STRONG_LONG", "LONG", "WEAK_LONG",
            "STRONG_SHORT", "SHORT", "WEAK_SHORT"}
        For Each tier In displayOrder
            Dim tally As Integer() = Nothing
            If Not tallies.TryGetValue(tier, tally) Then Continue For
            Dim correct As Integer = tally(0)
            Dim wrong   As Integer = tally(1)
            Dim total   As Integer = correct + wrong
            If total = 0 Then Continue For
            Dim pct As Integer = CInt(Math.Round(CDbl(correct) / total * 100.0))
            out.Add(String.Format("{0,-13} / Confidence: {1,-7} : {2,3} correct / {3,3} wrong  ({4}% success)",
                                  tier, ConfidenceFor(tier), correct, wrong, pct))
        Next
        out.Add(String.Format("{0,-13} / Confidence: {1,-7} : {2,3} rows  (informational only)",
                              "NO_TRADE", "N/A", noTradeCount))
        Return out
    End Function

    Private Shared Function IsDirectional(verdict As String) As Boolean
        Select Case verdict
            Case "STRONG LONG", "LONG", "WEAK LONG",
                 "STRONG SHORT", "SHORT", "WEAK SHORT"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function TierKey(verdict As String) As String
        Select Case verdict
            Case "STRONG LONG"  : Return "STRONG_LONG"
            Case "LONG"         : Return "LONG"
            Case "WEAK LONG"    : Return "WEAK_LONG"
            Case "STRONG SHORT" : Return "STRONG_SHORT"
            Case "SHORT"        : Return "SHORT"
            Case "WEAK SHORT"   : Return "WEAK_SHORT"
            Case Else           : Return verdict
        End Select
    End Function

    Private Shared Function ConfidenceFor(tier As String) As String
        Select Case tier
            Case "STRONG_LONG", "STRONG_SHORT" : Return "HIGH"
            Case "LONG", "SHORT"               : Return "MEDIUM"
            Case "WEAK_LONG", "WEAK_SHORT"     : Return "LOW"
            Case Else                          : Return "N/A"
        End Select
    End Function

    Private Shared Function LookupBucketForActiveSnapshot(filename As String,
                                                           manifestPath As String) As String
        For Each row In SnapshotManager.LoadAll(manifestPath)
            If row.Filename = filename Then Return row.ConditionBucket
        Next
        Return ""
    End Function

End Class
