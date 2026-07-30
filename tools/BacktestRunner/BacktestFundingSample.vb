' tools/BacktestRunner/BacktestFundingSample.vb
' A single funding-rate history sample for the backtest store.
'
' Public + top-level, and since v64 in its OWN file, for two link reasons:
'
'   • ReplayLoop's at-or-before helper compiles into the fixtures without pulling
'     HistoricalStore (which owns a live HttpClient) into the harness project — the
'     original reason it was hoisted to a top-level type.
'   • The root project links HistoricalStore.vb for the in-app gap repair
'     (docs/in-app-trade-store-capture-proposal.md §1.2 / D5). HistoricalStore references
'     this struct, so while it lived at the top of ReplayLoop.vb the app could not link
'     one without the other — and ReplayLoop drags the whole replay pipeline in.
'
' Host-agnostic POCO. No behaviour, no dependencies.

Public Structure BacktestFundingSample
    Public Property TsMs As Long
    Public Property Rate As Double
End Structure
