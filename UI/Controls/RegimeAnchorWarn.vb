' UI/Controls/RegimeAnchorWarn.vb
' UI reskin P3b — amber-pill caution row used when a STRONG verdict is
' fighting the 5m EMA(200) anchor. Conditional render — Height collapses
' to 0 when WarningText is empty so the parent layout doesn't reserve
' dead space.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class RegimeAnchorWarn
    Inherits Panel

    Private _warningText As String = ""

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
                Me.Height = 26
            End If
            Invalidate()
        End Set
    End Property

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

        Dim text = $"⚠ REGIME ANCHOR — {_warningText}"
        Dim font = Theme.FontMono(10.5F, FontStyle.Bold)
        Try
            Using brushText As New SolidBrush(Theme.ACC_WARN)
                Dim sz = g.MeasureString(text, font)
                g.DrawString(text, font, brushText, 10.0F, (Me.Height - sz.Height) / 2.0F)
            End Using
        Finally
            font.Dispose()
        End Try
    End Sub

End Class
