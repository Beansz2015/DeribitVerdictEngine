' UI/MainForm_Layout.vb
' Root partial class: layout constants, P/Invoke, shared fields, constructor,
' NUD centring, ResizeControls, SetOutputMargins.

Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Layout constants
    ' -----------------------------------------------------------------------
    Private Const HDR_Y    As Integer = 8
    Private Const HDR_H    As Integer = 42
    Private Const BTN_X    As Integer = 286
    Private Const BTN_W    As Integer = 140
    Private Const VRD_X    As Integer = 430
    Private Const AR_Y     As Integer = 58     ' 8px below HDR bottom (50)
    Private Const AR_H     As Integer = 22
    Private Const TXT_Y    As Integer = 88     ' 8px below AR bottom (80)
    Private Const SS_X     As Integer = 426    ' right edge of Analyze button
    Private Const STATUS_H As Integer = 18

    ' Longest line in the output (VWAP signal breakdown note row ~133 chars).
    ' Width and height are computed from the font at startup in SizeToContent().
    Private Const OUTPUT_CHARS As Integer = 133
    ' Number of output lines when fully rendered (counted from RenderOutput).
    ' Includes blank lines between sections.
    Private Const OUTPUT_LINES As Integer = 115

    ' -----------------------------------------------------------------------
    ' P/Invoke: Win32 edit-control messages
    ' -----------------------------------------------------------------------
    Private Const EM_SETMARGINS  As Integer = &HD3
    Private Const EC_LEFTMARGIN  As Integer = 1
    Private Const EC_RIGHTMARGIN As Integer = 2
    Private Const EM_SETRECT     As Integer = &HB3
    Private Const EM_SETRECTNP   As Integer = &HB4

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

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

    ' -----------------------------------------------------------------------
    ' Shared fields (used by multiple partials)
    ' -----------------------------------------------------------------------
    Private _oiHistory As New List(Of OiSnapshot)()

    ' Funding rate history ring buffer -- for FundingMomentum computation in Step 3b.
    ' Populated in RunAnalysisAsync after GetFundingRateAsync(); max FundingHistoryMax samples.
    ' Cold start (< 2 samples) returns FLAT from CalcFundingMomentum -- accepted warm-up.
    Private _fundingHistory As New List(Of Double)
    Private Const FundingHistoryMax As Integer = 10

    ' OFI ratio history ring buffer -- for OFIMomentum computation in scoring.
    ' Populated in RunAnalysisAsync after CalcOFI(); max OFIHistoryMax samples.
    ' Cold start (< 2 samples) returns FLAT from CalcOFIMomentum -- accepted warm-up.
    Private _ofiHistory As New List(Of Double)
    Private Const OFIHistoryMax As Integer = 10

    ' Auto-run state
    Private _autoRunTimer   As IAutoRunTimer
    Private _countdownTimer As Threading.Timer
    Private _countdownSecs  As Integer = 0
    Private _intervalMs     As Integer = 60_000

    ' MTF 15m candle TTL cache (P1 upgrade v0.47)
    ' Candles are re-fetched only when the cache is older than MTF_TTL_SECONDS.
    Private Const MTF_TTL_SECONDS As Integer = 60
    Private _mtfCandles15m     As List(Of Candle) = Nothing
    Private _mtfLastFetchTime  As DateTime = DateTime.MinValue

    ' [T1-B] Regime ADX hysteresis: stores the regime label from the previous
    ' run. Used in MainForm_Analysis.vb to apply a 1-bar grace period before
    ' allowing a RANGING flip away from TRENDING_* or TRANSITIONAL.
    Private _prevRegime As String = ""

    ' Resilience: count of skipped analyses this session (transient API failures).
    Private _skipCount As Integer = 0

    ' Raised at the end of every RunAnalysisAsync call (success or skip).
    ' TweakSettingsForm subscribes to this to refresh its status label reactively.
    Public Event AnalysisCompleted As EventHandler

    ' Analysis report link (created programmatically to avoid touching the auto-generated Designer.vb)
    Private WithEvents lnkAnalysisReport As System.Windows.Forms.LinkLabel

    ' Tweak Settings link (status bar, opens TweakSettingsForm non-modally)
    Private WithEvents lnkTweakSettings As System.Windows.Forms.LinkLabel

    ' Reference to the open TweakSettingsForm (Nothing when not open)
    Private _tweakForm As TweakSettingsForm

    ' Output Dump status-bar links
    Private WithEvents lnkOutputDump         As System.Windows.Forms.LinkLabel
    Private WithEvents lnkOutputDumpSettings As System.Windows.Forms.LinkLabel

    ' Reference to the open OutputDumpSettingsForm (Nothing when not open)
    Private _outputDumpSettingsForm As OutputDumpSettingsForm

    ' Live performance strip — six labels (Cur.Wk / 3d / Cur.Day / Asia / London / NY)
    ' plus a mode indicator (lblPerfMode) showing [B]/[T] for current metric mode.
    Private lblPerfMode   As System.Windows.Forms.Label
    Private lblPerfWeek   As System.Windows.Forms.Label
    Private lblPerf3d     As System.Windows.Forms.Label
    Private lblPerfDay    As System.Windows.Forms.Label
    Private lblPerfAsia   As System.Windows.Forms.Label
    Private lblPerfLondon As System.Windows.Forms.Label
    Private lblPerfNy     As System.Windows.Forms.Label
    ' Shared ToolTip for all six perf labels (created once in constructor).
    Private _perfTip As System.Windows.Forms.ToolTip

    ' [target-hit-toggle] In-memory metric mode for the perf strip. Initialised from
    ' settings.json at startup; left-click on any perf label flips it ephemerally
    ' (no settings.json write). Right-click opens a context menu that persists via
    ' SettingsLoader.Save.
    Private _metricMode As String = "barrier"
    Private _perfContextMenu As System.Windows.Forms.ContextMenuStrip

    Private Shared ReadOnly CHAR_PLAY As String = ChrW(9654) & " Start"
    Private Shared ReadOnly CHAR_STOP As String = ChrW(9632) & " Stop"

    ' Colour palette moved to UI/Theme/Theme.vb (P1 of UI reskin).
    ' Use Theme.* tokens directly; P1 keeps hex values pixel-identical.

    ' -----------------------------------------------------------------------
    ' Constructor
    ' -----------------------------------------------------------------------
    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.47 [P1]"

        ' P1 marker: switch txtOutput to Geist Mono so the bundled font is
        ' visibly in use. Confirms P1 build is the one running. Preserves
        ' the existing 10pt size and monospace metric, so layout is unchanged.
        txtOutput.Font = Theme.FontMono(10.0F)

        ' Analysis report link — must be created before the first ResizeControls() call
        lnkAnalysisReport = New System.Windows.Forms.LinkLabel() With {
            .AutoSize         = True,
            .Font             = New System.Drawing.Font("Segoe UI", 8.0!),
            .LinkColor        = System.Drawing.Color.DimGray,
            .ActiveLinkColor  = System.Drawing.Color.DodgerBlue,
            .VisitedLinkColor = System.Drawing.Color.DimGray,
            .Text             = "Analysis Report",
            .TextAlign        = System.Drawing.ContentAlignment.MiddleRight
        }
        Me.Controls.Add(lnkAnalysisReport)

        lnkTweakSettings = New System.Windows.Forms.LinkLabel() With {
            .AutoSize         = True,
            .Font             = New System.Drawing.Font("Segoe UI", 8.0!),
            .LinkColor        = System.Drawing.Color.DimGray,
            .ActiveLinkColor  = System.Drawing.Color.DodgerBlue,
            .VisitedLinkColor = System.Drawing.Color.DimGray,
            .Text             = "Tweak Settings",
            .TextAlign        = System.Drawing.ContentAlignment.MiddleRight
        }
        Me.Controls.Add(lnkTweakSettings)

        lnkOutputDump = New System.Windows.Forms.LinkLabel() With {
            .AutoSize         = True,
            .Font             = New System.Drawing.Font("Segoe UI", 8.0!),
            .LinkColor        = System.Drawing.Color.DimGray,
            .ActiveLinkColor  = System.Drawing.Color.DodgerBlue,
            .VisitedLinkColor = System.Drawing.Color.DimGray,
            .Text             = "Output Dump",
            .TextAlign        = System.Drawing.ContentAlignment.MiddleRight
        }
        Me.Controls.Add(lnkOutputDump)

        lnkOutputDumpSettings = New System.Windows.Forms.LinkLabel() With {
            .AutoSize         = True,
            .Font             = New System.Drawing.Font("Segoe UI", 8.0!),
            .LinkColor        = System.Drawing.Color.DimGray,
            .ActiveLinkColor  = System.Drawing.Color.DodgerBlue,
            .VisitedLinkColor = System.Drawing.Color.DimGray,
            .Text             = ChrW(&H2699),
            .TextAlign        = System.Drawing.ContentAlignment.MiddleRight
        }
        Me.Controls.Add(lnkOutputDumpSettings)

        ' Live performance strip labels — created here, positioned in ResizeControls().
        ' Colour is applied per-run by UpdatePerformanceLabels().
        lblPerfMode   = MakePerfLabel("[B]")
        lblPerfWeek   = MakePerfLabel("Cur.Wk: --%")
        lblPerf3d     = MakePerfLabel("3d: --%")
        lblPerfDay    = MakePerfLabel("Cur.Day: --%")
        lblPerfAsia   = MakePerfLabel("Asia: --%")
        lblPerfLondon = MakePerfLabel("London: --%")
        lblPerfNy     = MakePerfLabel("NY: --%")
        _perfTip = New System.Windows.Forms.ToolTip()
        Me.Controls.Add(lblPerfMode)
        For Each lbl In New System.Windows.Forms.Label() {
                lblPerfWeek, lblPerf3d, lblPerfDay,
                lblPerfAsia, lblPerfLondon, lblPerfNy}
            Me.Controls.Add(lbl)
        Next

        ' [target-hit-toggle] Wire left-click (ephemeral toggle) + right-click (persist
        ' via context menu) onto each of the six rate labels. _metricMode is finalised
        ' from settings.json below, after SettingsLoader.Initialise.
        _perfContextMenu = New System.Windows.Forms.ContextMenuStrip()
        Dim miBarrier = _perfContextMenu.Items.Add("Use barrier metric")
        Dim miTarget  = _perfContextMenu.Items.Add("Use target metric")
        AddHandler miBarrier.Click, Sub(s, ev) PersistMetricMode("barrier")
        AddHandler miTarget.Click,  Sub(s, ev) PersistMetricMode("target")
        For Each lbl In New System.Windows.Forms.Label() {
                lblPerfWeek, lblPerf3d, lblPerfDay,
                lblPerfAsia, lblPerfLondon, lblPerfNy}
            AddHandler lbl.MouseDown, AddressOf PerfLabel_MouseDown
        Next

        SetOutputMargins(6, 6)
        AddHandler Me.Resize, Sub(s As Object, ev As EventArgs) ResizeControls()
        AddHandler Me.HandleCreated, AddressOf OnFormHandleCreated
        ResizeControls()
        UpdateLogInfo()
        SettingsLoader.Initialise(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json"))
        ' [target-hit-toggle] Now that settings.json has been read, pick up its metric_mode.
        _metricMode = NormaliseMode(SettingsLoader.Current.PerformanceDisplay.MetricMode)
        InitAutoRunControls()

        ' Size the window to fit content exactly, based on actual font metrics.
        SizeToContent()

        ' Async fire-and-forget: load/backfill performance caches before first analysis.
        ' Runs on a background thread; UpdateAsync awaits the init task so there's no race.
        If SettingsLoader.Current.PerformanceDisplay.Enabled Then
            lblLogInfo.Text = "Loading performance history..."
            Dim evalPath As String = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis_eval_cache.csv")
            Dim ohlcPath As String = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ohlc_1m_cache.csv")
            Dim logPath  As String = AnalysisLogger.GetLogPath()
            Dim cfg      As EngineSettings = SettingsLoader.Current
            Task.Run(Async Function()
                Dim fetcher As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar))) =
                    Async Function(startDt As DateTime, endDt As DateTime) As Task(Of List(Of OhlcBar))
                        Dim startMs As Long = New DateTimeOffset(DateTime.SpecifyKind(startDt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                        Dim endMs   As Long = New DateTimeOffset(DateTime.SpecifyKind(endDt,   DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                        Dim candles = Await DeribitClient.GetCandlesAsync("1", startMs, endMs)
                        If candles Is Nothing Then Return New List(Of OhlcBar)()
                        Return candles.Select(Function(c) LivePerformanceTracker.CandleToBar(c)).ToList()
                    End Function
                Dim statusCb As Action(Of String) =
                    Sub(msg As String)
                        If IsDisposed Then Return
                        Try
                            Me.Invoke(Sub() lblLogInfo.Text = msg)
                        Catch
                            ' Form disposed mid-update; ignore.
                        End Try
                    End Sub
                Dim summary As String = Await LivePerformanceTracker.InitialiseAsync(
                    evalPath, ohlcPath, logPath, cfg,
                    cfg.PerformanceDisplay.EagerBackfillOnStartup, fetcher,
                    statusCb)
                Console.WriteLine("[LivePerformanceTracker] " & summary)
                If Not IsDisposed Then
                    Me.Invoke(Sub()
                        UpdateLogInfo()
                        UpdatePerformanceLabels()
                    End Sub)
                End If
            End Function)
        End If
    End Sub

    ' -----------------------------------------------------------------------
    ' Compute and apply the ideal window size from font metrics
    ' -----------------------------------------------------------------------
    Private Sub SizeToContent()
        Dim font As Font = txtOutput.Font

        ' Measure a single character width using a reference string.
        ' RichTextBox uses a monospace font so all chars are the same width.
        Dim charSize As SizeF
        Using g As Graphics = txtOutput.CreateGraphics()
            ' MeasureString adds padding; use MeasureCharacterRanges for accuracy.
            Dim fmt As New StringFormat()
            fmt.SetMeasurableCharacterRanges(
                New CharacterRange() {New CharacterRange(0, 1)})
            Dim regions = g.MeasureCharacterRanges("W", font, New RectangleF(0, 0, 1000, 1000), fmt)
            charSize = regions(0).GetBounds(g).Size
        End Using

        Dim charW As Integer = CInt(Math.Ceiling(charSize.Width))
        Dim lineH As Integer = CInt(Math.Ceiling(font.GetHeight()))

        ' --- Client width ---
        ' Left margin(8) + right margin(8) + inner text margins(6+6) +
        ' scrollbar(17) + chars + 4px safety buffer
        Const SCROLLBAR_W As Integer = 17
        Const MARGIN_W    As Integer = 8 + 8 + 6 + 6 + SCROLLBAR_W + 4
        Dim idealClientW As Integer = charW * OUTPUT_CHARS + MARGIN_W

        ' --- Client height ---
        ' Header(TXT_Y) + output lines + status bar + 4px safety buffer
        Const MARGIN_H As Integer = 4
        Dim idealClientH As Integer = TXT_Y + lineH * OUTPUT_LINES + STATUS_H + MARGIN_H

        ' --- Window (non-client) chrome ---
        Dim chromeW As Integer = Me.Width  - Me.ClientSize.Width
        Dim chromeH As Integer = Me.Height - Me.ClientSize.Height

        Dim idealW As Integer = idealClientW + chromeW
        Dim idealH As Integer = idealClientH + chromeH

        ' --- Cap against working area so we never overflow the screen ---
        Dim wa As Rectangle = Screen.FromControl(Me).WorkingArea
        idealW = Math.Min(idealW, wa.Width)
        idealH = Math.Min(idealH, wa.Height)

        Me.MinimumSize = New System.Drawing.Size(idealW, idealH)
        Me.Size        = New System.Drawing.Size(idealW, idealH)
        Me.Location    = New System.Drawing.Point(
            wa.Left + (wa.Width  - idealW) \ 2,
            wa.Top  + (wa.Height - idealH) \ 2)
    End Sub

    ' -----------------------------------------------------------------------
    ' NUD digit vertical centring
    ' -----------------------------------------------------------------------
    Private Sub OnFormHandleCreated(sender As Object, e As EventArgs)
        CentreNudText(nudMinutes)
        CentreNudText(nudSeconds)
    End Sub

    Private Shared Sub CentreNudText(nud As NumericUpDown)
        If nud.Controls.Count = 0 Then Return
        Dim innerTb As Control = nud.Controls(0)
        If Not innerTb.IsHandleCreated Then innerTb.CreateControl()

        Const SPIN_W  As Integer = 16
        Const TOP_PAD As Integer = 3
        Dim h As Integer = innerTb.Height

        Dim rc As RECT
        rc.Left   = 1
        rc.Top    = TOP_PAD
        rc.Right  = innerTb.Width - SPIN_W - 2
        rc.Bottom = h - 1
        SendMessage(innerTb.Handle, EM_SETRECTNP, 0, rc)
    End Sub

    ' -----------------------------------------------------------------------
    ' Layout / resize
    ' -----------------------------------------------------------------------
    Private Sub SetOutputMargins(leftPx As Integer, rightPx As Integer)
        Dim lParam As Integer = (rightPx << 16) Or (leftPx And &HFFFF)
        SendMessage(txtOutput.Handle, EM_SETMARGINS, EC_LEFTMARGIN Or EC_RIGHTMARGIN, lParam)
    End Sub

    Private Sub ResizeControls()
        Dim W As Integer = Me.ClientSize.Width
        Dim H As Integer = Me.ClientSize.Height

        ' Row 1 -- position / analyze / verdict
        lblPositionTitle.Location = New System.Drawing.Point(8, HDR_Y)
        lblPositionTitle.Size     = New System.Drawing.Size(108, HDR_H)
        rbNone.Location  = New System.Drawing.Point(120, HDR_Y + (HDR_H - 18) \ 2)
        rbLong.Location  = New System.Drawing.Point(210, HDR_Y + 2)
        rbShort.Location = New System.Drawing.Point(210, HDR_Y + 22)
        btnAnalyze.Location = New System.Drawing.Point(BTN_X, HDR_Y)
        btnAnalyze.Size     = New System.Drawing.Size(BTN_W, HDR_H)
        lblVerdict.Location = New System.Drawing.Point(VRD_X, HDR_Y)
        lblVerdict.Size     = New System.Drawing.Size(W - VRD_X - 8, HDR_H)

        ' Row 2 -- auto-run controls
        lblAutoRun.Location   = New System.Drawing.Point(8, AR_Y)
        lblAutoRun.Size       = New System.Drawing.Size(78, AR_H)
        nudMinutes.Location   = New System.Drawing.Point(90, AR_Y)
        nudMinutes.Size       = New System.Drawing.Size(42, AR_H)
        lblMin.Location       = New System.Drawing.Point(136, AR_Y + 3)
        nudSeconds.Location   = New System.Drawing.Point(164, AR_Y)
        nudSeconds.Size       = New System.Drawing.Size(42, AR_H)
        lblSec.Location       = New System.Drawing.Point(210, AR_Y + 3)
        pnlMode.Location      = New System.Drawing.Point(242, AR_Y)
        pnlMode.Size          = New System.Drawing.Size(134, AR_H)
        rbSingle.Location     = New System.Drawing.Point(0, 2)
        rbRepeat.Location     = New System.Drawing.Point(68, 2)
        btnStartStop.Location = New System.Drawing.Point(SS_X, AR_Y - 1)
        btnStartStop.Size     = New System.Drawing.Size(70, AR_H + 2)

        ' Row 2 continued -- performance strip (right of Start/Stop button, same row)
        ' Labels are AutoSize so we cascade X positions based on measured widths.
        Const PERF_SEP As Integer = 8  ' gap between strip labels
        Dim perfY As Integer = AR_Y + (AR_H - 14) \ 2  ' vertically centred on row 2
        Dim perfX As Integer = SS_X + 70 + PERF_SEP     ' start just past btnStartStop
        Dim perfLabels = New System.Windows.Forms.Label() {
            lblPerfMode, lblPerfWeek, lblPerf3d, lblPerfDay, lblPerfAsia, lblPerfLondon, lblPerfNy}
        For Each lbl In perfLabels
            If lbl Is Nothing Then Continue For
            lbl.Location = New System.Drawing.Point(perfX, perfY)
            lbl.Update()
            perfX += lbl.Width + PERF_SEP
        Next

        ' Output textbox
        Dim statusY As Integer = H - STATUS_H - 2
        txtOutput.Location = New System.Drawing.Point(8, TXT_Y)
        txtOutput.Size     = New System.Drawing.Size(W - 16, statusY - TXT_Y - 2)
        SetOutputMargins(6, 6)

        ' Status bar — cascade right-to-left using measured AutoSize widths so
        ' nothing overlaps. Previous fixed W-N offsets put lnkAnalysisReport entirely
        ' inside lblCountdown's span (invisible) and chained other link overlaps.
        Const STATUS_GAP    As Integer = 12
        Const COUNTDOWN_W   As Integer = 170
        Const RIGHT_MARGIN  As Integer = 8
        Const LEFT_MARGIN   As Integer = 8
        Dim   sbY           As Integer = H - STATUS_H   ' status-bar Y (top edge)

        ' Force AutoSize labels to remeasure (defensive — text may have changed).
        For Each lnk In New System.Windows.Forms.LinkLabel() {
                lnkResetLog, lnkTweakSettings, lnkCalibCheck, lnkAnalysisReport,
                lnkOutputDumpSettings, lnkOutputDump}
            If lnk IsNot Nothing Then lnk.PerformLayout()
        Next

        Dim rightX As Integer = W - RIGHT_MARGIN

        ' Right-side links (rightmost first): Reset Log, Tweak Settings,
        ' Calibration Readiness, Analysis Report
        rightX -= lnkResetLog.Width
        lnkResetLog.Location = New System.Drawing.Point(rightX, sbY)

        rightX -= STATUS_GAP + lnkTweakSettings.Width
        lnkTweakSettings.Location = New System.Drawing.Point(rightX, sbY)

        rightX -= STATUS_GAP + lnkCalibCheck.Width
        lnkCalibCheck.Location = New System.Drawing.Point(rightX, sbY)

        rightX -= STATUS_GAP + lnkAnalysisReport.Width
        lnkAnalysisReport.Location = New System.Drawing.Point(rightX, sbY)

        ' Middle: countdown / auto-run status (fixed width)
        rightX -= STATUS_GAP + COUNTDOWN_W
        lblCountdown.Location = New System.Drawing.Point(rightX, sbY)
        lblCountdown.Size     = New System.Drawing.Size(COUNTDOWN_W, STATUS_H)

        ' Left-of-countdown links: Output Dump Settings (gear), then Output Dump text
        rightX -= STATUS_GAP + lnkOutputDumpSettings.Width
        lnkOutputDumpSettings.Location = New System.Drawing.Point(rightX, sbY)

        rightX -= 4 + lnkOutputDump.Width   ' tight gap between text and icon
        lnkOutputDump.Location = New System.Drawing.Point(rightX, sbY)

        ' Log info fills the remaining left-side space
        lblLogInfo.Location = New System.Drawing.Point(LEFT_MARGIN, sbY)
        lblLogInfo.Size     = New System.Drawing.Size(
            Math.Max(0, rightX - LEFT_MARGIN - STATUS_GAP), STATUS_H)
    End Sub

    ' -----------------------------------------------------------------------
    ' Live performance strip helpers
    ' -----------------------------------------------------------------------

    ''' <summary>Factory for the six performance strip labels.</summary>
    Private Shared Function MakePerfLabel(initialText As String) As System.Windows.Forms.Label
        Return New System.Windows.Forms.Label() With {
            .AutoSize  = True,
            .Font      = New System.Drawing.Font("Segoe UI", 8.0!),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = System.Drawing.Color.Transparent,
            .Text      = initialText
        }
    End Function

    ''' <summary>
    ''' Recompute the six performance windows and update label text + colour.
    ''' Honours _metricMode ("barrier" | "target") for which numerator to show.
    ''' Tooltip carries the other metric's rate on a second line so both are
    ''' visible without toggling. Must be called on the UI thread.
    ''' </summary>
    Friend Sub UpdatePerformanceLabels()
        Dim cfg As EngineSettings = SettingsLoader.Current
        If Not cfg.PerformanceDisplay.Enabled Then
            For Each lbl In New System.Windows.Forms.Label() {
                    lblPerfMode, lblPerfWeek, lblPerf3d, lblPerfDay,
                    lblPerfAsia, lblPerfLondon, lblPerfNy}
                If lbl IsNot Nothing Then lbl.Visible = False
            Next
            Return
        End If

        Dim isTarget As Boolean = (_metricMode = "target")
        Dim defaultMode As String = NormaliseMode(cfg.PerformanceDisplay.MetricMode)

        ' Mode indicator: dim when ephemeral mode matches settings.json default; amber otherwise.
        If lblPerfMode IsNot Nothing Then
            lblPerfMode.Text      = If(isTarget, "[T]", "[B]")
            lblPerfMode.ForeColor = If(_metricMode = defaultMode, Theme.FG_QUATERNARY, Theme.ACC_WARN)
            lblPerfMode.Visible   = True
        End If

        Dim windows = LivePerformanceTracker.ComputeWindows(DateTime.UtcNow, cfg)
        Dim prefixes() As String = {"Cur.Wk", "3d", "Cur.Day", "Asia", "London", "NY"}
        Dim labels() As System.Windows.Forms.Label = {
            lblPerfWeek, lblPerf3d, lblPerfDay, lblPerfAsia, lblPerfLondon, lblPerfNy}
        Dim minN As Integer = cfg.PerformanceDisplay.MinSampleForRender

        For i As Integer = 0 To 5
            Dim lbl = labels(i)
            If lbl Is Nothing Then Continue For
            Dim w = If(i < windows.Count, windows(i), Nothing)
            Dim n   As Integer = If(w IsNot Nothing, w.SuccessCount + w.FailureCount, 0)
            Dim tip As String  = ""
            Dim text As String
            Dim fgColor As System.Drawing.Color

            If w IsNot Nothing AndAlso n >= minN Then
                Dim activePct As Double = If(isTarget, w.TargetRatePct, w.BarrierRatePct)
                Dim rate As Integer = CInt(Math.Round(activePct))
                text    = prefixes(i) & ": " & rate.ToString() & "%"
                fgColor = If(rate > 50, Theme.ACC_STRONG_LONG, Theme.ACC_SHORT)

                ' Tooltip line 1: sample summary; line 2: the other metric's rate.
                Dim otherLabel As String = If(isTarget, "Barrier-hit", "Target-hit")
                Dim otherHits  As Integer = If(isTarget, w.SuccessCount, w.TargetHitCount)
                Dim otherPct   As Double  = If(isTarget, w.BarrierRatePct, w.TargetRatePct)
                tip = String.Format("{0} predictions evaluated. {1:yyyy-MM-dd HH:mm} → {2:yyyy-MM-dd HH:mm} UTC+8." &
                                    Environment.NewLine &
                                    "{3}: {4}% ({5}/{6})",
                                    n, w.RangeStart, w.RangeEnd,
                                    otherLabel, CInt(Math.Round(otherPct)), otherHits, n)
            Else
                text    = prefixes(i) & ": --%"
                fgColor = Theme.FG_QUATERNARY
                If w IsNot Nothing Then
                    tip = String.Format("{0} predictions evaluated (below threshold). {1:yyyy-MM-dd HH:mm} → {2:yyyy-MM-dd HH:mm} UTC+8.",
                                        n, w.RangeStart, w.RangeEnd)
                End If
            End If

            lbl.Text      = text
            lbl.ForeColor = fgColor
            lbl.Visible   = True
            _perfTip.SetToolTip(lbl, tip)
        Next

        ' Re-run layout so cascaded X positions reflect updated text widths.
        ResizeControls()
    End Sub

    ''' <summary>
    ''' [target-hit-toggle] Coerce arbitrary settings.json values to a known mode
    ''' string. Anything other than "target" is treated as "barrier".
    ''' </summary>
    Private Shared Function NormaliseMode(raw As String) As String
        If raw IsNot Nothing AndAlso raw.Trim().Equals("target", StringComparison.OrdinalIgnoreCase) Then
            Return "target"
        End If
        Return "barrier"
    End Function

    ''' <summary>
    ''' [target-hit-toggle] Left-click flips mode ephemerally (no settings.json write).
    ''' Right-click opens a context menu that persists via SettingsLoader.Save.
    ''' </summary>
    Private Sub PerfLabel_MouseDown(sender As Object, e As System.Windows.Forms.MouseEventArgs)
        If e.Button = System.Windows.Forms.MouseButtons.Left Then
            _metricMode = If(_metricMode = "target", "barrier", "target")
            UpdatePerformanceLabels()
            lblLogInfo.Text = String.Format("Metric mode → {0} (right-click any label to persist)", _metricMode)
        ElseIf e.Button = System.Windows.Forms.MouseButtons.Right Then
            Dim lbl = TryCast(sender, System.Windows.Forms.Label)
            If lbl IsNot Nothing Then _perfContextMenu.Show(lbl, e.Location)
        End If
    End Sub

    ''' <summary>
    ''' [target-hit-toggle] Write the new metric mode to settings.json. Also updates
    ''' the in-memory ephemeral state so the mode indicator returns to dim (matches default).
    ''' </summary>
    Private Sub PersistMetricMode(mode As String)
        mode = NormaliseMode(mode)
        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.PerformanceDisplay.MetricMode = mode
        SettingsLoader.Save(cfg, "performance_display.metric_mode → " & mode)
        _metricMode = mode
        UpdatePerformanceLabels()
        lblLogInfo.Text = "Metric mode persisted → " & mode
    End Sub

    ' -----------------------------------------------------------------------
    ' Tweak Settings link click
    ' -----------------------------------------------------------------------
    Private Sub lnkTweakSettings_LinkClicked(sender As Object,
            e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) _
            Handles lnkTweakSettings.LinkClicked
        If _tweakForm Is Nothing OrElse _tweakForm.IsDisposed Then
            _tweakForm = New TweakSettingsForm(Me)
            AddHandler _tweakForm.FormClosed,
                Sub(s As Object, ev As System.Windows.Forms.FormClosedEventArgs)
                    _tweakForm = Nothing
                End Sub
        End If
        _tweakForm.Show()
        _tweakForm.BringToFront()
    End Sub

    ' -----------------------------------------------------------------------
    ' Output Dump helpers + link clicks
    ' -----------------------------------------------------------------------
    Friend Shared Function GetDumpPath() As String
        Return IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis_output_dump.md")
    End Function

    Private Sub lnkOutputDump_LinkClicked(sender As Object,
            e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) _
            Handles lnkOutputDump.LinkClicked
        Dim dumpPath As String = GetDumpPath()
        If Not IO.File.Exists(dumpPath) OrElse Not SettingsLoader.Current.AnalysisLogging.OutputDumpEnabled Then
            MessageBox.Show("Output dump is empty or disabled.", "Output Dump",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Dim psi As New System.Diagnostics.ProcessStartInfo(dumpPath) With {.UseShellExecute = True}
            System.Diagnostics.Process.Start(psi)
        Catch ex As Exception
            MessageBox.Show("Could not open dump file: " & ex.Message, "Output Dump",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub lnkOutputDumpSettings_LinkClicked(sender As Object,
            e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) _
            Handles lnkOutputDumpSettings.LinkClicked
        If _outputDumpSettingsForm Is Nothing OrElse _outputDumpSettingsForm.IsDisposed Then
            _outputDumpSettingsForm = New OutputDumpSettingsForm(GetDumpPath())
            AddHandler _outputDumpSettingsForm.FormClosed,
                Sub(s As Object, ev As System.Windows.Forms.FormClosedEventArgs)
                    _outputDumpSettingsForm = Nothing
                End Sub
        End If
        _outputDumpSettingsForm.Show()
        _outputDumpSettingsForm.BringToFront()
    End Sub

End Class
