' AutoRunTimer.vb  v0.1
' Portable auto-run timer abstraction for the Deribit Verdict Engine.
'
' IAutoRunTimer     -- interface; swappable between WinForms and future CLI/Linux hosts.
' WinFormsAutoRunTimer -- System.Threading.Timer implementation that marshals the
'                        analysis callback back onto the WinForms UI thread via
'                        Control.Invoke, keeping RunAnalysisAsync on the UI thread
'                        exactly as if the user had clicked Analyze Now.
'
' Portability note:
'   System.Threading.Timer is available on .NET 6+ on Linux with no dependencies.
'   For a future headless CLI port, implement IAutoRunTimer directly without Invoke.
'   No changes to core analysis or scoring logic are required.
'
' Minimum interval: 10 000 ms (10 seconds). Enforced in Start(); values below
' this floor are silently clamped to prevent runaway API hammering.

Imports System.Threading
Imports System.Windows.Forms

''' <summary>
''' Portable timer interface for the auto-run feature.
''' WinForms host uses WinFormsAutoRunTimer.
''' Future CLI/Linux host implements this directly without Control.Invoke.
''' </summary>
Public Interface IAutoRunTimer
    ''' <summary>Start (or restart) the timer with the given interval in milliseconds.</summary>
    Sub Start(intervalMs As Integer, callback As Action)
    ''' <summary>Stop the timer and release resources.</summary>
    Sub [Stop]()
    ''' <summary>True if the timer is currently running.</summary>
    ReadOnly Property IsRunning As Boolean
End Interface

''' <summary>
''' WinForms implementation of IAutoRunTimer.
''' Uses System.Threading.Timer (portable) and marshals the callback onto
''' the WinForms UI thread via the supplied owner Control.
''' </summary>
Public Class WinFormsAutoRunTimer
    Implements IAutoRunTimer

    Private Const MIN_INTERVAL_MS As Integer = 10_000

    Private ReadOnly _owner    As Control
    Private _timer             As Threading.Timer
    Private _callback          As Action
    Private _isRunning         As Boolean = False
    Private _repeat            As Boolean = True
    Private _intervalMs        As Integer

    ''' <param name="owner">The WinForms control used for thread marshalling (typically MainForm).</param>
    Public Sub New(owner As Control)
        _owner = owner
    End Sub

    Public ReadOnly Property IsRunning As Boolean Implements IAutoRunTimer.IsRunning
        Get
            Return _isRunning
        End Get
    End Property

    ''' <summary>
    ''' Start the timer.
    ''' If repeat=True (default) it fires repeatedly at intervalMs.
    ''' If repeat=False it fires once then stops automatically.
    ''' intervalMs is clamped to MIN_INTERVAL_MS (10 s) as a safety floor.
    ''' </summary>
    Public Sub Start(intervalMs As Integer, callback As Action) Implements IAutoRunTimer.Start
        [Stop]()
        _intervalMs = Math.Max(intervalMs, MIN_INTERVAL_MS)
        _callback   = callback
        _isRunning  = True
        ' dueTime = _intervalMs  (first fire after one interval, not immediately)
        ' period  = _intervalMs  (repeat) or Timeout.Infinite (single shot handled via _repeat)
        _timer = New Threading.Timer(AddressOf OnTick, Nothing, _intervalMs, _intervalMs)
    End Sub

    ''' <summary>Single-shot variant: fires once then self-stops.</summary>
    Public Sub StartOnce(intervalMs As Integer, callback As Action)
        [Stop]()
        _intervalMs = Math.Max(intervalMs, MIN_INTERVAL_MS)
        _callback   = callback
        _repeat     = False
        _isRunning  = True
        _timer = New Threading.Timer(AddressOf OnTick, Nothing, _intervalMs, Threading.Timeout.Infinite)
    End Sub

    Public Sub [Stop]() Implements IAutoRunTimer.Stop
        _isRunning = False
        _repeat    = True
        If _timer IsNot Nothing Then
            _timer.Dispose()
            _timer = Nothing
        End If
    End Sub

    Private Sub OnTick(state As Object)
        If Not _isRunning Then Return
        ' Single-shot: auto-stop before invoking so IsRunning is False during the call
        If Not _repeat Then [Stop]()
        ' Marshal onto WinForms UI thread
        If _owner.IsDisposed OrElse Not _owner.IsHandleCreated Then Return
        Try
            _owner.Invoke(Sub()
                              If _callback IsNot Nothing Then _callback()
                          End Sub)
        Catch ex As ObjectDisposedException
            ' Form closed mid-tick -- silently ignore
        End Try
    End Sub

End Class
