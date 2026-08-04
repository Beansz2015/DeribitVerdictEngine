# Trade-Store Coverage Report — Session 1 Spec-Back

**Date:** 2026-08-04 · **Settings:** v65 → **v65 (unchanged)** · **Scope:** D7's capture-scope
marker + the `coverage` verb (Part A only). **No settings keys added ⇒ no version bump**
(the brief's own rule: keys-changed, not app-touched); **NOT a dataset boundary** — nothing
on the scoring path, `analysis_log.csv` untouched.

**Spec of record:** [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md)
(BUILD-AUTHORIZED 2026-08-03, all seven D's ticked). **Implementer brief:**
[`trade-store-coverage-report-implementer-brief.md`](trade-store-coverage-report-implementer-brief.md).
**Rulings binding this build:** [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md)
+ [`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md).

**This is Session 1 of 2** (the brief's own split, §0): D7 marker + Part A verb + S0–S4 +
six-class logic + fixtures A49a–l. **Session 2** (not started) is Part B (the live
`TAPE STORE` status element) + fixture A49m.

**Commit (local, unpushed — trader tests before Session 2):** `dc2c0bd` — the whole build,
one commit. Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner +
OrderCheck build **0/0** Release; harness **ALL PASS** (A1–A52a unregressed + A49a–l);
`verify-gate.ps1 -Mode prepush` **GATE PASSED** (one expected, non-blocking WARN — see §2.9).

---

## 0. Ranked verification handles, for the reviewing seat

Highest-value checks first — everything below is a judgment call the spec/rulings left open,
each with my own read stated. None of these are hedges; I built to a specific interpretation
in every case and would defend it, but the brief itself flagged two of these (§2.1, §2.2) as
the traps most likely to survive a fast read, so they get first billing.

1. **§2.1 — `expected-missing` scope.** Fires ONLY for hours strictly before the very first
   evidenced process life. Every other "no uptime evidence" shape defaults to `defect`. Read
   this section before anything else — it's the one place the spec's own wording
   ("app-down hours are reported expected-missing") could be read more broadly than what I
   built, and I built the narrow reading deliberately.
2. **§2.2 — S1-skipped condition.** Skipped only when BOTH `ws_health.log` is absent AND
   `analysis_log.csv` has zero rows in range — not merely when the log file alone is missing.
   The fixture text (A49g) only names `ws_health.log`; I read it against the CSV's own
   documented primacy and built the compound condition.
3. **§2.6 — the real bug A49i caught.** Worth reading even though it's fixed, because it's
   the shape of error most likely to recur if this code is ever touched again: an
   hour-instant containment check instead of a window-overlap check. If anyone later adds a
   third evidence source, check it against A49i's exact scenario.
4. **§2.3 — the trailing-window boundary.** Bounded by the newest evidence found in the
   input files, not by `DateTime.UtcNow`. This is my mechanical implementation of "bounded
   by copy time, not now" absent a `--copy-time` CLI flag (none exists in the spec's CLI
   signature) — worth confirming this reading is what was intended before it's relied on.
5. **§2.8 — A49h tests the CLI's decision predicate, not the literal exit code.**
   `BacktestProgram.vb` stays out of the fixture harness, same as every other verb — so the
   `--strict` exit-code claim is reasoned from the predicate, not process-launched.

---

## 1. What shipped (map to the brief's §3 build shape)

| Brief item | Shipped |
|---|---|
| §3 D7 marker — one line per process, resolved `enabled` + `store_dir`, `WsHealthLog`/`AlertsSidecar` contract | ✅ New [`Core/CaptureMarkerLog.vb`](../Core/CaptureMarkerLog.vb) — `utc \| enabled \| store_dir \| instance_id`, exe-relative `capture_marker.log`, append-only, never throws. Wired into [`UI/MainForm_Layout.vb`](../UI/MainForm_Layout.vb) immediately beside the existing `WsHealthLog.LogStart` call, reading `SettingsLoader.Current.TradeStore` (the MERGED, overlay-aware value — see §2.4) and `TradeStoreWriter.ResolveStoreDir` (the resolved, absolute, exe-anchored path, not the raw configured string). |
| §3 Part A `coverage` verb, tools-only, no settings keys | ✅ New [`tools/BacktestRunner/CoverageReport.vb`](../tools/BacktestRunner/CoverageReport.vb) (the classification core) + a `Case "coverage"` dispatch in [`BacktestProgram.vb`](../tools/BacktestRunner/BacktestProgram.vb) with `--gap-ms` / `--out` / `--strict` / `--verify-venue`. Read-only; never fetches except the optional S0 path, never writes to the store. |
| §3 S1 primary = `analysis_log.csv`, `ws_health.log` supplements | ✅ `ParseAnalysisLogEvidence` (column indices resolved by header NAME, not position) + `ParseWsHealthEvidence` (state value deliberately ignored — every line is "alive" evidence, §2.5) merge into one evidence pool before `BuildUpIntervals` runs — so the CSV's primacy is structural, not a fallback branch. |
| §3 "`DOWN` proves the app was alive" trap | ✅ Verified by hand, §2.5 below. `ParseWsHealthEvidence` never reads the state column at all. |
| §3 Trailing-interval bounded by copy time, not now | ✅ `ResolveBoundaryUtc` — bounded by the newest evidence found across the input files, capped at `--to`. See the verification-handle note (§0.4) on why this is a mechanical reading of "copy time" absent a CLI flag for it. |
| §2.3 The six classes, collapsed into none | ✅ `HourClass` enum: `Captured` / `Defect` / `ExpectedMissing` / `NotCapturing` / `UnknownScope` / `OutOfScopeWeekend`. `ClassifyHour` resolves scope (marker) → uptime (S1) → store (S2/S3) in that order, never short-circuiting a class into another. |
| §2.1 Scope from a positive record, never current config or a baseline | ✅ Verified by hand, §2.4 below. `ResolveScope` reads ONLY the parsed marker file; nothing in the classification path reads `SettingsLoader.Current` at all. |
| §2.2 Weekday-only evaluation, D4 confirmed at 300,000 ms | ✅ `ClassifyHour` checks `DayOfWeek` first, unconditionally, before scope/uptime/store. `CoverageOptions.GapMs` defaults to `300000L`. |
| §10 Reuse the shared seams, no second parse | ✅ S4 reuses `StoreFiles.LoadCandleFile/LoadFundingFile` + `ExpectedGridPoints`/`CountCandlesInRange`/`CountFundingInRange`; S0 reuses `HistoricalStore.FetchTradesByTimeAsync`; the trade walk streams month-by-month via `TradeStoreWriter.ReadTradeFile` (never `HistoricalStore.LoadTradeRange`, which materialises + dedups the whole multi-month range — the wrong tool per §10). |
| §5 Fixtures A49a–l | ✅ All twelve, `verify/ordercheck/Program.vb`. Per-fixture detail in the commit message and the §15 changelog entry; not repeated here. |
| §5 Acceptance | ✅ Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0** Release; harness **ALL PASS**; `verify-gate prepush` **GATE PASSED**. |
| §4 Fences / parity / boundary, all as specced | ✅ HC28 stays free (no settings keys). Display-parity: no obligation (console + markdown, marker write silent — no snapshot line, no card binding). No dataset boundary. |

---

## 2. Deviations & decisions

**2.1 `expected-missing` scoped to "before the very first evidenced process life" only.**
The classification table's own wording — "recorded capturing, but recorded not-up for the
hour" — is compatible with a broader reading (any gap between two process lives, not just
the very first). I read it narrowly: J-B's own residual-ambiguity clause explicitly assigns
BOTH the trailing window AND cross-GUID gaps to `defect`, and those are structurally the
ONLY two ways a "no uptime evidence" hour can arise once at least one process life has
started. That leaves exactly one clean, unambiguous case for `expected-missing`: hours
before the first process life ever started, where there is nothing ambiguous to resolve —
capture genuinely had not begun. If this reading is too narrow, the fix is confined to one
`If` branch in `CoverageReport.ClassifyHour` and one part of A49b; nothing else depends on
which of the two readings is correct.

**2.2 S1 is skipped only when BOTH uptime sources are empty.** The proposal's own §2
revision makes `analysis_log.csv` primary specifically to prevent a missing `ws_health.log`
from blinding S1 — building "ws_health.log absent ⇒ S1 skipped" literally, as A49g's text
alone reads, would contradict that revision the same build is supposed to honour. I built
the compound condition (`BuildResult` in `CoverageReport.vb`) and wrote A49g against it. The
degrade-path fallback when S1 truly has nothing: judge every hour by the store alone (clean
⇒ `Captured`, otherwise ⇒ `Defect`, never `ExpectedMissing` — there's no positive evidence to
justify a clean-down read).

**2.3 Positive store evidence always wins as `Captured`, regardless of how ambiguous the
uptime read is.** Not explicit in either ruling. If an hour sits inside a cross-GUID gap or
the trailing window but the store itself shows clean, gap-free data for that hour, that is
direct proof capture was working — stronger evidence than an absent/ambiguous uptime signal,
so it overrides. The alternative (ambiguity always forces `Defect` even over a clean store)
would produce false alarms on exactly the hours where the store already answers the
question. `ClassifyHour` checks store-clean before consulting the uptime kind at all.

**2.4 D7's scoping rule (never read current config, never a baseline) verified by hand.**
`CoverageReport.ResolveScope` takes only `List(Of CaptureMarkerLog.MarkerRecord)` — parsed
from the marker file — and nothing in `CoverageReport.vb` or the CLI's `coverage` case calls
`SettingsLoader.Initialise` or reads `SettingsLoader.Current` at all (unlike `replay` /
`validate` / `report`, which need engine settings for the scoring replay). The marker is
written once, at process start, in `MainForm_Layout.vb`, from `SettingsLoader.Current` — the
MERGED overlay-aware singleton, confirmed against `architecture.md`'s own description of the
overlay mechanism, not the tracked base file.

**2.5 The `DOWN`-proves-alive trap verified by hand.** `ParseWsHealthEvidence` extracts only
`(utc, instanceId)` from each line; the state token (`parts(1)`) is read by nothing. Every
line — `OK`, `DOWN`, `DEGRADED`, `REST` — contributes identically to the up-interval it's
part of. A49c pins this directly (DEGRADED/REST-only evidence resolves the SAME as an
OK-covered hour); A49a's "process that ends without a DOWN line" sub-case is the same
guarantee from the other direction (closing an interval never depends on seeing a specific
token).

**2.6 A real bug found and fixed via A49i — recorded because it's the shape of error most
likely to recur.** The first implementation of `ClassifyUptime` checked whether an hour's
START INSTANT fell inside an evidenced `[First, Last]` interval. That is only correct when
evidence happens to land exactly on the hour boundary. Real heartbeats don't — the analysis
CSV's own documented cadence is p50 60s / p90 155s past whatever moment the run fires — so
this false-negatived every hour whose evidence landed a few minutes past `:00`, which is
effectively every hour. A49i (S1 primary/supplement precedence) is what caught it: feeding
`analysis_log.csv` rows at `:01`/`:02` and querying uptime at the hour's `:00` boundary
returned `before-first` instead of `up`. Fixed to check `[hourStart, hourEnd]` OVERLAP
against the interval instead (`CoverageReport.vb`, `ClassifyUptime`). Flagging this
prominently per the brief's own instruction that the fixtures can't fully self-catch a
misunderstanding the implementer also wrote the fixture under — this is the one case where
the fixture DID catch it, and it's worth knowing why, in case a future edit reintroduces the
narrower check.

**2.7 The uptime/analysis-log/marker files' location for the CLI is the repo root, CWD-
relative — the same directory `backtest_data` itself resolves against.** D2/C4 name the
files' location on the CAPTURING box as `<exe>\`, the store's parent. `BacktestProgram.vb`
already sets CWD to the repo root and resolves `HistoricalStore.StoreDir` ("backtest_data")
relative to it; the natural parallel for a copy-back is the SAME directory holding
`backtest_data\`, i.e. the repo root. `BuildResult` is called with three explicit paths
(`analysisLogPath`, `wsHealthPath`, `markerPath`) precisely so a future different convention
is a one-line change in `BacktestProgram.vb`'s `coverage` case, not a redesign.

**2.8 A49h pins the CLI's decision predicate, not a literal process exit code.**
`BacktestProgram.vb` carries its own `Main` and is excluded from `OrderCheck.vbproj` by the
same convention as `replay`/`validate`/`report`/`fetch` — none of those are process-launched
from the harness either. A49h constructs the same boolean the `Case "coverage"` block
evaluates (`opts.Strict AndAlso covResult.CountByClass(HourClass.Defect) > 0`) against real
`ClassifyHour` output and checks it three ways (defect+strict, expected-missing-only+strict,
defect+no-strict). The literal `Environment.ExitCode`/process-exit path is reasoned, not
harness-proven — I did smoke-test it manually (§4) but that's not a standing fixture.

**2.9 The version-bump WARN in `verify-gate` is expected and correctly non-blocking.**
`Core/CaptureMarkerLog.vb` is new under `Core/`, which the gate's `enginePrefixes` heuristic
always flags absent a settings.json version bump. This build adds no settings keys (D7
reads existing `trade_store.enabled`/`store_dir`), so no bump is warranted — matching the
brief's own "keys-changed, not app-touched" rule. The gate's version-bump check is WARN-only
by design (`verify-gate.ps1`'s own comment: "the 'is this a behaviour change?' call is too
soft to hard-block"), and it fired exactly once, correctly. Did not add `[no-engine-change]`
to the commit message — that token is for cases where the change genuinely touches nothing
behavioural; this one adds a new file under `Core/`, so the honest signal is "yes, `Core/`
changed, no, it needed no version bump," which is what the WARN (rather than a suppressed
silence) actually shows.

**2.10 No `--store-dir` CLI flag.** The proposal's own CLI signature
(`coverage --from ... --to ... [--gap-ms] [--out] [--strict]`) has none, so the verb always
resolves against `HistoricalStore.StoreDir` ("backtest_data", CWD-relative). `CoverageReport`
functions all take an explicit `storeDir` parameter internally (for fixture isolation in temp
directories), but the CLI wiring only ever passes the one real value. If a future session
wants to point the verb at an arbitrary store, that's a one-flag addition in
`BacktestProgram.vb`, not a `CoverageReport.vb` change.

**2.11 Independent validation against the real local store.** A read-only smoke run
(`BacktestRunner coverage --from 2026-07-28 --to 2026-07-31`) against the actual local
`backtest_data\trades_2026-07.csv` reproduced the proposal's own §0/§0a hand-derived figures
exactly — `capture begins 2026-07-29 10:43 UTC` and `longest gap 161.7s` — both independently
computed by this build's code from the raw file, not copied from the doc. No marker/
analysis_log/ws_health files exist locally (expected: the local box is AWS-only for capture
per D1, so it has never written a marker), which is why every walked hour in that smoke run
read `unknown-scope` — also the correct behaviour, not a bug.

---

## 3. Not done — deliberately (Session 2's scope, per the brief's own split)

1. **Part B — the live `TAPE STORE` status element.** Untouched. `TradeStoreWriter` already
   tracks the state this needs (seconds-since-flush, rows-this-process); Session 2 is a read
   plus a label, per the brief.
2. **Fixture A49m.** Needs Part B to exist (it pins the weekday `out-of-scope-weekend`
   classification — already implemented and exercised indirectly by every A49 fixture's
   `A49Monday()` weekday anchor — PAIRED with Part B's unconditional liveness read on the
   same weekend hour). The weekend classification itself is live code (`ClassifyHour`'s
   `DayOfWeek` check), just not yet pinned by a dedicated regression fixture.
3. **Version bump / settings.json entry for Part B.** Per the brief, D7's marker costs
   nothing extra precisely because Part B's build (not this one) is the one that takes the
   version bump.

---

## 4. Post-ship

**Nothing to watch behaviourally** — Part A is additive and read-only; not running the verb
IS the rollback, and the marker write is a few silent bytes per process start reading
existing config.

**For Session 2:** the marker is already live in `MainForm_Layout.vb`, so once Session 2
ships and the app is rebuilt/redeployed, `capture_marker.log` starts accumulating
immediately — no separate activation step. Session 2's implementer should re-read §0 of
this document before starting; the `expected-missing` and S1-skip decisions (§2.1, §2.2)
constrain how A49m's weekend/liveness pairing should be written, since A49m needs the SAME
six-class walk this session already ships.
