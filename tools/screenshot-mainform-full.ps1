# Sends Ctrl+Shift+S to the running DeribitVerdictEngine MainForm and
# polls for the saved PNG at the requested path. Captures the FULL form
# regardless of display height (uses MainForm.DrawToBitmap internally,
# which renders all content including off-screen cards).
#
# Usage:
#   pwsh tools/screenshot-mainform-full.ps1 [output-path]
#
# Exit codes:
#   0 = saved PNG at output-path
#   1 = MainForm not found (app not running)
#   2 = timed out waiting for the PNG

param([string]$OutputPath = "screenshot-full.png")

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

# Resolve to absolute path so the MainForm hotkey handler doesn't get confused
# by working-directory drift.
$absOut = [System.IO.Path]::GetFullPath($OutputPath)

# The app's working directory is bin/Debug/net8.0-windows/. The hotkey
# handler reads verify/.screenshot-target relative to AppDomain.BaseDirectory,
# so write the marker there.
$binMarker = Join-Path (Get-Location) "bin/Debug/net8.0-windows/verify/.screenshot-target"
$binMarkerDir = Split-Path $binMarker -Parent
if (-not (Test-Path $binMarkerDir)) {
    New-Item -ItemType Directory -Path $binMarkerDir -Force | Out-Null
}
$absOut | Out-File -FilePath $binMarker -Encoding utf8 -NoNewline

# Make sure the destination directory exists too (the app would create it
# itself, but doing it here keeps the failure mode explicit if perms are off).
$outDir = Split-Path $absOut -Parent
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# Find and foreground the MainForm so SendKeys hits it.
$root = [System.Windows.Automation.AutomationElement]::RootElement
$forms = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)
$form = $null
foreach ($f in $forms) {
    $name = $f.Current.Name
    if ($name -and $name.IndexOf("Deribit Verdict Engine", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $form = $f
        break
    }
}
if ($form -eq $null) {
    Write-Error "MainForm not found"
    exit 1
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WFG {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[WFG]::SetForegroundWindow([IntPtr]$form.Current.NativeWindowHandle) | Out-Null
Start-Sleep -Milliseconds 200

# Send Ctrl+Shift+S.
[System.Windows.Forms.SendKeys]::SendWait("^+s")

# Poll for the PNG (the hotkey handler takes ~200-500ms including the
# temporary form-resize-and-redraw).
$deadline = (Get-Date).AddSeconds(10)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $absOut) {
        $size = (Get-Item $absOut).Length
        if ($size -gt 0) {
            Start-Sleep -Milliseconds 200   # allow Save to fully flush
            Write-Host "Saved $absOut ($size bytes)"
            exit 0
        }
    }
    Start-Sleep -Milliseconds 100
}
Write-Error "Timed out waiting for $absOut"
exit 2
