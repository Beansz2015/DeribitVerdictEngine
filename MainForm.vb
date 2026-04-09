' MainForm.vb  v0.43
' v0.27 -- ATR entry levels block moved above DYNAMIC NORMS
' v0.28 -- CalcOFI updated.
' v0.29 -- CalcCVD; SettingsLoader.Initialise.
' v0.30 -- settings-driven gate params.
' v0.31 -- CalcVWAP ByRef; CalcVWAPBands.
' v0.32 -- session2Hour/Minute; WarmupCandles from settings.
' v0.33 -- CalcTTMSqueeze.
' v0.34 -- (engine only)
' v0.35 -- 15m candle fetch; CalcMTFGate.
' v0.36 -- BC30456 fix.
' v0.37 -- VolumeUSD rename.
' v0.38 -- Auto-run feature.
' v0.38a -- SettingsLoader.Save signature fix.
' v0.39 -- 6 UI bug fixes (ChrW, Panel grouping, Single default, spacing, alignment).
' v0.40 -- UI fixes:
'          1. InitAutoRunControls: cfg.AutoRun.Enabled forced False on load so
'             btnStartStop always shows Play+Start on cold start.
'          2. AR_Y=58, TXT_Y=88: equal 8px gaps above and below AR row.
'          3. NUD Padding.Top=5 in Designer for bottom-aligned digit appearance.
'          4. RenderOutput fully rewritten to use AppendRtf for colour-coded output.
' v0.41 -- Fix NUD digit top-alignment:
'          Padding does not move the inner TextBox of NumericUpDown.
'          Use EM_SETMARGINS (SendMessage) on the NUD's child TextBox handle
'          after the form handle is created to push digits to vertical centre.
' v0.42 -- Last transacted price display above ATR Entry Levels.
'          TIME: line changed from UTC to UTC+8.
' v0.43 -- CalcVPFRLite wired into RunAnalysisAsync (after CalcOBV).
'          VPFR-lite scoring now fully active end-to-end.

Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Public Class MainForm

    Private Const HDR_Y As Integer = 8
    Private Const HDR_H As Integer = 42
    Private Const BTN_X As Integer = 286
    Private Const BTN_W As Integer = 140
    Private Const VRD_X As Integer = 430
    Private Const AR_Y  As Integer = 58    ' 8px below HDR bottom (50)
    Private Const AR_H  As Integer = 22
    Private Const TXT_Y As Integer = 88    ' 8px below AR bottom (80)
    Private Const SS_X  As Integer = 426   ' right edge of Analyze button
    Private Const STATUS_H As Integer = 18

    Private Const EM_SETMARGINS  As Integer = &HD3
    Private Const EC_LEFTMARGIN  As Integer = 1
    Private Const EC_RIGHTMARGIN As Integer = 2

    ' EM_SETRECT / EM_SETRECTNP to override the internal formatting rect
    Private Const EM_SETRECT   As Integer = &HB3
    Private Const EM_SETRECTNP As Integer = &HB4

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

    ' Overload that accepts a RECT structure (needed for EM_SETRECT)
    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, ByRef lParam As RECT) As IntPtr
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RECT
        Public Left   As Integer
        Public Top    As Integer
        Public Right  As Integer
        Public Bottom As Integer
    End Structure

    Private _oiHistory As New List(Of OiSnapshot)()

    ' -----------------------------------------------------------------------
    ' Auto-run state
    ' -----------------------------------------------------------------------
    Private _autoRunTimer   As IAutoRunTimer
    Private _countdownTimer As Threading.Timer
    Private _countdownSecs  As Integer = 0
    Private _intervalMs     As Integer = 60_000

    Private Shared ReadOnly CHAR_PLAY As String = ChrW(9654) & " Start"
    Private Shared ReadOnly CHAR_STOP As String = ChrW(9632) & " Stop"

    ' -----------------------------------------------------------------------
    ' Colour palette for RenderOutput
    ' -----------------------------------------------------------------------
    Private Shared ReadOnly C_DIVIDER  As Color = Color.FromArgb(80, 80, 80)
    Private Shared ReadOnly C_HEADER   As Color = Color.FromArgb(255, 220, 80)   ' amber
    Private Shared ReadOnly C_LABEL    As Color = Color.FromArgb(160, 160, 160)  ' mid-grey
    Private Shared ReadOnly C_VALUE    As Color = Color.FromArgb(200, 200, 200)  ' light-grey
    Private Shared ReadOnly C_GOOD     As Color = Color.FromArgb(80, 220, 120)   ' green
    Private Shared ReadOnly C_WARN     As Color = Color.FromArgb(255, 180, 40)   ' orange
    Private Shared ReadOnly C_BAD      As Color = Color.FromArgb(255, 80, 80)    ' red
    Private Shared ReadOnly C_HIT      As Color = Color.FromArgb(100, 200, 255)  ' cyan
    Private Shared ReadOnly C_DIM      As Color = Color.FromArgb(100, 100, 100)  ' dim

    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.43"
        SetOutputMargins(6, 6)
        AddHandler Me.Resize, Sub(s As Object, ev As EventArgs) ResizeControls()
        ' Fix NUD inner TextBox vertical alignment once the handle exists
        AddHandler Me.HandleCreated, AddressOf OnHandleCreated
        ResizeControls()
        UpdateLogInfo()
        SettingsLoader.Initialise(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json"))
        InitAutoRunControls()
    End Sub

    ' -----------------------------------------------------------------------
    ' NUD digit vertical centering
    ' NumericUpDown hosts a child TextBox (Controls(0)).  Setting EM_SETRECT
    ' overrides the internal formatting rectangle so text renders vertically
    ' centred rather than pinned to the top of the control.
    ' -----------------------------------------------------------------------
    Private Sub OnHandleCreated(sender As Object, e As EventArgs)
        CentreNudText(nudMinutes)
        CentreNudText(nudSeconds)
    End Sub

    Private Shared Sub CentreNudText(nud As NumericUpDown)
        ' The inner TextBox is Controls(0) on all standard WinForms builds.
        If nud.Controls.Count = 0 Then Return
        Dim innerTb As Control = nud.Controls(0)
        If Not innerTb.IsHandleCreated Then innerTb.CreateControl()

        ' Spinner button width is typically 16px; exclude it from right edge.
        Const SPIN_W As Integer = 16
        Dim h As Integer = innerTb.Height

        ' Vertical padding: push text down ~3px so it sits centred, not top-pinned.
        Const TOP_PAD As Integer = 3

        Dim rc As RECT
        rc.Left   = 1
        rc.Top    = TOP_PAD
        rc.Right  = innerTb.Width - SPIN_W - 2
        rc.Bottom = h - 1

        SendMessage(innerTb.Handle, EM_SETRECTNP, 0, rc)
    End Sub

    ' -----------------------------------------------------------------------
    ' Auto-run: initialise controls from settings and wire events
    ' -----------------------------------------------------------------------
    Private Sub InitAutoRunControls()
        _autoRunTimer = New WinFormsAutoRunTimer(Me)
        Dim cfg As EngineSettings = SettingsLoader.Current
        nudMinutes.Value = Math.Max(0, Math.Min(60, cfg.AutoRun.IntervalMinutes))
        nudSeconds.Value = Math.Max(0, Math.Min(59, cfg.AutoRun.IntervalSeconds))
        rbSingle.Checked = True
        ' Always start in stopped state regardless of saved setting.
        ' User must manually click Start each session.
        btnStartStop.Text    = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        UpdateCountdownLabel("Auto-run: OFF")
    End Sub

    Private Sub btnStartStop_Click(sender As Object, e As EventArgs) Handles btnStartStop.Click
        If _autoRunTimer.IsRunning Then
            StopAutoRun()
        Else
            StartAutoRun()
        End If
    End Sub

    Private Sub StartAutoRun()
        Dim mins As Integer = CInt(nudMinutes.Value)
        Dim secs As Integer = CInt(nudSeconds.Value)
        _intervalMs = (mins * 60 + secs) * 1000
        If _intervalMs < 10_000 Then
            MessageBox.Show("Minimum interval is 10 seconds.", "Auto-Run",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.AutoRun.Enabled         = True
        cfg.AutoRun.IntervalMinutes = mins
        cfg.AutoRun.IntervalSeconds = secs
        SettingsLoader.Save(cfg, "auto_run enabled via UI")

        _countdownSecs = _intervalMs \ 1000
        btnStartStop.Text      = CHAR_STOP
        btnStartStop.BackColor = Color.FromArgb(160, 40, 40)
        nudMinutes.Enabled = False
        nudSeconds.Enabled = False

        _countdownTimer = New Threading.Timer(AddressOf OnCountdownTick, Nothing, 1000, 1000)

        If rbSingle.Checked Then
            CType(_autoRunTimer, WinFormsAutoRunTimer).StartOnce(_intervalMs, AddressOf RunAutoAnalysis)
        Else
            _autoRunTimer.Start(_intervalMs, AddressOf RunAutoAnalysis)
        End If
    End Sub

    Private Sub StopAutoRun()
        _autoRunTimer.Stop()
        If _countdownTimer IsNot Nothing Then
            _countdownTimer.Dispose()
            _countdownTimer = Nothing
        End If
        btnStartStop.Text      = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        nudMinutes.Enabled = True
        nudSeconds.Enabled = True
        UpdateCountdownLabel("Auto-run: OFF")
        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.AutoRun.Enabled = False
        SettingsLoader.Save(cfg, "auto_run disabled via UI")
    End Sub

    Private Sub RunAutoAnalysis()
        If Not btnAnalyze.Enabled Then Return
        _countdownSecs = _intervalMs \ 1000
        If rbSingle.Checked Then
            btnStartStop.Text      = CHAR_PLAY
            btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
            nudMinutes.Enabled = True
            nudSeconds.Enabled = True
        End If
        btnAnalyze_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub OnCountdownTick(state As Object)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        Try
            Me.Invoke(Sub()
                          If Not _autoRunTimer.IsRunning Then Return
                          _countdownSecs -= 1
                          If _countdownSecs < 0 Then _countdownSecs = _intervalMs \ 1000
                          Dim m As Integer = _countdownSecs \ 60
                          Dim s As Integer = _countdownSecs Mod 60
                          UpdateCountdownLabel(String.Format("Next run in: {0}:{1:D2}  [{2}]",
                                                             m, s,
                                                             If(rbRepeat.Checked, "REPEAT", "SINGLE")))
                      End Sub)
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub UpdateCountdownLabel(text As String)
        lblCountdown.Text = text
    End Sub

    ' -----------------------------------------------------------------------
    ' Layout
    ' -----------------------------------------------------------------------
    Private Sub SetOutputMargins(leftPx As Integer, rightPx As Integer)
        Dim lParam As Integer = (rightPx << 16) Or (leftPx And &HFFFF)
        SendMessage(txtOutput.Handle, EM_SETMARGINS, EC_LEFTMARGIN Or EC_RIGHTMARGIN, lParam)
    End Sub

    Private Sub ResizeControls()
        Dim W As Integer = Me.ClientSize.Width
        Dim H As Integer = Me.ClientSize.Height

        ' Row 1
        lblPositionTitle.Location = New System.Drawing.Point(8, HDR_Y)
        lblPositionTitle.Size     = New System.Drawing.Size(108, HDR_H)
        rbNone.Location  = New System.Drawing.Point(120, HDR_Y + (HDR_H - 18) \ 2)
        rbLong.Location  = New System.Drawing.Point(210, HDR_Y + 2)
        rbShort.Location = New System.Drawing.Point(210, HDR_Y + 22)
        btnAnalyze.Location = New System.Drawing.Point(BTN_X, HDR_Y)
        btnAnalyze.Size     = New System.Drawing.Size(BTN_W, HDR_H)
        lblVerdict.Location = New System.Drawing.Point(VRD_X, HDR_Y)
        lblVerdict.Size     = New System.Drawing.Size(W - VRD_X - 8, HDR_H)

        ' Row 2
        lblAutoRun.Location  = New System.Drawing.Point(8, AR_Y)
        lblAutoRun.Size      = New System.Drawing.Size(78, AR_H)
        nudMinutes.Location  = New System.Drawing.Point(90, AR_Y)
        nudMinutes.Size      = New System.Drawing.Size(42, AR_H)
        lblMin.Location      = New System.Drawing.Point(136, AR_Y + 3)
        nudSeconds.Location  = New System.Drawing.Point(164, AR_Y)
        nudSeconds.Size      = New System.Drawing.Size(42, AR_H)
        lblSec.Location      = New System.Drawing.Point(210, AR_Y + 3)
        pnlMode.Location     = New System.Drawing.Point(242, AR_Y)
        pnlMode.Size         = New System.Drawing.Size(134, AR_H)
        rbSingle.Location    = New System.Drawing.Point(0, 2)
        rbRepeat.Location    = New System.Drawing.Point(68, 2)
        btnStartStop.Location = New System.Drawing.Point(SS_X, AR_Y - 1)
        btnStartStop.Size     = New System.Drawing.Size(70, AR_H + 2)

        ' Output
        Dim statusY As Integer = H - STATUS_H - 2
        txtOutput.Location = New System.Drawing.Point(8, TXT_Y)
        txtOutput.Size     = New System.Drawing.Size(W - 16, statusY - TXT_Y - 2)
        SetOutputMargins(6, 6)

        ' Status bar
        lblLogInfo.Location    = New System.Drawing.Point(8, H - STATUS_H)
        lblLogInfo.Size        = New System.Drawing.Size(W - 420, STATUS_H)
        lblCountdown.Location  = New System.Drawing.Point(W - 410, H - STATUS_H)
        lblCountdown.Size      = New System.Drawing.Size(200, STATUS_H)
        lnkCalibCheck.Location = New System.Drawing.Point(W - 230, H - STATUS_H)
        lnkResetLog.Location   = New System.Drawing.Point(W - 80, H - STATUS_H)
    End Sub

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

    ' -----------------------------------------------------------------------
    ' AppendRtf helper
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

    ' Shorthand for a labelled key:value pair on its own line
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
    ' Analysis
    ' -----------------------------------------------------------------------
    Private Async Sub btnAnalyze_Click(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        btnAnalyze.Enabled = False
        btnAnalyze.Text    = "Fetching..."
        txtOutput.Clear()
        AppendRtf(txtOutput, "Fetching data from Deribit..." & Environment.NewLine, C_LABEL)
        lblVerdict.Text      = "..."
        lblVerdict.BackColor = Color.Gray

        Try
            Await RunAnalysisAsync()
        Catch ex As Exception
            txtOutput.Clear()
            AppendRtf(txtOutput, "ERROR: " & ex.Message & Environment.NewLine & ex.StackTrace, C_BAD)
            lblVerdict.Text      = "ERROR"
            lblVerdict.BackColor = Color.OrangeRed
        Finally
            btnAnalyze.Enabled = True
            btnAnalyze.Text    = "Analyze Now"
        End Try
    End Sub

    Private Async Function RunAnalysisAsync() As Task
        Dim cfg As EngineSettings = SettingsLoader.Current

        Dim t_1m      = DeribitClient.GetCandlesAsync("1", 250)
        Dim t_5m      = DeribitClient.GetCandlesAsync("5", 210)
        Dim t_15m     = DeribitClient.GetCandlesAsync("15", 70)
        Dim t_funding = DeribitClient.GetFundingRateAsync()
        Dim t_book    = DeribitClient.GetBookSummaryAsync()
        Dim t_ob      = DeribitClient.GetOrderBookAsync(10)
        Dim t_trades  = DeribitClient.GetRecentTradesAsync(100)

        Await Task.WhenAll(t_1m, t_5m, t_15m, t_funding, t_book, t_ob, t_trades)

        Dim candles1m    = Await t_1m
        Dim candles5m    = Await t_5m
        Dim candles15m   = Await t_15m
        Dim fundingRate  = Await t_funding
        Dim bookSummary  = Await t_book
        Dim orderBook    = Await t_ob
        Dim recentTrades = Await t_trades

        If candles1m.Count < 50 Then
            txtOutput.Clear()
            AppendRtf(txtOutput, "Insufficient 1m candle data returned. Please retry." & Environment.NewLine, C_WARN)
            Return
        End If

        ' Last transacted price from most recent trade (Deribit returns newest-first)
        Dim lastTradePrice As Double = If(recentTrades IsNot Nothing AndAlso recentTrades.Count > 0,
                                          recentTrades(0).Price, 0)

        Dim r As New IndicatorResults()
        r.CurrentPrice = candles1m.Last().Close

        r.ATR       = IndicatorEngine.CalcATR(candles1m, 7)
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

        r.VolumeSMA9       = IndicatorEngine.CalcVolumeSMA(candles1m, 9)
        r.CurrentVolume    = candles1m.Last().Volume
        r.CurrentVolumeUSD = candles1m.Last().VolumeUSD
        r.VolumeRatio      = If(r.VolumeSMA9 > 0, r.CurrentVolume / r.VolumeSMA9, 1)

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

        Dim vwapS2Hour   As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim vwapS2Minute As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim vwapWarmup   As Integer = cfg.Indicators.VWAP.WarmupCandles

        r.VWAP       = IndicatorEngine.CalcVWAP(candles1m, r.VWAPSessionCandles, vwapS2Hour, vwapS2Minute)
        r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)
        IndicatorEngine.CalcVWAPBands(candles1m, r.VWAP,
                                      r.VWAPSigma1Upper, r.VWAPSigma1Lower,
                                      r.VWAPSigma2Upper, r.VWAPSigma2Lower,
                                      vwapS2Hour, vwapS2Minute)

        Dim minBBW As Double
        IndicatorEngine.CalcBBW(candles1m, 20, 2.0, r.BBW, minBBW, r.SqueezeStatus)
        IndicatorEngine.CalcTTMSqueeze(candles1m, r.TTMHistogram, r.TTMDirection, r.TTMSignal)

        r.EMA9  = IndicatorEngine.CalcEMA(candles1m, 9)
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

        Dim mtfProposed As String = "NONE"
        If candles15m IsNot Nothing AndAlso candles15m.Count >= cfg.MTFGate.DmiPeriod + 2 Then
            If r.Regime = "TRENDING_UP" OrElse r.EMAAlignment = "BULL" Then
                mtfProposed = "LONG"
            ElseIf r.Regime = "TRENDING_DOWN" OrElse r.EMAAlignment = "BEAR" Then
                mtfProposed = "SHORT"
            End If
        End If

        IndicatorEngine.CalcMTFGate(
            candles15m,
            r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment,
            r.MTFGatePass, r.MTFGateReason,
            proposedDirection:=mtfProposed,
            adxPeriod:=cfg.MTFGate.DmiPeriod,
            adxMin:=cfg.Indicators.ADX.TrendThreshold,
            minOf:=cfg.MTFGate.RequiredConfirms,
            candleLookback:=cfg.MTFGate.CandleCount)

        r.EMA200_5m    = IndicatorEngine.CalcEMA(candles5m, 200)
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

        ' VPFR-lite: volume profile POC, HVN/LVN classification, scoring signal
        IndicatorEngine.CalcVPFRLite(candles1m, r)

        Dim posState As PositionState = PositionState.None
        If rbLong.Checked  Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort

        Dim verdict = ScoringEngine.Calculate(r, posState, norms, cfg)

        AnalysisLogger.LogRun(r, verdict)
        UpdateLogInfo()

        RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)
    End Function

    ' -----------------------------------------------------------------------
    ' RenderOutput -- full AppendRtf colour-coded output
    ' -----------------------------------------------------------------------
    Private Sub RenderOutput(r As IndicatorResults, v As VerdictResult,
                              norms As DynamicNorms, vwapWarmup As Integer,
                              lastTradePrice As Double)
        Dim rtb As RichTextBox = txtOutput
        rtb.Clear()

        Dim ts As String = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") & " UTC+8"

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

        ' --- ATR Entry Levels ---
        Dim atrStop    As Double = r.ATR * norms.ATRScaleFactor * 1.5
        Dim atrTarget  As Double = r.ATR * norms.ATRScaleFactor * 3.0
        Dim longStop   As Double = r.CurrentPrice - atrStop
        Dim longTarget As Double = r.CurrentPrice + atrTarget
        Dim shortStop  As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget

        SectionHeader(rtb, String.Format("ATR ENTRY LEVELS  (ATR {0:F2} x {1:F2} scale | 1.5x stop / 3.0x target)",
                                          r.ATR, norms.ATRScaleFactor))
        AppendRtf(rtb, "  Long:   ", C_LABEL)
        AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R 1:2  (risk {3:F1} / rwd {4:F1})",
                                      longStop, r.CurrentPrice, longTarget, atrStop, atrTarget) & Environment.NewLine, C_GOOD)
        AppendRtf(rtb, "  Short:  ", C_LABEL)
        AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R 1:2  (risk {3:F1} / rwd {4:F1})",
                                      shortStop, r.CurrentPrice, shortTarget, atrStop, atrTarget) & Environment.NewLine, C_BAD)

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
        Dim cfg As EngineSettings = SettingsLoader.Current
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
        Dim cvdColour As Color = If(r.CVDSlope = "UP", C_GOOD, If(r.CVDSlope = "DOWN", C_BAD, C_VALUE))
        AppendRtf(rtb, String.Format("Net:{0:F0}  |  Slope:{1}  |  Div:{2}",
                                      r.CVDValue, r.CVDSlope, r.CVDDivergence) & Environment.NewLine, cvdColour)

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
            Dim lMark As String = If(item.LongHit,  "[L]", "   ")
            Dim sMark As String = If(item.ShortHit, "[S]", "   ")
            Dim hitColour As Color = If(item.LongHit OrElse item.ShortHit, C_HIT, C_DIM)
            AppendRtf(rtb, String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                          item.Label, lMark, sMark, item.Note) & Environment.NewLine, hitColour)
        Next
        AppendRtf(rtb, "  " & New String("-"c, 70) & Environment.NewLine, C_DIVIDER)
        AppendRtf(rtb, String.Format("  {0,-18}  {1,5:F0}  {2,6:F0}",
                                      "TOTAL", CDbl(v.LongScore), CDbl(v.ShortScore)) & Environment.NewLine,
                  C_VALUE, bold:=True)

        ' --- Hold / Exit ---
        If v.HoldStatus <> "N/A -- no open position" Then
            AppendRtf(rtb, Environment.NewLine & "HOLD / EXIT: ", C_LABEL)
            AppendRtf(rtb, v.HoldStatus & Environment.NewLine, C_WARN, bold:=True)
        End If

        ' Update verdict label
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
