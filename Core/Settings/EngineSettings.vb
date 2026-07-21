' Core/Settings/EngineSettings.vb
' Typed model that mirrors settings.json.
' All hardcoded indicator/scoring thresholds are accessed via this class.
' Populated by SettingsLoader.Load() -- do not instantiate directly.
'
' v0.30: Added CvdSettings class and CVD property on IndicatorSettings.
'        Added RSI.DivergencePriceGate, RSI.DivergenceRsiDelta.
'        Added OBV.TrendGate, OBV.DivergenceGate.
'        Added ROC.SeriesLookback.
'        Added OI.ChangeThresholdPct.
'        Added ScoringSettings.VerdictStrongPct/MedPct/WeakPct.
' v0.32: VwapSettings expanded with session timing and warmup threshold.
'        Session1StartHour/Minute  -- daily session reset (default 00:00 UTC).
'        Session2StartHour/Minute  -- US session reset (default 13:30 UTC).
'        WarmupCandles             -- min candles before VWAP score is trusted (default 15).
' v0.33: Added MTFGateSettings class and MTFGate property on EngineSettings.
'        Controls the 15m multi-timeframe confluence gate (DMI/ADX/EMA 2-of-3 majority vote).
' v0.34: Removed dead RSI fields from MTFGateSettings (RsiPeriod, RsiBullZone, RsiBearZone).
' v0.35: Added AutoRunSettings class and AutoRun property on EngineSettings.
' v0.36: [P4] v0.48: Added TfiSettings and MicroCvdSettings.
' v0.37: [P10/P11/P12] v0.49: Added penalty/multiplier fields to ScoringSettings.
' v0.38: [P13] v0.50: RSI div penalty thresholds, CVD/MicroCVD penalty magnitudes.
' refactor: Removed ScoringWeights class and Weights property (never consumed by scoring engine).
'           Removed 6 dead integer threshold fields from ScoringSettings
'           (LongThreshold, ShortThreshold, Strong/Medium variants) -- superseded by
'           VerdictStrongPct/MedPct/WeakPct since v0.30.
' T1-D: Added 6 HoldStatus exit threshold fields to ScoringSettings.
'        CalcHoldStatus previously used hardcoded 0.6/-0.6 (ROC) and 60/40 (RSI) literals.
'        All defaults preserve prior behaviour exactly.
' fix [T3-B]: Added RsiSettings.PivotWing and RsiSettings.LookbackBars.
'        CalcRSIDivergence was wired to these cfg keys in MainForm_Analysis.vb [T3-B]
'        but the properties were missing from RsiSettings -- caused BC30456.
'        Defaults match the method''s previous hardcoded values (PivotWing=2, LookbackBars=20).
' fix [T3-C]: Added TtmSettings class and IndicatorSettings.TTM property.
'        CalcTTMSqueeze was wired to cfg.Indicators.TTM.FlatThreshold in MainForm_Analysis.vb
'        but the class and property were missing -- caused BC30456.
'        Default FlatThreshold=0.5 matches the method''s previous hardcoded default.
' fix [T3-A]: Added VpfrSettings class and IndicatorSettings.VPFR property.
'        CalcVPFRLite was wired to cfg.Indicators.VPFR.NumBuckets in MainForm_Analysis.vb
'        but the class and property were missing -- caused BC30456.
'        Default NumBuckets=50 matches the method''s previous hardcoded default.
' Step 5b: Added ContextTagStructuralMin and ContextTagFlowMax to ScoringSettings.
'        Used by CalcVerdictContext() in ScoringEngine_Calculate to classify weak/ambiguous
'        verdicts as FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED.
'        Defaults: ContextTagStructuralMin=3, ContextTagFlowMax=1.
' Kelly: Added KellySettings class and Kelly property on EngineSettings.
'        Used by CalcKellySizing() in MainForm_Render for display-only position sizing advisory.
'        Defaults: AccountSizeUsd=1000, UseHalfKelly=True, MaxRiskFraction=0.05,
'                  ContractFaceUsd=10, EstProbFloor=0.45, EstProbScale=0.20.
'        CAL mode removed — EST mode only until backtesting module ships.
' funding-momentum: Added FundingSettings class and Indicators.Funding property.
'        Used by CalcFundingMomentum() in Indicators_OrderFlow and Step 3b in
'        ScoringEngine_Calculate. MomentumEnabled master switch (default True).
'        MomentumWindow=3, MomentumThreshold=0.0001, MomentumAmplify=1, MomentumSoften=1.
' OI x CVD cross-confirm (Pass 2b): Added OiCvdSettings class and Indicators.OiCvd property.
'        Used by Pass 2b gate in ScoringEngine_Calculate_Scoring.
'        Enabled master switch (default True).
'        UpgradeBonus=1 (confirmed cross), ConflictPenalty=1 (opposed cross).
' Session-aware volume norms: Added SessionVolumeSettings class and SessionVolume property on EngineSettings.
'        Used by DynamicNorms.Compute() to scale volume thresholds by UTC session bucket.
'        Supports ASIA / LONDON / NY buckets with per-session high/mid multipliers.
' Pass 2c regime alignment gate: Added RegimeWeightSettings (with nested RegimeAlignSettings) and
'        RegimeWeights property on EngineSettings. Controls RunRegimeAlignmentPass() in
'        ScoringEngine_Calculate_Scoring. Enabled (default True).
'        Trending: EMA ribbon + ROC (threshold-gated) + CVD gate. AlignmentBonus=1, ConflictPenalty=1.
'        RangeBound: VWAP dev (if not warmup) + RSI(9) vs 50 + Donchian. Same defaults.
'        RegimeMaxScore() now takes cfg param; MaxScore bumped +AlignmentBonus for TRENDING/RANGE_BOUND.

Imports System.Text.Json.Serialization

Public Class EngineSettings
    <JsonPropertyName("version")>
    Public Property Version As Integer = 1

    <JsonPropertyName("last_modified")>
    Public Property LastModified As String = ""

    <JsonPropertyName("modified_by")>
    Public Property ModifiedBy As String = "manual"

    <JsonPropertyName("change_log")>
    Public Property ChangeLog As New List(Of String)

    <JsonPropertyName("indicators")>
    Public Property Indicators As New IndicatorSettings

    <JsonPropertyName("scoring")>
    Public Property Scoring As New ScoringSettings

    <JsonPropertyName("regime_gates")>
    Public Property RegimeGates As New RegimeGateSettings

    <JsonPropertyName("mtf_gate")>
    Public Property MTFGate As New MTFGateSettings

    <JsonPropertyName("auto_run")>
    Public Property AutoRun As New AutoRunSettings

    <JsonPropertyName("kelly")>
    Public Property Kelly As New KellySettings

    <JsonPropertyName("session_volume")>
    Public Property SessionVolume As New SessionVolumeSettings

    ''' <summary>
    ''' [v36 session-timeframe-resolution] Per-resolution override map keyed by the
    ''' resolution string ("1"/"3"/"5"). "1" is empty (everything inherits the global
    ''' values); "3" carries only the overridden ROC keys. A pure override-map: any key
    ''' absent from the active profile falls back to the global value. Default-empty dict
    ''' so an absent block = pure 1-min behaviour. Spec §2.2.
    ''' </summary>
    <JsonPropertyName("resolution_profiles")>
    Public Property ResolutionProfiles As Dictionary(Of String, ResolutionProfile) = New Dictionary(Of String, ResolutionProfile)

    <JsonPropertyName("regime_weights")>
    Public Property RegimeWeights As New RegimeWeightSettings

    <JsonPropertyName("network")>
    Public Property Network As New NetworkSettings

    <JsonPropertyName("analysis_logging")>
    Public Property AnalysisLogging As New AnalysisLoggingSettings

    <JsonPropertyName("performance_display")>
    Public Property PerformanceDisplay As New PerformanceDisplaySettings

    ''' <summary>[P4 #1 realtime-exit-guard] Display/alert-only exit-guard overlay parameters.</summary>
    <JsonPropertyName("exit_guard")>
    Public Property ExitGuard As New ExitGuardSettings

    ''' <summary>[P4 #3 live-microstructure-strip] Display-only live TAPE strip parameters.</summary>
    <JsonPropertyName("live_strip")>
    Public Property LiveStrip As New LiveStripSettings

    ''' <summary>[Signal Bridge v1] verdict_signal.json emission to the order app
    ''' (docs/signal-bridge-v1-proposal.md — schema v1 FROZEN 2026-07-03). Transport
    ''' plumbing, zero scoring impact; OFF the auto-tweaker surface (HARD CONSTRAINT 18).</summary>
    <JsonPropertyName("signal_bridge")>
    Public Property SignalBridge As New SignalBridgeSettings
End Class

' ---------------------------------------------------------------------------
' Indicator parameter bags
' ---------------------------------------------------------------------------

Public Class IndicatorSettings
    <JsonPropertyName("ADX")>      Public Property ADX      As New AdxSettings
    <JsonPropertyName("RSI")>      Public Property RSI      As New RsiSettings
    <JsonPropertyName("ROC")>      Public Property ROC      As New RocSettings
    <JsonPropertyName("VWAP")>     Public Property VWAP     As New VwapSettings
    <JsonPropertyName("BBW")>      Public Property BBW      As New BbwSettings
    <JsonPropertyName("EMA")>      Public Property EMA      As New EmaSettings
    <JsonPropertyName("Donchian")> Public Property Donchian As New DonchianSettings
    <JsonPropertyName("OBV")>      Public Property OBV      As New ObvSettings
    <JsonPropertyName("ATR")>      Public Property ATR      As New AtrSettings
    <JsonPropertyName("OFI")>      Public Property OFI      As New OfiSettings
    <JsonPropertyName("Volume")>   Public Property Volume   As New VolumeSettings
    <JsonPropertyName("VWAPDynamic")> Public Property VWAPDynamic As New VwapDynamicSettings
    <JsonPropertyName("Liquidations")> Public Property Liquidations As New LiquidationSettings
    <JsonPropertyName("OI")>       Public Property OI       As New OiSettings
    <JsonPropertyName("DMI")>      Public Property DMI      As New DmiSettings
    <JsonPropertyName("CVD")>      Public Property CVD      As New CvdSettings
    <JsonPropertyName("TFI")>      Public Property TFI      As New TfiSettings
    <JsonPropertyName("MicroCVD")> Public Property MicroCVD As New MicroCvdSettings
    ''' <summary>[T3-C] TTM Squeeze tuning parameters.</summary>
    <JsonPropertyName("TTM")>      Public Property TTM      As New TtmSettings
    ''' <summary>[T3-A] VPFR-lite tuning parameters.</summary>
    <JsonPropertyName("VPFR")>     Public Property VPFR     As New VpfrSettings
    ''' <summary>[funding-momentum] Funding rate momentum signal parameters.</summary>
    <JsonPropertyName("funding")>  Public Property Funding  As New FundingSettings
    ''' <summary>[OI x CVD cross-confirm] Pass 2b upgrade/conflict gate parameters.</summary>
    <JsonPropertyName("oi_cvd_cross")> Public Property OiCvd As New OiCvdSettings
    ''' <summary>[bid-ask-spread] Bid-ask spread microstructure thresholds.</summary>
    <JsonPropertyName("spread")> Public Property Spread As New SpreadSettings
    ''' <summary>[swing-pivots] Swing pivot detection parameters for 5m primary and 15m context.</summary>
    <JsonPropertyName("swing")>  Public Property Swing  As New SwingSettings
    ''' <summary>[d1-trend-structure] HH/HL/LH/LL classification + Pass 2c structure bonus parameters.</summary>
    <JsonPropertyName("trend_structure")> Public Property TrendStructure As New TrendStructureSettings
    ''' <summary>[P4 #5] Aggressor velocity / tape burst parameters (docs/aggressor-velocity-proposal.md §6).</summary>
    <JsonPropertyName("aggressor_velocity")> Public Property AggressorVelocity As New AggressorVelocitySettings
    ''' <summary>[P4 #6] Book absorption at structural levels (docs/book-absorption-proposal.md §6).</summary>
    <JsonPropertyName("absorption")> Public Property Absorption As New AbsorptionSettings
End Class

Public Class AdxSettings
    <JsonPropertyName("period")>          Public Property Period         As Integer = 9
    <JsonPropertyName("trend_threshold")> Public Property TrendThreshold As Double  = 25.0
    <JsonPropertyName("range_threshold")> Public Property RangeThreshold As Double  = 20.0
End Class

Public Class RsiSettings
    <JsonPropertyName("period")>             Public Property Period             As Integer = 9
    <JsonPropertyName("oversold")>           Public Property Oversold           As Double  = 40.0
    <JsonPropertyName("overbought")>         Public Property Overbought         As Double  = 60.0
    <JsonPropertyName("partial_oversold")>   Public Property PartialOversold    As Double  = 45.0
    <JsonPropertyName("partial_overbought")> Public Property PartialOverbought  As Double  = 55.0
    <JsonPropertyName("divergence_price_gate")> Public Property DivergencePriceGate As Double = 0.001
    <JsonPropertyName("divergence_rsi_delta")>  Public Property DivergenceRsiDelta  As Double = 5.0
    ''' <summary>[P13] v0.50: RSI level above which BEARISH div triggers long penalty. Default 65.</summary>
    <JsonPropertyName("div_penalty_rsi_high")> Public Property DivPenaltyRsiHigh As Double = 65.0
    ''' <summary>[P13] v0.50: RSI level below which BULLISH div triggers short penalty. Default 35.</summary>
    <JsonPropertyName("div_penalty_rsi_low")>  Public Property DivPenaltyRsiLow  As Double = 35.0
    ''' <summary>[T3-B] Half-width of the pivot detection window for RSI divergence scan. Default 2.</summary>
    <JsonPropertyName("pivot_wing")>           Public Property PivotWing         As Integer = 2
    ''' <summary>[T3-B] Number of bars to look back when scanning for RSI pivots. Default 20.</summary>
    <JsonPropertyName("lookback_bars")>        Public Property LookbackBars      As Integer = 20
    ''' <summary>[settings-exposure] RSI midline for Pass 2c RANGE_BOUND alignment check. Default 50.</summary>
    <JsonPropertyName("pass2c_midline")>       Public Property Pass2cMidline     As Double  = 50.0
    ''' <summary>[v20] Pivot RSI must be at or above this for BEARISH divergence to fire. Default 65.0.</summary>
    <JsonPropertyName("divergence_overbought_threshold")> Public Property DivergenceOverboughtThreshold As Double = 65.0
    ''' <summary>[v20] Pivot RSI must be at or below this for BULLISH divergence to fire. Default 35.0.</summary>
    <JsonPropertyName("divergence_oversold_threshold")>   Public Property DivergenceOversoldThreshold   As Double = 35.0
End Class

Public Class RocSettings
    <JsonPropertyName("period")>                 Public Property Period               As Integer = 9
    ''' <summary>
    ''' [v20] Threshold for ROCSlope delta classification (ROC change between consecutive samples).
    ''' delta > this → RISING; delta &lt; -this → FALLING; else FLAT. Default 0.05.
    ''' Was conflated with MagnitudeThreshold under slope_sensitivity in v18/v19.
    ''' </summary>
    <JsonPropertyName("slope_delta_threshold")>  Public Property SlopeDeltaThreshold  As Double  = 0.05
    ''' <summary>
    ''' [v20] Threshold for ROC magnitude in partial scoring (rocPartialLong/Short),
    ''' Pass 2c regime-alignment activation, and spread penalty direction. Default 0.1.
    ''' Was conflated with SlopeDeltaThreshold under slope_sensitivity in v18/v19.
    ''' </summary>
    <JsonPropertyName("magnitude_threshold")>    Public Property MagnitudeThreshold   As Double  = 0.1
    <JsonPropertyName("series_lookback")>        Public Property SeriesLookback       As Integer = 3
End Class

Public Class VwapSettings
    <JsonPropertyName("session2_start_hour")>    Public Property Session2StartHour    As Integer = 13
    <JsonPropertyName("session2_start_minute")>  Public Property Session2StartMinute  As Integer = 30
    <JsonPropertyName("warmup_candles")>         Public Property WarmupCandles        As Integer = 15
End Class

Public Class BbwSettings
    <JsonPropertyName("period")>  Public Property Period As Integer = 20
    <JsonPropertyName("std_dev")> Public Property StdDev As Double  = 2.0
    ''' <summary>[settings-exposure] BBW series window = period × multiplier. Default 5 (period 20 × 5 = 100 bars).</summary>
    <JsonPropertyName("series_window_multiplier")> Public Property SeriesWindowMultiplier As Integer = 5
    ''' <summary>[settings-exposure] Percentile of BBW series below which SqueezeStatus = ACTIVE. Default 0.20 (bottom 20%).</summary>
    <JsonPropertyName("squeeze_percentile")>       Public Property SqueezePercentile      As Double  = 0.20
End Class

Public Class EmaSettings
    <JsonPropertyName("fast")> Public Property Fast As Integer = 9
    <JsonPropertyName("mid")>  Public Property Mid  As Integer = 21
    <JsonPropertyName("slow")> Public Property Slow As Integer = 50
End Class

Public Class DonchianSettings
    <JsonPropertyName("period")>       Public Property Period      As Integer = 20
    ''' <summary>[settings-exposure] Quartile threshold for LONG_PARTIAL / SHORT_PARTIAL (fraction of channel range). Default 0.25.</summary>
    <JsonPropertyName("quartile_pct")> Public Property QuartilePct As Double  = 0.25
End Class

Public Class ObvSettings
    ''' <summary>Units since v31: net OBV drift in average-bar-volumes over the window (F5 normalisation). Seeded, not calibrated.</summary>
    <JsonPropertyName("trend_gate")>      Public Property TrendGate      As Double = 10.0
    <JsonPropertyName("divergence_gate")> Public Property DivergenceGate As Double = 0.001
End Class

Public Class AtrSettings
    <JsonPropertyName("period")>     Public Property Period    As Integer = 7
    ''' <summary>Cold-start fallback for DynamicNorms.ComputeATRRef only (live ATR ref
    ''' self-calibrates). Aligned to live v37 (38.0 — the 1-min ATR mean at BTC ~$62-67k;
    ''' the settings-only v37 flip deliberately rode "the next code commit" — this is it, B4b).</summary>
    <JsonPropertyName("static_ref")> Public Property StaticRef As Double  = 38.0
    <JsonPropertyName("scale_min")>  Public Property ScaleMin  As Double  = 0.25
    <JsonPropertyName("scale_max")>  Public Property ScaleMax  As Double  = 4.0
End Class

Public Class OfiSettings
    <JsonPropertyName("book_depth")>          Public Property BookDepth         As Integer = 5
    ' Aligned to live v48 (geometric-distribution re-baseline 1.60/0.625 — the defaults
    ' deliberately rode "the next code commit" per that entry; this is it).
    <JsonPropertyName("buy_dominant_ratio")>  Public Property BuyDominantRatio  As Double  = 1.60
    <JsonPropertyName("sell_dominant_ratio")> Public Property SellDominantRatio As Double  = 0.625
    ''' <summary>Master switch for the OFI momentum SCORING modifier. Default False —
    ''' RETIRED v50 (signal-health-retune R1: active ~90% of runs in every era = no
    ''' discrimination; unfixable by threshold; conditional outcomes adverse). The
    ''' CalcOFIMomentum computation, r.OFIMomentum, the CSV column, and the MOM:state
    ''' note segment all still populate (free diagnostics); only the score modifier is
    ''' off. Fenced off the tweaker surface with its momentum_ siblings (HC 20) —
    ''' reversal = flip this flag + drop the fence in one commit.</summary>
    <JsonPropertyName("momentum_enabled")>   Public Property MomentumEnabled   As Boolean = False
    ''' <summary>Lookback sample count for OFI ratio delta. Default 3.</summary>
    <JsonPropertyName("momentum_window")>    Public Property MomentumWindow    As Integer = 3
    ''' <summary>Min absolute delta to classify as RISING/FALLING. Default 0.15.</summary>
    <JsonPropertyName("momentum_threshold")> Public Property MomentumThreshold As Double  = 0.15
    ''' <summary>Bonus added when OFI level and momentum confirm. Capped at regimeMax. Default 1.</summary>
    <JsonPropertyName("momentum_bonus")>     Public Property MomentumBonus     As Integer = 1
    ''' <summary>[P4 #4 v46] Master switch for time-averaged OFI on the WS path. Default True.
    ''' A feature flag, NOT a threshold → OFF the auto-tweaker surface (exact-match rejected in
    ''' SettingsDiffApplier + PromptBuilder HARD CONSTRAINT 16). Exposed + hand-toggleable.
    ''' False reverts to the snapshot OFI hot (the rollback path).</summary>
    <JsonPropertyName("averaging_enabled")>  Public Property AveragingEnabled  As Boolean = True
    ''' <summary>[P4 #4 v46] Time-averaging window / time-aware EMA horizon, seconds. Default 10.
    ''' ON the tweaker surface — it shapes the OFI signal (a genuine failure-rate lever); changing
    ''' it shifts the OFIRatio distribution, so a manual change should be followed by a dominance-
    ''' ratio re-check (proposal §5 coupling caveat).</summary>
    <JsonPropertyName("avg_window_sec")>     Public Property AvgWindowSec      As Integer = 10
End Class

Public Class VolumeSettings
    <JsonPropertyName("sma_period")>             Public Property SmaPeriod           As Integer = 9
    <JsonPropertyName("static_high")>            Public Property StaticHigh          As Double  = 3.0
    <JsonPropertyName("static_mid")>             Public Property StaticMid           As Double  = 2.0
    <JsonPropertyName("dynamic_high_clamp_min")> Public Property DynamicHighClampMin As Double  = 2.0
    <JsonPropertyName("dynamic_high_clamp_max")> Public Property DynamicHighClampMax As Double  = 6.0
    <JsonPropertyName("dynamic_mid_clamp_min")>  Public Property DynamicMidClampMin  As Double  = 1.5
    <JsonPropertyName("dynamic_mid_clamp_max")>  Public Property DynamicMidClampMax  As Double  = 4.0
End Class

Public Class VwapDynamicSettings
    <JsonPropertyName("dev_clamp_min")>   Public Property DevClampMin    As Double = 0.30
    <JsonPropertyName("dev_clamp_max")>   Public Property DevClampMax    As Double = 3.0
    <JsonPropertyName("static_fallback")> Public Property StaticFallback As Double = 1.5
End Class

Public Class LiquidationSettings
    <JsonPropertyName("large_liq_size")>  Public Property LargeLiqSize   As Double = 200.0
    <JsonPropertyName("dominance_ratio")> Public Property DominanceRatio As Double = 2.0
End Class

Public Class OiSettings
    <JsonPropertyName("neutral_band_pct")>     Public Property NeutralBandPct     As Double = 0.05
    ' Aligned to live v30 (was 0.01; lowered to 0.003 in v19, 0.002 in v20).
    <JsonPropertyName("change_threshold_pct")> Public Property ChangeThresholdPct As Double = 0.002
End Class

Public Class DmiSettings
    <JsonPropertyName("period")> Public Property Period As Integer = 9
End Class

Public Class CvdSettings
    <JsonPropertyName("slope_min_usd")>         Public Property SlopeMinUsd         As Double  = 12000.0
    <JsonPropertyName("slope_pct_of_value")>    Public Property SlopePctOfValue     As Double  = 0.01
    <JsonPropertyName("divergence_price_gate")> Public Property DivergencePriceGate As Double  = 0.0005
    ''' <summary>[P13] v0.50: Score penalty magnitude for CVD divergence. Default 1.</summary>
    <JsonPropertyName("divergence_penalty")>    Public Property DivergencePenalty   As Integer = 1
    ''' <summary>[settings-exposure] Late-segment weight in 3-segment CVD slope formula. Default 2.0.</summary>
    <JsonPropertyName("late_segment_weight")>   Public Property LateSegmentWeight   As Double  = 2.0
    ''' <summary>[settings-exposure] Early-segment weight in 3-segment CVD slope formula. Default 1.0.</summary>
    <JsonPropertyName("early_segment_weight")>  Public Property EarlySegmentWeight  As Double  = 1.0
End Class

''' <summary>[P4] v0.48: TFI window independent of MicroCVD. Default 30 trades.</summary>
Public Class TfiSettings
    <JsonPropertyName("window_size")> Public Property WindowSize As Integer = 30
    <JsonPropertyName("threshold")>   Public Property Threshold  As Double  = 0.15
End Class

''' <summary>
''' [P4] v0.48: MicroCVD window independent of TFI. Default 50 trades.
''' [P13] v0.50: DecelPenalty -- opposing-side penalty magnitude. Default 1.
''' </summary>
Public Class MicroCvdSettings
    <JsonPropertyName("window_size")>     Public Property WindowSize     As Integer = 50
    ''' <summary>Static USD acceleration threshold. Used as floor anchor in dynamic mode. Default 10000.</summary>
    <JsonPropertyName("accel_threshold")> Public Property AccelThreshold As Double  = 10000.0
    ''' <summary>
    ''' Dynamic acceleration threshold as fraction of total window USD flow. Default 0.03 (3%).
    ''' Set to 0.0 to disable dynamic mode and use accel_threshold as a literal static value.
    ''' </summary>
    <JsonPropertyName("accel_threshold_dynamic_pct")> Public Property AccelThresholdDynamicPct As Double = 0.03
    ''' <summary>
    ''' Floor on dynamic threshold as fraction of accel_threshold. Default 0.25 (25%).
    ''' Prevents dead-flow windows from producing nonsensically small thresholds.
    ''' </summary>
    <JsonPropertyName("accel_threshold_floor_pct")>   Public Property AccelThresholdFloorPct   As Double = 0.25
    <JsonPropertyName("decel_penalty")>   Public Property DecelPenalty   As Integer = 1
End Class

''' <summary>
''' [T3-C] TTM Squeeze tuning parameters.
''' FlatThreshold: histogram bars whose absolute value is below this are treated as FLAT momentum.
''' Default 0.5 matches the method''s previous hardcoded behaviour.
''' </summary>
Public Class TtmSettings
    <JsonPropertyName("flat_threshold")> Public Property FlatThreshold As Double  = 0.5
    ''' <summary>[settings-exposure] SMA period for TTM histogram delta computation. Default 20.</summary>
    <JsonPropertyName("sma_period")>     Public Property SmaPeriod     As Integer = 20
    ''' <summary>[settings-exposure] Linear regression period for histogram fit. Default 7.</summary>
    <JsonPropertyName("lin_reg_period")> Public Property LinRegPeriod  As Integer = 7
End Class

''' <summary>
''' [T3-A] VPFR-lite tuning parameters.
''' NumBuckets: number of price buckets for the volume profile histogram.
''' Default 50 matches the method''s previous hardcoded behaviour.
''' </summary>
Public Class VpfrSettings
    <JsonPropertyName("num_buckets")>      Public Property NumBuckets      As Integer = 50
    ''' <summary>Fraction of total volume defining the value area. Default 0.70 (industry standard).</summary>
    <JsonPropertyName("value_area_pct")>   Public Property ValueAreaPct    As Double  = 0.70
    ''' <summary>Bucket vol / POC vol ratio above which a bucket is HVN. Default 0.6.</summary>
    <JsonPropertyName("hvn_vol_pct")>      Public Property HvnVolPct       As Double  = 0.6
    ''' <summary>Bucket vol / POC vol ratio below which a bucket is LVN. Default 0.2.</summary>
    <JsonPropertyName("lvn_vol_pct")>      Public Property LvnVolPct       As Double  = 0.2
    ''' <summary>Price proximity threshold for VPFRHVNearPoc / VPFRSignal classification. Default 0.002 (0.2%).</summary>
    <JsonPropertyName("hvn_proximity_pct")> Public Property HvnProximityPct As Double = 0.002
    ''' <summary>Exponential decay base for time-weighting candle volumes. Default 0.985 (~22% per 15 bars).</summary>
    <JsonPropertyName("decay_base")>       Public Property DecayBase       As Double  = 0.985
    ''' <summary>Enable optional value-area-breakout partial in scoring pipeline. Default False -- display + cap only.</summary>
    <JsonPropertyName("value_area_scoring_enabled")> Public Property ValueAreaScoringEnabled As Boolean = False
End Class

''' <summary>
''' [P4 #5 aggressor velocity] Tape-burst parameters (docs/aggressor-velocity-proposal.md §6).
''' Two time-decayed taker-USD horizons (fast burst vs rolling norm) → burstRatio + lean →
''' BURST_BUY / BURST_SELL / NORMAL. The v50 build sub-version was display/CSV-only; the v52
''' wire-in flipped ScoringEnabled True after the §5.1 correlation gate cleared (NOT redundant)
''' and the §5.2 per-session derivation set NY burst_ratio_threshold 4.5.
''' Three-tier tweaker surface (§6 — who changes each key, not whether it's exposed):
'''   ON the surface  — FastWindowSec, DirectionLeanFloor, GrossFloorUsdPerSec,
'''                     UpgradeBonus, ContraPenalty (flat dotted paths; the scoring
'''                     magnitudes become live levers once scoring is on).
'''   OFF, hand-tuned — Defaults + Sessions (per-session norm_window_sec /
'''                     burst_ratio_threshold — §5.2 re-baseline tier, HC11 class;
'''                     SettingsDiffApplier rejects the default./sessions. prefixes).
'''   OFF, hand-toggle — Enabled + ScoringEnabled (exact-match rejects + PromptBuilder
'''                     HARD CONSTRAINT 19, mirroring OFI.averaging_enabled / HC16).
''' </summary>
Public Class AggressorVelocitySettings
    ''' <summary>Feature switch — accumulator folds + reads stop entirely when False. Default True.</summary>
    <JsonPropertyName("enabled")>                 Public Property Enabled             As Boolean = True
    ''' <summary>The ⚠ scoring gate — armed at the v52 wire-in after the §5.1 correlation gate
    ''' cleared. When True the TFI burst modifier (§4.5) fires, but ONLY for sessions carrying an
    ''' explicit sessions[s].burst_ratio_threshold (S2a scoping — NY today). Default True (POCO rides
    ''' the v52 commit); False is the hot rollback to byte-identical no-modifier behaviour.</summary>
    <JsonPropertyName("scoring_enabled")>         Public Property ScoringEnabled      As Boolean = True
    ''' <summary>Burst horizon tau (seconds). 5s — sub-bar and leading, but long enough that a
    ''' single block doesn't dominate the rate (§10.3). Default 5.</summary>
    <JsonPropertyName("fast_window_sec")>         Public Property FastWindowSec       As Double = 5.0
    ''' <summary>Min |lean| to assign a direction — a balanced firehose stays NORMAL. Default 0.2.</summary>
    <JsonPropertyName("direction_lean_floor")>    Public Property DirectionLeanFloor  As Double = 0.2
    ''' <summary>Dead-tape guard on the norm (USD/sec) — stops a single print on a dead tape
    ''' reading as an infinite burst. Default 50.</summary>
    <JsonPropertyName("gross_floor_usd_per_sec")> Public Property GrossFloorUsdPerSec As Double = 50.0
    ''' <summary>TFI-modifier upgrade magnitude once scoring is on (inert at the build). Default 1.</summary>
    <JsonPropertyName("upgrade_bonus")>           Public Property UpgradeBonus        As Integer = 1
    ''' <summary>Contra-burst soften magnitude once scoring is on (inert at the build). Default 1.</summary>
    <JsonPropertyName("contra_penalty")>          Public Property ContraPenalty       As Integer = 1
    ''' <summary>Shared fallback for the per-session tier (norm window + burst threshold).</summary>
    <JsonPropertyName("default")>                 Public Property Defaults            As New AggressorVelocityDefaults
    ''' <summary>Per-session overrides keyed by session bucket name (null field ⇒ inherit
    ''' Defaults). Hand-tuned tier — OFF the tweaker surface. Seeded with NY 60s (dense 1-min
    ''' tape → shorter baseline) + the v52 §5.2 NY burst_ratio_threshold 4.5 so the code-defaults
    ''' path matches settings.json (S2a: the explicit NY threshold is ALSO the scoping key — its
    ''' presence is what arms the TFI modifier for NY, and its absence keeps res-3 inert).</summary>
    <JsonPropertyName("sessions")>                Public Property Sessions            As Dictionary(Of String, AggressorVelocitySessionOverride) =
        New Dictionary(Of String, AggressorVelocitySessionOverride) From {
            {"NY",     New AggressorVelocitySessionOverride With {.NormWindowSec = 60.0, .BurstRatioThreshold = 4.5}},
            {"LONDON", New AggressorVelocitySessionOverride()},
            {"ASIA",   New AggressorVelocitySessionOverride()}
        }
End Class

''' <summary>[P4 #5] The shared default tier: norm horizon 120s (a 3-min-session baseline;
''' NY overrides to 60) + burst_ratio_threshold 2.5. Pre-calibration anchors — §5 refines.</summary>
Public Class AggressorVelocityDefaults
    <JsonPropertyName("norm_window_sec")>       Public Property NormWindowSec       As Double = 120.0
    <JsonPropertyName("burst_ratio_threshold")> Public Property BurstRatioThreshold As Double = 2.5
End Class

''' <summary>[P4 #5] Nullable per-session override — Nothing ⇒ inherit AggressorVelocityDefaults
''' (the v40 per-session ROC override pattern). Hand-tuned, off the tweaker surface (HC11 class).</summary>
Public Class AggressorVelocitySessionOverride
    <JsonPropertyName("norm_window_sec")>       Public Property NormWindowSec       As Double? = Nothing
    <JsonPropertyName("burst_ratio_threshold")> Public Property BurstRatioThreshold As Double? = Nothing
End Class

''' <summary>
''' [P4 #6] Book absorption at structural levels — level-scoped episode tracker
''' (docs/book-absorption-proposal.md §6). All shipped anchors are PROVISIONAL until the
''' §5 target-engagement calibration pass. Three-tier tweaker surface (per the #5 §6
''' pattern — HARD CONSTRAINT 23):
'''   ON  surface — ProximityTicks, BandTicks, WindowSec, BreakTolTicks, AbsorbRatio,
'''                 DepletionFloorUsd, MaxPullFrac, Penalty.
'''   OFF, hand-tuned — Defaults + Sessions (per-session min_aggr_usd — the §5
'''                 target-engagement re-baseline tier, HC11 class; SettingsDiffApplier
'''                 rejects the default./sessions. prefixes).
'''   OFF, hand-toggle — Enabled + ScoringEnabled (exact-match rejects; ScoringEnabled
'''                 is the twice-evidence-gated ⚠ activation, proposal §5).
''' </summary>
Public Class AbsorptionSettings
    ''' <summary>Feature switch — feed folds + reads stop entirely when False (byte-identical
    ''' to pre-build). Default True.</summary>
    <JsonPropertyName("enabled")>             Public Property Enabled           As Boolean = True
    ''' <summary>The ⚠ activation gate — stays False at the build. Flipped only after BOTH §5
    ''' gates clear (independence AND a ≥10 pp adverse outcome gradient on n≥30 flagged rows).</summary>
    <JsonPropertyName("scoring_enabled")>     Public Property ScoringEnabled    As Boolean = False
    ''' <summary>ACTIVE gate distance (ticks) from the watched level — the tracker measures
    ''' only while the touch is within this of the level (§4.1). Default 12.</summary>
    <JsonPropertyName("proximity_ticks")>     Public Property ProximityTicks    As Integer = 12
    ''' <summary>Level band width (ticks) for resting-size tracking + fill assignment. Default 4.</summary>
    <JsonPropertyName("band_ticks")>          Public Property BandTicks         As Integer = 4
    ''' <summary>Rolling pressing-volume window (seconds). Default 10.</summary>
    <JsonPropertyName("window_sec")>          Public Property WindowSec         As Double = 10.0
    ''' <summary>Progress tolerance (ticks) beyond the level — a print past it ends the
    ''' episode instantly (a broken level never carries a stale ABSORB). Default 2.</summary>
    <JsonPropertyName("break_tol_ticks")>     Public Property BreakTolTicks     As Integer = 2
    ''' <summary>Classification threshold: pressing USD per USD net band depletion. Default 3.0.</summary>
    <JsonPropertyName("absorb_ratio")>        Public Property AbsorbRatio       As Double = 3.0
    ''' <summary>Divide-by-nothing guard on the depletion denominator (and the D8 postLB
    ''' denominator). Default 25000 USD.</summary>
    <JsonPropertyName("depletion_floor_usd")> Public Property DepletionFloorUsd As Double = 25000.0
    ''' <summary>D8 spoof-guard veto: provable pulls / provable posts above this ⇒ painted
    ''' defense ⇒ NONE. Default 0.5.</summary>
    <JsonPropertyName("max_pull_frac")>       Public Property MaxPullFrac       As Double = 0.5
    ''' <summary>Scoring magnitude once activated (inert at the build). Default 1.</summary>
    <JsonPropertyName("penalty")>             Public Property Penalty           As Integer = 1
    ''' <summary>Shared fallback for the per-session tier (min_aggr_usd).</summary>
    <JsonPropertyName("default")>             Public Property Defaults          As New AbsorptionDefaults
    ''' <summary>Per-session overrides keyed by session bucket name (null field ⇒ inherit
    ''' Defaults). Hand-tuned tier — OFF the tweaker surface. min_aggr_usd is expected to
    ''' split per-session at the §5 calibration (NY tape ≫ Asia); seeded empty.</summary>
    <JsonPropertyName("sessions")>            Public Property Sessions          As Dictionary(Of String, AbsorptionSessionOverride) =
        New Dictionary(Of String, AbsorptionSessionOverride) From {
            {"NY",     New AbsorptionSessionOverride()},
            {"LONDON", New AbsorptionSessionOverride()},
            {"ASIA",   New AbsorptionSessionOverride()}
        }
End Class

''' <summary>[P4 #6] The shared default tier: minimum pressing USD before an episode can
''' classify (150k — a pre-calibration anchor; §5 splits it per-session).</summary>
Public Class AbsorptionDefaults
    <JsonPropertyName("min_aggr_usd")> Public Property MinAggrUsd As Double = 150000.0
End Class

''' <summary>[P4 #6] Nullable per-session override — Nothing ⇒ inherit AbsorptionDefaults
''' (the v40 per-session override pattern). Hand-tuned, off the tweaker surface (HC11 class).</summary>
Public Class AbsorptionSessionOverride
    <JsonPropertyName("min_aggr_usd")> Public Property MinAggrUsd As Double? = Nothing
End Class

''' <summary>
''' [funding-momentum] Funding rate momentum signal parameters.
''' Controls Step 3b in ScoringEngine_Calculate and CalcFundingMomentum in Indicators_OrderFlow.
''' MomentumEnabled: master switch -- set false to skip Step 3b entirely. Default True.
''' MomentumWindowMinutes: [v53] age (minutes) of the anchor sample the current rate is
''' compared against -- the newest sample at least this old. Default 5. Replaced the
''' count-indexed MomentumWindow, whose span was 3 x run cadence and therefore scored the
''' same funding path differently at every cadence.
''' MomentumThreshold: min absolute delta (in rate units) to classify as RISING/FALLING. Default 0.00001 (0.001%).
''' MomentumAmplify: additional penalty when momentum confirms crowding. Capped at FundingHighPenalty at call site. Default 1.
''' MomentumSoften: score restored when momentum signals de-crowding. Default 1.
''' </summary>
Public Class FundingSettings
    <JsonPropertyName("momentum_enabled")>   Public Property MomentumEnabled   As Boolean = True
    ' [v53] W = 5 min (proposal §3 D2): >= 1 full bar at both execution resolutions (1m/3m),
    ' >= 2 samples at every cadence the engine has run, and at the front edge of the 2-15 min
    ' hold horizon. Empirically 5-min anchored deltas run SMALLER than the old ~90s count-window
    ' deltas (p50 3.0e-8 vs 8.0e-8) — the funding premium oscillates at short horizons and
    ' partially cancels over 5 min, so the anchor reads sustained drift, not wiggle. Shorter W
    ' re-imports that oscillation noise; longer W lags the hold decision. Coupled to
    ' MomentumThreshold (T is fit GIVEN W) — re-fit T if this moves.
    <JsonPropertyName("momentum_window_minutes")> Public Property MomentumWindowMinutes As Double = 5
    ' [v53] Re-derived at W=5min on a pooled 9-day / 2-regime fit (proposal §4, n=1,994
    ' anchored windows): FLAT 68.7% / Step-3b engagement 17.1%, mid-band on both. Numerically
    ' the same value v50 R2 shipped, but attached to a cadence-independent construction —
    ' a coincidence of scale, not of meaning (the count window spanned ~90s at its 30s
    ' derivation cadence; the anchored window spans 5-6 min at EVERY cadence).
    <JsonPropertyName("momentum_threshold")> Public Property MomentumThreshold As Double  = 0.0000002
    <JsonPropertyName("momentum_amplify")>   Public Property MomentumAmplify   As Integer = 1
    <JsonPropertyName("momentum_soften")>    Public Property MomentumSoften    As Integer = 1
End Class

''' <summary>
''' [OI x CVD cross-confirm] Pass 2b upgrade/conflict gate parameters.
''' Controls the OI x CVD cross-confirm gate in ScoringEngine_Calculate_Scoring.
''' Enabled: master switch -- set false to bypass Pass 2b entirely. Default True.
''' UpgradeBonus: score added when OI and CVD direction confirm each other. Default 1.
''' ConflictPenalty: score deducted when OI (full signal only) and CVD directly oppose. Default 1.
''' Set either to 0 in settings.json to disable that half of the gate without code change.
''' </summary>
Public Class OiCvdSettings
    <JsonPropertyName("enabled")>          Public Property Enabled         As Boolean = True
    <JsonPropertyName("upgrade_bonus")>    Public Property UpgradeBonus    As Integer = 1
    <JsonPropertyName("conflict_penalty")> Public Property ConflictPenalty As Integer = 1
End Class

''' <summary>
''' [bid-ask-spread] Bid-ask spread microstructure thresholds.
''' WideThresholdBps: spread at or above this is WIDE -- triggers entry-side penalty.
''' TightThresholdBps: spread at or below this is TIGHT -- display-only, no score impact.
''' </summary>
Public Class SpreadSettings
    ''' <summary>Spread (bps) at or above this is WIDE -- triggers entry-side penalty. Default 5.0.</summary>
    <JsonPropertyName("wide_threshold_bps")>  Public Property WideThresholdBps  As Double = 5.0
    ''' <summary>Spread (bps) at or below this is TIGHT -- display-only marker, no score impact. Default 1.5.</summary>
    <JsonPropertyName("tight_threshold_bps")> Public Property TightThresholdBps As Double = 1.5
End Class

''' <summary>
''' [swing-pivots] Swing pivot detection parameters for 5m primary and 15m context.
''' PivotWing5m: half-width of the confirmation window on 5m candles. Default 3.
'''   Wing 2 catches noise; wing 4 misses fresh swings during fast moves.
''' LookbackBars5m: how far back to scan for the most recent confirmed swing on 5m. Default 30 (~150 min).
''' PivotWing15m: half-width on 15m candles. Default 2 (slower TF needs less wing).
''' LookbackBars15m: lookback on 15m. Default 20 (~5 hours).
''' </summary>
Public Class SwingSettings
    ''' <summary>Pivot wing on 5m (bars left/right confirming a swing). Default 3.</summary>
    <JsonPropertyName("pivot_wing_5m")>     Public Property PivotWing5m     As Integer = 3
    ''' <summary>Lookback bars on 5m to scan for the most recent swing. Default 30.</summary>
    <JsonPropertyName("lookback_bars_5m")>  Public Property LookbackBars5m  As Integer = 30
    ''' <summary>Pivot wing on 15m. Default 2 (slower timeframe needs less wing).</summary>
    <JsonPropertyName("pivot_wing_15m")>    Public Property PivotWing15m    As Integer = 2
    ''' <summary>Lookback bars on 15m. Default 20 (proportionally smaller window).</summary>
    <JsonPropertyName("lookback_bars_15m")> Public Property LookbackBars15m As Integer = 20
End Class

''' <summary>
''' Session-aware volume threshold scaling by UTC trading bucket.
''' Enabled: master switch -- set false to bypass session scaling entirely. Default True.
''' Sessions: ordered list of UTC hour buckets with independent high/mid multipliers.
''' Intended defaults: ASIA 00:00-07:59, LONDON 08:00-13:29, NY 13:30-23:59.
''' Note: hour-only matching means NY effectively activates from hour 13 onward unless minute support is added later.
''' </summary>
Public Class SessionVolumeSettings
    <JsonPropertyName("enabled")>  Public Property Enabled  As Boolean = True
    ' Default buckets aligned to live v30 (ASIA/LONDON/NY). An empty default would
    ' silently skip all session scaling on the code-defaults path; a settable List
    ' property is fully replaced by settings.json on a successful load.
    <JsonPropertyName("sessions")> Public Property Sessions As New List(Of SessionBucketSettings) From {
        New SessionBucketSettings With {.Name = "ASIA",   .StartHour = 0,  .EndHour = 7,  .HighMultiplier = 0.8,  .MidMultiplier = 0.85},
        New SessionBucketSettings With {.Name = "LONDON", .StartHour = 8,  .EndHour = 12, .HighMultiplier = 1.0,  .MidMultiplier = 1.0},
        New SessionBucketSettings With {.Name = "NY",     .StartHour = 13, .EndHour = 23, .HighMultiplier = 1.15, .MidMultiplier = 1.1}
    }
End Class

Public Class SessionBucketSettings
    <JsonPropertyName("name")>            Public Property Name           As String = ""
    <JsonPropertyName("start_hour")>      Public Property StartHour      As Integer = 0
    <JsonPropertyName("end_hour")>        Public Property EndHour        As Integer = 23
    <JsonPropertyName("high_multiplier")> Public Property HighMultiplier As Double = 1.0
    <JsonPropertyName("mid_multiplier")>  Public Property MidMultiplier  As Double = 1.0
    ''' <summary>
    ''' [v36 session-timeframe-resolution] Execution-indicator-stack resolution in
    ''' minutes (1/3/5) for this session. Default 1 = current 1-min behaviour, zero
    ''' change. The engine fetches + computes the execution stack (incl. ATR) at this
    ''' resolution; the 5m regime + 15m MTF gate + 5m/15m swing pivots stay fixed.
    ''' OFF the auto-tweaker surface (strategy/regime lever — PromptBuilder HARD
    ''' CONSTRAINT 11). Spec: docs/session-timeframe-resolution-implementer-handoff.md.
    ''' </summary>
    <JsonPropertyName("execution_resolution")> Public Property ExecutionResolution As Integer = 1
    ''' <summary>
    ''' [B re-baseline 2026-06-20] Per-session ROC magnitude threshold (|ROC| "active"
    ''' gate). Nullable ⇒ inherit (resolution_profiles → global base). Asia/London 3-min
    ''' ROC *levels* diverge (Asia ~1.8× hotter), so a single resolution_profiles["3"]
    ''' magnitude cannot serve both — this per-session override does (ASIA 0.17 / LONDON
    ''' 0.11). Slope stays shared in resolution_profiles["3"]. MANUAL re-baseline only —
    ''' OFF the auto-tweaker surface (PromptBuilder HARD CONSTRAINT 11). Default buckets
    ''' leave it Nothing (silent-defaults path inherits base, like execution_resolution).
    ''' Spec: docs/asia-london-roc-rebaseline-proposal.md.
    ''' </summary>
    <JsonPropertyName("roc_magnitude_threshold")> Public Property RocMagnitudeThreshold As Double? = Nothing
End Class

''' <summary>
''' [v36 session-timeframe-resolution] Per-resolution override of the timeframe-sensitive
''' threshold keys. Keyed in EngineSettings.ResolutionProfiles by the resolution string
''' ("1"/"3"/"5"). Override fields are nullable so "absent ⇒ inherit the global 1-min
''' value" is unambiguous (a 0.0 default would be a real override). Only the two ROC keys
''' are candle-magnitude-gated and scale with resolution; CVD/MicroCVD read the fixed
''' 500/50-trade stream and stay 1-min. Spec §1.
''' </summary>
Public Class ResolutionProfile
    ''' <summary>ROC magnitude threshold override (3-min seed 0.21 = 1-min 0.1 ×2.1). Nothing ⇒ inherit global.</summary>
    <JsonPropertyName("roc_magnitude_threshold")>   Public Property RocMagnitudeThreshold   As Double? = Nothing
    ''' <summary>ROC slope-delta threshold override (3-min seed 0.105 = 1-min 0.05 ×2.1). Nothing ⇒ inherit global.</summary>
    <JsonPropertyName("roc_slope_delta_threshold")> Public Property RocSlopeDeltaThreshold  As Double? = Nothing
End Class

' ---------------------------------------------------------------------------
' Kelly sizing settings
' ---------------------------------------------------------------------------

''' <summary>
''' Display-only position sizing parameters for the Kelly Criterion block.
''' All computation happens in MainForm_Render.CalcKellySizing() -- no scoring impact.
''' EST mode only. CAL mode will be reinstated after the backtesting module is built.
''' </summary>
Public Class KellySettings
    ''' <summary>Account size in USD. Default $1,000.</summary>
    <JsonPropertyName("account_size_usd")>         Public Property AccountSizeUsd        As Double  = 1000.0
    ''' <summary>Use half-Kelly (True) or full Kelly (False). Default True.</summary>
    <JsonPropertyName("use_half_kelly")>            Public Property UseHalfKelly          As Boolean = True
    ''' <summary>Hard cap on risk fraction regardless of Kelly output. Default 0.05 = 5%.</summary>
    <JsonPropertyName("max_risk_fraction")>         Public Property MaxRiskFraction       As Double  = 0.05
    ''' <summary>Deribit BTC-PERPETUAL contract face value in USD. Default $10.</summary>
    <JsonPropertyName("contract_face_usd")>         Public Property ContractFaceUsd       As Double  = 10.0
    ''' <summary>Score-to-probability band floor (pre-calibration). Default 0.45.</summary>
    <JsonPropertyName("est_prob_floor")>            Public Property EstProbFloor          As Double  = 0.45
    ''' <summary>Score-to-probability band scale range (pre-calibration). Default 0.20 -> band [0.45, 0.65].</summary>
    <JsonPropertyName("est_prob_scale")>            Public Property EstProbScale          As Double  = 0.20
    ''' <summary>[D1/H4] Max leverage on notional (contracts × face / account). Binds before the $ risk cap at correct inverse-contract sizing. Default 5.0 — conservative; the trader tunes (Deribit BTC perp allows far more).</summary>
    <JsonPropertyName("max_leverage")>              Public Property MaxLeverage           As Double  = 5.0
End Class

' ---------------------------------------------------------------------------
' MTF Gate settings
' ---------------------------------------------------------------------------

''' <summary>
''' Controls the 15m multi-timeframe confluence gate.
''' 2-of-3 majority vote: DMI direction / ADX strength / EMA alignment on 15m series.
''' Set Enabled=false in settings.json to bypass entirely (hot-reload safe).
''' </summary>
Public Class MTFGateSettings
    <JsonPropertyName("enabled")>         Public Property Enabled          As Boolean = True
    <JsonPropertyName("candle_lookback")> Public Property CandleCount      As Integer = 60
    <JsonPropertyName("adx_period")>      Public Property DmiPeriod        As Integer = 9
    <JsonPropertyName("adx_min")>         Public Property AdxMin           As Double  = 20.0
    <JsonPropertyName("min_of")>          Public Property RequiredConfirms As Integer = 2
End Class

' ---------------------------------------------------------------------------
' Auto-run settings
' ---------------------------------------------------------------------------

''' <summary>Controls the auto-run timer. Minimum effective interval 10s (enforced in MainForm).</summary>
Public Class AutoRunSettings
    ' D4 (S-4): the former 'enabled' key was dead — InitAutoRunControls always
    ' starts stopped and nothing read it. Removed in v32.
    <JsonPropertyName("interval_minutes")> Public Property IntervalMinutes As Integer = 1
    <JsonPropertyName("interval_seconds")> Public Property IntervalSeconds As Integer = 0
    ' P4 #2 (on-close analysis mode, v44): run-TRIGGER selector — "interval" (default,
    ' today's fixed-timer behaviour) | "on_close" (fire on each execution-resolution bar
    ' close). Pure trigger change — RunAnalysisAsync / Calculate() are byte-identical; only
    ' WHEN a run fires changes. Off the auto-tweaker surface (operational preference). The
    ' interval_* keys double as the on-close interval BACKSTOP ceiling. Default "interval"
    ' preserves v43 behaviour exactly. (docs/on-close-analysis-mode-proposal.md)
    <JsonPropertyName("trigger_mode")> Public Property TriggerMode As String = "on_close" ' [v57] synced to the v44 operating mode (stomp-proofing); interval stays a valid operational choice
End Class

' ---------------------------------------------------------------------------
' Scoring settings
' ---------------------------------------------------------------------------

Public Class ScoringSettings
    <JsonPropertyName("verdict_strong_pct")> Public Property VerdictStrongPct As Double = 0.70
    <JsonPropertyName("verdict_med_pct")>    Public Property VerdictMedPct    As Double = 0.53
    <JsonPropertyName("verdict_weak_pct")>   Public Property VerdictWeakPct   As Double = 0.35
    ' Funding bands aligned to live v30 (v22 recalibration: high ±0.00008, low ±0.00001).
    <JsonPropertyName("funding_high_positive")> Public Property FundingHighPositive As Double = 0.00008
    <JsonPropertyName("funding_low_positive")>  Public Property FundingLowPositive  As Double = 0.00001
    <JsonPropertyName("funding_high_negative")> Public Property FundingHighNegative As Double = -0.00008
    <JsonPropertyName("funding_low_negative")>  Public Property FundingLowNegative  As Double = -0.00001
    ''' <summary>Score penalty while BBW TTM Squeeze is ACTIVE (both sides). Default 2.</summary>
    <JsonPropertyName("bbw_squeeze_penalty")>  Public Property BbwSqueezePenalty  As Integer = 2
    ''' <summary>Penalty for standard-size adverse liquidations. Default 1.</summary>
    <JsonPropertyName("liq_standard_penalty")> Public Property LiqStandardPenalty As Integer = 1
    ''' <summary>Penalty for large adverse liquidations (>= LargeLiqSize). Default 2.</summary>
    <JsonPropertyName("liq_large_penalty")>    Public Property LiqLargePenalty    As Integer = 2
    ''' <summary>Penalty applied to adverse side at extreme funding. Default 2.</summary>
    <JsonPropertyName("funding_high_penalty")> Public Property FundingHighPenalty As Integer = 2
    ''' <summary>Boost applied to favoured side at extreme funding. Default 1.</summary>
    <JsonPropertyName("funding_high_boost")>   Public Property FundingHighBoost   As Integer = 1
    ''' <summary>Penalty applied to adverse side at mild funding. Default 1.</summary>
    <JsonPropertyName("funding_low_penalty")>  Public Property FundingLowPenalty  As Integer = 1
    ''' <summary>ATR multiplier for the FALLBACK target (placed-geometry B4b: structure is
    ''' the primary target source; this places only when no structural tier survives the
    ''' looseness bound — or everywhere when structural_levels.enabled=false, the legacy
    ''' geometry). Re-derived v51: 2.0 → 1.75 (NY reach 56.8%, the DG3 55–60% design point;
    ''' LONDON/ASIA session overrides live in structural_levels.sessions).</summary>
    <JsonPropertyName("atr_target_multiplier")> Public Property AtrTargetMultiplier As Double = 1.75
    ''' <summary>ATR multiplier for the FALLBACK stop (placed-geometry B4b; also the DG1
    ''' clamp level via structural_levels.stop_max_atr_mult = same value). Re-derived v51:
    ''' 1.2 → 1.6 (survives ~80–85% of winners pooled; covers LONDON's winners-MAE p75 1.63 —
    ''' the old 1.2 stopped ~30% of eventual London winners before their target, DG2).</summary>
    <JsonPropertyName("atr_stop_multiplier")>   Public Property AtrStopMultiplier   As Double = 1.6
    ''' <summary>
    ''' [v35 min-tradeable-move gate + eval de-confound] Minimum take-profit distance as a
    ''' fraction of entry price. Shared editable floor consumed by BOTH:
    '''   (a) the scoring gate — a directional verdict whose realistic (post-cap) target sits
    '''       closer than this is overridden to NO TRADE (VerdictContext = BELOW_MIN_MOVE);
    '''   (b) the eval-metric de-confound — the favourable barrier is floored at this distance,
    '''       and historical directional trades whose ATR target can't clear it are EXCLUDED
    '''       from the success/fail counts (not scored as failures).
    ''' Price-relative so it tracks BTC with no recalibration (≈ $50 at $62k, sized to clear
    ''' slippage). Trader-owned risk preference — NEVER auto-tuned (off the auto-tweaker
    ''' surface, same exclusion class as the kelly.* keys). Hot-reloadable. Default 0.0008 (0.08%).
    ''' Specs: docs/min-tradeable-move-gate-proposal.md, docs/eval-metric-deconfound-proposal.md.
    ''' </summary>
    <JsonPropertyName("min_tradeable_move_pct")> Public Property MinTradeableMovePct As Double = 0.0008
    ''' <summary>[T1-D] ROC level above which TAKE PROFIT fires for a long. Default 0.6.</summary>
    <JsonPropertyName("hold_roc_take_profit_long")>  Public Property HoldRocTakeProfitLong  As Double = 0.6
    ''' <summary>[T1-D] ROC level below which TAKE PROFIT fires for a short. Default -0.6.</summary>
    <JsonPropertyName("hold_roc_take_profit_short")> Public Property HoldRocTakeProfitShort As Double = -0.6
    ''' <summary>[T1-D] RSI above this = HOLD (long). Default 60.</summary>
    <JsonPropertyName("hold_rsi_hold_long")>         Public Property HoldRsiHoldLong        As Double = 60.0
    ''' <summary>[T1-D] RSI at or above this = EVALUATE, below = EXIT (long). Default 40.</summary>
    <JsonPropertyName("hold_rsi_evaluate_long")>     Public Property HoldRsiEvaluateLong    As Double = 40.0
    ''' <summary>[T1-D] RSI below this = HOLD (short). Default 40.</summary>
    <JsonPropertyName("hold_rsi_hold_short")>        Public Property HoldRsiHoldShort       As Double = 40.0
    ''' <summary>[T1-D] RSI at or below this = EVALUATE, above = EXIT (short). Default 60.</summary>
    <JsonPropertyName("hold_rsi_evaluate_short")>    Public Property HoldRsiEvaluateShort   As Double = 60.0
    ''' <summary>
    ''' [Step 5b] Min structural signal hits (VWAP/BBW/EMA/DMI/ADX/Donchian/5mEMA200)
    ''' to classify verdict as FLOW_UNCONFIRMED. Default 3.
    ''' Review after 50+ trades: if FLOW_UNCONFIRMED fires >40% of WEAK verdicts, raise to 4.
    ''' </summary>
    <JsonPropertyName("context_tag_structural_min")> Public Property ContextTagStructuralMin As Integer = 3
    ''' <summary>
    ''' [Step 5b] Max flow signal hits (OFI/CVD/TFI/MicroCVD/OI Delta/ROC/Volume)
    ''' to classify verdict as FLOW_UNCONFIRMED. Default 1.
    ''' </summary>
    <JsonPropertyName("context_tag_flow_max")>       Public Property ContextTagFlowMax       As Integer = 1
    ''' <summary>[bid-ask-spread] Penalty applied to entry side when SpreadStatus = WIDE. Default 1.</summary>
    <JsonPropertyName("spread_wide_penalty")> Public Property SpreadWidePenalty As Integer = 1
    ''' <summary>[settings-exposure] Per-regime score ceiling base values (before regime_weights bonus).</summary>
    <JsonPropertyName("regime_max_score")>    Public Property RegimeMaxScore    As New RegimeMaxScoreSettings
    ''' <summary>[settings-exposure] TRANSITIONAL ADX penalty graceful-degradation floor breakpoints.</summary>
    <JsonPropertyName("tier_floor")>          Public Property TierFloor         As New TierFloorSettings
    ''' <summary>[settings-exposure] VerdictContext Step 5b MOMENTUM_FADING / STRUCTURALLY_WEAK thresholds.</summary>
    <JsonPropertyName("context_tag_thresholds")> Public Property ContextTag     As New ContextTagThresholds
    ''' <summary>[placed-geometry B4b] Structural-first placed levels — see StructuralLevelsSettings.</summary>
    <JsonPropertyName("structural_levels")>      Public Property StructuralLevels As New StructuralLevelsSettings
End Class

' ---------------------------------------------------------------------------
' Placed geometry — structural-first levels (B4b)
' ---------------------------------------------------------------------------

''' <summary>
''' [placed-geometry B4b] Structural-first placed-level arbitration
''' (docs/placed-geometry-structural-first-proposal.md §3/§7, values from
''' docs/placed-geometry-derivation-2026-07-06.md §4 — DG1–DG5 ticked 2026-07-06).
''' Consumed ONLY by SignalEmitter.ComputeSideLevels (the one shared arbitration seam).
''' Three-tier tweaker surface (HARD CONSTRAINT 21):
'''   ON  (flat numerics)  — target_max_atr_mult, stop_max_atr_mult, stop_min_floor_ticks.
'''   OFF (hand-toggle)    — enabled (rollback switch: false ⇒ byte-identical v50 geometry)
'''                          + stop_too_loose_mode (the D3 decision record; exact-match rejects).
'''   OFF (hand-tuned)     — sessions.* (the DG3 per-session fallback-target tier, HC11 class;
'''                          SettingsDiffApplier rejects the sessions. prefix).
''' </summary>
Public Class StructuralLevelsSettings
    ''' <summary>Master switch. False ⇒ legacy v50 geometry byte-identical (pure-ATR stop,
    ''' closest-wins target cap) — the rollback. Default True.</summary>
    <JsonPropertyName("enabled")>              Public Property Enabled           As Boolean = True
    ''' <summary>Target looseness bound (× ATR): a structural target tier places only when
    ''' 0 &lt; dist ≤ this × ATR; none survives ⇒ ATR fallback. DG4: 3.5 confirmed
    ''' (in-bound structural reach 66.7% pooled ≥ fallback).</summary>
    <JsonPropertyName("target_max_atr_mult")>  Public Property TargetMaxAtrMult  As Double = 3.5
    ''' <summary>Stop bound / D3 clamp level (× ATR). DG1 amends the D2 stop shape for v1:
    ''' placed stop = min(structural swing stop, this × ATR) — structure places only when
    ''' TIGHTER (5m swing stops run p50 4–9× ATR; at fixed sizing a true structural stop is
    ''' inoperative until consumer sizing-by-stop-distance exists, derivation §6b).
    ''' Deliberately = the fallback stop multiplier (1.6) per DG2.</summary>
    <JsonPropertyName("stop_max_atr_mult")>    Public Property StopMaxAtrMult    As Double = 1.6
    ''' <summary>Degenerate-tightness floor in ticks (BTC-PERPETUAL tick = $0.5): a structural
    ''' stop closer than this falls back to ATR. Near-moot (struct stops are almost never
    ''' tight) — DG confirmed 4.</summary>
    <JsonPropertyName("stop_min_floor_ticks")> Public Property StopMinFloorTicks As Integer = 4
    ''' <summary>D3 decision record: "clamp" (shipped — too-loose structural stop clamps to
    ''' stop_max_atr_mult × ATR, labeled STOP_CLAMPED) | "skip" (the D3-b alternative, a
    ''' new no-trade gate — NOT built; unrecognised values behave as clamp). Hand-toggle,
    ''' off the tweaker surface.</summary>
    <JsonPropertyName("stop_too_loose_mode")>  Public Property StopTooLooseMode  As String = "clamp"
    ''' <summary>[geometry-arbitration-modes v56] Target arbitration mode.
    ''' 0 = ladder (current: swing→HVN→POC→session-ATR fallback, priority-with-bound — the
    '''     legacy DG shape; structure wins even when FARTHER than the ATR level, up to bound).
    ''' 1 = NEAREST: among the qualifying structural candidates (same qualification: 0 &lt; dist
    '''     ≤ target_max_atr_mult×ATR, POC HVN-gated as today) AND the session-resolved ATR
    '''     fallback target, place whichever is CLOSEST to entry. Labels stay truthful (the
    '''     winning tier's label; fallback wins ⇒ FALLBACK_ATR / no cap reason, matching today's
    '''     fallback semantics). What-if instrument at build (defaults byte-identical to v51 B4b);
    '''     live activation is a LATER ⚠ D-table gated on replay evidence.
    ''' Fenced off the tweaker surface (HARD CONSTRAINT 24 — hand-ruled geometry, HC11 class;
    ''' exact-match).</summary>
    <JsonPropertyName("target_arbitration_mode")> Public Property TargetArbitrationMode As Integer = 0
    ''' <summary>[geometry-arbitration-modes v56] Stop arbitration mode.
    ''' 0 = tightest (current DG1: min(structural swing stop, stop_max_atr_mult×ATR) ≥ floor,
    '''     STOP_CLAMPED semantics verbatim).
    ''' 1 = WIDEST: max(structural swing stop distance, stop_max×ATR), still ≥ the 4-tick floor,
    '''     UNCLAMPED above — the trader's SL half. Label truthfully (SWING_STOP when structure
    '''     wins on wider distance, FALLBACK_ATR when the ATR distance wins). What-if instrument
    '''     at build (defaults byte-identical to v51 B4b); live activation is a LATER ⚠ D-table,
    '''     ALSO hard-gated on sizing-by-stop-distance (L3) regardless of replay evidence
    '''     (derivation F1: wide stops at fixed size = bigger losses).
    ''' Fenced off the tweaker surface (HARD CONSTRAINT 24; exact-match).</summary>
    <JsonPropertyName("stop_arbitration_mode")>   Public Property StopArbitrationMode   As Integer = 0
    ''' <summary>[geometry-arbitration-modes v56] Signed % of the placed target's distance from
    ''' entry, applied AFTER arbitration. Negative shaves the target toward entry (the trader's
    ''' pullback); positive pushes beyond. Formula: placed' = entry + (placed − entry) ×
    ''' (1 + pct/100). The Step-5c min-move gate evaluates the BUFFERED target (a deep negative
    ''' buffer can honestly gate a verdict to BELOW_MIN_MOVE in replay — where the gate
    ''' re-derives). Default 0.0 ⇒ byte-identical to v51 B4b. Fenced off the tweaker surface
    ''' (HARD CONSTRAINT 24; exact-match).</summary>
    <JsonPropertyName("target_buffer_pct")>       Public Property TargetBufferPct       As Double  = 0.0
    ''' <summary>[geometry-arbitration-modes v56] Signed % of the placed stop's distance from
    ''' entry, applied AFTER arbitration. Positive pushes the stop farther from entry (the
    ''' trader's buffer); negative tightens. Formula: placed' = entry + (placed − entry) ×
    ''' (1 + pct/100). The 4-tick stop floor applies to the BUFFERED stop (a −99% buffer will
    ''' snap to the floor rather than cross entry). Default 0.0 ⇒ byte-identical to v51 B4b.
    ''' Fenced off the tweaker surface (HARD CONSTRAINT 24; exact-match).</summary>
    <JsonPropertyName("stop_buffer_pct")>         Public Property StopBufferPct         As Double  = 0.0
    ''' <summary>Per-session nullable overrides keyed by session bucket name (v40 pattern;
    ''' null field ⇒ inherit the global scoring.atr_target_multiplier). DG3 hand-tuned tier:
    ''' LONDON 2.0 (already at the design reach), ASIA 1.25 (never reaches far targets).
    ''' Seeded so the code-defaults path matches settings.json.</summary>
    <JsonPropertyName("sessions")>             Public Property Sessions          As Dictionary(Of String, StructuralLevelsSessionOverride) =
        New Dictionary(Of String, StructuralLevelsSessionOverride) From {
            {"NY",     New StructuralLevelsSessionOverride()},
            {"LONDON", New StructuralLevelsSessionOverride With {.FallbackTargetAtrMult = 2.0}},
            {"ASIA",   New StructuralLevelsSessionOverride With {.FallbackTargetAtrMult = 1.25}}
        }
End Class

''' <summary>[placed-geometry B4b] Nullable per-session override — Nothing ⇒ inherit the
''' global fallback multipliers (the v40/aggressor-velocity override pattern). Hand-tuned,
''' off the tweaker surface (HC11 class).</summary>
Public Class StructuralLevelsSessionOverride
    <JsonPropertyName("fallback_target_atr_mult")> Public Property FallbackTargetAtrMult As Double? = Nothing
End Class

' ---------------------------------------------------------------------------
' Settings-exposure: scoring mechanics
' ---------------------------------------------------------------------------

''' <summary>Per-regime score ceiling base values (before regime_weights bonus).</summary>
Public Class RegimeMaxScoreSettings
    ''' <summary>Base max score for TRENDING_UP / TRENDING_DOWN. Default 19.</summary>
    <JsonPropertyName("trending")>     Public Property Trending     As Integer = 19
    ''' <summary>Base max score for RANGE_BOUND. Default 18.</summary>
    <JsonPropertyName("range_bound")>  Public Property RangeBound   As Integer = 18
    ''' <summary>Base max score for TRANSITIONAL. Default 15.</summary>
    <JsonPropertyName("transitional")> Public Property Transitional As Integer = 15
End Class

''' <summary>
''' Graceful-degradation floor for TRANSITIONAL ADX penalty.
''' If raw score >= HighThreshold, post-penalty floor is HighFloor.
''' Same pattern at Med and Low. Below LowThreshold no floor applies.
''' </summary>
Public Class TierFloorSettings
    <JsonPropertyName("high_threshold")> Public Property HighThreshold As Integer = 12
    <JsonPropertyName("high_floor")>     Public Property HighFloor     As Integer = 9
    <JsonPropertyName("med_threshold")>  Public Property MedThreshold  As Integer = 9
    <JsonPropertyName("med_floor")>      Public Property MedFloor      As Integer = 6
    <JsonPropertyName("low_threshold")>  Public Property LowThreshold  As Integer = 6
    <JsonPropertyName("low_floor")>      Public Property LowFloor      As Integer = 3
End Class

''' <summary>
''' VerdictContext classifier thresholds (Step 5b).
''' Distinct from ContextTagStructuralMin / ContextTagFlowMax (those gate FLOW_UNCONFIRMED).
''' These gate MOMENTUM_FADING and STRUCTURALLY_WEAK.
''' </summary>
Public Class ContextTagThresholds
    ''' <summary>Late vs early MicroCVD ratio threshold for fading detection. Default 0.5.</summary>
    <JsonPropertyName("momentum_fading_decay_ratio")>  Public Property MomentumFadingDecayRatio  As Double  = 0.5
    ''' <summary>Min count of fading signals to classify MOMENTUM_FADING. Default 2.</summary>
    <JsonPropertyName("momentum_fading_count_min")>    Public Property MomentumFadingCountMin    As Integer = 2
    ''' <summary>Structural hits below this + flow below StructurallyWeakFlowMin → STRUCTURALLY_WEAK. Default 2.</summary>
    <JsonPropertyName("structurally_weak_struct_min")> Public Property StructurallyWeakStructMin As Integer = 2
    ''' <summary>Flow hits below this + structural below StructurallyWeakStructMin → STRUCTURALLY_WEAK. Default 2.</summary>
    <JsonPropertyName("structurally_weak_flow_min")>   Public Property StructurallyWeakFlowMin   As Integer = 2
End Class

' ---------------------------------------------------------------------------
' Regime gate settings
' ---------------------------------------------------------------------------

''' <summary>
''' [Pass 2c] Regime alignment gate — bonus/penalty when all active regime-key signals agree or conflict.
''' Enabled: master switch. Default True.
''' Trending signals: EMA ribbon (1m), ROC(9) threshold-gated, CVD slope+value.
''' RangeBound signals: VWAP deviation (suppressed in warmup), RSI(9) vs 50, Donchian(20).
''' TRANSITIONAL: gate is suppressed. Gate also suppressed when LongScore = ShortScore.
''' RegimeMaxScore() adds AlignmentBonus headroom when Enabled so thresholds auto-adjust.
''' </summary>
Public Class RegimeWeightSettings
    <JsonPropertyName("enabled")>      Public Property Enabled    As Boolean = True
    <JsonPropertyName("trending")>     Public Property Trending   As New RegimeAlignSettings
    <JsonPropertyName("range_bound")>  Public Property RangeBound As New RegimeAlignSettings
End Class

Public Class RegimeAlignSettings
    <JsonPropertyName("alignment_bonus")>   Public Property AlignmentBonus   As Integer = 1
    <JsonPropertyName("conflict_penalty")>  Public Property ConflictPenalty  As Integer = 1
End Class

' ---------------------------------------------------------------------------
' Network / API resilience settings
' ---------------------------------------------------------------------------

''' <summary>
''' API resilience parameters for the REST fetch layer.
''' RequestTimeoutSeconds: HttpClient.Timeout for each fetch.
''' RetryCount: additional retries on transient failure (5xx, timeout, network drop).
'''   0 = no retry; 1 = retry once (default); higher values stack but should rarely be needed.
''' RetryBackoffMs: delay between retries in milliseconds.
''' </summary>
Public Class NetworkSettings
    <JsonPropertyName("request_timeout_seconds")> Public Property RequestTimeoutSeconds As Integer = 15
    <JsonPropertyName("retry_count")>              Public Property RetryCount            As Integer = 1
    <JsonPropertyName("retry_backoff_ms")>         Public Property RetryBackoffMs        As Integer = 1000

    ' ── WebSocket transport (v38, migration P1) ──────────────────────────────────────────
    ' Additive-only foundation; the live path stays pure REST until P2 wires the source in
    ' and P3 flips the cutover. See docs/websocket-migration-p1-implementer-handoff.md.
    ' Transport: "rest" | "ws" — cutover flag; P3 flips the default. Stays "rest" in P1/P2.
    <JsonPropertyName("transport")>           Public Property Transport        As String  = "rest"
    ' WsUrl: Deribit public JSON-RPC v2 WebSocket endpoint (public channels only, no auth).
    <JsonPropertyName("ws_url")>              Public Property WsUrl            As String  = "wss://www.deribit.com/ws/api/v2"
    ' WsHeartbeatSec: public/set_heartbeat interval (Deribit minimum 10s).
    <JsonPropertyName("ws_heartbeat_sec")>    Public Property WsHeartbeatSec   As Integer = 30
    ' WsStaleAfterSec: book/trades/ticker getters return Nothing past this age → consumer
    ' fallback handles it like a REST failure (candle series defer to IndicatorEngine.IsFresh).
    <JsonPropertyName("ws_stale_after_sec")>  Public Property WsStaleAfterSec  As Integer = 10
    ' WsCooldownSec: reconnect-storm hold (>5 reconnects / 10 min).
    <JsonPropertyName("ws_cooldown_sec")>     Public Property WsCooldownSec    As Integer = 300
    ' WsFallbackToRest: P2 routing — fall back to REST when the WS source is stale/down.
    <JsonPropertyName("ws_fallback_to_rest")> Public Property WsFallbackToRest As Boolean = True
    ' ShadowParity: when True (with transport="rest"), run the WS feed alongside and log a
    ' per-run WS-vs-REST field comparison to the side log (never the CSV, never scoring). The
    ' ≥50-consecutive-pass result is the proposal §7 cutover gate. Dev/validation mode; default
    ' off = zero WS overhead. Off the auto-tweaker surface (rides the network.* exclusion). (v39, P2)
    <JsonPropertyName("shadow_parity")>       Public Property ShadowParity     As Boolean = False
End Class

' ---------------------------------------------------------------------------
' D1 — Trend Structure classification settings
' ---------------------------------------------------------------------------

''' <summary>
''' [d1-trend-structure] HH/HL/LH/LL swing-structure classification + Pass 2c structure bonus.
''' Enabled: master switch. Default True.
''' PivotWing: confirmation bars left/right for ClassifyTrendStructure scan. Default 3 (matches swing.pivot_wing_5m).
''' PivotCount: total pivot events to collect before classifying (mix of highs and lows). Default 6.
'''   With typical alternation → ~3 highs + 3 lows. Enough to classify; not so many that it lags real regime changes.
''' StructureBonus: score added when structure agrees with dominant side (capped at regimeMax). Default 1.
''' </summary>
Public Class TrendStructureSettings
    <JsonPropertyName("enabled")>         Public Property Enabled        As Boolean = True
    <JsonPropertyName("pivot_wing")>      Public Property PivotWing      As Integer = 3
    <JsonPropertyName("pivot_count")>     Public Property PivotCount     As Integer = 6
    <JsonPropertyName("structure_bonus")> Public Property StructureBonus As Integer = 1
End Class

' ---------------------------------------------------------------------------
' Analysis logging settings
' ---------------------------------------------------------------------------

''' <summary>
''' Controls the output dump feature (analysis_output_dump.md).
''' OutputDumpEnabled: master switch — false = no I/O on analysis runs. Default True.
''' OutputDumpMaxRuns: maximum run blocks to retain (rolling-trim). 0 = unlimited. Default 3000.
''' </summary>
Public Class AnalysisLoggingSettings
    <JsonPropertyName("output_dump_enabled")>
    Public Property OutputDumpEnabled As Boolean = True
    <JsonPropertyName("output_dump_max_runs")>
    Public Property OutputDumpMaxRuns As Integer = 3000
End Class

' ---------------------------------------------------------------------------
' Regime gate settings
' ---------------------------------------------------------------------------

Public Class RegimeGateSettings
    ' transitional_adx_penalty_low removed in v47 (dead since the v31 F8 fix — the first
    ' TRANSITIONAL penalty arm covers [0, penalty_mid); only Mid/High are read).
    <JsonPropertyName("transitional_adx_penalty_mid")>  Public Property TransitionalAdxPenaltyMid   As Double  = 22.5
    <JsonPropertyName("transitional_adx_penalty_high")> Public Property TransitionalAdxPenaltyHigh  As Double  = 25.0
    <JsonPropertyName("transitional_penalty_low")>      Public Property TransitionalPenaltyLow      As Integer = 2
    <JsonPropertyName("transitional_penalty_mid")>      Public Property TransitionalPenaltyMid      As Integer = 1
End Class

' ---------------------------------------------------------------------------
' Live performance display settings (P7)
' ---------------------------------------------------------------------------

''' <summary>
''' Controls the six-window live success/fail rate strip shown after each analysis run.
''' Enabled: master switch — false = strip not rendered, no cache I/O. Default True.
''' MinSampleForRender: minimum evaluable rows before showing a numeric rate. Default 10
'''   (raised 4 -> 10 at v55 with the E2a WEAK exclusion; the denominator drops ~2.6x
'''   under STRONG+MEDIUM only, and a 3pp-step rate needs a real floor).
''' EagerBackfillOnStartup: fetch 7-day OHLC gap + backfill eval cache on engine start. Default True.
''' SessionBlockSemantic: "most_recent" only for now; reserved for future "calendar_day" variant.
''' GapBackfillEnabled: detect and fill interior OHLC gaps within the 7-day window on startup.
''' MaxGapFillCalls: safety cap on the number of Deribit calls per startup gap-fill pass.
''' MaxGapFillMinutes: chunk size per Deribit call (Deribit limit is 5000 bars per response).
''' </summary>
Public Class PerformanceDisplaySettings
    <JsonPropertyName("enabled")>
    Public Property Enabled As Boolean = True

    <JsonPropertyName("min_sample_for_render")>
    Public Property MinSampleForRender As Integer = 10

    <JsonPropertyName("eager_backfill_on_startup")>
    Public Property EagerBackfillOnStartup As Boolean = True

    <JsonPropertyName("session_block_semantic")>
    Public Property SessionBlockSemantic As String = "most_recent"

    <JsonPropertyName("gap_backfill_enabled")>
    Public Property GapBackfillEnabled As Boolean = True

    <JsonPropertyName("max_gap_fill_calls")>
    Public Property MaxGapFillCalls As Integer = 10

    <JsonPropertyName("max_gap_fill_minutes")>
    Public Property MaxGapFillMinutes As Integer = 5000

    ''' <summary>
    ''' [target-hit-toggle] Metric mode for the live performance strip.
    ''' "barrier" = SuccessCount/(Success+Failure) — barrier-hit before adverse stop.
    ''' "target"  = TargetHitCount/(Success+Failure) — favourable barrier touched at
    '''             any point within the window (ignores stop hit). Same denominator.
    ''' Default "barrier". Toggled at runtime via left-click (ephemeral) or right-click
    ''' (persisted via SettingsLoader.Save).
    ''' </summary>
    <JsonPropertyName("metric_mode")>
    Public Property MetricMode As String = "barrier"
End Class

' ---------------------------------------------------------------------------
' Realtime Exit Guard settings (P4 #1)
' ---------------------------------------------------------------------------

''' <summary>
''' [P4 #1 realtime-exit-guard] Display/alert-only overlay that re-runs the fast microstructure
''' exit checks against the live WS MarketState every interval_sec while a position is declared.
''' Zero scoring impact — never calls Calculate, never writes the CSV. OFF the auto-tweaker
''' surface (trader-risk/display preference, same exclusion class as kelly.* — SettingsDiffApplier
''' rejects "exit_guard." and PromptBuilder HARD CONSTRAINT 13). Hot-reloadable.
''' Spec: docs/realtime-exit-guard-proposal.md §5 / §9.
''' </summary>
Public Class ExitGuardSettings
    ''' <summary>Master switch. Active only when a position is declared; dormant when flat. Default True.</summary>
    <JsonPropertyName("enabled")>        Public Property Enabled       As Boolean = True
    ''' <summary>Guard re-evaluation cadence in seconds (range 2–5). In-memory recompute is cheap. Default 3.</summary>
    <JsonPropertyName("interval_sec")>   Public Property IntervalSec   As Integer = 3
    ''' <summary>Consecutive EXIT ticks required before the strip latches EXIT + the alarm fires (anti-jitter). Default 2.</summary>
    <JsonPropertyName("debounce_evals")> Public Property DebounceEvals As Integer = 2
    ''' <summary>Play an audible cue on the EXIT-latch transition. Default True.</summary>
    <JsonPropertyName("sound_enabled")>  Public Property SoundEnabled  As Boolean = True
End Class

''' <summary>
''' [P4 #3 live-microstructure-strip] Display-only one-line TAPE strip that recomputes the fast
''' streaming microstructure (price vs nearest structural levels, TFI, spread, book imbalance, tape
''' speed) from the live WS MarketState every refresh_sec between full runs. Deliberately NOT a verdict
''' (no score / no direction) — zero scoring impact, never calls Calculate, never writes the CSV, no
''' re-baseline. OFF the auto-tweaker surface (display preference, no failure-rate linkage — same
''' exclusion class as kelly.* / exit_guard.* / auto_run.*: SettingsDiffApplier rejects "live_strip."
''' and PromptBuilder HARD CONSTRAINT 15). Hot-reloadable. Reuses cfg.Indicators.OFI.BookDepth for the
''' imbalance depth (no duplicate key). Spec: docs/live-microstructure-strip-proposal.md §5.
''' </summary>
Public Class LiveStripSettings
    ''' <summary>Master switch. Default True (trader confirmed the look at the §9 #1 checkpoint 2026-06-29). Toggled live via the TAPE checkbox.</summary>
    <JsonPropertyName("enabled")>         Public Property Enabled       As Boolean = True
    ''' <summary>Strip recompute + repaint cadence in seconds. In-memory recompute is cheap. Default 2.</summary>
    <JsonPropertyName("refresh_sec")>     Public Property RefreshSec     As Integer = 2
    ''' <summary>Tape-speed measurement window in seconds (trades/sec + USD/sec over this lookback). Default 10.</summary>
    <JsonPropertyName("tape_window_sec")> Public Property TapeWindowSec As Integer = 10
End Class

''' <summary>
''' [Signal Bridge v1] Per-run atomic-write emission of verdict_signal.json — the machine-readable
''' mirror of the rendered verdict, consumed by DeribitOrderPlacementApp (docs/signal-bridge-v1-proposal.md,
''' schema v1 FROZEN 2026-07-03). The engine NEVER places orders; the human display stays primary — the
''' payload is the third parity surface (snapshot ↔ cards ↔ signal file). Zero scoring/CSV impact.
''' OFF the auto-tweaker surface (transport plumbing, network.* class: SettingsDiffApplier rejects
''' "signal_bridge." + PromptBuilder HARD CONSTRAINT 18). NOTE: the ARM AUTOTRADE toggle is deliberately
''' NOT here — it is runtime-only state (default OFF every start, never persisted; interlock rule §8 D7).
''' </summary>
Public Class SignalBridgeSettings
    ''' <summary>Master switch. Default False (§8 D3) — flipping it on is the trader's dated action after the consumer's log-only validation.</summary>
    <JsonPropertyName("enabled")>     Public Property Enabled    As Boolean = False
    ''' <summary>Atomic-write target. Default = the agreed neutral folder outside both repos (§8 D9); the emitter creates the directory if missing. Empty ⇒ beside the exe (fallback semantic).</summary>
    <JsonPropertyName("output_path")> Public Property OutputPath As String = "C:\Dev\DeribitBridge\verdict_signal.json"
End Class
