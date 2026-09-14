<#
.SYNOPSIS
  ASIA aggressor-velocity burst-watch read - fire rate and same-side TFI share over
  fully-covered weekday ASIA session-days, from a local collector fetch folder.

.DESCRIPTION
  The watch (docs/d3-asia-burst-watch-read-2026-08-10.md, section 10, rulings T-1..T-5):
    reference 11.0 %, band 8-14 %, read length >= 10 weekday session-days,
    same-side TFI share >= 85 %. A miss fires a re-derivation READ, never a threshold change.

  READ ONLY. It reads two CSVs from a fetch folder and writes nothing.

      <FetchFolder>\analysis_log.csv.v0.7.bak   (closed at the 2026-09-01 rotation)
    + <FetchFolder>\analysis_log.csv            (live)

  POPULATION RULE - taken from the template (docs/d3-asia-burst-watch-read-2026-08-10.md,
  section 1) and not changed:
    ASIA        = UTC hours 00-07 inclusive (settings.json session_volume.sessions[ASIA])
    weekday     = UTC day-of-week Mon-Fri (the CSV Timestamp is UTC; never the box locale)
    population  = AggrVelBurstRatio non-empty
    fire        = AggrVelSignal in {BURST_BUY, BURST_SELL}
    same-side   = BURST_BUY with TFISignal "BUY PRESSURE", BURST_SELL with "SELL PRESSURE"
    contra      = the opposite pressure; TFI NEUTRAL counts as neither

  COVERAGE RULE (a session-day with a capture hole is excluded, as the template's
  downtime section did): the day must have ASIA rows at or before 00:MaxGapMin, at or after
  07:(60-MaxGapMin), and no gap between consecutive ASIA rows longer than MaxGapMin minutes.
  At ExecResolution 3 the theoretical count is 160 rows per session.

  TRAPS GUARDED
    1. Both files are counted. Their spans are checked for overlap, and duplicate ASIA
       timestamps across the pair are counted and reported.
    2. No InstanceId filter. Every id seen is listed with its first/last stamp so it can be
       diffed against docs/aws-collector-deploy-checklist.md "Version <-> InstanceId ledger".
    3. ParseExact with InvariantCulture; DateTime.MinValue is rejected explicitly
       (its DayOfWeek is Monday, so it would pass a naive weekday test).
    4. Columns are located by HEADER NAME, not index: the .bak has 111 columns and the
       live file 116. A row whose field count differs from its header is counted and
       skipped, so a quoted comma cannot silently shift a column.
    5. Arming check independent of the ledger, in two kinds that mean different things:
         fireBelow   - BURST_* with AggrVelBurstRatio < Threshold. Evidence of a different
                       (lower) threshold or a disarm. Must be 0.
         normalAbove - NORMAL with AggrVelBurstRatio >= Threshold. EXPECTED in small numbers:
                       IndicatorEngine.ClassifyAggressorBurst also requires
                       |lean| >= direction_lean_floor, and lean is not logged.
       The minimum ratio among fires is printed per slice; it must sit at or just above
       Threshold.

  -SplitAt adds extra slices on covered weekday session-days after FirstReadEnd, cut at each
  given date (a day equal to the cut goes to the later slice). ATRmed per day (median ATR
  over the ASIA population rows) is printed so a regime edge can be seen next to the rate.

.PARAMETER FetchFolder
  A dated folder under aws_fetch/ produced by tools/ops/collector.ps1 fetch.
  Do NOT point this at the root analysis_log_aws.csv - fetch overwrites it.

.EXAMPLE
  powershell -File tools/ops/asia-burst-watch-read.ps1 -FetchFolder aws_fetch\20260913-153704
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FetchFolder,

    # Burst threshold. SHIPPED BEHAVIOUR - derived from the tracked settings.json unless
    # overridden, so the arming check follows the shipped value.
    [double]$Threshold = [double]::NaN,

    # First full armed ASIA session. v65 went live 2026-08-01 19:02:31 UTC (deploy ledger).
    [string]$ArmedFrom = '2026-08-02',

    # Last day of the first read's window. Slice (b) = days strictly after it.
    [string]$FirstReadEnd = '2026-08-07',

    [int]$MaxGapMin = 6,

    [string[]]$SplitAt = @(),

    # MEASUREMENT OF RECORD from the first read (docs/d3-asia-burst-watch-read-2026-08-10.md,
    # sections 0 and 10): 12 fully-covered AWS weekday session-days. Literals by intent.
    [int]$BaselineRows = 1905,
    [double]$BaselineRate = 0.1097,

    # Ruled tolerance T-1..T-4. Literals by intent - a ruling, not a settings key.
    [double]$Reference = 0.110,
    [double]$BandLo = 0.08,
    [double]$BandHi = 0.14,
    [int]$MinDays = 10,
    [double]$MinSameSide = 0.85
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$folder = if ([IO.Path]::IsPathRooted($FetchFolder)) { $FetchFolder } else { Join-Path $repoRoot $FetchFolder }

if ([double]::IsNaN($Threshold)) {
    $cfg = Get-Content (Join-Path $repoRoot 'settings.json') -Raw | ConvertFrom-Json
    $Threshold = [double]$cfg.indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold
    $asiaSess = $cfg.session_volume.sessions | Where-Object { $_.name -eq 'ASIA' }
    "settings.json version=$($cfg.version) ASIA burst_ratio_threshold=$Threshold ASIA hours=$($asiaSess.start_hour)-$($asiaSess.end_hour)"
}

$ci  = [Globalization.CultureInfo]::InvariantCulture
$fmt = 'yyyy-MM-dd HH:mm:ss'
$inv = [Globalization.CultureInfo]::InvariantCulture

$days = @{}        # yyyy-MM-dd -> stats hashtable
$ids  = [ordered]@{}
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
$dupAsia = 0
$spans = @()

function New-Day { @{ asia = 0; pop = 0; fires = 0; same = 0; contra = 0; neutral = 0; res3 = 0; resOther = 0;
                      fireBelow = 0; normalAbove = 0; minFireRatio = [double]::MaxValue; first = $null; last = $null; maxGap = 0.0; ids = @{};
                      atr = (New-Object 'System.Collections.Generic.List[double]') } }

function Get-Median($list) { if ($list.Count -eq 0) { return [double]::NaN }; $a = $list.ToArray(); [Array]::Sort($a); $m = [int][Math]::Floor($a.Length / 2); if ($a.Length % 2) { $a[$m] } else { ($a[$m - 1] + $a[$m]) / 2 } }

foreach ($name in 'analysis_log.csv.v0.7.bak', 'analysis_log.csv') {
    $path = Join-Path $folder $name
    if (-not (Test-Path $path)) { throw "file not found: $path" }
    $rows = 0; $badShape = 0; $unparsed = 0; $fFirst = $null; $fLast = $null
    $r = [IO.File]::OpenText($path)
    try {
        $cols = $r.ReadLine().Split(',')
        $ix = @{}
        foreach ($c in 'Timestamp', 'ExecResolution', 'AggrVelBurstRatio', 'AggrVelSignal', 'TFISignal', 'InstanceId', 'ATR') {
            $ix[$c] = [Array]::IndexOf($cols, $c)
            if ($ix[$c] -lt 0) { throw "$name header missing $c" }
        }
        while ($null -ne ($line = $r.ReadLine())) {
            if ($line -eq '') { continue }
            $p = $line.Split(',')
            if ($p.Length -ne $cols.Length) { $badShape++; continue }
            $rows++
            $ts = [datetime]::MinValue
            if (-not [datetime]::TryParseExact($p[$ix.Timestamp], $fmt, $ci, 'None', [ref]$ts) -or $ts -eq [datetime]::MinValue) { $unparsed++; continue }
            if ($null -eq $fFirst) { $fFirst = $ts }; $fLast = $ts

            $id = $p[$ix.InstanceId]
            if (-not $ids.Contains($id)) { $ids[$id] = @{ first = $ts; last = $ts; rows = 0; file = $name } }
            $ids[$id].last = $ts; $ids[$id].rows++

            if ($ts.Hour -gt 7) { continue }
            $k = $ts.ToString('yyyy-MM-dd')
            if (-not $seen.Add($ts.ToString($fmt))) { $dupAsia++; continue }
            if (-not $days.ContainsKey($k)) { $days[$k] = New-Day }
            $d = $days[$k]
            $d.asia++
            $d.ids[$id] = 1
            if ($null -ne $d.last) { $g = ($ts - $d.last).TotalMinutes; if ($g -gt $d.maxGap) { $d.maxGap = $g } }
            if ($null -eq $d.first) { $d.first = $ts }; $d.last = $ts
            if ($p[$ix.ExecResolution] -eq '3') { $d.res3++ } else { $d.resOther++ }

            $br = $p[$ix.AggrVelBurstRatio]
            if ($br -eq '') { continue }
            $d.pop++
            $ratio = [double]::Parse($br, $inv)
            $atrV = 0.0; if ([double]::TryParse($p[$ix.ATR], 'Float', $inv, [ref]$atrV)) { $d.atr.Add($atrV) }
            $sig = $p[$ix.AggrVelSignal]
            $isFire = ($sig -ceq 'BURST_BUY' -or $sig -ceq 'BURST_SELL')
            if ($isFire -and $ratio -lt $Threshold) { $d.fireBelow++ }
            if (-not $isFire -and $ratio -ge $Threshold) { $d.normalAbove++ }
            if (-not $isFire) { continue }
            $d.fires++
            if ($ratio -lt $d.minFireRatio) { $d.minFireRatio = $ratio }
            $tfi = $p[$ix.TFISignal]
            if (($sig -ceq 'BURST_BUY' -and $tfi -ceq 'BUY PRESSURE') -or ($sig -ceq 'BURST_SELL' -and $tfi -ceq 'SELL PRESSURE')) { $d.same++ }
            elseif ($tfi -ceq 'BUY PRESSURE' -or $tfi -ceq 'SELL PRESSURE') { $d.contra++ }
            else { $d.neutral++ }
        }
    } finally { $r.Close() }
    "FILE $name rows=$rows badShape=$badShape unparsed=$unparsed span=$($fFirst.ToString($fmt)) -> $($fLast.ToString($fmt))"
    $spans += , @($fFirst, $fLast)
}
if ($spans[1][0] -le $spans[0][1]) { Write-Warning "SPANS OVERLAP - .bak ends $($spans[0][1]) but live starts $($spans[1][0])" }
else { "spans do not overlap (.bak ends $($spans[0][1].ToString($fmt)), live starts $($spans[1][0].ToString($fmt)))" }
"duplicate ASIA timestamps skipped: $dupAsia"

""
"--- InstanceIds (all hours) ---"
foreach ($e in $ids.GetEnumerator()) { "{0}  {1} -> {2}  rows={3}  ({4})" -f $e.Key, $e.Value.first.ToString($fmt), $e.Value.last.ToString($fmt), $e.Value.rows, $e.Value.file }

""
"--- per ASIA session-day from $ArmedFrom (UTC) ---"
"day        dow asia pop fires rate%  same contra neut res!=3 fireBelow normalAbove minFire ATRmed maxGap first    last     covered weekday ids"
$sel = @()
foreach ($k in ($days.Keys | Where-Object { $_ -ge $ArmedFrom } | Sort-Object)) {
    $d = $days[$k]
    $dt = [datetime]::ParseExact($k, 'yyyy-MM-dd', $ci)
    $weekday = -not ($dt.DayOfWeek -eq 'Saturday' -or $dt.DayOfWeek -eq 'Sunday')
    $startGap = ($d.first - $dt).TotalMinutes
    $endGap = ($dt.AddHours(8) - $d.last).TotalMinutes
    $covered = ($startGap -le $MaxGapMin) -and ($endGap -le $MaxGapMin) -and ($d.maxGap -le $MaxGapMin)
    $rate = if ($d.pop) { 100.0 * $d.fires / $d.pop } else { 0 }
    $minFire = if ($d.fires) { '{0,7:N3}' -f $d.minFireRatio } else { '      -' }
    "{0} {1} {2,4} {3,3} {4,5} {5,5:N2} {6,5} {7,6} {8,4} {9,6} {10,9} {11,11} {12} {13,6:N1} {14,6:N1} {15} {16} {17,-7} {18,-7} {19}" -f $k, $dt.DayOfWeek.ToString().Substring(0, 3), $d.asia, $d.pop, $d.fires, $rate, $d.same, $d.contra, $d.neutral, $d.resOther, $d.fireBelow, $d.normalAbove, $minFire, (Get-Median $d.atr), $d.maxGap, $d.first.ToString('HH:mm:ss'), $d.last.ToString('HH:mm:ss'), $covered, $weekday, (($d.ids.Keys | ForEach-Object { $_.Substring(0, 8) }) -join ',')
    if ($weekday -and $covered) { $sel += , @($k, $d) }
}

function Write-Slice {
    param([string]$Label, $Items)
    $n = $Items.Count
    if ($n -eq 0) { "$Label : no qualifying days"; return }
    $pop = 0; $f = 0; $s = 0; $c = 0; $nu = 0; $rates = @(); $atrs = @(); $minF = [double]::MaxValue
    foreach ($it in $Items) { $d = $it[1]; $pop += $d.pop; $f += $d.fires; $s += $d.same; $c += $d.contra; $nu += $d.neutral; $rates += ($d.fires / $d.pop); $atrs += (Get-Median $d.atr); if ($d.fires -and $d.minFireRatio -lt $minF) { $minF = $d.minFireRatio } }
    $rate = $f / $pop
    $same = if ($f) { $s / $f } else { 0 }
    $mean = ($rates | Measure-Object -Average).Average
    $sd = if ($n -gt 1) { [Math]::Sqrt((($rates | ForEach-Object { ($_ - $mean) * ($_ - $mean) } | Measure-Object -Sum).Sum) / ($n - 1)) } else { 0 }
    $seRow = [Math]::Sqrt($Reference * (1 - $Reference) / $pop)
    $pp = ($f + $BaselineRate * $BaselineRows) / ($pop + $BaselineRows)
    $z2 = ($rate - $BaselineRate) / [Math]::Sqrt($pp * (1 - $pp) * (1.0 / $pop + 1.0 / $BaselineRows))
    $tDay = if ($sd -gt 0) { ($mean - $Reference) / ($sd / [Math]::Sqrt($n)) } else { 0 }
    ""
    "=== $Label ==="
    "days=$n ($($Items[0][0]) .. $($Items[-1][0]))  pop rows=$pop  rows/day={0:N1}" -f ($pop / $n)
    "fires=$f  FIRE RATE={0:P2}  band {1:P0}-{2:P0}  ref {3:P1}" -f $rate, $BandLo, $BandHi, $Reference
    "same=$s  SAME-SIDE={0:P2}  (bar {1:P0})  contra=$c ({2:P2})  neutral=$nu ({3:P2})  contra/day={4:N2}" -f $same, $MinSameSide, ($c / [Math]::Max($f, 1)), ($nu / [Math]::Max($f, 1)), ($c / $n)
    "daily rate mean={0:P2} sd={1:N2}pp  day-level t vs ref={2:N2} on {3} df  row-level z vs ref={4:N2}" -f $mean, (100 * $sd), $tDay, ($n - 1), (($rate - $Reference) / $seRow)
    "like-for-like vs first-read AWS baseline {0:P2} (n=$BaselineRows): diff={1:+0.00;-0.00}pp two-proportion z={2:N2}" -f $BaselineRate, (100 * ($rate - $BaselineRate)), $z2
    $ma = ($atrs | Measure-Object -Average).Average
    $corr = if ($n -gt 2 -and $sd -gt 0) {
        $sxy = 0.0; $sxx = 0.0; for ($i = 0; $i -lt $n; $i++) { $sxy += ($atrs[$i] - $ma) * ($rates[$i] - $mean); $sxx += ($atrs[$i] - $ma) * ($atrs[$i] - $ma) }
        $sxy / [Math]::Sqrt($sxx * (($rates | ForEach-Object { ($_ - $mean) * ($_ - $mean) } | Measure-Object -Sum).Sum))
    } else { [double]::NaN }
    "min fire ratio={0:N3} (threshold $Threshold)  mean daily ATRmed={1:N1}  Pearson r(daily ATRmed, daily rate)={2:N2}" -f $minF, $ma, $corr
    $pass = ($rate -ge $BandLo) -and ($rate -le $BandHi) -and ($same -ge $MinSameSide) -and ($n -ge $MinDays)
    "criteria: rateInBand=$(($rate -ge $BandLo) -and ($rate -le $BandHi)) sameSide=$($same -ge $MinSameSide) length=$($n -ge $MinDays)  => $(if ($pass) { 'PASS' } else { 'MISS' })"
}

$armed = @($days.Keys | Where-Object { $_ -ge $ArmedFrom })
$fb = ($armed | ForEach-Object { $days[$_].fireBelow } | Measure-Object -Sum).Sum
$na = ($armed | ForEach-Object { $days[$_].normalAbove } | Measure-Object -Sum).Sum
""
"arming check (armed days, all ASIA population rows incl. weekend): fireBelow=$fb (must be 0)  normalAbove=$na (lean-floor gate, expected)"

Write-Slice -Label "(a) all post-v65 covered weekday session-days" -Items $sel
$after = @($sel | Where-Object { $_[0] -gt $FirstReadEnd })
Write-Slice -Label "(b) covered weekday session-days after $FirstReadEnd" -Items $after
$cuts = @($SplitAt | Sort-Object)
$lo = $null
foreach ($cut in ($cuts + @($null))) {
    $part = @($after | Where-Object { ($null -eq $lo -or $_[0] -ge $lo) -and ($null -eq $cut -or $_[0] -lt $cut) })
    $label = "(split) covered weekday session-days after $FirstReadEnd" + $(if ($lo) { ", from $lo" } else { '' }) + $(if ($cut) { ", before $cut" } else { '' })
    if ($cuts.Count) { Write-Slice -Label $label -Items $part }
    $lo = $cut
}
