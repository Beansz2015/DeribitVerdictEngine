' analysis/FundingMomentumDiagnostic.vb
' Reads the FundingDelta column (v0.4) and computes the empirical distribution
' of |FundingDelta| in basis-point buckets, percentile table, and the implied
' threshold to achieve a target non-FLAT firing rate.
'
' Answers: should v23+ lower the funding_momentum_threshold further?
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Math

Public Class FundingMomentumDiagnostic

    ' cfg is threaded in (offline-analysis-report-audit-proposal.md §4.2) so the
    ' "current threshold" reported in the recommendation reflects the LIVE
    ' cfg.Indicators.Funding.MomentumThreshold, not the pre-v22 hardcoded 0.001.
    Public Shared Function Compute(rows As List(Of CsvRow),
                                   cfg As EngineSettings) As FundingMomentumDiagnosticResult
        Dim result As New FundingMomentumDiagnosticResult()
        result.TotalRows = rows.Count

        Dim absVals As New List(Of Double)()
        For Each row In rows
            Dim absDelta As Double = Abs(row.FundingDelta)
            If absDelta > 0 Then
                result.NonZeroRows += 1
                absVals.Add(absDelta)
            End If
        Next

        absVals.Sort()
        result.AbsValues = absVals

        If absVals.Count = 0 Then
            result.Recommendation = "No non-zero FundingDelta rows found. " &
                                    "Funding rate may not have changed during the logged period " &
                                    "(only distinct changes are appended). " &
                                    "This is expected at REST polling cadence — " &
                                    "defer threshold tuning to WebSocket migration."
            Return result
        End If

        result.Pct50 = Percentile(absVals, 0.50)
        result.Pct75 = Percentile(absVals, 0.75)
        result.Pct90 = Percentile(absVals, 0.90)
        result.Pct95 = Percentile(absVals, 0.95)

        ' Implied threshold: the value such that ~30% of non-zero rows exceed it.
        ' Equivalent to the 70th percentile of the non-zero distribution.
        result.ImpliedThreshold30Pct = Percentile(absVals, 0.70)

        ' Live funding momentum threshold (rate units → bp). Reads the current setting
        ' instead of the pre-v22 hardcoded 0.001 (§4.2).
        Dim currentThresholdRate As Double = If(cfg IsNot Nothing, cfg.Indicators.Funding.MomentumThreshold, 0.00001)
        Dim currentThresholdBp   As Double = currentThresholdRate * 10000
        Dim impliedBp            As Double = result.ImpliedThreshold30Pct * 10000
        Dim pct95Bp              As Double = result.Pct95 * 10000

        If pct95Bp < 0.5 Then
            result.Recommendation =
                "95th percentile of |FundingDelta| is below 0.5 bp. " &
                "This is a genuine REST-cadence ceiling: the funding rate does not move " &
                "meaningfully within a 60s poll window. " &
                "No change recommended — defer to WebSocket migration."
        ElseIf impliedBp < 0.5 Then
            result.Recommendation = String.Format(
                "Implied 30%-firing threshold is below 0.5 bp ({0:F4} bp) vs the live " &
                "momentum_threshold {1:F4} bp. The implied value is in the REST-noise floor; " &
                "validate against RISING/FALLING false-positive rate before lowering further.",
                impliedBp, currentThresholdBp)
        Else
            result.Recommendation = String.Format(
                "Implied threshold for ~30% non-FLAT rate: {0:F4} bp. " &
                "Live momentum_threshold: {1:F4} bp. " &
                "If non-FLAT rate is below 5%, consider moving the live threshold toward the implied value.",
                impliedBp, currentThresholdBp)
        End If

        Return result
    End Function

    Private Shared Function Percentile(sorted As List(Of Double), p As Double) As Double
        If sorted.Count = 0 Then Return 0
        Dim idx As Double = p * (sorted.Count - 1)
        Dim lo As Integer = CInt(Math.Floor(idx))
        Dim hi As Integer = Math.Min(lo + 1, sorted.Count - 1)
        Dim frac As Double = idx - lo
        Return sorted(lo) * (1 - frac) + sorted(hi) * frac
    End Function

End Class
