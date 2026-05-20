' UI/Controls/SegmentedToggle.vb
' UI reskin P3b — two-segment toggle. Selected segment renders as Solid
' FlatButton, unselected as Outline. Emits SelectionChanged when the
' user clicks a non-selected segment.

Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

Public Class SegmentedToggle
    Inherits UserControl

    Public Event SelectionChanged As EventHandler

    Private _options As String() = New String() {"Single", "Repeat"}
    Private _selectedIndex As Integer = 1
    Private _accentColor As Color = Theme.ACC_INFO
    Private _btnA As FlatButton
    Private _btnB As FlatButton

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = Color.Transparent
        MyBase.Size = New Size(160, 24)
        BuildButtons()
    End Sub

    Private Sub BuildButtons()
        _btnA = New FlatButton() With {
            .AccentColor = _accentColor,
            .InkColor = Theme.FG_INK,
            .CornerRadius = 4.0F,
            .Text = If(_options.Length > 0, _options(0), ""),
            .TabStop = False
        }
        _btnB = New FlatButton() With {
            .AccentColor = _accentColor,
            .InkColor = Theme.FG_INK,
            .CornerRadius = 4.0F,
            .Text = If(_options.Length > 1, _options(1), ""),
            .TabStop = False
        }
        AddHandler _btnA.Click, Sub() SelectIndex(0)
        AddHandler _btnB.Click, Sub() SelectIndex(1)
        Me.Controls.Add(_btnA)
        Me.Controls.Add(_btnB)
        ApplySelection()
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        If _btnA IsNot Nothing AndAlso _btnB IsNot Nothing Then
            Dim half = Me.ClientSize.Width \ 2
            _btnA.SetBounds(0, 0, half - 2, Me.ClientSize.Height)
            _btnB.SetBounds(half + 2, 0, Me.ClientSize.Width - half - 2, Me.ClientSize.Height)
        End If
        MyBase.OnLayout(levent)
    End Sub

    <Browsable(False)>
    Public Property Options As String()
        Get
            Return _options
        End Get
        Set(v As String())
            If v Is Nothing OrElse v.Length < 2 Then Return
            _options = v
            If _btnA IsNot Nothing Then _btnA.Text = _options(0)
            If _btnB IsNot Nothing Then _btnB.Text = _options(1)
        End Set
    End Property

    <Category("Theme")>
    <DefaultValue(1)>
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(v As Integer)
            If v < 0 Then v = 0
            If v > 1 Then v = 1
            If v <> _selectedIndex Then
                _selectedIndex = v
                ApplySelection()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    <Category("Theme")>
    Public Property AccentColor As Color
        Get
            Return _accentColor
        End Get
        Set(c As Color)
            _accentColor = c
            If _btnA IsNot Nothing Then _btnA.AccentColor = c
            If _btnB IsNot Nothing Then _btnB.AccentColor = c
            Invalidate()
        End Set
    End Property

    Private Sub SelectIndex(i As Integer)
        If i <> _selectedIndex Then
            _selectedIndex = i
            ApplySelection()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Private Sub ApplySelection()
        If _btnA Is Nothing OrElse _btnB Is Nothing Then Return
        _btnA.Variant = If(_selectedIndex = 0, FlatButton.ButtonVariant.Solid, FlatButton.ButtonVariant.Outline)
        _btnB.Variant = If(_selectedIndex = 1, FlatButton.ButtonVariant.Solid, FlatButton.ButtonVariant.Outline)
        _btnA.Invalidate()
        _btnB.Invalidate()
    End Sub

End Class
