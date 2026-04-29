# Spec: AnalysisLogger CSV Column Expansion
**Proposed:** 2026-04-29
**Status:** PROPOSED — pending user approval
**Target files:** `Core/ScoringEngine_Types.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `AnalysisLogger.vb`, `UI/MainForm_Render_Header.vb`

This is a **mechanical schema-expansion pass**. No scoring change. No new indicators. Adds three columns to `analysis_log.csv` so downstream calibration analysis can correlate already-shipped feature outputs with subsequent price action, and updates the `CalibrationReport` to surface basic aggregates over the new columns.

Closes Section 16.3 prerequisite item 4 ("CSV columns expanded") in `docs/DeribitIndicatorProject.md`.

---

## 1. Problem Statement

`AnalysisLogger.LogRun` writes a fixed-schema CSV row per analysis. The current header covers core indicator outputs, scores, regime, and ATR levels. It does **not** include three values that materially affect verdict interpretation and have been on the deferred-logging list since the features shipped:

- **`VerdictContext`** (CONFIRMED / FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK) — Step 5b classifier, set on every `VerdictResult` since 2026-04-14. Without this column, you can count how often each context tag fires but can't correlate the tag with subsequent 5–15 min price action.
- **`FundingMomentum`** (RISING / FALLING / FLAT) — `IndicatorResults.FundingMomentum`, computed from rolling history since 2026-04-20. Without this column, Step 3b modifier effectiveness can't be validated empirically.
- **`OiCvdOutcome`** — Pass 2b cross-confirm gate result per run. Currently captured only as text in the `OI Delta` breakdown note string. Promoting it to a structured value lets calibration analysis count confirmation/conflict events directly.

These are the three items called out as deferred CSV columns in `docs/DeribitIndicatorProject.md` Section 12 (Calibration Backlog) and `docs/post-websocket-post-calibration-backlog.md` Section B3.

The schema-change reasoning (`docs/DeribitIndicatorProject.md` Section 16.3 item 4): once calibration accumulation begins (~300+ rows across 3+ sessions over ~3-6 weeks), changing the CSV schema mid-stream invalidates prior rows. The expansion must land **before** calibration starts, not during.

Zero new API calls. No new indicator computation. No scoring change.

---

## 2. New Field: `VerdictResult.OiCvdOutcome`

`VerdictContext` and `FundingMomentum` are already exposed as properties:
- `VerdictResult.VerdictContext As String` (in `Core/ScoringEngine_Types.vb`)
- `IndicatorResults.FundingMomentum As String` (in `Core/IndicatorResults.vb`)

These can be logged directly. No code change needed beyond `AnalysisLogger`.

The OI×CVD Pass 2b outcome is **not** currently exposed — it lives only in the local `oiCvdNote` string within `RunScoringPipeline`. Promote it to a structured field.

### 2a. Add to `Core/ScoringEngine_Types.vb` `VerdictResult` class

```vb
''' <summary>
''' Pass 2b OI x CVD cross-confirm gate outcome for this run.
''' Values: "NONE" / "CONFIRMED_LONG" / "CONFIRMED_SHORT" / "CONFLICT_LONG" / "CONFLICT_SHORT".
''' "NONE" when the gate is disabled, OFI did not fire a level signal, or no qualifying
'''   alignment/conflict was detected.
''' Set by RunScoringPipeline at Pass 2b. Display-only impact (already surfaced in the
''' OI Delta breakdown note); CSV-loggable for calibration analysis.
''' </summary>
Public Property OiCvdOutcome As String = "NONE"
```

Default `"NONE"` so unset cases (gate disabled or no qualifying signal) log a clean default rather than empty string.

### 2b. Set the field in `RunScoringPipeline` Pass 2b

The existing Pass 2b block in `Core/ScoringEngine_Calculate_Scoring.vb` already branches into four paths (confirm long / confirm short / conflict long / conflict short). Set `res.OiCvdOutcome` in each branch.

**Existing Pass 2b (snippet):**
```vb
If cfg.Indicators.OiCvd.Enabled Then
    Dim cvdBullish As Boolean = (r.CVDSlope = "RISING" AndAlso r.CVDValue > 0)
    Dim cvdBearish As Boolean = (r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)

    If (oiLong OrElse oiLongUpgraded) AndAlso cvdBullish Then
        state.LongScore = Math.Min(state.LongScore + cfg.Indicators.OiCvd.UpgradeBonus, regimeMax)
        oiCvdNote = String.Format(" | PASS2b: +{0}[L] OI×CVD confirmed", cfg.Indicators.OiCvd.UpgradeBonus)
    ElseIf (oiShort OrElse oiShortUpgraded) AndAlso cvdBearish Then
        ' ... etc ...
    End If
End If
```

**After:** add `res.OiCvdOutcome = "..."` in each branch:

```vb
If cfg.Indicators.OiCvd.Enabled Then
    Dim cvdBullish As Boolean = (r.CVDSlope = "RISING" AndAlso r.CVDValue > 0)
    Dim cvdBearish As Boolean = (r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)

    If (oiLong OrElse oiLongUpgraded) AndAlso cvdBullish Then
        state.LongScore = Math.Min(state.LongScore + cfg.Indicators.OiCvd.UpgradeBonus, regimeMax)
        oiCvdNote = String.Format(" | PASS2b: +{0}[L] OI×CVD confirmed", cfg.Indicators.OiCvd.UpgradeBonus)
        res.OiCvdOutcome = "CONFIRMED_LONG"
    ElseIf (oiShort OrElse oiShortUpgraded) AndAlso cvdBearish Then
        state.ShortScore = Math.Min(state.ShortScore + cfg.Indicators.OiCvd.UpgradeBonus, regimeMax)
        oiCvdNote = String.Format(" | PASS2b: +{0}[S] OI×CVD confirmed", cfg.Indicators.OiCvd.UpgradeBonus)
        res.OiCvdOutcome = "CONFIRMED_SHORT"
    ElseIf oiLong AndAlso cvdBearish Then
        state.LongScore = Math.Max(0, state.LongScore - cfg.Indicators.OiCvd.ConflictPenalty)
        oiCvdNote = String.Format(" | PASS2b: -{0}[L] OI×CVD conflict", cfg.Indicators.OiCvd.ConflictPenalty)
        res.OiCvdOutcome = "CONFLICT_LONG"
    ElseIf oiShort AndAlso cvdBullish Then
        state.ShortScore = Math.Max(0, state.ShortScore - cfg.Indicators.OiCvd.ConflictPenalty)
        oiCvdNote = String.Format(" | PASS2b: -{0}[S] OI×CVD conflict", cfg.Indicators.OiCvd.ConflictPenalty)
        res.OiCvdOutcome = "CONFLICT_SHORT"
    End If
End If
```

The default `"NONE"` is preserved when none of the four branches match (gate disabled, OFI didn't fire, or no qualifying alignment/conflict). Don't add an explicit `Else` — the default initializer on the property handles it.

---

## 3. AnalysisLogger Changes

### 3a. Header — append three columns at the **end**

Append, don't insert. Existing column positions preserved keeps any external readers happy.

**Existing header tail (line 48):**
```
"ATR,ATRMultiplier"
```

**After:**
```
"ATR,ATRMultiplier," &
"VerdictContext,FundingMomentum,OiCvdOutcome"
```

### 3b. Data row — append three values matching column positions

In `LogRun`, after the existing final field `r.ATRSizeMultiplier.ToString("F4")`:

```vb
sw.WriteLine(String.Join(",",
    ts,
    ' ... existing 70+ fields unchanged ...
    r.ATR.ToString("F4"),
    r.ATRSizeMultiplier.ToString("F4"),
    If(v.VerdictContext, "CONFIRMED"),
    If(r.FundingMomentum, "FLAT"),
    If(v.OiCvdOutcome, "NONE")))
```

The `If(value, default)` pattern guards against null strings on cold-start runs (e.g., FundingMomentum is "FLAT" when history < 2 samples; the property is non-null but the guard is cheap insurance).

### 3c. Update the `v0.2` comment block

Add a `v0.3` comment to the file header noting the schema change:

```vb
' v0.3: Header expanded with VerdictContext, FundingMomentum, OiCvdOutcome columns
'       (closes Section 16.3 prerequisite item 4 — auto-tweaker calibration data).
'       Existing CSV files written by v0.2 are column-incompatible.
'       Use ResetLog() (Reset Log link in UI) after deploying this version.
```

---

## 4. CalibrationReport Read-Side Updates

`MainForm_Render_Header.BuildCalibrationReport()` already builds a `colIdx` dictionary from the header, so adding new columns is straightforward — read by name, not by position.

### 4a. Add aggregate counters

In the parsing loop (after the existing `Liq events` and `OFI` parsers), add:

```vb
Dim contextCounts As New Dictionary(Of String, Integer) From {
    {"CONFIRMED", 0}, {"FLOW_UNCONFIRMED", 0},
    {"MOMENTUM_FADING", 0}, {"STRUCTURALLY_WEAK", 0}
}
Dim fundingMomCounts As New Dictionary(Of String, Integer) From {
    {"RISING", 0}, {"FALLING", 0}, {"FLAT", 0}
}
Dim oiCvdCounts As New Dictionary(Of String, Integer) From {
    {"NONE", 0}, {"CONFIRMED_LONG", 0}, {"CONFIRMED_SHORT", 0},
    {"CONFLICT_LONG", 0}, {"CONFLICT_SHORT", 0}
}
```

In the per-row loop:

```vb
If colIdx.ContainsKey("VerdictContext") Then
    Dim ctx = parts(colIdx("VerdictContext")).Trim().ToUpper()
    If contextCounts.ContainsKey(ctx) Then contextCounts(ctx) += 1
End If
If colIdx.ContainsKey("FundingMomentum") Then
    Dim mom = parts(colIdx("FundingMomentum")).Trim().ToUpper()
    If fundingMomCounts.ContainsKey(mom) Then fundingMomCounts(mom) += 1
End If
If colIdx.ContainsKey("OiCvdOutcome") Then
    Dim oicvd = parts(colIdx("OiCvdOutcome")).Trim().ToUpper()
    If oiCvdCounts.ContainsKey(oicvd) Then oiCvdCounts(oicvd) += 1
End If
```

`If colIdx.ContainsKey(...)` guards against reading old log files that don't have the columns — graceful degradation when older logs are present.

### 4b. Render new sections

After the existing `INDICATOR VARIANCE` block and before the closing `===` divider, add:

```vb
sb.AppendLine()
sb.AppendLine("VERDICT CONTEXT DISTRIBUTION")
For Each kvp In contextCounts
    sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
Next
sb.AppendLine()
sb.AppendLine("FUNDING MOMENTUM DISTRIBUTION")
For Each kvp In fundingMomCounts
    sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
Next
sb.AppendLine()
sb.AppendLine("OI x CVD PASS 2b OUTCOMES")
For Each kvp In oiCvdCounts
    sb.AppendLine("  " & kvp.Key.PadRight(20) & " : " & kvp.Value.ToString().PadLeft(5) & " rows")
Next
```

These are aggregate-only — no per-tag accuracy correlation yet (that requires storing subsequent price action per row, which is future work). Aggregates are enough to validate the columns are populating correctly during the smoke test, and to spot obvious distribution issues (e.g., MOMENTUM_FADING never firing → maybe a bug).

---

## 5. Migration / Reset Workflow

The new schema breaks compatibility with existing `analysis_log.csv` files (different column count). Same pattern as the v0.2 → v0.1 break — handled via the existing `ResetLog()` UI affordance.

**On first run with v0.3:**
- If `analysis_log.csv` is empty or missing → header gets written from the new schema. Clean.
- If `analysis_log.csv` exists with v0.2 schema → `LogRun` will append a v0.3-shaped row to a v0.2 header. **CSV becomes structurally inconsistent.** User must hit "Reset Log" before logging starts, or pre-delete the file.

The existing `Header` constant is checked against `File.Exists(path) OrElse New FileInfo(path).Length = 0` — so an empty file reinitialises with the new header automatically.

**Recommended user workflow per release notes:**
1. Note current log row count (see status bar).
2. Pull the v0.3 build.
3. Click "Reset Log" before the first analysis — gives a clean v0.3 file from row 1.
4. Begin calibration accumulation against the new schema.

A defensive enhancement (optional): on first read in `BuildCalibrationReport`, check whether the header has the new columns and warn if not. Skipping for v0.3 to keep scope tight; user already knows from release notes.

---

## 6. Worked Examples

### Example A — CONFIRMED verdict, no Pass 2b activity, FLAT funding

```
Header: ...,ATR,ATRMultiplier,VerdictContext,FundingMomentum,OiCvdOutcome
Row:    ...,12.83,0.59,CONFIRMED,FLAT,NONE
```

### Example B — FLOW_UNCONFIRMED, RISING funding, OI×CVD CONFLICT_LONG

```
Row:    ...,15.21,0.71,FLOW_UNCONFIRMED,RISING,CONFLICT_LONG
```

This row tells the auto-tweaker: structural signals fired but flow didn't agree, funding momentum is building toward crowding, and OI×CVD specifically said the long thesis is weak. Three independent diagnoses of the same caution. The downstream price action correlates against this combined classification.

### Example C — Cold-start, momentum FLAT, no Pass 2b

```
Row:    ...,10.04,0.48,STRUCTURALLY_WEAK,FLAT,NONE
```

First few runs of a session, swing detection might not have produced a structural target yet → STRUCTURALLY_WEAK fires. Funding history < 2 samples → FLAT (per `CalcFundingMomentum` cold-start fallback). Pass 2b didn't activate (default NONE). All graceful.

### Example D — CalibrationReport summary after 50 runs

```
VERDICT CONTEXT DISTRIBUTION
  CONFIRMED            :    18 rows
  FLOW_UNCONFIRMED     :    14 rows
  MOMENTUM_FADING      :     7 rows
  STRUCTURALLY_WEAK    :    11 rows

FUNDING MOMENTUM DISTRIBUTION
  RISING               :     5 rows
  FALLING              :     3 rows
  FLAT                 :    42 rows

OI x CVD PASS 2b OUTCOMES
  NONE                 :    36 rows
  CONFIRMED_LONG       :     6 rows
  CONFIRMED_SHORT      :     4 rows
  CONFLICT_LONG        :     2 rows
  CONFLICT_SHORT       :     2 rows
```

Quickly spotcheck-able: distributions look plausible. If MOMENTUM_FADING was 0 across 50 runs, something's off. If OiCvdOutcome was 100% NONE, the gate isn't firing in practice.

---

## 7. Files Changed Summary

| File | Change |
|---|---|
| `Core/ScoringEngine_Types.vb` | Add `OiCvdOutcome As String = "NONE"` property to `VerdictResult` |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Set `res.OiCvdOutcome` in each of the 4 Pass 2b branches |
| `AnalysisLogger.vb` | Append 3 columns to `Header` constant; append 3 values in `LogRun` write; update file-level comment block to note v0.3 schema break |
| `UI/MainForm_Render_Header.vb` | Add 3 column parsers + 3 distribution sections in `BuildCalibrationReport` |

Approximate line count: ~30 lines added across the four files. No file deletions, no class structure changes.

---

## 8. Settings Keys

**None.** This spec is schema-only — no new tunables.

---

## 9. What This Does NOT Do

- Does **not** add per-tag accuracy correlation (e.g. "of 14 FLOW_UNCONFIRMED tags, how many were followed by adverse 5-min price?"). That requires either a follow-up logging pass capturing post-analysis price (T+5min, T+15min) or an offline analysis script. Out of scope for v0.3.
- Does **not** add a Pass 2c regime-alignment outcome column. Pass 2c was not on the deferred-logging list in Section 16.3 / B3. If calibration data shows it would help, add as a v0.4 follow-up.
- Does **not** change scoring, MTF gate, Kelly, regime classification, or any indicator. Pure logging expansion.
- Does **not** auto-detect schema mismatches in `BuildCalibrationReport`. Reader uses `colIdx.ContainsKey` guards — gracefully skips missing columns. User is responsible for `ResetLog()` per release notes.
- Does **not** bump `settings.json` version — no settings change.

---

## 10. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors.
2. **Reset existing log:** click "Reset Log" in UI to clear the v0.2 schema.
3. **Smoke test (10–20 analyses across at least 2 distinct VerdictContext outcomes):**
   - Click Analyse Now repeatedly across a session, ideally hitting at least one CONFIRMED verdict and at least one non-CONFIRMED context.
   - Inspect `analysis_log.csv` directly (text editor or Excel): confirm the 3 new columns are present at the end and populated.
   - Verify that values match what the in-app display shows for the same run (CONTEXT line in header block, Funding momentum row in FUNDING section, OI Delta breakdown note's `PASS2b:` suffix).
4. **CalibrationReport check:** click the calibration check link. The new sections (`VERDICT CONTEXT DISTRIBUTION`, `FUNDING MOMENTUM DISTRIBUTION`, `OI x CVD PASS 2b OUTCOMES`) must appear and counts must sum to the total row count.
5. **Cold-start check:** restart the engine fresh. First analysis should log `FundingMomentum=FLAT` (history < 2 samples). Verify.

If any of (3)–(5) fail, do not proceed to calibration accumulation until the discrepancy is fixed.

---

## 11. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should new columns be inserted at a logical position in the header, or appended at end? | **Appended at end.** Preserves existing column positions for any external readers (Excel, scripts, downstream parsers). Header is read by name in CalibrationReport so position doesn't matter internally | Resolved |
| Q2 | Should `OiCvdOutcome` distinguish confirmed/conflict by direction (LONG/SHORT), or just by category? | **By direction.** Auto-tweaker analysis benefits from knowing whether a confirmation was on the long or short side. 5 values is fine — small enum | Resolved |
| Q3 | Should `OiCvdOutcome` default to `"NONE"` or empty string? | **`"NONE"`.** Property default initializer is cleaner than null guards everywhere. Calibration parser dictionary handles `"NONE"` natively | Resolved |
| Q4 | Should we also log Pass 2c regime alignment outcome while we're here? | **No** — out of scope for this spec. Section 16.3 / B3 listed three columns; this spec ships those three. Pass 2c logging is a clean v0.4 follow-up if calibration data shows it would add value | Resolved |
| Q5 | Should `BuildCalibrationReport` warn the user when reading a log file with v0.2 (pre-expansion) header? | **No** — the `colIdx.ContainsKey` guards already gracefully skip missing columns. Adding a warning is scope creep. User knows from release notes | Resolved |
| Q6 | Should the `VerdictContext` value `"CONFIRMED"` be logged or be left blank (since CONFIRMED is the "no warning" case in display)? | **Logged as `"CONFIRMED"`.** The CSV is for analysis, not display. Empty strings make distribution counting harder. The display-side suppression of CONFIRMED is a separate concern | Resolved |
| Q7 | Header constant ordering — alphabetical? functional grouping? | **Match existing style** — append at end. Existing header is loosely functionally grouped. A reorganisation pass is out of scope | Resolved |
