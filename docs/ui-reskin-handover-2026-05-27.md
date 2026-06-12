# UI Reskin — Spec Author Handover (2026-05-27)

**Generated:** 2026-05-27
**Supersedes:** `docs/ui-reskin-handover-2026-05-22.md` (still on disk for history; the locked-decisions deltas have all been folded into this doc).
**For:** Next spec-author conversation taking over after the screenshot-reliability-fixes spec ships.
**Purpose:** Capture everything needed to continue as spec-author through P5-test → gap-fix → P5-test cleanup → P5b → Spec C without re-deriving context.

This is a handover between spec-author conversations, not implementation conversations. The implementation conversations are short-lived, one per kickoff. The spec-author role has been continuous; this doc transfers it cleanly.

---

## 1. State of the project (one paragraph)

The DeribitVerdictEngine UI reskin is **~95% complete in card-grid terms** but in a deliberately-extended verification window before the final deletion sweep. P1 → P4f (theme infra, palette repaint, custom controls library, all card bindings) shipped through P4f's ANALYSIS SKIPPED render. P3 maintenance pass closed the tofu glyph + pruned unused controls. Spec B (VPFR histogram) wired the volume profile mini-histogram. **P5a** shipped `BuildPlaintextSnapshot` and rewired `AnalysisOutputDump` to feed from data rather than `txtOutput.Text` — `txtOutput` and the verification dump card are still alive for parity inspection. **Screenshot reliability spec** shipped popup-on-parent-monitor positioning + four UIA helpers, then a **fixes spec** corrected three runtime defects in commit 2 (Windows 11 foreground steal, natural-height computation, form-size restore). The next actionable phase is **P5-test** — a temporary parity-verification harness (kickoff already drafted) that bypasses `RunAnalysisAsync` and drives 40-60 synthesised state cases through both renderers in a tight loop. After P5-test surfaces gaps, a card-binding fix spec ships, then P5-test cleanup commit removes the harness, then **P5b** deletes the legacy RTF pipeline. **Spec C** (SC column / TOTAL parity) is post-P5b as Phase 6. The engine itself sits at settings.json v30 with the auto-tweaker awaiting first fixed-window fire — none of the reskin work has touched scoring code.

---

## 2. Session start protocol for the new conversation

Read in this order:

1. **`CLAUDE.md`** (project root) — collaboration rules, shell tips, workflow conventions.
2. **This handover doc.** Everything that follows references it.
3. **`docs/DeribitIndicatorProject.md`** §1-3 + §15 — engine state + recent shipments. Skip §4-14 unless you need it for a specific question.
4. **`docs/architecture.md`** — codebase structure + display-behaviour clarifications.
5. **`docs/ui-reskin-proposal.md`** rev 2.1 — only the relevant section for whatever phase you're about to draft. §3 (palette + width), §4 (card inventory), §5 (gap coverage), §6 (custom controls), §8 (phase plan), §10 R/Q (decisions log).
6. Load the **`crypto-trading-context` skill**.

**Don't read at session start:**
- Any `.vb` source file. Open only when a specific edit needs them.
- The 2026-05-22 handover. This doc supersedes it.
- The 5-6 fresh proposal / kickoff / spec-back docs unless you're picking up that specific phase.

**Read on-demand when drafting the next phase:**
- `docs/ui-reskin-p5-test-harness-kickoff.md` — already drafted; P5-test is the next actionable phase.
- `docs/ui-reskin-p5b-kickoff.md` — drafted; gated on P5-test cleanup.
- `docs/sc-column-total-parity-proposal.md` — drafted; Phase 6 after the reskin.
- The most recent spec-back to understand what the previous implementation conversation actually shipped vs. what its kickoff prescribed.

---

## 3. Roadmap — what's left

The verification window between P5a and P5b was deliberately structured as a five-step sequence so trader workflow stays intact while the legacy RTF surface deletes. Trader's visual review of the 55 PNGs surfaced ~30 card-binding gaps + format inconsistencies, which now route through a dedicated gap-fix spec before commit 3 cleanup ships. **Current position: step 3 (gap-fix proposal drafted, awaiting fresh implementation conversation).**

```
0. P5a (shipped)                                                       — BuildPlaintextSnapshot + calibration migration
1. Screenshot reliability spec (shipped) + fixes spec (shipped)        — capture pipeline now reliable
2. P5-test harness (shipped)                                           — ✅ 55/55 PARITY text-level (db1675c..f54607f)
3. Gap-fix proposal drafted → SHIP NEXT                                — docs/ui-reskin-p5-test-gap-fixes-proposal.md (~30 items across 4 cards)
4. Gap-fix implementation (fresh Opus 4.7 High conv) + cleanup         — 4 work commits + harness re-runs + trader sign-off + P5-test commit 3 cleanup
5. P5b deletion sweep (kickoff drafted)                                — delete legacy RTF pipeline + verification dump card
6. Spec C — SC column / TOTAL parity (proposal drafted)                — engine-touch phase, post-reskin
6a. Post-P5b engine hygiene proposal (TO DRAFT)                        — B11 dead-code deletion + B13/B14 code comments
6b. Adaptive stop invalidation proposal (TO DRAFT)                     — industry "worse-of" stops + crypto-perp ATR multiplier scaling
```

### Step 2 — P5-test harness ✅ SHIPPED

**Kickoff:** `docs/ui-reskin-p5-test-harness-kickoff.md`.
**Spec-back:** `docs/ui-reskin-p5-test-spec-back.md` (full) + `docs/ui-reskin-p5-test-report-back.md` (condensed).
**Model:** Opus 4.7 High.
**Result:** 55 cases × byte-identical legacy vs snapshot (CRLF-normalised). Per-case PNGs + .txt files in `verify/p5-test/`.
**Branch coverage:** ~92% (≈106 of ≈115 arms substantively exercised). Remaining 8% split into test-side gaps (§6) and Type B engine-side unreachables (§6).
**Confidence statement:** "no documented arm exists where the new design silently drops information that legacy renders." Trader's gating question answered.

### Step 3 — Discrepancy fix spec — NOT NEEDED

Zero discrepancies means no fix-spec scope. Skipped. The post-mortem in the spec-back's §3 lists ~8% uncovered arms; those are not discrepancies (both renderers agree), they're coverage gaps. Test-side gaps (§6 below) close in a small commit-2 follow-up; engine-side Type B unreachables defer to the post-P5b engine hygiene proposal (§3.6a).

### Step 3 — Gap-fix proposal (NEXT — DRAFTED)

**Proposal:** `docs/ui-reskin-p5-test-gap-fixes-proposal.md` (drafted 2026-05-27).
**Source:** trader's `docs/VisualReviewQuestions.txt` + `docs/ContentRequestsAfterVisualReview.txt` + coder's triage handoff `docs/ui-reskin-p5-test-visual-review-handoff.md` + spec-author defaults + trader-confirmed C1a research + new C1c.
**Scope:** ~30 card-binding gaps + format fixes across 4 cards (ATR ENTRY LEVELS, STRUCTURAL, INDICATOR DETAILS, SIGNAL BREAKDOWN, VOLUME PROFILE). Card-only changes — snapshot stays at legacy parity to preserve the 55/55 text-parity gate. §4 paint carve-out NOT invoked.
**Critical deferrals:**
- **Q3g + C6 (SC half)** — DMI/ADX card SC under-reporting defers to **Spec C**. The clamp at `BuildRowDmiAdx` is band-aid territory; Spec C's `LongPoints`/`ShortPoints` field migration is the architectural fix.
- **C1a stop capping** — research-backed decision: stops stay uncapped, dual-row display already provides industry "worse-of" information. Code comment documents the asymmetry.
- **Adaptive stop invalidation** — parked to a new proposal `docs/adaptive-stop-invalidation-proposal.md` (post-Spec-C). Industry buffer-sizing + crypto-perp volatility-regime multiplier scaling.
**Critical addition:**
- **C1c (NEW)** — display-only stop-deeper visual flag on the ATR row. When structural stop is further from entry than ATR stop, prepend the structural figure to the ATR row's STOP cell with a visual cue. Trader makes the worse-of decision without scanning down. Card-only this round; post-P5b backlog item adds snapshot equivalent when snapshot becomes canonical text.

### Step 4 — Gap-fix implementation + cleanup (gated on Step 3 proposal landing)

**Model:** Opus 4.7 High (~20-25 distinct binding edits; synthesis of trader's 8 formatting guidelines).
**Commit shape:** 4 work commits (ATR + STRUCTURAL / INDICATOR DETAILS / SIGNAL BREAKDOWN / VOLUME PROFILE) + 1 cleanup commit. Each work commit goes through the harness re-run gate (text parity 55/55 must hold). Trader visual sign-off is the closing gate before cleanup ships.

Cleanup commit deletes `UI/MainForm_TestHarness.vb` + `UI/TestHarnessCases.vb` + the `Ctrl+Shift+T` ElseIf branch + `tools/send-ctrl-shift-t.ps1`. No `_testHarnessMode` guards to remove — implementer found those weren't needed (handover §5.11).

### Step 5 — P5b deletion sweep

**Kickoff:** `docs/ui-reskin-p5b-kickoff.md`.
**Model:** Opus 4.7 Medium (mechanical deletions).
**Gated on:** trader's explicit "P5b ready" signal after P5-test cleanup. Implementation conversation refuses to run otherwise.
**Scope:** delete `MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb`, six `AppendRtf(txtOutput, ...)` calls in `MainForm_Analysis.vb`, the `RenderOutput` call, P/Invoke surface, `OUTPUT_CHARS` / `OUTPUT_LINES` / `SizeToContent`, verification dump card, `ReparentVerificationDumpControls`. Consolidate `BuildPlainSectionHeader` → `MakeSectionHeader`.
**Critical (§3.2.2 of P5b kickoff):** must hoist `ScoringEngine.CalcKellySizing` into `RunAnalysisAsync` between `Calculate(...)` and `BindCardKelly(...)` — `BindCardKelly` currently reads `v.Kelly*` fields populated as a side effect of `RenderOutputHeader`, which P5b deletes. Without the hoist, KELLY card renders zeros for one cycle.
**Designer carve-out:** `MainForm.Designer.vb`'s `txtOutput` field declaration stays — only the visible/written state changes. The field becomes a zombie (declared but invisible, no writers, no readers).

### Step 6 — Spec C (post-P5b, Phase 6)

**Proposal:** `docs/sc-column-total-parity-proposal.md`.
**Model:** Opus 4.7 High (23 emission sites in `Core/ScoringEngine_Calculate_Scoring.vb`).
**Scope:** add `LongPoints` + `ShortPoints` integer fields to `SignalBreakdownItem`; every emission site captures the actual `state.LongScore` / `state.ShortScore` delta (cap-applied) when it fires; `ScForItem` returns `LongPoints - ShortPoints` instead of hit-derived ±1; debug assertion compares per-item sum to `v.LongScore` / `v.ShortScore`.
**Critical:** scoring math must stay bit-identical — CSV regression check via diff of pre/post `v.LongScore` / `v.ShortScore` / `v.MaxScore` columns is the load-bearing verification gate.

### Step 6b — Adaptive stop invalidation proposal (TO DRAFT)

**Proposal:** `docs/adaptive-stop-invalidation-proposal.md` — not yet drafted.
**Trigger:** post-Spec-C (or alongside Spec C if scope dynamics allow — leans toward post).
**Discuss-before-implementation flag:** trader-aware — open question on scope alignment with Spec C.
**Anticipated scope:**
- Industry "worse-of(structural ± buffer, ATR floor)" stop pattern as the executable stop calculation. Currently the engine emits ATR and structural separately; trader executes by manually picking. C1c (gap-fix spec) surfaces the choice on the card visually. This proposal makes the engine the source of truth.
- **Buffer-sizing convention research** — open question. The structural ± ATR buffer is per-source ambiguous; needs deeper research across momentum-scalper literature.
- **Crypto-perp volatility-regime multiplier scaling** — adapt ATR multiplier to volatility regime (tighter when calm, wider on spikes / pre-news). Per QuantVPS / luxalgo / gomarkets sources cited in spec-author C1a response 2026-05-27.
- Calibration runs against your CSV history to validate the worse-of rule doesn't introduce more whipsaws than it prevents.
**Engine work** — separate from card UI. Independent of Spec C's `LongPoints`/`ShortPoints` migration but may share calibration tooling.

### Step 6a — Post-P5b engine hygiene proposal (TO DRAFT)

**Proposal:** `docs/post-p5b-engine-hygiene-proposal.md` — not yet drafted.
**Trigger:** post-P5b. Not blocking anything.
**Discuss-before-implementation flag:** **trader explicitly wants to review the details with spec-author before any implementation kickoff drafts.** Don't ship a kickoff for this without confirming scope first.
**Anticipated scope (from P5-test §3.2 Type B unreachables):**
- **B11 — KELLY suppression dead-code deletion.** `CalcKellySizing` early-exits when `v.Verdict ∈ {NEUTRAL, WAIT, ""}` (`ScoringEngine_Kelly.vb:44–45`). Engine emits only from `{STRONG LONG, LONG, WEAK LONG, NO TRADE, WEAK SHORT, SHORT, STRONG SHORT}`. Spec must start with a confirming grep across `Core/ScoringEngine_*.vb` for those literal verdict strings before deletion lands — don't trust the assertion blind.
- **B13 — KELLY always-capped documentation.** With current sizing (`MaxRiskFraction=0.05`, `UseHalfKelly=True`, `EstProbFloor=0.45`, `EstProbScale=0.20`), minimum `fHalf` across all confidence tiers is ~0.06 → always over the 0.05 cap. Add an inline code comment noting the deterministic-cap property and the config knob that would change it.
- **B14a/B14b — KELLY Contracts ≥ 1 / Lean ≥ 1 documentation.** At `$1k` account + BTC ATR `~$80`, `riskPerContract` mathematically exceeds `KellyRiskUsd` → always `< 1 contract`. Not dead code (reachable at larger accounts), but production-unreachable with current sizing. Same code-comment treatment.

Total scope: small. One delete, three comments. Drafting waits for trader sign-off on scope.

---

## 4. Locked architectural decisions

These have been settled across earlier conversations and **must not be re-litigated** without new data or a concrete technical reason. Full table — supersedes the 2026-05-22 version:

| Decision | Locked in | Source |
|---|---|---|
| Single dark theme, no theme switching | rev 2 §2 | Q3 follow-up |
| Hard width ceiling 1280 px (= 3840 / 3) | rev 2.1 §3.6 | Q2 |
| Vertical unconstrained, scrolling acceptable | rev 2.1 §3.6 | implicit |
| Geist Mono bundled + fallback chain | P1 kickoff | locked |
| Palette tokens at design hex values | P2 kickoff | locked |
| **14 custom controls in `UI/Controls/`** — no new controls and no API surface changes. **Paint carve-out:** pure paint-style tweaks (font size, ForeColor, border placement) inside an existing control are allowed when card-grid consistency demands it (e.g. `SectionGroup` title bump in `bb7cd57`). No consumer code touched, no constructor / property surface changes. | P3 kickoff + P3 maintenance pass + paint carve-out `bb7cd57` | hard rule (paint carve-out) |
| Card-based grid replaces RichTextBox rendering | P4a kickoff | locked |
| Legacy `txtOutput` stays parked through P5a, deletes in P5b | rev 2 §10 R1 + user follow-up + P5 split 2026-05-25 | locked |
| Hybrid A + selective B — DIAGNOSTICS card for verbose data, NOTE column for single-value enrichments | gap audit | locked |
| **Card name: INDICATOR DETAILS** (not "DYNAMIC NORMS" / "DIAGNOSTICS" / "REFERENCE") | gap audit | user choice |
| **No `[L]/[S]` columns in cards** — STATE pill + SC encode direction | gap audit GAP-86 | user choice |
| **MicroCVD restored to 4-state colour distinction** (BULL_ACCEL / BULL_DECEL / BEAR_ACCEL / BEAR_DECEL / FLAT) | gap audit GAP-82 | user choice |
| STATE derivation for RSI(9), DMI/ADX, BBW/TTM uses `SignalBreakdownItem.LongHit/ShortHit` | P4c state-fix | locked |
| Score arc value = `max(EffectiveLongScore, EffectiveShortScore)` | P4c review §1 | locked |
| ATR card renders BOTH directions (Long + Short) | P4b retro-fix GAP-06 | user Q1 |
| KELLY card eff/penalty rows live in VERDICT card, not SCORE card | P4c review §4a | locked |
| ChipNumeric vs Designer NUDs: keep Designer NUDs for now (backlog) | P4c review §5 | accept |
| FlatButton vs Designer Buttons: keep Designer Buttons for now (backlog) | P4c review §5 | accept |
| `lblVerdict` hidden in P4, deleted in P5b | stabilisation | locked |
| Designer NumericUpDown spinner arrows visible — accepted | P4a review | accept |
| Hybrid A architecture name: "INDICATOR DETAILS" card with 12 inline groups | gap audit + P4d | locked |
| `SectionGroup` — **per-instance** title colour/font overrides still require inline composition. Global default style changes (e.g. `bb7cd57` 9pt→11pt bump) fall under the §4 paint carve-out. | P4d spec-back §2.3 + carve-out 2026-05-24 | locked (scoped) |
| `BuildGroupInline` helper local to `MainForm_Render_Cards.vb`, not promoted to P3 control | P4d spec-back §2.3 | locked |
| Pass 2c CONFLICT case = `LongHit=False AND ShortHit=False AND note prefix matches CONFLICT` | P4d spec-back §5.1 | locked (fixed) |
| Form width: **1100 px** (P4a settled at the floor of the rev 2.1 range) | P4a fix `3298ccf` | accept |
| REPEAT/SINGLE mode display: `Pill` chip mirroring `rbRepeat`/`rbSingle` radios (radios stay the source of truth). `SegmentedToggle` wrapper retired. | P4e + P3 maintenance pass | locked |
| **VERDICT hero text 18pt bold (not 22pt/28pt)** for normal rendering. 28pt is allowed for ANALYSIS SKIPPED state only (no 2×2 sub-grid below in that state). Do NOT re-propose 22-28pt for normal-render without new evidence. | P3 maintenance pass spec-back §4.3 + `12dd54f` | locked |
| ANALYSIS SKIPPED hero exception — 28pt bold + glow | P4f kickoff §3.1 + `c8ebac6` | locked exception |
| **Stale-card overlays occlude, not dim.** WinForms doesn't composite sibling child controls. Trader workflow during skipped state validated against this visual; do not re-propose label-ForeColor dimming. | P4f spec-back §1.1 + user accept 2026-05-25 | locked (occlusion is the design) |
| **Self-screenshot is the default verification path** for both implementation and spec-author conversations. `tools/screenshot-mainform-full.ps1` (post-fixes) is the primary capture. UIA + companion helpers for state manipulation. Sessions without GUI fall back to legacy user-screenshot. | P4f spec-back §1.3 + screenshot-reliability fixes 2026-05-27 | locked workflow |
| **ATR row stops stay uncapped** — research-backed (industry "worse-of" pattern shows BOTH ATR and structural; trader picks deeper). Capping the ATR row would break position-sizing decoupling (`Base × AvgATR/CurrATR` reads `atrStop`), duplicate the structural row, and mislead the ATR-row R:R. CAPPED indicator is targets-only. Future hybrid-stop work tracked separately. | gap-fix proposal §4.2 + spec-author C1a research 2026-05-27 | locked |
| **C1c display-only worse-of flag** — when structural stop is further from entry than ATR stop, prefix the structural figure to the ATR row's STOP cell so trader can decide which stop to execute against without scanning the structural row beneath. Card-only this round; snapshot equivalent backlogged to post-P5b polish. No engine work (both stop figures already computed). Symmetric across LONG and SHORT. Sizing math unchanged (`atrStop` continues to drive position sizing). | gap-fix proposal §4.4 + trader collaboration 2026-05-27 | locked |
| **DMI/ADX SC under-reporting defers to Spec C** — `BuildRowDmiAdx` clamps the combined DMI + ADX score to `[-1, +1]`; engine emits two separate +1 items contributing +2. Fix architecture lives in Spec C's `LongPoints`/`ShortPoints` field migration. Gap-fix spec documents the deferral but does not band-aid the clamp. C6's NOTE-column format expansion (`ADX=32.5 \| +DI=28.0 \| -DI=14.0`) stays in the gap-fix spec — only the SC accuracy half defers. | gap-fix proposal §2.2 | locked |
| **MTF Gate absence from card SIGNAL BREAKDOWN is by design** — MTF is a hard veto, not a +1/-1 scoring contributor. Card breakdown intentionally omits the row; MTF lives in INDICATOR DETAILS as its own sub-section. Snapshot retains the breakdown row for legacy parity; SC always `—`. Code comment in `BindCardSignalBreakdown` documents the design. | gap-fix proposal §7.4 + trader Q3l confirmation 2026-05-27 | locked |
| **VPFR card pill encodes indicator state, not scoring contribution** — `BuildRowVpfr` maps `r.VPFRSignal = "NEAR_HVN_SUPPORT"` → `BULL` directional pill, while SC column shows the scoring hit (which may be `0`/`NEUTRAL` if the signal didn't fire as a +1 emission). Pill ≠ SC is intentional. Code comment documents the design. Spec C makes SC more granular but does not change the pill-vs-SC distinction. | gap-fix proposal §7.3 + spec-author Q3k default 2026-05-27 | locked |
| **SIGNAL BREAKDOWN NOTE column stays lowercase/brevity-style** — G7 capitalisation applies to the VOLUME PROFILE card labels (C5a/C5b: `Near HVN support`, `Inside VA`) but NOT to breakdown NOTE cells (`none`, `accelerating`, `upper qtr`, `inside va`, …). Review finding F-02 triaged ACCEPT AS-IS — zero trading value in 17 string edits. Do not re-propose. | consolidated-fix kickoff triage 2026-06-12 (review F-02) | locked |
| **`Near HVN resist` renders ACC_SHORT red, not the C5a-spec'd ACC_WARN amber** — direction-consistent colour beats the spec'd amber; amber stays reserved for warnings/CAPPED. Mirrors: `Near HVN support` green, `In LVN bull/bear` green/red, `Above VAH` green, `Below VAL` red. | consolidated-fix kickoff triage 2026-06-12 (review F-06) | locked (blessed deviation) |
| **Context-tag colour severity mapping** — `MOMENTUM_FADING` amber, `STRUCTURALLY_WEAK` red, `CONFIRMED` green on both verdict sides. Intended severity scale, not a side-colour. | consolidated-fix kickoff triage 2026-06-12 (review OBS-E) | locked |

### 4.1 P3-touching specs — required tick-box gate

Any future spec that proposes changes to `UI/Controls/*.vb` MUST satisfy all four checks in the kickoff before invoking the §4 paint carve-out:

- [ ] **Cites the §4 carve-out language verbatim.**
- [ ] **Confirms paint-only.** No new public properties, no new events, no constructor / method signature changes. Allowed: font size, `ForeColor`, border placement, corner radius, internal padding values.
- [ ] **Confirms no consumer code changes.**
- [ ] **Confirms no new or deleted controls.** New / deletions are structural; require a maintenance-pass spec.

If any check is "no," the spec is a **structural P3 change** and needs a fresh locked-decision discussion. Implementation conversations have standing authority to refuse a kickoff that invokes the carve-out without satisfying all four checks.

---

## 5. Process lessons (apply to all future kickoffs)

Synthesised from spec-backs across the whole reskin. Each lesson saves measurable time in the next kickoff cycle.

### 5.1 Real type names (lesson from P4d spec-back §8.1)

Quote enum / property / field names from the actual source files, not from memory or pseudocode convention. The codebase uses VB `SCREAMING_SNAKE` enums and `snake_case` JSON properties (e.g., `OiCvdBadge.OiCvdOutcomeKind.CONFIRMED_LONG`, not `OiCvdOutcome.ConfirmedLong`; `WideThresholdBps`, not `WidePenaltyThresholdBps`). **Before quoting any enum / property in a kickoff, grep the target file for the exact identifier.**

### 5.2 Helper inventory (lesson from P4d spec-back §8.2)

Each kickoff's "What you inherit" section should grep `MainForm_Render_Cards.vb` (or equivalent) and enumerate the existing helpers the implementation conversation can reuse. Saves the implementer from declaring helpers that already exist.

### 5.3 Build / screenshot / measure gate (lesson from P4d spec-back §8.3)

Card row heights are absolute pixels; under-sized rows silently clip lower content with no build error. Each kickoff card-binding step should explicitly require: build, run, screenshot, measure clipping. Helpers exist (`tools/screenshot-mainform-full.ps1`).

### 5.4 Engine emission semantics audit (lesson from P4d spec-back §8.4)

Before consuming `SignalBreakdownItem.LongHit / ShortHit` (or any engine emission), tabulate every permutation the engine emits. Read the emission site in `Core/ScoringEngine_Calculate_Scoring.vb`. The Pass 2c CONFLICT shape (both False) was non-obvious; P4c shipped a dead-code branch as a result.

### 5.5 NICE-severity gaps need explicit per-commit ship / skip lists (lesson from P4d spec-back §8.5)

Per-commit specs should enumerate every gap touched (ship) and every gap explicitly skipped, with rationale. GAP-72/73 fell between P4d commits 3 and 4 because of this.

### 5.6 Don't over-specify field-level pseudocode (lesson from P4c)

Once the agent has the per-indicator mapping table, the binding code writes itself. Heavy `Public Sub BindCardXxx` skeletons in the kickoff create drift risk. Mapping table + helper inventory + gap reference are usually enough.

### 5.7 Worst-case-string-length budget (lesson from P3 maintenance pass spec-back §7.2)

Card pixel budgets like the hero row's 160 → 180 px and the verdict label's 22pt → 18pt implicitly assumed median-case content. Add a one-line check to future card kickoffs: "What's the longest plausible string for the dominant headline label?"

### 5.8 Self-screenshot is the default (lesson from P4f spec-back §1.3 + screenshot-reliability fixes 2026-05-27)

Implementation AND spec-author audit conversations run the `tools/` helpers before reporting visual findings. `tools/screenshot-mainform-full.ps1` captures the entire form regardless of display height. `tools/select-mainform-radio.ps1` toggles posState. `tools/close-popup-window.ps1` dismisses popups. The user is the second-pass reviewer for substance + mental-model match; first-pass visual issues belong to the conversation that wrote or audited the code.

### 5.9 Windows 11 foreground steal (lesson from screenshot-reliability-fixes 2026-05-27)

Plain `SetForegroundWindow` is silently blocked. Any helper that needs to send keys to MainForm must use the `AttachThreadInput` foreground-steal pattern (codified in `tools/screenshot-mainform-full.ps1` post-fix). If a future tool needs the same block, consider extracting to a shared `tools/lib/foreground-steal.ps1` — out of scope for the current spec but worth doing when the second consumer appears.

### 5.10 `Me.Size` is clamped by Windows `SystemMaximumSize` (lesson from screenshot-reliability-fixes 2026-05-27)

Setting `Me.MaximumSize = Size.Empty` does NOT lift the Windows-imposed `SystemMaximumSize` clamp (≈ screen working area). To render content past the working area, capture a child control directly via `Control.DrawToBitmap` rather than expanding the form. Pattern: temporarily undock the child, resize to natural extent, draw, restore.

### 5.11 Kickoff staleness audit (lesson from P5-test spec-back 2026-05-27)

Even a 2-day-old kickoff drifts. The P5-test implementer found three discoveries the kickoff didn't anticipate:

1. **Side-effect collaborator call-site migration.** `AnalysisLogger.LogRun` / `LivePerformanceTracker.UpdateAsync` / `AnalysisOutputDump.Append` had moved from `MainForm_Render_Sections.vb` into `RunAnalysisAsync` during P5a. Harness bypasses `RunAnalysisAsync` entirely, so the `_testHarnessMode` guard infrastructure planned in kickoff §1.2 wasn't needed at all — simplified scaffolding.
2. **Renderer signature drift.** `BuildPlaintextSnapshot` ended up with 6 args (`vwapWarmup` + `lastTradePrice` added in P5a) versus the 4 the kickoff §3.3 example showed.
3. **Inline recompute behaviour.** Both renderers call `ScoringEngine.CalcKellySizing(v, atrStop, cfg)` mid-render, so anything the harness writes to `v.Kelly*` gets overwritten. Idempotent in practice (parity preserved), but worth knowing if future fixtures need synthetic Kelly values.

**Rule:** future kickoffs should include an explicit "verify against current tree before commit 1" step that re-greps the collaborator call sites, the renderer signatures, and any helper the kickoff names. The spec-author's pre-kickoff audit catches some drift (handover §5.8) but cannot catch everything between draft date and implementation date. The 2-minute audit cost is much smaller than a wrong-shape scaffolding commit.

### 5.12 Trivial parity is not parity (lesson from P5-test 2026-05-27)

A test case that builds `VerdictResult` directly (`.WithVerdict(...)` setting `LongScore` / `ShortScore` only) leaves engine-emitted collections (`SignalBreakdown`, etc.) empty. The renderer then walks its fixed roster, finds no matching items, and writes blank rows + `—` notes consistently on both sides — `✅ PARITY` that exercises nothing.

For any harness or fixture that synthesises `VerdictResult` outside `ScoringEngine.Calculate(...)`, audit every collection / derived field that production fills via a side-effecting helper. Each one needs an explicit fluent setter in the builder (`.WithBreakdownItem(label, longHit, shortHit, note)`, `.WithAtrCapped(...)`, etc.) and explicit per-case population. Sentinel verification should confirm at least one sentinel exercises the populated path, not just the empty default.

The harness's `WithBreakdownItem` pattern (P5-test commit `303ba51`) is the precedent — model future synthesised-state builders the same way.

### 5.13 Research-backed decisions on engine policy (lesson from C1a stop-capping 2026-05-27)

When the trader asks an engine-policy question framed as "should we do X?" (e.g., "should we cap stops at structural levels?"), don't answer from first principles or assistant priors. Do the research first — `WebSearch` on the specific practice in the trader's style segment (momentum scalper, crypto perps, 1m/5m execution), surface the industry consensus, then map it to the engine's current architecture.

The C1a decision (stops uncapped, dual-row display preserved) was load-bearing because the wrong answer would have driven engine work that breaks position-sizing decoupling. The research established the industry "worse-of(structural ± buffer, ATR floor)" pattern, which then mapped cleanly to the trader's existing workflow (manual worse-of selection from the dual-row display). The display-only C1c flag captured the worse-of decision at the UI layer without engine work.

**Process rule:** for any engine-policy question, the spec-author's response includes (a) cited research, (b) explicit mapping to the engine's current architecture, (c) a recommendation backed by the research, not by intuition. The trader makes the call from the research, not from the spec-author's confidence.

---

## 6. Outstanding decisions (none critical)

Tracked but deferred items. Listed for the next spec author so they don't slip:

| Item | Default if not addressed | Locked decision needed? |
|---|---|---|
| `MakeSectionHeader` / `BuildPlainSectionHeader` consolidation | Bundle into P5b cleanup commit per handover | scheduled |
| Kickoff template: worst-case-string-length budget | Fold into next card kickoff as a one-line check | handled in §5.7 |
| **Gap-fix proposal (ACTIVE)** — `docs/ui-reskin-p5-test-gap-fixes-proposal.md` drafted 2026-05-27. ~30 card-binding gaps + format fixes from trader's visual review. 4 work commits + cleanup. Card-only changes, snapshot stays at legacy parity. §4 paint carve-out NOT invoked. Awaits fresh Opus 4.7 High implementation conversation. | Ship next implementation kickoff | scheduled (NEXT) |
| **C1c snapshot equivalent** — gap-fix proposal §4.4 lands the stop-deeper visual flag on the card only. Post-P5b polish round should add the equivalent text variant to `BuildPlaintextSnapshot` when it becomes the canonical text output. | Fold into post-P5b polish round | parked |
| **SC column / TOTAL parity (Spec C)** — proposal drafted at `docs/sc-column-total-parity-proposal.md`. Engine emits direction-only via `LongHit`/`ShortHit`; SC displays direction, TOTAL accumulates magnitudes. Hand-tallying SC doesn't sum to TOTAL when BBW / MicroCVD / Pass 2c / funding penalties fire. **Deferred to post-P5b as Phase 6.** Workaround during P5a window: legacy txtOutput's NOTE column shows penalty magnitudes inline. Now also absorbs the DMI/ADX SC under-reporting half of P5-test gap Q3g + C6. | post-P5b (Phase 6) | scheduled |
| **Adaptive stop invalidation proposal** — to draft post-Spec-C. Industry "worse-of(structural ± buffer, ATR floor)" pattern + crypto-perp volatility-regime ATR multiplier scaling. Engine work, trader-confirmed scope. **Discuss-before-implementation flag set.** | Drafts post-Spec-C; trader reviews scope before kickoff | scheduled (gated) |
| **P5-test test-side gap B6 — `NeutralVerdict.HoldStatus` default** — shipped via `f54607f`. | n/a — shipped | resolved |
| **P5-test test-side gaps B2 + B19 — ACCEPTED.** B2 empty `VerdictContext` is defensive code (production never emits); B19 VWAP s2 anchor is formatter-symmetric (single observation sufficient). No action. | n/a — accepted | resolved |
| **P5-test Type B unreachables — folded into post-P5b engine hygiene proposal (handover §3.6a).** B11 dead-code deletion + B13/B14 documentation comments. **Discuss-before-implementation flag set — trader wants scope sign-off before drafting kickoff.** | Drafts post-P5b; needs trader review of scope before kickoff lands | scheduled (gated) |
| Output Dump shell-launch placement (multi-monitor) — `Process.Start(dumpPath)` opens with OS-handler placement, not Engine-controlled | Track as parked observation; consider replacing with internal `AnalysisReportForm` viewer if multi-monitor friction surfaces in practice | parked |
| Marker-file IPC working-directory assumption — PowerShell helper assumes `bin/Debug/net8.0-windows/` is the app's working dir. Release / packaged builds may need derivation from process executable path. | Track for when P5-test harness consumes the helper in a more varied launch context | parked |
| Calibration Report viewer post-P5b — currently reused `AnalysisReportForm`; if friction surfaces (form title, sizing, close behaviour), polish spec lands separately | parked | not blocking |
| LiveQuant scripts that read `analysis_output_dump.md` — pre/post P5b diff check ensures shape parity | flagged in P5b kickoff §6.3 | covered |

---

## 7. Gap checklist status snapshot

**Source file:** `ui-reskin-p4-gap-checklist.md` — **worktree-only, NOT at `docs/` root.** Find via `find . -path '*worktrees*' -name 'ui-reskin-p4-gap-checklist.md'`. The snapshot below is the abbreviated status; the full 86-row checklist lives in the worktree. For active work (P5-test harness generates its own discrepancy report at runtime) the full checklist isn't needed — this summary is sufficient.

86 atomic gaps total.

**Shipped (84/86):**
- GAP-01..06: SCORE / LAST PRICE / ATR retro-fixes ✅ (P4b + P4c)
- GAP-07..16: KELLY card expansion ✅ (P4d commit 2)
- GAP-15..51: NORMS / REGIME / MTF / VWAP / BBW/TTM / EMA / Funding / OI / MicroCVD / Liq / Volume / Trend Structure detail ✅ (P4d commit 4 — INDICATOR DETAILS)
- GAP-52..68: SIGNAL BREAKDOWN NOTE enrichments ✅ (P4c)
- GAP-69..73: ORDER FLOW detail ✅ (P4c + P4e commit 0)
- GAP-74: Spread status in MiniMeter ✅ (P4e commit 0)
- GAP-75..82: CVD / TFI / MicroCVD / Liq detail ✅ (P4d INDICATOR DETAILS)
- GAP-83..86: Signal breakdown TOTAL, MicroCVD 4-state, hit-marks dropped ✅ (P4c)
- VOLUME PROFILE histogram (proposal §4.7.2) ✅ (Spec B)

**Pending — surface during P5-test harness:**
- Anything the harness finds. Scope unknown until harness runs.

---

## 8. File reference index

### Specs (read these for context)

- `docs/ui-reskin-proposal.md` rev 2.1 — master design spec
- This handover doc — supersedes 2026-05-22 version
- `docs/sc-column-total-parity-proposal.md` — Spec C, post-P5b

### Active kickoffs / proposals (drafted, awaiting trigger or in flight)

- `docs/ui-reskin-p5-test-gap-fixes-proposal.md` — **NEXT actionable** — fresh Opus 4.7 High implementation conv
- `docs/ui-reskin-p5b-kickoff.md` — gated on gap-fix work + cleanup
- `docs/sc-column-total-parity-proposal.md` — Phase 6, post-P5b
- `docs/ui-reskin-p5-test-harness-kickoff.md` — superseded (harness shipped); kept for archaeology
- `docs/ui-reskin-p5-test-visual-review-handoff.md` — superseded by gap-fix proposal; kept for triage reference
- `docs/VisualReviewQuestions.txt` — trader's raw visual review notes (source for gap-fix proposal)
- `docs/ContentRequestsAfterVisualReview.txt` — trader's format guidelines (binding constraints in gap-fix proposal §1)

### Recently shipped specs (most recent first)

- `docs/screenshot-reliability-fixes-kickoff.md` + spec-back — `20f1a0b` (2026-05-27): foreground steal + grid-resize capture + form-size dissolution
- `docs/screenshot-reliability-kickoff.md` + spec-back — `4a9781e` + `65dd6e7` + `aaf12e9` (2026-05-27): popup positioning + 4 UIA helpers
- `docs/ui-reskin-p5a-kickoff.md` + spec-back — `bcfdfd7` + `f707165` + `07c4ad2` (2026-05-26): `BuildPlaintextSnapshot` + calibration migration
- `docs/ui-reskin-p4f-kickoff.md` + spec-back — `3c72cec` + `c8ebac6` + `5f724ad` + `5b65ac2` (2026-05-25): ANALYSIS SKIPPED state + first screenshot helper
- `docs/vpfr-buckets-histogram-{proposal,kickoff,spec-back}.md` — `ae61be8` + `94bb78a` + `b77703d` + `bc83f06` (2026-05-24): Spec B VPFR histogram
- `docs/p3-maintenance-pass-{proposal,spec-back}.md` — `ba52994` + `be7f64b` + `12dd54f` + `bb7cd57` + `f54bdad` + `76d76c2` (2026-05-24): 📊 tofu + control prune + section header unification + paint carve-out
- `docs/ui-reskin-p4e-{kickoff,spec-back}.md` — `f7ec66e` + `045cb18` + `34564ee` + `cf07d03` + `85ed635` (2026-05-23): SETTINGS & TOOLS section

### Previous kickoffs (templates for structure — worktree-only, NOT promoted to docs/ root)

These early-phase kickoffs were written into the worktree at the time they were drafted and were never copied to the project's `docs/` root. They live at `.claude/worktrees/eloquent-aryabhata-bfee65/docs/` (path may differ across sessions — find via `find . -path '*worktrees*' -name 'ui-reskin-p[1-4]*-kickoff.md'`). The next spec-author conversation needs them only as templates for structural reference; the active work (P5-test, P5b, Spec C) doesn't depend on them.

- `ui-reskin-p1-kickoff.md` — theme infra (Geist Mono + palette tokens)
- `ui-reskin-p2-kickoff.md` — palette repaint
- `ui-reskin-p3-kickoff.md` — custom controls library
- `ui-reskin-p4-kickoff.md` — P4a + P4b foundation (card grid skeleton + top-half binding)
- `ui-reskin-p4-stabilisation-kickoff.md` — crash fix + perf strip
- `ui-reskin-p4-perf-strip-active-highlight-kickoff.md` — active session highlight
- `ui-reskin-p4c-kickoff.md` — SIGNAL BREAKDOWN binding
- `ui-reskin-p4d-kickoff.md` — KELLY + VOLUME PROFILE + OI×CVD CROSS + INDICATOR DETAILS

If a future kickoff needs to mimic one of their structures, copy the relevant file from the worktree at that time (or read via `git show`-equivalent of the worktree's HEAD). No need to promote eagerly.

### Implementation files (open on demand only)

- `UI/MainForm_Layout.vb` — `BuildCardGridLayout`, all card containers, perf strip, position radios, auto-run controls, popup-positioning helper, hotkey handler, `SaveFullFormScreenshot`, `ComputeGridNaturalHeight`. Owns `ApplyInitialFormSize`.
- `UI/MainForm_Render_Cards.vb` — all `BindCardXxx` methods, inline group builders, `MakeSignalRow`, `MakeSectionHeader`, `BuildPlainSectionHeader` (deletes in P5b).
- `UI/MainForm_PlaintextSnapshot.vb` — `BuildPlaintextSnapshot` builder (P5a, ~450 LoC). One top-level dispatcher + 13 private `AppendXxx` helpers.
- `UI/MainForm_Calibration.vb` — `BuildCalibrationReport` (P5a migrated; was in `Render_Header.vb`).
- `UI/MainForm_Analysis.vb` — `RunAnalysisAsync`, data-flow entry. `AnalysisOutputDump.Append` call site post-P5a.
- `UI/MainForm_Render_Header.vb` — **272 lines as of P5a**, just RTF helpers (`AppendRtf`, `AR`, `SectionHeader`, `Divider`) + `RenderOutputHeader`. P5b deletes the whole file.
- `UI/MainForm_Render_Sections.vb` — `RenderOutput` + indicator sections. P5b deletes.
- `UI/Theme/Theme.vb` — palette + `FontMono` factory. Do not modify.
- `UI/Controls/` — 14 custom controls + `Helpers/PaintHelpers.vb`. Do not modify (paint carve-out exception per §4).
- `Core/IndicatorResults.vb`, `Core/ScoringEngine_Types.vb` — read-only references.

### Tools (dev verification helpers)

- `tools/screenshot-mainform.ps1` — Win32 PrintWindow capture (visible-only)
- `tools/screenshot-mainform-full.ps1` — Full-form via `Ctrl+Shift+S` hotkey + `DrawToBitmap`. **Post-fixes uses AttachThreadInput foreground steal — works first try.**
- `tools/click-mainform-button.ps1` — UIA `InvokePattern` on buttons
- `tools/select-mainform-radio.ps1` — UIA `SelectionItemPattern.Select` on radios (toggle posState)
- `tools/close-popup-window.ps1` — UIA `WindowPattern.Close` on non-MainForm windows
- `tools/inspect-mainform-tree.ps1` — UIA element tree dump with regex filter
- `tools/resize-mainform.ps1` — `SetWindowPos` wrapper (legacy fallback)
- `tools/README.md` — workflow doc + script index. **Already updated to reflect all 7 helpers.**
- `verify/` — gitignored output directory for captured PNGs + diff files

### Engine docs

- `CLAUDE.md` — project root, collaboration rules
- `docs/DeribitIndicatorProject.md` — engine handover
- `docs/architecture.md` — codebase structure + display behaviour notes
- `docs/history-archive.md` — pre-v27 settings rationale, full version history (rarely needed)

---

## 9. Workflow conventions

Carried forward + extended through 2026-05-27.

1. **Local commits only.** User pushes after testing. Never push from any conversation.
2. **No `--no-verify` on commits, no force-push, no destructive git ops.**
3. **Designer.vb is untouchable.** All overrides programmatic. Even in P5b, the `txtOutput` field declaration stays.
4. **Spec-first.** Novel features get a proposal doc before coding. Small fixes can go straight to kickoff.
5. **Settings.json version is strictly monotonic.** No reskin work touches settings.json — engine config stays at v30.
6. **Fresh Opus conversation per phase kickoff — use whichever Opus version is current.** Spec author stays continuous across phases via handover docs like this one. Implementation conversations are one-shot per kickoff. Model-version references throughout these docs (e.g., "Opus 4.7 High") drift; treat them as "current Opus at the named effort level," not as hard pins. Mid-stream model swaps within a single implementation conversation are safer at commit boundaries than mid-commit — the spec docs and tool-call transcript carry the state, but the swapping model has to reconstruct unwritten plans from scrollback.
7. **High effort reserved for synthesis-heavy work.** Historical pattern: P3, P5a, P5-test, gap-fix, Spec C = High. P4a/b/c/d/e/f, P4-test, Spec A/B, screenshot fixes = Medium. P5b, post-P5b engine hygiene = Medium (mechanical). Use this as a rough guide for future phases of comparable scope.
8. **Push gate is user-side.** User tests the running app, then pushes.
9. **Self-screenshot is the default verification path.** Both implementation AND spec-author audit conversations use `tools/` helpers before reporting visual findings. Loop: launch → `screenshot-mainform-full.ps1` → `click-mainform-button.ps1 ANALYZE` → second screenshot → `inspect-mainform-tree.ps1 -Pattern …` for off-screen → kill. See `tools/README.md`.
10. **§4 paint carve-out invocations require the 4-check tick-box** per §4.1. Implementation conversations have standing authority to refuse a kickoff that invokes the carve-out without all four checks.

---

## 10. Recent commit history (most recent first)

All local-only on master. None pushed.

```
8507fd4  feat(test): P5-test commit 2 — 55-case library + spec-back
303ba51  fix(test): P5-test — WithBreakdownItem fluent setter per spec-author guidance (supersedes 10eafab)
10eafab  fix(test): P5-test — populate 21 empty NeutralVerdict rows (superseded)
db1675c  feat(test): P5-test commit 1 — render parity harness scaffolding + 5 sentinels + Ctrl+Shift+T
5920672  docs(screenshot-reliability-fixes): kickoff + spec-back report
20f1a0b  fix(tools): full-form screenshot foreground + natural extent + no form resize
aaf12e9  docs(screenshot-reliability): kickoff + spec-back report
65dd6e7  feat(tools): full-form screenshot + UIA radio/popup-close helpers
4a9781e  fix(ui): popup forms position on parent monitor
07c4ad2  docs(ui-reskin): P5a spec-back report
f707165  feat(ui-reskin): P5a — migrate calibration report to AnalysisReportForm
bcfdfd7  feat(ui-reskin): P5a — BuildPlaintextSnapshot + AnalysisOutputDump rewire
5b65ac2  docs(ui-reskin): P4f spec-back report
5f724ad  feat(ui-reskin): P4f — stale overlays + (stale) section-header pills
c8ebac6  feat(ui-reskin): P4f — ANALYSIS SKIPPED state plumbing + verdict-card render
3c72cec  chore(tools): add MainForm screenshot helper for visual verification
bc83f06  docs(ui-reskin): Spec B spec-back report
b77703d  fix(ui-reskin): VPFR histogram bucket count 8 → 16
94bb78a  fix(ui-reskin): downsample VPFR histogram to 8 visual buckets
ae61be8  feat(ui-reskin): VPFR bucket exposure + VolumeHistogramMini wiring
76d76c2  docs(ui-reskin): P3 maintenance pass spec-back
f54bdad  docs(ui-reskin): handover §4 — formalise P3 paint carve-out
bb7cd57  fix(ui-reskin): bump SectionGroup title 9pt FG_QUATERNARY → 11pt FG_SECONDARY
12dd54f  fix(ui-reskin): revert header tinting + VERDICT card crowding + TOTAL row size
be7f64b  fix(ui-reskin): unify card section-header style + tint SIGNAL BREAKDOWN
ba52994  fix(ui-reskin): P3 maintenance pass — 📊 tofu fix + prune unused controls
85ed635  docs(ui-reskin): P4e spec-back report
cf07d03  fix(ui-reskin): P4e — bump SETTINGS & TOOLS card row 300 → 340 px
34564ee  feat(ui-reskin): P4e — skipped-count surfacing + REPEAT/SINGLE chip
045cb18  feat(ui-reskin): P4e — SETTINGS & TOOLS grouped layout
f7ec66e  fix(ui-reskin): P4e commit 0 — GAP-72/73 OFI vol + GAP-74 spread status
```

P4d and earlier commits in `docs/ui-reskin-handover-2026-05-22.md` §11 — preserved for history.

---

## 11. Quick-reference / when in doubt

- **User runs the app on auto-run all day.** Don't disrupt scoring. Reskin is UI-only.
- **BTC has been in a macro bear since Oct 2025.** Calibration data skews short. Don't read STRONG_SHORT failure rates as steady-state.
- **Width hard ceiling 1280 px. Form is at 1100 px** — the floor of the rev 2.1 range, locked.
- **Vertical scroll is acceptable.** Form is tall (~1900 px logical with all cards). User's monitor is 4K vertical so scrolling works fine. Implementation conversations on smaller monitors use `screenshot-mainform-full.ps1` (post-fixes) to capture the full extent.
- **Trader-profile rejected patterns are absolute** — no fixed-% targets, no non-directional rewards, no double-counting, no flat regime penalties. None of the reskin touches these.
- **Conservative bias wins ties.** When a UX decision is between "show more / show less," favour show-less in cards the trader scans quickly, show-more in cards the trader references during calibration.
- **Don't push proactively.** Test gate is user-side.
- **Don't propose new indicator work.** Bottleneck is data, not UI.
- **Ask before doing.** Especially for anything touching scoring logic, settings.json structure, or CSV schema.
- **Self-screenshot before reporting visual findings.** Implementation AND spec-author. The user is the second-pass reviewer.
- **Multi-monitor support is real** — `PositionOnParentScreen` follows MainForm. `screenshot-mainform-full.ps1` works from a 4K secondary monitor. Confirmed across multiple spec-backs.

---

## 12. Suggested first action for the new conversation

After reading the cited files in §2:

**If the user signals "go for P5-test"**: read `docs/ui-reskin-p5-test-harness-kickoff.md` in full. The kickoff is complete and ready to drop into a fresh Opus 4.7 High conversation. The spec author's job at that point is to:

1. Wait for the implementation conversation's spec-back (with the discrepancy report).
2. Read the discrepancy report carefully — categorise each into "missing in cards" / "missing in snapshot" / "test case bug."
3. Draft `docs/ui-reskin-p5-test-gap-fixes-proposal.md` with the fix scope per gap category.
4. Hand off the fix spec to a fresh Opus 4.7 Medium conversation.
5. After fix ships, hand the P5-test cleanup commit + the P5b kickoff to subsequent conversations.

**If the user wants to defer P5-test**: the verification window stays open with `txtOutput` + verification dump card visible. P5b stays gated. Spec C stays parked. Trader can continue using the app normally without immediate spec-author intervention.

**If the user wants Spec C first (less likely)**: read `docs/sc-column-total-parity-proposal.md` and draft the kickoff. The proposal already covers the design decisions; the kickoff translates them to implementation. Recommended: don't pre-empt the reskin sequence; finish P5-test → P5b first.

---

## 13. Handover-doc maintenance protocol

This doc supersedes 2026-05-22. The next spec-author conversation should:

1. **Update §3 Roadmap** as each step ships — strike through completed items, add new sub-steps that emerged from spec-backs.
2. **Add to §4 Locked decisions** whenever a new lock surfaces. Cite source spec-back / commit.
3. **Add to §5 Process lessons** whenever a spec-back §8 (or equivalent) produces a generalisable rule.
4. **Move §6 Outstanding decisions** to the appropriate locked section once they're resolved.
5. **Refresh §10 Recent commits** — keep last ~20-30 commits visible; older ones get trimmed.
6. **Add a new handover doc** (e.g. `ui-reskin-handover-2026-MM-DD.md`) when this one approaches ~700 lines or when the project enters a new major arc (post-P5b would be the natural break).

The pattern across the project: handover docs accumulate, the most recent supersedes its predecessors, older versions stay on disk for archaeology but aren't read at session start.

---

**End of handover.** The next conversation has everything needed to continue the reskin through P5-test, the gap-fix spec, P5b, and Spec C without re-deriving context. Drop this doc as the opening message of the next spec-author conversation; the conversation reads it + `CLAUDE.md` + the relevant phase kickoff and is ready to operate.
