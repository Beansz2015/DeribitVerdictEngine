Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/PocGateDefect.vb
'
' --mode pocgate: the share of rows affected by the POC-tier gate defect that stopped the
' MEDIUM-tier bug hunt, session 1 (docs/medium-tier-bug-hunt-2026-09-16.md). The default mode
' and --mode stability are untouched.
'
' ⛔ READS BACKWARDS FROM a6b33fe ON (2026-09-24, trader-ruled Q-1 in
' docs/engine-fix-session-a-spec-back.md). This mode was written against the DEFECTIVE gate.
' a6b33fe fixed the gate in SignalEmitter, so on a tree at or after it:
'   * "as shipped"        = the FIXED gate (it reproduces the logged Placed* only on rows
'                           written by a pre-fix build where the fix changes nothing);
'   * "swapped" / counterfactual = the OLD, DEFECTIVE gate.
' The flip and move counts keep their size; their direction label inverts. The fix's own
' verification is in docs/engine-fix-session-a-spec-back.md (handle H-3 at base a3c9079).
' The method notes below describe the pre-fix tree and are kept as written.
'
' The defect. CalcVPFRLite labels price inside hvn_proximity_pct BELOW the POC as
' NEAR_HVN_SUPPORT and at or ABOVE the POC as NEAR_HVN_RESIST (Core/Indicators_Structure.vb:171-176,
' and its spec at introduction, 508f33d). The POC-tier gate in
' SignalEmitter.ComputeStructuralSideLevels (Core/SignalEmitter.vb:330-332) opens the LONG POC tier
' on NEAR_HVN_RESIST or IN_LVN_BEAR, and its spec at introduction (721882e) says that pair means
' "wall above price". The NEAR_HVN half of the gate therefore opens where the POC sits below price,
' where the tier cannot place, and stays shut where the POC sits above price. Mirror for SHORT.
'
' Method.
'   * The CSV logs neither VPFRPoc nor VPFRSignal, so CalcVPFRLite (the shipped function, tracked
'     VPFR keys) is RECOMPUTED from exchange candles with volume. A row is VERIFIED only when the
'     recomputed profile reproduces the four logged VPFR fields (VPFRVAH, VPFRVAL,
'     VPFRNearestHvnAbove, VPFRNearestHvnBelow), each within 0.006 USD of the F2 value. Five
'     candle-window variants are tried in a fixed order; the first that verifies is used.
'   * On a verified row the shipped SignalEmitter.ComputeSideLevels runs twice: as shipped (it must
'     reproduce the logged Placed* levels), and with NEAR_HVN_SUPPORT and NEAR_HVN_RESIST swapped in
'     r.VPFRSignal. pocGated is the only reader of VPFRSignal in that function, so the swap is
'     exactly the gate its spec describes. The Step 5c min-move gate (the composed floor from
'     scoring.trade_costs) is applied to both placed targets.
'   * Candles: the backtest store (backtest_data/candles_{1m,3m}_YYYY-MM.csv, read-only) where it
'     covers; bars after its newest bar come from public/get_tradingview_chart_data through
'     DeribitClient.GetCandlesAsync and are cached in this instrument's cache folder.
'   * Settings: the tracked settings.json. No VPFR, structural_levels, trade_costs or verdict-pct
'     value that these steps read changed between v51 and v68 (v56 added modes at 0 and buffers at
'     0; v62 composed the floor to the retired 0.0008; v63 added a flag at false).
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode pocgate \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks

Partial Module SwingFallbackReadProgram

    Private Const PgTol As Double = 0.006
    ' (closed bars before the forming bar, forming bar: 0 none / 1 zero-volume bar at the logged Price /
    '  2 the final exchange bar at the forming open, label)
    Private ReadOnly PgVariants As Tuple(Of Integer, Integer, String)() = {
        Tuple.Create(249, 1, "249 closed + zero-volume forming bar at Price"),
        Tuple.Create(250, 1, "250 closed + zero-volume forming bar at Price"),
        Tuple.Create(250, 0, "250 closed, no forming bar"),
        Tuple.Create(249, 2, "249 closed + final exchange bar at the forming open"),
        Tuple.Create(250, 2, "250 closed + final exchange bar at the forming open")}
    Private ReadOnly PgTiers As String() = {"STRONG", "MEDIUM", "WEAK"}
    Private ReadOnly PgLabels As String() = {"NEAR_HVN_SUPPORT", "NEAR_HVN_RESIST", "IN_LVN_BULL", "IN_LVN_BEAR", "NEUTRAL"}

    Private Class PgBars
        Public OpenMs As Long() = {}
        Public Bars As Candle() = {}
    End Class

    Private Class PgResult
        Public Ts As DateTime
        Public Session As String = ""
        Public Kind As String = ""          ' DIRECTIONAL / BELOW_MIN_MOVE / OTHER
        Public Tier As String = ""
        Public Side As String = ""          ' verdict side, or the effective dominant side on a BELOW_MIN_MOVE row
        Public InPopulation As Boolean
        Public WindowVariant As Integer = -1
        Public Signal As String = ""
        Public HasPlaced As Boolean
        Public Reproduced As Boolean
        Public StopChanged As Boolean
        Public AnyTargetChanged As Boolean
        Public CurReason As String = ""
        Public CfReason As String = ""
        Public SideTargetChanged As Boolean
        Public CurPass5c As Boolean
        Public CfPass5c As Boolean
        Public PocLongCur As Boolean
        Public PocShortCur As Boolean
    End Class

    Private Async Function RunPocGateAsync(header As StringBuilder, cfg As EngineSettings, root As String, cacheDir As String,
                                           pooledPath As String, livePath As String,
                                           merged As Dictionary(Of DateTime, Tuple(Of CsvRow, String)),
                                           sigs As List(Of Sig), collectorStart As DateTime, collectorIds As HashSet(Of String),
                                           weekOpenHour As Integer, weekCloseHourExcl As Integer,
                                           outPath As String) As Task(Of Integer)
        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode pocgate (POC-tier gate defect, share of rows affected)" & vbLf & vbLf &
            "> NOTE: from a6b33fe on, 'as shipped' is the FIXED gate and the swap is the OLD defect. See the header of tools/ops/SwingFallbackRead/PocGateDefect.vb."))
        o.AppendLine("- Read: docs/medium-tier-bug-hunt-2026-09-16.md. Stop rule: docs/medium-tier-diagnosis-brief-2026-09-16.md section 0.")
        o.AppendLine(String.Format(Inv, "- Tolerance for every logged-versus-recomputed comparison: {0} USD (the CSV prints F2).", PgTol))
        o.AppendLine(String.Format(Inv, "- Step 5c floor: scoring.trade_costs EffectiveMinMovePct = {0} of price.", cfg.Scoring.TradeCosts.EffectiveMinMovePct))
        o.AppendLine()

        ' ---- logged VAH / VAL (not carried on CsvRow): same file order and first-timestamp-wins rule as the merge
        Dim va As New Dictionary(Of DateTime, Tuple(Of Double, Double))()
        PgLoadValueArea(pooledPath, va)
        PgLoadValueArea(livePath, va)

        ' ---- rows: the swing read's funnel, keeping the non-directional rows
        Dim popTs As New HashSet(Of DateTime)(sigs.Select(Function(s) s.Ts))
        Dim rows As New List(Of Tuple(Of CsvRow, String))()
        Dim nPre As Integer = 0, nConc As Integer = 0, nOff As Integer = 0, nNoSess As Integer = 0, nNoVa As Integer = 0
        For Each kv In merged.OrderBy(Function(k) k.Key)
            Dim r = kv.Value.Item1
            If r.Timestamp < V51Edge Then nPre += 1 : Continue For
            If r.Timestamp >= collectorStart AndAlso Not collectorIds.Contains(kv.Value.Item2) Then nConc += 1 : Continue For
            If Not InTradingWeek(r.Timestamp, weekOpenHour, weekCloseHourExcl) Then nOff += 1 : Continue For
            Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, r.Timestamp.Hour)
            If bucket Is Nothing Then nNoSess += 1 : Continue For
            If Not va.ContainsKey(r.Timestamp) Then nNoVa += 1 : Continue For
            rows.Add(Tuple.Create(r, bucket.Name.ToUpperInvariant()))
        Next

        ' ---- candles with volume
        Dim covLog As New StringBuilder()
        covLog.AppendLine("| Resolution | Store files read | Bars from the store | Bars from this instrument's cache | Bars fetched this run | Bars missing in the needed span |")
        covLog.AppendLine("|---|---|---|---|---|---|")
        Dim minMs As Long = ToMs(rows.Min(Function(x) x.Item1.Timestamp))
        Dim maxMs As Long = ToMs(rows.Max(Function(x) x.Item1.Timestamp))
        Dim barsByRes As New Dictionary(Of Integer, PgBars)()
        For Each res In rows.Select(Function(x) x.Item1.ExecResolution).Distinct().OrderBy(Function(x) x).ToList()
            If res <> 1 AndAlso res <> 3 Then Continue For
            Dim resMs As Long = res * 60000L
            barsByRes(res) = Await PgLoadBarsAsync(root, cacheDir, res, (minMs \ resMs) * resMs - 260L * resMs, (maxMs \ resMs) * resMs + resMs, covLog)
        Next

        ' ---- recompute, verify, compare the shipped gate with the gate its spec describes
        Dim floorPct As Double = cfg.Scoring.TradeCosts.EffectiveMinMovePct
        Dim results As New List(Of PgResult)()
        Dim nBadRes As Integer = 0, nNoBars As Integer = 0
        Dim variantHits(PgVariants.Length - 1) As Integer
        Dim dummy As New VerdictResult()
        Dim vc = cfg.Indicators.VPFR
        For Each x In rows
            Dim r = x.Item1
            Dim bars As PgBars = Nothing
            If Not barsByRes.TryGetValue(r.ExecResolution, bars) Then nBadRes += 1 : Continue For
            Dim resMs As Long = r.ExecResolution * 60000L
            Dim formOpen As Long = (ToMs(r.Timestamp) \ resMs) * resMs
            Dim iPrev As Integer = Array.BinarySearch(bars.OpenMs, formOpen - resMs)
            If iPrev < 250 Then nNoBars += 1 : Continue For
            Dim iForm As Integer = Array.BinarySearch(bars.OpenMs, formOpen)
            Dim logged = va(r.Timestamp)

            Dim pr As New PgResult With {.Ts = r.Timestamp, .Session = x.Item2, .InPopulation = popTs.Contains(r.Timestamp), .HasPlaced = r.HasPlaced}
            Dim v As String = If(r.Verdict, "").Trim().ToUpperInvariant()
            Dim isDir As Boolean = (v = "STRONG LONG" OrElse v = "LONG" OrElse v = "WEAK LONG" OrElse v = "STRONG SHORT" OrElse v = "SHORT" OrElse v = "WEAK SHORT")
            If isDir Then
                pr.Kind = "DIRECTIONAL"
                pr.Side = If(v.Contains("LONG"), "LONG", "SHORT")
                pr.Tier = If(v.StartsWith("STRONG"), "STRONG", If(v.StartsWith("WEAK"), "WEAK", "MEDIUM"))
            ElseIf v.StartsWith("NO TRADE") AndAlso String.Equals(If(r.VerdictContext, "").Trim(), "BELOW_MIN_MOVE", StringComparison.OrdinalIgnoreCase) Then
                pr.Kind = "BELOW_MIN_MOVE"
                pr.Side = If(r.EffectiveLongScore > r.EffectiveShortScore, "LONG", If(r.EffectiveShortScore > r.EffectiveLongScore, "SHORT", ""))
                pr.Tier = PgTier(r, cfg, pr.Side)
            Else
                pr.Kind = "OTHER"
            End If

            Dim vr = PgRecomputeVpfr(bars, iPrev, iForm, formOpen, r, logged.Item1, logged.Item2, cfg, variantHits)
            pr.WindowVariant = vr.WindowVariant
            Dim poc As Double = vr.Poc, sig As String = vr.Signal

            If pr.WindowVariant >= 0 Then
                pr.Signal = sig
                Dim rCur = PgIndicators(r, poc, sig)
                Dim rCf = PgIndicators(r, poc, PgSwapNearHvn(sig))
                Dim curL = SignalEmitter.ComputeSideLevels(dummy, rCur, cfg, isLong:=True)
                Dim curS = SignalEmitter.ComputeSideLevels(dummy, rCur, cfg, isLong:=False)
                Dim cfL = SignalEmitter.ComputeSideLevels(dummy, rCf, cfg, isLong:=True)
                Dim cfS = SignalEmitter.ComputeSideLevels(dummy, rCf, cfg, isLong:=False)
                pr.Reproduced = r.HasPlaced AndAlso
                                Math.Abs(curL.Target - r.PlacedTargetLong) <= PgTol AndAlso Math.Abs(curL.StopPx - r.PlacedStopLong) <= PgTol AndAlso
                                Math.Abs(curS.Target - r.PlacedTargetShort) <= PgTol AndAlso Math.Abs(curS.StopPx - r.PlacedStopShort) <= PgTol
                pr.AnyTargetChanged = Math.Abs(cfL.Target - curL.Target) > PgTol OrElse Math.Abs(cfS.Target - curS.Target) > PgTol
                pr.StopChanged = Math.Abs(cfL.StopPx - curL.StopPx) > PgTol OrElse Math.Abs(cfS.StopPx - curS.StopPx) > PgTol
                pr.PocLongCur = (curL.TargetReason = "POC")
                pr.PocShortCur = (curS.TargetReason = "POC")
                If pr.Side <> "" Then
                    Dim cur = If(pr.Side = "LONG", curL, curS)
                    Dim cf = If(pr.Side = "LONG", cfL, cfS)
                    Dim floorDist As Double = floorPct * r.Price
                    pr.CurReason = If(cur.TargetReason, "")
                    pr.CfReason = If(cf.TargetReason, "")
                    pr.SideTargetChanged = Math.Abs(cf.Target - cur.Target) > PgTol
                    pr.CurPass5c = Not (floorDist > 0 AndAlso Math.Abs(cur.Target - r.Price) < floorDist)
                    pr.CfPass5c = Not (floorDist > 0 AndAlso Math.Abs(cf.Target - r.Price) < floorDist)
                End If
            End If
            results.Add(pr)
        Next

        Dim ver = results.Where(Function(p) p.WindowVariant >= 0).ToList()
        Dim popAll = results.Where(Function(p) p.InPopulation).ToList()
        Dim popVer = ver.Where(Function(p) p.InPopulation).ToList()

        ' ================================================================== report
        o.AppendLine("## 1. Row funnel")
        o.AppendLine()
        o.AppendLine("| Step | Rows |")
        o.AppendLine("|---|---|")
        o.AppendLine(Row("Merged rows (pooled book + box live log, first timestamp wins)", merged.Count))
        o.AppendLine(Row("Before the v51 geometry boundary (2026-07-06 13:08:51 UTC)", nPre))
        o.AppendLine(Row("Non-collector instance rows inside the collector era", nConc))
        o.AppendLine(Row("Outside the trading week", nOff))
        o.AppendLine(Row("No session bucket", nNoSess))
        o.AppendLine(Row("No parseable logged VPFRVAH/VPFRVAL", nNoVa))
        o.AppendLine(Row("Execution resolution other than 1 or 3", nBadRes))
        o.AppendLine(Row("Fewer than 250 closed bars before the forming bar", nNoBars))
        o.AppendLine(Row("**Rows recomputed**", results.Count))
        o.AppendLine(Row("... of which swing read population rows (trading-week directional, valid placed levels)", popAll.Count))
        o.AppendLine(Row("Swing read population, for reference", sigs.Count))
        o.AppendLine()

        o.AppendLine("## 2. Candle coverage")
        o.AppendLine()
        o.Append(covLog.ToString())
        o.AppendLine()

        o.AppendLine("## 3. Profile verification (recomputed VAH, VAL, nearest HVN above and below all equal the logged values)")
        o.AppendLine()
        o.AppendLine("| Window variant | Rows this variant verifies (each variant counted on its own) | Rows where it is the first variant that verifies |")
        o.AppendLine("|---|---|---|")
        For vi As Integer = 0 To PgVariants.Length - 1
            Dim viL As Integer = vi
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} |", PgVariants(vi).Item3, variantHits(vi), ver.Where(Function(p) p.WindowVariant = viL).Count()))
        Next
        o.AppendLine()
        o.AppendLine("| Session | Kind | Rows recomputed | Verified | Verified % |")
        o.AppendLine("|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each kind In {"DIRECTIONAL", "BELOW_MIN_MOVE", "OTHER"}
                Dim sessL As String = sess, kindL As String = kind
                Dim n As Integer = results.Where(Function(p) p.Session = sessL AndAlso p.Kind = kindL).Count()
                Dim k As Integer = ver.Where(Function(p) p.Session = sessL AndAlso p.Kind = kindL).Count()
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4:0.0} |", sess, kind, n, k, 100.0 * k / Math.Max(1, n)))
            Next
        Next
        o.AppendLine(String.Format(Inv, "| ALL | population rows | {0} | {1} | {2:0.0} |", popAll.Count, popVer.Count, 100.0 * popVer.Count / Math.Max(1, popAll.Count)))
        o.AppendLine(String.Format(Inv, "| ALL | all kinds | {0} | {1} | {2:0.0} |", results.Count, ver.Count, 100.0 * ver.Count / Math.Max(1, results.Count)))
        o.AppendLine()
        o.AppendLine("| UTC month | Rows recomputed | Verified | Verified % |")
        o.AppendLine("|---|---|---|---|")
        For Each mon In results.Select(Function(p) p.Ts.ToString("yyyy-MM", Inv)).Distinct().OrderBy(Function(s) s)
            Dim monL As String = mon
            Dim n As Integer = results.Where(Function(p) p.Ts.ToString("yyyy-MM", Inv) = monL).Count()
            Dim k As Integer = ver.Where(Function(p) p.Ts.ToString("yyyy-MM", Inv) = monL).Count()
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3:0.0} |", mon, n, k, 100.0 * k / Math.Max(1, n)))
        Next
        o.AppendLine()

        o.AppendLine("## 4. Shipped path reproduction on verified rows")
        o.AppendLine()
        Dim verPlaced = ver.Where(Function(p) p.HasPlaced).ToList()
        o.AppendLine(String.Format(Inv, "- Verified rows with logged placed levels: {0}. ComputeSideLevels as shipped reproduces all four logged Placed* values: {1} ({2:0.00} %).",
            verPlaced.Count, verPlaced.Where(Function(p) p.Reproduced).Count(), 100.0 * verPlaced.Where(Function(p) p.Reproduced).Count() / Math.Max(1, verPlaced.Count)))
        o.AppendLine(String.Format(Inv, "- Rows where the counterfactual gate moved a STOP (must be 0; the gate reads targets only): {0}.", ver.Where(Function(p) p.StopChanged).Count()))
        o.AppendLine(String.Format(Inv, "- Directional verified rows whose shipped placed target already fails Step 5c (must be 0): {0}.", ver.Where(Function(p) p.Kind = "DIRECTIONAL" AndAlso Not p.CurPass5c).Count()))
        o.AppendLine(String.Format(Inv, "- BELOW_MIN_MOVE verified rows whose shipped placed target passes Step 5c (must be 0): {0}.", ver.Where(Function(p) p.Kind = "BELOW_MIN_MOVE" AndAlso p.Side <> "" AndAlso p.CurPass5c).Count()))
        o.AppendLine()

        o.AppendLine("## 5. Recomputed VPFR label on verified rows")
        o.AppendLine()
        o.AppendLine("| Session | Verified rows | " & String.Join(" | ", PgLabels) & " |")
        o.AppendLine("|---|---|" & String.Concat(Enumerable.Repeat("---|", PgLabels.Length)))
        For Each sess In SessionOrder.Concat({"ALL"})
            Dim sessL As String = sess
            Dim sub1 = ver.Where(Function(p) sessL = "ALL" OrElse p.Session = sessL).ToList()
            o.AppendLine("| " & sess & " | " & sub1.Count.ToString(Inv) & " | " &
                         String.Join(" | ", PgLabels.Select(Function(l) PgPct(sub1.Where(Function(p) p.Signal = l).Count(), sub1.Count))) & " |")
        Next
        o.AppendLine()

        o.AppendLine("## 6. POC placements under the SHIPPED gate, by recomputed label (verified rows)")
        o.AppendLine()
        o.AppendLine("| Side | Label | Rows whose placed target is the POC |")
        o.AppendLine("|---|---|---|")
        For Each lab In PgLabels
            Dim labL As String = lab
            o.AppendLine(String.Format(Inv, "| LONG | {0} | {1} |", lab, ver.Where(Function(p) p.PocLongCur AndAlso p.Signal = labL).Count()))
        Next
        For Each lab In PgLabels
            Dim labL As String = lab
            o.AppendLine(String.Format(Inv, "| SHORT | {0} | {1} |", lab, ver.Where(Function(p) p.PocShortCur AndAlso p.Signal = labL).Count()))
        Next
        o.AppendLine(String.Format(Inv, "- Logged TargetCapReason = poc in the whole swing read population (all months, verified or not): {0} of {1}.", sigs.Where(Function(s) s.Cap = "poc").Count(), sigs.Count))
        o.AppendLine()

        o.AppendLine("## 7. The gate its spec describes (NEAR_HVN labels swapped), against the shipped gate")
        o.AppendLine()
        o.AppendLine("### 7.1 Population rows (trading-week directional, verified)")
        o.AppendLine()
        o.AppendLine("| Session | Tier | Verified rows | Verdict-side target moves | ... to a POC target | Step 5c would flip to NO TRADE | Flip % of verified rows |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder.Concat({"ALL"})
            For Each tier In PgTiers.Concat({"ALL"})
                Dim sessL As String = sess, tierL As String = tier
                Dim sub1 = popVer.Where(Function(p) (sessL = "ALL" OrElse p.Session = sessL) AndAlso (tierL = "ALL" OrElse p.Tier = tierL)).ToList()
                Dim moved As Integer = sub1.Where(Function(p) p.SideTargetChanged).Count()
                Dim toPoc As Integer = sub1.Where(Function(p) p.SideTargetChanged AndAlso p.CfReason = "POC").Count()
                Dim flips As Integer = sub1.Where(Function(p) p.CurPass5c AndAlso Not p.CfPass5c).Count()
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} |", sess, tier, sub1.Count, moved, toPoc, flips, PgPct(flips, sub1.Count)))
            Next
        Next
        o.AppendLine()
        o.AppendLine("### 7.2 NO TRADE rows vetoed by Step 5c (VerdictContext BELOW_MIN_MOVE, verified)")
        o.AppendLine()
        o.AppendLine("| Session | Tier before the veto | Verified rows | Dominant-side target moves | Step 5c would pass: row becomes directional |")
        o.AppendLine("|---|---|---|---|---|")
        Dim bmm = ver.Where(Function(p) p.Kind = "BELOW_MIN_MOVE" AndAlso p.Side <> "").ToList()
        For Each sess In SessionOrder.Concat({"ALL"})
            For Each tier In PgTiers.Concat({"ALL"})
                Dim sessL As String = sess, tierL As String = tier
                Dim sub1 = bmm.Where(Function(p) (sessL = "ALL" OrElse p.Session = sessL) AndAlso (tierL = "ALL" OrElse p.Tier = tierL)).ToList()
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} |", sess, tier, sub1.Count,
                    sub1.Where(Function(p) p.SideTargetChanged).Count(), sub1.Where(Function(p) Not p.CurPass5c AndAlso p.CfPass5c).Count()))
            Next
        Next
        o.AppendLine()
        o.AppendLine("### 7.3 Every verified row (all verdicts): any placed target that moves")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Verified rows: {0}. Rows where the long or the short placed target moves: {1}. These values reach the CSV Placed* columns and the bridge payload levels.",
            ver.Count, PgPct(ver.Where(Function(p) p.AnyTargetChanged).Count(), ver.Count)))
        o.AppendLine()

        o.AppendLine("## 8. VPFR vote against the verdict side (verified population rows, descriptive only)")
        o.AppendLine()
        o.AppendLine("The verdict already contains the VPFR vote, so these shares are not a test of the vote.")
        o.AppendLine()
        o.AppendLine("| Session | Label | Rows | Vote on the verdict side | Vote against the verdict side |")
        o.AppendLine("|---|---|---|---|---|")
        For Each sess In SessionOrder.Concat({"ALL"})
            For Each lab In PgLabels.Take(4)
                Dim sessL As String = sess, labL As String = lab
                Dim voteSide As String = If(lab = "NEAR_HVN_SUPPORT" OrElse lab = "IN_LVN_BULL", "LONG", "SHORT")
                Dim sub1 = popVer.Where(Function(p) (sessL = "ALL" OrElse p.Session = sessL) AndAlso p.Signal = labL).ToList()
                Dim same As Integer = sub1.Where(Function(p) p.Side = voteSide).Count()
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} |", sess, lab, sub1.Count, same, sub1.Count - same))
            Next
        Next
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

    ' ------------------------------------------------------------------ helpers

    ''' <summary>One CalcVPFRLite recompute for a logged row. WindowVariant is the first window variant
    ''' whose profile reproduces the four logged VPFR fields, or -1 when none does; the POC, label and
    ''' window are then the first applicable variant's (UNVERIFIED). Shared by --mode pocgate and
    ''' --mode rescore.</summary>
    Private Class PgVpfr
        Public WindowVariant As Integer = -1
        Public Poc As Double
        Public Signal As String = ""
        Public NearPoc As Boolean
        Public Vah As Double
        Public Val As Double
        Public VaSignal As String = ""
        Public HvnAbove As Double
        Public HvnBelow As Double
        Public LvnAbove As Double
        Public LvnBelow As Double
        Public Window As List(Of Candle)
    End Class

    ''' <summary>Tries the window variants in order (PgVariants). hits(vi) counts every variant that
    ''' verifies, the first verifying variant is returned; when none verifies, the first applicable
    ''' variant's recompute is returned with WindowVariant = -1.</summary>
    Private Function PgRecomputeVpfr(bars As PgBars, iPrev As Integer, iForm As Integer, formOpen As Long,
                                     row As CsvRow, loggedVah As Double, loggedVal As Double,
                                     cfg As EngineSettings, hits As Integer()) As PgVpfr
        Dim vc = cfg.Indicators.VPFR
        Dim best As PgVpfr = Nothing
        Dim fallback As PgVpfr = Nothing
        For vi As Integer = 0 To PgVariants.Length - 1
            Dim forming As Integer = PgVariants(vi).Item2
            If forming = 2 AndAlso iForm < 0 Then Continue For
            Dim candles = PgWindow(bars, iPrev, PgVariants(vi).Item1, forming, formOpen, row.Price, iForm)
            Dim x As New PgVpfr With {.Window = candles}
            Dim vols As Double() = Nothing, bLow As Double = 0, bSize As Double = 0
            IndicatorEngine.CalcVPFRLite(candles, row.Price, x.Poc, x.NearPoc, x.Signal, x.Vah, x.Val, x.VaSignal,
                                         x.HvnAbove, x.HvnBelow, x.LvnAbove, x.LvnBelow, vols, bLow, bSize,
                                         numBuckets:=vc.NumBuckets, hvnVolPct:=vc.HvnVolPct, lvnVolPct:=vc.LvnVolPct,
                                         hvnProximityPct:=vc.HvnProximityPct, decayBase:=vc.DecayBase, valueAreaPct:=vc.ValueAreaPct)
            If fallback Is Nothing Then fallback = x
            Dim ok As Boolean = Math.Abs(x.Vah - loggedVah) <= PgTol AndAlso Math.Abs(x.Val - loggedVal) <= PgTol AndAlso
                                Math.Abs(x.HvnAbove - row.VpfrNearestHvnAbove) <= PgTol AndAlso Math.Abs(x.HvnBelow - row.VpfrNearestHvnBelow) <= PgTol
            If ok Then
                If hits IsNot Nothing Then hits(vi) += 1
                If best Is Nothing Then
                    x.WindowVariant = vi
                    best = x
                End If
            End If
        Next
        Return If(best, fallback)
    End Function

    Private Function PgPct(k As Integer, n As Integer) As String
        If n = 0 Then Return "n/a"
        Return String.Format(Inv, "{0} ({1:0.00} %)", k, 100.0 * k / n)
    End Function

    Private Function PgSwapNearHvn(sig As String) As String
        If sig = "NEAR_HVN_SUPPORT" Then Return "NEAR_HVN_RESIST"
        If sig = "NEAR_HVN_RESIST" Then Return "NEAR_HVN_SUPPORT"
        Return sig
    End Function

    Private Function PgTier(r As CsvRow, cfg As EngineSettings, side As String) As String
        If side = "" OrElse r.MaxScore <= 0 Then Return ""
        Dim score As Integer = If(side = "LONG", r.EffectiveLongScore, r.EffectiveShortScore)
        If score >= CInt(Math.Ceiling(r.MaxScore * cfg.Scoring.VerdictStrongPct)) Then Return "STRONG"
        If score >= CInt(Math.Ceiling(r.MaxScore * cfg.Scoring.VerdictMedPct)) Then Return "MEDIUM"
        If score >= CInt(Math.Ceiling(r.MaxScore * cfg.Scoring.VerdictWeakPct)) Then Return "WEAK"
        Return "BELOW WEAK"
    End Function

    ' The fields ComputeSideLevels reads, from the logged row plus the recomputed POC and label
    ' (the WhatIfReplay.BuildIndicator field set, with the POC tier now open to measurement).
    Private Function PgIndicators(row As CsvRow, poc As Double, signal As String) As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = row.Price
        r.ATR = row.ATR
        r.Regime = row.Regime
        r.ExecResolution = row.ExecResolution
        r.SessionUtcHour = row.Timestamp.Hour
        r.SwingTargetLong = row.SwingTargetLong
        r.SwingTargetShort = row.SwingTargetShort
        r.SwingStopLong = row.SwingStopLong
        r.SwingStopShort = row.SwingStopShort
        r.VPFRNearestHvnAbove = row.VpfrNearestHvnAbove
        r.VPFRNearestHvnBelow = row.VpfrNearestHvnBelow
        r.VPFRPoc = poc
        r.VPFRSignal = signal
        r.BestPivotByVolume5m = row.BestPivotByVolume5m
        Return r
    End Function

    Private Function PgWindow(bars As PgBars, iPrev As Integer, nClosed As Integer, forming As Integer,
                              formOpen As Long, price As Double, iForm As Integer) As List(Of Candle)
        Dim list As New List(Of Candle)(nClosed + 1)
        For i As Integer = iPrev - nClosed + 1 To iPrev
            list.Add(bars.Bars(i))
        Next
        If forming = 1 Then
            list.Add(New Candle With {.Timestamp = formOpen, .Open = price, .High = price, .Low = price, .Close = price, .Volume = 0, .VolumeUSD = 0})
        ElseIf forming = 2 Then
            list.Add(bars.Bars(iForm))
        End If
        Return list
    End Function

    Private Sub PgLoadValueArea(path As String, va As Dictionary(Of DateTime, Tuple(Of Double, Double)))
        Dim iTs As Integer = -1, iVah As Integer = -1, iVal As Integer = -1
        Dim first As Boolean = True
        For Each line In File.ReadLines(path)
            Dim p = line.Split(","c)
            If first Then
                first = False
                iTs = Array.FindIndex(p, Function(h) h.Trim() = "Timestamp")
                iVah = Array.FindIndex(p, Function(h) h.Trim() = "VPFRVAH")
                iVal = Array.FindIndex(p, Function(h) h.Trim() = "VPFRVAL")
                If iTs < 0 OrElse iVah < 0 OrElse iVal < 0 Then Throw New InvalidOperationException("VPFRVAH or VPFRVAL column missing in " & path & ". STOP.")
                Continue For
            End If
            If p.Length <= Math.Max(iTs, Math.Max(iVah, iVal)) Then Continue For
            Dim ts As DateTime
            If Not DateTime.TryParseExact(p(iTs).Trim(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, ts) Then Continue For
            Dim vah As Double, vaLow As Double
            If Not Double.TryParse(p(iVah), NumberStyles.Float, Inv, vah) Then Continue For
            If Not Double.TryParse(p(iVal), NumberStyles.Float, Inv, vaLow) Then Continue For
            If Not va.ContainsKey(ts) Then va(ts) = Tuple.Create(vah, vaLow)
        Next
    End Sub

    Private Sub PgReadCandles(path As String, map As Dictionary(Of Long, Candle))
        For Each line In File.ReadLines(path).Skip(1)
            If String.IsNullOrWhiteSpace(line) Then Continue For
            Dim p = line.Split(","c)
            If p.Length < 6 Then Continue For
            Dim c As New Candle With {
                .Timestamp = Long.Parse(p(0), Inv),
                .Open = Double.Parse(p(1), NumberStyles.Float, Inv),
                .High = Double.Parse(p(2), NumberStyles.Float, Inv),
                .Low = Double.Parse(p(3), NumberStyles.Float, Inv),
                .Close = Double.Parse(p(4), NumberStyles.Float, Inv),
                .Volume = Double.Parse(p(5), NumberStyles.Float, Inv),
                .VolumeUSD = If(p.Length > 6, Double.Parse(p(6), NumberStyles.Float, Inv), 0.0)}
            map(c.Timestamp) = c
        Next
    End Sub

    Private Async Function PgLoadBarsAsync(root As String, cacheDir As String, res As Integer, fromMs As Long, toMs As Long,
                                           log As StringBuilder) As Task(Of PgBars)
        Dim resMs As Long = res * 60000L
        Dim map As New Dictionary(Of Long, Candle)()

        ' The backtest store, read-only, by month.
        Dim storeDir As String = Path.Combine(root, "backtest_data")
        Dim fromDt As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(fromMs).UtcDateTime
        Dim toDt As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(toMs).UtcDateTime
        Dim m As New DateTime(fromDt.Year, fromDt.Month, 1)
        Dim storeFiles As New List(Of String)()
        While m <= toDt
            Dim f As String = Path.Combine(storeDir, "candles_" & res.ToString(Inv) & "m_" & m.ToString("yyyy-MM", Inv) & ".csv")
            If File.Exists(f) Then
                PgReadCandles(f, map)
                storeFiles.Add(Path.GetFileName(f))
            End If
            m = m.AddMonths(1)
        End While
        Dim nStore As Integer = map.Count

        ' This instrument's cache: bars fetched on an earlier run.
        Dim cachePath As String = Path.Combine(cacheDir, "pocgate_candles_" & res.ToString(Inv) & "m.csv")
        Dim fetchedMap As New Dictionary(Of Long, Candle)()
        If File.Exists(cachePath) Then PgReadCandles(cachePath, fetchedMap)
        Dim nCache As Integer = 0
        For Each kv In fetchedMap
            If Not map.ContainsKey(kv.Key) Then
                map(kv.Key) = kv.Value
                nCache += 1
            End If
        Next

        ' Fetch everything after the newest bar held, up to toMs (HistoricalStore chunking: 4000 bars).
        Dim nFetched As Integer = 0
        Dim newest As Long = If(map.Count > 0, map.Keys.Max(), fromMs - resMs)
        If newest + resMs <= toMs Then
            Dim cursor As Long = newest + resMs
            Dim chunkMs As Long = resMs * 4000L
            While cursor <= toMs
                Dim chunkEnd As Long = Math.Min(toMs, cursor + chunkMs - 1)
                Dim chunk = Await DeribitClient.GetCandlesAsync(res.ToString(Inv), cursor, chunkEnd)
                If chunk Is Nothing Then Throw New InvalidOperationException("candle fetch failed: resolution " & res.ToString(Inv) & " from " & cursor.ToString(Inv) & ". STOP.")
                For Each c In chunk
                    If c.Timestamp >= cursor AndAlso c.Timestamp <= chunkEnd AndAlso Not map.ContainsKey(c.Timestamp) Then
                        map(c.Timestamp) = c
                        fetchedMap(c.Timestamp) = c
                        nFetched += 1
                    End If
                Next
                cursor = chunkEnd + 1
                Await Task.Delay(200)
            End While
            Dim sb As New StringBuilder()
            sb.AppendLine("Timestamp,Open,High,Low,Close,Volume,Cost")
            For Each kv In fetchedMap.OrderBy(Function(k) k.Key)
                Dim c = kv.Value
                sb.AppendLine(String.Join(",", c.Timestamp.ToString(Inv), c.Open.ToString("R", Inv), c.High.ToString("R", Inv),
                                          c.Low.ToString("R", Inv), c.Close.ToString("R", Inv), c.Volume.ToString("R", Inv), c.VolumeUSD.ToString("R", Inv)))
            Next
            File.WriteAllText(cachePath, sb.ToString())
        End If

        Dim keys = map.Keys.OrderBy(Function(k) k).ToArray()
        Dim result As New PgBars With {.OpenMs = keys, .Bars = keys.Select(Function(k) map(k)).ToArray()}
        Dim expected As Long = (toMs - fromMs) \ resMs + 1
        Dim present As Long = keys.Where(Function(k) k >= fromMs AndAlso k <= toMs).LongCount()
        log.AppendLine(String.Format(Inv, "| {0}m | {1} | {2} | {3} | {4} | {5} of {6} |", res, If(storeFiles.Count = 0, "none", String.Join(", ", storeFiles)),
                                     nStore, nCache, nFetched, expected - present, expected))
        Return result
    End Function

End Module
