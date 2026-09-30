#requires -Version 5.1
<#
  tools/ops/collector-watchdog.ps1 -- the BOX-SIDE collector watchdog (docs/collector-watchdog-spec.md).

  Installed to C:\DeribitEngine\watchdog\collector-watchdog.ps1 by `collector.ps1 install-watchdog`,
  OUTSIDE the six-item deploy allowlist, so a deploy never touches it. Run by the scheduled task
  DeribitCollectorWatchdog: user administrator, logon type Interactive, at logon + every 10 min,
  no parallel instances (spec section 2.1, WDG-1).

  One run = one TICK:
    1. read the state: process count FIRST, then the pause marker (see Invoke-WatchdogTick for why
       that order closes the deploy race), the last analysis_log.csv row, the restart count;
    2. decide with the pure function Get-WatchdogDecision (spec section 2.2, rules R1-R7);
    3. act, and log to watchdog.log as `utc | event | detail` (spec section 2.3).

  A tick NEVER throws out: any exception is logged as ERROR and the script exits 0, so the next
  tick runs anyway. The restart count for R3 is READ BACK from watchdog.log (WDG-3) -- there is no
  state file.

  Dot-sourcing this file (`. .\collector-watchdog.ps1 -InstallDir ...`) defines the functions and
  runs NO tick. That is how tools/ops/collector-watchdog.tests.ps1 loads it.

  Keep this file ASCII: Windows PowerShell 5.1 reads a BOM-less file as the ANSI code page.
#>
[CmdletBinding()]
param(
    # The box defaults. Parameters only so the offline tests can point every path at a temp folder.
    [string]$InstallDir = 'C:\DeribitEngine',
    [string]$LogPath,
    [string]$CsvPath,
    [string]$PausePath,
    [string]$ExePath,
    [string]$ProcessName = 'DeribitVerdictEngine',
    # Tests only: arguments for a stand-in exe, and a hidden window so no test window appears.
    [string]$ExeArguments = '',
    [ValidateSet('Normal', 'Hidden', 'Minimized', 'Maximized')]
    [string]$LaunchWindowStyle = 'Normal'
)

if (-not $LogPath)   { $LogPath   = Join-Path $InstallDir 'watchdog.log' }
if (-not $CsvPath)   { $CsvPath   = Join-Path $InstallDir 'analysis_log.csv' }
if (-not $PausePath) { $PausePath = Join-Path $InstallDir 'watchdog.pause' }
if (-not $ExePath)   { $ExePath   = Join-Path $InstallDir 'DeribitVerdictEngine.exe' }

# ---------------------------------------------------------------------------------------------
# Constants (WDG-2). Ops constants, NOT settings.json keys: the watchdog must work when the app or
# its settings are broken. Changing one is a re-install, which is deliberate.
# ---------------------------------------------------------------------------------------------
$WdStaleMin       = 30    # R6: last row older than this -> RESTART. Matches trader ruling AT-4.
$WdGraceMin       = 10    # R5: a process younger than this is left alone (first rows take ~4 min).
$WdTickMin        = 10    # the task's repetition interval. Informational here; the task owns it.
$WdRestartCap     = 3     # R3: restarts (LAUNCHED + RESTARTED lines) per rolling window.
$WdCapWindowHours = 24    # R3's rolling window.
$WdPauseExpiryMin = 45    # R1: a marker older than this is ignored (a crashed deploy cannot disable us).
$WdLaunchWaitSec  = 30    # how long to wait for a launched process to appear.
$WdLaunchSettleSec = 3    # after it appears, wait this long and COUNT AGAIN (trap 2).
$WdStopWaitSec    = 15    # how long to poll for zero processes after a stop.
$WdCsvTailLines   = 5     # the last row is the newest PARSEABLE timestamp among these lines.

# =============================================================================================
# The decision rule -- a PURE function. No I/O, no clock, no globals except the constants above.
# First match wins (spec section 2.2).
# =============================================================================================
function Get-WatchdogDecision {
    param(
        [Parameter(Mandatory = $true)][int]$ProcessCount,
        # $null = no row could be read (file absent, empty, header only, or unparseable).
        [Nullable[datetime]]$LastRowUtc,
        # $null = unknown. Only meaningful when ProcessCount is 1.
        [Nullable[datetime]]$ProcessStartUtc,
        [Parameter(Mandatory = $true)][datetime]$NowUtc,
        [Parameter(Mandatory = $true)][int]$RestartsInWindow,
        # $null = no marker.
        [Nullable[double]]$PauseAgeMinutes
    )
    # R1 -- a deploy, restart or manual job holds the box.
    if ($null -ne $PauseAgeMinutes -and $PauseAgeMinutes -lt $WdPauseExpiryMin) { return 'PAUSED' }
    # R2 -- never guess which process is the app.
    if ($ProcessCount -gt 1) { return 'AMBIGUOUS' }
    # R3 -- a long venue outage writes no rows; without a cap we would restart a healthy app forever.
    if ($RestartsInWindow -ge $WdRestartCap) { return 'CAPPED' }
    # R4
    if ($ProcessCount -eq 0) { return 'LAUNCH' }
    # R5 -- one process. An unknown start time does NOT grant grace: grace only delays action.
    # (No `.Value` on these: PowerShell unwraps [Nullable[datetime]] to a plain DateTime, whose
    # `.Value` is $null under non-strict mode -- it would silently compute against nothing.)
    if ($null -ne $ProcessStartUtc -and ($NowUtc - $ProcessStartUtc).TotalMinutes -lt $WdGraceMin) { return 'GRACE' }
    # R6 -- stale, or no row readable at all (WDG-5: the row's Timestamp, never the file's mtime).
    if ($null -eq $LastRowUtc -or ($NowUtc - $LastRowUtc).TotalMinutes -gt $WdStaleMin) { return 'RESTART' }
    # R7
    return 'OK'
}

# =============================================================================================
# The log -- `utc | event | detail`, append-only. It is also the state (WDG-3).
# =============================================================================================
$WdTsFormat = 'yyyy-MM-ddTHH:mm:ssZ'

function Format-WdUtc([Nullable[datetime]]$Utc) {
    if ($null -eq $Utc) { return 'NONE' }
    return $Utc.ToString($WdTsFormat, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-WatchdogLog {
    param([Parameter(Mandatory = $true)][string]$EventName, [string]$Detail = '', [datetime]$NowUtc = [DateTime]::UtcNow)
    $line = (Format-WdUtc $NowUtc) + ' | ' + $EventName + ' | ' + $Detail
    try {
        $dir = Split-Path -Parent $LogPath
        if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        [System.IO.File]::AppendAllText($LogPath, $line + "`r`n", (New-Object System.Text.UTF8Encoding $false))
    } catch {
        # Logging must never take the tick down. Nothing else can be done from a hidden task.
    }
}

<# Parses watchdog.log into entries {Utc, Event, Detail}. Malformed lines are skipped. #>
function Read-WatchdogLog {
    param([string]$Path = $LogPath)
    $entries = New-Object System.Collections.Generic.List[object]
    if (-not (Test-Path -LiteralPath $Path)) { return ,$entries }
    foreach ($ln in [System.IO.File]::ReadAllLines($Path)) {
        $parts = $ln -split ' \| ', 3
        if ($parts.Count -lt 2) { continue }
        $ts = [datetime]::MinValue
        $ok = [datetime]::TryParseExact($parts[0].Trim(), $WdTsFormat, [System.Globalization.CultureInfo]::InvariantCulture,
            ([System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal), [ref]$ts)
        if (-not $ok) { continue }
        $detail = ''
        if ($parts.Count -ge 3) { $detail = $parts[2] }
        $entries.Add([pscustomobject]@{ Utc = $ts; Event = $parts[1].Trim(); Detail = $detail })
    }
    return ,$entries
}

<# R3's input: LAUNCHED and RESTARTED lines younger than the window. EXACT event match -- a
   LAUNCH_FAILED or RESTART_FAILED line is not a restart the app received. #>
function Get-WatchdogRestartCount {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()]$Entries, [Parameter(Mandatory = $true)][datetime]$NowUtc)
    $n = 0
    foreach ($e in $Entries) {
        if ($e.Event -ne 'LAUNCHED' -and $e.Event -ne 'RESTARTED') { continue }
        if (($NowUtc - $e.Utc).TotalHours -lt $WdCapWindowHours) { $n++ }
    }
    return $n
}

function Get-LastWdEntry {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()]$Entries, [Parameter(Mandatory = $true)][string]$EventName)
    for ($i = $Entries.Count - 1; $i -ge 0; $i--) { if ($Entries[$i].Event -eq $EventName) { return $Entries[$i] } }
    return $null
}

# =============================================================================================
# State readers -- each returns $null rather than throwing.
# =============================================================================================

<# The newest parseable Timestamp among the last few lines. A torn last line (the app mid-write)
   or the header is skipped, so a healthy app is never restarted for a partial read. $null when no
   line parses -- R6 treats that as stale. #>
function Get-LastRowUtc {
    param([string]$Path = $CsvPath)
    try {
        if (-not (Test-Path -LiteralPath $Path)) { return $null }
        $best = $null
        foreach ($ln in @(Get-Content -LiteralPath $Path -Tail $WdCsvTailLines -ErrorAction Stop)) {
            $field = ($ln -split ',', 2)[0].Trim()
            $ts = [datetime]::MinValue
            $ok = [datetime]::TryParseExact($field, 'yyyy-MM-dd HH:mm:ss', [System.Globalization.CultureInfo]::InvariantCulture,
                ([System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal), [ref]$ts)
            if ($ok -and ($null -eq $best -or $ts -gt $best)) { $best = $ts }
        }
        return $best
    } catch { return $null }
}

function Get-PauseAgeMinutes {
    param([datetime]$NowUtc = [DateTime]::UtcNow)
    try {
        if (-not (Test-Path -LiteralPath $PausePath)) { return $null }
        return [double]($NowUtc - (Get-Item -LiteralPath $PausePath).LastWriteTimeUtc).TotalMinutes
    } catch { return $null }
}

function Get-AppProcesses { return @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) }

<# The start of this logon (the task runs inside the interactive logon), else the last boot. START
   is logged on the first tick after that instant. #>
function Get-WatchdogSessionStart {
    $boot = $null; $logon = $null
    try { $boot = (Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).LastBootUpTime.ToUniversalTime() } catch { }
    try {
        $me = Get-CimInstance Win32_Process -Filter "ProcessId=$PID" -ErrorAction Stop
        $ls = @(Get-CimAssociatedInstance -InputObject $me -ResultClassName Win32_LogonSession -ErrorAction Stop)
        if ($ls.Count -gt 0 -and $ls[0].StartTime) { $logon = $ls[0].StartTime.ToUniversalTime() }
    } catch { }
    $src = 'logon'; $key = $logon
    if ($null -eq $key) { $src = 'boot'; $key = $boot }
    return [pscustomobject]@{ Key = $key; Source = $src; Logon = $logon; Boot = $boot }
}

# =============================================================================================
# Actions. Both COUNT processes afterwards -- never trust the call (spec section 0, trap 2).
# =============================================================================================

<# Returns @{ Ok; Pid; Count }. Ok only when exactly ONE process runs after the settle wait. #>
function Invoke-WatchdogLaunch {
    try {
        if (-not (Test-Path -LiteralPath $ExePath)) { return [pscustomobject]@{ Ok = $false; Pid = 'NONE'; Count = 0; Why = 'exe_missing' } }
        $sp = @{ FilePath = $ExePath; WorkingDirectory = (Split-Path -Parent $ExePath); WindowStyle = $LaunchWindowStyle }
        if ($ExeArguments) { $sp['ArgumentList'] = $ExeArguments }
        Start-Process @sp -ErrorAction Stop | Out-Null
    } catch {
        return [pscustomobject]@{ Ok = $false; Pid = 'NONE'; Count = @(Get-AppProcesses).Count; Why = ('start_threw: ' + $_.Exception.Message) }
    }
    $deadline = (Get-Date).AddSeconds($WdLaunchWaitSec)
    $seen = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
        if (@(Get-AppProcesses).Count -ge 1) { $seen = $true; break }
    }
    if ($seen) { Start-Sleep -Seconds $WdLaunchSettleSec }
    $after = @(Get-AppProcesses)
    $pidTxt = 'NONE'
    if ($after.Count -ge 1) { $pidTxt = (($after | ForEach-Object { $_.Id }) -join ',') }
    $why = ''
    if ($after.Count -eq 0) { $why = 'no_process_after_launch' } elseif ($after.Count -gt 1) { $why = 'more_than_one_process' }
    return [pscustomobject]@{ Ok = ($after.Count -eq 1); Pid = $pidTxt; Count = $after.Count; Why = $why }
}

<# Stops one PID and polls for ZERO processes of the name, as Stop-RemoteApp does. #>
function Invoke-WatchdogStop {
    param([Parameter(Mandatory = $true)][int]$ProcessId)
    try { Stop-Process -Id $ProcessId -Force -ErrorAction Stop } catch { }
    $deadline = (Get-Date).AddSeconds($WdStopWaitSec)
    while ((Get-Date) -lt $deadline) {
        if (@(Get-AppProcesses).Count -eq 0) { return 0 }
        Start-Sleep -Seconds 1
    }
    return @(Get-AppProcesses).Count
}

function Test-WdPauseActive { $a = Get-PauseAgeMinutes; return ($null -ne $a -and $a -lt $WdPauseExpiryMin) }

function Format-WdAge([Nullable[datetime]]$Utc, [datetime]$NowUtc) {
    if ($null -eq $Utc) { return 'UNREADABLE' }
    return ('{0:0.0}' -f ($NowUtc - $Utc).TotalMinutes)
}

# =============================================================================================
# One tick. Never throws.
# =============================================================================================
function Invoke-WatchdogTick {
    try {
        $now = [DateTime]::UtcNow
        $log = Read-WatchdogLog

        # START -- the first tick of this logon (proves the boot path, spec section 2.3).
        $ss = Get-WatchdogSessionStart
        if ($null -ne $ss.Key) {
            $lastStart = Get-LastWdEntry -Entries $log -EventName 'START'
            if ($null -eq $lastStart -or $lastStart.Utc -lt $ss.Key.AddSeconds(-5)) {
                Write-WatchdogLog 'START' ('src=' + $ss.Source + ' logon=' + (Format-WdUtc $ss.Logon) + ' boot=' + (Format-WdUtc $ss.Boot) + ' session=' + (Get-Process -Id $PID).SessionId + ' user=' + $env:USERNAME) $now
            }
        } else {
            Write-WatchdogLog 'ERROR' 'could not read the logon or boot time; START not evaluated' $now
        }

        # ORDER MATTERS: processes FIRST, then the marker. deploy/restart write the marker BEFORE
        # they stop the app, so a tick that sees 0 processes also sees the marker. Reading the
        # marker first leaves a window where both are missed and the OLD exe is launched.
        $procs = Get-AppProcesses
        $pauseAge = Get-PauseAgeMinutes -NowUtc $now
        $lastRow = Get-LastRowUtc
        $procStart = $null
        if ($procs.Count -eq 1) { try { $procStart = $procs[0].StartTime.ToUniversalTime() } catch { $procStart = $null } }
        $restarts = Get-WatchdogRestartCount -Entries $log -NowUtc $now

        $decision = Get-WatchdogDecision -ProcessCount $procs.Count -LastRowUtc $lastRow -ProcessStartUtc $procStart `
            -NowUtc $now -RestartsInWindow $restarts -PauseAgeMinutes $pauseAge

        # PAUSED is logged only when it starts and when it ends.
        $lastPaused = Get-LastWdEntry -Entries $log -EventName 'PAUSED'
        $wasPaused = ($null -ne $lastPaused -and $lastPaused.Detail -like 'start*')
        $pauseTxt = 'none'
        if ($null -ne $pauseAge) { $pauseTxt = ('{0:0.0}' -f $pauseAge) }
        if ($decision -eq 'PAUSED' -and -not $wasPaused) {
            Write-WatchdogLog 'PAUSED' ('start marker_age_min=' + $pauseTxt) $now
        } elseif ($decision -ne 'PAUSED' -and $wasPaused) {
            $how = 'marker_removed'
            if ($null -ne $pauseAge) { $how = 'marker_expired' }
            Write-WatchdogLog 'PAUSED' ('end ' + $how + ' marker_age_min=' + $pauseTxt) $now
        }

        # HEARTBEAT -- the first tick of each UTC hour.
        $hourKey = $now.ToString('yyyy-MM-ddTHH', [System.Globalization.CultureInfo]::InvariantCulture)
        $lastHb = Get-LastWdEntry -Entries $log -EventName 'HEARTBEAT'
        if ($null -eq $lastHb -or $lastHb.Utc.ToString('yyyy-MM-ddTHH', [System.Globalization.CultureInfo]::InvariantCulture) -ne $hourKey) {
            Write-WatchdogLog 'HEARTBEAT' ('decision=' + $decision + ' procs=' + $procs.Count + ' last_row_age_min=' + (Format-WdAge $lastRow $now) + ' restarts_24h=' + $restarts + ' pause=' + $pauseTxt) $now
        }

        switch ($decision) {
            'AMBIGUOUS' {
                Write-WatchdogLog 'AMBIGUOUS' ('count=' + $procs.Count + ' pids=' + (($procs | ForEach-Object { $_.Id }) -join ',')) $now
            }
            'CAPPED' {
                $lastCap = Get-LastWdEntry -Entries $log -EventName 'CAP_REACHED'
                if ($null -eq $lastCap -or ($now - $lastCap.Utc).TotalHours -ge $WdCapWindowHours) {
                    Write-WatchdogLog 'CAP_REACHED' ('restarts_24h=' + $restarts + ' cap=' + $WdRestartCap + ' procs=' + $procs.Count + ' last_row_age_min=' + (Format-WdAge $lastRow $now) + ' -- no further action until the count drops') $now
                }
            }
            'LAUNCH' {
                # Re-check the marker immediately before acting: a deploy may have started since.
                if (Test-WdPauseActive) {
                    if (-not $wasPaused) { Write-WatchdogLog 'PAUSED' 'start marker appeared before LAUNCH' }
                } else {
                    $r = Invoke-WatchdogLaunch
                    if ($r.Ok) { Write-WatchdogLog 'LAUNCHED' ('pid=' + $r.Pid + ' last_row=' + (Format-WdUtc $lastRow)) }
                    else { Write-WatchdogLog 'LAUNCH_FAILED' ('count=' + $r.Count + ' pids=' + $r.Pid + ' why=' + $r.Why) }
                }
            }
            'RESTART' {
                if (Test-WdPauseActive) {
                    if (-not $wasPaused) { Write-WatchdogLog 'PAUSED' 'start marker appeared before RESTART' }
                } else {
                    $oldPid = $procs[0].Id
                    $remaining = Invoke-WatchdogStop -ProcessId $oldPid
                    if ($remaining -ne 0) {
                        Write-WatchdogLog 'RESTART_FAILED' ('stage=stop old_pid=' + $oldPid + ' remaining=' + $remaining + ' last_row=' + (Format-WdUtc $lastRow))
                    } else {
                        $r = Invoke-WatchdogLaunch
                        if ($r.Ok) { Write-WatchdogLog 'RESTARTED' ('old_pid=' + $oldPid + ' new_pid=' + $r.Pid + ' last_row=' + (Format-WdUtc $lastRow)) }
                        else { Write-WatchdogLog 'RESTART_FAILED' ('stage=launch old_pid=' + $oldPid + ' count=' + $r.Count + ' pids=' + $r.Pid + ' why=' + $r.Why + ' last_row=' + (Format-WdUtc $lastRow)) }
                    }
                }
            }
            default { }   # PAUSED, GRACE, OK: nothing to do.
        }
        return $decision
    } catch {
        Write-WatchdogLog 'ERROR' ($_.Exception.GetType().Name + ': ' + ($_.Exception.Message -replace '[\r\n]+', ' '))
        return 'ERROR'
    }
}

# Run one tick only when executed as a script -- dot-sourcing (the tests) defines functions only.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-WatchdogTick | Out-Null
    exit 0
}
