#requires -Version 5.1
<#
  tools/checks/selftest/decision-bias-runner-selftest.ps1 -- offline self-test for harness 6's
  runner, tools/checks/measure/decision-bias/run-decision-bias.ps1 (second reader, 2026-09-24 UTC).
  NO NETWORK, NO REAL JEV CALL: every call goes through -TestTransportOverride. The input is the
  committed SYNTHETIC window (measure/decision-bias/synthetic/), 4 items, never a real decision.

  Checks, each printed PASS or FAIL:
    A  A firewall block on the SECOND sample of item 2 (call 7 of the run) no longer aborts the
       run: exit 0, the item is WAF_BLOCKED with the one earlier sample's tokens (11), and the
       other three items are judged. Before the fix, Measure-Object -Property over hashtables
       threw here under ErrorActionPreference=Stop.
    B  The output JSON carries jev_model: 17 requested, 16 resolved to the synthetic model.

  Usage (repo root):  powershell -NoProfile -File tools/checks/selftest/decision-bias-runner-selftest.ps1
  Exit 0 when every check passes, 1 otherwise.
#>
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$tool = Join-Path $repo 'tools\checks\measure\decision-bias\run-decision-bias.ps1'
$syn = Join-Path $repo 'tools\checks\measure\decision-bias\synthetic'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("db-selftest-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tmp | Out-Null
$failures = 0
function Assert-That([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { "PASS  $name" } else { "FAIL  $name -- $detail"; $script:failures++ }
}
$global:DbCalls = 0
$transport = {
    param($bytes)
    $global:DbCalls++
    if ($global:DbCalls -eq 7) { return @{ Ok = $false; Response = $null; Error = 'synthetic 403'; Status = 403; WafBlocked = $true } }
    $json = '{"model":"jev-selftest-0.0.2","answers":{"verdict":{"type":"choice","choice":"no_richer_option","probabilities":{"no_richer_option":0.8}}},"usage":{"input_tokens":11,"output_tokens":1}}'
    return @{ Ok = $true; Response = ($json | ConvertFrom-Json); Error = $null; Status = 200; WafBlocked = $false }
}
$savedKey = $env:TYPESAFE_API_KEY
$env:TYPESAFE_API_KEY = 'selftest-dummy-key-not-real'
try {
    $outJson = Join-Path $tmp 'jev.json'
    $out = @(& $tool -Population (Join-Path $syn 'synthetic-population.json') -Baseline (Join-Path $syn 'synthetic-baseline.json') -OutPath $outJson -Samples 5 -CountersOnly -TestTransportOverride $transport *>&1 | ForEach-Object { [string]$_ })
    $rc = $LASTEXITCODE
    $j = if (Test-Path $outJson) { [System.IO.File]::ReadAllText($outJson) | ConvertFrom-Json } else { $null }
    $s2 = if ($j) { @($j.items | Where-Object { $_.id -eq 'synthetic/S2-mixed-sequence-span' }) | Select-Object -First 1 } else { $null }
    $judged = if ($j) { @($j.items | Where-Object { $_.verdict -eq 'no_richer_option' }).Count } else { -1 }
    Assert-That 'A mid-item firewall block does not abort the run' ($rc -eq 0 -and $null -ne $j) "exit=$rc; last lines: $(($out | Select-Object -Last 3) -join ' / ')"
    Assert-That 'A blocked item recorded with its earlier sample tokens' ($null -ne $s2 -and $s2.verdict -eq 'WAF_BLOCKED' -and [int]$s2.usage_input_tokens -eq 11) "s2=$($s2 | ConvertTo-Json -Compress)"
    Assert-That 'A the other three items judged' ($judged -eq 3) "judged=$judged"
    Assert-That 'B jev_model: 17 requested, 16 resolved' ($null -ne $j -and [string]$j.jev_model -match '^requested=\[jev-latest x17\] resolved=\[jev-selftest-0\.0\.2 x16\] run_utc=') "jev_model=$($j.jev_model)"
} finally {
    $env:TYPESAFE_API_KEY = $savedKey
    Remove-Item -Recurse -Force -Path $tmp -ErrorAction SilentlyContinue
    Remove-Variable -Scope Global -Name DbCalls -ErrorAction SilentlyContinue
}
if ($failures -eq 0) { 'SELFTEST PASSED'; exit 0 } else { "SELFTEST FAILED ($failures)"; exit 1 }
