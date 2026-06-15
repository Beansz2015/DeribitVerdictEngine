# Engine Code Audit Brief — One-off Independent Review

**For:** a fresh conversation (model under test: **Fable 5**).
**Type:** one-off, read-only code audit + test pass + new-function ideation. **Not** an implementation task.
**Author of brief:** spec-author conversation (Opus 4.8), 2026-06-10.
**Deliverable:** a single structured report at `docs/fable5-audit-report.md` that the user carries back to the main conversation for triage. **You do not fix anything and you do not commit anything.**

---

## 0. Read this first — what you are and aren't doing

You are doing an **independent, adversarial, read-only audit** of the DeribitVerdictEngine codebase. The goal is to surface correctness bugs, fragility, and well-justified improvement ideas that the maintainer can triage. Think of yourself as a senior reviewer brought in cold — your value is the fresh eye, not continuity.

**You ARE doing:**
- Static reading of the engine code for correctness, edge cases, numerical/financial soundness, resilience, and concurrency safety.
- Targeted dynamic verification (build, run, exercise specific paths) to confirm or refute suspicions.
- Proposing new functions / capabilities as **spec-stubs** (rationale + signature + trader-profile alignment), not implementations.

**You are NOT doing:**
- ❌ Editing, refactoring, or "fixing" any code. Zero source changes.
- ❌ Committing or pushing anything. The repo stays clean (a scratch test file is allowed under `verify/` only — see §6 — and must not be committed).
- ❌ Changing `settings.json`, the CSV schema, or any scoring math.
- ❌ Auditing the in-flight UI reskin systematically (see §5 — it's actively being worked in other conversations; churning on it creates conflicts).
- ❌ Re-opening settled design decisions. Where you disagree with a design, note it as an observation with a concrete technical reason — don't campaign.

If you find yourself wanting to write a fix, **stop and write it up instead.** The report is the product.

---

## 1. Session start protocol (read in this order)

1. **`CLAUDE.md`** (repo root) — architecture overview, layer responsibilities, data flow, design invariants, collaboration rules. This is your map.
2. **`docs/DeribitIndicatorProject.md`** §1–3 + §15 (engine state + version history). Skip §4–14 unless a specific finding needs it.
3. **`docs/architecture.md`** — codebase structure + display-behaviour notes.
4. **`docs/trader-profile.md`** — **mandatory before you propose anything.** Sections 3 (preferred indicators), 4 (rejected approaches), 5 (risk), 6 (signal philosophy). Your new-function ideas get checked against this.

Do **not** read every `.vb` file at session start. Open source files only when a specific audit thread needs them. Preserve budget for actual analysis.

---

## 2. The program in one paragraph

VB.NET / .NET 8 WinForms desktop app. It polls the Deribit REST API (1m/5m/15m candles, funding, order book, book summary, recent trades), computes technical indicators (pure functions in `Core/Indicators_*.vb`), derives per-run adaptive thresholds (`DynamicNorms.vb`), scores signals through an additive multi-tier pipeline (`Core/ScoringEngine_*.vb`) into a directional verdict (STRONG LONG → STRONG SHORT) with ATR-based levels and a Kelly sizing advisory, then renders to a card grid and logs each run to `analysis_log.csv`. All thresholds live in `settings.json` (no magic numbers in code). There is a separate host-agnostic console app under `tools/AutoTweaker/` and analysis utilities under `analysis/` that must stay free of WinForms references (a Linux CLI port is on the roadmap).

---

## 3. Hard constraints (violating any of these makes the audit unusable)

1. **Read-only.** No source edits. No commits. No pushes. No `git` mutations.
2. **Respect the trader profile.** When proposing functions or flagging "missing" capability, cross-check `docs/trader-profile.md` §4 (rejected approaches). Do **not** propose: Stochastic, MACD, CMF, fixed-% profit targets, ATR-based *execution* stops (ATR for sizing/reference is fine), non-directional reward components, double-counting a signal across scoring layers, or flat regime-transition penalties. If you think one of these is genuinely warranted by something you found, say so explicitly and cite the evidence — don't slip it in.
3. **Scoring math is sacrosanct in this audit.** You may *analyze* it for bugs, but any change is out of scope and high-risk; flag, don't touch.
4. **Settings externalization is an invariant.** If you find a hardcoded magic number that should be in `settings.json`, that's a valid finding — report it, don't move it.
5. **Host-agnostic rule.** Code in `analysis/` and `tools/` must not reference `System.Windows.Forms`, `Control.Invoke`, or `MainForm`. Flag any violation — it breaks the Linux port.
6. **Severity-rank everything.** Separate confirmed bugs from suspicions from style. A wall of unranked nitpicks is noise (see §8–9).

---

## 4. Audit dimensions

Work through these systematically. For each finding, capture `file:line`, what's wrong, why it matters, and a *direction* for the fix (not the fix itself). Not every dimension will yield findings — that's fine; absence of findings in a dimension is itself worth a one-line note.

### 4.1 Correctness & logic
- Off-by-one in window/lookback indexing (candle arrays, pivot detection, EMA/RSI/ROC warmup boundaries).
- Null / empty / insufficient-data handling: what happens on first run, short candle history, or a partial API response?
- Division-by-zero and NaN/Infinity propagation (ATR scale `AvgATR/CurrATR`, Kelly `b = target/stop`, ratio computations, VWAP deviation %).
- Negative-zero and clamp edges (funding rate near `1e-8`, sub-tick CAPPED suppression `max(0.5, ATR×0.02)`).
- Boolean/enum state coverage: are all `Select Case` arms reachable and correct? Any dead branches or missing `Case Else`?

### 4.2 Numerical / financial soundness
- Indicator math vs textbook definition: ROC(9), RSI(9), ATR(7), DMI/ADX(9), Bollinger/BBW, EMA ribbon, VWAP (note the **dual-session anchor at 00:00 and 13:30 UTC** — single-anchor is a known-bad pattern, verify the handoff logic), CVD weighting (late-period emphasis), MicroCVD segmentation, TFI, OFI, VPFR POC/HVN/LVN.
- Scoring pipeline integrity (analyze, don't change): additive +1/0/−1 voting, regime MaxScore ceiling (19/18/15), `Math.Ceiling(regimeMax × pct)` thresholds, the Pass-2b OI×CVD gate semantics, funding as adjunct (Step 3/3b, must **not** appear in Step 2), MTF gate hard veto.
- Kelly sizing: half-Kelly, 5% hard cap, $1,000 account, $10 contract face, suppression when `KellyF ≤ 0`. Verify the arithmetic and the suppression/cap branches. (Context: a prior review noted some Kelly arms may be unreachable under current sizing — `B11/B13/B14`. If you independently rediscover this, good signal; cite the math.)

### 4.3 Concurrency & async
- `Task.WhenAll` fan-out in the analysis run: any shared mutable state written concurrently? Ring buffers (`_oiHistory`, `_fundingHistory`, `_ofiHistory`) — are they touched off the UI thread safely?
- `async void` usage, unobserved exceptions, `.Result`/`.Wait()` deadlock risk on the UI thread.
- 15m candle cache (TTL=60s) — race between expiry check and refresh?

### 4.4 Resilience & error handling
- Deribit API failure modes: timeout, partial JSON, HTTP error, empty arrays. Does the engine degrade gracefully (ANALYSIS SKIPPED) or can it throw/render garbage?
- `HttpClient` lifecycle — reused or per-call (socket exhaustion risk)?
- CSV logging: can a malformed field or culture-sensitive number format (comma vs dot) corrupt a row? Check `CultureInfo.InvariantCulture` usage on parse/format.

### 4.5 Resource & performance
- Allocations in hot paths (per-run LINQ chains, string building in render).
- Any synchronous network or file I/O on the UI thread.
- Repeated recomputation that could be cached within a run.

### 4.6 Host-agnostic / portability
- Grep `analysis/` and `tools/` for `System.Windows.Forms` / `MainForm` / `Control.` references. Any hit is a finding.
- `tools/AutoTweaker/` builds as a separate `.csproj` — confirm it has zero WinForms coupling.

### 4.7 Config & magic numbers
- Hardcoded literals in `.vb` that affect behaviour and should be in `settings.json`. (Some calibration constants are deliberately inline — use judgment; flag the ones that look like they drifted.)

---

## 5. Scope: in vs out

**PRIMARY (audit deeply):**
- `Core/Indicators_*.vb`, `Core/ScoringEngine_*.vb`, `Core/Settings/EngineSettings.vb`
- `DeribitClient.vb`, `DynamicNorms.vb`, `SettingsLoader.vb`, `AnalysisLogger.vb`
- `analysis/*`, `tools/AutoTweaker/*` (host-agnostic compliance + logic)
- Data flow in `UI/MainForm_Analysis.vb` (the orchestration, not the rendering)

**OUT OF SCOPE (do not systematically audit):**
- The UI reskin card-binding / layout code (`UI/MainForm_Render_Cards.vb`, `UI/MainForm_Layout.vb`, `UI/MainForm_PlaintextSnapshot.vb`, the legacy `UI/MainForm_Render_*.vb`). **This is being actively reworked in other conversations** (a card-grid reskin nearing completion). Auditing it now collides with in-flight work.
- `MainForm.Designer.vb` — auto-generated, never reviewed.
- **Exception:** if you *stumble onto* a genuine crash, null-deref, or data-corruption bug in UI code while tracing a data-flow thread, note it in a separate "Incidental UI findings" appendix — but don't go looking.

**Scope toggle for the user:** if you'd rather Fable also audit the reskin UI, delete the "OUT OF SCOPE" restriction above before handing this brief over. Default is engine-only to avoid collision with the reskin work.

---

## 6. How to verify findings (build / run / test)

There is **no automated test suite.** Verification is manual. You have:

- **Build:** `dotnet build` against the solution (`DeribitVerdictEngine.sln`). Must be clean. Use this to sanity-check your mental model compiles.
- **Run:** `dotnet run` launches the WinForms app. Live Deribit data flows on **Analyze**.
- **Screenshot helpers** (in `tools/`, see `tools/README.md`): `screenshot-mainform-full.ps1` (full form), `click-mainform-button.ps1 ANALYZE` (trigger a run), `inspect-mainform-tree.ps1` (UIA tree dump). Use these only if a finding needs visual/runtime confirmation — most engine findings are static.
- **Scratch test harness (allowed, never committed):** if you want to prove an indicator-math or edge-case finding, you may write a throwaway script/console snippet **under `verify/`** (gitignored) that calls the relevant pure function with crafted inputs and prints results. Delete it or leave it in `verify/` (it won't be committed). Do **not** add a test project to the solution. Do **not** wire it into the build.

**Discipline:** when you claim a bug, say whether you *confirmed it by running* or *inferred it by reading*. Do not upgrade "I read the code and it looks wrong" into "I verified it fails." Label each finding `CONFIRMED (ran)` or `STATIC (read-only)`.

---

## 7. New-function / capability suggestions

This is the second half of the deliverable. The user wants ideas they can bring back and turn into spec proposals. Rules:

1. **Propose, don't build.** Each suggestion is a stub: name, one-line purpose, rough signature, where it'd live, and *why it's worth it*.
2. **Trader-profile alignment is mandatory.** Each suggestion must include a one-line "Profile check:" stating it does not reintroduce a rejected pattern (§4 of the profile) and which preferred indicator/workflow it serves. If a suggestion is genuinely in tension with the profile but you think the evidence justifies it, say so explicitly — flagged, not buried.
3. **Bias toward what the data/code is missing, not novelty for its own sake.** Good sources of ideas: gaps in resilience (e.g. a retry/backoff helper for Deribit calls), observability (e.g. a structured run-diff for calibration), correctness guards (e.g. an assertion that per-item scores sum to the total), host-agnostic test seams. The trader's stated bottleneck is **data, not UI** and **quality over quantity** — weight suggestions accordingly.
4. **Rank by value/effort.** A 3-line guard that prevents a class of silent corruption beats a speculative new indicator.
5. **No new indicators unless the profile's preferred list implies an obvious missing piece** and you make the case. The profile's rejected list is binding.

---

## 8. Deliverable format

Write everything to **`docs/fable5-audit-report.md`**. Structure:

```
# Fable 5 Engine Audit Report — <date>

## 0. Summary
- Files read: N. Findings: X confirmed / Y suspected / Z style. New-function ideas: M.
- One-paragraph headline: the most important 2–3 things the maintainer should look at first.

## 1. Confirmed bugs (CONFIRMED — ran, or STATIC with airtight reasoning)
| ID | Severity | File:line | What | Why it matters | Fix direction | Verified |
... one row per finding, plus a short prose block per HIGH/CRITICAL finding with the evidence.

## 2. Suspected issues (need maintainer judgment or data to confirm)
Same table shape. These are "this looks wrong but I couldn't fully confirm."

## 3. Resilience / concurrency / portability observations
Grouped notes. Include the host-agnostic grep result (clean or violations).

## 4. Config / magic-number observations

## 5. New-function / capability suggestions
Per suggestion: Name | Purpose | Signature sketch | Location | Value/effort | **Profile check** | Rationale.
Ranked best-first.

## 6. Dimensions with no findings
One line each (e.g. "4.3 Concurrency: reviewed Task.WhenAll fan-out, no shared-write races found"). Proves coverage.

## 7. Coverage & confidence
What you read, what you ran, what you didn't get to, and how confident you are per area.
```

Keep prose tight. Tables for findings, prose only where evidence needs explaining. The maintainer triages this the way a PR review gets triaged — make it scannable.

---

## 9. Severity definitions

- **CRITICAL** — wrong verdict/score/sizing output, data corruption, or crash on a realistic input. The trader could act on bad output.
- **HIGH** — incorrect behaviour on a plausible edge case (API failure, warmup, thin data) that doesn't crash but misleads.
- **MEDIUM** — correctness issue on an unlikely path, or fragility that will bite under maintenance.
- **LOW / STYLE** — clarity, dead code, naming, minor allocation. Group these; don't let them dominate.

Be honest about severity. Inflating a style nit to HIGH wastes the maintainer's triage time; burying a real CRITICAL under nitpicks is worse.

---

## 10. What to hand back

When done: the single file `docs/fable5-audit-report.md`, repo otherwise clean (`git status` shows only that one untracked file, plus anything left in gitignored `verify/`). The user copies the report back to the main conversation, where findings get triaged into fix specs and the function ideas get evaluated as proposals.

**Do not** start fixing, do not commit, do not push, do not modify `settings.json` or scoring code. Surface, rank, suggest. That's the job.

---

**End of brief.** Drop into a fresh Fable 5 conversation. Everything needed is here plus the four docs in §1.
