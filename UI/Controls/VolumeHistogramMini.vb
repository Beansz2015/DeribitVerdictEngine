' UI/Controls/VolumeHistogramMini.vb
' UI reskin P3a — 8-bar horizontal mini volume profile. POC bar is
' highlighted amber; a horizontal price line crosses the histogram at
' CurrentPriceFraction (0 = bottom, 1 = top).

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class VolumeHistogramMini
    Inherits Control

    Private _buckets As Single() = New Single() {0.12F, 0.28F, 0.55F, 0.8F, 1.0F, 0.72F, 0.45F, 0.22F}
    Private _pocIndex As Integer = 4
    Private _currentPriceFraction As Single = 0.36F
    Private _barColor As Color = Theme.FG_DIM
    Private _pocColor As Color = Theme.ACC_WARN
    Private _priceLineColor As Color = Theme.ACC_STRONG_LONG

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
    End Sub

    <Browsable(False)>
    Public Property Buckets As Single()
        Get
            Return _buckets
        End Get
        Set(v As Single())
            If v Is Nothing OrElse v.Length = 0 Then Return
            _buckets = v
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(4)>
    Public Property PocIndex As Integer
        Get
            Return _pocIndex
        End Get
        Set(v As Integer)
            _pocIndex = v
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(0.36F)>
    Public Property CurrentPriceFraction As Single
        Get
            Return _currentPriceFraction
        End Get
        Set(v As Single)
            If v < 0 Then v = 0
            If v > 1 Then v = 1
            _currentPriceFraction = v
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property BarColor As Color
        Get
            Return _barColor
        End Get
        Set(c As Color)
            _barColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property PocColor As Color
        Get
            Return _pocColor
        End Get
        Set(c As Color)
            _pocColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property PriceLineColor As Color
        Get
            Return _priceLineColor
        End Get
        Set(c As Color)
            _priceLineColor = c
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality

        Dim n = _buckets.Length
        If n < 1 Then Return
        Dim usableW As Single = Me.ClientSize.Width - 4.0F
        Dim usableH As Single = Me.ClientSize.Height - 2.0F
        If usableW <= 0 OrElse usableH <= 0 Then Return

        Dim barH As Single = usableH / CSng(n) - 1.0F
        If barH < 1 Then barH = 1

        ' Draw top-to-bottom, so index 0 sits at the top.
        For i = 0 To n - 1
            Dim w = _buckets(i) * usableW
            If w < 1 Then w = 1
            Dim y As Single = 1.0F + CSng(i) * (barH + 1.0F)
            Dim colour As Color = _barColor
            If i = _pocIndex Then
                colour = Color.FromArgb(128, _pocColor.R, _pocColor.G, _pocColor.B)
            End If
            Using brush As New SolidBrush(colour)
                g.FillRectangle(brush, 2.0F, y, w, barH)
            End Using
        Next

        ' Current-price line.
        Dim priceY As Single = Me.ClientSize.Height * (1.0F - _currentPriceFraction)
        Using pen As New Pen(_priceLineColor, 1.0F)
            g.DrawLine(pen, 0.0F, priceY, CSng(Me.ClientSize.Width), priceY)
        End Using
    End Sub

End Class
