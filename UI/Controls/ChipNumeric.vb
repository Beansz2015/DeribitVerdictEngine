' UI/Controls/ChipNumeric.vb
' UI reskin P3b — restyled NumericUpDown to look like a flat chip.
' Border in BORDER_CARD, transparent background, dim spinner arrows
' (revealed visually only when hovered — WinForms doesn't let us hide
' the UpDownButtons child cleanly, but we can recolour and gate
' Visible).
'
' If the inherit path turns out too restrictive in P4 wiring (NumericUpDown
' has a child UpDownButtons control that resists override), surface a
' switch to a wrapped UserControl + two FlatButton arrows. Documented in
' the P3 kickoff §"If you get stuck".

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class ChipNumeric
    Inherits NumericUpDown

    Private _isHover As Boolean = False
    Private _borderColor As Color = Theme.BORDER_CARD

    Public Sub New()
        Me.BorderStyle = BorderStyle.None
        Me.BackColor = Theme.BG_CARD_RAISED
        Me.ForeColor = Theme.FG_PRIMARY
        Me.Font = Theme.FontMono(10.0F, FontStyle.Bold)
        Me.TextAlign = HorizontalAlignment.Center
    End Sub

    <Category("Theme")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(c As Color)
            _borderColor = c
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _isHover = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _isHover = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.ClientRectangle.Width <= 0 OrElse Me.ClientRectangle.Height <= 0 Then Return
        MyBase.OnPaint(e)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        Using path = PaintHelpers.RoundedRect(rect, 4.0F)
            Using pen As New Pen(If(_isHover, Theme.ACC_INFO, _borderColor), 1.0F)
                g.DrawPath(pen, path)
            End Using
        End Using
    End Sub

End Class
