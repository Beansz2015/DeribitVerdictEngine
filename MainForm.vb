' MainForm.vb  v0.32
' v0.27 -- ATR entry levels block moved above DYNAMIC NORMS
' v0.28 -- CalcOFI call updated for new top-3 weighted signature;
'          OFI display line now shows weighted bid/ask volumes.
' v0.29 -- CalcCVD call added after CalcLiquidations in RunAnalysisAsync.
'          CVD display line added to TIER 2 block in RenderOutput.
'          SettingsLoader.Initialise() called from constructor.
' v0.30 -- CalcOBV / CalcRSIDivergence / CalcROCSeries now pass settings-driven gate params.
'          ScoringEngine.Calculate now receives EngineSettings as 4th argument.
' v0.31 -- CalcVWAP call updated: captures r.VWAPSessionCandles via ByRef.
'          CalcVWAPBands called after CalcVWAP to populate sigma1/sigma2 fields.
'          RenderOutput VWAP line now shows: VWAP value, dev%, session candles,
'          sigma1 and sigma2 band levels, and [WARMUP] tag if <15 candles.
' v0.32 -- CalcVWAP and CalcVWAPBands now receive session2Hour/Minute from settings.
'          Warmup threshold read from cfg.Indicators.VWAP.WarmupCandles instead of hardcoded 15.

Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public Class MainForm

    Private Const HDR_Y As Integer = 8
    Private Const HDR_H As Integer = 42
    Private Const BTN_X As Integer = 286
    Private Const BTN_W As Integer = 140
    Private Const VRD_X As Integer = 430
    Private Const TXT_Y As Integer = 56
    Private Const STATUS_H As Integer = 18

    Private Const EM_SETMARGINS As Integer = &HD3
    Private Const EC_LEFTMARGIN As Integer = 1
    Private Const EC_RIGHTMARGIN As Integer = 2

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

    Private _oiHistory As New List(Of OiSnapshot)()

    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.32"
        SetOutputMargins(6, 6)
        AddHandler Me.Resize, Sub(s As Object, ev As EventArgs) ResizeControls()
        ResizeControls()
        UpdateLogInfo()
        SettingsLoader.Initialise(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json"))
    End Sub

    Private Sub SetOutputMargins(leftPx As Integer, rightPx As Integer)
        Dim lParam As Integer = (rightPx << 16) Or (leftPx And &HFFFF)
        SendMessage(txtOutput.Handle, EM_SETMARGINS, EC_LEFTMARGIN Or EC_RIGHTMARGIN, lParam)
    End Sub

    Private Sub ResizeControls()
        Dim W As Integer = Me.ClientSize.Width
        Dim H As Integer = Me.ClientSize.Height

        lblPositionTitle.Location = New System.Drawing.Point(8, HDR_Y)
        lblPositionTitle.Size = New System.Drawing.Size(108, HDR_H)

        rbNone.Location = New System.Drawing.Point(120, HDR_Y + (HDR_H - 18) \ 2)
        rbLong.Location = New System.Drawing.Point(210, HDR_Y + 2)
        rbShort.Location = New System.Drawing.Point(210, HDR_Y + 22)

        btnAnalyze.Location = New System.Drawing.Point(BTN_X, HDR_Y)
        btnAnalyze.Size = New System.Drawing.Size(BTN_W, HDR_H)

        lblVerdict.Location = New System.Drawing.Point(VRD_X, HDR_Y)
        lblVerdict.Size = New System.Drawing.Size(W - VRD_X - 8, HDR_H)

        Dim statusY As Integer = H - STATUS_H - 2
        txtOutput.Location = New System.Drawing.Point(8, TXT_Y)
        txtOutput.Size = New System.Drawing.Size(W - 16, statusY - TXT_Y - 2)
        SetOutputMargins(6, 6)

        lblLogInfo.Location = New System.Drawing.Point(8, H - STATUS_H)
        lblLogInfo.Size = New System.Drawing.Size(W - 280, STATUS_H)
        lnkCalibCheck.Location = New System.Drawing.Point(W - 230, H - STATUS_H)
        lnkResetLog.Location = New System.Drawing.Point(W - 80, H - STATUS_H)
    End Sub

    Private Sub UpdateLogInfo()
        Dim rows As Integer = AnalysisLogger.GetRowCount()
        Dim path As String = AnalysisLogger.GetLogPath()
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
        txtOutput.Text = BuildCalibrationReport()
    End Sub

    Private Function BuildCalibrationReport() As String
        Dim path As String = AnalysisLogger.GetLogPath()
        Dim sb As New System.Text.StringBuilder()

        sb.AppendLine("===========================================================")
        sb.AppendLine("  CALIBRATION READINESS REPORT")
        sb.AppendLine("  " & DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") & " UTC")
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

        Dim totalRows As Integer = 0
        Dim regimeCounts As New Dictionary(Of String, Integer) From {
            {"TRENDING_UP", 0}, {"TRENDING_DOWN", 0},
            {"RANGE_BOUND", 0}, {"TRANSITIONAL", 0}
        }
        Dim liqEvents As Integer = 0
        Dim ofiValues As New List(Of Double)()
        Dim volRatioValues As New List(Of Double)()
        Dim sessionDates As New HashSet(Of String)()

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

        Const MIN_TOTAL As Integer = 300
        Const MIN_PER_REGIME As Integer = 50
        Const MIN_REGIMES_COVERED As Integer = 3
        Const MIN_LIQ_EVENTS As Integer = 2
        Const MIN_SESSIONS As Integer = 3

        Dim regimesCovered As Integer = regimeCounts.Values.ToList().Where(Function(c) c >= MIN_PER_REGIME).Count()
        Dim okTotal = totalRows >= MIN_TOTAL
        Dim okRegimes = regimesCovered >= MIN_REGIMES_COVERED
        Dim okLiq = liqEvents >= MIN_LIQ_EVENTS
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
            Dim ofiMin = ofiValues.Min()
            Dim ofiMax = ofiValues.Max()
            Dim ofiRange = ofiMax - ofiMin
            Dim ofiOk = ofiRange > 2.0
            sb.AppendLine("  OFI Ratio range   : " & ofiMin.ToString("F2") & " to " & ofiMax.ToString("F2") &
                          "  (spread: " & ofiRange.ToString("F2") & ")  " & Flag(ofiOk))
        Else
            sb.AppendLine("  OFI Ratio         : insufficient data")
        End If
        If volRatioValues.Count > 10 Then
            Dim vMin = volRatioValues.Min()
            Dim vMax = volRatioValues.Max()
            Dim vRange = vMax - vMin
            Dim vOk = vRange > 1.0
            sb.AppendLine("  Volume Ratio range: " & vMin.ToString("F2") & " to " & vMax.ToString("F2") &
                          "  (spread: " & vRange.ToString("F2") & ")  " & Flag(vOk))
        Else
            sb.AppendLine("  Volume Ratio      : insufficient data")
        End If
        sb.AppendLine()
        sb.AppendLine("===========================================================")
        If overallReady Then
            sb.AppendLine("  VERDICT: READY FOR RECALIBRATION")
        Else
            sb.AppendLine("  VERDICT: NOT YET READY -- see flags above")
        End If
        sb.AppendLine("===========================================================")
        Return sb.ToString()
    End Function

    Private Shared Function Flag(ok As Boolean) As String
        Return If(ok, "[OK]", "[--]")
    End Function

    Private Async Sub btnAnalyze_Click(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        btnAnalyze.Enabled = False
        btnAnalyze.Text = "Fetching..."
        txtOutput.Text = "Fetching data from Deribit..."
        lblVerdict.Text = "..."
        lblVerdict.BackColor = Color.Gray

        Try
            Await RunAnalysisAsync()
        Catch ex As Exception
            txtOutput.Text = "ERROR: " & ex.Message & Environment.NewLine & ex.StackTrace
            lblVerdict.Text = "ERROR"
            lblVerdict.BackColor = Color.OrangeRed
        Finally
            btnAnalyze.Enabled = True
            btnAnalyze.Text = "Analyze Now"
        End Try
    End Sub

    Private Async Function RunAnalysisAsync() As Task
        Dim cfg As EngineSettings = SettingsLoader.Current

        Dim t_1m = DeribitClient.GetCandlesAsync("1", 250)
        Dim t_5m = DeribitClient.GetCandlesAsync("5", 210)
        Dim t_funding = DeribitClient.GetFundingRateAsync()
        Dim t_book = DeribitClient.GetBookSummaryAsync()
        Dim t_ob = DeribitClient.GetOrderBookAsync(10)
        Dim t_trades = DeribitClient.GetRecentTradesAsync(100)

        Await Task.WhenAll(t_1m, t_5m, t_funding, t_book, t_ob, t_trades)

        Dim candles1m = Await t_1m
        Dim candles5m = Await t_5m
        Dim fundingRate = Await t_funding
        Dim bookSummary = Await t_book
        Dim orderBook = Await t_ob
        Dim recentTrades = Await t_trades

        If candles1m.Count < 50 Then
            txtOutput.Text = "Insufficient 1m candle data returned. Please retry."
            Return
        End If

        Dim r As New IndicatorResults()
        r.CurrentPrice = candles1m.Last().Close

        r.ATR = IndicatorEngine.CalcATR(candles1m, 7)
        r.ATRAvg20d = IndicatorEngine.CalcATR(candles5m, 60) * Math.Sqrt(5)

        Dim norms As DynamicNorms = DynamicNorms.Compute(candles1m, r.ATR)
        r.ATRSizeMultiplier = Math.Round(norms.ATRScaleFactor, 2)

        Dim rocSeries = IndicatorEngine.CalcROCSeries(candles1m,
                            cfg.Indicators.ROC.Period,
                            cfg.Indicators.ROC.SeriesLookback)
        r.ROC = If(rocSeries.Count > 0, rocSeries.Last(), 0)
        If rocSeries.Count >= 2 Then
            Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
            r.ROCSlope = If(delta > 0.01, "RISING", If(delta < -0.01, "FALLING", "FLAT"))
        Else
            r.ROCSlope = "FLAT"
        End If

        r.RSI = IndicatorEngine.CalcRSI(candles1m, cfg.Indicators.RSI.Period)

        r.VolumeSMA9 = IndicatorEngine.CalcVolumeSMA(candles1m, 9)
        r.CurrentVolume = candles1m.Last().Volume
        r.CurrentVolumeUSD = candles1m.Last().VolumUSD
        r.VolumeRatio = If(r.VolumeSMA9 > 0, r.CurrentVolume / r.VolumeSMA9, 1)

        IndicatorEngine.CalcDMI(candles5m, 9, r.PlusDI, r.MinusDI, r.ADX)
        If r.ADX > 25 AndAlso r.PlusDI > r.MinusDI Then
            r.Regime = "TRENDING_UP"
        ElseIf r.ADX > 25 AndAlso r.MinusDI > r.PlusDI Then
            r.Regime = "TRENDING_DOWN"
        ElseIf r.ADX < 20 Then
            r.Regime = "RANGE_BOUND"
        Else
            r.Regime = "TRANSITIONAL"
        End If

        ' v0.32: session boundary times read from settings
        Dim vwapS2Hour   As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim vwapS2Minute As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim vwapWarmup   As Integer = cfg.Indicators.VWAP.WarmupCandles

        r.VWAP = IndicatorEngine.CalcVWAP(candles1m, r.VWAPSessionCandles,
                                           vwapS2Hour, vwapS2Minute)
        r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)
        IndicatorEngine.CalcVWAPBands(candles1m, r.VWAP,
                                      r.VWAPSigma1Upper, r.VWAPSigma1Lower,
                                      r.VWAPSigma2Upper, r.VWAPSigma2Lower,
                                      vwapS2Hour, vwapS2Minute)

        Dim minBBW As Double
        IndicatorEngine.CalcBBW(candles1m, 20, 2.0, r.BBW, minBBW, r.SqueezeStatus)

        r.EMA9 = IndicatorEngine.CalcEMA(candles1m, 9)
        r.EMA21 = IndicatorEngine.CalcEMA(candles1m, 21)
        r.EMA50 = IndicatorEngine.CalcEMA(candles1m, 50)
        If r.EMA9 > r.EMA21 AndAlso r.EMA21 > r.EMA50 Then
            r.EMAAlignment = "BULL"
        ElseIf r.EMA9 < r.EMA21 AndAlso r.EMA21 < r.EMA50 Then
            r.EMAAlignment = "BEAR"
        Else
            r.EMAAlignment = "MIXED"
        End If

        r.FundingRate = fundingRate
        If fundingRate > 0.001 Then
            r.FundingBias = "LONGS HEAVILY CROWDED"
        ElseIf fundingRate > 0.0005 Then
            r.FundingBias = "LONGS CROWDED"
        ElseIf fundingRate < -0.001 Then
            r.FundingBias = "SHORTS HEAVILY CROWDED"
        ElseIf fundingRate < -0.0005 Then
            r.FundingBias = "SHORTS CROWDED"
        Else
            r.FundingBias = "NEUTRAL"
        End If

        Dim nowTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        r.OI_Current = bookSummary.OI
        _oiHistory.Add(New OiSnapshot(nowTs, bookSummary.OI))
        _oiHistory = _oiHistory.Where(Function(x) nowTs - x.Ts < 70 * 60 * 1000L).ToList()

        Dim oi15m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 15 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()
        Dim oi60m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 61 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()

        r.OIChange15m = If(oi15m IsNot Nothing AndAlso oi15m.OI > 0, (r.OI_Current - oi15m.OI) / oi15m.OI * 100, 0)
        r.OIChange60m = If(oi60m IsNot Nothing AndAlso oi60m.OI > 0, (r.OI_Current - oi60m.OI) / oi60m.OI * 100, 0)

        Dim priceUp As Boolean = r.CurrentPrice > bookSummary.MarkPrice * 0.9999
        If r.OIChange15m > 1 AndAlso priceUp Then
            r.OISignal = "NEW LONGS"
        ElseIf r.OIChange15m > 1 AndAlso Not priceUp Then
            r.OISignal = "NEW SHORTS"
        ElseIf r.OIChange15m < -1 AndAlso priceUp Then
            r.OISignal = "COVERING"
        ElseIf r.OIChange15m < -1 AndAlso Not priceUp Then
            r.OISignal = "CAPITULATION"
        Else
            r.OISignal = "NEUTRAL"
        End If

        IndicatorEngine.CalcOFI(orderBook, r.OFIRatio, r.OFISignal, r.OFIBidVol, r.OFIAskVol)
        IndicatorEngine.CalcLiquidations(recentTrades, r.LiqLongSize, r.LiqShortSize, r.LiqSignal)
        IndicatorEngine.CalcCVD(recentTrades, candles1m, r.CVDValue, r.CVDSlope, r.CVDDivergence)

        r.EMA200_5m = IndicatorEngine.CalcEMA(candles5m, 200)
        r.PriceVsEMA200 = If(r.EMA200_5m > 0,
                              If(r.CurrentPrice > r.EMA200_5m, "ABOVE", "BELOW"),
                              "N/A")

        IndicatorEngine.CalcDonchian(candles1m, 20, r.DonchianUpper, r.DonchianLower)
        If r.CurrentPrice > r.DonchianUpper Then
            r.DonchianSignal = "LONG"
        ElseIf r.CurrentPrice < r.DonchianLower Then
            r.DonchianSignal = "SHORT"
        Else
            r.DonchianSignal = "NONE"
        End If

        IndicatorEngine.CalcOBV(candles1m, r.OBVTrend, r.OBVDivergence,
                                cfg.Indicators.OBV.TrendGate,
                                cfg.Indicators.OBV.DivergenceGate)
        r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(candles1m,
                              cfg.Indicators.RSI.Period,
                              cfg.Indicators.RSI.DivergencePriceGate,
                              cfg.Indicators.RSI.DivergenceRsiDelta)

        Dim posState As PositionState = PositionState.None
        If rbLong.Checked Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort

        Dim verdict = ScoringEngine.Calculate(r, posState, norms, cfg)

        AnalysisLogger.LogRun(r, verdict)
        UpdateLogInfo()

        RenderOutput(r, verdict, norms, vwapWarmup)
    End Function

    Private Sub RenderOutput(r As IndicatorResults, v As VerdictResult, norms As DynamicNorms,
                              vwapWarmup As Integer)
        Dim sb As New System.Text.StringBuilder()
        Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") & " UTC"

        Dim usdStr As String
        If r.CurrentVolumeUSD >= 1_000_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000_000).ToString("F2") & "M"
        ElseIf r.CurrentVolumeUSD >= 1_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000).ToString("F1") & "K"
        Else
            usdStr = "$" & r.CurrentVolumeUSD.ToString("F0")
        End If

        Dim maxScore As Integer = v.MaxScore
        Dim scoreLine As String
        If v.RegimePenalty > 0 Then
            scoreLine = String.Format("Long {0}/{2} (eff.{1})  |  Short {3}/{2} (eff.{4})  |  TRANSITIONAL penalty: -{5}",
                                     v.LongScore, v.EffectiveLongScore, maxScore,
                                     v.ShortScore, v.EffectiveShortScore, v.RegimePenalty)
        Else
            scoreLine = String.Format("Long {0}/{1}  |  Short {2}/{1}", v.LongScore, maxScore, v.ShortScore)
        End If

        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC FALLBACK")

        Dim atrStop   As Double = r.ATR * norms.ATRScaleFactor * 1.5
        Dim atrTarget As Double = r.ATR * norms.ATRScaleFactor * 3.0
        Dim longStop   As Double = r.CurrentPrice - atrStop
        Dim longTarget As Double = r.CurrentPrice + atrTarget
        Dim shortStop  As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget

        sb.AppendLine("===========================================================")
        sb.AppendLine("  VERDICT:    " & v.Verdict)
        sb.AppendLine("  CONFIDENCE: " & v.Confidence)
        sb.AppendLine("  SCORE:      " & scoreLine)
        sb.AppendLine("  TIME:       " & ts)
        sb.AppendLine("===========================================================")
        sb.AppendLine()

        sb.AppendLine("ATR ENTRY LEVELS:  (ATR " & r.ATR.ToString("F2") & " x " & norms.ATRScaleFactor.ToString("F2") & " scale | 1.5x stop / 3.0x target)")
        sb.AppendLine("  Long:   Stop " & longStop.ToString("F1").PadLeft(9) &
                      "  |  Entry " & r.CurrentPrice.ToString("F1").PadLeft(9) &
                      "  |  Target " & longTarget.ToString("F1").PadLeft(9) &
                      "    R:R  1:2  (risk " & atrStop.ToString("F1") & " / rwd " & atrTarget.ToString("F1") & ")")
        sb.AppendLine("  Short:  Stop " & shortStop.ToString("F1").PadLeft(9) &
                      "  |  Entry " & r.CurrentPrice.ToString("F1").PadLeft(9) &
                      "  |  Target " & shortTarget.ToString("F1").PadLeft(9) &
                      "    R:R  1:2  (risk " & atrStop.ToString("F1") & " / rwd " & atrTarget.ToString("F1") & ")")
        sb.AppendLine()

        sb.AppendLine("DYNAMIC NORMS  [" & normMode & "]")
        sb.AppendLine("  Vol threshold : H:" & norms.VolHighThreshold.ToString("F2") & "x" &
                      "  M:" & norms.VolMidThreshold.ToString("F2") & "x" &
                      "  (mean=" & norms.VolMean.ToString("F4") & " BTC" &
                      "  σ=" & norms.VolStdDev.ToString("F4") & ")")
        sb.AppendLine("  VWAP dev thr  : ±" & norms.VWAPDevThreshold.ToString("F2") & "% (legacy ref)")
        sb.AppendLine("  ATR scale     : " & norms.ATRScaleFactor.ToString("F2") & "x" &
                      "  (ATR=" & r.ATR.ToString("F2") & "  ref=" & norms.ATRRef.ToString("F2") & ")")
        sb.AppendLine()

        sb.AppendLine("REGIME (5m): " & r.Regime)
        sb.AppendLine("  ADX: " & r.ADX.ToString("F1") & "  |  +DI: " & r.PlusDI.ToString("F1") & "  |  -DI: " & r.MinusDI.ToString("F1"))
        sb.AppendLine()

        sb.AppendLine("CORE SIGNALS (1m):")
        sb.AppendLine("  ROC(9):       " & r.ROC.ToString("F3") & "  |  Slope: " & r.ROCSlope)
        Dim rsiDiv As String = If(String.IsNullOrEmpty(r.RSIDivergence) OrElse r.RSIDivergence = "NONE",
                                  "",
                                  "  |  Div: " & r.RSIDivergence)
        sb.AppendLine("  RSI(9):       " & r.RSI.ToString("F1") & rsiDiv)
        sb.AppendLine("  Volume:       " & r.CurrentVolume.ToString("F4") & " BTC (" & usdStr & ")" &
                      "  |  vs SMA: " & r.VolumeRatio.ToString("F2") & "x  |  SMA: " & r.VolumeSMA9.ToString("F4") & " BTC")
        sb.AppendLine()

        ' v0.32: warmup threshold from settings parameter
        Dim vwapWarmupTag As String = If(r.VWAPSessionCandles < vwapWarmup, "  [WARMUP]", "")
        Dim vwapSessionLabel As String
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim s2h As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim s2m As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim nowHour As Integer = DateTime.UtcNow.Hour
        Dim nowMin  As Integer = DateTime.UtcNow.Minute
        If nowHour < s2h OrElse (nowHour = s2h AndAlso nowMin < s2m) Then
            vwapSessionLabel = "daily(00:00)"
        Else
            vwapSessionLabel = "US(" & s2h.ToString("D2") & ":" & s2m.ToString("D2") & ")"
        End If

        sb.AppendLine("TIER 1 SIGNALS:")
        sb.AppendLine("  VWAP:         " & r.VWAP.ToString("F1") &
                      "  |  Dev: " & r.VWAPDevPct.ToString("F2") & "%" &
                      "  |  Price: " & r.CurrentPrice.ToString("F1") &
                      "  |  Session: " & vwapSessionLabel & " (" & r.VWAPSessionCandles & "c)" & vwapWarmupTag)
        sb.AppendLine("  VWAP Bands:   σ1[" & r.VWAPSigma1Lower.ToString("F1") & "," & r.VWAPSigma1Upper.ToString("F1") & "]" &
                      "  σ2[" & r.VWAPSigma2Lower.ToString("F1") & "," & r.VWAPSigma2Upper.ToString("F1") & "]")
        sb.AppendLine("  BBW:          " & r.BBW.ToString("F4") & "  |  Squeeze: " & r.SqueezeStatus)
        sb.AppendLine("  EMA Ribbon:   9:" & r.EMA9.ToString("F1") & "  21:" & r.EMA21.ToString("F1") & "  50:" & r.EMA50.ToString("F1") & "  |  " & r.EMAAlignment)
        sb.AppendLine("  Funding:      " & (r.FundingRate * 100).ToString("F5") & "%  |  " & r.FundingBias & "  (info only)")
        sb.AppendLine("  OI Change:    15m: " & r.OIChange15m.ToString("F2") & "%  |  60m: " & r.OIChange60m.ToString("F2") & "%  |  " & r.OISignal)
        sb.AppendLine()

        sb.AppendLine("TIER 2 SIGNALS:")
        sb.AppendLine("  Order Flow:   " & r.OFISignal &
                      "  |  Ratio: " & r.OFIRatio.ToString("F2") &
                      "  |  Bid(w): " & r.OFIBidVol.ToString("F1") &
                      "  Ask(w): " & r.OFIAskVol.ToString("F1") &
                      "  [top-3 wtd]")
        sb.AppendLine("  Liquidations: " & r.LiqSignal & "  |  Long Liqs: " & r.LiqLongSize.ToString("F0") & "  Short Liqs: " & r.LiqShortSize.ToString("F0"))
        sb.AppendLine("  CVD:          Net:" & r.CVDValue.ToString("F0") &
                      "  |  Slope:" & r.CVDSlope &
                      "  |  Div:" & r.CVDDivergence)
        sb.AppendLine("  5m EMA(200):  " & r.EMA200_5m.ToString("F1") & "  |  " & r.PriceVsEMA200)
        sb.AppendLine()

        sb.AppendLine("TIER 3 SIGNALS:")
        sb.AppendLine("  Donchian(20): Upper:" & r.DonchianUpper.ToString("F1") & "  Lower:" & r.DonchianLower.ToString("F1") & "  |  " & r.DonchianSignal)
        sb.AppendLine("  OBV:          Trend:" & r.OBVTrend & "  |  Divergence:" & r.OBVDivergence)
        sb.AppendLine()

        sb.AppendLine("POSITION SIZING:")
        sb.AppendLine("  ATR(7):       " & r.ATR.ToString("F2") & "  |  Scale: " & norms.ATRScaleFactor.ToString("F2") & "x" &
                      "  (ref " & norms.ATRRef.ToString("F2") & ")")
        sb.AppendLine()

        sb.AppendLine("HOLD/EXIT STATUS:")
        sb.AppendLine("  " & v.HoldStatus)
        sb.AppendLine()

        sb.AppendLine("SIGNAL BREAKDOWN:")
        sb.AppendLine("  " & "Indicator".PadRight(20) & "Long".PadLeft(6) & "Short".PadLeft(7) & "  Note")
        sb.AppendLine("  " & New String("-"c, 65))
        For Each sig In v.SignalBreakdown
            Dim lMark As String = If(sig.LongHit, "[L]", " . ")
            Dim sMark As String = If(sig.ShortHit, "[S]", " . ")
            sb.AppendLine("  " & sig.Label.PadRight(20) & lMark.PadLeft(6) & sMark.PadLeft(7) & "  " & sig.Note)
        Next
        sb.AppendLine("===========================================================")

        txtOutput.Text = sb.ToString()

        lblVerdict.Text = v.Verdict
        Select Case v.Verdict
            Case "STRONG LONG"
                lblVerdict.BackColor = Color.LimeGreen
            Case "LONG"
                lblVerdict.BackColor = Color.Green
            Case "WEAK LONG"
                lblVerdict.BackColor = Color.DarkGreen
            Case "STRONG SHORT"
                lblVerdict.BackColor = Color.Red
            Case "SHORT"
                lblVerdict.BackColor = Color.Crimson
            Case "WEAK SHORT"
                lblVerdict.BackColor = Color.DarkRed
            Case Else
                lblVerdict.BackColor = Color.Gray
        End Select
        lblVerdict.ForeColor = Color.White
    End Sub

End Class
