' UI/Controls/RegimeAnchorWarn.vb
' UI reskin P3b — amber-pill caution row used when a STRONG verdict is
' fighting the 5m EMA(200) anchor. Conditional render — Height collapses
' to 0 when WarningText is empty so the parent layout doesn't reserve
' dead space.
'
' F-08 (consolidated fix 2026-06-12): the banner used a fixed 26 px height
' and a single-line DrawString, so the warning's conclusion ("fighting
' intermediate bear/bull") clipped at the card edge on cases 41/42. The
' text now word-wraps: height is measured against the current width and
' OnPaint draws into a wrapping rectangle. Wording unchanged (matches the
' legacy REGIME ANCHOR line for PNG↔legacy parity).

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class RegimeAnchorWarn
    Inherits Panel

    Private _warningText As String = ""

    ' Horizontal text inset (10 px left + 10 px right) and vertical padding
    ' (5 px top + 5 px bottom) used by both the measure and the paint.
    Private Const TEXT_INSET_X As Integer = 10
    Private Const PAD_Y As Integer = 5
    Private Const MIN_HEIGHT As Integer = 26

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        Me.Height = 0
        Me.Visible = False
    End Sub

    <Category("Theme")>
    Public Property WarningText As String
        Get
            Return _warningText
        End Get
        Set(v As String)
            _warningText = If(v, "")
            If String.IsNullOrEmpty(_warningText) Then
                Me.Visible = False
                Me.Height = 0
            Else
                Me.Visible = True
                Me.Height = MeasureRequiredHeight()
            End If
            Invalidate()
        End Set
    End Property

    Private Function DisplayText() As String
        Return $"⚠ REGIME ANCHOR — {_warningText}"
    End Function

    ''' <summary>
    ''' Word-wrapped height of the display text at the current control width.
    ''' Falls back to single-line height when Width isn't known yet (pre-layout).
    ''' </summary>
    Private Function MeasureRequiredHeight() As Integer
        Dim availWidth As Integer = Me.Width - TEXT_INSET_X * 2
        If availWidth <= 0 Then Return MIN_HEIGHT
        Using font = Theme.FontMono(10.5F, FontStyle.Bold)
            Dim sz = TextRenderer.MeasureText(DisplayText(), font,
                                              New Size(availWidth, Integer.MaxValue),
                                              TextFormatFlags.WordBreak)
            Return Math.Max(MIN_HEIGHT, sz.Height + PAD_Y * 2)
        End Using
    End Function

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        ' Width changes can change the wrap point — re-measure (no-op while
        ' hidden). Guard against feedback: only set when the value differs.
        If Not String.IsNullOrEmpty(_warningText) Then
            Dim h = MeasureRequiredHeight()
            If h <> Me.Height Then Me.Height = h
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.ClientRectangle.Width <= 0 OrElse Me.ClientRectangle.Height <= 0 Then Return
        If String.IsNullOrEmpty(_warningText) Then Return
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit

        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        Dim bgColour = Color.FromArgb(31, Theme.ACC_WARN.R, Theme.ACC_WARN.G, Theme.ACC_WARN.B)        ' ~12%
        Dim borderColour = Color.FromArgb(102, Theme.ACC_WARN.R, Theme.ACC_WARN.G, Theme.ACC_WARN.B)   ' ~40%
        Using path = PaintHelpers.RoundedRect(rect, 6.0F)
            Using brush As New SolidBrush(bgColour)
                g.FillPath(brush, path)
            End Using
            Using pen As New Pen(borderColour, 1.0F)
                g.DrawPath(pen, path)
            End Using
        End Using

        Dim font = Theme.FontMono(10.5F, FontStyle.Bold)
        Try
            ' TextRenderer to match the MeasureText wrap geometry exactly.
            Dim textRect = New Rectangle(TEXT_INSET_X, PAD_Y,
                                         Me.Width - TEXT_INSET_X * 2,
                                         Me.Height - PAD_Y * 2)
            TextRenderer.DrawText(g, DisplayText(), font, textRect, Theme.ACC_WARN,
                                  TextFormatFlags.WordBreak Or TextFormatFlags.VerticalCenter)
        Finally
            font.Dispose()
        End Try
    End Sub

End Class
