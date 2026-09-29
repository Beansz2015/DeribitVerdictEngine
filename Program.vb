' Program.vb

Imports System.Text
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        ' Required for .NET Core/5+ to support legacy encodings like Windows-1252
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

        ' [C-8 / CH-1, docs/collector-halt-fixes-spec.md] No UI-thread exception may raise the
        ' default modal .NET dialog: on the unattended collector it waits for a click that never
        ' comes (adversarial audit row B2). Route them to MainForm.OnUiThreadException, which logs
        ' to run_errors.log. Must be set before any window exists. A non-UI-thread exception still
        ' ends the process, as .NET requires; the AppDomain handler records why first.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnAppDomainUnhandled

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Dim form As New MainForm()
        AddHandler Application.ThreadException, AddressOf form.OnUiThreadException
        Application.Run(form)
    End Sub

    Private Sub OnAppDomainUnhandled(sender As Object, e As UnhandledExceptionEventArgs)
        RunErrorLog.Log(RunErrorLog.OriginAppDomain, TryCast(e.ExceptionObject, Exception), "",
                        ProcessIdentity.InstanceId)
    End Sub
End Module
