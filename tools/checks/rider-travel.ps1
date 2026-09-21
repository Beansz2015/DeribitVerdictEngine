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

  EXIT CODES (§4.6):
    0 - every TRAVELLING rider judged arrived or not_a_header_column
    1 - at least one rider judged missing or ambiguous
    2 - parse failure, baseline missing, or API failure (D-5, fail loud)
#>
[CmdletBinding()]
param(
    [string]$AfterFile = 'AnalysisLogger.vb',
    [string]$BeforeRev = 'HEAD',
    [string]$LedgerPath = 'docs/csv-rotation-riders.md',
    [string]$BaselinePath = 'rider-travel-baseline.json',
    [string]$OutPath = 'rider-travel-report.md'
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
    return $rows
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
$beforeCols = if ($null -ne $beforeHeader) { $beforeHeader.Split(',') } else { @() }
$afterCols  = if ($null -ne $afterHeader)  { $afterHeader.Split(',') }  else { @() }
$addedCols   = @($afterCols  | Where-Object { $beforeCols -notcontains $_ })
$removedCols = @($beforeCols | Where-Object { $afterCols  -notcontains $_ })

# ---------------------------------------------------------------------------------------
# Steps 2-3 (§4.2): parse the ledger, split TRAVELLING from everything else.
# ---------------------------------------------------------------------------------------
$ledgerFull = Resolve-RepoPath $LedgerPath
$allRows = Get-LedgerRows $ledgerFull
$ridersInLedger = if ($null -eq $allRows) { 0 } else { $allRows.Count }
$travelling = if ($null -eq $allRows) { @() } else { @($allRows | Where-Object { $_.Status -match 'TRAVELLING' }) }
$others     = if ($null -eq $allRows) { @() } else { @($allRows | Where-Object { $_.Status -notmatch 'TRAVELLING' }) }
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
# Step 4.4 (D-4): refuse to call the API without an operator-written baseline.
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each TRAVELLING rider BEFORE running this tool, as JSON, one key per rider ID, values one of arrived|missing|not_a_header_column|unsure. Example:"
    '{ "RIDER-1": "arrived", "RIDER-2": "not_a_header_column", "RIDER-3": "missing" }'
    "The whole point of the first run is comparing an independent human read against Jev's; looking first makes that comparison worthless permanently."
    exit 2
}
$baselineRaw = Get-Content -Raw -Path $baselineFull | ConvertFrom-Json
$baseline = @{}
if ($null -ne $baselineRaw) {
    foreach ($p in $baselineRaw.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}

# ---------------------------------------------------------------------------------------
# Step 6 (§4.2, §5): one Jev request per TRAVELLING rider. D-5: any API/parse failure
# fails loud and stops the run rather than degrading to a partial "all clear".
# ---------------------------------------------------------------------------------------
$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
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

$results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''

foreach ($r in $travelling) {
    $state = @{
        rider = @{
            id          = $r.Id
            description = $r.Rider
            lands_in    = $r.LandsIn
            status      = $r.Status
        }
        columns_added_by_this_rotation = @($addedCols)
        all_columns_after              = @($afterCols)
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

    $call = Invoke-Jev $apiKey $body
    if (-not $call.Ok) {
        $apiFailed = $true
        $apiFailMsg = "Jev request failed for $($r.Id): $($call.Error)"
        break
    }

    $ans = $call.Response.answers
    # --- TRAP 1 (docs/rider-travel-check-spec.md §0): the verdict comes from the `verdict`
    # Choice answer ALONE. The two Noul answers below are diagnostics ONLY -- never combine
    # them with `-and`/`-or` into a derived verdict. This is the one line a reviewer checks.
    $verdict = $ans.verdict.choice

    $results.Add([PSCustomObject]@{
        Id                 = $r.Id
        Verdict            = $verdict
        VerdictConfidence  = $ans.verdict.confidence
        LandsInHeaderNoul  = $ans.lands_in_header.noul
        ColumnPresentNoul  = $ans.column_present.noul
        Baseline           = if ($baseline.ContainsKey($r.Id)) { $baseline[$r.Id] } else { $null }
    })
}

if ($apiFailed) {
    Write-Coverage $ridersInLedger $ridersTravelling $results.Count $beforeCols.Count $afterCols.Count $addedCols.Count $removedCols.Count
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 7 (§4.2, §4.6): emit the report and decide the exit code from `verdict` alone.
# ---------------------------------------------------------------------------------------
Write-Coverage $ridersInLedger $ridersTravelling $results.Count $beforeCols.Count $afterCols.Count $addedCols.Count $removedCols.Count

"PER_RIDER_RESULTS:"
foreach ($res in $results) {
    $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
             elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
             elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
             else { 'DISAGREE' }
    "  $($res.Id) verdict=$($res.Verdict) (confidence=$([math]::Round([double]$res.VerdictConfidence,2))) baseline=$($res.Baseline) [$agree] lands_in_header_noul=$([math]::Round([double]$res.LandsInHeaderNoul,2)) column_present_noul=$([math]::Round([double]$res.ColumnPresentNoul,2))"
}

# Write the markdown report (§4.1 -OutPath: "where the comparison lands").
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Rider-travel check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/rider-travel.ps1`` against ``-BeforeRev $BeforeRev`` / ``-AfterFile $AfterFile``.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| RIDERS_IN_LEDGER | $ridersInLedger |")
$reportLines.Add("| RIDERS_TRAVELLING | $ridersTravelling |")
$reportLines.Add("| RIDERS_JUDGED | $($results.Count) |")
$reportLines.Add("| HEADER_COLUMNS_BEFORE | $($beforeCols.Count) |")
$reportLines.Add("| HEADER_COLUMNS_AFTER | $($afterCols.Count) |")
$reportLines.Add("| COLUMNS_ADDED | $($addedCols.Count) |")
$reportLines.Add("| COLUMNS_REMOVED | $($removedCols.Count) |")
$reportLines.Add('')
if ($addedCols.Count -gt 0) { $reportLines.Add('**Added:** ' + ($addedCols -join ', ')); $reportLines.Add('') }
if ($removedCols.Count -gt 0) { $reportLines.Add('**Removed:** ' + ($removedCols -join ', ')); $reportLines.Add('') }
$reportLines.Add('## Per-rider verdicts')
$reportLines.Add('')
$reportLines.Add('| ID | Verdict | Confidence | Baseline | Agreement | lands_in_header (noul) | column_present (noul) |')
$reportLines.Add('|---|---|---|---|---|---|---|')
foreach ($res in $results) {
    $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' }
             elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' }
             elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' }
             else { 'DISAGREE' }
    $reportLines.Add("| $($res.Id) | $($res.Verdict) | $([math]::Round([double]$res.VerdictConfidence,2)) | $($res.Baseline) | $agree | $([math]::Round([double]$res.LandsInHeaderNoul,2)) | $([math]::Round([double]$res.ColumnPresentNoul,2)) |")
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

$anyBad = @($results | Where-Object { $_.Verdict -eq 'missing' -or $_.Verdict -eq 'ambiguous' }).Count -gt 0
if ($anyBad) { exit 1 } else { exit 0 }
