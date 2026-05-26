' UI/MainForm_Render_Header.vb
' Partial class: top render block.
' Contains: RTF helpers + RenderOutputHeader (VERDICT / CONTEXT / CONFIDENCE /
'   SCORE / TIME / LAST PRICE / HOLD STATUS / ATR ENTRY LEVELS / KELLY SIZING).
' Split from MainForm_Render.vb for maintainability.
'
' P5a migrations (so this file deletes cleanly in P5b):
'   - FormatRR → MainForm_Render_Cards.vb
'   - UpdateLogInfo + lnkResetLog/lnkAnalysisReport/lnkCalibCheck handlers
'     → MainForm_Layout.vb
'   - BuildCalibrationReport + Flag → MainForm_Calibration.vb

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

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
        If valColour.IsEmpty Then valColour = Theme.FG_PRIMARY
        AppendRtf(rtb, "  " & label, Theme.FG_TERTIARY)
        AppendRtf(rtb, value & Environment.NewLine, valColour, valBold)
    End Sub

    Private Sub SectionHeader(rtb As RichTextBox, text As String)
        AppendRtf(rtb, Environment.NewLine & text & Environment.NewLine, Theme.FG_SECONDARY, bold:=True)
    End Sub

    Private Sub Divider(rtb As RichTextBox)
        AppendRtf(rtb, "===========================================================" & Environment.NewLine, Theme.BORDER_CARD)
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
        Dim rawTs As DateTime = If(v.Timestamp <> DateTime.MinValue, v.Timestamp, DateTime.Now)
        Dim offset As TimeSpan = TimeZoneInfo.Local.GetUtcOffset(rawTs)
        Dim tzSign As String = If(offset >= TimeSpan.Zero, "+", "-")
        Dim ts As String = rawTs.ToString("yyyy-MM-dd HH:mm:ss") & " UTC" & tzSign & CInt(Math.Floor(Math.Abs(offset.TotalHours))).ToString()
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        Dim longStop   As Double = r.CurrentPrice - atrStop
        Dim longTarget As Double = r.CurrentPrice + atrTarget
        Dim shortStop  As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget
        Dim rrRatio As String = String.Format("1:{0:F1}", targetMult / stopMult)

        Divider(rtb)
        AppendRtf(rtb, "  VERDICT:    ", Theme.FG_TERTIARY)
        Dim vColour As Color = Theme.FG_PRIMARY
        Select Case v.Verdict
            Case "STRONG LONG", "LONG"   : vColour = Theme.ACC_STRONG_LONG
            Case "WEAK LONG"             : vColour = Color.FromArgb(120, 200, 120)
            Case "STRONG SHORT", "SHORT" : vColour = Theme.ACC_SHORT
            Case "WEAK SHORT"            : vColour = Color.FromArgb(220, 130, 130)
            Case Else                    : vColour = Theme.ACC_WARN
        End Select
        AppendRtf(rtb, v.Verdict & Environment.NewLine, vColour, bold:=True)

        If v.VerdictContext <> "" Then
            AppendRtf(rtb, "  CONTEXT:    ", Theme.FG_TERTIARY)
            Dim ctxColour As Color
            Select Case v.VerdictContext
                Case "MOMENTUM_FADING"  : ctxColour = Theme.ACC_SHORT
                Case "FLOW_UNCONFIRMED" : ctxColour = Theme.ACC_WARN
                Case "STRUCTURALLY_WEAK": ctxColour = Theme.FG_QUATERNARY
                Case Else               : ctxColour = Theme.FG_PRIMARY
            End Select
            AppendRtf(rtb, v.VerdictContext & Environment.NewLine, ctxColour, bold:=True)
        End If

        AppendRtf(rtb, "  CONFIDENCE: ", Theme.FG_TERTIARY)
        Dim cColour As Color = If(v.Confidence = "HIGH", Theme.ACC_STRONG_LONG,
                                  If(v.Confidence = "MEDIUM", Theme.ACC_WARN, Theme.ACC_SHORT))
        AppendRtf(rtb, v.Confidence & Environment.NewLine, cColour, bold:=True)

        ' Regime-anchor warning: surface "verdict fighting the dominant short-term trend"
        ' caveat. Display-only — zero scoring impact. Fires only on STRONG verdicts when
        ' price is significantly displaced from the 5m EMA(200) anchor in the opposite
        ' direction. Threshold 3.0× ATR is a starting heuristic; tune to 2.0× (more
        ' warnings) or 5.0× (only extreme cases) if the default proves off in live use.
        '
        ' Honest labelling: 5m EMA(200) is ~3.3 hours of data — INTERMEDIATE trend, not
        ' macro. For true macro context (daily timeframe), separate spec needed to add
        ' Deribit daily candle fetch + indicator.
        Const REGIME_ANCHOR_ATR_THRESHOLD As Double = 3.0
        If r.ATR > 0 AndAlso r.EMA200_5m > 0 Then
            Dim atrUnits As Double = (r.CurrentPrice - r.EMA200_5m) / r.ATR
            Dim warning  As String = ""
            If v.Verdict.StartsWith("STRONG LONG") AndAlso atrUnits < -REGIME_ANCHOR_ATR_THRESHOLD Then
                warning = String.Format("price {0:F1}× ATR below 5m EMA(200) — STRONG LONG fighting intermediate bear",
                                        Math.Abs(atrUnits))
            ElseIf v.Verdict.StartsWith("STRONG SHORT") AndAlso atrUnits > REGIME_ANCHOR_ATR_THRESHOLD Then
                warning = String.Format("price {0:F1}× ATR above 5m EMA(200) — STRONG SHORT fighting intermediate bull",
                                        atrUnits)
            End If
            If warning <> "" Then
                AppendRtf(rtb, "  REGIME ANCHOR:  ", Theme.FG_TERTIARY)
                AppendRtf(rtb, ChrW(&H26A0) & " " & warning & Environment.NewLine, Theme.ACC_WARN, bold:=True)
            End If
        End If

        Dim maxScore As Integer = v.MaxScore
        AppendRtf(rtb, "  SCORE:      ", Theme.FG_TERTIARY)
        If v.RegimePenalty > 0 Then
            AppendRtf(rtb, String.Format("Long {0}/{2} (eff.{1})  |  Short {3}/{2} (eff.{4})  |  TRANSITIONAL penalty: -{5}",
                                         v.LongScore, v.EffectiveLongScore, maxScore,
                                         v.ShortScore, v.EffectiveShortScore, v.RegimePenalty) & Environment.NewLine, Theme.ACC_WARN)
        Else
            AppendRtf(rtb, String.Format("Long {0}/{1}  |  Short {2}/{1}",
                                          v.LongScore, maxScore, v.ShortScore) & Environment.NewLine, Theme.FG_PRIMARY)
        End If

        AppendRtf(rtb, "  TIME:       ", Theme.FG_TERTIARY)
        AppendRtf(rtb, ts & Environment.NewLine, Theme.FG_QUATERNARY)
        Divider(rtb)

        AppendRtf(rtb, "  LAST TRANSACTED PRICE:  ", Theme.FG_TERTIARY)
        AppendRtf(rtb, If(lastTradePrice > 0,
                          lastTradePrice.ToString("F1"),
                          "N/A") & Environment.NewLine, Theme.FG_PRIMARY)

        If v.HoldStatus <> "N/A -- no open position" Then
            AppendRtf(rtb, "  HOLD / EXIT: ", Theme.FG_TERTIARY)
            AppendRtf(rtb, v.HoldStatus & Environment.NewLine, Theme.ACC_WARN, bold:=True)
        End If

        ScoringEngine.CalcKellySizing(v, atrStop, cfg)

        SectionHeader(rtb, String.Format("ATR ENTRY LEVELS  (ATR {0:F2} x {1:F2} scale | {2:F1}x stop / {3:F1}x target)",
                                          r.ATR, norms.ATRScaleFactor, stopMult, targetMult))

        ' Sub-tick cap adjustments are visual noise; suppress amber-bold CAPPED
        ' annotation when adjustment is below max(0.5, ATR × 0.02). TargetCapReason
        ' still populated on VerdictResult for CSV logging.
        Dim capNoiseFloor As Double = Math.Max(0.5, r.ATR * 0.02)

        AppendRtf(rtb, "  Long:   ", Theme.FG_TERTIARY)
        If v.AdjustedLongTarget > 0 Then
            Dim longCapAdjustment As Double = Math.Abs(longTarget - v.AdjustedLongTarget)
            If longCapAdjustment < capNoiseFloor Then
                AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                              longStop, r.CurrentPrice, v.AdjustedLongTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, Theme.ACC_STRONG_LONG)
            Else
                AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} ",
                                              longStop, r.CurrentPrice, longTarget), Theme.FG_QUATERNARY)
                AppendRtf(rtb, String.Format("--> {0:F1}  [{1}]",
                                              v.AdjustedLongTarget, v.TargetCapReasonLong) & Environment.NewLine, Theme.ACC_WARN, bold:=True)
            End If
        Else
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          longStop, r.CurrentPrice, longTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, Theme.ACC_STRONG_LONG)
        End If

        ' Long structural levels (from swing pivot detection)
        If r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0 Then
            Dim swingRisk   As Double = r.CurrentPrice - r.SwingStopLong
            Dim swingReward As Double = r.SwingTargetLong - r.CurrentPrice
            Dim swingRR As String = FormatRR(swingReward, swingRisk)
            AppendRtf(rtb, "  Long structural:  ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          r.SwingStopLong, r.CurrentPrice, r.SwingTargetLong, swingRR, swingRisk, swingReward) & Environment.NewLine, Theme.ACC_INFO)
        ElseIf r.SwingTargetLong > 0 Then
            AppendRtf(rtb, "  Long structural:  ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Target {0,9:F1}  (stop unset: no swing low below entry within lookback)", r.SwingTargetLong) & Environment.NewLine, Theme.FG_QUATERNARY)
        ElseIf r.SwingStopLong > 0 Then
            AppendRtf(rtb, "  Long structural:  ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  (target unset: no swing high above entry within lookback)", r.SwingStopLong) & Environment.NewLine, Theme.FG_QUATERNARY)
        End If

        AppendRtf(rtb, "  Short:  ", Theme.FG_TERTIARY)
        If v.AdjustedShortTarget > 0 Then
            Dim shortCapAdjustment As Double = Math.Abs(shortTarget - v.AdjustedShortTarget)
            If shortCapAdjustment < capNoiseFloor Then
                AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                              shortStop, r.CurrentPrice, v.AdjustedShortTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, Theme.ACC_SHORT)
            Else
                AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} ",
                                              shortStop, r.CurrentPrice, shortTarget), Theme.FG_QUATERNARY)
                AppendRtf(rtb, String.Format("--> {0:F1}  [{1}]",
                                              v.AdjustedShortTarget, v.TargetCapReasonShort) & Environment.NewLine, Theme.ACC_WARN, bold:=True)
            End If
        Else
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          shortStop, r.CurrentPrice, shortTarget, rrRatio, atrStop, atrTarget) & Environment.NewLine, Theme.ACC_SHORT)
        End If

        ' Short structural levels (from swing pivot detection)
        If r.SwingTargetShort > 0 AndAlso r.SwingStopShort > 0 Then
            Dim swingRisk   As Double = r.SwingStopShort - r.CurrentPrice
            Dim swingReward As Double = r.CurrentPrice - r.SwingTargetShort
            Dim swingRR As String = FormatRR(swingReward, swingRisk)
            AppendRtf(rtb, "  Short structural: ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                          r.SwingStopShort, r.CurrentPrice, r.SwingTargetShort, swingRR, swingRisk, swingReward) & Environment.NewLine, Theme.ACC_INFO)
        ElseIf r.SwingTargetShort > 0 Then
            AppendRtf(rtb, "  Short structural: ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Target {0,9:F1}  (stop unset: no swing high above entry within lookback)", r.SwingTargetShort) & Environment.NewLine, Theme.FG_QUATERNARY)
        ElseIf r.SwingStopShort > 0 Then
            AppendRtf(rtb, "  Short structural: ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("Stop {0,9:F1}  (target unset: no swing low below entry within lookback)", r.SwingStopShort) & Environment.NewLine, Theme.FG_QUATERNARY)
        End If

        If v.KellyPWin > 0 Then
            Dim isNoTradeBias As Boolean = v.Verdict.StartsWith("NO TRADE")
            Dim capTag As String = If(v.KellyCapped, "  [CAPPED]", "")
            AppendRtf(rtb, Environment.NewLine, Theme.BORDER_CARD)
            If isNoTradeBias Then
                AppendRtf(rtb, String.Format("KELLY SIZING  [BIAS ONLY — NO TRADE]{0}" & Environment.NewLine,
                                              capTag), Theme.FG_SECONDARY, bold:=True)
            Else
                AppendRtf(rtb, String.Format("KELLY SIZING{0}" & Environment.NewLine,
                                              capTag), Theme.FG_SECONDARY, bold:=True)
            End If
            AppendRtf(rtb, "  Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets." & Environment.NewLine, Theme.FG_QUATERNARY)
            AppendRtf(rtb, "  Treat as directional bias indicator only." & Environment.NewLine, Theme.FG_QUATERNARY)
            AppendRtf(rtb, "  p(win):   ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("{0:P1}" & Environment.NewLine, v.KellyPWin), Theme.FG_PRIMARY)
            AppendRtf(rtb, "  f* / Half-Kelly:  ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("{0:P2}  /  {1:P2}" & Environment.NewLine,
                                          v.KellyF, v.KellyFHalf), Theme.FG_PRIMARY)
            AppendRtf(rtb, "  Applied fraction: ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("{0:P2}" & Environment.NewLine, v.KellyFApplied), Theme.FG_PRIMARY)
            AppendRtf(rtb, "  Risk $:    ", Theme.FG_TERTIARY)
            AppendRtf(rtb, String.Format("${0:F2}" & Environment.NewLine, v.KellyRiskUsd), Theme.FG_PRIMARY)
            AppendRtf(rtb, If(isNoTradeBias, "  Lean: ", "  Contracts: "), Theme.FG_TERTIARY)
            Dim contractColour As Color = If(v.KellyContracts >= 1, Theme.ACC_STRONG_LONG, Theme.ACC_WARN)
            Dim contractStr As String
            If isNoTradeBias Then
                contractStr = If(v.KellyContracts >= 1,
                                 String.Format("{0} {1}  (not a trade signal)", v.KellyContracts.ToString(), If(v.KellyContracts = 1, "contract", "contracts")),
                                 "< 1 contract  (bias only; not a trade signal)")
            Else
                contractStr = If(v.KellyContracts >= 1,
                                 v.KellyContracts.ToString() & " " & If(v.KellyContracts = 1, "contract", "contracts"),
                                 "< 1 contract  (stop too wide for min size)")
            End If
            AppendRtf(rtb, contractStr & Environment.NewLine, contractColour, bold:=True)
        End If
    End Sub

End Class
