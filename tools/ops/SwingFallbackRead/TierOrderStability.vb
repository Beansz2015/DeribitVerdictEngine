Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/TierOrderStability.vb
'
' --mode stability: the tier-order stability check, Part A of
' docs/tier-order-stability-check-brief-2026-09-15.md. It reuses the swing read's population,
' walks and Compute() unchanged (SwingFallbackRead.vb) and only re-cuts the per-signal outcomes.
' The default mode (no --mode) is untouched and still produces the swing read's report.
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode stability \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
'
' Pre-registered design: brief section 2. Interpretations fixed before the first run:
' docs/tier-order-stability-read-2026-09-15.md section M.
'   * Gate: reproduce the swing read's committed output (sections 6 and 8) within 0.1 bps, n exact,
'     before any split. Exit code 3 on failure.
'   * Splits: H1/H2 = first floor(D/2) UTC trading days / the rest; R1/R2 = before / from 2026-08-20.
'   * Statistic: net EV per trade per tier cell; difference per comparison; 95 % percentile CI from a
'     bootstrap of whole UTC trading days, 10,000 resamples, fixed seed.
'   * Readable cell: n >= 100. Labels: NOT READABLE -> UNSTABLE -> STABLE -> INCONCLUSIVE.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Partial Module SwingFallbackReadProgram

    Private Const StabSeed As Integer = 20260915
    Private Const StabResamples As Integer = 10000
    Private Const StabFloor As Integer = 100
    Private ReadOnly StabTiers As String() = {"STRONG", "MEDIUM", "WEAK"}
    Private ReadOnly StabCaps As String() = {"none", "swing"}
    Private ReadOnly StabSubs As String() = {"FULL", "H1", "H2", "R1", "R2"}
    Private ReadOnly StabSplitSubs As String() = {"H1", "H2", "R1", "R2"}
    Private ReadOnly StabModes As String() = {"main", "C24"}
    ' (label, tier index A, tier index B): the difference is EV(A) - EV(B). Tier index into StabTiers.
    Private ReadOnly StabComps As Tuple(Of String, Integer, Integer)() = {
        Tuple.Create("C1 WEAK vs MEDIUM", 2, 1),
        Tuple.Create("C2 STRONG vs MEDIUM", 0, 1),
        Tuple.Create("C3 WEAK vs STRONG", 2, 0)}

    Private Class BootGroup
        Public Days As Integer
        Public N As Integer() = New Integer(2) {}
        Public St As CellStats() = New CellStats(2) {}
        Public TierLo As Double() = New Double(2) {}
        Public TierHi As Double() = New Double(2) {}
        Public TierSkipped As Integer() = New Integer(2) {}
        Public DiffLo As Double() = New Double(2) {}
        Public DiffHi As Double() = New Double(2) {}
        Public DiffSd As Double() = New Double(2) {}
        Public DiffSkipped As Integer() = New Integer(2) {}
    End Class

    Private Function ModeKey(s As Sig, mode As String) As String
        Return If(mode = "main", "W" & s.Windows.Max().ToString(Inv), mode)
    End Function

    Private Function ModeName(mode As String) As String
        Return If(mode = "main", "main window", "carried 24 h")
    End Function

    Private Function InSub(s As Sig, subName As String, splitDate As DateTime) As Boolean
        Select Case subName
            Case "FULL" : Return True
            Case "H1" : Return s.Ts < splitDate
            Case "H2" : Return s.Ts >= splitDate
            Case "R1" : Return s.Ts < AtrStepEdge
            Case "R2" : Return s.Ts >= AtrStepEdge
            Case Else : Throw New ArgumentException("unknown sub-sample " & subName)
        End Select
    End Function

    Private Function Sgn(x As Double) As String
        Return If(x > 0, "+", If(x < 0, "-", "0"))
    End Function

    Private Function F1(x As Double) As String
        If Double.IsNaN(x) Then Return "n/a"
        Return x.ToString("+0.0;-0.0;0.0", Inv)
    End Function

    Private Function Ci1(pt As Double, lo As Double, hi As Double) As String
        Return F1(pt) & " [" & F1(lo) & ", " & F1(hi) & "]"
    End Function

    ' ------------------------------------------------------------------ bootstrap of whole trading days

    Private Function BootstrapGroup(rows As List(Of Sig), mode As String, fees As FeeCase, seed As Integer) As BootGroup
        Dim g As New BootGroup()
        For t As Integer = 0 To 2
            Dim tierName As String = StabTiers(t)
            Dim tr = rows.Where(Function(x) x.Tier = tierName).ToList()
            g.N(t) = tr.Count
            g.St(t) = If(tr.Count > 0, Compute(tr, ModeKey(tr(0), mode), fees), New CellStats())
        Next

        Dim dayIdx As New Dictionary(Of DateTime, Integer)()
        Dim sums As New List(Of Double())()
        Dim cnts As New List(Of Integer())()
        For Each s In rows
            Dim t As Integer = Array.IndexOf(StabTiers, s.Tier)
            If t < 0 Then Throw New InvalidOperationException("unknown tier " & s.Tier)
            Dim ev As Double = Result(s, s.Res(ModeKey(s, mode)), fees.MakerRt, fees.MakerRt)
            Dim day As DateTime = s.Ts.Date
            Dim i As Integer
            If Not dayIdx.TryGetValue(day, i) Then
                i = sums.Count
                dayIdx(day) = i
                sums.Add(New Double(2) {})
                cnts.Add(New Integer(2) {})
            End If
            sums(i)(t) += ev
            cnts(i)(t) += 1
        Next
        Dim nDays As Integer = sums.Count
        g.Days = nDays
        For t As Integer = 0 To 2
            g.TierLo(t) = Double.NaN : g.TierHi(t) = Double.NaN
            g.DiffLo(t) = Double.NaN : g.DiffHi(t) = Double.NaN : g.DiffSd(t) = Double.NaN
        Next
        If nDays = 0 Then Return g

        Dim rng As New Random(seed)
        Dim tierDraws As New List(Of List(Of Double)) From {New List(Of Double)(), New List(Of Double)(), New List(Of Double)()}
        Dim diffDraws As New List(Of List(Of Double)) From {New List(Of Double)(), New List(Of Double)(), New List(Of Double)()}
        Dim bs As Double() = New Double(2) {}
        Dim bc As Integer() = New Integer(2) {}
        Dim m As Double() = New Double(2) {}
        For b As Integer = 1 To StabResamples
            Array.Clear(bs)
            Array.Clear(bc)
            For j As Integer = 1 To nDays
                Dim i As Integer = rng.Next(nDays)
                For t As Integer = 0 To 2
                    bs(t) += sums(i)(t)
                    bc(t) += cnts(i)(t)
                Next
            Next
            For t As Integer = 0 To 2
                If bc(t) > 0 Then
                    m(t) = bs(t) / bc(t)
                    tierDraws(t).Add(m(t))
                Else
                    m(t) = Double.NaN
                    g.TierSkipped(t) += 1
                End If
            Next
            For c As Integer = 0 To 2
                Dim ia As Integer = StabComps(c).Item2, ib As Integer = StabComps(c).Item3
                If bc(ia) > 0 AndAlso bc(ib) > 0 Then
                    diffDraws(c).Add(m(ia) - m(ib))
                Else
                    g.DiffSkipped(c) += 1
                End If
            Next
        Next
        For t As Integer = 0 To 2
            If tierDraws(t).Count > 0 Then
                Dim srt = tierDraws(t).OrderBy(Function(x) x).ToList()
                g.TierLo(t) = Pctl(srt, 2.5)
                g.TierHi(t) = Pctl(srt, 97.5)
            End If
        Next
        For c As Integer = 0 To 2
            If diffDraws(c).Count > 1 Then
                Dim srt = diffDraws(c).OrderBy(Function(x) x).ToList()
                g.DiffLo(c) = Pctl(srt, 2.5)
                g.DiffHi(c) = Pctl(srt, 97.5)
                Dim mu As Double = srt.Average()
                g.DiffSd(c) = Math.Sqrt(srt.Sum(Function(x) (x - mu) * (x - mu)) / (srt.Count - 1))
            End If
        Next
        Return g
    End Function

    Private Function Diff(g As BootGroup, c As Integer) As Double
        Return g.St(StabComps(c).Item2).NetEv - g.St(StabComps(c).Item3).NetEv
    End Function

    Private Function CompReadable(g As BootGroup, c As Integer) As Boolean
        Return g.N(StabComps(c).Item2) >= StabFloor AndAlso g.N(StabComps(c).Item3) >= StabFloor
    End Function

    ' Label order fixed before the first run (read section M): NOT READABLE -> UNSTABLE -> STABLE -> INCONCLUSIVE.
    Private Function StabLabel(groups As Dictionary(Of String, BootGroup), mode As String, sess As String, cap As String, c As Integer, ByRef signs As String) As String
        Dim full = groups(mode & "|" & sess & "|" & cap & "|FULL")
        Dim parts As New List(Of String)()
        Dim readDiffs As New List(Of Double)()
        Dim readNames As New List(Of String)()
        For Each subName In StabSplitSubs
            Dim g = groups(mode & "|" & sess & "|" & cap & "|" & subName)
            Dim r As Boolean = CompReadable(g, c)
            Dim d As Double = Diff(g, c)
            parts.Add(subName & " " & If(r, Sgn(d), "n/r"))
            If r Then readDiffs.Add(d) : readNames.Add(subName)
        Next
        signs = String.Join("; ", parts)
        If Not CompReadable(full, c) Then Return "NOT READABLE"
        Dim allPos As Boolean = readDiffs.All(Function(x) x > 0)
        Dim allNeg As Boolean = readDiffs.All(Function(x) x < 0)
        If readDiffs.Count >= 2 AndAlso Not (allPos OrElse allNeg) Then Return "UNSTABLE"
        If readDiffs.Count >= 3 AndAlso readNames.Contains("H1") AndAlso readNames.Contains("H2") Then Return "STABLE"
        Return "INCONCLUSIVE"
    End Function

    ' ------------------------------------------------------------------ reference parsing (reproduction gate)

    Private Function SplitMdRow(line As String) As String()
        Return line.Trim().Trim("|"c).Split("|"c).Select(Function(x) x.Trim()).ToArray()
    End Function

    Private Function ParseRefSection(path As String, sectionPrefix As String) As Dictionary(Of String, Tuple(Of Integer, Double))
        Dim res As New Dictionary(Of String, Tuple(Of Integer, Double))()
        Dim lines = File.ReadAllLines(path)
        Dim start As Integer = Array.FindIndex(lines, Function(l) l.StartsWith(sectionPrefix))
        If start < 0 Then Throw New InvalidOperationException("reference section " & sectionPrefix & " not found in " & path)
        Dim h As Integer = -1
        For j As Integer = start + 1 To lines.Length - 1
            If lines(j).StartsWith("## ") Then Exit For
            If lines(j).StartsWith("| Session") Then h = j : Exit For
        Next
        If h < 0 Then Throw New InvalidOperationException("reference table header not found under " & sectionPrefix)
        Dim cols = SplitMdRow(lines(h))
        Dim cS As Integer = Array.IndexOf(cols, "Session")
        Dim cC As Integer = Array.IndexOf(cols, "Target set by")
        Dim cT As Integer = Array.IndexOf(cols, "Tier")
        Dim cN As Integer = Array.IndexOf(cols, "n")
        Dim cE As Integer = Array.IndexOf(cols, "Net EV bps [CI]")
        If {cS, cC, cT, cN, cE}.Any(Function(x) x < 0) Then Throw New InvalidOperationException("reference header columns missing under " & sectionPrefix)
        For j As Integer = h + 2 To lines.Length - 1
            If Not lines(j).StartsWith("|") Then Exit For
            Dim p = SplitMdRow(lines(j))
            Dim evText As String = p(cE).Split(" "c)(0)
            res(p(cS) & "|" & p(cC) & "|" & p(cT)) = Tuple.Create(Integer.Parse(p(cN), Inv), Double.Parse(evText, NumberStyles.Float, Inv))
        Next
        Return res
    End Function

    ' ------------------------------------------------------------------ report

    Private Function RunStability(header As StringBuilder, sigs As List(Of Sig), fees As FeeCase, refPath As String, outPath As String) As Integer
        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode stability (tier-order stability check, Part A)"))
        o.AppendLine("- Brief: docs/tier-order-stability-check-brief-2026-09-15.md, Part A. Interpretations: docs/tier-order-stability-read-2026-09-15.md section M.")
        o.AppendLine("- Reproduction reference: " & refPath)
        o.AppendLine(String.Format(Inv, "- Population: {0} signals. Fees: maker/maker {1:0.00} bps round trip. No slippage case.", sigs.Count, fees.MakerRt))
        o.AppendLine(String.Format(Inv, "- Bootstrap: {0} resamples of whole UTC trading days per session x target type x sub-sample x outcome mode; seed {1} + group index; percentile 95 % CI. Readable cell: n >= {2}.", StabResamples, StabSeed, StabFloor))
        o.AppendLine()

        ' ---- 1. reproduction gate
        o.AppendLine("## 1. Reproduction check against the swing read's committed output")
        o.AppendLine()
        o.AppendLine("Reference: section 6 (main window) and section 8 (carried 24 h) of the swing read output, printed to 0.1 bps. Pass: n equal and |this run - reference| <= 0.1 bps on every row.")
        o.AppendLine()
        o.AppendLine("| Mode | Session | Target set by | Tier | n (reference) | n (this run) | Net EV bps (reference) | Net EV bps (this run) | Delta bps |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|")
        Dim fails As Integer = 0, compared As Integer = 0, refTotal As Integer = 0
        Dim maxAbs As Double = 0
        For Each mode In StabModes
            Dim refRows = ParseRefSection(refPath, If(mode = "main", "## 6.", "## 8."))
            refTotal += refRows.Count
            For Each sess In SessionOrder
                For Each cap In CapOrder
                    For Each tier In TierOrder
                        Dim key As String = sess & "|" & cap & "|" & tier
                        Dim rows = Filter(sigs, sess, cap, tier)
                        Dim hasRef As Boolean = refRows.ContainsKey(key)
                        If rows.Count = 0 AndAlso Not hasRef Then Continue For
                        compared += 1
                        Dim st = If(rows.Count > 0, Compute(rows, ModeKey(rows(0), mode), fees), New CellStats())
                        Dim refN As Integer = If(hasRef, refRows(key).Item1, -1)
                        Dim refEv As Double = If(hasRef, refRows(key).Item2, Double.NaN)
                        Dim delta As Double = st.NetEv - refEv
                        Dim ok As Boolean = hasRef AndAlso refN = st.N AndAlso Math.Abs(delta) <= 0.1
                        If Not ok Then fails += 1
                        If hasRef AndAlso Math.Abs(delta) > maxAbs Then maxAbs = Math.Abs(delta)
                        o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6:+0.0;-0.0;0.0} | {7:+0.00;-0.00;0.00} | {8:+0.00;-0.00;0.00}{9} |",
                            ModeName(mode), sess, cap, tier, If(hasRef, refN.ToString(Inv), "missing"), st.N, refEv, st.NetEv, delta, If(ok, "", " FAIL")))
                    Next
                Next
            Next
        Next
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Rows compared: {0} (reference rows: {1}). Failing rows: {2}. Largest |delta|: {3:0.000} bps.", compared, refTotal, fails, maxAbs))
        If fails > 0 OrElse compared <> refTotal Then
            o.AppendLine("- **REPRODUCTION FAILED. STOP before any split** (brief section 0).")
            File.WriteAllText(outPath, o.ToString(), New UTF8Encoding(False))
            Console.Write(o.ToString())
            Console.WriteLine("Wrote " & outPath)
            Return 3
        End If
        o.AppendLine("- **REPRODUCTION PASSED.**")
        o.AppendLine()

        ' ---- 2. splits
        Dim days = sigs.Select(Function(x) x.Ts.Date).Distinct().OrderBy(Function(x) x).ToList()
        Dim splitIdx As Integer = days.Count \ 2
        Dim splitDate As DateTime = days(splitIdx)
        Dim r2Days As Integer = days.Where(Function(x) x >= AtrStepEdge).Count()
        o.AppendLine("## 2. Splits")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Trading days (distinct UTC dates in the population): {0}, from {1:yyyy-MM-dd} to {2:yyyy-MM-dd}.", days.Count, days(0), days(days.Count - 1)))
        o.AppendLine(String.Format(Inv, "- **Split date: {0:yyyy-MM-dd} 00:00 UTC.** H1 = {1} days ({2:yyyy-MM-dd} to {3:yyyy-MM-dd}); H2 = {4} days ({0:yyyy-MM-dd} to {5:yyyy-MM-dd}).",
            splitDate, splitIdx, days(0), days(splitIdx - 1), days.Count - splitIdx, days(days.Count - 1)))
        o.AppendLine(String.Format(Inv, "- Regime edge: {0:yyyy-MM-dd HH:mm} UTC. R1 = {1} trading days; R2 = {2} trading days.", AtrStepEdge, days.Count - r2Days, r2Days))
        o.AppendLine()
        o.AppendLine("| Session | Target set by | Sub-sample | STRONG n | MEDIUM n | WEAK n | Days in pool |")
        o.AppendLine("|---|---|---|---|---|---|---|")

        ' ---- compute every group once
        Dim groups As New Dictionary(Of String, BootGroup)()
        Dim gi As Integer = 0
        For Each mode In StabModes
            For Each sess In SessionOrder
                For Each cap In {"none", "swing", "hvn"}
                    For Each subName In StabSubs
                        If cap = "hvn" AndAlso subName <> "FULL" Then Continue For
                        Dim sessL As String = sess, capL As String = cap, subL As String = subName
                        Dim rows = sigs.Where(Function(x) x.Session = sessL AndAlso x.Cap = capL AndAlso InSub(x, subL, splitDate)).ToList()
                        groups(mode & "|" & sess & "|" & cap & "|" & subName) = BootstrapGroup(rows, mode, fees, StabSeed + gi)
                        gi += 1
                    Next
                Next
            Next
        Next
        For Each sess In SessionOrder
            For Each cap In StabCaps
                For Each subName In StabSubs
                    Dim g = groups("main|" & sess & "|" & cap & "|" & subName)
                    o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} |", sess, cap, subName, g.N(0), g.N(1), g.N(2), g.Days))
                Next
            Next
        Next
        o.AppendLine()
        Dim skipped As Integer = groups.Values.Sum(Function(g) g.DiffSkipped.Sum() + g.TierSkipped.Sum())
        o.AppendLine(String.Format(Inv, "- Bootstrap groups: {0}. Resample-statistics skipped because a tier drew zero signals (all groups, all tiers and comparisons): {1}.", groups.Count, skipped))
        Dim skippedReadable As Integer = 0
        For Each kv In groups
            For c As Integer = 0 To 2
                If CompReadable(kv.Value, c) Then skippedReadable += kv.Value.DiffSkipped(c)
            Next
        Next
        o.AppendLine("- Of those, skips inside READABLE comparisons: " & skippedReadable.ToString(Inv) & ".")
        o.AppendLine()

        ' ---- 3. verdict
        o.AppendLine("## 3. Verdict: C1-C3 x session x target type")
        o.AppendLine()
        o.AppendLine("Label from the main window (primary). Carried 24 h label is secondary. Signs: H1; H2; R1; R2 (n/r = not readable). Difference = EV(first tier) - EV(second tier), net EV per trade in bps.")
        o.AppendLine()
        o.AppendLine("| Session | Target set by | Comparison | Label, main window | Signs, main window | Full-sample difference [CI], main window | Full CI excludes 0 | Label, carried 24 h | Signs, carried 24 h | Full-sample difference [CI], carried 24 h |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|")
        Dim stableList As New List(Of Tuple(Of String, String, String, Integer))()
        For Each sess In SessionOrder
            For Each cap In StabCaps
                For c As Integer = 0 To 2
                    Dim sM As String = "", sC As String = ""
                    Dim lM = StabLabel(groups, "main", sess, cap, c, sM)
                    Dim lC = StabLabel(groups, "C24", sess, cap, c, sC)
                    Dim gM = groups("main|" & sess & "|" & cap & "|FULL")
                    Dim gC = groups("C24|" & sess & "|" & cap & "|FULL")
                    Dim dM As Double = Diff(gM, c), dC As Double = Diff(gC, c)
                    Dim excl As String = If(CompReadable(gM, c), If(gM.DiffLo(c) > 0 OrElse gM.DiffHi(c) < 0, "yes", "no"), "n/r")
                    o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | **{3}** | {4} | {5} | {6} | {7} | {8} | {9} |",
                        sess, cap, StabComps(c).Item1, lM, sM, Ci1(dM, gM.DiffLo(c), gM.DiffHi(c)), excl, lC, sC, Ci1(dC, gC.DiffLo(c), gC.DiffHi(c))))
                    If lM = "STABLE" Then stableList.Add(Tuple.Create("main", sess, cap, c))
                    If lC = "STABLE" Then stableList.Add(Tuple.Create("C24", sess, cap, c))
                Next
            Next
        Next
        o.AppendLine()

        ' ---- 4. per-sub-sample cells
        For Each mode In StabModes
            o.AppendLine("## " & If(mode = "main", "4", "5") & ". Per-sub-sample tier cells, " & ModeName(mode))
            o.AppendLine()
            o.AppendLine("| Session | Target set by | Sub-sample | Tier | n | Days | Readable | Success rate % | Gross BE % | Gross edge pp | Net EV bps [CI] |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
            For Each sess In SessionOrder
                For Each cap In StabCaps
                    For Each subName In StabSubs
                        Dim g = groups(mode & "|" & sess & "|" & cap & "|" & subName)
                        For t As Integer = 0 To 2
                            Dim st = g.St(t)
                            If g.N(t) = 0 Then
                                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | 0 | {4} | no | n/a | n/a | n/a | n/a |", sess, cap, subName, StabTiers(t), g.Days))
                                Continue For
                            End If
                            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7:0.0} | {8:0.0} | {9:+0.0;-0.0;0.0} | {10} |",
                                sess, cap, subName, StabTiers(t), g.N(t), g.Days, If(g.N(t) >= StabFloor, "yes", "no"),
                                st.SuccessPct, st.GrossBe, st.SuccessPct - st.GrossBe, Ci1(st.NetEv, g.TierLo(t), g.TierHi(t))))
                        Next
                    Next
                Next
            Next
            o.AppendLine()
        Next

        ' ---- 6/7. per-sub-sample differences
        For Each mode In StabModes
            o.AppendLine("## " & If(mode = "main", "6", "7") & ". Per-sub-sample differences, " & ModeName(mode))
            o.AppendLine()
            o.AppendLine("| Session | Target set by | Comparison | Sub-sample | n first / n second | Readable | Difference bps [CI] | Difference bps, 2 dp | CI excludes 0 | Bootstrap SE |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|---|")
            For Each sess In SessionOrder
                For Each cap In StabCaps
                    For c As Integer = 0 To 2
                        For Each subName In StabSubs
                            Dim g = groups(mode & "|" & sess & "|" & cap & "|" & subName)
                            Dim r As Boolean = CompReadable(g, c)
                            Dim ia As Integer = StabComps(c).Item2, ib As Integer = StabComps(c).Item3
                            Dim hasBoth As Boolean = g.N(ia) > 0 AndAlso g.N(ib) > 0
                            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} / {5} | {6} | {7} | {10} | {8} | {9} |",
                                sess, cap, StabComps(c).Item1, subName, g.N(ia), g.N(ib), If(r, "yes", "NOT READABLE"),
                                If(hasBoth, Ci1(Diff(g, c), g.DiffLo(c), g.DiffHi(c)), "n/a"),
                                If(hasBoth AndAlso Not Double.IsNaN(g.DiffLo(c)), If(g.DiffLo(c) > 0 OrElse g.DiffHi(c) < 0, "yes", "no"), "n/a"),
                                If(Double.IsNaN(g.DiffSd(c)), "n/a", g.DiffSd(c).ToString("0.00", Inv)),
                                If(hasBoth, Diff(g, c).ToString("+0.00;-0.00;0.00", Inv), "n/a")))
                        Next
                    Next
                Next
            Next
            o.AppendLine()
        Next

        ' ---- 8. HVN context
        o.AppendLine("## 8. HVN target rows, full sample, main window (context only, never a test)")
        o.AppendLine()
        o.AppendLine("| Session | Tier | n | Days | Success rate % | Gross edge pp | Net EV bps [CI] |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            Dim g = groups("main|" & sess & "|hvn|FULL")
            For t As Integer = 0 To 2
                If g.N(t) = 0 Then Continue For
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4:0.0} | {5:+0.0;-0.0;0.0} | {6} |", sess, StabTiers(t), g.N(t), g.Days,
                    g.St(t).SuccessPct, g.St(t).SuccessPct - g.St(t).GrossBe, Ci1(g.St(t).NetEv, g.TierLo(t), g.TierHi(t))))
            Next
        Next
        o.AppendLine()

        ' ---- 9. Part B candidates
        o.AppendLine("## 9. Part B candidates: STABLE comparisons and the forward sample each would need")
        o.AppendLine()
        o.AppendLine("Required signals (both cells) = (n first + n second) x (z x SE / |d|)^2, SE = full-sample bootstrap SE. Forward trading days = required signals / R2 rate (signals of both cells per R2 trading day). z 1.96 = the forward estimate just reaches significance if d is the true gap (50 % power); z 2.80 = 80 % power, two-sided 5 %.")
        o.AppendLine()
        o.AppendLine("| Mode | Session | Target set by | Comparison | d bps | SE bps | n first + n second | R2 rate per trading day | Required signals z 1.96 | Forward trading days z 1.96 | Required signals z 2.80 | Forward trading days z 2.80 |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|")
        If stableList.Count = 0 Then o.AppendLine("| (none) | | | | | | | | | | | |")
        For Each sc In stableList
            Dim mode = sc.Item1, sess = sc.Item2, cap = sc.Item3, c = sc.Item4
            Dim g = groups(mode & "|" & sess & "|" & cap & "|FULL")
            Dim g2 = groups(mode & "|" & sess & "|" & cap & "|R2")
            Dim ia As Integer = StabComps(c).Item2, ib As Integer = StabComps(c).Item3
            Dim d As Double = Diff(g, c)
            Dim se As Double = g.DiffSd(c)
            Dim nBoth As Integer = g.N(ia) + g.N(ib)
            Dim rateR2 As Double = (g2.N(ia) + g2.N(ib)) / CDbl(Math.Max(1, r2Days))
            Dim req196 As Double = nBoth * Math.Pow(1.96 * se / Math.Abs(d), 2)
            Dim req280 As Double = nBoth * Math.Pow(2.8 * se / Math.Abs(d), 2)
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4:+0.00;-0.00;0.00} | {5:0.00} | {6} | {7:0.0} | {8:0} | {9:0} | {10:0} | {11:0} |",
                ModeName(mode), sess, cap, StabComps(c).Item1, d, se, nBoth, rateR2, req196, req196 / rateR2, req280, req280 / rateR2))
        Next
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

End Module
