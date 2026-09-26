<#
.SYNOPSIS
  ATR-conditional aggressor-velocity burst-watch read, all three sessions. AVR-1 (b), ruled by
  the trader 2026-09-26 (docs/avr1-atr-conditional-burst-watch-spec.md). Read-only.

.DESCRIPTION
  Replaces the fixed-reference watches (docs/d3-asia-burst-watch-read-*.md, the NY 8-12% band
  in docs/DeribitIndicatorProject.md section 12) with a per-row expected fire rate keyed on the
  row's session and ATR fifth (docs/avr1-atr-conditional-burst-watch-spec.md section 3, a RULED
  reference table, not a settings key). A read now compares observed vs expected fire rate for
  the ATR mix of the rows actually read, not against one fixed number.

  POPULATION (unchanged from the templates, docs/avr1-atr-conditional-burst-watch-spec.md
  section 5): weekday (UTC day-of-week) rows, AggrVelBurstRatio non-empty, ATR parses,
  deduplicated on Timestamp across books. Session = UTC hour against tracked settings.json
  session_volume.sessions, read at run time. Fire = AggrVelSignal in {BURST_BUY, BURST_SELL}.
  Same-side = BURST_BUY with TFISignal "BUY PRESSURE", or BURST_SELL with "SELL PRESSURE".

  COVERAGE (per covered weekday session-day, same rule as the ASIA template, applied per
  session): first row <= session start + MaxGapMin, last row >= session end hour :(60-MaxGapMin),
  no gap > MaxGapMin minutes between consecutive RAW session rows of that session-day (every
  row in the session's hours, population or not - the ASIA template's `asia` set). The
  coverage check runs over the same population used for the rate (AggrVelBurstRatio non-empty,
  ATR parses) -- the spec presents Rows as one population row, not two.

  Books read, oldest first (same set and order as both templates): analysis_log.csv.v0.7.bak,
  every analysis_log.csv.*col-*.bak (sorted by rotation stamp), then analysis_log.csv.

  BANDS (docs/avr1-atr-conditional-burst-watch-spec.md section 5):
    ASIA   - expected +/- 3pp, same-side >= 85%, length >= 10 covered weekdays. PASS if all
             three hold, else MISS naming each failed criterion.
    NY     - judged like ASIA (AVR-3 = (a), ruled 2026-09-26): expected +/- 2pp (the v52 watch
             band, width kept) applies to the SLICE MEAN, same-side >= 85%, length >= 10
             covered weekdays. PASS if all three hold, else MISS naming each failed criterion.
             Per-day in-band flags stay printed in the per-day table, for information only.
    LONDON - no ruled band. Reported only ("no ruled band", no verdict).

.PARAMETER FetchFolder
  A dated folder under aws_fetch/ produced by tools/ops/collector.ps1 fetch.

.PARAMETER Session
  ASIA | LONDON | NY | All (default All).

.PARAMETER From / -To
  Read window, dates (inclusive, UTC calendar day). -To default = last day present in the
  filtered population.

.PARAMETER VerifyReference
  Recomputes section 3 from raw data over -RefFrom..-RefTo (default the ruled reference window,
  2026-08-02 .. 2026-09-25 08:51:01 inclusive), using the SAME equal-row five bins as
  tools/ops/aggr-vel-regime-read.ps1 (not the edge rule). Prints recomputed edges/rates next to
  the literals. Pass: every rate within 0.10pp and every edge within 0.1 of the literal, exit 0.
  Fail: names which bins differ, exit 1. This is an escalation trigger
  (docs/avr1-atr-conditional-burst-watch-spec.md section 0) -- do not silently "fix" a mismatch.

.EXAMPLE
  powershell -NoProfile -File tools/ops/burst-watch-read.ps1 -FetchFolder aws_fetch\20260925-085341 -VerifyReference
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FetchFolder,
    [ValidateSet('ASIA', 'LONDON', 'NY', 'All')][string]$Session = 'All',
    [string]$From,
    [string]$To,
    [switch]$VerifyReference,
    [string]$RefFrom = '2026-08-02',
    [string]$RefTo = '2026-09-25 08:51:01',
    [int]$MaxGapMin = 6
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$folder = if ([IO.Path]::IsPathRooted($FetchFolder)) { $FetchFolder } else { Join-Path $repoRoot $FetchFolder }
$inv = [Globalization.CultureInfo]::InvariantCulture
$fmt = 'yyyy-MM-dd HH:mm:ss'

# --- section 3: the ruled ATR-fifth reference table -------------------------------------------
# RULED AVR-1 2026-09-26 -- reference, not tunable; source doc
# docs/aggr-vel-burst-rederivation-read-2026-09-26.md section 2.2, "all rows" column.
# MECHANISM-class under the fixture-literal provenance rule: a frozen measurement of record.
$Ref = @{
    ASIA   = @{ edges = @(33.6, 51.2, 66.2, 83.9); rates = @(0.1546, 0.0851, 0.0513, 0.0469, 0.0287) }
    LONDON = @{ edges = @(31.9, 51.5, 68.4, 90.7); rates = @(0.1865, 0.1003, 0.0358, 0.0372, 0.0186) }
    NY     = @{ edges = @(19.3, 30.9, 43.0, 62.9); rates = @(0.1855, 0.0972, 0.0578, 0.0345, 0.0148) }
}
$Bands = @{
    ASIA   = @{ pp = 0.03; sameSide = 0.85; minDays = 10 }
    LONDON = @{ pp = $null; sameSide = 0.85; minDays = $null }
    NY     = @{ pp = 0.02; sameSide = 0.85; minDays = $null }
}

function Get-Fifth([double]$atr, [double[]]$edges) {
    for ($i = 0; $i -lt $edges.Count; $i++) { if ($atr -le $edges[$i]) { return $i + 1 } }
    return 5
}

$cfg = Get-Content (Join-Path $repoRoot 'settings.json') -Raw | ConvertFrom-Json
$sessions = @($cfg.session_volume.sessions | ForEach-Object { [pscustomobject]@{ name = $_.name; lo = [int]$_.start_hour; hi = [int]$_.end_hour } })
"settings.json version=$($cfg.version)  sessions: " + (($sessions | ForEach-Object { "$($_.name) h$($_.lo)-$($_.hi)" }) -join '  ')

$books = @('analysis_log.csv.v0.7.bak') +
         @(Get-ChildItem -Path $folder -Filter 'analysis_log.csv.*col-*.bak' | Sort-Object { $_.Name.Split('.')[-2] } | ForEach-Object Name) +
         @('analysis_log.csv')

# --- load: ALL weekday session rows (raw), dedup on Timestamp across books ---------------------
# Coverage (gaps, first/last) is computed over every raw session row, same as the ASIA template
# (its "asia" count). The population used for observed/expected/fires (AggrVelBurstRatio
# non-empty, ATR parses) is a per-row flag ($pop below), not a separate load, so a day with a
# few empty-ratio rows still has its true gaps measured -- population-filtering the coverage
# input itself caused H-3 to under-count covered days against the published read (escalation
# investigated, not triggered: fixed here, see spec-back).
$rows = New-Object 'System.Collections.Generic.List[object]'
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($name in $books) {
    $path = Join-Path $folder $name
    if (-not (Test-Path $path)) { throw "file not found: $path" }
    $r = [IO.File]::OpenText($path); $n = 0
    try {
        $cols = $r.ReadLine().Split(',')
        $ix = @{}; foreach ($c in 'Timestamp', 'AggrVelBurstRatio', 'AggrVelSignal', 'TFISignal', 'ATR', 'InstanceId') { $ix[$c] = [Array]::IndexOf($cols, $c); if ($ix[$c] -lt 0) { throw "$name header missing $c" } }
        while ($null -ne ($line = $r.ReadLine())) {
            if ($line -eq '') { continue }
            $p = $line.Split(','); if ($p.Length -ne $cols.Length) { continue }
            $ts = [datetime]::MinValue
            if (-not [datetime]::TryParseExact($p[$ix.Timestamp], $fmt, $inv, 'None', [ref]$ts) -or $ts -eq [datetime]::MinValue) { continue }
            if ($ts.DayOfWeek -eq 'Saturday' -or $ts.DayOfWeek -eq 'Sunday') { continue }
            if (-not $seen.Add($ts.ToString($fmt))) { continue }
            $sess = ($sessions | Where-Object { $ts.Hour -ge $_.lo -and $ts.Hour -le $_.hi } | Select-Object -First 1)
            if ($null -eq $sess) { continue }
            $ratioEmpty = ($p[$ix.AggrVelBurstRatio] -eq '')
            $atr = 0.0; $atrOk = [double]::TryParse($p[$ix.ATR], 'Float', $inv, [ref]$atr)
            $pop = (-not $ratioEmpty) -and $atrOk
            $sig = $p[$ix.AggrVelSignal]; $tfi = $p[$ix.TFISignal]
            $fire = $pop -and ($sig -ceq 'BURST_BUY' -or $sig -ceq 'BURST_SELL')
            $same = $fire -and (($sig -ceq 'BURST_BUY' -and $tfi -ceq 'BUY PRESSURE') -or ($sig -ceq 'BURST_SELL' -and $tfi -ceq 'SELL PRESSURE'))
            $rows.Add([pscustomobject]@{ ts = $ts; day = $ts.ToString('yyyy-MM-dd'); s = $sess.name; lo = $sess.lo; hi = $sess.hi; atr = $atr; pop = $pop; fire = $fire; same = $same; id = $p[$ix.InstanceId] }); $n++
        }
    } finally { $r.Close() }
    "BOOK $name weekday session rows kept=$n"
}
"total pooled weekday session rows=$($rows.Count)  (of which population rows=$(@($rows | Where-Object pop).Count))"

# ================================================================================================
if ($VerifyReference) {
    $refFromDt = [datetime]::Parse($RefFrom, $inv)
    $refToDt = [datetime]::Parse($RefTo, $inv)
    "--- -VerifyReference: recompute section 3 from raw data, $RefFrom .. $RefTo (equal-row 5 bins, same method as aggr-vel-regime-read.ps1) ---"
    $anyFail = $false
    foreach ($sName in @('ASIA', 'LONDON', 'NY')) {
        $set = @($rows | Where-Object { $_.pop -and $_.s -eq $sName -and $_.ts -ge $refFromDt -and $_.ts -le $refToDt } | Sort-Object atr)
        $m = $set.Count
        "$sName : rows=$m"
        if ($m -lt 5) { "  too few rows to bin"; $anyFail = $true; continue }
        $edges = New-Object 'double[]' 4
        $rates = New-Object 'double[]' 5
        for ($b = 0; $b -lt 5; $b++) {
            $lo = [int][Math]::Floor($b * $m / 5); $hi = [int][Math]::Floor(($b + 1) * $m / 5) - 1
            $bin = $set[$lo..$hi]
            $rates[$b] = 100.0 * (@($bin | Where-Object fire).Count) / $bin.Count
            if ($b -lt 4) { $edges[$b] = $bin[-1].atr }
        }
        $litEdges = $Ref[$sName].edges
        $litRates = @($Ref[$sName].rates | ForEach-Object { $_ * 100.0 })
        for ($i = 0; $i -lt 4; $i++) {
            $d = [Math]::Abs($edges[$i] - $litEdges[$i])
            $ok = $d -le 0.1
            if (-not $ok) { $anyFail = $true }
            "  edge $($i+1): literal={0:F1} recomputed={1:F1} diff={2:F2} {3}" -f $litEdges[$i], $edges[$i], $d, $(if ($ok) { 'OK' } else { 'FAIL' })
        }
        for ($i = 0; $i -lt 5; $i++) {
            $d = [Math]::Abs($rates[$i] - $litRates[$i])
            $ok = $d -le 0.10
            if (-not $ok) { $anyFail = $true }
            "  rate fifth $($i+1): literal={0:F2}% recomputed={1:F2}% diff={2:F2}pp {3}" -f $litRates[$i], $rates[$i], $d, $(if ($ok) { 'OK' } else { 'FAIL' })
        }
    }
    if ($anyFail) { "RESULT: FAIL"; exit 1 } else { "RESULT: PASS"; exit 0 }
}

# ================================================================================================
# normal read
$fromDt = if ($From) { [datetime]::Parse($From, $inv) } else { [datetime]::MinValue }
$maxDay = if ($rows.Count -gt 0) { ($rows | ForEach-Object day | Sort-Object -Descending | Select-Object -First 1) } else { $null }
$toDt = if ($To) { [datetime]::Parse($To, $inv) } else { if ($maxDay) { [datetime]::Parse($maxDay, $inv) } else { [datetime]::MaxValue } }

$pool = @($rows | Where-Object { $_.ts -ge $fromDt -and $_.ts -le $toDt.AddDays(1).AddSeconds(-1) })
$sessNames = if ($Session -eq 'All') { @('ASIA', 'LONDON', 'NY') } else { @($Session) }

function Get-Median($list) { if ($list.Count -eq 0) { return [double]::NaN }; $a = @($list); [Array]::Sort($a); $m = [int][Math]::Floor($a.Length / 2); if ($a.Length % 2) { $a[$m] } else { ($a[$m - 1] + $a[$m]) / 2 } }

foreach ($sName in $sessNames) {
    $sRows = @($pool | Where-Object { $_.s -eq $sName })
    $sInfo = $sessions | Where-Object { $_.name -eq $sName } | Select-Object -First 1
    $edges = $Ref[$sName].edges; $rates = $Ref[$sName].rates
    foreach ($row in $sRows) { if ($row.pop) { $row | Add-Member -NotePropertyName fifth -NotePropertyValue (Get-Fifth $row.atr $edges) -Force } }

    "";
    "=== $sName  (session hours $($sInfo.lo)-$($sInfo.hi) UTC, read window $($fromDt.ToString('yyyy-MM-dd')) .. $($toDt.ToString('yyyy-MM-dd'))) ==="
    "date       dow rows fires obs%  exp%  diff(pp) ATRmed inBand covered weekday ids"

    $byDay = $sRows | Group-Object day | Sort-Object Name
    $covered = @()
    foreach ($g in $byDay) {
        # coverage uses ALL session rows for the day (matches the ASIA template's "asia" count);
        # rate stats use only the population subset (matches its "pop" count).
        $d = @($g.Group | Sort-Object ts)
        $dPop = @($d | Where-Object pop)
        $dt = [datetime]::ParseExact($g.Name, 'yyyy-MM-dd', $inv)
        $weekday = -not ($dt.DayOfWeek -eq 'Saturday' -or $dt.DayOfWeek -eq 'Sunday')
        $sessStart = $dt.AddHours($sInfo.lo)
        $sessEnd = $dt.AddHours($sInfo.hi + 1)
        $startGap = ($d[0].ts - $sessStart).TotalMinutes
        $endGap = ($sessEnd - $d[-1].ts).TotalMinutes
        $maxGap = 0.0
        for ($i = 1; $i -lt $d.Count; $i++) { $g2 = ($d[$i].ts - $d[$i - 1].ts).TotalMinutes; if ($g2 -gt $maxGap) { $maxGap = $g2 } }
        $isCovered = ($startGap -le $MaxGapMin) -and ($endGap -le $MaxGapMin) -and ($maxGap -le $MaxGapMin)

        $n = $dPop.Count
        if ($n -eq 0) {
            "{0} {1} {2,4}     -     -     -        -      - {3,-6} {4,-7} {5,-7} {6}" -f $g.Name, $dt.DayOfWeek.ToString().Substring(0, 3), $d.Count, '-', $isCovered, $weekday, ''
            continue
        }
        $f = @($dPop | Where-Object fire).Count
        $obs = 100.0 * $f / $n
        $exp = 100.0 * (($dPop | ForEach-Object { $rates[$_.fifth - 1] } | Measure-Object -Average).Average)
        $diff = $obs - $exp
        $atrMed = Get-Median @($dPop | ForEach-Object atr)
        $band = if ($null -ne $Bands[$sName].pp) { [Math]::Abs($diff) -le (100.0 * $Bands[$sName].pp) } else { $null }
        $ids = (($d | ForEach-Object id | Select-Object -Unique) -join ',')
        $bandStr = if ($null -eq $band) { '-' } else { $band }
        $diffStr = '{0:+0.00;-0.00;0.00}' -f $diff
        "{0} {1} {2,4} {3,5} {4,5:N2} {5,5:N2} {6,8} {7,6:N1} {8,-6} {9,-7} {10,-7} {11}" -f $g.Name, $dt.DayOfWeek.ToString().Substring(0, 3), $n, $f, $obs, $exp, $diffStr, $atrMed, $bandStr, $isCovered, $weekday, $ids
        if ($weekday -and $isCovered) { $covered += , @($g.Name, $n, $f, $obs, $exp, $diff, $atrMed, $f) }
    }
    $uncov = @($byDay | Where-Object { $k = $_.Name; -not ($covered | Where-Object { $_[0] -eq $k }) })
    if ($uncov.Count -gt 0) {
        "uncovered weekday session-days:"
        foreach ($g in $uncov) {
            $dt = [datetime]::ParseExact($g.Name, 'yyyy-MM-dd', $inv)
            if ($dt.DayOfWeek -eq 'Saturday' -or $dt.DayOfWeek -eq 'Sunday') { continue }
            "  $($g.Name): not covered (gap/edge failure), rows=$($g.Group.Count)"
        }
    }

    "";
    "--- $sName slice ($($covered.Count) covered weekday session-days) ---"
    if ($covered.Count -eq 0) {
        "no covered weekday session-days in window"
        continue
    }
    $totalRows = ($covered | ForEach-Object { $_[1] } | Measure-Object -Sum).Sum
    $totalFires = ($covered | ForEach-Object { $_[2] } | Measure-Object -Sum).Sum
    $obsSlice = 100.0 * $totalFires / $totalRows
    $expSlice = ($covered | ForEach-Object { $_[1] * $_[4] } | Measure-Object -Sum).Sum / $totalRows
    $diffSlice = $obsSlice - $expSlice
    $sameOfFires = 0; $firesTotal = 0
    foreach ($g in $byDay) { if ($covered | Where-Object { $_[0] -eq $g.Name }) { $sameOfFires += @($g.Group | Where-Object { $_.fire -and $_.same }).Count; $firesTotal += @($g.Group | Where-Object fire).Count } }
    $samePct = if ($firesTotal) { 100.0 * $sameOfFires / $firesTotal } else { [double]::NaN }
    $dailyDiffs = @($covered | ForEach-Object { $_[5] })
    $meanDiff = ($dailyDiffs | Measure-Object -Average).Average
    $sdDiff = if ($dailyDiffs.Count -gt 1) { [Math]::Sqrt((($dailyDiffs | ForEach-Object { ($_ - $meanDiff) * ($_ - $meanDiff) } | Measure-Object -Sum).Sum) / ($dailyDiffs.Count - 1)) } else { 0 }

    "days=$($covered.Count)  rows=$totalRows  fires=$totalFires"
    "observed={0:N2}%  expected={1:N2}%  diff={2:+0.00;-0.00}pp" -f $obsSlice, $expSlice, $diffSlice
    "same-side={0:N2}%  daily diff mean={1:+0.00;-0.00}pp sd={2:N2}pp" -f $samePct, $meanDiff, $sdDiff

    $band = $Bands[$sName]
    if ($sName -eq 'LONDON') {
        "band: no ruled band"
        "verdict: no ruled band (report only; same-side {0:N2}% {1} 85% bar)" -f $samePct, $(if ($samePct -ge 85) { '>=' } else { '<' })
        continue
    }
    $lowerAbs = [Math]::Max(0.0, $expSlice - 100.0 * $band.pp)
    $upperAbs = $expSlice + 100.0 * $band.pp
    "band: expected {0:N2}%, band {1:N2}-{2:N2}%" -f $expSlice, $lowerAbs, $upperAbs
    if ($sName -eq 'ASIA') {
        $inBand = ($obsSlice -ge $lowerAbs) -and ($obsSlice -le $upperAbs)
        $sameSideOk = $samePct -ge (100.0 * $band.sameSide)
        $lengthOk = $covered.Count -ge $band.minDays
        $pass = $inBand -and $sameSideOk -and $lengthOk
        $failed = @()
        if (-not $inBand) { $failed += 'rate-in-band' }
        if (-not $sameSideOk) { $failed += 'same-side' }
        if (-not $lengthOk) { $failed += 'length' }
        "verdict: $(if ($pass) { 'PASS' } else { 'MISS: ' + ($failed -join ', ') })"
    } elseif ($sName -eq 'NY') {
        $inBand = ($obsSlice -ge $lowerAbs) -and ($obsSlice -le $upperAbs)
        $sameSideOk = $samePct -ge (100.0 * $band.sameSide)
        $lengthOk = $covered.Count -ge 10
        $pass = $inBand -and $sameSideOk -and $lengthOk
        $failed = @()
        if (-not $inBand) { $failed += 'rate-in-band' }
        if (-not $sameSideOk) { $failed += 'same-side' }
        if (-not $lengthOk) { $failed += 'length' }
        "verdict: $(if ($pass) { 'PASS' } else { 'MISS: ' + ($failed -join ', ') })"
    }
}
