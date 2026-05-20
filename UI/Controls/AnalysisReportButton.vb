' UI/Controls/AnalysisReportButton.vb
' UI reskin P3a — Solid FlatButton with a persistent amber glow halo
' beneath it. Approximates the design's CSS box-shadow at
' "0 0 18px #f59e0b50".
'
' Optional trailing arrow → renders right-aligned in the ink colour.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class AnalysisReportButton
    Inherits FlatButton

    Private _trailingArrow As Boolean = True

    Public Sub New()
        MyBase.New()
        Me.Variant = FlatButton.ButtonVariant.Solid
        Me.AccentColor = Theme.ACC_CTA
        Me.InkColor = Theme.FG_INK
        Me.IconText = Char.ConvertFromUtf32(&H1F4CA)   ' 📊
        Me.Font = Theme.FontMono(12.0F, FontStyle.Bold)
        Me.CornerRadius = 4.0F
        Me.Height = 44
    End Sub

    <Category("Theme")>
    <DefaultValue(True)>
    Public Property TrailingArrow As Boolean
        Get
            Return _trailingArrow
        End Get
        Set(v As Boolean)
            _trailingArrow = v
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(pevent As PaintEventArgs)
        ' Paint glow halo into the parent-relative client rect before the
        ' button body so the body sits on top of the halo.
        Dim g = pevent.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        Dim rect = New RectangleF(0.5F, 0.5F, Me.Width - 1.0F, Me.Height - 1.0F)
        PaintHelpers.DrawGlow(g, rect, Me.CornerRadius,
                              Theme.ACC_CTA, intensity:=0.4F, spread:=12.0F)

        MyBase.OnPaint(pevent)

        If _trailingArrow Then
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
            Dim arrow = "→"
            Dim font = Theme.FontMono(13.0F, FontStyle.Bold)
            Try
                Dim sz = g.MeasureString(arrow, font)
                Using brush As New SolidBrush(Me.InkColor)
                    g.DrawString(arrow, font, brush,
                                 CSng(Me.Width) - sz.Width - 14.0F,
                                 (CSng(Me.Height) - sz.Height) / 2.0F)
                End Using
            Finally
                font.Dispose()
            End Try
        End If
    End Sub

End Class
