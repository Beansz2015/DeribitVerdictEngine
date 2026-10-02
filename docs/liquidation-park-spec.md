# Liquidation park spec — `D-4` (a) PARK, with the `D-5` maker-side fix

**Written:** 2026-10-01 (UTC), `date -u` = `Thu Oct  1 14:48:32 UTC 2026`. Base commit `20f5373` (`master`, 1 ahead of `origin/master`; one unrelated uncommitted file, `tools/ops/burst_outcome_read.py`, not touched).
**Ruling implemented:** `D-4` RE-RULED 2026-09-30 (UTC) by the trader = (a) PARK, with Rider 1 (`D-5`) and Rider 2 (A4 as research). The ruling text is the box at the top of [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4, and **that text wins over this spec**.
**Supersedes:** the 2026-09-16 Session B design in [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4.1–§4.2 (measure, then parse or enrich). [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4.3 (`D-5`), §4.4 (`D-6`) and the `EF-3` ruling still bind, and this spec carries them.
**Status:** SPEC. No code, settings or fixture edit was made. **The build is reserved in two classes (scoring code, rendered value). Ask the trader before dispatch.**

**IDs used here:**

| ID | Source and kind | Meaning |
|---|---|---|
| `D-4`, `D-5`, `D-6` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.3, trader decisions | `D-4` the live liquidation flag (re-ruled 2026-09-30: park) · `D-5` maker-side booking · `D-6` the unit of `large_liq_size` |
| `L-1`, `L-2`, `L-3` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R, findings | `L-1` the stream never delivers the flag · `L-2` `M` booked to the taker's side · `L-3` the manual says BTC, the code compares USD |
| `EF-3` | [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6, trader decision | An unrecognised `liquidation` value is skipped and counted, never booked |
| `#7` | [`liq-cascade-level-alerts-proposal.md`](liq-cascade-level-alerts-proposal.md), feature number | The liquidation-cascade alarm (TAPE strip tag, flash, `liq_events.log`) |
| `#8` | same proposal, feature number | The level-approach alert. Shares the `alerts` block and `AlertsTracker` with `#7`. **NOT parked** |
| A4 (research item) | [`trader-tick-queue.md`](trader-tick-queue.md) §1 row `E7` | The liquidation × OFI flip study. ⚠ **Not fixture `A4`** (`A4_TfiWindowFromEnd`, the TFI window fixture). Two different things share the name |
| `HSR-4`, `HSR-11`, `HSR-13` | [`history-store-queue-reshape-evaluation.md`](history-store-queue-reshape-evaluation.md), trader decisions 2026-09-29 | `HSR-4` the liquidation-size study · `HSR-11` replays may join logged per-run OFI values · `HSR-13` the A4 gate |
| `AT-6`, `AT-L2` | [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §6 and §7, decisions | `AT-6` (b): a scoring fix off the burst path may ship after 2026-11-25 with an era stratum · `AT-L2`: the known-defect gate hatch closes when `D-4` ships |
| `A81a`, `A81b` | `verify/ordercheck/Program.vb`, harness fixtures | `T`-flag pin (passes) / `M`-flag known-defect repro (skips unless `ORDERCHECK_KNOWN_DEFECTS=1`) |
| `A82b`, `A82c` | same file, harness fixtures | Mirror-symmetry run / "every vote site was reached" marker check |
| `A37a`–`A37e` | same file, harness fixtures | `AlertsTracker` cascade math, approach episodes, sidecar shape, reset, `HC25` fence |
| **`LP-1`–`LP-8`** | **This spec §9, decisions (NEW)** | Checked free with `git grep -n -w` on 2026-10-01: 0 hits each |
| **`A81c`, `A81d`, `A81e`, `A37f`, `A37g`** | **This spec §8, fixtures (NEW, proposed)** | Checked free the same way: 0 hits each. ⚠ A plan, not a fact — the tree decides what ships (the `A56b` lesson in `CLAUDE.md`) |

---

## 0. Implementer brief — read this before anything else

**Model:** Opus
**Effort:** high
**Seats:** one agent, one session. **Ask the trader before dispatch** (reserved build).

### Why that tier

- The code is small: about 40 lines of engine code, four render sites, five or six fixtures.
- **The judgment is done, but the build is reserved in two classes** — scoring code and a rendered value. A wrong guard is invisible today, because `LiqSignal` is `NONE` on every live row. That is exactly the class `CLAUDE.md` says must not slip through.
- **The render half has ZERO fixture coverage.** `MainForm` is not linked into `verify/ordercheck`. Four sites must change together, and nothing checks them except a run and a screenshot.
- Two fixture edits must change MEANING, not just values (`A81b`, `A82c`). A weaker model tends to delete a failing marker rather than replace it with the pin that keeps the property guarded.

### Where it will slip — the concrete traps

| # | Trap | Why a fixture will not catch it |
|---|---|---|
| T1 | **`MT` written with the one-line boolean trick** `If (t.Direction = "buy") <> (t.Liquidation = "M")`. That is the `E-2` mutation recipe in [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1. It is right for `T` and `M` and **silently wrong for `MT`** (books one side only) | `MT` has zero occurrences in any store. `A81c` must assert BOTH sizes, see T2 |
| T2 | **`A81c` asserts only `signal = NONE`.** One `MT` trade booked to both sides gives `NONE` (1,000 is not ≥ 2 × 1,000). **A skipped `MT` trade also gives `NONE`.** | The fixture passes on the bug. Assert `long = 1000 AND short = 1000` |
| T3 | **`A81e` is vacuous because of the clamp.** Step 2 applies `state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)`. On a base where `LongScore` is 0, removing the guard changes nothing | The fixture must first assert its base `LongScore ≥ cfg.Scoring.LiqLargePenalty`, or it passes with the guard removed |
| T4 | **`A82c` goes red, and the "fix" is to delete the two markers.** `A82c` requires `points:Liq Penalty` and `note:Liq Penalty:PENALTY -`. Parked, neither is ever reached | Deleting them leaves the park unguarded on the full `Calculate` path. **Replace them with a never-reached pin** (the existing OBV-partial pin is the template, `verify/ordercheck/Program.vb:18805`) |
| T5 | **Changing `r.LiqSignal` to a sentinel such as `"PARKED"`.** `UI/MainForm_Calibration.vb:102-104` counts every `LiqSignal <> "NONE"` as a liquidation event. `tools/CeilingAudit/CsvFeatureBuilder.vb:325` and `tools/BacktestRunner/OverlapValidator.vb:137` read it as a category | Every row would read as a liquidation event in the calibration report, and the CSV value domain changes (a schema-class event). **The park never touches `LiqSignal`, `LiqLongSize` or `LiqShortSize`** |
| T6 | **Display-string parity across FOUR sites, not two.** Snapshot: the `LIQUIDATIONS:` line (`UI/MainForm_PlaintextSnapshot.vb:517-518`) and the `Liq Penalty` row of `SIGNAL BREAKDOWN` (note built at `Core/ScoringEngine_Calculate_Scoring.vb:854`). Card: the `LIQUIDATIONS` group header (`UI/MainForm_Render_Cards.vb:2597`) and the `Liq` signal row note (`UI/MainForm_Render_Cards.vb:3353`) | No harness diffs any of them. Read all four from one constant (this spec §6) |
| T7 | **A guard that silently changes a rendered value.** If the park tag is added under `LP-3` (b), the card pill colour and SC column must stay as they are. SC reads breakdown points (`ScForItem`, `UI/MainForm_Render_Cards.vb:2989`), which are 0 when parked — correct. **Do not** recolour the pill or blank the sizes | Not fixture-reachable |
| T8 | **A new source file not linked everywhere.** `Core/LiquidationPark.vb` (this spec §5) must be added as `Compile Include` to every project that links `Core/ScoringEngine_Calculate_Scoring.vb` or `MarketState.vb`: `verify/ordercheck/OrderCheck.vbproj`, `verify/auditproofs/AuditProofs.vbproj`, `tools/BacktestRunner/BacktestRunner.vbproj`, `tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj` (the root project globs `Core/**`) | The break is loud, but only in a project you build. Build all four |
| T9 | **VB string comparison treats `Nothing` as `""`.** Today `Nothing <> "none"` is `True`, so a `TradeRecord` with an unset `Liquidation` books to the taker's side. Under `EF-3` it is unrecognised: skipped and counted | `A81d` must include a `Nothing` case and a `""` case |
| T10 | **Adding a property to `IndicatorResults` for the `EF-3` counter.** `A82a` fails until the mirror classifies every property, and a property invites a CSV column, which `EF-3` ruled out | Use the out-parameter of `LP-6` (a) |

### Escalation triggers — stop and move up, or stop and ask

- **A live row carries `LiqSignal` other than `NONE`.** Re-run handle `H-6` of [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1 on a fresh fetch. A non-`NONE` row since 2026-09-13 breaks the "no live output change" premise in this spec §4. **Stop and report.**
- **A fixture other than `A81b`, `A82c`, and (only under `LP-5` (b)) `A37a`–`A37d` needs an edit to pass.** The park or the fix then reaches something outside its scope.
- **The render needs a line ADDED or REMOVED** rather than an existing line re-formatted. `LP-3` (b) authorises re-formats only (the `EF-4` precedent in [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6).
- **Any need for a CSV column, a `settings.json` key, an `IndicatorResults` property or a `VerdictResult` property.** Each is a separate reserved decision.
- **The compiler warns on the constant guard** (`If Not LiquidationPark.VoteParked Then`). Not checked while writing. Do not suppress the warning — report it.

### Session plan — one session, sequenced by dependency

| Step | Scope | Commit | Gate before the next step |
|---|---|---|---|
| 1 | `D-5` + `EF-3` in `CalcLiquidations` (this spec §7). Flip `A81b`; add `A81c`, `A81d` | Commit 1 | Harness `ALL PASS`, 0 `SKIP`. Mutations M1–M4 (this spec §8) run red, then restored |
| 2 | `Core/LiquidationPark.vb`; Step 2 guard (this spec §5); `A81e`; `A82c` pin | Commit 2 | Mutation M5 red on `A81e` AND `A82c` |
| 3 | `#7` guard in `MarketState.FoldAlertsTrade`; `A37f`. If `LP-5` (b): the shared side seam and `A37g` | Commit 3 | Mutation M6 red on `A37f` |
| 4 | Render, per the `LP-3` ruling (this spec §6). Skip if ruled (a) | Commit 4 (parity rule: both surfaces in this one commit) | `H-7` run read by eye; card screenshot, then delete the PNG |
| 5 | `LP-7` tweaker fence; docs (this spec §10) | Commit 5 | `verify-gate` green |

⚠ **Rebase before step 1.** `verify/ordercheck/Program.vb` is about 19,000 lines and other seats append to it.

### What you are handed

This whole spec, plus the trader's rulings on `LP-1`–`LP-8`. **Do not build a decision the trader has not ruled.** Where a decision is auto-proceed class (this spec §9 says which), take the read and log it in one line.

---

## 1. Scope

### In scope

| Item | What ships | Class |
|---|---|---|
| `D-4` (a) — vote | A code constant and a guard around the Step 2 liquidation penalty | ⛔ Reserved — scoring code (live no-op, replay change; this spec §4) |
| `D-4` (a) — alarm | A code guard that stops liquidation-flagged trades entering the `#7` window | Not reserved by class — no live surface moves (this spec §4) |
| `D-5` (Rider 1) | `CalcLiquidations` books `M` to the maker's side, `T` to the taker's, `MT` to both | ⛔ Reserved — scoring code. Live no-op; replay change |
| `EF-3` | An unrecognised value is skipped and counted, and the count goes to a log line | Not reserved — log line only |
| `A81b` flip | Known-defect repro becomes an always-on guard. Closes the last user of `ORDERCHECK_KNOWN_DEFECTS` (`AT-L2` (c)) | Harness |
| Render (`LP-3`) | What the four liquidation display sites show once parked | ⛔ Reserved — rendered value |
| `LLS-1` (Rider 3, ticked 2026-10-02 by the trader) | Per-session `large_liq_size` (ASIA / LONDON / NY): a new settings shape read by the Step 2 penalty. Values re-derived at build time by `H-1` of [`large-liq-size-rederivation-2026-10-02.md`](large-liq-size-rederivation-2026-10-02.md) on the then-current store under the `D-5` booking (2026-10-02 values: ~69.5k / ~83.3k / ~49.7k USD) | ⛔ Reserved — settings shape + scoring code. Live no-op while parked; settings version bump |
| `D-6` manual | Correct the unit in [`UserManual.md`](UserManual.md) §13 to USD, including lines 1451–1452 | Documentation. Already ruled |

### Explicitly NOT in scope

- **Re-deriving `large_liq_size` or `dominance_ratio`.** Research on the history store (`HSR-4`). No `settings.json` edit. Settings stays at version 69.
- **Rider 2 — the A4 research study.** No code here. The study joins history-store liquidations to the logged per-run OFI values (`HSR-11`).
- **Any change to the stream parse** (`DeribitWsFeed.vb:499`) or the REST parse (`DeribitClient.vb:410-414`). They read the field correctly; the venue sends it ~60 min late.
- **Retiring the vote.** `docs/trader-profile.md` §3 lists Liquidations as PREFERRED. The park keeps every line of the vote and says why it is off.
- **`alerts.enabled = false`.** It would also stop `#8` (level approach). Rejected on mechanism (`LP-4`).

---

## 2. State verified while writing this spec

| Fact | Value | How it was checked |
|---|---|---|
| UTC now | 2026-10-01 14:48 | `date -u` |
| Base commit | `20f5373`, `master`, 1 ahead of `origin/master` | `git rev-parse --short HEAD`; `git status -sb` |
| Settings version | 69 | tracked repo-root `settings.json` line 2 |
| Harness, default run | **494 `PASS`, 1 `SKIP` (`A81b`), 0 `FAIL`, `ALL PASS`** | `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release`, full output counted with `grep -c` |
| Harness, `ORDERCHECK_KNOWN_DEFECTS=1` | `A81b` FAIL ×2 (`got signal=SHORT LIQS long=0 short=1000`, `got signal=LONG LIQS long=1000 short=0`), `2 FAILURE(S)` | same command with the variable set |
| `ORDERCHECK_KNOWN_DEFECTS` users | One fixture, `A81b`: 2 non-comment lines (`verify/ordercheck/Program.vb:18081` the gate, `:18082` its `SKIP` message). The other 3 hits are comments | `git grep -n`, comment lines filtered (handle `H-2`) |
| Local trade store flags | `backtest_data/trades_2026-07.csv` 3 `T` · `trades_2026-08.csv` 2 `T` · `trades_2026-09.csv` 59 `T` + **13 `M`**. Jan–Jun: 0 | `awk` over column 5 |
| A real `M` case | 2026-09-21 08:38 UTC: 48 `T` taker-buys (157,030 USD) and 13 `M` taker-sells (28,470 USD) | `awk` sum by direction and flag |

⭐ **The 08:38 cascade is the first real-data check of `D-5`.** An `M` flag on a taker sell means the maker bought, and the maker was liquidated: a SHORT was force-bought. So all 61 trades are short liquidations — one short squeeze. **Today the code books the 13 `M` trades as LONG liquidations** (`long = 28,470`). After `D-5`: `short = 185,500`, `long = 0`. The signal is `SHORT LIQS` either way; the sizes are not.

---

## 3. Who reads the liquidation flag today — the consumer map

| # | Site | What it does | Live effect today |
|---|---|---|---|
| 1 | `DeribitWsFeed.vb:499` | Stream parse. Defaults to `"none"` when the field is absent | Always `"none"` at first delivery (`L-1`; raw channel 0 of 3; history host 7 of 7 late) |
| 2 | `DeribitClient.vb:410-414` | REST parse, same default | Fresh trades are unflagged. A trade older than ~60 min may carry the flag (this spec §4.2) |
| 3 | `DeribitWsFeed.vb:329` → `MarketState.SeedTrades` | REST seed of 500 trades into the ring on every (re)connect | As row 2. **Seed trades do not reach the `#7` fold** |
| 4 | `UI/MainForm_Analysis.vb:453` | Live call of `CalcLiquidations` on the 500-trade window | `NONE`, 0, 0 on 51,107 of 51,107 rows from the v51 edge to the 2026-09-13 fetch |
| 5 | `tools/BacktestRunner/ReplayLoop.vb:494` | Replay call of `CalcLiquidations` on the store slice | **Fires** on windows with flagged store rows (this spec §2) |
| 6 | `Core/Indicators_OrderFlow.vb:259-282` | `CalcLiquidations`: books by taker direction (`L-2`) | — |
| 7 | `Core/ScoringEngine_Calculate_Scoring.vb:396-408` | Step 2 penalty — **the only scoring consumer** of `LiqSignal` and the sizes | No-op on `NONE` |
| 8 | `Core/ScoringEngine_Calculate_Scoring.vb:854-857` | Breakdown row `Liq Penalty`, note `L:{0:F0} S:{1:F0} \| {2}` | Renders `L:0 S:0 \| NONE` |
| 9 | `AnalysisLogger.vb:129`, `:356-358` | CSV columns `LiqLongSize`, `LiqShortSize`, `LiqSignal` | `0.00`, `0.00`, `NONE` |
| 10 | `UI/MainForm_PlaintextSnapshot.vb:514-519` | `LIQUIDATIONS:` section, one line | `Long: 0  \|  Short: 0  \|  Signal: NONE` |
| 11 | `UI/MainForm_Render_Cards.vb:2589-2601` | Card group `LIQUIDATIONS · <signal>` + two sizes | `LIQUIDATIONS · NONE` |
| 12 | `UI/MainForm_Render_Cards.vb:3331-3355` | Card signal row `Liq`, pill + note + SC | `NONE`, `none`, SC 0 |
| 13 | `DeribitWsFeed.vb:515-518` → `MarketState.FoldAlertsTrade` (`MarketState.vb:319-325`) → `AlertsTracker.FoldTrade` (`Core/AlertsTracker.vb:114-169`) | `#7` window, `FIRST_SEEN` and `CASCADE` events, sidecar `liq_events.log` | Nothing ever enters the window. ⚠ **The side is the taker's direction (`isBuy`) — the `L-2` shape again** (`Core/AlertsTracker.vb:194-210`, `:241-244`) |
| 14 | `UI/MainForm_LiveStrip.vb:129`, `:230`, `:240-244`, `:171-182` | Strip tag `LIQ↑ n× ($x)`, red flash, tooltip on sidecar existence | Tag never rendered |
| 15 | `UI/MainForm_Calibration.vb:102-104` | Counts `LiqSignal <> "NONE"` as a liquidation event (informational) | 0 |
| 16 | `Core/Settings/SettingsLoader.vb:117-120`, `:133`; `tools/AutoTweaker/PromptBuilder.vb:165` | Reason text: `alerts.*` "gates `liq_events.log`, the sole A4 gate instrument" | Text only. Goes stale with Rider 2 |

⭐ **Finding (new while writing): row 13 carries the `L-2` defect a second time.** The `#7` alarm names its side from the taker's direction: "buy-liquidations = SHORTS stopped out". An `M` flag on a taker buy is a LONG liquidation, so the alarm would name the wrong direction for every `M` trade. It is latent (the alarm has never seen a flag) and parked by this spec. `LP-5` asks whether to fix it now.

---

## 4. Does the park change live output? — the evidence

### 4.1 Scoring, CSV, card and snapshot — no change, on the measured span

- **The vote fires only on `LONG LIQS` or `SHORT LIQS`** (`Core/ScoringEngine_Calculate_Scoring.vb:399-407`). On `NONE` both penalties stay 0 and both scores are untouched.
- **No other scoring site reads `LiqSignal`, the sizes, `liqLongPenalty` or `liqShortPenalty`.** Checked with `git grep` over every `.vb` outside `verify/` and `tools/ops/SwingFallbackRead`: the only hits are lines 396–407 and 854–857 of that file.
- **`LiqSignal` is `NONE` on 51,107 of 51,107 rows** from the v51 edge to the 2026-09-13 fetch (handle `H-6` of [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1). Carried over, not re-run.
- **So the guard is a no-op on every measured live row.** Scores, verdict, levels, Kelly and the CSV are byte-identical.
- **`D-5` changes `CalcLiquidations` output only for flagged trades** (`M`, `MT`, unrecognised). Live sees none, so its CSV cells do not move.
- **The `#7` alarm sees only streamed trades** (`DeribitWsFeed.vb:515-518`; the seed path does not fold). Streamed trades are never flagged at first delivery. So the alarm guard changes nothing live, and the sidecar is not written either way.

### 4.2 The one live edge — REST-sourced trades older than ~60 min

- The ring is seeded from REST on every (re)connect (`DeribitWsFeed.vb:329`). A degraded feed or `transport = rest` fetches 500 REST trades per run (`UI/MainForm_Analysis.vb:104`).
- **A REST trade that is older than ~60 min at fetch time can carry the flag.** For one to sit in the latest 500, the market must print fewer than about 0.14 trades/s for an hour. The 24 h mean is ~2.6 trades/s ([`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §0000).
- **It has not happened in the measured span** (0 of 51,107). Rows after 2026-09-13 are not measured.
- **If it ever happens, the CSV row says so by itself:** `LiqSignal` is not `NONE`. That is why the park leaves `CalcLiquidations` running and logging (`LP-1`). **A non-`NONE` value in a live row while parked is the tripwire**, and it is the escalation trigger in this spec §0.

### 4.3 The replay path — this DOES change

- `tools/BacktestRunner/ReplayLoop.vb:494` calls `CalcLiquidations` on store slices, and `:628` scores with the shipped `ScoringEngine.Calculate`.
- **The local store holds 77 flagged rows (Jul–Sep 2026, 13 of them `M`).** Every replay window holding one has scored the Step 2 penalty — a vote the live engine has never cast.
- Under `LP-2` (a), the park reaches replay: those penalties go, and replay matches live. Under `D-5`, the sizes on those windows move (this spec §2's 08:38 example).
- **Replay output is offline.** It is not `analysis_log.csv`, and no live dataset moves.

### 4.4 Rendered surfaces — change only if `LP-3` says so

Under `LP-3` (a) nothing renders differently. Under `LP-3` (b) four sites gain a `PARKED` tag. Both are listed in this spec §6.

**Verdict: on the measured span, no live score, verdict, level or CSV cell changes. The replay path changes. The rendered surface changes only by the `LP-3` ruling.**

---

## 5. The park mechanism

### 5.1 Options and what each does

| Option | Scoring (live) | Replay | CSV | Snapshot / card | `#7` alarm, sidecar | Self-describing? |
|---|---|---|---|---|---|---|
| **(a) Code constant + guard** — `Core/LiquidationPark.vb` with `Public Const VoteParked As Boolean = True`, `Public Const CascadeParked As Boolean = True`, `Public Const Reason As String`; Step 2 wrapped in `If Not LiquidationPark.VoteParked Then … End If`; `#7` guard in `MarketState.FoldAlertsTrade` | Byte-identical (this spec §4.1) | Penalty removed (this spec §4.3) | Unchanged | Per `LP-3` | Never enters the window; sidecar never written | ✅ The guard and the reason sit at the vote. A reader of Step 2 learns the truth from the code |
| (b) Settings key, e.g. `scoring.liq_vote_enabled: false` | Byte-identical | Penalty removed | Unchanged | Per `LP-3` | Needs a second key | ⚠ Visible in `settings.json`, but **advertises a tunable for a vote that has no data source**. Reserved settings change, version bump, hot-reload edge mid-`InstanceId`, a new tweaker fence. **Not "a guard in code", which is what the ruling says** |
| (c) Delete the Step 2 block and the alarm fold | Byte-identical | Penalty removed | Unchanged | `Liq Penalty` row disappears from both breakdowns — a removed line | Gone | ❌ Nothing left says why. A later seat re-adds the PREFERRED signal and finds no record. Contradicts "never retire" in `docs/trader-profile.md` §3 |
| (d) Force `r.LiqSignal = "NONE"` (or a sentinel) at the live call site | Byte-identical | Unchanged (vote still fires in replay) | Sentinel changes the value domain (T5) | Sentinel renders | Not covered | ❌ Hides the tripwire of this spec §4.2. `MainForm` is not fixture-reachable |

**Read: (a).** `LP-1` and `LP-2` in this spec §9 carry the three-step test.

### 5.2 The shape of `Core/LiquidationPark.vb` (guidance, not code)

- Host-agnostic, no WinForms, no settings read. `Public Const` throughout (the `CLAUDE.md` rule: a value ruled into a constant goes `Public Const`, so fixtures read it).
- `VoteParked` — read by the Step 2 guard only.
- `CascadeParked` — read by `MarketState.FoldAlertsTrade` only. **Two constants, not one**: un-parking the vote and the alarm are separate future decisions with separate evidence.
- `Reason` — one sentence: no real-time source carries the `liquidation` flag (main host ~60 min late, raw channel 0 of 3 at first delivery, history host 7 of 7 ~59.5 min late); `D-4` (a), 2026-09-30; links to this spec.
- `DisplayTag` — the short render string, only under `LP-3` (b). Proposed: `PARKED`. ⚠ It must not contain `PENALTY -` (an `A82c` marker substring) or `LONG` / `SHORT` (the card's substring fallback at `UI/MainForm_Render_Cards.vb:3345-3348` would pill it).
- **An un-park checklist in the header comment:** a measured real-time source → flip the constant → restore the two `A82c` markers and invert the `A82c` pin → invert `A37f` → treat the edge as a scoring dataset boundary → re-derive `large_liq_size` first (`D-6`).

### 5.3 The Step 2 guard

- Wrap lines 398–408 of `Core/ScoringEngine_Calculate_Scoring.vb` (from `pL = state.LongScore …` through `liqLP += …`). Keep `liqLongPenalty` / `liqShortPenalty` declared outside, so the breakdown row at line 857 still builds with zero points.
- **Keep the `Liq Penalty` breakdown row.** Removing it removes a line from the snapshot's `SIGNAL BREAKDOWN` and changes `ScForItem`'s lookup (T6).
- **Keep `CalcLiquidations` running live and in replay.** The CSV keeps recording what the window held; the research studies (`HSR-4`, A4) need the sizes.

### 5.4 The `#7` guard

- In `MarketState.FoldAlertsTrade` (`MarketState.vb:319-325`): pass `isLiq AndAlso Not LiquidationPark.CascadeParked` to the tracker.
- **`AlertsTracker` is untouched under `LP-5` (a).** `A37a`–`A37d` construct the tracker directly, so its cascade math stays exercised. The one production owner (`MarketState.vb:69`) is guarded.
- `#8` level approach is unaffected: the same fold still updates `_lastPrice` and the approach episodes.
- The tooltip (`UI/MainForm_LiveStrip.vb:171-182`) reads the sidecar file's existence. The park never writes the sidecar, so the tooltip cannot appear from this build. ⚠ **Whether `liq_events.log` already exists on the collector box was not checked.**

---

## 6. Rendered surfaces — the four sites (`LP-3`)

| Site | File:line | Today | `LP-3` (b) proposal |
|---|---|---|---|
| S1 snapshot `LIQUIDATIONS:` | `UI/MainForm_PlaintextSnapshot.vb:517-518` | `  Long: 0  \|  Short: 0  \|  Signal: NONE` | `  Long: 0  \|  Short: 0  \|  Signal: NONE  \|  PARKED (no real-time flag)` |
| S2 snapshot `SIGNAL BREAKDOWN`, row `Liq Penalty` | note built at `Core/ScoringEngine_Calculate_Scoring.vb:854` | `L:0 S:0 \| NONE` | `L:0 S:0 \| NONE \| PARKED` |
| S3 card group header | `UI/MainForm_Render_Cards.vb:2597` | `LIQUIDATIONS  ·  NONE` | `LIQUIDATIONS  ·  NONE  ·  PARKED` |
| S4 card signal row `Liq` note | `UI/MainForm_Render_Cards.vb:3353` | `none` | `none · parked` |

- **All four are re-formats of an existing line. No line is added or removed.** That is the `EF-4` (a) precedent ([`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6).
- **All four read `LiquidationPark.DisplayTag` and `LiquidationPark.VoteParked`.** A literal copied into four files rots on the first rename.
- S2 is built in `Core`, so it is fixture-reachable: `A81e` asserts the tag in the note. S1, S3 and S4 are not.
- **Pill colour and SC column are unchanged** (T7). The values stay visible: they are the tripwire in this spec §4.2.
- **The parity rule is met by putting S1–S4 in ONE commit** (`CLAUDE.md`, display-string parity rule).

---

## 7. The `L-2` fix (`D-5`) and `EF-3`

### 7.1 Required booking

`direction` is the TAKER's direction. Deribit's `liquidation`: `"T"` the taker was liquidated, `"M"` the maker was, `"MT"` both.

| Flag | Taker direction | Liquidated position | Books to |
|---|---|---|---|
| `T` | buy | SHORT, force-bought | `liqShortSize` |
| `T` | sell | LONG, force-sold | `liqLongSize` |
| `M` | buy | LONG, force-sold into the taker's buy | `liqLongSize` |
| `M` | sell | SHORT, force-bought from the taker's sell | `liqShortSize` |
| `MT` | either | both sides | **both** sizes, the full amount each |
| `none` | either | — | nothing, not counted |
| anything else, incl. `Nothing` and `""` | either | unknown | **nothing; count +1** (`EF-3`) |

### 7.2 Shape

- **An explicit `Select Case` on the flag.** Never the one-line boolean (T1).
- ⚠ **Direction is also a string.** A `T` or `M` trade whose direction is neither `buy` nor `sell` is unknown too: skip and count. The live parse never produces one; the guard costs one `Case Else`.
- **Signature:** add a required `ByRef unrecognisedCount As Integer` (`LP-6` (a)). Required, not `Optional` — the `A54a` ruling removed method defaults from this function on 2026-09-05 (`Core/Indicators_OrderFlow.vb:256-258`).
- **Both production call sites change:** `UI/MainForm_Analysis.vb:453` and `tools/BacktestRunner/ReplayLoop.vb:494`.
  - Live: when the count is above 0, write one line through `WsFeedLog.Write` (`Core/WsFeedLog.vb:69`): `liquidation: <n> trade(s) with an unrecognised liquidation flag skipped`.
  - Replay: add the count to the replay's run summary on the console.
- **`MT` books the full amount to both sides.** `D-5` says "both"; it does not say "split". Splitting would invent a rule. ⚠ `MT` has zero occurrences in any store, so this is untested against the venue.
- **Under `LP-5` (b) only:** put the booking table in one shared function, e.g. `IndicatorEngine.LiquidatedSides(flag, direction) As (LongLiq As Boolean, ShortLiq As Boolean, Recognised As Boolean)`. `CalcLiquidations` and the `#7` fold both call it, so they cannot disagree.

---

## 8. Fixtures

**Provenance (`CLAUDE.md` fixture-literal rule), for every fixture below:** `dominance_ratio`, `large_liq_size`, `liq_standard_penalty` and `liq_large_penalty` are **SHIPPED BEHAVIOUR** — read from the tracked `settings.json` through `A80ShippedCfg`, never typed. The 1,000 USD amounts and timestamps are **MECHANISM** — any positive amount makes one trade the whole window.

| ID | Input | Assertion | The input that makes it fail | Mutation that must turn it red |
|---|---|---|---|---|
| `A81a` | unchanged (`T` buy, `T` sell) | unchanged | — | M1 below also turns it red if the `T` arm is broken |
| **`A81b`** (flipped) | `M` buy; `M` sell; 1,000 each | `M` buy → `LONG LIQS`, long 1,000, short 0. `M` sell → `SHORT LIQS`, short 1,000, long 0. **No env gate; no "KNOWN DEFECT" text** | A single `M` trade — the defect inverts it | **M1:** book `M` like `T` (the shipped code) → both cases red |
| **`A81c`** (new) | (i) one `MT` buy, 1,000 · (ii) one `MT` sell, 1,000 · (iii) `MT` buy 1,000 + `T` buy 3,000 | (i), (ii): long = 1,000 **AND** short = 1,000, signal `NONE`. (iii): short = 4,000, long = 1,000, `SHORT LIQS` | **Sizes, not signal** (T2): a skipped `MT` also gives `NONE` | **M2:** the one-line boolean trick → (i) red (short only). **M3:** `MT` treated as unrecognised → (i), (iii) red |
| **`A81d`** (new) | one trade each flagged `"X"`, `"t"`, `""`, `Nothing`; plus a control list of the same trades flagged `"none"` | Each unrecognised list gives long, short and signal **identical to the control**, and count = 1 per trade. The control gives count = 0 | The `Nothing` and `""` cases — today they book to the taker's side (T9) | **M4:** `Case Else` books by taker direction (today's behaviour) → red. **M4b:** `Case Else` does not increment → red. **M4c:** `"none"` counted → the control is red |
| **`A81e`** (new) | `r` from the `A82b` site "Liquidations large" (`LiqSignal = LONG LIQS`, `LiqLongSize = LargeLiqSize × 2`) on a long-voting base. **Precondition check:** base `LongScore ≥ cfg.Scoring.LiqLargePenalty` (T3) | `Calculate(r)` equals `Calculate(r with LiqSignal NONE, sizes 0)` in `LongScore`, `ShortScore`, `Verdict`. The `Liq Penalty` row has `LongPoints = 0`, `ShortPoints = 0`, `LongHit = False`. Under `LP-3` (b): the note contains `LiquidationPark.DisplayTag` | A base with `LongScore = 0` makes it vacuous — hence the precondition | **M5:** remove the guard (or set `VoteParked = False`) → red |
| **`A82c`** (edited) | the existing `A82b` runs | Remove `points:Liq Penalty` and `note:Liq Penalty:PENALTY -` from the required list. **Add:** `A82c the liquidation vote never fires across the same runs (parked, D-4 2026-09-30)` = `Not seen.Contains("points:Liq Penalty") AndAlso Not seen.Contains("note:Liq Penalty:PENALTY -")` | The `A82b` sites "Liquidations standard" and "Liquidations large" already drive `LONG LIQS` through the full pipeline | **M5** → red (the vote is reached again) |
| **`A37f`** (new) | `New MarketState()`; alerts cfg enabled; delete the sidecar first (the `A37a` precedent); fold 3 liquidation-flagged trades inside 10 s through `MarketState.FoldAlertsTrade`; then `GetAlerts` | `CascadeSignal = NONE`, `CascadeCount = 0`, no pending `FIRST_SEEN` or `CASCADE`, and **the sidecar file does not exist**. Control: `LastPrice` equals the last folded price, so `#8` still folds | 3 flagged trades in the window — the `A37a` shape that fires `CASCADE_ABOVE` on an unguarded tracker | **M6:** remove the `MarketState` guard → red |
| **`A37g`** (new, only under `LP-5` (b)) | `New AlertsTracker()` (unparked); 3 `M`-flagged taker buys in 10 s | `CASCADE_BELOW` (longs liquidated) | Taker-direction booking reads them as `CASCADE_ABOVE` | **M7:** the fold keeps `isBuy` as the side → red |

- **Dispatcher:** add the new calls beside `A81a`/`A81b` (`verify/ordercheck/Program.vb:862-863`) and beside `A37e`. Rewrite the dispatcher comment at lines 858–861 the way the `A80b` comment at lines 17539–17544 was rewritten.
- **Expected harness result:** `ALL PASS`, **0 `SKIP`**, 0 `FAIL`. The base was 494 `PASS` + 1 `SKIP`. The new total is 494 + the number of `Check` lines added; state that number in the spec-back from the actual run, not from this table.
- ⚠ **Every mutation is run once, red output pasted, then restored by the inverse edit** — never by `git checkout --` over uncommitted work (memory rule "restore only the mutation").

---

## 9. Decisions

> ✅ **RULED 2026-10-01 (UTC), trader: `LP-1`–`LP-8` all AS READ.** **Build timing: after 2026-11-25**, as a fix off the burst path under `AT-6` (b). It changes no live output, so it gains nothing during the holiday; the history-store build has the pre-holiday agent slot. One agent, ask the trader before dispatch.

**Reserved marks:** ⛔ = reserved under `CLAUDE.md` (scoring code, rendered value, settings). Not marked = auto-proceed class; the implementer may take the read and log it.

**The three-step test** (`CLAUDE.md`, "what cheaper means"): (1) is there an option that records more, guarantees more, or is more self-describing than the read? (2) if yes and it is not picked because "mine is adequate" → reserve; (3) if it is not picked because it is mechanically wrong, uninterpretable or forbidden → take the read and name which.

### `LP-1` — the park mechanism for the Step 2 vote ⛔ reserved (scoring code)

| Option | Effect |
|---|---|
| **(a)** `Public Const` in `Core/LiquidationPark.vb` + guard around Step 2 | this spec §5.1 row (a) |
| (b) settings key | this spec §5.1 row (b) |
| (c) delete the block | this spec §5.1 row (c) |

- **Read: (a).** It is the ruling's own wording ("an explicit guard in code that says why").
- **Three-step:** Step 1 — (b) is also self-describing, in `settings.json`. But it states something false: that the vote is a tunable a trader may switch on. No data source exists to switch it on against. (c) records less. **Step 3 for (b): forbidden by the ruling's wording ("in code") and it misdescribes the state. Take (a).**

### `LP-2` — where the Step 2 guard sits; does the park reach replay? ⛔ reserved (scoring; replay outputs move)

| Option | Effect |
|---|---|
| **(a)** inside `ScoringEngine` Step 2 | Live, replay and harness all parked. Fixture-reachable (`A81e`, `A82c`). Replay outputs move on flagged windows (this spec §4.3) |
| (b) at the live call site `UI/MainForm_Analysis.vb:453` | Live parked; replay keeps scoring the vote. Not fixture-reachable |

- **Read: (a).**
- **Three-step:** Step 1 — does (b) record or guarantee more? It keeps a replay penalty, but that penalty is a vote the live engine cannot cast, so every replay silently diverges from live on flagged windows. (a) guarantees more (harness-proved) and is more truthful. **No richer option. Take (a).**
- ⚠ **Name the consequence to the trader:** any replay result from before this build, over a window holding a flagged trade, scored a penalty the live engine never applied.

### `LP-3` — what the four render sites show once parked ⛔ reserved (rendered value)

| Option | Effect |
|---|---|
| (a) no change | `Signal: NONE` everywhere |
| **(b)** re-format the four existing sites with a `PARKED` tag from one constant (this spec §6) | Values still shown; no line added or removed |
| (c) replace the values with `PARKED` / `—` | Sizes hidden |

- **Read: (b).**
- **Three-step:** Step 1 — (b) records the most: the values AND why they mean nothing. (a) reads "no liquidations" when the truth is "flag unavailable in time" — the silent-hole class this repo rejects. (c) hides the sizes, which are the tripwire in this spec §4.2. **No richer option. Take (b).**
- ⚠ **(a) is the cheaper option, and choosing it would be the "adequate" tell.** If the trader rules (a), the build skips step 4 of this spec §0 and `A81e` drops its tag assertion.

### `LP-4` — where the `#7` alarm guard sits (not reserved: no live surface moves)

| Option | Effect |
|---|---|
| **(a)** `MarketState.FoldAlertsTrade` | Production path guarded; `AlertsTracker` math still exercised by `A37a`–`A37d`; `A37f` reaches it |
| (b) `AlertsTracker.FoldTrade` | Also guards, but `A37a`, `A37c`, `A37d` go red and the cascade math becomes untested code |
| (c) `DeribitWsFeed.vb:515-518` | Not linked into the harness; unguarded by any fixture |
| (d) `alerts.enabled = false` | Settings change; also stops `#8` |

- **Read: (a).**
- **Three-step:** Step 1 — (b) would guard every owner of `AlertsTracker`, but there is exactly one (`MarketState.vb:69`), and (b) stops testing the math the un-park depends on. (c) guarantees less. (d) is **mechanically wrong** (it parks `#8`, which the ruling does not touch). **Step 3: take (a), naming (d) as mechanically wrong.**

### `LP-5` — move the `#7` side attribution onto the `D-5` rule now? (not reserved by class; queued because it extends Rider 1's scope)

| Option | Effect |
|---|---|
| (a) leave the alarm on taker direction; record it in the guard comment and the un-park checklist | No code in `AlertsTracker` |
| **(b)** one shared side function (this spec §7.2) used by `CalcLiquidations` AND the `#7` fold | `FoldTrade` takes the flag; `A37a`–`A37d` call sites update; `A37g` added |

- **Read: (b).**
- **Three-step:** Step 1 — (b) guarantees more: one seam, so two consumers of the same flag cannot disagree, and an un-park is correct by construction. Not picking it would rest on "parked, so (a) is adequate" — **the tell word.** No richer option than (b). Take (b).
- **Why it is queued and not auto-proceeded:** the ruling's Rider 1 names `CalcLiquidations` only. Widening a trader ruling's scope is the trader's call.

### `LP-6` — `EF-3` counter plumbing (not reserved: a log line)

| Option | Effect |
|---|---|
| **(a)** required `ByRef unrecognisedCount`; live writes one `WsFeedLog` line when > 0; replay prints it | No schema, no new property |
| (b) a new `IndicatorResults` property | Trips `A82a`; invites a CSV column |
| (c) a `Shared` counter in `IndicatorEngine` | State in the indicator layer |

- **Read: (a).** Sub-decision: `Nothing` and `""` are unrecognised (counted), not `"none"`.
- **Three-step:** Step 1 — (b) records more, but only by adding what `EF-3` ruled out ("a log line now, not a CSV column"). (c) is **forbidden** by the layer invariant (`CLAUDE.md` layer table: Indicators are "pure functions; no state"). **Step 3: take (a), naming the prior ruling and the invariant.** On the sub-decision, counting `Nothing` records more than mapping it to `"none"`. Take it.
- ⚠ **`WsFeedLog` is the feed's narrative log.** A flag value is feed content, so it fits; but the live call site is `MainForm_Analysis`, not the feed. If the trader prefers a dedicated sidecar, that is a new file and a new name — not recommended for an expected-zero event.

### `LP-7` — fence the keys the park makes dead (not reserved: offline tool, no settings change)

Keys: `scoring.liq_standard_penalty`, `scoring.liq_large_penalty`, `indicators.Liquidations.large_liq_size`, `indicators.Liquidations.dominance_ratio`.

| Option | Effect |
|---|---|
| **(a)** exact-match reject in `tools/AutoTweaker/SettingsDiffApplier.vb` + a `PromptBuilder` hard-constraint rule, the reason naming the park | A tweaker proposal on a dead key is refused, with the reason in the log |
| (b) leave them proposable | A proposal passes validation, burns a settings version and changes nothing |

- **Read: (a).** Use the next free hard-constraint number (the highest in the tree on 2026-10-01 is 28). Include a sibling-key pass check, the `A57d` / `A37e` precedent.
- **Three-step:** Step 1 — (a) records and guarantees more. Take (a).
- **Same commit:** rewrite the reason text in `Core/Settings/SettingsLoader.vb:117-120` and `:133`, and `tools/AutoTweaker/PromptBuilder.vb:165`. `alerts.*` stays rejected (it still carries `#8`), but "the sole A4 gate instrument" is no longer true once A4 moves to the collector-era study (Rider 2).

### `LP-8` — does this build add an era stratum to burst run 2 under `AT-6` (b)? (orchestrator / trader)

| Option | Effect |
|---|---|
| **(a)** no stratum | — |
| (b) add an era stratum and a pre-edge sensitivity run, as for any scoring fix | One more split in run 2's population |

- **Read: (a)**, conditional on handle `H-6` (this spec §11) after deploy.
- **Three-step:** Step 1 — (b) looks like it records more. It does not: a stratum on an edge where no score and no CSV cell can differ splits the population on a non-event, and the sensitivity run compares two identical sets. **Step 3: (b) is uninterpretable on this build.** And the one way the park could ever bite (this spec §4.2) marks its own row (`LiqSignal` not `NONE`), so the evidence is per row, not per era. Take (a).
- ⚠ **The rendered change under `LP-3` (b) is visible on deploy, but it is not a dataset edge.**

---

## 10. Documents the build must update

| Document | Change |
|---|---|
| [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 | One row: the park (live no-op; replay change), `D-5`, `EF-3`, the render (per `LP-3`). No settings version bump: no key changes |
| [`UserManual.md`](UserManual.md) §13 (lines 1438–1479) | Say the vote is PARKED and why. `D-6`: `200 BTC` → USD at lines 1467 and 1478, and delete the "~$15M … genuine cascade" claim; lines 1451–1452 "total BTC size" → USD notional. Replace the taker-only booking text at line 1457 with this spec §7.1 table |
| [`UserManual.md`](UserManual.md) line 1863 | Reads "Liquidation events ≥ 2 (gate …)". The code says it is not a gate (`UI/MainForm_Calibration.vb:194-198`). Found while writing; documentation, auto-proceed class |
| `docs/trader-profile.md` §3 | Liquidations stays PREFERRED, with one line: PARKED 2026-09-30 (`D-4`), no real-time source, this spec. ⚠ The profile is trader-ruled; the line is a status note, not a preference change |
| [`trader-tick-queue.md`](trader-tick-queue.md) §1 row `E7`, §2 `D-4` row | Mark built; the A4 sidecar gate (`liq_events.log` ≥ 1 `CASCADE`) is retired by the park — A4 is the `HSR-11` study |
| [`liq-cascade-level-alerts-proposal.md`](liq-cascade-level-alerts-proposal.md) | A status line at the top: `#7` parked, `#8` live |
| [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §7 `AT-L2` | Row C20 closed: the hatch has no users |
| [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4 | Already points here (added with this spec) |

---

## 11. Verification handles

`H-n` = the reader can run it after the build. `E-n` = build-time evidence the reader cannot re-run. **Pin every run to the build's base commit, not to `HEAD`.** The base-state halves below were run on 2026-10-01 at `20f5373`; the after-build halves are acceptance criteria and cannot run yet.

**If you only run one, run `H-1`.** Commands sit in code blocks, not table cells, so a copied pipe stays a pipe.

**H-1 — the harness** (Git Bash, repo root)

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release > h1.txt
grep -c '^SKIP' h1.txt; grep -c '^FAIL' h1.txt
grep -E '^PASS  (A81[a-e]|A37[fg]|A82c)' h1.txt | cut -c1-90; tail -1 h1.txt
```

- **Expected after the build:** `0`, `0`, the `A81a`–`A81e` and `A37f` lines (and `A37g` under `LP-5` (b)), three `A82c` lines including the new never-fires pin, `ALL PASS`.
- **Base output, run 2026-10-01 at `20f5373`:**

```
1
0
PASS  A81a taker-side liquidation (flag T, taker buy) books the TAKER's position → SHORT
PASS  A81a taker-side liquidation (flag T, taker sell) books the TAKER's position → LONG
PASS  A82c the A82b runs reached every vote and mutation site, every tier in every regime
PASS  A82c the OBV partial never upgrades across the same runs (v0.42 adverse-divergence b
ALL PASS
```

**H-2 — the known-defect hatch is closed**

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release | grep -c '^FAIL'
git grep -n ORDERCHECK_KNOWN_DEFECTS -- '*.vb' | grep -v -E "^[^:]+:[0-9]+:\s*'"
```

- **Expected after the build:** `0`, and no output from the second line.
- **Base, run 2026-10-01:** the first line printed the two `A81b` `FAIL` lines and `2 FAILURE(S)` (§2 of this spec). The second printed `verify/ordercheck/Program.vb:18081` (the gate) and `:18082` (its `SKIP` message).

**H-3 — exactly two guard sites**

```
git grep -n -E "Not LiquidationPark\.(VoteParked|CascadeParked)" -- Core MarketState.vb
```

- **Expected after the build:** two lines — one in `Core/ScoringEngine_Calculate_Scoring.vb` (`VoteParked`), one in `MarketState.vb` (`CascadeParked`). Render sites read the constant without `Not` and do not match.
- **Base, run 2026-10-01:** no output, exit 1 (the class does not exist).

**H-4 — mutations M1–M7 (this spec §8)**

- Reader-runnable: apply the one edit, run `H-1`, restore by the inverse edit.
- **The implementer publishes the exact edit and the red output for each in the spec-back.** This spec cannot: the post-build lines do not exist yet.
- Base: M1 is today's code, and `H-2`'s first line is its red output.

**H-5 — replay, before vs after**

- Build `tools/BacktestRunner` at the base (in a `git worktree`) and at the build.
- Run `replay --from 2026-09-21 --to 2026-09-22 --out <x>.csv` on each. Run the same for one June day (no flagged trades in `backtest_data/trades_2026-06.csv`). `diff` the CSV pairs.
- **Expected:** June — empty diff. 2026-09-21 — diffs only in rows whose window holds a flagged trade: `LiqLongSize` / `LiqShortSize` (the 08:38 `M` trades move from long to short) and the score cells the removed penalty touched.
- **Base: not run.** Replay runtime per day was not measured.

**H-6 — live no-op after deploy (trader, read-only)**

- Re-run handle `H-6` of [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1 (`tools/ops/SwingFallbackRead --mode liqflag`) on a fresh fetch. Read the rows since the deploy.
- **Expected:** `LiqSignal = NONE` and both sizes 0 on every row.
- **Base:** 51,107 of 51,107 `NONE` to the 2026-09-13 fetch. Carried over, not re-run.

**H-7 — render (`LP-3` (b) only)**

```
grep -n -A1 "^LIQUIDATIONS:" bin/Debug/net8.0-windows/analysis_output_dump.md | tail -2
grep -n "Liq Penalty" bin/Debug/net8.0-windows/analysis_output_dump.md | tail -1
```

- **Expected after one dev-machine run of the build:** the S1 and S2 strings of this spec §6.
- **Base, run 2026-10-01 on the existing dev dump** (the last line of each command):

```
345427-  Long: 0  |  Short: 0  |  Signal: NONE
345456:  Liq Penalty                        L:0 S:0 | NONE
```

**E-1 — the card (S3, S4).** A screenshot of the running app. Evidence only; delete the PNG afterwards. Expected: the §6 strings of this spec, with pill colour and SC unchanged.

---

## 12. Timing and live effect

- **Build after 2026-11-25 by default**, as the ruling says. The trader is away 2026-10-14 → 2026-11-25; no builds or deploys in that window.
- **It is off the burst path.** The build touches `CalcLiquidations`, the Step 2 liquidation block, `MarketState.FoldAlertsTrade`, `AlertsTracker` (only under `LP-5` (b)) and four render sites. None of them is `CalcTFI`, the aggressor-velocity fold (`MarketState.FoldAggressorVelocity`) or the burst threshold. Checked by reading each site. So `AT-6` (b) allows it in burst run 2's window.
- **Live output change: none on scores, verdicts, levels or CSV cells**, on the evidence of this spec §4.1 (0 of 51,107 non-`NONE` rows; the guard is a no-op on `NONE`; the alarm only folds unflagged streamed trades). **Two qualifications:** (1) rows after 2026-09-13 are not measured; (2) the REST edge in this spec §4.2 is possible in principle and has not occurred.
- **Rendered change: only if `LP-3` is ruled (b) or (c).**
- **Replay change: yes** (this spec §4.3). Offline only.
- **Era stratum:** none, per `LP-8` (a), confirmed by `H-6` after deploy.

---

## 13. What I did not verify

| Claim | Status |
|---|---|
| `LiqSignal` is `NONE` on live rows **after** 2026-09-13 | Not measured. Carried from `H-6` of [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1 to that date |
| A 500-trade REST window never spans > 60 min | Not measured. The trade-rate minimum per hour was not read |
| Whether `liq_events.log` exists on the collector box | Not checked |
| Whether `MT` arrives as the literal string `"MT"` | From the Deribit field description quoted in `verify/ordercheck/Program.vb:18037-18039`; no `MT` occurrence in any store |
| Whether `M` flags arrive with the same ~60 min delay as `T` | Not measured: all 7 history-host flags were `T` ([`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §00000) |
| That VB treats `Nothing <> "none"` as `True` (T9) | From VB string-comparison semantics; **not run**. `A81d` will settle it |
| Whether the compiler warns on `If Not <Public Const True>` | Not built |
| Replay runtime for one day, and that `backtest_data/` holds June candles for the `H-5` control window | Not run |
| That `A82b`'s mirror ignores breakdown notes, so a `PARKED` tag cannot break symmetry | Read in code (`verify/ordercheck/Program.vb:18823-18832` compares labels, points and hits only); not run with a tag |
| The local store counts in this spec §2 | Counted with `awk` on column 5 of the copy-back store in `backtest_data/`; the box's own store was not read. The 2026-07/08 files may mix the 5- and 7-column headers; the counts for those two months were not cross-checked |
| Every `docs/` line number in this spec §10 | Read on 2026-10-01; they move with edits |

---

## 14. Auto-proceed log (decisions taken while writing; one line each)

- **New doc, not a rewrite of [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4** — task-directed; the old engine-fix spec §4 keeps the history and gains a pointer.
- **Decision IDs `LP-1`–`LP-8` and fixture IDs `A81c`–`A81e`, `A37f`, `A37g`** — each checked free with `git grep -n -w`; 0 hits.
- **`D-6` manual fix and the line-1863 correction folded into the build's doc step** — both documentation; `D-6` is already ruled.
- **Two park constants, not one** (vote and alarm) — records more: the two un-parks are separate decisions on separate evidence.
- **`MT` books the full amount to each side, not a split** — the ruling says "both"; a split would invent a rule (step 3: forbidden by the ruling's text).
- **Baseline harness run included** — so the build's count is checked against a measured base, not a quoted one.
