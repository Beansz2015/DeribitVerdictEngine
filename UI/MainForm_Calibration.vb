' UI/MainForm_Calibration.vb
' Partial class: calibration-readiness report builder.
'
' P5a migration target: BuildCalibrationReport + Flag moved here from
' MainForm_Render_Header.vb so that Header.vb deletes cleanly in P5b.
' Pure markdown-string producer; no RTF dependency. Called by
' lnkCalibCheck_LinkClicked (in MainForm_Layout.vb after the P5a commit-2
' migration to AnalysisReportForm, or transitionally via txtOutput before
' that commit).

Imports System.IO

Partial Public Class MainForm

    Private Function BuildCalibrationReport() As String
        Dim path As String = AnalysisLogger.GetLogPath()
        Dim sb As New System.Text.StringBuilder()

        sb.AppendLine("===========================================================")
        sb.AppendLine("  CALIBRATION READINESS REPORT")
        sb.AppendLine("  " & DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") & " UTC+8")
        sb.AppendLine("===========================================================")
        sb.AppendLine()

        If Not File.Exists(path) Then
            sb.AppendLine("  No log file found. Run at least one analysis first.")
            Return sb.ToString()
        End If

        Dim lines = File.ReadAllLines(path)
        If lines.Length <= 1 Then
            sb.AppendLine("  Log file is empty. Run more analyses to accumulate data.")
            Return sb.ToString()
        End If

        Dim header = lines(0).Split(","c)
        Dim colIdx As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To header.Length - 1
            colIdx(header(i).Trim()) = i
        Next

        Dim totalRows      As Integer = 0
        Dim liqEvents      As Integer = 0
        Dim ofiValues      As New List(Of Double)()
        Dim volRatioValues As New List(Of Double)()
        Dim sessionDates   As New HashSet(Of String)()
        Dim regimeCounts   As New Dictionary(Of String, Integer) From {
            {"TRENDING_UP", 0}, {"TRENDING_DOWN", 0},
            {"RANGE_BOUND", 0}, {"TRANSITIONAL", 0}
        }
        ' "ALIGNED" added 2026-05-17 (audit cleanup pass): post-v30 NO TRADE rows
        ' carry VerdictContext="ALIGNED" instead of "CONFIRMED". Without this key,
        ' the ContainsKey guard silently dropped those rows from the distribution.
        Dim contextCounts As New Dictionary(Of String, Integer) From {
            {"CONFIRMED", 0}, {"ALIGNED", 0},
            {"FLOW_UNCONFIRMED", 0}, {"MOMENTUM_FADING", 0},
            {"STRUCTURALLY_WEAK", 0}
        }
        Dim fundingMomCounts As New Dictionary(Of String, Integer) From {
            {"RISING", 0}, {"FALLING", 0}, {"FLAT", 0}
        }
        Dim oiCvdCounts As New Dictionary(Of String, Integer) From {
            {"NONE", 0}, {"CONFIRMED_LONG", 0}, {"CONFIRMED_SHORT", 0},
            {"CONFLICT_LONG", 0}, {"CONFLICT_SHORT", 0}
        }
        ' v0.4 distribution buckets
        Dim spreadBuckets As New Dictionary(Of String, Integer) From {
            {"<=2 bps", 0}, {"2-5 bps", 0}, {"5-10 bps", 0}, {">10 bps", 0}
        }
        Dim ofiMomCounts As New Dictionary(Of String, Integer) From {
            {"RISING", 0}, {"FALLING", 0}, {"FLAT", 0}
        }
        Dim capReasonCounts As New Dictionary(Of String, Integer) From {
            {"swing", 0}, {"hvn", 0}, {"poc", 0}, {"none", 0}
        }
        ' v0.4.1 distribution buckets
        Dim trendStructCounts As New Dictionary(Of String, Integer) From {
            {"UPTREND", 0}, {"DOWNTREND", 0}, {"EXPANSION", 0},
            {"CONTRACTION", 0}, {"UNDEFINED", 0}
        }
        Dim bvpRatios          As New List(Of Double)()
        Dim bvpIsMostRecentCount As Integer = 0
        Dim bvpEligibleCount     As Integer = 0

        For i = 1 To lines.Length - 1
            Dim parts = lines(i).Split(","c)
            If parts.Length < header.Length Then Continue For
            totalRows += 1

            If colIdx.ContainsKey("Timestamp") Then
                Dim ts = parts(colIdx("Timestamp")).Trim()
                If ts.Length >= 10 Then sessionDates.Add(ts.Substring(0, 10))
            End If

            If colIdx.ContainsKey("Regime") Then
                Dim reg = parts(colIdx("Regime")).Trim().ToUpper()
                If regimeCounts.ContainsKey(reg) Then regimeCounts(reg) += 1
            End If

            If colIdx.ContainsKey("LiqSignal") Then
                Dim liq = parts(colIdx("LiqSignal")).Trim().ToUpper()
                If liq <> "NONE" Then liqEvents += 1
            End If

            If colIdx.ContainsKey("OFIRatio") Then
                Dim v As Double
                If Double.TryParse(parts(colIdx("OFIRatio")).Trim(), v) Then ofiValues.Add(v)
            End If

            If colIdx.ContainsKey("VolumeRatio") Then
                Dim v As Double
                If Double.TryParse(parts(colIdx("VolumeRatio")).Trim(), v) Then volRatioValues.Add(v)
            End If

            If colIdx.ContainsKey("VerdictContext") Then
                Dim ctx = parts(colIdx("VerdictContext")).Trim().ToUpper()
                If contextCounts.ContainsKey(ctx) Then contextCounts(ctx) += 1
            End If
            If colIdx.ContainsKey("FundingMomentum") Then
                Dim mom = parts(colIdx("FundingMomentum")).Trim().ToUpper()
                If fundingMomCounts.ContainsKey(mom) Then fundingMomCounts(mom) += 1
            End If
            If colIdx.ContainsKey("OiCvdOutcome") Then
                Dim oicvd = parts(colIdx("OiCvdOutcome")).Trim().ToUpper()
                If oiCvdCounts.ContainsKey(oicvd) Then oiCvdCounts(oicvd) += 1
            End If
            If colIdx.ContainsKey("SpreadBps") Then
                Dim sv As Double
                If Double.TryParse(parts(colIdx("SpreadBps")).Trim(), sv) Then
                    If sv <= 2.0 Then
                        spreadBuckets("<=2 bps") += 1
                    ElseIf sv <= 5.0 Then
                        spreadBuckets("2-5 bps") += 1
                    ElseIf sv <= 10.0 Then
                        spreadBuckets("5-10 bps") += 1
                    Else
                        spreadBuckets(">10 bps") += 1
                    End If
                End If
            End If
            If colIdx.ContainsKey("OFIMomentum") Then
                Dim om = parts(colIdx("OFIMomentum")).Trim().ToUpper()
                If ofiMomCounts.ContainsKey(om) Then ofiMomCounts(om) += 1
            End If
            If colIdx.ContainsKey("TargetCapReason") Then
                ' Normalise both the v0.4 canonical buckets ("swing"/"hvn"/"poc"/"none")
                ' AND the legacy rich-string form ("CAPPED @ 72480.0 (SWING_HIGH_5M)")
                ' so historical rows logged before the canonical fix are still
                ' counted correctly.
                Dim cr = AnalysisLogger.NormaliseCapReason(parts(colIdx("TargetCapReason")))
                If capReasonCounts.ContainsKey(cr) Then
                    capReasonCounts(cr) += 1
                Else
                    capReasonCounts("none") += 1
                End If
            End If
            ' v0.4.1: Trend Structure distribution
            If colIdx.ContainsKey("TrendStructure5m") Then
                Dim ts = parts(colIdx("TrendStructure5m")).Trim().ToUpper()
                If trendStructCounts.ContainsKey(ts) Then
                    trendStructCounts(ts) += 1
                Else
                    trendStructCounts("UNDEFINED") += 1
                End If
            End If
            ' v0.4.1: Best Volume Pivot distribution
            If colIdx.ContainsKey("BestPivotByVolume5m") AndAlso
               colIdx.ContainsKey("BestPivotVolumeRatio5m") Then
                Dim bvp As Double, bvpRatio As Double
                If Double.TryParse(parts(colIdx("BestPivotByVolume5m")).Trim(), bvp) AndAlso
                   Double.TryParse(parts(colIdx("BestPivotVolumeRatio5m")).Trim(), bvpRatio) AndAlso
                   bvp > 0 AndAlso bvpRatio > 0 Then
                    bvpRatios.Add(bvpRatio)
                    bvpEligibleCount += 1
                    ' "best is also most-recent" if BestPivot price matches LastSwingHigh5m or LastSwingLow5m
                    Dim isMostRecent As Boolean = False
                    If colIdx.ContainsKey("LastSwingHigh5m") AndAlso colIdx.ContainsKey("LastSwingLow5m") Then
                        Dim sh As Double, sl As Double
                        If Double.TryParse(parts(colIdx("LastSwingHigh5m")).Trim(), sh) AndAlso
                           Double.TryParse(parts(colIdx("LastSwingLow5m")).Trim(), sl) Then
                            isMostRecent = (Math.Abs(bvp - sh) < 1.0) OrElse (Math.Abs(bvp - sl) < 1.0)
                        End If
                    End If
                    If isMostRecent Then bvpIsMostRecentCount += 1
                End If
            End If
        Next

        Const MIN_TOTAL           As Integer = 300
        Const MIN_PER_REGIME      As Integer = 50
        Const MIN_REGIMES_COVERED As Integer = 3
        Const MIN_SESSIONS        As Integer = 3
        ' Liquidation events are tracked informationally but no longer gate the
        ' READY verdict. They are too rare to require for calibration: at typical
        ' BTC-PERPETUAL conditions on Deribit, multi-day windows can pass without
        ' a single liquidation crossing CalcLiquidations's threshold. When liqs
        ' do happen during auto-run the rows are still logged for offline review.

        Dim regimesCovered As Integer = regimeCounts.Values.ToList().Where(Function(c) c >= MIN_PER_REGIME).Count()
        Dim okTotal    = totalRows >= MIN_TOTAL
        Dim okRegimes  = regimesCovered >= MIN_REGIMES_COVERED
        Dim okSessions = sessionDates.Count >= MIN_SESSIONS
        Dim overallReady = okTotal AndAlso okRegimes AndAlso okSessions

        sb.AppendLine("SUMMARY")
        sb.AppendLine("  Total rows logged : " & totalRows & "  (need " & MIN_TOTAL & ")  " & Flag(okTotal))
        sb.AppendLine("  Sessions (days)   : " & sessionDates.Count & "  (need " & MIN_SESSIONS & ")  " & Flag(okSessions))
        sb.AppendLine("  Liq events logged : " & liqEvents & "  (informational; not a ready gate)")
        sb.AppendLine()
        sb.AppendLine("REGIME DISTRIBUTION  (need >= " & MIN_PER_REGIME & " rows each, " & MIN_REGIMES_COVERED & "+ regimes)")
        For Each kvp In regimeCounts
            Dim ok = kvp.Value >= MIN_PER_REGIME
            sb.AppendLine("  " & kvp.Key.PadRight(16) & " : " & kvp.Value.ToString().PadLeft(5) & " rows   " & Flag(ok))
        Next
        sb.AppendLine("  Regimes ready     : " & regimesCovered & "/" & MIN_REGIMES_COVERED & "  " & Flag(okRegimes))
        sb.AppendLine()
        sb.AppendLine("INDICATOR VARIANCE")
        If ofiValues.Count > 10 Then
            Dim ofiMin   = ofiValues.Min()
            Dim ofiMax   = ofiValues.Max()
            Dim ofiRange = ofiMax - ofiMin
            Dim ofiOk    = ofiRange > 2.0
            sb.AppendLine("  OFI Ratio range   : " & ofiMin.ToString("F2") & " to " & ofiMax.ToString("F2") &
                          "  (spread: " & ofiRange.ToString("F2") & ")  " & Flag(ofiOk))
        Else
            sb.AppendLine("  OFI Ratio         : insufficient data")
        End If
        If volRatioValues.Count > 10 Then
            Dim vMin   = volRatioValues.Min()
            Dim vMax   = volRatioValues.Max()
            Dim vRange = vMax - vMin
            Dim vOk    = vRange > 1.0
            sb.AppendLine("  Volume Ratio range: " & vMin.ToString("F2") & " to " & vMax.ToString("F2") &
                          "  (spread: " & vRange.ToString("F2") & ")  " & Flag(vOk))
        Else
            sb.AppendLine("  Volume Ratio      : insufficient data")
        End If
        sb.AppendLine()
        sb.AppendLine("VERDICT CONTEXT DISTRIBUTION")
        For Each kvp In contextCounts
            sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
        Next
        sb.AppendLine()
        sb.AppendLine("FUNDING MOMENTUM DISTRIBUTION")
        For Each kvp In fundingMomCounts
            sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
        Next
        sb.AppendLine()
        sb.AppendLine("OI x CVD PASS 2b OUTCOMES")
        For Each kvp In oiCvdCounts
            sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
        Next
        sb.AppendLine()
        If colIdx.ContainsKey("SpreadBps") Then
            sb.AppendLine("SPREAD DISTRIBUTION  (bps)")
            For Each kvp In spreadBuckets
                sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
            Next
            sb.AppendLine()
        End If
        If colIdx.ContainsKey("OFIMomentum") Then
            sb.AppendLine("OFI MOMENTUM DISTRIBUTION")
            For Each kvp In ofiMomCounts
                sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
            Next
            sb.AppendLine()
        End If
        If colIdx.ContainsKey("TargetCapReason") Then
            sb.AppendLine("TARGET CAP REASON DISTRIBUTION")
            For Each kvp In capReasonCounts
                sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
            Next
            sb.AppendLine()
        End If
        If colIdx.ContainsKey("TrendStructure5m") Then
            sb.AppendLine("TREND STRUCTURE DISTRIBUTION")
            For Each kvp In trendStructCounts
                sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
            Next
            sb.AppendLine()
        End If
        If colIdx.ContainsKey("BestPivotByVolume5m") AndAlso bvpEligibleCount > 0 Then
            sb.AppendLine("BEST VOLUME PIVOT DISTRIBUTION  (rows with >= 2 confirmed 5m pivots)")
            Dim sortedRatios = bvpRatios.OrderBy(Function(x) x).ToList()
            Dim avgRatio     As Double = sortedRatios.Sum() / sortedRatios.Count
            Dim p75          As Double = sortedRatios(CInt(Math.Floor(sortedRatios.Count * 0.75)))
            Dim p90          As Double = sortedRatios(CInt(Math.Floor(sortedRatios.Count * 0.90)))
            Dim mostRecentPct As Double = If(bvpEligibleCount > 0, bvpIsMostRecentCount / CDbl(bvpEligibleCount) * 100, 0)
            sb.AppendLine("  Eligible rows         : " & bvpEligibleCount.ToString().PadLeft(5))
            sb.AppendLine("  Average ratio         : " & avgRatio.ToString("F2"))
            sb.AppendLine("  75th percentile ratio : " & p75.ToString("F2"))
            sb.AppendLine("  90th percentile ratio : " & p90.ToString("F2"))
            sb.AppendLine(String.Format("  Best = most-recent    : {0} ({1:F1}%){2}",
                bvpIsMostRecentCount, mostRecentPct,
                If(mostRecentPct < 50, "  [consider v2 cap promotion]", "")))
            sb.AppendLine()
        ElseIf colIdx.ContainsKey("BestPivotByVolume5m") Then
            sb.AppendLine("BEST VOLUME PIVOT DISTRIBUTION  (no eligible rows yet)")
            sb.AppendLine()
        End If
        sb.AppendLine("===========================================================")
        sb.AppendLine(If(overallReady,
                         "  VERDICT: READY FOR RECALIBRATION",
                         "  VERDICT: NOT YET READY -- see flags above"))
        sb.AppendLine("===========================================================")
        Return sb.ToString()
    End Function

    Private Shared Function Flag(ok As Boolean) As String
        Return If(ok, "[OK]", "[--]")
    End Function

End Class
