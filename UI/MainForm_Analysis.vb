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

Imports System.Windows.Forms

Partial Public Class MainForm

    Private Async Sub btnAnalyze_Click(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        btnAnalyze.Enabled = False
        btnAnalyze.Text    = "Fetching..."

        Try
            Await RunAnalysisAsync()
        Catch ex As Exception
            ' P5b — errors surface via MessageBox now that the legacy txtOutput
            ' writer is gone. Briefly blocks the auto-run timer; acceptable per
            ' the P5b kickoff §3.2.1 trade-off (errors are rare, trader wants
            ' to see them). Swap to a transient header Pill if disruptive.
            MessageBox.Show(
                "Analysis failed:" & Environment.NewLine & Environment.NewLine &
                ex.Message,
                "Analysis Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
        Finally
            btnAnalyze.Enabled = True
            btnAnalyze.Text    = "Analyze Now"
        End Try
    End Sub

    Private Async Function RunAnalysisAsync() As Task
        Dim cfg As EngineSettings = SettingsLoader.Current

        ' [v36] Resolve the active session's execution resolution (1/3/5 min) from the
        ' UTC hour. ASIA/LONDON → 3-min, NY → 1-min (config-driven). The execution stack
        ' (incl. ATR) is computed on this resolution below; regime (5m) / MTF (15m) /
        ' swing pivots (5m/15m) are unchanged. At res=1 the run is byte-identical to v35.
        Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)

        Dim mtfStale As Boolean = _mtfCandles15m Is Nothing OrElse
                                   (DateTime.UtcNow - _mtfLastFetchTime).TotalSeconds >= MTF_TTL_SECONDS

        Dim t_1m      = DeribitClient.GetCandlesAsync("1", 250)
        Dim t_5m      = DeribitClient.GetCandlesAsync("5", 210)
        Dim t_funding = DeribitClient.GetFundingRateAsync()
        Dim t_book    = DeribitClient.GetBookSummaryAsync()
        Dim t_ob      = DeribitClient.GetOrderBookAsync(10)
        Dim t_trades  = DeribitClient.GetRecentTradesAsync(500)

        Dim t_15m As Task(Of List(Of Candle)) = Nothing
        If mtfStale Then
            t_15m = DeribitClient.GetCandlesAsync("15", 70)
            Await Task.WhenAll(t_1m, t_5m, t_15m, t_funding, t_book, t_ob, t_trades)
            Dim freshM15 = Await t_15m
            If freshM15 IsNot Nothing AndAlso freshM15.Count > 0 Then
                _mtfCandles15m    = freshM15
                _mtfLastFetchTime = DateTime.UtcNow
            End If
            ' If freshM15 is Nothing, leave the cache as-is. Stale data is better than no data
            ' for the MTF gate; 15m candles change slowly. Cache retry happens next cycle.
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

        ' [v36] Execution-stack candles at the session resolution. Keep the 1m fetch in
        ' ALL sessions (it feeds the 1m OHLC cache, the eval barrier walk, and last-trade
        ' price). When execRes = 1 (NY), candlesExec IS candles1m — no extra call, byte-
        ' identical. Otherwise fetch the 3/5-min stack (250 bars = 12.5h at 3m; ample for
        ' the 100-bar ATR-ref / volume baseline).
        Dim candlesExec As List(Of Candle) =
            If(execRes = 1, candles1m, Await DeribitClient.GetCandlesAsync(execRes.ToString(), 250))

        ' Resilience check: if any required fetch failed, skip cleanly.
        Dim skipReason As String = Nothing
        If candles1m Is Nothing OrElse candles1m.Count < 50 Then
            skipReason = "1m candles unavailable"
        ElseIf candles5m Is Nothing OrElse candles5m.Count < 30 Then
            skipReason = "5m candles unavailable"
        ElseIf execRes <> 1 AndAlso (candlesExec Is Nothing OrElse candlesExec.Count < 50) Then
            ' [v36] Exec-stack fetch failed for a 3/5-min session (1m fetch above is
            ' separate — it feeds the cache/eval walk regardless).
            skipReason = execRes & "m candles unavailable"
        ElseIf Not fundingRate.HasValue Then
            skipReason = "funding rate unavailable"
        ElseIf Not bookSummary.HasValue Then
            skipReason = "book summary unavailable"
        ElseIf orderBook Is Nothing Then
            skipReason = "order book unavailable"
        ElseIf recentTrades Is Nothing OrElse recentTrades.Count = 0 Then
            skipReason = "recent trades unavailable"
        ElseIf Not IndicatorEngine.IsFresh(candlesExec, execRes, DateTime.UtcNow) Then
            ' D5 (S-6): stale tape scores hours-old data as current → row pollution.
            ' [v36] Freshness honours the execution resolution — a 3-min bar is fresh up
            ' to ~6 min, not 2. At execRes=1 candlesExec IS candles1m, so this is the
            ' original 1m gate exactly. Gate exec-stack + 5m only; the 15m cache
            ' deliberately tolerates staleness.
            skipReason = execRes & "m candles stale"
        ElseIf Not IndicatorEngine.IsFresh(candles5m, 5, DateTime.UtcNow) Then
            skipReason = "5m candles stale"
        End If

        If skipReason IsNot Nothing Then
            _skipCount += 1
            _lastSkipReason = skipReason
            ' Skip state surfaces in the SKIPPED verdict panel (P4f) and the
            ' LOG sub-box skip counter (P4e); the legacy txtOutput write was
            ' deleted in P5b.
            UpdateLogInfo()
            RenderSkippedDashboard(skipReason)
            RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)
            Return
        End If

        ' recentTrades is chronological ascending — the last element is the most
        ' recent trade (see GetRecentTradesAsync contract).
        Dim lastTradePrice As Double = If(recentTrades IsNot Nothing AndAlso recentTrades.Count > 0,
                                          recentTrades(recentTrades.Count - 1).Price, 0)

        Dim r As New IndicatorResults()
        ' [v36] Stamp the resolution BEFORE scoring so the ROC magnitude override
        ' resolves via r.ExecResolution at its scoring read sites (no new Calculate param).
        r.ExecResolution = execRes
        r.CurrentPrice = candlesExec.Last().Close

        ' [v36] ATR (and the whole execution stack below) computed on candlesExec.
        ' The gate, ATR levels, eval barriers, and Kelly inherit the resolution
        ' automatically because they all derive from r.ATR.
        r.ATR = IndicatorEngine.CalcATR(candlesExec, cfg.Indicators.ATR.Period)

        Dim norms As DynamicNorms = DynamicNorms.Compute(candlesExec, r.ATR)
        r.ATRSizeMultiplier = Math.Round(norms.ATRScaleFactor, 2)

        Dim rocSeries = IndicatorEngine.CalcROCSeries(candlesExec,
                            cfg.Indicators.ROC.Period,
                            cfg.Indicators.ROC.SeriesLookback)
        r.ROC = If(rocSeries.Count > 0, rocSeries.Last(), 0)
        If rocSeries.Count >= 2 Then
            Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
            ' [v36] Resolution-aware slope-delta gate (3-min ROC delta runs ~2.1× larger).
            Dim slopeDelta As Double = ExecutionResolution.ResolveRocSlopeDelta(cfg, execRes)
            r.ROCSlope = If(delta > slopeDelta, "RISING", If(delta < -slopeDelta, "FALLING", "FLAT"))
        Else
            r.ROCSlope = "FLAT"
        End If

        r.RSI = IndicatorEngine.CalcRSI(candlesExec, cfg.Indicators.RSI.Period)

        r.VolumeSMA9       = IndicatorEngine.CalcVolumeSMA(candlesExec, cfg.Indicators.Volume.SmaPeriod)
        r.CurrentVolume    = candlesExec.Last().Volume
        r.CurrentVolumeUSD = candlesExec.Last().VolumeUSD
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

        r.VWAP       = IndicatorEngine.CalcVWAP(candlesExec, r.VWAPSessionCandles, vwapS2Hour, vwapS2Minute)
        r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)
        IndicatorEngine.CalcVWAPBands(candlesExec, r.VWAP,
                                      r.VWAPSigma1Upper, r.VWAPSigma1Lower,
                                      r.VWAPSigma2Upper, r.VWAPSigma2Lower,
                                      vwapS2Hour, vwapS2Minute)

        IndicatorEngine.CalcBBW(candlesExec, cfg.Indicators.BBW.Period, cfg.Indicators.BBW.StdDev,
                                r.BBW, r.SqueezeStatus,
                                seriesWindowMultiplier:=cfg.Indicators.BBW.SeriesWindowMultiplier,
                                squeezePercentile:=cfg.Indicators.BBW.SqueezePercentile)

        IndicatorEngine.CalcTTMSqueeze(candlesExec, r.TTMHistogram, r.TTMDirection, r.TTMSignal,
                                       smaPeriod:=cfg.Indicators.TTM.SmaPeriod,
                                       linRegPeriod:=cfg.Indicators.TTM.LinRegPeriod,
                                       flatThreshold:=cfg.Indicators.TTM.FlatThreshold)

        Dim emaFast As Integer = cfg.Indicators.EMA.Fast
        Dim emaMid  As Integer = cfg.Indicators.EMA.Mid
        Dim emaSlow As Integer = cfg.Indicators.EMA.Slow
        r.EMA9  = IndicatorEngine.CalcEMA(candlesExec, emaFast)
        r.EMA21 = IndicatorEngine.CalcEMA(candlesExec, emaMid)
        r.EMA50 = IndicatorEngine.CalcEMA(candlesExec, emaSlow)
        If r.EMA9 > r.EMA21 AndAlso r.EMA21 > r.EMA50 Then
            r.EMAAlignment = "BULL"
        ElseIf r.EMA9 < r.EMA21 AndAlso r.EMA21 < r.EMA50 Then
            r.EMAAlignment = "BEAR"
        Else
            r.EMAAlignment = "MIXED"
        End If

        ' FundingBias label from cfg thresholds
        ' fundingRate.Value is safe -- skip-check above guarantees HasValue
        r.FundingRate = fundingRate.Value
        If fundingRate.Value > cfg.Scoring.FundingHighPositive Then
            r.FundingBias = "LONGS HEAVILY CROWDED"
        ElseIf fundingRate.Value > cfg.Scoring.FundingLowPositive Then
            r.FundingBias = "LONGS CROWDED"
        ElseIf fundingRate.Value < cfg.Scoring.FundingHighNegative Then
            r.FundingBias = "SHORTS HEAVILY CROWDED"
        ElseIf fundingRate.Value < cfg.Scoring.FundingLowNegative Then
            r.FundingBias = "SHORTS CROWDED"
        Else
            r.FundingBias = "NEUTRAL"
        End If

        ' funding-momentum: maintain ring buffer then compute momentum signal.
        ' Max FundingHistoryMax samples retained; cold start (< 2) yields FLAT.
        ' [S9] Dedup: Deribit publishes funding ~every 8h; appending every 1m run
        ' fills the ring with identical values and forces FLAT. Only append when
        ' the rate actually changed from the previous sample.
        If _fundingHistory.Count = 0 OrElse _fundingHistory(_fundingHistory.Count - 1) <> fundingRate.Value Then
            _fundingHistory.Add(fundingRate.Value)
            If _fundingHistory.Count > FundingHistoryMax Then
                _fundingHistory.RemoveAt(0)
            End If
        End If
        r.FundingMomentum = IndicatorEngine.CalcFundingMomentum(_fundingHistory, cfg)
        r.FundingDelta    = If(_fundingHistory.Count >= 2,
                               _fundingHistory(_fundingHistory.Count - 1) - _fundingHistory(_fundingHistory.Count - 2),
                               0.0)

        Dim nowTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        ' bookSummary.Value is safe -- skip-check above guarantees HasValue
        r.OI_Current = bookSummary.Value.OI
        _oiHistory.Add(New OiSnapshot(nowTs, bookSummary.Value.OI))
        _oiHistory = _oiHistory.Where(Function(x) nowTs - x.Ts < 70 * 60 * 1000L).ToList()

        Dim oi15m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 15 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()
        Dim oi60m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 61 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()

        r.OIChange15m = If(oi15m IsNot Nothing AndAlso oi15m.OI > 0, (r.OI_Current - oi15m.OI) / oi15m.OI * 100, 0)
        r.OIChange60m = If(oi60m IsNot Nothing AndAlso oi60m.OI > 0, (r.OI_Current - oi60m.OI) / oi60m.OI * 100, 0)

        Dim oiThr As Double = cfg.Indicators.OI.ChangeThresholdPct * 100

        ' priceUp: did price rise over the same 15m window that OIChange15m measures?
        ' Earlier implementation compared r.CurrentPrice to bookSummary.MarkPrice * 0.9999.
        ' Mark price tracks current price within ~1bp at any snapshot, so that comparison
        ' was True almost always — biasing OISignal toward NEW LONGS / COVERING and
        ' starving NEW SHORTS / CAPITULATION. The CalibrationReport on 2026-05-08 showed
        ' a 41:2 long:short asymmetry in OI×CVD CONFIRMED counts as a direct consequence.
        ' Correct comparison: current 1m close vs the close 15 candles back.
        ' [v36] Deliberately stays on candles1m (NOT candlesExec). OIChange15m is a
        ' wall-clock 15-minute delta from the _oiHistory ring buffer (resolution-
        ' independent), so the paired price direction must span the same 15 wall-clock
        ' minutes — 15 × 1m bars. On candlesExec a 15-bar lookback would be 45 min at
        ' 3m, mismatching the OI window. Proposal §4 lists OI as resolution-stable ("slow").
        Dim priceUp As Boolean = False
        If candles1m IsNot Nothing AndAlso candles1m.Count >= 16 Then
            priceUp = r.CurrentPrice > candles1m(candles1m.Count - 16).Close
        End If

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

        ' [v36] CVD's USD slope/value gates the FIXED 500-trade stream (resolution-
        ' independent — slope_min_usd stays at 1-min). Only the divergence price-gate
        ' touches candles, so pass candlesExec so the price-change is on the execution bar.
        IndicatorEngine.CalcCVD(recentTrades, candlesExec, r.CVDValue, r.CVDSlope, r.CVDDivergence,
                                slopeMinUsd:=cfg.Indicators.CVD.SlopeMinUsd,
                                slopePctOfValue:=cfg.Indicators.CVD.SlopePctOfValue,
                                divergencePriceGate:=cfg.Indicators.CVD.DivergencePriceGate,
                                lateSegmentWeight:=cfg.Indicators.CVD.LateSegmentWeight,
                                earlySegmentWeight:=cfg.Indicators.CVD.EarlySegmentWeight,
                                weightedSlopeOut:=r.CVDWeightedSlope)

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

        ' Direction-independent 15m gate state; Step 4b consults the per-side
        ' flag matching the verdict's dominant side (no pre-scoring proposal).
        IndicatorEngine.CalcMTFGate(
            candles15m,
            r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment,
            r.MTFGatePassLong, r.MTFGatePassShort, r.MTFGateDetails,
            adxPeriod:=cfg.MTFGate.DmiPeriod,
            adxMin:=cfg.MTFGate.AdxMin,
            minOf:=cfg.MTFGate.RequiredConfirms,
            candleLookback:=cfg.MTFGate.CandleCount)

        r.EMA200_5m     = IndicatorEngine.CalcEMA(candles5m, 200)
        r.PriceVsEMA200 = If(r.EMA200_5m > 0,
                              If(r.CurrentPrice > r.EMA200_5m, "ABOVE", "BELOW"),
                              "N/A")

        IndicatorEngine.CalcDonchian(candlesExec, cfg.Indicators.Donchian.Period,
                                     r.DonchianUpper, r.DonchianLower)

        Dim channelRange As Double = r.DonchianUpper - r.DonchianLower
        If channelRange > 0 Then
            Dim qPct As Double = cfg.Indicators.Donchian.QuartilePct
            Dim q1 As Double = r.DonchianLower + channelRange * qPct
            Dim q3 As Double = r.DonchianUpper - channelRange * qPct
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

        IndicatorEngine.CalcOBV(candlesExec, r.OBVTrend, r.OBVDivergence,
                                cfg.Indicators.OBV.TrendGate,
                                cfg.Indicators.OBV.DivergenceGate)

        r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(candlesExec,
                              cfg.Indicators.RSI.Period,
                              cfg.Indicators.RSI.DivergencePriceGate,
                              cfg.Indicators.RSI.DivergenceRsiDelta,
                              pivotWing:=cfg.Indicators.RSI.PivotWing,
                              lookbackBars:=cfg.Indicators.RSI.LookbackBars,
                              overboughtThreshold:=cfg.Indicators.RSI.DivergenceOverboughtThreshold,
                              oversoldThreshold:=cfg.Indicators.RSI.DivergenceOversoldThreshold)

        ' Swing pivots on 5m -- structural reference for target/stop arbitration
        ' D2: volume-weighted pivot fields populated via Optional ByRef params
        IndicatorEngine.CalcSwingPivots(candles5m,
                                         r.LastSwingHigh5m, r.LastSwingLow5m,
                                         pivotWing:=cfg.Indicators.Swing.PivotWing5m,
                                         lookbackBars:=cfg.Indicators.Swing.LookbackBars5m,
                                         bestPivotByVolume:=r.BestPivotByVolume5m,
                                         bestPivotVolumeRatio:=r.BestPivotVolumeRatio5m,
                                         bestPivotIsHigh:=r.BestPivotIsHigh5m)

        ' 15m context (already-cached candles15m) -- no volume-weighted pivots on 15m
        If candles15m IsNot Nothing AndAlso candles15m.Count > 0 Then
            IndicatorEngine.CalcSwingPivots(candles15m,
                                             r.LastSwingHigh15m, r.LastSwingLow15m,
                                             pivotWing:=cfg.Indicators.Swing.PivotWing15m,
                                             lookbackBars:=cfg.Indicators.Swing.LookbackBars15m)
        End If

        ' Direction-aware bookkeeping wrappers
        r.SwingTargetLong  = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)
        r.SwingStopLong    = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
        r.SwingTargetShort = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
        r.SwingStopShort   = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)

        ' D1: trend structure classification from 5m pivot sequence
        Dim ts5mHighs As (Older As Double, Newer As Double) = (0.0, 0.0)
        Dim ts5mLows  As (Older As Double, Newer As Double) = (0.0, 0.0)
        r.TrendStructure = IndicatorEngine.ClassifyTrendStructure(
            candles5m,
            cfg.Indicators.TrendStructure.PivotWing,
            cfg.Indicators.TrendStructure.PivotCount,
            ts5mHighs, ts5mLows)
        r.LastTwoHighs5m = ts5mHighs
        r.LastTwoLows5m  = ts5mLows

        Dim vpfrPoc          As Double   = 0
        Dim vpfrHVNearPoc    As Boolean  = False
        Dim vpfrSignal       As String   = "NEUTRAL"
        Dim vpfrBucketVols() As Double   = Array.Empty(Of Double)()
        Dim vpfrBucketLow    As Double   = 0
        Dim vpfrBucketSize   As Double   = 0
        IndicatorEngine.CalcVPFRLite(candlesExec, r.CurrentPrice,
                                     vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                                     r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
                                     r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
                                     r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
                                     vpfrBucketVols, vpfrBucketLow, vpfrBucketSize,
                                     numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                                     hvnVolPct:=cfg.Indicators.VPFR.HvnVolPct,
                                     lvnVolPct:=cfg.Indicators.VPFR.LvnVolPct,
                                     hvnProximityPct:=cfg.Indicators.VPFR.HvnProximityPct,
                                     decayBase:=cfg.Indicators.VPFR.DecayBase,
                                     valueAreaPct:=cfg.Indicators.VPFR.ValueAreaPct)
        r.VPFRPoc            = vpfrPoc
        r.VPFRHVNearPoc      = vpfrHVNearPoc
        r.VPFRSignal         = vpfrSignal
        r.VPFRBucketVolumes  = vpfrBucketVols
        r.VPFRBucketPriceLow = vpfrBucketLow
        r.VPFRBucketSize     = vpfrBucketSize

        Dim posState As PositionState = PositionState.None
        If rbLong.Checked  Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort

        Dim verdict = ScoringEngine.Calculate(r, posState, norms, cfg)
        verdict.Timestamp = DateTime.Now

        ' Spec C — surface a ledger-guard mismatch on the LOG line (engine already
        ' wrote the detailed [LEDGER_MISMATCH] line to the console). Recomputed
        ' every run so it self-clears once the guard is quiet again.
        _ledgerWarn = If(verdict.LedgerMismatch, "SC LEDGER MISMATCH — see console · ", "")

        AnalysisLogger.LogRun(r, verdict)
        UpdateLogInfo()

        ' P5b — BuildPlaintextSnapshot is the engine's only text renderer.
        ' It MUST run before the card binds: its inline CalcKellySizing call
        ' (the sole surviving invocation) populates verdict.Kelly* fields that
        ' BindCardKelly reads below — the side effect the deleted legacy
        ' RenderOutputHeader used to provide. The string feeds the output dump
        ' after the perf-strip update further down.
        Dim snapshot As String = BuildPlaintextSnapshot(verdict, r, norms, cfg, vwapWarmup, lastTradePrice)

        ' Spec C — append the ledger-guard warning to the dump block when the SC
        ' breakdown points fail to sum to the scores. Only ever present on a real
        ' mis-attribution; the SC column itself is card-only (no snapshot column).
        If verdict.LedgerMismatch Then
            snapshot &= Environment.NewLine &
                String.Format("  [LEDGER_MISMATCH] SC breakdown points do not sum to TOTAL (Long {0} / Short {1}) — see console.",
                              verdict.LongScore, verdict.ShortScore)
        End If

        BindCardScore(verdict)
        BindCardVerdict(verdict, r)
        BindCardLastPrice(r, lastTradePrice)
        BindCardAtrLevels(verdict, r, norms)
        BindCardStructural(r, isLong:=True)
        BindCardStructural(r, isLong:=False)
        ' F-10: thread vwapWarmup so the card's [WARMUP] tag uses the same
        ' threshold the text renderers compare against (line 445 / 470).
        BindCardSignalBreakdown(verdict, r, vwapWarmup)
        BindCardOiCvdCross(r, verdict)
        BindCardVolumeProfile(r)
        BindCardKelly(verdict)
        BindCardIndicatorDetails(verdict, r, norms, SettingsLoader.Current, vwapWarmup)

        ' Update live performance strip (eval cache + OHLC cache + 6 window aggregates).
        Await LivePerformanceTracker.UpdateAsync(verdict, r, candles1m, DateTime.UtcNow)
        UpdatePerformanceLabels()

        ' Append the snapshot (built above, pre-bind) to the output dump.
        ' Kept after UpdatePerformanceLabels so ComposePerfStripLine reflects
        ' this run's perf-strip state.
        AnalysisOutputDump.Append(
            timestamp:=verdict.Timestamp,
            renderedText:=snapshot,
            dumpPath:=GetDumpPath(),
            enabled:=cfg.AnalysisLogging.OutputDumpEnabled,
            maxRuns:=cfg.AnalysisLogging.OutputDumpMaxRuns,
            perfStripLine:=ComposePerfStripLine())

        ' P4f — capture last-successful state for the SKIPPED-render fallback.
        ' Must be the last thing before the AnalysisCompleted event so the
        ' captured state always reflects a card-grid that successfully painted.
        _lastSuccessfulVerdict    = verdict
        _lastSuccessfulIndicators = r
        _lastSuccessfulNorms      = norms
        _lastSuccessfulCfg        = cfg
        _lastSuccessfulRenderTime = DateTime.Now
        ' Swap the VERDICT card back to the normal panel if the previous run
        ' was skipped. UpdateLogInfo also re-runs so the "last HH:mm:ss" line
        ' reflects this successful render.
        ClearStaleOverlays()
        UpdateLogInfo()

        RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)
    End Function

End Class
