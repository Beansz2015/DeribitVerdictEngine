<#
.SYNOPSIS
  Kelly trigger read - counts weekday STRONG rows and compares the total against the gate.

.DESCRIPTION
  The Kelly activation gate needs >= 406 pooled weekday STRONG rows. The pool is TWO files
  on the collector box, and the read is their sum:

      C:\DeribitEngine\analysis_log.csv.v0.7.bak   (closed at the v0.7 rotation)
    + C:\DeribitEngine\analysis_log.csv            (live)

  This script existed as a throwaway in three consecutive seats' scratchpads before being
  committed. It is READ ONLY: it sends no write of any kind to the box.

  WHY THE CALIBRATION STEP IS BUILT IN AND NOT OPTIONAL
  -----------------------------------------------------
  The baseline was first measured by another seat. Counting differently makes the sum
  meaningless, and nothing about a wrong count looks wrong. So -Mode Box runs the
  calibration FIRST and REFUSES to read the box if it fails. The reference file has a
  published expected answer (49, with 2026-09-02 = 14 and 2026-09-03 = 21) recorded in
  docs/seat-handover-2026-09-07.md 1.1 - that is the only file for which a correct answer
  is on record, so a NEWER copy-back is not a better calibration input, it is a useless
  one.

  THE REFERENCE FILE IS NOT IN THE REPO. AWS-copybacks/ is untracked (verified: git
  ls-files AWS-copybacks/ returns 0). A fresh clone therefore CANNOT calibrate. That is
  stated loudly rather than papered over: -SkipCalibration exists, prints a warning, and
  should be used only when you have another way to trust the count.

  THREE TRAPS, ALL GUARDED HERE
  -----------------------------
  1. EXACT-match the two verdict strings. All ten distinct Verdict values were enumerated
     2026-09-07 and again 2026-09-08: there is no "NO TRADE [STRONG ...]" form today, so a
     /STRONG/ substring would not over-count - but exact-match anyway, so the count cannot
     silently shift if one ever appears. Match is case-SENSITIVE (-cnotcontains).
  2. ParseExact with InvariantCulture. [datetime]::Parse reads the BOX locale.
  3. DateTime.MinValue.DayOfWeek is MONDAY, so an unparsed timestamp sails through a naive
     day check as a valid weekday row. MinValue is rejected explicitly, FIRST.

  ONE COUNTING ALGORITHM, ONE SOURCE. $CountScript below is the only implementation. It is
  executed locally for calibration and shipped verbatim to the box for the real read, so
  "the identical query" is enforced by construction rather than by care.

.PARAMETER Mode
  Box       (default) calibrate locally, then read BOTH files on the collector and sum.
  Calibrate run the calibration reference only and report pass/fail.
  Local     count one local CSV given by -Path.

.EXAMPLE
  pwsh tools/ops/kelly-trigger-read.ps1
.EXAMPLE
  pwsh tools/ops/kelly-trigger-read.ps1 -Mode Local -Path .\analysis_log.csv
#>
[CmdletBinding()]
param(
    [ValidateSet('Box', 'Calibrate', 'Local')]
    [string]$Mode = 'Box',

    # -Mode Local only.
    [string]$Path,

    # Deliberately no default that points at a box. Pass it explicitly.
    [string]$InstanceId = 'i-0d6c133058876273e',
    [string]$Region     = 'eu-west-2',

    [string]$RemoteLive = 'C:\DeribitEngine\analysis_log.csv',
    [string]$RemoteBak  = 'C:\DeribitEngine\analysis_log.csv.v0.7.bak',

    # The activation gate. Not a settings.json key - it comes from the Kelly activation
    # rule, so it is a literal here by intent, not by omission.
    [int]$Threshold = 406,

    # Calibration reference + its published expected answer. These three numbers are a
    # MEASUREMENT OF RECORD from docs/seat-handover-2026-09-07.md 1.1, not settings-derived
    # values, so a literal is correct here (fixture-literal provenance rule: MECHANISM).
    [string]$CalibrationPath  = 'AWS-copybacks\aws-copyback-2026-09-06\analysis_log_aws.csv',
    [int]$CalibrationExpected = 49,
    [switch]$SkipCalibration,

    [int]$TimeoutSec = 600
)

$ErrorActionPreference = 'Stop'

# -- the ONE counting implementation --------------------------------------------------
# Single-quoted here-string: nothing here expands locally. $Path is supplied by the caller
# (locally as a variable, remotely by literal substitution of the __KELLY_PATH__ token).
$CountScript = @'
$ci = [System.Globalization.CultureInfo]::InvariantCulture
$fmt = "yyyy-MM-dd HH:mm:ss"
$strong = @("STRONG LONG", "STRONG SHORT")
$comma = [char]44
$byDay = @{}
$total = 0; $weekend = 0; $unparsed = 0; $rows = 0
$first = $null; $last = $null
$reader = [System.IO.File]::OpenText($Path)
try {
    $header = $reader.ReadLine()
    $cols = $header.Split($comma)
    $tsIdx = [Array]::IndexOf($cols, "Timestamp")
    $vIdx = [Array]::IndexOf($cols, "Verdict")
    if ($tsIdx -lt 0 -or $vIdx -lt 0) { throw "header missing Timestamp or Verdict" }
    while ($null -ne ($line = $reader.ReadLine())) {
        $p = $line.Split($comma)
        if ($p.Length -le [Math]::Max($tsIdx, $vIdx)) { continue }
        $rows++
        if ($null -eq $first) { $first = $p[$tsIdx] }
        $last = $p[$tsIdx]
        $vv = $p[$vIdx].Trim()
        if ($strong -cnotcontains $vv) { continue }
        $ts = [datetime]::MinValue
        $ok = [datetime]::TryParseExact($p[$tsIdx].Trim(), $fmt, $ci, [System.Globalization.DateTimeStyles]::None, [ref]$ts)
        if (-not $ok) { $unparsed++; continue }
        if ($ts -eq [datetime]::MinValue) { $unparsed++; continue }
        if ($ts.DayOfWeek -eq [System.DayOfWeek]::Saturday -or $ts.DayOfWeek -eq [System.DayOfWeek]::Sunday) { $weekend++; continue }
        $total++
        $k = $ts.ToString("yyyy-MM-dd")
        if ($byDay.ContainsKey($k)) { $byDay[$k]++ } else { $byDay[$k] = 1 }
    }
} finally { $reader.Close() }
"KELLY_PATH=" + $Path
"KELLY_ROWS=" + $rows
"KELLY_SPAN=" + $first + " -> " + $last
"KELLY_WEEKDAY_STRONG=" + $total
"KELLY_EXCL=weekend:" + $weekend + " unparsed:" + $unparsed
"--- per weekday ---"
$byDay.GetEnumerator() | Sort-Object Name | ForEach-Object { $_.Name + " = " + $_.Value }
'@

function Invoke-CountLocal {
    param([Parameter(Mandatory = $true)][string]$CsvPath)
    if (-not (Test-Path $CsvPath)) { throw "file not found: $CsvPath" }
    $Path = (Resolve-Path $CsvPath).Path
    return @(Invoke-Expression $CountScript)
}

function Get-CountValue {
    param([string[]]$Lines, [string]$Key)
    $hit = $Lines | Where-Object { $_ -like "$Key=*" } | Select-Object -First 1
    if (-not $hit) { throw "no '$Key' line in output" }
    return $hit.Substring($Key.Length + 1)
}

<#
  Run $CountScript on the box against one remote file. Payload shape and BOM-less write
  mirror tools/ops/collector.ps1's Invoke-RemotePs - Set-Content -Encoding utf8 on Windows
  PowerShell 5.1 writes a BOM, and a BOM-prefixed file handed to
  `aws ssm send-command --parameters file://` risks a CLI-side JSON parse failure.
#>
function Invoke-CountRemote {
    param(
        [Parameter(Mandatory = $true)][string]$RemotePath,
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][string]$Region,
        [int]$TimeoutSec = 600
    )
    $body = '$Path = "__KELLY_PATH__"' + "`n" + $CountScript
    $body = $body.Replace('__KELLY_PATH__', $RemotePath)
    $commands = $body -split "`r?`n"

    $tmp = [System.IO.Path]::GetTempFileName()
    try {
        $payload = @{ commands = $commands } | ConvertTo-Json -Depth 4
        [System.IO.File]::WriteAllText($tmp, $payload, (New-Object System.Text.UTF8Encoding $false))

        $sendOut = & aws ssm send-command --instance-ids $InstanceId --region $Region `
                     --document-name 'AWS-RunPowerShellScript' --parameters "file://$tmp" --output json 2>&1
        if ($LASTEXITCODE -ne 0) { throw "send-command failed: $($sendOut -join ' ')" }
        $cmdId = (($sendOut -join "`n") | ConvertFrom-Json).Command.CommandId
        if (-not $cmdId) { throw "send-command returned no CommandId" }

        $deadline = (Get-Date).AddSeconds($TimeoutSec)
        $terminal = @('Success', 'Cancelled', 'TimedOut', 'Failed')
        $status = 'Pending'; $inv = $null
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 3
            $invOut = & aws ssm get-command-invocation --command-id $cmdId --instance-id $InstanceId `
                        --region $Region --output json 2>&1
            if ($LASTEXITCODE -ne 0) { continue }   # invocation record not visible yet
            $inv = ($invOut -join "`n") | ConvertFrom-Json
            $status = $inv.Status
            if ($terminal -contains $status) { break }
        }
        if ($status -ne 'Success') {
            throw "remote read did not succeed (status=$status, CommandId=$cmdId): $($inv.StandardErrorContent)"
        }
        return [pscustomobject]@{
            CommandId = $cmdId
            Lines     = @($inv.StandardOutputContent -split "`r?`n" | Where-Object { $_ -ne '' })
        }
    }
    finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

<#
  Prove the counting method reproduces a published answer before any number derived from
  it is believed. Returns $true / $false; never throws on a mismatch - the caller decides.
#>
function Test-Calibration {
    param([string]$CsvPath, [int]$Expected)

    $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $full = if ([System.IO.Path]::IsPathRooted($CsvPath)) { $CsvPath } else { Join-Path $repoRoot $CsvPath }

    if (-not (Test-Path $full)) {
        Write-Warning "CALIBRATION REFERENCE MISSING: $full"
        Write-Warning "AWS-copybacks/ is UNTRACKED, so a fresh clone has no reference to calibrate against."
        return $false
    }

    $out = Invoke-CountLocal -CsvPath $full
    $got = [int](Get-CountValue -Lines $out -Key 'KELLY_WEEKDAY_STRONG')
    $d02 = ($out | Where-Object { $_ -like '2026-09-02 = *' }) -replace '.*= '
    $d03 = ($out | Where-Object { $_ -like '2026-09-03 = *' }) -replace '.*= '

    $ok = ($got -eq $Expected) -and ($d02 -eq '14') -and ($d03 -eq '21')
    if ($ok) {
        Write-Host "CALIBRATION PASSED - total=$got (expected $Expected), 09-02=$d02, 09-03=$d03" -ForegroundColor Green
    } else {
        Write-Host "CALIBRATION FAILED - total=$got (expected $Expected), 09-02=$d02 (expected 14), 09-03=$d03 (expected 21)" -ForegroundColor Red
    }
    return $ok
}

# -- main -----------------------------------------------------------------------------
switch ($Mode) {

    'Local' {
        if (-not $Path) { throw "-Mode Local requires -Path" }
        Invoke-CountLocal -CsvPath $Path
    }

    'Calibrate' {
        $ok = Test-Calibration -CsvPath $CalibrationPath -Expected $CalibrationExpected
        if (-not $ok) { exit 1 }
    }

    'Box' {
        if ($SkipCalibration) {
            Write-Warning "CALIBRATION SKIPPED BY REQUEST. The sum below is unverified - the baseline was"
            Write-Warning "measured by another seat, and counting differently makes it meaningless."
        }
        else {
            if (-not (Test-Calibration -CsvPath $CalibrationPath -Expected $CalibrationExpected)) {
                throw "Refusing to read the box: calibration did not pass. Fix it, or re-run with -SkipCalibration and say so in whatever you publish."
            }
        }

        Write-Host ""
        Write-Host "reading $InstanceId ($Region) - READ ONLY" -ForegroundColor Cyan

        $bak = Invoke-CountRemote -RemotePath $RemoteBak  -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec
        $live = Invoke-CountRemote -RemotePath $RemoteLive -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec

        $bakN  = [int](Get-CountValue -Lines $bak.Lines  -Key 'KELLY_WEEKDAY_STRONG')
        $liveN = [int](Get-CountValue -Lines $live.Lines -Key 'KELLY_WEEKDAY_STRONG')
        $bakSpan  = Get-CountValue -Lines $bak.Lines  -Key 'KELLY_SPAN'
        $liveSpan = Get-CountValue -Lines $live.Lines -Key 'KELLY_SPAN'
        $total = $bakN + $liveN

        Write-Host ""
        $bak.Lines  | ForEach-Object { "  bak  | $_" }
        Write-Host ""
        $live.Lines | ForEach-Object { "  live | $_" }
        Write-Host ""
        Write-Host "  .bak  span $bakSpan"
        Write-Host "  live  span $liveSpan"
        Write-Host ""
        Write-Host "  .bak  weekday STRONG = $bakN"
        Write-Host "  live  weekday STRONG = $liveN"
        Write-Host "  TOTAL                = $total   against >= $Threshold"

        # An overlap would double-count. Compare the .bak's last stamp against the live
        # file's first: the rotation is only clean if the live file starts strictly after.
        $bakLast   = ($bakSpan  -split ' -> ')[1]
        $liveFirst = ($liveSpan -split ' -> ')[0]
        if ([string]::Compare($liveFirst, $bakLast) -le 0) {
            Write-Warning "SPANS OVERLAP - .bak ends $bakLast but live starts $liveFirst. The sum DOUBLE-COUNTS."
        } else {
            Write-Host "  spans do not overlap (.bak ends $bakLast, live starts $liveFirst) - sum is sound"
        }

        Write-Host ""
        if ($total -ge $Threshold) {
            Write-Host "  TRIGGER MET - $total >= $Threshold" -ForegroundColor Green
        } else {
            Write-Host "  TRIGGER NOT MET - $total, shortfall $($Threshold - $total)" -ForegroundColor Yellow
            Write-Host "  Do NOT fire the bundled W6-4 re-run: it would freeze on a below-trigger span."
        }
        Write-Host ""
        Write-Host "  CommandIds: bak=$($bak.CommandId) live=$($live.CommandId)"
    }
}
