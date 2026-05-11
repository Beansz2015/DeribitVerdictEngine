# Spec: Output Dump File — Full Analysis Capture for Post-Hoc Review
**Proposed:** 2026-05-09 (revised 2026-05-09)
**Status:** PROPOSED 2026-05-09
**Target files:** new — `AnalysisOutputDump.vb`, `UI/OutputDumpSettingsForm.vb` (+ designer); existing — `UI/MainForm_Render_Header.vb` (status-bar links), `UI/MainForm_Render_Sections.vb` (RenderOutput hook), `UI/MainForm_Layout.vb` (new links + dialog open), `Core/Settings/EngineSettings.vb`, `settings.json`, `.gitignore`

---

## 1. Background

The structured CSV (`analysis_log.csv`) captures one row per analysis run across 87 typed columns. It's the right format for the auto-tweaker and quantitative review.

The rendered analysis text — the full multi-section block printed to the RTF pane on each run — captures additional information that the structured CSV doesn't, including:

- Cross-section relationships between adjacent values (e.g., VAH and the LVN-below being identical, which surfaced the 2026-05-08 VPFR geometry question)
- Computed display strings the engine produces but doesn't log (HOLD/EXIT advisory text, signal breakdown table notes, `PARTIAL->UPGRADED` markers, `STEP3 / STEP3b` annotation in the funding row)
- The full breakdown table including penalty notes (`PENALTY -1 opposing`, `PASS2b: ...`)
- Structural rows (`Long structural`, `Short structural`) with R:R and risk/reward in dollars
- Verbose render-only text that aids human debugging

Multiple bugs in this project (the OI×CVD priceUp bias, the TargetCapReason mismatch, the OFI ratio outliers, the asymmetric structural R:R observation) were spotted by reviewing the full rendered output rather than the columnar CSV. A persistent record of these outputs would shorten future debugging and let an LLM-based reviewer (offline or via the auto-tweaker) check for subtle inconsistencies.

This spec adds a single append-only markdown file capturing the full rendered analysis text per run, with a configurable cap on the number of runs retained and a small dialog for managing it.

---

## 2. Specification

### 2a. File location, name, format

**Path:** `bin/Debug/net8.0-windows/analysis_output_dump.md` (same directory as `analysis_log.csv`, gitignored).

**Format:** plain markdown. Each run appends a block consisting of:

```
## Run 2026-05-09 10:23:45 UTC+8
<full rendered analysis text exactly as displayed in the RTF pane,
 including all sections from VERDICT through SIGNAL BREAKDOWN>

---
```

The H2 header gives the local timestamp matching the engine's display (so a grep on a known UTC time aligns with what was on screen). The trailing `---` is a clean horizontal-rule separator that also serves as the **run-block delimiter** the rolling-trim logic counts.

**Format choice — plain text, not JSON.** JSON was considered and rejected. Reasoning:
- Most fields in JSON would duplicate `analysis_log.csv` columns (87 already covered). Net new information is just display strings and breakdown notes — a thin layer that's harder to navigate than CSV.
- Visual scanning is the primary debugging mode. All the bugs caught in May 2026 were spotted by reading adjacent lines as visual pairs. JSON object keys don't preserve that.
- Space savings of JSON vs text are ~20–30% — outweighed by the readability cost.
- For an LLM reviewing the file, plain text matches what the user sees on screen, so the reviewer's mental model aligns with the trader's.

### 2b. Settings

New block in `settings.json`:

```json
"analysis_logging": {
    "output_dump_enabled": true,
    "output_dump_max_runs": 3000
}
```

- `output_dump_enabled` (Boolean, default `true`): when `false`, the dump path is never opened or written to.
- `output_dump_max_runs` (Integer, default `3000`): maximum number of run-blocks to retain in the file. **Rolling-trim semantics** — after each append, if the file contains more than `max_runs` blocks, the oldest blocks are dropped until count == `max_runs`. Set to `0` for unlimited (no trimming; grows until manually cleared).

At 3000 runs ≈ 50 hours / ~2 days of 60s-cadence data ≈ ~9 MB on disk. Sized to cover a typical debugging window (a couple of days back) without growing indefinitely.

Settings.json `version` bump: 24 → 25 with `modified_by = "output-dump"`.

### 2c. Output Dump Settings dialog

New WinForm `UI/OutputDumpSettingsForm.vb`, non-modal, opened from the gear icon next to the status-bar `Output Dump` link. Controls:

| Control | Purpose |
|---|---|
| `chkEnabled` (Checkbox) | Binds to `output_dump_enabled` |
| `txtMaxRuns` (TextBox, int ≥ 0) | Binds to `output_dump_max_runs`. `0` = unlimited. Label clarifies: "Keep last N runs (0 = unlimited)" |
| `lblFilePath` (Label, read-only) | Full path to `analysis_output_dump.md` |
| `lblFileSize` (Label, read-only) | Current file size and approximate run count (refresh on dialog open and on Save) |
| `btnClear` (Button) | "Clear Output Dump". Shows `MessageBox` confirm — `"Clear analysis_output_dump.md? This cannot be undone."` Yes truncates the file to empty, refreshes `lblFileSize`. No cancels. |
| `btnSave` (Button) | Validates inputs (`txtMaxRuns` parses to non-negative integer), writes both values back to `settings.json` via `SettingsLoader.Save`. Shows `MessageBox` confirmation. |
| `btnClose` (Button) | Closes dialog. |

Form is sized roughly 480×260 px, non-modal, matches the visual style of `TweakSettingsForm`.

### 2d. Status-bar UI changes

In `MainForm_Layout`:

1. **Status-bar link** `lnkOutputDump`:
   - Text: `Output Dump`
   - Click opens the file in the OS default handler (`Process.Start(path)` with `UseShellExecute = True`).
   - If the file doesn't exist yet or `output_dump_enabled = false`, show a brief `MessageBox`: "Output dump is empty or disabled."

2. **Gear icon link** `lnkOutputDumpSettings`, immediately to the right of `lnkOutputDump`:
   - Text: `⚙` (or `[settings]` if Unicode is impractical with the existing font)
   - Click opens `OutputDumpSettingsForm` non-modally.

The previously-proposed separate `Clear Output Dump` status-bar link is **removed** — the Clear button now lives inside the settings dialog where it's grouped with the other dump-management controls.

### 2e. Writer behaviour — rolling-trim

New helper class `AnalysisOutputDump`:

```vb
Public Class AnalysisOutputDump
    ' Append a run block to the dump file. Best-effort write; never throws.
    ' After appending, if maxRuns > 0 and block count > maxRuns, trim oldest
    ' blocks until count == maxRuns.
    Public Shared Sub Append(timestamp As DateTime, renderedText As String,
                              dumpPath As String, enabled As Boolean,
                              maxRuns As Integer)
        If Not enabled Then Return
        Try
            ' 1. Append the new block.
            Using sw As New IO.StreamWriter(dumpPath, append:=True)
                sw.WriteLine("## Run " & timestamp.ToString("yyyy-MM-dd HH:mm:ss") & " " & GetTzSuffix())
                sw.WriteLine(renderedText.TrimEnd())
                sw.WriteLine()
                sw.WriteLine("---")
                sw.WriteLine()
            End Using

            ' 2. Apply rolling-trim if cap is set.
            If maxRuns > 0 Then
                TrimToMaxRuns(dumpPath, maxRuns)
            End If
        Catch ex As Exception
            Console.WriteLine("[AnalysisOutputDump] write failed: " & ex.Message)
        End Try
    End Sub

    ' Truncate the dump file to empty.
    Public Shared Sub Clear(dumpPath As String)
        Try
            IO.File.WriteAllText(dumpPath, "")
        Catch ex As Exception
            Console.WriteLine("[AnalysisOutputDump] clear failed: " & ex.Message)
        End Try
    End Sub

    ' Count "## Run " header lines in the file (= run blocks).
    Public Shared Function CountRuns(dumpPath As String) As Integer
        If Not IO.File.Exists(dumpPath) Then Return 0
        Dim count As Integer = 0
        Try
            For Each line In IO.File.ReadLines(dumpPath)
                If line.StartsWith("## Run ") Then count += 1
            Next
        Catch
        End Try
        Return count
    End Function

    ' Trim the oldest run blocks until block count == maxRuns.
    Private Shared Sub TrimToMaxRuns(dumpPath As String, maxRuns As Integer)
        Dim lines As String() = IO.File.ReadAllLines(dumpPath)
        Dim runStartIdx As New List(Of Integer)()
        For i As Integer = 0 To lines.Length - 1
            If lines(i).StartsWith("## Run ") Then runStartIdx.Add(i)
        Next
        If runStartIdx.Count <= maxRuns Then Return

        ' Drop runStartIdx.Count - maxRuns oldest blocks.
        Dim keepFromIdx As Integer = runStartIdx(runStartIdx.Count - maxRuns)
        Dim trimmed As String() = lines.Skip(keepFromIdx).ToArray()
        IO.File.WriteAllLines(dumpPath, trimmed)
    End Sub
End Class
```

**Trim cost:** with max_runs = 3000 and ~3 KB per block, the file is ~9 MB. `ReadAllLines` + slice + `WriteAllLines` runs in well under 100ms on SSD. Acceptable per-write overhead. For users setting larger caps (e.g., 10000+), the trim may slow to ~250ms; still well within the auto-run cycle.

**Trim frequency:** runs after every append when over cap. No batching — simple and predictable. If file performance ever becomes a concern, add a slack threshold (e.g., trim only when count > max_runs × 1.1).

### 2f. Source of rendered text

Read `txtOutput.Text` directly after `RenderOutput` returns — the WinForms RichTextBox exposes the plain-text view of its content without RTF codes. This is the cleanest approach; the alternative of a parallel StringBuilder mirror is brittle if future code adds new `AppendRtf` callers.

Hook placement: end of `RenderOutput` in `MainForm_Render_Sections.vb`, after the breakdown table has been appended. Call:

```vb
AnalysisOutputDump.Append(
    timestamp:=v.Timestamp,    ' the run timestamp from the verdict, same as TIME: line
    renderedText:=txtOutput.Text,
    dumpPath:=GetDumpPath(),
    enabled:=cfg.AnalysisLogging.OutputDumpEnabled,
    maxRuns:=cfg.AnalysisLogging.OutputDumpMaxRuns)
```

### 2g. Concurrency and resilience

- File access uses `StreamWriter` in append mode with default `FileShare.Read` so the user can open the file externally while the engine is appending.
- Write failures (file locked, disk full) log to `Console.WriteLine` and do not propagate. Same pattern as `AnalysisLogger.LogRun` — the analysis run must never abort on dump-write failure.
- Trim operation reads then overwrites; brief window where the file is non-readable to external viewers (~tens of milliseconds). Acceptable.
- Dump write runs on the analysis thread, immediately after `RenderOutput` returns. No async needed.

### 2h. Build / gitignore

Add to `.gitignore`:

```
bin/Debug/net8.0-windows/analysis_output_dump.md
bin/Release/net8.0-windows/analysis_output_dump.md
```

(No `.bak` patterns this time — rolling-trim overwrites in place rather than creating backups.)

---

## 3. Storage expectations (informational)

Approximate sizing at default `max_runs = 3000`:

| Auto-run interval | File size at cap | Time covered |
|---|---|---|
| 60s | ~9 MB | ~50 hours / ~2 days |
| 30s | ~9 MB | ~25 hours / ~1 day |
| 10s | ~9 MB | ~8 hours |

The cap is by *run count*, not bytes, so disk footprint stays constant regardless of cadence. Time-coverage varies inversely with cadence — adjust `output_dump_max_runs` upward if you'd rather cover more wall-clock time at a fast cadence.

When an LLM (e.g. the auto-tweaker reviewer, or a Claude session) inspects the file:

- 9 MB is comfortably under a single `Read`'s token budget for typical text density (~3 MB of UTF-8 text fits a 2000-line cap, so a full `Read` may need multiple offset ranges to cover everything; usually unnecessary).
- `Grep` for a specific run timestamp or `## Run` header is the right access mode.
- The `## Run <timestamp>` header pattern is structured enough to slice by date range with `awk` or similar if needed.

---

## 4. Out of Scope

- Per-session segmented files (Asia / London / NY). Single-file approach chosen for simplicity.
- Per-run separate files. Would create thousands of small files; rejected.
- JSON or other structured per-run record. Rationale in §2a.
- Diff highlighting between consecutive runs. Possible future enhancement.
- Cloud sync / remote upload. The file stays local.
- Encryption / access control. The file contains the same information visible on screen — not sensitive.
- Trim-with-slack-threshold optimisation. Not needed at default cap; add if performance ever becomes a concern.

---

## 5. Acceptance

- `dotnet build` clean.
- On engine startup with `output_dump_enabled = true` and no existing dump file: file is created on the first analysis run.
- After 5+ analysis runs: dump file contains 5 blocks, each headed by a `## Run <timestamp>` line and separated by `---`.
- `Output Dump` status-bar link opens the file in the OS default markdown viewer.
- `⚙` gear-icon link opens the Output Dump Settings dialog.
- Dialog shows current path, current file size, current run count.
- Toggling Enabled OFF and saving: subsequent runs don't write to the file; file is preserved as-is.
- Setting `Keep last N runs` to 5, accumulating 10+ runs: file size stays bounded; only the most recent 5 blocks remain visible.
- Setting `Keep last N runs` to 0, accumulating runs: file grows without bound.
- Clear button with confirmation truncates file to empty; cancel leaves it intact.
- Save button writes both settings back to `settings.json`; `version` increments.
- With `output_dump_enabled = false`: no I/O happens on analysis runs; dialog still functions; Output Dump link shows "empty or disabled" message.
- Existing `analysis_log.csv` behaviour is unaffected.
- No spec-rejected patterns introduced.

---

## 6. Implementation notes

- The timestamp used in the H2 header should be the engine's run timestamp (the one that appears in the `TIME:` line of the rendered output), sourced from `VerdictResult.Timestamp`, not `DateTime.Now` at write time.
- `txtOutput.Text` is the plain-text view of the RichTextBox in WinForms — strips RTF codes naturally. Use directly.
- The `OutputDumpSettingsForm.lblFileSize` refresh: read the file's `FileInfo.Length` and divide by 1024 for KB / 1048576 for MB. Run count via `AnalysisOutputDump.CountRuns`.
- For the gear icon: WinForms LinkLabel renders Unicode `⚙` (U+2699) reliably on default Segoe UI. If the engine's font lacks it, fall back to text `[settings]`.
- Save in the dialog goes through `SettingsLoader.Save(cfg, "output-dump: enabled=X, max_runs=Y")` so the version bump and change_log entry happen via the existing path.
