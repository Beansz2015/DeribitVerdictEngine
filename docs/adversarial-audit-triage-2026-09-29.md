# Adversarial audit — orchestrator triage (2026-09-29 UTC)

**What this is.** The orchestrator's triage of the adversarial audit that the trader handed over on 2026-09-29. It dedupes the audit against `master`, sorts every decision into a bucket, decides the non-reserved items, and lists what goes to the trader. It answers item 3 of [`seat-handover-2026-09-29.md`](seat-handover-2026-09-29.md) §0, the build-order decision.

**Source.** Branch `claude/great-keller-s5f4gp`, pull request https://github.com/Beansz2015/DeribitVerdictEngine/pull/3 (open, docs and proof code only). Entry points on that branch: `docs/audits/2026-09-29-orchestrator-handoff.md`, then `docs/adversarial-audit-2026-09-24.md` §A–§D. **Until PR #3 is merged, the audit's files exist only on that branch.** Read one with `git show origin/claude/great-keller-s5f4gp:<path>`.

**Pins.** The audit read engine `6e74181` (2026-09-22, settings v68) and order app `8232e9e`. This triage read local `master` at `6f3442f` (189 commits past the pin; 22 of them unpushed).

**ID sources in this file.** Row IDs (A1, B2, C1 …) are rows of `docs/adversarial-audit-2026-09-24.md` §B. Decision IDs (C-1 … C-21) are decisions of that file's §C. ⚠ **The two sets collide on purpose in the audit:** row `C1` (a defect) and decision `C-1` (a stop-side ruling) are different things; the hyphen is the only difference. Lane IDs (L-7) and external checks (X-1 … X-4) are that file's §D. New decision IDs here use the prefix `AT-` (checked free in `docs/`, `Core/`, `tools/`, `verify/` on 2026-09-29).

---

## 0. The answer to handover item 3 — build order before the holiday

**My read: fix the collector-halt class first (decision C-8), deploy it by about 2026-10-06, and move nothing else from the audit before 2026-11-25.** The history store build stays after the holiday unless C-8 is deployed clean early and usage allows. Ruling `AT-1` in §6.

- **Why C-8 first.** The handover's rule (`seat-handover-2026-09-29.md` §2) was: fix first if triage finds a live defect that hurts what the collector records during the six weeks away. It found one class:
  - **Row B2, verified on `master`:** any exception in an auto-run analysis raises a modal `MessageBox` (`UI/MainForm_Analysis.vb:33-49`). The `Finally` that re-enables `btnAnalyze` does not run until someone clicks OK, and `RunAutoAnalysis` returns early while `btnAnalyze` is disabled (`UI/MainForm_AutoRun.vb:166`). **One unexpected exception stops the collector until a human clicks OK — up to six weeks.**
  - **What that loses is unrecoverable:** `analysis_log.csv` rows and book data. Tape lost in the same halt is recoverable from the history host ([`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §5a).
  - **Honest likelihood:** no halt of this kind is recorded in `docs/` (grep 2026-09-29). The proven triggers (inverted clamp pairs, `"trade_costs": null`, `VPFR.num_buckets 0`) are all settings edits, and none happens during the holiday. The residual risk is an exception nobody has found yet, or row B3 (below). **Low probability, six-week cost, small fix.**
- **Why nothing else before the holiday.** Every other engine decision is reserved (scoring, rendered values, settings or store writes). The handover's risk rule says a scoring change deployed just before 2026-10-14 is risky. None of the audit's engine findings corrupts the collector's recording.
- **The order app is a separate question, and it may be the most urgent one.** The three S0s (rows A1, A12, A13) are in the order app's position handling. If the app places live orders, manually or through the bridge, they are live money risks today. Question `AT-Q1` in §6.

---

## 1. Dedupe — what moved on `master` since the pin

Method: `git log --oneline 6e74181..master -- <file>` for every file the audit cites in bands B–F, then a read of the moved lines where a row depends on them (run 2026-09-29).

**Unchanged since the pin (the finding stands as written):** `DeribitClient.vb`, `DeribitWsFeed.vb`, `OhlcCache.vb`, `ExitGuardEvaluator.vb`, `UI/MainForm_ExitGuard.vb`, `UI/Controls/TapeStripLabel.vb`, `UI/Controls/MiniMeter.vb`, `Core/Indicators_Structure.vb`, `Core/Indicators_OrderFlow.vb`, `Core/Settings/SettingsLoader.vb`, all of `analysis/` cited, `tools/WhatIfRunner/*`, `tools/BacktestRunner/CoverageReport.vb` and `BacktestProgram.vb`, `tools/CeilingAudit/FeatureMatrix.vb` and `AuditReport.vb`, `tools/AutoTweaker/SettingsDiffApplier.vb`, `UI/TweakSettingsForm.vb`, `.github/workflows/verify.yml`.

**Moved, and what that means for the rows:**

| File (commits since pin) | Commits | Effect on audit rows |
|---|---|---|
| `Core/SignalEmitter.vb` (2) | `a6b33fe` POC-tier gate fix; `31f57d0` Kelly v69 | **K1 fixed and deployed 2026-09-24.** Row C20's A80b half is closed: A80b now runs by default (`verify/ordercheck/Program.vb:17409`). The stop floor (row A1, `SignalEmitter.vb` floor) was not touched |
| `verify/ordercheck/Program.vb` (14) | fixtures since the pin | Row C20 is **half open**: A81b (K2, maker-side liquidations) still skips unless `ORDERCHECK_KNOWN_DEFECTS=1` (`:17951`) |
| `LivePerformanceTracker.vb` (2) | Kelly v69 sessions 1 and 2 | Row B1 **still present**: `Await _initTcs.Task` runs before the `PerformanceDisplay.Enabled` check (`:666`, `:669`). Row C1 **still present**: new bars are admitted only when `CloseTime > maxExisting` (`:685`), so a stored forming-bar stub is never replaced |
| `UI/MainForm_Layout.vb` (2) | Kelly v69; MTF TTL refactor | Row B3 **still present**, moved to `:1996` (`CInt` over `UtcNow − LastFrameUtc`) |
| `UI/MainForm_Analysis.vb` (2) | absorption S1; CSV rotation S2 | Row B2 **still present** (`:33-49`) |
| `Core/TradeStoreWriter.vb`, `TradeStoreGapRepair.vb`, `tools/BacktestRunner/HistoricalStore.vb` | RR-1 sort key (`72f262e`); `D-2`/`D-2b` (`2dd65fb`) | Band E rows **not re-checked**. The RR-1 changes touch the repair sort, not the torn-row parse (row E1) or the flush accounting (row E2) — a reading of the commit subjects only |
| `tools/BacktestRunner/OverlapValidator.vb` (2) | `TOOL-1`/`D-10` fix, 2026-09-25 | Row C13 **fixed** (carried from `trader-tick-queue.md` §2; not re-read) |
| `tools/checks/verify-gate.ps1` (1) | `e00075a` per-commit token check | Does not touch row C20: the gate still never sets `ORDERCHECK_KNOWN_DEFECTS` |
| `MarketState.vb` (1) | absorption S1 | Row D4 (absorption tag) not re-checked |

**The four audits already in `docs/audits/` on `master`** (`2026-09-25-fixture-harness-a/b/c.md`, `2026-09-25-ui-controls.md`) are byte-identical to the branch copies (blob hashes compared). They are lanes L-1, L-2, L-3 and L-6 of this audit, not a separate package.

---

## 2. Triage buckets

### 2.1 Already fixed on `master`

| Row | What | Where fixed |
|---|---|---|
| K1 / C20 (A80b half) | POC-tier gate read the VPFR labels inverted | `a6b33fe`, deployed 2026-09-24 |
| C13 | `OverlapValidator` checked OI labels the engine never emits | 2026-09-25 (`TOOL-1`) |
| AUD-15, M2 F7, M6b "CRITICAL" (Kelly) | Kelly contradicted the payload's stops | Kelly rewritten in v69 (2026-09-25). The audit already drops these to display-only (report §A item 7). **Not re-checked against v69** |

### 2.2 Known and queued — the audit adds evidence, not new work

| Row | Queue item it joins | What the audit adds |
|---|---|---|
| C1 (OHLC stubs) | `trader-tick-queue.md` §2 row "The eval cache double-stores rows and misses hits the candles show" (7.7 % of rows) | **A candidate mechanism, confirmed in code** (`LivePerformanceTracker.vb:685`). ⚠ **It also reaches the v69 Kelly book:** `ComputeKellyBook` reads the same eval cache, so v69's "f* < 0 in every session" read may be partly this artefact. Not measured |
| K2, D5 (liquidation side and the dead cascade alarm) | `D-4` re-ruling (handover item 5) | Nothing new |
| C12 (`SwingFallbackRead` misses rows only in the `.bak`) | `trader-tick-queue.md` §2 row "`SwingFallbackRead` … don't read the rotated `*col-*.bak` books" | Default and `diagexport` modes fixed in `8943324`; four modes still open |

### 2.3 Retires with the box trade store — reserved disposition (`AT-3`)

Rows E1–E6 (tape-store integrity), the coverage-report half of decision C-19 (row C19), and row B5's gap-repair half. All of them protect or judge the box trade store, which retires at stage 4/5 of [`history-data-store-spec.md`](history-data-store-spec.md) §2. Any hole they cause is recoverable from the history host.

- **My read:** do not fix them on the box. Carry two lessons into the stage-2 history-store build instead: a torn row must not poison a scan (row E1), and rows count only after the write is flushed (row E2).
- ⛔ **This is reserved under the CLAUDE.md economy test.** A richer option exists (fix them now, so the box store is truthful until about December), and my reason for not taking it is cost. So it goes to the trader.
- ⭐ **Truthful now, whatever the ruling:** the audit's row C19 says a `--strict` coverage pass can certify holed tape. Treat every past `--strict` pass as **not** proof of completeness. The history-host validation is the completeness evidence for the windows it tested.

### 2.4 Auto-proceed — decided here (engine repo)

| Decision | What | Log |
|---|---|---|
| C-8 | The four collector-halt fixes (rows B1–B4). **Build is auto-proceed; the deploy is the trader's.** Waits on `AT-1` for timing only | `AT-L1` |
| C-20 (b) | The gate runs the known-defect fixtures against a ledger, the way [`csv-rotation-riders.md`](csv-rotation-riders.md) works. A skip without a ledger row fails the gate. One row today (A81b) | `AT-L2` |
| C-21 | Fixtures for the coverage gaps ship only in the same commits as the fixes they guard | `AT-L3` |

### 2.5 Reserved — to the trader, after 2026-11-25 unless you rule otherwise

Decisions C-1 to C-7, C-9 to C-12, C-14, C-19 (withdrawing the CeilingAudit ruling) and C-20 (c) (the K2 fix, which is `D-4`). Each is scoring, `settings.json`, a rendered value, store writes or a schema. **Rule them together by root after the holiday**, as the audit's §C opening list suggests: stop geometry (C-1, C-2) · fees (C-3, with fixture A41) · measurement (C-9, C-10, C-11, C-19) · settings validation (C-5, C-12). No ruling is needed before the holiday.

⚠ **One of them affects a reading made before the holiday:** C-11 (past rulings that read broken surfaces). The CeilingAudit ruling that closed `W6-5`/`B1` and `D3`–`D6` as "no measured headroom" rests on a model with no side term. I confirmed that in part: `tools/CeilingAudit/FeatureMatrix.vb:40-70` one-hot encodes the scored categoricals with no side column. I did not check whether rows are split by side upstream.

### 2.6 The order app — another repo, another seat

Decisions C-15 to C-18 and every Band A row except A1's engine half. They belong to the order app's owner and its seat. **Hand them over with this file and the audit's `docs/audits/order-app/` folder.** Urgency depends on `AT-Q1`.

---

## 3. The remaining audit items — decided

| Item | What | Decision | Log |
|---|---|---|---|
| L-7 | An independent review of the whole audit (Fable 5.1, high) | **Run it after 2026-11-25, before any reserved fix from §C ships.** C-8 does not wait for it | `AT-L4` |
| X-4 | Windows-only checks | **Its culture item is already answered in practice:** this Windows dev box runs A34a, A35a, A79m and A79o green (489 PASS at the `D-2b` build). The culture name only matters for the Linux port. The watcher probe (P9) waits for C-12; the `FileShare` collision (M5 F4) falls with §2.3; L-6's proof waits for C-14 | `AT-L5` |
| X-3 | The exit guard's false-latch rate on real tape | Only before ruling C-14, as the audit says | `AT-L6` |
| X-1 | Testnet orders that settle five Deribit behaviours | **The trader's call** (testnet account). It sets the trigger rate of rows A1 and A13 | `AT-Q2` |
| X-2 | How often row A1 fires, from real trades | **The trader's data.** Never commit it: the repo is public | `AT-Q2` |

---

## 4. A seam the audit found and the repo never recorded — daylight saving

Row F5 of the audit: session hours are fixed UTC. **Europe leaves summer time on 2026-10-25, the US on 2026-11-01 — both inside the holiday.** After those dates the London and New York opens are one hour later in UTC than the engine's session buckets and the 13:30 UTC VWAP anchor assume. No doc in `docs/` mentions daylight saving (grep 2026-09-29).

- **Nothing to change before the holiday.** A session-hour change is scoring, and reserved.
- ⚠ **Every session-segmented read that spans those dates mixes two session alignments.** That includes burst outcome **run 1** (data cut 2026-11-25) and the next burst-watch reads. Treat 2026-10-25 and 2026-11-01 as dataset seams. Whether run 1 needs a stratum for them is `AT-2`.

---

## 5. The pre-registered burst outcome read — checked against the audit's measurement flaws

The audit says every success-rate surface has three flaws (report §A item 3). Run 1 of [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) is the first read after the holiday, so I checked its tools:

| Flaw | Burst read status |
|---|---|
| Walk starts at T+3 with the entry priced at T (row C3) | **Not inherited.** The walk in `tools/ops/SwingFallbackRead/SwingFallbackRead.vb:378-387` starts at `RowMin + 1` minute. Row C3's `+3` is in `analysis/ForwardWindowJoiner.vb` and one other `SwingFallbackRead` mode (`:703`), not the one the burst read uses. I did not check whether the bar dictionary is keyed by open time or close time |
| Minute rows counted as independent trades (row C4) | **Handled.** The spec's CIs are cluster-robust by session-day (`burst-outcome-read-spec.md` line 203) |
| No fee-inclusive EV, and the loss path is taker (row C2) | **Net EV per trade is the headline**, per `DeribitIndicatorProject.md` §5a. Fees are maker/maker on both paths, by that section's rule 2. The audit's "the stop costs 5 bps, not 3" challenges that house rule; it is decision C-3/C-10 and reserved |

---

## 6. For the trader

**Rulings:**

| ID | Decision | Options | My read |
|---|---|---|---|
| `AT-1` | Build order before 2026-10-14 (handover item 3) | (a) C-8 first, deploy by ~10-06, history store after the holiday · (b) C-8, then the history store before the holiday if usage allows · (c) history store first, C-8 after the holiday | **(b).** C-8 is small; the history store is the larger build and needs the most seat time |
| `AT-2` | Daylight-saving seams in burst run 1 | (a) add a pre/post-seam stratum before any outcome is computed · (b) leave run 1 as pre-registered and note the seam | **(a).** Run 1 is still pre-outcome, so adding a stratum now costs no pre-registration integrity. (b) is cheaper and hides a known confound |
| `AT-3` | Box-store integrity rows (§2.3) | (a) fix E1/E2 on the box now · (b) carry the lessons into the history store and drop the box fixes with the drop list | **(b)**, flagged as the economy class — see §2.3 |
| `AT-4` | A halt the fix does not cover (a hang, a reboot) during the holiday | (a) a holiday seat may restart the collector app if a read-back shows no new rows for 30 minutes · (b) read-only only, as planned | **(a).** It covers every halt cause, including ones nobody has found. The 2026-09-18→21 outage ran 3 d 13 h |
| `AT-5` | Merge PR #3 | (a) merge it (docs and proof code only) · (b) leave it open | **(a).** This triage cites files that exist only on that branch |

**Questions:**

- `AT-Q1` — **Does the order app place live orders today, manually or through the bridge?** If yes, rows A12 (the emergency close cancels the stop, never checks the market reduce, and reports "Executed" anyway) and A13 (no position-level loss cap) are live now. Until decision C-16 ships, the cheap guards are operational: keep M.SL ticked, keep Amount at one contract, and do not flip the Buy/Sell toggle with a position open (row A16).
- `AT-Q2` — Do you want X-1 (testnet orders) and X-2 (your trade records) run? Both need your account or data.

---

## 7. Auto-proceed log (one line each)

- `AT-L1` — C-8 collector-halt fixes: options fix now / defer / leave; **fix now (build)**; no scoring, settings, CSV or rendered value, one revert each. Error display keeps the modal box for a manual click (the P5b §3.2.1 choice) and logs durably on auto-run.
- `AT-L2` — C-20 gate hatch: options (a) keep the env skip · (b) ledger · (c) fix and delete; **(b) now, (c) with `D-4`**; (a) is the economy option that ships a scoring defect under `ALL PASS`.
- `AT-L3` — C-21 fixtures: alone or with their fixes; **with their fixes**; alone they would pin today's defects.
- `AT-L4` — L-7 timing: now or after the holiday; **after the holiday, before any reserved §C fix ships**; the guarantee is the same because no reserved fix ships before then.
- `AT-L5` — X-4: run all four now or scope them; **scoped as in §3**; the culture item is answered by the existing Windows green runs.
- `AT-L6` — X-3: now or before C-14; **before C-14**; it informs only that ruling.

---

## 8. What I did not verify

- Any Band A row. The order app's code was not opened; `8232e9e` is the audit's pin, not checked against the app's current master.
- Rows B4, D1–D10, E1–E6, F1–F6, and every Band C row except C1 and C18 (in part). They are carried from the audit.
- The audit's proofs. None was re-run here.
- Whether row C1 moves the v69 Kelly book measurably.
- Whether the collector app restarts by itself after a box reboot.
