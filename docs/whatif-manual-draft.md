# What-If Replay — manual draft (for review)

> **Status:** DRAFT for trader review. Once approved, this gets condensed into `TraderGuide.md`
> (§17 "Working with the App", after Settings Snapshots) and expanded into `UserManual.md`
> (new §20, after "Tweak Settings & Auto-Tweaker"), then both PDFs regenerate.

---

## What it is

**What-If Replay** is an offline backtesting tool. It takes the runs the engine has already
logged (`analysis_log.csv`) plus 1-minute price history, applies a **hypothetical settings
change** (an "overlay"), and re-computes — on the exact same historical rows — what levels the
engine *would* have placed and what verdict it *would* have called under that change. It then
re-walks each trade's outcome against the price that actually followed and prints a
**baseline-vs-overlay** report: the current settings on the left, your hypothesis on the right,
same rows both sides.

It answers questions the live "calibrate forward and wait" loop can't answer quickly, e.g.
*"would a wider LONDON stop have helped on the book so far?"* or *"is the MEDIUM threshold too
tight?"* — as a repeatable, guard-railed instrument instead of a one-off hand analysis.

**Where:** SETTINGS & TOOLS card (bottom of the main window) → **What-If Replay** link → opens a
non-modal dialog. The report opens in the same **Analysis Report Viewer** the Analysis Report
button uses.

**What it is NOT:** it never changes the engine. It does not write `settings.json`, place orders,
or touch the live verdict. A promising result is evidence for a *spec proposal* — the normal
spec-first + test + own-watch discipline still gates any real change. Think of it as a wind
tunnel, not the aircraft.

---

## The fields — what each one is, and where it shows on the app

Every field maps to one engine setting (except **Eval window**, which is a backtest-only
measurement dimension). Leaving a field **blank** means "use whatever the engine runs live" — so
you only fill the ones you want to test.

| Field | Setting it overrides | What it controls | Where you see its effect on the app |
|---|---|---|---|
| **ATR target mult** | `scoring.atr_target_multiplier` (live 1.75) | The **fallback** take-profit distance, in multiples of ATR — used only when no structural target (swing / HVN) qualifies. | **ATR Entry Levels** card → `TARGET` on `FALLBACK_ATR` rows; the header's `…x target`. |
| **ATR stop mult** | `scoring.atr_stop_multiplier` (live 1.6) | The **fallback** stop distance (×ATR) — used **only** on rows with no usable structural stop. *Not* the clamp ceiling (that's **Stop max ×ATR** below); the two are equal by default but independent. | **ATR Entry Levels** → `STOP` on **`[FALLBACK_ATR]` rows only**; header's `…x stop`. |
| **Verdict STRONG %** | `scoring.verdict_strong_pct` (live 0.70) | Fraction of the regime's max score at/above which the call is **STRONG**. | **Verdict** headline (`STRONG LONG` / `STRONG SHORT`); **Score** gauge tier. |
| **Verdict MED %** | `scoring.verdict_med_pct` (live 0.53) | Threshold for a **MEDIUM** call (plain `LONG` / `SHORT`). | **Verdict** headline. |
| **Verdict WEAK %** | `scoring.verdict_weak_pct` (live 0.35) | Threshold for `WEAK LONG` / `WEAK SHORT`; below it the dominant side is `NO TRADE`. | **Verdict** headline. |
| **Min tradeable move %** | `scoring.min_tradeable_move_pct` (live 0.0008) | The smallest take-profit distance (as a fraction of price) a directional call must clear on its **placed** target — or it's overridden to `NO TRADE`. | **Verdict** flips to `NO TRADE`; **Context** shows `BELOW_MIN_MOVE`. |
| **Target max ×ATR** | `scoring.structural_levels.target_max_atr_mult` (live 3.5) | The looseness bound: a structural target (swing / HVN) only places if it sits within this ×ATR of entry; past it, the ATR fallback is used. | **ATR Entry Levels** → whether `TARGET` reads `PLACED @ … (SWING/HVN)` vs `FALLBACK_ATR`; **Structural · 5m Pivots** target rows. |
| **Stop max ×ATR** | `scoring.structural_levels.stop_max_atr_mult` (live 1.6) | The stop **clamp ceiling**: placed stop = min(structural swing stop, this ×ATR). Binds on **most** structural rows (5m swing stops usually run wider than the ceiling). *(The registered W6-1 LONDON 2.0 / 2.2 candidate.)* | **ATR Entry Levels** → `STOP` label: `SWING_STOP` when structure is tighter, `STOP_CLAMPED` when this ceiling binds. |
| **Stop min floor ticks** | `scoring.structural_levels.stop_min_floor_ticks` (live 4) | Degenerate-tightness floor: a structural stop closer than this many ticks ($0.5 each) is rejected for the ATR fallback. | **ATR Entry Levels** → `STOP` (rare; guards absurdly tight stops). |
| **NY / LONDON / ASIA fallback ×ATR** | `scoring.structural_levels.sessions.{NY,LONDON,ASIA}.fallback_target_atr_mult` (live: NY inherits 1.75, LONDON 2.0, ASIA 1.25) | Per-session override of the fallback target multiplier, applied when the run's UTC hour falls in that session. | **ATR Entry Levels** header `…x target` + `TARGET` on `FALLBACK_ATR` rows during that session. |
| **Eval window (bars)** | *(not an engine setting — backtest only)* | How many bars forward the replay walks to score each trade for the **EV ranking** (5 / 10 / 15, scaled by the row's execution resolution — so 15 bars = 45 min on a 3-min Asia/London row). | *(No app element — see "How outcomes are scored" below.)* |
| **Constraints** | *(backtest only)* | Optional rules that prune grid combinations before they run (e.g. keep two knobs coupled). Raw JSON — see the example below. | *(No app element.)* |
| **From / To** | *(backtest only)* | Limits which logged rows the replay runs over, by date (`yyyy-MM-dd`). Blank = the whole book. | *(No app element.)* |

### Field nuances — read before you sweep

Several fields don't behave the way the surface reading suggests. These are the ones that will
mislead a backtest if you don't know them.

**Verdict STRONG / MED / WEAK %.** The threshold is `⌈maxScore × pct⌉` — the fraction of max
score, **rounded up to a whole score** — and the test is `effectiveScore ≥ threshold`. So `70%`
of a max-15 regime is `⌈10.5⌉ = 11`, *not* 10.5: a score of 11 is STRONG, 10 is not. And the max
isn't fixed — it's the **regime's** ceiling (live values, with regime-alignment weighting on):

  | Regime | Max | STRONG (≥) | MED (≥) | WEAK (≥) |
  |---|---|---|---|---|
  | TRENDING | 20 | 14 | 11 | 7 |
  | RANGE_BOUND | 19 | 14 | 11 | 7 |
  | TRANSITIONAL | 15 | 11 | 8 | 6 |

  (The `/ 20` on the live **Score** gauge is the trending max.) Three consequences for sweeps:
  - **Sub-step no-ops.** Because the threshold is an integer, a `%` change that doesn't cross a
    `⌈max×pct⌉` boundary yields the *same* threshold and an identical cell. Sweeping `MED %`
    `0.50:0.56:0.01` on a max-19 regime gives threshold 10 for 0.50–0.52 and 11 for 0.53–0.56 —
    seven inputs, two real outcomes. Step in whole-score-sized jumps or expect duplicate rows.
  - **Keep them ordered.** The tier walk checks STRONG → MED → WEAK. Set `MED % > STRONG %` and
    the labels go incoherent. Keep `strong ≥ med ≥ weak`.
  - **What's compared** is the **dominant side's *effective* score** (after regime/MTF
    adjustments), only on the dominant side. The overlay re-tiers the logged scores; it never
    rebuilds the raw Step-2 tally.

**ATR stop mult vs Stop max ×ATR — the one to internalise.** Both default to 1.6, but they are
different knobs hitting different rows:
  - **Stop max ×ATR** is the *clamp ceiling* for structural stops. Most 5m swing stops run wider
    than 1.6×ATR, so most structural rows come out `STOP_CLAMPED` at this value → **this knob
    moves the stop on the majority of directional rows.** It's the high-leverage stop lever.
  - **ATR stop mult** is the *fallback* stop, used only when there's no usable structural stop
    (`FALLBACK_ATR` rows) → it moves a minority. Sweeping it and seeing little change in the
    matrix isn't a null result — it's that few rows use the fallback.
  - So to test "wider stops," sweep **Stop max ×ATR**.

**ATR target mult.** Same fallback caveat: it sets the target *only* where no swing/HVN qualifies
(`FALLBACK_ATR`). In a structured tape most targets sit on structure, so this knob has a narrow
footprint — the session overrides below are where the fallback target is actually tuned.

**Target max ×ATR.** An *eligibility bound*, not a level. It decides *whether* a structural target
may place (it must sit within this ×ATR of entry); it never moves the level itself. Raise it and
*farther* structural targets place (structure wins even when farther than the ATR fallback, up to
this bound); lower it and more rows fall back to ATR. Its real effect is the **structural-vs-
fallback mix** of your targets — which shifts R:R and can trip the min-move gate (a tight
structural target it lets through may then be vetoed as below-min-move).

**Stop min floor ticks.** A guard, not a lever. It rejects a structural stop tighter than N ticks
($0.5 each) for the fallback. Structural stops are almost never that tight, so **expect a sweep of
this to show ~no change.** Integer input.

**NY / LONDON / ASIA fallback ×ATR.** Session-scoped *and* fallback-only: each overrides the
fallback target multiplier **only for rows in that session** (by the run's UTC hour) **and only on
`FALLBACK_ATR` rows.** NY's value touches only NY×1 rows, LONDON's only LONDON×3, etc. Watch what
"blank" inherits: NY blank → the global 1.75, but **LONDON blank → 2.0 and ASIA blank → 1.25**
(those overrides are already live). To test "no session override," pin the field to the global
value — don't blank it.

**Min tradeable move %.** A fraction of **price**, not ATR (`0.0008 ≈ $49.6` at $62k; auto-scales
with BTC). It's checked against the **placed** target distance, so a near swing target trips it
the same as a small ATR target does — turning a technically-directional but untradeably-small call
into `NO TRADE` (Context `BELOW_MIN_MOVE`). Because it acts on the placed target it's coupled to
the geometry knobs: lower `MED %` to add trades and see the population barely grow, and the
min-move gate is usually vetoing the small ones.

**Eval window (bars).** Affects **only the EV-in-ATR ranking** used to pick a grid winner. It does
*not* change any placed level or verdict, and it does *not* change the baseline-vs-overlay failure
matrix (which always reports all three windows). So on a **single-cell** run it has no visible
effect; it matters only when a sweep must choose a winner. Values are a **bar-count budget**
(5/10/15) scaled by execution resolution — 15 bars is 15 min on NY (1-min) but 45 min on
Asia/London (3-min).

**How the population moves.** Lowering `MED %` (or `STRONG %`/`WEAK %`) promotes borderline rows to
a tradeable tier; raising it demotes them toward `WEAK` / `NO TRADE`. That change in *how many*
trades qualify — after the min-move gate takes its cut — is what the report's **Population shift**
line makes explicit. A pure geometry change (the stop/target knobs) usually leaves the population
untouched and moves only the win/adverse rates.

---

## How the backtesting works

### The one rule: blank / single / sweep

Each field accepts one of three things, and that's the whole input language:

- **Blank** → *inherit the live value.* The knob stays at whatever the engine runs today.
- **A single number** (e.g. `2.0`) → *pin* that knob to that value for the whole run.
- **A range `from:to:step`** (e.g. `1.6:2.4:0.2`) → *sweep* that knob across those values
  (here: 1.6, 1.8, 2.0, 2.2, 2.4).

### Single values vs sweeps vs combinations

The runner builds a **grid** — every combination of every field's values (the cartesian
product) — and runs each combination ("cell") as a full backtest. What you get depends on how
many fields you sweep:

- **All fields blank or single** → **one cell.** This is the plain backtest: baseline (live
  settings) vs your one hypothesis, side by side on identical rows. Use this to test a single
  concrete idea ("what if LONDON stop_max were 2.0").

- **One field swept, the rest blank/pinned** → a **1-D sweep.** The runner tries every value of
  that one knob with everything else fixed, then ranks them. Use this to find the best value of a
  single knob ("sweep stop_max from 1.6 to 2.4").

- **Two or more fields swept** → a **grid** (a 2-D, 3-D, … surface). It runs *every combination*
  — sweep `MED %` over 4 values and `Min move %` over 4 values and you get 4 × 4 = 16 cells. Use
  this to explore how two knobs interact.

Every cell is a **complete, runnable overlay** — the swept/pinned fields plus the live values for
every blank field — so a "winning" cell is always a full settings combination you could actually
propose, never a bare number floating on its own.

**Cap:** a grid is limited to **3,000 cells**. Above that, compute climbs (the replay re-walks
every row per cell — ~9 s at 1,000 cells, ~23 s at 3,000 on the current book, and it grows as the
book does) and multiple-comparisons risk mounts. If a sweep would blow past it, narrow a range or
pin more fields. The ranking table shows only the **top 50 cells** regardless of grid size (the
winner is always rank 1), so the report stays readable; the overfit banner still states the full
cell count evaluated.

### Constraints (optional)

When you sweep two related knobs, you often don't want *every* combination — only the ones that
make sense together. A **ratio constraint** prunes cells before they run. Example: to keep the
fallback stop and the structural-stop ceiling equal while sweeping both, add:

```json
[{"ratio":{"of":["scoring.structural_levels.stop_max_atr_mult","scoring.atr_stop_multiplier"],"min":1.0,"max":1.0}}]
```

That keeps only cells where `stop_max_atr_mult / atr_stop_multiplier` is between 1.0 and 1.0 —
i.e. the two are equal — turning a 9-cell square into the 3-cell diagonal. Leave the field blank
if you don't need it.

### Date span (From / To)

`From` / `To` (`yyyy-MM-dd`) limit which logged rows the replay covers. Leave both blank to run
the whole book. A shorter span runs faster (less price history to fetch) and lets you compare a
recent regime against an older one.

### The two buttons

- **Run Replay** — writes the overlay, fetches the price history, runs every cell, and opens the
  report.
- **Open Last Report** — re-opens the most recent report without re-running (handy after you've
  closed the viewer).

The status line at the bottom shows progress and any error (e.g. a knob typo, or an off-list key
the runner refuses).

---

## How outcomes are scored, and how a winner is chosen

For each historical row that the overlay makes a tradeable call, the replay re-places the stop
and target under that overlay, then walks the **actual** 1-minute bars that followed:

- target touched first → a win, worth **+the target distance**;
- stop (or both in the same bar) touched first → a loss, worth **−the stop distance**;
- neither within the window → marked to the price at the window's end.

Distances are measured in **ATR units** so trades of different sizes are comparable. The average
across all trades is the cell's **EV per trade (in ATR)** — the ranking objective. Win-rate alone
is *not* used to rank (a 90%-win / tiny-target setup can lose money); win-rate is shown as
context only. **Eval window** picks how far forward that walk runs.

**Split-half validation** (on any swept grid): the winner is chosen on **half** the book
(alternating session-days) and then re-checked on the **other, unseen half**. If the winner's
performance on the unseen half falls apart, it's flagged **DIVERGENT** — that's the tool telling
you the "win" is probably curve-fitting, not a real edge. **Trust the holdout, not the selection
half.**

---

## Reading the report

The report opens in the Analysis Report Viewer. Top to bottom:

1. **Guard-rail banner** — the four binding cautions (below), including the overfit counter.
2. **Grid ranking** *(swept grids only)* — every cell ranked by EV/trade in ATR, with the
   selection-half and holdout-half figures and any **DIVERGENT** flags, then the winning cell's
   full effective overlay.
3. **Population shift** — for each session, how many directional trades the change adds or
   removes (`baseline → overlay`). A verdict-threshold change moves this; a pure geometry change
   (stop/target) usually doesn't.
4. **Baseline vs overlay failure matrix** — per session × resolution × tier, the current settings
   vs the winning overlay on the *same* rows: success %, adverse-first %, expired %, each with a
   95% confidence interval; cells with fewer than 30 trades are flagged.

### The four binding cautions (printed on every report)

1. **It motivates, it isn't.** A result feeds a spec proposal. The runner never writes settings.
2. **Overfit.** Trying many knobs on one book *will* surface phantom winners. The header states
   how many overlays have been tested against this span and roughly how many false winners to
   expect from noise alone. Treat single-cell wins on a big sweep with suspicion.
3. **Touch-based.** Barriers are mid-price wick touches — **no fills, slippage, or queue
   position.** Real execution is worse than the report shows.
4. **POC rows excluded.** The volume-profile POC inputs aren't logged, so rows whose live target
   was placed on the POC tier are excluded and counted in the header. (Near-zero in practice.)

Only **v0.8+** logged rows (the current CSV schema) are replayed — no legacy fabrication.

---

## Worked examples

**A. Test one idea (single cell).** Put `2.0` in **Stop max ×ATR**, leave everything else blank,
set `From` = a recent Monday, `To` = today, Run. → baseline (live 1.6) vs 2.0 on the same rows;
read the LONDON row of the failure matrix. *(This is exactly the W6-1 LONDON `stop_max` question.)*

**B. Find the best value of one knob (1-D sweep).** Put `1.6:2.4:0.2` in **Stop max ×ATR**, rest
blank. → 5 cells, EV-ranked, split-half validated. The winner is the value with the best
*holdout* EV, not just the best headline number.

**C. Explore two knobs together (2-D grid).** `0.45:0.60:0.05` in **Verdict MED %** and
`0.0006:0.0012:0.0002` in **Min tradeable move %**. → 16 cells; watch the population-shift line —
loosening `MED %` adds trades, and the min-move floor decides how many of those survive.

**D. Coupled sweep (constraint).** Sweep both **ATR stop mult** and **Stop max ×ATR** over
`1.6:2.4:0.2`, and paste the ratio-constraint above to keep them equal. → the 9-cell square is
pruned to the 3-cell diagonal, so you test "both stops move together" rather than every mismatch.

---

## The short version

- Blank = leave it live. One number = pin it. `a:b:c` = sweep it.
- Sweep one knob → find its best value. Sweep several → explore the combinations. Everything runs
  against the *same* logged rows, current settings on one side, your idea on the other.
- Rank by EV-in-ATR, and **believe the split-half holdout, not the headline.**
- It never changes the engine. A good result is a reason to *propose* a change and watch it live —
  not a change itself.
