' UI/Theme/Theme.vb
' UI reskin P2 — palette tokens (design system hex values) +
' bundled-font factory.
'
' Hex values updated to the design palette per docs/ui-reskin-proposal.md §3.
' Token names are stable from P1 — call sites in MainForm_Render_*.vb and
' MainForm_Layout.vb continue to reference Theme.X unchanged.
'
' P1 carried a transitional ACC_HEADER token (legacy yellow) so the migration
' could be pixel-identical. P2 retires it: section headers move to
' FG_SECONDARY, CAPPED labels move to ACC_WARN.

Imports System.Drawing
Imports System.Drawing.Text
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices

Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ----- Palette tokens (P2 hex values from the design system; see
    '       docs/ui-reskin-proposal.md §3 for the rationale) -----

    Public Shared ReadOnly BG_BASE            As Color = Color.FromArgb(13, 17, 23)
    Public Shared ReadOnly BG_CARD            As Color = Color.FromArgb(22, 22, 27)
    Public Shared ReadOnly BG_CARD_RAISED     As Color = Color.FromArgb(28, 28, 34)
    Public Shared ReadOnly BORDER_CARD        As Color = Color.FromArgb(43, 43, 51)
    Public Shared ReadOnly BORDER_INNER       As Color = Color.FromArgb(31, 31, 37)
    Public Shared ReadOnly BORDER_DASHED_INFO As Color = Color.FromArgb(45, 93, 107)

    Public Shared ReadOnly FG_PRIMARY    As Color = Color.FromArgb(234, 234, 238)
    Public Shared ReadOnly FG_SECONDARY  As Color = Color.FromArgb(186, 186, 191)
    Public Shared ReadOnly FG_TERTIARY   As Color = Color.FromArgb(139, 139, 146)
    Public Shared ReadOnly FG_QUATERNARY As Color = Color.FromArgb(94, 94, 101)
    Public Shared ReadOnly FG_DIM        As Color = Color.FromArgb(63, 63, 71)
    Public Shared ReadOnly FG_INK        As Color = Color.FromArgb(13, 17, 23)

    Public Shared ReadOnly ACC_STRONG_LONG  As Color = Color.FromArgb(74, 222, 128)
    Public Shared ReadOnly ACC_LONG         As Color = Color.FromArgb(134, 239, 172)
    Public Shared ReadOnly ACC_WEAK_LONG    As Color = Color.FromArgb(187, 247, 208)
    Public Shared ReadOnly ACC_NO_TRADE     As Color = Color.FromArgb(148, 163, 184)
    Public Shared ReadOnly ACC_WEAK_SHORT   As Color = Color.FromArgb(252, 165, 165)
    Public Shared ReadOnly ACC_SHORT        As Color = Color.FromArgb(248, 113, 113)
    Public Shared ReadOnly ACC_STRONG_SHORT As Color = Color.FromArgb(239, 68, 68)
    Public Shared ReadOnly ACC_WARN         As Color = Color.FromArgb(251, 191, 36)
    Public Shared ReadOnly ACC_CTA          As Color = Color.FromArgb(245, 158, 11)
    Public Shared ReadOnly ACC_AMBER_DEEP   As Color = Color.FromArgb(217, 119, 6)
    ' Rose-600 — the LIQ cascade accent (v59 follow-up). Deliberately magenta-tinted so
    ' it reads as urgent-red WITHOUT overlapping the verdict ramp's short-side reds
    ' (ACC_WEAK_SHORT 252,165,165 / ACC_SHORT 248,113,113 / ACC_STRONG_SHORT 239,68,68 —
    ' all pure-red with green=blue) or the amber attention token (ACC_WARN 251,191,36).
    ' Used by the TAPE strip when the liq-cascade signal or its 6-s flash is active.
    ' Level-approach + FIRST_SEEN keep the amber ACC_WARN (spec-back §2.11 / display-only).
    Public Shared ReadOnly ACC_CASCADE      As Color = Color.FromArgb(225, 29, 72)
    Public Shared ReadOnly ACC_INFO         As Color = Color.FromArgb(103, 232, 249)
    Public Shared ReadOnly ACC_NEUTRAL      As Color = Color.FromArgb(100, 116, 139)

    ' ----- Bundled-font loading -----

    Private Shared ReadOnly _privateFonts As New PrivateFontCollection()
    Private Shared _geistMonoFamily As FontFamily = Nothing

    Shared Sub New()
        TryLoadEmbeddedFont("GeistMono-Regular.ttf")
        TryLoadEmbeddedFont("GeistMono-Bold.ttf")
        TryLoadEmbeddedFont("GeistMono-SemiBold.ttf")
        If _privateFonts.Families.Length > 0 Then
            _geistMonoFamily = _privateFonts.Families(0)
        End If
    End Sub

    Private Shared Sub TryLoadEmbeddedFont(resourceFileName As String)
        Try
            ' Manifest name format is "<RootNamespace>.fonts.<file>" — match by
            ' suffix to stay robust against root-namespace changes.
            Dim asm = Assembly.GetExecutingAssembly()
            Dim manifestName = asm.GetManifestResourceNames() _
                .FirstOrDefault(Function(n) n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase))
            If manifestName Is Nothing Then Return
            Using stream = asm.GetManifestResourceStream(manifestName)
                If stream Is Nothing Then Return
                Dim buffer(CInt(stream.Length) - 1) As Byte
                stream.Read(buffer, 0, buffer.Length)
                Dim handle = GCHandle.Alloc(buffer, GCHandleType.Pinned)
                Try
                    _privateFonts.AddMemoryFont(handle.AddrOfPinnedObject(), buffer.Length)
                Finally
                    handle.Free()
                End Try
            End Using
        Catch
            ' Fallback chain in FontMono handles absence.
        End Try
    End Sub

    Public Shared Function FontMono(size As Single, Optional style As FontStyle = FontStyle.Regular) As Font
        If _geistMonoFamily IsNot Nothing Then
            Try
                Return New Font(_geistMonoFamily, size, style)
            Catch
            End Try
        End If
        For Each candidate In {"JetBrains Mono", "Cascadia Code", "Consolas"}
            Try
                Dim f = New Font(candidate, size, style)
                If String.Equals(f.FontFamily.Name, candidate, StringComparison.OrdinalIgnoreCase) Then
                    Return f
                End If
                f.Dispose()
            Catch
            End Try
        Next
        Return New Font(SystemFonts.DefaultFont.FontFamily, size, style)
    End Function

End Class
