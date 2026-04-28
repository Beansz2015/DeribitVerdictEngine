' Core/ScoringEngine_Calculate_Scoring.vb
' ScoringEngine partial: Step 2 signal scoring, partial upgrades, Steps 3/3b funding.
' Split from ScoringEngine_Calculate.vb for maintainability.
'
' Contains:
'   - AppendLean()
'   - CalcVerdictContext()
'   - Signal scoring pass (Step 2)
'   - Partial upgrade pass (Pass 2)
'   - OI x CVD cross-confirm gate (Pass 2b)
'   - Regime alignment gate (Pass 2c)
'   - Funding modifier (Steps 3 / 3b)
'   - Breakdown note construction

Partial Public Class ScoringEngine

    Private Shared Function AppendLean(verdict As String, ls As Integer, ss As Integer, tWeak As Integer) As String
        If ls >= tWeak AndAlso ls >= ss Then
            Return verdict & " [WEAK LONG]"
        ElseIf ss >= tWeak AndAlso ss > ls Then
            Return verdict & " [WEAK SHORT]"
        End If
        Return verdict
    End Function

    Private Shared Function CalcVerdictContext(
        v       As VerdictResult,
        r       As IndicatorResults,
        state   As ScoreState,
        cfg     As EngineSettings) As String

        Dim isLong As Boolean = (v.LongScore >= v.ShortScore)

        Dim structScore As Integer = 0
        Dim flowScore   As Integer = 0
        For Each item In v.SignalBreakdown
            Dim hit As Boolean = If(isLong, item.LongHit, item.ShortHit)
            If Not hit Then Continue For
            Dim lbl As String = item.Label
            If lbl = "VWAP"         OrElse lbl = "BBW/TTM"     OrElse
               lbl = "EMA 9/21/50"  OrElse lbl = "DMI +/-DI"   OrElse
               lbl.StartsWith("ADX>")                           OrElse
               lbl = "Donchian(20)" OrElse lbl = "5m EMA(200)" Then
                structScore += 1
            End If
            If lbl = "OFI"      OrElse lbl = "CVD"      OrElse lbl = "TFI"     OrElse
               lbl = "MicroCVD" OrElse lbl = "OI Delta" OrElse
               lbl = "ROC(9)"   OrElse lbl = "Volume"   Then
                flowScore += 1
            End If
        Next

        Dim fadingCount As Integer = 0
        If isLong Then
            If r.MicroCVDSignal = "BULL_DECEL" Then fadingCount += 1
            If r.TTMSignal = "BULL_FADING" Then fadingCount += 1
            If r.RSI >= cfg.Indicators.RSI.DivPenaltyRsiHigh Then fadingCount += 1
            If r.MicroCVDEarly > 0 AndAlso r.MicroCVDLate < r.MicroCVDEarly * 0.5 Then fadingCount += 1
        Else
            If r.MicroCVDSignal = "BEAR_DECEL" Then fadingCount += 1
            If r.TTMSignal = "BEAR_FADING" Then fadingCount += 1
            If r.RSI <= cfg.Indicators.RSI.DivPenaltyRsiLow Then fadingCount += 1
            If r.MicroCVDEarly < 0 AndAlso r.MicroCVDLate > r.MicroCVDEarly * 0.5 Then fadingCount += 1
        End If
        If fadingCount >= 2 Then Return "MOMENTUM_FADING"

        If structScore >= cfg.Scoring.ContextTagStructuralMin AndAlso
           flowScore <= cfg.Scoring.ContextTagFlowMax Then
            Return "FLOW_UNCONFIRMED"
        End If

        If structScore < 2 AndAlso flowScore < 2 Then
            Return "STRUCTURALLY_WEAK"
        End If

        Return "CONFIRMED"
    End Function

    ' Returns a ScoreState and populated breakdown list after running all Step 2
    ' signal scoring, partial upgrades, Pass 2b OI x CVD gate, Steps 3/3b funding modifiers.
    ' Also outputs ls/ss as ByRef for use by the verdict stage.
    Friend Shared Sub RunScoringPipeline(
        r         As IndicatorResults,
        norms     As DynamicNorms,
        cfg       As EngineSettings,
        res       As VerdictResult,
        state     As ScoreState,
        ByRef ls  As Integer,
        ByRef ss  As Integer)

        Dim breakdown = res.SignalBreakdown
        Dim regimeMax As Integer = res.MaxScore

        Dim adxTrend As Double = cfg.Indicators.ADX.TrendThreshold
        Dim rsiOB      As Double = cfg.Indicators.RSI.Overbought
        Dim rsiOS      As Double = cfg.Indicators.RSI.Oversold
        Dim rsiPartOB  As Double = cfg.Indicators.RSI.PartialOverbought
        Dim rsiPartOS  As Double = cfg.Indicators.RSI.PartialOversold

        ' -- Step 2: Weighted Signal Scoring ----------------------------------
        Dim rocLong         As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort        As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong  As Boolean = r.ROC > cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < -cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "FALLING"
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum)

        Dim rsiLong         As Boolean = r.RSI > rsiOB
        Dim rsiShort        As Boolean = r.RSI < rsiOS
        Dim rsiPartialLong  As Boolean = r.RSI > rsiPartOB AndAlso r.RSI <= rsiOB
        Dim rsiPartialShort As Boolean = r.RSI < rsiPartOS AndAlso r.RSI >= rsiOS
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum)

        Dim rsiDivPenaltyHigh As Double = cfg.Indicators.RSI.DivPenaltyRsiHigh
        Dim rsiDivPenaltyLow  As Double = cfg.Indicators.RSI.DivPenaltyRsiLow
        Dim rsiDivPenaltyLong  As Boolean = False
        Dim rsiDivPenaltyShort As Boolean = False
        If r.RSIDivergence = "BEARISH" AndAlso r.RSI > rsiDivPenaltyHigh Then
            state.LongScore = Math.Max(0, state.LongScore - 1)
            rsiDivPenaltyLong = True
        End If
        If r.RSIDivergence = "BULLISH" AndAlso r.RSI < rsiDivPenaltyLow Then
            state.ShortScore = Math.Max(0, state.ShortScore - 1)
            rsiDivPenaltyShort = True
        End If

        Dim dmiLong  As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.MarketStructure)

        Dim adxLong  As Boolean = r.ADX > adxTrend AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > adxTrend AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.MarketStructure)

        Dim volHigh As Double = norms.VolHighThreshold
        Dim volMid  As Double = norms.VolMidThreshold
        Dim volLong    As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volShort   As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP
        Dim volPartial As Boolean = r.VolumeRatio >= volMid AndAlso r.VolumeRatio < volHigh
        AddFull(state, volLong, volShort, SignalCategory.Volume)

        Dim volMidLong  As Boolean = volPartial AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volMidShort As Boolean = volPartial AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP

        Dim vwapLong         As Boolean = False
        Dim vwapShort        As Boolean = False
        Dim vwapPartialLong  As Boolean = False
        Dim vwapPartialShort As Boolean = False
        Dim vwapNote As String
        Dim vwapWarmup As Boolean = r.VWAPSessionCandles < cfg.Indicators.VWAP.WarmupCandles

        If vwapWarmup Then
            vwapNote = String.Format("WARMUP ({0}/{1} candles) -- signal suppressed", r.VWAPSessionCandles, cfg.Indicators.VWAP.WarmupCandles)
        Else
            Dim price As Double = r.CurrentPrice
            vwapLong = price > r.VWAP AndAlso price <= r.VWAPSigma1Upper
            vwapShort = price < r.VWAP AndAlso price >= r.VWAPSigma1Lower
            vwapPartialLong = price > r.VWAPSigma1Upper AndAlso price <= r.VWAPSigma2Upper
            vwapPartialShort = price < r.VWAPSigma1Lower AndAlso price >= r.VWAPSigma2Lower
            AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure)
            vwapNote = String.Format("Price:{0:F0} VWAP:{1:F0} | s1:[{2:F0},{3:F0}] s2:[{4:F0},{5:F0}] | {6}candles",
                                     price, r.VWAP, r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                                     r.VWAPSigma2Lower, r.VWAPSigma2Upper, r.VWAPSessionCandles)
        End If

        Dim bbwLongHit  As Boolean = False
        Dim bbwShortHit As Boolean = False
        Dim bbwNote As String = ""
        Select Case r.SqueezeStatus
            Case "ACTIVE"
                state.LongScore = Math.Max(0, state.LongScore - cfg.Scoring.BbwSqueezePenalty)
                state.ShortScore = Math.Max(0, state.ShortScore - cfg.Scoring.BbwSqueezePenalty)
                bbwNote = String.Format("{0:F3} | ACTIVE -- penalty -{1} both sides | TTM:{2} {3} H:{4:F1}",
                                        r.BBW, cfg.Scoring.BbwSqueezePenalty,
                                        r.TTMSignal, r.TTMDirection, r.TTMHistogram)
            Case "RELEASING", "NONE"
                Select Case r.TTMSignal
                    Case "BULL_BUILDING"
                        state.LongScore += 1
                        state.FullLongCategories.Add(SignalCategory.Microstructure)
                        bbwLongHit = True
                        bbwNote = String.Format("{0:F3} | {1} -- BULL_BUILDING [L] H:{2:F1}", r.BBW, r.SqueezeStatus, r.TTMHistogram)
                    Case "BEAR_BUILDING"
                        state.ShortScore += 1
                        state.FullShortCategories.Add(SignalCategory.Microstructure)
                        bbwShortHit = True
                        bbwNote = String.Format("{0:F3} | {1} -- BEAR_BUILDING [S] H:{2:F1}", r.BBW, r.SqueezeStatus, r.TTMHistogram)
                    Case Else
                        bbwNote = String.Format("{0:F3} | {1} -- {2} {3} H:{4:F1} -- no award",
                                                r.BBW, r.SqueezeStatus, r.TTMSignal, r.TTMDirection, r.TTMHistogram)
                End Select
        End Select

        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.MarketStructure)

        Dim oiLong         As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort        As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong  As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure)

        Dim ofiBuy  As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure)

        Dim cvdLong  As Boolean = r.CVDSlope = "RISING" AndAlso r.CVDValue > 0
        Dim cvdShort As Boolean = r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0
        AddFull(state, cvdLong, cvdShort, SignalCategory.Microstructure)
        If r.CVDDivergence = "BEARISH" Then state.LongScore = Math.Max(0, state.LongScore - cfg.Indicators.CVD.DivergencePenalty)
        If r.CVDDivergence = "BULLISH" Then state.ShortScore = Math.Max(0, state.ShortScore - cfg.Indicators.CVD.DivergencePenalty)

        Dim tfiLong  As Boolean = r.TFISignal = "BUY PRESSURE"
        Dim tfiShort As Boolean = r.TFISignal = "SELL PRESSURE"
        AddFull(state, tfiLong, tfiShort, SignalCategory.Microstructure)

        Dim microLong  As Boolean = r.MicroCVDSignal = "BULL_ACCEL"
        Dim microShort As Boolean = r.MicroCVDSignal = "BEAR_ACCEL"
        AddFull(state, microLong, microShort, SignalCategory.Microstructure)
        If r.MicroCVDSignal = "BULL_DECEL" Then state.ShortScore = Math.Max(0, state.ShortScore - cfg.Indicators.MicroCVD.DecelPenalty)
        If r.MicroCVDSignal = "BEAR_DECEL" Then state.LongScore = Math.Max(0, state.LongScore - cfg.Indicators.MicroCVD.DecelPenalty)

        Dim microFlatStallLong  As Boolean = False
        Dim microFlatStallShort As Boolean = False
        If r.MicroCVDSignal = "FLAT" Then
            If r.CurrentPrice > r.VWAP AndAlso r.CVDValue <= 0 Then
                state.LongScore = Math.Max(0, state.LongScore - cfg.Indicators.MicroCVD.DecelPenalty)
                microFlatStallLong = True
            ElseIf r.CurrentPrice < r.VWAP AndAlso r.CVDValue >= 0 Then
                state.ShortScore = Math.Max(0, state.ShortScore - cfg.Indicators.MicroCVD.DecelPenalty)
                microFlatStallShort = True
            End If
        End If

        Dim liqLongPenalty  As Integer = 0
        Dim liqShortPenalty As Integer = 0
        If r.LiqSignal = "LONG LIQS" Then
            liqLongPenalty = If(r.LiqLongSize > cfg.Indicators.Liquidations.LargeLiqSize,
                                cfg.Scoring.LiqLargePenalty, cfg.Scoring.LiqStandardPenalty)
            state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)
        ElseIf r.LiqSignal = "SHORT LIQS" Then
            liqShortPenalty = If(r.LiqShortSize > cfg.Indicators.Liquidations.LargeLiqSize,
                                 cfg.Scoring.LiqLargePenalty, cfg.Scoring.LiqStandardPenalty)
            state.ShortScore = Math.Max(0, state.ShortScore - liqShortPenalty)
        End If

        ' Spread microstructure penalty -- WIDE spread on entry side reduces signal quality.
        ' ROC > 0  -> penalise long side (entering long into widening ask)
        ' ROC < 0  -> penalise short side (entering short into widening bid)
        ' ROC ~= 0 -> penalise both sides (general execution warning)
        Dim spreadPenaltyLong  As Integer = 0
        Dim spreadPenaltyShort As Integer = 0
        If r.SpreadStatus = "WIDE" Then
            Dim pen       As Integer = cfg.Scoring.SpreadWidePenalty
            Dim slopeSens As Double  = cfg.Indicators.ROC.SlopeSensitivity
            If r.ROC > slopeSens Then
                spreadPenaltyLong = pen
                state.LongScore = Math.Max(0, state.LongScore - pen)
            ElseIf r.ROC < -slopeSens Then
                spreadPenaltyShort = pen
                state.ShortScore = Math.Max(0, state.ShortScore - pen)
            Else
                spreadPenaltyLong  = pen
                spreadPenaltyShort = pen
                state.LongScore  = Math.Max(0, state.LongScore  - pen)
                state.ShortScore = Math.Max(0, state.ShortScore - pen)
            End If
        End If

        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.MarketStructure)

        Dim donchLong         As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort        As Boolean = r.DonchianSignal = "SHORT"
        Dim donchPartialLong  As Boolean = r.DonchianSignal = "LONG_PARTIAL"
        Dim donchPartialShort As Boolean = r.DonchianSignal = "SHORT_PARTIAL"
        AddFull(state, donchLong, donchShort, SignalCategory.MarketStructure)

        Dim obvLong         As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence <> "BEARISH"
        Dim obvShort        As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence <> "BULLISH"
        Dim obvPartialLong  As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume)

        Dim vpfrLong  As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")
        Dim vpfrShort As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")
        AddFull(state, vpfrLong, vpfrShort, SignalCategory.MarketStructure)

        ' -- Pass 2: Partial upgrades -----------------------------------------
        Dim rocLongUpgraded  As Boolean = rocPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += 1
        If rocShortUpgraded Then state.ShortScore += 1

        Dim rsiLongUpgraded  As Boolean = rsiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += 1
        If rsiShortUpgraded Then state.ShortScore += 1

        Dim vwapLongUpgraded  As Boolean = False
        Dim vwapShortUpgraded As Boolean = False
        If Not vwapWarmup Then
            vwapLongUpgraded = vwapPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
            vwapShortUpgraded = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
            If vwapLongUpgraded Then state.LongScore += 1
            If vwapShortUpgraded Then state.ShortScore += 1
        End If

        Dim oiLongUpgraded  As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim donchLongUpgraded  As Boolean = donchPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.MarketStructure)
        Dim donchShortUpgraded As Boolean = donchPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.MarketStructure)
        If donchLongUpgraded Then state.LongScore += 1
        If donchShortUpgraded Then state.ShortScore += 1

        Dim volMidLongUpgraded  As Boolean = volMidLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim volMidShortUpgraded As Boolean = volMidShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If volMidLongUpgraded Then state.LongScore += 1
        If volMidShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded  As Boolean = obvPartialLong AndAlso r.OBVDivergence <> "BEARISH" AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso r.OBVDivergence <> "BULLISH" AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' -- Pass 2b: OI x CVD Cross-Confirm ----------------------------------
        ' Confirmed: OI full signal and CVD direction + sign agree -> upgrade.
        ' Conflicted: OI full signal and CVD directly oppose -> penalty.
        ' Partial OI signals (COVERING/CAPITULATION) are upgrade-only; no penalty on conflict.
        Dim oiCvdNote As String = ""
        If cfg.Indicators.OiCvd.Enabled Then
            Dim cvdBullish As Boolean = (r.CVDSlope = "RISING" AndAlso r.CVDValue > 0)
            Dim cvdBearish As Boolean = (r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)

            If (oiLong OrElse oiLongUpgraded) AndAlso cvdBullish Then
                state.LongScore = Math.Min(state.LongScore + cfg.Indicators.OiCvd.UpgradeBonus, regimeMax)
                oiCvdNote = String.Format(" | PASS2b: +{0}[L] OI×CVD confirmed", cfg.Indicators.OiCvd.UpgradeBonus)
            ElseIf (oiShort OrElse oiShortUpgraded) AndAlso cvdBearish Then
                state.ShortScore = Math.Min(state.ShortScore + cfg.Indicators.OiCvd.UpgradeBonus, regimeMax)
                oiCvdNote = String.Format(" | PASS2b: +{0}[S] OI×CVD confirmed", cfg.Indicators.OiCvd.UpgradeBonus)
            ElseIf oiLong AndAlso cvdBearish Then
                state.LongScore = Math.Max(0, state.LongScore - cfg.Indicators.OiCvd.ConflictPenalty)
                oiCvdNote = String.Format(" | PASS2b: -{0}[L] OI×CVD conflict", cfg.Indicators.OiCvd.ConflictPenalty)
            ElseIf oiShort AndAlso cvdBullish Then
                state.ShortScore = Math.Max(0, state.ShortScore - cfg.Indicators.OiCvd.ConflictPenalty)
                oiCvdNote = String.Format(" | PASS2b: -{0}[S] OI×CVD conflict", cfg.Indicators.OiCvd.ConflictPenalty)
            End If
        End If

        ' -- Pass 2c: Regime Alignment Gate -----------------------------------
        ' Bonus when all active regime-key signals align with the dominant direction.
        ' Penalty when all active regime-key signals conflict with it.
        ' Suppressed when LongScore = ShortScore (ambiguous direction) or TRANSITIONAL.
        ' TRENDING: EMA ribbon (1m) / ROC threshold-gated / CVD slope+sign.
        ' RANGE_BOUND: VWAP dev (active only outside warmup) / RSI(9) vs 50 / Donchian(20).
        Dim regimeAlignNote     As String  = ""
        Dim regimeAlignLongHit  As Boolean = False
        Dim regimeAlignShortHit As Boolean = False

        If cfg.RegimeWeights.Enabled AndAlso state.LongScore <> state.ShortScore Then
            Dim p2cIsLong    As Boolean = state.LongScore > state.ShortScore
            Dim isTrending   As Boolean = (r.Regime = "TRENDING_UP" OrElse r.Regime = "TRENDING_DOWN")
            Dim isRangeBound As Boolean = (r.Regime = "RANGE_BOUND")

            If isTrending Then
                Dim emaAligned As Boolean = If(p2cIsLong, r.EMAAlignment = "BULL", r.EMAAlignment = "BEAR")
                Dim rocActive  As Boolean = Math.Abs(r.ROC) >= cfg.Indicators.ROC.SlopeSensitivity
                Dim rocAligned As Boolean = rocActive AndAlso If(p2cIsLong, r.ROC > 0, r.ROC < 0)
                Dim cvdAligned As Boolean = If(p2cIsLong,
                                               r.CVDSlope = "RISING"  AndAlso r.CVDValue > 0,
                                               r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)

                Dim activeCount  As Integer = 2 + If(rocActive, 1, 0)
                Dim alignedCount As Integer = (If(emaAligned, 1, 0)) +
                                              (If(rocAligned, 1, 0)) +
                                              (If(cvdAligned, 1, 0))
                Dim sigLabel     As String  = If(rocActive, "EMA+ROC+CVD", "EMA+CVD")
                Dim rocSuffix    As String  = If(rocActive, "", " (ROC neutral)")

                If alignedCount = activeCount Then
                    Dim bonus As Integer = cfg.RegimeWeights.Trending.AlignmentBonus
                    If p2cIsLong Then
                        state.LongScore  = Math.Min(state.LongScore  + bonus, regimeMax)
                        regimeAlignLongHit = True
                    Else
                        state.ShortScore = Math.Min(state.ShortScore + bonus, regimeMax)
                        regimeAlignShortHit = True
                    End If
                    regimeAlignNote = String.Format("+{0} REGIME ALIGN [TRENDING: {1} ✓{2}]", bonus, sigLabel, rocSuffix)
                ElseIf alignedCount = 0 Then
                    Dim penalty As Integer = cfg.RegimeWeights.Trending.ConflictPenalty
                    If p2cIsLong Then
                        state.LongScore  = Math.Max(0, state.LongScore  - penalty)
                    Else
                        state.ShortScore = Math.Max(0, state.ShortScore - penalty)
                    End If
                    regimeAlignNote = String.Format("-{0} REGIME CONFLICT [TRENDING: {1} ✗{2}]", penalty, sigLabel, rocSuffix)
                End If

            ElseIf isRangeBound Then
                Dim vwapActive   As Boolean = Not vwapWarmup
                Dim vwapAligned  As Boolean = vwapActive AndAlso If(p2cIsLong, r.CurrentPrice > r.VWAP, r.CurrentPrice < r.VWAP)
                Dim rsiAligned   As Boolean = If(p2cIsLong, r.RSI > 50, r.RSI < 50)
                Dim donchAligned As Boolean = If(p2cIsLong,
                                                 r.DonchianSignal = "LONG"  OrElse r.DonchianSignal = "LONG_PARTIAL",
                                                 r.DonchianSignal = "SHORT" OrElse r.DonchianSignal = "SHORT_PARTIAL")

                Dim activeCount  As Integer = 2 + If(vwapActive, 1, 0)
                Dim alignedCount As Integer = (If(vwapAligned, 1, 0)) +
                                              (If(rsiAligned, 1, 0)) +
                                              (If(donchAligned, 1, 0))
                Dim sigLabel     As String  = If(vwapActive, "VWAP+RSI+DON", "RSI+DON")
                Dim vwapSuffix   As String  = If(vwapActive, "", " (VWAP warmup)")

                If alignedCount = activeCount Then
                    Dim bonus As Integer = cfg.RegimeWeights.RangeBound.AlignmentBonus
                    If p2cIsLong Then
                        state.LongScore  = Math.Min(state.LongScore  + bonus, regimeMax)
                        regimeAlignLongHit = True
                    Else
                        state.ShortScore = Math.Min(state.ShortScore + bonus, regimeMax)
                        regimeAlignShortHit = True
                    End If
                    regimeAlignNote = String.Format("+{0} REGIME ALIGN [RANGE: {1} ✓{2}]", bonus, sigLabel, vwapSuffix)
                ElseIf alignedCount = 0 Then
                    Dim penalty As Integer = cfg.RegimeWeights.RangeBound.ConflictPenalty
                    If p2cIsLong Then
                        state.LongScore  = Math.Max(0, state.LongScore  - penalty)
                    Else
                        state.ShortScore = Math.Max(0, state.ShortScore - penalty)
                    End If
                    regimeAlignNote = String.Format("-{0} REGIME CONFLICT [RANGE: {1} ✗{2}]", penalty, sigLabel, vwapSuffix)
                End If
            End If
        End If

        ' Snapshot ls/ss after Pass 2c, before funding modifiers
        ls = state.LongScore
        ss = state.ShortScore

        ' -- Step 3: Funding Rate Confidence Modifier -------------------------
        Dim fr As Double = r.FundingRate

        Dim fundingBaseNote As String = ""
        If fr > cfg.Scoring.FundingHighPositive Then
            ls -= cfg.Scoring.FundingHighPenalty : ss += cfg.Scoring.FundingHighBoost
            fundingBaseNote = String.Format("STEP3: -{0}[L] +{1}[S]", cfg.Scoring.FundingHighPenalty, cfg.Scoring.FundingHighBoost)
        ElseIf fr > cfg.Scoring.FundingLowPositive Then
            ls -= cfg.Scoring.FundingLowPenalty
            fundingBaseNote = String.Format("STEP3: -{0}[L]", cfg.Scoring.FundingLowPenalty)
        ElseIf fr < cfg.Scoring.FundingHighNegative Then
            ss -= cfg.Scoring.FundingHighPenalty : ls += cfg.Scoring.FundingHighBoost
            fundingBaseNote = String.Format("STEP3: -{0}[S] +{1}[L]", cfg.Scoring.FundingHighPenalty, cfg.Scoring.FundingHighBoost)
        ElseIf fr < cfg.Scoring.FundingLowNegative Then
            ss -= cfg.Scoring.FundingLowPenalty
            fundingBaseNote = String.Format("STEP3: -{0}[S]", cfg.Scoring.FundingLowPenalty)
        Else
            fundingBaseNote = "STEP3: none"
        End If
        ls = Math.Max(0, ls)
        ss = Math.Max(0, ss)

        ' -- Step 3b: Funding Momentum Modifier -------------------------------
        Dim fundingStep3bNote As String = "STEP3b: disabled"
        If cfg.Indicators.Funding.MomentumEnabled Then
            fundingStep3bNote = "STEP3b: none"
            If r.FundingBias = "LONGS HEAVILY CROWDED" OrElse r.FundingBias = "LONGS CROWDED" Then
                If r.FundingMomentum = "RISING" Then
                    Dim extraPen As Integer = Math.Min(cfg.Indicators.Funding.MomentumAmplify, cfg.Scoring.FundingHighPenalty)
                    ls = Math.Max(0, ls - extraPen)
                    fundingStep3bNote = String.Format("STEP3b: -{0}[L] crowding↑", extraPen)
                ElseIf r.FundingMomentum = "FALLING" Then
                    ls = Math.Min(ls + cfg.Indicators.Funding.MomentumSoften, regimeMax)
                    fundingStep3bNote = String.Format("STEP3b: +{0}[L] de-crowding", cfg.Indicators.Funding.MomentumSoften)
                End If
            ElseIf r.FundingBias = "SHORTS HEAVILY CROWDED" OrElse r.FundingBias = "SHORTS CROWDED" Then
                If r.FundingMomentum = "FALLING" Then
                    Dim extraPen As Integer = Math.Min(cfg.Indicators.Funding.MomentumAmplify, cfg.Scoring.FundingHighPenalty)
                    ss = Math.Max(0, ss - extraPen)
                    fundingStep3bNote = String.Format("STEP3b: -{0}[S] crowding↓", extraPen)
                ElseIf r.FundingMomentum = "RISING" Then
                    ss = Math.Min(ss + cfg.Indicators.Funding.MomentumSoften, regimeMax)
                    fundingStep3bNote = String.Format("STEP3b: +{0}[S] de-crowding", cfg.Indicators.Funding.MomentumSoften)
                End If
            ElseIf r.FundingBias = "NEUTRAL" Then
                Dim extraPen As Integer = Math.Min(cfg.Indicators.Funding.MomentumAmplify, cfg.Scoring.FundingHighPenalty)
                If r.FundingMomentum = "RISING" AndAlso fr > 0 Then
                    ls = Math.Max(0, ls - extraPen)
                    fundingStep3bNote = String.Format("STEP3b: -{0}[L] neutral→crowding", extraPen)
                ElseIf r.FundingMomentum = "FALLING" AndAlso fr < 0 Then
                    ss = Math.Max(0, ss - extraPen)
                    fundingStep3bNote = String.Format("STEP3b: -{0}[S] neutral→crowding", extraPen)
                End If
            End If
        End If
        ls = Math.Max(0, ls)
        ss = Math.Max(0, ss)

        ' -- Breakdown notes --------------------------------------------------
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC")

        breakdown.Add(New SignalBreakdownItem("ROC(9)", rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
            BuildNote(String.Format("{0:F3} | Slope: {1}", r.ROC, r.ROCSlope),
                      rocPartialLong AndAlso Not rocLongUpgraded,
                      rocPartialShort AndAlso Not rocShortUpgraded,
                      rocLongUpgraded, rocShortUpgraded)))

        Dim rsiNote As String = String.Format("{0:F1} | zones OB:{1} OS:{2}", r.RSI, rsiOB, rsiOS)
        If r.RSIDivergence <> "NONE" Then rsiNote &= String.Format(" | DIV:{0}", r.RSIDivergence)
        If rsiDivPenaltyLong Then rsiNote &= String.Format(" | PENALTY -1 [L] (RSI>{0})", rsiDivPenaltyHigh)
        If rsiDivPenaltyShort Then rsiNote &= String.Format(" | PENALTY -1 [S] (RSI<{0})", rsiDivPenaltyLow)
        breakdown.Add(New SignalBreakdownItem("RSI(9)", rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
            BuildNote(rsiNote,
                      rsiPartialLong AndAlso Not rsiLongUpgraded,
                      rsiPartialShort AndAlso Not rsiShortUpgraded,
                      rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("DMI +/-DI", dmiLong, dmiShort,
            String.Format("+DI:{0:F1} -DI:{1:F1}", r.PlusDI, r.MinusDI)))

        breakdown.Add(New SignalBreakdownItem("ADX>" & adxTrend.ToString("F0"), adxLong, adxShort,
            String.Format("{0:F1} | thr:{1:F0}", r.ADX, adxTrend)))

        Dim volNote As String = String.Format("{0:F2}x | thr H:{1:F2}x M:{2:F2}x [{3}]", r.VolumeRatio, volHigh, volMid, normMode)
        breakdown.Add(New SignalBreakdownItem("Volume", volLong OrElse volMidLongUpgraded, volShort OrElse volMidShortUpgraded,
            BuildNote(volNote,
                      volMidLong AndAlso Not volMidLongUpgraded,
                      volMidShort AndAlso Not volMidShortUpgraded,
                      volMidLongUpgraded, volMidShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("VWAP", vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
            If(vwapWarmup, vwapNote,
               BuildNote(vwapNote,
                         vwapPartialLong AndAlso Not vwapLongUpgraded,
                         vwapPartialShort AndAlso Not vwapShortUpgraded,
                         vwapLongUpgraded, vwapShortUpgraded))))

        breakdown.Add(New SignalBreakdownItem("BBW/TTM", bbwLongHit, bbwShortHit, bbwNote))

        breakdown.Add(New SignalBreakdownItem("EMA 9/21/50", emaBull, emaBear,
            String.Format("9:{0:F0} 21:{1:F0} 50:{2:F0} | {3}", r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment)))

        Dim fundingInfoNote As String = String.Format("{0:F4}% | {1} | MOM:{2} | {3} | {4}",
                                                      r.FundingRate * 100, r.FundingBias, r.FundingMomentum,
                                                      fundingBaseNote, fundingStep3bNote)
        breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False, fundingInfoNote))

        ' OI Delta note includes Pass 2b result appended as oiCvdNote
        breakdown.Add(New SignalBreakdownItem("OI Delta", oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
            BuildNote(String.Format("15m:{0:F2}% 60m:{1:F2}% | {2}", r.OIChange15m, r.OIChange60m, r.OISignal),
                      oiPartialLong AndAlso Not oiLongUpgraded,
                      oiPartialShort AndAlso Not oiShortUpgraded,
                      oiLongUpgraded, oiShortUpgraded) & oiCvdNote))

        breakdown.Add(New SignalBreakdownItem("OFI", ofiBuy, ofiSell,
            String.Format("Ratio:{0:F2} | {1}", r.OFIRatio, r.OFISignal)))

        Dim cvdNote As String = String.Format("Net:{0:F0} | Slope:{1} | Div:{2}", r.CVDValue, r.CVDSlope, r.CVDDivergence)
        If r.CVDDivergence <> "NONE" Then cvdNote &= String.Format(" | PENALTY -{0}", cfg.Indicators.CVD.DivergencePenalty)
        breakdown.Add(New SignalBreakdownItem("CVD", cvdLong, cvdShort, cvdNote))

        breakdown.Add(New SignalBreakdownItem("TFI", tfiLong, tfiShort,
            String.Format("{0:F3} | {1}", r.TFIValue, r.TFISignal)))

        Dim microNote As String = String.Format("E:{0:F0} M:{1:F0} L:{2:F0} | {3} | {4}",
                                                r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                                r.MicroCVDMomentum, r.MicroCVDSignal)
        If r.MicroCVDSignal = "BULL_DECEL" OrElse r.MicroCVDSignal = "BEAR_DECEL" Then
            microNote &= String.Format(" | PENALTY -{0} opposing", cfg.Indicators.MicroCVD.DecelPenalty)
        End If
        If microFlatStallLong Then microNote &= String.Format(" | STALL PENALTY -{0} [L] (price>VWAP, CVD<=0)", cfg.Indicators.MicroCVD.DecelPenalty)
        If microFlatStallShort Then microNote &= String.Format(" | STALL PENALTY -{0} [S] (price<VWAP, CVD>=0)", cfg.Indicators.MicroCVD.DecelPenalty)
        breakdown.Add(New SignalBreakdownItem("MicroCVD", microLong, microShort, microNote))

        Dim liqNote As String = String.Format("L:{0:F0} S:{1:F0} | {2}", r.LiqLongSize, r.LiqShortSize, r.LiqSignal)
        If liqLongPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [L]", liqLongPenalty)
        If liqShortPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [S]", liqShortPenalty)
        breakdown.Add(New SignalBreakdownItem("Liq Penalty", liqLongPenalty > 0, liqShortPenalty > 0, liqNote))

        Dim spreadNote As String = String.Format("{0:F2} bps | {1}", r.SpreadBps, r.SpreadStatus)
        If spreadPenaltyLong  > 0 Then spreadNote &= String.Format(" | PENALTY -{0} [L]", spreadPenaltyLong)
        If spreadPenaltyShort > 0 Then spreadNote &= String.Format(" | PENALTY -{0} [S]", spreadPenaltyShort)
        breakdown.Add(New SignalBreakdownItem("Spread", spreadPenaltyLong > 0, spreadPenaltyShort > 0, spreadNote))

        breakdown.Add(New SignalBreakdownItem("5m EMA(200)", ema200Bull, ema200Bear,
            String.Format("{0:F0} | {1}", r.EMA200_5m, r.PriceVsEMA200)))

        Dim donchNote As String
        If r.DonchianSignal = "NONE" Then
            donchNote = String.Format("U:{0:F0} L:{1:F0} | MID-CHANNEL -- no signal", r.DonchianUpper, r.DonchianLower)
        Else
            donchNote = BuildNote(String.Format("U:{0:F0} L:{1:F0} | {2}", r.DonchianUpper, r.DonchianLower, r.DonchianSignal),
                                  donchPartialLong AndAlso Not donchLongUpgraded,
                                  donchPartialShort AndAlso Not donchShortUpgraded,
                                  donchLongUpgraded, donchShortUpgraded)
        End If
        breakdown.Add(New SignalBreakdownItem("Donchian(20)", donchLong OrElse donchLongUpgraded, donchShort OrElse donchShortUpgraded, donchNote))

        breakdown.Add(New SignalBreakdownItem("OBV", obvLong OrElse obvLongUpgraded, obvShort OrElse obvShortUpgraded,
            BuildNote(String.Format("Trend:{0} Div:{1}{2}",
                                   r.OBVTrend, r.OBVDivergence,
                                   If(obvPartialLong AndAlso Not obvLongUpgraded AndAlso r.OBVDivergence = "BEARISH", " [upgrade blocked]", "")),
                      obvPartialLong AndAlso Not obvLongUpgraded AndAlso r.OBVDivergence <> "BEARISH",
                      obvPartialShort AndAlso Not obvShortUpgraded AndAlso r.OBVDivergence <> "BULLISH",
                      obvLongUpgraded, obvShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("VPFR-lite", vpfrLong, vpfrShort,
            String.Format("POC:{0:F0} | {1} | HVN@POC:{2}", r.VPFRPoc, r.VPFRSignal, If(r.VPFRHVNearPoc, "YES", "NO"))))

        If regimeAlignNote <> "" Then
            breakdown.Add(New SignalBreakdownItem("Regime Align (2c)", regimeAlignLongHit, regimeAlignShortHit, regimeAlignNote))
        End If

    End Sub

End Class
