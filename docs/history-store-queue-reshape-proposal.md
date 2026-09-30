# History store — what it drops or reshapes in the queue and roadmap — PROPOSAL

> ✅ **RULED 2026-09-29 (trader): `HSR-1`–`HSR-13` accepted AS EVALUATED** in [`history-store-queue-reshape-evaluation.md`](history-store-queue-reshape-evaluation.md). That file's conditions and narrowings win over this proposal's rows; `HSR-13` is the CASCADE-watch item it added. Written into the docs the same day (index row in `trader-tick-queue.md` §2).

**Status:** proposal, 2026-09-29 (UTC), for evaluation. The trader is passing it to the previous orchestrator seat, which wrote [`history-data-store-spec.md`](history-data-store-spec.md) and holds its full context. The reply comes back to this seat.
**Asked by the trader 2026-09-29:** now that the history store will supply historical trades, which items in [`trader-tick-queue.md`](trader-tick-queue.md) and [`roadmap.md`](roadmap.md) should be dropped or changed?
**Class:** documentation only. It moves no code, no settings key and no scoring. Nothing here is actioned before the rulings come back. The re-gated reads run after history-store stage 2b (the backfill), which is after 2026-11-25.
**Decision IDs:** `HSR-1` to `HSR-12` are new (checked free across the repo 2026-09-29).

**For the evaluating seat:**
- Model: Opus 5.5 · Effort: medium.
- Why that tier: each row is a claim about another document's premise, and the check is to read that premise. No derivation.
- Where it can slip: accepting a re-gate whose outcome half still needs logged verdicts. §1 below states that line; hold every row to it.

---

## 0. What this proposal does NOT repeat

[`history-data-store-spec.md`](history-data-store-spec.md) already handles two sets of items:
- §2a: the drop list for the box's trade-capture machinery (gap repair, the write guard, `CoverageReport`, the venue check, `ws_health.log` as capture evidence, and the audit's box-store rows).
- §2b: three reshaped items (`large_liq_size`, A4's liquidation half, engine-fix B2).

This proposal covers only items **outside** those two lists.

---

## 1. The dividing line (from `history-data-store-spec.md` §1)

| Data | History after stage 2b | Consequence |
|---|---|---|
| Trades, including liquidation flags | ✅ back to 2020 | Trade-derived **distributions and fire rates** can be measured on about 21 months |
| Candles, funding | ✅ already | Unchanged |
| Order book, OI, sub-minute ticker | ❌ none | Forward-only |
| Engine outputs (`analysis_log.csv`, eval cache) | ❌ the engine's own record | Forward-only. **Any study whose arms or outcomes depend on logged verdicts still needs forward rows** |

⚠ **Replays mute the book votes.** [`backtest-synthesizer-proposal.md`](backtest-synthesizer-proposal.md) mutes OFI, OI, spread and absorption. It forbids using a replayed book for live-population rates (Kelly win rates, tier rates) or for calibrating the muted signals.

⭐ **But the book state is already logged per run.** `analysis_log.csv` writes `OI_Current`, `OIChange15m`, `OIChange60m`, `OISignal`, `OFIRatio`, `OFIBidVol`, `OFIAskVol`, `OFISignal`, `SpreadBps`, `OFIMomentum` and the absorption numerics on every run (`AnalysisLogger.vb:126-149`, read 2026-09-29). The collector keeps that file. **So no new store is needed for book data** (trader, 2026-09-29; this seat's earlier suggestion of one is withdrawn). See `HSR-11` for the cheaper use of those columns.

---

## 2. Drop or rewrite — the premise is false once the store exists

| ID | Item | Where | Proposed change | Why |
|---|---|---|---|---|
| `HSR-1` | P5 "Liquidation count window" (re-add a trade-count window if 0 liq events are seen after 1,000+ rows) | `DeribitIndicatorProject.md` §16.6 | **Drop.** Point to the `D-4` re-ruling (the liquidation vote and cascade alarm) | The 0 comes from the live stream never delivering the flag (finding `L-1`, [`medium-tier-bug-hunt-2026-09-16.md`](medium-tier-bug-hunt-2026-09-16.md)), not from the window size |
| `HSR-2` | "The store has THREE ERAS; any tape-derived measure must split on them" | `roadmap.md` §2, Books and store row | **Retire at stage 2b**, replaced by "tape-derived studies read the dev store" | The backfill is one complete era. It also heals the 2026-08-10/11 half-complete era and the 2026-09-18→21 outage hole |
| `HSR-3` | "Append-forward is the only path for trades" | `architecture.md` Design Decisions, trade-capture row; `roadmap.md` W4 trade-capture row | **Mark superseded** (quote-and-label), no work | [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §5a. The queue row on the history host already says the v64 premise is false |
| `HSR-4` | Liq `large_liq_size` and `dominance_ratio` as "review against the CSV log" open questions | `trader-profile.md` §7; `DeribitIndicatorProject.md` §12 (Liq dominanceRatio row) | **Rewrite as one history-store study**: `large_liq_size` is already in `history-data-store-spec.md` §2b; add `dominance_ratio` to it | Real liquidation sizes and sides exist in history. ⚠ Book the side by the `D-5` rule (maker side for `M`), or the study inherits finding `L-2` |

---

## 3. Re-gate — trade-derived calibration moves to history

Distribution and fire-rate work runs on the history store. **The outcome half of each item still needs forward verdict rows** (§1).

| ID | Item | Where | Today's gate | Proposed gate |
|---|---|---|---|---|
| `HSR-5` | ATR-conditional burst-watch reference (ruling `AVR-1`) | `trader-tick-queue.md` §4; [`aggr-vel-burst-rederivation-read-2026-09-26.md`](aggr-vel-burst-rederivation-read-2026-09-26.md) | Live rows since mid-2026-08 | **Re-derive the reference over about 21 months after stage 2b.** Aggressor velocity is purely trade-derived, so the history covers several volatility regimes. Burst outcome runs 1 and 2 stay forward and unchanged (they condition on logged verdicts) |
| `HSR-6` | CVD / RSI divergence-gate re-measure (v36 carry-forward (b)) | `roadmap.md` W1 | "Rides the next audit re-run's data pass" | **After stage 2b, CVD half on history.** RSI is candle-derived and already has history |
| `HSR-7` | TTM `flat_threshold` (ATR-relative k) | `roadmap.md` W1; `trader-tick-queue.md` §0a | "Un-parks on trades-covered replay" | **Reachable at stage 2b.** Un-parking still needs a spec, because the change is a scoring boundary |
| `HSR-8` | TFI threshold sweep | `DeribitIndicatorProject.md` §12 | "Waiting on the W1 audit re-run" | **Fire-rate half on history after stage 2b; outcome half on the live book** |
| `HSR-9` | MicroCVD accel threshold, CVD `SlopeMinUsd` | settings keys | No read scheduled | **Distribution read on history**, if and when either key is next questioned. No work is created now |
| `HSR-10` | Backtest-synthesizer muted-vote re-validation | `backtest-synthesizer-proposal.md` §7.4 | A thin span dominated by bar-swap noise | **Re-run over the backfilled span** |

---

## 4. The replacement for a book store

| ID | Item | Proposal | My read |
|---|---|---|---|
| `HSR-11` | Replays over the collector era mute OFI, OI and spread, although `analysis_log.csv` logged them per run | Let the backtest synthesizer **join the logged per-run book columns** for any bar the collector covered, and keep muting only outside that span. Disclose which bars are joined | **Worth a small spec, after stage 2b.** It turns the muted-vote gap into a covered-span question, with no new capture. ⚠ It does not permit re-thresholding OFI at a different book depth; that needs raw book, which is the planned raw-book test (`HH-2`) |
| `HSR-12` | Sub-run book detail (book depth changes, the absorption D8 re-derivations, A4's flip timing) | **No standing store.** Leave it to the raw-book test | Agree with the trader: a targeted experiment, not a store |

---

## 5. Unchanged — they need book data or verdict rows

A4 (liquidation × OFI flip; the book half is forward-only) · absorption Stage 1 and Stage 2 · the OFI dominance watch · the Kelly and tier-ladder separation (forbidden from replays) · the CeilingAudit re-run · burst outcome runs 1 and 2 · the tier-order forward test · the eval-cache items and `_evalCache` · the fills-import tool · the auto-tweaker first fire · the session-volume multipliers (blocked on the forming-bar ruling, not on data).

---

## 6. For the evaluating seat — the questions

1. Is any row in §2 or §3 already handled in `history-data-store-spec.md` or elsewhere in a way this proposal missed?
2. `HSR-5`: does a 21-month reference conflict with the pre-registration of burst outcome run 1? My read: no, because run 1's arms use logged verdicts and its ATR fifths use the RULED edges, which this does not move until a new ruling.
3. `HSR-11`: is joining logged per-run values into a replay consistent with the synthesizer's "no fabricated book state" rule? My read: yes, because they are observed values, not stand-ins, but only at run timestamps.
4. Anything the stage-2 build itself should change because of these rows?

---

## 7. What I did not verify

- That OI has no history source. `history-data-store-spec.md` §1 marks this unverified too.
- That the backtest synthesizer reads the dev store unchanged. The spec asserts it from a grep.
- That every column named in §1 is populated on every run (read from the header only, not from rows).
- The state of each gate quoted in §2–§3 beyond the text of the cited document.
