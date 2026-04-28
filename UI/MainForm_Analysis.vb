' UI/MainForm_Analysis.vb
' Partial class: Analyze button handler and RunAnalysisAsync pipeline.
'
' v0.52 [P15]: OI signal change threshold now reads cfg.Indicators.OI.ChangeThresholdPct.
' v0.52 [P16]: Donchian period now reads cfg.Indicators.Donchian.Period.
' v0.52 [P17]: BBW period and StdDev now read from cfg.
' v0.52 [P18]: FundingBias label thresholds now read from cfg.Scoring funding fields.
' fix [B1]: Regime classification reads cfg.Indicators.ADX.TrendThreshold / RangeThreshold.
' fix [B2]: CalcDMI period, EMA ribbon, ROC slope sensitivity, Volume SMA all from cfg.
' fix [B3]: CalcCVD passes all three tunable params from cfg.
' fix: Removed stale 6th arg from CalcBBW call.
' fix [T1-B]: Regime ADX hysteresis -- 1-bar grace period before RANGING flip.
' fix [T3-A]: CalcVPFRLite numBuckets from cfg.
' fix [T3-B]: CalcRSIDivergence pivotWing and lookbackBars from cfg.
' fix [T3-C]: CalcTTMSqueeze flatThreshold from cfg.
' fix [T3-D]: CalcLiquidations dominanceRatio from cfg.
' funding-momentum: Append fundingRate to _fundingHistory ring buffer (max FundingHistoryMax).
'   Call CalcFundingMomentum() after FundingBias is set; result stored in r.FundingMomentum.
'   Cold start (< 2 samples) returns FLAT -- accepted warm-up behaviour.
' session-volume-norms: No call-site changes required. DynamicNorms.Compute() internally
'   applies per-session HighMultiplier/MidMultiplier via ApplySessionVolume() after dynamic
'   vol thresholds are set. Controlled by cfg.SessionVolume (EngineSettings / settings.json v12).

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    Private Async Sub btnAnalyze_Click(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        btnAnalyze.Enabled = False
        btnAnalyze.Text    = "Fetching..."
        txtOutput.Clear()
        AppendRtf(txtOutput, "Fetching data from Deribit..." & Environment.NewLine, C_LABEL)
        lblVerdict.Text      = "..."
        lblVerdict.BackColor = Color.Gray

        Try
            Await RunAnalysisAsync()
        Catch ex As Exception
            txtOutput.Clear()
            AppendRtf(txtOutput, "ERROR: " & ex.Message & Environment.NewLine & ex.StackTrace, C_BAD)
            lblVerdict.Text      = "ERROR"
            lblVerdict.BackColor = Color.OrangeRed
        Finally
            btnAnalyze.Enabled = True
            btnAnalyze.Text    = "Analyze Now"
        End Try
    End Sub

    Private Async Function RunAnalysisAsync() As Task
        Dim cfg As EngineSettings = SettingsLoader.Current

        Dim mtfStale As Boolean = _mtfCandles15m Is Nothing OrElse
                                   (DateTime.UtcNow - _mtfLastFetchTime).TotalSeconds >= MTF_TTL_SECONDS

        Dim t_1m      = DeribitClient.GetCandlesAsync("1", 250)
        Dim t_5m      = DeribitClient.GetCandlesAsync("5", 210)
        Dim t_funding = DeribitClient.GetFundingRateAsync()
        Dim t_book    = DeribitClient.GetBookSummaryAsync()
        Dim t_ob      = DeribitClient.GetOrderBookAsync(10)
        Dim t_trades  = DeribitClient.GetRecentTradesAsync(100)

        Dim t_15m As Task(Of List(Of Candle)) = Nothing
        If mtfStale Then
            t_15m = DeribitClient.GetCandlesAsync("15", 70)
            Await Task.WhenAll(t_1m, t_5m, t_15m, t_funding, t_book, t_ob, t_trades)
            _mtfCandles15m    = Await t_15m
            _mtfLastFetchTime = DateTime.UtcNow
        Else
            Await Task.WhenAll(t_1m, t_5m, t_funding, t_book, t_ob, t_trades)
        End If

        Dim candles1m    = Await t_1m
        Dim candles5m    = Await t_5m
        Dim candles15m   = _mtfCandles15m
        Dim fundingRate  = Await t_funding
        Dim bookSummary  = Await t_book
        Dim orderBook    = Await t_ob
        Dim recentTrades = Await t_trades

        If candles1m.Count < 50 Then
            txtOutput.Clear()
            AppendRtf(txtOutput, "Insufficient 1m candle data returned. Please retry." & Environment.NewLine, C_WARN)
            Return
        End If

        Dim lastTradePrice As Double = If(recentTrades IsNot Nothing AndAlso recentTrades.Count > 0,
                                          recentTrades(0).Price, 0)

        Dim r As New IndicatorResults()
        r.CurrentPrice = candles1m.Last().Close

        r.ATR = IndicatorEngine.CalcATR(candles1m, cfg.Indicators.ATR.Period)

        Dim norms As DynamicNorms = DynamicNorms.Compute(candles1m, r.ATR)
        r.ATRSizeMultiplier = Math.Round(norms.ATRScaleFactor, 2)

        Dim rocSeries = IndicatorEngine.CalcROCSeries(candles1m,
                            cfg.Indicators.ROC.Period,
                            cfg.Indicators.ROC.SeriesLookback)
        r.ROC = If(rocSeries.Count > 0, rocSeries.Last(), 0)
        If rocSeries.Count >= 2 Then
            Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
            Dim slopeSens As Double = cfg.Indicators.ROC.SlopeSensitivity
            r.ROCSlope = If(delta > slopeSens, "RISING", If(delta < -slopeSens, "FALLING", "FLAT"))
        Else
            r.ROCSlope = "FLAT"
        End If

        r.RSI = IndicatorEngine.CalcRSI(candles1m, cfg.Indicators.RSI.Period)

        r.VolumeSMA9       = IndicatorEngine.CalcVolumeSMA(candles1m, cfg.Indicators.Volume.SmaPeriod)
        r.CurrentVolume    = candles1m.Last().Volume
        r.CurrentVolumeUSD = candles1m.Last().VolumeUSD
        r.VolumeRatio      = If(r.VolumeSMA9 > 0, r.CurrentVolume / r.VolumeSMA9, 1)

        IndicatorEngine.CalcDMI(candles5m, cfg.Indicators.DMI.Period, r.PlusDI, r.MinusDI, r.ADX)

        Dim adxTrendThr As Double = cfg.Indicators.ADX.TrendThreshold
        Dim adxRangeThr As Double = cfg.Indicators.ADX.RangeThreshold
        Dim rawRegime As String
        If r.ADX > adxTrendThr AndAlso r.PlusDI > r.MinusDI Then
            rawRegime = "TRENDING_UP"
        ElseIf r.ADX > adxTrendThr AndAlso r.MinusDI > r.PlusDI Then
            rawRegime = "TRENDING_DOWN"
        ElseIf r.ADX < adxRangeThr Then
            rawRegime = "RANGE_BOUND"
        Else
            rawRegime = "TRANSITIONAL"
        End If

        Dim prevWasTrending As Boolean = (_prevRegime = "TRENDING_UP" OrElse
                                          _prevRegime = "TRENDING_DOWN" OrElse
                                          _prevRegime = "TRANSITIONAL")
        If rawRegime = "RANGE_BOUND" AndAlso prevWasTrending Then
            r.Regime = _prevRegime
        Else
            r.Regime = rawRegime
        End If
        _prevRegime = rawRegime

        Dim vwapS2Hour   As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim vwapS2Minute As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim vwapWarmup   As Integer = cfg.Indicators.VWAP.WarmupCandles

        r.VWAP       = IndicatorEngine.CalcVWAP(candles1m, r.VWAPSessionCandles, vwapS2Hour, vwapS2Minute)
        r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)
        IndicatorEngine.CalcVWAPBands(candles1m, r.VWAP,
                                      r.VWAPSigma1Upper, r.VWAPSigma1Lower,
                                      r.VWAPSigma2Upper, r.VWAPSigma2Lower,
                                      vwapS2Hour, vwapS2Minute)

        IndicatorEngine.CalcBBW(candles1m, cfg.Indicators.BBW.Period, cfg.Indicators.BBW.StdDev,
                                r.BBW, r.SqueezeStatus)

        IndicatorEngine.CalcTTMSqueeze(candles1m, r.TTMHistogram, r.TTMDirection, r.TTMSignal,
                                       flatThreshold:=cfg.Indicators.TTM.FlatThreshold)

        Dim emaFast As Integer = cfg.Indicators.EMA.Fast
        Dim emaMid  As Integer = cfg.Indicators.EMA.Mid
        Dim emaSlow As Integer = cfg.Indicators.EMA.Slow
        r.EMA9  = IndicatorEngine.CalcEMA(candles1m, emaFast)
        r.EMA21 = IndicatorEngine.CalcEMA(candles1m, emaMid)
        r.EMA50 = IndicatorEngine.CalcEMA(candles1m, emaSlow)
        If r.EMA9 > r.EMA21 AndAlso r.EMA21 > r.EMA50 Then
            r.EMAAlignment = "BULL"
        ElseIf r.EMA9 < r.EMA21 AndAlso r.EMA21 < r.EMA50 Then
            r.EMAAlignment = "BEAR"
        Else
            r.EMAAlignment = "MIXED"
        End If

        ' FundingBias label from cfg thresholds
        r.FundingRate = fundingRate
        If fundingRate > cfg.Scoring.FundingHighPositive Then
            r.FundingBias = "LONGS HEAVILY CROWDED"
        ElseIf fundingRate > cfg.Scoring.FundingLowPositive Then
            r.FundingBias = "LONGS CROWDED"
        ElseIf fundingRate < cfg.Scoring.FundingHighNegative Then
            r.FundingBias = "SHORTS HEAVILY CROWDED"
        ElseIf fundingRate < cfg.Scoring.FundingLowNegative Then
            r.FundingBias = "SHORTS CROWDED"
        Else
            r.FundingBias = "NEUTRAL"
        End If

        ' funding-momentum: maintain ring buffer then compute momentum signal.
        ' Max FundingHistoryMax samples retained; cold start (< 2) yields FLAT.
        ' [S9] Dedup: Deribit publishes funding ~every 8h; appending every 1m run
        ' fills the ring with identical values and forces FLAT. Only append when
        ' the rate actually changed from the previous sample.
        If _fundingHistory.Count = 0 OrElse _fundingHistory(_fundingHistory.Count - 1) <> fundingRate Then
            _fundingHistory.Add(fundingRate)
            If _fundingHistory.Count > FundingHistoryMax Then
                _fundingHistory.RemoveAt(0)
            End If
        End If
        r.FundingMomentum = IndicatorEngine.CalcFundingMomentum(_fundingHistory, cfg)

        Dim nowTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        r.OI_Current = bookSummary.OI
        _oiHistory.Add(New OiSnapshot(nowTs, bookSummary.OI))
        _oiHistory = _oiHistory.Where(Function(x) nowTs - x.Ts < 70 * 60 * 1000L).ToList()

        Dim oi15m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 15 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()
        Dim oi60m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 61 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()

        r.OIChange15m = If(oi15m IsNot Nothing AndAlso oi15m.OI > 0, (r.OI_Current - oi15m.OI) / oi15m.OI * 100, 0)
        r.OIChange60m = If(oi60m IsNot Nothing AndAlso oi60m.OI > 0, (r.OI_Current - oi60m.OI) / oi60m.OI * 100, 0)

        Dim oiThr As Double = cfg.Indicators.OI.ChangeThresholdPct * 100
        Dim priceUp As Boolean = r.CurrentPrice > bookSummary.MarkPrice * 0.9999
        If r.OIChange15m > oiThr AndAlso priceUp Then
            r.OISignal = "NEW LONGS"
        ElseIf r.OIChange15m > oiThr AndAlso Not priceUp Then
            r.OISignal = "NEW SHORTS"
        ElseIf r.OIChange15m < -oiThr AndAlso priceUp Then
            r.OISignal = "COVERING"
        ElseIf r.OIChange15m < -oiThr AndAlso Not priceUp Then
            r.OISignal = "CAPITULATION"
        Else
            r.OISignal = "NEUTRAL"
        End If

        IndicatorEngine.CalcOFI(orderBook, r.OFIRatio, r.OFISignal, r.OFIBidVol, r.OFIAskVol,
                                buyDominantRatio:=cfg.Indicators.OFI.BuyDominantRatio,
                                sellDominantRatio:=cfg.Indicators.OFI.SellDominantRatio,
                                bookDepth:=cfg.Indicators.OFI.BookDepth)

        ' OFI momentum: append ratio to ring buffer, then derive momentum signal.
        _ofiHistory.Add(r.OFIRatio)
        If _ofiHistory.Count > OFIHistoryMax Then _ofiHistory.RemoveAt(0)
        r.OFIMomentum = IndicatorEngine.CalcOFIMomentum(_ofiHistory, cfg)

        IndicatorEngine.CalcSpread(orderBook, r.SpreadBps, r.SpreadStatus,
                                   wideThresholdBps:=cfg.Indicators.Spread.WideThresholdBps,
                                   tightThresholdBps:=cfg.Indicators.Spread.TightThresholdBps)

        IndicatorEngine.CalcLiquidations(recentTrades, r.LiqLongSize, r.LiqShortSize, r.LiqSignal,
                                         dominanceRatio:=cfg.Indicators.Liquidations.DominanceRatio)

        IndicatorEngine.CalcCVD(recentTrades, candles1m, r.CVDValue, r.CVDSlope, r.CVDDivergence,
                                slopeMinUsd:=cfg.Indicators.CVD.SlopeMinUsd,
                                slopePctOfValue:=cfg.Indicators.CVD.SlopePctOfValue,
                                divergencePriceGate:=cfg.Indicators.CVD.DivergencePriceGate)

        IndicatorEngine.CalcTFI(recentTrades, r.TFIValue, r.TFISignal,
                                tfiWindowSize:=cfg.Indicators.TFI.WindowSize,
                                threshold:=cfg.Indicators.TFI.Threshold)
        IndicatorEngine.CalcMicroCVD(recentTrades,
                                     r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                     r.MicroCVDMomentum, r.MicroCVDSignal,
                                     microWindowSize:=cfg.Indicators.MicroCVD.WindowSize,
                                     accelThreshold:=cfg.Indicators.MicroCVD.AccelThreshold,
                                     dynamicPct:=cfg.Indicators.MicroCVD.AccelThresholdDynamicPct,
                                     floorPct:=cfg.Indicators.MicroCVD.AccelThresholdFloorPct)

        Dim mtfProposed As String = "NONE"
        If candles15m IsNot Nothing AndAlso candles15m.Count >= cfg.MTFGate.DmiPeriod + 2 Then
            If r.Regime = "TRENDING_UP" OrElse r.EMAAlignment = "BULL" Then
                mtfProposed = "LONG"
            ElseIf r.Regime = "TRENDING_DOWN" OrElse r.EMAAlignment = "BEAR" Then
                mtfProposed = "SHORT"
            End If
        End If

        IndicatorEngine.CalcMTFGate(
            candles15m,
            r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment,
            r.MTFGatePass, r.MTFGateReason,
            proposedDirection:=mtfProposed,
            adxPeriod:=cfg.MTFGate.DmiPeriod,
            adxMin:=cfg.MTFGate.AdxMin,
            minOf:=cfg.MTFGate.RequiredConfirms,
            candleLookback:=cfg.MTFGate.CandleCount)

        r.EMA200_5m     = IndicatorEngine.CalcEMA(candles5m, 200)
        r.PriceVsEMA200 = If(r.EMA200_5m > 0,
                              If(r.CurrentPrice > r.EMA200_5m, "ABOVE", "BELOW"),
                              "N/A")

        IndicatorEngine.CalcDonchian(candles1m, cfg.Indicators.Donchian.Period,
                                     r.DonchianUpper, r.DonchianLower)

        Dim channelRange As Double = r.DonchianUpper - r.DonchianLower
        If channelRange > 0 Then
            Dim q1 As Double = r.DonchianLower + channelRange * 0.25
            Dim q3 As Double = r.DonchianUpper - channelRange * 0.25
            If r.CurrentPrice >= r.DonchianUpper Then
                r.DonchianSignal = "LONG"
            ElseIf r.CurrentPrice <= r.DonchianLower Then
                r.DonchianSignal = "SHORT"
            ElseIf r.CurrentPrice >= q3 Then
                r.DonchianSignal = "LONG_PARTIAL"
            ElseIf r.CurrentPrice <= q1 Then
                r.DonchianSignal = "SHORT_PARTIAL"
            Else
                r.DonchianSignal = "NONE"
            End If
        Else
            r.DonchianSignal = "NONE"
        End If

        IndicatorEngine.CalcOBV(candles1m, r.OBVTrend, r.OBVDivergence,
                                cfg.Indicators.OBV.TrendGate,
                                cfg.Indicators.OBV.DivergenceGate)

        r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(candles1m,
                              cfg.Indicators.RSI.Period,
                              cfg.Indicators.RSI.DivergencePriceGate,
                              cfg.Indicators.RSI.DivergenceRsiDelta,
                              pivotWing:=cfg.Indicators.RSI.PivotWing,
                              lookbackBars:=cfg.Indicators.RSI.LookbackBars)

        Dim vpfrPoc       As Double  = 0
        Dim vpfrHVNearPoc As Boolean = False
        Dim vpfrSignal    As String  = "NEUTRAL"
        IndicatorEngine.CalcVPFRLite(candles1m, r.CurrentPrice,
                                     vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                                     numBuckets:=cfg.Indicators.VPFR.NumBuckets)
        r.VPFRPoc       = vpfrPoc
        r.VPFRHVNearPoc = vpfrHVNearPoc
        r.VPFRSignal    = vpfrSignal

        Dim posState As PositionState = PositionState.None
        If rbLong.Checked  Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort

        Dim verdict = ScoringEngine.Calculate(r, posState, norms, cfg)

        AnalysisLogger.LogRun(r, verdict)
        UpdateLogInfo()

        RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)
    End Function

End Class
