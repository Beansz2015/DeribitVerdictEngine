# Spec: Settings Snapshot History + Round Statistics + Configurable Diff Cap
**Proposed:** 2026-05-12
**Status:** PROPOSED 2026-05-12
**Target files:**
new — `tools/AutoTweaker/SnapshotManager.vb`, `tools/AutoTweaker/ConditionsExtractor.vb`, `tools/AutoTweaker/CompositeScorer.vb`, `tools/AutoTweaker/RoundStatsBuilder.vb`, `UI/RoundStatsForm.vb` (+ designer);
existing — `tools/AutoTweaker/AutoTweakerCore.vb`, `tools/AutoTweaker/SettingsDiffApplier.vb`, `tools/AutoTweaker/TweakerState.vb`, `tools/AutoTweaker/TweakerConfig.vb`, `tools/AutoTweaker/PromptBuilder.vb`, `tools/AutoTweaker/AutoTweaker.vbproj`, `UI/TweakSettingsForm.vb` (+ designer), `docs/TraderGuide.md`, `docs/UserManual.md`, `docs/DeribitIndicatorProject.md`, `.gitignore`;
regenerated — `docs/TraderGuide.pdf`, `docs/UserManual.pdf`

**Prerequisites:** `auto-tweaker-pipeline-proposal.md` and `failure-definition-v2-proposal.md` shipped (current master).

---

## 1. Background

The auto-tweaker currently proposes settings diffs when verdict accuracy degrades, but discards the *previous* settings when a new diff is applied. There's no record of which historical configurations performed well, no way to revert to a known-good past state when current conditions revert to a prior pattern, and no per-round visibility into how the engine is actually performing day-to-day.

This spec adds three operationally-adjacent features:

1. **Settings snapshot history** — automatic capture of settings that produced X consecutive successful auto-tweaker rounds. Each snapshot carries a conditions vector (regime, volatility, funding, etc.) so the auto-tweaker can later propose a *revert* to a past snapshot when current conditions match.
2. **Round statistics** — on-demand display of per-round success/fail breakdowns by verdict tier × confidence, viewable from inside the Tweaker Settings dialog.
3. **Configurable diff scope cap** — the previously-hardcoded 3-key cap in `SettingsDiffApplier` becomes a tunable setting (with revert proposals exempt by design).

All three are gated by the same data the v2 auto-tweaker already produces — no new computation pipeline needed.

---

## 2. Definitions

| Term | Meaning |
|---|---|
| **Round** | One auto-tweaker invocation that produced an evaluable outcome (`BELOW_THRESHOLD` / `APPLIED` / `PROPOSED` / `DRY_RUN_WRITTEN`). `INELIGIBLE` and `ERROR` outcomes are NOT rounds. |
| **Successful round** | A round whose outcome is `BELOW_THRESHOLD` (failure rate stayed under threshold, no change needed). |
| **Streak** | Number of consecutive successful rounds. Resets to 0 on any change-triggering outcome (`APPLIED` / `PROPOSED` / `DRY_RUN_WRITTEN`). Unaffected by `INELIGIBLE` / `ERROR`. |
| **Streak X** | The configurable threshold for snapshot creation (default 3). When the streak reaches X, the current settings are saved as a snapshot. |
| **Active snapshot** | The snapshot file representing the current settings during an ongoing streak. Status flips to `ACTIVE` in the manifest on creation and `ROTATED` when superseded. |
| **Condition bucket** | A categorical key for snapshot retention: `regime × volatility tier`. ≈12 distinct buckets at most. |
| **Composite score** | The metric used to compare snapshots within a bucket. Defined in §2g. |

---

## 3. Specification

### 3a. Data model — manifest schema

Single CSV at `settings_snapshots/manifest.csv` (repo root, gitignored). One row per snapshot. Schema:

```
Filename                  String — e.g. "settings_snapshot_20260512143022.json"
CreatedIso                String — UTC timestamp of snapshot creation (when streak first hit X)
FinalisedIso              String — UTC timestamp of the LAST SUCCESSFUL round of the streak.
                          Empty while streak is ACTIVE; populated when finalised.
StreakLength              Integer — final round count of the streak. 0 while ACTIVE
                          (placeholder); populated when finalised.
AvgFailureRatePct         Double — average of the picked-cell aggregate failure rate
                          across all successful rounds in the streak.
RegimeMix                 String — pipe-separated %s: "UP:25|DN:10|RB:60|TR:5"
AtrScaleAvg               Double — average of r.ATR × norms.ATRScaleFactor over rounds
AtrScaleMin               Double — min across rounds
AtrScaleMax               Double — max across rounds
FundingMin                Double — min FundingRate observed across streak
FundingMax                Double — max FundingRate observed across streak
NetPriceMovePct           Double — (endPrice - startPrice) / startPrice × 100
VolumeRatioAvg            Double — average VolumeRatio across streak rows
VerdictTierMix            String — "SL:5|L:15|WL:30|NT:30|WS:10|S:8|SS:2" (% per tier).
                          SL=STRONG_LONG, L=LONG, WL=WEAK_LONG, NT=NO_TRADE,
                          WS=WEAK_SHORT, S=SHORT, SS=STRONG_SHORT.
VWAPDevAvg                Double — average |VWAPDevPct| across streak rows
VWAPDevMin                Double — min
VWAPDevMax                Double — max
SpreadRegimeMix           String — pipe-separated %s: "T:80|N:18|W:2"
                          T=TIGHT, N=NORMAL, W=WIDE
OFIImbalanceMix           String — pipe-separated %s: "BD:30|SD:25|BAL:45"
                          BD=BUY_DOMINANT, SD=SELL_DOMINANT, BAL=BALANCED
ConditionBucket           String — "{Regime}_{VolatilityTier}" e.g. "TRENDING_UP_HIGH"
Status                    String — ACTIVE | ROTATED
RotationReason            String — empty unless ROTATED; otherwise "superseded by <filename>"
```

Volatility tier derivation:
- `LOW` if AtrScaleAvg < 0.85
- `NORMAL` if 0.85 ≤ AtrScaleAvg < 1.15
- `HIGH` if AtrScaleAvg ≥ 1.15

Regime derivation: dominant regime in `RegimeMix` (the one with the highest %).

Bucket key: `{Regime}_{VolatilityTier}` → 4 regimes × 3 vol tiers = **12 buckets**.

### 3b. Snapshot file format

Each snapshot is a verbatim copy of `settings.json` at the moment of creation, written to `settings_snapshots/settings_snapshot_<yyyyMMddHHmmss>.json`. No additional fields, no wrapper — the file is a directly-applicable settings.json so a manual revert is just `cp`.

The manifest holds all the metadata. Keeping the snapshot file pure means `SettingsDiffApplier` can apply a revert as a wholesale file copy without first stripping metadata.

### 3c. Streak tracking

New field on `TweakerState`:

```vb
Public Property CurrentBelowThresholdStreak As Integer = 0
Public Property ActiveSnapshotFilename     As String = ""    ' empty when no active snapshot
Public Property ActiveSnapshotCreatedIso   As String = ""
```

`AutoTweakerCore` increments `CurrentBelowThresholdStreak` after each round resolves:

```
On round outcome:
    BELOW_THRESHOLD:
        streak += 1
        If streak == config.StreakX AndAlso ActiveSnapshotFilename == "":
            SnapshotManager.Create(currentSettings, state, currentRoundConditions)
        Else if streak > config.StreakX AndAlso ActiveSnapshotFilename != "":
            ' streak still going — no new file, but conditions vector accumulates
            (handled inside SnapshotManager.AccumulateConditions)
    APPLIED | PROPOSED | DRY_RUN_WRITTEN:
        If ActiveSnapshotFilename != "":
            SnapshotManager.Finalise(state, lastSuccessfulRoundTimestamp)
        streak = 0
        ActiveSnapshotFilename = ""
    INELIGIBLE | ERROR:
        no-op (streak unchanged)
```

Persistence: `TweakerState.json` carries the streak counter and active snapshot reference across auto-tweaker invocations. Engine restart preserves the streak. (Note: this differs from the previous "reset on restart" recommendation — persistence is more useful given the auto-tweaker runs intermittently and restarts shouldn't penalise an ongoing streak.)

### 3d. Snapshot creation

When `streak == StreakX` and no active snapshot exists, `SnapshotManager.Create()`:

1. Copy current `settings.json` to `settings_snapshots/settings_snapshot_<yyyyMMddHHmmss>.json`.
2. Compute the initial conditions vector via `ConditionsExtractor.Extract(state.RoundHistory)` — see §3e.
3. Append a new row to `manifest.csv` with:
   - `CreatedIso` = now
   - `FinalisedIso` = empty
   - `StreakLength` = StreakX (the current streak)
   - All condition fields populated from the rounds that produced this streak
   - `Status = ACTIVE`
4. Update `state.ActiveSnapshotFilename` and `state.ActiveSnapshotCreatedIso`.
5. Console log: `[SnapshotManager] Created snapshot <filename> after streak hit X=<n>`.

### 3e. Conditions extraction

`ConditionsExtractor.Extract(roundHistory)` accepts a list of round summaries (each round's CSV slice) and computes the full conditions vector. Inputs:

- Each round has a `WindowStartRow` and `WindowEndRow` index into `analysis_log.csv`
- For each successful round, slice the CSV and aggregate:
  - Regime → distribution
  - ATR scale → min/max/avg (from `ATRMultiplier` column 65)
  - Funding rate → min/max (from `FundingRate` column 38)
  - Net price move → (last row's `Price` - first row's `Price`) / first × 100
  - Volume ratio → avg of `VolumeRatio` column 19
  - Verdict tier mix → counts per tier as %
  - VWAP dev → min/max/avg of `|VWAPDevPct|` column 21
  - Spread regime → bucket `SpreadBps` by `cfg.Indicators.Spread.{TightThresholdBps,WideThresholdBps}` and report %s
  - OFI imbalance → from `OFISignal` column 47 directly, count %s of `BUY_DOMINANT` / `SELL_DOMINANT` / `BALANCED`
- Average failure rate → from `state.RoundHistory[*].AggregateFailureRatePct` (added per §3f)

When extending an existing ACTIVE snapshot's conditions (streak still growing past X), the extractor **re-computes from the full streak's row range each time**, not incrementally. Cleaner and avoids drift; the cost is trivial (a few thousand CSV rows).

### 3f. Round history persistence

`TweakerState` gains a list of recent round summaries:

```vb
Public Class RoundSummary
    Public Property RoundIso As String              ' ISO 8601 UTC
    Public Property Outcome As String               ' BELOW_THRESHOLD / APPLIED / etc.
    Public Property WindowStartRow As Integer
    Public Property WindowEndRow As Integer
    Public Property AggregateFailureRatePct As Double
    Public Property PickedCellsJson As String       ' compact JSON of {tier:{window,thr,n,fails}}
End Class

Public Property RoundHistory As New List(Of RoundSummary)
```

Cap: keep last **50** rounds. Older entries dropped on each save. 50 rounds at default cooldown=10 covers about 8+ hours of evaluation; plenty for round-stats display and conditions backfill.

### 3g. Snapshot finalisation

When the next round comes back with a change-triggering outcome AND there's an active snapshot:

1. `state.ActiveSnapshotFilename` identifies the manifest row to update.
2. Update that row in-place:
   - `FinalisedIso` = timestamp of the **last successful round** (most recent BELOW_THRESHOLD round before this interruption)
   - `StreakLength` = `state.CurrentBelowThresholdStreak` (which still holds the pre-reset value at this point)
   - Re-compute condition fields one final time over the full streak window
3. Run bucket-rotation check (§3h).
4. Clear `state.ActiveSnapshotFilename`, set `state.CurrentBelowThresholdStreak = 0`.
5. Console log: `[SnapshotManager] Finalised snapshot <filename> at streak length <n>`.

### 3h. Composite score and bucket rotation

When a snapshot is finalised, check whether it should supersede the existing ACTIVE snapshot in its bucket:

**Composite score** (computed for each candidate snapshot):

```
StreakLengthClamped = Math.Min(StreakLength, config.StreakLengthClamp)   ' default clamp = 20
Score = (100.0 - AvgFailureRatePct) + (StreakLengthClamped × config.StreakWeight)
```

Worked examples with default `StreakWeight = 1.5`, `StreakLengthClamp = 20`:

| Snapshot | Fail % | Streak | Score | Interpretation |
|---|---|---|---|---|
| A | 25% | 5 | 82.5 | Moderate both |
| B | 35% | 10 | 80.0 | Higher fail, long streak |
| C | 20% | 8 | 92.0 | Low fail, decent streak |
| D | 15% | 3 | 89.5 | Lowest fail, short streak |
| E | 40% | 30 (clamped to 20) | 90.0 | Higher fail, very long streak |

Failure rate dominates (weighted at 1 point per %), streak adds a secondary nudge (1.5 per round, capped at 20).

**Rotation rule**:

```
On finalisation of new snapshot N in bucket B:
    Let existing = ACTIVE snapshot in bucket B (if any)
    If existing is Nothing:
        N stays ACTIVE.
    Else If Score(N) > Score(existing):
        N stays ACTIVE.
        Mark existing as ROTATED, RotationReason = "superseded by <N.Filename>".
        Delete existing's .json file (manifest row retained as historical record).
    Else:
        N is immediately marked ROTATED, RotationReason = "score <Score(N)> <= existing <Score(existing)>".
        N's .json file is deleted.
```

`existing.Score` is recomputed at rotation-check time using the same formula, so the comparison uses fresh-formula values.

### 3i. Revert mechanism

When the auto-tweaker fires a non-`BELOW_THRESHOLD` round (i.e., normally it would propose a tweak diff), the model receives an extended prompt:

```
System message (existing trader-profile constraints) + new addition:
"There is a history of past settings that achieved successful streaks under
specific market conditions. Manifest is attached. Current market conditions
vector is also attached. If a past snapshot's conditions strongly match
current conditions AND its composite score is significantly higher than
the current settings would achieve, you may propose REVERT to that
snapshot instead of a fresh diff."

User message (existing fields) + new additions:
- Snapshot manifest (CSV content, only ACTIVE rows)
- Current conditions vector (extracted from the just-completed round's window)
```

Response format (extended):

```json
{
  "action": "tweak" | "revert",
  "tweak_diff": [...] (when action=tweak; same as existing v1)
  "revert_target": "settings_snapshot_20260510143022.json" (when action=revert)
  "reasoning": "..."
}
```

`SettingsDiffApplier` handles `action=revert`:

1. Validate `revert_target` matches a row in `manifest.csv` with `Status = ACTIVE` and the snapshot file exists. If not, reject.
2. Apply the snapshot file as a wholesale replacement of `settings.json`:
   - Read snapshot file
   - Bump `version`, set `modified_by = "auto-tweaker-revert"`
   - Append to `change_log`: `"v<N> reverted to snapshot <filename> created <createdIso>, streak <length> rounds, conditions match: <reasoning excerpt>"`
   - Write `settings.json`
3. Streak resets to 0 (this WAS an interruption-triggering outcome; finalisation already happened).

**Revert approval flow** (per D4 answer):

- Reverts use the same `auto_commit_enabled` toggle as tweaks. When `auto_commit_enabled = true`, reverts are applied automatically. When false, the revert proposal is written to `tools/AutoTweaker/proposed_diffs/<timestamp>.json` for manual review.
- This aligns with the long-term CLI-on-Linux deployment target.
- Pre-revert validation still applies: the snapshot file must not contain any rejected-pattern keys (`SettingsDiffApplier.Validate` runs over the snapshot's content before apply).

**Revert exempts the diff-key cap** (per §3j) — a revert is by definition many keys, and the snapshot's provenance (proven-successful settings from the canonical history) is the validation gate.

### 3j. Configurable diff scope cap

Currently `SettingsDiffApplier` hard-codes the max-keys-per-proposal at `3`. Move to `tweaker_config.json`:

```json
{
  ...
  "max_keys_per_proposal": 3
}
```

`TweakerConfig` gains:

```vb
Public Property MaxKeysPerProposal As Integer = 3
```

`SettingsDiffApplier.Validate(items, currentSettingsJson, maxKeysPerProposal)` accepts the cap as a parameter rather than a constant. Default remains 3 — no behaviour change at default.

UI exposure: a textbox in `TweakSettingsForm` labelled "Max keys per tweak proposal" (default 3, integer ≥ 1). Save routes through `SettingsLoader.Save` like the other tweaker settings.

Reverts are exempt — `SettingsDiffApplier.ApplyRevert()` doesn't invoke the cap check. The snapshot source is the gate.

### 3k. Round Statistics — display

New WinForm `UI/RoundStatsForm.vb`, non-modal, opened from a button inside `TweakSettingsForm` (NOT a status-bar link, per user preference). Sized roughly 720×520 px.

**Trigger**: `btnShowRoundStats` button inside `TweakSettingsForm`. On click, opens or brings-to-front the `RoundStatsForm` window.

**Content (Medium depth)**: a single RichTextBox showing:

```
═══════════════════════════════════════════════════════════════════════
  ROUND STATISTICS — last 5 auto-tweaker rounds
═══════════════════════════════════════════════════════════════════════

  Current streak: 4 successful rounds (need 3 for snapshot — ACTIVE)
  Active snapshot: settings_snapshot_20260512094521.json
  Snapshot bucket: TRENDING_UP_NORMAL

  ─── Round 2026-05-12 14:32:18 UTC   (BELOW_THRESHOLD)  ────────────────
    Aggregate failure rate: 28.5%   (threshold 40%)
    Window: 120 rows | Session: NY

    Verdict accuracy (this round's window):
      STRONG_LONG  / Confidence: HIGH    :  12 correct / 3 wrong  (80% success)
      LONG         / Confidence: MEDIUM  :  18 correct / 9 wrong  (67% success)
      WEAK_LONG    / Confidence: LOW     :   8 correct / 7 wrong  (53% success)
      STRONG_SHORT / Confidence: HIGH    :   5 correct / 2 wrong  (71% success)
      SHORT        / Confidence: MEDIUM  :   9 correct / 5 wrong  (64% success)
      WEAK_SHORT   / Confidence: LOW     :   4 correct / 6 wrong  (40% success)
      NO_TRADE     / Confidence: N/A     :  32 rows  (informational only)

  ─── Round 2026-05-12 13:48:02 UTC   (BELOW_THRESHOLD)  ────────────────
    [same structure as above]

  ─── Round 2026-05-12 13:18:47 UTC   (APPLIED)  ──────────────────────
    Aggregate failure rate: 47.2%   (threshold 40%)  ← TRIGGERED
    Settings change: scoring.bbw_squeeze_penalty 1.5 → 1.0
    Reasoning excerpt: "BBW squeeze events were under-penalised in 18% of failures."

  [...] (5 rounds total displayed)
```

**Computation**: `RoundStatsBuilder.Build(state, csvPath, n=5)` produces the report text. Per-row accuracy uses the v2 barrier-hit logic from `FailureRateMatrix.WalkBars`, applied to ALL directional verdicts (not just STRONG/MEDIUM as in the failure-rate matrix) so WEAK accuracy is captured for the trader's information. NO_TRADE rows are reported as informational counts only — no success/fail evaluation.

**Cost**: ~50–100ms for 5 rounds × 120 rows each × per-bar barrier walk. Acceptable for on-demand click; no async needed.

**Refresh**: button "Refresh" inside `RoundStatsForm` re-runs the build. Default behaviour on open is fresh build.

### 3l. Settings.json additions

No `settings.json` changes for this spec. Everything lives in `tweaker_config.json`:

```json
{
    "auto_commit_enabled": false,
    "dry_run_enabled": true,
    "window_size_verdicts": 120,
    "failure_rate_threshold_pct": 40,
    "cooldown_rows": 10,
    "min_tier_eligible_rows": 60,
    "max_keys_per_proposal": 3,
    "snapshot_streak_x": 3,
    "streak_weight": 1.5,
    "streak_length_clamp": 20,
    "csv_path": "bin/Debug/net8.0-windows/analysis_log.csv",
    "settings_path": "settings.json",
    "state_path": "tools/AutoTweaker/state.json",
    "snapshots_dir": "settings_snapshots",
    "manifest_path": "settings_snapshots/manifest.csv",
    "dry_run_output_dir": "tools/AutoTweaker/dry_run_payloads/",
    "anthropic_model_alias": "latest-opus"
}
```

New fields:
- `max_keys_per_proposal` (3) — previously hardcoded
- `snapshot_streak_x` (3) — rounds before snapshot is created
- `streak_weight` (1.5) — composite score weight per round
- `streak_length_clamp` (20) — cap on streak length contribution
- `snapshots_dir`, `manifest_path` — derived paths

### 3m. WinForm UI additions

`UI/TweakSettingsForm.vb` gains:

| New control | Purpose |
|---|---|
| `txtSnapshotStreakX` | Binds to `snapshot_streak_x` |
| `txtMaxKeysPerProposal` | Binds to `max_keys_per_proposal` |
| `txtStreakWeight` | Binds to `streak_weight` |
| `lblActiveSnapshot` | Read-only display: shows current `state.ActiveSnapshotFilename` and `state.CurrentBelowThresholdStreak` |
| `btnShowRoundStats` | Opens `RoundStatsForm` non-modally |
| `btnOpenSnapshotsDir` | Opens `settings_snapshots/` in file explorer (`Process.Start`) |

Layout: insert new controls below the existing `txtCooldownRows` row, grouped under a new section header label "Snapshot history".

`UI/RoundStatsForm.vb`: new file. Single RichTextBox showing the round stats text; `btnRefresh` to re-run the build; `btnClose` to close. Subscribes to `MainForm.AnalysisCompleted` to track when new data is available (label updates: "New rounds since last refresh: N").

### 3n. PromptBuilder extension

`PromptBuilder.Build` accepts additional arguments:

```vb
Public Shared Function Build(
    settingsJson As String,
    recentCsvSlice As String,
    failureMatrix As List(Of FailureCellResult),
    pickedCellHistory As List(Of PickedCellEntry),
    manifestActiveRows As String,        ' NEW — CSV content of ACTIVE snapshots
    currentConditions As String,         ' NEW — extracted conditions vector
    maxKeysPerProposal As Integer        ' NEW — passed to system message
) As (SystemMessage As String, UserMessage As String)
```

The system message section about constraints adds:

```
You may propose one of two actions in the response:
  1. TWEAK — propose up to {MaxKeysPerProposal} key changes to settings.json.
     The standard hard-rejection list applies (see SettingsDiffApplier constraints).
  2. REVERT — if a past snapshot's conditions strongly match the current
     conditions AND its historical composite score is meaningfully higher
     than the current settings would likely achieve, propose a revert to
     that snapshot.

Manifest of past ACTIVE snapshots and the current conditions vector are
provided in the user message. A REVERT can change many keys at once
(it's a wholesale replacement), so the max-keys constraint does not apply
to reverts. The snapshot's provenance — proven successful for streak rounds
under bucket-matching conditions — is the validation gate.

Default to TWEAK unless the conditions match is strong and the score
delta is meaningful.
```

### 3o. SettingsDiffApplier changes

```vb
' Validate now accepts the cap as a parameter:
Public Shared Function Validate(items As List(Of DiffItem),
                                currentSettingsJson As String,
                                maxKeysPerProposal As Integer) As DiffValidationResult

' New entry point for reverts:
Public Shared Sub ApplyRevert(snapshotPath As String,
                              settingsPath As String,
                              reasoning As String)
    ' 1. Read snapshot content
    ' 2. Validate (re-run rejected-pattern check on snapshot content; reverts to bad
    '    snapshots should still fail — snapshot integrity is not absolute trust)
    ' 3. Parse current settings.json to get current version
    ' 4. Bump version, set modified_by = "auto-tweaker-revert"
    ' 5. Append change_log entry citing snapshot filename + reasoning
    ' 6. Write settings.json
End Sub
```

### 3p. .gitignore additions

```
settings_snapshots/
```

Single directory entry — covers the manifest CSV and all snapshot files.

---

## 4. Documentation updates

### 4a. TraderGuide.md

Add a new subsection under Section 17 "Working with the App":

```
### Settings Snapshots

When the auto-tweaker has run for several consecutive rounds without
proposing any settings changes, the engine considers those settings
"proven" for the current market conditions and saves a snapshot. If
market conditions later revert to a similar pattern, the auto-tweaker
may propose reverting to one of these snapshots instead of tweaking
fresh keys.

You can see:
  - Active snapshot (if any) in the Tweaker Settings dialog
  - Full history of saved snapshots in settings_snapshots/manifest.csv
  - Round-level statistics for the last 5 rounds via the "Show Round Stats"
    button in the Tweaker Settings dialog

Snapshots are bucketed by regime × volatility tier (12 buckets), with the
best-scoring snapshot kept per bucket. Score blends failure rate
(weighted heavier) and streak length.

When a revert proposal fires, it follows the same auto-commit / dry-run
toggles as a regular tweak.
```

### 4b. UserManual.md

Add a new Section 20 "Settings Snapshot History" covering:

- §20a. Conceptual overview (round, streak, snapshot, condition bucket)
- §20b. Streak tracking (when increments, resets, persistence)
- §20c. Snapshot creation trigger (streak X, manifest entry)
- §20d. Conditions vector — full field list with formulas
- §20e. Composite score formula and worked examples
- §20f. Bucket rotation rule
- §20g. Revert mechanism (when proposed, validation, apply path)
- §20h. Round Statistics display

Add to Section 19 "Tweak Settings & Auto-Tweaker":
- New paragraph in §19's settings list covering the new tweaker_config fields (`snapshot_streak_x`, `streak_weight`, `streak_length_clamp`, `max_keys_per_proposal`)
- Note that `max_keys_per_proposal` is no longer hard-coded; document the cap as a configurable safeguard

Update Section 17 "CSV Logging" — add a paragraph about the new `settings_snapshots/manifest.csv` companion file (not a CSV column change, just an adjacent log).

### 4c. PDF regeneration

After .md updates:

```bash
cd docs
pandoc TraderGuide.md -o TraderGuide.pdf --pdf-engine=xelatex --template=TraderGuide-template.tex --toc --toc-depth=2 -V geometry:a4paper
pandoc UserManual.md  -o UserManual.pdf  --pdf-engine=xelatex --template=manual-template.tex      --toc --toc-depth=2 -V geometry:a4paper
```

Both PDFs committed alongside the .md files (existing pattern).

### 4d. DeribitIndicatorProject.md

§16.5 active-bundle table gets a new entry for this spec under "post-Bundle 4". §16.6 parked observations gain:

**P7. Live per-analysis success/fail display (configurable windows + sessions).**
*Condition:* trader confirmed need 2026-05-12 — settings-snapshot work showed that round-level stats (every ~10–15 min) are insufficient for real-time trading-style adaptation. User wants a panel showing current settings' success/fail % over multiple rolling windows (e.g., last 30 min, last 60 min, current session) updated on every analysis run, not just on auto-tweaker firing.
*Action when triggered:* spec `live-performance-display-proposal.md`. New WinForm panel (or extension of MainForm output) that re-evaluates the v2 barrier-hit logic over the latest N rows on every `AnalysisCompleted` event. Bucket the windows by configured session (Asia / London / NY). Show alongside the verdict header. Defer until snapshot system + auto-tweaker have produced ≥1 month of round-level data and the trader can articulate which windows matter most.

§15 Version History gets a new entry for the snapshot bundle.

---

## 5. Acceptance

- `dotnet build` clean (0/0).
- `tools/AutoTweaker/AutoTweaker.vbproj` still targets `net8.0`, zero WinForms references in the console-app dependency tree.
- Run the AutoTweaker console app against a CSV with ≥4 successful rounds in a row:
  - On round 3 (streak hits X=3), `settings_snapshots/settings_snapshot_<ts>.json` is created
  - Manifest gains a row with `Status=ACTIVE`, `FinalisedIso=""`, `StreakLength=3`
  - On round 4 (still below threshold), no new snapshot file; manifest row's conditions update on each successful round
- Trigger an interrupting round (force the failure rate to exceed threshold on a test CSV):
  - Active snapshot's manifest row updates: `FinalisedIso` = last successful round's timestamp; `StreakLength` = final value
  - If no existing same-bucket snapshot, status stays `ACTIVE`
  - If existing same-bucket snapshot has lower composite score, it's marked `ROTATED` + its .json file deleted
- TweakSettingsForm shows the new controls; Save persists to `tweaker_config.json`
- TweakSettingsForm "Show Round Stats" button opens RoundStatsForm with last 5 rounds rendered correctly
- Round stats display correctly handles a mix of `BELOW_THRESHOLD` and change-triggering rounds (the latter show the diff summary and reasoning)
- `max_keys_per_proposal=5` setting allows a 5-key tweak proposal to pass validation; setting back to 3 rejects it (verify validation parameter passes through)
- Force a revert path in dry-run mode: API payload includes manifest + conditions vector; manual-apply with a revert response correctly replaces settings.json from the snapshot
- TraderGuide.md and UserManual.md updated; both PDFs regenerated; visual inspection of PDFs confirms new sections render
- `.gitignore` covers `settings_snapshots/`
- No spec-rejected patterns introduced

---

## 6. Out of Scope

- **Live per-analysis success/fail display** — Section 4d's P7. Distinct feature, future spec.
- **Pareto-frontier snapshot retention** — chose 1-per-bucket composite-score retention. Pareto evaluated and rejected for simplicity.
- **Sidecar conditions detail JSON** — manifest CSV holds all current conditions data. Add later if richer detail proves useful.
- **Snapshot diff visualisation** — UI to show diff between current settings and a candidate revert. Useful but not v1.
- **Manual snapshot trigger** — a "Save current settings as snapshot" button. Out of scope; the streak-based automatic save handles the intended use case.
- **Cross-bucket nearest-neighbour reuse** — if no exact-bucket match, fall back to nearest-bucket snapshot. Deferred; v1 requires exact bucket match.
- **Bucket schema versioning** — if the 12-bucket scheme changes later, old manifest rows would need re-bucketing. Out of scope; address when/if buckets change.
- **Persistent round history beyond 50** — the 50-round cap on `state.RoundHistory` is sufficient for round-stats display and conditions extraction. Longer history goes into the CSV-level analysis report instead.

---

## 7. Implementation notes

- The `SnapshotManager.Finalise` step that re-extracts conditions over the full streak window requires walking the CSV — keep this efficient with a single-pass slice. Cost should stay sub-100ms.
- `RoundSummary.PickedCellsJson` is stored as a compact JSON string rather than a nested object for state.json simplicity. Parse only when displaying round stats.
- `ConditionsExtractor` is host-agnostic (no WinForms refs). Lives in `tools/AutoTweaker/` and is included in `AutoTweaker.vbproj`.
- `RoundStatsBuilder` uses `FailureRateMatrix.WalkBars` directly to compute per-verdict accuracy. To handle WEAK_* tiers (which are excluded from the failure-rate matrix), the builder runs the barrier walk with thresholds matched by tier — for WEAK use the same MEDIUM threshold values (`{0.3, 0.5}` per v2 spec) since WEAK is a "no trade" tier; the success metric is informational, not used for any decision.
- Snapshot deletion (rotation) uses `File.Delete` with a try/catch. Failure to delete is non-fatal; logged but doesn't abort the auto-tweaker.
- Manifest CSV writes are O(file length) — read all rows, modify the target row, write back. For typical 12–24 row manifests this is sub-millisecond.
- Verify the `Filename`-as-CSV-cell handling — embedded commas are unlikely in our timestamped filenames but quote-escape on write anyway to be safe.
- TweakSettingsForm's "Active snapshot" label refresh should subscribe to `MainForm.AnalysisCompleted` event (already raised per the v2 auto-tweaker spec) so the user sees real-time streak counter movement.

---

**End of spec.** Implementation expected to be a single Sonnet session — bounded but substantial (~6–8 new/modified files). Estimated 4–6 hours including doc updates and PDF regeneration. The composite score, bucket rotation, and revert mechanics are the only architecturally non-trivial parts; the rest is straightforward file I/O and UI wiring.
