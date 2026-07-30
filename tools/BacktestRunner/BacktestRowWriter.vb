' tools/BacktestRunner/BacktestRowWriter.vb
' Local CSV writer for the backtest synthesizer. Header + row format are a byte-verbatim
' clone of AnalysisLogger (v0.8 schema); fixture A43e asserts equality via reflection.
'
' Why a local writer, not AnalysisLogger.LogRun: AnalysisLogger's path is hardcoded
' (BaseDirectory + "analysis_log.csv"), its Header is Private, and the class is not
' Partial. The task's HARD CONSTRAINT forbids modifying engine .vb files; the fallback
' the task allows is "a local writer — in that case a fixture MUST assert byte-level
' header equality." That is exactly what A43e does.
'
' The Placed* columns route through SignalEmitter.ComputeSideLevels (the SHARED
' arbitration seam — one function, no copies) so backtest CSV Placed* == payload
' Placed* by construction, identical to the live LogRun contract.

Imports System.Globalization
Imports System.IO

Public Class BacktestRowWriter

    ' Byte-verbatim clone of AnalysisLogger.Header (v0.8). A43e reads AnalysisLogger's
    ' private Header via reflection and asserts equality — any drift on either side
    ' fails the harness immediately.
    Public Shared ReadOnly Header As String =
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
        "MTFGatePassLong,MTFGatePassShort,MTF15mTrend,MTF15mADX,MTF15mEMAAlignment,MTFGateReason," &
        "ATR,ATRMultiplier," &
        "VerdictContext,FundingMomentum,OiCvdOutcome," &
        "SpreadBps,OFIMomentum,FundingDelta," &
        "VPFRVAH,VPFRVAL,VPFRNearestHvnAbove,VPFRNearestHvnBelow," &
        "LastSwingHigh5m,LastSwingLow5m,LastSwingHigh15m,LastSwingLow15m," &
        "SwingTargetLong,SwingTargetShort,SwingStopLong,SwingStopShort," &
        "TargetCapReason,BestPivotByVolume5m,BestPivotVolumeRatio5m," &
        "TrendStructure5m," &
        "MicroCVDEarly,MicroCVDMid,MicroCVDLate,MicroCVDMomentum,MicroCVDSignal," &
        "ExecResolution,CVDWeightedSlope," &
        "AggrVelBurstRatio,AggrVelNet,AggrVelSignal," &
        "TFIValue,TFISignal," &
        "AbsorptionSignal,AbsorptionLevel,AbsorptionRatio,AbsorptionAggrUsd,AbsorptionPullFrac," &
        "PlacedTargetLong,PlacedStopLong,PlacedTargetShort,PlacedStopShort," &
        "InstanceId,SignalId"

    Private ReadOnly _path As String
    Private ReadOnly _instanceId As String
    Private _signalId As Long = 0

    ''' <summary>Open (or truncate) the given output path and write the header. The
    ''' InstanceId is stamped once per run — spec §2 requires the "BACKTEST-" prefix
    ''' so a stray synthetic file cannot be confused for live rows.</summary>
    Public Sub New(outputPath As String, Optional instanceId As String = Nothing)
        _path = outputPath
        _instanceId = If(instanceId, "BACKTEST-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)))
        Using sw As New StreamWriter(_path, append:=False)
            sw.WriteLine(Header)
        End Using
    End Sub

    Public ReadOnly Property InstanceId As String
        Get
            Return _instanceId
        End Get
    End Property

    ''' <summary>Append one row for the given (r, verdict) pair at the given synthetic
    ''' timestamp (the bar-close in the replay grid, UTC). SignalId ticks monotonically
    ''' per row within this writer instance (== per replay run).</summary>
    Public Sub WriteRow(r As IndicatorResults, v As VerdictResult, cfg As EngineSettings, tsUtc As DateTime)
        _signalId += 1
        Dim placedLong  = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim placedShort = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)

        Using sw As New StreamWriter(_path, append:=True)
            Dim ts As String = tsUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            Dim mtfReason As String = If(v.MTFGateReason, "").Replace(",", ";")
            Dim capReasonCsv As String
            If v.Verdict IsNot Nothing AndAlso v.Verdict.Contains("LONG") Then
                capReasonCsv = AnalysisLogger.NormaliseCapReason(v.TargetCapReasonLong)
            ElseIf v.Verdict IsNot Nothing AndAlso v.Verdict.Contains("SHORT") Then
                capReasonCsv = AnalysisLogger.NormaliseCapReason(v.TargetCapReasonShort)
            ElseIf Not String.IsNullOrEmpty(v.TargetCapReasonLong) Then
                capReasonCsv = AnalysisLogger.NormaliseCapReason(v.TargetCapReasonLong)
            ElseIf Not String.IsNullOrEmpty(v.TargetCapReasonShort) Then
                capReasonCsv = AnalysisLogger.NormaliseCapReason(v.TargetCapReasonShort)
            Else
                capReasonCsv = "none"
            End If

            sw.WriteLine(String.Join(",",
                ts,
                Inv(r.CurrentPrice, "F2"),
                If(v.Verdict, ""),
                If(v.Confidence, ""),
                v.LongScore.ToString(),
                v.ShortScore.ToString(),
                v.EffectiveLongScore.ToString(),
                v.EffectiveShortScore.ToString(),
                v.MaxScore.ToString(),
                v.RegimePenalty.ToString(),
                If(r.Regime, ""),
                Inv(r.ADX, "F2"),
                Inv(r.PlusDI, "F2"),
                Inv(r.MinusDI, "F2"),
                Inv(r.ROC, "F4"),
                If(r.ROCSlope, ""),
                Inv(r.RSI, "F2"),
                If(r.RSIDivergence, ""),
                Inv(r.VolumeRatio, "F4"),
                Inv(r.VWAP, "F2"),
                Inv(r.VWAPDevPct, "F4"),
                r.VWAPSessionCandles.ToString(),
                Inv(r.VWAPSigma1Upper, "F2"),
                Inv(r.VWAPSigma1Lower, "F2"),
                Inv(r.VWAPSigma2Upper, "F2"),
                Inv(r.VWAPSigma2Lower, "F2"),
                Inv(r.BBW, "F4"),
                If(r.SqueezeStatus, ""),
                Inv(r.TTMHistogram, "F4"),
                If(r.TTMDirection, ""),
                If(r.TTMSignal, ""),
                Inv(r.EMA9, "F2"),
                Inv(r.EMA21, "F2"),
                Inv(r.EMA50, "F2"),
                If(r.EMAAlignment, ""),
                Inv(r.EMA200_5m, "F2"),
                If(r.PriceVsEMA200, ""),
                Inv(r.FundingRate, "F8"),
                If(r.FundingBias, ""),
                Inv(r.OI_Current, "F0"),
                Inv(r.OIChange15m, "F4"),
                Inv(r.OIChange60m, "F4"),
                If(r.OISignal, ""),
                Inv(r.OFIRatio, "F4"),
                Inv(r.OFIBidVol, "F2"),
                Inv(r.OFIAskVol, "F2"),
                If(r.OFISignal, ""),
                Inv(r.CVDValue, "F0"),
                If(r.CVDSlope, ""),
                If(r.CVDDivergence, ""),
                Inv(r.LiqLongSize, "F2"),
                Inv(r.LiqShortSize, "F2"),
                If(r.LiqSignal, ""),
                Inv(r.DonchianUpper, "F2"),
                Inv(r.DonchianLower, "F2"),
                If(r.DonchianSignal, ""),
                If(r.OBVTrend, ""),
                If(r.OBVDivergence, ""),
                r.MTFGatePassLong.ToString(),
                r.MTFGatePassShort.ToString(),
                If(r.MTF15mTrend, ""),
                Inv(r.MTF15mADX, "F2"),
                If(r.MTF15mEMAAlignment, ""),
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
                r.TrendStructure.ToString(),
                Inv(r.MicroCVDEarly, "F0"),
                Inv(r.MicroCVDMid, "F0"),
                Inv(r.MicroCVDLate, "F0"),
                If(r.MicroCVDMomentum, "FLAT"),
                If(r.MicroCVDSignal, "FLAT"),
                r.ExecResolution.ToString(),
                Inv(r.CVDWeightedSlope, "F0"),
                InvOpt(r.AggrVelBurstRatio, "F4"),
                InvOpt(r.AggrVelNet, "F0"),
                If(r.AggrVelSignal, "NORMAL"),
                Inv(r.TFIValue, "F4"),
                If(r.TFISignal, "NEUTRAL"),
                If(r.AbsorptionSignal, "NONE"),
                InvOpt(r.AbsorptionLevel, "F2"),
                InvOpt(r.AbsorptionRatio, "F2"),
                InvOpt(r.AbsorptionAggrUsd, "F0"),
                InvOpt(r.AbsorptionPullFrac, "F4"),
                Inv(placedLong.Target, "F2"),
                Inv(placedLong.StopPx, "F2"),
                Inv(placedShort.Target, "F2"),
                Inv(placedShort.StopPx, "F2"),
                _instanceId,
                _signalId.ToString()))
        End Using
    End Sub

    Private Shared Function Inv(value As Double, fmt As String) As String
        Return value.ToString(fmt, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function InvOpt(value As Double?, fmt As String) As String
        If Not value.HasValue Then Return ""
        Return Inv(value.Value, fmt)
    End Function

End Class
