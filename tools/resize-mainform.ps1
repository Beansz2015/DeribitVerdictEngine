# Resizes and/or moves the DeribitVerdictEngine MainForm window via
# Win32 SetWindowPos. Used during dev verification to bring off-screen
# portions of the form into view for screenshot capture (the form
# fits a 4K vertical monitor but exceeds smaller working areas, so
# parts of it clip on the implementation conversation's display).
#
# Usage:
#   pwsh tools/resize-mainform.ps1 -X 10 -Y 10 -W 1116 -H 2000
#   pwsh tools/resize-mainform.ps1 -Y -700           # only move vertically
#   pwsh tools/resize-mainform.ps1 -W 1200 -H 2200   # only resize
#
# Each parameter is optional. Unspecified dimensions are read from the
# form's current GetWindowRect so partial updates keep the rest stable.
#
# Exit codes:
#   0 = repositioned successfully
#   1 = MainForm not found
#   2 = SetWindowPos failed

param(
    [int]$X = [int]::MinValue,
    [int]$Y = [int]::MinValue,
    [int]$W = [int]::MinValue,
    [int]$H = [int]::MinValue,
    [string]$WindowTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ResizeAPI {
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

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
    Write-Error "MainForm not found (substring '$WindowTitleSubstring')"
    exit 1
}

$hwnd = [IntPtr]$form.Current.NativeWindowHandle
$cur = New-Object ResizeAPI+RECT
[ResizeAPI]::GetWindowRect($hwnd, [ref]$cur) | Out-Null

# Fill in unspecified parameters from current geometry.
if ($X -eq [int]::MinValue) { $X = $cur.Left }
if ($Y -eq [int]::MinValue) { $Y = $cur.Top }
if ($W -eq [int]::MinValue) { $W = $cur.Right - $cur.Left }
if ($H -eq [int]::MinValue) { $H = $cur.Bottom - $cur.Top }

$SWP_NOZORDER = 0x0004
$ok = [ResizeAPI]::SetWindowPos($hwnd, [IntPtr]::Zero, $X, $Y, $W, $H, $SWP_NOZORDER)
if (-not $ok) {
    Write-Error "SetWindowPos failed"
    exit 2
}
Start-Sleep -Milliseconds 400

# Confirm new geometry.
$new = New-Object ResizeAPI+RECT
[ResizeAPI]::GetWindowRect($hwnd, [ref]$new) | Out-Null
$nw = $new.Right - $new.Left
$nh = $new.Bottom - $new.Top
Write-Host "Form repositioned: X=$($new.Left) Y=$($new.Top) W=$nw H=$nh"
exit 0
