#requires -Version 5.1
<#
  tools/checks/rider-travel.ps1 -- checks whether every TRAVELLING rider in
  docs/csv-rotation-riders.md actually arrived in a PROPOSED new AnalysisLogger.Header.

  ADVISORY ONLY (docs/rider-travel-check-spec.md D-3). Not wired into verify-gate.ps1.
  Run it by hand before a header-rotation commit lands, alongside an operator baseline
  written independently (D-4, §4.4).

  WHY: docs/csv-rotation-riders.md §0 rule 2 says the gate forces the ledger file to be
  TOUCHED on a rotation, never that its CONTENT is right. This tool is the review aid for
  that content: it reads each TRAVELLING rider's prose description and asks Jev (TypeSafe's
  System One model) whether a matching column actually landed in the proposed header.

  WHAT IT DOES NOT CHECK (docs/rider-travel-check-spec.md §8): ledger completeness (a rider
  never written down cannot be found -- that is how RIDER-7 was lost), correctness of an
  arrived column's name/position/value (presence only), and settings-touch riders.

  See docs/rider-travel-check-spec.md for the full design, including the three traps in
  its §0 -- most importantly: the verdict below is read from the `verdict` Choice answer
  ALONE. The two Noul answers (lands_in_header, column_present) are diagnostics, printed
  beside the verdict, and are NEVER combined with it or with each other.

  USAGE:
    set -a; . ./typesafe.local.env; set +a   # loads TYPESAFE_API_KEY (bash)
    powershell -NoProfile -File tools/checks/rider-travel.ps1 `
      -AfterFile <path to the proposed AnalysisLogger.vb> `
      -BaselinePath <path to your own pre-written read, see §4.4>

  EXIT CODES (§4.6, REVISION 2):
    0 - every TRAVELLING rider judged arrived or not_a_header_column, every Jev row STABLE,
        and no rider was WAF_BLOCKED
    1 - at least one rider judged missing or ambiguous, any Jev row UNSTABLE, or any rider
        WAF_BLOCKED (item 8n -- an unjudged item is never a pass)
    2 - parse failure, NO_ROTATION, baseline missing or incomplete, or API failure (D-5)

  ============================== REVISION 1 (2026-09-22 UTC) ==============================
  docs/jev-harnesses-adversarial-review-2026-09-22.md finding 2 (HIGH) and finding 5; trader
  "go". Made BEFORE the first run, which is this harness's only measurement.
    RT-R1-1: CODE decides every rider whose column is NAMED (Lands in = `header`, and a
      backticked identifier before the first "column"/"columns" word). Exact membership in
      the proposed header; no Jev call. At the 2026-09-22 ledger: 6 of 8 travelling riders.
    RT-R1-2: the rest go to Jev $Samples times (default 5), a fresh uid each; plurality,
      agreement rate and top probability on every row; a split vote is UNSTABLE, exit 1.
    RT-R1-3: RIDER_CANDIDATES printed before the gate, with how each will be decided.
    RT-R1-4: EXIT_REASON=NO_ROTATION when the proposed header adds no column, so a run
      made before the rotation cannot spend the riders on an unrotated header.
    RT-R1-5: item-level refusal (BASELINE_INCOMPLETE) when any travelling rider has no
      baseline line; -AllowUnbaselinedItems for routine re-runs.
  ==========================================================================================

  ============================== REVISION 2 (2026-09-23 UTC) ==============================
  Arming batch: docs/jev-harnesses-adversarial-review-2026-09-22.md findings 6, 8n, 11.
    RT-R2-1 (item 8n, the FP-D26 pattern): a web-firewall block on a rider's Jev call no
      longer aborts the whole run. The rider is recorded verdict WAF_BLOCKED (agreement
      NOT_JUDGED), the run continues to the remaining riders, RIDERS_WAF_BLOCKED prints,
      and any blocked rider makes the exit code 1 -- an unjudged item is never a pass.
      Harness 1's first run is one-shot on 8 riders; without this, one blocked rider used
      to spend the whole population on an aborted run.
    RT-R2-2 (finding 11): tools/checks/lib/InvokeJev.ps1 now retries transient failures
      (HTTP 5xx, or no response at all) with bounded backoff; a 403/other 4xx is never
      retried. RETRY_COUNT sums RetryCount across every Jev call this run makes, printed
      beside USAGE_INPUT_TOKENS.
    RT-R2-3 (finding 6, "an OK-class misread passes the exit code"): unlike fixture-
      parser's FP-D25 (item 8m), not_a_header_column is NOT moved into the bad-verdict set.
      That class is provably always wrong wherever it fires (an FP-1 site is always a
      hardcoded literal); not_a_header_column is not provably anything -- at the current
      ledger RIDER-1 and RIDER-2 are CORRECTLY not_a_header_column (they land in code / an
      ops doc, never the header), so folding it into the exit code would make a normal run
      exit 1 by construction. Instead: RIDERS_NOT_A_HEADER_COLUMN (with a STABLE sub-count)
      prints as its own reportable line, never touching $anyBad -- the docs/harness-
      shadow-mode-protocol.md section 4g point ("a stable OK is not evidence") made visible
      without inventing a verdict that always fails.
  ==========================================================================================
#>
[CmdletBinding()]
param(
    [string]$AfterFile = 'AnalysisLogger.vb',
    [string]$BeforeRev = 'HEAD',
    [string]$LedgerPath = 'docs/csv-rotation-riders.md',
    [string]$BaselinePath = 'rider-travel-baseline.json',
    [string]$OutPath = 'rider-travel-report.md',
    # REVISION 1 (adversarial review finding 2a): self-consistency draws per Jev-judged
    # rider (docs/harness-shadow-mode-protocol.md section 4b).
    [int]$Samples = 5,
    # REVISION 1 (finding 2b): refuse when any travelling rider has no baseline line,
    # unless this is passed -- for a routine re-run of riders measured once.
    [switch]$AllowUnbaselinedItems,
    # REVISION 2, TEST SEAM ONLY -- OFF BY DEFAULT ($null). Forwarded verbatim to every
    # Invoke-Jev call in this run in place of the real HTTP transport (see
    # tools/checks/lib/InvokeJev.ps1's own -TransportOverride). Exists so item 8n's
    # WAF-block handling and finding 11's retry accounting can be exercised end-to-end,
    # through this actual script, on synthetic riders -- never against the real 8-rider
    # one-shot population. No caller passes this in a real run.
    [scriptblock]$TestTransportOverride = $null
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Get-HeaderText is shared with tools/checks/rotation-riders.ps1 (docs/rider-travel-check-spec.md
# D-2). Do not re-implement it here -- a second copy drifts the first time the header
# declaration is refactored.
. (Join-Path $PSScriptRoot 'lib\HeaderText.ps1')

# Invoke-Jev is shared with tools/checks/commit-walker.ps1 (docs/commit-walker-check-spec.md
# section 6). Do not re-implement it here -- see tools/checks/lib/InvokeJev.ps1's header
# comment for why the manual UTF-8 body encoding is load-bearing, not cosmetic.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

# The git path used to read the BEFORE header. Matches rotation-riders.ps1's own default;
# not exposed as a parameter because the spec's §4.1 input table does not list one --
# -AfterFile is how a proposed header (possibly a scratch copy) is supplied.
$LoggerPath = 'AnalysisLogger.vb'

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

function Read-AtRev([string]$rev, [string]$path) {
    $spec = '{0}:{1}' -f $rev, $path
    $out = & git -C $repo show $spec 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return [string[]]$out
}

function Get-CleanId([string]$raw) {
    $c = $raw.Trim()
    $c = $c -replace '~~', ''
    $c = $c -replace '`', ''
    return $c.Trim()
}

# Parses docs/csv-rotation-riders.md §1 ("## 1. The ledger") only -- stops at the next
# "## " heading (the §2 archive). Splits each row on UNESCAPED pipes (a `\|` inside a
# cell stays literal) per the repo's own markdown-table lesson: a pipe inside backticks
# is not protected by the backticks, only by a backslash.
# Returns $null if the file is missing or §1's table cannot be located at all (parse
# failure, distinct from "table located but has zero data rows").
function Get-LedgerRows([string]$fullPath) {
    if (-not (Test-Path $fullPath)) { return $null }
    $lines = [string[]](Get-Content -Encoding UTF8 -Path $fullPath)
    $start = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^##\s+1\.') { $start = $i; break }
    }
    if ($start -lt 0) { return $null }
    $end = $lines.Length
    for ($i = $start + 1; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^##\s+\d') { $end = $i; break }
    }
    $rows = New-Object System.Collections.Generic.List[object]
    $seenHeader = $false
    $seenSep = $false
    for ($i = $start; $i -lt $end; $i++) {
        $line = $lines[$i]
        if ($line -notmatch '^\s*\|') { continue }
        if (-not $seenHeader) { $seenHeader = $true; continue }   # "| ID | Rider | ... |"
        if (-not $seenSep) { $seenSep = $true; continue }         # "|---|---|...|"
        $parts = [regex]::Split($line.Trim(), '(?<!\\)\|')
        $cells = New-Object System.Collections.Generic.List[string]
        for ($k = 0; $k -lt $parts.Length; $k++) {
            if ($k -eq 0 -and $parts[$k].Trim() -eq '') { continue }
            if ($k -eq ($parts.Length - 1) -and $parts[$k].Trim() -eq '') { continue }
            $cells.Add(($parts[$k].Trim() -replace '\\\|', '|'))
        }
        if ($cells.Count -lt 5) { continue }
        $rows.Add([PSCustomObject]@{
            RawId        = $cells[0]
            Id           = Get-CleanId $cells[0]
            Rider        = $cells[1]
            SourceRuling = $cells[2]
            Status       = $cells[3]
            LandsIn      = $cells[4]
        })
    }
    # Unary comma: without it the output pipeline enumerates the list, and a ONE-row
    # ledger arrives at the caller as a bare PSCustomObject whose .Count is $null in 5.1.
    return ,$rows
}

function Write-Coverage([int]$inLedger, [int]$travelling, [int]$judged, [int]$colBefore, [int]$colAfter, [int]$added, [int]$removed) {
    "RIDERS_IN_LEDGER=$inLedger"
    "RIDERS_TRAVELLING=$travelling"
    "RIDERS_JUDGED=$judged"
    "HEADER_COLUMNS_BEFORE=$colBefore"
    "HEADER_COLUMNS_AFTER=$colAfter"
    "COLUMNS_ADDED=$added"
    "COLUMNS_REMOVED=$removed"
}

# ---------------------------------------------------------------------------------------
# Step 1 (§4.2): extract BEFORE / AFTER header text and diff the column lists.
# ---------------------------------------------------------------------------------------
$beforeLines = Read-AtRev $BeforeRev $LoggerPath
$beforeHeader = Get-HeaderText $beforeLines

$afterFull = Resolve-RepoPath $AfterFile
$afterHeader = if (Test-Path $afterFull) { Get-HeaderText ([string[]](Get-Content -Encoding UTF8 -Path $afterFull)) } else { $null }

$headerExtractOk = ($null -ne $beforeHeader) -and ($null -ne $afterHeader)
$beforeCols = @(if ($null -ne $beforeHeader) { $beforeHeader.Split(',') })
$afterCols  = @(if ($null -ne $afterHeader)  { $afterHeader.Split(',') })
$addedCols   = @($afterCols  | Where-Object { $beforeCols -notcontains $_ })
$removedCols = @($beforeCols | Where-Object { $afterCols  -notcontains $_ })

# ---------------------------------------------------------------------------------------
# Steps 2-3 (§4.2): parse the ledger, split TRAVELLING from everything else.
# ---------------------------------------------------------------------------------------
$ledgerFull = Resolve-RepoPath $LedgerPath
$allRows = Get-LedgerRows $ledgerFull
$ridersInLedger = if ($null -eq $allRows) { 0 } else { $allRows.Count }
# @() wraps the WHOLE if-expression: an if-expression's output is enumerated like a
# function's, so an @() inside a branch does not survive a single match.
$travelling = @(if ($null -ne $allRows) { $allRows | Where-Object { $_.Status -match 'TRAVELLING' } })
$others     = @(if ($null -ne $allRows) { $allRows | Where-Object { $_.Status -notmatch 'TRAVELLING' } })
$ridersTravelling = $travelling.Count

# ---------------------------------------------------------------------------------------
# §4.3: coverage output prints before anything else on every exit path. RIDERS_JUDGED is
# 0 here on every early-exit branch (nothing has been judged yet); the happy path reprints
# it with the real count right before the per-rider report, still ahead of that report.
# ---------------------------------------------------------------------------------------
Write-Coverage $ridersInLedger $ridersTravelling 0 $beforeCols.Count $afterCols.Count $addedCols.Count $removedCols.Count

if (-not $headerExtractOk) {
    "EXIT_REASON=HEADER_EXTRACT_FAILED"
    "cannot extract AnalysisLogger.Header (before=$($null -ne $beforeHeader) after=$($null -ne $afterHeader)) from -BeforeRev '$BeforeRev' / -AfterFile '$AfterFile'"
    exit 2
}

if ($ridersInLedger -eq 0 -or $ridersTravelling -eq 0) {
    "EXIT_REASON=LEDGER_PARSE_FAILED"
    "RIDERS_IN_LEDGER or RIDERS_TRAVELLING read 0 against -LedgerPath '$LedgerPath' - treat this as the parser being broken, not as a clean ledger. A table reformat or a missing '## 1.' heading both look like this."
    exit 2
}

if ($others.Count -gt 0) {
    "NON_TRAVELLING_RIDERS (reported, not judged):"
    foreach ($o in $others) { "  $($o.Id) status=$($o.Status)" }
}

# ---------------------------------------------------------------------------------------
# REVISION 1 (docs/jev-harnesses-adversarial-review-2026-09-22.md finding 5): CODE decides
# every rider whose column is NAMED. Set membership is exact arithmetic, and the Jev docs'
# first anti-pattern is asking the model something code can compute. A rider is
# code-decided when its "Lands in" cell is exactly `header` AND its description names at
# least one backticked identifier BEFORE its first "column"/"columns" word -- the ledger's
# own convention (`TriggerMode` column; `VPFRSignal` and `VPFRPoc` columns). Identifiers
# AFTER that word are prose (RIDER-4 names `DeriveWsHealth`, a method, after "column").
# Everything else goes to Jev. The extraction is printed per rider, so a wrong parse is
# visible before anything is judged.
# ---------------------------------------------------------------------------------------
function Get-NamedColumns([string]$riderText) {
    $m = [regex]::Match($riderText, '(?i)\bcolumns?\b')
    if (-not $m.Success) { return @() }
    $head = $riderText.Substring(0, $m.Index)
    return @([regex]::Matches($head, '`([A-Za-z][A-Za-z0-9_]*)`') | ForEach-Object { $_.Groups[1].Value })
}
foreach ($r in $travelling) {
    $named = @()
    if ($r.LandsIn.Trim() -ieq 'header') { $named = @(Get-NamedColumns $r.Rider) }
    $r | Add-Member -NotePropertyName NamedColumns -NotePropertyValue $named
    $r | Add-Member -NotePropertyName DecidedBy -NotePropertyValue $(if ($named.Count -gt 0) { 'CODE' } else { 'JEV' })
}
$codeRiders = @($travelling | Where-Object { $_.DecidedBy -eq 'CODE' })
$jevRiders  = @($travelling | Where-Object { $_.DecidedBy -eq 'JEV' })
"RIDERS_DECIDED_BY_CODE=$($codeRiders.Count)"
"RIDERS_JUDGED_BY_JEV=$($jevRiders.Count)"

# REVISION 1 (finding 2d): protocol step 2 -- print the judged candidates, with no
# judgments, so the seat labels exactly what will be judged, keyed by the IDs shown.
"RIDER_CANDIDATES (no judgments, for baseline labelling -- key each by the ID shown):"
foreach ($r in $travelling) {
    $how = if ($r.DecidedBy -eq 'CODE') { "CODE checks [$($r.NamedColumns -join ', ')] against the proposed header" } else { 'JEV judges it (no column named before the word "column")' }
    "  $($r.Id)  lands_in=$($r.LandsIn)  -> $how"
}

# REVISION 1 (finding 2c): a run against a header that adds nothing is not a rotation.
# Without this, a run made before the rotation -- to "try the tool" -- judges every rider
# against the unrotated header and spends the one-shot first run on a meaningless window.
if ($addedCols.Count -eq 0) {
    "EXIT_REASON=NO_ROTATION"
    "COLUMNS_ADDED=0 -- -AfterFile '$AfterFile' adds no column over -BeforeRev '$BeforeRev'. Nothing was judged. Point -AfterFile at the PROPOSED rotated AnalysisLogger.vb."
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 4.4 (D-4): refuse to call the API without an operator-written baseline.
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each RIDER_CANDIDATES item above BEFORE running this tool, as JSON, one key per rider ID exactly as printed, values one of arrived|missing|not_a_header_column|ambiguous|unsure. Example:"
    '{ "RIDER-1": "not_a_header_column", "RIDER-3": "arrived", "RIDER-4": "missing" }'
    "The whole point of the first run is comparing an independent human read against Jev's; looking first makes that comparison worthless permanently."
    exit 2
}
$baselineRaw = Get-Content -Raw -Path $baselineFull | ConvertFrom-Json
$baseline = @{}
if ($null -ne $baselineRaw) {
    foreach ($p in $baselineRaw.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}

# REVISION 1 (finding 2b): ITEM-level refusal (the fixture-parser FP-D24 pattern). Every
# travelling rider needs a baseline line -- code-decided ones too, because the baseline is
# compared against them. A rotation commit edits the ledger, so a rider can appear between
# the seat's read and the run.
$unbaselined = @($travelling | Where-Object { -not $baseline.ContainsKey($_.Id) })
if ($unbaselined.Count -gt 0 -and -not $AllowUnbaselinedItems) {
    "EXIT_REASON=BASELINE_INCOMPLETE"
    "UNBASELINED_ITEMS=$($unbaselined.Count) -- no line in '$BaselinePath' for:"
    foreach ($u in $unbaselined) { "  $($u.Id)" }
    "Nothing was judged. Add your own read for each rider above. For a routine re-run of riders measured once, pass -AllowUnbaselinedItems."
    exit 2
}

$apiKey = $env:TYPESAFE_API_KEY
if ($jevRiders.Count -gt 0 -and [string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set in the environment. Load typesafe.local.env first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

$verdictCriteria = @{
    arrived              = 'This rider describes one or more `analysis_log.csv` columns, and every column it describes is present in `all_columns_after`.'
    missing              = 'This rider describes one or more `analysis_log.csv` columns, and at least one of them is absent from `all_columns_after`.'
    not_a_header_column  = 'This rider lands somewhere other than the `analysis_log.csv` header (a code behaviour, an ops script, a document) - column presence in `all_columns_after` does not apply.'
    ambiguous            = "The rider's description does not name its column(s) precisely enough to decide arrived vs missing against `all_columns_after`."
}

function Invoke-RiderVerdict([string]$apiKeyIn, $r, [string]$uid) {
    $state = @{
        rider = @{
            id          = $r.Id
            description = $r.Rider
            lands_in    = $r.LandsIn
            status      = $r.Status
        }
        columns_added_by_this_rotation = @($addedCols)
        all_columns_after              = @($afterCols)
        # REVISION 1 (finding 2a): a fresh uid per sample so the samples are independent
        # draws (docs/harness-shadow-mode-protocol.md section 4b).
        sample_uid                     = $uid
    }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            lands_in_header = @{
                type = 'noul'
                instructions = 'Does this rider describe one or more columns in the `analysis_log.csv` header, as opposed to a code behaviour, an ops script, or a document?'
            }
            column_present = @{
                type = 'noul'
                instructions = "Does a column matching the rider's description appear in `all_columns_after`?"
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify this rider's arrival status against `all_columns_after`, using `rider.description` and `rider.lands_in` to decide what it describes."
                criteria = $verdictCriteria
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body 3 1 $TestTransportOverride
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked; RetryCount = $call.RetryCount } }
    $ans = $call.Response.answers
    # --- TRAP 1 (docs/rider-travel-check-spec.md §0): the verdict comes from the `verdict`
    # Choice answer ALONE. The two Noul answers are diagnostics ONLY -- never combined.
    $verdict = $ans.verdict.choice
    $top = $null
    if ($ans.verdict.probabilities) {
        $vals = @($ans.verdict.probabilities.PSObject.Properties | ForEach-Object { [double]$_.Value })
        if ($vals.Count -gt 0) { $top = ($vals | Measure-Object -Maximum).Maximum }
    }
    $usageIn = 0
    if ($call.Response.usage -and $call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
    return @{ Ok = $true; Verdict = $verdict; TopProbability = $top; LandsInHeaderNoul = $ans.lands_in_header.noul; ColumnPresentNoul = $ans.column_present.noul; UsageIn = $usageIn; RetryCount = $call.RetryCount }
}

# ---------------------------------------------------------------------------------------
# Step 6 (§4.2, §5). CODE riders: exact membership. JEV riders: $Samples draws each
# (REVISION 1, finding 2a); plurality, agreement rate and top probability on every row; a
# split vote is UNSTABLE and never auto-resolved. D-5: any API failure fails loud.
# ---------------------------------------------------------------------------------------
$results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''
$usageInputTokens = 0
# REVISION 2 (finding 11): retries actually spent across every Jev call this run makes,
# summed the same way USAGE_INPUT_TOKENS already is -- see tools/checks/lib/InvokeJev.ps1.
$usageRetries = 0

foreach ($r in $codeRiders) {
    $absent = @($r.NamedColumns | Where-Object { $afterCols -notcontains $_ })
    $results.Add([PSCustomObject]@{
        Id = $r.Id; DecidedBy = 'CODE'
        Verdict = $(if ($absent.Count -eq 0) { 'arrived' } else { 'missing' })
        Detail = $(if ($absent.Count -eq 0) { "all of [$($r.NamedColumns -join ', ')] present" } else { "absent: [$($absent -join ', ')]" })
        AgreementRate = $null; Stable = $true; MeanTopProbability = $null; SampleVerdicts = ''
        LandsInHeaderNoul = $null; ColumnPresentNoul = $null
        Baseline = if ($baseline.ContainsKey($r.Id)) { $baseline[$r.Id] } else { $null }
    })
}

$ridersWafBlocked = 0
foreach ($r in $jevRiders) {
    $draws = New-Object System.Collections.Generic.List[object]
    $itemBlocked = $false
    for ($i = 0; $i -lt $Samples; $i++) {
        $uid = "$($r.Id):${i}:$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $s = Invoke-RiderVerdict $apiKey $r $uid
        if (-not $s.Ok) {
            # REVISION 2 (item 8n, docs/jev-harnesses-adversarial-review-2026-09-22.md finding
            # 6's sibling): a web-firewall block is about THIS rider's text, not the API. Record
            # the rider as unjudgeable and move on to the next one; any other failure still
            # aborts the whole run (unchanged).
            if ($s.WafBlocked) { $itemBlocked = $true; $usageRetries += $s.RetryCount; break }
            $apiFailed = $true; $apiFailMsg = "Jev request failed for $($r.Id) (sample $($i+1)/$Samples): $($s.Error)"; break
        }
        $usageInputTokens += $s.UsageIn
        $usageRetries += $s.RetryCount
        $draws.Add($s)
    }
    if ($apiFailed) { break }
    if ($itemBlocked) {
        $ridersWafBlocked++
        $results.Add([PSCustomObject]@{
            Id = $r.Id; DecidedBy = 'JEV'; Verdict = 'WAF_BLOCKED'; Detail = ''
            AgreementRate = $null; Stable = $true; MeanTopProbability = $null; SampleVerdicts = ''
            LandsInHeaderNoul = $null; ColumnPresentNoul = $null
            Baseline = if ($baseline.ContainsKey($r.Id)) { $baseline[$r.Id] } else { $null }
        })
        continue
    }
    $counts = @{}
    foreach ($s in $draws) { if (-not $counts.ContainsKey($s.Verdict)) { $counts[$s.Verdict] = 0 }; $counts[$s.Verdict]++ }
    $plurality = $null; $pc = -1
    foreach ($s in $draws) { if ($counts[$s.Verdict] -gt $pc) { $pc = $counts[$s.Verdict]; $plurality = $s.Verdict } }
    $rate = [math]::Round($pc / $draws.Count, 3)
    $tops = @($draws | Where-Object { $null -ne $_.TopProbability } | ForEach-Object { $_.TopProbability })
    $results.Add([PSCustomObject]@{
        Id = $r.Id; DecidedBy = 'JEV'; Verdict = $plurality; Detail = ''
        AgreementRate = $rate; Stable = ($rate -ge 1.0)
        MeanTopProbability = $(if ($tops.Count -gt 0) { [math]::Round((($tops | Measure-Object -Average).Average), 3) } else { $null })
        SampleVerdicts = (@($draws | ForEach-Object { $_.Verdict }) -join ',')
        LandsInHeaderNoul = [math]::Round((($draws | ForEach-Object { [double]$_.LandsInHeaderNoul } | Measure-Object -Average).Average), 3)
        ColumnPresentNoul = [math]::Round((($draws | ForEach-Object { [double]$_.ColumnPresentNoul } | Measure-Object -Average).Average), 3)
        Baseline = if ($baseline.ContainsKey($r.Id)) { $baseline[$r.Id] } else { $null }
    })
}

if ($apiFailed) {
    Write-Coverage $ridersInLedger $ridersTravelling $results.Count $beforeCols.Count $afterCols.Count $addedCols.Count $removedCols.Count
    "RETRY_COUNT=$usageRetries"
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 7 (§4.2, §4.6): emit the report and decide the exit code from `verdict` alone.
# ---------------------------------------------------------------------------------------
Write-Coverage $ridersInLedger $ridersTravelling $results.Count $beforeCols.Count $afterCols.Count $addedCols.Count $removedCols.Count
"USAGE_INPUT_TOKENS=$usageInputTokens"
"RETRY_COUNT=$usageRetries"
"RIDERS_WAF_BLOCKED=$ridersWafBlocked"

# REVISION 2 (finding 6, docs/jev-harnesses-adversarial-review-2026-09-22.md: "an OK-class
# misread passes the exit code"). not_a_header_column is a LEGITIMATE, common verdict --
# RIDER-1 and RIDER-2 in the current ledger are correctly not_a_header_column (they land in
# code / an ops doc, never the CSV header) -- so it must NOT flip the exit code the way
# FP-D25 did for fixture-parser's shipped_declared_ok (that class is provably always wrong
# on a hardcoded-literal site; this one is not provably anything). Reportable-only, the
# fixture-parser FP-D22/8j shape (a code-computed counter with zero exit-code effect), never
# folded into $anyBad below. docs/harness-shadow-mode-protocol.md section 4g: "a stable OK
# is not evidence" -- this print is what lets a reader see how many stable OKs there were,
# instead of them disappearing into an undifferentiated pass.
$notAHeaderColumn = @($results | Where-Object { $_.Verdict -eq 'not_a_header_column' }).Count
$notAHeaderColumnStable = @($results | Where-Object { $_.Verdict -eq 'not_a_header_column' -and $_.Stable }).Count
"RIDERS_NOT_A_HEADER_COLUMN=$notAHeaderColumn (of which STABLE=$notAHeaderColumnStable -- reported per docs/harness-shadow-mode-protocol.md section 4g; a stable OK is not evidence, not folded into the exit code)"

function Get-AgreeText($res) {
    if ($res.Verdict -eq 'WAF_BLOCKED') { 'NOT_JUDGED' }
    elseif ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
    elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
    elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
    else { 'DISAGREE' }
}

"PER_RIDER_RESULTS:"
foreach ($res in $results) {
    $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
    if ($res.DecidedBy -eq 'CODE') {
        "  $($res.Id) [CODE] verdict=$($res.Verdict) ($($res.Detail)) baseline=$($res.Baseline) [$(Get-AgreeText $res)]"
    } else {
        "  $($res.Id) [JEV $stableTxt] verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) verdicts=[$($res.SampleVerdicts)] baseline=$($res.Baseline) [$(Get-AgreeText $res)] lands_in_header_noul=$($res.LandsInHeaderNoul) column_present_noul=$($res.ColumnPresentNoul)"
    }
}

# Write the markdown report (§4.1 -OutPath: "where the comparison lands").
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Rider-travel check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/rider-travel.ps1`` against ``-BeforeRev $BeforeRev`` / ``-AfterFile $AfterFile``, -Samples $Samples.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| RIDERS_IN_LEDGER | $ridersInLedger |")
$reportLines.Add("| RIDERS_TRAVELLING | $ridersTravelling |")
$reportLines.Add("| RIDERS_DECIDED_BY_CODE | $($codeRiders.Count) |")
$reportLines.Add("| RIDERS_JUDGED_BY_JEV | $($jevRiders.Count) |")
$reportLines.Add("| HEADER_COLUMNS_BEFORE | $($beforeCols.Count) |")
$reportLines.Add("| HEADER_COLUMNS_AFTER | $($afterCols.Count) |")
$reportLines.Add("| COLUMNS_ADDED | $($addedCols.Count) |")
$reportLines.Add("| COLUMNS_REMOVED | $($removedCols.Count) |")
$reportLines.Add("| USAGE_INPUT_TOKENS | $usageInputTokens |")
$reportLines.Add("| RETRY_COUNT | $usageRetries |")
$reportLines.Add("| RIDERS_WAF_BLOCKED | $ridersWafBlocked |")
$reportLines.Add("| RIDERS_NOT_A_HEADER_COLUMN | $notAHeaderColumn (STABLE=$notAHeaderColumnStable) |")
$reportLines.Add('')
if ($addedCols.Count -gt 0) { $reportLines.Add('**Added:** ' + ($addedCols -join ', ')); $reportLines.Add('') }
if ($removedCols.Count -gt 0) { $reportLines.Add('**Removed:** ' + ($removedCols -join ', ')); $reportLines.Add('') }
$reportLines.Add('## Per-rider verdicts')
$reportLines.Add('')
$reportLines.Add('| ID | Decided by | Verdict | Detail / sample verdicts | Agreement rate | Mean top prob | Baseline | Agreement |')
$reportLines.Add('|---|---|---|---|---|---|---|---|')
foreach ($res in $results) {
    $d = if ($res.DecidedBy -eq 'CODE') { $res.Detail } else { $res.SampleVerdicts }
    $reportLines.Add("| $($res.Id) | $($res.DecidedBy) | $($res.Verdict) | $d | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.Baseline) | $(Get-AgreeText $res) |")
}
if ($others.Count -gt 0) {
    $reportLines.Add('')
    $reportLines.Add('## Non-TRAVELLING rows (reported, not judged)')
    $reportLines.Add('')
    $reportLines.Add('| ID | Status |')
    $reportLines.Add('|---|---|')
    foreach ($o in $others) { $reportLines.Add("| $($o.Id) | $($o.Status) |") }
}
$reportFull = Resolve-RepoPath $OutPath
Set-Content -Encoding UTF8 -Path $reportFull -Value ($reportLines -join "`r`n")
"Report written to $OutPath"

# Exit 1 on missing / ambiguous, on any UNSTABLE Jev row (REVISION 1) -- a split vote is
# unresolved and must not read as a pass -- and (REVISION 2, item 8n) on any WAF_BLOCKED
# rider: an unjudged item is never a pass either. not_a_header_column is deliberately
# EXCLUDED from this list (see RIDERS_NOT_A_HEADER_COLUMN above) -- it is a legitimate,
# common verdict, and folding it in here would make a normal run (most rotations carry a
# rider or two that never touches the header) exit 1 by construction, which is not a
# meaningful signal.
$anyBad = @($results | Where-Object { $_.Verdict -eq 'missing' -or $_.Verdict -eq 'ambiguous' -or $_.Verdict -eq 'WAF_BLOCKED' -or -not $_.Stable }).Count -gt 0
if ($anyBad) { exit 1 } else { exit 0 }
