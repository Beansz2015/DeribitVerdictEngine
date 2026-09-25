# Adversarial audit follow-up: review batch summary (2026-09-24)

**What this is.** The record of the 12 follow-up reviews that prompts M1–M10 in [`../adversarial-audit-2026-09-24.md`](../adversarial-audit-2026-09-24.md) §7 produced, and one ranked list of every defect that survived checking. The working document for a reviewer (handles, decisions, what my prompts got wrong, what's unverified) is [`2026-09-24-review-batch-spec-back.md`](2026-09-24-review-batch-spec-back.md).

**Pinned to.** Engine commit `6e74181` (settings v68) and order app commit `8232e9e`. Every finding and verdict here is against those trees. Master moved on after the reviews started. The trader ruled that the newer code is out of scope, so nothing here was re-checked against it.

**Where the reviews live.**
- The 11 engine-side reviews are merged into this branch as they were pushed: `docs/audits/2026-09-24-*.md`, with proofs under `docs/audits/proofs/`.
- M9 ran in the order app's repo. It is copied byte for byte into [`order-app/`](order-app/README.md).

**How each claim was checked.**
- Every proof set that ships a runnable harness was re-run in this session and matched its recorded output. That covers five compiled VB/C# harnesses and four Python ports.
- I also ran M6a's proofs, which that session had never executed.
- Claims with no proof were checked by reading the cited code. The ones I didn't read are marked, not assumed.

---

## 0. Read these first

1. **The first S0.** Three defects combine, and none of them needs a misconfiguration:
   - The engine's 4-tick stop floor lets a `SWING_STOP` sit 2 USD under a long's entry (AUD-05, M2 F6).
   - The order app places its entry at its own best bid, and may chase it up to 0.6 × ATR from the first bid it saw. It never compares that price with `levels.entry` (M9 F3).
   - The app never checks which side of the fill the stop is on (M9 F2).

   So a long that fills 2.5 USD or more under the engine's entry goes to Deribit with its sell-stop trigger at or above the fill. A settings-inverted stop (AUD-03) takes the same path. What Deribit does with that stop is not verified. Either answer costs money: an immediate stop-out, or a rejected stop leg that nothing notices (M9 F7). How often a swing stop lands that close to entry isn't measured.
2. **Every success-rate surface is unfit for rulings as it stands.** That means the perf strip, the failure matrix, the band ladder, the offline report, the what-if runner and the auto-tweaker's trigger. The reviews found the same three flaws in all of them, independently:
   - **No fee-inclusive EV anywhere.** The loss path costs 5 bps, not 3. Found by M4, M7a, M7b and M8, and consistent with AUD-01.
   - **A two-minute blind spot with the entry still priced at T.** The walk starts at the bar closing at T+3, and the fastest stop-outs happen before it. Found by M1, M4, M7a and M7b. M7b measured the bias at +0.19 ATR for a 0.6 × ATR stop.
   - **Consecutive minute rows counted as independent trades.** M4 cites 12.6 NY signals per episode. So CIs and n ≥ 30 gates overstate the evidence by roughly an order of magnitude. Found by M4, M7a and M7b.

   On top of those, **the live strip's NY numbers carry no outcome information at all** (M4 #1, reproduced). Every bar in its OHLC cache is a roughly one-second stub of the minute's first trade, because the completed bar never replaces it. So every touch reads as a timeout. Any ruling that leaned on these surfaces should be treated as unverified.
3. **The Kelly findings drop to display-only.** The order app never reads `kelly.*`; it appears only in a comment (`SignalBridge.vb:678`, app repo). So AUD-15, M2 F7 and M6b's "CRITICAL" are S3: an inconsistent display, not a sizing hazard.
4. **The harness audit (M8) is effectively not done.** My prompt shipped with an unfilled `<RANGE>` placeholder and should have been three prompts. The session read about 1,100 of 16,764 lines, and three of its four findings restate the known-gap list the prompt gave it. That's my error, not the reviewer's.
5. **Two corrections to reviewer claims:**
   - **M9 F1 overstates.** The entry chase cancels the order once price drifts more than 0.6 × ATR (`frmMainPageV2.vb:2383-2385` long, `:2436-2438` short). The bridge can't start with that guard unchecked (`SignalBridge.vb:316`). So a stale bid can't rest for hours and fill later. What remains is that a stand-down never cancels the working entry.
   - **M6b's absorption "HIGH" is refuted.** The TAPE strip renders the live absorption tag and burst state (`UI/MainForm_LiveStrip.vb:224-272`).

---

## 1. Ranked list of confirmed defects

**Severity** uses the scale from the audit report. S0 is a wrong-side, unprotected or unbounded order on the exchange under shipped config. S1 is systematic negative-EV or stale-data orders, or a silent total outage. S2 is a realistic trigger with bounded impact, or a contract gap. S3 is an edge case, a latent path or an advisory inconsistency.

Band C's measurement defects place no orders, so none of them is S0 or S1 on this scale. They rank high anyway, because they decide which settings ship.

**Evidence codes:**
- **R** — reproduced here by re-running the shipped code.
- **R\*** — reproduced by a Python port of the code.
- **C** — confirmed by reading the cited code.
- **N** — not checked by me; it rests on the reviewer.

### Band A: the order path

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| A1 | **S0** | A long can reach Deribit with its stop at or above the fill. The engine's 2-USD `SWING_STOP` floor, the app's entry at its own bid (slipping up to 0.6 × ATR) and the missing side check combine to do it. A settings-inverted stop takes the same path | AUD-05, AUD-03, M2 F6, M9 F2, F3 | R (H-P16, H-P05), R\* (M9 `trace.py` cases B, C), C (`SignalBridge.vb:687`, `:1053`; `frmMainPageV2.vb:3721-3723`) |
| A2 | S1 | A triggered stop becomes a post-only limit (`stop_limit`, `post_only`, `trigger: last_price`). Getting out depends on the app's chase plus the optional M.SL market fallback; with WS down in a flush the loss has no bound | M9 F4 | C (`frmMainPageV2.vb:4081-4090`, `:2575-2580`). The chase functions themselves were not read |
| A3 | S1 | The min-move gate prices the target path only, maker/maker. It can be switched off silently by a maker rebate, a negative `min_net_move_pct`, or a NaN typed into the UI box | AUD-01, M2 F5, M6a #5, M7b F4, M8 (A41) | R (H-P03, M2 P7, M6a P2), C (`MainForm_Layout.vb:1570-1588`) |
| A4 | S1 | WS trades carry no age gate on a connected socket | AUD-02 | R (H-P08) |
| A5 | S1 | Nothing validates settings values that reach scoring. Session names act as unchecked foreign keys across three blocks; sign slips bring back padding; `min_of` 0/1 bans shorts; wrong JSON types pass the tweaker and kill the next reload | AUD-03, M2 F2, F3, F4, M1 F5 | R (H-P05, M2 P2, P3, P5, P13, P14, P18, M1 P2) |
| A6 | S1 | The 15m hard veto fails open on missing or short data, keeps a failed-fetch cache with no age bound, and can only vote bull below 50 bars | AUD-07, M2 F3, M6a #3, M8 | R (H-P01, H-P02, M6a P4, M2 P5) |
| A7 | S2 | Freshness is emission time, checked against a window the payload itself declares (`2.5 × exec_resolution_min`, no upper bound) | AUD-06, M9 F8, M6a #2 | C (`SignalBridge.vb:602`) |
| A8 | S2 | A stand-down (stale, SKIPPED, ARM off) never cancels the working entry, so it can still fill | M9 F1 (corrected) | C (`SignalBridge.vb:521-533`) |
| A9 | S2 | The stop leg is sent with `trigger_offset` 30. The app's own spec says the exchange then trails it | M9 F6 | C (the field is sent); what Deribit does with it: N |
| A10 | S2 | Risk sizing is uncapped when `max_size_usd` ≤ 0. A 2-USD stop sizes to 737,540 USD. Needs two non-default settings | M9 F5 | C (`SignalBridge.vb:1123-1128`, `AppUserSettings.vb:44-52`) |
| A11 | S3 | Nearest-tick rounding ignores direction, ±12.5 % of a 2-USD stop | AUD-10, M9 F9 | C (`frmMainPageV2.vb:381`) |

### Band B: the collector stops

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| B1 | S1 | `performance_display.enabled` false at boot, or flipped to true later, hangs every run before emission. There's no dialog and no log line | AUD-04, M6a #1, M4 #9 | R (H-P14, M6a P1) |
| B2 | S1 | Any exception in a run raises a modal MessageBox that stops auto-run. Proven triggers: inverted clamp pairs (`ATR.scale_min > scale_max`, both display-only keys), `"trade_costs": null`, `VPFR.num_buckets 0` | AUD-09, M2 F1 | R (M2 P1, P4) |
| B3 | S2 | The status line's `CInt` overflows on first connect, before any frame has arrived. The REST seed stamps trades fresh, so the degraded branch doesn't catch it. Rare, but the result is B2's total stall | M6a #4 | R (M6a P3, the expression), C (`MainForm_Layout.vb:1987`; `MarketState.SeedTrades`) |
| B4 | S2 | `request_timeout_seconds` is read once, in a static constructor. A 0 poisons REST for the rest of the process, even after the file is fixed | M2 F13 | R (M2 P19), C (`DeribitClient.vb:26-29`) |
| B5 | S3 | A guard disabled at launch never starts, and neither does gap repair; the hot-reload comments claim otherwise | M3 #14, M5 F12 | N |

### Band C: decision integrity (every ruling reads these)

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| C1 | S2 | The live OHLC cache freezes forming-bar stubs. The completed bar never replaces the stub (`>` against `maxExisting`), so NY strip outcomes are really timeouts | M4 #1 | R (M4 T1: 37 of 37 bars High=Low), C (`LivePerformanceTracker.vb:536-548`) |
| C2 | S2 | No fee-inclusive EV on any surface. Green at 51 % where this geometry needs about 78 % | M4 #2, M7a F1 | C, R\* (M7a replica) |
| C3 | S2 | The walk starts at T+3 while the entry stays at T. The bias is directional and grows as stops tighten | M1 F8, M4 #4, M7a F3, M7b F1 | C (`ForwardWindowJoiner.vb:274`), R (M4 T11), R\* (M7b P5) |
| C4 | S2 | Minute rows are counted as independent trades, so the Wilson CIs, n ≥ 30 and ★ picks are overstated | M4 #3, M7a F2, M7b F3 | C (`FailureRateMatrix.vb:362-372`), R\* (M7a: 6 rows = 1 episode) |
| C5 | S2 | What-if: the six `tier_floor.*` knobs are whitelisted but never applied, because the replay reads the logged effective score | M7b F2 | C (no `TierFloor` reference in `WhatIfReplay.vb`), R\* (P7) |
| C6 | S2 | What-if picks winners with no minimum n, across different row populations | M7b F3 | N |
| C7 | S2 | Tweaker: a response it can't parse counts as a successful round, and "no cell reached n ≥ 30" reads as 0 % failure. The resulting snapshot tops the revert ranking | M1 F3, F6 | R (M1 P1, P5) |
| C8 | S2 | Tweaker (dormant only because Apply throws): it writes the tracked `settings.json`, not the one the engine reads, and a rename-save fires no watcher event. Naming a parent object bypasses every path fence. A revert restores `signal_bridge.enabled` and Kelly wholesale | M1 F1, F2, F11 | C (`TweakSettingsForm.vb:607` vs `MainForm_Layout.vb:379`), R (P2, P4, P9 Linux) |
| C9 | S2 | ◆ "highest success" always picks the longest window | M4 #12, M7a F4 | R (M4 T7: 800 of 800) |
| C10 | S2 | A window with 1 of 13 bars present is scored as complete | M4 #8, M7a F5 | R (M4 T10) |
| C11 | S2 | A floor edit while running stops re-evaluation; a floor change at startup turns rows older than 7 days into NO_DATA | M4 #5, #6 | R (M4 T3, T4) |
| C12 | S2 | SwingFallbackRead builds its population from pooled + live only, so rows sitting only in the current `.bak` vanish with no funnel line | M10 | C (`SwingFallbackRead.vb:268`). Impact depends on what the pooled books contain |
| C13 | S2 | The validator's OI check matches labels the engine never emits | M7b F6 | C (`OverlapValidator.vb:511` vs `MainForm_Analysis.vb:374-376`) |
| C14 | S3 | The session cells drop the last UTC hour of every session | M4 #7 | R (M4 T2), C (`LivePerformanceTracker.vb:621`, `:656+`) |
| C15 | S3 | The stale `AdverseFallbackAtrMultiplier = 1.2` (settings 1.6) gives legacy-yardstick rows a tighter stop | M7a F7 | C (`AnalysisConstants.vb:26`, `FailureRateMatrix.vb:96,103`) |
| C16 | S3 | OutlierAudit returns ASYMMETRIC_ALGORITHM when no regime qualifies | M7a F9 | C (`OutlierAudit.vb:95-107`) |
| C17 | S3 | Fixture A41 pins maker/maker drag on the stop arm as correct, so it will reject the fix to A3 | M8 | C (`verify/ordercheck/Program.vb:6872`) |

### Band D: the exit tools a trader acts on

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| D1 | S2 | The 5m swing pivot is "confirmed" by the forming bar. The flush that breaks it deletes it, and the guard clears at −1 % | AUD-14, M3 #2 | R (H-P13, M3 U6) |
| D2 | S2 | The exit guard latches on noise: 45–55 false EXITs per hour on synthetic balanced tape. The flow arms double-count the same prints. **Real-tape rate unmeasured** | M3 #1, #6 | R (M3 noise control, trace) |
| D3 | S2 | The latch auto-clears at maximum drawdown, and the guard reads the side from radio buttons, not the real position | M3 #3, #4 | R (#3 trace); #4 N |
| D4 | S2 | The absorption tag is manufactured by the approach itself: a top-10 ladder can't see the band depth | M3 #5 | R (M3 U7) |
| D5 | S3 | The cascade alarm is dead on WS. When fed, it infers the side from the aggressor, which is K2's defect, and does disk I/O under the MarketState lock | K2, M3 #8 | R (M3 U4) |
| D6 | S3 | MicroCVD votes "adverse" at exhaustion and abstains during the one-way leg | M3 #7 | R (M3 U1) |
| D7 | S3 | Approach alerts are a 2 s-sampled state, invisible at flush speed | M3 #9 | R (M3 stats) |

### Band E: tape store integrity

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| E1 | S2 | A torn row mid-file becomes a 3×10¹⁵ phantom hole, a silent lost trade, or a tail window disabled for the rest of the month | M5 F1 | R (M5 T1a–d) |
| E2 | S2 | Repair reports `PASS_CLEAN` over pages that never reached disk, and `AppendRows` counts rows a full disk refused, so the status reads NORMAL | M5 F2, F3 | R (M5 probe, `/dev/full`: 86 counted, 0 written) |
| E3 | S3 | The funding coverage check counts each sample twice, so a half-filled month reads as covered | M5 F6 | R |
| E4 | S3 | A full-month scan runs on the WS receive thread, under a lock the UI polls: 1.6 s on 1.8M rows here | M5 F5 | R |
| E5 | S3 | The REST seed blocks the WS subscribe, so capture goes dark for the whole retry budget | M5 F9 | C (`DeribitWsFeed.vb:215-218`) |

### Band F: settings and hot-reload plumbing

| # | Sev | Defect | Found by | Evidence |
|---|---|---|---|---|
| F1 | S2 | A hot-reload mid-run mixes two settings versions. The row is stamped with the old one | M2 F9 | R (M2 P8) |
| F2 | S2 | UI saves mutate the live singleton before `Save`, carry no version bump, and a failed save leaves the change live | M2 F10, M6a #5 | C (`MainForm_Layout.vb:1584`) |
| F3 | S2 | A duplicated block reads three ways: the engine takes the last copy, A62 checks the first, and any box with an overlay fails to parse | M2 F11 | R (M2 P12) |
| F4 | S3 | A parse failure is misreported, and at startup it drops the overlay | M2 F12 | R (M2 P9–P11) |
| F5 | S3 | Session hours are fixed UTC. The London and US opens move 2026-10-25 and 2026-11-01 | M2 F14 | C |
| F6 | S3 | `stop_max_atr_mult` and `atr_stop_multiplier` are one number in two tunable keys | M2 F8 | R (M2 P15) |

Every remaining finding is S3/S4 or a restatement. Each has its verdict in §3.

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

A finding carries the reviewer's own severity until §1 gives it a consolidated one.

**M1 AutoTweaker**
- F1: C + R (P9, Linux; the Windows watcher is unverified).
- F2: R (P2).
- F3: R (P1).
- F4: R. Extends AUD-16.
- F5: R. Extends AUD-03.
- F6: R (P5).
- F7: R\*.
- F8: C. The T+3 blind spot (C3).
- F9: N.
- F10: R (P7, P8).
- F11: R (P4).
- F12: R (P6 and Python).
- F13: N. Needs the live Models API.
- F14, F15, F16: N.

**M2 settings**
- F1: R (P1, P4). Extends AUD-09.
- F2: R.
- F3: R. Extends AUD-07.
- F4: R.
- F5: R. Extends AUD-01.
- F6: R. Same as AUD-05.
- F7: R. Same as AUD-15; display-only per §0.3.
- F8, F9, F11: R.
- F10: C in part (the mutate-before-save half). The rest is N.
- F12, F13: R.
- F14: C.
- F15, F16, F17: N.
- The range and sign table: not re-derived.

**M3 exit guard**
- #1: R, on synthetic tape. The real rate is unmeasured, so treat the "CRITICAL" as provisional.
- #2: R. Extends AUD-14.
- #3: R.
- #4: N.
- #5, #6, #7: R.
- #8: R. Same as K2, extended.
- #9: R.
- #10: R. Extends AUD-20.
- #11: R.
- #12: N.
- #13: R. Same as AUD-19.
- #14, #15: N.
- #16: the `GetBook` half is AUD-23; the rest is N.

**M4 outcome measurement**
- #1: R + C.
- #2, #3: C.
- #4: R + C.
- #5, #6: R.
- #7: R + C.
- #8: R.
- #9: R (timings). Otherwise the same as AUD-04 and AUD-06.
- #10, #11, #12, #13: R.
- #14: N.

**M5 tape store**
- F1, F2, F3: R.
- F4: N. Windows share modes.
- F5, F6: R.
- F7, F8: N.
- F9: C.
- F10, F11, F12: N.

**M6a UI shell**
- #1: R (P1). Same as AUD-04, plus the case where the flag is flipped to true after boot.
- #2: D (AUD-06).
- #3: R (P4). Same as AUD-07.
- #4: R + C. Severity corrected to S2, because the trigger is narrow (§1 B3).
- #5: R + C.
- #6: D (AUD-18).
- #7: C (`Me.Invoke` at `:498`, `:508`).
- #8: C (only visibility is toggled).
- #9 to #12: N.

**M6b render**
- Kelly "CRITICAL": D (AUD-15). X: the app never reads `kelly.*`, so this is S3.
- Try/Catch "CRITICAL": D (AUD-09). Its concrete example is hypothetical.
- Absorption "HIGH": **F**. The TAPE strip renders it.
- VPFR: N.
- `Math.Abs` R:R: C (`MainForm_PlaintextSnapshot.vb:289,294`).

**M7a offline analysis**
- F1, F2: C.
- F3: C + R\*.
- F4: R (via M4 T7).
- F5: R (via M4 T10).
- F6: R\*. Its first part is the known EVAL-1.
- F7: C.
- F8: N.
- F9: C + R\*.
- F10 to F13: N.

**M7b backtest / what-if**
- F1, F2: C + R\*.
- F3: N.
- F4: consistent with AUD-01.
- F5: R\* (P6).
- F6: C.
- F7: N. The muting is documented as D2.
- F8: R\*.
- F9, F10: N.
- F11: R\*.
- F12: N.

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
- F7: N.
- F8, F9: C.
- F10, F11: N.
- F12: C.

**M10 ops readers**
- CRITICAL: C (mechanism). Its impact depends on whether the pooled books already hold the rotated rows.
- HIGH, MEDIUM ×2, LOW: N.

**Tally** (138 findings across the 12 lanes). Each finding is counted once, under its primary verdict:

| Verdict | Count |
|---|---|
| Reproduced here (R or R\*) | 63 |
| Confirmed by reading the code | 24 |
| Duplicate of an audit-report item, adding nothing new | 7 |
| Holds only in part (M9 F1) | 1 |
| Refuted (M6b absorption) | 1 |
| Not checked | 42 |

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
