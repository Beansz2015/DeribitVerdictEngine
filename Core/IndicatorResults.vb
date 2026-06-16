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
    Public Property FundingMomentum As String  ' "RISING" / "FALLING" / "FLAT" -- computed from rolling history in MainForm_Layout
    Public Property OI_Current As Double
    Public Property OIChange15m As Double    ' % change
    Public Property OIChange60m As Double
    Public Property OISignal As String       ' NEW LONGS / NEW SHORTS / COVERING / CAPITULATION / NEUTRAL

    ' Tier 2
    Public Property OFIRatio As Double
    Public Property OFISignal As String      ' BUY DOMINANT / SELL DOMINANT / BALANCED
    Public Property OFIMomentum As String    ' "RISING" | "FALLING" | "FLAT"
    Public Property OFIBidVol As Double      ' weighted bid volume (top-3, w=3,2,1) -- display only
    Public Property OFIAskVol As Double      ' weighted ask volume (top-3, w=3,2,1) -- display only
    Public Property SpreadBps    As Double   ' best-bid/ask spread in basis points
    Public Property SpreadStatus As String  ' "TIGHT" | "NORMAL" | "WIDE"
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
    Public Property MTFGatePassLong As Boolean   ' True = 15m trend does not oppose a long (BULL/FLAT, or no data)
    Public Property MTFGatePassShort As Boolean  ' True = 15m trend does not oppose a short (BEAR/FLAT, or no data)
    Public Property MTFGateDetails As String     ' direction-free 15m metrics; final reason composed at Step 4b (VerdictResult.MTFGateReason)

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
    ' VPFR-lite v2: value area + nearest walls
    Public Property VPFRVah             As Double  ' Value Area High ($)
    Public Property VPFRVal             As Double  ' Value Area Low ($)
    Public Property VPFRValueAreaSignal As String  ' "INSIDE_VA" | "ABOVE_VAH" | "BELOW_VAL"
    Public Property VPFRNearestHvnAbove As Double  ' nearest HVN price above current ($), 0 = none
    Public Property VPFRNearestHvnBelow As Double  ' nearest HVN price below current ($), 0 = none
    Public Property VPFRNearestLvnAbove As Double  ' nearest LVN price above current ($), 0 = none
    Public Property VPFRNearestLvnBelow As Double  ' nearest LVN price below current ($), 0 = none
    ' VPFR-lite histogram buckets (Spec B — display only, populated alongside the v2 fields above).
    ' Engine-side ordering: index 0 = lowest-price bucket. UI reverses for top-down render.
    Public Property VPFRBucketVolumes  As Double() = Array.Empty(Of Double)()
    Public Property VPFRBucketPriceLow As Double
    Public Property VPFRBucketSize     As Double

    ' Swing pivots (5m primary, 15m context)
    Public Property LastSwingHigh5m  As Double  ' price ($), 0 = no confirmed pivot in lookback
    Public Property LastSwingLow5m   As Double
    Public Property LastSwingHigh15m As Double  ' higher-timeframe context (optional, may stay 0)
    Public Property LastSwingLow15m  As Double
    ' Convenience computed at scoring time (direction-aware bookkeeping)
    Public Property SwingTargetLong  As Double  ' = LastSwingHigh5m if > CurrentPrice, else 0
    Public Property SwingStopLong    As Double  ' = LastSwingLow5m  if < CurrentPrice, else 0
    Public Property SwingTargetShort As Double  ' = LastSwingLow5m  if < CurrentPrice, else 0
    Public Property SwingStopShort   As Double  ' = LastSwingHigh5m if > CurrentPrice, else 0

    ' Funding delta (period-over-period change from _fundingHistory ring buffer)
    Public Property FundingDelta As Double   ' raw decimal, 0 when rate stable or first sample

    ' Volume-weighted pivot fields (D2 — d2-volume-weighted-pivots-proposal.md)
    Public Property BestPivotByVolume5m    As Double   ' price of highest-volume confirmed swing pivot in 5m lookback
    Public Property BestPivotVolumeRatio5m As Double   ' volume of best pivot / avg pivot volume in lookback
    Public Property BestPivotIsHigh5m      As Boolean  ' True if best-volume pivot is a swing high; False if swing low

    ' Trend structure classification (D1 — d1-trend-structure-proposal.md)
    Public Property TrendStructure   As TrendStructure  ' UPTREND/DOWNTREND/EXPANSION/CONTRACTION/UNDEFINED
    Public Property LastTwoHighs5m   As (Older As Double, Newer As Double)  ' display: the two highs that produced the classification
    Public Property LastTwoLows5m    As (Older As Double, Newer As Double)  ' display: the two lows

    ' Current price (latest close of the execution candles)
    Public Property CurrentPrice As Double

    ' [v36 session-timeframe-resolution] Execution resolution (minutes) this run was
    ' computed on. Stamped in RunAnalysisAsync right after the session is resolved.
    ' Default 1 so any path that doesn't set it (and every legacy row) is 1-min.
    ' Threads into ScoringEngine via r so the ROC magnitude override resolves at its
    ' read sites with no new Calculate parameter; logged to CSV v0.7 + eval cache v4.
    Public Property ExecResolution As Integer = 1

    ' [v36 batch-along, v34 follow-up] CalcCVD's internal weighted slope, surfaced for
    ' precise CVD threshold calibration (was an unlogged local). USD units. CSV v0.7.
    Public Property CVDWeightedSlope As Double
End Class
