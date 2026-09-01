' AnalysisLogger.vb  v0.8
' Appends one row per analysis run to a local CSV file.
' File location: same directory as the executable.
' Reset: truncates file back to header only.
'
' 2026-09-01 (absorption instrumentation, docs/absorption-instrumentation-spec.md):
'       5 columns APPENDED at 112-116 — AbsorptionEpisodeSec, AbsorptionPullLB,
'       AbsorptionPostLB, AbsorptionSizeStart, AbsorptionSizeMin. Four were already
'       live state on LevelAbsorptionTracker.SideState and were thrown away at the
'       read boundary; EpisodeSec is the one new measurement (R2).
'       ⚠ NOT a schema version bump and NOT a comparability boundary. The columns go
'       AFTER InstanceId/SignalId (spec R1) precisely so no existing column moves:
'       every column keeps its position AND its meaning, and rows written before and
'       after this change stay fully comparable. The five are simply empty on every
'       pre-change row — the same shape a no-episode row writes.
'       ⚠ EnsureLogFile still rotates on the header change, and the .bak name is the
'       hardcoded "v0.7" string below. That name was already wrong for the v0.8 book
'       and is left alone deliberately (renaming it is a behaviour change no ruling
'       covers, and the file it names is NEVER deleted). Recorded in the spec-back.
'
' v0.8 (#5 aggressor-velocity boundary, ONE rotation for the whole wave —
'       roadmap §5 item 3 manifest): 16 columns appended (96-111):
'       - 3 native #5:  AggrVelBurstRatio, AggrVelNet (net taker USD/sec, burst
'         horizon), AggrVelSignal (BURST_BUY/BURST_SELL/NORMAL). Numerics empty
'         on REST/fallback/cold-feed runs (§8 — null, never guessed).
'       - 2 retune C1:  TFIValue, TFISignal (closes the F11 audit blindness).
'       - 5 absorption (reserved per book-absorption-proposal.md D4/D8; POPULATED
'         by the #6 build — rotation-free, same header): AbsorptionSignal
'         (NONE default), AbsorptionLevel, AbsorptionRatio, AbsorptionAggrUsd,
'         AbsorptionPullFrac (numerics EMPTY unless a level episode is active on
'         the WS-live path; pullFrac logs even on D8-vetoed episodes).
'       - 4 placed-geometry (placed-geometry-structural-first D5; populated from
'         day one with the CURRENT effective levels): PlacedTargetLong,
'         PlacedStopLong, PlacedTargetShort, PlacedStopShort — sourced from
'         SignalEmitter.ComputeSideLevels, the SAME shared arbitration the bridge
'         payload's levels.<side>.stop/target use, so CSV ≡ payload by construction.
'       - 2 attribution: InstanceId, SignalId (Core/ProcessIdentity — the id is
'         ticked in RunAnalysisAsync BEFORE LogRun, so CSV SignalId ≡ payload
'         signal_id per run; SKIPPED runs burn an id with no CSV row — expected).
'       Same pass (retune C2): FundingRate format F6→F8 (same column, no header
'       change — the WS funding feed's finer deltas were rounding away).
'       Superseded v0.7 files rotate to analysis_log.csv.v0.7.bak (kept — the
'       v48 §4a fire-rate watch reads it; NEVER delete).
'       LogRun now takes cfg (the run's EngineSettings) for the placed-level
'       arbitration multipliers.
'
' v0.7 (v36 session-timeframe-resolution): Columns 94-95 appended: ExecResolution
'       (execution resolution in minutes this row was computed on — 1/3/5; legacy
'       rows predate the column and are treated as 1 by header-name readers) and
'       CVDWeightedSlope (CalcCVD's internal weighted slope, surfaced for CVD
'       threshold calibration). Appended at the end so header-name-based readers
'       (all readers since F9) tolerate them transparently. Superseded v0.6 files
'       rotate to analysis_log.csv.v0.6.bak.
'
' v0.6 (2026-06-11): Columns 89-93 added: MicroCVDEarly, MicroCVDMid,
'       MicroCVDLate (net USD deltas — negative values valid), MicroCVDMomentum,
'       MicroCVDSignal. MicroCVD was never logged before; added immediately
'       post-reset (file was ~5 rows) so the accel-threshold re-baseline can
'       sweep from CSV instead of the output dump.
'
' v0.5 (engine correctness pass, 2026-06): MTF gate columns replaced —
'       MTFGatePass → MTFGatePassLong + MTFGatePassShort (both sides logged
'       for gate-disagreement analytics); MTFGateReason now carries the final
'       Step 4b composed string. The header change is the schema marker for
'       the pass: rows logged under this header have post-fix semantics for
'       CVDSlope, MicroCVD*, VolumeRatio (recent-window norms), OBVTrend/
'       OBVDivergence (mean-volume units), DonchianSignal (prior-bar channel),
'       and the verdict/confidence distributions. Old-header files rotate to
'       .bak as usual.
'
' v0.4.1 (d1+d2): Column 87 added: TrendStructure5m.
'         BestPivotByVolume5m (col 85) and BestPivotVolumeRatio5m (col 86) now populated.
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
        "InstanceId,SignalId," &
        "AbsorptionEpisodeSec,AbsorptionPullLB,AbsorptionPostLB,AbsorptionSizeStart,AbsorptionSizeMin"

    Public Shared Function GetLogPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    Public Shared Function GetRowCount() As Integer
        Dim path As String = GetLogPath()
        If Not File.Exists(path) Then Return 0
        Return Math.Max(0, File.ReadAllLines(path).Length - 1)
    End Function

    ' Called once per LogRun. Handles log rotation: if the existing file has a
    ' different header (i.e. a superseded schema), it is renamed to .bak before
    ' a fresh current-schema file is created.
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
                ' Schema mismatch — rotate old file (named for the superseded schema).
                ' The .bak is NEVER deleted: the v48 §4a per-session fire-rate watch
                ' reads the rotated v0.7 book.
                Dim dir As String = System.IO.Path.GetDirectoryName(path)
                Dim bakPath As String = System.IO.Path.Combine(dir, "analysis_log.csv.v0.7.bak")
                If File.Exists(bakPath) Then
                    Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                    bakPath = System.IO.Path.Combine(dir, "analysis_log.csv.v0.7." & ts & ".bak")
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

    ' cfg (v0.8): the run's EngineSettings — feeds the shared placed-level arbitration
    ' (SignalEmitter.ComputeSideLevels) so the Placed* columns equal the bridge payload's
    ' levels for the same run by construction.
    Public Shared Sub LogRun(r As IndicatorResults, v As VerdictResult, cfg As EngineSettings)
        EnsureLogFile()
        Dim path As String = GetLogPath()
        Try
            ' v0.8 placed-geometry columns — the SAME per-side arbitration the bridge
            ' payload emits. Since v51 (B4b) that arbitration is structural-first
            ' (swing/HVN/POC target ladder + DG1 min(structural, 1.6×ATR) stop); the
            ' column VALUES changed semantics at that boundary, the names did not
            ' (v31/v36 precedent — see the v51 change_log entry).
            Dim placedLong = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
            Dim placedShort = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
            Using sw As New StreamWriter(path, append:=True)
                Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                Dim mtfReason As String = If(v.MTFGateReason, "").Replace(",", ";")
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
                    Inv(r.FundingRate, "F8"),
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
                    r.MTFGatePassLong.ToString(),
                    r.MTFGatePassShort.ToString(),
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
                    ProcessIdentity.InstanceId,
                    ProcessIdentity.CurrentSignalId.ToString(),
                    InvOpt(r.AbsorptionEpisodeSec, "F1"),
                    InvOpt(r.AbsorptionPullLB, "F0"),
                    InvOpt(r.AbsorptionPostLB, "F0"),
                    InvOpt(r.AbsorptionSizeStart, "F0"),
                    InvOpt(r.AbsorptionSizeMin, "F0")))
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

    ' v0.8: nullable numeric — Nothing writes an EMPTY field (data unavailable this
    ' run, e.g. aggressor velocity on a REST-fallback/cold-feed run), distinct from 0.
    Private Shared Function InvOpt(value As Double?, fmt As String) As String
        If Not value.HasValue Then Return ""
        Return Inv(value.Value, fmt)
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
