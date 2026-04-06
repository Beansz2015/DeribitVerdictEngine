' Core/Settings/EngineSettings.vb  v0.30
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
'        Added ScoringWeights.CVD.

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
End Class

Public Class RocSettings
    <JsonPropertyName("period")>            Public Property Period          As Integer = 9
    <JsonPropertyName("slope_sensitivity")> Public Property SlopeSensitivity As Double = 0.1
    <JsonPropertyName("series_lookback")>   Public Property SeriesLookback  As Integer = 3
End Class

Public Class VwapSettings
    <JsonPropertyName("dev_threshold_pct")> Public Property DevThresholdPct As Double = 0.30
End Class

Public Class BbwSettings
    <JsonPropertyName("period")>                  Public Property Period               As Integer = 20
    <JsonPropertyName("std_dev")>                 Public Property StdDev               As Double  = 2.0
    <JsonPropertyName("releasing_roc_threshold")> Public Property ReleasingRocThreshold As Double = 0.1
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
    ''' <summary>Minimum USD delta required to register a slope direction (absolute floor).</summary>
    <JsonPropertyName("slope_min_usd")>          Public Property SlopeMinUsd         As Double  = 1000.0
    ''' <summary>Slope threshold as a fraction of |CVDValue|. Actual threshold = Max(SlopeMinUsd, |CVDValue| * SlopePctOfValue).</summary>
    <JsonPropertyName("slope_pct_of_value")>      Public Property SlopePctOfValue     As Double  = 0.01
    ''' <summary>Minimum price move (fraction) to trigger divergence evaluation.</summary>
    <JsonPropertyName("divergence_price_gate")>   Public Property DivergencePriceGate As Double  = 0.0005
    ''' <summary>Number of most-recent trades used for CVD calculation.</summary>
    <JsonPropertyName("trade_lookback")>          Public Property TradeLookback       As Integer = 100
End Class

' ---------------------------------------------------------------------------
' Scoring settings
' ---------------------------------------------------------------------------

Public Class ScoringSettings
    <JsonPropertyName("long_threshold")>         Public Property LongThreshold        As Integer = 6
    <JsonPropertyName("short_threshold")>        Public Property ShortThreshold       As Integer = 6
    <JsonPropertyName("strong_long_threshold")>  Public Property StrongLongThreshold  As Integer = 12
    <JsonPropertyName("strong_short_threshold")> Public Property StrongShortThreshold As Integer = 12
    <JsonPropertyName("medium_long_threshold")>  Public Property MediumLongThreshold  As Integer = 9
    <JsonPropertyName("medium_short_threshold")> Public Property MediumShortThreshold As Integer = 9
    ''' <summary>Score fraction required for STRONG verdict (default 70%).</summary>
    <JsonPropertyName("verdict_strong_pct")>     Public Property VerdictStrongPct     As Double  = 0.70
    ''' <summary>Score fraction required for MEDIUM verdict (default 53%).</summary>
    <JsonPropertyName("verdict_med_pct")>        Public Property VerdictMedPct        As Double  = 0.53
    ''' <summary>Score fraction required for WEAK verdict (default 35%).</summary>
    <JsonPropertyName("verdict_weak_pct")>       Public Property VerdictWeakPct       As Double  = 0.35
    <JsonPropertyName("transitional_penalty_enabled")> Public Property TransitionalPenaltyEnabled As Boolean = True
    <JsonPropertyName("funding_high_positive")>  Public Property FundingHighPositive  As Double = 0.001
    <JsonPropertyName("funding_low_positive")>   Public Property FundingLowPositive   As Double = 0.0005
    <JsonPropertyName("funding_high_negative")>  Public Property FundingHighNegative  As Double = -0.001
    <JsonPropertyName("funding_low_negative")>   Public Property FundingLowNegative   As Double = -0.0005
    <JsonPropertyName("weights")>                Public Property Weights              As New ScoringWeights
End Class

Public Class ScoringWeights
    <JsonPropertyName("ROC")>        Public Property ROC        As Integer = 1
    <JsonPropertyName("RSI")>        Public Property RSI        As Integer = 1
    <JsonPropertyName("DMI")>        Public Property DMI        As Integer = 1
    <JsonPropertyName("ADX")>        Public Property ADX        As Integer = 1
    <JsonPropertyName("Volume")>     Public Property Volume     As Integer = 1
    <JsonPropertyName("VWAP")>       Public Property VWAP       As Integer = 1
    <JsonPropertyName("BBW")>        Public Property BBW        As Integer = 1
    <JsonPropertyName("EMA")>        Public Property EMA        As Integer = 1
    <JsonPropertyName("OI")>         Public Property OI         As Integer = 1
    <JsonPropertyName("OFI")>        Public Property OFI        As Integer = 1
    <JsonPropertyName("CVD")>        Public Property CVD        As Integer = 1
    <JsonPropertyName("LiqPenalty")> Public Property LiqPenalty As Integer = 1
    <JsonPropertyName("EMA200")>     Public Property EMA200     As Integer = 1
    <JsonPropertyName("Donchian")>   Public Property Donchian   As Integer = 1
    <JsonPropertyName("OBV")>        Public Property OBV        As Integer = 1
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
