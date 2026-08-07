' Core/TradeStoreWriter.vb
' [v64 in-app trade-store capture] Host-agnostic, NETWORK-FREE writer/parser for the raw
' trade store (docs/in-app-trade-store-capture-proposal.md §2).
'
' This file owns the ONE place that knows the trade store's file naming, monthly rollover,
' row format, parse and monotonic append guard. Three consumers route through it:
'
'   • DeribitWsFeed.ApplyTrades   — streaming capture (§1.1), the PRIMARY mechanism
'   • HistoricalStore.Backfill*   — the network backfill / gap repair (§1.2)
'   • HistoricalStore.LoadTradeRange — the reader (delegates its per-file parse here)
'
' Because writer and reader share this seam they cannot drift — the "one seam, no copies"
' move already made for SignalEmitter.ComputeSideLevels.
'
' Deliberately NO HttpClient and no networking of any kind. That is the entire point of
' the §2 split: HistoricalStore owns a live HttpClient, so linking it into the app's feed
' path and into the fixture project was unacceptable. This file links everywhere.
'
' NEVER THROWS. A disk error logs to console and drops the batch — the
' SignalEmitter.TryWrite / liq_events.log discipline. Losing capture must never kill the
' feed or an analysis run.
'
' Fixtures: A48a (round-trip vs the shipped reader), A48b (monotonic guard),
' A48c (month rollover + header-on-create), A48e (unwritable path never throws),
' A48f (enabled:false ⇒ zero writes), A48h (exe-relative resolution, CWD-independent).

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text.Json

Public NotInheritable Class TradeStoreWriter

    ''' <summary>Store directory as it ships in settings.json (`trade_store.store_dir`).
    ''' Resolved against the EXE directory by <see cref="ResolveStoreDir"/> — see D3/A48h.</summary>
    Public Const DefaultStoreDir As String = "backtest_data"

    ''' <summary>
    ''' The store's trade-file header — SEVEN columns since the trade-identity build
    ''' (docs/trade-store-trade-identity-proposal.md §3.2, D5).
    '''
    ''' The two identity columns APPEND at the end, and that is the whole migration:
    ''' <see cref="TryParseRow"/> guards on `parts.Length &lt; 5` — a `&lt;`, not an `=` — so
    ''' five-column files written by every prior binary parse unchanged. No file rotation, no
    ''' rewrite of existing months, and a single file legitimately holds both shapes (§5).
    ''' </summary>
    Public Const HeaderLine As String = "Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq"

    ''' <summary>The pre-identity five-column header, still written by every binary before this
    ''' build. Kept as a named constant because the reader must recognise it as a HEADER (and
    ''' skip it) rather than as data.</summary>
    Public Const LegacyHeaderLine As String = "Timestamp,Price,Amount,Direction,Liquidation"

    ''' <summary>Sentinel for an absent <see cref="TradeRecord.TradeSeq"/> — an ALIAS of
    ''' <see cref="TradeRecord.AbsentSeq"/>, which is where it is defined. It lives on
    ''' TradeRecord because AutoTweaker, WhatIfRunner and CeilingAudit link DeribitClient.vb but
    ''' NOT this file; defining it here would break those three builds. Kept as an alias so
    ''' store-side call sites read against the store seam.</summary>
    Public Const AbsentSeq As Long = TradeRecord.AbsentSeq

    ' One process-wide append lock. Streaming capture and gap repair both append to the
    ' SAME monthly file, on different threads, so without this a flush could interleave
    ' mid-line with a backfill page. Ordering ACROSS the two is not guaranteed and does not
    ' need to be — LoadTradeRange sorts by timestamp and dedups on the whole line.
    Private Shared ReadOnly _appendLock As New Object()

    ' ── Instance state (the streaming buffer) ─────────────────────────────────────────

    Private ReadOnly _storeDir As String
    Private ReadOnly _pending As New List(Of TradeRecord)()

    ' Monotonic guard: the newest timestamp this writer has committed. -1 = not yet seeded
    ' from disk. Seeding lazily (rather than in the ctor) keeps construction free of I/O
    ' and means a writer built before the store exists still guards correctly.
    Private _lastTs As Long = -1
    Private _seeded As Boolean = False

    Public Sub New(storeDir As String)
        _storeDir = If(String.IsNullOrWhiteSpace(storeDir), DefaultStoreDir, storeDir)
    End Sub

    ''' <summary>The resolved directory this writer appends to.</summary>
    Public ReadOnly Property StoreDir As String
        Get
            Return _storeDir
        End Get
    End Property

    ''' <summary>Trades buffered but not yet flushed. The D2 count trigger reads this.</summary>
    Public ReadOnly Property PendingCount As Integer
        Get
            SyncLock _pending
                Return _pending.Count
            End SyncLock
        End Get
    End Property

    ''' <summary>Newest committed timestamp, or -1 before the first commit/seed. Test surface.</summary>
    Public ReadOnly Property LastWrittenTimestamp As Long
        Get
            SyncLock _pending
                Return _lastTs
            End SyncLock
        End Get
    End Property

    ' [C1 Session 2 / Part B — trade-store-coverage-report-proposal.md §4] Wall-clock flush
    ' bookkeeping for the live TAPE STORE status element. Distinct from _lastTs above: _lastTs
    ' is a TRADE timestamp (the monotonic dedup guard), these are WALL-CLOCK facts about this
    ' writer INSTANCE's own life — "when did disk last actually receive something" and "how
    ' much have I committed since I was constructed". A flush proves the WHOLE chain reached
    ' disk; a buffered trade only proves the stream got it.
    Private _lastFlushUtc As DateTime? = Nothing
    Private _totalRowsWritten As Long = 0

    ''' <summary>[Part B] Wall-clock time of the last flush that committed ≥1 row, or Nothing
    ''' if this writer instance has never successfully flushed anything.</summary>
    Public ReadOnly Property LastFlushUtc As DateTime?
        Get
            SyncLock _pending
                Return _lastFlushUtc
            End SyncLock
        End Get
    End Property

    ''' <summary>[Part B] Rows committed by THIS writer instance since construction — "this
    ''' process" for the streaming writer DeribitWsFeed owns, since a store_dir hot-reload or
    ''' an app restart both construct a fresh writer. Deliberately NOT reset by
    ''' ResetBufferState (a WS reconnect is still the same process capturing the same tape,
    ''' not a reason to zero the counter).</summary>
    Public ReadOnly Property TotalRowsWritten As Long
        Get
            SyncLock _pending
                Return _totalRowsWritten
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' Buffer one streamed trade. The monotonic guard drops anything at or before the
    ''' newest committed timestamp, which is what makes reconnect re-seed idempotent:
    ''' SeedAsync re-seeds the trade ring from REST on every (re)connect, so the same
    ''' trades WILL arrive twice (A48b).
    ''' Returns True when the trade was accepted into the buffer.
    ''' </summary>
    Public Function Buffer(t As TradeRecord) As Boolean
        SyncLock _pending
            EnsureSeeded(t.Timestamp)
            If t.Timestamp <= _lastTs Then Return False
            _pending.Add(t)
            ' Advance the guard on BUFFER, not on flush: a batch arriving before the flush
            ' timer fires would otherwise re-admit its own duplicates.
            _lastTs = t.Timestamp
            Return True
        End SyncLock
    End Function

    ''' <summary>
    ''' Write the buffered trades and clear the buffer. Never throws — on a disk error the
    ''' batch is logged and DROPPED (gap repair recovers it). Returns rows written.
    ''' </summary>
    Public Function Flush() As Integer
        Dim batch As List(Of TradeRecord)
        SyncLock _pending
            If _pending.Count = 0 Then Return 0
            batch = New List(Of TradeRecord)(_pending)
            _pending.Clear()
        End SyncLock
        Dim written As Integer = AppendRows(_storeDir, batch)
        If written > 0 Then
            SyncLock _pending
                _lastFlushUtc = DateTime.UtcNow
                _totalRowsWritten += written
            End SyncLock
        End If
        Return written
    End Function

    ''' <summary>
    ''' Called from DeribitWsFeed.SeedAsync on every (re)connect. Flushes anything already
    ''' buffered (a reconnect must not silently discard captured tape) and then un-seeds the
    ''' monotonic guard so it re-reads the on-disk high-water mark — which is what makes the
    ''' re-seeded REST window idempotent against whatever is already stored.
    ''' </summary>
    Public Sub ResetBufferState()
        Flush()
        SyncLock _pending
            _pending.Clear()
            _lastTs = -1
            _seeded = False
        End SyncLock
    End Sub

    ' Seed the guard from the on-disk high-water mark of the month the first trade lands in.
    ' Caller holds _pending.
    Private Sub EnsureSeeded(tsMs As Long)
        If _seeded Then Return
        _seeded = True
        Dim utc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime
        Dim onDisk As Long = LastTradeTimestamp(TradeFileFor(_storeDir, utc.Year, utc.Month))
        If onDisk > _lastTs Then _lastTs = onDisk
    End Sub

    ' ── Part B — live TAPE STORE status tier ──────────────────────────────────────────
    ' [C1 Session 2 — trade-store-coverage-report-proposal.md §4] Pure, host-agnostic, so the
    ' harness reaches it directly (A49m) without a live feed. Deliberately takes no date/day-
    ' of-week input at all — that absence IS the "stays unconditional on weekends" guarantee
    ' the weekday-scope-ruling-2026-08-03.md requires of Part B: there is nothing here that
    ' COULD suppress a Saturday/Sunday reading, unlike Part A's per-hour classification.

    ''' <summary>Amber past 3× flush_seconds since the last successful flush, red past 10×
    ''' (the proposal's own thresholds). "UNKNOWN" when secondsSinceFlush is Nothing — this
    ''' writer instance has never successfully flushed yet (freshly constructed / cold start,
    ''' not a fault).</summary>
    ''' <summary>
    ''' [Session 2 review finding, 2026-08-05] "UNKNOWN" alone is unbounded in time: A48e pins
    ''' that an unwritable store (blocked dir, locked file, full disk) NEVER THROWS —
    ''' AppendRows logs and returns 0 forever, so a permanently dead capture path is
    ''' indistinguishable from a genuine cold start unless the classifier is given a second
    ''' clock. `secondsSinceStart` (time since this process began expecting capture — the
    ''' status element's own first tick) is that clock: while no flush has EVER landed, a dead
    ''' path escalates UNKNOWN → AMBER → RED on the SAME 3×/10× thresholds a stale flush would
    ''' trip, instead of staying neutral forever.
    ''' </summary>
    Public Shared Function ClassifyTapeStoreTier(secondsSinceFlush As Double?, secondsSinceStart As Double,
                                                 flushSeconds As Integer) As String
        Dim safeFlush As Double = Math.Max(1, flushSeconds)
        Dim thresholdAmber As Double = 3.0 * safeFlush
        Dim thresholdRed As Double = 10.0 * safeFlush

        If secondsSinceFlush.HasValue Then
            If secondsSinceFlush.Value >= thresholdRed Then Return "RED"
            If secondsSinceFlush.Value >= thresholdAmber Then Return "AMBER"
            Return "NORMAL"
        End If

        ' Never flushed yet — judge against how long we've been waiting instead.
        If secondsSinceStart >= thresholdRed Then Return "RED"
        If secondsSinceStart >= thresholdAmber Then Return "AMBER"
        Return "UNKNOWN"
    End Function

    ' ── Feature gates ─────────────────────────────────────────────────────────────────
    ' [F1] These live here rather than inline at their call sites so the harness tests the
    ' PRODUCTION decision instead of a restatement of it. A48f originally re-stated the
    ' predicate and asserted the copy was false — which would still have passed if the real
    ' gate lost its `Not ts.Enabled` arm. Same class as the A43f lesson: internal consistency
    ' of a mirror proves nothing about the thing it mirrors.

    ''' <summary>The streaming-capture gate. `DeribitWsFeed.ResolveTradeStore` and A48f both
    ''' call this — there is one decision, in one place.</summary>
    Public Shared Function ShouldCapture(ts As TradeStoreSettings) As Boolean
        Return ts IsNot Nothing AndAlso ts.Enabled
    End Function

    ''' <summary>The gap-repair gate. Repair additionally requires its own switch, so the two
    ''' are independent: `enabled:false` stops both, `gap_repair_enabled:false` stops only
    ''' repair. `TradeStoreGapRepair.Start` / `RepairOnceAsync` and A48f all call this.</summary>
    Public Shared Function ShouldGapRepair(ts As TradeStoreSettings) As Boolean
        Return ShouldCapture(ts) AndAlso ts.GapRepairEnabled
    End Function

    ' ── Shared path / format helpers ──────────────────────────────────────────────────

    ''' <summary>
    ''' [D3 / A48h] Resolve `trade_store.store_dir` against the EXE DIRECTORY, never the
    ''' process working directory. The app's cwd is not guaranteed (a shortcut, a service
    ''' host and a debugger all set it differently), and a cwd-relative store would silently
    ''' scatter capture files. An absolute configured path is honoured as-is.
    ''' </summary>
    Public Shared Function ResolveStoreDir(configured As String) As String
        Dim dir As String = If(String.IsNullOrWhiteSpace(configured), DefaultStoreDir, configured.Trim())
        Try
            If Path.IsPathRooted(dir) Then Return Path.GetFullPath(dir)
            Return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir))
        Catch
            ' Malformed path characters — fall back to the literal so the caller still gets
            ' a usable relative dir rather than an exception out of a config read.
            Return dir
        End Try
    End Function

    ''' <summary>Monthly trade file for a store directory. The shipped naming, unchanged.</summary>
    Public Shared Function TradeFileFor(storeDir As String, year As Integer, month As Integer) As String
        Return Path.Combine(If(String.IsNullOrWhiteSpace(storeDir), DefaultStoreDir, storeDir),
                            String.Format("trades_{0:D4}-{1:D2}.csv", year, month))
    End Function

    ''' <summary>
    ''' One store row for a trade — SEVEN fields since the identity build. Absent identity
    ''' writes as an EMPTY column, which <see cref="TryParseRow"/> reads back as absent.
    '''
    ''' ⚠ This is the ON-DISK ROW and nothing else. It used to double as the dedup key; it no
    ''' longer may, because two genuinely distinct trades can share all five legacy fields
    ''' (observed live at the §1 gate: trade_id 439922656 and 439922657, same millisecond,
    ''' same price, same amount, same direction). Dedup goes through
    ''' <see cref="DedupTrades"/>; the five-field fallback key is <see cref="LegacyRowKey"/>.
    ''' </summary>
    Public Shared Function FormatRow(t As TradeRecord) As String
        Return String.Format(CultureInfo.InvariantCulture,
                             "{0},{1},{2}",
                             LegacyRowKey(t), SanitizeField(t.TradeId),
                             If(t.HasSeq, t.TradeSeq.ToString(CultureInfo.InvariantCulture), ""))
    End Function

    ''' <summary>
    ''' The five legacy fields, formatted exactly as every pre-identity binary wrote them.
    ''' Two jobs, and both need it to stay byte-stable: it is the prefix of
    ''' <see cref="FormatRow"/>, and it is the FALLBACK dedup/match key used whenever either
    ''' side of a comparison lacks an identity (§3.4, §3.5).
    ''' </summary>
    Public Shared Function LegacyRowKey(t As TradeRecord) As String
        Return String.Format(CultureInfo.InvariantCulture,
                             "{0},{1:F2},{2:F2},{3},{4}",
                             t.Timestamp, t.Price, t.Amount,
                             If(t.Direction, ""), If(t.Liquidation, "none"))
    End Function

    ' A comma inside a field would silently change the row's column count and corrupt every
    ' later parse. Deribit's trade_id is a numeric string so this is unreachable in practice —
    ' which is exactly why it is stripped rather than trusted.
    Private Shared Function SanitizeField(s As String) As String
        If String.IsNullOrEmpty(s) Then Return ""
        If s.IndexOf(","c) < 0 Then Return s
        Return s.Replace(",", "")
    End Function

    ''' <summary>
    ''' Parse one store row. False (and rec untouched) on any malformed line.
    '''
    ''' The `&lt; 5` guard is load-bearing and predates this build: it is what makes appending
    ''' identity columns backward-compatible in both directions (D5). Five-column legacy rows
    ''' parse with identity ABSENT; seven-column rows parse with it present. A seven-column row
    ''' whose identity columns are EMPTY also reads as absent — an empty column is not a value,
    ''' and treating it as one is the collapse this whole build exists to prevent (§3.4).
    ''' </summary>
    Public Shared Function TryParseRow(line As String, ByRef rec As TradeRecord) As Boolean
        If String.IsNullOrEmpty(line) Then Return False
        Dim parts = line.Split(","c)
        If parts.Length < 5 Then Return False
        Dim ts As Long
        Dim px, amt As Double
        If Not Long.TryParse(parts(0), NumberStyles.Integer, CultureInfo.InvariantCulture, ts) Then Return False
        If Not Double.TryParse(parts(1), NumberStyles.Float, CultureInfo.InvariantCulture, px) Then Return False
        If Not Double.TryParse(parts(2), NumberStyles.Float, CultureInfo.InvariantCulture, amt) Then Return False

        Dim tradeId As String = Nothing
        If parts.Length >= 6 AndAlso Not String.IsNullOrWhiteSpace(parts(5)) Then tradeId = parts(5).Trim()

        Dim tradeSeq As Long = AbsentSeq
        If parts.Length >= 7 Then
            Dim sq As Long
            If Long.TryParse(parts(6).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, sq) AndAlso
               sq >= 0 Then tradeSeq = sq
        End If

        rec = New TradeRecord() With {
            .Timestamp = ts, .Price = px, .Amount = amt,
            .Direction = parts(3), .Liquidation = parts(4),
            .TradeId = tradeId, .TradeSeq = tradeSeq}
        Return True
    End Function

    ' ── Dedup ─────────────────────────────────────────────────────────────────────────

    ''' <summary>
    ''' THE dedup contract (docs/trade-store-trade-identity-proposal.md §3.4), in the one place
    ''' all three consumers route through so they cannot drift:
    '''
    '''   • Two rows are the same trade iff both carry an identity and the identities are equal.
    '''   • If EITHER row lacks an identity, fall back to whole-row equality on the five legacy fields.
    '''   • ⚠ NEVER key on an absent or empty identity. A missing identity is not a value and
    '''     must not join a group. Keying a legacy row on "" collapses EVERY legacy row into
    '''     one — the original defect, reproduced at greater scale inside its own fix.
    '''
    ''' ⚠ IMPLEMENTATION NOTE — the spec's relation is not transitive, so an order had to be
    ''' chosen and it is recorded here rather than left implicit. Given a legacy row L and two
    ''' identified rows I1, I2 that all share the same five fields: L≡I1 and L≡I2 by fallback,
    ''' but I1≢I2 by identity. No grouping can satisfy all three. This resolves IDENTITY-FIRST:
    ''' identified rows are settled among themselves, then an identity-less row is dropped if its
    ''' legacy key was already claimed by an identified row. That makes the result independent of
    ''' input order (the alternative silently returned 1 or 2 rows depending on which came first)
    ''' and errs toward NOT double-counting, which is the conservative direction for a store whose
    ''' whole problem was inflated volume on merge.
    ''' </summary>
    Public Shared Function DedupTrades(rows As IEnumerable(Of TradeRecord)) As List(Of TradeRecord)
        Dim result As New List(Of TradeRecord)()
        If rows Is Nothing Then Return result

        ' Materialise once — the caller may hand us a lazy sequence and this walks it twice.
        Dim all As New List(Of TradeRecord)(rows)

        Dim seenIds As New HashSet(Of String)(StringComparer.Ordinal)
        Dim claimedLegacy As New HashSet(Of String)(StringComparer.Ordinal)
        Dim keep(all.Count - 1) As Boolean

        ' Pass 1 — identified rows, settled among themselves on identity ALONE. Two rows here
        ' with equal legacy fields and different ids are two trades and both survive (A53e).
        For i As Integer = 0 To all.Count - 1
            Dim r = all(i)
            If Not r.HasIdentity Then Continue For
            If Not seenIds.Add(r.TradeId) Then Continue For
            claimedLegacy.Add(LegacyRowKey(r))
            keep(i) = True
        Next

        ' Pass 2 — identity-less rows, on the five-field fallback key. Each distinct legacy row
        ' survives (A53c); one already represented by an identified row does not (A53f).
        For i As Integer = 0 To all.Count - 1
            Dim r = all(i)
            If r.HasIdentity Then Continue For
            If Not claimedLegacy.Add(LegacyRowKey(r)) Then Continue For
            keep(i) = True
        Next

        ' Emit in INPUT order. The two-pass resolution above is about which rows survive, not
        ' about what order they come back in — a caller that does not sort (and the coverage
        ' walk's gap arithmetic did not, before it sorted) must not silently get the file
        ' re-ordered underneath it.
        For i As Integer = 0 To all.Count - 1
            If keep(i) Then result.Add(all(i))
        Next

        Return result
    End Function

    ''' <summary>
    ''' Read one monthly trade file. THE per-file parse HistoricalStore.LoadTradeRange
    ''' delegates to, so a round-trip through this pair proves byte-compatibility with the
    ''' shipped reader (A48a). Missing file / unreadable file ⇒ empty list, never throws.
    ''' </summary>
    Public Shared Function ReadTradeFile(path As String) As List(Of TradeRecord)
        Dim result As New List(Of TradeRecord)()
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return result
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim rec As New TradeRecord()
                    If TryParseRow(line, rec) Then result.Add(rec)
                Loop
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[TradeStoreWriter] ReadTradeFile failed: " & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>Newest timestamp in a monthly trade file, or -1 when absent/empty/unreadable.
    ''' Rows are append-order so the last data line carries it.</summary>
    Public Shared Function LastTradeTimestamp(path As String) As Long
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return -1
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim prev As String = Nothing
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    prev = line
                Loop
                If prev Is Nothing Then Return -1
                Dim comma As Integer = prev.IndexOf(","c)
                If comma <= 0 Then Return -1
                Dim n As Long
                If Long.TryParse(prev.Substring(0, comma), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, n) Then Return n
            End Using
        Catch
        End Try
        Return -1
    End Function

    ''' <summary>
    ''' The timestamp a backfill should start fetching from for one monthly file, or -1 when
    ''' the window is ALREADY COVERED and there is nothing to fetch.
    '''
    ''' This is the arithmetic behind "gap-repair overlap is a no-op by construction" (§1.2 /
    ''' A48d): streaming has already written up to a few seconds ago, so a repair pass over
    ''' the same ground resumes past its own end and fetches nothing. Extracted onto this seam
    ''' — rather than left inline in the network backfill — so the claim is testable without a
    ''' live HTTP call, and so the live decision and the tested decision are the same code.
    ''' </summary>
    ''' <param name="clampToSegStart">True ⇒ never reach further back than segStartMs. Deribit
    ''' refuses trade windows past its ~24 h retention, so after a long outage an unclamped
    ''' resume cursor asks for a refused window and recovers NOTHING — including the recent
    ''' hours that are still served. Only the in-app gap repair passes True; the historical
    ''' backfill passes False so it still fills a hole between its resume point and segEnd.</param>
    Public Shared Function ResolveResumeCursorMs(path As String,
                                                 segStartMs As Long,
                                                 segEndInclMs As Long,
                                                 clampToSegStart As Boolean) As Long
        Dim resumeMs As Long = LastTradeTimestamp(path)
        Dim cursorMs As Long
        If resumeMs > 0 Then
            cursorMs = resumeMs + 1
            If clampToSegStart AndAlso cursorMs < segStartMs Then cursorMs = segStartMs
        Else
            cursorMs = segStartMs
        End If
        If cursorMs > segEndInclMs Then Return -1
        Return cursorMs
    End Function

    ''' <summary>
    ''' Append rows to the store, splitting by calendar month so a batch straddling a month
    ''' boundary lands in two files (A48c). The header is written ONLY when a file is
    ''' created. Never throws: a disk error logs and returns the rows written so far.
    ''' Returns the number of rows committed.
    ''' </summary>
    Public Shared Function AppendRows(storeDir As String, rows As IEnumerable(Of TradeRecord)) As Integer
        If rows Is Nothing Then Return 0
        Dim dir As String = If(String.IsNullOrWhiteSpace(storeDir), DefaultStoreDir, storeDir)

        ' Group by (year, month) preserving arrival order within each group.
        Dim byMonth As New Dictionary(Of String, List(Of TradeRecord))()
        Dim order As New List(Of String)()
        For Each t In rows
            Dim utc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(t.Timestamp).UtcDateTime
            Dim key As String = String.Format("{0:D4}-{1:D2}", utc.Year, utc.Month)
            Dim bucket As List(Of TradeRecord) = Nothing
            If Not byMonth.TryGetValue(key, bucket) Then
                bucket = New List(Of TradeRecord)()
                byMonth(key) = bucket
                order.Add(key)
            End If
            bucket.Add(t)
        Next
        If order.Count = 0 Then Return 0

        Dim written As Integer = 0
        SyncLock _appendLock
            Try
                Directory.CreateDirectory(dir)
            Catch ex As Exception
                Console.Error.WriteLine("[TradeStoreWriter] cannot create store dir '" & dir & "': " & ex.Message)
                Return 0
            End Try

            For Each key In order
                Dim y As Integer = Integer.Parse(key.Substring(0, 4), CultureInfo.InvariantCulture)
                Dim m As Integer = Integer.Parse(key.Substring(5, 2), CultureInfo.InvariantCulture)
                Dim path As String = TradeFileFor(dir, y, m)
                Try
                    Dim isNewFile As Boolean = Not File.Exists(path)
                    Using sw As New StreamWriter(path, append:=Not isNewFile)
                        If isNewFile Then sw.WriteLine(HeaderLine)
                        For Each t In byMonth(key)
                            sw.WriteLine(FormatRow(t))
                            written += 1
                        Next
                    End Using
                Catch ex As Exception
                    ' Disk full, path unwritable, file locked — log and drop. The feed and
                    ' the analysis run must be unaffected (A48e).
                    Console.Error.WriteLine("[TradeStoreWriter] append to '" & path & "' failed: " & ex.Message)
                End Try
            Next
        End SyncLock
        Return written
    End Function

End Class
