' analysis/OutlierAudit.vb
' Two diagnostic passes:
'   1. OFI Ratio outliers — count rows > 100 / > 1000, show top 10 by ratio.
'   2. OI x CVD asymmetry — confirmed_long vs confirmed_short broken down by
'      Regime and FundingBias to determine if the 24:1 long:short ratio is
'      regime-period bias or an asymmetric algorithm.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Linq

Public Class OutlierAudit

    Public Shared Function ComputeOfi(rows As List(Of CsvRow)) As OfiOutlierResult
        Dim result As New OfiOutlierResult() With {.TotalRows = rows.Count}

        For Each row In rows
            If row.OfiRatio > 100  Then result.RowsAbove100  += 1
            If row.OfiRatio > 1000 Then result.RowsAbove1000 += 1
        Next

        result.Top10 = rows _
            .Where(Function(r) r.OfiRatio > 100) _
            .OrderByDescending(Function(r) r.OfiRatio) _
            .Take(10) _
            .Select(Function(r) New OfiOutlierRow() With {
                .Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                .OfiRatio  = r.OfiRatio,
                .OfiBidVol = r.OfiBidVol,
                .OfiAskVol = r.OfiAskVol
            }) _
            .ToList()

        If result.RowsAbove1000 > 5 Then
            result.Recommendation = String.Format(
                "{0} rows have OFIRatio > 1000. This is persistent, not isolated. " &
                "Investigate CalcOFI for edge cases when orderbook is thin " &
                "(e.g. very low AskVol near zero). Consider adding a ratio cap " &
                "in CalcOFI or a clamp in the scoring penalty branch.",
                result.RowsAbove1000)
        ElseIf result.RowsAbove100 > 0 Then
            result.Recommendation = String.Format(
                "{0} rows above 100; {1} above 1000. " &
                "Sporadic spikes — likely thin-book moments during low-liquidity sessions. " &
                "Monitor over next 500+ rows before adding a cap.",
                result.RowsAbove100, result.RowsAbove1000)
        Else
            result.Recommendation = "No OFIRatio outliers above 100. No action needed."
        End If

        Return result
    End Function

    Public Shared Function ComputeOiCvdAsymmetry(rows As List(Of CsvRow)) As OiCvdAsymmetryResult
        Dim result As New OiCvdAsymmetryResult()

        For Each row In rows
            Dim oc = row.OiCvdOutcome.Trim().ToUpper()
            If oc = "CONFIRMED_LONG"  Then result.TotalConfirmedLong  += 1
            If oc = "CONFIRMED_SHORT" Then result.TotalConfirmedShort += 1

            If oc = "CONFIRMED_LONG" OrElse oc = "CONFIRMED_SHORT" Then
                Dim reg = If(row.Regime, "UNKNOWN")
                If Not result.ByRegime.ContainsKey(reg) Then
                    result.ByRegime(reg) = New LongShortCount()
                End If
                If oc = "CONFIRMED_LONG" Then
                    result.ByRegime(reg).LongCount  += 1
                Else
                    result.ByRegime(reg).ShortCount += 1
                End If

                Dim fb = If(row.FundingBias, "UNKNOWN")
                If Not result.ByFundingBias.ContainsKey(fb) Then
                    result.ByFundingBias(fb) = New LongShortCount()
                End If
                If oc = "CONFIRMED_LONG" Then
                    result.ByFundingBias(fb).LongCount  += 1
                Else
                    result.ByFundingBias(fb).ShortCount += 1
                End If
            End If
        Next

        Dim totalSignals As Integer = result.TotalConfirmedLong + result.TotalConfirmedShort
        If totalSignals = 0 Then
            result.Verdict = "INCONCLUSIVE"
            Return result
        End If

        Dim longRatio As Double = CDbl(result.TotalConfirmedLong) / totalSignals

        ' Check if asymmetry collapses when we stratify by regime
        Dim regimeMax As Double = 0
        Dim regimeMin As Double = 1
        For Each kvp In result.ByRegime
            Dim regTotal = kvp.Value.LongCount + kvp.Value.ShortCount
            If regTotal >= 10 Then
                Dim rRatio = CDbl(kvp.Value.LongCount) / regTotal
                If rRatio > regimeMax Then regimeMax = rRatio
                If rRatio < regimeMin Then regimeMin = rRatio
            End If
        Next

        If longRatio > 0.8 AndAlso (regimeMax - regimeMin) < 0.2 Then
            result.Verdict = "ASYMMETRIC_ALGORITHM"
        ElseIf (regimeMax - regimeMin) >= 0.4 Then
            result.Verdict = "REGIME_PERIOD_BIAS"
        Else
            result.Verdict = "INCONCLUSIVE"
        End If

        Return result
    End Function

End Class
