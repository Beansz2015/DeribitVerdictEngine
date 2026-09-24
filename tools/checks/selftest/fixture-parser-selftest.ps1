#requires -Version 5.1
<#
  tools/checks/selftest/fixture-parser-selftest.ps1 -- offline self-test for harness 3,
  tools/checks/fixture-parser.ps1, revision 4 (2026-09-23 UTC). NO NETWORK, NO REAL JEV CALL.

  Every call the harness would send goes through its -TestTransportOverride seam instead.
  The seam returns SYNTHETIC answers and counts calls. TYPESAFE_API_KEY is replaced with a
  dummy for the duration of the test and restored afterwards, so a seam that failed to
  thread through would reach the real API with a dummy key and fail loudly -- never with a
  real key, and never with real fixture text (checks B and C read a SYNTHETIC source file).

  Checks, each printed PASS or FAIL:
    A  Real verify/ordercheck/Program.vb, no baseline file, a key present: the harness exits
       2 with EXIT_REASON=BASELINE_MISSING and the transport sees ZERO calls.
    B  Synthetic source (fixture-parser-synthetic.vb.txt), baseline present, version 1 says
       mechanism_declared_ok everywhere and version 2 says declared_but_contradicted on one
       item: exit 0 -- version 2 does not reach the exit code. Also checks FP-D27's two
       AMBIGUOUS_CALLEE sites, FP-D28's SITE_NAMED / BLOCK_ONLY labels, that version 2 is
       reported beside version 1, and that the request carried the item-8o sentence in
       exactly the two criteria the probe put it in, and nowhere in version 1.
    C  Same, but version 1 says undeclared on one item while version 2 says
       mechanism_declared_ok: exit 1 -- version 1 alone drives the exit code.
    D  One residual FP-1 site (second reader, 2026-09-24): exit 0, 15 calls (1 FP-1 item +
       1 FP-2 item judged as FP-2 and FP-2t, 5 samples each -- FP-D33/SR-D6), and the
       JEV_MODEL line.
    E  FP-Q1's OWN item-level baseline gate (second reader, 2026-09-24, SR-D1/FP-D32): a
       scope baseline covering one of two candidates refuses the whole run before any
       FP-Q1 call (BASELINE_INCOMPLETE, zero transport calls); -AllowUnbaselinedItems
       opts out and the run proceeds (30 calls: 10 scope + 10 FP-1 + 10 FP-2/FP-2t).

  Usage (from the repo root):
    powershell -NoProfile -File tools/checks/selftest/fixture-parser-selftest.ps1
  Exit 0 when every check passes, 1 otherwise.
#>
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$tool = Join-Path $repo 'tools\checks\fixture-parser.ps1'
$synthetic = Join-Path $PSScriptRoot 'fixture-parser-synthetic.vb.txt'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("fp-selftest-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tmp | Out-Null

$failures = 0
function Assert-That([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { "PASS  $name" } else { "FAIL  $name -- $detail"; $script:failures++ }
}

# The synthetic transport. $global: because the harness invokes it from inside its own
# script scope, where $script: would resolve to the harness, not to this file.
$global:FpSelftestCalls = 0
$global:FpSelftestMode = 'B'
$global:FpSelftestBodyChecks = New-Object System.Collections.Generic.List[string]
$transport = {
    param($bytes)
    $global:FpSelftestCalls++
    $b = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $qs = @($b.questions.PSObject.Properties.Name)
    if ($qs -contains 'verdict_v2') {
        $v1c = $b.questions.verdict.criteria
        $v2c = $b.questions.verdict_v2.criteria
        $withSentence = @($v2c.PSObject.Properties | Where-Object { ([string]$_.Value).Contains('Naming `matched_key` or its shipped value ONLY') } | ForEach-Object { $_.Name } | Sort-Object)
        $v1Clean = -not @($v1c.PSObject.Properties | Where-Object { ([string]$_.Value).Contains('Naming `matched_key` or its shipped value ONLY') }).Count
        $sameElsewhere = $true
        foreach ($p in $v1c.PSObject.Properties) {
            if ($withSentence -notcontains $p.Name -and [string]$v2c.($p.Name) -ne [string]$p.Value) { $sameElsewhere = $false }
        }
        $global:FpSelftestBodyChecks.Add("v2_sentence_in=[$($withSentence -join ',')] v1_clean=$v1Clean other_criteria_identical=$sameElsewhere same_instructions=$([string]$b.questions.verdict.instructions -eq [string]$b.questions.verdict_v2.instructions)")
        $p = [string]$b.state.param_name
        $v1 = 'mechanism_declared_ok'; $v2 = 'mechanism_declared_ok'
        if ($global:FpSelftestMode -eq 'B' -and $p -eq 'tfiWindowSize') { $v2 = 'declared_but_contradicted' }
        if ($global:FpSelftestMode -eq 'C' -and $p -eq 'threshold') { $v1 = 'undeclared' }
        $json = '{"answers":{"comment_declares_class":{"noul":0.9},"class_matches_evidence":{"noul":0.9},"verdict":{"choice":"' + $v1 + '","probabilities":{"' + $v1 + '":0.8}},"verdict_v2":{"choice":"' + $v2 + '","probabilities":{"' + $v2 + '":0.7}}},"usage":{"input_tokens":100,"output_tokens":10}}'
    } else {
        $json = '{"answers":{"name_matches_assertion":{"noul":0.9},"verdict":{"choice":"name_matches","probabilities":{"name_matches":0.9}}},"usage":{"input_tokens":100,"output_tokens":10}}'
    }
    return @{ Ok = $true; Response = ($json | ConvertFrom-Json); Error = $null; Status = 200; WafBlocked = $false }
}

$savedKey = $env:TYPESAFE_API_KEY
$env:TYPESAFE_API_KEY = 'selftest-dummy-key-not-a-real-key'
try {
    # ---- A: the no-baseline refusal on the REAL fixture file, with a key present --------
    $global:FpSelftestCalls = 0
    $outA = & $tool -BaselinePath (Join-Path $tmp 'no-such-baseline.json') -OutPath (Join-Path $tmp 'rA.md') -TestTransportOverride $transport 2>&1
    $exitA = $LASTEXITCODE
    Assert-That 'A exits 2' ($exitA -eq 2) "exit=$exitA"
    Assert-That 'A says BASELINE_MISSING' ([bool]($outA -match '^EXIT_REASON=BASELINE_MISSING$')) 'no EXIT_REASON=BASELINE_MISSING line'
    Assert-That 'A made zero transport calls' ($global:FpSelftestCalls -eq 0) "calls=$($global:FpSelftestCalls)"

    # ---- B: version 2 disagrees, version 1 is clean -> exit 0 ---------------------------
    $baselinePath = Join-Path $tmp 'baseline.json'
    Set-Content -Encoding UTF8 -Path $baselinePath -Value '{ "A902_SyntheticTfiNamed#18#tfiWindowSize": "mechanism_declared_ok", "A902_SyntheticTfiNamed#18#threshold": "mechanism_declared_ok", "A902_SyntheticTfiNamed": "name_matches" }'
    $global:FpSelftestCalls = 0; $global:FpSelftestMode = 'B'; $global:FpSelftestBodyChecks.Clear()
    $outB = & $tool -SourceFile $synthetic -BaselinePath $baselinePath -OutPath (Join-Path $tmp 'rB.md') -TestTransportOverride $transport 2>&1
    $exitB = $LASTEXITCODE
    $txtB = ($outB | ForEach-Object { [string]$_ }) -join "`n"
    Assert-That 'B exits 0 (v2 disagreement does not reach the exit code)' ($exitB -eq 0) "exit=$exitB"
    # FP-D33 (SR-D6, second reader 2026-09-24): FP-2t now rides beside FP-2 in the same run,
    # so the 1 FP-2 item costs 10 calls (5 FP-2 + 5 FP-2t), not 5 -- 20 total, not 15.
    Assert-That 'B made 20 transport calls (2 FP-1 items x5 + 1 FP-2 item x5 FP-2 + x5 FP-2t)' ($global:FpSelftestCalls -eq 20) "calls=$($global:FpSelftestCalls)"
    Assert-That 'B MAPPING_AMBIGUOUS_CALLEE_SITES=2' ($txtB -match '(?m)^MAPPING_AMBIGUOUS_CALLEE_SITES=2$') 'counter missing or not 2'
    Assert-That 'B Snapshot and Compute listed as ambiguous' (($txtB -match 'CALLEE_MULTI_DECLARED Snapshot .*ambiguous=1 .*AMBIGUOUS_CALLEE') -and ($txtB -match 'CALLEE_MULTI_DECLARED Compute .*ambiguous=1 .*AMBIGUOUS_CALLEE')) 'CALLEE_MULTI_DECLARED lines wrong'
    Assert-That 'B IN_SCOPE_MARKED_SITE_NAMED=1 and IN_SCOPE_MARKED_BLOCK_ONLY=1' (($txtB -match '(?m)^IN_SCOPE_MARKED_SITE_NAMED=1$') -and ($txtB -match '(?m)^IN_SCOPE_MARKED_BLOCK_ONLY=1$')) 'marker counters wrong'
    Assert-That 'B tfiWindowSize is SITE_NAMED, threshold is BLOCK_ONLY' (($txtB -match '#18#tfiWindowSize .*marker=SITE_NAMED') -and ($txtB -match '#18#threshold .*marker=BLOCK_ONLY')) 'marker labels wrong'
    Assert-That 'B both FP-1 items were judged (BLOCK_ONLY is a label, not a filter)' (@($outB | Where-Object { [string]$_ -match '^  A902_SyntheticTfiNamed#18#.* verdict=' }).Count -eq 2) 'expected 2 FP1_RESULTS rows'
    Assert-That 'B v2 reported beside v1' (($txtB -match 'v2: verdict=declared_but_contradicted agreement_rate=1') -and ($txtB -match 'FP1V2_ITEMS_SAME_PLURALITY_AS_V1=1 of 2')) 'v2 line or counter missing'
    Assert-That 'B FP1_BAD_VERDICTS=0' ($txtB -match '(?m)^FP1_BAD_VERDICTS=0$') 'FP1_BAD_VERDICTS not 0'
    $distinctBody = @($global:FpSelftestBodyChecks | Select-Object -Unique)
    Assert-That 'B request body: sentence in exactly declared_but_contradicted + mechanism_declared_ok, v1 untouched' (($distinctBody.Count -eq 1) -and ($distinctBody[0] -eq 'v2_sentence_in=[declared_but_contradicted,mechanism_declared_ok] v1_clean=True other_criteria_identical=True same_instructions=True')) "saw: $($distinctBody -join ' | ')"

    # ---- C: version 1 flags, version 2 is clean -> exit 1 ------------------------------
    $global:FpSelftestCalls = 0; $global:FpSelftestMode = 'C'
    $outC = & $tool -SourceFile $synthetic -BaselinePath $baselinePath -OutPath (Join-Path $tmp 'rC.md') -TestTransportOverride $transport 2>&1
    $exitC = $LASTEXITCODE
    $txtC = ($outC | ForEach-Object { [string]$_ }) -join "`n"
    Assert-That 'C exits 1 (version 1 alone drives the exit code)' ($exitC -eq 1) "exit=$exitC"
    Assert-That 'C FP1_BAD_VERDICTS=1 while v2 said mechanism_declared_ok' (($txtC -match '(?m)^FP1_BAD_VERDICTS=1$') -and ($txtC -match '#18#threshold \[STABLE\] verdict=undeclared') -and ($txtC -match 'v2: verdict=mechanism_declared_ok')) 'C lines wrong'

    # ---- D (second reader, 2026-09-24 UTC): ONE residual FP-1 site, and the model line ----
    # A copy of the synthetic source with `threshold:=0.61` removed from line 18, so exactly
    # one FP-1 site remains -- the single-element path of $fp1JudgedSites (sweep RT-U4). The
    # line count is unchanged, so the item id keeps `#18#`. The synthetic transport's JSON
    # carries no `model`, so JEV_MODEL must say UNAVAILABLE, never invent a version.
    $oneSite = Join-Path $tmp 'synthetic-one-site.vb.txt'
    $synLines = [System.IO.File]::ReadAllLines($synthetic, [System.Text.Encoding]::UTF8)
    $synLines[17] = $synLines[17].Replace(', threshold:=0.61', '')
    [System.IO.File]::WriteAllLines($oneSite, $synLines, (New-Object System.Text.UTF8Encoding($false)))
    $global:FpSelftestCalls = 0; $global:FpSelftestMode = 'B'
    $outD = & $tool -SourceFile $oneSite -BaselinePath $baselinePath -OutPath (Join-Path $tmp 'rD.md') -TestTransportOverride $transport 2>&1
    $exitD = $LASTEXITCODE
    $txtD = ($outD | ForEach-Object { [string]$_ }) -join "`n"
    # FP-D33 (SR-D6): the 1 FP-2 item now costs 10 calls (FP-2 + FP-2t), so 1 FP-1 (5) + 1
    # FP-2 item (10) = 15 total, and JEV_MODEL now tallies 15 requests, not 10.
    Assert-That 'D one residual site: exit 0, 15 calls (1 FP-1 + 1 FP-2 item as FP-2/FP-2t, 5 samples each)' (($exitD -eq 0) -and ($global:FpSelftestCalls -eq 15)) "exit=$exitD calls=$($global:FpSelftestCalls)"
    Assert-That 'D exactly one FP-1 row judged' (@($outD | Where-Object { [string]$_ -match '^  A902_SyntheticTfiNamed#18#.* verdict=' }).Count -eq 1) 'expected 1 FP-1 row'
    Assert-That 'D JEV_MODEL line: 15 requested, resolved UNAVAILABLE' ($txtD -match '(?m)^JEV_MODEL requested=\[jev-latest x15\] resolved=\[UNAVAILABLE x15\] run_utc=') (($outD | ForEach-Object { [string]$_ } | Where-Object { $_ -like 'JEV_MODEL*' }) -join ' / ')

    # ---- E (second reader, 2026-09-24 UTC, SR-D1): FP-Q1's OWN item-level baseline gate --
    # the FP-D24 pattern applied to the scope filter (docs/jev-harnesses-second-reader-
    # 2026-09-24.md section 6). The synthetic source's needsJevParams population is 62:
    # minCoverageSec + currentATR (A901, ambiguous callees) plus f00..f59 (A903's Filler
    # padding, no production call site) -- probed directly (SCOPE_CANDIDATES) rather than
    # assumed. The baseline below covers 61 of the 62, missing ONLY currentATR, so this is
    # a clean single-item-missing probe. No key needed for the refusal itself.
    $scopeBaselinePartial = Join-Path $tmp 'scope-baseline-partial.json'
    $sbCovered = @{ minCoverageSec = 'input' }
    for ($fi = 0; $fi -lt 60; $fi++) { $sbCovered["f$($fi.ToString('00'))"] = 'input' }
    ($sbCovered | ConvertTo-Json) | Set-Content -Encoding UTF8 -Path $scopeBaselinePartial

    # E1: covers 61 of 62 candidates (missing currentATR only) -> refuse before any FP-Q1 call.
    $global:FpSelftestCalls = 0
    $outE1 = & $tool -SourceFile $synthetic -BaselinePath (Join-Path $tmp 'no-such-baseline-e1.json') -ScopeBaselinePath $scopeBaselinePartial -OutPath (Join-Path $tmp 'rE1.md') -TestTransportOverride $transport 2>&1
    $exitE1 = $LASTEXITCODE
    $txtE1 = ($outE1 | ForEach-Object { [string]$_ }) -join "`n"
    Assert-That 'E1 exits 2' ($exitE1 -eq 2) "exit=$exitE1"
    Assert-That 'E1 says BASELINE_INCOMPLETE' ([bool]($outE1 -match '^EXIT_REASON=BASELINE_INCOMPLETE$')) 'no EXIT_REASON=BASELINE_INCOMPLETE line'
    Assert-That 'E1 SCOPE_UNBASELINED_ITEMS=1, names currentATR' (($txtE1 -match '(?m)^SCOPE_UNBASELINED_ITEMS=1 ') -and ($txtE1 -match '(?m)^\s+currentATR\s')) 'counter or named param missing'
    Assert-That 'E1 made zero transport calls' ($global:FpSelftestCalls -eq 0) "calls=$($global:FpSelftestCalls)"

    # E2: -AllowUnbaselinedItems opts out -- FP-Q1 then DOES call every one of the 62
    # candidates (62 x 5 samples = 310), and the run proceeds past the scope gate to
    # FP-1/FP-2 (baseline.json from check B already covers A902's items, so those gates
    # pass too): 310 scope + 10 FP-1 + 10 FP-2/FP-2t = 330.
    $global:FpSelftestCalls = 0; $global:FpSelftestMode = 'B'
    $outE2 = & $tool -SourceFile $synthetic -BaselinePath $baselinePath -ScopeBaselinePath $scopeBaselinePartial -AllowUnbaselinedItems -OutPath (Join-Path $tmp 'rE2.md') -TestTransportOverride $transport 2>&1
    $exitE2 = $LASTEXITCODE
    $txtE2 = ($outE2 | ForEach-Object { [string]$_ }) -join "`n"
    Assert-That 'E2 -AllowUnbaselinedItems opts out (no BASELINE_INCOMPLETE)' (-not ($outE2 -match '^EXIT_REASON=BASELINE_INCOMPLETE$')) 'refusal fired anyway'
    Assert-That 'E2 SCOPE_JEV_CALLS=310 (62 candidates x 5 samples)' ($txtE2 -match '(?m)^SCOPE_JEV_CALLS=310$') 'scope call count wrong'
    Assert-That 'E2 exits 0 (mode B: v1 clean)' ($exitE2 -eq 0) "exit=$exitE2"
    Assert-That 'E2 made 330 transport calls (310 scope + 10 FP-1 + 10 FP-2/FP-2t)' ($global:FpSelftestCalls -eq 330) "calls=$($global:FpSelftestCalls)"

    ''
    'Selected harness output, check B:'
    $outB | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^(MAPPING_AMBIGUOUS_CALLEE_SITES|  CALLEE_MULTI_DECLARED|MARKED_SITES_|IN_SCOPE_MARKED_|  A902_\S+ \[|      v2:|FP1_BAD_VERDICTS|FP1V2_)' } | Select-Object -Unique
    "EXIT_B=$exitB"
    'Selected harness output, check C:'
    $outC | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^(  A902_\S+ \[|      v2:|FP1_BAD_VERDICTS|FP1V2_)' } | Select-Object -Unique
    "EXIT_C=$exitC"
} finally {
    $env:TYPESAFE_API_KEY = $savedKey
    Remove-Item -Recurse -Force -Path $tmp -ErrorAction SilentlyContinue
}

''
if ($failures -eq 0) { 'SELFTEST PASSED'; exit 0 } else { "SELFTEST FAILED ($failures check(s))"; exit 1 }
