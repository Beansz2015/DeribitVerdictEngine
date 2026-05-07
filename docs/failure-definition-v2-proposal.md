# Spec: Failure Definition v2 — Barrier-Hit with Adverse Stop + Deribit OHLC Fetch
**Proposed:** 2026-05-07
**Status:** PROPOSED 2026-05-07
**Target files:** `analysis/AnalysisConstants.vb`, `analysis/ForwardReturnJoiner.vb` (rename to `ForwardWindowJoiner.vb`), `analysis/FailureRateMatrix.vb`, `analysis/AnalysisRunner.vb`, `analysis/AnalysisReport.vb`, `analysis/MarkdownReportWriter.vb`, `analysis/DeribitOhlcFetcher.vb` (new), `tools/AutoTweaker/AutoTweakerCore.vb`, `DeribitClient.vb` (extend `GetCandlesAsync` to support explicit time range if needed), `analysis/AnalysisReportForm.vb` (display update)
**Supersedes:** `failure-definition-proposal.md` (v1)
**Prerequisites:** `csv-expansion-v0.4-proposal.md` shipped, `analysis-script-proposal.md` shipped, `auto-tweaker-pipeline-proposal.md` shipped

---

## 1. Background

The v1 failure definition measures **fixed-horizon return** — was the price at exactly T+5/T+10/T+15 minutes adverse to the verdict by more than `threshold × ATR`? It was a reasonable starting framework but has three issues against the trader's actual trading style:

1. **Wrong P&L semantic.** The trader places limit orders and watches live, exiting when the price hits the take-profit threshold. This is a **barrier-hit** event that can occur any time within the window, not just at the window-end timestamp.
2. **Asymmetric measurement.** v1 marks a verdict as "passed" if price stays flat — but a flat verdict that prompted a long entry costs fees and slippage. The trader treats flat outcomes as failed trades.
3. **Excludes too many rows.** When the engine is off after a verdict (no row at T+W±30s in CSV), v1 silently drops the verdict from the failure-rate denominator — undercounting the sample even when forward price data could be retrieved from Deribit's API.

v2 reframes the metric to match actual trading P&L:

- **Success:** the favourable barrier was hit by an intra-bar wick within the window
- **Failure:** the adverse barrier (structural stop or fallback ATR multiple) was hit first, or neither barrier was hit before the window expired
- **Data source:** Deribit OHLC bulk fetch, replacing the CSV-row close-only proxy

This makes the auto-tweaker's optimisation target literally "would I have made money trading the verdicts as they fired?" — directly aligned with `trader-profile.md` Section 5 and Section 6.

---

## 2. Specification

### 2a. Failure definition (barrier-hit with adverse stop)

For row N with verdict tier T, hold window W (minutes), and threshold multiplier `thr`:

**Inputs:**
- `entry = row.Price` (close of the candle at row.Timestamp)
- `atr = row.ATR` (logged as CSV column 64)
- `direction = LONG | SHORT` from tier
- `eligibleBars = OHLC bars closing at row.Timestamp + 3 min through row.Timestamp + W min`

**Barriers:**
- **Favourable barrier (per cell):**
  - LONG: `favBar = entry + thr × atr`
  - SHORT: `favBar = entry − thr × atr`
- **Adverse barrier (single per row, structural-first with ATR fallback):**
  - If `row.SwingStopLong > 0` (LONG) → `advBar = row.SwingStopLong`
  - Else (LONG) → `advBar = entry − cfg.Scoring.AtrStopMultiplier × atr` (default `1.2 × atr`)
  - Mirror for SHORT (uses `row.SwingStopShort` then `entry + AtrStopMultiplier × atr`)

**Walk eligible bars in chronological order:**

```
For each bar in eligibleBars (oldest → newest):
    favHit = (LONG ? bar.High >= favBar : bar.Low  <= favBar)
    advHit = (LONG ? bar.Low  <= advBar : bar.High >= advBar)

    If favHit AND advHit:
        Return FAILURE   // ambiguous within bar — conservative bias
    ElseIf favHit:
        Return SUCCESS
    ElseIf advHit:
        Return FAILURE
End For

Return FAILURE   // window expired without favourable hit
```

**Conservative ambiguous-bar rule:** when both barriers are touched within the same 1m candle (high reaches `favBar` AND low reaches `advBar`), the outcome is marked **failure**. Sub-minute order is unknown, and trader-profile §6 prefers conservative bias.

**Why structural stop first, then ATR fallback:** trader-profile §5 says "Stop-loss: structural — below previous swing low (longs) / above previous swing high (shorts)." The engine logs swing stops in CSV columns 82–83 when pivot data is sufficient. When unavailable (early session, deep chop), the engine's own default ATR stop (`AtrStopMultiplier`) is the right fallback because it's what the engine itself displays as a default trade plan.

### 2b. Same-bar / next-bar exclusion

Eligible bars start at `row.Timestamp + 3 min`. The first two bars after the verdict (bars closing at T+1 and T+2) are excluded because they are too quick to execute an entry, watch the price, hit the threshold, and exit.

| Window | Eligible bars (closes at) | Bar count |
|---|---|---|
| 5 min | T+3, T+4, T+5 | 3 |
| 10 min | T+3 … T+10 | 8 |
| 15 min | T+3 … T+15 | 13 |

The 5m window is tight (3 bars). Cells where 5m yields too few barrier resolutions will naturally produce wide CIs and the picker (lowest CI width subject to `n ≥ 30`) will prefer 10m / 15m for those tiers. Acceptable behaviour.

### 2c. Forward data source — Deribit OHLC bulk fetch (primary)

v1 used CSV-row closes via `±30s` timestamp lookup. That approach **systematically misses intra-bar wick hits** — exactly the cases where a trader watching live would have exited. v2 replaces this with a bulk OHLC fetch from Deribit, called once per analysis-run / once per auto-tweaker invocation.

**Fetch strategy:**

1. Iterate all CSV rows. Determine the earliest needed timestamp `start_ts = min(row.Timestamp) + 3 min` and latest needed `end_ts = max(row.Timestamp) + 15 min`.
2. Call `DeribitClient.GetCandlesAsync(resolution:="1", startTimestampMs:=start_ts, endTimestampMs:=end_ts)`.
3. Build an in-memory dictionary keyed by minute-aligned timestamp → OHLC. Single pass.
4. For each row, slice the relevant bars (T+3 through T+W per window) from the dictionary.
5. If a particular bar is missing from the response (Deribit gap), mark `WindowValid(w) = False` for that window of that row — exclude only that cell, not the whole row.

**Failure mode handling:**

- If the bulk fetch fails entirely (Deribit maintenance, network), the analysis run aborts gracefully:
  - Offline analysis: render report with banner `Forward-data fetch failed — report cannot be regenerated until Deribit is reachable.` No partial report.
  - Auto-tweaker: exit code 1 (`ERROR`), `last_run_outcome = ERROR`, summary line cites "Deribit OHLC fetch failed". Engine continues; auto-tweaker re-runs naturally on next cooldown clear.
- If `DeribitClient.GetCandlesAsync` doesn't currently support an explicit time range (existing signature is count-based per `architecture.md`), extend it. The Deribit `public/get_tradingview_chart_data` endpoint accepts `start_timestamp` and `end_timestamp` in ms — it's a parameter addition, not a behavioural change.

**Performance note:** typical auto-tweaker window is 120 verdicts. Bulk fetch covering ~120 minutes + 15 min = ~135 minutes of 1m candles ≈ 135 candle records. Single HTTP call. Well within Deribit's rate limits.

**Implementation file:** new `analysis/DeribitOhlcFetcher.vb` — thin wrapper around `DeribitClient.GetCandlesAsync` exposing a host-agnostic `FetchOhlcRange(startTs, endTs)` that returns `Dictionary(Of DateTime, OhlcBar)`. No `System.Windows.Forms` references. Reused by both `AnalysisRunner` (offline report) and `AutoTweakerCore`.

### 2d. ATR validation

Rows where `row.ATR <= 0` or missing must be **excluded** from the failure-rate denominator entirely. With `ATR = 0`, every barrier degenerates to the entry price and every bar trivially "hits" — meaningless data.

Implement in `FailureRateMatrix.Compute`:

```vb
For Each row In rows
    If row.ATR <= 0 Then Continue For   ' skip — no valid barrier computation
    Dim tier As String = ToTier(row.Verdict)
    If tier = "" Then Continue For
    ...
Next
```

Excluded rows are tracked in an informational counter (`AtrInvalidExcluded`) and reported in the markdown report's Section 1 Summary.

### 2e. Tier-asymmetric thresholds — swap from v1

v1's `{0.3, 0.5}` for STRONG vs `{0.5, 0.8}` for MEDIUM was correct under the **adverse-move-detection** semantic (smaller multiplier = catches smaller adverse moves = stricter). Under v2's **required-favourable-move** semantic, smaller multiplier = MORE LENIENT (smaller required profit move). To preserve the intuition that STRONG is held to a higher bar, the values must swap.

```vb
' analysis/AnalysisConstants.vb (v2 values)
Public ReadOnly StrongAtrThresholds As Double() = {0.5, 0.8}
Public ReadOnly MediumAtrThresholds As Double() = {0.3, 0.5}
```

**Reading under v2 semantic:**

- `STRONG_LONG × 0.5` — STRONG verdicts must produce a +0.5×ATR favourable wick within the eligible window. Tighter standard for higher-conviction predictions.
- `MEDIUM_LONG × 0.3` — MEDIUM verdicts pass on a smaller +0.3×ATR favourable wick. Looser standard for moderate-conviction predictions.
- Two thresholds per tier: as in v1, gives the picker flexibility — sometimes the higher threshold produces a more stable failure rate, sometimes the lower one does.

### 2f. Adverse barrier fallback constant

Add to `AnalysisConstants.vb`:

```vb
' Used when row.SwingStopLong / row.SwingStopShort is 0 (no swing data).
' Matches the engine's default cfg.Scoring.AtrStopMultiplier (1.2). Keep these in sync
' if the engine default ever changes.
Public Const AdverseFallbackAtrMultiplier As Double = 1.2
```

If implementer wants to read directly from `cfg.Scoring.AtrStopMultiplier` rather than hardcoding, that's preferred — pass `cfg` through to `FailureRateMatrix.Compute`. This avoids drift if the engine default is ever tuned. Either implementation is acceptable; document the choice in the change_log entry.

### 2g. Picked-cell history rotation

v1 picked-cell history captured "fixed-horizon adverse-move" outcomes. v2 captures "barrier-hit" outcomes. They are not interchangeable.

On first launch after v2 ships:

- If `analysis/picked_cell_history.csv` exists with a v1 header → rename to `analysis/picked_cell_history.v1.bak`.
- Start a fresh `analysis/picked_cell_history.csv` with a header line that includes a schema version marker. Suggested:

```
# schema=v2 (barrier-hit with adverse stop)
Timestamp,Tier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh
```

Schema version detection: if the first line of an existing file does NOT start with `# schema=v2`, rotate it. Idempotent.

### 2h. Auto-tweaker trigger threshold

v1 default `failure_rate_threshold_pct = 40` was calibrated against the lenient adverse-move semantic. v2's stricter semantic will report different (likely lower in absolute terms because intra-bar wicks add successes, but more variable across regimes) failure rates.

**Do NOT change the default in this spec.** Let the first 200–500 v2 rows surface the empirical distribution, then re-spec a calibration pass. The user can also tune via the WinForm `Tweak Settings` dialog without code change.

Document in the change_log entry: "v2 trigger threshold default unchanged from v1 (40%); recalibrate after first 200+ rows."

### 2i. Markdown report updates

`analysis/MarkdownReportWriter.vb` updates:

1. **Section 1 Summary** — add lines:
   - `Adverse barrier source: structural stop where available (N rows) / ATR fallback (M rows) / row excluded (K rows)`
   - `ATR-invalid rows excluded: K`
   - `Forward data source: Deribit OHLC bulk fetch (replaces v1 CSV-close lookup)`
2. **Section 2 Failure-Rate Matrix** — header row label changes:
   - v1: `failure rate% (n=sample) [ci_low - ci_high]`
   - v2: same format, but column header note clarifies "Failure = adverse barrier hit first OR window expired without favourable hit."
3. **Section 3 Recommended cell per tier** — same picker logic, no change.
4. **New Section 4a: Barrier-Hit Decomposition** — for each tier × window × threshold cell, additionally report:
   - Successes (favourable hit, no adverse hit first)
   - Failures by adverse hit
   - Failures by window expiry
   - Ambiguous-bar failures (favourable + adverse in same bar — counted as failure per §2a)
5. **Section 8 Hold Window Selection Stats** — unchanged structure but the answer to "STRONG_LONG verdicts most reliably hit their +0.5×ATR target within X minutes" is now literally what v2 measures. This becomes the trader's primary output.

### 2j. Trader-facing report wording

`AnalysisReportForm.vb` viewer should also surface a concise interpretation hint at the top of the report:

```
Failure model: barrier-hit with adverse stop (v2)
  - SUCCESS = price wicked through favourable barrier (entry ± multiplier × ATR)
              within the hold window, before any adverse hit.
  - FAILURE = adverse barrier (structural stop or 1.2×ATR fallback) hit first,
              OR window expired without favourable hit.
  - Same-bar and next-bar after verdict are excluded (too quick to execute).
  - Ambiguous bars (both barriers touched in same 1m candle) count as failure.
```

This is a static heading text in the report — no logic.

---

## 3. Data Model Changes

### 3a. `analysis/ForwardReturnJoiner.vb` → `analysis/ForwardWindowJoiner.vb`

Rename the file and class. The semantic shift is large enough that the v1 name is misleading.

```vb
Public Class CsvRow
    Public Property Index           As Integer
    Public Property Timestamp       As DateTime
    Public Property Price           As Double
    Public Property ATR             As Double
    Public Property Verdict         As String
    Public Property Regime          As String
    Public Property FundingBias     As String
    Public Property VerdictContext  As String
    Public Property OiCvdOutcome    As String
    Public Property OfiRatio        As Double
    Public Property OfiBidVol       As Double
    Public Property OfiAskVol       As Double
    Public Property FundingDelta    As Double
    Public Property SwingStopLong   As Double   ' NEW — CSV col 82
    Public Property SwingStopShort  As Double   ' NEW — CSV col 83

    ' v2: per-window OHLC bar list (replaces v1 ForwardPrice + WindowValid)
    Public Property ForwardBars As New Dictionary(Of Integer, List(Of OhlcBar))()
End Class

Public Class OhlcBar
    Public Property CloseTime As DateTime
    Public Property Open      As Double
    Public Property High      As Double
    Public Property Low       As Double
    Public Property Close     As Double
End Class
```

Property `WindowValid` is removed — eligibility is now implicit in `ForwardBars(W)`:
- Empty list (no bars fetched) → row excluded for that window
- Non-empty list → row eligible

### 3b. `analysis/DeribitOhlcFetcher.vb` (new)

```vb
Public Class DeribitOhlcFetcher

    ' Bulk-fetch 1m OHLC bars for the given UTC range. Returns Nothing on hard
    ' failure (Deribit maintenance, network). Caller decides how to handle Nothing.
    ' No retries here — DeribitClient.ExecuteWithRetry already handles transient
    ' failures.
    Public Shared Async Function FetchOhlcRange(
        client As DeribitClient,
        startUtc As DateTime,
        endUtc As DateTime
    ) As Task(Of Dictionary(Of DateTime, OhlcBar))

        Dim startMs As Long = New DateTimeOffset(startUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs   As Long = New DateTimeOffset(endUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim raw = Await client.GetCandlesAsync("1", startTimestampMs:=startMs, endTimestampMs:=endMs)
        If raw Is Nothing Then Return Nothing

        Dim map As New Dictionary(Of DateTime, OhlcBar)()
        For Each c In raw
            map(c.CloseTime) = New OhlcBar() With {
                .CloseTime = c.CloseTime,
                .Open      = c.Open,
                .High      = c.High,
                .Low       = c.Low,
                .Close     = c.Close
            }
        Next
        Return map
    End Function

End Class
```

Host-agnostic. No `System.Windows.Forms` references. Reusable by both `AnalysisRunner` and `AutoTweakerCore`.

### 3c. `DeribitClient.GetCandlesAsync` — extend signature if needed

If the existing signature is count-based only (e.g., `GetCandlesAsync(resolution, count)`), add an overload:

```vb
Public Async Function GetCandlesAsync(
    resolution As String,
    startTimestampMs As Long,
    endTimestampMs As Long
) As Task(Of List(Of Candle))
    ' Calls public/get_tradingview_chart_data with start_timestamp and end_timestamp.
    ' Wrapped in ExecuteWithRetry like the existing GetCandlesAsync.
End Function
```

The existing count-based overload stays — used by `RunAnalysisAsync` for live indicator computation. Don't touch that call path.

### 3d. `analysis/FailureRateMatrix.vb` rewrite

Existing `Compute(rows As List(Of CsvRow))` rewrites the inner loop to walk `ForwardBars(w)` and apply the §2a barrier-hit logic. `WindowValid` references go away. Adds per-cell decomposition counters (success / adverse-fail / window-expiry-fail / ambiguous-fail) for the new Section 4a report subsection.

### 3e. `analysis/AnalysisRunner.vb` — pipeline change

```
1. Load CSV via ForwardWindowJoiner (header parse, type conversions).
2. Compute startUtc, endUtc covering all rows + 15min.
3. DeribitOhlcFetcher.FetchOhlcRange(startUtc, endUtc) → ohlcMap
   If Nothing → render error banner, abort.
4. For each row, populate row.ForwardBars(W) by slicing ohlcMap for
   T+3 through T+W bar closes per window.
5. FailureRateMatrix.Compute(rows) → cell results
6. Pick recommended cell per tier (CI-width selector unchanged from v1).
7. Compose markdown report.
8. Write report file. Notify caller.
```

### 3f. `tools/AutoTweaker/AutoTweakerCore.vb` — pipeline change

Same as 3e but invoked from the auto-tweaker's flow. The eligibility check (cooldown, session-aligned window, tier-eligible row count) runs *before* the OHLC fetch — no point fetching if we're going to abort.

If OHLC fetch returns Nothing:
- Set `state.last_run_outcome = ERROR`
- Set `state.last_proposal_summary = "Deribit OHLC fetch failed — auto-tweaker cannot evaluate without forward price data."`
- Exit code 1
- WinForm `lblLastTweakSummary` will display this on next status refresh.

---

## 4. Settings.json

No new keys required. The `cfg.Scoring.AtrStopMultiplier` is already exposed (used by the engine's ATR Entry Levels rendering). Reuse it for the adverse barrier fallback.

`settings.json` `version` does NOT need to bump for this spec — no scoring-affecting parameter changes, no schema changes. The behaviour change lives entirely in the `analysis/` and `tools/AutoTweaker/` layers, which are not covered by the engine version invariant.

(Implementer's discretion: if you'd rather bump version 24 → 25 with a `modified_by = "failure-definition-v2"` and an explanatory `change_log` entry for traceability, that's also fine. State the choice in the commit message.)

---

## 5. Constraints

### 5a. Trader-profile compliance

- Conservative bias preserved via ambiguous-bar rule (§2a): both barriers in same bar → failure. Trader-profile §6 favours conservative classification.
- Structural stops as primary adverse barrier matches trader-profile §5: "Stop-loss: structural — below previous swing low".
- ATR-fallback only when structural unavailable, using engine's own default (`AtrStopMultiplier`) — internally consistent with the engine's display.
- Same-bar / next-bar exclusion respects realistic execution latency.

### 5b. Portability

All new code in `analysis/` and `tools/AutoTweaker/` must remain host-agnostic per `CLAUDE.md` Collaboration Rules. `DeribitOhlcFetcher.vb` calls `DeribitClient` (already host-agnostic). `AnalysisReportForm.vb` is the only WinForms-touching file in `analysis/` and remains so.

### 5c. Backward compatibility

- v1 picked-cell history rotates to `.v1.bak` automatically on first v2 run (§2g). No data loss.
- v1 markdown reports remain on disk untouched (they're per-run dated files; no overwrite).
- Auto-tweaker's existing trigger threshold default (40%) is unchanged. No surprise auto-tweaks on v2 ship.

---

## 6. Acceptance

- `dotnet build` of full solution: 0 warnings, 0 errors.
- `dotnet build tools/AutoTweaker/AutoTweaker.vbproj` independently: 0 warnings, 0 errors. Zero `System.Windows.Forms` references in dependency tree.
- `analysis/ForwardWindowJoiner.vb` exists; `analysis/ForwardReturnJoiner.vb` is removed (file rename).
- `analysis/DeribitOhlcFetcher.vb` exists; host-agnostic.
- `DeribitClient.GetCandlesAsync` time-range overload exists if it didn't before, wrapped in `ExecuteWithRetry`.
- Click `Analysis Report` from MainForm with the engine in any state:
  - Triggers a single Deribit OHLC bulk fetch.
  - Generates a report whose Section 1 Summary lists the new fields (forward data source, adverse barrier breakdown, ATR-invalid exclusions).
  - Section 2 matrix shows failure rates computed via barrier-hit logic.
  - Section 4a Barrier-Hit Decomposition renders.
- Run the AutoTweaker console app in dry-run mode against a CSV with ≥120 STRONG/MEDIUM rows:
  - Eligibility checks run first (no OHLC fetch if ineligible).
  - On eligible run, OHLC fetch happens, payload generated, dry-run file written.
  - Picked-cell history file shows v2 schema header.
- `analysis/picked_cell_history.csv.v1.bak` exists if a v1 history file was present.
- ATR-invalid rows in the test CSV are excluded — visible in the Section 1 `ATR-invalid rows excluded: K` line.
- Manual eyeball test on 5–10 rows: pick a row from the report, look up its window's bars on a Deribit chart, confirm the success/failure classification matches the visible barrier hits.

---

## 7. Out of Scope

- **Sub-minute resolution.** v2 stays at 1m OHLC. WebSocket-fed tick-level data is `post-websocket-post-calibration-backlog.md` Section A territory — out of scope until WebSocket migration ships.
- **Configurable hold windows.** The 5/10/15 minute set is hard-coded in `AnalysisConstants`. If a future calibration shows 7m or 20m would be more informative, that's a v3 change.
- **Configurable same-bar exclusion count.** The 2-bar exclusion (same + next) is hard-coded. If trading style or market microstructure changes, revisit.
- **Tier-asymmetric adverse barriers.** Currently the adverse barrier is a single value per row (structural-or-fallback), independent of the verdict tier. Could imagine STRONG getting a tighter adverse barrier than MEDIUM ("higher conviction = less tolerance for adverse"). Defer until data justifies it.
- **Per-bar tick recording for ambiguous-bar disambiguation.** When both barriers touch in same 1m candle, sub-minute order matters but is unobservable from 1m bars. Conservative-failure rule (§2a) is the v2 answer. Tick-level disambiguation is gated by WebSocket migration.

---

## 8. Open Items the Implementer Should Note

1. The `DeribitClient.GetCandlesAsync` signature in code today may already accept time-range parameters, in which case 3c becomes a no-op. Inspect first.
2. The existing `CsvRow.ForwardPrice` and `CsvRow.WindowValid` properties are removed in v2. Search for callers; the only ones should be inside `FailureRateMatrix` and the markdown writer. Update both.
3. v1 `WilsonCI` helper in `FailureRateMatrix.vb` stays unchanged — same statistical method used.
4. Consider whether the `lnkAnalysisReport` click should show a brief "Fetching Deribit OHLC…" status indicator during the bulk fetch. Single REST call usually under 2s, but visible feedback prevents the user from clicking again.
5. The change_log entry in `DeribitIndicatorProject.md` Section 15 should describe v2 in detail — barrier-hit semantic, adverse-stop modelling, OHLC fetch strategy, picked-cell rotation.

---

**End of spec.** Implementation expected to be a single Sonnet session — bounded, well-defined, no architectural ambiguity. Estimated 2–4 hours of implementation + manual verification on a sample CSV.
