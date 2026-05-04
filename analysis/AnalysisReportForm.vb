' analysis/AnalysisReportForm.vb
' Thin WinForms viewer for the analysis markdown report.
' This is the ONLY file in analysis/ that references System.Windows.Forms.
' All logic lives in the host-agnostic classes; this is a display shell only.

Imports System.Drawing
Imports System.Windows.Forms

Public Class AnalysisReportForm
    Inherits Form

    Private ReadOnly _rtb  As RichTextBox
    Private ReadOnly _lbl  As Label

    Public Sub New(markdownText As String, filePath As String)
        Me.Text            = "Analysis Report"
        Me.Size            = New Size(1000, 750)
        Me.MinimumSize     = New Size(700, 500)
        Me.StartPosition   = FormStartPosition.CenterScreen
        Me.BackColor       = Color.FromArgb(20, 20, 20)

        _rtb = New RichTextBox() With {
            .Dock          = DockStyle.Fill,
            .ReadOnly      = True,
            .BackColor     = Color.FromArgb(20, 20, 20),
            .ForeColor     = Color.FromArgb(200, 200, 200),
            .Font          = New Font("Consolas", 9.5!),
            .ScrollBars    = RichTextBoxScrollBars.Vertical,
            .WordWrap      = False,
            .BorderStyle   = BorderStyle.None,
            .Text          = markdownText
        }

        Dim pathLbl As String = If(String.IsNullOrEmpty(filePath), "", "Saved: " & filePath)
        _lbl = New Label() With {
            .Dock      = DockStyle.Bottom,
            .Height    = 20,
            .BackColor = Color.FromArgb(35, 35, 35),
            .ForeColor = Color.FromArgb(120, 120, 120),
            .Font      = New Font("Segoe UI", 8.0!),
            .Text      = pathLbl,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding   = New Padding(4, 0, 0, 0)
        }

        Me.Controls.Add(_rtb)
        Me.Controls.Add(_lbl)

        ' Position cursor at top
        _rtb.SelectionStart = 0
        _rtb.ScrollToCaret()
    End Sub

End Class
