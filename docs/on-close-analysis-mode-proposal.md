# On-Close Analysis Mode — Proposal (P4 #2)

**Status:** PROPOSED — awaiting trader sign-off (2026-06-25)
**Target:** settings **v43 → v44** (one new `auto_run.trigger_mode` key)
**Scoring impact:** **none** — pure run-*trigger* change. `RunAnalysisAsync` / `Calculate()` are byte-identical; only *when* a run fires changes. No CSV-schema change, **no re-baseline**.
**Item:** #2 in `websocket-migration-proposal.md` §11 (unmarked = display/trigger-only).
**Gate to build:** safe anytime — WS is validated and the engine computation is untouched. The second P4 feature per §8 sequencing.

---

## 1. Summary

A toggle that fires the full analysis run **at each execution-resolution bar close** (event-driven), instead of on the fixed-interval auto-run timer. When on, a run happens the instant a bar completes — eliminating the up-to-`interval` poll lag at the one moment that matters most for a structural-breakout trader: bar close.

The engine already runs at a per-session **execution resolution** (NY = 1-min, Asia/London = 3-min — v36). On-close fires when *that* bar closes: 1-min boundaries in NY, 3-min boundaries in Asia/London. The run itself is unchanged — it reads the same `MarketState` it always does; only the trigger moves from "every N seconds" to "when the exec bar rolls."

This is a **mode**, selected alongside the existing SINGLE/REPEAT radios and the interval NUD, defaulting OFF (interval mode = today's behaviour).

---

## 2. Motivation & profile alignment

- **Profile §2 (entry):** "price breaks above/below previous swing high/low, confirmed by impulse (ROC) and volume spike. **Structural breakout required — no chasing candles.**" A breakout is confirmed when a bar *closes* beyond the swing level. A 30s timer evaluates that breakout at an arbitrary phase — up to a full interval late, or mid-forming-bar (provisional). On-close fires the verdict the instant the confirming bar closes. That's the trader's decision moment, delivered without lag.
- **Pairs with the exit guard (#1):** the exit guard watches an *open* position at tick freshness; on-close sharpens the *entry* decision at bar-close freshness. Together they cover both sides of the trade at WS freshness. They're independent timers reading the same `MarketState`.
- **Fewer, better-timed runs.** At a 30s interval NY logs ~2 runs/min at arbitrary phase; on-close logs ~1 run/min, each aligned to a completed bar. Fewer rows, every one bar-aligned — cleaner for both the trader and the CSV.

---

## 3. Scope & non-goals

**In scope**
- A `trigger_mode` toggle (`interval` | `on_close`) + the UI control.
- A bar-close watcher that fires `RunAutoAnalysis` on each exec-resolution roll, honouring SINGLE/REPEAT.
- An interval **backstop** so the engine never goes silent if the WS feed stalls.
- Countdown-label adaptation for on-close mode.

**Explicit non-goals**
- **No engine change.** `RunAnalysisAsync` is untouched — same fetch, same indicators, same `Calculate()`, **same handling of the forming last bar.** On-close does **not** drop the forming bar or evaluate "closed bars only" — that would change the computation (a re-baseline) and is out of scope. The verdict at an on-close fire is exactly what a timer fire at that instant would produce.
- **No CSV/scoring/dataset change.** Rows are the same kind of observation, logged at bar-close moments instead of every N seconds. No schema change, no re-baseline.
- **Not a new data source.** Reads the existing `MarketState` exec-resolution series.

---

## 4. Design

### 4.1 Trigger modes

`StartAutoRun` (`UI/MainForm_AutoRun.vb`) branches on `cfg.AutoRun.TriggerMode`:

- **`interval` (default):** unchanged — `_autoRunTimer.Start(_intervalMs, AddressOf RunAutoAnalysis)` (or `StartOnce` for SINGLE). Byte-identical to today.
- **`on_close`:** start the **bar-close watcher** (§4.2) instead of the interval timer. SINGLE → fire on the next close then stop; REPEAT → fire on every close until Stop.

`StopAutoRun` stops whichever is active. The Start/Stop button, SINGLE/REPEAT radios, and min-10s interval rule are unchanged.

### 4.2 Bar-close detection

On-close requires the WS feed (it reads `MarketState`). A lightweight watcher timer (≈1s `Threading.Timer`, same family as the exit-guard tick) runs while on-close auto-run is engaged and, each tick:

1. Resolves the **current** exec resolution from the UTC hour — `ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)` (re-resolved each tick so a session boundary, e.g. London→NY 13:00 UTC flipping 3→1, is honoured live).
2. Reads `MarketState.GetCandles(execRes)`; the **forming bar's open-time** is `series.Last().Timestamp`.
3. **Roll detected** when that open-time advances past the last-seen value → the prior bar just closed → fire `RunAutoAnalysis` once, and store the new open-time.

A small host-agnostic helper carries the decision so the Linux port reuses it:

```vb
' returns (Fired, NewFormingBarOpen) given the last-seen forming-bar open time
Public Shared Function DetectBarRoll(state As MarketState, execRes As Integer, lastSeenOpen As DateTime) As (Fired As Boolean, FormingOpen As DateTime)
```

**Catch-up, not burst:** if several bars elapsed during a gap (reconnect), the open-time jumps multiple intervals — fire **once** and adopt the new open-time. We don't replay missed bars.

### 4.3 Interval backstop (feed-gap safety)

If the WS feed stalls (e.g. degraded — the run still works via REST fallback, but the WS-fed `MarketState` series stops rolling), pure on-close would go silent. To prevent that, the watcher also fires when **`now − lastFire ≥ _intervalMs`** (the configured interval as the backstop ceiling). A real bar-close resets `lastFire`, so the backstop only fires when no close has been seen for a full interval. Net: on-close normally fires on the roll; during a feed stall the interval keeps runs flowing (and those runs REST-fall-back as usual). Default on.

### 4.4 transport=rest / feed unavailable

On-close is a WS-mode feature. If `transport=rest` or `_marketState`/`_wsFeed` is `Nothing`, on-close **falls back to interval mode** for that session (the engine keeps running) and the status shows a one-time note. The toggle may still be selected; it simply has no effect until a feed is present. (The engine is on WS post-cutover, so this is the safety path, not the common one.)

### 4.5 Countdown label

In on-close mode, "Next run in: M:SS" is replaced by time-to-next-bar-boundary — `Next close: M:SS [MODE]` — computed from the next exec-resolution boundary (`ceil(now / execRes) − now`). Informative and reuses `lblCountdown`. During the rest-fallback case it shows the interval countdown as today.

### 4.6 Lifecycle

The watcher is created in `StartAutoRun` (on_close branch) and disposed in `StopAutoRun` + `OnFormClosing`. Idempotent (a re-start disposes any prior watcher). Independent of the exit-guard timer (different concern, different cadence). Reads `MarketState` only — never blocks, never throws into the UI thread.

---

## 5. Config — `auto_run.trigger_mode` (settings v44)

```json
"auto_run": {
  "interval_minutes": 1,
  "interval_seconds": 0,
  "trigger_mode": "interval"
}
```

- `AutoRunSettings` POCO gains `TriggerMode As String = "interval"` (`<JsonPropertyName("trigger_mode")>`).
- The interval keys keep their meaning and **double as the on-close backstop ceiling**.
- **Operational/UI toggle, not a tweaker target.** Like the interval, a runtime mode switch saves with `bumpVersion:=False` (v36 §10a precedent — no version/change_log churn on an operational change). Only the one-time *key addition* bumps v43→v44.
- **Off the auto-tweaker surface:** verify `auto_run` is already excluded; if not, add `"auto_run."` to `SettingsDiffApplier.RejectedPathPrefixes` + the PromptBuilder HARD-CONSTRAINT list (operational preference, no failure-rate linkage — same class as `kelly.*` / `exit_guard.*`).
- Bump v43→v44 + `change_log` + `DeribitIndicatorProject.md` §15 row.

---

## 6. Display-parity rule

The mode toggle is a WinForms control; `lblCountdown` is an existing status label (not the RTF verdict, not `BuildPlaintextSnapshot`, not a card). No new rendered verdict line. So **no card-binding obligation** — flagged here for the commit to state "no card surface affected." The run *output* is byte-identical to interval mode.

---

## 7. Edge cases & safety

- **Feed stall / degraded →** the interval backstop (§4.3) keeps runs flowing; on-close resumes the instant rolls are seen again.
- **Multi-bar gap (reconnect) →** fire once, adopt the new open-time (no burst).
- **Session boundary (execRes change) →** re-resolved each tick; the watcher switches to the new resolution's bars cleanly (first roll on the new resolution fires normally).
- **transport=rest →** falls back to interval (§4.4) — never silent.
- **SINGLE mode →** fires on the next close, then resets to Start exactly as `StartOnce` does today.
- **Double-fire guard →** `lastFire` is set on every fire (roll or backstop); the backstop checks against it, so a roll immediately followed by the backstop window can't double-fire.

---

## 8. Acceptance

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck.
- **No scoring change:** A1–A17h unregressed (`RunAnalysisAsync`/`Calculate()` untouched; only the trigger differs).
- **`interval` mode byte-identical** to current (the existing `_autoRunTimer` path is unchanged; prove via the rest-path/interval regression).
- **Detection harness (new):** `DetectBarRoll` — same forming-bar open-time → no fire; advanced by one interval → fire; advanced by several (gap) → single fire; resolution switch mid-stream → fires on the first new-resolution roll. Backstop arithmetic (`now − lastFire ≥ interval`) unit-checked.
- Host-agnostic: `DetectBarRoll` references no `System.Windows.Forms`.

---

## 9. Open decisions for trader sign-off

1. **Fire at exec-resolution close vs always 1-min** — recommend **exec-resolution close** (resolution-consistent: NY fires on the 1-min close the §11 text describes; Asia/London fire on the 3-min bar the engine actually evaluates, not a forming-bar mid-point). Always-1-min would fire NY identically but evaluate Asia/London on a 1/3-formed 3-min bar — provisional/noisy. 
2. **Interval backstop** — recommend **on** (the engine must never go silent on a feed stall; reuses the interval as the ceiling).
3. **Default `trigger_mode`** — recommend **`interval`** (preserves today's behaviour; on-close is opt-in).
4. **SINGLE/REPEAT in on-close** — recommend mirror the existing radios (REPEAT = every close; SINGLE = next close then stop).
5. **Countdown in on-close** — recommend time-to-next-bar-boundary (`Next close: M:SS`).

---

## 10. Implementation map (files)

- **`UI/MainForm_AutoRun.vb`** — `StartAutoRun` branches on `cfg.AutoRun.TriggerMode`; new `StartOnCloseWatcher` / `StopOnCloseWatcher` (a ~1s `Threading.Timer`) that calls `RunAutoAnalysis` on a detected roll or the backstop; `StopAutoRun` + `OnFormClosing` dispose it. `OnCountdownTick` / `UpdateCountdownLabel` adapted for the on-close "Next close" display.
- **New host-agnostic helper** (root or `Core/`) — `DetectBarRoll(state, execRes, lastSeenOpen) → (Fired, FormingOpen)`; reads `MarketState.GetCandles(execRes)`. Reuses `ExecutionResolution.ResolveResolution`.
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — `AutoRunSettings.TriggerMode`; the `trigger_mode` key; bump v43→v44 + change_log + §15.
- **UI control** — a mode toggle (radio pair or a small segmented control) beside the AUTO-RUN SINGLE/REPEAT + interval NUD; greys/relabels the interval NUD as "backstop" when on-close is selected. (Designer + `MainForm_Layout`/`MainForm_AutoRun` wiring.)
- **`tools/AutoTweaker/`** — confirm/add `"auto_run."` exclusion (verify it isn't already proposable).
- **`verify/ordercheck/`** — the §8 `DetectBarRoll` + backstop fixtures.

---

## 11. Out of scope / follow-ons

- **#3 LIVE microstructure strip** (`websocket-migration-proposal.md` §11) is the natural next display feature; it and on-close share the streaming-`MarketState` read pattern.
- After #3, the first ⚠ re-baseline upgrade is **#4 time-averaged OFI** — that one *does* change what an indicator sees and carries a re-baseline flag, so it's sequenced after the display items.
