<#
.SYNOPSIS
  Burst outcome read, step 1: the POWER COUNT. Counts only. Read-only. It computes NO outcome.
  Owed by docs/trader-tick-queue.md section 4 (the AVR-1/AVR-2 line); spec docs/burst-outcome-read-spec.md.

.DESCRIPTION
  PRE-REGISTRATION GUARD. This script reads signal-time fields only: Timestamp, Price, Verdict, ATR,
  AggrVelBurstRatio, AggrVelNet, AggrVelSignal, TFISignal, MaxScore, EffectiveLongScore,
  EffectiveShortScore, Placed* (for the validity filter only). It never opens analysis_eval_cache.csv,
  never reads a candle, and never computes a success rate, a result or a PnL. Do not add any of these
  here: the outcome belongs to the pre-registered read in docs/burst-outcome-read-spec.md.

  LOADER (copied from tools/ops/burst-watch-read.ps1, the RULED reference for rows, extended only by
  the extra columns named above): books oldest first -- analysis_log.csv.v0.7.bak, every
  analysis_log.csv.*col-*.bak sorted by rotation stamp, then analysis_log.csv. Weekday (UTC
  day-of-week) rows only. Dedup on Timestamp across books, first seen wins, BEFORE any other filter.
  Session = UTC hour against tracked settings.json session_volume.sessions. Population flag
  = AggrVelBurstRatio non-empty AND ATR parses. ATR fifth = the RULED edges of burst-watch-read.ps1.

  DIRECTIONAL (the outcome instrument's population, tools/ops/SwingFallbackRead/SwingFallbackRead.vb
  and the analysis/BandLadder.vb convention): Verdict in {STRONG LONG, LONG, WEAK LONG, STRONG SHORT,
  SHORT, WEAK SHORT}. Every NO TRADE form, including lean forms "NO TRADE [WEAK LONG]", is excluded.
  Band = STRONG / MEDIUM / WEAK. Valid placed levels = the SwingFallbackRead filter: Price > 0, ATR > 0,
  the verdict side's PlacedTarget and PlacedStop > 0, target beyond entry and stop behind it.

  ARMS, from the verdict side X (Core/ScoringEngine_Calculate_Scoring.vb Step 2, the v52 wire-in: the
  modifier acts on the TFI side's score, only when TFI is directional and the session is armed):
    A  TFI on X and AggrVelSignal on X   -> +upgrade_bonus was applied to X's score (burst-upgraded)
    B  TFI on X and AggrVelSignal NORMAL -> no modifier (the comparison arm)
    C  TFI on X and AggrVelSignal on the other side -> -contra_penalty applied to X (contra)
    O  TFI not on X (NEUTRAL or opposite) -> the modifier did not touch X; Oup = TFI opposite X with a
       same-side burst, so the +1 went to the side the verdict did NOT take
  SHADOW ARMS (option (c) counterfactual; literal per-fifth thresholds below):
    A_keep  A rows with ratio >= the fifth's shadow threshold   (a volatility-conditional threshold keeps them)
    A_drop  A rows with ratio <  the fifth's shadow threshold   (it would drop them)
    S_add   B rows with ratio >= the fifth's shadow threshold and sign(AggrVelNet) on X (it would add them;
            the lean floor cannot be checked -- lean is not logged -- so S_add is a SUPERSET)
    B_lean  B rows with ratio >= the session's fixed threshold: the lean floor rejected them (diagnostic)
  CROSSED: A rows whose effective score on X minus upgrade_bonus falls into a lower band than the logged
  one (the docs/medium-tier-diagnosis-read-2026-09-17.md D-4 counterfactual; the regimeMax cap is ignored).

  ERAS (dates of record, not settings thresholds):
    ASIA armed   2026-08-01 19:02:31 UTC  (docs/aws-collector-deploy-checklist.md section 5a, id 09c747f8)
                 NY (v52, 2026-07-14) and LONDON (v60, commit 631a3f5 2026-07-22 17:35) are armed on every
                 row of these books: the collector's first row is 2026-07-22 16:24 and its first LONDON
                 row is 2026-07-23.
    E1 armed -> v66 OBV scoring edge 2026-08-10 18:36:01 (first v66 row, id 3be7f4c9)
    E2 v66 -> ATR regime step 2026-08-20 00:00 (docs/aggr-vel-burst-rederivation-read-2026-09-26.md)
    E3 2026-08-20 -> POC-gate fix 2026-09-24 18:46:06 (first row of id 25951567)
    E4 from the POC-gate fix

.PARAMETER FetchFolder
  A dated folder under aws_fetch/ produced by tools/ops/collector.ps1 fetch.
.PARAMETER RunDate
  Projected run date (UTC). The script projects arm n per cell to it at the post-2026-08-20 weekday
  rate and prints the MDE at that n.
.PARAMETER MinN
  Readable minimum n per arm per cell (default 100, the census rule of
  tools/ops/medium_tier_diagnosis.py and tools/ops/q1d_tier_geometry.py).

.EXAMPLE
  powershell -NoProfile -File tools/ops/burst-outcome-power-count.ps1 -FetchFolder aws_fetch\20260928-121255 -RunDate 2026-11-02
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FetchFolder,
    [string]$RunDate,
    [int]$MinN = 100,
    [string]$AccrualFrom = '2026-08-20'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$folder = if ([IO.Path]::IsPathRooted($FetchFolder)) { $FetchFolder } else { Join-Path $repoRoot $FetchFolder }
$inv = [Globalization.CultureInfo]::InvariantCulture
$fmt = 'yyyy-MM-dd HH:mm:ss'

# --- RULED ATR-fifth edges: copied from tools/ops/burst-watch-read.ps1 $Ref (AVR-1, 2026-09-26). ---
# MECHANISM-class literals under the fixture-literal provenance rule: a frozen measurement of record
# (docs/aggr-vel-burst-rederivation-read-2026-09-26.md section 2.2). Must equal burst-watch-read.ps1.
$Edges = @{
    ASIA   = @(33.6, 51.2, 66.2, 83.9)
    LONDON = @(31.9, 51.5, 68.4, 90.7)
    NY     = @(19.3, 30.9, 43.0, 62.9)
}
# --- Shadow thresholds for option (c): AggrVelBurstRatio p90 per session x ATR fifth, "all rows" bins,
# 2026-08-02 .. 2026-09-25 08:51. Source: tools/ops/aggr-vel-regime-read.ps1 -FetchFolder
# aws_fetch\20260925-085341 (docs/aggr-vel-burst-rederivation-read-2026-09-26.md section 2.2, which
# prints ASIA, NY and LONDON fifths 1 and 5; LONDON fifths 2-4 from the same run, re-run 2026-09-28).
# MECHANISM-class: a frozen measurement, the p90 knee rule that set the fixed thresholds, applied per fifth.
$Shadow = @{
    ASIA   = @(7.41, 4.92, 3.88, 3.31, 2.62)
    LONDON = @(9.21, 5.71, 3.51, 3.50, 2.31)
    NY     = @(7.17, 4.46, 3.40, 2.90, 2.23)
}
# --- Published per-trade spread of net EV per trade, main window, maker/maker. NOT derived from new data.
# Source: docs/swing-vs-fallback-target-read-2026-09-15.md section 4.1, tier ALL rows: n and the 95 %
# CI of net EV per trade (cluster-robust by session-day, so the design effect is inside it).
# sigma_eff = (CI hi - CI lo) / (2 x 1.96) x sqrt(n). Per session the larger of the "none" and "swing"
# rows is used (conservative). MECHANISM-class literals: quoted measurements, not settings.
$Published = @{
    NY     = @(@{ tgt = 'none'; n = 2852; lo = -5.0; hi = -2.6 }, @{ tgt = 'swing'; n = 1758; lo = -4.9; hi = -2.0 })
    LONDON = @(@{ tgt = 'none'; n = 891; lo = -2.3; hi = 2.2 }, @{ tgt = 'swing'; n = 642; lo = -4.5; hi = -0.2 })
    ASIA   = @(@{ tgt = 'none'; n = 734; lo = -4.5; hi = -0.4 }, @{ tgt = 'swing'; n = 846; lo = -4.1; hi = 0.5 })
}
$AsiaArmed = [datetime]::ParseExact('2026-08-01 19:02:31', $fmt, $inv)
$EraEdges = @(
    @{ name = 'E1'; to = [datetime]::ParseExact('2026-08-10 18:36:01', $fmt, $inv) },
    @{ name = 'E2'; to = [datetime]::ParseExact('2026-08-20 00:00:00', $fmt, $inv) },
    @{ name = 'E3'; to = [datetime]::ParseExact('2026-09-24 18:46:06', $fmt, $inv) },
    @{ name = 'E4'; to = [datetime]::MaxValue }
)
$RefEnd = [datetime]::ParseExact('2026-09-12 00:00:00', $fmt, $inv)   # the swing read's population ends 2026-09-11
$accrualFromDt = [datetime]::ParseExact($AccrualFrom, 'yyyy-MM-dd', $inv)

function Get-Fifth([double]$atr, [double[]]$edges) {
    for ($i = 0; $i -lt $edges.Count; $i++) { if ($atr -le $edges[$i]) { return $i + 1 } }
    return 5
}
function Get-Era([datetime]$ts) { foreach ($e in $EraEdges) { if ($ts -lt $e.to) { return $e.name } } }
function Get-Median($list) { if ($list.Count -eq 0) { return [double]::NaN }; $a = [double[]]@($list); [Array]::Sort($a); $m = [int][Math]::Floor($a.Length / 2); if ($a.Length % 2) { $a[$m] } else { ($a[$m - 1] + $a[$m]) / 2 } }

$cfg = Get-Content (Join-Path $repoRoot 'settings.json') -Raw | ConvertFrom-Json
$sessions = @($cfg.session_volume.sessions | ForEach-Object { [pscustomobject]@{ name = $_.name; lo = [int]$_.start_hour; hi = [int]$_.end_hour } })
$av = $cfg.indicators.aggressor_velocity
$fixedThr = @{}; foreach ($s in 'ASIA', 'LONDON', 'NY') { $fixedThr[$s] = [double]$av.sessions.$s.burst_ratio_threshold }
$bonus = [int]$av.upgrade_bonus
$pct = @{ STRONG = [double]$cfg.scoring.verdict_strong_pct; MEDIUM = [double]$cfg.scoring.verdict_med_pct; WEAK = [double]$cfg.scoring.verdict_weak_pct }
"settings.json version=$($cfg.version)  sessions: " + (($sessions | ForEach-Object { "$($_.name) h$($_.lo)-$($_.hi)" }) -join '  ')
"fixed thresholds: ASIA=$($fixedThr.ASIA) LONDON=$($fixedThr.LONDON) NY=$($fixedThr.NY)  upgrade_bonus=$bonus contra_penalty=$($av.contra_penalty) scoring_enabled=$($av.scoring_enabled)"
"band pct (current settings, used for CROSSED only): strong=$($pct.STRONG) med=$($pct.MEDIUM) weak=$($pct.WEAK)"

$books = @('analysis_log.csv.v0.7.bak') +
         @(Get-ChildItem -Path $folder -Filter 'analysis_log.csv.*col-*.bak' | Sort-Object { $_.Name.Split('.')[-2] } | ForEach-Object Name) +
         @('analysis_log.csv')

$dirSet = @{ 'STRONG LONG' = @('LONG', 'STRONG'); 'LONG' = @('LONG', 'MEDIUM'); 'WEAK LONG' = @('LONG', 'WEAK');
             'STRONG SHORT' = @('SHORT', 'STRONG'); 'SHORT' = @('SHORT', 'MEDIUM'); 'WEAK SHORT' = @('SHORT', 'WEAK') }
$bandRank = @{ 'BELOW' = 0; 'WEAK' = 1; 'MEDIUM' = 2; 'STRONG' = 3 }
function Get-BandOf([int]$score, [int]$maxScore) {
    if ($score -ge [int][Math]::Ceiling($maxScore * $pct.STRONG)) { return 'STRONG' }
    if ($score -ge [int][Math]::Ceiling($maxScore * $pct.MEDIUM)) { return 'MEDIUM' }
    if ($score -ge [int][Math]::Ceiling($maxScore * $pct.WEAK)) { return 'WEAK' }
    return 'BELOW'
}

# --- load (burst-watch-read.ps1 loader, extended columns) ---------------------------------------
$rows = New-Object 'System.Collections.Generic.List[object]'
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
$x = @{ total = 0; pop = 0; nonDir = 0; lean = 0; badLevels = 0; asiaUnarmed = 0 }
$need = 'Timestamp', 'Price', 'Verdict', 'ATR', 'AggrVelBurstRatio', 'AggrVelNet', 'AggrVelSignal', 'TFISignal', 'MaxScore',
        'EffectiveLongScore', 'EffectiveShortScore', 'PlacedTargetLong', 'PlacedStopLong', 'PlacedTargetShort', 'PlacedStopShort'
foreach ($name in $books) {
    $path = Join-Path $folder $name
    if (-not (Test-Path $path)) { throw "file not found: $path" }
    $r = [IO.File]::OpenText($path); $n = 0; $nDir = 0
    try {
        $cols = $r.ReadLine().Split(',')
        $ix = @{}; foreach ($c in $need) { $ix[$c] = [Array]::IndexOf($cols, $c); if ($ix[$c] -lt 0) { throw "$name header missing $c" } }
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
            $n++; $x.total++
            if (-not $pop) { continue }
            $x.pop++
            $v = $p[$ix.Verdict].Trim().ToUpperInvariant()
            if (-not $dirSet.ContainsKey($v)) { if ($v.StartsWith('NO TRADE [')) { $x.lean++ } else { $x.nonDir++ }; continue }
            if ($sess.name -eq 'ASIA' -and $ts -lt $AsiaArmed) { $x.asiaUnarmed++; continue }
            $side = $dirSet[$v][0]; $band = $dirSet[$v][1]
            $price = 0.0; [void][double]::TryParse($p[$ix.Price], 'Float', $inv, [ref]$price)
            $tgt = 0.0; $stp = 0.0
            if ($side -eq 'LONG') { [void][double]::TryParse($p[$ix.PlacedTargetLong], 'Float', $inv, [ref]$tgt); [void][double]::TryParse($p[$ix.PlacedStopLong], 'Float', $inv, [ref]$stp) }
            else { [void][double]::TryParse($p[$ix.PlacedTargetShort], 'Float', $inv, [ref]$tgt); [void][double]::TryParse($p[$ix.PlacedStopShort], 'Float', $inv, [ref]$stp) }
            $sgn = if ($side -eq 'LONG') { 1.0 } else { -1.0 }
            if ($price -le 0 -or $atr -le 0 -or $tgt -le 0 -or $stp -le 0 -or ($sgn * ($tgt - $price)) -le 0 -or ($sgn * ($price - $stp)) -le 0) { $x.badLevels++; continue }
            $ratio = [double]::Parse($p[$ix.AggrVelBurstRatio], $inv)
            $net = 0.0; $netOk = [double]::TryParse($p[$ix.AggrVelNet], 'Float', $inv, [ref]$net)
            $sig = $p[$ix.AggrVelSignal]; $tfi = $p[$ix.TFISignal]
            $tfiSide = if ($tfi -ceq 'BUY PRESSURE') { 'LONG' } elseif ($tfi -ceq 'SELL PRESSURE') { 'SHORT' } else { '' }
            $burstSide = if ($sig -ceq 'BURST_BUY') { 'LONG' } elseif ($sig -ceq 'BURST_SELL') { 'SHORT' } else { '' }
            $fifth = Get-Fifth $atr $Edges[$sess.name]
            $shThr = $Shadow[$sess.name][$fifth - 1]
            $arm = 'O'; $shadowArm = ''; $oup = $false; $crossed = $false; $bandMismatch = $false; $bLean = $false
            if ($tfiSide -eq $side) {
                if ($burstSide -eq $side) {
                    $arm = 'A'
                    $shadowArm = if ($ratio -ge $shThr) { 'A_keep' } else { 'A_drop' }
                    $maxS = 0; $effL = 0; $effS = 0
                    if ([int]::TryParse($p[$ix.MaxScore], [ref]$maxS) -and [int]::TryParse($p[$ix.EffectiveLongScore], [ref]$effL) -and [int]::TryParse($p[$ix.EffectiveShortScore], [ref]$effS) -and $maxS -gt 0) {
                        $eff = if ($side -eq 'LONG') { $effL } else { $effS }
                        if ((Get-BandOf $eff $maxS) -ne $band) { $bandMismatch = $true }
                        $cf = Get-BandOf ($eff - $bonus) $maxS
                        if ($bandRank[$cf] -lt $bandRank[$band]) { $crossed = $true }
                    }
                } elseif ($burstSide -eq '') {
                    $arm = 'B'
                    if ($ratio -ge $fixedThr[$sess.name]) { $bLean = $true }
                    if ($ratio -ge $shThr -and $netOk -and ($sgn * $net) -gt 0) { $shadowArm = 'S_add' }
                } else { $arm = 'C' }
            } elseif ($tfiSide -ne '' -and $burstSide -eq $tfiSide) { $oup = $true }
            $rows.Add([pscustomobject]@{ ts = $ts; day = $ts.ToString('yyyy-MM-dd'); s = $sess.name; band = $band; fifth = $fifth; era = (Get-Era $ts);
                                         arm = $arm; sh = $shadowArm; oup = $oup; crossed = $crossed; mis = $bandMismatch; blean = $bLean;
                                         atrBps = 10000.0 * $atr / $price })
            $nDir++
        }
    } finally { $r.Close() }
    "BOOK $name weekday session rows kept=$n  directional valid armed rows=$nDir"
}
"weekday session rows=$($x.total)  population (ratio non-empty, ATR parses)=$($x.pop)"
"excluded from population: NO TRADE=$($x.nonDir)  NO TRADE lean forms=$($x.lean)  ASIA before arming=$($x.asiaUnarmed)  invalid placed levels=$($x.badLevels)"
"directional valid armed rows (the count population)=$($rows.Count)  first=$(($rows | Select-Object -First 1).ts.ToString($fmt))  last=$(($rows | Select-Object -Last 1).ts.ToString($fmt))"

# NOT $S: PowerShell names are case-insensitive, so $S would be the loop variable $s (it was, on the first run).
$SessList = @('ASIA', 'LONDON', 'NY')
function Cnt($set, [scriptblock]$f) { @($set | Where-Object $f).Count }

# ================================================================================================
""
"=== 1. Counts, all armed eras pooled: session x ATR fifth x arm (bands pooled; S/M/W split for A and B) ==="
"session fifth    A (S/M/W)        B (S/M/W)          C    O  Oup | A_keep A_drop S_add B_lean | crossed bandMismatch"
foreach ($s in $SessList) {
    $sr = @($rows | Where-Object { $_.s -eq $s })
    foreach ($f in 1..6) {
        $cr = if ($f -le 5) { @($sr | Where-Object { $_.fifth -eq $f }) } else { $sr }
        $lbl = if ($f -le 5) { "$f" } else { 'all' }
        $A = @($cr | Where-Object { $_.arm -eq 'A' }); $B = @($cr | Where-Object { $_.arm -eq 'B' })
        $aS = "{0} ({1}/{2}/{3})" -f $A.Count, (Cnt $A { $_.band -eq 'STRONG' }), (Cnt $A { $_.band -eq 'MEDIUM' }), (Cnt $A { $_.band -eq 'WEAK' })
        $bS = "{0} ({1}/{2}/{3})" -f $B.Count, (Cnt $B { $_.band -eq 'STRONG' }), (Cnt $B { $_.band -eq 'MEDIUM' }), (Cnt $B { $_.band -eq 'WEAK' })
        "{0,-7} {1,-4} {2,-16} {3,-18} {4,4} {5,4} {6,4} | {7,6} {8,6} {9,5} {10,6} | {11,7} {12,5}" -f $s, $lbl, $aS, $bS,
            (Cnt $cr { $_.arm -eq 'C' }), (Cnt $cr { $_.arm -eq 'O' }), (Cnt $cr { $_.oup }),
            (Cnt $cr { $_.sh -eq 'A_keep' }), (Cnt $cr { $_.sh -eq 'A_drop' }), (Cnt $cr { $_.sh -eq 'S_add' }), (Cnt $cr { $_.blean }),
            (Cnt $cr { $_.crossed }), (Cnt $cr { $_.mis })
    }
}

# ================================================================================================
""
"=== 2. Era split: arm A / arm B per session x ATR fifth x era ==="
"   E1 armed->v66 2026-08-10 18:36:01 | E2 ->2026-08-20 | E3 ->POC fix 2026-09-24 18:46:06 | E4 POC fix->"
"session fifth  E1 A/B      E2 A/B      E3 A/B      E4 A/B      | S_add E1/E2/E3/E4"
foreach ($s in $SessList) {
    $sr = @($rows | Where-Object { $_.s -eq $s })
    foreach ($f in 1..6) {
        $cr = if ($f -le 5) { @($sr | Where-Object { $_.fifth -eq $f }) } else { $sr }
        $lbl = if ($f -le 5) { "$f" } else { 'all' }
        $cells = foreach ($e in 'E1', 'E2', 'E3', 'E4') { $er = @($cr | Where-Object { $_.era -eq $e }); "{0}/{1}" -f (Cnt $er { $_.arm -eq 'A' }), (Cnt $er { $_.arm -eq 'B' }) }
        $sa = @(foreach ($e in 'E1', 'E2', 'E3', 'E4') { Cnt $cr { $_.era -eq $e -and $_.sh -eq 'S_add' } }) -join '/'
        "{0,-7} {1,-4}  {2,-11} {3,-11} {4,-11} {5,-11} | {6}" -f $s, $lbl, $cells[0], $cells[1], $cells[2], $cells[3], $sa
    }
}

# ================================================================================================
""
"=== 3. Accrual from $AccrualFrom (weekday days with >= 1 directional valid row in the session) ==="
$acc = @{}
foreach ($s in $SessList) {
    $ar = @($rows | Where-Object { $_.s -eq $s -and $_.ts -ge $accrualFromDt })
    $days = @($ar | ForEach-Object day | Select-Object -Unique).Count
    $acc[$s] = @{ days = $days }
    "{0}: weekdays={1}  directional rows/day={2:N1}" -f $s, $days, ($ar.Count / [Math]::Max(1, $days))
    "  fifth   A/day  B/day  S_add/day  A_keep/day  A_drop/day"
    foreach ($f in 1..5) {
        $cr = @($ar | Where-Object { $_.fifth -eq $f })
        $acc[$s]["A$f"] = (Cnt $cr { $_.arm -eq 'A' }) / [Math]::Max(1, $days)
        $acc[$s]["B$f"] = (Cnt $cr { $_.arm -eq 'B' }) / [Math]::Max(1, $days)
        $acc[$s]["S$f"] = (Cnt $cr { $_.sh -eq 'S_add' }) / [Math]::Max(1, $days)
        "  {0,-5} {1,6:N2} {2,6:N2} {3,9:N2} {4,10:N2} {5,10:N2}" -f $f, $acc[$s]["A$f"], $acc[$s]["B$f"], $acc[$s]["S$f"],
            ((Cnt $cr { $_.sh -eq 'A_keep' }) / [Math]::Max(1, $days)), ((Cnt $cr { $_.sh -eq 'A_drop' }) / [Math]::Max(1, $days))
    }
}

# ================================================================================================
""
"=== 4. Power: published sigma, projection to the run date, MDE ==="
$sigma = @{}
foreach ($s in $SessList) {
    $cand = foreach ($q in $Published[$s]) { [pscustomobject]@{ tgt = $q.tgt; n = $q.n; se = ($q.hi - $q.lo) / (2 * 1.959964); sig = ($q.hi - $q.lo) / (2 * 1.959964) * [Math]::Sqrt($q.n) } }
    foreach ($c in $cand) { "{0} published {1,-5} n={2,5} CI width={3:N1} -> SE={4:N3} sigma_eff={5:N1} bps" -f $s, $c.tgt, $c.n, (2 * 1.959964 * $c.se), $c.se, $c.sig }
    $sigma[$s] = ($cand | Measure-Object sig -Maximum).Maximum
    "{0} sigma used (max) = {1:N1} bps" -f $s, $sigma[$s]
}
$zCell = 1.959964 + 0.841621      # alpha 0.05 two-sided, power 0.80
$zPrim = 2.393980 + 0.841621      # alpha 0.05/3 two-sided (Holm, worst case over 3 sessions), power 0.80
"multiplier: cell (alpha 0.05 two-sided, power 0.80) = {0:N4}; session primary (alpha 0.0167 two-sided) = {1:N4}" -f $zCell, $zPrim

# weekdays between two dates, exclusive of start day, inclusive of end day
function Get-Weekdays([datetime]$a, [datetime]$b) { $n = 0; $d = $a.Date.AddDays(1); while ($d -le $b.Date) { if ($d.DayOfWeek -ne 'Saturday' -and $d.DayOfWeek -ne 'Sunday') { $n++ }; $d = $d.AddDays(1) }; $n }
function Add-Weekdays([datetime]$a, [int]$k) { $d = $a.Date; while ($k -gt 0) { $d = $d.AddDays(1); if ($d.DayOfWeek -ne 'Saturday' -and $d.DayOfWeek -ne 'Sunday') { $k-- } }; $d }
$lastTs = ($rows | Select-Object -Last 1).ts
$runDt = if ($RunDate) { [datetime]::ParseExact($RunDate, 'yyyy-MM-dd', $inv) } else { $lastTs.Date }
$wdAhead = Get-Weekdays $lastTs $runDt
"data end $($lastTs.ToString($fmt)); run date $($runDt.ToString('yyyy-MM-dd')); weekdays ahead (after the data-end day) = $wdAhead"

$refMed = @{}
foreach ($s in $SessList) { $refMed[$s] = Get-Median @($rows | Where-Object { $_.s -eq $s -and $_.ts -lt $RefEnd } | ForEach-Object atrBps) }
"reference median ATR (bps of price), directional valid rows before 2026-09-12: " + (($SessList | ForEach-Object { "{0} {1:N1}" -f $_, $refMed[$_] }) -join '  ')

$h2 = @{}
""
"cell       nA_now nB_now  A/day  nA_run nB_run  date nA>=$MinN   MDE_run bps  ATR scale  MDE_run scaled | S_add_now S_add_run date S_add>=$MinN"
foreach ($s in $SessList) {
    $sr = @($rows | Where-Object { $_.s -eq $s })
    $groups = @(
        @{ lbl = 'f1'; fs = @(1) }, @{ lbl = 'f2'; fs = @(2) }, @{ lbl = 'f3'; fs = @(3) }, @{ lbl = 'f4'; fs = @(4) }, @{ lbl = 'f5'; fs = @(5) },
        @{ lbl = 'low12'; fs = @(1, 2) }, @{ lbl = 'mid3'; fs = @(3) }, @{ lbl = 'high45'; fs = @(4, 5) }, @{ lbl = 'all'; fs = @(1, 2, 3, 4, 5) })
    foreach ($g in $groups) {
        $cr = @($sr | Where-Object { $g.fs -contains $_.fifth })
        $nA = Cnt $cr { $_.arm -eq 'A' }; $nB = Cnt $cr { $_.arm -eq 'B' }; $nS = Cnt $cr { $_.sh -eq 'S_add' }
        $rA = 0.0; $rB = 0.0; $rS = 0.0; foreach ($f in $g.fs) { $rA += $acc[$s]["A$f"]; $rB += $acc[$s]["B$f"]; $rS += $acc[$s]["S$f"] }
        $nAr = $nA + $rA * $wdAhead; $nBr = $nB + $rB * $wdAhead; $nSr = $nS + $rS * $wdAhead
        $dA = if ($nA -ge $MinN) { 'met' } elseif ($rA -gt 0) { (Add-Weekdays $lastTs ([int][Math]::Ceiling(($MinN - $nA) / $rA))).ToString('yyyy-MM-dd') } else { 'never' }
        $dS = if ($nS -ge $MinN) { 'met' } elseif ($rS -gt 0) { (Add-Weekdays $lastTs ([int][Math]::Ceiling(($MinN - $nS) / $rS))).ToString('yyyy-MM-dd') } else { 'never' }
        $z = if ($g.lbl -eq 'all') { $zPrim } else { $zCell }
        $mde = if ($nAr -gt 0 -and $nBr -gt 0) { $z * $sigma[$s] * [Math]::Sqrt(1.0 / $nAr + 1.0 / $nBr) } else { [double]::NaN }
        $cellMed = Get-Median @($cr | Where-Object { $_.arm -eq 'A' -or $_.arm -eq 'B' } | ForEach-Object atrBps)
        $scale = $cellMed / $refMed[$s]
        "{0,-6} {1,-6} {2,6} {3,6} {4,6:N2} {5,7:N0} {6,6:N0}  {7,-11} {8,10:N1} {9,10:N2} {10,14:N1} | {11,9} {12,9:N0} {13}" -f $s, $g.lbl, $nA, $nB, $rA, $nAr, $nBr, $dA, $mde, $scale, ($mde * $scale), $nS, $nSr, $dS
        if ($g.lbl -eq 'high45') { $h2[$s] = @{ n1 = $nAr + $nSr; n2 = $nBr - $nSr; scale = $scale } }
    }
}
""
"secondary H2 (option (c) upgraded set in fifths 4-5): (A + S_add) vs (B - S_add), projected to the run date, alpha 0.0167 two-sided"
"session  n1_run n2_run  MDE_run bps  MDE_run scaled"
foreach ($s in $SessList) {
    $q = $h2[$s]; $m = $zPrim * $sigma[$s] * [Math]::Sqrt(1.0 / $q.n1 + 1.0 / $q.n2)
    "{0,-7} {1,7:N0} {2,6:N0} {3,11:N1} {4,15:N1}" -f $s, $q.n1, $q.n2, $m, ($m * $q.scale)
}
