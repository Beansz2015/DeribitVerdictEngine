' UI/Controls/OiCvdBadge.vb
' UI reskin P3b — OI × CVD cross-confirm outcome badge. Dot prefix +
' uppercase outcome label, semantic colour by state.
'
'   CONFIRMED_LONG  ● CONFIRMED LONG    ACC_STRONG_LONG
'   CONFIRMED_SHORT ● CONFIRMED SHORT   ACC_SHORT
'   CONFLICT        ⚠ CONFLICT          ACC_WARN
'   NEUTRAL         ○ NEUTRAL           ACC_NEUTRAL

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class OiCvdBadge
    Inherits Label

    Public Enum OiCvdOutcomeKind
        CONFIRMED_LONG
        CONFIRMED_SHORT
        CONFLICT
        NEUTRAL
    End Enum

    Private _outcome As OiCvdOutcomeKind = OiCvdOutcomeKind.NEUTRAL

    Public Sub New()
        Me.AutoSize = True
        Me.BackColor = Color.Transparent
        Me.Font = Theme.FontMono(11.0F, FontStyle.Bold)
        ApplyOutcome()
    End Sub

    <Category("Theme")>
    <DefaultValue(GetType(OiCvdOutcomeKind), "NEUTRAL")>
    Public Property Outcome As OiCvdOutcomeKind
        Get
            Return _outcome
        End Get
        Set(v As OiCvdOutcomeKind)
            _outcome = v
            ApplyOutcome()
        End Set
    End Property

    Private Sub ApplyOutcome()
        Dim dot As String
        Dim displayName As String
        Dim colour As Color

        Select Case _outcome
            Case OiCvdOutcomeKind.CONFIRMED_LONG
                dot = "●"
                displayName = "CONFIRMED LONG"
                colour = Theme.ACC_STRONG_LONG
            Case OiCvdOutcomeKind.CONFIRMED_SHORT
                dot = "●"
                displayName = "CONFIRMED SHORT"
                colour = Theme.ACC_SHORT
            Case OiCvdOutcomeKind.CONFLICT
                dot = "⚠"
                displayName = "CONFLICT"
                colour = Theme.ACC_WARN
            Case Else
                dot = "○"
                displayName = "NEUTRAL"
                colour = Theme.ACC_NEUTRAL
        End Select

        Me.ForeColor = colour
        Me.Text = $"{dot} {displayName}"
    End Sub

End Class
