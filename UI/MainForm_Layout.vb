' UI/MainForm_Layout.vb
' UI reskin P4a — card-grid layout skeleton.
'
' Replaces the pixel-positioned ResizeControls() / SizeToContent() approach
' with a TableLayoutPanel hosting RoundedCardPanel rows. Existing Designer
' controls (position radios, ANALYZE button, NUDs, perf labels, status-bar
' links, txtOutput) are reparented from Me.Controls into their new card
' homes; per-control styling that previously lived in ApplyDesignerOverrides
' is folded into BuildCardGridLayout at reparent time.
'
' Rows 1-5 + 10 are populated in P4a; rows 6-9 and 11 are empty placeholders
' bound in P4c/P4d/P4e. Row 10 (verification dump) houses the legacy
' txtOutput so side-by-side parity can be eyeballed against the new bound
' cards above. P5 deletes txtOutput and row 10 entirely.

Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Shared fields (used by multiple partials)
    ' -----------------------------------------------------------------------
    Private _oiHistory As New List(Of OiSnapshot)()

    ' Funding rate history ring buffer -- for FundingMomentum computation in Step 3b.
    Private _fundingHistory As New List(Of Double)
    Private Const FundingHistoryMax As Integer = 10

    ' OFI ratio history ring buffer -- for OFIMomentum computation in scoring.
    Private _ofiHistory As New List(Of Double)
    Private Const OFIHistoryMax As Integer = 10

    ' Auto-run state
    Private _autoRunTimer   As IAutoRunTimer
    Private _countdownTimer As Threading.Timer
    Private _countdownSecs  As Integer = 0
    Private _intervalMs     As Integer = 60_000

    ' MTF 15m candle TTL cache (P1 upgrade v0.47)
    Private Const MTF_TTL_SECONDS As Integer = 60
    Private _mtfCandles15m     As List(Of Candle) = Nothing
    Private _mtfLastFetchTime  As DateTime = DateTime.MinValue

    ' [T1-B] Regime ADX hysteresis state.
    Private _prevRegime As String = ""

    ' Resilience: count of skipped analyses this session.
    Private _skipCount As Integer = 0

    ' Raised at the end of every RunAnalysisAsync call (success or skip).
    Public Event AnalysisCompleted As EventHandler

    ' Status-bar / SETTINGS & TOOLS links — created programmatically so the
    ' auto-generated Designer.vb stays untouched.
    Private WithEvents lnkAnalysisReport      As System.Windows.Forms.LinkLabel
    Private WithEvents lnkTweakSettings       As System.Windows.Forms.LinkLabel
    Private WithEvents lnkOutputDump          As System.Windows.Forms.LinkLabel
    Private WithEvents lnkOutputDumpSettings  As System.Windows.Forms.LinkLabel

    Private _tweakForm              As TweakSettingsForm
    Private _outputDumpSettingsForm As OutputDumpSettingsForm

    ' Live performance strip labels.
    Private lblPerfMode   As System.Windows.Forms.Label
    Private lblPerfWeek   As System.Windows.Forms.Label
    Private lblPerf3d     As System.Windows.Forms.Label
    Private lblPerfDay    As System.Windows.Forms.Label
    Private lblPerfAsia   As System.Windows.Forms.Label
    Private lblPerfLondon As System.Windows.Forms.Label
    Private lblPerfNy     As System.Windows.Forms.Label
    Private _perfTip      As System.Windows.Forms.ToolTip

    Private _metricMode      As String = "barrier"
    Private _perfContextMenu As System.Windows.Forms.ContextMenuStrip

    Private Shared ReadOnly CHAR_PLAY As String = ChrW(9654) & " Start"
    Private Shared ReadOnly CHAR_STOP As String = ChrW(9632) & " Stop"

    ' -----------------------------------------------------------------------
    ' P4a — card grid roots
    ' -----------------------------------------------------------------------
    Private _gridRoot              As TableLayoutPanel

    Private _cardHeaderStrip       As RoundedCardPanel
    Private _cardPerfStrip         As RoundedCardPanel
    Private _cardScore             As RoundedCardPanel
    Private _cardVerdict           As RoundedCardPanel
    Private _cardLastPrice         As RoundedCardPanel
    Private _cardAtrLevels         As RoundedCardPanel
    Private _cardStructLong        As RoundedCardPanel
    Private _cardStructShort       As RoundedCardPanel
    Private _cardSignalBreakdown   As RoundedCardPanel
    Private _cardOiCvdCross        As RoundedCardPanel
    Private _cardVolumeProfile     As RoundedCardPanel
    Private _cardKelly             As RoundedCardPanel
    Private _cardDynamicNorms      As RoundedCardPanel
    Private _cardVerificationDump  As RoundedCardPanel    ' P5 deletes
    Private _cardSettingsTools     As RoundedCardPanel    ' P4e binds

    ' Custom-control fields populated by the P4b binding methods.
    ' Declared here so MainForm_Render_Cards.vb can reference them without
    ' a second declaration site.
    Friend _scoreArc          As ScoreArcGauge
    Friend _lblScoreConfidence As Label
    Friend _lblVerdictText    As Label
    Friend _contextBadge      As ContextBadge
    Friend _lblRegime         As Label
    Friend _mtfRow            As MtfRow
    Friend _lblHold           As Label
    Friend _regimeAnchorWarn  As RegimeAnchorWarn
    Friend _lblLastPrice      As Label
    Friend _lblLastPriceAtr   As Label
    Friend _lblLastPriceTime  As Label
    Friend _lblLastPriceSession As Label
    Friend _atrStopValue      As Label
    Friend _atrRRValue        As Label
    Friend _atrEntryValue     As Label
    Friend _atrCappedValue    As Label
    Friend _atrTargetValue    As Label
    Friend _atrCapReason      As Label
    Friend _structLongCtrls   As StructuralCardControls
    Friend _structShortCtrls  As StructuralCardControls
    Friend _analyzeButton     As FlatButton

    ''' <summary>
    ''' Holds the controls inside a STRUCTURAL card so P4b binding code can
    ''' reach them without four parallel label fields.
    ''' </summary>
    Friend Class StructuralCardControls
        Public Property StopValue   As Label
        Public Property TargetValue As Label
        Public Property EntryValue  As Label
        Public Property RRValue     As Label
    End Class

    ' -----------------------------------------------------------------------
    ' Constructor
    ' -----------------------------------------------------------------------
    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.47 [P4]"
        Me.BackColor = Theme.BG_BASE

        ' Force the form's own background to the design base; the card grid
        ' fills the client area but the underlying form colour shows briefly
        ' during paint and any 1px gap.
        txtOutput.Font = Theme.FontMono(10.0F)

        ' Performance-strip labels — created here (added into the card grid
        ' inside BuildLivePerformanceStrip during P4b binding).
        lblPerfMode   = MakePerfLabel("[B]")
        lblPerfWeek   = MakePerfLabel("Cur.Wk: --%")
        lblPerf3d     = MakePerfLabel("3d: --%")
        lblPerfDay    = MakePerfLabel("Cur.Day: --%")
        lblPerfAsia   = MakePerfLabel("Asia: --%")
        lblPerfLondon = MakePerfLabel("London: --%")
        lblPerfNy     = MakePerfLabel("NY: --%")
        _perfTip = New System.Windows.Forms.ToolTip()

        ' Right-click context menu + left-click ephemeral toggle wiring.
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

        ' Status-bar / SETTINGS & TOOLS link labels — created here, parented
        ' inside BuildSettingsAndToolsCard during P4e. For P4a they live in
        ' the SETTINGS & TOOLS placeholder card so click handlers work.
        lnkAnalysisReport      = MakeLinkLabel("Analysis Report")
        lnkTweakSettings       = MakeLinkLabel("Tweak Settings")
        lnkOutputDump          = MakeLinkLabel("Output Dump")
        lnkOutputDumpSettings  = MakeLinkLabel(ChrW(&H2699))

        ' Apply the design palette to the Designer-set controls before we
        ' reparent them into cards. ApplyDesignerOverrides() is retired in
        ' P4a — its styling work happens here inline so each card owns the
        ' appearance of the controls it hosts.
        ApplyControlThemes()

        ' Build the card-grid layout. After this returns, every Designer
        ' control we care about has been reparented from Me.Controls into
        ' one of the card panels.
        BuildCardGridLayout()

        SettingsLoader.Initialise(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json"))
        _metricMode = NormaliseMode(SettingsLoader.Current.PerformanceDisplay.MetricMode)
        InitAutoRunControls()

        UpdateLogInfo()

        ApplyInitialFormSize()

        ' Async fire-and-forget: load/backfill performance caches before first analysis.
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
    ' Apply design palette to Designer-set controls
    ' (replaces the old ApplyDesignerOverrides pass; runs before reparent)
    ' -----------------------------------------------------------------------
    Private Sub ApplyControlThemes()
        txtOutput.BackColor  = Theme.BG_BASE
        txtOutput.ForeColor  = Theme.FG_PRIMARY
        txtOutput.BorderStyle = BorderStyle.None

        lblPositionTitle.ForeColor = Theme.FG_TERTIARY
        lblPositionTitle.BackColor = Color.Transparent
        rbNone.ForeColor  = Theme.FG_PRIMARY
        rbNone.BackColor  = Color.Transparent
        rbLong.ForeColor  = Theme.ACC_STRONG_LONG
        rbLong.BackColor  = Color.Transparent
        rbShort.ForeColor = Theme.ACC_SHORT
        rbShort.BackColor = Color.Transparent

        btnAnalyze.BackColor = Theme.ACC_CTA
        btnAnalyze.ForeColor = Theme.FG_INK
        btnAnalyze.Font      = Theme.FontMono(11.0F, FontStyle.Bold)
        btnAnalyze.FlatStyle = FlatStyle.Flat
        btnAnalyze.FlatAppearance.BorderColor = Theme.ACC_CTA

        lblVerdict.BackColor = Theme.BG_CARD
        lblVerdict.ForeColor = Theme.FG_PRIMARY
        lblVerdict.Font      = Theme.FontMono(16.0F, FontStyle.Bold)

        lblAutoRun.ForeColor   = Theme.FG_TERTIARY
        lblAutoRun.BackColor   = Color.Transparent
        nudMinutes.BackColor   = Theme.BG_CARD_RAISED
        nudMinutes.ForeColor   = Theme.FG_PRIMARY
        lblMin.ForeColor       = Theme.FG_TERTIARY
        lblMin.BackColor       = Color.Transparent
        nudSeconds.BackColor   = Theme.BG_CARD_RAISED
        nudSeconds.ForeColor   = Theme.FG_PRIMARY
        lblSec.ForeColor       = Theme.FG_TERTIARY
        lblSec.BackColor       = Color.Transparent
        pnlMode.BackColor      = Color.Transparent
        rbSingle.ForeColor     = Theme.FG_TERTIARY
        rbSingle.BackColor     = Color.Transparent
        rbRepeat.ForeColor     = Theme.FG_TERTIARY
        rbRepeat.BackColor     = Color.Transparent
        btnStartStop.BackColor = Theme.ACC_STRONG_LONG
        btnStartStop.ForeColor = Theme.FG_INK
        btnStartStop.FlatStyle = FlatStyle.Flat
        btnStartStop.FlatAppearance.BorderColor = Theme.ACC_STRONG_LONG

        lblLogInfo.ForeColor   = Theme.FG_TERTIARY
        lblLogInfo.BackColor   = Color.Transparent
        lblCountdown.ForeColor = Theme.FG_TERTIARY
        lblCountdown.BackColor = Color.Transparent

        Dim links() As System.Windows.Forms.LinkLabel = {
            lnkResetLog, lnkCalibCheck, lnkAnalysisReport,
            lnkTweakSettings, lnkOutputDump, lnkOutputDumpSettings}
        For Each lnk In links
            If lnk Is Nothing Then Continue For
            lnk.LinkColor        = Theme.FG_TERTIARY
            lnk.ActiveLinkColor  = Theme.ACC_INFO
            lnk.VisitedLinkColor = Theme.FG_TERTIARY
            lnk.BackColor        = Color.Transparent
            Dim hoverColour As Color = If(lnk Is lnkResetLog, Theme.ACC_SHORT, Theme.ACC_INFO)
            Dim captured = lnk
            AddHandler captured.MouseEnter, Sub(s, ev) captured.LinkColor = hoverColour
            AddHandler captured.MouseLeave, Sub(s, ev) captured.LinkColor = Theme.FG_TERTIARY
        Next
    End Sub

    Private Shared Function MakeLinkLabel(text As String) As System.Windows.Forms.LinkLabel
        Return New System.Windows.Forms.LinkLabel() With {
            .AutoSize = True,
            .Font = New System.Drawing.Font("Segoe UI", 8.0!),
            .Text = text,
            .TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        }
    End Function

    ' -----------------------------------------------------------------------
    ' BuildCardGridLayout — create the TableLayoutPanel + 11 card rows
    ' -----------------------------------------------------------------------
    Private Sub BuildCardGridLayout()
        _gridRoot = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .BackColor = Theme.BG_BASE,
            .Padding = New Padding(8),
            .AutoSize = False,
            .AutoScroll = True
        }
        _gridRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        ' Row 1: header strip
        _cardHeaderStrip = NewCard()
        AddRow(_cardHeaderStrip, 60)
        ReparentHeaderStripControls()

        ' Row 2: live performance strip
        _cardPerfStrip = NewCard()
        AddRow(_cardPerfStrip, 40)
        ReparentPerfStripControls()

        ' Row 3: hero — SCORE / VERDICT / LAST PRICE side by side
        Dim heroRow = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1,
            .BackColor = Theme.BG_BASE, .AutoSize = False,
            .Margin = New Padding(0, 0, 0, 8)
        }
        heroRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200))
        heroRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        heroRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200))
        _cardScore     = NewCard()
        _cardVerdict   = NewCard()
        _cardLastPrice = NewCard()
        For Each c In New Control() {_cardScore, _cardVerdict, _cardLastPrice}
            c.Dock = DockStyle.Fill
            c.Margin = New Padding(0, 0, 8, 0)
        Next
        _cardLastPrice.Margin = New Padding(0)
        heroRow.Controls.Add(_cardScore, 0, 0)
        heroRow.Controls.Add(_cardVerdict, 1, 0)
        heroRow.Controls.Add(_cardLastPrice, 2, 0)
        AddRow(heroRow, 160)

        ' Row 4: ATR ENTRY LEVELS (P4b binds contents)
        _cardAtrLevels = NewCard()
        AddRow(_cardAtrLevels, 110)

        ' Row 5: STRUCTURAL LONG + STRUCTURAL SHORT side by side
        Dim structRow = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1,
            .BackColor = Theme.BG_BASE, .AutoSize = False,
            .Margin = New Padding(0, 0, 0, 8)
        }
        structRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        structRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        _cardStructLong  = NewCard()
        _cardStructShort = NewCard()
        _cardStructLong.Dock  = DockStyle.Fill
        _cardStructShort.Dock = DockStyle.Fill
        _cardStructLong.Margin  = New Padding(0, 0, 8, 0)
        _cardStructShort.Margin = New Padding(0)
        structRow.Controls.Add(_cardStructLong, 0, 0)
        structRow.Controls.Add(_cardStructShort, 1, 0)
        AddRow(structRow, 110)

        ' Row 6: SIGNAL BREAKDOWN (placeholder — P4c)
        _cardSignalBreakdown = NewCard()
        AddRow(_cardSignalBreakdown, 440)
        AddPlaceholderHeader(_cardSignalBreakdown, "SIGNAL BREAKDOWN")

        ' Row 7: OI × CVD CROSS + VOLUME PROFILE side by side (placeholders — P4d)
        Dim row7 = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1,
            .BackColor = Theme.BG_BASE, .AutoSize = False,
            .Margin = New Padding(0, 0, 0, 8)
        }
        row7.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        row7.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        _cardOiCvdCross    = NewCard()
        _cardVolumeProfile = NewCard()
        _cardOiCvdCross.Dock    = DockStyle.Fill
        _cardVolumeProfile.Dock = DockStyle.Fill
        _cardOiCvdCross.Margin    = New Padding(0, 0, 8, 0)
        _cardVolumeProfile.Margin = New Padding(0)
        row7.Controls.Add(_cardOiCvdCross, 0, 0)
        row7.Controls.Add(_cardVolumeProfile, 1, 0)
        AddRow(row7, 140)
        AddPlaceholderHeader(_cardOiCvdCross,    "OI × CVD CROSS")
        AddPlaceholderHeader(_cardVolumeProfile, "VOLUME PROFILE")

        ' Row 8: KELLY SIZING (placeholder — P4d)
        _cardKelly = NewCard()
        AddRow(_cardKelly, 80)
        AddPlaceholderHeader(_cardKelly, "KELLY SIZING")

        ' Row 9: DYNAMIC NORMS (placeholder — P4d)
        _cardDynamicNorms = NewCard()
        AddRow(_cardDynamicNorms, 90)
        AddPlaceholderHeader(_cardDynamicNorms, "DYNAMIC NORMS")

        ' Row 10: legacy txtOutput verification dump (P5 deletes)
        _cardVerificationDump = NewCard()
        AddRow(_cardVerificationDump, 400)
        ReparentVerificationDumpControls()

        ' Row 11: SETTINGS & TOOLS (placeholder — P4e)
        _cardSettingsTools = NewCard()
        AddRow(_cardSettingsTools, 200)
        ReparentSettingsToolsControls()

        ' Populate the bindable cards from rows 3-5 with their static child
        ' controls (custom-paint controls + labels). Per-run binding methods
        ' live in UI/MainForm_Render_Cards.vb and only update values.
        InitBoundCardContents()

        Me.Controls.Add(_gridRoot)
    End Sub

    Private Function NewCard() As RoundedCardPanel
        Return New RoundedCardPanel() With {
            .Background = Theme.BG_CARD,
            .BorderColor = Theme.BORDER_CARD,
            .CornerRadius = 6.0F,
            .Padding = New Padding(12),
            .Margin = New Padding(0, 0, 0, 8),
            .Dock = DockStyle.Fill
        }
    End Function

    Private Sub AddRow(c As Control, height As Integer)
        _gridRoot.RowCount += 1
        _gridRoot.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
        _gridRoot.Controls.Add(c, 0, _gridRoot.RowCount - 1)
    End Sub

    Private Shared Sub AddPlaceholderHeader(card As Control, text As String)
        Dim lbl = New Label() With {
            .AutoSize = True,
            .Text = text,
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .Location = New Point(12, 12)
        }
        card.Controls.Add(lbl)
    End Sub

    ' -----------------------------------------------------------------------
    ' Reparent: header strip (P4a — static, no per-run binding)
    ' -----------------------------------------------------------------------
    Private Sub ReparentHeaderStripControls()
        Dim controlsToMove As Control() = {
            lblPositionTitle, rbNone, rbLong, rbShort,
            btnAnalyze, lblVerdict,
            lblAutoRun, nudMinutes, lblMin, nudSeconds, lblSec,
            pnlMode, btnStartStop}
        For Each c In controlsToMove
            If c Is Nothing Then Continue For
            Me.Controls.Remove(c)
            _cardHeaderStrip.Controls.Add(c)
        Next

        ' Position controls inside the header strip card. The card height
        ' is ~60px; we lay out left → right with simple absolute offsets.
        Const Y_TOP As Integer = 6
        lblPositionTitle.Location = New Point(4, Y_TOP + 14)
        lblPositionTitle.Size     = New Size(96, 18)
        rbNone.Location  = New Point(100, Y_TOP + 14)
        rbLong.Location  = New Point(190, Y_TOP + 2)
        rbShort.Location = New Point(190, Y_TOP + 22)

        ' lblVerdict floats next to the radios as the "current verdict" mini
        ' echo (the big verdict text lives in _cardVerdict). Keep it small,
        ' centred between the radios and ANALYZE so it stays useful as a
        ' compact echo until P5 deletes it.
        lblVerdict.Location = New Point(290, Y_TOP + 4)
        lblVerdict.Size     = New Size(280, 30)
        lblVerdict.TextAlign = ContentAlignment.MiddleLeft
        lblVerdict.Font      = Theme.FontMono(11.0F, FontStyle.Bold)

        ' AUTO EVERY {m} {s} chip cluster
        lblAutoRun.Text     = "AUTO EVERY"
        lblAutoRun.Location = New Point(590, Y_TOP + 14)
        lblAutoRun.Size     = New Size(80, 18)
        nudMinutes.Location = New Point(670, Y_TOP + 12)
        nudMinutes.Size     = New Size(42, 22)
        lblMin.Text         = "m"
        lblMin.Location     = New Point(715, Y_TOP + 14)
        lblMin.Size         = New Size(14, 18)
        nudSeconds.Location = New Point(732, Y_TOP + 12)
        nudSeconds.Size     = New Size(42, 22)
        lblSec.Text         = "s"
        lblSec.Location     = New Point(777, Y_TOP + 14)
        lblSec.Size         = New Size(14, 18)

        ' Single / Repeat segmented (existing pnlMode)
        pnlMode.Location = New Point(800, Y_TOP + 12)
        pnlMode.Size     = New Size(134, 22)
        rbSingle.Location = New Point(0, 2)
        rbRepeat.Location = New Point(68, 2)

        btnStartStop.Location = New Point(940, Y_TOP + 10)
        btnStartStop.Size     = New Size(70, 26)

        ' ANALYZE button — right-aligned. Card width is ~1168 inside padding,
        ' so place at 1168 - 140 - 4 = 1024 from the card's client origin.
        btnAnalyze.Location = New Point(1024, Y_TOP + 8)
        btnAnalyze.Size     = New Size(140, 30)
        btnAnalyze.Text     = ChrW(&H25B6) & "  ANALYZE"

        ' Hook the card's resize so the ANALYZE button stays right-aligned
        ' when the card width changes (it will, when DockStyle.Fill resizes).
        AddHandler _cardHeaderStrip.SizeChanged,
            Sub(s, ev)
                Dim cx As Integer = _cardHeaderStrip.ClientSize.Width
                If cx <= 0 Then Return
                btnAnalyze.Left = Math.Max(290, cx - btnAnalyze.Width - 16)
                ' Auto-run chip cluster sits just left of the ANALYZE button.
                Dim chipRight As Integer = btnAnalyze.Left - 12
                btnStartStop.Left = chipRight - btnStartStop.Width
                pnlMode.Left      = btnStartStop.Left - pnlMode.Width - 8
                lblSec.Left       = pnlMode.Left - lblSec.Width - 4
                nudSeconds.Left   = lblSec.Left - nudSeconds.Width - 2
                lblMin.Left       = nudSeconds.Left - lblMin.Width - 4
                nudMinutes.Left   = lblMin.Left - nudMinutes.Width - 2
                lblAutoRun.Left   = nudMinutes.Left - lblAutoRun.Width - 6
            End Sub
    End Sub

    ' -----------------------------------------------------------------------
    ' Reparent: live performance strip
    ' -----------------------------------------------------------------------
    Private Sub ReparentPerfStripControls()
        Dim flow = New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Padding = New Padding(2, 4, 2, 4)
        }
        Dim allLabels = New Label() {
            lblPerfMode, lblPerfWeek, lblPerf3d, lblPerfDay,
            lblPerfAsia, lblPerfLondon, lblPerfNy}
        For Each lbl In allLabels
            If lbl Is Nothing Then Continue For
            lbl.Margin = New Padding(0, 4, 16, 4)
            flow.Controls.Add(lbl)
        Next
        _cardPerfStrip.Controls.Add(flow)
    End Sub

    ' -----------------------------------------------------------------------
    ' Reparent: legacy txtOutput → row 10 verification dump card
    ' -----------------------------------------------------------------------
    Private Sub ReparentVerificationDumpControls()
        Dim header = New Label() With {
            .AutoSize = True,
            .Text = "LEGACY OUTPUT (verification — removed in P5)",
            .Font = Theme.FontMono(9.0F, FontStyle.Regular),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Top,
            .Padding = New Padding(0, 0, 0, 4)
        }
        _cardVerificationDump.Controls.Add(header)

        Me.Controls.Remove(txtOutput)
        txtOutput.Dock = DockStyle.Fill
        txtOutput.Margin = New Padding(0)
        txtOutput.ScrollBars = RichTextBoxScrollBars.Vertical
        _cardVerificationDump.Controls.Add(txtOutput)
        txtOutput.BringToFront()
        ' Header label is Dock=Top so it stays on top; txtOutput fills the rest.
    End Sub

    ' -----------------------------------------------------------------------
    ' Reparent: status-bar links / lblLogInfo / lblCountdown → SETTINGS & TOOLS
    ' (P4a placeholder — final layout in P4e)
    ' -----------------------------------------------------------------------
    Private Sub ReparentSettingsToolsControls()
        AddPlaceholderHeader(_cardSettingsTools, "SETTINGS & TOOLS")

        Dim flow = New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .BackColor = Color.Transparent,
            .Height = 140,
            .Padding = New Padding(8, 8, 8, 8),
            .AutoScroll = False
        }

        Dim controlsToMove As Control() = {
            lblLogInfo, lblCountdown,
            lnkResetLog, lnkCalibCheck, lnkAnalysisReport,
            lnkTweakSettings, lnkOutputDump, lnkOutputDumpSettings}
        For Each c In controlsToMove
            If c Is Nothing Then Continue For
            Me.Controls.Remove(c)
            c.Margin = New Padding(0, 4, 16, 4)
            ' lblLogInfo was Designer-sized; give it a sensible flow width.
            If c Is lblLogInfo Then
                lblLogInfo.AutoSize = True
                lblLogInfo.Size     = New Size(700, 18)
            End If
            If c Is lblCountdown Then
                lblCountdown.AutoSize = True
            End If
            flow.Controls.Add(c)
        Next

        _cardSettingsTools.Controls.Add(flow)
    End Sub

    ' -----------------------------------------------------------------------
    ' ApplyInitialFormSize — size the form from card-grid heights.
    ' Hard width ceiling 1280 px per parent spec §3.6.
    ' -----------------------------------------------------------------------
    Private Sub ApplyInitialFormSize()
        Const TARGET_CLIENT_W As Integer = 1200
        Const HARD_CEILING_W  As Integer = 1280

        Dim chromeW As Integer = Me.Width  - Me.ClientSize.Width
        Dim chromeH As Integer = Me.Height - Me.ClientSize.Height

        ' Sum the row heights + outer Padding.
        Dim totalRowH As Integer = 0
        For Each rs As RowStyle In _gridRoot.RowStyles
            If rs.SizeType = SizeType.Absolute Then totalRowH += CInt(rs.Height)
        Next
        Dim targetClientH As Integer = totalRowH + _gridRoot.Padding.Top + _gridRoot.Padding.Bottom + 16

        Dim wa As Rectangle = Screen.FromControl(Me).WorkingArea
        Dim finalW As Integer = Math.Min(TARGET_CLIENT_W + chromeW, HARD_CEILING_W)
        Dim finalH As Integer = Math.Min(targetClientH + chromeH, wa.Height - 40)

        Me.MinimumSize = New Size(1100 + chromeW, 700)
        Me.Size        = New Size(finalW, finalH)
        Me.Location    = New Point(
            wa.Left + (wa.Width  - finalW) \ 2,
            wa.Top  + (wa.Height - finalH) \ 2)
    End Sub

    ' -----------------------------------------------------------------------
    ' Live performance strip helpers
    ' -----------------------------------------------------------------------
    Private Shared Function MakePerfLabel(initialText As String) As System.Windows.Forms.Label
        Return New System.Windows.Forms.Label() With {
            .AutoSize  = True,
            .Font      = New System.Drawing.Font("Segoe UI", 8.0!),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = System.Drawing.Color.Transparent,
            .Text      = initialText
        }
    End Function

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

        ' TableLayoutPanel handles repositioning automatically once label
        ' widths change. No ResizeControls() call required.
        _cardPerfStrip?.PerformLayout()
    End Sub

    Private Shared Function NormaliseMode(raw As String) As String
        If raw IsNot Nothing AndAlso raw.Trim().Equals("target", StringComparison.OrdinalIgnoreCase) Then
            Return "target"
        End If
        Return "barrier"
    End Function

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
