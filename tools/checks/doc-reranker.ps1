#requires -Version 5.1
<#
  tools/checks/doc-reranker.ps1 -- harness 5 of the Jev programme: "where was this
  decided?" -- rank doc sections against a query.

  Spec: docs/doc-reranker-measurement-plan.md. Build record: docs/doc-reranker-build-spec-back.md.
  M2 measured BM25 as the better shortlist method (22/26 vs grep's 21/26 at k=30,
  17/26 vs 12/26 at k=10, over the 26 found-kind queries in the question set) --
  see tools/checks/measure/doc-reranker/measure_shortlist.py. This tool therefore
  builds its shortlist with BM25 only.

  ADVISORY ONLY (measurement plan section 3). Never wired into verify-gate.ps1 or
  the pre-push hook. It returns a ranked list; the seat reads the docs.

  THE SPLIT: CODE (Python, tools/checks/lib/doc_sections.py + doc_reranker_shortlist.py)
  builds the shortlist and later scores hit@k for the acceptance measurement. JEV judges
  ONE question per (query, section) pair -- "does this section answer the query?" -- plus
  a separate no-answer question over the top 5. CODE sorts by noul and decides date
  ordering; Jev never sees or compares dates (measurement plan section 3: "dates are
  text to Jev").

  DATE TIE-BREAK: "code sorts by noul... where the top candidates are rulings, code
  orders them newest first by commit date" (build brief section 2 step 4). Implemented
  as: sort by mean noul descending; WITHIN each 0.01-wide noul bucket (near-equal
  relevance -- the case where several candidates are all plausible rulings on the same
  question), break the tie by commit date descending (newest first). A single clear
  winner is never reordered by date.

  ACCEPTANCE / RESERVED (harness-shadow-mode-protocol.md section 4a): a judged RESERVED
  query is spent for good. -AcceptanceRun (default -Subset acceptance) refuses to run unless
  the question set names exactly 8 acceptance-subset queries, and reads only that subset.
  -Subset reserved (added 474c2e1) is the seat's measured run. Since 2026-09-24 (second
  reader) it is gated by the tracked ledger docs/harness-runs/doc-reranker-spent-queries.json:
  it refuses if any selected id is already there (RESERVED_QUERY_SPENT, exit 2, before the key
  is read) and records each id there before that id's first Jev call. -Query is unrestricted,
  but it warns loudly (WARNING_RESERVED_QUERY=) when its text is a reserved query verbatim,
  and records that id as spent.

  SAMPLING: the acceptance measurement samples the per-(query,candidate) Noul 5 times
  (self-consistency, harness-shadow-mode-protocol.md section 4b -- every harness in this
  programme samples N times and reports an agreement rate). A live -Query run samples
  ONCE by default and PRINTS that it did, per the build brief.

  LOGGING: every LIVE (-Query) run appends one line to a gitignored JSONL file
  (default doc-reranker-query-log.jsonl at repo root) -- query, top 5, timestamp -- for
  the on-the-go trial (measurement plan section 4). The acceptance run does NOT append
  here; it writes its own tracked report.

  USAGE (Git Bash; the key is loaded only for a keyed run):
    set -a; . ./typesafe.local.env; set +a
    powershell -NoProfile -File tools/checks/doc-reranker.ps1 -Query "<question>" [-Rev <rev>] [-K 30] [-Samples 1]
    powershell -NoProfile -File tools/checks/doc-reranker.ps1 -AcceptanceRun [-Rev <rev>] [-OutPath <file>]
    powershell -NoProfile -File tools/checks/doc-reranker.ps1 -NoAnswerOnly Q29 -Rev af5d6af -Samples 5 `
        [-AllowSpentOnce Q29 -Reason "<why>"]   # SR-D2: needed only when the id is already spent
#>
[CmdletBinding()]
param(
    [string]$Query,
    [switch]$AcceptanceRun,
    [string]$Rev = 'HEAD',
    [int]$K = 30,
    [int]$Samples = -1,
    [int]$NoAnswerTopN = 5,
    [string]$LogPath = 'doc-reranker-query-log.jsonl',
    [string]$OutPath = '',
    [string]$QueryFile = 'docs/harness-runs/doc-reranker-20260923T1930Z-queries.json',
    [string]$Python = 'python',
    [ValidateSet('acceptance', 'reserved')][string]$Subset = 'acceptance',
    # Comma-separated values are split: `powershell -File` passes `-ExcludeIds Q11,Q12` as ONE
    # string, which before 2026-09-24 excluded nothing.
    [string[]]$ExcludeIds = @(),
    # The tracked SPENT-QUERY LEDGER (second reader, 2026-09-24 UTC). A reserved query judged
    # once is spent for good (harness-shadow-mode-protocol.md section 4a). -Subset reserved
    # refuses any spent id, and appends each id it judges BEFORE its first Jev call.
    [string]$SpentLedger = 'docs/harness-runs/doc-reranker-spent-queries.json',
    # SR-D2 (docs/jev-harnesses-second-reader-2026-09-24.md section 6): a third mode. Runs
    # ONLY the no-answer Noul for one query id, over a freshly rebuilt re-ranked top 5 (the
    # id's own section Nouls are re-scored to reconstruct that order -- about K x -Samples
    # calls -- then ONE more call is the actual new measurement). If the id is already in
    # $SpentLedger (Q29's own case: its section Nouls were judged, its no-answer check never
    # was), this refuses unless -AllowSpentOnce names the SAME id with a -Reason, an
    # explicit, logged, one-time exception recorded in the ledger's own "exceptions" list --
    # never silently re-judged, and never a second entry in "spent".
    [string]$NoAnswerOnly = '',
    [string]$AllowSpentOnce = '',
    [string]$Reason = '',
    # TEST SEAM ONLY, the pattern harnesses 1-4 carry: forwarded to Invoke-Jev's
    # -TransportOverride. A real run never passes it.
    [scriptblock]$TestTransportOverride = $null
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:PYTHONIOENCODING = 'utf-8'
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

$ExcludeIds = @($ExcludeIds | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })

# ---- The spent-query ledger. Read and written as UTF-8 without a BOM. ----
function Read-SpentLedger {
    $full = Resolve-RepoPath $SpentLedger
    if (-not (Test-Path $full)) { return $null }
    $obj = [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    $ids = @{}
    foreach ($e in @($obj.spent)) { if ($null -ne $e -and $e.id) { $ids[[string]$e.id] = $e } }
    return $ids
}
function Add-SpentLedgerEntry([string]$id, [string]$how) {
    $full = Resolve-RepoPath $SpentLedger
    $obj = [PSCustomObject]@{ _note = 'Reserved doc-reranker queries already judged by Jev. Never re-judge one.'; spent = @() }
    if (Test-Path $full) { $obj = [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8) | ConvertFrom-Json }
    $list = New-Object System.Collections.Generic.List[object]
    foreach ($e in @($obj.spent)) { if ($null -ne $e) { $list.Add($e) } }
    if (@($list | Where-Object { $_.id -eq $id }).Count -gt 0) { return }
    $list.Add([PSCustomObject]([ordered]@{ id = $id; spent_utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd'); by = $how }))
    $out = [ordered]@{ _note = [string]$obj._note; spent = $list.ToArray() }
    [System.IO.File]::WriteAllText($full, ((ConvertTo-Json -InputObject $out -Depth 5) + "`n"), (New-Object System.Text.UTF8Encoding($false)))
}
function ConvertTo-QueryKey([string]$t) { (($t -replace '\s+', ' ').Trim()).ToLowerInvariant() }
# SR-D2: the one-time, logged exception. Separate "exceptions" array -- "spent" keeps its
# one-line-per-id, never-duplicated shape; this never touches or duplicates that entry.
function Add-SpentLedgerException([string]$id, [string]$reason) {
    $full = Resolve-RepoPath $SpentLedger
    $obj = [PSCustomObject]@{ _note = 'Reserved doc-reranker queries already judged by Jev. Never re-judge one.'; spent = @() }
    if (Test-Path $full) { $obj = [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8) | ConvertFrom-Json }
    $spentList = New-Object System.Collections.Generic.List[object]
    foreach ($e in @($obj.spent)) { if ($null -ne $e) { $spentList.Add($e) } }
    $excList = New-Object System.Collections.Generic.List[object]
    if ($obj.PSObject.Properties.Name -contains 'exceptions') { foreach ($e in @($obj.exceptions)) { if ($null -ne $e) { $excList.Add($e) } } }
    $excList.Add([PSCustomObject]([ordered]@{
        id = $id
        utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        reason = $reason
        what = 'no-answer Noul only, over a freshly rebuilt re-ranked top 5 (SR-D2) -- the section Nouls used to rebuild that order were already spent for this id; this call is the one measurement that id never got, never a re-judge of the reserved population''s own scored rows'
    }))
    $out = [ordered]@{ _note = [string]$obj._note; spent = $spentList.ToArray(); exceptions = $excList.ToArray() }
    [System.IO.File]::WriteAllText($full, ((ConvertTo-Json -InputObject $out -Depth 5) + "`n"), (New-Object System.Text.UTF8Encoding($false)))
}

$modeCount = 0
if ($Query) { $modeCount++ }
if ($AcceptanceRun) { $modeCount++ }
if ($NoAnswerOnly) { $modeCount++ }
if ($modeCount -eq 0) {
    Write-Host "EXIT_REASON=NO_MODE"
    Write-Host "Pass -Query '<question>' for a live single query, -AcceptanceRun for the M4 acceptance measurement (8 acceptance-subset queries only), or -NoAnswerOnly <id> for SR-D2's one-query no-answer-check-alone mode."
    exit 2
}
if ($modeCount -gt 1) {
    Write-Host "EXIT_REASON=AMBIGUOUS_MODE"
    Write-Host "-Query, -AcceptanceRun and -NoAnswerOnly are mutually exclusive."
    exit 2
}

# ---- -Query: warn LOUDLY when the text is a reserved query verbatim (case and whitespace
# folded). All code, before the key is read, so it is provable with no key.
$reservedMatch = $null
if ($Query) {
    $qfFullW = Resolve-RepoPath $QueryFile
    if (Test-Path $qfFullW) {
        $qsetW = Get-Content -Raw -Encoding UTF8 -Path $qfFullW | ConvertFrom-Json
        $qk = ConvertTo-QueryKey $Query
        $reservedMatch = @($qsetW.queries | Where-Object { $_.subset -eq 'reserved' -and (ConvertTo-QueryKey $_.query) -eq $qk }) | Select-Object -First 1
    }
    if ($reservedMatch) {
        $ledgerW = Read-SpentLedger
        $wasSpent = ($null -ne $ledgerW) -and $ledgerW.ContainsKey([string]$reservedMatch.id)
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        Write-Host "WARNING_RESERVED_QUERY=$($reservedMatch.id) already_spent=$wasSpent"
        Write-Host "  This -Query text is reserved query $($reservedMatch.id) of $QueryFile, verbatim."
        Write-Host "  Judging it spends it for good (harness-shadow-mode-protocol.md section 4a)."
        if (-not $wasSpent) { Write-Host "  It is NOT yet in $SpentLedger. This run records it there before the first Jev call." }
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    }
}

# ---- -AcceptanceRun: every gate runs here, before the key is read.
$acceptance = @()
if ($AcceptanceRun) {
    $qfFull = Resolve-RepoPath $QueryFile
    $qset = Get-Content -Raw -Encoding UTF8 -Path $qfFull | ConvertFrom-Json
    if ($Subset -eq 'reserved') {
        # -Subset reserved: the SEAT's measured run (doc-reranker-measurement-plan.md step M4).
        $ledger = Read-SpentLedger
        if ($null -eq $ledger) {
            Write-Host "EXIT_REASON=SPENT_LEDGER_MISSING"
            Write-Host "No spent-query ledger at '$SpentLedger'. A reserved run cannot prove it re-judges nothing. Nothing was judged."
            exit 2
        }
        $acceptance = @($qset.queries | Where-Object { $_.subset -eq 'reserved' -and $ExcludeIds -notcontains $_.id })
        $spentSel = @($acceptance | Where-Object { $ledger.ContainsKey([string]$_.id) } | ForEach-Object { [string]$_.id })
        Write-Host "SUBSET=reserved  QUERIES=$($acceptance.Count)  EXCLUDED=$($ExcludeIds -join ',')  SPENT_IN_SELECTION=$($spentSel.Count)"
        if ($spentSel.Count -gt 0) {
            Write-Host "EXIT_REASON=RESERVED_QUERY_SPENT"
            Write-Host "These reserved ids are already in '$SpentLedger' and can never be judged again: $($spentSel -join ',')"
            Write-Host "Nothing was judged. Pass them in -ExcludeIds to run the rest."
            exit 2
        }
        if ($acceptance.Count -eq 0) {
            Write-Host "EXIT_REASON=NO_QUERIES"
            Write-Host "No unspent reserved query is selected. Nothing was judged."
            exit 2
        }
    } else {
        $acceptance = @($qset.queries | Where-Object { $_.subset -eq 'acceptance' })
        if ($acceptance.Count -ne 8) {
            Write-Host "EXIT_REASON=ACCEPTANCE_SET_UNEXPECTED"
            Write-Host "Expected 8 acceptance-subset queries in $QueryFile, found $($acceptance.Count). Refusing -- this run's numbers are specced against exactly 8."
            exit 2
        }
    }
}

# ---- -NoAnswerOnly (SR-D2): every gate runs here too, before the key is read.
$noAnswerOnlyQuery = $null
if ($NoAnswerOnly) {
    $qfFullN = Resolve-RepoPath $QueryFile
    $qsetN = Get-Content -Raw -Encoding UTF8 -Path $qfFullN | ConvertFrom-Json
    $noAnswerOnlyQuery = @($qsetN.queries | Where-Object { [string]$_.id -eq $NoAnswerOnly }) | Select-Object -First 1
    if (-not $noAnswerOnlyQuery) {
        Write-Host "EXIT_REASON=QUERY_ID_NOT_FOUND"
        Write-Host "'$NoAnswerOnly' names no query in '$QueryFile'. Nothing was judged."
        exit 2
    }
    $ledgerN = Read-SpentLedger
    $noAnswerOnlySpent = ($null -ne $ledgerN) -and $ledgerN.ContainsKey($NoAnswerOnly)
    if ($noAnswerOnlySpent -and (($AllowSpentOnce -ne $NoAnswerOnly) -or [string]::IsNullOrWhiteSpace($Reason))) {
        Write-Host "EXIT_REASON=RESERVED_QUERY_SPENT"
        Write-Host "'$NoAnswerOnly' is already in '$SpentLedger'. Pass -AllowSpentOnce $NoAnswerOnly -Reason '<why>' for a one-time, logged exception (SR-D2). Nothing was judged."
        exit 2
    }
    if ($AllowSpentOnce -and -not $noAnswerOnlySpent) {
        Write-Host "EXIT_REASON=ALLOW_SPENT_ONCE_UNUSED"
        Write-Host "-AllowSpentOnce was passed but '$NoAnswerOnly' is not in '$SpentLedger' -- an ordinary run records it there as usual, no exception needed. Nothing was judged; drop -AllowSpentOnce/-Reason and re-run."
        exit 2
    }
}

$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "EXIT_REASON=API_FAILED"
    Write-Host "TYPESAFE_API_KEY is not set. Load it first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}
if ($Query -and $reservedMatch) { Add-SpentLedgerEntry ([string]$reservedMatch.id) "-Query verbatim match, $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))" }

$answerCriteria = [ordered]@{
    true  = 'The section states, describes, or directly implies the specific fact, ruling, value, or reason the query asks about -- a reader who reads only this section would learn the answer.'
    false = 'The section is on a related topic but does not state the specific answer, or is unrelated.'
}
$noAnswerCriteria = [ordered]@{
    true  = 'At least one of the listed candidate sections states, describes, or directly implies the specific answer to the query.'
    false = 'None of the listed candidate sections answers the query -- they are on related topics at best, or unrelated.'
}

function Invoke-SectionNoul([string]$key, [string]$q, $cand, [int]$samples, [switch]$DebugState) {
    $draws = New-Object System.Collections.Generic.List[object]
    $inTok = [long]0
    for ($i = 0; $i -lt $samples; $i++) {
        $uid = "$($cand.id):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $state = [ordered]@{
            query = $q; doc_path = $cand.path; heading_chain = $cand.heading_chain
            section_text = $cand.text; is_archive = [bool]$cand.is_archive; sample_uid = $uid
        }
        if ($DebugState) { Write-Host "STATE_DEBUG $($cand.id) keys=[$($state.Keys -join ',')] sample_uid=$uid" }
        # Noul, not Choice: docs.typesafe.ai/primitives/noul.md -- a yes/no question returns a
        # single probability directly under .noul, not a labelled .choice + .probabilities.
        $qdef = @{ type = 'noul'; criteria = $answerCriteria
                   instructions = 'Read the query and this one document section. Does this section answer the query -- does it state the specific fact, ruling, value or reason asked about?' }
        $call = Invoke-Jev $key @{ model = 'jev-latest'; state = $state; questions = @{ answers = $qdef } } 3 1 $TestTransportOverride
        if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked } }
        $v = $call.Response.answers.answers
        $noul = [double]$v.noul
        if ($call.Response.usage -and $call.Response.usage.input_tokens) { $inTok += [long]$call.Response.usage.input_tokens }
        $draws.Add($noul)
    }
    $mean = ($draws | Measure-Object -Average).Average
    # self-consistency: STABLE means every draw is on the same side of 0.5 (agrees on
    # yes/no), matching this repo's other harnesses' agreement-rate convention where the
    # underlying answer is a choice; here it is a continuous noul, so "agreement" is
    # "no draw crossed the decision boundary the others didn't."
    $sides = @($draws | ForEach-Object { $_ -ge 0.5 })
    # @() wrap is load-bearing: a bare (pipe).Count is $null, not 1, when exactly one item
    # survives Where-Object -- the single-element-array-unwraps trap (CLAUDE.md, PS 5.1
    # notes), reproduced live in Pct() below before this fix.
    $agreeCount = (@($sides | Where-Object { $_ -eq $sides[0] })).Count
    # [double] cast is load-bearing, not style: when agreeCount/draws.Count divides evenly
    # (e.g. 1/1, 5/5) PowerShell's / operator returns an Int32, and [math]::Round has no
    # (Int32, Int32) overload -- an evenly-dividing sample count throws "Argument types do
    # not match" from the .NET method-overload binder. Reproduced with -Samples 1 (the live
    # default): agreeCount=1, draws.Count=1, 1/1 -> Int32 1 -> Round fails.
    $agreement = [math]::Round([double]$agreeCount / $draws.Count, 3)
    # .ToArray(), not @($draws): wrapping a System.Collections.Generic.List[object] in the
    # @() array-subexpression operator throws "Argument types do not match" in this PS 5.1
    # host, reproduced down to a bare 1- or 2-element List[object] (a same-typed
    # List[double] is unaffected -- isolated with a standalone repro before this fix).
    # @() guards against a PS ARRAY unwrapping to a scalar; a .NET List never does that, so
    # the guard was both unnecessary and, here, actively broken.
    return @{ Ok = $true; MeanNoul = $mean; Agreement = $agreement; Draws = $draws.ToArray(); InputTokens = $inTok; Calls = $samples }
}

function Get-RerankedOrder($candidates, $meanNouls) {
    # sort by mean noul desc; within a 0.01-wide bucket, break ties by commit date desc.
    $withScore = @(for ($i = 0; $i -lt $candidates.Count; $i++) {
        [PSCustomObject]@{ Cand = $candidates[$i]; Noul = $meanNouls[$i]; Bucket = [math]::Round($meanNouls[$i], 2) }
    })
    @($withScore | Sort-Object -Property @{Expression = 'Bucket'; Descending = $true}, @{Expression = { $_.Cand.commit_date_epoch }; Descending = $true})
}

function ConvertTo-RankedJsonFile($orderedCands, [string]$path) {
    $ranked = @($orderedCands | ForEach-Object {
        [ordered]@{ id = $_.id; path = $_.path; heading_chain = $_.heading_chain; text = $_.text
                    start_line = $_.start_line; end_line = $_.end_line }
    })
    (ConvertTo-Json -InputObject $ranked -Depth 6) | Set-Content -Encoding UTF8 -Path $path
}

# =================================================================================================
# MODE 1: -Query, a live single free-text query.
# =================================================================================================
if ($Query) {
    if ($Samples -lt 1) { $Samples = 1 }
    $slFile = [System.IO.Path]::GetTempFileName()
    try {
        & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') shortlist --rev $Rev --query $Query --k $K --out $slFile | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SHORTLIST_FAILED"; exit 2 }
        $sl = Get-Content -Raw -Encoding UTF8 -Path $slFile | ConvertFrom-Json
    } finally { Remove-Item -Force $slFile -ErrorAction SilentlyContinue }

    Write-Host "REV=$($sl.rev7)  DOC_SCOPE=$($sl.doc_scope_count) docs  SECTIONS=$($sl.section_count)  SHORTLIST_K=$($sl.shortlist.Count)"
    if ($Samples -eq 1) { Write-Host "SAMPLED_ONCE=true (live mode; acceptance measurement samples 5x)" }

    $cands = @($sl.shortlist)
    $means = New-Object System.Collections.Generic.List[double]
    $jevCalls = 0; $usageIn = [long]0; $sectionsWaf = 0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    foreach ($c in $cands) {
        $r = Invoke-SectionNoul $apiKey $Query $c $Samples
        if (-not $r.Ok) {
            # A blocked section ranks LAST (noul -1) and is COUNTED -- it was silent before 2026-09-24.
            if ($r.WafBlocked) { $means.Add(-1.0); $sectionsWaf++; continue }
            Write-Host "EXIT_REASON=API_FAILED"; Write-Host "Jev request failed for $($c.id): $($r.Error)"; exit 2
        }
        $means.Add($r.MeanNoul); $jevCalls += $r.Calls; $usageIn += $r.InputTokens
    }
    $ordered = @(Get-RerankedOrder $cands $means)
    $top5 = @($ordered | Select-Object -First $NoAnswerTopN)

    # no-answer Noul over the top N (by the RE-RANKED order, the tool's final say).
    $naState = [ordered]@{ query = $Query; sample_uid = [guid]::NewGuid().ToString('N').Substring(0,8) }
    $i = 0
    foreach ($t in $top5) { $i++; $naState["candidate_$i`_path"] = $t.Cand.path; $naState["candidate_$i`_heading"] = $t.Cand.heading_chain
                            $naState["candidate_$i`_excerpt"] = ($t.Cand.text.Substring(0, [Math]::Min(600, $t.Cand.text.Length))) }
    $naQ = @{ type = 'noul'; criteria = $noAnswerCriteria
              instructions = 'Read the query and the listed candidate sections (path, heading, excerpt). Does ANY of them answer the query?' }
    $naCall = Invoke-Jev $apiKey @{ model = 'jev-latest'; state = $naState; questions = @{ any_answer = $naQ } } 3 1 $TestTransportOverride
    $sw.Stop()
    $noAnswerVerdict = 'API_FAILED'
    if ($naCall.Ok) {
        $naNoul = [double]$naCall.Response.answers.any_answer.noul
        $noAnswerVerdict = "noul=$([math]::Round($naNoul,3)) ($(if ($naNoul -ge 0.5) {'yes, an answer exists'} else {'no, none answers it'}))"
        $jevCalls++
        if ($naCall.Response.usage.input_tokens) { $usageIn += [long]$naCall.Response.usage.input_tokens }
    }

    Write-Host "NO_ANSWER_NOUL(any_answer)=$noAnswerVerdict"
    Write-Host "JEV_CALLS=$jevCalls  USAGE_INPUT_TOKENS=$usageIn  WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))  SECTIONS_WAF_BLOCKED=$sectionsWaf"
    Write-Host (Get-JevModelLine)
    Write-Host "TOP $($top5.Count):"
    $rank = 0
    foreach ($t in $top5) {
        $rank++
        $arch = if ($t.Cand.is_archive) { ' [ARCHIVE]' } else { '' }
        $dt = [DateTimeOffset]::FromUnixTimeSeconds([long]$t.Cand.commit_date_epoch).UtcDateTime.ToString('yyyy-MM-dd')
        Write-Host "  $rank. noul=$([math]::Round($t.Noul,3)) $($t.Cand.path)$arch  ::  $($t.Cand.heading_chain)  [commit $dt]"
    }

    # log the live query (gitignored, per measurement plan section 4)
    $logEntry = [ordered]@{
        ts_utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        query = $Query; rev = $sl.rev7; sampled_once = ($Samples -eq 1)
        no_answer_verdict = $noAnswerVerdict; sections_waf_blocked = $sectionsWaf
        jev_model = ((Get-JevModelLine) -replace '^JEV_MODEL ', '')
        top5 = @($top5 | ForEach-Object { @{ path = $_.Cand.path; heading_chain = $_.Cand.heading_chain; noul = $_.Noul } })
    }
    $logFull = Resolve-RepoPath $LogPath
    (ConvertTo-Json -InputObject $logEntry -Depth 6 -Compress) | Add-Content -Encoding UTF8 -Path $logFull
    Write-Host "Logged to $LogPath"
    exit 0
}

# =================================================================================================
# MODE 3: -NoAnswerOnly <id> (SR-D2, second reader 2026-09-24). Rebuilds the id's own
# re-ranked top 5 (BM25 shortlist, then Jev section Nouls at -Samples -- the SAME
# candidates that id's reserved run already scored, re-scored here only as the means to
# reconstruct the order the no-answer check is asked over), then asks the ONE no-answer
# question that id never got. Every gate above already ran before the key was read.
# =================================================================================================
if ($NoAnswerOnly) {
    if ($Samples -lt 1) { $Samples = 5 }
    $slFile3 = [System.IO.Path]::GetTempFileName()
    try {
        & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') shortlist --rev $Rev --query $noAnswerOnlyQuery.query --k $K --out $slFile3 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SHORTLIST_FAILED"; exit 2 }
        $sl3 = Get-Content -Raw -Encoding UTF8 -Path $slFile3 | ConvertFrom-Json
    } finally { Remove-Item -Force $slFile3 -ErrorAction SilentlyContinue }

    Write-Host "NO_ANSWER_ONLY id=$NoAnswerOnly  REV=$($sl3.rev7)  DOC_SCOPE=$($sl3.doc_scope_count) docs  SECTIONS=$($sl3.section_count)  SHORTLIST_K=$($sl3.shortlist.Count)  SAMPLES=$Samples"
    if ($noAnswerOnlySpent) { Write-Host "ALLOW_SPENT_ONCE=$NoAnswerOnly reason=[$Reason]" }

    $cands3 = @($sl3.shortlist)
    $means3 = New-Object System.Collections.Generic.List[double]
    $jevCalls3 = 0; $usageIn3 = [long]0; $sectionsWaf3 = 0
    $sw3 = [System.Diagnostics.Stopwatch]::StartNew()
    foreach ($c in $cands3) {
        $r = Invoke-SectionNoul $apiKey $noAnswerOnlyQuery.query $c $Samples
        if (-not $r.Ok) {
            if ($r.WafBlocked) { $means3.Add(-1.0); $sectionsWaf3++; continue }
            Write-Host "EXIT_REASON=API_FAILED"; Write-Host "Jev request failed for $($c.id): $($r.Error)"; exit 2
        }
        $means3.Add($r.MeanNoul); $jevCalls3 += $r.Calls; $usageIn3 += $r.InputTokens
    }
    $ordered3 = @(Get-RerankedOrder $cands3 $means3)
    $top5_3 = @($ordered3 | Select-Object -First $NoAnswerTopN)

    $naState3 = [ordered]@{ query = $noAnswerOnlyQuery.query; sample_uid = [guid]::NewGuid().ToString('N').Substring(0,8) }
    $i3 = 0
    foreach ($t in $top5_3) { $i3++; $naState3["candidate_$i3`_path"] = $t.Cand.path; $naState3["candidate_$i3`_heading"] = $t.Cand.heading_chain
                              $naState3["candidate_$i3`_excerpt"] = ($t.Cand.text.Substring(0, [Math]::Min(600, $t.Cand.text.Length))) }
    $naQ3 = @{ type = 'noul'; criteria = $noAnswerCriteria
               instructions = 'Read the query and the listed candidate sections (path, heading, excerpt). Does ANY of them answer the query?' }
    $naCall3 = Invoke-Jev $apiKey @{ model = 'jev-latest'; state = $naState3; questions = @{ any_answer = $naQ3 } } 3 1 $TestTransportOverride
    $sw3.Stop()
    if (-not $naCall3.Ok) {
        if ($naCall3.WafBlocked) { Write-Host "EXIT_REASON=WAF_BLOCKED"; Write-Host "The no-answer call itself was blocked. Nothing recorded as an answer; the ledger exception below still applies (the section scoring above still spent real calls)." }
        else { Write-Host "EXIT_REASON=API_FAILED"; Write-Host "Jev no-answer request failed: $($naCall3.Error)"; exit 2 }
    }
    $noAnswerVerdict3 = 'API_FAILED'
    if ($naCall3.Ok) {
        $naNoul3 = [double]$naCall3.Response.answers.any_answer.noul
        $noAnswerVerdict3 = "noul=$([math]::Round($naNoul3,3)) ($(if ($naNoul3 -ge 0.5) {'yes, an answer exists'} else {'no, none answers it'}))"
        $jevCalls3++
        if ($naCall3.Response.usage.input_tokens) { $usageIn3 += [long]$naCall3.Response.usage.input_tokens }
    }

    Write-Host "NO_ANSWER_NOUL(any_answer)=$noAnswerVerdict3"
    Write-Host "JEV_CALLS=$jevCalls3  USAGE_INPUT_TOKENS=$usageIn3  WALL_TIME_SEC=$([math]::Round($sw3.Elapsed.TotalSeconds,2))  SECTIONS_WAF_BLOCKED=$sectionsWaf3"
    Write-Host (Get-JevModelLine)
    Write-Host "TOP $($top5_3.Count) (the re-ranked order this no-answer check was asked over):"
    $rank3 = 0
    foreach ($t in $top5_3) {
        $rank3++
        $arch3 = if ($t.Cand.is_archive) { ' [ARCHIVE]' } else { '' }
        Write-Host "  $rank3. noul=$([math]::Round($t.Noul,3)) $($t.Cand.path)$arch3  ::  $($t.Cand.heading_chain)"
    }

    if ($noAnswerOnlySpent) {
        Add-SpentLedgerException $NoAnswerOnly $Reason
        Write-Host "Exception recorded in $SpentLedger (id already in 'spent'; this call is logged in the new 'exceptions' list, not a duplicate 'spent' entry)."
    } else {
        Add-SpentLedgerEntry $NoAnswerOnly "-NoAnswerOnly, $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))"
        Write-Host "Recorded $NoAnswerOnly as spent in $SpentLedger (-NoAnswerOnly)."
    }
    exit 0
}

# =================================================================================================
# MODE 2: -AcceptanceRun (M4). -Subset acceptance (default) reads the 8 acceptance-subset
# queries only. -Subset reserved is the SEAT's measured run; every gate for it ran above,
# before the key was read (harness-shadow-mode-protocol.md section 4a: a reserved query
# judged once is spent for good).
# =================================================================================================
if ($Samples -lt 1) { $Samples = 5 }

$rows = New-Object System.Collections.Generic.List[object]
$totalJevCalls = 0; $totalUsageIn = [long]0; $sectionsWaf = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()
# EVERY no-answer query gets its own check and its own report line. Before 2026-09-24 one
# variable held one report, and the kind test was `-eq 'nowhere'`, so `nowhere_trap:...`
# (Q29) was never checked at all.
$noAnswerReports = New-Object System.Collections.Generic.List[object]
$runRev7 = $null

foreach ($q in $acceptance) {
    if ($Subset -eq 'reserved') { Add-SpentLedgerEntry ([string]$q.id) "-AcceptanceRun -Subset reserved, $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))" }
    $slFile = [System.IO.Path]::GetTempFileName()
    & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') shortlist --rev $Rev --query $q.query --k $K --out $slFile | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SHORTLIST_FAILED ($($q.id))"; exit 2 }
    $sl = Get-Content -Raw -Encoding UTF8 -Path $slFile | ConvertFrom-Json
    Remove-Item -Force $slFile -ErrorAction SilentlyContinue
    if (-not $runRev7) { $runRev7 = [string]$sl.rev7 }
    $cands = @($sl.shortlist)

    $means = New-Object System.Collections.Generic.List[double]
    foreach ($c in $cands) {
        $r = Invoke-SectionNoul $apiKey $q.query $c $Samples
        if (-not $r.Ok) {
            if ($r.WafBlocked) { $means.Add(-1.0); $sectionsWaf++; continue }
            Write-Host "EXIT_REASON=API_FAILED"; Write-Host "Jev request failed for $($q.id)/$($c.id): $($r.Error)"; exit 2
        }
        $means.Add($r.MeanNoul); $totalJevCalls += $r.Calls; $totalUsageIn += $r.InputTokens
    }
    $ordered = @(Get-RerankedOrder $cands $means)

    $bm25RankedFile = [System.IO.Path]::GetTempFileName()
    $rerankedFile = [System.IO.Path]::GetTempFileName()
    $expectedFile = [System.IO.Path]::GetTempFileName()
    $bm25ScoreFile = [System.IO.Path]::GetTempFileName()
    $rerankScoreFile = [System.IO.Path]::GetTempFileName()
    try {
        ConvertTo-RankedJsonFile $cands $bm25RankedFile
        ConvertTo-RankedJsonFile ($ordered | ForEach-Object { $_.Cand }) $rerankedFile
        (ConvertTo-Json -InputObject @($q.expected) -Depth 6) | Set-Content -Encoding UTF8 -Path $expectedFile
        $bm25ScoreOut = & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') score --ranked-json $bm25RankedFile --expected-json $expectedFile --out $bm25ScoreFile 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SCORE_FAILED ($($q.id), bm25)"; Write-Host ($bm25ScoreOut -join "`n"); exit 2 }
        $rerankScoreOut = & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') score --ranked-json $rerankedFile --expected-json $expectedFile --out $rerankScoreFile 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SCORE_FAILED ($($q.id), rerank)"; Write-Host ($rerankScoreOut -join "`n"); exit 2 }
        $bm25Score = Get-Content -Raw -Encoding UTF8 -Path $bm25ScoreFile | ConvertFrom-Json
        $rerankScore = Get-Content -Raw -Encoding UTF8 -Path $rerankScoreFile | ConvertFrom-Json
    } finally {
        Remove-Item -Force $bm25RankedFile, $rerankedFile, $expectedFile, $bm25ScoreFile, $rerankScoreFile -ErrorAction SilentlyContinue
    }

    $isNoAnswer = ([string]$q.kind) -like 'nowhere*'
    $rows.Add([PSCustomObject]@{
        Id = $q.id; Kind = $q.kind; Answerable = (-not $isNoAnswer)
        Bm25_1 = $bm25Score.'1'; Bm25_5 = $bm25Score.'5'; Bm25_10 = $bm25Score.'10'
        Rerank_1 = $rerankScore.'1'; Rerank_5 = $rerankScore.'5'; Rerank_10 = $rerankScore.'10'
    })

    if ($isNoAnswer) {
        $top5 = @($ordered | Select-Object -First $NoAnswerTopN)
        $naState = [ordered]@{ query = $q.query; sample_uid = [guid]::NewGuid().ToString('N').Substring(0,8) }
        $i = 0
        foreach ($t in $top5) { $i++; $naState["candidate_$i`_path"] = $t.Cand.path; $naState["candidate_$i`_heading"] = $t.Cand.heading_chain
                                $naState["candidate_$i`_excerpt"] = ($t.Cand.text.Substring(0, [Math]::Min(600, $t.Cand.text.Length))) }
        $naQ = @{ type = 'noul'; criteria = $noAnswerCriteria
                  instructions = 'Read the query and the listed candidate sections (path, heading, excerpt). Does ANY of them answer the query?' }
        $naCall = Invoke-Jev $apiKey @{ model = 'jev-latest'; state = $naState; questions = @{ any_answer = $naQ } } 3 1 $TestTransportOverride
        $naTop5 = @($top5 | ForEach-Object { "$($_.Cand.path) :: $($_.Cand.heading_chain)" })
        if ($naCall.Ok) {
            $totalJevCalls++
            if ($naCall.Response.usage.input_tokens) { $totalUsageIn += [long]$naCall.Response.usage.input_tokens }
            $naNoul = [double]$naCall.Response.answers.any_answer.noul
            $naVerdict = "noul=$([math]::Round($naNoul,3)) ($(if ($naNoul -ge 0.5) {'yes, an answer exists'} else {'no, none answers it'}))"
        } elseif ($naCall.WafBlocked) {
            $naVerdict = 'WAF_BLOCKED'
        } else {
            $naVerdict = 'API_FAILED'
        }
        $noAnswerReports.Add([PSCustomObject]@{ Id = $q.id; Kind = $q.kind; Query = $q.query; Verdict = $naVerdict; Top5 = $naTop5 })
    }
    Write-Host "DONE $($q.id)  bm25[1/5/10]=$($bm25Score.'1')/$($bm25Score.'5')/$($bm25Score.'10')  rerank[1/5/10]=$($rerankScore.'1')/$($rerankScore.'5')/$($rerankScore.'10')"
}
$sw.Stop()

function Pct($rows_, $field) {
    # @() wrap is load-bearing: a bare (pipe).Count returns $null, not 1, when exactly one
    # row survives Where-Object -- reproduced live (PS 5.1 single-element-array-unwrap trap).
    # Piped, never @($rows_): in this PS 5.1 host @() of a New-Object List[object] throws
    # "Argument types do not match" (reproduced 2026-09-24 UTC).
    $all = @($rows_ | ForEach-Object { $_ })
    if ($all.Count -eq 0) { return '0/0' }
    $n = (@($all | Where-Object { $_.$field -eq $true })).Count
    "$n/$($all.Count) = $([math]::Round(100.0*$n/$all.Count,1))%"
}
$answerableRows = @($rows | Where-Object { $_.Answerable })
Write-Host ""
Write-Host "BM25 alone   top1=$(Pct $rows Bm25_1)  top5=$(Pct $rows Bm25_5)  top10=$(Pct $rows Bm25_10)"
Write-Host "BM25 + Jev   top1=$(Pct $rows Rerank_1)  top5=$(Pct $rows Rerank_5)  top10=$(Pct $rows Rerank_10)"
Write-Host "ANSWERABLE ONLY ($($answerableRows.Count)): BM25 top1=$(Pct $answerableRows Bm25_1) top10=$(Pct $answerableRows Bm25_10)  +Jev top1=$(Pct $answerableRows Rerank_1) top10=$(Pct $answerableRows Rerank_10)"
Write-Host "JEV_CALLS=$totalJevCalls  USAGE_INPUT_TOKENS=$totalUsageIn  WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))  SECTIONS_WAF_BLOCKED=$sectionsWaf"
Write-Host (Get-JevModelLine)
Write-Host "NO_ANSWER_QUERIES=$($noAnswerReports.Count)"
foreach ($na in $noAnswerReports) {
    Write-Host "NO_ANSWER_QUERY $($na.Id): verdict=$($na.Verdict)"
    Write-Host "  top5 shown to the no-answer Noul: $($na.Top5 -join ' | ')"
}

if (-not $OutPath) {
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    $OutPath = "docs/harness-runs/doc-reranker-$Subset-run-$stamp.md"
}
$rl = New-Object System.Collections.Generic.List[string]
$rl.Add("# Doc re-ranker (harness 5) -- $Subset run (M4)")
$rl.Add('')
$rl.Add("Generated by ``tools/checks/doc-reranker.ps1 -AcceptanceRun -Subset $Subset`` at revision ``$runRev7`` (``-Rev $Rev``), ``-Samples $Samples``, ``-K $K``.")
if ($ExcludeIds.Count -gt 0) { $rl.Add(''); $rl.Add("Excluded ids: $($ExcludeIds -join ', ').") }
$rl.Add('')
$rl.Add('## Per query')
$rl.Add('')
$rl.Add('| Query | Kind | BM25 top1 | BM25 top5 | BM25 top10 | +Jev top1 | +Jev top5 | +Jev top10 |')
$rl.Add('|---|---|---|---|---|---|---|---|')
foreach ($r in $rows) { $rl.Add("| ``$($r.Id)`` | $($r.Kind) | $($r.Bm25_1) | $($r.Bm25_5) | $($r.Bm25_10) | $($r.Rerank_1) | $($r.Rerank_5) | $($r.Rerank_10) |") }
$rl.Add('')
$rl.Add("## Totals ($($rows.Count) $Subset queries; $($answerableRows.Count) have an answer)")
$rl.Add('')
$rl.Add("- All $($rows.Count), BM25 alone: top1 $(Pct $rows Bm25_1), top5 $(Pct $rows Bm25_5), top10 $(Pct $rows Bm25_10)")
$rl.Add("- All $($rows.Count), BM25 + Jev: top1 $(Pct $rows Rerank_1), top5 $(Pct $rows Rerank_5), top10 $(Pct $rows Rerank_10)")
$rl.Add("- Answerable $($answerableRows.Count), BM25 alone: top1 $(Pct $answerableRows Bm25_1), top5 $(Pct $answerableRows Bm25_5), top10 $(Pct $answerableRows Bm25_10)")
$rl.Add("- Answerable $($answerableRows.Count), BM25 + Jev: top1 $(Pct $answerableRows Rerank_1), top5 $(Pct $answerableRows Rerank_5), top10 $(Pct $answerableRows Rerank_10)")
$rl.Add('')
$rl.Add("## No-answer Noul, every no-answer query ($($noAnswerReports.Count))")
$rl.Add('')
if ($noAnswerReports.Count -eq 0) {
    $rl.Add("- none found in the $Subset subset.")
}
foreach ($na in $noAnswerReports) {
    $rl.Add("- ``$($na.Id)`` ($($na.Kind)): *$($na.Query)* -- verdict ``$($na.Verdict)``")
    $rl.Add("  - top 5 shown: $($na.Top5 -join ' | ')")
}
$rl.Add('')
$rl.Add('## Jev spend')
$rl.Add('')
$rl.Add("- Jev calls: $totalJevCalls")
$rl.Add("- Input tokens: $totalUsageIn")
$rl.Add("- Wall time: $([math]::Round($sw.Elapsed.TotalSeconds,2)) s")
$rl.Add("- Sections WAF-blocked (ranked last, never judged): $sectionsWaf")
$rl.Add("- Model: $((Get-JevModelLine) -replace '^JEV_MODEL ', '')")
Set-Content -Encoding UTF8 -Path (Resolve-RepoPath $OutPath) -Value ($rl -join "`r`n")
Write-Host "Report written to $OutPath"
exit 0
