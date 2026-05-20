' UI/Controls/Pass2cBadge.vb
' UI reskin P3b — Pass 2c regime-alignment outcome badge.
'
'   ALIGNED     "ALIGNED ↑"    ACC_STRONG_LONG
'   CONFLICT    "CONFLICT ↓"   ACC_SHORT
'   SUPPRESSED  "SUPPRESSED"   ACC_NEUTRAL

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class Pass2cBadge
    Inherits Label

    Public Enum Pass2cOutcomeKind
        ALIGNED
        CONFLICT
        SUPPRESSED
    End Enum

    Private _outcome As Pass2cOutcomeKind = Pass2cOutcomeKind.SUPPRESSED

    Public Sub New()
        Me.AutoSize = True
        Me.BackColor = Color.Transparent
        Me.Font = Theme.FontMono(11.0F, FontStyle.Bold)
        ApplyOutcome()
    End Sub

    <Category("Theme")>
    <DefaultValue(GetType(Pass2cOutcomeKind), "SUPPRESSED")>
    Public Property Outcome As Pass2cOutcomeKind
        Get
            Return _outcome
        End Get
        Set(v As Pass2cOutcomeKind)
            _outcome = v
            ApplyOutcome()
        End Set
    End Property

    Private Sub ApplyOutcome()
        Select Case _outcome
            Case Pass2cOutcomeKind.ALIGNED
                Me.Text = "ALIGNED ↑"
                Me.ForeColor = Theme.ACC_STRONG_LONG
            Case Pass2cOutcomeKind.CONFLICT
                Me.Text = "CONFLICT ↓"
                Me.ForeColor = Theme.ACC_SHORT
            Case Else
                Me.Text = "SUPPRESSED"
                Me.ForeColor = Theme.ACC_NEUTRAL
        End Select
    End Sub

End Class
