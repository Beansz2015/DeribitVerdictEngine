# Spec: Live Performance Display — Per-Analysis Success-Rate Strip
**Proposed:** 2026-05-13 (P7 activation)
**Status:** PROPOSED 2026-05-13
**Target files:**
new — `LivePerformanceTracker.vb` (host-agnostic), `OhlcCache.vb` (host-agnostic);
existing — `UI/MainForm_Layout.vb` (6 new labels), `UI/MainForm_Analysis.vb` (hook), `Core/Settings/EngineSettings.vb` (new toggle), `settings.json`, `.gitignore`, `docs/TraderGuide.md`, `docs/UserManual.md`, `docs/DeribitIndicatorProject.md`;
regenerated — `docs/TraderGuide.pdf`, `docs/UserManual.pdf`

**Prerequisites:** `failure-definition-v2-proposal.md` shipped (reuses `WalkBars` and adverse-stop semantics).

**Activates:** P7 from `DeribitIndicatorProject.md §16.6`.

---

## 1. Background

The auto-tweaker computes success/fail rates over batched windows (120 verdicts every cooldown cycle, ~10–15 min apart). The Round Stats display surfaces the last 5 rounds on demand. Neither updates continuously enough for real-time trading-style adaptation.

The trader needs a live strip showing how the current settings have been performing across several time slices — week-to-date, last-3-days, today, and per-session — updated on every analysis run. This lets the trader pivot trading style mid-session (e.g., scalp tighter when the recent session's success rate is high but the streak length is short — your stated dual-mode rationale from the snapshot-history discussion).

The display is **read-only and observational**. It does not feed into any scoring or auto-tweaker decision; it just surfaces what the engine has been doing.

---

## 2. Specification

### 2a. Display location and layout

A horizontal strip of six labels, positioned **below the `lblVerdict` row** and **to the right of the auto-run `Start/Stop` button**. The strip occupies a single line of vertical space.

Shorthand format (single-line, compact):

```
Cur.Wk: 55%  |  3d: 60%  |  Cur.Day: 57%  |  Asia: 64%  |  London: 49%  |  NY: 57%
```

Each label is a `LinkLabel`-style `Label` control (non-clickable) with its own ForeColor for the rate value:

| Label | Field name (suggested) | Window |
|---|---|---|
| `Cur.Wk` | `lblPerfWeek` | Current week (Monday 00:00 UTC+8 → now) |
| `3d` | `lblPerf3d` | Last 3 chronological days (D-2 00:00, D-1 full, today 00:00 → now), UTC+8 |
| `Cur.Day` | `lblPerfDay` | Today (00:00 UTC+8 → now) |
| `Asia` | `lblPerfAsia` | Most-recent-block Asia (per §2b) |
| `London` | `lblPerfLondon` | Most-recent-block London |
| `NY` | `lblPerfNy` | Most-recent-block NY |

The rate value inside each label is coloured per §2g (green > 50%, red ≤ 50%). The label prefix and `|` separators stay neutral grey.

Per-label tooltip on hover shows: `"{Sample size n} predictions evaluated. {Range start} → {Range end} UTC+8."`

When sample size is 0 or below a render threshold, display `--%` instead of a numeric rate (uncoloured). Threshold defaults to `4` to keep noise out; configurable.

### 2b. Session boundaries — "most recent block" semantics

For each of the three session windows (Asia / London / NY), the displayed rate covers the most recently active or completed block of that session, regardless of calendar date. Three cases per session:

| State at `now` | Block window |
|---|---|
| `now` is **inside** the session's hour range | Block start = `today's session start` (or yesterday's, if the session straddles midnight and started before midnight). Block end = `now`. |
| `now` is **after** the session's hour range today | Block start = `today's session start`. Block end = `today's session end`. |
| `now` is **before** the session's hour range today | Block start = `yesterday's session start`. Block end = `yesterday's session end`. |

#### Concrete algorithm in UTC+8

For each session with `[startHour, endHour]` (both in UTC+8 24h):

```
isStraddle = endHour <= startHour      ' true only for NY (21 -> 7 next day)

If isStraddle Then
    blockEndDate = If(nowUtc8.Hour < endHour, todayDate, todayDate + 1 day)
    blockStartDate = blockEndDate - 1 day
    blockStart = blockStartDate AT startHour:00
    blockEnd   = blockEndDate AT endHour:00
Else
    If nowUtc8.Hour < startHour Then
        ' Before today's session — use yesterday's
        blockStartDate = todayDate - 1 day
    Else
        blockStartDate = todayDate
    End If
    blockStart = blockStartDate AT startHour:00
    blockEnd   = blockStartDate AT endHour:00
End If

' If we're currently INSIDE the session, the right edge is `now` not the session end.
displayEnd = Math.Min(blockEnd, nowUtc8)
```

Session hour values are read from `cfg.SessionVolume.Sessions[]`, converted from their stored UTC representation to UTC+8 at engine init:

| Session | UTC | UTC+8 |
|---|---|---|
| ASIA | 00:00–07:00 | 08:00–15:00 |
| LONDON | 08:00–12:00 | 16:00–20:00 |
| NY | 13:00–23:00 | 21:00–07:00 (next day) |

Each session is "active" if `nowUtc8.Hour` falls within `[startHour, endHour)` (for non-straddle) or within `[startHour, 24) OR [0, endHour)` (for NY).

### 2c. Success metric — v2 adverse-first barrier-hit

Reuses `FailureRateMatrix.WalkBars` semantics from `failure-definition-v2-proposal.md`. For each evaluable row:

**Verdict eligibility:** row's verdict is one of `STRONG LONG`, `LONG`, `WEAK LONG`, `STRONG SHORT`, `SHORT`, `WEAK SHORT`. `NO TRADE` and `NO TRADE [WEAK X]` are excluded (counted as not-a-prediction).

**Favourable barrier** (the "predicted price"):
- For LONG-direction verdicts: `favBar = If(v.AdjustedLongTarget > 0, v.AdjustedLongTarget, entry + 2.0 × ATR)`
- For SHORT-direction verdicts: `favBar = If(v.AdjustedShortTarget > 0, v.AdjustedShortTarget, entry - 2.0 × ATR)`

The `Adjusted*` value is what's displayed in the ATR Entry Levels block — the capped target if the 3-tier cap fired, else the raw 2×ATR target.

**Adverse barrier** (structural-first, ATR fallback — same as v2):
- For LONG: `advBar = If(SwingStopLong > 0, SwingStopLong, entry - 1.2 × ATR)`
- For SHORT: `advBar = If(SwingStopShort > 0, SwingStopShort, entry + 1.2 × ATR)`

**Bars walked:**
- Same exclusion rule as v2: skip `T+1` and `T+2` bars (too quick to execute), evaluate bars closing at `T+3` through `T+15`.
- 13 eligible bars total for a 15-min window.

**Outcome classification** (calls `FailureRateMatrix.WalkBars`):
- `SUCCESS` if favourable wick hit before any adverse hit
- `ADVERSE_HIT` if adverse hit first → counts as FAILURE
- `AMBIGUOUS` if both barriers touched in the same bar → counts as FAILURE (conservative-bias rule, same as v2)
- `WINDOW_EXPIRED` if neither hit by bar T+15 → counts as FAILURE

**Excluded rows** (don't count in numerator or denominator):
- `row.ATR <= 0` (degenerate barriers)
- `row.AdjustedLongTarget = 0` AND verdict is LONG (no displayed target — engine couldn't compute one; rare)
- Symmetric for SHORT
- Verdict in {`NO TRADE`, `NO TRADE [WEAK LONG]`, `NO TRADE [WEAK SHORT]`}

**Computed rate per window** = `SUCCESS_count / (SUCCESS_count + FAILURE_count) × 100`. Rendered as integer percent (e.g., `55%`).

### 2d. Cache architecture — two sidecar files

Two files, both gitignored, both at `bin/Debug/net8.0-windows/` (same as `analysis_log.csv`):

#### `analysis_eval_cache.csv`

One row per analysis_log.csv row that's been evaluated, plus PENDING rows awaiting forward data.

```
Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome
2026-05-13T08:32:00Z,LONG,80142.00,80298.50,80018.50,SUCCESS
2026-05-13T08:33:00Z,NO TRADE,80155.00,0,0,EXCLUDED_NO_PREDICTION
2026-05-13T08:34:00Z,WEAK SHORT,80147.50,80027.50,80307.50,PENDING
```

`EvalOutcome` values: `SUCCESS`, `ADVERSE_HIT`, `AMBIGUOUS`, `WINDOW_EXPIRED`, `EXCLUDED_NO_PREDICTION`, `EXCLUDED_ATR_INVALID`, `EXCLUDED_NO_TARGET`, `PENDING`.

Schema versioned at the top via a comment line: `# schema=v1 (live-performance-display)`. Future schema changes rotate the existing file to `.v1.bak`.

#### `ohlc_1m_cache.csv`

Rolling 7-day window of 1m OHLC bars. Used for evaluating PENDING rows as their 15-min windows complete, and reusable by future features.

```
CloseTime,Open,High,Low,Close,Volume
2026-05-13T08:31:00Z,80123.50,80145.00,80098.50,80142.00,1.2345
2026-05-13T08:32:00Z,80142.00,80165.00,80138.00,80162.50,2.1107
```

Schema versioned via header comment: `# schema=v1 (1m ohlc cache)`.

Rolling cap: keep the most recent **10,080 bars** (7 days × 24h × 60min). On each append, if count exceeds cap, drop oldest bars until count == 10,080. (Same rolling-trim pattern as `analysis_output_dump.md` from the output-dump spec.)

### 2e. Cold-start eager backfill

On engine startup, before the first analysis fires:

1. **Check `ohlc_1m_cache.csv`.**
   - If file exists and last bar's `CloseTime` ≥ `now - 7 days`: load it into the OHLC cache; fetch only the gap from `last bar + 1 min → now` via `DeribitClient.GetCandlesAsync(resolution:="1", startMs, endMs)`. Append to cache. ~1 sec.
   - If file is missing or stale: fetch the full 7-day range. ~3 sec. Write the new cache file.

2. **Check `analysis_eval_cache.csv`.**
   - Walk all rows in `analysis_log.csv`.
   - For each row not already in the eval cache:
     - Build `FavBar`, `AdvBar` from CSV row values (`AdjustedLongTarget`, `AdjustedShortTarget`, `SwingStopLong/Short`, `ATR`, `Price`)
     - If `row.Timestamp + 15 min ≤ now`: evaluate via `WalkBars` against the OHLC cache. Write SUCCESS / ADVERSE_HIT / etc.
     - Else: write PENDING.
   - Append all new rows to `analysis_eval_cache.csv`.

3. **Compute initial display state** from the eval cache for all 6 windows. Set the 6 label values.

4. **Status during eager backfill:** display `"Loading performance history..."` in `lblLogInfo` (or a similar status hook). The window controls remain responsive — backfill runs on a background task. New analyses queued during backfill wait until backfill completes (typically <5 sec).

Toggle: new setting `performance_display.eager_backfill_on_startup` defaulting to `true`. When `false`, skip steps 1–2; lazy-evaluate as analyses accumulate.

### 2f. Per-analysis update flow

Hook placement: in `MainForm_Analysis.RunAnalysisAsync`, **after** `RenderOutput(v, r)` returns and **after** the `AnalysisOutputDump.Append` call. Both should already have completed before the perf strip updates.

```vb
' New line near end of RunAnalysisAsync:
Await LivePerformanceTracker.UpdateAsync(v, r, candles1m, nowUtc)
UpdatePerformanceLabels()
```

`LivePerformanceTracker.UpdateAsync` flow:

1. **Append new analyses to OHLC cache.** Walk `candles1m` from oldest to newest. For each bar whose `CloseTime` is later than the cache's last entry, append. (Most analysis runs add 1 new bar; occasionally 0 or 2 depending on cadence drift.) Apply rolling-trim if cache exceeds 10,080 bars.

2. **Append the current row to eval cache as PENDING.** Build the cache entry from `v` and `r`:
   - `Timestamp` = `v.Timestamp` (UTC)
   - `Verdict` = `v.Verdict`
   - If excluded category → write the appropriate `EXCLUDED_*` outcome
   - Else: compute `FavBar` and `AdvBar`, write `PENDING`

3. **Resolve PENDING rows whose windows are now complete.** Find all eval-cache rows with `EvalOutcome = PENDING` AND `row.Timestamp + 15 min ≤ nowUtc`. For each, evaluate via `WalkBars` using the OHLC cache. Update the cache. Write changes back to `analysis_eval_cache.csv` (append-only — for in-place updates, use the same row-modification pattern as `SnapshotManager.Finalise`).

   In practice this resolves at most 1 row per analysis run (the row from 15 min ago). Cost per resolve: ~13 bar lookups via Dictionary.TryGetValue + 13 comparisons. <1ms.

4. **Recompute the 6 window aggregates.** For each window, walk in-memory eval cache, filter by timestamp range and outcome:
   - Numerator: count of `SUCCESS` outcomes in range
   - Denominator: count of `SUCCESS + ADVERSE_HIT + AMBIGUOUS + WINDOW_EXPIRED` in range (i.e., all FAILURE flavours + SUCCESS, excluding `EXCLUDED_*` and `PENDING`)
   - Rate = numerator / denominator × 100. If denominator < threshold → display `--%`.

5. **Return** the 6 (rate, n) tuples. Caller (`UpdatePerformanceLabels`) applies colour + writes to label text on the UI thread.

Total cost per call: appending 1 bar to OHLC cache (~10 µs), 1 row append to eval cache (~10 µs), 1 PENDING resolve (~1 ms), 6 window aggregations (~5 ms with current scale, scales linearly with cache size). Well under 10ms total — negligible against the analysis run's overall latency.

### 2g. Display rendering rules

Per label:

```
"{prefix}: {rate}%"      (when sample size >= threshold)
"{prefix}: --%"          (when sample size below threshold)
```

`prefix` is one of `Cur.Wk`, `3d`, `Cur.Day`, `Asia`, `London`, `NY`.

**Colour:**
- Rate value: `Color.LimeGreen` (or existing `C_GOOD`) when `> 50`. `Color.Crimson` (or `C_BAD`) when `<= 50`. Dim grey (`Color.DimGray`) when `--%`.
- Prefix and `:` and separators: neutral grey (`Color.LightGray` or existing `C_LABEL`).

Use `RichTextBox` with run-coloured spans if simpler than per-segment Label coloring. Alternative: each label is two children — a grey prefix and a coloured rate. Implementer's choice.

**Tooltip:** mouse hover on each label shows the underlying `{n} predictions evaluated. {Range start} → {Range end} UTC+8.` Useful for sanity-checking that the window matches expectations.

### 2h. Settings additions

New `settings.json` block:

```json
"performance_display": {
    "enabled": true,
    "min_sample_for_render": 4,
    "eager_backfill_on_startup": true,
    "session_block_semantic": "most_recent"
}
```

- `enabled` — master switch. When false, no perf strip rendered, no caches written.
- `min_sample_for_render` — below this row count, display `--%` for the window.
- `eager_backfill_on_startup` — see §2e.
- `session_block_semantic` — reserved for future. Currently always `most_recent` (per Q4 decision). If set to `calendar_day` later, switches to the rejected (a) semantic.

Settings.json `version` bump: 25 → 26 with `modified_by = "live-performance-display"`.

---

## 3. Code structure

### `LivePerformanceTracker.vb` (new, host-agnostic)

```vb
Public Class LivePerformanceTracker

    Public Class EvalCacheEntry
        Public Property Timestamp As DateTime    ' UTC
        Public Property Verdict As String
        Public Property EntryPrice As Double
        Public Property FavBar As Double          ' 0 if no valid barrier
        Public Property AdvBar As Double          ' 0 if no valid barrier
        Public Property EvalOutcome As String     ' see §2d
    End Class

    Public Class WindowAggregate
        Public Property RangeStart As DateTime
        Public Property RangeEnd As DateTime
        Public Property SuccessCount As Integer
        Public Property FailureCount As Integer
        Public Property TotalRange As Integer     ' incl. PENDING + EXCLUDED for tooltip
        Public ReadOnly Property RatePct As Double
            Get
                Dim n = SuccessCount + FailureCount
                If n = 0 Then Return -1.0
                Return CDbl(SuccessCount) / n * 100.0
            End Get
        End Property
    End Class

    ' Initialise from disk caches. Performs eager backfill if enabled. Returns
    ' summary of what happened (rows backfilled, OHLC fetched, etc.) for logging.
    Public Shared Async Function InitialiseAsync(
        evalCachePath As String,
        ohlcCachePath As String,
        analysisLogPath As String,
        cfg As EngineSettings,
        eagerBackfill As Boolean,
        ohlcFetcher As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar)))
    ) As Task(Of String)
        ...
    End Function

    ' Per-analysis update. See §2f for flow.
    Public Shared Async Function UpdateAsync(
        v As VerdictResult,
        r As IndicatorResults,
        candles1m As List(Of Candle),
        nowUtc As DateTime
    ) As Task
        ...
    End Function

    ' Compute the six window aggregates for display. Pure function over the
    ' in-memory eval cache; no I/O. Returns array of 6 WindowAggregate.
    Public Shared Function ComputeWindows(nowUtc As DateTime,
                                          cfg As EngineSettings) As List(Of WindowAggregate)
        ...
    End Function

End Class
```

### `OhlcCache.vb` (new, host-agnostic)

Manages the rolling 7-day OHLC cache file:

```vb
Public Class OhlcCache
    Public Shared Function Load(path As String) As Dictionary(Of DateTime, OhlcBar)
    Public Shared Sub Append(path As String, bars As IEnumerable(Of OhlcBar))
    Public Shared Sub RollingTrim(path As String, maxBars As Integer)
    Public Shared Function NewestBarTime(path As String) As DateTime?  ' for gap detection
End Class
```

Both new files include zero `System.Windows.Forms` references — they live at the project root alongside `AnalysisLogger.vb` and `AnalysisOutputDump.vb`, which is the established pattern for host-agnostic helpers used by the main app.

### `MainForm_Layout.vb` additions

Six new `Label` controls in the constructor. Layout positioned below `lblVerdict` and to the right of `btnStartStop`. Coordinates determined at form design time by the implementer; spec only constrains relative position (`Y` below `lblVerdict.Bottom`, `X` to right of `btnStartStop.Right`).

`UpdatePerformanceLabels()` method applies the aggregate values to the labels with colour rules.

### `MainForm_Analysis.vb` hook

Two new lines near the end of `RunAnalysisAsync`, after `RenderOutput` and `AnalysisOutputDump.Append`:

```vb
Await LivePerformanceTracker.UpdateAsync(v, r, candles1m, DateTime.UtcNow)
UpdatePerformanceLabels()
```

A startup hook in `MainForm_Layout.New()` after `SettingsLoader.Initialise(...)`:

```vb
' Async fire-and-forget: backfill performance cache before first analysis.
Task.Run(Async Function()
    Dim summary = Await LivePerformanceTracker.InitialiseAsync(...)
    Console.WriteLine("[LivePerformanceTracker] " & summary)
End Function)
```

### `.gitignore` additions

```
bin/Debug/net8.0-windows/analysis_eval_cache.csv
bin/Debug/net8.0-windows/ohlc_1m_cache.csv
bin/Release/net8.0-windows/analysis_eval_cache.csv
bin/Release/net8.0-windows/ohlc_1m_cache.csv
```

---

## 4. Documentation updates

### TraderGuide.md — Section 17 "Working with the App"

New subsection **"Live Performance Strip"** with what-to-watch-for guidance:
- What each label shows (week / 3d / day / sessions)
- Green > 50%, red ≤ 50%
- Most-recent-block semantics for sessions
- Tooltip shows sample size and exact range

### UserManual.md — new Section 21 "Live Performance Display"

Eight sub-sections:
- §21a. Concepts (window definitions, success metric, sample-size threshold)
- §21b. Most-recent-block algorithm (with worked examples for each session)
- §21c. Success metric — exact reuse of v2 barrier-hit (`WalkBars` reference)
- §21d. Cache architecture (two files, schema, rolling-trim)
- §21e. Cold-start backfill flow
- §21f. Per-analysis update flow
- §21g. Render rules and tooltips
- §21h. Settings reference

### `DeribitIndicatorProject.md` updates

- §15 version history: new entry dated 2026-05-13 for `[live-performance-display]`
- §16.6 P7: marked **RESOLVED 2026-05-13** with reference to this spec
- §16.6 new parked observation **P8** — see below

### New parked observation P8 — verdict tier filtering

Per Q3 decision, ALL directional verdicts count toward the success rate, including WEAK_*. This may dilute the rate vs. tier-filtered metric.

Add to §16.6:

```
**P8. Live performance display — WEAK tier filtering.**
*Condition:* after ~1 week of live data accumulation, if the inclusion of WEAK_*
verdicts in the success-rate calculation produces visibly different headline rates
vs. a STRONG+MEDIUM-only filter, AND if the trader observes the WEAK-included rate
is misleading (e.g., consistently lower than felt accuracy), revisit the eligibility
rule.
*Action when triggered:* small follow-up spec changing `LivePerformanceTracker`'s
eligibility filter from "all directional" to "STRONG_* + MEDIUM_*". Optionally
expose as a setting `performance_display.tier_filter` with values
`all_directional` | `actionable_only`.
```

### PDF regeneration

After all .md changes:

```bash
cd docs
pandoc TraderGuide.md -o TraderGuide.pdf --pdf-engine=xelatex --template=TraderGuide-template.tex --toc --toc-depth=2 -V geometry:a4paper
pandoc UserManual.md  -o UserManual.pdf  --pdf-engine=xelatex --template=manual-template.tex      --toc --toc-depth=2 -V geometry:a4paper
```

Both PDFs committed alongside the .md files.

---

## 5. Acceptance

- `dotnet build` clean (0/0). Main project + AutoTweaker.
- `LivePerformanceTracker` and `OhlcCache` host-agnostic — zero `System.Windows.Forms` references.
- On first engine launch:
  - `ohlc_1m_cache.csv` and `analysis_eval_cache.csv` are created
  - Eager backfill completes in <10 sec for a 7-day window (test with reset CSV — should complete in ≤3 sec)
  - Status label briefly shows "Loading performance history..."
  - Once backfill completes, all 6 labels show rates (or `--%` if sample size below threshold)
- On subsequent launches:
  - Caches load from disk (<1 sec)
  - Gap fetch covers only the time since last save
  - Display populates immediately on first analysis
- On each analysis:
  - New OHLC bar(s) appended to `ohlc_1m_cache.csv`
  - New row appended to `analysis_eval_cache.csv` (PENDING for current run)
  - The PENDING row from ~15 min ago resolves to SUCCESS / ADVERSE_HIT / AMBIGUOUS / WINDOW_EXPIRED
  - 6 labels refresh with updated rates
  - Total perf-strip overhead < 50ms per analysis (negligible against the full run)
- Visual verification:
  - Green when rate > 50, red when ≤ 50
  - Hover tooltip shows `{n} predictions evaluated. {Range start} → {Range end} UTC+8`
  - Disabling `performance_display.enabled = false` → strip not rendered, no I/O to caches
- Session block correctness:
  - Pre-21:00 UTC+8, NY label shows yesterday-21:00 → today-07:00 block (8h, fully completed)
  - Post-21:00 UTC+8, NY label shows today-21:00 → now (partial, growing)
  - 02:00 UTC+8, Asia label shows yesterday-08:00 → 15:00 block
  - 10:00 UTC+8 (mid-Asia), Asia label shows today-08:00 → now
- `.gitignore` covers both cache files (Debug + Release paths)
- No spec-rejected patterns introduced

---

## 6. Out of Scope

- **Hourly / 30-min / custom windows.** Six windows specified; no user-defined ranges.
- **Per-verdict-tier breakdown in the strip.** RoundStatsForm already provides this; the strip stays compact.
- **Multi-day session views** (e.g., "Average NY success rate over last 5 NY sessions"). The most-recent-block semantic is per the user's request; multi-block rolling-window views would be a future spec.
- **Forecast / predictive display** (e.g., projected next-hour rate). Out of scope; display is observational only.
- **Confidence intervals on the rates.** Wilson CI is already used by the failure-rate matrix for auto-tweaker; not needed in the at-a-glance strip.
- **Display in a separate window or dockable panel.** Single inline strip only.
- **Persistence of the rate values themselves.** The display is derived from the eval cache on every analysis; never stored separately.
- **Notifications / alerts when a rate crosses 50% threshold.** Out of scope; can be added later if useful.
- **Auto-tweaker integration.** The live display is standalone; it does not feed into auto-tweaker prompts or decisions. The auto-tweaker continues to use its own failure-rate matrix.

---

## 7. Implementation notes

- The eager backfill is wrapped in `Try`/`Catch`. On any failure (network, parse, file I/O), the engine continues with an empty cache and lazy-eval mode; the perf strip shows `--%` until rows accumulate. Log to console.
- The eval cache file is append-only for new rows; PENDING resolves in-place via the same pattern as `SnapshotManager.Finalise` (read all → modify target row → write back). For a typical cache of ~10K rows, this is sub-100ms.
- The OHLC cache rolling-trim runs on every append. Trim cost scales with cache size; at 10K rows it's <100ms total. Acceptable per-run overhead.
- Use `DateTime.SpecifyKind(t, DateTimeKind.Utc)` aggressively when serialising/deserialising timestamps to/from the cache CSVs. The eval cache stores UTC; the display converts to UTC+8 only at render time.
- The 3-day window is `[D-2 00:00 UTC+8, now)`. The week window is `[Monday 00:00 UTC+8, now)`. Both anchored on UTC+8 calendar dates.
- For an active session (now is inside the session range), `displayEnd = nowUtc8` — not the session-end time. The session label rate reflects partial-session data.
- Sample-size threshold `min_sample_for_render = 4` is intentionally low. The session blocks at engine start may have very few evaluable rows; `--%` is the right display until at least 4 predictions have completed their 15-min windows.
- `UpdatePerformanceLabels` runs on the UI thread (the Windows Forms label updates are not thread-safe). `LivePerformanceTracker.UpdateAsync` returns to the caller on the UI thread; the caller invokes `UpdatePerformanceLabels` directly. No `Invoke` needed.
- `OhlcCache.RollingTrim` operates on the in-memory dictionary AND the on-disk file in one shot. To avoid file-write churn, batch trims — only trim when the in-memory cache exceeds the cap by some slack (e.g., `cap × 1.05` = 10,584 bars). Reduces disk writes to every ~500 bars instead of every bar.
- Future eager backfill optimisation: instead of full 7-day OHLC fetch at startup, use the existing `picked_cell_history.csv` (or analysis log timestamps) to compute the exact range needed. Out of scope for v1.

---

**End of spec.** Implementation expected to be a single Sonnet session — moderate scope (~400 lines new code + tests + 6 labels in UI + doc updates + PDF regen). Estimated 3–5 hours. The barrier-hit logic is reused verbatim from v2; the new infrastructure is the two cache files, the 6 display labels, and the per-window aggregation. No architectural ambiguity.
