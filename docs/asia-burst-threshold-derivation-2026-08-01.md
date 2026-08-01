# res-3 §5.2 — ASIA aggressor-velocity threshold derivation (2026-08-01)

**From:** the incoming orchestrator seat (gap-audit item **G7** — the res-3 §5.2 ASIA pass, which the seat-close handover carried no slot for).
**Recipe followed:** [`aggressor-velocity-s52-derivation-2026-07-13.md`](aggressor-velocity-s52-derivation-2026-07-13.md) §3 + Appendix, verbatim — population `AggrVelBurstRatio ≠ ""`, fire-at-T = `Signal=BURST_* ∧ BurstRatio ≥ T`, same-side vs `TFISignal`. Valid for ASIA because its live threshold is the **default 2.5**, below every candidate T, so every candidate fire is already live-classified.
**Data:** pooled AWS-preferred book (2026-07-31 dedup ruling), frozen, weekday-only. **ASIA n = 1,489 rows with AggrVel data over 14 session-days.**

> **THE DATA GATE IS MET — and comfortably. 323 fires at the exploratory default against a ~150 bar.** §5 of the 07-13 derivation deferred this pass with *"36 fires across all res-3 rows … a per-session threshold needs ~150+ fires per session (several more weeks at current cadence)."* The AWS collector closed that in eight days.
>
> **RECOMMENDATION: `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` = 5.5** — the same value LONDON carries, arrived at independently.
>
> ⚠ **This is a live scoring change.** Threshold presence is what **arms** the TFI modifier on a session (wire-in spec-back D1), so setting it turns ASIA scoring on. It needs its own D-table and dataset boundary. **Nothing is built or changed here.**

---

## 1. Candidate table — ASIA (n=1,489, 14 session-days)

burstRatio distribution: p50 **0.55** · p75 2.09 · p80 2.90 · **p90 5.35** · p95 8.47 · p99 15.01

| T | fire rate | same-side TFI | fires/day | contra/day | per-day fire-rate range |
|---|---|---|---|---|---|
| 2.5 (current default) | 21.7% | 89.5% | 23.1 | — | — |
| 3.0 | 18.9% | 89.7% | 20.1 | — | — |
| 3.5 | 16.3% | 90.1% | 17.4 | — | — |
| 4.0 | 13.8% | 91.3% | 14.7 | 0.43 | 8.5–18.1% |
| 4.5 | 11.7% | 91.4% | 12.4 | 0.36 | 5.6–15.6% |
| 5.0 | 10.7% | 91.2% | 11.4 | 0.36 | 5.6–14.4% |
| **5.5 (rec)** | **9.7%** | **91.0%** | **10.4** | **0.29** | **4.5–13.8%** |
| 6.0 | 8.9% | 91.7% | 9.5 | 0.21 | 4.5–13.1% |

**Why 5.5**, against the 07-13 criteria in order:

- **(a) Fire rate.** 9.7% is the closest cell to the ~10% "genuine impulse" design point and sits inside the 8–12% band. The current default 2.5 fires on **21.7%** of ASIA rows — one bar in five, the always-on failure mode the audit retired OFIMomentum for.
- **(b) The p90 knee.** ASIA's p90 is **5.35**; 5.5 is the nearest grid value. Identical construction to NY (p90 4.51 → 4.5) and LONDON (p90 5.69 → 5.5).
- **(c) Same-side agreement.** 91.0%, comfortably over the ≥85% bar — but see §3: it does **not** discriminate here.
- **(d) The contra-soften arm.** See §3 — it cannot discriminate either, and that is itself a finding.

---

## 2. ASIA needs no value of its own — the res-3 sessions share one distribution

The two res-3 sessions' burstRatio distributions are the same within sampling noise:

| percentile | ASIA | LONDON | ASIA/LONDON |
|---|---:|---:|---:|
| p50 | 0.55 | 0.52 | 1.044 |
| p75 | 2.09 | 2.30 | 0.910 |
| p80 | 2.90 | 3.12 | 0.931 |
| **p90** | **5.35** | **5.69** | **0.941** |
| p95 | 8.56 | 8.82 | 0.971 |

Every percentile agrees within ±9% on n≈1,489 each. **A separate ASIA constant would be fitting noise**, which is why the recommendation is LONDON's shipped value rather than a bespoke 5.35 or 5.0.

**Decision-of-record proposed (the [D-B OBV rider](candle-store-derivation-batch-spec-back.md) shape): `burst_ratio_threshold` needs no ASIA-vs-LONDON split — the res-3 burst distribution is one distribution.** This is expected rather than surprising: `burstRatio` is a ratio of a fast window to a normalisation window, so it is scale-free by construction and should not carry session liquidity levels. The NY value differs (p90 4.51) because NY runs a **different norm window** (60 s vs the 120 s default), not because NY is busier — the §4 window↔threshold coupling caveat, still binding.

Keep two identical per-session entries rather than moving the value to `default`: the auto-arm semantics key on **session threshold presence**, so writing it to `default` would silently arm every session including any future one.

---

## 3. Two findings that change how the criteria read on res-3

**The contra-soften arm is effectively dead on res-3 at every candidate T — including at LONDON's already-shipped 5.5.** ASIA runs 0.21–0.43 contra/day and LONDON 0.16–0.42, against the 07-13 rule that *"at T≥5 it effectively dies (≤1.4/day), and that arm is the §4.5 'genuine warning' half of the design."* By that standard the arm is dead on res-3 across the whole candidate range, so **criterion (d) cannot discriminate between T values here** — it is not a reason to prefer a lower threshold, because even T=4.0 only buys 0.43/day.

This was not flagged when LONDON was armed at v60, and it is worth recording plainly: **on 3-minute sessions the aggressor-velocity modifier is, in practice, an upgrade-only mechanism.** Not a defect — the upgrade arm carries the design — but the §4.5 warning half should not be claimed as operative on res-3.

**Day-to-day stability is far worse than NY's.** NY showed ±2pp per-day range at fixed T; ASIA spans **4.5–13.8%** at T=5.5 and LONDON **5.3–21.0%**. That is a ~9pp and ~16pp spread. The mechanical cause is row density — ASIA carries ~106 AggrVel rows/day against NY's 1-minute cadence — so the per-day rates are small-sample. **It means no res-3 threshold should be re-fitted off a single session-day**, and any post-ship watch on ASIA needs a multi-day band, not NY's ±2pp.

---

## 4. What I did not verify

- **Nothing joined to outcomes.** This is a distributional derivation exactly as the 07-13 NY pass was: it sets *how often* the modifier engages, not whether the rows it engages on are the profitable ones. The conditional-outcome evidence remains unmeasured on every session.
- **The norm window is assumed, not re-derived.** ASIA inherits `default.norm_window_sec = 120`. The §4 coupling caveat binds: **5.5 is derived FOR fast=5 s / norm=120 s**, and changing either re-opens this.
- **No LONDON re-fit.** LONDON's 5.5 was read here only as a cross-check; its own watch passed 2026-07-27 and this derivation does not re-open it. That my method reproduces LONDON's shipped value from independent data is the main evidence that the method transferred correctly.
- **Weekend behaviour** — weekday-only throughout, per the standing rule.
- ~~**No engagement preview.**~~ **Supplied in §5.1** at the tick. The 07-13 pass produced a §6 wire-in preview (upgrades/softens per day against TFI's directional share); this one is **computed from §1's own table, not separately measured**, and is labelled as such.

---

## 5. D-table — **TICKED 2026-08-02 (trader). SHIPPED as settings v65.**

Status: **BUILT 2026-08-02**, local commit, unpushed — trader tests + pushes (§8 workflow). ⚠ **This is a live scoring change and a dataset boundary:** rows after this build carry the burst-modified TFI vote on ASIA.

| # | Decision | Ruling | Basis |
|---|---|---|---|
| **D3-1** | The value | **`sessions.ASIA.burst_ratio_threshold` = 5.5**, as recommended | §1 — fire rate 9.7 % against the ~10 % design point and inside the 8–12 % band; p90 knee 5.35 → nearest grid value; same-side 91.0 % over the ≥85 % bar |
| **D3-2** | ASIA-specific value, or LONDON's? | **LONDON's value. Decision-of-record: `burst_ratio_threshold` needs no ASIA-vs-LONDON split — the res-3 burst distribution is ONE distribution.** | §2 — every percentile within ±9 % on n≈1,489 each. `burstRatio` is scale-free by construction, so it should not carry session liquidity levels. A bespoke 5.35 would be fitting noise |
| **D3-3** | Where the key lives | **Two identical per-session entries. NOT promoted to `default`** | §2 — the auto-arm keys on session-threshold *presence*, so writing it to `default` would silently arm every session, including any future one |
| **D3-4** | Sequencing | **Alone and first**, ahead of the D1+D2 bundle | D-cluster ruling 2026-08-01 — D3 *arms* rather than retunes and an activation deserves a clean window; bundling with D2 (OBV `trend_gate` 18→23) would confound ASIA, since both push the ASIA upgrade path the same way |
| **D3-5** | Post-ship watch | **Fire rate ≈9.7 % · same-side ≥85 %, read over a MULTI-DAY band — never a single session-day** | §3 — ASIA's per-day rate spans **4.5–13.8 %** at T=5.5 on ~106 AggrVel rows/day. NY's ±2pp band does **not** transfer, and no res-3 threshold may be re-fitted off one session-day |
| **D3-6** | The outcome evidence | **Recorded in the change_log and here; does not block arming** | See §5.2 — it is neutral at best, and the queue's ruling was explicit that the D-table must say so rather than lead with the distributional case alone |

**Also carried into the build:** the §3 finding that **the contra-soften arm is effectively dead on res-3** at every candidate T including LONDON's shipped 5.5 (ASIA 0.21–0.43 contra/day). On 3-minute sessions this modifier is in practice **upgrade-only** — not a defect, the upgrade arm carries the design, but the §4.5 "genuine warning" half must not be claimed as operative on res-3. And the **coupling caveat binds**: 5.5 is derived *for* `fast_window_sec` 5 / `norm_window_sec` 120, so changing either re-opens this.

### 5.1 Engagement preview — computed from §1, not separately measured

At T=5.5 on ~106 AggrVel rows/day: **~10.4 fires/day**, of which **~9.5/day same-side** (+1 upgrade) and **~0.29/day contra** (−1 soften). The modifier therefore touches **~9.7 %** of ASIA rows, essentially all of it through the upgrade arm — which is §3's finding restated as a rate.

### 5.2 ⚠ The honest caveat, recorded rather than buried

The W6-4 ceiling audit's informational column is **the only outcome-linked read that exists on this knob, and it is neutral at best**: `AggrVelBurstRatio` univariate test **AUC 0.5179** (n=217), `AggrVelNet` **0.4654**.

It does not refute arming — NY and LONDON were armed on distributional grounds too, LONDON's watch passed 2026-07-27, and a ±1 modifier on ~10 % of rows would not be expected to show strongly in a univariate AUC on ~220 test rows. But it is the *only* outcome evidence, and it sits inside the **three-instrument finding of 2026-08-02**: the engine's own score at AUC 0.5407 with no challenger beating it, tier bands that do not separate, structural placement at 0.4683 / 0.5000 / 0.3221. **Do not spend on further scoring refinement without weighing that**, per W6-4's own "no spend meanwhile."

### 5.3 What the build actually touched

**Not a settings-only change**, contrary to how the queue row reads. Threshold *presence* is the arming mechanism, so the value must exist on **both** surfaces:

- `settings.json` — the ASIA key, version 64 → 65, change_log entry.
- `Core/Settings/EngineSettings.vb:478` — the POCO default in lockstep (the **v60 LONDON precedent**). The regression harness builds every cfg from `New EngineSettings()`, so a JSON-only edit would leave the app and the harness pinning different behaviour.
- `verify/ordercheck/Program.vb` — **A28c re-pinned.** ASIA *was* the un-armed exemplar; with all three shipped sessions now armed, **no session remains that can demonstrate the S2a scoping mechanism**, so the un-armed arm is now constructed by clearing ASIA's threshold on a cfg copy. That pins the mechanism rather than an incidental session, and it is the D3 rollback's only cover. A23f's comment corrected (stale since v60). **New A52a** pins the JSON→arming contract in three arms, the third being a JSON↔POCO drift guard.
- **No new hard constraint** — `indicators.aggressor_velocity.sessions.` is already prefix-rejected under **HC19**; HC28 stays free.
- **No display-string parity obligation** — the burst suffix appends to the existing TFI breakdown note row and the card binds the same `SignalBreakdownItem.Note`. Arming ASIA only makes an already-rendered suffix *reachable* on ASIA rows.
