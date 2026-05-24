# Captures the DeribitVerdictEngine MainForm window to a PNG via Win32
# PrintWindow. Works on non-foreground windows. Returns exit 0 on success,
# non-zero on failure (window not found, capture failed, etc.).
#
# Usage: pwsh tools/screenshot-mainform.ps1 [output-path]
#   Default output-path = "screenshot.png" in the current working dir.

param([string]$OutputPath = "screenshot.png")

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class W {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    public static IntPtr FindBySubstring(string needle) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h)) return true;
            int len = GetWindowTextLength(h);
            if (len <= 0) return true;
            StringBuilder sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# MainForm.Text in MainForm_Layout's constructor includes a version suffix
# (e.g. "Deribit Verdict Engine v0.47 [P4]"). Match by substring on the
# stable prefix so the helper survives version bumps.
$hWnd = [W]::FindBySubstring("Deribit Verdict Engine")
if ($hWnd -eq [IntPtr]::Zero) {
    # Fallback to exact-title candidates in case of an unusual harness setup.
    foreach ($title in @("DeribitVerdictEngine", "MainForm")) {
        $hWnd = [W]::FindWindow($null, $title)
        if ($hWnd -ne [IntPtr]::Zero) { break }
    }
}
if ($hWnd -eq [IntPtr]::Zero) {
    Write-Error "MainForm not found (substring 'Deribit Verdict Engine')"
    exit 1
}

$rect = New-Object W+RECT
$ok = [W]::GetWindowRect($hWnd, [ref]$rect)
if (-not $ok) { Write-Error "GetWindowRect failed"; exit 2 }

$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
if ($w -le 0 -or $h -le 0) { Write-Error "Invalid window dimensions: ${w}x${h}"; exit 3 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [W]::PrintWindow($hWnd, $hdc, 0)
$g.ReleaseHdc($hdc); $g.Dispose()

if (-not $ok) {
    $bmp.Dispose()
    Write-Error "PrintWindow returned false"
    exit 4
}

$dir = Split-Path $OutputPath -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $OutputPath (${w}x${h})"
exit 0
