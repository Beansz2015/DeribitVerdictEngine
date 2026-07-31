# Gap audit — the Fable seat-close handover (2026-07-31)

**From:** the incoming orchestrator seat, first session.
**Why:** the trader's concern that the seat-close handover left items out, raised after its task-1(a) collector verdict proved wrong (withdrawn in `8eda945`). This audit answers "what else is missing", not "was the seat careless".
**Method:** [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) cross-checked line-by-line against [`backlog-dependency-map.md`](backlog-dependency-map.md) (the board), [`trade-store-arc-spec-back-2026-07-31.md`](trade-store-arc-spec-back-2026-07-31.md) (JOB 1), [`candle-store-derivation-batch-spec-back.md`](candle-store-derivation-batch-spec-back.md) (JOB 2), [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) and [`seat-handover-2026-07-18.md`](seat-handover-2026-07-18.md) §4.

**Headline:** the handover's *rulings* are sound and complete — §2's observability cluster stands on its own evidence and nothing here touches it. The omissions are concentrated in the forward-looking sections (§4 first tasks, §5 month items), which is the expected failure mode for a doc written last, at 5% budget. **Twelve substantive items**, two of which contradict a ruling already on file.

---

## 1. Carries a task that was explicitly killed — fix before anyone acts on §5

> ✅ **BOTH CLOSED 2026-07-31**, in the order recommended in §8. **G2:** D1 re-ruled to **full clearance on the VWAP axis** — window cleared, VWAP values cleared outright at 100.00 %, fine dev-threshold sweeps < 5 bps cleared, the ~1.3 bps noise floor withdrawn; `VolumeRatio` at 65.00 % stays *advisory* as a separate axis (D3 forming-bar partiality, not the unit bug). Recorded in [`backtest-overlap-validation-2026-07-30.md`](backtest-overlap-validation-2026-07-30.md) §10.4 (canonical), mirrored in [`backtest-synthesizer-spec-back.md`](backtest-synthesizer-spec-back.md) §9.2 and [`pre-aug1-batch-spec-back.md`](pre-aug1-batch-spec-back.md) §2 D1. **G1:** the void D2 task struck from the handover's §5 and replaced with the corrected status. Doing G2 first was the right order — the clearance evidence is what makes G1's deletion self-evidently correct.

**G1 · §5 still lists the D2 tolerance-reclass micro-task as live.** The handover says *"backtester: cleared geometry-class, VWAP-values partial (D1/D2 rulings in `pre-aug1-batch-spec-back.md` §6 — **the D2 tolerance-reclass micro-task widens it**)"*.

That task is **void**. [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §1: *"**D2 — do NOT spec the bps-scale tolerance reclass.** `NumTight` was correct throughout."* The ~64,000× unit bug in the synthesizer's forming stub — USD notional summed into a BTC field — removed the question the reclass existed to answer. An implementer handed §5 as written would spec a task the previous seat's own handover forbids.

**G2 · D1's widening confirmation was never done, and §5 preserves the pre-fix status instead.** [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §5 ranked it **#2** of three items worth the Friday budget: *"D1's widening confirmation — one line, given §1."* Grepped the docs tree: no 07-31 document records it. §5 of the seat-close handover still describes VWAP-values clearance as *"partial"* — the pre-fix state.

Post-fix, VWAP agrees **100.00%** and the σ bands 99.76–100.00% on all 840 rows. This is the cheapest open item on the entire board and it unblocks the backtester's VWAP-sensitive study class. **Do G2 before G1** — confirming D1 is what makes G1's deletion obviously correct rather than merely instructed.

---

## 2. Open decisions dropped

**G3 · J-D is absent entirely.** JOB 1 §2 queues five lettered decisions. The handover rules J-A (§4.3), J-B, J-C, J-E (§2) and J-F (§2). **J-D — ratify the overlay's restated whitelist rule — appears nowhere.**

Its substance: the original rule was *"a key is safe to diverge per box exactly when it is safe off the tweaker surface"*, which is false — `network.transport` carries HC12 because the tweaker has no business tuning timeouts, **not** because transport is scoring-neutral. The restatement: *HC fences are a good first filter, not a proof; where the two justifications diverge, review per key.* The reviewer flags it as having *"reach past this spec — the same conflation is available anywhere an existing fence gets reused as evidence for a new property."* That reach is why it deserved a slot.

**G4 · The J-D verification residual is dropped with it.** JOB 1 §4: the reviewer audited **2 of 8** overlay whitelist blocks (`network.`, `auto_run.`) and **both failed**. The implementer reports the remaining six audited clean; the reviewer explicitly did **not** re-audit that audit and wrote *"given a 2-of-2 failure rate on the ones I did check, a second pair of eyes on the other six is worth more than the usual."* A 100% failure rate on the sampled half is not a residual you drop silently.

**G5 · D-C is unassigned.** §4 task 2 says *"JOB 2 read (deferred to you in full) + close **D-D/D-E**"* — naming two of JOB 2's six decisions. **D-C** (does session-volume calibration park behind D3?) is neither ruled nor assigned. Its concrete deliverable is a one-row board edit that the packet says *"costs nothing, and stops the next seat re-deriving what I just derived"* — and the packet re-confirmed at hand-over that the board **still** lacks the edge. Also inside D-C: a flag that v58's stated *mechanism* (the notch "suppressing trades") could account for at most a fraction of a percentage point through the volume channel, and **the same reasoning is queued next for LONDON and NY**.

---

## 3. Board items with no slot in §4 or §5

**G6 · The W6-1 LONDON ruling itself.** §4 task 1(c) confirms the *depth gate* is GO (re-verified: 556 post-07-08 / 567 full book against a ~227 prior base), but §5's month items never name the ruling that gate exists to unblock. The board row reads *"Audit re-run ~Aug 1 (+ what-if LONDON grid — L5/L6)"* — i.e. **due now**. Evidence already on file: 76.8% adverse-first on structural rows, 92% of stops at the clamp, winners-MAE p75 1.63×ATR, two candidates (LONDON `stop_max` 2.0–2.2, swing-buffer offset) that decide **together**.

**G7 · res-3 §5.2 ASIA aggressor-velocity thresholds.** LONDON armed at v60 and its watch **passed** 2026-07-27. ASIA *"remains data-gated"* on AWS coverage, which has been accruing daily since 07-22. Not mentioned anywhere in the handover. Sessions auto-arm on threshold presence, so this is a derivation with no engine change — cheap, and the gate may already be met.

**G8 · D2-v2 best-pivot promotion, in the geometry session.** v63 built `scoring.structural_levels.use_best_pivot_candidate` **specifically** to make this what-if-testable — before it, `ComputeSideLevels` never read `BestPivot*`, so any study would have measured the geometry it was not trying to test. The board names it *"candidate topic for the Aug-1 geometry session"*. §4 task 4's geometry session doesn't mention it. The instrument was built for this and the session it was built for doesn't reference it.

**G9 · Two different geometry reads may be conflated in task 4.** Task 4 says *"interpret the lane-E grids per D5"* — the pre-Aug-1 batch's tables. The board separately carries *"Geometry-modes study re-read (v56 instrument)"*, gated on *"book ~doubles (~mid-Aug) **or** the W6-1 audit — DIVERGENT flags must clear"*. These are different reads on different gates. Worth disambiguating before the session, or the v56 re-read silently inherits lane E's conclusion.

**G10 · Lower-priority board rows with no mention** (all genuinely parked or data-gated, listed for completeness, none urgent): tweaker first fire (W5/W6-2, needs a >40%-failure NY×1 window on post-migration rates) · A5 VPFR shape classification (15 of 30 distinct calendar dates) · the D3–D6 backlog refinements (parked behind W6-4) · W6-7 cross-venue lead-lag · the order-app consumer chain L3 → L9 · the fee relay to the order app (ACK'd 07-27).

---

## 4. Standing watches dropped

**G11 ·** §4 task 5 lists five periodic checks: liq_events CASCADE ⇒ A4 · §9 STRONG accrual · burst spot-checks · funding calm-week · absorption episode accrual under Path B. Against [`seat-handover-2026-07-18.md`](seat-handover-2026-07-18.md) §4, **three live watches are missing**:

| Watch | Band / trigger | Last state |
|---|---|---|
| **v48 §4a OFI dominance** | 0.6×–1.5× of 63.2%, 2 consecutive days | *"Continues; ASIA regressed in-band"* |
| **B4b §12** (reach, STOP_CLAMPED, BELOW_MIN_MOVE, LONDON F3) | F3 < 45% | F3 read 07-15: 49.3%, **not tripped**; *"next at W6-1"* — i.e. now |
| **pullFrac distribution** (W4 trigger) | fidelity-binds evidence | *"accrues passively; read at #6 activation"* — moves with the Path B mechanism spec |

B4b is the pointed one: its next read was scheduled *at W6-1*, and W6-1 is the item G6 says has no slot.

---

## 5. Documentation debt dropped

**G12 ·** [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §5 flagged three manual-content gaps the PDF lane found and deliberately left unfilled, marked "optional, only if budget survives". Budget did not survive, and they were not carried forward:

- v63's `use_best_pivot_candidate` appears in **neither** manual;
- the `MIN NET MOVE % (after fees)` row **label** never appears, though its model is documented at four sites — so it cannot be grepped from what's on screen;
- **`BacktestRunner` appears nowhere in either manual** — a whole tool, with a `report` verb and a `--closed-bars` flag.

Low urgency, but it compounds: the manuals were regenerated as PDFs on 07-31, so the gaps are now baked into the published artifacts.

---

## 6. State claims — checked, not inherited

| Claim | Verdict |
|---|---|
| Settings **v64** | ✅ confirmed (`settings.json` line 2) |
| Next free fixture family **A52** | ✅ confirmed by command — consumed through `A51e_`, A49/A50 reserved-unbuilt |
| **HC28 free** | ✅ confirmed — HC27 consumed by the v64 `trade_store.` prefix fence; HC28 appears only in "still free" claims |
| **26 commits** local, unpushed | ❌ was **28** when written (the two later docs commits are the handover and the coverage read themselves); **30** now |
| Board: candle-backfill fixture gap **OPEN** | ❌ stale — **CLOSED** by `99221b2`; A51a–e verified present in `verify/ordercheck/Program.vb`. Corrected in `8eda945` |
| Store 0-missing at four resolutions + funding | not re-verified this session — inherited from [`store-integrity-check-2026-07-31-post-fix.md`](store-integrity-check-2026-07-31-post-fix.md) |
| Collector state | ❌ withdrawn — see the correction block in the handover and `8eda945` |

---

## 7. What the handover got right

Stated plainly so this reads as an audit rather than a verdict on the seat. **§2's observability cluster is the valuable part of the document and none of it is affected by anything above** — J-B, J-C, J-E and D-F rest on four silent-divergence events that are real, independently documented in both packets, and correctly reasoned. Ruling the cluster together on one principle, at the reviewer's own suggestion (J-F), avoided three inconsistent answers. §3's refusal to authorize D-A/D-B as a build — direction-only, boundary of the next seat's choosing, own D-table — is exactly right under the one-⚠-per-window rule. §4 task 3's instruction to take a **fresh** read of A48f and *"not inherit either side's framing"* is the correct handling of a live disagreement. And §6's batching discipline for the rationed Fable moments is what makes the next month tractable.

**One structural lesson worth keeping**, consistent with this project's habit of recording what went wrong: the errors cluster in the sections written *about the future* rather than *about the work done*. The rulings were derived, checked and are sound; the task lists were assembled at the end, from memory, under budget pressure — and that is where every omission and both contradictions landed.

---

## 8. Suggested order

1. **G2** (D1 widening confirmation — one line, unblocks a study class) → **G1** (delete the void D2 task from §5).
2. **G5** (D-C: the one-row board edge) as part of the JOB 2 read already assigned in task 2.
3. **G3/G4** (J-D ratification + a second pass over the six unaudited overlay whitelist blocks) — the 2-of-2 failure rate makes G4 the higher-value half.
4. **G6 + G11's B4b row** together — the depth gate is GO and B4b's next read was scheduled at W6-1.
5. **G8/G9** folded into the geometry session (task 4) rather than run separately.
6. **G7** — cheap, may already be gated open.
7. **G12** at any gap; **G10** stays parked.
