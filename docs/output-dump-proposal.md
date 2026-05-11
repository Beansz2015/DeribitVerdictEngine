# Spec: Output Dump File — Full Analysis Capture for Post-Hoc Review
**Proposed:** 2026-05-09
**Status:** PROPOSED 2026-05-09
**Target files:** new — `AnalysisOutputDump.vb`; existing — `UI/MainForm_Render_Header.vb` (RenderOutputHeader, status bar link), `UI/MainForm_Render_Sections.vb` (RenderOutput hook), `UI/MainForm_Layout.vb` (new button + status-bar link), `Core/Settings/EngineSettings.vb`, `settings.json`

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

This spec adds a single append-only markdown file that captures the full rendered analysis text per run, plus a UI control to clear or rotate it.

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

The H2 header gives the local timestamp matching the engine's display (so a grep on a known UTC time aligns with what was on screen). The trailing `---` is a clean horizontal-rule separator. The file is valid markdown so any markdown viewer renders sections cleanly.

**On the rendered text content:** the engine produces RTF for the on-screen pane. The dump file captures the plain-text equivalent — same content, no RTF formatting codes. RTF colours and bold styling are display-only and not informative for post-hoc review.

### 2b. Toggle setting

New setting in `settings.json`:

```json
"analysis_logging": {
    "output_dump_enabled": true,
    "output_dump_max_mb": 0
}
```

- `output_dump_enabled` (Boolean, default `true`): when `false`, the dump path is never opened or written to. No file is touched.
- `output_dump_max_mb` (Integer, default `0`): optional auto-rotation cap. `0` = no rotation (single file grows until cleared manually). Positive value = on each write, if the file exceeds this size, rename the existing file to `analysis_output_dump.<yyyyMMdd_HHmmss>.md.bak` and start a fresh one. Recommended initial setting: `0` (off) per user preference; set to e.g. `50` if multi-GB single file ever becomes inconvenient.

Settings.json `version` bump: 24 → 25 with `modified_by = "output-dump"`.

### 2c. WinForm UI additions

Two controls in `MainForm_Layout`:

1. **Status-bar link** `lnkOutputDump` (next to `lnkAnalysisReport` and `lnkCalibCheck`):
   - Text: `Output Dump`
   - Click opens the file in the default OS handler (`Process.Start(path)` with `UseShellExecute = True`).
   - If the file doesn't exist yet, show a message "Output dump is empty or disabled."

2. **Clear-output-dump button**: rendered as a second link `lnkClearOutputDump` immediately to the right of the `Output Dump` link:
   - Text: `Clear Output Dump`
   - Click shows a `MessageBox` confirm: `"Clear analysis_output_dump.md? This cannot be undone."` Yes proceeds, No cancels.
   - On confirm: truncate the file (delete + recreate empty, or write empty contents).
   - Status feedback: the link's tooltip briefly updates to "Cleared at HH:MM:SS" after successful clear.

The two links use the same status-bar style as the existing `Output Dump` / `Calibration Check` links — small, dim grey when idle, hover-highlighted.

### 2d. Writer behaviour

New helper class `AnalysisOutputDump`:

```vb
Public Class AnalysisOutputDump
    ' Append the rendered output to the dump file, prefixed with a markdown
    ' H2 header carrying the run timestamp.
    ' Best-effort write: never throws to caller. Failure logs to console.
    Public Shared Sub Append(timestamp As DateTime, renderedText As String,
                              dumpPath As String, maxMb As Integer)
        ...
    End Sub

    ' Truncate the dump file to empty.
    Public Shared Sub Clear(dumpPath As String)
        ...
    End Sub
End Class
```

**Append flow:**
1. If `output_dump_enabled = false`, return immediately (no I/O).
2. If `maxMb > 0` and existing file size > `maxMb × 1024 × 1024`, rename to backup with timestamp suffix and start fresh.
3. Open the file in append mode.
4. Write the H2 header line with `timestamp.ToString("yyyy-MM-dd HH:mm:ss")`.
5. Write the rendered text body.
6. Write a blank line then `---` then blank line for the separator.
7. Close.

**Source of the rendered text:** route the existing `txtOutput` RTF accumulation through a `StringBuilder` mirror in `RenderOutputHeader` / `RenderOutput`. Each `AppendRtf` call also appends to the mirror without RTF colour codes. After `RenderOutput` completes, the mirror's `.ToString()` is the plain-text equivalent passed to `AnalysisOutputDump.Append`.

Alternative implementation: read the entire `txtOutput.Text` after render completes (skipping the RTF parse). Cleaner — single read at end rather than parallel accumulator. **Use this approach.**

### 2e. Concurrency and resilience

- File access uses `FileStream` with `FileShare.Read` so the user can open the file externally while the engine is appending.
- Write failures (file locked, disk full) log to `Console.WriteLine` and do not propagate — the analysis run must never abort on dump-write failure. Same pattern as `AnalysisLogger.LogRun`.
- The dump write runs on the analysis thread, immediately after `RenderOutput` returns. No async needed — a typical 3 KB write completes in microseconds.

### 2f. Build / gitignore

Add to `.gitignore`:

```
bin/Debug/net8.0-windows/analysis_output_dump.md
bin/Debug/net8.0-windows/analysis_output_dump.*.md.bak
bin/Release/net8.0-windows/analysis_output_dump.md
bin/Release/net8.0-windows/analysis_output_dump.*.md.bak
```

---

## 3. Storage expectations (informational)

Approximate sizing for default settings:

| Auto-run interval | Output size / run | Daily | Monthly |
|---|---|---|---|
| 60s | ~3 KB | ~4 MB | ~130 MB |
| 30s | ~3 KB | ~9 MB | ~260 MB |
| Manual only | — | depends on use | — |

Plain text on a modern SSD: no performance concern up to several GB. The `Output Dump` link opens the file in the OS default viewer; large files (>100 MB) may load slowly in GUI viewers — use a terminal `tail` or split-window editor for those cases. The Clear button is the primary maintenance mechanism.

When an LLM (e.g. the auto-tweaker reviewer, or a Claude session) inspects the file:

- A single `Read` of a multi-hundred-MB file would exceed context. Don't do that.
- `Grep` for a specific run timestamp or pattern is the right access mode — handles arbitrary file size.
- The `## Run <timestamp>` header pattern is structured enough to slice by date range with `awk` or similar if needed.

---

## 4. Out of Scope

- Per-session segmented files (Asia / London / NY). Single-file approach chosen for simplicity.
- Per-run separate files. Would create thousands of small files; rejected.
- JSON or other structured per-run record. The structured representation is `analysis_log.csv`; this spec deliberately captures the unstructured rendered text.
- Diff highlighting between consecutive runs. Possible future enhancement; out of scope now.
- Cloud sync / remote upload. The file stays local.
- Encryption / access control. The file contains the same information visible on screen — not sensitive.

---

## 5. Acceptance

- `dotnet build` clean.
- On engine startup with `output_dump_enabled = true` and no existing dump file: file is created on the first analysis run.
- After 5+ analysis runs: dump file contains 5 blocks, each headed by a timestamp matching the engine display and separated by `---`.
- `Output Dump` link opens the file in the OS default markdown viewer (or notepad).
- `Clear Output Dump` link prompts for confirmation; on Yes, file is truncated to empty; on No, file is unchanged.
- With `output_dump_enabled = false`: no file is created, no I/O happens, both links still function (open shows the "empty or disabled" message; clear succeeds silently on an empty file).
- With `output_dump_max_mb = 5` and accumulated file > 5 MB: next write triggers rename to backup, fresh file starts.
- Existing `analysis_log.csv` behaviour is unaffected.
- No spec-rejected patterns introduced.

---

## 6. Implementation notes

- The mirror-accumulator approach is brittle if a future change adds new `AppendRtf` callers that bypass the mirror. The single-read-of-`txtOutput.Text` approach is more robust — do it that way.
- `txtOutput.Text` is the plain-text view of the RichTextBox. It strips RTF codes naturally. Use this directly.
- Render order: the dump should capture the verdict header AND the indicator sections AND the signal breakdown table — i.e., the full content the user sees after `RenderOutput` returns. Place the `AnalysisOutputDump.Append` call at the end of `RenderOutput` in `MainForm_Render_Sections.vb`, after the breakdown table has been appended.
- The timestamp used in the header should be the engine's run timestamp (the one that appears in the `TIME:` line of the rendered output), not `DateTime.Now` at write time — they're usually the same but should be sourced consistently.
