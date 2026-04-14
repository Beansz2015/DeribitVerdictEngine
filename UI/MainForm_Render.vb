' UI/MainForm_Render.vb
' Partial class: RenderOutput, RTF helpers, calibration report, log helpers.

Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Log helpers
    ' -----------------------------------------------------------------------
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

    ' -----------------------------------------------------------------------
    ' Calibration readiness report
    ' -----------------------------------------------------------------------
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

    ' -----------------------------------------------------------------------
    ' RTF helpers
    ' -----------------------------------------------------------------------
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
    ' RenderOutput
    ' -----------------------------------------------------------------------
    Private Sub RenderOutput(r As IndicatorResults, v As VerdictResult,
                              norms As DynamicNorms, vwapWarmup As Integer,
                              lastTradePrice As Double)
        Dim rtb As RichTextBox = txtOutput
        rtb.Clear()

        Dim ts As String = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") & " UTC+8"
        Dim cfg As EngineSettings = SettingsLoader.Current

        ' --- Verdict block ---
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

        ' Step 5b: Verdict sub-context tag -- rendered when not CONFIRMED.
        ' CONFIRMED is silent (absence of line = all tiers aligned).
        ' MOMENTUM_FADING = red (C_BAD), FLOW_UNCONFIRMED = amber (C_WARN),
        ' STRUCTURALLY_WEAK = dim (C_DIM).
        If v.VerdictContext <> "CONFIRMED" AndAlso v.VerdictContext <> "" Then
            AppendRtf(rtb, "  CONTEXT:    ", C_LABEL)
            Dim ctxColour As Color
            Select Case v.VerdictContext
                Case "MOMENTUM_FADING"   : ctxColour = C_BAD
                Case "FLOW_UNCONFIRMED"  : ctxColour = C_WARN
                Case "STRUCTURALLY_WEAK" : ctxColour = C_DIM
                Case Else                : ctxColour = C_VALUE
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

        ' --- Last Transacted Price ---
        AppendRtf(rtb, "  LAST TRANSACTED PRICE:  ", C_LABEL)
        AppendRtf(rtb, If(lastTradePrice > 0,
                          lastTradePrice.ToString("F1"),
                          "N/A") & Environment.NewLine, C_VALUE)

        ' --- Hold / Exit ---
        If v.HoldStatus <> "N/A -- no open position" Then
            AppendRtf(rtb, "  HOLD / EXIT: ", C_LABEL)
            AppendRtf(rtb, v.HoldStatus & Environment.NewLine, C_WARN, bold:=True)
        End If

        ' --- ATR Entry Levels ---
        ' Entry pivot = r.CurrentPrice (1m candle close).
        ' Multipliers read from cfg so label stays in sync with settings.json.
        ' If VerdictResult carries an HVN-capped target, the capped level is
        ' shown in amber with the cap reason.  Raw R:R is always shown so the
        ' trader sees both the theoretical and the realistic exit.
        Dim stopMult    As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult  As Double = cfg.Scoring.AtrTargetMultiplier
        Dim atrStop     As Double = r.ATR * norms.ATRScaleFactor * stopMult
        Dim atrTarget   As Double = r.ATR * norms.ATRScaleFactor * targetMult
        Dim longStop    As Double = r.CurrentPrice - atrStop
        Dim longTarget  As Double = r.CurrentPrice + atrTarget
        Dim shortStop   As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget
        Dim rrRatio     As String = String.Format("1:{0:F1}", targetMult / stopMult)

        ' Populate Kelly fields on VerdictResult (display-only, no scoring impact).
        ' atrStop is always positive (distance in price points).
        ScoringEngine.CalcKellySizing(v, atrStop, cfg)

        SectionHeader(rtb, String.Format("ATR ENTRY LEVELS  (ATR {0:F2} x {1:F2} scale | {2:F1}x stop / {3:F1}x target)",
                                          r.ATR, norms.ATRScaleFactor, stopMult, targetMult))

        ' Long row
        AppendRtf(rtb, "  Long:   ", C_LABEL)
        If v.AdjustedLongTarget > 0 Then
            ' Show raw target struck-through in dim, then capped target in amber
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} ",
                                          longStop, r.CurrentPrice, longTarget), C_DIM)
            AppendRtf(rtb, String.Format("--> {0:F1}  [{1}]",
                                          v.AdjustedLongTarget, v.TargetCapReason) & Environment.NewLine, C_WARN, bold:=True)
        Else
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          longStop, r.CurrentPrice, longTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, C_GOOD)
        End If

        ' Short row
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

        ' --- Kelly Sizing block ---
        ' Shown only when there is positive edge (KellyF > 0).
        ' Suppressed on NEUTRAL / no-edge verdicts.
        If v.KellyF > 0 Then
            Dim capTag As String = If(v.KellyCapped, "  [CAPPED]", "")
            AppendRtf(rtb, Environment.NewLine, C_DIVIDER)
            AppendRtf(rtb, String.Format("KELLY SIZING  [{0}]{1}" & Environment.NewLine,
                                          v.KellyPMode, capTag), C_HEADER, bold:=True)
            AppendRtf(rtb, "  p(win):   ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P1}" & Environment.NewLine, v.KellyPWin), C_VALUE)
            AppendRtf(rtb, "  f* / Half-Kelly:  ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P2}  /  {1:P2}" & Environment.NewLine,
                                          v.KellyF, v.KellyFHalf), C_VALUE)
            AppendRtf(rtb, "  Applied fraction: ", C_LABEL)
            AppendRtf(rtb, String.Format("{0:P2}" & Environment.NewLine, v.KellyFApplied), C_VALUE)
            AppendRtf(rtb, "  Risk $:    ", C_LABEL)
            AppendRtf(rtb, String.Format("${0:F2}" & Environment.NewLine, v.KellyRiskUsd), C_VALUE)
            AppendRtf(rtb, "  Contracts: ", C_LABEL)
            Dim contractColour As Color = If(v.KellyContracts >= 1, C_GOOD, C_WARN)
            Dim contractStr As String = If(v.KellyContracts >= 1,
                                           v.KellyContracts.ToString() & " contracts",
                                           "< 1 contract  (stop too wide for min size)")
            AppendRtf(rtb, contractStr & Environment.NewLine, contractColour, bold:=True)
        End If

        ' --- Dynamic Norms ---
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC FALLBACK")
        SectionHeader(rtb, "DYNAMIC NORMS  [" & normMode & "]")
        AR(rtb, "Vol threshold : ",
           String.Format("H:{0:F2}x  M:{1:F2}x  (mean={2:F4} BTC  s={3:F4})",
                          norms.VolHighThreshold, norms.VolMidThreshold, norms.VolMean, norms.VolStdDev))
        AR(rtb, "VWAP dev thr  : ", String.Format("+/-{0:F2}% (legacy ref)", norms.VWAPDevThreshold))
        AR(rtb, "ATR scale     : ",
           String.Format("{0:F2}x  (ATR={1:F2}  ref={2:F2})", norms.ATRScaleFactor, r.ATR, norms.ATRRef))

        ' --- Regime ---
        SectionHeader(rtb, "REGIME (5m): " & r.Regime)
        Dim regColour As Color = C_VALUE
        Select Case r.Regime
            Case "TRENDING_UP"   : regColour = C_GOOD
            Case "TRENDING_DOWN" : regColour = C_BAD
            Case "RANGE_BOUND"   : regColour = C_WARN
            Case Else            : regColour = C_DIM
        End Select
        AppendRtf(rtb, "  ", C_LABEL)
        AppendRtf(rtb, String.Format("ADX: {0:F1}  |  +DI: {1:F1}  |  -DI: {2:F1}",
                                      r.ADX, r.PlusDI, r.MinusDI) & Environment.NewLine, regColour)

        ' --- Core Signals ---
        SectionHeader(rtb, "CORE SIGNALS (1m):")
        AppendRtf(rtb, "  ROC(9):       ", C_LABEL)
        Dim rocColour As Color = If(r.ROC > 0, C_GOOD, If(r.ROC < 0, C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("{0:F3}  |  Slope: {1}", r.ROC, r.ROCSlope) & Environment.NewLine, rocColour)

        AppendRtf(rtb, "  RSI(9):       ", C_LABEL)
        Dim rsiColour As Color = If(r.RSI > 70, C_BAD, If(r.RSI < 30, C_GOOD, C_VALUE))
        Dim rsiDiv As String = If(String.IsNullOrEmpty(r.RSIDivergence) OrElse r.RSIDivergence = "NONE",
                                   "", "  |  Div: " & r.RSIDivergence)
        AppendRtf(rtb, String.Format("{0:F1}", r.RSI) & rsiDiv & Environment.NewLine, rsiColour)

        Dim usdStr As String
        If r.CurrentVolumeUSD >= 1_000_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000_000).ToString("F2") & "M"
        ElseIf r.CurrentVolumeUSD >= 1_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000).ToString("F1") & "K"
        Else
            usdStr = "$" & r.CurrentVolumeUSD.ToString("F0")
        End If
        AppendRtf(rtb, "  Volume:       ", C_LABEL)
        Dim volColour As Color = If(r.VolumeRatio >= 1.5, C_GOOD, If(r.VolumeRatio < 0.7, C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("{0:F4} BTC ({1})  |  vs SMA: {2:F2}x  |  SMA: {3:F4} BTC",
                                      r.CurrentVolume, usdStr, r.VolumeRatio, r.VolumeSMA9) & Environment.NewLine, volColour)

        ' --- VWAP ---
        Dim s2h As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim s2m As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim vwapWarmupTag As String = If(r.VWAPSessionCandles < vwapWarmup, "  [WARMUP]", "")
        SectionHeader(rtb, String.Format("VWAP (reset {0:D2}:{1:D2} UTC){2}:", s2h, s2m, vwapWarmupTag))
        AppendRtf(rtb, "  Value:  ", C_LABEL)
        Dim devColour As Color = If(Math.Abs(r.VWAPDevPct) > norms.VWAPDevThreshold, C_WARN, C_VALUE)
        AppendRtf(rtb, String.Format("{0:F1}  |  Dev: {1:F3}%  |  Candles: {2}",
                                      r.VWAP, r.VWAPDevPct, r.VWAPSessionCandles) & Environment.NewLine, devColour)
        AppendRtf(rtb, "  s1 band: ", C_LABEL)
        AppendRtf(rtb, String.Format("[{0:F1}, {1:F1}]  |  s2 band: [{2:F1}, {3:F1}]",
                                      r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                                      r.VWAPSigma2Lower, r.VWAPSigma2Upper) & Environment.NewLine, C_DIM)

        ' --- BBW / TTM ---
        SectionHeader(rtb, "BBW / TTM SQUEEZE:")
        AppendRtf(rtb, "  BBW: ", C_LABEL)
        Dim sqColour As Color = If(r.SqueezeStatus = "SQUEEZE", C_WARN, C_VALUE)
        AppendRtf(rtb, String.Format("{0:F3}  |  Status: {1}", r.BBW, r.SqueezeStatus) & Environment.NewLine, sqColour)
        AppendRtf(rtb, "  TTM: ", C_LABEL)
        Dim ttmColour As Color = If(r.TTMDirection = "UP", C_GOOD, If(r.TTMDirection = "DOWN", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("Histogram={0:F2}  Dir={1}  Signal={2}",
                                      r.TTMHistogram, r.TTMDirection, r.TTMSignal) & Environment.NewLine, ttmColour)

        ' --- EMA Ribbon ---
        SectionHeader(rtb, "EMA RIBBON (1m):")
        AppendRtf(rtb, "  ", C_LABEL)
        Dim emaColour As Color = If(r.EMAAlignment = "BULL", C_GOOD, If(r.EMAAlignment = "BEAR", C_BAD, C_WARN))
        AppendRtf(rtb, String.Format("9: {0:F1}  |  21: {1:F1}  |  50: {2:F1}  |  Align: {3}",
                                      r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment) & Environment.NewLine, emaColour)
        AppendRtf(rtb, "  5m EMA200: ", C_LABEL)
        Dim ema200Colour As Color = If(r.PriceVsEMA200 = "ABOVE", C_GOOD, C_BAD)
        AppendRtf(rtb, String.Format("{0:F1}  |  Price: {1}", r.EMA200_5m, r.PriceVsEMA200) & Environment.NewLine, ema200Colour)

        ' --- Market Structure ---
        SectionHeader(rtb, "MARKET STRUCTURE:")
        AppendRtf(rtb, "  Donchian(20): ", C_LABEL)
        Dim donchColour As Color = If(r.DonchianSignal = "LONG", C_GOOD, If(r.DonchianSignal = "SHORT", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("Upper={0:F1}  Lower={1:F1}  |  Signal: {2}",
                                      r.DonchianUpper, r.DonchianLower, r.DonchianSignal) & Environment.NewLine, donchColour)
        AppendRtf(rtb, "  OBV: ", C_LABEL)
        AppendRtf(rtb, String.Format("Trend={0}  |  Div={1}", r.OBVTrend, r.OBVDivergence) & Environment.NewLine, C_VALUE)

        ' --- VPFR-lite ---
        Dim vpfrColour As Color = If(r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL", C_GOOD,
                                     If(r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR", C_BAD, C_DIM))
        AppendRtf(rtb, "  VPFR-lite: ", C_LABEL)
        AppendRtf(rtb, String.Format("POC:{0:F1}  |  {1}  |  HVN@POC:{2}",
                                      r.VPFRPoc, r.VPFRSignal,
                                      If(r.VPFRHVNearPoc, "YES", "NO")) & Environment.NewLine, vpfrColour)

        ' --- Open Interest ---
        SectionHeader(rtb, "OPEN INTEREST:")
        AppendRtf(rtb, "  OI: ", C_LABEL)
        Dim oiColour As Color = If(r.OISignal = "NEW LONGS" OrElse r.OISignal = "COVERING", C_GOOD,
                                   If(r.OISignal = "NEW SHORTS" OrElse r.OISignal = "CAPITULATION", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("{0:F0}  |  d15m: {1:F3}%  |  d60m: {2:F3}%  |  Signal: {3}",
                                      r.OI_Current, r.OIChange15m, r.OIChange60m, r.OISignal) & Environment.NewLine, oiColour)

        ' --- Order Flow ---
        SectionHeader(rtb, "ORDER FLOW:")
        AppendRtf(rtb, "  OFI Ratio: ", C_LABEL)
        Dim ofiColour As Color = If(r.OFIRatio > 1.2, C_GOOD, If(r.OFIRatio < 0.8, C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("{0:F2}  |  Bid Vol: {1:F0}  |  Ask Vol: {2:F0}  |  {3}",
                                      r.OFIRatio, r.OFIBidVol, r.OFIAskVol, r.OFISignal) & Environment.NewLine, ofiColour)

        AppendRtf(rtb, "  CVD:       ", C_LABEL)
        Dim cvdColour As Color = If(r.CVDSlope = "RISING", C_GOOD, If(r.CVDSlope = "FALLING", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("Net:{0:F0}  |  Slope:{1}  |  Div:{2}",
                                      r.CVDValue, r.CVDSlope, r.CVDDivergence) & Environment.NewLine, cvdColour)

        AppendRtf(rtb, "  TFI:       ", C_LABEL)
        Dim tfiColour As Color = If(r.TFISignal = "BUY PRESSURE", C_GOOD,
                                    If(r.TFISignal = "SELL PRESSURE", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("{0:F3}  |  {1}",
                                      r.TFIValue, r.TFISignal) & Environment.NewLine, tfiColour)

        AppendRtf(rtb, "  MicroCVD:  ", C_LABEL)
        Dim microColour As Color
        Select Case r.MicroCVDSignal
            Case "BULL_ACCEL" : microColour = C_GOOD
            Case "BEAR_ACCEL" : microColour = C_BAD
            Case "BULL_DECEL" : microColour = Color.FromArgb(120, 200, 120)
            Case "BEAR_DECEL" : microColour = Color.FromArgb(220, 130, 130)
            Case Else         : microColour = C_VALUE
        End Select
        AppendRtf(rtb, String.Format("E:{0:F0}  M:{1:F0}  L:{2:F0}  |  {3}  |  {4}",
                                      r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                      r.MicroCVDMomentum, r.MicroCVDSignal) & Environment.NewLine, microColour)

        ' --- Liquidations ---
        SectionHeader(rtb, "LIQUIDATIONS:")
        AppendRtf(rtb, "  ", C_LABEL)
        Dim liqColour As Color = If(r.LiqSignal <> "NONE", C_WARN, C_DIM)
        AppendRtf(rtb, String.Format("Long: {0:F0}  |  Short: {1:F0}  |  Signal: {2}",
                                      r.LiqLongSize, r.LiqShortSize, r.LiqSignal) & Environment.NewLine, liqColour)

        ' --- MTF Gate ---
        SectionHeader(rtb, "MTF GATE (15m): " & If(r.MTFGatePass, "PASS", "BLOCK"))
        Dim mtfColour As Color = If(r.MTFGatePass, C_GOOD, C_BAD)
        AppendRtf(rtb, "  15m Trend: ", C_LABEL)
        AppendRtf(rtb, String.Format("{0}  |  ADX: {1:F1}  |  EMA: {2}",
                                      r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment) & Environment.NewLine, mtfColour)
        AppendRtf(rtb, "  Reason: ", C_LABEL)
        AppendRtf(rtb, r.MTFGateReason & Environment.NewLine, C_DIM)

        ' --- Funding ---
        SectionHeader(rtb, "FUNDING:")
        AppendRtf(rtb, "  Rate: ", C_LABEL)
        Dim fundColour As Color = If(r.FundingBias.Contains("HEAVILY"), C_BAD,
                                     If(r.FundingBias = "NEUTRAL", C_VALUE, C_WARN))
        AppendRtf(rtb, String.Format("{0:F4}%  |  {1}", r.FundingRate, r.FundingBias) & Environment.NewLine, fundColour)

        ' --- Signal Breakdown ---
        AppendRtf(rtb, Environment.NewLine, C_DIVIDER)
        Divider(rtb)
        AppendRtf(rtb, "  SIGNAL BREAKDOWN" & Environment.NewLine, C_HEADER, bold:=True)
        Divider(rtb)
        AppendRtf(rtb, String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                      "Signal", "Long", "Short", "Note") & Environment.NewLine, C_LABEL)
        AppendRtf(rtb, "  " & New String("-"c, 70) & Environment.NewLine, C_DIVIDER)
        For Each item In v.SignalBreakdown
            Dim lMark    As String = If(item.LongHit,  "[L]", "   ")
            Dim sMark    As String = If(item.ShortHit, "[S]", "   ")
            Dim hitColour As Color = If(item.LongHit OrElse item.ShortHit, C_HIT, C_DIM)
            AppendRtf(rtb, String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                          item.Label, lMark, sMark, item.Note) & Environment.NewLine, hitColour)
        Next
        AppendRtf(rtb, "  " & New String("-"c, 70) & Environment.NewLine, C_DIVIDER)
        AppendRtf(rtb, String.Format("  {0,-18}  {1,5:F0}  {2,6:F0}",
                                      "TOTAL", CDbl(v.LongScore), CDbl(v.ShortScore)) & Environment.NewLine,
                  C_VALUE, bold:=True)

        ' --- Scroll to top ---
        rtb.SelectionStart = 0
        rtb.ScrollToCaret()

        Dim bg As Color
        Select Case v.Verdict
            Case "STRONG LONG"  : bg = Color.FromArgb(0, 180, 90)
            Case "LONG"         : bg = Color.FromArgb(0, 140, 60)
            Case "WEAK LONG"    : bg = Color.FromArgb(60, 160, 60)
            Case "STRONG SHORT" : bg = Color.FromArgb(200, 40, 40)
            Case "SHORT"        : bg = Color.FromArgb(180, 30, 30)
            Case "WEAK SHORT"   : bg = Color.FromArgb(180, 80, 80)
            Case Else           : bg = Color.DimGray
        End Select
        lblVerdict.BackColor = bg
        lblVerdict.Text      = v.Verdict & "  [" & v.Confidence & "]"
    End Sub

End Class
