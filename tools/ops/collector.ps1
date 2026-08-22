#requires -Version 5.1
<#
  tools/ops/collector.ps1 -- scripted collector deploy + copy-back over AWS SSM
  (docs/collector-ops-tooling-proposal.md). Three verbs, PowerShell driving the AWS CLI,
  alongside the tools/checks/verify-gate.ps1 precedent. NO ENGINE CODE IS TOUCHED by this
  file — it is outside every .vbproj.

    status  -- read-only. Wraps ssm-mem.json + ssm-apphealth.json. No S3, no writes.
    fetch   -- read-only on the box. Box -> S3 -> local. Never pools, dedups or merges.
    deploy  -- the only verb that writes. PRODUCTION ONLY per docs/collector-ops-tooling-
               proposal.md D-7 -- there is deliberately no default -InstanceId. Six-item
               allowlist ONLY (docs/aws-collector-deploy-checklist.md §1): the .exe/.dll/
               .deps.json/.runtimeconfig.json, settings.json, fonts\. NEVER backtest_data\,
               NEVER analysis_log.csv, NEVER settings.local.json.

  Out of scope, named so it is never assumed back in (proposal §2.7):
    - No pooling/dedup/analysis in `fetch` -- it moves bytes. The §3b minute-key dedup in
      aws-collector-deploy-checklist.md stays a separate, deliberate manual step.
    - No settings editing on the box -- settings travel as the tracked file or not at all.
    - No store manipulation, in either direction -- backtest_data\ is read-only to this tool.
    - No collector-REPLACEMENT / cutover automation. §5 of the proposal is a manual, ordered
      runbook that USES `fetch` + `status` as building blocks (its step 1/3/8) but the
      book/store carry-across (steps 4-6) is deliberately hand-run -- it is store
      manipulation, which this tool never does even for a good reason.
    - No Linux support. The launch mechanism (§2.1, scheduled-task-into-interactive-session)
      is Windows-only; the CLI port supersedes this whole file, not extends it.

  Requires: AWS CLI v2 configured locally (the operator's own credentials -- separate from
  the EC2 instance roles, which is what talks to S3 on the box's side). PowerShell 5.1+
  (ships with the target Server 2019/2025 boxes and with the operator's own machine).

  Exit codes: 0 = the verb completed and, for deploy, the NEW build is live and passed the
  acceptance gate.
  1 = aborted before anything changed (pre-flight failure, git dirty, plan declined) --
      status/fetch also use 1 for "something did not verify, nothing was written".
  2 = deploy wrote to the box and the new build did not pass the acceptance gate. A restore
      (and restart) onto the OLD build was ATTEMPTED in every such path -- see the console
      output for whether it confirmed. Exit 2 always means STOP AND INVESTIGATE BY HAND
      before trying again (§2.6) -- it is returned whether or not the restore itself
      confirmed, because either way the intended new build is not the one running.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('status', 'fetch', 'deploy')]
    [string]$Verb,

    # D-7: named explicitly on every invocation, no default. A tool that defaults to a
    # target is a tool that deploys to the wrong one -- that matters more, not less, once
    # two boxes are interchangeable (the REPLACEMENT candidate).
    [Parameter(Mandatory = $true)]
    [string]$InstanceId,

    [string]$Region = 'eu-west-2',
    # D-8: the dedicated bucket, created 2026-08-21. Overridable but never point this at
    # thecentralstorage -- that bucket holds an unrelated app's live backups and this
    # tool's lifecycle assumptions (7-day expiry) would be actively destructive there.
    [string]$Bucket = 'deribit-engine-bucket',

    [string]$LocalBuildDir,
    [string]$OutDir,

    # deploy only: print the plan and pre-flight results, then stop before the y/n prompt.
    # Nothing changes under -DryRun, by construction -- it returns before Step 3's prompt.
    [switch]$DryRun
)

$ErrorActionPreference = 'Continue'  # native aws.exe stderr foot-gun -- see verify-gate.ps1
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $LocalBuildDir) { $LocalBuildDir = Join-Path $repo 'bin\Release\net8.0-windows' }
if (-not $OutDir)        { $OutDir = $repo }

function Section($t) { Write-Host ''; Write-Host "=== $t ===" }
function Ok($m)   { Write-Host "OK    $m" -ForegroundColor Green }
function Warn($m) { Write-Host "WARN  $m" -ForegroundColor Yellow }
function Fail($m) { Write-Host "FAIL  $m" -ForegroundColor Red }
function Info($m) { Write-Host "      $m" }

# -- the six-item allowlist (docs/aws-collector-deploy-checklist.md §1) -- POSITIVE RECORD,
# never widen it here; a "back up the folder" or "copy everything" shortcut is exactly
# TRAP 1 from the proposal's §0. -------------------------------------------------------
$SixFiles = @(
    'DeribitVerdictEngine.exe',
    'DeribitVerdictEngine.dll',
    'DeribitVerdictEngine.deps.json',
    'DeribitVerdictEngine.runtimeconfig.json',
    'settings.json'
)
$SixDirs = @('fonts')
# Optional: travels alongside the six if present locally, but is never REQUIRED and is
# reported separately so deploy's own acceptance counts stay honest about what "the six"
# means.
$OptionalPdb = 'DeribitVerdictEngine.pdb'

# -- fetch targets (proposal §2.4 + §5.2's named sidecars) ------------------------------
$FetchFiles = @('analysis_log.csv', 'ws_health.log', 'capture_marker.log', 'analysis_eval_cache.csv')
$FetchDirs  = @('backtest_data', 'settings_snapshots')

# [FIX 8a, live-execution finding] `aws` does not reliably resolve by name inside an SSM
# session -- measured on the test box: the CLI installer updates the MACHINE PATH, but the
# SSM Agent's own long-running process keeps the PATH it started with, and does not pick up
# the registry change until the agent itself restarts. A fresh box (or an agent that hasn't
# restarted since install) fails silently on every remote `aws` call. Prepended, not
# appended, so a stale cached agent PATH entry pointing at a missing/older aws.exe cannot
# shadow the real one. Spliced into every remote command array that invokes `aws` -- do not
# duplicate this line by hand at a new call site; reference this variable instead.
$PathRefreshCmd = "`$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User') + ';' + `$env:Path"

# ===========================================================================
# AWS CLI + SSM plumbing -- collector.ps1 is the first script in this repo to drive
# ssm send-command / get-command-invocation programmatically; the existing ssm-*.json
# files at the repo root are hand-run payloads, not wrapped. This is that wrapper.
# ===========================================================================

function Invoke-Aws {
    param([Parameter(Mandatory = $true)][string[]]$Args)
    $out = & aws @Args 2>&1
    if ($LASTEXITCODE -ne 0) {
        $outText = $out -join "`n"
        throw "aws $($Args -join ' ') failed (exit $LASTEXITCODE): $outText"
    }
    return $out
}

<#
.SYNOPSIS
  Run a PowerShell command list on $InstanceId via SSM and block until it finishes.
.DESCRIPTION
  $Commands is written to a temp JSON file in the SAME shape as the existing repo-root
  ssm-*.json payloads ({"commands":[...]}) and passed as --parameters file://, so a
  command list built here is byte-for-byte what a hand-run ssm-*.json invocation sends.
  Polls get-command-invocation every 3s up to $TimeoutSec. Never throws on a remote
  script's own failure (Status=Failed) -- that is reported in the return object for the
  caller to act on; this only throws on an SSM/transport failure (can't reach the box).
#>
function Invoke-RemotePs {
    param(
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][string]$Region,
        [Parameter(Mandatory = $true)][string[]]$Commands,
        [int]$TimeoutSec = 180
    )
    $tmp = [System.IO.Path]::GetTempFileName()
    try {
        $payload = @{ commands = $Commands } | ConvertTo-Json -Depth 4
        # [FIX 6, live-execution finding] Set-Content -Encoding utf8 on Windows PowerShell 5.1
        # writes UTF-8 WITH A BOM. A BOM-prefixed file handed to `aws ssm send-command
        # --parameters file://...` risks a JSON parse failure on the CLI side -- caught only
        # by executing against the real CLI, not by a parse check on this script. WriteAllText
        # with an explicit BOM-less UTF8Encoding is the fix; this is the ONLY place this
        # script writes the SSM payload file, so one call site closes it everywhere.
        [System.IO.File]::WriteAllText($tmp, $payload, (New-Object System.Text.UTF8Encoding $false))

        $sendOut = Invoke-Aws -Args @(
            'ssm', 'send-command',
            '--instance-ids', $InstanceId,
            '--region', $Region,
            '--document-name', 'AWS-RunPowerShellScript',
            '--parameters', "file://$tmp",
            '--output', 'json'
        )
        $sendJson = ($sendOut -join "`n") | ConvertFrom-Json
        $cmdId = $sendJson.Command.CommandId
        if (-not $cmdId) { throw "send-command returned no CommandId: $($sendOut -join ' ')" }

        $deadline = (Get-Date).AddSeconds($TimeoutSec)
        $terminal = @('Success', 'Cancelled', 'TimedOut', 'Failed')
        $status = 'Pending'
        $inv = $null
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 3
            $invOut = & aws ssm get-command-invocation `
                --command-id $cmdId --instance-id $InstanceId --region $Region --output json 2>&1
            if ($LASTEXITCODE -ne 0) { continue }  # invocation record not visible yet -- retry
            $inv = ($invOut -join "`n") | ConvertFrom-Json
            $status = $inv.Status
            if ($terminal -contains $status) { break }
        }
        if (-not ($terminal -contains $status)) {
            return [pscustomobject]@{ Status = 'ClientTimeout'; StdOut = ''; StdErr = ''; CommandId = $cmdId }
        }
        return [pscustomobject]@{
            Status = $status
            StdOut = $inv.StandardOutputContent
            StdErr = $inv.StandardErrorContent
            CommandId = $cmdId
        }
    } finally {
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }
}

<# Run an existing {"commands":[...]} payload file (the ssm-*.json convention) as-is. #>
function Invoke-RemotePsFile {
    param(
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][string]$Region,
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutSec = 180
    )
    $payload = Get-Content $Path -Raw | ConvertFrom-Json
    return Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $payload.commands -TimeoutSec $TimeoutSec
}

<# Parses simple KEY=VALUE lines this script's own remote commands emit for structured
   round-trips (SSM stdout is plain text -- no remote JSON serialisation needed). #>
function ConvertFrom-KeyValueLines {
    param([string]$Text)
    $map = @{}
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line -match '^([A-Za-z0-9_]+)=(.*)$') { $map[$Matches[1]] = $Matches[2] }
    }
    return $map
}

# ===========================================================================
# status -- read-only, no S3. The replacement for the daily RDP glance (proposal §2.3).
# ===========================================================================
function Invoke-Status {
    Section "status -- $InstanceId ($Region)"
    $mem = Invoke-RemotePsFile -InstanceId $InstanceId -Region $Region `
        -Path (Join-Path $repo 'ssm-mem.json') -TimeoutSec 90
    Section 'memory + eviction (ssm-mem.json)'
    if ($mem.Status -eq 'Success') { $mem.StdOut -split "`r?`n" | ForEach-Object { Info $_ } }
    else { Fail "ssm-mem.json invocation ended $($mem.Status)"; $mem.StdErr -split "`r?`n" | ForEach-Object { Info $_ } }

    $health = Invoke-RemotePsFile -InstanceId $InstanceId -Region $Region `
        -Path (Join-Path $repo 'ssm-apphealth.json') -TimeoutSec 90
    Section 'app health (ssm-apphealth.json)'
    if ($health.Status -eq 'Success') { $health.StdOut -split "`r?`n" | ForEach-Object { Info $_ } }
    else { Fail "ssm-apphealth.json invocation ended $($health.Status)"; $health.StdErr -split "`r?`n" | ForEach-Object { Info $_ } }
}

# ===========================================================================
# fetch -- box -> S3 -> local, read-only on the box (proposal §2.4). Never pools/dedups.
# ===========================================================================
function Invoke-Fetch {
    Section "fetch -- $InstanceId ($Region) -> s3://$Bucket -> $OutDir"
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $prefix = "fetch/$InstanceId/$stamp"

    # -- Step 1: on the box, resolve $dir and SNAPSHOT every target into a staging dir FIRST,
    # then manifest and upload FROM THE SNAPSHOT, never from the live files. [FIX 7,
    # live-execution finding, docs/collector-ops-tooling-spec-back.md §7] The original
    # version measured (Get-Item).Length, then counted lines, then ran `aws s3 cp` -- three
    # separate instants against a file a 24/7 collector is actively appending to. Live run
    # against production caught it directly: analysis_log.csv manifested at 21,943,640 bytes
    # / 23,847 lines; the download landed at 21,944,609 bytes / the SAME 23,847 lines -- the
    # manifest read was torn mid-row, the upload (later still) caught the completed row, and
    # the download was correct throughout. A size-tolerance check would have papered over
    # that instead of preventing it, and would as easily hide a genuinely short transfer.
    # Snapshotting first makes manifest == uploaded == downloaded BY CONSTRUCTION -- all
    # three read the same static copy -- and removes the torn-read risk on a growing file
    # entirely, rather than tolerating it. ~80 MB transient cost on a 30 GB disk. The
    # snapshot dir is removed on every exit path, including a snapshot failure itself.
    $fileList = ($FetchFiles | ForEach-Object { "'$_'" }) -join ','
    $dirList  = ($FetchDirs  | ForEach-Object { "'$_'" }) -join ','
    $remoteCmds = @(
        "`$p = Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue",
        "if (-not `$p) { 'ERROR=app not running, cannot resolve install dir'; exit 1 }",
        "`$dir = Split-Path `$p.Path",
        "'REMOTE_DIR=' + `$dir",
        $PathRefreshCmd,
        "`$snap = Join-Path `$dir '_fetch_snapshot'",
        "Remove-Item -Recurse -Force `$snap -ErrorAction SilentlyContinue",
        "New-Item -ItemType Directory -Force `$snap | Out-Null",
        "try {",
        "  foreach (`$f in @($fileList)) {",
        "    `$fp = Join-Path `$dir `$f",
        "    if (Test-Path `$fp) { Copy-Item `$fp (Join-Path `$snap `$f) -Force -ErrorAction Stop }",
        "  }",
        "  foreach (`$d in @($dirList)) {",
        "    `$dp = Join-Path `$dir `$d",
        "    if (Test-Path `$dp) { Copy-Item `$dp (Join-Path `$snap `$d) -Recurse -Force -ErrorAction Stop }",
        "  }",
        "} catch {",
        "  'ERROR=snapshot failed: ' + `$_.Exception.Message",
        "  Remove-Item -Recurse -Force `$snap -ErrorAction SilentlyContinue",
        "  exit 1",
        "}",
        "foreach (`$f in @($fileList)) {",
        "  `$fp = Join-Path `$snap `$f",
        "  if (Test-Path `$fp) {",
        "    `$sz = (Get-Item `$fp).Length",
        "    `$rows = if (`$f -like '*.csv') { (Get-Content `$fp | Measure-Object -Line).Lines } else { -1 }",
        "    'MANIFEST_FILE=' + `$f + '|' + `$sz + '|' + `$rows",
        "    aws s3 cp `$fp `"s3://$Bucket/$prefix/`$f`" --only-show-errors",
        "  } else { 'MANIFEST_FILE=' + `$f + '|ABSENT|ABSENT' }",
        "}",
        "foreach (`$d in @($dirList)) {",
        "  `$dp = Join-Path `$snap `$d",
        "  if (Test-Path `$dp) {",
        "    `$cnt = (Get-ChildItem `$dp -Recurse -File | Measure-Object).Count",
        "    `$sz  = (Get-ChildItem `$dp -Recurse -File | Measure-Object -Property Length -Sum).Sum",
        "    'MANIFEST_DIR=' + `$d + '|' + `$sz + '|' + `$cnt",
        "    aws s3 cp `$dp `"s3://$Bucket/$prefix/`$d`" --recursive --only-show-errors",
        "  } else { 'MANIFEST_DIR=' + `$d + '|ABSENT|ABSENT' }",
        "}",
        "Remove-Item -Recurse -Force `$snap -ErrorAction SilentlyContinue",
        # Sweep finding: this printed unconditionally too. Nothing downstream currently
        # gates on it (informational only, unlike PLACED=done/BACKUP=done/RESTORED=done),
        # but a leftover _fetch_snapshot is worth knowing about rather than assuming clean --
        # made it check like every other marker in the file, for the same reason.
        "if (Test-Path `$snap) { 'SNAPSHOT_CLEANED=false' } else { 'SNAPSHOT_CLEANED=true' }"
    )
    $r = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $remoteCmds -TimeoutSec 600
    if ($r.Status -ne 'Success') {
        Fail "remote fetch step ended $($r.Status) -- nothing downloaded"
        # ERROR= lines (app not running, snapshot failed) are bare expressions on the success
        # stream, so they land in StdOut, not StdErr -- print both or the reason is invisible.
        $r.StdOut, $r.StdErr | ForEach-Object { $_ -split "`r?`n" } | ForEach-Object { Info $_ }
        exit 1
    }
    Section 'box-side manifest'
    $manifest = @{}
    foreach ($line in ($r.StdOut -split "`r?`n")) {
        Info $line
        if ($line -match '^MANIFEST_(FILE|DIR)=([^|]+)\|([^|]*)\|([^|]*)$') {
            $manifest[$Matches[2]] = @{ Size = $Matches[3]; Count = $Matches[4] }
        }
    }

    # -- Step 2: local download.
    $localDest = Join-Path (Join-Path $OutDir 'aws_fetch') $stamp
    New-Item -ItemType Directory -Force -Path $localDest | Out-Null
    Invoke-Aws -Args @('s3', 'cp', "s3://$Bucket/$prefix", $localDest, '--recursive', '--only-show-errors') | Out-Null

    # -- Step 3: land the CSV as analysis_log_aws.csv. NEVER touches analysis_log.csv (the
    # local book) -- that path is not written by this script at all, anywhere.
    $downloadedCsv = Join-Path $localDest 'analysis_log.csv'
    if (Test-Path $downloadedCsv) {
        Copy-Item $downloadedCsv (Join-Path $OutDir 'analysis_log_aws.csv') -Force
    }

    # -- Step 4: verify what landed against the box-side manifest. Fail loudly on mismatch.
    Section 'transfer verification (box manifest vs local download)'
    $mismatch = $false
    foreach ($f in $FetchFiles) {
        $box = $manifest[$f]
        $local = Join-Path $localDest $f
        if (-not $box -or $box.Size -eq 'ABSENT') { Info "$f -- absent on box, skipped"; continue }
        if (-not (Test-Path $local)) { Fail "$f -- box reports $($box.Size) bytes / $($box.Count) rows but NOTHING landed locally"; $mismatch = $true; continue }
        $localSz = (Get-Item $local).Length
        if ("$localSz" -ne $box.Size) {
            Fail "$f -- SIZE MISMATCH box=$($box.Size) local=$localSz"
            $mismatch = $true
        } else {
            Ok "$f -- $localSz bytes, $($box.Count) rows (box-reported), sizes match"
        }
    }
    foreach ($d in $FetchDirs) {
        $box = $manifest[$d]
        $localDir = Join-Path $localDest $d
        if (-not $box -or $box.Size -eq 'ABSENT') { Info "$d\ -- absent on box, skipped"; continue }
        if (-not (Test-Path $localDir)) { Fail "$d\ -- box reports $($box.Count) files / $($box.Size) bytes but NOTHING landed locally"; $mismatch = $true; continue }
        $localCnt = (Get-ChildItem $localDir -Recurse -File | Measure-Object).Count
        $localSz  = (Get-ChildItem $localDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
        if ("$localCnt" -ne $box.Count -or "$localSz" -ne $box.Size) {
            Fail "$d\ -- MISMATCH box=$($box.Count) files/$($box.Size) bytes local=$localCnt files/$localSz bytes"
            $mismatch = $true
        } else {
            Ok "$d\ -- $localCnt files, $localSz bytes, matches box"
        }
    }

    Section 'result'
    Info "downloaded to: $localDest"
    if (Test-Path (Join-Path $OutDir 'analysis_log_aws.csv')) { Info "CSV landed as: $(Join-Path $OutDir 'analysis_log_aws.csv')" }
    Warn 'no pooling, dedup or merge performed -- see aws-collector-deploy-checklist.md §3b for the manual minute-key dedup step'
    if ($mismatch) { Fail 'one or more transfers did not verify -- treat this fetch as INCOMPLETE'; exit 1 }
    Ok 'fetch complete, all transfers verified'
}

# ===========================================================================
# deploy -- the only verb that writes (proposal §2.5). Order is the safety property.
# ===========================================================================
function Invoke-Deploy {
    Section "deploy -- $InstanceId ($Region)  [PRODUCTION-CAPABLE -- confirm this is the intended box]"

    # -- Step 1: pre-flight, local. Abort before anything remote is even contacted. --------
    Section '1. pre-flight (local)'
    Push-Location $repo
    try {
        $dirty = git status --porcelain
        if ($dirty) { Fail 'working tree is dirty -- commit or stash before deploying'; exit 1 }
        Ok 'working tree clean'

        $upstream = git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $upstream) {
            Fail 'no upstream tracking branch resolvable -- push and set upstream before deploying'
            exit 1
        }
        $ahead = git rev-list --count '@{u}..HEAD'
        if ([int]$ahead -gt 0) { Fail "local HEAD is $ahead commit(s) ahead of $upstream -- push before deploying"; exit 1 }
        Ok "HEAD matches $upstream -- nothing unpushed"

        $commit = (git rev-parse --short HEAD).Trim()
    } finally {
        Pop-Location
    }

    $missing = @()
    foreach ($f in $SixFiles) { if (-not (Test-Path (Join-Path $LocalBuildDir $f))) { $missing += $f } }
    foreach ($d in $SixDirs)  { if (-not (Test-Path (Join-Path $LocalBuildDir $d))) { $missing += "$d\" } }
    if ($missing.Count -gt 0) {
        Fail "missing from $LocalBuildDir : $($missing -join ', ') -- build Release first"
        exit 1
    }
    Ok "all six allowlist items present in $LocalBuildDir"

    $localHashes = @{}
    foreach ($f in $SixFiles) { $localHashes[$f] = (Get-FileHash (Join-Path $LocalBuildDir $f) -Algorithm SHA256).Hash }
    foreach ($d in $SixDirs) {
        Get-ChildItem (Join-Path $LocalBuildDir $d) -Recurse -File | ForEach-Object {
            $rel = "$d\" + $_.FullName.Substring((Join-Path $LocalBuildDir $d).Length + 1)
            $localHashes[$rel] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        }
    }
    $havePdb = Test-Path (Join-Path $LocalBuildDir $OptionalPdb)
    if ($havePdb) { $localHashes[$OptionalPdb] = (Get-FileHash (Join-Path $LocalBuildDir $OptionalPdb) -Algorithm SHA256).Hash }

    $localSettingsVersion = (Get-Content (Join-Path $repo 'settings.json') -TotalCount 2) -join ' '
    Info "local tracked settings.json: $localSettingsVersion"

    # -- Step 2: pre-flight, remote. ---------------------------------------------------------
    Section '2. pre-flight (remote)'
    $preCmds = @(
        "`$p = Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue",
        "if (-not `$p) { 'ERROR=app not running on this box' } else {",
        "  `$dir = Split-Path `$p.Path",
        "  'REMOTE_DIR=' + `$dir",
        "  'REMOTE_PID=' + `$p.Id",
        "  'REMOTE_SESSION=' + `$p.SessionId",
        "  `$sj = Join-Path `$dir 'settings.json'",
        "  if (Test-Path `$sj) { 'REMOTE_SETTINGS_VERSION=' + ((Get-Content `$sj -TotalCount 2) -join ' ') }",
        "}"
    )
    $pre = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $preCmds -TimeoutSec 60
    if ($pre.Status -ne 'Success' -or $pre.StdOut -match 'ERROR=') {
        Fail "remote pre-flight failed (SSM status $($pre.Status)) -- is the app running on $InstanceId ?"
        $pre.StdOut, $pre.StdErr | ForEach-Object { $_ -split "`r?`n" } | ForEach-Object { Info $_ }
        exit 1
    }
    $remoteState = ConvertFrom-KeyValueLines $pre.StdOut
    $remoteDir = $remoteState['REMOTE_DIR']
    if (-not $remoteDir) { Fail 'could not resolve the remote install directory'; exit 1 }
    Ok "app present -- PID $($remoteState['REMOTE_PID']), dir $remoteDir"
    Info "remote settings.json: $($remoteState['REMOTE_SETTINGS_VERSION'])"

    # -- Step 3: print the plan and ask. NOTHING has changed up to this point. --------------
    Section '3. plan'
    Info "source: commit $commit (HEAD, pushed)"
    Info "target: $InstanceId ($Region), dir $remoteDir"
    Info "will stop: DeribitVerdictEngine PID $($remoteState['REMOTE_PID'])"
    Info "backup goes to: $remoteDir\_deploy_backup\ (single generation, overwritten)"
    Info 'items:'
    foreach ($f in $SixFiles) { Info ("  {0,-40} {1,10} bytes  {2}" -f $f, (Get-Item (Join-Path $LocalBuildDir $f)).Length, $localHashes[$f]) }
    foreach ($d in $SixDirs) {
        Get-ChildItem (Join-Path $LocalBuildDir $d) -Recurse -File | ForEach-Object {
            $rel = "$d\" + $_.FullName.Substring((Join-Path $LocalBuildDir $d).Length + 1)
            Info ("  {0,-40} {1,10} bytes  {2}" -f $rel, $_.Length, $localHashes[$rel])
        }
    }
    if ($havePdb) { Info "  (optional) $OptionalPdb travels alongside but is not part of the six-item gate" }
    Info "current remote: $($remoteState['REMOTE_SETTINGS_VERSION'])"
    Info "incoming local: $localSettingsVersion"

    if ($DryRun) { Ok '-DryRun: stopping before the confirmation prompt. Nothing changed.'; return }

    $resp = Read-Host "Proceed with deploy to $InstanceId ? [y/N]"
    if ($resp -notmatch '^(y|yes)$') { Warn 'declined -- no changes made'; exit 1 }

    # -- Step 4: stop the app. ----------------------------------------------------------------
    Section '4. stop the app'
    $stopCmds = @(
        "Stop-Process -Name DeribitVerdictEngine -Force -ErrorAction SilentlyContinue",
        "for (`$i = 0; `$i -lt 10; `$i++) {",
        "  if (-not (Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue)) { 'STOPPED=true'; break }",
        "  Start-Sleep -Seconds 1",
        "}",
        "if (Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue) { 'STOPPED=false' }"
    )
    $stop = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $stopCmds -TimeoutSec 60
    if ($stop.Status -ne 'Success' -or $stop.StdOut -notmatch 'STOPPED=true') {
        Fail 'could not confirm the app stopped -- aborting before touching any file. Nothing backed up or overwritten.'
        exit 1
    }
    Ok 'app stopped'

    # -- Step 5: back up ONLY the six allowlist items. TRAP 1 -- never widen this. ----------
    # TRAP 3's own shape applies here too, not just to the step-9 gate: a marker this script
    # prints is not a property this script has checked. Copy-Item runs -ErrorAction Stop
    # inside a try/catch (its default non-terminating errors would otherwise print to the
    # remote error stream and let the loop carry on), AND every one of the six items is
    # re-verified present in $bk (dirs by file COUNT, not existence alone) before the single
    # line that ever says BACKUP=done is reached. Mirrors the stop step above, which polls
    # the actual state rather than assuming the Stop-Process call worked.
    Section '5. backup (six items only)'
    $backupCmds = @(
        "`$dir = '$remoteDir'",
        "`$bk = Join-Path `$dir '_deploy_backup'",
        "Remove-Item -Recurse -Force `$bk -ErrorAction SilentlyContinue",
        "New-Item -ItemType Directory -Force `$bk | Out-Null",
        "try {",
        "  foreach (`$f in @($(($SixFiles | ForEach-Object { "'$_'" }) -join ','))) { Copy-Item (Join-Path `$dir `$f) (Join-Path `$bk `$f) -Force -ErrorAction Stop }",
        "  foreach (`$d in @($(($SixDirs | ForEach-Object { "'$_'" }) -join ','))) { Copy-Item (Join-Path `$dir `$d) (Join-Path `$bk `$d) -Recurse -Force -ErrorAction Stop }",
        "} catch {",
        "  'BACKUP=error:' + `$_.Exception.Message",
        "  exit 1",
        "}",
        "`$missing = @()",
        "foreach (`$f in @($(($SixFiles | ForEach-Object { "'$_'" }) -join ','))) { if (-not (Test-Path (Join-Path `$bk `$f))) { `$missing += `$f } }",
        "foreach (`$d in @($(($SixDirs | ForEach-Object { "'$_'" }) -join ','))) {",
        "  `$bkPath = Join-Path `$bk `$d",
        "  if (-not (Test-Path `$bkPath)) { `$missing += `$d; continue }",
        "  `$srcCount = (Get-ChildItem (Join-Path `$dir `$d) -Recurse -File | Measure-Object).Count",
        "  `$bkCount  = (Get-ChildItem `$bkPath -Recurse -File | Measure-Object).Count",
        "  if (`$srcCount -ne `$bkCount) { `$missing += `$d + ' (file count ' + `$bkCount + ' vs source ' + `$srcCount + ')' }",
        "}",
        "if (`$missing.Count -gt 0) { 'BACKUP=incomplete:' + (`$missing -join ',') } else { 'BACKUP=done' }"
    )
    $bk = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $backupCmds -TimeoutSec 90
    if ($bk.Status -ne 'Success' -or $bk.StdOut -notmatch 'BACKUP=done') {
        Fail "backup step did not confirm completion -- aborting before uploading anything ($($bk.StdOut) $($bk.StdErr))"
        exit 1
    }
    Ok "backup written to $remoteDir\_deploy_backup\ -- all six items verified present"

    # -- Step 6: upload -> download -> place. The six items, nothing else, ever. -----------
    Section '6. upload -> download -> place'
    $dstamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $dprefix = "deploy/$InstanceId/$dstamp"
    foreach ($f in $SixFiles) { Invoke-Aws -Args @('s3', 'cp', (Join-Path $LocalBuildDir $f), "s3://$Bucket/$dprefix/$f", '--only-show-errors') | Out-Null }
    foreach ($d in $SixDirs)  { Invoke-Aws -Args @('s3', 'cp', (Join-Path $LocalBuildDir $d), "s3://$Bucket/$dprefix/$d", '--recursive', '--only-show-errors') | Out-Null }
    Ok "uploaded to s3://$Bucket/$dprefix/"

    # [FIX 8b, live-execution finding] PLACED=done used to print unconditionally -- with
    # $ErrorActionPreference 'Continue' remotely, every `aws s3 cp` here can fail to stderr
    # and the script carries on regardless, so the marker was never actually checking the
    # property it claimed. Measured on the test box: all six `aws s3 cp` calls failed (FIX
    # 8a's PATH issue), PLACED=done printed anyway, and step 7's hash check was the only
    # thing that caught it -- exactly what TRAP 3 exists for, but the place step's own report
    # should not have needed step 7 to be told it was wrong. Each cp's $LASTEXITCODE is now
    # checked; PLACED=done is reached only if every one of the six succeeded.
    $placeCmds = @("`$dir = '$remoteDir'", $PathRefreshCmd, "`$failed = @()")
    foreach ($f in $SixFiles) {
        $placeCmds += "aws s3 cp `"s3://$Bucket/$dprefix/$f`" (Join-Path `$dir '$f') --only-show-errors; if (`$LASTEXITCODE -ne 0) { `$failed += '$f' }"
    }
    # [FIX 10 follow-up] `aws s3 cp --recursive` is CONTENT-merge semantics, not
    # Copy-Item's directory-nesting behaviour -- it copies each object under the S3 prefix
    # to the matching relative path under the destination, so a pre-existing `fonts\`
    # destination does not produce `fonts\fonts\` the way FIX 10's Copy-Item bug did.
    # Checked, not just assumed: step 7's hash walk (below) enumerates every file actually
    # present under the placed `fonts\` and looks each one up in $localHashes by its
    # relative path; a stray `fonts\fonts\OFL.txt` from any future nesting would have no
    # local counterpart and would print as a hash MISMATCH there, same backstop that
    # already caught FIX 8b live. No separate check added here for that reason -- one
    # verification, not two copies of it.
    foreach ($d in $SixDirs) {
        $placeCmds += "aws s3 cp `"s3://$Bucket/$dprefix/$d`" (Join-Path `$dir '$d') --recursive --only-show-errors; if (`$LASTEXITCODE -ne 0) { `$failed += '$d' }"
    }
    $placeCmds += "if (`$failed.Count -gt 0) { 'PLACED=incomplete:' + (`$failed -join ',') } else { 'PLACED=done' }"
    $place = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $placeCmds -TimeoutSec 180
    if ($place.Status -ne 'Success' -or $place.StdOut -notmatch 'PLACED=done') {
        Fail "place step did not confirm completion -- box may be in a PARTIAL state ($($place.StdOut) $($place.StdErr))"
        Invoke-Rollback -RemoteDir $remoteDir | Out-Null
        exit 2
    }
    Ok 'placed on box'

    # -- Step 7: verify by hash against source. A hash match proves bytes arrived — nothing
    # more (§2.6). It is still required, because a place step that silently truncated a file
    # must be caught before we ever restart on it. --------------------------------------------
    Section '7. verify by hash'
    $hashCmds = @("`$dir = '$remoteDir'")
    foreach ($f in $SixFiles) { $hashCmds += "'HASH:$f=' + (Get-FileHash (Join-Path `$dir '$f') -Algorithm SHA256).Hash" }
    foreach ($d in $SixDirs) {
        $hashCmds += "Get-ChildItem (Join-Path `$dir '$d') -Recurse -File | ForEach-Object { `$rel = '$d\' + `$_.FullName.Substring((Join-Path `$dir '$d').Length + 1); 'HASH:' + `$rel + '=' + (Get-FileHash `$_.FullName -Algorithm SHA256).Hash }"
    }
    $hv = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $hashCmds -TimeoutSec 90
    $hashMismatch = $false
    if ($hv.Status -ne 'Success') {
        $hashMismatch = $true
    } else {
        foreach ($line in ($hv.StdOut -split "`r?`n")) {
            if ($line -match '^HASH:(.+)=([0-9A-Fa-f]+)$') {
                $rel = $Matches[1]; $remoteHash = $Matches[2]
                if ($localHashes[$rel] -ne $remoteHash) { Fail "$rel hash mismatch (local $($localHashes[$rel]) vs remote $remoteHash)"; $hashMismatch = $true }
                else { Ok "$rel hash matches" }
            }
        }
    }
    if ($hashMismatch) {
        Fail 'hash verification failed -- box may be in a PARTIAL state'
        Invoke-Rollback -RemoteDir $remoteDir | Out-Null
        exit 2
    }

    # -- Step 8: restart via the §2.1 scheduled-task mechanism (measured, not assumed). -----
    Section '8. restart'
    $restartUtc = (Get-Date).ToUniversalTime()
    if (-not (Start-RemoteApp -RemoteDir $remoteDir)) {
        Fail 'restart did not confirm a running process'
        Invoke-Rollback -RemoteDir $remoteDir | Out-Null
        exit 2
    }

    # -- Step 9: verify for real -- TWO new CSV rows >=45 s apart within 12 minutes (§2.6, as
    # tightened 2026-08-22), not a file compare and not one row.
    Section '9. acceptance gate (2 new CSV rows >=45s apart, within 12 minutes)'
    $gateOk = Wait-DeployGate -RestartUtc $restartUtc -RemoteDir $remoteDir -ExpectSettingsVersion $localSettingsVersion

    if ($gateOk) {
        Ok 'ACCEPTED -- the analysis loop fired more than once, session non-zero, settings version matches. Deploy complete. This does NOT prove the box will still be collecting in an hour -- check status later.'
        exit 0
    }

    Fail 'gate did not pass within 12 minutes -- the analysis loop is not producing rows at cadence (process may still be up, and one row alone does not pass)'
    Invoke-Rollback -RemoteDir $remoteDir | Out-Null
    exit 2
}

<#
[FIX 9, live-execution finding, docs/collector-ops-tooling-spec-back.md §8] Every rollback
path -- place failure (step 6), hash failure (step 7), restart-confirm failure (step 8), and
gate failure (step 9) -- now shares this ONE function instead of three/four hand-copied
restore+restart sequences, which is what let step 6 and step 7's copies silently omit the
gate check step 9's own copy happened to have. Measured on the test box: the hash-failure
branch restored v67, relaunched it, printed "OK relaunched (LAUNCH_SESSION=2)", and exited --
PROCESS confirmed, ANALYSIS LOOP never checked. v67 (and every build before v68) has no
auto_run.start_engaged, so the restored app came back running with auto-run disengaged: up,
not producing rows, reporting success. Measured 3.4 minutes past a 3-minute cadence with
zero new rows.

[FIX 11 correction] The original wording here and in the Fail message below called this
"the collector silently idle" / "NOT capturing" -- WRONG, and important to get right because
it names the wrong emergency. WS streaming trade capture (the TAPE -- the thing genuinely
unrecoverable past ~24h) starts at form load and is independent of auto-run; only the
ANALYSIS LOOP (the BOOK -- verdicts, the analysis_log.csv row this gate actually checks)
needs auto-run engaged. A rollback to a pre-v68 build stops the BOOK, not the TAPE. Measured
live: analysis_log.csv gate failure, trades_2026-08.csv (tape) mtime 11 seconds old at the
same moment -- streaming had not stopped. The gate itself is correct (a missing analysis row
IS a real defect worth escalating loudly) and stays; only the diagnosis in the message was
wrong, and a wrong diagnosis at 3am is worse than no diagnosis.

Q2's original design ("restore + restart, without re-running the full 5-minute gate -- just
the restart") was reviewed and ratified on the reasoning that a stopped collector is worse
than an unverified one. This run proved that reasoning incomplete: an unverified restart can
ALSO leave the analysis loop stopped, just reporting success instead of admitting it. The
wait this function adds to every rollback is the cost of finding that out immediately instead
of leaving it to the next daily glance.

[2026-08-22 cadence gate] That wait is now up to 12 minutes, not 5 -- this function inherits
Wait-DeployGate's tightening automatically, and that is correct. A rollback to v66 or v67
restores a binary with no auto_run.start_engaged, so the app comes back running-but-stopped
and produces ZERO rows. The gate fails, which is exactly FIX 9's purpose: escalate rather
than report OK. Only the escalation LATENCY changes.

STILL UNPROVEN: this rollback path has never run on a green deploy. FIX 9 and FIX 10 are
proven only from the two FAILING attempts, and FIX 10's robocopy /MIR restore has never
executed at all (docs/seat-handover-2026-08-22.md §7). The cadence gate does not change that.

Returns $true only if the restored build is confirmed both running AND producing rows
at cadence.
#>
function Invoke-Rollback {
    param([Parameter(Mandatory = $true)][string]$RemoteDir)
    Restore-DeployBackup -RemoteDir $RemoteDir
    $restartUtc = (Get-Date).ToUniversalTime()
    if (-not (Start-RemoteApp -RemoteDir $RemoteDir)) {
        Fail 'ROLLBACK RESTART DID NOT CONFIRM A RUNNING PROCESS. STOP. Investigate the box by hand -- do not retry any automated action.'
        return $false
    }
    Section 'rollback acceptance gate (2 new CSV rows >=45s apart, within 12 minutes, on the RESTORED build)'
    $gateOk = Wait-DeployGate -RestartUtc $restartUtc -RemoteDir $RemoteDir -ExpectSettingsVersion $null
    if ($gateOk) {
        Ok 'rollback verified -- the restored build is running AND the analysis loop is producing rows. Deploy did not complete; investigate before retrying.'
    } else {
        # [FIX 11] Not "NOT capturing" / "unrecoverable past ~24h" -- that describes the TAPE
        # (WS streaming, independent of auto-run, unaffected by this). This gate only proves
        # the ANALYSIS LOOP, which the restored build may not auto-start (no
        # auto_run.start_engaged before v68). Naming the wrong emergency sends an operator
        # after the wrong fire.
        Fail 'ROLLBACK RESTARTED THE PROCESS, BUT THE ANALYSIS LOOP IS NOT RUNNING AT CADENCE -- fewer than 2 new CSV rows within 12 minutes. Tape capture (WS streaming) is UNAFFECTED and continues regardless -- this is not the ~24h data-loss emergency. It means the restored build is not producing verdicts/rows, most likely because it predates auto_run.start_engaged (v68) and nobody has clicked Start. Engage auto-run on the box by hand, or redeploy a build that sets auto_run.start_engaged. Do not retry any automated action until you know which.'
    }
    return $gateOk
}

<#
Restores the six items from the single-generation backup and reports success/failure.
[Sweep finding, docs/collector-ops-tooling-spec-back.md §8 -- the "fourth instance" the
review asked to look for after FIX 2/7/8 found the same class three times.] RESTORED=done
used to print unconditionally, with no -ErrorAction Stop on the Copy-Item calls and no
check that the six items actually landed back in $dir -- the single most safety-critical
place in this file for that defect class, since a rollback that lies about succeeding is
what Invoke-Rollback's own gate check exists to catch, but a partially-restored box could
fail that gate for a reason unrelated to collection (a missing settings.json, say) and be
misdiagnosed. Now matches FIX 2's backup-verification pattern: every item re-verified
present in $dir (dirs by file COUNT against the backup, not existence alone) before the
line that says RESTORED=done is reached.

[FIX 10, live-execution finding] The directory copy is `robocopy /MIR`, not `Copy-Item
-Recurse`. Copy-Item -Recurse into an EXISTING destination copies the source directory INTO
it rather than merging -- `Copy-Item fonts fonts -Recurse` on a pre-existing `fonts\`
produces `fonts\fonts\`, and each subsequent restore compounds it one level deeper
(`fonts\fonts\fonts\`, measured live). The backup step (FIX 2, above) gets away with plain
Copy-Item -Recurse only because it deletes $bk first, so ITS destination never pre-exists;
a restore's destination is never absent -- that is the entire point of a restore, so the
same call cannot be reused here. `/MIR` makes the destination match the source exactly
(also self-healing any stray nesting on the NEXT restore, though the existing nesting on a
box that hit this bug still needs a one-off manual clean -- robocopy mirrors whatever the
backup itself currently holds, and cannot know the backup's own nesting is wrong).
Robocopy's exit codes are bit flags, not a single success/fail bit: 0-7 are success (1 =
files copied, 2 = extra files removed to match source, 4 = mismatched files), 8+ means at
least one directory failed -- checked as `-ge 8`, never `-ne 0`, which would treat robocopy
doing its job (code 1, 2, or 3) as a failure.
#>
function Restore-DeployBackup {
    param([Parameter(Mandatory = $true)][string]$RemoteDir)
    Section 'restore from _deploy_backup'
    $restoreCmds = @(
        "`$dir = '$RemoteDir'",
        "`$bk = Join-Path `$dir '_deploy_backup'",
        "if (-not (Test-Path `$bk)) { 'ERROR=no backup present'; exit 1 }",
        "try {",
        "  foreach (`$f in @($(($SixFiles | ForEach-Object { "'$_'" }) -join ','))) { Copy-Item (Join-Path `$bk `$f) (Join-Path `$dir `$f) -Force -ErrorAction Stop }",
        "} catch {",
        "  'ERROR=restore failed: ' + `$_.Exception.Message",
        "  exit 1",
        "}",
        "`$roboFailed = @()",
        "foreach (`$d in @($(($SixDirs | ForEach-Object { "'$_'" }) -join ','))) {",
        "  `$src = Join-Path `$bk `$d",
        "  `$dst = Join-Path `$dir `$d",
        "  if (Test-Path `$src) {",
        "    robocopy `$src `$dst /MIR /NFL /NDL /NJH /NJS /NP | Out-Null",
        "    if (`$LASTEXITCODE -ge 8) { `$roboFailed += `$d }",
        "  }",
        "}",
        "if (`$roboFailed.Count -gt 0) { 'ERROR=restore failed: robocopy failed for ' + (`$roboFailed -join ','); exit 1 }",
        "`$missing = @()",
        "foreach (`$f in @($(($SixFiles | ForEach-Object { "'$_'" }) -join ','))) { if (-not (Test-Path (Join-Path `$dir `$f))) { `$missing += `$f } }",
        "foreach (`$d in @($(($SixDirs | ForEach-Object { "'$_'" }) -join ','))) {",
        "  `$bkPath = Join-Path `$bk `$d",
        "  `$dstPath = Join-Path `$dir `$d",
        "  if (-not (Test-Path `$dstPath)) { `$missing += `$d; continue }",
        "  `$bkCount = (Get-ChildItem `$bkPath -Recurse -File | Measure-Object).Count",
        "  `$dstCount = (Get-ChildItem `$dstPath -Recurse -File | Measure-Object).Count",
        "  if (`$bkCount -ne `$dstCount) { `$missing += `$d + ' (file count ' + `$dstCount + ' vs backup ' + `$bkCount + ')' }",
        # robocopy /MIR mirrors whatever $bkPath currently holds -- it cannot tell a
        # STILL-NESTED backup (the exact state a box that hit FIX 10 was left in, and which
        # this script cannot clean without a live instance ID) from a correct one, and an
        # equal file count either side of a nested mirror would otherwise report RESTORED=done
        # over a still-broken layout. Every one of $SixDirs is meant to be flat (no
        # subdirectory of its own); a stray one means the backup itself needs the one-off
        # manual clean this fix could not perform, and that must fail loudly, not silently.
        "  `$nested = (Get-ChildItem `$dstPath -Directory -ErrorAction SilentlyContinue | Measure-Object).Count",
        "  if (`$nested -gt 0) { `$missing += `$d + ' (unexpected nested directory -- the BACKUP itself is still corrupt, needs a one-off manual clean, not a re-run)' }",
        "}",
        "if (`$missing.Count -gt 0) { 'RESTORED=incomplete:' + (`$missing -join ',') } else { 'RESTORED=done' }"
    )
    $rs = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $restoreCmds -TimeoutSec 90
    if ($rs.Status -eq 'Success' -and $rs.StdOut -match 'RESTORED=done') { Ok 'restored from backup -- all six items verified present' }
    else { Fail "RESTORE ITSELF DID NOT CONFIRM -- the box may now be in an unknown state. Stop and investigate by hand. ($($rs.StdOut) $($rs.StdErr))" }
}

<# Launches the app via the measured session-2 scheduled-task mechanism (proposal §2.1),
   using the REAL engine exe path (not the PoC's notepad.exe), then deletes the task. #>
function Start-RemoteApp {
    param([Parameter(Mandatory = $true)][string]$RemoteDir)
    $tn = 'DeribitEngineDeploy'
    $launchCmds = @(
        "`$dir = '$RemoteDir'",
        "`$exe = Join-Path `$dir 'DeribitVerdictEngine.exe'",
        "schtasks /delete /tn $tn /f 2>`$null | Out-Null",
        "schtasks /create /tn $tn /tr `"`$exe`" /sc once /st 00:00 /ru 'administrator' /it /f 2>&1 | Out-Null",
        "schtasks /run /tn $tn 2>&1 | Out-Null",
        "Start-Sleep -Seconds 6",
        "schtasks /delete /tn $tn /f 2>&1 | Out-Null",
        "`$p = Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue",
        "if (`$p) { 'LAUNCHED=true'; 'LAUNCH_SESSION=' + `$p.SessionId } else { 'LAUNCHED=false' }"
    )
    $lr = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $launchCmds -TimeoutSec 60
    if ($lr.Status -eq 'Success' -and $lr.StdOut -match 'LAUNCHED=true') {
        Ok "relaunched ($($lr.StdOut -split "`r?`n" | Where-Object { $_ -match 'LAUNCH_SESSION' }))"
        return $true
    }
    Fail 'relaunch did not confirm a running process'
    return $false
}

<# Polls up to 12 minutes for CADENCE -- at least TWO CSV rows newer than $RestartUtc and at
   least 45 s apart -- plus a non-zero session and (when supplied) a matching settings version.
   The §2.6 acceptance gate, tightened 2026-08-22 (docs/deploy-acceptance-gate-cadence-spec.md).

   WHAT A PASS PROVES: the analysis loop fired MORE THAN ONCE after the restart.
   WHAT IT DOES NOT PROVE: that the collector will still be running in an hour. Two rows defeat
   the observed single-shot failure mode exactly, and nothing beyond it.

   The old one-row form passed the v68 deploy on 2026-08-22 -- and the box then wrote no
   analysis row for 175 minutes while its WS tape kept capturing normally. The gate did not
   merely miss the defect, it reported the opposite, and a handover recorded "proven in
   production conditions" on the strength of it. A marker you print is not a property you
   checked. #>
function Wait-DeployGate {
    param(
        [Parameter(Mandatory = $true)][datetime]$RestartUtc,
        [Parameter(Mandatory = $true)][string]$RemoteDir,
        [string]$ExpectSettingsVersion
    )
    $restartIso = $RestartUtc.ToString('yyyy-MM-ddTHH:mm:ss')
    # 12 min, derived not guessed: worst-case cadence is the 3-minute ASIA/LONDON execution
    # resolution (settings.json session_volume.sessions[].execution_resolution, v36). Two rows
    # need up to 3 min to the first bar roll + ~1 min analysis + 3 min to the second, and the
    # on_close feed-stall backstop is max(interval, (execRes+1)*60s) = 4 min. 12 gives headroom
    # without being open-ended. The SUCCESS path is unaffected -- it returns on the first
    # passing poll; only a genuine failure waits longer, and a genuine failure means STOP anyway.
    $deadline = (Get-Date).AddMinutes(12)
    while ((Get-Date) -lt $deadline) {
        $gateCmds = @(
            "`$dir = '$RemoteDir'",
            "`$p = Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue",
            "'GATE_PID=' + `$(if (`$p) { `$p.Id } else { 'NONE' })",
            "'GATE_SESSION=' + `$(if (`$p) { `$p.SessionId } else { -1 })",
            "`$sj = Join-Path `$dir 'settings.json'",
            "if (Test-Path `$sj) { 'GATE_SETTINGS_VERSION=' + ((Get-Content `$sj -TotalCount 2) -join ' ') }",
            "`$csv = Join-Path `$dir 'analysis_log.csv'",
            "if (Test-Path `$csv) {",
            "  `$restart = [datetime]::Parse('$restartIso')",
            # -Tail 50, NOT a full read: the production book is 22 MB and this runs every 20 s
            # over SSM. 50 rows is ~25 min of 1-min cadence -- far more than the 12-minute window
            # can consume. The header line and any malformed row throw inside [datetime]::Parse
            # and are skipped by the catch. That is the intent, not an accident: the parse IS the
            # validation. Do NOT "improve" it into a regex match on the timestamp shape.
            "  `$after = @()",
            "  foreach (`$ln in @(Get-Content `$csv -Tail 50)) {",
            "    try { `$dt = [datetime]::Parse(`$ln.Split(',')[0]); if (`$dt -gt `$restart) { `$after += `$dt } } catch { }",
            "  }",
            "  'GATE_ROWS_AFTER=' + `$after.Count",
            "  if (`$after.Count -ge 2) { 'GATE_SPAN_SEC=' + [math]::Round((`$after[-1] - `$after[0]).TotalSeconds, 0) }",
            "  else { 'GATE_SPAN_SEC=0' }",
            "} else { 'GATE_ROWS_AFTER=0'; 'GATE_SPAN_SEC=0' }"
        )
        $g = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $gateCmds -TimeoutSec 60
        if ($g.Status -eq 'Success') {
            $gv = ConvertFrom-KeyValueLines $g.StdOut
            $sessionOk = ($gv['GATE_SESSION'] -and [int]$gv['GATE_SESSION'] -gt 0)
            $rowsAfter = 0; [void][int]::TryParse($gv['GATE_ROWS_AFTER'], [ref]$rowsAfter)
            $spanSec   = 0; [void][int]::TryParse($gv['GATE_SPAN_SEC'],   [ref]$spanSec)
            # CADENCE, not existence. One row newer than the restart is exactly what a single-shot
            # auto-run produces -- measured on the t2.micro 2026-08-22, where this gate passed on one
            # row and the box then wrote nothing for 175 minutes. Two rows >=45 s apart prove the loop
            # fired MORE THAN ONCE. It does not prove the box will still be collecting in an hour.
            $rowOk = ($rowsAfter -ge 2 -and $spanSec -ge 45)
            $versionOk = (-not $ExpectSettingsVersion) -or ($gv['GATE_SETTINGS_VERSION'] -eq $ExpectSettingsVersion)
            Info "poll: PID=$($gv['GATE_PID']) session=$($gv['GATE_SESSION']) rowsAfterRestart=$rowsAfter spanSec=$spanSec settings=[$($gv['GATE_SETTINGS_VERSION'])]"
            if ($sessionOk -and $rowOk -and $versionOk) { return $true }
        }
        Start-Sleep -Seconds 20
    }
    return $false
}

# ===========================================================================
switch ($Verb) {
    'status' { Invoke-Status }
    'fetch'  { Invoke-Fetch }
    'deploy' { Invoke-Deploy }
}
