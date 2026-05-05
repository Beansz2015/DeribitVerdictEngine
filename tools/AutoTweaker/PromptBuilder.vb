' tools/AutoTweaker/PromptBuilder.vb
' Builds the system + user message pair sent to Claude.
' System message inlines trader-profile constraints from Section 4 (rejected approaches)
' so Claude never proposes a banned pattern.
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Text

Public Class PromptBuilder

    ' Trader-profile Section 4 constraints inlined as a hard instruction block.
    ' This list mirrors docs/trader-profile.md Section 4 + scoring design invariants.
    Private Shared ReadOnly SystemConstraints As String =
        "You are a settings optimiser for a technical-analysis trading engine." & vbLf &
        "Your job is to propose small, targeted changes to settings.json that reduce the empirical failure rate." & vbLf &
        vbLf &
        "HARD CONSTRAINTS — never propose changes that violate these:" & vbLf &
        "1. No fixed-% profit targets. The engine uses structural swing targets only. " &
            "Reject any key containing '_fixed_pct_' or proposing fixed percentage take-profit." & vbLf &
        "2. No ATR-based stop placement for execution. ATR is for sizing reference only." & vbLf &
        "3. No non-directional padding. A signal that fires on both sides equally (e.g., BBW NONE = +1) " &
            "must NEVER return as a positive reward. Removed in v0.18; do not reintroduce." & vbLf &
        "4. Funding must not appear in Step 2 scoring. It is a Step 3 modifier only. " &
            "Do not propose any path under 'indicators.funding' that adds a scoring bonus/penalty in Step 2." & vbLf &
        "5. The MTF gate must never be disabled. Do not propose setting mtf_gate.enabled = false." & vbLf &
        "6. The regime alignment gate (Pass 2c) must never be disabled. " &
            "Do not propose setting regime_weights.enabled = false." & vbLf &
        "7. Rejected indicators: Stochastic, MACD, CMF. Do not propose adding them." & vbLf &
        "8. No flat TRANSITIONAL penalties. ADX-proximity scale is required; flat -2 was removed." & vbLf &
        "9. Do not modify the 'version' key — the applier manages version bumping automatically." & vbLf &
        "10. Keys removed in the v15 cleanup (bbw_none_bonus, oi_prev15m, oi_prev60m, atr_avg20d, " &
            "static_vol_high, static_vol_mid, static_vol_low) must never be reintroduced." & vbLf &
        vbLf &
        "SCOPE CAP: Propose AT MOST 3 key changes in a single diff. Conservative, small steps." & vbLf &
        vbLf &
        "OUTPUT FORMAT: Respond with valid JSON only — no markdown, no preamble:" & vbLf &
        "{" & vbLf &
        "  ""reasoning"": ""<one paragraph explaining why these changes address the failure rate>"", " & vbLf &
        "  ""diff"": [" & vbLf &
        "    {""path"": ""scoring.bbw_squeeze_penalty"", ""old_value"": 1.5, ""new_value"": 1.0, " &
            """justification"": ""<why this specific key at this specific value>""}" & vbLf &
        "  ]" & vbLf &
        "}" & vbLf &
        "If no change is warranted, return an empty diff array: {""reasoning"": ""..."", ""diff"": []}"

    ' Build system + user messages for the Claude API call.
    ' trigger: human-readable failure-rate summary line (e.g. "47.2% > 40% threshold over 120 rows")
    Public Shared Function Build(settingsJson As String,
                                  csvRows As List(Of CsvRow),
                                  failureCells As List(Of FailureCellResult),
                                  pickedCellHistory As List(Of PickedCellEntry),
                                  trigger As String) As (SystemMsg As String, UserMsg As String)

        Dim user As New StringBuilder()

        user.AppendLine("## Trigger")
        user.AppendLine(trigger)
        user.AppendLine()

        user.AppendLine("## Current settings.json")
        user.AppendLine("```json")
        user.AppendLine(settingsJson)
        user.AppendLine("```")
        user.AppendLine()

        ' Failure-rate matrix as markdown table (per tier)
        user.AppendLine("## Failure-Rate Matrix (ATR-based forward returns)")
        user.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            user.AppendLine("### " & tier)
            Dim thrs As Double() = If(tier.StartsWith("STRONG"),
                                      AnalysisConstants.StrongAtrThresholds,
                                      AnalysisConstants.MediumAtrThresholds)
            Dim hdr As New StringBuilder("| Window |")
            For Each t In thrs : hdr.Append(String.Format(" {0:F1}x ATR |", t)) : Next
            user.AppendLine(hdr.ToString())
            Dim sep As New StringBuilder("|--------|")
            For Each t In thrs : sep.Append("------------|") : Next
            user.AppendLine(sep.ToString())
            For Each w In AnalysisConstants.HoldWindowsMinutes
                Dim row As New StringBuilder(String.Format("| {0,4}m |", w))
                For Each t In thrs
                    Dim c = failureCells.Find(Function(x) x.VerdictTier = tier AndAlso
                                                           x.WindowMin = w AndAlso
                                                           x.AtrThreshold = t)
                    If c Is Nothing OrElse c.SampleSize = 0 Then
                        row.Append(" n/a        |")
                    Else
                        Dim star As String = If(c.IsRecommended, "★", " ")
                        row.Append(String.Format(" {4}{0:P0} n={1} [{2:P0}-{3:P0}] |",
                                                 c.FailureRate, c.SampleSize,
                                                 c.CiLow, c.CiHigh, star))
                    End If
                Next
                user.AppendLine(row.ToString())
            Next
            user.AppendLine()
        Next

        ' Picked-cell history (last 20 entries)
        user.AppendLine("## Picked-Cell History (last 20 auto-tweaker runs)")
        user.AppendLine("| Timestamp           | Tier        | Window | ATR thr |")
        user.AppendLine("|---------------------|-------------|--------|---------|")
        Dim startIdx As Integer = Math.Max(0, pickedCellHistory.Count - 20)
        For i As Integer = startIdx To pickedCellHistory.Count - 1
            Dim e = pickedCellHistory(i)
            user.AppendLine(String.Format("| {0,-19} | {1,-11} | {2,4}m  | {3,5:F2}  |",
                                          e.Ts, e.Tier, e.WindowMin, e.AtrThreshold))
        Next
        user.AppendLine()

        ' Last 50 CSV rows
        user.AppendLine("## Recent CSV rows (last 50, most recent last)")
        user.AppendLine("```")
        user.AppendLine("Timestamp, Price, ATR, Verdict, Regime, FundingBias, VerdictContext, OiCvdOutcome")
        Dim rowStart As Integer = Math.Max(0, csvRows.Count - 50)
        For i As Integer = rowStart To csvRows.Count - 1
            Dim r = csvRows(i)
            user.AppendLine(String.Format("{0}, {1:F0}, {2:F1}, {3}, {4}, {5}, {6}, {7}",
                                          r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                          r.Price, r.ATR,
                                          r.Verdict, r.Regime, r.FundingBias,
                                          r.VerdictContext, r.OiCvdOutcome))
        Next
        user.AppendLine("```")
        user.AppendLine()

        user.AppendLine("Based on the failure-rate data above, propose changes to settings.json " &
                        "that would lower the aggregate failure rate at the ★ (recommended) cells. " &
                        "Respect all constraints in the system message. Output JSON only.")

        Return (SystemConstraints, user.ToString())
    End Function

End Class
