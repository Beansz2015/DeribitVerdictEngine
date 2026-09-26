<#
.SYNOPSIS
  Aggressor-velocity burst fire rate against volatility (ATR), per session. The instrument of
  docs/aggr-vel-burst-rederivation-read-2026-09-26.md (the re-derivation read owed by
  docs/d3-asia-burst-watch-read-2026-09-14.md section 8). Read-only.

  Reads every book in a collector fetch folder: analysis_log.csv.v0.7.bak, every
  analysis_log.csv.*col-*.bak (by rotation stamp), then analysis_log.csv. Columns by header name.
  Weekday rows only (UTC), from -From (all three sessions armed by 2026-08-02).
  Session = UTC hour against tracked settings.json session_volume.sessions.
  Fire = AggrVelSignal BURST_BUY or BURST_SELL. Population = AggrVelBurstRatio non-empty.
#>
param(
    [Parameter(Mandatory = $true)][string]$FetchFolder,
    [string]$From = '2026-08-02',
    [string]$StepAt = '2026-08-20',   # the regime step found by the 2026-09-14 watch read
    [int]$Bins = 5
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$folder = if ([IO.Path]::IsPathRooted($FetchFolder)) { $FetchFolder } else { Join-Path $repoRoot $FetchFolder }
$inv = [Globalization.CultureInfo]::InvariantCulture
$fmt = 'yyyy-MM-dd HH:mm:ss'
$fromDt = [datetime]::ParseExact($From, 'yyyy-MM-dd', $inv)
$stepDt = [datetime]::ParseExact($StepAt, 'yyyy-MM-dd', $inv)

$cfg = Get-Content (Join-Path $repoRoot 'settings.json') -Raw | ConvertFrom-Json
$sessions = @($cfg.session_volume.sessions | ForEach-Object { [pscustomobject]@{ name = $_.name; lo = [int]$_.start_hour; hi = [int]$_.end_hour } })
$av = $cfg.indicators.aggressor_velocity
$thr = @{}; foreach ($s in $sessions) { $t = $av.sessions.($s.name).burst_ratio_threshold; $thr[$s.name] = if ($null -ne $t) { [double]$t } else { [double]$av.default.burst_ratio_threshold } }
"settings.json version=$($cfg.version)  thresholds: " + (($sessions | ForEach-Object { "$($_.name)=$($thr[$_.name]) h$($_.lo)-$($_.hi)" }) -join '  ')

$books = @('analysis_log.csv.v0.7.bak') +
         @(Get-ChildItem -Path $folder -Filter 'analysis_log.csv.*col-*.bak' | Sort-Object { $_.Name.Split('.')[-2] } | ForEach-Object Name) +
         @('analysis_log.csv')

# rows: session, day, atr, ratio, fire, same
$rows = New-Object 'System.Collections.Generic.List[object]'
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($name in $books) {
    $path = Join-Path $folder $name
    if (-not (Test-Path $path)) { throw "file not found: $path" }
    $r = [IO.File]::OpenText($path); $n = 0
    try {
        $cols = $r.ReadLine().Split(',')
        $ix = @{}; foreach ($c in 'Timestamp', 'AggrVelBurstRatio', 'AggrVelSignal', 'TFISignal', 'ATR') { $ix[$c] = [Array]::IndexOf($cols, $c); if ($ix[$c] -lt 0) { throw "$name header missing $c" } }
        while ($null -ne ($line = $r.ReadLine())) {
            $p = $line.Split(','); if ($p.Length -ne $cols.Length) { continue }
            $ts = [datetime]::MinValue
            if (-not [datetime]::TryParseExact($p[$ix.Timestamp], $fmt, $inv, 'None', [ref]$ts)) { continue }
            if ($ts -lt $fromDt -or $ts.DayOfWeek -eq 'Saturday' -or $ts.DayOfWeek -eq 'Sunday') { continue }
            if ($p[$ix.AggrVelBurstRatio] -eq '') { continue }
            if (-not $seen.Add($ts.ToString($fmt))) { continue }
            $sess = ($sessions | Where-Object { $ts.Hour -ge $_.lo -and $ts.Hour -le $_.hi } | Select-Object -First 1)
            if ($null -eq $sess) { continue }
            $atr = 0.0; if (-not [double]::TryParse($p[$ix.ATR], 'Float', $inv, [ref]$atr)) { continue }
            $sig = $p[$ix.AggrVelSignal]; $tfi = $p[$ix.TFISignal]
            $fire = ($sig -ceq 'BURST_BUY' -or $sig -ceq 'BURST_SELL')
            $same = ($sig -ceq 'BURST_BUY' -and $tfi -ceq 'BUY PRESSURE') -or ($sig -ceq 'BURST_SELL' -and $tfi -ceq 'SELL PRESSURE')
            $rows.Add([pscustomobject]@{ s = $sess.name; day = $ts.ToString('yyyy-MM-dd'); post = ($ts -ge $stepDt); atr = $atr; ratio = [double]::Parse($p[$ix.AggrVelBurstRatio], $inv); fire = $fire; same = $same }); $n++
        }
    } finally { $r.Close() }
    "BOOK $name weekday population rows kept=$n"
}

function Q($sorted, [double]$q) { if ($sorted.Count -eq 0) { return [double]::NaN }; $sorted[[int][Math]::Min($sorted.Count - 1, [Math]::Floor($q * $sorted.Count))] }
function Stats($set, $T) {
    $n = @($set).Count; if ($n -eq 0) { return 'n=0' }
    $f = @($set | Where-Object fire).Count; $sm = @($set | Where-Object same).Count
    $rs = [double[]]@($set | ForEach-Object ratio); [Array]::Sort($rs)
    $as = [double[]]@($set | ForEach-Object atr); [Array]::Sort($as)
    '{0,6} {1,7:F2}% {2,7:F1}% {3,7:F1} {4,6:F2} {5,6:F2} {6,6:F2} {7,6:F2}' -f $n, (100.0 * $f / $n), $(if ($f) { 100.0 * $sm / $f } else { [double]::NaN }), (Q $as 0.5), (Q $rs 0.5), (Q $rs 0.75), (Q $rs 0.9), (Q $rs 0.95)
}
$hdr = '{0,-26} {1,6} {2,8} {3,8} {4,7} {5,6} {6,6} {7,6} {8,6}' -f 'slice', 'rows', 'fire', 'same', 'ATRmed', 'r.p50', 'r.p75', 'r.p90', 'r.p95'

foreach ($s in $sessions) {
    $set = @($rows | Where-Object { $_.s -eq $s.name })
    ""; "=== $($s.name)  threshold=$($thr[$s.name])  (ratio percentiles are of AggrVelBurstRatio) ==="
    $hdr
    ('{0,-26} ' -f "before $StepAt") + (Stats @($set | Where-Object { -not $_.post }) $thr[$s.name])
    ('{0,-26} ' -f "from $StepAt") + (Stats @($set | Where-Object post) $thr[$s.name])
    foreach ($part in @(@{ lab = 'all'; set = $set }, @{ lab = "from $StepAt"; set = @($set | Where-Object post) })) {
        $ps = @($part.set | Sort-Object atr); $m = $ps.Count; if ($m -lt $Bins) { continue }
        "  ATR bins ($Bins, equal rows) over $($part.lab):"
        for ($b = 0; $b -lt $Bins; $b++) {
            $lo = [int][Math]::Floor($b * $m / $Bins); $hi = [int][Math]::Floor(($b + 1) * $m / $Bins) - 1
            $bin = $ps[$lo..$hi]
            ('  {0,-24} ' -f ('ATR {0:F1}-{1:F1}' -f $bin[0].atr, $bin[-1].atr)) + (Stats $bin $thr[$s.name])
        }
    }
    # daily design effect, from the step on
    $daily = @($set | Where-Object post | Group-Object day | ForEach-Object { $g = @($_.Group); [pscustomobject]@{ n = $g.Count; rate = @($g | Where-Object fire).Count / [double]$g.Count; atr = ((@($g | ForEach-Object atr) | Sort-Object)[[int]($g.Count / 2)]) } })
    if ($daily.Count -ge 2) {
        $mean = ($daily | Measure-Object rate -Average).Average
        $sd = [Math]::Sqrt((($daily | ForEach-Object { ($_.rate - $mean) * ($_.rate - $mean) }) | Measure-Object -Sum).Sum / ($daily.Count - 1))
        $nbar = ($daily | Measure-Object n -Average).Average
        $bin = [Math]::Sqrt($mean * (1 - $mean) / $nbar)
        $mx = ($daily | Measure-Object atr -Average).Average; $my = $mean
        $cov = ($daily | ForEach-Object { ($_.atr - $mx) * ($_.rate - $my) } | Measure-Object -Sum).Sum
        $vx = ($daily | ForEach-Object { ($_.atr - $mx) * ($_.atr - $mx) } | Measure-Object -Sum).Sum
        $vy = ($daily | ForEach-Object { ($_.rate - $my) * ($_.rate - $my) } | Measure-Object -Sum).Sum
        '  daily from {0}: days={1} mean={2:F2}% sd={3:F2}pp binomial sd={4:F2}pp design effect(sd ratio)={5:F2}  r(daily ATRmed, daily rate)={6:F2}' -f $StepAt, $daily.Count, (100 * $mean), (100 * $sd), (100 * $bin), ($sd / $bin), ($cov / [Math]::Sqrt($vx * $vy))
    }
}
