' MarketState.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' Thread-safe latest-snapshot store fed by DeribitWsFeed's single receive loop
' (single-writer) and read by future analysis runs (multi-reader). All mutation and
' reads are guarded by one lock; readers receive COPIES so an external caller can never
' mutate live state or race the writer. Host-agnostic — no WinForms, no UI types.
'
' Holdings (each carries a LastUpdateUtc):
'   - 4 rolling candle series keyed by resolution: "1"(250) "3"(250) "5"(210) "15"(70).
'     Caps match the live fetch counts (MainForm_Analysis.vb 1m=250/5m=210/15m=70, exec 3m=250).
'   - Trade ring buffer, cap 5000, CHRONOLOGICAL ASCENDING (oldest first — the F1 contract).
'   - Top-of-book ladder: latest depth-limited OrderBookSnapshot (top-10).
'   - Ticker fields: Funding8h, OpenInterest, MarkPrice, IndexPrice.
'
' Dormant in P1 (only the §9 standalone soak constructs it).
Public NotInheritable Class MarketState

    ' Per-resolution rolling-buffer caps. Match the live REST fetch counts so a WS-served
    ' window is the same depth the indicator stack sees today.
    Public Shared ReadOnly Caps As New Dictionary(Of String, Integer) From {
        {"1", 250}, {"3", 250}, {"5", 210}, {"15", 70}}

    Private Const TradeCap As Integer = 5000

    Private ReadOnly _lock As New Object()

    ' Candle series + per-series last-update stamp.
    Private ReadOnly _series As New Dictionary(Of String, List(Of Candle))()
    Private ReadOnly _seriesUpdate As New Dictionary(Of String, DateTime)()

    ' Trade ring buffer (ascending).
    Private ReadOnly _trades As New List(Of TradeRecord)()
    Private _tradesUpdate As DateTime = DateTime.MinValue

    ' Top-of-book ladder.
    Private _book As OrderBookSnapshot = Nothing
    Private _bookUpdate As DateTime = DateTime.MinValue

    ' Ticker fields.
    Private _funding8h As Double? = Nothing
    Private _openInterest As Double = 0.0
    Private _markPrice As Double = 0.0
    Private _indexPrice As Double = 0.0
    Private _tickerUpdate As DateTime = DateTime.MinValue

    ' [P4 #4] Time-averaged OFI accumulator (docs/time-averaged-ofi-proposal.md §4.1).
    ' Folded once per streaming book update, read once per analysis run, reset on
    ' (re)connect — all under _lock (the accumulator is not internally locked).
    Private ReadOnly _ofiAcc As New OfiAccumulator()

    ' [P4 #5] Aggressor-velocity accumulator (docs/aggressor-velocity-proposal.md §4.1).
    ' Folded once per streamed trade (the trade analogue of the OFI book fold), read once
    ' per analysis run / live-strip tick, reset on (re)connect — all under _lock.
    Private ReadOnly _aggrVelAcc As New AggressorVelocityAccumulator()

    ' [P4 #6] Level-absorption episode tracker (docs/book-absorption-proposal.md §4.2).
    ' The first DUAL-FED tracker: folded from BOTH UpdateBook (each ~100 ms snapshot)
    ' and AppendTrade (each print) — MarketState sees both under this one lock, so the
    ' fold sites mirror FoldOfi / FoldAggressorVelocity exactly. Carried levels set once
    ' per full run; reset on (re)connect — all under _lock.
    Private ReadOnly _absorptionTracker As New LevelAbsorptionTracker()

    ' ── Writers (receive loop / seeding) ───────────────────────────────────────────────

    ''' <summary>Replace a candle series wholesale from a REST seed burst (startup / reconnect).
    ''' The incoming list is chronological ascending (DeribitClient order); trim to the
    ''' series cap keeping the most recent.</summary>
    Public Sub SeedCandles(resolution As String, candles As List(Of Candle), nowUtc As DateTime)
        If candles Is Nothing Then Return
        SyncLock _lock
            Dim cap As Integer = CapFor(resolution)
            Dim copy As New List(Of Candle)(candles)
            If copy.Count > cap Then copy.RemoveRange(0, copy.Count - cap)
            _series(resolution) = copy
            _seriesUpdate(resolution) = nowUtc
        End SyncLock
    End Sub

    ''' <summary>Apply one chart.trades notification for a resolution. The forming bar
    ''' (tick == latest bar's Timestamp) updates in place; a newer tick appends and trims
    ''' to cap. Out-of-order (older) ticks are ignored.</summary>
    Public Sub ApplyChartTick(resolution As String, c As Candle, nowUtc As DateTime)
        If c Is Nothing Then Return
        SyncLock _lock
            Dim s As List(Of Candle) = Nothing
            If Not _series.TryGetValue(resolution, s) Then
                s = New List(Of Candle)()
                _series(resolution) = s
            End If
            If s.Count = 0 Then
                s.Add(c)
            Else
                Dim last As Candle = s(s.Count - 1)
                If c.Timestamp = last.Timestamp Then
                    s(s.Count - 1) = c                 ' forming bar update
                ElseIf c.Timestamp > last.Timestamp Then
                    s.Add(c)                           ' bar roll
                    Dim cap As Integer = CapFor(resolution)
                    If s.Count > cap Then s.RemoveRange(0, s.Count - cap)
                End If
                ' c.Timestamp < last.Timestamp → stale/out-of-order; ignore.
            End If
            _seriesUpdate(resolution) = nowUtc
        End SyncLock
    End Sub

    ''' <summary>Replace the trade buffer wholesale from a REST seed (ascending).</summary>
    Public Sub SeedTrades(trades As List(Of TradeRecord), nowUtc As DateTime)
        If trades Is Nothing Then Return
        SyncLock _lock
            _trades.Clear()
            _trades.AddRange(trades)
            If _trades.Count > TradeCap Then _trades.RemoveRange(0, _trades.Count - TradeCap)
            _tradesUpdate = nowUtc
        End SyncLock
    End Sub

    ''' <summary>Append one streamed trade (newest at the end); trim oldest beyond cap.</summary>
    Public Sub AppendTrade(rec As TradeRecord, nowUtc As DateTime)
        If rec Is Nothing Then Return
        SyncLock _lock
            _trades.Add(rec)
            If _trades.Count > TradeCap Then _trades.RemoveRange(0, _trades.Count - TradeCap)
            _tradesUpdate = nowUtc
        End SyncLock
    End Sub

    ''' <summary>Replace the top-of-book ladder with the latest depth-limited snapshot.</summary>
    Public Sub UpdateBook(snap As OrderBookSnapshot, nowUtc As DateTime)
        If snap Is Nothing Then Return
        SyncLock _lock
            _book = snap
            _bookUpdate = nowUtc
        End SyncLock
    End Sub

    ''' <summary>[P4 #4] Fold one book-update OFI imbalance sample into the time-averaged
    ''' accumulator (proposal §4.1). Called by the feed right after UpdateBook with the
    ''' weighted bid/ask volumes + sanity-bounded ratio from IndicatorEngine.ComputeOfiImbalance.</summary>
    Public Sub FoldOfi(bidVol As Double, askVol As Double, ratio As Double, tsMs As Long, tauSec As Double)
        SyncLock _lock
            _ofiAcc.Fold(bidVol, askVol, ratio, tsMs, tauSec)
        End SyncLock
    End Sub

    ''' <summary>[P4 #4] Clear the OFI accumulator on (re)connect so a stale pre-disconnect
    ''' average can't bleed across a gap; the warmup fallback re-arms after each reconnect.</summary>
    Public Sub ResetOfiAccumulator()
        SyncLock _lock
            _ofiAcc.Reset()
        End SyncLock
    End Sub

    ''' <summary>[P4 #5] Fold one streamed trade into the aggressor-velocity accumulator
    ''' (proposal §4.1). Called by the feed right after AppendTrade with the trade's own
    ''' exchange timestamp; taus = fast_window_sec + the session-resolved norm_window_sec.</summary>
    Public Sub FoldAggressorVelocity(amountUsd As Double, isBuy As Boolean, tsMs As Long,
                                     tauFastSec As Double, tauNormSec As Double)
        SyncLock _lock
            _aggrVelAcc.Fold(amountUsd, isBuy, tsMs, tauFastSec, tauNormSec)
        End SyncLock
    End Sub

    ''' <summary>[P4 #5] Clear the aggressor-velocity accumulator on (re)connect so no
    ''' pre-disconnect flow bleeds across a gap; the warmup suppression re-arms.</summary>
    Public Sub ResetAggressorVelocity()
        SyncLock _lock
            _aggrVelAcc.Reset()
        End SyncLock
    End Sub

    ''' <summary>[P4 #6] Refresh the absorption tracker's carried candidate levels from a
    ''' completed full run (the strip's carry — proposal §4.1). A mid-episode re-map
    ''' resets that side's episode at the next fold (no cross-level bleed).</summary>
    Public Sub SetAbsorptionLevels(swingHigh5m As Double, swingLow5m As Double,
                                   hvnAbove As Double, hvnBelow As Double)
        SyncLock _lock
            _absorptionTracker.SetLevels(swingHigh5m, swingLow5m, hvnAbove, hvnBelow)
        End SyncLock
    End Sub

    ''' <summary>[P4 #6] Fold one book snapshot into the absorption tracker (proximity
    ''' gate, band-size trajectory, D8 conservation interval). Called by the feed right
    ''' after UpdateBook — the book half of the dual fold.</summary>
    Public Sub FoldAbsorptionBook(snap As OrderBookSnapshot, tsMs As Long, cfg As AbsorptionSettings)
        SyncLock _lock
            _absorptionTracker.FoldBook(snap, tsMs, cfg)
        End SyncLock
    End Sub

    ''' <summary>[P4 #6] Fold one streamed trade into the absorption tracker (pressing
    ''' volume, interval fills, break-through test). Called by the feed right after
    ''' AppendTrade — the trade half of the dual fold.</summary>
    Public Sub FoldAbsorptionTrade(price As Double, amountUsd As Double, isBuy As Boolean,
                                   tsMs As Long, cfg As AbsorptionSettings)
        SyncLock _lock
            _absorptionTracker.FoldTrade(price, amountUsd, isBuy, tsMs, cfg)
        End SyncLock
    End Sub

    ''' <summary>[P4 #6] Clear the absorption tracker on (re)connect so no pre-gap
    ''' episode bleeds across; it re-arms on the next approach after levels re-carry.</summary>
    Public Sub ResetAbsorption()
        SyncLock _lock
            _absorptionTracker.Reset()
        End SyncLock
    End Sub

    ''' <summary>Update the ticker fields. Funding8h serves GetFundingRateAsync — it is
    ''' funding_8h, NOT current_funding (parity with DeribitClient.GetFundingRateAsync).</summary>
    Public Sub UpdateTicker(funding8h As Double?, openInterest As Double,
                            markPrice As Double, indexPrice As Double, nowUtc As DateTime)
        SyncLock _lock
            If funding8h.HasValue Then _funding8h = funding8h
            _openInterest = openInterest
            _markPrice = markPrice
            _indexPrice = indexPrice
            _tickerUpdate = nowUtc
        End SyncLock
    End Sub

    ' ── Readers (return copies under lock) ──────────────────────────────────────────────

    ''' <summary>Copy of the resolution's series, or Nothing if never seeded/empty.</summary>
    Public Function GetCandles(resolution As String) As List(Of Candle)
        SyncLock _lock
            Dim s As List(Of Candle) = Nothing
            If Not _series.TryGetValue(resolution, s) OrElse s.Count = 0 Then Return Nothing
            Return New List(Of Candle)(s)
        End SyncLock
    End Function

    Public Function GetTrades() As List(Of TradeRecord)
        SyncLock _lock
            Return New List(Of TradeRecord)(_trades)
        End SyncLock
    End Function

    Public Function GetBook() As OrderBookSnapshot
        SyncLock _lock
            Return _book
        End SyncLock
    End Function

    ''' <summary>[P4 #4] A consistent read of the time-averaged OFI state + the warmup verdict
    ''' for the given window (proposal §4.2). The caller uses the averaged Ratio/Bid/Ask only
    ''' when HasWarmup is True; otherwise it falls back to the snapshot CalcOFI.</summary>
    Public Function GetOfiAverage(minCoverageSec As Double) As OfiAverageSnapshot
        SyncLock _lock
            Return _ofiAcc.Snapshot(minCoverageSec)
        End SyncLock
    End Function

    ''' <summary>[P4 #5] A consistent read of the aggressor-velocity burst state + the
    ''' warmup verdict for the session's norm window (proposal §4.2). The caller uses the
    ''' burst fields only when HasWarmup is True (else NORMAL / null — §8 suppression).</summary>
    Public Function GetAggressorVelocity(grossFloorUsdPerSec As Double, minCoverageSec As Double) As AggressorVelocitySnapshot
        SyncLock _lock
            Return _aggrVelAcc.Snapshot(grossFloorUsdPerSec, minCoverageSec)
        End SyncLock
    End Function

    ''' <summary>[P4 #6] A consistent read of both absorption-episode sides (proposal
    ''' §4.3). nowMs prunes the rolling pressing windows; the caller classifies via
    ''' IndicatorEngine.ClassifyAbsorption and surfaces numerics only for active
    ''' episodes (else NONE / null — never guesses).</summary>
    Public Function GetAbsorption(nowMs As Long, cfg As AbsorptionSettings) As AbsorptionSnapshot
        SyncLock _lock
            Return _absorptionTracker.Snapshot(nowMs, cfg)
        End SyncLock
    End Function

    Public ReadOnly Property Funding8h As Double?
        Get
            SyncLock _lock
                Return _funding8h
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property OpenInterest As Double
        Get
            SyncLock _lock
                Return _openInterest
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property MarkPrice As Double
        Get
            SyncLock _lock
                Return _markPrice
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property IndexPrice As Double
        Get
            SyncLock _lock
                Return _indexPrice
            End SyncLock
        End Get
    End Property

    ' ── Last-update stamps (for the WsMarketDataSource staleness gate + soak diagnostics) ──

    Public Function CandleLastUpdate(resolution As String) As DateTime
        SyncLock _lock
            Dim t As DateTime
            If _seriesUpdate.TryGetValue(resolution, t) Then Return t
            Return DateTime.MinValue
        End SyncLock
    End Function

    Public ReadOnly Property TradesLastUpdate As DateTime
        Get
            SyncLock _lock
                Return _tradesUpdate
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property BookLastUpdate As DateTime
        Get
            SyncLock _lock
                Return _bookUpdate
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property TickerLastUpdate As DateTime
        Get
            SyncLock _lock
                Return _tickerUpdate
            End SyncLock
        End Get
    End Property

    Public Function CandleCount(resolution As String) As Integer
        SyncLock _lock
            Dim s As List(Of Candle) = Nothing
            If _series.TryGetValue(resolution, s) Then Return s.Count
            Return 0
        End SyncLock
    End Function

    Public ReadOnly Property TradeCount As Integer
        Get
            SyncLock _lock
                Return _trades.Count
            End SyncLock
        End Get
    End Property

    Private Shared Function CapFor(resolution As String) As Integer
        Dim cap As Integer
        If Caps.TryGetValue(resolution, cap) Then Return cap
        Return 250
    End Function
End Class
