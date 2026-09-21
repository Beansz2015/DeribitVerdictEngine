#requires -Version 5.1
<#
  tools/checks/commit-walker.ps1 -- audits the self-declared `[no-engine-change]` commit
  tag against what a commit actually touched.

  ADVISORY ONLY (docs/commit-walker-check-spec.md, top matter -- "This is NOT a gate",
  same three reasons as docs/rider-travel-check-spec.md D-3). Not wired into
  verify-gate.ps1 or the pre-push hook.

  WHY: the `[no-engine-change]` tag is self-declared and nothing verifies it, yet it
  gates two obligations in CLAUDE.md -- a DeribitIndicatorProject.md section 15 version-
  history entry, and a settings.json version bump when config keys move. This tool reads
  git history; it never writes to a tracked source file.

  THE ARCHITECTURE (docs/commit-walker-check-spec.md section 2): a cheap path rule
  settles about 93% of commits in agreement with the tag (both classes are printed as
  COUNTS ONLY -- never judged). The remaining ~7% residual is genuinely ambiguous either
  way and is the ONLY population sent to Jev (TypeSafe's System One model). Judging the
  whole window would burn tokens on cases the path rule already settles and would
  contaminate the measurement of the harness itself.

  THE FOUR TRAPS (docs/commit-walker-check-spec.md section 0):
    1. Merge commits return an EMPTY file list from `git show --name-only`, so they look
       like "touched no engine path" and pollute the residual. Filtered at the `git log`
       step with --no-merges, never only at a later `show` step.
    2. Composing the two Noul diagnostics with AND. MEASURED elsewhere in this programme
       to score 0 of 8 against a single Choice's 4 of 8. The verdict below is read from
       the `verdict` Choice answer ALONE -- see the line marked TRAP 2.
    3. Sending diff bodies as state. Only the subject, touched paths and per-file
       added/deleted counts travel -- never the diff body.
    4. Judging all commits instead of the residual. Jev sees ONLY the residual class
       (RESIDUAL_TAGGED_BUT_ENGINE_PATH + RESIDUAL_UNTAGGED_NO_ENGINE_PATH).

  ENGINE PATHS, per docs/commit-walker-check-spec.md section 2: a root-level `*.vb`, or
  anything under `Core/`, `analysis/`, `UI/`, or `settings.json` itself. `tools/` is
  DELIBERATELY not an engine path -- those are offline, host-agnostic utilities.

  USAGE:
    set -a; . ./typesafe.local.env; set +a   # loads TYPESAFE_API_KEY (bash)
    powershell -NoProfile -File tools/checks/commit-walker.ps1 `
      -Count 300 -BaselinePath <path to your own pre-written read, see the shadow-mode
      protocol at docs/harness-shadow-mode-protocol.md section 2 step 3>

  EXIT CODES (section 4.5):
    0 - every residual commit judged tag_correct
    1 - at least one judged tag_wrong or ambiguous
    2 - baseline missing, classifier suspect, residual over 25%, or API failure
#>
[CmdletBinding()]
param(
    [int]$Count = 300,
    [string]$BaselinePath = 'commit-walker-baseline.json',
    [string]$OutPath = 'commit-walker-report.md'
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Invoke-Jev is shared with tools/checks/rider-travel.ps1 (docs/commit-walker-check-spec.md
# section 6: "Reuse its Invoke-Jev verbatim -- do not write a second HTTP call"). Do not
# re-implement it here -- see tools/checks/lib/InvokeJev.ps1's header comment for why the
# manual UTF-8 body encoding is load-bearing, not cosmetic.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# Engine-path rule, docs/commit-walker-check-spec.md section 2. Git always reports paths
# with forward slashes, on every OS, so these patterns need no separator translation.
function Test-EngineTouch([string[]]$paths) {
    foreach ($p in $paths) {
        if ($p -eq 'settings.json') { return $true }
        if ($p -match '^(Core|analysis|UI)/') { return $true }
        if ($p -match '^[^/]+\.vb$') { return $true }
    }
    return $false
}

# ---------------------------------------------------------------------------------------
# Step 1 (section 4.2): walk the log with --no-merges AT THE LOG STEP (trap 1) -- a merge
# commit must never reach the classifier, not merely be filtered out again downstream.
# --no-renames keeps numstat output as plain add/delete pairs per path, never the
# "{old => new}" compact rename form, which is simpler and safer to classify against.
# ---------------------------------------------------------------------------------------
$raw = [string[]](& git -C $repo log --no-merges --no-renames -$Count --numstat --format="COMMITSTART`t%H`t%s")

$commits = New-Object System.Collections.Generic.List[object]
$cur = $null
foreach ($line in $raw) {
    if ($line.StartsWith("COMMITSTART`t")) {
        if ($null -ne $cur) { $commits.Add($cur) }
        $parts = $line -split "`t"
        $subject = if ($parts.Length -gt 3) { ($parts[2..($parts.Length - 1)] -join "`t") } else { $parts[2] }
        $cur = [PSCustomObject]@{
            Hash       = $parts[1]
            Subject    = $subject
            Paths      = New-Object System.Collections.Generic.List[string]
            LineCounts = New-Object System.Collections.Generic.List[object]
        }
        continue
    }
    if ($line.Trim() -eq '') { continue }
    if ($null -eq $cur) { continue }
    $nsParts = $line -split "`t"
    if ($nsParts.Length -lt 3) { continue }
    $added   = if ($nsParts[0] -eq '-') { 0 } else { [int]$nsParts[0] }
    $deleted = if ($nsParts[1] -eq '-') { 0 } else { [int]$nsParts[1] }
    $path    = $nsParts[2]
    $cur.Paths.Add($path)
    $cur.LineCounts.Add([PSCustomObject]@{ Path = $path; Added = $added; Deleted = $deleted })
}
if ($null -ne $cur) { $commits.Add($cur) }

$commitsWalked = $commits.Count

# MERGES_EXCLUDED: how many merge commits sit between HEAD and the oldest commit this
# walk had to reach in order to collect $commitsWalked NON-merge commits. --no-merges
# applies its filter DURING the walk, so git may have to look further back than
# $Count raw commits to satisfy $Count non-merge ones; the gap between that raw span
# and $commitsWalked is exactly the merge commits skipped over.
$mergesExcluded = 0
if ($commitsWalked -gt 0) {
    $oldestSha = $commits[$commitsWalked - 1].Hash
    $allShas = [string[]](& git -C $repo log --format=%H)
    $idx = [Array]::IndexOf($allShas, $oldestSha)
    if ($idx -ge 0) {
        $totalSpan = $idx + 1
        $mergesExcluded = $totalSpan - $commitsWalked
    }
}

# ---------------------------------------------------------------------------------------
# Step 2 (section 4.2): classify each non-merge commit against the path rule.
# ---------------------------------------------------------------------------------------
$agreeTagged   = New-Object System.Collections.Generic.List[object]
$agreeUntagged = New-Object System.Collections.Generic.List[object]
$residualTaggedButEngine   = New-Object System.Collections.Generic.List[object]
$residualUntaggedNoEngine  = New-Object System.Collections.Generic.List[object]

foreach ($c in $commits) {
    $tagged = $c.Subject.Contains('[no-engine-change]')
    $touchedEngine = Test-EngineTouch $c.Paths
    $c | Add-Member -NotePropertyName Tagged -NotePropertyValue $tagged
    $c | Add-Member -NotePropertyName TouchedEngine -NotePropertyValue $touchedEngine

    if ($tagged -and -not $touchedEngine)      { $agreeTagged.Add($c) }
    elseif ((-not $tagged) -and $touchedEngine) { $agreeUntagged.Add($c) }
    elseif ($tagged -and $touchedEngine)        { $residualTaggedButEngine.Add($c) }
    else                                        { $residualUntaggedNoEngine.Add($c) }
}

$residualTotal = $residualTaggedButEngine.Count + $residualUntaggedNoEngine.Count
$residualPct = if ($commitsWalked -gt 0) { [math]::Round(($residualTotal / $commitsWalked) * 100, 1) } else { 0 }

function Write-Coverage([int]$walked, [int]$merges, [int]$agreeT, [int]$agreeU, [int]$resT, [int]$resU, [int]$resTotal, $resPct, [int]$judged) {
    "COMMITS_WALKED=$walked"
    "MERGES_EXCLUDED=$merges"
    "AGREE_TAGGED_NO_ENGINE_PATH=$agreeT"
    "AGREE_UNTAGGED_ENGINE_PATH=$agreeU"
    "RESIDUAL_TAGGED_BUT_ENGINE_PATH=$resT"
    "RESIDUAL_UNTAGGED_NO_ENGINE_PATH=$resU"
    "RESIDUAL_TOTAL=$resTotal"
    "RESIDUAL_PCT=$resPct"
    "COMMITS_JUDGED=$judged"
}

# ---------------------------------------------------------------------------------------
# Step 3 (section 4.2): coverage block prints before anything else on every exit path.
# The two agreeing classes are printed as COUNTS ONLY here -- they are never judged.
# ---------------------------------------------------------------------------------------
Write-Coverage $commitsWalked $mergesExcluded $agreeTagged.Count $agreeUntagged.Count `
    $residualTaggedButEngine.Count $residualUntaggedNoEngine.Count $residualTotal $residualPct 0

# Shadow-mode protocol step 2 (docs/harness-shadow-mode-protocol.md section 2): "The
# harness prints its candidate list first, with no judgments." Printed on every run,
# baseline present or not, so the seat can label against the SAME list the detector sees.
$residualAll = @($residualTaggedButEngine.ToArray() + $residualUntaggedNoEngine.ToArray())
if ($residualAll.Count -gt 0) {
    "RESIDUAL_CANDIDATES (no judgments, for baseline labelling):"
    foreach ($c in $residualAll) {
        $cls = if ($c.Tagged) { 'TAGGED_BUT_ENGINE_PATH' } else { 'UNTAGGED_NO_ENGINE_PATH' }
        "  $($c.Hash.Substring(0,10)) [$cls] $($c.Subject)"
    }
}

# ⛔ A broken classifier is loud (section 4.3): RESIDUAL_TOTAL=0 on a real window means
# the classifier broke, not that the repo is clean -- section 2 measured a real residual
# of about 22 (10 + 12) on a 300-commit window.
if ($residualTotal -eq 0) {
    "EXIT_REASON=CLASSIFIER_SUSPECT"
    "RESIDUAL_TOTAL read 0 over a $commitsWalked-commit window. docs/commit-walker-check-spec.md section 2 measured a real residual of about 22 of 300 -- treat a zero here as the path rule or the tag-detection string being broken, never as a clean repo."
    exit 2
}

# Escalation trigger (section 0 / section 4.3): residual over 25% means the path rule's
# whole architecture does not hold on this window -- stop and report, do not judge on.
if ($residualPct -gt 25) {
    "EXIT_REASON=RESIDUAL_TOO_HIGH"
    "RESIDUAL_PCT=$residualPct exceeds the 25% escalation trigger in docs/commit-walker-check-spec.md section 0. The engine-path rule in section 2 is measured to settle about 93% of commits; a residual this large means the rule does not hold for this window and needs review before any Jev call is spent on it."
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 4 (section 4.2, section 4.4): refuse to proceed without a baseline. Structural,
# before any API key read -- same mechanism and rationale as
# docs/rider-travel-check-spec.md D-4. This harness has NO pre-existing ground truth
# (docs/commit-walker-check-spec.md section 4.4): the seat's hand baseline IS the
# labelling, which makes writing it before the run more load-bearing here, not less.
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each RESIDUAL_CANDIDATES commit above BEFORE running this tool, as JSON, one key per full commit hash, values one of tag_correct|tag_wrong|unsure. Example:"
    '{ "88538d7...": "tag_correct", "4ab0b25...": "tag_wrong" }'
    "The whole point of the first run is comparing an independent human read against Jev's; looking first makes that comparison worthless permanently. See docs/harness-shadow-mode-protocol.md."
    exit 2
}
$baselineRaw = Get-Content -Raw -Path $baselineFull | ConvertFrom-Json
$baseline = @{}
if ($null -ne $baselineRaw) {
    foreach ($p in $baselineRaw.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}

# ---------------------------------------------------------------------------------------
# Step 5 (section 4.2, section 6): one Jev request per RESIDUAL commit only (trap 4).
# ---------------------------------------------------------------------------------------
$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set in the environment. Load typesafe.local.env first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

$verdictCriteria = @{
    tag_correct = 'The tag state matches the actual effect: `commit.declared_no_engine_change` is true and these changes do NOT alter what the running application computes, decides, logs, or displays (refactor, comments, tests, docs, or tooling only); OR `commit.declared_no_engine_change` is false and these changes DO alter runtime behaviour.'
    tag_wrong   = 'The tag state contradicts the actual effect: `commit.declared_no_engine_change` is true but these changes DO alter what the running application computes, decides, logs, or displays; OR `commit.declared_no_engine_change` is false but these changes are refactor-only (no observable output change).'
    ambiguous   = '`touched_paths` and `per_file_line_counts` do not give enough information to decide whether runtime behaviour changed.'
}

$results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''
$usageInputTokens = 0
$usageOutputTokens = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($c in $residualAll) {
    $state = @{
        commit = @{
            subject                   = $c.Subject
            declared_no_engine_change = $c.Tagged
        }
        touched_paths = @($c.Paths)
        per_file_line_counts = @($c.LineCounts | ForEach-Object { @{ path = $_.Path; added = $_.Added; deleted = $_.Deleted } })
    }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            changes_runtime_behaviour = @{
                type = 'noul'
                instructions = 'Do these changes alter what the running application computes, decides, logs or displays, as opposed to refactoring, comments, tests or documentation? Judge only from `commit.subject`, `touched_paths` and `per_file_line_counts`.'
            }
            is_refactor_only = @{
                type = 'noul'
                instructions = 'Do these changes restructure code without altering its observable output? Judge only from `commit.subject`, `touched_paths` and `per_file_line_counts`.'
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify whether `commit.declared_no_engine_change` correctly describes this commit's actual effect, using `touched_paths` and `per_file_line_counts` to judge what the commit did."
                criteria = $verdictCriteria
            }
        }
    }

    $call = Invoke-Jev $apiKey $body
    if (-not $call.Ok) {
        $apiFailed = $true
        $apiFailMsg = "Jev request failed for $($c.Hash.Substring(0,10)): $($call.Error)"
        break
    }

    $ans = $call.Response.answers
    # --- TRAP 2 (docs/commit-walker-check-spec.md section 0 / section 6): the verdict
    # comes from the `verdict` Choice answer ALONE. The two Noul answers below are
    # diagnostics ONLY -- never combined with `-and`/`-or` into a derived verdict. This
    # is the one line a reviewer checks. Pattern copied from tools/checks/rider-travel.ps1.
    $verdict = $ans.verdict.choice

    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens)  { $usageInputTokens  += [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOutputTokens += [int]$call.Response.usage.output_tokens }
    }

    $results.Add([PSCustomObject]@{
        Hash               = $c.Hash
        Subject            = $c.Subject
        Class              = if ($c.Tagged) { 'TAGGED_BUT_ENGINE_PATH' } else { 'UNTAGGED_NO_ENGINE_PATH' }
        Verdict            = $verdict
        VerdictConfidence  = $ans.verdict.confidence
        ChangesRuntimeNoul = $ans.changes_runtime_behaviour.noul
        RefactorOnlyNoul   = $ans.is_refactor_only.noul
        Baseline           = if ($baseline.ContainsKey($c.Hash)) { $baseline[$c.Hash] } else { $null }
    })
}
$sw.Stop()

if ($apiFailed) {
    Write-Coverage $commitsWalked $mergesExcluded $agreeTagged.Count $agreeUntagged.Count `
        $residualTaggedButEngine.Count $residualUntaggedNoEngine.Count $residualTotal $residualPct $results.Count
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 6 (section 4.2, section 4.5): report, then exit from `verdict` alone.
# ---------------------------------------------------------------------------------------
Write-Coverage $commitsWalked $mergesExcluded $agreeTagged.Count $agreeUntagged.Count `
    $residualTaggedButEngine.Count $residualUntaggedNoEngine.Count $residualTotal $residualPct $results.Count

"USAGE_INPUT_TOKENS=$usageInputTokens"
"USAGE_OUTPUT_TOKENS=$usageOutputTokens"
"WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"

"PER_COMMIT_RESULTS:"
foreach ($res in $results) {
    $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
             elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
             elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
             else { 'DISAGREE' }
    "  $($res.Hash.Substring(0,10)) [$($res.Class)] verdict=$($res.Verdict) (confidence=$([math]::Round([double]$res.VerdictConfidence,2))) baseline=$($res.Baseline) [$agree] changes_runtime_noul=$([math]::Round([double]$res.ChangesRuntimeNoul,2)) refactor_only_noul=$([math]::Round([double]$res.RefactorOnlyNoul,2))"
}

# Write the markdown report.
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Commit-walker check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/commit-walker.ps1`` against ``-Count $Count`` at ``HEAD``.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| COMMITS_WALKED | $commitsWalked |")
$reportLines.Add("| MERGES_EXCLUDED | $mergesExcluded |")
$reportLines.Add("| AGREE_TAGGED_NO_ENGINE_PATH | $($agreeTagged.Count) |")
$reportLines.Add("| AGREE_UNTAGGED_ENGINE_PATH | $($agreeUntagged.Count) |")
$reportLines.Add("| RESIDUAL_TAGGED_BUT_ENGINE_PATH | $($residualTaggedButEngine.Count) |")
$reportLines.Add("| RESIDUAL_UNTAGGED_NO_ENGINE_PATH | $($residualUntaggedNoEngine.Count) |")
$reportLines.Add("| RESIDUAL_TOTAL | $residualTotal |")
$reportLines.Add("| RESIDUAL_PCT | $residualPct |")
$reportLines.Add("| COMMITS_JUDGED | $($results.Count) |")
$reportLines.Add("| USAGE_INPUT_TOKENS | $usageInputTokens |")
$reportLines.Add("| USAGE_OUTPUT_TOKENS | $usageOutputTokens |")
$reportLines.Add("| WALL_TIME_SEC | $([math]::Round($sw.Elapsed.TotalSeconds,2)) |")
$reportLines.Add('')
$reportLines.Add('## Per-commit verdicts (residual only -- the two agreeing classes above are counts only, never judged)')
$reportLines.Add('')
$reportLines.Add('| Hash | Class | Verdict | Confidence | Baseline | Agreement | changes_runtime (noul) | is_refactor_only (noul) | Subject |')
$reportLines.Add('|---|---|---|---|---|---|---|---|---|')
foreach ($res in $results) {
    $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
             elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
             elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
             else { 'DISAGREE' }
    $subjEsc = $res.Subject -replace '\|', '\|'
    $reportLines.Add("| $($res.Hash.Substring(0,10)) | $($res.Class) | $($res.Verdict) | $([math]::Round([double]$res.VerdictConfidence,2)) | $($res.Baseline) | $agree | $([math]::Round([double]$res.ChangesRuntimeNoul,2)) | $([math]::Round([double]$res.RefactorOnlyNoul,2)) | $subjEsc |")
}
$reportFull = Resolve-RepoPath $OutPath
Set-Content -Encoding UTF8 -Path $reportFull -Value ($reportLines -join "`r`n")
"Report written to $OutPath"

$anyBad = @($results | Where-Object { $_.Verdict -eq 'tag_wrong' -or $_.Verdict -eq 'ambiguous' }).Count -gt 0
if ($anyBad) { exit 1 } else { exit 0 }
