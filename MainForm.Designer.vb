' MainForm.Designer.vb  v0.21

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
        Me.lblLogInfo = New System.Windows.Forms.Label()
        Me.grpPosition.SuspendLayout()
        Me.SuspendLayout()
        '
        ' btnAnalyze
        '
        Me.btnAnalyze.BackColor = System.Drawing.Color.DodgerBlue
        Me.btnAnalyze.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnAnalyze.Font = New System.Drawing.Font("Segoe UI", 11.0!, System.Drawing.FontStyle.Bold)
        Me.btnAnalyze.ForeColor = System.Drawing.Color.White
        Me.btnAnalyze.Location = New System.Drawing.Point(370, 36)
        Me.btnAnalyze.Name = "btnAnalyze"
        Me.btnAnalyze.Size = New System.Drawing.Size(200, 40)
        Me.btnAnalyze.TabIndex = 0
        Me.btnAnalyze.Text = "Analyze Now"
        Me.btnAnalyze.UseVisualStyleBackColor = False
        '
        ' txtOutput  -- bottom trimmed to leave room for log status bar
        '
        Me.txtOutput.BackColor = System.Drawing.Color.Black
        Me.txtOutput.Font = New System.Drawing.Font("Consolas", 11.0!)
        Me.txtOutput.ForeColor = System.Drawing.Color.LightGreen
        Me.txtOutput.Location = New System.Drawing.Point(12, 100)
        Me.txtOutput.Name = "txtOutput"
        Me.txtOutput.ReadOnly = True
        Me.txtOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical
        Me.txtOutput.Size = New System.Drawing.Size(1136, 880)
        Me.txtOutput.TabIndex = 1
        Me.txtOutput.Text = ""
        '
        ' lblVerdict
        '
        Me.lblVerdict.BackColor = System.Drawing.Color.Gray
        Me.lblVerdict.Font = New System.Drawing.Font("Segoe UI", 13.0!, System.Drawing.FontStyle.Bold)
        Me.lblVerdict.ForeColor = System.Drawing.Color.White
        Me.lblVerdict.Location = New System.Drawing.Point(590, 36)
        Me.lblVerdict.Name = "lblVerdict"
        Me.lblVerdict.Size = New System.Drawing.Size(558, 40)
        Me.lblVerdict.TabIndex = 2
        Me.lblVerdict.Text = "-- "
        Me.lblVerdict.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        ' grpPosition
        '
        Me.grpPosition.Controls.Add(Me.rbNone)
        Me.grpPosition.Controls.Add(Me.rbLong)
        Me.grpPosition.Controls.Add(Me.rbShort)
        Me.grpPosition.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.grpPosition.ForeColor = System.Drawing.Color.Silver
        Me.grpPosition.Location = New System.Drawing.Point(12, 36)
        Me.grpPosition.Name = "grpPosition"
        Me.grpPosition.Size = New System.Drawing.Size(340, 50)
        Me.grpPosition.TabIndex = 3
        Me.grpPosition.TabStop = False
        Me.grpPosition.Text = "Current Position"
        '
        ' rbNone
        '
        Me.rbNone.AutoSize = True
        Me.rbNone.Checked = True
        Me.rbNone.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.rbNone.ForeColor = System.Drawing.Color.White
        Me.rbNone.Location = New System.Drawing.Point(10, 20)
        Me.rbNone.Name = "rbNone"
        Me.rbNone.Size = New System.Drawing.Size(90, 19)
        Me.rbNone.TabIndex = 0
        Me.rbNone.TabStop = True
        Me.rbNone.Text = "No Position"
        Me.rbNone.UseVisualStyleBackColor = True
        '
        ' rbLong
        '
        Me.rbLong.AutoSize = True
        Me.rbLong.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.rbLong.ForeColor = System.Drawing.Color.Lime
        Me.rbLong.Location = New System.Drawing.Point(120, 20)
        Me.rbLong.Name = "rbLong"
        Me.rbLong.Size = New System.Drawing.Size(70, 19)
        Me.rbLong.TabIndex = 1
        Me.rbLong.Text = "In Long"
        Me.rbLong.UseVisualStyleBackColor = True
        '
        ' rbShort
        '
        Me.rbShort.AutoSize = True
        Me.rbShort.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.rbShort.ForeColor = System.Drawing.Color.OrangeRed
        Me.rbShort.Location = New System.Drawing.Point(210, 20)
        Me.rbShort.Name = "rbShort"
        Me.rbShort.Size = New System.Drawing.Size(75, 19)
        Me.rbShort.TabIndex = 2
        Me.rbShort.Text = "In Short"
        Me.rbShort.UseVisualStyleBackColor = True
        '
        ' lblLogInfo  -- row count + file path, left side of status bar
        '
        Me.lblLogInfo.AutoSize = False
        Me.lblLogInfo.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lblLogInfo.ForeColor = System.Drawing.Color.DimGray
        Me.lblLogInfo.Location = New System.Drawing.Point(12, 1006)
        Me.lblLogInfo.Name = "lblLogInfo"
        Me.lblLogInfo.Size = New System.Drawing.Size(900, 18)
        Me.lblLogInfo.TabIndex = 5
        Me.lblLogInfo.Text = "Log: 0 rows"
        Me.lblLogInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        ' lnkResetLog  -- text-link on the right side of status bar
        '
        Me.lnkResetLog.AutoSize = True
        Me.lnkResetLog.Font = New System.Drawing.Font("Segoe UI", 8.0!)
        Me.lnkResetLog.LinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.ActiveLinkColor = System.Drawing.Color.OrangeRed
        Me.lnkResetLog.VisitedLinkColor = System.Drawing.Color.DimGray
        Me.lnkResetLog.Location = New System.Drawing.Point(1080, 1006)
        Me.lnkResetLog.Name = "lnkResetLog"
        Me.lnkResetLog.Size = New System.Drawing.Size(60, 18)
        Me.lnkResetLog.TabIndex = 4
        Me.lnkResetLog.Text = "Reset Log"
        Me.lnkResetLog.TextAlign = System.Drawing.ContentAlignment.MiddleRight
        '
        ' MainForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(20, Byte), Integer), CType(CType(20, Byte), Integer), CType(CType(20, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(1160, 1030)
        Me.Controls.Add(Me.grpPosition)
        Me.Controls.Add(Me.lblVerdict)
        Me.Controls.Add(Me.txtOutput)
        Me.Controls.Add(Me.btnAnalyze)
        Me.Controls.Add(Me.lblLogInfo)
        Me.Controls.Add(Me.lnkResetLog)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.ForeColor = System.Drawing.Color.Cyan
        Me.MinimumSize = New System.Drawing.Size(800, 700)
        Me.Name = "MainForm"
        Me.Text = "Deribit Scalp Verdict Engine -- BTC-PERPETUAL"
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
    Friend WithEvents lblLogInfo As System.Windows.Forms.Label

End Class
