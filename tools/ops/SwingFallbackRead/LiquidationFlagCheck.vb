Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/LiquidationFlagCheck.vb
'
' --mode liqflag: evidence for the liquidation findings of the MEDIUM-tier bug hunt label-consumer
' audit (docs/medium-tier-bug-hunt-2026-09-16.md, section R of the resumed session). The default mode
' and every other mode are untouched.
'
'   L-1  On the live WebSocket path the liquidation flag never reaches CalcLiquidations. The trade
'        store the collector writes holds each liquidation twice: the streamed copy (appended first)
'        with flag "none", and a later REST copy with the real flag. Collector rows whose 500-trade
'        window holds a liquidation still log LiqSignal NONE and zero liquidation size.
'   L-2  CalcLiquidations (Core/Indicators_OrderFlow.vb:266-274) puts a liquidation on the TAKER's
'        side. Deribit's `liquidation` flag "M" marks the MAKER side as liquidated and `direction`
'        is the taker's (public/get_last_trades_by_instrument), so an "M" trade lands on the wrong side.
'   L-3  large_liq_size (200) is documented as BTC (docs/UserManual.md lines 1452, 1463) but is compared
'        with sums of `amount`, which Deribit gives in USD for BTC-PERPETUAL. Section 6 of the output
'        shows the store amounts are whole 10 USD contracts.
'
' Inputs (read-only): <fetch>/backtest_data/trades_YYYY-MM.csv (the collector's trade store copy),
' <fetch>/analysis_log.csv.v0.7.bak + analysis_log.csv (the collector's logs) and the merged
' population rows. Window rule: the last 500 trades at or before the row timestamp, after collapsing
' copies of one trade (same timestamp, price, amount, direction) into one record that keeps a flag
' if any copy carries one.
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode liqflag \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Partial Module SwingFallbackReadProgram

    Private Const LfWindow As Integer = 500

    Private Class LfTrade
        Public Ts As Long
        Public Price As String = ""
        Public Amount As Double
        Public Direction As String = ""
        Public Flag As String = "none"
        Public TradeId As String = ""
        Public FileName As String = ""
        Public Line As Integer
    End Class

    Private Class LfRow
        Public Ts As DateTime
        Public Verdict As String = ""
        Public LiqSignal As String = ""
        Public LiqLong As Double
        Public LiqShort As Double
    End Class

    Private Function RunLiqFlag(header As StringBuilder, fetchDir As String, pooledPath As String, bakPath As String, livePath As String,
                                sigs As List(Of Sig), outPath As String) As Integer
        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode liqflag (liquidation flag and attribution evidence)"))
        o.AppendLine("- Read: docs/medium-tier-bug-hunt-2026-09-16.md, resumed session. Deribit field meaning: public/get_last_trades_by_instrument (`direction` = the taker's side; `liquidation` M = maker side liquidated, T = taker side, MT = both).")
        o.AppendLine(String.Format(Inv, "- Window: the last {0} trades at or before the row timestamp, copies of one trade collapsed (a flag survives if any copy carries one).", LfWindow))
        o.AppendLine()

        ' ---- the trade store
        Dim storeDir As String = Path.Combine(fetchDir, "backtest_data")
        Dim files = Directory.GetFiles(storeDir, "trades_*.csv").OrderBy(Function(f) f, StringComparer.Ordinal).ToList()
        If files.Count = 0 Then Throw New InvalidOperationException("no trades_*.csv under " & storeDir & ". STOP.")
        Dim raw As New List(Of LfTrade)()
        For Each f In files
            Dim ln As Integer = 0
            For Each line In File.ReadLines(f)
                ln += 1
                If ln = 1 Then Continue For
                Dim p = line.Split(","c)
                If p.Length < 5 Then Continue For
                Dim ts As Long, amt As Double
                If Not Long.TryParse(p(0), NumberStyles.Integer, Inv, ts) Then Continue For
                If Not Double.TryParse(p(2), NumberStyles.Float, Inv, amt) Then Continue For
                raw.Add(New LfTrade With {.Ts = ts, .Price = p(1), .Amount = amt, .Direction = p(3),
                                          .Flag = If(String.IsNullOrEmpty(p(4)), "none", p(4)),
                                          .TradeId = If(p.Length > 5, p(5), ""), .FileName = Path.GetFileName(f), .Line = ln})
            Next
        Next
        Dim flagged = raw.Where(Function(t) t.Flag <> "none").ToList()

        o.AppendLine("## 1. Trade store")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Files: {0}. Rows: {1}. Span: {2:yyyy-MM-dd HH:mm:ss} to {3:yyyy-MM-dd HH:mm:ss} UTC.",
            String.Join(", ", files.Select(Function(f) Path.GetFileName(f))), raw.Count, LfUtc(raw.Min(Function(t) t.Ts)), LfUtc(raw.Max(Function(t) t.Ts))))
        o.AppendLine()
        o.AppendLine("| Liquidation flag | Taker direction | Rows |")
        o.AppendLine("|---|---|---|")
        For Each grpFlag In flagged.GroupBy(Function(t) t.Flag & "|" & t.Direction).OrderBy(Function(x) x.Key, StringComparer.Ordinal)
            Dim parts = grpFlag.Key.Split("|"c)
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} |", parts(0), parts(1), grpFlag.Count()))
        Next
        o.AppendLine()

        ' ---- copies of one trade (L-1)
        Dim byKey As New Dictionary(Of String, List(Of LfTrade))(StringComparer.Ordinal)
        For Each t In raw
            Dim k As String = LfKey(t)
            Dim lst As List(Of LfTrade) = Nothing
            If Not byKey.TryGetValue(k, lst) Then
                lst = New List(Of LfTrade)()
                byKey(k) = lst
            End If
            lst.Add(t)
        Next
        Dim withTwin As Integer = 0, twinEarlier As Integer = 0, sameId As Integer = 0, idComparable As Integer = 0
        For Each t In flagged
            Dim twins = byKey(LfKey(t)).Where(Function(x) x.Flag = "none").ToList()
            If twins.Count = 0 Then Continue For
            withTwin += 1
            If twins.Any(Function(x) x.FileName = t.FileName AndAlso x.Line < t.Line) Then twinEarlier += 1
            If t.TradeId <> "" AndAlso twins.Any(Function(x) x.TradeId <> "") Then
                idComparable += 1
                If twins.Any(Function(x) x.TradeId = t.TradeId) Then sameId += 1
            End If
        Next
        o.AppendLine("## 2. Copies of one liquidation trade (finding L-1)")
        o.AppendLine()
        o.AppendLine("| Measure | Rows |")
        o.AppendLine("|---|---|")
        o.AppendLine(Row("Liquidation-flagged store rows", flagged.Count))
        o.AppendLine(Row("... with an identical copy (same timestamp, price, amount, direction) whose flag is `none`", withTwin))
        o.AppendLine(Row("... where that `none` copy was appended EARLIER in the same file (the streamed copy)", twinEarlier))
        o.AppendLine(Row("... carrying a trade id on both copies", idComparable))
        o.AppendLine(Row("... where the `none` copy has the SAME trade id", sameId))
        o.AppendLine()
        o.AppendLine("| Flagged row (UTC) | File, line | Flag | Direction | Amount USD | Copies (flag @ line) |")
        o.AppendLine("|---|---|---|---|---|---|")
        For Each t In flagged.OrderBy(Function(x) x.Ts).Take(12)
            Dim tt = t
            o.AppendLine(String.Format(Inv, "| {0:yyyy-MM-dd HH:mm:ss.fff} | {1} {2} | {3} | {4} | {5:0} | {6} |", LfUtc(t.Ts), t.FileName, t.Line, t.Flag, t.Direction, t.Amount,
                String.Join("; ", byKey(LfKey(t)).Select(Function(x) x.Flag & " @ " & x.Line.ToString(Inv)))))
        Next
        o.AppendLine()

        ' ---- collapsed tape, sorted by time
        Dim tape = byKey.Values.Select(Function(l) New LfTrade With {
                        .Ts = l(0).Ts, .Price = l(0).Price, .Amount = l(0).Amount, .Direction = l(0).Direction,
                        .Flag = If(l.Any(Function(x) x.Flag <> "none"), l.First(Function(x) x.Flag <> "none").Flag, "none")}) _
                   .OrderBy(Function(x) x.Ts).ToList()
        Dim tapeTs As Long() = tape.Select(Function(x) x.Ts).ToArray()
        Dim flagPrefix(tape.Count) As Integer
        Dim makerPrefix(tape.Count) As Integer
        For i As Integer = 0 To tape.Count - 1
            flagPrefix(i + 1) = flagPrefix(i) + If(tape(i).Flag <> "none", 1, 0)
            makerPrefix(i + 1) = makerPrefix(i) + If(tape(i).Flag = "M" OrElse tape(i).Flag = "MT", 1, 0)
        Next

        ' ---- collector rows (L-1)
        Dim collector As New List(Of LfRow)()
        collector.AddRange(LfLoadRows(bakPath))
        collector.AddRange(LfLoadRows(livePath))
        Dim spanLo As Long = tapeTs(0), spanHi As Long = tapeTs(tapeTs.Length - 1)
        Dim inSpan As Integer = 0
        Dim hits As New List(Of Tuple(Of LfRow, Integer, Integer))()
        For Each r In collector.OrderBy(Function(x) x.Ts)
            Dim ms As Long = ToMs(r.Ts)
            If ms < spanLo OrElse ms > spanHi Then Continue For
            inSpan += 1
            Dim j As Integer = LfUpperBound(tapeTs, ms)          ' trades with Ts <= row time: indices [0, j)
            Dim lo As Integer = Math.Max(0, j - LfWindow)
            Dim nFlag As Integer = flagPrefix(j) - flagPrefix(lo)
            Dim nMaker As Integer = makerPrefix(j) - makerPrefix(lo)
            If nFlag > 0 Then hits.Add(Tuple.Create(r, nFlag, nMaker))
        Next
        o.AppendLine("## 3. Collector rows whose trade window holds a liquidation (finding L-1)")
        o.AppendLine()
        o.AppendLine("| Measure | Rows |")
        o.AppendLine("|---|---|")
        o.AppendLine(Row("Collector log rows inside the store span", inSpan))
        o.AppendLine(Row("... whose 500-trade window holds at least one liquidation trade", hits.Count))
        o.AppendLine(Row("... of which the collector logged LiqSignal other than NONE", hits.Where(Function(h) h.Item1.LiqSignal <> "NONE").Count()))
        o.AppendLine(Row("... of which the collector logged a non-zero liquidation size", hits.Where(Function(h) h.Item1.LiqLong > 0 OrElse h.Item1.LiqShort > 0).Count()))
        o.AppendLine()
        o.AppendLine("| Row (UTC) | Verdict | Logged LiqSignal | Logged long / short size | Liquidation trades in window | Of which maker-side (M, MT) |")
        o.AppendLine("|---|---|---|---|---|---|")
        For Each h In hits
            o.AppendLine(String.Format(Inv, "| {0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3:0.00} / {4:0.00} | {5} | {6} |",
                h.Item1.Ts, h.Item1.Verdict, h.Item1.LiqSignal, h.Item1.LiqLong, h.Item1.LiqShort, h.Item2, h.Item3))
        Next
        o.AppendLine()

        ' ---- maker-side liquidations (L-2)
        Dim makerTrades = tape.Where(Function(x) x.Flag = "M" OrElse x.Flag = "MT").ToList()
        o.AppendLine("## 4. Maker-side liquidations (finding L-2)")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Collapsed tape liquidation trades: {0}; flag T: {1}; flag M: {2}; flag MT: {3}.",
            tape.Where(Function(x) x.Flag <> "none").Count(), tape.Where(Function(x) x.Flag = "T").Count(),
            tape.Where(Function(x) x.Flag = "M").Count(), tape.Where(Function(x) x.Flag = "MT").Count()))
        For Each t In makerTrades
            o.AppendLine(String.Format(Inv, "- {0:yyyy-MM-dd HH:mm:ss.fff} UTC: flag {1}, taker direction {2}, {3:0} USD. CalcLiquidations books it as a {4} liquidation; Deribit's flag says the liquidated account was the {5}.",
                LfUtc(t.Ts), t.Flag, t.Direction, t.Amount, If(t.Direction = "buy", "SHORT", "LONG"),
                If(t.Flag = "MT", "maker and the taker", If(t.Direction = "buy", "maker on the sell side, a LONG", "maker on the buy side, a SHORT"))))
        Next
        o.AppendLine(String.Format(Inv, "- Collector rows whose window holds a maker-side liquidation: {0}.", hits.Where(Function(h) h.Item3 > 0).Count()))
        o.AppendLine()

        ' ---- logged LiqSignal over the population and all merged rows since v51
        Dim merged As New Dictionary(Of DateTime, LfRow)()
        For Each r In LfLoadRows(pooledPath).Concat(LfLoadRows(livePath))
            If Not merged.ContainsKey(r.Ts) Then merged(r.Ts) = r
        Next
        Dim since = merged.Values.Where(Function(r) r.Ts >= V51Edge).ToList()
        Dim popTs As New HashSet(Of DateTime)(sigs.Select(Function(s) s.Ts))
        o.AppendLine("## 5. Logged LiqSignal")
        o.AppendLine()
        o.AppendLine("| Rows | Count | NONE | LONG LIQS | SHORT LIQS | Non-zero size |")
        o.AppendLine("|---|---|---|---|---|---|")
        For Each grp In {Tuple.Create("All merged rows from the v51 edge (pooled book + box live log)", since),
                         Tuple.Create("Swing read population rows", since.Where(Function(r) popTs.Contains(r.Ts)).ToList())}
            Dim lst = grp.Item2
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} |", grp.Item1, lst.Count,
                lst.Where(Function(r) r.LiqSignal = "NONE").Count(), lst.Where(Function(r) r.LiqSignal = "LONG LIQS").Count(),
                lst.Where(Function(r) r.LiqSignal = "SHORT LIQS").Count(), lst.Where(Function(r) r.LiqLong > 0 OrElse r.LiqShort > 0).Count()))
        Next
        o.AppendLine()

        ' ---- amount units (L-3)
        Dim whole10 As Long = raw.LongCount(Function(t) Math.Abs(t.Amount / 10.0 - Math.Round(t.Amount / 10.0)) < 0.000001)
        o.AppendLine("## 6. Units of `amount` (finding L-3)")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Store rows whose amount is a whole multiple of 10: {0} of {1} ({2:0.0000} %). BTC-PERPETUAL trades in 10 USD contracts, so amounts are USD, not BTC.",
            whole10, raw.Count, 100.0 * whole10 / Math.Max(1, raw.Count)))
        o.AppendLine(String.Format(Inv, "- Median liquidation trade amount: {0:0} USD. `large_liq_size` 200 is compared with these USD sums.",
            Median(flagged.Select(Function(t) t.Amount).ToList())))
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

    Private Function LfKey(t As LfTrade) As String
        Return t.Ts.ToString(Inv) & "|" & t.Price & "|" & t.Amount.ToString("R", Inv) & "|" & t.Direction
    End Function

    Private Function LfUtc(ms As Long) As DateTime
        Return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
    End Function

    ' Count of elements <= value in an ascending array.
    Private Function LfUpperBound(arr As Long(), value As Long) As Integer
        Dim lo As Integer = 0, hi As Integer = arr.Length
        While lo < hi
            Dim mid As Integer = lo + (hi - lo) \ 2
            If arr(mid) <= value Then lo = mid + 1 Else hi = mid
        End While
        Return lo
    End Function

    Private Function LfLoadRows(path As String) As List(Of LfRow)
        Dim list As New List(Of LfRow)()
        Dim iTs As Integer = -1, iV As Integer = -1, iSig As Integer = -1, iLongCol As Integer = -1, iShortCol As Integer = -1
        Dim first As Boolean = True
        For Each line In File.ReadLines(path)
            Dim p = line.Split(","c)
            If first Then
                first = False
                iTs = Array.IndexOf(p, "Timestamp") : iV = Array.IndexOf(p, "Verdict") : iSig = Array.IndexOf(p, "LiqSignal")
                iLongCol = Array.IndexOf(p, "LiqLongSize") : iShortCol = Array.IndexOf(p, "LiqShortSize")
                If {iTs, iV, iSig, iLongCol, iShortCol}.Any(Function(x) x < 0) Then Throw New InvalidOperationException("liquidation columns missing in " & path & ". STOP.")
                Continue For
            End If
            If p.Length <= Math.Max(iSig, Math.Max(iLongCol, iShortCol)) Then Continue For
            Dim ts As DateTime
            If Not DateTime.TryParseExact(p(iTs).Trim(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, ts) Then Continue For
            Dim l As Double = 0, s As Double = 0
            Double.TryParse(p(iLongCol), NumberStyles.Float, Inv, l)
            Double.TryParse(p(iShortCol), NumberStyles.Float, Inv, s)
            list.Add(New LfRow With {.Ts = ts, .Verdict = p(iV).Trim(), .LiqSignal = p(iSig).Trim(), .LiqLong = l, .LiqShort = s})
        Next
        Return list
    End Function

End Module
