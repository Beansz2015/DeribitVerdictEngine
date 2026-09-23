#requires -Version 5.1
<#
  tools/checks/doc-scanner.ps1 -- harness 4 of the Jev programme: find lines in the docs that
  claim to be current and are not (version rot, identifier misuse, stale state).

  Spec: docs/doc-scanner-check-spec.md, INCLUDING its dated amendment of 2026-09-23 (UTC)
  (orchestrator rulings DS-A (g) and DS-B (b)), which overrides the text below it.
  Build record: docs/doc-scanner-build-spec-back.md.

  ADVISORY ONLY. Not a gate (spec top matter). Never wired into verify-gate.ps1 or the
  pre-push hook.

  THE SPLIT (spec section 1): CODE enumerates candidates and computes the truth; JEV judges only
  tense (Q-TENSE) or meaning (Q-MEANING); CODE decides. Jev never sees settings.json, never
  counts, never compares dates.
    - Enumeration is the committed Python, tools/checks/lib/doc_scanner_candidates.py, which
      imports the MEASURED instrument tools/checks/measure/doc-scanner/enumerators.py unchanged
      (decision DS-D2). This script owns the gates, the Jev calls and the report.
    - The ONE shared HTTP call site is tools/checks/lib/InvokeJev.ps1. It is dot-sourced below,
      never copied (spec section 5 item 6). It also handles PS 5.1's missing UTF-8 body encoding.

  STEPS (spec section 4.6):
    1. Enumerate at -Rev over the living set (or -Docs). Docs are read with `git show`, never
       from the working tree (DS-D7).
    2. Print the coverage block, then the code-only findings and the candidate list, with NO
       judgments.
    3. Refuse, in this order, before the API key is even read:
         BASELINE_MISSING           no file at -BaselinePath                         (DS-D6)
         BASELINE_REVISION_MISMATCH the file's _revision is not -Rev                 (DS-D7)
         BASELINE_INCOMPLETE        a judged item has no line in the file            (DS-D6)
         BASELINE_INVALID_VALUE     a line uses a word outside the item's vocabulary
       Revision is checked before completeness: at another revision every item id differs,
       and "incomplete" would name the wrong cause.
    4. Jev judges each item $Samples times (5), a fresh sample_uid each time (DS-D5).
    5. Report per item: verdict, agreement rate, mean top probability, baseline, AGREE/DISAGREE.
       A stable row is not a correct row (docs/harness-shadow-mode-protocol.md section 4g), so
       the report never collapses to a pass count.

  WHO WRITES THE BASELINE: for a first measured run, the SEAT, always
  (docs/harness-shadow-mode-protocol.md section 4c). An implementer stops at the refusal.

  BASELINE FILE FORMAT:
    { "_revision": "<sha, 7+ chars>", "judgments": { "<item id>": "<verdict or unsure>", ... } }
    Q-TENSE  vocabulary: asserts_current | history_or_quote | ambiguous | unsure
    Q-MEANING vocabulary: describes_this_fixture | describes_something_else | no_description |
                          ambiguous | unsure

  USAGE (Git Bash; the key is loaded only for a keyed run):
    set -a; . ./typesafe.local.env; set +a
    powershell -NoProfile -File tools/checks/doc-scanner.ps1 [-Rev <rev>] [-Docs <pattern,...>]
        [-BaselinePath <file>] [-OutPath <file>] [-Samples N] [-AllowUnbaselinedItems]
    powershell -NoProfile -File tools/checks/doc-scanner.ps1 -Replay     # spec section 5 item 1

  EXIT CODES (spec section 4.6):
    0  no finding and every row stable
    1  any finding, or any unstable row. Findings: a judged `asserts_current` (Q-TENSE) or
       `describes_something_else` (Q-MEANING); a judged `ambiguous`; a WAF-blocked item (never
       a pass); and the code-only findings CFG_MEMBER_MISSING, LINE_PAST_EOF,
       DATED_STATE_OVER_HORIZON, NEXT_FREE_FAMILY_STALE. The three VALUE never_shipped buckets
       are REPORTED but are not findings: they are the parser-error class (spec DS-D4 and the
       amendment), never a rot claim.
    2  a gate, a suspect enumerator, a missing -Docs pattern, or an API failure.
#>
[CmdletBinding()]
param(
    [string]$Rev = 'HEAD',
    # Patterns matched against tracked paths at -Rev (fnmatch). Comma-separated values are split,
    # because `powershell -File` passes `-Docs a,b` as one string. Empty = the living set.
    [string[]]$Docs = @(),
    [string]$BaselinePath = 'doc-scanner-baseline.json',
    [string]$OutPath = 'doc-scanner-report.md',
    [int]$Samples = -1,
    [switch]$AllowUnbaselinedItems,
    [switch]$Replay,
    [string]$Python = 'python',
    # Prints each call's state KEY NAMES and sample_uid only -- never content, never the key.
    [switch]$DebugState
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$pyScript = Join-Path $PSScriptRoot 'lib\doc_scanner_candidates.py'
$env:PYTHONIOENCODING = 'utf-8'

# DS-D5: 5 samples, agreement rate on every row.
$SELF_CONSISTENCY_SAMPLES = 5
if ($Samples -lt 1) { $Samples = $SELF_CONSISTENCY_SAMPLES }

# The one shared Jev call site. Never re-implemented here.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# ---------------------------------------------------------------------------------------------
# Spec section 5 item 1: -Replay. The tool's own arms (kept kinds), ruled pairing, UTC dating,
# in replay_recall.py's format.
# ---------------------------------------------------------------------------------------------
if ($Replay) {
    & $Python $pyScript replay --kinds kept --e2 ruled --dates utc
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------------------
# Step 1: enumerate (Python). The candidates JSON goes to a temp file, deleted on exit.
# ---------------------------------------------------------------------------------------------
$docPatterns = @($Docs | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
$candFile = [System.IO.Path]::GetTempFileName()
$pyArgs = @('scan', '--rev', $Rev, '--e2', 'ruled', '--dates', 'utc', '--out', $candFile)
if ($docPatterns.Count -gt 0) { $pyArgs += '--docs'; $pyArgs += $docPatterns }
$scanOut = & $Python $pyScript @pyArgs
if ($LASTEXITCODE -ne 0) {
    "EXIT_REASON=ENUMERATOR_FAILED"
    "The Python enumeration step exited $LASTEXITCODE. Output: $scanOut"
    Remove-Item -Force $candFile -ErrorAction SilentlyContinue
    exit 2
}
$scan = Get-Content -Raw -Encoding UTF8 -Path $candFile | ConvertFrom-Json
Remove-Item -Force $candFile -ErrorAction SilentlyContinue

$items = @($scan.items)
$counts = $scan.counts
$livingDocs = @($scan.living_docs)
$scannedDocs = @($scan.docs_scanned)
$jevArms = @('VERSION', 'VALUE', 'POINTER', 'FIXTURE_MEANING')

function Write-Coverage([int]$itemsJudged, [int]$jevCalls, [long]$usageIn, [double]$wallSec) {
    "REV=$($scan.rev7)"
    "LIVING_DOCS=$($livingDocs.Count)  (list: $($livingDocs -join ' '))"
    "DOCS_SCANNED=$($scannedDocs.Count)  source=$($scan.docs_source)  in_living_set=$(@($scan.docs_in_living_set).Count)"
    if ($scan.docs_source -eq 'explicit') { "  scanned: $($scannedDocs -join ' ')" }
    "E2_PAIRING=$($scan.e2_pairing)  DATED_STATE_DATES=$($scan.dated_state_date_source)  LIVE_SETTINGS_VERSION=v$($scan.live_settings_version)"
    "CANDIDATES_VERSION=$($counts.CANDIDATES_VERSION)  CANDIDATES_VALUE=$($counts.CANDIDATES_VALUE)  CANDIDATES_POINTER=$($counts.CANDIDATES_POINTER)  CANDIDATES_FIXTURE_MEANING=$($counts.CANDIDATES_FIXTURE_MEANING)"
    "VALUE_NEVER_SHIPPED_CODE_ONLY=$($counts.VALUE_NEVER_SHIPPED_CODE_ONLY)  VALUE_UNQUALIFIED_NEVER_SHIPPED=$($counts.VALUE_UNQUALIFIED_NEVER_SHIPPED)  VALUE_OPERATOR_NEVER_SHIPPED=$($counts.VALUE_OPERATOR_NEVER_SHIPPED)"
    "CFG_MEMBER_MISSING=$($counts.CFG_MEMBER_MISSING)  LINE_PAST_EOF=$($counts.LINE_PAST_EOF)  DATED_STATE_OVER_HORIZON=$($counts.DATED_STATE_OVER_HORIZON)  NEXT_FREE_FAMILY_STALE=$($counts.NEXT_FREE_FAMILY_STALE)  NEXT_FREE_FAMILY_CLAIMS=$($counts.NEXT_FREE_FAMILY_CLAIMS)"
    "ITEMS_JUDGED=$itemsJudged  JEV_CALLS=$jevCalls  USAGE_INPUT_TOKENS=$usageIn  WALL_TIME_SEC=$([math]::Round($wallSec, 2))"
}

# ---------------------------------------------------------------------------------------------
# Step 2: coverage, then the suspect-enumerator checks (spec section 4.5), then the lists.
# ---------------------------------------------------------------------------------------------
Write-Coverage 0 0 0 0

if (@($scan.docs_unmatched_patterns).Count -gt 0 -or $scannedDocs.Count -eq 0) {
    "EXIT_REASON=DOCS_UNMATCHED"
    "No tracked file at $($scan.rev7) for -Docs pattern(s): $(@($scan.docs_unmatched_patterns) -join ' ')"
    exit 2
}
$jevCandidateTotal = 0
foreach ($a in $jevArms) { $jevCandidateTotal += [int]$counts."CANDIDATES_$a" }
if ($livingDocs.Count -lt [int]$scan.living_expected -or $jevCandidateTotal -eq 0) {
    "EXIT_REASON=ENUMERATOR_SUSPECT"
    "LIVING_DOCS=$($livingDocs.Count) (need $($scan.living_expected); missing: $(@($scan.living_docs_missing) -join ' ')) or all four Jev arms returned zero candidates ($jevCandidateTotal). A zero is the tripwire, never a clean bill (docs/harness-shadow-mode-protocol.md section 5)."
    exit 2
}

$codeOnlyKeys = @('CFG_MEMBER_MISSING', 'LINE_PAST_EOF', 'DATED_STATE_OVER_HORIZON', 'NEXT_FREE_FAMILY',
                  'VALUE_NEVER_SHIPPED', 'VALUE_UNQUALIFIED_NEVER_SHIPPED', 'VALUE_OPERATOR_NEVER_SHIPPED')
"CODE_ONLY (computed by code; never sent to Jev):"
foreach ($k in $codeOnlyKeys) {
    foreach ($x in @($scan.code_only.$k)) {
        if ($null -eq $x) { continue }
        if ($k -eq 'NEXT_FREE_FAMILY') {
            "  $($x.id) claimed=A$($x.claimed) highest=A$($x.highest) stale=$($x.stale)"
        } else {
            "  $($x.id) $($x.detail)"
        }
    }
}

function Get-ItemFacts($it) {
    switch ($it.arm) {
        'VERSION' { return "doc=v$($it.doc_version) live=v$($it.live_version)" }
        'VALUE' {
            $lab = @()
            if ($it.qualified -eq $false) { $lab += 'unqualified' }
            if ($it.operator_context) { $lab += 'operator_context' }
            return "key=$($it.key_path) doc=$($it.doc_value_text) live=$($it.live_value) last_shipped=v$($it.last_shipped_version) rule=$($it.pair_rule) labels=[$($lab -join ',')]"
        }
        'POINTER' { return "points=$($it.pointed) newest=$($it.newest)" }
        'FIXTURE_MEANING' { return "fixture=$($it.fixture_id) subs=[$(@($it.sub_names) -join ',')] checks=$(@($it.check_titles).Count)" }
    }
    return ''
}

"CANDIDATES (no judgments, for baseline labelling):"
foreach ($it in $items) { "  $($it.id) [$($it.question)] $(Get-ItemFacts $it)" }

# ---------------------------------------------------------------------------------------------
# Step 3: the refusals (DS-D6, DS-D7). All before the API key is read.
# ---------------------------------------------------------------------------------------------
$vocab = @{
    'Q-TENSE'   = @('asserts_current', 'history_or_quote', 'ambiguous', 'unsure')
    'Q-MEANING' = @('describes_this_fixture', 'describes_something_else', 'no_description', 'ambiguous', 'unsure')
}
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Nothing was judged and no API call was made. Write your OWN read of every CANDIDATES item above first, as:"
    '  { "_revision": "' + $scan.rev7 + '", "judgments": { "<item id>": "<verdict or unsure>" } }'
    "Q-TENSE: $($vocab['Q-TENSE'] -join ' | ').  Q-MEANING: $($vocab['Q-MEANING'] -join ' | ')."
    "A first measured run's baseline is the SEAT's, always (docs/harness-shadow-mode-protocol.md section 4c)."
    exit 2
}
$baselineRaw = Get-Content -Raw -Encoding UTF8 -Path $baselineFull | ConvertFrom-Json
$baseRev = [string]$baselineRaw._revision
if ([string]::IsNullOrWhiteSpace($baseRev) -or $baseRev.Length -lt 7 -or -not $scan.rev.StartsWith($baseRev)) {
    "EXIT_REASON=BASELINE_REVISION_MISMATCH"
    "The baseline's _revision is '$baseRev'; this run is at $($scan.rev). Item ids carry their revision, so the two cannot align (DS-D7). Nothing was judged."
    exit 2
}
$baseline = @{}
if ($null -ne $baselineRaw.judgments) {
    foreach ($p in $baselineRaw.judgments.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}
$unbaselined = @($items | Where-Object { -not $baseline.ContainsKey($_.id) } | ForEach-Object { $_.id })
if ($unbaselined.Count -gt 0 -and -not $AllowUnbaselinedItems) {
    "EXIT_REASON=BASELINE_INCOMPLETE"
    "UNBASELINED_ITEMS=$($unbaselined.Count) -- no line in '$BaselinePath' for:"
    foreach ($u in $unbaselined) { "  $u" }
    "Nothing was judged. An unbaselined item judged once is spent for ever. -AllowUnbaselinedItems is for a routine re-run of items already measured."
    exit 2
}
$invalid = @($items | Where-Object { $baseline.ContainsKey($_.id) -and ($vocab[$_.question] -notcontains $baseline[$_.id]) } |
             ForEach-Object { "$($_.id) = '$($baseline[$_.id])' (allowed: $($vocab[$_.question] -join ' | '))" })
if ($invalid.Count -gt 0) {
    "EXIT_REASON=BASELINE_INVALID_VALUE"
    foreach ($v in $invalid) { "  $v" }
    exit 2
}

$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set. Load it first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

# ---------------------------------------------------------------------------------------------
# Step 4: the two questions (spec section 4.3). `verdict` is the ONLY answer defined and read.
# ---------------------------------------------------------------------------------------------
$tenseCriteria = [ordered]@{
    asserts_current  = '`line` states the value or pointer named in `claim` as the one in force now.'
    history_or_quote = '`line` narrates a past state, quotes an old value (for example in a fix note, a change record or a correction), or explains a correction.'
    ambiguous        = 'The text does not settle whether `line` states the value or pointer as current or as history.'
}
$meaningCriteria = [ordered]@{
    describes_this_fixture   = '`line` describes fixture `fixture_id` in a way consistent with its `sub_names` and `check_titles`.'
    describes_something_else = '`line` describes fixture `fixture_id` as testing something that its `sub_names` and `check_titles` show it does not test.'
    no_description           = '`line` names `fixture_id` without saying what it tests -- a bare reference or a list of ids.'
    ambiguous                = 'The text does not settle what `line` says fixture `fixture_id` tests.'
}

function Invoke-ItemVerdict([string]$key, $it, [string]$uid) {
    if ($it.question -eq 'Q-TENSE') {
        $state = [ordered]@{
            doc_path = $it.path; heading_chain = $it.heading_chain; line = $it.line_text
            lines_before = $it.lines_before; lines_after = $it.lines_after; claim = $it.claim
        }
        $q = @{ type = 'choice'; criteria = $tenseCriteria
                instructions = 'Code has already checked the facts in `claim`, and they are correct: do not recompute them. Decide only the TENSE of `line`: does it state the value or pointer named in `claim` as the one in force NOW, or does it narrate history, quote an old value, or explain a correction? Use `heading_chain`, `lines_before` and `lines_after` as context.' }
    } else {
        $state = [ordered]@{
            line = $it.line_text; lines_before = $it.lines_before; lines_after = $it.lines_after
            fixture_id = $it.fixture_id; sub_names = @($it.sub_names); check_titles = @($it.check_titles)
        }
        $q = @{ type = 'choice'; criteria = $meaningCriteria
                instructions = 'Code found fixture id `fixture_id` in `line` and looked it up in the fixture file: its Sub name is in `sub_names` and its assertion titles are in `check_titles`. Decide whether `line` describes that fixture correctly.' }
    }
    $state['sample_uid'] = $uid
    if ($DebugState) { Write-Host "STATE_DEBUG $($it.id) keys=[$($state.Keys -join ',')] sample_uid=$uid" }
    $call = Invoke-Jev $key @{ model = 'jev-latest'; state = $state; questions = @{ verdict = $q } }
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked } }
    $v = $call.Response.answers.verdict   # THE one read of an answer. Code decides from $v only.
    $top = $null
    if ($v.probabilities) { $top = (@($v.probabilities.PSObject.Properties | ForEach-Object { [double]$_.Value }) | Measure-Object -Maximum).Maximum }
    $inTok = 0
    if ($call.Response.usage -and $call.Response.usage.input_tokens) { $inTok = [long]$call.Response.usage.input_tokens }
    return @{ Ok = $true; Verdict = [string]$v.choice; TopProbability = $top; InputTokens = $inTok }
}

$findingVerdicts = @('asserts_current', 'describes_something_else')
$results = New-Object System.Collections.Generic.List[object]
$jevCalls = 0
$usageIn = [long]0
$sw = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($it in $items) {
    $draws = New-Object System.Collections.Generic.List[object]
    $blocked = $false
    for ($i = 0; $i -lt $Samples; $i++) {
        $uid = "$($it.id):$i`:$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $r = Invoke-ItemVerdict $apiKey $it $uid
        $jevCalls++
        if (-not $r.Ok) {
            if ($r.WafBlocked) { $blocked = $true; break }
            $sw.Stop()
            Write-Coverage $results.Count $jevCalls $usageIn $sw.Elapsed.TotalSeconds
            "EXIT_REASON=API_FAILED"
            "Jev request failed for $($it.id) (sample $($i + 1)/$Samples): $($r.Error)"
            exit 2
        }
        $usageIn += $r.InputTokens
        $draws.Add($r)
    }
    $base = if ($baseline.ContainsKey($it.id)) { $baseline[$it.id] } else { $null }
    if ($blocked) {
        $results.Add([PSCustomObject]@{ Id = $it.id; Question = $it.question; Verdict = 'WAF_BLOCKED'; Agreement = $null
            Stable = $true; MeanTop = $null; Draws = ''; Baseline = $base })
        continue
    }
    $tally = @{}
    foreach ($d in $draws) { if (-not $tally.ContainsKey($d.Verdict)) { $tally[$d.Verdict] = 0 }; $tally[$d.Verdict]++ }
    $plural = $null; $pc = -1
    foreach ($d in $draws) { if ($tally[$d.Verdict] -gt $pc) { $pc = $tally[$d.Verdict]; $plural = $d.Verdict } }
    $agree = [math]::Round($pc / $draws.Count, 3)
    $tops = @($draws | Where-Object { $null -ne $_.TopProbability } | ForEach-Object { $_.TopProbability })
    $meanTop = if ($tops.Count -gt 0) { [math]::Round(($tops | Measure-Object -Average).Average, 3) } else { $null }
    $results.Add([PSCustomObject]@{ Id = $it.id; Question = $it.question; Verdict = $plural; Agreement = $agree
        Stable = ($agree -ge 1.0); MeanTop = $meanTop; Draws = (@($draws | ForEach-Object { $_.Verdict }) -join ','); Baseline = $base })
}
$sw.Stop()

# ---------------------------------------------------------------------------------------------
# Step 5: report. Per item, never collapsed to a pass count (DS-D5).
# ---------------------------------------------------------------------------------------------
function Get-Agreement($res) {
    if ($res.Verdict -eq 'WAF_BLOCKED') { return 'NOT_JUDGED' }
    if ($null -eq $res.Baseline) { return 'NO_BASELINE_VALUE' }
    if ($res.Baseline -eq 'unsure') { return 'OPERATOR_UNSURE' }
    if ($res.Baseline -eq $res.Verdict) { return 'AGREE' }
    return 'DISAGREE'
}
Write-Coverage $results.Count $jevCalls $usageIn $sw.Elapsed.TotalSeconds
"RESULTS:"
foreach ($res in $results) {
    $st = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
    "  $($res.Id) [$st] verdict=$($res.Verdict) agreement_rate=$($res.Agreement) mean_top_prob=$($res.MeanTop) baseline=$($res.Baseline) [$(Get-Agreement $res)] draws=[$($res.Draws)]"
}
$unstable = @($results | Where-Object { -not $_.Stable }).Count
$judgedFindings = @($results | Where-Object { $findingVerdicts -contains $_.Verdict }).Count
$ambiguous = @($results | Where-Object { $_.Verdict -eq 'ambiguous' }).Count
$waf = @($results | Where-Object { $_.Verdict -eq 'WAF_BLOCKED' }).Count
$agreeN = @($results | Where-Object { (Get-Agreement $_) -eq 'AGREE' }).Count
$disagreeN = @($results | Where-Object { (Get-Agreement $_) -eq 'DISAGREE' }).Count
$codeFindings = [int]$counts.CFG_MEMBER_MISSING + [int]$counts.LINE_PAST_EOF + [int]$counts.DATED_STATE_OVER_HORIZON + [int]$counts.NEXT_FREE_FAMILY_STALE
"JUDGED_FINDINGS=$judgedFindings  AMBIGUOUS=$ambiguous  UNSTABLE=$unstable  WAF_BLOCKED=$waf  CODE_ONLY_FINDINGS=$codeFindings"
"BASELINE_AGREE=$agreeN  BASELINE_DISAGREE=$disagreeN  (a stable row is not a correct row -- docs/harness-shadow-mode-protocol.md section 4g)"

$rl = New-Object System.Collections.Generic.List[string]
$rl.Add('# Doc-scanner report')
$rl.Add('')
$rl.Add("Generated by ``tools/checks/doc-scanner.ps1`` at revision ``$($scan.rev7)``, source ``$($scan.docs_source)``, ``-Samples $Samples``.")
$rl.Add('')
$rl.Add('## Coverage')
$rl.Add('')
$rl.Add('```')
foreach ($l in (Write-Coverage $results.Count $jevCalls $usageIn $sw.Elapsed.TotalSeconds)) { $rl.Add($l) }
$rl.Add("JUDGED_FINDINGS=$judgedFindings  AMBIGUOUS=$ambiguous  UNSTABLE=$unstable  WAF_BLOCKED=$waf  CODE_ONLY_FINDINGS=$codeFindings")
$rl.Add("BASELINE_AGREE=$agreeN  BASELINE_DISAGREE=$disagreeN")
$rl.Add('```')
$rl.Add('')
$rl.Add('## Per item')
$rl.Add('')
# A list, not a table: item ids contain `|`, which splits a markdown table cell even inside backticks.
foreach ($res in $results) {
    $st = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
    $rl.Add("- ``$($res.Id)`` -- $st, verdict ``$($res.Verdict)``, agreement $($res.Agreement), mean top prob $($res.MeanTop), baseline ``$($res.Baseline)``, **$(Get-Agreement $res)**, draws ``$($res.Draws)``")
}
Set-Content -Encoding UTF8 -Path (Resolve-RepoPath $OutPath) -Value ($rl -join "`r`n")
"Report written to $OutPath"

if ($judgedFindings -gt 0 -or $ambiguous -gt 0 -or $unstable -gt 0 -or $waf -gt 0 -or $codeFindings -gt 0) { exit 1 }
exit 0
