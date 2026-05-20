' UI/Theme/Theme.vb
' UI reskin P1 — palette tokens + bundled-font factory.
'
' All callers reference Theme.X. P1 holds the same hex values as the old C_*
' palette so visual output stays pixel-identical; P2 swaps the hex values
' to the design palette without touching call sites.

Imports System.Drawing
Imports System.Drawing.Text
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices

Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ----- Palette tokens (P1 hex values mirror the previous C_* palette) -----

    Public Shared ReadOnly BG_BASE            As Color = Color.FromArgb(20, 20, 20)
    Public Shared ReadOnly BG_CARD            As Color = Color.FromArgb(20, 20, 20)
    Public Shared ReadOnly BG_CARD_RAISED     As Color = Color.FromArgb(30, 30, 30)
    Public Shared ReadOnly BORDER_CARD        As Color = Color.FromArgb(80, 80, 80)     ' was C_DIVIDER
    Public Shared ReadOnly BORDER_INNER       As Color = Color.FromArgb(60, 60, 60)
    Public Shared ReadOnly BORDER_DASHED_INFO As Color = Color.FromArgb(45, 93, 107)

    Public Shared ReadOnly FG_PRIMARY    As Color = Color.FromArgb(200, 200, 200)       ' was C_VALUE
    Public Shared ReadOnly FG_SECONDARY  As Color = Color.FromArgb(180, 180, 180)
    Public Shared ReadOnly FG_TERTIARY   As Color = Color.FromArgb(160, 160, 160)       ' was C_LABEL
    Public Shared ReadOnly FG_QUATERNARY As Color = Color.FromArgb(100, 100, 100)       ' was C_DIM
    Public Shared ReadOnly FG_DIM        As Color = Color.FromArgb(80, 80, 80)
    Public Shared ReadOnly FG_INK        As Color = Color.FromArgb(20, 20, 20)

    Public Shared ReadOnly ACC_STRONG_LONG  As Color = Color.FromArgb(80, 220, 120)     ' was C_GOOD
    Public Shared ReadOnly ACC_LONG         As Color = Color.FromArgb(80, 220, 120)
    Public Shared ReadOnly ACC_WEAK_LONG    As Color = Color.FromArgb(80, 220, 120)
    Public Shared ReadOnly ACC_NO_TRADE     As Color = Color.FromArgb(160, 160, 160)
    Public Shared ReadOnly ACC_WEAK_SHORT   As Color = Color.FromArgb(255, 80, 80)
    Public Shared ReadOnly ACC_SHORT        As Color = Color.FromArgb(255, 80, 80)      ' was C_BAD
    Public Shared ReadOnly ACC_STRONG_SHORT As Color = Color.FromArgb(255, 80, 80)
    Public Shared ReadOnly ACC_WARN         As Color = Color.FromArgb(255, 180, 40)     ' was C_WARN
    Public Shared ReadOnly ACC_CTA          As Color = Color.FromArgb(245, 158, 11)
    Public Shared ReadOnly ACC_AMBER_DEEP   As Color = Color.FromArgb(217, 119, 6)
    Public Shared ReadOnly ACC_INFO         As Color = Color.FromArgb(100, 200, 255)    ' was C_HIT
    Public Shared ReadOnly ACC_NEUTRAL      As Color = Color.FromArgb(100, 116, 139)

    ' Section / capped-label yellow. Kept distinct from ACC_WARN so P1 stays
    ' pixel-identical. P2 will likely split this between FG_SECONDARY (section
    ' headers) and ACC_WARN (CAPPED labels) — see ui-reskin-proposal §3.4.
    Public Shared ReadOnly ACC_HEADER As Color = Color.FromArgb(255, 220, 80)           ' was C_HEADER

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
