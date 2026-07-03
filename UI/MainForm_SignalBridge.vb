' UI/MainForm_SignalBridge.vb
' Signal Bridge v1 (docs/signal-bridge-v1-proposal.md, schema v1 FROZEN 2026-07-03) — thin WinForms glue.
'
' The engine half of the O2 signal bridge: after EVERY completed RunAnalysisAsync
' (success or skip) the run's payload is atomically written to
' signal_bridge.output_path via the host-agnostic Core/SignalEmitter. All schema
' mapping lives in the emitter (pure, harness-tested A22); this file only threads
' host state (feed health, the ARM toggle, the per-run signal id) into it.
'
' Emission discipline (§2): success payload is emitted AFTER BuildPlaintextSnapshot
' + the card binds (values = rendered values — the third parity surface); skip
' payload is emitted in the skip branch (SKIPPED = stand down, previous signal now
' stale); nothing is emitted on mid-run exceptions (silence = dead — the consumer's
' max-age gate covers engine death). Both glue subs are try/catch-hardened so the
' bridge can NEVER throw into the run (AnalysisOutputDump discipline).
'
' ARM AUTOTRADE (§4 / §8 D7) — the engine half of the dual-arm interlock:
' a visible checkbox (programmatic UI, the TAPE-checkbox pattern; created in
' MainForm_Layout.BuildCardGridLayout on the strip row, shown only while the
' bridge is enabled). RUNTIME-ONLY state: default OFF every start, deliberately
' NOT persisted to settings.json (interlock rule: restart = disarmed, both apps).
' Emitted as engine.autotrade_armed in every payload — emission itself is
' UNCONDITIONAL on arming (the consumer owns the decision to act; the engine
' never suppresses information for the bridge).

Imports System.Windows.Forms

Partial Public Class MainForm

    ' Created/parented in MainForm_Layout.BuildCardGridLayout (strip row, right
    ' side). NOT an RTF/snapshot/card surface — no card-binding obligation
    ' (engine display-string parity rule; same class as the TAPE checkbox).
    Friend chkArmAutotrade As CheckBox

    ' Runtime-only interlock state. No settings key, never persisted.
    Private _autotradeArmed As Boolean = False

    Private Sub OnArmAutotradeCheckChanged(sender As Object, e As EventArgs)
        _autotradeArmed = chkArmAutotrade.Checked
        ' Deliberately NO SettingsLoader.Save — restart = disarmed (§8 D7).
    End Sub

    ' Visibility follows signal_bridge.enabled ("shown when the bridge is
    ' enabled", §4). Synced at creation (Layout) and on every run via the
    ' emission glue below, which also covers a settings.json hot-reload flip
    ' by the next run.
    Private Sub SyncArmToggleVisibility(cfg As EngineSettings)
        If chkArmAutotrade IsNot Nothing AndAlso
           chkArmAutotrade.Visible <> cfg.SignalBridge.Enabled Then
            chkArmAutotrade.Visible = cfg.SignalBridge.Enabled
        End If
    End Sub

    ' -----------------------------------------------------------------------
    ' Emission glue — the two RunAnalysisAsync call sites route through here.
    ' -----------------------------------------------------------------------

    ''' <summary>Success path: full payload. Call AFTER the snapshot build + card
    ''' binds (the snapshot's inline CalcKellySizing populates v.Kelly*).</summary>
    Private Sub EmitBridgeSignal(v As VerdictResult, r As IndicatorResults,
                                 cfg As EngineSettings, signalId As Long)
        Try
            SyncArmToggleVisibility(cfg)
            If Not cfg.SignalBridge.Enabled Then Return
            Dim payload = SignalEmitter.BuildOk(v, r, cfg,
                                                ProcessIdentity.InstanceId, signalId,
                                                _autotradeArmed,
                                                CurrentBridgeWsHealth(cfg),
                                                _wsDegradedThisRun,
                                                DateTime.UtcNow)
            SignalEmitter.TryWrite(SignalEmitter.Serialize(payload),
                                   SignalEmitter.ResolveOutputPath(cfg.SignalBridge.OutputPath))
        Catch ex As Exception
            ' Never throw into the run — the bridge is an observer, not a gate.
            Console.WriteLine("[SignalBridge] emit failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Skip path: reduced SKIPPED payload — stand down + previous signal
    ''' now stale, never "hold last signal" (§2).</summary>
    Private Sub EmitBridgeSkipped(skipReason As String, cfg As EngineSettings, signalId As Long)
        Try
            SyncArmToggleVisibility(cfg)
            If Not cfg.SignalBridge.Enabled Then Return
            Dim payload = SignalEmitter.BuildSkipped(skipReason, cfg,
                                                     ProcessIdentity.InstanceId, signalId,
                                                     _autotradeArmed,
                                                     CurrentBridgeWsHealth(cfg),
                                                     _wsDegradedThisRun,
                                                     DateTime.UtcNow)
            SignalEmitter.TryWrite(SignalEmitter.Serialize(payload),
                                   SignalEmitter.ResolveOutputPath(cfg.SignalBridge.OutputPath))
        Catch ex As Exception
            Console.WriteLine("[SignalBridge] skip-emit failed: " & ex.Message)
        End Try
    End Sub

    ' health.ws from the live host state via the pure pinned derivation
    ' (SignalEmitter.DeriveWsHealth — REST | DEGRADED | DOWN | OK, §8 D8).
    Private Function CurrentBridgeWsHealth(cfg As EngineSettings) As String
        Return SignalEmitter.DeriveWsHealth(
            transportIsWs:=String.Equals(cfg.Network.Transport, "ws", StringComparison.OrdinalIgnoreCase),
            degradedThisRun:=_wsDegradedThisRun,
            feedExists:=_wsFeed IsNot Nothing,
            feedConnected:=_wsFeed IsNot Nothing AndAlso _wsFeed.IsConnected)
    End Function

End Class
