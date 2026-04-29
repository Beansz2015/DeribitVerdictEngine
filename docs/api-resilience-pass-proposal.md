# Spec: API Resilience Pass — Retry + Skip-on-Failure
**Proposed:** 2026-04-29
**Status:** PROPOSED — pending user approval
**Target files:** `DeribitClient.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Layout.vb`, `UI/MainForm_Render_Header.vb`, `settings.json`

This is a **resilience-only pass**. No scoring change. No new indicators. No CSV schema change. Hardens the REST fetch layer so transient Deribit/Cloudflare failures (HTTP 5xx, timeouts, network blips) don't abort the analysis with a stack trace and don't waste calibration data.

Designed with future WebSocket migration in mind: the resilience layer is structured so that the WebSocket equivalent can replace it without changing the call-site contract.

---

## 1. Problem Statement

Two failure modes observed during AFK auto-run at 10s intervals:

- **HTTP 525** — Cloudflare-side SSL handshake failure between Cloudflare and Deribit's origin. Transient, server-side. Currently throws an `HttpRequestException` from `EnsureSuccessStatusCode()` that propagates up `Task.WhenAll`, kills the analysis, and renders a stack trace in the UI.
- **HttpClient timeout** — `HttpClient.Timeout = 10s` fires when Deribit takes longer than 10s on any single call. Throws `TaskCanceledException`. Same propagation path, same outcome.

Current behaviour:

1. Any single fetch failure throws → `Task.WhenAll` propagates the first exception → `RunAnalysisAsync` aborts → `btnAnalyze_Click` Catch displays stack trace in `txtOutput` → analysis is lost (no CSV row, no UI summary).
2. The auto-run timer keeps firing on schedule, so subsequent runs may succeed. But every errored run loses calibration data and produces a confusing stack trace if the user is AFK and returns to the screen.
3. With WebSocket migration eventually planned, the constant-connection model has more failure surface area (disconnects, partial frames, reconnect lag). The current "throw and propagate" model becomes increasingly unsuitable.

This pass:

- Adds per-call retry-with-backoff for transient failures (5xx, timeout, network errors).
- Returns null/Nothing from `DeribitClient` methods on hard failure (after retry exhausted) instead of throwing.
- `RunAnalysisAsync` detects missing data and renders a clean **skip warning** with the failed-fetch reason. No CSV row written. No stack trace.
- Maintains a session-wide skip counter visible in the status bar so the user can see how often skips happened during AFK runs.
- Status bar already exists; just adds one display field.

Zero new API calls. No new fetches. No scoring change. No schema change.

---

## 2. Resilience Strategy

### 2a. Two-layer model

**Layer 1 — `DeribitClient` (retry):** each public `GetXxxAsync` method wraps its HTTP call in a small retry helper. On transient failure (5xx HTTP, `TaskCanceledException`, `HttpRequestException` without a 4xx status), retry once after a configurable backoff (default 1s). On hard failure (4xx, JSON parse error, retry exhausted), return null/Nothing.

**Layer 2 — `RunAnalysisAsync` (skip):** after `Task.WhenAll` completes, check each result. If any required result is missing, render a skip warning (reason + which fetch failed) instead of running the full analysis. No CSV row written. Skip counter incremented.

### 2b. Why "skip everything" not "degraded mode"?

Considered: continue analysis with default values for missing fetches (e.g., funding=0 if `GetFundingRateAsync` returns Nothing).

**Rejected for v0.4:**

- Adds branching complexity throughout `RunAnalysisAsync` ("if X is missing, do Y instead").
- Calibration data quality matters — degraded rows are not equivalent to clean rows. Better to log fewer, cleaner rows than more rows of mixed quality.
- The trader profile favours conservatism: missing data → no opinion → no row.
- WebSocket migration will need a different paradigm anyway (last-known-good cache + reconnection); building degraded-mode logic for REST that gets thrown away later is wasted work.

**Re-evaluate post-launch.** If skip rate is observed >10% in practice over 50+ runs, consider degraded-mode for the non-critical fetches (funding, book summary) in a v0.5 spec.

### 2c. Why retry once, not exponential backoff?

- Auto-run interval is 60s (post-spec recommendation). One retry with 1s backoff fits well under the cycle.
- Exponential backoff (1s → 2s → 4s → 8s) on 6 parallel fetches could push total fetch time past the next cycle, creating overlap risk.
- Single retry catches genuinely transient flakes (the most common Deribit/Cloudflare 525 pattern resolves on first retry).
- Retry count is configurable — user can raise to 2 if observed skip rate is high.

---

## 3. DeribitClient Changes

### 3a. New private helper — `ExecuteWithRetry`

```vb
''' <summary>
''' Executes an async HTTP fetch with bounded retry on transient failures.
''' Returns the parsed result or Nothing if all retries exhausted.
''' Transient failures: HTTP 5xx, TaskCanceledException (timeout),
'''   HttpRequestException without a status code (network drop).
''' Hard failures (no retry): HTTP 4xx, JSON parse errors, anything else.
''' </summary>
Private Shared Async Function ExecuteWithRetry(Of T)(
        fetcher As Func(Of Task(Of T)),
        callerName As String) As Task(Of T)

    Dim cfg = SettingsLoader.Current.Network
    Dim attempts As Integer = 1 + Math.Max(0, cfg.RetryCount)

    For i As Integer = 1 To attempts
        Try
            Return Await fetcher()
        Catch ex As HttpRequestException
            ' 4xx are hard failures -- don't retry. Caller likely has a bug.
            If ex.StatusCode.HasValue AndAlso
               CInt(ex.StatusCode.Value) >= 400 AndAlso
               CInt(ex.StatusCode.Value) < 500 Then
                Console.WriteLine(String.Format("[{0}] Hard HTTP failure: {1}", callerName, ex.Message))
                Return Nothing
            End If
            ' 5xx and network errors -- transient, retry if we have one left
            If i < attempts Then
                Console.WriteLine(String.Format("[{0}] Transient failure (attempt {1}/{2}): {3}",
                                                callerName, i, attempts, ex.Message))
                Await Task.Delay(cfg.RetryBackoffMs)
                Continue For
            End If
            Console.WriteLine(String.Format("[{0}] Retry exhausted: {1}", callerName, ex.Message))
            Return Nothing
        Catch ex As TaskCanceledException
            ' Treat timeout same as 5xx -- retry once
            If i < attempts Then
                Console.WriteLine(String.Format("[{0}] Timeout (attempt {1}/{2})",
                                                callerName, i, attempts))
                Await Task.Delay(cfg.RetryBackoffMs)
                Continue For
            End If
            Console.WriteLine(String.Format("[{0}] Timeout retry exhausted", callerName))
            Return Nothing
        Catch ex As Exception
            ' JSON parse, etc -- hard failure, no retry
            Console.WriteLine(String.Format("[{0}] Hard failure: {1}", callerName, ex.Message))
            Return Nothing
        End Try
    Next

    Return Nothing
End Function
```

Console writes are diagnostic only. Production console output goes to the debugger Output window when running from Visual Studio, or stderr otherwise. Cheap insurance during the AFK observation period.

### 3b. Method signature changes — return nullable

Current return types throw on failure. After this pass, they return Nothing/null on hard failure.

| Method | Current return | New return | Notes |
|---|---|---|---|
| `GetCandlesAsync` | `Task(Of List(Of Candle))` | `Task(Of List(Of Candle))` | Already reference type; null on failure |
| `GetFundingRateAsync` | `Task(Of Double)` | `Task(Of Double?)` | Nullable value type for failure signalling |
| `GetBookSummaryAsync` | `Task(Of (OI As Double, MarkPrice As Double))` | `Task(Of (OI As Double, MarkPrice As Double)?)` | Nullable value tuple |
| `GetOrderBookAsync` | `Task(Of OrderBookSnapshot)` | `Task(Of OrderBookSnapshot)` | Already reference type; null on failure |
| `GetRecentTradesAsync` | `Task(Of List(Of TradeRecord))` | `Task(Of List(Of TradeRecord))` | Already reference type; null on failure |

VB.NET 17 / .NET 8 supports nullable value types and nullable value tuples natively. No package additions needed.

### 3c. Updated method bodies

Each method becomes a thin wrapper around `ExecuteWithRetry`. Example for `GetFundingRateAsync`:

**Before:**
```vb
Public Shared Async Function GetFundingRateAsync() As Task(Of Double)
    Dim tickerUrl As String = BaseUrl & "/public/ticker?instrument_name=BTC-PERPETUAL"
    Dim json As String = Await _http.GetStringAsync(tickerUrl)
    Dim doc As JsonDocument = JsonDocument.Parse(json)
    Dim result As JsonElement = doc.RootElement.GetProperty("result")
    Dim fundingEl As JsonElement = Nothing
    If result.TryGetProperty("funding_8h", fundingEl) Then
        Return fundingEl.GetDouble()
    End If
    Return 0.0
End Function
```

**After:**
```vb
Public Shared Async Function GetFundingRateAsync() As Task(Of Double?)
    Return Await ExecuteWithRetry(Of Double?)(
        Async Function() As Task(Of Double?)
            Dim tickerUrl As String = BaseUrl & "/public/ticker?instrument_name=BTC-PERPETUAL"
            Dim json As String = Await _http.GetStringAsync(tickerUrl)
            Dim doc As JsonDocument = JsonDocument.Parse(json)
            Dim result As JsonElement = doc.RootElement.GetProperty("result")
            Dim fundingEl As JsonElement = Nothing
            If result.TryGetProperty("funding_8h", fundingEl) Then
                Return CType(fundingEl.GetDouble(), Double?)
            End If
            Return CType(0.0, Double?)
        End Function,
        "GetFundingRateAsync")
End Function
```

Same pattern for the other five methods. The inner lambda body is the existing fetch logic verbatim; only the wrapper changes.

### 3d. HttpClient timeout

Update the static constructor:

```vb
Shared Sub New()
    _http.DefaultRequestHeaders.Add("User-Agent", "DeribitScalpVerdictApp/1.0")
    _http.Timeout = TimeSpan.FromSeconds(SettingsLoader.Current.Network.RequestTimeoutSeconds)
End Sub
```

Note: `HttpClient.Timeout` is set once at construction. To make it hot-reload from settings.json, the user has to restart the app. Acceptable — the network block is unlikely to be tuned mid-session.

---

## 4. RunAnalysisAsync Changes

After `Task.WhenAll` completes, validate each result. If any required result is missing, render a skip warning and return cleanly.

### 4a. After awaiting fetches

**Insert immediately after `Await Task.WhenAll(...)`** (and before any indicator computation):

```vb
Dim candles1m    = Await t_1m
Dim candles5m    = Await t_5m
Dim candles15m   = _mtfCandles15m
Dim fundingRate  = Await t_funding
Dim bookSummary  = Await t_book
Dim orderBook    = Await t_ob
Dim recentTrades = Await t_trades

' Resilience check: if any required fetch failed, skip cleanly.
Dim skipReason As String = Nothing
If candles1m Is Nothing OrElse candles1m.Count < 50 Then
    skipReason = "1m candles unavailable"
ElseIf candles5m Is Nothing OrElse candles5m.Count < 30 Then
    skipReason = "5m candles unavailable"
ElseIf Not fundingRate.HasValue Then
    skipReason = "funding rate unavailable"
ElseIf Not bookSummary.HasValue Then
    skipReason = "book summary unavailable"
ElseIf orderBook Is Nothing Then
    skipReason = "order book unavailable"
ElseIf recentTrades Is Nothing OrElse recentTrades.Count = 0 Then
    skipReason = "recent trades unavailable"
End If

If skipReason IsNot Nothing Then
    _skipCount += 1
    txtOutput.Clear()
    AppendRtf(txtOutput, String.Format("ANALYSIS SKIPPED: {0}" & Environment.NewLine, skipReason), C_WARN, bold:=True)
    AppendRtf(txtOutput, String.Format("Skip count this session: {0}" & Environment.NewLine, _skipCount), C_DIM)
    AppendRtf(txtOutput, "Engine continues — next auto-run cycle will retry.", C_DIM)
    lblVerdict.Text      = "SKIPPED"
    lblVerdict.BackColor = Color.FromArgb(120, 100, 60)
    UpdateLogInfo()
    Return
End If

' Existing code continues -- candles1m.Last(), etc. all safe now.
Dim lastTradePrice As Double = ...
```

15m candles are special — they're cached with TTL. If the 15m fetch fails, the cache might still be valid (within 60s). The existing logic already handles this:

```vb
If mtfStale Then
    t_15m = DeribitClient.GetCandlesAsync("15", 70)
    Await Task.WhenAll(t_1m, t_5m, t_15m, t_funding, t_book, t_ob, t_trades)
    _mtfCandles15m    = Await t_15m
    _mtfLastFetchTime = DateTime.UtcNow
Else
    Await Task.WhenAll(t_1m, t_5m, t_funding, t_book, t_ob, t_trades)
End If
```

**Modify** the stale-fetch path so a failed 15m fetch doesn't overwrite the cache:

```vb
If mtfStale Then
    t_15m = DeribitClient.GetCandlesAsync("15", 70)
    Await Task.WhenAll(t_1m, t_5m, t_15m, t_funding, t_book, t_ob, t_trades)
    Dim freshM15 = Await t_15m
    If freshM15 IsNot Nothing AndAlso freshM15.Count > 0 Then
        _mtfCandles15m    = freshM15
        _mtfLastFetchTime = DateTime.UtcNow
    End If
    ' If freshM15 is null, leave the cache as-is. Stale data is better than no data
    ' for the MTF gate, since 15m candles change slowly. Cache retry happens next cycle.
Else
    Await Task.WhenAll(t_1m, t_5m, t_funding, t_book, t_ob, t_trades)
End If
```

This means: if 15m fetch fails but the cache is non-empty, MTF gate still has data. If the cache is empty (cold start) AND fetch fails, the engine continues — `CalcMTFGate` already handles `Nothing` / empty input gracefully (`gateReason = "MTF: insufficient 15m candles"`).

Net: **15m fetch failure alone does not skip the analysis.** Other six fetches do.

### 4b. Unwrap nullable returns

Where the existing code reads `r.FundingRate = fundingRate` (now `Double?`), unwrap explicitly:

```vb
r.FundingRate = fundingRate.Value  ' safe -- skip-check above guarantees HasValue
```

Same for bookSummary tuple unwrap.

---

## 5. UI Changes

### 5a. Skip counter field — `MainForm_Layout.vb`

Add alongside the existing field block:

```vb
' Resilience: count of skipped analyses this session (transient API failures).
Private _skipCount As Integer = 0
```

Resets on each app start (in-memory only). Visible in status bar.

### 5b. Status bar — `MainForm_Render_Header.vb`

Update `UpdateLogInfo`:

**Before:**
```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    lblLogInfo.Text = String.Format("Log: {0} rows  |  {1}", rows, path)
End Sub
```

**After:**
```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    Dim skipSuffix As String = If(_skipCount > 0, String.Format("  |  Skipped: {0}", _skipCount), "")
    lblLogInfo.Text = String.Format("Log: {0} rows{1}  |  {2}", rows, skipSuffix, path)
End Sub
```

Skip suffix only appears when `_skipCount > 0`. Clean status bar in normal operation.

---

## 6. Settings Keys

### `Core/Settings/EngineSettings.vb` — new `NetworkSettings` class

```vb
' Add to EngineSettings class (alongside Indicators, Scoring, Kelly, etc.):
<JsonPropertyName("network")>
Public Property Network As New NetworkSettings

''' <summary>
''' API resilience parameters for the REST fetch layer.
''' RequestTimeoutSeconds: HttpClient.Timeout for each fetch.
''' RetryCount: additional retries on transient failure (5xx, timeout, network drop).
'''   0 = no retry; 1 = retry once (default); higher values stack but should rarely be needed.
''' RetryBackoffMs: delay between retries in milliseconds.
''' </summary>
Public Class NetworkSettings
    <JsonPropertyName("request_timeout_seconds")> Public Property RequestTimeoutSeconds As Integer = 15
    <JsonPropertyName("retry_count")>              Public Property RetryCount            As Integer = 1
    <JsonPropertyName("retry_backoff_ms")>         Public Property RetryBackoffMs        As Integer = 1000
End Class
```

### `settings.json` — add top-level `network` block

```json
"network": {
  "request_timeout_seconds": 15,
  "retry_count": 1,
  "retry_backoff_ms": 1000
}
```

| Key | Default | Purpose |
|---|---|---|
| `request_timeout_seconds` | 15 | Per-call HTTP timeout (raised from prior 10s hardcoded) |
| `retry_count` | 1 | Additional retries after the initial attempt; 0 disables retry |
| `retry_backoff_ms` | 1000 | Delay between retries |

Bumps `settings.json` to v18.

---

## 7. WebSocket Readiness Note

**Out of scope for this spec.** Documented here so the design choices are visible.

The current spec hardens REST polling. WebSocket migration will use a fundamentally different model:

- Persistent connection with heartbeat
- Last-known-good cache for each subscribed channel
- Reconnect logic with backoff on disconnect
- Different failure surface (partial frames, schema mismatches, server-initiated closes)

What this spec does **right** for the WebSocket future:

- The contract `DeribitClient.GetXxxAsync` returns null on hard failure — same contract works with WebSocket. The implementation behind it changes; the call site doesn't.
- `RunAnalysisAsync` checks for null and renders skip — same skip handling works regardless of fetch backend.
- `_skipCount` and status bar surface are reusable.

What this spec does **NOT** do for WebSocket:

- No persistent connection management.
- No subscription lifecycle.
- No reconnect / backoff strategy at the connection level (only at the request level).
- No partial-frame or schema-mismatch handling.

When the WebSocket migration spec lands, this resilience layer becomes the REST-specific implementation behind the same call-site contract. WebSocket gets its own resilience layer behind the same contract.

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `DeribitClient.vb` | Add `ExecuteWithRetry` private helper. Wrap each of the 5 public `GetXxxAsync` methods. Update timeout to read from cfg. Change return types: `Double` → `Double?` for funding rate, value tuple → nullable value tuple for book summary. |
| `Core/Settings/EngineSettings.vb` | Add `NetworkSettings` class + `Network` property on `EngineSettings`. |
| `UI/MainForm_Layout.vb` | Add `Private _skipCount As Integer = 0` field. |
| `UI/MainForm_Analysis.vb` | After `Task.WhenAll`, validate each result and skip cleanly with reason. Unwrap nullable value types where existing code consumed unwrapped values. Update 15m cache update path to preserve cache on fetch failure. |
| `UI/MainForm_Render_Header.vb` | Update `UpdateLogInfo` to surface `_skipCount` when > 0. |
| `settings.json` | Add top-level `network` block (3 keys). Bump version to v18. |

Approximate line count: ~80 lines net new code across `DeribitClient.vb` (largest delta, retry helper + 5 wrapper rewrites) plus ~25 lines across the rest.

---

## 9. Worked Examples

### Example A — Transient 525, retry succeeds

```
T+0.0s:  GetFundingRateAsync attempt 1 → HttpRequestException (525)
         Console: "[GetFundingRateAsync] Transient failure (attempt 1/2): 525"
T+1.0s:  Retry attempt 2 → success, returns 0.000009
RunAnalysisAsync:  fundingRate = 0.000009 (HasValue=True)
                   skipReason = Nothing → analysis proceeds
                   CSV row written normally
UI:                normal verdict display
Status bar:        no skip suffix
```

### Example B — Transient timeout × 2, skip

```
T+0.0s:  GetCandlesAsync("1", 250) attempt 1 → TaskCanceledException after 15s
         Console: "[GetCandlesAsync] Timeout (attempt 1/2)"
T+16.0s: Retry attempt 2 → TaskCanceledException after 15s
         Console: "[GetCandlesAsync] Timeout retry exhausted"
T+31.0s: Returns Nothing
RunAnalysisAsync:  candles1m Is Nothing → skipReason = "1m candles unavailable"
                   _skipCount += 1
                   UI shows: "ANALYSIS SKIPPED: 1m candles unavailable"
                            "Skip count this session: 1"
                   No CSV row written.
                   FINALLY re-enables button.
Auto-run timer:    fires next cycle (60s after this one started, so ~30s from now)
```

### Example C — Hard 4xx, no retry

```
T+0.0s:  GetCandlesAsync("1", 250) attempt 1 → HttpRequestException (404)
         StatusCode 404 → hard failure, no retry
         Console: "[GetCandlesAsync] Hard HTTP failure: 404"
         Returns Nothing immediately
RunAnalysisAsync:  skip with reason "1m candles unavailable"
```

This case usually indicates a code bug (bad URL, bad params). Not retried because retrying won't fix it.

### Example D — 15m fetch fails, cache preserved

```
T+0.0s:  Cache stale (older than 60s). GetCandlesAsync("15", 70) launched.
T+30.0s: 15m fetch fails → returns Nothing.
         Other six fetches succeed.
RunAnalysisAsync:  freshM15 Is Nothing → cache NOT overwritten.
                   _mtfCandles15m still has previous (now stale) candles.
                   Skip-check passes (15m not in the required-fetch list).
                   CalcMTFGate runs against stale 15m data — acceptable
                     because 15m candles change slowly (next bar in ~15 min).
                   Analysis proceeds normally.
                   CSV row written.
                   Cache will retry on next auto-run cycle when stale check fires again.
```

### Example E — Skip suffix in status bar

```
After 5 successful runs and 2 skips:
  "Log: 5 rows  |  Skipped: 2  |  C:\Dev\DeribitVerdictEngine\bin\..."
```

After 50 successful runs and 0 skips:
```
  "Log: 50 rows  |  C:\Dev\DeribitVerdictEngine\bin\..."
```

Suffix hidden when not relevant. Stays clean.

---

## 10. What This Does NOT Do

- Does **not** add a separate skip log file (`analysis_skip_log.csv`). In-memory counter only. If skip rate proves disruptive over 50+ runs, persistent skip logging can be added in a v0.5 spec.
- Does **not** add a CSV column for skip reason — skips don't write CSV rows. CSV schema unchanged from v0.3.
- Does **not** implement degraded-mode analysis (use defaults for missing fetches). Skip-on-any-failure is simpler and produces cleaner calibration data. Re-evaluate post-launch.
- Does **not** implement WebSocket reconnection logic. Out of scope; documented in Section 7.
- Does **not** change scoring, indicators, MTF gate, Pass 2c, Kelly, regime classification, or any analytic behaviour.
- Does **not** change `_http` instance lifecycle — still a single `Shared ReadOnly` instance per the existing pattern. Just initialized with the cfg-driven timeout.
- Does **not** change exception behaviour for non-transient failures within the inner lambda (e.g., a logic bug in JSON parsing). Hard failures return Nothing per the helper; the user sees a skip rather than a stack trace.

---

## 11. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors. Some null-warning suppressions may be needed where existing code consumed unwrapped values; be careful not to suppress real null risks.
2. **Smoke test — happy path:** run 10 analyses with normal connectivity. Confirm verdict + CSV rows appear normally. Status bar shows no skip suffix.
3. **Smoke test — induced failure:**
   - Disable internet briefly (5–10s) during an analysis.
   - Confirm UI shows "ANALYSIS SKIPPED: <fetch> unavailable".
   - Confirm `_skipCount` increments.
   - Confirm no CSV row added (compare row count before/after).
   - Re-enable internet; confirm next auto-run cycle succeeds normally.
4. **Smoke test — 15m cache preservation:** populate the cache with one successful run, then time the next run such that 15m fetch fails (e.g., temporarily blackhole 15m URL via firewall rule, or simulate by inducing failure). Confirm:
   - Analysis proceeds (does NOT skip).
   - MTF gate uses cached 15m data.
   - CSV row written.
5. **Long AFK test:** set auto-run to 60s, leave for 30 minutes. Return and check:
   - Skip count visible if any failures occurred.
   - No stack traces in `txtOutput` (latest run replaces prior content anyway, but check by glancing at history if errors occurred mid-test).
   - CSV row count = expected count − skip count.

If any of (3)–(5) fail, do not push to remote. Investigate.

---

## 12. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Skip-on-any-failure vs degraded-mode? | **Skip-on-any-failure for v0.4.** Cleaner CSV. Simpler implementation. Re-evaluate post-launch if skip rate >10% | Resolved |
| Q2 | Retry once vs exponential backoff? | **Retry once with fixed 1s backoff.** Catches genuinely transient flakes. Configurable via `retry_count` if user wants more | Resolved |
| Q3 | Should 15m fetch failure trigger skip? | **No.** 15m candles change slowly; cache TTL=60s makes stale data acceptable. Cache preservation logic in Section 4a handles this | Resolved |
| Q4 | What about 4xx errors (404, 403)? | **Hard failure, no retry.** 4xx indicates a code bug or auth issue — retrying won't fix it. Return Nothing immediately so `RunAnalysisAsync` skips with a clear reason | Resolved |
| Q5 | Should skips be logged to a file? | **Not in v0.4.** In-memory `_skipCount` is enough for AFK observation. Add file logging in v0.5 if skip rate proves high | Resolved |
| Q6 | Should `_skipCount` persist across restarts? | **No.** Per-session counter. AFK observation works on within-session basis. Persistent skip rate calibration is a v0.5 concern | Resolved |
| Q7 | Should `RequestTimeoutSeconds` be hot-reloadable? | **No.** `HttpClient.Timeout` is set at construction. Restart required for timeout change. Acceptable; network settings rarely tuned mid-session | Resolved |
| Q8 | What if Deribit is fully down for an extended period (e.g., 30+ min)? | **Engine continues to attempt and skip every cycle.** UI shows mounting skip count. User notices and stops auto-run manually. No special "circuit breaker" logic — out of scope; the user is the circuit breaker | Resolved |
| Q9 | Should the inner lambda's exception logic be moved into a shared retry policy (e.g., Polly NuGet package)? | **No.** Adds external dependency for ~30 lines of code. Inline implementation is fine for this scope | Resolved |
| Q10 | Should this spec coexist with existing exception handlers in `btnAnalyze_Click`? | **Yes.** The outer `Try/Catch` in `btnAnalyze_Click` stays as a last-line backstop for unexpected exceptions (e.g., logic bugs in `RunAnalysisAsync` itself). The resilience pass handles the *expected* failure cases (transient API issues) cleanly without invoking that catch | Resolved |
