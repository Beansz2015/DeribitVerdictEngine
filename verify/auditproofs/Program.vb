' Adversarial-audit proof harness (2026-09-24).
' Every case below calls the SHIPPED engine sources linked by AuditProofs.vbproj
' (the same Compile list as verify/ordercheck/OrderCheck.vbproj) and the tracked
' repo-root settings.json. Nothing here edits a source file.
'
' Run all:     dotnet run -c Release --project verify/auditproofs/AuditProofs.vbproj
' Run one:     dotnet run -c Release --project verify/auditproofs/AuditProofs.vbproj -- P03
'
' Output contract: each case prints "[ID] DEFECT CONFIRMED :: ..." or
' "[ID] NOT REPRODUCED :: ..." plus indented evidence lines.

Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
Imports System.Text.Json.Nodes

Module Program

    Private ReadOnly RepoRoot As String = FindRepoRoot()

    ''' <summary>Walks up from the build output to the directory holding the solution and the
    ''' tracked settings.json, so the harness runs from any clone on any OS.</summary>
    Private Function FindRepoRoot() As String
        Dim d As New DirectoryInfo(AppContext.BaseDirectory)
        While d IsNot Nothing
            If File.Exists(Path.Combine(d.FullName, "DeribitVerdictEngine.sln")) AndAlso
               File.Exists(Path.Combine(d.FullName, "settings.json")) Then Return d.FullName
            d = d.Parent
        End While
        Throw New InvalidOperationException("repo root not found above " & AppContext.BaseDirectory)
    End Function
    Private _confirmed As Integer = 0
    Private _notRepro As Integer = 0
    Private ReadOnly JsonOpts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

    Sub Main(args As String())
        Dim only As String = If(args.Length > 0, args(0), "")
        Console.WriteLine("=== Adversarial audit proofs — shipped sources, tracked settings.json v" &
                          LoadShippedCfg().Version & " ===")
        RunCase("TRACE", AddressOf Trace_HighVolEvent, only)
        RunCase("P01", AddressOf P01_MtfFailOpen, only)
        RunCase("P02", AddressOf P02_MtfEmaBearUnreachableOnShortSeries, only)
        RunCase("P03", AddressOf P03_MinMoveGateIgnoresRiskReward, only)
        RunCase("P04", AddressOf P04_KellyEdges, only)
        RunCase("P05", AddressOf P05_TweakerAcceptsGeometryInversion, only)
        RunCase("P06", AddressOf P06_OfiAccumulatorWeightsArrivingSample, only)
        RunCase("P07", AddressOf P07_VwapFallbackDefeatsWarmup, only)
        RunCase("P08", AddressOf P08_WsTradesServedWithoutAgeGate, only)
        RunCase("P09", AddressOf P09_ReconnectSeedFailureLeavesHole, only)
        RunCase("P10", AddressOf P10_PayloadCarriesNoDataAge, only)
        RunCase("P11", AddressOf P11_KellyNotTiedToPlacedStop, only)
        RunCase("P12", AddressOf P12_GetBookReturnsLiveReference, only)
        RunCase("P13", AddressOf P13_SwingPivotRepaintsOnFormingBar, only)
        RunCase("P15", AddressOf P15_LevelsNotOnTickGrid, only)
        RunCase("P16", AddressOf P16_StopFloorInsideConsumerSlippageCap, only)
        RunCase("P17", AddressOf P17_ThresholdCeilingFragility, only)
        RunCase("P18", AddressOf P18_ApplyThrowsOnNet8, only)
        ' P14 LAST: it leaves LivePerformanceTracker's static init task pending by design.
        RunCase("P14", AddressOf P14_PerfDisplayDisabledDeadlocksRun, only)
        Console.WriteLine()
        Console.WriteLine(String.Format("=== {0} defect(s) confirmed, {1} not reproduced ===", _confirmed, _notRepro))
    End Sub

    ' ------------------------------------------------------------------ plumbing

    Sub RunCase(id As String, body As Action, only As String)
        If only <> "" AndAlso Not id.Equals(only, StringComparison.OrdinalIgnoreCase) Then Return
        Console.WriteLine()
        Console.WriteLine("---- " & id & " ----")
        Try
            body()
        Catch ex As Exception
            Console.WriteLine(String.Format("[{0}] HARNESS EXCEPTION: {1}: {2}", id, ex.GetType().Name, ex.Message))
            _notRepro += 1
        End Try
    End Sub

    Function LoadShippedCfg() As EngineSettings
        Return JsonSerializer.Deserialize(Of EngineSettings)(
            File.ReadAllText(Path.Combine(RepoRoot, "settings.json")), JsonOpts)
    End Function

    Sub AuMark(id As String, confirmed As Boolean, detail As String)
        Console.WriteLine(String.Format("[{0}] {1} :: {2}", id,
                          If(confirmed, "DEFECT CONFIRMED", "NOT REPRODUCED"), detail))
        If confirmed Then _confirmed += 1 Else _notRepro += 1
    End Sub

    Sub AuInfo(s As String)
        Console.WriteLine("    " & s)
    End Sub

    Function AuBar(tsMs As Long, o As Double, h As Double, l As Double, c As Double,
                 Optional v As Double = 10.0) As Candle
        Dim k As New Candle()
        k.Timestamp = tsMs : k.[Open] = o : k.High = h : k.Low = l : k.Close = c
        k.Volume = v : k.VolumeUSD = v * c
        Return k
    End Function

    Function AuMs(dt As DateTime) As Long
        Return New DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
    End Function

    Function AuFloorToMinutes(dt As DateTime, minutes As Integer) As DateTime
        Dim t As New DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc)
        Return t.AddMinutes(-(t.Minute Mod minutes))
    End Function

    Function AuNorms() As DynamicNorms
        Return New DynamicNorms() With {.VolHighThreshold = 3.0, .VolMidThreshold = 2.0,
                                        .VWAPDevThreshold = 0.3, .ATRScaleFactor = 1.0,
                                        .ATRRef = 38.0, .IsLive = True}
    End Function

    ''' <summary>A fully-voted long setup (every Step-2 long vote true, CONFIRMED context).
    ''' Swing levels are the only geometry inputs; everything else is held constant.</summary>
    Function DirectionalLongR(price As Double, atr As Double,
                              swingHigh As Double, swingLow As Double) As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = price : r.ATR = atr
        r.ExecResolution = 1 : r.SessionUtcHour = 15          ' NY, 1-min execution
        r.ROC = 0.3 : r.ROCSlope = "RISING"
        r.RSI = 62 : r.RSIDivergence = "NONE"
        r.PlusDI = 32 : r.MinusDI = 12 : r.ADX = 30 : r.Regime = "TRENDING_UP"
        r.VolumeRatio = 1.0
        r.VWAP = price - 10 : r.VWAPSessionCandles = 120
        r.VWAPSigma1Upper = price + 60 : r.VWAPSigma1Lower = price - 80
        r.VWAPSigma2Upper = price + 130 : r.VWAPSigma2Lower = price - 150
        r.SqueezeStatus = "NONE" : r.TTMSignal = "BULL_BUILDING" : r.TTMDirection = "RISING"
        r.EMAAlignment = "BULL"
        r.FundingRate = 0.00001 : r.FundingBias = "NEUTRAL" : r.FundingMomentum = "FLAT"
        r.OISignal = "NEW LONGS"
        r.OFISignal = "BUY DOMINANT" : r.OFIMomentum = "FLAT" : r.OFIRatio = 2.0
        r.SpreadStatus = "NORMAL" : r.SpreadBps = 0.1
        r.LiqSignal = "NONE"
        r.EMA200_5m = price - 500 : r.PriceVsEMA200 = "ABOVE"
        r.CVDValue = 80000 : r.CVDSlope = "RISING" : r.CVDDivergence = "NONE"
        r.TFIValue = 0.4 : r.TFISignal = "BUY PRESSURE"
        r.MicroCVDEarly = 0 : r.MicroCVDMid = 10000 : r.MicroCVDLate = 30000
        r.MicroCVDSignal = "BULL_ACCEL" : r.MicroCVDMomentum = "ACCELERATING"
        r.MTFGatePassLong = True : r.MTFGatePassShort = True
        r.MTF15mTrend = "BULL" : r.MTFGateDetails = "(synthetic)"
        r.DonchianSignal = "LONG" : r.OBVTrend = "RISING" : r.OBVDivergence = "NONE"
        r.VPFRSignal = "NEUTRAL" : r.VPFRValueAreaSignal = "INSIDE_VA"
        r.LastSwingHigh5m = swingHigh : r.LastSwingLow5m = swingLow
        r.SwingTargetLong = If(swingHigh > price, swingHigh, 0)
        r.SwingStopLong = If(swingLow < price AndAlso swingLow > 0, swingLow, 0)
        r.SwingTargetShort = If(swingLow < price AndAlso swingLow > 0, swingLow, 0)
        r.SwingStopShort = If(swingHigh > price, swingHigh, 0)
        r.TrendStructure = TrendStructure.UPTREND
        Return r
    End Function

    Function AuEmit(v As VerdictResult, r As IndicatorResults, cfg As EngineSettings,
                  Optional generatedAt As DateTime? = Nothing) As JsonObject
        Return SignalEmitter.BuildOk(v, r, cfg, "audit-proof", 42, True, "OK", False,
                                     If(generatedAt.HasValue, generatedAt.Value, DateTime.UtcNow))
    End Function

    Function AuNum(n As JsonNode) As Double
        Return n.GetValue(Of Double)()
    End Function

    ' ------------------------------------------------------------------ TRACE

    ''' <summary>Numbers for the report's LOGIC TRACE: a calm NY 1-min tape, a 3-bar
    ''' -1.5 % liquidation flush, then the on_close run firing on a seconds-old stub bar.</summary>
    Sub Trace_HighVolEvent()
        Dim cfg = LoadShippedCfg()
        Dim t0 As DateTime = New DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc)
        Dim bars As New List(Of Candle)()
        Dim px As Double = 60000
        Dim rng As New Random(7)
        For i As Integer = 0 To 239
            Dim o As Double = px
            Dim c As Double = Math.Round((px + (rng.NextDouble() - 0.5) * 24) * 2) / 2
            Dim h As Double = Math.Max(o, c) + 8 + Math.Round(rng.NextDouble() * 8)
            Dim l As Double = Math.Min(o, c) - 8 - Math.Round(rng.NextDouble() * 8)
            bars.Add(AuBar(AuMs(t0.AddMinutes(i)), o, h, l, c, 20 + rng.NextDouble() * 10))
            px = c
        Next
        Dim preFlushPrice As Double = px
        Dim flush() As Double = {-300, -350, -250}
        For j As Integer = 0 To flush.Length - 1
            Dim o As Double = px
            Dim c As Double = px + flush(j)
            bars.Add(AuBar(AuMs(t0.AddMinutes(240 + j)), o, o + 10, c - 20, c, 400))
            px = c
        Next
        Dim closed As New List(Of Candle)(bars)
        Dim stubOpen As DateTime = t0.AddMinutes(240 + flush.Length)
        Dim withStub As New List(Of Candle)(bars)
        withStub.Add(AuBar(AuMs(stubOpen), px, px + 1.5, px, px + 1.5, 3))   ' 2-second-old forming bar

        Dim atrClosed As Double = IndicatorEngine.CalcATR(closed, cfg.Indicators.ATR.Period)
        Dim atrStub As Double = IndicatorEngine.CalcATR(withStub, cfg.Indicators.ATR.Period)
        Dim atrPre As Double = IndicatorEngine.CalcATR(bars.Take(240).ToList(), cfg.Indicators.ATR.Period)
        AuInfo(String.Format("pre-flush price {0:F1}, ATR(7) {1:F1}  ({2:F1} bps)", preFlushPrice, atrPre, atrPre / preFlushPrice * 10000))
        AuInfo(String.Format("post-flush price {0:F1}; ATR(7) on CLOSED bars {1:F1}; ATR(7) as the on_close run computes it (stub last bar) {2:F1}  => {3:P1} low",
                           px + 1.5, atrClosed, atrStub, 1 - atrStub / atrClosed))

        ' 5m aggregation for swings (the run uses 5m pivots for placed levels)
        Dim five As New List(Of Candle)()
        For k As Integer = 0 To withStub.Count - 1 Step 5
            Dim grp = withStub.Skip(k).Take(5).ToList()
            five.Add(AuBar(grp(0).Timestamp, grp(0).[Open], grp.Max(Function(b) b.High),
                         grp.Min(Function(b) b.Low), grp.Last().Close, grp.Sum(Function(b) b.Volume)))
        Next
        Dim sh As Double, sl As Double
        IndicatorEngine.CalcSwingPivots(five, sh, sl, cfg.Indicators.Swing.PivotWing5m, cfg.Indicators.Swing.LookbackBars5m)

        Dim r As IndicatorResults = DirectionalLongR(px + 1.5, atrStub, sh, sl)
        ' VPFR from the real function on the exec candles
        Dim poc As Double, near As Boolean, sig As String = "", vah As Double, val As Double, vas As String = ""
        Dim ha As Double, hb As Double, la As Double, lb As Double
        Dim bv() As Double = Nothing, bl As Double, bs As Double
        Dim vp = cfg.Indicators.VPFR
        IndicatorEngine.CalcVPFRLite(withStub, r.CurrentPrice, poc, near, sig, vah, val, vas, ha, hb, la, lb,
                                     bv, bl, bs, vp.NumBuckets, vp.HvnVolPct, vp.LvnVolPct, vp.HvnProximityPct,
                                     vp.DecayBase, vp.ValueAreaPct)
        r.VPFRPoc = poc : r.VPFRSignal = sig : r.VPFRNearestHvnAbove = ha : r.VPFRNearestHvnBelow = hb
        Dim v As New VerdictResult()
        Dim lvL = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim lvS = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
        Dim floorUsd As Double = cfg.Scoring.TradeCosts.EffectiveMinMovePct * r.CurrentPrice
        AuInfo(String.Format("5m swings: high {0:F1} low {1:F1} (both now ABOVE price after the flush); VPFR {2} POC {3:F1} HVN^ {4:F1} HVNv {5:F1}",
                           sh, sl, sig, poc, ha, hb))
        AuInfo(String.Format("LONG  levels: entry {0:F1} stop {1:F2} [{2}] target {3:F2} [{4}]  risk {5:F1} / reward {6:F1}",
                           lvL.Entry, lvL.StopPx, lvL.StopReason, lvL.Target, lvL.TargetReason,
                           lvL.Entry - lvL.StopPx, lvL.Target - lvL.Entry))
        AuInfo(String.Format("SHORT levels: entry {0:F1} stop {1:F2} [{2}] target {3:F2} [{4}]  risk {5:F1} / reward {6:F1}",
                           lvS.Entry, lvS.StopPx, lvS.StopReason, lvS.Target, lvS.TargetReason,
                           lvS.StopPx - lvS.Entry, lvS.Entry - lvS.Target))
        AuInfo(String.Format("min-move floor {0:F1} USD ({1:F1} bps); with CLOSED-bar ATR the stops would be {2:F1} away, not {3:F1}",
                           floorUsd, cfg.Scoring.TradeCosts.EffectiveMinMovePct * 10000,
                           atrClosed * cfg.Scoring.AtrStopMultiplier, atrStub * cfg.Scoring.AtrStopMultiplier))
        Dim kv As New VerdictResult With {.Verdict = "SHORT", .Confidence = "MEDIUM"}
        ScoringEngine.CalcKellySizing(kv, r.ATR * cfg.Scoring.AtrStopMultiplier, r.CurrentPrice, cfg)
        AuInfo(String.Format("Kelly (MEDIUM, as the snapshot calls it): f*={0:F3} applied={1:P1} risk ${2:F2} contracts {3} (lev-capped {4}); notional ${5:N0}",
                           kv.KellyF, kv.KellyFApplied, kv.KellyRiskUsd, kv.KellyContracts, kv.KellyLevCapped,
                           kv.KellyContracts * cfg.Kelly.ContractFaceUsd))
    End Sub

    ' ------------------------------------------------------------------ P01

    Sub P01_MtfFailOpen()
        Dim cfg = LoadShippedCfg()
        Dim g = cfg.MTFGate
        Dim trend As String = "", ema As String = "", det As String = ""
        Dim adx As Double, pl As Boolean, ps As Boolean
        IndicatorEngine.CalcMTFGate(Nothing, trend, adx, ema, pl, ps, det,
                                    g.DmiPeriod, g.AdxMin, g.RequiredConfirms, g.CandleCount)
        AuInfo(String.Format("candles15m = Nothing -> trend {0}, passLong {1}, passShort {2}, '{3}'", trend, pl, ps, det))
        Dim few As New List(Of Candle)()
        Dim t0 As New DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc)
        For i As Integer = 0 To 9                     ' a crash: every 15m bar -300
            Dim o As Double = 60000 - 300 * i
            few.Add(AuBar(AuMs(t0.AddMinutes(15 * i)), o, o + 20, o - 320, o - 300))
        Next
        Dim pl2 As Boolean, ps2 As Boolean
        IndicatorEngine.CalcMTFGate(few, trend, adx, ema, pl2, ps2, det,
                                    g.DmiPeriod, g.AdxMin, g.RequiredConfirms, g.CandleCount)
        AuInfo(String.Format("10 x 15m bars of a -3,000 USD crash -> trend {0}, passLong {1}, passShort {2}, '{3}'", trend, pl2, ps2, det))
        AuMark("P01", pl AndAlso ps AndAlso pl2,
             "the 15m hard veto FAILS OPEN on missing/short data: a LONG passes the gate during a 15m crash when fewer than adx_period+2 bars are held")
    End Sub

    ' ------------------------------------------------------------------ P02

    Function AuChop15m(n As Integer, slope As Double) As List(Of Candle)
        Dim list As New List(Of Candle)()
        Dim t0 As New DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc)
        Dim prevClose As Double = 60000
        For i As Integer = 0 To n - 1
            Dim c As Double = 60000 + slope * i + If(i Mod 2 = 0, 60.0, -60.0)
            list.Add(AuBar(AuMs(t0.AddMinutes(15 * i)), prevClose, c + 30, c - 30, c))
            prevClose = c
        Next
        Return list
    End Function

    Sub P02_MtfEmaBearUnreachableOnShortSeries()
        Dim cfg = LoadShippedCfg()
        Dim g = cfg.MTFGate
        Dim rows As New List(Of String)()
        Dim upShort As String = "", dnShort As String = "", dnLong As String = ""
        ' Bar counts chosen so the LAST bar's parity favours the trend's own DM side in both
        ' mirrors (the alternating construction otherwise lets parity, not drift, pick DI).
        For Each spec In New (n As Integer, slope As Double)() {(31, 6.0), (30, -6.0), (61, 6.0), (60, -6.0)}
            Dim trend As String = "", ema As String = "", det As String = ""
            Dim adx As Double, pl As Boolean, ps As Boolean
            IndicatorEngine.CalcMTFGate(AuChop15m(spec.n, spec.slope), trend, adx, ema, pl, ps, det,
                                        g.DmiPeriod, g.AdxMin, g.RequiredConfirms, g.CandleCount)
            AuInfo(String.Format("{0,2} bars slope {1,5:+0.0;-0.0}/bar -> EMA {2,-5} trend {3,-4} passLong {4,-5} passShort {5,-5} | {6}",
                               spec.n, spec.slope, ema, trend, pl, ps, det))
            If spec.n = 31 AndAlso spec.slope > 0 Then upShort = trend
            If spec.n = 30 AndAlso spec.slope < 0 Then dnShort = trend
            If spec.n = 60 AndAlso spec.slope < 0 Then dnLong = trend
        Next
        AuMark("P02", upShort = "BULL" AndAlso dnShort = "FLAT" AndAlso dnLong = "BEAR",
             "with 21..49 15m bars CalcEMA(window,50) returns 0, so emaBear (ema21 < 0) is impossible: the mirror-image downtrend reads FLAT (passes longs) while the uptrend reads BULL (blocks shorts)")
    End Sub

    ' ------------------------------------------------------------------ P03

    Sub P03_MinMoveGateIgnoresRiskReward()
        Dim cfg = LoadShippedCfg()
        Dim price As Double = 60000
        Dim tc = cfg.Scoring.TradeCosts
        Dim floorUsd As Double = tc.EffectiveMinMovePct * price
        Dim worstNetBe As Double = 0
        Dim allDirectional As Boolean = True
        AuInfo(String.Format("floor = {0:F1} USD ({1:F1} bps). Swing target placed floor+1 USD above entry; swing low 5 ATR below (clamps).", floorUsd, tc.EffectiveMinMovePct * 10000))
        AuInfo("  ATR   verdict       ctx          stop[reason]            target    R:R   grossBE  netBE(maker in/TP, taker SL)")
        For Each atr In New Double() {30, 60, 100, 200}
            Dim r = DirectionalLongR(price, atr, price + floorUsd + 1, price - 5 * atr)
            Dim v = ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg)
            Dim p = AuEmit(v, r, cfg)
            Dim stopPx As Double = AuNum(p("levels")("long")("stop"))
            Dim tgtPx As Double = AuNum(p("levels")("long")("target"))
            Dim lv = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
            Dim risk As Double = price - stopPx, rwd As Double = tgtPx - price
            Dim winNet As Double = rwd - price * 2 * tc.MakerFeeBps / 10000
            Dim lossNet As Double = risk + price * (tc.MakerFeeBps + tc.TakerFeeBps) / 10000
            Dim grossBe As Double = risk / (risk + rwd)
            Dim netBe As Double = lossNet / (winNet + lossNet)
            worstNetBe = Math.Max(worstNetBe, netBe)
            allDirectional = allDirectional AndAlso p("direction").GetValue(Of String)() = "LONG"
            AuInfo(String.Format("  {0,4:F0}  {1,-12}  {2,-11}  {3,9:F1} [{4,-12}]  {5,9:F1}  {6,4:F2}  {7,6:P1}  {8,6:P1}",
                               atr, v.Verdict, v.VerdictContext, stopPx, lv.StopReason, tgtPx, rwd / risk, grossBe, netBe))
        Next
        AuMark("P03", allDirectional AndAlso worstNetBe > 0.9,
             String.Format("Step 5c gates only |target-entry| >= floor; no R:R / stop / net-EV floor exists. At ATR 200 the emitted STRONG LONG needs a {0:P1} win rate to break even after fees", worstNetBe))
    End Sub

    ' ------------------------------------------------------------------ P04

    Sub P04_KellyEdges()
        Dim cfg = LoadShippedCfg()
        ' (a) guard placed after nine dereferences
        Try
            ScoringEngine.CalcKellySizing(Nothing, 100, 60000, cfg)
            AuMark("P04a", False, "CalcKellySizing(Nothing, ...) returned normally")
        Catch ex As NullReferenceException
            AuMark("P04a", True, "CalcKellySizing(Nothing, ...) throws NullReferenceException at line 36 — its 'If v Is Nothing' guard (line 46) is dead code")
        End Try

        ' (b) max_leverage = 0 silently DISABLES the leverage cap
        Dim va As New VerdictResult With {.Verdict = "LONG", .Confidence = "MEDIUM"}
        ScoringEngine.CalcKellySizing(va, 96, 60000, cfg)
        cfg.Kelly.MaxLeverage = 0
        Dim vb As New VerdictResult With {.Verdict = "LONG", .Confidence = "MEDIUM"}
        ScoringEngine.CalcKellySizing(vb, 96, 60000, cfg)
        AuMark("P04b", vb.KellyContracts > va.KellyContracts,
             String.Format("stop 96 USD @60k: max_leverage 5 -> {0} contracts (${1:N0}); max_leverage 0 -> {2} contracts (${3:N0} = {4:F0}x a ${5:N0} account)",
                           va.KellyContracts, va.KellyContracts * 10, vb.KellyContracts, vb.KellyContracts * 10,
                           vb.KellyContracts * 10 / cfg.Kelly.AccountSizeUsd, cfg.Kelly.AccountSizeUsd))

        ' (c) CInt overflow on a degenerate stop distance
        cfg = LoadShippedCfg()
        Dim vc As New VerdictResult With {.Verdict = "LONG", .Confidence = "MEDIUM"}
        Try
            ScoringEngine.CalcKellySizing(vc, 0.00000001, 60000, cfg)
            AuMark("P04c", False, "no exception; contracts=" & vc.KellyContracts)
        Catch ex As OverflowException
            AuMark("P04c", True, "stop distance 1e-8 -> OverflowException from CInt(Math.Floor(riskUsd / riskPerContract)) BEFORE the leverage cap is applied (latent: shipped min-move floor keeps MEDIUM/HIGH off this path)")
        End Try

        ' (d) payoff ratio b is the GLOBAL 1.75/1.6, not the session's placed fallback
        Dim vNy As New VerdictResult With {.Verdict = "LONG", .Confidence = "MEDIUM"}
        ScoringEngine.CalcKellySizing(vNy, 96, 60000, cfg)
        Dim asiaMult As Double = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, 3)
        Dim cfgAsia = LoadShippedCfg() : cfgAsia.Scoring.AtrTargetMultiplier = asiaMult
        Dim vAsia As New VerdictResult With {.Verdict = "LONG", .Confidence = "MEDIUM"}
        ScoringEngine.CalcKellySizing(vAsia, 96, 60000, cfgAsia)
        AuMark("P04d", vNy.KellyF > 0 AndAlso vAsia.KellyF <= 0,
             String.Format("ASIA fallback target is {0:F2} x ATR (placed), yet Kelly uses b = {1:F2}/{2:F2}: f* shown {3:F3} (sizes {4} contracts) vs f* at the ASIA geometry {5:F3} (no edge)",
                           asiaMult, cfg.Scoring.AtrTargetMultiplier, cfg.Scoring.AtrStopMultiplier, vNy.KellyF, vNy.KellyContracts, vAsia.KellyF))

        ' (e) fee drag flips the sign at a typical NY ATR (arithmetic on the shipped fee keys)
        Dim tc = cfg.Scoring.TradeCosts
        Dim atr As Double = 60, price As Double = 60000
        Dim rwdBps As Double = atr * cfg.Scoring.AtrTargetMultiplier / price * 10000
        Dim riskBps As Double = atr * cfg.Scoring.AtrStopMultiplier / price * 10000
        Dim bNet As Double = (rwdBps - 2 * tc.MakerFeeBps) / (riskBps + tc.MakerFeeBps + tc.TakerFeeBps)
        Dim p As Double = cfg.Kelly.EstProbFloor + cfg.Kelly.EstProbScale / 2
        Dim fNet As Double = (bNet * p - (1 - p)) / bNet
        AuMark("P04e", fNet < 0,
             String.Format("ATR {0} @ {1:N0}: gross b {2:F3} -> f* {3:F3}; net-of-fee b {4:F3} -> f* {5:F3}. Kelly has no fee term, so it sizes a negative-edge MEDIUM at the 5% cap",
                           atr, price, cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier, vNy.KellyF, bNet, fNet))
    End Sub

    ' ------------------------------------------------------------------ P05

    Function AuDiff(path As String, oldRaw As String, newRaw As String) As DiffItem
        Return New DiffItem With {
            .Path = path,
            .OldValue = JsonDocument.Parse(oldRaw).RootElement.Clone(),
            .NewValue = JsonDocument.Parse(newRaw).RootElement.Clone(),
            .Justification = "audit proof"}
    End Function

    Sub P05_TweakerAcceptsGeometryInversion()
        Dim baseJson As String = File.ReadAllText(Path.Combine(RepoRoot, "settings.json"))
        Dim cases As New List(Of DiffItem) From {
            AuDiff("scoring.atr_stop_multiplier", "1.6", "-1.6"),
            AuDiff("scoring.structural_levels.stop_max_atr_mult", "1.6", "0"),
            AuDiff("mtf_gate.min_of", "2", "0"),
            AuDiff("indicators.ATR.period", "7", "0")}
        Dim allValid As Boolean = True
        For Each it In cases
            Dim res = SettingsDiffApplier.Validate(New List(Of DiffItem) From {it}, baseJson, 3)
            AuInfo(String.Format("SettingsDiffApplier.Validate({0}: {1} -> {2}) => IsValid={3} {4}",
                               it.Path, it.OldValue.GetRawText(), it.NewValue.GetRawText(), res.IsValid, res.ErrorReason))
            allValid = allValid AndAlso res.IsValid
        Next

        ' Write diff #1 the way a hand edit (or a working Apply) would, then load it through
        ' the REAL SettingsLoader — the same code path the live hot-reload runs.
        Dim dir As String = Path.Combine(Path.GetTempPath(), "audit_cfg_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(dir)
        Dim tmp As String = Path.Combine(dir, "settings.json")
        Dim root = JsonNode.Parse(baseJson)
        root("scoring")("atr_stop_multiplier") = JsonNode.Parse("-1.6")
        File.WriteAllText(tmp, root.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
        SettingsLoader.Initialise(tmp)
        Dim cfg As EngineSettings = SettingsLoader.Current
        AuInfo(String.Format("SettingsLoader.Initialise(edited file): LastLoadError='{0}', Current.Scoring.AtrStopMultiplier={1}",
                             SettingsLoader.LastLoadError, cfg.Scoring.AtrStopMultiplier))
        Dim r = DirectionalLongR(60000, 60, 60090, 0)          ' no swing low -> FALLBACK_ATR stop
        Dim v = ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg)
        Dim p = AuEmit(v, r, cfg)
        Dim lg = p("levels")("long")
        AuInfo(String.Format("after hot-load: verdict {0}, direction {1}, levels.long entry {2} stop {3} target {4}",
                           v.Verdict, p("direction").GetValue(Of String)(), AuNum(lg("entry")), AuNum(lg("stop")), AuNum(lg("target"))))
        Dim inverted As Boolean = AuNum(lg("stop")) > AuNum(lg("entry")) AndAlso p("direction").GetValue(Of String)() = "LONG"

        ' Diff #2: stop bound 0 -> stop placed AT entry.
        Dim cfg2 = LoadShippedCfg() : cfg2.Scoring.StructuralLevels.StopMaxAtrMult = 0
        Dim r2 = DirectionalLongR(60000, 60, 60090, 59800)
        Dim lv2 = SignalEmitter.ComputeSideLevels(New VerdictResult(), r2, cfg2, isLong:=True)
        AuInfo(String.Format("stop_max_atr_mult 0: long stop {0} [{1}] vs entry {2}", lv2.StopPx, lv2.StopReason, lv2.Entry))

        ' Diff #3: min_of 0 -> a straight-line 15m crash reads BULL; shorts blocked, longs pass.
        Dim g = LoadShippedCfg().MTFGate
        Dim trend As String = "", ema As String = "", det As String = ""
        Dim adx As Double, pl As Boolean, ps As Boolean
        IndicatorEngine.CalcMTFGate(AuChop15m(60, -40), trend, adx, ema, pl, ps, det, g.DmiPeriod, g.AdxMin, 0, g.CandleCount)
        AuInfo(String.Format("min_of 0 on a 60-bar 15m downtrend: trend {0}, passLong {1}, passShort {2}", trend, pl, ps))

        ' Diff #4: period 0 -> the exception class that reaches RunAnalysisAsync's MessageBox.
        Dim threw As String = "none"
        Try
            IndicatorEngine.CalcATR(AuChop15m(60, 1), 0)
        Catch ex As Exception
            threw = ex.GetType().Name & ": " & ex.Message
        End Try
        AuInfo("indicators.ATR.period 0: CalcATR throws " & threw)

        AuMark("P05", allValid AndAlso inverted AndAlso lv2.StopPx = lv2.Entry AndAlso trend = "BULL" AndAlso Not ps AndAlso threw <> "none",
             "the tweaker's Validate fences key PATHS only; values are never range/sign-checked, and SettingsLoader hot-reloads them into the order payload unvalidated")
    End Sub

    ' ------------------------------------------------------------------ P06

    Sub P06_OfiAccumulatorWeightsArrivingSample()
        Dim cfg = LoadShippedCfg()
        Dim o = cfg.Indicators.OFI
        Dim tau As Double = o.AvgWindowSec
        Dim acc As New OfiAccumulator()
        ' Reference: the correct time-weighted EMA of a piecewise-constant signal weights the
        ' value IN FORCE during each interval — the PREVIOUS sample — not the arriving one.
        Dim refEma As Double = 0, refPrev As Double = 0, refInit As Boolean = False, refLast As Long = 0
        Dim fold As Action(Of Double, Long) =
            Sub(ratio As Double, ts As Long)
                acc.Fold(100, 100, ratio, ts, tau)
                If Not refInit Then
                    refEma = Math.Log(ratio) : refInit = True
                Else
                    Dim a As Double = 1 - Math.Exp(-Math.Max(0, ts - refLast) / 1000.0 / tau)
                    refEma += a * (refPrev - refEma)
                End If
                refPrev = Math.Log(ratio) : refLast = ts
            End Sub
        Dim t As Long = 1_000_000
        For i As Integer = 0 To 100                ' 10 s of a balanced book at 100 ms
            fold(1.0, t) : t += 100
        Next
        t += 3000                                  ' 3 s with no book change / a delayed TCP read
        fold(20.0, t)                              ' one 20:1 top-of-book (spoof) ...
        t += 100
        fold(1.0, t)                               ' ... pulled 100 ms later
        Dim codeRatio As Double = acc.Snapshot(tau).Ratio
        Dim refRatio As Double = Math.Exp(refEma)
        Dim cls As String = IndicatorEngine.ClassifyOfiRatio(codeRatio, o.BuyDominantRatio, o.SellDominantRatio)
        Dim refCls As String = IndicatorEngine.ClassifyOfiRatio(refRatio, o.BuyDominantRatio, o.SellDominantRatio)
        Dim secsDominant As Double = 0
        Dim r As Double = codeRatio
        While IndicatorEngine.ClassifyOfiRatio(r, o.BuyDominantRatio, o.SellDominantRatio) = "BUY DOMINANT"
            t += 100 : fold(1.0, t) : secsDominant += 0.1
            r = acc.Snapshot(tau).Ratio
            If secsDominant > 60 Then Exit While
        End While
        AuInfo(String.Format("after a 100 ms spoof that followed a 3 s quiet gap: shipped OfiAccumulator ratio {0:F3} -> {1}; correct piecewise-constant EMA {2:F3} -> {3}",
                           codeRatio, cls, refRatio, refCls))
        AuInfo(String.Format("the shipped average stays BUY DOMINANT for {0:F1} s of perfectly balanced book afterwards (tau {1} s)", secsDominant, tau))
        AuMark("P06", cls = "BUY DOMINANT" AndAlso refCls = "BALANCED",
             "OfiAccumulator.Fold gives the ARRIVING sample the weight of the gap before it (alpha from dt-since-previous), so a transient that follows any lull or delivery burst dominates the 'time-averaged' OFI vote")
    End Sub

    ' ------------------------------------------------------------------ P07

    Sub P07_VwapFallbackDefeatsWarmup()
        Dim cfg = LoadShippedCfg()
        Dim vw = cfg.Indicators.VWAP
        Dim list As New List(Of Candle)()
        Dim start As New DateTime(2026, 9, 24, 9, 20, 0, DateTimeKind.Utc)     ' 250 x 1m bars, last opens 13:29
        For i As Integer = 0 To 249
            Dim c As Double = 59000 + i * 4                                   ' 4-hour +1,000 USD grind
            list.Add(AuBar(AuMs(start.AddMinutes(i)), c - 2, c + 6, c - 6, c, 10))
        Next
        Dim nowUtc As DateTime = New DateTime(2026, 9, 24, vw.Session2StartHour, vw.Session2StartMinute, 0, DateTimeKind.Utc).AddMilliseconds(500)
        Dim cnt As Integer
        Dim vwap As Double = IndicatorEngine.CalcVWAP(list, cnt, vw.Session2StartHour, vw.Session2StartMinute, nowUtc)
        Dim warmup As Boolean = cnt < vw.WarmupCandles
        AuInfo(String.Format("run at {0:HH:mm:ss.fff} UTC, newest bar opened {1:HH:mm}: VWAPSessionCandles = {2}, warmup gate (< {3}) = {4}, VWAP {5:F1} vs price {6:F1}",
                           nowUtc, start.AddMinutes(249), cnt, vw.WarmupCandles, warmup, vwap, list.Last().Close))
        AuMark("P07", cnt = list.Count AndAlso Not warmup,
             "GetSessionCandles falls back to the WHOLE list when no bar is in the new session, and CalcVWAP reports that full count, so the warmup guard reads 250 and a 4-hour VWAP votes as 'session VWAP'")
    End Sub

    ' ------------------------------------------------------------------ P08

    Sub P08_WsTradesServedWithoutAgeGate()
        Dim cfg = LoadShippedCfg()
        Dim ms As New MarketState()
        Dim twoHoursAgo As DateTime = DateTime.UtcNow.AddHours(-2)
        Dim t0 As Long = AuMs(twoHoursAgo)
        Dim trades As New List(Of TradeRecord)()
        For i As Integer = 0 To 499
            trades.Add(New TradeRecord With {.Price = 60000 + i * 0.5, .Amount = 1000,
                                             .Direction = If(i Mod 5 = 0, "sell", "buy"),
                                             .Timestamp = t0 + i * 100, .Liquidation = "none"})
        Next
        ms.SeedTrades(trades, twoHoursAgo)
        Dim book As New OrderBookSnapshot()
        book.Bids.Add((60000.0, 1000.0)) : book.Asks.Add((60000.5, 1000.0))
        ms.UpdateBook(book, twoHoursAgo)
        ' The live wiring: health = IsConnected AndAlso Not IsCoolingDown (MainForm_Layout.vb:537-538)
        Dim src As New WsMarketDataSource(ms, Function() True, cfg.Network.WsStaleAfterSec)
        Dim got = src.GetRecentTradesAsync(500).GetAwaiter().GetResult()
        Dim gotBook = src.GetOrderBookAsync(10).GetAwaiter().GetResult()
        Dim tfi As Double, tfiSig As String = ""
        IndicatorEngine.CalcTFI(got, tfi, tfiSig, cfg.Indicators.TFI.WindowSize, cfg.Indicators.TFI.Threshold)
        Dim cvd As Double, cvdSlope As String = "", cvdDiv As String = ""
        IndicatorEngine.CalcCVD(got, New List(Of Candle)(), cvd, cvdSlope, cvdDiv,
                                cfg.Indicators.CVD.SlopeMinUsd, cfg.Indicators.CVD.SlopePctOfValue,
                                cfg.Indicators.CVD.DivergencePriceGate, cfg.Indicators.CVD.LateSegmentWeight,
                                cfg.Indicators.CVD.EarlySegmentWeight)
        Dim ageMin As Double = (DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(got.Last().Timestamp).UtcDateTime).TotalMinutes
        AuInfo(String.Format("newest trade in the ring is {0:F0} min old; trades served: {1}; TFI {2:F2} {3}; CVD {4:F0} {5}; same-age book served: {6}",
                           ageMin, If(got Is Nothing, 0, got.Count), tfi, tfiSig, cvd, cvdSlope, gotBook IsNot Nothing))
        AuMark("P08", got IsNot Nothing AndAlso got.Count = 500 AndAlso gotBook Is Nothing AndAlso tfiSig = "BUY PRESSURE",
             "on a connected socket the trades getter has NO age gate (book/ticker have 10 s); a stalled trades channel scores TFI/CVD/MicroCVD from a frozen ring indefinitely, and IsDegraded() needs ALL three streams stale")
    End Sub

    ' ------------------------------------------------------------------ P09

    Sub P09_ReconnectSeedFailureLeavesHole()
        Dim cfg = LoadShippedCfg()
        Dim ms As New MarketState()
        Dim nowUtc As DateTime = DateTime.UtcNow
        Dim lastPreOpen As DateTime = AuFloorToMinutes(nowUtc.AddMinutes(-30), 3)
        Dim pre As New List(Of Candle)()
        Dim rng As New Random(3)
        For i As Integer = 249 To 0 Step -1
            Dim c As Double = 60000 + (rng.NextDouble() - 0.5) * 30
            pre.Add(AuBar(AuMs(lastPreOpen.AddMinutes(-3 * i)), c, c + 20, c - 20, c))
        Next
        ms.SeedCandles("3", pre, lastPreOpen)
        ' Reconnect: DeribitClient.GetCandlesAsync returns Nothing during the venue incident, so
        ' SeedAsync skips SeedCandles (DeribitWsFeed.vb:325-326) and the stale series survives.
        ' The first WS chart tick for the current bar then appends across the hole.
        Dim nowOpen As DateTime = AuFloorToMinutes(nowUtc, 3)
        ms.ApplyChartTick("3", AuBar(AuMs(nowOpen), 60400, 60420, 60395, 60410), nowUtc)
        Dim s = ms.GetCandles("3")
        Dim holeMin As Double = (s(s.Count - 1).Timestamp - s(s.Count - 2).Timestamp) / 60000.0
        Dim fresh As Boolean = IndicatorEngine.IsFresh(s, 3, nowUtc)
        Dim atrPre As Double = IndicatorEngine.CalcATR(pre, cfg.Indicators.ATR.Period)
        Dim atrPost As Double = IndicatorEngine.CalcATR(s, cfg.Indicators.ATR.Period)
        Dim rocPost = IndicatorEngine.CalcROCSeries(s, cfg.Indicators.ROC.Period, cfg.Indicators.ROC.SeriesLookback)
        AuInfo(String.Format("3m series: {0} bars, last-two-bar gap {1:F0} min (expected 3); IsFresh = {2}; ATR(7) {3:F1} -> {4:F1}; stop distance 1.6xATR {5:F0} -> {6:F0} USD; ROC now {7:F3}% computed across the hole",
                           s.Count, holeMin, fresh, atrPre, atrPost, 1.6 * atrPre, 1.6 * atrPost, rocPost.Last()))
        AuMark("P09", holeMin > 3 AndAlso fresh AndAlso atrPost > atrPre * 1.5,
             "a failed REST re-seed keeps the pre-disconnect series; MarketState.ApplyChartTick appends across the hole and nothing downstream checks bar continuity (IsFresh looks at the last bar only)")
    End Sub

    ' ------------------------------------------------------------------ P10

    Sub P10_PayloadCarriesNoDataAge()
        Dim cfg = LoadShippedCfg()
        Dim r = DirectionalLongR(60000, 60, 60090, 59850)
        Dim v = ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg)
        Dim p = AuEmit(v, r, cfg, DateTime.UtcNow)
        Dim timeish As New List(Of String)()
        Dim walk As Action(Of JsonNode, String) = Nothing
        walk = Sub(n As JsonNode, prefix As String)
                   Dim o = TryCast(n, JsonObject)
                   If o Is Nothing Then Return
                   For Each kv In o
                       Dim pth As String = If(prefix = "", kv.Key, prefix & "." & kv.Key)
                       Dim k As String = kv.Key.ToLowerInvariant()
                       If k.Contains("time") OrElse k.Contains("_at") OrElse k.EndsWith("_ts") OrElse k.Contains("age") Then timeish.Add(pth)
                       walk(kv.Value, pth)
                   Next
               End Sub
        walk(p, "")
        Dim serialized As String = SignalEmitter.Serialize(p)     ' the shipped writer's exact bytes
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "sample_verdict_signal.json"), serialized)
        AuInfo("SignalEmitter.Serialize OK (" & serialized.Length & " chars) -> bin/sample_verdict_signal.json")
        AuInfo("payload keys: " & String.Join(", ", p.Select(Function(kv) kv.Key)))
        AuInfo("time/age-bearing fields: " & String.Join(", ", timeish))
        AuMark("P10", timeish.Count = 1 AndAlso timeish(0) = "generated_at_utc",
             "the only clock in the payload is generated_at_utc (emission wall-clock); no candle/trade/book as-of time exists, so the consumer's max-age gate cannot see data age")
    End Sub

    ' ------------------------------------------------------------------ P11

    Sub P11_KellyNotTiedToPlacedStop()
        Dim cfg = LoadShippedCfg()
        Dim price As Double = 60000, atr As Double = 100
        Dim r = DirectionalLongR(price, atr, price + 250, price - 20)   ' swing low 20 USD below -> SWING_STOP
        Dim v = ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg)
        ' exactly the snapshot's call (UI/MainForm_PlaintextSnapshot.vb:48 + :149)
        ScoringEngine.CalcKellySizing(v, r.ATR * cfg.Scoring.AtrStopMultiplier, r.CurrentPrice, cfg)
        Dim p = AuEmit(v, r, cfg)
        Dim stopPx As Double = AuNum(p("levels")("long")("stop"))
        Dim k = p("kelly")
        Dim contracts As Integer = k("contracts").GetValue(Of Integer)()
        Dim riskField As Double = AuNum(k("risk_usd"))
        Dim riskAtStop As Double = contracts * cfg.Kelly.ContractFaceUsd * (price - stopPx) / price
        ' inverse contract on BTC collateral: the long's USD loss includes the collateral's own delta
        Dim collateralLoss As Double = cfg.Kelly.AccountSizeUsd * (price - stopPx) / price
        AuInfo(String.Format("payload: levels.long.stop {0} ({1} USD away); kelly.contracts {2}, kelly.risk_usd {3:F2}",
                           stopPx, price - stopPx, contracts, riskField))
        AuInfo(String.Format("risk those contracts actually carry at the payload's own stop: ${0:F2}; plus BTC-collateral delta on a long ${1:F2} (+{2:P0})",
                           riskAtStop, collateralLoss, collateralLoss / riskAtStop))
        AuMark("P11", Math.Abs(riskAtStop - riskField) > 1,
             "Kelly sizes on ATR x atr_stop_multiplier while the payload stop is the placed (structural) stop — the two fields in one file describe different trades")
    End Sub

    ' ------------------------------------------------------------------ P12

    Sub P12_GetBookReturnsLiveReference()
        Dim ms As New MarketState()
        Dim snap As New OrderBookSnapshot()
        snap.Bids.Add((60000.0, 1000.0)) : snap.Asks.Add((60000.5, 1000.0))
        ms.UpdateBook(snap, DateTime.UtcNow)
        Dim b1 = ms.GetBook()
        Dim same As Boolean = Object.ReferenceEquals(b1, ms.GetBook())
        b1.Bids.Clear()
        Dim after As Integer = ms.GetBook().Bids.Count
        AuMark("P12", same AndAlso after = 0,
             String.Format("MarketState.GetBook returns the live snapshot object (same ref: {0}); a reader clearing it leaves MarketState with {1} bids — the class header promises copies", same, after))
    End Sub

    ' ------------------------------------------------------------------ P13

    Sub P13_SwingPivotRepaintsOnFormingBar()
        Dim cfg = LoadShippedCfg()
        Dim sw = cfg.Indicators.Swing
        Dim t0 As New DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc)
        Dim five As New List(Of Candle)()
        Dim lows() As Double = New Double(39) {}
        For i As Integer = 0 To 39
            lows(i) = 60000 - Math.Abs(20 - i) * 5 + 50            ' gentle V
        Next
        lows(20) = 59800                                            ' older pivot low
        lows(36) = 59950                                            ' newest candidate pivot (Count-4)
        lows(37) = 59990 : lows(38) = 59995 : lows(39) = 59990      ' right wing; index 39 = FORMING bar
        For i As Integer = 0 To 39
            five.Add(AuBar(AuMs(t0.AddMinutes(5 * i)), lows(i) + 20, lows(i) + 60, lows(i), lows(i) + 30))
        Next
        Dim hiA As Double, loA As Double
        IndicatorEngine.CalcSwingPivots(five, hiA, loA, sw.PivotWing5m, sw.LookbackBars5m)
        ' 40 seconds later the SAME forming bar trades 20 USD lower (it has not closed)
        five(39) = AuBar(five(39).Timestamp, five(39).[Open], five(39).High, 59930, 59940)
        Dim hiB As Double, loB As Double
        IndicatorEngine.CalcSwingPivots(five, hiB, loB, sw.PivotWing5m, sw.LookbackBars5m)
        AuInfo(String.Format("pivotWing {0}: forming bar low 59990 -> LastSwingLow5m {1}; same bar at 59930 -> LastSwingLow5m {2}",
                           sw.PivotWing5m, loA, loB))
        AuMark("P13", loA = 59950 AndAlso loB <> 59950,
             "a 'confirmed' pivot's right wing includes the FORMING 5m bar, so the structural stop jumps between runs inside one 5m bar (repaint)")
    End Sub

    ' ------------------------------------------------------------------ P15

    Sub P15_LevelsNotOnTickGrid()
        Dim cfg = LoadShippedCfg()
        Dim r = DirectionalLongR(60000, 37.3, 0, 0)              ' no structure -> fallback levels
        Dim p = AuEmit(ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg), r, cfg)
        Dim lg = p("levels")("long") : Dim srt = p("levels")("short")
        Dim vals() As Double = {AuNum(lg("stop")), AuNum(lg("target")), AuNum(srt("stop")), AuNum(srt("target"))}
        Dim off = vals.Where(Function(x) Math.Abs(x / SignalEmitter.TickSize - Math.Round(x / SignalEmitter.TickSize)) > 0.000000001).ToList()
        AuInfo("levels emitted: " & String.Join(", ", vals.Select(Function(x) x.ToString("R"))))
        AuMark("P15", off.Count > 0,
             String.Format("{0} of 4 emitted prices are off the {1} USD tick grid; the frozen contract names no rounding owner or direction", off.Count, SignalEmitter.TickSize))
    End Sub

    ' ------------------------------------------------------------------ P16

    Sub P16_StopFloorInsideConsumerSlippageCap()
        Dim cfg = LoadShippedCfg()
        Dim price As Double = 60000, atr As Double = 40
        Dim floorUsd As Double = cfg.Scoring.StructuralLevels.StopMinFloorTicks * SignalEmitter.TickSize
        Dim r = DirectionalLongR(price, atr, price + 90, price - floorUsd)
        Dim v = ScoringEngine.Calculate(r, PositionState.None, AuNorms(), cfg)
        Dim lv = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim slippageCap As Double = 0.6 * atr     ' consumer default per signal-bridge-v1-proposal.md §3
        AuInfo(String.Format("verdict {0}: long stop {1} [{2}] = {3} USD ({4:F2} bps) from the reference entry; consumer entry-slippage tolerance 0.6 x ATR = {5} USD",
                           v.Verdict, lv.StopPx, lv.StopReason, price - lv.StopPx, (price - lv.StopPx) / price * 10000, slippageCap))
        AuMark("P16", lv.StopReason = "SWING_STOP" AndAlso (price - lv.StopPx) < slippageCap AndAlso Not v.Verdict.StartsWith("NO TRADE"),
             "the engine emits a directional signal whose stop is closer to entry than the consumer's own permitted entry slippage: a fill at the slippage limit is already through the stop")
    End Sub

    ' ------------------------------------------------------------------ P17

    Sub P17_ThresholdCeilingFragility()
        Dim mi = GetType(ScoringEngine).GetMethod("Threshold", BindingFlags.NonPublic Or BindingFlags.Static)
        Dim a As Integer = CInt(mi.Invoke(Nothing, New Object() {20, 0.3}))
        Dim b As Integer = CInt(mi.Invoke(Nothing, New Object() {20, 0.1 + 0.2}))
        AuMark("P17", a <> b,
             String.Format("ScoringEngine.Threshold(20, 0.3) = {0} but Threshold(20, 0.1+0.2 = {1:R}) = {2}: Ceiling on a float product at an integer boundary", a, 0.1 + 0.2, b))
    End Sub

    ' ------------------------------------------------------------------ P18

    Sub P18_ApplyThrowsOnNet8()
        Dim baseJson As String = File.ReadAllText(Path.Combine(RepoRoot, "settings.json"))
        Dim tmp As String = Path.Combine(Path.GetTempPath(), "audit_apply_" & Guid.NewGuid().ToString("N") & ".json")
        File.WriteAllText(tmp, baseJson)
        Dim item = AuDiff("scoring.verdict_weak_pct", "0.35", "0.36")    ' a benign, in-range tweak
        Dim ok As Boolean = SettingsDiffApplier.Validate(New List(Of DiffItem) From {item}, baseJson, 3).IsValid
        Dim err As String = Nothing
        Try
            SettingsDiffApplier.Apply(New List(Of DiffItem) From {item}, tmp, "audit proof")
        Catch ex As Exception
            err = ex.GetType().Name & ": " & ex.Message & Environment.NewLine &
                  String.Join(Environment.NewLine, ex.StackTrace.Split(New String() {Environment.NewLine}, StringSplitOptions.None).Take(8))
        End Try
        Dim unchanged As Boolean = File.ReadAllText(tmp) = baseJson
        ' The snapshot REVERT path — the tweaker's own safety net — builds change_log the same way.
        Dim snap As String = Path.Combine(Path.GetTempPath(), "audit_snap_" & Guid.NewGuid().ToString("N") & ".json")
        File.WriteAllText(snap, baseJson)
        Dim errRevert As String = Nothing
        Try
            SettingsDiffApplier.ApplyRevert(snap, tmp, "audit proof")
        Catch ex As Exception
            errRevert = ex.GetType().Name & ": " & ex.Message
        End Try
        File.Delete(tmp) : File.Delete(snap)
        AuInfo("Validate IsValid=" & ok & "; runtime " & System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
        AuInfo("Apply: " & If(err, "returned normally"))
        AuInfo("settings file unchanged after Apply: " & unchanged)
        AuInfo("ApplyRevert: " & If(errRevert, "returned normally"))
        AuMark("P18", err IsNot Nothing AndAlso errRevert IsNot Nothing,
               "SettingsDiffApplier.Apply AND ApplyRevert throw on .NET 8 for any diff: JsonArray.Add(String) binds the generic Add(Of T) -> JsonValueCustomized, and ToJsonString(options without TypeInfoResolver) calls MakeReadOnly. No fixture exercises either")
    End Sub

    ' ------------------------------------------------------------------ P14

    Sub P14_PerfDisplayDisabledDeadlocksRun()
        ' MainForm_Layout.vb:479 only calls InitialiseAsync when performance_display.enabled is
        ' true at startup; InitialiseAsync is the only code that completes _initTcs. RunAnalysisAsync
        ' calls UpdateAsync unconditionally (MainForm_Analysis.vb:678), and UpdateAsync awaits
        ' _initTcs (LivePerformanceTracker.vb:524) BEFORE its own Enabled check (:527).
        Dim v As New VerdictResult With {.Verdict = "NO TRADE", .Confidence = "N/A"}
        Dim task = LivePerformanceTracker.UpdateAsync(v, New IndicatorResults(), New List(Of Candle)(), DateTime.UtcNow)
        Dim finished As Boolean = task.Wait(3000)
        AuMark("P14", Not finished,
             String.Format("UpdateAsync without a prior InitialiseAsync completed within 3 s: {0} — the run never reaches EmitBridgeSignal, and btnAnalyze stays disabled so every later auto-run fire is dropped", finished))
    End Sub

End Module
