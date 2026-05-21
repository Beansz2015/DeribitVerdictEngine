' UI/Controls/RoundedCardPanel.vb
' UI reskin P3a — rounded-corner panel used as the backdrop for every card
' in the new layout. 1px border, optional dashed accent (used by the
' AUTO-RUN sub-box).
'
' Designer-safe — no runtime state captured at construct time.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class RoundedCardPanel
    Inherits Panel

    Public Enum CardBorderStyle
        Solid
        Dashed
    End Enum

    Private _cornerRadius As Single = 6.0F
    Private _borderColor As Color = Theme.BORDER_CARD
    Private _borderStyle As CardBorderStyle = CardBorderStyle.Solid
    Private _dashedAccent As Color = Theme.BORDER_DASHED_INFO
    Private _background As Color = Theme.BG_CARD

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        MyBase.DoubleBuffered = True
    End Sub

    <Category("Theme")>
    <DefaultValue(6.0F)>
    Public Property CornerRadius As Single
        Get
            Return _cornerRadius
        End Get
        Set(value As Single)
            If value < 0 Then value = 0
            _cornerRadius = value
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            _borderColor = value
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(GetType(CardBorderStyle), "Solid")>
    Public Property BorderStyle2 As CardBorderStyle
        Get
            Return _borderStyle
        End Get
        Set(value As CardBorderStyle)
            _borderStyle = value
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property DashedAccent As Color
        Get
            Return _dashedAccent
        End Get
        Set(value As Color)
            _dashedAccent = value
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property Background As Color
        Get
            Return _background
        End Get
        Set(value As Color)
            _background = value
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.ClientRectangle.Width <= 0 OrElse Me.ClientRectangle.Height <= 0 Then Return
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality

        ' Inset by 0.5 so the 1px stroke sits inside the client rect rather
        ' than half-clipping on the right/bottom.
        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        Using path = PaintHelpers.RoundedRect(rect, _cornerRadius)
            Using fill As New SolidBrush(_background)
                g.FillPath(fill, path)
            End Using

            Dim strokeColour = If(_borderStyle = CardBorderStyle.Dashed, _dashedAccent, _borderColor)
            Using pen As New Pen(strokeColour, 1.0F)
                If _borderStyle = CardBorderStyle.Dashed Then
                    pen.DashStyle = DashStyle.Custom
                    pen.DashPattern = New Single() {4.0F, 3.0F}
                End If
                g.DrawPath(pen, path)
            End Using
        End Using

        MyBase.OnPaint(e)
    End Sub

End Class
