# Fixture harness audit A — `verify/ordercheck/Program.vb` lines 1–5,628, at commit `6e74181`

Hostile audit. Scope: `verify/ordercheck/Program.vb` lines 1–5,600 (extended to line 5,628 to
finish the fixture the range boundary lands inside, `A35d_PromptBuilderOmitsLadderAndWeak`), and
`verify/ordercheck/OrderCheck.vbproj`. Audited tree: git worktree at commit `6e74181`
(`fix(fixtures): declare A43b's tfiWindowSize MECHANISM — the violation the clean run found`).
Nothing newer than that commit was read as "in scope"; two small, targeted excerpts of
`Core/Settings/EngineSettings.vb` and `Core/SignalEmitter.vb` were read **only** to write
compiling proof code (see §3) and are cited by exact line, not as part of the audited harness.

## 0. Not read in full

- **Nothing in the assigned scope was skipped.** `Program.vb` lines 1–5,628 were read
  continuously (Main's dispatch table, every helper, every fixture body from `A1_CvdSlopeRising`
  through `A35d_PromptBuilderOmitsLadderAndWeak`), and `OrderCheck.vbproj` was read in full.
- **Outside the assigned scope, not read at all:** `Program.vb` lines 5,629–16,764 (fixtures
  `A36` onward — `A36Cfg`/`A36a` begin at line 5,629, cleanly past the 5,600 boundary, so no
  fixture body there was opened). Every other `.vb` file in the tree, except two short,
  line-cited excerpts of `Core/Settings/EngineSettings.vb` (lines 905–983, 1001–1019) and
  `Core/SignalEmitter.vb` (a few grep hits around lines 230–455) read solely to get correct
  property names for the proof harness in §3 — not read as audited files, and no claim below
  treats them as exhaustively reviewed.
- `docs/*.md` referenced by the fixtures' own comments (e.g.
  `docs/placed-geometry-derivation-2026-07-06.md`, `docs/fee-aware-min-move-proposal.md`) were
  **not** opened; every claim below is checked against the fixture code and, where stated, against
  `git log -p --follow -- settings.json` at `6e74181`, not against the specs.
- `settings.json` itself (the tracked file at `6e74181`) was not read directly; its **history**
  was read via `git log -p --follow -- settings.json` (5,764 lines dumped) and grepped for the
  specific keys named below.

## 1. Method

1. Read the full assigned range once, straight through, taking the fixture-by-fixture notes in
   §4.
2. Spot-checked a sample of the file's own MECHANISM/SHIPPED provenance annotations against the
   real settings.json history at `6e74181` (not today's tree) — see §2 note. The file already
   carries an unusually dense, dated audit trail (`I17-SWEEP`, `A54a` S2, the fixture-literal
   provenance rule) from prior audit passes; where I re-checked a claim it held, so most of §4's
   Q2 answers below rely on that self-documentation rather than re-deriving every literal's
   history independently — flagged explicitly where that is the case.
3. Ran the existing harness unmodified (§3).
4. Wrote two small, independent proof fixtures against the same real linked sources
   (`verify/proofcheck/`, a scratch project in the worktree only, never committed) to convert two
   suspected coverage gaps into a run I actually executed. Output quoted verbatim in §3 and §5.
5. Wrote this report from the main checkout.

## 2. LOGIC TRACE

Scenario: BTC-PERPETUAL flushes -1.5% (e.g. $62,000 → $61,070) inside a few bars, with ATR(7)
rising from 30 to 140 as realised volatility catches up to the move. Three fixtures in range,
picked for proximity to the order payload and to the settings-mutation gate (the actual
`SettingsLoader` fixtures, `A50a`–`A58c`, are defined near the end of the file — *after* line
16,000, out of this range's scope; the closest in-range analogue is the auto-tweaker's
`SettingsDiffApplier.Validate` family):

**`A22a_PayloadFieldByField`** (`Program.vb:2756`) — builds the actual `verdict_signal.json`
payload the order app trades off, via `SignalEmitter.BuildOk`. Every case in it
(`BuildBridgeIndicators`, `Program.vb:2726`) is a **single static snapshot**: one fixed ATR
(41.3), one fixed price (59012.5), fixed carried swing levels that are always on the correct side
of price by construction. It would **not catch**: (a) a payload built moments after a flush,
while the 7-period ATR is still lagging the true move — no case varies ATR against a moving price
to check that target/stop *distances* (ATR multiples) still make sense once ATR has partially
caught up; (b) a carried structural level that price has already crossed since the level was
computed (e.g. a `SwingStopLong` sitting *above* `CurrentPrice` on a long, i.e. already breached)
— every A22a case keeps swing levels on the correct side of price by hand, so a same-tick
stale/crossed level is simply never constructed, let alone asserted on. (This is a *specific*
instance of the already-reported "no fixture asserts stop/entry/target geometry generically"; it
is named here only as the trace requires, not re-raised as a separate finding in §5.)

**`A26c_StopShapes`** (`Program.vb:3400`) — the DG1 stop arbitration (`SWING_STOP` /
`STOP_CLAMPED` / `FALLBACK_ATR` / floor-guarded) that produces the exact stop value the payload
above carries. Every one of its five cases runs at **`ATR=40.0` fixed**
(`BuildPgIndicators`, `Program.vb:3323-3328`), and none of them ever reads
`cfg.Scoring.TradeCosts`. It would **not catch**: (a) the DG1 floor
(`stop_min_floor_ticks × TickSize` = 4 × $0.5 = **$2.00**) being a flat dollar amount that does
not move with ATR at all — re-running A26c's own near-floor case at ATR=140 (the top of this
scenario's range) produces the *identical* $2.50 accepted stop, now 89× tighter than the
ATR-fallback distance the same call would use absent structure; (b) that floor, expressed as a
fraction of price (~0.40 bps), sitting far below even the cheapest round-trip fee the engine's own
`TradeCostSettings` prices (maker_maker, 3.0 bps) — exactly the transaction-cost exposure a stop
is most likely to eat when it fires *during* a fast flush, per `TradeCostSettings`' own doc
comment naming "emergency SL repositioning" as the taker-cost case it deliberately does not price.
Both of these are proven by a run in §3/§5, Finding F1 — the trace is what motivated writing that
proof.

**The `SettingsDiffApplier.Validate` family** (`A15f_ValidateRejectsOffSurfaceKeys` at
`Program.vb:1995`, and its siblings `A20g`/`A21a`/`A21b`/`A25b`/`A26g`/`A28d`/`A31g`) — the closest
in-range stand-in for "the settings loader": the gate on what the AI auto-tweaker can propose into
`settings.json`. Every one of these ~15 fixtures tests **path resolvability and HARD CONSTRAINT
prefix fences only**; none varies the proposed *value*. It would **not catch**: (a) a tweak fired
during the volatility event that turns `scoring.structural_levels.stop_max_atr_mult` or
`target_max_atr_mult` — the exact bounds the stop arbitration above enforces — into a nonsense
number; Finding F2/§5 proves `Validate` accepts both a negative multiplier and a six-figure one on
an on-surface, unfenced key; (b) any notion of *when* a proposal is safe to apply — no fixture
checks Validate() being re-evaluated against a settings tree whose own numbers are moving
(a live-market analogue of the "hot-reload mid-`InstanceId`" hazard `CLAUDE.md`'s RESERVED-class
table already names for `settings.json` changes in general). `SettingsDiffApplier.Apply` currently
throwing on .NET 8 (already reported) is the only thing stopping a bad value like this from
reaching the live file today — the validation layer itself gives no assurance either way.

## 3. Harness run

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

Ran clean. **425 PASS, 0 FAIL, exit code 0.** Last 20 lines of stdout (full output kept at
[`proofs/fixture-harness-a/harness_A_run_output_full.txt`](proofs/fixture-harness-a/harness_A_run_output_full.txt)):

```
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
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json present but overrode no key the base carries — base settings in force
[SettingsLoader] Re-read settings.local.json — overlay present: True · active: False
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json present but overrode no key the base carries — base settings in force
PASS  A50k admitted-but-absent key — warned, excluded from +local, base untouched; a real sibling key still applies and still activates
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): auto_run.start_engaged: true -> false
PASS  A58c overlay admits auto_run.start_engaged key-granular (base true -> overlaid false), and the tweaker fence still rejects it (auto_run. stays whole-block fenced)

ALL PASS
```

(Note: the tail of the run is `A50`/`A58c`, fixtures *defined* past line 16,000 and out of this
range's audit scope — they still execute because `Sub Main` calls the whole suite in one process.
The run is quoted in full for exit-code honesty, not as evidence about out-of-range fixtures.)

## 4. Proofs run (independent of the harness above)

Full session and commands: [`proofs/fixture-harness-a/README.md`](proofs/fixture-harness-a/README.md).
Source: [`ProofCheck.Program.vb.txt`](proofs/fixture-harness-a/ProofCheck.Program.vb.txt) /
[`ProofCheck.vbproj.txt`](proofs/fixture-harness-a/ProofCheck.vbproj.txt). Raw output:
[`proof_run_output.txt`](proofs/fixture-harness-a/proof_run_output.txt). Both proofs link the
same real shipped sources `OrderCheck.vbproj` links (identical `Compile Include` set), so they run
against production code, not copies.

```
Proof 1 -- DG1 stop floor is flat-dollar and fee-blind across a 30->140 ATR spike
  cfg: StopMinFloorTicks=4 TickSize=0.5 StopMaxAtrMult=1.6 TradeCosts(maker=1.5bps taker=3.5bps style=maker_maker minNet=0.0005)
  ATR=30.0    StopReason=SWING_STOP     stopDist=$2.50 (0.403 bps of price)  vs round-trip fee 3.0 bps (maker_maker), vs TARGET floor 8.0 bps, vs ATR-fallback stop dist $48.0
  ATR=140.0   StopReason=SWING_STOP     stopDist=$2.50 (0.403 bps of price)  vs round-trip fee 3.0 bps (maker_maker), vs TARGET floor 8.0 bps, vs ATR-fallback stop dist $224.0

Proof 2 -- SettingsDiffApplier.Validate has no numeric-bounds check on a proposable key
  stop_max_atr_mult 1.6 -> -50.0        : IsValid=True ErrorReason=''
  target_max_atr_mult 3.5 -> 999999.0   : IsValid=True ErrorReason=''
```

## 5. Findings

Severity scale per the audit brief: S0 wrong-side/unprotected/unbounded order under shipped
config · S1 systematic negative-EV or stale-data orders, or a silent total outage · S2 realistic
trigger with bounded impact, or a contract gap · S3 edge case, latent path, or advisory
inconsistency · S4 nit.

**Already reported, excluded here** (per the audit brief): the 15m MTF fail-open with no fixture;
`SettingsDiffApplier.Apply`/`ApplyRevert` never called (both throw on .NET 8); no fixture asserts
stop/entry/target geometry generically; `A41a`–`c` pin a uniform maker/maker fee drag on the stop
arm (out of this range anyway — `A41` is defined past line 5,600).

---

### F1 — S1 — the DG1 stop floor is a flat dollar amount, untested against ATR or against `TradeCosts`, and can sit far inside the round-trip fee it will pay

**Location:** `verify/ordercheck/Program.vb:3319-3434` (`A26c_StopShapes` and its shared
`BuildPgIndicators`/`BuildPgCfg` helpers) at `6e74181`. Production code cited for context only,
not audited: `Core/SignalEmitter.vb:230-231` (`TickSize = 0.5`) and `:455`
(`stopFloor = sl.StopMinFloorTicks * TickSize`); `Core/Settings/EngineSettings.vb:932-983`
(`TradeCostSettings`).

**Downstream impact:** the placed stop this arbitration produces is exactly what
`A22a`'s payload and the live tracker's `AdvBar` (`A27a`) carry into `verdict_signal.json` and the
CSV — i.e. what the order app would place as the protective stop, and what a live position's real
risk is measured against.

**Failure scenario:** proven by a run (§4, Proof 1). At the shipped POCO defaults, a structurally
placed stop sitting 4 ticks + $0.50 ($2.50) from entry is accepted as `SWING_STOP` **identically**
at ATR=30 and ATR=140 — the floor does not read ATR. At ATR=140 the ATR-fallback stop the *same*
call would use absent structure is $224, i.e. the accepted structural stop is 89× tighter than
the volatility the engine's own indicator says is current. Expressed as a fraction of price
(~0.40 bps) that accepted stop is roughly 1/7 of the *cheapest* round-trip fee the engine's own
`TradeCostSettings` prices (maker_maker, 3.0 bps) and about 1/20 of the TARGET side's own
fee-aware floor (`EffectiveMinMovePct`, 8.0 bps default). `TradeCostSettings`' own doc comment
(`EngineSettings.vb:940-944`) names "emergency SL repositioning" as exactly the taker-cost case
this floor does not price — the -1.5% flush this trace is built around is precisely that case.

**Analytical critique:** the code side of this is a documented, intentional design choice (the
floor deliberately prices only the TARGET/profit path — see the class doc comment at
`EngineSettings.vb:907-930`), not a hidden bug; the *harness* gap is what is being audited here.
No fixture in the audited range — not `A26c` (fixed ATR=40 throughout), not `A13`/`A40` (the only
fee-floor fixtures in range, both target-side only) — ever puts these two facts in the same test:
a near-floor structural stop, and the fee schedule it will actually pay. The risk this leaves
unguarded is systematic under *default* shipped config: it requires no misconfiguration, only a
structural swing level that happens to land near the 4-tick floor, which is exactly when a stop is
tightest relative to the market noise a violent flush produces. Rated S1 rather than S0 because no
single order is wrong-sided or unbounded — the risk is a stop that is real and correctly placed on
the correct side of price, just economically dominated by the cost of exiting it. **Proven by a
run** (§4).

### F2 — S3 — `SettingsDiffApplier.Validate` has no numeric-bounds check on any proposable key

**Location:** every `Validate(...)` call across `A15f_ValidateRejectsOffSurfaceKeys`
(`Program.vb:1995`), `A15g` (`:2018`), `A15h` (`:2039`), `A20g`/`A20h` (`:2654`/`:2663`),
`A21a`/`A21b` (`:2675`/`:2686`), `A25b` (`:3288`), `A26g` (`:3534`), `A28d` (`:3810`), `A31g`
(`:4435`) — fifteen call sites in range, none of which varies the proposed *value*, only the
*path*.

**Downstream impact:** `SettingsDiffApplier.Validate` is the acceptance gate for whatever the
AI-driven auto-tweaker (`ClaudeApiClient.vb`, linked but not exercised for real calls) proposes
for `settings.json`, hot-reloaded onto the live collector per `CLAUDE.md`'s own
`settings.json changes` RESERVED-class rationale.

**Failure scenario:** proven by a run (§4, Proof 2). A diff moving
`scoring.structural_levels.stop_max_atr_mult` from 1.6 to **-50.0**, and a second diff moving
`target_max_atr_mult` from 3.5 to **999999.0**, both return `IsValid = True`. Both keys are
on-surface, resolvable, and outside every HARD CONSTRAINT prefix this file's fixtures fence
(`HC21`, `A26g`), so `Validate` treats them exactly like a sane retune.

**Analytical critique:** rated S3 rather than higher because `SettingsDiffApplier.Apply` currently
throws on .NET 8 (already reported), which is the *only* thing stopping a value like this from
reaching the live file today — so the path is real but currently dead. It stops being dead the
day `Apply` is fixed, and nothing in this fixture family would need to change to notice: every one
of the fifteen fixtures above already has the shape (`Validate(OneDiff(path, old, new), s, 3)`)
needed to add a bounds case; none does. **Proven by a run** (§4).

### F3 — S3 — `A31h_TwoAtrScaleInvariance` proves the ATR→dollar scaling arithmetic, but never a single tracker whose ATR changes mid-episode

**Location:** `verify/ordercheck/Program.vb:4479-4539` (`A31h_TwoAtrScaleInvariance`).

**Downstream impact:** `LevelAbsorptionTracker`'s proximity/band/break-tolerance gates for the
absorption indicator that scores into `ScoringEngine` and logs into `analysis_log.csv`.

**Failure scenario, read from code (not run — no proof was written for this one; see caveat
below):** the fixture's own comment names the mechanism precisely: "[v61] The three tick keys
retired; `SetLevels` now carries resolved dollar distances (the carry-site pattern)"
(`Program.vb:4136-4139`). `A31h` demonstrates the *conversion arithmetic* (ATR-fraction → dollars)
doubles cleanly by building **two independent `LevelAbsorptionTracker` instances**, one seeded at
ATR=44 and one at ATR=88 (`Program.vb:4505-4515`) — never one tracker whose `SetLevels` is called
again with a new, larger distance while an episode from the old (narrower) distance is still open.
The audit brief's own scenario (ATR(7) rising from 30 to 140 inside one run) is exactly the
untested case: whether an already-open episode's proximity/band gate is refreshed to the new,
wider ATR-derived distance, or stays anchored to the pre-spike width, is not decided by anything
in this range.

**Analytical critique:** rated S3 (latent path) rather than higher because I could not verify from
`Program.vb` alone which behaviour the shipped `LevelAbsorptionTracker` (out of audited scope)
actually has — this is a coverage gap in the harness, not a demonstrated production defect. Flagged
as **read from code**, explicitly not proven by a run, so it is not overweighted next to F1/F2.

### F4 — S4 — `A26c_StopShapes` never tests the DG1 floor's own boundary (distance exactly equal to the floor)

**Location:** `verify/ordercheck/Program.vb:3400-3433`.

**Failure scenario, read from code:** the five cases test distance 50 (within bound → `SWING_STOP`),
100 (over the 64 bound → `STOP_CLAMPED`), none (→ `FALLBACK_ATR`), 1.0 (under the $2.00 floor →
`FALLBACK_ATR`), and the short mirror at 50. None tests distance = 2.0 exactly (the floor value
itself), so the `>` vs `≥` convention at the floor boundary is unpinned.

**Analytical critique:** low impact (S4) — the fixture already pins values immediately either
side of the boundary (1.0 below, 2.5 above in Proof 1), so a boundary-convention slip would need
a swing stop landing on the exact cent to matter, and even then the two adjacent branches
(`FALLBACK_ATR` vs `SWING_STOP`) are both individually sane. Named for completeness.

## 6. Fixture-by-fixture: (1) assert-vs-resemble · (2) provenance · (3) pins-a-defect

All fixtures below are in `verify/ordercheck/Program.vb`, commit `6e74181`. "Provenance"
column reports the file's *own* in-line MECHANISM/SHIPPED classification where one is given
(the overwhelming majority carry one — this file has already been through several audit/fixture
-literal-provenance passes, per `CLAUDE.md`'s own history). Marked **(spot-checked)** where I
independently re-ran `git log -p --follow -- settings.json` at `6e74181` against the specific
claim; marked **(carried)** where I read the annotation and the surrounding code but did not
re-derive the settings history myself — per `CLAUDE.md`'s instruction to separate verified from
carried claims, these are carried, not verified, though none contradicted anything I did check.
"Pins a defect" is **No** throughout this range unless stated — no instance of the `A41a`-shape
problem (pinning a known-wrong value as the expected/correct one) was found in lines 1–5,628.

| Fixture(s) | (1) Asserts the named property? | (2) Literal provenance | (3) Pins a defect? |
|---|---|---|---|
| `A1_CvdSlopeRising` | Yes — chronological RISING via `CalcCVD` on a constructed old-sell/new-buy tape. | MECHANISM, explicit in-line (`Program.vb:837-859`); slope_pct_of_value history 0.01→0.05→0.10 **(spot-checked, matches `git log -p`)**. | No |
| `A2_MicroCvdBullAccel` | Yes, but see F-adjacent note: comment documents `floorPct` is MASKED (inert only because `dynamicPct=0`), not truly insensitive — already self-corrected in-line, not a live gap. | MECHANISM, explicit (`:920-944`) **(carried)**. | No |
| `A3_MicroCvdWindowFromEnd` | Yes — window boundary at exactly 50 is load-bearing (band = single point). | MECHANISM, explicit (`:968-984`) **(carried)**. | No |
| `A4_TfiWindowFromEnd` | Yes — 30-trade window edge is load-bearing. | MECHANISM, explicit (`:1010-1020`) **(carried)**. | No |
| `A5_NormsRecentWindow` | Yes — asserts the 100-bar recent window, not the oldest. | No settings-derived literal (pure candle construction). | No |
| `A6_ObvNormalisation` | Yes — `trendGate` load-bearing band pinned at [0,47.9]/[48,∞). | MECHANISM, explicit and dated (`:1078-1088`); trend_gate history 0.001→10.0→18.0→23.0 **(spot-checked, matches `git log -p` exactly)**. | No |
| `A7_DonchianPriorWindow` | Yes. | No settings-derived literal. | No |
| `A8_DominantSideCascade` (2 checks) | Yes — exact score cascade (7/11 SHORT, 11/11 TIE) via the real `Calculate()`. | `fundingBoost` is a scenario knob, not a shipped-value claim. | No |
| `A9_MtfPerSideFlags` | Yes, with an explicit and unusually thorough self-critique: the fixture documents that its own one-at-a-time literal bands look inert only because of a spare bear vote, and that the real property (all three votes cast) is separately pinned by `A64a`. | MECHANISM, extensively documented, dated `I17-SWEEP 2026-09-06` **(carried — the file records this was itself corrected once)**. | No |
| `A10_KellyInverseLeverage` | Yes — exact contracts/risk/lev-capped arithmetic. | POCO defaults, not a settings-history claim. | No |
| `A11_CandleFreshness` (3 checks) | Yes. | No settings-derived literal (uses real `DateTimeOffset.UtcNow`). | No |
| `A12_LinearLevels` (2 checks) | Yes — the legacy quadratic-vs-linear boundary is pinned exactly at the raw-target value. | Deliberately non-1 `ATRScaleFactor` to expose the retired bug; explicit rationale (`:1344-1360`). | No — this fixture exists specifically to *prevent* re-pinning the old (quadratic) defect. |
| `A13_MinTradeableMoveGate` (4 checks) | Yes — target-side fee-composed floor, byte-for-byte (`0.0008 × 62000 = 49.6`). | Explicit v62 composition rationale in-line (`:1401-1406`); see F1 for the STOP side this fixture (correctly, per its own name) never touches. | No |
| `A14a`–`A14j` (11 fixtures) | Yes throughout — session/resolution table lookups, ROC overrides, ATR-on-3-min gate flip, freshness-by-resolution, tweaker-population independence. | `BuildResolutionCfg` literals explicitly marked MECHANISM/off-spec on purpose (`:1479-1485`) **(carried)**. | No |
| `A15a`–`A15h` (8 fixtures) | Yes — population filter, resolution homogeneity, legacy-row default, session derivation, re-seed-on-filter-change, and (`A15f`–`h`) `Validate` path/prefix rejection. See F2 for the value-bounds gap these never touch. | No settings-history literal at stake (pure filter/path logic); `A15f`'s example diff values are scenario constants. | No |
| `A16a`–`A16e` (5 fixtures) | Yes — connection-health gate vs legacy age-gate vs the 15m REST/WS refresh-policy split, each isolated cleanly. | No settings-derived literal. | No |
| `A17a`–`A17h` (8 fixtures) | Yes — adverse-count/structural-break primitives, end-to-end evaluator, `CalcHoldStatus` byte-identical string, and the D3 single-adverse→Clear ruling. | No settings-derived literal (pure signal combinations). | No |
| `A18a`–`A18e` (5 fixtures) | Yes — bar-roll fire-once/no-fire/multi-bar-catchup/resolution-switch/backstop arithmetic, all on constructed epoch-ms. | No settings-derived literal. | No |
| `A19a`–`A19e` (5 fixtures) | Yes — TFI/spread/imbalance via the reused pure fns, nearest-level bracketing, tape-speed window/lull, empty-buffer blanks. | No settings-derived literal. | No |
| `A20a`–`A20i` (9 fixtures) | Yes — `CalcOFI` byte-identity + shared-helper equality, edge cases, EMA steady-state/time-aware-step/geometric-symmetry, warmup/reset, tweaker fence (`HC16`) + accepted sibling. | `A20a`/`A20b`'s 2.0/0.5 ratio and `bookDepth:=5` are corrected in-line as ever-shipped (2026-04-22 → v48) rather than purely synthetic — the file records its own earlier mis-classification and fix. **(carried, per the file's own note; I did not re-run the settings history myself for this specific pair.)** | No |
| `A21a`, `A21b` | Yes — removed dead-key rejection + `scoring.hold_` prefix fence, each paired with an accepted live sibling. | Settings tree literals are scenario JSON, not shipped-value claims. | No |
| `A22a`–`A22g` (7 fixtures) | Yes — full payload field-by-field (3 placement cases + held-position pass-through), SKIPPED shape, NO-TRADE→direction-NONE incl. lean tags, ws-health/confidence enum pins, ARM flag + process identity, invariant-culture serialization, `HC18` fence. See §2 trace for the single-static-snapshot limitation. | `cfg.Version = 51` pinned deliberately "so the pass-through is visible" (`:2705-2710`) — an explicit, justified literal, not a shipped-value claim. | No |
| `A23a`–`A23g` (7 fixtures) | Yes — steady-rate analytic fixed point, two-horizon burst detection, cold-start suppression, reset re-arm, classify-edges, per-session resolution (with an in-line correction: "the old text claimed LONDON/ASIA still inherit... stale since v60/v65", `:3141-3146`), 3-tier tweaker surface (`HC19`). | Explicit, dated, self-correcting **(carried)**. | No |
| `A24a` | Yes — CSV `Placed*` ≡ payload levels across 3 cases + an absolute pin. | No settings-derived literal. | No |
| `A25a`, `A25b` | Yes — R1 retirement byte-identity + "flag still gates" + `HC20` momentum-prefix fence. | No settings-derived literal at stake. | No |
| `A26a`–`A26g` (7 fixtures) | Yes — structural-target-wins-even-when-farther, tier ladder walk (swing→HVN→POC→fallback), DG1 stop shapes (see F1, F4), min-move gate on the *placed* target, session fallback multiplier, `enabled:false` byte-identical rollback, `HC21` 3-tier fence. | POCO defaults = shipped v51 values, stated explicitly (`:3319-3321`). | No |
| `A27a`–`A27d` (4 fixtures) | Yes — live tracker barriers ≡ `ComputeSideLevels` (3 cases), offline placed-vs-legacy routing, D4 before/after render, eval-cache v4→v5 rotation. | No settings-derived literal. | No |
| `A28a`–`A28d` (4 fixtures) | Yes — TFI-burst upgrade/soften/no-op, regimeMax cap, S2a session-scoping (with an in-line correction noting D3 armed ASIA and the "un-armed" exemplar had to be *constructed*, `:3771-3778`), `HC22` fence. | Explicit, dated, self-correcting **(carried)**. | No |
| `A29a`–`A29e` (5 fixtures) | Yes — anchored classification, "anchor is newest ≥W not oldest-in-ring" (the load-bearing tier-selection pin), cold-start/post-gap FLAT, 30-min eviction with no count cap, and the cadence-invariance pin (30s vs 180s agree on state, not on delta). | `FundT0` is an arbitrary fixed epoch, explicitly labelled as having no wall-clock dependence. | No |
| `A30a`–`A30d` (4 fixtures) | Yes — adapter≡`ComputeSideLevels` ("no copies" guarantee), whitelist reject/accept, threshold-overlay population shift, POC-tier-closed-in-replay. | No settings-derived literal at stake. | No |
| `A31a`–`A31h` (8 fixtures) | Yes — episode lifecycle, analytic absorb-ratio case, D8 conservation + pullFrac veto, break-through/re-arm, cold/degenerate never-throws, 5 reserved CSV columns, session `min_aggr_usd` resolution + `HC23`, two-ATR scale invariance. See F3 for the one gap this range leaves (mid-episode ATR change). | Historic v54 test geometry (12t/4t/2t) re-pinned explicitly as absolute dollars for continuity (`:4148-4152`). | No |
| `A60e`, `A60a`–`A60d` (5 fixtures) | Yes — three-way header byte-parity (not just column count), round-trip of all 5 instrumentation columns with a documented spec-back correction on the reset-detection route (`:4759-4764`), idle-renders-empty-not-zero, episode-sec measures+resets (with a reflection-based check specifically added to close a hole the spec's own wording would have left untested), frozen pre-build column-position baseline. | `A60d`'s four index constants are explicitly and correctly justified as a frozen baseline, not settings-derived (`:4815-4823`). | No |
| `A32a`–`A32d` (4 fixtures) | Yes — placed-vs-legacy favourable-barrier routing + counters + gate distance, tweaker window-only pick + pre-migration history parse-tolerance, single-column D4 render, the 2026-07-17 floored-grid collapse reproduced-and-shown-impossible. | No settings-derived literal at stake. | No |
| `A33a`–`A33c` (3 fixtures) | Yes — empty-bars→NO_DATA vs covered→SUCCESS vs degenerate→WINDOW_EXPIRED unchanged, NO_DATA excluded from num/denom but counted in TotalRange, v5→v6 sweep reclassifies only the genuinely-uncovered row. | No settings-derived literal. | No |
| `A34a`–`A34e` (5 fixtures; `A34f` is called by `Main` but its body is defined outside this range — not reviewed) | Yes — success-render flip across report/full-markdown/CSV, tweaker trigger proven unchanged under the flip (independently re-derived, not just asserted), WEAK excluded from strip aggregate with its own tooltip aggregate, display-helper MEDIUM-prefix pin, and the load-bearing stored-form-unchanged pin (CSV/payload/eval-cache still bare LONG/SHORT). | E1 v55 2026-07-21 flip explicitly dated; no settings-derived literal. | No |
| `A35a`–`A35d` (4 fixtures) | Yes — band-ladder full render (3 bands + pooled + diagnostic + renumbering), WEAK-vs-NO-TRADE classifier divergence from `CanonicalTier` (explicitly cross-checked so a future consumer can't cross-wire them), matrix cell space unchanged at 12 cells, PromptBuilder omits the ladder/WEAK entirely. | No settings-derived literal. | No |

## 7. What I did not verify

- Every Q2 answer in §6 marked **(carried)** relies on the file's own in-line dated commentary,
  not an independent re-run of `git log -p --follow -- settings.json` for that specific key. I
  spot-checked three (`OBV.trend_gate`, `CVD.slope_pct_of_value`, `min_net_move_pct`) and all three
  matched exactly; I did not re-check the rest individually — doing so for every literal in ~90
  fixtures was out of proportion to what a spot-check already confirmed is a reliable source.
- F3 is **read from code, not proven by a run** — I chose not to build a proof for it because
  doing so honestly would have required opening `Core/LevelAbsorptionTracker.vb` (out of the
  audited file list) to learn whether `FoldBook`/`Snapshot` ever re-derive geometry from something
  other than the last `SetLevels` call, and I did not want to extend the audited-file set past what
  was assigned. It is reported as a harness-coverage gap, which is true regardless of the answer to
  that question, not as a demonstrated production defect.
- I did not attempt to reproduce the "1.6× / v50" values `A57b`'s deletion note (outside this
  range) or any other cross-reference to fixtures numbered above `A36` — those are out of scope
  and are mentioned only where the in-range file's own comments name them for context (e.g. `A64a`
  guarding `A9`'s configuration).
- The two production-file excerpts read for the proof harness (`EngineSettings.vb`,
  `SignalEmitter.vb`) were read only far enough to get correct property names and the exact
  `TickSize`/`stop_min_floor_ticks`/`TradeCostSettings` arithmetic right — I did not review those
  files for correctness beyond what F1's proof exercises.
