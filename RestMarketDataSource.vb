' RestMarketDataSource.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' The verified-identical fallback path: a thin pass-through to the existing (untouched)
' DeribitClient. Every method forwards to the matching DeribitClient shared method, so
' routing a consumer through this source is byte-for-byte identical to calling
' DeribitClient directly. Host-agnostic — no WinForms, no MainForm coupling.
'
' Dormant in P1 (nothing references it); consumer routing is P2.
Public NotInheritable Class RestMarketDataSource
    Implements IMarketDataSource

    Public Function GetCandlesAsync(resolution As String, count As Integer) As Task(Of List(Of Candle)) _
            Implements IMarketDataSource.GetCandlesAsync
        Return DeribitClient.GetCandlesAsync(resolution, count)
    End Function

    Public Function GetFundingRateAsync() As Task(Of Double?) _
            Implements IMarketDataSource.GetFundingRateAsync
        Return DeribitClient.GetFundingRateAsync()
    End Function

    Public Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double)?) _
            Implements IMarketDataSource.GetBookSummaryAsync
        Return DeribitClient.GetBookSummaryAsync()
    End Function

    Public Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot) _
            Implements IMarketDataSource.GetOrderBookAsync
        Return DeribitClient.GetOrderBookAsync(depth)
    End Function

    Public Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord)) _
            Implements IMarketDataSource.GetRecentTradesAsync
        Return DeribitClient.GetRecentTradesAsync(count)
    End Function
End Class
