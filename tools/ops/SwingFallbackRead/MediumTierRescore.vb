Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/MediumTierRescore.vb
'
' --mode rescore: the re-score reconstruction of the MEDIUM-tier bug hunt, session 1
' (docs/medium-tier-diagnosis-brief-2026-09-16.md §2.1; read: docs/medium-tier-bug-hunt-2026-09-16.md).
' Question: does the logged score equal what the shipped code computes from the logged inputs?
'
' For every trading-week row from the v51 edge (the swing read's funnel, NO TRADE rows kept):
'   1. Rebuild IndicatorResults from the CSV columns, by header name.
'   2. Fill the inputs the CSV does not log:
'        VPFRSignal, VPFRPoc     the shipped CalcVPFRLite on exchange candles, verified against the four
'                                logged VPFR fields (--mode pocgate's method, PgRecomputeVpfr);
'        DynamicNorms            the shipped DynamicNorms.Compute on the same candle window, with the row's
'                                era settings loaded through SettingsLoader;
'        SpreadStatus            the shipped ClassifySpread on the logged SpreadBps;
'        RocMagnitudeThreshold,  the shipped resolvers on the row's UTC hour.
'        SessionUtcHour
'      LiqSignal is taken as logged (NONE on every row: finding L-1; orchestrator ruling D-7).
'   3. Run the shipped ScoringEngine.Calculate under the row's ERA settings: settings.json at the last
'      settings commit at or before the row (git show), loaded through SettingsLoader.Initialise.
'   4. Compare LongScore, ShortScore, EffectiveLongScore, EffectiveShortScore, RegimePenalty, Verdict.
'   5. Classify each mismatch by the FIRST test that makes all six fields match:
'        settings era (another version) -> SessionUtcHour boundary -> logged precision (one field moved by
'        half its print unit) -> unlogged VPFR label -> unlogged volume thresholds -> both -> unexplained.
' The per-row SignalBreakdown points and mutation flags go to <cache>/rescore-attribution.csv (session 2).
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode rescore \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks

Partial Module SwingFallbackReadProgram

    ' (version, commit, commit time UTC). The commit time is the era edge. A collector may load a version
    ' later than its commit (deploy lag); the mismatch classification tests the other eras explicitly.
    Private ReadOnly RsEraTable As Tuple(Of Integer, String, DateTime)() = {
        Tuple.Create(51, "9ab3f04", New DateTime(2026, 7, 6, 13, 8, 51)),
        Tuple.Create(52, "3bc2a1e", New DateTime(2026, 7, 14, 14, 19, 52)),
        Tuple.Create(53, "1811b8d", New DateTime(2026, 7, 15, 15, 7, 43)),
        Tuple.Create(54, "ae6678c", New DateTime(2026, 7, 17, 13, 38, 39)),
        Tuple.Create(55, "4eef0d8", New DateTime(2026, 7, 21, 14, 24, 0)),
        Tuple.Create(56, "8c497e3", New DateTime(2026, 7, 21, 16, 54, 33)),
        Tuple.Create(57, "5f0b6b6", New DateTime(2026, 7, 21, 17, 30, 8)),
        Tuple.Create(58, "bd31a1a", New DateTime(2026, 7, 22, 9, 35, 58)),
        Tuple.Create(59, "46e7614", New DateTime(2026, 7, 22, 9, 54, 17)),
        Tuple.Create(60, "631a3f5", New DateTime(2026, 7, 22, 17, 35, 47)),
        Tuple.Create(61, "e9e6407", New DateTime(2026, 7, 22, 19, 19, 50)),
        Tuple.Create(62, "ce4ce37", New DateTime(2026, 7, 27, 15, 1, 55)),
        Tuple.Create(63, "74f77a0", New DateTime(2026, 7, 29, 16, 30, 51)),
        Tuple.Create(64, "1229a30", New DateTime(2026, 7, 30, 18, 43, 32)),
        Tuple.Create(65, "970087b", New DateTime(2026, 8, 1, 18, 45, 0)),
        Tuple.Create(66, "fd52299", New DateTime(2026, 8, 10, 18, 6, 57)),
        Tuple.Create(67, "613cf1e", New DateTime(2026, 8, 20, 14, 10, 9)),
        Tuple.Create(68, "aa0e6e7", New DateTime(2026, 8, 21, 16, 20, 29))}

    Private ReadOnly RsSixNames As String() = {"LongScore", "ShortScore", "EffectiveLongScore", "EffectiveShortScore", "RegimePenalty", "Verdict"}
    Private ReadOnly RsSecondaryNames As String() = {"MaxScore", "VerdictContext", "OiCvdOutcome", "Placed levels"}

    ' Breakdown label -> CSV column key. "ADX>" carries the trend threshold, matched by prefix.
    Private ReadOnly RsLabelKeys As Tuple(Of String, String)() = {
        Tuple.Create("ROC(9)", "ROC"), Tuple.Create("RSI(9)", "RSI"), Tuple.Create("DMI +/-DI", "DMI"), Tuple.Create("ADX>", "ADX"),
        Tuple.Create("Volume", "VOL"), Tuple.Create("VWAP", "VWAP"), Tuple.Create("BBW/TTM", "BBW_TTM"), Tuple.Create("EMA 9/21/50", "EMA"),
        Tuple.Create("Funding (info)", "FUNDING"), Tuple.Create("OI Delta", "OI"), Tuple.Create("OFI", "OFI"), Tuple.Create("CVD", "CVD"),
        Tuple.Create("TFI", "TFI"), Tuple.Create("MicroCVD", "MICROCVD"), Tuple.Create("Liq Penalty", "LIQ"), Tuple.Create("Spread", "SPREAD"),
        Tuple.Create("5m EMA(200)", "EMA200"), Tuple.Create("Donchian(20)", "DONCHIAN"), Tuple.Create("OBV", "OBV"), Tuple.Create("VPFR-lite", "VPFR"),
        Tuple.Create("Regime Align (2c)", "REGIME_ALIGN"), Tuple.Create("Trend Structure", "TREND_STRUCTURE")}

    Private Class RsRaw
        Public Ts As DateTime
        Public Inst As String = ""
        Public Col As Dictionary(Of String, Integer)
        Public F As String() = {}
    End Class

    Private Class RsEra
        Public Version As Integer
        Public Commit As String = ""
        Public Edge As DateTime
        Public Cfg As EngineSettings
        Public NormsKey As String = ""
        Public FilePath As String = ""
        Public Rows As Integer
    End Class

    Private Class RsRow
        Public Csv As CsvRow
        Public Raw As RsRaw
        Public Session As String = ""
        Public InPopulation As Boolean
        Public Era As Integer
        Public Vpfr As PgVpfr
        Public Bars As PgBars
        Public IPrev As Integer
        Public IForm As Integer
        Public FormOpen As Long
        Public LoggedVah As Double
        Public LoggedVal As Double
        Public Norms As New Dictionary(Of String, DynamicNorms)(StringComparer.Ordinal)
        Public NormsPrevHour As New Dictionary(Of String, DynamicNorms)(StringComparer.Ordinal)
        Public V As VerdictResult
        Public Mask As Integer
        Public Secondary As Integer
        Public Cls As String = "match"
        Public Detail As String = ""
        ' Every single-cause test that reproduces all six fields: kind code -> detail.
        Public Expl As New List(Of Tuple(Of String, String))()
    End Class

    ' Explanation kinds, in the precedence that picks the primary class. Input uncertainty first
    ' (the CSV does not carry the exact value the run scored), then a settings edge, then the two
    ' kinds with no input-uncertainty reading (a live run that did not do what its logged inputs say).
    Private ReadOnly RsKinds As Tuple(Of String, String)() = {
        Tuple.Create("P", "logged precision (one input moved by half its print unit)"),
        Tuple.Create("H", "SessionUtcHour boundary (run started in the previous UTC hour)"),
        Tuple.Create("F", "VPFR forming-bar volume (the live forming bar carried volume; the variant still verifies the four logged VPFR fields)"),
        Tuple.Create("V", "unlogged VPFR label (another label, no verification)"),
        Tuple.Create("N", "unlogged volume thresholds"),
        Tuple.Create("VN", "unlogged VPFR label and volume thresholds together"),
        Tuple.Create("EA", "adjacent settings era within 72 h of its edge (deploy or hot-reload timing)"),
        Tuple.Create("B", "burst modifier not applied (AggrVelSignal read as NORMAL)"),
        Tuple.Create("E", "other settings era, not adjacent")}

    Private Delegate Sub RsPerturb(r As IndicatorResults, d As Double, cfg As EngineSettings)

    Private Async Function RunRescoreAsync(header As StringBuilder, cfg As EngineSettings, root As String, cacheDir As String,
                                           pooledPath As String, livePath As String, settingsCopy As String,
                                           merged As Dictionary(Of DateTime, Tuple(Of CsvRow, String)),
                                           sigs As List(Of Sig), collectorStart As DateTime, collectorIds As HashSet(Of String),
                                           weekOpenHour As Integer, weekCloseHourExcl As Integer,
                                           outPath As String) As Task(Of Integer)
        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode rescore (re-score reconstruction)"))
        o.AppendLine("- Brief: docs/medium-tier-diagnosis-brief-2026-09-16.md section 2.1. Read: docs/medium-tier-bug-hunt-2026-09-16.md.")
        o.AppendLine("- LiqSignal is taken as logged (finding L-1, orchestrator ruling D-7).")
        o.AppendLine()

        ' ---- raw rows by header name: pooled book first, then the box live log (the merge's rule)
        Dim raw As New Dictionary(Of DateTime, RsRaw)()
        RsLoadRaw(pooledPath, raw)
        RsLoadRaw(livePath, raw)
        Dim va As New Dictionary(Of DateTime, Tuple(Of Double, Double))()
        PgLoadValueArea(pooledPath, va)
        PgLoadValueArea(livePath, va)

        ' ---- the swing read's funnel, keeping NO TRADE rows
        Dim popTs As New HashSet(Of DateTime)(sigs.Select(Function(s) s.Ts))
        Dim rows As New List(Of RsRow)()
        Dim nPre As Integer = 0, nConc As Integer = 0, nOff As Integer = 0, nNoSess As Integer = 0, nNoRaw As Integer = 0
        For Each kv In merged.OrderBy(Function(k) k.Key)
            Dim c = kv.Value.Item1
            If c.Timestamp < V51Edge Then nPre += 1 : Continue For
            If c.Timestamp >= collectorStart AndAlso Not collectorIds.Contains(kv.Value.Item2) Then nConc += 1 : Continue For
            If Not InTradingWeek(c.Timestamp, weekOpenHour, weekCloseHourExcl) Then nOff += 1 : Continue For
            Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, c.Timestamp.Hour)
            If bucket Is Nothing Then nNoSess += 1 : Continue For
            Dim x As RsRaw = Nothing
            If Not raw.TryGetValue(c.Timestamp, x) OrElse Not va.ContainsKey(c.Timestamp) Then nNoRaw += 1 : Continue For
            rows.Add(New RsRow With {.Csv = c, .Raw = x, .Session = bucket.Name.ToUpperInvariant(), .InPopulation = popTs.Contains(c.Timestamp)})
        Next

        ' ---- eras: settings.json at each settings commit, loaded through SettingsLoader
        Dim eraDir As String = Path.Combine(cacheDir, "rescore-eras")
        Directory.CreateDirectory(eraDir)
        Dim eras As New List(Of RsEra)()
        For Each e In RsEraTable
            Dim json As String = RsGitShow(root, e.Item2 & ":settings.json")
            Dim f As String = Path.Combine(eraDir, "settings-v" & e.Item1.ToString(Inv) & ".json")
            File.WriteAllText(f, json, New UTF8Encoding(False))
            SettingsLoader.Initialise(f)
            Dim ec As EngineSettings = SettingsLoader.Current
            If ec.Version <> e.Item1 Then Throw New InvalidOperationException("settings at " & e.Item2 & " reads version " & ec.Version.ToString(Inv) & ", expected " & e.Item1.ToString(Inv) & ". STOP.")
            eras.Add(New RsEra With {.Version = e.Item1, .Commit = e.Item2, .Edge = e.Item3, .Cfg = ec, .NormsKey = RsNormsKey(ec), .FilePath = f})
        Next
        For Each rw In rows
            Dim idx As Integer = 0
            For i As Integer = 0 To eras.Count - 1
                If eras(i).Edge <= rw.Csv.Timestamp Then idx = i
            Next
            rw.Era = idx
            eras(idx).Rows += 1
        Next

        ' ---- candles (the pocgate cache) and the VPFR recompute
        Dim covLog As New StringBuilder()
        covLog.AppendLine("| Resolution | Store files read | Bars from the store | Bars from this instrument's cache | Bars fetched this run | Bars missing in the needed span |")
        covLog.AppendLine("|---|---|---|---|---|---|")
        Dim minMs As Long = ToMs(rows.Min(Function(x) x.Csv.Timestamp))
        Dim maxMs As Long = ToMs(rows.Max(Function(x) x.Csv.Timestamp))
        Dim barsByRes As New Dictionary(Of Integer, PgBars)()
        For Each res In rows.Select(Function(x) Math.Max(1, x.Csv.ExecResolution)).Distinct().OrderBy(Function(x) x).ToList()
            Dim resMs As Long = res * 60000L
            barsByRes(res) = Await PgLoadBarsAsync(root, cacheDir, res, (minMs \ resMs) * resMs - 260L * resMs, (maxMs \ resMs) * resMs + resMs, covLog)
        Next
        Dim nNoBars As Integer = 0
        Dim usable As New List(Of RsRow)()
        For Each rw In rows
            Dim res As Integer = Math.Max(1, rw.Csv.ExecResolution)
            Dim bars As PgBars = Nothing
            If Not barsByRes.TryGetValue(res, bars) Then nNoBars += 1 : Continue For
            Dim resMs As Long = res * 60000L
            Dim formOpen As Long = (ToMs(rw.Csv.Timestamp) \ resMs) * resMs
            Dim iPrev As Integer = Array.BinarySearch(bars.OpenMs, formOpen - resMs)
            If iPrev < 250 Then nNoBars += 1 : Continue For
            Dim iForm As Integer = Array.BinarySearch(bars.OpenMs, formOpen)
            Dim lv = va(rw.Csv.Timestamp)
            rw.Vpfr = PgRecomputeVpfr(bars, iPrev, iForm, formOpen, rw.Csv, lv.Item1, lv.Item2, cfg, Nothing)
            rw.Bars = bars : rw.IPrev = iPrev : rw.IForm = iForm : rw.FormOpen = formOpen
            rw.LoggedVah = lv.Item1 : rw.LoggedVal = lv.Item2
            usable.Add(rw)
        Next
        rows = usable

        ' ---- norms: one pass per distinct norms-relevant settings group (SettingsLoader.Current is what
        '      DynamicNorms.Compute reads)
        Dim normKeys = eras.Select(Function(e) e.NormsKey).Distinct().ToList()
        For Each nk In normKeys
            Dim rep = eras.First(Function(e) e.NormsKey = nk)
            SettingsLoader.Initialise(rep.FilePath)
            For Each rw In rows
                Dim hour As Integer = rw.Csv.Timestamp.Hour
                Dim atr As Double = RsD(rw.Raw, "ATR")
                rw.Norms(nk) = DynamicNorms.Compute(rw.Vpfr.Window, atr, hour)
                If RsNearHourStart(rw.Csv.Timestamp) Then rw.NormsPrevHour(nk) = DynamicNorms.Compute(rw.Vpfr.Window, atr, (hour + 23) Mod 24)
            Next
        Next
        SettingsLoader.Initialise(settingsCopy)

        ' ---- primary re-score and classification
        Dim perturbs = RsPerturbations()
        For Each rw In rows
            Dim era = eras(rw.Era)
            Dim hour As Integer = rw.Csv.Timestamp.Hour
            Dim primary = RsScore(rw, era, hour, rw.Norms(era.NormsKey), rw.Vpfr, rw.Vpfr.Signal, Nothing, 0)
            rw.V = primary.Item1
            rw.Mask = RsMask(rw.V, rw.Raw)
            rw.Secondary = RsSecondaryMask(rw.V, rw.Raw, primary.Item2, era.Cfg)
            If rw.Mask = 0 Then Continue For
            RsClassify(rw, eras, perturbs, cfg)
            Dim first = RsKinds.FirstOrDefault(Function(k) rw.Expl.Any(Function(x) x.Item1 = k.Item1))
            If first Is Nothing Then
                rw.Cls = "unexplained"
                rw.Detail = RsMaskText(rw.Mask)
            Else
                rw.Cls = first.Item2
                rw.Detail = rw.Expl.First(Function(x) x.Item1 = first.Item1).Item2
            End If
        Next

        Dim pop = rows.Where(Function(x) x.InPopulation).ToList()

        ' ================================================================== report
        o.AppendLine("## 1. Row funnel")
        o.AppendLine()
        o.AppendLine("| Step | Rows |")
        o.AppendLine("|---|---|")
        o.AppendLine(Row("Merged rows (pooled book + box live log, first timestamp wins)", merged.Count))
        o.AppendLine(Row("Before the v51 geometry boundary", nPre))
        o.AppendLine(Row("Non-collector instance rows inside the collector era", nConc))
        o.AppendLine(Row("Outside the trading week", nOff))
        o.AppendLine(Row("No session bucket", nNoSess))
        o.AppendLine(Row("No raw columns or no logged VPFRVAH/VPFRVAL", nNoRaw))
        o.AppendLine(Row("No candle window", nNoBars))
        o.AppendLine(Row("**Rows re-scored**", rows.Count))
        o.AppendLine(Row("... of which swing read population rows", pop.Count))
        o.AppendLine(Row("Swing read population, for reference", sigs.Count))
        o.AppendLine()
        o.AppendLine("Candle coverage:")
        o.AppendLine()
        o.Append(covLog.ToString())
        o.AppendLine()

        o.AppendLine("## 2. Settings eras")
        o.AppendLine()
        o.AppendLine("| Version | Commit | Era edge (commit time, UTC) | Rows assigned | Norms settings group | structural_levels.enabled | value_area_scoring_enabled |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each e In eras
            o.AppendLine(String.Format(Inv, "| v{0} | {1} | {2:yyyy-MM-dd HH:mm:ss} | {3} | {4} | {5} | {6} |", e.Version, e.Commit, e.Edge, e.Rows, normKeys.IndexOf(e.NormsKey) + 1,
                e.Cfg.Scoring.StructuralLevels IsNot Nothing AndAlso e.Cfg.Scoring.StructuralLevels.Enabled, e.Cfg.Indicators.VPFR.ValueAreaScoringEnabled))
        Next
        o.AppendLine()

        o.AppendLine("## 3. Input provenance (every IndicatorResults field ScoringEngine.Calculate reads)")
        o.AppendLine()
        o.AppendLine("| Field | Source |")
        o.AppendLine("|---|---|")
        For Each p In RsProvenance()
            o.AppendLine("| " & p.Item1 & " | " & p.Item2 & " |")
        Next
        o.AppendLine()
        Dim verAll As Integer = rows.Where(Function(x) x.Vpfr.WindowVariant >= 0).Count()
        o.AppendLine(String.Format(Inv, "- VPFR profile verified on {0} of {1} rows ({2:0.0} %); population {3} of {4}. Unverified rows use the first window variant's recompute.",
            verAll, rows.Count, 100.0 * verAll / Math.Max(1, rows.Count), pop.Where(Function(x) x.Vpfr.WindowVariant >= 0).Count(), pop.Count))
        o.AppendLine()

        o.AppendLine("## 4. Match rate per field (primary re-score: the row's era, recomputed VPFR and norms)")
        o.AppendLine()
        o.AppendLine("| Field | Population rows matching | Population % | All rows matching | All rows % |")
        o.AppendLine("|---|---|---|---|---|")
        For bit As Integer = 0 To RsSixNames.Length - 1
            Dim b As Integer = 1 << bit
            Dim pm As Integer = pop.Where(Function(x) (x.Mask And b) = 0).Count()
            Dim am As Integer = rows.Where(Function(x) (x.Mask And b) = 0).Count()
            o.AppendLine(String.Format(Inv, "| {0} | {1} of {2} | {3:0.00} | {4} of {5} | {6:0.00} |", RsSixNames(bit), pm, pop.Count, 100.0 * pm / Math.Max(1, pop.Count), am, rows.Count, 100.0 * am / Math.Max(1, rows.Count)))
        Next
        Dim pAll As Integer = pop.Where(Function(x) x.Mask = 0).Count()
        Dim aAll As Integer = rows.Where(Function(x) x.Mask = 0).Count()
        o.AppendLine(String.Format(Inv, "| **All six** | {0} of {1} | {2:0.00} | {3} of {4} | {5:0.00} |", pAll, pop.Count, 100.0 * pAll / Math.Max(1, pop.Count), aAll, rows.Count, 100.0 * aAll / Math.Max(1, rows.Count)))
        o.AppendLine()
        o.AppendLine("| Secondary field | All rows matching | All rows % |")
        o.AppendLine("|---|---|---|")
        For bit As Integer = 0 To RsSecondaryNames.Length - 1
            Dim b As Integer = 1 << bit
            Dim am As Integer = rows.Where(Function(x) (x.Secondary And b) = 0).Count()
            o.AppendLine(String.Format(Inv, "| {0} | {1} of {2} | {3:0.00} |", RsSecondaryNames(bit), am, rows.Count, 100.0 * am / Math.Max(1, rows.Count)))
        Next
        o.AppendLine(String.Format(Inv, "- Ledger guard (CheckLedger) mismatches in the primary re-score: {0}.", rows.Where(Function(x) x.V.LedgerMismatch).Count()))
        o.AppendLine()
        o.AppendLine("| Secondary mismatch | Rows | ... with all six fields matching | ... population rows | ... VPFR profile verified |")
        o.AppendLine("|---|---|---|---|---|")
        For bit As Integer = 0 To RsSecondaryNames.Length - 1
            Dim b As Integer = 1 << bit
            Dim sm = rows.Where(Function(x) (x.Secondary And b) <> 0).ToList()
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} |", RsSecondaryNames(bit), sm.Count, sm.Where(Function(x) x.Mask = 0).Count(),
                sm.Where(Function(x) x.InPopulation).Count(), sm.Where(Function(x) x.Vpfr.WindowVariant >= 0).Count()))
        Next
        o.AppendLine()
        Dim plMis = rows.Where(Function(x) (x.Secondary And 8) <> 0).ToList()
        If plMis.Count > 0 Then
            o.AppendLine("| Placed-level mismatch (UTC) | Six match | Population | VPFR verified | Recomputed label | Logged long target / stop | Re-scored long target / stop | Logged short target / stop | Re-scored short target / stop |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|")
            For Each x In plMis.Take(30)
                Dim r2 = RsIndicators(x, eras(x.Era).Cfg, x.Csv.Timestamp.Hour, x.Vpfr, x.Vpfr.Signal)
                Dim pl = SignalEmitter.ComputeSideLevels(x.V, r2, eras(x.Era).Cfg, isLong:=True)
                Dim ps = SignalEmitter.ComputeSideLevels(x.V, r2, eras(x.Era).Cfg, isLong:=False)
                o.AppendLine(String.Format(Inv, "| {0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3} | {4} | {5} / {6} | {7:F2} / {8:F2} ({9}) | {10} / {11} | {12:F2} / {13:F2} ({14}) |", x.Csv.Timestamp,
                    If(x.Mask = 0, "yes", "no"), If(x.InPopulation, "yes", "no"), If(x.Vpfr.WindowVariant >= 0, "yes", "no"), x.Vpfr.Signal,
                    RsS(x.Raw, "PlacedTargetLong"), RsS(x.Raw, "PlacedStopLong"), pl.Target, pl.StopPx, pl.TargetReason,
                    RsS(x.Raw, "PlacedTargetShort"), RsS(x.Raw, "PlacedStopShort"), ps.Target, ps.StopPx, ps.TargetReason))
            Next
            o.AppendLine()
        End If

        o.AppendLine("| Session | Era | Rows | All six match | % |")
        o.AppendLine("|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each e In eras
                Dim sessL As String = sess, eL As Integer = eras.IndexOf(e)
                Dim sub1 = rows.Where(Function(x) x.Session = sessL AndAlso x.Era = eL).ToList()
                If sub1.Count = 0 Then Continue For
                Dim m As Integer = sub1.Where(Function(x) x.Mask = 0).Count()
                o.AppendLine(String.Format(Inv, "| {0} | v{1} | {2} | {3} | {4:0.00} |", sess, e.Version, sub1.Count, m, 100.0 * m / sub1.Count))
            Next
        Next
        o.AppendLine()

        Dim mism = rows.Where(Function(x) x.Mask <> 0).ToList()
        o.AppendLine("## 5. Mismatch explanations")
        o.AppendLine()
        o.AppendLine("Every single-cause test runs on every mismatching row; a test EXPLAINS the row when it reproduces all six fields. The primary class is the first explaining kind in this precedence:")
        o.AppendLine()
        o.AppendLine("| Code | Kind |")
        o.AppendLine("|---|---|")
        For Each k In RsKinds
            o.AppendLine("| " & k.Item1 & " | " & k.Item2 & " |")
        Next
        o.AppendLine()
        o.AppendLine("### 5.1 Primary class")
        o.AppendLine()
        o.AppendLine("| Primary class | Population rows | All rows | Examples (UTC, detail) |")
        o.AppendLine("|---|---|---|---|")
        For Each g In mism.GroupBy(Function(x) x.Cls).OrderByDescending(Function(x) x.Count())
            Dim gl = g.ToList()
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} |", g.Key, gl.Where(Function(x) x.InPopulation).Count(), gl.Count,
                String.Join("; ", gl.Take(4).Select(Function(x) x.Csv.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", Inv) & " " & x.Detail))))
        Next
        o.AppendLine()
        o.AppendLine("### 5.2 Explanation sets (which kinds explain the same row)")
        o.AppendLine()
        o.AppendLine("| Explaining kinds | Population rows | All rows |")
        o.AppendLine("|---|---|---|")
        For Each g In mism.GroupBy(Function(x) RsExplSet(x)).OrderByDescending(Function(x) x.Count()).ThenBy(Function(x) x.Key, StringComparer.Ordinal)
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} |", g.Key, g.Where(Function(x) x.InPopulation).Count(), g.Count()))
        Next
        o.AppendLine()
        o.AppendLine("| Kind | Rows it explains (all rows) | ... and no input-uncertainty kind (P, H, F, V, N, VN) explains them |")
        o.AppendLine("|---|---|---|")
        Dim inputKinds As String() = {"P", "H", "F", "V", "N", "VN"}
        For Each k In RsKinds
            Dim kc As String = k.Item1
            Dim withK = mism.Where(Function(x) x.Expl.Any(Function(e) e.Item1 = kc)).ToList()
            Dim only = withK.Where(Function(x) Not x.Expl.Any(Function(e) inputKinds.Contains(e.Item1))).Count()
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} |", kc, withK.Count, only))
        Next
        o.AppendLine()

        o.AppendLine("### 5.3 Burst-modifier rows against the rest")
        o.AppendLine()
        o.AppendLine("A burst-modifier row is one where the re-score's TFI breakdown note shows the burst modifier applied (confirm or contra).")
        o.AppendLine()
        o.AppendLine("| Rows | Re-scored | Mismatching | % | Explained by F | Explained by V | Explained by B |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each isBurst In {True, False}
            Dim ib As Boolean = isBurst
            Dim grp = rows.Where(Function(x) RsMutations(x.V).Any(Function(m) m.StartsWith("BURST_", StringComparison.Ordinal)) = ib).ToList()
            Dim mm = grp.Where(Function(x) x.Mask <> 0).ToList()
            o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3:0.00} | {4} | {5} | {6} |", If(ib, "Burst modifier applied", "No burst modifier"), grp.Count, mm.Count,
                100.0 * mm.Count / Math.Max(1, grp.Count), mm.Where(Function(x) x.Expl.Any(Function(e) e.Item1 = "F")).Count(),
                mm.Where(Function(x) x.Expl.Any(Function(e) e.Item1 = "V")).Count(), mm.Where(Function(x) x.Expl.Any(Function(e) e.Item1 = "B")).Count()))
        Next
        o.AppendLine()

        o.AppendLine("### 5.4 Rows no input-uncertainty kind explains (the defect candidates)")
        o.AppendLine()
        Dim cand = mism.Where(Function(x) Not x.Expl.Any(Function(e) inputKinds.Contains(e.Item1))).ToList()
        If cand.Count = 0 Then
            o.AppendLine("- None.")
        Else
            o.AppendLine("| UTC | Instance | Session | Regime | Era | Population | Mismatching fields | Logged verdict | Re-scored verdict | Explaining kinds |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|---|")
            For Each x In cand
                o.AppendLine(String.Format(Inv, "| {0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3} | v{4} | {5} | {6} | {7} | {8} | {9} |", x.Csv.Timestamp, If(x.Raw.Inst.Length > 8, x.Raw.Inst.Substring(0, 8), x.Raw.Inst),
                    x.Session, RsS(x.Raw, "Regime"), eras(x.Era).Version, If(x.InPopulation, "yes", "no"), RsMaskText(x.Mask), RsS(x.Raw, "Verdict"), x.V.Verdict,
                    If(x.Expl.Count = 0, "none", String.Join("; ", x.Expl.Select(Function(e) e.Item1 & " " & e.Item2)))))
            Next
        End If
        o.AppendLine()

        ' ---- attribution file
        Dim attrPath As String = Path.Combine(cacheDir, "rescore-attribution.csv")
        RsWriteAttribution(attrPath, rows, eras)
        o.AppendLine("## 6. Attribution file for session 2")
        o.AppendLine()
        o.AppendLine("- Path: " & attrPath & " (gitignored). One line per re-scored row: identity, era, VPFR verification, logged and re-scored verdict, match and class, the six scores, signed long and short points per breakdown label (from the primary re-score), and mutation flags read from the breakdown notes.")
        o.AppendLine(String.Format(Inv, "- Rows written: {0}.", rows.Count))
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

    ' ------------------------------------------------------------------ scoring

    Private Function RsScore(rw As RsRow, era As RsEra, hour As Integer, norms As DynamicNorms, vp As PgVpfr, vpfrSignal As String,
                             perturb As RsPerturb, delta As Double) As Tuple(Of VerdictResult, IndicatorResults)
        Dim r = RsIndicators(rw, era.Cfg, hour, vp, vpfrSignal)
        If perturb IsNot Nothing Then perturb(r, delta, era.Cfg)
        Dim v = ScoringEngine.Calculate(r, PositionState.None, norms, era.Cfg)
        Return Tuple.Create(v, r)
    End Function

    Private Function RsExplSet(rw As RsRow) As String
        If rw.Expl.Count = 0 Then Return "none (unexplained)"
        Return String.Join(" + ", RsKinds.Where(Function(k) rw.Expl.Any(Function(e) e.Item1 = k.Item1)).Select(Function(k) k.Item1))
    End Function

    ''' <summary>Runs EVERY single-cause test on a mismatching row and records each one that
    ''' reproduces all six fields. No early return: a row two kinds explain is reported as ambiguous.</summary>
    Private Sub RsClassify(rw As RsRow, eras As List(Of RsEra), perturbs As List(Of Tuple(Of String, Double, RsPerturb)), cfg As EngineSettings)
        Dim era = eras(rw.Era)
        Dim hour As Integer = rw.Csv.Timestamp.Hour
        Dim sig As String = rw.Vpfr.Signal
        Dim nm = rw.Norms(era.NormsKey)
        Dim verified As String = If(rw.Vpfr.WindowVariant >= 0, "verified profile", "unverified profile")

        ' P: one rounded input moved by half its print unit
        For Each p In perturbs
            For Each dirSign In {1.0, -1.0}
                If RsMask(RsScore(rw, era, hour, nm, rw.Vpfr, sig, p.Item3, dirSign * p.Item2).Item1, rw.Raw) = 0 Then
                    rw.Expl.Add(Tuple.Create("P", p.Item1))
                    GoTo NextPerturb
                End If
            Next
NextPerturb:
        Next

        ' H: the run started in the previous UTC hour
        If RsNearHourStart(rw.Csv.Timestamp) Then
            Dim ph As Integer = (hour + 23) Mod 24
            If RsMask(RsScore(rw, era, ph, rw.NormsPrevHour(era.NormsKey), rw.Vpfr, sig, Nothing, 0).Item1, rw.Raw) = 0 Then
                rw.Expl.Add(Tuple.Create("H", "run started in hour " & ph.ToString(Inv)))
            End If
        End If

        ' F: the live forming bar carried volume. Rebuild it from the final exchange bar: open = the
        ' final open, close = the logged Price, volume = a share of the final volume. Accepted only
        ' when the variant still reproduces the four logged VPFR fields.
        For Each formVar In RsFormingVariants(rw, cfg)
            If RsMask(RsScore(rw, era, hour, nm, formVar.Item2, formVar.Item2.Signal, Nothing, 0).Item1, rw.Raw) = 0 Then
                rw.Expl.Add(Tuple.Create("F", formVar.Item1 & ", label " & sig & " -> " & formVar.Item2.Signal))
                Exit For
            End If
        Next

        ' V: another VPFR label
        For Each alt In {"NEUTRAL", "NEAR_HVN_SUPPORT", "NEAR_HVN_RESIST"}
            If RsVote(alt) = RsVote(sig) Then Continue For
            If RsMask(RsScore(rw, era, hour, nm, rw.Vpfr, alt, Nothing, 0).Item1, rw.Raw) = 0 Then
                rw.Expl.Add(Tuple.Create("V", verified & ", vote " & RsVote(sig) & " -> " & RsVote(alt)))
                Exit For
            End If
        Next

        ' N: the volume thresholds
        For kind As Integer = 0 To 2
            If RsMask(RsScore(rw, era, hour, RsNormsVariant(nm, kind), rw.Vpfr, sig, Nothing, 0).Item1, rw.Raw) = 0 Then
                rw.Expl.Add(Tuple.Create("N", RsVolumeState(rw, era) & " -> " & {"none", "mid partial", "full"}(kind)))
                Exit For
            End If
        Next

        ' VN: both
        If Not rw.Expl.Any(Function(e) e.Item1 = "V" OrElse e.Item1 = "N") Then
            For Each alt In {"NEUTRAL", "NEAR_HVN_SUPPORT", "NEAR_HVN_RESIST"}
                If RsVote(alt) = RsVote(sig) Then Continue For
                Dim hit As Boolean = False
                For kind As Integer = 0 To 2
                    If RsMask(RsScore(rw, era, hour, RsNormsVariant(nm, kind), rw.Vpfr, alt, Nothing, 0).Item1, rw.Raw) = 0 Then
                        rw.Expl.Add(Tuple.Create("VN", verified))
                        hit = True
                        Exit For
                    End If
                Next
                If hit Then Exit For
            Next
        End If

        ' B: the burst modifier not applied
        If RsS(rw.Raw, "AggrVelSignal").StartsWith("BURST_", StringComparison.Ordinal) Then
            If RsMask(RsScore(rw, era, hour, nm, rw.Vpfr, sig, Sub(r, d, c) r.AggrVelSignal = "NORMAL", 0).Item1, rw.Raw) = 0 Then
                rw.Expl.Add(Tuple.Create("B", RsS(rw.Raw, "AggrVelSignal") & " with TFI " & RsS(rw.Raw, "TFISignal")))
            End If
        End If

        ' EA / E: another settings era
        For ei As Integer = 0 To eras.Count - 1
            If ei = rw.Era Then Continue For
            Dim e = eras(ei)
            If RsMask(RsScore(rw, e, hour, rw.Norms(e.NormsKey), rw.Vpfr, sig, Nothing, 0).Item1, rw.Raw) <> 0 Then Continue For
            Dim adjacent As Boolean =
                (ei = rw.Era - 1 AndAlso (rw.Csv.Timestamp - era.Edge).TotalHours <= 72.0) OrElse
                (ei = rw.Era + 1 AndAlso (e.Edge - rw.Csv.Timestamp).TotalHours <= 72.0)
            rw.Expl.Add(Tuple.Create(If(adjacent, "EA", "E"), String.Format(Inv, "v{0} instead of v{1}", e.Version, era.Version)))
        Next
    End Sub

    ''' <summary>Forming-bar variants for kind F: 249 closed bars plus a rebuilt forming bar, volume share
    ''' in {elapsed share of the bar, 0.25, 0.5, 1.0}, high/low from open and Price or from the final bar.
    ''' Returns only variants that reproduce the four logged VPFR fields.</summary>
    Private Function RsFormingVariants(rw As RsRow, cfg As EngineSettings) As List(Of Tuple(Of String, PgVpfr))
        Dim outList As New List(Of Tuple(Of String, PgVpfr))()
        If rw.IForm < 0 Then Return outList
        Dim fin = rw.Bars.Bars(rw.IForm)
        Dim price As Double = rw.Csv.Price
        Dim resMs As Long = Math.Max(1, rw.Csv.ExecResolution) * 60000L
        Dim elapsed As Double = Math.Min(1.0, Math.Max(0.0, (ToMs(rw.Csv.Timestamp) - rw.FormOpen) / CDbl(resMs)))
        Dim shares As New List(Of Double) From {elapsed, 0.25, 0.5, 1.0}
        Dim vc = cfg.Indicators.VPFR
        For Each share In shares.Distinct()
            For Each hlMode In {0, 1}
                Dim hi As Double = If(hlMode = 0, Math.Max(fin.Open, price), Math.Max(fin.High, price))
                Dim lo As Double = If(hlMode = 0, Math.Min(fin.Open, price), Math.Min(fin.Low, price))
                Dim bar As New Candle With {.Timestamp = rw.FormOpen, .Open = fin.Open, .High = hi, .Low = lo, .Close = price,
                                            .Volume = fin.Volume * share, .VolumeUSD = fin.VolumeUSD * share}
                Dim candles As New List(Of Candle)(250)
                For i As Integer = rw.IPrev - 248 To rw.IPrev
                    candles.Add(rw.Bars.Bars(i))
                Next
                candles.Add(bar)
                Dim x As New PgVpfr With {.Window = candles}
                Dim vols As Double() = Nothing, bLow As Double = 0, bSize As Double = 0
                IndicatorEngine.CalcVPFRLite(candles, price, x.Poc, x.NearPoc, x.Signal, x.Vah, x.Val, x.VaSignal,
                                             x.HvnAbove, x.HvnBelow, x.LvnAbove, x.LvnBelow, vols, bLow, bSize,
                                             numBuckets:=vc.NumBuckets, hvnVolPct:=vc.HvnVolPct, lvnVolPct:=vc.LvnVolPct,
                                             hvnProximityPct:=vc.HvnProximityPct, decayBase:=vc.DecayBase, valueAreaPct:=vc.ValueAreaPct)
                Dim ok As Boolean = Math.Abs(x.Vah - rw.LoggedVah) <= PgTol AndAlso Math.Abs(x.Val - rw.LoggedVal) <= PgTol AndAlso
                                    Math.Abs(x.HvnAbove - rw.Csv.VpfrNearestHvnAbove) <= PgTol AndAlso Math.Abs(x.HvnBelow - rw.Csv.VpfrNearestHvnBelow) <= PgTol
                If Not ok OrElse x.Signal = rw.Vpfr.Signal Then Continue For
                x.WindowVariant = 99
                outList.Add(Tuple.Create(String.Format(Inv, "volume share {0:0.00}, high/low {1}", share, If(hlMode = 0, "open and Price", "final bar")), x))
            Next
        Next
        Return outList
    End Function

    Private Function RsVote(sig As String) As String
        If sig = "NEAR_HVN_SUPPORT" OrElse sig = "IN_LVN_BULL" Then Return "LONG"
        If sig = "NEAR_HVN_RESIST" OrElse sig = "IN_LVN_BEAR" Then Return "SHORT"
        Return "none"
    End Function

    Private Function RsVolumeState(rw As RsRow, era As RsEra) As String
        Dim n = rw.Norms(era.NormsKey)
        Dim ratio As Double = RsD(rw.Raw, "VolumeRatio")
        If ratio >= n.VolHighThreshold Then Return "full"
        If ratio >= n.VolMidThreshold Then Return "mid partial"
        Return "none"
    End Function

    Private Function RsNearHourStart(ts As DateTime) As Boolean
        Return ts.Minute = 0 AndAlso ts.Second <= 10
    End Function

    Private Function RsMask(v As VerdictResult, x As RsRaw) As Integer
        Dim m As Integer = 0
        If v.LongScore <> RsI(x, "LongScore") Then m = m Or 1
        If v.ShortScore <> RsI(x, "ShortScore") Then m = m Or 2
        If v.EffectiveLongScore <> RsI(x, "EffectiveLongScore") Then m = m Or 4
        If v.EffectiveShortScore <> RsI(x, "EffectiveShortScore") Then m = m Or 8
        If v.RegimePenalty <> RsI(x, "RegimePenalty") Then m = m Or 16
        If Not String.Equals(If(v.Verdict, ""), RsS(x, "Verdict"), StringComparison.Ordinal) Then m = m Or 32
        Return m
    End Function

    Private Function RsMaskText(mask As Integer) As String
        Return String.Join("+", Enumerable.Range(0, RsSixNames.Length).Where(Function(b) (mask And (1 << b)) <> 0).Select(Function(b) RsSixNames(b)))
    End Function

    Private Function RsDetailKey(detail As String) As String
        ' Group the details table by the stable part: drop per-row numbers after the first comma.
        Dim i As Integer = detail.IndexOf(","c)
        Return If(i > 0, detail.Substring(0, i), detail)
    End Function

    Private Function RsSecondaryMask(v As VerdictResult, x As RsRaw, r As IndicatorResults, cfg As EngineSettings) As Integer
        Dim m As Integer = 0
        If v.MaxScore <> RsI(x, "MaxScore") Then m = m Or 1
        If Not String.Equals(If(v.VerdictContext, "CONFIRMED"), RsS(x, "VerdictContext"), StringComparison.Ordinal) Then m = m Or 2
        If Not String.Equals(If(v.OiCvdOutcome, "NONE"), RsS(x, "OiCvdOutcome"), StringComparison.Ordinal) Then m = m Or 4
        Dim pl = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim ps = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
        If Math.Abs(pl.Target - RsD(x, "PlacedTargetLong")) > PgTol OrElse Math.Abs(pl.StopPx - RsD(x, "PlacedStopLong")) > PgTol OrElse
           Math.Abs(ps.Target - RsD(x, "PlacedTargetShort")) > PgTol OrElse Math.Abs(ps.StopPx - RsD(x, "PlacedStopShort")) > PgTol Then m = m Or 8
        Return m
    End Function

    Private Function RsNormsVariant(n As DynamicNorms, kind As Integer) As DynamicNorms
        Dim c As New DynamicNorms With {
            .VolHighThreshold = n.VolHighThreshold, .VolMidThreshold = n.VolMidThreshold, .VolMean = n.VolMean, .VolStdDev = n.VolStdDev,
            .VWAPDevThreshold = n.VWAPDevThreshold, .ATRScaleFactor = n.ATRScaleFactor, .ATRRef = n.ATRRef, .IsLive = n.IsLive}
        Select Case kind
            Case 0 : c.VolHighThreshold = Double.MaxValue : c.VolMidThreshold = Double.MaxValue
            Case 1 : c.VolHighThreshold = Double.MaxValue : c.VolMidThreshold = 0.0
            Case Else : c.VolHighThreshold = 0.0 : c.VolMidThreshold = 0.0
        End Select
        Return c
    End Function

    Private Function RsNormsKey(c As EngineSettings) As String
        Dim v = c.Indicators.Volume, w = c.Indicators.VWAPDynamic, a = c.Indicators.ATR
        Dim vals As Double() = {v.StaticHigh, v.StaticMid, v.DynamicHighClampMin, v.DynamicHighClampMax, v.DynamicMidClampMin, v.DynamicMidClampMax,
                                w.DevClampMin, w.DevClampMax, w.StaticFallback, a.Period, a.ScaleMin, a.ScaleMax, a.StaticRef}
        Dim sb As New StringBuilder(String.Join("|", vals.Select(Function(x) x.ToString("R", Inv))))
        sb.Append("|SV:").Append(c.SessionVolume.Enabled.ToString())
        If c.SessionVolume.Sessions IsNot Nothing Then
            For Each b In c.SessionVolume.Sessions
                sb.Append("|").Append(b.Name).Append(":").Append(b.StartHour.ToString(Inv)).Append("-").Append(b.EndHour.ToString(Inv))
                sb.Append(":").Append(b.HighMultiplier.ToString("R", Inv)).Append(":").Append(b.MidMultiplier.ToString("R", Inv))
            Next
        End If
        Return sb.ToString()
    End Function

    Private Function RsPerturbations() As List(Of Tuple(Of String, Double, RsPerturb))
        Dim l As New List(Of Tuple(Of String, Double, RsPerturb))()
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("Price (F2)", 0.005, Sub(r, d, c) r.CurrentPrice += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("ROC (F4)", 0.00005, Sub(r, d, c) r.ROC += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("BBW (F4)", 0.00005, Sub(r, d, c) r.BBW += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("TTMHistogram (F4)", 0.00005, Sub(r, d, c) r.TTMHistogram += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("EMA9 (F2)", 0.005, Sub(r, d, c) r.EMA9 += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("EMA21 (F2)", 0.005, Sub(r, d, c) r.EMA21 += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("EMA50 (F2)", 0.005, Sub(r, d, c) r.EMA50 += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("OIChange15m (F4)", 0.00005, Sub(r, d, c) r.OIChange15m += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("OIChange60m (F4)", 0.00005, Sub(r, d, c) r.OIChange60m += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("OFIRatio (F4)", 0.00005, Sub(r, d, c) r.OFIRatio += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("TFIValue (F4)", 0.00005, Sub(r, d, c) r.TFIValue += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("MicroCVDEarly (F0)", 0.5, Sub(r, d, c) r.MicroCVDEarly += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("MicroCVDMid (F0)", 0.5, Sub(r, d, c) r.MicroCVDMid += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("MicroCVDLate (F0)", 0.5, Sub(r, d, c) r.MicroCVDLate += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("DonchianUpper (F2)", 0.005, Sub(r, d, c) r.DonchianUpper += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("DonchianLower (F2)", 0.005, Sub(r, d, c) r.DonchianLower += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("RSI (F2)", 0.005, Sub(r, d, c) r.RSI += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("PlusDI (F2)", 0.005, Sub(r, d, c) r.PlusDI += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("MinusDI (F2)", 0.005, Sub(r, d, c) r.MinusDI += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("ADX (F2)", 0.005, Sub(r, d, c) r.ADX += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VolumeRatio (F4)", 0.00005, Sub(r, d, c) r.VolumeRatio += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VWAP (F2)", 0.005, Sub(r, d, c) r.VWAP += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VWAPSigma1Upper (F2)", 0.005, Sub(r, d, c) r.VWAPSigma1Upper += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VWAPSigma1Lower (F2)", 0.005, Sub(r, d, c) r.VWAPSigma1Lower += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VWAPSigma2Upper (F2)", 0.005, Sub(r, d, c) r.VWAPSigma2Upper += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VWAPSigma2Lower (F2)", 0.005, Sub(r, d, c) r.VWAPSigma2Lower += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("EMA200_5m (F2)", 0.005, Sub(r, d, c) r.EMA200_5m += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("FundingRate (F8)", 0.000000005, Sub(r, d, c) r.FundingRate += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("CVDValue (F0)", 0.5, Sub(r, d, c) r.CVDValue += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("SpreadBps (F4)", 0.00005,
            Sub(r, d, c)
                r.SpreadBps += d
                r.SpreadStatus = IndicatorEngine.ClassifySpread(r.SpreadBps, c.Indicators.Spread.WideThresholdBps, c.Indicators.Spread.TightThresholdBps)
            End Sub))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("ATR (F4)", 0.00005, Sub(r, d, c) r.ATR += d))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("SwingTargetLong (F2)", 0.005, Sub(r, d, c) r.SwingTargetLong += If(r.SwingTargetLong > 0, d, 0.0)))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("SwingTargetShort (F2)", 0.005, Sub(r, d, c) r.SwingTargetShort += If(r.SwingTargetShort > 0, d, 0.0)))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VPFRNearestHvnAbove (F2)", 0.005, Sub(r, d, c) r.VPFRNearestHvnAbove += If(r.VPFRNearestHvnAbove > 0, d, 0.0)))
        l.Add(Tuple.Create(Of String, Double, RsPerturb)("VPFRNearestHvnBelow (F2)", 0.005, Sub(r, d, c) r.VPFRNearestHvnBelow += If(r.VPFRNearestHvnBelow > 0, d, 0.0)))
        Return l
    End Function

    Private Function RsProvenance() As List(Of Tuple(Of String, String))
        Dim l As New List(Of Tuple(Of String, String))()
        l.Add(Tuple.Create("CurrentPrice", "logged `Price` (F2)"))
        l.Add(Tuple.Create("ROC, RSI, PlusDI, MinusDI, ADX, VolumeRatio", "logged, same names (F4 / F2)"))
        l.Add(Tuple.Create("ROCSlope, RSIDivergence, Regime, SqueezeStatus, TTMSignal, TTMDirection, EMAAlignment, FundingBias, FundingMomentum, OISignal, OFISignal, OFIMomentum, CVDSlope, CVDDivergence, TFISignal, AggrVelSignal, MicroCVDSignal, MicroCVDMomentum, LiqSignal, DonchianSignal, OBVTrend, OBVDivergence, MTF15mTrend", "logged labels, same names"))
        l.Add(Tuple.Create("TrendStructure", "logged `TrendStructure5m`"))
        l.Add(Tuple.Create("MTFGatePassLong, MTFGatePassShort", "logged True / False"))
        l.Add(Tuple.Create("VWAP, VWAPSigma1/2 Upper/Lower, VWAPSessionCandles, EMA200_5m", "logged (F2 / integer)"))
        l.Add(Tuple.Create("FundingRate", "logged (F8)"))
        l.Add(Tuple.Create("CVDValue, MicroCVDEarly/Mid/Late", "logged (F0)"))
        l.Add(Tuple.Create("LiqLongSize, LiqShortSize", "logged (F2); all zero with LiqSignal NONE (finding L-1)"))
        l.Add(Tuple.Create("ATR, swing levels, VPFRNearestHvnAbove/Below, BestPivotByVolume5m", "logged (F4 / F2); read by the placed-level arbitration and Step 5c"))
        l.Add(Tuple.Create("ExecResolution", "logged integer"))
        l.Add(Tuple.Create("SpreadStatus", "DERIVED: shipped `ClassifySpread` on logged `SpreadBps` with the era's thresholds"))
        l.Add(Tuple.Create("RocMagnitudeThreshold", "DERIVED: shipped `ExecutionResolution.ResolveRocMagnitudeForHour` on the row's UTC hour"))
        l.Add(Tuple.Create("SessionUtcHour", "DERIVED: the logged timestamp's hour (boundary rows tested with the previous hour)"))
        l.Add(Tuple.Create("VPFRSignal, VPFRPoc", "RECOMPUTED: shipped `CalcVPFRLite` on exchange candles, verified against four logged VPFR fields"))
        l.Add(Tuple.Create("DynamicNorms (VolHighThreshold, VolMidThreshold)", "RECOMPUTED: shipped `DynamicNorms.Compute` on the same candle window under the era's settings"))
        l.Add(Tuple.Create("VPFRValueAreaSignal, VPFRNearestLvnAbove/Below", "RECOMPUTED with VPFRSignal (value-area scoring is disabled in every era: section 2)"))
        l.Add(Tuple.Create("LastTwoHighs5m, LastTwoLows5m, MTFGateDetails", "NOT SET: read only into breakdown notes and MTFGateReason text, never into points"))
        Return l
    End Function

    ' ------------------------------------------------------------------ IndicatorResults from a logged row

    Private Function RsIndicators(rw As RsRow, cfg As EngineSettings, hour As Integer, vp As PgVpfr, vpfrSignal As String) As IndicatorResults
        Dim x = rw.Raw
        Dim r As New IndicatorResults()
        r.CurrentPrice = RsD(x, "Price")
        r.ROC = RsD(x, "ROC") : r.ROCSlope = RsS(x, "ROCSlope")
        r.RSI = RsD(x, "RSI") : r.RSIDivergence = RsS(x, "RSIDivergence")
        r.ATR = RsD(x, "ATR") : r.ATRSizeMultiplier = RsD(x, "ATRMultiplier")
        r.VolumeRatio = RsD(x, "VolumeRatio")
        r.PlusDI = RsD(x, "PlusDI") : r.MinusDI = RsD(x, "MinusDI") : r.ADX = RsD(x, "ADX") : r.Regime = RsS(x, "Regime")
        r.VWAP = RsD(x, "VWAP") : r.VWAPDevPct = RsD(x, "VWAPDevPct") : r.VWAPSessionCandles = RsI(x, "VWAPSessionCandles")
        r.VWAPSigma1Upper = RsD(x, "VWAPSigma1Upper") : r.VWAPSigma1Lower = RsD(x, "VWAPSigma1Lower")
        r.VWAPSigma2Upper = RsD(x, "VWAPSigma2Upper") : r.VWAPSigma2Lower = RsD(x, "VWAPSigma2Lower")
        r.BBW = RsD(x, "BBW") : r.SqueezeStatus = RsS(x, "SqueezeStatus")
        r.TTMHistogram = RsD(x, "TTMHistogram") : r.TTMDirection = RsS(x, "TTMDirection") : r.TTMSignal = RsS(x, "TTMSignal")
        r.EMA9 = RsD(x, "EMA9") : r.EMA21 = RsD(x, "EMA21") : r.EMA50 = RsD(x, "EMA50") : r.EMAAlignment = RsS(x, "EMAAlignment")
        r.EMA200_5m = RsD(x, "EMA200_5m") : r.PriceVsEMA200 = RsS(x, "PriceVsEMA200")
        r.FundingRate = RsD(x, "FundingRate") : r.FundingBias = RsS(x, "FundingBias") : r.FundingMomentum = RsS(x, "FundingMomentum") : r.FundingDelta = RsD(x, "FundingDelta")
        r.OI_Current = RsD(x, "OI_Current") : r.OIChange15m = RsD(x, "OIChange15m") : r.OIChange60m = RsD(x, "OIChange60m") : r.OISignal = RsS(x, "OISignal")
        r.OFIRatio = RsD(x, "OFIRatio") : r.OFIBidVol = RsD(x, "OFIBidVol") : r.OFIAskVol = RsD(x, "OFIAskVol")
        r.OFISignal = RsS(x, "OFISignal") : r.OFIMomentum = RsS(x, "OFIMomentum")
        r.SpreadBps = RsD(x, "SpreadBps")
        r.SpreadStatus = IndicatorEngine.ClassifySpread(r.SpreadBps, cfg.Indicators.Spread.WideThresholdBps, cfg.Indicators.Spread.TightThresholdBps)
        r.LiqLongSize = RsD(x, "LiqLongSize") : r.LiqShortSize = RsD(x, "LiqShortSize") : r.LiqSignal = RsS(x, "LiqSignal")
        r.CVDValue = RsD(x, "CVDValue") : r.CVDSlope = RsS(x, "CVDSlope") : r.CVDDivergence = RsS(x, "CVDDivergence")
        r.CVDWeightedSlope = RsD(x, "CVDWeightedSlope")
        r.TFIValue = RsD(x, "TFIValue") : r.TFISignal = RsS(x, "TFISignal")
        r.AggrVelBurstRatio = RsDN(x, "AggrVelBurstRatio") : r.AggrVelNet = RsDN(x, "AggrVelNet") : r.AggrVelSignal = RsS(x, "AggrVelSignal")
        r.MicroCVDEarly = RsD(x, "MicroCVDEarly") : r.MicroCVDMid = RsD(x, "MicroCVDMid") : r.MicroCVDLate = RsD(x, "MicroCVDLate")
        r.MicroCVDMomentum = RsS(x, "MicroCVDMomentum") : r.MicroCVDSignal = RsS(x, "MicroCVDSignal")
        r.MTF15mTrend = RsS(x, "MTF15mTrend") : r.MTF15mADX = RsD(x, "MTF15mADX") : r.MTF15mEMAAlignment = RsS(x, "MTF15mEMAAlignment")
        r.MTFGatePassLong = RsB(x, "MTFGatePassLong") : r.MTFGatePassShort = RsB(x, "MTFGatePassShort") : r.MTFGateDetails = ""
        r.DonchianUpper = RsD(x, "DonchianUpper") : r.DonchianLower = RsD(x, "DonchianLower") : r.DonchianSignal = RsS(x, "DonchianSignal")
        r.OBVTrend = RsS(x, "OBVTrend") : r.OBVDivergence = RsS(x, "OBVDivergence")
        r.VPFRVah = RsD(x, "VPFRVAH") : r.VPFRVal = RsD(x, "VPFRVAL")
        r.VPFRNearestHvnAbove = RsD(x, "VPFRNearestHvnAbove") : r.VPFRNearestHvnBelow = RsD(x, "VPFRNearestHvnBelow")
        r.VPFRPoc = vp.Poc : r.VPFRHVNearPoc = vp.NearPoc : r.VPFRSignal = vpfrSignal
        r.VPFRNearestLvnAbove = vp.LvnAbove : r.VPFRNearestLvnBelow = vp.LvnBelow
        r.VPFRValueAreaSignal = vp.VaSignal
        r.LastSwingHigh5m = RsD(x, "LastSwingHigh5m") : r.LastSwingLow5m = RsD(x, "LastSwingLow5m")
        r.LastSwingHigh15m = RsD(x, "LastSwingHigh15m") : r.LastSwingLow15m = RsD(x, "LastSwingLow15m")
        r.SwingTargetLong = RsD(x, "SwingTargetLong") : r.SwingTargetShort = RsD(x, "SwingTargetShort")
        r.SwingStopLong = RsD(x, "SwingStopLong") : r.SwingStopShort = RsD(x, "SwingStopShort")
        r.BestPivotByVolume5m = RsD(x, "BestPivotByVolume5m") : r.BestPivotVolumeRatio5m = RsD(x, "BestPivotVolumeRatio5m")
        Dim ts As TrendStructure
        If [Enum].TryParse(Of TrendStructure)(RsS(x, "TrendStructure5m"), True, ts) Then r.TrendStructure = ts
        r.ExecResolution = Math.Max(1, RsI(x, "ExecResolution"))
        r.SessionUtcHour = hour
        r.RocMagnitudeThreshold = ExecutionResolution.ResolveRocMagnitudeForHour(cfg, hour)
        r.AbsorptionSignal = If(RsS(x, "AbsorptionSignal") = "", "NONE", RsS(x, "AbsorptionSignal"))
        Return r
    End Function

    ' ------------------------------------------------------------------ attribution file

    Private Sub RsWriteAttribution(path As String, rows As List(Of RsRow), eras As List(Of RsEra))
        Dim sb As New StringBuilder()
        sb.Append("Timestamp,InstanceId,Session,Regime,InPopulation,Era,VpfrVerified,VpfrSignal,LoggedVerdict,RescoredVerdict,MatchSix,MismatchFields,Class,ClassDetail,")
        sb.Append("LongScore,ShortScore,EffectiveLongScore,EffectiveShortScore,MaxScore,RegimePenalty")
        For Each lk In RsLabelKeys
            sb.Append(",").Append(lk.Item2).Append("_L,").Append(lk.Item2).Append("_S")
        Next
        sb.AppendLine(",Mutations")
        For Each rw In rows
            Dim v = rw.V
            Dim pts As New Dictionary(Of String, Tuple(Of Integer, Integer))(StringComparer.Ordinal)
            For Each it In v.SignalBreakdown
                If it Is Nothing Then Continue For
                Dim key As String = Nothing
                For Each lk In RsLabelKeys
                    If it.Label = lk.Item1 OrElse (lk.Item1 = "ADX>" AndAlso it.Label.StartsWith("ADX>", StringComparison.Ordinal)) Then key = lk.Item2 : Exit For
                Next
                If key Is Nothing Then Continue For
                pts(key) = Tuple.Create(it.LongPoints, it.ShortPoints)
            Next
            sb.Append(rw.Csv.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(",").Append(rw.Raw.Inst).Append(",").Append(rw.Session).Append(",")
            sb.Append(RsS(rw.Raw, "Regime")).Append(",").Append(If(rw.InPopulation, "1", "0")).Append(",v").Append(eras(rw.Era).Version.ToString(Inv)).Append(",")
            sb.Append(If(rw.Vpfr.WindowVariant >= 0, "1", "0")).Append(",").Append(rw.Vpfr.Signal).Append(",")
            sb.Append(RsS(rw.Raw, "Verdict")).Append(",").Append(v.Verdict).Append(",").Append(If(rw.Mask = 0, "1", "0")).Append(",")
            sb.Append(RsMaskText(rw.Mask)).Append(",").Append(rw.Cls).Append(",").Append(rw.Detail.Replace(",", ";")).Append(",")
            sb.Append(v.LongScore.ToString(Inv)).Append(",").Append(v.ShortScore.ToString(Inv)).Append(",").Append(v.EffectiveLongScore.ToString(Inv)).Append(",")
            sb.Append(v.EffectiveShortScore.ToString(Inv)).Append(",").Append(v.MaxScore.ToString(Inv)).Append(",").Append(v.RegimePenalty.ToString(Inv))
            For Each lk In RsLabelKeys
                Dim t As Tuple(Of Integer, Integer) = Nothing
                If pts.TryGetValue(lk.Item2, t) Then
                    sb.Append(",").Append(t.Item1.ToString(Inv)).Append(",").Append(t.Item2.ToString(Inv))
                Else
                    sb.Append(",,")
                End If
            Next
            sb.Append(",").AppendLine(String.Join(";", RsMutations(v)))
        Next
        File.WriteAllText(path, sb.ToString(), New UTF8Encoding(False))
    End Sub

    ''' <summary>Mutation flags read from the breakdown notes and points of the re-score.</summary>
    Private Function RsMutations(v As VerdictResult) As List(Of String)
        Dim f As New List(Of String)()
        For Each it In v.SignalBreakdown
            If it Is Nothing Then Continue For
            Dim n As String = If(it.Note, "")
            Dim lbl As String = it.Label
            Dim upKey As String = Nothing
            Select Case lbl
                Case "ROC(9)" : upKey = "ROC"
                Case "RSI(9)" : upKey = "RSI"
                Case "Volume" : upKey = "VOL"
                Case "VWAP" : upKey = "VWAP"
                Case "OI Delta" : upKey = "OI"
                Case "Donchian(20)" : upKey = "DONCHIAN"
                Case "OBV" : upKey = "OBV"
            End Select
            If upKey IsNot Nothing Then
                If n.Contains("PARTIAL->UPGRADED [L]") Then f.Add(upKey & "_UPGRADE_L")
                If n.Contains("PARTIAL->UPGRADED [S]") Then f.Add(upKey & "_UPGRADE_S")
            End If
            Select Case lbl
                Case "RSI(9)"
                    If n.Contains("PENALTY -1 [L]") Then f.Add("RSI_DIV_PEN_L")
                    If n.Contains("PENALTY -1 [S]") Then f.Add("RSI_DIV_PEN_S")
                Case "BBW/TTM"
                    If n.Contains("ACTIVE -- penalty") Then f.Add("SQUEEZE_PEN")
                    If it.LongHit Then f.Add("TTM_BUILD_L")
                    If it.ShortHit Then f.Add("TTM_BUILD_S")
                Case "OFI"
                    If n.Contains("confirmed") Then f.Add(If(n.Contains("[L]"), "OFI_MOM_CONFIRM_L", "OFI_MOM_CONFIRM_S"))
                    If n.Contains("suppressed") Then f.Add(If(n.Contains("[L]"), "OFI_MOM_SUPPRESS_L", "OFI_MOM_SUPPRESS_S"))
                Case "CVD"
                    If n.Contains("PENALTY -") Then f.Add(If(n.Contains("Div:BEARISH"), "CVD_DIV_PEN_L", "CVD_DIV_PEN_S"))
                Case "TFI"
                    If n.Contains("BURST_BUY +") Then f.Add("BURST_CONFIRM_L")
                    If n.Contains("BURST_SELL +") Then f.Add("BURST_CONFIRM_S")
                    If n.Contains("BURST_SELL -") Then f.Add("BURST_CONTRA_L")
                    If n.Contains("BURST_BUY -") Then f.Add("BURST_CONTRA_S")
                Case "MicroCVD"
                    If n.Contains("opposing") Then f.Add(If(n.Contains("BULL_DECEL"), "DECEL_PEN_S", "DECEL_PEN_L"))
                    If n.Contains("STALL PENALTY") Then f.Add(If(n.Contains("STALL PENALTY -1 [L]") OrElse n.Contains("[L] (price>VWAP"), "STALL_PEN_L", "STALL_PEN_S"))
                Case "Liq Penalty"
                    If it.LongHit Then f.Add("LIQ_PEN_L")
                    If it.ShortHit Then f.Add("LIQ_PEN_S")
                Case "Spread"
                    If it.LongHit Then f.Add("SPREAD_PEN_L")
                    If it.ShortHit Then f.Add("SPREAD_PEN_S")
                Case "OI Delta"
                    If n.Contains("PASS2b: +") Then f.Add(If(n.Contains("+1[L]") OrElse n.Contains("[L] OI"), "OICVD_CONFIRM_L", "OICVD_CONFIRM_S"))
                    If n.Contains("PASS2b: -") Then f.Add(If(n.Contains("[L] OI"), "OICVD_CONFLICT_L", "OICVD_CONFLICT_S"))
                Case "Regime Align (2c)"
                    If n.Contains("REGIME ALIGN") Then f.Add(If(it.LongHit, "P2C_ALIGN_L", "P2C_ALIGN_S"))
                    If n.Contains("REGIME CONFLICT") Then f.Add(If(it.LongPoints < 0, "P2C_CONFLICT_L", If(it.ShortPoints < 0, "P2C_CONFLICT_S", "P2C_CONFLICT_CLAMPED")))
                Case "Trend Structure"
                    If it.LongHit Then f.Add("TS_BONUS_L")
                    If it.ShortHit Then f.Add("TS_BONUS_S")
                Case "Funding (info)"
                    For Each seg In n.Split("|"c)
                        Dim s As String = seg.Trim()
                        If (s.StartsWith("STEP3:", StringComparison.Ordinal) OrElse s.StartsWith("STEP3b:", StringComparison.Ordinal)) AndAlso
                           Not s.EndsWith("none", StringComparison.Ordinal) AndAlso Not s.EndsWith("disabled", StringComparison.Ordinal) Then
                            f.Add(s.Replace(" ", ""))
                        End If
                    Next
            End Select
        Next
        Return f
    End Function

    ' ------------------------------------------------------------------ raw CSV access by header name

    Private Sub RsLoadRaw(path As String, dict As Dictionary(Of DateTime, RsRaw))
        Dim col As Dictionary(Of String, Integer) = Nothing
        For Each line In File.ReadLines(path)
            Dim p = line.Split(","c)
            If col Is Nothing Then
                col = New Dictionary(Of String, Integer)(StringComparer.Ordinal)
                For i As Integer = 0 To p.Length - 1
                    col(p(i).Trim()) = i
                Next
                Continue For
            End If
            If p.Length < 60 Then Continue For
            Dim ts As DateTime
            If Not DateTime.TryParseExact(p(0).Trim(), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, ts) Then Continue For
            If dict.ContainsKey(ts) Then Continue For
            Dim x As New RsRaw With {.Ts = ts, .Col = col, .F = p}
            x.Inst = RsS(x, "InstanceId")
            dict(ts) = x
        Next
    End Sub

    Private Function RsS(x As RsRaw, name As String) As String
        Dim i As Integer
        If x.Col.TryGetValue(name, i) AndAlso i < x.F.Length Then Return x.F(i).Trim()
        Return ""
    End Function

    Private Function RsD(x As RsRaw, name As String) As Double
        Dim v As Double
        Double.TryParse(RsS(x, name), NumberStyles.Float, Inv, v)
        Return v
    End Function

    Private Function RsDN(x As RsRaw, name As String) As Double?
        Dim s As String = RsS(x, name)
        Dim v As Double
        If s = "" OrElse Not Double.TryParse(s, NumberStyles.Float, Inv, v) Then Return Nothing
        Return v
    End Function

    Private Function RsI(x As RsRaw, name As String) As Integer
        Dim v As Integer
        Integer.TryParse(RsS(x, name), NumberStyles.Integer, Inv, v)
        Return v
    End Function

    Private Function RsB(x As RsRaw, name As String) As Boolean
        Return String.Equals(RsS(x, name), "True", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function RsGitShow(root As String, spec As String) As String
        Dim psi As New ProcessStartInfo("git") With {
            .RedirectStandardOutput = True, .RedirectStandardError = True, .UseShellExecute = False,
            .StandardOutputEncoding = New UTF8Encoding(False)}
        psi.ArgumentList.Add("-C")
        psi.ArgumentList.Add(root)
        psi.ArgumentList.Add("show")
        psi.ArgumentList.Add(spec)
        Using p = Process.Start(psi)
            Dim text As String = p.StandardOutput.ReadToEnd()
            Dim err As String = p.StandardError.ReadToEnd()
            p.WaitForExit()
            If p.ExitCode <> 0 Then Throw New InvalidOperationException("git show " & spec & " failed: " & err.Trim() & ". STOP.")
            Return text.TrimStart(ChrW(&HFEFF))
        End Using
    End Function

End Module
