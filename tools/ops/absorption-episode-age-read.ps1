<#
.SYNOPSIS
  Absorption episode-age read - the D-1 post-ship read that decides what D-2 is worth.

.DESCRIPTION
  D-2 (docs/absorption-mechanism-revision-proposal.md section 4.1) changes the absorption
  press accumulator from a 10-second rolling window to episode-cumulative. The two return
  the SAME number on any read whose episode is younger than window_sec, because the press
  queue is cleared at every episode open and close (Core/LevelAbsorptionTracker.vb :117,
  :315). So D-2's entire effect lives on reads where AbsorptionEpisodeSec > window_sec.
  This script measures that population.

  READ ONLY. It reads one local CSV. It never touches the collector - fetch a copy first:
      powershell tools/ops/collector.ps1 fetch -InstanceId <id>

  THE DECIDING VARIABLE IS AGE AT THE READ, NOT EPISODE LIFETIME
  -------------------------------------------------------------
  AbsorptionEpisodeSec is (read instant - episode open). That is exactly what decides
  whether PressSum is window-limited at the read, so no lifetime correction applies.
  Do NOT quote these percentiles as episode lifetimes - the sample is length-biased
  toward long episodes and each value is a partial age.

  WHY THE CALIBRATION STEP IS BUILT IN
  ------------------------------------
  The first read was done 2026-09-11 in Python (csv module) by another session. PowerShell
  splits on commas; Python honours quoting. If the two disagree, every number below is
  meaningless and nothing about it looks wrong. So -Mode Read runs the calibration FIRST
  and refuses to continue if it fails. The reference is a DATED fetch directory, which no
  later fetch overwrites - a newer copy is not a better calibration input, it is one with
  no published answer.

  THE TRAPS, ALL GUARDED HERE
  ---------------------------
  1. ParseExact with InvariantCulture. [datetime]::Parse reads the machine locale.
  2. DateTime.MinValue.DayOfWeek is MONDAY. MinValue is rejected FIRST, before the
     weekday test, or an unparsed stamp counts as a Monday.
  3. Weekday scope uses the UTC stamp as logged (weekday-scope ruling 2026-08-03), not
     the GMT+8 display calendar.
  4. Columns are resolved BY NAME from the header, never by position.
  5. A blank AbsorptionEpisodeSec means no episode was live at the read. It is excluded,
     never read as 0 - a zero is a real, legitimate age.
  6. window_sec is read from the tracked settings.json (shipped behaviour), printed, and
     overridable. After D-2 ships it no longer drives the press path; a read spanning
     that edge must be split on the deploy InstanceId first.

.PARAMETER Mode
  Read       (default) calibrate, then read -Path.
  Calibrate  run the calibration reference only.

.EXAMPLE
  powershell -NoProfile -File tools/ops/absorption-episode-age-read.ps1 -Path aws_fetch\<stamp>\analysis_log.csv
#>
[CmdletBinding()]
param(
    [ValidateSet('Read', 'Calibrate')]
    [string]$Mode = 'Read',

    [string]$Path,

    # 0 = take indicators.absorption.window_sec from the tracked settings.json.
    [double]$WindowSec = 0,

    # Calibration reference + its published answer: the 2026-09-11 (UTC) read recorded in
    # docs/d6d-episode-continuity-spec.md section 2.2. MEASUREMENT OF RECORD, not settings-
    # derived, so literals are correct here (fixture-literal provenance rule: MECHANISM).
    [string]$CalibrationPath = 'aws_fetch\20260909-143922\analysis_log.csv',
    [int]$CalReads    = 840,
    [int]$CalOver     = 218,
    [long]$CalAggrAll  = 909850,
    [long]$CalAggrOver = 442070,
    [switch]$SkipCalibration
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ci   = [System.Globalization.CultureInfo]::InvariantCulture

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# -- settings: window_sec + session buckets, both from the tracked file --------------------
$settingsPath = Join-Path $repo 'settings.json'
$settings = Get-Content -Raw -Encoding UTF8 $settingsPath | ConvertFrom-Json
if ($WindowSec -le 0) { $WindowSec = [double]$settings.indicators.absorption.window_sec; $winSrc = "settings.json v$($settings.version)" }
else { $winSrc = 'override' }
$buckets = @($settings.session_volume.sessions | ForEach-Object {
    [pscustomobject]@{ Name = $_.name; Start = [int]$_.start_hour; End = [int]$_.end_hour } })

function Get-Session([int]$hour) {
    # Mirrors ExecutionResolution.MatchSessionBucket: first bucket whose range contains the hour.
    foreach ($b in $buckets) { if ($hour -ge $b.Start -and $hour -le $b.End) { return $b.Name } }
    return 'UNMATCHED'
}

# Python-compatible percentile: sorted, k=(n-1)*q, linear interpolation.
function Get-Pct([double[]]$sorted, [double]$q) {
    if ($sorted.Length -eq 0) { return [double]::NaN }
    $k = ($sorted.Length - 1) * $q
    $lo = [Math]::Floor($k); $hi = [Math]::Min($lo + 1, $sorted.Length - 1)
    return $sorted[$lo] + ($sorted[$hi] - $sorted[$lo]) * ($k - $lo)
}

# -- the ONE reading implementation --------------------------------------------------------
function Invoke-EpisodeAgeRead([string]$CsvPath) {
    $full = Resolve-RepoPath $CsvPath
    if (-not (Test-Path $full)) { throw "file not found: $full" }

    $r = [pscustomobject]@{
        Path = $full; Rows = 0; Weekday = 0; Weekend = 0; Unparsed = 0; Short = 0; QuotedRows = 0
        First = $null; Last = $null
        Ages = New-Object System.Collections.Generic.List[double]
        Aggr = New-Object System.Collections.Generic.List[double]
        Day = @{}; Sess = @{}; Inst = @{}
    }
    $reader = [System.IO.File]::OpenText($full)
    try {
        $cols = $reader.ReadLine().Split([char]44)
        $need = 'Timestamp', 'AbsorptionEpisodeSec', 'AbsorptionAggrUsd', 'InstanceId'
        $ix = @{}
        foreach ($n in $need) {
            $i = [Array]::IndexOf($cols, $n)
            if ($i -lt 0) { throw "header missing column '$n' - is this a pre-2026-09-01 book?" }
            $ix[$n] = $i
        }
        $maxIx = ($ix.Values | Measure-Object -Maximum).Maximum
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.IndexOf([char]34) -ge 0) { $r.QuotedRows++ }   # a quote would break Split
            $p = $line.Split([char]44)
            if ($p.Length -le $maxIx) { $r.Short++; continue }
            $r.Rows++
            $ts = [datetime]::MinValue
            $ok = [datetime]::TryParseExact($p[$ix.Timestamp].Trim(), 'yyyy-MM-dd HH:mm:ss', $ci,
                                            [System.Globalization.DateTimeStyles]::None, [ref]$ts)
            if (-not $ok -or $ts -eq [datetime]::MinValue) { $r.Unparsed++; continue }       # trap 2: FIRST
            if ($ts.DayOfWeek -eq 'Saturday' -or $ts.DayOfWeek -eq 'Sunday') { $r.Weekend++; continue }
            $r.Weekday++
            if ($null -eq $r.First) { $r.First = $ts }
            $r.Last = $ts

            $s = $p[$ix.AbsorptionEpisodeSec].Trim()
            if ($s -eq '') { continue }                                                     # trap 5
            $age = 0.0
            if (-not [double]::TryParse($s, [System.Globalization.NumberStyles]::Float, $ci, [ref]$age)) { continue }
            $ag = 0.0
            [void][double]::TryParse($p[$ix.AbsorptionAggrUsd].Trim(), [System.Globalization.NumberStyles]::Float, $ci, [ref]$ag)

            $r.Ages.Add($age); $r.Aggr.Add($ag)
            $over = [int]($age -gt $WindowSec)
            foreach ($pair in @(@($r.Day, $ts.ToString('yyyy-MM-dd ddd', $ci)), @($r.Sess, (Get-Session $ts.Hour)), @($r.Inst, $p[$ix.InstanceId]))) {
                $tbl = $pair[0]; $key = $pair[1]
                if (-not $tbl.ContainsKey($key)) { $tbl[$key] = @(0, 0, 0.0, 0.0) }   # n, over, aggrAll, aggrOver
                $v = $tbl[$key]; $v[0]++; $v[1] += $over; $v[2] += $ag; if ($over) { $v[3] += $ag }
            }
        }
    } finally { $reader.Close() }
    return $r
}

function Get-Summary($r) {
    $ages = $r.Ages.ToArray(); $aggr = $r.Aggr.ToArray()
    $n = $ages.Length
    $over = 0; $aAll = 0.0; $aOver = 0.0
    $mult = New-Object System.Collections.Generic.List[double]
    for ($i = 0; $i -lt $n; $i++) {
        $aAll += $aggr[$i]
        if ($ages[$i] -gt $WindowSec) { $over++; $aOver += $aggr[$i]; $mult.Add($ages[$i] / $WindowSec) }
    }
    $sorted = [double[]]($ages | Sort-Object)
    $ms = [double[]]($mult | Sort-Object)
    [pscustomobject]@{
        N = $n; Over = $over; AggrAll = $aAll; AggrOver = $aOver
        Sorted = $sorted; Mult = $ms
        Mean = $(if ($n) { ($ages | Measure-Object -Average).Average } else { [double]::NaN })
    }
}

function Test-Calibration {
    $full = Resolve-RepoPath $CalibrationPath
    if (-not (Test-Path $full)) {
        Write-Warning "CALIBRATION REFERENCE MISSING: $full  (aws_fetch/ is untracked - a fresh clone cannot calibrate)"
        return $false
    }
    $s = Get-Summary (Invoke-EpisodeAgeRead $CalibrationPath)
    $ok = ($s.N -eq $CalReads) -and ($s.Over -eq $CalOver) -and
          ([Math]::Round($s.AggrAll) -eq $CalAggrAll) -and ([Math]::Round($s.AggrOver) -eq $CalAggrOver)
    $msg = "reads=$($s.N)/$CalReads over=$($s.Over)/$CalOver aggrAll=$([Math]::Round($s.AggrAll))/$CalAggrAll aggrOver=$([Math]::Round($s.AggrOver))/$CalAggrOver"
    if ($ok) { Write-Host "CALIBRATION PASSED - $msg" -ForegroundColor Green }
    else     { Write-Host "CALIBRATION FAILED - $msg" -ForegroundColor Red }
    return $ok
}

function Write-Segment([string]$title, [hashtable]$tbl) {
    "--- by $title ---"
    ('{0,-22} {1,6} {2,7} {3,9} {4,10}' -f $title, 'reads', 'over', 'over%', 'press%over')
    $tbl.GetEnumerator() | Sort-Object Name | ForEach-Object {
        $v = $_.Value
        $pct  = if ($v[0]) { 100.0 * $v[1] / $v[0] } else { 0 }
        $ppct = if ($v[2] -gt 0) { 100.0 * $v[3] / $v[2] } else { [double]::NaN }
        '{0,-22} {1,6} {2,7} {3,8:N1}% {4,9:N1}%' -f $_.Name, $v[0], $v[1], $pct, $ppct
    }
}

# -- main -----------------------------------------------------------------------------------
if ($Mode -eq 'Calibrate') { if (-not (Test-Calibration)) { exit 1 }; return }

if (-not $Path) { throw '-Mode Read requires -Path (fetch first: tools/ops/collector.ps1 fetch)' }
if ($SkipCalibration) { Write-Warning 'CALIBRATION SKIPPED BY REQUEST - the numbers below are unverified.' }
elseif (-not (Test-Calibration)) { throw 'Refusing to read: calibration did not pass.' }

$r = Invoke-EpisodeAgeRead $Path
$s = Get-Summary $r
$md5 = (Get-FileHash -Algorithm MD5 -Path $r.Path).Hash

''
"EPISODE_PATH=$($r.Path)"
"EPISODE_MD5=$md5"
"EPISODE_WINDOW_SEC=$WindowSec ($winSrc)"
"EPISODE_ROWS=$($r.Rows) weekday:$($r.Weekday) weekend:$($r.Weekend) unparsed:$($r.Unparsed) short:$($r.Short) quoted:$($r.QuotedRows)"
"EPISODE_WEEKDAY_SPAN=$($r.First.ToString('yyyy-MM-dd HH:mm:ss', $ci)) -> $($r.Last.ToString('yyyy-MM-dd HH:mm:ss', $ci)) UTC"
"EPISODE_READS=$($s.N)"
foreach ($q in 0.25, 0.50, 0.75, 0.90, 0.95, 0.99) { 'EPISODE_P{0:00}={1:N2}' -f ($q * 100), (Get-Pct $s.Sorted $q) }
'EPISODE_MEAN={0:N2}' -f $s.Mean
'EPISODE_MAX={0:N2}' -f $s.Sorted[-1]
'EPISODE_OVER_WINDOW={0}/{1} = {2:N2} %' -f $s.Over, $s.N, (100.0 * $s.Over / [Math]::Max($s.N, 1))
'EPISODE_PRESS_ON_OVER={0:N0}/{1:N0} USD = {2:N2} %' -f $s.AggrOver, $s.AggrAll, $(if ($s.AggrAll -gt 0) { 100.0 * $s.AggrOver / $s.AggrAll } else { [double]::NaN })
if ($s.Mult.Length) {
    'EPISODE_SPAN_MULT=p50 {0:N2}x  p75 {1:N2}x  p90 {2:N2}x  max {3:N2}x' -f (Get-Pct $s.Mult 0.50), (Get-Pct $s.Mult 0.75), (Get-Pct $s.Mult 0.90), $s.Mult[-1]
}

# -- concentration: is the pressing share a RATE, or a handful of large prints? -------------
# Added 2026-09-13 (UTC): the pooled pressing-on-over share moved 48.6 -> 36.4 % between a
# 6- and an 8-weekday-day read while the over-window READ share held at ~26 %. A sum of a
# heavy-tailed quantity behaves like that; a rate does not. This measures which it is.
$ag = $r.Aggr.ToArray(); $ageArr = $r.Ages.ToArray()
$posAll = 0; $posOver = 0
for ($i = 0; $i -lt $ag.Length; $i++) {
    if ($ag[$i] -gt 0) { $posAll++; if ($ageArr[$i] -gt $WindowSec) { $posOver++ } }
}
$top10 = 0.0
if ($ag.Length) { $top10 = [double](($ag | Sort-Object -Descending | Select-Object -First 10 | Measure-Object -Sum).Sum) }
'EPISODE_PRESS_POSITIVE_READS={0} of {1}  (over-window: {2} of {3})' -f $posAll, $s.N, $posOver, $s.Over
'EPISODE_TOP10_READS_SHARE={0:N2} % of all logged AbsorptionAggrUsd' -f (100.0 * $top10 / [Math]::Max($s.AggrAll, 1))

# -- day-block bootstrap: resample whole UTC weekdays, because pressing is lumpy by DAY ------
# Crude with ~10 blocks and labelled as such. Fixed seed so the interval reproduces.
$days = New-Object System.Collections.ArrayList
foreach ($k in $r.Day.Keys) { [void]$days.Add($r.Day[$k]) }
if ($days.Count -ge 3) {
    $rng = New-Object System.Random 20260913
    $bOver = New-Object System.Collections.Generic.List[double]
    $bPress = New-Object System.Collections.Generic.List[double]
    for ($b = 0; $b -lt 2000; $b++) {
        $bn = 0; $bo = 0; $ba = 0.0; $bp = 0.0
        for ($d = 0; $d -lt $days.Count; $d++) {
            $v = $days[$rng.Next($days.Count)]
            $bn += $v[0]; $bo += $v[1]; $ba += $v[2]; $bp += $v[3]
        }
        if ($bn -gt 0) { $bOver.Add(100.0 * $bo / $bn) }
        if ($ba -gt 0) { $bPress.Add(100.0 * $bp / $ba) }
    }
    $so = [double[]]($bOver | Sort-Object); $sp = [double[]]($bPress | Sort-Object)
    'EPISODE_BOOT_OVER_95={0:N1} .. {1:N1} %  ({2} UTC-weekday blocks, 2000 resamples, seed 20260913)' -f (Get-Pct $so 0.025), (Get-Pct $so 0.975), $days.Count
    'EPISODE_BOOT_PRESS_95={0:N1} .. {1:N1} %' -f (Get-Pct $sp 0.025), (Get-Pct $sp 0.975)
}
''
Write-Segment 'weekday' $r.Day
''
Write-Segment 'session' $r.Sess
''
Write-Segment 'InstanceId' $r.Inst
if ($r.Inst.Count -gt 1) { Write-Warning 'MORE THAN ONE InstanceId - split on the deploy ledger before pooling if a version edge falls inside this span.' }
