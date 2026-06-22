' WsMarketDataSource.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' Serves the 5 IMarketDataSource shapes from a MarketState populated by DeribitWsFeed.
' Dormant in P1 (nothing references it); consumer routing is P2.
'
' Staleness gate (book / ticker): if now − stream.LastUpdateUtc exceeds ws_stale_after_sec,
' the getter returns Nothing → the consumer's existing skip-gate / REST fallback handles it
' like a REST failure. A never-updated stream has LastUpdate = DateTime.MinValue, so it reads
' stale until the first frame arrives.
'
' TRADES are gated on CONNECTION-HEALTH, not last-trade-age (P3 §3 —
' docs/websocket-migration-p3-cutover-spec.md): trades legitimately go quiet, and REST returns
' old trades happily, so a complete-but-quiet buffer is valid. Only an unhealthy connection
' (the wired healthCheck delegate returning False) yields Nothing; DeribitWsFeed.IsDegraded()
' already catches that for the whole-run REST fallback. Without a delegate (legacy/test path)
' the trades getter falls back to the age-gate. This is the fix for the shadow-parity gate's
' "WS-NOT-READY trades" resets in active markets (run cadence ≈ ws_stale_after_sec).
'
' Candle series do NOT get a staleness gate here — candle freshness is the consumer's
' existing IndicatorEngine.IsFresh (D5) job; double-gating would be wrong. The candle
' getter returns Nothing only when the series was never seeded (empty).
Public NotInheritable Class WsMarketDataSource
    Implements IMarketDataSource

    Private ReadOnly _state As MarketState
    Private ReadOnly _staleAfterSec As Integer
    Private ReadOnly _healthCheck As Func(Of Boolean)

    ' healthCheck: returns True while the feed connection is usable (connected, not cooling
    ' down). Gates the TRADES stream (P3 §3); when Nothing, trades fall back to the age-gate.
    Public Sub New(state As MarketState,
                   Optional healthCheck As Func(Of Boolean) = Nothing,
                   Optional staleAfterSec As Integer = -1)
        _state = state
        _healthCheck = healthCheck
        _staleAfterSec = If(staleAfterSec >= 0, staleAfterSec,
                            SettingsLoader.Current.Network.WsStaleAfterSec)
    End Sub

    Private Function IsStale(lastUpdate As DateTime) As Boolean
        Return (DateTime.UtcNow - lastUpdate).TotalSeconds > _staleAfterSec
    End Function

    Public Function GetCandlesAsync(resolution As String, count As Integer) As Task(Of List(Of Candle)) _
            Implements IMarketDataSource.GetCandlesAsync
        Dim series As List(Of Candle) = _state.GetCandles(resolution)
        If series Is Nothing OrElse series.Count = 0 Then
            Return Task.FromResult(Of List(Of Candle))(Nothing)
        End If
        Dim n As Integer = Math.Min(count, series.Count)
        Dim tail As New List(Of Candle)(series.GetRange(series.Count - n, n))
        Return Task.FromResult(tail)
    End Function

    Public Function GetFundingRateAsync() As Task(Of Double?) _
            Implements IMarketDataSource.GetFundingRateAsync
        If IsStale(_state.TickerLastUpdate) Then Return Task.FromResult(Of Double?)(Nothing)
        Return Task.FromResult(_state.Funding8h)
    End Function

    Public Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double)?) _
            Implements IMarketDataSource.GetBookSummaryAsync
        If IsStale(_state.TickerLastUpdate) Then
            Return Task.FromResult(Of (OI As Double, MarkPrice As Double)?)(Nothing)
        End If
        Dim v As (OI As Double, MarkPrice As Double)? = (_state.OpenInterest, _state.MarkPrice)
        Return Task.FromResult(v)
    End Function

    Public Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot) _
            Implements IMarketDataSource.GetOrderBookAsync
        If IsStale(_state.BookLastUpdate) Then Return Task.FromResult(Of OrderBookSnapshot)(Nothing)
        Dim book As OrderBookSnapshot = _state.GetBook()
        If book Is Nothing Then Return Task.FromResult(Of OrderBookSnapshot)(Nothing)
        If book.Bids.Count <= depth AndAlso book.Asks.Count <= depth Then
            Return Task.FromResult(book)
        End If
        ' Trim to top-`depth` (the subscribed ladder is already top-10, so this is a no-op
        ' for depth >= 10; guards a hypothetical deeper request).
        Dim snap As New OrderBookSnapshot()
        For i As Integer = 0 To Math.Min(depth, book.Bids.Count) - 1
            snap.Bids.Add(book.Bids(i))
        Next
        For i As Integer = 0 To Math.Min(depth, book.Asks.Count) - 1
            snap.Asks.Add(book.Asks(i))
        Next
        Return Task.FromResult(snap)
    End Function

    Public Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord)) _
            Implements IMarketDataSource.GetRecentTradesAsync
        ' [P3 §3] Connection-health gate (not last-trade-age): a complete-but-quiet buffer is
        ' valid — matches REST, which returns old trades in a quiet market. Fall back to the
        ' age-gate only when no health delegate is wired (legacy/test path).
        If _healthCheck IsNot Nothing Then
            If Not _healthCheck() Then Return Task.FromResult(Of List(Of TradeRecord))(Nothing)
        ElseIf IsStale(_state.TradesLastUpdate) Then
            Return Task.FromResult(Of List(Of TradeRecord))(Nothing)
        End If
        Dim all As List(Of TradeRecord) = _state.GetTrades()
        If all Is Nothing OrElse all.Count = 0 Then Return Task.FromResult(Of List(Of TradeRecord))(Nothing)
        Dim n As Integer = Math.Min(count, all.Count)
        Dim tail As New List(Of TradeRecord)(all.GetRange(all.Count - n, n))
        Return Task.FromResult(tail)
    End Function
End Class
