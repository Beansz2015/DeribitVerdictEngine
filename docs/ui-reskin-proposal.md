# UI Reskin Proposal

**Status:** PROPOSED (rev 2.1 — open questions resolved)
**Author:** Claude (Opus 4.7) + user
**Date drafted:** 2026-05-18 (rev 1) | **Updated:** 2026-05-19 (rev 2 — Claude Design v2 handover; rev 2.1 — Q1–Q5 resolved + width constraint)
**Spec target:** Windows Forms (.NET 8, VB.NET). No framework change.
**Inspiration:** Claude Design v2 handover bundle (`Wireframes.html` — MockupA dashboard, MockupStates gallery, MockupSkipped degraded state). Reference only — visual treatment ports to native WinForms with hand-painted controls.
**Settings.json impact:** none expected for v1. Optional `ui.theme` block for v2 (out of scope).

---

## 1. Motivation

The current `MainForm` renders the entire analysis as one big `RichTextBox` (`txtOutput`) driven by RTF helpers (`AppendRtf`, `AR`, `SectionHeader`, `Divider`). Readable, but visually dense — 115 monospace lines of text. The new design language is card-based with distinct typography, a custom score arc, mini progress bars, a mini volume profile, and a dedicated **Settings & Tools** section replacing the thin status bar.

The reskin is **visual only**. No change to scoring, indicators, settings schema, or CSV outputs. Only the rendering layer (`MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb`) and the layout (`MainForm_Layout.vb`) get rewritten.

---

## 2. Non-goals

- **No data shape changes.** `VerdictResult` and `IndicatorResults` stay byte-identical.
- **No new indicators.**
- **Minimal animation.** Score arc fill on render is the only allowed animation (Q3 — ~300ms ease-out, fires once per `RunAnalysisAsync` success). No other transitions, no value-change tweens, no idle ambient motion.
- **No theme switching at runtime.** Single dark theme, packaged.
- **No window-resize responsiveness beyond what the current form has.** Window stays fixed-size; the new layout sizes itself from card metrics rather than character metrics. **Hard width ceiling — see §3.6.**
- **No WebView2, HTML, JS, or web stack.** Pure WinForms with custom-paint controls.

---

## 3. Visual language (palette + typography)

Hex values are locked to the design's `Wireframes.html` source. Where the design uses `rgba(255,255,255,X)` alpha values for greys, the spec converts to opaque RGB on the dark base for WinForms (no alpha blending across cards).

### 3.1 Background layers

| Token | Hex | Use |
|---|---|---|
| `BG_BASE` | `#0D1117` | Form background (matches design) |
| `BG_CARD` | `#16161B` | Card panel background |
| `BG_CARD_RAISED` | `#1C1C22` | Inner sub-panels (e.g., grid alternate rows, perf strip background) |
| `BORDER_CARD` | `#2B2B33` | 1px card border (approximates `rgba(255,255,255,0.10)`) |
| `BORDER_INNER` | `#1F1F25` | Dividers inside a card (approximates `rgba(255,255,255,0.07)`) |
| `BORDER_DASHED_INFO` | `#2D5D6B` | Dashed cyan border on the Auto-run display group (approximates `rgba(103,232,249,0.35)`) |

### 3.2 Foreground (text)

| Token | Hex | Approximates | Use |
|---|---|---|---|
| `FG_PRIMARY` | `#EAEAEE` | `rgba(255,255,255,0.92)` | Verdict text, large values |
| `FG_SECONDARY` | `#BABABF` | `rgba(255,255,255,0.72)` | Section header text, normal labels |
| `FG_TERTIARY` | `#8B8B92` | `rgba(255,255,255,0.55)` | Tier labels, indicator names |
| `FG_QUATERNARY` | `#5E5E65` | `rgba(255,255,255,0.35)` | Confidence text, dim sub-labels |
| `FG_DIM` | `#3F3F47` | `rgba(255,255,255,0.25)` | "no swing target above entry", stale ATR values |
| `FG_INK` | `#0D1117` | `BG_BASE` | Text on amber CTA button (inverse) |

### 3.3 Accent (semantic) — design-aligned

| Token | Hex | Use |
|---|---|---|
| `ACC_STRONG_LONG` | `#4ADE80` | STRONG LONG verdict, BULL signals, +1 score, CONFIRMED, ALIGNED ↑ |
| `ACC_LONG` | `#86EFAC` | LONG verdict |
| `ACC_WEAK_LONG` | `#BBF7D0` | WEAK LONG verdict |
| `ACC_NO_TRADE` | `#94A3B8` | NO TRADE verdict |
| `ACC_WEAK_SHORT` | `#FCA5A5` | WEAK SHORT verdict |
| `ACC_SHORT` | `#F87171` | SHORT verdict, BEAR signals, −1 score, CONFLICT, STRUCTURALLY_WEAK |
| `ACC_STRONG_SHORT` | `#EF4444` | STRONG SHORT verdict |
| `ACC_WARN` | `#FBBF24` | CAPPED, REGIME ANCHOR caution, FLOW_UNCONFIRMED, MOMENTUM_FADING, [T] mode chip, ★◆ both-views-agree |
| `ACC_CTA` | `#F59E0B` | ANALYZE button fill (slightly deeper amber than `ACC_WARN`) |
| `ACC_CTA_GLOW` | `#F59E0B50` | Box-shadow glow under ANALYZE button (50% alpha amber) |
| `ACC_AMBER_DEEP` | `#D97706` | ANALYSIS SKIPPED verdict text colour |
| `ACC_INFO` | `#67E8F9` | Structural rows (cyan), HVN reference labels, Auto-run display countdown, ALIGNED context tag |
| `ACC_NEUTRAL` | `#64748B` | NEUTRAL / FLAT / NONE / MIXED / INSUFFICIENT signal states; SUPPRESSED Pass 2c |
| `ACC_TEXT_SHADOW_LONG` | `#4ADE8050` | Verdict text glow (textShadow under STRONG LONG, 50% alpha) |
| `ACC_TEXT_SHADOW_SHORT` | `#EF444450` | Mirror glow under STRONG SHORT |

### 3.4 Mapping from current `C_*` palette

| Current | New |
|---|---|
| `C_HEADER (255,220,80)` | `ACC_WARN (#FBBF24)` |
| `C_LABEL (160,160,160)` | `FG_TERTIARY (#8B8B92)` |
| `C_VALUE (200,200,200)` | `FG_PRIMARY (#EAEAEE)` |
| `C_GOOD (80,220,120)` | `ACC_STRONG_LONG (#4ADE80)` |
| `C_WARN (255,180,40)` | `ACC_WARN (#FBBF24)` |
| `C_BAD (255,80,80)` | `ACC_SHORT (#F87171)` |
| `C_HIT (100,200,255)` | `ACC_INFO (#67E8F9)` |
| `C_DIM (100,100,100)` | `FG_QUATERNARY (#5E5E65)` |
| `C_DIVIDER (80,80,80)` | `BORDER_CARD (#2B2B33)` |

### 3.5 Typography

The design preview uses `Caveat` (handwritten/wireframe) and `Share Tech Mono` (data) as **wireframe placeholders** — the tweaks panel exposes Consolas as an alternate. Production target stays **Geist Mono** (cleaner, geometric, modern) — flagged in the design's README, which explicitly says "recreate them pixel-perfectly in whatever technology makes sense for the target codebase." We're not adopting the wireframe fonts.

| Use | Family | Size | Weight |
|---|---|---|---|
| Verdict (`STRONG LONG`) | Geist Mono | 28pt | Bold, with text-shadow glow `0 0 20px ACC_TEXT_SHADOW_*` |
| Verdict in state-gallery (smaller mockups) | Geist Mono | 18pt | Bold |
| ANALYZE button label | Geist Mono | 15pt | Bold, letter-spaced |
| Score numeric (centre of arc) | Geist Mono | 22pt | Bold |
| Score denominator ("/ 20") | Geist Mono | 11pt | Regular |
| Card section header ("SCORE", "VERDICT") | Geist Mono | 11pt | Bold, letter-spaced 0.10em, uppercase |
| Large values ("$94,210") | Geist Mono | 15–19pt | Bold |
| Body / indicator rows | Geist Mono | 11pt | Regular |
| Compact signal-row state pill | Geist Mono | 11pt | Bold |
| Compact signal-row note | Geist Mono | 9pt | Regular |
| Status / chip text ("[B]", "REPEAT", "Auto every") | Geist Mono | 9–10pt | Bold |
| ANALYSIS SKIPPED hero text | Geist Mono | 28pt | Bold, deep amber |
| Sub-label dim text | Geist Mono | 9–10pt | Regular, alpha-reduced colour |

**Font choice — Geist Mono.** Selected for visual match to the wireframe (clean geometric mono, good for trading data). Bundled as:
- `fonts/GeistMono-Regular.ttf`
- `fonts/GeistMono-Bold.ttf`
- `fonts/GeistMono-SemiBold.ttf`
- `fonts/OFL.txt` (licence; SIL Open Font Licence 1.1)

**Font loading.** Embed as resource, register at process startup via `PrivateFontCollection`, expose via static `Theme.FontMono(size, style)` factory. No installer dependency — the .ttf files travel with the .exe.

**Fallback chain:** Geist Mono → JetBrains Mono → Cascadia Code → Consolas → SystemFonts.MonospacedFont. If `PrivateFontCollection.AddFontFile` fails, walk the chain.

**Docs note:** `CLAUDE.md` and `docs/DeribitIndicatorProject.md` need a line under "Build & Run" stating the .ttf files in `fonts/` and `fonts/OFL.txt` must be included in any future installer / xcopy deployment. Added in P1.

### 3.6 Window dimensions — hard constraints (Q2)

The trader runs the app alongside live TradingView / Deribit charts on a 28″ 4K monitor (3840 × 2160). The reskin must not consume more than **one third of the screen width** so the charts remain readable beside the app.

| Constraint | Value |
|---|---|
| **Max width (hard ceiling)** | 1280 px (= 3840 / 3) |
| **Target width (design)** | 1200 px |
| **Min width** | 1100 px (below this the side-by-side STRUCTURAL row breaks) |
| **Max height** | unconstrained by user, but should fit the current ~930px on a 1080p secondary monitor — target ≤ 1400 px |
| **DPI scaling** | the app must verify pixel widths under `AutoScaleMode.Font` at 100% and 150% DPI; constraint is on logical pixels |

**Implications across the design:**

- **STRUCTURAL LONG + SHORT** side-by-side (Q2 decision): each card needs ~280px including label/value columns + padding. Two cards + 8px gutter = ~570px. Fits comfortably.
- **OI × CVD CROSS + VOLUME PROFILE** side-by-side: same envelope, ~570px.
- **SIGNAL BREAKDOWN** stays two-column at ~1100-1180px usable width (~540px per column inside padding).
- **VERDICT hero row** three cards: SCORE ~150px, VERDICT flex (largest), LAST PRICE ~140px. Verdict card gets the rest.
- **SETTINGS & TOOLS** section: ANALYSIS REPORT CTA full-width inside the section ~1100px; LOG / AUTO-RUN grid two columns at ~540px each.
- **Live performance strip** must keep its 7 labels in one row at this width — verified (current labels at Geist Mono 9–10pt fit well under 800px with gaps).

**Designer note for P4a:** `SizeToContent()` replacement must clamp to `Min(targetCardGridWidth + chrome, Screen.PrimaryScreen.WorkingArea.Width / 3)`. On the user's 4K monitor that resolves to 1280 px. Add a runtime assertion (debug only).

---

## 4. Layout + card inventory + data binding

Each card is a `RoundedCardPanel` hosting per-card child controls. All bindings to existing data sources listed so the rewrite has no ambiguity. Top-to-bottom order matches the design.

### 4.1 Header strip (full-width row 1)

Layout: position selector left, ANALYZE + auto-run controls right (right-aligned column).

| Element | Type | Source field |
|---|---|---|
| `POSITION` label + 3× radio (No Position / Long / Short) | Section label + custom radio set | `rbNone` / `rbLong` / `rbShort` — reused |
| **ANALYZE button** — solid amber fill `ACC_CTA`, glow `ACC_CTA_GLOW`, ▶ + uppercase `ANALYZE` label, dark ink text | `FlatButton` variant `Solid` | `btnAnalyze` |
| `AUTO EVERY` chip label + minute chip + `m` + second chip + `s` | Inline bordered chips wrapping `NumericUpDown` | `nudMinutes`, `nudSeconds` (existing) — re-skinned as flat chips |

The wireframe shows the auto-run integers as static chips ("1 m  00 s"). Production keeps the `NumericUpDown` controls but restyled to look like the chips: flat border, no spinner arrows visible by default (use up/down hover affordance only).

### 4.2 Live performance strip (full-width row 2)

Lives **directly above** the SCORE/VERDICT/LAST PRICE row, **not** in the footer.

| Element | Source |
|---|---|
| `[B]` / `[T]` mode chip | `_metricMode` |
| 6× rate labels: Cur.Wk · 3d · Cur.Day · Asia · London · NY | `LivePerformanceTracker.ComputeWindows()` |

**Colour rules per cell** (matches existing `UpdatePerformanceLabels`):
- pct > 50 → `ACC_STRONG_LONG`
- pct < 50 → `ACC_SHORT`
- pct == null OR n < `MinSampleForRender` → `FG_QUATERNARY` (renders as `--%`)

**Mode chip colour — divergence from design (see §10 R8):** Engine semantic stays — chip dim when `_metricMode == defaultMode`, amber when ephemeral. Design used `green for [B] / amber for [T]` which loses the ephemeral indicator. Resolution: chip uses **mode-token colour** (`ACC_STRONG_LONG` for `[B]`, `ACC_WARN` for `[T]`) **AND** an italic asterisk suffix when ephemeral (e.g., `[B]*`). Hover tooltip explains.

Tooltip handler (`_perfTip.SetToolTip`) reused unchanged. Click handlers (`PerfLabel_MouseDown`, `PersistMetricMode`) unchanged.

### 4.3 Verdict hero row (3 cards side-by-side)

#### Card 4.3.1 — SCORE
| Element | Source |
|---|---|
| Circular arc gauge (240° sweep starting at −210°) | `v.Score / v.MaxScore` |
| Numeric "15" (arc colour matches verdict colour) | `v.Score` |
| Denominator "/ 20" | `v.MaxScore` |
| Confidence % sub-label | `v.Confidence` |

#### Card 4.3.2 — VERDICT (largest, flex)
| Element | Source |
|---|---|
| Verdict string (28pt with glow) | `v.VerdictString` → one of seven colours from §3.3 |
| CONTEXT row | `v.VerdictContext` — rendered via `ContextBadge` control (§6) |
| REGIME row | `r.Regime` text + amber colour, with optional ↺ glyph when `_prevRegime` differs (hysteresis breadcrumb) |
| MTF GATE row | `r.MTFGatePass` + `r.MTFGateReason` — rendered via `MtfRow` control, three formats |
| HOLD row (gated) | `v.HoldStatus` — rendered only when `posState ≠ None` |
| **REGIME ANCHOR caution** | When trigger fires — rendered via `RegimeAnchorWarn` control (§6), amber pill `⚠ REGIME ANCHOR — {text}` |

#### Card 4.3.3 — LAST PRICE
| Element | Source |
|---|---|
| Price ("$94,210") | `r.CurrentPrice` |
| ATR sub-row ("ATR 112.4") | `r.ATR` |
| Divider | — |
| UTC time | `DateTime.UtcNow.AddHours(8)` |
| Session label | ASIA / LONDON / NY |

### 4.4 ATR ENTRY LEVELS (full-width row 4)

Five-zone horizontal layout: Stop ← R:R divider → Entry ← CAPPED divider → Target.

| Element | Source |
|---|---|
| STOP value (red) | `v.StopLong` / `v.StopShort` |
| R:R divider centre text | `FormatRR(stopDist, targetDist)` — uses v30 `< 0.1` literal |
| ENTRY value (white) | `r.CurrentPrice` |
| **CAPPED divider centre text** (amber) | "CAPPED" when `|raw − adjusted| ≥ max(0.5, ATR × 0.02)`; **omitted entirely** when sub-tick (label and the entire centre column collapse to a thin divider line) |
| TARGET value (green) | `v.AdjustedLongTarget` / `v.AdjustedShortTarget` when capped; raw target otherwise |
| Cap reason sub-label below target (amber, small) | `v.TargetCapReasonLong` / `v.TargetCapReasonShort` (e.g. `SWING_HIGH_5M`, `HVN_AT`, `POC`) — only shown when CAPPED label visible |

### 4.5 SIGNAL BREAKDOWN (full-width, two-column grid)

Column headers: `INDICATOR · STATE · NOTE · SC` (per column, twice across the grid).

**Left column — CORE → TIER 1:**
- ROC(9), RSI(9), RSI Div, DMI/ADX, Volume
- VWAP Dev, VWAP Bands, BBW/TTM, EMA Ribbon, **Trend Str** (new — D1), Funding, **Funding Mom** (new — separate row), OI Change

**Right column — TIER 2 → TIER 3:**
- Spread, OFI, OFI Mom, Liq, CVD, MicroCVD, TFI, EMA200 5m
- Donchian, OBV, VPFR, Swing Pivots (with D2 sub-note: `↳ best vol: HIGH @ 95200 (2.3×)`)

Each row binds to a `SignalBreakdownItem` in `v.SignalBreakdown`: dot colour from state, label, coloured state pill, dim note, signed score.

**Footer rows** (full-width, below the grid, separated by divider):

| Element | Source |
|---|---|
| OI × CVD | `r.OiCvdOutcome` — one of four states via `OiCvdBadge` (§6) |
| Pass 2c | `v.Pass2cOutcome` — one of three states (ALIGNED ↑ / CONFLICT ↓ / SUPPRESSED) |
| Funding Mom | `r.FundingMomentum` (RISING / FALLING / FLAT) — step 3b indicator |

### 4.6 STRUCTURAL (LONG / SHORT) — 5M PIVOTS (full-width row 6)

**Side-by-side** (Q2 — confirmed fits within 1280px width ceiling — see §3.6). Two cards, ~280px each + 8px gutter. **Three render states each** — explicit handling:

| State | STRUCT STOP | STRUCT TARGET |
|---|---|---|
| FULL | value, red | value, cyan |
| STOP ONLY | value, red | dim text "— no swing target above entry" |
| TARGET ONLY | dim text "— no swing stop below entry" | value, cyan |

ENTRY and R:R rendered only when both target+stop exist.

### 4.7 OI × CVD CROSS + VOLUME PROFILE (full-width row 7, two cards side-by-side)

#### Card 4.7.1 — OI × CVD CROSS
| Element | Source |
|---|---|
| Outcome dot + label (one of 4 states) | `r.OiCvdOutcome` via `OiCvdBadge` |
| `MiniMeter` Funding Mom | `r.FundingMomentum` magnitude + direction colour |
| `MiniMeter` Spread | `r.SpreadBps` against `cfg.Indicators.Spread.WidePenaltyThresholdBps` |

#### Card 4.7.2 — VOLUME PROFILE
| Element | Source |
|---|---|
| VAH row | `r.VPFRVAH` |
| HVN↑ row | `r.VPFRNearestHvnAbove` |
| POC row (bold + amber) | `r.VPFRPOC` |
| HVN↓ row | `r.VPFRNearestHvnBelow` |
| VAL row | `r.VPFRVAL` |
| Mini histogram (8 bars, POC highlighted amber, current-price line green) | derived from `r.VPFRBuckets` + `r.CurrentPrice` |
| "price above POC ↑" caption | derived |

### 4.8 KELLY SIZING (full-width row 8)

Three-column grid. Hidden when `v.KellyF ≤ 0`.

| Element | Source |
|---|---|
| CONTRACTS | `v.KellyContracts` — singular "1 contract" / plural "3 contracts" |
| RISK $ (amber) | `v.KellyRiskDollars` |
| KELLY F | `v.KellyF` |

### 4.9 SETTINGS & TOOLS section (full-width row 9 — **replaces status bar**)

**This is a major restructure from rev 1.** Not a thin footer bar — a dedicated grouped section with:

```
┌─────────────────────────────────────────────────────────┐
│ SETTINGS & TOOLS                                        │
│ ┌─────────────────────┬─────────────────────────────┐   │
│ │ LOG                 │ AUTO-RUN  (dashed cyan)     │   │
│ │ Log: 2580 rows      │ Next run in: 00:42          │   │
│ │ ↺ Reset Log         │ ▶ REPEAT                    │   │
│ └─────────────────────┴─────────────────────────────┘   │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ 📊 ANALYSIS REPORT                              →   │ │  ← prominent CTA (amber)
│ └─────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ TOOLS                                               │ │
│ │ › Calibration Readiness                             │ │
│ │ › Tweak Settings                                    │ │
│ │ › Output Dump                                  ⚙    │ │
│ └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

| Element | Source / handler |
|---|---|
| LOG box header | static "LOG" |
| Log info line | `lblLogInfo.Text` — "Log: 2580 rows" |
| **Skipped tag** | when `_skipCount > 0` append `· skipped {N}` (NEW — surfaces resilience state) |
| Reset Log button (red-bordered pill) | `lnkResetLog_LinkClicked` |
| AUTO-RUN box header | static "AUTO-RUN" (cyan dashed border) |
| Countdown line | `lblCountdown.Text` |
| REPEAT / SINGLE chip | derived from `rbRepeat.Checked` / `rbSingle.Checked` |
| **ANALYSIS REPORT prominent button** | `lnkAnalysisReport_LinkClicked` — amber border + glow, 📊 icon, → arrow |
| TOOLS box header | static "TOOLS" |
| › Calibration Readiness | `lnkCalibCheck_LinkClicked` |
| › Tweak Settings | `lnkTweakSettings_LinkClicked` |
| › Output Dump + ⚙ icon | `lnkOutputDump_LinkClicked` + `lnkOutputDumpSettings_LinkClicked` |

Analysis Report is promoted to a primary CTA because the trader uses it after every batch of runs — buried link-style treatment undersells it.

---

## 5. Coverage of current-state features the wireframe omits OR adds

Combined audit (rev 1 omissions + rev 2 design additions). All items must be present in the reskin.

| # | Feature | Engine version | Treatment |
|---|---|---|---|
| 1 | **Settings & Tools section** (status bar replacement — design rev 2 restructure) | n/a (UI) | See §4.9. Grouped section with LOG / AUTO-RUN / ANALYSIS REPORT CTA / TOOLS sub-boxes. |
| 2 | **Skipped session count** in Log row | v18 (resilience pass) | "Log: N rows · skipped {M}" appended when `_skipCount > 0`. Currently `_skipCount` exists but isn't surfaced. |
| 3 | Live performance strip — `lblPerfMode` + 6 rate labels | v26 + v28 | Dedicated row 2, above verdict hero. Mode chip resolution per §4.2. |
| 4 | Auto-run controls — `nudMinutes`, `nudSeconds` | unchanged | Inline flat chips inside header strip. |
| 5 | **ALIGNED context tag** | v30 F11 | `ContextBadge` "↗ ALIGNED" (cyan). |
| 6 | **REGIME ANCHOR caution line** | post-v30 (`1eff4f3`) | `RegimeAnchorWarn` control — amber pill, conditional, vertical space reserved when absent. |
| 7 | **Sub-tick CAPPED suppression** | v30 F1 | ATR card collapses the CAPPED divider centre when `|raw − adjusted| < max(0.5, ATR × 0.02)`. |
| 8 | **MTF three-format reason** | by design | `MtfRow` control — three branches (PASS / BLOCK / state-only). |
| 9 | **SHORT structural row** | unchanged | Structural card mirrors LONG; three-state rendering per §4.6. |
| 10 | **STRUCTURAL three states** (FULL / STOP ONLY / TARGET ONLY) | design rev 2 | Explicit in §4.6 table. |
| 11 | **HOLD\EXIT line** | unchanged | Inside VERDICT card. Render-gated on `posState ≠ None`. |
| 12 | **Verdict label 7-colour ramp** | unchanged | STRONG LONG → STRONG SHORT colours from §3.3. |
| 13 | **Context badge 5 visual variants** | v30 + design | `ContextBadge` map — ✓ / ↗ / ⚠ / ⚠ / ⚠ with semantic colours. |
| 14 | **OI × CVD four states** | Bundle 1 + design rev 2 | `OiCvdBadge` — ● CONFIRMED LONG / ● CONFIRMED SHORT / ⚠ CONFLICT / ○ NEUTRAL. |
| 15 | **Pass 2c three states** | Pass 2c + design rev 2 | ALIGNED ↑ (green) / CONFLICT ↓ (red) / SUPPRESSED (grey). |
| 16 | **Trend Structure (D1) row** | Bundle 3 / v24 | New Tier 1 row "Trend Str" — HH/HL (green) / LH/LL (red) / MIXED / INSUFFICIENT (grey). Note column shows direction context. |
| 17 | **Volume-Weighted Pivots (D2) sub-note** | Bundle 3 / v24 | Appended as `↳ best vol: HIGH @ 95200 (2.3×)` sub-line under the existing Swing Pivots row. Sourced from `r.BestPivotByVolume5m`, `r.BestPivotVolumeRatio5m`, `r.BestPivotIsHigh5m`. |
| 18 | **Funding Mom dual placement** | v22 | Separate row in Tier 1 of breakdown grid (`Funding Mom · RISING · step 3b · +1`) AND footer aggregation row alongside OI × CVD / Pass 2c. |
| 19 | **Funding negative-zero clamp** | v30 F4 | Applied at all display sites (`|r| < 1e-8` → 0.0). |
| 20 | **R:R `< 0.1` literal** | v30 F3 | `FormatRR` helper at all R:R sites. |
| 21 | **Kelly pluralisation** | v30 F8 | "1 contract" singular vs "3 contracts" plural. |
| 22 | **DYNAMIC NORMS section** | unchanged | Small card row below VOLUME PROFILE — diagnostic, default visible. ATR scale, VolHigh/Mid, VWAPDevThreshold, session bucket. Wireframe omits this card; we add it. |
| 23 | **Regime hysteresis breadcrumb** | v15 | `↺` glyph next to REGIME label in verdict card when `_prevRegime` differs. |
| 24 | **Dual ★/◆ recommendation marks** | post-v30 | **Not surfaced in main form** — Analysis Report markdown only. Audit-complete; no main-form work. |
| 25 | **ANALYSIS SKIPPED degraded state** | v18 (resilience) + design rev 2 | New render path. See §5.1. |
| 26 | **Stale tags on cards during SKIPPED** | new (with §5.1) | `(stale)` amber sub-label next to section headers; cards rendered at opacity 0.4 with last-known values. |
| 27 | **Analysis Report promoted to CTA** | n/a (UI) | Lives inside SETTINGS & TOOLS section as a prominent amber button. |

### 5.1 ANALYSIS SKIPPED degraded render

When `Task.WhenAll` in `RunAnalysisAsync` produces any `Nothing` for a required dependency (1m candles, 5m candles, funding, book summary, order book, recent trades — 15m failure alone does NOT skip per existing resilience rules), the engine currently increments `_skipCount` and aborts the run. The reskin gives this path a proper visual surface.

**Render plan:**

- **Header strip:** opacity 0.5 — visible but visibly inert.
- **Live performance strip:** unchanged (last computed values remain valid).
- **VERDICT card:** verdict text replaced by `ANALYSIS SKIPPED` in `ACC_AMBER_DEEP` (28pt bold with glow). Section header accent colour switches to `ACC_WARN`. Two sub-lines:
  - **Reason** — colour `#FBBF24AA` — "Deribit REST fetch failed — {which call} returned {error}". Sourced from new `_lastSkipReason` field captured at skip time.
  - **Hint** — colour `FG_TERTIARY` — "Engine retains last-known indicator values. Skipping verdict generation until next successful fetch (auto-run continues)."
- **Other cards** (LAST PRICE, ATR ENTRY LEVELS, SIGNAL BREAKDOWN, STRUCTURAL, OI×CVD CROSS, VOLUME PROFILE, KELLY): rendered at opacity 0.4 showing **last known values** from the most recent successful run. Each card section header carries a small amber `(stale)` tag and an optional age label (`2 min stale`).
- **SETTINGS & TOOLS section:** LOG box updates with `· skipped {N}` and a new timestamp line `last 2026-05-18 14:21:09` (sourced from last successful run).

**State plumbing required:**
- `_lastSuccessfulVerdict As VerdictResult` — captured at end of every successful render
- `_lastSuccessfulIndicators As IndicatorResults`
- `_lastSuccessfulRenderTime As DateTime`
- `_lastSkipReason As String` — set when `RunAnalysisAsync` aborts

All four added to `MainForm_Layout.vb` shared fields. Captured / mutated in `MainForm_Analysis.vb` at the success / abort points.

**Render entry point:** new `RenderSkippedDashboard(reason)` method in `MainForm_Render_Cards.vb` (new partial file). Called instead of `RenderDashboard(v, r)` when the skip branch is taken.

---

## 6. Custom controls (new)

All housed in `UI/Controls/`. Each is a `UserControl` subclass — no `MainForm` coupling, palette tokens injected via constructor.

| Control | Purpose | Notes |
|---|---|---|
| `RoundedCardPanel` | Card background with rounded corners and 1px border. `OnPaint` via `GraphicsPath.AddArc`. | `CornerRadius` (default 6), `BorderColor`, `Background`, `BorderStyle` (Solid / Dashed). |
| `ScoreArcGauge` | 240° arc gauge starting at −210°. Background track + foreground arc with drop-shadow approximation. Centre text rendered separately. | `Value`, `Max`, `ArcColor`, `Verdict` (drives colour). |
| `VolumeHistogramMini` | 8-bar horizontal mini histogram. POC bar amber-tinted, current-price horizontal line green. | Reads `r.VPFRBuckets` + `r.CurrentPrice`. |
| `MiniMeter` | Label + value + thin progress bar. Used in OI × CVD CROSS card. | `Label`, `Value`, `Pct`, `FillColor`. |
| `FlatButton` | Replacement for `btnAnalyze`. Two variants: `Solid` (amber CTA fill, dark ink) and `Outline` (border + tinted fill). Reuses click handlers. | `Variant`, `Label`, `AccentColor`, `Icon`. |
| `ChipNumeric` | Replacement skin for `NumericUpDown`. Flat border, no spinner arrows, click-to-edit. | Wraps existing `NumericUpDown`. |
| `SegmentedToggle` | Single/Repeat segmented control. | Wraps `rbSingle` / `rbRepeat`. |
| `Pill` | Generic text capsule. Used by `[B]/[T]` mode chip, REPEAT chip, stale tag, `(stale)` sub-labels. | `Text`, `BgColor`, `FgColor`, `BorderColor`. |
| `LinkRow` | Styled link row used in TOOLS group. `›` chevron + label + optional trailing icon (e.g., ⚙). | Hover changes colour to `ACC_INFO`. |
| `ContextBadge` | 5-variant context tag — icon + label. Maps CONFIRMED / ALIGNED / FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK to icon (✓ / ↗ / ⚠ / ⚠ / ⚠) and colour. | Property: `Kind` (enum). |
| `MtfRow` | 3-format MTF row. Branches on `Kind` enum (PASS / BLOCK / STATE_ONLY). | Properties: `Kind`, `Direction`, `BlockedAgainst`. |
| `RegimeAnchorWarn` | Amber-pill caution row. Renders only when `Text` non-empty (reserves vertical space when absent via height=0). | Property: `Text`. |
| `OiCvdBadge` | 4-variant outcome badge. CONFIRMED LONG (green ●) / CONFIRMED SHORT (red ●) / CONFLICT (amber ⚠) / NEUTRAL (grey ○). | Property: `Outcome` (enum). |
| `Pass2cBadge` | 3-variant outcome — ALIGNED ↑ / CONFLICT ↓ / SUPPRESSED. | Property: `Outcome` (enum). |
| `AnalysisReportButton` | Prominent amber CTA — border + glow + 📊 icon + → arrow. Larger than other links by design. | Click → `lnkAnalysisReport_LinkClicked`. |
| `SectionGroup` | LOG / AUTO-RUN / TOOLS grouped sub-box inside the SETTINGS & TOOLS section. Optional dashed border (used by AUTO-RUN). | `Title`, `BorderStyle`. |

**Soft glow on verdict text** — approximated via `TextRenderer.DrawText` over a `GraphicsPath` filled with `PathGradientBrush` at low alpha. Cheaper than true GDI+ blur; close enough for the dark background. Painted only on STRONG LONG, STRONG SHORT, and ANALYSIS SKIPPED.

---

## 7. Render binding refactor

After reskin, `txtOutput` is deleted; the binding shape becomes:

```
RunAnalysisAsync
  ├─ if all fetches succeed:
  │     RenderDashboard(v, r, norms, cfg)
  │       ├─ BindHeaderStrip()
  │       ├─ BindLivePerformanceStrip()
  │       ├─ BindCardScore(v)
  │       ├─ BindCardVerdict(v, r)              ' incl. ALIGNED, REGIME ANCHOR, MTF reason
  │       ├─ BindCardLastPrice(r)
  │       ├─ BindCardAtrLevels(v, r)            ' incl. sub-tick CAPPED suppression
  │       ├─ BindCardStructural(r, isLong:=True, isLong:=False)  ' 3 states per side
  │       ├─ BindCardSignalBreakdown(v, r)      ' incl. Trend Str row + D2 sub-note + Funding Mom
  │       ├─ BindCardOiCvdCross(r)              ' OiCvdBadge 4-state
  │       ├─ BindCardVolumeProfile(r)
  │       ├─ BindCardKelly(v)
  │       ├─ BindCardDynamicNorms(norms)
  │       └─ BindSettingsAndTools()             ' incl. _skipCount surfacing
  │     CaptureLastSuccessful(v, r)             ' for skip recovery
  │
  └─ if any fetch fails:
        _lastSkipReason = <which call + error>
        RenderSkippedDashboard(_lastSkipReason)
          ├─ BindSkippedVerdictCard(reason)
          ├─ ApplyStaleOverlayToCards()         ' opacity 0.4 + (stale) tags
          └─ BindSettingsAndTools()             ' surfaces skipped count + last successful timestamp
```

Each binding method lives in `UI/MainForm_Render_Cards.vb` (new partial). Existing `MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` are deleted at P5.

**Helpers retained:** `BuildCalibrationReport`, `UpdateLogInfo`, `Flag()`, all link-click handlers.

**Helpers deleted:** `AppendRtf`, `AR`, `SectionHeader`, `Divider`, `SetOutputMargins`, `OUTPUT_CHARS`, `OUTPUT_LINES`, P/Invoke `EM_*` messages, `RECT` struct, `SizeToContent` (subsumed by card-grid sizing).

**Window sizing:** card-grid measurement replaces character-width × OUTPUT_CHARS. Concrete pixel values measured during P3.

---

## 8. Phased implementation plan

Each phase compiles, runs, and passes auto-run for at least 3 cycles before moving on. Local commit per phase. Push only after all phases ship and the user has verified end-to-end on live data.

### P1 — Theme infrastructure (no UI change visible)

- Add `fonts/GeistMono-Regular.ttf`, `Bold.ttf`, `SemiBold.ttf`, `fonts/OFL.txt` to repo.
- Add `<ItemGroup><EmbeddedResource Include="fonts\*.ttf"/></ItemGroup>` to `.csproj`.
- New file `UI/Theme/Theme.vb` — `PrivateFontCollection`, palette constants (all `ACC_*` / `BG_*` / `FG_*` tokens), `FontMono(size, style)` factory.
- Replace `C_*` references in existing `MainForm_Layout.vb` with new palette tokens at **same hex values** so visual is unchanged.
- Update `CLAUDE.md` and `docs/DeribitIndicatorProject.md` with the bundled-fonts note.

**Verification:** builds, runs, renders identically.

### P2 — Theme repaint (visual change, layout unchanged)

- Swap palette tokens to new hex values.
- `txtOutput.Font` → `Theme.FontMono(11)`.
- `lblVerdict.Font` → `Theme.FontMono(22, Bold)`.
- `btnAnalyze` recolour to `ACC_CTA` with glow approximation.
- Existing layout untouched.

**Verification:** screenshot comparison; tone matches even though layout is still text-based.

### P3 — Custom controls library

- Build all 15 controls in `UI/Controls/`.
- No `MainForm` wiring yet.

**Verification:** drop each control onto a scratch form and confirm paint behaviour.

### P4 — New layout skeleton + card binding

- **P4a:** New programmatic layout in `MainForm_Layout.vb`. All cards as `RoundedCardPanel` inside a top-level `TableLayoutPanel`. Don't bind data yet.
- **P4b:** `BindHeaderStrip`, `BindLivePerformanceStrip`, `BindCardScore`, `BindCardVerdict`, `BindCardLastPrice`, `BindCardAtrLevels`, `BindCardStructural` × 2. Hook into `RunAnalysisAsync` end alongside existing `RenderOutput`. Both render targets co-exist.
- **P4c:** `BindCardSignalBreakdown` — densest binding (incl. Trend Str + D2 sub-note + Funding Mom row).
- **P4d:** `BindCardOiCvdCross`, `BindCardVolumeProfile`, `BindCardKelly`, `BindCardDynamicNorms`.
- **P4e:** `BindSettingsAndTools` — implements the §4.9 layout. Migrates existing perf strip + status bar handlers into the new section.
- **P4f (NEW):** ANALYSIS SKIPPED degraded state — `CaptureLastSuccessful`, `RenderSkippedDashboard`, `ApplyStaleOverlayToCards`, `_lastSkipReason` plumbing in `MainForm_Analysis.vb`. Verify by temporarily forcing a fetch failure (return `Nothing` from one `GetXxxAsync` call) and confirming the rendered SKIPPED state.

**Verification:** new cards render correct values for every verdict. Side-by-side comparison against still-running `txtOutput` confirms parity. SKIPPED path manually triggered once.

### P5 — Remove old rendering

- Delete `txtOutput` from designer.
- Delete `MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb`.
- Delete RTF helpers and P/Invoke surface area (per §7).
- **Replace `AnalysisOutputDump.Append`** behaviour: build `BuildPlaintextSnapshot(v, r, norms, cfg)` that produces the existing markdown shape directly from data. **Non-trivial; see §10 R1.**

**Verification:** full auto-run cycle. Output dump still produces the same shape. CSV unchanged.

---

## 9. Settings.json change

**None expected for v1.**

---

## 10. Risks + open questions

| # | Risk / question | Mitigation / proposal |
|---|---|---|
| R1 | `AnalysisOutputDump` snapshots `txtOutput.Text`. After P5 there's no text surface. | Build `BuildPlaintextSnapshot` from data. **Critical task in P5.** |
| R2 | Geist Mono via `PrivateFontCollection` has known DPI/anti-aliasing quirks on Windows. | Test in P1. Fall back to JetBrains Mono if hinting artefacts visible. Both bundled. |
| R3 | `MainForm.Designer.vb` references `txtOutput`. Opening the designer mid-port can auto-regenerate and re-add it. | Don't open the designer during P4/P5. Add sharper warning in CLAUDE.md (current rule covers editing; needs to also cover opening). |
| R4 | Form control count rises from ~25 to ~200+. Z-order / focus / tab-index need attention. | TabIndex set per card cluster; `TabStop = False` on decorative panels. Manual sweep in P4e. |
| R5 | Existing `ResizeControls()` uses fixed pixel coords. New layout uses `TableLayoutPanel` + `DockStyle.Fill`. | Acceptable. Form stays fixed-size. |
| R6 | "Soft glow" approximation may feel flatter than the wireframe's CSS `text-shadow`. | Accept for v1. Add real `GlowText` control in v2 if user disagrees after P2. |
| R7 | `UpdatePerformanceLabels` currently calls `ResizeControls()` to re-cascade X positions when label widths change. The new `TableLayoutPanel` auto-sizes. | Replace with `card.PerformLayout()` in P4e. Minor. |
| R8 | **[B]/[T] mode chip semantic conflict.** Design uses green-for-B / amber-for-T (semantic = which metric). Engine currently uses dim-when-default / amber-when-ephemeral (semantic = persistence state). | Combine both: chip uses mode colour (`[B]` green, `[T]` amber) AND adds italic `*` suffix when ephemeral. Tooltip explains both. See §4.2. |
| R9 | The ANALYSIS SKIPPED state requires capturing last-successful `VerdictResult` + `IndicatorResults` between runs. Memory shape change (small) on `MainForm`. | New shared fields in `MainForm_Layout.vb`. Captured at end of `RunAnalysisAsync` success path. Trivial. |
| R10 | The "stale tags" on cards during SKIPPED require each `BindCardXxx` method to accept a `staleness` parameter or use `(stale, lastRunTs)` overrides. | Resolve with single `ApplyStaleOverlayToCards()` post-pass that traverses each card and toggles opacity + sets the section-header `(stale)` pill. Cards don't need awareness; the overlay does the work. |
| R11 | Geist Mono OFL licence requires the licence text travel with the .ttf bundles. | `fonts/OFL.txt` committed alongside the .ttf files. Listed in P1. |
| R12 | Analysis Report promoted to a CTA is a UX change beyond pure reskin — moves from buried status-bar link to a primary action. User may want it back where it was. | Flagged here. Confirm before P4e. |
| R13 | The wireframe-default mono font is `Consolas` (TWEAK_DEFAULTS in source); the previewed font is Share Tech Mono. Production target is Geist Mono. Three fonts in play across reference vs implementation. | Don't be confused — Geist Mono is the production choice. Design's font picker is a wireframe affordance. |
| Q1 | DYNAMIC NORMS card default visibility | **DECIDED — visible**. |
| Q2 | LONG/SHORT structural side-by-side vs stacked | **DECIDED — side-by-side** within a hard 1280px width ceiling (= 3840/3 on the user's 4K monitor). Drives all width constraints in §3.6. |
| Q3 | SCORE arc animation | **DECIDED — yes**, score-arc fill only, ~300ms ease-out on render. No additional screen space. §2 non-goal updated. |
| Q4 | Auto-run NUDs styling | **DECIDED — chip + click-to-edit**. Hover reveals up/down arrows on the hovered chip. |
| Q5 | Skipped count surfacing | **DECIDED — only when N > 0**. LOG row appends `· skipped {N}` conditionally. |

---

## 11. Out of scope (future work)

- Light theme.
- User-customisable palette.
- Animated transitions.
- Sparklines on the performance strip.
- Full-page PDF / image export.
- Linux CLI port — separate spec.

---

## 12. Approval gate

User reviews this revised doc and either:
- Approves wholesale → P1 begins.
- Approves with revisions → spec updates first.
- Rejects → throw away, redo.

**Spec-first workflow rule:** no implementation code is written until this rev 2 is approved.
