# On-Close Analysis Mode — Implementer Spec-Back (P4 #2)

**Built:** 2026-06-25 against `on-close-analysis-mode-proposal.md` (APPROVED, all five §9 decisions confirmed).
**Settings:** v43 → **v44** (one new key `auto_run.trigger_mode`).
**Scope honoured:** pure run-*trigger* change — `RunAnalysisAsync` / `ScoringEngine.Calculate()` untouched, no CSV/eval-cache schema change, **no re-baseline**, forming bar NOT dropped.
**Status:** solution(Release) + AutoTweaker + OrderCheck build **0/0**; harness **A1–A17h unregressed + new A18a–e** all pass. Local commits only — trader tests + pushes.

---

## 1. What shipped, file by file

| File | Change |
|---|---|
| `Core/Settings/EngineSettings.vb` | `AutoRunSettings.TriggerMode As String = "interval"` (`<JsonPropertyName("trigger_mode")>`). |
| `settings.json` | `auto_run.trigger_mode: "interval"`; bump v43→v44; `change_log` entry. |
| `Core/BarCloseDetector.vb` | **New host-agnostic** `DetectBarRoll(state, execRes, lastSeenOpen) → (Fired, FormingOpen)`. Reads `MarketState.GetCandles(execRes)`; fires when the forming-bar open-time advances; multi-bar gap → single catch-up fire; `Unseen` sentinel for first-look adopt-no-fire. No WinForms. |
| `UI/MainForm_AutoRun.vb` | `StartAutoRun` branches on `OnCloseModeActive()`; new `StartOnCloseWatcher`/`StopOnCloseWatcher`/`OnCloseWatcherTick` (~1s `Threading.Timer`); `AutoRunEngaged`/`SetAutoRunInputsEnabled`/`BuildOnCloseCountdownText`; `OnCountdownTick` renders `Next close: M:SS` in on-close mode; `RunAutoAnalysis` SINGLE stops the watcher; `OnTriggerModeChanged`/`UpdateTriggerModeUi`. |
| `UI/MainForm_Layout.vb` | Watcher state fields; `BuildTriggerModeToggle` (programmatic INTERVAL\|ON-CLOSE radios) wired into the header-strip cluster + its `SizeChanged` re-flow. `StopAutoRun`/`OnFormClosing` already dispose via `StopOnCloseWatcher`. |
| `tools/AutoTweaker/SettingsDiffApplier.vb` | `"auto_run."` added to `RejectedPathPrefixes`. |
| `tools/AutoTweaker/PromptBuilder.vb` | **HARD CONSTRAINT 14** — never propose `auto_run.*`. |
| `verify/ordercheck/OrderCheck.vbproj` + `Program.vb` | Links `Core/BarCloseDetector.vb`; new **A18a–e** fixtures. |
| `docs/DeribitIndicatorProject.md` | §6 version → v44 + `auto_run` block note; §15 row. |

---

## 2. Deviations from the proposal (all faithful realisations, flagged for review)

1. **`DetectBarRoll` open-time is `Long`, not `DateTime`.** The proposal §4.2 signature used `DateTime`, but `Candle.Timestamp` is **epoch-ms `Long`** (the real field `series.Last().Timestamp` returns). Using `Long` is the literal realisation of "the forming bar's open-time advances" — a `DateTime` signature would have forced a lossy conversion. No behavioural difference.

2. **Watcher is a `Threading.Timer` (per the explicit §4.2/§10 wording), NOT the exit-guard's `System.Windows.Forms.Timer`.** The proposal also called it "same family as the exit-guard tick" — but the exit guard ended up a WinForms Timer under coordinator ruling D6 *after* this spec was written, so that descriptor is stale. Honoured the explicit "`Threading.Timer`" text: the tick reads `MarketState` off-thread (lock-guarded, safe) and **marshals the fire via `Me.Invoke(Sub() RunAutoAnalysis())`** — exactly the `_countdownTimer` / `WinFormsAutoRunTimer` pattern already in this file. RunAutoAnalysis touches UI on the marshalled thread only.

3. **UI toggle built programmatically, not in the Designer.** Proposal §10 said "(Designer + … wiring)". CLAUDE.md is a hard rule: *never edit `MainForm.Designer.vb` manually*. The established precedent (the P4 #1 exit-guard strip) creates new controls in code and parents them in `MainForm_Layout`. Followed that: `BuildTriggerModeToggle` creates `pnlTrigger` + the two radios and inserts them into the header-strip's right-aligned cluster + its `SizeChanged` re-flow. Mutual exclusion via the shared `pnlTrigger` parent.

4. **Resolution-switch handling lives in the host, detector stays stateless.** `DetectBarRoll` only compares open-times for whatever series it's told to read. On a session boundary the watcher tick detects `execRes ≠ _onCloseLastRes` and resets `_onCloseLastSeenOpen = Unseen`, so the new resolution re-adopts (no spurious fire) then fires on its first real roll — implementing §4.2/§7 "switches to the new resolution's bars cleanly." A18d verifies this contract on the detector.

5. **Fallback note is a persistent suffix, not a one-shot.** §4.4 asked for "a one-time note" when on-close is requested at `transport=rest`/no feed. Rendered instead as a continuous `[on-close: WS only]` suffix on the interval countdown while the run is engaged — a one-shot label is overwritten within ~1s by the countdown tick and never seen; the suffix is honest about why on-close had no effect for the whole session.

---

## 3. Behavioural notes

- **Default `interval` is byte-identical to v43** — the `on_close` branch is the only new path; the `_autoRunTimer` `Start`/`StartOnce` calls are unchanged.
- **`AutoRunEngaged()`** ORs `_autoRunTimer.IsRunning` and `_onCloseActive`, because in on-close mode the interval timer is NOT started — every caller that gated on "is auto-run on" (`btnStartStop_Click`, `OnCountdownTick`) now routes through it.
- **Backstop** fires at `now − lastFire ≥ _intervalMs`; the configured interval (min 10s, unchanged) doubles as the ceiling. A real roll resets `lastFire`, so the backstop only fires after a full silent interval; setting `lastFire` on *every* fire is also the double-fire guard.
- **SINGLE in on-close** fires on the next close (or backstop) then `StopOnCloseWatcher` — parity with interval SINGLE's `StartOnce` self-stop.
- **`auto_run.trigger_mode` is OFF the auto-tweaker surface** — prefix-rejected in `SettingsDiffApplier` + HARD CONSTRAINT 14. Verified the existing A15f-style rejection path covers it (prefix match, same as `kelly.`/`network.`/`exit_guard.`).
- **No card-binding obligation** — the only rendered surface is `lblCountdown` (a status label), not the RTF verdict / `BuildPlaintextSnapshot` / a card. The run *output* is byte-identical to interval mode.

---

## 4. Acceptance (proposal §8)

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck. ✅
- **A1–A17h unregressed** (RunAnalysisAsync/Calculate untouched). ✅
- **A18a** same forming-open → no fire (first-look adopt-no-fire). ✅
- **A18b** +1 interval → fire once then quiesce. ✅
- **A18c** multi-bar reconnect gap → single catch-up fire. ✅
- **A18d** resolution switch → re-adopt no-fire, first new-resolution roll fires. ✅
- **A18e** backstop arithmetic (`now − lastFire ≥ interval`). ✅
- `DetectBarRoll` references no `System.Windows.Forms`. ✅

---

## 5. Not done / left for the trader

- **Live verification** — the watcher timer, the `Me.Invoke` fire path, the header-strip toggle layout, and the `Next close` countdown are host glue validated by a live run (as with the A17 exit-guard timer, the WinForms timer itself isn't harness-compiled). Trader confirms on a live WS session, then pushes.
- **Follow-on:** #3 LIVE microstructure strip is the natural next display feature (shares the streaming-`MarketState` read); after it, #4 time-averaged OFI is the first ⚠ re-baseline item (proposal §11).
