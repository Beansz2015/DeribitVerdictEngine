Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/SwingFallbackRead.vb
'
' The swing-vs-ATR-fallback target read, session 1 (join + forward walk + Part A) of
' docs/swing-vs-fallback-target-read-brief-2026-09-14.md. Vocabulary follows
' docs/DeribitIndicatorProject.md §5a (success rate, gross/net breakeven rate, gross/net
' edge, net EV per trade).
'
' READ-ONLY. Inputs: analysis_log.csv copies, analysis_eval_cache.csv, a byte-identical COPY
' of settings.json (the tracked file is never opened by SettingsLoader, so it cannot be
' written), public Deribit GETs (1-minute candles via DeribitOhlcFetcher, hourly funding
' history), cached under a gitignored folder. Output: one markdown report.
'
' Run (from the repo root):
'   dotnet run --project tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release -- \
'     --root . --fetch aws_fetch/20260913-153704 \
'     --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
'
' Rules implemented (brief §3.1-§3.4):
'   * Population: directional verdicts with valid placed levels, inside the trading week
'     (Monday's first session open to Friday's last session close, from session_volume).
'   * Session = session_volume bucket of the signal's UTC hour. Tier = STRONG / MEDIUM / WEAK.
'   * Walk starts at the 1-minute bar that CONTAINS the signal (CloseTime = minute floor + 1).
'     Same-bar target and stop = stop hit (FailureRateMatrix.WalkBars rule).
'   * Window-bound: session windows from AnalysisConstants.HoldWindowsForResolution(session
'     execution_resolution); a timeout is marked to the window-close bar close.
'   * Carried: first touch; cap = earlier of 24 h and Friday's session close (UNRESOLVED is
'     marked to the cap-bar close); session-end variant caps at the signal's session close.
'   * Funding: continuous accrual of Deribit interest_1h over the hold, per minute.
'   * Fees: scoring.trade_costs. Maker/maker = RoundTripFeePct. Taker-stop case = maker entry,
'     taker exit on a stop hit or a marked exit, maker exit on a target hit.
'   * CIs: 95 %, cluster-robust by session-day (signals inside one session overlap heavily).
'
' Era edges below are dates of record, not settings thresholds:
'   v66 OBV scoring edge: commit fd52299 at 2026-08-10 18:06:57 UTC; the first collector process
'   after it (3be7f4c9) started 2026-08-10 18:36:01 UTC. ATR step: 2026-08-20 00:00 UTC
'   (docs/d3-asia-burst-watch-read-2026-09-14.md).

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks

Public Module SwingFallbackReadProgram

    Private ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture
    ' v51 B4b structural-first geometry (commit 9ab3f04, 2026-07-06 13:08:51 UTC) is its own dataset
    ' boundary per its change_log entry: before it, TargetCapReason and the Placed* levels follow the
    ' v50 closest-wins cap with a 2.0xATR target and 1.2xATR stop. Those rows are a different label
    ' meaning, so they are excluded and counted.
    Private ReadOnly V51Edge As New DateTime(2026, 7, 6, 13, 8, 51)
    Private ReadOnly V66Edge As New DateTime(2026, 8, 10, 18, 36, 1)
    Private ReadOnly AtrStepEdge As New DateTime(2026, 8, 20, 0, 0, 0)
    Private ReadOnly CapOrder As String() = {"none", "swing", "hvn", "poc"}
    Private ReadOnly TierOrder As String() = {"ALL", "S+M", "STRONG", "MEDIUM", "WEAK"}
    Private ReadOnly SessionOrder As String() = {"NY", "LONDON", "ASIA"}
    Private ReadOnly OutcomeNames As String() = {"OPEN", "TARGET", "STOP", "AMBIGUOUS"}

    ' ------------------------------------------------------------------ types

    Private Class Sig
        Public Ts As DateTime
        Public RowMin As DateTime
        Public Price As Double
        Public Atr As Double
        Public Verdict As String = ""
        Public Tier As String = ""
        Public Session As String = ""
        Public Cap As String = ""
        Public IsLong As Boolean
        Public ExecRes As Integer
        Public SessionRes As Integer
        Public TargetPx As Double
        Public StopPx As Double
        Public TBps As Double
        Public SBps As Double
        Public TAtr As Double
        Public SAtr As Double
        Public SwingTarget As Double
        Public HvnTarget As Double
        Public FallbackMult As Double
        Public SessionEnd As DateTime
        Public WeekEnd As DateTime
        Public Windows As Integer() = {}
        Public Cluster As String = ""
        Public Era As String = ""
        Public Res As New Dictionary(Of String, WalkResult)()
    End Class

    Private Class WalkResult
        Public Outcome As Integer          ' 0 open, 1 target, 2 stop, 3 ambiguous (= stop hit)
        Public ResolveMin As Double        ' signal -> resolving bar close (or cap), minutes
        Public MarkBps As Double           ' open only: signed mark at the last bar close
        Public FundingBps As Double        ' funding COST to the position (+ = paid)
        Public MissingBars As Integer
        Public BarsSeen As Integer
    End Class

    Private Class FeeCase
        Public MakerRt As Double
        Public TakerWin As Double
        Public TakerLoss As Double
    End Class

    Private Class CellStats
        Public N As Integer
        Public NTarget As Integer
        Public NStop As Integer
        Public NAmb As Integer
        Public NOpen As Integer
        Public SuccessPct As Double
        Public SuccessCi As Double
        Public GrossBe As Double
        Public NetBe As Double
        Public NetBeTaker As Double
        Public NetEv As Double
        Public NetEvCi As Double
        Public NetEvTaker As Double
        Public FundingBps As Double
        Public MedTAtr As Double
        Public MedSAtr As Double
        Public MeanTBps As Double
        Public MeanSBps As Double
        Public P50 As Double
        Public P90 As Double
        Public RowsWithMissing As Integer
    End Class

    Private Class FundingCurve
        Private ReadOnly _start As DateTime
        Private ReadOnly _cum As Double()
        Public MissingHours As Integer
        Public Hours As Integer

        ' Deribit get_funding_rate_history: one record per hour; the record stamped T carries
        ' interest_1h accrued over [T-1h, T). Accrual here is per minute, rate / 60.
        Public Sub New(startUtc As DateTime, endUtc As DateTime, recs As Dictionary(Of Long, Double))
            _start = HourFloor(startUtc)
            Dim minutes As Integer = CInt((HourFloor(endUtc).AddHours(1) - _start).TotalMinutes)
            _cum = New Double(minutes) {}
            Dim seenHours As New HashSet(Of Long)()
            For i As Integer = 0 To minutes - 1
                Dim m As DateTime = _start.AddMinutes(i)
                Dim hourEndMs As Long = ToMs(HourFloor(m).AddHours(1))
                Dim rate As Double = 0
                If recs.TryGetValue(hourEndMs, rate) Then
                    seenHours.Add(hourEndMs)
                Else
                    If i Mod 60 = 0 Then MissingHours += 1
                End If
                _cum(i + 1) = _cum(i) + rate / 60.0
            Next
            Hours = CInt(minutes / 60)
        End Sub

        Public Function Accrued(fromUtc As DateTime, toUtc As DateTime) As Double
            Dim a As Integer = Clamp(CInt(Math.Floor((fromUtc - _start).TotalMinutes)))
            Dim b As Integer = Clamp(CInt(Math.Floor((toUtc - _start).TotalMinutes)))
            If b <= a Then Return 0
            Return _cum(b) - _cum(a)
        End Function

        Private Function Clamp(i As Integer) As Integer
            If i < 0 Then Return 0
            If i > _cum.Length - 1 Then Return _cum.Length - 1
            Return i
        End Function
    End Class

    Private Class EvalRow
        Public Ts As DateTime
        Public Verdict As String = ""
        Public FavBar As Double
        Public AdvBar As Double
        Public Outcome As String = ""
        Public ExecRes As Integer
        Public IsBackfill As Boolean
    End Class

    ' ------------------------------------------------------------------ entry

    Public Function Main(args As String()) As Integer
        Try
            Return RunAsync(args).GetAwaiter().GetResult()
        Catch ex As Exception
            Console.Error.WriteLine("FATAL: " & ex.ToString())
            Return 2
        End Try
    End Function

    Private Async Function RunAsync(args As String()) As Task(Of Integer)
        Dim a = ParseArgs(args)
        Dim root As String = Path.GetFullPath(ArgOr(a, "root", Directory.GetCurrentDirectory()))
        Dim fetchDir As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "fetch", Path.Combine("aws_fetch", "20260913-153704"))))
        Dim pooledPath As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "pooled", Path.Combine("AWS-copybacks", "pooled-book-2026-09-09", "analysis_log_pooled.csv"))))
        Dim bakPath As String = Path.Combine(fetchDir, "analysis_log.csv.v0.7.bak")
        Dim livePath As String = Path.Combine(fetchDir, "analysis_log.csv")
        Dim evalPath As String = Path.Combine(fetchDir, "analysis_eval_cache.csv")
        Dim trackedSettings As String = Path.Combine(root, "settings.json")
        Dim cacheDir As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "cache", Path.Combine("backtest_data", "swing-fallback-read"))))
        Directory.CreateDirectory(cacheDir)
        Dim outPath As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "swing-fallback-read-output.md"))))

        Dim o As New StringBuilder()
        o.AppendLine("# SwingFallbackRead output")
        o.AppendLine()
        o.AppendLine("- Run at (UTC): " & DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv))
        o.AppendLine("- Pooled log: " & pooledPath)
        o.AppendLine("- Box logs: " & bakPath & " + " & livePath)
        o.AppendLine("- Eval cache: " & evalPath)

        ' ---- settings: load a byte-identical COPY so the tracked file is never opened for write
        Dim settingsCopy As String = Path.Combine(cacheDir, "settings.copy.json")
        File.Copy(trackedSettings, settingsCopy, overwrite:=True)
        Dim hTracked As String = FileSha256(trackedSettings)
        Dim hCopy As String = FileSha256(settingsCopy)
        If hTracked <> hCopy Then Throw New InvalidOperationException("settings copy hash mismatch")
        SettingsLoader.Initialise(settingsCopy)
        Dim cfg As EngineSettings = SettingsLoader.Current
        o.AppendLine("- settings.json version " & cfg.Version.ToString(Inv) & ", sha256 " & hTracked.Substring(0, 16) & " (copy identical)")

        Dim sessions = cfg.SessionVolume.Sessions
        For Each s In sessions
            If s.StartHour > s.EndHour Then Throw New InvalidOperationException("session " & s.Name & " wraps midnight; the trading-week rule needs a re-derivation. STOP.")
            o.AppendLine(String.Format(Inv, "- Session {0}: hours {1}-{2} UTC inclusive, execution_resolution {3}, windows {4} min, fallback target {5}xATR",
                s.Name, s.StartHour, s.EndHour, s.ExecutionResolution,
                String.Join("/", AnalysisConstants.HoldWindowsForResolution(s.ExecutionResolution)),
                ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, s.StartHour)))
        Next
        Dim weekOpenHour As Integer = sessions.Min(Function(s) s.StartHour)
        Dim weekCloseHourExcl As Integer = sessions.Max(Function(s) s.EndHour) + 1
        o.AppendLine(String.Format(Inv, "- Trading week: Monday {0:00}:00 UTC to Friday {1:00}:00 UTC (exclusive)", weekOpenHour, weekCloseHourExcl))

        Dim tc = cfg.Scoring.TradeCosts
        Dim fees As New FeeCase With {
            .MakerRt = tc.RoundTripFeePct * 10000.0,
            .TakerWin = tc.MakerFeeBps * 2.0,
            .TakerLoss = tc.MakerFeeBps + tc.TakerFeeBps
        }
        o.AppendLine(String.Format(Inv, "- Fees: style {0}, round trip {1:0.00} bps (maker {2}, taker {3}). Taker-stop case: {4:0.00} bps on a target hit, {5:0.00} bps on a stop hit or a marked exit",
            tc.RoundTripStyle, fees.MakerRt, tc.MakerFeeBps, tc.TakerFeeBps, fees.TakerWin, fees.TakerLoss))
        o.AppendLine()

        ' ---- load logs
        Dim pooled = LoadWithInstance(pooledPath)
        Dim bak = LoadWithInstance(bakPath)
        Dim live = LoadWithInstance(livePath)
        Dim collectorIds As New HashSet(Of String)(bak.Concat(live).Select(Function(x) x.Item2))
        Dim collectorStart As DateTime = bak.Concat(live).Where(Function(x) x.Item1.Timestamp <> DateTime.MinValue).Min(Function(x) x.Item1.Timestamp)

        ' ---- join diagnosis (brief §2)
        Dim evalRows = LoadEval(evalPath)
        Dim boxLog As New Dictionary(Of DateTime, CsvRow)()
        For Each x In bak.Concat(live)
            If x.Item1.Timestamp <> DateTime.MinValue Then boxLog(x.Item1.Timestamp) = x.Item1
        Next

        ' ---- merge population rows
        Dim merged As New Dictionary(Of DateTime, Tuple(Of CsvRow, String))()
        Dim nLoaded As Integer = 0, nIdentDup As Integer = 0, nConflict As Integer = 0, nUnparsed As Integer = 0
        For Each x In pooled.Concat(live)
            nLoaded += 1
            If x.Item1.Timestamp = DateTime.MinValue Then nUnparsed += 1 : Continue For
            Dim prev As Tuple(Of CsvRow, String) = Nothing
            If merged.TryGetValue(x.Item1.Timestamp, prev) Then
                If prev.Item1.Verdict = x.Item1.Verdict AndAlso prev.Item1.Price = x.Item1.Price Then nIdentDup += 1 Else nConflict += 1
                Continue For
            End If
            merged(x.Item1.Timestamp) = x
        Next

        Dim sigs As New List(Of Sig)()
        Dim nNonDir As Integer = 0, nPreV51 As Integer = 0, nConcurrent As Integer = 0, nOffWeek As Integer = 0, nBadLevels As Integer = 0, nNoSession As Integer = 0
        For Each kv In merged.OrderBy(Function(k) k.Key)
            Dim r = kv.Value.Item1
            Dim inst = kv.Value.Item2
            Dim v As String = If(r.Verdict, "").Trim().ToUpperInvariant()
            Dim isDir As Boolean = (v = "STRONG LONG" OrElse v = "LONG" OrElse v = "WEAK LONG" OrElse v = "STRONG SHORT" OrElse v = "SHORT" OrElse v = "WEAK SHORT")
            If Not isDir Then nNonDir += 1 : Continue For
            If r.Timestamp < V51Edge Then nPreV51 += 1 : Continue For
            If r.Timestamp >= collectorStart AndAlso Not collectorIds.Contains(inst) Then nConcurrent += 1 : Continue For
            If Not InTradingWeek(r.Timestamp, weekOpenHour, weekCloseHourExcl) Then nOffWeek += 1 : Continue For
            Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, r.Timestamp.Hour)
            If bucket Is Nothing Then nNoSession += 1 : Continue For
            Dim s As New Sig()
            s.Ts = r.Timestamp
            s.RowMin = New DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, r.Timestamp.Minute, 0)
            s.Price = r.Price
            s.Atr = r.ATR
            s.Verdict = v
            s.IsLong = v.Contains("LONG")
            s.Tier = If(v.StartsWith("STRONG"), "STRONG", If(v.StartsWith("WEAK"), "WEAK", "MEDIUM"))
            s.Session = bucket.Name.ToUpperInvariant()
            s.SessionRes = bucket.ExecutionResolution
            s.ExecRes = r.ExecResolution
            s.Cap = If(String.IsNullOrEmpty(r.TargetCapReason), "(blank)", r.TargetCapReason.Trim().ToLowerInvariant())
            s.TargetPx = If(s.IsLong, r.PlacedTargetLong, r.PlacedTargetShort)
            s.StopPx = If(s.IsLong, r.PlacedStopLong, r.PlacedStopShort)
            s.SwingTarget = If(s.IsLong, r.SwingTargetLong, r.SwingTargetShort)
            s.HvnTarget = If(s.IsLong, r.VpfrNearestHvnAbove, r.VpfrNearestHvnBelow)
            Dim dirSign As Double = If(s.IsLong, 1.0, -1.0)
            Dim tDist As Double = dirSign * (s.TargetPx - s.Price)
            Dim sDist As Double = dirSign * (s.Price - s.StopPx)
            If Not r.HasPlaced OrElse s.Price <= 0 OrElse s.Atr <= 0 OrElse s.TargetPx <= 0 OrElse s.StopPx <= 0 OrElse tDist <= 0 OrElse sDist <= 0 Then
                nBadLevels += 1 : Continue For
            End If
            s.TBps = tDist / s.Price * 10000.0
            s.SBps = sDist / s.Price * 10000.0
            s.TAtr = tDist / s.Atr
            s.SAtr = sDist / s.Atr
            s.FallbackMult = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, r.Timestamp.Hour)
            s.SessionEnd = r.Timestamp.Date.AddHours(bucket.EndHour + 1)
            Dim daysSinceMonday As Integer = (CInt(r.Timestamp.DayOfWeek) + 6) Mod 7
            s.WeekEnd = r.Timestamp.Date.AddDays(4 - daysSinceMonday).AddHours(weekCloseHourExcl)
            If s.SessionEnd > s.WeekEnd Then s.SessionEnd = s.WeekEnd
            s.Windows = AnalysisConstants.HoldWindowsForResolution(bucket.ExecutionResolution)
            s.Cluster = r.Timestamp.ToString("yyyy-MM-dd", Inv) & "|" & s.Session
            s.Era = EraOf(r.Timestamp, collectorStart)
            sigs.Add(s)
        Next

        ' ---- --mode liqflag: the liquidation flag and attribution evidence (LiquidationFlagCheck.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("liqflag", StringComparison.OrdinalIgnoreCase) Then
            Dim lfOut As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "liquidation-flag-output.md"))))
            Return RunLiqFlag(o, fetchDir, pooledPath, bakPath, livePath, sigs, lfOut)
        End If

        ' ---- --mode pocgate: the POC-tier gate defect measurement (PocGateDefect.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("pocgate", StringComparison.OrdinalIgnoreCase) Then
            Dim pgOut As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "poc-gate-defect-output.md"))))
            Return Await RunPocGateAsync(o, cfg, root, cacheDir, pooledPath, livePath, merged, sigs, collectorStart, collectorIds,
                                         weekOpenHour, weekCloseHourExcl, pgOut)
        End If

        ' ---- --mode rescore: the re-score reconstruction (MediumTierRescore.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("rescore", StringComparison.OrdinalIgnoreCase) Then
            Dim rsOut As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "rescore-output.md"))))
            Return Await RunRescoreAsync(o, cfg, root, cacheDir, pooledPath, livePath, settingsCopy, merged, sigs, collectorStart, collectorIds,
                                         weekOpenHour, weekCloseHourExcl, rsOut)
        End If

        ' ---- candles + funding
        Dim spanStart As DateTime = sigs.Min(Function(x) x.RowMin)
        Dim spanEnd As DateTime = sigs.Max(Function(x) x.WeekEnd)
        Dim firstMonday As DateTime = spanStart.Date.AddDays(-((CInt(spanStart.DayOfWeek) + 6) Mod 7))
        Dim lastMonday As DateTime = spanEnd.AddMinutes(-1).Date.AddDays(-((CInt(spanEnd.AddMinutes(-1).DayOfWeek) + 6) Mod 7))
        Dim covLog As New StringBuilder()
        Dim bars = Await LoadBarsAsync(cacheDir, firstMonday, lastMonday, weekOpenHour, weekCloseHourExcl, covLog)
        Dim http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DeribitVerdictEngine-SwingFallbackRead/1.0")
        Dim fund = Await LoadFundingAsync(cacheDir, spanStart, spanEnd, http, covLog)

        ' ---- walks
        For Each s In sigs
            For Each w In s.Windows
                Dim last As DateTime = s.RowMin.AddMinutes(w)
                If last > s.WeekEnd Then last = s.WeekEnd
                s.Res("W" & w.ToString(Inv)) = Walk(s, s.TargetPx, s.StopPx, s.RowMin.AddMinutes(1), last, bars, fund)
                s.Res("N" & w.ToString(Inv)) = Walk(s, s.TargetPx, s.StopPx, s.RowMin.AddMinutes(2), last, bars, fund)
            Next
            Dim c24 As DateTime = s.RowMin.AddMinutes(1440)
            If c24 > s.WeekEnd Then c24 = s.WeekEnd
            s.Res("C24") = Walk(s, s.TargetPx, s.StopPx, s.RowMin.AddMinutes(1), c24, bars, fund)
            s.Res("C24N") = Walk(s, s.TargetPx, s.StopPx, s.RowMin.AddMinutes(2), c24, bars, fund)
            Dim cse As DateTime = s.SessionEnd
            If cse < s.RowMin.AddMinutes(1) Then cse = s.RowMin.AddMinutes(1)
            s.Res("CSE") = Walk(s, s.TargetPx, s.StopPx, s.RowMin.AddMinutes(1), cse, bars, fund)
        Next

        ' ---- --mode stability: the tier-order stability check (TierOrderStability.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("stability", StringComparison.OrdinalIgnoreCase) Then
            Dim refPath As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "reference", Path.Combine("docs", "swing-vs-fallback-target-read-2026-09-15-output.md"))))
            Dim stabOut As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "tier-order-stability-output.md"))))
            Return RunStability(o, sigs, fees, refPath, stabOut)
        End If

        ' ---- --mode census: the tier-demotion census (TierDemotionCensus.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("census", StringComparison.OrdinalIgnoreCase) Then
            Dim cnOut As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "tier-demotion-census-output.md"))))
            Return RunCensus(o, cfg, sigs, fees, merged, pooledPath, livePath, collectorStart, collectorIds, weekOpenHour, weekCloseHourExcl, cnOut)
        End If

        ' ---- --mode diagexport: the MEDIUM-tier diagnosis per-row export (MediumTierDiagnosisExport.vb). Default mode unchanged.
        If ArgOr(a, "mode", "swing").Equals("diagexport", StringComparison.OrdinalIgnoreCase) Then
            Dim dxCsv As String = Path.GetFullPath(Path.Combine(root, ArgOr(a, "out", Path.Combine(cacheDir, "diagnosis-rows.csv"))))
            Return RunDiagExport(o, sigs, fees, pooledPath, livePath, dxCsv, Path.ChangeExtension(dxCsv, ".md"))
        End If

        ' ================================================================== report
        AppendJoin(o, evalRows, boxLog, sigs, bars, weekOpenHour, weekCloseHourExcl, collectorStart)

        o.AppendLine("## 2. Population funnel")
        o.AppendLine()
        o.AppendLine("| Step | Rows |")
        o.AppendLine("|---|---|")
        o.AppendLine(Row("Loaded (pooled book + box live log)", nLoaded))
        o.AppendLine(Row("Identical duplicate timestamps dropped (pooled already holds the live log to 2026-09-09)", nIdentDup))
        o.AppendLine(Row("Conflicting duplicate timestamps dropped", nConflict))
        o.AppendLine(Row("Unparsed timestamps", nUnparsed))
        o.AppendLine(Row("Non-directional verdicts", nNonDir))
        o.AppendLine(Row("Directional rows before the v51 geometry boundary (2026-07-06 13:08:51 UTC)", nPreV51))
        o.AppendLine(Row("Non-collector instance rows inside the collector era (concurrent duplicates)", nConcurrent))
        o.AppendLine(Row("Outside the trading week", nOffWeek))
        o.AppendLine(Row("No session bucket", nNoSession))
        o.AppendLine(Row("Invalid placed levels (missing, zero, or wrong side)", nBadLevels))
        o.AppendLine(Row("**Population**", sigs.Count))
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Span: {0:yyyy-MM-dd HH:mm} to {1:yyyy-MM-dd HH:mm} UTC. Collector era starts {2:yyyy-MM-dd HH:mm:ss} UTC.", sigs.Min(Function(x) x.Ts), sigs.Max(Function(x) x.Ts), collectorStart))
        o.AppendLine("- Rows whose logged ExecResolution differs from their session's configured resolution: " & sigs.Where(Function(x) x.ExecRes <> x.SessionRes).Count().ToString(Inv))
        o.AppendLine()
        o.AppendLine("| Session | Era | none | swing | hvn | poc | other |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each era In sigs.Select(Function(x) x.Era).Distinct().OrderBy(Function(x) x)
                Dim sub1 = sigs.Where(Function(x) x.Session = sess AndAlso x.Era = era).ToList()
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} |", sess, era,
                    sub1.Where(Function(x) x.Cap = "none").Count(), sub1.Where(Function(x) x.Cap = "swing").Count(),
                    sub1.Where(Function(x) x.Cap = "hvn").Count(), sub1.Where(Function(x) x.Cap = "poc").Count(),
                    sub1.Where(Function(x) Not CapOrder.Contains(x.Cap)).Count()))
            Next
        Next
        o.AppendLine()

        AppendLabelCheck(o, sigs, cfg)

        o.AppendLine("## 4. Candle and funding coverage")
        o.AppendLine()
        o.Append(covLog.ToString())
        Dim rowsMissing As Integer = sigs.Where(Function(x) x.Res.Values.Any(Function(w) w.MissingBars > 0)).Count()
        o.AppendLine("- Population rows with at least one missing bar inside any walk: " & rowsMissing.ToString(Inv))
        o.AppendLine(String.Format(Inv, "- Funding hours in span: {0}; hours with no funding record: {1}", fund.Hours, fund.MissingHours))
        o.AppendLine()

        ' ---- Part A tables
        o.AppendLine("## 6. Window-bound, main window (NY 15 min, LONDON and ASIA 45 min), maker/maker")
        o.AppendLine()
        AppendCellTable(o, sigs, Function(s) "W" & s.Windows.Max().ToString(Inv), fees, carried:=False, byTier:=True)

        o.AppendLine("## 7. Window-bound, every window, ALL tiers and STRONG+MEDIUM")
        o.AppendLine()
        o.AppendLine("| Session | Target set by | Tier | Window | n | Success rate [CI] | Stop hit % | Timeout % | Gross BE % | Net BE % | Net edge pp | Net EV bps [CI] |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            Dim wins = sigs.Where(Function(x) x.Session = sess).Select(Function(x) x.Windows).FirstOrDefault()
            If wins Is Nothing Then Continue For
            For Each cap In CapOrder
                For Each tier In {"ALL", "S+M"}
                    For Each w In wins
                        Dim key As String = "W" & w.ToString(Inv)
                        Dim rows = Filter(sigs, sess, cap, tier)
                        Dim st = Compute(rows, key, fees)
                        If st.N = 0 Then Continue For
                        o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6:0.0} | {7:0.0} | {8:0.0} | {9:0.0} | {10:+0.0;-0.0;0.0} | {11} |",
                            sess, cap, tier, w, st.N, PctCi(st), 100.0 * (st.NStop + st.NAmb) / st.N, 100.0 * st.NOpen / st.N,
                            st.GrossBe, st.NetBe, st.SuccessPct - st.NetBe, EvCi(st)))
                    Next
                Next
            Next
        Next
        o.AppendLine()

        o.AppendLine("## 8. Carried to conclusion, cap = earlier of 24 h and Friday close, maker/maker")
        o.AppendLine()
        AppendCellTable(o, sigs, Function(s) "C24", fees, carried:=True, byTier:=True)

        o.AppendLine("## 9. Carried to conclusion, cap = end of the signal's session, maker/maker")
        o.AppendLine()
        AppendCellTable(o, sigs, Function(s) "CSE", fees, carried:=True, byTier:=True)

        o.AppendLine("## 10. Side split (cells with n >= 30), ALL tiers")
        o.AppendLine()
        o.AppendLine("| Session | Target set by | Side | Mode | n | Success rate [CI] | Gross BE % | Net EV bps [CI] |")
        o.AppendLine("|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each cap In CapOrder
                For Each side In {"LONG", "SHORT"}
                    Dim rows = Filter(sigs, sess, cap, "ALL").Where(Function(x) x.IsLong = (side = "LONG")).ToList()
                    If rows.Count < 30 Then Continue For
                    For Each mode In {"main window", "carried 24 h"}
                        Dim st = If(mode = "main window",
                                    Compute(rows, "W" & rows(0).Windows.Max().ToString(Inv), fees),
                                    Compute(rows, "C24", fees))
                        o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6:0.0} | {7} |", sess, cap, side, mode, st.N, PctCi(st), st.GrossBe, EvCi(st)))
                    Next
                Next
            Next
        Next
        o.AppendLine()

        o.AppendLine("## 11. Distance-matched comparison: target distance in ATR multiples, ALL tiers")
        o.AppendLine()
        o.AppendLine("| Session | Target bucket (xATR) | Target set by | n | Med stop xATR | Main window: success [CI] | Gross BE % | Net EV bps [CI] | Carried 24 h: success [CI] | Gross BE % | Net EV bps [CI] |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
        Dim edges As Double() = {0.0, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 99.0}
        For Each sess In SessionOrder
            For bi As Integer = 0 To edges.Length - 2
                Dim lo As Double = edges(bi), hi As Double = edges(bi + 1)
                For Each cap In CapOrder
                    Dim rows = Filter(sigs, sess, cap, "ALL").Where(Function(x) Math.Round(x.TAtr, 3) >= lo AndAlso Math.Round(x.TAtr, 3) < hi).ToList()
                    If rows.Count = 0 Then Continue For
                    Dim sw = Compute(rows, "W" & rows(0).Windows.Max().ToString(Inv), fees)
                    Dim sc = Compute(rows, "C24", fees)
                    o.AppendLine(String.Format(Inv, "| {0} | {1:0.0}-{2} | {3} | {4} | {5:0.00} | {6} | {7:0.0} | {8} | {9} | {10:0.0} | {11} |",
                        sess, lo, If(hi > 50, "up", hi.ToString("0.0", Inv)), cap, sw.N, sw.MedSAtr, PctCi(sw), sw.GrossBe, EvCi(sw), PctCi(sc), sc.GrossBe, EvCi(sc)))
                Next
            Next
        Next
        o.AppendLine()

        o.AppendLine("## 12. Regime split, ALL tiers")
        o.AppendLine()
        o.AppendLine("| Session | Era | Target set by | n | Main window: success [CI] | Net EV bps [CI] | Carried 24 h: success [CI] | Gross edge pp | Net EV bps [CI] | Med ATR bps |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each era In sigs.Select(Function(x) x.Era).Distinct().OrderBy(Function(x) x)
                For Each cap In CapOrder
                    Dim rows = Filter(sigs, sess, cap, "ALL").Where(Function(x) x.Era = era).ToList()
                    If rows.Count = 0 Then Continue For
                    Dim sw = Compute(rows, "W" & rows(0).Windows.Max().ToString(Inv), fees)
                    Dim sc = Compute(rows, "C24", fees)
                    Dim medAtrBps As Double = Median(rows.Select(Function(x) x.Atr / x.Price * 10000.0).ToList())
                    o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7:+0.0;-0.0;0.0} | {8} | {9:0.0} |",
                        sess, era, cap, sw.N, PctCi(sw), EvCi(sw), PctCi(sc), sc.SuccessPct - sc.GrossBe, EvCi(sc), medAtrBps))
                Next
            Next
        Next
        o.AppendLine()

        o.AppendLine("## 13. Taker-stop fee case (maker entry, taker exit on a stop hit or a marked exit)")
        o.AppendLine()
        o.AppendLine("| Session | Target set by | Tier | n | Main window: net BE % (maker/maker) | net BE % (taker-stop) | net EV bps (taker-stop) | Carried 24 h: net BE % (taker-stop) | net EV bps (taker-stop) |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each cap In CapOrder
                For Each tier In {"ALL", "S+M"}
                    Dim rows = Filter(sigs, sess, cap, tier)
                    If rows.Count = 0 Then Continue For
                    Dim sw = Compute(rows, "W" & rows(0).Windows.Max().ToString(Inv), fees)
                    Dim sc = Compute(rows, "C24", fees)
                    o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4:0.0} | {5:0.0} | {6:+0.0;-0.0;0.0} | {7:0.0} | {8:+0.0;-0.0;0.0} |",
                        sess, cap, tier, sw.N, sw.NetBe, sw.NetBeTaker, sw.NetEvTaker, sc.NetBeTaker, sc.NetEvTaker))
                Next
            Next
        Next
        o.AppendLine()

        o.AppendLine("## 14. Sensitivity: walk starts at the NEXT full bar instead of the bar containing the signal, ALL tiers")
        o.AppendLine()
        o.AppendLine("| Session | Target set by | n | Main window net EV bps: containing bar | next bar | Carried 24 h net EV bps: containing bar | next bar |")
        o.AppendLine("|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each cap In CapOrder
                Dim rows = Filter(sigs, sess, cap, "ALL")
                If rows.Count = 0 Then Continue For
                Dim wmax As String = rows(0).Windows.Max().ToString(Inv)
                o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3:+0.00;-0.00;0.00} | {4:+0.00;-0.00;0.00} | {5:+0.00;-0.00;0.00} | {6:+0.00;-0.00;0.00} |",
                    sess, cap, rows.Count, Compute(rows, "W" & wmax, fees).NetEv, Compute(rows, "N" & wmax, fees).NetEv,
                    Compute(rows, "C24", fees).NetEv, Compute(rows, "C24N", fees).NetEv))
            Next
        Next
        o.AppendLine()

        ' ---- funding rule (brief §3.2 rule 4)
        Dim maxFund As Double = 0
        Dim maxFundCell As String = ""
        For Each mode In {"C24", "CSE"}
            For Each sess In SessionOrder
                For Each cap In CapOrder
                    For Each tier In TierOrder
                        Dim st = Compute(Filter(sigs, sess, cap, tier), mode, fees)
                        If st.N = 0 Then Continue For
                        If Math.Abs(st.FundingBps) > Math.Abs(maxFund) Then
                            maxFund = st.FundingBps
                            maxFundCell = mode & " " & sess & " " & cap & " " & tier & " (n=" & st.N.ToString(Inv) & ")"
                        End If
                    Next
                Next
            Next
        Next
        o.AppendLine("## 15. Funding rule check")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Largest mean funding cost in any carried cell: {0:+0.000;-0.000;0.000} bps, in {1}. Rule: include funding in net EV only if a cell moves by more than 0.5 bps.", maxFund, maxFundCell))
        o.AppendLine()

        Dim text As String = o.ToString()
        File.WriteAllText(outPath, text, New UTF8Encoding(False))
        Console.Write(text)
        Console.WriteLine()
        Console.WriteLine("Wrote " & outPath)
        Return 0
    End Function

    ' ------------------------------------------------------------------ join (brief §2)

    Private Sub AppendJoin(o As StringBuilder, evalRows As List(Of EvalRow), boxLog As Dictionary(Of DateTime, CsvRow),
                           sigs As List(Of Sig), bars As Dictionary(Of DateTime, OhlcBar),
                           weekOpenHour As Integer, weekCloseHourExcl As Integer, collectorStart As DateTime)
        Dim outcomeSet As New HashSet(Of String)({"SUCCESS", "ADVERSE_HIT", "AMBIGUOUS", "WINDOW_EXPIRED", "NO_DATA"})
        Dim wk = evalRows.Where(Function(e) outcomeSet.Contains(e.Outcome) AndAlso InTradingWeek(e.Ts, weekOpenHour, weekCloseHourExcl)).ToList()

        o.AppendLine("## 1. The eval-cache join")
        o.AppendLine()
        o.AppendLine(String.Format(Inv, "- Eval cache rows: {0}. Trading-week directional outcome rows: {1} ({2} excluding NO_DATA).", evalRows.Count, wk.Count, wk.Where(Function(e) e.Outcome <> "NO_DATA").Count()))
        o.AppendLine(String.Format(Inv, "- Of those, whole-second .0000000Z rows (backfill provenance): {0}; sub-second rows (live provenance): {1}.", wk.Where(Function(e) e.IsBackfill).Count(), wk.Where(Function(e) Not e.IsBackfill).Count()))
        o.AppendLine()

        ' (a) naive join: +/-3 s, same verdict, each log row used at most once
        For Each exclNoData In {True, False}
            Dim used As New HashSet(Of DateTime)()
            Dim joined As Integer = 0, total As Integer = 0
            For Each e In wk
                If exclNoData AndAlso e.Outcome = "NO_DATA" Then Continue For
                total += 1
                Dim sec As DateTime = FloorSecond(e.Ts)
                For Each k In {0, -1, 1, -2, 2, -3, 3}
                    Dim t As DateTime = sec.AddSeconds(k)
                    Dim lr As CsvRow = Nothing
                    If Not used.Contains(t) AndAlso boxLog.TryGetValue(t, lr) AndAlso SameVerdict(lr.Verdict, e.Verdict) Then
                        used.Add(t) : joined += 1 : Exit For
                    End If
                Next
            Next
            o.AppendLine(String.Format(Inv, "- Naive join (+/-3 s, same verdict, one eval row per log row{0}): {1} of {2} = {3:0.0} %.",
                If(exclNoData, ", NO_DATA excluded", ", NO_DATA included"), joined, total, 100.0 * joined / Math.Max(1, total)))
        Next

        ' (b) fixed join: live row -> nearest earlier log row within 60 s with the same verdict;
        '     backfill row -> exact timestamp. One eval row kept per log row (live copy preferred).
        Dim byLog As New Dictionary(Of DateTime, List(Of EvalRow))()
        Dim unjoined As Integer = 0
        Dim offsets As New Dictionary(Of Integer, Integer)()
        For Each e In wk
            Dim sec As DateTime = FloorSecond(e.Ts)
            Dim hit As DateTime = DateTime.MinValue
            If e.IsBackfill Then
                Dim lr As CsvRow = Nothing
                If boxLog.TryGetValue(sec, lr) AndAlso SameVerdict(lr.Verdict, e.Verdict) Then hit = sec
            Else
                For k As Integer = 0 To 60
                    Dim lr As CsvRow = Nothing
                    If boxLog.TryGetValue(sec.AddSeconds(-k), lr) Then
                        If SameVerdict(lr.Verdict, e.Verdict) Then
                            hit = sec.AddSeconds(-k)
                            Dim cur As Integer = 0
                            offsets.TryGetValue(k, cur)
                            offsets(k) = cur + 1
                        End If
                        Exit For
                    End If
                Next
            End If
            If hit = DateTime.MinValue Then unjoined += 1 : Continue For
            If Not byLog.ContainsKey(hit) Then byLog(hit) = New List(Of EvalRow)()
            byLog(hit).Add(e)
        Next
        Dim pairs As Integer = byLog.Values.Where(Function(l) l.Count >= 2).Count()
        Dim pairsDisagree As Integer = byLog.Values.Where(Function(l) l.Count >= 2 AndAlso l.Select(Function(x) x.Outcome).Distinct().Count() > 1).Count()
        Dim triples As Integer = byLog.Values.Where(Function(l) l.Count > 2).Count()
        Dim dedupRows As Integer = byLog.Count + unjoined
        o.AppendLine(String.Format(Inv, "- Log rows carrying two or more eval copies: {0} (more than two: {1}). Copies whose outcomes disagree: {2}.", pairs, triples, pairsDisagree))
        o.AppendLine(String.Format(Inv, "- **Fixed join** (live copy to nearest earlier log row within 60 s, backfill copy exact, one copy per log row): {0} of {1} de-duplicated rows = {2:0.00} %.",
            byLog.Count, dedupRows, 100.0 * byLog.Count / Math.Max(1, dedupRows)))
        o.AppendLine("- Live-copy lag behind its log row (seconds: rows): " & String.Join(", ", offsets.OrderBy(Function(kv) kv.Key).Select(Function(kv) kv.Key.ToString(Inv) & ":" & kv.Value.ToString(Inv))))

        ' (c) cross-check against the candle walk, production window (T+3 .. T+15 x res)
        Dim sigByTs = sigs.ToDictionary(Function(s) s.Ts)
        Dim compared As Integer = 0, agree As Integer = 0, levelsMatch As Integer = 0
        Dim confusion As New Dictionary(Of String, Integer)()
        Dim popCollector As Integer = 0, popJoined As Integer = 0
        Dim evalEnd As DateTime = If(evalRows.Count > 0, evalRows.Max(Function(e) e.Ts), DateTime.MinValue)
        For Each s In sigs
            If s.Ts < collectorStart OrElse s.Ts.AddMinutes(45) > evalEnd Then Continue For
            popCollector += 1
            If byLog.ContainsKey(s.Ts) Then popJoined += 1
        Next
        For Each kv In byLog
            Dim s As Sig = Nothing
            If Not sigByTs.TryGetValue(kv.Key, s) Then Continue For
            Dim e As EvalRow = If(kv.Value.FirstOrDefault(Function(x) Not x.IsBackfill), kv.Value(0))
            If e.Outcome = "NO_DATA" Then Continue For
            If Math.Abs(e.FavBar - s.TargetPx) <= 0.011 AndAlso Math.Abs(e.AdvBar - s.StopPx) <= 0.011 Then levelsMatch += 1
            Dim res As Integer = If(e.ExecRes <= 0, 1, e.ExecRes)
            Dim w = Walk(s, e.FavBar, e.AdvBar, s.RowMin.AddMinutes(3), s.RowMin.AddMinutes(15 * res), bars, Nothing)
            Dim mine As String = If(w.Outcome = 1, "SUCCESS", If(w.Outcome = 2, "ADVERSE_HIT", If(w.Outcome = 3, "AMBIGUOUS", "WINDOW_EXPIRED")))
            compared += 1
            If mine = e.Outcome Then agree += 1
            Dim ck As String = e.Outcome & " -> " & mine
            Dim cur As Integer = 0
            confusion.TryGetValue(ck, cur)
            confusion(ck) = cur + 1
        Next
        o.AppendLine(String.Format(Inv, "- Population rows in the collector era with a full eval window: {0}; with a joined eval row: {1} = {2:0.00} %.", popCollector, popJoined, 100.0 * popJoined / Math.Max(1, popCollector)))
        o.AppendLine(String.Format(Inv, "- Cross-check, candle walk with the production window (bars closing T+3 to T+15 x resolution) on the eval row's own barriers: {0} of {1} outcomes agree = {2:0.00} %. Eval barriers equal the logged placed levels on {3} rows.",
            agree, compared, 100.0 * agree / Math.Max(1, compared), levelsMatch))
        o.AppendLine("- Cross-check disagreements (eval -> candle walk): " & String.Join("; ", confusion.Where(Function(kv) kv.Key.Split({" -> "}, StringSplitOptions.None)(0) <> kv.Key.Split({" -> "}, StringSplitOptions.None)(1)).OrderByDescending(Function(kv) kv.Value).Select(Function(kv) kv.Key & " " & kv.Value.ToString(Inv))))
        o.AppendLine()
    End Sub

    ' ------------------------------------------------------------------ label check

    Private Sub AppendLabelCheck(o As StringBuilder, sigs As List(Of Sig), cfg As EngineSettings)
        o.AppendLine("## 3. Label meaning check (data side)")
        o.AppendLine()
        o.AppendLine("- `none` rows: target distance within 0.021 xATR of the session fallback multiple read from settings (the v30 noise floor is 0.02 xATR).")
        o.AppendLine("- `swing` rows: placed target within 0.5 USD of the logged SwingTarget for the side. `hvn` rows: within 0.5 USD of the logged nearest HVN for the side.")
        o.AppendLine(String.Format(Inv, "- Stop at the ATR bound: stop distance within 0.021 xATR of stop_max_atr_mult {0} or atr_stop_multiplier {1}.", cfg.Scoring.StructuralLevels.StopMaxAtrMult, cfg.Scoring.AtrStopMultiplier))
        o.AppendLine()
        o.AppendLine("| Session | Era | Target set by | n | Label matches geometry % | Med target xATR | p5-p95 target xATR | Stop at ATR bound % | Med stop xATR |")
        o.AppendLine("|---|---|---|---|---|---|---|---|---|")
        For Each sess In SessionOrder
            For Each era In sigs.Select(Function(x) x.Era).Distinct().OrderBy(Function(x) x)
                For Each cap In CapOrder
                    Dim rows = sigs.Where(Function(x) x.Session = sess AndAlso x.Era = era AndAlso x.Cap = cap).ToList()
                    If rows.Count = 0 Then Continue For
                    Dim match As Integer
                    Select Case cap
                        Case "none" : match = rows.Where(Function(x) Math.Abs(x.TAtr - x.FallbackMult) <= 0.021).Count()
                        Case "swing" : match = rows.Where(Function(x) Math.Abs(x.TargetPx - x.SwingTarget) <= 0.5).Count()
                        Case "hvn" : match = rows.Where(Function(x) Math.Abs(x.TargetPx - x.HvnTarget) <= 0.5).Count()
                        Case Else : match = 0
                    End Select
                    Dim stopBound As Integer = rows.Where(Function(x) Math.Abs(x.SAtr - cfg.Scoring.StructuralLevels.StopMaxAtrMult) <= 0.021 OrElse Math.Abs(x.SAtr - cfg.Scoring.AtrStopMultiplier) <= 0.021).Count()
                    Dim ta = rows.Select(Function(x) x.TAtr).OrderBy(Function(x) x).ToList()
                    o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5:0.00} | {6:0.00}-{7:0.00} | {8:0.0} | {9:0.00} |",
                        sess, era, cap, rows.Count, If(cap = "poc", "n/a", (100.0 * match / rows.Count).ToString("0.0", Inv)),
                        Median(ta), Pctl(ta, 5), Pctl(ta, 95), 100.0 * stopBound / rows.Count, Median(rows.Select(Function(x) x.SAtr).ToList())))
                Next
            Next
        Next
        o.AppendLine()
    End Sub

    ' ------------------------------------------------------------------ cell table

    Private Sub AppendCellTable(o As StringBuilder, sigs As List(Of Sig), modeOf As Func(Of Sig, String), fees As FeeCase, carried As Boolean, byTier As Boolean)
        If carried Then
            o.AppendLine("| Session | Target set by | Tier | n | Success rate [CI] | Stop hit % | UNRESOLVED % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Net EV bps [CI] | Funding bps | Net EV incl. funding | Med target / stop xATR | Resolution p50 / p90 min |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        Else
            o.AppendLine("| Session | Target set by | Tier | n | Success rate [CI] | Stop hit % | Timeout % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Net EV bps [CI] | Med target / stop xATR | Resolution p50 / p90 min |")
            o.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        End If
        For Each sess In SessionOrder
            For Each cap In CapOrder
                For Each tier In If(byTier, TierOrder, {"ALL"})
                    Dim rows = Filter(sigs, sess, cap, tier)
                    If rows.Count = 0 Then Continue For
                    Dim st = Compute(rows, modeOf(rows(0)), fees)
                    If carried Then
                        o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5:0.0} | {6:0.0} | {7:0.0} | {8:0.0} | {9:+0.0;-0.0;0.0} | {10:+0.0;-0.0;0.0} | {11} | {12:+0.00;-0.00;0.00} | {13:+0.0;-0.0;0.0} | {14:0.00} / {15:0.00} | {16:0} / {17:0} |",
                            sess, cap, tier, st.N, PctCi(st), 100.0 * (st.NStop + st.NAmb) / st.N, 100.0 * st.NOpen / st.N,
                            st.GrossBe, st.NetBe, st.SuccessPct - st.GrossBe, st.SuccessPct - st.NetBe, EvCi(st),
                            st.FundingBps, st.NetEv - st.FundingBps, st.MedTAtr, st.MedSAtr, st.P50, st.P90))
                    Else
                        o.AppendLine(String.Format(Inv, "| {0} | {1} | {2} | {3} | {4} | {5:0.0} | {6:0.0} | {7:0.0} | {8:0.0} | {9:+0.0;-0.0;0.0} | {10:+0.0;-0.0;0.0} | {11} | {12:0.00} / {13:0.00} | {14:0} / {15:0} |",
                            sess, cap, tier, st.N, PctCi(st), 100.0 * (st.NStop + st.NAmb) / st.N, 100.0 * st.NOpen / st.N,
                            st.GrossBe, st.NetBe, st.SuccessPct - st.GrossBe, st.SuccessPct - st.NetBe, EvCi(st),
                            st.MedTAtr, st.MedSAtr, st.P50, st.P90))
                    End If
                Next
            Next
        Next
        o.AppendLine()
    End Sub

    ' ------------------------------------------------------------------ core maths

    Private Function Walk(s As Sig, target As Double, stopPx As Double, firstClose As DateTime, lastClose As DateTime,
                          bars As Dictionary(Of DateTime, OhlcBar), fund As FundingCurve) As WalkResult
        Dim w As New WalkResult()
        Dim lastC As Double = Double.NaN
        Dim t As DateTime = firstClose
        Dim endT As DateTime = lastClose
        Dim resolved As Boolean = False
        While t <= lastClose
            Dim b As OhlcBar = Nothing
            If bars.TryGetValue(t, b) Then
                w.BarsSeen += 1
                lastC = b.Close
                Dim favHit As Boolean = If(s.IsLong, b.High >= target, b.Low <= target)
                Dim advHit As Boolean = If(s.IsLong, b.Low <= stopPx, b.High >= stopPx)
                If favHit OrElse advHit Then
                    w.Outcome = If(favHit AndAlso advHit, 3, If(favHit, 1, 2))
                    endT = t
                    resolved = True
                    Exit While
                End If
            Else
                w.MissingBars += 1
            End If
            t = t.AddMinutes(1)
        End While
        If Not resolved Then
            w.Outcome = 0
            If Not Double.IsNaN(lastC) Then w.MarkBps = If(s.IsLong, 1.0, -1.0) * (lastC - s.Price) / s.Price * 10000.0
        End If
        w.ResolveMin = (endT - s.Ts).TotalMinutes
        If fund IsNot Nothing Then w.FundingBps = If(s.IsLong, 1.0, -1.0) * fund.Accrued(s.Ts, endT) * 10000.0
        Return w
    End Function

    Private Function Result(s As Sig, w As WalkResult, feeWin As Double, feeLoss As Double) As Double
        Select Case w.Outcome
            Case 1 : Return s.TBps - feeWin
            Case 2, 3 : Return -s.SBps - feeLoss
            Case Else : Return w.MarkBps - feeLoss
        End Select
    End Function

    Private Function Compute(rows As List(Of Sig), mode As String, fees As FeeCase) As CellStats
        Dim st As New CellStats()
        Dim list = rows.Where(Function(s) s.Res.ContainsKey(mode)).ToList()
        st.N = list.Count
        If st.N = 0 Then Return st
        Dim sumT As Double = 0, sumS As Double = 0
        Dim succ As New List(Of Double)(), ev As New List(Of Double)(), evT As New List(Of Double)(), fundL As New List(Of Double)()
        Dim cl As New List(Of String)(), resMin As New List(Of Double)()
        For Each s In list
            Dim w = s.Res(mode)
            sumT += s.TBps : sumS += s.SBps
            Select Case w.Outcome
                Case 1 : st.NTarget += 1
                Case 2 : st.NStop += 1
                Case 3 : st.NAmb += 1
                Case Else : st.NOpen += 1
            End Select
            succ.Add(If(w.Outcome = 1, 1.0, 0.0))
            ' Maker/maker: one round-trip fee on every outcome (§5a).
            ev.Add(Result(s, w, fees.MakerRt, fees.MakerRt))
            ' Taker-stop: maker entry + maker exit on target; maker entry + taker exit otherwise.
            evT.Add(Result(s, w, fees.TakerWin, fees.TakerLoss))
            fundL.Add(w.FundingBps)
            cl.Add(s.Cluster)
            If w.Outcome <> 0 Then resMin.Add(w.ResolveMin)
            If w.MissingBars > 0 Then st.RowsWithMissing += 1
        Next
        Dim n As Double = st.N
        Dim m As Double = 0
        st.SuccessCi = 100.0 * ClusterCi(succ, cl, m)
        st.SuccessPct = 100.0 * m
        st.NetEvCi = ClusterCi(ev, cl, m)
        st.NetEv = m
        st.NetEvTaker = evT.Average()
        st.FundingBps = fundL.Average()
        st.GrossBe = 100.0 * sumS / (sumT + sumS)
        st.NetBe = 100.0 * (sumS + n * fees.MakerRt) / (sumT + sumS)
        st.NetBeTaker = 100.0 * (sumS + n * fees.TakerLoss) / (sumT - n * fees.TakerWin + sumS + n * fees.TakerLoss)
        st.MedTAtr = Median(list.Select(Function(x) x.TAtr).ToList())
        st.MedSAtr = Median(list.Select(Function(x) x.SAtr).ToList())
        st.MeanTBps = sumT / n
        st.MeanSBps = sumS / n
        Dim rs = resMin.OrderBy(Function(x) x).ToList()
        st.P50 = If(rs.Count > 0, Pctl(rs, 50), Double.NaN)
        st.P90 = If(rs.Count > 0, Pctl(rs, 90), Double.NaN)
        Return st
    End Function

    ' Cluster-robust 95 % half-width of a mean (CR1 small-cluster correction); clusters = session-days.
    Private Function ClusterCi(vals As List(Of Double), clusters As List(Of String), ByRef mean As Double) As Double
        Dim n As Integer = vals.Count
        mean = vals.Average()
        Dim mu As Double = mean
        Dim sums As New Dictionary(Of String, Double)()
        For i As Integer = 0 To n - 1
            Dim cur As Double = 0
            sums.TryGetValue(clusters(i), cur)
            sums(clusters(i)) = cur + (vals(i) - mu)
        Next
        Dim nc As Integer = sums.Count
        If nc < 2 Then Return Double.NaN
        Dim ss As Double = sums.Values.Sum(Function(x) x * x)
        Return 1.96 * Math.Sqrt(ss * nc / (nc - 1.0)) / n
    End Function

    ' ------------------------------------------------------------------ data loading

    Private Function LoadWithInstance(path As String) As List(Of Tuple(Of CsvRow, String))
        Dim rows = ForwardWindowJoiner.Load(path)
        Dim lines = File.ReadAllLines(path)
        Dim header = lines(0).Split(","c)
        Dim idx As Integer = Array.FindIndex(header, Function(h) h.Trim().Equals("InstanceId", StringComparison.OrdinalIgnoreCase))
        Dim result As New List(Of Tuple(Of CsvRow, String))(rows.Count)
        For Each r In rows
            Dim parts = lines(r.Index + 1).Split(","c)
            Dim inst As String = If(idx >= 0 AndAlso idx < parts.Length, parts(idx).Trim(), "")
            result.Add(Tuple.Create(r, inst))
        Next
        Return result
    End Function

    Private Function LoadEval(path As String) As List(Of EvalRow)
        Dim list As New List(Of EvalRow)()
        Dim col As Dictionary(Of String, Integer) = Nothing
        For Each line In File.ReadLines(path)
            If line.StartsWith("#") OrElse String.IsNullOrWhiteSpace(line) Then Continue For
            Dim p = line.Split(","c)
            If p(0).Trim() = "Timestamp" Then
                col = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                For i As Integer = 0 To p.Length - 1 : col(p(i).Trim()) = i : Next
                Continue For
            End If
            If col Is Nothing Then Throw New InvalidOperationException("eval cache has no header")
            Dim e As New EvalRow()
            Dim tsText As String = p(col("Timestamp")).Trim()
            e.Ts = DateTime.Parse(tsText, Inv, DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
            e.IsBackfill = tsText.EndsWith(".0000000Z")
            e.Verdict = p(col("Verdict")).Trim()
            Double.TryParse(p(col("FavBar")), NumberStyles.Float, Inv, e.FavBar)
            Double.TryParse(p(col("AdvBar")), NumberStyles.Float, Inv, e.AdvBar)
            e.Outcome = p(col("EvalOutcome")).Trim()
            Dim res As Integer = 1
            If col.ContainsKey("ExecResolution") AndAlso col("ExecResolution") < p.Length Then Integer.TryParse(p(col("ExecResolution")), res)
            e.ExecRes = res
            list.Add(e)
        Next
        Return list
    End Function

    Private Async Function LoadBarsAsync(cacheDir As String, firstMonday As DateTime, lastMonday As DateTime,
                                         weekOpenHour As Integer, weekCloseHourExcl As Integer,
                                         log As StringBuilder) As Task(Of Dictionary(Of DateTime, OhlcBar))
        Dim all As New Dictionary(Of DateTime, OhlcBar)()
        log.AppendLine("| Week (Monday) | Source | Bars (Mon open to Fri close) | Expected | Missing | Gaps (first 5) |")
        log.AppendLine("|---|---|---|---|---|---|")
        Dim wk As DateTime = firstMonday
        Dim totalMissing As Integer = 0
        While wk <= lastMonday
            Dim openT As DateTime = wk.AddHours(weekOpenHour)
            Dim closeT As DateTime = wk.AddDays(4).AddHours(weekCloseHourExcl)
            Dim path As String = IO.Path.Combine(cacheDir, "ohlc_1m_BTC-PERPETUAL_week_" & wk.ToString("yyyy-MM-dd", Inv) & ".csv")
            Dim weekBars As New List(Of OhlcBar)()
            Dim source As String
            If File.Exists(path) Then
                source = "cache"
                For Each line In File.ReadLines(path).Skip(1)
                    Dim p = line.Split(","c)
                    weekBars.Add(New OhlcBar With {
                        .CloseTime = DateTime.ParseExact(p(0), "yyyy-MM-dd HH:mm", Inv),
                        .Open = Double.Parse(p(1), Inv), .High = Double.Parse(p(2), Inv),
                        .Low = Double.Parse(p(3), Inv), .Close = Double.Parse(p(4), Inv)})
                Next
            Else
                source = "Deribit"
                Dim map = Await DeribitOhlcFetcher.FetchOhlcRange(openT, closeT)
                If map Is Nothing Then Throw New InvalidOperationException("candle fetch failed for week " & wk.ToString("yyyy-MM-dd", Inv) & ". STOP.")
                weekBars = map.Values.Where(Function(b) b.CloseTime > openT AndAlso b.CloseTime <= closeT).OrderBy(Function(b) b.CloseTime).ToList()
                Dim sb As New StringBuilder()
                sb.AppendLine("CloseTimeUtc,Open,High,Low,Close")
                For Each b In weekBars
                    sb.AppendLine(String.Join(",", b.CloseTime.ToString("yyyy-MM-dd HH:mm", Inv), b.Open.ToString("R", Inv), b.High.ToString("R", Inv), b.Low.ToString("R", Inv), b.Close.ToString("R", Inv)))
                Next
                File.WriteAllText(path, sb.ToString())
                Await Task.Delay(1000)
            End If
            For Each b In weekBars : all(b.CloseTime) = b : Next
            Dim expected As Integer = CInt((closeT - openT).TotalMinutes)
            Dim gaps As New List(Of String)()
            Dim missing As Integer = 0
            Dim t As DateTime = openT.AddMinutes(1)
            Dim gapStart As DateTime = DateTime.MinValue
            While t <= closeT
                If Not all.ContainsKey(t) Then
                    missing += 1
                    If gapStart = DateTime.MinValue Then gapStart = t
                ElseIf gapStart <> DateTime.MinValue Then
                    gaps.Add(gapStart.ToString("MM-dd HH:mm", Inv) & "+" & CInt((t - gapStart).TotalMinutes).ToString(Inv))
                    gapStart = DateTime.MinValue
                End If
                t = t.AddMinutes(1)
            End While
            If gapStart <> DateTime.MinValue Then gaps.Add(gapStart.ToString("MM-dd HH:mm", Inv) & "+" & CInt((t - gapStart).TotalMinutes).ToString(Inv))
            totalMissing += missing
            log.AppendLine(String.Format(Inv, "| {0:yyyy-MM-dd} | {1} | {2} | {3} | {4} | {5} |", wk, source, weekBars.Count, expected, missing, String.Join(" ", gaps.Take(5))))
            wk = wk.AddDays(7)
        End While
        log.AppendLine()
        log.AppendLine("- Total missing trading-week 1-minute bars: " & totalMissing.ToString(Inv))
        Return all
    End Function

    Private Async Function LoadFundingAsync(cacheDir As String, spanStart As DateTime, spanEnd As DateTime,
                                            http As HttpClient, log As StringBuilder) As Task(Of FundingCurve)
        Dim path As String = IO.Path.Combine(cacheDir, "funding_1h_BTC-PERPETUAL.csv")
        Dim recs As New Dictionary(Of Long, Double)()
        If File.Exists(path) Then
            For Each line In File.ReadLines(path).Skip(1)
                Dim p = line.Split(","c)
                recs(Long.Parse(p(0), Inv)) = Double.Parse(p(1), NumberStyles.Float, Inv)
            Next
        End If
        Dim missing As Boolean = False
        Dim h As DateTime = HourFloor(spanStart).AddHours(1)
        While h <= HourFloor(spanEnd).AddHours(1)
            If Not recs.ContainsKey(ToMs(h)) Then missing = True : Exit While
            h = h.AddHours(1)
        End While
        If missing Then
            Dim cur As DateTime = HourFloor(spanStart)
            While cur <= spanEnd.AddHours(1)
                Dim chunkEnd As DateTime = cur.AddDays(10)
                Dim url As String = "https://www.deribit.com/api/v2/public/get_funding_rate_history?instrument_name=BTC-PERPETUAL&start_timestamp=" &
                                    ToMs(cur).ToString(Inv) & "&end_timestamp=" & ToMs(chunkEnd).ToString(Inv)
                Dim json As String = Await http.GetStringAsync(url)
                Using doc = JsonDocument.Parse(json)
                    For Each el In doc.RootElement.GetProperty("result").EnumerateArray()
                        Dim rateEl As JsonElement
                        If el.TryGetProperty("interest_1h", rateEl) AndAlso rateEl.ValueKind = JsonValueKind.Number Then
                            recs(el.GetProperty("timestamp").GetInt64()) = rateEl.GetDouble()
                        End If
                    Next
                End Using
                Await Task.Delay(1000)
                cur = chunkEnd
            End While
            Dim sb As New StringBuilder()
            sb.AppendLine("TimestampMs,Interest1h")
            For Each kv In recs.OrderBy(Function(k) k.Key)
                sb.AppendLine(kv.Key.ToString(Inv) & "," & kv.Value.ToString("R", Inv))
            Next
            File.WriteAllText(path, sb.ToString())
            log.AppendLine("- Funding history: fetched from Deribit public/get_funding_rate_history, " & recs.Count.ToString(Inv) & " hourly records.")
        Else
            log.AppendLine("- Funding history: cache, " & recs.Count.ToString(Inv) & " hourly records.")
        End If
        Return New FundingCurve(spanStart, spanEnd, recs)
    End Function

    ' ------------------------------------------------------------------ helpers

    Private Function Filter(sigs As List(Of Sig), sess As String, cap As String, tier As String) As List(Of Sig)
        Return sigs.Where(Function(x) x.Session = sess AndAlso x.Cap = cap AndAlso
                              (tier = "ALL" OrElse (tier = "S+M" AndAlso x.Tier <> "WEAK") OrElse x.Tier = tier)).ToList()
    End Function

    Private Function InTradingWeek(ts As DateTime, openHour As Integer, closeHourExcl As Integer) As Boolean
        If ts = DateTime.MinValue Then Return False
        Dim d As DayOfWeek = ts.DayOfWeek
        If d = DayOfWeek.Saturday OrElse d = DayOfWeek.Sunday Then Return False
        If d = DayOfWeek.Monday AndAlso ts.Hour < openHour Then Return False
        If d = DayOfWeek.Friday AndAlso ts.Hour >= closeHourExcl Then Return False
        Return True
    End Function

    Private Function EraOf(ts As DateTime, collectorStart As DateTime) As String
        If ts < collectorStart Then Return "1 pre-collector dev runs"
        If ts < V66Edge Then Return "2 collector to v66"
        If ts < AtrStepEdge Then Return "3 v66 to 2026-08-20"
        Return "4 from 2026-08-20"
    End Function

    Private Function SameVerdict(a As String, b As String) As Boolean
        Return String.Equals(If(a, "").Trim(), If(b, "").Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function FloorSecond(t As DateTime) As DateTime
        Return New DateTime(t.Ticks - (t.Ticks Mod TimeSpan.TicksPerSecond))
    End Function

    Private Function HourFloor(t As DateTime) As DateTime
        Return New DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0)
    End Function

    Private Function ToMs(t As DateTime) As Long
        Return New DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeMilliseconds()
    End Function

    Private Function Median(v As List(Of Double)) As Double
        If v.Count = 0 Then Return Double.NaN
        Dim s = v.OrderBy(Function(x) x).ToList()
        Return Pctl(s, 50)
    End Function

    ' Nearest-rank percentile on an ascending list.
    Private Function Pctl(sorted As List(Of Double), p As Double) As Double
        If sorted.Count = 0 Then Return Double.NaN
        Dim rank As Integer = CInt(Math.Ceiling(p / 100.0 * sorted.Count))
        If rank < 1 Then rank = 1
        If rank > sorted.Count Then rank = sorted.Count
        Return sorted(rank - 1)
    End Function

    Private Function PctCi(st As CellStats) As String
        Return String.Format(Inv, "{0:0.0} [{1:0.0}, {2:0.0}]", st.SuccessPct, Math.Max(0, st.SuccessPct - st.SuccessCi), Math.Min(100, st.SuccessPct + st.SuccessCi))
    End Function

    Private Function EvCi(st As CellStats) As String
        Return String.Format(Inv, "{0:+0.0;-0.0;0.0} [{1:+0.0;-0.0;0.0}, {2:+0.0;-0.0;0.0}]", st.NetEv, st.NetEv - st.NetEvCi, st.NetEv + st.NetEvCi)
    End Function

    Private Function Row(label As String, n As Integer) As String
        Return "| " & label & " | " & n.ToString(Inv) & " |"
    End Function

    Private Function ParseArgs(args As String()) As Dictionary(Of String, String)
        Dim d As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim i As Integer = 0
        While i < args.Length
            If args(i).StartsWith("--") AndAlso i + 1 < args.Length Then
                d(args(i).Substring(2)) = args(i + 1)
                i += 2
            Else
                i += 1
            End If
        End While
        Return d
    End Function

    Private Function ArgOr(d As Dictionary(Of String, String), key As String, fallback As String) As String
        Dim v As String = Nothing
        Return If(d.TryGetValue(key, v), v, fallback)
    End Function

    Private Function FileSha256(path As String) As String
        Using sha = SHA256.Create()
            Using fs = File.OpenRead(path)
                Return Convert.ToHexString(sha.ComputeHash(fs))
            End Using
        End Using
    End Function

End Module
