' Core/Indicators_Structure.vb
' IndicatorEngine partial: price structure and multi-timeframe indicators.
' Covers: Donchian Channel, OBV, VPFR-lite, MTF Gate.

Partial Public Class IndicatorEngine

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

    ' -- VPFR-lite (Volume Profile Fixed Range using existing 1m candles) -----
    ' [P7] v0.47: Exponential decay weighting applied to candle volumes before
    ' bucketing.  Each candle's volume is multiplied by decayBase^(age) where
    ' age=0 for the most recent candle.  decayBase default 0.985 gives ~22%
    ' weight reduction per 15 bars, making the POC track intraday structure
    ' shifts rather than anchoring to high-volume events earlier in the session.
    Public Shared Sub CalcVPFRLite(candles As List(Of Candle),
                                    currentPrice As Double,
                                    ByRef poc As Double,
                                    ByRef hvnNearPoc As Boolean,
                                    ByRef signal As String,
                                    Optional numBuckets As Integer = 50,
                                    Optional hvnVolPct As Double = 0.6,
                                    Optional lvnVolPct As Double = 0.2,
                                    Optional hvnProximityPct As Double = 0.002,
                                    Optional decayBase As Double = 0.985)
        poc = 0 : hvnNearPoc = False : signal = "NEUTRAL"
        If candles Is Nothing OrElse candles.Count < 10 Then Return

        Dim priceHigh As Double = candles.Max(Function(c) c.High)
        Dim priceLow  As Double = candles.Min(Function(c) c.Low)
        Dim priceRange As Double = priceHigh - priceLow
        If priceRange <= 0 Then Return

        Dim bucketSize As Double = priceRange / numBuckets
        Dim bucketVol(numBuckets - 1) As Double

        Dim n As Integer = candles.Count
        For i As Integer = 0 To n - 1
            ' age=0 for most recent candle (index n-1), increases toward older bars
            Dim age    As Integer = n - 1 - i
            Dim weight As Double  = Math.Pow(decayBase, age)
            Dim c = candles(i)
            Dim tp As Double = (c.High + c.Low + c.Close) / 3.0
            Dim idx As Integer = CInt(Math.Floor((tp - priceLow) / bucketSize))
            If idx < 0 Then idx = 0
            If idx >= numBuckets Then idx = numBuckets - 1
            bucketVol(idx) += c.Volume * weight
        Next

        Dim pocIdx As Integer = 0
        Dim pocVol As Double = 0
        For i As Integer = 0 To numBuckets - 1
            If bucketVol(i) > pocVol Then
                pocVol = bucketVol(i)
                pocIdx = i
            End If
        Next

        poc = priceLow + (pocIdx + 0.5) * bucketSize

        Dim curIdx As Integer = CInt(Math.Floor((currentPrice - priceLow) / bucketSize))
        If curIdx < 0 Then curIdx = 0
        If curIdx >= numBuckets Then curIdx = numBuckets - 1
        Dim curBucketVol As Double = bucketVol(curIdx)

        Dim hvnThreshold As Double = pocVol * hvnVolPct
        Dim lvnThreshold As Double = pocVol * lvnVolPct

        Dim proximityPct As Double = If(poc > 0, Math.Abs(currentPrice - poc) / poc, 1.0)
        hvnNearPoc = (proximityPct <= hvnProximityPct)

        If hvnNearPoc Then
            If currentPrice < poc Then
                signal = "NEAR_HVN_SUPPORT"
            Else
                signal = "NEAR_HVN_RESIST"
            End If
        ElseIf curBucketVol <= lvnThreshold Then
            If currentPrice > poc Then
                signal = "IN_LVN_BULL"
            Else
                signal = "IN_LVN_BEAR"
            End If
        Else
            signal = "NEUTRAL"
        End If
    End Sub

    ' -- MTF Gate (15m timeframe) ---------------------------------------------
    Public Shared Sub CalcMTFGate(candles15m As List(Of Candle),
                                   ByRef mtfTrend As String,
                                   ByRef mtfADX As Double,
                                   ByRef mtfEMAAlignment As String,
                                   ByRef gatePass As Boolean,
                                   ByRef gateReason As String,
                                   Optional proposedDirection As String = "NONE",
                                   Optional adxPeriod As Integer = 9,
                                   Optional adxMin As Double = 20.0,
                                   Optional minOf As Integer = 2,
                                   Optional candleLookback As Integer = 60)
        mtfTrend = "FLAT" : mtfADX = 0 : mtfEMAAlignment = "MIXED"
        gatePass = True : gateReason = "MTF gate: no data"

        If candles15m Is Nothing OrElse candles15m.Count < adxPeriod + 2 Then
            gateReason = "MTF: insufficient 15m candles (" &
                         If(candles15m Is Nothing, "0", candles15m.Count.ToString()) & ")"
            Return
        End If

        Dim window As List(Of Candle)
        If candles15m.Count > candleLookback Then
            window = candles15m.Skip(candles15m.Count - candleLookback).ToList()
        Else
            window = candles15m
        End If

        Dim plusDI As Double = 0, minusDI As Double = 0, adxVal As Double = 0
        CalcDMI(window, adxPeriod, plusDI, minusDI, adxVal)
        mtfADX = adxVal
        Dim dmiIsBull As Boolean = plusDI > minusDI

        Dim adxStrong As Boolean = adxVal >= adxMin

        Dim ema9  As Double = CalcEMA(window, 9)
        Dim ema21 As Double = CalcEMA(window, 21)
        Dim ema50 As Double = CalcEMA(window, 50)
        Dim emaBull As Boolean = ema9 > ema21 AndAlso ema21 > ema50
        Dim emaBear As Boolean = ema9 < ema21 AndAlso ema21 < ema50
        mtfEMAAlignment = If(emaBull, "BULL", If(emaBear, "BEAR", "MIXED"))

        Dim bullScore As Integer = 0
        Dim bearScore As Integer = 0

        If dmiIsBull Then
            bullScore += 1
        Else
            bearScore += 1
        End If

        If adxStrong Then
            If dmiIsBull Then
                bullScore += 1
            Else
                bearScore += 1
            End If
        End If

        If emaBull Then
            bullScore += 1
        ElseIf emaBear Then
            bearScore += 1
        End If

        If bullScore >= minOf Then
            mtfTrend = "BULL"
        ElseIf bearScore >= minOf Then
            mtfTrend = "BEAR"
        Else
            mtfTrend = "FLAT"
        End If

        Dim details As String = String.Format(
            "15m +DI:{0:F1} -DI:{1:F1} ADX:{2:F1} EMA:{3} | Bull:{4} Bear:{5} (need {6})",
            plusDI, minusDI, adxVal, mtfEMAAlignment, bullScore, bearScore, minOf)

        Select Case proposedDirection
            Case "LONG"
                gatePass = (mtfTrend = "BULL" OrElse mtfTrend = "FLAT")
                gateReason = If(gatePass,
                    "MTF PASS [LONG] " & details,
                    "MTF BLOCK [LONG vs BEAR] " & details)
            Case "SHORT"
                gatePass = (mtfTrend = "BEAR" OrElse mtfTrend = "FLAT")
                gateReason = If(gatePass,
                    "MTF PASS [SHORT] " & details,
                    "MTF BLOCK [SHORT vs BULL] " & details)
            Case Else
                gatePass = True
                gateReason = "MTF state: " & mtfTrend & " | " & details
        End Select
    End Sub

End Class
