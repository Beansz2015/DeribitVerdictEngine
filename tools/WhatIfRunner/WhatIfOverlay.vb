' tools/WhatIfRunner/WhatIfOverlay.vb
' Overlay parsing, the v1 knob whitelist, grid-sweep expansion, constraint pruning,
' and cell → EngineSettings application for the offline What-If replay runner.
' docs/offline-whatif-replay-proposal.md §2 (whitelist), §3b (grid), W2 (overlay format).
'
' One semantic, no modes (§3b): every knob contributes a value-set to the grid —
'   blank/omitted = inherit the live settings value,
'   single value  = pinned (a one-point set),
'   range         = swept ({"sweep": {"from":.., "to":.., "step":..}}),
' and the grid is the cartesian product of all sets. The whitelist is enforced in
' code: an overlay key outside it fails the run with a NAMED error — no silent
' no-ops (the v47-F1 lesson).
'
' Host-agnostic: no System.Windows.Forms references. net8.0, Linux-portable.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text.Json

''' <summary>Thrown when the overlay references a knob outside the §2 whitelist,
''' malforms a sweep/constraint, or blows the ≤1,000-cell readability cap. The
''' message names the offending path so the failure is loud, never silent.</summary>
Public Class WhatIfOverlayError
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>One knob's contribution to the grid: a pinned single value or a swept range.
''' Absent knobs never become a WhatIfKnobSpec (they inherit the live value).</summary>
Public Class WhatIfKnobSpec
    Public Property Path   As String
    Public Property Values As List(Of Double)   ' expanded value-set (1 element when pinned)
    Public Property IsSweep As Boolean
End Class

''' <summary>A ratio guard-rail that prunes cells before they run (§3b):
''' min ≤ value(of[0]) / value(of[1]) ≤ max, evaluated against each cell's
''' effective values (cell override, else live).</summary>
Public Class WhatIfRatioConstraint
    Public Property Numerator   As String
    Public Property Denominator As String
    Public Property Min         As Double = Double.NegativeInfinity
    Public Property Max         As Double = Double.PositiveInfinity
End Class

''' <summary>The parsed overlay: the per-knob value-sets + any ratio constraints.
''' A cell is a Dictionary(path → value); a knob absent from the cell inherits live.</summary>
Public Class WhatIfOverlay
    Public Property Knobs       As New List(Of WhatIfKnobSpec)()
    Public Property Constraints As New List(Of WhatIfRatioConstraint)()

    ''' <summary>Hard grid cap. Raised 1000 → 3000 (2026-07-17) after measuring: the replay
    ''' re-walks per cell, so on the ~4k-row book 1000 cells ≈ 9s, 3000 ≈ 23s, 5000 ≈ 40s,
    ''' 10000 &gt; 2 min — 3000 is the compute-safe knee with headroom as the book grows.
    ''' Readability is handled separately (the ranking table shows only the top cells, see
    ''' WhatIfReport), and the multiple-comparisons risk stays covered by the split-half
    ''' holdout + the overfit counter (§3b/§4).</summary>
    Public Const MaxGridCells As Integer = 3000

    ' -- The v1 knob whitelist (§2). Numeric leaves only. Anything else fails loudly. --
    ' eval_window is the matrix window dimension (bar-count budget 5/10/15 per §2), not a
    ' settings key — resolved to minutes × row resolution by the replay.
    Public Shared ReadOnly Whitelist As HashSet(Of String) = New HashSet(Of String)(StringComparer.Ordinal) From {
        "scoring.atr_target_multiplier",
        "scoring.atr_stop_multiplier",
        "scoring.verdict_strong_pct",
        "scoring.verdict_med_pct",
        "scoring.verdict_weak_pct",
        "scoring.trade_costs.min_net_move_pct",
        "scoring.tier_floor.high_threshold",
        "scoring.tier_floor.high_floor",
        "scoring.tier_floor.med_threshold",
        "scoring.tier_floor.med_floor",
        "scoring.tier_floor.low_threshold",
        "scoring.tier_floor.low_floor",
        "scoring.structural_levels.target_max_atr_mult",
        "scoring.structural_levels.stop_max_atr_mult",
        "scoring.structural_levels.stop_min_floor_ticks",
        "scoring.structural_levels.sessions.NY.fallback_target_atr_mult",
        "scoring.structural_levels.sessions.LONDON.fallback_target_atr_mult",
        "scoring.structural_levels.sessions.ASIA.fallback_target_atr_mult",
        "scoring.structural_levels.target_arbitration_mode",
        "scoring.structural_levels.stop_arbitration_mode",
        "scoring.structural_levels.target_buffer_pct",
        "scoring.structural_levels.stop_buffer_pct",
        "scoring.structural_levels.use_best_pivot_candidate",
        "eval_window"
    }

    ' Verdict-shaping knobs: their presence means the directional population can shift,
    ' so the report always prints the population-shift line (§4). (Placement knobs can
    ' also flip the min-move gate, so in practice the runner always shows the line — this
    ' set just documents intent.)
    Public Shared ReadOnly VerdictKnobs As HashSet(Of String) = New HashSet(Of String)(StringComparer.Ordinal) From {
        "scoring.verdict_strong_pct", "scoring.verdict_med_pct", "scoring.verdict_weak_pct",
        "scoring.trade_costs.min_net_move_pct",
        "scoring.tier_floor.high_threshold", "scoring.tier_floor.high_floor",
        "scoring.tier_floor.med_threshold", "scoring.tier_floor.med_floor",
        "scoring.tier_floor.low_threshold", "scoring.tier_floor.low_floor"
    }

    ''' <summary>Parse an overlay JSON fragment (partial settings.json shape + optional
    ''' top-level "constraints"). Throws WhatIfOverlayError on any off-whitelist path,
    ''' malformed sweep, or unknown constraint form.</summary>
    Public Shared Function Parse(json As String) As WhatIfOverlay
        Dim result As New WhatIfOverlay()
        Dim doc As JsonDocument
        Try
            doc = JsonDocument.Parse(json)
        Catch ex As Exception
            Throw New WhatIfOverlayError("Overlay is not valid JSON: " & ex.Message)
        End Try

        Using doc
            Dim root = doc.RootElement
            If root.ValueKind <> JsonValueKind.Object Then
                Throw New WhatIfOverlayError("Overlay root must be a JSON object.")
            End If

            For Each prop In root.EnumerateObject()
                If String.Equals(prop.Name, "constraints", StringComparison.Ordinal) Then
                    ParseConstraints(prop.Value, result)
                Else
                    WalkKnobs(prop.Value, prop.Name, result)
                End If
            Next
        End Using

        Return result
    End Function

    ' Recursively collect knob specs. A numeric leaf = pinned; an object carrying a
    ' "sweep" property = swept; any other object recurses (extending the dotted path).
    Private Shared Sub WalkKnobs(el As JsonElement, path As String, result As WhatIfOverlay)
        Select Case el.ValueKind
            Case JsonValueKind.Number
                RequireWhitelisted(path)
                result.Knobs.Add(New WhatIfKnobSpec With {
                    .Path = path, .IsSweep = False,
                    .Values = New List(Of Double) From {el.GetDouble()}})

            Case JsonValueKind.Object
                Dim sweepEl As JsonElement
                If el.TryGetProperty("sweep", sweepEl) Then
                    RequireWhitelisted(path)
                    result.Knobs.Add(New WhatIfKnobSpec With {
                        .Path = path, .IsSweep = True,
                        .Values = ExpandSweep(path, sweepEl)})
                Else
                    For Each child In el.EnumerateObject()
                        WalkKnobs(child.Value, path & "." & child.Name, result)
                    Next
                End If

            Case Else
                Throw New WhatIfOverlayError(
                    String.Format("Overlay knob '{0}' must be a number or a {{""sweep"": ...}} object, got {1}.",
                                  path, el.ValueKind))
        End Select
    End Sub

    Private Shared Sub RequireWhitelisted(path As String)
        If Not Whitelist.Contains(path) Then
            Throw New WhatIfOverlayError(
                "Overlay knob '" & path & "' is NOT in the v1 backtestable-from-logs whitelist (proposal §2). " &
                "Rejected rather than silently ignored. Whitelisted knobs: " &
                String.Join(", ", Whitelist.OrderBy(Function(k) k)))
        End If
    End Sub

    ' {"from": a, "to": b, "step": s} → inclusive-ish arithmetic set. step must be > 0
    ' and from ≤ to. A tiny epsilon guards float drift so the endpoint is included.
    Private Shared Function ExpandSweep(path As String, sweepEl As JsonElement) As List(Of Double)
        If sweepEl.ValueKind <> JsonValueKind.Object Then
            Throw New WhatIfOverlayError("Sweep for '" & path & "' must be an object {from,to,step}.")
        End If
        Dim fromV = ReadNum(sweepEl, "from", path)
        Dim toV = ReadNum(sweepEl, "to", path)
        Dim stepV = ReadNum(sweepEl, "step", path)
        If stepV <= 0 Then Throw New WhatIfOverlayError("Sweep step for '" & path & "' must be > 0.")
        If toV < fromV Then Throw New WhatIfOverlayError("Sweep 'to' < 'from' for '" & path & "'.")

        Dim values As New List(Of Double)()
        Dim eps As Double = stepV * 0.000001
        Dim v As Double = fromV
        Dim guard As Integer = 0
        While v <= toV + eps
            values.Add(Math.Round(v, 10))
            v += stepV
            guard += 1
            If guard > MaxGridCells Then
                Throw New WhatIfOverlayError("Sweep for '" & path & "' expands past the " &
                                             MaxGridCells & "-cell cap on its own.")
            End If
        End While
        Return values
    End Function

    Private Shared Function ReadNum(obj As JsonElement, key As String, path As String) As Double
        Dim el As JsonElement
        If Not obj.TryGetProperty(key, el) OrElse el.ValueKind <> JsonValueKind.Number Then
            Throw New WhatIfOverlayError("Sweep for '" & path & "' is missing numeric '" & key & "'.")
        End If
        Return el.GetDouble()
    End Function

    Private Shared Sub ParseConstraints(el As JsonElement, result As WhatIfOverlay)
        If el.ValueKind <> JsonValueKind.Array Then
            Throw New WhatIfOverlayError("'constraints' must be a JSON array.")
        End If
        For Each c In el.EnumerateArray()
            Dim ratioEl As JsonElement
            If Not c.TryGetProperty("ratio", ratioEl) Then
                Throw New WhatIfOverlayError("Only 'ratio' constraints are supported in v1.")
            End If
            Dim ofEl As JsonElement
            If Not ratioEl.TryGetProperty("of", ofEl) OrElse ofEl.ValueKind <> JsonValueKind.Array Then
                Throw New WhatIfOverlayError("ratio.of must be a 2-element array of knob paths.")
            End If
            Dim paths = ofEl.EnumerateArray().Select(Function(x) x.GetString()).ToList()
            If paths.Count <> 2 Then
                Throw New WhatIfOverlayError("ratio.of must name exactly two knob paths.")
            End If
            For Each p In paths
                If Not Whitelist.Contains(p) Then
                    Throw New WhatIfOverlayError("ratio.of references off-whitelist knob '" & p & "'.")
                End If
            Next
            Dim con As New WhatIfRatioConstraint With {.Numerator = paths(0), .Denominator = paths(1)}
            Dim minEl As JsonElement, maxEl As JsonElement
            If ratioEl.TryGetProperty("min", minEl) AndAlso minEl.ValueKind = JsonValueKind.Number Then con.Min = minEl.GetDouble()
            If ratioEl.TryGetProperty("max", maxEl) AndAlso maxEl.ValueKind = JsonValueKind.Number Then con.Max = maxEl.GetDouble()
            result.Constraints.Add(con)
        Next
    End Sub

    ''' <summary>Expand the value-sets into the cartesian product, prune by constraints,
    ''' and enforce the ≤1,000-cell cap. Each cell is a Dictionary(path → value); an
    ''' all-singles overlay yields a single cell = the plain §1 backtest. liveValueOf
    ''' resolves inherited knobs so a constraint can reference an un-swept knob.</summary>
    Public Function ExpandGrid(liveValueOf As Func(Of String, Double)) As List(Of Dictionary(Of String, Double))
        Dim cells As New List(Of Dictionary(Of String, Double))() From {New Dictionary(Of String, Double)()}

        For Each knob In Knobs
            Dim next_ As New List(Of Dictionary(Of String, Double))()
            For Each partial_ In cells
                For Each v In knob.Values
                    Dim c As New Dictionary(Of String, Double)(partial_)
                    c(knob.Path) = v
                    next_.Add(c)
                Next
            Next
            cells = next_
            If cells.Count > MaxGridCells Then
                Throw New WhatIfOverlayError(String.Format(
                    "Grid expanded to {0} cells — over the {1}-cell cap (compute grows per cell; readability + " &
                    "multiple-comparisons above it). Narrow a sweep or pin a knob.", cells.Count, MaxGridCells))
            End If
        Next

        ' Constraint pruning against each cell's effective values (cell override, else live).
        If Constraints.Count > 0 Then
            cells = cells.Where(Function(c) PassesConstraints(c, liveValueOf)).ToList()
        End If

        If cells.Count = 0 Then
            Throw New WhatIfOverlayError("Every grid cell was pruned by the constraints — nothing to run.")
        End If
        Return cells
    End Function

    Private Function PassesConstraints(cell As Dictionary(Of String, Double),
                                       liveValueOf As Func(Of String, Double)) As Boolean
        For Each con In Constraints
            Dim num As Double = If(cell.ContainsKey(con.Numerator), cell(con.Numerator), liveValueOf(con.Numerator))
            Dim den As Double = If(cell.ContainsKey(con.Denominator), cell(con.Denominator), liveValueOf(con.Denominator))
            If den = 0 Then Return False
            Dim ratio As Double = num / den
            If ratio < con.Min OrElse ratio > con.Max Then Return False
        Next
        Return True
    End Function
End Class
