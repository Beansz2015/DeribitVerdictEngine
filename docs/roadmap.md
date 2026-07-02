# DeribitVerdictEngine — Strategic Roadmap

**Created:** 2026-07-02 (Fable seat, post-audit planning session; trader-directed).
**Purpose:** the single cross-project planning document — objectives, workstreams, sequencing rules, and the execution schedule. Engine-local backlogs stay where they are (`DeribitIndicatorProject.md` §12/§13/§16.6, `post-websocket-post-calibration-backlog.md`); this doc says **what runs in what order and why**, across projects.
**Referred from:** `CLAUDE.md` (session-start pointer), `DeribitIndicatorProject.md` §13/§16.
**Maintenance:** update the §2 state snapshot and tick §4/§5 items as they land; move finished items to a dated ✅ line rather than deleting. Re-plan sections, not sentences.

---

## 1. Strategic objectives (in priority order)

**O1 — Signal quality to its data-supported ceiling.** The scoring core is correct (2026-07-02 full audit: all invariants hold); remaining alpha is calibration debt and signal-stack hygiene, not new machinery. Everything in W1 serves this.

**O2 — Signal bridge to DeribitOrderPlacementApp (NEW, 2026-07-02).** The engine's verdict/score/direction/ATR will feed the order-placement app's autotrade function (`C:\Users\user\source\repos\DeribitOrderPlacementApp` — its `FrmIndicators.ProcessAutomatedSignal` → `ExecuteAutomatedTrade(direction, score, atr)` chain today runs on a home-grown EMA/VWAP/DMI score; the bridge replaces/gates that with the engine's verdict). **Design invariant: the human display remains the engine's primary output — the machine contract is an *additional* surface**, and it joins the display-parity obligation (signal-file values must equal what the cards/snapshot show for the same run). Autotrade consumption raises the effective cost of a wrong signal from discretionary to mechanical, which raises the priority of W1's calibration items — especially the reach-target calibration (the order app will place ATR-derived targets).

**O3 — Linux CLI port (long-standing, §16.2).** Unchanged destination; the port's hard prerequisite (host-agnostic run-state) is specced in this window so the port becomes "write a renderer + a runner," implementable any time after. The signal bridge is deliberately designed host-agnostic (file emission) so a future headless engine feeds the same contract.

---

## 2. State snapshot (2026-07-02)

- Engine **v47** (audit fixes F1/F2/F3+N1–N4) — local commits `688127b..552965b`, gate-green, awaiting trader test+push. origin/master = `a00ac35`.
- **v48 OFI dominance re-baseline** is the next dataset-gated pass (geometric distribution; 2.0/0.5 are stale snapshot-era values). Collection running: 07-01 partial (Asia-tail/London) + 07-02 through NY (first NY×1 geometric session). Gate ≈ 2–3 weekday session-days incl. NY → expected satisfiable **~Jul 4**.
- P4 #5 aggressor velocity: spec **approved**, build deliberately queued behind v48 (§6 rule 1). P4 #6 book absorption: spec not yet written. Display trio + #10 shipped (v43–v45).
- F3 observational watch open (EXIT GUARD strip vs HOLD\EXIT row corroboration during holds).
- Fable available through **Jul 7**; Opus on Max-5x for the month after. Fable = specs/derivations/rulings; Opus = implementation.

## 3. Workstreams

### W1 — Calibration & signal health (O1, feeds O2)
| Item | State | Gate |
|---|---|---|
| v48 OFI dominance re-baseline (+ `OFI.Momentum*` retire-or-keep) | Spec Jul 3; derivation when data clears | Multi-session geometric data incl. NY (~Jul 4) |
| Post-WS signal-health audit — fire rates, pairwise agreement, conditional outcomes for OFI/TFI/CVD/MicroCVD + FundingMomentum/RSI-div/OISignal | Fable runs it in-window | Post-v42 book (exists) |
| Spread revival re-baseline (WIDE/TIGHT thresholds on 100ms-fresh book; A1 spread-momentum rides along) | Folds into audit output spec | Audit results |
| Directional reach-target calibration (D7 spin-off — 43% reach / 53% window-expiry vs `AtrTargetMultiplier` 2.0; **elevated by O2**: autotrade places these targets) | Spec in-window if time, else month item #1 | Analysis-layer; data exists |

### W2 — Indicator queue (O1)
| Item | State | Sequence |
|---|---|---|
| #5 Aggressor velocity ⚠ | Spec approved | Build **after v48 lands** (rule 1), then collect → calibrate |
| #6 Book absorption ⚠ | Spec: Fable, ~Jul 4–5 (snapshot-feed v1 — public data, no auth, no incremental-book plumbing) | Build after #5 calibrates |
| A4 Liquidation × OFI flip ⚠ | Spec: month item (backlog Section A; high payoff; not in the original §11 list — added here) | After #6 |
| Deferred: A5 VPFR shape (30-day data gate), D3/D4/D5/D6, B1 per-indicator weights (overfit-gated) | Unchanged | Per their backlog triggers |

### W3 — Signal bridge → DeribitOrderPlacementApp (O2, NEW)
**Trader rulings (2026-07-02, binding on the spec):**
- **R1 — Full replacement, not gating.** The engine's scoring **replaces `FrmIndicators`' home-grown EMA/VWAP/DMI score entirely** (those are lagging indicators and a strict subset of the engine's inputs — gating the verdict with them would be cross-process double-counting, would selectively block early entries, and would decouple the executed population from the engine's calibration book). The order app keeps **execution-policy gates only**: cooloff, max-age/health checks, tier selection. The action key is the **verdict tier/confidence, never the raw score** (raw scores sit under regime-dependent ceilings; scores travel in the payload for logging only). Optional trust-building during the supervised period: a shadow log of what FrmIndicators would have said — never a gate — decommissioned after.
- **R2 — Engine levels are authoritative; order-app ATR is a checkbox override.** The order app consumes the engine's **final effective levels** (post-Step-5b cap-arbitrated target + stop), not an ATR formula — the verdict only survived the BELOW_MIN_MOVE gate because of that effective target, and the engine's ATR is execution-resolution ATR (1m NY / 3m Asia-London, v36), which the order app's own quote-feed ATR would silently diverge from by session. Raw ATR + multipliers ride in the payload for transparency and for the override math. The override checkbox (order-app side, not yet implemented): **default OFF**, applies to distance math only, can never resurrect a verdict the engine suppressed, and when ON both level sets are logged per trade so the divergence is measurable.

**v1 (this window's spec, month's first implementation):** per-run atomic-write `verdict_signal.json` (tmp + `File.Replace`, the repo's existing pattern) emitted after every completed run — **including NO TRADE and SKIPPED runs**, so the consumer can distinguish "engine says stand down" from "engine dead." Consumer side: FileSystemWatcher + debounce in the order app. Contract carries (spec finalises): `schema_version`, `signal_id` (monotonic), `generated_at_utc`, engine/settings version, instrument, verdict + confidence + context tag, long/short raw+effective scores + max, dominant direction, price, ATR + exec resolution, **effective levels** (stop/target incl. capped target + cap reason), structural swing levels, MTF-blocked flag, skip/health flags (WS state, freshness, `LedgerMismatch`). Consumer-side hard gates the spec mandates: max-age staleness, schema-version match, NO TRADE/BELOW_MIN_MOVE/skip ⇒ no action; cooloff/sizing/execution policy stay order-app-owned. Emission goes through a **host-agnostic core class** (thin call from the run path) — serves the CLI port unchanged.
**v2 (later, own spec):** position-state feedback (order app → engine file so `posState` stops being a manual radio during autotrade), which makes `HoldStatus`/EXIT-guard output an actionable exit signal in the contract; slippage-aware signal pricing. **v2 is gated on v1 running supervised for ≥1–2 weeks.**

### W4 — Platform & plumbing (O3 + ops)
| Item | State |
|---|---|
| CLI-port run-state extraction (host-agnostic run-context for `_fundingHistory`/`_ofiHistory`/`_oiHistory`/MTF cache/`_prevRegime` + headless runner skeleton) | Spec: Fable ~Jul 6; implement during month; zero behaviour change |
| WS-health line persistence to log (closes the "feed health is inferred" monitoring caveat) | Tiny; Opus any time |
| Incremental full-depth order book (public channel, change_id semantics) | **Deferred** — only if #6-v1 proves absorption on snapshots; then its own spec |
| **Authenticated API / raw interval: NOT pursued** (decision 2026-07-02) | Verified vs current Deribit docs: auth unlocks only sub-100ms `raw` feeds — below the noise floor for 2–15 min holds; the material upgrade (full-depth incremental book) is public at 100ms. Auth also means key custody on an unattended box. Revisit trigger: order-management-in-engine ever enters scope (it is currently excluded), or a specced consumer needs event-time sequencing that 100ms provably blurs |

### W5 — Auto-tweaker ops
First live fire stays data-gated (>40%-failure NY×1 window) and supervised; P13 UserManual tier documentation = doc pass during month; Phase-2b per-population autotune stays parked (may never be built).

## 4. Fable window — Jul 2–7

| Day | Deliverable |
|---|---|
| **Jul 2** ✅ | Audit + fixes (v47) + this roadmap |
| **Jul 3** | **v48 re-baseline spec** (recipe from `time-averaged-ofi-spec-back.md` §5 + Momentum* decision); **signal-bridge v1 contract spec** started |
| **Jul 4** | **v48 derivation** (if data gate clears) → settings diff for approval; bridge spec finished |
| **Jul 5** | **Signal-health audit** run + results → consolidated retune/retire spec (spread revival folded in); **#6 absorption spec** |
| **Jul 6** | **CLI-port state-extraction spec**; buffer (slipped gates, coordinator review of any in-flight Opus work, reach-target spec if time) |
| **Jul 7** | **Month handover doc** (Opus execution order + gates + review checklists + sign-off points); memory closeout |

Protected if trimming is forced: v48 derivation, signal-bridge spec, handover doc.

## 5. Month execution order (Opus, after Jul 7)

1. v48 settings pass (if not landed in-window) → **push boundary**.
2. Signal-bridge v1 implementation (engine emit + order-app consume) → supervised parallel run (autotrade in log-only/dry mode first).
3. #5 aggressor velocity build → collect → calibrate (own boundary).
4. Signal-health-audit retune/retire pass (spread thresholds, FundingMomentum, any retirements) — settings-mostly.
5. #6 book absorption build → collect → calibrate.
6. CLI-port state extraction (behaviour-neutral, any gap).
7. Reach-target calibration spec execution; A4 spec+build; bridge v2 (position feedback) once v1 has 1–2 supervised weeks.
8. Continuous: tweaker first-fire when its window appears; F3 watch; WS-health persistence.

## 6. Sequencing rules (binding)

1. **One scoring change per dataset boundary.** A ⚠ item never builds while another ⚠ item's collection window is open. Current open window: v48.
2. **Spec-first for scoring/novel behaviour; trader signs off; local-first; trader tests + pushes.** Unchanged.
3. **Display parity now has three surfaces:** plaintext snapshot ↔ cards ↔ `verdict_signal.json`. Any commit changing a value on one updates all three or states why not.
4. **Autotrade safety is consumer-gated, engine-informed:** the engine never suppresses information for the bridge (it emits health flags and stands-down states); the order app owns the decision to act. Engine-side trade execution stays out of scope.
5. The rejected-approaches list (trader-profile §4 / backlog Section E) binds this roadmap; nothing here reintroduces a rejected pattern.
