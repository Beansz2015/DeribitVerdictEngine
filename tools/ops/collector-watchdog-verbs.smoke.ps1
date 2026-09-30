#requires -Version 5.1
<#
  tools/ops/collector-watchdog-verbs.smoke.ps1 -- OFFLINE smoke test of the collector.ps1 changes for
  the box watchdog (docs/collector-watchdog-spec.md section 2.5): install-watchdog, uninstall-watchdog,
  and the pause marker in restart. NO BOX, NO AWS: a global `aws` FUNCTION shadows aws.exe (a function
  wins PowerShell command resolution) and answers every call from canned output. The script refuses to
  run if `aws` does not resolve to that stub.

  What it proves: every remote script these verbs send PARSES; the verbs take the right exit code on
  the success and failure arms; restart writes the pause marker BEFORE the stop and removes it AFTER
  the gate on success AND failure; plan-only runs send no write. What it cannot prove: anything the
  box does with those scripts -- that is live test section 4.2.

  Not covered: `deploy` (its pre-flight needs a clean, pushed tree and a Release build). Its marker
  handling has the same try/finally shape as restart's, read, not executed.

  Needs tools/ops/collector-watchdog.ps1 COMMITTED (install-watchdog refuses an uncommitted script).
  Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-watchdog-verbs.smoke.ps1
#>
$ErrorActionPreference = 'Continue'
$collector = Join-Path $PSScriptRoot 'collector.ps1'
$global:WdHash = (Get-FileHash (Join-Path $PSScriptRoot 'collector-watchdog.ps1') -Algorithm SHA256).Hash

$global:StubCalls = New-Object System.Collections.Generic.List[object]
$global:StubMode = @{}
function global:aws {
    $global:LASTEXITCODE = 0
    $a = @($args)
    if ($a[0] -eq 'ssm' -and $a[1] -eq 'send-command') {
        $path = $a[[array]::IndexOf($a, '--parameters') + 1] -replace '^file://', ''
        $script = ((Get-Content $path -Raw | ConvertFrom-Json).commands) -join "`n"
        $errs = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($script, [ref]$null, [ref]$errs)
        $id = 'stub-' + $global:StubCalls.Count
        $global:StubCalls.Add([pscustomobject]@{ Id = $id; Kind = (Get-StubKind $script); Script = $script; ParseErrors = $errs.Count })
        return ('{"Command":{"CommandId":"' + $id + '"}}')
    }
    if ($a[0] -eq 'ssm' -and $a[1] -eq 'get-command-invocation') {
        $id = $a[[array]::IndexOf($a, '--command-id') + 1]
        $call = $global:StubCalls | Where-Object { $_.Id -eq $id } | Select-Object -First 1
        return (@{ Status = 'Success'; StandardOutputContent = (Get-StubOutput $call.Kind); StandardErrorContent = '' } | ConvertTo-Json)
    }
    if ($a[0] -eq 's3') { $global:StubCalls.Add([pscustomobject]@{ Id = 's3'; Kind = 'S3_UPLOAD'; Script = ($a -join ' '); ParseErrors = 0 }); return '' }
    $global:LASTEXITCODE = 1
    return "stub aws: unexpected call: $($a -join ' ')"
}
function global:Get-StubKind([string]$s) {
    if ($s -match 'PAUSE_SET')                { return 'PAUSE_SET' }
    if ($s -match 'PAUSE_CLEARED')            { return 'PAUSE_CLEAR' }
    if ($s -match 'EXE_PRESENT')              { return 'INSTALL_PRE' }
    if ($s -match 'PLACED_HASH')              { return 'INSTALL_PLACE' }
    # Unregister BEFORE Register: -match is a substring test, and 'Unregister-...' contains 'register-...'.
    if ($s -match 'Unregister-ScheduledTask') { return 'UNINSTALL' }
    if ($s -match 'Register-ScheduledTask')   { return 'INSTALL_REGISTER' }
    if ($s -match 'Start-ScheduledTask')      { return 'INSTALL_TICK' }
    if ($s -match 'TASK_EXISTING')            { return 'UNINSTALL_PRE' }
    if ($s -match 'LAST_ROW=')                { return 'RESTART_PRE' }
    if ($s -match 'STOPPED=true')             { return 'STOP' }
    if ($s -match 'LAUNCHED=true')            { return 'LAUNCH' }
    if ($s -match 'GATE_PROC_COUNT')          { return 'GATE' }
    return 'UNKNOWN'
}
function global:Get-StubOutput([string]$kind) {
    $m = $global:StubMode
    switch ($kind) {
        'PAUSE_SET'        { if ($m.PauseFail) { return 'PAUSE_SET=false' } return 'PAUSE_SET=true' }
        'PAUSE_CLEAR'      { return 'PAUSE_CLEARED=true' }
        'INSTALL_PRE'      { return "EXE_PRESENT=True`nTASK_EXISTING=none`nSCRIPT_EXISTING=none`nAUTOLOGON=1 USER=administrator`nADMIN_SESSIONS=2`nAPP_COUNT=1`nPAUSE_MARKER=none" }
        'INSTALL_PLACE'    { return "PLACED_HASH=$global:WdHash" }
        'INSTALL_REGISTER' {
            $dur = ''; if ($m.Duration) { $dur = $m.Duration }
            return "REGISTERED=true`nTASK_STATE=Ready`nTASK_PRINCIPAL=administrator|Interactive`nTASK_MULTI=IgnoreNew`nTASK_LIMIT=PT5M`nTASK_TRIGGERS=LogonTrigger|administrator||;TimeTrigger||PT10M|$dur"
        }
        'INSTALL_TICK'     { $r = '0'; if ($m.TickResult) { $r = $m.TickResult }; return "TICK_RAN=True`nTICK_RESULT=$r`nTICK_STATE=Ready`nLOG 2026-10-01T00:00:00Z | START | src=logon" }
        'UNINSTALL_PRE'    { return 'TASK_EXISTING=Ready' }
        'UNINSTALL'        { return 'UNREGISTERED=true' }
        'RESTART_PRE'      { return "PROC_COUNT=1`nREMOTE_PID=123`nREMOTE_DIR=C:\DeribitEngine`nLAST_ROW=2026-10-01 00:00:00" }
        'STOP'             { return "STOPPED=true`nREMAINING=0" }
        'LAUNCH'           { if ($m.LaunchFail) { return "LAUNCH_COUNT=0`nLAUNCHED=false" } return "LAUNCH_COUNT=1`nLAUNCHED=true`nLAUNCH_SESSION=2" }
        'GATE'             { return "GATE_PROC_COUNT=1`nGATE_PID=124`nGATE_SESSION=2`nGATE_ROWS_AFTER=2`nGATE_SPAN_SEC=60" }
    }
    return ''
}

if ((Get-Command aws).CommandType -ne 'Function') { Write-Host 'REFUSING: aws does not resolve to the stub function'; exit 1 }

$script:fails = 0; $script:passes = 0
function Check([string]$Id, [string]$What, $Expected, $Actual) {
    if ("$Expected" -eq "$Actual") { $script:passes++; Write-Host ("PASS  {0,-4} {1}  (got {2})" -f $Id, $What, $Actual) }
    else { $script:fails++; Write-Host ("FAIL  {0,-4} {1}  expected [{2}] got [{3}]" -f $Id, $What, $Expected, $Actual) -ForegroundColor Red }
}
function Run([string]$Verb, [hashtable]$Mode, [switch]$Yes) {
    $global:StubCalls.Clear(); $global:StubMode = $Mode
    # A sentinel, so a run that never reaches `exit` (a binding error) cannot report a stale code.
    $global:LASTEXITCODE = -99
    $null = & $collector $Verb -InstanceId 'i-stub00000000000000' -Yes:$Yes *>&1
    $code = $LASTEXITCODE
    $kinds = ($global:StubCalls | ForEach-Object { $_.Kind }) -join ','
    $bad = @($global:StubCalls | Where-Object { $_.ParseErrors -gt 0 -or $_.Kind -eq 'UNKNOWN' }).Count
    return [pscustomobject]@{ Code = $code; Kinds = $kinds; Bad = $bad }
}

$r = Run 'install-watchdog' @{}
Check 'S1' 'install-watchdog, no -Yes -> exit 1, pre-flight only, no upload' '1|INSTALL_PRE|0' "$($r.Code)|$($r.Kinds)|$($r.Bad)"

$r = Run 'install-watchdog' @{} -Yes
Check 'S2' 'install-watchdog -Yes -> exit 0; pre, upload, place, register, tick; every remote script parses' '0|INSTALL_PRE,S3_UPLOAD,INSTALL_PLACE,INSTALL_REGISTER,INSTALL_TICK|0' "$($r.Code)|$($r.Kinds)|$($r.Bad)"
$reg = ($global:StubCalls | Where-Object { $_.Kind -eq 'INSTALL_REGISTER' }).Script
$regOk = ($reg -match "-LogonType Interactive") -and ($reg -match "-AtLogOn -User 'administrator'") -and ($reg -match '-MultipleInstances IgnoreNew') -and
         ($reg -match '-RepetitionInterval \(New-TimeSpan -Minutes 10\)') -and ($reg -notmatch '-RepetitionDuration') -and ($reg -match 'collector-watchdog\.ps1')
Check 'S2b' 'the register script names Interactive, at-logon administrator, IgnoreNew, 10 min, no duration cap' 'True' "$regOk"
$tick = ($global:StubCalls | Where-Object { $_.Kind -eq 'INSTALL_TICK' }).Script
Check 'S2c' 'the tick runs THROUGH the task (Start-ScheduledTask), never the script from SSM (session 0)' 'True|False' "$($tick -match 'Start-ScheduledTask')|$($tick -match 'collector-watchdog\.ps1')"

$r = Run 'install-watchdog' @{ Duration = 'P1D' } -Yes
Check 'S3' 'register read-back shows a 1-day repetition -> exit 2 (not indefinite)' '2' "$($r.Code)"

$r = Run 'install-watchdog' @{ TickResult = '2147946720' } -Yes
Check 'S4' 'the tick ran with a non-zero result (no session) -> exit 2' '2' "$($r.Code)"

$r = Run 'restart' @{}
Check 'S5' 'restart, no -Yes -> exit 1, no pause marker, no stop' '1|RESTART_PRE' "$($r.Code)|$($r.Kinds)"

$r = Run 'restart' @{} -Yes
Check 'S6' 'restart -Yes -> exit 0; marker BEFORE the stop, removed AFTER the gate' '0|RESTART_PRE,PAUSE_SET,STOP,LAUNCH,GATE,PAUSE_CLEAR|0' "$($r.Code)|$($r.Kinds)|$($r.Bad)"

$r = Run 'restart' @{ LaunchFail = $true } -Yes
Check 'S7' 'restart -Yes, relaunch fails -> exit 2 AND the marker is still removed (finally)' '2|RESTART_PRE,PAUSE_SET,STOP,LAUNCH,PAUSE_CLEAR' "$($r.Code)|$($r.Kinds)"

$r = Run 'restart' @{ PauseFail = $true } -Yes
Check 'S8' 'restart -Yes, marker not confirmed -> exit 1, the app is NOT stopped' '1|RESTART_PRE,PAUSE_SET' "$($r.Code)|$($r.Kinds)"

$r = Run 'uninstall-watchdog' @{}
Check 'S9' 'uninstall-watchdog, no -Yes -> exit 1, nothing unregistered' '1|UNINSTALL_PRE' "$($r.Code)|$($r.Kinds)"

$r = Run 'uninstall-watchdog' @{} -Yes
Check 'S10' 'uninstall-watchdog -Yes -> exit 0, unregistered and confirmed' '0|UNINSTALL_PRE,UNINSTALL|0' "$($r.Code)|$($r.Kinds)|$($r.Bad)"

Remove-Item Function:\aws, Function:\Get-StubKind, Function:\Get-StubOutput -ErrorAction SilentlyContinue
Write-Host ''
Write-Host ("RESULT  {0} PASS  {1} FAIL" -f $script:passes, $script:fails)
if ($script:fails -gt 0) { exit 1 }
exit 0
