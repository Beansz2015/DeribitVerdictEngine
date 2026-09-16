Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/TierDemotionCensus.vb
'
' --mode census: the tier-demotion census of the MEDIUM-tier bug hunt, session 1
' (docs/medium-tier-diagnosis-brief-2026-09-16.md section 2.4; read: docs/medium-tier-bug-hunt-2026-09-16.md).
' Question: does the TRANSITIONAL ADX penalty (Step 4) demote signals that then beat the native signals
' of their new tier? If so, the penalty or the tier floor is miscalibrated (a D-table, not a fix).
'
' Definitions (from the logged row; no re-score):
'   * Side: the logged verdict's side. Thresholds: Ceiling(MaxScore x verdict pct), the logged MaxScore,
'     the tracked pcts (0.70 / 0.53 / 0.35; identical in every settings era v51-v68, checked in the read).
'   * Raw tier: the side's LongScore or ShortScore (through Step 3b) against the thresholds.
'   * Effective tier: the side's EffectiveLongScore or EffectiveShortScore against the thresholds; it must
'     equal the logged verdict's tier (self-check, counted).
'   * Classes: native STRONG, demoted-to-MEDIUM (raw STRONG, effective MEDIUM), native MEDIUM,
'     demoted-to-WEAK (raw MEDIUM, effective WEAK), native WEAK, demoted two tiers (raw STRONG, effective WEAK).
'   * Tier-floor inertness: Step 4 sets effective = Max(raw - penalty, TierFloor(raw)). The census counts
'     rows where effective <> raw - RegimePenalty on the verdict side, and TRANSITIONAL rows (either side)
'     where the TierFloor arm wins with a floor above zero.
'
' Pre-registered read, fixed before the first run:
'   * Population: the swing read's population (directional, placed levels valid, trading week, v51 edge,
'     collector filter). Counts also cover every trading-week directional row. Sessions separate.
'   * Outcome: net EV per trade, maker/maker, main window primary (NY 15 min, LONDON and ASIA 45 min),
'     carried 24 h secondary. FULL, H1, H2 with the stability read's split (first floor(D/2) UTC trading days).
'   * CI: 95 % percentile bootstrap of whole UTC trading days, 10,000 resamples, fixed seed.
'   * Readable: n >= 100 in both cells of a comparison.
'   * Comparisons, d = EV(first) - EV(second):
'       K1 demoted-to-WEAK vs native WEAK (every regime)
'       K2 demoted-to-MEDIUM vs native MEDIUM (every regime)
'       K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL rows only
'       K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL rows only
'   * Label, first match wins: NOT READABLE (FULL n < 100 in a cell) -> CONFIRMED (both halves readable,
'     both CIs exclude 0, same sign) -> H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT -> H1 FINDING, H2 CONTRADICTS
'     OR NOT READABLE -> DISCOVERY ONLY (H2) -> NO DIFFERENCE SHOWN.
'   * Reading: d > 0 CONFIRMED on K1-K4 = demoted rows beat native rows of their new tier = a scoring
'     finding for a D-table. d < 0 CONFIRMED = the demotion removes weaker signals (the penalty earns its keep).
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode census \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Partial Module SwingFallbackReadProgram

    Private Const CnSeed As Integer = 20260916
    Private ReadOnly CnClasses As String() = {"native STRONG", "demoted-to-MEDIUM", "native MEDIUM", "demoted-to-WEAK", "native WEAK", "demoted two tiers"}
    Private ReadOnly CnRegimes As String() = {"TRENDING_UP", "TRENDING_DOWN", "RANGE_BOUND", "TRANSITIONAL"}
    ' (label, class A, class B, TRANSITIONAL only)
    Private ReadOnly CnComps As Tuple(Of String, Integer, Integer, Boolean)() = {
        Tuple.Create("K1 demoted-to-WEAK vs native WEAK", 3, 4, False),
        Tuple.Create("K2 demoted-to-MEDIUM vs native MEDIUM", 1, 2, False),
        Tuple.Create("K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only", 3, 4, True),
        Tuple.Create("K4 demoted-to-MEDIUM vs native MEDIUM, TRANSITIONAL only", 1, 2, True)}

    Private Class CnRow
        Public Ts As DateTime
        Public Session As String = ""
        Public Regime As String = ""
        Public Side As String = ""
        Public MaxScore As Integer
        Public Raw As Integer
        Public Eff As Integer
        Public Penalty As Integer
        Public RawTier As String = ""
        Public EffTier As String = ""
        Public LoggedTier As String = ""
        Public Cls As Integer = -1
        Public Sig As Sig
    End Class

    Private Function CnTier(score As Integer, maxScore As Integer, cfg As EngineSettings) As String
        If score >= CInt(Math.Ceiling(maxScore * cfg.Scoring.VerdictStrongPct)) Then Return "STRONG"
        If score >= CInt(Math.Ceiling(maxScore * cfg.Scoring.VerdictMedPct)) Then Return "MEDIUM"
        If score >= CInt(Math.Ceiling(maxScore * cfg.Scoring.VerdictWeakPct)) Then Return "WEAK"
        Return "BELOW WEAK"
    End Function

    Private Function CnClassOf(rawTier As String, effTier As String) As Integer
        If rawTier = effTier Then
            Select Case effTier
                Case "STRONG" : Return 0
                Case "MEDIUM" : Return 2
                Case "WEAK" : Return 4
            End Select
            Return -1
        End If
        If rawTier = "STRONG" AndAlso effTier = "MEDIUM" Then Return 1
        If rawTier = "MEDIUM" AndAlso effTier = "WEAK" Then Return 3
        If rawTier = "STRONG" AndAlso effTier = "WEAK" Then Return 5
        Return -1
    End Function

    Private Function CnFloor(raw As Integer, cfg As EngineSettings) As Integer
        Dim tf = cfg.Scoring.TierFloor
        If raw >= tf.HighThreshold Then Return tf.HighFloor
        If raw >= tf.MedThreshold Then Return tf.MedFloor
        If raw >= tf.LowThreshold Then Return tf.LowFloor
        Return 0
    End Function

    Private Function RunCensus(header As StringBuilder, cfg As EngineSettings, sigs As List(Of Sig), fees As FeeCase,
                               merged As Dictionary(Of DateTime, Tuple(Of CsvRow, String)), pooledPath As String, livePath As String,
                               collectorStart As DateTime, collectorIds As HashSet(Of String),
                               weekOpenHour As Integer, weekCloseHourExcl As Integer, outPath As String) As Integer
        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode census (tier-demotion census)"))
        o.AppendLine("- Brief: docs/medium-tier-diagnosis-brief-2026-09-16.md section 2.4. Read: docs/medium-tier-bug-hunt-2026-09-16.md. Rules fixed before the first run: this instrument's header.")
        o.AppendLine(String.Format(Inv, "- Thresholds: Ceiling(MaxScore x {0} / {1} / {2}). Tier floor: raw >= {3} -> {4}, >= {5} -> {6}, >= {7} -> {8}. TRANSITIONAL penalty {9} below ADX {10}, {11} below ADX {12}.",
            cfg.Scoring.VerdictStrongPct, cfg.Scoring.VerdictMedPct, cfg.Scoring.VerdictWeakPct,
            cfg.Scoring.TierFloor.HighThreshold, cfg.Scoring.TierFloor.HighFloor, cfg.Scoring.TierFloor.MedThreshold, cfg.Scoring.TierFloor.MedFloor,
            cfg.Scoring.TierFloor.LowThreshold, cfg.Scoring.TierFloor.LowFloor, cfg.RegimeGates.TransitionalPenaltyLow, cfg.RegimeGates.TransitionalAdxPenaltyMid,
            cfg.RegimeGates.TransitionalPenaltyMid, cfg.RegimeGates.TransitionalAdxPenaltyHigh))
        o.AppendLine(String.Format(Inv, "- Bootstrap: {0} resamples of whole UTC trading days, seed {1} + group index, percentile 95 % CI. Readable: n >= {2} in both cells.", StabResamples, CnSeed, StabFloor))
        o.AppendLine()

        Dim raw As New Dictionary(Of DateTime, RsRaw)()
        RsLoadRaw(pooledPath, raw)
        RsLoadRaw(livePath, raw)
        Dim popBySigTs = sigs.ToDictionary(Function(s) s.Ts)

        ' ---- every trading-week row after the v51 edge (the swing read's funnel before the levels check)
        Dim dirRows As New List(Of CnRow)()
        Dim nTrans As Integer = 0, nFloorWins As Integer = 0, nZeroClamp As Integer = 0, nOtherDiff As Integer = 0
        Dim nToNoTrade As Integer = 0, nToNoTradePop As Integer = 0
        Dim toNoTradeBySess As New Dictionary(Of String, Integer)()
        For Each kv In merged.OrderBy(Function(k) k.Key)
            Dim c = kv.Value.Item1
            If c.Timestamp < V51Edge Then Continue For
            If c.Timestamp >= collectorStart AndAlso Not collectorIds.Contains(kv.Value.Item2) Then Continue For
            If Not InTradingWeek(c.Timestamp, weekOpenHour, weekCloseHourExcl) Then Continue For
            Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, c.Timestamp.Hour)
            If bucket Is Nothing Then Continue For
            Dim x As RsRaw = Nothing
            If Not raw.TryGetValue(c.Timestamp, x) Then Continue For
            Dim sess As String = bucket.Name.ToUpperInvariant()
            Dim regime As String = RsS(x, "Regime")
            Dim maxS As Integer = RsI(x, "MaxScore")
            Dim ls As Integer = RsI(x, "LongScore"), ss As Integer = RsI(x, "ShortScore")
            Dim els As Integer = RsI(x, "EffectiveLongScore"), ess As Integer = RsI(x, "EffectiveShortScore")
            Dim pen As Integer = RsI(x, "RegimePenalty")
            If regime = "TRANSITIONAL" Then
                nTrans += 1
                For Each pr In {Tuple.Create(ls, els), Tuple.Create(ss, ess)}
                    Dim fl As Integer = CnFloor(pr.Item1, cfg)
                    If pr.Item2 <> pr.Item1 - pen Then
                        If fl > 0 AndAlso pr.Item2 = fl AndAlso fl > pr.Item1 - pen Then
                            nFloorWins += 1
                        ElseIf fl = 0 AndAlso pr.Item2 = 0 AndAlso pr.Item1 < pen Then
                            nZeroClamp += 1
                        Else
                            nOtherDiff += 1
                        End If
                    End If
                Next
            End If
            Dim verdict As String = RsS(x, "Verdict").ToUpperInvariant()
            Dim isDir As Boolean = verdict = "STRONG LONG" OrElse verdict = "LONG" OrElse verdict = "WEAK LONG" OrElse verdict = "STRONG SHORT" OrElse verdict = "SHORT" OrElse verdict = "WEAK SHORT"
            If Not isDir Then
                ' Demoted out of WEAK into NO TRADE by the penalty alone: the raw dominant side clears WEAK, the
                ' effective one does not, and neither the MTF veto nor the min-move gate fired.
                If pen > 0 AndAlso ls <> ss Then
                    Dim rawSide As Integer = Math.Max(ls, ss), effSide As Integer = If(ls > ss, els, ess)
                    Dim tWeak As Integer = CInt(Math.Ceiling(maxS * cfg.Scoring.VerdictWeakPct))
                    If rawSide >= tWeak AndAlso effSide < tWeak AndAlso Not RsS(x, "MTFGateReason").StartsWith("MTF BLOCK", StringComparison.Ordinal) AndAlso
                       RsS(x, "VerdictContext") <> "BELOW_MIN_MOVE" Then
                        nToNoTrade += 1
                        toNoTradeBySess(sess) = If(toNoTradeBySess.ContainsKey(sess), toNoTradeBySess(sess), 0) + 1
                    End If
                End If
                Continue For
            End If
            Dim isLong As Boolean = verdict.Contains("LONG")
            Dim cr As New CnRow With {.Ts = c.Timestamp, .Session = sess, .Regime = regime, .Side = If(isLong, "LONG", "SHORT"), .MaxScore = maxS,
                                      .Raw = If(isLong, ls, ss), .Eff = If(isLong, els, ess), .Penalty = pen,
                                      .LoggedTier = If(verdict.StartsWith("STRONG"), "STRONG", If(verdict.StartsWith("WEAK"), "WEAK", "MEDIUM"))}
            cr.RawTier = CnTier(cr.Raw, maxS, cfg)
            cr.EffTier = CnTier(cr.Eff, maxS, cfg)
            cr.Cls = CnClassOf(cr.RawTier, cr.EffTier)
            Dim sg As Sig = Nothing
            If popBySigTs.TryGetValue(c.Timestamp, sg) Then cr.Sig = sg
            dirRows.Add(cr)
        Next
        Dim pop = dirRows.Where(Function(x) x.Sig IsNot Nothing).ToList()

        ' ================================================================== report
        o.AppendLine("## 1. Self-checks")
        o.AppendLine()
        o.AppendLine("| Check | Rows | Result |")
        o.AppendLine("|---|---|---|")
        Dim tierMis = dirRows.Where(Function(x) x.EffTier <> x.LoggedTier).Count()
        o.AppendLine(String.Format(Inv, "| Effective tier from the logged effective score equals the logged verdict tier | {0} directional rows | {1} mismatches |", dirRows.Count, tierMis))
        Dim penMis = dirRows.Where(Function(x) x.Eff <> x.Raw - x.Penalty).Count()
        o.AppendLine(String.Format(Inv, "| Verdict side: effective = raw - RegimePenalty (demotion is the ADX penalty alone) | {0} directional rows | {1} rows differ |", dirRows.Count, penMis))
        o.AppendLine(String.Format(Inv, "| TRANSITIONAL rows where the TierFloor arm wins with a floor above zero (either side) | {0} TRANSITIONAL rows | {1} |", nTrans, nFloorWins))
        o.AppendLine(String.Format(Inv, "| TRANSITIONAL side scores below the penalty, held at 0 (raw < penalty, TierFloor 0: the Max(.., 0) arm) | {0} TRANSITIONAL rows | {1} side scores |", nTrans, nZeroClamp))
        o.AppendLine(String.Format(Inv, "| TRANSITIONAL side scores where effective matches neither raw - penalty, nor the floor, nor 0 | {0} TRANSITIONAL rows | {1} side scores |", nTrans, nOtherDiff))
        o.AppendLine(String.Format(Inv, "| Non-TRANSITIONAL directional rows with RegimePenalty <> 0 | {0} | {1} |", dirRows.Where(Function(x) x.Regime <> "TRANSITIONAL").Count(),
            dirRows.Where(Function(x) x.Regime <> "TRANSITIONAL" AndAlso x.Penalty <> 0).Count()))
        o.AppendLine(String.Format(Inv, "| Directional rows outside the six classes (raw tier below the effective tier, or unclassifiable) | {0} | {1} |", dirRows.Count, dirRows.Where(Function(x) x.Cls < 0).Count()))
        o.AppendLine(String.Format(Inv, "| Population rows matched to a logged row | {0} population signals | {1} matched |", sigs.Count, pop.Count))
        o.AppendLine()
        o.AppendLine("- Tier-floor arithmetic: the floor binds only when TierFloor(raw) > raw - penalty. TierFloor(raw) <= raw - 3 for every raw >= 6 (12 -> 9, 9 -> 6, 6 -> 3, and the gap grows inside each band), and the penalty is at most 2. So at the tracked values the floor cannot bind, and the demotion is the ADX penalty alone.")
        o.AppendLine()

        o.AppendLine("## 2. Census counts: every trading-week directional row")
        o.AppendLine()
        o.AppendLine("| Session | Regime | Rows | native STRONG | demoted-to-MEDIUM | native MEDIUM | demoted-to-WEAK | native WEAK | demoted two tiers | Unchanged | Demoted share % |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each rg In CnRegimes
                Dim sL As String = sess, rL As String = rg
                Dim cell = dirRows.Where(Function(x) x.Session = sL AndAlso x.Regime = rL).ToList()
                If cell.Count = 0 Then Continue For
                Dim cnt = Enumerable.Range(0, CnClasses.Length).Select(Function(k) cell.Where(Function(x) x.Cls = k).Count()).ToArray()
                Dim demoted As Integer = cnt(1) + cnt(3) + cnt(5)
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10:0.0} |", sess, rg, cell.Count, cnt(0), cnt(1), cnt(2), cnt(3), cnt(4), cnt(5),
                    cnt(0) + cnt(2) + cnt(4), 100.0 * demoted / cell.Count))
            Next
        Next
        o.AppendLine()
        o.AppendLine("| Session | Demoted out of WEAK into NO TRADE by the penalty alone (not in the population, counts only) |")
        o.AppendLine("|---|---|")
        For Each sess In SessionOrder
            o.AppendLine(String.Format(Inv, "| {0} | {1} |", sess, If(toNoTradeBySess.ContainsKey(sess), toNoTradeBySess(sess), 0)))
        Next
        o.AppendLine()

        o.AppendLine("## 3. Census headline: the swing read population")
        o.AppendLine()
        If pop.Count = dirRows.Count Then
            o.AppendLine(String.Format(Inv, "- Every trading-week directional row ({0}) is in the population, so the section 2 counts are the population counts.", dirRows.Count))
        Else
            o.AppendLine(String.Format(Inv, "- {0} of {1} trading-week directional rows are in the population; the table below counts the population only.", pop.Count, dirRows.Count))
        End If
        o.AppendLine()
        o.AppendLine("| Session | Rows | TRANSITIONAL rows | native STRONG | demoted-to-MEDIUM | native MEDIUM | demoted-to-WEAK | native WEAK | demoted two tiers | Demoted share of all rows % | Demoted share of TRANSITIONAL rows % |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder.Concat({"ALL"})
            Dim sL As String = sess
            Dim cell = pop.Where(Function(x) sL = "ALL" OrElse x.Session = sL).ToList()
            If cell.Count = 0 Then Continue For
            Dim cnt = Enumerable.Range(0, CnClasses.Length).Select(Function(k) cell.Where(Function(x) x.Cls = k).Count()).ToArray()
            Dim nT As Integer = cell.Where(Function(x) x.Regime = "TRANSITIONAL").Count()
            Dim dem As Integer = cnt(1) + cnt(3) + cnt(5)
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9:0.0} | {10:0.0} |", If(sL = "ALL", "ALL (counts only, never pooled for outcomes)", sL), cell.Count, nT,
                cnt(0), cnt(1), cnt(2), cnt(3), cnt(4), cnt(5), 100.0 * dem / cell.Count, 100.0 * dem / Math.Max(1, nT)))
        Next
        o.AppendLine()

        ' ---- outcomes
        Dim days = sigs.Select(Function(x) x.Ts.Date).Distinct().OrderBy(Function(x) x).ToList()
        Dim splitIdx As Integer = days.Count \ 2
        Dim splitDate As DateTime = days(splitIdx)
        o.AppendLine("## 4. Outcomes per class (net EV per trade, bps, maker/maker; 95 % bootstrap CI by trading day)")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Split date {0:yyyy-MM-dd} 00:00 UTC: H1 = {1} trading days, H2 = {2} (the stability read's rule).", splitDate, splitIdx, days.Count - splitIdx))
        o.AppendLine()
        Dim gi As Integer = 0
        Dim labels As New List(Of String)()
        For Each mode In StabModes
            o.AppendLine("### 4." & (Array.IndexOf(StabModes, mode) + 1).ToString(Inv) & " " & ModeName(mode))
            o.AppendLine()
            o.AppendLine("| Session | Sub-sample | Class | Scope | n | Net EV [95 % CI] |")
            o.AppendLine("|---|---|---|---|---|---|")
            Dim compRows As New StringBuilder()
            compRows.AppendLine("| Session | Comparison | FULL d [95 % CI] (n first / n second) | H1 d [95 % CI] (n) | H2 d [95 % CI] (n) | Label |")
            compRows.AppendLine("|---|---|---|---|---|---|")
            For Each sess In SessionOrder
                Dim res As New Dictionary(Of String, CnBoot)()
                For Each subName In {"FULL", "H1", "H2"}
                    For Each transOnly In {False, True}
                        Dim sL As String = sess, subL As String = subName, tL As Boolean = transOnly
                        Dim rows = pop.Where(Function(x) x.Session = sL AndAlso InSub(x.Sig, subL, splitDate) AndAlso (Not tL OrElse x.Regime = "TRANSITIONAL")).ToList()
                        gi += 1
                        res(subName & "|" & transOnly.ToString()) = CnBootstrap(rows, mode, fees, CnSeed + gi)
                    Next
                Next
                For Each subName In {"FULL", "H1", "H2"}
                    For Each transOnly In {False, True}
                        Dim b = res(subName & "|" & transOnly.ToString())
                        For k As Integer = 0 To CnClasses.Length - 1
                            If b.N(k) = 0 Then Continue For
                            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4}{5} | {6} |", sess, subName, CnClasses(k), If(transOnly, "TRANSITIONAL", "every regime"),
                                b.N(k), If(b.N(k) < StabFloor, " (NOT READABLE)", ""), Ci1(b.Ev(k), b.Lo(k), b.Hi(k))))
                        Next
                    Next
                Next
                For ci As Integer = 0 To CnComps.Length - 1
                    Dim cp = CnComps(ci)
                    Dim key As String = cp.Item4.ToString()
                    Dim f = res("FULL|" & key), h1 = res("H1|" & key), h2 = res("H2|" & key)
                    Dim lbl As String = CnLabel(f, h1, h2, cp.Item2, cp.Item3)
                    compRows.AppendLine(String.Format(Inv, "| {0} | {1} | {2} ({3} / {4}) | {5} ({6} / {7}) | {8} ({9} / {10}) | {11} |", sess, cp.Item1,
                        CnDiffText(f, cp.Item2, cp.Item3), f.N(cp.Item2), f.N(cp.Item3),
                        CnDiffText(h1, cp.Item2, cp.Item3), h1.N(cp.Item2), h1.N(cp.Item3),
                        CnDiffText(h2, cp.Item2, cp.Item3), h2.N(cp.Item2), h2.N(cp.Item3), lbl))
                    labels.Add(ModeName(mode) & " | " & sess & " | " & cp.Item1 & " | " & lbl)
                Next
            Next
            o.AppendLine()
            o.AppendLine("Comparisons (d = EV(first) - EV(second)):")
            o.AppendLine()
            o.Append(compRows.ToString())
            o.AppendLine()
        Next

        o.AppendLine("## 5. Labels")
        o.AppendLine()
        For Each l In labels
            o.AppendLine("- " & l)
        Next
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

    Private Class CnBoot
        Public N As Integer() = New Integer(5) {}
        Public Ev As Double() = New Double(5) {}
        Public Lo As Double() = New Double(5) {}
        Public Hi As Double() = New Double(5) {}
        ' pairwise difference CIs keyed "a|b"
        Public DLo As New Dictionary(Of String, Double)()
        Public DHi As New Dictionary(Of String, Double)()
    End Class

    Private Function CnBootstrap(rows As List(Of CnRow), mode As String, fees As FeeCase, seed As Integer) As CnBoot
        Dim b As New CnBoot()
        For k As Integer = 0 To 5
            b.Ev(k) = Double.NaN : b.Lo(k) = Double.NaN : b.Hi(k) = Double.NaN
        Next
        Dim dayIdx As New Dictionary(Of DateTime, Integer)()
        Dim sums As New List(Of Double())()
        Dim cnts As New List(Of Integer())()
        Dim tot As Double() = New Double(5) {}
        For Each r In rows
            If r.Cls < 0 Then Continue For
            Dim s = r.Sig
            Dim ev As Double = Result(s, s.Res(ModeKey(s, mode)), fees.MakerRt, fees.MakerRt)
            Dim i As Integer
            If Not dayIdx.TryGetValue(s.Ts.Date, i) Then
                i = sums.Count
                dayIdx(s.Ts.Date) = i
                sums.Add(New Double(5) {})
                cnts.Add(New Integer(5) {})
            End If
            sums(i)(r.Cls) += ev
            cnts(i)(r.Cls) += 1
            b.N(r.Cls) += 1
            tot(r.Cls) += ev
        Next
        For k As Integer = 0 To 5
            If b.N(k) > 0 Then b.Ev(k) = tot(k) / b.N(k)
        Next
        Dim nDays As Integer = sums.Count
        If nDays = 0 Then Return b
        Dim rng As New Random(seed)
        Dim draws As New List(Of List(Of Double))()
        For k As Integer = 0 To 5
            draws.Add(New List(Of Double)())
        Next
        Dim pairs = CnComps.Select(Function(c) Tuple.Create(c.Item2, c.Item3)).Distinct().ToList()
        Dim dDraws = pairs.Select(Function(p) New List(Of Double)()).ToList()
        Dim bs As Double() = New Double(5) {}
        Dim bc As Integer() = New Integer(5) {}
        For it As Integer = 1 To StabResamples
            Array.Clear(bs)
            Array.Clear(bc)
            For j As Integer = 1 To nDays
                Dim i As Integer = rng.Next(nDays)
                For k As Integer = 0 To 5
                    bs(k) += sums(i)(k)
                    bc(k) += cnts(i)(k)
                Next
            Next
            For k As Integer = 0 To 5
                If bc(k) > 0 Then draws(k).Add(bs(k) / bc(k))
            Next
            For pi As Integer = 0 To pairs.Count - 1
                Dim a As Integer = pairs(pi).Item1, c As Integer = pairs(pi).Item2
                If bc(a) > 0 AndAlso bc(c) > 0 Then dDraws(pi).Add(bs(a) / bc(a) - bs(c) / bc(c))
            Next
        Next
        For k As Integer = 0 To 5
            If draws(k).Count > 0 Then
                Dim srt = draws(k).OrderBy(Function(x) x).ToList()
                b.Lo(k) = Pctl(srt, 2.5)
                b.Hi(k) = Pctl(srt, 97.5)
            End If
        Next
        For pi As Integer = 0 To pairs.Count - 1
            Dim key As String = pairs(pi).Item1.ToString(Inv) & "|" & pairs(pi).Item2.ToString(Inv)
            If dDraws(pi).Count > 1 Then
                Dim srt = dDraws(pi).OrderBy(Function(x) x).ToList()
                b.DLo(key) = Pctl(srt, 2.5)
                b.DHi(key) = Pctl(srt, 97.5)
            Else
                b.DLo(key) = Double.NaN
                b.DHi(key) = Double.NaN
            End If
        Next
        Return b
    End Function

    Private Function CnDiffText(b As CnBoot, a As Integer, c As Integer) As String
        If b.N(a) = 0 OrElse b.N(c) = 0 Then Return "n/a"
        Dim key As String = a.ToString(Inv) & "|" & c.ToString(Inv)
        Return Ci1(b.Ev(a) - b.Ev(c), b.DLo(key), b.DHi(key))
    End Function

    ''' <summary>The pre-registered label (this file's header), first match wins.</summary>
    Private Function CnLabel(f As CnBoot, h1 As CnBoot, h2 As CnBoot, a As Integer, c As Integer) As String
        If f.N(a) < StabFloor OrElse f.N(c) < StabFloor Then Return "NOT READABLE"
        Dim key As String = a.ToString(Inv) & "|" & c.ToString(Inv)
        Dim readable = Function(b As CnBoot) b.N(a) >= StabFloor AndAlso b.N(c) >= StabFloor
        Dim d = Function(b As CnBoot) b.Ev(a) - b.Ev(c)
        Dim sig = Function(b As CnBoot) readable(b) AndAlso Not Double.IsNaN(b.DLo(key)) AndAlso (b.DLo(key) > 0 OrElse b.DHi(key) < 0)
        Dim side = Function(b As CnBoot) If(d(b) > 0, "demoted BETTER", "demoted WORSE")
        If sig(h1) AndAlso sig(h2) AndAlso Math.Sign(d(h1)) = Math.Sign(d(h2)) Then Return "CONFIRMED: " & side(h1)
        If sig(h1) AndAlso readable(h2) AndAlso Math.Sign(d(h1)) = Math.Sign(d(h2)) Then Return "H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT: " & side(h1)
        If sig(h1) Then Return "H1 FINDING, H2 CONTRADICTS OR NOT READABLE: " & side(h1)
        If sig(h2) Then Return "DISCOVERY ONLY (H2): " & side(h2)
        Return "NO DIFFERENCE SHOWN"
    End Function

End Module
