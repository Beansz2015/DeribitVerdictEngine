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
        "'SNAPSHOT_CLEANED=true'"
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

    $placeCmds = @("`$dir = '$remoteDir'")
    foreach ($f in $SixFiles) { $placeCmds += "aws s3 cp `"s3://$Bucket/$dprefix/$f`" (Join-Path `$dir '$f') --only-show-errors" }
    foreach ($d in $SixDirs)  { $placeCmds += "aws s3 cp `"s3://$Bucket/$dprefix/$d`" (Join-Path `$dir '$d') --recursive --only-show-errors" }
    $placeCmds += "'PLACED=done'"
    $place = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $placeCmds -TimeoutSec 180
    if ($place.Status -ne 'Success' -or $place.StdOut -notmatch 'PLACED=done') {
        Fail 'place step did not confirm completion -- box may be in a PARTIAL state. Restoring from backup and restarting on the OLD build (the app is currently stopped and must not be left that way).'
        Restore-DeployBackup -RemoteDir $remoteDir
        Start-RemoteApp -RemoteDir $remoteDir | Out-Null
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
        Fail 'hash verification failed -- restoring from backup and restarting on the OLD build (the app is currently stopped and must not be left that way)'
        Restore-DeployBackup -RemoteDir $remoteDir
        Start-RemoteApp -RemoteDir $remoteDir | Out-Null
        exit 2
    }

    # -- Step 8: restart via the §2.1 scheduled-task mechanism (measured, not assumed). -----
    Section '8. restart'
    $restartUtc = (Get-Date).ToUniversalTime()
    if (-not (Start-RemoteApp -RemoteDir $remoteDir)) {
        Fail 'restart did not confirm a running process -- restoring from backup and retrying restart once'
        Restore-DeployBackup -RemoteDir $remoteDir
        Start-RemoteApp -RemoteDir $remoteDir | Out-Null
    }

    # -- Step 9: verify for real -- a NEW CSV row within 5 minutes (§2.6), not a file compare.
    Section '9. acceptance gate (new CSV row within 5 minutes)'
    $gateOk = Wait-DeployGate -RestartUtc $restartUtc -RemoteDir $remoteDir -ExpectSettingsVersion $localSettingsVersion

    if ($gateOk) {
        Ok 'ACCEPTED -- new row landed, session non-zero, settings version matches. Deploy complete.'
        exit 0
    }

    Fail 'gate did not pass within 5 minutes -- restoring from backup and restarting'
    Restore-DeployBackup -RemoteDir $remoteDir
    $rollbackRestartUtc = (Get-Date).ToUniversalTime()
    Start-RemoteApp -RemoteDir $remoteDir | Out-Null
    $rollbackOk = Wait-DeployGate -RestartUtc $rollbackRestartUtc -RemoteDir $remoteDir -ExpectSettingsVersion $null
    if ($rollbackOk) { Warn 'rollback verified (new row landed on the RESTORED build). STOP. Do not retry the deploy. Investigate before trying again.' }
    else { Fail 'rollback ALSO did not produce a new row. STOP. Investigate the box by hand — do not retry any automated action.' }
    exit 2
}

<# Restores the six items from the single-generation backup and reports success/failure. #>
function Restore-DeployBackup {
    param([Parameter(Mandatory = $true)][string]$RemoteDir)
    Section 'restore from _deploy_backup'
    $restoreCmds = @(
        "`$dir = '$RemoteDir'",
        "`$bk = Join-Path `$dir '_deploy_backup'",
        "if (-not (Test-Path `$bk)) { 'ERROR=no backup present'; exit 1 }",
        "foreach (`$f in @($(($SixFiles | ForEach-Object { "'$_'" }) -join ','))) { Copy-Item (Join-Path `$bk `$f) (Join-Path `$dir `$f) -Force }",
        "foreach (`$d in @($(($SixDirs | ForEach-Object { "'$_'" }) -join ','))) { Copy-Item (Join-Path `$bk `$d) (Join-Path `$dir `$d) -Recurse -Force }",
        "'RESTORED=done'"
    )
    $rs = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $restoreCmds -TimeoutSec 90
    if ($rs.Status -eq 'Success' -and $rs.StdOut -match 'RESTORED=done') { Ok 'restored from backup' }
    else { Fail 'RESTORE ITSELF DID NOT CONFIRM -- the box may now be in an unknown state. Stop and investigate by hand.' }
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

<# Polls up to 5 minutes for a CSV row newer than $RestartUtc, a non-zero session, and
   (when supplied) a matching settings version -- the §2.6 acceptance gate. #>
function Wait-DeployGate {
    param(
        [Parameter(Mandatory = $true)][datetime]$RestartUtc,
        [Parameter(Mandatory = $true)][string]$RemoteDir,
        [string]$ExpectSettingsVersion
    )
    $restartIso = $RestartUtc.ToString('yyyy-MM-ddTHH:mm:ss')
    $deadline = (Get-Date).AddMinutes(5)
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
            "  `$l = Get-Content `$csv",
            "  if (`$l.Count -gt 1) {",
            "    `$lastTs = [datetime]::Parse(`$l[-1].Split(',')[0])",
            "    `$restart = [datetime]::Parse('$restartIso')",
            "    'GATE_LAST_ROW_NEWER=' + (`$lastTs -gt `$restart)",
            "  } else { 'GATE_LAST_ROW_NEWER=False' }",
            "} else { 'GATE_LAST_ROW_NEWER=False' }"
        )
        $g = Invoke-RemotePs -InstanceId $InstanceId -Region $Region -Commands $gateCmds -TimeoutSec 60
        if ($g.Status -eq 'Success') {
            $gv = ConvertFrom-KeyValueLines $g.StdOut
            $sessionOk = ($gv['GATE_SESSION'] -and [int]$gv['GATE_SESSION'] -gt 0)
            $rowOk = ($gv['GATE_LAST_ROW_NEWER'] -eq 'True')
            $versionOk = (-not $ExpectSettingsVersion) -or ($gv['GATE_SETTINGS_VERSION'] -eq $ExpectSettingsVersion)
            Info "poll: PID=$($gv['GATE_PID']) session=$($gv['GATE_SESSION']) newerRow=$($gv['GATE_LAST_ROW_NEWER']) settings=[$($gv['GATE_SETTINGS_VERSION'])]"
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
