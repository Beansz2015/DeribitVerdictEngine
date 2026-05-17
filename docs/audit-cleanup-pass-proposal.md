# Audit Cleanup Pass — Proposal

**Status:** ✅ IMPLEMENTED — 2026-05-17
**Settings.json:** unchanged (no new keys, no version bump)
**Spec dependency:** None. All changes are internal code fixes.

---

## Motivation

The 2026-05-17 audit pass (`A: OHLC cache + auto-tweaker first-fire` + `B: calibration tooling readiness`) surfaced four small bugs. Two are crash-safety hardening; two are post-v30 enum drift that silently undercounts. All are internal code-only fixes — no behaviour change at the happy path, no settings.json change, no schema change.

Bundling into one commit because the fixes are independently small and share the spec-and-review overhead.

Findings folded in:
- **A-MED1** `TweakerState.Save` not atomic — mid-write crash wipes auto-tweaker accumulated state (LastEvaluatedRowIndex, streak counter, snapshot tracking, PickedCellHistory, RoundHistory). Next `Load` returns defaults.
- **A-LOW1** `OhlcCache.WriteAll` + `RollingTrim` not atomic — mid-write crash wipes the OHLC cache. Recovery is graceful (full 7-day re-fetch on next startup) but ~30 seconds slow.
- **B-LOW1** `BuildCalibrationReport.contextCounts` initialised with 4 keys (CONFIRMED / FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK). Post-v30 NO TRADE rows write the new "ALIGNED" value, which fails the `ContainsKey` guard and silently doesn't increment. CONTEXT DISTRIBUTION undercounts by the number of NO TRADE rows.
- **B-LOW2** `AnalysisRunner` `VerdictContext × Outcome` cross-tab hardcodes the same 4-value enum at line 88. ALIGNED rows are currently masked by the upstream "NO TRADE" filter so no count error today, but enum/filter divergence is brittle.

---

## Design changes

### 1. Atomic write for `TweakerState.Save` (A-MED1)

[tools/AutoTweaker/TweakerState.vb:129-140](tools/AutoTweaker/TweakerState.vb:129)

Current code uses `File.WriteAllText` — truncate-then-write. Mid-write crash leaves an empty or partial JSON that triggers the defaults-reset on next Load.

Replace with write-to-tmp + atomic rename:

```vb
Public Shared Sub Save(path As String, state As TweakerState)
    ' Cap round history before persisting.
    If state.RoundHistory IsNot Nothing AndAlso state.RoundHistory.Count > RoundHistoryCap Then
        state.RoundHistory = state.RoundHistory.
            Skip(state.RoundHistory.Count - RoundHistoryCap).ToList()
    End If

    Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
    Dim dir = IO.Path.GetDirectoryName(path)
    If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)

    ' Atomic write: persist to .tmp, then rename. NTFS rename is atomic at the
    ' filesystem level — a mid-write crash either leaves the original file
    ' intact (rename never happened) or the new file in place (rename completed).
    ' Avoids the failure mode where File.WriteAllText is killed mid-write and
    ' leaves a partial state.json that triggers a defaults reset on Load.
    Dim tmpPath As String = path & ".tmp"
    Try
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(state, opts))
        If File.Exists(path) Then
            File.Replace(tmpPath, path, Nothing)
        Else
            File.Move(tmpPath, path)
        End If
    Catch
        Try : File.Delete(tmpPath) : Catch : End Try
        Throw
    End Try
End Sub
```

`File.Replace` is the canonical atomic Move-with-overwrite on NTFS. If the destination doesn't exist (first-ever save), falls back to plain `File.Move`. The Catch cleans up the .tmp file if the write itself failed, then re-throws so the caller still sees the error.

### 2. Atomic write for `OhlcCache.WriteAll` + `RollingTrim` (A-LOW1)

[OhlcCache.vb:82-94](OhlcCache.vb:82) and [OhlcCache.vb:101-123](OhlcCache.vb:101).

Same pattern. `WriteAll` uses `StreamWriter` so the tmp file is built incrementally; on success, atomic rename.

`WriteAll`:
```vb
Public Shared Sub WriteAll(path As String, bars As IEnumerable(Of OhlcBar))
    Dim tmpPath As String = path & ".tmp"
    Try
        Using sw As New StreamWriter(tmpPath, append:=False)
            sw.WriteLine(SCHEMA_COMMENT)
            sw.WriteLine(COL_HEADER)
            For Each bar In bars.OrderBy(Function(b) b.CloseTime)
                sw.WriteLine(FormatBar(bar))
            Next
        End Using
        If File.Exists(path) Then
            File.Replace(tmpPath, path, Nothing)
        Else
            File.Move(tmpPath, path)
        End If
    Catch ex As Exception
        Console.WriteLine("[OhlcCache] WriteAll failed: " & ex.Message)
        Try : File.Delete(tmpPath) : Catch : End Try
    End Try
End Sub
```

`RollingTrim`:
```vb
Public Shared Sub RollingTrim(path As String, maxBars As Integer)
    Try
        If Not File.Exists(path) Then Return
        Dim allLines = File.ReadAllLines(path)
        Dim header As New List(Of String)()
        Dim data   As New List(Of String)()
        For Each line In allLines
            If line.StartsWith("#") OrElse line.StartsWith("CloseTime") Then
                header.Add(line)
            ElseIf Not String.IsNullOrWhiteSpace(line) Then
                data.Add(line)
            End If
        Next
        If data.Count <= maxBars Then Return
        Dim kept = data.Skip(data.Count - maxBars).ToList()
        Dim result As New List(Of String)()
        result.AddRange(header)
        result.AddRange(kept)

        ' Atomic write — see WriteAll comment.
        Dim tmpPath As String = path & ".tmp"
        Try
            File.WriteAllLines(tmpPath, result)
            File.Replace(tmpPath, path, Nothing)
        Catch
            Try : File.Delete(tmpPath) : Catch : End Try
            Throw
        End Try
    Catch ex As Exception
        Console.WriteLine("[OhlcCache] RollingTrim failed: " & ex.Message)
    End Try
End Sub
```

Note: `OhlcCache.Append` (used for incremental persists) is **not** changed. Append-mode writes are inherently safer than truncate-mode — a mid-write crash truncates the last line at worst, doesn't wipe prior content. Out of scope for this spec.

### 3. CalibrationReport `contextCounts` add "ALIGNED" (B-LOW1)

[UI/MainForm_Render_Header.vb:118-121](UI/MainForm_Render_Header.vb:118):

```vb
Dim contextCounts As New Dictionary(Of String, Integer) From {
    {"CONFIRMED", 0}, {"FLOW_UNCONFIRMED", 0},
    {"MOMENTUM_FADING", 0}, {"STRUCTURALLY_WEAK", 0}
}
```

becomes:

```vb
Dim contextCounts As New Dictionary(Of String, Integer) From {
    {"CONFIRMED", 0}, {"ALIGNED", 0},
    {"FLOW_UNCONFIRMED", 0}, {"MOMENTUM_FADING", 0},
    {"STRUCTURALLY_WEAK", 0}
}
```

One line addition. The dict is consumed by [MainForm_Render_Header.vb:303](UI/MainForm_Render_Header.vb:303) which iterates the dict's KeyValuePairs to render the VERDICT CONTEXT DISTRIBUTION section — adding the key automatically gives ALIGNED its own row in the report.

### 4. AnalysisRunner `VerdictContext × Outcome` cross-tab add "ALIGNED" (B-LOW2)

[analysis/AnalysisRunner.vb:88](analysis/AnalysisRunner.vb:88):

```vb
For Each ctx In {"CONFIRMED", "FLOW_UNCONFIRMED", "MOMENTUM_FADING", "STRUCTURALLY_WEAK"}
```

becomes:

```vb
For Each ctx In {"CONFIRMED", "ALIGNED", "FLOW_UNCONFIRMED", "MOMENTUM_FADING", "STRUCTURALLY_WEAK"}
```

The ALIGNED rows will currently still be filtered out by the inner predicate `r.Verdict.ToUpper() <> "NO TRADE"` (the cross-tab only evaluates directional verdicts under barrier-hit semantics). So the new ALIGNED tier row will show as `n=0` until/unless the filter is relaxed in a future spec. The point of adding it now is to remove enum/filter divergence — if a future change writes ALIGNED on directional verdicts (e.g. as a secondary tier of CONFIRMED), the cross-tab won't silently drop those rows.

---

## Implementation steps

1. **`tools/AutoTweaker/TweakerState.vb`** — replace `Save` method body with the atomic-write version (§1). No interface change.
2. **`OhlcCache.vb`** — replace `WriteAll` and `RollingTrim` method bodies with atomic-write versions (§2). No interface change.
3. **`UI/MainForm_Render_Header.vb`** — add "ALIGNED" to the `contextCounts` initialiser dict (§3). One line.
4. **`analysis/AnalysisRunner.vb`** — add "ALIGNED" to the For-Each list at line 88 (§4). One line.
5. **Build clean.** `dotnet build "C:\Dev\DeribitVerdictEngine\DeribitVerdictEngine.sln"` from the repo root. 0 errors / 0 warnings on both DeribitVerdictEngine and AutoTweaker projects.
6. **Update docs.**
   - `docs/DeribitIndicatorProject.md` §15 — append a one-line v30.1 entry (no settings.json bump, but worth recording the cleanup).
   - Mark this spec file's Status header "✅ IMPLEMENTED".

No settings.json bump. No `change_log` entry inside settings.json (no config changes).

---

## Manual smoke test

1. **Build clean** — both projects 0/0.
2. **Atomic write self-test (TweakerState):**
   - Delete `tools/AutoTweaker/state.json` if it exists.
   - Run `tools/AutoTweaker/bin/Debug/net8.0/AutoTweaker.exe` once (the auto-tweaker should INELIGIBLE-exit quickly since fixed-window data hasn't accumulated). Confirm `state.json` is created cleanly.
   - Check no `state.json.tmp` lingers in the directory (Catch block should always clean up on success or failure).
3. **Atomic write self-test (OhlcCache):**
   - Launch the engine, let `InitialiseAsync` complete. Confirm `bin/Debug/net8.0-windows/ohlc_1m_cache.csv` exists and parses cleanly.
   - Check no `ohlc_1m_cache.csv.tmp` lingers after startup.
4. **CalibrationReport ALIGNED:**
   - Run a few analyses with `posState=None` to accumulate NO TRADE rows post-v30 (which write ALIGNED to the CSV).
   - Open the CalibrationReport via the status-bar `Calibration Readiness` link.
   - Verify the VERDICT CONTEXT DISTRIBUTION section shows an `ALIGNED` row with a non-zero count.
   - Confirm `(CONFIRMED + ALIGNED + FLOW_UNCONFIRMED + MOMENTUM_FADING + STRUCTURALLY_WEAK)` ≈ total directional + NO TRADE rows that have a VerdictContext.
5. **Analysis Report ALIGNED row:**
   - Click `Analysis Report` link, wait for OHLC fetch.
   - In the rendered markdown, find the VerdictContext × Outcome cross-tab. Confirm an `ALIGNED` row exists (will likely be `n=0` because of the NO TRADE filter — that's expected, the addition is for enum hygiene not new data).

---

## Risk

All changes are internal:

| Change | Behavioural risk |
|---|---|
| Atomic state.json write | None at happy path. New failure mode: tmp file lingers if process crashes BETWEEN writing tmp and renaming. Negligible — it'll be overwritten on next save. |
| Atomic OHLC cache write | Same. |
| contextCounts ALIGNED key | None — adds a row to a report. |
| AnalysisRunner ALIGNED entry | None — adds an iteration that currently produces n=0 due to NO TRADE filter. |

No CSV schema change. No settings.json change. No scoring/verdict impact. The changes are detectable in the rendered output only by the presence of the new "ALIGNED" row in the two report sections.

---

## Out of scope (deferred per audit triage)

- **A-LOW2 — concurrent `UpdateAsync` race.** Needs a 30-second source check on `btnAnalyze_Click` to verify re-entry is gated. If not gated, would add a `_inFlight` flag at the top of `RunAnalysisAsync`. Defer until verified.
- **A-LOW3 — out-of-order OHLC + `RollingTrim`.** Narrow conditions; currently mitigated by gap-fill canonicalisation. Defensive sort in `RollingTrim` (sort `data` by parsed CloseTime before `Skip(N - maxBars)`) is a one-line hardening — defer until observed in the wild.
- **Cleanup of `AnalysisRunner.Run`'s unused `cfg` parameter.** Dead parameter cargo from an older signature. Defer to a future refactor pass.

---

## Commit message

```
fix: audit cleanup pass — atomic state writes, ALIGNED enum coverage

Four small fixes bundled from the 2026-05-17 audit (A: OHLC + auto-tweaker
first-fire, B: calibration tooling readiness):

- TweakerState.Save: write to .tmp + File.Replace for atomic persist. Avoids
  wiping accumulated state on mid-write process kill.
- OhlcCache.WriteAll / RollingTrim: same atomic-write pattern.
- BuildCalibrationReport.contextCounts: add "ALIGNED" key. Post-v30 NO TRADE
  rows were silently dropped from the CONTEXT DISTRIBUTION count.
- analysis/AnalysisRunner.vb: add "ALIGNED" to the VerdictContext × Outcome
  enum list. No count change today (NO TRADE filter masks ALIGNED rows) but
  removes enum/filter divergence.

Zero behaviour change at happy path. No CSV schema change. No settings.json
change. Build clean.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```
