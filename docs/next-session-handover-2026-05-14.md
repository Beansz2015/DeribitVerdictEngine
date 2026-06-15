# Handover: Next Conversation — DeribitVerdictEngine
**Generated:** 2026-05-14 (UTC+8)
**Source conversation context:** ~71% — handing off to a fresh conversation to preserve budget.
**Active model recommendation for continuation:** Opus 4.7 medium for design/discussion, Sonnet 4.6 medium for any implementation spec follow-throughs.

---

## 1. One-paragraph state of the project

The engine is at `settings.json` v26. All in-flight specs are shipped: output-dump capture, settings-snapshot history with revert capability, configurable diff cap, failure-definition v2 (barrier-hit with adverse stop), and the live-performance display strip (P7). The trader is in **active calibration** — all logs were reset on 2026-05-13 afternoon UTC+8 to eliminate legacy-data noise; the engine has been accumulating fresh data since. The most recent fix (commit `4caa0bc`) corrects a UTC timestamp parsing bug in `LivePerformanceTracker` that was zeroing the perf display on restart. **Build state:** clean; nothing in flight requiring implementation. **Implementation queue:** empty. **Master tip:** `4caa0bc`, several commits ahead of `origin/master` (user pushes after testing).

---

## 2. Session start protocol for the new conversation

Read in order:

1. `CLAUDE.md` (auto-loaded)
2. **This file** (handover)
3. `docs/DeribitIndicatorProject.md` — top header confirms v26 + live-performance-display layered on prior bundles. Skim §15 version history (recent entries 2026-05-11 → 2026-05-13), full read §16.5 (active spec bundle status), §16.6 (parked observations P1–P8, P3/P7 RESOLVED), §16.7 (Linux portability constraint).
4. `docs/architecture.md` — data flow + design decisions table.
5. **Load skill `crypto-trading-context`** — provides writing-style + trader-profile. **Do not** also separately read `docs/trader-profile.md`; the skill loads it.

**Critical reminder:** The `load_skill()` prohibition at the top of `DeribitIndicatorProject.md` is OBSOLETE for this skill — `crypto-trading-context` is project-specific and DOES belong here. The prohibition was historical and should be revised when next this doc is edited.

---

## 3. Recent commits (chronological, most recent first)

```
4caa0bc  fix(LivePerformanceTracker): UTC timestamp parsing on cache reload
a6201f7  docs: live performance display spec — P7 activation
625ad4a  feat: live performance display strip (P7) — settings.json v25→v26
f5554eb  fix(display): S1 VWAP anchor label + S2 trend-structure note parity
dec63bf  fix(display): B1+B2 output-dump bug audit fixes
78ed1bb  feat(auto-tweaker): settings snapshot history + round stats + configurable diff cap
5b6801c  docs: settings-snapshot history + round stats + configurable diff cap spec
c6ac379  fix(output-dump): move status-bar dump links left of countdown label
6783afd  feat(output-dump): full analysis text capture to analysis_output_dump.md
77c2911  docs: output-dump spec — full analysis text capture to single .md file
095f258  feat(CalibrationReport): drop liquidation events from READY gate
```

All of these are local-only on master, **not pushed to remote**. The trader-profile §8 workflow requires test-then-push by the user, not by the AI. Don't push without explicit instruction.

---

## 4. Active state of the Live Performance Display (P7)

### What it does

A six-label horizontal strip below `lblVerdict`, to the right of the auto-run Start/Stop button. Displays success-rate percentages for six time windows, updated on every analysis run:

```
Cur.Wk: 55%  |  3d: 60%  |  Cur.Day: 57%  |  Asia: 64%  |  London: 49%  |  NY: 57%
```

- `Cur.Wk` — Monday 00:00 UTC+8 → now
- `3d` — last 3 chronological days
- `Cur.Day` — today 00:00 UTC+8 → now
- `Asia` / `London` / `NY` — "most-recent-block" session windows (8h each). NY straddles midnight UTC+8.

Green when rate > 50%, red when ≤ 50%, dim grey `--%` when sample size below threshold (default 4).

### Success metric

Reuses `FailureRateMatrix.WalkBars` (v2 failure-definition):

- **Favourable barrier** = displayed ATR target (`AdjustedLongTarget` / `AdjustedShortTarget` if 3-tier cap fired, else raw `entry + 2×ATR`)
- **Adverse barrier** = `SwingStopLong/Short` if logged, else `entry ± 1.2×ATR`
- **Eligible bars** = closes at T+3 through T+15 (skip T+1, T+2 per execution-latency rule)
- **Outcomes**: SUCCESS / ADVERSE_HIT / AMBIGUOUS / WINDOW_EXPIRED. Last three count as failure for the rate denominator.

ALL directional verdicts count (STRONG/MEDIUM/WEAK LONG/SHORT). NO_TRADE excluded. **Parked observation P8** tracks whether WEAK dilutes the rate misleadingly — revisit after a week of data; if so, restrict to STRONG+MEDIUM.

### Caches (both gitignored)

- `bin/Debug/net8.0-windows/analysis_eval_cache.csv` — one row per analysis run with the resolved barrier-hit outcome. Schema: `Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome`. Header comment `# schema=v1 (live-performance-display)`.
- `bin/Debug/net8.0-windows/ohlc_1m_cache.csv` — rolling 7-day 1m OHLC bars. Schema: `CloseTime,Open,High,Low,Close,Volume`. Header comment `# schema=v1 (1m ohlc cache)`. Rolling-trim cap: 10,080 bars.

### Cold-start backfill

Eager: at engine startup, fetch 7d of 1m OHLC in one Deribit call, evaluate all eligible analysis_log.csv rows, populate caches. ~3 seconds. Status label briefly shows `"Loading performance history..."`.

Toggle: `settings.json` `performance_display.eager_backfill_on_startup` (default `true`).

### Critical recent fix — `4caa0bc` (2026-05-13)

**Bug:** After app restart, all six labels showed `0%` or `--%` even though the cache had 26 SUCCESS rows out of ~256 evaluable (~10% rate).

**Root cause:** `LivePerformanceTracker.ParseEvalLine` (and `ParseAnalysisLog`) used `DateTime.Parse(...)` without `DateTimeStyles.AdjustToUniversal`. For Z-suffixed ISO timestamps like `2026-05-13T17:08:07.0000000Z`:

1. `DateTime.Parse` converted the UTC instant to local time (UTC+8) with `Kind=Local`
2. `DateTime.SpecifyKind(Utc)` re-labelled Kind to Utc WITHOUT changing the value
3. Result: every cached timestamp was 8 hours ahead of its true UTC instant
4. `ComputeWindows` filtered by Timestamp in UTC; mislabeled rows fell into "future" timestamps outside all current windows
5. Display showed only freshly-created in-session entries (correct UTC), which during market-open volatility were mostly FAILUREs → 0%

**Fix:** Use `DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal`. AdjustToUniversal honours the Z suffix, AssumeUniversal handles the unsuffixed `analysis_log.csv` format defensively. SpecifyKind dropped (now redundant).

### Current state of perf display

- Build clean (0/0).
- User has stopped and rebuilt the app to activate `4caa0bc`. **Verification pending** on whether rates now restore correctly across a restart.
- No further changes queued for the perf display.

### Known sensitivities to monitor

1. **WEAK tier dilution (P8)** — if including WEAK_* verdicts pushes the headline rate visibly below what the trader expects, may need to restrict to STRONG+MEDIUM.
2. **NY session display partial pre-21:00 UTC+8** — when checking the strip before NY market open, the NY label shows yesterday's completed NY block (full 8h, fully evaluated). After 21:00 UTC+8, it switches to today's growing partial block. Confirmed working correctly in the spec's `ComputeSessionWindow` straddle branch.
3. **Eager backfill startup latency** — typical 1–3 seconds. If the user reports it takes substantially longer, check `OhlcCache.Load` for large file handling and consider chunked-fetch fallback.
4. **Cache file growth** — the eval cache appends one row per analysis run; no rolling-trim. At 60s cadence, ~1440 rows/day. After a few weeks, file becomes large but should still parse quickly (~10K rows is sub-100ms read). If it ever becomes a concern, add rolling-trim mirroring the OHLC cache pattern.

---

## 5. Active state of the Auto-Tweaker

### Architecture summary

Separate .NET 8 console app at `tools/AutoTweaker/AutoTweaker.dll`. Zero `System.Windows.Forms` references — Linux-portable by design. Configured via `tools/AutoTweaker/tweaker_config.json` and the WinForms `TweakSettingsForm` dialog. Persistent state in `tools/AutoTweaker/state.json`.

Key files (all host-agnostic):
- `AutoTweakerProgram.vb` — entry, arg parse, walks up to find DeribitVerdictEngine.sln, sets working dir, calls SettingsLoader.Initialise
- `AutoTweakerCore.vb` — pipeline (eligibility → window → failure matrix → trigger → prompt → API → diff parse → apply/propose)
- `PromptBuilder.vb` — builds System+User message including settings.json, recent CSV slice, failure matrix, snapshot manifest, conditions vector
- `ClaudeApiClient.vb` — latest-Opus model discovery via `/v1/models`, dry-run file writer
- `SettingsDiffApplier.vb` — Validate (3-arg signature taking maxKeysPerProposal) + Apply + ApplyRevert (snapshot wholesale replacement, key-cap bypassed but rejected-pattern validation still runs)
- `TweakerConfig.vb` / `TweakerState.vb` — POCO config + state, with snapshot-history fields (`CurrentBelowThresholdStreak`, `ActiveSnapshotFilename`, `LastSuccessfulRoundIso`, `RoundHistory`)
- `CompositeScorer.vb` — composite score formula: `(100 - AvgFailureRatePct) + min(StreakLength, StreakLengthClamp) × StreakWeight`. Default StreakWeight=1.5, StreakLengthClamp=20. Formula reproduced **verbatim** in PromptBuilder system message and UserManual §20e.
- `ConditionsExtractor.vb` — turns a CSV slice into a 10-field conditions vector (regime mix, ATR scale, funding, VWAP dev, spread regime, OFI imbalance, etc.)
- `SnapshotManager.vb` — Create (at streak hits X) / AccumulateConditions (while ACTIVE) / Finalise (on interruption, with FinalisedIso = LAST SUCCESSFUL round's timestamp, NOT the interruption round's)
- `RoundStatsBuilder.vb` — async per-tier × confidence-level accuracy display ("STRONG_LONG / Confidence: HIGH: 12 correct / 3 wrong (80% success)")

### Configurable knobs (`tweaker_config.json`)

- `window_size_verdicts` (default 120) — rows in the failure-rate window
- `failure_rate_threshold_pct` (default 40) — when aggregate failure rate exceeds this, propose a tweak
- `cooldown_rows` (default 10) — minimum new rows between auto-tweaker runs
- `min_tier_eligible_rows` (default 60) — minimum STRONG/MEDIUM rows in the window for trustworthy evaluation
- `max_keys_per_proposal` (default 3) — diff scope cap; previously hardcoded, now tunable. Reverts exempt by design.
- `snapshot_streak_x` (default 3) — consecutive BELOW_THRESHOLD rounds before snapshotting
- `streak_weight` (default 1.5), `streak_length_clamp` (default 20) — composite score knobs
- `auto_commit_enabled` (default false) — when true, applied tweaks/reverts write to settings.json directly; when false, diffs are parked at `proposed_diffs/<timestamp>.json` for manual apply via `--apply-manual`
- `dry_run_enabled` (default true) — when true, prompt + payload are written to `dry_run_payloads/<timestamp>.txt` instead of calling the API

### Snapshot history

- Settings snapshots saved at `settings_snapshots/` (gitignored) when streak hits X
- Manifest at `settings_snapshots/manifest.csv` — wide schema covering 10-field conditions vector + composite score + ACTIVE/ROTATED status
- Bucket key: `{Regime}_{VolatilityTier}` (12 buckets max)
- Rotation rule: best composite score per bucket wins; loser's .json file deleted, manifest row retained as historical record
- Revert mechanism: auto-tweaker can propose `REVERT to snapshot <filename>` when current conditions match a past snapshot's bucket. Same auto-commit / dry-run toggles as regular tweaks.

### Audit finding 2026-05-12 — "Bug 2" was NOT a bug

User reported: "Waiting for session-aligned window: 14/30 rows" — display counter incrementing regardless of verdict, and decrementing unexpectedly.

**Investigation result:** by-design behaviour per `TweakSettingsForm.CountCurrentSessionRows` lines 240–267:

- The counter is intentionally **verdict-agnostic** — it counts rows in the current session (walks backward from most-recent CSV row, stops at first session boundary).
- The "decrement" the user observed is the legitimate session-boundary reset (e.g., London 20:00 → NY 21:00 = boundary crossed → count resets to 1).
- Tier-eligibility (STRONG/MEDIUM filter) only applies at the NEXT status stage, after this same-session check is satisfied.

**Optional polish offered, not applied yet:** rename the display message from `"Waiting for session-aligned window: 14/30 rows"` to `"Same-session rows: 14/30 (verdict-agnostic — tier filter applies after)"` for clarity. User did not accept or reject; defer until user feedback.

### Operational state

- Auto-tweaker has been running passively since ~2026-05-09 with `dry_run_enabled = true`, `auto_commit_enabled = false`.
- User has not yet exercised the auto-commit path (still in observation mode).
- No tweaker round has yet hit the trigger threshold to my knowledge — most rounds returning `BELOW_THRESHOLD` per the snapshot-history accumulation evidence.
- No snapshots created yet (would need 3 consecutive `BELOW_THRESHOLD` rounds; user reset logs 2026-05-13 so accumulation just started).

### Known sensitivities to monitor

1. **First auto-tweaker proposal** — when failure rate first crosses threshold, the user will see a dry-run payload in `tools/AutoTweaker/dry_run_payloads/`. Open in a separate Claude conversation, paste the SYSTEM + USER messages, get back a diff, save to `manual_diffs/`, run `AutoTweaker.exe --apply-manual <path>`. End-to-end flow has not yet been exercised under real conditions.
2. **First snapshot creation** — at 3 consecutive BELOW_THRESHOLD rounds, a snapshot file appears in `settings_snapshots/`. Verify the manifest CSV row populates correctly with ACTIVE status, the FinalisedIso field empty until the streak ends.
3. **First snapshot finalisation** — when the streak ends, FinalisedIso should be set to the **LAST SUCCESSFUL round's timestamp**, not the interruption round's. This is the critical invariant we hammered on at spec time.
4. **First revert proposal** — when the auto-tweaker proposes `REVERT`, the response JSON has `action: "revert"` and `revert_target: <filename>`. SettingsDiffApplier.ApplyRevert validates the snapshot's content against the rejected-pattern list before applying. Bypass the 3-key cap for reverts but not the rejected-pattern check.

---

## 6. Calibration state

### Calibration Report status

User reports: "Calibration Report now has the status 'Ready for Calibration'."

The liquidation events gate was removed on 2026-05-09 (commit `095f258`). Current READY criteria:

- ≥300 total rows logged
- ≥3 sessions (UTC+8 days)
- ≥3 regimes covered with ≥50 rows each

Liquidation events are still tracked but informational only.

### What's left to calibrate

The implementation queue is empty; the user is in pure data-accumulation mode. Specific calibration tasks (most are passive — let the data accumulate, then audit):

| # | Task | Trigger condition | Action |
|---|---|---|---|
| 1 | Validate v2 failure-rate trigger threshold | ~200 evaluable rounds in RoundHistory | If most land 50–70%, raise default from 40% → 55%. If 20–30%, lower toward 30%. Tune via Tweak Settings dialog. |
| 2 | Validate snapshot system end-to-end | ~50–100 rounds | Expect ≥1–2 snapshots created. Rotation triggers at least once if conditions reverted. First REVERT proposal should fire when conditions revert to a past snapshot. |
| 3 | Validate Round Stats accuracy | Open Round Stats periodically | Sanity-check per-tier success rates against trader's gut sense. Flag if dramatically off. |
| 4 | Funding Momentum diagnostic | After ~500 rows | Run Analysis Report. Section 5 (Funding Momentum) tells whether 1bp threshold is appropriate or polling-cadence-bound. If sub-minute spikes are systematically missed, accept as WebSocket ceiling. |
| 5 | OFI outlier audit | Periodic | Section 6 of Analysis Report. Should be rare after 2026-05-09 cap fix. |
| 6 | TRENDING_UP regime coverage | Wait for 50+ rows in TRENDING_UP | No action needed; just wait. |
| 7 | Live perf display rates plausibility | Continuous (visual) | After restart, rates should populate from loaded cache. Watch for WEAK tier dilution (P8). |

### Recent reset (2026-05-13)

User reset all logs (analysis_log.csv, output_dump.md, picked_cell_history.csv) on 2026-05-13 afternoon UTC+8 to eliminate legacy-data noise. Started accumulating fresh data from there. The eval cache and OHLC cache were also cleared.

---

## 7. Parked observations (full P-list)

From `DeribitIndicatorProject.md §16.6`. Status as of 2026-05-14:

| ID | Topic | Status | Trigger to revisit |
|---|---|---|---|
| **P1** | Promote BestPivotByVolume to v2 cap arbitration | Watching | "Best is also most-recent" rate < 50% (currently 16.6% — partially met) AND auto-tweaker output shows volume-weighted pivots correlate with target hit rate |
| **P2** | Funding momentum threshold v23+ tuning | Watching | Offline analysis shows current 1bp threshold is genuinely above all observed deltas at REST cadence — would accept as polling-cadence ceiling |
| **P3** | OI×CVD asymmetry | **RESOLVED 2026-05-08** | Fixed via `priceUp` comparison against 15m-ago close (commit `e2ecb95`). Watch CONFIRMED ratio normalise toward regime mix in subsequent reports. |
| **P4** | STRONG/MEDIUM tier collapse in failure-rate matrix | Watching | 1000+ tier-eligible rows, both matrices pick same (window, threshold) |
| **P5** | Liquidation count window | Watching | 1000+ rows accumulate with still 0 liq events → spec re-introducing `cfg.Indicators.Liquidations.TradeCount` removed in v15 |
| **P6** | `STRUCTURAL_RR_LOW` context tag | Watching | When auto-tweaker output validates that verdict-direction R:R < 1:1 correlates with elevated failure rate |
| **P7** | Live per-analysis success/fail display | **RESOLVED 2026-05-13** | Shipped as `live-performance-display-proposal.md` (`625ad4a` + `4caa0bc`). |
| **P8** | Live perf display WEAK tier filtering | Watching | After ~1 week of live data, if WEAK_* inclusion misleads vs STRONG+MEDIUM only |

---

## 8. Outstanding spec items / future work

### Ready when desired (no data dependency)

- **D3** — 5m RSI divergence (symmetric extension of existing 1m logic). Not gated.
- **D4** — Donchian × BBW state cross-reference. Spec needed first, risk of double-counting.
- **C2** — Anchored VWAP from session high/low. Needs multi-session state plumbing.
- **C1** — Multi-session VPFR (naked POCs). Same state-plumbing prerequisite as C2.

### Gated on calibration data accumulation

- **B1 re-spec** — per-indicator regime weights. Currently STUB; needs hit-rate output to redraft.
- Per-item B4 threshold tuning sweeps (most TFI, OFI, TTM thresholds still pending).

### Architectural / long-arc

- WebSocket migration (`§16.4`) — gated on indicator backlog exhaustion + Section A item demand.
- Section A items — Spread momentum, Aggressor velocity, Order book absorption, Liq × OFI flip detector, VPFR profile shape. All gated by WebSocket.
- Linux CLI port (`§16.2`) — gated on auto-tweaker shipping (✓ done) + analysis accuracy plateau.

### Low-value / explicitly deferred

- D5 (Smart OBV), D6 (MFI replacement) — only revisit if specific divergence false-positives observed.
- HH/HL/LH/LL pattern extension (D1 v2) — defer unless data shows 2-pivot version misses transitions.

---

## 9. Workflow conventions (critical for the new conversation)

1. **Commit local, do not push.** All commits stay on local master. The user pushes after testing. Per trader-profile §8.
2. **Docs-only commits can be pushed by AI** historically, but recent pattern is leave them local too and batch with code commits. Default: local-only.
3. **Crypto-trading-context skill** is the canonical source for trader-profile + writing-style. Load it; don't read separate trader-profile.md.
4. **Host-agnostic constraint** for new files in `tools/AutoTweaker/` and (now) the `LivePerformanceTracker.vb` / `OhlcCache.vb` shared helpers: zero `System.Windows.Forms` references. Form-side viewers like `AnalysisReportForm`, `OutputDumpSettingsForm`, `RoundStatsForm` are thin wrappers and may use WinForms.
5. **Spec-first for substantial features.** Drafts go to `/docs` as `<name>-proposal.md` with status PROPOSED. Implementation flips to ✅ IMPLEMENTED after commit.
6. **Settings.json version is strictly monotonic.** Bump on every settings change. `change_log` array — append only.
7. **No `--no-verify` on commits, no force-push.** Standard hygiene.
8. **Build verification:** `dotnet build` is the gate. If the app is running it locks the .exe and the file-copy step fails but VBC compilation succeeds — recognise this and tell the user to stop the app to rebuild cleanly.

---

## 10. Recent debugging case studies (useful patterns for future audits)

### Pattern 1 — UTC vs Local timezone bugs in serialisation

The `4caa0bc` fix above is the second instance of this pattern in the project. Both times the symptom was "data looks lost after restart" and the root cause was `DateTime.Parse` defaulting to local-time conversion.

**Defensive practice:** any time the code parses an ISO 8601 timestamp string with Z suffix or +offset, use `DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal`. Do NOT rely on `SpecifyKind` to "fix" the kind after parsing — it only changes the label, not the value.

### Pattern 2 — Display-side bugs surfaced from output dump

Multiple bugs caught via the output dump file (the `.md` capture of full rendered analysis text) that were INVISIBLE in the columnar CSV:
- VAH = LVN-below coincidence (turned out to be geometry, not bug)
- Structural R:R asymmetry on LONG verdicts (P6)
- TargetCapReason cross-row contamination (B1)
- CONTEXT line silently dropped on CONFIRMED (B2)

**Implication:** when a user reports something off, ask for both CSV columns AND a recent rendered output paste. The latter often reveals patterns the former hides.

### Pattern 3 — "Bug 2" was not a bug

User reported the "Waiting for session-aligned window: 14/30 rows" counter as a bug because it "incremented regardless of verdict" and "decremented unexpectedly." Investigation showed both behaviours were spec-correct: verdict-agnostic by design (counts same-session rows), decrement is session-boundary reset.

**Defensive practice:** before fixing, read the spec to confirm the user's expectation matches the design intent. If the bug report describes correct behaviour but with confusion, the fix may be a wording/clarity change to the UI, not a code change.

---

## 11. Pending verification items

Things the user will test in the immediate future; if findings come back, the new conversation should be ready to handle them:

1. **Live perf display rates after restart** — with the `4caa0bc` fix, rates from the loaded cache should appear correctly. If still 0%, there's a second bug we haven't found.
2. **First auto-tweaker trigger** — under v2 strict semantics, the failure rate trigger may fire more often. Watch for cooldown/cadence issues.
3. **First snapshot creation** — verify manifest schema is correct, snapshot file appears in `settings_snapshots/`, ACTIVE status set.
4. **First REVERT proposal** — verify it bypasses the 3-key cap but still validates against rejected-pattern list.
5. **Session boundary handling** — when crossing 08:00, 16:00, 21:00 UTC+8 (Asia/London/NY starts) and the matching ends, verify both the live perf display and the auto-tweaker status update correctly.

---

## 12. Quick-reference file paths

```
Engine root:                 C:/Dev/DeribitVerdictEngine/
Active worktree (this one):  C:/Dev/DeribitVerdictEngine/.claude/worktrees/nostalgic-bhabha-f68bd6/

Settings:                    settings.json (v26)
Project handover:            docs/DeribitIndicatorProject.md
Architecture:                docs/architecture.md
Recent spec (last shipped):  docs/live-performance-display-proposal.md
Recent spec (last before):   docs/settings-snapshot-history-proposal.md
Recent spec (last before):   docs/failure-definition-v2-proposal.md
Audit report (2026-05-12):   docs/output-dump-bug-audit-2026-05-12.md (in vigilant-elbakyan-fdbadc worktree — copy to root if useful)

CSV / cache locations:       bin/Debug/net8.0-windows/
                             ├── analysis_log.csv         (the engine's per-run log, v0.4.1 schema, 87 cols)
                             ├── analysis_output_dump.md  (full rendered output per run, rolling 3000)
                             ├── analysis_eval_cache.csv  (perf display eval results)
                             ├── ohlc_1m_cache.csv        (rolling 7-day OHLC)
                             └── analysis_log.csv.v0.4.bak (rotated old logs)

AutoTweaker working dir:     tools/AutoTweaker/
                             ├── tweaker_config.json
                             ├── state.json
                             ├── dry_run_payloads/
                             ├── proposed_diffs/
                             └── manual_diffs/

Snapshot history:            settings_snapshots/
                             ├── manifest.csv
                             └── settings_snapshot_<ts>.json (one per ACTIVE bucket)
```

---

## 13. When in doubt

- **The trader-profile §4 rejected-pattern list is absolute.** No fixed-% targets, no non-directional rewards, no double-counting, no flat regime penalties.
- **Conservative bias wins ties.** If a fix could be lenient or strict, go strict. The user prefers false-negative (missed trade) over false-positive (bad trade).
- **Don't push proactively.** Test gate is user-side.
- **Don't propose new indicator work pre-calibration.** The bottleneck is data, not code.
- **Ask before doing.** Especially for anything touching scoring logic or settings.json structure. The user prefers explicit confirmation over speed.

---

**End of handover.** This document is comprehensive enough to start a fresh conversation cold. Paste it as the first user message (or save it under `docs/` and link to it in the kickoff prompt). The new conversation should have full operational context after reading it + the standard session-start docs.
