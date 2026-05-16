# Auto-Tweaker Fixed Window + MinTier Rework — Proposal

**Status:** ✅ IMPLEMENTED — 2026-05-17 (settings.json v28 → v29). RoundHistory cap settled at 1000 (open question §6).
**Settings.json:** v28 → v29 (ships last; after OHLC gap-backfill and target-hit specs)
**Spec dependency:** None functional. Sequenced last by user preference — perf-display changes (OHLC + target-hit) land first.

---

## Motivation

The auto-tweaker currently uses a **sliding** window when evaluating recent failure rates. With `cooldown_rows=10` and `WindowSize=30`, consecutive evaluations share 20 of 30 rows — the same bad trade contributes to multiple consecutive "below-threshold" rounds.

The snapshot-history mechanic relies on "3 consecutive below-threshold rounds" being three **independent** statistical signals. With sliding windows, consecutive rounds are correlated, so 3-in-a-row can fire from a single bad cluster of trades rather than three distinct bad windows.

Additionally:

- `min_tier_eligible_rows` default of 60 was calibrated for the default `WindowSize=120`. When the user lowers WindowSize to 30, the gate becomes mathematically unreachable (you can't have 60 directionals in 30 rows).
- `min_tier_eligible_rows` is not exposed in the Tweak Settings dialog — silent unreachable threshold.

---

## Design changes

### 1. Switch to fixed (non-overlapping) windows

A "round" is now `N` consecutive CSV rows where `N = WindowSize`. Rounds are disjoint:

```
Round 1: rows 1..30
Round 2: rows 31..60
Round 3: rows 61..90
...
```

Each row contributes to at most one round. Consecutive rounds are statistically independent — the "3 in a row" snapshot trigger now means what it claims to mean.

**State tracking:**

- New `state.LastEvaluatedRowIndex` (Integer, default `-1` = uninitialised) — highest CSV row index already consumed by a completed round.
- Eligibility: `currentRowCount - LastEvaluatedRowIndex >= WindowSize` **AND** no session boundary crosses within rows `[LastEvaluatedRowIndex+1 .. LastEvaluatedRowIndex+WindowSize]`.
- After a completed round (evaluable or skipped), advance `LastEvaluatedRowIndex += WindowSize`.

**First-run initialisation (v27 upgrade path):**

On the first auto-tweaker run after v27 ships, if `LastEvaluatedRowIndex == -1` (the upgrade default), initialise it to **`currentRowCount`**, not 0. This means the first fixed-mode round evaluates rows that arrive *after* the v27 upgrade — historical sliding-mode data is preserved in the CSV (for analysis purposes) but not re-evaluated under the new logic. Avoids:
- Auto-tweaker firing an immediate tweak based on stale historical data
- Mixing sliding-era and fixed-era data in the same RoundHistory streak
- Inflated "first round" results from a window that happens to land on an unusually good/bad historical stretch

Log a one-time INFO message: `"[AutoTweaker] First v27 run — LastEvaluatedRowIndex initialised to currentRowCount=N. Historical rows preserved but not re-evaluated."`

**Backward-compat:** `cooldown_rows` retained in config but treated as no-op when mode=fixed. Log a one-time INFO message on first read. The natural cooldown is "wait for the next disjoint batch to fill".

### 2. MinTier as a statistical floor (semantics unchanged, behaviour clarified)

MinTier semantics already match your reading:
- Count of STRONG/MEDIUM directional rows in the WindowSize-row window.
- If `tier_eligible_count < MinTier` → don't evaluate this window.

**Behaviour change in fixed mode:** when MinTier is unmet, treat the round as **SKIPPED** (not BELOW_THRESHOLD).

- `state.RoundHistory` entry tagged `SKIPPED_INSUFFICIENT_TIER`
- `LastEvaluatedRowIndex` advances by WindowSize as normal
- **The streak counter does NOT tick** — a skipped round is not evidence of bad performance, just thin data

This preserves the integrity of the streak count. Without this, a session with mostly NO_TRADEs could accidentally trigger a snapshot from skipped rounds masquerading as failures.

### 3. Default MinTier rescaling

Computed at config-load time:

```
If user explicitly set min_tier_eligible_rows in tweaker_config.json:
    use user value
Else:
    min_tier_eligible_rows = max(15, ceil(WindowSize × 0.5))
```

**Why 15 as the floor:** binomial 95% CI at n=15 is roughly ±25%. Below that, the failure rate is too noisy to act on. Above WindowSize×0.5, directional density needs to consistently exceed 50% for any window to qualify — too restrictive for chop sessions.

**Why scale with WindowSize:** keeps the directional-density requirement (~50%) constant regardless of WindowSize. Matches the historical implicit assumption (60/120 = 50%).

### 4. Expose MinTier in Tweak Settings dialog

Add textbox row between "Cooldown rows" and "Snapshot history" section:

```
Min tier-eligible rows (statistical floor):  [____15____]
```

Tooltip: *"Minimum STRONG/MEDIUM directional rows that must exist within a window for the round to evaluate. Rounds with fewer are skipped (don't tick the streak). Default scales with Window Size as max(15, ceil(WindowSize × 0.5))."*

**Save-time validation:**

| Check | Action |
|---|---|
| MinTier < 5 | Reject with MessageBox: "Min ≥ 5 required for any statistical meaning." |
| MinTier > WindowSize | Reject with MessageBox: "Cannot exceed Window Size — gate would be unreachable." |
| MinTier > WindowSize × 0.7 | Warning MessageBox (proceed anyway): "MinTier exceeds 70% of WindowSize. Many rounds may be skipped if NO_TRADE density is high. Proceed?" |

### 5. Window mode setting

New top-level key in `tweaker_config.json`:

```json
"window_mode": "fixed"     // "fixed" | "sliding"
```

Default `fixed`. `sliding` retained for legacy comparison and ad-hoc experimentation. Documented as deprecated; planned for removal once fixed mode is validated.

### 6. RoundHistory schema augmentation

Add row-span tracking for audit (asked-for in last conversation):

```json
{
  "round_id": 42,
  "started_iso": "2026-05-15T14:30:00Z",
  "window_start_row": 91,
  "window_end_row": 120,
  "outcome": "BELOW_THRESHOLD",  // BELOW_THRESHOLD | APPLIED | PROPOSED | DRY_RUN_WRITTEN | SKIPPED_INSUFFICIENT_TIER | SKIPPED_SESSION_BOUNDARY
  "tier_eligible_count": 18,
  "failure_rate_pct": 47.5,
  ...
}
```

Round Stats viewer reads `window_start_row` / `window_end_row` for "examine this round's rows" deep-link (future enhancement).

---

## Settings.json schema (v29)

```jsonc
"auto_tweaker": {
  "version": 29,
  "window_mode": "fixed",            // NEW: "fixed" | "sliding", default "fixed"
  "window_size_verdicts": 30,        // unchanged
  "min_tier_eligible_rows": null,    // null = auto-compute as max(15, ceil(size × 0.5))
  "cooldown_rows": 10,               // unchanged; no-op when mode=fixed
  "failure_rate_threshold_pct": 40,  // unchanged
  "snapshot_streak_x": 3,            // unchanged
  "max_keys_per_proposal": 3,        // unchanged
  "streak_weight": 1.5,              // unchanged
  "streak_length_clamp": 20,         // unchanged
  "auto_commit_enabled": false,
  "dry_run_enabled": true
}
```

`change_log` entry:
> v29 (2026-05-15): auto_tweaker switched from sliding to fixed (non-overlapping) windows. New `window_mode` key (default "fixed"). `min_tier_eligible_rows` now auto-scales with WindowSize when null. `cooldown_rows` deprecated in fixed mode. RoundHistory entries gain `window_start_row`/`window_end_row`. New SKIPPED_INSUFFICIENT_TIER outcome.

---

## Implementation steps

1. **TweakerConfig.vb:** Add `WindowMode` (enum String, default `"fixed"`). Add `MinTierEligibleRows` as `Integer?` (Nullable, null triggers auto-compute). Override `MinTierEligibleRows` getter to apply default formula when null.
2. **TweakerState.vb:** Add `LastEvaluatedRowIndex` (Integer, default 0). Update `RoundHistory` entry shape with `WindowStartRow`/`WindowEndRow` + new outcome value.
3. **AutoTweakerCore.RunAsync:** Replace Step 2/3 window construction with fixed-mode logic when `WindowMode == "fixed"`. Step 4 (MinTier check) becomes "skip round, advance index, write SKIPPED_INSUFFICIENT_TIER to RoundHistory, do not tick streak".
4. **TweakSettingsForm.vb:** Add MinTier textbox + tooltip. Add Save-time validations (3 checks above).
5. **TweakSettingsForm.UpdateStatusLabel:** When fixed mode, show `"Next round: K/N rows accumulating (rows X..Y)"` instead of the sliding "Waiting for session-aligned window".
6. **SnapshotManager.vb:** Streak tick now only fires on `BELOW_THRESHOLD`, never on `SKIPPED_*`.
7. **Docs:** Update `auto-tweaker-pipeline-proposal.md` §3 with new pipeline. Update `UserManual.md` §20 fixed-mode workflow. Add `docs/DeribitIndicatorProject.md` §15 entry.
8. **settings.json:** Bump version, append change_log entry.

---

## Test plan

| Test | Expected |
|---|---|
| Existing state.json missing `LastEvaluatedRowIndex` | Defaults to 0 silently, no migration needed |
| WindowSize=30, MinTier=null, save config | Computed MinTier shows as `max(15, 15) = 15` in dialog |
| WindowSize=30, MinTier=50, save | Rejected with MessageBox (>WindowSize) |
| WindowSize=30, MinTier=22, save | Warning MessageBox (>WindowSize × 0.7), proceeds on OK |
| Run tweaker with mode=fixed, 30 rows accumulated | Round 1 evaluates rows 1..30. LastEvaluatedRowIndex → 30 |
| Continue running, accumulate 30 more rows | Round 2 evaluates rows 31..60. Disjoint from Round 1 |
| Window contains only 10 directionals (MinTier=15) | SKIPPED_INSUFFICIENT_TIER, LastEvaluatedRowIndex → +30, streak unchanged |
| Cross-session window | Existing SKIPPED_SESSION_BOUNDARY logic preserved |
| Build | Clean (`dotnet build`) |

---

## Future-proofing notes

**WEAK tier exclusion (user-flagged future change):** When/if WEAK_LONG and WEAK_SHORT are removed from the directional pool for failure-rate evaluation, the MinTier denominator pool shrinks. With current ~50% directional density (LONG + SHORT only would be ~25%), the user should expect more rounds to skip. The `max(15, ceil(WindowSize × 0.5))` formula remains correct since it's WindowSize-based, but document this trade-off in the Tweak Settings tooltip when the WEAK exclusion lands.

**Sliding mode deprecation timeline:** Recommended 4-week observation after fixed mode ships before removing the sliding branch. If no regressions surface, the sliding branch + `cooldown_rows` field can be dropped in v28 or later cleanup pass.

---

## Open questions

1. RoundHistory retention — currently unbounded. Should we cap at last 1000 rounds to keep state.json manageable? (Recommend: yes, ring-buffer trim at 1000.)
2. Backward-compat `cooldown_rows` warning message — log once per process start, or every round? (Recommend: once per process start.)
