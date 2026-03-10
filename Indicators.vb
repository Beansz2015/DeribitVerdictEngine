' Indicators.vb
' Pure calculation layer — no I/O, no UI references.
' Input: List(Of Candle). Output: typed result objects.

Public Class IndicatorResults
    ' Core
    Public Property ROC As Double
    Public Property ROCSlope As String       ' "RISING" / "FALLING" / "FLAT"
    Public Property RSI As Double
    Public Property ATR As Double
    Public Property ATRAvg20d As Double      ' approximated from fetched 1m history
    Public Property ATRSizeMultiplier As Double
    Public Property VolumeSMA9 As Double
    Public Property CurrentVolume As Double
    Public Property VolumeRatio As Double

    ' Trend (5m)
    Public Property PlusDI As Double
    Public Property MinusDI As Double
    Public Property ADX As Double
    Public Property Regime As String         ' TRENDING_UP / TRENDING_DOWN / RANGE_BOUND / TRANSITIONAL

    ' Tier 1
    Public Property VWAP As Double
    Public Property VWAPDevPct As Double
    Public Property BBW As Double
    Public Property SqueezeStatus As String  ' ACTIVE / RELEASING / NONE
    Public Property EMA9 As Double
    Public Property EMA21 As Double
    Public Property EMA50 As Double
    Public Property EMAAlignment As String   ' BULL / BEAR / MIXED
    Public Property FundingRate As Double    ' raw 8h decimal e.g. 0.0001
    Public Property FundingBias As String
    Public Property OI_Current As Double
    Public Property OI_Prev15m As Double     ' stored externally, passed in
    Public Property OI_Prev60m As Double
    Public Property OIChange15m As Double    ' % change
    Public Property OIChange60m As Double
    Public Property OISignal As String       ' NEW LONGS / NEW SHORTS / COVERING / CAPITULATION

    ' Tier 2
    Public Property OFIRatio As Double       ' buy volume / sell volume from top 5 book levels
    Public Property OFISignal As String      ' BUY DOMINANT / SELL DOMINANT / BALANCED
    Public Property LiqLongSize As Double    ' USD size of long liq in last 100 trades
    Public Property LiqShortSize As Double
    Public Property LiqSignal As String
    Public Property EMA200_5m As Double
    Public Property PriceVsEMA200 As String  ' ABOVE / BELOW

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

    ' ── DMI + ADX ────────────────────────────────────────────────────────────
    ' period = 9, smoothing = 9 (Wilder smoothing = same as RMA)
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

        ' Wilder smoothing (initial = sum of first `period` values)
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

        ' ADX = Wilder smoothed DX
        If adxList.Count < period Then
            adx = 0 : Return
        End If
        Dim smoothADX As Double = adxList.Take(period).Average()
        For i As Integer = period To adxList.Count - 1
            smoothADX = (smoothADX * (period - 1) + adxList(i)) / period
        Next
        adx = smoothADX
    End Sub

    ' ── ATR ──────────────────────────────────────────────────────────────────
    Public Shared Function CalcATR(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period + 1 Then Return 0
        Dim trValues As New List(Of Double)
        For i As Integer = 1 To candles.Count - 1
            Dim c = candles(i) : Dim p = candles(i - 1)
            trValues.Add(Math.Max(c.High - c.Low,
                         Math.Max(Math.Abs(c.High - p.Close),
                                  Math.Abs(c.Low - p.Close))))
        Next
        ' Wilder smoothed ATR
        Dim atr As Double = trValues.Take(period).Average()
        For i As Integer = period To trValues.Count - 1
            atr = (atr * (period - 1) + trValues(i)) / period
        Next
        Return atr
    End Function

    ' ── EMA ──────────────────────────────────────────────────────────────────
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

    ' EMA on a raw double list (used for RSI signal line)
    Public Shared Function CalcEMAList(values As List(Of Double), period As Integer) As Double
        If values.Count < period Then Return 0
        Dim k As Double = 2.0 / (period + 1)
        Dim ema As Double = values.Take(period).Average()
        For i As Integer = period To values.Count - 1
            ema = values(i) * k + ema * (1 - k)
        Next
        Return ema
    End Function

    ' ── RSI (EMA-smoothed Wilder) ─────────────────────────────────────────────
    Public Shared Function CalcRSI(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period + 1 Then Return 50
        Dim gains As New List(Of Double)
        Dim losses As New List(Of Double)
        For i As Integer = 1 To candles.Count - 1
            Dim diff As Double = candles(i).Close - candles(i - 1).Close
            gains.Add(If(diff > 0, diff, 0))
            losses.Add(If(diff < 0, Math.Abs(diff), 0))
        Next
        ' Wilder initial average
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

    ' ── ROC ──────────────────────────────────────────────────────────────────
    ' Returns last 3 ROC values so we can check slope
    Public Shared Function CalcROCSeries(candles As List(Of Candle), period As Integer) As List(Of Double)
        Dim result As New List(Of Double)
        If candles.Count < period + 3 Then Return result
        For i As Integer = candles.Count - 3 To candles.Count - 1
            If i - period >= 0 Then
                Dim roc As Double = ((candles(i).Close - candles(i - period).Close) /
                                     candles(i - period).Close) * 100
                result.Add(roc)
            End If
        Next
        Return result
    End Function

    ' ── Volume SMA ───────────────────────────────────────────────────────────
    Public Shared Function CalcVolumeSMA(candles As List(Of Candle), period As Integer) As Double
        If candles.Count < period Then Return 0
        Return candles.Skip(candles.Count - period).Average(Function(c) c.Volume)
    End Function

    ' ── VWAP (session from midnight UTC) ─────────────────────────────────────
    Public Shared Function CalcVWAP(candles As List(Of Candle)) As Double
        ' Filter candles from session start (00:00 UTC today)
        Dim sessionStart As Long = New DateTimeOffset(
            DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim sessionCandles = candles.Where(Function(c) c.Timestamp >= sessionStart).ToList()
        If sessionCandles.Count = 0 Then sessionCandles = candles ' fallback
        Dim cumTPV As Double = 0
        Dim cumVol As Double = 0
        For Each c In sessionCandles
            Dim tp As Double = (c.High + c.Low + c.Close) / 3
            cumTPV += tp * c.Volume
            cumVol += c.Volume
        Next
        Return If(cumVol > 0, cumTPV / cumVol, 0)
    End Function

    ' ── Bollinger Band Width ──────────────────────────────────────────────────
    ' Returns (current BBW, rolling 120-bar minimum BBW, squeeze status)
    Public Shared Sub CalcBBW(candles As List(Of Candle), period As Integer, stdMult As Double,
                               ByRef bbw As Double, ByRef minBBW As Double, ByRef squeezeStatus As String)
        bbw = 0 : minBBW = Double.MaxValue : squeezeStatus = "NONE"
        If candles.Count < period Then Return

        Dim bbwSeries As New List(Of Double)
        Dim windowSize As Integer = Math.Min(120, candles.Count)
        Dim startIdx As Integer = candles.Count - windowSize

        For i As Integer = startIdx To candles.Count - 1
            If i - period + 1 < 0 Then Continue For
            Dim window = candles.Skip(i - period + 1).Take(period).Select(Function(c) c.Close).ToList()
            Dim sma As Double = window.Average()
            Dim variance As Double = window.Average(Function(x) (x - sma) ^ 2)
            Dim sd As Double = Math.Sqrt(variance)
            Dim upper As Double = sma + stdMult * sd
            Dim lower As Double = sma - stdMult * sd
            Dim bw As Double = If(sma <> 0, (upper - lower) / sma * 100, 0)
            bbwSeries.Add(bw)
        Next

        If bbwSeries.Count = 0 Then Return
        bbw = bbwSeries.Last()
        minBBW = bbwSeries.Min()

        Dim squeezeThreshold As Double = minBBW * 1.05  ' within 5% of rolling min
        If bbw <= squeezeThreshold Then
            squeezeStatus = "ACTIVE"
        ElseIf bbwSeries.Count >= 3 AndAlso
               bbwSeries(bbwSeries.Count - 3) <= squeezeThreshold AndAlso
               bbw > squeezeThreshold Then
            squeezeStatus = "RELEASING"
        Else
            squeezeStatus = "NONE"
        End If
    End Sub

    ' ── Donchian Channel ─────────────────────────────────────────────────────
    Public Shared Sub CalcDonchian(candles As List(Of Candle), period As Integer,
                                    ByRef upper As Double, ByRef lower As Double)
        If candles.Count < period Then upper = 0 : lower = 0 : Return
        Dim window = candles.Skip(candles.Count - period).Take(period)
        upper = window.Max(Function(c) c.High)
        lower = window.Min(Function(c) c.Low)
    End Sub

    ' ── OBV ──────────────────────────────────────────────────────────────────
    ' Returns (trend string, divergence string) based on last N candles
    Public Shared Sub CalcOBV(candles As List(Of Candle),
                               ByRef trend As String, ByRef divergence As String)
        trend = "FLAT" : divergence = "NONE"
        If candles.Count < 20 Then Return

        Dim obvSeries As New List(Of Double)
        Dim obv As Double = 0
        For i As Integer = 1 To candles.Count - 1
            If candles(i).Close > candles(i - 1).Close Then
                obv += candles(i).Volume
            ElseIf candles(i).Close < candles(i - 1).Close Then
                obv -= candles(i).Volume
            End If
            obvSeries.Add(obv)
        Next

        If obvSeries.Count < 10 Then Return

        ' Simple trend: compare last 5 OBV average vs previous 5
        Dim recent5OBV As Double = obvSeries.Skip(obvSeries.Count - 5).Average()
        Dim prev5OBV As Double = obvSeries.Skip(obvSeries.Count - 10).Take(5).Average()
        Dim recentPrice As Double = candles.Skip(candles.Count - 5).Average(Function(c) c.Close)
        Dim prevPrice As Double = candles.Skip(candles.Count - 10).Take(5).Average(Function(c) c.Close)

        If recent5OBV > prev5OBV * 1.001 Then
            trend = "RISING"
        ElseIf recent5OBV < prev5OBV * 0.999 Then
            trend = "FALLING"
        Else
            trend = "FLAT"
        End If

        ' Simple divergence: price up, OBV down = bearish; price down, OBV up = bullish
        If recentPrice > prevPrice * 1.001 AndAlso recent5OBV < prev5OBV * 0.999 Then
            divergence = "BEARISH"
        ElseIf recentPrice < prevPrice * 0.999 AndAlso recent5OBV > prev5OBV * 1.001 Then
            divergence = "BULLISH"
        End If
    End Sub

    ' ── Order Flow Imbalance (from static order book snapshot) ───────────────
    ' Approximation: total bid size vs ask size across top 5 levels
    Public Shared Sub CalcOFI(book As OrderBookSnapshot,
                               ByRef ratio As Double, ByRef signal As String)
        ratio = 1.0 : signal = "BALANCED"
        If book.Bids.Count = 0 OrElse book.Asks.Count = 0 Then Return
        Dim bidVol As Double = book.Bids.Take(5).Sum(Function(b) b.Size)
        Dim askVol As Double = book.Asks.Take(5).Sum(Function(a) a.Size)
        If askVol = 0 Then Return
        ratio = bidVol / askVol
        If ratio >= 3.0 Then
            signal = "BUY DOMINANT"
        ElseIf ratio <= 1.0 / 3.0 Then
            signal = "SELL DOMINANT"
        Else
            signal = "BALANCED"
        End If
    End Sub

    ' ── Liquidation analysis ──────────────────────────────────────────────────
    Public Shared Sub CalcLiquidations(trades As List(Of TradeRecord),
                                        ByRef longLiqSize As Double,
                                        ByRef shortLiqSize As Double,
                                        ByRef signal As String)
        longLiqSize = 0 : shortLiqSize = 0 : signal = "NONE"
        ' Liquidation field: "M" = maker liq, "T" = taker liq
        ' Direction "sell" with liquidation = long liq; "buy" = short liq
        For Each t In trades
            If t.Liquidation = "none" OrElse t.Liquidation = "" Then Continue For
            If t.Direction = "sell" Then
                longLiqSize += t.Amount
            ElseIf t.Direction = "buy" Then
                shortLiqSize += t.Amount
            End If
        Next
        If longLiqSize > shortLiqSize * 2 AndAlso longLiqSize > 50000 Then
            signal = "LONG LIQS"
        ElseIf shortLiqSize > longLiqSize * 2 AndAlso shortLiqSize > 50000 Then
            signal = "SHORT LIQS"
        Else
            signal = "NONE"
        End If
    End Sub

End Class
