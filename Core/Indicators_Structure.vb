' Core/Indicators_Structure.vb
' IndicatorEngine partial: price structure and multi-timeframe indicators.
' Covers: Donchian Channel, OBV, VPFR-lite, Swing Pivots, MTF Gate,
'         TrendStructure classification (D1), volume-weighted pivots (D2).

''' <summary>
''' Sequence classification of the most recent confirmed swing highs and lows on 5m candles.
''' UPTREND (HH+HL), DOWNTREND (LH+LL), EXPANSION (HH+LL), CONTRACTION (LH+HL), UNDEFINED.
''' </summary>
Public Enum TrendStructure
    UNDEFINED
    UPTREND
    DOWNTREND
    EXPANSION
    CONTRACTION
End Enum

Partial Public Class IndicatorEngine

    ' -- Donchian Channel -----------------------------------------------------
    ' Textbook Donchian: the channel spans the PRIOR `period` bars, excluding
    ' the current bar. With the current bar included, a full breakout required
    ' the close to equal the window max exactly — a knife-edge event that never
    ' fired. Excluding it makes "close beyond the prior channel" a genuine,
    ' close-confirmed breakout.
    Public Shared Sub CalcDonchian(candles As List(Of Candle), period As Integer,
                                    ByRef upper As Double, ByRef lower As Double)
        upper = 0 : lower = 0
        If candles.Count < period + 1 Then Return
        Dim window = candles.Skip(candles.Count - period - 1).Take(period).ToList()
        upper = window.Max(Function(c) c.High)
        lower = window.Min(Function(c) c.Low)
    End Sub

    ' -- OBV ------------------------------------------------------------------
    ' obvChange is normalised by the MEAN per-bar volume of the bars that
    ' contribute to OBV, so its units are "net OBV drift in average-bar-volumes
    ' over the window" (pure-noise drift over ~250 bars scales ≈ √249 ≈ 16).
    ' First-bar normalisation was degenerate: zero when the first two closes
    ' were equal (OBV dead for the whole run) and effectively sign() otherwise.
    ' trendGate is in the same average-bar-volume units (settings v31: 10.0).
    ''' <summary>[A54a S2, 2026-09-05] trendGate/divergenceGate were Optional with method-local
    ''' defaults; both production call sites already supplied cfg values (positionally), so
    ''' making them required is dead-code removal only.</summary>
    Public Shared Sub CalcOBV(candles As List(Of Candle),
                               ByRef obvTrend As String, ByRef obvDivergence As String,
                               trendGate As Double,
                               divergenceGate As Double)
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
        Dim meanVol As Double = candles.Skip(1).Average(Function(c) c.Volume)   ' bars 1..N-1 — the bars that contribute to OBV
        Dim obvChange As Double = If(meanVol > 0, (obvLast - obvFirst) / meanVol, 0)
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
    ''' <summary>[A54a S2, 2026-09-05] numBuckets/hvnVolPct/lvnVolPct/hvnProximityPct/decayBase/
    ''' valueAreaPct were Optional with method-local defaults; both production call sites
    ''' already supplied cfg values by name, so making them required is dead-code removal
    ''' only. ⚠ Five adjacent Doubles -- see the S2 spec-back's per-call-site diff review.</summary>
    Public Shared Sub CalcVPFRLite(candles As List(Of Candle),
                                    currentPrice As Double,
                                    ByRef poc As Double,
                                    ByRef hvnNearPoc As Boolean,
                                    ByRef signal As String,
                                    ByRef vah As Double,
                                    ByRef val As Double,
                                    ByRef valueAreaSignal As String,
                                    ByRef nearestHvnAbove As Double,
                                    ByRef nearestHvnBelow As Double,
                                    ByRef nearestLvnAbove As Double,
                                    ByRef nearestLvnBelow As Double,
                                    ByRef bucketVolumesOut As Double(),
                                    ByRef bucketPriceLowOut As Double,
                                    ByRef bucketSizeOut As Double,
                                    numBuckets As Integer,
                                    hvnVolPct As Double,
                                    lvnVolPct As Double,
                                    hvnProximityPct As Double,
                                    decayBase As Double,
                                    valueAreaPct As Double)
        poc = 0 : hvnNearPoc = False : signal = "NEUTRAL"
        vah = 0 : val = 0 : valueAreaSignal = "INSIDE_VA"
        nearestHvnAbove = 0 : nearestHvnBelow = 0
        nearestLvnAbove = 0 : nearestLvnBelow = 0
        ' Spec B histogram outputs — seed defaults so UI's Length > 0 gate
        ' suppresses the histogram cleanly when either early-return fires.
        bucketVolumesOut  = Array.Empty(Of Double)()
        bucketPriceLowOut = 0
        bucketSizeOut     = 0
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

        ' -- VAH / VAL greedy expansion from POC ----------------------------------
        ' Expand outward from POC bucket; at each step take the higher-volume neighbour.
        ' Tie-break: prefer the lower bucket (conservatism -- bias toward tighter VAL).
        Dim totalVol As Double = 0
        For i As Integer = 0 To numBuckets - 1
            totalVol += bucketVol(i)
        Next
        Dim targetVol As Double = totalVol * valueAreaPct
        Dim vahIdx As Integer = pocIdx
        Dim valIdx As Integer = pocIdx
        Dim cumVol As Double = bucketVol(pocIdx)
        Dim hiPtr  As Integer = pocIdx + 1
        Dim loPtr  As Integer = pocIdx - 1
        While cumVol < targetVol
            Dim canGoHi As Boolean = hiPtr < numBuckets
            Dim canGoLo As Boolean = loPtr >= 0
            If Not canGoHi AndAlso Not canGoLo Then Exit While
            Dim hiVol As Double = If(canGoHi, bucketVol(hiPtr), -1.0)
            Dim loVol As Double = If(canGoLo, bucketVol(loPtr), -1.0)
            ' Tie-break: prefer lower
            If loVol >= hiVol Then
                cumVol += loVol
                valIdx = loPtr
                loPtr -= 1
            Else
                cumVol += hiVol
                vahIdx = hiPtr
                hiPtr += 1
            End If
        End While
        vah = priceLow + (vahIdx + 0.5) * bucketSize
        val = priceLow + (valIdx + 0.5) * bucketSize

        If currentPrice >= vah Then
            valueAreaSignal = "ABOVE_VAH"
        ElseIf currentPrice <= val Then
            valueAreaSignal = "BELOW_VAL"
        Else
            valueAreaSignal = "INSIDE_VA"
        End If

        ' -- Nearest HVN / LVN above and below current price ---------------------
        ' Each price = bucket centre: priceLow + (idx + 0.5) * bucketSize.
        ' 0 = no qualifying bucket exists in that direction.
        For i As Integer = curIdx + 1 To numBuckets - 1
            If nearestHvnAbove = 0 AndAlso bucketVol(i) >= hvnThreshold Then
                nearestHvnAbove = priceLow + (i + 0.5) * bucketSize
            End If
            If nearestLvnAbove = 0 AndAlso bucketVol(i) <= lvnThreshold Then
                nearestLvnAbove = priceLow + (i + 0.5) * bucketSize
            End If
            If nearestHvnAbove > 0 AndAlso nearestLvnAbove > 0 Then Exit For
        Next
        For i As Integer = curIdx - 1 To 0 Step -1
            If nearestHvnBelow = 0 AndAlso bucketVol(i) >= hvnThreshold Then
                nearestHvnBelow = priceLow + (i + 0.5) * bucketSize
            End If
            If nearestLvnBelow = 0 AndAlso bucketVol(i) <= lvnThreshold Then
                nearestLvnBelow = priceLow + (i + 0.5) * bucketSize
            End If
            If nearestHvnBelow > 0 AndAlso nearestLvnBelow > 0 Then Exit For
        Next

        ' Spec B histogram outputs — happy-path assignment. bucketVol is the
        ' fully-populated weighted-volume array (index 0 = priceLow bucket).
        ' UI reverses both the array and the POC index for top-down render.
        bucketVolumesOut  = bucketVol
        bucketPriceLowOut = priceLow
        bucketSizeOut     = bucketSize
    End Sub

    ' -- Swing Pivot Detection ------------------------------------------------
    ''' <summary>
    ''' Scans candle list for the most recent confirmed swing high and swing low pivots.
    ''' A confirmed pivot has pivotWing bars on each side. Returns 0 for either if
    ''' no pivot is found within lookbackBars of the latest confirmable index.
    ''' D2 optional outputs: bestPivotByVolume / bestPivotVolumeRatio / bestPivotIsHigh —
    ''' price and relative volume of the highest-volume confirmed pivot in the lookback.
    ''' </summary>
    ''' <summary>[A54a S2, 2026-09-05] pivotWing/lookbackBars were Optional with method-local
    ''' defaults; all production call sites already supplied cfg values by name, so making them
    ''' required is dead-code removal only. bestPivotByVolume/bestPivotVolumeRatio/bestPivotIsHigh
    ''' stay Optional ByRef -- output parameters wearing Optional clothing, excluded per the
    ''' ruling.</summary>
    Public Shared Sub CalcSwingPivots(candles As List(Of Candle),
                                       ByRef lastSwingHighPrice As Double,
                                       ByRef lastSwingLowPrice As Double,
                                       pivotWing As Integer,
                                       lookbackBars As Integer,
                                       Optional ByRef bestPivotByVolume As Double = 0.0,
                                       Optional ByRef bestPivotVolumeRatio As Double = 0.0,
                                       Optional ByRef bestPivotIsHigh As Boolean = False)
        lastSwingHighPrice = 0
        lastSwingLowPrice  = 0
        bestPivotByVolume  = 0.0
        bestPivotVolumeRatio = 0.0
        bestPivotIsHigh    = False

        If candles Is Nothing OrElse candles.Count < pivotWing * 2 + 2 Then Return

        Dim scanEnd   As Integer = candles.Count - 1 - pivotWing
        If scanEnd < pivotWing Then Return
        Dim scanStart As Integer = Math.Max(pivotWing, scanEnd - lookbackBars)

        Dim foundHigh As Boolean = False
        Dim foundLow  As Boolean = False

        ' D2: collect wing-window volumes for all confirmed pivots
        Dim pivotWingVols As New List(Of Double)
        Dim bestVol       As Double  = 0
        Dim bestPrice     As Double  = 0
        Dim bestIsHigh    As Boolean = False

        For i As Integer = scanEnd To scanStart Step -1
            Dim isHigh As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(i - w).High >= candles(i).High OrElse
                   candles(i + w).High >= candles(i).High Then
                    isHigh = False : Exit For
                End If
            Next

            Dim isLow As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(i - w).Low <= candles(i).Low OrElse
                   candles(i + w).Low <= candles(i).Low Then
                    isLow = False : Exit For
                End If
            Next

            If isHigh AndAlso Not foundHigh Then
                lastSwingHighPrice = candles(i).High
                foundHigh = True
            End If
            If isLow AndAlso Not foundLow Then
                lastSwingLowPrice = candles(i).Low
                foundLow = True
            End If

            ' D2: accumulate wing-window volume for each confirmed pivot
            If isHigh OrElse isLow Then
                Dim wingStart As Integer = Math.Max(0, i - pivotWing)
                Dim wingEnd   As Integer = Math.Min(candles.Count - 1, i + pivotWing)
                Dim wingVol As Double = 0
                For j As Integer = wingStart To wingEnd
                    wingVol += candles(j).Volume
                Next
                pivotWingVols.Add(wingVol)
                ' Track the pivot type and price separately when both fire on same bar
                ' (extremely rare — normal markets alternate highs and lows)
                Dim pivPrice As Double  = If(isHigh, candles(i).High, candles(i).Low)
                Dim pivIsHigh As Boolean = isHigh
                If wingVol > bestVol Then
                    bestVol    = wingVol
                    bestPrice  = pivPrice
                    bestIsHigh = pivIsHigh
                End If
            End If
        Next

        ' D2: only populate ratio when 2+ pivots exist (1 pivot = trivially ratio 1.0, no insight)
        If pivotWingVols.Count >= 2 Then
            Dim avgVol As Double = pivotWingVols.Sum() / pivotWingVols.Count
            bestPivotByVolume    = bestPrice
            bestPivotVolumeRatio = If(avgVol > 0, bestVol / avgVol, 0.0)
            bestPivotIsHigh      = bestIsHigh
        End If
    End Sub

    ' -- Trend Structure Classification (D1) ---------------------------------
    ''' <summary>
    ''' Classifies 5m swing structure as UPTREND (HH+HL), DOWNTREND (LH+LL),
    ''' EXPANSION (HH+LL), CONTRACTION (LH+HL), or UNDEFINED (insufficient data).
    ''' Pure function — no state.
    ''' </summary>
    Public Shared Function ClassifyTrendStructure(
            candles5m       As List(Of Candle),
            pivotWing       As Integer,
            pivotCount      As Integer,
            ByRef lastTwoHighs As (Older As Double, Newer As Double),
            ByRef lastTwoLows  As (Older As Double, Newer As Double)
        ) As TrendStructure

        lastTwoHighs = (0.0, 0.0)
        lastTwoLows  = (0.0, 0.0)

        If candles5m Is Nothing OrElse candles5m.Count < pivotWing * 2 + 2 Then
            Return TrendStructure.UNDEFINED
        End If

        Dim scanEnd As Integer = candles5m.Count - 1 - pivotWing
        If scanEnd < pivotWing Then Return TrendStructure.UNDEFINED

        ' Collect up to pivotCount confirmed pivots, newest first
        Dim highs As New List(Of Double)
        Dim lows  As New List(Of Double)
        Dim total As Integer = 0

        For i As Integer = scanEnd To pivotWing Step -1
            If total >= pivotCount Then Exit For

            Dim isHigh As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles5m(i - w).High >= candles5m(i).High OrElse
                   candles5m(i + w).High >= candles5m(i).High Then
                    isHigh = False : Exit For
                End If
            Next

            Dim isLow As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles5m(i - w).Low <= candles5m(i).Low OrElse
                   candles5m(i + w).Low <= candles5m(i).Low Then
                    isLow = False : Exit For
                End If
            Next

            If isHigh Then
                highs.Add(candles5m(i).High)
                total += 1
            End If
            If isLow Then
                lows.Add(candles5m(i).Low)
                total += 1
            End If
        Next

        If highs.Count < 2 OrElse lows.Count < 2 Then
            Return TrendStructure.UNDEFINED
        End If

        ' highs(0) = newest confirmed high, highs(1) = next older
        lastTwoHighs = (Older:=highs(1), Newer:=highs(0))
        lastTwoLows  = (Older:=lows(1),  Newer:=lows(0))

        Dim isHH As Boolean = highs(0) > highs(1)  ' newer high > older high
        Dim isHL As Boolean = lows(0)  > lows(1)   ' newer low  > older low

        If isHH AndAlso isHL     Then Return TrendStructure.UPTREND
        If Not isHH AndAlso Not isHL Then Return TrendStructure.DOWNTREND
        If isHH AndAlso Not isHL Then Return TrendStructure.EXPANSION
        Return TrendStructure.CONTRACTION  ' LH + HL
    End Function

    ' -- MTF Gate (15m timeframe) ---------------------------------------------
    ' Direction-independent: computes the 15m trend state (BULL/BEAR/FLAT,
    ' 2-of-3 over DMI direction / ADX strength / EMA stack) and exposes
    ' per-side pass flags. FLAT passes both sides; no-data paths fail open
    ' (both flags True). The comparison against the verdict direction — and
    ' the composition of the final display reason — happens at scoring
    ' Step 4b against the actual dominant side, not here.
    ''' <summary>[A54a S2, 2026-09-05] adxPeriod/adxMin/minOf/candleLookback were Optional
    ''' with method-local defaults; both production call sites already supplied cfg values
    ''' by name, so making them required is dead-code removal only.</summary>
    Public Shared Sub CalcMTFGate(candles15m As List(Of Candle),
                                   ByRef mtfTrend As String,
                                   ByRef mtfADX As Double,
                                   ByRef mtfEMAAlignment As String,
                                   ByRef gatePassLong As Boolean,
                                   ByRef gatePassShort As Boolean,
                                   ByRef gateDetails As String,
                                   adxPeriod As Integer,
                                   adxMin As Double,
                                   minOf As Integer,
                                   candleLookback As Integer)
        mtfTrend = "FLAT" : mtfADX = 0 : mtfEMAAlignment = "MIXED"
        gatePassLong = True : gatePassShort = True : gateDetails = "MTF gate: no data"

        If candles15m Is Nothing OrElse candles15m.Count < adxPeriod + 2 Then
            gateDetails = "MTF: insufficient 15m candles (" &
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

        gateDetails = String.Format(
            "15m +DI:{0:F1} -DI:{1:F1} ADX:{2:F1} EMA:{3} | Bull:{4} Bear:{5} (need {6})",
            plusDI, minusDI, adxVal, mtfEMAAlignment, bullScore, bearScore, minOf)

        ' BULL or FLAT passes a long; BEAR or FLAT passes a short — preserving
        ' the original pass semantics, now exposed per side.
        gatePassLong  = (mtfTrend <> "BEAR")
        gatePassShort = (mtfTrend <> "BULL")
    End Sub

End Class
