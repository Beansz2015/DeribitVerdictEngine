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
Imports System.Globalization

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
                Dim capReasonCsv As String
                If v.Verdict.Contains("LONG") Then
                    capReasonCsv = NormaliseCapReason(v.TargetCapReasonLong)
                ElseIf v.Verdict.Contains("SHORT") Then
                    capReasonCsv = NormaliseCapReason(v.TargetCapReasonShort)
                ElseIf Not String.IsNullOrEmpty(v.TargetCapReasonLong) Then
                    capReasonCsv = NormaliseCapReason(v.TargetCapReasonLong)
                ElseIf Not String.IsNullOrEmpty(v.TargetCapReasonShort) Then
                    capReasonCsv = NormaliseCapReason(v.TargetCapReasonShort)
                Else
                    capReasonCsv = "none"
                End If
                sw.WriteLine(String.Join(",",
                    ts,
                    Inv(r.CurrentPrice, "F2"),
                    v.Verdict,
                    v.Confidence,
                    v.LongScore.ToString(),
                    v.ShortScore.ToString(),
                    v.EffectiveLongScore.ToString(),
                    v.EffectiveShortScore.ToString(),
                    v.MaxScore.ToString(),
                    v.RegimePenalty.ToString(),
                    r.Regime,
                    Inv(r.ADX, "F2"),
                    Inv(r.PlusDI, "F2"),
                    Inv(r.MinusDI, "F2"),
                    Inv(r.ROC, "F4"),
                    r.ROCSlope,
                    Inv(r.RSI, "F2"),
                    r.RSIDivergence,
                    Inv(r.VolumeRatio, "F4"),
                    Inv(r.VWAP, "F2"),
                    Inv(r.VWAPDevPct, "F4"),
                    r.VWAPSessionCandles.ToString(),
                    Inv(r.VWAPSigma1Upper, "F2"),
                    Inv(r.VWAPSigma1Lower, "F2"),
                    Inv(r.VWAPSigma2Upper, "F2"),
                    Inv(r.VWAPSigma2Lower, "F2"),
                    Inv(r.BBW, "F4"),
                    r.SqueezeStatus,
                    Inv(r.TTMHistogram, "F4"),
                    r.TTMDirection,
                    r.TTMSignal,
                    Inv(r.EMA9, "F2"),
                    Inv(r.EMA21, "F2"),
                    Inv(r.EMA50, "F2"),
                    r.EMAAlignment,
                    Inv(r.EMA200_5m, "F2"),
                    r.PriceVsEMA200,
                    Inv(r.FundingRate, "F6"),
                    r.FundingBias,
                    Inv(r.OI_Current, "F0"),
                    Inv(r.OIChange15m, "F4"),
                    Inv(r.OIChange60m, "F4"),
                    r.OISignal,
                    Inv(r.OFIRatio, "F4"),
                    Inv(r.OFIBidVol, "F2"),
                    Inv(r.OFIAskVol, "F2"),
                    r.OFISignal,
                    Inv(r.CVDValue, "F0"),
                    r.CVDSlope,
                    r.CVDDivergence,
                    Inv(r.LiqLongSize, "F2"),
                    Inv(r.LiqShortSize, "F2"),
                    r.LiqSignal,
                    Inv(r.DonchianUpper, "F2"),
                    Inv(r.DonchianLower, "F2"),
                    r.DonchianSignal,
                    r.OBVTrend,
                    r.OBVDivergence,
                    r.MTFGatePass.ToString(),
                    r.MTF15mTrend,
                    Inv(r.MTF15mADX, "F2"),
                    r.MTF15mEMAAlignment,
                    mtfReason,
                    Inv(r.ATR, "F4"),
                    Inv(r.ATRSizeMultiplier, "F4"),
                    If(v.VerdictContext, "CONFIRMED"),
                    If(r.FundingMomentum, "FLAT"),
                    If(v.OiCvdOutcome, "NONE"),
                    Inv(r.SpreadBps, "F4"),
                    If(r.OFIMomentum, "FLAT"),
                    Inv(r.FundingDelta, "F8"),
                    Inv(r.VPFRVah, "F2"),
                    Inv(r.VPFRVal, "F2"),
                    Inv(r.VPFRNearestHvnAbove, "F2"),
                    Inv(r.VPFRNearestHvnBelow, "F2"),
                    Inv(r.LastSwingHigh5m, "F2"),
                    Inv(r.LastSwingLow5m, "F2"),
                    Inv(r.LastSwingHigh15m, "F2"),
                    Inv(r.LastSwingLow15m, "F2"),
                    Inv(r.SwingTargetLong, "F2"),
                    Inv(r.SwingTargetShort, "F2"),
                    Inv(r.SwingStopLong, "F2"),
                    Inv(r.SwingStopShort, "F2"),
                    capReasonCsv,
                    Inv(r.BestPivotByVolume5m, "F2"),
                    Inv(r.BestPivotVolumeRatio5m, "F2"),
                    r.TrendStructure.ToString()))
            End Using
        Catch
            ' Silent fail — logging must never crash the main pipeline
        End Try
    End Sub

    ' Format a numeric field with InvariantCulture so a comma-decimal host locale
    ' can't split a value across CSV columns. Every parser in the repo reads with
    ' InvariantCulture; the v26+ cache writers already format invariantly. On a
    ' dot-decimal host (this machine) the output is byte-identical to the prior
    ' culture-sensitive ToString — the change only bites a comma-decimal locale.
    Private Shared Function Inv(value As Double, fmt As String) As String
        Return value.ToString(fmt, CultureInfo.InvariantCulture)
    End Function

    ' Normalise the engine's TargetCapReason display string to a canonical
    ' bucket value matching csv-expansion-v0.4-proposal.md §2a:
    '   "swing" / "hvn" / "poc" / "none"
    ' The engine populates TargetCapReason as a rich display string
    ' (e.g. "CAPPED @ 72480.0 (SWING_HIGH_5M)") suitable for the trader-facing
    ' header render. The CSV column is categorical, so we project to the bucket.
    Public Shared Function NormaliseCapReason(reason As String) As String
        If String.IsNullOrEmpty(reason) Then Return "none"
        Dim r = reason.ToUpperInvariant()
        If r.Contains("SWING")   Then Return "swing"
        If r.Contains("HVN")     Then Return "hvn"
        If r.Contains("POC")     Then Return "poc"
        Return "none"
    End Function

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
