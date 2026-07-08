# Dumps the UI Automation element tree of the DeribitVerdictEngine
# MainForm, optionally filtered by a regex on the element Name.
# Useful for verifying that off-screen elements (cards past the display
# working-area clip) exist and are positioned correctly even when they
# aren't visible in a screenshot.
#
# Each line: "Y=<screen-Y> X=<screen-X> W=<width> | 'Name'"
# Coordinates are screen pixels as reported by UI Automation's
# BoundingRectangle (these match physical pixels under per-monitor
# DPI awareness; the form's internal logical coordinates differ).
#
# Usage:
#   pwsh tools/inspect-mainform-tree.ps1
#   pwsh tools/inspect-mainform-tree.ps1 -Pattern "SETTINGS|TOOLS|last"
#   pwsh tools/inspect-mainform-tree.ps1 -Pattern "^(KELLY|INDICATOR DETAILS|ANALYSIS SKIPPED|stale)"
#
# Exit codes:
#   0 = dumped successfully (zero or more matches)
#   1 = MainForm not found

param(
    [string]$Pattern = "",
    [string]$WindowTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

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

$fr = $form.Current.BoundingRectangle
Write-Host "Form bounding rect: X=$($fr.X) Y=$($fr.Y) W=$($fr.Width) H=$($fr.Height)"

$all = $form.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)

$count = 0
foreach ($el in $all) {
    $n = $el.Current.Name
    if (-not $n) { continue }
    if ($Pattern -and ($n -notmatch $Pattern)) { continue }
    $r = $el.Current.BoundingRectangle
    $line = "  Y={0,5:F0} X={1,5:F0} W={2,4:F0} | '{3}'" -f $r.Y, $r.X, $r.Width, $n
    Write-Host $line
    $count++
}
Write-Host "Found $count element(s) matching pattern '$Pattern'"
exit 0
