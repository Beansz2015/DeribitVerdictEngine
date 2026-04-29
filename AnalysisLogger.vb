' AnalysisLogger.vb  v0.3
' Appends one row per analysis run to a local CSV file.
' File location: same directory as the executable.
' Reset: truncates file back to header only.
'
' v0.3: Header expanded with VerdictContext, FundingMomentum, OiCvdOutcome columns
'       (closes Section 16.3 prerequisite item 4 — auto-tweaker calibration data).
'       Existing CSV files written by v0.2 are column-incompatible.
'       Use ResetLog() (Reset Log link in UI) after deploying this version.
'
' v0.2: Header and data row expanded to include all current IndicatorResults fields:
'       CVD (Value, Slope, Divergence)
'       MTF Gate (Pass, Trend, ADX, EMAAlignment, Reason)
'       VWAP (DevPct, SessionCandles, Sigma1Upper/Lower, Sigma2Upper/Lower)
'       TTM Squeeze (Histogram, Direction, Signal)
'       BBW (Value, SqueezeStatus)
'       OI (Change15m, Change60m, Signal)
'       Funding (Rate, Bias)
'       EMA (9, 21, 50, Alignment, EMA200_5m, PriceVsEMA200)
'       DMI (PlusDI, MinusDI, ADX)
'       Donchian (Upper, Lower, Signal)
'       Scores (MaxScore, EffectiveLongScore, EffectiveShortScore, RegimePenalty)
'       OFIBidVol, OFIAskVol added to existing OFI columns

Imports System.IO

Public Class AnalysisLogger

    Private Const FileName As String = "analysis_log.csv"

    Private Shared ReadOnly Header As String =
        "Timestamp,Price,Verdict,Confidence," &
        "LongScore,ShortScore,EffectiveLongScore,EffectiveShortScore,MaxScore,RegimePenalty," &
        "Regime,ADX,PlusDI,MinusDI," &
        "ROC,ROCSlope,RSI,RSIDivergence," &
        "VolumeRatio," &
        "VWAP,VWAPDevPct,VWAPSessionCandles," &
        "VWAPSigma1Upper,VWAPSigma1Lower,VWAPSigma2Upper,VWAPSigma2Lower," &
        "BBW,SqueezeStatus," &
        "TTMHistogram,TTMDirection,TTMSignal," &
        "EMA9,EMA21,EMA50,EMAAlignment,EMA200_5m,PriceVsEMA200," &
        "FundingRate,FundingBias," &
        "OI_Current,OIChange15m,OIChange60m,OISignal," &
        "OFIRatio,OFIBidVol,OFIAskVol,OFISignal," &
        "CVDValue,CVDSlope,CVDDivergence," &
        "LiqLongSize,LiqShortSize,LiqSignal," &
        "DonchianUpper,DonchianLower,DonchianSignal," &
        "OBVTrend,OBVDivergence," &
        "MTFGatePass,MTF15mTrend,MTF15mADX,MTF15mEMAAlignment,MTFGateReason," &
        "ATR,ATRMultiplier," &
        "VerdictContext,FundingMomentum,OiCvdOutcome"

    Public Shared Function GetLogPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    Public Shared Function GetRowCount() As Integer
        Dim path As String = GetLogPath()
        If Not File.Exists(path) Then Return 0
        ' Subtract 1 for header row
        Return Math.Max(0, File.ReadAllLines(path).Length - 1)
    End Function

    Public Shared Sub LogRun(r As IndicatorResults, v As VerdictResult)
        Dim path As String = GetLogPath()
        Dim writeHeader As Boolean = Not File.Exists(path) OrElse New FileInfo(path).Length = 0
        Try
            Using sw As New StreamWriter(path, append:=True)
                If writeHeader Then sw.WriteLine(Header)
                Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                ' MTFGateReason may contain commas -- strip them to keep CSV clean
                Dim mtfReason As String = If(r.MTFGateReason, "").Replace(",", ";")
                sw.WriteLine(String.Join(",",
                    ts,
                    r.CurrentPrice.ToString("F2"),
                    v.Verdict,
                    v.Confidence,
                    v.LongScore.ToString(),
                    v.ShortScore.ToString(),
                    v.EffectiveLongScore.ToString(),
                    v.EffectiveShortScore.ToString(),
                    v.MaxScore.ToString(),
                    v.RegimePenalty.ToString(),
                    r.Regime,
                    r.ADX.ToString("F2"),
                    r.PlusDI.ToString("F2"),
                    r.MinusDI.ToString("F2"),
                    r.ROC.ToString("F4"),
                    r.ROCSlope,
                    r.RSI.ToString("F2"),
                    r.RSIDivergence,
                    r.VolumeRatio.ToString("F4"),
                    r.VWAP.ToString("F2"),
                    r.VWAPDevPct.ToString("F4"),
                    r.VWAPSessionCandles.ToString(),
                    r.VWAPSigma1Upper.ToString("F2"),
                    r.VWAPSigma1Lower.ToString("F2"),
                    r.VWAPSigma2Upper.ToString("F2"),
                    r.VWAPSigma2Lower.ToString("F2"),
                    r.BBW.ToString("F4"),
                    r.SqueezeStatus,
                    r.TTMHistogram.ToString("F4"),
                    r.TTMDirection,
                    r.TTMSignal,
                    r.EMA9.ToString("F2"),
                    r.EMA21.ToString("F2"),
                    r.EMA50.ToString("F2"),
                    r.EMAAlignment,
                    r.EMA200_5m.ToString("F2"),
                    r.PriceVsEMA200,
                    r.FundingRate.ToString("F6"),
                    r.FundingBias,
                    r.OI_Current.ToString("F0"),
                    r.OIChange15m.ToString("F4"),
                    r.OIChange60m.ToString("F4"),
                    r.OISignal,
                    r.OFIRatio.ToString("F4"),
                    r.OFIBidVol.ToString("F2"),
                    r.OFIAskVol.ToString("F2"),
                    r.OFISignal,
                    r.CVDValue.ToString("F0"),
                    r.CVDSlope,
                    r.CVDDivergence,
                    r.LiqLongSize.ToString("F2"),
                    r.LiqShortSize.ToString("F2"),
                    r.LiqSignal,
                    r.DonchianUpper.ToString("F2"),
                    r.DonchianLower.ToString("F2"),
                    r.DonchianSignal,
                    r.OBVTrend,
                    r.OBVDivergence,
                    r.MTFGatePass.ToString(),
                    r.MTF15mTrend,
                    r.MTF15mADX.ToString("F2"),
                    r.MTF15mEMAAlignment,
                    mtfReason,
                    r.ATR.ToString("F4"),
                    r.ATRSizeMultiplier.ToString("F4"),
                    If(v.VerdictContext, "CONFIRMED"),
                    If(r.FundingMomentum, "FLAT"),
                    If(v.OiCvdOutcome, "NONE")))
            End Using
        Catch
            ' Silent fail -- logging must never crash the main pipeline
        End Try
    End Sub

    Public Shared Sub ResetLog()
        Dim path As String = GetLogPath()
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(Header)
            End Using
        Catch
        End Try
    End Sub

End Class
