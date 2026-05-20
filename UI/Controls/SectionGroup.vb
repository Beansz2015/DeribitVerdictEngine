' UI/Controls/SectionGroup.vb
' UI reskin P3b — lightweight grouped sub-box. Title text above a 1px
' bordered rectangle. Used inside the SETTINGS & TOOLS section (P4) for
' LOG / AUTO-RUN / TOOLS sub-cards. Dashed variant used by AUTO-RUN.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Windows.Forms

Public Class SectionGroup
    Inherits Panel

    Public Enum GroupBorderStyle
        Solid
        Dashed
    End Enum

    Private _title As String = "SECTION"
    Private _borderStyle As GroupBorderStyle = GroupBorderStyle.Solid
    Private _accentColor As Color = Theme.BORDER_CARD

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        Me.Padding = New Padding(8, 22, 8, 8)
    End Sub

    <Category("Theme")>
    Public Property Title As String
        Get
            Return _title
        End Get
        Set(v As String)
            _title = If(v, "")
            Invalidate()
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(GetType(GroupBorderStyle), "Solid")>
    Public Property BorderStyle2 As GroupBorderStyle
        Get
            Return _borderStyle
        End Get
        Set(v As GroupBorderStyle)
            _borderStyle = v
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

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit

        ' Title text (top-left, small uppercase, dim)
        Dim titleFont = Theme.FontMono(9.0F, FontStyle.Bold)
        Try
            Using brush As New SolidBrush(Theme.FG_QUATERNARY)
                g.DrawString(_title.ToUpperInvariant(), titleFont, brush, 2.0F, 2.0F)
            End Using
        Finally
            titleFont.Dispose()
        End Try

        ' Border rect starts just below the title.
        Dim rect = New RectangleF(0.5F, 18.5F, Me.Width - 1.0F, Me.Height - 19.0F)
        Using path = PaintHelpers.RoundedRect(rect, 4.0F)
            Using pen As New Pen(_accentColor, 1.0F)
                If _borderStyle = GroupBorderStyle.Dashed Then
                    pen.DashStyle = DashStyle.Custom
                    pen.DashPattern = New Single() {4.0F, 3.0F}
                End If
                g.DrawPath(pen, path)
            End Using
        End Using

        MyBase.OnPaint(e)
    End Sub

End Class
