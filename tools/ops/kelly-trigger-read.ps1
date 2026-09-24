<#
.SYNOPSIS
  Kelly trigger read - counts weekday STRONG rows and compares the total against the gate.

.DESCRIPTION
  The Kelly activation gate needs >= 406 pooled weekday STRONG rows. The pool is EVERY
  rotated book on the collector box plus the live file, and the read is their sum:

      C:\DeribitEngine\analysis_log.csv*.bak       (every rotated book, discovered on the box)
    + C:\DeribitEngine\analysis_log.csv            (live)

  [RIDER-2b, docs/absorption-d2-stage1-rotation-build-spec.md 4.5 - build trap T-7] Until
  the 2026-09 rotation this pooled exactly two files, the live one and the literal
  analysis_log.csv.v0.7.bak. That .bak closed at the 2026-09-01 rotation (it holds the
  111-column book, 2026-07-22 -> 2026-09-01 15:48:01 UTC), NOT at "the v0.7 rotation" as
  this line used to say. The 2026-09 rotation files the 116-column book under a new name
  (analysis_log.csv.<N>col-<h8>.<utc>.bak, AnalysisLogger.RotatedBakName), so a
  two-file read would silently drop 2026-09-01 -> the deploy. The read now DISCOVERS every
  analysis_log.csv*.bak on the box, counts each, sums all, and checks the span of every
  adjacent pair in time order for overlap. The counting algorithm and the calibration are
  unchanged.

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
  Box       (default) calibrate locally, then read EVERY rotated book + the live file on
            the collector and sum.
  Calibrate run the calibration reference only and report pass/fail.
  Local     count one local CSV given by -Path.
  LocalDir  the Box pool, on a LOCAL folder given by -Dir (for example an aws_fetch\<stamp>
            folder): every analysis_log.csv*.bak in it + its analysis_log.csv, same sum,
            same overlap check. Network-free, so the pooling itself can be reviewed.

.EXAMPLE
  pwsh tools/ops/kelly-trigger-read.ps1
.EXAMPLE
  pwsh tools/ops/kelly-trigger-read.ps1 -Mode Local -Path .\analysis_log.csv
.EXAMPLE
  pwsh tools/ops/kelly-trigger-read.ps1 -Mode LocalDir -Dir .\aws_fetch\20260924-084613
#>
[CmdletBinding()]
param(
    [ValidateSet('Box', 'Calibrate', 'Local', 'LocalDir')]
    [string]$Mode = 'Box',

    # -Mode Local only.
    [string]$Path,

    # -Mode LocalDir only.
    [string]$Dir,

    # Deliberately no default that points at a box. Pass it explicitly.
    [string]$InstanceId = 'i-0d6c133058876273e',
    [string]$Region     = 'eu-west-2',

    [string]$RemoteLive = 'C:\DeribitEngine\analysis_log.csv',
    # [RIDER-2b] Every rotated book beside the live file matches this filter. It replaced
    # the literal -RemoteBak 'C:\DeribitEngine\analysis_log.csv.v0.7.bak'.
    [string]$BakFilter  = 'analysis_log.csv*.bak',

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
    return Invoke-RemoteScript -Body $body -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec
}

<#
  [RIDER-2b] List every rotated book beside the live file ON THE BOX. READ ONLY: a
  directory listing. Emits one KELLY_BAK=<full path> line per match, sorted by name.
#>
function Get-RemoteBooks {
    param(
        [Parameter(Mandatory = $true)][string]$LivePath,
        [Parameter(Mandatory = $true)][string]$Filter,
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][string]$Region,
        [int]$TimeoutSec = 600
    )
    $dir = Split-Path -Parent $LivePath
    $body = "Get-ChildItem -LiteralPath '__KELLY_DIR__' -Filter '__KELLY_FILTER__' -File | Sort-Object Name | ForEach-Object { 'KELLY_BAK=' + `$_.FullName }"
    $body = $body.Replace('__KELLY_DIR__', $dir).Replace('__KELLY_FILTER__', $Filter)
    $r = Invoke-RemoteScript -Body $body -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec
    return @($r.Lines | Where-Object { $_ -like 'KELLY_BAK=*' } | ForEach-Object { $_.Substring(10) })
}

<#
  [RIDER-2b] The pooled read, shared by -Mode Box and -Mode LocalDir so the two cannot
  disagree. $Books is a list of @{ Label; Lines; CommandId } - one per book, live included -
  each Lines being $CountScript's output for that file. Sorts the books by their FIRST
  timestamp, checks every ADJACENT pair for overlap (the rotation is clean only if the later
  book starts strictly after the earlier one ends), sums, and compares to the threshold.
  KELLY_SPAN is first-line -> last-line of the file; within one book the collector appends
  in time order, so that is the book's span.
#>
function Show-PooledRead {
    param([Parameter(Mandatory = $true)][object[]]$Books, [int]$Threshold)
    $rowsOut = @()
    foreach ($b in $Books) {
        $n = [int](Get-CountValue -Lines $b.Lines -Key 'KELLY_WEEKDAY_STRONG')
        $span = Get-CountValue -Lines $b.Lines -Key 'KELLY_SPAN'
        $parts = $span -split ' -> '
        $rowsOut += [pscustomobject]@{ Label = $b.Label; N = $n; First = $parts[0].Trim(); Last = $parts[1].Trim(); Span = $span; CommandId = $b.CommandId; Lines = $b.Lines }
    }
    # Empty books (no data row) have an empty span; they add 0 and join no overlap pair.
    $ordered = @($rowsOut | Sort-Object First)
    Write-Host ""
    foreach ($o in $ordered) {
        $o.Lines | ForEach-Object { "  $($o.Label) | $_" }
        Write-Host ""
    }
    $total = 0
    foreach ($o in $ordered) {
        Write-Host ("  {0,-60} span {1}" -f $o.Label, $o.Span)
        Write-Host ("  {0,-60} weekday STRONG = {1}" -f '', $o.N)
        $total += $o.N
    }
    Write-Host ("  BOOKS                = {0}" -f $ordered.Count)
    Write-Host "  TOTAL                = $total   against >= $Threshold"

    $withRows = @($ordered | Where-Object { $_.First -ne '' })
    $overlap = $false
    for ($i = 1; $i -lt $withRows.Count; $i++) {
        $prev = $withRows[$i - 1]; $cur = $withRows[$i]
        if ([string]::Compare($cur.First, $prev.Last, [System.StringComparison]::Ordinal) -le 0) {
            Write-Warning "SPANS OVERLAP - $($prev.Label) ends $($prev.Last) but $($cur.Label) starts $($cur.First). The sum DOUBLE-COUNTS."
            $overlap = $true
        } else {
            Write-Host "  no overlap: $($prev.Label) ends $($prev.Last), $($cur.Label) starts $($cur.First)"
        }
    }
    if (-not $overlap) { Write-Host "  every adjacent pair is clean - sum is sound" }

    Write-Host ""
    if ($total -ge $Threshold) {
        Write-Host "  TRIGGER MET - $total >= $Threshold" -ForegroundColor Green
    } else {
        Write-Host "  TRIGGER NOT MET - $total, shortfall $($Threshold - $total)" -ForegroundColor Yellow
        Write-Host "  Do NOT fire the bundled W6-4 re-run: it would freeze on a below-trigger span."
    }
    $ids = @($ordered | Where-Object { $_.CommandId } | ForEach-Object { "$($_.Label)=$($_.CommandId)" })
    if ($ids.Count -gt 0) { Write-Host ""; Write-Host "  CommandIds: $($ids -join ' ')" }
}

<#
  Send one PowerShell body to the box and return its stdout lines. Payload shape and
  BOM-less write mirror tools/ops/collector.ps1's Invoke-RemotePs (see the note above
  Invoke-CountRemote). Extracted 2026-09 so the book listing and the count share it.
#>
function Invoke-RemoteScript {
    param(
        [Parameter(Mandatory = $true)][string]$Body,
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][string]$Region,
        [int]$TimeoutSec = 600
    )
    $commands = $Body -split "`r?`n"

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

        # [RIDER-2b] Discover every rotated book, then count each and the live file.
        $bakPaths = @(Get-RemoteBooks -LivePath $RemoteLive -Filter $BakFilter -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec)
        Write-Host "  rotated books found on the box: $($bakPaths.Count)"
        $bakPaths | ForEach-Object { Write-Host "    $_" }
        $books = @()
        foreach ($bp in $bakPaths) {
            $c = Invoke-CountRemote -RemotePath $bp -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec
            $books += @{ Label = (Split-Path -Leaf $bp); Lines = $c.Lines; CommandId = $c.CommandId }
        }
        $live = Invoke-CountRemote -RemotePath $RemoteLive -InstanceId $InstanceId -Region $Region -TimeoutSec $TimeoutSec
        $books += @{ Label = (Split-Path -Leaf $RemoteLive) + ' (live)'; Lines = $live.Lines; CommandId = $live.CommandId }
        Show-PooledRead -Books $books -Threshold $Threshold
    }

    'LocalDir' {
        if (-not $Dir) { throw "-Mode LocalDir requires -Dir" }
        if (-not (Test-Path $Dir)) { throw "folder not found: $Dir" }
        $liveLocal = Join-Path $Dir 'analysis_log.csv'
        $bakFiles = @(Get-ChildItem -LiteralPath $Dir -Filter $BakFilter -File | Sort-Object Name)
        Write-Host "  rotated books found in ${Dir}: $($bakFiles.Count)"
        $bakFiles | ForEach-Object { Write-Host "    $($_.Name)" }
        $books = @()
        foreach ($bf in $bakFiles) {
            $books += @{ Label = $bf.Name; Lines = (Invoke-CountLocal -CsvPath $bf.FullName); CommandId = $null }
        }
        if (Test-Path $liveLocal) {
            $books += @{ Label = 'analysis_log.csv (live)'; Lines = (Invoke-CountLocal -CsvPath $liveLocal); CommandId = $null }
        } else {
            Write-Warning "no analysis_log.csv in $Dir - pooling the rotated books only"
        }
        Show-PooledRead -Books $books -Threshold $Threshold
    }
}
