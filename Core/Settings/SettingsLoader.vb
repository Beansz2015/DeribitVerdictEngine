' Core/Settings/SettingsLoader.vb
' Loads EngineSettings from settings.json, provides hot-reload via FileSystemWatcher.
' Thread-safe singleton access via SettingsLoader.Current.
'
' Usage:
'   SettingsLoader.Initialise(path)   -- call once at app startup
'   SettingsLoader.Current            -- read settings anywhere, always up-to-date
'
' [settings.local.json overlay — docs/settings-local-overlay-proposal.md, D1-D6 ticked
'  2026-08-01; corrections of record in docs/overlay-whitelist-reaudit-2026-07-31.md and
'  §1 of docs/settings-local-overlay-implementer-brief.md]
'
'   <exe>\settings.json         tracked, shared, version-bearing  -- the BASE
'   <exe>\settings.local.json   gitignored, per-box, no version   -- the OVERLAY
'
' Two boxes run the same binary from the same tracked settings.json: AWS is the sole
' raw-tape capturer, the local box explicitly is not. They need different values for
' trade_store.enabled and there was no way to express that -- settings.json is
' CopyToOutputDirectory=PreserveNewest, so every build with a newer tracked file
' silently restored the shared value.
'
' Three invariants this file has to hold:
'   1. ABSENT OVERLAY => byte-identical to the pre-overlay engine. The merge branch is
'      skipped entirely; load and Save both take the original code path.
'   2. SAVE OPERATES ON THE BASE, NEVER THE MERGE (§1.2). Inverting this promotes a
'      local-only override into the shared tracked file and, from there, onto AWS on
'      the next xcopy deploy. That is the catastrophic direction. Fixture A50c.
'   3. THE WHITELIST IS AN ALLOW-LIST BY CONSTRUCTION (re-audit F3). Nothing is
'      overridable unless it is named in AdmittedBlocks/AdmittedKeys below. A
'      reject-list would have shipped an overlay able to flip mtf_gate -- the hard
'      veto -- per box with no version change, because §2.2 of the spec enumerated
'      16 of settings.json's 17 blocks and mtf_gate was the missing one.

Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Threading

Public Class SettingsLoader

    ''' <summary>Overlay file name, resolved in the same directory as settings.json (D2).</summary>
    Public Const LocalOverlayFileName As String = "settings.local.json"

    Private Shared _current As EngineSettings = New EngineSettings()
    Private Shared _lock As New ReaderWriterLockSlim()
    Private Shared _watcher As FileSystemWatcher
    Private Shared _localWatcher As FileSystemWatcher
    Private Shared _settingsPath As String = ""
    Private Shared _localPath As String = ""
    Private Shared _lastLoadError As String = ""

    ' -- overlay state -------------------------------------------------------
    ' All four move together and are reset by ClearOverlayState(); _overlayActive is
    ' the single predicate every overlay-aware branch tests.
    Private Shared _overlayActive As Boolean = False
    ''' <summary>The parsed BASE document, kept in memory so Save() can write it rather than the merge.</summary>
    Private Shared _baseNode As JsonObject = Nothing
    ''' <summary>The overlay AFTER whitelist filtering — only admitted keys survive here.</summary>
    Private Shared _effectiveOverlay As JsonObject = Nothing
    ''' <summary>Serialisation of the currently-published EngineSettings — the canonical
    ''' form Save() compares a caller's object against to tell "the caller changed this
    ''' key" from "the overlay value rode along". Both sides are POCO serialisations, so
    ''' the comparison is apples-to-apples (raw JSON would differ on 6 vs 6.0).</summary>
    Private Shared _currentNode As JsonObject = Nothing
    Private Shared _overlayApplied As New List(Of String)
    Private Shared _overlayRejected As New List(Of String)

    ' -- whitelist (§2.2 as corrected by the re-audit) -----------------------
    '
    ' ADMITTED WHOLE — six blocks. Each is off the scoring path AND (clause (ii) of the
    ' J-D rule, ratified 2026-07-31) cannot change what an evidence instrument records
    ' that a queued decision or standing watch depends on.
    Private Shared ReadOnly AdmittedBlocks As String() = {
        "trade_store",          ' the block this feature exists for (F6 ruling: capture ON for AWS, OFF locally)
        "signal_bridge",        ' clean, verified: ws_health.log is written BEFORE the enabled early-return
        "live_strip",           ' display-only
        "exit_guard",           ' the only genuinely inert block — no file write, no network, no logging
        "performance_display",  ' see the note below — admitted WITH a recorded reason, not "clean"
        "analysis_logging"      ' post-render output dump only
    }
    '
    ' performance_display. is admitted but is NOT clean (re-audit F2): it gates the eval
    ' cache, the OHLC cache and the gap-fill outright (LivePerformanceTracker.vb:188-191,
    ' :494, :261-266), so diverging it diverges the live per-box outcome yardstick.
    ' IT IS ADMITTED BECAUSE NO QUEUED DECISION READS THE EVAL CACHE -- the offline stack
    ' re-fetches its own OHLC and the tweaker reads analysis_log.csv. REVISIT THE MOMENT
    ' ANYTHING GATES ON IT; Kelly CAL is the near candidate, since it wants empirical
    ' per-tier win rates.
    '
    ' network. — admitted PER KEY. These change whether a run skips, never what a
    ' completed run computes; a skipped run emits no row, so the rows that exist stay
    ' comparable. ws_url is a different endpoint for the same venue and the same data.
    Private Shared ReadOnly AdmittedKeys As String() = {
        "network.request_timeout_seconds",
        "network.retry_count",
        "network.retry_backoff_ms",
        "network.ws_url"
    }
    '
    ' EVERYTHING ELSE IS REJECTED. The enforcement is the allow-list above, not this
    ' list -- these entries exist so the log says WHY, and so the two the re-audit
    ' singled out are named in code rather than left to a catch-all:
    '   mtf_gate.*  -- the HARD VETO. BLOCK forces NO TRADE regardless of score.
    '   alerts.*    -- gates liq_events.log, the SOLE gate instrument on A4, with both
    '                  boxes' sidecars pooled. cascade_min_trades/cascade_window_sec are
    '                  worse than binary: they change the evidence CONTENT while both
    '                  boxes stamp the same settings version.
    '   network.transport (+ ws_fallback_to_rest / ws_stale_after_sec / ws_heartbeat_sec /
    '                  ws_cooldown_sec) -- three sites in MainForm_Analysis gate a signal on
    '                  `src Is _wsSource`, so transport is a different computation, and the
    '                  health keys decide how often a WS box silently scores like a REST box.
    '   network.shadow_parity -- rejected on SIDE-EFFECTS: it starts a WS feed, which since
    '                  v64 also runs trade-store capture.
    '   auto_run.*  -- cadence moves scoring (the whole v53 funding rewrite is the proof),
    '                  and trigger_mode is not yet a CSV column, so divergence is invisible.
    Private Shared ReadOnly RejectNotes As String() = {
        "mtf_gate|the hard veto — BLOCK forces NO TRADE regardless of score",
        "alerts|gates liq_events.log, the sole A4 gate instrument (both boxes pooled)",
        "network.transport|selects the data source — three run-path signals gate on it",
        "network.ws_fallback_to_rest|decides how often a WS box silently scores like a REST box",
        "network.ws_stale_after_sec|decides how often a WS box silently scores like a REST box",
        "network.ws_heartbeat_sec|modulates connection health, which selects the data source",
        "network.ws_cooldown_sec|modulates connection health, which selects the data source",
        "network.shadow_parity|starts a WS feed, which since v64 also runs trade-store capture",
        "auto_run|cadence moves scoring and trigger_mode is not yet a CSV column",
        "scoring|scoring path",
        "indicators|scoring path",
        "session_volume|scoring path",
        "resolution_profiles|scoring path",
        "regime_gates|scoring path",
        "regime_weights|scoring path",
        "kelly|scoring path",
        "version|the base owns document metadata",
        "last_modified|the base owns document metadata",
        "modified_by|the base owns document metadata",
        "change_log|the base owns document metadata"
    }

    ''' <summary>
    ''' Non-empty when the most recent load from disk failed to parse — the engine
    ''' is then running on the in-memory POCO defaults rather than calibrated values.
    ''' Cleared on a successful load. Surfaced by MainForm in the status bar at
    ''' startup; also logged to the console for the future headless CLI host.
    ''' </summary>
    Public Shared ReadOnly Property LastLoadError As String
        Get
            Return _lastLoadError
        End Get
    End Property

    ''' <summary>
    ''' True when settings.local.json exists, parsed, and actually applied at least one
    ''' admitted key. Drives the title-bar "+local" marker (D4). An overlay whose every
    ''' key was rejected reads False on purpose — the marker means "the overlay is doing
    ''' something", so the trader's daily glance can rely on its ABSENCE too.
    ''' </summary>
    Public Shared ReadOnly Property OverlayActive As Boolean
        Get
            Return _overlayActive
        End Get
    End Property

    ''' <summary>Dotted paths the overlay actually applied, in file order. Diagnostic.</summary>
    Public Shared ReadOnly Property OverlayAppliedKeys As IReadOnlyList(Of String)
        Get
            Return _overlayApplied
        End Get
    End Property

    ''' <summary>Dotted paths the whitelist refused, in file order. Diagnostic.</summary>
    Public Shared ReadOnly Property OverlayRejectedKeys As IReadOnlyList(Of String)
        Get
            Return _overlayRejected
        End Get
    End Property

    ''' <summary>
    ''' Returns the currently active settings. Always thread-safe.
    ''' </summary>
    Public Shared ReadOnly Property Current As EngineSettings
        Get
            _lock.EnterReadLock()
            Try
                Return _current
            Finally
                _lock.ExitReadLock()
            End Try
        End Get
    End Property

    ''' <summary>
    ''' Load settings from the given path and start watching for file changes.
    ''' Call once at application startup (e.g., MainForm_Load).
    ''' If the file does not exist, a default settings.json is written to that path.
    ''' Also resolves and watches the sibling settings.local.json overlay.
    ''' </summary>
    Public Shared Sub Initialise(settingsPath As String)
        _settingsPath = settingsPath

        Dim dir As String = System.IO.Path.GetDirectoryName(settingsPath)
        _localPath = If(String.IsNullOrEmpty(dir), LocalOverlayFileName,
                        System.IO.Path.Combine(dir, LocalOverlayFileName))

        If Not File.Exists(settingsPath) Then
            WriteDefaults(settingsPath)
        End If

        LoadFromDisk()
        StartWatcher(settingsPath)
    End Sub

    ''' <summary>
    ''' Save the supplied settings object back to settings.json.
    ''' Updates last_modified always. When bumpVersion is True (scoring/feature saves)
    ''' it also increments version and appends a change_log entry. Operational/UI-only
    ''' saves (auto_run interval, perf metric_mode, output-dump settings) pass
    ''' bumpVersion:=False so they don't churn the feature version or the change_log
    ''' (§10a — D4 closed start/stop churn; this closes interval-change version bumps).
    '''
    ''' With an overlay active the write target is the BASE document, never the merge
    ''' (overlay spec §1.2). Every overlay-owned key whose value the caller did not
    ''' change is reverted to what the tracked file said before the write, so a local
    ''' override can never be promoted into the shared file. Where the caller DID change
    ''' an overlaid key (a UI click on live_strip.enabled, say), the click goes to the
    ''' base and the overlay keeps winning locally — the deliberate one-way mirror the
    ''' re-audit's F4 documents, pinned by A50j.
    ''' </summary>
    Public Shared Sub Save(settings As EngineSettings, changeNote As String, Optional bumpVersion As Boolean = True)
        If String.IsNullOrEmpty(_settingsPath) Then Return
        settings.LastModified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        If bumpVersion Then
            settings.Version += 1
            If Not String.IsNullOrEmpty(changeNote) Then
                settings.ChangeLog.Add(String.Format("v{0} [{1}]: {2}", settings.Version, settings.LastModified, changeNote))
            End If
        End If
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        Dim json As String = JsonSerializer.Serialize(settings, opts)

        If Not _overlayActive OrElse _baseNode Is Nothing OrElse _effectiveOverlay Is Nothing Then
            ' No overlay — the pre-overlay path, unchanged.
            _lock.EnterWriteLock()
            Try
                AtomicWriteAllText(_settingsPath, json)
                _current = settings
            Finally
                _lock.ExitWriteLock()
            End Try
            Return
        End If

        Dim candidate As JsonObject = TryCast(JsonNode.Parse(json), JsonObject)
        If candidate Is Nothing Then
            ' Should not happen (we just serialised a POCO). Fail closed: write nothing
            ' rather than risk writing a merged tree into the shared file.
            Console.WriteLine("[SettingsLoader] Save aborted — could not re-parse the serialised settings")
            Return
        End If

        For Each p As String In _overlayApplied
            Dim tgt As JsonNode = Nothing
            If Not TryGetPath(candidate, p, tgt) Then Continue For

            ' Fail-safe default: unless we can PROVE the caller changed this key, revert
            ' it to the base value. Proving the negative is what would leak the overlay.
            Dim callerChanged As Boolean = False
            Dim canon As JsonNode = Nothing
            If _currentNode IsNot Nothing AndAlso TryGetPath(_currentNode, p, canon) Then
                callerChanged = Not NodeEquals(tgt, canon)
            End If
            If callerChanged Then Continue For

            Dim baseVal As JsonNode = Nothing
            If TryGetPath(_baseNode, p, baseVal) Then
                SetPath(candidate, p, CloneNode(baseVal))
            Else
                RemovePath(candidate, p)
            End If
        Next

        Dim outJson As String = candidate.ToJsonString(opts)

        ' Re-merge the overlay on top so Current keeps reflecting the effective config.
        Dim merged As JsonObject = TryCast(CloneNode(candidate), JsonObject)
        MergeInto(merged, _effectiveOverlay)
        Dim reMerged As EngineSettings = Nothing
        Try
            reMerged = JsonSerializer.Deserialize(Of EngineSettings)(
                merged.ToJsonString(), New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
        Catch ex As Exception
            Console.WriteLine("[SettingsLoader] Save re-merge failed: " & ex.Message)
        End Try

        _lock.EnterWriteLock()
        Try
            AtomicWriteAllText(_settingsPath, outJson)
            _baseNode = candidate
            _current = If(reMerged, settings)
            _currentNode = SerialiseToObject(_current)
        Finally
            _lock.ExitWriteLock()
        End Try
    End Sub

    ' -- Private helpers -----------------------------------------------------

    ''' <summary>
    ''' Write text to a file atomically: persist to a sibling .tmp then rename.
    ''' NTFS rename is atomic — a mid-write crash leaves either the original file
    ''' intact (rename never happened) or the new file in place (rename completed),
    ''' never a truncated settings.json. Mirrors TweakerState.Save.
    ''' </summary>
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

    Private Shared Sub LoadFromDisk()
        If String.IsNullOrEmpty(_settingsPath) OrElse Not File.Exists(_settingsPath) Then Return
        Try
            Dim json As String = File.ReadAllText(_settingsPath)
            Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

            Dim overlayText As String = ReadOverlayText()
            If overlayText Is Nothing Then
                LoadBaseOnly(json, opts)
                Return
            End If

            Dim baseObj As JsonObject = TryCast(JsonNode.Parse(json), JsonObject)
            Dim ovObj As JsonObject = Nothing
            Try
                ovObj = TryCast(JsonNode.Parse(overlayText), JsonObject)
            Catch exOv As Exception
                ' A50e — malformed overlay is logged and ignored; the app still starts.
                Console.WriteLine("[SettingsLoader] " & LocalOverlayFileName &
                                  ": parse error — IGNORED (" & exOv.Message & ")")
            End Try

            If baseObj Is Nothing OrElse ovObj Is Nothing Then
                LoadBaseOnly(json, opts)
                Return
            End If

            Dim rejected As New List(Of String)
            Dim effective As JsonObject = FilterOverlay(ovObj, rejected)
            For Each rk As String In rejected
                Console.WriteLine("[SettingsLoader] " & LocalOverlayFileName & ": '" & rk &
                                  "' is not overridable — IGNORED" & RejectNote(rk))
            Next

            Dim applied As New List(Of String)
            CollectLeafPaths(effective, "", applied)
            If applied.Count = 0 Then
                LoadBaseOnly(json, opts)
                _overlayRejected = rejected
                Console.WriteLine("[SettingsLoader] " & LocalOverlayFileName &
                                  " present but overrode nothing — base settings in force")
                Return
            End If

            Dim merged As JsonObject = TryCast(CloneNode(baseObj), JsonObject)
            MergeInto(merged, effective)
            Dim loaded = JsonSerializer.Deserialize(Of EngineSettings)(merged.ToJsonString(), opts)
            If loaded Is Nothing Then
                LoadBaseOnly(json, opts)
                Return
            End If

            ' §3 — one startup line naming every key the overlay actually changed. This is
            ' the line a future seat greps when a box behaves oddly.
            Dim shown As New List(Of String)
            For Each p As String In applied
                Dim beforeNode As JsonNode = Nothing
                Dim beforeTxt As String = If(TryGetPath(baseObj, p, beforeNode), NodeText(beforeNode), "(absent)")
                Dim afterNode As JsonNode = Nothing
                TryGetPath(merged, p, afterNode)
                shown.Add(p & ": " & beforeTxt & " -> " & NodeText(afterNode))
            Next
            Console.WriteLine("[SettingsLoader] " & LocalOverlayFileName & " ACTIVE — " &
                              applied.Count & " override(s): " & String.Join(" · ", shown))

            _lock.EnterWriteLock()
            Try
                _current = loaded
            Finally
                _lock.ExitWriteLock()
            End Try
            _baseNode = baseObj
            _effectiveOverlay = effective
            _overlayApplied = applied
            _overlayRejected = rejected
            _overlayActive = True
            _currentNode = SerialiseToObject(loaded)
            _lastLoadError = ""

        Catch ex As Exception
            ' On parse error, keep the last good settings rather than crashing.
            ' At startup "last good" is the POCO defaults, so the engine is then
            ' running on uncalibrated values — record it so MainForm can surface it.
            _lastLoadError = ex.Message
            Console.WriteLine("[SettingsLoader] Parse error: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' The pre-overlay load, byte-for-byte: deserialise the base text and publish it.
    ''' Also clears overlay state, which is what makes deleting settings.local.json
    ''' revert without a restart (A50f).
    ''' </summary>
    Private Shared Sub LoadBaseOnly(json As String, opts As JsonSerializerOptions)
        Dim loaded = JsonSerializer.Deserialize(Of EngineSettings)(json, opts)
        If loaded Is Nothing Then Return
        ClearOverlayState()
        _lock.EnterWriteLock()
        Try
            _current = loaded
        Finally
            _lock.ExitWriteLock()
        End Try
        _lastLoadError = ""
    End Sub

    Private Shared Sub ClearOverlayState()
        _overlayActive = False
        _baseNode = Nothing
        _effectiveOverlay = Nothing
        _currentNode = Nothing
        _overlayApplied = New List(Of String)
        _overlayRejected = New List(Of String)
    End Sub

    ''' <summary>Overlay text, or Nothing when the file is absent or unreadable.</summary>
    Private Shared Function ReadOverlayText() As String
        If String.IsNullOrEmpty(_localPath) OrElse Not File.Exists(_localPath) Then Return Nothing
        Try
            Return File.ReadAllText(_localPath)
        Catch ex As Exception
            Console.WriteLine("[SettingsLoader] " & LocalOverlayFileName &
                              ": read error — IGNORED (" & ex.Message & ")")
            Return Nothing
        End Try
    End Function

    ' -- whitelist ------------------------------------------------------------

    ''' <summary>
    ''' Allow-list membership, ORDINAL. Case variants fail closed (rejected + logged)
    ''' rather than being admitted — a case-insensitive admit would merge a duplicate
    ''' key alongside the real one and hand the case-insensitive deserialiser an
    ''' ambiguity.
    ''' </summary>
    Private Shared Function IsAdmitted(path As String) As Boolean
        For Each b As String In AdmittedBlocks
            If path = b OrElse path.StartsWith(b & ".", StringComparison.Ordinal) Then Return True
        Next
        For Each k As String In AdmittedKeys
            If path = k Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' Filter the overlay down to admitted keys. Rejected dotted paths accumulate in
    ''' `rejected` for logging. Descends into non-admitted objects because an admitted
    ''' leaf can live under one (network.ws_url), which is also why nothing is admitted
    ''' by descent alone.
    ''' </summary>
    Private Shared Function FilterOverlay(overlay As JsonObject, rejected As List(Of String)) As JsonObject
        Dim outp As New JsonObject()
        WalkFilter(overlay, "", outp, rejected)
        Return outp
    End Function

    Private Shared Sub WalkFilter(src As JsonObject, prefix As String, dst As JsonObject, rejected As List(Of String))
        For Each kv As KeyValuePair(Of String, JsonNode) In src
            Dim path As String = If(prefix = "", kv.Key, prefix & "." & kv.Key)
            If IsAdmitted(path) Then
                dst(kv.Key) = CloneNode(kv.Value)
                Continue For
            End If
            Dim child As JsonObject = TryCast(kv.Value, JsonObject)
            If child IsNot Nothing Then
                Dim nested As New JsonObject()
                WalkFilter(child, path, nested, rejected)
                If nested.Count > 0 Then dst(kv.Key) = nested
            Else
                rejected.Add(path)
            End If
        Next
    End Sub

    ''' <summary>Human-readable "why" appended to the rejection log line.</summary>
    Private Shared Function RejectNote(path As String) As String
        For Each entry As String In RejectNotes
            Dim sep As Integer = entry.IndexOf("|"c)
            Dim key As String = entry.Substring(0, sep)
            If path = key OrElse path.StartsWith(key & ".", StringComparison.Ordinal) Then
                Return " (" & entry.Substring(sep + 1) & ")"
            End If
        Next
        Return ""
    End Function

    ' -- JsonNode plumbing ----------------------------------------------------

    ''' <summary>
    ''' Deep per-key merge of `overlay` over `target`. Objects recurse; arrays and
    ''' scalars replace wholesale (there is no sane per-element merge for
    ''' session_volume.sessions[], and the whitelist fences that block out anyway).
    ''' </summary>
    Private Shared Sub MergeInto(target As JsonObject, overlay As JsonObject)
        If target Is Nothing OrElse overlay Is Nothing Then Return
        For Each kv As KeyValuePair(Of String, JsonNode) In overlay
            Dim child As JsonObject = TryCast(kv.Value, JsonObject)
            If child Is Nothing Then
                target(kv.Key) = CloneNode(kv.Value)
                Continue For
            End If
            Dim tgtChild As JsonObject = TryCast(target(kv.Key), JsonObject)
            If tgtChild Is Nothing Then
                tgtChild = New JsonObject()
                target(kv.Key) = tgtChild
            End If
            MergeInto(tgtChild, child)
        Next
    End Sub

    ''' <summary>Every leaf (non-object) path in the tree, dotted, in file order.</summary>
    Private Shared Sub CollectLeafPaths(src As JsonObject, prefix As String, outp As List(Of String))
        If src Is Nothing Then Return
        For Each kv As KeyValuePair(Of String, JsonNode) In src
            Dim path As String = If(prefix = "", kv.Key, prefix & "." & kv.Key)
            Dim child As JsonObject = TryCast(kv.Value, JsonObject)
            If child IsNot Nothing Then
                CollectLeafPaths(child, path, outp)
            Else
                outp.Add(path)
            End If
        Next
    End Sub

    ''' <summary>Re-parse rather than DeepClone — a JsonNode may not be attached twice.</summary>
    Private Shared Function CloneNode(n As JsonNode) As JsonNode
        If n Is Nothing Then Return Nothing
        Return JsonNode.Parse(n.ToJsonString())
    End Function

    Private Shared Function NodeEquals(a As JsonNode, b As JsonNode) As Boolean
        If a Is Nothing AndAlso b Is Nothing Then Return True
        If a Is Nothing OrElse b Is Nothing Then Return False
        Return a.ToJsonString() = b.ToJsonString()
    End Function

    Private Shared Function NodeText(n As JsonNode) As String
        If n Is Nothing Then Return "null"
        Return n.ToJsonString()
    End Function

    Private Shared Function SerialiseToObject(settings As EngineSettings) As JsonObject
        If settings Is Nothing Then Return Nothing
        Try
            Return TryCast(JsonNode.Parse(JsonSerializer.Serialize(settings)), JsonObject)
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function TryGetPath(root As JsonObject, path As String, ByRef value As JsonNode) As Boolean
        value = Nothing
        If root Is Nothing Then Return False
        Dim parts As String() = path.Split("."c)
        Dim cur As JsonObject = root
        For i As Integer = 0 To parts.Length - 2
            If cur Is Nothing OrElse Not cur.ContainsKey(parts(i)) Then Return False
            cur = TryCast(cur(parts(i)), JsonObject)
        Next
        If cur Is Nothing OrElse Not cur.ContainsKey(parts(parts.Length - 1)) Then Return False
        value = cur(parts(parts.Length - 1))
        Return True
    End Function

    Private Shared Sub SetPath(root As JsonObject, path As String, value As JsonNode)
        If root Is Nothing Then Return
        Dim parts As String() = path.Split("."c)
        Dim cur As JsonObject = root
        For i As Integer = 0 To parts.Length - 2
            Dim nxt As JsonObject = TryCast(cur(parts(i)), JsonObject)
            If nxt Is Nothing Then
                nxt = New JsonObject()
                cur(parts(i)) = nxt
            End If
            cur = nxt
        Next
        cur(parts(parts.Length - 1)) = value
    End Sub

    Private Shared Sub RemovePath(root As JsonObject, path As String)
        If root Is Nothing Then Return
        Dim parts As String() = path.Split("."c)
        Dim cur As JsonObject = root
        For i As Integer = 0 To parts.Length - 2
            cur = TryCast(cur(parts(i)), JsonObject)
            If cur Is Nothing Then Return
        Next
        cur.Remove(parts(parts.Length - 1))
    End Sub

    ' -- watchers -------------------------------------------------------------

    Private Shared Sub StartWatcher(path As String)
        ' Fully-qualified to avoid collision with System.Windows.Shapes.Path in WinForms projects.
        Dim dir As String = System.IO.Path.GetDirectoryName(path)
        Dim fileName As String = System.IO.Path.GetFileName(path)
        If String.IsNullOrEmpty(dir) OrElse Not Directory.Exists(dir) Then Return

        DisposeWatchers()

        _watcher = New FileSystemWatcher(dir, fileName) With {
            .NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size,
            .EnableRaisingEvents = True
        }
        AddHandler _watcher.Changed, AddressOf OnFileChanged

        ' §1.1 — the overlay hot-reloads too, otherwise editing it appears to do nothing
        ' until restart. Created/Deleted/Renamed matter here in a way they don't for the
        ' base file: dropping the overlay in, or deleting it, IS the edit (A50f), and
        ' FileName is the NotifyFilter those events need.
        _localWatcher = New FileSystemWatcher(dir, LocalOverlayFileName) With {
            .NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size Or NotifyFilters.FileName,
            .EnableRaisingEvents = True
        }
        AddHandler _localWatcher.Changed, AddressOf OnOverlayChanged
        AddHandler _localWatcher.Created, AddressOf OnOverlayChanged
        AddHandler _localWatcher.Deleted, AddressOf OnOverlayChanged
        AddHandler _localWatcher.Renamed, AddressOf OnOverlayRenamed
    End Sub

    Private Shared Sub DisposeWatchers()
        Try
            If _watcher IsNot Nothing Then _watcher.Dispose()
        Catch
        End Try
        Try
            If _localWatcher IsNot Nothing Then _localWatcher.Dispose()
        Catch
        End Try
        _watcher = Nothing
        _localWatcher = Nothing
    End Sub

    Private Shared Sub OnFileChanged(sender As Object, e As FileSystemEventArgs)
        ' Small delay to allow the writer to finish flushing before we read.
        Thread.Sleep(200)
        LoadFromDisk()
        Console.WriteLine("[SettingsLoader] Hot-reloaded settings.json")
    End Sub

    Private Shared Sub OnOverlayChanged(sender As Object, e As FileSystemEventArgs)
        Thread.Sleep(200)
        LoadFromDisk()
        Console.WriteLine("[SettingsLoader] Hot-reloaded " & LocalOverlayFileName &
                          " (" & e.ChangeType.ToString() & ") — overlay active: " & _overlayActive)
    End Sub

    Private Shared Sub OnOverlayRenamed(sender As Object, e As RenamedEventArgs)
        OnOverlayChanged(sender, e)
    End Sub

    Private Shared Sub WriteDefaults(path As String)
        Dim defaults As New EngineSettings()
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(path, JsonSerializer.Serialize(defaults, opts))
    End Sub

End Class
