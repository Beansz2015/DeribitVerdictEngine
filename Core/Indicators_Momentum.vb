' Core/Indicators_Momentum.vb
' IndicatorEngine partial: momentum and trend primitives.
' Covers: DMI/ADX, ATR, EMA, RSI, RSI Divergence, ROC, Volume SMA.
'
' v0.48 [P3]: CalcRSIDivergence rewritten to use pivot-based peak/trough detection.
'   Previous implementation compared two rolling 5-bar averages separated by 5 bars.
'   This approach conflated micro-noise with genuine divergence -- any 5-bar period with
'   an elevated RSI mean vs the prior 5-bar mean triggered a signal even without a
'   structural price high/low.  The new implementation:
'     1. Scans the most recent lookback bars for the highest price pivot (for bearish div)
'        and lowest price pivot (for bullish div) using a configurable left/right wing.
'     2. Identifies the RSI value at that structural pivot and compares it to the current
'        RSI reading.
'     3. Fires BEARISH if: price made a higher high AND RSI was lower at that pivot than now
'        AND price moved more than priceGate AND rsi delta exceeds rsiDelta.
'     4. Fires BULLISH if: price made a lower low AND RSI was higher at that pivot than now.
'   Default pivotWing=3 matches the 1m scalping timeframe; tunable via optional param.
' refactor: Removed CalcEMAList(values As List(Of Double)) -- never called anywhere.
'           CalcEMA(candles, period) is the only live EMA entry point.

Partial Public Class IndicatorEngine

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

    ' -- RSI Divergence (pivot-based) -----------------------------------------
    ' [P3] v0.48: Replaced rolling-average window comparison with pivot scan.
    '   Bearish: price makes higher high (swing high) but RSI at that pivot was lower
    '            than current RSI -- weakening momentum into the new high.
    '   Bullish: price makes lower low (swing low) but RSI at that pivot was higher
    '            than current RSI -- weakening selling pressure into the new low.
    '   pivotWing: bars left and right required to confirm a swing point (default 3).
    '   lookbackBars: window to scan for the structural pivot (default 30).
    Public Shared Function CalcRSIDivergence(candles As List(Of Candle), period As Integer,
                                              priceGate As Double, rsiDelta As Double,
                                              Optional pivotWing As Integer = 3,
                                              Optional lookbackBars As Integer = 30) As String
        Dim minNeeded As Integer = period + lookbackBars + pivotWing
        If candles.Count < minNeeded Then Return "NONE"

        Dim rsiSeries = CalcRSISeries(candles, period)
        If rsiSeries.Count < lookbackBars Then Return "NONE"

        Dim scanEnd   As Integer = rsiSeries.Count - 1
        Dim scanStart As Integer = Math.Max(pivotWing, scanEnd - lookbackBars)

        Dim currentRSI   As Double = rsiSeries(scanEnd)
        Dim currentPrice As Double = candles.Last().Close

        ' ---- Bearish divergence: higher-high price pivot, lower RSI at pivot ----
        Dim bestHighPivotIdx  As Integer = -1
        Dim bestHighPrice     As Double  = Double.MinValue
        Dim bestHighRSI       As Double  = 0
        For i As Integer = scanStart To scanEnd - pivotWing
            Dim candleIdx As Integer = i + period
            If candleIdx < pivotWing OrElse candleIdx >= candles.Count - pivotWing Then Continue For
            Dim iPrice As Double = candles(candleIdx).High
            Dim isSwingHigh As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(candleIdx - w).High >= iPrice OrElse
                   candles(candleIdx + w).High >= iPrice Then
                    isSwingHigh = False : Exit For
                End If
            Next
            If isSwingHigh AndAlso iPrice > bestHighPrice Then
                bestHighPrice    = iPrice
                bestHighPivotIdx = i
                bestHighRSI      = rsiSeries(i)
            End If
        Next
        If bestHighPivotIdx >= 0 AndAlso
           bestHighPrice > currentPrice * (1.0 + priceGate) AndAlso
           bestHighRSI > currentRSI + rsiDelta Then
            Return "BEARISH"
        End If

        ' ---- Bullish divergence: lower-low price pivot, higher RSI at pivot ----
        Dim bestLowPivotIdx  As Integer = -1
        Dim bestLowPrice     As Double  = Double.MaxValue
        Dim bestLowRSI       As Double  = 0
        For i As Integer = scanStart To scanEnd - pivotWing
            Dim candleIdx As Integer = i + period
            If candleIdx < pivotWing OrElse candleIdx >= candles.Count - pivotWing Then Continue For
            Dim iPrice As Double = candles(candleIdx).Low
            Dim isSwingLow As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(candleIdx - w).Low <= iPrice OrElse
                   candles(candleIdx + w).Low <= iPrice Then
                    isSwingLow = False : Exit For
                End If
            Next
            If isSwingLow AndAlso iPrice < bestLowPrice Then
                bestLowPrice    = iPrice
                bestLowPivotIdx = i
                bestLowRSI      = rsiSeries(i)
            End If
        Next
        If bestLowPivotIdx >= 0 AndAlso
           bestLowPrice < currentPrice * (1.0 - priceGate) AndAlso
           bestLowRSI < currentRSI - rsiDelta Then
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

End Class
