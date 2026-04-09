' Core/Indicators_OrderFlow.vb
' IndicatorEngine partial: order flow and market microstructure indicators.
' Covers: OFI, Liquidations, CVD.

Partial Public Class IndicatorEngine

    ' -- OFI (Order Flow Imbalance) top-3 levels, volume-weighted (w=3,2,1) ---
    Public Shared Sub CalcOFI(orderBook As OrderBookSnapshot,
                               ByRef ofiRatio As Double, ByRef ofiSignal As String,
                               ByRef ofiBidVol As Double, ByRef ofiAskVol As Double)
        ofiRatio = 1.0 : ofiSignal = "BALANCED" : ofiBidVol = 0 : ofiAskVol = 0
        If orderBook Is Nothing Then Return

        Dim bids = orderBook.Bids.Take(3).ToList()
        Dim asks = orderBook.Asks.Take(3).ToList()
        Dim weights() As Double = {3, 2, 1}

        Dim bidVol As Double = 0
        Dim askVol As Double = 0
        For i As Integer = 0 To Math.Min(bids.Count, 3) - 1
            bidVol += bids(i).Size * weights(i)
        Next
        For i As Integer = 0 To Math.Min(asks.Count, 3) - 1
            askVol += asks(i).Size * weights(i)
        Next

        ofiBidVol = bidVol
        ofiAskVol = askVol

        Dim total As Double = bidVol + askVol
        If total = 0 Then Return
        ofiRatio = bidVol / askVol
        If ofiRatio > 1.2 Then
            ofiSignal = "BUY DOMINANT"
        ElseIf ofiRatio < 0.833 Then
            ofiSignal = "SELL DOMINANT"
        Else
            ofiSignal = "BALANCED"
        End If
    End Sub

    ' -- Liquidations ---------------------------------------------------------
    Public Shared Sub CalcLiquidations(trades As List(Of TradeRecord),
                                        ByRef liqLongSize As Double,
                                        ByRef liqShortSize As Double,
                                        ByRef liqSignal As String)
        liqLongSize = 0 : liqShortSize = 0 : liqSignal = "NONE"
        If trades Is Nothing OrElse trades.Count = 0 Then Return
        For Each t In trades
            If t.Liquidation <> "none" Then
                If t.Direction = "buy" Then
                    liqShortSize += t.Amount
                Else
                    liqLongSize += t.Amount
                End If
            End If
        Next
        If liqLongSize > 0 AndAlso liqLongSize >= liqShortSize Then
            liqSignal = "LONG LIQS"
        ElseIf liqShortSize > 0 AndAlso liqShortSize > liqLongSize Then
            liqSignal = "SHORT LIQS"
        Else
            liqSignal = "NONE"
        End If
    End Sub

    ' -- CVD (Cumulative Volume Delta) ----------------------------------------
    Public Shared Sub CalcCVD(trades As List(Of TradeRecord), candles As List(Of Candle),
                               ByRef cvdValue As Double, ByRef cvdSlope As String,
                               ByRef cvdDivergence As String,
                               Optional slopeMinUsd As Double = 50000,
                               Optional slopePctOfValue As Double = 0.05,
                               Optional divergencePriceGate As Double = 0.002)
        cvdValue = 0 : cvdSlope = "FLAT" : cvdDivergence = "NONE"
        If trades Is Nothing OrElse trades.Count = 0 Then Return

        Dim half As Integer = trades.Count \ 2
        Dim earlyDelta As Double = 0
        Dim lateDelta As Double = 0
        For i As Integer = 0 To trades.Count - 1
            Dim t = trades(i)
            Dim usdDelta As Double = If(t.Direction = "buy", t.Amount, -t.Amount)
            If i < half Then earlyDelta += usdDelta Else lateDelta += usdDelta
        Next
        cvdValue = earlyDelta + lateDelta

        Dim absValue As Double = Math.Abs(cvdValue)
        Dim slopeThreshold As Double = Math.Max(slopeMinUsd, absValue * slopePctOfValue)
        Dim slopeDelta As Double = lateDelta - earlyDelta
        If slopeDelta > slopeThreshold Then
            cvdSlope = "RISING"
        ElseIf slopeDelta < -slopeThreshold Then
            cvdSlope = "FALLING"
        Else
            cvdSlope = "FLAT"
        End If

        If candles.Count < 2 Then Return
        Dim priceChange As Double = (candles.Last().Close - candles(candles.Count - 2).Close) /
                                     candles(candles.Count - 2).Close
        If Math.Abs(priceChange) < divergencePriceGate Then Return
        If priceChange > 0 AndAlso cvdValue < 0 Then
            cvdDivergence = "BEARISH"
        ElseIf priceChange < 0 AndAlso cvdValue > 0 Then
            cvdDivergence = "BULLISH"
        End If
    End Sub

End Class
