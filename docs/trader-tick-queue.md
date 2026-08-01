# Trader Tick Queue

**Created:** 2026-08-01 (trader-directed). **Purpose:** the one place that answers *"what is waiting on me, in what order, and what does each one block?"* Derived from the specs and the board — **not a new authority.**

**Authorities unchanged:** [`roadmap.md`](roadmap.md) = execution order · [`backlog-dependency-map.md`](backlog-dependency-map.md) = what blocks what · [`profitability-risk-levers.md`](profitability-risk-levers.md) = profitability lens · each spec's own D-table = the decision text. **This doc carries ORDER and GATES only; where it disagrees with a spec, the spec wins.**

**Maintenance:** update State on every tick. Move closed rows to §5 rather than deleting them — the same convention the board uses. A tick that turns out to need re-deriving goes back to the queue with the reason.

---

## 1. The queue

### Cluster A — the v64 landing. **These three are one sequence, not three independent items.**

| # | Tick | Gate | Where |
|---|---|---|---|
| **A1** | **F6 — how does the local box avoid capturing?** | **Ungated. Precondition on A3.** | [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) §3 |
| **A2** | **`settings.local.json` overlay — D1–D7** | Ungated. **Re-read D1 first** | [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md) §7 |
| **A3** | **v64 test** — *push done, runtime test outstanding* | **Blocked on A1** | the test gate |

**A3 status 2026-08-01: pushed ✅, tested ❌.** All 37 commits are on origin (`master...origin/master`, clean). But **no rebuild has occurred** — `bin\Debug` still carries the pre-v64 dll (2026-07-30 17:45 UTC) and **v63 settings with no `trade_store` block**, and `backtest_data\` is absent. So the v64 *runtime* test — trades actually landing on disk from the WS stream, first flush, first gap-repair call — is still entirely unexercised, and it is the one the [v64 review §5](trade-store-capture-review-2026-07-31.md) says matters most. **The upside: F6 has not fired, so A1 can still be decided before the test build, exactly as this cluster intends.**

> **PATH CHOSEN 2026-08-01 (trader): option (c) — build A2 first, then run A3 through it.** So the order is **tick A2's D1–D7 → build A2 → create `settings.local.json` in `bin\Debug\net8.0-windows\` with `{"trade_store":{"enabled":false}}` → *then* build+run the v64 test.** Choosing (c) does not skip A2's tick; it makes that tick the thing standing in front of everything else.
>
> **Why the order is safe:** the overlay file is gitignored and lives in `bin\`, so it must be placed **before the first post-overlay Debug build**. Once it is, `PreserveNewest` can copy the v64 tracked settings into `bin\` and the merge still resolves `trade_store.enabled` to false, so capture never starts locally. The window in between is harmless — the currently-running binary is pre-v64 and has no capture code at all.
>
> **Re-read D1 before ticking** — it changed materially on 2026-07-31 ([re-audit](overlay-whitelist-reaudit-2026-07-31.md)): `alerts.` must be **rejected** (it gates `liq_events.log`, the sole A4 instrument), **`mtf_gate` is named nowhere** in the enumeration despite being the hard veto, `performance_display.` is admitted only with a caveat, the block arithmetic is wrong, and §1.2 lists two of the four live-save paths — so **whitelist ∩ UI-writeback needs a fixture** that A50c does not provide.

**A1 in one paragraph.** `trade_store.enabled` ships `true`, which is right for AWS and wrong for the local box — D1 ruled **AWS-only**. It hasn't bitten yet: `bin\Debug\settings.json` is still **v63 with no `trade_store` block** and the exe is pre-v64, so nothing is capturing locally. But `settings.json` is `PreserveNewest` and the tracked file is already newer, so **the build that tests v64 is the build that starts local capture** — ~900 MB/year into a directory nobody watches. Three ways: (a) set `enabled:false` in `bin\Debug\settings.json` after the test build — the manual chore, with the silent-restore failure mode v57 and A2 both exist to remove; (b) accept dual capture and record that D1 was softened in practice; (c) **build A2 first**, which is the tidy one — the overlay's own header names the F6 ruling as its origin.

**Why A2 moved up.** It was a convenience item. F6 makes it the clean way to run the v64 test, and §5.1 means building it **before** A5 saves rework on that spec's D7 (which must read the *merged* value once an overlay exists). **Re-read D1 before ticking** — the [second-pass re-audit](overlay-whitelist-reaudit-2026-07-31.md) rejects `alerts.` from the whitelist (it gates `liq_events.log`, the sole A4 instrument) and found `mtf_gate` — the hard veto — named nowhere in the enumeration.

### Cluster B — ~~ungated correctness~~ ✅ **CLOSED: it was never work. It shipped 2026-07-21.**

**B1 (eval `NO_DATA` / F4) SHIPPED 2026-07-21 as `75a2694`** — N1–N5 built, R1 (WhatIfRunner in the gate build set) and R2/N4 (`RoundStatsBuilder` onto placed geometry) with it. The successor display pass (F2/F3/F12, `4eef0d8`, v55) shipped after it, per N5's sequencing. **Verified live 2026-08-01:** `# schema=v6` in the cache, `analysis_eval_cache.csv.v5.bak` present, **120 `NO_DATA` rows**. Nothing to build, nothing to tick.

**The EV-on-the-strip precondition is therefore already satisfied** — that question is unblocked whenever it is wanted.

### 2a. The convention this produced — verify shipped state before offering work

**This item was carried on the queue as outstanding, ticked by the trader, and an implementer conversation was recommended for it. All three were wrong, and the error survived a correction pass.** The proposal's header read *"PROPOSED — D-table awaits trader"* while §3 recorded the tick; on 2026-08-01 I corrected the header to "BUILD-AUTHORIZED" **from the D-table alone** and never checked the tree. That fixed the wrong half. It surfaced only when reading the code to write the implementer brief.

**Second occurrence of this exact shape.** [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §6.2: *"I listed the eval net-EV rider as an available candidate. It was already shipped (`99cc0dc`) … I checked before building and did not rebuild it, but I should have checked before offering it."* Same failure, same month, and — noting it because it is not a coincidence — **both times the item was an eval-surface fix whose spec header had gone stale.**

**Convention, on the two-occurrence standard this project applies elsewhere (J-A's fixture-mirroring rule):**

> **A spec's status header is not evidence of code state. Before offering any spec as available work, verify it in the tree.**
> One command: `git log --oneline -S'<distinctive-symbol>' -- <file>`. For B1 that was `git log -S'NO_DATA' -- LivePerformanceTracker.vb`, which names the commit immediately.

The cost of skipping it is asymmetric and this case shows both halves: a wasted implementer conversation, and a queue that misrepresents what is actually outstanding to the person deciding what to spend on.

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
| **D3** | **ASIA `burst_ratio_threshold` = 5.5** | ⚠ Ready; data gate **met** (323 fires vs ~150). **Ship SEPARATELY — see below** | [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) |

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
| **E1** | F1 report mechanics → **Kelly CAL** → **P5 tier values** | Count gate GO (201 pooled weekday STRONG, re-verified). Blocked on the pooled-book report-runner decision; inputs inherit **B1** |
| ~~**E2**~~ | ~~**W6-4 ceiling audit run**~~ | ✅ **RUN 2026-08-01 → INCONCLUSIVE.** NY×1 ΔAUC −0.0291, CI [−0.197, +0.124] straddling the ±0.030 margin. **The queue does not unlock:** W6-5/B1, the D3–D6 backlog refinements and any W6-7 Tier-C spend stay parked, and §4's instruction is *"re-run at the next book doubling. No spend meanwhile."* [`w6-4-ceiling-audit-run-2026-08-01.md`](w6-4-ceiling-audit-run-2026-08-01.md) |
| **E3** | **D2-v2 what-if candidate mode** — D-table | Wants ticking **before** the geometry session, since v63 built `use_best_pivot_candidate` to make exactly that testable |
| **E4** | **Backtest synthesizer** — D1–D8 | Build sequenced behind the absorption mechanism spec |
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
| **`change_log` v64 reversibility wording** — "harness-proven" overstates it; the **gate** is proven, the **fold's inertness** is reasoned | The next tick that touches `settings.json` (likely **D3** or **E6**). A change_log-only diff would trip the version-bump guard with no key change to justify a bump |
| **`TriggerMode` CSV column** | The next natural CSV header rotation — **never force one** |
| **Effective-source per-row stamp** (`DeriveWsHealth`) — ruled **J-E** | The same rotation, alongside `TriggerMode`. Until it ships, treat every REST-fallback-sensitive figure as a **bound, not an estimate** |

---

## 4. Standing watches — periodic, no tick required

liq_events CASCADE ⇒ A4 · §9 STRONG accrual · burst-watch spot-checks · funding calm-week re-read · absorption episode accrual under Path B · **v48 §4a OFI dominance** · **B4b §12** (its F3 arm is blocked — §2) · **pullFrac distribution (W4)**. The last three were dropped from the seat-close handover's periodic list and are restored here.

---

## 5. Closed — recorded so they are not re-asked

**2026-08-01:** **37 commits PUSHED** (clean vs origin) — but the v64 **runtime** test is still outstanding, see A3 · **E6 fee knob CONFIRMED** (5-bps net stands, decision-of-record) · **E2 W6-4 ceiling audit RUN → INCONCLUSIVE**, queue does not unlock, no spend meanwhile · **D-cluster sequencing RULED** (D3 alone first; D1+D2 bundled after D1's re-derivation) · **B1 correction** — it was build-authorized since 2026-07-21, not awaiting a tick; stale header fixed · J-A ratified (A48f — both readings correct about different claims) · G7 ASIA derivation (gate met, T=5.5 recommended) · v64 review F1/F4/F5 confirmed resolved.
**2026-07-31:** the "both collectors down" verdict **withdrawn** (date-premise error) · AWS-preferred dedup **ruled** · F1 count gate and W6-1 depth **re-verified** · **G1/G2** (D1 widened to full VWAP clearance; the void D2 task struck) · **G3/G4** (J-D ratified and extended; whitelist re-audited) · **G5** (D-C board edge) · **G6** (W6-1 ruled no-change) · **D-C/D-D/D-E** · candle-backfill fixture gap confirmed closed (A51a–e).

---

## 6. Provenance

Assembled 2026-08-01 from: [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) (12 omissions from the seat-close handover) · [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) · [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) · [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) · [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) · [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) · and each spec's own D-table.
