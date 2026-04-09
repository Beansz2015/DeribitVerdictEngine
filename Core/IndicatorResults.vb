' Core/IndicatorResults.vb
' All output properties for the indicator calculation layer.
' No logic -- data container only.

Public Class IndicatorResults
    ' Core
    Public Property ROC As Double
    Public Property ROCSlope As String       ' "RISING" / "FALLING" / "FLAT"
    Public Property RSI As Double
    Public Property RSIDivergence As String  ' NONE / BULLISH / BEARISH
    Public Property ATR As Double
    Public Property ATRAvg20d As Double
    Public Property ATRSizeMultiplier As Double
    Public Property VolumeSMA9 As Double
    Public Property CurrentVolume As Double  ' BTC volume -- used for scoring
    Public Property CurrentVolumeUSD As Double ' USD volume -- display only
    Public Property VolumeRatio As Double

    ' Trend (5m)
    Public Property PlusDI As Double
    Public Property MinusDI As Double
    Public Property ADX As Double
    Public Property Regime As String         ' TRENDING_UP / TRENDING_DOWN / RANGE_BOUND / TRANSITIONAL

    ' Tier 1
    Public Property VWAP As Double
    Public Property VWAPDevPct As Double
    Public Property VWAPSessionCandles As Integer  ' number of candles in current VWAP session
    Public Property VWAPSigma1Upper As Double       ' VWAP + 1 sigma
    Public Property VWAPSigma1Lower As Double       ' VWAP - 1 sigma
    Public Property VWAPSigma2Upper As Double       ' VWAP + 2 sigma
    Public Property VWAPSigma2Lower As Double       ' VWAP - 2 sigma
    Public Property BBW As Double
    Public Property SqueezeStatus As String  ' ACTIVE / RELEASING / NONE
    Public Property TTMHistogram As Double   ' positive = bullish momentum, negative = bearish
    Public Property TTMDirection As String   ' "RISING" / "FALLING" / "FLAT"
    Public Property TTMSignal As String      ' "BULL_BUILDING" / "BEAR_BUILDING" / "BULL_FADING" / "BEAR_FADING" / "FLAT"
    Public Property EMA9 As Double
    Public Property EMA21 As Double
    Public Property EMA50 As Double
    Public Property EMAAlignment As String   ' BULL / BEAR / MIXED
    Public Property FundingRate As Double    ' raw 8h decimal e.g. 0.0001
    Public Property FundingBias As String
    Public Property OI_Current As Double
    Public Property OI_Prev15m As Double
    Public Property OI_Prev60m As Double
    Public Property OIChange15m As Double    ' % change
    Public Property OIChange60m As Double
    Public Property OISignal As String       ' NEW LONGS / NEW SHORTS / COVERING / CAPITULATION / NEUTRAL

    ' Tier 2
    Public Property OFIRatio As Double
    Public Property OFISignal As String      ' BUY DOMINANT / SELL DOMINANT / BALANCED
    Public Property OFIBidVol As Double      ' weighted bid volume (top-3, w=3,2,1) -- display only
    Public Property OFIAskVol As Double      ' weighted ask volume (top-3, w=3,2,1) -- display only
    Public Property LiqLongSize As Double
    Public Property LiqShortSize As Double
    Public Property LiqSignal As String
    Public Property EMA200_5m As Double
    Public Property PriceVsEMA200 As String  ' ABOVE / BELOW
    Public Property CVDValue As Double       ' net USD delta (buy-sell) over last N trades
    Public Property CVDSlope As String       ' "RISING" / "FALLING" / "FLAT"
    Public Property CVDDivergence As String  ' "BULLISH" / "BEARISH" / "NONE"

    ' TFI (Trade Flow Index) -- rolling window buy/sell pressure ratio
    Public Property TFIValue As Double       ' normalised [-1, +1]; positive = buy pressure
    Public Property TFISignal As String      ' "BUY PRESSURE" / "SELL PRESSURE" / "NEUTRAL"

    ' MicroCVD -- intra-window CVD segmentation (early / mid / late thirds)
    Public Property MicroCVDEarly As Double  ' USD delta, first third of trade window
    Public Property MicroCVDMid As Double    ' USD delta, middle third
    Public Property MicroCVDLate As Double   ' USD delta, last third
    Public Property MicroCVDMomentum As String  ' "ACCELERATING" / "DECELERATING" / "FLAT"
    Public Property MicroCVDSignal As String    ' "BULL_ACCEL" / "BEAR_ACCEL" / "BULL_DECEL" / "BEAR_DECEL" / "FLAT"

    ' MTF Gate (15m timeframe)
    Public Property MTF15mTrend As String        ' "BULL" / "BEAR" / "FLAT"
    Public Property MTF15mADX As Double          ' ADX value computed on 15m candles
    Public Property MTF15mEMAAlignment As String ' "BULL" / "BEAR" / "MIXED"
    Public Property MTFGatePass As Boolean       ' True = gate passed (or disabled)
    Public Property MTFGateReason As String      ' human-readable gate result for display

    ' Tier 3
    Public Property DonchianUpper As Double
    Public Property DonchianLower As Double
    Public Property DonchianSignal As String ' LONG / SHORT / NONE
    Public Property OBVTrend As String       ' RISING / FALLING / FLAT
    Public Property OBVDivergence As String  ' NONE / BEARISH / BULLISH

    ' VPFR-lite (derived from 1m candle volume distribution)
    Public Property VPFRPoc As Double        ' Point of Control mid-price ($)
    Public Property VPFRHVNearPoc As Boolean ' True = current price within hvnProximityPct of POC
    Public Property VPFRSignal As String     ' NEAR_HVN_SUPPORT / NEAR_HVN_RESIST / IN_LVN_BULL / IN_LVN_BEAR / NEUTRAL

    ' Current price (latest close of 1m candles)
    Public Property CurrentPrice As Double
End Class
