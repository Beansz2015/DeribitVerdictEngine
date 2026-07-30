' tools/BacktestRunner/OverlapValidator.vb
' Overlap validation for the backtest synthesizer (docs/backtest-synthesizer-proposal.md §4).
'
' Joins a synthetic backtest CSV to one or two live CSVs per bar-close and reports:
'   - Per-column match rates on the reconstructable set (numeric within stated tolerance,
'     categorical exact).
'   - Verdict + tier agreement, overall and per session.
'   - Muted-vote delta: on disagreeing rows, LongScore/ShortScore delta distribution +
'     conditional agreement on rows where the live OFI/OI signals carried a non-neutral
'     value (the empirical read of how much the muted signals move verdicts).
'   - Funding momentum agreement (the coarse-history approximation, measured).
'
' Join rule: each row is keyed by (BucketMs) = floor(Timestamp, ExecResolution * 60000).
' Synthetic rows land exactly on the grid by construction (ReplayLoop iterates on the
' execRes minute mark); live rows are floored to the same grid using their ExecResolution
' column. Unjoined rows on either side are counted, not guessed. When both --live and
' --live2 are given, the local row wins on collision; the AWS row fills in gaps (spec §2
' provenance rule).
'
' Tolerances are DISCLOSED in the report header. Numeric buckets:
'   TightAbs = 1e-4 * |live|   (candle-derived: OHLC-driven prices, EMAs, VWAP, ATR, RSI,
'                              ADX, +/-DI, ROC, BBW, TTM histogram, EMA200_5m, Donchian,
'                              VolumeRatio, VWAPDevPct, VWAPSessionCandles).
'   LooseAbs = 0.5% (5e-3) + abs floor 1.0 * |live|
'                              (trade-window-edge-sensitive: CVD, CVDWeightedSlope,
'                              LiqLong/ShortSize, MicroCVD* net USD deltas, TFIValue,
'                              AggrVelBurstRatio, AggrVelNet, BestPivotVolumeRatio5m).
'   Exact / small-integer: VWAPSessionCandles (exact), swing pivots (F2 → rounded),
'                          MTFGatePass* booleans, verdict/context strings, regime,
'                          slope tags, signal labels, ExecResolution.
' Muted columns (OFI/OI/spread/absorption/AbsorptionSignal-family/OFIMomentum) are
' skipped — the D2 policy is that they are inert on synthetic rows by construction.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text

Public Class OverlapValidator

    ' ── Tolerance policy (single source of truth; the report prints these lines) ─────
    Public Const RelTolTight  As Double = 0.0001    ' 1e-4 rel — candle-derived
    Public Const RelTolLoose  As Double = 0.005     ' 0.5% rel — trade-window-edge
    Public Const AbsFloorTight As Double = 0.01     ' don't punish ≈0 with rel checks
    Public Const AbsFloorLoose As Double = 1.0

    ' ── Column classification ────────────────────────────────────────────────────────
    Public Enum ColKind
        NumTight        ' candle-derived numeric
        NumLoose        ' trade-window-edge numeric
        NumInt          ' integer (VWAPSessionCandles, ExecResolution)
        Categorical     ' exact string
        BoolTF          ' True/False text
        Muted           ' skipped — inert on synthetic by construction
        Meta            ' Timestamp/Price/InstanceId/SignalId — separate handling
    End Enum

    Public Class ColSpec
        Public Property Name As String
        Public Property Kind As ColKind
        Public Sub New(n As String, k As ColKind)
            Name = n : Kind = k
        End Sub
    End Class

    Public Shared ReadOnly Cols As New List(Of ColSpec) From {
        New ColSpec("Timestamp",                ColKind.Meta),
        New ColSpec("Price",                    ColKind.NumTight),
        New ColSpec("Verdict",                  ColKind.Categorical),
        New ColSpec("Confidence",               ColKind.Categorical),
        New ColSpec("LongScore",                ColKind.NumInt),
        New ColSpec("ShortScore",               ColKind.NumInt),
        New ColSpec("EffectiveLongScore",       ColKind.NumInt),
        New ColSpec("EffectiveShortScore",      ColKind.NumInt),
        New ColSpec("MaxScore",                 ColKind.NumInt),
        New ColSpec("RegimePenalty",            ColKind.NumInt),
        New ColSpec("Regime",                   ColKind.Categorical),
        New ColSpec("ADX",                      ColKind.NumTight),
        New ColSpec("PlusDI",                   ColKind.NumTight),
        New ColSpec("MinusDI",                  ColKind.NumTight),
        New ColSpec("ROC",                      ColKind.NumTight),
        New ColSpec("ROCSlope",                 ColKind.Categorical),
        New ColSpec("RSI",                      ColKind.NumTight),
        New ColSpec("RSIDivergence",            ColKind.Categorical),
        New ColSpec("VolumeRatio",              ColKind.NumTight),
        New ColSpec("VWAP",                     ColKind.NumTight),
        New ColSpec("VWAPDevPct",               ColKind.NumTight),
        New ColSpec("VWAPSessionCandles",       ColKind.NumInt),
        New ColSpec("VWAPSigma1Upper",          ColKind.NumTight),
        New ColSpec("VWAPSigma1Lower",          ColKind.NumTight),
        New ColSpec("VWAPSigma2Upper",          ColKind.NumTight),
        New ColSpec("VWAPSigma2Lower",          ColKind.NumTight),
        New ColSpec("BBW",                      ColKind.NumTight),
        New ColSpec("SqueezeStatus",            ColKind.Categorical),
        New ColSpec("TTMHistogram",             ColKind.NumTight),
        New ColSpec("TTMDirection",             ColKind.Categorical),
        New ColSpec("TTMSignal",                ColKind.Categorical),
        New ColSpec("EMA9",                     ColKind.NumTight),
        New ColSpec("EMA21",                    ColKind.NumTight),
        New ColSpec("EMA50",                    ColKind.NumTight),
        New ColSpec("EMAAlignment",             ColKind.Categorical),
        New ColSpec("EMA200_5m",                ColKind.NumTight),
        New ColSpec("PriceVsEMA200",            ColKind.Categorical),
        New ColSpec("FundingRate",              ColKind.NumLoose),
        New ColSpec("FundingBias",              ColKind.Categorical),
        New ColSpec("OI_Current",               ColKind.Muted),
        New ColSpec("OIChange15m",              ColKind.Muted),
        New ColSpec("OIChange60m",              ColKind.Muted),
        New ColSpec("OISignal",                 ColKind.Muted),
        New ColSpec("OFIRatio",                 ColKind.Muted),
        New ColSpec("OFIBidVol",                ColKind.Muted),
        New ColSpec("OFIAskVol",                ColKind.Muted),
        New ColSpec("OFISignal",                ColKind.Muted),
        New ColSpec("CVDValue",                 ColKind.NumLoose),
        New ColSpec("CVDSlope",                 ColKind.Categorical),
        New ColSpec("CVDDivergence",            ColKind.Categorical),
        New ColSpec("LiqLongSize",              ColKind.NumLoose),
        New ColSpec("LiqShortSize",             ColKind.NumLoose),
        New ColSpec("LiqSignal",                ColKind.Categorical),
        New ColSpec("DonchianUpper",            ColKind.NumTight),
        New ColSpec("DonchianLower",            ColKind.NumTight),
        New ColSpec("DonchianSignal",           ColKind.Categorical),
        New ColSpec("OBVTrend",                 ColKind.Categorical),
        New ColSpec("OBVDivergence",            ColKind.Categorical),
        New ColSpec("MTFGatePassLong",          ColKind.BoolTF),
        New ColSpec("MTFGatePassShort",         ColKind.BoolTF),
        New ColSpec("MTF15mTrend",              ColKind.Categorical),
        New ColSpec("MTF15mADX",                ColKind.NumTight),
        New ColSpec("MTF15mEMAAlignment",       ColKind.Categorical),
        New ColSpec("MTFGateReason",            ColKind.Meta),
        New ColSpec("ATR",                      ColKind.NumTight),
        New ColSpec("ATRMultiplier",            ColKind.NumTight),
        New ColSpec("VerdictContext",           ColKind.Categorical),
        New ColSpec("FundingMomentum",          ColKind.Categorical),
        New ColSpec("OiCvdOutcome",             ColKind.Categorical),
        New ColSpec("SpreadBps",                ColKind.Muted),
        New ColSpec("OFIMomentum",              ColKind.Muted),
        New ColSpec("FundingDelta",             ColKind.NumLoose),
        New ColSpec("VPFRVAH",                  ColKind.NumLoose),
        New ColSpec("VPFRVAL",                  ColKind.NumLoose),
        New ColSpec("VPFRNearestHvnAbove",      ColKind.NumLoose),
        New ColSpec("VPFRNearestHvnBelow",      ColKind.NumLoose),
        New ColSpec("LastSwingHigh5m",          ColKind.NumTight),
        New ColSpec("LastSwingLow5m",           ColKind.NumTight),
        New ColSpec("LastSwingHigh15m",         ColKind.NumTight),
        New ColSpec("LastSwingLow15m",          ColKind.NumTight),
        New ColSpec("SwingTargetLong",          ColKind.NumTight),
        New ColSpec("SwingTargetShort",         ColKind.NumTight),
        New ColSpec("SwingStopLong",            ColKind.NumTight),
        New ColSpec("SwingStopShort",           ColKind.NumTight),
        New ColSpec("TargetCapReason",          ColKind.Categorical),
        New ColSpec("BestPivotByVolume5m",      ColKind.NumTight),
        New ColSpec("BestPivotVolumeRatio5m",   ColKind.NumLoose),
        New ColSpec("TrendStructure5m",         ColKind.Categorical),
        New ColSpec("MicroCVDEarly",            ColKind.NumLoose),
        New ColSpec("MicroCVDMid",              ColKind.NumLoose),
        New ColSpec("MicroCVDLate",             ColKind.NumLoose),
        New ColSpec("MicroCVDMomentum",         ColKind.Categorical),
        New ColSpec("MicroCVDSignal",           ColKind.Categorical),
        New ColSpec("ExecResolution",           ColKind.NumInt),
        New ColSpec("CVDWeightedSlope",         ColKind.NumLoose),
        New ColSpec("AggrVelBurstRatio",        ColKind.NumLoose),
        New ColSpec("AggrVelNet",               ColKind.NumLoose),
        New ColSpec("AggrVelSignal",            ColKind.Categorical),
        New ColSpec("TFIValue",                 ColKind.NumLoose),
        New ColSpec("TFISignal",                ColKind.Categorical),
        New ColSpec("AbsorptionSignal",         ColKind.Muted),
        New ColSpec("AbsorptionLevel",          ColKind.Muted),
        New ColSpec("AbsorptionRatio",          ColKind.Muted),
        New ColSpec("AbsorptionAggrUsd",        ColKind.Muted),
        New ColSpec("AbsorptionPullFrac",       ColKind.Muted),
        New ColSpec("PlacedTargetLong",         ColKind.NumTight),
        New ColSpec("PlacedStopLong",           ColKind.NumTight),
        New ColSpec("PlacedTargetShort",        ColKind.NumTight),
        New ColSpec("PlacedStopShort",          ColKind.NumTight),
        New ColSpec("InstanceId",               ColKind.Meta),
        New ColSpec("SignalId",                 ColKind.Meta)
    }

    ' ── Row model ────────────────────────────────────────────────────────────────────
    Public Class Row
        Public Property TsMs As Long
        Public Property BucketMs As Long
        Public Property ExecRes As Integer
        Public Property Session As String    ' derived from utcHour + cfg buckets
        Public Property Cells As String()    ' by column index in Cols
        Public Function G(name As String) As String
            Dim idx = ColIndex(name)
            If idx < 0 OrElse idx >= Cells.Length Then Return ""
            Return Cells(idx)
        End Function
    End Class

    Private Shared _colIndex As Dictionary(Of String, Integer) = Nothing
    Public Shared Function ColIndex(name As String) As Integer
        If _colIndex Is Nothing Then
            Dim d As New Dictionary(Of String, Integer)()
            For i As Integer = 0 To Cols.Count - 1
                d(Cols(i).Name) = i
            Next
            _colIndex = d
        End If
        Dim v As Integer
        If _colIndex.TryGetValue(name, v) Then Return v
        Return -1
    End Function

    ''' <summary>Floor tsMs to the ExecResolution grid (execMin minutes).</summary>
    Public Shared Function FloorToBucket(tsMs As Long, execMin As Integer) As Long
        If execMin <= 0 Then execMin = 1
        Dim bucketMs As Long = CLng(execMin) * 60L * 1000L
        Return (tsMs \ bucketMs) * bucketMs
    End Function

    ''' <summary>Parse "yyyy-MM-dd HH:mm:ss" (invariant) → UTC ms.</summary>
    Public Shared Function ParseTsMs(s As String) As Long
        Dim d As DateTime
        If DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                                  DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, d) Then
            Return New DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeMilliseconds()
        End If
        Return -1
    End Function

    Public Class RowReadResult
        Public Property Rows As New List(Of Row)()
        Public Property SkippedOutOfWindow As Integer = 0
        Public Property SkippedBadTs As Integer = 0
        Public Property SchemaMismatch As Boolean = False
    End Class

    ''' <summary>Read a CSV; skip rows whose TsMs is outside [fromMs, toMs). Fills
    ''' BucketMs from ExecResolution and Session from utcHour using cfg.</summary>
    Public Shared Function LoadCsv(path As String, cfg As EngineSettings,
                                    fromMs As Long, toMs As Long) As RowReadResult
        Dim r As New RowReadResult()
        If Not File.Exists(path) Then
            Console.Error.WriteLine("[Validate] CSV not found: " & path)
            Return r
        End If
        Using sr As New StreamReader(path)
            Dim header = sr.ReadLine()
            If header Is Nothing Then Return r
            Dim expectedHeader As String = String.Join(",", Cols.Select(Function(c) c.Name))
            If header <> expectedHeader Then
                r.SchemaMismatch = True
                Console.Error.WriteLine("[Validate] Header schema mismatch in " & path)
            End If
            Dim expectedCount As Integer = Cols.Count
            Dim tsIdx  As Integer = ColIndex("Timestamp")
            Dim exeIdx As Integer = ColIndex("ExecResolution")
            Dim line As String
            Do
                line = sr.ReadLine()
                If line Is Nothing Then Exit Do
                Dim cells = SplitCsvKeepingLength(line, expectedCount)
                If cells Is Nothing Then Continue Do
                Dim ts As Long = ParseTsMs(cells(tsIdx))
                If ts < 0 Then
                    r.SkippedBadTs += 1
                    Continue Do
                End If
                If ts < fromMs OrElse ts >= toMs Then
                    r.SkippedOutOfWindow += 1
                    Continue Do
                End If
                Dim ex As Integer
                If Not Integer.TryParse(cells(exeIdx), NumberStyles.Integer,
                                        CultureInfo.InvariantCulture, ex) Then
                    ex = 1
                End If
                Dim rec As New Row With {
                    .TsMs = ts, .BucketMs = FloorToBucket(ts, ex), .ExecRes = ex, .Cells = cells}
                rec.Session = ReplayLoop.ResolveSessionLabel(cfg,
                                DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime.Hour)
                r.Rows.Add(rec)
            Loop
        End Using
        Return r
    End Function

    ' A safe splitter that respects the ',' inside MTFGateReason (we already replace with ';'
    ' in both writers, so plain split works). If the row length disagrees with the schema
    ' return Nothing.
    Private Shared Function SplitCsvKeepingLength(line As String, expected As Integer) As String()
        Dim parts = line.Split(","c)
        If parts.Length <> expected Then Return Nothing
        Return parts
    End Function

    ' ── Comparison primitives ────────────────────────────────────────────────────────
    Public Shared Function TryNum(s As String, ByRef v As Double) As Boolean
        If String.IsNullOrEmpty(s) Then Return False
        Return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v)
    End Function

    Public Shared Function CompareNum(liveS As String, synS As String, kind As ColKind,
                                        ByRef absDiff As Double) As Integer
        ' Returns 1 = match, 0 = mismatch, -1 = both empty (skip)
        absDiff = 0
        Dim lv, sv As Double
        Dim lOk = TryNum(liveS, lv)
        Dim sOk = TryNum(synS, sv)
        If Not lOk AndAlso Not sOk Then Return -1
        If lOk <> sOk Then Return 0
        Dim d = Math.Abs(lv - sv)
        absDiff = d
        Dim rel As Double, floorAbs As Double
        Select Case kind
            Case ColKind.NumTight
                rel = RelTolTight : floorAbs = AbsFloorTight
            Case ColKind.NumLoose
                rel = RelTolLoose : floorAbs = AbsFloorLoose
            Case ColKind.NumInt
                Return If(d < 0.5, 1, 0)
            Case Else
                rel = RelTolLoose : floorAbs = AbsFloorLoose
        End Select
        Dim tol = Math.Max(floorAbs, Math.Abs(lv) * rel)
        Return If(d <= tol, 1, 0)
    End Function

    ' ── Stats ────────────────────────────────────────────────────────────────────────
    Public Class ColStat
        Public Property Name As String
        Public Property Kind As ColKind
        Public Property NCompared As Integer = 0
        Public Property NMatch As Integer = 0
        Public Property NBothEmpty As Integer = 0
        Public Property SumAbsDiff As Double = 0
        Public Property MaxAbsDiff As Double = 0
        Public Property WorstSampleLive As String = ""
        Public Property WorstSampleSyn As String = ""
        Public Property WorstSampleTs As String = ""
        Public ReadOnly Property MatchRate As Double
            Get
                Return If(NCompared > 0, CDbl(NMatch) / CDbl(NCompared), 0.0)
            End Get
        End Property
        Public ReadOnly Property MeanAbsDiff As Double
            Get
                Return If(NCompared > 0, SumAbsDiff / CDbl(NCompared), 0.0)
            End Get
        End Property
    End Class

    Public Class Report
        Public Property FromUtc As DateTime
        Public Property ToUtc As DateTime
        Public Property SyntheticPath As String
        Public Property LivePathPrimary As String
        Public Property LivePathSecondary As String
        Public Property CfgVersion As Integer
        Public Property SyntheticRows As Integer
        Public Property LivePrimaryRows As Integer
        Public Property LiveSecondaryRows As Integer
        Public Property LiveJoinedFromPrimary As Integer
        Public Property LiveJoinedFromSecondary As Integer
        Public Property JoinedPairs As Integer
        Public Property SyntheticUnjoined As Integer
        Public Property LiveUnjoined As Integer

        ' Verdict / tier
        Public Property VerdictMatchOverall As Integer = 0
        Public Property VerdictComparedOverall As Integer = 0
        Public Property TierMatchOverall As Integer = 0
        Public Property TierComparedOverall As Integer = 0
        Public Property VerdictMatchBySession As New Dictionary(Of String, (M As Integer, N As Integer))()
        Public Property TierMatchBySession As New Dictionary(Of String, (M As Integer, N As Integer))()

        ' Column stats
        Public Property ColStats As New List(Of ColStat)()

        ' Muted-vote delta
        Public Property DisagreeCount As Integer = 0
        Public Property DisagreeCondOfiOiNonNeutral As Integer = 0
        Public Property AgreeCondOfiOiNonNeutral As Integer = 0
        Public Property AgreeCondOfiOiNeutral As Integer = 0
        Public Property DisagreeCondOfiOiNeutral As Integer = 0
        Public Property SumLsDelta As Double = 0
        Public Property SumSsDelta As Double = 0
        Public Property SumAbsLsDelta As Double = 0
        Public Property SumAbsSsDelta As Double = 0
        Public Property MaxAbsLsDelta As Double = 0
        Public Property MaxAbsSsDelta As Double = 0

        ' Funding momentum
        Public Property FundingMomentumMatch As Integer = 0
        Public Property FundingMomentumCompared As Integer = 0
        Public Property FundingMomentumConfusion As New Dictionary(Of String, Integer)()
    End Class

    Public Shared Function ReduceTier(v As String) As String
        If String.IsNullOrEmpty(v) Then Return ""
        If v.StartsWith("STRONG") Then Return "STRONG"
        If v.StartsWith("WEAK") Then Return "WEAK"
        If v = "LONG" OrElse v = "SHORT" Then Return "MID"
        Return "NONE"
    End Function

    ''' <summary>Run the join + compare. Returns the populated Report.</summary>
    Public Shared Function Validate(cfg As EngineSettings,
                                     syntheticPath As String, livePath1 As String, livePath2 As String,
                                     fromUtc As DateTime, toUtc As DateTime) As Report
        Dim fromMs = New DateTimeOffset(fromUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim toMs   = New DateTimeOffset(toUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim rep As New Report With {
            .FromUtc = fromUtc, .ToUtc = toUtc,
            .SyntheticPath = syntheticPath,
            .LivePathPrimary = livePath1, .LivePathSecondary = If(livePath2, ""),
            .CfgVersion = cfg.Version}

        Dim synR = LoadCsv(syntheticPath, cfg, fromMs, toMs)
        Dim l1R  = LoadCsv(livePath1,     cfg, fromMs, toMs)
        Dim l2R  = If(String.IsNullOrEmpty(livePath2), New RowReadResult(),
                                                       LoadCsv(livePath2, cfg, fromMs, toMs))
        rep.SyntheticRows      = synR.Rows.Count
        rep.LivePrimaryRows    = l1R.Rows.Count
        rep.LiveSecondaryRows  = l2R.Rows.Count

        ' Bucket the two live sources (many rows per bucket; keep the LATEST-Ts row).
        Dim live1By As Dictionary(Of Long, Row) = ByBucketLatest(l1R.Rows)
        Dim live2By As Dictionary(Of Long, Row) = ByBucketLatest(l2R.Rows)
        Dim liveJoined As New HashSet(Of Long)()

        For Each s In synR.Rows
            Dim l As Row = Nothing
            Dim fromSecondary As Boolean = False
            If live1By.TryGetValue(s.BucketMs, l) Then
                rep.LiveJoinedFromPrimary += 1
            ElseIf live2By.TryGetValue(s.BucketMs, l) Then
                rep.LiveJoinedFromSecondary += 1
                fromSecondary = True
            End If
            If l Is Nothing Then
                rep.SyntheticUnjoined += 1
                Continue For
            End If
            rep.JoinedPairs += 1
            liveJoined.Add(s.BucketMs)
            ComparePair(rep, s, l)
        Next

        ' Live rows in-window that no synthetic bucket covered.
        Dim allLiveBuckets As New HashSet(Of Long)(live1By.Keys)
        For Each k In live2By.Keys : allLiveBuckets.Add(k) : Next
        For Each k In allLiveBuckets
            If Not liveJoined.Contains(k) Then rep.LiveUnjoined += 1
        Next

        Return rep
    End Function

    Private Shared Function ByBucketLatest(rows As List(Of Row)) As Dictionary(Of Long, Row)
        Dim d As New Dictionary(Of Long, Row)()
        For Each r In rows
            Dim existing As Row = Nothing
            If Not d.TryGetValue(r.BucketMs, existing) OrElse r.TsMs > existing.TsMs Then
                d(r.BucketMs) = r
            End If
        Next
        Return d
    End Function

    Private Shared Sub ComparePair(rep As Report, syn As Row, live As Row)
        ' Verdict / tier — session-tagged from the LIVE row (canonical source).
        Dim sVerdict = syn.G("Verdict")
        Dim lVerdict = live.G("Verdict")
        rep.VerdictComparedOverall += 1
        Dim vMatch As Boolean = (sVerdict = lVerdict)
        If vMatch Then rep.VerdictMatchOverall += 1
        Dim vSess = live.Session
        Dim vs As (M As Integer, N As Integer) = (0, 0)
        rep.VerdictMatchBySession.TryGetValue(vSess, vs)
        vs = (vs.M + If(vMatch, 1, 0), vs.N + 1)
        rep.VerdictMatchBySession(vSess) = vs

        Dim sTier = ReduceTier(sVerdict), lTier = ReduceTier(lVerdict)
        rep.TierComparedOverall += 1
        Dim tMatch As Boolean = (sTier = lTier)
        If tMatch Then rep.TierMatchOverall += 1
        Dim ts As (M As Integer, N As Integer) = (0, 0)
        rep.TierMatchBySession.TryGetValue(vSess, ts)
        ts = (ts.M + If(tMatch, 1, 0), ts.N + 1)
        rep.TierMatchBySession(vSess) = ts

        ' Muted-vote delta bookkeeping. On disagreement, sum score deltas.
        Dim liveLs, liveSs, synLs, synSs As Double
        TryNum(live.G("LongScore"),  liveLs)
        TryNum(live.G("ShortScore"), liveSs)
        TryNum(syn.G("LongScore"),   synLs)
        TryNum(syn.G("ShortScore"),  synSs)
        Dim liveOfi = live.G("OFISignal")
        Dim liveOi  = live.G("OISignal")
        Dim nonNeutralMuted As Boolean =
            (liveOfi = "BUY DOMINANT" OrElse liveOfi = "SELL DOMINANT") OrElse
            (liveOi = "LONG_PARTIAL" OrElse liveOi = "SHORT_PARTIAL" OrElse
             liveOi = "LONG_FULL" OrElse liveOi = "SHORT_FULL")

        If Not vMatch Then
            rep.DisagreeCount += 1
            Dim dLs = liveLs - synLs
            Dim dSs = liveSs - synSs
            rep.SumLsDelta += dLs : rep.SumSsDelta += dSs
            rep.SumAbsLsDelta += Math.Abs(dLs) : rep.SumAbsSsDelta += Math.Abs(dSs)
            If Math.Abs(dLs) > rep.MaxAbsLsDelta Then rep.MaxAbsLsDelta = Math.Abs(dLs)
            If Math.Abs(dSs) > rep.MaxAbsSsDelta Then rep.MaxAbsSsDelta = Math.Abs(dSs)
            If nonNeutralMuted Then rep.DisagreeCondOfiOiNonNeutral += 1 Else rep.DisagreeCondOfiOiNeutral += 1
        Else
            If nonNeutralMuted Then rep.AgreeCondOfiOiNonNeutral += 1 Else rep.AgreeCondOfiOiNeutral += 1
        End If

        ' Funding-momentum confusion.
        Dim lFm = live.G("FundingMomentum"), sFm = syn.G("FundingMomentum")
        rep.FundingMomentumCompared += 1
        If lFm = sFm Then rep.FundingMomentumMatch += 1
        Dim key = lFm & "→" & sFm
        Dim cur As Integer = 0
        rep.FundingMomentumConfusion.TryGetValue(key, cur)
        rep.FundingMomentumConfusion(key) = cur + 1

        ' Per-column comparison — populate/update ColStats.
        For i As Integer = 0 To Cols.Count - 1
            Dim c = Cols(i)
            If c.Kind = ColKind.Meta OrElse c.Kind = ColKind.Muted Then Continue For
            Dim cs = GetOrAddStat(rep, c)
            Dim lVal = live.Cells(i)
            Dim sVal = syn.Cells(i)
            Select Case c.Kind
                Case ColKind.NumTight, ColKind.NumLoose, ColKind.NumInt
                    Dim ad As Double = 0
                    Dim res = CompareNum(lVal, sVal, c.Kind, ad)
                    If res = -1 Then
                        cs.NBothEmpty += 1
                    Else
                        cs.NCompared += 1
                        cs.SumAbsDiff += ad
                        If ad > cs.MaxAbsDiff Then
                            cs.MaxAbsDiff = ad
                            cs.WorstSampleLive = lVal
                            cs.WorstSampleSyn = sVal
                            cs.WorstSampleTs = syn.G("Timestamp")
                        End If
                        If res = 1 Then cs.NMatch += 1
                    End If
                Case ColKind.Categorical
                    cs.NCompared += 1
                    If lVal = sVal Then cs.NMatch += 1
                Case ColKind.BoolTF
                    cs.NCompared += 1
                    If lVal = sVal Then cs.NMatch += 1
            End Select
        Next
    End Sub

    Private Shared Function GetOrAddStat(rep As Report, c As ColSpec) As ColStat
        For Each s In rep.ColStats
            If s.Name = c.Name Then Return s
        Next
        Dim ns As New ColStat With {.Name = c.Name, .Kind = c.Kind}
        rep.ColStats.Add(ns)
        Return ns
    End Function

    ' ── Report emission ─────────────────────────────────────────────────────────────
    Public Shared Function BuildMarkdown(rep As Report, cfg As EngineSettings) As String
        Dim sb As New StringBuilder()
        Dim inv = CultureInfo.InvariantCulture

        sb.AppendLine("# Backtest overlap validation — " & rep.FromUtc.ToString("yyyy-MM-dd") &
                      " → " & rep.ToUtc.ToString("yyyy-MM-dd"))
        sb.AppendLine()
        sb.AppendLine("**Generated:** " & DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"))
        sb.AppendLine("**Synthetic CSV:** `" & rep.SyntheticPath & "`  ")
        sb.AppendLine("**Live primary  :** `" & rep.LivePathPrimary & "`  ")
        If Not String.IsNullOrEmpty(rep.LivePathSecondary) Then
            sb.AppendLine("**Live secondary:** `" & rep.LivePathSecondary & "`  ")
        End If
        sb.AppendLine("**Settings version:** v" & cfg.Version)
        sb.AppendLine()
        sb.AppendLine("## 0. Caveats (auto-printed)")
        sb.AppendLine()
        sb.AppendLine("- **Muted signals** (D2 policy — inert on synthetic by construction): " &
                      "`OFIRatio/OFIBidVol/OFIAskVol/OFISignal/OFIMomentum/SpreadBps/" &
                      "OI_Current/OIChange15m/OIChange60m/OISignal/AbsorptionSignal/" &
                      "AbsorptionLevel/AbsorptionRatio/AbsorptionAggrUsd/AbsorptionPullFrac`. " &
                      "These columns are SKIPPED in the per-column table (comparing them would " &
                      "just measure how often the live column happened to be neutral).")
        sb.AppendLine("- **Funding is approximated** (proposal §1) — the historical " &
                      "`get_funding_rate_history` returns 8-hour anchors; live samples every run. " &
                      "Section 5 quantifies the resulting FundingMomentum drift.")
        sb.AppendLine("- **Coordinator ruling** (spec-back §closing note): validation window is " &
                      "**2026-07-23 → 2026-07-30** because live rows before ~07-23 carry " &
                      "materially different settings (v48→v60 drift); v61/v62/v63 are byte-" &
                      "identical at defaults, so post-07-23 live is effectively current-cfg.")
        sb.AppendLine()
        sb.AppendLine("## 1. Tolerances")
        sb.AppendLine()
        sb.AppendLine(String.Format(inv,
            "- **NumTight** (candle-derived): tol = max({0}, {1:P4} × |live|). Applied to: " &
            "Price, ADX/PlusDI/MinusDI, ROC, RSI, VWAP/DevPct/sigmas, BBW, TTMHistogram, " &
            "EMA9/21/50/200_5m, MTF15mADX, ATR, ATRMultiplier, VolumeRatio, DonchianUpper/Lower, " &
            "LastSwing*/SwingTarget*/SwingStop*, BestPivotByVolume5m, PlacedTarget*/Stop*.",
            AbsFloorTight, RelTolTight))
        sb.AppendLine(String.Format(inv,
            "- **NumLoose** (trade-window-edge / net-USD): tol = max({0}, {1:P4} × |live|). " &
            "Applied to: CVDValue, CVDWeightedSlope, LiqLong/ShortSize, MicroCVDEarly/Mid/Late, " &
            "AggrVelBurstRatio, AggrVelNet, TFIValue, VPFRVAH/VAL/HvnAbove/HvnBelow, " &
            "FundingRate, FundingDelta, BestPivotVolumeRatio5m.",
            AbsFloorLoose, RelTolLoose))
        sb.AppendLine("- **NumInt** (exact-integer): VWAPSessionCandles, ExecResolution, and " &
                      "the LongScore/ShortScore/MaxScore/RegimePenalty score fields.")
        sb.AppendLine("- **Categorical / Bool** — exact string equality.")
        sb.AppendLine()

        sb.AppendLine("## 2. Join summary")
        sb.AppendLine()
        sb.AppendLine("- Synthetic rows in window: " & rep.SyntheticRows)
        sb.AppendLine("- Live primary   rows in window: " & rep.LivePrimaryRows)
        If Not String.IsNullOrEmpty(rep.LivePathSecondary) Then
            sb.AppendLine("- Live secondary rows in window: " & rep.LiveSecondaryRows)
        End If
        sb.AppendLine("- Joined pairs: " & rep.JoinedPairs)
        sb.AppendLine("  - from primary: " & rep.LiveJoinedFromPrimary)
        sb.AppendLine("  - from secondary (gap fill): " & rep.LiveJoinedFromSecondary)
        sb.AppendLine("- Unjoined synthetic (no matching live bucket): " & rep.SyntheticUnjoined)
        sb.AppendLine("- Unjoined live (bucket not covered by any synthetic row): " & rep.LiveUnjoined)
        sb.AppendLine()

        sb.AppendLine("## 3. Verdict + tier agreement")
        sb.AppendLine()
        sb.AppendLine(String.Format(inv, "**Overall verdict agreement: {0}/{1} = {2:P2}**",
                                    rep.VerdictMatchOverall, rep.VerdictComparedOverall,
                                    If(rep.VerdictComparedOverall > 0,
                                       CDbl(rep.VerdictMatchOverall) / rep.VerdictComparedOverall, 0.0)))
        sb.AppendLine(String.Format(inv, "**Overall tier agreement (STRONG/WEAK/MID/NONE): {0}/{1} = {2:P2}**",
                                    rep.TierMatchOverall, rep.TierComparedOverall,
                                    If(rep.TierComparedOverall > 0,
                                       CDbl(rep.TierMatchOverall) / rep.TierComparedOverall, 0.0)))
        sb.AppendLine()
        sb.AppendLine("| Session | Verdict agreement | Tier agreement |")
        sb.AppendLine("|---|---|---|")
        Dim sessions = rep.VerdictMatchBySession.Keys.OrderBy(Function(x) x).ToList()
        For Each s In sessions
            Dim v = rep.VerdictMatchBySession(s)
            Dim t As (M As Integer, N As Integer) = (0, 0)
            rep.TierMatchBySession.TryGetValue(s, t)
            sb.AppendLine(String.Format(inv, "| {0} | {1}/{2} = {3:P2} | {4}/{5} = {6:P2} |",
                                        s, v.M, v.N,
                                        If(v.N > 0, CDbl(v.M) / v.N, 0.0),
                                        t.M, t.N,
                                        If(t.N > 0, CDbl(t.M) / t.N, 0.0)))
        Next
        sb.AppendLine()

        sb.AppendLine("## 4. Per-column match rates (reconstructable set)")
        sb.AppendLine()
        sb.AppendLine("Ordered by match rate ascending. Numeric columns show mean/max absolute diff on the compared set.")
        sb.AppendLine()
        sb.AppendLine("| Column | Kind | N | Match rate | Mean |Δ| | Max |Δ| |")
        sb.AppendLine("|---|---|---:|---:|---:|---:|")
        Dim ordered = rep.ColStats.OrderBy(Function(c) c.MatchRate).ThenBy(Function(c) c.Name).ToList()
        For Each cs In ordered
            Dim numeric As Boolean = cs.Kind = ColKind.NumTight OrElse
                                     cs.Kind = ColKind.NumLoose OrElse
                                     cs.Kind = ColKind.NumInt
            sb.AppendLine(String.Format(inv, "| {0} | {1} | {2} | {3:P2} | {4} | {5} |",
                cs.Name, cs.Kind.ToString(),
                cs.NCompared,
                cs.MatchRate,
                If(numeric AndAlso cs.NCompared > 0, cs.MeanAbsDiff.ToString("G4", inv), "—"),
                If(numeric AndAlso cs.NCompared > 0, cs.MaxAbsDiff.ToString("G4", inv), "—")))
        Next
        sb.AppendLine()

        sb.AppendLine("## 5. Five worst columns — detail")
        sb.AppendLine()
        Dim worst = ordered.Take(5).ToList()
        sb.AppendLine("| Column | Match rate | Worst live sample | Worst syn sample | Worst @ |")
        sb.AppendLine("|---|---:|---|---|---|")
        For Each cs In worst
            sb.AppendLine(String.Format(inv, "| {0} | {1:P2} | {2} | {3} | {4} |",
                cs.Name, cs.MatchRate,
                If(String.IsNullOrEmpty(cs.WorstSampleLive), "—", cs.WorstSampleLive),
                If(String.IsNullOrEmpty(cs.WorstSampleSyn),  "—", cs.WorstSampleSyn),
                If(String.IsNullOrEmpty(cs.WorstSampleTs),   "—", cs.WorstSampleTs)))
        Next
        sb.AppendLine()

        sb.AppendLine("## 6. Muted-vote delta (the empirical read of §1's muted set)")
        sb.AppendLine()
        Dim vAgree As Integer = rep.VerdictMatchOverall
        Dim vDisagree As Integer = rep.DisagreeCount
        sb.AppendLine("**Verdict agreement conditioned on live OFI/OI non-neutrality:**")
        sb.AppendLine()
        sb.AppendLine("| Live OFI/OI state | Agree | Disagree | Agree rate |")
        sb.AppendLine("|---|---:|---:|---:|")
        Dim nonNeutralN = rep.AgreeCondOfiOiNonNeutral + rep.DisagreeCondOfiOiNonNeutral
        Dim neutralN    = rep.AgreeCondOfiOiNeutral + rep.DisagreeCondOfiOiNeutral
        sb.AppendLine(String.Format(inv, "| non-neutral (OFI ∈ {{BUY,SELL DOMINANT}} OR OI ∈ {{*_PARTIAL,*_FULL}}) | {0} | {1} | {2:P2} |",
            rep.AgreeCondOfiOiNonNeutral, rep.DisagreeCondOfiOiNonNeutral,
            If(nonNeutralN > 0, CDbl(rep.AgreeCondOfiOiNonNeutral) / nonNeutralN, 0.0)))
        sb.AppendLine(String.Format(inv, "| neutral (OFI=BALANCED AND OI=NEUTRAL)                                   | {0} | {1} | {2:P2} |",
            rep.AgreeCondOfiOiNeutral, rep.DisagreeCondOfiOiNeutral,
            If(neutralN > 0, CDbl(rep.AgreeCondOfiOiNeutral) / neutralN, 0.0)))
        sb.AppendLine()
        sb.AppendLine("**Score-delta distribution on disagreeing rows** (live − synthetic):")
        sb.AppendLine()
        If vDisagree > 0 Then
            sb.AppendLine(String.Format(inv,
                "- LongScore  delta: mean = {0:F3}, mean |Δ| = {1:F3}, max |Δ| = {2:F0}",
                rep.SumLsDelta / vDisagree, rep.SumAbsLsDelta / vDisagree, rep.MaxAbsLsDelta))
            sb.AppendLine(String.Format(inv,
                "- ShortScore delta: mean = {0:F3}, mean |Δ| = {1:F3}, max |Δ| = {2:F0}",
                rep.SumSsDelta / vDisagree, rep.SumAbsSsDelta / vDisagree, rep.MaxAbsSsDelta))
        Else
            sb.AppendLine("- (no disagreements)")
        End If
        sb.AppendLine()

        sb.AppendLine("## 7. FundingMomentum agreement (D2 approximation, measured)")
        sb.AppendLine()
        sb.AppendLine(String.Format(inv, "Match: {0}/{1} = {2:P2}",
            rep.FundingMomentumMatch, rep.FundingMomentumCompared,
            If(rep.FundingMomentumCompared > 0,
               CDbl(rep.FundingMomentumMatch) / rep.FundingMomentumCompared, 0.0)))
        sb.AppendLine()
        sb.AppendLine("Confusion (`live→synthetic`):")
        sb.AppendLine()
        sb.AppendLine("| Transition | Count |")
        sb.AppendLine("|---|---:|")
        For Each kv In rep.FundingMomentumConfusion.OrderByDescending(Function(k) k.Value)
            sb.AppendLine(String.Format("| {0} | {1} |", kv.Key, kv.Value))
        Next
        sb.AppendLine()

        Return sb.ToString()
    End Function

End Class
