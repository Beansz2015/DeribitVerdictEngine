' tools/BacktestRunner/HistoryCompare.vb
' docs/history-data-store-spec.md §4 — the comparison that gates stage 4, the stage-4 pass
' rule over a ledger of comparisons, and the store status / deep verification used after the
' cloud backfill and after the transfer. READ-ONLY on both stores. Host-agnostic, network-free.
'
' Comparison (§4.1): the box store (a fetch folder's backtest_data, seven columns, duplicates
' and seq-less legacy rows allowed) against the dev store, over the days the dev store holds
' as complete (checkpoint status OK). Counts seqs only in the box, seqs only in the dev store,
' field mismatches (trade_id, timestamp, price, amount, direction) on EVERY box row (not one
' copy per seq, so a disagreeing duplicate cannot hide), liquidation-flag disagreements, and
' the dev store's own seq contiguity over the compared days.
'
' Verdict PASS needs (§4.2): 0 seqs only in the box, 0 field mismatches — plus, because each
' is also a trade the dev store would get wrong: 0 unmatched seq-less box rows, 0 box flags the
' dev store lacks, 0 dev duplicates, 0 dev missing seqs, 0 unparseable dev rows, at least one
' compared day. Seqs only in the dev store are box holes: counted, never a failure.
'
' Fixtures: A94h (comparison), A94i (pass rule), A94j (status --deep counts a torn row).

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq

Public NotInheritable Class HistoryCompareResult
    Public Property FromDay As DateTime
    Public Property ToDayExcl As DateTime
    Public Property ComparedDays As New List(Of DateTime)()
    Public Property NotCompared As New List(Of String)()
    Public Property BoxRows As Long
    Public Property BoxDistinctSeqs As Long
    Public Property BoxSeqless As Long
    Public Property BoxUnparseable As Long
    Public Property BoxMissingFiles As New List(Of String)()
    Public Property DevRows As Long
    Public Property DevUnparseable As Long
    Public Property DevDuplicateSeqs As Long
    Public Property OnlyInBox As Long
    Public Property OnlyInBoxFirst As New List(Of Long)()
    Public Property OnlyInDev As Long
    Public Property SeqlessUnmatched As Long
    Public Property Mismatch As New Dictionary(Of String, Long) From {
        {"trade_id", 0}, {"timestamp", 0}, {"price", 0}, {"amount", 0}, {"direction", 0}}
    Public Property MismatchExamples As New List(Of String)()
    Public Property LiqBoxFlagged As Long
    Public Property LiqDevFlagged As Long
    Public Property LiqBoxFlaggedDevNone As Long
    Public Property LiqDevFlaggedBoxNone As Long
    Public Property LiqBothDiffer As Long
    Public Property DevMissingSeqs As Long
    Public Property FailReasons As New List(Of String)()

    Public ReadOnly Property FieldMismatches As Long
        Get
            Return Mismatch.Values.Sum()
        End Get
    End Property

    Public ReadOnly Property Verdict As String
        Get
            Return If(FailReasons.Count = 0, "PASS", "FAIL")
        End Get
    End Property
End Class

Public NotInheritable Class HistoryCompare

    ''' <summary>[spec §4.2, fixed 2026-09-29] At least this many PASS comparisons…</summary>
    Public Const PassRuleMinComparisons As Integer = 3
    ''' <summary>…whose run dates span at least this many days (last − first), and no FAIL.</summary>
    Public Const PassRuleMinSpanDays As Integer = 14

    Public Const LedgerHeader As String =
        "ComparedAtUtc,FromDay,ToDayExcl,ComparedDays,FirstComparedDay,LastComparedDay,BoxRows,DevRows,OnlyInBox,OnlyInDev,SeqlessUnmatched,FieldMismatches,LiqBoxOnly,DevMissingSeqs,Verdict"

    Private Shared Function IsFlagged(liq As String) As Boolean
        Return Not String.IsNullOrEmpty(liq) AndAlso Not String.Equals(liq, "none", StringComparison.Ordinal)
    End Function

    ' Trade fields without the liquidation flag: the box copy of a flagged trade reads `none`
    ' (the flag arrives late), so the five-field LegacyRowKey would never match it.
    Private Shared Function MatchKey(t As TradeRecord) As String
        Return String.Format(CultureInfo.InvariantCulture, "{0},{1:F2},{2:F2},{3}", t.Timestamp, t.Price, t.Amount, t.Direction)
    End Function

    ''' <summary>Compare the box store in <paramref name="boxDir"/> with the dev store in
    ''' <paramref name="storeDir"/> over [fromDay, toDayExcl).</summary>
    Public Shared Function Compare(boxDir As String, storeDir As String, fromDay As DateTime, toDayExcl As DateTime) As HistoryCompareResult
        Dim r As New HistoryCompareResult With {.FromDay = fromDay.Date, .ToDayExcl = toDayExcl.Date}
        Dim cp = HistoryStore.LoadCheckpoint(storeDir)
        Dim compared As New HashSet(Of Long)()
        Dim d As DateTime = fromDay.Date
        While d < toDayExcl.Date
            Dim en As HistoryDayEntry = Nothing
            If cp.TryGetValue(HistoryStore.DayKey(d), en) AndAlso en.Status = HistoryStore.StatusOk Then
                r.ComparedDays.Add(d)
                compared.Add(HistoryStore.DayStartMs(d))
            Else
                r.NotCompared.Add(HistoryStore.DayKey(d) & " (" & If(en Is Nothing, "not in the dev store", en.Status) & ")")
            End If
            d = d.AddDays(1)
        End While

        Dim months = r.ComparedDays.Select(Function(x) (x.Year, x.Month)).Distinct().ToList()

        ' ── Dev store ──
        Dim dev As New Dictionary(Of Long, TradeRecord)()
        Dim devKeys As New Dictionary(Of String, List(Of Long))(StringComparer.Ordinal)
        For Each m In months
            Dim p As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            If Not File.Exists(p) Then
                r.FailReasons.Add("dev month file missing: " & p)
                Continue For
            End If
            Using sr As New StreamReader(p)
                Dim header As String = sr.ReadLine()
                If header IsNot Nothing AndAlso header <> HistoryStore.HeaderLine Then
                    r.FailReasons.Add("dev file has a foreign header (is --store a history store?): " & p)
                    Continue For
                End If
                Do
                    Dim line As String = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim rec As TradeRecord = Nothing
                    Dim x As HistoryExtras = Nothing
                    If Not HistoryStore.TryParseHistoryRow(line, rec, x) Then
                        r.DevUnparseable += 1
                        Continue Do
                    End If
                    If Not compared.Contains(HistoryStore.DayStartMs(HistoryStore.DayOfMs(rec.Timestamp))) Then Continue Do
                    r.DevRows += 1
                    If dev.ContainsKey(rec.TradeSeq) Then
                        r.DevDuplicateSeqs += 1
                    Else
                        dev(rec.TradeSeq) = rec
                        Dim mk As String = MatchKey(rec)
                        Dim lst As List(Of Long) = Nothing
                        If Not devKeys.TryGetValue(mk, lst) Then
                            lst = New List(Of Long)()
                            devKeys(mk) = lst
                        End If
                        lst.Add(rec.TradeSeq)
                    End If
                Loop
            End Using
        Next
        For Each t In dev.Values
            If IsFlagged(t.Liquidation) Then r.LiqDevFlagged += 1
        Next

        ' ── Box store: every row, duplicates included ──
        Dim boxSeqs As New HashSet(Of Long)()
        Dim onlyBox As New SortedSet(Of Long)()
        Dim seqlessKeys As New HashSet(Of String)(StringComparer.Ordinal)
        Dim liqBoxSeqs As New HashSet(Of Long)()
        For Each m In months
            Dim p As String = TradeStoreWriter.TradeFileFor(boxDir, m.Year, m.Month)
            If Not File.Exists(p) Then
                r.BoxMissingFiles.Add(p)
                Continue For
            End If
            Using sr As New StreamReader(p)
                sr.ReadLine()   ' header (5- or 7-column box header)
                Do
                    Dim line As String = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim b As New TradeRecord()
                    If Not TradeStoreWriter.TryParseRow(line, b) Then
                        r.BoxUnparseable += 1
                        Continue Do
                    End If
                    If Not compared.Contains(HistoryStore.DayStartMs(HistoryStore.DayOfMs(b.Timestamp))) Then Continue Do
                    r.BoxRows += 1
                    If Not b.HasSeq Then
                        r.BoxSeqless += 1
                        seqlessKeys.Add(MatchKey(b))
                        Continue Do
                    End If
                    boxSeqs.Add(b.TradeSeq)
                    Dim v As TradeRecord = Nothing
                    If Not dev.TryGetValue(b.TradeSeq, v) Then
                        onlyBox.Add(b.TradeSeq)
                        Continue Do
                    End If
                    CompareField(r, "trade_id", String.Equals(If(b.TradeId, ""), If(v.TradeId, ""), StringComparison.Ordinal), b, v)
                    CompareField(r, "timestamp", b.Timestamp = v.Timestamp, b, v)
                    CompareField(r, "price", b.Price = v.Price, b, v)
                    CompareField(r, "amount", b.Amount = v.Amount, b, v)
                    CompareField(r, "direction", String.Equals(b.Direction, v.Direction, StringComparison.Ordinal), b, v)
                    Dim bf As Boolean = IsFlagged(b.Liquidation)
                    Dim df As Boolean = IsFlagged(v.Liquidation)
                    If bf Then liqBoxSeqs.Add(b.TradeSeq)
                    If bf AndAlso Not df Then r.LiqBoxFlaggedDevNone += 1
                    If bf AndAlso df AndAlso Not String.Equals(b.Liquidation, v.Liquidation, StringComparison.Ordinal) Then r.LiqBothDiffer += 1
                Loop
            End Using
        Next
        r.BoxDistinctSeqs = boxSeqs.Count
        r.LiqBoxFlagged = liqBoxSeqs.Count
        r.OnlyInBox = onlyBox.Count
        r.OnlyInBoxFirst.AddRange(onlyBox.Take(10))
        ' A seq-less (pre-identity) box row is matched on its trade fields; the dev trade(s) it
        ' matches ARE in the box, so they are not "only in dev".
        For Each k In seqlessKeys
            Dim lst As List(Of Long) = Nothing
            If devKeys.TryGetValue(k, lst) Then
                For Each sq In lst
                    boxSeqs.Add(sq)
                Next
            Else
                r.SeqlessUnmatched += 1
            End If
        Next
        For Each kv In dev
            If Not boxSeqs.Contains(kv.Key) Then
                r.OnlyInDev += 1
                If IsFlagged(kv.Value.Liquidation) Then r.LiqDevFlaggedBoxNone += 1
            ElseIf IsFlagged(kv.Value.Liquidation) AndAlso Not liqBoxSeqs.Contains(kv.Key) Then
                r.LiqDevFlaggedBoxNone += 1
            End If
        Next

        ' ── Dev contiguity across the compared days (and the seam between adjacent ones) ──
        Dim seqs As List(Of Long) = dev.Keys.ToList()
        seqs.Sort()
        For i As Integer = 1 To seqs.Count - 1
            Dim gap As Long = seqs(i) - seqs(i - 1) - 1L
            If gap <= 0 Then Continue For
            Dim da As Long = HistoryStore.DayStartMs(HistoryStore.DayOfMs(dev(seqs(i - 1)).Timestamp))
            Dim db As Long = HistoryStore.DayStartMs(HistoryStore.DayOfMs(dev(seqs(i)).Timestamp))
            If db - da <= HistoryStore.DayMs Then r.DevMissingSeqs += gap
        Next

        If r.ComparedDays.Count = 0 Then r.FailReasons.Add("no compared day (no dev day with status OK in the window)")
        If r.OnlyInBox > 0 Then r.FailReasons.Add(r.OnlyInBox & " seq(s) only in the box store")
        If r.SeqlessUnmatched > 0 Then r.FailReasons.Add(r.SeqlessUnmatched & " seq-less box row(s) with no matching dev trade")
        If r.FieldMismatches > 0 Then r.FailReasons.Add(r.FieldMismatches & " field mismatch(es)")
        If r.LiqBoxFlaggedDevNone > 0 Then r.FailReasons.Add(r.LiqBoxFlaggedDevNone & " box liquidation flag(s) the dev store lacks")
        If r.LiqBothDiffer > 0 Then r.FailReasons.Add(r.LiqBothDiffer & " liquidation flag(s) that differ")
        If r.DevDuplicateSeqs > 0 Then r.FailReasons.Add(r.DevDuplicateSeqs & " duplicate seq row(s) in the dev store")
        If r.DevMissingSeqs > 0 Then r.FailReasons.Add(r.DevMissingSeqs & " seq(s) missing inside the dev store's compared days")
        If r.DevUnparseable > 0 Then r.FailReasons.Add(r.DevUnparseable & " unparseable dev row(s)")
        Return r
    End Function

    Private Shared Sub CompareField(r As HistoryCompareResult, field As String, equal As Boolean, b As TradeRecord, v As TradeRecord)
        If equal Then Return
        r.Mismatch(field) += 1
        If r.MismatchExamples.Count < 5 Then
            r.MismatchExamples.Add(String.Format(CultureInfo.InvariantCulture,
                "seq {0} {1}: box={2} dev={3}", b.TradeSeq, field, TradeStoreWriter.FormatRow(b), TradeStoreWriter.FormatRow(v)))
        End If
    End Sub

    Public Shared Function Report(r As HistoryCompareResult) As List(Of String)
        Dim o As New List(Of String)()
        o.Add(String.Format(CultureInfo.InvariantCulture, "[compare] window {0} .. {1} (to exclusive) | compared days {2} | not compared {3}",
                            HistoryStore.DayKey(r.FromDay), HistoryStore.DayKey(r.ToDayExcl), r.ComparedDays.Count, r.NotCompared.Count))
        If r.NotCompared.Count > 0 Then o.Add("[compare] not compared: " & String.Join("; ", r.NotCompared.Take(10)) & If(r.NotCompared.Count > 10, " ...", ""))
        For Each f In r.BoxMissingFiles
            o.Add("[compare] box month file absent: " & f)
        Next
        o.Add(String.Format(CultureInfo.InvariantCulture,
            "[compare] box rows {0} (distinct seqs {1}, seq-less {2}, unparseable lines in the month files {3}) | dev rows {4} (duplicate seqs {5}, unparseable {6})",
            r.BoxRows, r.BoxDistinctSeqs, r.BoxSeqless, r.BoxUnparseable, r.DevRows, r.DevDuplicateSeqs, r.DevUnparseable))
        o.Add(String.Format(CultureInfo.InvariantCulture,
            "[compare] seqs only in box {0}{1} | only in dev {2} (box holes; counted, not a failure) | seq-less box rows unmatched {3}",
            r.OnlyInBox, If(r.OnlyInBoxFirst.Count > 0, " (first " & String.Join(", ", r.OnlyInBoxFirst) & ")", ""),
            r.OnlyInDev, r.SeqlessUnmatched))
        o.Add("[compare] field mismatches: " & String.Join(" ", r.Mismatch.Select(Function(kv) kv.Key & "=" & kv.Value)))
        For Each e In r.MismatchExamples
            o.Add("[compare]   e.g. " & e)
        Next
        o.Add(String.Format(CultureInfo.InvariantCulture,
            "[compare] liquidation: dev flagged {0} | box flagged {1} | box flagged, dev none {2} | dev flagged, box none {3} (expected: the box copy predates the flag) | both flagged, differ {4}",
            r.LiqDevFlagged, r.LiqBoxFlagged, r.LiqBoxFlaggedDevNone, r.LiqDevFlaggedBoxNone, r.LiqBothDiffer))
        o.Add("[compare] dev missing seqs across compared days: " & r.DevMissingSeqs)
        If r.BoxUnparseable > 0 Then
            o.Add("[compare] WARN box month files hold " & r.BoxUnparseable & " unparseable line(s); they carry no timestamp, so no window can claim them (audit row E1)")
        End If
        o.Add(String.Format(CultureInfo.InvariantCulture,
            "HISTORY_COMPARE verdict={0} compared_days={1} box_rows={2} dev_rows={3} only_in_box={4} only_in_dev={5} field_mismatches={6} liq_box_only={7} dev_missing={8}{9}",
            r.Verdict, r.ComparedDays.Count, r.BoxRows, r.DevRows, r.OnlyInBox, r.OnlyInDev, r.FieldMismatches,
            r.LiqBoxFlaggedDevNone, r.DevMissingSeqs,
            If(r.FailReasons.Count > 0, " reasons=" & String.Join("; ", r.FailReasons), "")))
        Return o
    End Function

    ' ── Ledger and the stage-4 pass rule (§4.2) ─────────────────────────────────────────

    Public Shared Sub AppendLedger(ledgerPath As String, r As HistoryCompareResult, comparedAtUtc As DateTime)
        Dim dir As String = Path.GetDirectoryName(Path.GetFullPath(ledgerPath))
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Dim isNew As Boolean = Not File.Exists(ledgerPath) OrElse New FileInfo(ledgerPath).Length = 0
        Dim first As String = If(r.ComparedDays.Count > 0, HistoryStore.DayKey(r.ComparedDays.Min()), "")
        Dim last As String = If(r.ComparedDays.Count > 0, HistoryStore.DayKey(r.ComparedDays.Max()), "")
        Dim row As String = String.Join(",", {
            comparedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            HistoryStore.DayKey(r.FromDay), HistoryStore.DayKey(r.ToDayExcl),
            r.ComparedDays.Count.ToString(CultureInfo.InvariantCulture), first, last,
            r.BoxRows.ToString(CultureInfo.InvariantCulture), r.DevRows.ToString(CultureInfo.InvariantCulture),
            r.OnlyInBox.ToString(CultureInfo.InvariantCulture), r.OnlyInDev.ToString(CultureInfo.InvariantCulture),
            r.SeqlessUnmatched.ToString(CultureInfo.InvariantCulture), r.FieldMismatches.ToString(CultureInfo.InvariantCulture),
            r.LiqBoxFlaggedDevNone.ToString(CultureInfo.InvariantCulture), r.DevMissingSeqs.ToString(CultureInfo.InvariantCulture),
            r.Verdict})
        Using w As New StreamWriter(ledgerPath, append:=True)
            w.NewLine = vbLf
            If isNew Then w.WriteLine(LedgerHeader)
            w.WriteLine(row)
        End Using
    End Sub

    ''' <summary>
    ''' The stage-4 gate: MET when the ledger holds at least PassRuleMinComparisons PASS rows,
    ''' their run dates (ComparedAtUtc) span at least PassRuleMinSpanDays (last − first), and
    ''' NO row is FAIL — any failure stops the plan and goes to the trader (§4.2). The span is
    ''' measured on the RUN dates, not on the data windows, because the parallel run exists to
    ''' catch divergence that appears over time; three comparisons run on one afternoon over
    ''' old windows would not test that.
    ''' </summary>
    Public Shared Function EvaluatePassRule(ledgerPath As String, reasons As List(Of String)) As Boolean
        If Not File.Exists(ledgerPath) Then
            reasons.Add("ledger not found: " & ledgerPath)
            Return False
        End If
        Dim lines As String() = File.ReadAllLines(ledgerPath)
        If lines.Length = 0 OrElse lines(0) <> LedgerHeader Then
            reasons.Add("ledger header missing or foreign")
            Return False
        End If
        Dim passDates As New List(Of DateTime)()
        Dim fails As Integer = 0
        Dim bad As Integer = 0
        For i As Integer = 1 To lines.Length - 1
            If lines(i).Length = 0 Then Continue For
            Dim p As String() = lines(i).Split(","c)
            Dim t As DateTime
            If p.Length <> 15 OrElse Not DateTime.TryParse(p(0), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, t) Then
                bad += 1
                Continue For
            End If
            If p(14) = "PASS" Then
                passDates.Add(t)
            Else
                fails += 1
            End If
        Next
        If bad > 0 Then reasons.Add(bad & " unparseable ledger row(s)")
        If fails > 0 Then reasons.Add(fails & " FAIL comparison(s) — the plan stops and goes to the trader")
        If passDates.Count < PassRuleMinComparisons Then
            reasons.Add(passDates.Count & " PASS comparison(s), need " & PassRuleMinComparisons)
        End If
        Dim spanDays As Double = If(passDates.Count > 0, (passDates.Max() - passDates.Min()).TotalDays, 0)
        If spanDays < PassRuleMinSpanDays Then
            reasons.Add(String.Format(CultureInfo.InvariantCulture, "PASS comparisons span {0:F1} day(s), need {1}", spanDays, PassRuleMinSpanDays))
        End If
        Return reasons.Count = 0
    End Function

    ' ── Store status and the deep verification ──────────────────────────────────────────

    Public NotInheritable Class StatusResult
        Public Property Lines As New List(Of String)()
        Public Property Problems As New List(Of String)()
        Public Property CompleteMonths As New List(Of String)()
    End Class

    ''' <summary>
    ''' Summarise the checkpoint: day counts by status, calendar days missing inside the span,
    ''' the seq seam between adjacent OK days (day D's first seq must be day D−1's last + 1),
    ''' and per-month completeness. With <paramref name="deep"/>, re-read every month file:
    ''' unparseable rows, rows per day against the checkpoint, duplicate seqs, missing seqs
    ''' between adjacent OK days, rows for days the checkpoint does not hold.
    ''' </summary>
    Public Shared Function Status(storeDir As String, deep As Boolean) As StatusResult
        Dim s As New StatusResult()
        Dim cp = HistoryStore.LoadCheckpoint(storeDir)
        If cp.Count = 0 Then
            s.Problems.Add("no checkpoint rows in " & Path.Combine(storeDir, HistoryStore.CheckpointFileName))
            Return s
        End If
        Dim days = cp.Values.OrderBy(Function(e) e.Day).ToList()
        Dim first As DateTime = days.First().Day
        Dim last As DateTime = days.Last().Day
        Dim nOk As Integer = days.Where(Function(e) e.Status = HistoryStore.StatusOk).Count()
        Dim nGap As Integer = days.Where(Function(e) e.Status = HistoryStore.StatusGap).Count()
        Dim nFail As Integer = days.Count - nOk - nGap
        Dim rows As Long = days.Sum(Function(e) e.Rows)
        s.Lines.Add(String.Format(CultureInfo.InvariantCulture,
            "[status] {0} | days {1} .. {2} | OK {3} | GAP {4} | FAILED {5} | rows {6}",
            storeDir, HistoryStore.DayKey(first), HistoryStore.DayKey(last), nOk, nGap, nFail, rows))
        For Each e In days.Where(Function(x) x.Status <> HistoryStore.StatusOk)
            s.Problems.Add(HistoryStore.DayKey(e.Day) & " " & e.Status & " " & e.Detail)
        Next
        Dim holes As New List(Of String)()
        Dim d As DateTime = first
        While d <= last
            If Not cp.ContainsKey(HistoryStore.DayKey(d)) Then holes.Add(HistoryStore.DayKey(d))
            d = d.AddDays(1)
        End While
        If holes.Count > 0 Then s.Problems.Add(holes.Count & " day(s) inside the span not fetched, first " & String.Join(", ", holes.Take(5)))

        Dim seams As Integer = 0
        For i As Integer = 1 To days.Count - 1
            Dim a = days(i - 1)
            Dim b = days(i)
            If b.Day <> a.Day.AddDays(1) OrElse a.Status <> HistoryStore.StatusOk OrElse b.Status <> HistoryStore.StatusOk Then Continue For
            If b.FirstSeq <> a.LastSeq + 1L Then
                seams += 1
                If seams <= 5 Then s.Problems.Add(String.Format(CultureInfo.InvariantCulture,
                    "seq seam {0}/{1}: last {2} then first {3}", HistoryStore.DayKey(a.Day), HistoryStore.DayKey(b.Day), a.LastSeq, b.FirstSeq))
            End If
        Next
        If seams > 5 Then s.Problems.Add(seams & " seq seam break(s) in total")

        For Each g In days.GroupBy(Function(e) (e.Day.Year, e.Day.Month)).OrderBy(Function(x) x.Key.Year * 100 + x.Key.Month)
            Dim inMonth As Integer = DateTime.DaysInMonth(g.Key.Year, g.Key.Month)
            Dim ok As Integer = g.Where(Function(e) e.Status = HistoryStore.StatusOk).Count()
            Dim key As String = String.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}", g.Key.Year, g.Key.Month)
            Dim complete As Boolean = ok = inMonth
            If complete Then s.CompleteMonths.Add(key)
            s.Lines.Add(String.Format(CultureInfo.InvariantCulture, "MONTH {0} ok={1}/{2} gap={3} failed={4} rows={5}{6}",
                key, ok, inMonth, g.Where(Function(e) e.Status = HistoryStore.StatusGap).Count(),
                g.Where(Function(e) e.Status = HistoryStore.StatusFailed).Count(), g.Sum(Function(e) e.Rows),
                If(complete, " COMPLETE", "")))
        Next

        If deep Then DeepCheck(storeDir, cp, s)
        s.Lines.Add(String.Format(CultureInfo.InvariantCulture, "HISTORY_STATUS problems={0} complete_months={1}{2}",
                                  s.Problems.Count, s.CompleteMonths.Count, If(deep, " deep=yes", " deep=no")))
        Return s
    End Function

    Private Shared Sub DeepCheck(storeDir As String, cp As Dictionary(Of String, HistoryDayEntry), s As StatusResult)
        Dim months = cp.Values.Select(Function(e) (e.Day.Year, e.Day.Month)).Distinct().OrderBy(Function(x) x.Year * 100 + x.Month).ToList()
        For Each m In months
            Dim p As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            If Not File.Exists(p) Then
                s.Problems.Add("month file missing: " & p)
                Continue For
            End If
            Dim perDay As New Dictionary(Of String, Long)(StringComparer.Ordinal)
            Dim pts As New List(Of (Seq As Long, DayMs As Long))()
            Dim unparseable As Long = 0
            Dim lineNo As Long = 0
            Using sr As New StreamReader(p)
                Dim header As String = sr.ReadLine()
                If header <> HistoryStore.HeaderLine Then s.Problems.Add("foreign header in " & p)
                Do
                    Dim line As String = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    lineNo += 1
                    Dim rec As TradeRecord = Nothing
                    Dim x As HistoryExtras = Nothing
                    If Not HistoryStore.TryParseHistoryRow(line, rec, x) Then
                        unparseable += 1
                        If unparseable <= 3 Then s.Problems.Add("unparseable row at data line " & lineNo & " of " & p)
                        Continue Do
                    End If
                    Dim dk As String = HistoryStore.DayKey(HistoryStore.DayOfMs(rec.Timestamp))
                    Dim c As Long = 0
                    perDay.TryGetValue(dk, c)
                    perDay(dk) = c + 1
                    pts.Add((rec.TradeSeq, HistoryStore.DayStartMs(HistoryStore.DayOfMs(rec.Timestamp))))
                Loop
            End Using
            If unparseable > 3 Then s.Problems.Add(unparseable & " unparseable row(s) in " & p)
            For Each kv In perDay
                Dim en As HistoryDayEntry = Nothing
                If Not cp.TryGetValue(kv.Key, en) Then
                    s.Problems.Add(kv.Value & " row(s) for " & kv.Key & ", a day the checkpoint does not hold")
                ElseIf en.Rows <> kv.Value Then
                    s.Problems.Add(String.Format(CultureInfo.InvariantCulture, "{0}: file holds {1} rows, checkpoint says {2}", kv.Key, kv.Value, en.Rows))
                End If
            Next
            For Each en In cp.Values.Where(Function(e) e.Day.Year = m.Year AndAlso e.Day.Month = m.Month AndAlso e.Status <> HistoryStore.StatusFailed)
                If Not perDay.ContainsKey(HistoryStore.DayKey(en.Day)) Then s.Problems.Add(HistoryStore.DayKey(en.Day) & ": checkpoint says " & en.Rows & " rows, file holds none")
            Next
            pts.Sort(Function(a, b) a.Seq.CompareTo(b.Seq))
            Dim dups As Long = 0
            Dim missing As Long = 0
            For i As Integer = 1 To pts.Count - 1
                Dim gap As Long = pts(i).Seq - pts(i - 1).Seq
                If gap = 0 Then
                    dups += 1
                ElseIf gap > 1 Then
                    Dim ea As HistoryDayEntry = Nothing
                    Dim eb As HistoryDayEntry = Nothing
                    cp.TryGetValue(HistoryStore.DayKey(HistoryStore.DayOfMs(pts(i - 1).DayMs)), ea)
                    cp.TryGetValue(HistoryStore.DayKey(HistoryStore.DayOfMs(pts(i).DayMs)), eb)
                    If ea IsNot Nothing AndAlso eb IsNot Nothing AndAlso ea.Status = HistoryStore.StatusOk AndAlso
                       eb.Status = HistoryStore.StatusOk AndAlso pts(i).DayMs - pts(i - 1).DayMs <= HistoryStore.DayMs Then
                        missing += gap - 1L
                    End If
                End If
            Next
            If dups > 0 Then s.Problems.Add(dups & " duplicate seq row(s) in " & p)
            If missing > 0 Then s.Problems.Add(missing & " seq(s) missing between OK days in " & p)
            s.Lines.Add(String.Format(CultureInfo.InvariantCulture, "DEEP {0:D4}-{1:D2} rows={2} unparseable={3} duplicates={4} missing={5}",
                                      m.Year, m.Month, pts.Count, unparseable, dups, missing))
        Next
    End Sub

End Class
