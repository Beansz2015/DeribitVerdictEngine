' Core/TradeStoreWriter.vb
' [v64 in-app trade-store capture] Host-agnostic, NETWORK-FREE writer/parser for the raw
' trade store (docs/in-app-trade-store-capture-proposal.md §2).
'
' This file owns the ONE place that knows the trade store's file naming, monthly rollover,
' row format, parse and identity-keyed append guard. Three consumers route through it:
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
' Fixtures: A48a (round-trip vs the shipped reader), A48b (write guard vs replay),
' A48c (month rollover + header-on-create), A48e (unwritable path never throws),
' A48f (enabled:false ⇒ zero writes), A48h (exe-relative resolution, CWD-independent),
' A55a-g (the write guard keyed on IDENTITY — docs/trade-store-write-guard-identity-proposal.md).

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

    ' ── The write guard (docs/trade-store-write-guard-identity-proposal.md §3.2) ───────
    '
    ' ⚠ THIS REPLACED A MONOTONIC TIMESTAMP HIGH-WATER MARK, and the reason is the whole
    ' point of the file. The shipped guard was `If t.Timestamp <= _lastTs Then Return False`
    ' — a MILLISECOND USED AS AN IDENTITY. It is not one: Deribit reports a market order
    ' sweeping several price levels as several records sharing one millisecond, so the writer
    ' kept the first leg and silently discarded the rest. Measured at 49.2 % of the tape
    ' against the live wire, and 69 of 70 multi-trade timestamps arrived inside a SINGLE
    ' notification batch (§1a). `_lastTs` is GONE — not demoted to a pre-filter, because a
    ' pre-filter reinstates the defect exactly: same-millisecond siblings would fail it and
    ' never reach the check below (§0 trap 1).
    '
    ' The guard is still needed: the WS replays prints on re-subscribe after a reconnect, and a
    ' fresh writer can open a store that already holds them. Only its KEY was wrong.
    '
    ' ⚠ WHERE THE DUPLICATES DO *NOT* COME FROM, corrected 2026-08-11 — v64's comment here and
    ' at both DeribitWsFeed call sites credited SeedAsync's REST re-seed, and this file repeated
    ' it. `Buffer` has exactly ONE caller (`DeribitWsFeed.ApplyTrades`); SeedAsync's REST trades
    ' go to `MarketState.SeedTrades`, the in-memory ring, and never reach the store. Gap repair
    ' reaches the file through `AppendRows` and bypasses this guard entirely. So the replay
    ' window this must cover is a WS re-subscribe, not a 500-trade REST fetch — which is why
    ' RecentWindowCapacity below is generous rather than merely sufficient.
    '
    ' ⚠ THE BIAS IS DELIBERATE AND ASYMMETRIC (§3.1). A duplicate on disk is harmless — the
    ' read path dedups it and A48d already pins that. A dropped trade is unrecoverable past
    ' Deribit's ~24 h retention. So every ambiguous case here resolves toward WRITING the row.
    ' Anything that makes this guard stricter is a defect; anything that makes it looser is
    ' at worst a wasted row.
    '
    ' Bounded recent-trade window, testing MEMBERSHIP rather than a tolerance. Not a
    ' `trade_seq` high-water mark (D-2, ratified): a high-water mark IS a tolerance, and
    ' whether Deribit ever resets `trade_seq` was never verified — a reset, a wrap or one
    ' out-of-order batch would silently drop everything behind the mark, which is the exact
    ' failure class being fixed.

    ''' <summary>
    ''' [D-3, ruled 2026-08-11] How many recently-seen trades the write guard remembers. A
    ''' CONSTANT, not a settings key: it has no failure-rate linkage, nobody will ever tune it,
    ''' and a key would cost a version bump and a boundary question for nothing.
    '''
    ''' 20,000 trades is ≈5–10 h of tape at the measured true rate (~31–60 trades/min) and
    ''' costs a few MB of strings — far longer than any reconnect replay window, which is all
    ''' the guard actually has to cover. Public so fixtures read the PRODUCTION number instead
    ''' of restating it (the F1 lesson below).
    ''' </summary>
    Public Const RecentWindowCapacity As Integer = 20000

    ' One remembered trade, with both its keys precomputed so eviction never re-formats.
    ' Id is Nothing when the row carries no identity — ABSENT, which is not a value and must
    ' never become a key (§0 trap 2).
    Private Structure WindowEntry
        Public Id As String
        Public LegacyKey As String
    End Structure

    ' Insertion-ordered window + refcounted key indexes. Refcounts rather than plain sets
    ' because two DISTINCT trades can legitimately share a legacy key (that is A53e), so
    ' evicting one must not un-remember the other.
    Private ReadOnly _window As New Queue(Of WindowEntry)()
    Private ReadOnly _windowIds As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
    Private ReadOnly _windowLegacy As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

    ' False until the window has been populated from the on-disk tail. Seeding lazily (rather
    ' than in the ctor) keeps construction free of I/O and means a writer built before the
    ' store exists still guards correctly.
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

    ''' <summary>Trades currently remembered by the write guard, capped at
    ''' <see cref="RecentWindowCapacity"/>. Test surface — A55e needs the window BOUNDARY, and
    ''' guessing at it from the outside would be restating the implementation.</summary>
    Public ReadOnly Property RecentWindowCount As Integer
        Get
            SyncLock _pending
                Return _window.Count
            End SyncLock
        End Get
    End Property

    ' [C1 Session 2 / Part B — trade-store-coverage-report-proposal.md §4] Wall-clock flush
    ' bookkeeping for the live TAPE STORE status element. Distinct from the write guard above:
    ' that one keys on TRADE identity, these are WALL-CLOCK facts about this writer INSTANCE's
    ' own life — "when did disk last actually receive something" and "how much have I
    ' committed since I was constructed". A flush proves the WHOLE chain reached
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
    ''' Buffer one streamed trade — the store's ONLY inbound path, called from
    ''' DeribitWsFeed.ApplyTrades and nowhere else. The guard drops a trade already in this
    ''' writer's recent window, which is what makes a WS re-subscribe replay idempotent (A48b,
    ''' A55b). Two DISTINCT trades sharing a millisecond both survive (A55a) — that is the
    ''' defect this replaced.
    ''' Returns True when the trade was accepted into the buffer.
    ''' </summary>
    Public Function Buffer(t As TradeRecord) As Boolean
        SyncLock _pending
            EnsureSeeded(t.Timestamp)
            If AlreadyCommitted(t) Then Return False
            _pending.Add(t)
            ' Advance the window on BUFFER, not on flush: a batch arriving before the flush
            ' timer fires would otherwise re-admit its own duplicates (§3.4).
            Remember(t)
            Return True
        End SyncLock
    End Function

    ' ⚠ THE SAME RELATION <see cref="DedupTrades"/> DEFINES, in streaming form — one contract,
    ' two call sites, no copies. Caller holds _pending.
    '
    '   • An IDENTIFIED trade is settled on identity ALONE. Its legacy fields are irrelevant,
    '     which is exactly why the sibling case works: two trades on one millisecond with equal
    '     price, amount and direction but different trade_id are two trades (A53e, A55a).
    '   • An IDENTITY-LESS trade falls back to whole-row equality on the five legacy fields.
    '   • ⚠ NEVER key on an absent or empty identity. Keying identity-less rows on "" would
    '     collapse every one of them into a single group — the original defect, reproduced at
    '     greater scale inside its own fix (§0 trap 2, A53c, A55d).
    '
    ' ⚠ One deliberate divergence from DedupTrades, and it resolves toward ADMITTING. DedupTrades
    ' settles identified rows FIRST so its result is order-independent; a streaming guard only
    ' ever sees arrival order and cannot look ahead. So if an identity-less row arrives before an
    ' identified row sharing its legacy fields, BOTH are written where DedupTrades would have
    ' kept one. That is a duplicate on disk, which the read path removes — the harmless
    ' direction under §3.1, and the only direction this guard is ever allowed to err in.
    Private Function AlreadyCommitted(t As TradeRecord) As Boolean
        If t.HasIdentity Then Return _windowIds.ContainsKey(t.TradeId)
        Return _windowLegacy.ContainsKey(LegacyRowKey(t))
    End Function

    ' Remember one trade and evict past the cap. An identified trade registers BOTH keys — its
    ' identity, and its legacy key so a later identity-less re-delivery of the same trade is
    ' still recognised (the `claimedLegacy` arm of DedupTrades Pass 1). Caller holds _pending.
    Private Sub Remember(t As TradeRecord)
        Dim e As WindowEntry
        e.Id = If(t.HasIdentity, t.TradeId, Nothing)
        e.LegacyKey = LegacyRowKey(t)
        _window.Enqueue(e)
        If e.Id IsNot Nothing Then BumpKey(_windowIds, e.Id, 1)
        BumpKey(_windowLegacy, e.LegacyKey, 1)

        Do While _window.Count > RecentWindowCapacity
            Dim old As WindowEntry = _window.Dequeue()
            If old.Id IsNot Nothing Then BumpKey(_windowIds, old.Id, -1)
            BumpKey(_windowLegacy, old.LegacyKey, -1)
        Loop
    End Sub

    ' Refcount one key, removing it at zero so the dictionaries stay bounded by the window.
    Private Shared Sub BumpKey(counts As Dictionary(Of String, Integer), key As String, delta As Integer)
        Dim n As Integer = 0
        counts.TryGetValue(key, n)
        n += delta
        If n <= 0 Then
            counts.Remove(key)
        Else
            counts(key) = n
        End If
    End Sub

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
    ''' guard so its window is rebuilt from the on-disk tail — which is what makes the
    ''' re-seeded REST window idempotent against whatever is already stored.
    ''' </summary>
    ''' <remarks>
    ''' [F2, 2026-09-07] The whole body is under ONE SyncLock. It used to be
    ''' `Flush() : SyncLock _pending { Clear... }` -- two separate acquisitions with a gap
    ''' between them, and that gap DROPPED TAPE. Flush() releases _pending before AppendRows
    ''' does its disk I/O, so a trade buffered by the WS receive loop in that window was then
    ''' erased by the _pending.Clear() below without ever being written. Same loss class as the
    ''' 49.2 % write-guard defect, reached by a different route.
    '''
    ''' Monitor is RE-ENTRANT, so Flush()'s own inner `SyncLock _pending` is a no-op recursion
    ''' here -- that is what makes the one-lock form legal rather than a self-deadlock.
    '''
    ''' ⚠ VERIFIED BEFORE WIDENING, because widening a lock over file I/O is how deadlocks are
    ''' introduced: nothing in the tree acquires _appendLock and then _pending. Inside the
    ''' `SyncLock _appendLock` region of AppendRows there are ZERO _pending references, so the
    ''' ordering is strictly _pending -> _appendLock and never the reverse.
    '''
    ''' ⚠ CONSEQUENCE, stated rather than hidden: AppendRows' disk write now runs while
    ''' _pending is held, so the WS buffering path blocks for that write. It happens only on
    ''' (re)connect, and stalling the buffer briefly is strictly better than silently dropping
    ''' the trades it holds -- which is the trade this project has already made twice.
    ''' </remarks>
    Public Sub ResetBufferState()
        SyncLock _pending
            Flush()
            _pending.Clear()
            _window.Clear()
            _windowIds.Clear()
            _windowLegacy.Clear()
            _seeded = False
        End SyncLock
    End Sub

    ' [D-4(a), ruled 2026-08-11] Seed the window from the TAIL of the month file the first trade
    ' lands in, so a restart does not re-write the rows it already holds. Caller holds _pending.
    '
    ' ⚠ Every failure here must produce DUPLICATES, never drops (§3.1). ReadTradeFileTail
    ' returns an empty list on a missing or unreadable file rather than throwing, so an
    ' unseedable window simply admits everything and leaves the read path to dedup.
    '
    ' Only the FIRST trade's month is read, matching the shipped behaviour. A restart in the
    ' first moments of a new month therefore seeds from an empty file and may re-admit a few
    ' rows from the previous month's tail — duplicates, in the safe direction, and not worth a
    ' second file read on every writer construction.
    Private Sub EnsureSeeded(tsMs As Long)
        If _seeded Then Return
        _seeded = True
        Dim utc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime
        For Each r In ReadTradeFileTail(TradeFileFor(_storeDir, utc.Year, utc.Month), RecentWindowCapacity)
            Remember(r)
        Next
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

    ''' <summary>
    ''' The LAST <paramref name="maxRows"/> parseable rows of a monthly trade file, in file
    ''' order. Missing / unreadable file ⇒ empty list, never throws.
    '''
    ''' Exists so the write guard's window can be seeded (D-4) without holding a whole month in
    ''' memory: a month of tape at the true rate is well over a million rows, and this runs on
    ''' the WS message thread inside the writer's lock. One streaming pass with a bounded ring —
    ''' the same single pass <see cref="LastTradeTimestamp"/> already makes, so seeding costs no
    ''' more file I/O than the guard it replaced.
    ''' </summary>
    Public Shared Function ReadTradeFileTail(path As String, maxRows As Integer) As List(Of TradeRecord)
        Dim ring As New Queue(Of TradeRecord)()
        If maxRows <= 0 Then Return New List(Of TradeRecord)()
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return New List(Of TradeRecord)()
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim rec As New TradeRecord()
                    If TryParseRow(line, rec) Then
                        ring.Enqueue(rec)
                        If ring.Count > maxRows Then ring.Dequeue()
                    End If
                Loop
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[TradeStoreWriter] ReadTradeFileTail failed: " & ex.Message)
        End Try
        Return New List(Of TradeRecord)(ring)
    End Function

    ''' <summary>Newest timestamp in a monthly trade file, or -1 when absent/empty/unreadable.
    ''' Rows are append-order so the last data line carries it.
    '''
    ''' ⚠ RESIDUAL, recorded rather than fixed (§3.5): this reads the file's LAST LINE, not its
    ''' MAXIMUM timestamp. The store already holds one out-of-order block, so the invariant is
    ''' already violated once. Harmless today — the write guard no longer depends on it, and its
    ''' other caller (<see cref="ResolveResumeCursorMs"/>) wants a resume point, not a
    ''' maximum.</summary>
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

    ' ── Hole-derived repair windows (docs/trade-store-downtime-repair-proposal.md Part A) ──
    '
    ' ⚠ WHY ResolveResumeCursorMs ABOVE IS NOT ENOUGH, and it is not a refinement of it.
    ' That function seeds the fetch cursor from the file's LAST WRITTEN ROW. Once streaming
    ' reconnects after an outage, that row is current again, so every hole BEHIND it reads as
    ' "already covered" and is never fetched. Measured instance: a 60.3-minute hole on
    ' 2026-08-11 (08:59:56 -> 10:00:12 UTC, zero rows) survived seven hours and two scheduled
    ' repair passes, then aged past Deribit's ~24 h retention and became unrecoverable.
    '
    ' ⚠ WIDENING gap_repair_lookback_hours DOES NOT FIX THIS and is the single most likely
    ' wrong turn (proposal §0 trap 4). The lookback moves segStartMs earlier; the cursor is
    ' still lastTs + 1. The cursor skips the HOLE, not the WINDOW.
    '
    ' Detection is on trade_seq ALONE — never on a time gap (D-2, ratified). A time threshold
    ' is a TOLERANCE, and this project's recorded store-integrity lesson is that a guard
    ' checking a fixed tolerance rather than completeness turns one bad fetch into permanent
    ' silent loss. ASIA at 03:00 is legitimately sparse; a tolerance would either miss real
    ' holes or invent them. trade_seq gives completeness with no threshold at all.

    ''' <summary>
    ''' [D-4, ruled 2026-08-12] Backstop against a pathological era, not a tuning knob. The
    ''' pre-fix era holds 7,471 gap runs; without a cap one pass over it would issue 7,471 REST
    ''' fetches. No failure-rate linkage, nobody will tune it, and a key would cost a version
    ''' bump and a dataset-boundary question for nothing.
    ''' </summary>
    Public Const MaxHolesPerPass As Integer = 32

    ''' <summary>
    ''' Row cap for one scan. Required by the proposal's §4.1 step 1 ("bounded by a constant row
    ''' cap") but not named in its D-4 constant table, so it is ruled here on D-4's own stated
    ''' principle — no failure-rate linkage, nobody will tune it.
    '''
    ''' 500,000 rows is ~7× a 20 h lookback at the measured true rate (~31–60 trades/min) and is
    ''' held as two Longs per row, not as TradeRecords — a bounded ~8 MB, not the ~50 MB the
    ''' records would cost. When the cap bites, the NEWEST rows are kept (the recoverable
    ''' ground) and the truncation is LOGGED — a silent cap reads as "covered everything".
    ''' </summary>
    Public Const MaxScanRows As Integer = 500000

    ' ── Seq-range repair windows (GR-1 (d), docs/gap-repair-same-ms-page-skip-spec.md §4.2) ──
    '
    ' ⛔ WHY REPAIR IS KEYED BY trade_seq AND NOT BY TIME. The time windows this replaced were
    ' [prev.ts + 1, cur.ts − 1]. A lost trade that shares a millisecond with a stored neighbour
    ' sits OUTSIDE that range, so no pass could ever fetch it — and 62.34 % of September's trades
    ' share a millisecond with a trade_seq neighbour. The time pager's `newestMs + 1` cursor
    ' created exactly that shape at every full-page boundary: 16 permanent holes (70 trades) in
    ' the 2026-08-17 start-up repair. A hole IS a missing sequence range, so fetching the range
    ' itself is exact, has no millisecond edge, and says in the code what is being repaired.

    ''' <summary>What a repair window asks the venue for.</summary>
    Public Enum RepairWindowKind
        ''' <summary>A trade_seq-bracketed hole: fetch exactly [FirstSeq, LastSeq].</summary>
        Hole
        ''' <summary>The trailing window after the newest stored row that carries a sequence:
        ''' fetch from FirstSeq, open-ended, committing nothing timestamped after StopAfterMs.</summary>
        Tail
        ''' <summary>A trailing window with no stored sequence to start from (an empty file, a
        ''' legacy last row, or the offline backfill's resume point): ONE time-endpoint call with
        ''' count=1 at AnchorMs resolves the first sequence, then it pages by sequence.</summary>
        AnchoredTail
        ''' <summary>[DUP-2] The month file's scan FAILED. Carries the failure text; nothing is
        ''' fetched for the file, and the pass reads PASS_FAILED. Never treat it as an empty store.</summary>
        ScanFailure
        ''' <summary>[DUP-2] The F-1 previous-month seed read FAILED. Emitted first; the current
        ''' file's own holes and tail follow, with no cross-month hole.</summary>
        SeedReadFailure
    End Enum

    ''' <summary>
    ''' One window a repair pass fetches, keyed by trade_seq. A value type: the resolver returns at
    ''' most <see cref="MaxHolesPerPass"/> + 1 of these. Unused sequence fields hold
    ''' <see cref="AbsentSeq"/>; unused timestamps hold 0.
    ''' </summary>
    Public Structure RepairWindow
        Public Kind As RepairWindowKind
        ''' <summary>First trade_seq to fetch, inclusive. AbsentSeq for an AnchoredTail.</summary>
        Public FirstSeq As Long
        ''' <summary>Last trade_seq to fetch, inclusive. AbsentSeq ⇒ open-ended (tails).</summary>
        Public LastSeq As Long
        ''' <summary>AnchoredTail only: the time-endpoint start_timestamp.</summary>
        Public AnchorMs As Long
        ''' <summary>Tails only: commit nothing timestamped after this. ⛔ A seq tail with no
        ''' end_seq runs to "now" (measured 2026-09-14), so without this a month-boundary pass
        ''' re-fetches the NEXT month's already-captured trades and double-writes them.</summary>
        Public StopAfterMs As Long
        ''' <summary>Holes only: the bracketing rows' timestamps. For the repair log, never for
        ''' fetching.</summary>
        Public LeftTsMs As Long
        Public RightTsMs As Long
        ''' <summary>[DUP-2] Failure kinds only: "&lt;ExceptionType&gt;: &lt;message&gt;".</summary>
        Public Failure As String

        ''' <summary>Holes only: sequences the hole is missing — the MaxHolesPerPass ranking key.
        ''' 0 for a tail.</summary>
        Public ReadOnly Property MissingSeqs As Long
            Get
                If Kind <> RepairWindowKind.Hole Then Return 0L
                Return LastSeq - FirstSeq + 1L
            End Get
        End Property

        Public Shared Function ForHole(prevSeq As Long, prevTsMs As Long,
                                       curSeq As Long, curTsMs As Long) As RepairWindow
            Dim w As New RepairWindow()
            w.Kind = RepairWindowKind.Hole
            w.FirstSeq = prevSeq + 1L
            w.LastSeq = curSeq - 1L
            w.LeftTsMs = prevTsMs
            w.RightTsMs = curTsMs
            Return w
        End Function

        Public Shared Function ForTail(lastStoredSeq As Long, stopAfterMs As Long) As RepairWindow
            Dim w As New RepairWindow()
            w.Kind = RepairWindowKind.Tail
            w.FirstSeq = lastStoredSeq + 1L
            w.LastSeq = AbsentSeq
            w.StopAfterMs = stopAfterMs
            Return w
        End Function

        Public Shared Function ForAnchoredTail(anchorMs As Long, stopAfterMs As Long) As RepairWindow
            Dim w As New RepairWindow()
            w.Kind = RepairWindowKind.AnchoredTail
            w.FirstSeq = AbsentSeq
            w.LastSeq = AbsentSeq
            w.AnchorMs = anchorMs
            w.StopAfterMs = stopAfterMs
            Return w
        End Function

        Public Shared Function ForScanFailure(failure As String) As RepairWindow
            Dim w As New RepairWindow()
            w.Kind = RepairWindowKind.ScanFailure
            w.FirstSeq = AbsentSeq
            w.LastSeq = AbsentSeq
            w.Failure = failure
            Return w
        End Function

        Public Shared Function ForSeedReadFailure(failure As String) As RepairWindow
            Dim w As New RepairWindow()
            w.Kind = RepairWindowKind.SeedReadFailure
            w.FirstSeq = AbsentSeq
            w.LastSeq = AbsentSeq
            w.Failure = failure
            Return w
        End Function
    End Structure

    ''' <summary>
    ''' What one repair window achieved — network-free, so the repair log and the fixtures read it
    ''' without HistoricalStore's HttpClient. States and their meaning:
    ''' docs/gap-repair-same-ms-page-skip-spec.md §4.3.
    '''
    ''' ⚠ "Not served" is counted, never assumed away. A start_seq older than the venue's ~24 h
    ''' retention returns trades from the retention EDGE, not an empty list (measured 2026-09-14),
    ''' so a pager that took the first served trade as its start would report a clean repair over
    ''' a range the venue no longer holds.
    ''' </summary>
    Public NotInheritable Class RepairWindowOutcome
        Public Const HoleRepaired As String = "HOLE_REPAIRED"
        Public Const HolePartial As String = "HOLE_PARTIAL"
        Public Const HoleNotServed As String = "HOLE_NOT_SERVED"
        Public Const TailOk As String = "TAIL_OK"
        Public Const TailPastRetention As String = "TAIL_PAST_RETENTION"
        Public Const TailGap As String = "TAIL_GAP"
        Public Const TailEmpty As String = "TAIL_EMPTY"
        Public Const FetchFailed As String = "FETCH_FAILED"
        Public Const PageCap As String = "PAGE_CAP"
        Public Const NoProgress As String = "NO_PROGRESS"
        Public Const ScanFailed As String = "SCAN_FAILED"
        Public Const SeedReadFailed As String = "SEED_READ_FAILED"

        ''' <summary>The month file this window repaired (file name only).</summary>
        Public Property FileName As String = ""
        Public Property Window As RepairWindow
        Public Property State As String = ""
        ''' <summary>Rows AppendRows reported written.</summary>
        Public Property Committed As Integer
        ''' <summary>Sequences skipped before the first served trade (the venue's retention edge).</summary>
        Public Property NotServedBefore As Long
        ''' <summary>Sequences missing between served trades.</summary>
        Public Property NotServedInside As Long
        ''' <summary>Holes only: sequences after the last served trade up to LastSeq.</summary>
        Public Property NotServedAfter As Long
        ''' <summary>Returned trades never committed: no trade_seq, outside the bounds, or a repeat.</summary>
        Public Property Rejected As Integer
        ''' <summary>Venue requests made, the anchor call included.</summary>
        Public Property Pages As Integer
        Public Property Reason As String = ""
        ''' <summary>The first sequence actually requested — FirstSeq, or the anchor's result.</summary>
        Public Property StartSeq As Long = AbsentSeq
        ''' <summary>The last sequence committed, or AbsentSeq if none.</summary>
        Public Property LastServedSeq As Long = AbsentSeq

        Public ReadOnly Property NotServed As Long
            Get
                Return NotServedBefore + NotServedInside + NotServedAfter
            End Get
        End Property

        Public ReadOnly Property IsFailure As Boolean
            Get
                Return State = FetchFailed OrElse State = PageCap OrElse State = NoProgress OrElse
                       State = ScanFailed OrElse State = SeedReadFailed
            End Get
        End Property
    End Class

    ' One scanned row, reduced to the only two fields hole detection needs. Seq is
    ' AbsentSeq when the row carries none. Friend (not Private): ScanForRepair below is Friend
    ' so A56g can drive it directly with a small cap, and a Friend function cannot expose a
    ' Private type through its signature.
    Friend Structure SeqPoint
        Public TsMs As Long
        Public Seq As Long
    End Structure

    ''' <summary>
    ''' Every window a repair pass should fetch for one monthly file — the trade_seq holes BEHIND
    ''' the tail, then the tail itself. An empty list means there is nothing to fetch.
    '''
    ''' <para><b>The tail.</b> The newest sorted row carries a sequence ⇒ a
    ''' <see cref="RepairWindowKind.Tail"/> from that sequence + 1, stopping at segEndInclMs. No
    ''' rows ⇒ an <see cref="RepairWindowKind.AnchoredTail"/> at segStartMs. A legacy (seq-less)
    ''' newest row ⇒ an AnchoredTail at its timestamp + 1 — ⚠ that +1 can still miss legacy
    ''' same-millisecond siblings, and the legacy era closed on 2026-08-10.</para>
    '''
    ''' <para>⚠ The tail is emitted even for a store that is already current (newest row at
    ''' segEndInclMs): its fetch then commits nothing. The old "covered store ⇒ empty list"
    ''' property does not survive the move to sequences, because a covered store can still lack
    ''' same-millisecond siblings of its newest row. ResolveResumeCursorMs (the offline path) is
    ''' unchanged.</para>
    ''' </summary>
    ''' <param name="clampToSegStart">Applies only to a legacy AnchoredTail's anchor. Sequence
    ''' windows need no clamp: the venue serves from its own retention edge and the fetcher counts
    ''' the rest as not served.</param>
    ''' <param name="previousMonthPath">[F-1] The previous month's file. When given, and this file has
    ''' rows but none below segStartMs, that file's newest row seeds the bracket — so the gap across
    ''' 00:00 UTC on the 1st is a hole. Nothing ⇒ single-file behaviour.</param>
    Public Shared Function ResolveRepairWindows(path As String,
                                                segStartMs As Long,
                                                segEndInclMs As Long,
                                                clampToSegStart As Boolean,
                                                Optional previousMonthPath As String = Nothing) As List(Of RepairWindow)
        Return ResolveRepairWindowsCore(path, segStartMs, segEndInclMs, clampToSegStart, previousMonthPath, MaxScanRows)
    End Function

    ''' <summary>ResolveRepairWindows with the scan cap as a parameter, so A79k reaches the truncation
    ''' path without 500,000 rows (the ScanForRepair precedent). Production passes MaxScanRows.</summary>
    Friend Shared Function ResolveRepairWindowsCore(path As String,
                                                    segStartMs As Long,
                                                    segEndInclMs As Long,
                                                    clampToSegStart As Boolean,
                                                    previousMonthPath As String,
                                                    maxScanRows As Integer) As List(Of RepairWindow)
        Dim result As New List(Of RepairWindow)()

        ' ── 1. Scan ───────────────────────────────────────────────────────────────────
        Dim truncated As Boolean = False
        Dim scanFailure As String = Nothing
        Dim rows As List(Of SeqPoint) = ScanForRepair(path, segStartMs, maxScanRows, truncated, scanFailure)
        ' ⛔ [DUP-1/DUP-2] A FAILED scan is not an empty store. Reading it as one made three September
        ' passes re-fetch and re-append the whole 20 h lookback (298,932 duplicate rows,
        ' docs/trade-store-duplicate-rows-read-2026-09-15.md). Fetch nothing for this file, say so,
        ' and let the next pass retry.
        If scanFailure IsNot Nothing Then
            result.Add(RepairWindow.ForScanFailure(scanFailure))
            Return result
        End If

        ' ── 1b. [F-1] Seed a cross-month bracket (docs/gap-repair-cross-month-gap-spec.md §2) ──
        ' ⛔ WHY. ScanForRepair reads ONE month file, and a September file never holds an August
        ' row. So once streaming has written September's first rows, the range from August's last
        ' stored seq to September's first stored seq was a hole in NEITHER scan: August's tail stops
        ' at August's end (GT-3) and September's tail starts after September's newest row.
        '
        ' Seeded only when ALL hold, each for a reason:
        '   • not truncated — the rows a cut dropped sit between the seed and the survivors, and the
        '     walk would report ground the store holds as a hole (the same-file bracket rule);
        '   • this file has rows — an EMPTY file already gets the anchored tail at segStartMs, which
        '     fetches every trade from there (CF-3);
        '   • this file has no row below segStartMs — a same-file bracket wins.
        ' The seed is the previous file's NEWEST row, legacy or not (CF-2): a legacy seed breaks the
        ' walk exactly as trap 2 does, where the newest SEQ-CARRYING row would bracket across a newer
        ' legacy row and invent a phantom. ScanForRepair is called unchanged: every previous-month row
        ' sits below segStartMs, so it returns that file's single bracket and nothing to walk.
        '
        ' ⛔ ORDER INVARIANT — no double-write needs no partition. A repair pass resolves months in
        ' ASCENDING order (HistoricalStore.EnumerateMonths, walked sequentially by
        ' TradeStoreGapRepair.RepairOnceAsync). So the previous month's tail has ALREADY committed
        ' its trades when this seed is read, and the seed is that month's newest row AFTER its
        ' repair: this hole starts exactly where the previous month's tail stopped. Each seq is
        ' committed — and each gap counted — by one window. If the previous month's tail failed,
        ' the seed is its unrepaired newest row, and this hole commits those trades itself, once.
        ' A caller that resolves months out of order breaks this; A79j part 1 pins it.
        If Not truncated AndAlso rows.Count > 0 AndAlso Not String.IsNullOrWhiteSpace(previousMonthPath) AndAlso
           Not rows.Exists(Function(r) r.TsMs < segStartMs) Then
            Dim prevTruncated As Boolean = False
            Dim prevFailure As String = Nothing
            Dim prevRows As List(Of SeqPoint) = ScanForRepair(previousMonthPath, segStartMs, maxScanRows, prevTruncated, prevFailure)
            If prevFailure IsNot Nothing Then
                ' ⛔ [DUP-2, SF-3] A failed seed read is LOUD, never a silent fall back to single-file
                ' behaviour. This file's own holes and tail still run — without a seed there is no
                ' cross-month hole, so nothing phantom — and the pass reads PASS_FAILED.
                result.Add(RepairWindow.ForSeedReadFailure(prevFailure))
            Else
                Dim haveSeed As Boolean = False
                Dim seed As SeqPoint
                For Each p In prevRows
                    If p.TsMs >= segStartMs Then Continue For
                    If Not haveSeed OrElse p.TsMs > seed.TsMs OrElse (p.TsMs = seed.TsMs AndAlso p.Seq > seed.Seq) Then
                        seed = p
                        haveSeed = True
                    End If
                Next
                If haveSeed Then rows.Add(seed)
            End If
        End If

        ' ── 2. Sort. ⚠ TRAP 1, AND IT IS NON-NEGOTIABLE ───────────────────────────────
        ' The store is NOT sorted, and LastTradeTimestamp's own summary records why: repair
        ' appends its pages AFTER whatever streaming has already written, and the store already
        ' holds one out-of-order block. Walking the file in append order reports a PHANTOM HOLE
        ' at every repair-block boundary, and each phantom costs a REST fetch.
        '
        ' Sorted by (Timestamp, TradeSeq), not by Timestamp alone. List.Sort is unstable, and
        ' same-millisecond siblings are the defining feature of this tape — a market order
        ' sweeping several levels reports as several records sharing one millisecond. Ordering
        ' those arbitrarily would manufacture negative deltas inside a millisecond.
        rows.Sort(Function(a, b)
                      Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
                      If c <> 0 Then Return c
                      Return a.Seq.CompareTo(b.Seq)
                  End Function)

        ' ── 3–4. Walk the sequence-carrying rows and emit holes ───────────────────────
        Dim holes As New List(Of RepairWindow)()
        Dim prev As SeqPoint
        Dim hasPrev As Boolean = False

        For Each cur In rows
            ' ⚠ TRAP 2 — an absent trade_seq is NOT a sequence number. TradeRecord.AbsentSeq is
            ' −1 and every pre-2026-08-10 row carries it, so feeding it into the arithmetic
            ' makes a legacy→identified boundary look like a hole ~296 million wide. That count
            ' would then win the MaxHolesPerPass ranking outright and evict every real hole.
            ' Same lesson as the write guard's "⚠ NEVER key on an absent identity", one seam over.
            '
            ' ⚠ A seq-less row BREAKS the walk rather than being skipped past, and the
            ' difference is the whole point. Skipping past would bracket two identified rows
            ' ACROSS an interleaved block of legacy rows and report the ground those legacy rows
            ' already cover as a hole — a phantom, in exactly the mixed-era store trap 2 is
            ' about. Breaking can only ever MISS a hole in legacy ground, never invent one, and
            ' D-6 rules that the pre-fix era is not to be chased. Costs nothing today: every row
            ' written since 2026-08-10 carries a sequence, so inside the lookback the two
            ' readings are identical.
            If cur.Seq < 0 Then
                hasPrev = False
                Continue For
            End If

            If hasPrev Then
                Dim delta As Long = cur.Seq - prev.Seq
                ' delta = 0 is a surviving duplicate, delta < 0 a discontinuity (whether Deribit
                ' ever RESETS trade_seq was never verified project-wide). Neither is loss, and
                ' neither emits a window.
                If delta > 1L Then
                    ' The missing RANGE itself. delta > 1 guarantees FirstSeq <= LastSeq, so a
                    ' sequence window can never invert — the "unfetchable" drop is gone with it.
                    holes.Add(RepairWindow.ForHole(prev.Seq, prev.TsMs, cur.Seq, cur.TsMs))
                End If
            End If

            prev = cur
            hasPrev = True
        Next

        ' ── 5–6. No clamp, no inverted-range drop (GR-1 (d), amends DR-1) ───────────────
        ' DR-1 (docs/downtime-repair-followups-implementer-briefs.md §1) kept one drop here: a time
        ' window that inverted, "because there is no sub-millisecond query to issue". ⛔ That
        ' premise was false — Deribit's time bounds are both inclusive (measured 2026-09-14) —
        ' and the drop discarded exactly the same-millisecond losses repair most needs. A sequence
        ' window cannot invert, and it needs no time clamp: ScanForRepair already scopes holes to
        ' the lookback, and the venue serves from its own retention edge, which the fetcher
        ' counts as not served rather than retrying forever.

        ' ── 7. Cap, keeping the LARGEST, and say what was dropped ─────────────────────
        Dim dropped As Integer = 0
        If holes.Count > MaxHolesPerPass Then
            holes.Sort(Function(a, b) b.MissingSeqs.CompareTo(a.MissingSeqs))
            dropped = holes.Count - MaxHolesPerPass
            holes.RemoveRange(MaxHolesPerPass, dropped)
        End If
        ' Back into sequence order — the ranking above is about WHICH holes survive, not about
        ' what order they come back in, and the trailing window must still be last.
        holes.Sort(Function(a, b) a.FirstSeq.CompareTo(b.FirstSeq))
        result.AddRange(holes)

        If dropped > 0 OrElse truncated Then
            Console.WriteLine(String.Format(
                "[TradeStoreWriter] repair scan '{0}': {1} hole(s) returned, {2} dropped by the " &
                "MaxHolesPerPass={3} cap{4}",
                path, result.Count, dropped, MaxHolesPerPass,
                If(truncated, ", scan TRUNCATED at MaxScanRows=" & MaxScanRows, "")))
        End If

        ' ── 8. The trailing window, last ──────────────────────────────────────────────
        If rows.Count = 0 Then
            If segStartMs <= segEndInclMs Then result.Add(RepairWindow.ForAnchoredTail(segStartMs, segEndInclMs))
        Else
            Dim newest As SeqPoint = rows(rows.Count - 1)
            If newest.TsMs <= segEndInclMs Then
                If newest.Seq >= 0 Then
                    result.Add(RepairWindow.ForTail(newest.Seq, segEndInclMs))
                Else
                    Dim anchor As Long = newest.TsMs + 1L
                    If clampToSegStart AndAlso anchor < segStartMs Then anchor = segStartMs
                    If anchor <= segEndInclMs Then result.Add(RepairWindow.ForAnchoredTail(anchor, segEndInclMs))
                End If
            End If
        End If

        Return result
    End Function

    ''' <summary>
    ''' One streaming pass over a monthly file, keeping the rows the hole walk needs: every row
    ''' at or after <paramref name="segStartMs"/>, plus the single row with the greatest
    ''' timestamp BELOW it.
    '''
    ''' <para>⚠ That one earlier row is the bracket. Without it a hole STRADDLING segStartMs has
    ''' nothing on its left to bracket against and is invisible, and its missing sequence range is
    ''' never fetched. It is kept whether or not it carries
    ''' a sequence: a seq-less bracket correctly BREAKS the walk instead of licensing a phantom
    ''' hole across the boundary.</para>
    '''
    ''' <para>⚠ DR-2 (docs/downtime-repair-followups-implementer-briefs.md §2). After truncation
    ''' the retained rows must stay CONTIGUOUS IN TIME — no interior gaps — because the caller
    ''' sorts by (Timestamp, TradeSeq) and walks brackets across whatever survives. A cut that
    ''' removes rows by FILE POSITION rather than by TIME leaves interior gaps on an
    ''' out-of-order store (repair pages append after streaming, so file order and time order
    ''' differ), and the walk reports each gap as a PHANTOM hole with a huge missing-sequence
    ''' count — which then wins the MaxHolesPerPass ranking and evicts every real hole. So when
    ''' the cap bites: sort once, drop the oldest block BY TIME, and remember the new floor —
    ''' every row read afterward that falls below the floor is discarded as it arrives rather
    ''' than being re-admitted and removed again later.</para>
    '''
    ''' <paramref name="maxScanRows"/> is a parameter (not a direct read of
    ''' <see cref="MaxScanRows"/>) so a fixture can drive a small cap and reach the truncation
    ''' path without constructing 500,000 rows; the production call site passes the constant.
    '''
    ''' Missing / unreadable file ⇒ empty list, never throws — the ReadTradeFile discipline.
    ''' </summary>
    Friend Shared Function ScanForRepair(path As String, segStartMs As Long, maxScanRows As Integer,
                                         ByRef truncated As Boolean, ByRef failure As String) As List(Of SeqPoint)
        Dim inWindow As New List(Of SeqPoint)()
        truncated = False
        failure = Nothing
        ' A missing file is NOT a failure: a month file does not exist before its first trade (SF-6).
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return inWindow

        Dim haveBracket As Boolean = False
        Dim bracket As SeqPoint
        ' Nothing until the cap first bites. Once set, no row below it may re-enter the window —
        ' that is what keeps the retained set contiguous after a cut (see the doc comment above).
        Dim floorMs As Long? = Nothing

        Try
            ' [DUP-2] Share mode ReadWrite (OpenStoreForScan), so this scan opens beside a streaming
            ' flush and the flush opens beside this scan. ⛔ SF-8: read only to the last line feed
            ' present at open — a row still being appended, or torn by a crash, is not read. A row
            ' torn inside its trade_seq would parse as a TINY seq and start a tail at the venue's
            ' retention edge.
            Using fs As FileStream = OpenStoreForScan(path),
                  sr As New StreamReader(New BoundedReadStream(fs, LastCompleteLineEnd(fs)))
                sr.ReadLine()   ' header
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim rec As New TradeRecord()
                    If Not TryParseRow(line, rec) Then Continue Do

                    Dim p As SeqPoint
                    p.TsMs = rec.Timestamp
                    p.Seq = rec.TradeSeq

                    If p.TsMs < segStartMs Then
                        ' The bracket is the MAXIMUM below segStartMs, not the last one seen —
                        ' the file is not sorted, so those are different rows.
                        If Not haveBracket OrElse p.TsMs > bracket.TsMs OrElse
                           (p.TsMs = bracket.TsMs AndAlso p.Seq > bracket.Seq) Then
                            bracket = p
                            haveBracket = True
                        End If
                        Continue Do
                    End If

                    If floorMs.HasValue AndAlso p.TsMs < floorMs.Value Then Continue Do

                    inWindow.Add(p)
                    If inWindow.Count > maxScanRows Then
                        ' Sort ONCE at the cut — not on every overflow, which would be
                        ' O(n log n) per row — then drop the oldest block BY TIME so the
                        ' survivors have no interior gap.
                        inWindow.Sort(Function(a, b)
                                          Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
                                          If c <> 0 Then Return c
                                          Return a.Seq.CompareTo(b.Seq)
                                      End Function)
                        Dim dropCount As Integer = Math.Max(1, maxScanRows \ 10)
                        If dropCount > inWindow.Count Then dropCount = inWindow.Count
                        inWindow.RemoveRange(0, dropCount)
                        truncated = True
                        floorMs = inWindow(0).TsMs
                    End If
                Loop
            End Using
        Catch ex As Exception
            ' ⛔ [DUP-1/DUP-2, SF-4] Report it, and discard EVERYTHING read so far. The rows before the
            ' exception are no rows or an old bracket — exactly what made a pass re-fetch its whole
            ' lookback. The caller turns this into SCAN_FAILED; it must never look like an empty store.
            failure = ex.GetType().Name & ": " & ex.Message
            Console.Error.WriteLine("[TradeStoreWriter] ScanForRepair failed: " & failure)
            truncated = False
            Return New List(Of SeqPoint)()
        End Try

        ' ⚠ A truncated scan invalidates the bracket. The rows the cap discarded sit BELOW the
        ' floor, which may now sit ABOVE the bracket's own timestamp; bracketing across them
        ' would report ground those discarded rows cover as a hole. Drop it and let the walk
        ' start clean.
        If haveBracket AndAlso Not truncated Then inWindow.Add(bracket)
        Return inWindow
    End Function

    ''' <summary>[DUP-2] How the repair path opens a store file: read access, share mode ReadWrite.
    ''' A plain StreamReader shares Read only — it cannot open while the streaming writer holds the
    ''' file, and while it is open the writer's own open fails and AppendRows drops the batch (B-3,
    ''' both halves). Friend so A79o holds exactly this handle.</summary>
    Friend Shared Function OpenStoreForScan(path As String) As FileStream
        Return New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    End Function

    ''' <summary>[DUP-2, SF-8] Bytes up to and including the last line feed present now. Rows are
    ''' ~60 bytes, so the last 64 KB always holds one unless the file has no complete row there.</summary>
    Friend Shared Function LastCompleteLineEnd(fs As FileStream) As Long
        Dim len As Long = fs.Length
        If len <= 0 Then Return 0L
        Dim block As Integer = CInt(Math.Min(65536L, len))
        fs.Position = len - block
        Dim buf(block - 1) As Byte
        Dim got As Integer = 0
        While got < block
            Dim n As Integer = fs.Read(buf, got, block - got)
            If n <= 0 Then Exit While
            got += n
        End While
        fs.Position = 0
        For i As Integer = got - 1 To 0 Step -1
            If buf(i) = 10 Then Return len - block + i + 1
        Next
        Return len - block
    End Function

    ' A read-only view of the first `limit` bytes of a stream. It never disposes the inner stream.
    Private NotInheritable Class BoundedReadStream
        Inherits Stream

        Private ReadOnly _inner As Stream
        Private _remaining As Long

        Public Sub New(inner As Stream, limit As Long)
            _inner = inner
            _remaining = Math.Max(0L, limit)
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property

        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property

        Public Overrides Sub Flush()
        End Sub

        Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
            If _remaining <= 0 OrElse count <= 0 Then Return 0
            Dim n As Integer = _inner.Read(buffer, offset, CInt(Math.Min(CLng(count), _remaining)))
            If n > 0 Then _remaining -= n
            Return n
        End Function

        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function

        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class

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
