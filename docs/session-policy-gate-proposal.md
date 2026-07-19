# Session Policy Gate — Per-Session Tier/Context Subsets + Size Multipliers (Proposal)

**Date:** 2026-07-18 · **Status:** **APPROVED — P1–P5 ALL TICKED 2026-07-18 (as recommended). HANDOFF-READY: give this doc to the order-app coordinator** (the bridge-contract pattern) · **Engine impact: ZERO** — no boundary, no settings keys, no emission change.
**P2 UI note (trader expectation, recorded at sign-off):** the context selector (`any` / specific tags) and the per-session fields join the order app's EXISTING tier-gate settings section — one policy surface, not a new screen; the §2 config block is the contract, the UI grouping is the implementer's layout call within that expectation.
**Driver:** trader 2026-07-18 — "MEDIUM seems more accurate than STRONG; I want the choice to trade specific subsets per session." Part of the profitability/risk ladder (`profitability-risk-levers.md` L1/L2).

## 1. Why consumer-side (the design decision)

- **Thresholds can't say "MEDIUM yes, STRONG no."** Engine verdict thresholds truncate from below; subset selection needs set membership, not a bar.
- **R1 already assigns this here.** The 2026-07-02 ruling: the order app keeps execution-policy gates — cooloff, max-age, **tier selection**. This spec is tier selection grown up: per-session sets, plus context tags, plus a size multiplier.
- **The engine's book stays whole.** All verdicts continue to be emitted/logged/evaluated; refused signals are dispositioned, so the counterfactual ("what would LONDON STRONG have done") keeps measuring — the evidence to *revise* the policy never stops accruing. An engine-side suppression would blind exactly the cells you need to watch.
- Everything needed is already in the payload: verdict tier, confidence, context tag, `generated_at_utc`.

## 2. Mechanism (order-app config, evaluated at signal receipt)

```json
"session_policy": {
  "enabled": true,
  "sessions": {
    "NY":     { "tiers": ["STRONG","MEDIUM"], "contexts": "any",           "size_mult": 1.0 },
    "LONDON": { "tiers": ["MEDIUM"],          "contexts": ["CONFIRMED"],   "size_mult": 0.5 },
    "ASIA":   { "tiers": ["STRONG","MEDIUM"], "contexts": "any",           "size_mult": 0.75 }
  }
}
```

- **Session derivation:** from `generated_at_utc` hour, using the ENGINE'S buckets, pinned here to prevent drift: **ASIA 00:00–07:59 · LONDON 08:00–12:59 · NY 13:00–23:59 UTC.**
- A signal failing its session's tier or context set → **no action + disposition `refused: policy(<session>/<tier-or-context>)`** — distinct from `refused:` (other gates) and `rejected:` (API), so the soak/ledger joins can quantify what the policy declined and what it would have done.
- `size_mult` multiplies into whatever sizing formula is current (fixed-min today; the stop-distance formula later — it becomes the `sessionFactor` term there, ONE formula, no stacking of hidden multipliers).
- Defaults when a session/key is absent: `tiers ["STRONG","MEDIUM"]` (WEAK stays refused as today), `contexts "any"`, `size_mult 1.0` — i.e., the block is opt-in restriction; absent config = current behaviour.

## 3. Evidence discipline (binding)

The subsets follow the measured book, not impressions: the per-(tier × session) cells of the placed-target matrix (M1–M5) are the instrument. Today's basis for "MEDIUM > STRONG" is thin — NY STRONG n=24/16 with CIs spanning ~40pp vs MEDIUM n=118/86 — so the first policy should be **conservative and reversible** (it's config), and revised at each audit re-run / matrix regeneration, not tuned week-to-week by feel. Log-derived policy-refusal outcomes (the counterfactual cells) are first-class evidence in that review.

## 4. D-table (trader sign-off)

| # | Decision | Recommendation |
|---|---|---|
| **P1** | Home | **Consumer-side** (R1-native; engine book stays whole). Engine-side per-session thresholds rejected for the subset use-case — record as decision-of-record if ticked |
| **P2** | Config shape | §2 as shown (per-session tier set + context set + size_mult; opt-in restriction defaults) |
| **P3** | Disposition | `refused: policy(...)` distinct token; counted separately in the soak review |
| **P4** | Session buckets | Pinned §2 table (engine-identical) |
| **P5** | First live policy | Trader sets values at the live-ladder step (post-soak-review). Suggested starting point, conservative: NY unchanged; LONDON `MEDIUM`+`CONFIRMED`-only at 0.5×; ASIA unchanged at 0.75× — all revisable at the first matrix regeneration |

## 5. Acceptance (order-app side)

Config parse + defaults; bucket derivation across boundary hours (07:59/08:00, 12:59/13:00); refusal disposition token in the log; size_mult reaching the order size exactly once; `enabled:false` byte-identical to today. Their harness per the shared recipe (`ui-automation-harness-recipe.md` §0 layer 1).
