' Core/SignalEmitter.vb
' [Signal Bridge v1] verdict_signal.json emitter (docs/signal-bridge-v1-proposal.md,
' schema v1 FROZEN 2026-07-03; canonical consumer copy:
' DeribitOrderPlacementApp/docs/integration-contract-verdictengine.md — §3 of the
' proposal is the verbatim emission/parity mirror this file implements).
'
' Build*(…) are PURE (fixture-testable, harness A22): they map the SAME
' VerdictResult/IndicatorResults fields the plaintext snapshot renders into the
' frozen schema — the payload is the engine's THIRD parity surface
' (snapshot ↔ cards ↔ signal file). Field-sourcing highlights:
'   - levels.*.target mirrors the snapshot's ATR-block logic 1:1, including the
'     v30 sub-tick cap-noise suppression (|raw − adjusted| < max(0.5, ATR×0.02)
'     renders uncapped → the file reports target_capped:false, matching the display).
'   - direction is NONE on ALL "NO TRADE*" verdicts (leans live in `verdict` for
'     logging, never actionable — §8 D2); WEAK verdicts DO carry their direction
'     (actionability is the consumer's confidence-tier gate, not this field's job).
'   - hold_status mirrors the snapshot's suppression: the "no open position"
'     sentinel emits null.
' Serialization pins (§9 item 6): System.Text.Json, numbers as JSON numbers,
' invariant culture, generated_at_utc ISO-8601 UTC with Z, JsonObject insertion
' order = the §3 schema order.
'
' TryWrite is the repo's standard atomic write (tmp + File.Replace — the
' SettingsLoader/OhlcCache pattern), creates the target directory if missing
' (§1 never-throw discipline), and NEVER throws (catch + console log).
'
' Host-agnostic: no WinForms, no MainForm coupling — CLI-port-ready. The two
' WinForms call sites live in UI/MainForm_SignalBridge.vb.

Imports System.Globalization
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>One side's placed levels — the shared arbitration result consumed by the
''' payload levels block AND the CSV v0.8 Placed* columns (parity by construction).</summary>
Public Structure SideLevels
    Public Property Entry     As Double
    Public Property StopPx    As Double
    Public Property RawTarget As Double
    Public Property Target    As Double
    Public Property Capped    As Boolean
    Public Property Reason    As String
End Structure

Public NotInheritable Class SignalEmitter

    Private Sub New()
    End Sub

    ''' <summary>Frozen contract version. Any schema change bumps this AND updates
    ''' both contract docs in one coordinated pass (§8 D10).</summary>
    Public Const SchemaVersion As Integer = 1

    ''' <summary>v1 is single-instrument (§6).</summary>
    Public Const Instrument As String = "BTC-PERPETUAL"

    Private Const AppName As String = "DeribitVerdictEngine"

    ' -----------------------------------------------------------------------
    ' Pure derivations (enum-pinned — harness A22c/A22d)
    ' -----------------------------------------------------------------------

    ''' <summary>Pinned enum: "LONG" | "SHORT" | "NONE". NONE on ALL "NO TRADE*"
    ''' verdicts — the prefix check MUST run before the substring checks because
    ''' lean tags ("NO TRADE [WEAK LONG]") contain the direction word.</summary>
    Public Shared Function DeriveDirection(verdict As String) As String
        If verdict Is Nothing OrElse verdict.StartsWith("NO TRADE") Then Return "NONE"
        If verdict.Contains("LONG") Then Return "LONG"
        If verdict.Contains("SHORT") Then Return "SHORT"
        Return "NONE"
    End Function

    ''' <summary>Pinned enum: "OK" | "DEGRADED" | "DOWN" | "REST" (§8 D8).
    ''' REST = engine deliberately on network.transport=rest (the WS feed is not
    ''' the verdict path); the consumer gates block DOWN only.</summary>
    Public Shared Function DeriveWsHealth(transportIsWs As Boolean,
                                          degradedThisRun As Boolean,
                                          feedExists As Boolean,
                                          feedConnected As Boolean) As String
        If Not transportIsWs Then Return "REST"
        If degradedThisRun Then Return "DEGRADED"
        If Not feedExists OrElse Not feedConnected Then Return "DOWN"
        Return "OK"
    End Function

    ' -----------------------------------------------------------------------
    ' Payload builders (pure)
    ' -----------------------------------------------------------------------

    ''' <summary>Full payload for a successful run (§2). MUST be called after
    ''' BuildPlaintextSnapshot + the card binds so v.Kelly* are populated and
    ''' payload values = rendered values (the parity anchor).</summary>
    Public Shared Function BuildOk(v As VerdictResult,
                                   r As IndicatorResults,
                                   cfg As EngineSettings,
                                   instanceId As String,
                                   signalId As Long,
                                   autotradeArmed As Boolean,
                                   wsHealth As String,
                                   degradedThisRun As Boolean,
                                   generatedAtUtc As DateTime) As JsonObject
        Dim o As JsonObject = BuildCommonHead(cfg, instanceId, signalId, autotradeArmed,
                                              generatedAtUtc, signalState:="OK", skipReason:=Nothing)

        o("verdict") = v.Verdict
        o("confidence") = v.Confidence
        o("direction") = DeriveDirection(v.Verdict)
        o("verdict_context") = v.VerdictContext
        o("mtf_blocked") = v.MTFGateBlocked
        o("scores") = New JsonObject From {
            {"long", v.LongScore},
            {"short", v.ShortScore},
            {"eff_long", v.EffectiveLongScore},
            {"eff_short", v.EffectiveShortScore},
            {"max", v.MaxScore}
        }

        o("price") = r.CurrentPrice
        o("exec_resolution_min") = r.ExecResolution
        o("trigger_mode") = cfg.AutoRun.TriggerMode
        o("atr") = r.ATR

        ' Same linear ATR distances the snapshot header computes (v32 D2). Routed
        ' through the SHARED per-side arbitration (ComputeSideLevels) — the same
        ' function the CSV v0.8 Placed* columns read, so payload and CSV levels
        ' cannot drift (aggressor-velocity boundary commit, placed-geometry D5).
        o("levels") = New JsonObject From {
            {"long", BuildSideLevels(ComputeSideLevels(v, r, cfg, isLong:=True))},
            {"short", BuildSideLevels(ComputeSideLevels(v, r, cfg, isLong:=False))}
        }

        ' Structural zeros = unset (§3 serialization pins) — emitted verbatim from
        ' the direction-aware bookkeeping the structural rows render.
        o("structural") = New JsonObject From {
            {"swing_target_long", r.SwingTargetLong},
            {"swing_stop_long", r.SwingStopLong},
            {"swing_target_short", r.SwingTargetShort},
            {"swing_stop_short", r.SwingStopShort}
        }

        ' Mirrors the snapshot's HOLD/EXIT suppression: the no-position sentinel → null.
        ' Informational until v2 makes posState truthful (§6).
        Dim holdStatus As String = v.HoldStatus
        If holdStatus = "N/A -- no open position" Then holdStatus = Nothing
        o("hold_status") = JStr(holdStatus)

        ' Advisory context only — never sizing in v1 (§8 D5). Fields are 0/false
        ' when the display suppresses the Kelly block (no edge).
        o("kelly") = New JsonObject From {
            {"contracts", v.KellyContracts},
            {"risk_usd", v.KellyRiskUsd},
            {"lev_capped", v.KellyLevCapped}
        }

        o("health") = BuildHealth(wsHealth, degradedThisRun, v.LedgerMismatch)
        Return o
    End Function

    ''' <summary>Reduced payload for a skipped run (§2): SKIPPED = stand down +
    ''' previous signal now stale, never "hold last signal". Only identity,
    ''' state, and health ride — no verdict/levels fields at all.</summary>
    Public Shared Function BuildSkipped(skipReason As String,
                                        cfg As EngineSettings,
                                        instanceId As String,
                                        signalId As Long,
                                        autotradeArmed As Boolean,
                                        wsHealth As String,
                                        degradedThisRun As Boolean,
                                        generatedAtUtc As DateTime) As JsonObject
        Dim o As JsonObject = BuildCommonHead(cfg, instanceId, signalId, autotradeArmed,
                                              generatedAtUtc, signalState:="SKIPPED", skipReason:=skipReason)
        ' No VerdictResult exists on a skip — ledger_mismatch reports false (nothing
        ' to mismatch this run); the consumer stands down on SKIPPED regardless.
        o("health") = BuildHealth(wsHealth, degradedThisRun, ledgerMismatch:=False)
        Return o
    End Function

    ' Shared head — §3 key order: schema_version, signal_id, generated_at_utc,
    ' engine, instrument, signal_state, skip_reason.
    Private Shared Function BuildCommonHead(cfg As EngineSettings,
                                            instanceId As String,
                                            signalId As Long,
                                            autotradeArmed As Boolean,
                                            generatedAtUtc As DateTime,
                                            signalState As String,
                                            skipReason As String) As JsonObject
        Dim o As New JsonObject()
        o("schema_version") = SchemaVersion
        o("signal_id") = signalId
        o("generated_at_utc") = generatedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",
                                                        CultureInfo.InvariantCulture)
        o("engine") = New JsonObject From {
            {"app", AppName},
            {"settings_version", cfg.Version},
            {"instance_id", instanceId},
            {"autotrade_armed", autotradeArmed}
        }
        o("instrument") = Instrument
        o("signal_state") = signalState
        o("skip_reason") = JStr(skipReason)
        Return o
    End Function

    ''' <summary>
    ''' SHARED per-side placed-level arbitration — the ONE definition of "the effective
    ''' stop/target the engine would place for this run", consumed by BOTH the payload
    ''' levels block (BuildOk) and the CSV v0.8 PlacedTarget/PlacedStop columns
    ''' (AnalysisLogger.LogRun), so the two surfaces are equal by construction.
    ''' Mirrors the snapshot ATR-block arbitration:
    '''   adjusted = 0            → raw ATR target, uncapped.
    '''   adjusted > 0, |raw−adj| &lt; max(0.5, ATR×0.02)
    '''                           → adjusted value but reported UNCAPPED (the v30
    '''                             sub-tick cap-noise suppression the display uses).
    '''   adjusted > 0 otherwise  → adjusted value, capped, reason verbatim.
    ''' entry = the signal reference price (the close the pipeline scored against);
    ''' stop = the exit-trigger level. Today's geometry: stop = pure k×ATR, target =
    ''' min(k×ATR, structural cap) — the placed-geometry structural-first pass (B4b)
    ''' replaces the INPUTS to this arbitration later; the sharing survives it.
    ''' </summary>
    Public Shared Function ComputeSideLevels(v As VerdictResult,
                                             r As IndicatorResults,
                                             cfg As EngineSettings,
                                             isLong As Boolean) As SideLevels
        Dim entry As Double = r.CurrentPrice
        Dim atrStop As Double = r.ATR * cfg.Scoring.AtrStopMultiplier
        Dim atrTarget As Double = r.ATR * cfg.Scoring.AtrTargetMultiplier
        Dim adjustedTarget As Double = If(isLong, v.AdjustedLongTarget, v.AdjustedShortTarget)
        Dim capReason As String = If(isLong, v.TargetCapReasonLong, v.TargetCapReasonShort)

        Dim lv As New SideLevels
        lv.Entry = entry
        lv.StopPx = If(isLong, entry - atrStop, entry + atrStop)
        lv.RawTarget = If(isLong, entry + atrTarget, entry - atrTarget)
        lv.Target = lv.RawTarget
        lv.Capped = False
        lv.Reason = Nothing
        If adjustedTarget > 0 Then
            lv.Target = adjustedTarget
            Dim capNoiseFloor As Double = Math.Max(0.5, r.ATR * 0.02)
            If Math.Abs(lv.RawTarget - adjustedTarget) >= capNoiseFloor Then
                lv.Capped = True
                lv.Reason = capReason
            End If
        End If
        Return lv
    End Function

    ' One side of the levels block — a thin JSON wrapper over the shared arbitration.
    Private Shared Function BuildSideLevels(lv As SideLevels) As JsonObject
        Return New JsonObject From {
            {"entry", lv.Entry},
            {"stop", lv.StopPx},
            {"target", lv.Target},
            {"target_capped", lv.Capped},
            {"cap_reason", JStr(lv.Reason)},
            {"raw_target", lv.RawTarget}
        }
    End Function

    Private Shared Function BuildHealth(wsHealth As String,
                                        degradedThisRun As Boolean,
                                        ledgerMismatch As Boolean) As JsonObject
        Return New JsonObject From {
            {"ws", wsHealth},
            {"degraded_this_run", degradedThisRun},
            {"ledger_mismatch", ledgerMismatch}
        }
    End Function

    ' Explicit null-capable string node (assigning Nothing through the JsonObject
    ' indexer is a valid JSON null, but the helper keeps intent readable).
    Private Shared Function JStr(s As String) As JsonNode
        If s Is Nothing Then Return Nothing
        Return JsonValue.Create(s)
    End Function

    ' -----------------------------------------------------------------------
    ' Serialization + atomic write
    ' -----------------------------------------------------------------------

    ''' <summary>Indented for trader eyeballing during the live smoke; numbers as
    ''' numbers, invariant culture by System.Text.Json construction (A22f proves it).</summary>
    Public Shared Function Serialize(payload As JsonObject) As String
        Return payload.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})
    End Function

    ''' <summary>Empty/whitespace configured path ⇒ beside the exe (§1 fallback semantic).</summary>
    Public Shared Function ResolveOutputPath(configuredPath As String) As String
        If String.IsNullOrWhiteSpace(configuredPath) Then
            Return Path.Combine(AppContext.BaseDirectory, "verdict_signal.json")
        End If
        Return configuredPath
    End Function

    ''' <summary>Atomic write (tmp + File.Replace — the repo pattern), creating the
    ''' target directory if missing. NEVER throws (§2 emitter discipline): failures
    ''' are console-logged and reported via the return value.</summary>
    Public Shared Function TryWrite(json As String, path As String) As Boolean
        Dim tmpPath As String = path & ".tmp"
        Try
            Dim dir As String = IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            File.WriteAllText(tmpPath, json)
            If File.Exists(path) Then
                File.Replace(tmpPath, path, Nothing)
            Else
                File.Move(tmpPath, path)
            End If
            Return True
        Catch ex As Exception
            Console.WriteLine("[SignalEmitter] write failed: " & ex.Message)
            Try : File.Delete(tmpPath) : Catch : End Try
            Return False
        End Try
    End Function

End Class
