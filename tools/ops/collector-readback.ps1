#requires -Version 5.1
<#
  tools/ops/collector-readback.ps1 -- READ-ONLY health read-back of the AWS collector box over SSM.

  Added 2026-09-25 for post-deploy checks and seat-start probe checks. Every remote command below only
  READS: Get-ChildItem, Get-Content, Import-Csv, Get-Process, Get-CimInstance, Get-Counter, Get-PSDrive.
  It stops nothing, starts nothing, writes no file on the box and touches no S3 object.
  ⛔ Keep it that way: a write belongs in collector.ps1 (deploy/fetch), never here. This script is
  allow-listed for auto mode in .claude/settings.local.json ON THE BASIS that it is read-only.

  Prints: the analysis_log.csv* files, the header width, the last rows (InstanceId, SignalId,
  TriggerMode, WsHealth, SettingsVersion ...), the absorption_episodes.log and repair_status.log tails,
  the ws_health.log tail, the run_errors.log line count and tail (C-8, docs/collector-halt-fixes-spec.md),
  the box watchdog's WATCHDOG_TAIL (last 3 lines of watchdog.log, or ABSENT), its task state and its pause
  marker age (docs/collector-watchdog-spec.md section 2.5 -- reads only: Get-ScheduledTask, Get-ScheduledTaskInfo,
  Get-Item), memory/commit/paging, and the liquidation probe's status line if it runs.

  Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-readback.ps1 -InstanceId i-0d6c133058876273e
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstanceId,
    [string]$Region = 'eu-west-2',
    [int]$Rows = 3,
    [int]$TimeoutSec = 300
)
$ErrorActionPreference = 'Continue'

$cmds = @(
 "'UTC=' + (Get-Date).ToUniversalTime().ToString('u')",
 'Get-ChildItem C:\DeribitEngine -File -Filter analysis_log.csv* | ForEach-Object { ''FILE '' + $_.Name + '' '' + $_.Length }',
 '$h = Get-Content C:\DeribitEngine\analysis_log.csv -TotalCount 1; ''HEADER_COLS='' + ($h -split '','').Count',
 ('$csv = Import-Csv C:\DeribitEngine\analysis_log.csv; ''ROWS='' + $csv.Count; $csv | Select-Object -Last ' + $Rows + ' | ForEach-Object { ''ROW ts='' + $_.Timestamp + '' iid='' + $_.InstanceId + '' sid='' + $_.SignalId + '' verdict='' + $_.Verdict + '' trig='' + $_.TriggerMode + '' ws='' + $_.WsHealth + '' sv='' + $_.SettingsVersion + '' sle='' + $_.SettingsLoadError + '' rtc='' + $_.RecentTradeCount + '' vpfr='' + $_.VPFRSignal }'),
 '$ids = Import-Csv C:\DeribitEngine\analysis_log.csv | Group-Object InstanceId | ForEach-Object { ''INSTANCE '' + $_.Name + '' rows='' + $_.Count + '' first='' + ($_.Group | Select-Object -First 1).Timestamp }; $ids',
 '''EPISODES_TAIL''; if (Test-Path C:\DeribitEngine\absorption_episodes.log) { Get-Content C:\DeribitEngine\absorption_episodes.log -Tail 2 | ForEach-Object { $_.Substring(0, [math]::Min(200, $_.Length)) } } else { ''ABSENT'' }',
 '''REPAIR_TAIL''; if (Test-Path C:\DeribitEngine\repair_status.log) { Get-Content C:\DeribitEngine\repair_status.log -Tail 3 | ForEach-Object { $_.Substring(0, [math]::Min(220, $_.Length)) } }',
 '''WSHEALTH_TAIL''; Get-Content C:\DeribitEngine\ws_health.log -Tail 3',
 '''RUNERR_TAIL''; if (Test-Path C:\DeribitEngine\run_errors.log) { ''RUNERR_LINES='' + @(Get-Content C:\DeribitEngine\run_errors.log).Count; Get-Content C:\DeribitEngine\run_errors.log -Tail 3 | ForEach-Object { $_.Substring(0, [math]::Min(220, $_.Length)) } } else { ''ABSENT (no run error logged since the C-8 build)'' }',
 '''WATCHDOG_TAIL''; if (Test-Path C:\DeribitEngine\watchdog.log) { Get-Content C:\DeribitEngine\watchdog.log -Tail 3 | ForEach-Object { $_.Substring(0, [math]::Min(220, $_.Length)) } } else { ''ABSENT'' }; $wt = Get-ScheduledTask -TaskName DeribitCollectorWatchdog -ErrorAction SilentlyContinue; if ($wt) { $wi = $wt | Get-ScheduledTaskInfo -ErrorAction SilentlyContinue; ''WATCHDOG_TASK state='' + $wt.State + '' logon='' + $wt.Principal.LogonType + '' last='' + $wi.LastRunTime + '' result='' + $wi.LastTaskResult + '' next='' + $wi.NextRunTime } else { ''WATCHDOG_TASK=ABSENT'' }; if (Test-Path C:\DeribitEngine\watchdog.pause) { ''WATCHDOG_PAUSE age_min='' + [math]::Round(((Get-Date).ToUniversalTime() - (Get-Item C:\DeribitEngine\watchdog.pause).LastWriteTimeUtc).TotalMinutes, 1) } else { ''WATCHDOG_PAUSE=none'' }',
 '''TASKS (non-Microsoft)''; Get-ScheduledTask | Where-Object { $_.TaskPath -notlike ''\Microsoft\*'' } | ForEach-Object { $i = $_ | Get-ScheduledTaskInfo -ErrorAction SilentlyContinue; ''TASK '' + $_.TaskPath + $_.TaskName + '' state='' + $_.State + '' run='' + (($_.Actions | ForEach-Object { $_.Execute + '' '' + $_.Arguments }) -join '';'') + '' triggers='' + (($_.Triggers | ForEach-Object { $_.CimClass.CimClassName -replace ''MSFT_Task'','''' }) -join '','') + '' last='' + $i.LastRunTime + '' result='' + $i.LastTaskResult }',
 '''STARTUP''; foreach ($p in @(''C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp'') + @(Get-ChildItem C:\Users -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName ''AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup'' })) { if (Test-Path $p) { Get-ChildItem $p -File | ForEach-Object { ''STARTUP_ITEM '' + $_.FullName } } }',
 '$w = Get-ItemProperty ''HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'' -ErrorAction SilentlyContinue; ''AUTOLOGON='' + $w.AutoAdminLogon + '' USER='' + $w.DefaultUserName',
 '$a = @(Get-Process DeribitVerdictEngine -ErrorAction SilentlyContinue); ''APP_COUNT='' + $a.Count; foreach ($x in $a) { ''APP PID='' + $x.Id + '' PRIV_MB='' + [math]::Round($x.PrivateMemorySize64/1MB) + '' THREADS='' + $x.Threads.Count + '' START='' + $x.StartTime.ToUniversalTime().ToString(''u'') }',
 '$os = Get-CimInstance Win32_OperatingSystem; ''MEM_FREE_MB='' + [math]::Round($os.FreePhysicalMemory/1KB) + '' COMMIT_FREE_MB='' + [math]::Round($os.FreeVirtualMemory/1KB) + '' COMMIT_LIMIT_MB='' + [math]::Round($os.TotalVirtualMemorySize/1KB) + '' DISK_FREE_GB='' + [math]::Round((Get-PSDrive C).Free/1GB,2)',
 '$c = Get-Counter ''\Memory\Pages Output/sec'',''\Memory\Pages Input/sec'' -SampleInterval 2 -MaxSamples 3; $c.CounterSamples | Group-Object Path | ForEach-Object { ''CTR '' + $_.Name + '' mean='' + [math]::Round(($_.Group | Measure-Object CookedValue -Average).Average,1) }',
 '$pf = ''C:\probe-runs\liq-2026-09-24\probe.pid''; if (Test-Path $pf) { $pp = Get-Process -Id (Get-Content $pf) -ErrorAction SilentlyContinue; if ($pp) { ''PROBE ALIVE PID='' + $pp.Id + '' PRIV_MB='' + [math]::Round($pp.PrivateMemorySize64/1MB) } else { ''PROBE=NOT RUNNING'' }; Get-Content C:\probe-runs\liq-2026-09-24\probe-console.log -Tail 1 } else { ''PROBE=NO PID FILE'' }'
)

$tmp = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($tmp, (@{ commands = $cmds } | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding $false))
    $id = aws ssm send-command --instance-ids $InstanceId --region $Region --document-name AWS-RunPowerShellScript `
          --parameters "file://$tmp" --query Command.CommandId --output text
    if (-not $id) { Write-Output 'READBACK_FAILED send-command returned no id'; exit 2 }
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep -Seconds 8
        $r = aws ssm get-command-invocation --command-id $id --instance-id $InstanceId --region $Region --output json 2>$null | ConvertFrom-Json
    } while ((-not $r -or $r.Status -in 'Pending', 'InProgress', 'Delayed') -and (Get-Date) -lt $deadline)
    Write-Output ('SSM_STATUS=' + $(if ($r) { $r.Status } else { 'NO_RESULT' }))
    if ($r) { Write-Output $r.StandardOutputContent; if ($r.StandardErrorContent) { Write-Output ('STDERR: ' + $r.StandardErrorContent) } }
    if (-not $r -or $r.Status -ne 'Success') { exit 1 }
} finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
