# Target-Hit Metric Toggle — Proposal

**Status:** ✅ IMPLEMENTED — 2026-05-17 (proposed 2026-05-15)
**Settings.json:** v27 → v28 (ships after OHLC gap-backfill, before auto-tweaker)
**Eval cache schema:** v1 → v2
**Spec dependency:** Ships AFTER `ohlc-gap-backfill-proposal.md` so the v1→v2 backfill has complete OHLC to walk against. Functional without it, just leaves more cells blank.

---

## Motivation

The live performance display measures **barrier-hit rate**: did the favourable barrier hit BEFORE the adverse barrier within T+3..T+15. This conflates two distinct quality signals:

1. **Direction prediction quality** — did the engine call the right side?
2. **Stop placement quality** — was the stop wide enough to survive normal noise?

Decoupling them lets the trader see which is the bottleneck.

**Empirical evidence (2026-05-15 probe, n=67, OHLC-limited):**

| Tier | Barrier-hit | Target-hit | Gap |
|---|---|---|---|
| LONG | 0.0% | 30.8% | +30.8pp |
| WEAK LONG | 0.0% | 44.9% | +44.9pp |
| WEAK SHORT | 0.0% | 25.0% | +25.0pp |

A 30–45pp gap means: "direction was right enough for the target to get touched, but the stop bit first". The metrics measure different things; both are worth seeing.

---

## Design changes

### 1. Eval cache schema v1 → v2

New column appended at the end: `TargetEverHit`.

- `1` = favourable barrier was touched within T+3..T+15 (regardless of adverse outcome)
- `0` = not touched
- (empty) = not yet evaluated (e.g. insufficient OHLC coverage during backfill)

Schema comment: `# schema=v2 (target-hit-toggle)`.

Updated column header:
```
Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome,TargetEverHit
```

**Migration from v1:** detect by header line not containing `TargetEverHit`. On first load:
1. Parse v1 rows into memory with `TargetEverHit = Nothing`
2. Walk every non-EXCLUDED, non-PENDING row against `_ohlcLookup`
3. Rewrite the file with v2 header + populated column where bars were available
4. Rows with no OHLC coverage stay `Nothing` (written as empty string)

Backfill runs once per v1→v2 migration. Subsequent restarts skip it.

### 2. New walk helper

```vb
Public Shared Function TargetHitWalk(bars   As List(Of OhlcBar),
                                     favBar As Double,
                                     isLong As Boolean) As Boolean
    For Each b In bars
        If isLong Then
            If b.High >= favBar Then Return True
        Else
            If b.Low <= favBar Then Return True
        End If
    Next
    Return False
End Function
```

Lives in `analysis/FailureRateMatrix.vb` next to `WalkBars` to keep walk semantics co-located. Used by:
- `LivePerformanceTracker.EvaluateEntry` (live evaluation, alongside `WalkBars`)
- `LivePerformanceTracker.MigrateV1ToV2` (one-time backfill)
- Future offline analysis code (auto-tweaker target-hit-aware metrics, etc.)

### 3. Live evaluation path

`EvaluateEntry` now computes both metrics in one pass:

```vb
Private Shared Function EvaluateEntry(e         As EvalCacheEntry,
                                       ts        As DateTime,
                                       nowUtc    As DateTime) As (outcome As String, targetHit As Boolean?)
    If e.FavBar = 0 OrElse e.AdvBar = 0 Then Return ("WINDOW_EXPIRED", Nothing)
    Dim bars = GetEligibleBars(ts, nowUtc)
    If bars.Count = 0 Then Return ("WINDOW_EXPIRED", Nothing)
    Dim isLong As Boolean = IsLongVerdict(e.Verdict)
    Dim barrierOutcome = FailureRateMatrix.WalkBars(bars, e.FavBar, e.AdvBar, isLong)
    Dim targetHit      = FailureRateMatrix.TargetHitWalk(bars, e.FavBar, isLong)
    Return (barrierOutcome, targetHit)
End Function
```

Callers `ResolvePendingRows` and `UpdateAsync`'s eval path unpack the tuple and set both fields.

### 4. WindowAggregate dual-metric

```vb
Public Class WindowAggregate
    Public Property RangeStart       As DateTime
    Public Property RangeEnd         As DateTime
    Public Property SuccessCount     As Integer  ' barrier metric numerator
    Public Property FailureCount     As Integer
    Public Property TargetHitCount   As Integer  ' NEW: target metric numerator
    Public Property TotalRange       As Integer

    Public ReadOnly Property BarrierRatePct As Double
        Get
            Dim n = SuccessCount + FailureCount
            If n = 0 Then Return -1.0
            Return CDbl(SuccessCount) / n * 100.0
        End Get
    End Property

    Public ReadOnly Property TargetRatePct As Double
        Get
            Dim n = SuccessCount + FailureCount
            If n = 0 Then Return -1.0
            Return CDbl(TargetHitCount) / n * 100.0
        End Get
    End Property
End Class
```

Same denominator `(SuccessCount + FailureCount)` — rows with blank `TargetEverHit` are excluded from BOTH numerator and denominator (consistent treatment). Rows with `TargetEverHit ∈ {0, 1}` contribute to the denominator regardless of which mode is active.

### 5. BuildAggregate updates

```vb
For Each e In _evalCache
    If e.Timestamp < rangeStartUtc OrElse e.Timestamp > rangeEndUtc Then Continue For
    agg.TotalRange += 1
    Select Case e.EvalOutcome
        Case "SUCCESS"
            agg.SuccessCount += 1
            If e.TargetEverHit.HasValue AndAlso e.TargetEverHit.Value Then agg.TargetHitCount += 1
        Case "ADVERSE_HIT", "AMBIGUOUS", "WINDOW_EXPIRED"
            agg.FailureCount += 1
            If e.TargetEverHit.HasValue AndAlso e.TargetEverHit.Value Then agg.TargetHitCount += 1
    End Select
Next
```

Note: SUCCESS always implies TargetEverHit=1 (favourable hit, by definition), so the increments are consistent.

### 6. Settings.json

```jsonc
"performance_display": {
  "enabled": true,
  "min_sample_for_render": 4,
  "eager_backfill_on_startup": true,
  "session_block_semantic": "most_recent",
  "metric_mode": "barrier"              // NEW: "barrier" | "target", default "barrier"
}
```

### 7. UI: live mode toggle

**Render path:** `UpdatePerformanceLabels` reads `cfg.PerformanceDisplay.MetricMode` and renders the appropriate rate per label.

**Mode indicator:** prefix the strip with a small label `[B]` (barrier) or `[T]` (target), positioned just before the first perf label. Single character, dim grey when matches default, amber when not.

**Click toggle (ephemeral):** left-click on any perf label flips `MetricMode` in-memory only, refreshes labels. Status bar message: `"Metric mode → target (right-click any label to persist)"`. Resets to settings.json value on next app restart.

**Right-click persist:** right-click on any perf label opens a 2-item context menu: `"Use barrier metric"` / `"Use target metric"`. Selecting writes the value to settings.json via `SettingsLoader.Save`.

**Tooltip enhancement:** existing tooltip shows `"{N} predictions evaluated. {start} → {end} UTC+8."`. Add a second line: `"Other metric: {rate}% ({hit}/{n})"`. So both metrics are visible without toggling.

Example tooltip in barrier mode:
```
55 predictions evaluated. 2026-05-15 00:00 → 2026-05-15 14:30 UTC+8.
Target-hit: 42% (23/55)
```

### 8. Color logic preserved

Same thresholds:
- `> 50%` → C_GOOD (green)
- `≤ 50%` → C_BAD (red)
- below `min_sample_for_render` → `--%` C_DIM (dim grey)

Applied to whichever metric is active.

---

## Implementation steps

1. **EvalCacheEntry:** add `TargetEverHit As Boolean?` (Nullable).
2. **FailureRateMatrix:** add `TargetHitWalk` static method.
3. **LivePerformanceTracker:**
   - Update `ParseEvalLine` to handle 6-or-7-column rows (v1 vs v2)
   - Update `FormatEvalEntry` to write the 7th column (empty string for `Nothing`)
   - Update `EVAL_SCHEMA_COMMENT` to `# schema=v2 (target-hit-toggle)`
   - Update `EVAL_COL_HEADER` with the new column
   - Update `EvaluateEntry` to return the tuple and populate both fields
   - Update `ResolvePendingRows` and `UpdateAsync` call sites
   - Add `MigrateV1ToV2` private method, called from `InitialiseAsync` Step 2.5 (after LoadEvalCache, before backfill)
4. **WindowAggregate:** add `TargetHitCount`, dual `*RatePct` properties.
5. **BuildAggregate:** populate both numerators.
6. **EngineSettings POCO:** add `MetricMode` field to `PerformanceDisplaySettings`. Default `"barrier"`.
7. **UpdatePerformanceLabels (MainForm_Layout.vb):**
   - Read `MetricMode`, switch on it for rate / colour
   - Add mode indicator label `lblPerfMode` (1-char prefix)
   - Update tooltip with second-line dual-metric info
8. **Click handlers:**
   - Left-click handler on each perf label → toggle ephemeral mode + refresh
   - Right-click handler → context menu with persist options
9. **Build clean, settings.json v27 → v28.**
10. **Docs:** add Section 16.X to DeribitIndicatorProject.md. Update architecture.md data flow.

---

## Test plan

| Test | Expected |
|---|---|
| Start app with v1 cache | Migration runs once, file rewritten as v2, console logs `"v1→v2 migration: X rows backfilled, Y blank (no OHLC)"` |
| Start app with v2 cache | No migration, normal load |
| Live analysis, barrier mode | Strip shows barrier rates (same as v25 behaviour) |
| Live analysis, target mode | Strip shows target-hit rates; visually different where gap exists |
| Left-click a label, mode flips | Ephemeral toggle, no settings.json write |
| Right-click → persist | Settings.json updated, persists across restart |
| Tooltip shows both metrics | Both rates visible without toggling |
| Build | Clean |

---

## Risks / open questions

1. **Migration cost:** ~4000 rows × ~13 bars each = ~52k array lookups. Sub-second even on slow hardware. Acceptable.
2. **Blank cells in mixed cache:** if OHLC has gaps, some rows will have `TargetEverHit = Nothing`. These rows are excluded from BOTH numerators (target AND barrier) for consistency. Documented in tooltip.
3. **Future SUCCESS != TargetEverHit consistency:** if a future schema change alters barrier-hit semantics, we should add a self-check in `EvaluateEntry`: when outcome=SUCCESS, `targetHit` MUST be true. Log a warning otherwise.
4. **Click on AutoSize label hit-testing:** WinForms label click events work fine on AutoSize labels. Verified pattern used by existing status-bar links.
