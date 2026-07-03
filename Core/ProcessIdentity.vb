' Core/ProcessIdentity.vb
' [Signal Bridge v1] Shared process-identity primitive (docs/signal-bridge-v1-proposal.md §4).
'
' One GUID minted at process start + one counter incremented once per completed
' analysis run (success AND skip — both emit a bridge payload). Deliberately NOT
' emitter-private: consumed twice —
'   (a) the SignalEmitter's engine.instance_id + signal_id payload fields;
'   (b) the CSV InstanceId/SignalId attribution columns added at the #5 v0.8
'       header rotation (launch attribution for the book + the soak-review join
'       to the order-app disposition log).
' CSV SignalId ≡ payload signal_id per run by construction: RunAnalysisAsync
' ticks NextSignalId() once per completed run BEFORE the CSV write and hands the
' same value to the emitter. SKIPPED runs consume an id but write no CSV row —
' the join is total CSV→payload, partial payload→CSV, by design.
'
' Readable with the bridge disabled (the counter ticks regardless of
' signal_bridge.enabled). De-dupe key consumer-side = (instance_id, signal_id);
' gaps are legal (an exception between tick and emission burns an id — "never
' on error paths mid-run" §2), monotonicity within a process is the guarantee.
'
' Host-agnostic: no WinForms, no MainForm coupling — CLI-port-ready.

Imports System.Threading

Public NotInheritable Class ProcessIdentity

    Private Sub New()
    End Sub

    ' Minted once per process (static init on first touch — process start for
    ' every real consumer, since the first analysis run reads it).
    Private Shared ReadOnly _instanceId As String = Guid.NewGuid().ToString("D")

    Private Shared _signalId As Long = 0

    ''' <summary>Stable per engine process. The payload's engine.instance_id.</summary>
    Public Shared ReadOnly Property InstanceId As String
        Get
            Return _instanceId
        End Get
    End Property

    ''' <summary>The most recently issued signal id (0 = no completed run yet).
    ''' The #5 v0.8 CSV columns read this at LogRun time.</summary>
    Public Shared ReadOnly Property CurrentSignalId As Long
        Get
            Return Interlocked.Read(_signalId)
        End Get
    End Property

    ''' <summary>Issue the next per-run signal id. Called exactly once per
    ''' completed run (success or skip) by RunAnalysisAsync. Monotonic within
    ''' the process; starts at 1.</summary>
    Public Shared Function NextSignalId() As Long
        Return Interlocked.Increment(_signalId)
    End Function

End Class
