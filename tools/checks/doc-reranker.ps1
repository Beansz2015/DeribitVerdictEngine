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
  query is spent for good. -AcceptanceRun refuses to run unless the question set names
  exactly 8 acceptance-subset queries, and only ever reads that subset -- it never touches
  a reserved-subset row. -Query (live, single free-text question) is unrestricted, because
  a live query is not one of the 29 measured questions and carries no baseline to
  contaminate.

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
    [string]$Python = 'python'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:PYTHONIOENCODING = 'utf-8'
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

if (-not $Query -and -not $AcceptanceRun) {
    Write-Host "EXIT_REASON=NO_MODE"
    Write-Host "Pass -Query '<question>' for a live single query, or -AcceptanceRun for the M4 acceptance measurement (8 acceptance-subset queries only)."
    exit 2
}
if ($Query -and $AcceptanceRun) {
    Write-Host "EXIT_REASON=AMBIGUOUS_MODE"
    Write-Host "-Query and -AcceptanceRun are mutually exclusive."
    exit 2
}

$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "EXIT_REASON=API_FAILED"
    Write-Host "TYPESAFE_API_KEY is not set. Load it first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

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
        $call = Invoke-Jev $key @{ model = 'jev-latest'; state = $state; questions = @{ answers = $qdef } }
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
    $jevCalls = 0; $usageIn = [long]0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    foreach ($c in $cands) {
        $r = Invoke-SectionNoul $apiKey $Query $c $Samples
        if (-not $r.Ok) {
            if ($r.WafBlocked) { $means.Add(-1.0); continue }
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
    $naCall = Invoke-Jev $apiKey @{ model = 'jev-latest'; state = $naState; questions = @{ any_answer = $naQ } }
    $sw.Stop()
    $noAnswerVerdict = 'API_FAILED'
    if ($naCall.Ok) {
        $naNoul = [double]$naCall.Response.answers.any_answer.noul
        $noAnswerVerdict = "noul=$([math]::Round($naNoul,3)) ($(if ($naNoul -ge 0.5) {'yes, an answer exists'} else {'no, none answers it'}))"
        $jevCalls++
        if ($naCall.Response.usage.input_tokens) { $usageIn += [long]$naCall.Response.usage.input_tokens }
    }

    Write-Host "NO_ANSWER_NOUL(any_answer)=$noAnswerVerdict"
    Write-Host "JEV_CALLS=$jevCalls  USAGE_INPUT_TOKENS=$usageIn  WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"
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
        no_answer_verdict = $noAnswerVerdict
        top5 = @($top5 | ForEach-Object { @{ path = $_.Cand.path; heading_chain = $_.Cand.heading_chain; noul = $_.Noul } })
    }
    $logFull = Resolve-RepoPath $LogPath
    (ConvertTo-Json -InputObject $logEntry -Depth 6 -Compress) | Add-Content -Encoding UTF8 -Path $logFull
    Write-Host "Logged to $LogPath"
    exit 0
}

# =================================================================================================
# MODE 2: -AcceptanceRun (M4). Acceptance-subset queries ONLY -- never the reserved subset
# (harness-shadow-mode-protocol.md section 4a: a reserved query judged once is spent for good).
# =================================================================================================
$qfFull = Resolve-RepoPath $QueryFile
$qset = Get-Content -Raw -Encoding UTF8 -Path $qfFull | ConvertFrom-Json
$acceptance = @($qset.queries | Where-Object { $_.subset -eq 'acceptance' })
if ($acceptance.Count -ne 8) {
    Write-Host "EXIT_REASON=ACCEPTANCE_SET_UNEXPECTED"
    Write-Host "Expected 8 acceptance-subset queries in $QueryFile, found $($acceptance.Count). Refusing -- this run's numbers are specced against exactly 8."
    exit 2
}
if ($Samples -lt 1) { $Samples = 5 }

$rows = New-Object System.Collections.Generic.List[object]
$totalJevCalls = 0; $totalUsageIn = [long]0
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$noAnswerReport = $null

foreach ($q in $acceptance) {
    $slFile = [System.IO.Path]::GetTempFileName()
    & $Python (Join-Path $PSScriptRoot 'lib\doc_reranker_shortlist.py') shortlist --rev $Rev --query $q.query --k $K --out $slFile | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "EXIT_REASON=SHORTLIST_FAILED ($($q.id))"; exit 2 }
    $sl = Get-Content -Raw -Encoding UTF8 -Path $slFile | ConvertFrom-Json
    Remove-Item -Force $slFile -ErrorAction SilentlyContinue
    $cands = @($sl.shortlist)

    $means = New-Object System.Collections.Generic.List[double]
    foreach ($c in $cands) {
        $r = Invoke-SectionNoul $apiKey $q.query $c $Samples
        if (-not $r.Ok) {
            if ($r.WafBlocked) { $means.Add(-1.0); continue }
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

    $rows.Add([PSCustomObject]@{
        Id = $q.id; Kind = $q.kind
        Bm25_1 = $bm25Score.'1'; Bm25_5 = $bm25Score.'5'; Bm25_10 = $bm25Score.'10'
        Rerank_1 = $rerankScore.'1'; Rerank_5 = $rerankScore.'5'; Rerank_10 = $rerankScore.'10'
    })

    if ($q.kind -eq 'nowhere') {
        $top5 = @($ordered | Select-Object -First $NoAnswerTopN)
        $naState = [ordered]@{ query = $q.query; sample_uid = [guid]::NewGuid().ToString('N').Substring(0,8) }
        $i = 0
        foreach ($t in $top5) { $i++; $naState["candidate_$i`_path"] = $t.Cand.path; $naState["candidate_$i`_heading"] = $t.Cand.heading_chain
                                $naState["candidate_$i`_excerpt"] = ($t.Cand.text.Substring(0, [Math]::Min(600, $t.Cand.text.Length))) }
        $naQ = @{ type = 'noul'; criteria = $noAnswerCriteria
                  instructions = 'Read the query and the listed candidate sections (path, heading, excerpt). Does ANY of them answer the query?' }
        $naCall = Invoke-Jev $apiKey @{ model = 'jev-latest'; state = $naState; questions = @{ any_answer = $naQ } }
        if ($naCall.Ok) {
            $totalJevCalls++
            if ($naCall.Response.usage.input_tokens) { $totalUsageIn += [long]$naCall.Response.usage.input_tokens }
            $naNoul = [double]$naCall.Response.answers.any_answer.noul
            $naVerdict = "noul=$([math]::Round($naNoul,3)) ($(if ($naNoul -ge 0.5) {'yes, an answer exists'} else {'no, none answers it'}))"
            $noAnswerReport = [PSCustomObject]@{ Id = $q.id; Query = $q.query; Verdict = $naVerdict
                                                  Top5 = @($top5 | ForEach-Object { "$($_.Cand.path) :: $($_.Cand.heading_chain)" }) }
        } else {
            $noAnswerReport = [PSCustomObject]@{ Id = $q.id; Query = $q.query; Verdict = 'API_FAILED'; Top5 = @() }
        }
    }
    Write-Host "DONE $($q.id)  bm25[1/5/10]=$($bm25Score.'1')/$($bm25Score.'5')/$($bm25Score.'10')  rerank[1/5/10]=$($rerankScore.'1')/$($rerankScore.'5')/$($rerankScore.'10')"
}
$sw.Stop()

function Pct($rows_, $field) {
    # @() wrap is load-bearing: a bare (pipe).Count returns $null, not 1, when exactly one
    # row survives Where-Object -- reproduced live (PS 5.1 single-element-array-unwrap trap).
    $n = (@($rows_ | Where-Object { $_.$field -eq $true })).Count
    "$n/$($rows_.Count) = $([math]::Round(100.0*$n/$rows_.Count,1))%"
}
Write-Host ""
Write-Host "BM25 alone   top1=$(Pct $rows Bm25_1)  top5=$(Pct $rows Bm25_5)  top10=$(Pct $rows Bm25_10)"
Write-Host "BM25 + Jev   top1=$(Pct $rows Rerank_1)  top5=$(Pct $rows Rerank_5)  top10=$(Pct $rows Rerank_10)"
Write-Host "JEV_CALLS=$totalJevCalls  USAGE_INPUT_TOKENS=$totalUsageIn  WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"
if ($noAnswerReport) {
    Write-Host "NO_ANSWER_QUERY $($noAnswerReport.Id): verdict=$($noAnswerReport.Verdict)"
    Write-Host "  top5 shown to the no-answer Noul: $($noAnswerReport.Top5 -join ' | ')"
}

if (-not $OutPath) {
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    $OutPath = "docs/harness-runs/doc-reranker-acceptance-run-$stamp.md"
}
$rl = New-Object System.Collections.Generic.List[string]
$rl.Add('# Doc re-ranker (harness 5) -- acceptance run (M4)')
$rl.Add('')
$rl.Add("Generated by ``tools/checks/doc-reranker.ps1 -AcceptanceRun`` at revision ``$Rev``, ``-Samples $Samples``, ``-K $K``.")
$rl.Add('')
$rl.Add('## Per query')
$rl.Add('')
$rl.Add('| Query | BM25 top1 | BM25 top5 | BM25 top10 | +Jev top1 | +Jev top5 | +Jev top10 |')
$rl.Add('|---|---|---|---|---|---|---|')
foreach ($r in $rows) { $rl.Add("| ``$($r.Id)`` | $($r.Bm25_1) | $($r.Bm25_5) | $($r.Bm25_10) | $($r.Rerank_1) | $($r.Rerank_5) | $($r.Rerank_10) |") }
$rl.Add('')
$rl.Add('## Totals (8 acceptance queries)')
$rl.Add('')
$rl.Add("- BM25 alone: top1 $(Pct $rows Bm25_1), top5 $(Pct $rows Bm25_5), top10 $(Pct $rows Bm25_10)")
$rl.Add("- BM25 + Jev: top1 $(Pct $rows Rerank_1), top5 $(Pct $rows Rerank_5), top10 $(Pct $rows Rerank_10)")
$rl.Add('')
$rl.Add('## No-answer Noul, the one acceptance "nowhere" query')
$rl.Add('')
if ($noAnswerReport) {
    $rl.Add("- ``$($noAnswerReport.Id)``: *$($noAnswerReport.Query)* -- verdict ``$($noAnswerReport.Verdict)``")
    $rl.Add("- top 5 shown: $($noAnswerReport.Top5 -join ' | ')")
} else {
    $rl.Add('- none found in the acceptance subset (unexpected -- check the question set).')
}
$rl.Add('')
$rl.Add('## Jev spend')
$rl.Add('')
$rl.Add("- Jev calls: $totalJevCalls")
$rl.Add("- Input tokens: $totalUsageIn")
$rl.Add("- Wall time: $([math]::Round($sw.Elapsed.TotalSeconds,2)) s")
Set-Content -Encoding UTF8 -Path (Resolve-RepoPath $OutPath) -Value ($rl -join "`r`n")
Write-Host "Report written to $OutPath"
exit 0
