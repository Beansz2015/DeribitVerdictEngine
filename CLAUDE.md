# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Start Protocol

1. **Read `docs/DeribitIndicatorProject.md`** in full (~10K tokens after the 2026-05-17 trim; historical content moved to `docs/history-archive.md`). Touch the archive doc only if you need pre-v27 settings rationale or full version history.
2. **Read `docs/architecture.md`** in full.
3. **Load the `crypto-trading-context` skill** — it carries the trader profile and writing style. Do not separately read `docs/trader-profile.md`; the skill loads it.
4. **Do not read individual `.vb` files at session start** — only open them when a specific edit is required.

This preserves context budget for actual work.

## Shell / Path Tips (Windows)

- **Do not `cd`** in shell commands — the harness manages the working directory. Use absolute paths in `Read`/`Write`/`Edit`/`Bash` calls.
- **Bash + Windows paths**: backslashes get consumed as escape characters. Use forward-slash form (`/c/Dev/DeribitVerdictEngine/...`) or quote the path (`"C:\Dev\DeribitVerdictEngine"`).
- **Build verification**: run `dotnet build` against the absolute solution path (`dotnet build /c/Dev/DeribitVerdictEngine/DeribitVerdictEngine.sln` or use the harness `Bash` working dir).

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
| **UI** | `UI/MainForm_*.vb` | WinForms shell. Four partial-class files: Layout (fields/constants), AutoRun, Analysis (orchestrator), Render_Header, Render_Sections |
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
      → RenderOutput(v, r) → RTF display
      → AnalysisLogger.LogRun() → analysis_log.csv
```

### Key design invariants

- **All thresholds in `settings.json`** — no magic numbers in `.vb` files. `SettingsLoader.Current` is the singleton accessor.
- **Scoring pipeline is additive** — signals vote +1/0/−1 into `ScoreState`; regime MaxScore (19/18/15) sets the ceiling; verdict thresholds are `Math.Ceiling(regimeMax × pct)`.
- **MTF Gate (15m) is a hard veto** — BLOCK forces NO TRADE regardless of score.
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

**Push back explicitly** when a proposed change would reintroduce a deliberately removed pattern (non-directional padding, double-counting, fixed penalties vs. ADX-proximity scale). Cite the version it was removed and why.

**Every commit that changes engine behaviour** gets a version history entry in `docs/DeribitIndicatorProject.md` Section 15 and a `settings.json` version bump if any config keys were added or changed.

**Do not re-open settled design decisions** without new data or a concrete technical reason.

**Linux CLI port is the long-term target.** The current WinForms app is the active development surface, but a future port to a headless Linux service is on the roadmap (see `docs/DeribitIndicatorProject.md` Section 16.2). To keep the port tractable, all new code in `analysis/` and `tools/` MUST be host-agnostic — no `System.Windows.Forms` references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers (e.g., `AnalysisReportForm`) are allowed but must be thin wrappers that call host-agnostic core classes. Any new project (e.g., the auto-tweaker console app) builds against a separate `.csproj` with zero WinForms references.

---

## settings.json Version

Current: **v28**. Top-level blocks: `indicators`, `session_volume`, `mtf_gate`, `auto_run`, `scoring`, `kelly`, `regime_gates`, `network`, `performance_display`, `analysis_logging`. When adding new config keys, increment `version` and append an entry to `change_log` (newest first inside the array).

The exact current version is the source of truth — read `settings.json` line 1 (`"version": N`) before assuming. Always bump from whatever is current, not from the number quoted here (this header drifts).
