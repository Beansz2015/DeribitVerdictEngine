' Core/ScoringEngine_Calculate.vb
' ScoringEngine partial: main scoring pipeline.
' Contains the Calculate() entry point and the MaxScore constant.
' Depends on: ScoringEngine_Helpers.vb (utility methods)
'             ScoringEngine_Types.vb  (VerdictResult, ScoreState, etc.)

Partial Public Class ScoringEngine

    ' Theoretical max score (TRENDING regime, all signals firing). Legacy reference.
    Public Const MaxScore As Integer = 17

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState,
                                     norms As DynamicNorms,
                                     cfg As EngineSettings) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        Dim regimeMax As Integer = RegimeMaxScore(r.Regime)
        res.MaxScore = regimeMax

        ' -- Step 2: Weighted Signal Scoring ----------------------------------

        ' ROC (Momentum)
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong As Boolean = r.ROC > 0.1 AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < -0.1 AndAlso r.ROCSlope <> "FALLING"
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum)

        ' RSI (Momentum)
        Dim rsiLong As Boolean = r.RSI > 60
        Dim rsiShort As Boolean = r.RSI < 40
        Dim rsiPartialLong As Boolean = r.RSI > 50 AndAlso r.RSI <= 60
        Dim rsiPartialShort As Boolean = r.RSI < 50 AndAlso r.RSI >= 40
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum)

        ' DMI (MarketStructure)
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.MarketStructure)

        ' ADX (MarketStructure)
        Dim adxLong As Boolean = r.ADX > 25 AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > 25 AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.MarketStructure)

        ' Volume (Volume)
        Dim volHigh As Double = norms.VolHighThreshold
        Dim volMid As Double = norms.VolMidThreshold
        Dim volLong As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volShort As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP
        Dim volPartial As Boolean = r.VolumeRatio >= volMid AndAlso r.VolumeRatio < volHigh
        AddFull(state, volLong, volShort, SignalCategory.Volume)

        ' VWAP (Microstructure) -- adaptive sigma bands
        Dim vwapLong As Boolean = False
        Dim vwapShort As Boolean = False
        Dim vwapPartialLong As Boolean = False
        Dim vwapPartialShort As Boolean = False
        Dim vwapNote As String
        Dim vwapWarmup As Boolean = r.VWAPSessionCandles < 15

        If vwapWarmup Then
            vwapNote = String.Format("WARMUP ({0}/15 candles) -- signal suppressed", r.VWAPSessionCandles)
        Else
            Dim price As Double = r.CurrentPrice
            vwapLong  = price > r.VWAP AndAlso price <= r.VWAPSigma1Upper
            vwapShort = price < r.VWAP AndAlso price >= r.VWAPSigma1Lower
            vwapPartialLong  = price > r.VWAPSigma1Upper AndAlso price <= r.VWAPSigma2Upper
            vwapPartialShort = price < r.VWAPSigma1Lower AndAlso price >= r.VWAPSigma2Lower
            AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure)
            vwapNote = String.Format(
                "Price:{0:F0} VWAP:{1:F0} | σ1:[{2:F0},{3:F0}] σ2:[{4:F0},{5:F0}] | {6}candles",
                price, r.VWAP,
                r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                r.VWAPSigma2Lower, r.VWAPSigma2Upper,
                r.VWAPSessionCandles)
        End If

        ' BBW / TTM Squeeze scoring
        Dim bbwLongHit As Boolean = False
        Dim bbwShortHit As Boolean = False
        ' v0.31: initialised to "" to prevent BC42104; overwritten by every Select Case arm.
        Dim bbwNote As String = ""

        Select Case r.SqueezeStatus
            Case "ACTIVE"
                state.LongScore  = Math.Max(0, state.LongScore  - 1)
                state.ShortScore = Math.Max(0, state.ShortScore - 1)
                bbwNote = String.Format("{0:F3} | ACTIVE -- penalty -1 both sides | TTM:{1} {2} H:{3:F1}",
                                        r.BBW, r.TTMSignal, r.TTMDirection, r.TTMHistogram)
            Case "RELEASING", "NONE"
                Select Case r.TTMSignal
                    Case "BULL_BUILDING"
                        state.LongScore += 1
                        state.FullLongCategories.Add(SignalCategory.Microstructure)
                        bbwLongHit = True
                        bbwNote = String.Format("{0:F3} | {1} -- BULL_BUILDING [L] H:{2:F1}",
                                                r.BBW, r.SqueezeStatus, r.TTMHistogram)
                    Case "BEAR_BUILDING"
                        state.ShortScore += 1
                        state.FullShortCategories.Add(SignalCategory.Microstructure)
                        bbwShortHit = True
                        bbwNote = String.Format("{0:F3} | {1} -- BEAR_BUILDING [S] H:{2:F1}",
                                                r.BBW, r.SqueezeStatus, r.TTMHistogram)
                    Case Else
                        bbwNote = String.Format("{0:F3} | {1} -- {2} {3} H:{4:F1} -- no award",
                                                r.BBW, r.SqueezeStatus,
                                                r.TTMSignal, r.TTMDirection, r.TTMHistogram)
                End Select
            Case Else
                bbwNote = String.Format("{0:F3} | unexpected status:{1} -- no award", r.BBW, r.SqueezeStatus)
        End Select

        ' EMA Ribbon (MarketStructure)
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.MarketStructure)

        ' OI (Microstructure)
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure)

        ' OFI (Microstructure)
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure)

        ' CVD (Microstructure)
        Dim cvdLong  As Boolean = r.CVDSlope = "RISING"  AndAlso r.CVDValue > 0
        Dim cvdShort As Boolean = r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0
        AddFull(state, cvdLong, cvdShort, SignalCategory.Microstructure)
        If r.CVDDivergence = "BEARISH" Then state.LongScore  = Math.Max(0, state.LongScore  - 1)
        If r.CVDDivergence = "BULLISH" Then state.ShortScore = Math.Max(0, state.ShortScore - 1)

        ' Liquidations -- penalty only
        Dim liqLongPenalty As Integer = 0
        Dim liqShortPenalty As Integer = 0
        If r.LiqSignal = "LONG LIQS" Then
            liqLongPenalty = If(r.LiqLongSize > cfg.Indicators.Liquidations.LargeLiqSize, 2, 1)
            state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)
        ElseIf r.LiqSignal = "SHORT LIQS" Then
            liqShortPenalty = If(r.LiqShortSize > cfg.Indicators.Liquidations.LargeLiqSize, 2, 1)
            state.ShortScore = Math.Max(0, state.ShortScore - liqShortPenalty)
        End If

        ' 5m EMA200 (MarketStructure)
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.MarketStructure)

        ' Donchian (MarketStructure)
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        AddFull(state, donchLong, donchShort, SignalCategory.MarketStructure)

        ' OBV (Volume)
        ' v0.32 fix: full signal awarded to any rising OBV not warning bearishly.
        Dim obvLong        As Boolean = r.OBVTrend = "RISING"  AndAlso r.OBVDivergence <> "BEARISH"
        Dim obvShort       As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence <> "BULLISH"
        Dim obvPartialLong  As Boolean = r.OBVTrend = "RISING"  AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume)

        ' VPFR-lite (MarketStructure)
        Dim vpfrLong  As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")
        Dim vpfrShort As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST"  OrElse r.VPFRSignal = "IN_LVN_BEAR")
        AddFull(state, vpfrLong, vpfrShort, SignalCategory.MarketStructure)

        ' Pass 2: partial upgrades
        Dim rocLongUpgraded As Boolean = rocPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += 1
        If rocShortUpgraded Then state.ShortScore += 1

        Dim rsiLongUpgraded As Boolean = rsiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += 1
        If rsiShortUpgraded Then state.ShortScore += 1

        Dim vwapLongUpgraded As Boolean = False
        Dim vwapShortUpgraded As Boolean = False
        If Not vwapWarmup Then
            vwapLongUpgraded  = vwapPartialLong  AndAlso HasCrossConfirm(state.FullLongCategories,  SignalCategory.Microstructure)
            vwapShortUpgraded = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
            If vwapLongUpgraded  Then state.LongScore  += 1
            If vwapShortUpgraded Then state.ShortScore += 1
        End If

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' Breakdown notes
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC")

        breakdown.Add(New SignalBreakdownItem("ROC(9)", rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
            BuildNote(String.Format("{0:F3} | Slope: {1}", r.ROC, r.ROCSlope),
                      rocPartialLong AndAlso Not rocLongUpgraded, rocPartialShort AndAlso Not rocShortUpgraded,
                      rocLongUpgraded, rocShortUpgraded)))

        Dim rsiNote As String = String.Format("{0:F1}", r.RSI)
        If r.RSIDivergence <> "NONE" Then rsiNote &= String.Format(" | DIV:{0}", r.RSIDivergence)
        breakdown.Add(New SignalBreakdownItem("RSI(9)", rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
            BuildNote(rsiNote,
                      rsiPartialLong AndAlso Not rsiLongUpgraded, rsiPartialShort AndAlso Not rsiShortUpgraded,
                      rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("DMI +/-DI", dmiLong, dmiShort,
            String.Format("+DI:{0:F1} -DI:{1:F1}", r.PlusDI, r.MinusDI)))

        breakdown.Add(New SignalBreakdownItem("ADX>25", adxLong, adxShort,
            String.Format("{0:F1}", r.ADX)))

        breakdown.Add(New SignalBreakdownItem("Volume", volLong, volShort,
            BuildNote(String.Format("{0:F2}x | thr H:{1:F2}x M:{2:F2}x [{3}]",
                                   r.VolumeRatio, volHigh, volMid, normMode),
                      volPartial, volPartial, False, False)))

        breakdown.Add(New SignalBreakdownItem("VWAP", vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
            If(vwapWarmup, vwapNote,
               BuildNote(vwapNote,
                         vwapPartialLong AndAlso Not vwapLongUpgraded,
                         vwapPartialShort AndAlso Not vwapShortUpgraded,
                         vwapLongUpgraded, vwapShortUpgraded))))

        breakdown.Add(New SignalBreakdownItem("BBW/TTM", bbwLongHit, bbwShortHit, bbwNote))

        breakdown.Add(New SignalBreakdownItem("EMA 9/21/50", emaBull, emaBear,
            String.Format("9:{0:F0} 21:{1:F0} 50:{2:F0} | {3}", r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment)))

        breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False,
            String.Format("{0:F4}% | {1}", r.FundingRate * 100, r.FundingBias)))

        breakdown.Add(New SignalBreakdownItem("OI Delta", oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
            BuildNote(String.Format("15m:{0:F2}% 60m:{1:F2}% | {2}", r.OIChange15m, r.OIChange60m, r.OISignal),
                      oiPartialLong AndAlso Not oiLongUpgraded, oiPartialShort AndAlso Not oiShortUpgraded,
                      oiLongUpgraded, oiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("OFI", ofiBuy, ofiSell,
            String.Format("Ratio:{0:F2} | {1}", r.OFIRatio, r.OFISignal)))

        Dim cvdNote As String = String.Format("Net:{0:F0} | Slope:{1} | Div:{2}", r.CVDValue, r.CVDSlope, r.CVDDivergence)
        If r.CVDDivergence <> "NONE" Then cvdNote &= " | PENALTY -1"
        breakdown.Add(New SignalBreakdownItem("CVD", cvdLong, cvdShort, cvdNote))

        Dim liqNote As String = String.Format("L:{0:F0} S:{1:F0} | {2}", r.LiqLongSize, r.LiqShortSize, r.LiqSignal)
        If liqLongPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [L]", liqLongPenalty)
        If liqShortPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [S]", liqShortPenalty)
        breakdown.Add(New SignalBreakdownItem("Liq Penalty", liqLongPenalty > 0, liqShortPenalty > 0, liqNote))

        breakdown.Add(New SignalBreakdownItem("5m EMA(200)", ema200Bull, ema200Bear,
            String.Format("{0:F0} | {1}", r.EMA200_5m, r.PriceVsEMA200)))

        breakdown.Add(New SignalBreakdownItem("Donchian(20)", donchLong, donchShort,
            String.Format("U:{0:F0} L:{1:F0} | {2}", r.DonchianUpper, r.DonchianLower, r.DonchianSignal)))

        breakdown.Add(New SignalBreakdownItem("OBV", obvLong OrElse obvLongUpgraded, obvShort OrElse obvShortUpgraded,
            BuildNote(String.Format("Trend:{0} Div:{1}", r.OBVTrend, r.OBVDivergence),
                      obvPartialLong AndAlso Not obvLongUpgraded, obvPartialShort AndAlso Not obvShortUpgraded,
                      obvLongUpgraded, obvShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("VPFR-lite", vpfrLong, vpfrShort,
            String.Format("POC:{0:F0} | {1} | HVN@POC:{2}",
                          r.VPFRPoc, r.VPFRSignal, If(r.VPFRHVNearPoc, "YES", "NO"))))

        ' -- Step 3: Funding Rate Confidence Modifier -------------------------
        Dim ls As Integer = state.LongScore
        Dim ss As Integer = state.ShortScore
        Dim fr As Double = r.FundingRate
        If fr > cfg.Scoring.FundingHighPositive Then
            ls -= 2 : ss += 1
        ElseIf fr > cfg.Scoring.FundingLowPositive Then
            ls -= 1
        ElseIf fr < cfg.Scoring.FundingHighNegative Then
            ss -= 2 : ls += 1
        ElseIf fr < cfg.Scoring.FundingLowNegative Then
            ss -= 1
        End If
        ls = Math.Max(0, ls)
        ss = Math.Max(0, ss)

        ' -- Step 4: Regime Veto / Override -----------------------------------
        Dim effectiveLS As Integer = ls
        Dim effectiveSS As Integer = ss
        Dim adxPenalty As Integer = 0

        Select Case r.Regime
            Case "TRENDING_UP"
                If ss > ls Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If ls > ss Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRANSITIONAL"
                Dim penLow  As Double = cfg.RegimeGates.TransitionalAdxPenaltyLow
                Dim penMid  As Double = cfg.RegimeGates.TransitionalAdxPenaltyMid
                If r.ADX >= penLow AndAlso r.ADX < penMid Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyLow
                ElseIf r.ADX >= penMid AndAlso r.ADX < cfg.RegimeGates.TransitionalAdxPenaltyHigh Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyMid
                End If
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss))
        End Select

        ' -- Step 4b: MTF Gate Veto -------------------------------------------
        Dim tWeakCheck As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)
        Dim proposedDir As String = "NONE"
        If effectiveLS >= tWeakCheck AndAlso effectiveLS >= effectiveSS Then
            proposedDir = "LONG"
        ElseIf effectiveSS >= tWeakCheck AndAlso effectiveSS > effectiveLS Then
            proposedDir = "SHORT"
        End If

        Dim mtfBlocked As Boolean = False
        If cfg.MTFGate.Enabled AndAlso proposedDir <> "NONE" AndAlso Not r.MTFGatePass Then
            mtfBlocked = True
        End If

        ' Always add MTF info row to breakdown regardless of gate result
        breakdown.Add(New SignalBreakdownItem("MTF Gate (15m)",
            r.MTFGatePass AndAlso proposedDir = "LONG",
            r.MTFGatePass AndAlso proposedDir = "SHORT",
            r.MTFGateReason))

        If mtfBlocked Then
            res.Verdict = "NO TRADE" : res.Confidence = "N/A"
            res.LongScore = ls : res.ShortScore = ss
            res.EffectiveLongScore = effectiveLS : res.EffectiveShortScore = effectiveSS
            res.RegimePenalty = adxPenalty
            res.HoldStatus = CalcHoldStatus(r, posState)
            Return res
        End If

        ' -- Step 5: Generate Verdict -----------------------------------------
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        Dim tStrong As Integer = Threshold(regimeMax, cfg.Scoring.VerdictStrongPct)
        Dim tMed    As Integer = Threshold(regimeMax, cfg.Scoring.VerdictMedPct)
        Dim tWeak   As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)

        If effectiveLS >= tStrong Then
            res.Verdict = "STRONG LONG" : res.Confidence = "HIGH"
        ElseIf effectiveLS >= tMed Then
            res.Verdict = "LONG" : res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= tWeak Then
            res.Verdict = "WEAK LONG" : res.Confidence = "LOW"
        ElseIf effectiveSS >= tStrong Then
            res.Verdict = "STRONG SHORT" : res.Confidence = "HIGH"
        ElseIf effectiveSS >= tMed Then
            res.Verdict = "SHORT" : res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= tWeak Then
            res.Verdict = "WEAK SHORT" : res.Confidence = "LOW"
        Else
            res.Verdict = "NO TRADE" : res.Confidence = "N/A"
        End If

        ' -- Step 6: Hold / Exit Assessment -----------------------------------
        res.HoldStatus = CalcHoldStatus(r, posState)
        Return res
    End Function

End Class
