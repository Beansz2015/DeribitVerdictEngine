#requires -Version 5.1
<#
  tools/checks/selftest/fixture-parser-fp2-mutation.ps1 -- the FP-2 MUTATION TEST for
  harness 3 (docs/fixture-parser-check-spec.md section 3, decision FP-D3: "Mutation.
  Corrupt a known-good fixture's name, confirm the detector flags it, restore"). The only
  route to known POSITIVES for the name check. KEYED: it makes real Jev calls.

  verify/ordercheck/Program.vb is NEVER edited. The six renames are applied to a COPY in a
  temp directory, and the harness reads the copy through -SourceFile. Before and after, the
  script hashes the real file and prints `git diff --stat` for it, which must be empty.

  The six fixtures: every one was judged `name_matches` by a past FP-2 run AND the seat
  agreed (docs/harness-runs/fixture-parser-clean-run-2026-09-22.md, -a23a-run-2026-09-22.md,
  -a20-a23b-run-2026-09-22.md). Spending them again costs no fresh population.

  Each wrong name keeps the original ID prefix (so it stays unique in the file -- the harness
  finds a Sub's body by name, first match wins) and takes its DESCRIPTIVE part from another
  real fixture that tests an unrelated property. The truth is constructed: every mutated
  name is wrong, so any verdict other than name_matches is a detection.

  Usage (repo root; the key comes from the gitignored typesafe.local.env):
    set -a; . ./typesafe.local.env; set +a
    powershell -NoProfile -File tools/checks/selftest/fixture-parser-fp2-mutation.ps1 [-RenameTitles]
  Cost as measured 2026-09-23 (UTC): 30 calls. -RenameTitles (2026-09-24) also rewrites each
  mutated Sub's Check titles to the new name; run record
  docs/harness-runs/fixture-parser-fp2-mutation-titles-2026-09-24.md. See the run record in
  docs/harness-runs/fixture-parser-fp2-mutation-2026-09-23.md.

  FP-2t (second reader, 2026-09-24 UTC, SR-D6, docs/jev-harnesses-second-reader-2026-09-24.md
  section 6): the harness itself (tools/checks/fixture-parser.ps1, FP-D33) now asks a
  title-stripped variant beside FP-2 in the SAME -Fp2Only run -- 10 calls per fixture (5 FP-2
  + 5 FP-2t) instead of 5, so a run of all six is 60 calls, not 30. This script needs no
  extra flag or invocation for it; the table below reads both from one run's output.
#>
[CmdletBinding()]
param(
    # TEST SEAM ONLY -- forwarded to the harness's own -TestTransportOverride, so the
    # mutation plumbing can be dry-run with synthetic answers and zero spend. A real
    # mutation run never passes it.
    [scriptblock]$TestTransportOverride = $null,
    # TITLE RENAME (second reader, 2026-09-24 UTC; close-list step 6). The 2026-09-23 run left
    # each body's Check("...") title carrying the ORIGINAL name, so a detector that only
    # compared the Sub name against the title would score the same as one that read the
    # assertions. With this switch each mutated Sub's Check titles are rewritten to match the
    # NEW (wrong) name, so the only evidence against the name is the body's code.
    [switch]$RenameTitles
)
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$tool = Join-Path $repo 'tools\checks\fixture-parser.ps1'
$real = Join-Path $repo 'verify\ordercheck\Program.vb'

# original -> mutated. Descriptive parts borrowed from: Kelly sizing (KellyInverseLeverage),
# CSV month rollover (MonthRolloverSplitsAndHeadersOnCreateOnly),
# absorption episodes (AbsorptionEpisodeLifecycle), settings hot reload
# (HotReloadReMergesAndDeleteReverts), trade-store sequence gaps (SequenceGapDetection),
# funding-rate merge (FundingMergeClipsOverreachButKeepsStored).
$mutations = [ordered]@{
    'A1_CvdSlopeRising'               = 'A1_KellyInverseLeverage'
    'A3_MicroCvdWindowFromEnd'        = 'A3_MonthRolloverSplitsAndHeadersOnCreateOnly'
    'A20a_CalcOfiRefactorEquivalence' = 'A20a_HotReloadReMergesAndDeleteReverts'
    'A20b_CalcOfiEdgeCasesUnchanged'  = 'A20b_SequenceGapDetection'
    'A23a_AggrVelSteadyRate'          = 'A23a_FundingMergeClipsOverreachButKeepsStored'
    'A65c_WideArmIsTestedFirst'       = 'A65c_AbsorptionEpisodeLifecycle'
}

if ([string]::IsNullOrWhiteSpace($env:TYPESAFE_API_KEY) -and -not $TestTransportOverride) { 'EXIT_REASON=NO_API_KEY -- load typesafe.local.env first. Nothing was run.'; exit 2 }

$hashBefore = (Get-FileHash -Algorithm SHA256 -Path $real).Hash
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("fp2-mutation-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $lines = [System.IO.File]::ReadAllLines($real, [System.Text.Encoding]::UTF8)
    $allSubNames = @($lines | ForEach-Object { if ($_ -match '^\s*(?:Private|Public|Friend)?\s*Sub\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(') { $Matches[1] } })
    foreach ($orig in $mutations.Keys) {
        $new = $mutations[$orig]
        if ($allSubNames -contains $new) { "EXIT_REASON=MUTATION_COLLIDES -- '$new' already names a Sub."; exit 2 }
        $hits = 0
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -match "^(\s*(?:Private|Public|Friend)?\s*Sub\s+)$([regex]::Escape($orig))(\s*\(.*)$") {
                $lines[$i] = $Matches[1] + $new + $Matches[2]; $hits++
            }
        }
        if ($hits -ne 1) { "EXIT_REASON=MUTATION_MISSED -- '$orig' declaration found $hits times, need exactly 1."; exit 2 }
    }
    if ($RenameTitles) {
        # Titles in ASCII on purpose: PS 5.1 reads a BOM-less .ps1 as ANSI. Each keeps the ID
        # prefix and spells out the new name's descriptive part.
        $newTitles = @{
            'A1_KellyInverseLeverage'                       = 'A1 Kelly inverse leverage'
            'A3_MonthRolloverSplitsAndHeadersOnCreateOnly'  = 'A3 month rollover splits and headers on create only'
            'A20a_HotReloadReMergesAndDeleteReverts'        = 'A20a hot reload re-merges and delete reverts'
            'A20b_SequenceGapDetection'                     = 'A20b sequence gap detection'
            'A23a_FundingMergeClipsOverreachButKeepsStored' = 'A23a funding merge clips overreach but keeps stored'
            'A65c_AbsorptionEpisodeLifecycle'               = 'A65c absorption episode lifecycle'
        }
        foreach ($new in $mutations.Values) {
            $decl = -1
            for ($i = 0; $i -lt $lines.Length; $i++) { if ($lines[$i] -match "^\s*(?:Private|Public|Friend)?\s*Sub\s+$([regex]::Escape($new))\s*\(") { $decl = $i; break } }
            $renamed = 0
            for ($i = $decl + 1; $i -lt $lines.Length -and $lines[$i] -notmatch '^\s*End\s+Sub\b'; $i++) {
                if ($lines[$i] -match '^(\s*Check\(")([^"]*)(".*)$') {
                    "TITLE_RENAMED $new line $($i + 1): [$($Matches[2])] -> [$($newTitles[$new])]"
                    $lines[$i] = $Matches[1] + $newTitles[$new] + $Matches[3]; $renamed++
                }
            }
            if ($renamed -lt 1) { "EXIT_REASON=TITLE_RENAME_MISSED -- no Check(`"...`") title found in '$new'."; exit 2 }
        }
    }
    $copy = Join-Path $tmp 'Program.mutated.vb'
    [System.IO.File]::WriteAllLines($copy, $lines, (New-Object System.Text.UTF8Encoding($false)))
    "MUTATED_COPY_LINES=$($lines.Length) (real file lines: $(([System.IO.File]::ReadAllLines($real)).Length))"

    $baseline = Join-Path $tmp 'baseline.json'
    # 'unsure' on purpose: the constructed truth is "not name_matches", which no single
    # vocabulary value expresses (overclaims and understates both cover "a different
    # property"). The FLAGGED column below is the comparison that counts.
    $bl = @{}; foreach ($new in $mutations.Values) { $bl[$new] = 'unsure' }
    ($bl | ConvertTo-Json) | Set-Content -Encoding UTF8 -Path $baseline
    $filter = '^(' + (($mutations.Values | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')$'

    $out = & $tool -SourceFile $copy -BaselinePath $baseline -SubFilter $filter -Fp2Only -Samples 5 -OutPath (Join-Path $tmp 'report.md') -TestTransportOverride $TestTransportOverride 2>&1
    $exitCode = $LASTEXITCODE
    $txt = @($out | ForEach-Object { [string]$_ })
    $txt | Where-Object { $_ -match '^(EXIT_REASON|FP2_ONLY|USAGE_INPUT_TOKENS|USAGE_OUTPUT_TOKENS|WALL_TIME_SEC|SCOPE_JEV_CALLS|FP2_UNSTABLE|FP2_BAD_VERDICTS|FP2_WAF_BLOCKED|FP2T_ITEMS_SAME_PLURALITY_AS_FP2|FP2T_WAF_BLOCKED|SITES_JUDGED|JEV_MODEL)=?' } | Select-Object -Unique
    "HARNESS_EXIT=$exitCode"
    ''
    # FP-D33 (SR-D6, second reader 2026-09-24): FP-2t rides along automatically -- the
    # harness now asks it beside FP-2 in the same -Fp2Only run, one indented "      t: ..."
    # line right after each SubName's own row. No second harness invocation needed.
    '| Original name | Mutated name | FP-2 verdict (of 5) | FP-2 agreement | FP-2 mean top prob | FP-2t verdict (of 5) | FP-2t agreement | FP-2t mean top prob | Flagged (FP-2) |'
    '|---|---|---|---|---|---|---|---|---|'
    $flagged = 0; $named = 0
    foreach ($orig in $mutations.Keys) {
        $new = $mutations[$orig]
        $rowIdx = -1
        for ($ri = 0; $ri -lt $txt.Count; $ri++) { if ($txt[$ri] -match "^  $([regex]::Escape($new)) \[") { $rowIdx = $ri; break } }
        if ($rowIdx -lt 0) { "| $orig | $new | NOT_JUDGED | | | | | | no |"; continue }
        $row = $txt[$rowIdx]
        $rowT = if ($rowIdx + 1 -lt $txt.Count -and $txt[$rowIdx + 1] -match '^\s+t: ') { $txt[$rowIdx + 1] } else { $null }
        $v = if ($row -match ' verdict=(\S+)') { $Matches[1] } else { '?' }
        $ar = if ($row -match ' agreement_rate=(\S+)') { $Matches[1] } else { '?' }
        $mp = if ($row -match ' mean_top_prob=(\S+)') { $Matches[1] } else { '?' }
        $vT = if ($rowT -and $rowT -match 'verdict=(\S+)') { $Matches[1] } else { '?' }
        $arT = if ($rowT -and $rowT -match 'agreement_rate=(\S+)') { $Matches[1] } else { '?' }
        $mpT = if ($rowT -and $rowT -match 'mean_top_prob=(\S+)') { $Matches[1] } else { '?' }
        $f = if ($v -in @('name_overclaims', 'name_understates')) { 'yes'; $flagged++; $named++ } elseif ($v -eq 'ambiguous') { 'exit-code only (ambiguous)'; $flagged++ } else { 'NO' }
        "| $orig | $new | $v | $ar | $mp | $vT | $arT | $mpT | $f |"
    }
    ''
    "MUTATIONS_FLAGGED=$flagged of $($mutations.Count) (named the mismatch: $named; ambiguous counted as flagged by the exit code only; FP-2 column only -- FP-2t is informational)"
    if ($flagged -lt [math]::Ceiling($mutations.Count / 2)) { 'ESCALATE: FP-2 flagged fewer than half of the mutations. A finding for the seat -- do not tune it away.' }
} finally {
    Remove-Item -Recurse -Force -Path $tmp -ErrorAction SilentlyContinue
}
$hashAfter = (Get-FileHash -Algorithm SHA256 -Path $real).Hash
"REAL_PROGRAM_VB_UNCHANGED=$($hashBefore -eq $hashAfter) (sha256 $($hashAfter.Substring(0,16))...)"
"GIT_DIFF_STAT_PROGRAM_VB=[$((& git -C $repo diff --stat -- verify/ordercheck/Program.vb) -join ' ')]"
