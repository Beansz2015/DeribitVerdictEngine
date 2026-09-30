#requires -Version 5.1
<#
  tools/ops/collector-watchdog.tests.ps1 -- OFFLINE tests for tools/ops/collector-watchdog.ps1
  (docs/collector-watchdog-spec.md section 4.1). No box, no AWS, no network.

    T1-T9  one case per decision rule of Get-WatchdogDecision (the spec's table, verbatim inputs).
    L1     R3's restart count read back from a sample watchdog.log: 23 h 59 min counted,
           24 h 1 min not; *_FAILED lines and a mention inside a detail are not restarts.
    C1-C2  the last-row reader: a torn last line is skipped; a header-only file reads as none.
    K1-K6  whole ticks against a STAND-IN process (a copy of ping.exe named WdTestApp.exe, hidden),
           in a temp folder: launch-and-count, restart, pause start/end, cap logged once, a launch
           that starts nothing (trap 2), and a tick that never throws.

  Each case names the input that fails it, and the spec-back records one mutation per case seen red.
  Prints PASS/FAIL per case. Exit code: 0 all pass, 1 any failure.

  Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-watchdog.tests.ps1
#>
$ErrorActionPreference = 'Stop'
$wdScript = Join-Path $PSScriptRoot 'collector-watchdog.ps1'

$tmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('wdtest-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
$testExe = Join-Path $tmpRoot 'WdTestApp.exe'
Copy-Item (Join-Path $env:SystemRoot 'System32\PING.EXE') $testExe

# Dot-source: defines the functions and the constants, runs NO tick. Every path points into $tmpRoot.
. $wdScript -InstallDir $tmpRoot -ProcessName 'WdTestApp' -ExePath $testExe `
    -ExeArguments '-n 600 127.0.0.1' -LaunchWindowStyle Hidden
# The box runs the tick under the default 'Continue'. The dot-sourced functions inherit this
# scope's preference, so match the box rather than test a stricter mode than production runs.
$ErrorActionPreference = 'Continue'

$script:fails = 0
$script:passes = 0
function Check([string]$Id, [string]$What, $Expected, $Actual) {
    if ("$Expected" -eq "$Actual") { $script:passes++; Write-Host ("PASS  {0,-4} {1}  (got {2})" -f $Id, $What, $Actual) }
    else { $script:fails++; Write-Host ("FAIL  {0,-4} {1}  expected [{2}] got [{3}]" -f $Id, $What, $Expected, $Actual) -ForegroundColor Red }
}

$now = [datetime]::SpecifyKind([datetime]'2026-10-01T12:00:00', [DateTimeKind]::Utc)
function Min([double]$m) { return $now.AddMinutes(-$m) }

try {
    # ---------------------------------------------------------------- T1-T9: the decision rule
    Write-Host '--- decision rule (Get-WatchdogDecision), spec section 4.1 ---'
    Check 'T1' 'marker 20 min old, 0 processes -> PAUSED (fails if R1 is dropped)' 'PAUSED' `
        (Get-WatchdogDecision -ProcessCount 0 -LastRowUtc $null -ProcessStartUtc $null -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes 20)
    Check 'T2' 'marker 60 min old, 0 processes -> LAUNCH (fails if the 45 min expiry is removed)' 'LAUNCH' `
        (Get-WatchdogDecision -ProcessCount 0 -LastRowUtc $null -ProcessStartUtc $null -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes 60)
    Check 'T3' '2 processes (started 2 h ago, last row 40 min old) -> AMBIGUOUS (fails if R4/R6 run on a count > 1)' 'AMBIGUOUS' `
        (Get-WatchdogDecision -ProcessCount 2 -LastRowUtc (Min 40) -ProcessStartUtc (Min 120) -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes $null)
    Check 'T4' '0 processes, 3 restarts in 24 h -> CAPPED (fails at cap 4)' 'CAPPED' `
        (Get-WatchdogDecision -ProcessCount 0 -LastRowUtc $null -ProcessStartUtc $null -NowUtc $now -RestartsInWindow 3 -PauseAgeMinutes $null)
    Check 'T5' '0 processes, 2 restarts in 24 h -> LAUNCH (fails at cap 2)' 'LAUNCH' `
        (Get-WatchdogDecision -ProcessCount 0 -LastRowUtc $null -ProcessStartUtc $null -NowUtc $now -RestartsInWindow 2 -PauseAgeMinutes $null)
    Check 'T6' '1 process started 5 min ago, last row 40 min old -> GRACE (fails if R5 is dropped)' 'GRACE' `
        (Get-WatchdogDecision -ProcessCount 1 -LastRowUtc (Min 40) -ProcessStartUtc (Min 5) -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes $null)
    Check 'T7' '1 process started 2 h ago, last row 31 min old -> RESTART (fails at threshold 35)' 'RESTART' `
        (Get-WatchdogDecision -ProcessCount 1 -LastRowUtc (Min 31) -ProcessStartUtc (Min 120) -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes $null)
    Check 'T8' '1 process started 2 h ago, last row 29 min old -> OK (fails at threshold 25)' 'OK' `
        (Get-WatchdogDecision -ProcessCount 1 -LastRowUtc (Min 29) -ProcessStartUtc (Min 120) -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes $null)
    Check 'T9' '1 process started 2 h ago, CSV unreadable -> RESTART (fails if unreadable reads as fresh)' 'RESTART' `
        (Get-WatchdogDecision -ProcessCount 1 -LastRowUtc $null -ProcessStartUtc (Min 120) -NowUtc $now -RestartsInWindow 0 -PauseAgeMinutes $null)

    # ---------------------------------------------------------------- L1: 24 h count from the log
    Write-Host '--- restart count read back from watchdog.log (WDG-3) ---'
    $f = 'yyyy-MM-ddTHH:mm:ssZ'
    $ic = [System.Globalization.CultureInfo]::InvariantCulture
    $sample = Join-Path $tmpRoot 'sample-watchdog.log'
    $sampleLines = @(
        ($now.AddMinutes(-(24 * 60 + 1)).ToString($f, $ic) + ' | LAUNCHED | pid=100 last_row=NONE'),          # 24 h 1 min: NOT counted
        ($now.AddMinutes(-(23 * 60 + 59)).ToString($f, $ic) + ' | RESTARTED | old_pid=100 new_pid=101 last_row=x'), # 23 h 59 min: counted
        ($now.AddMinutes(-120).ToString($f, $ic) + ' | RESTART_FAILED | stage=launch old_pid=101 count=0'),      # not a restart
        ($now.AddMinutes(-90).ToString($f, $ic) + ' | LAUNCH_FAILED | count=0 pids=NONE why=x'),                 # not a restart
        ($now.AddMinutes(-80).ToString($f, $ic) + ' | ERROR | something mentioned LAUNCHED in passing'),          # a mention, not an event
        'this line is malformed and must be skipped',
        ($now.AddMinutes(-60).ToString($f, $ic) + ' | LAUNCHED | pid=102 last_row=NONE'),                        # counted
        ($now.AddMinutes(-30).ToString($f, $ic) + ' | HEARTBEAT | decision=OK')
    )
    [System.IO.File]::WriteAllLines($sample, $sampleLines)
    $entries = Read-WatchdogLog -Path $sample
    Check 'L1' 'restarts in 24 h: 23h59m counted, 24h01m not, *_FAILED and mentions not (fails at a 25 h window or a LAUNCH* prefix match)' 2 `
        (Get-WatchdogRestartCount -Entries $entries -NowUtc $now)

    # ---------------------------------------------------------------- C1-C2: the last-row reader
    Write-Host '--- last analysis_log.csv row (WDG-5) ---'
    $csv1 = Join-Path $tmpRoot 'c1.csv'
    [System.IO.File]::WriteAllText($csv1, "Timestamp,Price,Verdict`r`n2026-10-01 11:50:00,1,NO TRADE`r`n2026-10-01 11:53:00,1,NO TRADE`r`n2026-10-01 11:5")
    Check 'C1' 'torn last line skipped, newest parseable row returned (fails if only the last line is parsed)' '2026-10-01T11:53:00Z' `
        (Format-WdUtc (Get-LastRowUtc -Path $csv1))
    $csv2 = Join-Path $tmpRoot 'c2.csv'
    [System.IO.File]::WriteAllText($csv2, "Timestamp,Price,Verdict`r`n")
    Check 'C2' 'header-only file reads as no row (-> R6 RESTART)' 'NONE' (Format-WdUtc (Get-LastRowUtc -Path $csv2))

    # ---------------------------------------------------------------- K1-K6: whole ticks
    Write-Host '--- whole ticks against the stand-in process WdTestApp.exe (hidden ping) ---'
    Stop-Process -Name WdTestApp -Force -ErrorAction SilentlyContinue
    $WdLaunchWaitSec = 5
    # Read-WatchdogLog returns its List as ONE object (the `,$list` idiom). Piping it straight into
    # Where-Object filters the whole list as a single item -- every count reads 1. Enumerate first.
    function LogEntries { $e = Read-WatchdogLog; $e }
    function EventCount([string]$ev) { return @(LogEntries | Where-Object { $_.Event -eq $ev }).Count }

    # K1 -- nothing running, no CSV: LAUNCH, then COUNT: exactly one process, its PID logged.
    $d = Invoke-WatchdogTick
    $p = @(Get-Process WdTestApp -ErrorAction SilentlyContinue)
    Check 'K1' 'tick with 0 processes -> LAUNCH; LAUNCHED logged with the PID of the one process now running' ("LAUNCH|1|1|pid=" + ($p | ForEach-Object { $_.Id })) `
        ("$d|$($p.Count)|$(EventCount 'LAUNCHED')|" + ((LogEntries | Where-Object { $_.Event -eq 'LAUNCHED' } | Select-Object -Last 1).Detail -split ' ')[0])
    Check 'K1b' 'first tick also logs START and HEARTBEAT exactly once' '1|1' ("$(EventCount 'START')|$(EventCount 'HEARTBEAT')")

    # K2 -- one process, last row 40 min old, grace forced to 0: RESTART -> a NEW pid, still exactly one.
    $WdGraceMin = 0
    $oldPid = $p[0].Id
    [System.IO.File]::WriteAllText($CsvPath, "Timestamp,Price`r`n" + [DateTime]::UtcNow.AddMinutes(-40).ToString('yyyy-MM-dd HH:mm:ss', $ic) + ",1`r`n")
    $d = Invoke-WatchdogTick
    $p = @(Get-Process WdTestApp -ErrorAction SilentlyContinue)
    $rl = (LogEntries | Where-Object { $_.Event -eq 'RESTARTED' } | Select-Object -Last 1)
    Check 'K2' 'stale row -> RESTART; old pid gone, one new pid, RESTARTED names both' ("RESTART|1|True|old_pid=$oldPid new_pid=" + ($p | ForEach-Object { $_.Id })) `
        ("$d|$($p.Count)|$($p[0].Id -ne $oldPid)|" + (($rl.Detail -split ' ')[0..1] -join ' '))
    $WdGraceMin = 10
    Check 'K1c' 'START and HEARTBEAT not repeated on a later tick in the same logon and hour' '1|1' ("$(EventCount 'START')|$(EventCount 'HEARTBEAT')")

    # K3 -- marker present, app killed: PAUSED, nothing launched; marker removed: PAUSED end, then LAUNCHED.
    Stop-Process -Name WdTestApp -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500
    Set-Content -LiteralPath $PausePath -Value 'test' -Encoding ascii
    $launchedBefore = EventCount 'LAUNCHED'
    $d1 = Invoke-WatchdogTick
    $d2 = Invoke-WatchdogTick
    $cnt = @(Get-Process WdTestApp -ErrorAction SilentlyContinue).Count
    $pausedStarts = @(LogEntries | Where-Object { $_.Event -eq 'PAUSED' -and $_.Detail -like 'start*' }).Count
    Check 'K3' 'marker + 0 processes, two ticks -> PAUSED twice, PAUSED start logged ONCE, nothing launched' "PAUSED|PAUSED|1|0|$launchedBefore" `
        "$d1|$d2|$pausedStarts|$cnt|$(EventCount 'LAUNCHED')"
    Remove-Item -LiteralPath $PausePath -Force
    $d = Invoke-WatchdogTick
    $ends = @(LogEntries | Where-Object { $_.Event -eq 'PAUSED' -and $_.Detail -like 'end marker_removed*' }).Count
    Check 'K3b' 'marker removed -> PAUSED end logged, then LAUNCH' "LAUNCH|1|$($launchedBefore + 1)" "$d|$ends|$(EventCount 'LAUNCHED')"

    # K4 -- the log now holds 3 restarts (LAUNCHED, RESTARTED, LAUNCHED): CAPPED; CAP_REACHED logged once.
    Stop-Process -Name WdTestApp -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500
    $d1 = Invoke-WatchdogTick
    $d2 = Invoke-WatchdogTick
    $cnt = @(Get-Process WdTestApp -ErrorAction SilentlyContinue).Count
    Check 'K4' '3 restarts in the log, 0 processes, two ticks -> CAPPED twice, CAP_REACHED once, nothing launched' 'CAPPED|CAPPED|1|0' `
        "$d1|$d2|$(EventCount 'CAP_REACHED')|$cnt"

    # K5 -- trap 2: the launch call succeeds but the process exits at once. Must log LAUNCH_FAILED.
    $LogPath = Join-Path $tmpRoot 'k5-watchdog.log'
    $ExeArguments = '-n 1 127.0.0.1'
    $WdLaunchWaitSec = 3
    $d = Invoke-WatchdogTick
    Check 'K5' 'launch whose process exits at once -> LAUNCH_FAILED count=0, no LAUNCHED (fails if the launch call is trusted)' 'LAUNCH|1|0' `
        "$d|$(EventCount 'LAUNCH_FAILED')|$(EventCount 'LAUNCHED')"

    # K6 -- an exception inside the tick is logged as ERROR and never escapes.
    $LogPath = Join-Path $tmpRoot 'k6-watchdog.log'
    $savedName = $ProcessName
    $ProcessName = ''   # Get-Process -Name '' is a parameter-binding error: it throws even under SilentlyContinue
    $d = 'THREW'
    try { $d = Invoke-WatchdogTick } catch { $d = 'THREW' }
    $ProcessName = $savedName
    Check 'K6' 'exception inside a tick -> returns ERROR, logs one ERROR line, does not throw' 'ERROR|1' "$d|$(EventCount 'ERROR')"
}
finally {
    Stop-Process -Name WdTestApp -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Remove-Item -Recurse -Force $tmpRoot -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host ("RESULT  {0} PASS  {1} FAIL" -f $script:passes, $script:fails)
if ($script:fails -gt 0) { exit 1 }
exit 0
