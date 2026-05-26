# Selects a radio button inside the DeribitVerdictEngine MainForm by name
# substring match (UI Automation SelectionItemPattern). Companion to
# click-mainform-button.ps1 — used by the P5-test harness to toggle posState
# (No Position / In Long / In Short) for Hold/Exit test cases.
#
# Usage:
#   pwsh tools/select-mainform-radio.ps1 "In Long"
#   pwsh tools/select-mainform-radio.ps1 "No Position"
#
# Exit codes:
#   0 = selected successfully
#   1 = MainForm not found (app not running)
#   2 = no radio matched the pattern (case-insensitive substring)
#   3 = matched radio does not support SelectionItemPattern

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$NamePattern,
    [string]$WindowTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# Find the MainForm by window-title substring.
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

# Iterate radio buttons; match by case-insensitive substring on Name.
$radios = $form.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::RadioButton)))

foreach ($r in $radios) {
    $name = $r.Current.Name
    if (-not $name) { continue }
    if ($name.IndexOf($NamePattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        try {
            $sel = $r.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $sel.Select()
            Write-Host "Selected: '$name'"
            exit 0
        } catch {
            Write-Error "Radio '$name' does not support SelectionItemPattern: $_"
            exit 3
        }
    }
}

# Diagnostic: print all radio names to help the caller refine the pattern.
Write-Error "No radio matched '$NamePattern'. Available radios:"
foreach ($r in $radios) {
    $n = $r.Current.Name
    if ($n) { Write-Host "  - '$n'" }
}
exit 2
