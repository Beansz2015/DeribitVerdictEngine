# `S2-2` — split `CalcSpread` into `CalcSpreadBps` + `ClassifySpread`: implementation spec

**Status:** ✅ **BUILD-AUTHORIZED 2026-09-06 (UTC) — §4's D-table TICKED IN FULL, every row as recommended.** ⛔ **§4b is the SINGLE AUTHORITATIVE BUILD LIST. Read §3 first, then build from §4b.** §4 is kept as the record of how the decisions were reached.
**Author seat:** Opus, 2026-09-06 (UTC). **Origin:** [`a54a-session2-step1-measurement-2026-09-05.md`](a54a-session2-step1-measurement-2026-09-05.md) §9 (the analysis, trader-requested 2026-09-05) · [`trader-tick-queue.md`](trader-tick-queue.md) §2 `S2-2` row · [`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §4.

**Ruling, in one line:** **`D-1` (a) `Double?` · `D-2` (a) inside `ClassifySpread` · `D-3` (b) leave `HasTopOfBook`, record the residual · `D-4` (a) delete `CalcSpread` · `D-5` (a) keep the names.** Trader-directed 2026-09-06, ticked in full without amendment.

⚠ **All dates in this document are UTC.** The workstation is GMT+8.

> ⛔⛔ **READ §3 BEFORE §4.** The analysis this proposal implements — [`a54a-session2-step1-measurement-2026-09-05.md`](a54a-session2-step1-measurement-2026-09-05.md) §9.2 option (i) — describes the split as `CalcSpreadBps(orderBook) As Double` + `ClassifySpread(bps, wide, tight) As String`. **Written exactly that way, the split silently changes a rendered value on FOUR surfaces.** §3 has the arithmetic. That finding is why this needs a D-table and not a patch.

---

## 0. Model + effort — **Opus, effort HIGH**, one session

⛔ **THE QUEUE'S SIZING IS WRONG AND IS CORRECTED HERE.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 sizes `S2-2` as *"a refactor behind its own spec"* and its pick-order row in [`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §0 already upgrades it from the *"Sonnet, medium"* the queue implies. **This proposal upgrades it again on new evidence, not on caution.**

**Why HIGH.**

- ⭐ **It is the ONLY item in the whole A54a arc where the display-string parity rule is LIVE.** Everything else in that arc was harness-only. `r.SpreadStatus` reaches **four rendered surfaces** (§2), so CLAUDE.md's *engine display-string parity rule* binds: the card bindings must move in the same commit or the commit message must state why no card surface is affected.
- ⛔ **The obvious implementation is wrong in a way that reads correct.** §3. A reviewer reasoning about the split ticks it green; only running it against a degenerate book shows the flip. **This is the third instance in this arc of a spec-described change being unobservable to reasoning** — `A62f` and `A63a` were the first two, both recorded in [`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §5.
- **The classify half gates scoring.** `SpreadStatus = "WIDE"` drives the Step-2 entry penalty (`Core/ScoringEngine_Calculate_Scoring.vb:417`), and `indicators.spread.` is auto-tweaker-tunable. Losing or shifting the classification is a scoring change, not a display change.

**Why NOT higher than one session.** The blast radius is genuinely small and is *measured*, not estimated: **two production call sites, zero fixture call sites, zero settings keys, zero JSON change**. §2 and §6.

**Where the implementer will slip — three traps, all with code in §5:**

1. ⛔ **The degenerate-book `"NORMAL"` seed (§3).** `CalcSpread` writes `spreadStatus = "NORMAL"` **before** its five early-return guards. A split that classifies the returned `0.0` produces `"TIGHT"` instead, because `0.0 <= tight_threshold_bps` (1.5). **Four rendered surfaces move and the card colour flips grey → green.** ⚠ **The fixtures cannot be relied on to catch this, because the implementer writes them too** — §6 therefore names the input shape, not just the assertion.
2. ⚠ **The classification order is load-bearing under misconfiguration.** Shipped tests `>= wide` **first**, then `<= tight`. If a tweaker or a hand edit ever puts `tight_threshold_bps >= wide_threshold_bps`, shipped resolves WIDE and a re-ordered `ClassifySpread` resolves TIGHT — **the opposite scoring outcome**. The order is not stylistic.
3. ⚠ **`HasTopOfBook` is a THIRD copy of the validity predicate and it is WEAKER than `CalcSpread`'s.** `LiveMicrostructureEvaluator.vb:303-307` tests three conditions; `CalcSpread` tests six. **They are not interchangeable** (§5.3). An implementer collapsing them "because they are the same check" changes live-strip behaviour. `D-3` exists to stop that being decided silently.

**Escalation trigger — stop and hand back if:**

- the split requires **any** change to `Core/IndicatorResults.vb`'s `SpreadBps` / `SpreadStatus` field types, **or**
- a rendered string differs from shipped on **any** input, degenerate or not, after the D-table's chosen contract is applied, **or**
- more than the **two** production call sites in §2 need editing.

Each means the blast radius has left this proposal.

---

## 1. What this changes, and why it is worth doing

`IndicatorEngine.CalcSpread` (`Core/Indicators_OrderFlow.vb:581-606`) returns **two** values through `ByRef`, and its two threshold parameters exist **only** to compute the second one. One of its two production callers wants only the first.

**Today, after `S2-1` (B) shipped 2026-09-05:** `LiveMicrostructureEvaluator.vb:140-142` passes `cfg.Indicators.Spread.WideThresholdBps` and `TightThresholdBps` **purely to compute a `sStatus` it never reads**. Correct, guarded, and — in that build's own words — ugly.

**What the split buys.** After it, `LiveMicrostructureEvaluator` references the spread thresholds **not at all**. The tweaker-retune divergence that `S2-1` had to guard becomes **impossible by construction** rather than *currently correct*.

⭐ **That is this project's own strongest pattern, applied a third time:** `SignalEmitter.ComputeSideLevels` (*"one seam, no copies"* — four parity surfaces), `TradeStoreWriter` (*"writer and reader cannot drift"*). **Making the wrong thing unreachable beats guarding against it.** It also turns a comment into a type: `LiveMicrostructureEvaluator.vb:132-134` currently *asserts in prose* that the bps is threshold-independent. After the split the signature says it, and the prose cannot go stale.

**What this is NOT.** It is not a behaviour change, not a settings change, not a dataset boundary. §7 requires that to be **proved on the rendered surfaces**, not asserted.

---

## 2. Verified map — every claim `file:line`, read 2026-09-06 UTC

**Method:** unanchored `grep` over `--include=*.vb`, then each hit read in full. ⚠ **Not line-anchored** — CLAUDE.md's VB grep rule.

### 2.1 The method and its callers

| What | Where | Detail |
|---|---|---|
| The method | `Core/Indicators_OrderFlow.vb:581-606` | `Public Shared Sub CalcSpread(orderBook, ByRef spreadBps As Double, ByRef spreadStatus As String, wideThresholdBps As Double, tightThresholdBps As Double)`. Both thresholds **required** since `S2-1` (B), 2026-09-05 |
| Production call site 1 | `UI/MainForm_Analysis.vb:422-424` | Passes both by name into `r.SpreadBps` / `r.SpreadStatus` |
| Production call site 2 | `LiveMicrostructureEvaluator.vb:140-142` | Passes both by name; reads `sBps` only, discards `sStatus` |
| Fixture call sites | **none** | `grep -rn "CalcSpread" verify/ tools/ analysis/` returns **only compiled `.dll` binaries** — no `.vb` source hit outside the two above. Fixtures set `r.SpreadStatus` directly |

### 2.2 The five consumers of `r.SpreadStatus` — four rendered, one scoring

| # | Surface | Where | What it does with the value |
|---|---|---|---|
| 1 | **Scoring** (not rendered) | `Core/ScoringEngine_Calculate_Scoring.vb:417` | `If r.SpreadStatus = "WIDE"` → Step-2 entry penalty, ROC-directional |
| 2 | **Rendered** — breakdown note | `Core/ScoringEngine_Calculate_Scoring.vb:853` | `String.Format("{0:F2} bps \| {1}", r.SpreadBps, r.SpreadStatus)` |
| 3 | **Rendered** — plaintext snapshot | `UI/MainForm_PlaintextSnapshot.vb:450` | `"  Spread:    {0:F2} bps  \|  {1}"` |
| 4 | **Rendered** — card MiniMeter | `UI/MainForm_Render_Cards.vb:2073-2079` | `If(r.SpreadStatus, "").ToUpperInvariant()`; suffix `· STATUS`; colour via `ResolveSpreadColour` (`:2120-2126`) |
| 5 | **Rendered** — card breakdown row | `UI/MainForm_Render_Cards.vb:3248-3252` | `Select Case If(r.SpreadStatus, "")` → `TIGHT` / `WIDE` / `NORM` + `Theme` colour |

**Non-consumers, confirmed:** `tools/BacktestRunner/ReplayLoop.vb:466` sets `r.SpreadStatus = "NORMAL"` directly and never calls the method — the replay path is unaffected by anything in this proposal.

### 2.3 The three copies of the thresholds — all in agreement today

| Copy | Where | `wide` | `tight` |
|---|---|---:|---:|
| JSON (tracked) | `settings.json:310-313` | 5.0 | 1.5 |
| POCO | `Core/Settings/EngineSettings.vb:649,651` | 5.0 | 1.5 |
| Method default | — | ⭐ **gone**, removed by `S2-1` (B) | — |

⭐ **All copies agree, so any correct implementation of this split is byte-identical on every surface today.** That is what makes the change safe to make and §7's parity proof meaningful.

---

## 3. ⛔ THE CRUX — the split as described flips a rendered value on four surfaces

### 3.1 What shipped actually does

```vb
Public Shared Sub CalcSpread(orderBook As OrderBookSnapshot,
                              ByRef spreadBps As Double,
                              ByRef spreadStatus As String,
                              wideThresholdBps  As Double,
                              tightThresholdBps As Double)
    spreadBps = 0 : spreadStatus = "NORMAL"          ' ← THE SEED, set BEFORE every guard
    If orderBook Is Nothing Then Return
    If orderBook.Bids Is Nothing OrElse orderBook.Bids.Count = 0 Then Return
    If orderBook.Asks Is Nothing OrElse orderBook.Asks.Count = 0 Then Return
    ...
    If bestBid <= 0 OrElse bestAsk <= 0 Then Return
    ...
    If mid <= 0 Then Return
    spreadBps = ((bestAsk - bestBid) / mid) * 10000.0
    ' classification runs ONLY past all five guards
End Sub
```

⭐ **The `"NORMAL"` seed is not a default — it is the DEGENERATE-BOOK ANSWER**, and the five early returns are what deliver it. **Shipped therefore distinguishes two different zeros:**

| Input | Shipped `SpreadBps` | Shipped `SpreadStatus` | Why |
|---|---:|---|---|
| A **locked book** — `bestBid = bestAsk > 0` | `0.00` | **`TIGHT`** | Real measurement. Classification runs, `0 <= 1.5` |
| A **degenerate book** — empty ladder, or a non-positive top-of-book price | `0.00` | **`NORMAL`** | No measurement. Early return, seed survives |

### 3.2 What the naive split does

```vb
r.SpreadBps    = IndicatorEngine.CalcSpreadBps(orderBook)                  ' 0.0 on degenerate
r.SpreadStatus = IndicatorEngine.ClassifySpread(r.SpreadBps, wide, tight)  ' 0.0 <= 1.5 → "TIGHT"
```

⛔ **The two zeros collapse.** A degenerate book renders **`TIGHT`** where shipped renders **`NORMAL`**.

### 3.3 The rendered effect, at the shipped 5.0 / 1.5

| Surface | Shipped | Naive split | Moves? |
|---|---|---|---|
| Breakdown note `Core/ScoringEngine_Calculate_Scoring.vb:853` | `0.00 bps \| NORMAL` | `0.00 bps \| TIGHT` | ⛔ **yes** |
| Snapshot `UI/MainForm_PlaintextSnapshot.vb:450` | `Spread:    0.00 bps  \|  NORMAL` | `…  \|  TIGHT` | ⛔ **yes** |
| Card MiniMeter `UI/MainForm_Render_Cards.vb:2073-2079` | `0.00 bps · NORMAL`, colour `Theme.FG_TERTIARY` | `0.00 bps · TIGHT`, colour `Theme.ACC_STRONG_LONG` | ⛔ **yes — text AND colour** |
| Card breakdown row `UI/MainForm_Render_Cards.vb:3248-3252` | `NORM`, `Theme.FG_TERTIARY` | `TIGHT`, `Theme.ACC_STRONG_LONG` | ⛔ **yes — text AND colour** |
| Step-2 penalty `:417` | no penalty | no penalty | ✅ no — only `"WIDE"` is scored |

⛔⛔ **And the direction is the worst available one.** A book the engine could not read renders **green, "TIGHT"** — *best possible execution* — when the truth is *no data*. That is the display-strings-carry-no-fabricated-measurement class the absorption instrumentation row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 already names: *"a zero … would be a fabricated measurement the study cannot tell from a real one."*

### 3.4 Is the degenerate path reachable?

**Read, not assumed.**

- ⭐ **`orderBook Is Nothing` is NOT reachable at `UI/MainForm_Analysis.vb:422`.** `UI/MainForm_Analysis.vb:139` (`ElseIf orderBook Is Nothing Then`) skips the whole run first. That guard covers one of the five shapes.
- ⚠ **The other four ARE reachable there** — a non-`Nothing` book with an empty bid or ask ladder, or with a non-positive top-of-book price. On the WS path the book is served from `MarketState`, so a cold or thinly-seeded ladder is exactly this shape. **The v67 thin-trade-window skip gate exists because degraded feeds happen** ([`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15, 2026-08-20 audit).
- At `LiveMicrostructureEvaluator.vb:140` the status is discarded, so **no rendered effect there today** — but that is a property of the current caller, not of the method.

⚠ **I have NOT measured how often a degenerate book reaches `:422` in the live book.** The CSV logs `SpreadBps` but the degenerate and locked cases both write `0.00`, and `SpreadStatus` is not a CSV column — **the two are not separable from the book.** Stated plainly rather than estimated. **Reachability is established from the code; frequency is not established at all, and the fix does not depend on it.**

---

## 4. D-table — ✅ TICKED IN FULL 2026-09-06, every row as recommended. ⛔ **Build from §4b, not this table**

*(Kept as the record of how each decision was reached. The "My read" column is what was ruled, so it does not disagree with §4b — but §4b is the one that carries the code.)*

**Legend:** ⭐ marks my read. Nothing below is ruled.

| # | Decision | Options | My read |
|---|---|---|---|
| **D-1** | **How does `CalcSpreadBps` signal "no measurable spread"?** This is §3's crux and every other row depends on it | **(a)** ⭐ `CalcSpreadBps(orderBook) As Double?` — `Nothing` on any of the five degenerate shapes, a real `Double` otherwise · **(b)** `CalcSpreadBps(orderBook, ByRef hasTopOfBook As Boolean) As Double` · **(c)** keep it `As Double` and make each **caller** guard before classifying · **(d)** sentinel `-1.0` | ⭐ **(a).** `Double?` is this repo's established idiom for *"no measurement"* — the absorption instrumentation build ruled exactly this (`Double?` + `InvOpt`, *"a no-episode row renders EMPTY, never `0`"*), and `HourStoreStats.LastTsMs` was re-ruled from a sentinel to `Long?` for the same reason (`D-3` (c), [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) §4a.4). ⛔ **(c) is the one to refuse: it puts the invariant back in the caller, which is the thing this split exists to remove**, and it re-opens the divergence in a new place. **(d)** re-introduces a magic value the type system already models. **(b)** works but needs a `ByRef` at every call site — the pattern being deleted |
| **D-2** | **Where does the `"NORMAL"` degenerate answer live?** | **(a)** ⭐ inside `ClassifySpread(bps As Double?, wide, tight) As String` — returns `"NORMAL"` on `Nothing` · **(b)** each caller assigns `"NORMAL"` when the bps is absent | ⭐ **(a) — ONE seam.** Under (b) the answer is restated at two call sites and a third caller can forget it, which is §3's defect re-created by hand. Under (a) the degenerate contract is unreachable-by-mistake: a caller that has a `Double?` gets the right string or a compile error |
| **D-3** | **Does `LiveMicrostructureEvaluator.HasSpread` move onto the new nullable?** ⚠ **They are NOT equivalent** — `HasTopOfBook` (`:303-307`) tests three conditions, `CalcSpreadBps` tests six (§5.3) | **(a)** `snap.HasSpread = bps.HasValue` — collapses the third copy of the predicate, but **changes live-strip behaviour** on a zero-priced top-of-book · **(b)** ⭐ leave `HasTopOfBook` alone; record the divergence as a residual | ⭐ **(b) for this build.** `S2-2`'s whole claim is *zero behaviour change*, provable on every surface; **(a) breaks that claim for a cosmetic gain and hides a real behaviour change inside a refactor.** ⚠ **(a) is defensible on the merits** — it is strictly more truthful, and a zero-priced top-of-book genuinely is *no spread* — but it belongs in its own one-line change with its own line in the record, not smuggled in here. ⛔ **This row exists because an implementer WILL be tempted to collapse them** |
| **D-4** | **Does `CalcSpread` survive as a wrapper?** | **(a)** ⭐ **delete it** — both call sites move to the pair · **(b)** keep it as a thin `Sub` wrapper over the pair | ⭐ **(a).** (b) reinstates precisely what the split removes: a threshold-taking entry point a future caller reaches for by habit, and a **third** place the composition can drift. There is nothing to keep it for — **zero fixtures call it** (§2.1), and both callers are in this repo |
| **D-5** | **Naming** | **(a)** ⭐ `CalcSpreadBps` + `ClassifySpread`, as [`a54a-session2-step1-measurement-2026-09-05.md`](a54a-session2-step1-measurement-2026-09-05.md) §9.2 (i) names them · **(b)** `CalcSpreadBps` + `ClassifySpreadStatus` | ⭐ **(a).** The analysis already used these names and the queue row quotes them; renaming now costs greppability against three documents for no gain. **`ClassifySpread` returns the `SpreadStatus` string — the `Classify*` prefix already matches `ClassifyAbsorption` / `ClassifyTrendStructure`** |

**If D-1 (a) and D-2 (a) are ruled, the shape is:**

```vb
' Pure. No thresholds. Nothing ⇒ no measurable top-of-book.
Public Shared Function CalcSpreadBps(orderBook As OrderBookSnapshot) As Double?

' Nothing ⇒ "NORMAL" (the degenerate answer, preserved from the pre-split seed).
Public Shared Function ClassifySpread(spreadBps As Double?,
                                      wideThresholdBps As Double,
                                      tightThresholdBps As Double) As String
```

---

## 4b. ✅ THE RULED STATE — build from THIS section

**Six build items. Nothing else is in scope.** ⛔ **Order matters: item 4 before item 2/3 is wrong — write the fixtures against the NEW methods, then move the callers, so the harness proves the composition rather than the other way round.**

### 4b.1 — `Core/Indicators_OrderFlow.vb`: delete `CalcSpread`, add the pair

⛔ **Delete `CalcSpread` outright (`D-4` (a)) — no wrapper, no `<Obsolete>` shim.** Both callers are in this repo and no `.vb` fixture calls it (§2.1). A wrapper is a third place the composition can drift.

Replace `Core/Indicators_OrderFlow.vb:571-606` with the pair below. ⚠ **`:571` — the range starts at the `''' <summary>` line, not at `Public Shared Sub` (`:581`); the whole `S2-1` doc block goes with the method it documents.** *(Verified by reading `:568-582`, 2026-09-06. The first draft of this section said `:576` and was wrong — re-read the range before deleting, do not trust this line either.)*

```vb
''' <summary>
''' Computes basis-point spread from the best bid/ask of the order book snapshot.
''' Returns Nothing when there is NO MEASURABLE TOP OF BOOK -- an absent book, an empty
''' ladder on either side, a non-positive best price, or a non-positive mid.
'''
''' [S2-2, 2026-09-06] Split out of the former CalcSpread. ⛔ Nothing is NOT a failure code:
''' it is the DEGENERATE-BOOK ANSWER, and ClassifySpread maps it to "NORMAL" -- exactly the
''' value the pre-split CalcSpread seeded before its five early-return guards. Returning 0.0
''' here instead would classify TIGHT (0.0 <= tight_threshold_bps) and render a book the
''' engine could not read as best-possible execution, on four surfaces. A LOCKED book
''' (bestBid = bestAsk > 0) is a real 0.0 and legitimately TIGHT -- the two zeros are
''' different answers and this signature is what keeps them apart.
''' See s2-2-calcspread-split-proposal.md §3.
'''
''' Deliberately THRESHOLD-FREE: LiveMicrostructureEvaluator wants only this value, so after
''' the split it references indicators.spread.* not at all and CANNOT diverge from
''' MainForm_Analysis when the auto-tweaker retunes those keys (they are NOT fenced in
''' SettingsDiffApplier.RejectedPathPrefixes). Impossible by construction rather than
''' guarded -- the ComputeSideLevels / TradeStoreWriter one-seam pattern.
''' </summary>
Public Shared Function CalcSpreadBps(orderBook As OrderBookSnapshot) As Double?
    If orderBook Is Nothing Then Return Nothing
    If orderBook.Bids Is Nothing OrElse orderBook.Bids.Count = 0 Then Return Nothing
    If orderBook.Asks Is Nothing OrElse orderBook.Asks.Count = 0 Then Return Nothing

    Dim bestBid As Double = orderBook.Bids(0).Price
    Dim bestAsk As Double = orderBook.Asks(0).Price
    If bestBid <= 0 OrElse bestAsk <= 0 Then Return Nothing
    Dim mid As Double = (bestBid + bestAsk) / 2.0
    If mid <= 0 Then Return Nothing

    Return ((bestAsk - bestBid) / mid) * 10000.0
End Function

''' <summary>
''' Classifies a spread (bps) as TIGHT / NORMAL / WIDE against configurable thresholds.
''' Nothing -- no measurable top of book -- returns "NORMAL".
'''
''' [S2-2, 2026-09-06] ⚠ The ">= wide" arm is tested FIRST and THAT ORDER IS LOAD-BEARING.
''' Both comparisons are inclusive, so when tight >= wide the arms overlap and order alone
''' decides. That state is reachable: indicators.spread. is auto-tweaker-tunable. Shipped
''' resolved WIDE, which is the conservative answer (WIDE is the only value that scores).
''' ⛔ Do not rewrite as a Select Case -- that is where the order gets lost.
''' </summary>
Public Shared Function ClassifySpread(spreadBps As Double?,
                                      wideThresholdBps  As Double,
                                      tightThresholdBps As Double) As String
    If Not spreadBps.HasValue Then Return "NORMAL"
    If spreadBps.Value >= wideThresholdBps  Then Return "WIDE"
    If spreadBps.Value <= tightThresholdBps Then Return "TIGHT"
    Return "NORMAL"
End Function
```

⚠ **The five guards must each `Return Nothing`, not `Return 0.0`.** A partial conversion — even one guard left returning `0.0` — re-creates §3's flip through that path alone, and only that path's fixture catches it.

### 4b.2 — `UI/MainForm_Analysis.vb:422-424`

```vb
' [S2-2] CalcSpread split into a pure bps fn + a classifier. Nothing = no measurable top of
' book, which ClassifySpread maps to "NORMAL" -- the pre-split seed value. r.SpreadBps still
' receives 0.0 in that case, exactly as before (it is a Double and four surfaces format it F2).
Dim spreadBps As Double? = IndicatorEngine.CalcSpreadBps(orderBook)
r.SpreadBps    = If(spreadBps.HasValue, spreadBps.Value, 0.0)
r.SpreadStatus = IndicatorEngine.ClassifySpread(spreadBps,
                     wideThresholdBps:=cfg.Indicators.Spread.WideThresholdBps,
                     tightThresholdBps:=cfg.Indicators.Spread.TightThresholdBps)
```

⛔ **Arguments stay NAMED** — the A54a session-2 convention: a named argument turns a silent threshold swap into a compile error.

### 4b.3 — `LiveMicrostructureEvaluator.vb:132-145`

⭐ **The whole threshold pass disappears, and the stale-prose comment goes with it.**

```vb
' Spread -- CalcSpreadBps is THRESHOLD-FREE by construction (S2-2), so this evaluator no
' longer references indicators.spread.* at all and cannot diverge from MainForm_Analysis if
' the auto-tweaker retunes them. The claim the old comment made in prose is now the signature.
' ⚠ snap.HasSpread deliberately STAYS on HasTopOfBook (D-3 ruled (b)): the two predicates are
' NOT equivalent -- HasTopOfBook tests 3 conditions, CalcSpreadBps tests 6 (it adds the three
' price tests). Collapsing them would change live-strip behaviour on a zero-priced top of
' book. That divergence is a NAMED RESIDUAL, not an oversight -- see the proposal §8.
Dim bps As Double? = IndicatorEngine.CalcSpreadBps(book)
snap.SpreadBps = If(bps.HasValue, bps.Value, 0.0)
snap.HasSpread = HasTopOfBook(book)
```

⛔ **Delete the two-line `Dim sBps As Double = 0, sStatus As String = "NORMAL"` declaration.** The throwaway variable is the thing this build exists to remove; leaving it means the build did not land.

### 4b.4 — Fixtures `A65a`–`A65d` in `verify/ordercheck/Program.vb`

**Next free family is `A65`.** Register the four calls beside `A64a_…` / `A64b_…` (`verify/ordercheck/Program.vb:597-598`). Signature is `Check(name As String, cond As Boolean, detail As String)` (`:629`).

⚠ **`MakeBook(bestBid, bestAsk, bidSize, askSize)` (the existing helper) always fills FIVE levels a side, so it CANNOT build `A65a`'s shape.** `A65a` constructs `New OrderBookSnapshot()` directly and leaves one ladder empty. `A65b` may use `MakeBook`. See §6 for what each pins and the mutation that must fail it.

### 4b.5 — `docs/DeribitIndicatorProject.md` §15

**ONE row**, per its own one-item-one-row rule. ⚠ **Check the retention cap first: five settings versions (v68 · v67 · v66 · v65 · v64) currently sit exactly at it, and §15 holds 14 rows** (measured 2026-09-06). Settings does **not** move, so this is a settings-untouched row and it must be newer than v64's date — it is.

### 4b.6 — `docs/trader-tick-queue.md`

Close the `S2-2` row in §2 and clear the `S2-2` line from the **§0a owed table** and the state banner. ⛔ **Both places** — a decision closed in one and left standing in the other is the exact defect §0a exists to prevent, recorded there four times already.

### 4b.7 — ⛔ NOT in scope

`settings.json` · any `Core/IndicatorResults.vb` field type · `tools/BacktestRunner/ReplayLoop.vb:466` (sets `r.SpreadStatus` directly, never calls the method) · the `HasTopOfBook` predicate itself (`D-3` (b)) · the 5.0 / 1.5 values.

---

## 5. The traps, with the code

### 5.1 The seed (⛔ trap 1 — §3)

`ClassifySpread` must answer `"NORMAL"` for absent, **not** fall through to the `<= tight` arm. Under D-1 (a) / D-2 (a):

```vb
Public Shared Function ClassifySpread(spreadBps As Double?,
                                      wideThresholdBps As Double,
                                      tightThresholdBps As Double) As String
    ' [S2-2] Nothing is the DEGENERATE-BOOK answer, not a default. Pre-split, CalcSpread
    ' seeded spreadStatus = "NORMAL" before its five early-return guards, so an unreadable
    ' book rendered NORMAL. Classifying a 0.0 instead yields TIGHT (0.0 <= 1.5) and moves
    ' four rendered surfaces. See s2-2-calcspread-split-proposal.md §3.
    If Not spreadBps.HasValue Then Return "NORMAL"
    ...
End Function
```

⚠ **And `CalcSpreadBps` must return `Nothing` — never `0.0` — on all five guards**, including `mid <= 0`. Returning `0.0` from any one of them re-creates the flip through that path alone, which is the shape a partial fix produces.

### 5.2 The classification order (⚠ trap 2)

Preserve **exactly**:

```vb
If spreadBps.Value >= wideThresholdBps  Then Return "WIDE"
If spreadBps.Value <= tightThresholdBps Then Return "TIGHT"
Return "NORMAL"
```

⚠ **`>= wide` FIRST.** Both comparisons are inclusive, so when `tight >= wide` the arms overlap and order decides. Shipped resolves **WIDE**; a re-ordered version resolves **TIGHT** — the opposite scoring outcome, on a key the auto-tweaker may propose changes to (`indicators.spread.` is **not** in `tools/AutoTweaker/SettingsDiffApplier.vb`'s fenced prefixes). **A `Select Case` rewrite is where this gets lost.**

### 5.3 The two validity predicates are NOT the same (⚠ trap 3)

| Predicate | Where | Tests |
|---|---|---|
| `CalcSpread`'s guards | `Core/Indicators_OrderFlow.vb:587-595` (seed at `:586`) | book non-`Nothing` · bids non-`Nothing` · bids non-empty · asks non-`Nothing` · asks non-empty · **`bestBid > 0`** · **`bestAsk > 0`** · **`mid > 0`** |
| `HasTopOfBook` | `LiveMicrostructureEvaluator.vb:303-307` | book non-`Nothing` · bids non-`Nothing` · bids non-empty · asks non-`Nothing` · asks non-empty |

⛔ **`HasTopOfBook` omits all three price tests.** They agree on every book except one with a non-positively-priced top of book — where `HasTopOfBook` says True and `CalcSpreadBps` (under D-1 (a)) says `Nothing`. **Do not collapse them without ruling `D-3`.**

### 5.4 Call-site composition at `UI/MainForm_Analysis.vb:422-424`

The one place where the split must reproduce shipped exactly:

```vb
Dim spreadBps As Double? = IndicatorEngine.CalcSpreadBps(orderBook)
r.SpreadBps    = If(spreadBps.HasValue, spreadBps.Value, 0.0)   ' pre-split wrote 0 on degenerate
r.SpreadStatus = IndicatorEngine.ClassifySpread(spreadBps,
                     wideThresholdBps:=cfg.Indicators.Spread.WideThresholdBps,
                     tightThresholdBps:=cfg.Indicators.Spread.TightThresholdBps)
```

⚠ **`r.SpreadBps` must still receive `0.0`, not stay unassigned** — `Core/IndicatorResults.vb:61` declares it `As Double` and every rendered surface formats it `F2`. **Arguments stay named** (the A54a session-2 convention: named arguments turn a silent swap into a compile error).

At `LiveMicrostructureEvaluator.vb:140-142` the whole threshold pass disappears:

```vb
Dim bps As Double? = IndicatorEngine.CalcSpreadBps(book)
snap.SpreadBps = If(bps.HasValue, bps.Value, 0.0)
snap.HasSpread = HasTopOfBook(book)        ' unchanged under D-3 (b)
```

⭐ **The stale-prose comment at `:132-134` goes with it** — the signature now carries the claim.

---

## 6. Fixture blast radius — **zero existing, four new**

⭐ **Nothing existing breaks.** No `.vb` fixture calls `CalcSpread` (§2.1); fixtures that need a spread state set `r.SpreadStatus` directly. **That is the good news and also the risk: there is no existing coverage of this method at all**, so the new fixtures are the only guard.

**Next free fixture family is `A65`** ([`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §6).

| Fixture | Pins | ⛔ The input shape that makes it fail — name it before writing the assertion |
|---|---|---|
| **`A65a`** | ⛔ **The crux.** A degenerate book classifies **`NORMAL`**, not `TIGHT` | ⚠ **`MakeBook` CANNOT build this shape** — it always fills five levels a side. Construct directly: `Dim b As New OrderBookSnapshot() : b.Bids.Add((100000.0, 10.0))` and leave `Asks` **empty**. **Second case in the same fixture:** a zero-priced top of book — `b.Bids.Add((0.0, 10.0))` + `b.Asks.Add((100010.0, 10.0))` — which exercises the `bestBid <= 0` guard rather than the empty-ladder one. ⛔ **NOT `orderBook = Nothing`** — unreachable at `UI/MainForm_Analysis.vb:422` (`:139` skips first), so it tests the one guard nobody can hit. **Mutation: make `CalcSpreadBps` return `0.0` instead of `Nothing` on the empty-ladder guard — `A65a` must FAIL and the other three must still PASS** |
| **`A65b`** | The **locked book** stays `TIGHT` | `MakeBook(100000.0, 100000.0, 10.0, 10.0)` ⇒ level 0 is exactly `bestBid = bestAsk = 100000.0` ⇒ `bps = 0.0`, status `TIGHT`. ⭐ **This is `A65a`'s twin and the pair IS the point:** both produce `0.00 bps` and they must classify **differently**. ⛔ **Without `A65b`, `A65a` passes trivially under a `ClassifySpread` that returns `"NORMAL"` unconditionally** — which would silently retire the TIGHT state on every surface. **Mutation: make `ClassifySpread` return `"NORMAL"` for `HasValue` too — `A65b` must FAIL** |
| **`A65c`** | The **order** of the two arms (§5.2) | `wideThresholdBps:=1.0`, `tightThresholdBps:=2.0` (deliberately inverted), `bps = 1.5` ⇒ **`WIDE`**. ⛔ **MECHANISM literals — the inversion is impossible in shipped config and is asserting the arm order, not a settings value.** Declare that at the call site per CLAUDE.md's fixture-literal provenance rule. **Mutation: swap the two `If` arms — `A65c` must FAIL and `A65a`/`A65b`/`A65d` must all still PASS** |
| **`A65d`** | Boundary **inclusivity** on both arms | `bps` exactly `= wide` ⇒ `WIDE`; exactly `= tight` ⇒ `TIGHT`; strictly between ⇒ `NORMAL`. Derive the two thresholds **from `cfg`**, not literals — this one asserts SHIPPED BEHAVIOUR, so the provenance rule requires cfg-derived values |

⛔⛔ **EVERY MUTATION MUST BE RUN, NOT REASONED.** That requirement is what caught `A62f` and `A63a` — each built exactly to a spec's worked description and each unable to observe a mutation that spec itself named ([`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §5). ⚠ **The named risk here is concrete: `A65a` and `A65b` both produce `0.00 bps`, so a fixture that asserts on the bps rather than the status will pass under the exact defect §3 describes.** Assert the **status string**.

---

## 7. Acceptance

1. **Builds 0/0 Release, each run separately:** solution · `AutoTweaker` · `WhatIfRunner` · `CeilingAudit` · `BacktestRunner` · `OrderCheck`.
2. **Harness ALL PASS**, count **328 → 332** (`A65a`–`A65d`). `A1`–`A64b` unregressed.
   ⭐ **The 328 baseline is MEASURED, not inherited** — `dotnet verify/ordercheck/bin/Release/net8.0/OrderCheck.dll` piped to `grep -c "^PASS "` printed **328** with `ALL PASS` and zero `FAIL`, run 2026-09-06 UTC at base commit **`2e8bc76`**. ⚠ **Pinned to that commit deliberately** — re-measure at the commit the build actually starts from rather than trusting this line. ⛔ **Do NOT count `Check(` in the source to verify it: `grep -c '^\s*Check('` prints 333 and the unanchored form prints 342.** Neither is the harness count, because some `Check` calls sit in helpers invoked more than once and others are unreached. **Run the harness; do not count its source.**
3. **`tools/checks/verify-gate.ps1` GATE PASSED.**
4. ⛔ **The display-string parity proof — this is the acceptance item, not a formality.** The rule is LIVE here (four rendered surfaces, §2.2). **Prove parity, do not assert it:**
   - Capture the plaintext snapshot's `Spread:` line and the two card bindings' resolved `(text, colour)` pre-change and post-change, over **at least**: one WIDE book, one TIGHT book, one NORMAL book, and ⛔ **one degenerate book (`A65a`'s shape)**.
   - **All four must be byte-identical.** ⚠ **The degenerate case is the one that matters** — the other three are identical under the defective implementation too.
5. **`settings.json` unchanged — still v68**, no new keys, no `change_log` entry. ⛔ **If any part of this needs a settings key, stop: that is a different change.**
6. **`docs/DeribitIndicatorProject.md` §15** gets **one row**, per its own one-item-one-row rule. ⚠ **Check the retention cap first** — five settings versions (v68 · v67 · v66 · v65 · v64) currently sit exactly at it.
7. **Commit message must state the parity position explicitly** — either the card bindings moved, or why no card surface is affected. Under a correct implementation the answer is *"no rendered string or colour changes on any input; proved on four book shapes including degenerate"*, and **that sentence is only true if item 4 was actually run.**

---

## 8. Out of scope — recorded so they are not swept in

- ⛔ **`D-3` (a)'s predicate collapse.** If `D-3` is ruled (b), `HasTopOfBook`'s divergence from `CalcSpreadBps` (§5.3) stays a **named residual**, not a silent one. It is a one-line change with a real, if small, live-strip effect and it deserves its own line in the record.
- **`SpreadStatus` is not a CSV column.** That is why §3.4's frequency question cannot be answered from the book. **Adding one is a schema rotation** and is not proposed here.
- **The `D4` residual** — positional passing, whole-`cfg` builders, and `OfiAccumulator.vb:84`'s `tauSec` — is a separate queue item ([`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §6). `S2-2` touches none of it.
- **No threshold is re-derived.** `wide_threshold_bps` 5.0 and `tight_threshold_bps` 1.5 are untouched. Whether they are the right numbers is a calibration question this refactor deliberately does not open.
