' UI/Controls/Helpers/PaintHelpers.vb
' UI reskin P3 — shared paint utilities used by RoundedCardPanel,
' ScoreArcGauge, FlatButton, AnalysisReportButton and others.
'
' Public surface:
'   RoundedRect(bounds, radius)  -> GraphicsPath (caller disposes)
'   DrawGlow(g, bounds, radius, glowColour, intensity, spread)
'   ArcPath(cx, cy, r, startAngle, sweepAngle) -> GraphicsPath
'
' Notes:
'   - Angles are degrees. 0° = 3 o'clock, positive = clockwise.
'   - DrawGlow is a cheap "blur" approximation: 4 concentric outset
'     rounded rects at decaying alpha. Good enough for the dark base.

Imports System.Drawing
Imports System.Drawing.Drawing2D

Public Module PaintHelpers

    Public Function RoundedRect(bounds As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = radius * 2.0F
        If d <= 0 Then
            path.AddRectangle(bounds)
            Return path
        End If
        ' Clamp diameter to the shorter side so we never get a path that
        ' folds in on itself for narrow rectangles.
        If d > bounds.Width Then d = bounds.Width
        If d > bounds.Height Then d = bounds.Height

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90)
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90)
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90)
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Public Sub DrawGlow(g As Graphics, bounds As RectangleF, radius As Single,
                        glowColour As Color, intensity As Single, spread As Single)
        ' 4 concentric outset rounded rects, decaying alpha. Intensity ∈ [0,1]
        ' multiplies the base alpha; spread (px) is the total outset.
        Dim steps = 4
        Dim stepPx = spread / CSng(steps)
        Dim baseAlphas() As Single = {1.0F, 0.6F, 0.35F, 0.2F}
        For i = steps - 1 To 0 Step -1
            Dim outset = stepPx * CSng(i + 1)
            Dim a = CInt(Math.Round(glowColour.A * baseAlphas(i) * intensity))
            If a <= 0 Then Continue For
            If a > 255 Then a = 255
            Dim ringColour = Color.FromArgb(a, glowColour.R, glowColour.G, glowColour.B)
            Dim rect = New RectangleF(bounds.X - outset, bounds.Y - outset,
                                      bounds.Width + outset * 2, bounds.Height + outset * 2)
            Using path = RoundedRect(rect, radius + outset)
                Using brush As New SolidBrush(ringColour)
                    g.FillPath(brush, path)
                End Using
            End Using
        Next
    End Sub

    Public Function ArcPath(cx As Single, cy As Single, r As Single,
                            startAngle As Single, sweepAngle As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        path.AddArc(cx - r, cy - r, r * 2, r * 2, startAngle, sweepAngle)
        Return path
    End Function

End Module
