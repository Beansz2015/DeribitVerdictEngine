' MainForm.Designer.vb  v0.23
' Compact layout:
'   - Form width reduced to 780px (tight fit for output text at Consolas 10pt)
'   - Top margin reduced: all header controls at Y=8, txtOutput starts at Y=64
'   - grpPosition, btnAnalyze, lblVerdict all on same Y baseline
'   - Radio buttons tightly packed inside grpPosition
'   - lblVerdict width fills remaining space after button
'   - Status bar anchored to bottom
'   - Form starts at 780 x 900 to show full output without scrolling

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
        Me.btnAnalyze = New System.Windows.Forms.Button()
        Me.txtOutput = New System.Windows.Forms.RichTextBox()
        Me.lblVerdict = New System.Windows.Forms.Label()
        Me.grpPosition = New System.Windows.Forms.GroupBox()
        Me.rbNone = New System.Windows.Forms.RadioButton()
        Me.rbLong = New System.Windows.Forms.RadioButton()
        Me.rbShort = New System.Windows.Forms.RadioButton()
        Me.lnkResetLog = New System.Windows.Forms.LinkLabel()
        Me.lnkCalibCheck = New System.Windows.Forms.LinkLabel()
        Me.lblLogInfo = New System.Windows.Forms.Label()
        Me.grpPosition.SuspendLayout()
        Me.SuspendLayout()

        ' -- grpPosition: compact, left-aligned, Y=8 -------------------------
        Me.grpPosition.Controls.Add(Me.rbNone)
        Me.grpPosition.Controls.Add(Me.rbLong)
        Me.grpPosition.Controls.Add(Me.rbShort)
        Me.grpPosition.Font = New System.Drawing.Font("Segoe UI", 8.5!)
        Me.grpPosition.ForeColor = System.Drawing.Color.Silver
        Me.grpPosition.Location = New System.Drawing.Point(8, 8)
        Me.grpPosition.Name = "grpPosition"
        Me.grpPosition.Size = New System.Drawing.Size(252, 42)
        Me.grpPosition.TabIndex = 3
        Me.grpPosition.TabStop = False
        Me.grpPosition.Text = "Current Position"

        ' rbNone
        Me.rbNone.AutoSize = True
        Me.rbNone.Checked = True
        Me.rbNone.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.rbNone.ForeColor = System.Drawing.Color.White
        Me.rbNone.Location = New System.Drawing.Point(6, 18)
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
        Me.rbLong.Location = New System.Drawing.Point(96, 18)
        Me.rbLong.Name = "rbLong"
        Me.rbLong.Size = New System.Drawing.Size(62, 18)
        Me.rbLong.TabIndex = 1
        Me.rbLong.Text = "In Long"
        Me.rbLong.UseVisualStyleBackColor = True

        ' rbShort
        Me.rbShort.AutoSize = True
        Me.rbShort.Font = New System.Drawing.Font("Segoe UI", 8.5!, System.Drawing.FontStyle.Bold)
        Me.rbShort.ForeColor = System.Drawing.Color.OrangeRed
        Me.rbShort.Location = New System.Drawing.Point(172, 18)
        Me.rbShort.Name = "rbShort"
        Me.rbShort.Size = New System.Drawing.Size(68, 18)
        Me.rbShort.TabIndex = 2
        Me.rbShort.Text = "In Short"
        Me.rbShort.UseVisualStyleBackColor = True

        ' -- btnAnalyze: immediately right of grpPosition, same Y baseline ---
        Me.btnAnalyze.BackColor = System.Drawing.Color.DodgerBlue
        Me.btnAnalyze.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAnalyze.Font = New System.Drawing.Font("Segoe UI", 10.0!, System.Drawing.FontStyle.Bold)
        Me.btnAnalyze.ForeColor = System.Drawing.Color.White
        Me.btnAnalyze.Location = New System.Drawing.Point(264, 8)
        Me.btnAnalyze.Name = "btnAnalyze"
        Me.btnAnalyze.Size = New System.Drawing.Size(150, 42)
        Me.btnAnalyze.TabIndex = 0
        Me.btnAnalyze.Text = "Analyze Now"
        Me.btnAnalyze.UseVisualStyleBackColor = False

        ' -- lblVerdict: fills remaining width to right edge ------------------
        Me.lblVerdict.BackColor = System.Drawing.Color.Gray
        Me.lblVerdict.Font = New System.Drawing.Font("Segoe UI", 12.0!, System.Drawing.FontStyle.Bold)
        Me.lblVerdict.ForeColor = System.Drawing.Color.White
        Me.lblVerdict.Location = New System.Drawing.Point(418, 8)
        Me.lblVerdict.Name = "lblVerdict"
        Me.lblVerdict.Size = New System.Drawing.Size(354, 42)
        Me.lblVerdict.TabIndex = 2
        Me.lblVerdict.Text = "--"
        Me.lblVerdict.TextAlign = System.Drawing.ContentAlignment.MiddleCenter

        ' -- txtOutput: starts at Y=58, Consolas 10pt, fills width/height -----
        Me.txtOutput.BackColor = System.Drawing.Color.Black
        Me.txtOutput.Font = New System.Drawing.Font("Consolas", 10.0!)
        Me.txtOutput.ForeColor = System.Drawing.Color.LightGreen
        Me.txtOutput.Location = New System.Drawing.Point(8, 58)
        Me.txtOutput.Name = "txtOutput"
        Me.txtOutput.ReadOnly = True
        Me.txtOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical
        Me.txtOutput.Size = New System.Drawing.Size(764, 820)
        Me.txtOutput.TabIndex = 1
        Me.txtOutput.Text = ""

        ' -- Status bar: lblLogInfo left, lnkCalibCheck + lnkResetLog right --
        Me.lblLogInfo.AutoSize = False
        Me.lblLogInfo.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lblLogInfo.ForeColor = System.Drawing.Color.DimGray
        Me.lblLogInfo.Location = New System.Drawing.Point(8, 882)
        Me.lblLogInfo.Name = "lblLogInfo"
        Me.lblLogInfo.Size = New System.Drawing.Size(500, 16)
        Me.lblLogInfo.TabIndex = 5
        Me.lblLogInfo.Text = "Log: 0 rows"
        Me.lblLogInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft

        Me.lnkCalibCheck.AutoSize = True
        Me.lnkCalibCheck.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lnkCalibCheck.LinkColor = System.Drawing.Color.DimGray
        Me.lnkCalibCheck.ActiveLinkColor = System.Drawing.Color.DodgerBlue
        Me.lnkCalibCheck.VisitedLinkColor = System.Drawing.Color.DimGray
        Me.lnkCalibCheck.Location = New System.Drawing.Point(608, 882)
        Me.lnkCalibCheck.Name = "lnkCalibCheck"
        Me.lnkCalibCheck.Size = New System.Drawing.Size(110, 16)
        Me.lnkCalibCheck.TabIndex = 6
        Me.lnkCalibCheck.Text = "Calibration Readiness"
        Me.lnkCalibCheck.TextAlign = System.Drawing.ContentAlignment.MiddleRight

        Me.lnkResetLog.AutoSize = True
        Me.lnkResetLog.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lnkResetLog.LinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.ActiveLinkColor = System.Drawing.Color.OrangeRed
        Me.lnkResetLog.VisitedLinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.Location = New System.Drawing.Point(728, 882)
        Me.lnkResetLog.Name = "lnkResetLog"
        Me.lnkResetLog.Size = New System.Drawing.Size(56, 16)
        Me.lnkResetLog.TabIndex = 4
        Me.lnkResetLog.Text = "Reset Log"
        Me.lnkResetLog.TextAlign = System.Drawing.ContentAlignment.MiddleRight

        ' -- MainForm ---------------------------------------------------------
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(20, 20, 20)
        Me.ClientSize = New System.Drawing.Size(780, 902)
        Me.Controls.Add(Me.grpPosition)
        Me.Controls.Add(Me.lblVerdict)
        Me.Controls.Add(Me.txtOutput)
        Me.Controls.Add(Me.btnAnalyze)
        Me.Controls.Add(Me.lblLogInfo)
        Me.Controls.Add(Me.lnkCalibCheck)
        Me.Controls.Add(Me.lnkResetLog)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.ForeColor = System.Drawing.Color.Cyan
        Me.MinimumSize = New System.Drawing.Size(780, 600)
        Me.Name = "MainForm"
        Me.Text = "Deribit Verdict Engine v0.23"
        Me.grpPosition.ResumeLayout(False)
        Me.grpPosition.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents btnAnalyze As System.Windows.Forms.Button
    Friend WithEvents txtOutput As System.Windows.Forms.RichTextBox
    Friend WithEvents lblVerdict As System.Windows.Forms.Label
    Friend WithEvents grpPosition As System.Windows.Forms.GroupBox
    Friend WithEvents rbNone As System.Windows.Forms.RadioButton
    Friend WithEvents rbLong As System.Windows.Forms.RadioButton
    Friend WithEvents rbShort As System.Windows.Forms.RadioButton
    Friend WithEvents lnkResetLog As System.Windows.Forms.LinkLabel
    Friend WithEvents lnkCalibCheck As System.Windows.Forms.LinkLabel
    Friend WithEvents lblLogInfo As System.Windows.Forms.Label

End Class
