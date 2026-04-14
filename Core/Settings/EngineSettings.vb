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
'        Defaults match the method's previous hardcoded values (PivotWing=2, LookbackBars=20).
' fix [T3-C]: Added TtmSettings class and IndicatorSettings.TTM property.
'        CalcTTMSqueeze was wired to cfg.Indicators.TTM.FlatThreshold in MainForm_Analysis.vb
'        but the class and property were missing -- caused BC30456.
'        Default FlatThreshold=0.5 matches the method's previous hardcoded default.
' fix [T3-A]: Added VpfrSettings class and IndicatorSettings.VPFR property.
'        CalcVPFRLite was wired to cfg.Indicators.VPFR.NumBuckets in MainForm_Analysis.vb
'        but the class and property were missing -- caused BC30456.
'        Default NumBuckets=50 matches the method's previous hardcoded default.
' Step 5b: Added ContextTagStructuralMin and ContextTagFlowMax to ScoringSettings.
'        Used by CalcVerdictContext() in ScoringEngine_Calculate to classify weak/ambiguous
'        verdicts as FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED.
'        Defaults: ContextTagStructuralMin=3, ContextTagFlowMax=1.
' Kelly: Added KellySettings class and Kelly property on EngineSettings.
'        Used by CalcKellySizing() in MainForm_Render for display-only position sizing advisory.
'        Defaults: AccountSizeUsd=1000, UseHalfKelly=True, MaxRiskFraction=0.05,
'                  ContractFaceUsd=10, MinCalibrationSamples=30,
'                  EstProbFloor=0.45, EstProbScale=0.20.

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
    <JsonPropertyName("EMA200")>   Public Property EMA200   As New Ema200Settings
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
    <JsonPropertyName("partial_oversold")>   Public Property PartialOversold    As Double  = 50.0
    <JsonPropertyName("partial_overbought")> Public Property PartialOverbought  As Double  = 50.0
    <JsonPropertyName("divergence_price_gate")> Public Property DivergencePriceGate As Double = 0.001
    <JsonPropertyName("divergence_rsi_delta")>  Public Property DivergenceRsiDelta  As Double = 2.0
    ''' <summary>[P13] v0.50: RSI level above which BEARISH div triggers long penalty. Default 65.</summary>
    <JsonPropertyName("div_penalty_rsi_high")> Public Property DivPenaltyRsiHigh As Double = 65.0
    ''' <summary>[P13] v0.50: RSI level below which BULLISH div triggers short penalty. Default 35.</summary>
    <JsonPropertyName("div_penalty_rsi_low")>  Public Property DivPenaltyRsiLow  As Double = 35.0
    ''' <summary>[T3-B] Half-width of the pivot detection window for RSI divergence scan. Default 2.</summary>
    <JsonPropertyName("pivot_wing")>           Public Property PivotWing         As Integer = 2
    ''' <summary>[T3-B] Number of bars to look back when scanning for RSI pivots. Default 20.</summary>
    <JsonPropertyName("lookback_bars")>        Public Property LookbackBars      As Integer = 20
End Class

Public Class RocSettings
    <JsonPropertyName("period")>            Public Property Period           As Integer = 9
    <JsonPropertyName("slope_sensitivity")> Public Property SlopeSensitivity As Double  = 0.1
    <JsonPropertyName("series_lookback")>   Public Property SeriesLookback   As Integer = 3
End Class

Public Class VwapSettings
    <JsonPropertyName("dev_threshold_pct")>      Public Property DevThresholdPct      As Double  = 0.30
    <JsonPropertyName("session1_start_hour")>    Public Property Session1StartHour    As Integer = 0
    <JsonPropertyName("session1_start_minute")>  Public Property Session1StartMinute  As Integer = 0
    <JsonPropertyName("session2_start_hour")>    Public Property Session2StartHour    As Integer = 13
    <JsonPropertyName("session2_start_minute")>  Public Property Session2StartMinute  As Integer = 30
    <JsonPropertyName("warmup_candles")>         Public Property WarmupCandles        As Integer = 15
End Class

Public Class BbwSettings
    <JsonPropertyName("period")>                  Public Property Period                As Integer = 20
    <JsonPropertyName("std_dev")>                 Public Property StdDev                As Double  = 2.0
    <JsonPropertyName("releasing_roc_threshold")> Public Property ReleasingRocThreshold As Double  = 0.1
End Class

Public Class EmaSettings
    <JsonPropertyName("fast")> Public Property Fast As Integer = 9
    <JsonPropertyName("mid")>  Public Property Mid  As Integer = 21
    <JsonPropertyName("slow")> Public Property Slow As Integer = 50
End Class

Public Class Ema200Settings
    <JsonPropertyName("timeframe_minutes")> Public Property TimeframeMinutes As Integer = 5
End Class

Public Class DonchianSettings
    <JsonPropertyName("period")> Public Property Period As Integer = 20
End Class

Public Class ObvSettings
    <JsonPropertyName("lookback")>        Public Property Lookback       As Integer = 10
    <JsonPropertyName("trend_gate")>      Public Property TrendGate      As Double  = 0.001
    <JsonPropertyName("divergence_gate")> Public Property DivergenceGate As Double  = 0.001
End Class

Public Class AtrSettings
    <JsonPropertyName("period")>     Public Property Period    As Integer = 7
    <JsonPropertyName("ref_period")> Public Property RefPeriod As Integer = 20
    <JsonPropertyName("static_ref")> Public Property StaticRef As Double  = 150.0
    <JsonPropertyName("scale_min")>  Public Property ScaleMin  As Double  = 0.25
    <JsonPropertyName("scale_max")>  Public Property ScaleMax  As Double  = 4.0
End Class

Public Class OfiSettings
    <JsonPropertyName("book_depth")>          Public Property BookDepth         As Integer = 5
    <JsonPropertyName("buy_dominant_ratio")>  Public Property BuyDominantRatio  As Double  = 3.0
    <JsonPropertyName("sell_dominant_ratio")> Public Property SellDominantRatio As Double  = 0.333
End Class

Public Class VolumeSettings
    <JsonPropertyName("sma_period")>             Public Property SmaPeriod           As Integer = 9
    <JsonPropertyName("static_high")>            Public Property StaticHigh          As Double  = 3.0
    <JsonPropertyName("static_mid")>             Public Property StaticMid           As Double  = 2.0
    <JsonPropertyName("dynamic_high_clamp_min")> Public Property DynamicHighClampMin As Double  = 1.5
    <JsonPropertyName("dynamic_high_clamp_max")> Public Property DynamicHighClampMax As Double  = 6.0
    <JsonPropertyName("dynamic_mid_clamp_min")>  Public Property DynamicMidClampMin  As Double  = 1.2
    <JsonPropertyName("dynamic_mid_clamp_max")>  Public Property DynamicMidClampMax  As Double  = 4.0
End Class

Public Class VwapDynamicSettings
    <JsonPropertyName("dev_clamp_min")>   Public Property DevClampMin    As Double = 0.30
    <JsonPropertyName("dev_clamp_max")>   Public Property DevClampMax    As Double = 3.0
    <JsonPropertyName("static_fallback")> Public Property StaticFallback As Double = 1.5
End Class

Public Class LiquidationSettings
    <JsonPropertyName("long_liq_threshold")>  Public Property LongLiqThreshold  As Double = 50000.0
    <JsonPropertyName("short_liq_threshold")> Public Property ShortLiqThreshold As Double = 50000.0
    <JsonPropertyName("large_liq_size")>      Public Property LargeLiqSize      As Double = 200.0
    <JsonPropertyName("dominance_ratio")>     Public Property DominanceRatio    As Double = 2.0
End Class

Public Class OiSettings
    <JsonPropertyName("neutral_band_pct")>     Public Property NeutralBandPct     As Double = 0.05
    <JsonPropertyName("change_threshold_pct")> Public Property ChangeThresholdPct As Double = 0.01
End Class

Public Class DmiSettings
    <JsonPropertyName("period")> Public Property Period As Integer = 9
End Class

Public Class CvdSettings
    <JsonPropertyName("slope_min_usd")>        Public Property SlopeMinUsd         As Double  = 1000.0
    <JsonPropertyName("slope_pct_of_value")>   Public Property SlopePctOfValue     As Double  = 0.01
    <JsonPropertyName("divergence_price_gate")> Public Property DivergencePriceGate As Double  = 0.0005
    <JsonPropertyName("trade_lookback")>        Public Property TradeLookback       As Integer = 100
    ''' <summary>[P13] v0.50: Score penalty magnitude for CVD divergence. Default 1.</summary>
    <JsonPropertyName("divergence_penalty")>   Public Property DivergencePenalty   As Integer = 1
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
    <JsonPropertyName("accel_threshold")> Public Property AccelThreshold As Double  = 5000.0
    <JsonPropertyName("decel_penalty")>   Public Property DecelPenalty   As Integer = 1
End Class

''' <summary>
''' [T3-C] TTM Squeeze tuning parameters.
''' FlatThreshold: histogram bars whose absolute value is below this are treated as FLAT momentum.
''' Default 0.5 matches the method's previous hardcoded behaviour.
''' </summary>
Public Class TtmSettings
    <JsonPropertyName("flat_threshold")> Public Property FlatThreshold As Double = 0.5
End Class

''' <summary>
''' [T3-A] VPFR-lite tuning parameters.
''' NumBuckets: number of price buckets for the volume profile histogram.
''' Default 50 matches the method's previous hardcoded behaviour.
''' </summary>
Public Class VpfrSettings
    <JsonPropertyName("num_buckets")> Public Property NumBuckets As Integer = 50
End Class

' ---------------------------------------------------------------------------
' Kelly sizing settings
' ---------------------------------------------------------------------------

''' <summary>
''' Display-only position sizing parameters for the Kelly Criterion block.
''' All computation happens in MainForm_Render.CalcKellySizing() -- no scoring impact.
''' EST mode active until CalibrationReport reaches READY; CAL mode deferred.
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
    ''' <summary>Min logged trades per verdict tier before switching EST->CAL mode. Default 30.</summary>
    <JsonPropertyName("min_calibration_samples")>   Public Property MinCalibrationSamples As Integer = 30
    ''' <summary>Score-to-probability band floor (pre-calibration). Default 0.45.</summary>
    <JsonPropertyName("est_prob_floor")>            Public Property EstProbFloor          As Double  = 0.45
    ''' <summary>Score-to-probability band scale range (pre-calibration). Default 0.20 -> band [0.45, 0.65].</summary>
    <JsonPropertyName("est_prob_scale")>            Public Property EstProbScale          As Double  = 0.20
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
    <JsonPropertyName("enabled")>           Public Property Enabled         As Boolean = True
    <JsonPropertyName("candle_count")>      Public Property CandleCount     As Integer = 60
    <JsonPropertyName("ema_period_fast")>   Public Property EmaPeriodFast   As Integer = 9
    <JsonPropertyName("ema_period_slow")>   Public Property EmaPeriodSlow   As Integer = 21
    <JsonPropertyName("dmi_period")>        Public Property DmiPeriod       As Integer = 9
    <JsonPropertyName("required_confirms")> Public Property RequiredConfirms As Integer = 2
End Class

' ---------------------------------------------------------------------------
' Auto-run settings
' ---------------------------------------------------------------------------

''' <summary>Controls the auto-run timer. Minimum effective interval 10s (enforced in MainForm).</summary>
Public Class AutoRunSettings
    <JsonPropertyName("enabled")>          Public Property Enabled         As Boolean = False
    <JsonPropertyName("interval_minutes")> Public Property IntervalMinutes As Integer = 1
    <JsonPropertyName("interval_seconds")> Public Property IntervalSeconds As Integer = 0
End Class

' ---------------------------------------------------------------------------
' Scoring settings
' ---------------------------------------------------------------------------

Public Class ScoringSettings
    <JsonPropertyName("verdict_strong_pct")> Public Property VerdictStrongPct As Double  = 0.70
    <JsonPropertyName("verdict_med_pct")>    Public Property VerdictMedPct    As Double  = 0.53
    <JsonPropertyName("verdict_weak_pct")>   Public Property VerdictWeakPct   As Double  = 0.35
    <JsonPropertyName("transitional_penalty_enabled")> Public Property TransitionalPenaltyEnabled As Boolean = True
    <JsonPropertyName("funding_high_positive")> Public Property FundingHighPositive As Double = 0.001
    <JsonPropertyName("funding_low_positive")>  Public Property FundingLowPositive  As Double = 0.0005
    <JsonPropertyName("funding_high_negative")> Public Property FundingHighNegative As Double = -0.001
    <JsonPropertyName("funding_low_negative")>  Public Property FundingLowNegative  As Double = -0.0005
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
    ''' <summary>ATR distance multiplier for raw target price. Default 2.0.</summary>
    <JsonPropertyName("atr_target_multiplier")> Public Property AtrTargetMultiplier As Double = 2.0
    ''' <summary>ATR distance multiplier for stop-loss price. Default 1.0.</summary>
    <JsonPropertyName("atr_stop_multiplier")>  Public Property AtrStopMultiplier  As Double  = 1.0
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
End Class

' ---------------------------------------------------------------------------
' Regime gate settings
' ---------------------------------------------------------------------------

Public Class RegimeGateSettings
    <JsonPropertyName("suppress_long_in_trending_down")>  Public Property SuppressLongInTrendingDown  As Boolean = True
    <JsonPropertyName("suppress_short_in_trending_up")>   Public Property SuppressShortInTrendingUp   As Boolean = True
    <JsonPropertyName("transitional_adx_penalty_low")>    Public Property TransitionalAdxPenaltyLow   As Double  = 20.0
    <JsonPropertyName("transitional_adx_penalty_mid")>    Public Property TransitionalAdxPenaltyMid   As Double  = 22.5
    <JsonPropertyName("transitional_adx_penalty_high")>   Public Property TransitionalAdxPenaltyHigh  As Double  = 25.0
    <JsonPropertyName("transitional_penalty_low")>        Public Property TransitionalPenaltyLow      As Integer = 2
    <JsonPropertyName("transitional_penalty_mid")>        Public Property TransitionalPenaltyMid      As Integer = 1
End Class
