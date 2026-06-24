# Realtime Exit Guard — Implementer Spec-Back (P4 #1)

**Status:** IMPLEMENTED — **coordinator ruling (D1–D8) folded in 2026-06-25**. Local commits `6346885` (initial) + a fold-in commit on `master`, **NOT pushed** (trader tests + pushes).
**Spec:** `docs/realtime-exit-guard-proposal.md` (APPROVED 2026-06-24).
**Settings:** v42 → **v43** (one new `exit_guard` block; no further bump for the fold-in — no config-key change, v43 unpushed).
**Builds:** solution(Release) + AutoTweaker + OrderCheck **0/0** (post-fold-in).
**Harness:** A1–A16e unregressed + **A17a–A17h** — 53/53 pass.
**Scope honoured:** display/alert only — no `Calculate()` call, no CSV write, no verdict change, no card surface.

§1–§2 confirm the build; §3 documents each divergence from the literal spec; §4 is the §8 acceptance audit. **§0 below records the coordinator ruling and exactly what changed in the fold-in** — read it first.

---

## 0. Coordinator ruling (D1–D8) — folded in 2026-06-25

| # | Ruling | What changed in the code |
|---|---|---|
| **D3** | **CHANGE — drop the Warn tier.** A single adverse signal (any of micro/OFI/TFI/CVD) → **Clear**, not Warn. Single-micro is already on the HOLD\EXIT row; single OFI/TFI/CVD alone is noise; a frequently-amber strip desensitizes the trader to the real EXIT. | `ExitGuardKind` Warn member **removed**; evaluator's `count==1` branch deleted → falls through to Clear. `ComputeFastExitPrimitives` **unchanged** (count still needed for `CalcHoldStatus` Layer 3 → A17g byte-identical still passes). Host `RenderExitGuard` Warn branch removed. Final strip vocabulary: **Clear → "confirming n/d" → EXIT+alarm → Paused**. New fixture **A17h** asserts single-adverse → Clear. |
| **D4** | **CHANGE — full-width row.** Relocate the strip out of the half-width LOG `SectionGroup` into its own full-width row in the SETTINGS & TOOLS `outer` TLP, between LOG/AUTO-RUN and the CTA. Full line inline; **drop the tooltip**. | Strip moved; `outer` TLP `RowCount` 3→4 (new 26px row at index 1; CTA→2, TOOLS→3); LOG row reverted 134→110; card 364→366. `lblExitGuard` is now `Dock=Fill` mono-9pt. `_exitGuardTip` + `SetToolTip` removed; `SetExitGuardStrip` lost its `tip` param. Separator is `·` (matches `BuildWsStatusSegment`): `EXIT GUARD · ⚠ EXIT — 2 adverse (MicroCVD BEAR_ACCEL, TFI SELL)`. |
| **D6** | **CHANGE — decouple from auto-run; gate on posState.** Start the timer once at form load, self-gate each tick on posState + feed health; dispose on close. `MarketState` streams whenever `transport=ws` regardless of auto-run, so a declared position must stay watched even with auto-run paused. | `StartExitGuard()` moved from `StartAutoRun` → the constructor; the `StartAutoRun`/`StopAutoRun` calls removed. `StopExitGuard()` stays in `OnFormClosing`. Always-on 3s timer, no-ops when flat / at `transport=rest`. |
| **D1** | **Accept as built.** The `FastExitPrimitives` class exposing the four booleans is the single-source-of-truth the spec wanted — better than the §4.3 tuple. | No change. |
| **D5** | **Keep as built.** The "confirming 1/2" pending render stays; with Warn gone it's the graduated heads-up toward EXIT. | No change. |
| **D8** | **Accept as built.** Paused belongs in the host (the evaluator has only a `MarketState`, no feed handle). | No change. |
| **D2, D7** | FYI, no objection. | No change. |

Net code delta vs the initial commit: enum + evaluator mapping (D3), the strip's layout home + tooltip removal (D4), the timer's start site (D6), one new fixture (A17h), and the v43 change_log / §15 wording (Warn dropped, full-width, posState-tied). `ComputeFastExitPrimitives` and the byte-identical `CalcHoldStatus` refactor are untouched.

---

## 1. Summary

Built the approved spec verbatim in intent. The four pieces from §11:

1. **Shared primitive** — `ComputeFastExitPrimitives` extracted from `CalcHoldStatus`; Layers 1/1.5/3 refactored to consume it, **output byte-identical**.
2. **Host-agnostic evaluator** — new root `ExitGuardEvaluator.vb`, no WinForms.
3. **Settings** — `exit_guard` block + `ExitGuardSettings` POCO; off the auto-tweaker surface.
4. **WinForms host** — `MainForm_ExitGuard.vb`: timer + §4.1 gate + LOG-cascade strip + debounced EXIT latch + sound.

It re-evaluates the existing fast-exit logic on the live `MarketState` while a position is declared and the WS feed is healthy, surfacing an `EXIT GUARD` strip and an alarm on a confirmed EXIT.

---

## 2. Implementation vs the §11 file map

| Spec §11 item | Built | Notes |
|---|---|---|
| `ExitGuardEvaluator.vb` (root, host-agnostic) | ✅ | `Evaluate(state, posState, lastSwingLow5m, lastSwingHigh5m, cfg) → ExitGuardResult`. Recomputes the 4 streaming signals, calls the shared primitive, maps to `{Clear, Warn, Exit}`. Never throws into the caller (wrapped in `Try`). |
| `Core/ScoringEngine_Helpers.vb` — extract `ComputeFastExitPrimitives`, refactor Layers 1/1.5/3 | ✅ | Byte-identical (see §3 D1 for the return-shape deviation and §4 for the proof). |
| `EngineSettings.vb` + `settings.json` — POCO + block, bump v43 + change_log + §15 | ✅ | All done; §15 row + §6 version pointer + block list updated. |
| AutoTweaker — `RejectedPathPrefixes` + PromptBuilder HARD CONSTRAINT | ✅ | `"exit_guard."` added to `SettingsDiffApplier.RejectedPathPrefixes`; new **HARD CONSTRAINT 13** in `PromptBuilder`. |
| `UI/MainForm_*` — timer, gate, strip, sound, carried swing levels | ✅ | All host glue in a new partial `UI/MainForm_ExitGuard.vb`; the strip is parented in `MainForm_Layout` (see §3 D4). Swing levels read from `_lastSuccessfulIndicators`. |
| `verify/ordercheck/` fixtures | ✅ | A17a–A17g (gitignored harness; local-only). |

§6 display-parity: confirmed **no card-binding obligation** — the strip is a live status-bar element like `BuildWsStatusSegment`, not RTF / `BuildPlaintextSnapshot` / card. The commit message states "no card surface affected" with the reason on record.

---

## 3. Deviations & decisions (discussion agenda)

Eight items where I made a call the spec didn't pin down, or where the spec text was internally ambiguous. **D3 and D4 are the two I most want a ruling on.**

### D1 — `ComputeFastExitPrimitives` returns a class, not the §4.3 tuple
**Spec §4.3:** `… As (AdverseCount As Integer, AdverseSignals As String(), StructuralBreak As Boolean, BreakLevel As Double)`.
**Built:** a `Public Class FastExitPrimitives` carrying those four **plus** the four per-signal booleans `MicroAdverse / OfiAdverse / TfiAdverse / CvdAdverse`.
**Why:** the spec's own goal is "no second, drifting copy of *what counts as adverse*." A 4-field tuple can't deliver that: `CalcHoldStatus`'s Layer-1 string needs to know *which* signals are adverse to rebuild `BEAR_ACCEL+OFI:SELL+…`, and the guard strip needs the same to build its readable labels. With only `AdverseCount` + `AdverseSignals`, one of the two consumers would have to re-derive the booleans → exactly the drift the spec kills. Exposing the booleans makes the booleans themselves the single source.
**Question:** acceptable, or do you want the tuple shape preserved (and accept that one consumer re-derives)?

### D2 — `AdverseSignals` = terse `CalcHoldStatus` fragments; readable strip labels built separately
**Spec:** §4.3 names the field `AdverseSignals As String()`; §4.6 shows the strip reading `MicroCVD BEAR_ACCEL, TFI SELL`.
**Built:** `AdverseSignals` holds the **terse** fragments (`BEAR_ACCEL`, `OFI:SELL`, `TFI:SELL`, `CVD:FALLING`) in `[micro, ofi, tfi, cvd]` order, so `CalcHoldStatus` stays byte-identical via `String.Join("+", p.AdverseSignals)`. The guard builds its **readable** labels (`MicroCVD BEAR_ACCEL`, `OFI SELL`, …) in `ExitGuardEvaluator.ReadableSignals` from the *same booleans*.
**Net:** two label vocabularies (terse for the engine's existing string, readable for the new strip), both derived from one boolean source — no drift, but two formatters.
**Why:** the byte-identical mandate (§8) forbids changing `CalcHoldStatus`'s terse output; §4.6's readable form is the UX intent. This satisfies both. The alternative — `AdverseSignals` = readable, and stop routing `CalcHoldStatus`'s string through it — would either break byte-identical or re-introduce an inline second copy.
**Question:** fine, or do you want a single vocabulary (and which)?

### D3 — Warn fires on **any** single adverse signal, not micro-only ⭐
**Spec conflict:** §4.2 says Layer 3 = `microAdverse alone (count == 1 via micro) → EVALUATE`. §1 says "plus the single-adverse soft warning." §4.6's Warn row example is `1 adverse (TFI SELL)` — a **TFI**-only warn, which §4.2's micro-only rule would render unreachable.
**Built:** Warn = `AdverseCount == 1` (any single adverse, with the offending signal named). EXIT precedence unchanged (count ≥ 2, then structural break).
**Why:** §4.6 (the trader-signed mockup) shows a non-micro Warn, and the guard's Warn is a new informational state with no sound and zero scoring impact — broadening it can only surface more, never fire a false EXIT. Also: the guard deliberately skips Layer 2 (ROC/OBV/RSI), so it can't perfectly mirror `CalcHoldStatus` Layer 3 anyway (which is only reached *after* Layer 2 doesn't fire). So the guard's Warn is its own thing by construction.
**Question:** keep broad (matches the §4.6 mockup), or restrict to micro-only (matches §4.2's parenthetical)? **One predicate either way.**

### D4 — Strip detail rides a tooltip; the inline line is short ⭐
**Spec §4.6:** shows the full detail inline, e.g. `EXIT GUARD · ⚠ EXIT — 2 adverse (MicroCVD BEAR_ACCEL, TFI SELL)`.
**Built:** inline line is compact (`EXIT GUARD: ⚠ EXIT — 2 adverse`); the full `MicroCVD BEAR_ACCEL, TFI SELL` (or the swing-break level) is on a **tooltip**.
**Why:** the strip lives in the LOG `SectionGroup`, which is **half-width** of the SETTINGS & TOOLS card (~270px interior). The full §4.6 string at mono ~8.5pt overruns that and would collide with the AUTO-RUN box to its right. The tooltip keeps the inline line glanceable while preserving all detail on hover.
**Trade-off:** the detail isn't visible at a glance — you hover for the signal list.
**Question:** acceptable, or do you want the strip relocated to a **full-width row** (its own row in the SETTINGS & TOOLS `outer` TLP, between the LOG/AUTO-RUN row and the CTA) so the full §4.6 line fits inline? That's a slightly more invasive layout change but gives the literal mockup.

### D5 — Added a "confirming n/d" pending render during the debounce build-up
**Spec §4.6:** lists the latched states (Clear / Warn / Exit / Paused) but doesn't specify what shows *during* the `debounce_evals` build-up before EXIT latches.
**Built:** while an EXIT condition is present but unconfirmed, the strip shows amber `⚠ EXIT? confirming 1/2` (no sound). On the `debounce_evals`-th consecutive tick it latches to red `⚠ EXIT` + sound.
**Why:** the alternative (show nothing / show the prior Warn until latch) felt less honest — the trader is mid-confirmation and the strip should say so. No sound until latch (anti-jitter intact).
**Question:** keep the pending render, or suppress it (strip stays on the prior state until the latch)?

### D6 — Timer tied to the auto-run lifecycle (per §4.5/§11), with a single-mode note
**Spec:** §4.5 and §11 both say "started/stopped with the auto-run lifecycle." §4.1 says "the timer does no work (dormant when flat)" — which reads as *the timer ticks but no-ops when flat*.
**Built:** the guard timer is created in `StartAutoRun` and disposed in `StopAutoRun` (and `OnFormClosing`); each tick self-gates on posState + feed health. Start is idempotent (disposes any prior timer first).
**Single-mode edge worth confirming:** in SINGLE auto-run mode, `RunAutoAnalysis` resets the button to "Start" after the one run but does **not** call `StopAutoRun`, so the guard timer **keeps running** (self-gated) until the next explicit Stop. I think that's correct — you declared a position, you want the guard watching — but it means SINGLE mode leaves a 3s timer alive post-run.
**Question:** is "guard runs while auto-run is engaged, and SINGLE-mode leaves it running until manual Stop" the intended lifecycle? (The alternative reading of §4.1 — start the timer once at form load and let the gate handle everything — decouples it from auto-run entirely. I went with the literal §4.5/§11 wording.)

### D7 — CVD recompute pinned to the last **500** trades (parity finding)
**Spec §4.2:** "CVDSlope / CVDValue | `CalcCVD(MarketState.trades, MarketState.candles("1"), …)`".
**Built:** the evaluator feeds `CalcCVD` the **last 500** trades from `MarketState`, not the full 5000-trade ring.
**Why:** `CalcCVD` walks the *entire* list it's handed (`count = trades.Count`), and the full run feeds it `GetRecentTradesAsync(500)` — i.e. the last 500. CVD over 5000 ≠ CVD over 500 and would diverge on slope classification. TFI/MicroCVD self-window via `LastN(30/50)`, so they're insensitive — only CVD needs this. `TradeWindow = 500` mirrors the engine's call-site constant exactly; locks the guard's CVD methodology to the engine's, only the data is fresher.
**Note:** this is a parity *clarification* of §4.2, not a behavioural change — flagging it because "MarketState.trades" was ambiguous between "the whole ring" and "the 500 the engine reads."

### D8 — `Paused` is host-only; the §8 "feed-stale → Paused" fixture moved to the host gate
**Spec §8:** lists "feed-stale stub → Paused (no eval)" as an **evaluator** fixture.
**Built:** the evaluator's domain is `{Clear, Warn, Exit}` only (per §4.4). **Paused is decided in the host** (`MainForm_ExitGuard.ExitGuardPausedReason` — connected / not-cooling / trades+book fresh ≤ `ws_stale_after_sec`); when paused the host shows the strip and **does not call `Evaluate`**. The harness compiles host-agnostic sources only (no `MainForm`), so Paused isn't harness-testable.
**Substituted:** the harness covers `empty buffer → Clear` (A17f) as the evaluator's degenerate-safety case; Paused is validated by the host gate + the trader's live run.
**Question:** agreed that Paused belongs in the host (matches §4.1's "the timer does no work … does not evaluate")? If you'd rather the evaluator own a `Paused` kind, say so — but it has no feed handle, only a `MarketState` snapshot.

**Minor clarifications (not deviations):**
- **CurrentPrice** = the streaming trade tail (`window.Last().Price`), per §4.2 — deliberately fresher than the full run's candle-close `CurrentPrice`. Intended.
- **Sound** = `System.Media.SystemSounds.Exclamation` (no bundled asset), wrapped in `Try` so a missing audio device never disrupts a run. §4.5 said "System.Media on EXIT transition" without naming a sound.
- **Strip placement** = the LOG `SectionGroup` (`grpLog`); LOG row bumped 110→134 and the SETTINGS & TOOLS card 340→364 to give it a clean row without squeezing the TOOLS box.

---

## 4. §8 acceptance audit

| §8 criterion | Result | How |
|---|---|---|
| Build 0/0 — solution(Release) + AutoTweaker + OrderCheck | ✅ | All three built clean; Release dodged the running-app Debug exe lock. |
| `CalcHoldStatus` byte-identical after the extraction | ✅ | **A17g** asserts the exact Layer-1 string `EXIT -- microstructure deterioration (BEAR_ACCEL+OFI:SELL+TFI:SELL+CVD:FALLING)` through the public `Calculate()`/InLong. **Note:** there were *no* pre-existing `CalcHoldStatus` fixtures (it's `Private Shared`), so "existing hold-status fixtures unchanged" is vacuously true; A17g is the new positive proof, asserted via the one public surface that exposes `res.HoldStatus`. |
| Evaluator: 2 adverse + long → Exit | ✅ | A17a (primitive) + A17f (end-to-end over `MarketState`). |
| structural break → Exit | ✅ | A17b (primitive: price ≤ swing low → `StructuralBreak`, `BreakLevel`). |
| 1 adverse → **Clear** (D3: Warn dropped) | ✅ | **A17h** (evaluator: single adverse → Clear) + A17c (primitive count = 1). |
| 0 adverse → Clear | ✅ | A17d. |
| mirror for short | ✅ | A17e (OFI+TFI buy → count 2). |
| feed-stale stub → Paused (no eval) | ↗ moved | Host gate (`ExitGuardPausedReason`) — see **D8**. Not harness-testable without WinForms; empty-buffer→Clear (A17f) substitutes for the evaluator's safety case. |
| empty buffer → Clear | ✅ | A17f. |
| Dormancy: posState=None → no eval/allocation | ✅ | `Evaluate` returns early on `None`; the host tick returns *before* calling `Evaluate` when flat. By construction (host), not a harness fixture. |
| `transport=rest` byte-identical | ✅ | Guard is host-gated (`_wsFeed`/`_marketState` Nothing at rest → strip shows "WS only", no eval); verdict path untouched. A1–A16e unregressed confirm `Calculate()` is unchanged. |
| Host-agnostic: evaluator + primitive reference no WinForms | ✅ | Both compile into OrderCheck.exe, which has **zero** WinForms reference. |

---

## 5. Files changed (commit `6346885`)

Tracked:
- `Core/ScoringEngine_Helpers.vb` — `FastExitPrimitives` class + `ComputeFastExitPrimitives`; Layers 1/1.5/3 refactor.
- `ExitGuardEvaluator.vb` *(new, root)* — `ExitGuardKind` / `ExitGuardResult` / `ExitGuardEvaluator`.
- `Core/Settings/EngineSettings.vb` — `ExitGuardSettings` POCO + property.
- `settings.json` — `exit_guard` block, v42→v43, change_log.
- `UI/MainForm_ExitGuard.vb` *(new)* — timer, gate, debounce/latch, strip render, sound.
- `UI/MainForm_Layout.vb` — strip label parented in `grpLog`; row/card height bumps; `StopExitGuard` on close.
- `UI/MainForm_AutoRun.vb` — `StartExitGuard`/`StopExitGuard` in the auto-run lifecycle.
- `tools/AutoTweaker/SettingsDiffApplier.vb` — `"exit_guard."` prefix.
- `tools/AutoTweaker/PromptBuilder.vb` — HARD CONSTRAINT 13.
- `docs/DeribitIndicatorProject.md` — §15 row + §6 version pointer + block list.

Gitignored (local-only, not in the commit): `verify/ordercheck/OrderCheck.vbproj` (+`ExitGuardEvaluator.vb` link) and `verify/ordercheck/Program.vb` (A17a–g).

---

## 6. Resolved (was the discussion list)

All settled by the coordinator ruling in §0:
1. **D3** → Warn tier **dropped** (single adverse → Clear).
2. **D4** → **full-width row**; tooltip dropped.
3. **D1** → class-with-booleans **accepted**.
4. **D6** → **decoupled** from auto-run (form-load start, posState self-gate).
5. **D5** → "confirming n/d" pending render **kept**.
6. **D8** → Paused **stays in the host**.

D2 / D7 — FYI, no objection.

---

## 7. Not done / gates

- **Trader rebuild required** — the running app is the pre-change build (I built Release; the live app is Debug). The guard appears after a rebuild + restart, with a position declared, auto-run started, and a healthy WS feed.
- **Local commits only** — `6346885` (initial) + the D1–D8 fold-in, not pushed. Trader tests live (does the strip track the tape during a real hold; does the alarm fire on a genuine 2-adverse / structural break; no false EXIT on a stale-feed blip; strip stays Clear on lone single-adverse noise) then pushes, per the local-first gate.
- This spec-back is committed with the fold-in (the D1–D8 ruling is now on record in §0).
