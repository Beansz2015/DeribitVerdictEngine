' tools/AutoTweaker/SettingsDiffApplier.vb
' Validates and applies a Claude-proposed settings diff.
' Hard rejection list from spec 7a + trader-profile Section 4.
' 3-key scope cap per spec 7b.
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

Public Class SettingsDiffApplier

    ' Keys and patterns that must NEVER be touched by an auto-tweaker proposal.
    ' Derived from spec 7a and trader-profile Section 4.
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

    ' Validate a proposed diff list. Returns IsValid=False with reason if any constraint violated.
    Public Shared Function Validate(items As List(Of DiffItem),
                                    currentSettingsJson As String) As DiffValidationResult
        Dim result As New DiffValidationResult()

        ' 3-key scope cap
        If items.Count > 3 Then
            result.IsValid   = False
            result.ErrorReason = String.Format(
                "Diff scope cap exceeded: {0} keys proposed, maximum is 3.", items.Count)
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

            ' Reject version key (applier manages this)
            If path = "version" Then
                result.IsValid    = False
                result.ErrorReason = "Rejected: 'version' key must not appear in diff (managed by applier)."
                Return result
            End If

            ' Reject disabling gated paths
            For Each gatePath In DisabledGatedPaths
                If path = gatePath.ToLower() Then
                    ' Check if new value is false/0
                    Dim newValStr As String = item.NewValue.ToString().Trim().ToLower()
                    If newValStr = "false" OrElse newValStr = "0" Then
                        result.IsValid    = False
                        result.ErrorReason = String.Format(
                            "Rejected: '{0}' must never be disabled.", item.Path)
                        Return result
                    End If
                End If
            Next

            ' Stale-value check: old_value must match current settings value
            If currentRoot IsNot Nothing Then
                Try
                    Dim current = NavigatePath(currentRoot, item.Path)
                    If current IsNot Nothing Then
                        Dim currentStr = current.ToJsonString()
                        Dim oldStr     = item.OldValue.GetRawText()
                        If currentStr <> oldStr Then
                            result.IsValid    = False
                            result.ErrorReason = String.Format(
                                "Stale diff: path '{0}' has current value {1} but diff expects {2}.",
                                item.Path, currentStr, oldStr)
                            Return result
                        End If
                    End If
                Catch
                    ' If we can't navigate the path, skip the stale check
                End Try
            End If
        Next

        Return result
    End Function

    ' Apply validated diff to settings.json in-place.
    ' Bumps version, sets modified_by, appends change_log entry.
    ' Returns the new version number.
    Public Shared Function Apply(items As List(Of DiffItem),
                                  settingsPath As String,
                                  reasoning As String) As Integer
        Dim json As String = File.ReadAllText(settingsPath)
        Dim root As JsonNode = JsonNode.Parse(json)

        ' Determine current version
        Dim currentVersion As Integer = 0
        Try
            currentVersion = CInt(root("version").GetValue(Of Integer)())
        Catch
        End Try
        Dim newVersion As Integer = currentVersion + 1

        ' Apply each diff item
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
                ' Re-parse the new value as a JsonNode to preserve correct type
                parent(key) = JsonNode.Parse(item.NewValue.GetRawText())
            End If
        Next

        ' Bump version and metadata
        root("version")       = JsonValue.Create(newVersion)
        root("last_modified") = JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        root("modified_by")   = JsonValue.Create("auto-tweaker-v" & newVersion)

        ' Append change_log entry
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

        ' Write back with indentation
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(settingsPath, root.ToJsonString(opts))
        Return newVersion
    End Function

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
    ' Returns an empty list on parse failure.
    Public Shared Function ParseDiff(responseText As String) As (Items As List(Of DiffItem), Reasoning As String)
        Dim items As New List(Of DiffItem)()
        Dim reasoning As String = ""
        Try
            ' Claude may wrap the JSON in markdown fences — strip them
            Dim text = responseText.Trim()
            If text.StartsWith("```") Then
                Dim firstNl = text.IndexOf(vbLf)
                If firstNl >= 0 Then text = text.Substring(firstNl + 1)
                text = text.TrimEnd("`"c, vbCrLf.ToCharArray()(0), vbCrLf.ToCharArray()(1), " "c)
            End If

            Dim doc = JsonDocument.Parse(text)
            Dim root = doc.RootElement

            If root.TryGetProperty("reasoning", Nothing) Then
                Dim rEl As JsonElement
                If root.TryGetProperty("reasoning", rEl) Then
                    reasoning = rEl.GetString()
                End If
            End If

            Dim diffArr As JsonElement
            If Not root.TryGetProperty("diff", diffArr) Then Return (items, reasoning)

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
        Catch ex As Exception
            Console.Error.WriteLine("[SettingsDiffApplier] Parse error: " & ex.Message)
        End Try
        Return (items, reasoning)
    End Function

End Class
