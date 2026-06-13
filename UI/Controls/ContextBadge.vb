' UI/Controls/ContextBadge.vb
' UI reskin P3b — 5-variant verdict-context tag. Icon glyph + uppercase
' display label rendered as a single Label with auto-mapped colour.
'
' Per parent spec §6:
'   CONFIRMED          ✓  ACC_STRONG_LONG
'   ALIGNED            ↗  ACC_INFO
'   FLOW_UNCONFIRMED   ⚠  ACC_WARN
'   MOMENTUM_FADING    ⚠  ACC_WARN
'   STRUCTURALLY_WEAK  ⚠  ACC_SHORT
'   BELOW_MIN_MOVE     ⚠  ACC_AMBER_DEEP   (v35 min-tradeable-move gate: NO TRADE,
'                                            realistic target can't clear the floor)

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class ContextBadge
    Inherits Label

    Public Enum ContextKind
        CONFIRMED
        ALIGNED
        FLOW_UNCONFIRMED
        MOMENTUM_FADING
        STRUCTURALLY_WEAK
        BELOW_MIN_MOVE
    End Enum

    Private _kind As ContextKind = ContextKind.CONFIRMED

    Public Sub New()
        Me.AutoSize = True
        Me.BackColor = Color.Transparent
        Me.Font = Theme.FontMono(11.0F, FontStyle.Bold)
        ApplyKind()
    End Sub

    <Category("Theme")>
    <DefaultValue(GetType(ContextKind), "CONFIRMED")>
    Public Property Kind As ContextKind
        Get
            Return _kind
        End Get
        Set(v As ContextKind)
            _kind = v
            ApplyKind()
        End Set
    End Property

    Private Sub ApplyKind()
        Dim icon As String
        Dim displayName As String
        Dim colour As Color

        Select Case _kind
            Case ContextKind.CONFIRMED
                icon = "✓"
                displayName = "CONFIRMED"
                colour = Theme.ACC_STRONG_LONG
            Case ContextKind.ALIGNED
                icon = "↗"
                displayName = "ALIGNED"
                colour = Theme.ACC_INFO
            Case ContextKind.FLOW_UNCONFIRMED
                icon = "⚠"
                displayName = "FLOW UNCONFIRMED"
                colour = Theme.ACC_WARN
            Case ContextKind.MOMENTUM_FADING
                icon = "⚠"
                displayName = "MOMENTUM FADING"
                colour = Theme.ACC_WARN
            Case ContextKind.STRUCTURALLY_WEAK
                icon = "⚠"
                displayName = "STRUCTURALLY WEAK"
                colour = Theme.ACC_SHORT
            Case ContextKind.BELOW_MIN_MOVE
                icon = "⚠"
                displayName = "BELOW MIN MOVE"
                colour = Theme.ACC_AMBER_DEEP
            Case Else
                icon = ""
                displayName = ""
                colour = Theme.FG_TERTIARY
        End Select

        Me.ForeColor = colour
        Me.Text = $"{icon} {displayName}"
    End Sub

End Class
