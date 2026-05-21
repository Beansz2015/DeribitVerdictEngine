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
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 3,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
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

        _cardScore.Controls.Add(inner)
    End Sub

    ' -----------------------------------------------------------------------
    ' VERDICT card
    ' -----------------------------------------------------------------------
    Private Sub InitVerdictCard()
        Dim inner = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1, .RowCount = 4,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        inner.RowStyles.Add(New RowStyle(SizeType.AutoSize))
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

        _regimeAnchorWarn = New RegimeAnchorWarn() With {
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 4, 0, 0),
            .WarningText = ""
        }
        inner.Controls.Add(_regimeAnchorWarn, 0, 3)

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
            .ColumnCount = 1, .RowCount = 3,
            .BackColor = Color.Transparent
        }
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 18))
        inner.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        inner.RowStyles.Add(New RowStyle(SizeType.Absolute, 14))
        inner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        inner.Controls.Add(MakeSectionHeader("ATR ENTRY LEVELS"), 0, 0)

        Dim zones = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5, .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        zones.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        zones.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 12.0F))
        zones.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        zones.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        zones.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        zones.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        _atrStopValue   = MakeZoneLabel("STOP",   Theme.ACC_SHORT)
        _atrRRValue     = MakeZoneLabel("R:R",    Theme.FG_QUATERNARY)
        _atrEntryValue  = MakeZoneLabel("ENTRY",  Theme.FG_PRIMARY)
        _atrCappedValue = MakeZoneLabel("CAPPED", Theme.ACC_WARN)
        _atrTargetValue = MakeZoneLabel("TARGET", Theme.ACC_STRONG_LONG)

        zones.Controls.Add(_atrStopValue,   0, 0)
        zones.Controls.Add(_atrRRValue,     1, 0)
        zones.Controls.Add(_atrEntryValue,  2, 0)
        zones.Controls.Add(_atrCappedValue, 3, 0)
        zones.Controls.Add(_atrTargetValue, 4, 0)

        inner.Controls.Add(zones, 0, 1)

        _atrCapReason = MakeValueLabel(Theme.FontMono(8.0F, FontStyle.Regular), Theme.ACC_WARN)
        _atrCapReason.Dock = DockStyle.Fill
        _atrCapReason.TextAlign = ContentAlignment.MiddleCenter
        _atrCapReason.Text = ""
        inner.Controls.Add(_atrCapReason, 0, 2)

        _cardAtrLevels.Controls.Add(inner)
    End Sub

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
    End Sub

    Public Sub BindCardLastPrice(r As IndicatorResults)
        If _lblLastPrice IsNot Nothing Then
            _lblLastPrice.Text = If(r.CurrentPrice > 0, $"${r.CurrentPrice:N0}", "—")
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

    Public Sub BindCardAtrLevels(v As VerdictResult, r As IndicatorResults)
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier

        ' DynamicNorms recompute would require re-fetching candles; reuse the
        ' simpler ratio-of-multipliers approach for R:R, which matches the
        ' legacy RTF render.
        Dim isLong As Boolean = v.LongScore > v.ShortScore
        Dim isShort As Boolean = v.ShortScore > v.LongScore
        Dim atrUnit As Double = r.ATR * r.ATRSizeMultiplier
        Dim atrStop   As Double = atrUnit * stopMult
        Dim atrTarget As Double = atrUnit * targetMult

        Dim stopPx As Double
        Dim entryPx As Double = r.CurrentPrice
        Dim rawTargetPx As Double
        Dim adjustedTargetPx As Double
        Dim capReason As String = ""

        If isShort Then
            stopPx           = entryPx + atrStop
            rawTargetPx      = entryPx - atrTarget
            adjustedTargetPx = If(v.AdjustedShortTarget > 0, v.AdjustedShortTarget, rawTargetPx)
            capReason        = If(v.TargetCapReasonShort, "")
            _atrTargetValue.ForeColor = Theme.ACC_SHORT
        Else
            ' Default to long-side rendering when scores are tied (entry-only
            ' visual is still useful — Stop/Target both populated for display).
            stopPx           = entryPx - atrStop
            rawTargetPx      = entryPx + atrTarget
            adjustedTargetPx = If(v.AdjustedLongTarget > 0, v.AdjustedLongTarget, rawTargetPx)
            capReason        = If(v.TargetCapReasonLong, "")
            _atrTargetValue.ForeColor = Theme.ACC_STRONG_LONG
        End If

        Dim showCapped As Boolean = False
        If (isLong AndAlso v.AdjustedLongTarget > 0) OrElse
           (isShort AndAlso v.AdjustedShortTarget > 0) Then
            Dim noiseFloor As Double = Math.Max(0.5, r.ATR * 0.02)
            showCapped = Math.Abs(rawTargetPx - adjustedTargetPx) >= noiseFloor
        End If

        SetZoneValue(_atrStopValue,   "STOP",   $"{stopPx:F1}")
        SetZoneValue(_atrEntryValue,  "ENTRY",  $"{entryPx:F1}")
        SetZoneValue(_atrTargetValue, "TARGET", $"{adjustedTargetPx:F1}")
        SetZoneValue(_atrRRValue,     "R:R",    FormatRR(atrTarget, atrStop))

        If showCapped Then
            SetZoneValue(_atrCappedValue, "CAPPED", $"→ {adjustedTargetPx:F1}")
            _atrCappedValue.ForeColor = Theme.ACC_WARN
            _atrCapReason.Text = capReason
        Else
            SetZoneValue(_atrCappedValue, "·", "")
            _atrCappedValue.ForeColor = Theme.BORDER_INNER
            _atrCapReason.Text = ""
        End If
    End Sub

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

End Class
