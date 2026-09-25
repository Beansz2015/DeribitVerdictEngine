# Fixture harness audit B — `verify/ordercheck/Program.vb` lines 5,601–11,200 (+ fixture-boundary extension)

**Scope actually audited:** `verify/ordercheck/Program.vb` lines **5576–11308** AT commit `6e74181f000ddc7666e8b7d17c64a195855b45cd`, read via worktree `C:\Dev\audit-6e74181-b` (a pre-existing clean worktree at the target commit; used in place of creating a duplicate one, since `C:\Dev\audit-6e74181` was already occupied by a parallel audit on this same commit). This covers fixture **A35d** (whose body straddles the 5,601 line-boundary — included whole, back to its own comment header at 5571) through fixture **A46a** (whose closing brace sits at 11308, past the 11,200 boundary — included whole). The next fixture group, A50, starts at line 11310 and is out of scope.

## Not read in full / skimmed — stated up front

- Nothing in the assigned line range was skimmed. Every line from 5576 to 11308 was read via the `Read` tool (in ~15 overlapping passes, checked for gaps).
- I did **not** read any file outside `verify/ordercheck/Program.vb` except: `CLAUDE.md` (in full, as instructed), and `git log -p --follow` output for `settings.json` (grepped for specific keys, not read end-to-end — see "Provenance checks run" below).
- I did **not** open `Core/ScoringEngine_*.vb`, `SignalEmitter.vb`, `TradeStoreWriter.vb`, `CoverageReport.vb`, `AlertsTracker.vb`, or any other production source file. All three questions below were answered from the fixture code itself, its own comments (many of which quote production constants/line numbers), and the settings.json history grep. Where a claim in this report depends on production code I did not open (e.g. "ClassifyHour's worst-of combine order"), it is sourced from the fixture's own comments describing that code, not from independent verification against the source — flagged inline as such.
- I did not re-derive or independently verify every numeric constant's arithmetic (e.g. Wilson CI bounds, AUC formulas, the logistic-loss math in A39a) — these are treated as testing internal consistency of the harness's own stats helpers, not settings-derived thresholds, so they fall outside the fixture-literal provenance rule's scope.

## Scratchpad — LOGIC TRACE

**--- LOGIC TRACE ---**

Three fixtures closest to the order payload / settings loader, traced against: *a −1.5% flush with ATR(7) rising from 30 to 140 (volatility spiking 4–5×) mid-event.*

**1. `A36a_DefaultsByteIdenticalToV51B4b` (Program.vb:5645) — `SignalEmitter.ComputeSideLevels`, the function that produces the Target/StopPx that become the order payload's price levels.**

Every case in A36 (and its sibling A42) calls `A36Indicators()` / `A42Indicators()`, which hardcodes `r.ATR = 40.0` as a **fixed, static** value. No case in A36a–f or A42a–d ever passes an ATR anywhere near 140. Concretely, this fixture would **not catch**:
- Whether `stop_max_atr_mult × ATR` (1.6 × 140 = 224 pts) or `target_max_atr_mult × ATR` (3.5 × 140 = 490 pts) produces a geometrically sane stop/target once ATR itself has already blown out 4–5× — the arithmetic is linear and untested at that magnitude, so a latent overflow, a sign error that only manifests at large magnitudes, or a floor/clamp that behaves differently at 3-digit ATR values would pass this fixture silently.
- The **lag** interaction: ATR(7) is a smoothed indicator. During the first few bars of a violent flush, the ATR value the engine reads for a given analysis cycle is necessarily a *lagging* estimate of the volatility already in progress — real ATR(7) does not reach 140 instantaneously, it climbs there over several bars while price has already moved. A36/A42 pass ATR as a single frozen snapshot to `ComputeSideLevels`; there is no fixture anywhere in this range that drives ATR through a *rising sequence* into the geometry-arbitration call, so the harness cannot show whether a stop sized on a just-stale, still-low ATR reading is set too tight relative to the volatility that has already materialized by the time the order is placed.
- Any cross-call staleness between the ATR value `ComputeSideLevels` uses for level placement and the ATR value `TradeCostSettings`/A40's min-move floor uses in the same analysis cycle — both are supplied as independent, hand-set `IndicatorResults.ATR` fields in every fixture, so a production bug where the two reads see different candle snapshots mid-flush is structurally invisible to any fixture in this file.

**2. `A40b_DefaultsByteIdenticalToV61Floor` (Program.vb:6649) — the fee-aware min-move floor that gates whether a trade fires at all (`BELOW_MIN_MOVE`).**

The case set is `{13.0, 30.0, 100.0, 100.0}` ATR, entry price fixed at 62000 throughout. This would **not catch**:
- Behaviour at ATR ≈ 140–200 (the flush scenario) is never exercised; the highest ATR tested is 100.
- The min-move floor is a **fixed percentage of price** (`0.0003 + 0.0005 = 0.0008`, i.e. ≈49.6 pts at 62000), while the ATR-scaled fallback target/stop distances **scale with ATR**. As ATR quadruples, the fee floor stays flat while the ATR-derived target distance roughly quadruples too — the fixture never asserts anything about the *relative* scale between the two once they diverge this far, so there is no coverage for a scenario where the fee floor becomes trivially easy to clear (every ATR-scaled placement is now "tradeable" by the floor's own logic) at exactly the moment volatility risk is highest — i.e. the floor's only real job during a calm market (filtering out noise-sized moves) is structurally absent during a flush, and nothing here proves whether that absence is intentional or accidental.
- Price is frozen at 62000 in every case; the −1.5% move itself (price and ATR changing together, inside the same analysis cycle) is not modelled — `IndicatorResults` is always a frozen snapshot in this harness by construction, so simultaneous price/ATR motion cannot appear in any fixture here.

**3. `A57e_ExitGuardClearOnThinAdverseBuffer` (Program.vb:11097) — `ExitGuardEvaluator.Evaluate`, the guard that decides whether to flag an already-open position as adverse.**

This fixture *proves* (and the fixture's own comment names it a "D-4 pinned" ruling) that at the shipped default `MinTradesForScoring` gate (50), a 40-trade heavy-sell buffer reads `Clear` — i.e. the exit guard goes **silent** on an open position — purely because the trade count is thin, even though the *same* buffer would read `Exit`/`AdverseCount≥2` the instant the gate is bypassed. What this fixture would **not catch** in the flush scenario:
- `MinTradesForScoring` is a pure **count** gate with no volatility or time dimension — nothing in `ExitGuardEvaluator.Evaluate`'s signature (as called here) takes ATR or a time-since-last-trade parameter, so a genuine 4–5× volatility spike gives the guard no basis to override a thin-buffer silence even though the exact condition it is silencing itself for (a heavy, one-sided adverse tape) is happening *harder* and *faster* during a flush than in the calm-market case this fixture actually builds (40 trades at 1/ms in the fixture's synthetic clock, vs. the comment's own cited real throughput of ~1.4 trades/sec, i.e. the fixture's own trades arrive far faster than the ~36 s cold-start window the surrounding comment describes for live traffic).
- No fixture in this range drives the guard through a *sequence* where the buffer count crosses the 50-trade threshold **while** the adverse move is ongoing — i.e. there is no proof of how many seconds of "silent, adverse, open position" a real flush would produce before the guard re-arms, only a proof that the two static endpoints (thin+silent vs. full+alerting) both behave as documented.

**--- END LOGIC TRACE ---**

## Provenance checks run

- `git log -p --follow 6e74181 -- settings.json` (run from the main checkout, capped at `6e74181`) dumped to a scratch file and grepped for every settings-derived literal cited by name in an A36/A40/A41/A42/A57 fixture comment as "shipped"/"POCO default":
  - `maker_fee_bps`/`taker_fee_bps`/`round_trip_style`/`min_net_move_pct` (A40/A41): introduced once, at v62, at `1.5`/`3.5`/`"maker_maker"`/`0.0005` — **never changed** in any later commit up to `6e74181`. A40a/A40b/A41a-c's claims about "shipped defaults" are accurate.
  - `target_arbitration_mode`/`stop_arbitration_mode`/`target_buffer_pct`/`stop_buffer_pct`/`use_best_pivot_candidate`/`target_max_atr_mult`/`stop_max_atr_mult`/`stop_min_floor_ticks` (A36/A42): each introduced once at its stated version (v56/v63) at the value the fixture claims (`0`/`0`/`0.0`/`0.0`/`false`/`3.5`/`1.6`/`4`) and never changed since. No provenance violation found.
  - `indicators.TFI.window_size` / MicroCVD window (A57a's derived "50"): confirmed via the v67 `modified_by` changelog entry itself, which states the shipped defaults (TFI 30 / MicroCVD 50) verbatim and explains the derivation — consistent with A57a's hardcoded expectation.
- I did not exhaustively re-verify every numeric literal in every one of the ~130 fixtures against full settings.json history — I verified the specific keys used by the three "closest to the order payload" fixtures (the LOGIC TRACE targets) plus A57a, on the basis that these are the highest-consequence literals in the assigned range. Everything else in this range that touches settings-derived values does so by reading `New EngineSettings()`/`New TradeCostSettings()`/`New AlertsSettings()` POCO defaults directly (not by restating a parallel hardcoded number), which is the pattern the fixture-literal provenance rule asks for, or explicitly reads a `Public Const`/`Public Shared` production value (`TradeStoreWriter.MaxHolesPerPass` in A56f, `TradeStoreWriter.RecentWindowCapacity` in A55e, `TradeStoreWriter.AbsentSeq` throughout A53/A55/A56) rather than restating it.

## Harness run

**PROVEN — ran:** `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` inside the worktree `C:\Dev\audit-6e74181-b`.

Last 20 lines of actual output:

```
[SettingsLoader] settings.local.json: 'change_log' is not overridable — IGNORED (the base owns document metadata)
[SettingsLoader] settings.local.json: 'trade_store.probe_list' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): trade_store.enabled: true -> false · trade_store.probe_list: (absent) -> [9,9] [NO EFFECT]  [1 admitted key(s) absent from the base — see the warning above]
PASS  A50g arrays are single leaves — one applied path for an admitted array, one rejected path for change_log; siblings and the base array survive
[SettingsLoader] settings.local.json: 'scoring.regime_max_score.trending' is not overridable — IGNORED (scoring path)
[SettingsLoader] settings.local.json: 'scoring.regime_max_score.range_bound' is not overridable — IGNORED (scoring path)
[SettingsLoader] settings.local.json: 'scoring.regime_max_score.transitional' is not overridable — IGNORED (scoring path)
[SettingsLoader] settings.local.json: 'scoring.verdict_strong_pct' is not overridable — IGNORED (scoring path)
[SettingsLoader] settings.local.json: 'indicators.RSI.pass2c_midline' is not overridable — IGNORED (scoring path)
[SettingsLoader] settings.local.json present but overrode no key the base carries — base settings in force
PASS  A50h scoring-surface pin — a scoring.*/indicators.* overlay leaves the verdict byte-identical through the real Calculate(), and the same values DO move it when applied directly
[SettingsLoader] settings.local.json: 'network.transport' is not overridable — IGNORED (selects the data source — three run-path signals gate on it)
[SettingsLoader] settings.local.json: 'auto_run.trigger_mode' is not overridable — IGNORED (cadence moves scoring and trigger_mode is not yet a CSV column)
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): network.request_timeout_seconds: 15 -> 45
PASS  A50i network split is key-granular — request_timeout_seconds admitted, transport and auto_run.trigger_mode rejected, base values survive
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): live_strip.enabled: true -> false
PASS  A50j whitelist ∩ UI-writeback — a click on an overlaid key writes the BASE and snaps back; an unrelated save never promotes the overlay value
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): trade_store.enabledd: (absent) -> false [NO EFFECT] · live_strip.enabled: true -> false  [1 admitted key(s) absent from the base — see the warning above]
```

Followed immediately by:
```
PASS  A50k admitted-but-absent key — warned, excluded from +local, base untouched; a real sibling key still applies and still activates
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): auto_run.start_engaged: true -> false
PASS  A58c overlay admits auto_run.start_engaged key-granular (base true -> overlaid false), and the tweaker fence still rejects it (auto_run. stays whole-block fenced)

ALL PASS
```

Totals (via `grep -c "^PASS"` / `grep -c "^FAIL"` on the full captured output): **425 PASS, 0 FAIL.**

## Already-reported defects seen again in this range (not re-reported as new)

- **A41a `NetEvSubtractsFeeDragOnSuccessAndStopArms`, A41b `WindowExpiredArmAlsoPaysFeeDrag`, A41c `FeesZeroCfgIsByteIdenticalToGross`** (Program.vb:6878, 6918, 6946) all pin a single, uniform `RoundTripFeePct` (maker/maker) drag applied identically to the SUCCESS arm and the ADVERSE_HIT (stop) arm. This is the same maker/maker-on-the-stop-arm defect already on record. The fixture family's own header comment (Program.vb:6868) additionally states outright that "the maker→taker emergency loss-arm delta ... is NOT built and no fixture pins its absence" — i.e. the gap is self-documented in the same commit, not newly discovered here.

## Fixture-by-fixture three-question review

Legend: **Q1** = assertion vs resemblance (✅ = asserts the real property; noted otherwise) · **Q2** = fixture-literal provenance (✅ = no bare literal driving other logic / correctly reads cfg or a `Public Const`; "n/a" = fixture has no settings-derived literal at all) · **Q3** = pinned known defect (✅ = none found; "A41" = the already-reported item above).

| Fixture (Program.vb:line) | Q1 | Q2 | Q3 |
|---|---|---|---|
| A35d:5576 | ✅ asserts absence of ladder heading + every WEAK-row shape, plus matrix-still-present parity | n/a | ✅ |
| A36a:5645 | ✅ real `ComputeSideLevels` calls, structural assertions on `.Target`/`.StopPx`/`.Reason` | ✅ POCO defaults, cross-checked against settings.json history (v56, unchanged) | ✅ |
| A36b:5694 | ✅ | ✅ | ✅ |
| A36c:5736 | ✅ | ✅ | ✅ |
| A36d:5780 | ✅ real `ScoringEngine.Calculate()` end-to-end, not just the levels helper | ✅ | ✅ |
| A36e:5834 | ✅ real `SettingsDiffApplier.Validate`/`WhatIfOverlay.Parse` calls | ✅ | ✅ |
| A36f:5881 | ✅ round-trips through `WhatIfSettings.BuildCellSettings` and compares to a direct call | ✅ | ✅ |
| A37a:5949 | ✅ real `AlertsTracker` state machine, counts `PendingEvents` by `Kind` | n/a (uses `AlertsCfgDefault()` POCO) | ✅ |
| A37b:6025 | ✅ | n/a | ✅ |
| A37c:6082 | ✅ splits the on-disk line and checks each field by position, not a substring | n/a | ✅ |
| A37d:6134 | ✅ | n/a | ✅ |
| A37e:6176 | ✅ real `SettingsDiffApplier.Validate` | n/a | ✅ |
| A34f:6201 | ✅ checks specific rendered strings + relative ordering by index (`iA < iU`) | n/a | ✅ |
| A38a:6246 | ✅ counts real lines written to a real file across real calls | n/a | ✅ |
| A38b:6287 | ✅ splits the line and checks fields by position | n/a | ✅ |
| A39a:6336 | ✅ (statistical/mechanism, no settings dependency) | n/a | ✅ |
| A39b:6378 | ✅ | n/a | ✅ |
| A39c:6420 | ✅ | n/a | ✅ |
| A39d:6447 | ✅ | n/a | ✅ |
| A39e:6500 | ✅ checks column names by substring match against a banned list, AND checks transformed-matrix shape (belt-and-braces, self-declared) | n/a | ✅ |
| A40a:6588 | ✅ asserts to 1e-12 against the composed property, not a resemblance | ✅ (see provenance section) | ✅ |
| A40b:6649 | ✅ real `ScoringEngine.Calculate()`, byte-identity across 4 real cases | ✅ | ✅ (see LOGIC TRACE — coverage gap, not a pinned defect) |
| A40c:6722 | ✅ | ✅ | ✅ |
| A40d:6752 | ✅ real `SettingsDiffApplier`/`WhatIfOverlay` calls, distinguishes "unresolvable" from "fragment-banned" | ✅ | ✅ |
| A40e:6813 | ✅ | ✅ | ✅ |
| A41a:6878 | ✅ (assertion is real; the *value* it asserts is the known defect) | ✅ (fee value itself correctly sourced from cfg) | **A41** (already reported) |
| A41b:6918 | ✅ | ✅ | **A41** |
| A41c:6946 | ✅ | ✅ | **A41** |
| A41d:7000 | ✅ | n/a (StdPop is a hand-computed known constant, not a settings key) | ✅ |
| A42a:7074 | ✅ incl. an explicit "populated but ignored" negative case per candidate | ✅ | ✅ |
| A42b:7145 | ✅ | ✅ | ✅ |
| A42c:7206 | ✅ | ✅ | ✅ |
| A42d:7252 | ✅ | ✅ | ✅ |
| A43a:7350 | ✅ boundary-exact checks (`<=` vs `<`) | n/a | ✅ |
| A43b:7378 | ✅ | ✅ **explicitly declared MECHANISM per-line, with the fixture-literal-provenance rule quoted verbatim in-comment** (Program.vb:7399-7423) — a documented, already-resolved provenance finding from a prior sweep (`fixture-parser-clean-run-2026-09-22.md`), not a new one | ✅ |
| A43c:7447 | ✅ | n/a | ✅ |
| A43d:7495 | ✅ compares two real `Calculate()` runs field-by-field | n/a | ✅ |
| A43e:7559 | ✅ reflection against the real `Private Shared Header` field, not a restated copy | n/a | ✅ |
| A44a:7626 | ✅ | n/a | ✅ |
| A43f:7663 | ✅ (and self-documents a prior unit-convention defect it now catches — A47b is its belt-and-braces sibling) | n/a | ✅ |
| A45a:7752 | ✅ four sub-checks incl. a documented, already-fixed §8.6 wall-clock-fallback defect, explicitly named as such in-comment | n/a | ✅ (documented, not newly found) |
| A47a:7836 | ✅ | n/a | ✅ |
| A47b:7922 | ✅ (the "pin that was missing" — cross-series scale check A43f could not provide) | n/a | ✅ |
| A48a:8021 | ✅ | n/a | ✅ |
| A48b:8088 | ✅ (explicitly named as insufficient on its own — see A55 family) | n/a | ✅ |
| A48c:8135 | ✅ | n/a | ✅ |
| A48d:8190 | ✅ drives the real `HistoricalStore.BackfillTradeMonthAsync` for the DR-3 sub-case | n/a | ✅ |
| A48e:8250 | ✅ | n/a | ✅ |
| A48f:8307 | ✅ calls the real `TradeStoreWriter.ShouldCapture`/`ShouldGapRepair` gates directly (explicitly rewritten to do so after an earlier internal-mirror weakness, per its own comment) | fixture asserts POCO's own default values (30/500/6.0/20.0) — this is testing the default constructor itself, not passing a parallel literal into other logic; not the pattern the provenance rule targets | ✅ |
| A48g:8358 | ✅ | n/a | ✅ |
| A48h:8403 | ✅ | n/a | ✅ |
| A49a:8499 | ✅ | n/a | ✅ |
| A49b:8528 | ✅ | n/a | ✅ |
| A49c:8563 | ✅ | n/a | ✅ |
| A49d:8592 | ✅ | n/a | ✅ |
| A49e:8635 | ✅ | n/a | ✅ |
| A49f:8687 | ✅ | n/a | ✅ |
| A49g:8718 | ✅ | n/a | ✅ |
| A49h:8753 | ✅ (pins the decision, not the process exit code, and says so) | n/a | ✅ |
| A49i:8778 | ✅ | n/a | ✅ |
| A49j:8814 | ✅ | n/a | ✅ |
| A49k:8855 | ✅ | n/a | ✅ |
| A49l:8882 | ✅ | n/a | ✅ |
| A49m:8904 | ✅ | n/a | ✅ |
| A49n:8940 | ✅ | n/a | ✅ |
| A49o:8982 | ✅ mutation-proof named explicitly | n/a | ✅ |
| A49p:9027 | ✅ mutation-proof named explicitly | n/a | ✅ |
| A49q:9061 | ✅ | n/a | ✅ |
| A49r:9102 | ✅ | n/a | ✅ |
| A49s:9127 | ✅ | n/a | ✅ |
| A49t:9173 | ✅ | n/a | ✅ |
| A49u:9219 | ✅ | n/a | ✅ |
| A49w:9277 | ✅ | n/a | ✅ |
| A49v:9299 | ✅ | n/a | ✅ |
| F1a:9344 | ✅ mutation-proof named explicitly | n/a | ✅ |
| F1b:9371 | ✅ | n/a | ✅ |
| F1c:9397 | ✅ | n/a | ✅ |
| F1d:9426 | ✅ mutation-proof named explicitly | n/a | ✅ |
| F1e:9460 | ✅ mutation-proof named explicitly | n/a | ✅ |
| F1f:9492 | ✅ constructed directly against `BuildConsoleSummary`, no store I/O, so nothing else can mask the assertion | n/a | ✅ |
| A61a:9515 | ✅ mutation-proof named explicitly, with the exact pre-fix figure computed and compared | n/a | ✅ |
| A61b:9557 | ✅ isolates the mechanism directly against `ClassifyHour`, not the Reason string | n/a | ✅ |
| A61c:9587 | ✅ | n/a | ✅ |
| A61d:9614 | ✅ | n/a | ✅ |
| A61e:9642 | ✅ | n/a | ✅ |
| A61f:9680 | ✅ two independent scenarios, mutation-proof named explicitly | n/a | ✅ |
| A51a:9773 | ✅ reproduces the real "June wipe" shape | n/a | ✅ |
| A51b:9822 | ✅ | n/a | ✅ |
| A51c:9853 | ✅ | n/a | ✅ |
| A51d:9890 | ✅ | n/a | ✅ |
| A51e:9935 | ✅ | n/a | ✅ |
| A52a:11159 | ✅ | ✅ (explicitly named as "the drift guard" against JSON/POCO disagreement) | ✅ |
| A53a:10017 | ✅ | n/a | ✅ |
| A53b:10060 | ✅ | n/a | ✅ |
| A53c:10106 | ✅ names the naive failure mode and computes it (`collapsedTo`) for comparison | n/a | ✅ |
| A53d:10128 | ✅ | n/a | ✅ |
| A53e:10155 | ✅ uses two REAL Deribit trades from the §1 verification gate, proven indistinguishable on the legacy 5 fields before asserting the identity-aware result | n/a | ✅ |
| A53f:10179 | ✅ order-independence explicitly checked | n/a | ✅ |
| A53g:10216 | ✅ | n/a | ✅ |
| A53h:10248 | ✅ incl. the "false-clean" trap (no-sequence store ≠ clean store) | n/a | ✅ |
| A55a:10314 | ✅ real Deribit data, legacy-key collision proven before asserting the fix | n/a | ✅ |
| A55b:10356 | ✅ | n/a | ✅ |
| A55c:10398 | ✅ | n/a | ✅ |
| A55d:10438 | ✅ | n/a | ✅ |
| A55e:10474 | ✅ | ✅ reads `TradeStoreWriter.RecentWindowCapacity` rather than restating 20000 — correct, explicitly cited as the reason (F1 lesson) | ✅ |
| A55f:10513 | ✅ | n/a | ✅ |
| A55g:10548 | ✅ | n/a | ✅ |
| A56a:10630 | ✅ mutation-proof named explicitly | n/a (constructed inputs, MECHANISM, declared once for the whole family at Program.vb:10600-10606) | ✅ |
| A56b:10676 | ✅ five sub-cases incl. an explicitly-named inversion (GT-6) from a prior ruling | ✅ | ✅ |
| A56c:10761 | ✅ builds a genuinely out-of-order file and proves it (`genuinelyUnsorted`) rather than assuming it | ✅ | ✅ |
| A56d:10807 | ✅ two shapes, the second explicitly justified as the one that actually tests the decision | ✅ | ✅ |
| A56e:10863 | ✅ | ✅ | ✅ |
| A56f:10900 | ✅ | ✅ reads `TradeStoreWriter.MaxHolesPerPass` rather than restating 32 — the one fixture in the family explicitly marked SHIPPED BEHAVIOUR, correctly | ✅ |
| A56g:10971 | ✅ mutation-proof hand-traced against the pre-fix algorithm (5 phantom holes) in-comment | ✅ | ✅ |
| A57a:11039 | ✅ | ✅ (see provenance section — confirmed against v67 changelog) | ✅ |
| A57c:11055 | ✅ mutation-proof named explicitly ("hardcode-trap catcher") | ✅ | ✅ |
| A57d:11068 | ✅ real `SettingsDiffApplier.Validate` | ✅ | ✅ |
| A57e:11097 | ✅ real `ExitGuardEvaluator.Evaluate`, bypass-vs-default comparison | n/a | ✅ (documented D-4 ruling — not a pinned-as-correct bug; see LOGIC TRACE for the coverage gap around it) |
| A58a:11137 | ✅ | n/a | ✅ |
| A58b:11149 | ✅ explicitly scoped as "JSON-contract only", says what it does NOT prove | n/a | ✅ |
| A46a:11204 | ✅ drives the real `ForwardWindowJoiner.Load → PopulateForwardBars → FailureRateMatrix.Compute → BandLadder.Compute → MarkdownReportWriter` chain end-to-end | n/a | ✅ |

## Findings

**SEVERITY:** S3
**LOCATION:** `verify/ordercheck/Program.vb:5629-5932` (A36a-f) and `:7058-7325` (A42a-d) AT `6e74181`
**DOWNSTREAM IMPACT:** The geometry-arbitration test family (`SignalEmitter.ComputeSideLevels`, which produces the Target/StopPx that flow into `VerdictResult` and ultimately the order payload) has zero coverage at ATR values consistent with a volatility-spike regime. Every fixture in both families hardcodes `r.ATR = 40.0`.
**FAILURE SCENARIO:** During a fast, violent move (ATR(7) climbing 30→140), a defect that only manifests at large ATR magnitudes — e.g. a clamp/floor computed with an intermediate value that overflows or rounds differently at 3-digit ATR, or a sign/ordering bug in the tier-walk that only reorders candidates once distances exceed some other constant — would ship undetected, because no fixture anywhere in this file drives `ComputeSideLevels` with an ATR outside roughly [13, 100].
**ANALYTICAL CRITIQUE:** This is a coverage gap, not a proven defect (READ FROM CODE — not executed; I did not construct a case at ATR=140 to check for an actual break, since the point of the finding is that the harness itself never does). The gap is structural: every `IndicatorResults` in this harness is a hand-built, frozen snapshot, so the harness has no mechanism for expressing *regime change* (a rapidly rising ATR sequence) at all — only static points on the ATR axis. Given this engine explicitly trades short (2–15 minute) holds around exactly this kind of event, the absence of any high-ATR case in the fixture family that determines the actual stop/target prices is a real, if currently unrealized, blind spot.

**SEVERITY:** S3
**LOCATION:** `verify/ordercheck/Program.vb:6588-6851` (A40a-e) AT `6e74181`
**DOWNSTREAM IMPACT:** The fee-aware min-move floor (`TradeCostSettings.EffectiveMinMovePct`, which gates `BELOW_MIN_MOVE` and therefore whether an order is placed at all) is tested only at ATR ∈ {13, 30, 100, 100} and a fixed entry price of 62000. There is no case exercising the floor's behaviour once the ATR-scaled target/stop distance has grown to several multiples of the floor itself.
**FAILURE SCENARIO:** The floor is a fixed fraction of price (~0.08% ≈ 49.6 pts at 62000) while ATR-scaled distances grow with ATR. During a volatility spike the floor's filtering function (rejecting noise-sized moves) becomes vacuous — every ATR-scaled placement clears it trivially — at precisely the moment a false-positive-tolerant gate would matter most. No fixture proves whether this is the intended design (the floor is meant only to catch calm-market noise, and ATR-scaling is meant to take over during volatility) or an unconsidered interaction.
**ANALYTICAL CRITIQUE:** READ FROM CODE — not executed. Same structural cause as the previous finding (frozen-snapshot fixtures, no regime-change modelling). Distinct from the A41 already-reported item: A41's defect is about which arms pay the fee drag; this finding is about the floor's *sensitivity* never being tested outside a narrow ATR band.

**SEVERITY:** S3
**LOCATION:** `verify/ordercheck/Program.vb:11097-11116` (A57e) AT `6e74181`
**DOWNSTREAM IMPACT:** `ExitGuardEvaluator`'s thin-buffer silence (ruled and documented in-repo as "D-4") is proven correct at its two static endpoints (40 trades / gate bypassed → alerts; 40 trades / gate at shipped default → silent) but never under a condition where the adverse move and the volatility spike are simultaneous and ongoing.
**FAILURE SCENARIO:** `MinTradesForScoring` is purely count-gated (no ATR or elapsed-time input). During the exact −1.5%-flush scenario, the guard could remain silent on an open, adverse position for as long as the trade buffer takes to refill past the derived minimum (the fixture's own surrounding comment cites "~36 s after a seed failure" as the documented worst case) — precisely the highest-risk window for an unmonitored open position. No fixture drives the buffer through that transition live.
**ANALYTICAL CRITIQUE:** READ FROM CODE — not executed. This is explicitly a *ruled, accepted* engine behaviour (D-4), not a pinned-as-correct bug in the A41 sense — I am not asserting the ruling is wrong. The gap is narrower than the engine behaviour itself: no fixture in this harness demonstrates how the guard behaves as ATR is spiking, because ATR is not even a parameter the fixture (or, per its signature as called here, the function) considers. Given the trader-facing rule in `CLAUDE.md` that a scoring/exit-facing change is a RESERVED class requiring explicit sign-off, this is worth surfacing as a named coverage gap even though the underlying design decision is not in question.

No S0, S1, or S2 findings in this range. No new (i.e. not already on the excluded list) instance of a fixture asserting a false property, a resemblance-only check standing in for the real one, or a bare literal that silently drifted from settings.json history was found in the ~130 fixtures reviewed.
