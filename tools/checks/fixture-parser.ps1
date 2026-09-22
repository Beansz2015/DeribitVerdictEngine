#requires -Version 5.1
<#
  tools/checks/fixture-parser.ps1 -- fixture-literal provenance (FP-1) and fixture-name-
  vs-assertion (FP-2) audit over verify/ordercheck/Program.vb.

  ADVISORY ONLY (docs/fixture-parser-check-spec.md top matter -- "NOT a gate. Advisory,
  same three reasons as docs/rider-travel-check-spec.md D-3"). Not wired into
  verify-gate.ps1 or the pre-push hook.

  WHY: CLAUDE.md's fixture-literal provenance rule (RULED 2026-08-11) says a fixture
  passing a settings-derived threshold as a literal must declare, in a comment, whether it
  is SHIPPED BEHAVIOUR (must derive from cfg) or MECHANISM (a literal is fine, and the
  comment must say why) -- and says plainly "no tool can tell the two apart". This harness
  splits the problem per docs/fixture-parser-check-spec.md section 1: CODE computes
  whether a literal equals any EVER-SHIPPED value of its matched settings.json key
  (deterministic); Jev judges only whether the comment declares the right class for what
  the code found (semantic). Jev is never asked to do the arithmetic or the set-membership
  check itself -- see the line marked "CODE COMPUTES SET MEMBERSHIP HERE" below.

  THE FOUR TRAPS (docs/fixture-parser-check-spec.md section 0):
    1. Walk ALL settings.json revisions the tracked file has ever had (git log -- the
       path), never a recent slice. MEASURED: a 40-revision walk clears indicators.OBV.
       trend_gate as novel when the full 87-revision walk finds it was 0.001 and 10.0
       before today's 18.0/23.0 pair.
    2. Never compose the two Noul diagnostics with AND. The `verdict` Choice answer is the
       ONLY thing code reads -- see the lines marked TRAP 2 below.
    3. Never trust a documented value history (a doc in this repo records trend_gate
       starting at 8.0; 8.0 never shipped). Ground truth is the revision walk alone.
    4. Do not line-anchor regexes over VB. Sub/Function/End declarations ARE always
       statement-starting lines in this codebase (unlike Return/Throw/Exit), so anchoring
       those specific patterns is safe; the named-argument literal scan itself is applied
       to comment-stripped line text, never anchored to line start, so it cannot be fooled
       by an inline single-line form the way a `^\s*Return` scan would be.

  THE SPLIT THAT MAKES THIS TRACTABLE (docs/fixture-parser-check-spec.md section 1):
    - CODE: does the literal equal any ever-shipped value of its matched key?
    - JEV:  does the comment at that call site declare the right class for what code found?

  KEY DESIGN POINT auto-proceed decisions recorded here (CLAUDE.md's auto-proceed
  obligation -- "log every auto-proceeded decision in ONE line"), because
  docs/fixture-parser-check-spec.md section 3's FP-D1..FP-D5 table does not settle every
  mechanical question a build needs:

  FP-D6 (this build): "call site" for FP-1's population is a named-argument literal pass
  (`name:=literal`), matching section 4.1 step 1's literal wording ("every named-argument
  literal pass"). Section 2's "Call sites carrying MECHANISM | 44" / "carrying SHIPPED |
  18" are whole-file grep counts and also cover non-`:=` literal forms (confirmed: line
  1472's MECHANISM comment sits above a `.Property = value` object-initializer assignment,
  not a named-argument call) -- so SITES_WITH_PROVENANCE_COMMENT below, computed only over
  named-argument call sites, will not reproduce 44+18 exactly. Section 2 itself says
  "treat as smoke anchors, never assertions". Flagged in the build report as a spec
  ambiguity, not silently reconciled.

  FP-D7 (this build): FP-D2's Jev adjudication for zero/multi-candidate parameter-to-key
  matches runs as ONE unsampled `key_resolution` call per residual (provenance-commented)
  site that needs it, separate from and prior to FP-1's sampled verdict call. FP-D4's
  self-consistency mandate is read as scoped to the two NAMED harness questions (FP-1,
  FP-2), not to this auxiliary resolution step, to keep API cost bounded. The resolution
  is diagnostic input to FP-1's state, never itself part of a verdict CODE reads.

  FP-D8 (this build): -SubFilter restricts which residual sites/subs are JUDGED (sent to
  Jev), for splitting the acceptance window from the measured window
  (docs/harness-shadow-mode-protocol.md section 4a). It does NOT affect the full-file
  coverage counters used for the PARSER_SUSPECT check -- that check exists to catch a
  broken parser regardless of which subset a given run chooses to judge.

  FP-D9 (this build): acceptance item 3 (forced LITERAL_CALL_SITES < 60) is demonstrated
  by pointing -SourceFile at a truncated scratch copy of Program.vb, not a synthetic
  override switch -- the demonstrated code path is then the real parser logic running on
  real (if incomplete) input, not a test-only branch that could itself drift from the
  real one.

  USAGE:
    set -a; . ./typesafe.local.env; set +a   # loads TYPESAFE_API_KEY (bash)
    powershell -NoProfile -File tools/checks/fixture-parser.ps1 `
      -BaselinePath <path to your own pre-written read, see
      docs/harness-shadow-mode-protocol.md section 2 step 3> `
      [-SubFilter <regex on enclosing sub name>] [-Samples N] [-CountersOnly]

  EXIT CODES:
    0 - every judged FP-1 call site verdict is mechanism_declared_ok or
        shipped_declared_ok, every judged FP-2 sub verdict is name_matches, and no row is
        UNSTABLE (self-consistency agreement rate below 1.0)
    1 - at least one judged FP-1 verdict is undeclared / declared_but_contradicted /
        ambiguous, at least one judged FP-2 verdict is name_overclaims / name_understates /
        ambiguous, or any judged row is UNSTABLE (never auto-resolved)
    2 - baseline missing, parser suspect (docs/fixture-parser-check-spec.md section 4.2),
        or API failure
#>
[CmdletBinding()]
param(
    [string]$SourceFile = 'verify/ordercheck/Program.vb',
    [string]$SettingsPath = 'settings.json',
    [string]$BaselinePath = 'fixture-parser-baseline.json',
    [string]$OutPath = 'fixture-parser-report.md',
    [string]$SettingsCachePath = 'fixture-parser-settings-cache.json',
    # Restricts which residual (provenance-commented) sites/subs are JUDGED -- a regex
    # tested against the enclosing sub's name. Lets an acceptance dry-run window and the
    # eventual measured first run be disjoint (docs/harness-shadow-mode-protocol.md
    # section 4a). Never affects the coverage counters (FP-D8 above).
    [string]$SubFilter = $null,
    # FP-D4 / docs/harness-shadow-mode-protocol.md section 4b: self-consistency sample
    # count. -1 is a sentinel for "use $SELF_CONSISTENCY_SAMPLES below".
    [int]$Samples = -1,
    # docs/harness-shadow-mode-protocol.md section 4a: withholds per-item verdicts and
    # every aggregate from a RESERVED window's dry run, console and report alike. Mirrors
    # tools/checks/commit-walker.ps1's -CountersOnly exactly.
    [switch]$CountersOnly,
    # Mirrors tools/checks/commit-walker.ps1's -DebugState: prints each call's state KEY
    # NAMES and sample_uid only, never content or the API key. Off by default.
    [switch]$DebugState
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# FP-D4: 5 samples, agreement rate on every row (docs/harness-shadow-mode-protocol.md
# section 4b), named beside its sibling constants, not buried in the param block.
$SELF_CONSISTENCY_SAMPLES = 5
if ($Samples -lt 1) { $Samples = $SELF_CONSISTENCY_SAMPLES }

# Defensive cap on a fixture Sub's body text sent to FP-2 -- not specced, added because a
# handful of subs in this file run past 150 lines. Truncated, not refused, so FP-2 still
# gets a judgment on an oversized sub rather than silently failing it. Same reasoning
# tools/checks/commit-walker.ps1 uses for $MAX_ESCALATION_DIFF_CHARS.
$MAX_SUB_BODY_CHARS = 12000

# Invoke-Jev is shared with tools/checks/commit-walker.ps1 and tools/checks/rider-travel.ps1
# (docs/fixture-parser-check-spec.md section 4: "Reuse it. Never write a second HTTP
# call."). Do not re-implement it here.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# ---------------------------------------------------------------------------------------
# Step 0: VB source parsing helpers (docs/fixture-parser-check-spec.md section 4.1 step 1
# / trap 4). Everything here that scans for a PATTERN (named-argument literals) works on
# comment-stripped text and is NOT line-anchored. The declaration/End scans ARE anchored
# to line start deliberately -- Sub/Function/End declarations are always statement-starting
# lines in this codebase, unlike the Return/Throw/Exit/single-line-If forms trap 4 warns
# about, so anchoring those specific patterns does not reintroduce the trap.
# ---------------------------------------------------------------------------------------

# Strips a trailing VB comment from a line, string-literal aware (VB strings use "" as an
# escaped double-quote; a `'` inside a string is not a comment start).
function Get-CodeOnly([string]$line) {
    $inStr = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $ch = $line[$i]
        if ($ch -eq '"') {
            if ($inStr -and ($i + 1) -lt $line.Length -and $line[$i + 1] -eq '"') { $i++; continue }
            $inStr = -not $inStr
            continue
        }
        if ($ch -eq "'" -and -not $inStr) { return $line.Substring(0, $i) }
    }
    return $line
}

# camelCase -> snake_case (FP-D2's "name normalisation"). "currentATR" -> "current_atr",
# "slopeMinUsd" -> "slope_min_usd" -- verified against all 43 distinct parameter names
# found in this file before this script was written.
function ConvertTo-SnakeCase([string]$name) {
    $s = [regex]::Replace($name, '([a-z0-9])([A-Z])', '$1_$2')
    return $s.ToLowerInvariant()
}

# ---------------------------------------------------------------------------------------
# Step 1 (section 4.1 step 1): read the source, build per-line code-only text, a paren-
# depth table (VB.NET allows implicit line continuation inside unclosed parens), Sub/
# Function ranges, and every named-argument literal pass with its enclosing statement's
# comment block.
# ---------------------------------------------------------------------------------------
$srcFull = Resolve-RepoPath $SourceFile
if (-not (Test-Path $srcFull)) {
    "EXIT_REASON=SOURCE_MISSING"
    "No file at -SourceFile '$SourceFile'."
    exit 2
}
$lines = [string[]](Get-Content -Encoding UTF8 -Path $srcFull)
$n = $lines.Length

$codeOnly = New-Object string[] $n
$netParen = New-Object int[] $n
for ($i = 0; $i -lt $n; $i++) {
    $codeOnly[$i] = Get-CodeOnly $lines[$i]
    $opens = ([regex]::Matches($codeOnly[$i], '\(')).Count
    $closes = ([regex]::Matches($codeOnly[$i], '\)')).Count
    $netParen[$i] = $opens - $closes
}
# depth0[i] = paren depth at the START of line i (0-based). A line is the start of a new
# VB statement iff depth0[i] == 0 -- unclosed parens are the only implicit continuation
# this codebase uses (no trailing "_" line-continuation forms observed).
$depth0 = New-Object int[] ($n + 1)
for ($i = 0; $i -lt $n; $i++) {
    $d = $depth0[$i] + $netParen[$i]
    if ($d -lt 0) { $d = 0 }
    $depth0[$i + 1] = $d
}
function Get-StatementStart([int]$Lidx) {
    for ($k = $Lidx; $k -ge 0; $k--) { if ($depth0[$k] -eq 0) { return $k } }
    return 0
}
# Contiguous run of comment lines immediately above a statement's start line -- no blank-
# line tolerance, matching this file's own style (comment block sits directly on top of
# the code it describes, verified against the A1/A14b worked examples read before writing
# this parser).
function Get-CommentBlock([int]$stmtStart) {
    $collected = New-Object System.Collections.Generic.List[string]
    $k = $stmtStart - 1
    while ($k -ge 0) {
        $t = $lines[$k].Trim()
        if ($t.StartsWith("'")) { $collected.Insert(0, $t); $k-- } else { break }
    }
    return ($collected -join "`n")
}

$declRe = '^(Private|Public|Friend|Protected)?\s*(Shared\s+)?(Sub|Function)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\('
$endRe = '^End\s+(Sub|Function)\b'
$stack = New-Object System.Collections.Generic.Stack[object]
$ranges = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $n; $i++) {
    $t = $codeOnly[$i].Trim()
    if ($t -match $declRe) {
        $stack.Push([PSCustomObject]@{ Kind = $Matches[3]; Name = $Matches[4]; Start = $i })
    } elseif ($t -match $endRe) {
        if ($stack.Count -gt 0) {
            $top = $stack.Pop()
            $ranges.Add([PSCustomObject]@{ Kind = $top.Kind; Name = $top.Name; Start = $top.Start; End = $i })
        }
    }
}
function Get-EnclosingProc([int]$Lidx) {
    $best = $null
    foreach ($r in $ranges) {
        if ($r.Start -le $Lidx -and $Lidx -le $r.End) {
            if ($null -eq $best -or ($r.End - $r.Start) -lt ($best.End - $best.Start)) { $best = $r }
        }
    }
    return $best
}

# Named-argument literal pass: `paramName:=numericLiteral`, matched against comment-
# stripped text ONLY (never against the raw line, so a literal-looking string inside a
# comment or a quoted message can never be mistaken for a real call-site argument).
$literalRe = '([A-Za-z_][A-Za-z0-9_]*):=(-?\d+(?:\.\d+)?[FLDR]?)'
$callSites = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $n; $i++) {
    $ms = [regex]::Matches($codeOnly[$i], $literalRe)
    foreach ($m in $ms) {
        $param = $m.Groups[1].Value
        $valRaw = $m.Groups[2].Value
        $valClean = $valRaw.TrimEnd('F', 'L', 'D', 'R', 'f', 'l', 'd', 'r')
        $valD = [double]::Parse($valClean, [System.Globalization.CultureInfo]::InvariantCulture)
        $stmtStart = Get-StatementStart $i
        $commentBlock = Get-CommentBlock $stmtStart
        $proc = Get-EnclosingProc $i
        $callSites.Add([PSCustomObject]@{
            Line         = $i + 1
            Param        = $param
            LiteralRaw   = $valRaw
            LiteralValue = $valD
            CallLineText = $lines[$i].Trim()
            CommentBlock = $commentBlock
            EnclosingSub = if ($proc) { $proc.Name } else { '(module-level)' }
            EnclosingKind = if ($proc) { $proc.Kind } else { 'Unknown' }
            HasMarker    = [bool]($commentBlock -match '\bMECHANISM\b' -or $commentBlock -match '\bSHIPPED\b')
        })
    }
}

$fixtureSubs = @($ranges | Where-Object { $_.Kind -eq 'Sub' -and $_.Name -match '^A\d+[a-z]?_' }).Count

# ---------------------------------------------------------------------------------------
# Step 2 (section 4.1 step 2): walk ALL settings.json revisions (trap 1). Flatten each
# revision's JSON to dotted scalar-leaf paths (arrays skipped -- none of the 43 distinct
# parameter names found in this file collide with a settings.json array element, verified
# before this build). Cache per-revision flattening to a gitignored scratch file: history
# below HEAD is immutable, so a hash already in cache is never re-walked.
# ---------------------------------------------------------------------------------------
function Get-FlattenedSettings($obj, [string]$prefix, [hashtable]$out) {
    if ($null -eq $obj) { return }
    if ($obj -is [System.Management.Automation.PSCustomObject]) {
        foreach ($p in $obj.PSObject.Properties) {
            $np = if ($prefix) { "$prefix.$($p.Name)" } else { $p.Name }
            Get-FlattenedSettings $p.Value $np $out
        }
    } elseif ($obj -is [System.Array]) {
        return
    } else {
        if ($prefix) { $out[$prefix] = $obj }
    }
}

$cacheFull = Resolve-RepoPath $SettingsCachePath
$revisionCache = @{}
if (Test-Path $cacheFull) {
    try {
        $raw = Get-Content -Raw -Path $cacheFull | ConvertFrom-Json -ErrorAction Stop
        foreach ($hp in $raw.PSObject.Properties) {
            $inner = @{}
            foreach ($kp in $hp.Value.PSObject.Properties) { $inner[$kp.Name] = $kp.Value }
            $revisionCache[$hp.Name] = $inner
        }
    } catch { $revisionCache = @{} }
}

$settingsHashes = [string[]](& git -C $repo log --format=%H -- $SettingsPath)
$newlyWalked = 0
foreach ($h in $settingsHashes) {
    if ($revisionCache.ContainsKey($h)) { continue }
    $raw = & git -C $repo show "${h}:$SettingsPath" 2>$null
    if (-not $raw) { continue }
    try {
        $j = ($raw -join "`n") | ConvertFrom-Json -ErrorAction Stop
    } catch { continue }
    $inner = @{}
    Get-FlattenedSettings $j '' $inner
    $revisionCache[$h] = $inner
    $newlyWalked++
}
if ($newlyWalked -gt 0) {
    $toSave = @{}
    foreach ($hk in $revisionCache.Keys) { $toSave[$hk] = $revisionCache[$hk] }
    ($toSave | ConvertTo-Json -Depth 6) | Set-Content -Encoding UTF8 -Path $cacheFull
}
$settingsRevisionsWalked = @($settingsHashes | Where-Object { $revisionCache.ContainsKey($_) }).Count

# Ever-shipped value SET for one settings key path, across every walked revision. This is
# the only source of truth for "ever shipped" -- never a doc (trap 3), never a recent
# slice (trap 1).
function Get-EverShippedSet([string]$keyPath) {
    $set = New-Object System.Collections.Generic.List[double]
    $seen = New-Object System.Collections.Generic.HashSet[double]
    foreach ($h in $settingsHashes) {
        if (-not $revisionCache.ContainsKey($h)) { continue }
        $flat = $revisionCache[$h]
        if ($flat.ContainsKey($keyPath)) {
            $d = $null
            if ([double]::TryParse([string]$flat[$keyPath], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$d)) {
                if ($seen.Add($d)) { $set.Add($d) }
            }
        }
    }
    # PowerShell's `return` writes to the success pipeline, which UNROLLS a collection --
    # a 0- or 1-element array collapses to nothing / a bare scalar at the call site. That
    # is exactly the bug this comment exists to prevent a re-introduction of: it silently
    # turned a single ever-shipped VALUE into a scalar double that then behaved correctly
    # by accident, and a single-candidate settings KEY PATH into a scalar STRING whose
    # `[0]` index sliced its first CHARACTER, not its first array element (caught during
    # this build -- see the matching comment on Get-KeyCandidates below). -NoEnumerate
    # forces the whole array through as one object, array-ness preserved regardless of
    # element count.
    Write-Output -NoEnumerate @($set | Sort-Object)
}

# ---------------------------------------------------------------------------------------
# Step 3 (section 4.1 step 3, FP-D2): match each call site's parameter to candidate
# settings keys by name normalisation. HEAD's settings.json supplies the candidate key
# SHAPE (which paths exist); the ever-shipped VALUE set for a matched path still comes
# from the full revision walk above, never from HEAD alone.
# ---------------------------------------------------------------------------------------
$headRaw = Get-Content -Raw -Path (Resolve-RepoPath $SettingsPath) | ConvertFrom-Json
$headFlat = @{}
Get-FlattenedSettings $headRaw '' $headFlat
$leafIndex = @{}
foreach ($k in $headFlat.Keys) {
    $leaf = $k.Split('.')[-1]
    if (-not $leafIndex.ContainsKey($leaf)) { $leafIndex[$leaf] = New-Object System.Collections.Generic.List[string] }
    $leafIndex[$leaf].Add($k)
}

# See the Get-EverShippedSet comment above: every branch uses Write-Output -NoEnumerate so
# a single-candidate match survives as a 1-element ARRAY, not a scalar string that a later
# `[0]` index would slice into its first character.
function Get-KeyCandidates([string]$paramName) {
    $snake = ConvertTo-SnakeCase $paramName
    if ($leafIndex.ContainsKey($snake)) { Write-Output -NoEnumerate @($leafIndex[$snake]); return }
    # FP-D2: strip a leading indicator prefix and retry once.
    $parts = $snake -split '_'
    if ($parts.Length -gt 1) {
        $stripped = ($parts[1..($parts.Length - 1)] -join '_')
        if ($leafIndex.ContainsKey($stripped)) { Write-Output -NoEnumerate @($leafIndex[$stripped]); return }
    }
    Write-Output -NoEnumerate @()
}

foreach ($cs in $callSites) {
    $cands = Get-KeyCandidates $cs.Param
    $cs | Add-Member -NotePropertyName Candidates -NotePropertyValue $cands
    $cs | Add-Member -NotePropertyName MatchClass -NotePropertyValue $(
        if ($cands.Count -eq 0) { 'ZERO' } elseif ($cands.Count -eq 1) { 'ONE' } else { 'MULTI' })
}

$allCandidatePaths = New-Object System.Collections.Generic.HashSet[string]
foreach ($cs in $callSites) { foreach ($c in $cs.Candidates) { [void]$allCandidatePaths.Add($c) } }
$everShippedByKey = @{}
foreach ($kp in $allCandidatePaths) { $everShippedByKey[$kp] = Get-EverShippedSet $kp }
$keysWithValueHistory = @($everShippedByKey.Keys | Where-Object { $everShippedByKey[$_].Count -gt 0 }).Count

# ---------------------------------------------------------------------------------------
# Step 4 (section 4.1 step 4): CODE COMPUTES SET MEMBERSHIP HERE. This is the one line
# checkable by reading per the build brief's report item 4 -- Jev is later told the
# resulting boolean (`literal_equals_ever_shipped`) as state; it is never asked to compare
# a number against a set itself (docs/fixture-parser-check-spec.md section 1's KEY DESIGN
# POINT). Only MATCHED_ONE_KEY sites get a code-only verdict here; ZERO/MULTI sites are
# resolved (or not) via the FP-D7 key-resolution call further down, only for the residual
# population that actually needs a Jev judgment.
# ---------------------------------------------------------------------------------------
foreach ($cs in $callSites) {
    if ($cs.MatchClass -eq 'ONE') {
        $kp = $cs.Candidates[0]
        $set = $everShippedByKey[$kp]
        $equals = $false
        foreach ($v in $set) { if ([math]::Abs($v - $cs.LiteralValue) -lt 0.0000001) { $equals = $true; break } }
        $cs | Add-Member -NotePropertyName ResolvedKey -NotePropertyValue $kp
        $cs | Add-Member -NotePropertyName EverShipped -NotePropertyValue $set
        $cs | Add-Member -NotePropertyName LiteralEqualsEverShipped -NotePropertyValue $equals
    } else {
        $cs | Add-Member -NotePropertyName ResolvedKey -NotePropertyValue $null
        $cs | Add-Member -NotePropertyName EverShipped -NotePropertyValue @()
        $cs | Add-Member -NotePropertyName LiteralEqualsEverShipped -NotePropertyValue $null
    }
}

$literalCallSites = $callSites.Count
$paramsDistinct = @($callSites | ForEach-Object { $_.Param } | Select-Object -Unique).Count
$matchedOne = @($callSites | Where-Object { $_.MatchClass -eq 'ONE' }).Count
$matchedZero = @($callSites | Where-Object { $_.MatchClass -eq 'ZERO' }).Count
$matchedMulti = @($callSites | Where-Object { $_.MatchClass -eq 'MULTI' }).Count
$literalEqualsShipped = @($callSites | Where-Object { $_.LiteralEqualsEverShipped -eq $true }).Count
$sitesWithProvenance = @($callSites | Where-Object { $_.HasMarker }).Count

function Write-Coverage([int]$judged) {
    "FIXTURE_SUBS=$fixtureSubs"
    "LITERAL_CALL_SITES=$literalCallSites"
    "PARAMS_DISTINCT=$paramsDistinct"
    "SETTINGS_REVISIONS_WALKED=$settingsRevisionsWalked"
    "KEYS_WITH_VALUE_HISTORY=$keysWithValueHistory"
    "MATCHED_ONE_KEY=$matchedOne"
    "MATCHED_ZERO_KEYS=$matchedZero"
    "MATCHED_MULTI_KEYS=$matchedMulti"
    "LITERAL_EQUALS_EVER_SHIPPED=$literalEqualsShipped"
    "SITES_WITH_PROVENANCE_COMMENT=$sitesWithProvenance"
    "SITES_JUDGED=$judged"
}

# ---------------------------------------------------------------------------------------
# Step 5 (section 4.2): coverage prints before anything else, on every exit path. A broken
# parser is loud (escalation trigger, section 0 / section 4.2): LITERAL_CALL_SITES < 60 or
# SETTINGS_REVISIONS_WALKED < 80 exits PARSER_SUSPECT before any API key is even read.
# ---------------------------------------------------------------------------------------
Write-Coverage 0

if ($literalCallSites -lt 60 -or $settingsRevisionsWalked -lt 80) {
    "EXIT_REASON=PARSER_SUSPECT"
    "LITERAL_CALL_SITES=$literalCallSites (need >= 60, docs/fixture-parser-check-spec.md section 2 measured 120) or SETTINGS_REVISIONS_WALKED=$settingsRevisionsWalked (need >= 80, section 2 measured 87) tripped the escalation trigger. Treat this as the parser missing a form, never as a smaller-than-usual file."
    exit 2
}

# Trap-1 proof line, always printed once revisions are walked and BEFORE the API-key gate,
# so it is visible even on a BASELINE_MISSING exit (docs/fixture-parser-check-spec.md
# section 5 acceptance item 1).
if ($everShippedByKey.ContainsKey('indicators.OBV.trend_gate')) {
    $obvSet = $everShippedByKey['indicators.OBV.trend_gate']
    "TRAP1_PROOF indicators.OBV.trend_gate ever-shipped = {$($obvSet -join ', ')}"
} else {
    $obvOnDemand = Get-EverShippedSet 'indicators.OBV.trend_gate'
    "TRAP1_PROOF indicators.OBV.trend_gate ever-shipped = {$($obvOnDemand -join ', ')}"
}

# ---------------------------------------------------------------------------------------
# Shadow-mode protocol step 2 (docs/harness-shadow-mode-protocol.md section 2): print the
# candidate list first, with NO judgments, before the baseline gate. -SubFilter (FP-D8)
# narrows this to the window actually being judged this run.
# ---------------------------------------------------------------------------------------
$residualSites = @($callSites | Where-Object { $_.HasMarker })
if ($SubFilter) { $residualSites = @($residualSites | Where-Object { $_.EnclosingSub -match $SubFilter }) }

function Get-Fp1Id($cs) { "$($cs.EnclosingSub)#$($cs.Line)#$($cs.Param)" }

if ($residualSites.Count -gt 0) {
    "FP1_CANDIDATES (no judgments, for baseline labelling):"
    foreach ($cs in $residualSites) {
        "  $(Get-Fp1Id $cs) matchClass=$($cs.MatchClass) key=$(if ($cs.ResolvedKey) { $cs.ResolvedKey } else { '(unresolved)' })"
    }
}

$residualSubNames = @($residualSites | ForEach-Object { $_.EnclosingSub } | Select-Object -Unique)
$fp2Items = New-Object System.Collections.Generic.List[object]
foreach ($sn in $residualSubNames) {
    $r = $ranges | Where-Object { $_.Kind -eq 'Sub' -and $_.Name -eq $sn } | Select-Object -First 1
    if ($null -eq $r) { continue }
    $bodyLines = $lines[$r.Start..$r.End]
    $body = ($bodyLines -join "`n")
    if ($body.Length -gt $MAX_SUB_BODY_CHARS) { $body = $body.Substring(0, $MAX_SUB_BODY_CHARS) + "`n... [truncated at $MAX_SUB_BODY_CHARS chars]" }
    $fp2Items.Add([PSCustomObject]@{ SubName = $sn; Body = $body })
}
if ($fp2Items.Count -gt 0) {
    "FP2_CANDIDATES (no judgments, for baseline labelling):"
    foreach ($f2 in $fp2Items) { "  $($f2.SubName)" }
}

if ($residualSites.Count -eq 0 -and $fp2Items.Count -eq 0) {
    "EXIT_REASON=NO_RESIDUAL_IN_WINDOW"
    "No provenance-commented (MECHANISM/SHIPPED) call sites matched$(if ($SubFilter) { " -SubFilter '$SubFilter'" } else { '' }). Nothing to judge this run."
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 6 (section 4.1 step 5): refuse to call the API without an operator-written
# baseline. Structural, before any API key read -- same mechanism as
# tools/checks/commit-walker.ps1 and tools/checks/rider-travel.ps1.
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each FP1_CANDIDATES / FP2_CANDIDATES item above BEFORE running this tool, as JSON, one key per item id (FP1 ids are '<Sub>#<Line>#<Param>', FP2 ids are the bare Sub name), values one of the verdict vocabulary above plus 'unsure'. Example:"
    '{ "A1_CvdSlopeRising#861#slopeMinUsd": "mechanism_declared_ok", "A1_CvdSlopeRising": "name_matches" }'
    "The whole point of the first run is comparing an independent human read against Jev's; looking first makes that comparison worthless permanently. See docs/harness-shadow-mode-protocol.md."
    exit 2
}
$baselineRaw = Get-Content -Raw -Path $baselineFull | ConvertFrom-Json
$baseline = @{}
if ($null -ne $baselineRaw) {
    foreach ($p in $baselineRaw.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}

$apiKey = $env:TYPESAFE_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set in the environment. Load typesafe.local.env first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

# ---------------------------------------------------------------------------------------
# FP-D7: fuzzy candidate shortlist for the zero/multi key-resolution call. Substring
# overlap on snake tokens of length >= 4 (skips tiny connective tokens like "of"/"in").
# ---------------------------------------------------------------------------------------
$allLeafPaths = @($headFlat.Keys)
# Same Write-Output -NoEnumerate discipline as Get-EverShippedSet / Get-KeyCandidates
# above -- a single fuzzy match must survive as a 1-element array.
function Get-FuzzyCandidates([string]$paramName) {
    $snake = ConvertTo-SnakeCase $paramName
    $tokens = @($snake -split '_' | Where-Object { $_.Length -ge 4 })
    if ($tokens.Count -eq 0) { Write-Output -NoEnumerate @(); return }
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($k in $allLeafPaths) {
        $leaf = $k.Split('.')[-1]
        foreach ($t in $tokens) {
            if ($leaf -like "*$t*") { [void]$found.Add($k); break }
        }
    }
    Write-Output -NoEnumerate @($found | Select-Object -Unique | Select-Object -First 8)
}

function Invoke-KeyResolution([string]$apiKeyIn, $cs) {
    $fuzzy = if ($cs.MatchClass -eq 'MULTI') { @($cs.Candidates) } else { Get-FuzzyCandidates $cs.Param }
    if ($fuzzy.Count -eq 0) { return @{ Ok = $true; Key = 'none_apply'; UsageIn = 0; UsageOut = 0 } }
    $criteria = @{}
    foreach ($c in $fuzzy) { $criteria[$c] = "The parameter `$($cs.Param)` corresponds to settings key ``$c``." }
    $criteria['none_apply'] = 'No settings.json key corresponds to this parameter -- it is a pure fixture/mechanism value with no settings-derived counterpart.'
    $state = @{
        fixture_sub          = $cs.EnclosingSub
        call_line            = $cs.CallLineText
        comment_block        = $cs.CommentBlock
        param_name           = $cs.Param
        code_found_candidates = @($cs.Candidates)
        broader_candidates   = @($fuzzy)
    }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            key_resolution = @{
                type = 'choice'
                instructions = "This fixture parameter's name did not map to exactly one settings.json key by mechanical camelCase-to-snake_case normalisation (code found $($cs.Candidates.Count) candidate(s)). Using `comment_block` and `call_line` for context, decide which settings key (if any) this literal is meant to track."
                criteria = $criteria
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error } }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{ Ok = $true; Key = $call.Response.answers.key_resolution.choice; UsageIn = $usageIn; UsageOut = $usageOut }
}

# ---------------------------------------------------------------------------------------
# FP-1 / FP-2 question sets. `verdict` is the ONLY answer code reads for either question
# (TRAP 2) -- the Noul diagnostics are printed alongside, never combined with `-and`/`-or`.
# ---------------------------------------------------------------------------------------
$fp1Criteria = @{
    mechanism_declared_ok     = 'The comment block declares this literal is MECHANISM (an arbitrary or structurally-motivated value, not meant to track settings.json), and that declaration is consistent with the evidence: `literal_equals_ever_shipped` may be true or false either way, so long as the comment''s own reasoning does not depend on tracking `matched_key`.'
    shipped_declared_ok       = 'The comment block declares this literal is SHIPPED BEHAVIOUR (deliberately derived from settings.json''s value for `matched_key`), and `literal_equals_ever_shipped` is true.'
    undeclared                = 'The comment block at this call site does not state which of the two classes (MECHANISM or SHIPPED BEHAVIOUR) this literal is.'
    declared_but_contradicted = 'The comment declares a class, but it is inconsistent with the evidence -- e.g. it claims SHIPPED BEHAVIOUR while `literal_equals_ever_shipped` is false, or it claims MECHANISM while its own reasoning ties the value to tracking `matched_key`.'
    ambiguous                 = '`comment_block`, `matched_key`, and `ever_shipped_values` together do not give enough information to place this call site in one of the classes above.'
}
$fp2Criteria = @{
    name_matches     = "The fixture Sub's body asserts exactly the property its name claims to test -- no more, no less."
    name_overclaims  = "The fixture Sub's name claims to test a broader or different property than what its body actually asserts."
    name_understates = "The fixture Sub's body asserts more, or something different, than what its name claims -- the name undersells what is actually being tested."
    ambiguous        = "The Sub's name or body do not give enough information to decide whether the name matches the assertion."
}

function Invoke-Fp1Verdict([string]$apiKeyIn, $cs, [string]$uid) {
    $state = @{
        fixture_sub   = $cs.EnclosingSub
        enclosing_kind = $cs.EnclosingKind
        call_line     = $cs.CallLineText
        comment_block = $cs.CommentBlock
        param_name    = $cs.Param
        literal_value = $cs.LiteralValue
        matched_key   = $cs.ResolvedKey
        ever_shipped_values = @($cs.EverShipped)
        # CODE COMPUTES SET MEMBERSHIP HERE, not Jev (docs/fixture-parser-check-spec.md
        # section 1's KEY DESIGN POINT) -- the boolean below is the authoritative answer to
        # "does the literal match a shipped value"; Jev is asked only whether the comment's
        # declared class is consistent with it, never to recompute it.
        literal_equals_ever_shipped = $cs.LiteralEqualsEverShipped
    }
    if ($uid) { $state.sample_uid = $uid }
    if ($DebugState) { Write-Host "STATE_DEBUG fp1 id=$(Get-Fp1Id $cs) keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)" }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            comment_declares_class = @{
                type = 'noul'
                instructions = "Does `comment_block` state whether the literal passed for `param_name` is MECHANISM (an arbitrary/structural value not meant to track settings.json) or SHIPPED BEHAVIOUR (deliberately derived from `matched_key`'s settings.json value)?"
            }
            class_matches_evidence = @{
                type = 'noul'
                instructions = "Is the class `comment_block` declares (if any) consistent with `literal_equals_ever_shipped` and `ever_shipped_values`? A SHIPPED BEHAVIOUR declaration is consistent only if `literal_equals_ever_shipped` is true. A MECHANISM declaration is consistent regardless of `literal_equals_ever_shipped`, so long as the comment's own reasoning does not depend on tracking `matched_key`."
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify this call site using `comment_block`, `matched_key`, `ever_shipped_values`, and `literal_equals_ever_shipped`. Do not recompute set membership -- `literal_equals_ever_shipped` is already the authoritative answer to that question."
                criteria = $fp1Criteria
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error } }
    $ans = $call.Response.answers
    $verdict = $ans.verdict.choice
    $probs = @{}
    $topProbability = $null
    if ($ans.verdict.probabilities) {
        foreach ($p in $ans.verdict.probabilities.PSObject.Properties) { $probs[$p.Name] = [double]$p.Value }
        if ($probs.Count -gt 0) { $topProbability = ($probs.Values | Measure-Object -Maximum).Maximum }
    }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{
        Ok = $true; Verdict = $verdict; TopProbability = $topProbability
        DeclaresClassNoul = $ans.comment_declares_class.noul
        MatchesEvidenceNoul = $ans.class_matches_evidence.noul
        UsageInputTokens = $usageIn; UsageOutputTokens = $usageOut
    }
}

function Invoke-Fp2Verdict([string]$apiKeyIn, $f2, [string]$uid) {
    $state = @{ fixture_sub = $f2.SubName; sub_body = $f2.Body }
    if ($uid) { $state.sample_uid = $uid }
    if ($DebugState) { Write-Host "STATE_DEBUG fp2 sub=$($f2.SubName) keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)" }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            name_matches_assertion = @{
                type = 'noul'
                instructions = "Does `sub_body` assert the property `fixture_sub`'s name claims to test?"
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify whether `fixture_sub`'s name matches what `sub_body` actually asserts."
                criteria = $fp2Criteria
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error } }
    $ans = $call.Response.answers
    $verdict = $ans.verdict.choice
    $probs = @{}
    $topProbability = $null
    if ($ans.verdict.probabilities) {
        foreach ($p in $ans.verdict.probabilities.PSObject.Properties) { $probs[$p.Name] = [double]$p.Value }
        if ($probs.Count -gt 0) { $topProbability = ($probs.Values | Measure-Object -Maximum).Maximum }
    }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{
        Ok = $true; Verdict = $verdict; TopProbability = $topProbability
        NameMatchesNoul = $ans.name_matches_assertion.noul
        UsageInputTokens = $usageIn; UsageOutputTokens = $usageOut
    }
}

# FP-D4 self-consistency aggregation -- mirrors tools/checks/commit-walker.ps1's
# Get-SelfConsistencyAggregate (defined locally here rather than shared: it is arithmetic,
# not the HTTP call, and docs/fixture-parser-check-spec.md section 4 only asks to reuse
# Invoke-Jev, not this function).
function Get-SelfConsistencyAggregate([object[]]$sampleResultsIn) {
    $verdictCounts = @{}
    foreach ($sr in $sampleResultsIn) {
        if (-not $verdictCounts.ContainsKey($sr.Verdict)) { $verdictCounts[$sr.Verdict] = 0 }
        $verdictCounts[$sr.Verdict] += 1
    }
    $pluralityVerdict = $null
    $pluralityCount = -1
    foreach ($sr in $sampleResultsIn) {
        $v = $sr.Verdict
        if ($verdictCounts[$v] -gt $pluralityCount) { $pluralityCount = $verdictCounts[$v]; $pluralityVerdict = $v }
    }
    $agreementRate = [math]::Round(($pluralityCount / $sampleResultsIn.Count), 3)
    $topProbs = @($sampleResultsIn | Where-Object { $null -ne $_.TopProbability } | ForEach-Object { $_.TopProbability })
    $meanTopProbability = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Average).Average), 3) } else { $null }
    $minTopProbability = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Minimum).Minimum), 3) } else { $null }
    $sampleVerdicts = ($sampleResultsIn | ForEach-Object { $_.Verdict }) -join ','
    $sampleTopProbs = ($sampleResultsIn | ForEach-Object { if ($null -ne $_.TopProbability) { [math]::Round([double]$_.TopProbability, 3) } else { $null } }) -join ','
    return [PSCustomObject]@{
        PluralityVerdict = $pluralityVerdict; AgreementRate = $agreementRate; Stable = ($agreementRate -ge 1.0)
        MeanTopProbability = $meanTopProbability; MinTopProbability = $minTopProbability
        SampleCount = $sampleResultsIn.Count; SampleVerdicts = $sampleVerdicts; SampleTopProbabilities = $sampleTopProbs
    }
}

$fp1Results = New-Object System.Collections.Generic.List[object]
$fp2Results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''
$usageInputTokens = 0
$usageOutputTokens = 0
$keyResolutionCalls = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# ---------------------------------------------------------------------------------------
# FP-1: resolve ambiguous keys first (FP-D7, unsampled), then sample the verdict question
# $Samples times per residual call site.
# ---------------------------------------------------------------------------------------
foreach ($cs in $residualSites) {
    if ($cs.MatchClass -ne 'ONE') {
        $kr = Invoke-KeyResolution $apiKey $cs
        if (-not $kr.Ok) { $apiFailed = $true; $apiFailMsg = "Jev key-resolution failed for $(Get-Fp1Id $cs): $($kr.Error)"; break }
        $keyResolutionCalls++
        $usageInputTokens += $kr.UsageIn
        $usageOutputTokens += $kr.UsageOut
        if ($kr.Key -and $kr.Key -ne 'none_apply' -and $everShippedByKey.ContainsKey($kr.Key)) {
            $set = $everShippedByKey[$kr.Key]
            $equals = $false
            foreach ($v in $set) { if ([math]::Abs($v - $cs.LiteralValue) -lt 0.0000001) { $equals = $true; break } }
            $cs.ResolvedKey = $kr.Key
            $cs.EverShipped = $set
            $cs.LiteralEqualsEverShipped = $equals
        }
    }

    $sampleResults = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Samples; $i++) {
        $uid = "$(Get-Fp1Id $cs):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $r = Invoke-Fp1Verdict $apiKey $cs $uid
        if (-not $r.Ok) { $apiFailed = $true; $apiFailMsg = "Jev FP-1 request failed for $(Get-Fp1Id $cs) (sample $($i+1)/$Samples): $($r.Error)"; break }
        $usageInputTokens += $r.UsageInputTokens
        $usageOutputTokens += $r.UsageOutputTokens
        $sampleResults.Add($r)
    }
    if ($apiFailed) { break }

    $agg = Get-SelfConsistencyAggregate $sampleResults
    $meanDeclares = [math]::Round((($sampleResults | ForEach-Object { [double]$_.DeclaresClassNoul } | Measure-Object -Average).Average), 3)
    $meanMatches = [math]::Round((($sampleResults | ForEach-Object { [double]$_.MatchesEvidenceNoul } | Measure-Object -Average).Average), 3)
    $id = Get-Fp1Id $cs
    $fp1Results.Add([PSCustomObject]@{
        Id = $id; EnclosingSub = $cs.EnclosingSub; Param = $cs.Param; Line = $cs.Line
        MatchedKey = $cs.ResolvedKey; EverShipped = ($cs.EverShipped -join ',')
        LiteralValue = $cs.LiteralValue; LiteralEqualsEverShipped = $cs.LiteralEqualsEverShipped
        Verdict = $agg.PluralityVerdict; AgreementRate = $agg.AgreementRate; Stable = $agg.Stable
        MeanTopProbability = $agg.MeanTopProbability; MinTopProbability = $agg.MinTopProbability
        DeclaresClassNoul = $meanDeclares; MatchesEvidenceNoul = $meanMatches
        SampleCount = $agg.SampleCount; SampleVerdicts = $agg.SampleVerdicts; SampleTopProbabilities = $agg.SampleTopProbabilities
        Baseline = if ($baseline.ContainsKey($id)) { $baseline[$id] } else { $null }
    })
}

# ---------------------------------------------------------------------------------------
# FP-2: one sampled question set per distinct residual sub (FP-D5 scope).
# ---------------------------------------------------------------------------------------
if (-not $apiFailed) {
    foreach ($f2 in $fp2Items) {
        $sampleResults = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $Samples; $i++) {
            $uid = "$($f2.SubName):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
            $r = Invoke-Fp2Verdict $apiKey $f2 $uid
            if (-not $r.Ok) { $apiFailed = $true; $apiFailMsg = "Jev FP-2 request failed for $($f2.SubName) (sample $($i+1)/$Samples): $($r.Error)"; break }
            $usageInputTokens += $r.UsageInputTokens
            $usageOutputTokens += $r.UsageOutputTokens
            $sampleResults.Add($r)
        }
        if ($apiFailed) { break }
        $agg = Get-SelfConsistencyAggregate $sampleResults
        $meanNameMatches = [math]::Round((($sampleResults | ForEach-Object { [double]$_.NameMatchesNoul } | Measure-Object -Average).Average), 3)
        $fp2Results.Add([PSCustomObject]@{
            SubName = $f2.SubName
            Verdict = $agg.PluralityVerdict; AgreementRate = $agg.AgreementRate; Stable = $agg.Stable
            MeanTopProbability = $agg.MeanTopProbability; MinTopProbability = $agg.MinTopProbability
            NameMatchesNoul = $meanNameMatches
            SampleCount = $agg.SampleCount; SampleVerdicts = $agg.SampleVerdicts; SampleTopProbabilities = $agg.SampleTopProbabilities
            Baseline = if ($baseline.ContainsKey($f2.SubName)) { $baseline[$f2.SubName] } else { $null }
        })
    }
}
$sw.Stop()

if ($apiFailed) {
    Write-Coverage ($fp1Results.Count + $fp2Results.Count)
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 7 (section 4.1 step 7): report, exit from `verdict`/stability alone.
# ---------------------------------------------------------------------------------------
Write-Coverage ($fp1Results.Count + $fp2Results.Count)
"KEY_RESOLUTION_CALLS=$keyResolutionCalls"
"USAGE_INPUT_TOKENS=$usageInputTokens"
"USAGE_OUTPUT_TOKENS=$usageOutputTokens"
"WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"

$fp1Unstable = @($fp1Results | Where-Object { -not $_.Stable }).Count
$fp2Unstable = @($fp2Results | Where-Object { -not $_.Stable }).Count
$fp1Bad = @($fp1Results | Where-Object { $_.Verdict -in @('undeclared', 'declared_but_contradicted', 'ambiguous') }).Count
$fp2Bad = @($fp2Results | Where-Object { $_.Verdict -in @('name_overclaims', 'name_understates', 'ambiguous') }).Count

if ($CountersOnly) {
    "COUNTERS_ONLY=true (per-item verdicts and aggregates withheld -- docs/harness-shadow-mode-protocol.md section 4a)"
} else {
    "FP1_RESULTS:"
    foreach ($res in $fp1Results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
        "  $($res.Id) [$stableTxt] verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability) key=$($res.MatchedKey) ever_shipped=[$($res.EverShipped)] literal=$($res.LiteralValue) equals_shipped=$($res.LiteralEqualsEverShipped) baseline=$($res.Baseline) [$agree] declares_class_noul=$($res.DeclaresClassNoul) matches_evidence_noul=$($res.MatchesEvidenceNoul) verdicts=[$($res.SampleVerdicts)] top_probs=[$($res.SampleTopProbabilities)]"
    }
    "FP2_RESULTS:"
    foreach ($res in $fp2Results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
        "  $($res.SubName) [$stableTxt] verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability) name_matches_noul=$($res.NameMatchesNoul) baseline=$($res.Baseline) [$agree] verdicts=[$($res.SampleVerdicts)] top_probs=[$($res.SampleTopProbabilities)]"
    }
    "FP1_UNSTABLE=$fp1Unstable"
    "FP2_UNSTABLE=$fp2Unstable"
    "FP1_BAD_VERDICTS=$fp1Bad"
    "FP2_BAD_VERDICTS=$fp2Bad"
}

# Markdown report.
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Fixture-parser check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/fixture-parser.ps1`` against ``-SubFilter $SubFilter -Samples $Samples`` at ``HEAD``.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| FIXTURE_SUBS | $fixtureSubs |")
$reportLines.Add("| LITERAL_CALL_SITES | $literalCallSites |")
$reportLines.Add("| PARAMS_DISTINCT | $paramsDistinct |")
$reportLines.Add("| SETTINGS_REVISIONS_WALKED | $settingsRevisionsWalked |")
$reportLines.Add("| KEYS_WITH_VALUE_HISTORY | $keysWithValueHistory |")
$reportLines.Add("| MATCHED_ONE_KEY | $matchedOne |")
$reportLines.Add("| MATCHED_ZERO_KEYS | $matchedZero |")
$reportLines.Add("| MATCHED_MULTI_KEYS | $matchedMulti |")
$reportLines.Add("| LITERAL_EQUALS_EVER_SHIPPED | $literalEqualsShipped |")
$reportLines.Add("| SITES_WITH_PROVENANCE_COMMENT | $sitesWithProvenance |")
$reportLines.Add("| SITES_JUDGED | $($fp1Results.Count + $fp2Results.Count) |")
$reportLines.Add("| KEY_RESOLUTION_CALLS | $keyResolutionCalls |")
if (-not $CountersOnly) {
    $reportLines.Add("| FP1_UNSTABLE | $fp1Unstable |")
    $reportLines.Add("| FP2_UNSTABLE | $fp2Unstable |")
    $reportLines.Add("| FP1_BAD_VERDICTS | $fp1Bad |")
    $reportLines.Add("| FP2_BAD_VERDICTS | $fp2Bad |")
}
$reportLines.Add("| USAGE_INPUT_TOKENS | $usageInputTokens |")
$reportLines.Add("| USAGE_OUTPUT_TOKENS | $usageOutputTokens |")
$reportLines.Add("| WALL_TIME_SEC | $([math]::Round($sw.Elapsed.TotalSeconds,2)) |")
$reportLines.Add('')
if (-not $CountersOnly) {
    $reportLines.Add('## FP-1 (fixture-literal provenance)')
    $reportLines.Add('')
    $reportLines.Add('| Id | Verdict | Agreement | Mean top prob | Key | Ever shipped | Literal | Equals shipped | Baseline | Agreement |')
    $reportLines.Add('|---|---|---|---|---|---|---|---|---|---|')
    foreach ($res in $fp1Results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $reportLines.Add("| $($res.Id) | $($res.Verdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.MatchedKey) | $($res.EverShipped) | $($res.LiteralValue) | $($res.LiteralEqualsEverShipped) | $($res.Baseline) | $agree |")
    }
    $reportLines.Add('')
    $reportLines.Add('## FP-2 (fixture name vs. assertion)')
    $reportLines.Add('')
    $reportLines.Add('| Sub | Verdict | Agreement | Mean top prob | Baseline | Agreement |')
    $reportLines.Add('|---|---|---|---|---|---|')
    foreach ($res in $fp2Results) {
        $agree = if ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $reportLines.Add("| $($res.SubName) | $($res.Verdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.Baseline) | $agree |")
    }
} else {
    $reportLines.Add('## Per-item detail withheld')
    $reportLines.Add('')
    $reportLines.Add('Run with `-CountersOnly` over a RESERVED window (docs/harness-shadow-mode-protocol.md section 4a). Per-item verdicts and every aggregate are withheld from both console and this report.')
}
$reportFull = Resolve-RepoPath $OutPath
Set-Content -Encoding UTF8 -Path $reportFull -Value ($reportLines -join "`r`n")
"Report written to $OutPath"

$anyBad = ($fp1Bad -gt 0) -or ($fp2Bad -gt 0) -or ($fp1Unstable -gt 0) -or ($fp2Unstable -gt 0)
if ($anyBad) { exit 1 } else { exit 0 }
