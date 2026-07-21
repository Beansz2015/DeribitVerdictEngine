#requires -Version 5.1
<#
  tools/checks/verify-gate.ps1 -- single source of truth for the build + harness
  verification gate (dev-workflow-automation-proposal.md, Items 1 + 3).

  Modes:
    local-fast : build AutoTweaker + OrderCheck (Release) + run the harness;
                 parity/version heuristics = WARN; ALWAYS exits 0 (advisory -- the
                 Claude Code Stop hook uses this so it never blocks ending a turn).
    prepush    : + solution build (Release); parity = FAIL-without-token; version =
                 WARN; exits 1 on any build/harness/parity failure (git pre-push hook).
    ci         : identical to prepush; range = the push event (GitHub Actions).

  Display-parity (reconciled to post-P5b state 2026-06-30): the legacy text renderers
  (MainForm_Render_Header/Sections) were retired in the reskin. The live parity pair is
  the plaintext snapshot <-> the card binding. If the snapshot changed but the card did
  NOT (and no [no-card-surface] token in the range's commit messages), flag it -- the card
  is the historically-unchecked third surface (CLAUDE.md display-string parity rule).

  Settings-version nudge: an engine-behaviour path changed but settings.json's "version"
  line did not (and no [no-engine-change] token) -> WARN only (the "is this a behaviour
  change?" call is too soft to hard-block).

  No AI, no network, no Anthropic spend -- pure dotnet + git. Exit 0 = pass (warnings
  allowed) or advisory mode; exit 1 = build/harness/parity failure (prepush/ci only).
#>
[CmdletBinding()]
param(
    [ValidateSet('local-fast', 'prepush', 'ci')]
    [string]$Mode = 'local-fast',
    [string]$BaseRef = 'origin/master'
)

# Continue (not Stop): native dotnet/git non-zero exits are handled via $LASTEXITCODE,
# not exceptions -- avoids the PS 5.1 NativeCommandError-on-stderr foot-gun.
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repo

$script:failed = $false
$warnings = New-Object System.Collections.Generic.List[string]

function Section($t) { Write-Host ""; Write-Host "=== $t ===" }
function Ok($m)   { Write-Host "OK    $m" -ForegroundColor Green }
function Warn($m) { $warnings.Add($m) | Out-Null; Write-Host "WARN  $m" -ForegroundColor Yellow }
function Fail($m) { $script:failed = $true; Write-Host "FAIL  $m" -ForegroundColor Red }

function Build($proj) {
    Section "build $proj"
    & dotnet build $proj -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { Fail "build failed: $proj"; return $false }
    Ok "build $proj"
    return $true
}

# --- builds ---
$ok = $true
if ($Mode -ne 'local-fast') { if (-not (Build 'DeribitVerdictEngine.sln')) { $ok = $false } }
if ($ok) { if (-not (Build 'tools/AutoTweaker/AutoTweaker.vbproj')) { $ok = $false } }
# R1 rider (2026-07-21): WhatIfRunner was invisible to the gate for four days when
# #6 broke it (LevelAbsorptionTracker missing from its .vbproj). One extra build closes
# the hole; the runtime cost is one small Release build.
if ($ok) { if (-not (Build 'tools/WhatIfRunner/WhatIfRunner.vbproj')) { $ok = $false } }
if ($ok) { if (-not (Build 'verify/ordercheck/OrderCheck.vbproj')) { $ok = $false } }

# --- harness ---
if ($ok) {
    Section 'harness'
    $out = & dotnet run --project 'verify/ordercheck/OrderCheck.vbproj' -c Release --no-build
    $code = $LASTEXITCODE
    $out | ForEach-Object { Write-Host $_ }
    if ($code -ne 0 -or -not ($out -match 'ALL PASS')) {
        Fail "harness did not report ALL PASS (exit $code)"
    } else {
        Ok 'harness ALL PASS'
    }
}

# --- diff range ---
function ResolveBase {
    if ($Mode -eq 'local-fast') { return 'HEAD' }
    & git rev-parse --verify --quiet "$BaseRef^{commit}" *> $null
    if ($LASTEXITCODE -eq 0) { return $BaseRef }
    & git rev-parse --verify --quiet 'HEAD~1^{commit}' *> $null
    if ($LASTEXITCODE -eq 0) { return 'HEAD~1' }
    return $null
}
$base = ResolveBase

if ($Mode -eq 'local-fast') {
    $changed = @(& git diff --name-only HEAD)
    $msgs    = (& git log -1 --format=%B) -join "`n"
} elseif ($null -eq $base) {
    $changed = @()
    $msgs    = ''
} else {
    $changed = @(& git diff --name-only "$base" HEAD)
    $msgs    = (& git log "$base..HEAD" --format=%B) -join "`n"
}

# --- 3a display-parity (post-P5b: snapshot <-> card) ---
Section 'display-parity'
$snap = 'UI/MainForm_PlaintextSnapshot.vb'
$card = 'UI/MainForm_Render_Cards.vb'
if (($changed -contains $snap) -and -not ($changed -contains $card)) {
    if ($msgs -match '\[no-card-surface\]') {
        Ok 'snapshot changed without card, but [no-card-surface] token present'
    } elseif ($Mode -eq 'local-fast') {
        Warn 'snapshot changed but card did not (advisory -- update the card or add [no-card-surface])'
    } else {
        Fail 'snapshot changed but card did not, and no [no-card-surface] token in range'
    }
} else {
    Ok 'no snapshot/card drift detected'
}

# --- 3b settings-version-bump nudge (WARN only) ---
Section 'version-bump'
$enginePrefixes = @('Core/', 'DynamicNorms.vb', 'analysis/')
$engineChanged = $false
foreach ($f in $changed) {
    foreach ($p in $enginePrefixes) { if ($f -like "$p*") { $engineChanged = $true; break } }
    if ($engineChanged) { break }
}
if ($engineChanged) {
    if ($Mode -eq 'local-fast') { $sd = & git diff HEAD -- settings.json }
    elseif ($null -eq $base)    { $sd = '+ "version":' }   # can't tell -> don't nag
    else                        { $sd = & git diff "$base" HEAD -- settings.json }
    if ($sd -match '^\+\s*"version"') {
        Ok 'engine-path change accompanied by a settings.json version bump'
    } elseif ($msgs -match '\[no-engine-change\]') {
        Ok 'engine path changed but [no-engine-change] token present'
    } else {
        Warn 'engine-path change without a settings.json version bump (nudge only)'
    }
} else {
    Ok 'no engine-path change'
}

# --- result ---
Section 'result'
if ($warnings.Count -gt 0) { Write-Host ("{0} warning(s)" -f $warnings.Count) -ForegroundColor Yellow }
if ($script:failed) {
    if ($Mode -eq 'local-fast') {
        Write-Host 'ADVISORY: gate found failures (not blocking on Stop).' -ForegroundColor Yellow
        exit 0
    }
    Write-Host 'GATE FAILED' -ForegroundColor Red
    exit 1
}
Write-Host 'GATE PASSED' -ForegroundColor Green
exit 0
