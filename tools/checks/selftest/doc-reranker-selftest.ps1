#requires -Version 5.1
<#
  tools/checks/selftest/doc-reranker-selftest.ps1 -- offline self-test for harness 5,
  tools/checks/doc-reranker.ps1, after the second reader's 2026-09-24 (UTC) changes.
  NO NETWORK, NO REAL JEV CALL: every call goes through -TestTransportOverride, which returns
  SYNTHETIC answers. The shortlist step is real (code only, python over the tracked docs).
  The query file and the spent ledger are SYNTHETIC temp files; the real ledger
  docs/harness-runs/doc-reranker-spent-queries.json is only READ (check A), never written.

  Checks, each printed PASS or FAIL:
    A  The REAL ledger refuses -Subset reserved over the real question set: exit 2,
       RESERVED_QUERY_SPENT, zero transport calls.
    B  A synthetic reserved subset of 3 (one found, one `nowhere`, one `nowhere_trap:...`) with
       no ledger file: exit 2, SPENT_LEDGER_MISSING, zero calls.
    C  Same, empty ledger: exit 0; BOTH no-answer queries are checked and reported; every id
       is written to the ledger; the report says "reserved run" and "3 reserved queries; 1 have
       an answer"; one WAF-blocked section is COUNTED; JEV_MODEL names the synthetic resolved
       model.
    D  Re-run C: exit 2, RESERVED_QUERY_SPENT, zero calls.
    E  One-element paths: -ExcludeIds passed as ONE comma-joined string (the `-File` shape)
       leaves exactly 1 query; -K 1 gives a 1-candidate shortlist; the run completes, exit 0,
       and the totals read 1/1 or 0/1, never blank.
    F  -Query with a reserved query's text (case and spaces changed): the loud warning prints.
    G  -NoAnswerOnly on an id already in the ledger (S1, spent by check E), no
       -AllowSpentOnce: refuses RESERVED_QUERY_SPENT, zero calls (SR-D2).
    H  Same id, -AllowSpentOnce S1 -Reason: runs (rebuild-shortlist call + the one no-answer
       call), records an "exceptions" entry in the ledger, never a duplicate "spent" entry.
    I  -AllowSpentOnce on an id that is NOT spent: refuses ALLOW_SPENT_ONCE_UNUSED, zero calls.
    J  -NoAnswerOnly on an unknown id: refuses QUERY_ID_NOT_FOUND, zero calls.

  Usage (repo root):  powershell -NoProfile -File tools/checks/selftest/doc-reranker-selftest.ps1
  Exit 0 when every check passes, 1 otherwise.
#>
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$tool = Join-Path $repo 'tools\checks\doc-reranker.ps1'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("dr-selftest-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tmp | Out-Null
$failures = 0
function Assert-That([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { "PASS  $name" } else { "FAIL  $name -- $detail"; $script:failures++ }
}

# The tool prints with Write-Host (stream 6), so every capture below is *>&1, not 2>&1.
$global:DrCalls = 0
$transport = {
    param($bytes)
    $global:DrCalls++
    $b = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $qs = @($b.questions.PSObject.Properties.Name)
    # The very first section call is a simulated firewall block.
    if ($global:DrCalls -eq 1 -and $qs -contains 'answers') {
        return @{ Ok = $false; Response = $null; Error = 'synthetic 403'; Status = 403; WafBlocked = $true }
    }
    $ans = @{}
    if ($qs -contains 'answers') { $ans.answers = @{ type = 'noul'; noul = 0.7 } }
    if ($qs -contains 'any_answer') { $ans.any_answer = @{ type = 'noul'; noul = 0.3 } }
    return @{ Ok = $true; Error = $null; Status = 200; WafBlocked = $false
              Response = [PSCustomObject]@{ model = 'jev-selftest-0.0.1'; answers = [PSCustomObject]$ans; usage = [PSCustomObject]@{ input_tokens = 11; output_tokens = 1 } } }
}
$savedKey = $env:TYPESAFE_API_KEY
$env:TYPESAFE_API_KEY = 'selftest-dummy-key-not-real'
try {
    # ---- A: the real ledger, the real question set ----
    $global:DrCalls = 0
    $out = @(& $tool -AcceptanceRun -Subset reserved -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'A real ledger refuses the reserved subset' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=RESERVED_QUERY_SPENT') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"

    # ---- synthetic query set ----
    $qf = Join-Path $tmp 'queries.json'
    $qjson = @'
{ "queries": [
  { "id": "S1", "subset": "reserved", "kind": "found", "query": "Where are the ATR low normal high bands defined?", "expected": [ { "path": "docs/trader-profile.md", "section": "5 ATR thresholds" } ] },
  { "id": "S2", "subset": "reserved", "kind": "nowhere", "query": "Where is the synthetic selftest rule Z-99 defined?", "expected": [] },
  { "id": "S3", "subset": "reserved", "kind": "nowhere_trap:selftest", "query": "Where was the MACD indicator re-adopted after being rejected?", "expected": [] },
  { "id": "S4", "subset": "acceptance", "kind": "found", "query": "unused", "expected": [] }
] }
'@
    [System.IO.File]::WriteAllText($qf, $qjson, (New-Object System.Text.UTF8Encoding($false)))
    $ledger = Join-Path $tmp 'spent.json'
    $rep = Join-Path $tmp 'report.md'

    # ---- B: no ledger file ----
    $global:DrCalls = 0
    $out = @(& $tool -AcceptanceRun -Subset reserved -QueryFile $qf -SpentLedger $ledger -K 3 -Samples 1 -OutPath $rep -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'B no ledger file refuses' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=SPENT_LEDGER_MISSING') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"

    # ---- C: empty ledger, full run ----
    [System.IO.File]::WriteAllText($ledger, '{ "_note": "selftest", "spent": [] }', (New-Object System.Text.UTF8Encoding($false)))
    $global:DrCalls = 0
    $out = @(& $tool -AcceptanceRun -Subset reserved -QueryFile $qf -SpentLedger $ledger -K 3 -Samples 1 -OutPath $rep -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    $txt = $out -join "`n"
    $spent = @(([System.IO.File]::ReadAllText($ledger) | ConvertFrom-Json).spent | ForEach-Object { $_.id })
    $repTxt = if (Test-Path $rep) { [System.IO.File]::ReadAllText($rep) } else { '' }
    Assert-That 'C run completes' ($rc -eq 0) "exit=$rc"
    Assert-That 'C both no-answer queries checked' (($out -contains 'NO_ANSWER_QUERIES=2') -and $txt.Contains('NO_ANSWER_QUERY S2: verdict=noul=0.3') -and $txt.Contains('NO_ANSWER_QUERY S3: verdict=noul=0.3')) (($out | Where-Object { $_ -like 'NO_ANSWER*' }) -join ' / ')
    Assert-That 'C ledger records all three' ((($spent | Sort-Object) -join ',') -eq 'S1,S2,S3') "spent=[$($spent -join ',')]"
    Assert-That 'C report headings name the subset' ($repTxt.Contains('-- reserved run (M4)') -and $repTxt.Contains('## Totals (3 reserved queries; 1 have an answer)') -and $repTxt.Contains('every no-answer query (2)') -and -not $repTxt.Contains('acceptance')) 'heading text'
    Assert-That 'C WAF-blocked section counted' ($txt.Contains('SECTIONS_WAF_BLOCKED=1') -and $repTxt.Contains('Sections WAF-blocked (ranked last, never judged): 1')) 'waf counter'
    Assert-That 'C resolved model recorded' ($txt -match 'JEV_MODEL requested=\[jev-latest x\d+\] resolved=\[jev-selftest-0\.0\.1 x\d+\]' -and $repTxt.Contains('resolved=[jev-selftest-0.0.1')) (($out | Where-Object { $_ -like 'JEV_MODEL*' }) -join ' ')

    # ---- D: re-run is refused ----
    $global:DrCalls = 0
    $out = @(& $tool -AcceptanceRun -Subset reserved -QueryFile $qf -SpentLedger $ledger -K 3 -Samples 1 -OutPath $rep -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'D spent ids refused on re-run' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=RESERVED_QUERY_SPENT') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"

    # ---- E: one-element paths, the -File argument shape ----
    [System.IO.File]::WriteAllText($ledger, '{ "_note": "selftest", "spent": [] }', (New-Object System.Text.UTF8Encoding($false)))
    $global:DrCalls = 0
    $out = @(& $tool -AcceptanceRun -Subset reserved -QueryFile $qf -SpentLedger $ledger -ExcludeIds 'S2,S3' -K 1 -Samples 1 -OutPath $rep -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    $txt = $out -join "`n"
    Assert-That 'E comma-joined -ExcludeIds leaves one query' ($txt.Contains('SUBSET=reserved  QUERIES=1  EXCLUDED=S2,S3')) (($out | Where-Object { $_ -like 'SUBSET=*' }) -join ' ')
    Assert-That 'E one query, one candidate: totals never blank' ($rc -eq 0 -and $txt -match 'BM25 alone   top1=[01]/1 = ' -and $txt -match 'BM25 \+ Jev   top1=[01]/1 = ' -and ($out -contains 'NO_ANSWER_QUERIES=0')) "exit=$rc"

    # ---- F: -Query with reserved text ----
    $out = @(& $tool -Query '  where are the ATR LOW normal high   bands defined? ' -QueryFile $qf -SpentLedger $ledger -K 1 -LogPath (Join-Path $tmp 'log.jsonl') -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    Assert-That 'F reserved text in -Query warns loudly' (@($out | Where-Object { $_ -like 'WARNING_RESERVED_QUERY=S1 *' }).Count -eq 1) (($out | Select-Object -First 3) -join ' / ')

    # ---- G (second reader, 2026-09-24, SR-D2): -NoAnswerOnly on an already-spent id (S1,
    # spent by check E above), no -AllowSpentOnce -- refuse, zero calls.
    $global:DrCalls = 0
    $out = @(& $tool -NoAnswerOnly S1 -QueryFile $qf -SpentLedger $ledger -K 1 -Samples 1 -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'G -NoAnswerOnly on a spent id with no exception refuses' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=RESERVED_QUERY_SPENT') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"

    # ---- H: -AllowSpentOnce S1 -Reason opts out -- runs (1 rebuild call + 1 no-answer call
    # at -K 1 -Samples 1), and the ledger gets an "exceptions" entry, not a duplicate "spent".
    $global:DrCalls = 0
    $out = @(& $tool -NoAnswerOnly S1 -QueryFile $qf -SpentLedger $ledger -K 1 -Samples 1 -AllowSpentOnce S1 -Reason 'selftest exception' -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    $txt = $out -join "`n"
    $ledgerObj = [System.IO.File]::ReadAllText($ledger) | ConvertFrom-Json
    $spentAfterH = @($ledgerObj.spent | ForEach-Object { $_.id })
    $excAfterH = @($ledgerObj.exceptions | ForEach-Object { $_.id })
    Assert-That 'H exits 0 and makes 2 calls (1 rebuild + 1 no-answer)' ($rc -eq 0 -and $global:DrCalls -eq 2) "exit=$rc calls=$global:DrCalls"
    Assert-That 'H prints the ALLOW_SPENT_ONCE line and the no-answer verdict' ($txt.Contains('ALLOW_SPENT_ONCE=S1 reason=[selftest exception]') -and $txt.Contains('NO_ANSWER_NOUL(any_answer)=noul=0.3 (no, none answers it)')) 'lines missing'
    Assert-That 'H spent list unchanged (still just S1, no duplicate)' ((($spentAfterH | Sort-Object) -join ',') -eq 'S1') "spent=[$($spentAfterH -join ',')]"
    Assert-That 'H exceptions list gains exactly one S1 entry' ((($excAfterH | Sort-Object) -join ',') -eq 'S1') "exceptions=[$($excAfterH -join ',')]"

    # ---- I: -AllowSpentOnce on an id that is NOT spent (S4) -- refuse, zero calls.
    $global:DrCalls = 0
    $out = @(& $tool -NoAnswerOnly S4 -QueryFile $qf -SpentLedger $ledger -K 1 -Samples 1 -AllowSpentOnce S4 -Reason 'unnecessary' -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'I -AllowSpentOnce on an unspent id refuses' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=ALLOW_SPENT_ONCE_UNUSED') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"

    # ---- J: -NoAnswerOnly on an unknown id -- refuse, zero calls.
    $global:DrCalls = 0
    $out = @(& $tool -NoAnswerOnly S99 -QueryFile $qf -SpentLedger $ledger -K 1 -Samples 1 -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    Assert-That 'J -NoAnswerOnly on an unknown id refuses' ($rc -eq 2 -and ($out -contains 'EXIT_REASON=QUERY_ID_NOT_FOUND') -and $global:DrCalls -eq 0) "exit=$rc calls=$global:DrCalls"
} finally {
    $env:TYPESAFE_API_KEY = $savedKey
    Remove-Item -Recurse -Force -Path $tmp -ErrorAction SilentlyContinue
    Remove-Variable -Scope Global -Name DrCalls -ErrorAction SilentlyContinue
}
if ($failures -eq 0) { 'SELFTEST PASSED'; exit 0 } else { "SELFTEST FAILED ($failures)"; exit 1 }
