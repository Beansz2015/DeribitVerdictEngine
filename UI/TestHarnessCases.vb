' UI/TestHarnessCases.vb
'
' P5-test commit 2 — the full case library. Each Shared Function builds
' one TestCase populating the engine state needed to exercise one or more
' render branches per the kickoff §4 coverage matrix.
'
' Each case's comment cites the production scenario it models and tags
' the branches it covers (BNN in the audit inventory — see spec-back).
'
' Deleted in P5-test commit 3 alongside MainForm_TestHarness.vb.

Imports System.Collections.Generic

Public Class TestHarnessCases

    ' Entry point invoked by MainForm.RunRenderParityHarness.
    Public Shared Function BuildAll(cfg As EngineSettings) As List(Of TestCase)
        Dim out As New List(Of TestCase)

        ' --- Verdict tier coverage (B1, B3, B16, B26, B27, B33, B37, B39, B43) ---
        out.Add(StrongLongFullConfluence(cfg))
        out.Add(LongMidStrengthTrending(cfg))
        out.Add(WeakLongMomentumFading(cfg))
        out.Add(NoTradeBalancedRange(cfg))
        out.Add(NoTradeMtfBlockLong(cfg))
        out.Add(WeakShortStructurallyWeak(cfg))
        out.Add(ShortMidStrengthTrending(cfg))
        out.Add(StrongShortFullConfluence(cfg))

        ' --- MTF / Regime variants (B39, B16, B5) ---
        out.Add(MtfStateOnlyFlat(cfg))
        out.Add(TransitionalRegimePenalty(cfg))

        ' --- ATR CAPPED variants (B7, B9) ---
        out.Add(CappedLongBySwingHigh(cfg))
        out.Add(CappedLongByHvnAbove(cfg))
        out.Add(CappedShortByHvnBelow(cfg))
        out.Add(CappedShortByPoc(cfg))
        out.Add(SubTickCapSuppressionLong(cfg))

        ' --- Structural row variants (B8, B10) ---
        out.Add(StructuralLongFullShortTargetOnly(cfg))
        out.Add(StructuralBothStopOnly(cfg))
        out.Add(StructuralLongTargetShortFull(cfg))
        out.Add(StructuralNoSwingData(cfg))

        ' --- KELLY variants (B11–B14) ---
        out.Add(KellyDirectionalSubContract(cfg))
        out.Add(KellyBiasOnlyCappedNoTrade(cfg))
        out.Add(KellyBiasOnlyNotCappedNoTrade(cfg))

        ' --- VPFR variants (B26, B27, B28) ---
        out.Add(VpfrNearHvnResistAboveVah(cfg))
        out.Add(VpfrInLvnBullInsideVa(cfg))
        out.Add(VpfrInLvnBearBelowVal(cfg))
        out.Add(VpfrHvnAtPocYes(cfg))

        ' --- OI × CVD outcomes (engine-side, surfaces in OiCvdOutcome) ---
        out.Add(OiCvdConflictLong(cfg))
        out.Add(OiCvdConflictShort(cfg))

        ' --- Trend Structure variants (B29, B30, B31) ---
        out.Add(TrendStructureDowntrend(cfg))
        out.Add(TrendStructureExpansion(cfg))
        out.Add(TrendStructureContraction(cfg))
        out.Add(TrendStructureUndefinedInsufficient(cfg))
        out.Add(BestVolPivotLow(cfg))

        ' --- MicroCVD 5-state (B37) — BULL_ACCEL/BEAR_ACCEL covered elsewhere ---
        out.Add(MicroCvdBullDecel(cfg))
        out.Add(MicroCvdBearDecel(cfg))

        ' --- BBW squeeze states (B21) ---
        out.Add(BbwActiveSqueeze(cfg))
        out.Add(BbwReleasingSqueeze(cfg))

        ' --- Funding variants (B40, B41, B42) ---
        out.Add(FundingHeavilyLongRising(cfg))
        out.Add(FundingHeavilyShortFalling(cfg))
        out.Add(FundingNegativeZeroClamp(cfg))

        ' --- REGIME ANCHOR warnings (B4) ---
        out.Add(RegimeAnchorStrongLongAgainstBear(cfg))
        out.Add(RegimeAnchorStrongShortAgainstBull(cfg))

        ' --- HOLD/EXIT layers (B6) — uses v.HoldStatus directly ---
        out.Add(HoldLayer1FastExitLong(cfg))
        out.Add(HoldLayer15StructuralBreakLong(cfg))
        out.Add(HoldLayer2ObvDivergenceShort(cfg))
        out.Add(HoldLayer3SingleAdverseLong(cfg))

        ' --- Edge cases ---
        out.Add(NormsStaticFallback(cfg))
        out.Add(VwapWarmupTag(cfg))
        out.Add(SpreadWide(cfg))
        out.Add(SpreadTight(cfg))
        out.Add(LiquidationsActive(cfg))
        out.Add(RsiDivergenceBullish(cfg))
        out.Add(RsiDivergenceBearish(cfg))
        out.Add(VolumeUsdSmallFormat(cfg))
        out.Add(VolumeUsdMidFormat(cfg))

        Return out
    End Function

    ' ========================================================================
    ' Verdict tier coverage
    ' ========================================================================

    ' Production scenario: high-confluence breakout in TRENDING_UP — every
    ' bullish signal firing, MTF PASS [LONG], BUY DOMINANT, NEW LONGS,
    ' BULL_ACCEL microCVD, Donchian breakout. Covers B1 STRONG LONG arm,
    ' B16 TRENDING_UP, B26 NEAR_HVN_SUPPORT, B33 OFI>1.2, B37 BULL_ACCEL,
    ' B39 PASS, B43 long-only hits, B19 (anchor 00:00 — runtime-dependent).
    Public Shared Function StrongLongFullConfluence(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("01_strong_long_full_confluence", cfg) _
            .WithDescription("STRONG LONG full confluence in TRENDING_UP — every bull signal firing.") _
            .WithVerdict("STRONG LONG", "HIGH", longScore:=15, shortScore:=2, maxScore:=19) _
            .WithContext("CONFIRMED") _
            .WithRegime("TRENDING_UP", adx:=32.5, plusDi:=28.0, minusDi:=14.0) _
            .WithCoreSignals(roc:=0.085, rocSlope:="RISING", rsi:=64.0, rsiDiv:="NONE", volumeRatio:=1.6) _
            .WithVolumeUsd(8_000_000.0) _
            .WithVwap(vwap:=49920.0, devPct:=0.16, candles:=180) _
            .WithBbw(bbw:=2.1, squeezeStatus:="NONE", ttmHist:=12.5, ttmDir:="RISING", ttmSignal:="BULL_BUILDING") _
            .WithEmaRibbon(ema9:=50050.0, ema21:=49980.0, ema50:=49900.0, alignment:="BULL", ema200:=49600.0, priceVs:="ABOVE") _
            .WithDonchian(upper:=50100.0, lower:=49600.0, signal:="LONG") _
            .WithObv(trend:="RISING", divergence:="NONE") _
            .WithVpfr(poc:=49950.0, signal:="NEAR_HVN_SUPPORT", hvnNearPoc:=False,
                       vah:=50100.0, vaLow:=49850.0, areaSignal:="INSIDE_VA") _
            .WithVpfrWalls(hvnAbove:=50200.0, hvnBelow:=49800.0, lvnAbove:=0.0, lvnBelow:=0.0) _
            .WithTrendStructure(TrendStructure.UPTREND, olderHigh:=49920.0, newerHigh:=50050.0,
                                 olderLow:=49810.0, newerLow:=49900.0) _
            .WithBestPivot(price:=50050.0, ratio:=1.8, isHigh:=True) _
            .WithOpenInterest(oi:=1_250_000.0, d15m:=0.42, d60m:=1.10, signal:="NEW LONGS") _
            .WithOfi(ratio:=1.45, signal:="BUY DOMINANT", momentum:="RISING", bidVol:=145.0, askVol:=100.0) _
            .WithCvd(value:=12500.0, slope:="RISING", divergence:="NONE") _
            .WithTfi(value:=0.42, signal:="BUY PRESSURE") _
            .WithMicroCvd(early:=4500.0, mid:=5200.0, late:=6300.0, momentum:="ACCELERATING", signal:="BULL_ACCEL") _
            .WithFunding(rate:=0.0001, bias:="NEUTRAL", momentum:="FLAT") _
            .WithOiCvdOutcome("CONFIRMED_LONG") _
            .WithBreakdown(SignalBreakdownPresets.StrongLong()) _
            .Build()
    End Function

    Public Shared Function LongMidStrengthTrending(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("02_long_mid_strength", cfg) _
            .WithDescription("LONG mid-strength TRENDING_UP — partial confluence, MTF PASS.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=11, shortScore:=4, maxScore:=19) _
            .WithContext("CONFIRMED") _
            .WithRegime("TRENDING_UP", adx:=24.0, plusDi:=24.0, minusDi:=16.0) _
            .WithCoreSignals(roc:=0.040, rocSlope:="RISING", rsi:=58.0, rsiDiv:="NONE", volumeRatio:=1.2) _
            .WithEmaRibbon(ema9:=50030.0, ema21:=50000.0, ema50:=49960.0, alignment:="BULL", ema200:=49750.0, priceVs:="ABOVE") _
            .WithOpenInterest(oi:=1_120_000.0, d15m:=0.18, d60m:=0.42, signal:="NEW LONGS") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    ' Covers B2 CONTEXT MOMENTUM_FADING arm, B1 WEAK LONG arm.
    Public Shared Function WeakLongMomentumFading(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("03_weak_long_momentum_fading", cfg) _
            .WithDescription("WEAK LONG with MOMENTUM_FADING context — score sufficient, microstructure decelerating.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=4, maxScore:=19) _
            .WithContext("MOMENTUM_FADING") _
            .WithRegime("TRENDING_UP", adx:=23.0, plusDi:=22.0, minusDi:=15.0) _
            .WithCoreSignals(roc:=0.020, rocSlope:="FALLING", rsi:=56.0, rsiDiv:="NONE", volumeRatio:=0.9) _
            .WithEmaRibbon(ema9:=50010.0, ema21:=50000.0, ema50:=49980.0, alignment:="BULL", ema200:=49850.0, priceVs:="ABOVE") _
            .WithMicroCvd(early:=3200.0, mid:=2100.0, late:=900.0, momentum:="DECELERATING", signal:="BULL_DECEL") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    ' Covers B1 NO TRADE arm, B16 RANGE_BOUND.
    Public Shared Function NoTradeBalancedRange(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("04_no_trade_balanced_range", cfg) _
            .WithDescription("NO TRADE balanced in RANGE_BOUND — score 7/7, MTF state-only FLAT.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithContext("CONFIRMED") _
            .WithRegime("RANGE_BOUND", adx:=14.0, plusDi:=18.0, minusDi:=18.0) _
            .WithCoreSignals(roc:=0.001, rocSlope:="FLAT", rsi:=50.0, rsiDiv:="NONE", volumeRatio:=1.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' Covers B39 BLOCK arm, MTF gate veto messaging.
    Public Shared Function NoTradeMtfBlockLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("05_no_trade_mtf_block_long", cfg) _
            .WithDescription("NO TRADE — bullish 1m signals but MTF 15m gate BLOCKs against LONG vs bear trend.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=11, shortScore:=4, maxScore:=19) _
            .WithContext("CONFIRMED") _
            .WithRegime("TRENDING_UP", adx:=22.0, plusDi:=23.0, minusDi:=15.0) _
            .WithMtfBlock("BLOCK [LONG vs BEAR trend]") _
            .WithMtfDetail(trend:="BEAR", adx:=24.0, emaAlign:="BEAR") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .OverrideBreakdownItem("MTF Gate (15m)", longHit:=False, shortHit:=False, note:="BLOCK [LONG vs BEAR trend]") _
            .Build()
    End Function

    ' Covers B2 CONTEXT STRUCTURALLY_WEAK arm.
    Public Shared Function WeakShortStructurallyWeak(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("06_weak_short_structurally_weak", cfg) _
            .WithDescription("WEAK SHORT with STRUCTURALLY_WEAK context — flow bearish but no swing confirmation.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=4, shortScore:=8, maxScore:=19) _
            .WithContext("STRUCTURALLY_WEAK") _
            .WithRegime("TRENDING_DOWN", adx:=22.0, plusDi:=15.0, minusDi:=22.0) _
            .WithCoreSignals(roc:=-0.020, rocSlope:="FALLING", rsi:=44.0, rsiDiv:="NONE", volumeRatio:=0.9) _
            .WithEmaRibbon(ema9:=49990.0, ema21:=50000.0, ema50:=50020.0, alignment:="BEAR", ema200:=50150.0, priceVs:="BELOW") _
            .WithSwings(longTarget:=0.0, longStop:=0.0, shortTarget:=0.0, shortStop:=0.0) _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    Public Shared Function ShortMidStrengthTrending(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("07_short_mid_strength", cfg) _
            .WithDescription("SHORT mid-strength TRENDING_DOWN — partial bear confluence, MTF PASS.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=4, shortScore:=11, maxScore:=19) _
            .WithContext("CONFIRMED") _
            .WithRegime("TRENDING_DOWN", adx:=24.0, plusDi:=16.0, minusDi:=24.0) _
            .WithCoreSignals(roc:=-0.040, rocSlope:="FALLING", rsi:=42.0, rsiDiv:="NONE", volumeRatio:=1.2) _
            .WithEmaRibbon(ema9:=49970.0, ema21:=50000.0, ema50:=50040.0, alignment:="BEAR", ema200:=50250.0, priceVs:="BELOW") _
            .WithOpenInterest(oi:=1_120_000.0, d15m:=-0.18, d60m:=-0.42, signal:="NEW SHORTS") _
            .WithBreakdown(SignalBreakdownPresets.Short_()) _
            .Build()
    End Function

    Public Shared Function StrongShortFullConfluence(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("08_strong_short_full_confluence", cfg) _
            .WithDescription("STRONG SHORT full confluence TRENDING_DOWN — mirror of case 01.") _
            .WithVerdict("STRONG SHORT", "HIGH", longScore:=2, shortScore:=15, maxScore:=19) _
            .WithContext("CONFIRMED") _
            .WithRegime("TRENDING_DOWN", adx:=32.5, plusDi:=14.0, minusDi:=28.0) _
            .WithCoreSignals(roc:=-0.085, rocSlope:="FALLING", rsi:=36.0, rsiDiv:="NONE", volumeRatio:=1.6) _
            .WithVolumeUsd(8_000_000.0) _
            .WithVwap(vwap:=50080.0, devPct:=-0.16, candles:=180) _
            .WithBbw(bbw:=2.1, squeezeStatus:="NONE", ttmHist:=-12.5, ttmDir:="FALLING", ttmSignal:="BEAR_BUILDING") _
            .WithEmaRibbon(ema9:=49950.0, ema21:=50020.0, ema50:=50100.0, alignment:="BEAR", ema200:=50400.0, priceVs:="BELOW") _
            .WithDonchian(upper:=50400.0, lower:=49900.0, signal:="SHORT") _
            .WithObv(trend:="FALLING", divergence:="NONE") _
            .WithVpfr(poc:=50050.0, signal:="NEAR_HVN_RESIST", hvnNearPoc:=False,
                       vah:=50150.0, vaLow:=49900.0, areaSignal:="INSIDE_VA") _
            .WithVpfrWalls(hvnAbove:=50200.0, hvnBelow:=49800.0, lvnAbove:=0.0, lvnBelow:=0.0) _
            .WithTrendStructure(TrendStructure.DOWNTREND, olderHigh:=50080.0, newerHigh:=49950.0,
                                 olderLow:=49990.0, newerLow:=49880.0) _
            .WithBestPivot(price:=49880.0, ratio:=1.8, isHigh:=False) _
            .WithOpenInterest(oi:=1_250_000.0, d15m:=-0.42, d60m:=-1.10, signal:="NEW SHORTS") _
            .WithOfi(ratio:=0.55, signal:="SELL DOMINANT", momentum:="FALLING", bidVol:=100.0, askVol:=180.0) _
            .WithCvd(value:=-12500.0, slope:="FALLING", divergence:="NONE") _
            .WithTfi(value:=-0.42, signal:="SELL PRESSURE") _
            .WithMicroCvd(early:=-4500.0, mid:=-5200.0, late:=-6300.0, momentum:="ACCELERATING", signal:="BEAR_ACCEL") _
            .WithFunding(rate:=0.0001, bias:="NEUTRAL", momentum:="FLAT") _
            .WithOiCvdOutcome("CONFIRMED_SHORT") _
            .WithBreakdown(SignalBreakdownPresets.StrongShort()) _
            .Build()
    End Function

    ' ========================================================================
    ' MTF / Regime
    ' ========================================================================

    Public Shared Function MtfStateOnlyFlat(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("09_mtf_state_only_flat", cfg) _
            .WithDescription("MTF state-only — FLAT 15m, no direction proposed.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithRegime("RANGE_BOUND", adx:=15.0, plusDi:=17.0, minusDi:=17.0) _
            .WithMtfDetail(trend:="FLAT", adx:=15.0, emaAlign:="MIXED") _
            .WithMtfPass() _
            .OverrideBreakdownItem("MTF Gate (15m)", longHit:=False, shortHit:=False, note:="state: FLAT") _
            .Build()
    End Function

    ' Covers B5 RegimePenalty > 0 SCORE format.
    Public Shared Function TransitionalRegimePenalty(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("10_transitional_regime_penalty", cfg) _
            .WithDescription("TRANSITIONAL regime with -2 ADX penalty — eff scores diverge from raw.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=10, shortScore:=4, maxScore:=15) _
            .WithRegimePenalty(penalty:=2, effLong:=8, effShort:=2) _
            .WithRegime("TRANSITIONAL", adx:=18.0, plusDi:=21.0, minusDi:=16.0) _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    ' ========================================================================
    ' ATR CAPPED variants
    ' ========================================================================

    ' Long ATR target is 50160; swing high at 50080 caps below it.
    Public Shared Function CappedLongBySwingHigh(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("11_capped_long_by_swing_high", cfg) _
            .WithDescription("LONG verdict — ATR target 50160 capped at swing high 50080.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=12, shortScore:=3, maxScore:=19) _
            .WithRegime("TRENDING_UP", adx:=24.0, plusDi:=24.0, minusDi:=16.0) _
            .WithSwings(longTarget:=50080.0, longStop:=49700.0, shortTarget:=49700.0, shortStop:=50300.0) _
            .WithAtrCapLong(adjustedTarget:=50080.0, reason:="CAPPED @ 50080.0 (SWING_HIGH_5M)") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    ' Long ATR target capped at the nearest HVN above current price.
    Public Shared Function CappedLongByHvnAbove(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("12_capped_long_by_hvn_above", cfg) _
            .WithDescription("LONG verdict — ATR target capped at NEAREST_HVN_ABOVE 50125.5.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=12, shortScore:=3, maxScore:=19) _
            .WithRegime("TRENDING_UP", adx:=24.0, plusDi:=24.0, minusDi:=16.0) _
            .WithVpfrWalls(hvnAbove:=50125.5, hvnBelow:=49800.0, lvnAbove:=0.0, lvnBelow:=0.0) _
            .WithAtrCapLong(adjustedTarget:=50125.5, reason:="CAPPED @ 50125.5 (NEAREST_HVN_ABOVE)") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    Public Shared Function CappedShortByHvnBelow(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("13_capped_short_by_hvn_below", cfg) _
            .WithDescription("SHORT verdict — ATR target capped at NEAREST_HVN_BELOW 49874.5.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=3, shortScore:=12, maxScore:=19) _
            .WithRegime("TRENDING_DOWN", adx:=24.0, plusDi:=16.0, minusDi:=24.0) _
            .WithVpfrWalls(hvnAbove:=50200.0, hvnBelow:=49874.5, lvnAbove:=0.0, lvnBelow:=0.0) _
            .WithAtrCapShort(adjustedTarget:=49874.5, reason:="CAPPED @ 49874.5 (NEAREST_HVN_BELOW)") _
            .WithBreakdown(SignalBreakdownPresets.Short_()) _
            .Build()
    End Function

    Public Shared Function CappedShortByPoc(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("14_capped_short_by_poc", cfg) _
            .WithDescription("SHORT verdict — ATR target capped at POC 49880.0.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=3, shortScore:=12, maxScore:=19) _
            .WithRegime("TRENDING_DOWN", adx:=24.0, plusDi:=16.0, minusDi:=24.0) _
            .WithVpfr(poc:=49880.0, signal:="NEUTRAL", hvnNearPoc:=False,
                       vah:=50150.0, vaLow:=49850.0, areaSignal:="INSIDE_VA") _
            .WithAtrCapShort(adjustedTarget:=49880.0, reason:="CAPPED @ 49880.0 (POC)") _
            .WithBreakdown(SignalBreakdownPresets.Short_()) _
            .Build()
    End Function

    ' Covers the sub-tick-CAPPED suppression branch — cap exists but adjustment
    ' < max(0.5, ATR×0.02). For ATR=80, capNoiseFloor = max(0.5, 1.6) = 1.6.
    ' Setting adjustedTarget = 50159 vs raw 50160 (delta 1.0) puts it within
    ' the floor → no [CAPPED] suffix should render. Tests the formatter
    ' branch that uses adjusted target as Target but skips the --> markup.
    Public Shared Function SubTickCapSuppressionLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("15_subtick_cap_suppression_long", cfg) _
            .WithDescription("LONG — cap delta 1.0 below 1.6 noise floor → adjusted-as-target but no --> markup.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=12, shortScore:=3, maxScore:=19) _
            .WithRegime("TRENDING_UP", adx:=24.0, plusDi:=24.0, minusDi:=16.0) _
            .WithAtrCapLong(adjustedTarget:=50159.0, reason:="CAPPED @ 50159.0 (NEAREST_HVN_ABOVE)") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    ' ========================================================================
    ' Structural row combinations (Long FULL/STOP_ONLY/TARGET_ONLY × Short same)
    ' Neutral baseline has both FULL — we vary the swing fields.
    ' ========================================================================

    Public Shared Function StructuralLongFullShortTargetOnly(cfg As EngineSettings) As TestCase
        ' Short stop > price requires SwingHigh5m > price.
        ' Short target = SwingLow5m < price.
        ' We want LongFull (both swing fields), ShortTargetOnly (LongTarget+LongStop, ShortTarget set, ShortStop=0).
        Return TestCaseBuilder.NeutralCase("16_structural_long_full_short_target_only", cfg) _
            .WithDescription("Long FULL row + Short TARGET_ONLY (no SwingHigh above entry).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithSwings(longTarget:=50300.0, longStop:=49700.0, shortTarget:=49700.0, shortStop:=0.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function StructuralBothStopOnly(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("17_structural_both_stop_only", cfg) _
            .WithDescription("Long STOP_ONLY + Short STOP_ONLY — neither swing target on either side.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithSwings(longTarget:=0.0, longStop:=49700.0, shortTarget:=0.0, shortStop:=50300.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function StructuralLongTargetShortFull(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("18_structural_long_target_short_full", cfg) _
            .WithDescription("Long TARGET_ONLY + Short FULL (no SwingLow below entry).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithSwings(longTarget:=50300.0, longStop:=0.0, shortTarget:=49700.0, shortStop:=50300.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function StructuralNoSwingData(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("19_structural_no_swing_data", cfg) _
            .WithDescription("No swing data — both structural rows suppressed.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithSwings(longTarget:=0.0, longStop:=0.0, shortTarget:=0.0, shortStop:=0.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' ========================================================================
    ' KELLY variants
    ' Note: CalcKellySizing runs inline in both renderers. To force specific
    ' Kelly outputs, we leverage the deterministic mapping (confidence → p,
    ' AtrStopMultiplier × ATR → contract risk). KellyContracts < 1 needs a
    ' very wide stop relative to the account: shrink the account or widen
    ' the stop. We synthesize KellyContracts < 1 by making ATR huge so the
    ' per-contract risk swamps the applied dollar risk.
    ' ========================================================================

    Public Shared Function KellyDirectionalSubContract(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("20_kelly_directional_sub_contract", cfg) _
            .WithDescription("LONG verdict, very wide ATR → KellyContracts < 1 (stop too wide for min size).") _
            .WithVerdict("LONG", "MEDIUM", longScore:=11, shortScore:=4, maxScore:=19) _
            .WithAtr(atr:=5000.0, scaleFactor:=1.0) _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    Public Shared Function KellyBiasOnlyCappedNoTrade(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("21_kelly_bias_only_capped_no_trade", cfg) _
            .WithDescription("NO TRADE — KELLY block emits BIAS ONLY tag + [CAPPED] (HIGH confidence inflates fHalf).") _
            .WithVerdict("NO TRADE", "HIGH", longScore:=9, shortScore:=7, maxScore:=18) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function KellyBiasOnlyNotCappedNoTrade(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("22_kelly_bias_only_not_capped_no_trade", cfg) _
            .WithDescription("NO TRADE — KELLY block emits BIAS ONLY without [CAPPED] (LOW confidence keeps fHalf below cap).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' ========================================================================
    ' VPFR variants
    ' ========================================================================

    Public Shared Function VpfrNearHvnResistAboveVah(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("23_vpfr_near_hvn_resist_above_vah", cfg) _
            .WithDescription("VPFR NEAR_HVN_RESIST + ABOVE_VAH — price at resistance overhead, breakout candidate.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=5, maxScore:=19) _
            .WithVpfr(poc:=49920.0, signal:="NEAR_HVN_RESIST", hvnNearPoc:=False,
                       vah:=49980.0, vaLow:=49850.0, areaSignal:="ABOVE_VAH") _
            .WithVpfrWalls(hvnAbove:=50050.0, hvnBelow:=49800.0, lvnAbove:=0.0, lvnBelow:=49870.0) _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    Public Shared Function VpfrInLvnBullInsideVa(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("24_vpfr_in_lvn_bull_inside_va", cfg) _
            .WithDescription("VPFR IN_LVN_BULL inside value area — price in low-volume pocket, bullish lean.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=10, shortScore:=4, maxScore:=19) _
            .WithVpfr(poc:=49850.0, signal:="IN_LVN_BULL", hvnNearPoc:=False,
                       vah:=50100.0, vaLow:=49800.0, areaSignal:="INSIDE_VA") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    Public Shared Function VpfrInLvnBearBelowVal(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("25_vpfr_in_lvn_bear_below_val", cfg) _
            .WithDescription("VPFR IN_LVN_BEAR + BELOW_VAL — price below value area in low-volume pocket.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=4, shortScore:=10, maxScore:=19) _
            .WithVpfr(poc:=50150.0, signal:="IN_LVN_BEAR", hvnNearPoc:=False,
                       vah:=50200.0, vaLow:=50050.0, areaSignal:="BELOW_VAL") _
            .WithBreakdown(SignalBreakdownPresets.Short_()) _
            .Build()
    End Function

    Public Shared Function VpfrHvnAtPocYes(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("26_vpfr_hvn_at_poc_yes", cfg) _
            .WithDescription("VPFR HVN@POC = YES — price coincident with POC and a high-volume cluster.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithVpfr(poc:=50000.0, signal:="NEUTRAL", hvnNearPoc:=True,
                       vah:=50100.0, vaLow:=49900.0, areaSignal:="INSIDE_VA") _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' ========================================================================
    ' OI × CVD CONFLICT outcomes (the CONFIRMED variants are covered by 01/08)
    ' ========================================================================

    Public Shared Function OiCvdConflictLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("27_oi_cvd_conflict_long", cfg) _
            .WithDescription("OI signals NEW LONGS but CVD falling — Pass 2b CONFLICT_LONG.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=9, shortScore:=5, maxScore:=19) _
            .WithOpenInterest(oi:=1_100_000.0, d15m:=0.35, d60m:=0.80, signal:="NEW LONGS") _
            .WithCvd(value:=-2500.0, slope:="FALLING", divergence:="BEARISH") _
            .WithOiCvdOutcome("CONFLICT_LONG") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    Public Shared Function OiCvdConflictShort(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("28_oi_cvd_conflict_short", cfg) _
            .WithDescription("OI signals NEW SHORTS but CVD rising — Pass 2b CONFLICT_SHORT.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=5, shortScore:=9, maxScore:=19) _
            .WithOpenInterest(oi:=1_100_000.0, d15m:=-0.35, d60m:=-0.80, signal:="NEW SHORTS") _
            .WithCvd(value:=2500.0, slope:="RISING", divergence:="BULLISH") _
            .WithOiCvdOutcome("CONFLICT_SHORT") _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    ' ========================================================================
    ' Trend Structure variants (UPTREND covered by 01, UNDEFINED by neutrals)
    ' ========================================================================

    Public Shared Function TrendStructureDowntrend(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("29_trend_structure_downtrend", cfg) _
            .WithDescription("Trend Structure DOWNTREND with LH/LL pivot detail.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=4, shortScore:=11, maxScore:=19) _
            .WithTrendStructure(TrendStructure.DOWNTREND, olderHigh:=50250.0, newerHigh:=50100.0,
                                 olderLow:=50000.0, newerLow:=49870.0) _
            .WithBreakdown(SignalBreakdownPresets.Short_()) _
            .Build()
    End Function

    Public Shared Function TrendStructureExpansion(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("30_trend_structure_expansion", cfg) _
            .WithDescription("Trend Structure EXPANSION — HH and LL simultaneously, range widening.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithTrendStructure(TrendStructure.EXPANSION, olderHigh:=50100.0, newerHigh:=50250.0,
                                 olderLow:=49900.0, newerLow:=49750.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function TrendStructureContraction(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("31_trend_structure_contraction", cfg) _
            .WithDescription("Trend Structure CONTRACTION — LH and HL, range narrowing.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithTrendStructure(TrendStructure.CONTRACTION, olderHigh:=50250.0, newerHigh:=50100.0,
                                 olderLow:=49750.0, newerLow:=49900.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function TrendStructureUndefinedInsufficient(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("32_trend_structure_undefined_insufficient", cfg) _
            .WithDescription("Trend Structure UNDEFINED — insufficient pivot data ((0.0, 0.0) tuples).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithTrendStructure(TrendStructure.UNDEFINED, olderHigh:=0.0, newerHigh:=0.0,
                                 olderLow:=0.0, newerLow:=0.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' Covers B31 BestPivotIsHigh=False arm.
    Public Shared Function BestVolPivotLow(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("33_best_vol_pivot_low", cfg) _
            .WithDescription("Best Volume Pivot is a LOW (not HIGH) — covers B31 isHigh=False arm.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=5, shortScore:=8, maxScore:=19) _
            .WithBestPivot(price:=49680.0, ratio:=2.1, isHigh:=False) _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    ' ========================================================================
    ' MicroCVD 5-state coverage
    ' ========================================================================

    Public Shared Function MicroCvdBullDecel(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("34_microcvd_bull_decel", cfg) _
            .WithDescription("MicroCVD BULL_DECEL — buy pressure fading late in the window.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=4, maxScore:=19) _
            .WithMicroCvd(early:=4500.0, mid:=3200.0, late:=1100.0, momentum:="DECELERATING", signal:="BULL_DECEL") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    Public Shared Function MicroCvdBearDecel(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("35_microcvd_bear_decel", cfg) _
            .WithDescription("MicroCVD BEAR_DECEL — sell pressure fading late in the window.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=4, shortScore:=8, maxScore:=19) _
            .WithMicroCvd(early:=-4500.0, mid:=-3200.0, late:=-1100.0, momentum:="DECELERATING", signal:="BEAR_DECEL") _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    ' ========================================================================
    ' BBW squeeze states
    ' ========================================================================

    Public Shared Function BbwActiveSqueeze(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("36_bbw_active_squeeze", cfg) _
            .WithDescription("BBW ACTIVE — squeeze loaded, awaiting breakout.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithBbw(bbw:=0.35, squeezeStatus:="ACTIVE", ttmHist:=0.0, ttmDir:="FLAT", ttmSignal:="FLAT") _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function BbwReleasingSqueeze(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("37_bbw_releasing_squeeze", cfg) _
            .WithDescription("BBW RELEASING — squeeze breaking, expansion underway.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=4, maxScore:=19) _
            .WithBbw(bbw:=1.4, squeezeStatus:="RELEASING", ttmHist:=8.0, ttmDir:="RISING", ttmSignal:="BULL_BUILDING") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    ' ========================================================================
    ' Funding variants (B40, B41, B42)
    ' ========================================================================

    Public Shared Function FundingHeavilyLongRising(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("38_funding_heavily_long_rising", cfg) _
            .WithDescription("Funding HEAVILY LONG + RISING momentum — contrarian short bias signal.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=4, shortScore:=8, maxScore:=19) _
            .WithFunding(rate:=0.00075, bias:="HEAVILY LONG", momentum:="RISING") _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    Public Shared Function FundingHeavilyShortFalling(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("39_funding_heavily_short_falling", cfg) _
            .WithDescription("Funding HEAVILY SHORT + FALLING momentum — contrarian long bias signal.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=4, maxScore:=19) _
            .WithFunding(rate:=-0.00075, bias:="HEAVILY SHORT", momentum:="FALLING") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    ' B42 — Math.Abs(FundingRate) < 1e-8 → display 0.0000% (clamp).
    Public Shared Function FundingNegativeZeroClamp(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("40_funding_negative_zero_clamp", cfg) _
            .WithDescription("Funding rate at -1e-12 (well below 1e-8 threshold) → display clamps to 0.0000%.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithFunding(rate:=-0.000000000001, bias:="NEUTRAL", momentum:="FLAT") _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' ========================================================================
    ' REGIME ANCHOR warnings (B4)
    ' ========================================================================
    ' atrUnits = (CurrentPrice - EMA200_5m) / ATR must satisfy |atrUnits| > 3.0
    ' for the warning to fire. ATR=80 → CurrentPrice 50000, EMA200 = 50250
    ' gives atrUnits = -3.125 (LONG fighting bear). For SHORT fighting bull:
    ' EMA200 = 49750 gives atrUnits = +3.125.

    Public Shared Function RegimeAnchorStrongLongAgainstBear(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("41_regime_anchor_strong_long_vs_bear", cfg) _
            .WithDescription("STRONG LONG fighting intermediate bear — price 3.1× ATR below 5m EMA(200).") _
            .WithVerdict("STRONG LONG", "HIGH", longScore:=15, shortScore:=3, maxScore:=19) _
            .WithEmaRibbon(ema9:=50020.0, ema21:=49990.0, ema50:=49960.0, alignment:="BULL",
                            ema200:=50250.0, priceVs:="BELOW") _
            .WithBreakdown(SignalBreakdownPresets.StrongLong()) _
            .Build()
    End Function

    Public Shared Function RegimeAnchorStrongShortAgainstBull(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("42_regime_anchor_strong_short_vs_bull", cfg) _
            .WithDescription("STRONG SHORT fighting intermediate bull — price 3.1× ATR above 5m EMA(200).") _
            .WithVerdict("STRONG SHORT", "HIGH", longScore:=3, shortScore:=15, maxScore:=19) _
            .WithEmaRibbon(ema9:=49980.0, ema21:=50010.0, ema50:=50040.0, alignment:="BEAR",
                            ema200:=49750.0, priceVs:="ABOVE") _
            .WithBreakdown(SignalBreakdownPresets.StrongShort()) _
            .Build()
    End Function

    ' ========================================================================
    ' HOLD/EXIT layers (B6 gate emits when HoldStatus != "N/A -- no open position")
    ' ========================================================================

    Public Shared Function HoldLayer1FastExitLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("43_hold_layer1_fast_exit_long", cfg) _
            .WithDescription("In LONG position, 2+ adverse microstructure → Layer 1 fast EXIT.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=4, shortScore:=7, maxScore:=19) _
            .WithHoldStatus("EXIT LONG (Layer 1) -- 2+ adverse microstructure: OFI flipped, CVD diverging") _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    Public Shared Function HoldLayer15StructuralBreakLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("44_hold_layer15_structural_break_long", cfg) _
            .WithDescription("In LONG position, price closed below prior swing low → Layer 1.5 structural break exit.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=5, shortScore:=5, maxScore:=18) _
            .WithHoldStatus("EXIT LONG (Layer 1.5) -- price closed 49680.0 below prior swing low 49700.0") _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function HoldLayer2ObvDivergenceShort(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("45_hold_layer2_obv_divergence_short", cfg) _
            .WithDescription("In SHORT position, OBV bullish divergence → Layer 2 exit warning.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=7, shortScore:=4, maxScore:=19) _
            .WithObv(trend:="RISING", divergence:="BULLISH") _
            .WithHoldStatus("EXIT SHORT (Layer 2) -- OBV bullish divergence vs price LL") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    Public Shared Function HoldLayer3SingleAdverseLong(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("46_hold_layer3_single_adverse_long", cfg) _
            .WithDescription("In LONG position, single adverse microstructure → Layer 3 warning (hold continues).") _
            .WithVerdict("LONG", "MEDIUM", longScore:=10, shortScore:=4, maxScore:=19) _
            .WithHoldStatus("HOLD LONG (Layer 3) -- single adverse: OFI flipped to SELL DOMINANT, monitor next bar") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .Build()
    End Function

    ' ========================================================================
    ' Edge cases
    ' ========================================================================

    Public Shared Function NormsStaticFallback(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("47_norms_static_fallback", cfg) _
            .WithDescription("DYNAMIC NORMS in STATIC FALLBACK mode (insufficient candle history).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithNormsLive(False) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' B20 — VWAPSessionCandles < vwapWarmup → "[WARMUP]" tag.
    Public Shared Function VwapWarmupTag(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("48_vwap_warmup_tag", cfg) _
            .WithDescription("VWAP session candles below warmup threshold — [WARMUP] tag.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithVwap(vwap:=50000.0, devPct:=0.05, candles:=20) _
            .WithVwapWarmup(60) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    Public Shared Function SpreadWide(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("49_spread_wide", cfg) _
            .WithDescription("Spread WIDE — liquidity-thin condition, both sides penalised.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithSpread(bps:=4.5, status:="WIDE") _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .OverrideBreakdownItem("Spread", longHit:=False, shortHit:=False, note:="-1 WIDE 4.5 bps") _
            .Build()
    End Function

    Public Shared Function SpreadTight(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("50_spread_tight", cfg) _
            .WithDescription("Spread TIGHT — strong liquidity, both sides un-penalised.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=11, shortScore:=4, maxScore:=19) _
            .WithSpread(bps:=0.3, status:="TIGHT") _
            .WithBreakdown(SignalBreakdownPresets.Long_()) _
            .OverrideBreakdownItem("Spread", longHit:=False, shortHit:=False, note:="TIGHT 0.3 bps") _
            .Build()
    End Function

    Public Shared Function LiquidationsActive(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("51_liquidations_active", cfg) _
            .WithDescription("Liquidations cascade firing — LiqSignal != NONE, both penalty + warn colour.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=5, maxScore:=19) _
            .WithLiquidations(longSize:=420.0, shortSize:=85.0, signal:="LONG_CASCADE") _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .OverrideBreakdownItem("Liq Penalty", longHit:=False, shortHit:=True, note:="-1 long-side liq cascade") _
            .Build()
    End Function

    ' B17 — RSI Div suffix arm (BULLISH).
    Public Shared Function RsiDivergenceBullish(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("52_rsi_divergence_bullish", cfg) _
            .WithDescription("RSI BULLISH divergence — RSI rising while price made LL.") _
            .WithVerdict("WEAK LONG", "LOW", longScore:=8, shortScore:=5, maxScore:=19) _
            .WithCoreSignals(roc:=-0.005, rocSlope:="FLAT", rsi:=38.0, rsiDiv:="BULLISH", volumeRatio:=1.1) _
            .WithBreakdown(SignalBreakdownPresets.WeakLong()) _
            .Build()
    End Function

    Public Shared Function RsiDivergenceBearish(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("53_rsi_divergence_bearish", cfg) _
            .WithDescription("RSI BEARISH divergence — RSI falling while price made HH.") _
            .WithVerdict("WEAK SHORT", "LOW", longScore:=5, shortScore:=8, maxScore:=19) _
            .WithCoreSignals(roc:=0.005, rocSlope:="FLAT", rsi:=62.0, rsiDiv:="BEARISH", volumeRatio:=1.1) _
            .WithBreakdown(SignalBreakdownPresets.WeakShort()) _
            .Build()
    End Function

    ' B18 — Volume USD < 1K format ("$N").
    Public Shared Function VolumeUsdSmallFormat(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("54_volume_usd_small_format", cfg) _
            .WithDescription("Volume USD below 1K threshold — bare $N format (no K/M suffix).") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithVolumeUsd(450.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

    ' B18 — Volume USD between 1K and 1M ("$N.NK").
    Public Shared Function VolumeUsdMidFormat(cfg As EngineSettings) As TestCase
        Return TestCaseBuilder.NeutralCase("55_volume_usd_mid_format", cfg) _
            .WithDescription("Volume USD between 1K and 1M — $N.NK format.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=6, shortScore:=6, maxScore:=18) _
            .WithVolumeUsd(48_500.0) _
            .WithBreakdown(SignalBreakdownPresets.NoTrade()) _
            .Build()
    End Function

End Class

' ===========================================================================
' SignalBreakdown preset rosters. Each function returns a 21-row list with
' a hit pattern that approximates the corresponding verdict tier — useful
' as a one-line .WithBreakdown(SignalBreakdownPresets.X()) call inside case
' factories. Individual rows can then be overridden via OverrideBreakdownItem
' for MTF BLOCK / Spread WIDE / Liq cascade variants.
'
' Hit counts are approximate by design — production hits don't sum to score
' (regime cap, modifiers, multi-point hits, penalties), and the spec author
' confirmed approximation is acceptable. The label vocabulary mirrors the
' scoring engine (Core/ScoringEngine_Calculate_Scoring.vb 637-757 + the MTF
' Gate row from ScoringEngine_Calculate_Verdict.vb:82).
' ===========================================================================

Public Class SignalBreakdownPresets

    Public Shared Function StrongLong() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         True,  False, "+1 BULL ROC rising"),
            New SignalBreakdownItem("RSI(9)",         True,  False, "+1 RSI 64 > 50"),
            New SignalBreakdownItem("DMI +/-DI",      True,  False, "+1 +DI 28 > -DI 14"),
            New SignalBreakdownItem("ADX>22",         True,  False, "+1 trend strength"),
            New SignalBreakdownItem("Volume",         True,  False, "+1 vol 1.6x SMA"),
            New SignalBreakdownItem("VWAP",           True,  False, "+1 above VWAP"),
            New SignalBreakdownItem("BBW/TTM",        True,  False, "+1 BULL_BUILDING"),
            New SignalBreakdownItem("EMA 9/21/50",    True,  False, "+1 BULL alignment"),
            New SignalBreakdownItem("Funding (info)", False, False, "info: rate 0.010% NEUTRAL"),
            New SignalBreakdownItem("OI Delta",       True,  False, "+1 NEW LONGS"),
            New SignalBreakdownItem("OFI",            True,  False, "+1 BUY DOMINANT"),
            New SignalBreakdownItem("CVD",            True,  False, "+1 RISING"),
            New SignalBreakdownItem("TFI",            True,  False, "+1 BUY PRESSURE"),
            New SignalBreakdownItem("MicroCVD",       True,  False, "+1 BULL_ACCEL"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL 1.0 bps"),
            New SignalBreakdownItem("5m EMA(200)",    True,  False, "+1 price ABOVE"),
            New SignalBreakdownItem("Donchian(20)",   True,  False, "+1 breakout up"),
            New SignalBreakdownItem("OBV",            True,  False, "+1 RISING"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "NEUTRAL"),
            New SignalBreakdownItem("MTF Gate (15m)", True,  False, "PASS [LONG] vs BULL trend")
        }
    End Function

    ' "Long_" with trailing underscore — VB.NET would parse "Long" as keyword.
    Public Shared Function Long_() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         True,  False, "+1 BULL ROC"),
            New SignalBreakdownItem("RSI(9)",         True,  False, "+1 RSI 58"),
            New SignalBreakdownItem("DMI +/-DI",      True,  False, "+1 +DI > -DI"),
            New SignalBreakdownItem("ADX>22",         True,  False, "+1 trend"),
            New SignalBreakdownItem("Volume",         False, False, "mid 1.2x"),
            New SignalBreakdownItem("VWAP",           True,  False, "+1 above VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, False, "FLAT"),
            New SignalBreakdownItem("EMA 9/21/50",    True,  False, "+1 BULL"),
            New SignalBreakdownItem("Funding (info)", False, False, "info: 0.012%"),
            New SignalBreakdownItem("OI Delta",       True,  False, "+1 NEW LONGS"),
            New SignalBreakdownItem("OFI",            False, True,  "-1 SELL DOMINANT"),
            New SignalBreakdownItem("CVD",            True,  False, "+1 RISING"),
            New SignalBreakdownItem("TFI",            False, False, "NEUTRAL"),
            New SignalBreakdownItem("MicroCVD",       False, True,  "-1 BULL_DECEL late fade"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL"),
            New SignalBreakdownItem("5m EMA(200)",    True,  False, "+1 ABOVE"),
            New SignalBreakdownItem("Donchian(20)",   False, False, "NONE"),
            New SignalBreakdownItem("OBV",            False, True,  "-1 divergence"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "INSIDE_VA"),
            New SignalBreakdownItem("MTF Gate (15m)", True,  False, "PASS [LONG]")
        }
    End Function

    Public Shared Function WeakLong() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         True,  False, "+1 weak BULL ROC"),
            New SignalBreakdownItem("RSI(9)",         True,  False, "+1 RSI 56"),
            New SignalBreakdownItem("DMI +/-DI",      True,  False, "+1 +DI > -DI"),
            New SignalBreakdownItem("ADX>22",         True,  False, "+1 trend just over thr"),
            New SignalBreakdownItem("Volume",         False, False, "0.9x SMA"),
            New SignalBreakdownItem("VWAP",           False, False, "near VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, False, "FLAT"),
            New SignalBreakdownItem("EMA 9/21/50",    True,  False, "+1 BULL"),
            New SignalBreakdownItem("Funding (info)", False, False, "info"),
            New SignalBreakdownItem("OI Delta",       False, False, "NEUTRAL"),
            New SignalBreakdownItem("OFI",            False, True,  "-1 SELL DOMINANT"),
            New SignalBreakdownItem("CVD",            False, False, "FLAT"),
            New SignalBreakdownItem("TFI",            False, False, "NEUTRAL"),
            New SignalBreakdownItem("MicroCVD",       False, True,  "-1 BULL_DECEL"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL"),
            New SignalBreakdownItem("5m EMA(200)",    True,  False, "+1 ABOVE"),
            New SignalBreakdownItem("Donchian(20)",   False, False, "NONE"),
            New SignalBreakdownItem("OBV",            False, False, "FLAT"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "INSIDE_VA"),
            New SignalBreakdownItem("MTF Gate (15m)", True,  False, "PASS [LONG]")
        }
    End Function

    Public Shared Function NoTrade() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         False, False, "FLAT"),
            New SignalBreakdownItem("RSI(9)",         False, False, "RSI 50"),
            New SignalBreakdownItem("DMI +/-DI",      False, False, "balanced"),
            New SignalBreakdownItem("ADX>22",         False, False, "ADX 14 below thr"),
            New SignalBreakdownItem("Volume",         False, False, "1.0x SMA"),
            New SignalBreakdownItem("VWAP",           True,  True,  "±0 at VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, False, "FLAT"),
            New SignalBreakdownItem("EMA 9/21/50",    False, False, "MIXED"),
            New SignalBreakdownItem("Funding (info)", False, False, "info: 0.010%"),
            New SignalBreakdownItem("OI Delta",       False, False, "NEUTRAL"),
            New SignalBreakdownItem("OFI",            False, False, "BALANCED"),
            New SignalBreakdownItem("CVD",            False, False, "FLAT"),
            New SignalBreakdownItem("TFI",            False, False, "NEUTRAL"),
            New SignalBreakdownItem("MicroCVD",       False, False, "FLAT"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL"),
            New SignalBreakdownItem("5m EMA(200)",    True,  False, "+1 ABOVE"),
            New SignalBreakdownItem("Donchian(20)",   False, False, "NONE"),
            New SignalBreakdownItem("OBV",            False, False, "FLAT"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "INSIDE_VA NEUTRAL"),
            New SignalBreakdownItem("MTF Gate (15m)", False, False, "state: FLAT")
        }
    End Function

    Public Shared Function WeakShort() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         False, True,  "-1 weak BEAR ROC"),
            New SignalBreakdownItem("RSI(9)",         False, True,  "-1 RSI 44"),
            New SignalBreakdownItem("DMI +/-DI",      False, True,  "-1 -DI > +DI"),
            New SignalBreakdownItem("ADX>22",         False, True,  "-1 trend just over thr"),
            New SignalBreakdownItem("Volume",         False, False, "0.9x SMA"),
            New SignalBreakdownItem("VWAP",           False, False, "near VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, False, "FLAT"),
            New SignalBreakdownItem("EMA 9/21/50",    False, True,  "-1 BEAR"),
            New SignalBreakdownItem("Funding (info)", False, False, "info"),
            New SignalBreakdownItem("OI Delta",       False, False, "NEUTRAL"),
            New SignalBreakdownItem("OFI",            True,  False, "+1 BUY DOMINANT (snap)"),
            New SignalBreakdownItem("CVD",            False, False, "FLAT"),
            New SignalBreakdownItem("TFI",            False, False, "NEUTRAL"),
            New SignalBreakdownItem("MicroCVD",       True,  False, "+1 BEAR_DECEL"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL"),
            New SignalBreakdownItem("5m EMA(200)",    False, True,  "-1 BELOW"),
            New SignalBreakdownItem("Donchian(20)",   False, False, "NONE"),
            New SignalBreakdownItem("OBV",            False, False, "FLAT"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "INSIDE_VA"),
            New SignalBreakdownItem("MTF Gate (15m)", False, True,  "PASS [SHORT]")
        }
    End Function

    Public Shared Function Short_() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         False, True,  "-1 BEAR ROC"),
            New SignalBreakdownItem("RSI(9)",         False, True,  "-1 RSI 42"),
            New SignalBreakdownItem("DMI +/-DI",      False, True,  "-1 -DI > +DI"),
            New SignalBreakdownItem("ADX>22",         False, True,  "-1 trend"),
            New SignalBreakdownItem("Volume",         False, False, "mid 1.2x"),
            New SignalBreakdownItem("VWAP",           False, True,  "-1 below VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, False, "FLAT"),
            New SignalBreakdownItem("EMA 9/21/50",    False, True,  "-1 BEAR"),
            New SignalBreakdownItem("Funding (info)", False, False, "info: 0.008%"),
            New SignalBreakdownItem("OI Delta",       False, True,  "-1 NEW SHORTS"),
            New SignalBreakdownItem("OFI",            True,  False, "+1 BUY DOMINANT (snap)"),
            New SignalBreakdownItem("CVD",            False, True,  "-1 FALLING"),
            New SignalBreakdownItem("TFI",            False, False, "NEUTRAL"),
            New SignalBreakdownItem("MicroCVD",       True,  False, "+1 BEAR_DECEL late bounce"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL"),
            New SignalBreakdownItem("5m EMA(200)",    False, True,  "-1 BELOW"),
            New SignalBreakdownItem("Donchian(20)",   False, False, "NONE"),
            New SignalBreakdownItem("OBV",            True,  False, "+1 bull divergence"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "INSIDE_VA"),
            New SignalBreakdownItem("MTF Gate (15m)", False, True,  "PASS [SHORT]")
        }
    End Function

    Public Shared Function StrongShort() As List(Of SignalBreakdownItem)
        Return New List(Of SignalBreakdownItem) From {
            New SignalBreakdownItem("ROC(9)",         False, True,  "-1 BEAR ROC falling"),
            New SignalBreakdownItem("RSI(9)",         False, True,  "-1 RSI 36 < 50"),
            New SignalBreakdownItem("DMI +/-DI",      False, True,  "-1 -DI 28 > +DI 14"),
            New SignalBreakdownItem("ADX>22",         False, True,  "-1 trend strength"),
            New SignalBreakdownItem("Volume",         False, True,  "-1 vol 1.6x SMA"),
            New SignalBreakdownItem("VWAP",           False, True,  "-1 below VWAP"),
            New SignalBreakdownItem("BBW/TTM",        False, True,  "-1 BEAR_BUILDING"),
            New SignalBreakdownItem("EMA 9/21/50",    False, True,  "-1 BEAR alignment"),
            New SignalBreakdownItem("Funding (info)", False, False, "info: 0.005% NEUTRAL"),
            New SignalBreakdownItem("OI Delta",       False, True,  "-1 NEW SHORTS"),
            New SignalBreakdownItem("OFI",            False, True,  "-1 SELL DOMINANT"),
            New SignalBreakdownItem("CVD",            False, True,  "-1 FALLING"),
            New SignalBreakdownItem("TFI",            False, True,  "-1 SELL PRESSURE"),
            New SignalBreakdownItem("MicroCVD",       False, True,  "-1 BEAR_ACCEL"),
            New SignalBreakdownItem("Liq Penalty",    False, False, "—"),
            New SignalBreakdownItem("Spread",         False, False, "NORMAL 1.0 bps"),
            New SignalBreakdownItem("5m EMA(200)",    False, True,  "-1 price BELOW"),
            New SignalBreakdownItem("Donchian(20)",   False, True,  "-1 breakdown"),
            New SignalBreakdownItem("OBV",            False, True,  "-1 FALLING"),
            New SignalBreakdownItem("VPFR-lite",      False, False, "NEUTRAL"),
            New SignalBreakdownItem("MTF Gate (15m)", False, True,  "PASS [SHORT] vs BEAR trend")
        }
    End Function

End Class
