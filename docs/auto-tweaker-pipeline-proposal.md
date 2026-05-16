# Spec: Auto-Tweaker Pipeline (Console App + WinForm Settings Dialog)
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** new — `tools/AutoTweaker/AutoTweakerProgram.vb`, `tools/AutoTweaker/AutoTweakerCore.vb`, `tools/AutoTweaker/PromptBuilder.vb`, `tools/AutoTweaker/ClaudeApiClient.vb`, `tools/AutoTweaker/SettingsDiffApplier.vb`, `tools/AutoTweaker/TweakerConfig.vb`, `tools/AutoTweaker/TweakerState.vb`, `tools/AutoTweaker/AutoTweaker.csproj`; `UI/TweakSettingsForm.vb`, `UI/TweakSettingsForm.Designer.vb`; `UI/MainForm_Layout.vb` (add button + status polling)
**Prerequisites:** `csv-expansion-v0.4-proposal.md`, `analysis-script-proposal.md`, `failure-definition-proposal.md`

---

## 1. Background

Per project handover Section 16.1 (renamed "auto-tweaker"), the engine should periodically self-tune `settings.json` thresholds against empirical performance. A frontier LLM (Claude Opus, latest) receives the current settings + recent failure-rate data + reasoning context, returns a proposed diff, the engine applies it.

Q-answers shape the design:
- Q4/Q18: VB.NET console app under `/tools/AutoTweaker/`, host-agnostic for future Linux CLI port
- Q13: auto-commit toggleable via WinForm
- Q14: window of 120 verdicts default, must be same UTC session
- Q15: 40% failure-rate trigger default
- Q16: 10-row cooldown
- Q17: latest Claude Opus, version-agnostic
- Q18a: dry-run mode that writes the API payload to a `.txt` file instead of calling

---

## 2. Project Structure

### 2a. Console app (`tools/AutoTweaker/`)

Standalone .NET 8 console project, separate `.csproj`, references the main project's `Core/` for settings types.

```
tools/AutoTweaker/
├── AutoTweaker.csproj
├── AutoTweakerProgram.vb       Entry point. Reads tweaker_config.json,
│                               invokes AutoTweakerCore, exits 0/1/2 (see codes below).
│                               No Console.ReadKey or interactive prompts — runs to
│                               completion or fails. Linux-friendly.
├── AutoTweakerCore.vb          The pipeline. Eligibility check → window selection →
│                               failure-rate compute → trigger check → prompt build →
│                               API call (or dry-run write) → diff parse → apply or
│                               propose. All host-agnostic.
├── PromptBuilder.vb            Builds the prompt + JSON payload sent to Claude.
│                               Inputs: settings.json, recent CSV slice, failure-rate
│                               matrix, picked cells, trader-profile excerpt (Section 4
│                               rejected approaches inlined as constraints).
│                               Output: System message + User message strings.
├── ClaudeApiClient.vb          HTTPS client to Anthropic API. Latest-Opus discovery
│                               via /v1/models. Dry-run mode writes payload to file
│                               and returns nothing.
├── SettingsDiffApplier.vb      Validates the diff Claude returned (no removed-pattern
│                               keys, no version regression, schema-conformant), applies
│                               to settings.json, bumps version, appends change_log.
├── TweakerConfig.vb            POCO for tweaker_config.json. Hot-reloadable: file
│                               read fresh on each run.
└── TweakerState.vb             Persistent state across runs: last_run_timestamp,
                                last_run_csv_row_count, last_proposal_summary,
                                picked-cell history. Stored at
                                tools/AutoTweaker/state.json (gitignored).
```

### 2b. WinForm dialog (`UI/TweakSettingsForm.vb`)

Non-modal dialog, opened from a new MainForm button "Tweak Settings".

Controls:

| Control | Purpose |
|---|---|
| `chkAutoCommit` (Checkbox) | Auto-commit on/off (Q13) |
| `chkDryRun` (Checkbox) | Dry-run mode — writes API call to file instead of calling (Q18a) |
| `txtWindowSize` (TextBox, default 120) | Verdicts in failure-rate window (Q14) |
| `txtFailureThreshold` (TextBox, default 40) | Failure-rate % trigger (Q15) |
| `txtCooldownRows` (TextBox, default 10) | Min new rows between auto-tweaks (Q16) |
| `lblConfigPath` (Label, read-only) | Full path to `tweaker_config.json` (Q18c) |
| `lblCsvPath` (Label, read-only) | Full path to `analysis_log.csv` (Q18c) |
| `lblStatePath` (Label, read-only) | Full path to `state.json` (Q18c) |
| `lblTweakerStatus` (Label, dynamic) | One of: `Ready`, `Cooldown: N rows remaining`, `Waiting for session-aligned window: M/120 rows`, `Session boundary in window — restarting count`, `Insufficient tier-eligible rows: K/60` |
| `btnRunNow` (Button) | "Run Tweaker Now" — disabled unless `lblTweakerStatus = Ready` (Q18b) |
| `btnSave` (Button) | Writes textbox values to `tweaker_config.json` |
| `lblLastTweakSummary` (Label, multi-line read-only) | Last proposal: timestamp, summary, applied or pending |

Status polling: `lblTweakerStatus` updates after every analysis run (subscribe to `MainForm`'s post-analysis event) AND on a 30s timer when the form is open. Cheap — just rereads `state.json` and inspects CSV row count.

### 2c. Tweaker config file (`tools/AutoTweaker/tweaker_config.json`)

Gitignored. Created on first save.

```json
{
  "version": 1,
  "auto_commit_enabled": false,
  "dry_run_enabled": true,
  "window_size_verdicts": 120,
  "failure_rate_threshold_pct": 40,
  "cooldown_rows": 10,
  "min_tier_eligible_rows": 60,
  "csv_path": "bin/Debug/net8.0-windows/analysis_log.csv",
  "settings_path": "settings.json",
  "state_path": "tools/AutoTweaker/state.json",
  "dry_run_output_dir": "tools/AutoTweaker/dry_run_payloads/",
  "anthropic_model_alias": "latest-opus"
}
```

### 2d. Tweaker state file (`tools/AutoTweaker/state.json`)

Gitignored. Tracks runtime info.

```json
{
  "last_run_at_iso": "2026-05-05T13:00:00Z",
  "last_run_csv_row_count": 2580,
  "last_run_outcome": "PROPOSED" | "APPLIED" | "DRY_RUN_WRITTEN" | "BELOW_THRESHOLD" | "INELIGIBLE",
  "last_proposal_summary": "Lowered scoring.bbw_squeeze_penalty from 1.5 to 1.0 — squeeze runs were under-penalised in 18% of trending failures.",
  "last_pending_diff_path": "tools/AutoTweaker/proposed_diffs/2026-05-05T13-00-00.json",
  "picked_cell_history": [
    {"ts": "...", "tier": "STRONG_LONG", "window_min": 10, "atr_threshold": 0.3}
  ]
}
```

---

## 3. Pipeline Flow

> **v29 update (fixed-window mode, default).** `tweaker_config.window_mode` selects between disjoint (`fixed`) and overlapping (`sliding`) windows. Steps 1–3 below describe the new fixed-mode flow; the original sliding-mode logic is retained under the `Else` arm of `AutoTweakerCore.RunAsync` for legacy comparison. In fixed mode `cooldown_rows` is a no-op (the natural cooldown is "wait for the next disjoint batch to fill"); on the first v29 run `state.last_evaluated_row_index` is seeded to `currentRowCount` so historical sliding-era CSV rows are preserved on disk but not re-evaluated.

```
[AutoTweakerProgram.Main]
        │
        ├── Read tweaker_config.json
        ├── Read state.json (or initialise if missing)
        │
        ▼
[AutoTweakerCore.RunAsync]  (fixed mode — default)
        │
        ├─── 1. First-run init: if state.last_evaluated_row_index == -1
        │       → set to currentRowCount, write INELIGIBLE, exit 2 (next run
        │       will wait for WindowSize fresh rows to accumulate).
        │
        ├─── 2. Window-full check: if currentRowCount - last_evaluated_row_index
        │       < window_size_verdicts → INELIGIBLE, exit 2.
        │
        ├─── 3. Build disjoint window: allRows[last_evaluated_row_index ..
        │       last_evaluated_row_index + WindowSize - 1].
        │
        │       3a. Session-boundary scan within the disjoint slice. If a
        │           session-start hour falls between any two consecutive rows
        │           → outcome = SKIPPED_SESSION_BOUNDARY (RoundSummary written;
        │           streak NOT ticked; last_evaluated_row_index += WindowSize),
        │           exit 2.
        │
        │       3b. Tier-eligible check using TweakerConfig.EffectiveMinTier
        │           (null in config → max(15, ceil(WindowSize × 0.5))). If
        │           tier_eligible < threshold → outcome = SKIPPED_INSUFFICIENT_TIER
        │           (RoundSummary written; streak NOT ticked;
        │           last_evaluated_row_index += WindowSize), exit 2.
        │
        ├─── 4. Compute FailureRateMatrix over the window
        │       (reuses analysis/FailureRateMatrix.vb from Bundle 1).
        │       Pick the most stable cell per tier (per failure-definition spec).
        │       Append to state.picked_cell_history.
        │
        ├─── 5. Compute aggregate failure rate at picked cells.
        │       If aggregate < failure_rate_threshold_pct
        │       → outcome = BELOW_THRESHOLD, exit 0 (this is the success path —
        │       engine is performing fine, no tweak needed).
        │
        ├─── 6. PromptBuilder.Build():
        │         System: trader-profile constraints (no double-counting, no non-
        │                 directional padding, conservative bias, structural targets,
        │                 ATR for sizing not stops, list of rejected approaches).
        │         User:  current settings.json (full)
        │              + failure-rate matrix as table
        │              + picked-cell history (last 20)
        │              + last 50 CSV rows (verbose)
        │              + summarised distributions of stuck columns from the offline
        │                analysis report (cached output if available)
        │              + structured ask: "Propose changes to settings.json that
        │                                 would lower the aggregate failure rate at
        │                                 the picked cells. Output JSON of the form
        │                                 {reasoning: '...', diff: [{path: 'a.b.c',
        │                                 old_value: X, new_value: Y, justification:
        │                                 '...'}]}. Do not propose changes that violate
        │                                 the constraints in the system message."
        │
        ├─── 7. Branch on dry_run_enabled:
        │       TRUE  → ClaudeApiClient.WriteDryRunFile(prompt, payload)
        │               outcome = DRY_RUN_WRITTEN, exit 0
        │       FALSE → ClaudeApiClient.Call(prompt, payload) → response_json
        │
        ├─── 8. SettingsDiffApplier.Validate(response_json.diff)
        │         - Reject if any diff path matches a rejected-pattern key
        │           (e.g., adding a fixed-% target, re-adding a removed dead key,
        │            re-introducing non-directional padding pattern). Hard list
        │            in SettingsDiffApplier.
        │         - Reject if any old_value doesn't match current value (stale diff).
        │         - Reject if version regresses.
        │
        ├─── 9. Branch on auto_commit_enabled:
        │       TRUE  → SettingsDiffApplier.Apply(diff)
        │               • bump settings.json version
        │               • set modified_by = "auto-tweaker-vN"
        │               • append change_log entry: timestamp + summary +
        │                 cited failure rate + Claude reasoning excerpt
        │               • write file (FileSystemWatcher hot-reloads engine)
        │               outcome = APPLIED
        │       FALSE → Write proposed_diffs/<timestamp>.json (full response).
        │               Update state.last_pending_diff_path.
        │               outcome = PROPOSED
        │
        ├─── 10. Update state.json (last_run timestamp, row count, outcome,
        │        proposal summary).
        │
        └─── exit 0
```

### Exit codes

- `0` — clean run (no action OR action taken successfully)
- `1` — error (API failure, settings parse error, invalid diff)
- `2` — ineligible (cooldown / session-not-aligned / insufficient tiers). Not an error.

---

## 4. Latest-Opus Model Resolution (Q17)

`ClaudeApiClient.ResolveLatestOpusModel()`:

1. `GET https://api.anthropic.com/v1/models` with `x-api-key: $ANTHROPIC_API_KEY`
2. Filter the `data[]` for entries where `id` starts with `claude-opus-`
3. Sort by `created_at` descending
4. Return `data[0].id`
5. Cache the result for the duration of the process; refetch on next process start

If the API call fails or returns nothing matching, fall back to a hardcoded sentinel `claude-opus-latest` and let the actual messages call surface the error.

This makes the engine version-agnostic — a future `claude-opus-5-0-20271015` is picked up automatically the next time the auto-tweaker runs.

---

## 5. Dry-Run Mode (Q18a)

When `dry_run_enabled = true`:

- The full prompt (system + user) and request body JSON are written to:
  `tools/AutoTweaker/dry_run_payloads/<yyyyMMdd_HHmmss>.txt`
- File format:

```
=== AUTO-TWEAKER DRY RUN === <timestamp>
Trigger: aggregate failure rate 47.2% > threshold 40% over window 120 rows (NY session)

=== SYSTEM MESSAGE ===
<full system message text>

=== USER MESSAGE ===
<full user message text>

=== JSON REQUEST BODY ===
<pretty-printed body that would be POSTed to /v1/messages>

=== INSTRUCTIONS FOR HUMAN ===
Open a new Claude conversation. Paste the SYSTEM MESSAGE as the system prompt
(or as a leading user message if system prompts are unavailable in your client).
Paste the USER MESSAGE as your first message. Claude returns a JSON diff.
Save the JSON in tools/AutoTweaker/manual_diffs/<timestamp>.json. Run
AutoTweakerProgram.exe with --apply-manual <path> to apply.
```

The console app exits 0 after writing. No API call made. No state change beyond `last_run_outcome = DRY_RUN_WRITTEN`.

The manual-apply path supports `AutoTweakerProgram.exe --apply-manual <diff.json>` which loads the JSON, runs the SettingsDiffApplier validate+apply, updates state. This closes the loop without an API call.

---

## 6. WinForm Dialog Interaction Detail

### 6a. Opening the dialog

MainForm gets a new toolbar button or menu item: "Tweak Settings". Click → `New TweakSettingsForm().Show()`. Non-modal (Q24).

The form keeps a reference to MainForm to subscribe to a new event `MainForm.AnalysisCompleted` (raised at the end of each `RunAnalysisAsync()` whether auto-run or manual). Event handler refreshes `lblTweakerStatus`.

### 6b. Status polling logic

`UpdateStatusLabel()`:

1. Read `state.json`. If missing → `Status = Ready (no prior runs)`.
2. Read CSV current row count. If `(current - state.last_run_csv_row_count) < config.cooldown_rows` → `Status = Cooldown: N rows remaining`.
3. Walk back N=window_size rows in CSV; check session uniqueness.
   - If session boundary crossed → `Status = Session boundary in window — restarting count: M/120 rows`
4. Count tier-eligible rows in window. If < `min_tier_eligible_rows` → `Status = Insufficient tier-eligible rows: K/60`.
5. Else → `Status = Ready`. Enable `btnRunNow`.

Polling: in addition to subscription, a `System.Windows.Forms.Timer` ticks every 30s. Cheap I/O.

### 6c. Run Now click

Disabled unless Ready. On click:
1. Disable button, set `lblTweakerStatus = Running...`
2. Run `Process.Start("tools/AutoTweaker/bin/Debug/net8.0/AutoTweaker.exe")` with redirect-stdout
3. Async wait for exit
4. Refresh `lblTweakerStatus` and `lblLastTweakSummary` from updated state.json
5. Re-enable button if state shows Ready again

### 6d. Save click

1. Validate textbox inputs (positive integers, % between 1–99)
2. Read existing `tweaker_config.json`, update fields, write back
3. Show `MessageBox` confirmation
4. No engine restart needed — console app reads config fresh each invocation

---

## 7. Constraints and Safeguards

### 7a. Hard-coded rejected-pattern guard

`SettingsDiffApplier` keeps a hard-coded list of patterns that any proposed diff must reject:

- Adding any key matching `*_fixed_pct_*` (fixed-% targets — banned)
- Modifying `kelly.atr_target_multiplier` to a non-ATR-based formula
- Re-introducing keys removed in v15 cleanup (e.g., `bbw_none_bonus`, `OI_Prev15m`, etc.)
- Setting `regime_weights.enabled = false` (would disable Pass 2c silently)
- Setting `mtf_gate.enabled = false` (would remove the hard veto)
- Any diff that touches `version` directly (auto-bumped by SettingsDiffApplier itself)

Reject = log to state, exit 1 with reason. Don't apply. Don't notify Claude — the next run with updated context will likely propose differently.

### 7b. Diff scope cap

A single tweak proposal can change at most **3** keys. More than 3 → reject as too aggressive. Trader-profile says conservative; small steps.

### 7c. Settings-version monotonicity

Auto-tweaker increments `settings.json.version` by 1 per applied tweak. Sets `modified_by = "auto-tweaker-vN"` where N is the new version. Always appends to `change_log`.

### 7d. Fallback on API failure

If the API call fails (network, auth, rate limit), the run aborts with exit 1, state is updated to `last_run_outcome = ERROR`, and the WinForm shows the error in `lblLastTweakSummary`. No partial state corruption.

---

## 8. Build and Deployment

`AutoTweaker.csproj` is added to the solution, builds alongside the WinForm app. Output is `tools/AutoTweaker/bin/Debug/net8.0/AutoTweaker.exe` (Windows) or `dotnet AutoTweaker.dll` (Linux).

The console project must:
- Reference the main project's `Core/Settings/EngineSettings.vb` and `Core/Settings/SettingsLoader.vb`
- Reference the new `analysis/AnalysisConstants.vb`, `analysis/FailureRateMatrix.vb`, `analysis/ForwardReturnJoiner.vb`
- Have **zero references to System.Windows.Forms** (Linux-portable)
- Use `Newtonsoft.Json` (already referenced by main project) for settings serialisation

For the Linux port: same code, same csproj, run `dotnet AutoTweaker.dll` from cron. No code change required.

---

## 9. Out of Scope

- Auto-scheduled execution (cron/Windows Task Scheduler) — Q18b said `Process.Start` from WinForm. Linux cron config is a future deployment doc.
- Multi-LLM support (Sonnet, Haiku as fallback) — single-LLM (latest Opus) only in v1
- Diff visualisation in WinForm before apply — `lblLastTweakSummary` is text-only. A diff viewer is a possible v2.
- Rollback button — settings.json is in git; `git revert` is the rollback mechanism
- Adversarial-LLM checking (have a second model review the diff) — overkill for a tool that's already gated by hard-coded constraint list + 3-key scope cap
- Web dashboard — explicitly out of scope; the WinForm and (later) Linux logs are the interfaces

---

## 10. Acceptance

- `tools/AutoTweaker/AutoTweaker.csproj` builds clean, separate from MainForm. Zero `System.Windows.Forms` references.
- `analysis/` classes referenced by both the WinForm app and AutoTweaker.exe.
- WinForm dialog opens, all controls function, paths shown correctly.
- `lblTweakerStatus` updates reactively with each row addition.
- Dry-run mode produces a parsable text file with the expected sections.
- Manual-apply path applies a known-good diff and bumps settings version + change_log.
- Hard-coded rejection list catches at least one malicious diff in unit-test (e.g., trying to disable mtf_gate).
- 5+ end-to-end dry-run cycles on real CSV data produce sensible-looking proposal payloads (manual review).
- Linux build via `dotnet build tools/AutoTweaker/AutoTweaker.csproj` succeeds (validated via `dotnet build` from any platform).
