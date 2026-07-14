' UI/Controls/TapeStripLabel.vb
' [P4 #5 wire-in follow-up] The LIVE TAPE strip's data label (MainForm_LiveStrip).
' A single-line, left-aligned Label that owner-draws its text so the trailing
' aggressor-velocity BURST segment ("... 5.1× BURST↑" / "BURST↓") renders in an
' accent colour while the rest of the strip stays dim. A plain Label has one
' ForeColor, so highlighting just the burst word needs the two-run draw here.
'
' Design note: the strip is deliberately NEUTRAL (never the verdict colour ramp,
' so it reads as a readout not a call — live-microstructure-strip-proposal §4.4).
' The burst is highlighted in the amber ATTENTION accent (ACC_WARN), NOT the
' green/red directional ramp; the ↑/↓ arrow still carries the direction.

Imports System.Drawing
Imports System.Windows.Forms

Public Class TapeStripLabel
    Inherits Label

    ' Colour for the BURST segment. Amber attention accent by default; the ↑/↓
    ' arrow carries direction so this stays non-directional (respects the strip's
    ' "not a verdict call" intent). Color.Empty ⇒ no highlight (draw all dim).
    Public Property BurstColor As Color = Theme.ACC_WARN

    Public Sub New()
        ' Reduce repaint flicker without taking over the (transparent) background —
        ' the base Label still composites the parent behind us via OnPaintBackground.
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        ' Owner-draw the line ourselves. We intentionally do NOT call MyBase.OnPaint
        ' (that would draw the default single-colour text underneath). The transparent
        ' background is still handled by the base OnPaintBackground.
        Dim g = e.Graphics
        Dim flags As TextFormatFlags =
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or
            TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding

        Dim full As String = If(Text, "")
        ' The burst segment is always the trailing "BURST..." run; match the ASCII
        ' "BURST" (encoding-safe vs the ↑/↓) and highlight from there to the end.
        Dim idx As Integer = full.IndexOf("BURST", StringComparison.Ordinal)
        If idx < 0 OrElse BurstColor.IsEmpty Then
            TextRenderer.DrawText(g, full, Font, ClientRectangle, ForeColor, flags)
            Return
        End If

        Dim prefix As String = full.Substring(0, idx)
        Dim tail As String = full.Substring(idx)

        ' Draw the dim prefix, then the burst word in the accent colour starting
        ' exactly where the prefix ends (NoPadding keeps the seam tight; the strip
        ' font is monospace so the measured offset is exact).
        TextRenderer.DrawText(g, prefix, Font, ClientRectangle, ForeColor, flags)
        Dim pw As Integer = TextRenderer.MeasureText(
            g, prefix, Font, New Size(Integer.MaxValue, ClientRectangle.Height),
            TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding).Width
        Dim tailRect As New Rectangle(
            ClientRectangle.X + pw, ClientRectangle.Y,
            ClientRectangle.Width - pw, ClientRectangle.Height)
        TextRenderer.DrawText(g, tail, Font, tailRect, BurstColor, flags)
    End Sub

End Class
