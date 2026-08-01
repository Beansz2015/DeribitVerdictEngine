# Kelly EST — decision of record + exact display strings (2026-08-02)

**Decision (trader, 2026-08-02):** **option (c) for the measurement — keep waiting for tier separation — and render the assumption honestly now.** The two halves were deliberately separated: only the measurement needs more book.
**Evidence:** [`f1-tier-ladder-read-2026-08-01.md`](f1-tier-ladder-read-2026-08-01.md) §2.1–§2.2.
**Type:** display-only. **No scoring impact, no settings keys, no version bump, no dataset boundary.** ⚠ **Both render surfaces in one commit** — the display-string parity rule, which has three recorded drift instances.

---

## 1. What is being fixed

`p(win): 65.0%` reads as a measured probability. It is an **assumption from a tier map** — `est_prob_floor 0.45` + `est_prob_scale` by confidence (HIGH 0.65 / MEDIUM 0.55 / LOW 0.45) — and the book measures the corresponding rates at **46.8 % / 42.2 %**, 13–18 points lower. The display asserts an edge the evidence does not support, and does so in the optimistic direction.

Two further facts that shape the wording rather than the decision: **the tier ladder is already invisible in the output** (HIGH and MEDIUM both exceed the 5 % cap after halving, so both render identical applied sizing), and **breakeven is 47.76 %** at `b = 1.0938`, so every measured tier sits below it.

## 2. Exact strings — sign these off before the code lands

**Current, both surfaces:**

```
KELLY SIZING
  Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets.
  Treat as directional bias indicator only.
  <net R:R line>
  p(win):   65.0%
```

**Proposed:**

```
KELLY SIZING
  Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets.
  p(win) is ASSUMED from the confidence tier — not measured, and currently unvalidated.
  Treat as directional bias indicator only.
  <net R:R line>
  p(win) [EST]:   65.0%
```

**Two changes, and that is all:**

1. **One advisory line inserted** after the ATR-basis line. Deliberately carries **no numbers and no dates** — a display string with `46.8 %` in it goes stale the moment the book grows, which is the exact failure this session spent its time correcting. "Assumed, not measured, currently unvalidated" stays true whatever the next re-read says.
2. **The `p(win):` label gains the mode tag**, sourced from the existing `v.KellyPMode` field (already populated, already in the bridge payload) rather than a literal. So when CAL eventually ships it renders `p(win) [CAL]:` **automatically**, and retiring the advisory line is then the only remaining edit.

## 3. Sites

| Surface | File | Change |
|---|---|---|
| Plaintext snapshot | `UI/MainForm_PlaintextSnapshot.vb` | insert after `:251`; relabel `:254` |
| Card | `UI/MainForm_Render_Cards.vb` | `BuildCardAdvisory` at `:1574` takes the new line; `BuildCardKvRow` label at `:1580` |

**Checked, not assumed:** `BuildCardAdvisory` is declared `ParamArray lines As String()`, so a fourth line is free — no signature change, no deviation, and the two surfaces stay in lockstep by construction.

## 4. Acceptance

Both surfaces byte-identical in wording. Existing Kelly fixtures unregressed. Six Release builds 0/0; `verify-gate prepush` **GATE PASSED post-commit** (pre-commit runs the parity guard vacuously — the v64 F5 lesson). **The commit message must state that both surfaces moved together**, per the hard rule.

No settings keys ⇒ no version bump; the version-bump guard is WARN-only on the engine path and the `[no-engine-change]` token does not apply here since UI files are touched — expect the WARN and state why in the message.

## 5. What this does NOT do

- **Does not change sizing.** Every number rendered is identical; only labels and one advisory line move.
- **Does not resolve F1.** The measurement stays option (c) — re-read at the next book doubling. This change exists so the display stops overstating while that runs.
- **Does not touch `KellyPMode`'s value**, the bridge payload, the CSV, or any consumer. The order app reads `direction` + `confidence`, neither of which moves.
