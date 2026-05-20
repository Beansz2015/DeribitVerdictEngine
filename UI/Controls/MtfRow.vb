' UI/Controls/MtfRow.vb
' UI reskin P3b — three-format MTF gate readout. Branches on Kind:
'   PASS       -> "MTF PASS [{Direction}]"                       green
'   BLOCK      -> "MTF BLOCK [{Direction} vs {BlockedAgainst}]"  red
'   STATE_ONLY -> "MTF state: {Direction}"                       dim

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class MtfRow
    Inherits Label

    Public Enum MtfKind
        PASS
        BLOCK
        STATE_ONLY
    End Enum

    Private _kind As MtfKind = MtfKind.PASS
    Private _direction As String = "LONG"
    Private _blockedAgainst As String = ""

    Public Sub New()
        Me.AutoSize = True
        Me.BackColor = Color.Transparent
        Me.Font = Theme.FontMono(11.0F, FontStyle.Bold)
        Refresh2()
    End Sub

    <Category("Theme")>
    <DefaultValue(GetType(MtfKind), "PASS")>
    Public Property Kind As MtfKind
        Get
            Return _kind
        End Get
        Set(v As MtfKind)
            _kind = v
            Refresh2()
        End Set
    End Property

    <Category("Theme")>
    Public Property Direction As String
        Get
            Return _direction
        End Get
        Set(v As String)
            _direction = If(v, "")
            Refresh2()
        End Set
    End Property

    <Category("Theme")>
    Public Property BlockedAgainst As String
        Get
            Return _blockedAgainst
        End Get
        Set(v As String)
            _blockedAgainst = If(v, "")
            Refresh2()
        End Set
    End Property

    Private Sub Refresh2()
        Select Case _kind
            Case MtfKind.PASS
                Me.Text = $"MTF PASS [{_direction}]"
                Me.ForeColor = Theme.ACC_STRONG_LONG
            Case MtfKind.BLOCK
                Me.Text = $"MTF BLOCK [{_direction} vs {_blockedAgainst}]"
                Me.ForeColor = Theme.ACC_SHORT
            Case MtfKind.STATE_ONLY
                Me.Text = $"MTF state: {_direction}"
                Me.ForeColor = Theme.FG_QUATERNARY
        End Select
    End Sub

End Class
