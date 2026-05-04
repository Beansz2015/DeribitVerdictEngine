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

    Public Shared Function Compute(rows As List(Of CsvRow)) As FundingMomentumDiagnosticResult
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

        Dim currentThresholdBp As Double = 0.001 * 10000  ' 0.001% = 1 bp (v22 default in decimal * 10000)
        Dim pct95Bp As Double = result.Pct95 * 10000

        If pct95Bp < 0.5 Then
            result.Recommendation =
                "95th percentile of |FundingDelta| is below 0.5 bp. " &
                "This is a genuine REST-cadence ceiling: the funding rate does not move " &
                "meaningfully within a 60s poll window. " &
                "No change recommended — defer to WebSocket migration."
        ElseIf result.ImpliedThreshold30Pct * 10000 < 0.5 Then
            result.Recommendation =
                "Implied 30%-firing threshold is below 0.5 bp. " &
                "Consider lowering momentum_threshold to 0.0005 (0.05 bp) for v24+, " &
                "but validate against RISING/FALLING false-positive rate first."
        Else
            result.Recommendation = String.Format(
                "Implied threshold for ~30% non-FLAT rate: {0:F4} bp. " &
                "Current threshold: {1:F4} bp. " &
                "If non-FLAT rate is below 5%, consider halving the threshold.",
                result.ImpliedThreshold30Pct * 10000,
                currentThresholdBp)
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
