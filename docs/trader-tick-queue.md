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
| **A3** | **v64 test + push** (36 commits local) | **Blocked on A1** | the compile/test gate |

**A1 in one paragraph.** `trade_store.enabled` ships `true`, which is right for AWS and wrong for the local box — D1 ruled **AWS-only**. It hasn't bitten yet: `bin\Debug\settings.json` is still **v63 with no `trade_store` block** and the exe is pre-v64, so nothing is capturing locally. But `settings.json` is `PreserveNewest` and the tracked file is already newer, so **the build that tests v64 is the build that starts local capture** — ~900 MB/year into a directory nobody watches. Three ways: (a) set `enabled:false` in `bin\Debug\settings.json` after the test build — the manual chore, with the silent-restore failure mode v57 and A2 both exist to remove; (b) accept dual capture and record that D1 was softened in practice; (c) **build A2 first**, which is the tidy one — the overlay's own header names the F6 ruling as its origin.

**Why A2 moved up.** It was a convenience item. F6 makes it the clean way to run the v64 test, and §5.1 means building it **before** A5 saves rework on that spec's D7 (which must read the *merged* value once an overlay exists). **Re-read D1 before ticking** — the [second-pass re-audit](overlay-whitelist-reaudit-2026-07-31.md) rejects `alerts.` from the whitelist (it gates `liq_events.log`, the sole A4 instrument) and found `mtf_gate` — the hard veto — named nowhere in the enumeration.

### Cluster B — ungated correctness. Parallel lane; does not compete with A.

| # | Tick | Gate | Where |
|---|---|---|---|
| **B1** | **Eval `NO_DATA` (F4)** | **Ungated, and it should be early** | [`eval-no-data-outcome-proposal.md`](eval-no-data-outcome-proposal.md) |

Self-declared *"FIRST of the F-series"* and correctly so: `EvaluateEntry` records an empty bar-list as `WINDOW_EXPIRED` (a failure) while the offline `FailureRateMatrix` excludes the same condition — so **live rates bias downward, invisibly** (proven instance: 2026-07-03 NY, 22/22 fabricated expiries). Every measurement inherits it — F1's re-read, the W6 audits, Kelly CAL inputs, and any future EV-on-the-strip work. Zero scoring impact, no boundary, no settings keys.

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
| **D3** | **ASIA `burst_ratio_threshold` = 5.5** | ⚠ Ready; data gate **met** (323 fires vs ~150) | [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) |

**Three ⚠ scoring changes, one boundary slot.** The standing rule is one ⚠ change per open window; bundling several at one boundary with trader sign-off is precedented (v52+v53). So this is a **sequencing decision**: bundle all three at one boundary, or serialize three. D-A and D-B were already ruled *"should be ruled together"*; D3 is new from 2026-08-01 and lands in the same contention.

**D1 is on the critical path if you bundle.** Its recommended 25.0/40.0 ladder rests on a mis-identified quantity — `AWARD%` is **not** the vote, because the TTM award block sits under `Case "RELEASING","NONE"` and awards nothing on `ACTIVE` rows (29.2%/24.5%). The real vote rate is already 43.4%/46.3%, so the ladder would push it the wrong way. The ~100× finding itself is untouched and correct.

### Cluster E — data-gated, other repos, or genuinely later.

| # | Tick | Gate |
|---|---|---|
| **E1** | F1 report mechanics → **Kelly CAL** → **P5 tier values** | Count gate GO (201 pooled weekday STRONG, re-verified). Blocked on the pooled-book report-runner decision; inputs inherit **B1** |
| **E2** | **W6-4 ceiling audit run** | Data gate was "early Aug" — that is now |
| **E3** | **D2-v2 what-if candidate mode** — D-table | Wants ticking **before** the geometry session, since v63 built `use_best_pivot_candidate` to make exactly that testable |
| **E4** | **Backtest synthesizer** — D1–D8 | Build sequenced behind the absorption mechanism spec |
| **E5** | **Absorption Path B** — tick the path | Activation slips past mid-Aug on any path |
| **E6** | **Fee knob** — `min_net_move_pct` | Deadline passed 2026-08-01. Current 5-bps-net state is a deliberate choice — **confirm or revisit**, not a blocker |
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

**2026-08-01:** J-A ratified (A48f — both readings correct about different claims) · G7 ASIA derivation (gate met, T=5.5 recommended) · v64 review F1/F4/F5 confirmed resolved.
**2026-07-31:** the "both collectors down" verdict **withdrawn** (date-premise error) · AWS-preferred dedup **ruled** · F1 count gate and W6-1 depth **re-verified** · **G1/G2** (D1 widened to full VWAP clearance; the void D2 task struck) · **G3/G4** (J-D ratified and extended; whitelist re-audited) · **G5** (D-C board edge) · **G6** (W6-1 ruled no-change) · **D-C/D-D/D-E** · candle-backfill fixture gap confirmed closed (A51a–e).

---

## 6. Provenance

Assembled 2026-08-01 from: [`seat-close-handover-gap-audit-2026-07-31.md`](seat-close-handover-gap-audit-2026-07-31.md) (12 omissions from the seat-close handover) · [`job1-outstanding-2026-08-01.md`](job1-outstanding-2026-08-01.md) · [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) · [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) · [`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) · [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) · and each spec's own D-table.
