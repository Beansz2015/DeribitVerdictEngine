# AutoTweaker hostile audit — 2026-09-24

**Scope:** the 11 files under `tools/AutoTweaker/`: `AutoTweakerCore.vb`, `PromptBuilder.vb`, `ClaudeApiClient.vb`, `SettingsDiffApplier.vb`, `SnapshotManager.vb`, `ConditionsExtractor.vb`, `RoundStatsBuilder.vb`, `CompositeScorer.vb`, `TweakerConfig.vb`, `TweakerState.vb`, `AutoTweakerProgram.vb`.
**Audited tree:** commit `6e74181`.
**Files not covered:** none. All 11 were read in full.
**Read beyond scope, to trace dependencies:** `analysis/FailureRateMatrix.vb`, `analysis/ForwardWindowJoiner.vb`, `analysis/AnalysisConstants.vb`, `analysis/DeribitOhlcFetcher.vb`, `Core/Settings/SettingsLoader.vb`, `Core/Settings/EngineSettings.vb` (relevant classes), `Core/SignalEmitter.vb` (`ComputeSideLevels`), `Core/ScoringEngine_Calculate_Verdict.vb` (Step 5c), `Core/ExecutionResolution.vb`, `DynamicNorms.vb`, `AnalysisLogger.vb` (header and row), `UI/TweakSettingsForm.vb`, `UI/MainForm_Layout.vb:379`, `UI/MainForm_SignalBridge.vb`, `settings.json`.
**Already known before this audit, and extended rather than repeated:** `Validate` never checks values; `Apply`/`ApplyRevert` throw on .NET 8 at the change_log / `ToJsonString` step; a failed `Apply` doesn't advance the window, so every run re-bills.
**Proofs:** [`docs/audits/proofs/autotweaker/`](proofs/autotweaker/README.md) has the probe source, the exact run command and the recorded output. Where the report below says `scratchpad/probe/` or "`scratchpad/probe/Program.cs`", that is now `docs/audits/proofs/autotweaker/Program.cs.txt` (renamed to `.txt`; the README explains why).

The report below is reproduced verbatim from the chat report. The only thing left out is the closing one-line offer to publish it as a page.

---

I built AutoTweaker with .NET 8 in this container and ran nine probes against the real `AutoTweaker.dll` (source: `scratchpad/probe/Program.cs`). I didn't modify any repo files. The build output I created in the repo has been removed.

Headline: none of this reaches the live payload today, because `Apply` still throws. The probe shows the throw happens at `ToJsonString(opts)`, before any write. But fixing that crash on its own doesn't produce a hot reload. The tweaker writes the tracked repo-root `settings.json`, and the engine reads a different file. Fixing the crash also switches on F1, F2 and F11 at once.

---

**--- LOGIC TRACE ---**

Setup: v68, NY × 1-min, weekdays only, 120-row window, 40% threshold, `auto_commit` on, triggered by the "Run Tweaker Now" button (that's the only trigger; there's no scheduler). Prices assume BTC ≈ $64k. That comes from the repo's own "$51 floor = 0.0008" anchor and is illustrative; the conclusions depend on ratios, not the price. The 8 bps floor is ≈ $51.

1. **Before the flush:** ATR(7, 1m) is 30. The NY fallback target is 1.75 × 30 = $52.5, which clears the floor by $1.5. The stop is at most 1.6 × 30 = $48.
2. **Onset (−1.5% ≈ $960 in about 20 min):**
   - 1m bar ranges are $250–400, and ATR needs about 7 bars to catch up. Rows from these minutes carry $48/$52 barriers.
   - The order app enters at about T+0 and is stopped inside the first bar.
   - The evaluator skips bars T+1 and T+2 and starts walking at T+3, when price is already $150–300 lower. SHORT rows that were stopped out live score **SUCCESS**; LONG rows score ADVERSE_HIT. (F8)
   - The 15m MTF veto lags, so many shorts become NO TRADE.
3. **High-vol chop, ATR 140:** targets are $245 (38 bps) and stops ≤ $224 (35 bps). The 5m window rarely resolves, so expiry counts as failure. A bar with a range ≥ $469 is AMBIGUOUS, which also counts as failure. `ATRMultiplier` hits the 4.0 clamp at onset, then falls back to about 1.0 within about 100 bars. (F10)
4. **Trigger:** the trader clicks on Thursday. The cursor is oldest-first, so it evaluates Tuesday's window. (F9) Three branches:
   - **A.** Fewer than 60 of 120 rows are tier-eligible (MTF veto, NO TRADE): SKIPPED_INSUFFICIENT_TIER, the cursor moves on, and the flush is never scored.
   - **B.** Tier-eligible rows reach 60 or more but split, say, 22/18/12/10 across four tiers, so no cell reaches n ≥ 30. The aggregate is **0.0%**, the round is BELOW_THRESHOLD, and the streak goes up by one. At streak 3 the tweaker copies the *tracked* settings file into a snapshot with AvgFailure ≈ 0 and a composite score ≈ 100+, which tops the revert leaderboard. (F6, P5)
   - **C.** MEDIUM_SHORT has n = 41, with failure 5m 78% / 10m 63% / 15m 51%. The narrowest-CI rule picks **5m**, so the aggregate is 78% and the prompt fires. (P6)
5. **Prompt (branch C):**
   - The full `settings.json` is embedded: 172 KB, of which 147 KB is `change_log` prose.
   - Then the success matrix, and the last 50 of the 120 rows. Those rows carry no placed levels and no per-row outcome.
   - HC2 says "no ATR-based stops"; HC21 says the ATR stop keys are tunable.
   - The model is chosen via `/v1/models`, which returns `created_at` as a string. The code only reads numbers, so it takes the first `claude-opus-*` in the list. Current Opus models think by default, and that thinking shares the 4096-token output limit. (F13)
6. **Likely diff:** `stop_max_atr_mult` 1.6→2.2, `atr_stop_multiplier` 1.6→2.2, `atr_target_multiplier` 1.75→1.25. In the idealised zero-edge case, failure drops from 52% to 36% (below threshold), while expected value moves only from −4.0 to −3.7 bps — which is just fee drag. The metric "improves" by 16 points without any new information. (F7)
7. **Validate:** these keys are explicitly tunable, so the fences pass. The stale check passes if the model copies "1.6" exactly. Values aren't checked (already known). Picked cells were already appended to state before the decision.
8. **Apply today:** it throws (known). The round becomes ERROR, the cursor doesn't move, and the saved state now includes duplicate picked cells. The next click sends an identical prompt and a new model sample. The loop only exits on a parse failure or an empty diff, and both are **recorded as a successful round**. (F3, F4) With the current code, the flush window ends up booked as a success.
9. **Apply once the crash is fixed:** it writes repo-root `settings.json` v69 via temp file + rename. The engine watches `bin\…\settings.json`, so nothing happens. Even on the right file, a rename doesn't fire the watcher's `Changed` event (proven on Linux, unverified on Windows). The engine stays on v68. The tweaker records APPLIED v69 and scores the next backlog window as if it ran on v69. (F1)
10. **Next live payload:** v68 geometry is unchanged until a build copies the tracked file into `bin`. At that point it goes live at an arbitrary moment, and there's no settings-version column in the CSV to locate the edge. Say it loads when ATR is back to 40:
    - A SHORT with no structural target gets a fallback target of 1.25 × 40 = $50 (7.8 bps). That's under the $51 floor, so Step 5c flips it to NO TRADE and the payload direction is NONE.
    - A SHORT with a structural target $90 away keeps that target, but the stop clamp moves from $64 to $88. Reward-to-risk drops from 1.41 to 1.02.
    - With fewer tier-eligible rows, later windows are SKIPPED_INSUFFICIENT_TIER, so the tweak is never re-evaluated. It sticks.
    - If the sampled value had been type-invalid instead (e.g. 1.5 on an Integer key, the shape of the prompt's own example): the engine's reload throws and it keeps v68 with a LastLoadError banner. On the next restart it boots on class defaults, with the signal bridge **off**. (F5)

---

**Severity scale:** S0 = reaches the live payload today; S1 = puts wrong values into live settings or disables a safety control once the known crash is fixed, or makes a failing round read as a success today; S2 = systematically wrong decisions; S3 = degraded or misleading; S4 = hygiene. **Nothing is S0 today, and the only reason is the known crash.** The v66 change_log entry also records that the tweaker has never fired.

### F1 — Tweaker writes a file the engine doesn't read, and couldn't trigger a reload anyway
- **SEVERITY:** S1 (confirmed from code; Linux watcher behaviour confirmed by probe P9; Windows unverified)
- **LOCATION:** `UI/TweakSettingsForm.vb:607` (hardcodes `repoRoot\settings.json`); `TweakerConfig.vb:80`; `UI/MainForm_Layout.vb:379` (engine loads `BaseDirectory\settings.json`); `SettingsDiffApplier.vb:363-372`; `SettingsLoader.vb:698-702`
- **DOWNSTREAM IMPACT:** "Hot-reloads within ~200 ms" is false for the tweaker's write path. The change goes live whenever the next build copies the tracked file into `bin`, mid-session, with no CSV marker. The prompt's "current settings" and every snapshot come from the tracked file, not from what produced the rows. The tweaker's write also makes the tracked file newer, so the next build overwrites any runtime UI edits in the `bin` copy.
- **FAILURE SCENARIO:** APPLIED v69 while the engine keeps running v68. The next window is credited to v69; the streak and snapshot then certify settings that never ran.
- **ANALYTICAL CRITIQUE:** Two independent faults. Pointing `settings_path` at the `bin` copy still leaves the rename invisible to a `LastWrite|Size` watcher. In P9 the rename produced 0 events and an in-place write produced 1.

### F2 — Naming the parent object bypasses every path fence
- **SEVERITY:** S1 (confirmed by probe P2)
- **LOCATION:** `SettingsDiffApplier.vb:113-130` and `229-240` (prefix and exact-path matches); `NavigatePath` resolves objects
- **DOWNSTREAM IMPACT:** One "key" can replace a whole block, including `scoring` itself.
- **FAILURE SCENARIO:** In P2 each of these passed validation, and the resulting file reloaded cleanly:
  - `mtf_gate` → `enabled:false`: reload gives `mtf.enabled=False`.
  - `kelly` → `max_risk_fraction` 0.5.
  - `scoring.trade_costs` → floor 0.
  - `signal_bridge` → new `output_path`.
  - `scoring.structural_levels` → `enabled:false`.
- **ANALYTICAL CRITIQUE:**
  - The only obstacle is that `old_value` must be the exact compact JSON of the current object. The rejection-sampling loop (F4) eventually produces it.
  - Omitting `old_value` makes the stale check fail open: its `Try` swallows the error. The only things stopping the write are two unrelated crashes (`AutoTweakerCore.vb:680`, `SettingsDiffApplier.vb:343`).

### F3 — A garbage model response counts as a successful round
- **SEVERITY:** S1 (confirmed by probe P1; reachable today)
- **LOCATION:** `SettingsDiffApplier.vb:551` (catch returns 0 items with action "tweak"); `AutoTweakerCore.vb:585, 647-664`
- **DOWNSTREAM IMPACT:** A window with ≥40% failure gets streak +1 and `LastSuccessfulRoundIso`, and may trigger snapshot creation.
- **FAILURE SCENARIO:** All of these parse to 0 items: output truncated at the token limit, prose before the JSON, a code fence followed by more text, `"action":null`, and `revert` without a target.
- **ANALYTICAL CRITIQUE:** "No change warranted" and "I couldn't read the answer" end up in the same state. Combined with F4, it's the dominant way out of a failing window.

### F4 — The re-bill loop is rejection sampling against the validator
- **SEVERITY:** S2 (confirmed from code and probe P2)
- **LOCATION:** `AutoTweakerCore.vb:443-452` (picked cells appended before the decision) and `666-674`; `PromptBuilder.vb:273`
- **DOWNSTREAM IMPACT:** Extends the known re-bill finding. The problem isn't only cost: identical prompts get re-sampled until something passes the validator or fails to parse. Each retry also saves duplicate picked-cell entries, so the prompt's "last 20 runs" shows one window as consecutive rounds.
- **FAILURE SCENARIO:** The stale check compares raw text, so `1.60` against the file's `1.6` is rejected every time. Each attempt ships 172 KB.
- **ANALYTICAL CRITIQUE:** A validator that is sampled repeatedly acts as a search target, not a gate.

### F5 — Wrong-type values pass, including the one value check that exists
- **SEVERITY:** S2 (confirmed by probe P2)
- **LOCATION:** `SettingsDiffApplier.vb:232-233`; `PromptBuilder.vb:230`
- **DOWNSTREAM IMPACT:** The engine's deserialisation throws and it keeps the old settings (`SettingsLoader.vb:475-480`). The next restart boots on class defaults with `SignalBridge.Enabled=False`.
- **FAILURE SCENARIO:** `mtf_gate.enabled` set to `null`, `0.0` or `"off"`; `bbw_squeeze_penalty` 2→1.5 (an Integer); `"2.4"` as a string. All passed validation, and the engine's reload threw on every one.
- **ANALYTICAL CRITIQUE:** The system prompt's own example uses a fractional value on an Integer key. It teaches the invalid shape.

### F6 — "No sufficient cell" reads as 0% failure
- **SEVERITY:** S1 (confirmed by probe P5; reachable today)
- **LOCATION:** `AutoTweakerCore.vb:461`; `FailureRateMatrix.vb:342`
- **DOWNSTREAM IMPACT:** The resulting snapshot has AvgFailure ≈ 0, which gives the maximum composite score. The revert prompt then tells the model to prefer it.
- **FAILURE SCENARIO:** In P5, 64 of 64 rows were stopped out, split 16/16/16/16 across the tiers. Recommended cells: 0. Aggregate: 0.0%. Outcome: BELOW_THRESHOLD.
- **ANALYTICAL CRITIQUE:** The tier-count gate (≥60 across four tiers, counted before exclusions) doesn't guarantee any cell reaches n ≥ 30. In two-sided chop, that's the normal case.

### F7 — The objective can be improved by geometry alone
- **SEVERITY:** S2 (confirmed from code and closed-form arithmetic)
- **LOCATION:** `FailureRateMatrix.vb:293`; `PromptBuilder.vb:24`
- **DOWNSTREAM IMPACT:** The optimiser is rewarded for tighter targets and wider stops. The tweak carries into calm conditions, where fallback targets fall below the 8 bps floor. Step 5c then turns those verdicts into NO TRADE, windows are skipped, and the tweak is never re-evaluated.
- **FAILURE SCENARIO:** In the idealised zero-edge case (driftless price, no expiry), P(success) = S/(T+S). The diff in step 6 moves failure from 52% to 36% while expected value stays at −4.0 → −3.7 bps.
- **ANALYTICAL CRITIQUE:**
  - Expiry counts as failure, and in bad regimes the recommended cell is the 5m one (P6). Both push toward shorter targets.
  - A wick touch isn't a maker fill.
  - The maker take-profit vs taker stop fee asymmetry isn't priced.
  - The inverse-contract asymmetry is only 0.7% at 35 bps, a second-order effect. The real problem is that the objective has no P&L term at all.

### F8 — The T+3 latency floor models a human; the order app is a machine
- **SEVERITY:** S2 (confirmed from code)
- **LOCATION:** `ForwardWindowJoiner.vb:270-279`; `RoundStatsBuilder.vb:287`
- **DOWNSTREAM IMPACT:** Stop-outs in the first two bars are invisible to the evaluator. Those are exactly where stops based on a lagging ATR get run.
- **FAILURE SCENARIO:** Trace step 2: live stop-outs score as SUCCESS.
- **ANALYTICAL CRITIQUE:** The evaluator measures a trade the automated order app doesn't take.

### F9 — Settings versions are never attributed, and the cursor is oldest-first
- **SEVERITY:** S2 (confirmed from code)
- **LOCATION:** `AutoTweakerCore.vb:238-243`; `AutoTweakerProgram.vb:130-140`
- **DOWNSTREAM IMPACT:** Each manual click tweaks *today's* settings using the oldest unevaluated rows. Windows straddle settings versions (the CSV has no version column, and InstanceId is ignored). `--apply-manual` doesn't reset the streak, move the cursor, or finalise the snapshot, and it uses a hard-coded cap of 3.
- **FAILURE SCENARIO:** A streak credits v69 for rows that ran under v68.
- **ANALYTICAL CRITIQUE:** Nothing records which settings produced which rows.

### F10 — The conditions vector can't see the volatility regime
- **SEVERITY:** S2 (confirmed from code and probes P7, P8)
- **LOCATION:** `ConditionsExtractor.vb:364-375`; `AutoTweakerCore.vb:751, 762`; `DynamicNorms.vb:105`
- **DOWNSTREAM IMPACT:** Revert matching only really works on the regime label.
- **FAILURE SCENARIO:**
  - `ATRMultiplier` = ATR ÷ its own ~100-bar 1m mean, so an ATR-140 week and an ATR-30 week both bucket as NORMAL.
  - The snapshot paths pass 0.0/0.0 spread thresholds, so every snapshot records spread mix `W:100`.
  - A skip inside a streak truncates it: counter 3, rounds returned 2.
- **ANALYTICAL CRITIQUE:** Absolute ATR isn't in the vector at all.

### F11 — A revert restores trader-owned state wholesale
- **SEVERITY:** S1 (confirmed by probe P4; currently blocked by the known crash)
- **LOCATION:** `SettingsDiffApplier.vb:381-437` and `440-468`
- **DOWNSTREAM IMPACT:** Rolls back Kelly, fee venue facts, the bridge's `enabled`/`output_path`, feature switches and `hold_*`; drops keys added after the snapshot; replaces the live change_log with the snapshot's.
- **FAILURE SCENARIO:**
  - In P4, the snapshot integrity check passed with Kelly at 0.5, taker fee 0, absorption scoring on, structural levels off, and a missing key.
  - Worst case: the trader halts orders by setting `signal_bridge.enabled=false`. ARM is runtime-only and survives the reload. A revert sets `enabled=true`, and armed payloads resume.
- **ANALYTICAL CRITIQUE:** "Proven snapshot" certifies scoring, not the operational state around it.

### F12 — The trigger is statistically noisy and reads different windows on either side of 50%
- **SEVERITY:** S3 (confirmed by probe P6 and arithmetic)
- **LOCATION:** `FailureRateMatrix.vb:342-354`
- **DOWNSTREAM IMPACT:** At a true 35% failure rate, a 45-row cell reads ≥40% 29% of the time. The pick rule chooses the best window below 50% and the worst above it.
- **FAILURE SCENARIO:** In P6, a tier with a 45% failure rate in its 5m cell reported an aggregate of 30%. The trigger line also says "over window 120 rows" while the aggregate covers roughly 30–60.
- **ANALYTICAL CRITIQUE:** There's no hysteresis and no statistical-power check.

### F13 — The model is unpinned, and thinking eats the token limit
- **SEVERITY:** S3 (plausible; the API wasn't called live)
- **LOCATION:** `ClaudeApiClient.vb:20-21, 49-57, 105-114`
- **DOWNSTREAM IMPACT:** The code reads `created_at` as a number, but the published Models API returns a string. So selection falls to list order, and behaviour changes whenever a new Opus ships. The model used isn't recorded anywhere.
- **FAILURE SCENARIO:** Current Opus models think by default, within the 4096-token limit. That produces either a response with no text block (ERROR, re-bill) or truncated JSON (F3). The fallback id `claude-opus-latest` isn't a valid model id.
- **ANALYTICAL CRITIQUE:** Each re-sample may come from a different model generation.

### F14 — The prompt contradicts itself and hides the evidence
- **SEVERITY:** S3 (confirmed from code)
- **LOCATION:** `PromptBuilder.vb:29` vs `118-122`; `377-389`
- **DOWNSTREAM IMPACT:** HC2 forbids ATR stops while HC21 permits tuning them. The rows shown to the model have no placed levels and no outcomes. 85% of the payload is change_log prose, which includes the tweaker's own earlier reasoning fed back in.
- **FAILURE SCENARIO:** Proposals reflect the model's priors, not inference from the data.
- **ANALYTICAL CRITIQUE:** The model can't connect the failures to the geometry that caused them.

### F15 — Cursor and population fragility
- **SEVERITY:** S3 (confirmed from code)
- **LOCATION:** `AutoTweakerCore.vb:164-168, 274, 402, 427`
- **DOWNSTREAM IMPACT:**
  - A skip discards the whole 120-row window. On most days that drops the start of the NY session, usually about the first hour (13:00–14:00 UTC, including the 13:30 US open), plus the session's final rows.
  - The row filter uses the current session boundaries, but the key that detects a filter change doesn't include them. Editing a session's hours reshuffles which rows the cursor points to.
  - A 3-min population is accepted, but the matrix hard-codes 1-min windows and the fetch window ends at +16 min.
  - The streak and the active snapshot survive a re-seed or a log rotation.
- **FAILURE SCENARIO:** Stored row ranges end up pointing into a different file.
- **ANALYTICAL CRITIQUE:** Parts of the data are dropped systematically, not at random.

### F16 — Hygiene
- **SEVERITY:** S4 (confirmed from code)
- **LOCATION:** `SettingsDiffApplier.vb:346, 415`; `TweakerConfig.vb:116-123`
- **DOWNSTREAM IMPACT / FAILURE SCENARIO:**
  - `change_log` is appended at the end, but the repo convention is newest-first.
  - A revert truncates the change history.
  - Validate uses the settings as read at the start of the run; Apply re-reads the file after an API call of up to 120 s and doesn't re-check for stale values, so a concurrent edit to the same key can be overwritten.
  - A typo in the tweaker config silently loads defaults and drops the population filter, which pools sessions.
- **ANALYTICAL CRITIQUE:** Each is small on its own; together they mean the files don't reliably tell you what happened.

---

**Not verified:** the watcher behaviour on Windows (run P9 there); the live API response shape and thinking behaviour (no API key in this container). Run the probes with `dotnet build -o out && dotnet out/Probe.dll` in `scratchpad/probe/` after building `AutoTweaker.vbproj` to `/tmp/claude-0/atbuild`.

One design note, not a finding: `auto_commit` writes `settings.json` changes that affect scoring. CLAUDE.md puts that class on its reserved list, for exactly the "version edge lands mid-InstanceId" reason that F1 and F9 show.
