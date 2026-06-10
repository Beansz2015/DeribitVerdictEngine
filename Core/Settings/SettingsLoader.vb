' Core/Settings/SettingsLoader.vb
' Loads EngineSettings from settings.json, provides hot-reload via FileSystemWatcher.
' Thread-safe singleton access via SettingsLoader.Current.
'
' Usage:
'   SettingsLoader.Initialise(path)   -- call once at app startup
'   SettingsLoader.Current            -- read settings anywhere, always up-to-date

Imports System.IO
Imports System.Text.Json
Imports System.Threading

Public Class SettingsLoader

    Private Shared _current As EngineSettings = New EngineSettings()
    Private Shared _lock As New ReaderWriterLockSlim()
    Private Shared _watcher As FileSystemWatcher
    Private Shared _settingsPath As String = ""
    Private Shared _lastLoadError As String = ""

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
    ''' </summary>
    Public Shared Sub Initialise(settingsPath As String)
        _settingsPath = settingsPath

        If Not File.Exists(settingsPath) Then
            WriteDefaults(settingsPath)
        End If

        LoadFromDisk()
        StartWatcher(settingsPath)
    End Sub

    ''' <summary>
    ''' Save the supplied settings object back to settings.json.
    ''' Updates version, last_modified timestamp, and appends to change_log.
    ''' </summary>
    Public Shared Sub Save(settings As EngineSettings, changeNote As String)
        If String.IsNullOrEmpty(_settingsPath) Then Return
        settings.Version += 1
        settings.LastModified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        If Not String.IsNullOrEmpty(changeNote) Then
            settings.ChangeLog.Add(String.Format("v{0} [{1}]: {2}", settings.Version, settings.LastModified, changeNote))
        End If
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        Dim json As String = JsonSerializer.Serialize(settings, opts)
        _lock.EnterWriteLock()
        Try
            AtomicWriteAllText(_settingsPath, json)
            _current = settings
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
            Dim loaded = JsonSerializer.Deserialize(Of EngineSettings)(json, opts)
            If loaded IsNot Nothing Then
                _lock.EnterWriteLock()
                Try
                    _current = loaded
                Finally
                    _lock.ExitWriteLock()
                End Try
                _lastLoadError = ""
            End If
        Catch ex As Exception
            ' On parse error, keep the last good settings rather than crashing.
            ' At startup "last good" is the POCO defaults, so the engine is then
            ' running on uncalibrated values — record it so MainForm can surface it.
            _lastLoadError = ex.Message
            Console.WriteLine("[SettingsLoader] Parse error: " & ex.Message)
        End Try
    End Sub

    Private Shared Sub StartWatcher(path As String)
        ' Fully-qualified to avoid collision with System.Windows.Shapes.Path in WinForms projects.
        Dim dir As String = System.IO.Path.GetDirectoryName(path)
        Dim fileName As String = System.IO.Path.GetFileName(path)
        If String.IsNullOrEmpty(dir) OrElse Not Directory.Exists(dir) Then Return

        _watcher = New FileSystemWatcher(dir, fileName) With {
            .NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size,
            .EnableRaisingEvents = True
        }
        AddHandler _watcher.Changed, AddressOf OnFileChanged
    End Sub

    Private Shared Sub OnFileChanged(sender As Object, e As FileSystemEventArgs)
        ' Small delay to allow the writer to finish flushing before we read.
        Thread.Sleep(200)
        LoadFromDisk()
        Console.WriteLine("[SettingsLoader] Hot-reloaded settings.json")
    End Sub

    Private Shared Sub WriteDefaults(path As String)
        Dim defaults As New EngineSettings()
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(path, JsonSerializer.Serialize(defaults, opts))
    End Sub

End Class
