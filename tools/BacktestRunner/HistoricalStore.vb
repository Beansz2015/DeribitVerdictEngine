' tools/BacktestRunner/HistoricalStore.vb
' Fetch-once historical data store for the backtest synthesizer (docs/backtest-synthesizer
' -proposal.md §3). Stores 1m/3m/5m/15m candles, raw trades (with the liq flag), and funding
' rate history under backtest_data/, keyed monthly per resolution / stream. Re-runs skip
' complete months; a partial trade month resumes from the last-stored timestamp.
'
' Endpoints:
'   Candles  — public/get_tradingview_chart_data     (via DeribitClient.GetCandlesAsync range,
'                                                     inherits ExecuteWithRetry)
'   Trades   — public/get_last_trades_by_instrument by trade_seq, with one count=1 anchor call
'              to public/get_last_trades_by_instrument_and_time (local HTTP; not in DeribitClient)
'   Funding  — public/get_funding_rate_history       (local HTTP; not in DeribitClient)
'
' Host-agnostic (no WinForms). Polite: 200ms delay between paginated calls; retry-once on
' 5xx / timeout for the two local-HTTP endpoints (the ExecuteWithRetry discipline, implemented
' here to keep DeribitClient untouched per the task's HARD CONSTRAINT).
'
' Store paths (relative to CWD, which BacktestProgram sets to the repo root):
'   backtest_data/candles_1m_YYYY-MM.csv    ts,open,high,low,close,volume,cost
'   backtest_data/candles_3m_YYYY-MM.csv    same
'   backtest_data/candles_5m_YYYY-MM.csv    same
'   backtest_data/candles_15m_YYYY-MM.csv   same
'   backtest_data/trades_YYYY-MM.csv        ts,price,amount,direction,liquidation
'   backtest_data/funding_YYYY-MM.csv       ts,rate
'   backtest_data/.state.json               resumable cursors (per-stream last-ts)

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks

Public Class HistoricalStore

    Public Const StoreDir As String = "backtest_data"
    Public Const InstrumentName As String = "BTC-PERPETUAL"

    ' Polite delay between paginated requests (trades + funding). Deribit's public
    ' endpoints have generous rate limits; 200 ms leaves plenty of headroom and keeps a
    ' months-long backfill from ever tripping them.
    Private Const PoliteDelayMs As Integer = 200

    ' Trades: max 1000 per call is Deribit's cap for get_last_trades_* — measured on both the
    ' time and the seq endpoint (count=1001 ⇒ -32602 "value is too high"). Public so fixtures
    ' read the production number instead of restating it.
    Public Const TradesPerPage As Integer = 1000

    ' Guard against runaway loops on inverted ranges / bad cursors.
    Private Const MaxTradePages As Integer = 200000

    ' ⛔ UseProxy:=False — same WPAD hazard as DeribitClient.vb, and this client matters on the
    ' box too: TradeStoreGapRepair links this file, so in-app gap repair fetches through it.
    ' A hang in proxy resolution is NOT bounded by .Timeout (the request timeout starts after
    ' the request is issued), so the 30 s below would not have saved it. See DeribitClient.vb.
    Private Shared ReadOnly _http As New HttpClient(New HttpClientHandler With {.UseProxy = False}) With {.Timeout = TimeSpan.FromSeconds(30)}

    ' [F2/F3 sweep, 2026-09-07] The User-Agent now names the ACTUAL host process, resolved from
    ' the entry assembly, instead of the hardcoded "DeribitBacktestRunner/1.0".
    '
    ' Why this is not a literal swap. HistoricalStore is linked by the APP as well as by the
    ' offline tool -- TradeStoreGapRepair calls BackfillTradeMonthAsync -- so the live
    ' collector's repair traffic was announcing itself to Deribit as a backtest runner, and
    ' venue-side logs could not tell live repair from an offline replay. Swapping the literal to
    ' "DeribitVerdictEngine" would simply move the same lie onto the offline tool. Reading the
    ' entry assembly is the only form that is TRUE from every host: the app reports
    ' DeribitVerdictEngine, tools/BacktestRunner reports BacktestRunner, and a future host
    ' reports itself with no edit here.
    '
    ' GetEntryAssembly() can return Nothing (unmanaged host, some test runners), so the literal
    ' fallback stays -- it is a last resort, not the normal path.
    ''' <summary>The User-Agent this process sends. Extracted from the shared constructor so the
    ''' property is assertable -- _http is Private Shared ReadOnly and a fixture cannot read its
    ''' headers, so testing the ctor directly would mean reflection over a private field. A66c
    ''' pins that the string NAMES THE RUNNING HOST rather than a hardcoded foreign one.</summary>
    Friend Shared Function ResolveUserAgent() As String
        Dim host As String = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name
        If String.IsNullOrWhiteSpace(host) Then host = "DeribitVerdictEngine"
        Return host & "/1.0"
    End Function

    Shared Sub New()
        _http.DefaultRequestHeaders.Add("User-Agent", ResolveUserAgent())
    End Sub

    ' ── Path helpers ──────────────────────────────────────────────────────────────────

    Public Shared Function CandleFileFor(resolution As Integer, year As Integer, month As Integer) As String
        Return Path.Combine(StoreDir, String.Format("candles_{0}m_{1:D4}-{2:D2}.csv", resolution, year, month))
    End Function

    ' [v64] Trade-file naming lives in TradeStoreWriter — the ONE seam shared with the
    ' streaming capture in DeribitWsFeed.ApplyTrades and with the reader below, so the
    ' three cannot drift (in-app-trade-store-capture-proposal.md §2).
    Public Shared Function TradeFileFor(year As Integer, month As Integer) As String
        Return TradeStoreWriter.TradeFileFor(StoreDir, year, month)
    End Function

    Public Shared Function FundingFileFor(year As Integer, month As Integer) As String
        Return Path.Combine(StoreDir, String.Format("funding_{0:D4}-{1:D2}.csv", year, month))
    End Function

    Private Shared Sub EnsureStoreDir()
        Directory.CreateDirectory(StoreDir)
    End Sub

    ' Iterate the (year, month, monthStartUtc, monthEndExclusiveUtc) tuples that cover
    ' [fromUtc, toUtc]. Boundaries snap to first-of-month; a partial trailing month is
    ' capped at toUtc.
    Public Shared Iterator Function EnumerateMonths(fromUtc As DateTime, toUtc As DateTime) _
            As IEnumerable(Of (Year As Integer, Month As Integer, StartUtc As DateTime, EndUtcExcl As DateTime))
        Dim cur As New DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim endCap As DateTime = toUtc
        While cur < endCap
            Dim nxt As DateTime = cur.AddMonths(1)
            Dim segStart As DateTime = If(cur < fromUtc, fromUtc, cur)
            Dim segEnd   As DateTime = If(nxt > endCap, endCap, nxt)
            Yield (cur.Year, cur.Month, segStart, segEnd)
            cur = nxt
        End While
    End Function

    ' ── Candle backfill ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Fetch (if needed) and store one calendar month of candles at the given resolution.
    '''
    ''' [2026-07-31 fix] This function destroyed a month of 3m/5m/15m June data, via two
    ''' independent defects that were each survivable alone:
    '''
    ''' (1) **Resolution-blind coverage check.** The retired `MonthFileCovers` heuristic
    '''     required the file's last bar to be within a FIXED 2 minutes of the segment end.
    '''     A month's last bar is 23:59 at 1m but 23:57 / 23:55 / 23:45 at 3m / 5m / 15m — so
    '''     **every non-1m month failed the check on every run** and was refetched
    '''     unconditionally. 1m alone was spared, which is exactly why 1m alone survived.
    '''
    ''' (2) **A SEGMENT fetch overwrote the whole MONTH file** (`append:=False`). Segments are
    '''     not always whole months: `BackfillAllAsync` starts 20 h before `fromUtc`, so a
    '''     fetch from 2026-07-01 gives June the segment 06-30 04:00 → 07-01. Combined with
    '''     (1), that replaced all of June with 20 hours at three resolutions.
    '''
    ''' Both are fixed the way the funding path was: coverage is a COUNT against the
    ''' deterministic grid, and the write MERGES with what is already stored. With the merge
    ''' in place a partial or failed fetch can no longer destroy anything — the worst case is
    ''' that it adds nothing. That is the invariant worth keeping; the count check is only an
    ''' optimisation on top of it.
    ''' </summary>
    Public Shared Async Function BackfillCandleMonthAsync(
            resolution As Integer, year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = CandleFileFor(resolution, year, month)

        Dim startMs As Long = New DateTimeOffset(segStart,   TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs   As Long = New DateTimeOffset(segEndExcl.AddMilliseconds(-1), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim nowMs   As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        If endMs > nowMs Then endMs = nowMs

        ' Coverage by count against the bar grid — resolution-aware by construction, which
        ' defect (1) was not.
        Dim intervalMs As Long = CLng(resolution) * 60000L
        Dim existing As List(Of Candle) = StoreFiles.LoadCandleFile(path)
        Dim haveInRange As Integer = StoreFiles.CountCandlesInRange(existing, startMs, endMs)
        Dim expected As Integer = StoreFiles.ExpectedGridPoints(startMs, endMs, intervalMs)
        If haveInRange >= expected Then Return existing.Count

        ' Deribit's get_tradingview_chart_data caps each response at ~5001 ticks. For a
        ' multi-day 1m fetch that under-reports silently (only the trailing window
        ' comes back). Chunk into ~4000-candle segments per call — safely under the cap
        ' at every resolution — and stitch the results (dedup by Timestamp).
        Dim chunkMs As Long = intervalMs * 4000L
        Dim collected As New SortedDictionary(Of Long, Candle)()
        Dim cursor As Long = startMs
        While cursor <= endMs
            Dim chunkEnd As Long = Math.Min(endMs, cursor + chunkMs - 1)
            Dim chunk As List(Of Candle) = Await DeribitClient.GetCandlesAsync(resolution.ToString(), cursor, chunkEnd)
            If chunk Is Nothing Then
                Console.Error.WriteLine(String.Format(
                    "[HistoricalStore] Candle fetch failed for {0}m {1:D4}-{2:D2} @ {3} — keeping {4} stored bar(s)",
                    resolution, year, month, cursor, existing.Count))
                Exit While
            End If
            For Each c In chunk
                collected(c.Timestamp) = c
            Next
            cursor = chunkEnd + 1
            If chunk.Count > 0 Then
                Await Task.Delay(PoliteDelayMs)
            End If
        End While

        If collected.Count = 0 Then Return existing.Count

        ' MERGE — stored bars survive, fetched bars fill the holes. The invariant lives in
        ' StoreFiles (network-free, fixture-reachable, A51); this is the line whose absence
        ' cost a month of data.
        Dim before As Integer = existing.Count
        Dim total As Integer = StoreFiles.MergeAndWriteCandles(path, existing, collected.Values)
        Console.WriteLine(String.Format(
            "[HistoricalStore] Candles {0}m {1:D4}-{2:D2}: {3} stored (+{4} new, expected {5} in range)",
            resolution, year, month, total, total - before, expected))
        Return total
    End Function

    ''' <summary>Load one candle-month file into memory. Empty list on any error.</summary>
    Public Shared Function LoadCandleMonth(resolution As Integer, year As Integer, month As Integer) As List(Of Candle)
        Return StoreFiles.LoadCandleFile(CandleFileFor(resolution, year, month))
    End Function

    ' [2026-07-31] The candle parse moved to Core/StoreFiles.vb — network-free and
    ' fixture-reachable (A51). LoadCandleMonth above delegates to it.

    ''' <summary>Load the union of candle months covering [fromUtc, toUtc] and preceding
    ''' warm-up (needed for the 250-bar window at the leftmost bar-close). Chronological
    ''' ascending. Duplicate timestamps deduped keeping the last (newer file wins on the
    ''' seam).</summary>
    Public Shared Function LoadCandleRange(resolution As Integer,
                                            warmupStartUtc As DateTime,
                                            toUtc As DateTime) As List(Of Candle)
        Dim all As New List(Of Candle)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            all.AddRange(LoadCandleMonth(resolution, m.Year, m.Month))
        Next
        ' Dedup by Timestamp, keep the last-encountered.
        Dim map As New SortedDictionary(Of Long, Candle)()
        For Each c In all
            map(c.Timestamp) = c
        Next
        Return map.Values.ToList()
    End Function

    ' ── Trades backfill ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Fetch trades for one calendar-month segment [segStart, segEndExcl), appending to the month
    ''' file. ⛔ [GR-1 (d), docs/gap-repair-same-ms-page-skip-spec.md §4.3] Every window is fetched
    ''' BY trade_seq through <see cref="FetchRepairWindowAsync"/>; the time pager and its
    ''' `newestMs + 1` cursor are gone, including on the offline path. That cursor skipped any
    ''' trade sharing a full page's last millisecond, and the time hole windows could never fetch
    ''' it back: 16 permanent holes (70 trades) in the 2026-08-17 start-up repair.
    '''
    ''' [v64] Rows are committed through TradeStoreWriter.AppendRows — the SAME seam the
    ''' streaming capture writes through — so format, header-on-create and monthly rollover
    ''' cannot drift between the two producers, and the process-wide append lock keeps a
    ''' backfill page from interleaving with a streaming flush.
    ''' </summary>
    ''' <param name="storeDir">Nothing ⇒ the CWD-relative repo store (BacktestProgram sets
    ''' CWD to the repo root). The in-app gap repair passes the EXE-resolved dir (D3).</param>
    ''' <param name="clampToSegStart">[v64 gap repair] True ⇒ never reach further back than
    ''' segStart even when the on-disk resume point is older. Deribit's public trades endpoint
    ''' refuses windows past its ~24 h retention, so after a long outage an unclamped resume
    ''' cursor would ask for a refused window and recover NOTHING — including the last 20 h
    ''' that are still served. False (the default) preserves the historical-backfill
    ''' behaviour exactly: resume from disk and fill any hole between there and segEnd.</param>
    ''' <param name="repairHoles">[downtime repair Part A, D-5] True ⇒ resolve the fetch windows
    ''' through TradeStoreWriter.ResolveRepairWindows, which returns the trade_seq holes behind the
    ''' tail as well as the tail itself. TradeStoreGapRepair.RepairOnceAsync is the only caller
    ''' that passes True. False ⇒ one AnchoredTail at ResolveResumeCursorMs (the offline path).</param>
    ''' <param name="outcomes">[GR-4 (b)] Receives one outcome per window, for repair_status.log.</param>
    Public Shared Async Function BackfillTradeMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime,
            Optional storeDir As String = Nothing,
            Optional clampToSegStart As Boolean = False,
            Optional repairHoles As Boolean = False,
            Optional outcomes As List(Of TradeStoreWriter.RepairWindowOutcome) = Nothing) As Task(Of Integer)
        Return Await BackfillTradeMonthCoreAsync(year, month, segStart, segEndExcl, storeDir, clampToSegStart,
                                                 repairHoles, AddressOf FetchSeqPageJsonAsync,
                                                 AddressOf FetchTimeAnchorJsonAsync, PoliteDelayMs, outcomes)
    End Function

    ''' <summary>The month loop with the two venue calls injected, so A79c can drive a
    ''' month boundary with no network. Production goes through
    ''' <see cref="BackfillTradeMonthAsync"/>.</summary>
    Friend Shared Async Function BackfillTradeMonthCoreAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime,
            storeDir As String, clampToSegStart As Boolean, repairHoles As Boolean,
            fetchSeq As Func(Of Long, Long?, Integer, Task(Of String)),
            fetchAnchor As Func(Of Long, Long, Task(Of String)),
            pageDelayMs As Integer,
            outcomes As List(Of TradeStoreWriter.RepairWindowOutcome)) As Task(Of Integer)
        ' ⛔ VB IS CASE-INSENSITIVE: the parameter `storeDir` and the class const `StoreDir`
        ' (line 36) are THE SAME IDENTIFIER inside this body, so an unqualified `StoreDir`
        ' here resolves to the PARAMETER and the fallback silently returned Nothing.
        ' Found 2026-09-21 when `BacktestRunner fetch` crashed with
        ' ArgumentNullException(path) at the CreateDirectory below. The class-qualified form
        ' cannot bind to a local. Core/TradeStoreWriter.vb avoids this by naming its own
        ' fallback const DefaultStoreDir — the only reason it never collided.
        Dim dir As String = If(String.IsNullOrWhiteSpace(storeDir), HistoricalStore.StoreDir, storeDir)
        Directory.CreateDirectory(dir)
        Dim path As String = TradeStoreWriter.TradeFileFor(dir, year, month)
        Dim segStartMs As Long = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs As Long = New DateTimeOffset(segEndExcl, TimeSpan.Zero).ToUnixTimeMilliseconds() - 1

        ' Both resume decisions live on the shared seam (A48d and A56a–f exercise these exact calls).
        Dim windows As New List(Of TradeStoreWriter.RepairWindow)()
        If repairHoles Then
            ' [F-1] The previous month's file seeds a cross-month bracket when this file has none.
            Dim prevMonth As DateTime = New DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1)
            windows = TradeStoreWriter.ResolveRepairWindows(path, segStartMs, endMs, clampToSegStart,
                                                            TradeStoreWriter.TradeFileFor(dir, prevMonth.Year, prevMonth.Month))
        Else
            Dim cursor0 As Long = TradeStoreWriter.ResolveResumeCursorMs(path, segStartMs, endMs, clampToSegStart)
            If cursor0 >= 0 Then windows.Add(TradeStoreWriter.RepairWindow.ForAnchoredTail(cursor0, endMs))
        End If
        ' [DR-3] Nothing to fetch ⇒ this pass appended 0 rows. Returning the file's total row
        ' count here (as before) meant TradeStoreGapRepair.RepairOnceAsync summed the WHOLE
        ' month file into TotalRowsRepaired and logged it as "rows appended" on every healthy
        ' pass — the common case. BackfillAllAsync (this function's other caller) discards the
        ' return value entirely (see its .vb:575 call site), so this contract change is safe
        ' for both callers.
        If windows.Count = 0 Then Return 0

        Dim total As Integer = 0
        Dim page As Integer = 0
        Dim failed As Integer = 0
        Dim fileName As String = System.IO.Path.GetFileName(path)

        For w As Integer = 0 To windows.Count - 1
            If w > 0 AndAlso pageDelayMs > 0 Then Await Task.Delay(pageDelayMs)
            Dim o As TradeStoreWriter.RepairWindowOutcome =
                Await FetchRepairWindowAsync(windows(w), fetchSeq, fetchAnchor,
                                             Function(rows) TradeStoreWriter.AppendRows(dir, rows),
                                             TradesPerPage, MaxTradePages - page, pageDelayMs)
            o.FileName = fileName
            outcomes?.Add(o)
            total += o.Committed
            page += o.Pages
            If o.IsFailure OrElse o.NotServed > 0 Then
                failed += If(o.IsFailure, 1, 0)
                Console.Error.WriteLine(String.Format(
                    "[HistoricalStore] {0} {1}: {2} committed={3} not_served={4} {5}",
                    fileName, o.Window.Kind, o.State, o.Committed, o.NotServed, o.Reason))
            End If
            ' A failed window abandons THAT window only; the page cap stops the month.
            If o.State = TradeStoreWriter.RepairWindowOutcome.PageCap Then Exit For
        Next

        Console.WriteLine(String.Format(
            "[HistoricalStore] Trades {0:D4}-{1:D2}: appended {2} rows across {3} request(s) in {4} window(s), {5} failed",
            year, month, total, page, windows.Count, failed))
        Return total
    End Function

    ' ── Seq-range fetcher (GR-1 (d), docs/gap-repair-same-ms-page-skip-spec.md §4.1 and §4.3) ──
    '
    ' The venue contract this relies on — MEASURED live 2026-09-14, not documented:
    '   • get_last_trades_by_instrument: start_seq and end_seq are both INCLUSIVE;
    '   • paging with start_seq = last + 1 is exact (2,501 of 2,501, 0 duplicates, 0 missing);
    '   • has_more = more trades remain INSIDE the requested range after this page;
    '   • count caps at 1,000; an open end_seq runs to the latest trade;
    '   • ⚠ a start_seq older than the ~24 h retention returns trades FROM THE RETENTION EDGE,
    '     not an empty list; a range wholly past retention returns nothing.
    ' ⛔ If the venue ever breaks this contract (a trade outside the requested range, a skipped
    ' or repeated seq across pages), that is the spec's escalation trigger, not a local patch.

    ''' <summary>One parsed trades page: the trades and the venue's `has_more` (Nothing when absent).</summary>
    Friend NotInheritable Class TradePage
        Public ReadOnly Property Trades As New List(Of TradeRecord)()
        Public Property HasMore As Boolean?
    End Class

    ''' <summary>
    ''' Fetch one repair window by trade_seq and commit what the venue serves. Never throws a
    ''' venue problem at the caller: every failure is a state on the returned outcome. The two
    ''' venue calls return a response body or Nothing — injected, so A79a–A79e run with no network.
    ''' </summary>
    Friend Shared Async Function FetchRepairWindowAsync(win As TradeStoreWriter.RepairWindow,
            fetchSeq As Func(Of Long, Long?, Integer, Task(Of String)),
            fetchAnchor As Func(Of Long, Long, Task(Of String)),
            commit As Func(Of List(Of TradeRecord), Integer),
            pageSize As Integer, pagesLeft As Integer, pageDelayMs As Integer) _
            As Task(Of TradeStoreWriter.RepairWindowOutcome)
        Dim o As New TradeStoreWriter.RepairWindowOutcome With {.Window = win}
        Dim isHole As Boolean = win.Kind = TradeStoreWriter.RepairWindowKind.Hole
        Dim endSeq As Long? = If(win.LastSeq >= 0, CType(win.LastSeq, Long?), Nothing)

        ' ⛔ [DUP-2] A failed store read fetches NOTHING and says so; the next pass retries.
        If win.Kind = TradeStoreWriter.RepairWindowKind.ScanFailure Then
            Return Finish(o, TradeStoreWriter.RepairWindowOutcome.ScanFailed, win.Failure)
        End If
        If win.Kind = TradeStoreWriter.RepairWindowKind.SeedReadFailure Then
            Return Finish(o, TradeStoreWriter.RepairWindowOutcome.SeedReadFailed, win.Failure)
        End If

        ' ── 1. Resolve the first sequence ─────────────────────────────────────────────
        Dim startSeq As Long
        If win.Kind = TradeStoreWriter.RepairWindowKind.AnchoredTail Then
            If pagesLeft <= 0 Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.PageCap, "page cap before the anchor")
            Dim aj As String = Await fetchAnchor(win.AnchorMs, win.StopAfterMs)
            o.Pages += 1
            If aj Is Nothing Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.FetchFailed, "anchor fetch failed at " & win.AnchorMs)
            Dim ap As TradePage = ParseTradesPage(aj)
            If ap Is Nothing Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.FetchFailed, "unparseable anchor response at " & win.AnchorMs)
            Dim first As TradeRecord = Nothing
            For Each t In ap.Trades
                If t.HasSeq AndAlso t.Timestamp >= win.AnchorMs AndAlso t.Timestamp <= win.StopAfterMs Then
                    first = t
                    Exit For
                End If
                o.Rejected += 1
            Next
            ' No trade at or after the anchor inside the window: nothing to repair.
            If first Is Nothing Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.TailEmpty, "")
            startSeq = first.TradeSeq
        Else
            startSeq = win.FirstSeq
        End If
        o.StartSeq = startSeq

        ' ── 2–8. Page by sequence ─────────────────────────────────────────────────────
        Dim cursor As Long = startSeq
        Dim expected As Long = startSeq
        Dim served As Boolean = False
        Dim seen As New HashSet(Of Long)()
        Do
            If endSeq.HasValue AndAlso cursor > endSeq.Value Then Exit Do
            If o.Pages >= pagesLeft Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.PageCap, "page cap at start_seq " & cursor)
            Dim json As String = Await fetchSeq(cursor, endSeq, pageSize)
            o.Pages += 1
            If json Is Nothing Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.FetchFailed, "seq fetch failed at start_seq " & cursor)
            Dim page As TradePage = ParseTradesPage(json)
            If page Is Nothing Then Return Finish(o, TradeStoreWriter.RepairWindowOutcome.FetchFailed, "unparseable response at start_seq " & cursor)

            If page.Trades.Count = 0 Then
                ' ⛔ has_more with nothing on the page cannot advance — never loop on it.
                If page.HasMore.GetValueOrDefault(False) Then
                    Return Finish(o, TradeStoreWriter.RepairWindowOutcome.NoProgress, "has_more with an empty page at start_seq " & cursor)
                End If
                Exit Do
            End If

            ' Sequence order is the walk order; the venue sends ascending, this does not rely on it.
            Dim ordered As New List(Of TradeRecord)(page.Trades)
            ordered.Sort(Function(a, b) a.TradeSeq.CompareTo(b.TradeSeq))

            Dim kept As New List(Of TradeRecord)()
            Dim reachedStop As Boolean = False
            Dim maxSeqOnPage As Long = TradeStoreWriter.AbsentSeq
            For Each t In ordered
                If t.HasSeq AndAlso t.TradeSeq > maxSeqOnPage Then maxSeqOnPage = t.TradeSeq
                If reachedStop Then Continue For
                If Not t.HasSeq OrElse t.TradeSeq < cursor OrElse
                   (endSeq.HasValue AndAlso t.TradeSeq > endSeq.Value) Then
                    o.Rejected += 1
                    Continue For
                End If
                ' ⛔ GT-3: a tail stops at its segment end, or a month-boundary pass double-writes
                ' the next month's already-captured trades.
                If Not isHole AndAlso t.Timestamp > win.StopAfterMs Then
                    reachedStop = True
                    Continue For
                End If
                If Not seen.Add(t.TradeSeq) Then
                    o.Rejected += 1
                    Continue For
                End If
                ' ⛔ GT-1: a gap before the first served trade is the venue's retention edge, not a
                ' clean start. Count it; never take the first served seq as the start.
                If t.TradeSeq > expected Then
                    If served Then
                        o.NotServedInside += t.TradeSeq - expected
                    Else
                        o.NotServedBefore += t.TradeSeq - expected
                    End If
                End If
                kept.Add(t)
                served = True
                expected = t.TradeSeq + 1L
                o.LastServedSeq = t.TradeSeq
            Next
            If kept.Count > 0 Then o.Committed += commit(kept)
            If reachedStop Then Exit Do

            Dim more As Boolean = If(page.HasMore.HasValue, page.HasMore.Value, page.Trades.Count >= pageSize)
            If Not more Then Exit Do
            ' ⛔ A page that reports more but holds no sequence at or past the cursor cannot advance.
            If maxSeqOnPage < cursor Then
                Return Finish(o, TradeStoreWriter.RepairWindowOutcome.NoProgress, "no trade_seq at or after start_seq " & cursor & " on a page reporting more")
            End If
            cursor = maxSeqOnPage + 1L
            If pageDelayMs > 0 Then Await Task.Delay(pageDelayMs)
        Loop

        ' ── 9–10. Close the window ────────────────────────────────────────────────────
        If isHole AndAlso expected <= win.LastSeq Then o.NotServedAfter += win.LastSeq - expected + 1L
        Dim state As String
        If isHole Then
            If o.NotServed = 0 Then
                state = TradeStoreWriter.RepairWindowOutcome.HoleRepaired
            ElseIf served Then
                state = TradeStoreWriter.RepairWindowOutcome.HolePartial
            Else
                state = TradeStoreWriter.RepairWindowOutcome.HoleNotServed
            End If
        ElseIf o.NotServedBefore > 0 Then
            state = TradeStoreWriter.RepairWindowOutcome.TailPastRetention
        ElseIf o.NotServedInside > 0 Then
            state = TradeStoreWriter.RepairWindowOutcome.TailGap
        Else
            state = TradeStoreWriter.RepairWindowOutcome.TailOk
        End If
        Return Finish(o, state, "")
    End Function

    Private Shared Function Finish(o As TradeStoreWriter.RepairWindowOutcome, state As String,
                                   reason As String) As TradeStoreWriter.RepairWindowOutcome
        o.State = state
        o.Reason = If(reason, "")
        Return o
    End Function

    ''' <summary>Parse one get_last_trades_* response. Nothing on a malformed body, a JSON-RPC
    ''' error, or a missing trades array — never throws.</summary>
    Friend Shared Function ParseTradesPage(json As String) As TradePage
        If String.IsNullOrEmpty(json) Then Return Nothing
        Try
            Using doc As JsonDocument = JsonDocument.Parse(json)
                Dim result As JsonElement = Nothing
                If Not doc.RootElement.TryGetProperty("result", result) Then Return Nothing
                Dim tradesEl As JsonElement = Nothing
                If Not result.TryGetProperty("trades", tradesEl) OrElse
                   tradesEl.ValueKind <> JsonValueKind.Array Then Return Nothing
                Dim page As New TradePage()
                Dim hm As JsonElement = Nothing
                If result.TryGetProperty("has_more", hm) AndAlso
                   (hm.ValueKind = JsonValueKind.True OrElse hm.ValueKind = JsonValueKind.False) Then
                    page.HasMore = hm.GetBoolean()
                End If
                For Each t In tradesEl.EnumerateArray()
                    Dim rec As New TradeRecord()
                    rec.Price = t.GetProperty("price").GetDouble()
                    rec.Amount = t.GetProperty("amount").GetDouble()
                    rec.Direction = t.GetProperty("direction").GetString()
                    rec.Timestamp = t.GetProperty("timestamp").GetInt64()
                    Dim liqEl As JsonElement = Nothing
                    rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
                    ' [trade identity] The same two shared readers the WS feed and the venue
                    ' check use, so the three parse sites cannot disagree about identity.
                    rec.TradeId = TradeRecord.ReadTradeId(t)
                    rec.TradeSeq = TradeRecord.ReadTradeSeq(t)
                    page.Trades.Add(rec)
                Next
                Return page
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>One seq-endpoint page. <paramref name="endSeq"/> Nothing ⇒ open-ended.</summary>
    Friend Shared Function FetchSeqPageJsonAsync(startSeq As Long, endSeq As Long?, count As Integer) As Task(Of String)
        Dim url As String = "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument" &
                            "?instrument_name=" & InstrumentName &
                            "&start_seq=" & startSeq.ToString(CultureInfo.InvariantCulture) &
                            If(endSeq.HasValue, "&end_seq=" & endSeq.Value.ToString(CultureInfo.InvariantCulture), "") &
                            "&count=" & count.ToString(CultureInfo.InvariantCulture) &
                            "&sorting=asc"
        Return GetWithRetryAsync(url)
    End Function

    ''' <summary>The time endpoint with count=1: the first trade at or after
    ''' <paramref name="startMs"/>. The only time-endpoint use left in this file.</summary>
    Friend Shared Function FetchTimeAnchorJsonAsync(startMs As Long, endMs As Long) As Task(Of String)
        Dim url As String = "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time" &
                            "?instrument_name=" & InstrumentName &
                            "&start_timestamp=" & startMs.ToString(CultureInfo.InvariantCulture) &
                            "&end_timestamp=" & endMs.ToString(CultureInfo.InvariantCulture) &
                            "&count=1&sorting=asc"
        Return GetWithRetryAsync(url)
    End Function

    ''' <summary>GET with retry-once on a 5xx or a timeout (the ExecuteWithRetry discipline
    ''' reproduced here so DeribitClient can stay untouched). Nothing on any other failure.</summary>
    Private Shared Async Function GetWithRetryAsync(url As String) As Task(Of String)
        For attempt As Integer = 1 To 2
            Dim needsRetry As Boolean = False
            Try
                Return Await _http.GetStringAsync(url)
            Catch ex As HttpRequestException
                If attempt < 2 AndAlso ex.StatusCode.HasValue AndAlso CInt(ex.StatusCode.Value) >= 500 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Trades HTTP failure: " & ex.Message)
                    Return Nothing
                End If
            Catch ex As TaskCanceledException
                If attempt < 2 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Trades timeout")
                    Return Nothing
                End If
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] Trades error: " & ex.Message)
                Return Nothing
            End Try
            If needsRetry Then Await Task.Delay(500)
        Next
        Return Nothing
    End Function

    ''' <summary>Load the union of trade months for [warmupStartUtc, toUtc], chronological
    ''' ascending (the F1 contract). Duplicates removed by TradeStoreWriter.DedupTrades, which
    ''' is what makes a gap-repair pass overlapping streamed data harmless at read time (A48d).
    '''
    ''' [v64] The per-file parse delegates to TradeStoreWriter.ReadTradeFile — the same seam
    ''' that formats the rows — so reader and writer cannot drift. Sorting here (rather than
    ''' assuming file order) is what tolerates a backfill page landing after a streaming
    ''' flush of newer trades.
    '''
    ''' [trade identity] The dedup was `seen.Add(FormatRow(rec))` — whole-row equality on five
    ''' fields. That silently DROPPED genuinely distinct trades that happened to share all five
    ''' (22,376 of AWS's 228,163 August rows were exact five-field duplicates, and nothing could
    ''' say how many were real). It now routes through the §3.4 contract, which keys on identity
    ''' where identity exists and falls back to the five fields only where it does not. Dedup is
    ''' applied ACROSS the whole month union, not per file, exactly as before.</summary>
    Public Shared Function LoadTradeRange(warmupStartUtc As DateTime, toUtc As DateTime) As List(Of TradeRecord)
        Dim raw As New List(Of TradeRecord)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            raw.AddRange(TradeStoreWriter.ReadTradeFile(TradeFileFor(m.Year, m.Month)))
        Next
        Dim all = TradeStoreWriter.DedupTrades(raw)
        all.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))
        Return all
    End Function

    ' ── Funding backfill ──────────────────────────────────────────────────────────────
    ' The sample type is the TOP-LEVEL BacktestFundingSample (declared in ReplayLoop.vb) —
    ' keeps HistoricalStore's HttpClient off the harness's link surface.

    ' Funding samples land on the hour, every hour (verified: 0 off-hour samples across
    ' Feb–Jun 2026, and 3,637 of 3,643 intervals exactly 60 min).
    Private Const FundingIntervalMs As Long = 3600000L

    ''' <summary>
    ''' Fetch and store one calendar-month of funding rate history. The endpoint returns the
    ''' full range in one call (~720 hourly samples/month), so pagination is unnecessary.
    '''
    ''' [2026-07-31 fix] Two defects lived here and both are closed:
    '''
    ''' (1) **Exclusive start.** `start_timestamp` is EXCLUSIVE — verified against the live
    '''     endpoint: a request from exactly 2026-06-01T00:00:00.000Z returns 01:00 first,
    '''     the same request minus 1 ms returns 00:00. Since each month's window began at the
    '''     boundary instant, **every month silently lost its 00:00 sample** — 5 of 5 internal
    '''     seams in the store (Feb 671/672, Mar 743/744, Apr 719/720, May 743/744,
    '''     Jun 719/720: exactly one missing each, always the boundary). Fixed by fetching one
    '''     interval early and filtering the result back to the segment, which is correct
    '''     under either inclusive or exclusive semantics.
    '''
    ''' (2) **Fetch-once with no coverage check.** The guard was `If File.Exists(path)`, so a
    '''     partial month file was frozen PERMANENTLY. That is what produced the 28.2-day hole
    '''     (2026-06-30 23:00 → 2026-07-29 05:00 UTC): a narrow early fetch on 07-30 created
    '''     `funding_2026-07.csv` with 30 samples, and the 6-month fetch the next day skipped
    '''     the month entirely on File.Exists. The candle path never had this bug because it
    '''     checks `MonthFileCovers`; funding had no equivalent. Fixed by comparing the stored
    '''     in-range count against the expected hourly count, and MERGING on refetch rather
    '''     than rewriting — so repeated runs accumulate coverage instead of churning it.
    '''
    ''' Cost of the coverage check: a month the VENUE genuinely cannot fill stays short and
    ''' costs one redundant call per run. At one call per month that is not worth extra state
    ''' to avoid.
    ''' </summary>
    Public Shared Async Function BackfillFundingMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = FundingFileFor(year, month)

        Dim segStartMs As Long = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs      As Long = New DateTimeOffset(segEndExcl.AddMilliseconds(-1), TimeSpan.Zero).ToUnixTimeMilliseconds()

        ' Cap the window at NOW. The current month's segment runs to the month end, so without
        ' this the expectation counts hours that have not happened yet and the month can never
        ' satisfy the coverage check — a redundant fetch every run, forever. (Observed: the
        ' repair fetch left July at 716/720, the four "missing" samples being 20:00–23:00 on a
        ' day that had not reached 20:00.) Nothing is lost: the future cannot be fetched.
        Dim nowMs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        If endMs > nowMs Then endMs = nowMs

        ' Coverage check (defect 2). Count what is already stored INSIDE the requested
        ' segment — an out-of-range straggler must not make a short month look complete.
        Dim existing As List(Of BacktestFundingSample) = StoreFiles.LoadFundingFile(path)
        Dim haveInRange As Integer = StoreFiles.CountFundingInRange(existing, segStartMs, endMs)
        For Each s In existing
            If s.TsMs >= segStartMs AndAlso s.TsMs <= endMs Then haveInRange += 1
        Next
        Dim expected As Integer = ExpectedFundingSamples(segStartMs, endMs)
        If haveInRange >= expected Then Return existing.Count

        ' Fetch one interval early (defect 1) and filter back to the segment below.
        Dim samples As List(Of BacktestFundingSample) =
            Await FetchFundingHistoryAsync(segStartMs - FundingIntervalMs, endMs)
        If samples Is Nothing Then
            Console.Error.WriteLine(String.Format(
                "[HistoricalStore] Funding fetch failed for {0:D4}-{1:D2} — keeping {2} stored sample(s)",
                year, month, existing.Count))
            Return existing.Count
        End If

        ' Merge: stored rows survive, fetched rows fill the holes; the deliberate one-interval
        ' over-reach on the start is clipped. Invariant lives in StoreFiles (A51).
        Dim before As Integer = existing.Count
        Dim total As Integer = StoreFiles.MergeAndWriteFunding(path, existing, samples, segStartMs, endMs)
        Console.WriteLine(String.Format(
            "[HistoricalStore] Funding {0:D4}-{1:D2}: {2} stored (+{3} new, expected {4} in range)",
            year, month, total, total - before, expected))
        Return total
    End Function

    ''' <summary>How many hourly samples SHOULD sit in [startMs, endMsIncl]. The candle path
    ''' uses the same grid arithmetic at its own interval — one helper, two callers.</summary>
    Public Shared Function ExpectedFundingSamples(startMs As Long, endMsIncl As Long) As Integer
        Return StoreFiles.ExpectedGridPoints(startMs, endMsIncl, FundingIntervalMs)
    End Function

    Public Shared Async Function FetchFundingHistoryAsync(startMs As Long, endMs As Long) As Task(Of List(Of BacktestFundingSample))
        Dim url As String = "https://www.deribit.com/api/v2/public/get_funding_rate_history" &
                            "?instrument_name=" & InstrumentName &
                            "&start_timestamp=" & startMs &
                            "&end_timestamp=" & endMs
        For attempt As Integer = 1 To 2
            Dim needsRetry As Boolean = False
            Try
                Dim json = Await _http.GetStringAsync(url)
                Dim doc  = JsonDocument.Parse(json)
                Dim result = doc.RootElement.GetProperty("result")
                Dim list As New List(Of BacktestFundingSample)()
                For Each row In result.EnumerateArray()
                    Dim s As New BacktestFundingSample()
                    s.TsMs = row.GetProperty("timestamp").GetInt64()
                    ' Prefer interest_8h (the projected 8h rate; matches DeribitClient.GetFundingRateAsync's
                    ' funding_8h source). Fall back to interest_1h*8 if 8h absent.
                    Dim rateEl As JsonElement = Nothing
                    If row.TryGetProperty("interest_8h", rateEl) Then
                        s.Rate = rateEl.GetDouble()
                    ElseIf row.TryGetProperty("interest_1h", rateEl) Then
                        s.Rate = rateEl.GetDouble() * 8.0
                    Else
                        s.Rate = 0.0
                    End If
                    list.Add(s)
                Next
                Return list
            Catch ex As HttpRequestException
                If attempt < 2 AndAlso ex.StatusCode.HasValue AndAlso CInt(ex.StatusCode.Value) >= 500 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Funding HTTP failure: " & ex.Message)
                    Return Nothing
                End If
            Catch ex As TaskCanceledException
                If attempt < 2 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Funding timeout")
                    Return Nothing
                End If
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] Funding error: " & ex.Message)
                Return Nothing
            End Try
            If needsRetry Then Await Task.Delay(500)
        Next
        Return Nothing
    End Function

    Public Shared Function LoadFundingRange(warmupStartUtc As DateTime, toUtc As DateTime) As List(Of BacktestFundingSample)
        Dim all As New List(Of BacktestFundingSample)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            all.AddRange(StoreFiles.LoadFundingFile(FundingFileFor(m.Year, m.Month)))
        Next
        all.Sort(Function(a, b) a.TsMs.CompareTo(b.TsMs))
        Return all
    End Function

    ' ── Top-level fetch orchestration ─────────────────────────────────────────────────

    ''' <summary>Fetch (fetch-once) all data for [fromUtc, toUtc] plus a warm-up prefix.
    ''' Warm-up is 3 hours: enough for 250×1m, 210×5m and 70×15m windows at the leftmost
    ''' bar-close (max window = 70×15 = 1050 min = 17.5 h; the trade window is 500 trades
    ''' — a few minutes at most on BTC-PERPETUAL). Actually 20 h to be safe.</summary>
    Public Shared Async Function BackfillAllAsync(fromUtc As DateTime, toUtc As DateTime) As Task
        Dim warmupStart As DateTime = fromUtc.AddHours(-20)
        EnsureStoreDir()

        For Each res In New Integer() {1, 3, 5, 15}
            For Each m In EnumerateMonths(warmupStart, toUtc)
                Dim n As Integer = Await BackfillCandleMonthAsync(res, m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
                Console.WriteLine(String.Format("[HistoricalStore] Candles {0}m {1:D4}-{2:D2}: {3} rows on disk",
                                                res, m.Year, m.Month, n))
                Await Task.Delay(PoliteDelayMs)
            Next
        Next

        For Each m In EnumerateMonths(warmupStart, toUtc)
            Await BackfillFundingMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
            Await Task.Delay(PoliteDelayMs)
        Next

        For Each m In EnumerateMonths(warmupStart, toUtc)
            Await BackfillTradeMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
        Next
    End Function

End Class
