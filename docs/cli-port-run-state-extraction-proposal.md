# CLI-Port Run-State Extraction — host-agnostic run context + headless runner skeleton

**Status:** PROPOSED 2026-07-06 (Fable seat; the roadmap §4 Jul-6 deliverable). Sign-off decisions in §8.
**Roadmap:** W4 / O3 (month execution order item 6 — "behaviour-neutral, any gap"). After this ships, the Linux port becomes "write a renderer + a runner" — and the renderer moves *in this spec*, so the residual port is the runner + packaging.
**Class:** ZERO behaviour change. Not a ⚠ item, no dataset boundary, no settings change (no version bump). Pure code motion, pinned byte-identical by the existing harness + parity fixtures. Opus implements; coordinator reviews per stage.

**Scope clarification (trader question, 2026-07-07 — recorded with the D-ticks):** the end product is a **separate console app** (`tools/HeadlessRunner`, own `.vbproj`, `dotnet` on the Linux server) and the WinForms app remains the Windows surface, **behaving byte-identically at every stage** (the pinned proof obligation). What the WinForms *codebase* does undergo is internal restructuring — Stages 1–3 move the run state, the text renderer, and the run sequence into `Core/` classes the form then calls — deliberately, because a runner that *duplicated* the run sequence instead of sharing it would fork the pipeline into two drift-prone copies (the anti-pattern the `ComputeSideLevels` one-seam rule exists to prevent). One engine, two hosts. Stage 4 is the skeleton (compile-proof + a working `--once`/`--interval` loop); systemd/packaging/ops polish belongs to the later port spec (O3).

**D1–D7 ALL TICKED 2026-07-07** (trader; conditioned on the clarification above, which is exactly the design). Implementation may be scheduled any gap per §6.

---

## 1. Problem

`RunAnalysisAsync` (UI/MainForm_Analysis.vb) is the only place the full run sequence exists, and it lives on `MainForm`. The scoring/indicator/settings/transport layers are already host-agnostic; what keeps the engine WinForms-bound is (a) **cross-run mutable state held as MainForm fields**, (b) **the text renderer being a MainForm partial**, and (c) **the run sequence itself interleaving UI touchpoints** (card binds, labels, radios, MessageBox). This spec extracts (a) and (b) verbatim, seams (c) behind a small host interface, and adds a compile-proof headless runner skeleton.

## 2. Inventory — MainForm-held run state (audited 2026-07-06 against the v50 code)

| Field | Semantics (pinned as-is) | Run-path use |
|---|---|---|
| `_fundingHistory` (List(Of (UtcMs, Rate))) — **restated at v53** (was `List(Of Double)`, max 10) | append **every run** (no dedup), evict age > 30 min, **no count cap** — all of it inside `IndicatorEngine.AppendFundingSample`, which is already host-agnostic | CalcFundingMomentum (time-anchored — takes `nowUtcMs`), `r.FundingDelta` |
| `_ofiHistory` (List(Of Double), max 10) | append every run, trim head | CalcOFIMomentum |
| `_oiHistory` (List(Of OiSnapshot)) | append every run; evict > 70 min; 15 m/61 m window reads | OIChange15m/60m |
| `_mtfCandles15m` + `_mtfLastFetchTime` | TTL cache via `MtfRefreshPolicy`; **kept stale on fetch failure** | MTF gate |
| `_prevRegime` (String) | 1-bar RANGE_BOUND hysteresis; updated to rawRegime every run | regime classification |
| `_skipCount`, `_lastSkipReason` | incremented on skip-gate | status/LOG surface |
| `_wsDegradedThisRun` | set by `ResolveSource()` per run | WS status line |
| `_ledgerWarn` | recomputed from `verdict.LedgerMismatch` every run | LOG line |
| `_restSource`/`_wsSource`/`_marketState`/`_wsFeed`/`_parityComparer` | transport objects (already host-agnostic classes; MainForm only *owns* them) | fetches, OFI avg, aggr-vel |

UI touchpoints inside the run: `rbLong/rbShort` (posState), ARM-AUTOTRADE checkbox (via `EmitBridgeSignal/Skipped` glue), `RenderSkippedDashboard`, `UpdateLogInfo`, card binds, `UpdatePerformanceLabels`, `ComposePerfStripLine`, `_lastSuccessful*` capture, `ClearStaleOverlays`, `AnalysisCompleted` event, MessageBox (in `btnAnalyze_Click`, not the run itself).

`BuildPlaintextSnapshot` (UI/MainForm_PlaintextSnapshot.vb) reads **no MainForm state** — verified: fully determined by its `(verdict, r, norms, cfg, vwapWarmup, lastTradePrice)` arguments (its inline `CalcKellySizing` side-effect populates `v.Kelly*` and must keep running before card binds).

## 3. Design — four stages, each its own gate-green commit

### Stage 1 — `Core/EngineRunContext.vb` (the run-state container)

One host-agnostic class owning rows 1–8 of the §2 table. The mutation blocks move **verbatim** into methods so host and runner share one implementation:

- `AppendFunding(nowMs, rate)` — **restated at v53**: a thin delegation to `IndicatorEngine.AppendFundingSample`, which already holds the append+evict rule host-agnostically (so this row is now the easiest of the eight, not a verbatim block move). `AppendOfi(ratio)`, `AppendOi(ts, oi)` + eviction + the 15 m/61 m window reads, `MtfCandles`/`MtfLastFetch` (policy stays `MtfRefreshPolicy`), `PrevRegime`, `SkipCount`/`LastSkipReason`/`WsDegradedThisRun`/`LedgerWarn`.
- `MainForm` replaces the eight fields with one `Friend _runCtx As New EngineRunContext` and delegates. The `OFIHistoryMax` constant moves with its ring; the funding ring has no count cap since v53 (its 30-min horizon lives in `IndicatorEngine.FundingRingMaxAgeMs`).
- NOT moved: `_metricMode`, `_lastSuccessful*`, overlays, all card/timer fields (display state); the transport objects stay MainForm-owned this stage (the runner constructs its own in Stage 4 — the classes are already host-agnostic).

### Stage 2 — `Core/PlaintextSnapshotRenderer.vb` (the text renderer)

`BuildPlaintextSnapshot` + its private helpers move verbatim to a host-agnostic Shared class; the MainForm partial becomes a one-line delegate (or call sites re-point and the partial file is deleted — implementer's choice, state which). **Proof:** the P5-test parity fixtures pin the output byte-identical; the dump shape cannot move. The engine display-string parity rule is untouched (no line added/removed/renamed).

### Stage 3 — `Core/AnalysisOrchestrator.vb` + `IAnalysisRunHost` (the run sequence)

> ⚠ **AMENDED 2026-08-08 (trader-directed). Call-order preservation is now an explicit, provable acceptance condition — not a stated intention.**
>
> **Why this amendment exists.** This whole stage is safe to run only because it is behaviour-neutral, and behaviour-neutrality here rests entirely on the *order* of the run sequence being preserved. `MainForm_Analysis.vb` has ordering constraints that are load-bearing and not obvious from reading it — the clearest being that **`BuildPlaintextSnapshot` must run BEFORE the card binds**, because its inline `CalcKellySizing` is what populates the `v.Kelly*` fields the card then reads (CLAUDE.md, data-flow section). An extraction that preserves every call but reorders two of them produces a green build, a green harness, and **a card with blank Kelly values** — wrong in a way that looks right.
>
> **The condition.** Stage 3 is accepted only if the run sequence is proven byte-order-identical, not merely byte-output-identical on one fixture. Two outputs can match while the order that produced them differs, and the difference then surfaces on a path the fixture did not exercise.
>
> **How to prove it — an order ledger, not an eyeball.** Before the extraction, instrument `RunAnalysisAsync` to append each touchpoint name to an in-memory list as it fires, and capture that list for a set of fixture runs covering: a scored run, a SKIPPED run, a NO TRADE run, and an error-path run. After the extraction, capture the same list from `AnalysisOrchestrator.RunOnceAsync`. **The two lists must be equal, element for element, for every case.** Ship the comparison as a fixture so the property is defended afterwards, not just at the moment of the change.
>
> ⚠ **The error path is the one that will be missed.** Ordering on the happy path is easy to preserve by construction. An early `Return` or a `Try/Catch` that skipped a touchpoint in the original will quietly stop skipping it once the body is behind an interface — and no output diff on a successful run will show that.
>
> **Escalation trigger.** If the ledgers cannot be made equal for any one of the four cases, **stop and report rather than adjusting the expected ledger.** A mismatch is the finding. Editing the expectation to match the new behaviour converts a caught regression into a shipped one.

The body of `RunAnalysisAsync` moves to a host-agnostic `AnalysisOrchestrator.RunOnceAsync(deps, ctx, host)` with the UI touchpoints seamed behind:

```
Interface IAnalysisRunHost
    Function GetPositionState() As PositionState      ' radios (WinForms) / None or file (CLI)
    Function GetAutotradeArmed() As Boolean           ' ARM checkbox / False headless
    Sub OnSkipped(reason As String)                   ' UpdateLogInfo + RenderSkippedDashboard
    Sub OnScored(v, r, norms, cfg, vwapWarmup, lastTradePrice, snapshot) ' card binds + capture
    Function ComposePerfStripLine() As String         ' perf labels line / minimal CLI line
    Sub AfterPerfUpdate()                             ' UpdatePerformanceLabels / no-op
    Sub OnRunCompleted()                              ' overlays + UpdateLogInfo + event / no-op
End Interface
```

Hard constraints on the move: **call order is byte-preserved** (skip-gate → parity compare → indicators → posState → Calculate → signal-id tick → ledgerWarn → LogRun → snapshot → OnScored/binds → perf tracker → dump append → emit → completed); each `IAnalysisRunHost` method body on the WinForms side is the verbatim code that sits at that point today; `SignalEmitter` emission moves into the orchestrator (it is already host-agnostic — the WinForms glue only contributed the ARM flag, now `GetAutotradeArmed()`), keeping the after-snapshot/after-binds parity ordering. Method granularity may be adjusted at implementation if order-preservation forces it — state deviations in the spec-back.

### Stage 4 — `tools/HeadlessRunner/` skeleton (compile-proof, not a product)

- Own `HeadlessRunner.vbproj`, **`net8.0` (not `net8.0-windows`), zero WinForms references** (CLAUDE.md hard rule). The root project already excludes `tools\**\*.vb` from auto-discovery (AutoTweaker precedent) — no root-glob hazard.
- Links the engine sources it needs (`Core/**`, the root host-agnostic files: DeribitClient, transport/WS stack, DynamicNorms, AnalysisLogger, AnalysisOutputDump, LivePerformanceTracker, OhlcCache, OiSnapshot) — the ordercheck linked-source pattern. Any accidental WinForms dependency in a linked file becomes a **compile error on net8.0 — that compile IS the port-readiness proof** and the standing regression guard against re-coupling.
- `Program.vb`: load settings → construct transport (rest, or ws via `DeribitWsFeed`+`MarketState`) → `EngineRunContext` → loop `AnalysisOrchestrator.RunOnceAsync` with a trivial `ConsoleRunHost` (posState None, armed False, snapshot → stdout and/or dump file) — SINGLE (`--once`) and fixed-interval REPEAT (`--interval N`). `on_close` triggering deferred (BarCloseDetector is already host-agnostic; wiring it is runner polish, not port risk).
- Explicitly out of scope: exit guard, live strip, perf-strip rendering, auto-tweaker driving, any new behaviour. The skeleton exists to prove the seam is complete and to be the Linux `dotnet run` entry point later.

## 4. What deliberately stays WinForms-side

Cards/binds, all four timers (auto-run, countdown, on-close watcher, exit-guard, live-strip), radios + ARM checkbox (as `IAnalysisRunHost` inputs), `_lastSuccessful*` + skip overlays, perf labels, MessageBox error surface, TweakSettingsForm/dialogs. The exit guard and TAPE strip read `MarketState` directly and stay host features (a headless equivalent is a port-time decision, not this spec).

## 5. Proof obligations (per stage)

- 3 Release builds 0/0 (solution + AutoTweaker + OrderCheck) + **HeadlessRunner builds 0/0 from Stage 4**; `verify-gate.ps1 -Mode prepush` green.
- A-series unregressed at every stage; parity fixtures byte-identical at Stage 2/3 (the snapshot text is the pinned surface).
- New fixtures (Stage 1): S9 funding dedup + trim-at-10; OFI ring trim; OI eviction at 70 min + the 15 m/61 m window picks (oldest-in-window semantics); 1-bar regime hysteresis (RANGE_BOUND after TRENDING holds prev, then releases); MTF cache kept on failed refresh. Pin **current** semantics exactly — any latent oddity found during the move is reported, not fixed (zero-behaviour-change means bugs move too; file them separately).
- Stage 3: a fixture (or ordercheck extension) asserting the WinForms host produces an identical CSV row + dump block + `verdict_signal.json` for a replayed fixture run pre/post-refactor.
- CSV v0.8 / eval cache v4 untouched; no settings keys added.

## 6. Sequencing & seat discipline

Behaviour-neutral → may land in any gap (roadmap §5 item 6), including while the #5 collection window is open — it is not a scoring change. BUT it rewrites `MainForm_Analysis.vb` wholesale: **serialize against B4b** (placed-geometry touches the same file). Do not start Stage 3 while B4b is uncommitted; rebase-order is trader's call at D6. Local-first, trader tests + pushes, coordinator reviews each stage.

## 7. Why this shape (and not alternatives)

- **Not a full port now:** the port waits on the tweaker live-fire prerequisite (§16.3) and has packaging/ops questions (systemd, headless settings edit) that deserve their own spec once the seam exists.
- **Not an events-based decoupling:** an interface with verbatim-moved bodies is mechanically reviewable (diff ≈ move); event indirection invites subtle reorderings — the parity ordering (snapshot → binds → emit) is a shipped invariant.
- **Not moving `_lastSuccessful*`/overlays:** they exist purely to repaint a WinForms grid after skips.

## 8. Sign-off decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Stage 1 container name/home: `Core/EngineRunContext.vb` | **Yes** |
| D2 | Ops state (`SkipCount`/`LastSkipReason`/`WsDegradedThisRun`/`LedgerWarn`) moves into the context (not just the five roadmap-named items) | **Yes** — they're run outcomes every host needs; display prefs stay behind |
| D3 | Renderer moves in this spec (Stage 2), not at port time | **Yes** — the parity fixtures make it cheap now; it's the port's biggest unknown otherwise |
| D4 | Orchestrator seam = `IAnalysisRunHost` (§3), order-preservation binding, granularity flexible with spec-back deviations | **Yes** |
| D5 | Runner: `tools/HeadlessRunner/`, `net8.0`, linked-source, SINGLE + interval only | **Yes** — the net8.0 compile is the port-readiness proof |
| D6 | Staging: 4 commits, gate + coordinator review each; Stage 3 serialized behind B4b's commit | **Yes** |
| D7 | LivePerformanceTracker + dump + CSV + emitter live inside the orchestrator (exact current order), perf-strip line via host | **Yes** |
