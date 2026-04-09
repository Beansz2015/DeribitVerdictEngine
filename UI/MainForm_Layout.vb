' UI/MainForm_Layout.vb
' Root partial class: layout constants, P/Invoke, shared fields, constructor,
' NUD centring, ResizeControls, SetOutputMargins.

Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Layout constants
    ' -----------------------------------------------------------------------
    Private Const HDR_Y    As Integer = 8
    Private Const HDR_H    As Integer = 42
    Private Const BTN_X    As Integer = 286
    Private Const BTN_W    As Integer = 140
    Private Const VRD_X    As Integer = 430
    Private Const AR_Y     As Integer = 58     ' 8px below HDR bottom (50)
    Private Const AR_H     As Integer = 22
    Private Const TXT_Y    As Integer = 88     ' 8px below AR bottom (80)
    Private Const SS_X     As Integer = 426    ' right edge of Analyze button
    Private Const STATUS_H As Integer = 18

    ' -----------------------------------------------------------------------
    ' P/Invoke: Win32 edit-control messages
    ' -----------------------------------------------------------------------
    Private Const EM_SETMARGINS  As Integer = &HD3
    Private Const EC_LEFTMARGIN  As Integer = 1
    Private Const EC_RIGHTMARGIN As Integer = 2
    Private Const EM_SETRECT     As Integer = &HB3
    Private Const EM_SETRECTNP   As Integer = &HB4

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, ByRef lParam As RECT) As IntPtr
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RECT
        Public Left   As Integer
        Public Top    As Integer
        Public Right  As Integer
        Public Bottom As Integer
    End Structure

    ' -----------------------------------------------------------------------
    ' Shared fields (used by multiple partials)
    ' -----------------------------------------------------------------------
    Private _oiHistory As New List(Of OiSnapshot)()

    ' Auto-run state
    Private _autoRunTimer   As IAutoRunTimer
    Private _countdownTimer As Threading.Timer
    Private _countdownSecs  As Integer = 0
    Private _intervalMs     As Integer = 60_000

    Private Shared ReadOnly CHAR_PLAY As String = ChrW(9654) & " Start"
    Private Shared ReadOnly CHAR_STOP As String = ChrW(9632) & " Stop"

    ' Colour palette (used by Render partial)
    Private Shared ReadOnly C_DIVIDER As Color = Color.FromArgb(80, 80, 80)
    Private Shared ReadOnly C_HEADER  As Color = Color.FromArgb(255, 220, 80)   ' amber
    Private Shared ReadOnly C_LABEL   As Color = Color.FromArgb(160, 160, 160)  ' mid-grey
    Private Shared ReadOnly C_VALUE   As Color = Color.FromArgb(200, 200, 200)  ' light-grey
    Private Shared ReadOnly C_GOOD    As Color = Color.FromArgb(80, 220, 120)   ' green
    Private Shared ReadOnly C_WARN    As Color = Color.FromArgb(255, 180, 40)   ' orange
    Private Shared ReadOnly C_BAD     As Color = Color.FromArgb(255, 80, 80)    ' red
    Private Shared ReadOnly C_HIT     As Color = Color.FromArgb(100, 200, 255)  ' cyan
    Private Shared ReadOnly C_DIM     As Color = Color.FromArgb(100, 100, 100)  ' dim

    ' -----------------------------------------------------------------------
    ' Constructor
    ' -----------------------------------------------------------------------
    Public Sub New()
        InitializeComponent()
        Me.Text = "Deribit Verdict Engine v0.46"

        ' Initial window size: wide enough for the output panel, tall enough
        ' to show all content without scrolling on first open.
        Me.ClientSize   = New System.Drawing.Size(1000, 900)
        Me.MinimumSize  = New System.Drawing.Size(820, 600)

        SetOutputMargins(6, 6)
        AddHandler Me.Resize, Sub(s As Object, ev As EventArgs) ResizeControls()
        AddHandler Me.HandleCreated, AddressOf OnFormHandleCreated
        ResizeControls()
        UpdateLogInfo()
        SettingsLoader.Initialise(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json"))
        InitAutoRunControls()
    End Sub

    ' -----------------------------------------------------------------------
    ' NUD digit vertical centring
    ' -----------------------------------------------------------------------
    Private Sub OnFormHandleCreated(sender As Object, e As EventArgs)
        CentreNudText(nudMinutes)
        CentreNudText(nudSeconds)
    End Sub

    Private Shared Sub CentreNudText(nud As NumericUpDown)
        If nud.Controls.Count = 0 Then Return
        Dim innerTb As Control = nud.Controls(0)
        If Not innerTb.IsHandleCreated Then innerTb.CreateControl()

        Const SPIN_W  As Integer = 16
        Const TOP_PAD As Integer = 3
        Dim h As Integer = innerTb.Height

        Dim rc As RECT
        rc.Left   = 1
        rc.Top    = TOP_PAD
        rc.Right  = innerTb.Width - SPIN_W - 2
        rc.Bottom = h - 1
        SendMessage(innerTb.Handle, EM_SETRECTNP, 0, rc)
    End Sub

    ' -----------------------------------------------------------------------
    ' Layout / resize
    ' -----------------------------------------------------------------------
    Private Sub SetOutputMargins(leftPx As Integer, rightPx As Integer)
        Dim lParam As Integer = (rightPx << 16) Or (leftPx And &HFFFF)
        SendMessage(txtOutput.Handle, EM_SETMARGINS, EC_LEFTMARGIN Or EC_RIGHTMARGIN, lParam)
    End Sub

    Private Sub ResizeControls()
        Dim W As Integer = Me.ClientSize.Width
        Dim H As Integer = Me.ClientSize.Height

        ' Row 1 -- position / analyze / verdict
        lblPositionTitle.Location = New System.Drawing.Point(8, HDR_Y)
        lblPositionTitle.Size     = New System.Drawing.Size(108, HDR_H)
        rbNone.Location  = New System.Drawing.Point(120, HDR_Y + (HDR_H - 18) \ 2)
        rbLong.Location  = New System.Drawing.Point(210, HDR_Y + 2)
        rbShort.Location = New System.Drawing.Point(210, HDR_Y + 22)
        btnAnalyze.Location = New System.Drawing.Point(BTN_X, HDR_Y)
        btnAnalyze.Size     = New System.Drawing.Size(BTN_W, HDR_H)
        lblVerdict.Location = New System.Drawing.Point(VRD_X, HDR_Y)
        lblVerdict.Size     = New System.Drawing.Size(W - VRD_X - 8, HDR_H)

        ' Row 2 -- auto-run controls
        lblAutoRun.Location   = New System.Drawing.Point(8, AR_Y)
        lblAutoRun.Size       = New System.Drawing.Size(78, AR_H)
        nudMinutes.Location   = New System.Drawing.Point(90, AR_Y)
        nudMinutes.Size       = New System.Drawing.Size(42, AR_H)
        lblMin.Location       = New System.Drawing.Point(136, AR_Y + 3)
        nudSeconds.Location   = New System.Drawing.Point(164, AR_Y)
        nudSeconds.Size       = New System.Drawing.Size(42, AR_H)
        lblSec.Location       = New System.Drawing.Point(210, AR_Y + 3)
        pnlMode.Location      = New System.Drawing.Point(242, AR_Y)
        pnlMode.Size          = New System.Drawing.Size(134, AR_H)
        rbSingle.Location     = New System.Drawing.Point(0, 2)
        rbRepeat.Location     = New System.Drawing.Point(68, 2)
        btnStartStop.Location = New System.Drawing.Point(SS_X, AR_Y - 1)
        btnStartStop.Size     = New System.Drawing.Size(70, AR_H + 2)

        ' Output textbox
        Dim statusY As Integer = H - STATUS_H - 2
        txtOutput.Location = New System.Drawing.Point(8, TXT_Y)
        txtOutput.Size     = New System.Drawing.Size(W - 16, statusY - TXT_Y - 2)
        SetOutputMargins(6, 6)

        ' Status bar
        lblLogInfo.Location    = New System.Drawing.Point(8, H - STATUS_H)
        lblLogInfo.Size        = New System.Drawing.Size(W - 420, STATUS_H)
        lblCountdown.Location  = New System.Drawing.Point(W - 410, H - STATUS_H)
        lblCountdown.Size      = New System.Drawing.Size(200, STATUS_H)
        lnkCalibCheck.Location = New System.Drawing.Point(W - 230, H - STATUS_H)
        lnkResetLog.Location   = New System.Drawing.Point(W - 80, H - STATUS_H)
    End Sub

End Class
