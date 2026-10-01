#requires -Version 5.1
<#
  tools/ops/history-compare.ps1 -- the stage-3 parallel-run comparison
  (docs/history-data-store-spec.md section 4.1: "Wire it after collector.ps1 fetch, like the
  venue check. Read-only.").

  Runs on THIS machine, never on the box. For one copy-back folder:
    1. (unless -SkipTopUp) `BacktestRunner history topup` on the dev store, so the days the
       box captured are in the dev store before they are compared (network: history.deribit.com
       only; ~2 min per missing day);
    2. `BacktestRunner history compare` over the last -Days complete UTC days before the fetch
       started, box store = <FetchFolder>\backtest_data, appending one row to the ledger that
       the stage-4 pass rule reads (`history passrule --ledger ...`).

  INERT until the dev store exists: with no history_checkpoint.csv in -Store it prints one line
  and exits 0. Called at the end of every successful `collector.ps1 fetch` (opt out with
  -SkipHistoryCompare there).

  Exit codes: 0 = PASS, or the dev store is not installed yet. 3 = FAIL (the plan stops and goes
  to the trader, section 4.2). 1 = the comparison could not run (build, top-up or argument).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FetchFolder,

    # When the fetch started (UTC). The box's day of the fetch is partial, so the window ends at
    # 00:00 UTC of that day. Default: now.
    [string]$FetchStartUtc,

    [ValidateRange(1, 60)]
    [int]$Days = 7,

    [string]$Store = 'C:\DeribitData\history',
    [string]$LedgerPath,
    [switch]$SkipTopUp
)

$ErrorActionPreference = 'Continue'   # native stderr foot-gun -- see verify-gate.ps1
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $LedgerPath) { $LedgerPath = Join-Path $Store 'compare_ledger.csv' }
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$utcStyles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal

if (-not (Test-Path (Join-Path $Store 'history_checkpoint.csv'))) {
    Write-Host "      dev history store not installed at $Store -- comparison skipped (inert until the transfer, docs/history-store-backfill-runbook.md step e)"
    exit 0
}
$box = Join-Path $FetchFolder 'backtest_data'
if (-not (Test-Path $box -PathType Container)) { Write-Host "FAIL  no backtest_data\ in $FetchFolder" -ForegroundColor Red; exit 1 }

$start = (Get-Date).ToUniversalTime()
if ($FetchStartUtc) {
    $p = [datetime]::MinValue
    if (-not [datetime]::TryParse($FetchStartUtc, $inv, $utcStyles, [ref]$p)) { Write-Host "FAIL  unparseable -FetchStartUtc '$FetchStartUtc'" -ForegroundColor Red; exit 1 }
    $start = $p
}
$toDay = New-Object DateTime ($start.Year, $start.Month, $start.Day, 0, 0, 0, [DateTimeKind]::Utc)
$fromDay = $toDay.AddDays(-$Days)

$proj = Join-Path $repo 'tools\BacktestRunner\BacktestRunner.vbproj'
$exe  = Join-Path $repo 'tools\BacktestRunner\bin\Release\net8.0\BacktestRunner.exe'
$buildOut = & dotnet build $proj -c Release -nologo -v q 2>&1 | ForEach-Object { "$_" }
if ($LASTEXITCODE -ne 0) { Write-Host ("FAIL  BacktestRunner build failed: " + (($buildOut | Select-Object -Last 3) -join ' | ')) -ForegroundColor Red; exit 1 }

$prevOutEnc = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
try {
    if (-not $SkipTopUp) {
        Write-Host "      top-up: $Store"
        & $exe history topup --store $Store | ForEach-Object { Write-Host "      $_" }
        $tu = $LASTEXITCODE
        if ($tu -ne 0 -and $tu -ne 4) { Write-Host "FAIL  history topup exit $tu -- comparison not run" -ForegroundColor Red; exit 1 }
        if ($tu -eq 4) { Write-Host "WARN  top-up left GAP/FAILED days; they are 'not compared' below" -ForegroundColor Yellow }
    }
    $from = $fromDay.ToString('yyyy-MM-dd', $inv)
    $to = $toDay.ToString('yyyy-MM-dd', $inv)
    Write-Host "      compare: box $box | dev $Store | $from .. $to (exclusive) | ledger $LedgerPath"
    & $exe history compare --box $box --store $Store --from $from --to $to --ledger $LedgerPath | ForEach-Object { Write-Host "      $_" }
    $code = $LASTEXITCODE
} finally {
    [Console]::OutputEncoding = $prevOutEnc
}
if ($code -eq 0) { Write-Host "OK    history comparison PASS" -ForegroundColor Green; exit 0 }
if ($code -eq 3) { Write-Host "FAIL  history comparison FAIL -- the plan stops and goes to the trader (docs/history-data-store-spec.md section 4.2)" -ForegroundColor Red; exit 3 }
Write-Host "FAIL  history compare exit $code" -ForegroundColor Red
exit 1
