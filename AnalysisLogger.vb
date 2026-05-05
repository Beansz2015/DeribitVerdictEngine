' AnalysisLogger.vb  v0.4.1
' Appends one row per analysis run to a local CSV file.
' File location: same directory as the executable.
' Reset: truncates file back to header only.
'
' v0.4.1 (d1+d2): Column 87 added: TrendStructure5m.
'         BestPivotByVolume5m (col 85) and BestPivotVolumeRatio5m (col 86) now populated.
'         Log rotation: if existing file does not match v0.4.1 header,
'         it is renamed to analysis_log.csv.v0.4.bak (timestamped if .bak exists).
'
' v0.4: Header expanded with 18 new columns (cols 69-86):
'       SpreadBps, OFIMomentum, FundingDelta,
'       VPFRVAH, VPFRVAL, VPFRNearestHvnAbove, VPFRNearestHvnBelow,
'       LastSwingHigh5m, LastSwingLow5m, LastSwingHigh15m, LastSwingLow15m,
'       SwingTargetLong, SwingTargetShort, SwingStopLong, SwingStopShort,
'       TargetCapReason, BestPivotByVolume5m, BestPivotVolumeRatio5m.
'
' v0.3: Header expanded with VerdictContext, FundingMomentum, OiCvdOutcome columns.
'
' v0.2: Header and data row expanded to include all current IndicatorResults fields.

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
        "VerdictContext,FundingMomentum,OiCvdOutcome," &
        "SpreadBps,OFIMomentum,FundingDelta," &
        "VPFRVAH,VPFRVAL,VPFRNearestHvnAbove,VPFRNearestHvnBelow," &
        "LastSwingHigh5m,LastSwingLow5m,LastSwingHigh15m,LastSwingLow15m," &
        "SwingTargetLong,SwingTargetShort,SwingStopLong,SwingStopShort," &
        "TargetCapReason,BestPivotByVolume5m,BestPivotVolumeRatio5m," &
        "TrendStructure5m"

    Public Shared Function GetLogPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    Public Shared Function GetRowCount() As Integer
        Dim path As String = GetLogPath()
        If Not File.Exists(path) Then Return 0
        Return Math.Max(0, File.ReadAllLines(path).Length - 1)
    End Function

    ' Called once per LogRun. Handles log rotation: if the existing file has a
    ' different header (i.e. old schema), it is renamed to .v0.3.bak before a
    ' fresh v0.4 file is created.
    Public Shared Sub EnsureLogFile()
        Dim path As String = GetLogPath()
        If Not File.Exists(path) Then
            WriteHeader(path)
            Return
        End If

        Try
            Dim firstLine As String = Nothing
            Using sr As New StreamReader(path)
                firstLine = sr.ReadLine()
            End Using

            If firstLine Is Nothing OrElse firstLine.Trim() <> Header Then
                ' Schema mismatch — rotate old file
                Dim dir As String = System.IO.Path.GetDirectoryName(path)
                Dim bakPath As String = System.IO.Path.Combine(dir, "analysis_log.csv.v0.4.bak")
                If File.Exists(bakPath) Then
                    Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                    bakPath = System.IO.Path.Combine(dir, "analysis_log.csv.v0.3." & ts & ".bak")
                End If
                File.Move(path, bakPath)
                WriteHeader(path)
            End If
        Catch
            ' Silent fail — if we can't rotate, we'll catch the schema mismatch at read time
        End Try
    End Sub

    Private Shared Sub WriteHeader(path As String)
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(Header)
            End Using
        Catch
        End Try
    End Sub

    Public Shared Sub LogRun(r As IndicatorResults, v As VerdictResult)
        EnsureLogFile()
        Dim path As String = GetLogPath()
        Try
            Using sw As New StreamWriter(path, append:=True)
                Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
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
                    If(v.OiCvdOutcome, "NONE"),
                    r.SpreadBps.ToString("F4"),
                    If(r.OFIMomentum, "FLAT"),
                    r.FundingDelta.ToString("F8"),
                    r.VPFRVah.ToString("F2"),
                    r.VPFRVal.ToString("F2"),
                    r.VPFRNearestHvnAbove.ToString("F2"),
                    r.VPFRNearestHvnBelow.ToString("F2"),
                    r.LastSwingHigh5m.ToString("F2"),
                    r.LastSwingLow5m.ToString("F2"),
                    r.LastSwingHigh15m.ToString("F2"),
                    r.LastSwingLow15m.ToString("F2"),
                    r.SwingTargetLong.ToString("F2"),
                    r.SwingTargetShort.ToString("F2"),
                    r.SwingStopLong.ToString("F2"),
                    r.SwingStopShort.ToString("F2"),
                    If(String.IsNullOrEmpty(v.TargetCapReason), "none", v.TargetCapReason),
                    r.BestPivotByVolume5m.ToString("F2"),
                    r.BestPivotVolumeRatio5m.ToString("F2"),
                    r.TrendStructure.ToString()))
            End Using
        Catch
            ' Silent fail — logging must never crash the main pipeline
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
