# Thin-Trade-Window Skip Gate — close the one unguarded degraded-feed path

**Date:** 2026-08-20. **Origin:** the degraded-feed audit run 2026-08-20 (recorded in [`trader-tick-queue.md`](trader-tick-queue.md) §2). **Class:** one skip-gate condition in `UI/MainForm_Analysis.vb`, one derived helper, one settings key. **No indicator maths changes. No scoring maths changes. No CSV header change. No card or snapshot line.**

---

## 0. Model + effort — READ THIS FIRST

**Model: Sonnet. Effort: medium.**

**Why that tier.** The judgment is already done and written into this spec. The defect is located, the mechanism is traced to a single line, the fix shape is ruled, and every mechanical piece has an in-repo template — the skip gate is an existing `ElseIf` chain you extend, the settings key follows the `TfiSettings` pattern verbatim, and the fixture family has A-series precedent. **What makes it medium rather than low is the derivation rule in §3.2 and the boundary question in §5.** Both are stated, but both are easy to implement wrongly in a way that still compiles and still passes a naive test.

**Where Sonnet will specifically slip — three traps, named.**

- **TRAP 1 — hardcoding 50.** The obvious implementation is `If recentTrades.Count < 50`. **That is wrong and it is the exact defect class the fixture-literal provenance rule exists for.** `MicroCvdSettings.WindowSize` and `TfiSettings.WindowSize` are both settings-driven (`EngineSettings.vb:377, 386`). A literal 50 rots silently the first time either moves. **Derive it — see §3.2.**
- **TRAP 2 — gating on the wrong count.** The gate must test the list the indicators actually consume, which is `recentTrades` **after** `src.GetRecentTradesAsync` returns — not the 500 that was requested, and not `MarketState`'s ring length. `WsMarketDataSource` returns `Math.Min(count, all.Count)`, so the requested and delivered counts differ precisely in the failure case this spec exists for.
- **TRAP 3 — writing the fixture from the spec's prose instead of from the failure.** ⚠ **A fixture that feeds 49 trades and asserts "skipped" is nearly worthless** — it passes against a hardcoded 50 and against a derived 50 identically, so it cannot tell TRAP 1 from a correct build. **The fixture MUST mutate the settings value** (see §6, A57c) — that is the only shape that distinguishes them. This is the standing lesson from `feedback_fixture_shape_must_admit_the_failure`: name the input that makes it fail, then mutate once per decision.

**Escalation trigger — stop and move to Opus if any of these appear.**

- The skip rate after deploy is **not near-zero**. This gate should fire almost never (it needs a seed failure). A measurable rate means the thin-list path is reached by some route this spec did not model, and the diagnosis matters more than the fix.
- You find yourself wanting to change `CalcTFI`, `CalcCVD`, `CalcMicroCVD` or `CalcLiquidations`. **Do not.** They are correct (§2.3). If the fix seems to need them, the gate is in the wrong place.
- The D-table in §5 does not answer your question. Stop and ask rather than ruling it yourself.

**Session split: none. This is one session, one commit.**

---

## 1. The defect

**Every guard on the trade path tests `Count = 0`. Nothing tests for a thin list.**

The skip gate at `UI/MainForm_Analysis.vb:123-149` checks each required shape. Its trades condition reads:

```vbnet
ElseIf recentTrades Is Nothing OrElse recentTrades.Count = 0 Then
    skipReason = "recent trades unavailable"
```

`WsMarketDataSource.GetRecentTradesAsync` (`WsMarketDataSource.vb:91-106`) returns `Math.Min(count, all.Count)` — however few are in the ring.

### 1.1 How a thin ring is reached

`DeribitWsFeed.SeedAsync` re-seeds the trade ring from REST on every connect (`DeribitWsFeed.vb:318`):

```vbnet
Dim trades = Await DeribitClient.GetRecentTradesAsync(500)
If trades IsNot Nothing Then _state.SeedTrades(trades, nowUtc)
```

**One transient REST failure on that single call silently skips the trade seed.** Candles seed normally. The connect proceeds, and `_connected = True` is set at `DeribitWsFeed.vb:211`. The health gate then passes. The ring holds only trades that streamed in after subscribe.

⚠ **The connect ordering is otherwise correct and must not be changed** — `_connected = True` is set *after* `Await SeedAsync` (`:205`, `:211`), which is why there is no general reconnect window. This spec closes the one path that ordering does not cover.

### 1.2 Why it matters

At the measured **~1.4 trades/sec**, refill times are:

| Consumer | Window | Time to fill |
|---|---:|---:|
| `CalcTFI` | `TFI.WindowSize` = 30 | ~21 s |
| `CalcMicroCVD` | `MicroCVD.WindowSize` = 50 | ~36 s |
| `CalcCVD` | walks the whole list | — |

⚠ **`TFIValue` from a single trade is exactly ±1.0**, and `|1.0| > TFI.Threshold` (0.15) yields `"BUY PRESSURE"` or `"SELL PRESSURE"` **at maximum magnitude from one print**. TFI scores. MicroCVD — the heaviest scoring consumer at 9 references — computes thirds of a handful. `CalcCVD`'s `weightedSlope` over three trades is noise that can trip `slopeThreshold`.

A spurious TFI direction also opens the aggressor-velocity burst path, which is gated on `tfiLong Xor tfiShort` (`ScoringEngine_Calculate_Scoring.vb:357`).

### 1.3 ⚠ It is invisible, and that is the strongest argument for fixing it

No skip. No log line. No CSV marker. **There is no trade-count column, so the row looks entirely normal in the book and this cannot be found retrospectively.** Any past occurrence is unrecoverable as evidence.

### 1.4 The same exposure on an open position

`ExitGuardEvaluator.vb:58-64` mirrors the same window with the same `Count = 0` guard and no minimum. **The exit guard acts on live positions**, so it inherits the defect. §3.3 covers it.

---

## 2. What is NOT wrong — do not "fix" these

Recording this because the audit checked them and they are correct. Changing them is out of scope and would be a regression.

### 2.1 `IsDegraded()` is right, including the conjunction

`DeribitWsFeed.vb:596` returns True on `Not _connected`, `_coolingDown`, or **all three** streams stale. The conjunction is deliberate: one stale stream is handled a tier lower by `WsMarketDataSource`'s per-shape gate returning `Nothing`, which the skip gate then treats like a REST failure. **Do not weaken it to a disjunction** — that would throw whole runs onto REST for one quiet channel.

### 2.2 The WS-only signals degrade correctly

`src Is _wsSource` gates three reads. On REST fallback: OFI averaging falls through to book-snapshot OFI (`:387`); aggressor velocity (`:442`) and absorption (`:463`) are never set and keep their score-neutral defaults `"NORMAL"` and `"NONE"`.

### 2.3 All four trade-derived indicators self-initialise correctly

`CalcCVD` sets `"FLAT"`/`"NONE"` and `CalcTFI` sets `"NEUTRAL"` **before** their empty guard, so they never rely on caller defaults. ⛔ **This spec does not touch any of the four.**

---

## 3. The fix

### 3.1 Where

One new condition in the existing skip-gate chain in `UI/MainForm_Analysis.vb`, placed **immediately after** the existing `recent trades unavailable` condition and **before** the `IsFresh` conditions.

```vbnet
ElseIf recentTrades.Count < MinTradesForScoring(cfg) Then
    skipReason = "recent trades thin (" & recentTrades.Count & "<" &
                 MinTradesForScoring(cfg) & ")"
```

**The reason string must carry both numbers.** It surfaces in the SKIPPED panel and the LOG sub-box counter, and it is the only forward evidence this path was ever taken. Do not shorten it.

### 3.2 ⚠ The derivation rule — this is TRAP 1

**The minimum is DERIVED from the windows the trade-derived signals consume. It is never a literal.**

Add one host-agnostic helper. Home it beside the other pure engine helpers, not in the form.

```vbnet
''' <summary>Smallest trade count at which every trade-derived signal has a full
''' window. Derived, never a literal: TFI and MicroCVD window sizes are settings
''' (EngineSettings.vb:377, 386) and a hardcoded copy rots the first time either
''' moves — the fixture-literal provenance rule, applied at the source.
''' CalcCVD walks the whole list and imposes no minimum of its own.</summary>
Public Shared Function MinTradesForScoring(cfg As EngineSettings) As Integer
    Return Math.Max(cfg.Indicators.TFI.WindowSize,
                    cfg.Indicators.MicroCVD.WindowSize)
End Function
```

At shipped defaults this evaluates to **50**. **That number appears nowhere in the code.**

### 3.3 The exit guard

`ExitGuardEvaluator` takes the same treatment: below the derived minimum it returns its existing **Clear** result — the same posture it already takes on an empty buffer (`ExitGuardEvaluator.vb:58`). **It must not skip or throw**; the exit guard runs on an open position and its never-throw discipline is load-bearing.

### 3.4 The settings key

**One new key**, so the gate is tunable without a rebuild and so an operator can widen it if the deploy reveals an unmodelled path:

```json
"scoring": { "min_trades_for_scoring_override": 0 }
```

**`0` means "use the derived value"** — the shipped default, and byte-identical to deriving. A positive value overrides. **`MinTradesForScoring` returns the override when it is `> 0`.**

⚠ **Fence it off the auto-tweaker surface.** Add `scoring.min_trades_for_scoring_override` to the exact-match reject set in `SettingsDiffApplier` and a matching numbered rule in `PromptBuilder`, following the `scoring.min_tradeable_move_pct` precedent (HC11 class — data-sufficiency plumbing, no failure-rate linkage). Use the next free hard-constraint number; do not reuse one.

**Bump `settings.json` `version` and prepend a `change_log` entry.**

---

## 4. What this deliberately does NOT do

- **No CSV column and no header rotation.** A trade-count column would be genuinely useful and is **explicitly out of scope** — it forces a rotation, and there is a rotation already queued as absorption `D-1` with two riders attached. **Propose it as a third rider on that rotation, not here.**
- **No change to the four indicator functions.**
- **No change to `IsDegraded()`, the connect ordering, or `SeedAsync`.** ⚠ Making `SeedAsync` retry or fail the connect on a trade-seed failure is a *plausible* alternative fix and it is **rejected**: it would trade a scoring defect for a connection-availability defect, and the skip gate is where every other shape's insufficiency is already handled. One seam, no copies.

---

## 5. D-table — await trader

| # | Decision | Recommendation |
|---|---|---|
| **D-1** | Thin window ⇒ **SKIP the run** (no CSV row, SKIPPED bridge payload) rather than proceed with trade-derived signals forced neutral | **SKIP.** Consistent with how every other insufficient shape is already handled, and a forced-neutral row is silently degraded in the book — the same invisibility this spec exists to remove |
| **D-2** | Minimum is **derived** as `max(TFI.WindowSize, MicroCVD.WindowSize)`, with a settings override defaulting to `0` = derived | **Yes** — §3.2. The override exists so a deploy surprise is tunable without a rebuild |
| **D-3** | ⚠ **Is this a dataset boundary?** Rows that would previously have scored will now skip | **NO — recommended, with the reasoning recorded so it can be overruled.** The affected rows are the ones this spec argues were never valid, the expected rate is near-zero, and the closest precedent — the v32 D5 candle-freshness skip gate — took no boundary. ⚠ **If the deploy shows a non-negligible skip rate, this decision must be revisited**, because then the population really did change |
| **D-4** | `ExitGuardEvaluator` returns **Clear** below the minimum, never skip or throw | **Yes** — §3.3 |
| **D-5** | Trade-count CSV column deferred to the `D-1` absorption rotation as a rider | **Yes** — §4 |

---

## 6. Fixtures — `A57a`–`A57d` in `verify/ordercheck`

Use the next free A-number if `A57` is taken; state which you used in the spec-back.

- **A57a — the gate fires.** A run whose `recentTrades.Count` is below the derived minimum produces a skip with the reason string carrying both numbers, **and writes no CSV row**.
- **A57b — the gate does not over-fire.** A run at exactly the derived minimum scores normally and is byte-identical to the pre-change build.
- **A57c — ⚠ THE ONE THAT CATCHES TRAP 1, and it is the reason this fixture family exists.** Set `MicroCvdSettings.WindowSize` to a **non-default** value — say 80 — and assert the gate now fires at 79 and passes at 80. ⛔ **A build that hardcoded 50 passes A57a and A57b and FAILS this one.** That is the entire point. **Do not write A57a/A57b and call the family done.**
- **A57d — the override.** `min_trades_for_scoring_override = 0` is byte-identical to derived; a positive value takes precedence; the auto-tweaker fence rejects the key while a sibling `scoring.*` tunable still passes.

**Mutation obligation.** ⚠ **Prove the teeth by mutation, not by assertion.** Revert the gate condition to a hardcoded `< 50` and confirm **A57c fails**. If it does not, the fixture is not testing what it claims and must be rewritten before the build is offered for review. This is the standing lesson from the `A56` family, where the one mutation the spec required left two fixtures green.

---

## 7. Acceptance

- Solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0 Release**.
- Full A-series unregressed, plus `A57a`–`A57d`.
- `tools/checks/verify-gate.ps1 -Mode prepush` green.
- **`settings.json` version bumped, `change_log` entry prepended.**
- Display-string parity: **no obligation, and state that explicitly in the commit message** — no snapshot line, no card binding, no CSV column, no bridge field is added, removed, renamed or re-formatted. The skip reason rides the existing SKIPPED surface.
- **Local commit — the trader tests and pushes.**

## 8. Post-ship watch

**The falsifiable prediction, written before deploy: the thin-trade skip rate is near-zero.**

Read `_lastSkipReason` occurrences carrying `recent trades thin` over the first weekday-week. ⚠ **A non-negligible rate falsifies §1.1's model of how the path is reached** — it would mean the thin ring has a second cause this spec did not find, and **D-3's dataset-boundary answer must then be revisited.** Report the rate either way; a zero reading is the expected result and is still worth recording, because it is the evidence that the gate is not silently eating runs.
