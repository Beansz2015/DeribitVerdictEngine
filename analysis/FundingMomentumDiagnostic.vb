' analysis/FundingMomentumDiagnostic.vb
' Reads the FundingDelta column (v0.4) and computes the empirical distribution
' of |FundingDelta| in basis-point buckets, percentile table, and the implied
' threshold to achieve a target non-FLAT firing rate.
'
' Answers: should the funding_momentum_threshold move?
'
' [text refresh 2026-07-21, offline-matrix-placed-target-proposal.md M4] The canned
' recommendations were written in the REST-polling era and still said "defer to WebSocket
' migration" — two eras stale. The WS cutover shipped at v42 (2026-06-24), and v53 re-cut
' the momentum window from a 3-sample count to a 5-minute TIME anchor, which changed what
' FundingDelta even measures: it is now "funding moved more than T over ≥ W minutes",
' identical at every run cadence, rather than a quantity that scaled with polling rate.
' Anchored deltas run SMALLER than the old count-window deltas (the funding premium
' oscillates at short horizons and partially cancels), so a percentile table from this
' book is not comparable with a pre-v53 one. Recommendations below say that plainly
' instead of blaming a poll cadence that no longer exists.
'
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
            result.Recommendation = "No non-zero FundingDelta rows found — the funding rate did not " &
                                    "move measurably over the anchored window anywhere in this book. " &
                                    "On a live WS feed that is unusual rather than expected; check the " &
                                    "ticker feed is delivering funding_8h before reading anything into " &
                                    "a FLAT-dominated momentum state."
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

        ' Common tail: this distribution is of v53 TIME-ANCHORED deltas, so it is not
        ' comparable with a pre-v53 table and a threshold move should be judged on the
        ' resulting non-FLAT rate, not on the percentile alone.
        Const AnchoredNote As String =
            " (Distribution is of v53 time-anchored deltas — ""moved more than T over ≥ W minutes"" — " &
            "so it is cadence-independent but NOT comparable with a pre-v53 percentile table. " &
            "Judge any move by the resulting non-FLAT rate and Step 3b engagement, and re-read " &
            "across a calm stretch before retuning: a single trending week biases the tail.)"

        If pct95Bp < 0.5 Then
            result.Recommendation = String.Format(
                "95th percentile of |FundingDelta| is below 0.5 bp ({0:F4} bp) — funding barely drifts " &
                "over the anchored window across this whole book. Lowering the live momentum_threshold " &
                "({1:F4} bp) to chase a firing rate here would mostly buy noise; leave it and re-read " &
                "on a book with more funding movement.", pct95Bp, currentThresholdBp) & AnchoredNote
        ElseIf impliedBp < 0.5 Then
            result.Recommendation = String.Format(
                "Implied 30%-firing threshold is below 0.5 bp ({0:F4} bp) vs the live " &
                "momentum_threshold {1:F4} bp. That is a sub-basis-point signal; validate against the " &
                "RISING/FALLING false-positive rate before lowering further.",
                impliedBp, currentThresholdBp) & AnchoredNote
        Else
            result.Recommendation = String.Format(
                "Implied threshold for ~30% non-FLAT rate: {0:F4} bp. " &
                "Live momentum_threshold: {1:F4} bp. " &
                "If the non-FLAT rate is below 5%, consider moving the live threshold toward the implied value.",
                impliedBp, currentThresholdBp) & AnchoredNote
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
