' MainForm.Designer.vb  v0.24a
' Header row layout (HDR_Y=8, HDR_H=42):
'   lblPositionTitle : X=8,   W=108, Y=8,  H=42  (MiddleLeft)
'   rbNone           : X=120, Y=20   (vertically centred in 42px row)
'   rbLong           : X=210, Y=10   (top of stack)
'   rbShort          : X=210, Y=30   (bottom of stack, 2px gap)
'   btnAnalyze       : X=286, W=140  (8px gap after stack ends ~278)
'   lblVerdict       : X=430, fills to right edge => W=222 at 660px form
'
' Auto-run row layout (AR_Y=54, AR_H=20):
'   lblAutoRun       : X=8,   W=64   label "Auto-run:"
'   nudMinutes       : X=76,  W=42   0-60
'   lblMin           : X=122, W=24   "min"
'   nudSeconds       : X=150, W=42   0-59
'   lblSec           : X=196, W=28   "sec"
'   rbSingle         : X=228, W=58   "Single"
'   rbRepeat         : X=290, W=62   "Repeat"  (default checked)
'   btnStartStop     : X=356, W=70   "▶ Start" / "■ Stop"
'
' txtOutput.Location.Y bumped from 56 -> 76 to accommodate auto-run row.
' ClientSize.Height bumped by 20 to 922 to preserve output area.

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class MainForm
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.btnAnalyze    = New System.Windows.Forms.Button()
        Me.txtOutput     = New System.Windows.Forms.RichTextBox()
        Me.lblVerdict    = New System.Windows.Forms.Label()
        Me.lblPositionTitle = New System.Windows.Forms.Label()
        Me.rbNone        = New System.Windows.Forms.RadioButton()
        Me.rbLong        = New System.Windows.Forms.RadioButton()
        Me.rbShort       = New System.Windows.Forms.RadioButton()
        Me.lnkResetLog   = New System.Windows.Forms.LinkLabel()
        Me.lnkCalibCheck = New System.Windows.Forms.LinkLabel()
        Me.lblLogInfo    = New System.Windows.Forms.Label()
        Me.lblAutoRun    = New System.Windows.Forms.Label()
        Me.nudMinutes    = New System.Windows.Forms.NumericUpDown()
        Me.lblMin        = New System.Windows.Forms.Label()
        Me.nudSeconds    = New System.Windows.Forms.NumericUpDown()
        Me.lblSec        = New System.Windows.Forms.Label()
        Me.rbSingle      = New System.Windows.Forms.RadioButton()
        Me.rbRepeat      = New System.Windows.Forms.RadioButton()
        Me.btnStartStop  = New System.Windows.Forms.Button()
        Me.lblCountdown  = New System.Windows.Forms.Label()
        CType(Me.nudMinutes, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudSeconds, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()

        ' ----------------------------------------------------------------
        ' Row 1: position + analyze + verdict
        ' ----------------------------------------------------------------

        ' lblPositionTitle
        Me.lblPositionTitle.AutoSize = False
        Me.lblPositionTitle.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.lblPositionTitle.ForeColor = System.Drawing.Color.Silver
        Me.lblPositionTitle.Location = New System.Drawing.Point(8, 8)
        Me.lblPositionTitle.Name = "lblPositionTitle"
        Me.lblPositionTitle.Size = New System.Drawing.Size(108, 42)
        Me.lblPositionTitle.TabIndex = 7
        Me.lblPositionTitle.Text = "Current Position:"
        Me.lblPositionTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        ' rbNone
        Me.rbNone.AutoSize = True
        Me.rbNone.Checked = True
        Me.rbNone.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.rbNone.ForeColor = System.Drawing.Color.White
        Me.rbNone.Location = New System.Drawing.Point(120, 20)
        Me.rbNone.Name = "rbNone"
        Me.rbNone.Size = New System.Drawing.Size(82, 18)
        Me.rbNone.TabIndex = 0
        Me.rbNone.TabStop = True
        Me.rbNone.Text = "No Position"
        Me.rbNone.UseVisualStyleBackColor = True

        ' rbLong
        Me.rbLong.AutoSize = True
        Me.rbLong.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.rbLong.ForeColor = System.Drawing.Color.Lime
        Me.rbLong.Location = New System.Drawing.Point(210, 10)
        Me.rbLong.Name = "rbLong"
        Me.rbLong.Size = New System.Drawing.Size(62, 18)
        Me.rbLong.TabIndex = 1
        Me.rbLong.Text = "In Long"
        Me.rbLong.UseVisualStyleBackColor = True

        ' rbShort
        Me.rbShort.AutoSize = True
        Me.rbShort.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.rbShort.ForeColor = System.Drawing.Color.OrangeRed
        Me.rbShort.Location = New System.Drawing.Point(210, 30)
        Me.rbShort.Name = "rbShort"
        Me.rbShort.Size = New System.Drawing.Size(68, 18)
        Me.rbShort.TabIndex = 2
        Me.rbShort.Text = "In Short"
        Me.rbShort.UseVisualStyleBackColor = True

        ' btnAnalyze
        Me.btnAnalyze.BackColor = System.Drawing.Color.DodgerBlue
        Me.btnAnalyze.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAnalyze.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnAnalyze.ForeColor = System.Drawing.Color.White
        Me.btnAnalyze.Location = New System.Drawing.Point(286, 8)
        Me.btnAnalyze.Name = "btnAnalyze"
        Me.btnAnalyze.Size = New System.Drawing.Size(140, 42)
        Me.btnAnalyze.TabIndex = 3
        Me.btnAnalyze.Text = "Analyze Now"
        Me.btnAnalyze.UseVisualStyleBackColor = False

        ' lblVerdict
        Me.lblVerdict.BackColor = System.Drawing.Color.Gray
        Me.lblVerdict.Font = New System.Drawing.Font("Segoe UI", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblVerdict.ForeColor = System.Drawing.Color.White
        Me.lblVerdict.Location = New System.Drawing.Point(430, 8)
        Me.lblVerdict.Name = "lblVerdict"
        Me.lblVerdict.Size = New System.Drawing.Size(222, 42)
        Me.lblVerdict.TabIndex = 4
        Me.lblVerdict.Text = "--"
        Me.lblVerdict.TextAlign = System.Drawing.ContentAlignment.MiddleCenter

        ' ----------------------------------------------------------------
        ' Row 2: auto-run controls  (Y=54, H=20)
        ' ----------------------------------------------------------------

        ' lblAutoRun
        Me.lblAutoRun.AutoSize = False
        Me.lblAutoRun.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.lblAutoRun.ForeColor = System.Drawing.Color.Silver
        Me.lblAutoRun.Location = New System.Drawing.Point(8, 54)
        Me.lblAutoRun.Name = "lblAutoRun"
        Me.lblAutoRun.Size = New System.Drawing.Size(64, 20)
        Me.lblAutoRun.TabIndex = 20
        Me.lblAutoRun.Text = "Auto-run:"
        Me.lblAutoRun.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        ' nudMinutes  (0-60, default 1)
        Me.nudMinutes.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.nudMinutes.Location = New System.Drawing.Point(76, 54)
        Me.nudMinutes.Maximum = 60
        Me.nudMinutes.Minimum = 0
        Me.nudMinutes.Name = "nudMinutes"
        Me.nudMinutes.Size = New System.Drawing.Size(42, 20)
        Me.nudMinutes.TabIndex = 21
        Me.nudMinutes.Value = 1
        Me.nudMinutes.BackColor = System.Drawing.Color.FromArgb(40, 40, 40)
        Me.nudMinutes.ForeColor = System.Drawing.Color.White

        ' lblMin
        Me.lblMin.AutoSize = True
        Me.lblMin.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.lblMin.ForeColor = System.Drawing.Color.Silver
        Me.lblMin.Location = New System.Drawing.Point(122, 56)
        Me.lblMin.Name = "lblMin"
        Me.lblMin.Size = New System.Drawing.Size(24, 16)
        Me.lblMin.TabIndex = 22
        Me.lblMin.Text = "min"

        ' nudSeconds  (0-59, default 0)
        Me.nudSeconds.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.nudSeconds.Location = New System.Drawing.Point(150, 54)
        Me.nudSeconds.Maximum = 59
        Me.nudSeconds.Minimum = 0
        Me.nudSeconds.Name = "nudSeconds"
        Me.nudSeconds.Size = New System.Drawing.Size(42, 20)
        Me.nudSeconds.TabIndex = 23
        Me.nudSeconds.Value = 0
        Me.nudSeconds.BackColor = System.Drawing.Color.FromArgb(40, 40, 40)
        Me.nudSeconds.ForeColor = System.Drawing.Color.White

        ' lblSec
        Me.lblSec.AutoSize = True
        Me.lblSec.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.lblSec.ForeColor = System.Drawing.Color.Silver
        Me.lblSec.Location = New System.Drawing.Point(196, 56)
        Me.lblSec.Name = "lblSec"
        Me.lblSec.Size = New System.Drawing.Size(28, 16)
        Me.lblSec.TabIndex = 24
        Me.lblSec.Text = "sec"

        ' rbSingle
        Me.rbSingle.AutoSize = True
        Me.rbSingle.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.rbSingle.ForeColor = System.Drawing.Color.Silver
        Me.rbSingle.Location = New System.Drawing.Point(228, 56)
        Me.rbSingle.Name = "rbSingle"
        Me.rbSingle.Size = New System.Drawing.Size(58, 18)
        Me.rbSingle.TabIndex = 25
        Me.rbSingle.Text = "Single"
        Me.rbSingle.UseVisualStyleBackColor = True

        ' rbRepeat  (default checked)
        Me.rbRepeat.AutoSize = True
        Me.rbRepeat.Checked = True
        Me.rbRepeat.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.rbRepeat.ForeColor = System.Drawing.Color.Silver
        Me.rbRepeat.Location = New System.Drawing.Point(290, 56)
        Me.rbRepeat.Name = "rbRepeat"
        Me.rbRepeat.Size = New System.Drawing.Size(62, 18)
        Me.rbRepeat.TabIndex = 26
        Me.rbRepeat.TabStop = True
        Me.rbRepeat.Text = "Repeat"
        Me.rbRepeat.UseVisualStyleBackColor = True

        ' btnStartStop -- use ChrW() for Unicode codepoints > 255
        Me.btnStartStop.BackColor = System.Drawing.Color.FromArgb(0, 140, 60)
        Me.btnStartStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnStartStop.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.btnStartStop.ForeColor = System.Drawing.Color.White
        Me.btnStartStop.Location = New System.Drawing.Point(356, 53)
        Me.btnStartStop.Name = "btnStartStop"
        Me.btnStartStop.Size = New System.Drawing.Size(70, 22)
        Me.btnStartStop.TabIndex = 27
        Me.btnStartStop.Text = ChrW(9654) & " Start"
        Me.btnStartStop.UseVisualStyleBackColor = False

        ' ----------------------------------------------------------------
        ' Output area  (Y=76 -- shifted down 20px for auto-run row)
        ' ----------------------------------------------------------------

        ' txtOutput
        Me.txtOutput.BackColor = System.Drawing.Color.Black
        Me.txtOutput.Font = New System.Drawing.Font("Consolas", 10.0!)
        Me.txtOutput.ForeColor = System.Drawing.Color.LightGreen
        Me.txtOutput.Location = New System.Drawing.Point(8, 76)
        Me.txtOutput.Name = "txtOutput"
        Me.txtOutput.ReadOnly = True
        Me.txtOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical
        Me.txtOutput.Size = New System.Drawing.Size(644, 820)
        Me.txtOutput.TabIndex = 5
        Me.txtOutput.Text = ""

        ' ----------------------------------------------------------------
        ' Status bar  (Y=900)
        ' ----------------------------------------------------------------

        Me.lblLogInfo.AutoSize = False
        Me.lblLogInfo.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lblLogInfo.ForeColor = System.Drawing.Color.DimGray
        Me.lblLogInfo.Location = New System.Drawing.Point(8, 902)
        Me.lblLogInfo.Name = "lblLogInfo"
        Me.lblLogInfo.Size = New System.Drawing.Size(240, 16)
        Me.lblLogInfo.TabIndex = 8
        Me.lblLogInfo.Text = "Log: 0 rows"
        Me.lblLogInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        ' lblCountdown
        Me.lblCountdown.AutoSize = False
        Me.lblCountdown.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lblCountdown.ForeColor = System.Drawing.Color.DimGray
        Me.lblCountdown.Location = New System.Drawing.Point(252, 902)
        Me.lblCountdown.Name = "lblCountdown"
        Me.lblCountdown.Size = New System.Drawing.Size(180, 16)
        Me.lblCountdown.TabIndex = 28
        Me.lblCountdown.Text = "Auto-run: OFF"
        Me.lblCountdown.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        Me.lnkCalibCheck.AutoSize = True
        Me.lnkCalibCheck.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lnkCalibCheck.LinkColor = System.Drawing.Color.DimGray
        Me.lnkCalibCheck.ActiveLinkColor = System.Drawing.Color.DodgerBlue
        Me.lnkCalibCheck.VisitedLinkColor = System.Drawing.Color.DimGray
        Me.lnkCalibCheck.Location = New System.Drawing.Point(430, 902)
        Me.lnkCalibCheck.Name = "lnkCalibCheck"
        Me.lnkCalibCheck.Size = New System.Drawing.Size(120, 16)
        Me.lnkCalibCheck.TabIndex = 9
        Me.lnkCalibCheck.Text = "Calibration Readiness"
        Me.lnkCalibCheck.TextAlign = System.Drawing.ContentAlignment.MiddleRight

        Me.lnkResetLog.AutoSize = True
        Me.lnkResetLog.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lnkResetLog.LinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.ActiveLinkColor = System.Drawing.Color.OrangeRed
        Me.lnkResetLog.VisitedLinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.Location = New System.Drawing.Point(590, 902)
        Me.lnkResetLog.Name = "lnkResetLog"
        Me.lnkResetLog.Size = New System.Drawing.Size(56, 16)
        Me.lnkResetLog.TabIndex = 6
        Me.lnkResetLog.Text = "Reset Log"
        Me.lnkResetLog.TextAlign = System.Drawing.ContentAlignment.MiddleRight

        ' ----------------------------------------------------------------
        ' MainForm
        ' ----------------------------------------------------------------
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(20, 20, 20)
        Me.ClientSize = New System.Drawing.Size(660, 922)
        Me.Controls.Add(Me.lblPositionTitle)
        Me.Controls.Add(Me.rbNone)
        Me.Controls.Add(Me.rbLong)
        Me.Controls.Add(Me.rbShort)
        Me.Controls.Add(Me.btnAnalyze)
        Me.Controls.Add(Me.lblVerdict)
        Me.Controls.Add(Me.lblAutoRun)
        Me.Controls.Add(Me.nudMinutes)
        Me.Controls.Add(Me.lblMin)
        Me.Controls.Add(Me.nudSeconds)
        Me.Controls.Add(Me.lblSec)
        Me.Controls.Add(Me.rbSingle)
        Me.Controls.Add(Me.rbRepeat)
        Me.Controls.Add(Me.btnStartStop)
        Me.Controls.Add(Me.txtOutput)
        Me.Controls.Add(Me.lblLogInfo)
        Me.Controls.Add(Me.lblCountdown)
        Me.Controls.Add(Me.lnkCalibCheck)
        Me.Controls.Add(Me.lnkResetLog)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.ForeColor = System.Drawing.Color.Cyan
        Me.MinimumSize = New System.Drawing.Size(660, 500)
        Me.Name = "MainForm"
        Me.Text = "Deribit Verdict Engine v0.24"
        Me.ResumeLayout(False)
        Me.PerformLayout()
        CType(Me.nudMinutes, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudSeconds, System.ComponentModel.ISupportInitialize).EndInit()
    End Sub

    Friend WithEvents btnAnalyze       As System.Windows.Forms.Button
    Friend WithEvents txtOutput        As System.Windows.Forms.RichTextBox
    Friend WithEvents lblVerdict       As System.Windows.Forms.Label
    Friend WithEvents lblPositionTitle As System.Windows.Forms.Label
    Friend WithEvents rbNone           As System.Windows.Forms.RadioButton
    Friend WithEvents rbLong           As System.Windows.Forms.RadioButton
    Friend WithEvents rbShort          As System.Windows.Forms.RadioButton
    Friend WithEvents lnkResetLog      As System.Windows.Forms.LinkLabel
    Friend WithEvents lnkCalibCheck    As System.Windows.Forms.LinkLabel
    Friend WithEvents lblLogInfo        As System.Windows.Forms.Label
    Friend WithEvents lblAutoRun       As System.Windows.Forms.Label
    Friend WithEvents nudMinutes       As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblMin           As System.Windows.Forms.Label
    Friend WithEvents nudSeconds       As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblSec           As System.Windows.Forms.Label
    Friend WithEvents rbSingle         As System.Windows.Forms.RadioButton
    Friend WithEvents rbRepeat         As System.Windows.Forms.RadioButton
    Friend WithEvents btnStartStop     As System.Windows.Forms.Button
    Friend WithEvents lblCountdown     As System.Windows.Forms.Label

End Class
