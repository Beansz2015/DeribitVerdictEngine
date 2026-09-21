#requires -Version 5.1
<#
  tools/checks/rotation-riders.ps1 -- the analysis_log.csv header-rotation rider check.

  WHY: the 2026-09-01 header rotation fired and none of the changes parked on "the next
  rotation" travelled with it. Nothing checked the rider list when the event fired.
  The ledger of record is docs/csv-rotation-riders.md.

  THE PROPERTY: if the AnalysisLogger.Header STRING differs between BEFORE and AFTER,
  then docs/csv-rotation-riders.md must be in the changed-file list.

  It compares the extracted header TEXT, never the file. A comment edit to
  AnalysisLogger.vb changes the file and must NOT trip this check; a header change
  must. Counting a file change would be a copy of the property.

  A header it cannot extract is reported, never skipped: a check that silently turns
  itself off after a refactor is the hole it exists to close. -Strict makes that a FAIL.

  OUTPUT: two lines, ROTATION_STATUS=OK|WARN|FAIL and ROTATION_DETAIL=<text>. Exit 0
  always -- the caller (verify-gate.ps1) decides what a status means for its mode.

  STANDALONE TEST (no build needed):
    powershell -NoProfile -File tools/checks/rotation-riders.ps1 -BeforeRev HEAD `
      -AfterFile <copy of AnalysisLogger.vb> -Changed AnalysisLogger.vb
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BeforeRev,
    # Git revision for AFTER. Empty = read -AfterFile from disk instead.
    [string]$AfterRev = '',
    [string]$AfterFile = 'AnalysisLogger.vb',
    [string[]]$Changed = @(),
    [switch]$Strict,
    [string]$LoggerPath = 'AnalysisLogger.vb',
    [string]$LedgerPath = 'docs/csv-rotation-riders.md'
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Get-HeaderText lives in lib/HeaderText.ps1, shared with tools/checks/rider-travel.ps1
# (docs/rider-travel-check-spec.md D-2). Do not re-inline it here -- a second copy is
# the exact drift class CLAUDE.md rules against for fixture literals.
. (Join-Path $PSScriptRoot 'lib\HeaderText.ps1')

function Emit([string]$status, [string]$detail) {
    "ROTATION_STATUS=$status"
    "ROTATION_DETAIL=$detail"
}

function Read-AtRev([string]$rev, [string]$path) {
    $spec = '{0}:{1}' -f $rev, $path
    $out = & git -C $repo show $spec 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return [string[]]$out
}

$normChanged = @($Changed | ForEach-Object { $_.Replace('\', '/') })
if ($normChanged -notcontains $LoggerPath) {
    Emit 'OK' "$LoggerPath not in the changed set - no header rotation possible"
    return
}

$before = Get-HeaderText (Read-AtRev $BeforeRev $LoggerPath)
if ($AfterRev -ne '') {
    $after = Get-HeaderText (Read-AtRev $AfterRev $LoggerPath)
} else {
    $af = if ([System.IO.Path]::IsPathRooted($AfterFile)) { $AfterFile } else { Join-Path $repo $AfterFile }
    $after = if (Test-Path $af) { Get-HeaderText ([string[]](Get-Content -Encoding UTF8 -Path $af)) } else { $null }
}

if ($null -eq $before -or $null -eq $after) {
    $msg = "cannot extract AnalysisLogger.Header (before=$($null -ne $before) after=$($null -ne $after)) - the rotation check is BLIND; update tools/checks/rotation-riders.ps1"
    Emit $(if ($Strict) { 'FAIL' } else { 'WARN' }) $msg
    return
}

if ($before -ceq $after) {
    Emit 'OK' "$LoggerPath changed but its Header is byte-identical - not a rotation"
    return
}

$nb = $before.Split(',').Length
$na = $after.Split(',').Length
if ($normChanged -contains $LedgerPath) {
    Emit 'OK' "HEADER ROTATION in range ($nb -> $na columns) and $LedgerPath was updated"
} else {
    Emit $(if ($Strict) { 'FAIL' } else { 'WARN' }) "HEADER ROTATION in range ($nb -> $na columns) but $LedgerPath was NOT updated - mark every rider it carries CONSUMED, or re-park it with a reason"
}
