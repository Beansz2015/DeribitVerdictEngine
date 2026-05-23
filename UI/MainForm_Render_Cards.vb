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
        inner.RowStyles.Add(New RowStyle(SizeType.AutoSize))         ' eff/penalty sub-row (collapses to 0 when label hidden)
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
            ' N1 to preserve the ".5" tick precision the legacy renders (e.g.
            ' "$76,888.5"). N0 was rounding to "$76,888" — info loss.
            _lblLastPrice.Text = If(displayPrice > 0, $"${displayPrice:N1}", "—")
        End If
        If _lblLastPriceAtr IsNot Nothing Then
            ' Legacy renders ATR at F2 (e.g. "11.58"). F1 was rounding to "11.6".
            _lblLastPriceAtr.Text = $"ATR {r.ATR:F2}"
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
    ' KELLY SIZING card (P4d commit 2). Gaps GAP-07..16.
    ' Hidden entirely when v.KellyPWin <= 0. Otherwise renders header (with
    ' optional [BIAS ONLY] / [CAPPED] tags) + 2-line ATR-basis advisory +
    ' six KV rows ending in Contracts/Lean with singular/plural handling.
    ' =======================================================================
    Public Sub BindCardKelly(v As VerdictResult)
        If _cardKelly Is Nothing Then Return

        _cardKelly.SuspendLayout()
        _cardKelly.Controls.Clear()

        ' GAP-15: hide entire card when there's no Kelly result.
        If v.KellyPWin <= 0 Then
            _cardKelly.Visible = False
            _cardKelly.ResumeLayout(True)
            Return
        End If
        _cardKelly.Visible = True

        Dim verdict As String = If(v.Verdict, "")
        Dim isNoTradeBias As Boolean = verdict.StartsWith("NO TRADE", StringComparison.OrdinalIgnoreCase)

        ' Vertical stack — header / advisory / KV rows.
        Dim stack As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }

        ' GAP-11, GAP-12: section header with optional bias / capped tags.
        Dim biasTag As String = If(isNoTradeBias, "  [BIAS ONLY — NO TRADE]", "")
        Dim capTag  As String = If(v.KellyCapped, "  [CAPPED]", "")
        stack.Controls.Add(BuildCardHeaderWithTags("KELLY SIZING", biasTag, capTag))

        ' GAP-10: 2-line ATR-basis advisory.
        stack.Controls.Add(BuildCardAdvisory(
            "Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets.",
            "Treat as directional bias indicator only."))

        ' KV rows: p(win), f*/Half-Kelly, Applied fraction, Risk $, Contracts/Lean.
        stack.Controls.Add(BuildCardKvRow("p(win):",          v.KellyPWin.ToString("P1")))
        stack.Controls.Add(BuildCardKvRow("f* / Half-Kelly:", $"{v.KellyF:P2}  /  {v.KellyFHalf:P2}"))
        stack.Controls.Add(BuildCardKvRow("Applied fraction:", v.KellyFApplied.ToString("P2")))
        stack.Controls.Add(BuildCardKvRow("Risk $:",          $"${v.KellyRiskUsd:F2}"))

        ' GAP-13, GAP-14, GAP-16: Lean/Contracts label switch, <1-contract
        ' fallback, singular/plural agreement.
        Dim leanOrContracts As String = If(isNoTradeBias, "Lean:", "Contracts:")
        Dim contractStr As String
        Dim contractColour As Color = If(v.KellyContracts >= 1, Theme.ACC_STRONG_LONG, Theme.ACC_WARN)
        If isNoTradeBias Then
            If v.KellyContracts >= 1 Then
                Dim unit As String = If(v.KellyContracts = 1, "contract", "contracts")
                contractStr = $"{v.KellyContracts} {unit}  (not a trade signal)"
            Else
                contractStr = "< 1 contract  (bias only; not a trade signal)"
            End If
        Else
            If v.KellyContracts >= 1 Then
                Dim unit As String = If(v.KellyContracts = 1, "contract", "contracts")
                contractStr = $"{v.KellyContracts} {unit}"
            Else
                contractStr = "< 1 contract  (stop too wide for min size)"
            End If
        End If
        stack.Controls.Add(BuildCardKvRow(leanOrContracts, contractStr, contractColour))

        _cardKelly.Controls.Add(stack)
        _cardKelly.ResumeLayout(True)
    End Sub

    ' -----------------------------------------------------------------------
    ' Card composition helpers — reused by KELLY card (commit 2) and the
    ' INDICATOR DETAILS sub-groups (commit 4).
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Header line "MAIN  [BIAS TAG]  [CAP TAG]" — bold mono, ACC_WARN tags.
    ''' Each tag string is rendered verbatim (caller includes its own leading
    ''' spaces); empty tag strings are omitted.
    ''' </summary>
    Private Shared Function BuildCardHeaderWithTags(mainText As String,
                                                    biasTag As String,
                                                    capTag As String) As Control
        Dim row As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 4),
            .Padding = New Padding(0)
        }
        row.Controls.Add(New Label() With {
            .AutoSize = True,
            .Text = mainText,
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_SECONDARY,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        })
        If Not String.IsNullOrEmpty(biasTag) Then
            row.Controls.Add(New Label() With {
                .AutoSize = True,
                .Text = biasTag,
                .Font = Theme.FontMono(10.0F, FontStyle.Bold),
                .ForeColor = Theme.ACC_WARN,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0, 2, 0, 0)
            })
        End If
        If Not String.IsNullOrEmpty(capTag) Then
            row.Controls.Add(New Label() With {
                .AutoSize = True,
                .Text = capTag,
                .Font = Theme.FontMono(10.0F, FontStyle.Bold),
                .ForeColor = Theme.ACC_WARN,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0, 2, 0, 0)
            })
        End If
        Return row
    End Function

    ''' <summary>Two-line dim advisory text in FG_QUATERNARY at 9pt.</summary>
    Private Shared Function BuildCardAdvisory(line1 As String, line2 As String) As Control
        Dim panel As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(0)
        }
        For Each line In {line1, line2}
            If String.IsNullOrEmpty(line) Then Continue For
            panel.Controls.Add(New Label() With {
                .AutoSize = True,
                .Text = line,
                .Font = Theme.FontMono(9.0F, FontStyle.Regular),
                .ForeColor = Theme.FG_QUATERNARY,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0)
            })
        Next
        Return panel
    End Function

    ''' <summary>
    ''' Key-value row: 130 px right-aligned FG_TERTIARY label + flex value
    ''' label. Default value colour FG_PRIMARY; per-row override via the
    ''' optional valueColour. Used by KELLY card and INDICATOR DETAILS
    ''' sub-groups.
    ''' </summary>
    Private Shared Function BuildCardKvRow(label As String,
                                           value As String,
                                           Optional valueColour As Color = Nothing,
                                           Optional indent As Boolean = False,
                                           Optional wrap As Boolean = False) As Control
        Dim row As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 2),
            .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        row.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim labelText As String = If(indent, "", If(label, ""))
        row.Controls.Add(New Label() With {
            .AutoSize = False,
            .Width = 130,
            .Height = 18,
            .Text = labelText,
            .Font = Theme.FontMono(9.5F, FontStyle.Regular),
            .ForeColor = Theme.FG_TERTIARY,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleRight,
            .Margin = New Padding(0, 0, 6, 0),
            .Padding = New Padding(0)
        }, 0, 0)

        Dim vCol As Color = If(valueColour.IsEmpty, Theme.FG_PRIMARY, valueColour)
        Dim valLabel As New Label() With {
            .AutoSize = True,
            .Text = If(value, ""),
            .Font = Theme.FontMono(9.5F, FontStyle.Regular),
            .ForeColor = vCol,
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(If(indent, 12, 0), 0, 0, 0),
            .Padding = New Padding(0)
        }
        If wrap Then
            valLabel.AutoSize = True
            valLabel.MaximumSize = New Size(360, 0)
        End If
        row.Controls.Add(valLabel, 1, 0)
        Return row
    End Function

    ' =======================================================================
    ' VOLUME PROFILE card (P4d commit 3). Gaps GAP-62..66.
    ' Renders the VPFR price-level stack (VAH → HVN↑ → LVN↑ → POC → LVN↓ →
    ' HVN↓ → VAL) with conditional LVN rows when present. Two sub-labels
    ' below the stack: VPFR signal text and value-area signal.
    ' =======================================================================
    Public Sub BindCardVolumeProfile(r As IndicatorResults)
        If _cardVolumeProfile Is Nothing Then Return

        _cardVolumeProfile.SuspendLayout()
        _cardVolumeProfile.Controls.Clear()

        Dim stack As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }

        stack.Controls.Add(BuildPlainSectionHeader("VOLUME PROFILE"))

        ' Price-level rows, top-to-bottom: VAH → HVN↑ → LVN↑ → POC → LVN↓ →
        ' HVN↓ → VAL. LVN/HVN rows only render when their value > 0.
        AddLevelRow(stack, "VAH",  r.VPFRVah,             Theme.ACC_WARN, bold:=False)
        If r.VPFRNearestHvnAbove > 0 Then AddLevelRow(stack, "HVN↑", r.VPFRNearestHvnAbove, Theme.ACC_INFO, bold:=False)
        If r.VPFRNearestLvnAbove > 0 Then AddLevelRow(stack, "LVN↑", r.VPFRNearestLvnAbove, Theme.FG_TERTIARY, bold:=False)
        AddLevelRow(stack, "POC", r.VPFRPoc, Theme.ACC_WARN, bold:=True,
                    suffix:=If(r.VPFRHVNearPoc, "  (HVN@POC)", ""))
        If r.VPFRNearestLvnBelow > 0 Then AddLevelRow(stack, "LVN↓", r.VPFRNearestLvnBelow, Theme.FG_TERTIARY, bold:=False)
        If r.VPFRNearestHvnBelow > 0 Then AddLevelRow(stack, "HVN↓", r.VPFRNearestHvnBelow, Theme.ACC_INFO, bold:=False)
        AddLevelRow(stack, "VAL",  r.VPFRVal,             Theme.ACC_WARN, bold:=False)

        ' GAP-62: VPFR signal sub-label.
        Dim vpfrSig As String = If(r.VPFRSignal, "")
        If vpfrSig.Length > 0 Then
            stack.Controls.Add(BuildSubLabel(vpfrSig.Replace("_"c, " "c).ToLowerInvariant(), Theme.FG_TERTIARY))
        End If

        ' GAP-64: value-area signal sub-label with semantic colour.
        Dim vaSig As String = If(r.VPFRValueAreaSignal, "")
        If vaSig.Length > 0 Then
            Dim vaColour As Color = Theme.FG_TERTIARY
            Select Case vaSig.ToUpperInvariant()
                Case "ABOVE_VAH" : vaColour = Theme.ACC_STRONG_LONG
                Case "BELOW_VAL" : vaColour = Theme.ACC_SHORT
                Case Else        : vaColour = Theme.FG_TERTIARY
            End Select
            stack.Controls.Add(BuildSubLabel(vaSig.Replace("_"c, " "c).ToLowerInvariant(), vaColour))
        End If

        _cardVolumeProfile.Controls.Add(stack)
        _cardVolumeProfile.ResumeLayout(True)
    End Sub

    ''' <summary>Add one VOLUME PROFILE price-level row to the stack.</summary>
    Private Shared Sub AddLevelRow(parent As FlowLayoutPanel,
                                   label As String,
                                   value As Double,
                                   colour As Color,
                                   bold As Boolean,
                                   Optional suffix As String = "")
        Dim row As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 1),
            .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 50))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        row.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim style As FontStyle = If(bold, FontStyle.Bold, FontStyle.Regular)
        row.Controls.Add(New Label() With {
            .AutoSize = False, .Width = 50, .Height = 16,
            .Text = label, .Font = Theme.FontMono(9.5F, style),
            .ForeColor = colour, .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(0)
        }, 0, 0)
        row.Controls.Add(New Label() With {
            .AutoSize = False, .Width = 90, .Height = 16,
            .Text = If(value > 0, value.ToString("F1"), "—"),
            .Font = Theme.FontMono(9.5F, style),
            .ForeColor = If(value > 0, colour, Theme.FG_DIM),
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleRight, .Margin = New Padding(0)
        }, 1, 0)
        If Not String.IsNullOrEmpty(suffix) Then
            row.Controls.Add(New Label() With {
                .AutoSize = True, .Text = suffix,
                .Font = Theme.FontMono(8.5F, FontStyle.Regular),
                .ForeColor = Theme.FG_QUATERNARY,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0)
            }, 2, 0)
        End If
        parent.Controls.Add(row)
    End Sub

    ''' <summary>Small dim sub-label, used below VOLUME PROFILE levels.</summary>
    Private Shared Function BuildSubLabel(text As String, colour As Color) As Label
        Return New Label() With {
            .AutoSize = True,
            .Text = text,
            .Font = Theme.FontMono(9.0F, FontStyle.Regular),
            .ForeColor = colour,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 4, 0, 0)
        }
    End Function

    ''' <summary>Section header for cards built by P4d binders (KELLY,
    ''' VOLUME PROFILE, OI × CVD CROSS, INDICATOR DETAILS). Matches the
    ''' MakeSectionHeader style used by the bound row 3-5 cards.</summary>
    Private Shared Function BuildPlainSectionHeader(text As String) As Label
        Return New Label() With {
            .AutoSize = True,
            .Text = text,
            .Font = Theme.FontMono(11.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_SECONDARY,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 6)
        }
    End Function

    ' =======================================================================
    ' OI × CVD CROSS card (P4d commit 3). Was an empty placeholder.
    ' Outcome badge (OiCvdBadge 4-state) + Funding Mom MiniMeter + Spread
    ' MiniMeter. v.OiCvdOutcome drives the badge; r.FundingMomentum +
    ' r.SpreadBps/SpreadStatus drive the meters.
    ' =======================================================================
    Public Sub BindCardOiCvdCross(r As IndicatorResults, v As VerdictResult)
        If _cardOiCvdCross Is Nothing Then Return

        _cardOiCvdCross.SuspendLayout()
        _cardOiCvdCross.Controls.Clear()

        Dim stack As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }

        stack.Controls.Add(BuildPlainSectionHeader("OI × CVD CROSS"))

        ' Outcome badge + note tag side-by-side.
        Dim outcomeKey As String = If(v.OiCvdOutcome, "NONE").ToUpperInvariant()
        Dim badge As New OiCvdBadge() With {
            .Outcome = MapOiCvdOutcome(outcomeKey),
            .Margin = New Padding(0)
        }
        stack.Controls.Add(BuildBadgeRow(badge, ResolveOiCvdNote(outcomeKey)))

        ' Funding Mom MiniMeter.
        Dim fm As String = If(r.FundingMomentum, "FLAT")
        stack.Controls.Add(BuildMiniMeter("Funding Mom", fm,
                                          ResolveFundMomMagnitude(fm),
                                          ResolveFundMomColour(fm)))

        ' Spread MiniMeter — pct = bps / wide-threshold, clamped to 100.
        Dim wideThresh As Double = SettingsLoader.Current.Indicators.Spread.WideThresholdBps
        Dim spreadPct As Single = 0.0F
        If wideThresh > 0 Then
            spreadPct = CSng(Math.Min(100.0, (r.SpreadBps / wideThresh) * 100.0))
        End If
        stack.Controls.Add(BuildMiniMeter("Spread", $"{r.SpreadBps:F2} bps",
                                          spreadPct,
                                          ResolveSpreadColour(If(r.SpreadStatus, ""))))

        _cardOiCvdCross.Controls.Add(stack)
        _cardOiCvdCross.ResumeLayout(True)
    End Sub

    ''' <summary>Map v.OiCvdOutcome string → OiCvdBadge enum value.</summary>
    Private Shared Function MapOiCvdOutcome(s As String) As OiCvdBadge.OiCvdOutcomeKind
        Select Case s.ToUpperInvariant()
            Case "CONFIRMED_LONG"  : Return OiCvdBadge.OiCvdOutcomeKind.CONFIRMED_LONG
            Case "CONFIRMED_SHORT" : Return OiCvdBadge.OiCvdOutcomeKind.CONFIRMED_SHORT
            Case "CONFLICT", "CONFLICT_LONG", "CONFLICT_SHORT" : Return OiCvdBadge.OiCvdOutcomeKind.CONFLICT
            Case Else              : Return OiCvdBadge.OiCvdOutcomeKind.NEUTRAL
        End Select
    End Function

    Private Shared Function ResolveOiCvdNote(s As String) As String
        Select Case s.ToUpperInvariant()
            Case "CONFIRMED_LONG", "CONFIRMED_SHORT" : Return "+1 bonus"
            Case "CONFLICT", "CONFLICT_LONG", "CONFLICT_SHORT" : Return "−1 penalty"
            Case Else : Return "no signal"
        End Select
    End Function

    Private Shared Function ResolveFundMomColour(m As String) As Color
        Select Case If(m, "").ToUpperInvariant()
            Case "RISING"  : Return Theme.ACC_WARN          ' rising into crowded → caution
            Case "FALLING" : Return Theme.ACC_STRONG_LONG   ' de-crowding
            Case Else      : Return Theme.FG_TERTIARY
        End Select
    End Function

    ''' <summary>No numeric magnitude field on IndicatorResults for funding
    ''' momentum — heuristic 70% on RISING/FALLING, 20% on FLAT.</summary>
    Private Shared Function ResolveFundMomMagnitude(m As String) As Single
        Select Case If(m, "").ToUpperInvariant()
            Case "RISING", "FALLING" : Return 70.0F
            Case Else                : Return 20.0F
        End Select
    End Function

    Private Shared Function ResolveSpreadColour(status As String) As Color
        Select Case If(status, "").ToUpperInvariant()
            Case "TIGHT" : Return Theme.ACC_STRONG_LONG
            Case "WIDE"  : Return Theme.ACC_SHORT
            Case Else    : Return Theme.FG_TERTIARY
        End Select
    End Function

    ''' <summary>OI × CVD badge + note tag row.</summary>
    Private Shared Function BuildBadgeRow(badge As Control, noteText As String) As Control
        Dim row As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 8),
            .Padding = New Padding(0)
        }
        row.Controls.Add(badge)
        row.Controls.Add(New Label() With {
            .AutoSize = True,
            .Text = noteText,
            .Font = Theme.FontMono(9.0F, FontStyle.Regular),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .Margin = New Padding(12, 5, 0, 0)
        })
        Return row
    End Function

    ''' <summary>Build a MiniMeter and return it wrapped in a fixed-height
    ''' panel so the FlowLayoutPanel stack respects its size.</summary>
    Private Shared Function BuildMiniMeter(label As String,
                                           valueText As String,
                                           pct As Single,
                                           barColour As Color) As Control
        Dim meter As New MiniMeter() With {
            .LabelText = label,
            .ValueText = valueText,
            .Pct = pct,
            .BarColor = barColour,
            .Size = New Size(260, 24),
            .Margin = New Padding(0, 0, 0, 6)
        }
        Return meter
    End Function

    ' =======================================================================
    ' INDICATOR DETAILS card (P4d commit 4). Was the empty _cardDynamicNorms
    ' placeholder; renamed to _cardIndicatorDetails. Two-column grouped
    ' layout holding the verbose absolute-value detail that didn't fit in
    ' the SIGNAL BREAKDOWN NOTE column (gaps GAP-17..51, GAP-67..71, GAP-78..84).
    '
    ' 12 grouped sub-sections (6 per column). Each group is built inline
    ' (BuildGroupInline) — SectionGroup control doesn't expose a coloured-
    ' title API, so the per-group "regime tag" colour is rendered as a
    ' header Label over a bordered Panel.
    ' =======================================================================
    Public Sub BindCardIndicatorDetails(v As VerdictResult,
                                        r As IndicatorResults,
                                        norms As DynamicNorms,
                                        cfg As EngineSettings)
        If _cardIndicatorDetails Is Nothing Then Return

        _cardIndicatorDetails.SuspendLayout()
        _cardIndicatorDetails.Controls.Clear()

        Dim outer As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 2,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        outer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        outer.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
        outer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        outer.Controls.Add(BuildPlainSectionHeader("INDICATOR DETAILS"), 0, 0)

        Dim grid As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 4, 0, 0), .Padding = New Padding(0)
        }
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim leftCol  As FlowLayoutPanel = NewIndicatorDetailsColumn(rightSide:=False)
        Dim rightCol As FlowLayoutPanel = NewIndicatorDetailsColumn(rightSide:=True)
        grid.Controls.Add(leftCol,  0, 0)
        grid.Controls.Add(rightCol, 1, 0)

        ' Left column.
        BuildGroupNorms(leftCol, norms, r)
        BuildGroupVwap(leftCol, r, cfg)
        BuildGroupEmaRibbon(leftCol, r)
        BuildGroupBbwTtm(leftCol, r)
        BuildGroupVolume(leftCol, r)
        BuildGroupTrendStructure(leftCol, r)

        ' Right column.
        BuildGroupRegime5m(rightCol, r)
        BuildGroupMtfGate(rightCol, r)
        BuildGroupFunding(rightCol, r, cfg)
        BuildGroupOpenInterest(rightCol, r)
        BuildGroupMicroCvd(rightCol, r)
        BuildGroupLiquidations(rightCol, r)

        outer.Controls.Add(grid, 0, 1)
        _cardIndicatorDetails.Controls.Add(outer)
        _cardIndicatorDetails.ResumeLayout(True)
    End Sub

    Private Shared Function NewIndicatorDetailsColumn(rightSide As Boolean) As FlowLayoutPanel
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

    ''' <summary>
    ''' Inline replacement for SectionGroup — coloured title label above a
    ''' 1px bordered Panel host that hosts the group's KV rows. SectionGroup
    ''' doesn't expose a coloured-title API; this composition lets the
    ''' regime / MTF / OI / MicroCVD / Liq groups tint their title text to
    ''' match the regime tag.
    ''' </summary>
    Private Shared Function BuildGroupInline(title As String,
                                             titleColour As Color) As (host As FlowLayoutPanel, body As FlowLayoutPanel)
        Dim host As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(0)
        }
        Dim titleLbl = New Label() With {
            .AutoSize = True,
            .Text = title,
            .Font = Theme.FontMono(9.5F, FontStyle.Bold),
            .ForeColor = titleColour,
            .BackColor = Color.Transparent,
            .Margin = New Padding(2, 0, 0, 2)
        }
        host.Controls.Add(titleLbl)

        Dim body As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .BorderStyle = BorderStyle.FixedSingle,
            .Margin = New Padding(0),
            .Padding = New Padding(8, 6, 8, 6)
        }
        host.Controls.Add(body)
        Return (host, body)
    End Function

    Private Shared Sub AddKv(body As FlowLayoutPanel,
                             label As String, value As String,
                             Optional valueColour As Color = Nothing,
                             Optional indent As Boolean = False,
                             Optional wrap As Boolean = False)
        body.Controls.Add(BuildCardKvRow(label, value, valueColour, indent, wrap))
    End Sub

    ' -----------------------------------------------------------------------
    ' Helpers for regime colour / formatting reused across groups.
    ' -----------------------------------------------------------------------
    Private Shared Function ResolveRegimeColour(regime As String) As Color
        Select Case If(regime, "").ToUpperInvariant()
            Case "TRENDING_UP"   : Return Theme.ACC_STRONG_LONG
            Case "TRENDING_DOWN" : Return Theme.ACC_SHORT
            Case "RANGE_BOUND"   : Return Theme.ACC_WARN
            Case "TRANSITIONAL"  : Return Theme.FG_TERTIARY
            Case Else            : Return Theme.FG_TERTIARY
        End Select
    End Function

    Private Shared Function FormatUsd(usd As Double) As String
        If usd >= 1_000_000.0 Then Return "$" & (usd / 1_000_000.0).ToString("F2") & "M"
        If usd >= 1_000.0 Then Return "$" & (usd / 1_000.0).ToString("F1") & "K"
        Return "$" & usd.ToString("F0")
    End Function

    ' --- LEFT COLUMN groups ---

    Private Shared Sub BuildGroupNorms(parent As FlowLayoutPanel, norms As DynamicNorms, r As IndicatorResults)
        Dim modeTag As String = If(norms.IsLive, "[LIVE]", "[STATIC FALLBACK]")
        Dim modeColour As Color = If(norms.IsLive, Theme.ACC_STRONG_LONG, Theme.ACC_WARN)
        Dim g = BuildGroupInline($"NORMS  {modeTag}", modeColour)
        AddKv(g.body, "ATR scale:",   $"{norms.ATRScaleFactor:F2}×")
        AddKv(g.body, "",             $"(ATR {r.ATR:F2}  ref {norms.ATRRef:F2})", indent:=True)
        AddKv(g.body, "Vol H/M:",     $"{norms.VolHighThreshold:F2}×  /  {norms.VolMidThreshold:F2}×")
        AddKv(g.body, "Vol mean/σ:",  $"{norms.VolMean:F4} BTC  /  σ {norms.VolStdDev:F4}")
        AddKv(g.body, "VWAP dev thr:", $"±{norms.VWAPDevThreshold:F2}%")
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupVwap(parent As FlowLayoutPanel, r As IndicatorResults, cfg As EngineSettings)
        Dim warmupTag As String = ""
        If r.VWAPSessionCandles < cfg.Indicators.VWAP.WarmupCandles Then warmupTag = "  [WARMUP]"

        Dim s2h As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim s2m As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim nowUtc = DateTime.UtcNow
        Dim anchor As String
        If nowUtc.Hour < s2h OrElse (nowUtc.Hour = s2h AndAlso nowUtc.Minute < s2m) Then
            anchor = "00:00 UTC"
        Else
            anchor = String.Format("{0:D2}:{1:D2} UTC", s2h, s2m)
        End If

        Dim titleColour As Color = If(warmupTag.Length > 0, Theme.ACC_WARN, Theme.FG_TERTIARY)
        Dim g = BuildGroupInline($"VWAP  ·  reset {anchor}{warmupTag}", titleColour)
        AddKv(g.body, "Value:",   $"{r.VWAP:F1}")
        AddKv(g.body, "Dev:",     String.Format("{0:+0.000;-0.000;0.000}%", r.VWAPDevPct))
        AddKv(g.body, "Candles:", r.VWAPSessionCandles.ToString())
        AddKv(g.body, "σ1 band:", $"[{r.VWAPSigma1Lower:F1},  {r.VWAPSigma1Upper:F1}]")
        AddKv(g.body, "σ2 band:", $"[{r.VWAPSigma2Lower:F1},  {r.VWAPSigma2Upper:F1}]")
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupEmaRibbon(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim alignColour As Color
        Select Case If(r.EMAAlignment, "")
            Case "BULL"  : alignColour = Theme.ACC_STRONG_LONG
            Case "BEAR"  : alignColour = Theme.ACC_SHORT
            Case "MIXED" : alignColour = Theme.ACC_WARN
            Case Else    : alignColour = Theme.FG_TERTIARY
        End Select
        Dim g = BuildGroupInline($"EMA RIBBON  ·  {r.EMAAlignment}", alignColour)
        AddKv(g.body, "9:",        $"{r.EMA9:F1}")
        AddKv(g.body, "21:",       $"{r.EMA21:F1}")
        AddKv(g.body, "50:",       $"{r.EMA50:F1}")
        Dim priceTag As String = $" ({If(r.PriceVsEMA200, "—")})"
        Dim priceColour As Color = If(r.PriceVsEMA200 = "ABOVE", Theme.ACC_STRONG_LONG,
                                   If(r.PriceVsEMA200 = "BELOW", Theme.ACC_SHORT, Theme.FG_TERTIARY))
        AddKv(g.body, "200 (5m):", $"{r.EMA200_5m:F1}" & priceTag, valueColour:=priceColour)
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupBbwTtm(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim g = BuildGroupInline("BBW / TTM", Theme.FG_TERTIARY)
        AddKv(g.body, "BBW:",        $"{r.BBW:F3}")
        AddKv(g.body, "Squeeze:",    If(r.SqueezeStatus, "—"))
        AddKv(g.body, "TTM hist:",   String.Format("{0:+0.0;-0.0;0.0}", r.TTMHistogram))
        AddKv(g.body, "TTM dir:",    If(r.TTMDirection, "—"))
        AddKv(g.body, "TTM signal:", If(r.TTMSignal, "—"))
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupVolume(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim g = BuildGroupInline("VOLUME", Theme.FG_TERTIARY)
        AddKv(g.body, "Current:", $"{r.CurrentVolume:F4} BTC  =  {FormatUsd(r.CurrentVolumeUSD)}")
        AddKv(g.body, "vs SMA9:", $"{r.VolumeRatio:F2}×")
        AddKv(g.body, "SMA9:",    $"{r.VolumeSMA9:F4} BTC")
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupTrendStructure(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim tsLabel As String = r.TrendStructure.ToString()
        Dim tsColour As Color
        Select Case r.TrendStructure
            Case TrendStructure.UPTREND     : tsColour = Theme.ACC_STRONG_LONG
            Case TrendStructure.DOWNTREND   : tsColour = Theme.ACC_SHORT
            Case TrendStructure.EXPANSION   : tsColour = Theme.ACC_WARN
            Case TrendStructure.CONTRACTION : tsColour = Theme.ACC_INFO
            Case Else                       : tsColour = Theme.FG_TERTIARY
        End Select
        Dim g = BuildGroupInline($"TREND STRUCTURE  ·  {tsLabel}", tsColour)
        Dim h = r.LastTwoHighs5m
        Dim l = r.LastTwoLows5m
        If h.Newer > 0 AndAlso h.Older > 0 AndAlso l.Newer > 0 AndAlso l.Older > 0 Then
            Dim hiOp As String = If(h.Newer > h.Older, ">", "<")
            Dim lowOp As String = If(l.Newer > l.Older, ">", "<")
            AddKv(g.body, "Highs:", $"{h.Newer:F1} {hiOp} {h.Older:F1}")
            AddKv(g.body, "Lows:",  $"{l.Newer:F1} {lowOp} {l.Older:F1}")
        Else
            AddKv(g.body, "Status:", "insufficient pivot data", valueColour:=Theme.FG_QUATERNARY)
        End If
        parent.Controls.Add(g.host)
    End Sub

    ' --- RIGHT COLUMN groups ---

    Private Shared Sub BuildGroupRegime5m(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim regColour As Color = ResolveRegimeColour(r.Regime)
        Dim g = BuildGroupInline($"REGIME 5m  ·  {If(r.Regime, "—")}", regColour)
        AddKv(g.body, "ADX:", $"{r.ADX:F1}")
        AddKv(g.body, "+DI:", $"{r.PlusDI:F1}")
        AddKv(g.body, "−DI:", $"{r.MinusDI:F1}")
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupMtfGate(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim gateLabel As String = If(r.MTFGatePass, "PASS", "BLOCK")
        Dim gateColour As Color = If(r.MTFGatePass, Theme.ACC_STRONG_LONG, Theme.ACC_SHORT)
        Dim g = BuildGroupInline($"MTF GATE 15m  ·  {gateLabel}", gateColour)
        AddKv(g.body, "15m Trend:", If(r.MTF15mTrend, "—"))
        AddKv(g.body, "15m ADX:",   $"{r.MTF15mADX:F1}")
        AddKv(g.body, "15m EMA:",   If(r.MTF15mEMAAlignment, "—"))
        ' Reason text — wrap if long so it doesn't overflow the column.
        AddKv(g.body, "Reason:",    If(r.MTFGateReason, "—"), wrap:=True)
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupFunding(parent As FlowLayoutPanel, r As IndicatorResults, cfg As EngineSettings)
        Dim g = BuildGroupInline("FUNDING", Theme.FG_TERTIARY)
        ' GAP-30: v30 negative-zero clamp at display.
        Dim ratePct As Double = If(Math.Abs(r.FundingRate) < 0.00000001, 0.0, r.FundingRate * 100.0)
        AddKv(g.body, "Rate:",     String.Format("{0:+0.0000;-0.0000;0.0000}%  ·  {1}", ratePct, If(r.FundingBias, "—")))
        AddKv(g.body, "Momentum:", If(r.FundingMomentum, "—"))
        Dim cfgFm = cfg.Indicators.Funding
        Dim enFlag As String = If(cfgFm.MomentumEnabled, "Y", "N")
        Dim cfgStr As String = $"en:{enFlag}  soft:+{cfgFm.MomentumSoften}  amp:−{cfgFm.MomentumAmplify}"
        AddKv(g.body, "Config:",   cfgStr, valueColour:=Theme.FG_QUATERNARY)
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupOpenInterest(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim oiColour As Color
        Select Case If(r.OISignal, "").ToUpperInvariant()
            Case "NEW LONGS", "COVERING"     : oiColour = Theme.ACC_STRONG_LONG
            Case "NEW SHORTS", "CAPITULATION" : oiColour = Theme.ACC_SHORT
            Case Else                         : oiColour = Theme.FG_TERTIARY
        End Select
        Dim g = BuildGroupInline($"OPEN INTEREST  ·  {If(r.OISignal, "—")}", oiColour)
        AddKv(g.body, "OI:",     $"{r.OI_Current:N0}")
        AddKv(g.body, "Δ 15m:",  String.Format("{0:+0.000;-0.000;0.000}%", r.OIChange15m))
        AddKv(g.body, "Δ 60m:",  String.Format("{0:+0.000;-0.000;0.000}%", r.OIChange60m))
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupMicroCvd(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim sig As String = If(r.MicroCVDSignal, "")
        Dim sigColour As Color
        Select Case sig.ToUpperInvariant()
            Case "BULL_ACCEL" : sigColour = Theme.ACC_STRONG_LONG
            Case "BEAR_ACCEL" : sigColour = Theme.ACC_SHORT
            Case "BULL_DECEL" : sigColour = Color.FromArgb(120, 200, 120)
            Case "BEAR_DECEL" : sigColour = Color.FromArgb(220, 130, 130)
            Case Else         : sigColour = Theme.FG_TERTIARY
        End Select
        Dim g = BuildGroupInline($"MICROCVD  ·  {If(sig.Length > 0, sig, "—")}", sigColour)
        AddKv(g.body, "Early:",    String.Format("{0:+0;-0;0}", r.MicroCVDEarly))
        AddKv(g.body, "Mid:",      String.Format("{0:+0;-0;0}", r.MicroCVDMid))
        AddKv(g.body, "Late:",     String.Format("{0:+0;-0;0}", r.MicroCVDLate))
        AddKv(g.body, "Momentum:", If(r.MicroCVDMomentum, "—"))
        parent.Controls.Add(g.host)
    End Sub

    Private Shared Sub BuildGroupLiquidations(parent As FlowLayoutPanel, r As IndicatorResults)
        Dim liqColour As Color
        Select Case If(r.LiqSignal, "").ToUpperInvariant()
            Case "LONG LIQS"  : liqColour = Theme.ACC_SHORT      ' longs liquidating = bearish
            Case "SHORT LIQS" : liqColour = Theme.ACC_STRONG_LONG ' shorts liquidating = bullish
            Case Else         : liqColour = Theme.FG_TERTIARY
        End Select
        Dim g = BuildGroupInline($"LIQUIDATIONS  ·  {If(r.LiqSignal, "—")}", liqColour)
        AddKv(g.body, "Long size:",  $"{r.LiqLongSize:F0}")
        AddKv(g.body, "Short size:", $"{r.LiqShortSize:F0}")
        parent.Controls.Add(g.host)
    End Sub

    ' =======================================================================
    ' VOLUME PROFILE card (P4d commit 3). Gaps GAP-62..66.
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
        colHdr.Controls.Add(MakeBreakdownColumnHeader(rightSide:=False), 0, 0)
        colHdr.Controls.Add(MakeBreakdownColumnHeader(rightSide:=True),  1, 0)
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

    ''' <summary>
    ''' Column-header row matching MakeSignalRow's TableLayoutPanel column
    ''' widths (110 / 75 / flex / 30). Each header cell sits in its own
    ''' fixed-width column so the labels align under their data cells.
    ''' </summary>
    Private Shared Function MakeBreakdownColumnHeader(Optional rightSide As Boolean = False) As Control
        ' Same 500 px width as MakeSignalRow so the data cells line up under
        ' the header cells. Margin mirrors NewBreakdownColumn so the two
        ' halves' offsets agree.
        Dim hdr As New TableLayoutPanel() With {
            .Width = 500,
            .Height = 14,
            .ColumnCount = 4, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = If(rightSide, New Padding(4, 2, 0, 0), New Padding(0, 2, 4, 0)),
            .Padding = New Padding(0)
        }
        hdr.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
        hdr.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 75))
        hdr.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        hdr.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 30))
        hdr.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        hdr.Controls.Add(MakeColHeaderCell("INDICATOR"), 0, 0)
        hdr.Controls.Add(MakeColHeaderCell("STATE"),     1, 0)
        hdr.Controls.Add(MakeColHeaderCell("NOTE"),      2, 0)
        hdr.Controls.Add(MakeColHeaderCell("SC", alignRight:=True), 3, 0)
        Return hdr
    End Function

    Private Shared Function MakeColHeaderCell(text As String,
                                              Optional alignRight As Boolean = False) As Label
        Return New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = text,
            .Font = Theme.FontMono(8.0F, FontStyle.Bold),
            .ForeColor = Theme.FG_QUATERNARY,
            .BackColor = Color.Transparent,
            .TextAlign = If(alignRight, ContentAlignment.MiddleRight, ContentAlignment.MiddleLeft),
            .Margin = New Padding(0),
            .Padding = New Padding(0)
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
    ''' Build one inline signal row as a TableLayoutPanel with four columns
    ''' pinned to the header positions: INDICATOR 110 / STATE 75 / NOTE
    ''' flex / SC 30 (right-aligned). Previous flow-positioned implementation
    ''' rendered the row as a single composite mono-formatted Label, which
    ''' left columns to float by natural width — state pills on rows with
    ''' shorter indicator labels ended up at different x-positions than rows
    ''' with longer labels (e.g., ROC(9) vs Funding Mom). TableLayoutPanel
    ''' pins the columns under their headers.
    '''
    ''' SC cell encodes three states (preserved from previous impl):
    '''   sc Is Nothing     → "—" in FG_DIM (this row didn't vote)
    '''   sc.Value &gt; 0     → "+N" in ACC_STRONG_LONG
    '''   sc.Value &lt; 0     → "-N" in ACC_SHORT
    '''   sc.Value = 0      → "0"  in FG_TERTIARY
    ''' If subNote is supplied, a second row spans NOTE+SC cells with an
    ''' indented "↳ {text}" sub-line (used by Swing Pivots for the D2
    ''' best-volume sub-note).
    ''' </summary>
    Private Shared Function MakeSignalRow(label As String,
                                          state As String,
                                          stateColour As Color,
                                          note As String,
                                          sc As Integer?,
                                          Optional subNote As String = Nothing,
                                          Optional subNoteColour As Color = Nothing) As Control
        Dim hasSubNote As Boolean = Not String.IsNullOrEmpty(subNote)

        ' Hosted inside a FlowLayoutPanel column — Width is explicit (matches
        ' the column-header sub-grid width), Dock is unset (FlowLayoutPanel
        ' ignores it). The 500 px matches the existing tier-separator width.
        Dim row As New TableLayoutPanel() With {
            .Width = 500,
            .Height = If(hasSubNote, SIGROW_HEIGHT + SIGROW_SUBNOTE_HEIGHT, SIGROW_HEIGHT),
            .ColumnCount = 4,
            .RowCount = If(hasSubNote, 2, 1),
            .BackColor = Color.Transparent,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))   ' INDICATOR
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 75))    ' STATE
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F)) ' NOTE (flex)
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 30))    ' SC
        row.RowStyles.Add(New RowStyle(SizeType.Absolute, SIGROW_HEIGHT))
        If hasSubNote Then row.RowStyles.Add(New RowStyle(SizeType.Absolute, SIGROW_SUBNOTE_HEIGHT))

        row.Controls.Add(MakeBreakdownCell(If(label, ""), Theme.FG_SECONDARY),  0, 0)
        row.Controls.Add(MakeBreakdownCell(If(state, ""), stateColour),         1, 0)
        row.Controls.Add(MakeBreakdownCell(If(note, ""),  Theme.FG_QUATERNARY), 2, 0)

        ' SC cell — sign-coloured for votes, dim em-dash for non-voting rows.
        Dim scText As String
        Dim scColour As Color
        If Not sc.HasValue Then
            scText = "—"
            scColour = Theme.FG_DIM
        ElseIf sc.Value > 0 Then
            scText = "+" & sc.Value.ToString()
            scColour = Theme.ACC_STRONG_LONG
        ElseIf sc.Value < 0 Then
            scText = sc.Value.ToString()
            scColour = Theme.ACC_SHORT
        Else
            scText = "0"
            scColour = Theme.FG_TERTIARY
        End If
        row.Controls.Add(MakeBreakdownCell(scText, scColour, alignRight:=True), 3, 0)

        If hasSubNote Then
            Dim subLbl = MakeBreakdownCell("↳ " & subNote,
                                           If(subNoteColour.IsEmpty, Theme.FG_QUATERNARY, subNoteColour))
            subLbl.Font = Theme.FontMono(8.5F, FontStyle.Regular)
            row.Controls.Add(subLbl, 2, 1)
            row.SetColumnSpan(subLbl, 2)
        End If

        Return row
    End Function

    ''' <summary>
    ''' Single cell factory for the SIGNAL BREAKDOWN row layout. AutoSize
    ''' False + Dock Fill so the TableLayoutPanel column widths drive
    ''' positioning (the whole point of the alignment fix).
    ''' </summary>
    Private Shared Function MakeBreakdownCell(text As String,
                                              colour As Color,
                                              Optional alignRight As Boolean = False) As Label
        Return New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .Text = text,
            .Font = Theme.FontMono(9.5F, FontStyle.Regular),
            .ForeColor = colour,
            .BackColor = Color.Transparent,
            .TextAlign = If(alignRight, ContentAlignment.MiddleRight, ContentAlignment.MiddleLeft),
            .AutoEllipsis = True,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
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

    ''' <summary>
    ''' Derive STATE pill text + colour from a SignalBreakdownItem's
    ''' LongHit / ShortHit booleans so STATE always agrees with SC. Used by
    ''' rows where raw-field thresholds disagree with the engine's actual
    ''' vote logic (RSI, DMI/ADX, BBW/TTM) — the spec-supplied thresholds
    ''' would otherwise show NEUT next to a +1 / -1 SC, which reads as a
    ''' contradiction to the trader.
    ''' </summary>
    Private Shared Function StateFromHits(item As SignalBreakdownItem) As (state As String, colour As Color)
        If item Is Nothing Then Return ("—", Theme.FG_TERTIARY)
        If item.LongHit AndAlso item.ShortHit Then Return ("MIXED", Theme.ACC_WARN)
        If item.LongHit Then Return ("BULL", Theme.ACC_STRONG_LONG)
        If item.ShortHit Then Return ("BEAR", Theme.ACC_SHORT)
        Return ("NEUT", Theme.FG_TERTIARY)
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
        ' STATE derived from breakdown hits so it stays in lock-step with SC.
        ' The engine's RSI vote uses PARTIAL→UPGRADED logic beyond simple
        ' 30/70 overbought-oversold cuts, so raw thresholds disagree with
        ' the actual vote on a meaningful share of runs.
        Dim sd = StateFromHits(FindItem(items, "RSI(9)"))
        Dim note As String = r.RSI.ToString("F1")
        If Not String.IsNullOrEmpty(r.RSIDivergence) AndAlso r.RSIDivergence <> "NONE" Then
            note &= "  div:" & r.RSIDivergence.ToLower()
        End If
        Return MakeSignalRow("RSI(9)", sd.state, sd.colour, note, ScForItem(items, "RSI(9)"))
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
        ' STATE derived from the "DMI +/-DI" breakdown item's hits so it
        ' agrees with SC. The ADX threshold gate is separate and lives in
        ' the SC sum below; trying to recreate the directional decision
        ' from raw ADX / +DI / -DI fields produced NEUT-with-+1 contradictions.
        Dim sd = StateFromHits(FindItem(items, "DMI +/-DI"))
        ' Legacy renders ADX at F1 (e.g. "ADX: 24.7"). F0 was rounding to "25".
        Dim note As String = $"ADX {r.ADX:F1}"
        ' Engine emits two items ("DMI +/-DI" and "ADX>{N}") — sum their SCs.
        Dim sc As Integer = ScForItem(items, "DMI +/-DI") + ScForItemPrefix(items, "ADX>")
        If sc > 1 Then sc = 1
        If sc < -1 Then sc = -1
        Return MakeSignalRow("DMI/ADX", sd.state, sd.colour, note, sc)
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
        ' Legacy renders the SMA-relative volume ratio at F2 (e.g. "1.58x").
        Dim note As String = $"{r.VolumeRatio:F2}× {FormatUsdShort(r.CurrentVolumeUSD)}"
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
        ' Legacy renders VWAP Dev at F3 (e.g. "-0.080%"). F2 was rounding to "-0.08%".
        Dim note As String = String.Format("{0:+0.000;-0.000;0.000}%", r.VWAPDevPct)
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
        ' STATE derived from breakdown hits so it stays in lock-step with SC.
        ' r.SqueezeStatus alone misses the TTM histogram + directional gating
        ' the engine layers on top, producing NEUT-with-+1 contradictions.
        Dim sd = StateFromHits(FindItem(items, "BBW/TTM"))
        Dim note As String = If(r.SqueezeStatus, "—").ToLower()
        Return MakeSignalRow("BBW/TTM", sd.state, sd.colour, note, ScForItem(items, "BBW/TTM"))
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
        ' Legacy renders OI 15m delta at F3 (e.g. "0.003%"). F1 was rounding sub-percent
        ' deltas to "0.0%" — the percent change in OI is typically small and rounding
        ' it away destroys the directional signal.
        Dim note As String = String.Format("{0:+0.000;-0.000;0.000}%", r.OIChange15m)
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
        ' Legacy renders spread at F2 (e.g. "0.06 bps"). F1 rounded sub-decimal
        ' spreads to "0.1 bps" — losing the trader's tight-spread signal.
        Dim note As String = $"{r.SpreadBps:F2} bps"
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
        ' Legacy renders CVD net at F0 raw (e.g. "Net:2133890"). Compressing
        ' to "/1000 k" with F1 lost the last 3 digits of precision. Switched
        ' to N0 with thousand separators — keeps full precision, readable.
        Dim note As String = If(r.CVDValue >= 0,
                                "+" & r.CVDValue.ToString("N0"),
                                r.CVDValue.ToString("N0"))
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
        ' Legacy renders TFI at F3 (e.g. "0.969"). F2 was rounding to "0.97".
        Dim note As String = r.TFIValue.ToString("F3")
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
            ' Legacy renders the best-volume pivot price at F1 (e.g. "77998.5").
            ' F0 was rounding away the half-tick.
            subNote = String.Format("best vol: {0} @ {1:F1} ({2:F1}×)",
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

        ' Pass 2c — derive from "Regime Align (2c)" breakdown item (no
        ' Pass2cOutcome field on VerdictResult).
        '
        ' Engine emission permutations (from ScoringEngine_Calculate_Scoring.vb
        ' lines 433-510, 753):
        '   SUPPRESSED      → item not emitted (TRANSITIONAL or zero-net scores)
        '   ALIGNED LONG    → item with LongHit=True,  ShortHit=False, note "+N REGIME ALIGN [...]"
        '   ALIGNED SHORT   → item with LongHit=False, ShortHit=True,  note "+N REGIME ALIGN [...]"
        '   CONFLICT        → item with BOTH hits False, note "-N REGIME CONFLICT [...]"
        '                     (penalty applied to dominant side, but neither
        '                     hit flag is set — only the note carries the
        '                     conflict marker).
        '
        ' Prior code missed the CONFLICT case (item present + both False fell
        ' through to "SUPPRESSED"). The Both-True branch was dead code —
        ' kept removed since the engine never emits that shape.
        Dim p2cItem = FindItem(items, "Regime Align (2c)")
        Dim p2cState As String, p2cColour As Color, p2cTag As String
        Dim p2cArrow As String = "", p2cArrowColour As Color = Color.Empty
        If p2cItem Is Nothing Then
            p2cState = "SUPPRESSED"
            p2cColour = Theme.ACC_NEUTRAL
            p2cTag = ""
        ElseIf p2cItem.LongHit AndAlso Not p2cItem.ShortHit Then
            p2cState = "ALIGNED"
            p2cColour = Theme.ACC_STRONG_LONG
            p2cTag = ExtractPass2cTag(p2cItem.Note)
            p2cArrow = "↑"
            p2cArrowColour = Theme.ACC_STRONG_LONG
        ElseIf p2cItem.ShortHit AndAlso Not p2cItem.LongHit Then
            p2cState = "ALIGNED"
            p2cColour = Theme.ACC_STRONG_LONG
            p2cTag = ExtractPass2cTag(p2cItem.Note)
            p2cArrow = "↓"
            p2cArrowColour = Theme.ACC_SHORT
        Else
            ' Item present + both hits false (or — defensively — both true).
            ' Engine emits this for CONFLICT (bidirectional penalty).
            p2cState = "CONFLICT"
            p2cColour = Theme.ACC_SHORT
            p2cTag = ExtractPass2cTag(p2cItem.Note)
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

    ''' <summary>
    ''' Parse the "Regime Align (2c)" breakdown note for its leading ±N
    ''' magnitude and produce a footer tag like "+1 regime" or "−1 regime".
    ''' Note shape is always "[+|−]N REGIME (ALIGN|CONFLICT) [...]" — engine
    ''' uses ASCII "-" for the conflict prefix; we render as "−" (U+2212)
    ''' for typographic consistency with the rest of the UI.
    ''' Falls back to "+1 regime" / "−1 regime" if parsing fails so the
    ''' badge always carries some context.
    ''' </summary>
    Private Shared Function ExtractPass2cTag(note As String) As String
        Dim text As String = If(note, "").TrimStart()
        If text.Length = 0 Then Return ""
        Dim isPenalty As Boolean = text.StartsWith("-"c) OrElse text.StartsWith("−"c)
        Dim sign As String = If(isPenalty, "−", "+")
        Dim defaultMag As String = "1"
        ' Skip the leading +/- and read consecutive digits.
        Dim startIdx As Integer = 0
        If text(0) = "+"c OrElse text(0) = "-"c OrElse text(0) = "−"c Then startIdx = 1
        Dim digits As String = ""
        For i As Integer = startIdx To text.Length - 1
            If Char.IsDigit(text(i)) Then
                digits &= text(i)
            Else
                Exit For
            End If
        Next
        Dim mag As String = If(digits.Length > 0, digits, defaultMag)
        Return $"{sign}{mag} regime"
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
