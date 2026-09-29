# Late liquidation-flag watcher. Read-only, public endpoints, no key.
# Question: on the given Deribit host, is a liquidation trade flagged at FIRST sight, or does the
# `liquidation` field appear later? Polls the latest 1000 trades every 10 s, records each trade's flag at
# first sight, and every 60 s re-checks by seq range the trades first seen 1-150 min earlier.
# Logs FLAG_AT_FIRST_SIGHT, LATE_FLAG (with the delay) and a status line every 60 s.
# Evidence so far: www.deribit.com -> LATE_FLAG at ~60 min (2026-09-28, n = 1),
# docs/liquidation-probe-run-2026-09-21.md section 0000. The history-host run is owed
# (docs/seat-handover-2026-09-29.md). The history host widens seq ranges to whole milliseconds, so
# re-check chunks are 800 seqs to leave headroom under count=1000.
# Usage: powershell -NoProfile -File tools/ops/liq-late-flag-watch.ps1 -Source history -Minutes 480
param(
    [ValidateSet('history', 'main')][string]$Source = 'history',
    [int]$Minutes = 480,
    [string]$LogPath = 'C:\probe-runs\liqlag\watch.log'
)
$hostName = if ($Source -eq 'history') { 'history.deribit.com' } else { 'www.deribit.com' }
$base = "https://$hostName/api/v2/public/get_last_trades_by_instrument?instrument_name=BTC-PERPETUAL"
New-Item -ItemType Directory -Force -Path (Split-Path $LogPath) | Out-Null
$seen = @{}   # seq -> @{ t0 = first seen UTC; f0 = flag at first sight; ts = venue ts; late }
function Flag($t) { if ($t.PSObject.Properties.Name -contains 'liquidation' -and $t.liquidation) { [string]$t.liquidation } else { 'none' } }
function W($s) { $line = "[{0}] {1}" -f (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss'), $s; Add-Content -Path $LogPath -Value $line }
W "start; source=$hostName minutes=$Minutes"
$stop = (Get-Date).AddMinutes($Minutes); $lastRe = Get-Date; $polls = 0; $flagFirst = 0; $late = 0; $errs = 0
while ((Get-Date) -lt $stop) {
  try {
    $tr = @((Invoke-RestMethod "$base&count=1000&sorting=desc" -TimeoutSec 20).result.trades); $polls++
    $now = (Get-Date).ToUniversalTime()
    foreach ($t in $tr) {
      $s = [long]$t.trade_seq
      if (-not $seen.ContainsKey($s)) {
        $f = Flag $t; $seen[$s] = @{ t0 = $now; f0 = $f; ts = [long]$t.timestamp; late = $false }
        if ($f -ne 'none') { $flagFirst++; W "FLAG_AT_FIRST_SIGHT seq=$s flag=$f venue_age_s=$([math]::Round(($now - [DateTimeOffset]::FromUnixTimeMilliseconds($t.timestamp).UtcDateTime).TotalSeconds,1))" }
      }
    }
  } catch { $errs++ }
  if (((Get-Date) - $lastRe).TotalSeconds -ge 60) {
    $lastRe = Get-Date; $now = (Get-Date).ToUniversalTime()
    $cand = @($seen.Keys | Where-Object { $a = ($now - $seen[$_].t0).TotalMinutes; $a -ge 1 -and $a -le 150 -and $seen[$_].f0 -eq 'none' -and -not $seen[$_].late } | Sort-Object)
    for ($i = 0; $i -lt $cand.Count; $i += 800) {
      $lo = $cand[$i]; $hi = $cand[[math]::Min($i + 799, $cand.Count - 1)]
      try {
        $tr = @((Invoke-RestMethod "$base&start_seq=$lo&end_seq=$hi&count=1000&sorting=asc" -TimeoutSec 20).result.trades)
        foreach ($t in $tr) { $s = [long]$t.trade_seq; $f = Flag $t
          if ($seen.ContainsKey($s) -and $seen[$s].f0 -eq 'none' -and $f -ne 'none' -and -not $seen[$s].late) {
            $seen[$s].late = $true; $late++
            W ("LATE_FLAG seq={0} flag={1} first_seen_unflagged={2:HH:mm:ss} venue_ts={3:HH:mm:ss.fff} detected_age_s={4:N0}" -f $s, $f, $seen[$s].t0, [DateTimeOffset]::FromUnixTimeMilliseconds($seen[$s].ts).UtcDateTime, ($now - [DateTimeOffset]::FromUnixTimeMilliseconds($seen[$s].ts).UtcDateTime).TotalSeconds) } }
      } catch { $errs++ }
      Start-Sleep -Milliseconds 150
    }
    foreach ($k in @($seen.Keys)) { if (($now - $seen[$k].t0).TotalMinutes -gt 180) { $seen.Remove($k) } }
    W "status polls=$polls tracked=$($seen.Count) flag_at_first_sight=$flagFirst late_flags=$late errors=$errs"
  }
  Start-Sleep -Seconds 10
}
W "end polls=$polls flag_at_first_sight=$flagFirst late_flags=$late errors=$errs"
