# Backlog Dependency Map

**Created:** 2026-07-22 (trader-directed). **Restructured 2026-08-07.**

## 0. What this doc is, and the change that was made to it

This doc answers exactly one question: **what blocks what.** It is referred to by [`roadmap.md`](roadmap.md) (execution order) and [`trader-tick-queue.md`](trader-tick-queue.md) (immediate ticks and build slots). It is not a new authority and never was.

**The 2026-08-07 restructure — the State column is gone.** It carried a *copy* of state that lives in the queue and the roadmap, and on 2026-08-07 **seven of its cells were stale against the tree**. One of them — the backtest synthesizer, reading *"D1–D8 await trader"* against a `tools/BacktestRunner/` that has been in active use since 2026-07-30 — was a row the queue's own §1b sweep had already corrected on 2026-08-01 and nobody backported. That is not carelessness; it is the structural consequence of keeping a third copy of state.

Two of this project's own rulings say so directly: **"a doc must not carry a number that lives somewhere else"** ([`seat-handover-2026-08-05.md`](seat-handover-2026-08-05.md) §5.6) and the tape-retention rule, **keep it unless it is a copy**. The State cells were copies.

**What replaces it.** Each row carries `Blocked by` / `Unblocks` / **`State lives in`** — a *pointer*, never a value. A row moves between §2 (live edge) and §3 (opened edge) and nothing else changes. That is the only maintenance action this doc has, and it fires on a *dependency* change, not on every ship.

**Consequence for readers:** to learn whether an item is done, follow the pointer. This doc will never tell you.

## 0a. Reused IDs — read this before you go looking for one

**Flagged 2026-08-07, trader-directed.** Several short IDs mean more than one thing in this project. Always take the ID together with its source document.

| ID | Meaning A | Meaning B | Meaning C |
|---|---|---|---|
| **C1** | **Trade-store coverage report** — a build, shipped 2026-08-05 (queue Cluster C) | **Bridge v2 feedback file** — a cross-repo contract with the order app, documentation phase closed | **C1/C2 backlog** — multi-session VPFR / anchored VWAP, parked behind the CLI port |
| **F1** | **Tier-ladder read** — `docs/f1-tier-ladder-read-2026-08-01.md`; gates Kelly CAL | **Coverage-report review finding** — trailing-edge gaps are charged to the following hour (`docs/c1-session1-review-2026-08-04.md`) | **Overlay spec-back finding** — the `+local` marker cannot be earned by a rejected key (`docs/settings-local-overlay-spec-back.md`) |
| **F2** | **Coverage-report review finding** — a capture-state flip mid-hour is scoped by the previous marker | **Buffer race** — `ResetBufferState` drops trades (`docs/job1-outstanding-2026-08-01.md`) | — |
| **F3** | **Observational watch** — EXIT GUARD strip vs HOLD/EXIT row during holds; live and unevaluable | **Collector defect** — repair calls send `User-Agent: DeribitBacktestRunner/1.0` (`docs/job1-outstanding-2026-08-01.md`) | — |
| **D1** | **Trade-store capture ruling** — who captures raw tape (`docs/in-app-trade-store-capture-proposal.md` §7) | **TTM `flat_threshold` re-anchor** — parked scoring item (queue Cluster D) | Also the first row of most other specs' D-tables |

**F1, F2 and F3 are the dangerous set.** All three are live in `docs/trader-tick-queue.md` at the same time, in both meanings. A bare "F2" there could mean a spec question or a four-line `SyncLock` fix.

---

## 1. How to read a row

- **Blocked by** — the condition that must become true. "—" means nothing blocks it; it is a slot, not a gate.
- **Unblocks** — what becomes available when it lands. This is the column that justifies sequencing spend.
- **State lives in** — where to look up whether it is done. **Never answered here.**

---

## 2. Live edges — the blocker is not yet satisfied

### 2a. Blocked on a trader decision

| Item | Blocked by | Unblocks | State lives in |
|---|---|---|---|
| **D2** OBV `trend_gate` 18→~23 ⚠ | (i) an open ⚠ boundary slot **and** (ii) the D3 ASIA watch READING — D2 makes OBV less often directional and D3 upgrades TFI votes on ASIA; both push the same ASIA path, so shipping D2 into an open D3 watch corrupts the evidence D3 was sequenced alone to get | Cross-category upgrades; closes the D1+D2 bundle that was ruled "should be ruled together" | queue §0a |
| **E5 / #6 absorption activation** ⚠ | Trader ticks **Path B** (hold v61 anchors, spec a mechanism revision) → mechanism spec → ~1–1.5 wk re-collection → re-derive → *then* the gates | Absorption Step-2 penalty wire-in | queue §0a · [`absorption-anchor-rederivation-2026-07-30.md`](absorption-anchor-rederivation-2026-07-30.md) |
| **F3 watch** (B4b trigger) | A tooling decision: cap-bucket-segmented outcomes on an offline surface, **or** explicit retirement. It is live and unevaluable — do not leave it in that state | Closes a watch that currently cannot produce a verdict | queue §0a · [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) §3 |
| **C1-coverage F2** — split-hour capture-state flip | A **spec question**: a flip mid-hour is scoped by the *previous* marker, which errs toward *not* flagging — opposite to J-B's stated preference | A coverage report that cannot under-report a flip hour | queue §2 · [`c1-session1-review-2026-08-04.md`](c1-session1-review-2026-08-04.md) |

### 2b. Blocked on data or the market

| Item | Blocked by | Unblocks | State lives in |
|---|---|---|---|
| **A4** liquidation × OFI flip ⚠ | **The market only** — ≥1 CASCADE line in `liq_events.log`. Instrument live since v59 | The last unbuilt scoring item in the W2 queue | roadmap §W2 |
| ⚠ **A4's instrument, protected** | `alerts.` must stay **REJECTED** from the `settings.local.json` overlay whitelist — `alerts.enabled:false` on a box means `liq_events.log` is never written there, and the gate pools both boxes' sidecars | — (this is a *guard* edge, not a work item) | [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) F1 |
| **Kelly CAL** (W6-3 / L4) | F1 ladder **separation** — trigger ≥406 pooled weekday STRONG. ⚠ **NEW EDGE 2026-08-07: an AWS CSV copy-back.** The AWS book in hand stops **2026-07-31 13:59**; the gate is unreadable without a fresh copy | Consumer sizing reads empirical kelly fields; **P5 tier values**; retires the on-screen "actual numbers after next book doubling" promise | queue §4 (dated trigger) · [`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md) |
| **P5 tier values** (order app) | Same F1 separation. LONDON currently **inverts** at the top (MEDIUM 53.0% > STRONG 44.8%), which contradicts a STRONG-only policy there | Per-session tier/context subsets going live in the order app | [`f1-tier-ladder-read-2026-08-01.md`](f1-tier-ladder-read-2026-08-01.md) |
| **W6-4 re-run** | Next book doubling (~2,712 eligible rows). ⚠ **Same copy-back edge as Kelly** — bundle both into ONE pooled freeze so the overfit counter stays honest on a shared span | **W6-5 / B1**, the D3–D6 backlog refinements, any W6-7 Tier-C spend | roadmap §W6 · [`w6-4-ceiling-audit-run-2026-08-01.md`](w6-4-ceiling-audit-run-2026-08-01.md) |
| **W6-5** B1 regime-conditional weights | W6-4 showing a **measured prize** + a materially bigger evaluated book (overfit risk is the binding constraint) | The combination-tier ceiling | roadmap §W6 |
| **D3 / D4 / D5 / D6 backlog** (5m RSI-div · Donchian×BBW · smart OBV · MFI) | W6-4 showing combination headroom | — | roadmap §W6 |
| **W6-7** cross-venue lead-lag | Current queue done **AND** W6-4. #5-style correlation gate + #6-style outcome gradient bind activation | The one remaining non-marginal signal class | roadmap §W6 |
| **A5** VPFR shape classification | 30 distinct calendar dates in the book — **28 as of 2026-08-07** (2026-07-03 → 2026-08-06), up from 15 on 07-22. **~2 dates out.** Second, non-negotiable edge: even when met it must clear the **W6 new-indicator bar** (it is a VPFR refinement, not an orthogonal class) — spec-first with that argument, never automatic | — | roadmap §W2 |
| **Geometry-modes study re-read** (v56 instrument) | Book ~doubles, or the W6-1 audit — DIVERGENT flags must clear | TP-half (nearest-mode) live ⚠ candidate | roadmap §W1 |
| **Funding calm-week re-read** | A calm funding week | Fully closes the v52+v53 observation window | queue §4 |
| **Tweaker first fire** (W5 / W6-2) | A >40%-failure NY×1 window, supervised dry-run first. ⚠ **NEW EDGE 2026-08-07: the AutoTweaker weekday filter must land FIRST** — it is the only surface that WRITES `settings.json`, so unfiltered it tunes the engine on sessions never traded. Verified never fired (no tweaker state file, `settings_snapshots/` empty), so this is fixable before it can matter | Window/MinTier recalibration | queue §2 · [`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md) |
| **D1** TTM `flat_threshold` ⚠ | **Un-parks on trades-covered replay** — the vote must be testable against outcomes, not counted. ⚠ **NEW EDGE 2026-08-07: the store has no August data** (root `backtest_data/` ends 2026-07), so no replay can yet cover the v65 boundary | Restores the FLAT band to doing anything. Rides the next boundary that opens for another reason | queue §0a · [`ttm-flat-threshold-rederivation-2026-08-02.md`](ttm-flat-threshold-rederivation-2026-08-02.md) |
| **`session_volume` LONDON/NY multiplier passes** | **RULED PARK (D-C, 2026-07-31)** — the lever is inert live. `DynamicNorms.vb:36-40` builds the threshold from the last 100 **completed** bars while `MainForm_Analysis.vb:237` uses **that same in-progress bar** as the ratio's numerator. Volume vote fires 0.69% of NY / 2.66% at ExecRes 3. A closed-bar-derived multiplier would describe the D3 closed-bar arm, not the live engine | The queued LONDON/NY passes; the v58 follow-on | [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) §3 |

### 2c. Blocked on another repo, or on a rotation that must never be forced

| Item | Blocked by | Unblocks | State lives in |
|---|---|---|---|
| **L3** stop-distance sizing | Order-app side; live-at-min-size stabilising | **L9** + the widest-stop mode's live eligibility | [`profitability-risk-levers.md`](profitability-risk-levers.md) |
| **L9 / E8** structural-stop un-clamp ⚠ | **L3 shipped** (+ calm-regime re-derivation, DG5). W6-1 ruled no-change and handed the real question here — the clamp binds **95.5–99.6%**, so stops are de facto ATR stops | The trader's real stop method live | [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) |
| **T8** — order-app `mode` blind spot | Their **acceptance 3** (owner-only: mode Live + ARM + START). Until it passes the feedback file is *partially proven* — shape/lifecycle/position semantics observed; **disposition path, `last_signal`, mode strings NOT** | Full proof of the bridge v2 feedback contract | [`signal-bridge-v1-proposal.md`](signal-bridge-v1-proposal.md) §10.4 |
| **Effective-source per-row stamp** (`DeriveWsHealth`, ruled **J-E**) | The **next natural CSV header rotation** — **never force one** | Makes overlay **D7 option (c)** implementable; first measurement of the degraded-feed fallback rate; strengthens the coverage report's S1 | queue §3 |
| **`TriggerMode` CSV column** | Same rotation | Makes a trigger-mode change visible in the volume distribution | queue §3 |
| **`SettingsVersion` per-row column** | Same rotation, third of the attribution set | Makes a version straddle filterable. Until then: make every settings-version change coincide with a **process restart** so `InstanceId` is a usable proxy, and keep the checklist §5 ledger — the only place that mapping exists | queue §3 · [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a |
| **D7 option (c)** — exclude a REST box's rows | The effective-source stamp above. **(a) is answered and adequate**; (c) is an upgrade, not a gap | Closes the last open question on the overlay spec | [`overlay-d7-and-row-source-stamp-2026-07-31.md`](overlay-d7-and-row-source-stamp-2026-07-31.md) |
| **C1/C2-backlog** multi-session VPFR · anchored VWAP | The **CLI-port run-state restructure (Q6)**, deferred LAST. Q6-last **keeps these parked** — it does not open them | — | roadmap §W4 |
| **CLI port (Q6)** | The whole W6 programme. Trader ruling 2026-07-08: do not pull forward | O3 | roadmap §W4 |

### 2d. Ops — new 2026-08-07

| Item | Blocked by | Unblocks | State lives in |
|---|---|---|---|
| ⚠ **AWS copy-back — CSV and store** | Trader action. **Verified 2026-08-07:** `analysis_log_aws.csv` ends **2026-07-31 13:59** (7 days stale) and the AWS *trade store* has **never** been copied back at all | **Three things at once:** the Kelly dated trigger and the W6-4 re-run (both need a pooled freeze); the first coverage report against the real AWS store; and resolving whether the 78,798-trade `bin\Release` tape (2026-08-03 21:44 → 2026-08-05 13:13 UTC) is a duplicate or unique | queue §4 · handover §7 |
| **Store has no August data** | The same copy-back, plus a `BacktestRunner fetch` pass | Any replay spanning the **v65/D3 boundary** (2026-08-01 19:02Z onward) — including D1's un-park condition. Root `backtest_data/` holds 1m/3m/5m/15m candles + funding for **2026-01…2026-07 only** | this row |

---

## 3. Opened edges — the blocker is satisfied, and what it released

Kept because "this is now available" is the half of a dependency map people forget to record. Dated at the moment the edge opened.

| Edge | Opened | What it released |
|---|---|---|
| **#7 liq-cascade alarm + #8 level alerts** → A4's gate instrument | 2026-07-22 (v59) | A4 became **market-gated only**. `liq_events.log` sidecar is durable and survives restarts |
| **WS-health line persistence** → durable feed-health record | 2026-07-22 | Closes the "feed health is inferred" monitoring caveat; later becomes coverage-report evidence |
| **B1 eval `NO_DATA` (F4)** → the EV-on-the-strip precondition | 2026-07-21 (`75a2694`) | **EV on the strip is unblocked whenever it is wanted.** Verified live: `# schema=v6`, 120 `NO_DATA` rows |
| **M1–M5 placed-target matrix migration** → W6-3's p-instrument | 2026-07-21 (parity 1335/1335) | Kelly CAL has an instrument to fit against, once the ladder separates |
| **v51 placed geometry PUSHED** → live-at-min-size hard gate | 2026-07-07 | Autotrade live-at-min-size; CLI Stage 3 |
| **Bridge soak review called clean** (69/69 would-act ≡ `Placed*`) | 2026-07-22 | The live ladder: ARM → live-at-min-size → L3 spec slot |
| **W6-4 method spec APPROVED** (K1–K6) + data gate | 2026-07-23 / 2026-08-01 | The W6-4 **run**. ⚠ It ran and was **INCONCLUSIVE**, so it did *not* open W6-5 — the edge below it stays live |
| **Pooled-file report runner built** | 2026-07-30 | The F1 §9 read, which then ran 2026-08-01 |
| **Backtest synthesizer BUILT** (`tools/BacktestRunner/`) | 2026-07-30 | Weeks→days on the whole geometry/threshold class: W6-1 depth, v56 re-read, D2-v2, ASIA §5.2, D3–D6, all future re-baselines. Also *how* the ~64,000× unit bug was found |
| **v62 fee-aware min-move floor** | 2026-07-27 | The §6.1 eval net-EV rider (post-Aug-1); the trader's live `min_net_move_pct` knob. Defaults compose to **0.0008**, byte-identical to v61 |
| **AWS supplementary collector deployed** | 2026-07-23 | Every coverage-bound gate: res-3 §5.2 (which then delivered D3), W6-1 depth, F1 STRONG, W6-4 depth; a second 24/7 A4 cascade instrument. **Dedup RULED AWS-preferred 2026-07-31** |
| **v64 trade-store capture + `settings.local.json` overlay** | 2026-08-01 | The C1 coverage report; the eventual trades-covered replay that un-parks D1. Overlay is what lets the local box run the latest build without capturing tape |
| **J-B scoping ruled** (positive record of intent, never an uptime baseline) | 2026-08-02 | **C1 unblocked**, and D7=(b) ruled out. The general form: a box that dies permanently converges to its own baseline and reports healthy |
| **Weekday scope ruled** (capture 24/7, *evaluation* weekday-only) | 2026-08-03 | Confirmed D4 at 300,000 ms on its existing basis; **cancelled the REST-backfill task**; kept Part B unconditional. Exposed the three-surface filter gap now sitting in queue §2 |
| **C1-coverage report BUILT**, both sessions | 2026-08-05 | **The precondition instrument for every data-gated item** — it decides whether future collection gaps are seen at all. `coverage` verb at `BacktestProgram.vb:282`; Part A `BuildResult`, Part B `RunVenueDiffAsync` |
| **Candle-backfill fixture gap CLOSED** (A51a–e) | 2026-07-31 (`99221b2`) | Guards the defect that destroyed a month of store data. **Residual, not test debt:** nobody has re-introduced the overwrite and re-run to confirm A51 would catch it |
| **W6-1 LONDON ruled NO CHANGE** | 2026-07-31 | Handed the real question to **L9** — the two named candidates are algebraically one lever because the clamp binds 95.5% of LONDON rows |
| **Untracked strays ruling + UI-automation `.ps1`** | 2026-07-22 (`241e791`) | Clean tree |

---

## 4. No dependency — these are build slots, not gates

Listed here so nobody looks for a blocker that does not exist. Sizing and current state live in **queue §2**.

`AutoTweaker` / `LivePerformanceTracker` / `AnalysisRunner` / `WhatIfRunner` weekday filters (AutoTweaker first — see the tweaker edge in §2b) · the **atomic-write total-primitive swap** (5 sites) · **C1-coverage F1** trailing-edge fix · **F2** `ResetBufferState` race · **F3** collector User-Agent · **G12** three manual-content gaps · the **CeilingAudit expected-version constant**.

---

## 5. Standing rejections — do not re-propose without new evidence

`post-websocket-post-calibration-backlog.md` §E + [`trader-profile.md`](trader-profile.md) §4 + [`roadmap.md`](roadmap.md) §5b: sub-minute baseline cadence · #9 provisional forming-bar verdict · authenticated / `raw` Deribit feeds · tweaker Phase-2b per-population autotune · A1 spread-momentum (refuted with evidence 2026-07-03).
