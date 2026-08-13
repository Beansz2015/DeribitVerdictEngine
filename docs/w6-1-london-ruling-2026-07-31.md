# W6-1 LONDON ruling + the B4b §12 watch read (2026-07-31)

**From:** the incoming orchestrator seat (gap-audit items G6 + G11's B4b row, ruled together because the B4b watch's next read was scheduled *at* W6-1).
**Instrument:** the what-if replay runner — its own report names this as **registered use case #4**: *"W6-1 LONDON `stop_max` 2.0/2.2, and LONDON STRONG-only selectivity — the two named candidates decide on evidence from this instrument."*
**Input:** pooled AWS-preferred book (the 2026-07-31 dedup ruling), frozen, weekday, `--from 2026-07-08` per the D5 decision-surface ruling. Two runs: full book (910 evaluable directional rows) and LONDON-only (215).

> **RULING: NO CHANGE. Do not widen LONDON `stop_max`, and do not ship a swing-buffer offset.** Not "not yet on thin evidence" — the two named candidates are **one lever**, and every widening of it is neutral-to-worse on both books. The real W6-1 finding is elsewhere, in §4.

---

## 1. The two candidates are the same lever — this is the load-bearing finding

W6-1 was framed as two knobs that *"decide TOGETHER"*: LONDON `stop_max` 2.0–2.2, and a swing-buffer offset. On this book they are not two knobs.

`stop_buffer_pct` is applied **after** the clamp (`Core/SignalEmitter.vb:481-488`):

```
placedStopPx = entry + (placedStopPx − entry) × (1 + sBuf/100)
```

and `placedStopPx` at that point is already `min(structuralDist, stop_max × ATR)`. So the final stop distance is the **product**:

```
stopDist = min(structuralDist, stop_max × ATR) × (1 + buf/100)
```

Whenever the clamp binds — i.e. the structural stop is looser than `stop_max × ATR` — the two knobs are algebraically interchangeable. **The clamp binds on 95.5% of LONDON structural-stop rows** (§3), so they are interchangeable almost everywhere.

**Confirmed empirically, not just derived.** The grid was built to contain matched pairs, and they land on top of each other:

| effective stop | via `stop_max` | via buffer | EV/ATR | σ |
|---|---|---|---:|---:|
| 1.80 | (1.8, 0%) | (1.6, +12.5%) | −0.506 / −0.504 | 1.753 / 1.754 |
| 2.00 | (2.0, 0%) | (1.6, +25%) | −0.552 / −0.551 | 1.833 / 1.836 |

Agreement to **0.002 EV and 0.003 σ** — the residual is exactly the ~4.5% of LONDON rows where the structural stop is tighter than the clamp and the two paths genuinely differ. Reproduced on the LONDON-only run (1.80: −0.153 / −0.155; 2.00: −0.209 / −0.213).

**Consequence for the decision:** "decide them together" is satisfied trivially. The 2-D grid collapses to a 1-D sweep of the product, and any apparent 2-D optimum would be a point on a diagonal ridge — exactly the single-cell-win-on-a-swept-grid artifact the runner's guard-rail 2 warns about.

---

## 2. Widening loses, and the LONDON evidence cannot separate the top

**Full book (n=910/cell):** the live baseline wins outright and the ordering is monotone in the effective stop — EV/ATR **−0.475** at 1.60, falling to **−0.647** at the widest cell. The two named candidates are refuted on outcome evidence: **2.0 → −0.552**, **2.2 → −0.584**, both clearly worse than 1.6. **No DIVERGENT flags on any of the 15 cells**; selection and holdout halves agree throughout.

**But that ranking is pooled, and NY is 61% of it** (NY 555 / LONDON 215 / ASIA 140). It is not a LONDON verdict, so the grid was re-run LONDON-only.

**LONDON-only (n=215/cell):** the live baseline still ranks first, but honestly —

| rank | cell | effective | EV full | EV (sel) 95% CI |
|---|---|---|---:|---|
| 1 | (1.6, 0%) | 1.60 | **−0.155** | [−0.454, +0.246] |
| 2 | (1.8, 0%) | 1.80 | −0.153 | [−0.527, +0.207] |
| 3 | (1.6, +12.5%) | 1.80 | −0.155 | [−0.529, +0.206] |
| … | | | | |
| 15 | (2.4, +25%) | 3.00 | −0.392 | [−0.957, −0.033] |

**The top three are a tie inside noise** — rank 2 is nominally *better* on the full sample than the winner, which was selected on the selection half. Every CI is ~0.7 EV wide and they all overlap. What survives at this n is only the *direction*: the wide tail (ranks 10–15, effective 2.4–3.0) is materially worse.

So there is no LONDON evidence supporting 2.0 or 2.2, and none supporting a buffer. **"No separable change" is the legitimate outcome** — the D5 ruling's own framing, applied here.

**A caveat on the depth gate, which corrects the seat-close handover.** Task 1(c) reported *"W6-1 depth: GO — 540 pooled weekday LONDON directional rows (the prior evidence base was ~227)"*; I re-verified that count as **556**. But the instrument that actually decides W6-1 sees **LONDON n=215**, after the `BELOW_MIN_MOVE` exclusion (112 LONDON rows) and the placed-geometry filter. That is **not deeper than the ~227 the original F3 read used** — it is marginally shallower on a different window. **The raw-row depth gate is not the depth the ruling gets**, and future gates on this item should count evaluable rows, not directional rows.

---

## 3. B4b §12 post-ship watch — read, with two items unrunnable

| # | Item | Result |
|---|---|---|
| **(2)** | STOP_CLAMPED frequency | **NY 99.6% · LONDON 95.5% · ASIA 96.0%** of directional rows carrying a structural stop. Stop-distance/ATR ratio has **p50 = p90 = 1.60 exactly**. The §12 expectation was "binds on MOST rows at v1 fixed sizing" — it binds on essentially all, and harder than the 92% the original W6-1 evidence recorded. |
| **(3)** | BELOW_MIN_MOVE rate | **NY 23.35% · LONDON 15.16% · ASIA 18.28%** of all pooled weekday post-07-08 rows. Reported as absolute rates: the §12 projection was a *delta* ("+4–6pp NY, ~0 elsewhere") against a pre-v51 baseline that this book cannot reconstruct, so the two are not directly comparable. Recorded as the new baseline. |
| **(1)** | Structural vs fallback **reach-rate** | ⚠ **NOT RUNNABLE — no instrument produces it.** |
| **(4)** | **F3 LONDON structural-target inversion** (trigger: still <45% after ≥3 more LONDON session-days) | ⚠ **NOT READ — same reason. The trigger cannot currently be evaluated.** |

**Why (1) and (4) are unrunnable, which is itself the finding.** Both require outcomes segmented by **cap bucket** (`TargetCapReason` = swing/hvn = structural vs none = fallback). Nothing produces that segmentation any more:

- `FailureRateMatrix` is `(tier × window)` — the placed-target migration of 2026-07-21 **deliberately retired the geometry axis** ("the per-tier ATR grid retired, so the cell space is (tier × window) — one placed-geometry cell").
- The what-if runner reads `TargetCapReason` only to **exclude** `poc` rows (`WhatIfProgram.vb:91-92`).
- `CeilingAudit` carries it only as an info-categorical for an AUC column.

**The F3 watch outlived its instrument.** The migration that made the eval stack coherent removed the axis this watch was written against, and nobody noticed because the watch's next read was deferred to W6-1 — which is now. Either F3 gets a cap-bucket segmentation added to some offline surface, or it should be retired explicitly rather than left as a live trigger nobody can evaluate. ~~**Flagged, not ruled**~~ ✅ **RULED 2026-08-12 (trader).**

> ## ✅ THE F3 WATCH IS RETIRED — 2026-08-12, trader-directed.
>
> **Retired explicitly, not archived and not stranded.** ⛔ **No cap-bucket segmentation is to be built for it.** The F3-watch tooling row in [`trader-tick-queue.md`](trader-tick-queue.md) §2 is **cancelled**.
>
> ⚠ **`F3` here means the B4b post-ship watch item (4) — the LONDON structural-TARGET inversion trigger, *"still under 45 % after ≥3 more LONDON session-days"*. It is not a coverage-report finding ID.** Other spec-backs reuse `F3` for unrelated findings; always name the document.
>
> ⚠ **What is retired is the TRIGGER, not the TOPIC.** *Does a LONDON structural target actually get reached?* is still open and still has no instrument. If it is ever wanted, it returns as a **scoped instrument request with its own spec** — never again as a live watch nobody can read.
>
> **What survives and stays usable:** the denominator above — LONDON directional placement **structural 41.0 %** vs **fallback 59.0 %** — is computable from the CSV today. The **reach rate** is the half with no instrument.
>
> ⚠ **Do not cite §4's 95.5 % ATR-clamp figure as the reason for this retirement.** That figure is about structural **stops**; F3 is about structural **targets**. The two are different questions and conflating them would make this ruling look better-evidenced than it is. **F3 was retired because it is unreadable, not because it was answered.**

What *can* be said from the CSV alone: LONDON directional placement is **structural 41.0%** (swing 33.6% + hvn 7.4%) vs **fallback 59.0%**. That is the denominator of F3, not its reach.

---

## 4. The real W6-1 finding — the multiplier was never the question

`min(structural, 1.6×ATR)` resolves to the **ATR clamp on 95.5% of LONDON rows and 99.6% of NY rows**, with the distance ratio pinned at exactly 1.60 at both p50 and p90. **The engine is running ATR stops in practice.** The structural stop — the thing B4b was built to deliver, and the thing the trader profile explicitly prefers over ATR stops for execution — is operative on roughly one row in twenty.

This is not a defect and not news in kind: the placed-geometry derivation's F1 said so ("5m structural STOPS are inoperative at fixed sizing", p50 4–9×ATR), and DG1 chose the clamp deliberately for v1. What is new is the **magnitude** — 95.5–99.6%, tighter than the 92% on file — and what it implies for W6-1: **tuning the clamp multiplier is tuning the ATR-stop regime, not the structural one.** Widening it to 2.0–2.2 would not make stops more structural; it would make the ATR stop wider, which §2 shows is neutral-to-worse.

**The question W6-1 was really asking belongs to L9 (structural-stop un-clamp), which is gated on L3 (consumer sizing-by-stop-distance).** Until sizing can vary with stop distance, a structural stop at 4–9×ATR is unusable at fixed size, and the clamp is the only thing making the geometry shippable. W6-1 should close as **no change**, and the live question should be tracked where it can actually be answered.

---

## 5. What I did not verify

- **No LONDON-specific EV separation exists at this n**, and I did not manufacture one. Any future W6-1 re-read should gate on **evaluable** LONDON rows (n=215 today), not raw directional count.
- **Touch-based barriers.** Guard-rail 3: mid-price wick touches on 1m OHLC — no fills, no slippage, no queue position. Real execution is worse than every EV printed here. W6-6 closes that loop.
- **All EVs are negative** on this book, net of fees, across every cell and both runs (LONDON −0.155, pooled −0.475). That is a property of the whole evaluated population — all directional rows, not the tier-filtered subset a trader would act on — measured on a touch basis. It is context for "widening a stop in this regime increases loss per trade", **not** a claim about realised trading performance, and I did not investigate it further. It deserves its own look.
- **F3 and the structural-vs-fallback reach**, per §3 — unrunnable, not merely unread.
- **The overfit counter now records these two runs** against this book span. Guard-rail 2 applies: ~15 cells per run, so ≈0.8 phantom winners expected at a 95% bar. The ruling here is *no change*, which is the outcome least exposed to that risk.
