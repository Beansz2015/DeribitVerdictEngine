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
  p(win) is ASSUMED from the confidence tier — Actual numbers after next book doubling.
  Treat as directional bias indicator only.
  <net R:R line>
  p(win) [EST]:   65.0%
```

**Wording set by the trader 2026-08-02.** It is better than the draft it replaced ("not measured, and currently unvalidated") because it gives the reader a **horizon** rather than an open-ended disclaimer — the question "so when does this become real?" is answered on the line itself.

**It also changes the line's nature, and that is why §2.1 exists.** The string now makes a **forward promise**. If the doubling arrives and nobody re-reads, the display becomes untrue — a stale commitment rendered on screen, which is a worse failure than the vagueness it replaced. **The watch is not an optional companion to this wording; it is what keeps it honest.**

Still deliberately carries **no measured numbers** — a string with `46.8 %` in it goes stale the moment the book grows, which is the exact failure this session spent its time correcting.

### 2.1 The watch this wording obliges — **trigger derived, not estimated**

Doubling is measured against **F1's own basis**, since this line is downstream of F1: **pooled weekday STRONG**, currently **201** (evaluable 203 after post-filters).

**Trigger: ≥ 406 pooled weekday STRONG** on the AWS-preferred deduped book.

Accrual measured on the current two-box topology rather than assumed: **12.4 STRONG/weekday** across the 8 weekdays since AWS came up 2026-07-22 (the whole-book rate of 10.1 understates it, because it averages in the single-box era). So a doubling needs ~201 more at 12.4/weekday ≈ **17 weekdays ≈ 4 calendar weeks — ETA ~2026-08-30.**

**W6-4's re-run lands in the same window** on its own separate basis (2,712 eligible rows), so **the two should bundle** — one pooled freeze, one re-read session, both instruments. That also keeps the overfit counter honest, since both consume the same book span.

**On trigger:** re-run the §9 band ladder ([`f1-tier-ladder-read-2026-08-01.md`](f1-tier-ladder-read-2026-08-01.md) recipe — `BacktestRunner report --csv <pooled>`), then re-work §2.1's f\* table at the then-current `b`. **Outcomes and what each obliges:** ladder separates **and** STRONG clears the 47.76 % breakeven ⇒ CAL becomes arguable, and this advisory line is retired with the mode tag flipping to `[CAL]` on its own. Ladder still flat, or STRONG still below breakeven ⇒ **the line must be re-worded or the block suppressed** — what it must *not* do is silently promise another doubling.
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
