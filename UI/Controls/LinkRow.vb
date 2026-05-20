' UI/Controls/LinkRow.vb
' UI reskin P3b — single horizontal row: › chevron + LinkLabel +
' optional trailing icon. Hover colour switches to ACC_INFO. Used
' inside the TOOLS sub-box of the SETTINGS & TOOLS section.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class LinkRow
    Inherits UserControl

    Public Event LinkClicked As EventHandler

    Private _chevron As Label
    Private _link As LinkLabel
    Private _trailing As Label
    Private _linkColor As Color = Theme.FG_SECONDARY
    Private _hoverColor As Color = Theme.ACC_INFO

    Public Sub New()
        MyBase.BackColor = Color.Transparent
        MyBase.Size = New Size(220, 22)
        BuildChildren()
    End Sub

    Private Sub BuildChildren()
        _chevron = New Label() With {
            .Text = "›",
            .AutoSize = True,
            .ForeColor = Theme.FG_QUATERNARY,
            .Font = Theme.FontMono(12.0F, FontStyle.Bold),
            .BackColor = Color.Transparent,
            .Location = New Point(0, 2)
        }
        _link = New LinkLabel() With {
            .Text = "Link",
            .AutoSize = True,
            .LinkColor = _linkColor,
            .ActiveLinkColor = _hoverColor,
            .VisitedLinkColor = _linkColor,
            .LinkBehavior = LinkBehavior.HoverUnderline,
            .Font = Theme.FontMono(10.0F, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .Location = New Point(14, 4)
        }
        AddHandler _link.LinkClicked, Sub(s, e) RaiseEvent LinkClicked(Me, EventArgs.Empty)
        AddHandler _link.MouseEnter, Sub(s, e)
                                         _link.LinkColor = _hoverColor
                                         _chevron.ForeColor = _hoverColor
                                     End Sub
        AddHandler _link.MouseLeave, Sub(s, e)
                                         _link.LinkColor = _linkColor
                                         _chevron.ForeColor = Theme.FG_QUATERNARY
                                     End Sub

        _trailing = New Label() With {
            .Text = "",
            .AutoSize = True,
            .ForeColor = Theme.FG_QUATERNARY,
            .Font = Theme.FontMono(10.0F, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .Visible = False
        }

        Me.Controls.Add(_chevron)
        Me.Controls.Add(_link)
        Me.Controls.Add(_trailing)
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        If _trailing IsNot Nothing AndAlso _trailing.Visible Then
            _trailing.Location = New Point(Me.ClientSize.Width - _trailing.Width - 4, 4)
        End If
        MyBase.OnLayout(levent)
    End Sub

    <Category("Theme")>
    Public Property LinkText As String
        Get
            Return If(_link IsNot Nothing, _link.Text, "")
        End Get
        Set(v As String)
            If _link IsNot Nothing Then _link.Text = If(v, "")
            PerformLayout()
        End Set
    End Property

    <Category("Theme")>
    Public Property TrailingIcon As String
        Get
            Return If(_trailing IsNot Nothing, _trailing.Text, "")
        End Get
        Set(v As String)
            If _trailing IsNot Nothing Then
                _trailing.Text = If(v, "")
                _trailing.Visible = Not String.IsNullOrEmpty(_trailing.Text)
            End If
            PerformLayout()
        End Set
    End Property

End Class
