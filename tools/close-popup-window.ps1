# Closes any top-level window matched by title substring, EXCEPT the
# DeribitVerdictEngine MainForm itself. Defensive helper for the P5-test
# harness — dismisses popups (AnalysisReportForm, MessageBox, etc.) that
# would otherwise stall an automated run loop.
#
# Usage:
#   pwsh tools/close-popup-window.ps1 "Analysis Report"
#   pwsh tools/close-popup-window.ps1 "Error"
#
# Exit codes:
#   0 = closed N matches (N may be 0; check stdout for diagnostic)

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TitleSubstring,
    [string]$MainFormTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$candidates = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)

$closedCount = 0
foreach ($w in $candidates) {
    $name = $w.Current.Name
    if (-not $name) { continue }
    # Skip the MainForm itself.
    if ($name.IndexOf($MainFormTitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) { continue }
    if ($name.IndexOf($TitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        try {
            $win = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $win.Close()
            Write-Host "Closed: '$name'"
            $closedCount++
        } catch {
            Write-Warning "Could not close '$name': $_"
        }
    }
}

if ($closedCount -eq 0) {
    Write-Host "No windows matched '$TitleSubstring' (nothing to close)"
}
exit 0
