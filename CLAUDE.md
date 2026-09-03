# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Start Protocol

1. **Read `docs/DeribitIndicatorProject.md`** in full (**58K tokens / 139,185 B, MEASURED 2026-09-04**; historical content lives in `docs/history-archive.md`). Touch the archive doc only if you need pre-v61 version history or pre-v27 settings rationale.

   ⚠ **This number has now been wrong FOUR times, always in the same direction — the file is always BIGGER than stated.** It read "~10K" while the file was ~9× that; corrected to "~24K" on 2026-08-02; measured at **47K** on 2026-08-12; carried "~41K" until 2026-08-24, when it **measured 52K**; then carried that until 2026-09-04, when it **measured 58K**. **Assume it is stale and re-check it, rather than budgeting against it.**

   ⭐ **The 2026-09-04 instance is the FIRST one that is NOT a retention breach, and that is the useful part.** §15's cap was checked before the archive was reached, exactly as the next paragraph-but-one instructs: **five settings versions (v68 · v67 · v66 · v65 · v64) sit exactly at the cap, every settings-untouched row is newer than v64's 2026-07-31, and no item holds two rows.** **The rule is being followed and the file still grew 14 %** — because the growth is now in **cell CONTENT**, not row count. The 2026-09-01 absorption row alone runs past 2,000 tokens. **Collapsing cannot fix that; only shorter cells can, and no rule currently caps cell length.** Name this before reaching for either remedy.

   ⛔ **THE MECHANISM IS WORTH MORE THAN THE FIGURE, and the 2026-08-24 instance names it precisely: the 41K was NOT a measurement.** It was the 47K reading *scaled by the byte reduction* from the trim — and the note said so, honestly, with a ±10 % caveat. **The real value was 27 % higher, outside that band.** **Bytes do not scale to tokens at a fixed rate**, because a trim that removes prose and a trim that removes dense tables move the ratio in opposite directions. ⚠ **A derived number inherits the authority of a measured one the moment it is written into a table.** **If you have not measured it this session, do not quote it — measure it or omit it.**

   ✅ **How to re-measure, so the next seat does not scale again:** open the file with the `Read` tool and read the token count it reports for the whole file. That is a real count, not an estimate.

   ⚠ **If it has grown again, check §15's row count BEFORE reaching for the archive.** The 2026-08-14 trim recovered 13 % of the file and moved **nothing** to the archive: retention was already at its cap (five settings versions), and the entire overgrowth was the *other* half of §15's rule — **one item had taken three rows, twice.** The archive is the remedy for *age*; collapsing is the remedy for *sprawl*, and sprawl has been the actual mechanism both times.
2. **Read `docs/architecture.md`** in full.
3. **Load the `crypto-trading-context` skill** — it carries the trader profile and writing style. Do not separately read `docs/trader-profile.md`; the skill loads it.
4. **Do not read individual `.vb` files at session start** — only open them when a specific edit is required.
5. **When the task involves prioritisation, sequencing, or selecting new features**, also read `docs/roadmap.md` — the cross-project strategic roadmap (calibration queue, indicator queue, the DeribitOrderPlacementApp signal bridge, Linux port). Skip it for routine implementation of an already-specced item.
6. **Before saying anything about what is outstanding, read `docs/trader-tick-queue.md`** — it is the state read, and its §0 scopes what every other doc is authoritative *for*. **Authority is scoped, not ranked:** `roadmap.md` = execution order · `backlog-dependency-map.md` = what blocks what, **not** current state (its cells are dated individually) · `seat-handover-2026-07-18.md` §3 = standing rules, still binding · each spec's own D-table = the decision text, which wins over any summary. A doc's status header is **not** evidence of code state — verify in the tree (`git log --oneline -S'<symbol>' -- <file>`) before offering any spec as available work. A 2026-08-01 sweep found 4 of 13 queue rows describing already-shipped work as outstanding, every one traceable to stale status prose.

This preserves context budget for actual work.

## Shell / Path Tips (Windows)

- **Do not `cd`** in shell commands — the harness manages the working directory. Use absolute paths in `Read`/`Write`/`Edit`/`Bash` calls.
- **Bash + Windows paths**: backslashes get consumed as escape characters. Use forward-slash form (`/c/Dev/DeribitVerdictEngine/...`) or quote the path (`"C:\Dev\DeribitVerdictEngine"`).
- **Build verification**: run `dotnet build` against the absolute solution path (`dotnet build /c/Dev/DeribitVerdictEngine/DeribitVerdictEngine.sln` or use the harness `Bash` working dir).
- **Do not line-anchor greps over VB (learned 2026-08-05, order-app seat's miss, relayed).** A scan anchored on `^\s*Return` misses **`If … Then Return`** — VB's inline single-line form, and the *commonest* one in this codebase. The same trap applies to `^\s*Throw`, `^\s*Exit`, `^\s*Continue`, and to any `^\s*If` scan against a `Select Case` arm. **Prefer unanchored patterns and filter the noise by eye**; a line-anchored VB grep that returns few hits is more likely mis-anchored than genuinely sparse. Their instance is worth knowing because the missed form *was the one the claim was about* — an anchor can hide exactly the case you are checking.
- **A build failure that names a locked `.exe` is not a compile error.** `MSB3021`/`MSB3027` on `bin\...\DeribitVerdictEngine.exe` means the app is running; the copy step fails while compilation succeeded. Close it, or build `verify/ordercheck/OrderCheck.vbproj` instead — it links `Core/`, `analysis/` and the root `.vb` files, so it type-checks the same sources without touching the app's output.

---

## Build & Run

This is a **VB.NET / .NET 8 Windows Forms** desktop application. Solution file: `DeribitVerdictEngine.sln`.

```bash
# Build (from repo root)
dotnet build

# Run
dotnet run

# Build Release
dotnet build -c Release
```

**Bundled fonts.** The app embeds Geist Mono (`fonts/*.ttf`) as resources and
ships `fonts/OFL.txt` alongside the .exe (SIL OFL 1.1 licence). Any installer
or xcopy deployment must include the contents of `fonts/`. The .ttf files travel
inside the .exe via `EmbeddedResource`; the OFL licence travels as `Content`.

Open in **Visual Studio 2022** for the full WinForms designer experience. `MainForm.Designer.vb` is auto-generated — never edit it manually.

There is no automated test suite. Verification is done via live runs with the auto-run timer and the CSV calibration report (`analysis_log.csv`).

---

## Architecture Overview

The engine polls the Deribit REST API, computes technical indicators, scores them through a multi-tier pipeline, and outputs a directional verdict (STRONG LONG → STRONG SHORT) with ATR-based levels and a Kelly sizing advisory.

### Layer responsibilities

| Layer | Files | Role |
|---|---|---|
| **API** | `DeribitClient.vb` | All Deribit REST calls — candles (1m/5m/15m), funding, order book, book summary, recent trades |
| **Norms** | `DynamicNorms.vb` | Per-run adaptive thresholds: ATR scale factor, session-adjusted volume thresholds (ASIA/LONDON/NY), VWAP dev threshold |
| **Indicators** | `Core/Indicators_*.vb` | Pure functions; no state. Four files: Momentum, Volatility, OrderFlow, Structure |
| **Scoring** | `Core/ScoringEngine_*.vb` | Signal scoring pipeline → VerdictResult. Split across Types, Helpers, _Scoring (Steps 2–3b), _Verdict (Steps 4–5b+) |
| **Settings** | `SettingsLoader.vb` + `Core/Settings/EngineSettings.vb` + `settings.json` | JSON singleton; all thresholds externalised, no hardcoded magic numbers |
| **UI** | `UI/MainForm_*.vb` | WinForms shell. Partial-class files: Layout (fields/constants), AutoRun, Analysis (orchestrator), PlaintextSnapshot (`BuildPlaintextSnapshot` — the only text renderer; feeds the output dump), Render_Cards (card bindings — the second rendered surface), Calibration, ExitGuard, LiveStrip. (`Render_Header`/`Render_Sections` were deleted in P5b.) |
| **Logging** | `AnalysisLogger.vb` | CSV run logger + CalibrationReport |

### Data flow (single analysis run)

```
[Analyze Now click]
  → MainForm_Analysis.RunAnalysisAsync()
      → Task.WhenAll: DeribitClient calls (1m/5m candles, 15m cached TTL=60s,
                      funding, book summary, order book, recent trades)
      → DynamicNorms.Compute() → session-adjusted norms
      → All Indicators_* functions → IndicatorResults r
      → ScoringEngine.Calculate(r, posState, norms, cfg) → VerdictResult v
            Steps: 1 regime MaxScore | 2 signal scoring | Pass2 cross-confirm
                   Pass2b OI×CVD gate | 3 funding baseline | 3b funding momentum
                   4 regime veto | 4b MTF gate veto | 4c VPFR HVN cap
                   5 threshold → verdict | 5b VerdictContext | 6 HoldStatus
                   7 ATR levels | Post: CalcKellySizing
      → AnalysisLogger.LogRun() → analysis_log.csv
      → BuildPlaintextSnapshot(v, r, …) → plaintext surface + output dump
        (runs BEFORE the card binds — its inline CalcKellySizing populates v.Kelly*)
      → BindCard*(…) in MainForm_Render_Cards.vb → card UI
```

### Key design invariants

- **All thresholds in `settings.json`** — no magic numbers in `.vb` files. `SettingsLoader.Current` is the singleton accessor.
- **Scoring pipeline is additive** — signals vote +1/0/−1 into `ScoreState`; regime MaxScore (19/18/15) sets the ceiling; verdict thresholds are `Math.Ceiling(regimeMax × pct)`.
- **MTF Gate (15m) is a hard veto** — BLOCK forces NO TRADE regardless of score. Direction-aware since v31: `CalcMTFGate` emits per-side flags; Step 4b consults the flag matching the verdict's dominant side and composes the final reason (`VerdictResult.MTFGateReason`).
- **Trade lists are chronological ascending** (v31) — `GetRecentTradesAsync` reverses the API's newest-first order before returning. Window consumers take the most recent n trades from the END of the list (`IndicatorEngine.LastN`); `Take(n)` on a trade list selects the OLDEST n and is a bug.
- **VerdictContext is display-only** (Step 5b) — CONFIRMED / FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK. Zero scoring impact.
- **Kelly sizing is display-only** — suppressed when KellyF ≤ 0. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face.
- **Funding modifier is adjunct** (Step 3 + 3b) — never in Step 2 to avoid double-counting.
- **OI × CVD cross-confirm is Pass 2b** — bonus on agreement, penalty on full-signal conflict only; partial OI conflict is non-penalising.
- **MicroCVD early/late values are net USD deltas** — negative values are valid and expected.
- **Session volume norms adjust thresholds, not signals** — `DynamicNorms.ApplySessionVolume()` scales `VolHighThreshold`/`VolMidThreshold` by UTC bucket; it is not a hidden directional signal.
- **Dual-session VWAP** anchors at 00:00 UTC and 13:30 UTC — single-anchor deviates badly after session handoffs.

---

## Collaboration Rules

**Before proposing any upgrade, new indicator, or settings change**, read `docs/trader-profile.md` and verify alignment with:
- The PREFERRED / NEUTRAL / REJECTED indicator list (Section 3–4)
- Explicitly banned patterns: double-counting (funding must not appear in Step 2 scoring), non-directional padding (e.g. BBW NONE = +1 was removed in v0.18), fixed % targets, flat penalties instead of ADX-proximity scale
- Conservative false-positive tolerance (Section 6) — the engine should say NO TRADE rather than output a weak directional signal

**Spec-first workflow.** Novel features require a proposal `.md` file committed to `/docs` before coding begins. Implement only approved specs — do not invent design decisions unilaterally.

**Every spec or implementer brief handed to a new implementer MUST carry a model + effort recommendation (RULED 2026-08-03, trader-directed).** The seat that wrote the spec has just done the hardest read of it and is the only one positioned to judge what building it needs; making the trader ask is making the wrong person estimate. Put it at the **top** of the brief, not the end. A bare "Sonnet, high" is not enough — it must carry:

- **Model + effort**, and **why that tier** — name what makes it easy or hard. "The judgment work is done and every mechanical piece has an in-repo template" is a reason; "this is straightforward" is not.
- **Where that model will specifically slip**, if anywhere — the two or three concrete traps, not a general warning. Say plainly when the fixtures cannot be relied on to catch them (the implementer writes the fixtures too, so a misunderstanding propagates into its own test).
- **The escalation trigger** — the observable behaviour that means stop and move up a tier.
- **A session split** when the build is large, with per-session effort. Sequence it by dependency, not by size.

Worked example: [`trade-store-coverage-report-implementer-brief.md`](docs/trade-store-coverage-report-implementer-brief.md) §0.

**Set your own effort level to match the task in front of you (RULED 2026-08-04, trader-directed).** Do not run everything at one tier. If the trader prefixes an explicit effort directive, that wins. Otherwise infer it, and say which you picked when it is not obvious:

- **Low** — recipes with a known answer: watch-reads, status checks, a settings-value lookup, re-running a documented command, tidying a doc.
- **Medium** — mechanical builds against a settled spec, routine reviews, writing up a decision already made.
- **High** — derivations, ⚠ reviews, anything correcting a prior ruling, spec-backs on a build that touched scoring, and any task where being wrong is expensive or hard to notice.

**Re-assess mid-task rather than once at the start.** A task that opens as a lookup and turns into a contradiction between two documents has changed tier, and the tier should change with it — this is how the 1.45-ratio and `AWARD%` errors were caught, both of which began as routine reads. Escalating costs a few minutes; not escalating costs a wrong number that ships and is inherited.

**Reporting a multi-lane batch back to a reviewing seat** follows `docs/batch-review-packet-convention.md` — two documents, not one: a `*-batch-summary.md` outcome record (what happened) plus a `*-spec-back.md` review packet (ranked verification handles · decisions queued with your read where you have one · feedback on the spec's own assumptions · what you did not verify). Fable-confirmed 2026-07-31.

**Push back explicitly** when a proposed change would reintroduce a deliberately removed pattern (non-directional padding, double-counting, fixed penalties vs. ADX-proximity scale). Cite the version it was removed and why.

**Every commit that changes engine behaviour** gets a version history entry in `docs/DeribitIndicatorProject.md` Section 15 and a `settings.json` version bump if any config keys were added or changed.

**Engine display-string parity rule (hard rule).** Any commit that adds, removes, renames, or re-formats a line emitted by the text renderer (`BuildPlaintextSnapshot` in `UI/MainForm_PlaintextSnapshot.vb` — the only text surface since P5b deleted `MainForm_Render_Header.vb`/`MainForm_Render_Sections.vb`) — including `VerdictResult` / `IndicatorResults` field-default changes that alter rendered output — MUST update the corresponding card binding in `UI/MainForm_Render_Cards.vb` in the same commit, or state in the commit message why no card surface is affected. The P5-test text-parity harness cannot catch this drift (it diffs legacy↔snapshot, which move together; the card is the unchecked third surface). Evidence: three drift instances in one cycle — v31 `ccdd652` (MTFGateReason default), Tier D `0bd1b63` (Kelly Notional line), Tier D `482c9bb` (ATR ratio relabel). See `docs/ui-reskin-consolidated-fix-spec-back.md` §2–§3.

**Fixture-literal provenance rule (hard rule, RULED 2026-08-11).** A fixture that passes a settings-derived threshold as a literal MUST declare which of two things it is asserting, in a comment at the call site:

- **SHIPPED BEHAVIOUR** — then it must **derive the value from cfg**, never hardcode it.
- **MECHANISM** — then a literal is correct, and the comment must say so and why.

**Why this is a rule and not a preference: no tool can tell the two apart, and one of them rots.** `A20a`/`A20b` pass OFI thresholds at 2.0/0.5 — neither the method default nor the shipped 1.6/0.625 — and that is **legitimate**: they are refactor-equivalence tests comparing two code paths on identical inputs, so any consistent value serves. `A6` pins `trendGate:=10.0` against a shipped 23.0 and that is **stale**. To a machine they are the same shape, which is why the value-copy guard (A56b) explicitly **cannot** cover this case and why it falls to review.

**Evidence it is needed:** the `OBV.trend_gate` divergence survived **two months** and was still live after v66 "fixed" it, because the fixture copy was never in scope. A second instance — `BuildResolutionCfg`'s `RocSlopeDeltaThreshold = 0.105` against a v40-shipped 0.06 — was found the same day, by accident, while checking something else. **Two known, found by two different routes, neither by a test.** See `docs/seam-audit-2026-08-11.md` and `docs/seam-audit-decisions-second-opinion-2026-08-11.md` §5.1.

**A value ruled into a CONSTANT goes `Public Const`, not `Private Const` (RULED 2026-08-11).** A ruling that a number is a constant rather than a settings key is about *tunability*, not *visibility* — `Public Const` satisfies it fully (no settings key, no version bump, untunable at runtime) while letting the fixture read the production number. `Private Const` forces the fixture to restate the literal, which the F1 lesson forbids and which rots silently the first time the constant moves. **Name this in any spec that rules a value into a constant**, or the next implementer hardcodes it. Worked example: `TradeStoreWriter.RecentWindowCapacity`, read by fixture A55e rather than restated. Same failure the fixture-literal provenance rule above exists for.

**Verification handles must test the property, not a string that mentions it (RULED 2026-08-11).** A review packet's headline check read *"`grep -c "_lastTs"` must print 0"*. It printed **2** — both inside comments explaining that `_lastTs` had been removed. The field was gone and the build was correct; **a reviewer following the handle literally would have rejected a sound build.** Counting a name is a *copy* of the property and drifts from it the moment a comment mentions the name. **Assert the declaration or the executable reference** (`grep -c "Private _lastTs"`, or filter comment lines) — and note the same packet applied a comment filter on a lesser check and omitted it on the one billed as *"if you only run one."* See `docs/trade-store-write-guard-spec-back.md` §R1.

**Do not re-open settled design decisions** without new data or a concrete technical reason.

**Linux CLI port is the long-term target.** The current WinForms app is the active development surface, but a future port to a headless Linux service is on the roadmap (see `docs/DeribitIndicatorProject.md` Section 16.2). To keep the port tractable, all new code in `analysis/` and `tools/` MUST be host-agnostic — no `System.Windows.Forms` references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers (e.g., `AnalysisReportForm`) are allowed but must be thin wrappers that call host-agnostic core classes. Any new project (e.g., the auto-tweaker console app, which builds against its own `AutoTweaker.vbproj`) gets a separate project file with zero WinForms references.

---

## settings.json Version

**No version is quoted here on purpose** — this header carried "v31" while the tree ran to v65, and the same rot hit `architecture.md`'s header (stuck at v54). **Read the tracked repo-root `settings.json` line 2.** When adding new config keys, increment `version` and append an entry to `change_log` (newest first inside the array).

⚠ **The block list that stood here was SIX blocks short — corrected 2026-08-24.** It named 11 and omitted `resolution_profiles`, `exit_guard`, `live_strip`, `signal_bridge`, `alerts` and `trade_store`, several of which carry their own tweaker fence. **The 17 top-level blocks, verified against the tracked file 2026-08-24:** `indicators` · `session_volume` · `resolution_profiles` · `mtf_gate` · `auto_run` · `scoring` · `kelly` · `regime_gates` · `regime_weights` · `network` · `analysis_logging` · `performance_display` · `exit_guard` · `live_strip` · `signal_bridge` · `alerts` · `trade_store`. ⚠ **`trade_costs` is NOT top-level — it is nested under `scoring`.** **`docs/DeribitIndicatorProject.md` §6 carries the same list annotated with each block's tweaker-fence status; prefer it over this one.**

The exact current version is the source of truth — read the **tracked repo-root** `settings.json` **line 2** (`"version": N`) before assuming. *(⚠ This sentence said "line 1" while the paragraph above said "line 2"; **line 2 is correct** — line 1 is the opening `{`. Reconciled 2026-08-24. A two-line instruction that contradicts itself sends half its readers to the wrong line.)* **Name which copy you read: `bin\Debug\net8.0-windows\settings.json` is a build artefact and legitimately lags the tracked file until the next build** (`CopyToOutputDirectory=PreserveNewest` copies on *build*, not on push — as of 2026-08-01 tracked is v64 while the running collector's copy is v63). Quoting the bin copy as "the version" is a live orientation hazard, not a hypothetical one. Always bump from whatever is current, not from the number quoted here (this header drifts).
