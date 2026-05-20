' UI/Controls/MiniMeter.vb
' UI reskin P3a — composite control: label + right-aligned value with a
' thin 4px progress bar below. Used inside OI × CVD CROSS card for
' Funding Mom and Spread readouts.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class MiniMeter
    Inherits UserControl

    Private _labelText As String = "Label"
    Private _valueText As String = "--"
    Private _pct As Single = 0.0F
    Private _barColor As Color = Theme.ACC_STRONG_LONG
    Private _labelColor As Color = Theme.FG_TERTIARY
    Private _valueColor As Color = Theme.FG_PRIMARY

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        MyBase.Size = New Size(180, 24)
    End Sub

    <Category("Theme")>
    Public Property LabelText As String
        Get
            Return _labelText
        End Get
        Set(v As String)
            _labelText = If(v, "")
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property ValueText As String
        Get
            Return _valueText
        End Get
        Set(v As String)
            _valueText = If(v, "")
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(0.0F)>
    Public Property Pct As Single
        Get
            Return _pct
        End Get
        Set(v As Single)
            If v < 0 Then v = 0
            If v > 100 Then v = 100
            _pct = v
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
    Public Property LabelColor As Color
        Get
            Return _labelColor
        End Get
        Set(c As Color)
            _labelColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property ValueColor As Color
        Get
            Return _valueColor
        End Get
        Set(c As Color)
            _valueColor = c
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim labelFont = Theme.FontMono(9.5F, FontStyle.Regular)
        Dim valueFont = Theme.FontMono(10.0F, FontStyle.Bold)
        Try
            ' Label left-aligned.
            Using brush As New SolidBrush(_labelColor)
                g.DrawString(_labelText, labelFont, brush, 0.0F, 0.0F)
            End Using

            ' Value right-aligned.
            Using brush As New SolidBrush(_valueColor)
                Dim sz = g.MeasureString(_valueText, valueFont)
                g.DrawString(_valueText, valueFont, brush,
                             CSng(Me.ClientSize.Width) - sz.Width, 0.0F)
            End Using

            ' Bar below.
            Dim barY As Single = CSng(Me.ClientSize.Height) - 4.0F
            Dim barH As Single = 4.0F
            Using brushBg As New SolidBrush(Theme.BG_CARD_RAISED)
                g.FillRectangle(brushBg, 0.0F, barY, CSng(Me.ClientSize.Width), barH)
            End Using
            Dim fillW = CSng(Me.ClientSize.Width) * (_pct / 100.0F)
            If fillW > 0 Then
                Using brushFill As New SolidBrush(_barColor)
                    g.FillRectangle(brushFill, 0.0F, barY, fillW, barH)
                End Using
            End If
        Finally
            labelFont.Dispose()
            valueFont.Dispose()
        End Try
    End Sub

End Class
