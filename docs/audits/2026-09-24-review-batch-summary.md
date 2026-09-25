# Adversarial audit follow-up: review batch summary (2026-09-24)

**What this is.** The record of the 12 follow-up reviews that prompts M1–M10 in [`../adversarial-audit-2026-09-24.md`](../adversarial-audit-2026-09-24.md) §7 produced. **The full audit report, with the ranked list and the decisions for the orchestrator, is that file.** This one keeps the per-lane record and the verdict on every finding. The runnable handles and the feedback on my prompts are in [`2026-09-24-review-batch-spec-back.md`](2026-09-24-review-batch-spec-back.md).

**Pinned to.** Engine commit `6e74181` (settings v68) and order app commit `8232e9e`. Every finding and verdict here is against those trees. Master moved on after the reviews started. The trader ruled that the newer code is out of scope, so nothing here was re-checked against it.

**Where the reviews live.**
- The 11 engine-side reviews are merged into this branch as they were pushed: `docs/audits/2026-09-24-*.md`, with proofs under `docs/audits/proofs/`.
- M9 ran in the order app's repo. It is copied byte for byte into [`order-app/`](order-app/README.md).

**How each claim was checked.**
- Every proof set that ships a runnable harness was re-run in this session and matched its recorded output. That covers five compiled VB/C# harnesses and four Python ports.
- I also ran M6a's proofs, which that session had never executed.
- Claims with no proof were checked by reading the cited code. The first pass left 42 unchecked; all 42 were checked on 2026-09-25. One remains open in part (M9 F7), and a lane covers it.

---

## 0–1. Moved

The headline findings and the ranked list of confirmed defects now live in the full audit report, [`../adversarial-audit-2026-09-24.md`](../adversarial-audit-2026-09-24.md) §A and §B. That report is the single entry point for the project orchestrator. This file stays the record: the per-lane table (§2), the verdict on every finding (§3) and the first run of the M6a proofs (§4).

---

## 2. Per-lane record

"Ran on" is each session's configured model and effort, as its session record showed at about 13:20 UTC. M1–M3 started before the §7.0 recommendations existed.

| Lane | Report | Ran on | Recommended (§7.0) | Coverage of its prompt | Proofs | Findings |
|---|---|---|---|---|---|---|
| M1 AutoTweaker | [`2026-09-24-autotweaker.md`](2026-09-24-autotweaker.md) | Opus 5.5 high | Opus 5.5 xhigh | Full, 11 of 11 files | C# probe P1–P9 + Python: **re-run, match** (one watcher event count in the control differs) | 16 |
| M2 settings | [`2026-09-24-engine-settings.md`](2026-09-24-engine-settings.md) | Opus 5.5 medium | Opus 5.5 high | Full, except the `change_log` prose | VB P1–P19: **re-run, match** (one extra hot-reload parse line) | 17 + range table |
| M3 exit guard | [`2026-09-24-exit-guard-live-strip-alerts.md`](2026-09-24-exit-guard-live-strip-alerts.md) | Opus 5.5 high | Opus 5.5 xhigh | Full | VB harness, 4 modes: **re-run, identical** | 16 |
| M4 outcome measurement | [`2026-09-24-live-performance-eval-pipeline.md`](2026-09-24-live-performance-eval-pipeline.md) | Opus 5.5 xhigh | Opus 5.5 xhigh, 2 sittings | Full, in 1 sitting | VB T1–T11: **re-run, identical** apart from temp paths | 14 |
| M5 tape store | [`2026-09-24-trade-store.md`](2026-09-24-trade-store.md) | Opus 5.5 medium | Opus 5.5 high | Full (three logs comment-filtered) | VB probe: **re-run, match** apart from timings and paths | 12 |
| M6a UI shell | [`2026-09-24-mainform-layout-tapestore-calibration.md`](2026-09-24-mainform-layout-tapestore-calibration.md) | Opus 5.5 medium | Opus 5.5 high | Full (Designer grep only) | P1–P4 unrun by the lane (no SDK). **Run here: all four as predicted** (§4) | 12 |
| M6b render | [`2026-09-24-render-cards-kelly-audit.md`](2026-09-24-render-cards-kelly-audit.md) | Sonnet 5 high | Sonnet 5 high, 2 sittings | **Partial**: 14 of 16 `UI/Controls` files unread | Python arithmetic: re-run, match | 5 |
| M7a offline analysis | [`2026-09-24-offline-analysis-report.md`](2026-09-24-offline-analysis-report.md) | Opus 5.5 medium | Opus 5.5 high | Full | Python replica: re-run, match | 13 |
| M7b backtest / what-if | [`2026-09-24-backtest-whatif-ceilingaudit.md`](2026-09-24-backtest-whatif-ceilingaudit.md) | Opus 5.5 high | Opus 5.5 xhigh, 3 sittings | **Partial**: CoverageReport ~40 %; FeatureMatrix, L2Logistic, AuditReport unread | Python ports: **re-run, identical** | 12 |
| M8 harness | [`2026-09-24-ordercheck-payload-fee-mtf-audit.md`](2026-09-24-ordercheck-payload-fee-mtf-audit.md) | Sonnet 5 high | Sonnet 5 high, 3 sittings | **~7 %** of `Program.vb` (my prompt's unfilled `<RANGE>`) | Greps only; nothing run | 4 |
| M9 order app | [`order-app/2026-09-24-signal-to-exchange-order-path.md`](order-app/2026-09-24-signal-to-exchange-order-path.md) | Opus 5.5 high | Fable 5.1 xhigh | **Partial**: SL-chase and edit functions unread | Python trace: re-run, match. VB fixture unrun | 12 |
| M10 ops readers | [`2026-09-24-swing-fallback-read-wstradeprobe.md`](2026-09-24-swing-fallback-read-wstradeprobe.md) | Sonnet 5 high | Sonnet 5 high, 2 sittings | Full, 9 of 9 files | None | 5 |

No lane reviewed PR #3 itself. The PR-review row in §7.0 never ran.

---

## 3. Verdict on every finding

**Codes:**
- **R** — reproduced here.
- **R\*** — reproduced by a port.
- **C** — confirmed by reading the code.
- **X** — holds only in part; the correction is given.
- **F** — refuted.
- **D** — duplicate of an audit-report item.
- **N** — not checked by me.

A finding carries the reviewer's own severity until the report's §B gives it a consolidated one.

**M1 AutoTweaker**
- F1: C + R (P9, Linux; the Windows watcher is unverified).
- F2: R (P2).
- F3: R (P1).
- F4: R. Extends AUD-16.
- F5: R. Extends AUD-03.
- F6: R (P5).
- F7: R\*.
- F8: C. The T+3 blind spot (C3).
- F9: C. The cursor walks oldest-first (`AutoTweakerCore.vb:238-243`), and `--apply-manual` leaves the streak, cursor and snapshot untouched (`AutoTweakerProgram.vb:92-144`).
- F10: R (P7, P8).
- F11: R (P4).
- F12: R (P6 and Python).
- F13: C in the code (`ClaudeApiClient.vb:20-21`, `:43-66`). `created_at` is read only as a number, so the first `claude-opus-*` in list order wins. The fallback id is `claude-opus-latest`, and `max_tokens` is 4096. The API-side half (`created_at` is a string; current Opus models think by default) comes from the published API docs, not from a live call.
- F14: C. HC2 (`PromptBuilder.vb:29`) contradicts HC21 (`:116-121`).
- F15: C. The population key omits the session hours (`AutoTweakerCore.vb:164-168`), and a skip discards the whole window (`:274`).
- F16: C. `change_log` is appended at the end (`SettingsDiffApplier.vb:346`), and a config typo silently loads the defaults (`TweakerConfig.vb:116-123`).

**M2 settings**
- F1: R (P1, P4). Extends AUD-09.
- F2: R.
- F3: R. Extends AUD-07.
- F4: R.
- F5: R. Extends AUD-01.
- F6: R. Same as AUD-05.
- F7: R. Same as AUD-15; display-only per the report's §A.3.
- F8, F9, F11: R.
- F10: C in part (the mutate-before-save half). The rest is N.
- F12, F13: R.
- F14: C.
- F15: C. `target_buffer_pct` is divided by 100 (`SignalEmitter.vb:415`).
- F16: C. A62c skips nullables that are present (`verify/ordercheck/Program.vb:12189`).
- F17: C (`SignalEmitter.vb:559-564`).
- The range and sign table: not re-derived.

**M3 exit guard**
- #1: R, on synthetic tape. The real rate is unmeasured, so treat the "CRITICAL" as provisional.
- #2: R. Extends AUD-14.
- #3: R.
- #4: C. The side comes from the radio buttons (`MainForm_ExitGuard.vb:85-92`), and `Evaluate` takes no entry price.
- #5, #6, #7: R.
- #8: R. Same as K2, extended.
- #9: R.
- #10: R. Extends AUD-20.
- #11: R.
- #12: C. Both monitors run on a `Forms.Timer`.
- #13: R. Same as AUD-19.
- #14: C (`MainForm_ExitGuard.vb:34`).
- #15: C. The strip returns early (`MainForm_LiveStrip.vb:103-113`) before alert handling at `:121`.
- #16: C. Its `GetBook` half is AUD-23.

**M4 outcome measurement**
- #1: R + C.
- #2, #3: C.
- #4: R + C.
- #5, #6: R.
- #7: R + C.
- #8: R.
- #9: R (timings). Otherwise the same as AUD-04 and AUD-06.
- #10, #11, #12, #13: R.
- #14: C. The denominator is Success + Failure (`LivePerformanceTracker.vb:117`), which includes rows whose `TargetEverHit` is Nothing.

**M5 tape store**
- F1, F2, F3: R.
- F4: C in the code: three plain `StreamReader` opens (`TradeStoreWriter.vb:586`, `:617`, `:647`). The Windows collision itself is unproven.
- F5, F6: R.
- F7: C (`HistoricalStore.vb:625`).
- F8: C (`TradeStoreWriter.vb:585-599`).
- F9: C.
- F10: C (`WsHealthLog.vb:41-42`, `VenueStatusLog.vb:57-58`).
- F11: C (`WsFeedLog.vb:80-81`, `:103`).
- F12: C (`TradeStoreGapRepair.vb:69-72`).

**M6a UI shell**
- #1: R (P1). Same as AUD-04, plus the case where the flag is flipped to true after boot.
- #2: D (AUD-06).
- #3: R (P4). Same as AUD-07.
- #4: R + C. Severity corrected to S2, because the trigger is narrow (report §B, row B3).
- #5: R + C.
- #6: D (AUD-18).
- #7: C (`Me.Invoke` at `:498`, `:508`).
- #8: C (only visibility is toggled).
- #9: C (`MainForm_TapeStoreStatus.vb:87`).
- #10: C. `OnFormClosing` stops neither auto-run timer.
- #11: C. The interval is clamped at `MainForm_AutoRun.vb:35-36` and saved at `:99-103`, outside any `Try`.
- #12: C (`MainForm_Calibration.vb:30`).

**M6b render**
- Kelly "CRITICAL": D (AUD-15). X: the app never reads `kelly.*`, so this is S3.
- Try/Catch "CRITICAL": D (AUD-09). Its concrete example is hypothetical.
- Absorption "HIGH": **F**. The TAPE strip renders it.
- VPFR: C (`MainForm_Render_Cards.vb:1881`, `VolumeHistogramMini.vb:119`).
- `Math.Abs` R:R: C (`MainForm_PlaintextSnapshot.vb:289,294`).

**M7a offline analysis**
- F1, F2: C.
- F3: C + R\*.
- F4: R (via M4 T7).
- F5: R (via M4 T10).
- F6: R\*. Its first part is the known EVAL-1.
- F7: C.
- F8: C (`MarkdownReportWriter.vb:349-350`).
- F9: C + R\*.
- F10: C. `ForwardWindowJoiner.Load` skips an embedded header and keeps the first file's column map and `HasPlaced`.
- F11: C. The implied threshold is the 70th percentile of non-zero rows only (`FundingMomentumDiagnostic.vb:62`).
- F12: C. The chunk cap returns Nothing (`DeribitOhlcFetcher.vb:63-67`), and the report then blames the network (`AnalysisRunner.vb:79-84`).
- F13: C (`MarkdownReportWriter.vb:741-746`).

**M7b backtest / what-if**
- F1, F2: C + R\*.
- F3: C. Any cell with N > 0 can win (`WhatIfProgram.vb:163-166`); n < 30 is only a display flag.
- F4: consistent with AUD-01.
- F5: R\* (P6).
- F6: C.
- F7: C (`ReplayLoop.vb:457-466`). The muting itself is documented as D2; the finding is that nothing downstream corrects for it.
- F8: R\*.
- F9: C. There is no `BACKTEST-` check in `analysis/`, `WhatIfRunner/` or `CeilingAudit/`.
- F10: C (`OverlapValidator.vb:42`, `:607`).
- F11: R\*.
- F12: C, spot-checked (`WhatIfReplay.vb:163`, `CoverageReport.vb:889`).

**M8 harness**
- The MTF fail-open fixture gap: D (AUD-07). The prompt listed it as known.
- `Apply` never called: D (AUD-16). Listed as known.
- A41 pins maker/maker on the stop arm: C, and **new**.
- No geometry invariant: listed as known; the whole-file grep supports it.

**M9 order app**
- F1: X. The chase cancels beyond 0.6 × ATR; what remains is that a stand-down doesn't cancel.
- F2, F3: C.
- F4: C. Its severity turns on the chase functions nobody read.
- F5: C. X on severity: it needs two non-default settings.
- F6: C that the field is sent. N on what Deribit does with it.
- F7: C in part. The placement ack covers only the entry order (`frmMainPageV2.vb:1902` onward). Whether the order-state branches catch a rejected stop leg is still open; lane M9b covers it (report §7.1).
- F8, F9: C.
- F10: C (`SignalBridge.vb:857-864`).
- F11: C. `ExecuteOrderAsync` returns early before `RegisterPendingPlacement` (`frmMainPageV2.vb:4113`).
- F12: C.

**M10 ops readers**
- CRITICAL: C (mechanism). Its impact depends on whether the pooled books already hold the rotated rows.
- HIGH: C (`LiquidationFlagCheck.vb:159-161` vs `:208-210`).
- MEDIUM, missing bars: C (`SwingFallbackRead.vb:796`).
- MEDIUM, `LfKey`: C (`LiquidationFlagCheck.vb:245-247`).
- LOW: C. It's a methodology note, not a defect.

**Tally** (138 findings across the 12 lanes). Each finding is counted once, under its primary verdict:

| Verdict | Count |
|---|---|
| Reproduced here (R or R\*) | 63 |
| Confirmed by reading the code | 65 |
| Duplicate of an audit-report item, adding nothing new | 7 |
| Holds only in part (M9 F1) | 1 |
| Refuted (M6b absorption) | 1 |
| Confirmed in part; the rest is in lane M9b (M9 F7) | 1 |

Four confirmed findings also had their severity cut: M6a #4 (a narrow trigger), M6b Kelly (the app doesn't read it), M9 F5 (needs two non-default settings) and M3 #1 (synthetic tape only).

---

## 4. The M6a proofs, run for the first time

That lane had no .NET SDK. I linked its `AuditProofs.vb.txt` against the same 69 sources as `verify/auditproofs`, with a four-line `Main`. The run order was P2, P3, P4, then P1, because P1 leaves a task pending. Output, verbatim:

```
P2a: parsed=True rejected=False
P2b: gateFires=False
P2c: Save throws -> singleton keeps NaN, file keeps old value
P3: OverflowException -> escapes UpdateLogInfo -> RunAnalysisAsync -> modal MessageBox
P4: passLong=True passShort=True (MTF: insufficient 15m candles (0))
P1: HANGS (RunAnalysisAsync never reaches EmitBridgeSignal)
```

P1 and P4 call the shipped `LivePerformanceTracker.UpdateAsync` and `IndicatorEngine.CalcMTFGate`. P2 and P3 run copies of the exact expressions from `MainForm_Layout.vb`, which can't be linked off Windows: the `TryParse`/range test, the Step 5c comparison, `JsonSerializer` on NaN, and the `CInt` over `UtcNow − MinValue`.
