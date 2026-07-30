# Handover to the Fable seat — 2026-07-31

**From:** the Opus orchestrator that ran the pre-Aug-1 batch and everything after it.
**For:** the Friday session. **Read §1 first — one finding invalidates a ruling you already made.**
**Budget context:** the trader has ~9 % Fable left, reserved for Friday + the seat-handover doc. This document exists so you spend none of it re-deriving what is already settled. **Everything below is done and committed; nothing here needs a ruling unless §5 says so.**

State: **25 commits ahead of origin** at time of writing (20 pushed earlier, then more). Settings **v63**, untouched by every lane. Harness **A1–A47b** green. Fixture families consumed through **A47**; next free **A48** (reserved by the approved spec in §4). Next free hard constraint **HC27** (also reserved by that spec).

---

## 1. THE THING THAT CHANGES YOUR RULINGS — a ~64,000× unit bug

**D2 is void. So was my counter-argument to it. There was never a tolerance question.**

Running D3's ordered closed-bar A/B produced an impossible first result — the *stub* arm carrying more volume than the *closed-bar* arm. Cause: `ReplayLoop.BuildFormingStub` summed `TradeRecord.Amount` straight into `Candle.Volume`. On Deribit perpetuals `Amount` is **USD notional**; `Candle.Volume` is **BTC**. Store evidence: mean 1m candle Volume **2.3937**, mean trade Amount **2909.10**.

Fixed (`ae8a1f6`, tools-only). Effect on synthetic↔live agreement, same 840 rows, only the stub arithmetic changed:

| Column | before | after |
|---|---:|---:|
| **VWAP** | 56.19 % | **100.00 %** |
| VWAP σ bands | 53–55 % | 99.76–100.00 % |
| VolumeRatio | 23.57 % | 65.00 % |
| **OBVTrend / OBVDiv** | 71.43 / 84.17 % | **99.76 / 99.52 %** |
| **Verdict / Tier** | 74.05 / 81.43 % | **79.64 / 86.19 %** |
| ATR / ADX / RSI | 46.6 / 49.8 / 43.0 % | unchanged |

**What this does to your five rulings:**

- **D2 — do NOT spec the bps-scale tolerance reclass.** `NumTight` was correct throughout. Both of us were wrong in the same direction: we each assumed the inputs were sound and argued about how to score them. Neither checked the inputs.
- **D1 — can widen to full clearance on evidence.** The fine-sweep withholding rested on VWAP values being untrustworthy; they now agree **exactly** on all 840 rows. This should be a one-line confirmation, not a discussion. **The single cheapest use of your remaining budget.**
- **D3 — unaffected and progressing.** Its first ordered evidence step is done (§2).
- **D4 — done** (you added the §15 row yourself; I verified rather than duplicated).
- **D5 — recorded** in the summary ahead of the lane-E tables.

Also resolved: the §9.5 OBV regression, previously blamed on the stub's *near-zero* volume. The cause was its *oversized* volume.

**Why A43f missed it:** it verified the stub's *internal* arithmetic against hand-computed sums and passed, correctly, while the units were wrong. Internal consistency cannot detect a unit error. New **A47b** checks against real store scale instead — a 2-second stub must be a fraction of a one-minute bar, never a multiple. Pre-fix that read 9000 vs 2.4.

---

## 2. D3 evidence lane — first step complete

`docs/d3-closed-bar-volume-ab-2026-07-31.md`. Two replay arms, one variable, window length held constant, A47a pinning the shape.

| | stub (live mirror) | closed bars | live actual |
|---|---:|---:|---:|
| p50 VolumeRatio | 0.0000 | 0.6001 | 0.0123 |
| VR ≥ 3.0 (the 3× breakout gate) | 0.00 % | **6.19 %** | 0.51 % |
| directional verdicts | 25.7 % | **26.0 %** | — |

**Closed bars raise volume-signal engagement ~12–17× and move the directional share by 0.3 pp.** Independently reproduces lane C's counterfactual by a different route.

**Outcomes were attempted, not skipped, and are underpowered by ~25×.** Both arms went through the offline report: STRONG n=6 vs 7, every band's CI overlapping. Detecting a ~6 pp difference needs ~1,100 directional rows per arm against the 44/36 available. **Nothing but more trades-covered window closes this** — not a better estimator, not more analysis. That is calendar-gated, which is what §4 is about.

**No recommendation made.** Live change remains its own maximal-⚠ D-table after F1/Kelly-CAL, per your ruling.

---

## 3. What else shipped, briefly

Everything gated, all `[no-engine-change]` or tools/docs-only, settings never bumped.

- **Batch lanes A–E** — all five ran; see `pre-aug1-batch-summary.md` (**§0 is the unit bug, placed ahead of §A because it changes how §A reads**) and `pre-aug1-batch-spec-back.md` (§7 is my response to your rulings; §7.1–§7.2c retain the D2 root-cause chain and four eliminated hypotheses for audit, all now superseded by §1 above).
- **AWS redeployed to v63** — 16:42 UTC, local confirmed 16:43. **The knob was deliberately NOT turned**: `min_net_move_pct` stays 0.0005, composed floor 0.0008 on both boxes. So **no dataset boundary**, rows across the restart are fully comparable, and the v61/v63 straddle is now closed structurally rather than by coincidence. Runbook with the executed record: `aws-redeploy-and-fee-knob-runbook-2026-07-31.md`.
- **The Aug-1 fee change needs no action.** Fees already carry post-Aug-1 values (set at v62). The trader knowingly keeps 5 bps net rather than 8.
- **6-month candle+funding store fetched** — 259,974 1m bars back to February, plus 3m/5m/15m and funding. Trades appended 19,529 rows; older months correctly returned 0 (the ~24 h retention cap).
- **Net-of-fees R:R rider built** (`de27b40`) — the last open item in the fee proposal §6, trader-ticked. One line in the KELLY SIZING block. Parity satisfied *by construction*: one composer called by both surfaces.
- **Manual PDFs regenerated** — both were behind, not just UserManual. Three content gaps flagged and deliberately unfilled (§5).
- **Convention captured** — `batch-review-packet-convention.md`, per your note that the packet shape should be copied. CLAUDE.md points at it.
- **ATR bands re-verified against 6 months** — p25 19.35 / p75 53.95 against bands of 20 / 55, splitting Low 26.3 % / Normal 49.5 % / High 24.1 %. **The v37 bands are still right; no action.**

---

## 4. Queued work, with owners

### 4.1 Approved and implementer-ready — needs no Fable time

**`in-app-trade-store-capture-proposal.md` — APPROVED, D1–D5 ticked.** Streaming trade capture in `ApplyTrades` (the app already receives every trade, so it is a write not a fetch) plus in-app gap repair. Settings v63→v64, HC27, fixtures A48a–h.

**D1 was ruled against my recommendation** and the reason is worth carrying: I argued dual-box redundancy; the trader's ground is that **the end goal is AWS-only operation**, so dual capture builds for a topology being retired. §7.1 records what that makes binding — gap repair must fire *once on start*, since an AWS restart is precisely when a gap exists.

### 4.2 The 6-month store unblocks a specific set — Opus derivation work

The dividing line is what a study *consumes*. Candle/volume-derived work is unblocked **now**; anything needing verdicts or outcomes still needs logged rows or trades.

| Item | Status |
|---|---|
| §12 **3-min weekday-ASIA `session_volume` re-verify** | **Directly unblocked** — that row explicitly says it *"bundles the offline Deribit volume re-fetch"*, which now exists. Includes its OBV `\|obvChange\|` median re-anchor and the ASIA/LONDON clamp-binding check. |
| §12 **TTM `flat_threshold`** | Unblocked — wants the 1m candle-range distribution. |
| §12 **swing pivot wing/lookback** | Unblocked — candle-derived. |
| §12 **session volume multipliers** | Unblocked. |
| Failure matrix / band ladder / geometry EV | **Not helped.** What-If replays *logged rows*; `BacktestRunner replay` still needs trades. |

**Shape:** each is a derivation (Opus) producing a recommended threshold, then a settings pass that needs a ruling. **Do not spend Friday budget running them** — hand them to an implementer/orchestrator and rule on the outputs later.

### 4.3 Trader-owned

Schedule the append-forward fetch (superseded in practice by §4.1 once built) · capture the new AWS `InstanceId` at next copy-back — easiest from `ws_health.log`, format `utc | state | iid`, the last line after the 16:42 restart.

---

## 5. What actually wants your Friday budget

Ranked. Everything else above is settled or delegable.

1. **The geometry read (lane E).** Your own D5 already frames it: full-book tables are **context-only** (07-08 regime break dominates), **post-07-08 is the decision surface**, and its winner being the live baseline with no flags means **"no separable geometry change yet" is a legitimate outcome**. Raw tables in `pre-aug1-batch-summary.md` §E; overlays committed so it reproduces.
2. **D1's widening confirmation** — one line, given §1.
3. **The seat-handover doc** — the protected item.

**Optional, only if budget survives:** the three manual-content gaps the PDF lane found and deliberately did not fill — v63's `use_best_pivot_candidate` is in neither manual; the `MIN NET MOVE % (after fees)` row *label* never appears even though its model is documented at four sites (so it can't be grepped from what's on screen); and **`BacktestRunner` appears nowhere in either manual**, now a whole-tool gap given it has a `report` verb and a `--closed-bars` flag.

**Deliberately NOT queued for you:** the D3 live change (evidence-first, after F1/Kelly-CAL), the `TriggerMode` column (rides the next natural rotation, never forces one), Tardis (declined on cost — ~$350–700/mo subscription, and the retention question is undocumented).

---

## 6. Two things I got wrong, recorded plainly

1. **The D2 tolerance hypothesis was mine**, you confirmed it, and it was wrong. Neither of us checked whether the inputs were sound. The lesson is cheap to state and worth keeping: when two seats agree on how to *score* a discrepancy, that is exactly when someone should verify the *inputs*.
2. **I listed the eval net-EV rider as an available candidate.** It was already shipped (`99cc0dc`, A41a–d, coordinator-approved `e2e1b41`). I checked before building and did not rebuild it, but I should have checked before offering it.
