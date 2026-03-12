' OiSnapshot.vb
' Helper class replacing anonymous tuple in MainForm._oiHistory list.

Public Class OiSnapshot
    Public Property Ts As Long
    Public Property OI As Double
    Public Sub New(ts As Long, oi As Double)
        Me.Ts = ts : Me.OI = oi
    End Sub
End Class
