' tools/AutoTweaker/SettingsDiffApplier.vb
' Validates and applies a Claude-proposed settings diff.
' Hard rejection list from spec 7a + trader-profile Section 4.
' Diff-scope cap is now a parameter (settings-snapshot-history-proposal.md §3j).
' Version monotonicity: bumps settings.json version, sets modified_by, appends change_log.
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes

Public Class DiffItem
    Public Property Path As String = ""
    Public Property OldValue As JsonElement
    Public Property NewValue As JsonElement
    Public Property Justification As String = ""
End Class

Public Class DiffValidationResult
    Public Property IsValid As Boolean = True
    Public Property ErrorReason As String = ""
End Class

Public Class RevertResponse
    Public Property Action As String = ""              ' "tweak" | "revert"
    Public Property RevertTarget As String = ""
    Public Property Reasoning As String = ""
End Class

Public Class SettingsDiffApplier

    ' Keys and patterns that must NEVER be touched by an auto-tweaker proposal
    ' (or appear in a snapshot we're about to revert to).
    Private Shared ReadOnly RejectedPathFragments As String() = {
        "_fixed_pct_",              ' fixed-% targets — banned
        "bbw_none_bonus",           ' non-directional padding removed in v0.18
        "oi_prev15m",               ' dead key removed in v15 cleanup
        "oi_prev60m",               ' dead key removed in v15 cleanup
        "atr_avg20d",               ' dead key removed in v15 cleanup
        "static_vol_high",          ' dead key removed in v15 cleanup
        "static_vol_mid",           ' dead key removed in v15 cleanup
        "static_vol_low"            ' dead key removed in v15 cleanup
    }

    ' Full paths that must never be set to a disabling value.
    Private Shared ReadOnly DisabledGatedPaths As String() = {
        "mtf_gate.enabled",         ' hard veto — never disable
        "regime_weights.enabled"    ' Pass 2c gate — never disable
    }

    ' v36 Phase-2a / WS-P2 — trader-owned / off-tweaker-surface key subtrees that must
    ' NEVER appear in a proposed diff (HARD CONSTRAINT 11/12). Prefix semantics (not
    ' the substring match RejectedPathFragments uses) so they reject the whole
    ' subtree without over-matching unrelated keys. This hardens the previously
    ' prompt-only 'kelly.*' convention, covers the 'resolution_profiles.*' surface,
    ' and (WS-P2, HARD CONSTRAINT 12) the whole 'network.*' transport-plumbing block
    ' (the 3 REST keys + the WS keys + shadow_parity — no failure-rate linkage, no
    ' rational tweak proposal). [v62] The retired flat 'scoring.min_tradeable_move_pct' key
    ' is gone — its successor block 'scoring.trade_costs.' is prefix-fenced below (HARD
    ' CONSTRAINT 26); the retired key is applier-unresolvable, so C-6 rejects it naturally
    ' and it is deliberately NOT added to RejectedPathFragments (the v47-F1 snapshot-
    ' poisoning lesson). Applied only
    ' on proposed CHANGES (Validate) — NOT ValidateSnapshotContent, where a wholesale
    ' revert legitimately restores these keys unchanged.
    Private Shared ReadOnly RejectedPathPrefixes As String() = {
        "kelly.",                   ' trader-owned risk sizing
        "resolution_profiles.",     ' provisional per-resolution ROC overrides — manual re-baseline only
        "network.",                 ' transport plumbing (REST/WS/shadow_parity) — not a failure-rate lever (HARD CONSTRAINT 12)
        "exit_guard.",              ' realtime exit-guard overlay — trader-risk/display preference, display-only (HARD CONSTRAINT 13)
        "auto_run.",                ' run cadence/trigger (interval + on-close trigger_mode) — operational preference, no failure-rate linkage (HARD CONSTRAINT 14)
        "live_strip.",              ' live microstructure TAPE strip — display preference, display-only, no failure-rate linkage (HARD CONSTRAINT 15)
        "scoring.hold_",            ' CalcHoldStatus hold/exit thresholds — trader hold-discipline preference, no failure-rate linkage (HoldStatus never feeds the failure matrix); same class as kelly.* (HARD CONSTRAINT 17). Prefix-safe: sibling scoring.* tunables stay proposable
        "signal_bridge.",           ' order-app signal-file emission (verdict_signal.json) — transport plumbing, zero scoring impact, no failure-rate linkage; same class as network.* (HARD CONSTRAINT 18)
        "indicators.aggressor_velocity.default.",   ' [P4 #5] shared re-baseline tier (norm window + burst threshold) — hand-tuned per §5.2, HC11 class (HARD CONSTRAINT 19). Prefix-safe: the flat aggressor_velocity params stay proposable
        "indicators.aggressor_velocity.sessions.",  ' [P4 #5] per-session overrides — hand-tuned per §5.2, HC11 class (HARD CONSTRAINT 19)
        "indicators.ofi.momentum_",                 ' [v50 retune R1] OFI momentum modifier RETIRED (momentum_enabled=false) — the momentum_window/threshold/bonus keys are inert; leaving them proposable recreates the recorded-APPLIED-no-op class v47 F1 closed (HARD CONSTRAINT 20). Prefix-safe: book_depth, buy/sell_dominant_ratio, averaging_enabled, avg_window_sec are NOT momentum_-prefixed
        "scoring.structural_levels.sessions.",      ' [B4b placed-geometry] per-session fallback-target overrides (DG3: LONDON 2.0 / ASIA 1.25) — hand-tuned re-baseline tier, HC11 class (HARD CONSTRAINT 21). Prefix-safe: the flat structural_levels numerics stay proposable
        "indicators.absorption.default.",           ' [P4 #6] shared re-baseline tier (min_aggr_usd) — hand-tuned per book-absorption §5 target-engagement, HC11 class (HARD CONSTRAINT 23). Prefix-safe: the flat absorption params stay proposable
        "indicators.absorption.sessions.",          ' [P4 #6] per-session overrides — hand-tuned per book-absorption §5, HC11 class (HARD CONSTRAINT 23)
        "scoring.trade_costs.",                     ' [v62 fee-aware min-move floor] Execution-cost model + the trader's min NET move. The fee/style keys are VENUE FACTS (edited when Deribit changes them) and min_net_move_pct is a kelly.*-class risk preference — nothing under the block is ever a failure-rate lever (HARD CONSTRAINT 26). Prefix-safe: sibling scoring.* tunables stay proposable
        "alerts."                                    ' [#7 + #8 v59] liq-cascade alarm + level-approach alerts — display/alert-only plumbing, zero scoring impact, no failure-rate linkage; same class as exit_guard.* / live_strip.* / signal_bridge.* (HARD CONSTRAINT 25). Prefix-safe: no other top-level "alerts." keys exist
    }

    ' Validate a proposed diff list.
    ' maxKeysPerProposal — diff-scope cap; default 3, configurable from tweaker_config.json.
    Public Shared Function Validate(items As List(Of DiffItem),
                                    currentSettingsJson As String,
                                    maxKeysPerProposal As Integer) As DiffValidationResult
        Dim result As New DiffValidationResult()

        ' Diff scope cap (configurable per spec §3j)
        If items.Count > maxKeysPerProposal Then
            result.IsValid    = False
            result.ErrorReason = String.Format(
                "Diff scope cap exceeded: {0} keys proposed, maximum is {1}.",
                items.Count, maxKeysPerProposal)
            Return result
        End If

        ' Parse current settings for stale-value check
        Dim currentRoot As JsonNode = Nothing
        Try
            currentRoot = JsonNode.Parse(currentSettingsJson)
        Catch
        End Try

        For Each item In items
            Dim path = item.Path.Trim().ToLower()

            ' Reject banned path fragments
            For Each frag In RejectedPathFragments
                If path.Contains(frag.ToLower()) Then
                    result.IsValid    = False
                    result.ErrorReason = String.Format(
                        "Rejected: path '{0}' contains banned fragment '{1}'.", item.Path, frag)
                    Return result
                End If
            Next

            ' Reject trader-owned / off-tweaker-surface subtrees (HARD CONSTRAINT 11/12).
            For Each pre In RejectedPathPrefixes
                If path.StartsWith(pre) Then
                    result.IsValid    = False
                    result.ErrorReason = String.Format(
                        "Rejected: '{0}' is a trader-owned / off-tweaker-surface key (HARD CONSTRAINT 11/12).", item.Path)
                    Return result
                End If
            Next
            ' [v62] The 'scoring.min_tradeable_move_pct' exact-match reject that lived here is
            ' GONE with the key it guarded: the floor is now composed from the prefix-fenced
            ' scoring.trade_costs. block (HARD CONSTRAINT 26, above). A diff naming the retired
            ' key fails the C-6 resolve check instead — deliberately NOT re-added as a banned
            ' fragment, which would poison every snapshot that still carries it (v47-F1).
            ' [P4 #4] OFI time-averaging FEATURE FLAG — off the tweaker surface (a structural
            ' on/off toggle, not a failure-rate threshold). Exact-match, NOT a prefix: the sibling
            ' OFI keys (avg_window_sec, buy/sell_dominant_ratio, book_depth) STAY tunable.
            If path = "indicators.ofi.averaging_enabled" Then
                result.IsValid    = False
                result.ErrorReason = "Rejected: 'indicators.OFI.averaging_enabled' is the OFI time-averaging feature flag, not a threshold (off tweaker surface — HARD CONSTRAINT 16)."
                Return result
            End If
            ' [P4 #5] Aggressor-velocity FEATURE SWITCHES — off the tweaker surface (structural
            ' on/off toggles, not thresholds; scoring_enabled is the data-gated ⚠ scoring gate).
            ' Exact-match, NOT a prefix: the flat siblings (fast_window_sec, direction_lean_floor,
            ' gross_floor_usd_per_sec, upgrade_bonus, contra_penalty) STAY tunable.
            If path = "indicators.aggressor_velocity.enabled" OrElse
               path = "indicators.aggressor_velocity.scoring_enabled" Then
                result.IsValid    = False
                result.ErrorReason = String.Format(
                    "Rejected: '{0}' is an aggressor-velocity feature switch, not a threshold (off tweaker surface — HARD CONSTRAINT 19).", item.Path)
                Return result
            End If
            ' [P4 #6 / v61] Absorption FEATURE SWITCHES — off the tweaker surface (structural on/off
            ' toggles, not thresholds; scoring_enabled is the TWICE-evidence-gated ⚠ activation,
            ' book-absorption proposal §5). Exact-match, NOT a prefix: the flat siblings
            ' (proximity_atr_frac, band_atr_frac, window_sec, break_tol_atr_frac, absorb_ratio,
            ' depletion_floor_usd, max_pull_frac, penalty) STAY tunable — [v61] the three
            ' *_atr_frac keys replaced the retired tick keys per docs/absorption-geometry-
            ' rescale-proposal.md §1 (retired tick keys become applier-unresolvable ⇒ C-6 rejects
            ' them naturally; NOT added to RejectedPathFragments — v47-F1 lesson).
            If path = "indicators.absorption.enabled" OrElse
               path = "indicators.absorption.scoring_enabled" Then
                result.IsValid    = False
                result.ErrorReason = String.Format(
                    "Rejected: '{0}' is a book-absorption feature switch, not a threshold (off tweaker surface — HARD CONSTRAINT 23).", item.Path)
                Return result
            End If
            ' [P4 #5 wire-in / S5 rider] session_volume FEATURE SWITCH — off the tweaker surface.
            ' The volume-multiplier / execution-resolution machinery is trader-owned session
            ' structure, not a failure-rate threshold, and disabling it silently would drop the
            ' session-adjusted volume norms + reslice execution resolution — never an optimisation.
            ' Exact-match, NOT a prefix: the array-nested session_volume.sessions[] keys are already
            ' rejected as UNRESOLVED (NavigatePath can't traverse arrays), so this only closes the
            ' one flat switch. Mirrors OFI.averaging_enabled / structural_levels.enabled (HC16/21 class).
            If path = "session_volume.enabled" Then
                result.IsValid    = False
                result.ErrorReason = "Rejected: 'session_volume.enabled' is a session-structure feature switch, not a threshold (off tweaker surface — HARD CONSTRAINT 22)."
                Return result
            End If
            ' [B4b placed-geometry] Structural-levels HAND-TOGGLES — off the tweaker surface
            ' (enabled is the geometry rollback switch; stop_too_loose_mode is the D3 decision
            ' record, not a threshold). Exact-match, NOT a prefix: the flat siblings
            ' (target_max_atr_mult, stop_max_atr_mult, stop_min_floor_ticks) STAY tunable.
            If path = "scoring.structural_levels.enabled" OrElse
               path = "scoring.structural_levels.stop_too_loose_mode" Then
                result.IsValid    = False
                result.ErrorReason = String.Format(
                    "Rejected: '{0}' is a structural-levels hand-toggle, not a threshold (off tweaker surface — HARD CONSTRAINT 21).", item.Path)
                Return result
            End If
            ' [geometry-arbitration-modes v56] Geometry ARBITRATION MODES + SIGNED BUFFERS —
            ' off the tweaker surface (hand-ruled geometry, HC11 class). Exact-match, NOT a
            ' prefix: the flat siblings (target_max_atr_mult, stop_max_atr_mult,
            ' stop_min_floor_ticks) STAY tunable, HC21 unchanged. Modes are a shape choice
            ' (ladder vs nearest / tightest vs widest); enabling them live is a later ⚠
            ' D-table gated on what-if replay evidence, and the stop-widest side is
            ' ADDITIONALLY hard-gated on consumer sizing-by-stop-distance (L3). Buffers
            ' are trader-owned pullback/protection shape, not failure-rate thresholds.
            If path = "scoring.structural_levels.target_arbitration_mode" OrElse
               path = "scoring.structural_levels.stop_arbitration_mode" OrElse
               path = "scoring.structural_levels.target_buffer_pct" OrElse
               path = "scoring.structural_levels.stop_buffer_pct" Then
                result.IsValid    = False
                result.ErrorReason = String.Format(
                    "Rejected: '{0}' is a hand-ruled geometry knob (arbitration mode / signed buffer), not a threshold (off tweaker surface — HARD CONSTRAINT 24).", item.Path)
                Return result
            End If

            ' Reject version key (applier manages this)
            If path = "version" Then
                result.IsValid    = False
                result.ErrorReason = "Rejected: 'version' key must not appear in diff (managed by applier)."
                Return result
            End If

            ' Reject disabling gated paths
            For Each gatePath In DisabledGatedPaths
                If path = gatePath.ToLower() Then
                    Dim newValStr As String = item.NewValue.ToString().Trim().ToLower()
                    If newValStr = "false" OrElse newValStr = "0" Then
                        result.IsValid    = False
                        result.ErrorReason = String.Format(
                            "Rejected: '{0}' must never be disabled.", item.Path)
                        Return result
                    End If
                End If
            Next

            ' Path-resolution + stale-value check (both need parsed current settings).
            If currentRoot IsNot Nothing Then
                Dim current As JsonNode = Nothing
                Try
                    current = NavigatePath(currentRoot, item.Path)
                Catch
                End Try
                ' Reject any path that does not resolve in the current settings tree.
                ' Previously an unresolved path skipped the stale check and passed;
                ' Apply then CREATED the unknown key, so a typo'd path from the model
                ' (e.g. "indicators.RSI.overbough") validated, applied, bumped the
                ' version, and was recorded APPLIED as a silent no-op the engine
                ' never reads — corrupting failure-rate evaluation of the "tweak".
                If current Is Nothing Then
                    result.IsValid    = False
                    result.ErrorReason = String.Format(
                        "Rejected: path '{0}' does not resolve in current settings (no key creation).", item.Path)
                    Return result
                End If
                ' Stale-value check: old_value must match current settings value.
                Try
                    Dim currentStr = current.ToJsonString()
                    Dim oldStr     = item.OldValue.GetRawText()
                    If currentStr <> oldStr Then
                        result.IsValid    = False
                        result.ErrorReason = String.Format(
                            "Stale diff: path '{0}' has current value {1} but diff expects {2}.",
                            item.Path, currentStr, oldStr)
                        Return result
                    End If
                Catch
                End Try
            End If
        Next

        Return result
    End Function

    ' Backward-compatible overload — defaults to the legacy cap of 3.
    Public Shared Function Validate(items As List(Of DiffItem),
                                    currentSettingsJson As String) As DiffValidationResult
        Return Validate(items, currentSettingsJson, 3)
    End Function

    ' Apply validated diff to settings.json in-place.
    ' Bumps version, sets modified_by, appends change_log entry.
    ' Returns the new version number.
    Public Shared Function Apply(items As List(Of DiffItem),
                                  settingsPath As String,
                                  reasoning As String) As Integer
        Dim json As String = File.ReadAllText(settingsPath)
        Dim root As JsonNode = JsonNode.Parse(json)

        Dim currentVersion As Integer = 0
        Try
            currentVersion = CInt(root("version").GetValue(Of Integer)())
        Catch
        End Try
        Dim newVersion As Integer = currentVersion + 1

        For Each item In items
            Dim parts() As String = item.Path.Split("."c)
            Dim parent = TryCast(root, JsonObject)
            For i As Integer = 0 To parts.Length - 2
                Dim child = parent(parts(i))
                parent = TryCast(child, JsonObject)
                If parent Is Nothing Then Exit For
            Next
            If parent IsNot Nothing Then
                Dim key = parts(parts.Length - 1)
                ' Never create keys — only overwrite an existing leaf. Unknown paths
                ' are rejected by Validate; this is defence in depth so a typo'd path
                ' can't be silently materialised into settings.json here.
                If parent.ContainsKey(key) Then
                    parent(key) = JsonNode.Parse(item.NewValue.GetRawText())
                Else
                    Console.Error.WriteLine(String.Format(
                        "[SettingsDiffApplier] Skipped unknown path '{0}' in Apply — key not created.", item.Path))
                End If
            End If
        Next

        root("version")       = JsonValue.Create(newVersion)
        root("last_modified") = JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        root("modified_by")   = JsonValue.Create("auto-tweaker-v" & newVersion)

        Dim changeLogNode = TryCast(root("change_log"), JsonArray)
        If changeLogNode Is Nothing Then
            changeLogNode = New JsonArray()
            root("change_log") = changeLogNode
        End If

        Dim summary As New System.Text.StringBuilder()
        summary.Append(String.Format("v{0} [auto-tweaker] ", newVersion))
        summary.Append(If(reasoning.Length > 200,
                          reasoning.Substring(0, 200) & "...",
                          reasoning))
        summary.Append(" | Changes: ")
        For Each item In items
            summary.Append(String.Format("{0}: {1} -> {2}; ",
                                         item.Path,
                                         item.OldValue.GetRawText(),
                                         item.NewValue.GetRawText()))
        Next
        changeLogNode.Add(summary.ToString())

        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        AtomicWriteAllText(settingsPath, root.ToJsonString(opts))
        Return newVersion
    End Function

    ' Atomic write: persist to a sibling .tmp then rename so a mid-write crash
    ' can't truncate settings.json. NTFS rename is atomic at the filesystem level.
    ' Mirrors TweakerState.Save — duplicated here rather than coupling the
    ' AutoTweaker project to the engine assembly (host-agnostic constraint).
    Private Shared Sub AtomicWriteAllText(path As String, content As String)
        Dim tmpPath As String = path & ".tmp"
        Try
            File.WriteAllText(tmpPath, content)
            If File.Exists(path) Then
                File.Replace(tmpPath, path, Nothing)
            Else
                File.Move(tmpPath, path)
            End If
        Catch
            Try : File.Delete(tmpPath) : Catch : End Try
            Throw
        End Try
    End Sub

    ' Apply a wholesale revert from a snapshot file. Per spec §3i:
    '   - Snapshot content runs through the same rejected-pattern / disabled-gate
    '     validation as a normal diff (we still gate on snapshot integrity).
    '   - The diff-scope cap is EXEMPT — a revert is many keys by definition;
    '     the snapshot's provenance (proven-successful streak) is the gate.
    '   - Bumps settings.json version, sets modified_by="auto-tweaker-revert",
    '     appends a change_log entry citing the snapshot filename + reasoning.
    Public Shared Function ApplyRevert(snapshotPath As String,
                                        settingsPath As String,
                                        reasoning As String) As Integer
        If Not File.Exists(snapshotPath) Then
            Throw New FileNotFoundException("Snapshot file not found: " & snapshotPath)
        End If

        Dim snapshotJson As String = File.ReadAllText(snapshotPath)
        Dim snapshotRoot As JsonNode = JsonNode.Parse(snapshotJson)

        ' Snapshot-content validation — reject banned fragments and disabled gates
        ' even when reverting. Snapshot integrity is not absolute trust.
        Dim integrity = ValidateSnapshotContent(snapshotRoot)
        If Not integrity.IsValid Then
            Throw New InvalidOperationException(
                "Snapshot rejected by integrity check: " & integrity.ErrorReason)
        End If

        ' Read current version for bump
        Dim currentJson As String = File.ReadAllText(settingsPath)
        Dim currentRoot As JsonNode = JsonNode.Parse(currentJson)
        Dim currentVersion As Integer = 0
        Try
            currentVersion = CInt(currentRoot("version").GetValue(Of Integer)())
        Catch
        End Try
        Dim newVersion As Integer = currentVersion + 1

        ' Take the snapshot wholesale, then patch metadata + change_log
        snapshotRoot("version")       = JsonValue.Create(newVersion)
        snapshotRoot("last_modified") = JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        snapshotRoot("modified_by")   = JsonValue.Create("auto-tweaker-revert")

        ' Carry the historical change_log forward but append a new entry.
        Dim changeLogNode = TryCast(snapshotRoot("change_log"), JsonArray)
        If changeLogNode Is Nothing Then
            ' If the snapshot lacked a change_log array, preserve the live one.
            changeLogNode = TryCast(currentRoot("change_log"), JsonArray)
            If changeLogNode Is Nothing Then
                changeLogNode = New JsonArray()
            Else
                changeLogNode = JsonNode.Parse(changeLogNode.ToJsonString())
            End If
            snapshotRoot("change_log") = changeLogNode
        End If

        Dim summary As String = String.Format(
            "v{0} reverted to snapshot {1} | Reasoning: {2}",
            newVersion,
            Path.GetFileName(snapshotPath),
            If(reasoning.Length > 200, reasoning.Substring(0, 200) & "...", reasoning))
        changeLogNode.Add(summary)

        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        AtomicWriteAllText(settingsPath, snapshotRoot.ToJsonString(opts))
        Return newVersion
    End Function

    ' Walk the snapshot content and reject banned fragments / disabled gates.
    Private Shared Function ValidateSnapshotContent(root As JsonNode) As DiffValidationResult
        Dim result As New DiffValidationResult()
        Dim paths As New List(Of (Path As String, Value As JsonNode))()
        FlattenPaths(root, "", paths)

        For Each entry In paths
            Dim path As String = entry.Path.ToLower()
            For Each frag In RejectedPathFragments
                If path.Contains(frag.ToLower()) Then
                    result.IsValid    = False
                    result.ErrorReason = String.Format(
                        "Snapshot contains banned fragment '{0}' at path '{1}'.", frag, entry.Path)
                    Return result
                End If
            Next
            For Each gatePath In DisabledGatedPaths
                If path = gatePath.ToLower() AndAlso entry.Value IsNot Nothing Then
                    Dim s As String = entry.Value.ToJsonString().Trim().ToLower()
                    If s = "false" OrElse s = "0" Then
                        result.IsValid    = False
                        result.ErrorReason = String.Format(
                            "Snapshot disables gated path '{0}'.", entry.Path)
                        Return result
                    End If
                End If
            Next
        Next
        Return result
    End Function

    Private Shared Sub FlattenPaths(node As JsonNode, prefix As String,
                                     acc As List(Of (Path As String, Value As JsonNode)))
        Dim obj = TryCast(node, JsonObject)
        If obj IsNot Nothing Then
            For Each kv In obj
                Dim newPath As String = If(prefix.Length = 0, kv.Key, prefix & "." & kv.Key)
                acc.Add((newPath, kv.Value))
                FlattenPaths(kv.Value, newPath, acc)
            Next
            Return
        End If
        Dim arr = TryCast(node, JsonArray)
        If arr IsNot Nothing Then
            For i As Integer = 0 To arr.Count - 1
                FlattenPaths(arr(i), prefix & "[" & i & "]", acc)
            Next
        End If
    End Sub

    ' Navigate a dot-separated path in a JsonNode tree. Returns Nothing if path not found.
    Private Shared Function NavigatePath(root As JsonNode, path As String) As JsonNode
        Dim parts() As String = path.Split("."c)
        Dim cur As JsonNode = root
        For Each part In parts
            If cur Is Nothing Then Return Nothing
            Dim obj = TryCast(cur, JsonObject)
            If obj Is Nothing Then Return Nothing
            cur = obj(part)
        Next
        Return cur
    End Function

    ' Parse a Claude JSON response string into a list of DiffItems.
    ' Also returns the parsed "action" / "revert_target" so callers can branch.
    Public Shared Function ParseDiff(responseText As String) As (Items As List(Of DiffItem),
                                                                  Reasoning As String,
                                                                  Action As String,
                                                                  RevertTarget As String)
        Dim items As New List(Of DiffItem)()
        Dim reasoning As String = ""
        Dim action As String = "tweak"
        Dim revertTarget As String = ""
        Try
            Dim text = responseText.Trim()
            If text.StartsWith("```") Then
                Dim firstNl = text.IndexOf(vbLf)
                If firstNl >= 0 Then text = text.Substring(firstNl + 1)
                text = text.TrimEnd("`"c, vbCrLf.ToCharArray()(0), vbCrLf.ToCharArray()(1), " "c)
            End If

            Dim doc = JsonDocument.Parse(text)
            Dim root = doc.RootElement

            Dim rEl As JsonElement
            If root.TryGetProperty("reasoning", rEl) Then reasoning = rEl.GetString()

            Dim aEl As JsonElement
            If root.TryGetProperty("action", aEl) Then action = aEl.GetString().Trim().ToLower()

            Dim tEl As JsonElement
            If root.TryGetProperty("revert_target", tEl) Then revertTarget = tEl.GetString()

            Dim diffArr As JsonElement
            If root.TryGetProperty("diff", diffArr) Then
                For Each entry In diffArr.EnumerateArray()
                    Dim item As New DiffItem()
                    Dim pathEl As JsonElement
                    If entry.TryGetProperty("path", pathEl) Then item.Path = pathEl.GetString()

                    Dim oldEl As JsonElement
                    If entry.TryGetProperty("old_value", oldEl) Then item.OldValue = oldEl

                    Dim newEl As JsonElement
                    If entry.TryGetProperty("new_value", newEl) Then item.NewValue = newEl

                    Dim justEl As JsonElement
                    If entry.TryGetProperty("justification", justEl) Then item.Justification = justEl.GetString()

                    If Not String.IsNullOrEmpty(item.Path) Then items.Add(item)
                Next
            End If
        Catch ex As Exception
            Console.Error.WriteLine("[SettingsDiffApplier] Parse error: " & ex.Message)
        End Try
        Return (items, reasoning, action, revertTarget)
    End Function

End Class
