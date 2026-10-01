#requires -Version 5.1
<#
  tools/ops/history-backfill/history-pull.ps1 -- pull the cloud backfill (ruling HDS-2) from S3 to
  this machine and install it as the dev history store. docs/history-store-backfill-runbook.md
  step (e) is the procedure.

  1. Downloads s3://<Bucket>/<Prefix>/ to -Staging (skip with -SkipDownload to re-run on files
     already downloaded).
  2. For every month in manifest.csv: checks the .gz SHA-256, decompresses it, checks the CSV
     SHA-256, and only then renames it into -Store. A month already in -Store with the SAME
     SHA-256 is skipped; one with a DIFFERENT SHA-256 stops the pull (nothing is overwritten).
  3. Copies history_checkpoint.csv, history_backfill.log and the status files beside them (the
     checkpoint under the same no-overwrite rule).
  4. Runs `BacktestRunner history status --deep` on the installed store.

  Read-only on S3 (the bucket's 7-day lifecycle removes the objects; this script deletes nothing).
  Exit 0 = every month verified and installed and the deep status found no problem; 1 = anything
  else (the message says what).
#>
[CmdletBinding()]
param(
    [string]$Bucket = 'deribit-engine-bucket',
    [string]$Region = 'eu-west-2',
    [string]$Prefix = 'history-backfill/store',
    [string]$Store = 'C:\DeribitData\history',
    [string]$Staging = 'C:\DeribitData\history-transfer',
    [string]$Runner,
    [switch]$SkipDownload
)

$ErrorActionPreference = 'Stop'
# Resolved here, not as the param default: $PSScriptRoot is empty inside param() on
# Windows PowerShell 5.1 when the script runs through -File (found by the local test run).
if (-not $Runner) { $Runner = Join-Path $PSScriptRoot '..\..\BacktestRunner\bin\Release\net8.0\BacktestRunner.dll' }
function Info($m) { Write-Host "[pull] $m" }
function Die($m) { Write-Host "[pull] STOP: $m" -ForegroundColor Red; exit 1 }

function Sha256Of($path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() }

New-Item -ItemType Directory -Force -Path $Staging | Out-Null
New-Item -ItemType Directory -Force -Path $Store | Out-Null

if (-not $SkipDownload) {
    Info "downloading s3://$Bucket/$Prefix/ -> $Staging"
    & aws s3 cp "s3://$Bucket/$Prefix/" $Staging --recursive --region $Region --only-show-errors
    if ($LASTEXITCODE -ne 0) { Die "aws s3 cp failed (exit $LASTEXITCODE)" }
}

$manifestPath = Join-Path $Staging 'manifest.csv'
if (-not (Test-Path -LiteralPath $manifestPath)) { Die "no manifest.csv in $Staging" }
if (Test-Path -LiteralPath (Join-Path $Staging 'DONE')) {
    Info ("backfill DONE marker: " + (Get-Content -LiteralPath (Join-Path $Staging 'DONE') -Raw).Trim())
} elseif (Test-Path -LiteralPath (Join-Path $Staging 'INCOMPLETE')) {
    Write-Host "[pull] WARNING: the backfill finished INCOMPLETE -- read status_final.txt before relying on the store" -ForegroundColor Yellow
} else {
    Write-Host "[pull] WARNING: no DONE marker -- the backfill has not finished; this pull is partial" -ForegroundColor Yellow
}

$rows = Import-Csv -LiteralPath $manifestPath
if ($rows.Count -eq 0) { Die "manifest.csv lists no month" }
$installed = 0; $skipped = 0; $totalRows = 0L
foreach ($r in $rows) {
    $m = $r.Month
    $gz = Join-Path $Staging "trades_$m.csv.gz"
    $dest = Join-Path $Store "trades_$m.csv"
    if (-not (Test-Path -LiteralPath $gz)) { Die "trades_$m.csv.gz listed in the manifest but not downloaded" }
    if ((Sha256Of $gz) -ne $r.GzSha256.ToLowerInvariant()) { Die "gz SHA-256 mismatch for $m (transfer damaged; re-download)" }
    if (Test-Path -LiteralPath $dest) {
        if ((Sha256Of $dest) -eq $r.CsvSha256.ToLowerInvariant()) { $skipped++; $totalRows += [long]$r.Rows; continue }
        Die "$dest exists with different content -- refusing to overwrite (move it aside first)"
    }
    $tmp = "$dest.tmp"
    $in = [System.IO.File]::OpenRead($gz)
    try {
        $gzs = New-Object System.IO.Compression.GZipStream($in, [System.IO.Compression.CompressionMode]::Decompress)
        $out = [System.IO.File]::Create($tmp)
        try { $gzs.CopyTo($out); $out.Flush($true) } finally { $out.Dispose(); $gzs.Dispose() }
    } finally { $in.Dispose() }
    if ((Sha256Of $tmp) -ne $r.CsvSha256.ToLowerInvariant()) { Remove-Item -LiteralPath $tmp; Die "CSV SHA-256 mismatch for $m after decompression" }
    Move-Item -LiteralPath $tmp -Destination $dest
    $installed++; $totalRows += [long]$r.Rows
    Info "installed trades_$m.csv ($($r.Rows) rows, SHA-256 verified)"
}

foreach ($f in @('history_checkpoint.csv', 'history_backfill.log', 'status_final.txt', 'status_latest.txt', 'loop.log')) {
    $src = Join-Path $Staging $f
    if (-not (Test-Path -LiteralPath $src)) { continue }
    $dst = Join-Path $Store $f
    if ($f -eq 'history_checkpoint.csv' -and (Test-Path -LiteralPath $dst) -and ((Sha256Of $dst) -ne (Sha256Of $src))) {
        Die "$dst exists with different content -- refusing to overwrite the checkpoint (move it aside first)"
    }
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
Info "months installed $installed, already present $skipped, rows $totalRows -> $Store"

$runnerFull = [System.IO.Path]::GetFullPath($Runner)
if (-not (Test-Path -LiteralPath $runnerFull)) { Die "runner not found: $runnerFull (build tools\BacktestRunner first)" }
Info "deep status: dotnet $runnerFull history status --store $Store --deep"
& dotnet $runnerFull history status --store $Store --deep
$code = $LASTEXITCODE
if ($code -ne 0) { Die "deep status reported problems (exit $code) -- see the PROBLEM lines above" }
Info "deep status clean. Next: runbook step (e) -- the comparison against the newest aws_fetch folder."
exit 0
