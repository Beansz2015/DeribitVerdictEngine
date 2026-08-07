# Trader Tick Queue

**Created:** 2026-08-01 (trader-directed). **Purpose:** the one place that answers *"what is waiting on me, in what order, and what does each one block?"* — the **scoped immediate slice** of [`roadmap.md`](roadmap.md). Derived from the specs and the board; **not a new authority.**

**Authorities:** [`roadmap.md`](roadmap.md) = the strategic board and execution order · [`backlog-dependency-map.md`](backlog-dependency-map.md) = **what blocks what** (edges only — as of 2026-08-07 it carries **no state at all**, by design) · [`profitability-risk-levers.md`](profitability-risk-levers.md) = profitability lens · each spec's own D-table = the decision text. **This doc carries ORDER and GATES; where it disagrees with a spec, the spec wins.**

**Maintenance:** update State on every tick. Move closed rows to §5 rather than deleting. **Re-run the §1b shipped-state sweep whenever this queue is rebuilt — do not inherit a prior assembly.**

## 0. Orientation — what each doc is authoritative FOR

**The failure this table fixes is not that docs go wrong — it is that "authority" was never scoped.** Nothing below is demoted; each is pinned to what it actually governs.

| Doc | Authoritative for | **Not** for |
|---|---|---|
| [`trader-tick-queue.md`](trader-tick-queue.md) *(this doc)* | **What is outstanding, in what order, and its gates.** §0a answers "what is owed" at a glance; §1b is the dated shipped-state sweep | Dependencies · decision text · strategic altitude |
| [`roadmap.md`](roadmap.md) | **The strategic board** — every workstream, done and outstanding, at altitude; execution order; the **binding sequencing rules** (§5) | The immediate slice — it points here |
| [`backlog-dependency-map.md`](backlog-dependency-map.md) | **What blocks what** — edges, and a pointer to where each item's state lives | **State. It carries none, deliberately** (restructured 2026-08-07 after 7 stale cells) |
| [`seat-handover-2026-08-05.md`](seat-handover-2026-08-05.md) | **THE current seat handover** — verified state with re-check commands, rulings, conventions, and what that seat got wrong | A task list; it points here instead, deliberately |
| [`seat-handover-2026-08-02.md`](seat-handover-2026-08-02.md) | Its **rulings and conventions**, all still binding | Current state — superseded |
| [`seat-handover-2026-07-18.md`](seat-handover-2026-07-18.md) §3 | **Standing rules — all still binding** | Current state · its §2 queue is spent |
| [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) | The **2026-07-31 rulings** (J-B/J-C/J-E/D-F) | Its §4 task list and §1 state — read [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) beside it |
| each spec's own D-table | **The decision text** — where this queue disagrees, the spec wins | — |

**Superseded for state, still current for rules:** `seat-handover-2026-07-18.md` (§3 binds), `fable-continuation-2026-07-23.md`. Neither should be cited for what is outstanding today.

---

## 0a. Decisions genuinely unanswered — the short list

**§1 records history with strikethrough, so "what is actually owed" is not answerable at a glance. This is.** ✅ **Re-verified against the tree 2026-08-07** — re-verify rather than inherit.

| # | Owed by | Note |
|---|---|---|
| **D2** OBV `trend_gate` 18→~23 | trader — ⚠ boundary | Derived and ready, **not available now.** Verified `18.0` in `settings.json`. Two reasons, and the second is the binding one: (i) its D1 bundle-partner is **PARKED**, so D2 no longer waits on a D1 re-derivation — it goes alone at a future boundary or waits for one to open; (ii) ⚠ **it must wait for the D3 watch to READ.** D2 makes OBV less often directional; D3 upgrades TFI votes on ASIA. Both push the same ASIA path, which is exactly why the ruling separated them — and **that confound does not end when D3 ships, it moves from "same boundary" to "during the watch."** Shipping D2 into an open D3 watch corrupts the evidence D3 was sequenced alone to get |
| **E5** absorption **Path B** | trader ticks the path | Coordinator ratified Path B (hold v61 anchors, spec a mechanism revision — `window_sec` / episode-cumulative pressing / D8 sparse handling, display-only, no ⚠). The path itself is unticked. Path A's V-table is on file but projects NY 0.000% and ~10–15 weeks to the gate — **not recommended.** Activation slips past mid-Aug on any path |
| **F3 watch** | a tooling decision | Live and **unevaluable** — needs cap-bucket-segmented outcomes on an offline surface, or explicit retirement. **Do not leave it live**; archiving or stranding a live watch is a failure this project already has once |
| **C1-coverage F2** — split-hour rule | a spec question | ⚠ **NEW 2026-08-07 (promoted from §4 of the handover).** A capture-state flip mid-hour is scoped by the *previous* marker. Rare, but it errs toward **not** flagging — the opposite of J-B's stated preference, which is the reason it needs a decision rather than a build slot |
| ⚠ **Local capture ON — one action outstanding** | the trader, on the box | ✅ **RULED 2026-08-07: option (b).** Not a decision any more — a **file action**. Delete `settings.local.json` from **`bin\Debug\net8.0-windows` ONLY**. ⚠ **KEEP the `bin\Release` overlay** — corrected same day, before it shipped: Release is the AWS deploy source, and capture there repopulates `backtest_data\` inside it, which an overwriting xcopy would then push over AWS's tape (measured: 228,163 AWS rows vs 78,798 local — about 150,000 trades destroyed, unrecoverable). Hazard box and pre-flight: `aws-collector-deploy-checklist.md` §1a and §1b. ⚠ **Expect `+local` to disappear from `bin\Debug` and REMAIN on `bin\Release`** — `aws-collector-deploy-checklist.md` §3 now has a three-row table because the expected marker differs per folder. Decision text: `in-app-trade-store-capture-proposal.md` §7 row **D1-a** |

**Explicitly NOT owed — do not re-raise:**

- **C1-coverage is BUILT** (2026-08-05, both sessions, reviewed and ACCEPTED, one review finding fixed `c6a7a63`). Verified in the tree: `coverage` verb at `tools/BacktestRunner/BacktestProgram.vb:282`, Part A `BuildResult`, Part B `RunVenueDiffAsync`, TAPE STORE strip in `UI/MainForm_Layout.vb`. **Its F1 and F2 are the only remainders** — F1 is a build slot (§2), F2 is the decision above.
- **D1 TTM is RE-DERIVED and PARKED** (2026-08-02). The **unit** is wrong, not the value — `delta` tracks volatility (CV 26% in USD vs **6.5–11.7% in ATR multiples**), so an absolute USD constant cannot survive a regime change, and in ATR units the per-resolution ladder dissolves. `flat_threshold` stays **0.5** and the FLAT band stays inert — **recorded and deliberate, not unnoticed.** ⚠ The fix is ATR-relative k≈0.25–0.30 and is a **CODE change with its own spec and ⚠ boundary**, not a settings tick. **Do not inherit either retired number** — neither the 25.0/40.0 ladder (it moves the vote 46.5→35.1%, *away* from its target) nor the 1.45 ratio it was to be rebuilt from (measured **1.774**, stable every month, ≈√3). Un-parks on trades-covered replay.
- **D3 is TICKED, SHIPPED AND LIVE ON BOTH BOXES** — settings **v65**, ASIA armed at 5.5 from 2026-08-01 19:02Z, boxes restarted **1.6 s apart** so the version edge is cleanly attributable. Verified: ASIA 5.5 / NY 4.5 / LONDON 5.5. The ⚠ boundary is spent; the watch is live in §4. **Any pooled ASIA read spanning the boundary must split on the InstanceId ledger** in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a — there is no settings-version column in the CSV.
- **E1 Kelly is DECIDED** (option (c) — wait for separation, display honestly now). Display shipped `18b1ea8` and confirmed. **CAL is parked, not pending.**
- **E6** fee knob confirmed · **E2** W6-4 run (INCONCLUSIVE — the queue does *not* unlock) · **E3 / E4** approved and built · **A1 / A2 / A3** closed · **B1** shipped 07-21.
- **E7** (A4) is market-gated and **E8** (L9) is order-app-gated — neither is a decision.

> **Why this section exists, recorded because it was the third instance.** The E1 row said *"needs an explicit trader decision"* for a decision the trader had already made — written into its own doc and never carried back to the row. §0 designates **this queue** as the state read, so a stale row here is worse than a stale spec header: it is wrong in the doc everything else points at. **The 2026-08-07 sweep found the same shape again** — see §1b.

---

## 1. The queue

### Cluster A — the v64 landing. ✅ **COMPLETE 2026-08-02.**

A1 answered (option **c** — the local box avoids capture through the overlay, not a hand-edit, so there is no standing chore and no silent-restore failure mode) · A2 overlay **built** `291457c`, F1/F2/D-C fixed `1611011`, re-reviewed and accepted · A3 **both boxes live**, AWS capturing from 2026-08-01 17:50 UTC, local capture correctly OFF, Kelly `[EST]` trader-confirmed.

**The reusable part, kept.** `trade_store.enabled` ships `true` — right for AWS, wrong for the local box under D1's AWS-only ruling — and `settings.json` is `PreserveNewest`, so **the build that tested v64 would have been the build that started local capture.** The overlay is gitignored and lives in `bin\`, so it must be placed **before** the first build carrying a newer tracked `settings.json`. Once it is, `PreserveNewest` can refresh the base and the merge still resolves capture to `false`.

**On D7, kept because it is not a permanent close.** Answered 2026-07-31, option (a): a REST-mode box is expressed by a tracked `settings.json` change with a version bump, because a REST box is break-glass rather than routine and a break-glass change *should* be deliberate and version-visible. Option (c) — excluding a REST box's rows from the pooled book — unlocks once the J-E effective-source stamp gives rows a source column.

### Cluster B — ✅ **CLOSED: it was never work. It shipped 2026-07-21.**

**B1 (eval `NO_DATA` / F4) SHIPPED `75a2694`** — N1–N5 built, R1 and R2/N4 with it. **Verified live:** `# schema=v6` in the cache, `analysis_eval_cache.csv.v5.bak` present, **120 `NO_DATA` rows**. **The EV-on-the-strip precondition is therefore already satisfied** — that question is unblocked whenever it is wanted.

### 1a. The convention this produced — verify shipped state before offering work

**This item was carried as outstanding, ticked by the trader, and an implementer conversation was recommended for it. All three were wrong, and the error survived a correction pass.** The proposal's header read *"PROPOSED — D-table awaits trader"* while §3 recorded the tick; the header was corrected **from the D-table alone** and the tree was never checked. That fixed the wrong half.

**Second occurrence of the exact shape** — [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §6.2. Both times the item was an eval-surface fix whose spec header had gone stale.

> **A spec's status header is not evidence of code state. Before offering any spec as available work, verify it in the tree.**
> One command: `git log --oneline -S'<distinctive-symbol>' -- <file>`.

The cost of skipping it is asymmetric: a wasted implementer conversation, and a queue that misrepresents what is outstanding to the person deciding what to spend on.

### 1b. Shipped-state sweeps

**2026-08-01 — 4 of 13 rows wrong, all in the same direction.** B1 (shipped 07-21, live), E3 (built, `SignalEmitter.vb:324-328`), E4 (built, `tools/BacktestRunner/`), E1 (blocker removed 07-30). **Nothing was found in the opposite direction.** Root cause: the queue was assembled partly from a grep for *"await trader"* across `docs/` — **that grep matches prose and section headings, not state.**

**2026-08-07 — re-run against the tree, 4 findings.** The queue held up much better; every settings value, every gate and every "explicitly not owed" claim verified. What did not:

| Finding | Detail |
|---|---|
| **Cluster C read BUILD-AUTHORIZED for work built 08-05** | And §2 of this same file already listed *C1's F1* as a backlog item — which only makes sense if C1 shipped. **The contradiction was internal to one document**, the identical shape as the E1 row that created §0a. Both rows fixed here |
| **Push state** | Handover says "in sync"; tree is **ahead 1** (`8b488cc`, the handover doc committing itself) |
| **Atomic-write site line numbers had drifted** | The comment added in `5ed8862` moved them. Re-anchored in §2 — and re-verified that `Core/Settings/SettingsLoader.vb:346` is real, after a first grep scoped to `Core/*.vb` missed it and nearly produced a false "site absent" report |
| **A blocker that was recorded nowhere** | The AWS copy-back — see §2 and §4. It gates two dated instruments and no doc named it |

### Cluster D — the ⚠ boundary contention

| # | Tick | Gate |
|---|---|---|
| ~~**D1**~~ | ~~TTM `flat_threshold` re-anchor~~ | ⛔ **RE-DERIVED then PARKED 2026-08-02.** See §0a |
| **D2** | **OBV `trend_gate` 18 → ~23** | ⚠ Ready; blocked behind the D3 watch reading. See §0a |
| ~~**D3**~~ | ~~ASIA `burst_ratio_threshold` = 5.5~~ | ✅ **SHIPPED as settings v65.** ⚠ Boundary spent. **Not the settings-only change the original row implied** — threshold *presence* is the arming mechanism, so the POCO default in `EngineSettings.vb` moved in lockstep (v60 precedent; the harness builds cfgs from `New EngineSettings()`), and **A28c had to be re-pinned**: ASIA *was* the un-armed exemplar, so the un-armed arm is now **constructed** rather than borrowed. New **A52a** (JSON→arming contract + JSON↔POCO drift guard) |

**RULED 2026-08-01 — two boundaries, not one or three: D3 alone, then D1+D2 bundled.** D3 went first because it **arms** rather than retunes, and an activation deserves its own clean window. **Honest caveat carried into its D-table:** the W6-4 ceiling audit gave the first outcome-linked read on ASIA aggressor velocity — `AggrVelBurstRatio` AUC **0.5179** (n=217). Essentially no demonstrated edge. It does not refute arming (NY and LONDON were armed on distributional grounds too, and LONDON's watch passed) — but it is the only outcome evidence that exists on this knob, and it is neutral at best.

### Cluster E — data-gated, other repos, or genuinely later

| # | Item | Gate |
|---|---|---|
| **E1** | F1 §9 pooled read → Kelly CAL → P5 | 📖 **READ DONE → THE LADDER DOES NOT SEPARATE.** MEDIUM 42.2% ≈ WEAK 42.7%, all CIs overlap, **LONDON inverts at the top** (MEDIUM 53.0% > STRONG 44.8%). ✅ **DECIDED 08-02: option (c).** Display shipped; **CAL parked**; **P5 values remain gated** — the LONDON inversion contradicts a STRONG-only policy there. Re-reads at the §4 dated trigger |
| ~~**E2**~~ | W6-4 ceiling audit run | ✅ **RUN → INCONCLUSIVE.** ΔAUC −0.0291, CI [−0.197, +0.124] straddling ±0.030. **The queue does not unlock**; re-run at the next book doubling, **no spend meanwhile** |
| ~~**E3**~~ | D2-v2 what-if candidate mode | ✅ **BUILT** — `use_best_pivot_candidate` wired at `SignalEmitter.vb:324-328`, default `false`. Live-enable is a later separate ⚠ |
| ~~**E4**~~ | Backtest synthesizer | ✅ **BUILT** — `tools/BacktestRunner/`, in active use. Its §6 heading still reads *"D-table (await trader)"* — **that heading is the stale artefact, not the status line** |
| **E5** | Absorption Path B | Trader ticks the path — §0a |
| ~~**E6**~~ | Fee knob | ✅ **CONFIRMED** — the 5-bps-net state stands, composing **0.0008**. Verified in `settings.json` |
| **E7** | A4 liquidation × OFI flip | Market-gated only (≥1 CASCADE line). **Protect the instrument — `alerts.` stays REJECTED from the overlay whitelist** |
| **E8** | L9 structural-stop un-clamp | Gated on **L3** (order app). W6-1 ruled no-change and handed the real question here |

---

## 2. Not ticks — these need a build slot, not a decision

Ordered by what each one prevents. **Verified present in the tree 2026-08-07** unless noted.

| Item | Size | Where |
|---|---|---|
| ⚠ **AWS copy-back — CSV *and* store** | Ops action, not a build | **Newly recorded 2026-08-07 and it gates the most.** `analysis_log_aws.csv` in hand ends **2026-07-31 13:59** (7 days stale) and the AWS *trade store* has **never** been copied back. It blocks: the **Kelly dated trigger**, the **W6-4 re-run**, the first coverage report against the real AWS store, and resolving the `bin\Release` tape. Bundle the two dated reads into **one pooled freeze on one span** |
| ⚠ **Run S0 `--verify-venue` on a DAILY cadence** | Ops/scheduling, not a build — the code exists | **NEW 2026-08-07, and it is time-critical in a way nothing else on this list is.** The coverage report's **S3 longest-gap metric cannot detect scattered per-trade loss**: AWS-only max gap over the measured window is **153.1 s** and the merged store carrying 16,459 more trades reads **exactly the same 153.1 s** (density ~1 trade/2.3 s, threshold 300 s). **S0 `--verify-venue` is the intended completeness check** — it diffs the store against Deribit — but it is **opt-in and bounded to the 24 h before `--to`** (`BacktestProgram.vb:298-299`), and that bound is not a design choice: **Deribit's public-trades retention is ~24 h**, so a day's completeness is checkable **only within ~24 h of capture and never again.** Every day it does not run is a day whose tape can never be verified. **It has never been run.** Cheap: one scheduled command per box |
| **Weekday filters — 3 surfaces** | Mechanical; `AutoTweaker` first | From the [weekday-scope ruling](weekday-scope-ruling-2026-08-03.md). Weekday-only is enforced in code in **exactly one place** — verified: only `CoverageReport.vb:346` and CeilingAudit's `CsvFeatureBuilder.vb:199-200`. **`AutoTweaker` is the sharp one** — the only surface that WRITES `settings.json`, so unfiltered it tunes the engine on sessions never traded; **verified never fired** (no tweaker state file, `settings_snapshots/` empty), so it is fixable before it can matter. Then **`LivePerformanceTracker`** — expect *fewer* rendered cells once filtered, since n shrinks against `min_sample_for_render`; note its `DayOfWeek` at `:560` is **Monday week-anchoring, not a filter** — then `AnalysisRunner` / `WhatIfRunner`. **None is a ⚠ boundary** |
| **Atomic writes: swap the partial primitive for a total one** | 5 sites, mechanical, but includes `SettingsLoader.Save` so it wants fixtures | **The framing is the order-app seat's and it is the useful part: a guard is what you OWE when the primitive is PARTIAL. Choosing a TOTAL primitive removes the obligation rather than satisfying it.** `File.Replace` throws on a missing destination, so all five sites need an existence guard; four have one, and `OhlcCache` is safe only via a distant early-out. `File.Move(src, dst, overwrite:=True)` is **total** — works whether or not the destination exists, atomic on the same volume, available on .NET 8. Swapping it in **deletes the hazard class.** **Sites, re-anchored 2026-08-07:** `OhlcCache.vb:99,134` · `SignalEmitter.vb:563` · `Core/Settings/SettingsLoader.vb:346` · `SettingsDiffApplier.vb:352` · `TweakerState.vb:162`. Not a boundary, no settings keys |
| **C1-coverage F1** — trailing-edge gap mis-attribution | Small-medium; one new fixture | `Captured` means "rows present and no gap *ending* in this hour breached", **not** "fully covered": trades :00–:05 then silence until the next hour's :30 charges the gap to the FOLLOWING hour, so the hour it started in reads `Captured` despite ~55 min of silence. Not data loss — the next hour still flags `Defect` — but a reader reasonably takes `Captured` to mean complete. Currently in the report's legend only. ⚠ **The obvious fix false-positives every window's final hour** — bound `hourEnd − lastTradeInHour` against the SAME trailing-evidence boundary `ResolveBoundaryUtc` already computes. `tools/BacktestRunner/CoverageReport.vb` (`AccumulateHourStats`) |
| **CeilingAudit expected-version constant** | One constant | Verified: `tools/CeilingAudit/CeilingAuditProgram.vb:79` reads `expectedVersion As Integer = 59` against a live **v65** — six versions stale. Confirm nothing the audit reads changed v59→v65, then bump. Otherwise the warning is noise that hides a real mismatch |
| **F2** — `ResetBufferState` drops trades in a narrow race | ~4 lines; one `SyncLock` round the whole body (`Monitor` is re-entrant) | Verified still open: `DeribitWsFeed.vb:298`. [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) §2 |
| **F3** — collector's repair calls send `User-Agent: DeribitBacktestRunner/1.0` | one line, cosmetic | Verified still open: `tools/BacktestRunner/HistoricalStore.vb:52` |
| **F3-watch tooling** | Needs cap-bucket segmentation on an offline surface, **or** explicit retirement | The decision half is in §0a |
| **G12** — three manual-content gaps, baked into the regenerated PDFs | `use_best_pivot_candidate` in neither manual · the `MIN NET MOVE %` row label (in `UserManual.md`, **not** `TraderGuide.md`) · **`BacktestRunner` absent from both** | [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) §5 |

---

## 3. Riders — cannot travel alone; attach to the next qualifying event

| Rider | Attaches to |
|---|---|
| ~~`change_log` v64 reversibility wording~~ | ✅ **CONSUMED 2026-08-02 — travelled with D3/v65** |
| **`TriggerMode` CSV column** | The next natural CSV header rotation — **never force one** |
| **Effective-source per-row stamp** (`DeriveWsHealth`) — ruled **J-E** | The same rotation. Until it ships, treat every REST-fallback-sensitive figure as a **bound, not an estimate** |
| **`SettingsVersion` per-row column** | The same rotation, third of the attribution set. **The deploy checklist §4.5 asserted this column already existed; it does not** — `AnalysisLogger.vb`'s header runs `Timestamp` → `SignalId` with `InstanceId,SignalId` as the only attribution fields. A version straddle is **not filterable from the data**, and because `settings.json` hot-reloads it can land **mid-`InstanceId`**. Until it ships: **make every settings-version change coincide with a process restart** so `InstanceId` is a usable proxy, and keep the checklist §5 ledger — now the only place that mapping exists. **Sharpest at a scoring boundary** — v65/D3's armed and unarmed ASIA rows are identical in shape, so a mixed fleet contaminates the D3 watch's own numerator |

---

## 4. Standing watches — periodic, no tick required

liq_events CASCADE ⇒ A4 · §9 STRONG accrual · burst-watch spot-checks · funding calm-week re-read · absorption episode accrual under Path B · **v48 §4a OFI dominance** · **B4b §12** (its F3 arm is blocked — §2) · **pullFrac distribution (W4)**.

**⚠ The D3/v65 ASIA arming watch.** Triggers: **fire rate ≈9.7 %** and **same-side ≥85 %** on ASIA rows. **Read over a MULTI-DAY band, never a single session-day** — ASIA's per-day fire rate spans **4.5–13.8 %** at T=5.5 because row density is only ~106 AggrVel rows/day, so **NY's ±2pp band does not transfer** and no res-3 threshold may be re-fitted off one session-day. Expect ~10.4 fires/day, essentially all through the upgrade arm (**the contra arm is effectively dead on res-3**, 0.21–0.43/day). Accruing since the 2026-08-01 19:02Z restart. **No reading yet.** [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5.

**⭐ Dated trigger — the only watch with a computed date.** The Kelly EST advisory renders *"Actual numbers after next book doubling"* — a **forward promise on screen**. **Trigger: ≥406 pooled weekday STRONG** (double the 201 at the F1 read), **ETA ~2026-08-30** at the measured two-box rate of **12.4 STRONG/weekday**. **Bundle with the W6-4 re-run**, which lands in the same window on its own basis — one pooled freeze, one session, both instruments, and the overfit counter stays honest because they consume the same span. **If the ladder still does not separate, the line must be re-worded or the block suppressed — it must not silently promise another doubling.** ⚠ **Blocked on the §2 copy-back:** as of 2026-08-07 the local book holds **149** weekday STRONG (2026-07-03 → 2026-08-06) and the AWS copy stops 07-31, so **the pooled figure cannot be read at all right now.** [`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md) §2.1.

**Data gates worth watching, with fresh readings 2026-08-07:** **A5 VPFR shape — 28 of 30 distinct calendar dates** (2026-07-03 → 2026-08-06), up from 15 on 07-22, so **~2 dates out**. ⚠ Meeting it does not authorize the build: A5 must still clear the **W6 new-indicator bar** as a VPFR refinement rather than an orthogonal class.

---

## 5. Closed — recorded so they are not re-asked

**2026-08-07:** **three-doc division restated** (roadmap = strategic board · this queue = immediate scoped slice · dependency map = edges, referred to by both) · **`backlog-dependency-map.md` RESTRUCTURED** — its State column removed, because it was a copy of state that lives here and 7 of its cells were stale; it now carries `Blocked by` / `Unblocks` / a **pointer** to where state lives, and moves rows between "live edge" and "opened edge" as its only maintenance action · **`roadmap.md` re-planned** — §2 replaced rather than appended (it had become a 12-entry log dated 07-02…07-22 against a v65 tree), and ⚠ **§6 rule 1 corrected: it named the v52+v53 bundle as the open window**, spent since 07-17, when the open window is the v65/D3 ASIA watch — a *binding* sequencing rule pointing at the wrong boundary reads as "the slot is free," and **D2 is exactly the change that would have taken it** · **new blocker recorded: the AWS copy-back** · **C1-coverage marked BUILT** in both places that still said BUILD-AUTHORIZED.

**2026-08-07 (latest):** ⚠ **D1-a RULED (trader): local capture returns as a SECOND SAMPLER, temporary.** Decision text in `in-app-trade-store-capture-proposal.md` §7 row **D1-a**. **D1's original ground is untouched** — the end state is still AWS-only, and this retires with the local box. What D1 did not price is a sole capturer being *systematically incomplete while healthy*. ⚠ **Bias recorded with the ruling because it binds every tape analysis:** the local box runs during active hours, mostly NY, so **tape completeness will be higher for NY than for ASIA or LONDON**. The CSV book barely moves (AWS-preferred minute-key dedup drops ~99.9 % of overlapping local rows) but **the tape store merges as a plain union with no preference**, so the bias lands there in full. **No tape-derived measure may compare across sessions without accounting for uneven completeness.** Also amended: `aws-collector-deploy-checklist.md` §1a (both boxes now `true`) and its §3 daily glance (**inverted** — the local box no longer shows `+local`). **ID collisions recorded** in `backlog-dependency-map.md` §0a — **F1, F2 and F3 each mean two different things and all are live in this queue simultaneously.**
**2026-08-07 (later):** ⚠ **FIRST AWS STORE COPY-BACK — executed.** Both dated instruments are now readable: **Kelly gate at 237 pooled weekday STRONG** (was 201 at the F1 read; 169 to go, measured rate **9.5/weekday** — *not* the 12.4 the watch assumed, so **ETA slips ~08-30 → ~09-01**). **The `bin\Release` tape is NOT a duplicate** — 16,459 trades existed only there; merged, zero loss (July 115,029→118,144, August 222,246). **The retention rule is what stopped it being deleted.** Two findings queued above: **D1's AWS-only ruling needs a re-read** (sole capturer measured 78.8 % complete while healthy) and **S0 must run daily** (S3 cannot see scattered loss; Deribit's ~24 h retention means a day is checkable once and never again). Procedure written up as [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) **§4b** — it had none, and three traps fired during the run: `BacktestRunner` **sets its own CWD** to the repo root so a staging folder cannot be the target; `sort -u` **with a key** dedups on the key and would have destroyed ~44,000 trades (10,199 timestamps carry >1 trade); a `\r`-stripping filter turns a comparison into a false "0 rows in common". **All three were caught by counting rows** — the 2026-07-31 store-hole lesson, applied.
**2026-08-05:** **C1 trade-store coverage report BUILT**, both sessions, reviewed and ACCEPTED (`c6a7a63` fixes the one review finding) · **tape retention RULED** — keep all tape unless it is a copy; merge first, judge duplication after · **atomic-write total-primitive swap queued** as a build slot · **T8 amendment** to the bridge mirror — an unrecognised enum must render DISTINGUISHABLY · session-start doc cut **37 %** by applying §15's own five-row rule, and three doc headers carrying stale version numbers killed.

**2026-08-03:** **RULED — every spec/brief for a new implementer MUST carry a model + effort recommendation** (binding text in **CLAUDE.md**; worked example [`trade-store-coverage-report-implementer-brief.md`](trade-store-coverage-report-implementer-brief.md) §0). Not a bare tier: it must name *why that tier*, *where that model will specifically slip*, the *escalation trigger*, and a *session split* when the build is large · **C1 FULLY TICKED (D1–D7), BUILD-AUTHORIZED** · **WEEKDAY SCOPE RULED** (correcting J-C): capture stays 24/7, *evaluation* is weekday-only; confirms D4's 300,000 ms, cancels the REST-backfill task, Part B stays unconditional.

**2026-08-02:** **J-B scoping clause RULED** — scope a defect rule by a **positive record of intent**, never a baseline derived from the behaviour being judged (a box that dies permanently converges to its own baseline and reports healthy); **C1 unblocked**, D7=(b) ruled out · **D1 TTM RE-DERIVED then PARKED** · **D3 TICKED + SHIPPED as v65**, ⚠ boundary spent · **CLUSTER A COMPLETE** · **E1 decided** (option c) · **Kelly display shipped `18b1ea8`** and trader-confirmed · deploy checklist **§3b dedup corrected** to AWS-preferred minute-key · this queue's **§0a added**.

**2026-08-01:** **37 commits PUSHED** · **E6 fee knob CONFIRMED** · **E2 W6-4 RUN → INCONCLUSIVE**, no spend meanwhile · **D-cluster sequencing RULED** · **B1 correction** — build-authorized since 07-21, not awaiting a tick · J-A ratified · G7 ASIA derivation.

**2026-07-31:** the "both collectors down" verdict **withdrawn** (date-premise error) · AWS-preferred dedup **ruled** · F1 count gate and W6-1 depth **re-verified** · **W6-1 ruled NO CHANGE** · candle-backfill fixture gap confirmed closed (A51a–e).

---

## 6. Provenance

Assembled 2026-08-01 from: [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) · [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) · [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) · [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) · [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) · [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) · and each spec's own D-table. **Re-swept against the tree 2026-08-07** — findings in §1b.
