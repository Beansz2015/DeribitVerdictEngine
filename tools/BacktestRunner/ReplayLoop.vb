' tools/BacktestRunner/ReplayLoop.vb
' The heart of the backtest synthesizer (docs/backtest-synthesizer-proposal.md §3).
' Iterates bar-closes on the execution-resolution grid, slices the historical store into
' the exact live indicator shapes, walks the trade stream through a MarketState (for
' aggressor velocity), assembles IndicatorResults mirroring MainForm_Analysis.RunAnalysisAsync
' line-for-line for the reconstructable subset, calls the SHIPPED ScoringEngine.Calculate,
' and writes the synthetic v0.8 row via BacktestRowWriter.
'
' Muted signals (D2 binding — proposal §1 / task):
'   - OFI:           r.OFIRatio=1, OFISignal="BALANCED", OFIMomentum="FLAT" (no ring, no fold)
'   - Spread:        r.SpreadBps=0, r.SpreadStatus="NORMAL"
'   - OI:            r.OI_Current=0, r.OIChange15m=0, r.OIChange60m=0, r.OISignal="NEUTRAL"
'                    (Pass 2b is inert on OISignal=NEUTRAL by construction — see
'                    ScoringEngine_Calculate_Scoring OI × CVD gate)
'   - Absorption:    r.AbsorptionSignal="NONE" + null numerics (matches WS-off live behaviour)
'
' The Aggressor Velocity path IS reconstructed: we instantiate a MarketState per replay
' run and feed only the trade stream through AppendTrade + FoldAggressorVelocity at each
' historical trade's exchange timestamp — the exact shape MainForm_Analysis reads at each
' close. The BOOK path is deliberately NOT fed (no historical book snapshots), so
' time-averaged OFI and Absorption stay muted (matches transport=rest / REST-fallback).
'
' Sequential state carried across the loop:
'   - _prevRegime: the 1-bar hold on RANGE_BOUND after TRENDING/TRANSITIONAL (regime
'                  hysteresis; byte-identical to the live rule in MainForm_Analysis).
'   - _fundingHistory: (UtcMs, Rate) ring appended every close via
'                  IndicatorEngine.AppendFundingSample, momentum via CalcFundingMomentum
'                  (the v53 time-anchored window — see docs/funding-momentum-time-anchored
'                  -window-proposal.md; the D2 approximation is that the historical funding
'                  granularity is coarser than live per-run sampling).
'
' All indicator + scoring functions are the SHIPPED ones (linked, not copied). Cfg is
' the pinned settings.json passed at CLI time (defaults to the repo's live copy).

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks

''' <summary>A single funding-rate history sample. Public + top-level so ReplayLoop's
''' at-or-before helper compiles into the fixtures without pulling HistoricalStore
''' (which owns a live HttpClient) into the harness project.</summary>
Public Structure BacktestFundingSample
    Public Property TsMs As Long
    Public Property Rate As Double
End Structure

Public Class ReplayLoop

    ' Warmup depths for the leftmost bar-close slice. These match the live fetch counts
    ' (MainForm_Analysis) exactly — a 250×1m window at res=1 needs 250 minutes of prior
    ' data; the 70×15m window needs 17.5 h; funding needs 30 min. Take the max + a
    ' comfortable buffer and load that much extra to the LEFT of fromUtc.
    Public Const WarmupHours As Integer = 20

    ' Live fetch counts mirrored from MainForm_Analysis.
    Public Const Candles1mCount  As Integer = 250
    Public Const Candles5mCount  As Integer = 210
    Public Const Candles15mCount As Integer = 70
    Public Const CandlesExecCount As Integer = 250   ' when execRes != 1
    Public Const TradeWindowCount As Integer = 500

    ' §7.1 (backtest-synthesizer-proposal.md) forming-bar stub — the on-close firing
    ' latency window. Live's REST/WS chart response at closeMs+~2s includes the
    ' currently-forming bar as the last candle of every series; the synthesizer mirrors
    ' that by appending a stub built from trades in [closeMs, closeMs + FormingStubDeltaMs]
    ' (zero-trade fallback = prev close + 0 volume). The delta is fixed at 2 s — a
    ' documented approximation, not a per-run calibration.
    Public Const FormingStubDeltaMs As Long = 2000L

    Public Class RunSummary
        Public Property RowsWritten     As Integer
        Public Property RowsPerSession  As New Dictionary(Of String, Integer)()
        Public Property RowsPerVerdict  As New Dictionary(Of String, Integer)()
        Public Property FromUtc         As DateTime
        Public Property ToUtc           As DateTime
        Public Property OutputPath      As String
        Public Property InstanceId      As String
        Public Property SampleRows      As New List(Of String)()
    End Class

    ''' <summary>Slice `all` to bars whose CLOSE time (openTs + resMin*60000) is
    ''' &lt;= closeMs, then return the last N. Empty list if none qualify.</summary>
    Public Shared Function SliceCandlesAtOrBefore(all As List(Of Candle), resolutionMin As Integer,
                                                   closeMs As Long, n As Integer) As List(Of Candle)
        If all Is Nothing OrElse all.Count = 0 Then Return New List(Of Candle)()
        Dim resMs As Long = CLng(resolutionMin) * 60L * 1000L
        ' Find the largest i such that all(i).Timestamp + resMs <= closeMs.
        ' Since candles are ascending, we can walk from the end.
        Dim endIdx As Integer = -1
        For i As Integer = all.Count - 1 To 0 Step -1
            If all(i).Timestamp + resMs <= closeMs Then
                endIdx = i
                Exit For
            End If
        Next
        If endIdx < 0 Then Return New List(Of Candle)()
        Dim startIdx As Integer = Math.Max(0, endIdx - n + 1)
        Return all.GetRange(startIdx, endIdx - startIdx + 1)
    End Function

    ''' <summary>Slice trades to those with ts &lt;= closeMs, then return the last N.
    ''' Preserves the chronological ascending contract (F1).</summary>
    Public Shared Function SliceTradesAtOrBefore(all As List(Of TradeRecord),
                                                  closeMs As Long, n As Integer) As List(Of TradeRecord)
        If all Is Nothing OrElse all.Count = 0 Then Return New List(Of TradeRecord)()
        Dim endIdx As Integer = -1
        For i As Integer = all.Count - 1 To 0 Step -1
            If all(i).Timestamp <= closeMs Then
                endIdx = i
                Exit For
            End If
        Next
        If endIdx < 0 Then Return New List(Of TradeRecord)()
        Dim startIdx As Integer = Math.Max(0, endIdx - n + 1)
        Return all.GetRange(startIdx, endIdx - startIdx + 1)
    End Function

    ''' <summary>Return the newest funding sample at-or-before closeMs, or Nothing if
    ''' none exists in the loaded history.</summary>
    Public Shared Function FundingAtOrBefore(history As List(Of BacktestFundingSample), closeMs As Long) As BacktestFundingSample?
        If history Is Nothing OrElse history.Count = 0 Then Return Nothing
        Dim latest As BacktestFundingSample? = Nothing
        For i As Integer = history.Count - 1 To 0 Step -1
            If history(i).TsMs <= closeMs Then
                latest = history(i)
                Exit For
            End If
        Next
        Return latest
    End Function

    ''' <summary>§7.1 forming-bar stub — return every trade in the closed interval
    ''' [closeMs, closeMs + FormingStubDeltaMs]. Trades in the store are chronologically
    ''' ascending, so a single linear scan suffices; the window is small so the total
    ''' cost across a full replay is trivial.</summary>
    Public Shared Function TradesInStubWindow(allTrades As List(Of TradeRecord), closeMs As Long) As List(Of TradeRecord)
        Dim out As New List(Of TradeRecord)()
        If allTrades Is Nothing OrElse allTrades.Count = 0 Then Return out
        Dim endMs As Long = closeMs + FormingStubDeltaMs
        For i As Integer = 0 To allTrades.Count - 1
            Dim ts As Long = allTrades(i).Timestamp
            If ts < closeMs Then Continue For
            If ts > endMs Then Exit For
            out.Add(allTrades(i))
        Next
        Return out
    End Function

    ''' <summary>§7.1 forming-bar stub — build one Candle from the trades-in-window set.
    ''' Zero-trade fallback: {Open=High=Low=Close = prevClose, Volume=0}. The stub's
    ''' Timestamp is closeMs — a valid boundary for the 1m/3m execution-resolution grid;
    ''' for 5m/15m at non-aligned closes the value is semantically "the moment we sampled",
    ''' consumed by no timestamp-sensitive indicator on those series.</summary>
    Public Shared Function BuildFormingStub(prevClose As Double,
                                             tradesInWindow As List(Of TradeRecord),
                                             stubTsMs As Long) As Candle
        Dim stub As New Candle With {.Timestamp = stubTsMs}
        If tradesInWindow Is Nothing OrElse tradesInWindow.Count = 0 Then
            stub.Open  = prevClose
            stub.High  = prevClose
            stub.Low   = prevClose
            stub.Close = prevClose
            stub.Volume = 0
            stub.VolumeUSD = 0
            Return stub
        End If
        Dim first = tradesInWindow(0)
        Dim last  = tradesInWindow(tradesInWindow.Count - 1)
        Dim hi As Double = first.Price
        Dim lo As Double = first.Price
        Dim vol As Double = 0
        Dim volUsd As Double = 0
        For i As Integer = 0 To tradesInWindow.Count - 1
            Dim tr = tradesInWindow(i)
            If tr.Price > hi Then hi = tr.Price
            If tr.Price < lo Then lo = tr.Price
            vol += tr.Amount
            volUsd += tr.Amount * tr.Price
        Next
        stub.Open  = first.Price
        stub.High  = hi
        stub.Low   = lo
        stub.Close = last.Price
        stub.Volume = vol
        stub.VolumeUSD = volUsd
        Return stub
    End Function

    ''' <summary>§7.1 forming-bar stub — append a stub to `slice` in place. `prevClose`
    ''' is drawn from the last real bar in the slice (which is expected to be non-empty
    ''' by the caller's freshness / count gates); if the slice is empty, no-op.</summary>
    Public Shared Sub AppendFormingStub(slice As List(Of Candle),
                                         stubTrades As List(Of TradeRecord),
                                         closeMs As Long)
        If slice Is Nothing OrElse slice.Count = 0 Then Return
        Dim prevClose As Double = slice(slice.Count - 1).Close
        slice.Add(BuildFormingStub(prevClose, stubTrades, closeMs))
    End Sub

    ' -- Per-run driver ---------------------------------------------------------------
    '
    ' cfg          — pinned EngineSettings (typically SettingsLoader.Current after Initialise).
    ' fromUtc/toUtc — [inclusive, exclusive) replay window; bar-closes at every execRes-min
    '                 tick inside this window are scored.
    ' outputPath   — synthetic CSV path (BACKTEST-*.csv per proposal §2).
    Public Shared Function Run(cfg As EngineSettings,
                                fromUtc As DateTime, toUtc As DateTime,
                                outputPath As String) As RunSummary
        Dim warmupStart As DateTime = fromUtc.AddHours(-WarmupHours)

        Console.WriteLine("[Replay] Loading historical store ...")
        Dim c1m  As List(Of Candle) = HistoricalStore.LoadCandleRange(1, warmupStart, toUtc)
        Dim c3m  As List(Of Candle) = HistoricalStore.LoadCandleRange(3, warmupStart, toUtc)
        Dim c5m  As List(Of Candle) = HistoricalStore.LoadCandleRange(5, warmupStart, toUtc)
        Dim c15m As List(Of Candle) = HistoricalStore.LoadCandleRange(15, warmupStart, toUtc)
        Dim allTrades = HistoricalStore.LoadTradeRange(warmupStart, toUtc)
        Dim allFunding = HistoricalStore.LoadFundingRange(warmupStart, toUtc)
        Console.WriteLine(String.Format(
            "[Replay] Loaded: 1m={0} 3m={1} 5m={2} 15m={3} trades={4} funding={5}",
            c1m.Count, c3m.Count, c5m.Count, c15m.Count, allTrades.Count, allFunding.Count))

        Dim writer As New BacktestRowWriter(outputPath)
        Dim summary As New RunSummary With {
            .FromUtc = fromUtc, .ToUtc = toUtc,
            .OutputPath = outputPath, .InstanceId = writer.InstanceId}

        ' Sequential state.
        Dim prevRegime As String = ""
        Dim fundingHistory As New List(Of (UtcMs As Long, Rate As Double))()
        Dim state As New MarketState()
        Dim tradeCursor As Integer = 0     ' index into allTrades of the next un-fed trade
        Dim avCfg = cfg.Indicators.AggressorVelocity

        ' Iterate every minute in [fromUtc, toUtc). At each minute compute utcHour and
        ' the session's exec resolution; a close fires only when the minute is on that
        ' resolution's grid (minute % execRes == 0).
        Dim curUtc As DateTime = New DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day,
                                              fromUtc.Hour, fromUtc.Minute, 0, DateTimeKind.Utc)
        Dim tick As Integer = 0
        While curUtc < toUtc
            Dim utcHour As Integer = curUtc.Hour
            Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, utcHour)
            If execRes <= 0 Then execRes = 1
            If curUtc.Minute Mod execRes <> 0 Then
                curUtc = curUtc.AddMinutes(1)
                Continue While
            End If

            Dim closeMs As Long = New DateTimeOffset(curUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()

            ' Feed all trades in (lastFed, closeMs + stub delta] through MarketState
            ' (aggr-vel only). The +stub-delta widening mirrors live's poll-at-closeMs+~2s
            ' state — the trades in [closeMs, closeMs+2s] are already folded when live
            ' reads the accumulator (§7.1).
            Dim aggrCutoff As Long = closeMs + FormingStubDeltaMs
            If avCfg IsNot Nothing AndAlso avCfg.Enabled Then
                Dim tauFast As Double = avCfg.FastWindowSec
                Dim tauNorm As Double = ExecutionResolution.ResolveAggrVelNormWindow(cfg, utcHour)
                While tradeCursor < allTrades.Count AndAlso allTrades(tradeCursor).Timestamp <= aggrCutoff
                    Dim tr = allTrades(tradeCursor)
                    state.AppendTrade(tr, DateTime.UtcNow)
                    state.FoldAggressorVelocity(tr.Amount, tr.Direction = "buy", tr.Timestamp, tauFast, tauNorm)
                    tradeCursor += 1
                End While
            End If

            ' Slice closed-bar windows. We deliberately request (count - 1) closed bars
            ' per series, then append the §7.1 forming stub as the last bar — the resulting
            ' total-count matches live's chart-endpoint response byte-for-byte (which is
            ' (count-1) closed bars + 1 forming bar).
            Dim slice1m  = SliceCandlesAtOrBefore(c1m, 1, closeMs, Candles1mCount - 1)
            Dim slice5m  = SliceCandlesAtOrBefore(c5m, 5, closeMs, Candles5mCount - 1)
            Dim slice15m = SliceCandlesAtOrBefore(c15m, 15, closeMs, Candles15mCount - 1)
            Dim slice3m As List(Of Candle) = Nothing
            If execRes = 3 Then
                slice3m = SliceCandlesAtOrBefore(c3m, 3, closeMs, CandlesExecCount - 1)
            End If

            Dim sliceExec As List(Of Candle)
            If execRes = 1 Then
                sliceExec = slice1m
            ElseIf execRes = 3 Then
                sliceExec = slice3m
            Else
                sliceExec = slice5m
            End If

            ' Trade slice widened to closeMs + stub delta so live's [closeMs, closeMs+2s]
            ' trades are inside our indicator window too (CVD / TFI / MicroCVD / etc. see
            ' the same trades live saw at poll time).
            Dim sliceTrades = SliceTradesAtOrBefore(allTrades, aggrCutoff, TradeWindowCount)

            ' Live-parity skip gates (mirroring RunAnalysisAsync). Under-populated windows
            ' or stale candles ⇒ no row (no scoring, matches the live SKIPPED path).
            ' Gates run on the RAW closed-bar slices — the stub-appended count is a mirror
            ' artefact, not a warmup signal.
            If slice1m.Count < 50 OrElse slice5m.Count < 30 OrElse
               (execRes <> 1 AndAlso sliceExec.Count < 50) OrElse
               sliceTrades.Count = 0 Then
                curUtc = curUtc.AddMinutes(1) : Continue While
            End If
            If Not IndicatorEngine.IsFresh(sliceExec, execRes, curUtc) OrElse
               Not IndicatorEngine.IsFresh(slice5m, 5, curUtc) Then
                curUtc = curUtc.AddMinutes(1) : Continue While
            End If

            ' -- §7.1 FORMING-BAR STUBS: mirror live's convention across all four series.
            ' A single trade window [closeMs, closeMs + 2s] feeds every stub (they differ
            ' only in prevClose, drawn from each series' last-real bar). Under execRes=1
            ' the sliceExec/slice1m reference is shared, so the 1m append updates both.
            Dim stubTrades = TradesInStubWindow(allTrades, closeMs)
            AppendFormingStub(slice1m,  stubTrades, closeMs)
            AppendFormingStub(slice5m,  stubTrades, closeMs)
            AppendFormingStub(slice15m, stubTrades, closeMs)
            If slice3m IsNot Nothing Then AppendFormingStub(slice3m, stubTrades, closeMs)

            ' Funding for this close.
            Dim fund = FundingAtOrBefore(allFunding, closeMs)
            If Not fund.HasValue Then
                ' No funding sample yet — matches live's HasValue skip path.
                curUtc = curUtc.AddMinutes(1) : Continue While
            End If
            Dim fundingRate As Double = fund.Value.Rate

            ' -- Assemble IndicatorResults r (mirror RunAnalysisAsync order) --
            Dim r As New IndicatorResults()
            r.ExecResolution = execRes
            r.RocMagnitudeThreshold = ExecutionResolution.ResolveRocMagnitudeForHour(cfg, utcHour)
            r.SessionUtcHour = utcHour
            r.CurrentPrice = sliceExec.Last().Close

            r.ATR = IndicatorEngine.CalcATR(sliceExec, cfg.Indicators.ATR.Period)
            Dim norms As DynamicNorms = DynamicNorms.Compute(sliceExec, r.ATR, utcHour)
            r.ATRSizeMultiplier = Math.Round(norms.ATRScaleFactor, 2)

            Dim rocSeries = IndicatorEngine.CalcROCSeries(sliceExec,
                                cfg.Indicators.ROC.Period, cfg.Indicators.ROC.SeriesLookback)
            r.ROC = If(rocSeries.Count > 0, rocSeries.Last(), 0)
            If rocSeries.Count >= 2 Then
                Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
                Dim slopeDelta As Double = ExecutionResolution.ResolveRocSlopeDelta(cfg, execRes)
                r.ROCSlope = If(delta > slopeDelta, "RISING", If(delta < -slopeDelta, "FALLING", "FLAT"))
            Else
                r.ROCSlope = "FLAT"
            End If

            r.RSI = IndicatorEngine.CalcRSI(sliceExec, cfg.Indicators.RSI.Period)

            r.VolumeSMA9       = IndicatorEngine.CalcVolumeSMA(sliceExec, cfg.Indicators.Volume.SmaPeriod)
            r.CurrentVolume    = sliceExec.Last().Volume
            r.CurrentVolumeUSD = sliceExec.Last().VolumeUSD
            r.VolumeRatio      = If(r.VolumeSMA9 > 0, r.CurrentVolume / r.VolumeSMA9, 1)

            IndicatorEngine.CalcDMI(slice5m, cfg.Indicators.DMI.Period, r.PlusDI, r.MinusDI, r.ADX)
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
            Dim prevWasTrending As Boolean = (prevRegime = "TRENDING_UP" OrElse
                                              prevRegime = "TRENDING_DOWN" OrElse
                                              prevRegime = "TRANSITIONAL")
            If rawRegime = "RANGE_BOUND" AndAlso prevWasTrending Then
                r.Regime = prevRegime
            Else
                r.Regime = rawRegime
            End If
            prevRegime = rawRegime

            ' §7.5: pass the bar close as the session anchor. Live's default (DateTime.UtcNow)
            ' is correct there because now ~= candle time; under replay it would anchor
            ' historical candles to the real wall clock. curUtc is already this loop's "now"
            ' (it drives the IsFresh gates above), so the anchor and the freshness gate agree.
            Dim vwapS2Hour   As Integer = cfg.Indicators.VWAP.Session2StartHour
            Dim vwapS2Minute As Integer = cfg.Indicators.VWAP.Session2StartMinute
            r.VWAP       = IndicatorEngine.CalcVWAP(sliceExec, r.VWAPSessionCandles,
                                                    vwapS2Hour, vwapS2Minute, curUtc)
            r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)
            IndicatorEngine.CalcVWAPBands(sliceExec, r.VWAP,
                                          r.VWAPSigma1Upper, r.VWAPSigma1Lower,
                                          r.VWAPSigma2Upper, r.VWAPSigma2Lower,
                                          vwapS2Hour, vwapS2Minute, curUtc)

            IndicatorEngine.CalcBBW(sliceExec, cfg.Indicators.BBW.Period, cfg.Indicators.BBW.StdDev,
                                    r.BBW, r.SqueezeStatus,
                                    seriesWindowMultiplier:=cfg.Indicators.BBW.SeriesWindowMultiplier,
                                    squeezePercentile:=cfg.Indicators.BBW.SqueezePercentile)
            IndicatorEngine.CalcTTMSqueeze(sliceExec, r.TTMHistogram, r.TTMDirection, r.TTMSignal,
                                           smaPeriod:=cfg.Indicators.TTM.SmaPeriod,
                                           linRegPeriod:=cfg.Indicators.TTM.LinRegPeriod,
                                           flatThreshold:=cfg.Indicators.TTM.FlatThreshold)

            r.EMA9  = IndicatorEngine.CalcEMA(sliceExec, cfg.Indicators.EMA.Fast)
            r.EMA21 = IndicatorEngine.CalcEMA(sliceExec, cfg.Indicators.EMA.Mid)
            r.EMA50 = IndicatorEngine.CalcEMA(sliceExec, cfg.Indicators.EMA.Slow)
            If r.EMA9 > r.EMA21 AndAlso r.EMA21 > r.EMA50 Then
                r.EMAAlignment = "BULL"
            ElseIf r.EMA9 < r.EMA21 AndAlso r.EMA21 < r.EMA50 Then
                r.EMAAlignment = "BEAR"
            Else
                r.EMAAlignment = "MIXED"
            End If

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

            ' v53 funding-momentum: append every close, evict at 30 min.
            IndicatorEngine.AppendFundingSample(fundingHistory, closeMs, fundingRate)
            r.FundingMomentum = IndicatorEngine.CalcFundingMomentum(fundingHistory, closeMs, cfg)
            r.FundingDelta    = If(fundingHistory.Count >= 2,
                                   fundingHistory(fundingHistory.Count - 1).Rate - fundingHistory(fundingHistory.Count - 2).Rate,
                                   0.0)

            ' -- MUTED signals (D2 binding) --
            r.OI_Current  = 0
            r.OIChange15m = 0
            r.OIChange60m = 0
            r.OISignal    = "NEUTRAL"

            r.OFIRatio    = 1.0
            r.OFIBidVol   = 0
            r.OFIAskVol   = 0
            r.OFISignal   = "BALANCED"
            r.OFIMomentum = "FLAT"

            r.SpreadBps    = 0
            r.SpreadStatus = "NORMAL"

            r.AbsorptionSignal   = "NONE"
            r.AbsorptionLevel    = Nothing
            r.AbsorptionRatio    = Nothing
            r.AbsorptionAggrUsd  = Nothing
            r.AbsorptionPullFrac = Nothing

            ' -- Trade-derived signals (reconstructable at full fidelity) --
            IndicatorEngine.CalcLiquidations(sliceTrades, r.LiqLongSize, r.LiqShortSize, r.LiqSignal,
                                             dominanceRatio:=cfg.Indicators.Liquidations.DominanceRatio)
            IndicatorEngine.CalcCVD(sliceTrades, sliceExec, r.CVDValue, r.CVDSlope, r.CVDDivergence,
                                    slopeMinUsd:=cfg.Indicators.CVD.SlopeMinUsd,
                                    slopePctOfValue:=cfg.Indicators.CVD.SlopePctOfValue,
                                    divergencePriceGate:=cfg.Indicators.CVD.DivergencePriceGate,
                                    lateSegmentWeight:=cfg.Indicators.CVD.LateSegmentWeight,
                                    earlySegmentWeight:=cfg.Indicators.CVD.EarlySegmentWeight,
                                    weightedSlopeOut:=r.CVDWeightedSlope)
            IndicatorEngine.CalcTFI(sliceTrades, r.TFIValue, r.TFISignal,
                                    tfiWindowSize:=cfg.Indicators.TFI.WindowSize,
                                    threshold:=cfg.Indicators.TFI.Threshold)
            IndicatorEngine.CalcMicroCVD(sliceTrades,
                                         r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                         r.MicroCVDMomentum, r.MicroCVDSignal,
                                         microWindowSize:=cfg.Indicators.MicroCVD.WindowSize,
                                         accelThreshold:=cfg.Indicators.MicroCVD.AccelThreshold,
                                         dynamicPct:=cfg.Indicators.MicroCVD.AccelThresholdDynamicPct,
                                         floorPct:=cfg.Indicators.MicroCVD.AccelThresholdFloorPct)

            ' Aggressor velocity — read only when the accumulator has warmed up (§8).
            If avCfg IsNot Nothing AndAlso avCfg.Enabled Then
                Dim avNormWin As Double = ExecutionResolution.ResolveAggrVelNormWindow(cfg, utcHour)
                Dim avSnap = state.GetAggressorVelocity(avCfg.GrossFloorUsdPerSec, avNormWin)
                If avSnap.HasWarmup Then
                    r.AggrVelBurstRatio = avSnap.BurstRatio
                    r.AggrVelNet        = avSnap.NetUsdPerSec
                    r.AggrVelSignal     = IndicatorEngine.ClassifyAggressorBurst(
                                              avSnap.BurstRatio, avSnap.Lean,
                                              ExecutionResolution.ResolveAggrVelBurstThreshold(cfg, utcHour),
                                              avCfg.DirectionLeanFloor)
                End If
            End If

            ' MTF gate (15m).
            IndicatorEngine.CalcMTFGate(
                slice15m,
                r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment,
                r.MTFGatePassLong, r.MTFGatePassShort, r.MTFGateDetails,
                adxPeriod:=cfg.MTFGate.DmiPeriod, adxMin:=cfg.MTFGate.AdxMin,
                minOf:=cfg.MTFGate.RequiredConfirms, candleLookback:=cfg.MTFGate.CandleCount)

            r.EMA200_5m     = IndicatorEngine.CalcEMA(slice5m, 200)
            r.PriceVsEMA200 = If(r.EMA200_5m > 0,
                                  If(r.CurrentPrice > r.EMA200_5m, "ABOVE", "BELOW"), "N/A")

            IndicatorEngine.CalcDonchian(sliceExec, cfg.Indicators.Donchian.Period,
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

            IndicatorEngine.CalcOBV(sliceExec, r.OBVTrend, r.OBVDivergence,
                                    cfg.Indicators.OBV.TrendGate, cfg.Indicators.OBV.DivergenceGate)

            r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(sliceExec,
                                  cfg.Indicators.RSI.Period,
                                  cfg.Indicators.RSI.DivergencePriceGate,
                                  cfg.Indicators.RSI.DivergenceRsiDelta,
                                  pivotWing:=cfg.Indicators.RSI.PivotWing,
                                  lookbackBars:=cfg.Indicators.RSI.LookbackBars,
                                  overboughtThreshold:=cfg.Indicators.RSI.DivergenceOverboughtThreshold,
                                  oversoldThreshold:=cfg.Indicators.RSI.DivergenceOversoldThreshold)

            IndicatorEngine.CalcSwingPivots(slice5m,
                                             r.LastSwingHigh5m, r.LastSwingLow5m,
                                             pivotWing:=cfg.Indicators.Swing.PivotWing5m,
                                             lookbackBars:=cfg.Indicators.Swing.LookbackBars5m,
                                             bestPivotByVolume:=r.BestPivotByVolume5m,
                                             bestPivotVolumeRatio:=r.BestPivotVolumeRatio5m,
                                             bestPivotIsHigh:=r.BestPivotIsHigh5m)
            If slice15m IsNot Nothing AndAlso slice15m.Count > 0 Then
                IndicatorEngine.CalcSwingPivots(slice15m,
                                                 r.LastSwingHigh15m, r.LastSwingLow15m,
                                                 pivotWing:=cfg.Indicators.Swing.PivotWing15m,
                                                 lookbackBars:=cfg.Indicators.Swing.LookbackBars15m)
            End If
            r.SwingTargetLong  = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)
            r.SwingStopLong    = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
            r.SwingTargetShort = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
            r.SwingStopShort   = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)

            Dim ts5mHighs As (Older As Double, Newer As Double) = (0.0, 0.0)
            Dim ts5mLows  As (Older As Double, Newer As Double) = (0.0, 0.0)
            r.TrendStructure = IndicatorEngine.ClassifyTrendStructure(
                slice5m,
                cfg.Indicators.TrendStructure.PivotWing,
                cfg.Indicators.TrendStructure.PivotCount,
                ts5mHighs, ts5mLows)
            r.LastTwoHighs5m = ts5mHighs
            r.LastTwoLows5m  = ts5mLows

            Dim vpfrPoc As Double = 0
            Dim vpfrHVNearPoc As Boolean = False
            Dim vpfrSignal As String = "NEUTRAL"
            Dim vpfrBucketVols() As Double = Array.Empty(Of Double)()
            Dim vpfrBucketLow As Double = 0
            Dim vpfrBucketSize As Double = 0
            IndicatorEngine.CalcVPFRLite(sliceExec, r.CurrentPrice,
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

            ' -- Score --
            Dim verdict = ScoringEngine.Calculate(r, PositionState.None, norms, cfg)

            ' -- Write --
            writer.WriteRow(r, verdict, cfg, curUtc)
            summary.RowsWritten += 1

            Dim sessionName As String = ResolveSessionLabel(cfg, utcHour)
            If Not summary.RowsPerSession.ContainsKey(sessionName) Then summary.RowsPerSession(sessionName) = 0
            summary.RowsPerSession(sessionName) += 1
            Dim vk As String = If(verdict.Verdict, "")
            If Not summary.RowsPerVerdict.ContainsKey(vk) Then summary.RowsPerVerdict(vk) = 0
            summary.RowsPerVerdict(vk) += 1

            If summary.RowsWritten <= 3 Then
                summary.SampleRows.Add(String.Format(CultureInfo.InvariantCulture,
                    "{0} {1,-8} px={2:F2} ls/ss={3}/{4} regime={5} exec={6}m sess={7}",
                    curUtc.ToString("yyyy-MM-dd HH:mm:ss"), If(verdict.Verdict, ""), r.CurrentPrice,
                    verdict.EffectiveLongScore, verdict.EffectiveShortScore,
                    r.Regime, execRes, sessionName))
            End If

            tick += 1
            If tick Mod 1000 = 0 Then
                Console.WriteLine(String.Format("[Replay] {0} closes scored, latest {1:yyyy-MM-dd HH:mm}", tick, curUtc))
            End If

            curUtc = curUtc.AddMinutes(1)
        End While

        Return summary
    End Function

    ''' <summary>Bucket name for a given UTC hour (mirrors the shared MatchSessionBucket).
    ''' Returns "UNKNOWN" if the cfg has no matching bucket.</summary>
    Public Shared Function ResolveSessionLabel(cfg As EngineSettings, utcHour As Integer) As String
        Dim b = ExecutionResolution.MatchSessionBucket(cfg, utcHour)
        If b Is Nothing OrElse String.IsNullOrEmpty(b.Name) Then Return "UNKNOWN"
        Return b.Name
    End Function

End Class
