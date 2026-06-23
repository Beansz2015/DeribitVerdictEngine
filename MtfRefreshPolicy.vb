' MtfRefreshPolicy.vb
' WebSocket migration P3 (cutover §4 — 15m-TTL collapse on the WS path,
' docs/websocket-migration-p3-cutover-spec.md §4).
'
' Pure, host-agnostic predicate deciding whether RunAnalysisAsync should (re)fetch the 15m
' MTF series this run. Extracted from MainForm_Analysis so the WS-vs-REST branch is unit-
' testable: the live RunAnalysisAsync is WinForms-coupled and not harness-compiled, but the
' decision is plain data → the A16 harness exercises it directly (P3 §6 (b)). The fetch + the
' _mtfCandles15m / _mtfLastFetchTime cache update stay host-side in MainForm_Analysis (§4
' "the branch lives in MainForm_Analysis").
'
'   transport="ws"   → ALWAYS refresh. 15m is served from the in-memory MarketState (zero API
'                      cost, current forming bar), so the 60s TTL — which exists ONLY to spare
'                      the REST HTTP call — buys nothing. Reading every run keeps the MTF gate
'                      on the freshest 15m. The closed 15m bars are identical to REST; only the
'                      forming bar is up to ~60s more current, and 15m DMI/ADX + EMA move
'                      slowly, so the gate-flip rate is negligible (semantics-neutral per §4).
'   transport="rest" → keep the TTL gate: refresh only when there is no cached series yet OR
'                      the cache is at/over the TTL. The expression is byte-identical to the
'                      pre-P3 REST path, so transport="rest" is unchanged (the key safety prop).
Public NotInheritable Class MtfRefreshPolicy

    Private Sub New()
    End Sub

    ''' <summary>True ⇒ (re)fetch the 15m MTF series this run. WS path: always. REST path: the
    ''' original TTL gate — no cache yet, or the cache has reached/passed ttlSeconds.</summary>
    Public Shared Function ShouldRefresh(transport As String,
                                         haveCached As Boolean,
                                         secondsSinceLastFetch As Double,
                                         ttlSeconds As Integer) As Boolean
        If String.Equals(transport, "ws", StringComparison.OrdinalIgnoreCase) Then Return True
        Return (Not haveCached) OrElse secondsSinceLastFetch >= ttlSeconds
    End Function

End Class
