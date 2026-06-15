# Handover: Next Conversation — DeribitVerdictEngine
**Generated:** 2026-05-18 (UTC+8)
**Context handover trigger:** previous session at 67%, planned hand-off to fresh Opus High conversation.
**Active model recommendation for continuation:** Opus High for design, debugging, synthesis-heavy reviews. Opus Medium for spec implementation (the kickoff-in-fresh-conversation pattern).

---

## 1. One-paragraph state of the project

The engine is at **settings.json v30** with five post-v30 internal-cleanup / display-polish commits layered on top. The 2026-05-17/18 session shipped:

1. **v27** OHLC cache gap-backfill (with 3 post-impl fix commits for UTC parse, file order, NewestBarTime).
2. **v28** Target-hit metric toggle on perf strip + eval cache schema v1→v2.
3. **v29** Auto-tweaker fixed-window mode + MinTier statistical-floor rework.
4. **v30** Display polish pass — 8 fixes including output dump perf-strip capture, R:R `< 0.1` rendering, sub-tick CAPPED suppression, NO TRADE → ALIGNED context tag.
5. **Audit cleanup pass** (atomic state writes for TweakerState.Save + OhlcCache; ALIGNED enum coverage in CalibrationReport + AnalysisRunner).
6. **Chunked Deribit OHLC fetch** in `analysis/DeribitOhlcFetcher.vb` — closed STRONG_SHORT cell-eligibility starvation that hid 47 short verdicts from the failure-rate matrix.
7. **Dual recommendation** (★ most-precise + ◆ lowest-failure) per tier in Analysis Report.
8. **REGIME ANCHOR caution** display on STRONG verdicts fighting the 5m EMA(200) anchor by > 3.0× ATR.

**Doc trim:** `DeribitIndicatorProject.md` went 683 lines / ~40K tokens → 489 lines / ~10K tokens. Historical content moved to `docs/history-archive.md`. The main doc now reads in full under the 25K Read cap. `CLAUDE.md` updated to drop the partial-read workaround.

**Build state:** clean (0/0 both projects). Nothing in flight. Implementation queue empty.

**Master tip:** `1eff4f3`. **12 commits** ahead of `origin/master`, all local-only pending user testing.

**Today is 2026-05-18 (UTC+8). BTC is in a macro bear market since Oct 2025** — important context for any STRONG_LONG vs STRONG_SHORT performance discussion.

---

## 2. Session start protocol for the new conversation

Read in order:

1. `CLAUDE.md` (auto-loaded). Updated 2026-05-17 to drop the partial-read workaround and the legacy `load_skill()` prohibition. Shell tips section is current.
2. **This file** (handover).
3. `docs/DeribitIndicatorProject.md` — reads in full now (~10K tokens). §15 has the recent-changes table; full historical version log is at `docs/history-archive.md` §E if needed.
4. `docs/architecture.md` — including the "Display Behaviour Clarifications" section appended 2026-05-17 covering HOLD/EXIT position gate, POC tier 3 HVN gating, STRONG + warning tags by design, and MTF Reason three-format design.
5. **Load skill `crypto-trading-context`** — trader profile + writing style. **Do not** also separately read `docs/trader-profile.md`; the skill loads it.

---

## 3. Recent commits this session (chronological, most recent first)

```
1eff4f3  feat(display): REGIME ANCHOR caution on STRONG verdicts fighting intermediate trend
d09ea5b  feat(analysis): dual recommendation per tier — most-precise + lowest-failure
ebfad56  fix(analysis): chunked Deribit OHLC fetch — closes STRONG_SHORT starvation
d590e92  fix: audit cleanup pass — atomic state writes, ALIGNED enum coverage
7ab2df5  docs: trim DeribitIndicatorProject.md (40K → 10K tokens) + new history-archive.md
9cdaab7  feat(display): polish pass — output dump perf-strip, R:R rendering, CAPPED suppression, NO TRADE→ALIGNED — settings.json v29→v30
eb3c537  docs(tweak-settings): note MinTier ≤ WindowSize in tooltip
31d8e91  feat(auto-tweaker): fixed-window mode + MinTier rework — settings.json v28→v29
1015709  feat(perf-display): target-hit metric toggle — eval cache v1→v2, settings.json v27→v28
7a9a6f8  fix(OhlcCache): NewestBarTime must scan all rows, not trust file order
c799187  fix(LivePerformanceTracker): canonicalise ohlc cache file order after gap-fill
5fa6916  fix(OhlcCache): UTC timestamp parse on cache reload
2f417de  feat(perf-display): OHLC cache gap-backfill on startup — settings.json v26→v27
```

**All 12 commits are local-only, unpushed.** The trader-profile §8 workflow requires test-then-push by the user, not by the AI. **Do not push without explicit instruction.**

The user has compiled and run the app against the latest commit and confirmed the rendered output looks good. So the test gate is partially passed — the remaining is whatever the user wants to verify under live conditions (auto-tweaker first-fire, target-hit metric distribution, regime anchor warning appearing).

---

## 4. State of the post-v30 features

### Live performance strip (v26-v28)

- Six rate labels + `[B]/[T]` mode indicator on the perf strip.
- Left-click any label toggles metric mode in-memory (ephemeral, no settings write).
- Right-click opens context menu that persists via `SettingsLoader.Save`.
- Eval cache v2 schema has `TargetEverHit` column; migration is one-shot on first v28+ load.
- Tooltip second line carries the OTHER metric so both visible without toggling.

**Critical fix in v27:** OHLC cache gap-backfill on `InitialiseAsync` (Step 1.5). Detects interior gaps within the 7-day window and fetches missing minutes. Throttled by `max_gap_fill_calls` (10) and chunked by `max_gap_fill_minutes` (5000).

**3 post-v27 fix commits** addressed cascading bugs: UTC parse in OhlcCache, file-order canonicalisation after gap-fill, NewestBarTime scanning all rows instead of trusting file order. All shipped. **Don't re-flag these** — they're stable.

### Auto-tweaker fixed-window mode (v29)

Switched from sliding to fixed (non-overlapping) windows. Key state:
- `TweakerConfig.WindowMode` defaults to `"fixed"`. `"sliding"` retained as deprecated.
- `TweakerConfig.MinTierEligibleRows` is nullable — null auto-scales as `max(15, ceil(WindowSize × 0.5))`.
- `TweakerState.LastEvaluatedRowIndex` (default -1, seeded to `currentRowCount` on first v29 run so historical sliding-era data stays in CSV but isn't re-evaluated).
- `RoundHistoryCap` raised 50 → 1000.
- New `SKIPPED_INSUFFICIENT_TIER` and `SKIPPED_SESSION_BOUNDARY` outcomes (advance row index but do NOT tick BELOW_THRESHOLD streak).

**Status:** Auto-tweaker has NOT yet fired in production under fixed-window semantics. The first fire will be a single-shot event — watch carefully when it does.

### Display polish pass (v30)

8 fixes shipped in one commit. Most relevant for ongoing observation:
- **NO TRADE rows now write VerdictContext="ALIGNED"** (was CONFIRMED). New value in the CSV column.
- **CAPPED label suppressed** when `|raw - adjusted| < max(0.5, ATR × 0.02)` — sub-tick adjustments no longer flash amber bold.
- Output dump now captures the perf strip values as `PERF STRIP [B/T] ...` line after each `## Run` header.

### Audit cleanup pass (post-v30, no settings.json bump)

- `TweakerState.Save` and `OhlcCache.WriteAll` / `RollingTrim` use atomic write pattern (tmp + `File.Replace`). Mid-write crashes no longer wipe accumulated state.
- `BuildCalibrationReport.contextCounts` gained "ALIGNED" key (was silently dropping NO TRADE rows from CONTEXT DISTRIBUTION).
- `analysis/AnalysisRunner.vb` `VerdictContext × Outcome` cross-tab gained "ALIGNED" — currently renders n>0 because the filter lets `"NO TRADE [WEAK X]"` bracketed forms through. Working as designed.

### Chunked Deribit OHLC fetch (post-v30)

**The fix that revealed real STRONG_SHORT performance.** `DeribitOhlcFetcher.FetchOhlcRange` was making a single `DeribitClient.GetCandlesAsync` call. Deribit caps responses at ~5000 bars per call, so CSV spans > ~3.5 days silently lost the oldest portion. All 47 STRONG_SHORT verdicts were in the head of the CSV (2026-05-13 13:56–16:11 UTC+8) → all silently excluded from the matrix.

Fix: loop in `CHUNK_MINUTES=5000` segments with `MAX_CHUNKS=20` safety cap. Mid-range failure aborts to avoid silently-partial OHLC maps.

After the fix, the next Analysis Report showed STRONG_SHORT n=47 with **14.9% failure rate** at the recommended cell. Strikingly good — but all 47 samples are from a 2-hour bear cluster on 2026-05-13 PM, so the rate is not yet broadly representative.

### Dual recommendation (post-v30)

- `FailureCellResult` gained `IsMostProfitable` flag (lowest failure rate with n ≥ MinSamplesPerCell, alongside the existing `IsRecommended` for lowest CI width).
- Markdown report renders ★ for IsRecommended, ◆ for IsMostProfitable, ★◆ when both views agree.
- §1, §2, §3, §8 all dual-view. CSV summary gained IsMostProfitable column.
- **AnalysisRunner cross-tab still uses IsRecommended** for v2 barrier-hit walks (correct for the auto-tweaker-friendly consumer).

Why: the original picker minimised CI width, which favours extreme p values (Wilson CI is narrower at p near 0/1). For high-failure tiers like STRONG_LONG (69% failure), the "recommended" cell was the WORST trading-wise. The ◆ view exposes the actually-best cell (for STRONG_LONG, that's 15m / 0.5× ATR at 43% failure).

### REGIME ANCHOR caution (post-v30)

Display-only warning line in the verdict header. Fires only when:
- STRONG_LONG verdict + price > 3.0× ATR below 5m EMA(200) → "fighting intermediate bear"
- STRONG_SHORT verdict + price > 3.0× ATR above 5m EMA(200) → "fighting intermediate bull"

Rendered between CONFIDENCE and SCORE lines. Suppressed otherwise — avoids cluttering normal output.

Threshold (3.0× ATR) is hardcoded. Promote to `cfg.Scoring.RegimeAnchorAtrThreshold` if tuning proves needed.

**Caveat: 5m EMA(200) is ~3.3 hours of data, NOT macro.** Honest labelling kept the field as "REGIME ANCHOR" not "MACRO". True macro (daily timeframe) would need a separate spec adding Deribit daily candle fetch + indicator + UI. Parked as potential future work.

---

## 5. Recent Analysis Report (2026-05-18 14:21 UTC+8) — key findings

Most recent report data (run after all post-v30 commits):

- Rows in CSV: **2580** | Excluded (no OHLC for any window): **1** (was 622 before chunked fetch — fix verified)
- Tier-eligible row totals: STRONG_LONG=72, STRONG_SHORT=47, MEDIUM_LONG=274, MEDIUM_SHORT=115. **Total 508** — matches `structural stop 508 rows` counter. Math verified.

### Failure rates at recommended cells

| Tier | ★ (lowest CI width) | ◆ (lowest failure rate) | Notes |
|---|---|---|---|
| STRONG_LONG | 5m / 0.8× ATR → 69.4% | 15m / 0.5× ATR → 42.6% | **Big divergence.** ★ is worst trading cell, ◆ is actually-best. |
| STRONG_SHORT | 15m / 0.5× ATR → 14.9% | (same — agreed) | All 47 samples from one bear cluster. |
| MEDIUM_LONG | 15m / 0.3× ATR → 25.5% | (same — agreed) | Best workhorse tier. n=274. |
| MEDIUM_SHORT | 15m / 0.3× ATR → 18.3% | (same — agreed) | n=115. |

### STRONG_LONG anomaly — explained but not resolved

**Zero adverse hits across all 432 (6 cells × 72 rows) walks.** Every failure is `Window Expired`, not `Adverse Hit`. Direction quality issue, not stop-tightness issue. The engine is calling STRONG_LONG into a bear regime where price doesn't make the upside target move within the window.

The user accepted the asymmetry and is waiting for the auto-tweaker to flag this when it next fires (69% > 40% threshold).

### Verdict Context × Outcome (with v30 ALIGNED)

| Tag | n | Failure rate |
|---|---|---|
| CONFIRMED | 343 | 52.5% |
| ALIGNED | 76 | 78.9% |
| FLOW_UNCONFIRMED | 19 | 21.1% |
| MOMENTUM_FADING | 422 | 54.3% |
| STRUCTURALLY_WEAK | 203 | 45.3% |

ALIGNED rows are NO TRADE [WEAK X] bracketed forms walked under their sub-bias direction. 78.9% failure means the engine correctly avoided ~79% of these would-have-lost trades. Working as designed.

---

## 6. Active state of auto-tweaker

Settings.json at v30 confirms the v29 fixed-window code is live. No auto-tweaker round has fired yet — `LastEvaluatedRowIndex` was just seeded on the first v29 run (per the spec, set to `currentRowCount` so historical sliding-era data is preserved but not re-evaluated).

Next round eligibility:
- `currentRowCount - LastEvaluatedRowIndex ≥ WindowSize (30)` → need 30 fresh rows post-seeding
- No session boundary crossing within those 30 rows
- Tier-eligible count ≥ MinTier (default 15)
- Failure rate > 40% threshold → propose tweak; ≤ 40% → BELOW_THRESHOLD

The first fire will likely target STRONG_LONG's 69% failure rate. Watch for:
- The proposed diff (in `tools/AutoTweaker/proposed_diffs/<timestamp>.json` if `auto_commit_enabled=false`)
- Whether the LLM proposes the right kind of fix (tighter threshold, regime gate, or something else)
- Whether the `SettingsDiffApplier` validation accepts it (rejected-pattern check, 3-key cap, version monotonicity)

User has `auto_commit_enabled = false` and `dry_run_enabled = true` — the first proposals will be parked for review, not auto-applied.

---

## 7. Parked observations status

From `DeribitIndicatorProject.md` §16.6. As of 2026-05-18:

| ID | Topic | Status | Trigger to revisit |
|---|---|---|---|
| **P1** | Promote BestPivotByVolume to v2 cap arbitration | Watching | "Best is also most-recent" rate < 50% + auto-tweaker validates volume-pivot/target-hit correlation |
| **P2** | Funding momentum threshold v23+ tuning | Watching | Offline analysis shows FundingDelta percentiles support a lower threshold |
| **P3** | OI×CVD asymmetry | **RESOLVED 2026-05-08** | Fixed via priceUp comparison. See history-archive §D. |
| **P4** | STRONG/MEDIUM tier collapse | Watching | 1000+ tier-eligible rows, matrices pick same (window, threshold) |
| **P5** | Liquidation count window | Watching | 1000+ rows, still 0 liq events |
| **P6** | STRUCTURAL_RR_LOW context tag | Watching | Auto-tweaker validates verdict-direction R:R < 1:1 correlates with failure |
| **P7** | Live per-analysis success/fail display | **RESOLVED 2026-05-13** | Shipped as v26. |
| **P8** | Live perf display WEAK tier filtering | Watching | After ~1 week, if WEAK_* inclusion misleads vs STRONG+MEDIUM only |
| **P9** | Auto-tweaker SKIPPED_SESSION_BOUNDARY waste | Watching | If RoundHistory shows many SKIPPED_SESSION_BOUNDARY entries |
| **P10** | POC tier 3 of target cap never fires | Watching | 1000+ runs with CAPPED events, 0 POC selections. Geometry, not dead code. |

**New parked candidates from this session** (NOT yet in §16.6):
- **P11 (resolved-same-session)**: STRONG_SHORT cell-eligibility starvation. Root cause was Deribit fetch truncation. Fixed in `ebfad56`. Don't re-add as parked — fully closed.
- **Possible future P**: Daily-timeframe macro regime indicator. The 5m EMA(200) "REGIME ANCHOR" we just shipped is intermediate, not macro. A daily-EMA spec is a credible follow-up if the trader wants longer-horizon context. Not currently scheduled.

---

## 8. Calibration state

User in **pure data accumulation mode** under the v27-v30 metrics + post-v30 cleanups. Things worth watching as data grows:

### Immediate (next 1-2 days)

1. **First auto-tweaker fire under v29 semantics.** As above. Expect STRONG_LONG to be the target.
2. **REGIME ANCHOR warning frequency.** Should fire ~10-30% of STRONG verdicts at current 3.0× ATR threshold. If much more often, raise to 5.0×; if much less, drop to 2.0×.
3. **Target-hit vs barrier-hit gap.** v28 toggle is live. Probe showed +35pp gap on 67-row sample. Full data should show ~20-40pp consistent gap. If much smaller, direction quality is genuinely poor; if much larger, stops are biting hard.

### Medium-term (next 1-2 weeks)

4. **STRONG_SHORT sample diversity.** Currently all 47 samples are from one 2-hour bear cluster on 2026-05-13. Need more diverse samples across regimes before trusting the 14.9% failure rate.
5. **MEDIUM_LONG / MEDIUM_SHORT growth.** n=274 and n=115 are healthy but tightening the CIs further would help. MEDIUM_LONG is the workhorse tier.
6. **Snapshot creation.** First snapshot fires after 3 consecutive BELOW_THRESHOLD rounds. With STRONG_LONG at 69% (above threshold), the streak is reset. Need a calmer tier to actually generate a snapshot.
7. **WEAK tier P8 check.** After 1 week of live data, compare headline rate with vs without WEAK inclusion. If >5pp difference, promote P8 to a spec.

### Three pre-staged calibration scripts available

`C:\Dev\DeribitVerdictEngine\.claude\worktrees\jolly-tesla-fa3fca\tmp_calibration_queries\`:
- `q1_target_vs_barrier_per_tier.ps1`
- `q2_target_vs_barrier_per_regime.ps1`
- `q3_weak_tier_dilution.ps1`

These were created in the previous session for use once data accumulates. **Run as `pwsh` or `powershell`** — no args, hardcoded paths, write `.md` reports next to themselves.

---

## 9. Calibration Report status

User reports: "Calibration Report now has the status 'Ready for Calibration'." Per the v0.4 schema READY criteria (≥300 rows, ≥3 sessions, ≥3 regimes with ≥50 rows each, liquidation events informational).

**Now also reports `ALIGNED` rows under CONTEXT DISTRIBUTION** (audit cleanup pass fix).

---

## 10. Workflow conventions (critical for the new conversation)

1. **Commit local, do not push.** All commits stay on local master. The user pushes after testing. Per trader-profile §8.
2. **Crypto-trading-context skill** is canonical for trader-profile + writing-style. Load it; don't read trader-profile.md separately.
3. **Host-agnostic constraint** for new code in `tools/AutoTweaker/`, `analysis/`, plus the shared cache helpers (`LivePerformanceTracker`, `OhlcCache`, `AnalysisOutputDump`, `DeribitOhlcFetcher`): zero `System.Windows.Forms` references. Form-side viewers (`AnalysisReportForm`, `OutputDumpSettingsForm`, `RoundStatsForm`, `TweakSettingsForm`) are thin wrappers and may use WinForms.
4. **Spec-first for substantial features.** Drafts go to `/docs` as `<name>-proposal.md` with status PROPOSED. Implementation flips to ✅ IMPLEMENTED after commit.
5. **Settings.json version is strictly monotonic.** Bump on every settings change. `change_log` array — append-only, newest-first.
6. **No `--no-verify` on commits, no force-push.** Standard hygiene.
7. **Build verification:** `dotnet build "C:\Dev\DeribitVerdictEngine\DeribitVerdictEngine.sln"` (quote the path or use forward slashes). If the app is running it locks the .exe — tell the user to stop it to rebuild cleanly.
8. **Shell tips:** Don't `cd` in Bash — the harness manages working dir. Use forward-slash paths (`/c/Dev/DeribitVerdictEngine`) or quoted backslash paths.

### Implementation pattern (kickoff vs inline)

Two patterns established in the previous session:
- **Spec-then-kickoff** for substantial features (> 1 hour, > 100 LOC, settings.json change): write spec, draft a kickoff prompt, user pastes into a fresh Opus Medium conversation.
- **Inline implementation** for small targeted edits (< 30 min, < 50 LOC, no settings.json change): just do it in the current conversation.

The user has been favouring inline for small work this session. Don't insist on kickoff for everything.

---

## 11. Recent debugging case studies (worth knowing)

### Pattern 1 — UTC vs Local timezone bugs in serialisation

Recurring pattern in this codebase. Three instances now:
- `LivePerformanceTracker.ParseEvalLine` (commit `4caa0bc`)
- `OhlcCache.NewestBarTime` + `OhlcCache.ParseLine` (commit `5fa6916`)

Pattern: `DateTime.Parse(s)` without `DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal` converts Z-suffixed strings to local time. `SpecifyKind(Utc)` re-labels Kind without correcting the value, leaving timestamps offset by the local UTC offset.

**Defensive practice:** any time the code parses an ISO 8601 timestamp string with Z suffix or +offset, use the universal-styles flags. Search for `DateTime.Parse` in any new code and verify.

### Pattern 2 — Deribit API truncation

Deribit's `public/get_tradingview_chart_data` caps responses at ~5000 bars per call. Single-call fetches for ranges larger than this silently return only the latest 5000. Two places hit this:

- `LivePerformanceTracker.FetchGapChunked` (v27) — already handled correctly with chunked loop.
- `DeribitOhlcFetcher.FetchOhlcRange` (post-v30 `ebfad56`) — JUST fixed with the same pattern. Silently truncated multi-day analysis fetches.

**Defensive practice:** any new code path calling `DeribitClient.GetCandlesAsync(resolution, startMs, endMs)` for ranges > ~3.5 days needs chunking. The pattern is now in both `LivePerformanceTracker.FetchGapChunked` and `DeribitOhlcFetcher.FetchOhlcRange` — copy from either.

### Pattern 3 — File-write atomicity

Three sites had naive truncate-then-write that wiped state on crashes mid-write:
- `TweakerState.Save` — wiped auto-tweaker state on mid-write kill
- `OhlcCache.WriteAll` — wiped OHLC cache
- `OhlcCache.RollingTrim` — same

Fixed in audit cleanup pass (`d590e92`) with the tmp + `File.Replace` pattern. `OhlcCache.Append` was deliberately NOT changed — append-mode writes can only truncate the last line at worst, can't wipe prior content.

**Defensive practice:** new file-write code that truncates (`File.WriteAllText`, `File.WriteAllLines`, `StreamWriter(path, append:=False)`) on user-persistent data needs the atomic pattern. Append-mode is fine.

### Pattern 4 — Display-side bugs surfaced from output dump audits

The output dump (`analysis_output_dump.md`) is invaluable for catching display-side bugs the CSV can't show:
- 2026-05-12 audit caught B1 TargetCapReason cross-row contamination, B2 CONTEXT line silently dropped on CONFIRMED.
- 2026-05-17 audit caught 8 actionable display issues bundled into the v30 polish pass.

**When the user reports something off, ask for both CSV columns AND a recent output-dump paste.** The latter reveals patterns the former hides.

### Pattern 5 — "Bug 2" — verify against spec before fixing

The "Waiting for session-aligned window" counter was reported as buggy (incremented regardless of verdict, decremented unexpectedly). Investigation showed both behaviours were spec-correct: verdict-agnostic by design, decrement is session-boundary reset.

**Defensive practice:** before fixing, read the spec to confirm the user's expectation matches the design intent. If the bug report describes correct behaviour but with confusion, the fix may be UI/wording, not code.

---

## 12. Pending verification items

Things the user will test in the immediate future. If findings come back, the new conversation should be ready to handle them:

1. **Push to remote** when ready. 12 commits queued. User mentioned "I'll push to remote these changes" earlier this session re v27-v30, then continued making post-v30 commits. Default: don't push proactively.
2. **REGIME ANCHOR caution fires when expected.** Test trigger: stop the engine when STRONG_LONG fires during a clear price-below-EMA(200) moment. If never fires, lower the 3.0× ATR threshold temporarily.
3. **First auto-tweaker fire.** As above. Watch for the proposed diff and validation outcome.
4. **STRONG_SHORT diversifies.** Currently a one-cluster sample. Need fresh STRONG_SHORTs across different regimes.

---

## 13. Outstanding spec items / future work

### Ready when desired (no data dependency)

- **D3** — 5m RSI divergence (symmetric extension of existing 1m logic). Not gated.
- **D4** — Donchian × BBW state cross-reference. Spec needed first, risk of double-counting.
- **C2** — Anchored VWAP from session high/low. Needs multi-session state plumbing.
- **C1** — Multi-session VPFR (naked POCs). Same state-plumbing prerequisite as C2.
- **Macro regime (daily timeframe)** — extend the new REGIME ANCHOR display to use daily EMA(200) instead of 5m EMA(200). Adds Deribit daily candle fetch path + new indicator + UI changes. ~150-200 LOC. Would replace or supplement the 5m anchor.

### Gated on calibration data

- **B1 re-spec** — per-indicator regime weights. Currently STUB; needs hit-rate output to redraft.
- Per-item B4 threshold tuning sweeps (TFI, OFI, TTM, etc.).

### Architectural / long-arc

- WebSocket migration (`§16.4`) — gated on indicator backlog exhaustion + Section A item demand.
- Linux CLI port (`§16.2`) — gated on auto-tweaker proving on live data + analysis accuracy plateau.
- Section A items (post-WebSocket) — Spread momentum, Aggressor velocity, Order book absorption, Liq × OFI flip detector, VPFR profile shape.

### Low-value / explicitly deferred

- D5 (Smart OBV), D6 (MFI replacement) — only if specific divergence false-positives observed.
- HH/HL/LH/LL pattern extension (D1 v2) — defer unless data shows 2-pivot version misses transitions.

---

## 14. Quick-reference file paths

```
Engine root:                 C:\Dev\DeribitVerdictEngine\
Solution file:               DeribitVerdictEngine.sln (root)
Active worktree (this one):  C:\Dev\DeribitVerdictEngine\.claude\worktrees\jolly-tesla-fa3fca\

Settings:                    settings.json (v30)
Project handover:            docs\DeribitIndicatorProject.md (489 lines after trim)
History archive:             docs\history-archive.md (pre-v27 detail, full version log)
Architecture:                docs\architecture.md (with Display Behaviour Clarifications appendix)
Trader profile:              loaded via crypto-trading-context skill — DO NOT read directly

Recent specs (active, IMPLEMENTED):
  docs\display-polish-pass-proposal.md         (v30)
  docs\audit-cleanup-pass-proposal.md          (post-v30, atomic writes + ALIGNED)
  docs\auto-tweaker-fixed-window-proposal.md   (v29)
  docs\target-hit-metric-proposal.md           (v28)
  docs\ohlc-gap-backfill-proposal.md           (v27)
  docs\live-performance-display-proposal.md    (v26)

CSV / cache locations:       bin\Debug\net8.0-windows\
                             ├── analysis_log.csv             (v0.4 schema, 87 cols)
                             ├── analysis_output_dump.md      (full rendered output per run)
                             ├── analysis_eval_cache.csv      (v2 schema with TargetEverHit)
                             └── ohlc_1m_cache.csv            (rolling 7-day)

AutoTweaker:                 tools\AutoTweaker\
                             ├── tweaker_config.json
                             ├── state.json                   (atomic writes now)
                             ├── dry_run_payloads\
                             ├── proposed_diffs\
                             └── manual_diffs\

Snapshot history:            settings_snapshots\
                             ├── manifest.csv
                             └── settings_snapshot_<ts>.json

Calibration query scripts:   .claude\worktrees\jolly-tesla-fa3fca\tmp_calibration_queries\
                             ├── q1_target_vs_barrier_per_tier.ps1
                             ├── q2_target_vs_barrier_per_regime.ps1
                             └── q3_weak_tier_dilution.ps1
```

---

## 15. When in doubt

- **The trader-profile §4 rejected-pattern list is absolute.** No fixed-% targets, no non-directional rewards, no double-counting, no flat regime penalties.
- **Conservative bias wins ties.** Strict over lenient. False-negative (missed trade) preferred over false-positive (bad trade).
- **Don't push proactively.** Test gate is user-side.
- **Don't propose new indicator work pre-calibration.** The bottleneck is data, not code.
- **Ask before doing.** Especially for anything touching scoring logic, settings.json structure, or CSV schema. The user prefers explicit confirmation over speed.
- **BTC macro context: bear market since Oct 2025.** Calibration data is regime-biased toward short trades. Don't over-anchor on STRONG_SHORT 14.9% failure or STRONG_LONG 69% failure as steady-state numbers — both are regime-correlated.

---

**End of handover.** Paste this as the first user message in the new conversation (or save under `docs/` and link to it in the kickoff). The new conversation should have full operational context after reading it + the standard session-start docs.
