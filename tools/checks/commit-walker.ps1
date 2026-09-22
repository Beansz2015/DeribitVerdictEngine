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
  COUNTS ONLY -- never judged). The remaining residual is genuinely ambiguous either
  way and is the ONLY population sent to Jev (TypeSafe's System One model). Judging the
  whole window would burn tokens on cases the path rule already settles and would
  contaminate the measurement of the harness itself.

  ============================== REVISION 1 (2026-09-22) ==============================
  docs/commit-walker-check-spec.md section 9, after the first (n=3) run. Supersedes the
  old two-Noul question set (section 6). Full record: section 9.2 -- the tag turned out
  to be INCONSISTENT AT SOURCE (two same-shape commits, one tagged, one not), so "is the
  tag correct" presumes a convention that does not exist. The harness's job changed: ask
  Jev to classify a commit's ACTUAL effect on the shipped app, independent of the tag,
  and let CODE compare that classification to the tag. A disagreement is reported for
  the seat to adjudicate, not auto-resolved.

  Five things changed here (section 9.3 / 9.4):
    1. Question set replaced: three narrow Nouls (changes_computation, changes_display,
       changes_writes) plus a five-way verdict Choice (no_app_change, changes_computation,
       changes_display, changes_writes, ambiguous). The old changes_runtime_behaviour /
       is_refactor_only pair is gone -- it was too broad and produced 7 tag_wrong
       verdicts on a 10-commit residual that mostly weren't wrong.
    2. CODE compares verdict to tag, not the model. `declared_no_engine_change` is
       therefore no longer sent to Jev at all -- the model classifies the commit's
       effect blind to what it was tagged, and the comparison happens after the call
       returns. This keeps the model's read independent of the claim being audited.
    3. Era guard: a commit dated before the tag's adoption is excluded into its own
       PRE_ERA_EXCLUDED class, never the residual. See the ERA GUARD comment block below
       for why the cutoff is DERIVED at runtime rather than a hardcoded date.
    4. The commit BODY travels alongside the subject (state.commit.body).
    5. Low-confidence escalation: below $LOW_CONFIDENCE_ESCALATION_THRESHOLD, a second
       request is sent carrying the diff of the commit's shipped-app files only.
    Plus a -Repeat N determinism probe (section 9.4 item 4): ask the same commit's
    primary question N times and report whether verdict/confidence moved.
  =======================================================================================

  ============================== REVISION 2 (2026-09-22) ==============================
  docs/commit-walker-check-spec.md section 10, written after
  docs/harness-shadow-mode-protocol.md section 4b measured the detector FLIP a verdict
  on IDENTICAL input: c6c6942d8a's three -Repeat calls returned changes_writes /
  no_app_change / changes_writes, confidences 0.32 / 0.32 / 0.27. A single call's verdict
  is not reproducible near the threshold. Self-consistency
  (docs.typesafe.ai/cookbooks/consistency_choice_cookbook.md) is the documented fix: ask
  each residual commit's primary question $SELF_CONSISTENCY_SAMPLES times (default 5, a
  named constant beside the others below), with a FRESH `uid` per sample in the STATE
  (never the questions) so each sample is an independent draw, and use label agreement
  across the samples as the signal.

  -Repeat is RETIRED as a separate flag -- self-consistency is now the default path and
  -Repeat was its prototype (section 10.3 item 6). -Samples N is the override, still
  defaulting to $SELF_CONSISTENCY_SAMPLES.

  Every residual row now ALWAYS reports (section 10.3 item 3): the plurality verdict,
  the AGREEMENT RATE (samples on the plurality / N -- on every row, not only unstable
  ones), and the mean and min TOP PROBABILITY (max(probabilities) on the `verdict`
  Choice answer, per sample, per section 10.1 detail 2).

  ⛔⛔ THE TRAP THIS REVISION IS NAMED FOR (section 10.1 detail 2): the uncertainty band
  ($MIN_TOP_PROBABILITY = 0.60) is on that TOP PROBABILITY -- a different statistic on a
  different scale from `confidence` (docs.typesafe.ai/confidence.md). Revision 1's
  $LOW_CONFIDENCE_ESCALATION_THRESHOLD was a CONFIDENCE threshold and is RETIRED below,
  not reused or renamed into the new band.

  Section 10.2's decision matrix replaces revision 1's confidence-only escalation trigger:
    - N samples AGREE, top probability HIGH -> confident and stable. Report the verdict.
    - N samples AGREE, top probability LOW  -> stable but under-informed -> ESCALATE
      (same diff-carrying second call as revision 1's cascade, engine-touched files only).
    - N samples DISAGREE                    -> genuinely on the fence -> report UNSTABLE.
      NEVER escalated, NEVER auto-resolved, whatever the plurality says (section 10.3
      item 7). Escalation is structurally unreachable from a disagreeing row: see the
      `if (-not $stable) { ... } elseif (...) { escalate }` shape in the main loop below
      -- there is no path from the first branch into the second.
  Auto-proceeded design call, recorded here per CLAUDE.md's auto-proceed obligation: the
  spec asks for "mean and min top probability" to be REPORTED on every row but does not
  say which statistic the escalation decision itself reads. This build uses the MEAN
  top probability across the N agreeing samples for that decision (min is reported
  alongside as a diagnostic, never as a second gate) -- consistent with "agree" already
  meaning every sample landed on the same label, so mean is the representative read of
  how informed that stable answer was. A stricter min-based gate was the richer/more
  conservative alternative; not taken because the spec's own matrix names a single
  condition ("top probability low"), not a two-statistic AND, and this build declined to
  invent a second implicit band the spec does not ask for.
  =======================================================================================

  ============================== REVISION 2 CORRECTION (2026-09-22) ==============================
  docs/commit-walker-check-spec.md sections 10.7-10.9, found empirically by THIS build on
  91942d6739: revision 2's escalation call was itself a SINGLE, unsampled call -- exactly
  the single-call instability the self-consistency design exists to remove. 5 of 5 primary
  samples agreed on changes_computation (matching the hand baseline); the mean top
  probability was 0.414, under the band, so it escalated; the one unsampled escalation call
  returned no_app_change and SILENTLY REPLACED the unanimous plurality (the old
  `$finalVerdict = $escResult.Verdict` line). A system built on sampling must not exempt
  its own tie-breaker, and on the one case observed the unsampled call moved the answer
  AWAY from the baseline the sampled primary matched -- so escalated is not assumed better-
  informed.

  Two changes, both required (section 10.7):
    1. The escalation call now runs in a LOOP of $ESCALATION_SAMPLES (named constant,
       default 3, beside $SELF_CONSISTENCY_SAMPLES and $MIN_TOP_PROBABILITY below), with a
       fresh `uid` per escalation sample same as the primary. Its plurality, agreement
       rate, and mean/min top probability are computed by the SAME function the primary
       uses (Get-SelfConsistencyAggregate) -- not a second copy of the same arithmetic that
       can drift from the first.
    2. A new CONFLICT status: when the escalated plurality disagrees with the primary
       plurality, the tool reports BOTH verdicts, BOTH agreement rates, and BOTH
       probability ranges, and exits non-zero. It does NOT pick a winner -- see the
       `-ne $pluralityVerdict` comparison marked CONFLICT below.
    CONFIDENT_AFTER_ESCALATION now means escalated AND the escalated plurality AGREED with
    the primary plurality. Escalation still cannot fire on an UNSTABLE row -- unchanged;
    same `if (-not $stable) { ... } elseif (...) { escalate }` shape as revision 2.
  ===================================================================================================

  THE TRAPS THAT STILL APPLY (docs/commit-walker-check-spec.md section 0, unchanged by
  either revision):
    1. Merge commits return an EMPTY file list from `git show --name-only`, so they look
       like "touched no engine path" and pollute the residual. Filtered at the `git log`
       step with --no-merges, never only at a later `show` step.
    2. Composing the Noul diagnostics with AND. MEASURED elsewhere in this programme to
       score 0 of 8 against a single Choice's 4 of 8. The verdict below is read from the
       `verdict` Choice answer ALONE -- see the line marked TRAP 2.
    3. Sending diff bodies as state for the 93% the path rule already settles. Only the
       residual gets a Jev call at all; only a LOW-TOP-PROBABILITY residual commit gets a
       diff, and only for its shipped-app files (item 5 above / revision 2's matrix).
    4. Judging all commits instead of the residual. Jev sees ONLY the residual class
       (RESIDUAL_TAGGED_BUT_ENGINE_PATH + RESIDUAL_UNTAGGED_NO_ENGINE_PATH), and now
       excludes PRE_ERA_EXCLUDED first.

  ENGINE PATHS, per docs/commit-walker-check-spec.md section 2: a root-level `*.vb`, or
  anything under `Core/`, `analysis/`, `UI/`, or `settings.json` itself. `tools/` is
  DELIBERATELY not an engine path -- those are offline, host-agnostic utilities.
  Re-measured section 9.1 against 339 post-adoption commits: the rule as written agrees
  with the tag 94% of the time and is the best fit of every candidate tested (adding
  `tools/` drops agreement to 85%). It stands unchanged.

  USAGE:
    set -a; . ./typesafe.local.env; set +a   # loads TYPESAFE_API_KEY (bash)
    powershell -NoProfile -File tools/checks/commit-walker.ps1 `
      -Count 300 -BaselinePath <path to your own pre-written read, see the shadow-mode
      protocol at docs/harness-shadow-mode-protocol.md section 2 step 3> `
      [-Skip N] [-Samples N]

  EXIT CODES:
    0 - every residual commit judged CONSISTENT or N/A_UNTAGGED, no `ambiguous` verdict,
        and no UNSTABLE row (self-consistency agreement rate below 1.0)
    1 - at least one residual commit is a DISAGREEMENT (tagged [no-engine-change], but
        verdict != no_app_change), judged `ambiguous`, UNSTABLE, or CONFLICT. Revision 2:
        an unresolved self-consistency disagreement is reported to the seat and never
        auto-resolved (section 10.3 item 7), so that non-resolution must not exit 0.
        Revision 2 correction (section 10.7): an escalated plurality that disagrees with
        the primary plurality is CONFLICT, reported to the seat with neither side picked
        as the winner, and must not exit 0 either.
    2 - baseline missing, classifier suspect, all-pre-era window, residual over 25%, or
        API failure
    (Restated from the original section 4.5 for the new five-way verdict vocabulary --
    the old vocabulary was tag_correct/tag_wrong, which no longer exists. This mapping is
    an interpretation of the same intent, not literally specified in section 9; see the
    build's own report for that call.)
#>
[CmdletBinding()]
param(
    [int]$Count = 300,
    # Commits to skip before walking, so an ACCEPTANCE window and a MEASURED first-run
    # window can be made disjoint. A build's acceptance dry run reports verdicts back and
    # contaminates the seat on that window; the measured run must use another one.
    # See docs/harness-shadow-mode-protocol.md section 4a.
    [int]$Skip = 0,
    [string]$BaselinePath = 'commit-walker-baseline.json',
    [string]$OutPath = 'commit-walker-report.md',
    # REVISION 2, section 10.3 items 1 and 6: self-consistency sample count, replacing the
    # retired -Repeat. -1 is a sentinel meaning "use the named constant
    # $SELF_CONSISTENCY_SAMPLES below" -- kept out of the param default itself because the
    # constant is defined beside its siblings further down, not buried in the signature.
    [int]$Samples = -1,
    # Acceptance-only instrumentation for section 10.6 item 2 ("show two sample states,
    # key names and the differing uid only"): prints each call's state KEY NAMES and its
    # sample_uid to the host as the calls happen. Off by default -- adds console noise on
    # every call otherwise. Never prints subject/body/diff content or the API key.
    [switch]$DebugState,
    # docs/harness-shadow-mode-protocol.md section 4a: an acceptance dry run over a
    # RESERVED window (section 10.6 item 6 spends -Skip 0 -Count 300) contaminates the
    # seat on that window if per-commit verdicts or any aggregate are ever seen, even
    # relayed second-hand in a report. Rather than trust discipline after the fact, this
    # switch makes the harness itself withhold that content: PER_COMMIT_RESULTS,
    # SELF_CONSISTENCY, DISAGREEMENTS/UNSTABLE counts and their *_LIST blocks are all
    # suppressed from console AND from the written report. Coverage, token usage and wall
    # time still print -- section 10.6 item 6 asks for exactly those and nothing else.
    [switch]$CountersOnly
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# ---------------------------------------------------------------------------------------
# REVISION 2 constants (section 10.3 items 1 and 4): NAMED, not buried, beside the
# REVISION 1 constant that survives ($MAX_ESCALATION_DIFF_CHARS). Both are guesses that
# will be re-tuned once a real population exists.
# ---------------------------------------------------------------------------------------

# How many times each residual commit's primary question is sampled. Default 5 -- the
# cookbook (docs.typesafe.ai/cookbooks/consistency_choice_cookbook.md) uses 15; 5 is this
# repo's starting point (section 10.3 item 1, section 10.4's cost estimate is built on 5).
$SELF_CONSISTENCY_SAMPLES = 5
if ($Samples -lt 1) { $Samples = $SELF_CONSISTENCY_SAMPLES }

# The uncertainty band on the TOP PROBABILITY -- max(probabilities) on a single sample's
# `verdict` Choice answer -- NOT on `confidence`. Section 10.1 detail 2: these are
# different statistics on different scales; a `confidence` of 0.3 is roughly a top
# probability of 0.44 for a five-option Choice. See "THE BAND" below for the one line
# this threshold is applied on.
$MIN_TOP_PROBABILITY = 0.60

# REVISION 2 CORRECTION (section 10.7 item 1): how many times the escalation call itself
# is sampled, once it fires. Escalation measures ~8,090 tokens against a primary's ~1,605
# (section 10.8), so 3 samples of it is roughly $0.001 per escalated commit -- "not a
# reason to skip it" per the spec. Named beside its siblings, not buried.
$ESCALATION_SAMPLES = 3

# RETIRED, REVISION 2 (section 10.1 detail 2 / section 10.3 item 5): revision 1's
# $LOW_CONFIDENCE_ESCALATION_THRESHOLD = 0.3 was a CONFIDENCE threshold, a different
# statistic on a different scale from $MIN_TOP_PROBABILITY above. It is deliberately NOT
# defined here and MUST NOT be reused or renamed into the probability band -- the
# confidence-only escalation trigger it drove is deleted, replaced by the section 10.2
# matrix (agree-but-low-probability only; never on a disagreeing row).

# Defensive cap on the escalation diff's size, in characters. Not specced -- added because
# a single commit's engine-path diff can run to hundreds of lines (measured: c6c6942d8a's
# Core/ diff alone is 300 lines) and the brief's own escalation trigger is "any request
# nears the 32k state limit". Truncated, not refused, so escalation still adds SOME signal
# on an oversized commit rather than silently downgrading to the un-escalated read.
# KEPT per section 10.5's ruling ("sensible, and it was right to add it unspecced").
$MAX_ESCALATION_DIFF_CHARS = 20000

# ---------------------------------------------------------------------------------------
# ERA GUARD (section 9.4 item 1). The spec text names "2026-08-13" as the tag's adoption
# date. That figure does not hold under conversion: c6c6942d8a -- the measured first
# tagged commit -- has committer date 2026-08-13T01:21:40+08:00, which is
# 2026-08-12T17:21:40Z. Its UTC calendar date is Aug 12, not Aug 13 -- the spec's date was
# read off the commit's LOCAL +08:00 stamp without converting, the same GMT+8 slip this
# project's own session history has hit repeatedly. A hardcoded "corrected" date would
# just be a second magic constant that can go stale the same way (rebase, a future commit
# closer to the true boundary, etc.), so the cutoff below is DERIVED at run time: the
# earliest commit in the whole history that actually carries the tag, and ITS OWN UTC
# instant is the inclusive boundary. Printed every run as ERA_CUTOFF_COMMIT /
# ERA_CUTOFF_UTC so this claim is checkable without re-deriving it.
# ---------------------------------------------------------------------------------------
function Get-EraCutoffUtc([string]$repoPath) {
    $scan = [string[]](& git -C $repoPath log --no-merges --reverse --format="%H%x09%cI%x09%s")
    foreach ($line in $scan) {
        $parts = $line -split "`t", 3
        if ($parts.Length -lt 3) { continue }
        if ($parts[2].Contains('[no-engine-change]')) {
            return [PSCustomObject]@{
                Hash = $parts[0]
                Utc  = [DateTimeOffset]::Parse($parts[1]).UtcDateTime
            }
        }
    }
    return $null
}

# Invoke-Jev is shared with tools/checks/rider-travel.ps1 (docs/commit-walker-check-spec.md
# section 6: "Reuse its Invoke-Jev verbatim -- do not write a second HTTP call"). Do not
# re-implement it here -- see tools/checks/lib/InvokeJev.ps1's header comment for why the
# manual UTF-8 body encoding is load-bearing, not cosmetic. It matters MORE with this
# revision than before: commit BODIES now travel too, and bodies in this repo carry far
# more em-dashes and section marks than subjects alone.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# Engine-path rule, docs/commit-walker-check-spec.md section 2 (re-measured section 9.1,
# stands unchanged). Git always reports paths with forward slashes, on every OS, so these
# patterns need no separator translation.
function Test-EngineTouch([string[]]$paths) {
    foreach ($p in $paths) {
        if ($p -eq 'settings.json') { return $true }
        if ($p -match '^(Core|analysis|UI)/') { return $true }
        if ($p -match '^[^/]+\.vb$') { return $true }
    }
    return $false
}

# Same rule, returning the MATCHING paths instead of a bool -- used to scope the
# low-confidence escalation diff to shipped-app files only (never tools/, never verify/).
function Get-EngineTouchedPaths([string[]]$paths) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($p in $paths) {
        if ($p -eq 'settings.json') { $out.Add($p); continue }
        if ($p -match '^(Core|analysis|UI)/') { $out.Add($p); continue }
        if ($p -match '^[^/]+\.vb$') { $out.Add($p); continue }
    }
    return $out.ToArray()
}

function Get-ShippedAppDiff([string]$repoPath, [string]$hash, [string[]]$paths, [int]$maxChars) {
    $out = [string[]](& git -C $repoPath show --format="" $hash -- @paths 2>$null)
    $joined = ($out -join "`n")
    if ($joined.Length -gt $maxChars) {
        $joined = $joined.Substring(0, $maxChars) + "`n... [diff truncated at $maxChars chars -- engine-path files only, see touched_paths for the full list]"
    }
    return $joined
}

# ---------------------------------------------------------------------------------------
# Step 1 (section 4.2): walk the log with --no-merges AT THE LOG STEP (trap 1) -- a merge
# commit must never reach the classifier, not merely be filtered out again downstream.
# --no-renames keeps numstat output as plain add/delete pairs per path, never the
# "{old => new}" compact rename form, which is simpler and safer to classify against.
#
# REVISION 1: the format now also carries %cI (strict ISO committer date, for the era
# guard) and the commit BODY (%b), delimited by sentinel lines rather than relying on
# tab/blank-line heuristics, because a body can itself contain blank lines and arbitrary
# text that must not be mistaken for numstat rows or a new commit.
# ---------------------------------------------------------------------------------------
$skipArgs = @()
if ($Skip -gt 0) { $skipArgs = @("--skip=$Skip") }
$raw = [string[]](& git -C $repo log --no-merges --no-renames @skipArgs -$Count --numstat --format="COMMITSTART`t%H`t%cI`t%s%n@@CW_BODY@@%n%b%n@@CW_BODYEND@@")

$commits = New-Object System.Collections.Generic.List[object]
$cur = $null
$inBody = $false
$bodyLines = New-Object System.Collections.Generic.List[string]
foreach ($line in $raw) {
    if ($line.StartsWith("COMMITSTART`t")) {
        if ($null -ne $cur) { $commits.Add($cur) }
        $parts = $line -split "`t"
        # parts: [0]=COMMITSTART [1]=hash [2]=commit-date-iso [3..]=subject (subject may
        # itself contain a literal tab in a pathological case, so re-join the remainder).
        $subject = if ($parts.Length -gt 4) { ($parts[3..($parts.Length - 1)] -join "`t") } else { $parts[3] }
        $cur = [PSCustomObject]@{
            Hash       = $parts[1]
            DateIso    = $parts[2]
            Subject    = $subject
            Body       = ''
            Paths      = New-Object System.Collections.Generic.List[string]
            LineCounts = New-Object System.Collections.Generic.List[object]
        }
        $inBody = $false
        continue
    }
    if ($line -eq '@@CW_BODY@@') {
        $inBody = $true
        $bodyLines = New-Object System.Collections.Generic.List[string]
        continue
    }
    if ($line -eq '@@CW_BODYEND@@') {
        $inBody = $false
        if ($null -ne $cur) { $cur.Body = ($bodyLines -join "`n").Trim() }
        continue
    }
    if ($inBody) { $bodyLines.Add($line); continue }
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

$eraCutoff = Get-EraCutoffUtc $repo
"ERA_CUTOFF_COMMIT=$(if ($eraCutoff) { $eraCutoff.Hash.Substring(0,10) } else { 'NONE_FOUND' })"
"ERA_CUTOFF_UTC=$(if ($eraCutoff) { $eraCutoff.Utc.ToString('yyyy-MM-ddTHH:mm:ssZ') } else { 'N/A' })"

# ---------------------------------------------------------------------------------------
# Step 2 (section 4.2): classify each non-merge, post-era commit against the path rule.
# A pre-era commit is excluded here, BEFORE the tag/path comparison, into its own class --
# never into either AGREE_* bucket and never into the residual (section 9.4 item 1).
# ---------------------------------------------------------------------------------------
$preEra = New-Object System.Collections.Generic.List[object]
$agreeTagged   = New-Object System.Collections.Generic.List[object]
$agreeUntagged = New-Object System.Collections.Generic.List[object]
$residualTaggedButEngine   = New-Object System.Collections.Generic.List[object]
$residualUntaggedNoEngine  = New-Object System.Collections.Generic.List[object]

foreach ($c in $commits) {
    $commitUtc = [DateTimeOffset]::Parse($c.DateIso).UtcDateTime
    $isPreEra = ($null -ne $eraCutoff) -and ($commitUtc -lt $eraCutoff.Utc)
    $c | Add-Member -NotePropertyName IsPreEra -NotePropertyValue $isPreEra
    if ($isPreEra) { $preEra.Add($c); continue }

    $tagged = $c.Subject.Contains('[no-engine-change]')
    $touchedEngine = Test-EngineTouch $c.Paths
    $c | Add-Member -NotePropertyName Tagged -NotePropertyValue $tagged
    $c | Add-Member -NotePropertyName TouchedEngine -NotePropertyValue $touchedEngine

    if ($tagged -and -not $touchedEngine)      { $agreeTagged.Add($c) }
    elseif ((-not $tagged) -and $touchedEngine) { $agreeUntagged.Add($c) }
    elseif ($tagged -and $touchedEngine)        { $residualTaggedButEngine.Add($c) }
    else                                        { $residualUntaggedNoEngine.Add($c) }
}

$classifiedTotal = $commitsWalked - $preEra.Count
$residualTotal = $residualTaggedButEngine.Count + $residualUntaggedNoEngine.Count
$residualPct = if ($classifiedTotal -gt 0) { [math]::Round(($residualTotal / $classifiedTotal) * 100, 1) } else { 0 }

function Write-Coverage([int]$walked, [int]$merges, [int]$preEraN, [int]$agreeT, [int]$agreeU, [int]$resT, [int]$resU, [int]$resTotal, $resPct, [int]$judged) {
    "COMMITS_WALKED=$walked"
    "MERGES_EXCLUDED=$merges"
    "PRE_ERA_EXCLUDED=$preEraN"
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
# PRE_ERA_EXCLUDED is a SUBSET of COMMITS_WALKED (unlike MERGES_EXCLUDED, which never
# reaches $commits at all -- pre-era commits DO get walked, then routed out here).
# ---------------------------------------------------------------------------------------
Write-Coverage $commitsWalked $mergesExcluded $preEra.Count $agreeTagged.Count $agreeUntagged.Count `
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

if ($preEra.Count -gt 0) {
    "PRE_ERA_EXCLUDED_COMMITS (reported, not classified -- predate ERA_CUTOFF_COMMIT):"
    foreach ($c in $preEra) {
        "  $($c.Hash.Substring(0,10)) $($c.Subject)"
    }
}

# All-pre-era window: nothing to classify. Distinct from CLASSIFIER_SUSPECT below --
# this is an expected outcome of a window chosen entirely before tag adoption, not a
# broken classifier.
if ($classifiedTotal -eq 0 -and $commitsWalked -gt 0) {
    "EXIT_REASON=ALL_PRE_ERA"
    "Every commit in this $commitsWalked-commit window predates ERA_CUTOFF_COMMIT ($(if ($eraCutoff) { $eraCutoff.Hash.Substring(0,10) } else { 'n/a' }) at $(if ($eraCutoff) { $eraCutoff.Utc.ToString('o') } else { 'n/a' })). Nothing to classify. Pick a -Skip/-Count window that reaches into the tagged era."
    exit 2
}

# A broken classifier is loud (section 4.3): RESIDUAL_TOTAL=0 on a real post-era window
# means the classifier broke, not that the repo is clean -- section 2 measured a real
# residual, and section 9.1 re-measured it at 6% of 339 post-adoption commits.
if ($residualTotal -eq 0) {
    "EXIT_REASON=CLASSIFIER_SUSPECT"
    "RESIDUAL_TOTAL read 0 over a $classifiedTotal-commit classified window. docs/commit-walker-check-spec.md sections 2 and 9.1 measured a real residual -- treat a zero here as the path rule or the tag-detection string being broken, never as a clean repo."
    exit 2
}

# Escalation trigger (section 0 / section 4.3): residual over 25% means the path rule's
# whole architecture does not hold on this window -- stop and report, do not judge on.
if ($residualPct -gt 25) {
    "EXIT_REASON=RESIDUAL_TOO_HIGH"
    "RESIDUAL_PCT=$residualPct exceeds the 25% escalation trigger in docs/commit-walker-check-spec.md section 0. The engine-path rule in section 2 is measured to settle the large majority of commits; a residual this large means the rule does not hold for this window and needs review before any Jev call is spent on it."
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 4 (section 4.2, section 4.4): refuse to proceed without a baseline. Structural,
# before any API key read -- same mechanism and rationale as
# docs/rider-travel-check-spec.md D-4. This harness has NO pre-existing ground truth
# (docs/commit-walker-check-spec.md section 4.4): the seat's hand baseline IS the
# labelling, which makes writing it before the run more load-bearing here, not less.
# NOTE (revision 1): a baseline written under the OLD tag_correct/tag_wrong/unsure
# vocabulary will not string-match the new five-way verdict. The Agreement column below
# is a plain string comparison regardless of vocabulary era -- see the build report for
# why this is left as-is rather than guessed at.
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each RESIDUAL_CANDIDATES commit above BEFORE running this tool, as JSON, one key per full commit hash. Example:"
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
# Step 5 (section 4.2, section 6/9.3/10.3): one Jev request per RESIDUAL commit (trap 4),
# times $Samples (REVISION 2 self-consistency, replacing the retired $Repeat). REVISION
# 1's question set: three Nouls (diagnostics only) plus the `verdict` Choice, which is the
# ONLY answer code reads (TRAP 2, marked below).
# ---------------------------------------------------------------------------------------
$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set in the environment. Load typesafe.local.env first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

$verdictCriteria = @{
    no_app_change       = 'These changes do NOT alter the shipped Windows application (the code inside the .exe) when it runs: no change to what it computes, decides, renders on screen, or writes to disk. Changes confined to `tools/`, `verify/`, documentation, tests, comments, or a proven output-identical refactor of app code all satisfy this.'
    changes_computation = 'These changes alter what the shipped application computes or decides while running -- a score, a verdict, a gate outcome, a threshold comparison, or a placed ATR/Kelly level -- regardless of whether display or writes also change.'
    changes_display      = 'These changes alter a text string or value the shipped application renders on screen (a card, the plaintext snapshot, a label) while running, regardless of whether the underlying computation also changes.'
    changes_writes       = 'These changes alter what the shipped application writes to disk while running -- an `analysis_log.csv` column or value, a log line, or a trade-store write -- regardless of whether computation or display also change.'
    ambiguous            = '`commit.subject`, `commit.body`, `touched_paths`, and `per_file_line_counts` together do not give enough information to place this commit in exactly one of the four classes above.'
}

# Builds and sends one Jev request for a commit, optionally carrying a shipped-app diff
# (the low-top-probability escalation pass) and a per-sample `uid` (REVISION 2, section
# 10.1 detail 1). Centralised so the primary calls, the escalated call, and every sample
# all use the IDENTICAL question set -- one place to check for trap 2, not several.
function Invoke-CommitVerdict([string]$apiKeyIn, $c, [string]$diffText, [hashtable]$criteria, [string]$uid) {
    $commitState = @{
        subject = $c.Subject
        body    = $c.Body
        # REVISION 1 / section 9.3: `declared_no_engine_change` is deliberately NOT sent.
        # The model classifies the commit's actual effect; CODE compares that verdict to
        # the tag afterwards (see the Disagreement computation below). Sending the tag
        # here would let the model anchor on the claim it is meant to audit.
    }
    $state = @{
        commit                = $commitState
        touched_paths         = @($c.Paths)
        per_file_line_counts  = @($c.LineCounts | ForEach-Object { @{ path = $_.Path; added = $_.Added; deleted = $_.Deleted } })
    }
    if ($diffText) { $state.shipped_app_diff = $diffText }
    # REVISION 2, section 10.1 detail 1: a fresh throwaway uid per sample, in STATE, never
    # in the questions -- makes each self-consistency sample "a distinct, independent
    # draw" per the cookbook. Never read by any question's criteria.
    if ($uid) { $state.sample_uid = $uid }

    if ($DebugState) {
        Write-Host "STATE_DEBUG hash=$($c.Hash.Substring(0,10)) keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)"
    }

    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            changes_computation = @{
                type = 'noul'
                instructions = "Does this commit alter what the SHIPPED Windows application (the code inside the .exe -- NOT `tools/` or `verify/`, which are separate offline projects) computes or decides while running: a score, a verdict, a gate outcome, a threshold comparison, or a placed ATR/Kelly level? Judge from `commit.subject`, `commit.body`, `touched_paths`, and `per_file_line_counts` (and `shipped_app_diff` when present)."
            }
            changes_display = @{
                type = 'noul'
                instructions = "Does this commit alter any text or value the SHIPPED Windows application renders on screen while running -- a card, the plaintext snapshot, a label -- whether or not the underlying computation also changes? Judge from the same fields."
            }
            changes_writes = @{
                type = 'noul'
                instructions = "Does this commit alter what the SHIPPED Windows application writes to disk while running -- an `analysis_log.csv` column or value, a log line, or a trade-store write -- whether or not computation or display also change? Judge from the same fields."
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify this commit's actual effect on the SHIPPED Windows application (the code inside the .exe) using `touched_paths`, `per_file_line_counts`, `commit.subject`, `commit.body`, and `shipped_app_diff` when present. `tools/` and `verify/` changes are NOT app changes, however large."
                criteria = $criteria
            }
        }
    }

    $call = Invoke-Jev $apiKeyIn $body
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error } }

    $ans = $call.Response.answers
    # --- TRAP 2 (docs/commit-walker-check-spec.md section 0 / section 9.3): the verdict
    # comes from the `verdict` Choice answer ALONE. The three Noul answers below are
    # diagnostics ONLY -- never combined with `-and`/`-or` into a derived verdict. This is
    # the one line a reviewer checks.
    $verdict = $ans.verdict.choice

    # REVISION 2 / section 10.1 detail 2: the per-option PROBABILITIES, not `confidence`.
    # docs.typesafe.ai/cookbooks/consistency_choice_cookbook.md: `response.answers[key].
    # probabilities` is a dict keyed by option label. Read every value and take the max --
    # that IS "the top probability" this revision's band is applied to.
    $probs = @{}
    $topProbability = $null
    if ($ans.verdict.probabilities) {
        foreach ($p in $ans.verdict.probabilities.PSObject.Properties) {
            $probs[$p.Name] = [double]$p.Value
        }
        if ($probs.Count -gt 0) { $topProbability = ($probs.Values | Measure-Object -Maximum).Maximum }
    }

    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens)  { $usageIn  = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }

    return @{
        Ok                = $true
        Verdict           = $verdict
        Probabilities     = $probs
        TopProbability    = $topProbability
        # Confidence travels only as a side diagnostic -- REVISION 2 never reads it for
        # any decision (section 10.1 detail 2). Kept so a reviewer can see the gap between
        # it and TopProbability on the same call, the exact confusion this revision closes.
        Confidence        = $ans.verdict.confidence
        ComputationNoul   = $ans.changes_computation.noul
        DisplayNoul       = $ans.changes_display.noul
        WritesNoul        = $ans.changes_writes.noul
        UsageInputTokens  = $usageIn
        UsageOutputTokens = $usageOut
    }
}

# REVISION 2 CORRECTION (section 10.7 item 2): shared by the primary sample loop AND the
# escalation sample loop below, so "computing its plurality and agreement rate exactly as
# the primary does" is true by construction -- one function, not two copies of the same
# arithmetic that can silently drift apart. Plurality by raw vote count; ties broken by
# first-seen order among the samples, matching the cookbook's Counter.most_common tie
# behaviour (unchanged from revision 2).
function Get-SelfConsistencyAggregate([object[]]$sampleResultsIn) {
    $verdictCounts = @{}
    foreach ($sr in $sampleResultsIn) {
        if (-not $verdictCounts.ContainsKey($sr.Verdict)) { $verdictCounts[$sr.Verdict] = 0 }
        $verdictCounts[$sr.Verdict] += 1
    }
    $pluralityVerdict = $null
    $pluralityCount = -1
    foreach ($sr in $sampleResultsIn) {
        $v = $sr.Verdict
        if ($verdictCounts[$v] -gt $pluralityCount) {
            $pluralityCount = $verdictCounts[$v]
            $pluralityVerdict = $v
        }
    }
    $agreementRate = [math]::Round(($pluralityCount / $sampleResultsIn.Count), 3)

    $topProbs = @($sampleResultsIn | Where-Object { $null -ne $_.TopProbability } | ForEach-Object { $_.TopProbability })
    $meanTopProbability = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Average).Average), 3) } else { $null }
    $minTopProbability  = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Minimum).Minimum), 3) } else { $null }

    $sampleVerdicts = @($sampleResultsIn | ForEach-Object { $_.Verdict })
    $sampleTopProbs = @($sampleResultsIn | ForEach-Object { if ($null -ne $_.TopProbability) { [math]::Round([double]$_.TopProbability, 3) } else { $null } })

    return [PSCustomObject]@{
        PluralityVerdict       = $pluralityVerdict
        AgreementRate          = $agreementRate
        Stable                  = ($agreementRate -ge 1.0)
        MeanTopProbability      = $meanTopProbability
        MinTopProbability       = $minTopProbability
        SampleCount             = $sampleResultsIn.Count
        SampleVerdicts          = ($sampleVerdicts -join ',')
        SampleTopProbabilities  = ($sampleTopProbs -join ',')
    }
}

$results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''
$usageInputTokens = 0
$usageOutputTokens = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($c in $residualAll) {
    $sampleResults = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Samples; $i++) {
        $uid = "$($c.Hash.Substring(0,10)):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $r = Invoke-CommitVerdict $apiKey $c $null $verdictCriteria $uid
        if (-not $r.Ok) {
            $apiFailed = $true
            $apiFailMsg = "Jev request failed for $($c.Hash.Substring(0,10)) (sample $($i+1)/$Samples): $($r.Error)"
            break
        }
        $usageInputTokens  += $r.UsageInputTokens
        $usageOutputTokens += $r.UsageOutputTokens
        $sampleResults.Add($r)
    }
    if ($apiFailed) { break }

    # -------------------------------------------------------------------------------
    # REVISION 2 self-consistency aggregation (section 10.3 items 2-3, section 10.2's
    # matrix), via the shared Get-SelfConsistencyAggregate (revision 2 correction, section
    # 10.7 item 2) so the escalation loop below computes its own stats the SAME way.
    # -------------------------------------------------------------------------------
    $agg = Get-SelfConsistencyAggregate $sampleResults
    $pluralityVerdict  = $agg.PluralityVerdict
    $agreementRate     = $agg.AgreementRate
    $stable            = $agg.Stable
    $meanTopProbability = $agg.MeanTopProbability
    $minTopProbability  = $agg.MinTopProbability

    $meanComputationNoul = [math]::Round((($sampleResults | ForEach-Object { [double]$_.ComputationNoul } | Measure-Object -Average).Average), 3)
    $meanDisplayNoul     = [math]::Round((($sampleResults | ForEach-Object { [double]$_.DisplayNoul } | Measure-Object -Average).Average), 3)
    $meanWritesNoul      = [math]::Round((($sampleResults | ForEach-Object { [double]$_.WritesNoul } | Measure-Object -Average).Average), 3)

    $escalated = $false
    $escalationNote = $null
    $finalVerdict = $pluralityVerdict
    $escPluralityVerdict = $null
    $escAgreementRate = $null
    $escStable = $null
    $escMeanTopProbability = $null
    $escMinTopProbability = $null
    $escSampleCount = 0
    $escSampleVerdicts = ''
    $escSampleTopProbabilities = ''

    # --- THE BAND (section 10.1 detail 2 / section 10.3 item 4): applied to the MEAN
    # top probability across the agreeing samples, never to `confidence`. This is the one
    # line acceptance item 3 asks for -- $MIN_TOP_PROBABILITY compared against
    # $meanTopProbability, both derived from $ans.verdict.probabilities above, never from
    # $ans.verdict.confidence.
    if (-not $stable) {
        # DISAGREE cell (section 10.2): genuinely on the fence. NEVER escalated, NEVER
        # auto-resolved (section 10.3 item 7) -- structurally unreachable from here into
        # the escalation branch below. UNCHANGED by the revision 2 correction (section
        # 10.9 item 5).
        $status = 'UNSTABLE'
    } elseif ($null -ne $meanTopProbability -and $meanTopProbability -lt $MIN_TOP_PROBABILITY) {
        # AGREE, top probability LOW: stable but under-informed -- ESCALATE.
        $engineFiles = Get-EngineTouchedPaths $c.Paths
        if ($engineFiles.Count -gt 0) {
            $diffText = Get-ShippedAppDiff $repo $c.Hash $engineFiles $MAX_ESCALATION_DIFF_CHARS

            # ---------------------------------------------------------------------------
            # REVISION 2 CORRECTION (section 10.7 items 1-2): the escalation call is now
            # SAMPLED, $ESCALATION_SAMPLES times, with a fresh `uid` per sample exactly
            # like the primary loop above -- an unsampled tie-breaker was the defect this
            # correction exists to fix. Its plurality/agreement/probability stats are
            # computed by the SAME Get-SelfConsistencyAggregate function the primary used.
            # ---------------------------------------------------------------------------
            $escSampleResultsList = New-Object System.Collections.Generic.List[object]
            for ($j = 0; $j -lt $ESCALATION_SAMPLES; $j++) {
                $escUid = "$($c.Hash.Substring(0,10)):esc:$j`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
                $escResult = Invoke-CommitVerdict $apiKey $c $diffText $verdictCriteria $escUid
                if (-not $escResult.Ok) {
                    $apiFailed = $true
                    $apiFailMsg = "Jev escalation request failed for $($c.Hash.Substring(0,10)) (escalation sample $($j+1)/$ESCALATION_SAMPLES): $($escResult.Error)"
                    break
                }
                $usageInputTokens  += $escResult.UsageInputTokens
                $usageOutputTokens += $escResult.UsageOutputTokens
                $escSampleResultsList.Add($escResult)
            }
            if ($apiFailed) { break }

            $escAgg = Get-SelfConsistencyAggregate $escSampleResultsList
            $escPluralityVerdict       = $escAgg.PluralityVerdict
            $escAgreementRate          = $escAgg.AgreementRate
            $escStable                 = $escAgg.Stable
            $escMeanTopProbability     = $escAgg.MeanTopProbability
            $escMinTopProbability      = $escAgg.MinTopProbability
            $escSampleCount            = $escAgg.SampleCount
            $escSampleVerdicts         = $escAgg.SampleVerdicts
            $escSampleTopProbabilities = $escAgg.SampleTopProbabilities
            $escalated = $true

            # --- CONFLICT (section 10.7 item 2): the escalated plurality disagrees with
            # the PRIMARY plurality ($pluralityVerdict, from the sampled primary above).
            # The tool does NOT pick a winner -- $finalVerdict is left at $pluralityVerdict
            # (its pre-escalation value) only as a display fallback; the CONFLICT status
            # below, not this assignment, is what a caller must act on. Report BOTH.
            if ($escPluralityVerdict -ne $pluralityVerdict) {
                $status = 'CONFLICT'
            } else {
                # CONFIDENT_AFTER_ESCALATION (section 10.7 item 4): escalated AND agreed
                # with the primary.
                $finalVerdict = $escPluralityVerdict
                $status = 'CONFIDENT_AFTER_ESCALATION'
            }
        } else {
            # RESIDUAL_UNTAGGED_NO_ENGINE_PATH commits touch, by definition, zero engine
            # paths -- there is no shipped-app diff to send, so escalation is skipped
            # rather than sent empty.
            $escalationNote = 'SKIPPED_NO_ENGINE_FILES'
            $status = 'AGREE_LOW_PROBABILITY_NO_ESCALATION_TARGET'
        }
    } else {
        # AGREE, top probability HIGH: confident and stable.
        $status = 'CONFIDENT'
    }
    if ($apiFailed) { break }

    # Section 9.3: code, not the model, compares verdict to tag. A DISAGREEMENT exists
    # only in the tagged direction -- an untagged commit makes no claim to contradict
    # (section 9.2: the tag is inconsistent at source, so "should this have been tagged"
    # is not a question this harness answers). UNSTABLE and CONFLICT both override the
    # tagged/untagged split: an unresolved row (self-consistency disagreement, OR an
    # escalated plurality disagreeing with the primary) is reported as unresolved, never
    # coerced into CONSISTENT/DISAGREEMENT by a read the harness itself will not stand
    # behind.
    $disagreement =
        if ($status -eq 'UNSTABLE') { 'UNSTABLE' }
        elseif ($status -eq 'CONFLICT') { 'CONFLICT' }
        elseif ($c.Tagged) { if ($finalVerdict -ne 'no_app_change') { 'DISAGREEMENT' } else { 'CONSISTENT' } }
        else { 'N/A_UNTAGGED' }

    $results.Add([PSCustomObject]@{
        Hash                       = $c.Hash
        Subject                    = $c.Subject
        Class                      = if ($c.Tagged) { 'TAGGED_BUT_ENGINE_PATH' } else { 'UNTAGGED_NO_ENGINE_PATH' }
        Tagged                     = $c.Tagged
        Verdict                    = $finalVerdict
        PluralityVerdict           = $pluralityVerdict
        Status                     = $status
        AgreementRate              = $agreementRate
        Stable                     = $stable
        MeanTopProbability          = $meanTopProbability
        MinTopProbability          = $minTopProbability
        ComputationNoul            = $meanComputationNoul
        DisplayNoul                = $meanDisplayNoul
        WritesNoul                 = $meanWritesNoul
        Disagreement               = $disagreement
        Escalated                  = $escalated
        EscalationNote             = $escalationNote
        EscalatedPluralityVerdict  = $escPluralityVerdict
        EscalatedAgreementRate     = $escAgreementRate
        EscalatedStable            = $escStable
        EscalatedMeanTopProbability = $escMeanTopProbability
        EscalatedMinTopProbability = $escMinTopProbability
        EscalatedSampleCount       = $escSampleCount
        EscalatedSampleVerdicts    = $escSampleVerdicts
        EscalatedSampleTopProbabilities = $escSampleTopProbabilities
        SampleCount                = $agg.SampleCount
        SampleVerdicts             = $agg.SampleVerdicts
        SampleTopProbabilities     = $agg.SampleTopProbabilities
        Baseline                   = if ($baseline.ContainsKey($c.Hash)) { $baseline[$c.Hash] } else { $null }
    })
}
$sw.Stop()

if ($apiFailed) {
    Write-Coverage $commitsWalked $mergesExcluded $preEra.Count $agreeTagged.Count $agreeUntagged.Count `
        $residualTaggedButEngine.Count $residualUntaggedNoEngine.Count $residualTotal $residualPct $results.Count
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 6 (section 4.2, section 4.5): report, then exit from `verdict`/`Disagreement` alone.
# ---------------------------------------------------------------------------------------
Write-Coverage $commitsWalked $mergesExcluded $preEra.Count $agreeTagged.Count $agreeUntagged.Count `
    $residualTaggedButEngine.Count $residualUntaggedNoEngine.Count $residualTotal $residualPct $results.Count

"USAGE_INPUT_TOKENS=$usageInputTokens"
"USAGE_OUTPUT_TOKENS=$usageOutputTokens"
"WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"

# docs/harness-shadow-mode-protocol.md section 4a: -CountersOnly withholds every
# per-commit and aggregate line from a RESERVED window's dry run (section 10.6 item 6).
# $disagreementCount/$unstableCount/$conflictCount are computed either way -- the exit
# code (bottom of file) reads $results directly and must not depend on what got printed.
$disagreementCount = @($results | Where-Object { $_.Disagreement -eq 'DISAGREEMENT' }).Count
$unstableCount = @($results | Where-Object { $_.Disagreement -eq 'UNSTABLE' }).Count
# REVISION 2 CORRECTION (section 10.7 item 2): CONFLICT is its own count, alongside
# DISAGREEMENT and UNSTABLE, never folded into either.
$conflictCount = @($results | Where-Object { $_.Disagreement -eq 'CONFLICT' }).Count

if ($CountersOnly) {
    "COUNTERS_ONLY=true (per-commit verdicts and aggregates withheld -- docs/harness-shadow-mode-protocol.md section 4a)"
} else {
    "PER_COMMIT_RESULTS:"
    foreach ($res in $results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
                 elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
                 elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
                 else { 'DISAGREE' }
        # REVISION 2 CORRECTION (section 10.7 item 2 / section 10.9 item 3): when escalated,
        # report BOTH the primary plurality+agreement rate+probability range AND the
        # escalated plurality+agreement rate+probability range -- never only the primary's
        # pre-escalation snapshot, and never a single blended number.
        $escTxt = if ($res.Escalated) { "escalated=true primary=[plurality=$($res.PluralityVerdict) agreement=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability)] escalated=[plurality=$($res.EscalatedPluralityVerdict) agreement=$($res.EscalatedAgreementRate) mean_top_prob=$($res.EscalatedMeanTopProbability) min_top_prob=$($res.EscalatedMinTopProbability)]" }
                  elseif ($res.EscalationNote) { "escalated=false ($($res.EscalationNote))" }
                  else { "escalated=false" }
        # REVISION 2, section 10.3 item 3: agreement rate on EVERY row, not only unstable ones.
        "  $($res.Hash.Substring(0,10)) [$($res.Class)] status=$($res.Status) verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability) disagreement=$($res.Disagreement) baseline=$($res.Baseline) [$agree] $escTxt computation_noul=$($res.ComputationNoul) display_noul=$($res.DisplayNoul) writes_noul=$($res.WritesNoul)"
    }

    "SELF_CONSISTENCY (-Samples $Samples -- raw sample sequence per commit, not interpreted):"
    foreach ($res in $results) {
        $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
        "  $($res.Hash.Substring(0,10)) [$stableTxt] n=$($res.SampleCount) plurality=$($res.PluralityVerdict) agreement_rate=$($res.AgreementRate) verdicts=[$($res.SampleVerdicts)] top_probs=[$($res.SampleTopProbabilities)]"
    }

    "DISAGREEMENTS=$disagreementCount"
    "UNSTABLE=$unstableCount"
    "CONFLICT=$conflictCount"
    if ($disagreementCount -gt 0) {
        "DISAGREEMENT_LIST (tagged [no-engine-change] but verdict != no_app_change -- for the seat to adjudicate):"
        foreach ($res in ($results | Where-Object { $_.Disagreement -eq 'DISAGREEMENT' })) {
            "  $($res.Hash.Substring(0,10)) verdict=$($res.Verdict) $($res.Subject)"
        }
    }
    if ($unstableCount -gt 0) {
        "UNSTABLE_LIST (self-consistency agreement rate below 1.0 -- never auto-resolved, for the seat to adjudicate):"
        foreach ($res in ($results | Where-Object { $_.Disagreement -eq 'UNSTABLE' })) {
            "  $($res.Hash.Substring(0,10)) agreement_rate=$($res.AgreementRate) verdicts=[$($res.SampleVerdicts)] $($res.Subject)"
        }
    }
    if ($conflictCount -gt 0) {
        "CONFLICT_LIST (escalated plurality disagrees with the primary plurality -- the tool does NOT pick a winner, for the seat to adjudicate):"
        foreach ($res in ($results | Where-Object { $_.Disagreement -eq 'CONFLICT' })) {
            "  $($res.Hash.Substring(0,10)) primary=[plurality=$($res.PluralityVerdict) agreement=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability)] escalated=[plurality=$($res.EscalatedPluralityVerdict) agreement=$($res.EscalatedAgreementRate) mean_top_prob=$($res.EscalatedMeanTopProbability)] $($res.Subject)"
        }
    }
}

# Write the markdown report.
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Commit-walker check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/commit-walker.ps1`` against ``-Skip $Skip -Count $Count -Samples $Samples`` at ``HEAD``.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| ERA_CUTOFF_COMMIT | $(if ($eraCutoff) { $eraCutoff.Hash.Substring(0,10) } else { 'NONE_FOUND' }) |")
$reportLines.Add("| ERA_CUTOFF_UTC | $(if ($eraCutoff) { $eraCutoff.Utc.ToString('yyyy-MM-ddTHH:mm:ssZ') } else { 'N/A' }) |")
$reportLines.Add("| COMMITS_WALKED | $commitsWalked |")
$reportLines.Add("| MERGES_EXCLUDED | $mergesExcluded |")
$reportLines.Add("| PRE_ERA_EXCLUDED | $($preEra.Count) |")
$reportLines.Add("| AGREE_TAGGED_NO_ENGINE_PATH | $($agreeTagged.Count) |")
$reportLines.Add("| AGREE_UNTAGGED_ENGINE_PATH | $($agreeUntagged.Count) |")
$reportLines.Add("| RESIDUAL_TAGGED_BUT_ENGINE_PATH | $($residualTaggedButEngine.Count) |")
$reportLines.Add("| RESIDUAL_UNTAGGED_NO_ENGINE_PATH | $($residualUntaggedNoEngine.Count) |")
$reportLines.Add("| RESIDUAL_TOTAL | $residualTotal |")
$reportLines.Add("| RESIDUAL_PCT | $residualPct |")
$reportLines.Add("| COMMITS_JUDGED | $($results.Count) |")
if (-not $CountersOnly) {
    $reportLines.Add("| DISAGREEMENTS | $disagreementCount |")
    $reportLines.Add("| UNSTABLE | $unstableCount |")
    $reportLines.Add("| CONFLICT | $conflictCount |")
}
$reportLines.Add("| USAGE_INPUT_TOKENS | $usageInputTokens |")
$reportLines.Add("| USAGE_OUTPUT_TOKENS | $usageOutputTokens |")
$reportLines.Add("| WALL_TIME_SEC | $([math]::Round($sw.Elapsed.TotalSeconds,2)) |")
$reportLines.Add('')
if ($CountersOnly) {
    $reportLines.Add('## Per-commit detail withheld')
    $reportLines.Add('')
    $reportLines.Add('Run with `-CountersOnly` over a RESERVED window (docs/harness-shadow-mode-protocol.md section 4a). Per-commit verdicts and every aggregate (DISAGREEMENTS, UNSTABLE, CONFLICT) are withheld from both console and this report so the window is not contaminated for its eventual measured first run.')
} else {
    $reportLines.Add('## Per-commit verdicts (residual only -- the two agreeing classes above are counts only, never judged)')
    $reportLines.Add('')
    $reportLines.Add('| Hash | Class | Status | Verdict | Agreement rate | Mean top prob | Min top prob | Disagreement | Escalated | Baseline | Agreement | computation (noul) | display (noul) | writes (noul) | Subject |')
    $reportLines.Add('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    foreach ($res in $results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
                 elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
                 elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
                 else { 'DISAGREE' }
        $subjEsc = $res.Subject -replace '\|', '\|'
        $reportLines.Add("| $($res.Hash.Substring(0,10)) | $($res.Class) | $($res.Status) | $($res.Verdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.MinTopProbability) | $($res.Disagreement) | $($res.Escalated) | $($res.Baseline) | $agree | $($res.ComputationNoul) | $($res.DisplayNoul) | $($res.WritesNoul) | $subjEsc |")
    }
    $reportLines.Add('')
    $reportLines.Add("## Self-consistency (-Samples $Samples)")
    $reportLines.Add('')
    $reportLines.Add('| Hash | Stable | N | Plurality | Agreement rate | Verdicts | Top probabilities |')
    $reportLines.Add('|---|---|---|---|---|---|---|')
    foreach ($res in $results) {
        $reportLines.Add("| $($res.Hash.Substring(0,10)) | $($res.Stable) | $($res.SampleCount) | $($res.PluralityVerdict) | $($res.AgreementRate) | $($res.SampleVerdicts) | $($res.SampleTopProbabilities) |")
    }
    $escRows = @($results | Where-Object { $_.Escalated })
    if ($escRows.Count -gt 0) {
        $reportLines.Add('')
        $reportLines.Add("## Escalation (revision 2 correction, section 10.7 -- escalation sampled $ESCALATION_SAMPLES times per commit, primary plurality never silently overridden)")
        $reportLines.Add('')
        $reportLines.Add('| Hash | Status | Primary plurality | Primary agreement | Primary mean top prob | Escalated plurality | Escalated agreement | Escalated mean top prob | Escalated verdicts |')
        $reportLines.Add('|---|---|---|---|---|---|---|---|---|')
        foreach ($res in $escRows) {
            $reportLines.Add("| $($res.Hash.Substring(0,10)) | $($res.Status) | $($res.PluralityVerdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.EscalatedPluralityVerdict) | $($res.EscalatedAgreementRate) | $($res.EscalatedMeanTopProbability) | $($res.EscalatedSampleVerdicts) |")
        }
    }
}
if ($preEra.Count -gt 0) {
    $reportLines.Add('')
    $reportLines.Add('## Pre-era commits (reported, not classified)')
    $reportLines.Add('')
    $reportLines.Add('| Hash | Subject |')
    $reportLines.Add('|---|---|')
    foreach ($c in $preEra) {
        $subjEsc = $c.Subject -replace '\|', '\|'
        $reportLines.Add("| $($c.Hash.Substring(0,10)) | $subjEsc |")
    }
}
$reportFull = Resolve-RepoPath $OutPath
Set-Content -Encoding UTF8 -Path $reportFull -Value ($reportLines -join "`r`n")
"Report written to $OutPath"

# REVISION 2: an UNSTABLE row is, by section 10.3 item 7, never auto-resolved -- so it
# must not be swallowed into a 0 exit either. REVISION 2 CORRECTION (section 10.7 item 2):
# CONFLICT joins it on the same basis -- an escalated plurality that disagrees with the
# primary is unresolved by construction (the tool does not pick a winner), so it must not
# exit 0 either. Four non-zero conditions total: `ambiguous`, DISAGREEMENT, UNSTABLE,
# CONFLICT.
$anyBad = @($results | Where-Object { $_.Verdict -eq 'ambiguous' -or $_.Disagreement -eq 'DISAGREEMENT' -or $_.Disagreement -eq 'UNSTABLE' -or $_.Disagreement -eq 'CONFLICT' }).Count -gt 0
if ($anyBad) { exit 1 } else { exit 0 }
