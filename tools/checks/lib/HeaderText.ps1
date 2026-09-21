#requires -Version 5.1
<#
  tools/checks/lib/HeaderText.ps1 -- shared AnalysisLogger.Header text extraction.

  Dot-sourced by BOTH:
    - tools/checks/rotation-riders.ps1  (the pre-push rotation gate)
    - tools/checks/rider-travel.ps1     (the advisory rider-travel check)

  Extracted verbatim out of rotation-riders.ps1 on 2026-09-22 per
  docs/rider-travel-check-spec.md D-2. A second copy of this function drifts the
  first time the header declaration is refactored -- that is the exact defect class
  CLAUDE.md already rules on for fixture literals; same reasoning applies here.

  Changing this function changes the observable ROTATION_STATUS / ROTATION_DETAIL
  output of rotation-riders.ps1, which is wired into the pre-push gate. Any change
  here MUST re-run the D-2 regression check documented in
  docs/rider-travel-check-spec.md §4.5 (run the standalone test below, before and
  after, and confirm both output lines are byte-identical) before it ships.

  STANDALONE TEST for rotation-riders.ps1 (no build needed) -- unchanged by this
  extraction, reproduced here only so both scripts' header comments stay accurate:
    powershell -NoProfile -File tools/checks/rotation-riders.ps1 -BeforeRev HEAD `
      -AfterFile <copy of AnalysisLogger.vb> -Changed AnalysisLogger.vb
#>

# Concatenate the string literals of `Shared ReadOnly Header As String =` up to the first
# continuation line that does not end in `&`. Returns $null when it cannot be found.
function Get-HeaderText([string[]]$lines) {
    if ($null -eq $lines) { return $null }
    $decl = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match 'Shared\s+ReadOnly\s+Header\s+As\s+String\s*=') { $decl = $i; break }
    }
    if ($decl -lt 0) { return $null }
    $sb = New-Object System.Text.StringBuilder
    $found = $false
    for ($j = $decl + 1; $j -lt $lines.Length; $j++) {
        $t = $lines[$j].Trim()
        if ($t -eq '' -or $t.StartsWith("'")) { continue }
        $lits = [regex]::Matches($t, '"((?:[^"]|"")*)"')
        foreach ($m in $lits) { [void]$sb.Append($m.Groups[1].Value.Replace('""', '"')); $found = $true }
        if (-not $t.EndsWith('&')) { break }
    }
    if (-not $found) { return $null }
    return $sb.ToString()
}
