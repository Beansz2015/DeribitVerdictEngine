' UI/MainForm_Layout.vb
' UI reskin P4a — card-grid layout skeleton.
'
' Replaces the pixel-positioned ResizeControls() / SizeToContent() approach
' with a TableLayoutPanel hosting RoundedCardPanel rows. Existing Designer
' controls (position radios, ANALYZE button, NUDs, perf labels, status-bar
' links) are reparented from Me.Controls into their new card homes;
' per-control styling that previously lived in ApplyDesignerOverrides
' is folded into BuildCardGridLayout at reparent time.
'
' P5b deleted the legacy RTF pipeline: the verification dump card (old row
' 10) is gone, and txtOutput survives only as a hidden Designer-locked
' zombie field (declared in MainForm.Designer.vb; zero writers, zero
' readers — see the locked carve-out in the P5b kickoff §3.6).

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

    ' [P4 #2] On-close analysis-mode watcher (docs/on-close-analysis-mode-proposal.md). A ~1s
    ' Threading.Timer engaged in StartAutoRun's on_close branch (in place of the interval timer);
    ' each tick reads the live MarketState and fires RunAutoAnalysis when the exec-resolution bar
    ' rolls OR the interval BACKSTOP elapses (never silent on a feed stall). Disposed in
    ' StopAutoRun + OnFormClosing. _onCloseActive is the "engaged" flag (the interval _autoRunTimer
    ' is NOT running in on-close mode, so AutoRunEngaged() ORs the two). LastSeenOpen carries the
    ' forming-bar open-time between ticks; LastFireUtc anchors the backstop + double-fire guard.
    Private _onCloseWatcher       As Threading.Timer
    Private _onCloseActive        As Boolean = False
    Private _onCloseLastSeenOpen  As Long = BarCloseDetector.Unseen
    Private _onCloseLastRes       As Integer = 0
    Private _onCloseLastFireUtc   As DateTime = DateTime.MinValue
    ' True when trigger_mode=on_close was requested but no WS feed is present (transport=rest /
    ' feed Nothing) → the session ran interval mode instead (§4.4). Surfaces a status-line note.
    Private _onCloseFellBackToInterval As Boolean = False

    ' [P4 #2] Trigger-mode radios (INTERVAL | ON-CLOSE), created programmatically in
    ' ReparentHeaderStripControls beside the SINGLE/REPEAT pnlMode (no Designer edit — same
    ' programmatic pattern as the exit-guard strip). Mutual exclusion via the shared pnlTrigger
    ' parent. Source of truth = auto_run.trigger_mode; CheckedChanged persists it (bumpVersion:=False)
    ' and relabels lblAutoRun "AUTO EVERY" ↔ "BACKSTOP". Disabled while a run is engaged (like nudMinutes).
    Private pnlTrigger      As Panel
    Private rbModeInterval  As RadioButton
    Private rbModeOnClose   As RadioButton

    ' MTF 15m candle TTL cache (P1 upgrade v0.47)
    Private Const MTF_TTL_SECONDS As Integer = 60
    Private _mtfCandles15m     As List(Of Candle) = Nothing
    Private _mtfLastFetchTime  As DateTime = DateTime.MinValue

    ' WebSocket transport (P2 — consumer routing + shadow parity). The per-run source is
    ' resolved in RunAnalysisAsync.ResolveSource(); this is the ONLY host glue — the feed,
    ' the sources, and the parity comparer are all host-agnostic. On the pure-REST path
    ' (transport="rest" AND shadow_parity=false) the feed is never started, so _wsFeed /
    ' _wsSource / _marketState stay Nothing and ResolveSource always returns _restSource —
    ' byte-identical to v38. _wsDegradedThisRun is reset at the top of each run and set by
    ' ResolveSource when the per-run REST fallback fires (status line reads it).
    Private _restSource       As IMarketDataSource
    Private _wsSource         As IMarketDataSource
    Private _marketState      As MarketState
    Private _wsFeed           As DeribitWsFeed
    Private _wsDegradedThisRun As Boolean = False

    ' Shadow-parity comparer (P2). Non-Nothing only when network.shadow_parity is on; holds
    ' the running consecutive-pass counter the WS-health status line reads. Host-agnostic
    ' (logs to a side file, never the CSV/scoring).
    Private _parityComparer   As ShadowParityComparer

    ' [T1-B] Regime ADX hysteresis state.
    Private _prevRegime As String = ""

    ' Resilience: count of skipped analyses this session.
    Private _skipCount As Integer = 0

    ' Spec C — SC ledger guard. Non-empty when the most recent run's signed
    ' breakdown points failed to sum to the scores (a mis-attributed scoring
    ' contribution). Prepended to the LOG line by UpdateLogInfo, same pattern as
    ' SettingsLoader.LastLoadError. Set after each ScoringEngine.Calculate.
    Private _ledgerWarn As String = ""

    ' P4f — last-successful capture for the ANALYSIS SKIPPED degraded render.
    ' Refs default to Nothing; render-time defaults to MinValue (the LOG
    ' "last HH:mm:ss" line stays hidden until the first success).
    Friend _lastSuccessfulVerdict     As VerdictResult
    Friend _lastSuccessfulIndicators  As IndicatorResults
    Friend _lastSuccessfulNorms       As DynamicNorms
    Friend _lastSuccessfulCfg         As EngineSettings
    Friend _lastSuccessfulRenderTime  As DateTime = DateTime.MinValue
    Friend _lastSkipReason            As String

    ' P4f — overlay panels tracked per card for opacity-dim during the
    ' skipped state. Created lazily in commit 2; commit 1 leaves this list
    ' empty but defines the field so ClearStaleOverlays compiles.
    Friend _staleOverlays As New List(Of Control)()

    ' P4f — VERDICT card has two stacked inner panels. Normal layout is
    ' built by InitVerdictCard; the SKIPPED panel is built once next to it
    ' and toggled via Visible. Avoids tearing down/rebuilding controls on
    ' every skip cycle and keeps existing label refs (_lblVerdictText etc.)
    ' valid across skip transitions.
    Friend _verdictNormalPanel  As TableLayoutPanel
    Friend _verdictSkippedPanel As TableLayoutPanel
    Friend _lblSkippedReason    As Label

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

    ' P4e commit 2 — REPEAT/SINGLE chip inside the AUTO-RUN sub-box; tooltip
    ' carries the full log path now that the LOG line shows row count only.
    Friend _autoRunChip   As Pill
    Friend _logInfoTooltip As ToolTip

    ' P4f — "last HH:mm:ss" timestamp label inside the LOG sub-box. Hidden
    ' until the first successful render (_lastSuccessfulRenderTime > MinValue).
    Friend lblLastSuccess As Label

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
    Private _cardIndicatorDetails      As RoundedCardPanel
    Private _cardSettingsTools     As RoundedCardPanel    ' P4e binds

    ' Custom-control fields populated by the P4b binding methods.
    ' Declared here so MainForm_Render_Cards.vb can reference them without
    ' a second declaration site.
    Friend _scoreArc          As ScoreArcGauge
    Friend _lblScoreConfidence As Label
    Friend _lblScoreRaw       As Label   ' GAP-01: "Long N/M | Short N/M"
    ' GAP-02 + GAP-03 moved to VERDICT card per stabilisation decision —
    ' eff scores + TRANSITIONAL penalty render beneath the REGIME line so
    ' the SCORE card keeps full vertical room for the arc gauge.
    Friend _lblVerdictEffPenalty As Label
    Friend _lblVerdictText    As Label
    Friend _contextBadge      As ContextBadge
    Friend _lblRegime         As Label
    Friend _mtfRow            As MtfRow
    Friend _lblHold           As Label
    ' F-09 (consolidated fix): full-width wrapping HOLD/EXIT reason line under
    ' the 2×2 grid. The HOLD chip clips long CalcHoldStatus strings (Layer 1.5
    ' lost the structural-break prices entirely); this row carries the full
    ' string. After P5b deletes the legacy block it is the only site that does.
    Friend _lblHoldReason     As Label
    Friend _regimeAnchorWarn  As RegimeAnchorWarn
    ' F-08/F-09: hero row height is re-computed per bind — base height plus
    ' room for whichever conditional rows (eff/penalty, hold reason, anchor
    ' banner) are visible this run. Index captured at AddRow time.
    Friend _heroRowIndex      As Integer = -1
    Friend Const HERO_ROW_BASE As Integer = 180
    Friend _lblLastPrice      As Label
    Friend _lblLastPriceAtr   As Label
    Friend _lblLastPriceTime  As Label
    Friend _lblLastPriceSession As Label
    ' ATR card: sub-header + dual (long / short) zone rows. Bottom cap-reason
    ' label removed in P5-test gap-fix commit 1 (C1b dedup) — the (label)
    ' parenthetical is now appended inline to each row's CAPPED cell.
    Friend _atrSubHeader      As Label
    Friend _atrLongRow        As AtrRowControls
    Friend _atrShortRow       As AtrRowControls

    ''' <summary>
    ''' Five-zone labels for one direction of the ATR ENTRY LEVELS card.
    ''' Mirrors StructuralCardControls but for the ATR row layout.
    ''' </summary>
    Friend Class AtrRowControls
        Public Property DirLabel         As Label   ' "LONG" / "SHORT" prefix
        Public Property StopValue        As Label   ' ATR stop price cell
        Public Property StructStopValue  As Label   ' C1c: deeper structural stop (hidden when struct == atr or missing)
        Public Property StopCellLayout   As TableLayoutPanel  ' C1c: holds StructStopValue + StopValue; width-ratio mutated at bind time
        Public Property RRValue          As Label
        Public Property RRSubValue       As Label   ' KNOWN-0 fix: "(risk N / rwd N)" on its own smaller-font label so the line can't clip inside RRValue
        Public Property EntryValue       As Label
        Public Property CappedValue      As Label
        Public Property TargetValue      As Label
    End Class
    Friend _structLongCtrls   As StructuralCardControls
    Friend _structShortCtrls  As StructuralCardControls

    ''' <summary>
    ''' Holds the controls inside a STRUCTURAL card so P4b binding code can
    ''' reach them without four parallel label fields.
    ''' </summary>
    Friend Class StructuralCardControls
        Public Property StopValue   As Label
        Public Property TargetValue As Label
        Public Property EntryValue  As Label
        Public Property RRValue     As Label
        Public Property RRSubValue  As Label   ' F-01 fix: risk/rwd line on its own smaller-font label (was wrapping + clipping inside RRValue)
    End Class

    ' -----------------------------------------------------------------------
    ' Constructor
    ' -----------------------------------------------------------------------
    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.47 [P4]"
        Me.BackColor = Theme.BG_BASE

        ' Enable form-level key preview so the Ctrl+Shift+S full-form screenshot
        ' hotkey reaches OnFormKeyDown before child controls consume it.
        Me.KeyPreview = True

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

        ' Tooltip for lblLogInfo — full CSV path moves here per P4e proposal §4.9
        ' so the LOG line stays clean ("Log: N rows · skipped M").
        _logInfoTooltip = New System.Windows.Forms.ToolTip() With {
            .InitialDelay = 400,
            .AutoPopDelay = 8000,
            .ReshowDelay  = 200,
            .IsBalloon    = False
        }

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
        InitMarketDataSources()
        InitAutoRunControls()
        ' [P4 #1] Start the exit-guard tick at form load (D6: decoupled from auto-run — MarketState
        ' streams whenever transport=ws regardless of auto-run). Each tick self-gates on posState +
        ' feed health; disposed in OnFormClosing.
        StartExitGuard()

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
    ' Market-data sources + WS feed lifecycle (P2)
    '
    ' RestMarketDataSource is always available (the pass-through fallback). The WS feed +
    ' WsMarketDataSource are started only when transport="ws" OR shadow_parity is on — pure
    ' REST otherwise (zero WS overhead, today's behaviour). The feed reads settings once at
    ' start; a transport change needs an app restart (hot-swap is out of scope, proposal §5).
    ' Called from the constructor after SettingsLoader.Initialise.
    ' -----------------------------------------------------------------------
    Private Sub InitMarketDataSources()
        _restSource = New RestMarketDataSource()
        Dim net = SettingsLoader.Current.Network
        Dim wantWs As Boolean =
            String.Equals(net.Transport, "ws", StringComparison.OrdinalIgnoreCase) OrElse net.ShadowParity
        If wantWs Then
            _marketState = New MarketState()
            _wsFeed = New DeribitWsFeed(_marketState)
            ' [P3 §3] Gate the WS trades stream on connection-health, not last-trade-age — a
            ' complete-but-quiet buffer is valid (matches REST). The lambda is late-bound, so
            ' _wsFeed is set by the time a run invokes it (feed constructed just above).
            _wsSource = New WsMarketDataSource(_marketState,
                            Function() _wsFeed.IsConnected AndAlso Not _wsFeed.IsCoolingDown)
            _wsFeed.StartAsync()   ' returns immediately; the connect/receive/reconnect loop runs on a background task
            If net.ShadowParity Then
                _parityComparer = New ShadowParityComparer(GetParityLogPath())
            End If
        End If
    End Sub

    ' Side-log path for the shadow-parity comparison (exe dir; never the CSV).
    Friend Shared Function GetParityLogPath() As String
        Return IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ws_parity_log.txt")
    End Function

    ' Stop the WS feed (if running) on form close so the background socket unwinds cleanly.
    Protected Overrides Sub OnFormClosing(e As System.Windows.Forms.FormClosingEventArgs)
        Try
            _wsFeed?.Stop()
        Catch
        End Try
        Try
            StopExitGuard()   ' [P4 #1] dispose the exit-guard tick on close
        Catch
        End Try
        MyBase.OnFormClosing(e)
    End Sub

    ' -----------------------------------------------------------------------
    ' Apply design palette to Designer-set controls
    ' (replaces the old ApplyDesignerOverrides pass; runs before reparent)
    ' -----------------------------------------------------------------------
    Private Sub ApplyControlThemes()
        ' P5b — txtOutput is a Designer-locked zombie (no writers, no readers,
        ' not parented to any card). InitializeComponent still creates it at a
        ' visible position, so hide it here to keep it off the card grid.
        txtOutput.Visible = False

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

        ' Row 2: live performance strip. 40 px wasn't tall enough for the
        ' AutoSize labels + flow padding + card padding (totalled ~30 px
        ' vertical demand against 16 px of inner content room); labels
        ' were clipping to zero. Bumped to 52 with tighter card padding.
        _cardPerfStrip = NewCard()
        _cardPerfStrip.Padding = New Padding(10, 6, 10, 6)
        AddRow(_cardPerfStrip, 52)
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
        ' Bumped 160 → 180 px after the 11pt section-header bump tightened
        ' the VERDICT card's 2×2 grid (CONTEXT / REGIME / MTF / HOLD) to the
        ' point where eff/penalty + grid items overlapped on long verdict
        ' strings. Extra 20 px feeds the grid; SCORE arc + LAST PRICE block
        ' had headroom to spare so they don't suffer.
        ' F-08/F-09 (consolidated fix): 180 is the BASE height. BindCardVerdict
        ' grows this row per run when the conditional rows beneath the 2×2 grid
        ' are visible (eff/penalty, hold reason, regime-anchor banner) so they
        ' get their own pixels instead of compressing the grid / occluding the
        ' HOLD slot (review finding F-08 on cases 41/42).
        AddRow(heroRow, HERO_ROW_BASE)
        _heroRowIndex = _gridRoot.RowStyles.Count - 1

        ' Row 4: ATR ENTRY LEVELS. Bumped from 110 to 150 in P4 retro-fix
        ' for GAP-06 dual long+short rendering (section header + ATR sub-
        ' header + two zone rows + cap reason).
        ' KNOWN-0 (consolidated fix): 150 → 200. At 150 each zone row got
        ' ~43 px against ~60 px of three-line content at the active-side
        ' 11.5pt bold font — the third line (risk/rwd under R:R, the CAPPED
        ' cell's reason label) clipped on every case; both rows clipped on
        ' NO TRADE where both render large. 200 gives each zone row ~68 px.
        _cardAtrLevels = NewCard()
        AddRow(_cardAtrLevels, 200)

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
        ' F-01 (consolidated fix): 110 → 130. At 110 the cells got ~60 px;
        ' the R:R cell's "(risk N / rwd N)" line wrapped at the 25% column
        ' width and the wrapped tail clipped (losing the rwd value), and the
        ' per-side missing-leg notes ("— no swing target above") lost their
        ' wrapped second line (review OBS-F). 130 gives cells ~80 px.
        AddRow(structRow, 130)

        ' Row 6: SIGNAL BREAKDOWN (P4c binds). 500 px to accommodate 8 rows
        ' in the longest tier (TIER 1) plus header / column headers / 3
        ' footer rows / TOTAL.
        _cardSignalBreakdown = NewCard()
        AddRow(_cardSignalBreakdown, 500)

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
        ' Height grown from 140 → 210 (P4d commit 3) → 320 (Spec B) to fit
        ' VOLUME PROFILE's 7-row level stack + 2 sub-labels + the 90 px
        ' VolumeHistogramMini below them. OI × CVD column had headroom and
        ' tolerates the bump without redesign.
        AddRow(row7, 320)
        AddPlaceholderHeader(_cardOiCvdCross,    "OI × CVD CROSS")
        AddPlaceholderHeader(_cardVolumeProfile, "VOLUME PROFILE")

        ' Row 8: KELLY SIZING (P4d commit 2 binds). Height fits header + bias
        ' / capped tags + 2-line advisory + 5 KV rows ending in contracts row.
        ' Card hides itself entirely (Visible=False) when v.KellyPWin = 0,
        ' so the row collapses visually but the AddRow slot remains reserved.
        _cardKelly = NewCard()
        ' Bumped 180 → 220 after live-run verification — 180 clipped the
        ' Lean/Contracts row mid-line. NewCard adds 12 px padding top+bottom
        ' (24 total), leaving usable interior. Header (~22) + 2 advisory
        ' lines (~32) + 5 KV rows × 22 (~110) ≈ 164; row margins push to
        ' ~180 actual content, so 220 gives breathing room.
        AddRow(_cardKelly, 220)
        AddPlaceholderHeader(_cardKelly, "KELLY SIZING")

        ' Row 9: INDICATOR DETAILS (P4d commit 4 binds). Renamed from
        ' _cardDynamicNorms — holds the verbose absolute-value detail
        ' (NORMS + 11 other diagnostic sub-groups) that didn't fit in the
        ' SIGNAL BREAKDOWN NOTE column. Height tall enough for 6 SectionGroup
        ' equivalents per column on the 4K-monitor scrolling form; if rows
        ' overflow, the top-level _gridRoot will scroll vertically.
        _cardIndicatorDetails = NewCard()
        ' Bumped 480 → 760 after live-run verification — 480 clipped the
        ' lower groups (VOLUME / TREND STRUCTURE left; OI / MICROCVD /
        ' LIQUIDATIONS right). Six groups per column at ~100-130 px each
        ' plus header / padding lands ~720; 760 gives breathing room.
        AddRow(_cardIndicatorDetails, 760)

        ' Row 11: SETTINGS & TOOLS — grouped layout (P4e commit 1).
        ' 300 px clipped the Output Dump LinkRow + cog after live-run check
        ' (TOOLS sub-box got ~98 px of the 246 px content area after
        ' placeholder header + LOG/AUTO-RUN + CTA). Bumped to 340 px per
        ' P4e kickoff §4 "bump the row height by 40 px" guidance.
        _cardSettingsTools = NewCard()
        ' P4 #1: +26px over the P4e 340 for the full-width EXIT GUARD strip's own row, so the
        ' TOOLS (percent) row stays whole.
        AddRow(_cardSettingsTools, 366)
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

        ' lblVerdict is superseded by the VERDICT card. P5b removed its last
        ' writer (legacy RenderOutput); the Designer declaration stays per the
        ' locked carve-out, so it lives on here as a hidden zombie control.
        lblVerdict.Visible = False

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

        ' [P4 #2] Trigger-mode segmented (INTERVAL | ON-CLOSE) — built programmatically (no
        ' Designer edit). Sits left of the AUTO EVERY cluster; the SizeChanged handler below
        ' keeps it flowing right-to-left with the rest of the auto-run controls.
        BuildTriggerModeToggle()
        pnlTrigger.Location = New Point(460, Y_TOP + 12)

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
                ' [P4 #2] trigger-mode toggle sits left of the AUTO EVERY label.
                If pnlTrigger IsNot Nothing Then pnlTrigger.Left = lblAutoRun.Left - pnlTrigger.Width - 10
            End Sub
    End Sub

    ' [P4 #2] Build the INTERVAL | ON-CLOSE segmented toggle (mirrors the SINGLE/REPEAT pnlMode
    ' pattern, created in code rather than the Designer). The radios share pnlTrigger as their
    ' parent so WinForms mutually excludes them. CheckedChanged → persist trigger_mode + relabel.
    ' Initial Checked state is set later in InitAutoRunControls (after SettingsLoader.Initialise).
    Private Sub BuildTriggerModeToggle()
        pnlTrigger = New Panel() With {
            .Size = New Size(146, 22),
            .BackColor = Color.Transparent,
            .TabStop = False
        }
        rbModeInterval = New RadioButton() With {
            .AutoSize = True,
            .Text = "Interval",
            .Font = New Font("Segoe UI", 8.5!),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .Location = New Point(0, 2),
            .Checked = True
        }
        rbModeOnClose = New RadioButton() With {
            .AutoSize = True,
            .Text = "On-close",
            .Font = New Font("Segoe UI", 8.5!),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .Location = New Point(66, 2)
        }
        pnlTrigger.Controls.Add(rbModeInterval)
        pnlTrigger.Controls.Add(rbModeOnClose)
        _cardHeaderStrip.Controls.Add(pnlTrigger)
        AddHandler rbModeInterval.CheckedChanged, AddressOf OnTriggerModeChanged
        AddHandler rbModeOnClose.CheckedChanged, AddressOf OnTriggerModeChanged
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
            .Padding = New Padding(2, 2, 2, 2)
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
    ' Reparent: status-bar links / lblLogInfo / lblCountdown → SETTINGS & TOOLS
    ' (P4e commit 1 — grouped layout: LOG / AUTO-RUN / ANALYSIS REPORT / TOOLS)
    '
    ' Layout:
    '   Row 1 (TLP 2×1): LOG (solid) | AUTO-RUN (dashed cyan)
    '   Row 2 (full):    AnalysisReportButton CTA
    '   Row 3 (full):    TOOLS — three LinkRows (Calibration / Tweak / Dump)
    '                    plus cog LinkLabel positioned at right of dump row
    '
    ' Old Designer/programmatic LinkLabels are hidden but kept alive so the
    ' Handles … LinkClicked partial-class wiring stays satisfied. The new
    ' P3 controls forward clicks via shim lambdas to the existing handlers.
    ' -----------------------------------------------------------------------
    Private Sub ReparentSettingsToolsControls()
        AddPlaceholderHeader(_cardSettingsTools, "SETTINGS & TOOLS")
        _cardSettingsTools.TabStop = False

        ' Outer 3-row TLP — sits below the placeholder header.
        Dim outer = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 4,
            .BackColor = Color.Transparent,
            .Padding = New Padding(0, 30, 0, 0),
            .Margin = New Padding(0),
            .TabStop = False
        }
        outer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 110))   ' LOG/AUTO-RUN (P4f +18px "last HH:mm:ss" line)
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))    ' P4#1 EXIT GUARD strip (full-width)
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))    ' CTA
        outer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F)) ' TOOLS

        ' ---------------- Row 1: LOG / AUTO-RUN (2 cols) ----------------
        Dim row1 = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 8),
            .TabStop = False
        }
        row1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        row1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        Dim grpLog = New SectionGroup() With {
            .Title = "LOG",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 4, 0),
            .BorderStyle2 = SectionGroup.GroupBorderStyle.Solid,
            .AccentColor = Theme.BORDER_CARD,
            .TabStop = False
        }
        Me.Controls.Remove(lblLogInfo)
        lblLogInfo.Anchor = AnchorStyles.None
        lblLogInfo.Dock = DockStyle.None
        lblLogInfo.AutoSize = True
        lblLogInfo.Location = New Point(10, 26)
        grpLog.Controls.Add(lblLogInfo)

        ' P4f — last-successful render timestamp. Hidden until UpdateLogInfo
        ' first sees _lastSuccessfulRenderTime > DateTime.MinValue.
        lblLastSuccess = New Label() With {
            .AutoSize = True,
            .Anchor = AnchorStyles.None,
            .Dock = DockStyle.None,
            .Location = New Point(10, 46),
            .Text = "",
            .Font = lblLogInfo.Font,
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .Visible = False,
            .TabStop = False
        }
        grpLog.Controls.Add(lblLastSuccess)

        Me.Controls.Remove(lnkResetLog)
        lnkResetLog.Anchor = AnchorStyles.None
        lnkResetLog.Dock = DockStyle.None
        lnkResetLog.AutoSize = True
        lnkResetLog.Location = New Point(10, 66)
        grpLog.Controls.Add(lnkResetLog)

        Dim grpAutoRun = New SectionGroup() With {
            .Title = "AUTO-RUN",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 0, 0, 0),
            .BorderStyle2 = SectionGroup.GroupBorderStyle.Dashed,
            .AccentColor = Theme.BORDER_DASHED_INFO,
            .TabStop = False
        }
        Me.Controls.Remove(lblCountdown)
        lblCountdown.Anchor = AnchorStyles.None
        lblCountdown.Dock = DockStyle.None
        lblCountdown.AutoSize = True
        lblCountdown.Location = New Point(10, 26)
        grpAutoRun.Controls.Add(lblCountdown)

        ' REPEAT / SINGLE chip — mirrors the rbRepeat/rbSingle radios that
        ' still live in the header strip. Source of truth = radios; the chip
        ' is updated via UpdateAutoRunChip on their CheckedChanged events.
        _autoRunChip = New Pill() With {
            .Size = New Size(96, 22),
            .Location = New Point(10, 52),
            .CornerRadius = 10.0F,
            .Text = "▶ SINGLE",
            .TabStop = False
        }
        grpAutoRun.Controls.Add(_autoRunChip)
        AddHandler rbRepeat.CheckedChanged, AddressOf UpdateAutoRunChip
        AddHandler rbSingle.CheckedChanged, AddressOf UpdateAutoRunChip
        UpdateAutoRunChip(Nothing, EventArgs.Empty)

        row1.Controls.Add(grpLog,     0, 0)
        row1.Controls.Add(grpAutoRun, 1, 0)
        outer.Controls.Add(row1, 0, 0)

        ' ---------------- Row 2: EXIT GUARD strip (full-width, P4 #1) ----------------
        ' Live status-bar element (sibling of the WS-health line), its own full-width row so the full
        ' line renders inline — EXIT GUARD · ⚠ EXIT — 2 adverse (MicroCVD BEAR_ACCEL, TFI SELL) /
        ' the swing-break level — no tooltip (D4). Driven by the guard timer (MainForm_ExitGuard.vb),
        ' visible only when a position is declared. NOT an RTF/snapshot/card surface → no card-binding
        ' obligation (spec §6).
        lblExitGuard = New Label() With {
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Text = "",
            .Font = Theme.FontMono(9.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .Margin = New Padding(2, 0, 2, 4),
            .Visible = False,
            .TabStop = False
        }
        outer.Controls.Add(lblExitGuard, 0, 1)

        ' ---------------- Row 3: ANALYSIS REPORT CTA ----------------
        ' P3 AnalysisReportButton — Solid amber FlatButton with 📊 icon, →
        ' arrow, and a persistent glow halo. Click shim forwards into the
        ' existing async LinkLabel handler so we don't duplicate its body.
        Dim btnReport = New AnalysisReportButton() With {
            .Text = "ANALYSIS REPORT",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 0, 8),
            .Height = 44
        }
        AddHandler btnReport.Click, Sub(s, ev) lnkAnalysisReport_LinkClicked(s, Nothing)
        outer.Controls.Add(btnReport, 0, 2)

        ' Hide the original Analysis Report LinkLabel — the FlatButton owns
        ' the visual surface now. The LinkLabel stays alive (Handles clause
        ' is still bound) but invisible; the shim above bypasses it.
        Me.Controls.Remove(lnkAnalysisReport)
        lnkAnalysisReport.Visible = False
        _cardSettingsTools.Controls.Add(lnkAnalysisReport)

        ' ---------------- Row 4: TOOLS ----------------
        Dim grpTools = New SectionGroup() With {
            .Title = "TOOLS",
            .Dock = DockStyle.Fill,
            .BorderStyle2 = SectionGroup.GroupBorderStyle.Solid,
            .AccentColor = Theme.BORDER_CARD,
            .TabStop = False
        }

        Dim rowCalib = New LinkRow() With {
            .LinkText = "Calibration Readiness",
            .Location = New Point(10, 26),
            .Size = New Size(320, 22),
            .TabStop = False
        }
        AddHandler rowCalib.LinkClicked, Sub(s, ev) lnkCalibCheck_LinkClicked(s, Nothing)
        grpTools.Controls.Add(rowCalib)

        Dim rowTweak = New LinkRow() With {
            .LinkText = "Tweak Settings",
            .Location = New Point(10, 52),
            .Size = New Size(320, 22),
            .TabStop = False
        }
        AddHandler rowTweak.LinkClicked, Sub(s, ev) lnkTweakSettings_LinkClicked(s, Nothing)
        grpTools.Controls.Add(rowTweak)

        Dim rowDump = New LinkRow() With {
            .LinkText = "Output Dump",
            .Location = New Point(10, 78),
            .Size = New Size(320, 22),
            .TabStop = False
        }
        AddHandler rowDump.LinkClicked, Sub(s, ev) lnkOutputDump_LinkClicked(s, Nothing)
        grpTools.Controls.Add(rowDump)

        ' Cog (settings) — reuse the existing programmatic LinkLabel so its
        ' Handles … LinkClicked partial-class binding stays intact. Placed
        ' right-anchored inside the TOOLS box, level with the Output Dump row.
        Me.Controls.Remove(lnkOutputDumpSettings)
        lnkOutputDumpSettings.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        lnkOutputDumpSettings.Dock = DockStyle.None
        lnkOutputDumpSettings.AutoSize = True
        lnkOutputDumpSettings.Font = Theme.FontMono(11.0F, FontStyle.Regular)
        grpTools.Controls.Add(lnkOutputDumpSettings)
        AddHandler grpTools.SizeChanged,
            Sub(s, ev)
                If grpTools.ClientSize.Width <= 0 Then Return
                lnkOutputDumpSettings.Location = New Point(
                    grpTools.ClientSize.Width - lnkOutputDumpSettings.Width - 14, 80)
            End Sub

        ' The three replaced LinkLabels (Calib / Tweak / Output Dump) stay
        ' alive but hidden — their Handles clauses are still bound, but the
        ' new LinkRows forward clicks via the shims above.
        For Each lnk As LinkLabel In {lnkCalibCheck, lnkTweakSettings, lnkOutputDump}
            Me.Controls.Remove(lnk)
            lnk.Visible = False
            _cardSettingsTools.Controls.Add(lnk)
        Next

        outer.Controls.Add(grpTools, 0, 3)

        _cardSettingsTools.Controls.Add(outer)
        outer.BringToFront()
    End Sub

    ' -----------------------------------------------------------------------
    ' ApplyInitialFormSize — size the form from card-grid heights.
    ' Hard width ceiling 1280 px per parent spec §3.6.
    ' -----------------------------------------------------------------------
    Private Sub ApplyInitialFormSize()
        ' Logical pixels. Under 150% DPI scaling these physically render as
        ' 1.5×, so 1000 logical ≈ 1500 physical, which still leaves the rest
        ' of a 3840-physical 4K screen for charts. Spec §3.6 hard ceiling of
        ' "1280 px" assumed 100% DPI; on Windows that constraint must apply
        ' to logical pixels because Me.Size is a logical measurement.
        Const TARGET_CLIENT_W As Integer = 1100
        Const HARD_CEILING_W  As Integer = 1180

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

        Me.MinimumSize = New Size(950 + chromeW, 700)
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

                ' Dim inactive session cells (Asia/London/NY when not currently
                ' running). The labels array indices 3-5 are the session cells;
                ' indices 0-2 are Cur.Wk/3d/Cur.Day rolling windows which are
                ' always inherently active. --% cells (the Else branch below)
                ' keep FG_QUATERNARY without double-dimming.
                If i >= 3 AndAlso Not w.IsActive Then
                    fgColor = DimColour(fgColor, 0.6F)
                End If

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

    ''' <summary>
    ''' Returns a darker variant of <paramref name="c"/> by multiplying each
    ''' channel by <paramref name="factor"/>. Used to dim inactive perf-strip
    ''' session cells so the currently-active session stands out by brightness.
    ''' Alpha-blending on Label.ForeColor doesn't work in WinForms (alpha is
    ''' ignored), so we compute an actual darker RGB value.
    ''' </summary>
    Private Shared Function DimColour(c As Color, factor As Single) As Color
        Return Color.FromArgb(
            c.A,
            CInt(Math.Round(c.R * factor)),
            CInt(Math.Round(c.G * factor)),
            CInt(Math.Round(c.B * factor)))
    End Function

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
        ' [v36 §10a] operational perf-strip toggle — no feature version bump / change_log.
        SettingsLoader.Save(cfg, "performance_display.metric_mode → " & mode, bumpVersion:=False)
        _metricMode = mode
        UpdatePerformanceLabels()
        lblLogInfo.Text = "Metric mode persisted → " & mode
    End Sub

    ' -----------------------------------------------------------------------
    ' Position a child form centered on whichever monitor the parent occupies.
    ' Survives multi-monitor layouts. Sets StartPosition=Manual so any
    ' subsequent layout code doesn't override. Call after constructing the
    ' child but before .Show().
    ' -----------------------------------------------------------------------
    Friend Shared Sub PositionOnParentScreen(child As Form, parent As Form)
        If child Is Nothing OrElse parent Is Nothing Then Return
        Dim host = Screen.FromControl(parent)
        child.StartPosition = FormStartPosition.Manual
        child.Location = New Point(
            host.WorkingArea.X + (host.WorkingArea.Width - child.Width) \ 2,
            host.WorkingArea.Y + (host.WorkingArea.Height - child.Height) \ 2)
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
        PositionOnParentScreen(_tweakForm, Me)
        _tweakForm.Show()
        _tweakForm.BringToFront()
    End Sub

    ' -----------------------------------------------------------------------
    ' Output Dump helpers + link clicks
    ' -----------------------------------------------------------------------
    Friend Shared Function GetDumpPath() As String
        Return IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis_output_dump.md")
    End Function

    ' Composes the optional PERF STRIP line that AnalysisOutputDump.Append
    ' renders directly under the "## Run …" header for each dump block.
    ' Reads instance label text + _metricMode, so lives on the partial that
    ' owns the perf-strip controls. Called from RunAnalysisAsync after
    ' UpdatePerformanceLabels has refreshed the label values.
    Friend Function ComposePerfStripLine() As String
        Dim modeTag As String = If(_metricMode = "target", "[T]", "[B]")
        Return String.Format("PERF STRIP {0} {1} | {2} | {3} | {4} | {5} | {6}",
                              modeTag,
                              lblPerfWeek.Text,
                              lblPerf3d.Text,
                              lblPerfDay.Text,
                              lblPerfAsia.Text,
                              lblPerfLondon.Text,
                              lblPerfNy.Text)
    End Function

    Private Sub lnkOutputDump_LinkClicked(sender As Object,
            e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) _
            Handles lnkOutputDump.LinkClicked
        Dim dumpPath As String = GetDumpPath()
        If Not IO.File.Exists(dumpPath) OrElse Not SettingsLoader.Current.AnalysisLogging.OutputDumpEnabled Then
            MessageBox.Show(Me, "Output dump is empty or disabled.", "Output Dump",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            Dim psi As New System.Diagnostics.ProcessStartInfo(dumpPath) With {.UseShellExecute = True}
            System.Diagnostics.Process.Start(psi)
        Catch ex As Exception
            MessageBox.Show(Me, "Could not open dump file: " & ex.Message, "Output Dump",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' -----------------------------------------------------------------------
    ' REPEAT / SINGLE chip update (P4e commit 2)
    ' Wired to rbRepeat.CheckedChanged and rbSingle.CheckedChanged.
    ' -----------------------------------------------------------------------
    Friend Sub UpdateAutoRunChip(sender As Object, e As EventArgs)
        If _autoRunChip Is Nothing Then Return
        If rbRepeat IsNot Nothing AndAlso rbRepeat.Checked Then
            _autoRunChip.Text        = "▶ REPEAT"
            _autoRunChip.BgColor     = Theme.BG_CARD_RAISED
            _autoRunChip.FgColor     = Theme.ACC_INFO
            _autoRunChip.BorderColor = Theme.ACC_INFO
        Else
            _autoRunChip.Text        = "▶ SINGLE"
            _autoRunChip.BgColor     = Theme.BG_CARD_RAISED
            _autoRunChip.FgColor     = Theme.FG_SECONDARY
            _autoRunChip.BorderColor = Theme.BORDER_CARD
        End If
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
        PositionOnParentScreen(_outputDumpSettingsForm, Me)
        _outputDumpSettingsForm.Show()
        _outputDumpSettingsForm.BringToFront()
    End Sub

    ' -----------------------------------------------------------------------
    ' Log / calibration / analysis-report link clicks (P5a migration target).
    ' These three handlers + UpdateLogInfo moved here from MainForm_Render_Header.vb
    ' so that Header.vb deletes cleanly in P5b. UpdateLogInfo touches only Label
    ' controls and ToolTip — no RTF dependency. The calibration handler still
    ' writes to txtOutput in P5a commit 1; commit 2 switches it to AnalysisReportForm.
    ' -----------------------------------------------------------------------
    Private Sub UpdateLogInfo()
        Dim rows As Integer = AnalysisLogger.GetRowCount()
        Dim path As String  = AnalysisLogger.GetLogPath()
        ' "Log: {N} rows[ · skipped {M}]". Full path moves to a tooltip so the
        ' LOG sub-box stays scannable at a glance.
        Dim skipSuffix As String = If(_skipCount > 0, String.Format(" · skipped {0}", _skipCount), "")
        ' Persistent calibration-integrity warning: if settings.json failed to
        ' parse at load, the engine is running on uncalibrated POCO defaults.
        ' Recomputed every call (like skipSuffix) so it stays visible across
        ' renders and self-clears once a valid settings.json successfully loads.
        Dim cfgWarn As String = If(Not String.IsNullOrEmpty(SettingsLoader.LastLoadError),
                                   "settings.json parse failed — running on code defaults · ", "")
        ' [WS-P2] WS-health segment — empty on the pure-REST path (feed never started), so the
        ' LOG line is byte-identical to v38 unless transport="ws" or shadow_parity is on.
        Dim wsSeg As String = BuildWsStatusSegment()
        lblLogInfo.Text = String.Format("{0}{1}{2}Log: {3} rows{4}", _ledgerWarn, cfgWarn, wsSeg, rows, skipSuffix)
        If _logInfoTooltip IsNot Nothing Then
            _logInfoTooltip.SetToolTip(lblLogInfo, path)
        End If
        ' P4f — last-successful render timestamp. Hidden until the first
        ' successful run captures _lastSuccessfulRenderTime.
        If lblLastSuccess IsNot Nothing Then
            If _lastSuccessfulRenderTime > DateTime.MinValue Then
                lblLastSuccess.Text    = "last " & _lastSuccessfulRenderTime.ToString("HH:mm:ss")
                lblLastSuccess.Visible = True
            Else
                lblLastSuccess.Visible = False
            End If
        End If
    End Sub

    ' -----------------------------------------------------------------------
    ' WS-health status segment (P2). Empty unless the feed is active (transport="ws"
    ' or shadow_parity, i.e. _wsFeed was started). Mirrors the _ledgerWarn / cfgWarn /
    ' skip-counter cascade — a trailing " · "-separated prefix on the LOG line. Reads the
    ' feed's plain health fields on the UI thread (no Control.Invoke in the feed).
    '   WS OK · 1/3/5/15 fresh · trades N        — connected, streams fresh
    '   WS DEGRADED — REST fallback (stream stale) — per-run fallback fired this run
    '   WS DOWN — reconnecting (Xs backoff, R reconnects) — disconnected
    ' Shadow mode appends · parity NN/50.
    ' -----------------------------------------------------------------------
    Private Function BuildWsStatusSegment() As String
        If _wsFeed Is Nothing Then Return ""
        Dim net = SettingsLoader.Current.Network

        Dim seg As String
        If _wsDegradedThisRun Then
            seg = "WS DEGRADED — REST fallback (stream stale)"
        ElseIf Not _wsFeed.IsConnected Then
            seg = String.Format("WS DOWN — reconnecting ({0}s backoff, {1} reconnects)",
                                _wsFeed.CurrentBackoffSec, _wsFeed.ReconnectCount)
        Else
            Dim n As Integer = If(_marketState IsNot Nothing, _marketState.TradeCount, 0)
            Dim ageSec As Integer = CInt(Math.Max(0, (DateTime.UtcNow - _wsFeed.LastFrameUtc).TotalSeconds))
            If ageSec <= net.WsStaleAfterSec Then
                seg = String.Format("WS OK · 1/3/5/15 fresh · trades {0}", n)
            Else
                seg = String.Format("WS OK · streams {0}s stale · trades {1}", ageSec, n)
            End If
        End If

        If net.ShadowParity AndAlso _parityComparer IsNot Nothing Then
            seg &= String.Format(" · parity {0}/50", _parityComparer.ConsecutivePasses)
        End If
        Return seg & " · "
    End Function

    Private Sub lnkResetLog_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkResetLog.LinkClicked
        Dim result = MessageBox.Show(Me,
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
        ' P5a commit 2 — calibration report now opens in AnalysisReportForm
        ' (non-modal, same viewer the ANALYSIS REPORT button uses). Removes
        ' the txtOutput dependency so this handler survives the P5b deletion
        ' sweep. The form's filePath param identifies the underlying log file
        ' because BuildCalibrationReport derives its content from there.
        Dim md As String = BuildCalibrationReport()
        Dim frm As New AnalysisReportForm(md, AnalysisLogger.GetLogPath())
        PositionOnParentScreen(frm, Me)
        frm.Show()
    End Sub

    Private Async Sub lnkAnalysisReport_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkAnalysisReport.LinkClicked
        Dim csvPath As String = AnalysisLogger.GetLogPath()
        If Not IO.File.Exists(csvPath) Then
            MessageBox.Show(Me, "No analysis_log.csv found. Run at least one analysis first.",
                            "Analysis Report", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Verify v0.4.1 schema — TrendStructure5m is the column added in v0.4.1
        Dim firstLine As String = Nothing
        Try
            Using sr As New IO.StreamReader(csvPath)
                firstLine = sr.ReadLine()
            End Using
        Catch
        End Try
        If firstLine Is Nothing OrElse Not firstLine.Contains("TrendStructure5m") Then
            MessageBox.Show(Me, "Log file is not v0.4.1 schema." & Environment.NewLine &
                            "Run analyses after the d1/d2 upgrade to accumulate v0.4.1 rows." & Environment.NewLine &
                            "(Old file was rotated to analysis_log.csv.v0.4.bak on first run.)",
                            "Analysis Report", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Disable link during fetch so the user can't double-click.
        lnkAnalysisReport.Enabled = False
        lnkAnalysisReport.Text = "Fetching OHLC…"
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim outputDir As String = AppDomain.CurrentDomain.BaseDirectory
        Try
            Dim report As AnalysisReport = Await AnalysisRunner.Run(csvPath, outputDir, cfg)
            Dim frm As New AnalysisReportForm(report.MarkdownText, report.MarkdownFilePath)
            PositionOnParentScreen(frm, Me)
            frm.Show()
        Catch ex As Exception
            MessageBox.Show(Me, "Analysis failed: " & ex.Message,
                            "Analysis Report", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            lnkAnalysisReport.Text = "Analysis Report"
            lnkAnalysisReport.Enabled = True
        End Try
    End Sub

    ' -----------------------------------------------------------------------
    ' Full-form screenshot (Ctrl+Shift+S) — dev tooling for the verification
    ' harness. Renders the entire form via DrawToBitmap so cards beyond the
    ' on-screen working area still appear in the PNG. PowerShell helper
    ' (tools/screenshot-mainform-full.ps1) writes the target path to
    ' verify/.screenshot-target, presses Ctrl+Shift+S, then polls for the
    ' PNG to appear. No-op when no marker file is present.
    ' -----------------------------------------------------------------------
    Private Sub OnFormKeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.S Then
            Dim targetPath As String = ReadScreenshotTargetPath()
            If Not String.IsNullOrEmpty(targetPath) Then
                SaveFullFormScreenshot(targetPath)
                e.Handled = True
            End If
        End If
    End Sub

    Private Function ReadScreenshotTargetPath() As String
        Dim markerPath As String = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "verify", ".screenshot-target")
        If Not File.Exists(markerPath) Then Return Nothing
        Try
            Dim p As String = File.ReadAllText(markerPath).Trim()
            File.Delete(markerPath)
            Return p
        Catch
            Return Nothing
        End Try
    End Function

    Friend Sub SaveFullFormScreenshot(outPath As String)
        ' Capture _gridRoot directly rather than Me. The form's outer Size is
        ' clamped by Windows' SystemMaximumSize (~screen working area) even
        ' with MaximumSize = Empty, so growing Me to the natural row-sum
        ' extent silently fails on any layout taller than the monitor and
        ' DrawToBitmap clips at the screen height (≈2171 logical on the
        ' test machine vs. ≈3170 needed for all 11 card rows).
        '
        ' Temporarily undocking _gridRoot lets us resize the inner panel past
        ' the form's client area and draw the whole grid. The form itself
        ' never changes size, so there's no on-screen flash and no restore
        ' artefact.
        Dim originalDock       = _gridRoot.Dock
        Dim originalAnchor     = _gridRoot.Anchor
        Dim originalAutoScroll = _gridRoot.AutoScroll
        Dim originalSize       = _gridRoot.Size
        Dim originalLoc        = _gridRoot.Location
        Try
            _gridRoot.Dock       = DockStyle.None
            _gridRoot.Anchor     = AnchorStyles.Top Or AnchorStyles.Left
            _gridRoot.AutoScroll = False
            _gridRoot.Location   = New Point(0, 0)
            _gridRoot.Size       = New Size(originalSize.Width, ComputeGridNaturalHeight())
            _gridRoot.PerformLayout()
            Application.DoEvents()

            Dim dir = Path.GetDirectoryName(outPath)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If
            Using bmp As New Bitmap(_gridRoot.Width, _gridRoot.Height)
                _gridRoot.DrawToBitmap(bmp, New Rectangle(0, 0, _gridRoot.Width, _gridRoot.Height))
                bmp.Save(outPath, Imaging.ImageFormat.Png)
            End Using
        Finally
            _gridRoot.AutoScroll = originalAutoScroll
            _gridRoot.Size       = originalSize
            _gridRoot.Location   = originalLoc
            _gridRoot.Anchor     = originalAnchor
            _gridRoot.Dock       = originalDock
            _gridRoot.PerformLayout()
            Application.DoEvents()
        End Try
    End Sub

    ' Sum of runtime-resolved row heights (via TableLayoutPanel.GetRowHeights,
    ' which covers Absolute / Percent / AutoSize uniformly) plus the grid's
    ' own outer padding and a 16 px slack. No form chrome — we draw the grid
    ' itself, not the whole form.
    Private Function ComputeGridNaturalHeight() As Integer
        Dim totalRowH As Integer = 0
        For Each h As Integer In _gridRoot.GetRowHeights()
            totalRowH += h
        Next
        Return totalRowH + _gridRoot.Padding.Top + _gridRoot.Padding.Bottom + 16
    End Function

End Class
