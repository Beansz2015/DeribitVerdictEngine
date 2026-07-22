' tools/CeilingAudit/AuditReport.vb
' Renders the W6-4 ceiling-audit markdown report — one file per run.
'
' Per §4 the three-way verdict line reads the CI on ΔAUC (test block, block-bootstrap over
' session-hour blocks) and prints one of three strings verbatim:
'   - "CEILING DECLARED"  when CI upper bound < +0.03
'   - "B1 PRIZE MEASURED" when CI lower bound > +0.03
'   - "INCONCLUSIVE"       when CI straddles ±0.03
' The margin is trader-adjustable per K4 (2026-07-23 tick: 0.03; overridable via CLI).
'
' Population tables render: AUC(baseline), AUC(challenger), ΔAUC + CI, Brier both sides,
' lift @ operating point, N train, N test. The coefficient table sorts by |coef| descending
' — the report is a diagnostic, not a wire — so the trader can scan which features carry
' weight before deciding what W6-5 should scope.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace CeilingAudit

    Public Class AuditPopulationReport
        Public Property Name As String
        Public Property IsDecisive As Boolean                ' NY×1 = true; others indicative-only
        Public Property IsAggrVelArmed As Boolean
        Public Property NRowsTotal As Integer
        Public Property NRowsLabelled As Integer
        Public Property NTrain As Integer
        Public Property NTest As Integer
        Public Property Split As ChronologicalSplit
        Public Property BaselineTest As MetricResult
        Public Property ChallengerTest As MetricResult
        Public Property OperatingPointK As Integer            ' = STRONG+MEDIUM count in TEST
        Public Property TieredCounts As New Dictionary(Of String, Integer)()   ' STRONG/MEDIUM/WEAK counts (test)
        Public Property DeltaAucCi As BootstrapCi
        Public Property BestLambda As Double
        Public Property LambdaAucs As Dictionary(Of Double, Double)
        Public Property Model As LogisticModel
        Public Property Schema As FeatureSchema
        Public Property InfoCoefs As List(Of (Name As String, Coef As Double, N As Integer))
        Public Property SkippedReason As String               ' non-empty ⇒ table renders as skipped
    End Class

    Public Class AuditReportModel
        Public Property StampUtc As DateTime
        Public Property CsvPath As String
        Public Property SettingsPath As String
        Public Property SettingsVersion As Integer
        Public Property VersionCheck As String                ' "OK", "WARN: ...", etc — D6 honest reporting
        Public Property MarginDelta As Double                 ' §4 margin (default 0.03)
        Public Property BootstrapB As Integer
        Public Property BootstrapSeed As Integer
        Public Property MinTestDays As Integer
        Public Property LoadStats As LoadStats
        Public Property SpanFrom As DateTime
        Public Property SpanTo As DateTime
        Public Property Populations As New List(Of AuditPopulationReport)()
    End Class

    Public Class AuditReport

        Public Shared Function Build(m As AuditReportModel) As String
            Dim sb As New StringBuilder()
            Dim inv As IFormatProvider = CultureInfo.InvariantCulture
            sb.AppendLine("# W6-4 Ceiling Audit Report")
            sb.AppendLine()
            sb.AppendLine("**Generated:** " & m.StampUtc.ToString("yyyy-MM-dd HH:mm:ss UTC", inv))
            sb.AppendLine("**CSV:** `" & m.CsvPath & "`")
            sb.AppendLine("**Settings source:** `" & m.SettingsPath & "` (version " & m.SettingsVersion & ") — " & m.VersionCheck)
            sb.AppendLine("**Margin (§4):** ±" & m.MarginDelta.ToString("F3", inv))
            sb.AppendLine("**Bootstrap:** B=" & m.BootstrapB & " session-hour blocks · seed " & m.BootstrapSeed)
            sb.AppendLine()

            ' -- §1 Load stats ----------------------------------------------------------
            sb.AppendLine("## 1. Load & filter")
            sb.AppendLine()
            sb.AppendLine("| Field | Count |")
            sb.AppendLine("|---|---|")
            sb.AppendLine("| CSV rows (after repeated-header skip) | " & m.LoadStats.TotalRows & " |")
            sb.AppendLine("| Repeated headers skipped | " & m.LoadStats.RepeatedHeadersSkipped & " |")
            sb.AppendLine("| Excluded — pre-v0.8 / no placed levels | " & m.LoadStats.NonV08Excluded & " |")
            sb.AppendLine("| Excluded — weekend | " & m.LoadStats.WeekendExcluded & " |")
            sb.AppendLine("| Excluded — NO TRADE / lean / non-directional | " & m.LoadStats.NonDirectionalExcluded & " |")
            sb.AppendLine("| Excluded — burst InstanceId prefix `8706ebae` | " & m.LoadStats.BurstInstancePrefixExcluded & " |")
            sb.AppendLine("| Excluded — burst-cadence rows (median gap < 45s) | " & m.LoadStats.BurstCadenceRowsExcluded & " |")
            sb.AppendLine()
            If m.LoadStats.BurstInstanceIds.Count > 0 Then
                sb.AppendLine("**Burst instance ids excluded:** " & String.Join(", ", m.LoadStats.BurstInstanceIds.Select(Function(s) "`" & s & "`")))
                sb.AppendLine()
            End If
            sb.AppendLine("**Span:** " & m.SpanFrom.ToString("yyyy-MM-dd HH:mm", inv) & " → " & m.SpanTo.ToString("yyyy-MM-dd HH:mm", inv) & " UTC")
            sb.AppendLine()

            ' -- §2 Per-population summary ---------------------------------------------
            sb.AppendLine("## 2. Per-population summary")
            sb.AppendLine()
            sb.AppendLine("| Population | Decisive | AggrVel | N (labelled) | N train | N test | Test span (days) | Test AUC (base → chal) | ΔAUC | 95% CI | Verdict (§4) |")
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
            For Each p In m.Populations
                If Not String.IsNullOrEmpty(p.SkippedReason) Then
                    sb.AppendLine(String.Format(inv, "| {0} | {1} | {2} | {3} | — | — | — | — | — | — | {4} |",
                                                p.Name, If(p.IsDecisive, "yes", "no"),
                                                If(p.IsAggrVelArmed, "scored", "informational"),
                                                p.NRowsLabelled, p.SkippedReason))
                    Continue For
                End If
                Dim spanDays As Double = (p.Split.TestEndUtc - p.Split.TestStartUtc).TotalDays
                Dim verdict As String = VerdictFor(p.DeltaAucCi, m.MarginDelta)
                sb.AppendLine(String.Format(inv,
                    "| {0} | {1} | {2} | {3} | {4} | {5} | {6:F2} | {7:F4} → {8:F4} | {9:+0.0000;-0.0000} | [{10:+0.0000;-0.0000}, {11:+0.0000;-0.0000}] | {12} |",
                    p.Name, If(p.IsDecisive, "yes", "no"),
                    If(p.IsAggrVelArmed, "scored", "informational"),
                    p.NRowsLabelled, p.NTrain, p.NTest, spanDays,
                    p.BaselineTest.Auc, p.ChallengerTest.Auc,
                    p.DeltaAucCi.DeltaMean, p.DeltaAucCi.CiLow, p.DeltaAucCi.CiHigh,
                    verdict))
            Next
            sb.AppendLine()

            ' -- §3 Per-population detail sections -------------------------------------
            For Each p In m.Populations
                sb.AppendLine("## 3. " & p.Name & (If(p.IsDecisive, "  [DECISIVE]", "  [indicative only]")))
                sb.AppendLine()
                If Not String.IsNullOrEmpty(p.SkippedReason) Then
                    sb.AppendLine("**Skipped:** " & p.SkippedReason)
                    sb.AppendLine()
                    Continue For
                End If

                sb.AppendLine("**Split:** train " & p.NTrain & " · test " & p.NTest &
                              " (test spans " & (p.Split.TestEndUtc - p.Split.TestStartUtc).TotalDays.ToString("F2", inv) & " days" &
                              (If(p.Split.TestSpansSessions, ", ≥3 hours covered", ", *insufficient session coverage*")) & ")")
                sb.AppendLine("**AggrVel scoring:** " & (If(p.IsAggrVelArmed, "ARMED (fields enter design matrix)", "un-armed (fields in informational side-column only)")))
                sb.AppendLine("**λ selected on internal walk-forward:** " & p.BestLambda.ToString("G4", inv))
                If p.LambdaAucs IsNot Nothing Then
                    Dim parts As New List(Of String)()
                    For Each kv In p.LambdaAucs.OrderBy(Function(k) k.Key)
                        parts.Add(kv.Key.ToString("G4", inv) & " → " & (If(Double.IsNaN(kv.Value), "n/a", kv.Value.ToString("F4", inv))))
                    Next
                    sb.AppendLine("**λ grid val AUCs:** " & String.Join(" · ", parts))
                End If
                sb.AppendLine()

                sb.AppendLine("| Metric | Baseline (dom-eff / max) | Challenger (L2 logistic) |")
                sb.AppendLine("|---|---|---|")
                sb.AppendLine(String.Format(inv, "| Test AUC | {0:F4} | {1:F4} |", p.BaselineTest.Auc, p.ChallengerTest.Auc))
                sb.AppendLine(String.Format(inv, "| Test Brier | {0:F4} | {1:F4} |", p.BaselineTest.Brier, p.ChallengerTest.Brier))
                sb.AppendLine(String.Format(inv, "| Success @ top-K (K={0}) | {1:P1} | {2:P1} |",
                                            p.OperatingPointK,
                                            p.BaselineTest.SuccessAtOperatingPoint,
                                            p.ChallengerTest.SuccessAtOperatingPoint))
                sb.AppendLine()
                sb.AppendLine("**Test-block tier counts (STRONG/MEDIUM/WEAK):** " &
                              GetOr(p.TieredCounts, "STRONG") & " / " &
                              GetOr(p.TieredCounts, "MEDIUM") & " / " &
                              GetOr(p.TieredCounts, "WEAK") &
                              "  (operating point K = STRONG+MEDIUM = " & p.OperatingPointK & ")")
                sb.AppendLine()

                ' §4 line rendered EXPLICITLY per spec.
                Dim ver As String = VerdictFor(p.DeltaAucCi, m.MarginDelta)
                sb.AppendLine("**§4 verdict:** ΔAUC = " & p.DeltaAucCi.DeltaMean.ToString("+0.0000;-0.0000", inv) &
                              "  95% CI [" & p.DeltaAucCi.CiLow.ToString("+0.0000;-0.0000", inv) & ", " &
                              p.DeltaAucCi.CiHigh.ToString("+0.0000;-0.0000", inv) & "]  → **" & ver & "**")
                sb.AppendLine()

                ' Coefficient table — |coef| descending.
                If p.Model IsNot Nothing AndAlso p.Schema IsNot Nothing Then
                    sb.AppendLine("### Challenger coefficients — |coef| descending")
                    sb.AppendLine()
                    sb.AppendLine("| Feature | Coef |")
                    sb.AppendLine("|---|---:|")
                    Dim rows As New List(Of (Name As String, Coef As Double))()
                    For j = 0 To p.Schema.Columns.Count - 1
                        rows.Add((p.Schema.Columns(j), p.Model.Weights(j)))
                    Next
                    For Each r In rows.OrderByDescending(Function(x) System.Math.Abs(x.Coef)).Take(30)
                        sb.AppendLine("| " & r.Name & " | " & r.Coef.ToString("+0.0000;-0.0000", inv) & " |")
                    Next
                    sb.AppendLine("| _(intercept)_ | " & p.Model.Bias.ToString("+0.0000;-0.0000", inv) & " |")
                    sb.AppendLine()
                End If

                ' Informational extras — reported, never scored.
                If p.InfoCoefs IsNot Nothing AndAlso p.InfoCoefs.Count > 0 Then
                    sb.AppendLine("### Informational side-column (Absorption, un-armed AggrVel) — **not in §4 decision**")
                    sb.AppendLine()
                    sb.AppendLine("| Feature | Univariate AUC on test | N with feature |")
                    sb.AppendLine("|---|---:|---:|")
                    For Each r In p.InfoCoefs
                        sb.AppendLine("| " & r.Name & " | " & (If(Double.IsNaN(r.Coef), "—", r.Coef.ToString("F4", inv))) & " | " & r.N & " |")
                    Next
                    sb.AppendLine()
                End If
            Next

            ' -- §4 Overall verdict paragraph -----------------------------------------
            sb.AppendLine("## 4. Overall")
            sb.AppendLine()
            Dim ny = m.Populations.FirstOrDefault(Function(p) p.IsDecisive)
            If ny Is Nothing OrElse Not String.IsNullOrEmpty(ny.SkippedReason) Then
                sb.AppendLine("Decisive population (NY×1) was not evaluable this run — see the table above.")
            Else
                Dim v As String = VerdictFor(ny.DeltaAucCi, m.MarginDelta)
                sb.AppendLine("**Decisive verdict (NY×1):** " & v)
                sb.AppendLine()
                Select Case v
                    Case "CEILING DECLARED"
                        sb.AppendLine("The L2-logistic challenger did not beat the pipeline's own effective score by more than the ±" &
                                      m.MarginDelta.ToString("F3", inv) & " margin. Per §4, combination spend stops; W6-5/B1 + D3-D6 close as 'no measured headroom'; W6-7 Tier-C stays refused.")
                    Case "B1 PRIZE MEASURED"
                        sb.AppendLine("The L2-logistic challenger beats the pipeline by more than the ±" &
                                      m.MarginDelta.ToString("F3", inv) & " margin. Per §4, W6-5 (B1) may spec, scoped to the top-|coef| features listed above.")
                    Case Else
                        sb.AppendLine("The CI straddles the ±" & m.MarginDelta.ToString("F3", inv) &
                                      " margin. Per §4, inconclusive; re-run at the next book doubling. No spend meanwhile.")
                End Select
            End If
            sb.AppendLine()

            Return sb.ToString()
        End Function

        Private Shared Function VerdictFor(ci As BootstrapCi, margin As Double) As String
            If ci Is Nothing Then Return "n/a"
            If ci.CiHigh < margin Then Return "CEILING DECLARED"
            If ci.CiLow > margin Then Return "B1 PRIZE MEASURED"
            Return "INCONCLUSIVE"
        End Function

        Private Shared Function GetOr(d As Dictionary(Of String, Integer), key As String) As String
            Dim v As Integer
            If d Is Nothing OrElse Not d.TryGetValue(key, v) Then Return "0"
            Return v.ToString()
        End Function

    End Class

End Namespace
