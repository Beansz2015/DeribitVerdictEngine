' tools/AutoTweaker/PromptBuilder.vb
' Builds the system + user message pair sent to Claude.
' System message inlines trader-profile constraints from Section 4 (rejected approaches)
' so Claude never proposes a banned pattern.
'
' settings-snapshot-history-proposal.md §3n extension:
'   - System message exposes the configurable diff-scope cap.
'   - System message describes the optional REVERT action and the composite-score
'     formula (identical wording must appear in CompositeScorer.vb and UserManual.md §20e).
'   - User message carries the ACTIVE snapshot manifest CSV and the current conditions
'     vector when both are available.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Public Class PromptBuilder

    ' System message template — {0} is the configurable diff-scope cap from tweaker_config.
    Private Const SystemTemplate As String =
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
        "11. Trader-owned risk-preference keys are NEVER auto-tuned. Do not propose changing " &
            "'scoring.min_tradeable_move_pct' (the minimum-tradeable-move floor — the trader's slippage " &
            "threshold, shared with the eval de-confound), any 'kelly.*' key, any " &
            "'session_volume.sessions[].execution_resolution' (the per-session execution timeframe — a " &
            "strategy/regime selector that defines a calibration-regime boundary, not a failure-rate lever), " &
            "any 'session_volume.sessions[].roc_magnitude_threshold' (the per-session 3-min ROC magnitude " &
            "override — a provisional value re-baselined manually, not a failure-rate lever), " &
            "or any 'resolution_profiles.*' key (the per-resolution 3-min ROC overrides — a provisional " &
            "seed re-baselined manually, not a failure-rate lever). " &
            "These are set by the trader, not failure-rate levers." & vbLf &
        "12. Never propose any 'network.*' key. The entire network block is transport plumbing — " &
            "REST timeout/retry, the WebSocket transport/url/heartbeat/staleness/cooldown/fallback flags, " &
            "and shadow_parity (a dev/validation toggle). None of it has any failure-rate linkage; the " &
            "transport is chosen by the trader, not optimised. (Enforced in code: SettingsDiffApplier " &
            "rejects 'network.' as well.)" & vbLf &
        "13. Never propose any 'exit_guard.*' key. The exit_guard block is a display/alert-only " &
            "overlay (a realtime exit cue with a debounce + audible alarm) — a trader risk/display " &
            "preference with zero scoring impact and no failure-rate linkage. (Enforced in code: " &
            "SettingsDiffApplier rejects 'exit_guard.' as well.)" & vbLf &
        "14. Never propose any 'auto_run.*' key. The auto_run block is the run cadence/trigger — the " &
            "interval and the trigger_mode (interval vs on-close bar-close firing). It is an operational " &
            "preference set by the trader with no failure-rate linkage (and on-close logs the SAME " &
            "observations, just at bar-close moments). (Enforced in code: SettingsDiffApplier rejects " &
            "'auto_run.' as well.)" & vbLf &
        "15. Never propose any 'live_strip.*' key. The live_strip block drives a display-only live TAPE " &
            "strip (a between-runs microstructure readout — deliberately NOT a verdict, zero scoring " &
            "impact, never writes the CSV). It is a display preference with no failure-rate linkage. " &
            "(Enforced in code: SettingsDiffApplier rejects 'live_strip.' as well.)" & vbLf &
        "16. Never propose 'indicators.OFI.averaging_enabled'. It is the on/off feature flag for " &
            "time-averaged OFI (a structural toggle, not a failure-rate threshold). UNLIKE the block-wide " &
            "constraints above, this is a SINGLE-key exclusion — the sibling OFI keys REMAIN tunable: " &
            "'indicators.OFI.avg_window_sec' (the averaging / EMA window — it shapes the OFI signal, a " &
            "genuine failure-rate lever), 'indicators.OFI.buy_dominant_ratio', " &
            "'indicators.OFI.sell_dominant_ratio', and 'indicators.OFI.book_depth' are all on-surface. " &
            "(Enforced in code: SettingsDiffApplier exact-match rejects 'indicators.ofi.averaging_enabled'.)" & vbLf &
        "17. Never propose any 'scoring.hold_*' key. The six CalcHoldStatus hold/exit thresholds " &
            "(hold_roc_take_profit_long/short, hold_rsi_hold_long/short, hold_rsi_evaluate_long/short) " &
            "are the trader's hand-tuned hold/exit discipline. HoldStatus is advisory during a declared " &
            "position and never feeds the failure-rate matrix, so these keys have no failure-rate " &
            "linkage — same class as 'kelly.*'. The sibling 'scoring.*' tunables (verdict percentages, " &
            "penalties, etc.) REMAIN on the tweaker surface. (Enforced in code: SettingsDiffApplier " &
            "rejects the 'scoring.hold_' prefix as well.)" & vbLf &
        "18. Never propose any 'signal_bridge.*' key. The signal_bridge block configures the " &
            "order-app signal-file emission (an atomic-write verdict_signal.json mirror of the " &
            "already-computed verdict) — transport plumbing with zero scoring impact and no " &
            "failure-rate linkage; same class as 'network.*'. (Enforced in code: SettingsDiffApplier " &
            "rejects 'signal_bridge.' as well.)" & vbLf &
        "19. Aggressor velocity (indicators.aggressor_velocity) is a THREE-TIER surface. " &
            "Never propose 'indicators.aggressor_velocity.enabled' or " &
            "'indicators.aggressor_velocity.scoring_enabled' (feature switches — the latter is the " &
            "data-gated scoring gate, flipped only after the correlation gate clears). Never propose " &
            "any 'indicators.aggressor_velocity.default.*' or 'indicators.aggressor_velocity.sessions.*' " &
            "key (the per-session norm_window_sec / burst_ratio_threshold re-baseline tier — " &
            "hand-tuned by the trader like session_volume.sessions[].roc_magnitude_threshold, " &
            "HARD CONSTRAINT 11 class). The FLAT keys REMAIN tunable: " &
            "'indicators.aggressor_velocity.fast_window_sec', '...direction_lean_floor', " &
            "'...gross_floor_usd_per_sec', '...upgrade_bonus', '...contra_penalty'. " &
            "(Enforced in code: SettingsDiffApplier exact-match rejects the two switches and " &
            "rejects the 'default.'/'sessions.' prefixes.)" & vbLf &
        vbLf &
        "SCOPE CAP: Propose AT MOST {0} key changes in a single TWEAK diff. Conservative, small steps." & vbLf &
        vbLf &
        "RESPONSE ACTIONS — choose ONE per response:" & vbLf &
        "  1. TWEAK — propose up to {0} key changes to settings.json. Standard rejection list applies." & vbLf &
        "  2. REVERT — if a past snapshot's conditions strongly match the current conditions AND" & vbLf &
        "     its composite score is meaningfully higher than the current settings would likely" & vbLf &
        "     achieve, propose reverting to that snapshot. The snapshot manifest of ACTIVE rows" & vbLf &
        "     and the current conditions vector are provided in the user message." & vbLf &
        vbLf &
        "REVERT scope: a revert is a wholesale replacement, so the SCOPE CAP does NOT apply to" & vbLf &
        "reverts. The snapshot's provenance — proven successful over a streak of below-threshold" & vbLf &
        "rounds under bucket-matching conditions — is the validation gate." & vbLf &
        vbLf &
        "Composite score formula used for snapshot ranking:" & vbLf &
        "  StreakLengthClamped = min(StreakLength, StreakLengthClamp)" & vbLf &
        "  Score = (100 - AvgFailureRatePct) + StreakLengthClamped * StreakWeight" & vbLf &
        "Defaults: StreakWeight = 1.5, StreakLengthClamp = 20. Failure rate dominates" & vbLf &
        "(1 point per percentage point); streak adds a secondary nudge." & vbLf &
        vbLf &
        "Default to TWEAK unless conditions match a past snapshot strongly AND the score delta is meaningful." & vbLf &
        vbLf &
        "OUTPUT FORMAT (TWEAK): valid JSON only, no markdown:" & vbLf &
        "{" & vbLf &
        "  ""action"": ""tweak""," & vbLf &
        "  ""reasoning"": ""<one paragraph explaining why these changes address the failure rate>""," & vbLf &
        "  ""diff"": [" & vbLf &
        "    {""path"": ""scoring.bbw_squeeze_penalty"", ""old_value"": 1.5, ""new_value"": 1.0, " &
            """justification"": ""<why this specific key at this specific value>""}" & vbLf &
        "  ]" & vbLf &
        "}" & vbLf &
        vbLf &
        "OUTPUT FORMAT (REVERT): valid JSON only, no markdown:" & vbLf &
        "{" & vbLf &
        "  ""action"": ""revert""," & vbLf &
        "  ""revert_target"": ""<snapshot filename from manifest>""," & vbLf &
        "  ""reasoning"": ""<why this snapshot matches and why its score wins>""" & vbLf &
        "}" & vbLf &
        vbLf &
        "If no change is warranted, return an empty diff array: " &
        "{""action"": ""tweak"", ""reasoning"": ""..."", ""diff"": []}"

    ' Build system + user messages for the Claude API call.
    ' trigger: human-readable failure-rate summary line (e.g. "47.2% > 40% threshold over 120 rows")
    ' maxKeysPerProposal: configurable diff-scope cap (§3j).
    ' manifestActiveRows: CSV of ACTIVE snapshot rows (may be empty); included in user msg when non-empty.
    ' conditions: extracted conditions vector for the just-completed window (may be Nothing).
    Public Shared Function Build(settingsJson As String,
                                  csvRows As List(Of CsvRow),
                                  failureCells As List(Of FailureCellResult),
                                  pickedCellHistory As List(Of PickedCellEntry),
                                  trigger As String,
                                  manifestActiveRows As String,
                                  conditions As ConditionsVector,
                                  maxKeysPerProposal As Integer) As (SystemMsg As String, UserMsg As String)

        ' NOTE: SystemTemplate embeds literal JSON braces ({ } in the OUTPUT FORMAT
        ' blocks), so String.Format would mis-read them as format items and throw
        ' ("Expected an ASCII digit"). The only substitution is {0} (the scope cap),
        ' so a plain replace is correct and brace-safe.
        Dim systemMsg As String = SystemTemplate.Replace("{0}", maxKeysPerProposal.ToString())

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
                        Dim star As String = If(c.IsRecommended, "*", " ")
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

        ' Snapshot manifest (ACTIVE rows only) — empty if no snapshots yet
        If Not String.IsNullOrWhiteSpace(manifestActiveRows) Then
            user.AppendLine("## Snapshot Manifest (ACTIVE rows only)")
            user.AppendLine("```csv")
            user.Append(manifestActiveRows)
            If Not manifestActiveRows.EndsWith(vbLf) Then user.AppendLine()
            user.AppendLine("```")
            user.AppendLine()
        End If

        ' Current conditions vector — used for revert evaluation
        If conditions IsNot Nothing AndAlso Not String.IsNullOrEmpty(conditions.ConditionBucket) Then
            user.AppendLine("## Current Conditions Vector (this round's window)")
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- ConditionBucket: {0}", conditions.ConditionBucket))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- RegimeMix: {0}", conditions.RegimeMix))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- AtrScaleAvg: {0:F4}  (min {1:F4} / max {2:F4})",
                conditions.AtrScaleAvg, conditions.AtrScaleMin, conditions.AtrScaleMax))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- Funding range: {0:F8} .. {1:F8}",
                conditions.FundingMin, conditions.FundingMax))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- NetPriceMovePct: {0:F4}%", conditions.NetPriceMovePct))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- VolumeRatioAvg: {0:F4}", conditions.VolumeRatioAvg))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- VerdictTierMix: {0}", conditions.VerdictTierMix))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- VWAPDevAvg: {0:F4}  (min {1:F4} / max {2:F4})",
                conditions.VWAPDevAvg, conditions.VWAPDevMin, conditions.VWAPDevMax))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- SpreadRegimeMix: {0}", conditions.SpreadRegimeMix))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- OFIImbalanceMix: {0}", conditions.OFIImbalanceMix))
            user.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "- AvgFailureRatePct (current round): {0:F2}%", conditions.AvgFailureRatePct))
            user.AppendLine()
        End If

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

        user.AppendLine("Based on the failure-rate data above, propose either a TWEAK diff " &
                        "(<=" & maxKeysPerProposal.ToString() & " keys) or a REVERT to a past snapshot. " &
                        "Respect all constraints in the system message. Output JSON only.")

        Return (systemMsg, user.ToString())
    End Function

End Class
