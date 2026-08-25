' tools/CeilingAudit/CeilingAuditProgram.vb
' Entry point for the W6-4 offline ceiling-audit runner.
' docs/w6-4-ceiling-audit-method-proposal.md  (APPROVED 2026-07-23, K1–K6 all ticked).
'
' Usage:
'   CeilingAudit <csvPath> [--out <dir>] [--settings <settings.json>] [--min-test-days N]
'                          [--margin 0.03] [--bootstrap-b 1000] [--seed 42]
'   → ceiling_audit_report_<stamp>.md
'
' Exit codes: 0 report written · 1 error (missing CSV, empty book, OHLC fetch failed).
'
' Analysis-only: reads the pooled analysis_log.csv (local + AWS-collector externally
' concatenated), fetches 1m OHLC for the label walk, produces one markdown report. Never
' writes settings.json, never touches engine state.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports DeribitVerdictEngine.CeilingAudit

Public Class CeilingAuditProgram

    Public Shared Function Main(args As String()) As Integer
        Try
            Return RunAsync(args).GetAwaiter().GetResult()
        Catch ex As Exception
            Console.Error.WriteLine("[CeilingAudit] Fatal: " & ex.Message)
            Console.Error.WriteLine(ex.StackTrace)
            Return 1
        End Try
    End Function

    Private Shared Async Function RunAsync(args As String()) As Task(Of Integer)
        SetWorkingDirectoryToRepoRoot()

        If args Is Nothing OrElse args.Length = 0 OrElse args(0).StartsWith("--") Then
            Console.Error.WriteLine("Usage: CeilingAudit <csvPath> [--out <dir>] [--settings <settings.json>] " &
                                    "[--min-test-days N] [--margin 0.03] [--bootstrap-b 1000] [--seed 42]")
            Return 1
        End If

        Dim csvPath As String = args(0)
        Dim outDir As String = "."
        Dim settingsPath As String = "settings.json"
        Dim minTestDays As Integer = 7
        Dim margin As Double = 0.03
        Dim bootstrapB As Integer = 1000
        Dim seed As Integer = 42
        Dim i As Integer = 1
        While i < args.Length
            Select Case args(i).ToLowerInvariant()
                Case "--out" : i += 1 : If i < args.Length Then outDir = args(i)
                Case "--settings" : i += 1 : If i < args.Length Then settingsPath = args(i)
                Case "--min-test-days" : i += 1 : If i < args.Length Then Integer.TryParse(args(i), minTestDays)
                Case "--margin" : i += 1 : If i < args.Length Then Double.TryParse(args(i), NumberStyles.Float, CultureInfo.InvariantCulture, margin)
                Case "--bootstrap-b" : i += 1 : If i < args.Length Then Integer.TryParse(args(i), bootstrapB)
                Case "--seed" : i += 1 : If i < args.Length Then Integer.TryParse(args(i), seed)
            End Select
            i += 1
        End While

        If Not File.Exists(csvPath) Then
            Console.Error.WriteLine("[CeilingAudit] CSV not found: " & csvPath)
            Return 1
        End If

        ' -- Load settings ---------------------------------------------------------
        Dim cfg As EngineSettings
        Try
            SettingsLoader.Initialise(settingsPath)
            cfg = SettingsLoader.Current
        Catch ex As Exception
            Console.Error.WriteLine("[CeilingAudit] Settings load failed: " & ex.Message)
            Return 1
        End Try

        ' ⭐ PROVENANCE RECORD, NOT A VERSION CHECK (2026-08-25).
        ' This used to compare cfg.Version against a build-time literal and WARN on a
        ' mismatch. That check ROTTED BY CONSTRUCTION and did so three times — recorded
        ' as "six versions stale", then seven, then nine — because settings.json bumps
        ' for reasons this tool cannot read. It fired on versions that could not affect
        ' the audit, which trains a reader to ignore it: alarm fatigue on a check nobody
        ' could act on. The version number was only ever a poor PROXY for the real
        ' question, which is "what was this run parameterised by?"
        '
        ' So answer that question directly. The audit consumes EXACTLY these three
        ' values (verified 2026-08-25 by grepping every cfg.* reference in this project):
        '     cfg.Scoring.AtrTargetMultiplier             CsvFeatureBuilder.vb:409
        '     cfg.Scoring.TradeCosts.EffectiveMinMovePct  CsvFeatureBuilder.vb:408
        '     cfg.SessionVolume.Sessions[].StartHour      CsvFeatureBuilder.vb:395
        '
        ' ⛔ DO NOT reintroduce a baseline to compare these against. A hardcoded
        ' expected set is a FOURTH COPY of values that already live in settings.json,
        ' and it drifts exactly as the version literal did — the defect class
        ' docs/seam-audit-2026-08-11.md S-2 found across 11 of 42 method defaults.
        ' RECORD the values; do not judge them. A reader comparing two reports can see
        ' what moved; a literal in here can only go stale.
        Dim consumed As New List(Of String)()
        consumed.Add("atr_target_mult=" & cfg.Scoring.AtrTargetMultiplier.ToString("0.####", CultureInfo.InvariantCulture))
        consumed.Add("min_move_pct=" & cfg.Scoring.TradeCosts.EffectiveMinMovePct.ToString("0.######", CultureInfo.InvariantCulture))
        Dim buckets As New List(Of String)()
        If cfg.SessionVolume IsNot Nothing AndAlso cfg.SessionVolume.Sessions IsNot Nothing Then
            For Each b In cfg.SessionVolume.Sessions
                If b IsNot Nothing Then buckets.Add(b.Name & "@" & b.StartHour.ToString(CultureInfo.InvariantCulture))
            Next
        End If
        ' Absence is reported, never silently rendered as an empty list — a report that
        ' shows "buckets=" reads as "no buckets configured", which is a different and
        ' much worse claim than "this tool could not read them".
        consumed.Add("buckets=" & If(buckets.Count > 0, String.Join("/", buckets), "NONE READABLE"))
        Dim versionCheck As String = "consumed " & String.Join(" · ", consumed)
        Console.WriteLine("[CeilingAudit] " & versionCheck)

        ' -- Load + partition rows -------------------------------------------------
        Dim stats As LoadStats = Nothing
        Dim loaded = CsvFeatureBuilder.LoadAndBuild(csvPath, stats)
        Dim allRows = loaded.Item1
        Dim allBundles = loaded.Item2
        If allRows.Count = 0 Then
            Console.Error.WriteLine("[CeilingAudit] No eligible v0.8+ directional rows in " & csvPath & " — check the schema and filters.")
            Return 1
        End If

        Dim populations = CsvFeatureBuilder.PartitionIntoPopulations(cfg, allRows, allBundles)
        Console.WriteLine("[CeilingAudit] Loaded " & allRows.Count & " eligible rows; populations: " &
                          String.Join(", ", populations.Select(Function(p) p.Name & "=" & p.Rows.Count)))

        ' -- OHLC fetch for the label walk ----------------------------------------
        Dim spanFrom As DateTime = allRows.Min(Function(r) r.Timestamp)
        Dim spanTo As DateTime = allRows.Max(Function(r) r.Timestamp)
        Dim maxWindowMin As Integer = allRows.
            Select(Function(r) AnalysisConstants.HoldWindowsForResolution(r.ExecResolution).Max()).
            DefaultIfEmpty(15).Max()
        Console.WriteLine("[CeilingAudit] OHLC fetch span " & spanFrom.ToString("yyyy-MM-dd HH:mm") &
                          " → " & spanTo.AddMinutes(maxWindowMin + 1).ToString("yyyy-MM-dd HH:mm") & " UTC")
        Dim ohlcMap = Await DeribitOhlcFetcher.FetchOhlcRange(spanFrom, spanTo.AddMinutes(maxWindowMin + 1))
        If ohlcMap Is Nothing Then
            Console.Error.WriteLine("[CeilingAudit] OHLC fetch failed — report cannot be built until Deribit is reachable.")
            Return 1
        End If
        ForwardWindowJoiner.PopulateForwardBars(allRows, ohlcMap)

        ' -- Per-population fit + evaluate ----------------------------------------
        Dim popReports As New List(Of AuditPopulationReport)()
        For Each pop In populations
            CsvFeatureBuilder.AttachLabels(pop, cfg)
            popReports.Add(EvaluatePopulation(pop, cfg, minTestDays, margin, bootstrapB, seed))
        Next

        ' -- Write report ---------------------------------------------------------
        Dim model As New AuditReportModel With {
            .StampUtc = DateTime.UtcNow,
            .CsvPath = Path.GetFullPath(csvPath),
            .SettingsPath = Path.GetFullPath(settingsPath),
            .SettingsVersion = cfg.Version,
            .VersionCheck = versionCheck,
            .MarginDelta = margin,
            .BootstrapB = bootstrapB,
            .BootstrapSeed = seed,
            .MinTestDays = minTestDays,
            .LoadStats = stats,
            .SpanFrom = spanFrom,
            .SpanTo = spanTo,
            .Populations = popReports}
        Dim md As String = AuditReport.Build(model)
        Directory.CreateDirectory(outDir)
        Dim stamp As String = model.StampUtc.ToString("yyyyMMdd_HHmmss")
        Dim outPath As String = Path.Combine(outDir, "ceiling_audit_report_" & stamp & ".md")
        File.WriteAllText(outPath, md)
        Console.WriteLine("[CeilingAudit] Report written: " & Path.GetFullPath(outPath))
        Return 0
    End Function

    Private Shared Function EvaluatePopulation(pop As Population, cfg As EngineSettings,
                                                 minTestDays As Integer, margin As Double,
                                                 bootstrapB As Integer, seed As Integer) As AuditPopulationReport
        Dim rep As New AuditPopulationReport With {
            .Name = pop.Name,
            .IsDecisive = String.Equals(pop.Name, "NY×1", StringComparison.Ordinal),
            .IsAggrVelArmed = pop.IsAggrVelArmed,
            .NRowsTotal = pop.Rows.Count}

        ' Keep only rows with a real label (drop unevaluable rows — no bars, ATR≤0, gate-killed).
        Dim keepIdx As New List(Of Integer)()
        For i = 0 To pop.Features.Count - 1
            If pop.Features(i).Label >= 0 Then keepIdx.Add(i)
        Next
        rep.NRowsLabelled = keepIdx.Count
        If keepIdx.Count < 40 Then
            rep.SkippedReason = "n=" & keepIdx.Count & " < 40 (insufficient for a train/test split; audit skipped)"
            Return rep
        End If

        Dim bundles As New List(Of FeatureBundle)()
        Dim timestamps As New List(Of DateTime)()
        Dim y As New List(Of Integer)()
        Dim baseline As New List(Of Double)()
        Dim tierStrs As New List(Of String)()
        For Each i In keepIdx
            bundles.Add(pop.Features(i))
            timestamps.Add(pop.Rows(i).Timestamp)
            y.Add(pop.Features(i).Label)
            baseline.Add(pop.Features(i).BaselineScore)
            Dim tier As String = FailureRateMatrix.CanonicalTier(pop.Rows(i).Verdict)
            If tier <> "" Then
                tierStrs.Add(If(tier.StartsWith("STRONG"), "STRONG", "MEDIUM"))
            Else
                tierStrs.Add("WEAK")
            End If
        Next

        Dim split = AuditMetrics.MakeChronologicalSplit(bundles, timestamps, minTestDays)
        rep.Split = split
        rep.NTrain = split.TrainIdx.Count
        rep.NTest = split.TestIdx.Count
        If split.TrainIdx.Count < 30 OrElse split.TestIdx.Count < 20 Then
            rep.SkippedReason = "split too small: train=" & split.TrainIdx.Count & " test=" & split.TestIdx.Count
            Return rep
        End If

        ' Build train slices for the schema + numeric stats.
        Dim trainBundles = split.TrainIdx.Select(Function(idx) bundles(idx)).ToList()
        Dim trainTs = split.TrainIdx.Select(Function(idx) timestamps(idx)).ToList()
        Dim yTrain = split.TrainIdx.Select(Function(idx) y(idx)).ToArray()
        Dim yTest = split.TestIdx.Select(Function(idx) y(idx)).ToArray()
        Dim baselineTrain = split.TrainIdx.Select(Function(idx) baseline(idx)).ToArray()
        Dim baselineTest = split.TestIdx.Select(Function(idx) baseline(idx)).ToArray()

        Dim schema = FeatureMatrix.FitSchema(trainBundles, pop.IsAggrVelArmed)
        rep.Schema = schema
        Dim Xtrain = FeatureMatrix.Transform(schema, trainBundles)
        Dim testBundles = split.TestIdx.Select(Function(idx) bundles(idx)).ToList()
        Dim Xtest = FeatureMatrix.Transform(schema, testBundles)

        Dim tuned = AuditMetrics.TuneLambda(Xtrain, yTrain, trainTs)
        rep.BestLambda = tuned.BestLambda
        rep.LambdaAucs = tuned.ValAucs
        Dim model = L2Logistic.Fit(Xtrain, yTrain, tuned.BestLambda)
        rep.Model = model

        Dim challengerScoresTest = model.PredictAll(Xtest)

        ' Operating point K = STRONG+MEDIUM count in TEST (WEAK included in the model's
        ' evaluation set — but the operating point mirrors what the pipeline traded).
        Dim K As Integer = 0
        Dim tierCounts As New Dictionary(Of String, Integer) From {{"STRONG", 0}, {"MEDIUM", 0}, {"WEAK", 0}}
        For Each idx In split.TestIdx
            Dim t As String = tierStrs(idx)
            tierCounts(t) = tierCounts(t) + 1
            If t <> "WEAK" Then K += 1
        Next
        rep.OperatingPointK = K
        rep.TieredCounts = tierCounts

        rep.BaselineTest = AuditMetrics.Evaluate(baselineTest, yTest, K)
        rep.ChallengerTest = AuditMetrics.Evaluate(challengerScoresTest, yTest, K)

        ' Block bootstrap over session-hour blocks on the TEST slice.
        Dim testTs = split.TestIdx.Select(Function(idx) timestamps(idx)).ToList()
        Dim blocks = AuditMetrics.AssignBlocks(testTs)
        rep.DeltaAucCi = AuditMetrics.BootstrapDeltaAucCi(
            challengerScoresTest, baselineTest, yTest, blocks, bootstrapB, seed)

        ' Informational side-column: univariate AUC per Absorption/AggrVel field on test.
        ' Reported but NEVER in the model matrix — the §4 decision reads only the challenger
        ' vs baseline delta above.
        rep.InfoCoefs = BuildInformationalTable(pop, split, testTs, yTest)

        Return rep
    End Function

    Private Shared Function BuildInformationalTable(pop As Population,
                                                     split As ChronologicalSplit,
                                                     testTs As List(Of DateTime),
                                                     yTest As Integer()) As List(Of (Name As String, Coef As Double, N As Integer))
        Dim out As New List(Of (Name As String, Coef As Double, N As Integer))()
        Dim testBundles = split.TestIdx.Select(Function(idx) pop.Features(idx)).ToList()

        ' Absorption categorical — encode as {NONE=0, ABSORB_ABOVE=+1, ABSORB_BELOW=-1}
        ' and report the univariate AUC of the |sign|-adjusted score against the label.
        Dim absScores As New List(Of Double)()
        Dim absN As Integer = 0
        For Each fb In testBundles
            Dim s As String = ""
            fb.InfoCategoricals.TryGetValue("AbsorptionSignal", s)
            Dim x As Double = 0
            If s = "ABSORB_ABOVE" Then
                x = 1 : absN += 1
            ElseIf s = "ABSORB_BELOW" Then
                x = -1 : absN += 1
            End If
            absScores.Add(x)
        Next
        out.Add(("AbsorptionSignal (any)", If(absN = 0, Double.NaN, AuditMetrics.Auc(absScores.ToArray(), yTest)), absN))

        ' TargetCapReason — Step-5b OUTPUT (placed-geometry bucket the arbitration emitted).
        ' Coordinator-demoted 2026-07-23 to informational-only. Per-bucket indicator: SWING/HVN/
        ' POC/NONE — for the univariate AUC we score the "structural tier fired" vs the null case
        ' (swing / hvn / poc → +1; none → 0). The report table shows this ONE number so the
        ' trader can see whether the geometry-difficulty class alone carries directional signal.
        Dim tcrScores As New List(Of Double)()
        Dim tcrN As Integer = 0
        For Each fb In testBundles
            Dim s As String = ""
            fb.InfoCategoricals.TryGetValue("TargetCapReason", s)
            Dim x As Double = 0
            Dim sn As String = If(s, "").ToLowerInvariant()
            If sn = "swing" OrElse sn = "hvn" OrElse sn = "poc" Then
                x = 1 : tcrN += 1
            End If
            tcrScores.Add(x)
        Next
        out.Add(("TargetCapReason (structural fired)", If(tcrN = 0, Double.NaN, AuditMetrics.Auc(tcrScores.ToArray(), yTest)), tcrN))

        ' Absorption numerics — univariate AUC on the numeric itself (median-imputed for
        ' NaN so the AUC is stable; count records the non-NaN denominator).
        For Each nm In {"AbsorptionRatio", "AbsorptionAggrUsd", "AbsorptionPullFrac", "AbsorptionLevel"}
            Dim vals As New List(Of Double)()
            Dim n As Integer = 0
            For Each fb In testBundles
                Dim v As Double
                fb.InfoNumerics.TryGetValue(nm, v)
                If Not Double.IsNaN(v) AndAlso Not Double.IsInfinity(v) Then
                    vals.Add(v) : n += 1
                Else
                    vals.Add(0.0)
                End If
            Next
            out.Add((nm, If(n = 0, Double.NaN, AuditMetrics.Auc(vals.ToArray(), yTest)), n))
        Next

        ' AggrVel — only INFORMATIONAL on un-armed populations; on armed, it's in the model.
        If Not pop.IsAggrVelArmed Then
            Dim signVals As New List(Of Double)()
            Dim n1 As Integer = 0
            For Each fb In testBundles
                Dim x As Double = 0
                If fb.AggrVelSignal = "BURST_BUY" Then
                    x = 1 : n1 += 1
                ElseIf fb.AggrVelSignal = "BURST_SELL" Then
                    x = -1 : n1 += 1
                End If
                signVals.Add(x)
            Next
            out.Add(("AggrVelSignal (un-armed)", If(n1 = 0, Double.NaN, AuditMetrics.Auc(signVals.ToArray(), yTest)), n1))
            For Each nm In {"AggrVelBurstRatio", "AggrVelNet"}
                Dim vals As New List(Of Double)()
                Dim n As Integer = 0
                For Each fb In testBundles
                    Dim v As Double = If(nm = "AggrVelBurstRatio", fb.AggrVelBurstRatio, fb.AggrVelNet)
                    If Not Double.IsNaN(v) AndAlso Not Double.IsInfinity(v) Then
                        vals.Add(v) : n += 1
                    Else
                        vals.Add(0.0)
                    End If
                Next
                out.Add((nm & " (un-armed)", If(n = 0, Double.NaN, AuditMetrics.Auc(vals.ToArray(), yTest)), n))
            Next
        End If

        Return out
    End Function

    Private Shared Sub SetWorkingDirectoryToRepoRoot()
        Try
            Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
            For level = 1 To 8
                dir = Path.GetFullPath(Path.Combine(dir, ".."))
                If File.Exists(Path.Combine(dir, "DeribitVerdictEngine.sln")) Then
                    Directory.SetCurrentDirectory(dir)
                    Return
                End If
            Next
        Catch
        End Try
    End Sub

End Class
