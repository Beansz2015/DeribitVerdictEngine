# Trader Tick Queue

**Created:** 2026-08-01 (trader-directed). **Purpose:** the one place that answers *"what is waiting on me, in what order, and what does each one block?"* Derived from the specs and the board — **not a new authority.**

**Authorities unchanged:** [`roadmap.md`](roadmap.md) = execution order · [`backlog-dependency-map.md`](backlog-dependency-map.md) = what blocks what · [`profitability-risk-levers.md`](profitability-risk-levers.md) = profitability lens · each spec's own D-table = the decision text. **This doc carries ORDER and GATES only; where it disagrees with a spec, the spec wins.**

**Maintenance:** update State on every tick. Move closed rows to §5 rather than deleting them — the same convention the board uses. A tick that turns out to need re-deriving goes back to the queue with the reason. **Re-run the §1b shipped-state sweep whenever this queue is rebuilt** — do not inherit a prior assembly.

## 0. Orientation — what each doc is authoritative FOR

Added 2026-08-01 after a Q&A seat reported that seat briefs name superseded docs as current-state authorities. **The failure is not that those docs are wrong — it is that "authority" was never scoped.** Nothing below is demoted; each is pinned to what it actually governs.

| Doc | Authoritative for | **Not** for |
|---|---|---|
| [`trader-tick-queue.md`](trader-tick-queue.md) *(this doc)* | **What is outstanding, in what order, and its gates.** **§0a answers "what is owed" at a glance**; §1b is the dated shipped-state sweep | Dependencies · decision text · execution order of *builds* |
| [`backlog-dependency-map.md`](backlog-dependency-map.md) | **What blocks what** | **Current state** — its cells are dated individually; it is not a snapshot |
| [`roadmap.md`](roadmap.md) | **Execution order** | Current state |
| [`seat-handover-2026-08-02.md`](seat-handover-2026-08-02.md) | **The current seat handover** — verified state with re-check commands, what was ruled, and the conventions | A task list; it points here instead, deliberately |
| [`seat-handover-2026-07-18.md`](seat-handover-2026-07-18.md) §3 | **Standing rules — all still binding**, reaffirmed by the seat-close handover | Current state · its §2 queue is spent |
| [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) | The **2026-07-31 rulings** (J-B/J-C/J-E/D-F) | Its §4 task list and §1 state — **read [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) beside it** |
| each spec's own D-table | **The decision text** — where this queue disagrees, the spec wins | — |
| [`profitability-risk-levers.md`](profitability-risk-levers.md) | The **profitability lens** (L1–L9) | Sequencing |

**Superseded for state, still current for rules:** `seat-handover-2026-07-18.md` (§3 binds), `fable-continuation-2026-07-23.md`. **Neither should be cited for what is outstanding today.**

---

## 0a. Decisions genuinely unanswered — the short list

**Added 2026-08-02 after a fresh seat had to reconstruct this by reading every row, and got one wrong because a row of mine was stale.** §1 records history with strikethrough, so "what is actually owed" is not answerable at a glance. This is. **Verified against the tree on 2026-08-02; re-verify rather than inherit.**

| # | Owed by | Note |
|---|---|---|
| **C1** | trader — **D1–D5 TICKED 2026-08-03; D6–D7 remain** | ⏳ **D6** (`--strict` default; rec (a) opt-in) and **D7** (defect vs switched-off; **(b) ruled out**, and **(a) is now FREE** because D5=(a)'s bump absorbs the marker line). **No data dependency left** — the [weekday-scope ruling](weekday-scope-ruling-2026-08-03.md) retired J-C's weekend clause and confirmed D4's 300,000 ms as already weekday-anchored, cancelling the REST-backfill task. *Previously:* ✅ **UNBLOCKED 2026-08-02** — the J-B clause is ruled ([`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md)). Now a clean tick, with one constraint carried in: **D7 = (b) is ruled out** (reading `trade_store.enabled` gives the flag's value *now*, not during the historical window — and post-overlay a base-file read sees `true` and flags every local up-hour as a defect). **(a) marker line** satisfies the clause; **(c) AWS-only scope** satisfies it honestly but declares scope rather than recording it |
| ~~**D1** TTM `flat_threshold`~~ | ⛔ **PARKED — not owed** | ✅ **RE-DERIVED then PARKED 2026-08-02 (trader).** No build, no boundary opened. **It rides the next scoring boundary that opens for another reason** (the v64 change_log rider precedent). Un-parks on **trades-covered replay**, when the vote can be tested against outcomes instead of counted. `flat_threshold` stays **0.5** and the FLAT band stays inert — recorded and deliberate, not unnoticed. *Detail:* **RE-DERIVED 2026-08-02** → [`ttm-flat-threshold-rederivation-2026-08-02.md`](ttm-flat-threshold-rederivation-2026-08-02.md), D-table **D1-a…D1-d awaiting trader**. **Both prior numbers are discarded** — the 25.0/40.0 ladder (measured: it moves the vote 46.5→35.1 %, away from its own target) **and the 1.45 ratio it was to be rebuilt from** (measured **1.774**, stable in every month, ≈√3; \|histogram\| tested at 1.749, so 1.45 reproduces from nothing). **The real defect is the UNIT:** `delta` tracks volatility, not price — per-month CV is 26 % in USD, 28 % price-normalised, **6.5–11.7 % in ATR multiples** — so an absolute USD constant cannot survive a regime change, and in ATR units the per-resolution ladder dissolves (one k, both resolutions, within ~2 pp). ⚠ **That makes it a CODE change with its own spec and ⚠ boundary, not the settings tick this row implied.** ⚠ **D1-d asks whether to build at all** — the knob is low-leverage (an 11 % FLAT band costs 3.8 pp of vote) and W6-4 says no spend meanwhile; **my read is park the build, bank the finding** |
| **D2** OBV `trend_gate` 18→~23 | trader — ⚠ boundary | Derived and ready, but **not available now.** (i) ⚠ **its D1 bundle-partner is PARKED as of 2026-08-02**, so D2 no longer waits on a D1 re-derivation — it either goes alone at a future boundary or waits for one to open; (ii) ⚠ **corrected 2026-08-02 — it must also wait for the D3 watch to READ.** D2 unblocks cross-category upgrades by making OBV less often directional; D3 upgrades TFI votes on ASIA. Both push the same ASIA path, which is exactly why the ruling separated them — and that confound does not end when D3 ships, it just moves from "same boundary" to "during the watch." Shipping D2 into an open D3 watch corrupts the evidence D3 was sequenced alone to get. *(An earlier edit this session called D2 "next in line because D3's boundary is spent" — that read the ruling too narrowly.)* |
| **E5** absorption **Path B** | trader ticks the path | Coordinator ratified Path B; the path itself is untick​ed |
| **F3 watch** | a tooling decision | Live and **unevaluable** — needs cap-bucket-segmented outcomes, or explicit retirement. Do not leave it live |
| **Weekday filters — 3 surfaces** | a build slot, not a decision | ⚠ **NEW 2026-08-03** from the [weekday-scope ruling](weekday-scope-ruling-2026-08-03.md). Weekday-only is enforced in code in **exactly one place** (`CeilingAudit`); everywhere else it is a hand-applied analyst convention. **`AutoTweaker` is the sharp one** — the only surface that WRITES `settings.json`, so unfiltered it would tune the engine on sessions never traded; it has never fired live, so it is fixable before it can matter. Then **`LivePerformanceTracker`** (the perf strip's 3-day/week windows include Sat+Sun today — expect *fewer* rendered cells once filtered, since n shrinks against `min_sample_for_render`), then `AnalysisRunner` / `WhatIfRunner`. **None is a ⚠ boundary** — none changes scoring |

**Explicitly NOT owed — do not re-raise these:** **D1 TTM is RE-DERIVED and PARKED** (2026-08-02 — the unit is wrong, not the value; fix is ATR-relative k≈0.25–0.30 when it eventually rides a boundary, un-parks on trades-covered replay; **do not re-open it as a settings tick, and do not inherit the retired 25/40 ladder or the unreproducible 1.45 ratio**) · **D3 is TICKED, SHIPPED AND LIVE ON BOTH BOXES** (settings **v65**, pushed; ASIA armed at 5.5 from 2026-08-01 19:02Z — AWS `09c747f8`, local `3916540f`, restarted 1.6 s apart. The ⚠ boundary is spent, its watch is live in §4, and **any pooled ASIA read spanning the boundary must split on the InstanceId ledger** in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a — there is no settings-version column in the CSV) · **E1 Kelly is DECIDED** (2026-08-02, option (c) wait-for-separation + honest display; display shipped `18b1ea8` and confirmed; CAL is *parked*, not pending) · **E6** fee knob confirmed · **E2** W6-4 run · **E3 / E4** approved and built · **A1 / A2 / A3** closed. **E7** (A4) is market-gated and **E8** (L9) is order-app-gated — neither is a decision.

> **Why this section exists, recorded because it is the third instance.** The E1 row said *"needs an explicit trader decision"* for a decision the trader had already made — I wrote the decision into its own doc and never came back to the row. §0 designates **this queue** as the state read, so a stale row here is worse than a stale spec header: it is wrong in the doc everything else points at. **Same shape as B1 and the D1–D7 count.** The rule in §1a applies to this file too.

---

## 1. The queue

### Cluster A — the v64 landing. ✅ **COMPLETE 2026-08-02.** *(Four rows: A1–A3 were one sequence; A2-R is the review that closed the build.)*

| # | Tick | Gate | Where |
|---|---|---|---|
| ~~**A1 · A2 · A2-R · A3**~~ | ~~the v64 landing sequence~~ | ✅ **ALL CLOSED 2026-08-02.** A1 answered (option **c** — the local box avoids capture through the overlay, not a hand-edit, so there is no standing chore and no silent-restore failure mode) · A2 overlay **built** `291457c` · A2-R review findings **F1/F2/D-C fixed** `1611011`, re-reviewed and accepted · A3 **both boxes live**, AWS capturing from 2026-08-01 17:50 UTC (first trade to disk anywhere), local capture correctly OFF, Kelly `[EST]` trader-confirmed. **Outcome detail is in §5; the reasoning is in "how it was decided" directly below.** *(Compressed 2026-08-02: these four rows held ~4,300 chars of superseded "Previously:" status chains in a doc the session-start protocol reads every time. Nothing is lost — the decisions are in the linked spec-backs and in git.)* | [`settings-local-overlay-spec-back.md`](settings-local-overlay-spec-back.md) · [`settings-local-overlay-review-2026-08-02.md`](settings-local-overlay-review-2026-08-02.md) |

**On D7, kept because it is not a permanent close.** D7 was answered 2026-07-31 by the orchestrator seat, option (a): a REST-mode box is expressed by a tracked `settings.json` change with a version bump, because a REST box is break-glass rather than routine and a break-glass change *should* be deliberate and version-visible. Option (c) — excluding a REST box's rows from the pooled book — unlocks once the J-E effective-source stamp gives rows a source column. **D5 was ticked conditional on D1 staying (a), which it did.**

### Cluster A — how it was decided, kept as the record

*Compacted 2026-08-02. Four paragraphs of live-decision narrative sat here — the A1 three-option choice, the "path chosen" block, a pre-build state snapshot, and a "why A2 moved up" note. **All four had been overtaken by the rows above and were actively contradicting them:** they still described `bin\Debug` as pre-v64 with capture unexercised, still said "tick D1–D7", still said "re-read D1 before ticking", and one referenced a non-existent "A5". Replaced with this summary rather than deleted, per the maintenance rule.*

**The sequence, as it ran.** `trade_store.enabled` ships `true` — right for AWS, wrong for the local box under D1's AWS-only ruling — and `settings.json` is `PreserveNewest`, so **the build that tested v64 would have been the build that started local capture.** Three ways were on the table: hand-edit `bin\Debug\settings.json` after each build (the chore, with the silent-restore failure v57 and A2 both exist to remove); accept dual capture and record D1 as softened; or **build the overlay first.** The trader chose the third on 2026-08-01, which turned A2 from a convenience into the critical path.

**The ordering constraint that made it safe**, and it is the reusable part: the overlay is gitignored and lives in `bin\`, so it must be placed **before** the first build carrying a newer tracked `settings.json`. Once it is, `PreserveNewest` can refresh the base and the merge still resolves capture to `false`. Executed exactly that way, and the window in between was harmless because the running binary was pre-v64 and had no capture code at all.

**D1 was re-read before ticking**, as flagged: the [second-pass re-audit](overlay-whitelist-reaudit-2026-07-31.md) rejected `alerts.` from the whitelist (it gates `liq_events.log`, the sole A4 instrument), found **`mtf_gate` — the hard veto — named nowhere** in the enumeration, admitted `performance_display.` only with a recorded reason, corrected the block arithmetic, and added the missing whitelist ∩ UI-writeback fixture. **All of it landed in the build.** The trader ticked **D1–D6** — not D1–D7; D7 had been answered on 2026-07-31.

### Cluster B — ~~ungated correctness~~ ✅ **CLOSED: it was never work. It shipped 2026-07-21.**

**B1 (eval `NO_DATA` / F4) SHIPPED 2026-07-21 as `75a2694`** — N1–N5 built, R1 (WhatIfRunner in the gate build set) and R2/N4 (`RoundStatsBuilder` onto placed geometry) with it. The successor display pass (F2/F3/F12, `4eef0d8`, v55) shipped after it, per N5's sequencing. **Verified live 2026-08-01:** `# schema=v6` in the cache, `analysis_eval_cache.csv.v5.bak` present, **120 `NO_DATA` rows**. Nothing to build, nothing to tick.

**The EV-on-the-strip precondition is therefore already satisfied** — that question is unblocked whenever it is wanted.

### 1a. The convention this produced — verify shipped state before offering work

**This item was carried on the queue as outstanding, ticked by the trader, and an implementer conversation was recommended for it. All three were wrong, and the error survived a correction pass.** The proposal's header read *"PROPOSED — D-table awaits trader"* while §3 recorded the tick; on 2026-08-01 I corrected the header to "BUILD-AUTHORIZED" **from the D-table alone** and never checked the tree. That fixed the wrong half. It surfaced only when reading the code to write the implementer brief.

**Second occurrence of this exact shape.** [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §6.2: *"I listed the eval net-EV rider as an available candidate. It was already shipped (`99cc0dc`) … I checked before building and did not rebuild it, but I should have checked before offering it."* Same failure, same month, and — noting it because it is not a coincidence — **both times the item was an eval-surface fix whose spec header had gone stale.**

**Convention, on the two-occurrence standard this project applies elsewhere (J-A's fixture-mirroring rule):**

> **A spec's status header is not evidence of code state. Before offering any spec as available work, verify it in the tree.**
> One command: `git log --oneline -S'<distinctive-symbol>' -- <file>`. For B1 that was `git log -S'NO_DATA' -- LivePerformanceTracker.vb`, which names the commit immediately.

The cost of skipping it is asymmetric and this case shows both halves: a wasted implementer conversation, and a queue that misrepresents what is actually outstanding to the person deciding what to spend on.

### 1b. Systematic sweep, 2026-08-01 — **4 of 13 rows were wrong, all in the same direction**

Trader-directed after B1. Every outstanding row was checked against the tree rather than against its doc. **Nothing was found in the opposite direction** — no row was more outstanding than stated.

| Row | Queue said | Tree says |
|---|---|---|
| **B1** eval `NO_DATA` | outstanding tick | **SHIPPED** 2026-07-21 `75a2694`, live (v6 cache, 120 `NO_DATA` rows) |
| **E3** D2-v2 candidate mode | tick awaited | **APPROVED + BUILT** 2026-07-29 — wired at `SignalEmitter.vb:324-328` |
| **E4** backtest synthesizer | D1–D8 await trader | **APPROVED + BUILT** 2026-07-30 — `tools/BacktestRunner/`, in active use |
| **E1** F1 → Kelly CAL → P5 | blocked on report mechanics | **Blocker removed** 2026-07-30 — the pooled report runner is built; the §9 read is runnable now |

**Confirmed genuinely outstanding** (checked, not assumed): A1 (trader decision, no code) · **A2** — `SettingsLoader.vb` has no overlay support at all · A3 — `bin\Debug` still pre-v64 · **C1** — no `coverage` verb exists in `BacktestRunner` · **D1** TTM still `0.5` · **D2** OBV still `18.0` · **D3** ASIA threshold still absent ⇒ unarmed · **E5** — no absorption mechanism-revision spec exists (the anchor-rederivation is evidence, not spec) · E7 market-gated · E8 order-app · F2/F3 verified open earlier today.

**G12 is partially closed:** `use_best_pivot_candidate` and `BacktestRunner` are absent from **both** manuals (gaps real); `MIN NET MOVE` now appears in `UserManual.md` but **not** `TraderGuide.md`.

**The root cause, stated once.** This queue was assembled partly from a grep for *"await trader"* across `docs/`. **That grep matches prose and section headings, not state** — and four specs carry stale status text in four different shapes: B1's header contradicted its own §3; E4's §6 heading contradicts its own status line; E3 and E1 were stale on the board rather than in the spec. **Prose is not state.** §1a's convention is the fix; this sweep is what applying it systematically looks like, and it should be re-run whenever this queue is rebuilt rather than trusted from a prior assembly.

### Cluster C — the instrument.

| # | Tick | Gate | Where |
|---|---|---|---|
| **C1** | **Trade-store coverage report — D1–D7** | **After A2** (§5.1) · **needs the J-B scoping clause first** | [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) §9 |

Ruled the **precondition instrument for every data-gated item** — it decides whether future collection gaps are seen at all, and the seat-close handover put it ahead of discretionary builds. **Open correction before it builds:** J-B's *"silence ⇒ defect"* needs a **per-box expected-uptime scoping clause**. Unscoped it classifies most of the local box's existence as defect — local logged 567/392/268 rows on 07-28/29/30 against AWS's 921/921/914, because local is an opportunistic addendum, not a 24/7 collector. Companion clause from the same session: **a trailing interval on a copied file is bounded by the copy time, not by now**, or every AWS copy-back reads as a fresh death.

### Cluster D — the ⚠ boundary contention. **The item that needs a decision, not just a tick.**

| # | Tick | Gate | Where |
|---|---|---|---|
| **D1** | **TTM `flat_threshold` re-anchor (D-A)** | ⚠ **Cannot be ticked as written — needs re-deriving** | [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) §2 |
| **D2** | **OBV `trend_gate` 18 → ~23 (D-B)** | ⚠ Ready; shares a root with D1 | [`candle-store-derivation-batch-spec-back.md`](candle-store-derivation-batch-spec-back.md) §2 |
| ~~**D3**~~ | ~~**ASIA `burst_ratio_threshold` = 5.5**~~ | ✅ **TICKED + SHIPPED 2026-08-02 as settings v65** (local, unpushed — trader tests + pushes). ⚠ **The boundary is spent**: rows after this build carry the burst-modified TFI vote on ASIA. **Not the settings-only change this row implied** — threshold *presence* is the arming mechanism, so the POCO default in `EngineSettings.vb` moved in lockstep (v60 precedent; the harness builds cfgs from `New EngineSettings()`), and **A28c had to be re-pinned**: ASIA *was* the un-armed exemplar, all three shipped sessions are now armed, so the un-armed arm is **constructed** rather than borrowed from a real session. New **A52a** (JSON→arming contract + JSON↔POCO drift guard). No new HC — HC19 already fences `sessions.`; **HC28 stays free**. D-table: [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5. Watch is live in §4 | [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5 |

**Three ⚠ scoring changes, one boundary slot.** The standing rule is one ⚠ change per open window; bundling several at one boundary with trader sign-off is precedented (v52+v53). So this is a **sequencing decision**: bundle all three at one boundary, or serialize three. D-A and D-B were already ruled *"should be ruled together"*; D3 is new from 2026-08-01 and lands in the same contention.

**D1 is on the critical path if you bundle.** Its recommended 25.0/40.0 ladder rests on a mis-identified quantity — `AWARD%` is **not** the vote, because the TTM award block sits under `Case "RELEASING","NONE"` and awards nothing on `ACTIVE` rows (29.2%/24.5%). The real vote rate is already 43.4%/46.3%, so the ladder would push it the wrong way. The ~100× finding itself is untouched and correct.

**RULED 2026-08-01 — two boundaries, not one or three: D3 alone, then D1+D2 bundled once D1 is re-derived.**

- **D3 goes alone and can go first.** It is the only one of the three that **arms** rather than retunes — ASIA has no threshold today, so the aggressor-velocity modifier does not score there at all. An activation deserves its own clean observation window, and D3's is crisp and **session-isolated**: fire rate ≈9.7% and same-side ≈91%, measured on ASIA rows only. It is also ready now, whereas D1 is not.
- **Why not bundle D3 with D2.** They interact on exactly the rows D3's watch reads. D2 (OBV `trend_gate` 18→23) makes OBV less often directional, which **unblocks** cross-category upgrades; D3 **upgrades** TFI votes on ASIA. Both push the ASIA upgrade path the same way, so bundling them confounds the one session whose watch justifies D3.
- **D1+D2 stay bundled** — both global, both the same "restore stated design intent" class, and already ruled *"should be ruled together."* They wait on D1's re-derivation.
- **Honest caveat on D3, new from the W6-4 run:** the ceiling audit's informational column produced the **first outcome-linked read on ASIA aggressor velocity** — `AggrVelBurstRatio` univariate test AUC **0.5179** (n=217), `AggrVelNet` 0.4654. Essentially no demonstrated edge. It does not refute arming (NY and LONDON were armed on distributional grounds too, LONDON's watch passed, and a ±1 modifier on ~10% of rows would not be expected to show strongly in a univariate AUC on 220 test rows) — but it is the only outcome evidence that exists on this knob, and it is neutral at best. **The D-table should say so rather than lead with the distributional case alone.**

### Cluster E — data-gated, other repos, or genuinely later.

| # | Tick | Gate |
|---|---|---|
| **E1** | ⭐ **F1 §9 pooled read → Kelly CAL → P5 tier values** | 📖 **§9 READ DONE 2026-08-01 → THE LADDER DOES NOT SEPARATE.** Gate met (203 pooled STRONG), but MEDIUM 42.2% ≈ WEAK 42.7%, all CIs overlap, and **LONDON inverts at the top** (MEDIUM 53.0% > STRONG 44.8%). **Kelly CAL should not ship on this book** — empirical per-tier rates would size all tiers alike with the ordering partly inverted, which is worse than EST because it carries false authority. ✅ **DECIDED 2026-08-02 (trader): option (c) — WAIT FOR SEPARATION on the measurement, and render the assumption honestly now.** The two halves were split deliberately; only the measurement needs more book. The display half **shipped `18b1ea8` and is trader-confirmed visually**, and the wait is backed by the dated watch in §4 (≥406 pooled weekday STRONG, ETA ~2026-08-30). **Kelly CAL is therefore parked, not pending** — no tick is owed. P5 tier selection still has no demonstrated pooled basis and the LONDON inversion contradicts a STRONG-only policy there, so **P5 values remain gated**. *(History: the "blocked on report mechanics" gate was removed 2026-07-30 — the pooled-file report runner is built, `BacktestRunner report --csv <pooledCsv>`, and its spec-back names the purpose outright: "unblock the F1 §9 read." **Kelly CAL itself remains unbuilt** — `ScoringEngine_Kelly.vb:23` still reads "EST mode only for now (pre-calibration)" — and under the decision above that is the intended state, not a gap.)* [`f1-tier-ladder-read-2026-08-01.md`](f1-tier-ladder-read-2026-08-01.md) |
| ~~**E2**~~ | ~~**W6-4 ceiling audit run**~~ | ✅ **RUN 2026-08-01 → INCONCLUSIVE.** NY×1 ΔAUC −0.0291, CI [−0.197, +0.124] straddling the ±0.030 margin. **The queue does not unlock:** W6-5/B1, the D3–D6 backlog refinements and any W6-7 Tier-C spend stay parked, and §4's instruction is *"re-run at the next book doubling. No spend meanwhile."* [`w6-4-ceiling-audit-run-2026-08-01.md`](w6-4-ceiling-audit-run-2026-08-01.md) |
| ~~**E3**~~ | ~~**D2-v2 what-if candidate mode** — D-table~~ | ✅ **NOT OUTSTANDING — APPROVED 2026-07-29 (D1–D6 ticked) and BUILT.** `use_best_pivot_candidate` is wired into the real arbitration at `SignalEmitter.vb:324-328`. What remains is the **what-if study**, not a tick; live-enable is a later separate ⚠ (the P1 promotion). Folds into the geometry session |
| ~~**E4**~~ | ~~**Backtest synthesizer** — D1–D8~~ | ✅ **NOT OUTSTANDING — APPROVED 2026-07-30, D1–D8 ticked all-as-recommended, BUILD-AUTHORIZED and pulled forward, and BUILT.** `tools/BacktestRunner/` exists and is in active use — `ReplayLoop.vb`, `OverlapValidator.vb`, `BacktestRowWriter.vb`, verbs `fetch`/`replay`/`report` with `--closed-bars`. The D3 closed-bar A/B ran through it, and the ~64,000× unit bug was found *in* it. Its §6 heading still reads *"D-table (await trader)"* — **that heading is the stale artefact, not the status line** |
| **E5** | **Absorption Path B** — tick the path | Activation slips past mid-Aug on any path |
| ~~**E6**~~ | ~~**Fee knob** — `min_net_move_pct`~~ | ✅ **CONFIRMED 2026-08-01 (trader): the 5-bps-net state stands.** Decision-of-record — `trade_costs` keeps `maker_fee_bps 1.5` / `taker_fee_bps 3.5` / `round_trip_style maker_maker` / `min_net_move_pct 0.0005`, composing an effective floor of **0.0008**, unchanged from the pre-fee-change value. **No settings write**, so the v64 change_log rider (§3) does **not** travel here and still awaits a qualifying event |
| **E7** | **A4 liquidation × OFI flip** | Market-gated only (≥1 CASCADE line). **Protect the instrument — see A2's `alerts.` finding** |
| **E8** | **L9 structural-stop un-clamp** | Gated on **L3** (order-app). W6-1 ruled no-change and handed the real question here |

---

## 2. Not ticks — these need a build slot, not a decision

| Item | Size | Where |
|---|---|---|
| **F2** — `ResetBufferState` drops trades in a narrow race | ~4 lines; one `SyncLock` round the whole body (`Monitor` is re-entrant) | [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) §2 |
| **F3** — live collector's repair calls send `User-Agent: DeribitBacktestRunner/1.0` | one line, cosmetic | same |
| **G12** — three manual-content gaps, now baked into the regenerated PDFs | `use_best_pivot_candidate` in neither manual · the `MIN NET MOVE %` row label · **`BacktestRunner` absent from both** | [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) §5 |
| **F3-watch tooling** — the B4b F3 trigger is unevaluable | Needs cap-bucket segmentation on an offline surface, **or** an explicit retirement | [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) §3 |
| **CeilingAudit expected-version constant** — warns `expected 59` against live v64 | One constant; confirm nothing the audit reads changed v59→v64, then bump. Otherwise the warning becomes noise that hides a real mismatch | [`w6-4-ceiling-audit-run-2026-08-01.md`](w6-4-ceiling-audit-run-2026-08-01.md) §4 |

---

## 3. Riders — cannot travel alone; attach to the next qualifying event

| Rider | Attaches to |
|---|---|
| ~~**`change_log` v64 reversibility wording**~~ | ✅ **CONSUMED 2026-08-02 — travelled with D3/v65.** Corrected in both surfaces (`settings.json` change_log + `DeribitIndicatorProject.md` §15): A48f proves the **gate**; the fold's **inertness beyond it** is reasoned, not harness-proven |
| **`TriggerMode` CSV column** | The next natural CSV header rotation — **never force one** |
| **Effective-source per-row stamp** (`DeriveWsHealth`) — ruled **J-E** | The same rotation, alongside `TriggerMode`. Until it ships, treat every REST-fallback-sensitive figure as a **bound, not an estimate** |
| **`SettingsVersion` per-row column** — NEW 2026-08-02 | The same rotation, third of the attribution set. **The deploy checklist §4.5 asserted this column already existed; it does not** — verified in `AnalysisLogger.vb`, whose header runs `Timestamp` → `SignalId` with `InstanceId,SignalId` as the only attribution fields. So a version straddle across the two boxes is **not filterable from the data**, and because `settings.json` hot-reloads it can even land **mid-`InstanceId`** — same instance, different scoring, no marker. Until it ships: **make every settings-version change coincide with a process restart** so `InstanceId` becomes a usable proxy, and keep the checklist §5 version↔instance ledger, which is now the *only* place that mapping exists. **Sharpest at a scoring boundary** — v65/D3's armed and unarmed ASIA rows are identical in shape, so a mixed fleet contaminates the D3 watch's own numerator |

---

## 4. Standing watches — periodic, no tick required

liq_events CASCADE ⇒ A4 · §9 STRONG accrual · burst-watch spot-checks · funding calm-week re-read · absorption episode accrual under Path B · **v48 §4a OFI dominance** · **B4b §12** (its F3 arm is blocked — §2) · **pullFrac distribution (W4)**. The last three were dropped from the seat-close handover's periodic list and are restored here.

**⚠ NEW 2026-08-02 — the D3/v65 ASIA arming watch.** Trigger values: **fire rate ≈9.7 %** and **same-side ≥85 %** on ASIA rows. **Read over a MULTI-DAY band, never a single session-day** — ASIA's per-day fire rate spans **4.5–13.8 %** at T=5.5 because row density is only ~106 AggrVel rows/day, so **NY's ±2pp band does not transfer** and no res-3 threshold may be re-fitted off one session-day. Expect ~10.4 fires/day, essentially all through the upgrade arm (**the contra arm is effectively dead on res-3** — 0.21–0.43/day — so this modifier is in practice upgrade-only there). Starts accruing from the trader's post-push build. [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5.

**⭐ Dated trigger — the only watch here with a computed date, added 2026-08-02.** The Kelly EST advisory now renders *"Actual numbers after next book doubling"*, which is a **forward promise on screen**. **Trigger: ≥406 pooled weekday STRONG** (double the 201 at the F1 read), **ETA ~2026-08-30** at the measured two-box rate of **12.4 STRONG/weekday** (the whole-book rate of 10.1 understates it — it averages in the single-box era before AWS came up 2026-07-22). **Bundle with the W6-4 re-run**, which lands in the same window on its own basis (2,712 eligible rows) — one pooled freeze, one session, both instruments, and the overfit counter stays honest because they consume the same span. **If the ladder still does not separate, the line must be re-worded or the block suppressed — it must not silently promise another doubling.** [`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md) §2.1.

---

## 5. Closed — recorded so they are not re-asked

**2026-08-02 (latest):** **J-B scoping clause RULED** — scope by positive record, never by an uptime baseline; **C1 unblocked**, and D7=(b) ruled out · **D1 TTM RE-DERIVED then PARKED** — the unit is wrong rather than the value (`delta` tracks volatility: CV 26 % in USD vs 6.5–11.7 % in ATR multiples), both prior numbers retired, no boundary opened, rides the next one · **session-start doc read cut 37 %** (337,595 → 214,283 chars) by applying §15's own five-row rule and killing three headers that carried stale version numbers.
**2026-08-02 (later):** **D3 TICKED + SHIPPED as settings v65** — ASIA `burst_ratio_threshold` = 5.5, the ⚠ boundary spent, D-table written with the AUC 0.5179 caveat the sequencing ruling required · the **v64 `change_log` reversibility rider CONSUMED** on the same write · `session_block_semantic` **re-classified** from "dead key" to a shipped reserved key whose removal needs a version bump (handover §4 corrected in place) · **D2 is now the next ⚠ in line**, still waiting on D1's re-derivation.
**2026-08-02:** **CLUSTER A COMPLETE** — A1 answered (option c) · A2 overlay **built** `291457c`, reviewed, **F1/F2/D-C fixed** `1611011`, re-reviewed and accepted · A3 landed: both boxes live on v64, **AWS capturing since 17:50 UTC** (first observation anywhere of a trade reaching disk), local capture correctly OFF via the overlay · **Kelly display shipped `18b1ea8` and trader-confirmed** · **E1 decided** (option c, wait for separation) · deploy checklist **§3b dedup corrected** to AWS-preferred minute-key, superseding the 2026-07-29 rule on both axes · new InstanceIds recorded (AWS `5a3afd99`, local `2f8c9fe1`) · this queue's **§0a added** and its **Cluster A narrative compacted** after it began contradicting its own rows.
**2026-08-01:** **37 commits PUSHED** (clean vs origin) — but the v64 **runtime** test is still outstanding, see A3 · **E6 fee knob CONFIRMED** (5-bps net stands, decision-of-record) · **E2 W6-4 ceiling audit RUN → INCONCLUSIVE**, queue does not unlock, no spend meanwhile · **D-cluster sequencing RULED** (D3 alone first; D1+D2 bundled after D1's re-derivation) · **B1 correction** — it was build-authorized since 2026-07-21, not awaiting a tick; stale header fixed · J-A ratified (A48f — both readings correct about different claims) · G7 ASIA derivation (gate met, T=5.5 recommended) · v64 review F1/F4/F5 confirmed resolved.
**2026-07-31:** the "both collectors down" verdict **withdrawn** (date-premise error) · AWS-preferred dedup **ruled** · F1 count gate and W6-1 depth **re-verified** · **G1/G2** (D1 widened to full VWAP clearance; the void D2 task struck) · **G3/G4** (J-D ratified and extended; whitelist re-audited) · **G5** (D-C board edge) · **G6** (W6-1 ruled no-change) · **D-C/D-D/D-E** · candle-backfill fixture gap confirmed closed (A51a–e).

---

## 6. Provenance

Assembled 2026-08-01 from: [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) (12 omissions from the seat-close handover) · [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) · [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) · [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) · [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) · [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) · and each spec's own D-table.
