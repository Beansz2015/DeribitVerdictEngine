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
    Public Sub ResetBufferState()
        Flush()
        SyncLock _pending
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

    ''' <summary>A closed, inclusive millisecond range — one window a repair pass should fetch.
    ''' Deliberately a value type holding two Longs: the scan below keeps up to
    ''' <see cref="MaxScanRows"/> of these and must not retain whole TradeRecords.</summary>
    Public Structure LongRange
        Public ReadOnly StartMs As Long
        Public ReadOnly EndInclMs As Long

        Public Sub New(startMs As Long, endInclMs As Long)
            Me.StartMs = startMs
            Me.EndInclMs = endInclMs
        End Sub

        ''' <summary>Inclusive width. An empty or inverted range reports 0 or less.</summary>
        Public ReadOnly Property WidthMs As Long
            Get
                Return EndInclMs - StartMs + 1L
            End Get
        End Property
    End Structure

    ' One scanned row, reduced to the only two fields hole detection needs. Seq is
    ' AbsentSeq when the row carries none. Friend (not Private): ScanForRepair below is Friend
    ' so A56g can drive it directly with a small cap, and a Friend function cannot expose a
    ' Private type through its signature.
    Friend Structure SeqPoint
        Public TsMs As Long
        Public Seq As Long
    End Structure

    ' A detected hole, carried with its missing-sequence count so the MaxHolesPerPass cap can
    ' rank by SIZE rather than by position.
    Private Structure Hole
        Public Range As LongRange
        Public MissingSeqs As Long
    End Structure

    ''' <summary>
    ''' Every window a repair pass should fetch for one monthly file — the holes BEHIND the
    ''' tail, then the tail itself. An empty list means there is nothing to fetch.
    '''
    ''' <para><b>The relationship to <see cref="ResolveResumeCursorMs"/>, stated precisely
    ''' because a looser version of it appears in the proposal.</b> The LAST window returned is
    ''' the trailing window, and for an IN-ORDER store its start is exactly today's
    ''' ResolveResumeCursorMs result; an empty list is exactly today's −1. Everything before it
    ''' is strictly additional, which makes A48d's "gap-repair overlap is a no-op by
    ''' construction" a special case of this function rather than something the change has to be
    ''' argued not to have broken.</para>
    '''
    ''' <para>⚠ For an OUT-OF-ORDER store the two differ, deliberately: ResolveResumeCursorMs
    ''' reads the file's LAST LINE (it says so in its own summary), this reads the MAXIMUM
    ''' timestamp after sorting. Max is the correct choice here and cannot under-cover — the
    ''' span between the last line and the maximum is bracketed by rows at BOTH ends, so any
    ''' sequence gap inside it is emitted as a hole by the walk below.</para>
    ''' </summary>
    ''' <param name="clampToSegStart">As <see cref="ResolveResumeCursorMs"/> — True ⇒ never reach
    ''' further back than segStartMs, so the fetch stays inside Deribit's ~24 h retention. Only
    ''' the in-app gap repair passes True.</param>
    Public Shared Function ResolveRepairWindowsMs(path As String,
                                                  segStartMs As Long,
                                                  segEndInclMs As Long,
                                                  clampToSegStart As Boolean) As List(Of LongRange)
        Dim result As New List(Of LongRange)()

        ' ── 1. Scan ───────────────────────────────────────────────────────────────────
        Dim truncated As Boolean = False
        Dim rows As List(Of SeqPoint) = ScanForRepair(path, segStartMs, MaxScanRows, truncated)

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
        Dim holes As New List(Of Hole)()
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
                    Dim h As Hole
                    h.Range = New LongRange(prev.TsMs + 1L, cur.TsMs - 1L)
                    h.MissingSeqs = delta - 1L
                    holes.Add(h)
                End If
            End If

            prev = cur
            hasPrev = True
        Next

        ' ── 5–6. Clamp each hole into the window, dropping only the UNFETCHABLE ones ──
        ' ⚠ TRAP 3 — the clamp goes on EACH HOLE, not only on the pass's outer window. A hole
        ' past Deribit's retention would otherwise be found, fetched, return nothing, and be
        ' found again at the next pass, forever.
        '
        ' ⚠ NO WIDTH FLOOR (DR-1, docs/downtime-repair-followups-implementer-briefs.md §1).
        ' Detection is on trade_seq completeness alone (D-2); a time threshold applied to the
        ' output of that check is the exact tolerance-over-completeness pattern this project's
        ' own store-integrity lesson warns against. The only drop kept here is a range that
        ' INVERTS after clamping — `prev.Ts = cur.Ts` or the two rows one millisecond apart —
        ' which is not a tolerance but a hard fact: the venue API takes a time range, so there
        ' is no sub-millisecond query to issue. That drop is LOGGED below, because a silent
        ' drop here is the same mistake in miniature.
        Dim kept As New List(Of Hole)()
        Dim unfetchable As Integer = 0
        For Each h In holes
            Dim s As Long = Math.Max(h.Range.StartMs, segStartMs)
            Dim e As Long = Math.Min(h.Range.EndInclMs, segEndInclMs)
            If e < s Then
                unfetchable += 1
                Continue For
            End If
            Dim k As Hole
            k.Range = New LongRange(s, e)
            k.MissingSeqs = h.MissingSeqs
            kept.Add(k)
        Next

        ' ── 7. Cap, keeping the LARGEST, and say what was dropped ─────────────────────
        Dim dropped As Integer = 0
        If kept.Count > MaxHolesPerPass Then
            kept.Sort(Function(a, b) b.MissingSeqs.CompareTo(a.MissingSeqs))
            dropped = kept.Count - MaxHolesPerPass
            kept.RemoveRange(MaxHolesPerPass, dropped)
        End If
        ' Back into chronological order — the ranking above is about WHICH holes survive, not
        ' about what order they come back in, and the trailing window must still be last.
        kept.Sort(Function(a, b) a.Range.StartMs.CompareTo(b.Range.StartMs))
        For Each k In kept
            result.Add(k.Range)
        Next

        If dropped > 0 OrElse truncated OrElse unfetchable > 0 Then
            Console.WriteLine(String.Format(
                "[TradeStoreWriter] repair scan '{0}': {1} hole(s) returned, {2} dropped by the " &
                "MaxHolesPerPass={3} cap, {4} unfetchable (sub-millisecond after clamp){5}",
                path, result.Count, dropped, MaxHolesPerPass, unfetchable,
                If(truncated, ", scan TRUNCATED at MaxScanRows=" & MaxScanRows, "")))
        End If

        ' ── 8. The trailing window, last ──────────────────────────────────────────────
        Dim tailStart As Long
        If rows.Count = 0 Then
            tailStart = segStartMs
        Else
            tailStart = rows(rows.Count - 1).TsMs + 1L
            If clampToSegStart AndAlso tailStart < segStartMs Then tailStart = segStartMs
        End If
        If tailStart <= segEndInclMs Then result.Add(New LongRange(tailStart, segEndInclMs))

        Return result
    End Function

    ''' <summary>
    ''' One streaming pass over a monthly file, keeping the rows the hole walk needs: every row
    ''' at or after <paramref name="segStartMs"/>, plus the single row with the greatest
    ''' timestamp BELOW it.
    '''
    ''' <para>⚠ That one earlier row is the bracket. Without it a hole STRADDLING segStartMs has
    ''' nothing on its left to bracket against and is invisible — which would make the clamp in
    ''' step 5 unreachable for the case it exists to serve. It is kept whether or not it carries
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
                                         ByRef truncated As Boolean) As List(Of SeqPoint)
        Dim inWindow As New List(Of SeqPoint)()
        truncated = False
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return inWindow

        Dim haveBracket As Boolean = False
        Dim bracket As SeqPoint
        ' Nothing until the cap first bites. Once set, no row below it may re-enter the window —
        ' that is what keeps the retained set contiguous after a cut (see the doc comment above).
        Dim floorMs As Long? = Nothing

        Try
            Using sr As New StreamReader(path)
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
            Console.Error.WriteLine("[TradeStoreWriter] ScanForRepair failed: " & ex.Message)
        End Try

        ' ⚠ A truncated scan invalidates the bracket. The rows the cap discarded sit BELOW the
        ' floor, which may now sit ABOVE the bracket's own timestamp; bracketing across them
        ' would report ground those discarded rows cover as a hole. Drop it and let the walk
        ' start clean.
        If haveBracket AndAlso Not truncated Then inWindow.Add(bracket)
        Return inWindow
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
