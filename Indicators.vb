' Indicators.vb  v0.34
' Pure calculation layer -- no I/O, no UI references.
' Input: List(Of Candle). Output: typed result objects.
'
' v0.28 -- CalcOFI rewritten: top-3 levels only, volume-weighted (w=3,2,1).
'          Two new IndicatorResults fields: OFIBidVol, OFIAskVol (weighted sums for display).
' v0.29 -- Added CVD fields: CVDValue, CVDSlope, CVDDivergence.
'          Added CalcCVD method.
' v0.30 -- Settings completeness pass: all hardcoded thresholds now passed as parameters
'          so every tuneable value is driven from settings.json.
'          CalcCVD:            slopeMinUsd, slopePctOfValue, divergencePriceGate
'          CalcRSIDivergence:  priceGate, rsiDelta
'          CalcOBV:            trendGate, divergenceGate
'          CalcROCSeries:      lookback (was hardcoded 3)
' v0.31 -- CalcVWAP: auto-selects session reset time.
'          Before 13:30 UTC  -> session starts 00:00 UTC (daily VWAP).
'          At/after 13:30 UTC -> session starts 13:30 UTC (US session VWAP).
'          Returns sessionCandleCount via ByRef; warmup guard enforced by caller.
'          CalcVWAPBands: computes rolling sigma1/sigma2 bands from session TP deviations.
'          New IndicatorResults fields:
'            VWAPSessionCandles, VWAPSigma1Upper, VWAPSigma1Lower,
'            VWAPSigma2Upper, VWAPSigma2Lower.
' v0.32 -- CalcVWAP and CalcVWAPBands: session boundary times now passed as parameters
'          (session2Hour, session2Minute) instead of hardcoded 13:30 UTC.
'          Callers read these from cfg.Indicators.VWAP in MainForm.
' v0.33 -- TTM Squeeze momentum upgrade for BBW.
'          New IndicatorResults fields: TTMHistogram, TTMDirection, TTMSignal.
'          New method: CalcTTMSqueeze -- computes momentum histogram using
'          linear regression of (Close - SMA20) over the last N candles.
'          Direction classified by comparing last vs prior histogram thirds.
'          Signal: BULL_BUILDING / BEAR_BUILDING / BULL_FADING / BEAR_FADING / FLAT.
' v0.34 -- Fix type mismatches that caused build errors:
'          CalcOFI:          parameter type OrderBook -> OrderBookSnapshot
'                            (Price As Double, Size As Double) tuple access corrected
'          CalcLiquidations: parameter type Trade -> TradeRecord
'                            Liquidation field is String ("M"/"T"/"none"), not Boolean;
'                            guard changed to: t.Liquidation <> "none"
'          CalcCVD:          parameter type Trade -> TradeRecord

Public Class IndicatorResults
    ' Core
    Public Property ROC As Double
    Public Property ROCSlope As String       ' "RISING" / "FALLING" / "FLAT"
    Public Property RSI As Double
    Public Property RSIDivergence As String  ' NONE / BULLISH / BEARISH
    Public Property ATR As Double
    Public Property ATRAvg20d As Double
    Public Property ATRSizeMultiplier As Double
    Public Property VolumeSMA9 As Double
    Public Property CurrentVolume As Double  ' BTC volume -- used for scoring
    Public Property CurrentVolumeUSD As Double ' USD volume -- display only
    Public Property VolumeRatio As Double

    ' Trend (5m)
    Public Property PlusDI As Double
    Public Property MinusDI As Double
    Public Property ADX As Double
    Public Property Regime As String         ' TRENDING_UP / TRENDING_DOWN / RANGE_BOUND / TRANSITIONAL

    ' Tier 1
    Public Property VWAP As Double
    Public Property VWAPDevPct As Double
    Public Property VWAPSessionCandles As Integer  ' number of candles in current VWAP session
    Public Property VWAPSigma1Upper As Double       ' VWAP + 1 sigma
    Public Property VWAPSigma1Lower As Double       ' VWAP - 1 sigma
    Public Property VWAPSigma2Upper As Double       ' VWAP + 2 sigma
    Public Property VWAPSigma2Lower As Double       ' VWAP - 2 sigma
    Public Property BBW As Double
    Public Property SqueezeStatus As String  ' ACTIVE / RELEASING / NONE
    Public Property TTMHistogram As Double   ' positive = bullish momentum, negative = bearish
    Public Property TTMDirection As String   ' "RISING" / "FALLING" / "FLAT"
    Public Property TTMSignal As String      ' "BULL_BUILDING" / "BEAR_BUILDING" / "BULL_FADING" / "BEAR_FADING" / "FLAT"
    Public Property EMA9 As Double
    Public Property EMA21 As Double
    Public Property EMA50 As Double
    Public Property EMAAlignment As String   ' BULL / BEAR / MIXED
    Public Property FundingRate As Double    ' raw 8h decimal e.g. 0.0001
    Public Property FundingBias As String
    Public Property OI_Current As Double
    Public Property OI_Prev15m As Double
    Public Property OI_Prev60m As Double
    Public Property OIChange15m As Double    ' % change
    Public Property OIChange60m As Double
    Public Property OISignal As String       ' NEW LONGS / NEW SHORTS / COVERING / CAPITULATION / NEUTRAL

    ' Tier 2
    Public Property OFIRatio As Double
    Public Property OFISignal As String      ' BUY DOMINANT / SELL DOMINANT / BALANCED
    Public Property OFIBidVol As Double      ' weighted bid volume (top-3, w=3,2,1) -- display only
    Public Property OFIAskVol As Double      ' weighted ask volume (top-3, w=3,2,1) -- display only
    Public Property LiqLongSize As Double
    Public Property LiqShortSize As Double
    Public Property LiqSignal As String
    Public Property EMA200_5m As Double
    Public Property PriceVsEMA200 As String  ' ABOVE / BELOW
    Public Property CVDValue As Double       ' net USD delta (buy-sell) over last N trades
    Public Property CVDSlope As String       ' "RISING" / "FALLING" / "FLAT"
    Public Property CVDDivergence As String  ' "BULLISH" / "BEARISH" / "NONE"

    ' Tier 3
    Public Property DonchianUpper As Double
    Public Property DonchianLower As Double
    Public Property DonchianSignal As String ' LONG / SHORT / NONE
    Public Property OBVTrend As String       ' RISING / FALLING / FLAT
    Public Property OBVDivergence As String  ' NONE / BEARISH / BULLISH

    ' Current price (latest close of 1m candles)
    Public Property CurrentPrice As Double
End Class

Public Class IndicatorEngine

    ' -- DMI + ADX ------------------------------------------------------------
    Public Shared Sub CalcDMI(candles As List(Of Candle), period As Integer,
                               ByRef plusDI As Double, ByRef minusDI As Double, ByRef adx As Double)
        If candles.Count < period + 2 Then
            plusDI = 0 : minusDI = 0 : adx = 0 : Return
        End If

        Dim trList As New List(Of Double)
        Dim dmPlusList As New List(Of Double)
        Dim dmMinusList As New List(Of Double)

        For i As Integer = 1 To candles.Count - 1
            Dim c = candles(i)
            Dim p = candles(i - 1)
            Dim tr As Double = Math.Max(c.High - c.Low,
                               Math.Max(Math.Abs(c.High - p.Close),
                                        Math.Abs(c.Low - p.Close)))
            Dim upMove As Double = c.High - p.High
            Dim downMove As Double = p.Low - c.Low
            Dim dmPlus As Double = If(upMove > downMove AndAlso upMove > 0, upMove, 0)
            Dim dmMinus As Double = If(downMove > upMove AndAlso downMove > 0, downMove, 0)
            trList.Add(tr)
            dmPlusList.Add(dmPlus)
            dmMinusList.Add(dmMinus)
        Next

        Dim smoothTR As Double = trList.Take(period).Sum()
        Dim smoothPlus As Double = dmPlusList.Take(period).Sum()
        Dim smoothMinus As Double = dmMinusList.Take(period).Sum()

        Dim adxList As New List(Of Double)
        Dim prevDI_Plus As Double = 0, prevDI_Minus As Double = 0

        For i As Integer = period To trList.Count - 1
            smoothTR = smoothTR - smoothTR / period + trList(i)
            smoothPlus = smoothPlus - smoothPlus / period + dmPlusList(i)
            smoothMinus = smoothMinus - smoothMinus / period + dmMinusList(i)

            Dim di_plus As Double = If(smoothTR <> 0, 100 * smoothPlus / smoothTR, 0)
            Dim di_minus As Double = If(smoothTR <> 0, 100 * smoothMinus / smoothTR, 0)
            Dim dx As Double = If((di_plus + di_minus) <> 0,
                                   100 * Math.Abs(di_plus - di_minus) / (di_plus + di_minus), 0)
            adxList.Add(dx)
            prevDI_Plus = di_plus
            prevDI_Minus = di_minus
        Next

        plusDI = prevDI_Plus
        minusDI = prevDI_Minus

        If adxList.Count < period Then
            adx = 0 : Return
        End If
        Dim smoothADX As Double = adxList.Take(period).Average()
        For i As Integer = period To adxList.Count - 1
            smoothADX = (smoothADX * (period - 1) + adxList(i)) / period
        Next
        adx = smoothADX
    End Sub

    ' -- ATR ------------------------------------------------------------------
    Public Shared Function CalcATR(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period + 1 Then Return 0
        Dim trValues As New List(Of Double)
        For i As Integer = 1 To candles.Count - 1
            Dim c = candles(i) : Dim p = candles(i - 1)
            trValues.Add(Math.Max(c.High - c.Low,
                         Math.Max(Math.Abs(c.High - p.Close),
                                  Math.Abs(c.Low - p.Close))))
        Next
        Dim atr As Double = trValues.Take(period).Average()
        For i As Integer = period To trValues.Count - 1
            atr = (atr * (period - 1) + trValues(i)) / period
        Next
        Return atr
    End Function

    ' -- EMA ------------------------------------------------------------------
    Public Shared Function CalcEMA(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period Then Return 0
        Dim closes = candles.Select(Function(c) c.Close).ToList()
        Dim k As Double = 2.0 / (period + 1)
        Dim ema As Double = closes.Take(period).Average()
        For i As Integer = period To closes.Count - 1
            ema = closes(i) * k + ema * (1 - k)
        Next
        Return ema
    End Function

    Public Shared Function CalcEMAList(values As List(Of Double), period As Integer) As Double
        If values.Count < period Then Return 0
        Dim k As Double = 2.0 / (period + 1)
        Dim ema As Double = values.Take(period).Average()
        For i As Integer = period To values.Count - 1
            ema = values(i) * k + ema * (1 - k)
        Next
        Return ema
    End Function

    ' -- RSI (Wilder EMA-smoothed) --------------------------------------------
    Public Shared Function CalcRSI(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period + 1 Then Return 50
        Dim gains As New List(Of Double)
        Dim losses As New List(Of Double)
        For i As Integer = 1 To candles.Count - 1
            Dim diff As Double = candles(i).Close - candles(i - 1).Close
            gains.Add(If(diff > 0, diff, 0))
            losses.Add(If(diff < 0, Math.Abs(diff), 0))
        Next
        Dim avgGain As Double = gains.Take(period).Average()
        Dim avgLoss As Double = losses.Take(period).Average()
        For i As Integer = period To gains.Count - 1
            avgGain = (avgGain * (period - 1) + gains(i)) / period
            avgLoss = (avgLoss * (period - 1) + losses(i)) / period
        Next
        If avgLoss = 0 Then Return 100
        Dim rs As Double = avgGain / avgLoss
        Return 100 - (100 / (1 + rs))
    End Function

    ' -- RSI Series (for divergence detection) --------------------------------
    Private Shared Function CalcRSISeries(candles As List(Of Candle), period As Integer) As List(Of Double)
        Dim result As New List(Of Double)
        If candles.Count < period + 1 Then Return result
        Dim gains As New List(Of Double)
        Dim losses As New List(Of Double)
        For i As Integer = 1 To candles.Count - 1
            Dim diff As Double = candles(i).Close - candles(i - 1).Close
            gains.Add(If(diff > 0, diff, 0))
            losses.Add(If(diff < 0, Math.Abs(diff), 0))
        Next
        Dim avgGain As Double = gains.Take(period).Average()
        Dim avgLoss As Double = losses.Take(period).Average()
        Dim rsiVal As Double = If(avgLoss = 0, 100, 100 - (100 / (1 + avgGain / avgLoss)))
        result.Add(rsiVal)
        For i As Integer = period To gains.Count - 1
            avgGain = (avgGain * (period - 1) + gains(i)) / period
            avgLoss = (avgLoss * (period - 1) + losses(i)) / period
            rsiVal = If(avgLoss = 0, 100, 100 - (100 / (1 + avgGain / avgLoss)))
            result.Add(rsiVal)
        Next
        Return result
    End Function

    ' -- RSI Divergence -------------------------------------------------------
    Public Shared Function CalcRSIDivergence(candles As List(Of Candle), period As Integer,
                                              priceGate As Double, rsiDelta As Double) As String
        If candles.Count < period + 12 Then Return "NONE"
        Dim rsiSeries = CalcRSISeries(candles, period)
        If rsiSeries.Count < 10 Then Return "NONE"

        Dim recentRSI As Double = rsiSeries.Skip(rsiSeries.Count - 5).Average()
        Dim prevRSI As Double = rsiSeries.Skip(rsiSeries.Count - 10).Take(5).Average()
        Dim recentPrice As Double = candles.Skip(candles.Count - 5).Average(Function(c) c.Close)
        Dim prevPrice As Double = candles.Skip(candles.Count - 10).Take(5).Average(Function(c) c.Close)

        If recentPrice > prevPrice * (1.0 + priceGate) AndAlso recentRSI < prevRSI - rsiDelta Then
            Return "BEARISH"
        ElseIf recentPrice < prevPrice * (1.0 - priceGate) AndAlso recentRSI > prevRSI + rsiDelta Then
            Return "BULLISH"
        End If
        Return "NONE"
    End Function

    ' -- ROC ------------------------------------------------------------------
    Public Shared Function CalcROCSeries(candles As List(Of Candle), period As Integer,
                                          lookback As Integer) As List(Of Double)
        Dim result As New List(Of Double)
        If candles.Count < period + lookback Then Return result
        For i As Integer = candles.Count - lookback To candles.Count - 1
            If i - period >= 0 Then
                Dim roc As Double = ((candles(i).Close - candles(i - period).Close) /
                                     candles(i - period).Close) * 100
                result.Add(roc)
            End If
        Next
        Return result
    End Function

    ' -- Volume SMA -----------------------------------------------------------
    Public Shared Function CalcVolumeSMA(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period Then Return 0
        Return candles.Skip(candles.Count - period).Average(Function(c) c.Volume)
    End Function

    ' -- VWAP (auto-session, parameterised boundary) --------------------------
    ' session2Hour/session2Minute: UTC time for session 2 reset (e.g. 13, 30 for US open).
    ' Before session2 time  -> session 1 (daily, resets at midnight UTC).
    ' At/after session2 time -> session 2 (e.g. US session).
    ' sessionCandleCount: number of 1m candles in the current session (for warmup guard).
    Public Shared Function CalcVWAP(candles As List(Of Candle),
                                     ByRef sessionCandleCount As Integer,
                                     Optional session2Hour As Integer = 13,
                                     Optional session2Minute As Integer = 30) As Double
        Dim nowUtc As DateTime = DateTime.UtcNow

        Dim sessionStart As DateTime
        If nowUtc.Hour < session2Hour OrElse
           (nowUtc.Hour = session2Hour AndAlso nowUtc.Minute < session2Minute) Then
            sessionStart = nowUtc.Date  ' midnight UTC
        Else
            sessionStart = nowUtc.Date.AddHours(session2Hour).AddMinutes(session2Minute)
        End If

        Dim sessionStartMs As Long = New DateTimeOffset(sessionStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim sessionCandles = candles.Where(Function(c) c.Timestamp >= sessionStartMs).ToList()
        If sessionCandles.Count = 0 Then sessionCandles = candles
        sessionCandleCount = sessionCandles.Count

        Dim cumTPV As Double = 0
        Dim cumVol As Double = 0
        For Each c In sessionCandles
            Dim tp As Double = (c.High + c.Low + c.Close) / 3
            cumTPV += tp * c.Volume
            cumVol += c.Volume
        Next
        Return If(cumVol > 0, cumTPV / cumVol, 0)
    End Function

    ' -- VWAP Sigma Bands (parameterised boundary) ----------------------------
    Public Shared Sub CalcVWAPBands(candles As List(Of Candle), vwap As Double,
                                     ByRef sigma1Upper As Double, ByRef sigma1Lower As Double,
                                     ByRef sigma2Upper As Double, ByRef sigma2Lower As Double,
                                     Optional session2Hour As Integer = 13,
                                     Optional session2Minute As Integer = 30)
        sigma1Upper = vwap : sigma1Lower = vwap
        sigma2Upper = vwap : sigma2Lower = vwap
        If vwap = 0 Then Return

        Dim nowUtc As DateTime = DateTime.UtcNow
        Dim sessionStart As DateTime
        If nowUtc.Hour < session2Hour OrElse
           (nowUtc.Hour = session2Hour AndAlso nowUtc.Minute < session2Minute) Then
            sessionStart = nowUtc.Date
        Else
            sessionStart = nowUtc.Date.AddHours(session2Hour).AddMinutes(session2Minute)
        End If
        Dim sessionStartMs As Long = New DateTimeOffset(sessionStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim sessionCandles = candles.Where(Function(c) c.Timestamp >= sessionStartMs).ToList()
        If sessionCandles.Count = 0 Then sessionCandles = candles
        If sessionCandles.Count < 2 Then Return

        Dim cumVol As Double = 0
        Dim cumWeightedSqDev As Double = 0
        For Each c In sessionCandles
            Dim tp As Double = (c.High + c.Low + c.Close) / 3
            Dim dev As Double = tp - vwap
            cumWeightedSqDev += c.Volume * dev * dev
            cumVol += c.Volume
        Next
        If cumVol = 0 Then Return
        Dim sigma As Double = Math.Sqrt(cumWeightedSqDev / cumVol)

        sigma1Upper = vwap + sigma
        sigma1Lower = vwap - sigma
        sigma2Upper = vwap + 2 * sigma
        sigma2Lower = vwap - 2 * sigma
    End Sub

    ' -- Bollinger Band Width -------------------------------------------------
    Public Shared Sub CalcBBW(candles As List(Of Candle), period As Integer, stdMult As Double,
                               ByRef bbw As Double, ByRef minBBW As Double, ByRef squeezeStatus As String)
        bbw = 0 : minBBW = Double.MaxValue : squeezeStatus = "NONE"
        If candles.Count < period Then Return

        Dim bbwSeries As New List(Of Double)
        Dim windowSize As Integer = Math.Min(candles.Count, period * 5)

        For i As Integer = candles.Count - windowSize To candles.Count - 1
            If i < period - 1 Then Continue For
            Dim window = candles.Skip(i - period + 1).Take(period).ToList()
            Dim avg As Double = window.Average(Function(c) c.Close)
            Dim variance As Double = window.Average(Function(c) (c.Close - avg) * (c.Close - avg))
            Dim stdDev As Double = Math.Sqrt(variance)
            Dim mid As Double = avg
            Dim upper As Double = mid + stdMult * stdDev
            Dim lower As Double = mid - stdMult * stdDev
            Dim bw As Double = If(mid <> 0, (upper - lower) / mid, 0)
            bbwSeries.Add(bw)
            If bw < minBBW Then minBBW = bw
        Next

        If bbwSeries.Count = 0 Then Return
        bbw = bbwSeries.Last()
        If minBBW = Double.MaxValue Then minBBW = bbw

        Dim threshold As Double = minBBW * 1.5
        If bbw <= threshold Then
            squeezeStatus = "ACTIVE"
        ElseIf bbwSeries.Count >= 2 AndAlso bbwSeries(bbwSeries.Count - 2) <= threshold Then
            squeezeStatus = "RELEASING"
        Else
            squeezeStatus = "NONE"
        End If
    End Sub

    ' -- TTM Squeeze Momentum -------------------------------------------------
    ' Computes the momentum histogram used in the TTM Squeeze indicator.
    ' Methodology: linear regression of (Close - SMA20) over the last
    ' linRegPeriod candles gives a momentum value anchored to the 20-period mean.
    ' Positive histogram = bullish momentum building; negative = bearish.
    '
    ' Direction: derived by comparing the average of the last third of the
    ' regression window against the average of the first third.
    '   delta > flatThreshold  -> RISING
    '   delta < -flatThreshold -> FALLING
    '   else                   -> FLAT
    '
    ' Signal classification:
    '   histogram > 0 AND direction = RISING  -> BULL_BUILDING
    '   histogram > 0 AND direction = FALLING -> BULL_FADING
    '   histogram < 0 AND direction = FALLING -> BEAR_BUILDING
    '   histogram < 0 AND direction = RISING  -> BEAR_FADING
    '   else                                  -> FLAT
    '
    ' Parameters:
    '   smaPeriod     -- SMA period for the mean baseline (default 20, matches BBW)
    '   linRegPeriod  -- window for linear regression (default 7)
    '   flatThreshold -- min delta to register direction change (default 0.5)
    Public Shared Sub CalcTTMSqueeze(candles As List(Of Candle),
                                      ByRef histogram As Double,
                                      ByRef direction As String,
                                      ByRef signal As String,
                                      Optional smaPeriod As Integer = 20,
                                      Optional linRegPeriod As Integer = 7,
                                      Optional flatThreshold As Double = 0.5)
        histogram = 0 : direction = "FLAT" : signal = "FLAT"
        If candles.Count < smaPeriod + linRegPeriod Then Return

        ' Build delta series: Close(i) - SMA20(i) for the last linRegPeriod candles
        Dim deltas As New List(Of Double)
        For i As Integer = candles.Count - linRegPeriod To candles.Count - 1
            Dim window = candles.Skip(i - smaPeriod + 1).Take(smaPeriod).ToList()
            Dim sma As Double = window.Average(Function(c) c.Close)
            deltas.Add(candles(i).Close - sma)
        Next

        If deltas.Count < linRegPeriod Then Return

        ' Linear regression over the deltas -- compute fitted value at the last point
        Dim n As Integer = deltas.Count
        Dim sumX As Double = 0, sumY As Double = 0
        Dim sumXY As Double = 0, sumX2 As Double = 0
        For i As Integer = 0 To n - 1
            sumX += i : sumY += deltas(i)
            sumXY += i * deltas(i) : sumX2 += i * i
        Next
        Dim denom As Double = n * sumX2 - sumX * sumX
        If denom = 0 Then Return
        Dim slope As Double = (n * sumXY - sumX * sumY) / denom
        Dim intercept As Double = (sumY - slope * sumX) / n
        histogram = intercept + slope * (n - 1)  ' fitted value at last index

        ' Direction: compare mean of last third vs mean of first third
        Dim third As Integer = Math.Max(1, n \ 3)
        Dim firstMean As Double = deltas.Take(third).Average()
        Dim lastMean As Double = deltas.Skip(n - third).Average()
        Dim delta As Double = lastMean - firstMean

        If delta > flatThreshold Then
            direction = "RISING"
        ElseIf delta < -flatThreshold Then
            direction = "FALLING"
        Else
            direction = "FLAT"
        End If

        ' Signal classification
        If histogram > 0 AndAlso direction = "RISING" Then
            signal = "BULL_BUILDING"
        ElseIf histogram > 0 AndAlso direction = "FALLING" Then
            signal = "BULL_FADING"
        ElseIf histogram < 0 AndAlso direction = "FALLING" Then
            signal = "BEAR_BUILDING"
        ElseIf histogram < 0 AndAlso direction = "RISING" Then
            signal = "BEAR_FADING"
        Else
            signal = "FLAT"
        End If
    End Sub

    ' -- OFI (Order Flow Imbalance) top-3 levels, volume-weighted (w=3,2,1) ---
    ' v0.34: parameter corrected to OrderBookSnapshot (was OrderBook -- type did not exist)
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
    ' v0.34: parameter corrected to TradeRecord (was Trade -- type did not exist)
    '        Liquidation field is a String: "M" or "T" = liquidation, "none" = normal trade
    Public Shared Sub CalcLiquidations(trades As List(Of TradeRecord),
                                        ByRef liqLongSize As Double,
                                        ByRef liqShortSize As Double,
                                        ByRef liqSignal As String)
        liqLongSize = 0 : liqShortSize = 0 : liqSignal = "NONE"
        If trades Is Nothing OrElse trades.Count = 0 Then Return
        For Each t In trades
            ' Deribit liquidation field: "M" = maker liq, "T" = taker liq, "none" = normal
            If t.Liquidation <> "none" Then
                If t.Direction = "buy" Then
                    liqShortSize += t.Amount  ' buy-side liq = short position liquidated
                Else
                    liqLongSize += t.Amount   ' sell-side liq = long position liquidated
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
    ' v0.34: parameter corrected to TradeRecord (was Trade -- type did not exist)
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

    ' -- Donchian Channel -----------------------------------------------------
    Public Shared Sub CalcDonchian(candles As List(Of Candle), period As Integer,
                                    ByRef upper As Double, ByRef lower As Double)
        upper = 0 : lower = 0
        If candles.Count < period Then Return
        Dim window = candles.Skip(candles.Count - period).Take(period).ToList()
        upper = window.Max(Function(c) c.High)
        lower = window.Min(Function(c) c.Low)
    End Sub

    ' -- OBV ------------------------------------------------------------------
    Public Shared Sub CalcOBV(candles As List(Of Candle),
                               ByRef obvTrend As String, ByRef obvDivergence As String,
                               Optional trendGate As Double = 0.01,
                               Optional divergenceGate As Double = 0.001)
        obvTrend = "FLAT" : obvDivergence = "NONE"
        If candles.Count < 3 Then Return

        Dim obvValues As New List(Of Double)
        Dim obv As Double = 0
        For i As Integer = 1 To candles.Count - 1
            If candles(i).Close > candles(i - 1).Close Then
                obv += candles(i).Volume
            ElseIf candles(i).Close < candles(i - 1).Close Then
                obv -= candles(i).Volume
            End If
            obvValues.Add(obv)
        Next

        If obvValues.Count < 2 Then Return
        Dim obvFirst As Double = obvValues(0)
        Dim obvLast As Double = obvValues.Last()
        Dim obvChange As Double = If(Math.Abs(obvFirst) > 0, (obvLast - obvFirst) / Math.Abs(obvFirst), 0)
        If obvChange > trendGate Then
            obvTrend = "RISING"
        ElseIf obvChange < -trendGate Then
            obvTrend = "FALLING"
        End If

        Dim priceFirst As Double = candles.First().Close
        Dim priceLast As Double = candles.Last().Close
        Dim priceChange As Double = If(priceFirst <> 0, (priceLast - priceFirst) / priceFirst, 0)
        If Math.Abs(priceChange) < divergenceGate Then Return

        If priceChange > 0 AndAlso obvChange < 0 Then
            obvDivergence = "BEARISH"
        ElseIf priceChange < 0 AndAlso obvChange > 0 Then
            obvDivergence = "BULLISH"
        End If
    End Sub

End Class
