' WsMarketDataSource.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' Serves the 5 IMarketDataSource shapes from a MarketState populated by DeribitWsFeed.
' Dormant in P1 (nothing references it); consumer routing is P2.
'
' Staleness gate (book / trades / ticker only): if now − stream.LastUpdateUtc exceeds
' ws_stale_after_sec, the getter returns Nothing → the consumer's existing skip-gate /
' REST fallback handles it exactly like a REST failure. A never-updated stream has
' LastUpdate = DateTime.MinValue, so it reads stale until the first frame arrives.
'
' Candle series do NOT get a staleness gate here — candle freshness is the consumer's
' existing IndicatorEngine.IsFresh (D5) job; double-gating would be wrong. The candle
' getter returns Nothing only when the series was never seeded (empty).
Public NotInheritable Class WsMarketDataSource
    Implements IMarketDataSource

    Private ReadOnly _state As MarketState
    Private ReadOnly _staleAfterSec As Integer

    Public Sub New(state As MarketState, Optional staleAfterSec As Integer = -1)
        _state = state
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
        If IsStale(_state.TradesLastUpdate) Then Return Task.FromResult(Of List(Of TradeRecord))(Nothing)
        Dim all As List(Of TradeRecord) = _state.GetTrades()
        If all Is Nothing OrElse all.Count = 0 Then Return Task.FromResult(Of List(Of TradeRecord))(Nothing)
        Dim n As Integer = Math.Min(count, all.Count)
        Dim tail As New List(Of TradeRecord)(all.GetRange(all.Count - n, n))
        Return Task.FromResult(tail)
    End Function
End Class
