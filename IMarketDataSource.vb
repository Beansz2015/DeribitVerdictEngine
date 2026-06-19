' IMarketDataSource.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' Transport-agnostic market-data contract. Mirrors the live DeribitClient call shapes
' VERBATIM so a consumer can be routed through REST or WS without any signature change
' (consumer routing is P2; this interface is dormant/unreferenced in P1).
'
' Only the COUNT-based GetCandlesAsync(resolution, count) overload is part of the
' contract — the live indicator path. The time-range overload
' GetCandlesAsync(resolution, startMs, endMs) (DeribitClient.vb, used by
' DeribitOhlcFetcher / OhlcCache gap-fill / LivePerformanceTracker.FetchGapChunked)
' is deliberately NOT here: WS streams forward only, so all backfill/seeding stays a
' direct REST call.
'
' Nullability semantics are identical to DeribitClient: a Nothing result means
' "unavailable", which the existing RunAnalysisAsync skip-gate already handles exactly
' like a REST failure.
'
' Reuses the existing Candle / OrderBookSnapshot / TradeRecord DTOs (DeribitClient.vb);
' it does not redefine them.
Public Interface IMarketDataSource
    Function GetCandlesAsync(resolution As String, count As Integer) As Task(Of List(Of Candle))
    Function GetFundingRateAsync() As Task(Of Double?)
    Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double)?)
    Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot)
    Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord))
End Interface
