# OHLC Cache Gap Backfill — Proposal

**Status:** ✅ IMPLEMENTED — 2026-05-15
**Settings.json:** v26 → v27 (ships first; ahead of target-hit and auto-tweaker specs)
**Spec dependency:** None. Ships first because the target-hit spec's v1→v2 migration depends on complete OHLC coverage.

---

## Motivation

The OHLC cache (`ohlc_1m_cache.csv`) accumulates 1-minute bars used by:
- `LivePerformanceTracker.EvaluateEntry` — for barrier-hit / target-hit determination
- Auto-tweaker `DeribitOhlcFetcher` — historical analysis
- Future offline analysis tooling

Current population logic ([LivePerformanceTracker.vb:103-134](LivePerformanceTracker.vb:103)):

1. **Eager backfill on startup:** if `lastBar < sevenDaysAgo` (or cache empty), fetch full 7-day range in one Deribit call. Otherwise, fetch only `lastBar → nowUtc` (the trailing gap).
2. **Per-analysis append:** `UpdateAsync` adds `candles1m` bars **newer than `maxExisting`** ([LivePerformanceTracker.vb:204-216](LivePerformanceTracker.vb:204)).

**Failure mode:** bars from interior hours (mid-week, mid-day) can be missing if:
- A previous live session was interrupted mid-fetch
- The Deribit response was truncated (max 5000 bars per call)
- A brief network outage dropped some candles1m
- The user reset the cache mid-session, leaving a forward-only state

The gap-fetch path only fixes the **trailing edge** (lastBar → nowUtc). It does not detect interior gaps within the existing 7-day window.

**Real example (current state):**

```
2026-05-10: 1285 bars (almost full)
2026-05-11: 1440 (full)
2026-05-12: 1440 (full)
2026-05-13:  960 (16h)  ← gap: 14-21 UTC missing (8 hours)
2026-05-14:  978 (16h)  ← gap: 16-23 UTC missing (8 hours)
2026-05-15:  512 (8.5h, in progress)
```

The gaps line up exactly with hours when the engine was running auto-analysis. Result: ~950 minutes of OHLC permanently missing, which means ~950 historical eval cache rows can't be re-walked for the target-hit metric (Spec 2), and the auto-tweaker has incomplete data for those windows.

---

## Design changes

### 1. Gap detection on InitialiseAsync

Add a new **Step 1.5** between the existing Step 1 (load/trailing-fetch) and Step 2 (load eval cache):

```
Step 1.5: Detect and fill interior OHLC gaps within the 7-day window.
  - Enumerate expected minute slots from nowUtc.AddDays(-7) to nowUtc
  - Find contiguous runs of minutes NOT present in _ohlcLookup
  - For each gap run, fetch from Deribit (chunked if > 5000 minutes)
  - Append to _ohlcLookup AND OhlcCache.Append (persist)
  - Throttle: stop after max_gap_fill_calls (default 10)
```

### 2. Gap detection algorithm

```vb
''' <summary>
''' Find contiguous runs of minute timestamps within [rangeStart, rangeEnd]
''' that are NOT present in the OHLC lookup. Returns list of (start, end) tuples
''' inclusive on both ends.
''' </summary>
Private Shared Function FindGaps(
        lookup     As Dictionary(Of DateTime, OhlcBar),
        rangeStart As DateTime,
        rangeEnd   As DateTime
    ) As List(Of (StartUtc As DateTime, EndUtc As DateTime))

    Dim gaps As New List(Of (DateTime, DateTime))()
    Dim cursor As DateTime = TruncateToMinute(rangeStart)
    Dim endUtc As DateTime = TruncateToMinute(rangeEnd)
    Dim gapStart As DateTime? = Nothing

    While cursor <= endUtc
        If Not lookup.ContainsKey(cursor) Then
            If gapStart Is Nothing Then gapStart = cursor
        Else
            If gapStart IsNot Nothing Then
                gaps.Add((gapStart.Value, cursor.AddMinutes(-1)))
                gapStart = Nothing
            End If
        End If
        cursor = cursor.AddMinutes(1)
    End While

    If gapStart IsNot Nothing Then
        gaps.Add((gapStart.Value, endUtc))
    End If

    Return gaps
End Function

Private Shared Function TruncateToMinute(t As DateTime) As DateTime
    Return New DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0,
                        DateTimeKind.Utc)
End Function
```

Note on bar-time semantics: `OhlcBar.CloseTime` is `open_time + 1 minute`. So a bar for minute 14:00-14:01 has CloseTime 14:01:00. The lookup key matching uses this convention consistently — no off-by-one needed.

### 3. Chunking for long gaps

Deribit's `public/get_tradingview_chart_data` returns at most 5000 bars per call. If `(EndUtc - StartUtc).TotalMinutes > MAX_GAP_FILL_MINUTES` (default 5000), split:

```vb
Private Shared Async Function FetchGapChunked(
        fetcher  As Func(Of DateTime, DateTime, Task(Of List(Of OhlcBar))),
        gapStart As DateTime,
        gapEnd   As DateTime,
        chunkMinutes As Integer
    ) As Task(Of List(Of OhlcBar))

    Dim result As New List(Of OhlcBar)()
    Dim cursor As DateTime = gapStart
    While cursor <= gapEnd
        Dim chunkEnd As DateTime = cursor.AddMinutes(chunkMinutes - 1)
        If chunkEnd > gapEnd Then chunkEnd = gapEnd
        Dim bars = Await fetcher(cursor, chunkEnd)
        If bars IsNot Nothing Then result.AddRange(bars)
        cursor = chunkEnd.AddMinutes(1)
    End While
    Return result
End Function
```

### 4. Throttle / safety

New settings.json keys under `performance_display`:

```jsonc
"performance_display": {
  ...existing keys...,
  "gap_backfill_enabled":      true,    // master switch
  "max_gap_fill_calls":        10,      // safety cap on Deribit calls per startup
  "max_gap_fill_minutes":      5000     // chunk size per call
}
```

**Throttle rationale:** typical fragmentation needs 2–5 calls. 10 is generous headroom. Each call takes ~200-400ms; total backfill ≤4 seconds at worst — invisible to the user during the existing `"Loading performance history..."` status.

### 5. Idempotency

`OhlcCache.Append` writes bars sequentially. Already-present bars must be skipped. Confirm current `Append` either (a) skips bars whose CloseTime exists in the file, or (b) the new gap-fill path filters before calling. Recommended: filter at the call site since OhlcCache.Append is shared with the per-analysis path which assumes new bars only.

```vb
Dim freshBars = gapBars.Where(Function(b) Not _ohlcLookup.ContainsKey(b.CloseTime)).ToList()
If freshBars.Count > 0 Then
    For Each b In freshBars : _ohlcLookup(b.CloseTime) = b : Next
    OhlcCache.Append(ohlcCachePath, freshBars)
End If
```

### 6. UI feedback

Status bar text during backfill:
```
"Loading performance history... (OHLC gap fill: 3 of 5)"
```

Console log per call:
```
[LivePerformanceTracker] Gap-fill call 3 of 5: 2026-05-13T14:00Z → 2026-05-13T21:59Z (480 bars expected)
[LivePerformanceTracker] Gap-fill call 3 received: 478 bars (2 missing, likely Deribit-side)
```

### 7. Non-goals

- **Real-time gap detection** in `UpdateAsync`. Out of scope. Live-running gaps should be rare and self-heal on next restart.
- **Filling beyond the 7-day window.** Bars older than 7 days are trimmed; backfilling them would require coordination with `OhlcCache.MAX_BARS`. Stick to `[nowUtc - 7 days, nowUtc]`.
- **Cross-restart deduplication.** Once a bar has been fetched and persisted, it stays.

---

## Settings.json schema (v29)

Change to `performance_display` block (already exists, extending):

```jsonc
"performance_display": {
  "enabled":                    true,
  "min_sample_for_render":      4,
  "eager_backfill_on_startup":  true,
  "session_block_semantic":     "most_recent",
  "metric_mode":                "barrier",
  "gap_backfill_enabled":       true,    // NEW
  "max_gap_fill_calls":         10,      // NEW
  "max_gap_fill_minutes":       5000     // NEW
}
```

`change_log` entry:
> v27 (2026-05-15): OHLC cache gap-fill on startup. `InitialiseAsync` now detects interior gaps within the 7-day window and back-fills from Deribit. New keys `gap_backfill_enabled`, `max_gap_fill_calls`, `max_gap_fill_minutes`. Addresses the "engine running but bars missing" failure mode.

---

## Implementation steps

1. **PerformanceDisplaySettings POCO:** add `GapBackfillEnabled`, `MaxGapFillCalls`, `MaxGapFillMinutes` with defaults.
2. **LivePerformanceTracker.vb:**
   - Add `FindGaps` private shared function
   - Add `TruncateToMinute` helper
   - Add `FetchGapChunked` private async helper
   - Add Step 1.5 block in `InitialiseAsync` between existing Step 1 (lines 134-end) and Step 2 (line 137)
   - Add status callback hook for "OHLC gap fill: K of N" text
3. **MainForm_Layout.vb:** pass a status-update lambda to `InitialiseAsync` (already passes via `lblLogInfo.Text` update; just need the in-progress messages).
4. **Build:** clean.
5. **Docs:** update `architecture.md` data flow with Step 1.5. Add §15 entry.

---

## Test plan

| Test | Expected |
|---|---|
| Start app with current cache (known May 13/14 gaps) | Gap-fill runs, console logs ~2 calls, OHLC cache size grows by ~960 bars |
| Restart app (no gaps now) | `FindGaps` returns empty, no fetches, no UI feedback noise |
| Start app with empty cache | Existing full 7-day fetch runs first, then gap-fill detects 0 gaps |
| Force Deribit failure mid-backfill | Safety cap stops at `max_gap_fill_calls`, next restart picks up |
| `gap_backfill_enabled=false` | Step 1.5 skipped silently |
| Build | Clean |
| Verify post-fix: re-run target-hit probe (Spec 2) on full eval cache | Should see ~2013 rows processed instead of 67 |

---

## Risks

| Risk | Mitigation |
|---|---|
| Deribit rate limit on many gap calls | `max_gap_fill_calls=10` cap. Typical < 5 calls. Rate limit is 100 req/sec public — well within bounds |
| `FindGaps` performance on 10080 slots | Dictionary lookup is O(1) per slot. Total < 100ms even on slow hardware. Measured negligible |
| Bar timestamps not aligned to whole minutes | Deribit returns aligned bars. `TruncateToMinute` defensive in case of millisecond drift |
| Gap-fill races with per-analysis append | Step 1.5 runs inside `InitialiseAsync` before `_initTcs.SetResult`. `UpdateAsync` awaits `_initTcs.Task`, so no race |
| File-size growth | Bounded by `OhlcCache.RollingTrim` to MAX_BARS (10080). Same as today |

---

## Future considerations

**Optional Step 1.6 — eval cache PENDING re-resolve:**
After Step 1.5 fills gaps, walk `_evalCache` once for any `PENDING` entries whose 15-min windows are now complete AND have OHLC coverage. Resolve them. This isn't strictly required (the existing `ResolvePendingRows` will catch them on the next `UpdateAsync`), but it gives the user a "fully resolved" cache on startup display rather than after first analysis. Add only if requested.
