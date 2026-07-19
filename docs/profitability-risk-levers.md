# Profitability / Risk Levers — the Sizing & Selectivity Ladder

**Created:** 2026-07-18 (Fable seat; records the sizing synthesis the trader commissioned 2026-07-17). **Role:** the trader's tracking surface for the sub-project "handle low-accuracy sessions with something smarter than not-trading." **Sequencing authority stays `roadmap.md`** (§5/§W6) — this doc is the profitability LENS: what each lever does, where it lives, its state, and its next action. Update the table as levers land; the roadmap links here.

## 0. The organizing idea

Accuracy (p), payoff (b), size (f), and participation (which signals you take) are **one Kelly optimization**, not four independent dials: `f* = (p·b − q)/b`. Sizing is the continuous version of not-trading — it reaches zero exactly when edge does, and comes back the same way. Worked example at placed geometry:

| Case | p | b | f* | Half-Kelly → size |
|---|---|---|---|---|
| NY MEDIUM | 0.52 | 1.1 | 8.4% | 4.2% → full size |
| LONDON at 45% | 0.45 | 1.25 | 1.0% | 0.5% → ~min size |
| LONDON at 42% | 0.42 | 1.25 | negative | zero — Kelly says stand down |

The programme's converging state: B4b/D6 made **b** honest (placed geometry, executed stops); the M1–M5 matrix migration makes **p** measurable per (tier × session); W6-3 turns (p, b) into **f**; the bridge carries f to execution; the session policy gate handles **participation**. Guardrail (binding on the eventual consumer sizing spec): levers multiply — session × tier × Kelly × stop-distance must resolve into **ONE formula with a floor and a capped total reduction**, and any geometry change (stops/targets) re-derives the sizing inputs with it (b moves when stops move — the fit-given coupling rule).

## 1. The ladder

| # | Lever | What it does | Lives at | State / gate | Next action |
|---|---|---|---|---|---|
| **L1** | Session size multipliers | Coarse per-session exposure (LONDON 0.5× etc.) | Order app — `session-policy-gate-proposal.md` §2 `size_mult` | **APPROVED 07-18 (P1–P5) — handed to the order-app coordinator** | Order app implements; policy values set at the live-ladder step |
| **L2** | Tier/context subsets per session ("trade MEDIUM only", "CONFIRMED only") | Participation filtering with the counterfactual still measured (`refused: policy` dispositions) | Order app — same spec (context selector joins the existing tier-settings section, P2 note) | **APPROVED 07-18** | Same lane |
| **L3** | Stop-distance sizing | Risk-constant contracts = riskBudget / stopDistance; absorbs L1 as its sessionFactor | Order app — own small spec (queued in roadmap W3/Q4 notes) | Queued: after live-at-min-size stabilises | Order-app spec; unlocks L9 |
| **L4** | Kelly CAL (EST→CAL) | Automates L1 from measured per-(tier×session×res) win rates; payload kelly fields become empirical | Engine — roadmap **W6-3** | **M1–M5 TICKED 07-18 → matrix migration builds now**; then eval-book depth per cell | Build M-migration (Opus, medium); size the book-depth gate at pickup |
| **L5** | LONDON geometry (stop bound 2.0–2.2×ATR; targets already session-scoped) | Trades win-rate vs payoff; EV-arbitrated, not accuracy-offsetting | Engine settings — roadmap **W6-1 named candidates**; evidence via the what-if runner (its first registered use case) | Evidence-gated: W6-1 audit re-run | Run the what-if LONDON grid when deciding |
| **L6** | LONDON calibration debt (is the low accuracy even real?) | Fixes p itself — divergence gates, session-scoped norms baselines | Roadmap **W6-1** | Pickable at any audit re-run; post-v53 book accruing | Precedes any permanent session verdict |
| **L7** | Direction-conditional filtering (e.g., LONDON longs only) | Finer participation than L2; report hints MEDIUM_LONG LONDON 32% failure vs shorts worse (small n) | L2's config (a tier set per side would extend the shape) if evidence firms | Evidence-pending: migrated-matrix cells at n≥30 | Read at matrix regenerations |
| **L8** | Regime/session re-weighting of existing indicators | Changes p by combination, not new signals | Roadmap **W6-5 (B1)** | Gated: W6-4 ceiling audit | Per roadmap |
| **L9** | Structural-stop un-clamp + swing-buffer offset | The end-state: stops = the trader's real method, size normalized by L3 | Roadmap §6b / placed-geometry derivation + W6-1 candidate #2 | Gated: L3 shipped (+ ideally a calm-regime re-derivation) | After L3 |

## 2. Cross-references

- **Q1 of the 2026-07-18 discussion (geometry)** → L5/L9 — and the architecture ruling stands: geometry is engine-side, session-scoped, flows to the order app as PRICES (R2); no per-signal ratio fields.
- **Q2 (position size)** → L1 (now) → L3 (formula) → L4 (automatic).
- **Q3 (other methods)** → L2/L7 (selectivity), L6 (fix p), L5 (geometry), plus the horizon note: LONDON failure falls steeply with window (67→39% across 15→45m in the 2026-07-17 report) — a discretionary-hold input today, an eval-horizon question later.
- **Evidence instruments:** the what-if runner (deliberate counterfactuals, EV-ranked, holdout-validated) and the placed-target matrix (M1–M5 — what actually happened, per cell). Every lever above cites one of the two before moving.
