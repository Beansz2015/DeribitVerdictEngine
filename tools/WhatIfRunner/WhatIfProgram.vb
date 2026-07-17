' tools/WhatIfRunner/WhatIfProgram.vb
' Entry point for the offline What-If replay runner.
' docs/offline-whatif-replay-proposal.md (APPROVED 2026-07-16, W1–W7).
'
' Usage:
'   WhatIfRunner <overlay.json> [--from yyyy-MM-dd] [--to yyyy-MM-dd]
'                [--csv <analysis_log.csv>] [--settings <settings.json>] [--out <dir>]
'   → whatif_report_<stamp>.md   (baseline vs overlay, side by side)
'
' Exit codes: 0 report written · 1 error (bad overlay / no rows / OHLC fetch failed).
'
' Take the logged book (CSV v0.8+ rows) + 1m OHLC, apply a settings overlay (a small JSON
' fragment of hypothesis values, whitelist-validated), re-derive per row the placed levels
' and verdict tier under the overlay, re-walk outcomes with the SHIPPED FailureRateMatrix,
' and print a baseline-vs-overlay failure report on identical rows. Analysis-only: zero
' scoring impact, never writes settings.json.
'
' Host-agnostic: no System.Windows.Forms references. net8.0, runs on Linux via `dotnet WhatIfRunner.dll`.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks

Public Class WhatIfProgram

    Private Const DefaultEvalWindowBars As Integer = 15   ' full hold horizon (§2 eval-window dimension)

    Public Shared Function Main(args As String()) As Integer
        Try
            Return RunAsync(args).GetAwaiter().GetResult()
        Catch ex As WhatIfOverlayError
            Console.Error.WriteLine("[WhatIf] Overlay rejected: " & ex.Message)
            Return 1
        Catch ex As Exception
            Console.Error.WriteLine("[WhatIf] Fatal: " & ex.Message)
            Return 1
        End Try
    End Function

    Private Shared Async Function RunAsync(args As String()) As Task(Of Integer)
        SetWorkingDirectoryToRepoRoot()

        ' -- Parse args --------------------------------------------------------------------
        If args.Length = 0 OrElse args(0).StartsWith("--") Then
            Console.Error.WriteLine("Usage: WhatIfRunner <overlay.json> [--from yyyy-MM-dd] [--to yyyy-MM-dd] " &
                                    "[--csv <path>] [--settings <path>] [--out <dir>]")
            Return 1
        End If
        Dim overlayPath As String = args(0)
        Dim csvPath As String = "analysis_log.csv"
        Dim settingsPath As String = "settings.json"
        Dim outDir As String = "."
        Dim fromDate As DateTime = DateTime.MinValue
        Dim toDate As DateTime = DateTime.MaxValue
        Dim i As Integer = 1
        While i < args.Length
            Select Case args(i).ToLowerInvariant()
                Case "--from" : i += 1 : fromDate = ParseDate(args, i, DateTime.MinValue)
                Case "--to" : i += 1 : toDate = ParseDate(args, i, DateTime.MaxValue)
                Case "--csv" : i += 1 : If i < args.Length Then csvPath = args(i)
                Case "--settings" : i += 1 : If i < args.Length Then settingsPath = args(i)
                Case "--out" : i += 1 : If i < args.Length Then outDir = args(i)
            End Select
            i += 1
        End While

        If Not File.Exists(overlayPath) Then
            Console.Error.WriteLine("[WhatIf] Overlay file not found: " & overlayPath)
            Return 1
        End If

        ' -- Load settings + overlay -------------------------------------------------------
        Dim settings As New WhatIfSettings(settingsPath)
        Dim overlay As WhatIfOverlay = WhatIfOverlay.Parse(File.ReadAllText(overlayPath))
        Dim cells As List(Of Dictionary(Of String, Double)) =
            overlay.ExpandGrid(Function(p) settings.LiveValueOf(p))
        Console.WriteLine(String.Format("[WhatIf] Grid: {0} cell(s), {1} swept knob(s).",
                                        cells.Count, overlay.Knobs.Where(Function(k) k.IsSweep).Count()))

        ' -- Load + filter rows (v0.8+, in span, POC-tier excluded) ------------------------
        Dim allRows As List(Of CsvRow) = ForwardWindowJoiner.Load(csvPath)
        Dim toEff As DateTime = If(toDate <> DateTime.MaxValue AndAlso toDate.TimeOfDay = TimeSpan.Zero,
                                   toDate.AddDays(1), toDate)
        Dim inSpan = allRows.Where(Function(r) r.HasPlaced AndAlso r.MaxScore > 0 AndAlso
                                       r.Timestamp <> DateTime.MinValue AndAlso
                                       r.Timestamp >= fromDate AndAlso r.Timestamp < toEff).ToList()
        Dim pocExcluded As Integer = inSpan.Where(Function(r) String.Equals(r.TargetCapReason, "poc", StringComparison.OrdinalIgnoreCase)).Count()
        Dim rows = inSpan.Where(Function(r) Not String.Equals(r.TargetCapReason, "poc", StringComparison.OrdinalIgnoreCase)).ToList()

        If rows.Count = 0 Then
            Console.Error.WriteLine("[WhatIf] No eligible v0.8+ rows in span " &
                                    fromDate.ToString("yyyy-MM-dd") & " → " & toDate.ToString("yyyy-MM-dd") &
                                    " (POC-excluded: " & pocExcluded & ").")
            Return 1
        End If

        Dim spanFrom As DateTime = rows.Min(Function(r) r.Timestamp)
        Dim spanTo As DateTime = rows.Max(Function(r) r.Timestamp)
        Console.WriteLine(String.Format("[WhatIf] {0} eligible rows ({1:yyyy-MM-dd HH:mm} → {2:yyyy-MM-dd HH:mm} UTC); POC-excluded {3}.",
                                        rows.Count, spanFrom, spanTo, pocExcluded))

        ' -- Fetch 1m OHLC for the span + forward windows, populate ForwardBars ------------
        Dim maxWindowMin As Integer = rows.
            Select(Function(r) AnalysisConstants.HoldWindowsForResolution(r.ExecResolution).Max()).
            DefaultIfEmpty(15).Max()
        Dim ohlcMap = Await DeribitOhlcFetcher.FetchOhlcRange(spanFrom, spanTo.AddMinutes(maxWindowMin + 1))
        If ohlcMap Is Nothing Then
            Console.Error.WriteLine("[WhatIf] Forward-data (1m OHLC) fetch failed — report cannot be built until Deribit is reachable.")
            Return 1
        End If
        ForwardWindowJoiner.PopulateForwardBars(rows, ohlcMap)

        ' -- Baseline (live cfg) -----------------------------------------------------------
        Dim baselineRun = WhatIfReplay.RunCell(rows, settings.Live, DefaultEvalWindowBars, keepRows:=True)

        ' -- Grid ranking pass (EV only) ---------------------------------------------------
        Dim gridCells As New List(Of WhatIfGridCell)()
        For idx = 0 To cells.Count - 1
            Dim cell = cells(idx)
            Dim evalWin As Integer = If(cell.ContainsKey("eval_window"), CInt(cell("eval_window")), DefaultEvalWindowBars)
            Dim cfg = settings.BuildCellSettings(cell)
            Dim run = WhatIfReplay.RunCell(rows, cfg, evalWin, keepRows:=False)
            gridCells.Add(New WhatIfGridCell With {
                .Index = idx, .Cell = cell, .EvalWindowBars = evalWin,
                .EvSamples = run.EvSamples, .DirectionalCount = run.DirectionalCount,
                .BelowMinMoveExcluded = run.BelowMinMoveExcluded})
        Next

        ' -- Split-half validation (alternating session-days) ------------------------------
        Dim selectionDates = SelectionDateSet(rows)
        For Each gc In gridCells
            Dim full = gc.EvSamples.Select(Function(s) s.EvAtr)
            Dim sel = gc.EvSamples.Where(Function(s) selectionDates.Contains(s.Timestamp.Date)).Select(Function(s) s.EvAtr)
            Dim hold = gc.EvSamples.Where(Function(s) Not selectionDates.Contains(s.Timestamp.Date)).Select(Function(s) s.EvAtr)
            gc.EvFull = WhatIfEvStat.Of_(full)
            gc.EvSel = WhatIfEvStat.Of_(sel)
            gc.EvHold = WhatIfEvStat.Of_(hold)
            gc.Divergent = (gc.EvSel.N > 0 AndAlso gc.EvHold.N > 0 AndAlso gc.EvHold.Mean < gc.EvSel.CiLow)
        Next

        ' Winner: best selection-half EV (single-cell grid ⇒ the only cell).
        Dim winnerIdx As Integer = 0
        If gridCells.Count > 1 Then
            winnerIdx = gridCells.OrderByDescending(Function(c) If(c.EvSel.N > 0, c.EvSel.Mean, Double.NegativeInfinity)).
                                  ThenByDescending(Function(c) c.EvFull.Mean).First().Index
        End If
        Dim winnerCell = gridCells(winnerIdx)

        ' -- Winner run WITH rows (for the failure matrix) ---------------------------------
        Dim winnerCfg = settings.BuildCellSettings(winnerCell.Cell)
        Dim winnerRun = WhatIfReplay.RunCell(rows, winnerCfg, winnerCell.EvalWindowBars, keepRows:=True)

        ' -- Overfit counter (§4 guard-rail 2) ---------------------------------------------
        Dim overfit As Integer = BumpOverfitCounter(outDir, spanFrom, spanTo, cells.Count)

        ' -- Assemble + write report -------------------------------------------------------
        Dim stamp As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
        Dim model As New WhatIfReportModel With {
            .Stamp = stamp, .OverlayPath = overlayPath, .CsvPath = csvPath,
            .SpanFrom = spanFrom, .SpanTo = spanTo,
            .TotalRows = rows.Count, .PocExcluded = pocExcluded,
            .GridCellCount = cells.Count,
            .SweptKnobs = overlay.Knobs.Where(Function(k) k.IsSweep).Select(Function(k) k.Path).ToList(),
            .OverlaySummary = "", .OverfitCounter = overfit,
            .Cells = gridCells, .WinnerIndex = winnerIdx,
            .LiveCfg = settings.Live, .WinnerCfg = winnerCfg,
            .BaselineRows = baselineRun.ReplayedRows, .WinnerRows = winnerRun.ReplayedRows,
            .BaselineBelowMin = baselineRun.BelowMinMoveExcluded, .WinnerBelowMin = winnerRun.BelowMinMoveExcluded,
            .SettingsVersion = settings.Live.Version}

        Dim md As String = WhatIfReport.Build(model)
        Directory.CreateDirectory(outDir)
        Dim outPath As String = Path.Combine(outDir, "whatif_report_" & stamp & ".md")
        File.WriteAllText(outPath, md)
        Console.WriteLine("[WhatIf] Report written: " & Path.GetFullPath(outPath))
        Console.WriteLine("[WhatIf] Winner cell: " & (If(winnerCell.Cell.Count = 0, "(live baseline)",
            String.Join(", ", winnerCell.Cell.Select(Function(kv) kv.Key & "=" & kv.Value.ToString("0.####", CultureInfo.InvariantCulture))))))
        Return 0
    End Function

    ' Alternating session-days: distinct UTC dates sorted, even-index → selection half.
    Private Shared Function SelectionDateSet(rows As List(Of CsvRow)) As HashSet(Of Date)
        Dim dates = rows.Select(Function(r) r.Timestamp.Date).Distinct().OrderBy(Function(d) d).ToList()
        Dim sel As New HashSet(Of Date)()
        For j = 0 To dates.Count - 1
            If j Mod 2 = 0 Then sel.Add(dates(j))
        Next
        Return sel
    End Function

    ' Read-modify-write JSON counter of overlay/cell evaluations per book span. The counter is
    ' the loud reminder that trying many knobs on one book finds phantom winners (§4 guard-rail 2).
    Private Shared Function BumpOverfitCounter(outDir As String, spanFrom As DateTime, spanTo As DateTime, cellCount As Integer) As Integer
        Dim counterPath As String = Path.Combine(outDir, "whatif_overlay_counter.json")
        Dim key As String = spanFrom.ToString("yyyy-MM-dd") & "|" & spanTo.ToString("yyyy-MM-dd")
        Dim obj As JsonObject = Nothing
        Try
            If File.Exists(counterPath) Then
                obj = TryCast(JsonNode.Parse(File.ReadAllText(counterPath)), JsonObject)
            End If
        Catch
        End Try
        If obj Is Nothing Then obj = New JsonObject()
        Dim current As Integer = 0
        Dim existing As JsonNode = Nothing
        If obj.TryGetPropertyValue(key, existing) AndAlso existing IsNot Nothing Then
            Integer.TryParse(existing.ToString(), current)
        End If
        current += cellCount
        obj(key) = current
        Try
            File.WriteAllText(counterPath, obj.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
        Catch
        End Try
        Return current
    End Function

    Private Shared Function ParseDate(args As String(), idx As Integer, fallback As DateTime) As DateTime
        If idx >= args.Length Then Return fallback
        Dim d As DateTime
        If DateTime.TryParse(args(idx), CultureInfo.InvariantCulture,
                             Globalization.DateTimeStyles.AssumeUniversal Or Globalization.DateTimeStyles.AdjustToUniversal, d) Then
            Return d
        End If
        Return fallback
    End Function

    Private Shared Sub SetWorkingDirectoryToRepoRoot()
        Try
            Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
            For level As Integer = 1 To 8
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
