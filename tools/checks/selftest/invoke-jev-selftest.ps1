#requires -Version 5.1
<#
  tools/checks/selftest/invoke-jev-selftest.ps1 -- offline self-test for the shared call site,
  tools/checks/lib/InvokeJev.ps1. NO NETWORK: every attempt goes through -TransportOverride.
  Added 2026-09-24 (UTC) by the second reader. It commits the retry matrix that
  docs/harnesses-1-2-arming-spec-back.md section 1 H-2 ran from an uncommitted scratch script,
  so that handle becomes runnable, and it covers REVISION 2's model tally.

  Checks: A transient-then-success retries twice and succeeds · B a 403 WAF block is never
  retried · C a 400 is never retried · D an always-transient failure stops after MaxRetries ·
  E the model tally counts every requested call and the resolved model of each success, and
  says UNAVAILABLE for a success whose response carries no model.

  Usage (repo root):  powershell -NoProfile -File tools/checks/selftest/invoke-jev-selftest.ps1
#>
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot '..\lib\InvokeJev.ps1')
$failures = 0
function Assert-That([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { "PASS  $name" } else { "FAIL  $name -- $detail"; $script:failures++ }
}
$okResp = { param($m) $r = @{ answers = @{}; usage = @{ input_tokens = 1 } }; if ($m) { $r.model = $m }; @{ Ok = $true; Response = [PSCustomObject]$r; Error = $null; Status = 200; WafBlocked = $false } }
$body = @{ model = 'jev-latest'; state = @{}; questions = @{} }

$global:IjN = 0
$r = Invoke-Jev 'k' $body 2 0 { param($b) $global:IjN++; if ($global:IjN -le 2) { @{ Ok = $false; Response = $null; Error = '503'; Status = 503; WafBlocked = $false } } else { & $okResp 'jev-9.9.9' } }
Assert-That 'A transient then success' ($r.Ok -and $r.RetryCount -eq 2 -and $global:IjN -eq 3 -and $r.Model -eq 'jev-9.9.9') "ok=$($r.Ok) retries=$($r.RetryCount) calls=$global:IjN model=$($r.Model)"

$global:IjN = 0
$r = Invoke-Jev 'k' $body 2 0 { param($b) $global:IjN++; @{ Ok = $false; Response = $null; Error = '403'; Status = 403; WafBlocked = $true } }
Assert-That 'B WAF block never retried' ((-not $r.Ok) -and $r.WafBlocked -and $r.RetryCount -eq 0 -and $global:IjN -eq 1) "calls=$global:IjN"

$global:IjN = 0
$r = Invoke-Jev 'k' $body 2 0 { param($b) $global:IjN++; @{ Ok = $false; Response = $null; Error = '400'; Status = 400; WafBlocked = $false } }
Assert-That 'C 400 never retried' ((-not $r.Ok) -and $r.RetryCount -eq 0 -and $global:IjN -eq 1) "calls=$global:IjN"

$global:IjN = 0
$r = Invoke-Jev 'k' $body 2 0 { param($b) $global:IjN++; @{ Ok = $false; Response = $null; Error = 'timeout'; Status = $null; WafBlocked = $false } }
Assert-That 'D always transient stops after MaxRetries' ((-not $r.Ok) -and $r.RetryCount -eq 2 -and $global:IjN -eq 3) "calls=$global:IjN retries=$($r.RetryCount)"

$r = Invoke-Jev 'k' $body 0 0 { param($b) & $okResp $null }
$line = Get-JevModelLine
Assert-That 'E model tally' ($r.Model -eq 'UNAVAILABLE' -and $line -match '^JEV_MODEL requested=\[jev-latest x5\] resolved=\[jev-9\.9\.9 x1, UNAVAILABLE x1\] run_utc=\d{4}-\d\d-\d\dT') $line
Remove-Variable -Scope Global -Name IjN -ErrorAction SilentlyContinue
if ($failures -eq 0) { 'SELFTEST PASSED'; exit 0 } else { "SELFTEST FAILED ($failures)"; exit 1 }
