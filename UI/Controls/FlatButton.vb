' UI/Controls/FlatButton.vb
' UI reskin P3a — flat custom button with hover / pressed / disabled
' states and two variants:
'   Solid   — rounded-rect fill in AccentColor, ink-coloured text
'   Outline — 1px stroke at AccentColor + low-alpha tinted fill,
'             AccentColor-coloured text
'
' Optional IconText (e.g. "▶") renders before the label with an 8px gap.
' Existing Button click handlers are reused — this is a paint override,
' not a custom click model.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class FlatButton
    Inherits Button

    Public Enum ButtonVariant
        Solid
        Outline
    End Enum

    Private _variant As ButtonVariant = ButtonVariant.Solid
    Private _accentColor As Color = Theme.ACC_CTA
    Private _inkColor As Color = Theme.FG_INK
    Private _iconText As String = ""
    Private _cornerRadius As Single = 4.0F

    Protected _isHover As Boolean
    Protected _isPressed As Boolean

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        Me.FlatStyle = FlatStyle.Flat
        Me.FlatAppearance.BorderSize = 0
        Me.FlatAppearance.MouseOverBackColor = Color.Transparent
        Me.FlatAppearance.MouseDownBackColor = Color.Transparent
        Me.BackColor = Color.Transparent
        Me.ForeColor = Theme.FG_INK
        Me.Font = Theme.FontMono(11.0F, FontStyle.Bold)
        Me.Cursor = Cursors.Hand
    End Sub

    <Category("Theme")>
    <DefaultValue(GetType(ButtonVariant), "Solid")>
    Public Property [Variant] As ButtonVariant
        Get
            Return _variant
        End Get
        Set(v As ButtonVariant)
            _variant = v
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property AccentColor As Color
        Get
            Return _accentColor
        End Get
        Set(c As Color)
            _accentColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    Public Property InkColor As Color
        Get
            Return _inkColor
        End Get
        Set(c As Color)
            _inkColor = c
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue("")>
    Public Property IconText As String
        Get
            Return _iconText
        End Get
        Set(v As String)
            _iconText = If(v, "")
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(4.0F)>
    Public Property CornerRadius As Single
        Get
            Return _cornerRadius
        End Get
        Set(v As Single)
            If v < 0 Then v = 0
            _cornerRadius = v
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
        _isPressed = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseDown(mevent As MouseEventArgs)
        If mevent.Button = MouseButtons.Left Then
            _isPressed = True
            Invalidate()
        End If
        MyBase.OnMouseDown(mevent)
    End Sub

    Protected Overrides Sub OnMouseUp(mevent As MouseEventArgs)
        _isPressed = False
        Invalidate()
        MyBase.OnMouseUp(mevent)
    End Sub

    Protected Overrides Sub OnPaint(pevent As PaintEventArgs)
        If Me.ClientRectangle.Width <= 0 OrElse Me.ClientRectangle.Height <= 0 Then Return
        Dim g = pevent.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
        g.PixelOffsetMode = PixelOffsetMode.HighQuality

        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        Using path = PaintHelpers.RoundedRect(rect, _cornerRadius)
            Dim fillColour As Color
            Dim textColour As Color
            Dim borderColour As Color = Color.Empty

            If _variant = ButtonVariant.Solid Then
                fillColour = _accentColor
                If Not Me.Enabled Then
                    fillColour = Blend(fillColour, Theme.BG_CARD, 0.5F)
                ElseIf _isPressed Then
                    fillColour = Darken(fillColour, 0.1F)
                ElseIf _isHover Then
                    fillColour = Lighten(fillColour, 0.1F)
                End If
                textColour = _inkColor
            Else
                ' Outline variant
                Dim alpha As Integer = 26  ' ~10%
                If _isPressed Then
                    alpha = 64  ' ~25%
                ElseIf _isHover Then
                    alpha = 46  ' ~18%
                End If
                fillColour = Color.FromArgb(alpha, _accentColor.R, _accentColor.G, _accentColor.B)
                borderColour = _accentColor
                textColour = _accentColor
                If Not Me.Enabled Then
                    textColour = Color.FromArgb(120, _accentColor.R, _accentColor.G, _accentColor.B)
                End If
            End If

            Using brush As New SolidBrush(fillColour)
                g.FillPath(brush, path)
            End Using
            If Not borderColour.IsEmpty Then
                Using pen As New Pen(borderColour, 1.0F)
                    g.DrawPath(pen, path)
                End Using
            End If

            ' Compose label: icon + gap + text
            Dim label = If(Me.Text, "")
            Dim iconGap As Single = 8.0F
            Dim iconSize As SizeF = SizeF.Empty
            If Not String.IsNullOrEmpty(_iconText) Then
                iconSize = g.MeasureString(_iconText, Me.Font)
            End If
            Dim textSize = g.MeasureString(label, Me.Font)
            Dim totalW = iconSize.Width + (If(iconSize.Width > 0, iconGap, 0.0F)) + textSize.Width
            Dim xStart As Single = (Me.Width - totalW) / 2.0F
            Dim yMid As Single = (Me.Height - textSize.Height) / 2.0F

            Using brushText As New SolidBrush(textColour)
                If Not String.IsNullOrEmpty(_iconText) Then
                    g.DrawString(_iconText, Me.Font, brushText, xStart, yMid)
                    xStart += iconSize.Width + iconGap
                End If
                g.DrawString(label, Me.Font, brushText, xStart, yMid)
            End Using
        End Using
    End Sub

    Private Shared Function Lighten(c As Color, amount As Single) As Color
        Dim r = CInt(Math.Min(255, c.R + 255 * amount))
        Dim g = CInt(Math.Min(255, c.G + 255 * amount))
        Dim b = CInt(Math.Min(255, c.B + 255 * amount))
        Return Color.FromArgb(c.A, r, g, b)
    End Function

    Private Shared Function Darken(c As Color, amount As Single) As Color
        Dim r = CInt(Math.Max(0, c.R - 255 * amount))
        Dim g = CInt(Math.Max(0, c.G - 255 * amount))
        Dim b = CInt(Math.Max(0, c.B - 255 * amount))
        Return Color.FromArgb(c.A, r, g, b)
    End Function

    Private Shared Function Blend(a As Color, b As Color, t As Single) As Color
        Dim r = CInt(a.R * (1 - t) + b.R * t)
        Dim gg = CInt(a.G * (1 - t) + b.G * t)
        Dim bb = CInt(a.B * (1 - t) + b.B * t)
        Return Color.FromArgb(a.A, r, gg, bb)
    End Function

End Class
