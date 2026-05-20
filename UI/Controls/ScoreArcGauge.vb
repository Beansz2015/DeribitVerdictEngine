' UI/Controls/ScoreArcGauge.vb
' UI reskin P3a — 240° score arc with centred numeric + denominator,
' driven by Value / Max. Optional 300ms cubic ease-out animation on
' value changes via SetValueAnimated().
'
' Geometry: arc starts at -210° and sweeps 240° clockwise, leaving the
' open gap at the bottom-centre. Centre is offset down 6px so the gap
' looks balanced.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class ScoreArcGauge
    Inherits Control

    Private Const ARC_START As Single = -210.0F
    Private Const ARC_SWEEP As Single = 240.0F
    Private Const ANIM_MS As Integer = 300
    Private Const TICK_MS As Integer = 15

    Private _value As Integer = 0
    Private _max As Integer = 20
    Private _arcColor As Color = Theme.ACC_STRONG_LONG
    Private _backArcColor As Color = Theme.BG_CARD_RAISED
    Private _labelFont As Font = Theme.FontMono(22.0F, FontStyle.Bold)
    Private _denomFont As Font = Theme.FontMono(11.0F, FontStyle.Regular)
    Private _animationEnabled As Boolean = True

    Private _currentValue As Single = 0.0F
    Private _animFrom As Single = 0.0F
    Private _animTo As Single = 0.0F
    Private _animStartTick As Integer = 0
    Private WithEvents _animTimer As Timer

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.SupportsTransparentBackColor, True)
        MyBase.BackColor = Color.Transparent

        _animTimer = New Timer() With {.Interval = TICK_MS}

        ' Default designer value so the gauge isn't a blank circle when
        ' dropped onto a blank form.
        _currentValue = CSng(_value)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _animTimer IsNot Nothing Then
                _animTimer.Stop()
                _animTimer.Dispose()
                _animTimer = Nothing
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    <Category("Theme")>
    <DefaultValue(0)>
    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(v As Integer)
            _value = v
            If Not Me.DesignMode AndAlso _animationEnabled Then
                ' Property setter path skips animation — use
                ' SetValueAnimated() explicitly when animation is wanted.
                _currentValue = CSng(v)
            Else
                _currentValue = CSng(v)
            End If
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(20)>
    Public Property [Max] As Integer
        Get
            Return _max
        End Get
        Set(v As Integer)
            If v < 1 Then v = 1
            _max = v
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property ArcColor As Color
        Get
            Return _arcColor
        End Get
        Set(c As Color)
            _arcColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property BackArcColor As Color
        Get
            Return _backArcColor
        End Get
        Set(c As Color)
            _backArcColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property LabelFont As Font
        Get
            Return _labelFont
        End Get
        Set(f As Font)
            _labelFont = f
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property DenomFont As Font
        Get
            Return _denomFont
        End Get
        Set(f As Font)
            _denomFont = f
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(True)>
    Public Property AnimationEnabled As Boolean
        Get
            Return _animationEnabled
        End Get
        Set(v As Boolean)
            _animationEnabled = v
        End Set
    End Property

    ''' <summary>
    ''' Kicks off the 300ms ease-out fill animation from the current
    ''' (possibly intermediate) value to newValue. If an animation is
    ''' already running, it's cancelled and a new one starts from the
    ''' current intermediate value.
    ''' </summary>
    Public Sub SetValueAnimated(newValue As Integer, newMax As Integer)
        _value = newValue
        If newMax < 1 Then newMax = 1
        _max = newMax

        If Me.DesignMode OrElse Not _animationEnabled Then
            _currentValue = CSng(newValue)
            Invalidate()
            Return
        End If

        _animFrom = _currentValue
        _animTo = CSng(newValue)
        _animStartTick = Environment.TickCount
        _animTimer.Stop()
        _animTimer.Start()
    End Sub

    Private Sub OnAnimTick(sender As Object, e As EventArgs) Handles _animTimer.Tick
        Dim elapsed = Environment.TickCount - _animStartTick
        Dim t = CSng(elapsed) / CSng(ANIM_MS)
        If t >= 1.0F Then
            _currentValue = _animTo
            _animTimer.Stop()
            Invalidate()
            Return
        End If
        If t < 0 Then t = 0
        ' Cubic ease-out: 1 - (1 - t)^3
        Dim u = 1.0F - t
        Dim eased = 1.0F - u * u * u
        _currentValue = _animFrom + (_animTo - _animFrom) * eased
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
        g.PixelOffsetMode = PixelOffsetMode.HighQuality

        Dim cx As Single = Me.ClientSize.Width / 2.0F
        Dim cy As Single = Me.ClientSize.Height / 2.0F + 6.0F
        Dim r As Single = Math.Min(Me.ClientSize.Width, Me.ClientSize.Height) / 2.0F - 8.0F
        If r <= 0 Then Return

        ' Design-mode default so the gauge looks alive when dropped on a form.
        Dim displayValue As Single = _currentValue
        Dim displayMax As Integer = _max
        Dim displayInt As Integer = _value
        If Me.DesignMode AndAlso displayValue <= 0 Then
            displayValue = 15.0F
            displayInt = 15
            displayMax = 20
        End If

        ' Background track.
        Using pen As New Pen(_backArcColor, 8.0F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, ARC_START, ARC_SWEEP)
        End Using

        ' Foreground arc.
        If displayValue > 0 Then
            Dim frac = displayValue / CSng(displayMax)
            If frac > 1.0F Then frac = 1.0F
            Dim sweep = ARC_SWEEP * frac
            Using pen As New Pen(_arcColor, 8.0F)
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, ARC_START, sweep)
            End Using
        End If

        ' Centre numeric — use rounded value so animation frames don't
        ' show fractional integers.
        Dim valStr = CInt(Math.Round(displayValue)).ToString()
        Dim denomStr = "/ " & displayMax.ToString()

        Using brushVal As New SolidBrush(_arcColor)
            Dim sz = g.MeasureString(valStr, _labelFont)
            g.DrawString(valStr, _labelFont, brushVal,
                         cx - sz.Width / 2.0F, cy - sz.Height / 2.0F - 4.0F)
        End Using

        Using brushDenom As New SolidBrush(Theme.FG_QUATERNARY)
            Dim sz = g.MeasureString(denomStr, _denomFont)
            g.DrawString(denomStr, _denomFont, brushDenom,
                         cx - sz.Width / 2.0F, cy + 14.0F)
        End Using
    End Sub

End Class
