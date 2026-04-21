' UI/MainForm_Render_Header.vb
' Partial class: top render block.
' Contains: RTF helpers, log/calibration helpers, RenderOutput header section
'   (VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME / LAST PRICE /
'    HOLD STATUS / ATR ENTRY LEVELS / KELLY SIZING)
' Split from MainForm_Render.vb for maintainability.

Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Partial Public Class MainForm

    Private Sub UpdateLogInfo()
        Dim rows As Integer = AnalysisLogger.GetRowCount()
        Dim path As String  = AnalysisLogger.GetLogPath()
        lblLogInfo.Text = String.Format("Log: {0} rows  |  {1}", rows, path)
    End Sub

    Private Sub lnkResetLog_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkResetLog.LinkClicked
        Dim result = MessageBox.Show(
            "Reset the analysis log? This will delete all logged rows and cannot be undone." &
            Environment.NewLine & Environment.NewLine &
            "File: " & AnalysisLogger.GetLogPath(),
            "Reset Log",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning)
        If result = DialogResult.Yes Then
            AnalysisLogger.ResetLog()
            UpdateLogInfo()
        End If
    End Sub

    Private Sub lnkCalibCheck_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkCalibCheck.LinkClicked
        txtOutput.Clear()
        AppendRtf(txtOutput, BuildCalibrationReport(), C_VALUE)
    End Sub

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
        Next

        Const MIN_TOTAL           As Integer = 300
        Const MIN_PER_REGIME      As Integer = 50
        Const MIN_REGIMES_COVERED As Integer = 3
        Const MIN_LIQ_EVENTS      As Integer = 2
        Const MIN_SESSIONS        As Integer = 3

        Dim regimesCovered As Integer = regimeCounts.Values.ToList().Where(Function(c) c >= MIN_PER_REGIME).Count()
        Dim okTotal    = totalRows >= MIN_TOTAL
        Dim okRegimes  = regimesCovered >= MIN_REGIMES_COVERED
        Dim okLiq      = liqEvents >= MIN_LIQ_EVENTS
        Dim okSessions = sessionDates.Count >= MIN_SESSIONS
        Dim overallReady = okTotal AndAlso okRegimes AndAlso okLiq AndAlso okSessions

        sb.AppendLine("SUMMARY")
        sb.AppendLine("  Total rows logged : " & totalRows & "  (need " & MIN_TOTAL & ")  " & Flag(okTotal))
        sb.AppendLine("  Sessions (days)   : " & sessionDates.Count & "  (need " & MIN_SESSIONS & ")  " & Flag(okSessions))
        sb.AppendLine("  Liq events logged : " & liqEvents & "  (need " & MIN_LIQ_EVENTS & ")  " & Flag(okLiq))
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

    Private Shared Sub AppendRtf(rtb As RichTextBox, text As String,
                                  colour As Color,
                                  Optional bold As Boolean = False,
                                  Optional italic As Boolean = False,
                                  Optional underline As Boolean = False)
        Dim style As FontStyle = FontStyle.Regular
        If bold      Then style = style Or FontStyle.Bold
        If italic    Then style = style Or FontStyle.Italic
        If underline Then style = style Or FontStyle.Underline
        rtb.SelectionStart  = rtb.TextLength
        rtb.SelectionLength = 0
        rtb.SelectionColor  = colour
        rtb.SelectionFont   = New Font(rtb.Font, style)
        rtb.AppendText(text)
        rtb.SelectionColor = rtb.ForeColor
        rtb.SelectionFont  = rtb.Font
    End Sub

    Private Sub AR(rtb As RichTextBox, label As String, value As String,
                   Optional valColour As Color = Nothing,
                   Optional valBold As Boolean = False)
        If valColour.IsEmpty Then valColour = C_VALUE
        AppendRtf(rtb, "  " & label, C_LABEL)
        AppendRtf(rtb, value & Environment.NewLine, valColour, valBold)
    End Sub

    Private Sub SectionHeader(rtb As RichTextBox, text As String)
        AppendRtf(rtb, Environment.NewLine & text & Environment.NewLine, C_HEADER, bold:=True)
    End Sub

    Private Sub Divider(rtb As RichTextBox)
        AppendRtf(rtb, "===========================================================" & Environment.NewLine, C_DIVIDER)
    End Sub

    ' -----------------------------------------------------------------------
    ' RenderOutput: header block
    ' Writes VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME /
    ' LAST TRANSACTED PRICE / HOLD STATUS / ATR ENTRY LEVELS / KELLY SIZING.
    ' Called from RenderOutput() in MainForm_Render_Sections.vb.
    ' -----------------------------------------------------------------------
    Friend Sub RenderOutputHeader(rtb As RichTextBox,
                                   r As IndicatorResults,
                                   v As VerdictResult,
                                   norms As DynamicNorms,
                                   cfg As EngineSettings,
                                   lastTradePrice As Double,
                                   atrStop As Double,
                                   atrTarget As Double)
        Dim ts As String = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") & " UTC+8"
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        Dim longStop   As Double = r.CurrentPrice - atrStop
        Dim longTarget As Double = r.CurrentPrice + atrTarget
        Dim shortStop  As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget
        Dim rrRatio As String = String.Format("1:{0:F1}", targetMult / stopMult)

        Divider(rtb)
        AppendRtf(rtb, "  VERDICT:    ", C_LABEL)
        Dim vColour As Color = C_VALUE
        Select Case v.Verdict
            Case "STRONG LONG", "LONG"   : vColour = C_GOOD
            Case "WEAK LONG"             : vColour = Color.FromArgb(120, 200, 120)
            Case "STRONG SHORT", "SHORT" : vColour = C_BAD
            Case "WEAK SHORT"            : vColour = Color.FromArgb(220, 130, 130)
            Case Else                    : vColour = C_WARN
        End Select
        AppendRtf(rtb, v.Verdict & Environment.NewLine, vColour, bold:=True)

        If v.VerdictContext <> "" AndAlso v.VerdictContext <> "CONFIRMED" Then
            AppendRtf(rtb, "  CONTEXT:    ", C_LABEL)
            Dim ctxColour As Color
            Select Case v.VerdictContext
                Case "MOMENTUM_FADING"  : ctxColour = C_BAD
                Case "FLOW_UNCONFIRMED" : ctxColour = C_WARN
                Case "STRUCTURALLY_WEAK": ctxColour = C_DIM
                Case Else               : ctxColour = C_VALUE
            End Select
            AppendRtf(rtb, v.VerdictContext & Environment.NewLine, ctxColour, bold:=True)
        End If

        AppendRtf(rtb, "  CONFIDENCE: ", C_LABEL)
        Dim cColour As Color = If(v.Confidence = "HIGH", C_GOOD,
                                  If(v.Confidence = "MEDIUM", C_WARN, C_BAD))
        AppendRtf(rtb, v.Confidence & Environment.NewLine, cColour, bold:=True)

        Dim maxScore As Integer = v.MaxScore
        AppendRtf(rtb, "  SCORE:      ", C_LABEL)
        If v.RegimePenalty > 0 Then
            AppendRtf(rtb, String.Format("Long {0}/{2} (eff.{1})  |  Short {3}/{2} (eff.{4})  |  TRANSITIONAL penalty: -{5}",
                                         v.LongScore, v.EffectiveLongScore, maxScore,
                                         v.ShortScore, v.EffectiveShortScore, v.RegimePenalty) & Environment.NewLine, C_WARN)
        Else
            AppendRtf(rtb, String.Format("Long {0}/{1}  |  Short {2}/{1}",
                                          v.LongScore, maxScore, v.ShortScore) & Environment.NewLine, C_VALUE)
        End If

        AppendRtf(rtb, "  TIME:       ", C_LABEL)
        AppendRtf(rtb, ts & Environment.NewLine, C_DIM)
        Divider(rtb)

        AppendRtf(rtb, "  LAST TRANSACTED PRICE:  ", C_LABEL)
        AppendRtf(rtb, If(lastTradePrice > 0,
                          lastTradePrice.ToString("F1"),
                          "N/A") & Environment.NewLine, C_VALUE)

        If v.HoldStatus <> "N/A -- no open position" Then
            AppendRtf(rtb, "  HOLD / EXIT: ", C_LABEL)
            AppendRtf(rtb, v.HoldStatus & Environment.NewLine, C_WARN, bold:=True)
        End If

        ScoringEngine.CalcKellySizing(v, atrStop, cfg)

        SectionHeader(rtb, String.Format("ATR ENTRY LEVELS  (ATR {0:F2} x {1:F2} scale | {2:F1}x stop / {3:F1}x target)",
                                          r.ATR, norms.ATRScaleFactor, stopMult, targetMult))

        AppendRtf(rtb, "  Long:   ", C_LABEL)
        If v.AdjustedLongTarget > 0 Then
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} ",
                                          longStop, r.CurrentPrice, longTarget), C_DIM)
            AppendRtf(rtb, String.Format("--> {0:F1}  [{1}]",
                                          v.AdjustedLongTarget, v.TargetCapReason) & Environment.NewLine, C_WARN, bold:=True)
        Else
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          longStop, r.CurrentPrice, longTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, C_GOOD)
        End If

        AppendRtf(rtb, "  Short:  ", C_LABEL)
        If v.AdjustedShortTarget > 0 Then
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} ",
                                          shortStop, r.CurrentPrice, shortTarget), C_DIM)
            AppendRtf(rtb, String.Format("--> {0:F1}  [{1}]",
                                          v.AdjustedShortTarget, v.TargetCapReason) & Environment.NewLine, C_WARN, bold:=True)
        Else
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          shortStop, r.CurrentPrice, shortTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, C_BAD)
        End If

        If v.KellyPWin > 0 Then
            Dim isNoTradeBias As Boolean = v.Verdict.StartsWith("NO TRADE")
            Dim capTag As String = If(v.KellyCapped, "  [CAPPED]", "")
            AppendRtf(rtb, Environment.NewLine, C_DIVIDER)
            If isNoTradeBias Then
                AppendRtf(rtb, String.Format("KELLY SIZING  [BIAS ONLY — NO TRADE]{0}" & Environment.NewLine,
                                              capTag), C_HEADER, bold:=True)
            Else
                AppendRtf(rtb, String.Format("KELLY SIZING{0}" & Environment.NewLine,
                                              capTag), C_HEADER, bold:=True)
            End If
            AppendRtf(rtb, "  Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets." & Environment.NewLine, C_DIM)
            AppendRtf(rtb, "  Treat as directional bias indicator only." & Environment.NewLine, C_DIM)
            AppendRtf(rtb, "  p(win):   ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P1}" & Environment.NewLine, v.KellyPWin), C_VALUE)
            AppendRtf(rtb, "  f* / Half-Kelly:  ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P2}  /  {1:P2}" & Environment.NewLine,
                                          v.KellyF, v.KellyFHalf), C_VALUE)
            AppendRtf(rtb, "  Applied fraction: ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P2}" & Environment.NewLine, v.KellyFApplied), C_VALUE)
            AppendRtf(rtb, "  Risk $:    ", C_LABEL)
            AppendRtf(rtb, String.Format("${0:F2}" & Environment.NewLine, v.KellyRiskUsd), C_VALUE)
            AppendRtf(rtb, If(isNoTradeBias, "  Lean: ", "  Contracts: "), C_LABEL)
            Dim contractColour As Color = If(v.KellyContracts >= 1, C_GOOD, C_WARN)
            Dim contractStr As String
            If isNoTradeBias Then
                contractStr = If(v.KellyContracts >= 1,
                                 String.Format("{0} contracts  (not a trade signal)", v.KellyContracts.ToString()),
                                 "< 1 contract  (bias only; not a trade signal)")
            Else
                contractStr = If(v.KellyContracts >= 1,
                                 v.KellyContracts.ToString() & " contracts",
                                 "< 1 contract  (stop too wide for min size)")
            End If
            AppendRtf(rtb, contractStr & Environment.NewLine, contractColour, bold:=True)
        End If
    End Sub

End Class
