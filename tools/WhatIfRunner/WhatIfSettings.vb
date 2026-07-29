' tools/WhatIfRunner/WhatIfSettings.vb
' Clones the live EngineSettings per grid cell and applies a cell's whitelisted knob
' values through the strongly-typed setters — the whitelist is enforced HERE too
' (an unrecognised path throws), so the arbitration a cell replays under is a genuine
' EngineSettings, not a bag of loose numbers. docs/offline-whatif-replay-proposal.md §2/§3.
'
' The runner NEVER writes settings.json (§4 guard-rail 1). It reads the live file once,
' deserialises a fresh copy per cell, and mutates only the in-memory clone.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text.Json

Public Class WhatIfSettings

    Private ReadOnly _json As String
    Private ReadOnly _opts As JsonSerializerOptions

    ''' <summary>The live settings, deserialised once, used as the BASELINE cfg and for
    ''' resolving inherited-knob values (constraints + report "vs live" marking).</summary>
    Public ReadOnly Property Live As EngineSettings

    Public Sub New(settingsPath As String)
        If Not File.Exists(settingsPath) Then
            Throw New FileNotFoundException("settings.json not found for the What-If baseline: " & settingsPath)
        End If
        _json = File.ReadAllText(settingsPath)
        _opts = New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
        Live = Deserialise()
    End Sub

    ''' <summary>A fresh, independent EngineSettings clone (no mutation bleed between cells).</summary>
    Public Function Clone() As EngineSettings
        Return Deserialise()
    End Function

    Private Function Deserialise() As EngineSettings
        Dim cfg = JsonSerializer.Deserialize(Of EngineSettings)(_json, _opts)
        If cfg Is Nothing Then Throw New Exception("settings.json deserialised to nothing.")
        Return cfg
    End Function

    ''' <summary>Build the cfg for one grid cell: clone the live settings, then set each
    ''' whitelisted knob the cell overrides. eval_window is NOT a settings key — it is a
    ''' replay/report dimension, so it is skipped here and read by the replay separately.</summary>
    Public Function BuildCellSettings(cell As Dictionary(Of String, Double)) As EngineSettings
        Dim cfg = Clone()
        For Each kv In cell
            If String.Equals(kv.Key, "eval_window", StringComparison.Ordinal) Then Continue For
            ApplyKnob(cfg, kv.Key, kv.Value)
        Next
        Return cfg
    End Function

    ''' <summary>The live value of a knob path (for constraint resolution and the report's
    ''' pinned-vs-inherited marking). Reads the same paths BuildCellSettings writes.</summary>
    Public Function LiveValueOf(path As String) As Double
        Return ReadKnob(Live, path)
    End Function

    ' -- Whitelisted setters (the code-enforced §2 whitelist, second gate) -----------------
    Private Shared Sub ApplyKnob(cfg As EngineSettings, path As String, value As Double)
        Dim s = cfg.Scoring
        Select Case path
            Case "scoring.atr_target_multiplier"                       : s.AtrTargetMultiplier = value
            Case "scoring.atr_stop_multiplier"                         : s.AtrStopMultiplier = value
            Case "scoring.verdict_strong_pct"                          : s.VerdictStrongPct = value
            Case "scoring.verdict_med_pct"                             : s.VerdictMedPct = value
            Case "scoring.verdict_weak_pct"                            : s.VerdictWeakPct = value
            ' [v62] The floor sweep IS the min-net sweep at fixed fees — the fee/style keys are
            ' deliberately NOT sweepable (a fee sweep answers no question the trader can act on).
            Case "scoring.trade_costs.min_net_move_pct"                : s.TradeCosts.MinNetMovePct = value
            Case "scoring.tier_floor.high_threshold"                   : s.TierFloor.HighThreshold = CInt(value)
            Case "scoring.tier_floor.high_floor"                       : s.TierFloor.HighFloor = CInt(value)
            Case "scoring.tier_floor.med_threshold"                    : s.TierFloor.MedThreshold = CInt(value)
            Case "scoring.tier_floor.med_floor"                        : s.TierFloor.MedFloor = CInt(value)
            Case "scoring.tier_floor.low_threshold"                    : s.TierFloor.LowThreshold = CInt(value)
            Case "scoring.tier_floor.low_floor"                        : s.TierFloor.LowFloor = CInt(value)
            Case "scoring.structural_levels.target_max_atr_mult"       : s.StructuralLevels.TargetMaxAtrMult = value
            Case "scoring.structural_levels.stop_max_atr_mult"         : s.StructuralLevels.StopMaxAtrMult = value
            Case "scoring.structural_levels.stop_min_floor_ticks"      : s.StructuralLevels.StopMinFloorTicks = CInt(value)
            Case "scoring.structural_levels.sessions.NY.fallback_target_atr_mult"     : SetSessionFallback(s, "NY", value)
            Case "scoring.structural_levels.sessions.LONDON.fallback_target_atr_mult" : SetSessionFallback(s, "LONDON", value)
            Case "scoring.structural_levels.sessions.ASIA.fallback_target_atr_mult"   : SetSessionFallback(s, "ASIA", value)
            ' [geometry-arbitration-modes v56] Modes are int-coded; the numeric sweep machinery
            ' handles them unchanged (a {0,1} sweep is a mode comparison).
            Case "scoring.structural_levels.target_arbitration_mode"                 : s.StructuralLevels.TargetArbitrationMode = CInt(value)
            Case "scoring.structural_levels.stop_arbitration_mode"                   : s.StructuralLevels.StopArbitrationMode = CInt(value)
            Case "scoring.structural_levels.target_buffer_pct"                       : s.StructuralLevels.TargetBufferPct = value
            Case "scoring.structural_levels.stop_buffer_pct"                         : s.StructuralLevels.StopBufferPct = value
            ' [D2-v2 v63] Boolean-as-int sweep: any non-zero ⇒ true (v56 int-mode precedent).
            Case "scoring.structural_levels.use_best_pivot_candidate"                : s.StructuralLevels.UseBestPivotCandidate = (value <> 0)
            Case Else
                Throw New WhatIfOverlayError("No setter for whitelisted knob '" & path & "' — whitelist/setter drift.")
        End Select
    End Sub

    Private Shared Sub SetSessionFallback(s As ScoringSettings, sessionName As String, value As Double)
        Dim sess = s.StructuralLevels.Sessions
        If sess Is Nothing Then
            sess = New Dictionary(Of String, StructuralLevelsSessionOverride)()
            s.StructuralLevels.Sessions = sess
        End If
        If Not sess.ContainsKey(sessionName) Then sess(sessionName) = New StructuralLevelsSessionOverride()
        sess(sessionName).FallbackTargetAtrMult = value
    End Sub

    Private Shared Function ReadKnob(cfg As EngineSettings, path As String) As Double
        Dim s = cfg.Scoring
        Select Case path
            Case "scoring.atr_target_multiplier"                       : Return s.AtrTargetMultiplier
            Case "scoring.atr_stop_multiplier"                         : Return s.AtrStopMultiplier
            Case "scoring.verdict_strong_pct"                          : Return s.VerdictStrongPct
            Case "scoring.verdict_med_pct"                             : Return s.VerdictMedPct
            Case "scoring.verdict_weak_pct"                            : Return s.VerdictWeakPct
            Case "scoring.trade_costs.min_net_move_pct"                : Return s.TradeCosts.MinNetMovePct
            Case "scoring.tier_floor.high_threshold"                   : Return s.TierFloor.HighThreshold
            Case "scoring.tier_floor.high_floor"                       : Return s.TierFloor.HighFloor
            Case "scoring.tier_floor.med_threshold"                    : Return s.TierFloor.MedThreshold
            Case "scoring.tier_floor.med_floor"                        : Return s.TierFloor.MedFloor
            Case "scoring.tier_floor.low_threshold"                    : Return s.TierFloor.LowThreshold
            Case "scoring.tier_floor.low_floor"                        : Return s.TierFloor.LowFloor
            Case "scoring.structural_levels.target_max_atr_mult"       : Return s.StructuralLevels.TargetMaxAtrMult
            Case "scoring.structural_levels.stop_max_atr_mult"         : Return s.StructuralLevels.StopMaxAtrMult
            Case "scoring.structural_levels.stop_min_floor_ticks"      : Return s.StructuralLevels.StopMinFloorTicks
            Case "scoring.structural_levels.sessions.NY.fallback_target_atr_mult"     : Return SessionFallback(s, "NY")
            Case "scoring.structural_levels.sessions.LONDON.fallback_target_atr_mult" : Return SessionFallback(s, "LONDON")
            Case "scoring.structural_levels.sessions.ASIA.fallback_target_atr_mult"   : Return SessionFallback(s, "ASIA")
            Case "scoring.structural_levels.target_arbitration_mode"                 : Return s.StructuralLevels.TargetArbitrationMode
            Case "scoring.structural_levels.stop_arbitration_mode"                   : Return s.StructuralLevels.StopArbitrationMode
            Case "scoring.structural_levels.target_buffer_pct"                       : Return s.StructuralLevels.TargetBufferPct
            Case "scoring.structural_levels.stop_buffer_pct"                         : Return s.StructuralLevels.StopBufferPct
            Case "scoring.structural_levels.use_best_pivot_candidate"                : Return If(s.StructuralLevels.UseBestPivotCandidate, 1.0, 0.0)
            Case "eval_window"                                         : Return 0   ' not a settings key
            Case Else
                Throw New WhatIfOverlayError("No reader for knob '" & path & "'.")
        End Select
    End Function

    ' Session fallback resolves to the global atr_target_multiplier when the override is
    ' Nothing (the live inheritance rule ExecutionResolution.ResolveFallbackTargetMultiplier uses).
    Private Shared Function SessionFallback(s As ScoringSettings, sessionName As String) As Double
        Dim sess = s.StructuralLevels.Sessions
        If sess IsNot Nothing AndAlso sess.ContainsKey(sessionName) AndAlso
           sess(sessionName) IsNot Nothing AndAlso sess(sessionName).FallbackTargetAtrMult.HasValue Then
            Return sess(sessionName).FallbackTargetAtrMult.Value
        End If
        Return s.AtrTargetMultiplier
    End Function
End Class
