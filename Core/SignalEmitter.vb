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
' TryWrite is the repo's standard atomic write (temp + atomic replace — the
' SettingsLoader/OhlcCache pattern). NOTE the existence guard is load-bearing:
' File.Replace THROWS when the destination does not exist, which is exactly the
' first-write case, so TryWrite falls back to File.Move there. Naming the API
' alone would pin something that cannot serve the requirement unaided —
' atomicity is the requirement, File.Replace is only half of how it is met.
' (Wording aligned 2026-08-04 with the order-app seat's §8.1 correction; the
' CODE already had the guard, only this comment was imprecise.)
' Creates the target directory if missing
' (§1 never-throw discipline), and NEVER throws (catch + console log).
'
' Host-agnostic: no WinForms, no MainForm coupling — CLI-port-ready. The two
' WinForms call sites live in UI/MainForm_SignalBridge.vb.

Imports System.Globalization
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>One side's placed levels — the shared arbitration result consumed by the
''' payload levels block, the CSV v0.8 Placed* columns, AND (since B4b) the snapshot +
''' card ATR rows and the Step 5b copy-out (parity by construction across all surfaces).
''' StopReason/TargetReason are the B4b source labels — Nothing on the legacy
''' (structural_levels.enabled=false) path, which renders/serialises exactly as v50.</summary>
Public Structure SideLevels
    Public Property Entry     As Double
    Public Property StopPx    As Double
    Public Property RawTarget As Double
    Public Property Target    As Double
    Public Property Capped    As Boolean
    Public Property Reason    As String
    ''' <summary>[B4b] Placed-stop source: SWING_STOP | STOP_CLAMPED | FALLBACK_ATR.
    ''' Nothing on the legacy path (stop is pure k×ATR there).</summary>
    Public Property StopReason   As String
    ''' <summary>[B4b] Placed-target source: SWING_HIGH_5M | SWING_LOW_5M |
    ''' NEAREST_HVN_ABOVE | NEAREST_HVN_BELOW | POC | FALLBACK_ATR. Always set on the
    ''' structural-first path (including noise-suppressed rows); Nothing on legacy.</summary>
    Public Property TargetReason As String
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

    ''' <summary>BTC-PERPETUAL price tick (USD). v1 is single-instrument (§6); the same
    ''' $0.5 anchors the v30 cap-noise floor. Basis for stop_min_floor_ticks.</summary>
    Public Const TickSize As Double = 0.5

    ''' <summary>
    ''' SHARED per-side placed-level arbitration — the ONE definition of "the effective
    ''' stop/target the engine would place for this run", consumed by the payload levels
    ''' block (BuildOk), the CSV v0.8 PlacedTarget/PlacedStop columns
    ''' (AnalysisLogger.LogRun), the snapshot + card ATR rows, and ScoringEngine Step 5b
    ''' (which copies the outputs onto v.Adjusted*/TargetCapReason* so the v35 min-move
    ''' gate evaluates the PLACED target) — all surfaces equal by construction.
    '''
    ''' [B4b] structural_levels.enabled=true → structural-first (proposal §3 amended by
    ''' DG1); reads ONLY r + cfg (never v — Step 5b calls it before v.Adjusted* exist):
    '''   TARGET ladder (priority, first tier with 0 &lt; dist ≤ target_max_atr_mult×ATR):
    '''     swing target → nearest HVN → POC (HVN-gated, as the legacy cap) →
    '''     fallback = entry ± fallbackMult×ATR, where fallbackMult is the session-resolved
    '''     scoring.atr_target_multiplier (structural_levels.sessions via r.SessionUtcHour).
    '''     Structure wins even when FARTHER than the ATR level, up to the bound.
    '''     Capped/Reason keep the v30 sub-tick noise suppression vs the fallback price;
    '''     reason format "PLACED @ p (LABEL)".
    '''   STOP (DG1): min(structural swing stop, stop_max_atr_mult×ATR) — structure places
    '''     only when tighter and ≥ stop_min_floor_ticks×tick. SWING_STOP when structural;
    '''     STOP_CLAMPED when the bound binds on an existing-but-wider structural stop
    '''     (D3 clamp; "skip" is the unbuilt D3-b alternative — unrecognised modes clamp);
    '''     FALLBACK_ATR when no structural stop exists or it is sub-floor.
    '''
    ''' structural_levels.enabled=false → the v50 legacy geometry BYTE-IDENTICAL (pure
    ''' k×ATR stop; target = v.Adjusted* when the Step 5b closest-wins cap fired, with the
    ''' v30 noise suppression; StopReason/TargetReason = Nothing so renderers take their
    ''' legacy branches). entry = the signal reference price (the close the pipeline
    ''' scored against); stop = the exit-trigger level.
    ''' </summary>
    Public Shared Function ComputeSideLevels(v As VerdictResult,
                                             r As IndicatorResults,
                                             cfg As EngineSettings,
                                             isLong As Boolean) As SideLevels
        Dim sl = cfg.Scoring.StructuralLevels
        If sl IsNot Nothing AndAlso sl.Enabled Then
            Return ComputeStructuralSideLevels(r, cfg, sl, isLong)
        End If

        ' ---- legacy path (v50 geometry, byte-identical — the enabled:false rollback) ----
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

    ' [B4b] The structural-first arbitration proper (one side). Pure in r + cfg —
    ' verdict-independent (both sides are always computable, exactly as the legacy
    ' levels block emitted both sides on every run including NO TRADE).
    '
    ' [geometry-arbitration-modes v56] Two arbitration modes + two signed buffers are now
    ' seamed here (docs/geometry-arbitration-modes-proposal.md §1). All defaults
    ' (target/stop mode 0, buffer 0.0) are BYTE-IDENTICAL to the v51 B4b path — the
    ' knobs are a what-if instrument at build; live activation is a later ⚠ D-table.
    ' The mode-0 branches below are the shipped v51 code verbatim; the mode-1 branches
    ' are additive; buffer application is a post-arbitration transform gated on pct ≠ 0
    ' so the default path skips it entirely. The 4-tick stop floor and Step-5c min-move
    ' gate evaluate the BUFFERED prices (buffers apply INSIDE the seam before outputs
    ' leave it — every downstream surface inherits by construction).
    Private Shared Function ComputeStructuralSideLevels(r As IndicatorResults,
                                                        cfg As EngineSettings,
                                                        sl As StructuralLevelsSettings,
                                                        isLong As Boolean) As SideLevels
        Dim entry As Double = r.CurrentPrice
        Dim dirSign As Double = If(isLong, 1.0, -1.0)

        Dim lv As New SideLevels
        lv.Entry = entry

        ' ---- TARGET candidate collection (shared by both modes) ----------------------
        Dim fallbackMult As Double = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, r.SessionUtcHour)
        Dim fallbackTarget As Double = entry + dirSign * (r.ATR * fallbackMult)
        Dim targetBound As Double = sl.TargetMaxAtrMult * r.ATR

        Dim placedTarget As Double = 0
        Dim targetLabel As String = Nothing

        Dim swingTarget As Double = If(isLong, r.SwingTargetLong, r.SwingTargetShort)
        Dim nearestHvn As Double = If(isLong, r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow)
        ' POC tier keeps the legacy HVN-proximity gate (VPFRSignal flags the side).
        Dim pocGated As Boolean = If(isLong,
            (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR"),
            (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL"))

        ' [D2-v2 v63] Best-pivot candidate — gated on use_best_pivot_candidate (default False ⇒
        ' candidate absent ⇒ every downstream branch below is byte-identical to v56). Side is
        ' PRICE-vs-entry (D3), the one rule live and replay share (CSV logs the price + ratio
        ' but not IsHigh). Zero pivot ⇒ candidate absent — counted, not guessed (POC precedent).
        Dim useBestPivot As Boolean = sl.UseBestPivotCandidate
        Dim bestPivot As Double = r.BestPivotByVolume5m
        Dim bestPivotDist As Double = dirSign * (bestPivot - entry)
        Dim bestPivotQualifies As Boolean = useBestPivot AndAlso bestPivot > 0 AndAlso
                                            bestPivotDist > 0 AndAlso bestPivotDist <= targetBound

        Dim swingDist As Double = dirSign * (swingTarget - entry)
        Dim swingQualifies As Boolean = swingTarget > 0 AndAlso swingDist > 0 AndAlso swingDist <= targetBound
        Dim hvnDist As Double = dirSign * (nearestHvn - entry)
        Dim hvnQualifies As Boolean = nearestHvn > 0 AndAlso hvnDist > 0 AndAlso hvnDist <= targetBound
        Dim pocDist As Double = dirSign * (r.VPFRPoc - entry)
        Dim pocQualifies As Boolean = pocGated AndAlso r.VPFRPoc > 0 AndAlso pocDist > 0 AndAlso pocDist <= targetBound

        If sl.TargetArbitrationMode = 1 Then
            ' Mode 1 — NEAREST: among qualifying structural candidates AND the session-resolved
            ' ATR fallback target, pick whichever is CLOSEST to entry (pre-B4b cap philosophy,
            ' generalised across all three tiers + the fallback). Labels stay truthful; fallback
            ' wins ⇒ placedTarget stays 0 so the shared fallback-path materialisation runs (same
            ' Reason=Nothing / Capped=False as today's fallback).
            Dim bestDist As Double = r.ATR * fallbackMult                 ' fallback distance
            Dim bestPrice As Double = fallbackTarget
            Dim bestLabel As String = Nothing                              ' Nothing ⇒ fallback wins
            ' [D2-v2] Best-pivot competes on distance like any other candidate (D1 orthogonal
            ' to arbitration mode). No priority — the min-distance rule alone decides.
            If bestPivotQualifies AndAlso bestPivotDist < bestDist Then
                bestDist = bestPivotDist
                bestPrice = bestPivot
                bestLabel = "BEST_PIVOT_5M"
            End If
            If swingQualifies AndAlso swingDist < bestDist Then
                bestDist = swingDist
                bestPrice = swingTarget
                bestLabel = If(isLong, "SWING_HIGH_5M", "SWING_LOW_5M")
            End If
            If hvnQualifies AndAlso hvnDist < bestDist Then
                bestDist = hvnDist
                bestPrice = nearestHvn
                bestLabel = If(isLong, "NEAREST_HVN_ABOVE", "NEAREST_HVN_BELOW")
            End If
            If pocQualifies AndAlso pocDist < bestDist Then
                bestDist = pocDist
                bestPrice = r.VPFRPoc
                bestLabel = "POC"
            End If
            If bestLabel IsNot Nothing Then
                placedTarget = bestPrice
                targetLabel = bestLabel
            End If
        Else
            ' Mode 0 — LADDER (v51 B4b): swing → HVN → POC (HVN-gated) → fallback, with the
            ' [D2-v2] best-pivot tier inserted as the FIRST tier ABOVE swing when enabled
            ' (D2 verbatim — P1's "4th cap tier above swing"). Structure wins even when farther
            ' than the ATR level, up to the looseness bound.
            If bestPivotQualifies Then
                placedTarget = bestPivot
                targetLabel = "BEST_PIVOT_5M"
            End If
            If placedTarget = 0 AndAlso swingQualifies Then
                placedTarget = swingTarget
                targetLabel = If(isLong, "SWING_HIGH_5M", "SWING_LOW_5M")
            End If
            If placedTarget = 0 AndAlso hvnQualifies Then
                placedTarget = nearestHvn
                targetLabel = If(isLong, "NEAREST_HVN_ABOVE", "NEAREST_HVN_BELOW")
            End If
            If placedTarget = 0 AndAlso pocQualifies Then
                placedTarget = r.VPFRPoc
                targetLabel = "POC"
            End If
        End If

        ' ---- Materialise target + buffer + noise suppression -------------------------
        ' At tBuf = 0 (default) the buffer branch is skipped ⇒ byte-identical to v51 B4b.
        Dim tBuf As Double = sl.TargetBufferPct
        If placedTarget > 0 Then
            Dim finalTarget As Double = placedTarget
            If tBuf <> 0.0 Then
                finalTarget = entry + (placedTarget - entry) * (1.0 + tBuf / 100.0)
            End If
            lv.Target = finalTarget
            lv.TargetReason = targetLabel
            ' v30 sub-tick noise suppression, measured against the fallback ATR price.
            Dim capNoiseFloor As Double = Math.Max(TickSize, r.ATR * 0.02)
            If Math.Abs(fallbackTarget - finalTarget) >= capNoiseFloor Then
                lv.Capped = True
                lv.Reason = String.Format(CultureInfo.InvariantCulture,
                                          "PLACED @ {0:F1} ({1})", finalTarget, targetLabel)
                If tBuf <> 0.0 Then
                    lv.Reason = lv.Reason & FormatBufferSuffix(tBuf)
                End If
            End If
        Else
            ' Fallback path — mode 0 always lands here when no structural tier qualifies,
            ' mode 1 lands here when fallback wins the closest-to-entry race.
            Dim finalFb As Double = fallbackTarget
            If tBuf <> 0.0 Then
                finalFb = entry + (fallbackTarget - entry) * (1.0 + tBuf / 100.0)
            End If
            lv.Target = finalFb
            lv.TargetReason = "FALLBACK_ATR"
            ' Buffered fallback: reason string surfaces the buffer suffix if it moved the
            ' price beyond the noise floor. At tBuf = 0 this is silent (Capped=False,
            ' Reason=Nothing) — byte-identical to v51 B4b's fallback rendering.
            If tBuf <> 0.0 Then
                Dim capNoiseFloor As Double = Math.Max(TickSize, r.ATR * 0.02)
                If Math.Abs(finalFb - fallbackTarget) >= capNoiseFloor Then
                    lv.Capped = True
                    lv.Reason = String.Format(CultureInfo.InvariantCulture,
                                              "PLACED @ {0:F1} (FALLBACK_ATR)", finalFb) &
                                FormatBufferSuffix(tBuf)
                End If
            End If
        End If
        lv.RawTarget = fallbackTarget

        ' ---- STOP arbitration --------------------------------------------------------
        Dim stopBound As Double = sl.StopMaxAtrMult * r.ATR
        Dim stopFloor As Double = sl.StopMinFloorTicks * TickSize
        Dim fallbackStopDist As Double = r.ATR * cfg.Scoring.AtrStopMultiplier
        Dim swingStop As Double = If(isLong, r.SwingStopLong, r.SwingStopShort)
        Dim stopDist As Double = dirSign * (entry - swingStop)   ' >0 = protective side

        Dim placedStopPx As Double
        Dim stopLabel As String

        If sl.StopArbitrationMode = 1 Then
            ' Mode 1 — WIDEST: max(structural swing stop distance, stop_max×ATR); structure
            ' wins when it is WIDER than the bound (no upper clamp — the trader's SL half).
            ' Below-floor structural distances always lose to the ATR distance (which is well
            ' above the 4-tick floor by construction: fallbackStopDist ≈ stopBound = 1.6×ATR).
            If swingStop > 0 AndAlso stopDist > stopBound Then
                placedStopPx = swingStop
                stopLabel = "SWING_STOP"
            Else
                placedStopPx = entry - dirSign * stopBound
                stopLabel = "FALLBACK_ATR"
            End If
        Else
            ' Mode 0 — DG1 verbatim: min(structural swing stop, stop_max×ATR) ≥ floor.
            If swingStop > 0 AndAlso stopDist > 0 AndAlso stopDist >= stopFloor AndAlso stopDist <= stopBound Then
                placedStopPx = swingStop
                stopLabel = "SWING_STOP"
            ElseIf swingStop > 0 AndAlso stopDist > stopBound Then
                ' D3 clamp (the only built mode; "skip" is D3-b, not implemented — any
                ' unrecognised stop_too_loose_mode value lands here identically).
                placedStopPx = entry - dirSign * stopBound
                stopLabel = "STOP_CLAMPED"
            Else
                ' No structural stop, wrong-side data, or sub-floor tightness.
                placedStopPx = entry - dirSign * fallbackStopDist
                stopLabel = "FALLBACK_ATR"
            End If
        End If

        ' Apply signed stop buffer, then enforce the 4-tick floor on the buffered stop
        ' (a −99% buffer snaps to the floor rather than crossing entry). Default 0.0 ⇒
        ' the branch is skipped and the stop is byte-identical to v51 B4b.
        Dim sBuf As Double = sl.StopBufferPct
        If sBuf <> 0.0 Then
            placedStopPx = entry + (placedStopPx - entry) * (1.0 + sBuf / 100.0)
            Dim bufStopDist As Double = dirSign * (entry - placedStopPx)
            If bufStopDist < stopFloor Then
                placedStopPx = entry - dirSign * stopFloor
            End If
        End If

        lv.StopPx = placedStopPx
        lv.StopReason = stopLabel

        Return lv
    End Function

    ' [geometry-arbitration-modes v56] Reason-string suffix for a non-zero buffer.
    ' Consumed only in the buffer ≠ 0 replay branches — no rendered surface at defaults.
    ' Explicit "+" sign on non-negatives so the display cleanly separates pushed-beyond
    ' (+N%) from shaved-toward-entry (−N%, sign from the Double).
    Private Shared Function FormatBufferSuffix(pct As Double) As String
        Dim sign As String = If(pct >= 0.0, "+", "")
        Return String.Format(CultureInfo.InvariantCulture, " BUF {0}{1:0.###}%", sign, pct)
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

    ''' <summary>Atomic write (temp + atomic replace — the repo pattern), creating the
    ''' target directory if missing. NEVER throws (§2 emitter discipline): failures
    ''' are console-logged and reported via the return value.
    ''' <para>The <c>File.Exists</c> guard below is LOAD-BEARING, not defensive:
    ''' <c>File.Replace</c> THROWS when the destination does not exist, which is exactly
    ''' the first-write case, so this falls back to <c>File.Move</c> there. Naming the API
    ''' alone would pin something that cannot serve the requirement unaided — atomicity is
    ''' the requirement, <c>File.Replace</c> is only half of how it is met.</para></summary>
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
