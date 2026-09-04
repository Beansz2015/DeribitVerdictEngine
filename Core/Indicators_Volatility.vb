' Core/Indicators_Volatility.vb
' IndicatorEngine partial: volatility and band indicators.
' Covers: VWAP, VWAP Sigma Bands, Bollinger Band Width, TTM Squeeze.
'
' v0.48 [P2]: CalcBBW squeeze threshold replaced from session-minimum (minBBW * 1.5)
' to 20th-percentile of the observed BBW series.  The old min-times-1.5 trigger fired
' frequently at market open when the very first candle had a narrow spread, inflating
' the ACTIVE count throughout the session.  The 20th-percentile threshold is stable:
' it rises and falls with realised volatility across the session and only fires when
' the current BBW is genuinely in the bottom fifth of recent behaviour.
' refactor: Removed ByRef minBBW parameter from CalcBBW. minBBW is still computed
' internally but was never read by the caller after the v0.48 [P2] threshold change.

Partial Public Class IndicatorEngine

    ' Returns the active VWAP session window, anchored at session-2 cutoff or 00:00 UTC.
    ' Falls back to the full candle list if no candle is in-session yet.
    '
    ' nowUtc (§7.5): the clock the session anchor is derived from. Nothing => DateTime.UtcNow,
    ' which is the live path and is byte-identical to the pre-parameterisation behaviour.
    ' Offline replay passes the bar close instead: anchoring historical candles to the real
    ' wall clock puts the whole slice on the wrong side of the session cutoff (the VWAP-family
    ' agreement collapse documented in backtest-overlap-validation-2026-07-30.md §8.6).
    Private Shared Function GetSessionCandles(candles As List(Of Candle),
                                              session2Hour As Integer,
                                              session2Minute As Integer,
                                              Optional nowUtc As DateTime? = Nothing) As List(Of Candle)
        Dim effNow As DateTime = If(nowUtc.HasValue, nowUtc.Value, DateTime.UtcNow)
        Dim sessionStart As DateTime
        If effNow.Hour < session2Hour OrElse
           (effNow.Hour = session2Hour AndAlso effNow.Minute < session2Minute) Then
            sessionStart = effNow.Date
        Else
            sessionStart = effNow.Date.AddHours(session2Hour).AddMinutes(session2Minute)
        End If
        Dim sessionStartMs As Long = New DateTimeOffset(sessionStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim sessionCandles = candles.Where(Function(c) c.Timestamp >= sessionStartMs).ToList()
        If sessionCandles.Count = 0 Then sessionCandles = candles
        Return sessionCandles
    End Function

    ' -- VWAP (auto-session, parameterised boundary) --------------------------
    ''' <summary>[A54a S2, 2026-09-05] session2Hour/session2Minute were Optional with
    ''' method-local defaults; both production call sites already supplied cfg values
    ''' (positionally), so making them required is dead-code removal only. nowUtc stays
    ''' Optional -- excluded per the ruling (internal convenience, no settings counterpart).</summary>
    Public Shared Function CalcVWAP(candles As List(Of Candle),
                                     ByRef sessionCandleCount As Integer,
                                     session2Hour As Integer,
                                     session2Minute As Integer,
                                     Optional nowUtc As DateTime? = Nothing) As Double
        Dim sessionCandles = GetSessionCandles(candles, session2Hour, session2Minute, nowUtc)
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
    ''' <summary>[A54a S2, 2026-09-05] session2Hour/session2Minute were Optional with
    ''' method-local defaults; both production call sites already supplied cfg values
    ''' (positionally), so making them required is dead-code removal only. nowUtc stays
    ''' Optional -- excluded per the ruling (internal convenience, no settings counterpart).</summary>
    Public Shared Sub CalcVWAPBands(candles As List(Of Candle), vwap As Double,
                                     ByRef sigma1Upper As Double, ByRef sigma1Lower As Double,
                                     ByRef sigma2Upper As Double, ByRef sigma2Lower As Double,
                                     session2Hour As Integer,
                                     session2Minute As Integer,
                                     Optional nowUtc As DateTime? = Nothing)
        sigma1Upper = vwap : sigma1Lower = vwap
        sigma2Upper = vwap : sigma2Lower = vwap
        If vwap = 0 Then Return

        Dim sessionCandles = GetSessionCandles(candles, session2Hour, session2Minute, nowUtc)
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
    ' [P2] v0.48: Squeeze threshold is now 20th-percentile of the BBW series.
    '             Previously used minBBW * 1.5 which fired on any session-low spike.
    ''' <summary>[A54a S2, 2026-09-05] seriesWindowMultiplier/squeezePercentile were Optional
    ''' with method-local defaults; both production call sites already supplied cfg values
    ''' by name, so making them required is dead-code removal only.</summary>
    Public Shared Sub CalcBBW(candles As List(Of Candle), period As Integer, stdMult As Double,
                               ByRef bbw As Double, ByRef squeezeStatus As String,
                               seriesWindowMultiplier As Integer,
                               squeezePercentile As Double)
        bbw = 0 : squeezeStatus = "NONE"
        If candles.Count < period Then Return

        Dim bbwSeries As New List(Of Double)
        Dim windowSize As Integer = Math.Min(candles.Count, period * seriesWindowMultiplier)

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
        Next

        If bbwSeries.Count = 0 Then Return
        bbw = bbwSeries.Last()

        Dim sorted = bbwSeries.OrderBy(Function(x) x).ToList()
        Dim pctIdx As Integer = CInt(Math.Floor(sorted.Count * squeezePercentile))
        If pctIdx >= sorted.Count Then pctIdx = sorted.Count - 1
        Dim threshold As Double = sorted(pctIdx)

        If bbw <= threshold Then
            squeezeStatus = "ACTIVE"
        ElseIf bbwSeries.Count >= 2 AndAlso bbwSeries(bbwSeries.Count - 2) <= threshold Then
            squeezeStatus = "RELEASING"
        Else
            squeezeStatus = "NONE"
        End If
    End Sub

    ' -- TTM Squeeze Momentum -------------------------------------------------
    ''' <summary>[A54a S2, 2026-09-05] smaPeriod/linRegPeriod/flatThreshold were Optional
    ''' with method-local defaults; both production call sites already supplied cfg values
    ''' by name, so making them required is dead-code removal only.</summary>
    Public Shared Sub CalcTTMSqueeze(candles As List(Of Candle),
                                      ByRef histogram As Double,
                                      ByRef direction As String,
                                      ByRef signal As String,
                                      smaPeriod As Integer,
                                      linRegPeriod As Integer,
                                      flatThreshold As Double)
        histogram = 0 : direction = "FLAT" : signal = "FLAT"
        If candles.Count < smaPeriod + linRegPeriod Then Return

        Dim deltas As New List(Of Double)
        For i As Integer = candles.Count - linRegPeriod To candles.Count - 1
            Dim window = candles.Skip(i - smaPeriod + 1).Take(smaPeriod).ToList()
            Dim sma As Double = window.Average(Function(c) c.Close)
            deltas.Add(candles(i).Close - sma)
        Next

        If deltas.Count < linRegPeriod Then Return

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
        histogram = intercept + slope * (n - 1)

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

End Class
