' tools/CeilingAudit/CsvFeatureBuilder.vb
' CSV → (CsvRow, FeatureBundle) rows for the W6-4 ceiling-audit instrument.
' Reads a pooled analysis_log.csv (local + AWS-collector rows concatenated externally),
' filters to v0.8 evaluable directional weekday rows, computes InstanceId burst-cadence
' exclusions per the spec, partitions into NY×1 / LONDON×3 / ASIA×3 populations.
'
' §2 amended (2026-07-23 tick): SCORED signal states + named numerics + regime +
' session-hour go into the decision-model feature matrix; logged-but-unscored signals
' (Absorption* on every population; AggrVel on populations whose session bucket has NO
' explicit burst_ratio_threshold override — un-armed) go into a SEPARATE informational
' bundle that never enters §4. The armed-vs-un-armed decision is per POPULATION, resolved
' once via ExecutionResolution.HasExplicitAggrVelBurstThreshold against a representative
' UTC hour of the bucket — so today NY×1 → armed (AggrVel scored), LONDON/ASIA → un-armed
' (AggrVel informational). Re-derived on every run, so a future §5.2 pass that adds a
' LONDON override auto-arms that population without a code change.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq

Namespace CeilingAudit

    ''' <summary>The categorical + numeric feature record built for ONE CsvRow. Split into
    ''' scored (goes into the decision-model design matrix X) and informational (reported
    ''' as a separate coefficient table, provably absent from X — fixture A39e).</summary>
    Public Class FeatureBundle
        ' Aligned index into the population's row list — every downstream array uses this.
        Public Property RowIndex As Integer

        ' Label = placed-vs-placed SUCCESS at the population's tracker horizon.
        ' Populated by LabelBuilder after ForwardBars are attached; -1 = no data / excluded.
        Public Property Label As Integer = -1

        ' Baseline ranking feature = dominant-effective / MaxScore. Populated at build time
        ' from the logged scores (no re-derivation of dominance under a regime veto here;
        ' the audit reads the pipeline's own harvest as it emitted it).
        Public Property BaselineScore As Double

        ' Scored inputs — SESSION-HOUR (int, 0..23) surfaces as a categorical one-hot at
        ' matrix-build time so a hand-tuned integer never leaks a numeric ordering into the
        ' fit. Regime likewise.
        Public Property SessionHour As Integer
        Public Property Regime As String

        ' Categorical signal states (all bare strings; empty → treated as its own bucket).
        ' Cross-referenced against §2 amended: SCORED indicators only. VerdictContext / Kelly /
        ' exit-guard / TAPE metrics are display-only and EXCLUDED.
        Public Property ScoredCategoricals As New Dictionary(Of String, String)()

        ' Named scored numerics (§2 list). Missing → Double.NaN and the column-level
        ' train-median impute + a paired missing-indicator column carry the state.
        Public Property ScoredNumerics As New Dictionary(Of String, Double)()

        ' AggrVel scored membership (armed populations only) — the bundle carries it, the
        ' matrix builder consults IsAggrVelArmed to decide whether to emit those columns.
        Public Property AggrVelSignal As String
        Public Property AggrVelBurstRatio As Double = Double.NaN
        Public Property AggrVelNet As Double = Double.NaN

        ' Informational side-column (never in X). Reported as separate coef table.
        Public Property InfoCategoricals As New Dictionary(Of String, String)()
        Public Property InfoNumerics As New Dictionary(Of String, Double)()
    End Class

    ''' <summary>One (session, resolution) population and everything it carries.</summary>
    Public Class Population
        Public Property Name As String                    ' "NY×1" / "LONDON×3" / "ASIA×3"
        Public Property Rows As List(Of CsvRow) = New List(Of CsvRow)()
        Public Property Features As List(Of FeatureBundle) = New List(Of FeatureBundle)()
        Public Property IsAggrVelArmed As Boolean         ' true → AggrVel is SCORED; false → informational
    End Class

    ''' <summary>Diagnostics from the load pass — reported in the audit report's §1.</summary>
    Public Class LoadStats
        Public Property TotalRows As Integer
        Public Property RepeatedHeadersSkipped As Integer
        Public Property NonV08Excluded As Integer
        Public Property WeekendExcluded As Integer
        Public Property NonDirectionalExcluded As Integer
        Public Property BurstInstancePrefixExcluded As Integer
        Public Property BurstCadenceInstancesExcluded As Integer
        Public Property BurstCadenceRowsExcluded As Integer
        Public Property BurstInstanceIds As New List(Of String)()
    End Class

    Public Class CsvFeatureBuilder

        ' Fixed hard-coded prefix per spec: known burst-instance id family.
        Private Const BurstInstancePrefix As String = "8706ebae"

        ' Any InstanceId whose MEDIAN inter-row gap is below this floor is a sub-cadence
        ' burst — excluded whole. Computed at load time; NOT a second hardcoded id.
        Private Const BurstCadenceMedianGapSec As Double = 45.0

        ''' <summary>Load a pooled analysis_log.csv, filter to v0.8 evaluable directional weekday
        ''' rows, compute burst-instance exclusions, and return one FeatureBundle per surviving
        ''' row. The CsvRow list is aligned position-wise with the FeatureBundle list — every
        ''' downstream stage uses the row's Index field to cross-reference. Populated CsvRows
        ''' feed straight into ForwardWindowJoiner.PopulateForwardBars + FailureRateMatrix's
        ''' shipped resolvers, so the audit's label matches the offline matrix / D4 / What-If
        ''' by construction.</summary>
        Public Shared Function LoadAndBuild(csvPath As String,
                                             ByRef stats As LoadStats) As (List(Of CsvRow), List(Of FeatureBundle), List(Of String))
            stats = New LoadStats()
            Dim allRows As New List(Of CsvRow)()
            Dim allBundles As New List(Of FeatureBundle)()
            Dim instanceIds As New List(Of String)()

            Dim lines As String() = File.ReadAllLines(csvPath)
            If lines.Length <= 1 Then Return (allRows, allBundles, instanceIds)

            ' First line = header; but a pooled book (local + AWS concat) may carry more
            ' header lines from the collector's file — skip any row whose first field is
            ' literally "Timestamp".
            Dim headerFields As String() = lines(0).Split(","c)
            Dim colIdx As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To headerFields.Length - 1
                colIdx(headerFields(i).Trim()) = i
            Next

            Dim hasPlacedSchema As Boolean =
                colIdx.ContainsKey("PlacedTargetLong") AndAlso colIdx.ContainsKey("PlacedStopLong") AndAlso
                colIdx.ContainsKey("PlacedTargetShort") AndAlso colIdx.ContainsKey("PlacedStopShort")

            If Not hasPlacedSchema Then
                Console.WriteLine("[CeilingAudit] CSV header does not carry the v0.8 placed columns — nothing to audit.")
                Return (allRows, allBundles, instanceIds)
            End If

            ' First pass: parse rows into a working list with InstanceId + Timestamp for the
            ' burst-cadence detector. Skip repeated headers here.
            Dim working As New List(Of (Row As CsvRow, Fields As String(), InstanceId As String))()
            For i = 1 To lines.Length - 1
                Dim parts As String() = lines(i).Split(","c)
                If parts.Length < 2 Then Continue For
                If String.Equals(parts(0).Trim(), "Timestamp", StringComparison.OrdinalIgnoreCase) Then
                    stats.RepeatedHeadersSkipped += 1
                    Continue For
                End If

                Dim row As New CsvRow() With {.Index = working.Count}
                ParseCsvRow(row, parts, colIdx, hasPlacedSchema)
                Dim iid As String = GetStr(parts, colIdx, "InstanceId")
                working.Add((row, parts, iid))
            Next
            stats.TotalRows = working.Count

            ' Compute per-InstanceId median inter-row gap (only for iid's we see ≥4 rows —
            ' fewer than that and the median is meaningless).
            Dim iidGroups = working.GroupBy(Function(w) w.InstanceId).ToList()
            Dim burstIids As New HashSet(Of String)(StringComparer.Ordinal)
            For Each g In iidGroups
                Dim iid As String = g.Key
                If String.IsNullOrEmpty(iid) Then Continue For
                If iid.StartsWith(BurstInstancePrefix, StringComparison.OrdinalIgnoreCase) Then
                    burstIids.Add(iid)
                    Continue For
                End If
                Dim sorted = g.OrderBy(Function(w) w.Row.Timestamp).ToList()
                If sorted.Count < 4 Then Continue For
                Dim gaps As New List(Of Double)()
                For j = 1 To sorted.Count - 1
                    Dim dt As TimeSpan = sorted(j).Row.Timestamp - sorted(j - 1).Row.Timestamp
                    If dt.TotalSeconds > 0 AndAlso dt.TotalSeconds < 3600 Then gaps.Add(dt.TotalSeconds)
                Next
                If gaps.Count = 0 Then Continue For
                gaps.Sort()
                Dim median As Double = gaps(gaps.Count \ 2)
                If median < BurstCadenceMedianGapSec Then burstIids.Add(iid)
            Next

            stats.BurstInstanceIds = burstIids.OrderBy(Function(s) s).ToList()
            Dim seenBurstPrefix As Integer = working.Where(
                Function(w) Not String.IsNullOrEmpty(w.InstanceId) AndAlso
                            w.InstanceId.StartsWith(BurstInstancePrefix, StringComparison.OrdinalIgnoreCase)).Count()
            stats.BurstInstancePrefixExcluded = seenBurstPrefix
            stats.BurstCadenceInstancesExcluded = burstIids.Count - If(seenBurstPrefix > 0, iidGroups.
                Where(Function(g) Not String.IsNullOrEmpty(g.Key) AndAlso
                                  g.Key.StartsWith(BurstInstancePrefix, StringComparison.OrdinalIgnoreCase)).Count(), 0)

            Dim seenIids As New HashSet(Of String)()
            Dim keptIndex As Integer = 0
            For Each w In working
                Dim iid As String = w.InstanceId
                If Not String.IsNullOrEmpty(iid) AndAlso burstIids.Contains(iid) Then
                    stats.BurstCadenceRowsExcluded += 1
                    Continue For
                End If

                If Not w.Row.HasPlaced OrElse w.Row.MaxScore <= 0 Then
                    stats.NonV08Excluded += 1
                    Continue For
                End If

                If w.Row.Timestamp = DateTime.MinValue Then Continue For
                Dim dow As DayOfWeek = w.Row.Timestamp.DayOfWeek
                If dow = DayOfWeek.Saturday OrElse dow = DayOfWeek.Sunday Then
                    stats.WeekendExcluded += 1
                    Continue For
                End If

                Dim tier As String = FailureRateMatrix.CanonicalTier(w.Row.Verdict)
                Dim isWeak As Boolean = False
                If String.IsNullOrEmpty(tier) Then
                    ' WEAK is included per K2 — the model may find WEAK carries signal.
                    Dim v As String = If(w.Row.Verdict, "").Trim().ToUpperInvariant()
                    If v = "WEAK LONG" OrElse v = "WEAK SHORT" Then
                        isWeak = True
                    Else
                        stats.NonDirectionalExcluded += 1
                        Continue For
                    End If
                End If

                ' Row survives. Re-key its Index so the aligned bundle index is 0..N-1.
                w.Row.Index = keptIndex
                keptIndex += 1
                allRows.Add(w.Row)

                Dim fb As FeatureBundle = BuildFeatureBundle(w.Row, w.Fields, colIdx)
                fb.RowIndex = w.Row.Index
                allBundles.Add(fb)

                If Not String.IsNullOrEmpty(iid) AndAlso Not seenIids.Contains(iid) Then
                    seenIids.Add(iid)
                    instanceIds.Add(iid)
                End If
            Next

            Return (allRows, allBundles, instanceIds)
        End Function

        ' Populate one CsvRow beyond what ForwardWindowJoiner.Load reads (which we bypass so
        ' the pooled-CSV / repeated-header handling stays in one place).
        Private Shared Sub ParseCsvRow(row As CsvRow, parts As String(),
                                        colIdx As Dictionary(Of String, Integer),
                                        hasPlaced As Boolean)
            If colIdx.ContainsKey("Timestamp") Then
                DateTime.TryParseExact(parts(colIdx("Timestamp")).Trim(),
                                       "yyyy-MM-dd HH:mm:ss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                       row.Timestamp)
            End If
            row.Price = TryD(parts, colIdx, "Price")
            row.Verdict = GetStr(parts, colIdx, "Verdict")
            row.Confidence = GetStr(parts, colIdx, "Confidence")
            row.ATR = TryD(parts, colIdx, "ATR")
            row.Regime = GetStr(parts, colIdx, "Regime")
            row.FundingBias = GetStr(parts, colIdx, "FundingBias")
            row.VerdictContext = GetStr(parts, colIdx, "VerdictContext")
            row.OiCvdOutcome = GetStr(parts, colIdx, "OiCvdOutcome")
            row.OfiRatio = TryD(parts, colIdx, "OFIRatio")
            row.OfiBidVol = TryD(parts, colIdx, "OFIBidVol")
            row.OfiAskVol = TryD(parts, colIdx, "OFIAskVol")
            row.FundingDelta = TryD(parts, colIdx, "FundingDelta")
            row.SwingStopLong = TryD(parts, colIdx, "SwingStopLong")
            row.SwingStopShort = TryD(parts, colIdx, "SwingStopShort")
            row.SwingTargetLong = TryD(parts, colIdx, "SwingTargetLong")
            row.SwingTargetShort = TryD(parts, colIdx, "SwingTargetShort")
            row.VpfrNearestHvnAbove = TryD(parts, colIdx, "VPFRNearestHvnAbove")
            row.VpfrNearestHvnBelow = TryD(parts, colIdx, "VPFRNearestHvnBelow")
            row.TargetCapReason = GetStr(parts, colIdx, "TargetCapReason")
            row.ExecResolution = ParseIntOr(GetStr(parts, colIdx, "ExecResolution"), 1)
            row.LongScore = ParseIntOr(GetStr(parts, colIdx, "LongScore"), 0)
            row.ShortScore = ParseIntOr(GetStr(parts, colIdx, "ShortScore"), 0)
            row.EffectiveLongScore = ParseIntOr(GetStr(parts, colIdx, "EffectiveLongScore"), 0)
            row.EffectiveShortScore = ParseIntOr(GetStr(parts, colIdx, "EffectiveShortScore"), 0)
            row.MaxScore = ParseIntOr(GetStr(parts, colIdx, "MaxScore"), 0)
            row.MtfGatePassLong = ParseBoolOr(GetStr(parts, colIdx, "MTFGatePassLong"), True)
            row.MtfGatePassShort = ParseBoolOr(GetStr(parts, colIdx, "MTFGatePassShort"), True)
            row.HasPlaced = hasPlaced
            If hasPlaced Then
                row.PlacedTargetLong = TryD(parts, colIdx, "PlacedTargetLong")
                row.PlacedStopLong = TryD(parts, colIdx, "PlacedStopLong")
                row.PlacedTargetShort = TryD(parts, colIdx, "PlacedTargetShort")
                row.PlacedStopShort = TryD(parts, colIdx, "PlacedStopShort")
            End If
        End Sub

        ' Build one FeatureBundle from a raw CSV row. Named strings that are always logged
        ' (regardless of scoring-enabled state) come in as-is; the informational partition is
        ' Absorption* + AggrVel (the AggrVel armed-vs-un-armed decision is per POPULATION and
        ' happens in PartitionIntoPopulations, not per row).
        Private Shared Function BuildFeatureBundle(row As CsvRow, parts As String(),
                                                    colIdx As Dictionary(Of String, Integer)) As FeatureBundle
            Dim fb As New FeatureBundle()
            fb.SessionHour = row.Timestamp.Hour
            fb.Regime = If(row.Regime, "")

            ' Baseline = dominant-effective / MaxScore. Ties → 0 (matches the pipeline's own
            ' NO-TRADE-on-tie behaviour; the audit measures what the pipeline HARVESTED, so a
            ' tied row gets zero credit either way).
            Dim dominant As Double = 0
            If row.MaxScore > 0 Then
                If row.EffectiveLongScore > row.EffectiveShortScore Then
                    dominant = CDbl(row.EffectiveLongScore) / row.MaxScore
                ElseIf row.EffectiveShortScore > row.EffectiveLongScore Then
                    dominant = CDbl(row.EffectiveShortScore) / row.MaxScore
                End If
            End If
            fb.BaselineScore = dominant

            ' Scored categoricals — one per column listed in §2's scored inputs.
            Dim scoredCats As String() = {
                "ROCSlope", "RSIDivergence", "SqueezeStatus", "TTMSignal", "EMAAlignment",
                "FundingBias", "FundingMomentum", "OISignal", "OiCvdOutcome", "OFISignal",
                "LiqSignal", "CVDSlope", "CVDDivergence", "MicroCVDSignal", "MicroCVDMomentum",
                "TFISignal", "DonchianSignal", "OBVTrend", "OBVDivergence",
                "TrendStructure5m", "TargetCapReason", "PriceVsEMA200",
                "MTF15mTrend", "MTF15mEMAAlignment"}
            For Each c In scoredCats
                fb.ScoredCategoricals(c) = GetStr(parts, colIdx, c)
            Next

            ' Scored numerics (§2 named list). VolumeRatio + OFIRatio can be zero on cold rows —
            ' left as-is; the train-median impute only fires on NaN (unlogged).
            Dim scoredNums As String() = {"ATR", "VolumeRatio", "ADX", "VWAPDevPct", "SpreadBps", "OFIRatio"}
            For Each n In scoredNums
                fb.ScoredNumerics(n) = TryDOrNaN(parts, colIdx, n)
            Next

            ' AggrVel — carried on every bundle; whether it enters X depends on the population's
            ' armed status.
            fb.AggrVelSignal = GetStr(parts, colIdx, "AggrVelSignal")
            fb.AggrVelBurstRatio = TryDOrNaN(parts, colIdx, "AggrVelBurstRatio")
            fb.AggrVelNet = TryDOrNaN(parts, colIdx, "AggrVelNet")

            ' Informational (never in X): Absorption* is scoring_enabled:false at build.
            fb.InfoCategoricals("AbsorptionSignal") = GetStr(parts, colIdx, "AbsorptionSignal")
            fb.InfoNumerics("AbsorptionLevel") = TryDOrNaN(parts, colIdx, "AbsorptionLevel")
            fb.InfoNumerics("AbsorptionRatio") = TryDOrNaN(parts, colIdx, "AbsorptionRatio")
            fb.InfoNumerics("AbsorptionAggrUsd") = TryDOrNaN(parts, colIdx, "AbsorptionAggrUsd")
            fb.InfoNumerics("AbsorptionPullFrac") = TryDOrNaN(parts, colIdx, "AbsorptionPullFrac")

            Return fb
        End Function

        ''' <summary>Partition rows into NY×1 / LONDON×3 / ASIA×3 populations using
        ''' ExecutionResolution.MatchSessionBucket (the engine's own bucket definition — the
        ''' §1 population filter cannot drift from ApplySessionVolume / ResolveSessionLabel).
        ''' A row whose (session bucket name, ExecResolution) does not match one of the three
        ''' target populations is dropped; NY-hour rows that logged at res-3 (e.g. an old
        ''' backfill) or ASIA/LONDON rows at res-1 do not belong to any of the three.</summary>
        Public Shared Function PartitionIntoPopulations(cfg As EngineSettings,
                                                        rows As List(Of CsvRow),
                                                        bundles As List(Of FeatureBundle)) As List(Of Population)
            Dim targets As New Dictionary(Of String, Population)(StringComparer.OrdinalIgnoreCase) From {
                {"NY|1", New Population With {.Name = "NY×1"}},
                {"LONDON|3", New Population With {.Name = "LONDON×3"}},
                {"ASIA|3", New Population With {.Name = "ASIA×3"}}}

            For i = 0 To rows.Count - 1
                Dim r = rows(i)
                Dim fb = bundles(i)
                Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, r.Timestamp.Hour)
                If bucket Is Nothing OrElse String.IsNullOrEmpty(bucket.Name) Then Continue For
                Dim key As String = bucket.Name & "|" & r.ExecResolution.ToString()
                Dim pop As Population = Nothing
                If Not targets.TryGetValue(key, pop) Then Continue For
                r.Index = pop.Rows.Count
                fb.RowIndex = r.Index
                pop.Rows.Add(r)
                pop.Features.Add(fb)
            Next

            ' Armed-vs-un-armed decision: per bucket, checked once via HasExplicitAggrVelBurstThreshold
            ' at a representative UTC hour (the bucket's start hour is guaranteed inside its range).
            For Each pop In targets.Values
                Dim bucketName As String = pop.Name.Split("×"c)(0)
                Dim repHour As Integer = RepresentativeHourFor(cfg, bucketName)
                pop.IsAggrVelArmed = ExecutionResolution.HasExplicitAggrVelBurstThreshold(cfg, repHour)
            Next

            Return targets.Values.ToList()
        End Function

        Private Shared Function RepresentativeHourFor(cfg As EngineSettings, bucketName As String) As Integer
            If cfg IsNot Nothing AndAlso cfg.SessionVolume IsNot Nothing AndAlso
               cfg.SessionVolume.Sessions IsNot Nothing Then
                For Each b In cfg.SessionVolume.Sessions
                    If b IsNot Nothing AndAlso String.Equals(b.Name, bucketName, StringComparison.OrdinalIgnoreCase) Then
                        Return b.StartHour
                    End If
                Next
            End If
            Return -1
        End Function

        ''' <summary>Attach placed-vs-placed SUCCESS labels to a population's bundles. Walks the
        ''' row's ForwardBars at the F1 horizon (res-1 → 15m, res-3 → 45m — the same horizon
        ''' the BandLadder uses) via the shipped FailureRateMatrix.WalkBars. Rows whose gate
        ''' would kill them (v35 de-confound), rows with ATR≤0, and rows with no bars at the
        ''' horizon are labelled -1 and dropped from every downstream stage.</summary>
        Public Shared Sub AttachLabels(pop As Population, cfg As EngineSettings)
            Dim floorPct As Double = cfg.Scoring.MinTradeableMovePct
            Dim engineTargetMult As Double = cfg.Scoring.AtrTargetMultiplier
            Dim dummyStruct As Integer, dummyAtrFb As Integer
            Dim dummyPlaced As Integer, dummyLegacyFav As Integer

            For i = 0 To pop.Rows.Count - 1
                Dim row = pop.Rows(i)
                Dim fb = pop.Features(i)
                If row.ATR <= 0 Then Continue For

                Dim tier As String = FailureRateMatrix.CanonicalTier(row.Verdict)
                Dim vRaw As String = If(row.Verdict, "").Trim().ToUpperInvariant()
                Dim isWeak As Boolean = (vRaw = "WEAK LONG" OrElse vRaw = "WEAK SHORT")
                Dim isLong As Boolean
                If tier <> "" Then
                    isLong = tier.EndsWith("LONG")
                ElseIf isWeak Then
                    isLong = vRaw.Contains("LONG")
                Else
                    Continue For
                End If

                Dim entry As Double = row.Price
                Dim atr As Double = row.ATR
                Dim floorDist As Double = floorPct * entry
                If FailureRateMatrix.GateTargetDistance(row, isLong, entry, atr,
                                                        AdverseBarrierMode.Placed,
                                                        engineTargetMult) < floorDist Then
                    Continue For
                End If

                Dim advBar As Double = FailureRateMatrix.ResolveAdverseBarrier(
                    row, isLong, entry, atr, AdverseBarrierMode.Placed, dummyStruct, dummyAtrFb)
                Dim favBar As Double = FailureRateMatrix.ResolveFavourableBarrier(
                    row, isLong, entry, atr, AdverseBarrierMode.Placed,
                    engineTargetMult, floorPct, dummyPlaced, dummyLegacyFav)

                Dim horizon As Integer = AnalysisConstants.HoldWindowsForResolution(row.ExecResolution).Max()
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(horizon, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For
                End If

                Dim outcome As String = FailureRateMatrix.WalkBars(bars, favBar, advBar, isLong)
                fb.Label = If(outcome = "SUCCESS", 1, 0)
            Next
        End Sub

        ' -- CSV field helpers -----------------------------------------------

        Private Shared Function TryD(parts As String(), colIdx As Dictionary(Of String, Integer),
                                     key As String) As Double
            Dim idx As Integer
            If Not colIdx.TryGetValue(key, idx) Then Return 0.0
            If idx >= parts.Length Then Return 0.0
            Dim v As Double = 0.0
            Double.TryParse(parts(idx).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, v)
            Return v
        End Function

        Private Shared Function TryDOrNaN(parts As String(), colIdx As Dictionary(Of String, Integer),
                                          key As String) As Double
            Dim idx As Integer
            If Not colIdx.TryGetValue(key, idx) Then Return Double.NaN
            If idx >= parts.Length Then Return Double.NaN
            Dim s As String = parts(idx).Trim()
            If String.IsNullOrEmpty(s) Then Return Double.NaN
            Dim v As Double = 0.0
            If Not Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then Return Double.NaN
            Return v
        End Function

        Private Shared Function GetStr(parts As String(), colIdx As Dictionary(Of String, Integer),
                                       key As String) As String
            Dim idx As Integer
            If Not colIdx.TryGetValue(key, idx) Then Return ""
            If idx >= parts.Length Then Return ""
            Return parts(idx).Trim()
        End Function

        Private Shared Function ParseIntOr(s As String, fallback As Integer) As Integer
            If String.IsNullOrEmpty(s) Then Return fallback
            Dim v As Integer
            Return If(Integer.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, v), v, fallback)
        End Function

        Private Shared Function ParseBoolOr(s As String, fallback As Boolean) As Boolean
            If String.IsNullOrEmpty(s) Then Return fallback
            Dim v As Boolean
            Return If(Boolean.TryParse(s.Trim(), v), v, fallback)
        End Function

    End Class

End Namespace
