#requires -Version 5.1
<#
  tools/ops/venue-check.ps1 -- the option-A venue sample
  (docs/venue-check-plan-review-2026-09-14.md §3; ruling: trader, 2026-09-14 UTC).

  Runs `BacktestRunner coverage --verify-venue` over ONE copy-back folder on THIS machine and
  appends ONE row to aws_fetch/venue_check_ledger.csv (gitignored). It never contacts the
  collector box: the store it reads is the copy-back, and the only network call is Deribit's
  public trade endpoint.

  Called at the end of every successful `tools/ops/collector.ps1 fetch` (opt out with
  -SkipVenueCheck there). Runnable by hand against any aws_fetch/<stamp>/ folder.

  What the sample is for: testing the assumption the in-app gap repair and the coverage report
  share -- that trade_seq is a gap-free counter on Deribit's public tape. A LOSS row with
  missing_inside_seq_span > 0 is the decisive read (R-2, trader-ruled 2026-09-14); a LOSS with
  it at 0 is an edge effect, not decisive. The dated review lives in docs/trader-tick-queue.md §4.

  ABSENCE OF EVIDENCE IS RECORDED AS NOT_RUN, NEVER AS CLEAN: no binary, a failed build, a
  missing folder, or output without a VENUE_CHECK line each write a NOT_RUN row.

  Exit codes: 0 = a CLEAN row was written. 2 = any other row was written (LOSS, INEXACT,
  VENUE_SHORT, NOT_RUN). 1 = not even a ledger row could be written.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FetchFolder,

    # The window end, a whole UTC hour, e.g. 2026-09-15T08:00:00Z. Default: the previous whole
    # UTC hour before now. collector.ps1 passes the whole hour before the FETCH STARTED, which
    # clears the 30 s trade_store.flush_seconds lag.
    [string]$ToUtc,

    # 13 h keeps the window start ~13-14 h old at run time, inside Deribit's ~24 h retention.
    [ValidateRange(1, 24)]
    [int]$VenueHours = 13,

    [string]$LedgerPath
)

$ErrorActionPreference = 'Continue'   # native stderr foot-gun -- see verify-gate.ps1
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $LedgerPath) { $LedgerPath = Join-Path $repo 'aws_fetch\venue_check_ledger.csv' }

# [R-2, R-3 trader-ruled 2026-09-14] missing_inside_seq_span, store_trades,
# store_outside_venue_span and dump added while the ledger held zero rows.
$Columns = @('run_utc', 'fetch_folder', 'window_from_utc', 'window_to_utc', 'verdict', 'venue_trades',
             'identity_matched', 'fallback_matched', 'missing', 'missing_inside_seq_span', 'store_legacy_only',
             'store_trades', 'store_outside_venue_span', 'pages', 'venue_first_ts', 'venue_last_ts',
             'seq_contiguous', 'tool_commit', 'dump', 'exit_code', 'reason')

$runUtc = (Get-Date).ToUniversalTime()
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$utcStyles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal

function Iso([datetime]$d) { $d.ToString('yyyy-MM-ddTHH:mm:ssZ', $inv) }

function Write-LedgerRow([hashtable]$row) {
    try {
        $dir = Split-Path $LedgerPath -Parent
        if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        $enc = New-Object System.Text.UTF8Encoding $false
        if (-not (Test-Path $LedgerPath)) {
            [System.IO.File]::WriteAllText($LedgerPath, ($Columns -join ',') + "`r`n", $enc)
        }
        $cells = foreach ($c in $Columns) {
            $v = if ($row.ContainsKey($c) -and $null -ne $row[$c]) { [string]$row[$c] } else { '' }
            '"' + $v.Replace('"', '""').Replace("`r", ' ').Replace("`n", ' ') + '"'
        }
        [System.IO.File]::AppendAllText($LedgerPath, ($cells -join ',') + "`r`n", $enc)
        return $true
    } catch {
        Write-Host "FAIL  could not write the venue-check ledger row: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Every early exit goes through here, so a skipped run always leaves its row.
function Exit-NotRun([string]$reason, [hashtable]$window, [string]$exitCode = '') {
    $row = @{ run_utc = (Iso $runUtc); fetch_folder = (Split-Path $FetchFolder -Leaf); verdict = 'NOT_RUN';
              tool_commit = 'unknown'; exit_code = $exitCode; reason = $reason }
    if ($window) { $row.window_from_utc = $window.From; $row.window_to_utc = $window.To }
    $written = Write-LedgerRow $row
    Write-Host "NOT_RUN  venue check did not run: $reason" -ForegroundColor Red
    if ($written) { Write-Host "      ledger row appended: $LedgerPath"; exit 2 }
    exit 1
}

# -- window ------------------------------------------------------------------------------
if ($ToUtc) {
    $to = [datetime]::MinValue
    if (-not [datetime]::TryParse($ToUtc, $inv, $utcStyles, [ref]$to)) { Exit-NotRun "unparseable -ToUtc '$ToUtc'" $null }
} else {
    $to = $runUtc
}
# Always a whole UTC hour: truncate, never round up, so the window never ends after the fetch.
# With no -ToUtc, "now" truncated IS the previous whole-hour boundary.
$to = New-Object DateTime ($to.Year, $to.Month, $to.Day, $to.Hour, 0, 0, [DateTimeKind]::Utc)
$from = $to.AddHours(-$VenueHours)
$window = @{ From = (Iso $from); To = (Iso $to) }
$stamp = $to.ToString('yyyyMMdd-HH', $inv) + 'Z'

# -- evidence folder -----------------------------------------------------------------------
if (-not (Test-Path $FetchFolder -PathType Container)) { Exit-NotRun "fetch folder not found: $FetchFolder" $window }
$FetchFolder = (Resolve-Path $FetchFolder).Path
if (-not (Test-Path (Join-Path $FetchFolder 'backtest_data') -PathType Container)) {
    Exit-NotRun "no backtest_data\ in $FetchFolder -- nothing to compare" $window
}

# -- binary: build from the current tree so tool_commit names what actually ran -------------
$proj = Join-Path $repo 'tools\BacktestRunner\BacktestRunner.vbproj'
$exe  = Join-Path $repo 'tools\BacktestRunner\bin\Release\net8.0\BacktestRunner.exe'
$buildOut = & dotnet build $proj -c Release -nologo -v q 2>&1 | ForEach-Object { "$_" }
if ($LASTEXITCODE -ne 0) { Exit-NotRun ("BacktestRunner build failed: " + (($buildOut | Select-Object -Last 3) -join ' | ')) $window }
if (-not (Test-Path $exe)) { Exit-NotRun "no BacktestRunner binary at $exe after build" $window }

# -- run -------------------------------------------------------------------------------------
$md   = Join-Path $FetchFolder "venue_check_$stamp.md"
$log  = Join-Path $FetchFolder "venue_check_$stamp.log"
$dump = Join-Path $FetchFolder "venue_pages_$stamp.json.gz"
$runArgs = @('coverage',
             '--from', $from.ToString('yyyy-MM-ddTHH:mm', $inv),
             '--to', $to.ToString('yyyy-MM-ddTHH:mm', $inv),
             '--venue-hours', "$VenueHours",
             '--verify-venue', '--strict',
             '--evidence-dir', $FetchFolder,
             '--out', $md,
             '--venue-dump', $dump)
Write-Host "      window $($window.From) -> $($window.To) ($VenueHours h), evidence $FetchFolder"
# The runner writes UTF-8 (→ — ·); PowerShell 5.1 would decode it with the OEM code page and
# the .log would carry mojibake. Decode as UTF-8 for this one call, then restore.
$prevOutEnc = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
try {
    $out = & $exe @runArgs 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
} finally {
    [Console]::OutputEncoding = $prevOutEnc
}
try {
    [System.IO.File]::WriteAllText($log, ((@("> BacktestRunner.exe " + ($runArgs -join ' ')) + $out + "EXIT=$exitCode") -join "`r`n") + "`r`n",
                                   (New-Object System.Text.UTF8Encoding $false))
} catch {
    Write-Host "WARN  could not write $log : $($_.Exception.Message)" -ForegroundColor Yellow
}

$vline = $out | Where-Object { $_ -like 'VENUE_CHECK *' } | Select-Object -Last 1
if (-not $vline) { Exit-NotRun "no VENUE_CHECK line in the output (exit $exitCode) -- see $log" $window "$exitCode" }

# key=value pairs; reason is last and quoted.
$kv = @{}
foreach ($m in [regex]::Matches($vline, '(\w+)=("[^"]*"|\S+)')) {
    $kv[$m.Groups[1].Value] = $m.Groups[2].Value.Trim('"')
}

$row = @{
    run_utc = (Iso $runUtc); fetch_folder = (Split-Path $FetchFolder -Leaf)
    window_from_utc = $window.From; window_to_utc = $window.To
    verdict = $kv['verdict']; venue_trades = $kv['venue']; identity_matched = $kv['identity']
    fallback_matched = $kv['fallback']; missing = $kv['missing']
    missing_inside_seq_span = $kv['missing_inside_seq_span']; store_legacy_only = $kv['legacy']
    store_trades = $kv['store']; store_outside_venue_span = $kv['store_outside_venue_span']
    pages = $kv['pages']; venue_first_ts = $kv['first']; venue_last_ts = $kv['last']
    seq_contiguous = $kv['seq_contiguous']; tool_commit = $kv['tool_commit']; dump = $kv['dump']
    exit_code = "$exitCode"
    reason = $kv['reason']
}
if (-not (Write-LedgerRow $row)) { exit 1 }

$color = if ($kv['verdict'] -eq 'CLEAN') { 'Green' } elseif ($kv['verdict'] -eq 'LOSS') { 'Red' } else { 'Yellow' }
Write-Host ("{0,-6}venue check {1} -- missing={2} venue={3} pages={4} seq_contiguous={5} tool_commit={6}" -f '', $kv['verdict'],
            $kv['missing'], $kv['venue'], $kv['pages'], $kv['seq_contiguous'], $kv['tool_commit']) -ForegroundColor $color
if ($kv['reason']) { Write-Host "      reason: $($kv['reason'])" -ForegroundColor $color }
# [R-2] Only a loss INSIDE the store's trade_seq span is decisive; an edge loss is not.
$insideN = 0
if ($kv['verdict'] -eq 'LOSS' -and [int]::TryParse([string]$kv['missing_inside_seq_span'], [ref]$insideN) -and $insideN -gt 0) {
    Write-Host "      !!! LOSS WITH missing_inside_seq_span=$insideN (seq_contiguous=$($kv['seq_contiguous'])) -- see docs/venue-check-plan-review-2026-09-14.md §5." -ForegroundColor Red
} elseif ($kv['verdict'] -eq 'LOSS') {
    Write-Host '      LOSS with missing_inside_seq_span=0 -- edge effect, not decisive.' -ForegroundColor Yellow
}
Write-Host "      report $md"
Write-Host "      ledger row appended: $LedgerPath"
if ($kv['verdict'] -eq 'CLEAN') { exit 0 }
exit 2
