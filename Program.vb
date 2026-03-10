' Program.vb

Imports System.Text
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        ' Required for .NET Core/5+ to support legacy encodings like Windows-1252
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module
