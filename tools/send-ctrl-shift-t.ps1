# Foregrounds the DeribitVerdictEngine MainForm and sends Ctrl+Shift+T
# to trigger the P5-test render-parity harness.
#
# Lives in tools/ for the P5-test cycle only — delete in P5-test commit 3
# alongside the rest of the harness scaffolding.

param(
    [string]$WindowTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

# Win32 SetForegroundWindow — UIA can locate the window but can't focus it.
$sig = @'
[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll", SetLastError=true)]
public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
'@
Add-Type -MemberDefinition $sig -Namespace Win32 -Name NativeMethods

$root = [System.Windows.Automation.AutomationElement]::RootElement
$forms = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)
$form = $null
foreach ($f in $forms) {
    $name = $f.Current.Name
    if ($name -and $name.IndexOf($WindowTitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $form = $f
        break
    }
}
if ($form -eq $null) {
    Write-Error "MainForm not found"
    exit 1
}

$hwnd = [IntPtr]$form.Current.NativeWindowHandle
[Win32.NativeMethods]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
[Win32.NativeMethods]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 250

[System.Windows.Forms.SendKeys]::SendWait("^+T")
Write-Host "Sent Ctrl+Shift+T to '$($form.Current.Name)' (hwnd=$hwnd)"
