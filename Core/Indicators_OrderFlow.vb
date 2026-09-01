' Core/Indicators_OrderFlow.vb
' IndicatorEngine partial: order flow and market microstructure indicators.
' Covers: OFI, Liquidations, CVD, TFI, MicroCVD, FundingMomentum.
'
' v0.48 [P4]: TFI window size separated from MicroCVD window size.
'   Previously both CalcTFI and CalcMicroCVD defaulted to windowSize=50 and
'   shared the same optional parameter name at the call site in RunAnalysisAsync.
'   TFI measures recent aggressor pressure over a short burst window (optimally
'   20-30 trades); MicroCVD measures structural segmentation over a wider window
'   (50 trades default) to detect acceleration vs deceleration across thirds.
'   Using the same window conflated two distinct measurements.  The fix:
'     - CalcTFI: Optional tfiWindowSize As Integer = 30  (was 50, renamed)
'     - CalcMicroCVD: Optional microWindowSize As Integer = 50  (renamed for clarity)
'     - Call site in RunAnalysisAsync passes cfg.Indicators.TFI.WindowSize and
'       cfg.Indicators.MicroCVD.WindowSize independently.
'
' v0.51 [P14]: OFI dominance thresholds now injectable via optional parameters.
'   Was: hardcoded 1.2 (BUY DOMINANT) and 0.833 (SELL DOMINANT) inside CalcOFI.
'   Now: Optional buyDominantRatio As Double = 1.2
'        Optional sellDominantRatio As Double = 0.833
'   Call site passes cfg.Indicators.OFI.BuyDominantRatio / SellDominantRatio.
'   cfg keys already existed (added v0.30) but were never wired into CalcOFI.
'
' fix [T2-B]: OFI BookDepth now injectable via Optional bookDepth As Integer = 3.
'   Was: Take(3) hardcoded in two places inside CalcOFI; weights array fixed at {3,2,1}.
'   Now: Take(bookDepth) used for both bids and asks; weights array built dynamically
'   as descending integers (depth, depth-1, ..., 1), normalising weighting scheme
'   consistently regardless of depth setting.
'   Call site in RunAnalysisAsync passes cfg.Indicators.OFI.BookDepth.
'   cfg key already existed (added v0.30, default 3) but was never wired through.
'
' fix [T3-D]: CalcLiquidations dominanceRatio now injectable via Optional parameter.
'   Was: liqLongSize >= liqShortSize (hardcoded equal-or-greater threshold).
'   Now: Optional dominanceRatio As Double = 1.0
'        LONG LIQS fires when liqLongSize > 0 AND liqLongSize >= liqShortSize * dominanceRatio.
'        SHORT LIQS fires when liqShortSize > 0 AND liqShortSize > liqLongSize * dominanceRatio.
'   Default 1.0 preserves existing behaviour exactly.
'   Call site passes cfg.Indicators.Liquidations.DominanceRatio.
'
' funding-momentum: CalcFundingMomentum + AppendFundingSample.
'   Derives RISING / FALLING / FLAT from a TIME-ANCHORED window over a ring of
'   timestamped funding rate samples (v53 — was a count-indexed window, which made
'   the state cadence-dependent; see the block comment on CalcFundingMomentum).
'   Cold start / post-gap (no sample >= W old) returns FLAT.
'   Window and threshold injectable via cfg.Indicators.Funding.MomentumWindowMinutes /
'   MomentumThreshold. Called from RunAnalysisAsync after GetFundingRateAsync().
'
' fix [orderfix F1]: trade lists are CHRONOLOGICAL ASCENDING (oldest first).
'   GetRecentTradesAsync reverses the API's newest-first order before returning
'   (see its doc comment). Window-consuming indicators (TFI, MicroCVD) take the
'   most recent n trades from the END of the list via LastN — Take(n) would
'   select the OLDEST n. Positional segment labels (early/mid/late) in CalcCVD
'   and CalcMicroCVD are chronologically truthful under this contract.

Partial Public Class IndicatorEngine

    ' -- Recent-trade window helper --------------------------------------------
    ' Input lists are chronological ascending, so the most recent n trades are
    ' the LAST n elements. n >= Count returns the whole list, preserving the
    ' short-list behaviour of the previous Take(n) call sites.
    Private Shared Function LastN(trades As List(Of TradeRecord), n As Integer) As List(Of TradeRecord)
        Return trades.Skip(Math.Max(0, trades.Count - n)).ToList()
    End Function

    ' -- OFI (Order Flow Imbalance) configurable depth, descending weights ----
    ' [P14] v0.51: buyDominantRatio / sellDominantRatio optional params
    ' [T2-B]: bookDepth optional param -- replaces hardcoded Take(3) / {3,2,1}.
    ' Weights are built as {depth, depth-1, ..., 1} so the nearest level always
    ' carries the highest weight regardless of how many levels are used.
    ' [P4 #4 v46]: the weighted-imbalance math + the dominance classification are now
    ' two pure helpers (ComputeOfiImbalance / ClassifyOfiRatio) shared with the
    ' time-averaged OFI accumulator (OfiAccumulator), so the snapshot and the WS-averaged
    ' paths run the SAME cap/floor/weight logic. CalcOFI is byte-identical to v45.
    Public Shared Sub CalcOFI(orderBook As OrderBookSnapshot,
                               ByRef ofiRatio As Double, ByRef ofiSignal As String,
                               ByRef ofiBidVol As Double, ByRef ofiAskVol As Double,
                               Optional buyDominantRatio  As Double  = 1.2,
                               Optional sellDominantRatio As Double  = 0.833,
                               Optional bookDepth         As Integer = 3)
        ofiRatio = 1.0 : ofiSignal = "BALANCED" : ofiBidVol = 0 : ofiAskVol = 0

        Dim bidVol, askVol, ratio As Double
        Dim ok As Boolean = ComputeOfiImbalance(orderBook, bookDepth, bidVol, askVol, ratio)
        ' Surface the weighted volumes whether or not the ratio is usable — matches the
        ' pre-refactor ordering (ofiBidVol/ofiAskVol were assigned before the total check).
        ofiBidVol = bidVol
        ofiAskVol = askVol
        If Not ok Then Return   ' book Nothing or zero total → BALANCED, ratio 1.0 (byte-identical)

        ofiRatio  = ratio
        ofiSignal = ClassifyOfiRatio(ratio, buyDominantRatio, sellDominantRatio)
    End Sub

    ''' <summary>
    ''' Pure top-book weighted imbalance — the shared math behind CalcOFI and the
    ''' time-averaged OFI accumulator (OfiAccumulator). Computes the descending-weighted
    ''' bid/ask volumes over bookDepth levels and the sanity-bounded bid/ask ratio.
    ''' NO classification. Returns False when the book is unusable (Nothing, or zero total
    ''' weighted volume) — the caller leaves OFI defaults / skips the accumulator fold.
    ''' The ratio is floored/capped to [1/1000, 1000] exactly as CalcOFI did before the
    ''' extraction (a near-zero ask side on a pulled/thin book otherwise blows the ratio up
    ''' and pollutes the histogram + the auto-tweaker outlier audit).
    ''' </summary>
    Public Shared Function ComputeOfiImbalance(orderBook As OrderBookSnapshot,
                                                bookDepth As Integer,
                                                ByRef bidVol As Double,
                                                ByRef askVol As Double,
                                                ByRef ratio As Double) As Boolean
        bidVol = 0 : askVol = 0 : ratio = 1.0
        If orderBook Is Nothing Then Return False

        Dim depth As Integer = Math.Max(1, bookDepth)
        Dim bids = orderBook.Bids.Take(depth).ToList()
        Dim asks = orderBook.Asks.Take(depth).ToList()

        ' Build descending weight array: {depth, depth-1, ..., 1}
        Dim weights(depth - 1) As Double
        For i As Integer = 0 To depth - 1
            weights(i) = depth - i
        Next

        For i As Integer = 0 To Math.Min(bids.Count, depth) - 1
            bidVol += bids(i).Size * weights(i)
        Next
        For i As Integer = 0 To Math.Min(asks.Count, depth) - 1
            askVol += asks(i).Size * weights(i)
        Next

        Dim total As Double = bidVol + askVol
        If total = 0 Then Return False   ' ratio stays 1.0; caller treats as BALANCED / no fold

        Const RatioCap     As Double = 1000.0
        Const VolumeFloor  As Double = 0.001

        Dim safeAsk As Double = Math.Max(askVol, VolumeFloor)
        ratio = bidVol / safeAsk
        If ratio > RatioCap Then
            ratio = RatioCap
        ElseIf ratio < (1.0 / RatioCap) Then
            ratio = 1.0 / RatioCap
        End If
        Return True
    End Function

    ''' <summary>
    ''' Classify a (sanity-bounded) OFI bid/ask ratio into BUY DOMINANT / SELL DOMINANT /
    ''' BALANCED against the dominance thresholds. Shared by CalcOFI and the WS-averaged
    ''' path so both classify identically. [P4 #4 v46]
    ''' </summary>
    Public Shared Function ClassifyOfiRatio(ratio As Double,
                                             buyDominantRatio As Double,
                                             sellDominantRatio As Double) As String
        If ratio > buyDominantRatio Then
            Return "BUY DOMINANT"
        ElseIf ratio < sellDominantRatio Then
            Return "SELL DOMINANT"
        Else
            Return "BALANCED"
        End If
    End Function

    ''' <summary>
    ''' [P4 #5] Classify an aggressor-velocity burst snapshot into BURST_BUY / BURST_SELL /
    ''' NORMAL (docs/aggressor-velocity-proposal.md §4.3). Pure — parallels ClassifyOfiRatio.
    ''' Fires ONLY when the tape is bursting (burstRatio >= threshold) AND the burst is
    ''' directional (|lean| >= floor): a balanced firehose and a one-sided trickle are both
    ''' NORMAL — the proposal's non-directional-reward guard (§2.1) made concrete.
    ''' burstRatioThreshold is the session-resolved value
    ''' (ExecutionResolution.ResolveAggrVelBurstThreshold).
    ''' </summary>
    Public Shared Function ClassifyAggressorBurst(burstRatio As Double,
                                                   lean As Double,
                                                   burstRatioThreshold As Double,
                                                   directionLeanFloor As Double) As String
        If burstRatio >= burstRatioThreshold Then
            If lean >= directionLeanFloor Then Return "BURST_BUY"
            If lean <= -directionLeanFloor Then Return "BURST_SELL"
        End If
        Return "NORMAL"
    End Function

    ''' <summary>
    ''' [P4 #6] Classify a level-absorption tracker read into ABSORB_ABOVE / ABSORB_BELOW /
    ''' NONE (docs/book-absorption-proposal.md §4.2). Pure — parallels ClassifyOfiRatio /
    ''' ClassifyAggressorBurst. A side is a candidate when its episode is ACTIVE (which
    ''' already implies no progress — a break-through closes the episode instantly),
    ''' aggrUsd ≥ the session-resolved min_aggr_usd, and absorbRatio ≥ absorb_ratio.
    ''' D8 veto: a candidate whose pullFrac exceeds max_pull_frac is painted defense →
    ''' NONE for that side. Both sides qualifying (tight bracketing levels) → the higher
    ''' ratio wins. NONE is the modal state by construction — the proximity gate keeps
    ''' the tracker IDLE on most runs, and no non-directional payout exists (§2).
    '''
    ''' The returned read also carries the PRIMARY episode's numerics for CSV/display:
    ''' the signalling side when one fires, else the active side with the larger
    ''' aggrUsd — pullFrac is logged even on vetoed episodes (D8: the W4 fidelity-binds
    ''' evidence). HasEpisode=False ⇒ all numerics meaningless (null CSV, §4.3).
    ''' [absorption instrumentation, 2026-09-01] The read also carries the primary
    ''' episode's EpisodeSec / PullLB / PostLB / SizeStart / SizeMin, on the same
    ''' HasEpisode discipline — CSV-only diagnostics, no scoring or display effect.
    ''' </summary>
    Public Shared Function ClassifyAbsorption(snap As AbsorptionSnapshot,
                                              minAggrUsd As Double,
                                              absorbRatioThreshold As Double,
                                              maxPullFrac As Double) As AbsorptionRead
        Dim aboveFires As Boolean =
            snap.Above.Active AndAlso snap.Above.AggrUsd >= minAggrUsd AndAlso
            snap.Above.AbsorbRatio >= absorbRatioThreshold AndAlso
            snap.Above.PullFrac <= maxPullFrac
        Dim belowFires As Boolean =
            snap.Below.Active AndAlso snap.Below.AggrUsd >= minAggrUsd AndAlso
            snap.Below.AbsorbRatio >= absorbRatioThreshold AndAlso
            snap.Below.PullFrac <= maxPullFrac

        Dim primary As AbsorptionSideRead
        Dim signal As String = "NONE"
        If aboveFires AndAlso belowFires Then
            primary = If(snap.Above.AbsorbRatio >= snap.Below.AbsorbRatio, snap.Above, snap.Below)
            signal = If(snap.Above.AbsorbRatio >= snap.Below.AbsorbRatio, "ABSORB_ABOVE", "ABSORB_BELOW")
        ElseIf aboveFires Then
            primary = snap.Above : signal = "ABSORB_ABOVE"
        ElseIf belowFires Then
            primary = snap.Below : signal = "ABSORB_BELOW"
        ElseIf snap.Above.Active AndAlso snap.Below.Active Then
            primary = If(snap.Above.AggrUsd >= snap.Below.AggrUsd, snap.Above, snap.Below)
        ElseIf snap.Above.Active Then
            primary = snap.Above
        ElseIf snap.Below.Active Then
            primary = snap.Below
        Else
            Return New AbsorptionRead With {.Signal = "NONE", .HasEpisode = False}
        End If

        Return New AbsorptionRead With {
            .Signal = signal,
            .HasEpisode = True,
            .LevelPrice = primary.LevelPrice,
            .AbsorbRatio = primary.AbsorbRatio,
            .AggrUsd = primary.AggrUsd,
            .PullFrac = primary.PullFrac,
            .EpisodeSec = primary.EpisodeSec,
            .PullLB = primary.PullLB,
            .PostLB = primary.PostLB,
            .SizeStart = primary.SizeStart,
            .SizeMin = primary.SizeMin}
    End Function

    ' -- Liquidations ---------------------------------------------------------
    ' [T3-D]: dominanceRatio optional param.
    ' LONG LIQS: liqLongSize >= liqShortSize * dominanceRatio.
    ' SHORT LIQS: liqShortSize > liqLongSize * dominanceRatio.
    ' Default 1.0 = equal-or-greater fallback; live config (settings.json) supplies 2.0
    ' to require the dominant side to be proportionally larger before signalling.
    Public Shared Sub CalcLiquidations(trades As List(Of TradeRecord),
                                        ByRef liqLongSize As Double,
                                        ByRef liqShortSize As Double,
                                        ByRef liqSignal As String,
                                        Optional dominanceRatio As Double = 1.0)
        liqLongSize = 0 : liqShortSize = 0 : liqSignal = "NONE"
        If trades Is Nothing OrElse trades.Count = 0 Then Return
        For Each t In trades
            If t.Liquidation <> "none" Then
                If t.Direction = "buy" Then
                    liqShortSize += t.Amount
                Else
                    liqLongSize += t.Amount
                End If
            End If
        Next
        If liqLongSize > 0 AndAlso liqLongSize >= liqShortSize * dominanceRatio Then
            liqSignal = "LONG LIQS"
        ElseIf liqShortSize > 0 AndAlso liqShortSize > liqLongSize * dominanceRatio Then
            liqSignal = "SHORT LIQS"
        Else
            liqSignal = "NONE"
        End If
    End Sub

    ' -- CVD (Cumulative Volume Delta) ----------------------------------------
    ' [P3] v0.47: Replace half-split with 3-segment weighted slope.
    ' Input contract: trades chronological ascending (oldest first), so the
    ' positional thirds are chronologically truthful — early (oldest third),
    ' mid, late (most recent third).
    ' Weighted slope = (lateDelta * 2 - earlyDelta * 1) / weightedDenom.
    ' Late segment carries 2x weight so the slope emphasises the most recent
    ' flow and a single large trade in early does not dominate the signal,
    ' reducing false RISING/FALLING flips.
    Public Shared Sub CalcCVD(trades As List(Of TradeRecord), candles As List(Of Candle),
                               ByRef cvdValue As Double, ByRef cvdSlope As String,
                               ByRef cvdDivergence As String,
                               Optional slopeMinUsd As Double = 50000,
                               Optional slopePctOfValue As Double = 0.05,
                               Optional divergencePriceGate As Double = 0.002,
                               Optional lateSegmentWeight As Double = 2.0,
                               Optional earlySegmentWeight As Double = 1.0,
                               Optional ByRef weightedSlopeOut As Double = 0)
        cvdValue = 0 : cvdSlope = "FLAT" : cvdDivergence = "NONE" : weightedSlopeOut = 0
        If trades Is Nothing OrElse trades.Count = 0 Then Return

        Dim count   As Integer = trades.Count
        Dim segSize As Integer = Math.Max(1, count \ 3)

        Dim earlyDelta As Double = 0
        Dim midDelta   As Double = 0
        Dim lateDelta  As Double = 0

        For i As Integer = 0 To count - 1
            Dim usdDelta As Double = If(trades(i).Direction = "buy", trades(i).Amount, -trades(i).Amount)
            If i < segSize Then
                earlyDelta += usdDelta
            ElseIf i < segSize * 2 Then
                midDelta += usdDelta
            Else
                lateDelta += usdDelta
            End If
        Next

        cvdValue = earlyDelta + midDelta + lateDelta

        Dim weightedSlope As Double = lateDelta * lateSegmentWeight - earlyDelta * earlySegmentWeight
        weightedSlopeOut = weightedSlope   ' surface for CSV v0.7 calibration logging
        Dim absValue As Double = Math.Abs(cvdValue)
        Dim slopeThreshold As Double = Math.Max(slopeMinUsd, absValue * slopePctOfValue)

        If weightedSlope > slopeThreshold Then
            cvdSlope = "RISING"
        ElseIf weightedSlope < -slopeThreshold Then
            cvdSlope = "FALLING"
        Else
            cvdSlope = "FLAT"
        End If

        If candles.Count < 2 Then Return
        Dim priceChange As Double = (candles.Last().Close - candles(candles.Count - 2).Close) /
                                     candles(candles.Count - 2).Close
        If Math.Abs(priceChange) < divergencePriceGate Then Return
        If priceChange > 0 AndAlso cvdValue < 0 Then
            cvdDivergence = "BEARISH"
        ElseIf priceChange < 0 AndAlso cvdValue > 0 Then
            cvdDivergence = "BULLISH"
        End If
    End Sub

    ' -- TFI (Trade Flow Index) -----------------------------------------------
    ' [P4] v0.48: tfiWindowSize now a dedicated parameter (default 30), separate
    ' from MicroCVD's microWindowSize (default 50).  TFI measures short-burst
    ' aggressor pressure; a smaller window (20-30 trades) is more responsive and
    ' appropriate for 1m scalping.  MicroCVD needs a wider window to segment
    ' meaningfully into thirds.  Renamed param: windowSize -> tfiWindowSize.
    Public Shared Sub CalcTFI(trades As List(Of TradeRecord),
                               ByRef tfiValue As Double,
                               ByRef tfiSignal As String,
                               Optional tfiWindowSize As Integer = 30,
                               Optional threshold As Double = 0.15)
        tfiValue = 0.0 : tfiSignal = "NEUTRAL"
        If trades Is Nothing OrElse trades.Count = 0 Then Return

        Dim window = LastN(trades, tfiWindowSize)   ' most recent trades (list is ascending)
        Dim buyFlow  As Double = 0
        Dim sellFlow As Double = 0
        For Each t In window
            If t.Direction = "buy" Then
                buyFlow += t.Amount
            Else
                sellFlow += t.Amount
            End If
        Next

        Dim total As Double = buyFlow + sellFlow
        If total = 0 Then Return

        tfiValue = (buyFlow - sellFlow) / total

        If tfiValue > threshold Then
            tfiSignal = "BUY PRESSURE"
        ElseIf tfiValue < -threshold Then
            tfiSignal = "SELL PRESSURE"
        Else
            tfiSignal = "NEUTRAL"
        End If
    End Sub

    ' -- MicroCVD (Intra-window CVD segmentation) -----------------------------
    ' [P4] v0.48: Optional param renamed windowSize -> microWindowSize for
    ' clarity at call sites where both CalcTFI and CalcMicroCVD are invoked.
    ' Default remains 50 trades -- wide enough for meaningful thirds.
    ' Window = most recent microWindowSize trades (LastN on the ascending list);
    ' within the window, early/mid/late thirds are chronological — index 0 is
    ' the oldest trade of the window, so microLate vs microEarly compares the
    ' newest flow against the oldest. Early/Mid/Late are net USD deltas;
    ' negative values are valid (net sell pressure in that segment).
    Public Shared Sub CalcMicroCVD(trades As List(Of TradeRecord),
                                    ByRef microEarly As Double,
                                    ByRef microMid As Double,
                                    ByRef microLate As Double,
                                    ByRef microMomentum As String,
                                    ByRef microSignal As String,
                                    Optional microWindowSize  As Integer = 50,
                                    Optional accelThreshold   As Double  = 10000,
                                    Optional dynamicPct       As Double  = 0.0,
                                    Optional floorPct         As Double  = 0.25)
        microEarly = 0 : microMid = 0 : microLate = 0
        microMomentum = "FLAT" : microSignal = "FLAT"
        If trades Is Nothing OrElse trades.Count = 0 Then Return

        Dim window  = LastN(trades, microWindowSize)   ' most recent trades (list is ascending)
        Dim segSize As Integer = Math.Max(1, window.Count \ 3)

        For i As Integer = 0 To window.Count - 1
            Dim delta As Double = If(window(i).Direction = "buy", window(i).Amount, -window(i).Amount)
            If i < segSize Then
                microEarly += delta
            ElseIf i < segSize * 2 Then
                microMid += delta
            Else
                microLate += delta
            End If
        Next

        ' Compute effective acceleration threshold.
        ' When dynamicPct > 0, scale against total window USD flow (same window, same units).
        ' Floor prevents pathological dead-flow windows from producing nonsensically small thresholds.
        ' dynamicPct = 0 (function default) preserves the prior static-only behaviour exactly.
        Dim effThreshold As Double = accelThreshold
        If dynamicPct > 0.0 Then
            Dim totalUsd As Double = 0.0
            For Each t In window
                totalUsd += t.Amount
            Next
            Dim dyn   As Double = totalUsd * dynamicPct
            Dim floor As Double = accelThreshold * floorPct
            effThreshold = Math.Max(dyn, floor)
        End If

        Dim netDelta As Double = microEarly + microMid + microLate
        Dim isBull   As Boolean = netDelta > 0

        If isBull Then
            If microLate > 0 AndAlso microLate > microEarly + effThreshold Then
                microMomentum = "ACCELERATING"
            ElseIf microLate < 0 OrElse microLate < microEarly - effThreshold Then
                microMomentum = "DECELERATING"
            Else
                microMomentum = "FLAT"
            End If
        Else
            If microLate < 0 AndAlso microLate < microEarly - effThreshold Then
                microMomentum = "ACCELERATING"
            ElseIf microLate > 0 OrElse microLate > microEarly + effThreshold Then
                microMomentum = "DECELERATING"
            Else
                microMomentum = "FLAT"
            End If
        End If

        If isBull AndAlso microMomentum = "ACCELERATING" Then
            microSignal = "BULL_ACCEL"
        ElseIf isBull AndAlso microMomentum = "DECELERATING" Then
            microSignal = "BULL_DECEL"
        ElseIf Not isBull AndAlso microMomentum = "ACCELERATING" Then
            microSignal = "BEAR_ACCEL"
        ElseIf Not isBull AndAlso microMomentum = "DECELERATING" Then
            microSignal = "BEAR_DECEL"
        Else
            microSignal = "FLAT"
        End If
    End Sub

    ' -- FundingMomentum ------------------------------------------------------
    ' [v53] TIME-ANCHORED WINDOW (funding-momentum-time-anchored-window-proposal.md).
    ' Replaces the count-indexed window, which measured "funding moved more than T
    ' per 3 *changes*" -- a span of 3 x run cadence, so the same funding path scored
    ' differently at 30s / 60s / 180s cadence (FLAT 52% on on-close NY vs 0% on
    ' on-close London, where a single 3-min step already cleared the whole-window
    ' threshold). The anchored window measures "funding moved more than T per >= W
    ' minutes" -- identical at every cadence.
    '
    ' history: ascending timestamped samples, host-owned, appended every run via
    '          AppendFundingSample (no dedup -- anchoring is by age).
    ' anchor : the NEWEST sample with age >= MomentumWindowMinutes (W). Newest, not
    '          oldest-in-window: the oldest would re-import cadence dependence via
    '          the ring's total span.
    ' delta  = current - anchor.Rate; > T RISING, < -T FALLING, else FLAT.
    ' Cold start / post-gap (no sample >= W old) returns FLAT -- the same warm-up
    ' posture the count window had.
    Public Shared Function CalcFundingMomentum(
        history  As List(Of (UtcMs As Long, Rate As Double)),
        nowUtcMs As Long,
        cfg      As EngineSettings) As String

        If history Is Nothing OrElse history.Count < 2 Then Return "FLAT"

        Dim windowMs As Long   = CLng(cfg.Indicators.Funding.MomentumWindowMinutes * 60_000.0)
        Dim current  As Double = history(history.Count - 1).Rate

        ' Walk back from the newest sample; the first one old enough IS the newest >= W.
        Dim anchorIdx As Integer = -1
        For i As Integer = history.Count - 1 To 0 Step -1
            If nowUtcMs - history(i).UtcMs >= windowMs Then
                anchorIdx = i
                Exit For
            End If
        Next
        If anchorIdx < 0 Then Return "FLAT"

        Dim delta As Double = current - history(anchorIdx).Rate

        If delta >  cfg.Indicators.Funding.MomentumThreshold Then Return "RISING"
        If delta < -cfg.Indicators.Funding.MomentumThreshold Then Return "FALLING"
        Return "FLAT"
    End Function

    ' [v53] Funding sample ring maintenance -- append-every-run + age eviction.
    ' Host-agnostic and pure over the passed list so the shipped eviction rule is what
    ' the acceptance fixtures exercise (the ring itself stays host-owned; it moves with
    ' the rest of the run state at the W4 extraction).
    ' No dedup: the count window deduped on value change ([S9]) because identical samples
    ' filled the ring and forced FLAT. Anchoring by age makes repeats harmless -- and
    ' informative, since "funding hasn't moved in W minutes" IS FLAT.
    ' maxAgeMs default 30 min = the audit's segment-reset horizon (<= 60 entries at the
    ' fastest cadence the engine has ever run).
    Public Const FundingRingMaxAgeMs As Long = 30L * 60L * 1000L

    Public Shared Sub AppendFundingSample(
        history  As List(Of (UtcMs As Long, Rate As Double)),
        nowUtcMs As Long,
        rate     As Double,
        Optional maxAgeMs As Long = FundingRingMaxAgeMs)

        If history Is Nothing Then Return
        history.Add((nowUtcMs, rate))
        history.RemoveAll(Function(s) nowUtcMs - s.UtcMs > maxAgeMs)
    End Sub

    ''' <summary>
    ''' Derives OFI momentum from a rolling history of OFI ratio samples.
    ''' Returns "RISING", "FALLING", or "FLAT".
    ''' Cold start (fewer than 2 samples) returns "FLAT".
    ''' </summary>
    Public Shared Function CalcOFIMomentum(
        history As List(Of Double),
        cfg     As EngineSettings) As String

        If history Is Nothing OrElse history.Count < 2 Then Return "FLAT"

        Dim window   As Integer = cfg.Indicators.OFI.MomentumWindow
        Dim priorIdx As Integer = Math.Max(0, history.Count - 1 - window)
        Dim delta    As Double  = history(history.Count - 1) - history(priorIdx)

        If delta >  cfg.Indicators.OFI.MomentumThreshold Then Return "RISING"
        If delta < -cfg.Indicators.OFI.MomentumThreshold Then Return "FALLING"
        Return "FLAT"
    End Function

    ''' <summary>
    ''' Computes basis-point spread from the best bid/ask of the order book snapshot.
    ''' Classifies as TIGHT / NORMAL / WIDE against configurable thresholds.
    ''' </summary>
    Public Shared Sub CalcSpread(orderBook As OrderBookSnapshot,
                                  ByRef spreadBps As Double,
                                  ByRef spreadStatus As String,
                                  Optional wideThresholdBps  As Double = 5.0,
                                  Optional tightThresholdBps As Double = 1.5)
        spreadBps = 0 : spreadStatus = "NORMAL"
        If orderBook Is Nothing Then Return
        If orderBook.Bids Is Nothing OrElse orderBook.Bids.Count = 0 Then Return
        If orderBook.Asks Is Nothing OrElse orderBook.Asks.Count = 0 Then Return

        Dim bestBid As Double = orderBook.Bids(0).Price
        Dim bestAsk As Double = orderBook.Asks(0).Price
        If bestBid <= 0 OrElse bestAsk <= 0 Then Return
        Dim mid As Double = (bestBid + bestAsk) / 2.0
        If mid <= 0 Then Return

        spreadBps = ((bestAsk - bestBid) / mid) * 10000.0

        If spreadBps >= wideThresholdBps Then
            spreadStatus = "WIDE"
        ElseIf spreadBps <= tightThresholdBps Then
            spreadStatus = "TIGHT"
        Else
            spreadStatus = "NORMAL"
        End If
    End Sub

End Class
