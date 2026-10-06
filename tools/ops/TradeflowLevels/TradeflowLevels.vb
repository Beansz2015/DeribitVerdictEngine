' tools/ops/TradeflowLevels/TradeflowLevels.vb
'
' The placed-level replay for the liquidation x trade-flow flip study, session 2
' (docs/liq-tradeflow-flip-study-spec.md; ruling TFS-8 (d); the feasibility addendum in that spec, section 15).
'
' For each row (a decision instant D, an entry price, the session hour at T0, a side) it rebuilds the inputs that
' SignalEmitter.ComputeSideLevels reads, from candles built from the history store's trade prices, and calls the SHIPPED
' functions. ComputeSideLevels with structural_levels.enabled reads ONLY r + cfg (Core/SignalEmitter.vb: "verdict-
' independent"); the r fields it reads are CurrentPrice, ATR, SessionUtcHour, SwingTarget*/SwingStop*, BestPivotByVolume5m,
' VPFRNearestHvnAbove/Below, VPFRPoc and VPFRSignal. None needs funding, open interest or the order book.
'
' Mapping onto the engine (each line mirrors tools/BacktestRunner/ReplayLoop.vb unless stated):
'   execRes     = ExecutionResolution.ResolveResolution(cfg, hour at T0)   (1 -> 1m series, 3 -> 3m, else 5m)
'   sliceExec   = the last 250 COMPLETED exec candles (open + res <= D);  slice5m = the last 210 completed 5m candles.
'                 Completed bars only = ReplayLoop's useFormingStub:=False arm (D is not on a bar close; the shipped stub
'                 window [close, close + 2 s] has no meaning at an arbitrary instant). Spec section 4.8 reads "completed".
'   skip gate   = ReplayLoop's: slice1m < 50 or slice5m < 30 or (execRes <> 1 and sliceExec < 50) -> status few_candles.
'   CurrentPrice = the entry trade's price (the price the engine would place around at D; live = the forming close).
'   ATR         = IndicatorEngine.CalcATR(sliceExec, indicators.ATR.period); 0 -> status atr_zero.
'   swings      = CalcSwingPivots(slice5m, ...) and the four Swing* fields exactly as ReplayLoop lines 593-596.
'   VPFR        = CalcVPFRLite(sliceExec, CurrentPrice, ...), cfg values as ReplayLoop.
'   levels      = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cfg, isLong).
' The ATR-fallback geometry of spec section 4.8 (TFS-8 (c), descriptive): ATR(7) over the last 50 completed exec
' candles; target = entry +/- ResolveFallbackTargetMultiplier(cfg, hour) x ATR50; stop = entry -/+ atr_stop_multiplier x ATR50.
'
' Slicing uses a binary search over the timestamp array (ReplayLoop.SliceCandlesAtOrBefore scans from the end; same rule,
' "open + res <= D", same "last N").
'
' Modes:
'   levels   --settings <copy of settings.json> --candles <dir with candles_1m.csv, candles_3m.csv, candles_5m.csv>
'            --rows <csv: key,d_ms,entry_px,hour,is_long> --out <csv>
'   compare  --settings <copy> --candles <tape dir> --venue <backtest_data> --months 2026-01,..  --from yyyy-MM-dd
'            --to yyyy-MM-dd --out <txt> [--instants <csv: d_ms,hour,is_long>]
'            (tape-built vs venue candles, and the levels each places on a 15-min grid and at given instants)
'   selftest --settings <copy>

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq

Public Module TradeflowLevelsProgram

    Private ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture
    Public Const ExecCount As Integer = 250      ' ReplayLoop.Candles1mCount / CandlesExecCount
    Public Const FiveCount As Integer = 210      ' ReplayLoop.Candles5mCount
    Public Const AtrFallbackBars As Integer = 50 ' spec section 4.8: the last 50 completed exec candles

    Public Class Series
        Public Res As Integer
        Public Ts As Long()
        Public Bars As List(Of Candle)
        Public Sub New(res As Integer, bars As List(Of Candle))
            Me.Res = res
            Me.Bars = bars
            Me.Ts = bars.Select(Function(c) c.Timestamp).ToArray()
        End Sub
        ''' <summary>The last n bars whose CLOSE (open + res) is at or before dMs.</summary>
        Public Function Completed(dMs As Long, n As Integer) As List(Of Candle)
            Dim resMs As Long = CLng(Res) * 60000L
            ' largest i with Ts(i) + resMs <= dMs  <=>  Ts(i) <= dMs - resMs
            Dim lim As Long = dMs - resMs
            Dim lo As Integer = 0, hi As Integer = Ts.Length - 1, endIdx As Integer = -1
            While lo <= hi
                Dim mid As Integer = (lo + hi) \ 2
                If Ts(mid) <= lim Then
                    endIdx = mid : lo = mid + 1
                Else
                    hi = mid - 1
                End If
            End While
            If endIdx < 0 Then Return New List(Of Candle)()
            Dim startIdx As Integer = Math.Max(0, endIdx - n + 1)
            Return Bars.GetRange(startIdx, endIdx - startIdx + 1)
        End Function
    End Class

    Public Class Placed
        Public Status As String = "ok"
        Public ExecRes As Integer
        Public NExec As Integer
        Public N5 As Integer
        Public Atr As Double
        Public Target As Double
        Public StopPx As Double
        Public TargetReason As String = ""
        Public StopReason As String = ""
        Public CStatus As String = "ok"
        Public Atr50 As Double
        Public CTarget As Double
        Public CStop As Double
        Public FbMult As Double
    End Class

    Public Function Place(cfg As EngineSettings, s1 As Series, s3 As Series, s5 As Series,
                          dMs As Long, entryPx As Double, hour As Integer, isLong As Boolean) As Placed
        Dim p As New Placed()
        Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, hour)
        If execRes <= 0 Then execRes = 1
        p.ExecRes = execRes
        Dim sExec As Series = If(execRes = 1, s1, If(execRes = 3, s3, s5))
        Dim slice1m = s1.Completed(dMs, ExecCount)
        Dim sliceExec = sExec.Completed(dMs, ExecCount)
        Dim slice5m = s5.Completed(dMs, FiveCount)
        p.NExec = sliceExec.Count : p.N5 = slice5m.Count
        p.FbMult = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, hour)
        Dim sgn As Double = If(isLong, 1.0, -1.0)

        ' ---- (c) the ATR-fallback geometry, spec section 4.8
        Dim slice50 = sExec.Completed(dMs, AtrFallbackBars)
        If slice50.Count < AtrFallbackBars Then
            p.CStatus = "few_candles"
        Else
            p.Atr50 = IndicatorEngine.CalcATR(slice50, cfg.Indicators.ATR.Period)
            If p.Atr50 <= 0 Then
                p.CStatus = "atr_zero"
            Else
                p.CTarget = entryPx + sgn * p.FbMult * p.Atr50
                p.CStop = entryPx - sgn * cfg.Scoring.AtrStopMultiplier * p.Atr50
            End If
        End If

        ' ---- (d) the engine's placed levels
        If slice1m.Count < 50 OrElse slice5m.Count < 30 OrElse (execRes <> 1 AndAlso sliceExec.Count < 50) Then
            p.Status = "few_candles"
            Return p
        End If
        Dim r As New IndicatorResults()
        r.ExecResolution = execRes
        r.SessionUtcHour = hour
        r.CurrentPrice = entryPx
        r.ATR = IndicatorEngine.CalcATR(sliceExec, cfg.Indicators.ATR.Period)
        p.Atr = r.ATR
        If r.ATR <= 0 Then
            p.Status = "atr_zero"
            Return p
        End If
        IndicatorEngine.CalcSwingPivots(slice5m,
                                         r.LastSwingHigh5m, r.LastSwingLow5m,
                                         pivotWing:=cfg.Indicators.Swing.PivotWing5m,
                                         lookbackBars:=cfg.Indicators.Swing.LookbackBars5m,
                                         bestPivotByVolume:=r.BestPivotByVolume5m,
                                         bestPivotVolumeRatio:=r.BestPivotVolumeRatio5m,
                                         bestPivotIsHigh:=r.BestPivotIsHigh5m)
        r.SwingTargetLong = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)
        r.SwingStopLong = If(r.LastSwingLow5m < r.CurrentPrice AndAlso r.LastSwingLow5m > 0, r.LastSwingLow5m, 0)
        r.SwingTargetShort = If(r.LastSwingLow5m < r.CurrentPrice AndAlso r.LastSwingLow5m > 0, r.LastSwingLow5m, 0)
        r.SwingStopShort = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)

        Dim vpfrPoc As Double = 0
        Dim vpfrHVNearPoc As Boolean = False
        Dim vpfrSignal As String = "NEUTRAL"
        Dim vpfrBucketVols() As Double = Array.Empty(Of Double)()
        Dim vpfrBucketLow As Double = 0
        Dim vpfrBucketSize As Double = 0
        IndicatorEngine.CalcVPFRLite(sliceExec, r.CurrentPrice,
                                     vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                                     r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
                                     r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
                                     r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
                                     vpfrBucketVols, vpfrBucketLow, vpfrBucketSize,
                                     numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                                     hvnVolPct:=cfg.Indicators.VPFR.HvnVolPct,
                                     lvnVolPct:=cfg.Indicators.VPFR.LvnVolPct,
                                     hvnProximityPct:=cfg.Indicators.VPFR.HvnProximityPct,
                                     decayBase:=cfg.Indicators.VPFR.DecayBase,
                                     valueAreaPct:=cfg.Indicators.VPFR.ValueAreaPct)
        r.VPFRPoc = vpfrPoc
        r.VPFRHVNearPoc = vpfrHVNearPoc
        r.VPFRSignal = vpfrSignal

        Dim lv = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cfg, isLong)
        p.Target = lv.Target
        p.StopPx = lv.StopPx
        p.TargetReason = If(lv.TargetReason, "")
        p.StopReason = If(lv.StopReason, "")
        Return p
    End Function

    ' ------------------------------------------------------------------ IO
    Public Function LoadCandleFile(path As String, Optional fromMs As Long = Long.MinValue, Optional toMs As Long = Long.MaxValue) As List(Of Candle)
        Dim out As New List(Of Candle)()
        Using sr As New StreamReader(path)
            Dim hdr = sr.ReadLine()
            If hdr Is Nothing OrElse Not hdr.StartsWith("Timestamp,Open,High,Low,Close,Volume") Then
                Throw New InvalidDataException("unexpected candle header in " & path & ": " & hdr)
            End If
            Dim line As String = sr.ReadLine()
            While line IsNot Nothing
                Dim f = line.Split(","c)
                Dim ts As Long = Long.Parse(f(0), Inv)
                If ts >= fromMs AndAlso ts < toMs Then
                    out.Add(New Candle With {.Timestamp = ts,
                        .Open = Double.Parse(f(1), Inv), .High = Double.Parse(f(2), Inv),
                        .Low = Double.Parse(f(3), Inv), .Close = Double.Parse(f(4), Inv),
                        .Volume = Double.Parse(f(5), Inv), .VolumeUSD = If(f.Length > 6, Double.Parse(f(6), Inv), 0)})
                End If
                line = sr.ReadLine()
            End While
        End Using
        For i As Integer = 1 To out.Count - 1
            If out(i).Timestamp <= out(i - 1).Timestamp Then Throw New InvalidDataException("candles not strictly ascending in " & path)
        Next
        Return out
    End Function

    Private Function Arg(args As String(), name As String, Optional def As String = Nothing) As String
        For i As Integer = 0 To args.Length - 2
            If args(i) = name Then Return args(i + 1)
        Next
        Return def
    End Function

    Private Function LoadCfg(path As String) As EngineSettings
        SettingsLoader.Initialise(path)
        Dim cfg = SettingsLoader.Current
        If Not String.IsNullOrEmpty(SettingsLoader.LastLoadError) Then
            Throw New InvalidDataException("settings load error: " & SettingsLoader.LastLoadError)
        End If
        If SettingsLoader.OverlayActive Then Throw New InvalidDataException("a settings.local.json overlay is active beside " & path)
        Return cfg
    End Function

    Private Function F(x As Double) As String
        Return x.ToString("R", Inv)
    End Function

    Public Function Main(args As String()) As Integer
        If args.Length = 0 Then
            Console.WriteLine("usage: TradeflowLevels levels|compare|selftest --settings <copy> ...")
            Return 2
        End If
        Dim cfg = LoadCfg(Arg(args, "--settings"))
        Select Case args(0)
            Case "levels" : Return RunLevels(cfg, args)
            Case "compare" : Return RunCompare(cfg, args)
            Case "selftest" : Return RunSelftest(cfg)
        End Select
        Console.WriteLine("unknown mode " & args(0))
        Return 2
    End Function

    Private Function RunLevels(cfg As EngineSettings, args As String()) As Integer
        Dim dir = Arg(args, "--candles")
        Dim s1 As New Series(1, LoadCandleFile(Path.Combine(dir, "candles_1m.csv")))
        Dim s3 As New Series(3, LoadCandleFile(Path.Combine(dir, "candles_3m.csv")))
        Dim s5 As New Series(5, LoadCandleFile(Path.Combine(dir, "candles_5m.csv")))
        Console.WriteLine(String.Format(Inv, "candles: 1m {0}  3m {1}  5m {2}", s1.Bars.Count, s3.Bars.Count, s5.Bars.Count))
        Dim n As Integer = 0
        Dim st As New Dictionary(Of String, Integer)()
        Using sr As New StreamReader(Arg(args, "--rows")), sw As New StreamWriter(Arg(args, "--out"))
            sw.NewLine = vbLf
            sr.ReadLine()
            sw.WriteLine("key,status,exec_res,n_exec,n_5m,atr,target,stop,target_reason,stop_reason,c_status,atr50,c_target,c_stop,fb_mult")
            Dim line = sr.ReadLine()
            While line IsNot Nothing
                Dim fl = line.Split(","c)
                Dim p = Place(cfg, s1, s3, s5, Long.Parse(fl(1), Inv), Double.Parse(fl(2), Inv), Integer.Parse(fl(3), Inv), fl(4) = "1")
                sw.WriteLine(String.Join(",", fl(0), p.Status, p.ExecRes.ToString(Inv), p.NExec.ToString(Inv), p.N5.ToString(Inv),
                                         F(p.Atr), F(p.Target), F(p.StopPx), p.TargetReason, p.StopReason,
                                         p.CStatus, F(p.Atr50), F(p.CTarget), F(p.CStop), F(p.FbMult)))
                Dim k = p.Status & "/" & p.CStatus
                st(k) = If(st.ContainsKey(k), st(k), 0) + 1
                n += 1
                line = sr.ReadLine()
            End While
        End Using
        Console.WriteLine(String.Format(Inv, "rows placed: {0}  status (d)/(c): {1}", n,
                          String.Join("  ", st.OrderBy(Function(kv) kv.Key).Select(Function(kv) kv.Key & "=" & kv.Value.ToString(Inv)))))
        Return 0
    End Function

    ' ------------------------------------------------------------------ compare: tape-built vs venue candles
    Private Function Pct(sorted As List(Of Double), q As Double) As Double
        If sorted.Count = 0 Then Return Double.NaN
        Dim k As Integer = Math.Max(1, Math.Min(sorted.Count, CInt(Math.Ceiling(q / 100.0 * sorted.Count))))
        Return sorted(k - 1)
    End Function

    Private Function RunCompare(cfg As EngineSettings, args As String()) As Integer
        Dim tape = Arg(args, "--candles"), venue = Arg(args, "--venue")
        Dim months = Arg(args, "--months").Split(","c)
        Dim fromMs = New DateTimeOffset(DateTime.ParseExact(Arg(args, "--from"), "yyyy-MM-dd", Inv), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim toMs = New DateTimeOffset(DateTime.ParseExact(Arg(args, "--to"), "yyyy-MM-dd", Inv), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim outLines As New List(Of String)()
        Dim tapeS As New Dictionary(Of Integer, Series)(), venS As New Dictionary(Of Integer, Series)()
        For Each res In {1, 3, 5}
            tapeS(res) = New Series(res, LoadCandleFile(Path.Combine(tape, String.Format(Inv, "candles_{0}m.csv", res))))
            Dim vb As New List(Of Candle)()
            For Each m In months
                Dim pth = Path.Combine(venue, String.Format(Inv, "candles_{0}m_{1}.csv", res, m))
                If File.Exists(pth) Then vb.AddRange(LoadCandleFile(pth))
            Next
            vb = vb.GroupBy(Function(c) c.Timestamp).Select(Function(g) g.First()).OrderBy(Function(c) c.Timestamp).ToList()
            venS(res) = New Series(res, vb)
            ' ---- candle parity over [from, to)
            Dim tIdx As New Dictionary(Of Long, Candle)()
            For Each c In tapeS(res).Bars
                If c.Timestamp >= fromMs AndAlso c.Timestamp < toMs Then tIdx(c.Timestamp) = c
            Next
            Dim nV = 0, nMatch = 0, nMiss = 0, nOhlcEq = 0, nCloseEq = 0
            Dim dH As New List(Of Double)(), dL As New List(Of Double)(), dC As New List(Of Double)(), vr As New List(Of Double)()
            For Each v In vb
                If v.Timestamp < fromMs OrElse v.Timestamp >= toMs Then Continue For
                nV += 1
                Dim t As Candle = Nothing
                If Not tIdx.TryGetValue(v.Timestamp, t) Then nMiss += 1 : Continue For
                nMatch += 1
                If t.Open = v.Open AndAlso t.High = v.High AndAlso t.Low = v.Low AndAlso t.Close = v.Close Then nOhlcEq += 1
                If t.Close = v.Close Then nCloseEq += 1
                dH.Add(Math.Abs(t.High - v.High) / v.Close * 10000.0)
                dL.Add(Math.Abs(t.Low - v.Low) / v.Close * 10000.0)
                dC.Add(Math.Abs(t.Close - v.Close) / v.Close * 10000.0)
                If v.Volume > 0 Then vr.Add(t.Volume / v.Volume)
            Next
            dH.Sort() : dL.Sort() : dC.Sort() : vr.Sort()
            outLines.Add(String.Format(Inv,
                "candles {0}m: venue {1}  matched {2}  venue bar with no tape bar {3}  tape bars in range {4}  OHLC identical {5} ({6:0.000})  close identical {7} ({8:0.000})",
                res, nV, nMatch, nMiss, tIdx.Count, nOhlcEq, nOhlcEq / Math.Max(1.0, nMatch), nCloseEq, nCloseEq / Math.Max(1.0, nMatch)))
            outLines.Add(String.Format(Inv,
                "  |diff| bps of close: high p50/p99/max {0:0.000}/{1:0.000}/{2:0.000}  low {3:0.000}/{4:0.000}/{5:0.000}  close {6:0.000}/{7:0.000}/{8:0.000}  volume ratio tape/venue p1/p50/p99 {9:0.000}/{10:0.000}/{11:0.000}",
                Pct(dH, 50), Pct(dH, 99), If(dH.Count > 0, dH.Last(), Double.NaN), Pct(dL, 50), Pct(dL, 99), If(dL.Count > 0, dL.Last(), Double.NaN),
                Pct(dC, 50), Pct(dC, 99), If(dC.Count > 0, dC.Last(), Double.NaN), Pct(vr, 1), Pct(vr, 50), Pct(vr, 99)))
        Next
        ' ---- levels from each source; entry = the last completed tape 1m close at the instant (a price BEFORE it)
        Dim grid As New List(Of (T As Long, Hour As Integer, Sides As Boolean()))()
        Dim tq As Long = fromMs
        While tq < toMs
            grid.Add((tq, DateTimeOffset.FromUnixTimeMilliseconds(tq).UtcDateTime.Hour, New Boolean() {True, False}))
            tq += 15L * 60000L
        End While
        CompareLevels(cfg, tapeS, venS, grid, "levels at 15-min instants x 2 sides", outLines)
        Dim instPath = Arg(args, "--instants")
        If instPath IsNot Nothing Then
            Dim ev As New List(Of (T As Long, Hour As Integer, Sides As Boolean()))()
            For Each line In File.ReadAllLines(instPath).Skip(1)
                Dim fl = line.Split(","c)
                Dim tt = Long.Parse(fl(0), Inv)
                If tt >= fromMs AndAlso tt < toMs Then ev.Add((tt, Integer.Parse(fl(1), Inv), New Boolean() {fl(2) = "1"}))
            Next
            CompareLevels(cfg, tapeS, venS, ev, "levels at the event decision instants D (hour at T0, the traded side)", outLines)
        End If
        For Each l In outLines
            Console.WriteLine(l)
        Next
        Dim outPath = Arg(args, "--out")
        If outPath IsNot Nothing Then File.WriteAllLines(outPath, outLines)
        Return 0
    End Function

    Private Sub CompareLevels(cfg As EngineSettings, tapeS As Dictionary(Of Integer, Series), venS As Dictionary(Of Integer, Series),
                              inst As List(Of (T As Long, Hour As Integer, Sides As Boolean())), title As String, outLines As List(Of String))
        Dim nInst = 0, nBoth = 0, nSkip = 0
        Dim sameT = 0, sameS = 0, bothSame = 0
        Dim dT As New List(Of Double)(), dS As New List(Of Double)(), atrR As New List(Of Double)()
        Dim reasonsT As New Dictionary(Of String, Integer)()
        For Each it In inst
            Dim last = tapeS(1).Completed(it.T, 1)
            If last.Count <> 1 Then Continue For
            Dim px As Double = last(0).Close
            For Each isLong In it.Sides
                nInst += 1
                Dim a = Place(cfg, tapeS(1), tapeS(3), tapeS(5), it.T, px, it.Hour, isLong)
                Dim b = Place(cfg, venS(1), venS(3), venS(5), it.T, px, it.Hour, isLong)
                If a.Status <> "ok" OrElse b.Status <> "ok" Then nSkip += 1 : Continue For
                nBoth += 1
                If a.TargetReason = b.TargetReason Then sameT += 1
                If a.StopReason = b.StopReason Then sameS += 1
                If a.TargetReason = b.TargetReason AndAlso a.StopReason = b.StopReason Then bothSame += 1
                Dim k = "tape " & a.TargetReason & " / venue " & b.TargetReason
                reasonsT(k) = If(reasonsT.ContainsKey(k), reasonsT(k), 0) + 1
                dT.Add(Math.Abs(a.Target - b.Target) / px * 10000.0)
                dS.Add(Math.Abs(a.StopPx - b.StopPx) / px * 10000.0)
                atrR.Add(a.Atr / b.Atr)
            Next
        Next
        dT.Sort() : dS.Sort() : atrR.Sort()
        outLines.Add(String.Format(Inv, "{0}: {1}  placed by both {2}  skipped (a source failed the candle gate) {3}", title, nInst, nBoth, nSkip))
        outLines.Add(String.Format(Inv, "  same target reason {0} ({1:0.000})  same stop reason {2} ({3:0.000})  both same {4} ({5:0.000})",
                                   sameT, sameT / Math.Max(1.0, nBoth), sameS, sameS / Math.Max(1.0, nBoth), bothSame, bothSame / Math.Max(1.0, nBoth)))
        outLines.Add(String.Format(Inv, "  |target tape - venue| bps p50/p90/p99/max {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00}   |stop| p50/p90/p99/max {4:0.00}/{5:0.00}/{6:0.00}/{7:0.00}",
                                   Pct(dT, 50), Pct(dT, 90), Pct(dT, 99), If(dT.Count > 0, dT.Last(), Double.NaN),
                                   Pct(dS, 50), Pct(dS, 90), Pct(dS, 99), If(dS.Count > 0, dS.Last(), Double.NaN)))
        outLines.Add(String.Format(Inv, "  share |target diff| <= 0.5 bps {0:0.000}   share |stop diff| <= 0.5 bps {1:0.000}   ATR tape/venue p1/p50/p99 {2:0.000}/{3:0.000}/{4:0.000}",
                                   dT.Where(Function(x) x <= 0.5).Count() / Math.Max(1.0, dT.Count), dS.Where(Function(x) x <= 0.5).Count() / Math.Max(1.0, dS.Count),
                                   Pct(atrR, 1), Pct(atrR, 50), Pct(atrR, 99)))
        outLines.Add("  target reason cross-tab (tape / venue): " & String.Join("  ", reasonsT.OrderByDescending(Function(kv) kv.Value).Select(Function(kv) kv.Key & "=" & kv.Value.ToString(Inv))))
    End Sub

    ' ------------------------------------------------------------------ selftest
    Private Function RunSelftest(cfg As EngineSettings) As Integer
        Dim fails As New List(Of String)()
        Dim check = Sub(name As String, ok As Boolean, detail As String)
                        Console.WriteLine(String.Format(Inv, "  {0,-72} {1}  {2}", name, If(ok, "PASS", "FAIL"), detail))
                        If Not ok Then fails.Add(name)
                    End Sub
        ' 900 one-minute bars from 2025-01-01 00:00 UTC: O=C=100, H=101, L=99 (TR = 2 every bar), volume 1.
        Dim t0 As Long = New DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim mk = Function(res As Integer, n As Integer) As List(Of Candle)
                     Dim l As New List(Of Candle)()
                     For i As Integer = 0 To n - 1
                         l.Add(New Candle With {.Timestamp = t0 + CLng(i) * res * 60000L, .Open = 100, .High = 101, .Low = 99, .Close = 100, .Volume = 1, .VolumeUSD = 100})
                     Next
                     Return l
                 End Function
        Dim s1 As New Series(1, mk(1, 900)), s3 As New Series(3, mk(3, 300)), s5 As New Series(5, mk(5, 180))
        ' D = 14:00:30 -> the 1m bar 14:00 is NOT complete; the last completed 1m bar opens 13:59.
        Dim d As Long = t0 + (14L * 60L) * 60000L + 30000L
        Dim lastC = s1.Completed(d, 250)
        check("slice: last completed 1m bar opens at D - 90 s (forming bar excluded)", lastC.Last().Timestamp = d - 90000L, lastC.Last().Timestamp.ToString(Inv))
        check("slice: a bar closing exactly at D is included", s1.Completed(t0 + 120000L, 5).Last().Timestamp = t0 + 60000L, "")
        check("slice: count capped at 250", lastC.Count = 250, lastC.Count.ToString(Inv))
        ' NY hour 14 -> 1m exec; ASIA hour 3 and LONDON hour 9 -> 3m exec (settings session_volume)
        Dim ny = Place(cfg, s1, s3, s5, d, 200, 14, True)
        Dim asia = Place(cfg, s1, s3, s5, d, 200, 3, True)
        Dim lon = Place(cfg, s1, s3, s5, d, 200, 9, False)
        check("exec resolution: NY 1, ASIA 3, LONDON 3", ny.ExecRes = 1 AndAlso asia.ExecRes = 3 AndAlso lon.ExecRes = 3,
              String.Format(Inv, "{0}/{1}/{2}", ny.ExecRes, asia.ExecRes, lon.ExecRes))
        check("ATR of a constant TR = 2 series is 2", Math.Abs(ny.Atr - 2.0) < 1e-12 AndAlso Math.Abs(asia.Atr - 2.0) < 1e-12, F(ny.Atr))
        ' entry 200 sits above every bar: no structural long target -> FALLBACK_ATR at entry + mult x ATR
        check("NY long: FALLBACK_ATR target = 200 + 1.75 x 2", ny.TargetReason = "FALLBACK_ATR" AndAlso Math.Abs(ny.Target - 203.5) < 1e-9, ny.TargetReason & " " & F(ny.Target))
        check("ASIA long: FALLBACK_ATR target = 200 + 1.25 x 2", asia.TargetReason = "FALLBACK_ATR" AndAlso Math.Abs(asia.Target - 202.5) < 1e-9, asia.TargetReason & " " & F(asia.Target))
        check("NY long: stop 1.6 x ATR below entry (swing low 99 is wider: clamped)", Math.Abs(ny.StopPx - 196.8) < 1e-9, ny.StopReason & " " & F(ny.StopPx))
        check("LONDON short: stop 1.6 x ATR ABOVE entry", Math.Abs(lon.StopPx - 203.2) < 1e-9 AndAlso lon.StopPx > 200, lon.StopReason & " " & F(lon.StopPx))
        check("LONDON short: target BELOW entry", lon.Target < 200, lon.TargetReason & " " & F(lon.Target))
        check("(c) geometry ASIA long: 200 + 1.25 x ATR50, stop 200 - 1.6 x ATR50", Math.Abs(asia.CTarget - 202.5) < 1e-9 AndAlso Math.Abs(asia.CStop - 196.8) < 1e-9,
              F(asia.CTarget) & " " & F(asia.CStop))
        ' a confirmed 5m swing high at 105, 10 bars back: the Swing* mapping must hand it to the arbitration
        Dim b5 = mk(5, 180)
        Dim d5 As Long = t0 + 180L * 5L * 60000L        ' every 5m bar complete
        b5(169).High = 105
        Dim s5p As New Series(5, b5)
        Dim swL = Place(cfg, s1, s3, s5p, d5, 100, 14, True)
        Dim swS = Place(cfg, s1, s3, s5p, d5, 100, 14, False)
        check("swing high 105 above entry 100 (dist 5 <= 3.5 x ATR 2): long target SWING_HIGH_5M 105", swL.TargetReason = "SWING_HIGH_5M" AndAlso swL.Target = 105,
              swL.TargetReason & " " & F(swL.Target))
        check("same swing high is the short's stop side: 5 > 1.6 x 2 -> STOP_CLAMPED at 103.2", swS.StopReason = "STOP_CLAMPED" AndAlso Math.Abs(swS.StopPx - 103.2) < 1e-9,
              swS.StopReason & " " & F(swS.StopPx))
        ' too early: fewer than 50 completed 3m bars before D
        Dim early = Place(cfg, s1, s3, s5, t0 + 100L * 60000L, 200, 3, True)
        check("few candles: ASIA at 01:40 has 33 completed 3m bars -> few_candles in both arms", early.Status = "few_candles" AndAlso early.CStatus = "few_candles",
              early.Status & "/" & early.CStatus)
        Console.WriteLine(String.Format(Inv, "SELFTEST {0} ({1} failure(s))", If(fails.Count = 0, "PASS", "FAIL"), fails.Count))
        Return If(fails.Count = 0, 0, 1)
    End Function

End Module
