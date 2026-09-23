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
    Assert-That 'B made 15 transport calls (2 FP-1 items + 1 FP-2 item, 5 samples each)' ($global:FpSelftestCalls -eq 15) "calls=$($global:FpSelftestCalls)"
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
