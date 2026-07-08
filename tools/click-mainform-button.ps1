# Clicks a button inside the DeribitVerdictEngine MainForm by name
# substring match (UI Automation Invoke pattern). Used during dev
# verification to trigger Analyze Now without keyboard / mouse focus.
#
# Usage:
#   pwsh tools/click-mainform-button.ps1 ANALYZE
#   pwsh tools/click-mainform-button.ps1 "Analyze Now"
#   pwsh tools/click-mainform-button.ps1 Start
#
# Exit codes:
#   0 = clicked successfully
#   1 = MainForm not found (app not running)
#   2 = no button matched the pattern (case-insensitive substring)
#   3 = matched button is not invokable (missing InvokePattern)

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

# Iterate buttons; match by case-insensitive substring on Name.
$buttons = $form.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)))

foreach ($b in $buttons) {
    $name = $b.Current.Name
    if (-not $name) { continue }
    if ($name.IndexOf($NamePattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        try {
            $invoke = $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invoke.Invoke()
            Write-Host "Clicked: '$name'"
            exit 0
        } catch {
            Write-Error "Button '$name' is not invokable: $_"
            exit 3
        }
    }
}

# Diagnostic: print all button names to help the caller refine the pattern.
Write-Error "No button matched '$NamePattern'. Available buttons:"
foreach ($b in $buttons) {
    $n = $b.Current.Name
    if ($n) { Write-Host "  - '$n'" }
}
exit 2
