# Overlay whitelist — second-pass re-audit + J-D ratification (2026-07-31)

**From:** the incoming orchestrator seat (gap-audit items **G3** and **G4**).
**Why this exists:** JOB 1 §2 queued **J-D** (ratify the restated whitelist rule) and §4 recorded the residual that made it urgent — *"I audited two of eight blocks and both failed; the implementer reports auditing the remaining six and finding them clean. I did not re-audit their audit … given a 2-of-2 failure rate on the ones I did check, a second pair of eyes on the other six is worth more than the usual."* Neither was carried into the seat-close handover. This closes both.
**Subject:** [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md) §2.2/§2.4 — **still AWAITING TRADER (D1–D7), nothing built**, so these are pre-build corrections.
**Result: the residual was worth chasing. Of the seven blocks reported clean, two are not, one whole block is named nowhere at all, and the section's arithmetic is wrong.**

---

## 1. J-D — RATIFIED, and extended

The restatement under review (§2.1): *"A key is safe to diverge per box **when it cannot move the failure rate**. The HC fences are a **good first filter** for that — not a proof."*

**Ratified as standing.** It is correct and it has reach past this spec: the same conflation — *an existing fence justifies a new property* — is available anywhere a fence gets reused as evidence. `network.transport` carries HC12 because the tweaker has no business tuning timeouts, not because transport is scoring-neutral, and that is a general failure shape.

**But the criterion is necessary and not sufficient, and §2.4 fell through the gap.** Two of the blocks it clears cannot move the failure rate and nonetheless break the instruments that **measure** it — one of them the sole gate on a queued ⚠ scoring decision. The rule as written has no clause that would catch either.

**Extended form, ruled as standing:**

> A key is safe to diverge per box when **(i)** it cannot move the failure rate, **and (ii)** it cannot change what any evidence instrument records that a **queued decision or standing watch** depends on.
> **HC fences are a good first filter for (i) and no filter at all for (ii).**

Clause (ii) is the one this project keeps needing and keeps not having: `alerts.` is fenced off the tweaker, is scoring-neutral by every test §2.4 applies, and switching it off deletes the A4 gate evidence.

---

## 2. Findings, ranked

### F1 · `alerts.` is **not** clean — it gates the A4 gate instrument ⚠

`Core/AlertsTracker.vb:116` opens `FoldTrade` with `If cfg Is Nothing OrElse Not cfg.Enabled Then Return`, and the sidecar writes (`AlertsSidecar.TryAppend`) sit **inside that same method** at `:143` (FIRST_SEEN) and `:157` (CASCADE). Upstream, `DeribitWsFeed.vb:439` computes `foldAlerts = al IsNot Nothing AndAlso al.Enabled` and skips the fold entirely. **`alerts.enabled: false` ⇒ `liq_events.log` is never created on that box.**

That file is the *sole* A4 gate — [`backlog-dependency-map.md`](backlog-dependency-map.md): *"A4 liquidation × OFI flip ⚠ | **ONLY the market now**: ≥1 CASCADE line in `liq_events.log`"* — and both boxes are explicitly counted as two instruments ("second 24/7 A4 cascade instrument"; the deploy checklist says to **pool both boxes' sidecars**). Diverging this halves the observation rate on an event that has **never been seen once** in 8,025+ runs.

It is not even binary: `cascade_min_trades` / `cascade_window_sec` (`:150`, `:175`) decide *which* events earn a CASCADE line, so a threshold divergence changes the evidence **content** while both boxes stamp the same settings version. That is the unfilterable straddle §2 exists to prevent — relocated from the scoring book into the evidence book, where nothing checks for it.

**My read: REJECT `alerts.` from the whitelist.** The cost is nil — per-box alert divergence is not the need driving this spec (`trade_store.` is), and suppressing local alert noise is a UI concern, not a settings one. The alternative — "admit, but AWS must keep it on" — is a conditional a whitelist cannot express.

### F2 · `performance_display.` gates the whole live outcome-measurement instrument

`LivePerformanceTracker.vb:188-191` — `If Not cfg.PerformanceDisplay.Enabled Then _initTcs.TrySetResult(True) : Return "disabled"` — neither `analysis_eval_cache.csv` nor `ohlc_1m_cache.csv` loads or fetches; `:494` short-circuits before any per-run eval row is appended. The OHLC gap-fill knobs (`GapBackfillEnabled` `:261`, `MaxGapFillCalls` `:265`, `MaxGapFillMinutes` `:266`) change how much OHLC coverage exists, hence which eval rows resolve versus sit PENDING/WINDOW_EXPIRED. Under §2.1 read literally these do not move the failure **rate** — they move its **measurement**, which the spec never distinguishes.

**Mitigant, so this is not overstated:** the offline pipeline is independent — `analysis/AnalysisRunner.vb` loads `analysis_log.csv` and re-fetches 1m OHLC from Deribit; the tweaker reads `analysis_log.csv`; **no tool reads the eval cache**. So what diverges is the *live per-box yardstick*, recoverable offline.

**My read: ADMIT, with the caveat recorded in the spec** — no queued decision or standing watch reads the eval cache, so clause (ii) is satisfied today. **Revisit the moment anything gates on it** (Kelly CAL is the near candidate, since it wants empirical per-tier win rates). Second-order and worth one line: the output dump's `PERF STRIP` content and the `[B]`/`[T]` tag diverge too.

### F3 · `mtf_gate` is named **nowhere** — not admitted, not split, not rejected ⚠

`settings.json` carries **17 settings blocks**. §2.2 enumerates 7 admitted + `network.` + `auto_run.` + a catch-all *"Everything else — `scoring.`, `indicators.`, `session_volume.`, `resolution_profiles.`, `regime_*`, `kelly.`, `version`"* = **16**. The missing one is `mtf_gate` — and per CLAUDE.md it is *"a hard veto — BLOCK forces NO TRADE regardless of score."*

The catch-all covers it **if and only if the build is an allow-list**, which D1 implies but never states as an implementation constraint, and **A50d pins only `scoring.*`, `indicators.*` and `version`**. An implementer reading §2.2 as a reject-list ships an overlay that can flip a hard veto per box with no version change. §2.4's scoring-path list *does* name `MTFGate`, which is what makes this an enumeration bug rather than an analysis one — and exactly the kind that survives review.

**My read: name `mtf_gate` in the reject list explicitly, state the allow-list constraint in D1, and extend A50d to pin it.** Also unnamed: `last_modified`, `modified_by`, `change_log` — §1 *asserts* the overlay carries no `version`/`change_log`, but only `version` is backed by a whitelist entry, and under §1's "arrays replace wholesale" rule an overlay `change_log` would merge. Cosmetic beside `mtf_gate`, but it is the gap between what §1 claims and what §2.2 enforces.

### F4 · The whitelist ∩ UI-writeback interaction is unpinned

Three admitted blocks are **live-UI-writable**: `live_strip.enabled` (`UI/MainForm_LiveStrip.vb:341-343`), `performance_display.metric_mode` (`UI/MainForm_Layout.vb:1689-1691`), `analysis_logging.output_dump_*` (`UI/OutputDumpSettingsForm.vb:80-86`). §1.2 names only *"MIN NET MOVE %, output-dump settings"* — it misses the TAPE checkbox and the metric-mode context menu.

Under §1.2's own (correct) rule that `Save` writes the **base**, these become one-way mirrors on an overlaid box: the click mutates the **shared tracked file — the xcopy source for AWS** — while the overlay keeps winning locally. For `live_strip` it is visibly broken: the refresh tick re-syncs the checkbox from merged config (`:90-93`), so the user's click snaps back within ~2 s having silently written to the deploy source.

**A50c pins base-vs-merge; nothing pins whitelist ∩ UI-writeback.** **My read: add a fixture**, and have §1.2 enumerate all four live-save paths rather than two.

### F5 · Verified passes, recorded so they are not re-audited

- **`signal_bridge.` — clean, for a reason the spec did not state.** `UI/MainForm_SignalBridge.vb:62-65`/`:86-88` call `LogWsHealthTransitionForRun` **before** the `enabled` early-return, explicitly commented *"Runs UNCONDITIONALLY."* So `ws_health.log` survives a disabled bridge — which matters, because the coverage report's S1 reads it. Had the ordering been reversed this would have been a third failure. A verified pass, not an assumed one.
- **`exit_guard.` — the only genuinely inert block of the seven.** No file write, no network, no logging; timer-only and dormant when flat.
- **`analysis_logging.` — passes**, with one unstated caveat: `output_dump_max_runs` is a retention depth on a rolling evidence file, so diverged boxes carry different history depth silently. No queued decision names the dump as a gate, so severity is low, but "Clean" overstates it.
- **`trade_store.` — clean for scoring, and an instrument by definition.** Diverging it is the entire point of the spec; the dependency is already recorded (coverage-report D7). Flagging only the framing: filing it under "Clean" is the wrong frame for the one block whose divergence *is* the feature. Note `store_dir` relocates the store and `gap_repair_*` changes REST call volume.

### F6 · The arithmetic in §2.2/§2.4/§8 is wrong

§2.4 audits **nine** blocks (7 admitted whole + `network.` split + `auto_run.` rejected), not eight. The sentence should read *"Two of **nine** blocks failed the review's inspection; the remaining **seven** pass mine. **Seven** blocks admitted whole, one split, one rejected."* Every "six" should be "seven"; every "eight" should be "nine".

**And after this re-audit the honest count is: 5 clean · 2 instrument-breaking (`alerts.`, `performance_display.`) · 1 split · 1 rejected · 1 unlisted (`mtf_gate`).**

Minor: §2.4's *"The scoring path reads exactly: … `ATR`, `Volume`, `VWAPDynamic` …"* mixes top-level blocks with children of `indicators` (`EngineSettings.vb:175,177,178`). Read as the top-level enumeration §2.2 uses it as, it overstates coverage — 9 top-level + 3 nested, not 12 top-level.

**Dead key, found in passing:** `session_block_semantic` is declared at `Core/Settings/EngineSettings.vb:1226-1227` and read nowhere in the tree.

---

## 3. What this says about the review process, not the spec

The implementer's audit was not lazy — its method is stated, it is reproducible, and it found the right answer to the question it asked. It asked *"does this block reach the scoring path?"* and answered correctly for all seven. **The failure is that the question was too narrow, and the question came from the rule.** §2.1's criterion mentions only the failure rate, so a diligent audit against it cannot see A4's gate instrument.

That is why §1 extends the rule rather than merely ratifying it. **A checklist derived from an incomplete criterion inherits its blind spot, and re-running the same checklist harder never finds it** — which is the generalisable half, and the same shape as the A43f and D2 lessons: internal consistency cannot detect a wrong frame.

---

## 4. What I did not verify

- **I did not re-derive the `network.`/`auto_run.` findings** — those are the original reviewer's, already accepted and folded into the spec; I audited the seven they did not.
- **Nothing was run.** The overlay is spec-only and unbuilt, so no behaviour was observed; every finding is static code reading against the tracked tree (worktree copies under `.claude/worktrees/` excluded).
- **I did not audit the four metadata keys** beyond noting that three are unnamed.
- **Severity of F2 rests on "no tool reads the eval cache"**, which was checked by enumeration across `analysis/` and `tools/`. If a future instrument reads it, F2 escalates from caveat to reject.
