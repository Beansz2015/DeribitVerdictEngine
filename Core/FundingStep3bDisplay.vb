' Core/FundingStep3bDisplay.vb
' [D-9 (b) + EF-4 (a), docs/engine-fix-build-spec-2026-09-21.md §5.2]
' The ONE place that turns Step 3b's actual effect (VerdictResult.FundingStep3bLongPoints /
' FundingStep3bShortPoints) into display text and a display tone. Both render surfaces call it:
'   * the text surface — UI/MainForm_PlaintextSnapshot.vb AppendFunding, the "Momentum:" line;
'   * the card — UI/MainForm_Render_Cards.vb BuildGroupFunding (the "Step 3b:" row) and the
'     three funding-momentum colour sites (the Funding Mom MiniMeter, BuildRowFundingMom and
'     the breakdown footer aggregate), which map Tone to a Theme colour.
' Keeping the text and the tone here, host-agnostic, lets the harness pin what both surfaces
' show (the P5-test text-parity harness cannot see the card).
' ⛔ Never derive the effect from the breakdown note ("STEP3b: -1[L] crowding↑"): that is a
' display string, and a reader keyed on its wording drifts the day the wording changes.
' Host-agnostic: no System.Windows.Forms reference.

Public Enum FundingStep3bTone
    ''' <summary>No effect: Step 3b disabled, no arm fired, or a clamp absorbed it.</summary>
    Neutral
    ''' <summary>Step 3b applied a crowding PENALTY (a negative effect on a side).</summary>
    Caution
    ''' <summary>Step 3b applied a de-crowding SOFTEN (a positive effect on a side).</summary>
    Relief
End Enum

Public NotInheritable Class FundingStep3bDisplay

    Private Sub New()
    End Sub

    ''' <summary>Step 3b's effect in the breakdown's own shape: "-1[L]", "+1[S]", or "none".
    ''' If both sides ever moved (no current arm does that), both are listed, long first.</summary>
    Public Shared Function EffectText(v As VerdictResult) As String
        If v Is Nothing Then Return "none"
        Dim parts As New List(Of String)()
        If v.FundingStep3bLongPoints <> 0 Then parts.Add(Signed(v.FundingStep3bLongPoints) & "[L]")
        If v.FundingStep3bShortPoints <> 0 Then parts.Add(Signed(v.FundingStep3bShortPoints) & "[S]")
        If parts.Count = 0 Then Return "none"
        Return String.Join(" ", parts)
    End Function

    ''' <summary>The sign of the effect: any penalty → Caution; else any soften → Relief;
    ''' else Neutral. A penalty wins over a soften, so a caution is never hidden.</summary>
    Public Shared Function Tone(v As VerdictResult) As FundingStep3bTone
        If v Is Nothing Then Return FundingStep3bTone.Neutral
        If v.FundingStep3bLongPoints < 0 OrElse v.FundingStep3bShortPoints < 0 Then Return FundingStep3bTone.Caution
        If v.FundingStep3bLongPoints > 0 OrElse v.FundingStep3bShortPoints > 0 Then Return FundingStep3bTone.Relief
        Return FundingStep3bTone.Neutral
    End Function

    ''' <summary>The text surface's "Momentum:" line (EF-4 (a): re-formatted, not added).
    ''' The momentum state, whether Step 3b is enabled (it explains a zero effect), and the
    ''' effect. The configured soften / amplify values left this line; they stay in
    ''' settings.json and in the breakdown note.</summary>
    Public Shared Function MomentumLine(r As IndicatorResults, v As VerdictResult, cfg As EngineSettings) As String
        Return String.Format("  Momentum: {0}  |  Enabled: {1}  |  Effect: {2}",
                             If(r Is Nothing, Nothing, r.FundingMomentum),
                             If(IsEnabled(cfg), "YES", "NO"),
                             EffectText(v))
    End Function

    ''' <summary>The card's FUNDING group "Step 3b:" value, the card twin of MomentumLine's
    ''' last two fields in the card's own "=" and "|" style.</summary>
    Public Shared Function CardValue(v As VerdictResult, cfg As EngineSettings) As String
        Return String.Format("Enabled={0} | Effect={1}", If(IsEnabled(cfg), "Y", "N"), EffectText(v))
    End Function

    Private Shared Function IsEnabled(cfg As EngineSettings) As Boolean
        Return cfg IsNot Nothing AndAlso cfg.Indicators IsNot Nothing AndAlso
               cfg.Indicators.Funding IsNot Nothing AndAlso cfg.Indicators.Funding.MomentumEnabled
    End Function

    Private Shared Function Signed(n As Integer) As String
        Return If(n > 0, "+" & n.ToString(Globalization.CultureInfo.InvariantCulture),
                  n.ToString(Globalization.CultureInfo.InvariantCulture))
    End Function

End Class
