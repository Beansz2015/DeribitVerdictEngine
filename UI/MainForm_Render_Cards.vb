' UI/MainForm_Render_Cards.vb
' UI reskin P4b — binding methods that populate the new card-grid layout
' from VerdictResult / IndicatorResults. Companion to MainForm_Layout.vb.
'
' Called from RunAnalysisAsync after legacy RenderOutput() completes —
' both render paths coexist until P5 deletes txtOutput. The legacy
' txtOutput continues to live inside the verification dump card (row 10)
' so side-by-side parity can be eyeballed during P4 development.
'
' Card contents for rows 3-5 (SCORE / VERDICT / LAST PRICE / ATR LEVELS /
' STRUCTURAL × 2) are built once by InitBoundCardContents() which is
' invoked at the end of BuildCardGridLayout. Per-run BindCard* methods
' only update value-bearing controls.

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Static section-header label factory (used by every bound card)
    ' -----------------------------------------------------------------------
    Private Shared Function MakeSectionHeader(text As String,
                                              Optional colour As Color = Nothing) As Label
        Dim c As Color = If(colour.IsEmpty, Theme.FG_QUATERNARY, colour)
        Return New Label() With {
            .AutoSize = True,
            .Text = text,
            .Font = Theme.FontMono(9.0F, FontStyle.Bold),
            .ForeColor = c,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 4)
        }
    End Function

    Private Shared Function MakeValueLabel(font As Font, colour As Color) As Label
        Return New Label() With {
            .AutoSize = True,
            .Text = "",
            .Font = font,
            .ForeColor = colour,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
    End Function

    ' -----------------------------------------------------------------------
    ' InitBoundCardContents — build the per-card child controls once.
    ' Called from BuildCardGridLayout after all card panels exist.
    ' -----------------------------------------------------------------------
    Private Sub InitBoundCardContents()
        InitScoreCard()
        InitVerdictCard()
        InitLastPriceCard()
        InitAtrLevelsCard()
        InitStructuralCard(_cardStructLong,  isLong:=True)
        InitStructuralCard(_cardStructShort, isLong:=False)
    End Sub

    ' -----------------------------------------------------------------------
    ' SCORE card
    ' -----------------------------------------------------------------------
    Private Sub InitScoreCard()
        ' Layout: header / arc (flex) / confidence / raw scores.
        ' Eff. scores + TRANSITIONAL penalty moved to VERDICT card so the
        ' arc gauge keeps full vertical room (160 px hero / -24 padding /
        ' -50 px stacked labels = ~86 px for the arc → enough for the
        ' default 22pt LabelFont).
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 4,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))   ' SCORE header
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F)) ' arc gauge (flex)
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 16))   ' confidence
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 16))   ' GAP-01 raw scores
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        inner.Controls.Add(MakeSectionHeader("SCORE"), 0, 0)

        _scoreArc = New ScoreArcGauge() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Value = 0,
            .Max = 20,
            .ArcColor = Theme.ACC_NO_TRADE
        }
        inner.Controls.Add(_scoreArc, 0, 1)

        _lblScoreConfidence = MakeValueLabel(Theme.FontMono(9.0F, FontStyle.Regular), Theme.FG_QUATERNARY)
        _lblScoreConfidence.Dock = DockStyle.Fill
        _lblScoreConfidence.TextAlign = ContentAlignment.MiddleCenter
        _lblScoreConfidence.Text = "Confidence --"
        inner.Controls.Add(_lblScoreConfidence, 0, 2)

        ' GAP-01 raw scores side-by-side.
        _lblScoreRaw = MakeValueLabel(Theme.FontMono(9.0F, FontStyle.Regular), Theme.FG_SECONDARY)
        _lblScoreRaw.Dock = DockStyle.Fill
        _lblScoreRaw.TextAlign = ContentAlignment.MiddleCenter
        _lblScoreRaw.Text = "—"
        inner.Controls.Add(_lblScoreRaw, 0, 3)

        _cardScore.Controls.Add(inner)
    End Sub

    ' -----------------------------------------------------------------------
    ' VERDICT card
    ' -----------------------------------------------------------------------
    Private Sub InitVerdictCard()
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 5,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))    ' section header
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))    ' verdict text
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F)) ' 2×2 grid (flex)
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 16))    ' eff/penalty sub-row (conditional)
        inner.RowStyles.Add(New RowStyle(SizeType.AutoSize))         ' regime anchor warn
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        inner.Controls.Add(MakeSectionHeader("VERDICT"), 0, 0)

        _lblVerdictText = New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = "—",
            .Font = Theme.FontMono(22.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_PRIMARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }
        inner.Controls.Add(_lblVerdictText, 0, 1)

        ' 2×2 grid: CONTEXT / REGIME / MTF / HOLD
        Dim grid = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2, .RowCount = 2,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        _contextBadge = New ContextBadge() With {
            .Kind = ContextBadge.ContextKind.CONFIRMED,
            .Margin = New Padding(0)
        }
        grid.Controls.Add(MakePairCell("CONTEXT", _contextBadge), 0, 0)

        _lblRegime = MakeValueLabel(Theme.FontMono(11.0F, FontStyle.Bold), Theme.ACC_WARN)
        grid.Controls.Add(MakePairCell("REGIME", _lblRegime), 1, 0)

        _mtfRow = New MtfRow() With {
            .Kind = MtfRow.MtfKind.STATE_ONLY,
            .Direction = "—",
            .Margin = New Padding(0)
        }
        grid.Controls.Add(MakePairCell("MTF GATE", _mtfRow), 0, 1)

        _lblHold = MakeValueLabel(Theme.FontMono(11.0F, FontStyle.Bold), Theme.ACC_WARN)
        grid.Controls.Add(MakePairCell("HOLD", _lblHold), 1, 1)

        inner.Controls.Add(grid, 0, 2)

        ' GAP-02 + GAP-03 (relocated from SCORE card): effective scores +
        ' TRANSITIONAL penalty rendered as a single conditional sub-line
        ' beneath the REGIME / MTF grid. Hidden when RegimePenalty = 0.
        _lblVerdictEffPenalty = MakeValueLabel(Theme.FontMono(9.0F, FontStyle.Bold), Theme.ACC_WARN)
        _lblVerdictEffPenalty.Dock = DockStyle.Top
        _lblVerdictEffPenalty.TextAlign = ContentAlignment.MiddleLeft
        _lblVerdictEffPenalty.Margin = New Padding(0, 2, 0, 0)
        _lblVerdictEffPenalty.Text = ""
        _lblVerdictEffPenalty.Visible = False
        inner.Controls.Add(_lblVerdictEffPenalty, 0, 3)

        _regimeAnchorWarn = New RegimeAnchorWarn() With {
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 4, 0, 0),
            .WarningText = ""
        }
        ' RegimeAnchorWarn moves into a new bottom row since EffPenalty took row 3.
        inner.Controls.Add(_regimeAnchorWarn, 0, 4)

        _cardVerdict.Controls.Add(inner)
    End Sub

    Private Shared Function MakePairCell(headerText As String, valueControl As Control) As Control
        Dim panel = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 2,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 4, 0)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 14))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim hdr = New Label() With {
            .AutoSize = True,
            .Text = headerText,
            .Font = Theme.FontMono(8.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        panel.Controls.Add(hdr, 0, 0)
        valueControl.Dock = DockStyle.Top
        valueControl.Margin = New Padding(0)
        panel.Controls.Add(valueControl, 0, 1)
        Return panel
    End Function

    ' -----------------------------------------------------------------------
    ' LAST PRICE card
    ' -----------------------------------------------------------------------
    Private Sub InitLastPriceCard()
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 5,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 1))
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        inner.Controls.Add(MakeSectionHeader("LAST PRICE"), 0, 0)

        _lblLastPrice = MakeValueLabel(Theme.FontMono(19.0F, FontStyle.Bold), Theme.FG_PRIMARY)
        _lblLastPrice.Dock = DockStyle.Fill
        _lblLastPrice.Text = "—"
        inner.Controls.Add(_lblLastPrice, 0, 1)

        _lblLastPriceAtr = MakeValueLabel(Theme.FontMono(11.0F, FontStyle.Regular), Theme.FG_QUATERNARY)
        _lblLastPriceAtr.Dock = DockStyle.Fill
        _lblLastPriceAtr.Text = "ATR —"
        inner.Controls.Add(_lblLastPriceAtr, 0, 2)

        Dim divider = New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Theme.BORDER_INNER,
            .Margin = New Padding(0, 4, 0, 4),
            .Height = 1
        }
        inner.Controls.Add(divider, 0, 3)

        Dim bottom = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 2,
            .BackColor = Color.Transparent
        }
        bottom.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        bottom.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        bottom.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        _lblLastPriceTime = MakeValueLabel(Theme.FontMono(11.0F, FontStyle.Bold), Theme.FG_SECONDARY)
        _lblLastPriceTime.Dock = DockStyle.Fill
        _lblLastPriceTime.Text = "—"
        bottom.Controls.Add(_lblLastPriceTime, 0, 0)

        _lblLastPriceSession = MakeValueLabel(Theme.FontMono(9.0F, FontStyle.Regular), Theme.FG_QUATERNARY)
        _lblLastPriceSession.Dock = DockStyle.Fill
        _lblLastPriceSession.Text = "—"
        bottom.Controls.Add(_lblLastPriceSession, 0, 1)

        inner.Controls.Add(bottom, 0, 4)

        _cardLastPrice.Controls.Add(inner)
    End Sub

    ' -----------------------------------------------------------------------
    ' ATR ENTRY LEVELS card — five horizontal zones
    ' -----------------------------------------------------------------------
    Private Sub InitAtrLevelsCard()
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 5,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))   ' section header
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 14))   ' GAP-05 sub-header
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F)) ' LONG zone row
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F)) ' SHORT zone row
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 14))   ' cap reason
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        inner.Controls.Add(MakeSectionHeader("ATR ENTRY LEVELS"), 0, 0)

        ' GAP-05 ATR config sub-header.
        _atrSubHeader = MakeValueLabel(Theme.FontMono(8.5F, FontStyle.Regular), Theme.FG_QUATERNARY)
        _atrSubHeader.Dock = DockStyle.Fill
        _atrSubHeader.TextAlign = ContentAlignment.MiddleLeft
        _atrSubHeader.Text = ""
        inner.Controls.Add(_atrSubHeader, 0, 1)

        _atrLongRow  = BuildAtrZoneRow("LONG",  Theme.ACC_STRONG_LONG)
        _atrShortRow = BuildAtrZoneRow("SHORT", Theme.ACC_SHORT)
        inner.Controls.Add(_atrLongRow.DirLabel.Parent,  0, 2)
        inner.Controls.Add(_atrShortRow.DirLabel.Parent, 0, 3)

        _atrCapReason = MakeValueLabel(Theme.FontMono(8.0F, FontStyle.Regular), Theme.ACC_WARN)
        _atrCapReason.Dock = DockStyle.Fill
        _atrCapReason.TextAlign = ContentAlignment.MiddleCenter
        _atrCapReason.Text = ""
        inner.Controls.Add(_atrCapReason, 0, 4)

        _cardAtrLevels.Controls.Add(inner)
    End Sub

    ''' <summary>
    ''' Build one direction's 5-zone row (DirLabel + STOP / R:R / ENTRY /
    ''' CAPPED / TARGET). Returned struct carries the labels; the row
    ''' Panel is reachable via DirLabel.Parent.
    ''' </summary>
    Private Function BuildAtrZoneRow(dirText As String, targetColour As Color) As AtrRowControls
        Dim row = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        ' Col 0 widened to 70 px (was 50) — "SHORT" at 11pt Bold Geist Mono
        ' was wrapping to "SHO / RT" at 50.
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 70))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 12.0F))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim r As New AtrRowControls()
        r.DirLabel = New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = dirText,
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = targetColour,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }
        r.StopValue   = MakeZoneLabel("STOP",   Theme.ACC_SHORT)
        r.RRValue     = MakeZoneLabel("R:R",    Theme.FG_QUATERNARY)
        r.EntryValue  = MakeZoneLabel("ENTRY",  Theme.FG_PRIMARY)
        r.CappedValue = MakeZoneLabel("CAPPED", Theme.ACC_WARN)
        r.TargetValue = MakeZoneLabel("TARGET", targetColour)

        row.Controls.Add(r.DirLabel,    0, 0)
        row.Controls.Add(r.StopValue,   1, 0)
        row.Controls.Add(r.RRValue,     2, 0)
        row.Controls.Add(r.EntryValue,  3, 0)
        row.Controls.Add(r.CappedValue, 4, 0)
        row.Controls.Add(r.TargetValue, 5, 0)

        Return r
    End Function

    Private Shared Function MakeZoneLabel(headerText As String, valueColour As Color) As Label
        ' Tag stores the header text; we render header + value in the same
        ' Label via two-line text. Simpler than nested panels per zone.
        Dim lbl = New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = headerText & Environment.NewLine & "—",
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = valueColour,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Margin = New Padding(0),
            .Tag = headerText
        }
        Return lbl
    End Function

    ' -----------------------------------------------------------------------
    ' STRUCTURAL (LONG / SHORT) card
    ' -----------------------------------------------------------------------
    Private Sub InitStructuralCard(card As RoundedCardPanel, isLong As Boolean)
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 2,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim header = MakeSectionHeader(
            If(isLong, "STRUCTURAL (LONG) · 5M PIVOTS", "STRUCTURAL (SHORT) · 5M PIVOTS"),
            Theme.ACC_INFO)
        inner.Controls.Add(header, 0, 0)

        Dim grid = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        For i = 0 To 3
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim stopColour   As Color = Theme.ACC_SHORT
        Dim targetColour As Color = Theme.ACC_INFO
        Dim ctrls As New StructuralCardControls()
        ctrls.StopValue   = MakeZoneLabel("STRUCT STOP",   stopColour)
        ctrls.TargetValue = MakeZoneLabel("STRUCT TARGET", targetColour)
        ctrls.EntryValue  = MakeZoneLabel("ENTRY",         Theme.FG_PRIMARY)
        ctrls.RRValue     = MakeZoneLabel("R:R",           Theme.FG_QUATERNARY)

        grid.Controls.Add(ctrls.StopValue,   0, 0)
        grid.Controls.Add(ctrls.TargetValue, 1, 0)
        grid.Controls.Add(ctrls.EntryValue,  2, 0)
        grid.Controls.Add(ctrls.RRValue,     3, 0)

        inner.Controls.Add(grid, 0, 1)
        card.Controls.Add(inner)

        If isLong Then
            _structLongCtrls = ctrls
        Else
            _structShortCtrls = ctrls
        End If
    End Sub

    ' =======================================================================
    ' PER-RUN BINDING METHODS
    ' Each takes (v, r) [or subset] and only mutates Text / ForeColor /
    ' enum-typed properties. No control creation in here.
    ' =======================================================================

    Public Sub BindCardScore(v As VerdictResult)
        Dim verdictColour As Color = ResolveVerdictColour(v.Verdict)
        Dim shownScore As Integer = Math.Max(v.EffectiveLongScore, v.EffectiveShortScore)
        Dim shownMax   As Integer = If(v.MaxScore > 0, v.MaxScore, 20)

        If _scoreArc IsNot Nothing Then
            _scoreArc.ArcColor = verdictColour
            _scoreArc.SetValueAnimated(shownScore, shownMax)
        End If
        If _lblScoreConfidence IsNot Nothing Then
            _lblScoreConfidence.Text = $"Confidence {If(String.IsNullOrEmpty(v.Confidence), "—", v.Confidence)}"
        End If

        ' GAP-01 raw scores side-by-side.
        If _lblScoreRaw IsNot Nothing Then
            _lblScoreRaw.Text = $"Long {v.LongScore}/{shownMax}  |  Short {v.ShortScore}/{shownMax}"
        End If
        ' GAP-02 + GAP-03 (eff scores + TRANSITIONAL penalty) render
        ' inside the VERDICT card via BindCardVerdict — see _lblVerdictEffPenalty.
    End Sub

    Public Sub BindCardVerdict(v As VerdictResult, r As IndicatorResults)
        Dim verdictColour As Color = ResolveVerdictColour(v.Verdict)

        If _lblVerdictText IsNot Nothing Then
            _lblVerdictText.Text      = If(String.IsNullOrEmpty(v.Verdict), "—", v.Verdict)
            _lblVerdictText.ForeColor = verdictColour
        End If

        If _contextBadge IsNot Nothing Then
            _contextBadge.Kind = ParseContextKind(v.VerdictContext)
        End If

        If _lblRegime IsNot Nothing Then
            Dim regimeText  As String = If(String.IsNullOrEmpty(r.Regime), "—", r.Regime)
            Dim regimeColour As Color = Theme.FG_PRIMARY
            Select Case r.Regime
                Case "TRENDING_UP"   : regimeColour = Theme.ACC_STRONG_LONG
                Case "TRENDING_DOWN" : regimeColour = Theme.ACC_SHORT
                Case "RANGE_BOUND"   : regimeColour = Theme.ACC_WARN
                Case Else            : regimeColour = Theme.FG_QUATERNARY
            End Select
            _lblRegime.Text      = regimeText
            _lblRegime.ForeColor = regimeColour
        End If

        If _mtfRow IsNot Nothing Then
            ApplyMtfRow(_mtfRow, r.MTFGatePass, r.MTFGateReason)
        End If

        If _lblHold IsNot Nothing Then
            ' Designer convention: scoring engine emits "N/A -- no open position"
            ' when posState = None. Hide the label in that case to match the
            ' legacy RTF render (which only printed HOLD/EXIT when meaningful).
            If String.IsNullOrEmpty(v.HoldStatus) OrElse v.HoldStatus = "N/A -- no open position" Then
                _lblHold.Text    = "—"
                _lblHold.ForeColor = Theme.FG_QUATERNARY
            Else
                _lblHold.Text      = v.HoldStatus
                _lblHold.ForeColor = Theme.ACC_WARN
            End If
        End If

        If _regimeAnchorWarn IsNot Nothing Then
            _regimeAnchorWarn.WarningText = ComputeRegimeAnchorWarning(v, r)
        End If

        ' GAP-02 + GAP-03 — eff scores + TRANSITIONAL penalty under the
        ' REGIME grid. Visible only when the engine actually applied a
        ' transitional regime penalty.
        If _lblVerdictEffPenalty IsNot Nothing Then
            Dim hasPenalty As Boolean = v.RegimePenalty > 0
            _lblVerdictEffPenalty.Visible = hasPenalty
            If hasPenalty Then
                Dim mx As Integer = If(v.MaxScore > 0, v.MaxScore, 20)
                _lblVerdictEffPenalty.Text =
                    $"eff. L {v.EffectiveLongScore}/{mx}  |  S {v.EffectiveShortScore}/{mx}   ·   penalty −{v.RegimePenalty}"
            End If
        End If
    End Sub

    Public Sub BindCardLastPrice(r As IndicatorResults, lastTradePrice As Double)
        ' GAP-04: prefer the recent-trades price (closer to live than the
        ' 1m candle close). Fall back to the candle close if recent-trades
        ' wasn't populated this run.
        Dim displayPrice As Double = If(lastTradePrice > 0, lastTradePrice, r.CurrentPrice)
        If _lblLastPrice IsNot Nothing Then
            _lblLastPrice.Text = If(displayPrice > 0, $"${displayPrice:N0}", "—")
        End If
        If _lblLastPriceAtr IsNot Nothing Then
            _lblLastPriceAtr.Text = $"ATR {r.ATR:F1}"
        End If

        Dim utcNow As DateTime = DateTime.UtcNow
        Dim localNow As DateTime = DateTime.Now
        Dim offset As TimeSpan = TimeZoneInfo.Local.GetUtcOffset(localNow)
        Dim sign As String = If(offset >= TimeSpan.Zero, "+", "-")
        Dim hrs As Integer = CInt(Math.Floor(Math.Abs(offset.TotalHours)))
        If _lblLastPriceTime IsNot Nothing Then
            _lblLastPriceTime.Text = $"{localNow:HH:mm:ss} UTC{sign}{hrs}"
        End If
        If _lblLastPriceSession IsNot Nothing Then
            _lblLastPriceSession.Text = ResolveSessionLabel(utcNow.Hour) & " session"
        End If
    End Sub

    Public Sub BindCardAtrLevels(v As VerdictResult, r As IndicatorResults, norms As DynamicNorms)
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        ' Legacy parity: math uses the unrounded norms.ATRScaleFactor so
        ' stop/target prices match the legacy txtOutput render exactly.
        ' Sub-header still displays the rounded r.ATRSizeMultiplier (e.g.,
        ' "× 0.71 scale") since that's what the trader is used to seeing.
        Dim atrUnit    As Double = r.ATR * norms.ATRScaleFactor
        Dim atrStop    As Double = atrUnit * stopMult
        Dim atrTarget  As Double = atrUnit * targetMult
        Dim entryPx    As Double = r.CurrentPrice

        ' GAP-05 sub-header: current ATR config params (rounded scale per
        ' r.ATRSizeMultiplier for display continuity with the legacy line).
        If _atrSubHeader IsNot Nothing Then
            _atrSubHeader.Text = $"ATR {r.ATR:F2} × {r.ATRSizeMultiplier:F2} scale  |  {stopMult:F1}× stop / {targetMult:F1}× target"
        End If

        ' GAP-06: render BOTH long and short rows. Verdict direction gets
        ' primary weight; the contrary side dims. NO TRADE renders both rows
        ' at equal weight, FG_PRIMARY.
        Dim verdict As String = If(v.Verdict, "")
        Dim isLongVerdict  As Boolean = verdict.StartsWith("LONG") OrElse verdict.StartsWith("STRONG LONG") OrElse verdict.StartsWith("WEAK LONG")
        Dim isShortVerdict As Boolean = verdict.StartsWith("SHORT") OrElse verdict.StartsWith("STRONG SHORT") OrElse verdict.StartsWith("WEAK SHORT")
        Dim isNoTrade      As Boolean = verdict.StartsWith("NO TRADE")

        Dim longCapReason  As String = BindAtrRow(
            row:=_atrLongRow,
            isLong:=True,
            entryPx:=entryPx,
            stopPx:=entryPx - atrStop,
            rawTargetPx:=entryPx + atrTarget,
            adjustedTarget:=v.AdjustedLongTarget,
            cfgCapReason:=If(v.TargetCapReasonLong, ""),
            atrStop:=atrStop,
            atrTarget:=atrTarget,
            atrForFloor:=r.ATR,
            primary:=isLongVerdict OrElse isNoTrade,
            verdictColour:=ResolveVerdictColour(v.Verdict))

        Dim shortCapReason As String = BindAtrRow(
            row:=_atrShortRow,
            isLong:=False,
            entryPx:=entryPx,
            stopPx:=entryPx + atrStop,
            rawTargetPx:=entryPx - atrTarget,
            adjustedTarget:=v.AdjustedShortTarget,
            cfgCapReason:=If(v.TargetCapReasonShort, ""),
            atrStop:=atrStop,
            atrTarget:=atrTarget,
            atrForFloor:=r.ATR,
            primary:=isShortVerdict OrElse isNoTrade,
            verdictColour:=ResolveVerdictColour(v.Verdict))

        ' Show the cap reason from whichever side has one. If both sides
        ' have a cap, the verdict-direction wins; on NO TRADE, long wins
        ' arbitrarily (rare to have both anyway).
        Dim shownReason As String = ""
        If isShortVerdict Then
            shownReason = shortCapReason
            If shownReason = "" Then shownReason = longCapReason
        Else
            shownReason = longCapReason
            If shownReason = "" Then shownReason = shortCapReason
        End If
        If _atrCapReason IsNot Nothing Then
            _atrCapReason.Text = shownReason
        End If
    End Sub

    ''' <summary>
    ''' Bind one direction's ATR row. Returns the cap-reason string (empty
    ''' when CAPPED divider is suppressed). primary=True renders at full
    ''' verdict colour + bold; primary=False dims to FG_TERTIARY at smaller
    ''' font weight so the contrary direction reads as secondary detail.
    ''' </summary>
    Private Function BindAtrRow(row As AtrRowControls,
                                isLong As Boolean,
                                entryPx As Double,
                                stopPx As Double,
                                rawTargetPx As Double,
                                adjustedTarget As Double,
                                cfgCapReason As String,
                                atrStop As Double,
                                atrTarget As Double,
                                atrForFloor As Double,
                                primary As Boolean,
                                verdictColour As Color) As String

        If row Is Nothing Then Return ""

        Dim adjustedTargetPx As Double = If(adjustedTarget > 0, adjustedTarget, rawTargetPx)

        ' Sub-tick CAPPED suppression (v30 F1): only show CAPPED when the
        ' adjustment exceeds max(0.5, ATR × 0.02).
        Dim showCapped As Boolean = False
        Dim capReasonReturn As String = ""
        If adjustedTarget > 0 Then
            Dim noiseFloor As Double = Math.Max(0.5, atrForFloor * 0.02)
            showCapped = Math.Abs(rawTargetPx - adjustedTargetPx) >= noiseFloor
            If showCapped Then capReasonReturn = cfgCapReason
        End If

        SetZoneValue(row.StopValue,   "STOP",   $"{stopPx:F1}")
        SetZoneValue(row.EntryValue,  "ENTRY",  $"{entryPx:F1}")
        SetZoneValue(row.TargetValue, "TARGET", $"{adjustedTargetPx:F1}")
        SetZoneValue(row.RRValue,     "R:R",    FormatRR(atrTarget, atrStop))

        If showCapped Then
            SetZoneValue(row.CappedValue, "CAPPED", $"→ {adjustedTargetPx:F1}")
            row.CappedValue.ForeColor = Theme.ACC_WARN
        Else
            SetZoneValue(row.CappedValue, "·", "")
            row.CappedValue.ForeColor = Theme.BORDER_INNER
        End If

        ' Apply primary/secondary type weight.
        Dim directionColour As Color = If(isLong, Theme.ACC_STRONG_LONG, Theme.ACC_SHORT)
        Dim labelColour     As Color = If(primary, directionColour, Theme.FG_TERTIARY)
        Dim valueFont       As Font  = If(primary, Theme.FontMono(11.5F, FontStyle.Bold), Theme.FontMono(9.5F, FontStyle.Regular))

        row.DirLabel.ForeColor = labelColour
        row.DirLabel.Font      = Theme.FontMono(If(primary, 11.0F, 9.0F), FontStyle.Bold)

        For Each lbl In New Label() {row.StopValue, row.RRValue, row.EntryValue, row.CappedValue, row.TargetValue}
            lbl.Font = valueFont
        Next

        ' Per-side target colour: primary direction in its accent; secondary
        ' dimmed to FG_TERTIARY so the dominant row reads first.
        If primary Then
            row.TargetValue.ForeColor = directionColour
            row.StopValue.ForeColor   = Theme.ACC_SHORT
            row.RRValue.ForeColor     = Theme.FG_QUATERNARY
            row.EntryValue.ForeColor  = Theme.FG_PRIMARY
        Else
            row.TargetValue.ForeColor = Theme.FG_TERTIARY
            row.StopValue.ForeColor   = Theme.FG_TERTIARY
            row.RRValue.ForeColor     = Theme.FG_DIM
            row.EntryValue.ForeColor  = Theme.FG_TERTIARY
            If Not showCapped Then row.CappedValue.ForeColor = Theme.BORDER_INNER
        End If

        Return capReasonReturn
    End Function

    Public Sub BindCardStructural(r As IndicatorResults, isLong As Boolean)
        Dim ctrls = If(isLong, _structLongCtrls, _structShortCtrls)
        If ctrls Is Nothing Then Return

        Dim stopPx   As Double = If(isLong, r.SwingStopLong,   r.SwingStopShort)
        Dim targetPx As Double = If(isLong, r.SwingTargetLong, r.SwingTargetShort)
        Dim entryPx  As Double = r.CurrentPrice

        Dim hasStop   As Boolean = stopPx > 0
        Dim hasTarget As Boolean = targetPx > 0

        If hasStop AndAlso hasTarget Then
            ' FULL state
            SetZoneValue(ctrls.StopValue,   "STRUCT STOP",   $"{stopPx:F1}")
            SetZoneValue(ctrls.TargetValue, "STRUCT TARGET", $"{targetPx:F1}")
            SetZoneValue(ctrls.EntryValue,  "ENTRY",         $"{entryPx:F1}")

            Dim risk As Double, reward As Double
            If isLong Then
                risk   = entryPx - stopPx
                reward = targetPx - entryPx
            Else
                risk   = stopPx - entryPx
                reward = entryPx - targetPx
            End If
            SetZoneValue(ctrls.RRValue, "R:R", FormatRR(reward, risk))
            ctrls.TargetValue.ForeColor = Theme.ACC_INFO
            ctrls.StopValue.ForeColor   = Theme.ACC_SHORT
        ElseIf hasStop Then
            ' STOP ONLY
            SetZoneValue(ctrls.StopValue,   "STRUCT STOP",   $"{stopPx:F1}")
            ctrls.TargetValue.Text = "STRUCT TARGET" & Environment.NewLine &
                If(isLong, "— no swing target above", "— no swing target below")
            ctrls.TargetValue.ForeColor = Theme.FG_DIM
            SetZoneValue(ctrls.EntryValue, "ENTRY", $"{entryPx:F1}")
            SetZoneValue(ctrls.RRValue,    "R:R",   "—")
            ctrls.StopValue.ForeColor   = Theme.ACC_SHORT
        ElseIf hasTarget Then
            ' TARGET ONLY
            ctrls.StopValue.Text = "STRUCT STOP" & Environment.NewLine &
                If(isLong, "— no swing stop below", "— no swing stop above")
            ctrls.StopValue.ForeColor = Theme.FG_DIM
            SetZoneValue(ctrls.TargetValue, "STRUCT TARGET", $"{targetPx:F1}")
            SetZoneValue(ctrls.EntryValue,  "ENTRY",         $"{entryPx:F1}")
            SetZoneValue(ctrls.RRValue,     "R:R",           "—")
            ctrls.TargetValue.ForeColor = Theme.ACC_INFO
        Else
            ' Neither stop nor target — nothing structural to show.
            ctrls.StopValue.Text   = "STRUCT STOP" & Environment.NewLine & "—"
            ctrls.TargetValue.Text = "STRUCT TARGET" & Environment.NewLine & "—"
            ctrls.EntryValue.Text  = "ENTRY" & Environment.NewLine & $"{entryPx:F1}"
            ctrls.RRValue.Text     = "R:R" & Environment.NewLine & "—"
            ctrls.StopValue.ForeColor   = Theme.FG_DIM
            ctrls.TargetValue.ForeColor = Theme.FG_DIM
        End If
    End Sub

    ' -----------------------------------------------------------------------
    ' Helpers used by the binding methods
    ' -----------------------------------------------------------------------

    Private Shared Sub SetZoneValue(lbl As Label, header As String, value As String)
        If lbl Is Nothing Then Return
        lbl.Text = header & Environment.NewLine & value
    End Sub

    Private Shared Function ResolveVerdictColour(verdict As String) As Color
        If verdict Is Nothing Then Return Theme.FG_PRIMARY
        Select Case verdict
            Case "STRONG LONG"  : Return Theme.ACC_STRONG_LONG
            Case "LONG"         : Return Theme.ACC_LONG
            Case "WEAK LONG"    : Return Theme.ACC_WEAK_LONG
            Case "WEAK SHORT"   : Return Theme.ACC_WEAK_SHORT
            Case "SHORT"        : Return Theme.ACC_SHORT
            Case "STRONG SHORT" : Return Theme.ACC_STRONG_SHORT
        End Select
        If verdict.StartsWith("NO TRADE") Then Return Theme.ACC_NO_TRADE
        Return Theme.FG_PRIMARY
    End Function

    Private Shared Function ParseContextKind(ctx As String) As ContextBadge.ContextKind
        If ctx Is Nothing Then Return ContextBadge.ContextKind.CONFIRMED
        Select Case ctx.Trim().ToUpperInvariant()
            Case "CONFIRMED"        : Return ContextBadge.ContextKind.CONFIRMED
            Case "ALIGNED"          : Return ContextBadge.ContextKind.ALIGNED
            Case "FLOW_UNCONFIRMED" : Return ContextBadge.ContextKind.FLOW_UNCONFIRMED
            Case "MOMENTUM_FADING"  : Return ContextBadge.ContextKind.MOMENTUM_FADING
            Case "STRUCTURALLY_WEAK": Return ContextBadge.ContextKind.STRUCTURALLY_WEAK
        End Select
        Return ContextBadge.ContextKind.CONFIRMED
    End Function

    Private Shared Sub ApplyMtfRow(row As MtfRow, gatePass As Boolean, reason As String)
        ' MTFGateReason format from IndicatorEngine.CalcMTFGate:
        '   "MTF PASS [LONG] 15m +DI:39.8"           — passing direction
        '   "MTF BLOCK [LONG vs SHORT] ADX 18 < 20"  — proposed vs blocking
        '   "MTF state: BULLISH"                     — no proposed direction
        ' Strip the literal "MTF " prefix before the keyword test so the
        ' control's own "MTF " prefix doesn't end up doubled in the display.
        Dim reasonText As String = If(reason, "").Trim()
        Dim stripped As String = reasonText
        If stripped.StartsWith("MTF ", StringComparison.OrdinalIgnoreCase) Then
            stripped = stripped.Substring(4).TrimStart()
        End If
        Dim direction As String  = ExtractDirection(stripped)
        Dim blockedAgainst As String = ExtractBlockedAgainst(stripped)

        If stripped.StartsWith("PASS", StringComparison.OrdinalIgnoreCase) Then
            row.Kind = MtfRow.MtfKind.PASS
            row.Direction = If(String.IsNullOrEmpty(direction), "—", direction)
        ElseIf stripped.StartsWith("BLOCK", StringComparison.OrdinalIgnoreCase) Then
            row.Kind = MtfRow.MtfKind.BLOCK
            row.Direction = If(String.IsNullOrEmpty(direction), "?", direction)
            row.BlockedAgainst = If(String.IsNullOrEmpty(blockedAgainst), "?", blockedAgainst)
        ElseIf stripped.StartsWith("state:", StringComparison.OrdinalIgnoreCase) Then
            row.Kind = MtfRow.MtfKind.STATE_ONLY
            row.Direction = stripped.Substring(6).Trim()
        Else
            row.Kind = MtfRow.MtfKind.STATE_ONLY
            row.Direction = If(String.IsNullOrEmpty(stripped), "—", stripped)
        End If
    End Sub

    Private Shared Function ExtractDirection(reason As String) As String
        Dim openIdx As Integer = reason.IndexOf("["c)
        Dim closeIdx As Integer = reason.IndexOf("]"c)
        If openIdx < 0 OrElse closeIdx <= openIdx Then Return ""
        Dim inside As String = reason.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim()
        Dim vsIdx As Integer = inside.IndexOf(" vs ", StringComparison.OrdinalIgnoreCase)
        If vsIdx > 0 Then Return inside.Substring(0, vsIdx).Trim()
        Return inside
    End Function

    Private Shared Function ExtractBlockedAgainst(reason As String) As String
        Dim openIdx As Integer = reason.IndexOf("["c)
        Dim closeIdx As Integer = reason.IndexOf("]"c)
        If openIdx < 0 OrElse closeIdx <= openIdx Then Return ""
        Dim inside As String = reason.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim()
        Dim vsIdx As Integer = inside.IndexOf(" vs ", StringComparison.OrdinalIgnoreCase)
        If vsIdx > 0 Then Return inside.Substring(vsIdx + 4).Trim()
        Return ""
    End Function

    Private Shared Function ComputeRegimeAnchorWarning(v As VerdictResult,
                                                       r As IndicatorResults) As String
        Const REGIME_ANCHOR_ATR_THRESHOLD As Double = 3.0
        If r.ATR <= 0 OrElse r.EMA200_5m <= 0 Then Return ""
        Dim atrUnits As Double = (r.CurrentPrice - r.EMA200_5m) / r.ATR
        If v.Verdict IsNot Nothing Then
            If v.Verdict.StartsWith("STRONG LONG") AndAlso atrUnits < -REGIME_ANCHOR_ATR_THRESHOLD Then
                Return String.Format("price {0:F1}× ATR below 5m EMA(200) — STRONG LONG fighting intermediate bear",
                                     Math.Abs(atrUnits))
            ElseIf v.Verdict.StartsWith("STRONG SHORT") AndAlso atrUnits > REGIME_ANCHOR_ATR_THRESHOLD Then
                Return String.Format("price {0:F1}× ATR above 5m EMA(200) — STRONG SHORT fighting intermediate bull",
                                     atrUnits)
            End If
        End If
        Return ""
    End Function

    Private Shared Function ResolveSessionLabel(utcHour As Integer) As String
        ' Mirrors DynamicNorms.ApplySessionVolume() bucket boundaries.
        '   00-06 UTC  → ASIA
        '   07-12 UTC  → LONDON
        '   13-23 UTC  → NY
        If utcHour < 7 Then Return "ASIA"
        If utcHour < 13 Then Return "LONDON"
        Return "NY"
    End Function

    ' =======================================================================
    ' SIGNAL BREAKDOWN card (P4c). Clear-and-rebuild on each bind — 23 rows
    ' + tier separators + footer aggregates + TOTAL row.
    '
    ' Row layout: [label 110px] [state pill 64px] [note flex] [sc 30px]
    ' Each row mono-formatted; state-derivation rules come from the gap
    ' checklist B-architecture (Note column enrichment).
    ' =======================================================================

    Private Const SIGROW_HEIGHT As Integer = 18
    Private Const SIGROW_SUBNOTE_HEIGHT As Integer = 14

    ' -----------------------------------------------------------------------
    ' Engine-label → display-label mapping for SIGNAL BREAKDOWN.
    ' Anchored here so the binding shape survives context churn — without
    ' this table the next maintainer has to re-derive it from the scoring
    ' source.
    '
    ' Engine emits these breakdown items (from Core/ScoringEngine_Calculate
    ' _Scoring.vb + _Verdict.vb):
    '
    '   Engine label          → Display row label    Notes
    '   ----------------------  -------------------   --------------------------------
    '   ROC(9)                  ROC(9)                direct
    '   RSI(9)                  RSI(9)                direct
    '   (none)                  RSI Div               derived from r.RSIDivergence
    '                                                  → non-voting (SC "—")
    '   DMI +/-DI               DMI/ADX (component)   SC = sum of these two,
    '   ADX>{N}                  DMI/ADX (component)   clamped to ±1
    '   Volume                  Volume                direct
    '   VWAP                    VWAP Dev              SC attributed to Dev row
    '                           VWAP Bands            non-voting (SC "—")
    '   BBW/TTM                 BBW/TTM               direct
    '   EMA 9/21/50             EMA Ribbon            relabel
    '   Trend Structure         Trend Str             relabel
    '   Funding (info)          Funding               always SC=0 in breakdown —
    '                                                  Step 3 modifier surfaces
    '                                                  through VERDICT card's
    '                                                  eff. L/M sub-row.
    '                                                  Rendered non-voting (SC "—").
    '   (none)                  Funding Mom           Step 3b adjunct, non-voting
    '   OI Delta                OI Change             relabel
    '   Spread                  Spread                direct
    '   OFI                     OFI                   direct
    '   (none)                  OFI Mom               momentum derivative, non-voting
    '   Liq Penalty             Liq                   relabel
    '   CVD                     CVD                   direct
    '   MicroCVD                MicroCVD              direct
    '   TFI                     TFI                   direct
    '   5m EMA(200)             EMA200 5m             relabel
    '   Donchian(20)            Donchian              relabel
    '   OBV                     OBV                   direct
    '   VPFR-lite               VPFR                  relabel
    '   (none)                  Swing Pivots          derived from r.LastSwingHigh*
    '                                                  / Low* + r.BestPivot*,
    '                                                  non-voting (SC "—")
    '
    ' Emitted by engine but NOT rendered as a per-tier row here:
    '   Regime Align (2c)  → drives Pass 2c badge in the footer
    '   MTF Gate (15m)     → drives MtfRow in the VERDICT card
    '
    ' SC cell display convention:
    '   sc.HasValue = True,  sc.Value <> 0   → "+1" / "-1"
    '   sc.HasValue = True,  sc.Value = 0    → " 0"
    '   sc.HasValue = False                  → "—" (this row didn't vote)
    ' -----------------------------------------------------------------------
    Public Sub BindCardSignalBreakdown(v As VerdictResult, r As IndicatorResults)
        If _cardSignalBreakdown Is Nothing Then Return

        _cardSignalBreakdown.SuspendLayout()
        _cardSignalBreakdown.Controls.Clear()

        Dim items = If(v.SignalBreakdown, New List(Of SignalBreakdownItem)())

        Dim outer = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 5,
            .BackColor = Color.Transparent
        }
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))   ' section header
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 16))   ' column headers
        outer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F)) ' two-col grid (flex)
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 72))   ' footer + dividers
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))   ' TOTAL row
        outer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        outer.Controls.Add(MakeSectionHeader("SIGNAL BREAKDOWN"), 0, 0)

        ' Column headers (repeated for both halves).
        Dim colHdr = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        colHdr.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        colHdr.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        colHdr.Controls.Add(MakeBreakdownColumnHeader(), 0, 0)
        colHdr.Controls.Add(MakeBreakdownColumnHeader(), 1, 0)
        outer.Controls.Add(colHdr, 0, 1)

        ' Two-column main content. Each side is a vertical stack: tier
        ' label, then rows, then next tier label, then rows.
        Dim grid = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim leftCol  = NewBreakdownColumn(rightSide:=False)
        Dim rightCol = NewBreakdownColumn(rightSide:=True)
        grid.Controls.Add(leftCol,  0, 0)
        grid.Controls.Add(rightCol, 1, 0)
        outer.Controls.Add(grid, 0, 2)

        ' --- Left column: CORE + TIER 1 ---
        leftCol.Controls.Add(MakeTierLabel("CORE"))
        leftCol.Controls.Add(BuildRowRoc(r, items))
        leftCol.Controls.Add(BuildRowRsi(r, items))
        leftCol.Controls.Add(BuildRowRsiDiv(r, items))
        leftCol.Controls.Add(BuildRowDmiAdx(r, items))
        leftCol.Controls.Add(BuildRowVolume(r, items))

        leftCol.Controls.Add(MakeTierLabel("TIER 1"))
        leftCol.Controls.Add(BuildRowVwapDev(r, items))
        leftCol.Controls.Add(BuildRowVwapBands(r, items))
        leftCol.Controls.Add(BuildRowBbwTtm(r, items))
        leftCol.Controls.Add(BuildRowEmaRibbon(r, items))
        leftCol.Controls.Add(BuildRowTrendStr(r, items))
        leftCol.Controls.Add(BuildRowFunding(r, items))
        leftCol.Controls.Add(BuildRowFundingMom(r, items))
        leftCol.Controls.Add(BuildRowOiChange(r, items))

        ' --- Right column: TIER 2 + TIER 3 ---
        rightCol.Controls.Add(MakeTierLabel("TIER 2"))
        rightCol.Controls.Add(BuildRowSpread(r, items))
        rightCol.Controls.Add(BuildRowOfi(r, items))
        rightCol.Controls.Add(BuildRowOfiMom(r, items))
        rightCol.Controls.Add(BuildRowLiq(r, items))
        rightCol.Controls.Add(BuildRowCvd(r, items))
        rightCol.Controls.Add(BuildRowMicroCvd(r, items))
        rightCol.Controls.Add(BuildRowTfi(r, items))
        rightCol.Controls.Add(BuildRowEma200(r, items))

        rightCol.Controls.Add(MakeTierLabel("TIER 3"))
        rightCol.Controls.Add(BuildRowDonchian(r, items))
        rightCol.Controls.Add(BuildRowObv(r, items))
        rightCol.Controls.Add(BuildRowVpfr(r, items))
        rightCol.Controls.Add(BuildRowSwingPivots(r, items))

        ' --- Footer aggregates ---
        outer.Controls.Add(BuildBreakdownFooter(v, r, items), 0, 3)

        ' --- TOTAL row ---
        Dim shownMax As Integer = If(v.MaxScore > 0, v.MaxScore, 20)
        Dim total = New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = $"TOTAL          Long {v.LongScore}/{shownMax}  |  Short {v.ShortScore}/{shownMax}",
            .Font = Theme.FontMono(12.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_PRIMARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 0, 0),
            .Margin = New Padding(0)
        }
        outer.Controls.Add(total, 0, 4)

        _cardSignalBreakdown.Controls.Add(outer)
        _cardSignalBreakdown.ResumeLayout(True)
    End Sub

    ' -----------------------------------------------------------------------
    ' Layout helpers
    ' -----------------------------------------------------------------------

    Private Shared Function MakeBreakdownColumnHeader() As Label
        Dim text As String = String.Format("{0,-14}  {1,-6}  {2}   {3}",
                                            "INDICATOR", "STATE", "NOTE", "SC")
        Return New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = text,
            .Font = Theme.FontMono(8.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(4, 0, 4, 0)
        }
    End Function

    Private Shared Function NewBreakdownColumn(rightSide As Boolean) As FlowLayoutPanel
        Return New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent,
            .Margin = If(rightSide, New Padding(4, 0, 0, 0), New Padding(0, 0, 4, 0)),
            .Padding = New Padding(0)
        }
    End Function

    Private Shared Function MakeTierLabel(text As String) As Label
        ' Left-accented tier separator: small uppercase bold label, vertical
        ' breathing room. Width is the flow column width; the FlowLayoutPanel
        ' sets Width automatically once it lays out.
        Return New Label() With {
            .AutoSize = False,
            .Width = 500,
            .Height = 16,
            .Text = "  " & text,
            .Font = Theme.FontMono(9.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0, 5, 0, 3)
        }
    End Function

    ''' <summary>
    ''' Build one inline signal row. Composite mono-formatted Label with
    ''' fixed-width columns; full row colour follows state. If subNote is
    ''' provided, the row's height grows and a second indented line renders.
    ''' </summary>
    Private Shared Function MakeSignalRow(label As String,
                                          state As String,
                                          stateColour As Color,
                                          note As String,
                                          sc As Integer?,
                                          Optional subNote As String = Nothing,
                                          Optional subNoteColour As Color = Nothing) As Control
        Dim trimLabel As String = If(label, "")
        If trimLabel.Length > 14 Then trimLabel = trimLabel.Substring(0, 14)
        Dim trimState As String = If(state, "")
        If trimState.Length > 8 Then trimState = trimState.Substring(0, 8)

        ' SC cell encodes three states:
        '   sc Is Nothing     → "—"  (this row didn't vote — display-only)
        '   sc.Value <> 0     → "+1" / "-1"  (signed vote)
        '   sc.Value = 0      → " 0" (vote measured zero)
        ' "—" is rendered in FG_DIM via a separate label overlay so the rest
        ' of the row keeps its state-driven colour.
        Dim hasVote As Boolean = sc.HasValue
        Dim scText As String
        If Not hasVote Then
            scText = " —"
        ElseIf sc.Value > 0 Then
            scText = "+" & sc.Value.ToString()
        ElseIf sc.Value < 0 Then
            scText = sc.Value.ToString()
        Else
            scText = " 0"
        End If
        Dim noteText As String = If(note, "")

        ' Composite text omits the SC for non-voting rows; that cell is drawn
        ' as a separate overlay label in FG_DIM so the dim "—" reads as
        ' explicitly not-a-score rather than a faded number.
        Dim text As String
        If hasVote Then
            text = String.Format("{0,-14}  {1,-6}  {2,-32} {3,3}",
                                 trimLabel, trimState, noteText, scText)
        Else
            text = String.Format("{0,-14}  {1,-6}  {2,-32}    ",
                                 trimLabel, trimState, noteText)
        End If

        Dim rowH As Integer = SIGROW_HEIGHT
        If Not String.IsNullOrEmpty(subNote) Then rowH = SIGROW_HEIGHT + SIGROW_SUBNOTE_HEIGHT

        Dim row = New Panel() With {
            .Width = 500,
            .Height = rowH,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }

        Dim mainLbl = New Label() With {
            .AutoSize = False,
            .Location = New Point(2, 0),
            .Size = New Size(496, SIGROW_HEIGHT),
            .Text = text,
            .Font = Theme.FontMono(9.5F, FontStyle.Regular),
            .ForeColor = stateColour,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        row.Controls.Add(mainLbl)

        ' Em-dash overlay for non-voting rows. Positioned over the SC slot
        ' (right edge of the row) in FG_DIM to read as "no vote here."
        If Not hasVote Then
            Dim dashLbl = New Label() With {
                .AutoSize = False,
                .Location = New Point(470, 0),
                .Size = New Size(28, SIGROW_HEIGHT),
                .Text = "—",
                .Font = Theme.FontMono(9.5F, FontStyle.Regular),
                .ForeColor = Theme.FG_DIM,
                .BackColor = Color.Transparent,
                .TextAlign = ContentAlignment.MiddleRight
            }
            row.Controls.Add(dashLbl)
            dashLbl.BringToFront()
        End If

        If Not String.IsNullOrEmpty(subNote) Then
            Dim subLbl = New Label() With {
                .AutoSize = False,
                .Location = New Point(20, SIGROW_HEIGHT),
                .Size = New Size(478, SIGROW_SUBNOTE_HEIGHT),
                .Text = "↳ " & subNote,
                .Font = Theme.FontMono(8.5F, FontStyle.Regular),
                .ForeColor = If(subNoteColour.IsEmpty, Theme.FG_QUATERNARY, subNoteColour),
                .BackColor = Color.Transparent,
                .TextAlign = ContentAlignment.MiddleLeft,
                .AutoEllipsis = True
            }
            row.Controls.Add(subLbl)
        End If

        Return row
    End Function

    ' -----------------------------------------------------------------------
    ' Per-indicator row builders.  Each derives state + colour + note from
    ' the IndicatorResults fields (B-architecture per gap checklist).
    ' SC comes from finding the matching SignalBreakdownItem and computing
    ' signed total from LongHit / ShortHit.
    ' -----------------------------------------------------------------------

    Private Shared Function ScForItem(items As List(Of SignalBreakdownItem), label As String) As Integer
        For Each it In items
            If it Is Nothing OrElse it.Label Is Nothing Then Continue For
            If String.Equals(it.Label, label, StringComparison.OrdinalIgnoreCase) Then
                If it.LongHit AndAlso Not it.ShortHit Then Return 1
                If it.ShortHit AndAlso Not it.LongHit Then Return -1
                Return 0
            End If
        Next
        Return 0
    End Function

    ''' <summary>
    ''' Like ScForItem but matches "ADX>{N}" labels which carry a numeric
    ''' threshold suffix (so case-equality misses them).
    ''' </summary>
    Private Shared Function ScForItemPrefix(items As List(Of SignalBreakdownItem), prefix As String) As Integer
        For Each it In items
            If it Is Nothing OrElse it.Label Is Nothing Then Continue For
            If it.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                If it.LongHit AndAlso Not it.ShortHit Then Return 1
                If it.ShortHit AndAlso Not it.LongHit Then Return -1
                Return 0
            End If
        Next
        Return 0
    End Function

    Private Shared Function FindItem(items As List(Of SignalBreakdownItem), label As String) As SignalBreakdownItem
        For Each it In items
            If it Is Nothing OrElse it.Label Is Nothing Then Continue For
            If String.Equals(it.Label, label, StringComparison.OrdinalIgnoreCase) Then Return it
        Next
        Return Nothing
    End Function

    Private Shared Function FormatUsdShort(usd As Double) As String
        If usd >= 1_000_000.0 Then Return "$" & (usd / 1_000_000.0).ToString("F1") & "M"
        If usd >= 1_000.0 Then Return "$" & (usd / 1_000.0).ToString("F1") & "K"
        Return "$" & usd.ToString("F0")
    End Function

    ' --- CORE tier ---

    Private Shared Function BuildRowRoc(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        If r.ROC > 0 Then
            state = "BULL"
            colour = Theme.ACC_STRONG_LONG
        ElseIf r.ROC < 0 Then
            state = "BEAR"
            colour = Theme.ACC_SHORT
        Else
            state = "NEUT"
            colour = Theme.FG_TERTIARY
        End If
        Dim note As String = String.Format("{0:+0.000;-0.000;0.000}% {1}", r.ROC, If(r.ROCSlope, "").ToLower())
        Return MakeSignalRow("ROC(9)", state, colour, note, ScForItem(items, "ROC(9)"))
    End Function

    Private Shared Function BuildRowRsi(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        If r.RSI > 70 Then
            state = "BEAR"
            colour = Theme.ACC_SHORT
        ElseIf r.RSI < 30 Then
            state = "BULL"
            colour = Theme.ACC_STRONG_LONG
        Else
            state = "NEUT"
            colour = Theme.FG_TERTIARY
        End If
        Dim note As String = r.RSI.ToString("F1")
        If Not String.IsNullOrEmpty(r.RSIDivergence) AndAlso r.RSIDivergence <> "NONE" Then
            note &= "  div:" & r.RSIDivergence.ToLower()
        End If
        Return MakeSignalRow("RSI(9)", state, colour, note, ScForItem(items, "RSI(9)"))
    End Function

    Private Shared Function BuildRowRsiDiv(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim div As String = If(r.RSIDivergence, "NONE")
        Dim state As String, colour As Color
        If div.Contains("BULL") Then
            state = "BULL"
            colour = Theme.ACC_STRONG_LONG
        ElseIf div.Contains("BEAR") Then
            state = "BEAR"
            colour = Theme.ACC_SHORT
        Else
            state = "—"
            colour = Theme.FG_TERTIARY
        End If
        ' RSI Div isn't a separate scoring item — non-voting row (SC "—").
        Return MakeSignalRow("RSI Div", state, colour, div.ToLower(), CType(Nothing, Integer?))
    End Function

    Private Shared Function BuildRowDmiAdx(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        If r.ADX < 20 Then
            state = "NEUT" : colour = Theme.FG_TERTIARY
        ElseIf r.PlusDI > r.MinusDI AndAlso r.ADX >= 25 Then
            state = "BULL" : colour = Theme.ACC_STRONG_LONG
        ElseIf r.MinusDI > r.PlusDI AndAlso r.ADX >= 25 Then
            state = "BEAR" : colour = Theme.ACC_SHORT
        Else
            state = "MIXED" : colour = Theme.ACC_WARN
        End If
        Dim note As String = $"ADX {r.ADX:F0}"
        ' Engine emits two items ("DMI +/-DI" and "ADX>{N}") — sum their SCs.
        Dim sc As Integer = ScForItem(items, "DMI +/-DI") + ScForItemPrefix(items, "ADX>")
        If sc > 1 Then sc = 1
        If sc < -1 Then sc = -1
        Return MakeSignalRow("DMI/ADX", state, colour, note, sc)
    End Function

    Private Shared Function BuildRowVolume(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        If r.VolumeRatio >= 1.5 Then
            state = "HIGH"
            colour = Theme.ACC_STRONG_LONG
        ElseIf r.VolumeRatio < 0.7 Then
            state = "LOW"
            colour = Theme.ACC_SHORT
        Else
            state = "NORM"
            colour = Theme.FG_TERTIARY
        End If
        Dim note As String = $"{r.VolumeRatio:F1}× {FormatUsdShort(r.CurrentVolumeUSD)}"
        Return MakeSignalRow("Volume", state, colour, note, ScForItem(items, "Volume"))
    End Function

    ' --- TIER 1 ---

    Private Shared Function BuildRowVwapDev(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        If r.VWAPDevPct > 0 Then
            state = "LONG"
            colour = Theme.ACC_STRONG_LONG
        ElseIf r.VWAPDevPct < 0 Then
            state = "SHORT"
            colour = Theme.ACC_SHORT
        Else
            state = "NEUT"
            colour = Theme.FG_TERTIARY
        End If
        Dim note As String = String.Format("{0:+0.00;-0.00;0.00}%", r.VWAPDevPct)
        ' VWAP scoring fires as a single "VWAP" breakdown item — split between
        ' VWAP Dev and VWAP Bands rows. Attribute the SC to VWAP Dev (primary).
        Return MakeSignalRow("VWAP Dev", state, colour, note, ScForItem(items, "VWAP"))
    End Function

    Private Shared Function BuildRowVwapBands(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        ' Band zone from current price vs σ1/σ2 bounds. SC=0 here — the VWAP
        ' breakdown item's SC already counted on the Dev row above.
        Dim p As Double = r.CurrentPrice
        Dim state As String, colour As Color, note As String
        If p > r.VWAPSigma2Upper Then
            state = "OB σ2" : colour = Theme.ACC_SHORT : note = "above σ2 upper"
        ElseIf p > r.VWAPSigma1Upper Then
            state = "σ1 up" : colour = Theme.ACC_WARN : note = "σ1–σ2 upper"
        ElseIf p < r.VWAPSigma2Lower Then
            state = "OS σ2" : colour = Theme.ACC_STRONG_LONG : note = "below σ2 lower"
        ElseIf p < r.VWAPSigma1Lower Then
            state = "σ1 dn" : colour = Theme.ACC_WARN : note = "σ1–σ2 lower"
        Else
            state = "inside" : colour = Theme.FG_TERTIARY : note = "within σ1"
        End If
        ' VWAP Bands is state-only — the engine's single "VWAP" item's SC
        ' is attributed to the VWAP Dev row above.
        Return MakeSignalRow("VWAP Bands", state, colour, note, CType(Nothing, Integer?))
    End Function

    Private Shared Function BuildRowBbwTtm(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.SqueezeStatus, "")
            Case "RELEASING" : state = "RELEASE" : colour = Theme.ACC_STRONG_LONG
            Case "ACTIVE"    : state = "ACTIVE"  : colour = Theme.ACC_WARN
            Case Else        : state = "NEUT"    : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = If(r.SqueezeStatus, "—").ToLower()
        Return MakeSignalRow("BBW/TTM", state, colour, note, ScForItem(items, "BBW/TTM"))
    End Function

    Private Shared Function BuildRowEmaRibbon(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color, note As String
        Select Case If(r.EMAAlignment, "")
            Case "BULL" : state = "BULL"  : colour = Theme.ACC_STRONG_LONG : note = "9>21>50"
            Case "BEAR" : state = "BEAR"  : colour = Theme.ACC_SHORT       : note = "50>21>9"
            Case Else   : state = "MIXED" : colour = Theme.ACC_WARN        : note = "mixed"
        End Select
        Return MakeSignalRow("EMA Ribbon", state, colour, note, ScForItem(items, "EMA 9/21/50"))
    End Function

    Private Shared Function BuildRowTrendStr(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color, note As String
        Select Case r.TrendStructure
            Case TrendStructure.UPTREND     : state = "UP"      : colour = Theme.ACC_STRONG_LONG       : note = "HH/HL"
            Case TrendStructure.DOWNTREND   : state = "DOWN"    : colour = Theme.ACC_SHORT             : note = "LH/LL"
            Case TrendStructure.EXPANSION   : state = "EXPAND"  : colour = Theme.ACC_WARN              : note = "HH/LL"
            Case TrendStructure.CONTRACTION : state = "CONTR"   : colour = Color.FromArgb(80, 200, 210) : note = "LH/HL"
            Case Else                       : state = "—"       : colour = Theme.FG_QUATERNARY        : note = "insuff."
        End Select
        Return MakeSignalRow("Trend Str", state, colour, note, ScForItem(items, "Trend Structure"))
    End Function

    Private Shared Function BuildRowFunding(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim bias As String = If(r.FundingBias, "")
        Dim state As String, colour As Color
        If bias.Contains("HEAVILY") Then
            state = "HEAVY"
            colour = Theme.ACC_WARN
        ElseIf bias = "NEUTRAL" OrElse bias = "" Then
            state = "NEUT"
            colour = Theme.FG_TERTIARY
        Else
            state = "CROWD"
            colour = Theme.FG_TERTIARY
        End If
        Dim clamped As Double = If(Math.Abs(r.FundingRate) < 1.0E-8, 0.0, r.FundingRate)
        Dim note As String = String.Format("{0:+0.0000;-0.0000;0.0000}%", clamped * 100.0)
        ' Funding is not a Step 2 vote — the engine's Step 3 modifier
        ' surfaces through the VERDICT card's eff. L/M sub-row. Non-voting
        ' display row (SC "—").
        Return MakeSignalRow("Funding", state, colour, note, CType(Nothing, Integer?))
    End Function

    Private Shared Function BuildRowFundingMom(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.FundingMomentum, "")
            Case "RISING"  : state = "RISE"  : colour = Theme.ACC_WARN
            Case "FALLING" : state = "FALL"  : colour = Theme.ACC_STRONG_LONG
            Case Else      : state = "FLAT"  : colour = Theme.FG_TERTIARY
        End Select
        ' Funding Mom is a Step 3b adjunct, not a standalone vote — non-
        ' voting display row (SC "—").
        Return MakeSignalRow("Funding Mom", state, colour, "step 3b", CType(Nothing, Integer?))
    End Function

    Private Shared Function BuildRowOiChange(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.OISignal, "")
            Case "NEW LONGS"   : state = "NEW LO" : colour = Theme.ACC_STRONG_LONG
            Case "COVERING"    : state = "COVER"  : colour = Theme.ACC_STRONG_LONG
            Case "NEW SHORTS"  : state = "NEW SH" : colour = Theme.ACC_SHORT
            Case "CAPITULATION": state = "CAPIT"  : colour = Theme.ACC_SHORT
            Case Else          : state = "NEUT"   : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = String.Format("{0:+0.0;-0.0;0.0}%", r.OIChange15m)
        Return MakeSignalRow("OI Change", state, colour, note, ScForItem(items, "OI Delta"))
    End Function

    ' --- TIER 2 ---

    Private Shared Function BuildRowSpread(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.SpreadStatus, "")
            Case "TIGHT"  : state = "TIGHT"  : colour = Theme.ACC_STRONG_LONG
            Case "WIDE"   : state = "WIDE"   : colour = Theme.ACC_SHORT
            Case Else     : state = "NORM"   : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = $"{r.SpreadBps:F1} bps"
        Return MakeSignalRow("Spread", state, colour, note, ScForItem(items, "Spread"))
    End Function

    Private Shared Function BuildRowOfi(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.OFISignal, "")
            Case "BUY DOMINANT"  : state = "BULL" : colour = Theme.ACC_STRONG_LONG
            Case "SELL DOMINANT" : state = "BEAR" : colour = Theme.ACC_SHORT
            Case Else            : state = "BAL"  : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = $"ratio {r.OFIRatio:F2}"
        Return MakeSignalRow("OFI", state, colour, note, ScForItem(items, "OFI"))
    End Function

    Private Shared Function BuildRowOfiMom(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.OFIMomentum, "")
            Case "RISING"  : state = "RISE" : colour = Theme.ACC_STRONG_LONG
            Case "FALLING" : state = "FALL" : colour = Theme.ACC_SHORT
            Case Else      : state = "FLAT" : colour = Theme.FG_TERTIARY
        End Select
        ' OFI Mom is a momentum derivative — non-voting display row (SC "—").
        Return MakeSignalRow("OFI Mom", state, colour, "", CType(Nothing, Integer?))
    End Function

    Private Shared Function BuildRowLiq(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim sig As String = If(r.LiqSignal, "NONE")
        Dim state As String, colour As Color
        Select Case sig
            Case "LONG LIQS"  : state = "L LIQ" : colour = Theme.ACC_SHORT     ' longs liquidating = bearish
            Case "SHORT LIQS" : state = "S LIQ" : colour = Theme.ACC_STRONG_LONG ' shorts liquidating = bullish
            Case Else         : state = "NONE"  : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = sig.ToLower()
        Return MakeSignalRow("Liq", state, colour, note, ScForItem(items, "Liq Penalty"))
    End Function

    Private Shared Function BuildRowCvd(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.CVDSlope, "")
            Case "RISING"  : state = "BULL" : colour = Theme.ACC_STRONG_LONG
            Case "FALLING" : state = "BEAR" : colour = Theme.ACC_SHORT
            Case Else      : state = "FLAT" : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = String.Format("{0:+0.0;-0.0;0.0}k", r.CVDValue / 1000.0)
        If Not String.IsNullOrEmpty(r.CVDDivergence) AndAlso r.CVDDivergence <> "NONE" Then
            note &= "  div:" & r.CVDDivergence.ToLower()
        End If
        Return MakeSignalRow("CVD", state, colour, note, ScForItem(items, "CVD"))
    End Function

    Private Shared Function BuildRowMicroCvd(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        ' GAP-82 — restore 4-state colour distinction. BULL_DECEL gets a pale-
        ' green tint so a fading bull momentum reads visibly different from
        ' BULL_ACCEL (bright green). Mirror for BEAR.
        Dim state As String, colour As Color
        Select Case If(r.MicroCVDSignal, "")
            Case "BULL_ACCEL" : state = "BULL+"  : colour = Theme.ACC_STRONG_LONG
            Case "BEAR_ACCEL" : state = "BEAR+"  : colour = Theme.ACC_SHORT
            Case "BULL_DECEL" : state = "BULL-"  : colour = Color.FromArgb(120, 200, 120)
            Case "BEAR_DECEL" : state = "BEAR-"  : colour = Color.FromArgb(220, 130, 130)
            Case Else         : state = "FLAT"   : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = If(r.MicroCVDMomentum, "").ToLower()
        Return MakeSignalRow("MicroCVD", state, colour, note, ScForItem(items, "MicroCVD"))
    End Function

    Private Shared Function BuildRowTfi(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.TFISignal, "")
            Case "BUY PRESSURE"  : state = "BUY"  : colour = Theme.ACC_STRONG_LONG
            Case "SELL PRESSURE" : state = "SELL" : colour = Theme.ACC_SHORT
            Case Else            : state = "NEUT" : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String = r.TFIValue.ToString("F2")
        Return MakeSignalRow("TFI", state, colour, note, ScForItem(items, "TFI"))
    End Function

    Private Shared Function BuildRowEma200(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.PriceVsEMA200, "")
            Case "ABOVE" : state = "ABOVE" : colour = Theme.ACC_STRONG_LONG
            Case "BELOW" : state = "BELOW" : colour = Theme.ACC_SHORT
            Case Else    : state = "—"     : colour = Theme.FG_TERTIARY
        End Select
        Return MakeSignalRow("EMA200 5m", state, colour, "", ScForItem(items, "5m EMA(200)"))
    End Function

    ' --- TIER 3 ---

    Private Shared Function BuildRowDonchian(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.DonchianSignal, "")
            Case "LONG", "LONG_PARTIAL"   : state = "LONG"  : colour = Theme.ACC_STRONG_LONG
            Case "SHORT", "SHORT_PARTIAL" : state = "SHORT" : colour = Theme.ACC_SHORT
            Case Else                     : state = "NEUT"  : colour = Theme.FG_TERTIARY
        End Select
        ' Quartile derived inline from price position.
        Dim note As String
        Dim range As Double = r.DonchianUpper - r.DonchianLower
        If range <= 0 Then
            note = "no range"
        Else
            Dim posPct As Double = (r.CurrentPrice - r.DonchianLower) / range
            If posPct >= 0.75 Then
                note = "upper qtr"
            ElseIf posPct <= 0.25 Then
                note = "lower qtr"
            Else
                note = "mid"
            End If
        End If
        Return MakeSignalRow("Donchian", state, colour, note, ScForItem(items, "Donchian(20)"))
    End Function

    Private Shared Function BuildRowObv(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim state As String, colour As Color
        Select Case If(r.OBVTrend, "")
            Case "RISING"  : state = "BULL" : colour = Theme.ACC_STRONG_LONG
            Case "FALLING" : state = "BEAR" : colour = Theme.ACC_SHORT
            Case Else      : state = "FLAT" : colour = Theme.FG_TERTIARY
        End Select
        Dim note As String
        If Not String.IsNullOrEmpty(r.OBVDivergence) AndAlso r.OBVDivergence <> "NONE" Then
            note = "div:" & r.OBVDivergence.ToLower()
        Else
            note = "no div"
        End If
        Return MakeSignalRow("OBV", state, colour, note, ScForItem(items, "OBV"))
    End Function

    Private Shared Function BuildRowVpfr(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim sig As String = If(r.VPFRSignal, "")
        Dim state As String, colour As Color
        If sig = "NEAR_HVN_SUPPORT" OrElse sig = "IN_LVN_BULL" Then
            state = "BULL" : colour = Theme.ACC_STRONG_LONG
        ElseIf sig = "NEAR_HVN_RESIST" OrElse sig = "IN_LVN_BEAR" Then
            state = "BEAR" : colour = Theme.ACC_SHORT
        Else
            state = "NEUT" : colour = Theme.FG_TERTIARY
        End If
        Dim note As String = If(r.VPFRValueAreaSignal, "").ToLower().Replace("_", " ")
        Return MakeSignalRow("VPFR", state, colour, note, ScForItem(items, "VPFR-lite"))
    End Function

    Private Shared Function BuildRowSwingPivots(r As IndicatorResults, items As List(Of SignalBreakdownItem)) As Control
        Dim has5m  As Boolean = r.LastSwingHigh5m  > 0 OrElse r.LastSwingLow5m  > 0
        Dim has15m As Boolean = r.LastSwingHigh15m > 0 OrElse r.LastSwingLow15m > 0
        Dim state As String, colour As Color, note As String
        If has5m AndAlso has15m Then
            state = "BOTH" : colour = Theme.ACC_STRONG_LONG : note = "5m+15m"
        ElseIf has5m Then
            state = "5m"   : colour = Theme.ACC_WARN        : note = "5m only"
        ElseIf has15m Then
            state = "15m"  : colour = Theme.ACC_WARN        : note = "15m only"
        Else
            state = "NONE" : colour = Theme.FG_TERTIARY     : note = "no pivots"
        End If

        Dim subNote As String = Nothing
        If r.BestPivotByVolume5m > 0 Then
            subNote = String.Format("best vol: {0} @ {1:F0} ({2:F1}×)",
                                    If(r.BestPivotIsHigh5m, "HIGH", "LOW"),
                                    r.BestPivotByVolume5m, r.BestPivotVolumeRatio5m)
        End If
        ' Swing Pivots aren't a SignalBreakdownItem — non-voting display row.
        Return MakeSignalRow("Swing Pivots", state, colour, note, CType(Nothing, Integer?),
                             subNote:=subNote,
                             subNoteColour:=Theme.ACC_INFO)
    End Function

    ' -----------------------------------------------------------------------
    ' Footer aggregates (OI × CVD, Pass 2c, Funding Mom)
    ' -----------------------------------------------------------------------

    Private Shared Function BuildBreakdownFooter(v As VerdictResult,
                                                 r As IndicatorResults,
                                                 items As List(Of SignalBreakdownItem)) As Control
        Dim panel = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 5,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 4, 0, 0)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 1))    ' divider
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))   ' OI × CVD
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))   ' Pass 2c
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))   ' Funding Mom
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 1))    ' divider

        panel.Controls.Add(NewDivider(), 0, 0)

        ' OI × CVD
        Dim oiOutcome As String = If(v.OiCvdOutcome, "NONE").ToUpper()
        Dim oiState As String, oiColour As Color, oiTag As String
        Select Case oiOutcome
            Case "CONFIRMED_LONG"  : oiState = "● CONFIRMED LONG"  : oiColour = Theme.ACC_STRONG_LONG : oiTag = "+1 bonus"
            Case "CONFIRMED_SHORT" : oiState = "● CONFIRMED SHORT" : oiColour = Theme.ACC_SHORT       : oiTag = "+1 bonus"
            Case "CONFLICT_LONG", "CONFLICT_SHORT", "CONFLICT"
                                    oiState = "⚠ CONFLICT"          : oiColour = Theme.ACC_WARN        : oiTag = "−1 penalty"
            Case Else              : oiState = "○ NEUTRAL"          : oiColour = Theme.ACC_NEUTRAL     : oiTag = "no signal"
        End Select
        panel.Controls.Add(MakeFooterAggregate("OI × CVD", oiState, oiColour, oiTag), 0, 1)

        ' Pass 2c — derive from "Regime Align (2c)" item (no Pass2cOutcome
        ' field on VerdictResult). The 3-state Pass2cBadge can't encode
        ' direction, so emit a separate ↑/↓ arrow next to the badge when
        ' ALIGNED on a specific side.
        Dim p2cItem = FindItem(items, "Regime Align (2c)")
        Dim p2cState As String, p2cColour As Color, p2cTag As String
        Dim p2cArrow As String = "", p2cArrowColour As Color = Color.Empty
        If p2cItem Is Nothing Then
            p2cState = "SUPPRESSED"
            p2cColour = Theme.ACC_NEUTRAL
            p2cTag = ""
        ElseIf p2cItem.LongHit AndAlso p2cItem.ShortHit Then
            p2cState = "CONFLICT"
            p2cColour = Theme.ACC_SHORT
            p2cTag = "−1 regime"
        ElseIf p2cItem.LongHit Then
            p2cState = "ALIGNED"
            p2cColour = Theme.ACC_STRONG_LONG
            p2cTag = "+1 regime"
            p2cArrow = "↑"
            p2cArrowColour = Theme.ACC_STRONG_LONG
        ElseIf p2cItem.ShortHit Then
            p2cState = "ALIGNED"
            p2cColour = Theme.ACC_STRONG_LONG
            p2cTag = "+1 regime"
            p2cArrow = "↓"
            p2cArrowColour = Theme.ACC_SHORT
        Else
            p2cState = "SUPPRESSED"
            p2cColour = Theme.ACC_NEUTRAL
            p2cTag = ""
        End If
        panel.Controls.Add(MakeFooterAggregate("Pass 2c", p2cState, p2cColour, p2cTag,
                                               arrow:=p2cArrow,
                                               arrowColour:=p2cArrowColour), 0, 2)

        ' Funding Mom
        Dim fmDir As String = If(r.FundingMomentum, "FLAT").ToUpper()
        Dim fmState As String, fmColour As Color
        Select Case fmDir
            Case "RISING"  : fmState = "↑ RISING"  : fmColour = Theme.ACC_WARN
            Case "FALLING" : fmState = "↓ FALLING" : fmColour = Theme.ACC_STRONG_LONG
            Case Else      : fmState = "— FLAT"    : fmColour = Theme.FG_TERTIARY
        End Select
        panel.Controls.Add(MakeFooterAggregate("Funding Mom", fmState, fmColour, "step 3b"), 0, 3)

        panel.Controls.Add(NewDivider(), 0, 4)
        Return panel
    End Function

    Private Shared Function MakeFooterAggregate(label As String, stateText As String,
                                                stateColour As Color, tag As String,
                                                Optional arrow As String = "",
                                                Optional arrowColour As Color = Nothing) As Control
        Dim row = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))   ' label
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140))   ' state
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F)) ' arrow + spacer
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130))   ' tag
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        row.Controls.Add(New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = label,
            .Font = Theme.FontMono(9.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(8, 0, 0, 0)
        }, 0, 0)

        row.Controls.Add(New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = stateText,
            .Font = Theme.FontMono(10.0F, FontStyle.Bold),
            .ForeColor = stateColour,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }, 1, 0)

        ' Arrow column — independent colour so e.g. ALIGNED-SHORT renders
        ' the badge text green but the arrow red.
        Dim arrowText As String = If(arrow, "")
        row.Controls.Add(New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = arrowText,
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = If(arrowColour.IsEmpty, Theme.FG_TERTIARY, arrowColour),
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }, 2, 0)

        row.Controls.Add(New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = If(tag, ""),
            .Font = Theme.FontMono(9.0F, FontStyle.Regular),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleRight,
            .Margin = New Padding(0, 0, 8, 0)
        }, 3, 0)

        Return row
    End Function

    Private Shared Function NewDivider() As Panel
        Return New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Theme.BORDER_INNER,
            .Height = 1,
            .Margin = New Padding(8, 2, 8, 2)
        }
    End Function

End Class
