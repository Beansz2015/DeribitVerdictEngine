' MainForm.Designer.vb
' Designer-compatible layout file — classic property assignment style.

Imports System.Drawing
Imports System.Windows.Forms

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
        Me.components = New System.ComponentModel.Container()

        Me.btnAnalyze = New Button()
        Me.txtOutput = New RichTextBox()
        Me.lblVerdict = New Label()
        Me.lblVerdictCaption = New Label()
        Me.grpPosition = New GroupBox()
        Me.rbNone = New RadioButton()
        Me.rbLong = New RadioButton()
        Me.rbShort = New RadioButton()
        Me.lblTitle = New Label()
        Me.lblInstrument = New Label()

        Me.grpPosition.SuspendLayout()
        Me.SuspendLayout()

        ' lblTitle
        Me.lblTitle.Text = "DERIBIT SCALP VERDICT ENGINE"
        Me.lblTitle.Font = New Font("Consolas", 13.0F, FontStyle.Bold)
        Me.lblTitle.ForeColor = Color.Cyan
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Location = New Point(14, 14)

        ' lblInstrument
        Me.lblInstrument.Text = "Instrument: BTC-PERPETUAL  |  Style: Momentum Hybrid  |  Timeframes: 1m / 5m"
        Me.lblInstrument.ForeColor = Color.Silver
        Me.lblInstrument.AutoSize = True
        Me.lblInstrument.Location = New Point(14, 42)

        ' grpPosition
        Me.grpPosition.Text = "Current Position"
        Me.grpPosition.ForeColor = Color.Silver
        Me.grpPosition.Location = New Point(14, 68)
        Me.grpPosition.Size = New Size(300, 48)
        Me.grpPosition.Font = New Font("Consolas", 9.0F)
        Me.grpPosition.Controls.Add(Me.rbNone)
        Me.grpPosition.Controls.Add(Me.rbLong)
        Me.grpPosition.Controls.Add(Me.rbShort)

        ' rbNone
        Me.rbNone.Text = "No Position"
        Me.rbNone.Checked = True
        Me.rbNone.ForeColor = Color.White
        Me.rbNone.Location = New Point(8, 18)
        Me.rbNone.AutoSize = True

        ' rbLong
        Me.rbLong.Text = "In Long"
        Me.rbLong.ForeColor = Color.LimeGreen
        Me.rbLong.Location = New Point(110, 18)
        Me.rbLong.AutoSize = True

        ' rbShort
        Me.rbShort.Text = "In Short"
        Me.rbShort.ForeColor = Color.Tomato
        Me.rbShort.Location = New Point(200, 18)
        Me.rbShort.AutoSize = True

        ' btnAnalyze
        Me.btnAnalyze.Text = Chr(9654) & "  Analyze Now"
        Me.btnAnalyze.Font = New Font("Consolas", 11.0F, FontStyle.Bold)
        Me.btnAnalyze.BackColor = Color.FromArgb(0, 120, 212)
        Me.btnAnalyze.ForeColor = Color.White
        Me.btnAnalyze.FlatStyle = FlatStyle.Flat
        Me.btnAnalyze.Size = New Size(220, 44)
        Me.btnAnalyze.Location = New Point(330, 60)
        Me.btnAnalyze.FlatAppearance.BorderSize = 0

        ' lblVerdictCaption
        Me.lblVerdictCaption.Text = "VERDICT"
        Me.lblVerdictCaption.ForeColor = Color.Silver
        Me.lblVerdictCaption.AutoSize = True
        Me.lblVerdictCaption.Location = New Point(570, 64)

        ' lblVerdict
        Me.lblVerdict.Text = "--"
        Me.lblVerdict.Font = New Font("Consolas", 14.0F, FontStyle.Bold)
        Me.lblVerdict.ForeColor = Color.White
        Me.lblVerdict.BackColor = Color.Gray
        Me.lblVerdict.AutoSize = False
        Me.lblVerdict.Size = New Size(270, 38)
        Me.lblVerdict.Location = New Point(570, 82)
        Me.lblVerdict.TextAlign = ContentAlignment.MiddleCenter

        ' txtOutput
        Me.txtOutput.BackColor = Color.FromArgb(12, 12, 18)
        Me.txtOutput.ForeColor = Color.FromArgb(200, 200, 200)
        Me.txtOutput.Font = New Font("Consolas", 9.0F)
        Me.txtOutput.ReadOnly = True
        Me.txtOutput.ScrollBars = RichTextBoxScrollBars.Vertical
        Me.txtOutput.BorderStyle = BorderStyle.None
        Me.txtOutput.Location = New Point(14, 122)
        Me.txtOutput.Size = New Size(856, 580)
        Me.txtOutput.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right

        ' Form
        Me.Text = "Deribit Scalp Verdict Engine -- BTC-PERPETUAL"
        Me.Size = New Size(900, 780)
        Me.MinimumSize = New Size(800, 650)
        Me.BackColor = Color.FromArgb(18, 18, 24)
        Me.ForeColor = Color.White
        Me.Font = New Font("Consolas", 9.5F)
        Me.StartPosition = FormStartPosition.CenterScreen

        Me.Controls.Add(Me.lblTitle)
        Me.Controls.Add(Me.lblInstrument)
        Me.Controls.Add(Me.grpPosition)
        Me.Controls.Add(Me.btnAnalyze)
        Me.Controls.Add(Me.lblVerdictCaption)
        Me.Controls.Add(Me.lblVerdict)
        Me.Controls.Add(Me.txtOutput)

        Me.grpPosition.ResumeLayout(False)
        Me.grpPosition.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then components.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
