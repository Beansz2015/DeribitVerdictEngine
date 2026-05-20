' UI/Controls/Pill.vb
' UI reskin P3b — generic rounded text capsule. Used for the [B]/[T]
' mode chip, REPEAT chip, (stale) sub-labels, and other compact text
' tags throughout the new layout.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class Pill
    Inherits Label

    Private _bgColor As Color = Theme.BG_CARD_RAISED
    Private _fgColor As Color = Theme.FG_SECONDARY
    Private _borderColor As Color = Theme.BORDER_CARD
    Private _cornerRadius As Single = 8.0F
    Private _paddingX As Integer = 8

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        Me.Font = Theme.FontMono(9.5F, FontStyle.Bold)
        Me.AutoSize = False
        Me.TextAlign = ContentAlignment.MiddleCenter
        Me.Height = 20
    End Sub

    <Category("Theme")>
    Public Property BgColor As Color
        Get
            Return _bgColor
        End Get
        Set(c As Color)
            _bgColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property FgColor As Color
        Get
            Return _fgColor
        End Get
        Set(c As Color)
            _fgColor = c
            ForeColor = c
            Invalidate()
        End Set
    End Property

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

    <Category("Theme")>
    <DefaultValue(8.0F)>
    Public Property CornerRadius As Single
        Get
            Return _cornerRadius
        End Get
        Set(v As Single)
            _cornerRadius = v
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit

        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        Using path = PaintHelpers.RoundedRect(rect, _cornerRadius)
            Using brush As New SolidBrush(_bgColor)
                g.FillPath(brush, path)
            End Using
            Using pen As New Pen(_borderColor, 1.0F)
                g.DrawPath(pen, path)
            End Using
        End Using

        Dim label = If(Me.Text, "")
        Using brushText As New SolidBrush(_fgColor)
            Dim sz = g.MeasureString(label, Me.Font)
            Dim x = (Me.Width - sz.Width) / 2.0F
            Dim y = (Me.Height - sz.Height) / 2.0F
            g.DrawString(label, Me.Font, brushText, x, y)
        End Using
    End Sub

End Class
