' tools/BacktestRunner/HistoryCli.vb
' The `history` verb family of BacktestRunner (docs/history-data-store-spec.md §3-§4).
' Dispatched from BacktestProgram BEFORE it moves the working directory to the repo root, so a
' relative --store / --box / --ledger resolves against the caller's own directory. Every
' resolved path is printed.
'
'   history backfill --from yyyy-MM-dd --to yyyy-MM-dd [--store <dir>] [--max-days N]
'   history topup    [--store <dir>] [--max-days N]
'   history status   [--store <dir>] [--deep]
'   history compare  --box <backtest_data dir> --from yyyy-MM-dd --to yyyy-MM-dd [--store <dir>] [--ledger <csv>]
'   history passrule --ledger <csv>
'
' --to is EXCLUSIVE everywhere (the other verbs' convention): --from 2025-01-01 --to 2026-10-01
' covers 2025-01-01 through 2026-09-30.
'
' Exit codes: 0 done / PASS / MET · 1 bad arguments or a store problem · 2 stopped on a host
' failure the retries did not absorb (resumable: run again) · 3 compare FAIL / pass rule not
' met · 4 every planned day was attempted but some are GAP or FAILED, or status found a problem.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Threading.Tasks

Public NotInheritable Class HistoryCli

    Public Shared Async Function RunAsync(args As String()) As Task(Of Integer)
        If args.Length < 2 Then
            PrintUsage()
            Return HistoryStore.ExitBadArgsOrStore
        End If
        Dim sub_ As String = args(1).ToLowerInvariant()
        Dim opts As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim flags As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim i As Integer = 2
        While i < args.Length
            Dim a As String = args(i)
            Select Case a.ToLowerInvariant()
                Case "--deep"
                    flags.Add(a)
                Case "--from", "--to", "--store", "--box", "--ledger", "--max-days"
                    ' A value-taking flag at the end of the line is an error, never silently
                    ' dropped (the coverage verb's lesson: a dropped --evidence-dir read the
                    ' wrong store and reported clean).
                    If i + 1 >= args.Length OrElse args(i + 1).StartsWith("--", StringComparison.Ordinal) Then
                        Console.Error.WriteLine("[history] " & a & " needs a value")
                        Return HistoryStore.ExitBadArgsOrStore
                    End If
                    opts(a.ToLowerInvariant()) = args(i + 1)
                    i += 1
                Case Else
                    Console.Error.WriteLine("[history] unknown argument: " & a)
                    PrintUsage()
                    Return HistoryStore.ExitBadArgsOrStore
            End Select
            i += 1
        End While

        Dim store As String = Path.GetFullPath(If(opts.ContainsKey("--store"), opts("--store"), HistoryStore.DefaultStoreDir()))
        Dim maxDays As Integer = 0
        If opts.ContainsKey("--max-days") AndAlso
           (Not Integer.TryParse(opts("--max-days"), NumberStyles.Integer, CultureInfo.InvariantCulture, maxDays) OrElse maxDays < 1) Then
            Console.Error.WriteLine("[history] --max-days must be a positive integer")
            Return HistoryStore.ExitBadArgsOrStore
        End If

        Select Case sub_
            Case "backfill"
                Dim f, t As DateTime
                If Not TryDay(opts, "--from", f) OrElse Not TryDay(opts, "--to", t) Then Return HistoryStore.ExitBadArgsOrStore
                If t <= f Then
                    Console.Error.WriteLine("[history] --to (exclusive) must be after --from")
                    Return HistoryStore.ExitBadArgsOrStore
                End If
                Return Await RunBackfill(store, f, t, maxDays)

            Case "topup"
                Dim cp As Dictionary(Of String, HistoryDayEntry)
                Try
                    cp = HistoryStore.LoadCheckpoint(store)
                Catch ex As HistoryStoreException
                    Console.Error.WriteLine("[history] " & ex.Message)
                    Return HistoryStore.ExitBadArgsOrStore
                End Try
                If cp.Count = 0 Then
                    Console.Error.WriteLine("[history] no checkpoint in " & store & " — run `history backfill` first")
                    Return HistoryStore.ExitBadArgsOrStore
                End If
                Dim first As DateTime = DateTime.MaxValue
                For Each e In cp.Values
                    If e.Day < first Then first = e.Day
                Next
                Dim lastSettled As DateTime = HistoryStore.LastSettledDay(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                Console.WriteLine("[history] top-up: every day not yet OK from " & HistoryStore.DayKey(first) &
                                  " (first checkpointed day) to " & HistoryStore.DayKey(lastSettled) & " (last settled day)")
                Return Await RunBackfill(store, first, lastSettled.AddDays(1), maxDays)

            Case "status"
                Console.WriteLine("[history] store: " & store)
                Dim s As HistoryCompare.StatusResult
                Try
                    s = HistoryCompare.Status(store, flags.Contains("--deep"))
                Catch ex As HistoryStoreException
                    Console.Error.WriteLine("[history] " & ex.Message)
                    Return HistoryStore.ExitBadArgsOrStore
                End Try
                For Each l In s.Lines
                    Console.WriteLine(l)
                Next
                For Each p In s.Problems
                    Console.WriteLine("PROBLEM " & p)
                Next
                Return If(s.Problems.Count = 0, HistoryStore.ExitOk, HistoryStore.ExitDaysIncomplete)

            Case "compare"
                Dim f, t As DateTime
                If Not TryDay(opts, "--from", f) OrElse Not TryDay(opts, "--to", t) Then Return HistoryStore.ExitBadArgsOrStore
                If Not opts.ContainsKey("--box") Then
                    Console.Error.WriteLine("[history] compare needs --box <the fetch folder's backtest_data directory>")
                    Return HistoryStore.ExitBadArgsOrStore
                End If
                Dim box As String = Path.GetFullPath(opts("--box"))
                If Not Directory.Exists(box) Then
                    Console.Error.WriteLine("[history] --box not found: " & box)
                    Return HistoryStore.ExitBadArgsOrStore
                End If
                Console.WriteLine("[history] box store: " & box)
                Console.WriteLine("[history] dev store: " & store)
                Dim r As HistoryCompareResult
                Try
                    r = HistoryCompare.Compare(box, store, f, t)
                Catch ex As HistoryStoreException
                    Console.Error.WriteLine("[history] " & ex.Message)
                    Return HistoryStore.ExitBadArgsOrStore
                End Try
                For Each l In HistoryCompare.Report(r)
                    Console.WriteLine(l)
                Next
                If opts.ContainsKey("--ledger") Then
                    Dim lp As String = Path.GetFullPath(opts("--ledger"))
                    HistoryCompare.AppendLedger(lp, r, DateTime.UtcNow)
                    Console.WriteLine("[history] ledger row appended: " & lp)
                End If
                Return If(r.Verdict = "PASS", HistoryStore.ExitOk, HistoryStore.ExitCompareFail)

            Case "passrule"
                If Not opts.ContainsKey("--ledger") Then
                    Console.Error.WriteLine("[history] passrule needs --ledger <csv>")
                    Return HistoryStore.ExitBadArgsOrStore
                End If
                Dim lp As String = Path.GetFullPath(opts("--ledger"))
                Dim reasons As New List(Of String)()
                Dim met As Boolean = HistoryCompare.EvaluatePassRule(lp, reasons)
                Console.WriteLine("[history] ledger: " & lp)
                For Each rr In reasons
                    Console.WriteLine("[history]   " & rr)
                Next
                Console.WriteLine("HISTORY_PASSRULE " & If(met, "MET", "NOT_MET") &
                                  String.Format(CultureInfo.InvariantCulture, " (need >= {0} PASS comparisons over >= {1} days, no FAIL)",
                                                HistoryCompare.PassRuleMinComparisons, HistoryCompare.PassRuleMinSpanDays))
                Return If(met, HistoryStore.ExitOk, HistoryStore.ExitCompareFail)

            Case Else
                Console.Error.WriteLine("[history] unknown history verb: " & sub_)
                PrintUsage()
                Return HistoryStore.ExitBadArgsOrStore
        End Select
    End Function

    Private Shared Async Function RunBackfill(store As String, f As DateTime, t As DateTime, maxDays As Integer) As Task(Of Integer)
        Dim hs As New HistoryStore(store, AddressOf HistoryHostHttp.GetJsonAsync)
        Dim r As HistoryRunResult = Await hs.RunAsync(f, t, maxDays)
        Dim tradeRate As Double = If(r.Seconds > 0, r.Trades / r.Seconds, 0)
        Dim reqRate As Double = If(r.Seconds > 0, r.Requests / r.Seconds, 0)
        Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
            "HISTORY_RUN exit={0} ok={1} gap={2} failed={3} remaining={4} already_complete={5} not_settled={6} trades={7} requests={8} retries={9} secs={10:F0} rate={11:F0} trades/s {12:F2} req/s{13}",
            r.ExitCode, r.DaysOk, r.DaysGap, r.DaysFailed, r.RemainingNotDone, r.AlreadyComplete, r.Unsettled,
            r.Trades, r.Requests, r.Retries, r.Seconds, tradeRate, reqRate,
            If(String.IsNullOrEmpty(r.StoppedReason), "", " reason=" & r.StoppedReason)))
        Return r.ExitCode
    End Function

    Private Shared Function TryDay(opts As Dictionary(Of String, String), key As String, ByRef d As DateTime) As Boolean
        If Not opts.ContainsKey(key) Then
            Console.Error.WriteLine("[history] " & key & " yyyy-MM-dd is required")
            Return False
        End If
        If Not DateTime.TryParseExact(opts(key), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, d) Then
            Console.Error.WriteLine("[history] " & key & " must be yyyy-MM-dd (UTC day), got " & opts(key))
            Return False
        End If
        d = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc)
        Return True
    End Function

    Public Shared Sub PrintUsage()
        Console.Error.WriteLine("  BacktestRunner history backfill --from yyyy-MM-dd --to yyyy-MM-dd [--store <dir>] [--max-days N]   (--to exclusive; newest day first)")
        Console.Error.WriteLine("  BacktestRunner history topup    [--store <dir>] [--max-days N]")
        Console.Error.WriteLine("  BacktestRunner history status   [--store <dir>] [--deep]")
        Console.Error.WriteLine("  BacktestRunner history compare  --box <backtest_data dir> --from yyyy-MM-dd --to yyyy-MM-dd [--store <dir>] [--ledger <csv>]")
        Console.Error.WriteLine("  BacktestRunner history passrule --ledger <csv>")
        Console.Error.WriteLine("  default --store: " & HistoryStore.DefaultStoreDir())
    End Sub

End Class
