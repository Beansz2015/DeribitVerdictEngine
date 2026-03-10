' MainForm.Designer.vb
' Auto-generated designer file — defines layout of all controls.

Partial Class MainForm
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    Friend WithEvents btnAnalyze As Button
    Friend WithEvents txtOutput As RichTextBox
    Friend WithEvents lblVerdict As Label
    Friend WithEvents lblVerdictCaption As Label
    Friend WithEvents grpPosition As GroupBox
    Friend WithEvents rbNone As RadioButton
    Friend WithEvents rbLong As RadioButton
    Friend WithEvents rbShort As RadioButton
    Friend WithEvents lblTitle As Label
    Friend WithEvents lblInstrument As Label

    Private Sub InitializeComponent()
        Me.SuspendLayout()

        ' Form
        Me.Text = "Deribit Scalp Verdict Engine — BTC-PERPETUAL"
        Me.Size = New Size(900, 780)
        Me.MinimumSize = New Size(800, 650)
        Me.BackColor = Color.FromArgb(18, 18, 24)
        Me.ForeColor = Color.White
        Me.Font = New Font("Consolas", 9.5F)
        Me.StartPosition = FormStartPosition.CenterScreen

        ' Title label
        lblTitle = New Label With {
            .Text = "DERIBIT SCALP VERDICT ENGINE",
            .Font = New Font("Consolas", 13F, FontStyle.Bold),
            .ForeColor = Color.Cyan,
            .AutoSize = True,
            .Location = New Point(14, 14)
        }

        ' Instrument label
        lblInstrument = New Label With {
            .Text = "Instrument: BTC-PERPETUAL  |  Style: Momentum Hybrid  |  Timeframes: 1m / 5m",
            .ForeColor = Color.Silver,
            .AutoSize = True,
            .Location = New Point(14, 42)
        }

        ' Position state group
        grpPosition = New GroupBox With {
            .Text = "Current Position",
            .ForeColor = Color.Silver,
            .Location = New Point(14, 68),
            .Size = New Size(300, 48),
            .Font = New Font("Consolas", 9F)
        }
        rbNone = New RadioButton With {
            .Text = "No Position", .Checked = True,
            .ForeColor = Color.White, .Location = New Point(8, 18), .AutoSize = True
        }
        rbLong = New RadioButton With {
            .Text = "In Long", .ForeColor = Color.LimeGreen,
            .Location = New Point(110, 18), .AutoSize = True
        }
        rbShort = New RadioButton With {
            .Text = "In Short", .ForeColor = Color.Tomato,
            .Location = New Point(200, 18), .AutoSize = True
        }
        grpPosition.Controls.AddRange({rbNone, rbLong, rbShort})

        ' Analyze button
        btnAnalyze = New Button With {
            .Text = "▶  Analyze Now",
            .Font = New Font("Consolas", 11F, FontStyle.Bold),
            .BackColor = Color.FromArgb(0, 120, 212),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(200, 44),
            .Location = New Point(330, 64)
        }
        btnAnalyze.FlatAppearance.BorderSize = 0

        ' Verdict label (big colored box)
        lblVerdictCaption = New Label With {
            .Text = "VERDICT",
            .ForeColor = Color.Silver,
            .AutoSize = True,
            .Location = New Point(560, 70)
        }
        lblVerdict = New Label With {
            .Text = "—",
            .Font = New Font("Consolas", 14F, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.Gray,
            .AutoSize = False,
            .Size = New Size(280, 38),
            .Location = New Point(560, 88),
            .TextAlign = ContentAlignment.MiddleCenter
        }

        ' Output text box
        txtOutput = New RichTextBox With {
            .BackColor = Color.FromArgb(12, 12, 18),
            .ForeColor = Color.FromArgb(200, 200, 200),
            .Font = New Font("Consolas", 9F),
            .ReadOnly = True,
            .ScrollBars = RichTextBoxScrollBars.Vertical,
            .BorderStyle = BorderStyle.None,
            .Location = New Point(14, 122),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or
                      AnchorStyles.Left Or AnchorStyles.Right
        }

        Me.Controls.AddRange({lblTitle, lblInstrument, grpPosition,
                               btnAnalyze, lblVerdictCaption, lblVerdict, txtOutput})
        Me.ResumeLayout(False)
        Me.PerformLayout()
        Me.Resize += Sub(s, ev) ResizeControls()
        ResizeControls()
    End Sub

    Private Sub ResizeControls()
        txtOutput.Size = New Size(Me.ClientSize.Width - 28,
                                   Me.ClientSize.Height - 134)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
