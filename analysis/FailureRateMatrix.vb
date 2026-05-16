' analysis/FailureRateMatrix.vb
' Computes per-tier x window x ATR-threshold failure rates with 95% Wilson CI.
' Verdict tiers: STRONG_LONG, STRONG_SHORT, MEDIUM_LONG, MEDIUM_SHORT.
' NO_TRADE and WEAK_* are excluded from the denominator.
' Rows where ATR <= 0 are excluded entirely (degenerate barriers).
'
' Failure definition v2 (failure-definition-v2-proposal.md):
'   Barrier-hit with adverse stop. Walk 1m OHLC bars in chronological order:
'     favHit AND advHit in same bar → FAILURE (conservative ambiguous-bar rule)
'     favHit first → SUCCESS
'     advHit first → FAILURE
'   Window expires without any hit → FAILURE
'
' Adverse barrier: structural stop (SwingStopLong/Short) where available;
'   ATR-multiple fallback (AdverseFallbackAtrMultiplier) when not.
' Favourable barrier: entry ± AtrThreshold × ATR (per cell).
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Math

Public Class FailureRateMatrix

    ' Maps engine verdict strings → canonical tier names.
    Private Shared Function ToTier(verdict As String) As String
        Select Case verdict.Trim().ToUpper()
            Case "STRONG LONG"  : Return "STRONG_LONG"
            Case "LONG"         : Return "MEDIUM_LONG"
            Case "STRONG SHORT" : Return "STRONG_SHORT"
            Case "SHORT"        : Return "MEDIUM_SHORT"
            Case Else           : Return ""   ' WEAK / NO TRADE — excluded
        End Select
    End Function

    Private Shared Function ThresholdsFor(tier As String) As Double()
        If tier.StartsWith("STRONG") Then Return AnalysisConstants.StrongAtrThresholds
        Return AnalysisConstants.MediumAtrThresholds
    End Function

    Private Shared Function IsLong(tier As String) As Boolean
        Return tier.EndsWith("LONG")
    End Function

    ' Walk eligible bars chronologically and classify outcome.
    ' Returns "SUCCESS", "ADVERSE_HIT", "AMBIGUOUS", or "WINDOW_EXPIRED".
    ' AMBIGUOUS (both barriers touched in same bar) → treated as FAILURE by caller
    ' per spec §2a conservative-bias rule.
    Public Shared Function WalkBars(bars As List(Of OhlcBar),
                                    favBar As Double,
                                    advBar As Double,
                                    isLong As Boolean) As String
        For Each bar In bars
            Dim favHit As Boolean = If(isLong, bar.High >= favBar, bar.Low <= favBar)
            Dim advHit As Boolean = If(isLong, bar.Low <= advBar,  bar.High >= advBar)

            If favHit AndAlso advHit Then Return "AMBIGUOUS"   ' conservative = failure
            If favHit Then Return "SUCCESS"
            If advHit Then Return "ADVERSE_HIT"
        Next
        Return "WINDOW_EXPIRED"
    End Function

    ' Walk eligible bars and return True iff the favourable barrier was touched
    ' at any point within the window, regardless of whether the adverse barrier
    ' hit first. Used by the target-hit metric — decouples "direction was right"
    ' from "stop placement survived noise".
    Public Shared Function TargetHitWalk(bars   As List(Of OhlcBar),
                                         favBar As Double,
                                         isLong As Boolean) As Boolean
        For Each b In bars
            If isLong Then
                If b.High >= favBar Then Return True
            Else
                If b.Low <= favBar Then Return True
            End If
        Next
        Return False
    End Function

    ' Compute the full failure-rate matrix.
    ' ByRef counters are informational: atrInvalidExcluded counts rows excluded because
    ' ATR <= 0; structuralStopRows / atrFallbackRows count rows where the adverse barrier
    ' was structural vs ATR-multiple (counted once per row regardless of window count).
    Public Shared Function Compute(rows As List(Of CsvRow),
                                   ByRef atrInvalidExcluded As Integer,
                                   ByRef structuralStopRows  As Integer,
                                   ByRef atrFallbackRows     As Integer) As List(Of FailureCellResult)

        atrInvalidExcluded = 0
        structuralStopRows  = 0
        atrFallbackRows     = 0

        ' counts(tier)(window)(threshold) = (N, Failures, Successes, AdverseHits, Expiries, Ambiguous)
        Dim counts As New Dictionary(Of String, Dictionary(Of Integer, Dictionary(Of Double,
            (N As Integer, F As Integer, Suc As Integer, Adv As Integer, Exp As Integer, Amb As Integer))))()

        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            counts(tier) = New Dictionary(Of Integer, Dictionary(Of Double,
                (Integer, Integer, Integer, Integer, Integer, Integer)))()
            For Each w In AnalysisConstants.HoldWindowsMinutes
                counts(tier)(w) = New Dictionary(Of Double,
                    (Integer, Integer, Integer, Integer, Integer, Integer))()
                For Each thr In ThresholdsFor(tier)
                    counts(tier)(w)(thr) = (0, 0, 0, 0, 0, 0)
                Next
            Next
        Next

        For Each row In rows
            ' Exclude rows with degenerate ATR (barriers would collapse to entry price).
            If row.ATR <= 0 Then
                atrInvalidExcluded += 1
                Continue For
            End If

            Dim tier As String = ToTier(row.Verdict)
            If tier = "" Then Continue For

            Dim isLongRow As Boolean = IsLong(tier)
            Dim entry     As Double  = row.Price
            Dim atr       As Double  = row.ATR

            ' Adverse barrier: structural stop first, ATR-multiple fallback.
            Dim advBar As Double
            If isLongRow Then
                If row.SwingStopLong > 0 Then
                    advBar = row.SwingStopLong
                    structuralStopRows += 1
                Else
                    advBar = entry - AnalysisConstants.AdverseFallbackAtrMultiplier * atr
                    atrFallbackRows += 1
                End If
            Else
                If row.SwingStopShort > 0 Then
                    advBar = row.SwingStopShort
                    structuralStopRows += 1
                Else
                    advBar = entry + AnalysisConstants.AdverseFallbackAtrMultiplier * atr
                    atrFallbackRows += 1
                End If
            End If

            For Each w In AnalysisConstants.HoldWindowsMinutes
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For   ' no data for this window — exclude from denominator
                End If

                For Each thr In ThresholdsFor(tier)
                    ' Favourable barrier per cell (varies by threshold).
                    Dim favBar As Double = If(isLongRow, entry + thr * atr, entry - thr * atr)

                    Dim outcome As String = WalkBars(bars, favBar, advBar, isLongRow)
                    Dim failed  As Boolean = (outcome <> "SUCCESS")

                    Dim cur = counts(tier)(w)(thr)
                    Dim newSuc = cur.Suc + If(outcome = "SUCCESS",      1, 0)
                    Dim newAdv = cur.Adv + If(outcome = "ADVERSE_HIT",  1, 0)
                    Dim newExp = cur.Exp + If(outcome = "WINDOW_EXPIRED", 1, 0)
                    Dim newAmb = cur.Amb + If(outcome = "AMBIGUOUS",    1, 0)
                    counts(tier)(w)(thr) = (cur.N + 1,
                                            cur.F + If(failed, 1, 0),
                                            newSuc, newAdv, newExp, newAmb)
                Next
            Next
        Next

        ' Build result list and mark recommended cell per tier.
        Dim results As New List(Of FailureCellResult)()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim bestCiWidth As Double  = Double.MaxValue
            Dim bestIdx     As Integer = -1
            Dim tierResults As New List(Of FailureCellResult)()
            For Each w In AnalysisConstants.HoldWindowsMinutes
                For Each thr In ThresholdsFor(tier)
                    Dim c = counts(tier)(w)(thr)
                    Dim cell As New FailureCellResult() With {
                        .VerdictTier        = tier,
                        .WindowMin          = w,
                        .AtrThreshold       = thr,
                        .SampleSize         = c.N,
                        .Failures           = c.F,
                        .Successes          = c.Suc,
                        .AdverseHitFails    = c.Adv,
                        .WindowExpiryFails  = c.Exp,
                        .AmbiguousFails     = c.Amb
                    }
                    If c.N > 0 Then
                        cell.FailureRate = CDbl(c.F) / c.N
                        WilsonCI(c.F, c.N, cell.CiLow, cell.CiHigh)
                        cell.CiWidth = cell.CiHigh - cell.CiLow
                    End If
                    tierResults.Add(cell)
                    If c.N >= AnalysisConstants.MinSamplesPerCell AndAlso cell.CiWidth < bestCiWidth Then
                        bestCiWidth = cell.CiWidth
                        bestIdx = tierResults.Count - 1
                    End If
                Next
            Next
            If bestIdx >= 0 Then tierResults(bestIdx).IsRecommended = True
            results.AddRange(tierResults)
        Next

        Return results
    End Function

    ' 95% Wilson score confidence interval (zsq = 1.96^2 = 3.8416).
    Public Shared Sub WilsonCI(failures As Integer, n As Integer,
                                ByRef ciLow As Double, ByRef ciHigh As Double)
        Const Zsq As Double = 3.8416
        If n = 0 Then ciLow = 0 : ciHigh = 1 : Return
        Dim p      As Double = CDbl(failures) / n
        Dim denom  As Double = 1.0 + Zsq / n
        Dim centre As Double = (p + Zsq / (2 * n)) / denom
        Dim margin As Double = Sqrt(p * (1 - p) / n + Zsq / (4.0 * n * n)) * 1.96 / denom
        ciLow  = Max(0.0, centre - margin)
        ciHigh = Min(1.0, centre + margin)
    End Sub

    ' Append one picked-cell entry to analysis/picked_cell_history.csv.
    ' v2 schema: first line is "# schema=v2 (barrier-hit with adverse stop)".
    ' If an existing file does NOT start with that marker it is a v1 file —
    ' rotate it to .v1.bak before writing. Idempotent on repeated calls.
    Public Shared Sub AppendPickedCell(csvPath As String,
                                       tier As String, windowMin As Integer,
                                       atrThreshold As Double,
                                       failureRate As Double, sampleSize As Integer,
                                       ciLow As Double, ciHigh As Double)
        Try
            RotateV1HistoryIfNeeded(csvPath)
            Dim writeHeader As Boolean = Not IO.File.Exists(csvPath)
            Using sw As New IO.StreamWriter(csvPath, append:=True)
                If writeHeader Then
                    sw.WriteLine("# schema=v2 (barrier-hit with adverse stop)")
                    sw.WriteLine("Timestamp,Tier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh")
                End If
                sw.WriteLine(String.Join(",",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    tier,
                    windowMin.ToString(),
                    atrThreshold.ToString("F2"),
                    failureRate.ToString("F6"),
                    sampleSize.ToString(),
                    ciLow.ToString("F6"),
                    ciHigh.ToString("F6")))
            End Using
        Catch
            ' Best-effort write — do not abort the auto-tweaker run on CSV I/O failure.
        End Try
    End Sub

    ' Rename an existing v1 picked-cell history file to .v1.bak.
    ' Detection: first line of the file does NOT start with "# schema=v2".
    Private Shared Sub RotateV1HistoryIfNeeded(csvPath As String)
        If Not IO.File.Exists(csvPath) Then Return
        Dim firstLine As String = ""
        Try
            Using sr As New IO.StreamReader(csvPath)
                firstLine = sr.ReadLine()
            End Using
        Catch
            Return
        End Try
        If firstLine IsNot Nothing AndAlso firstLine.StartsWith("# schema=v2") Then Return
        ' File is v1 — rename.
        Dim bakPath As String = csvPath & ".v1.bak"
        If IO.File.Exists(bakPath) Then
            Dim ts As String = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            bakPath = csvPath & ".v1." & ts & ".bak"
        End If
        Try
            IO.File.Move(csvPath, bakPath)
            Console.WriteLine("[FailureRateMatrix] Rotated v1 picked-cell history → " & bakPath)
        Catch ex As Exception
            Console.WriteLine("[FailureRateMatrix] Could not rotate v1 history: " & ex.Message)
        End Try
    End Sub

End Class
