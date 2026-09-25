# UI controls audit — `UI/Controls/*` + `Helpers/PaintHelpers.vb`, at commit `6e74181`

Hostile audit. Scope: the 14 files named in the brief —
`UI/Controls/OiCvdBadge.vb`, `SectionGroup.vb`, `ContextBadge.vb`, `FlatButton.vb`, `Pill.vb`,
`AnalysisReportButton.vb`, `RoundedCardPanel.vb`, `TapeStripLabel.vb`, `RegimeAnchorWarn.vb`,
`LinkRow.vb`, `Helpers/PaintHelpers.vb`, `ChipNumeric.vb`, `MiniMeter.vb`, `MtfRow.vb`. Audited
tree: git worktree at commit `6e74181f000ddc7666e8b7d17c64a195855b45cd`
(`fix(fixtures): declare A43b's tfiWindowSize MECHANISM — the violation the clean run found`,
2026-09-22 22:57:58 +0800). Nothing newer than that commit was read as "in scope."

Excluded per the brief (already reported, not re-reported here): Kelly sized from a different
stop than the placed levels; no try/catch around the card binds; the degenerate all-zero VPFR
histogram; `Math.Abs` hiding a wrong-side R:R.

## 0. Not read in full

**Nothing in the assigned 14-file scope was skipped — all 14 were read start to end.**

For call-site context (required to judge reachability, not part of the audited fileset, and not
claimed as exhaustively reviewed):

- `UI/MainForm_Render_Cards.vb` — read the `BindCard*` methods that construct/mutate the 14
  audited controls (`BindCardVerdict`, `BindCardOiCvdCross`, `ApplyMtfRow`/`ExtractDirection`/
  `ExtractBlockedAgainst`, `ParseContextKind`, `MapOiCvdOutcome`, `ComputeRegimeAnchorWarning`,
  `ResolveVerdictColour`, `BuildMiniMeter`, the stale-pill block) and the control-construction
  block (~lines 150–270). Not read end-to-end; large sections (Kelly card, structural cards,
  signal-breakdown card, indicator-details card) were not opened because they don't touch the
  14 audited controls.
- `UI/MainForm_LiveStrip.vb` — read in full. It is the sole source of the text/color that reaches
  `TapeStripLabel`, so its `ComposeLiveStrip`/`ComposeTape`/`ComposeAbsorption` ordering had to be
  verified directly rather than assumed.
- `UI/MainForm_Layout.vb` — read the construction blocks for `lblLiveStrip`, `_contextBadge`,
  `_mtfRow`, `_regimeAnchorWarn`, `_autoRunChip` (a `Pill`) to confirm which controls are
  persistent, field-held instances versus rebuilt-per-run locals, and to confirm every
  `CornerRadius`/`Pct` call site passes a static literal, not a data-derived value. Not read
  end-to-end.
- `UI/MainForm_Analysis.vb` — read `btnAnalyze_Click` (the only try/catch around
  `RunAnalysisAsync`) and the `BindCard*` call sequence (lines ~646–675) to establish render
  order. Not read end-to-end.
- `UI/MainForm_AutoRun.vb` — grepped for `Try`/`Catch`/`RunAnalysisAsync` only, to confirm
  auto-run's failure path is the same `btnAnalyze_Click` handler (per the brief's own framing).
  Not read in any other respect.
- `Core/Indicators_Structure.vb` (`CalcMTFGate`) and `Core/ScoringEngine_Calculate_Verdict.vb`
  (the `MTFGateReason` composition, Step 4b) — read narrowly to pin down the exact literal text
  the "MTF insufficient 15m candles" scenario in the brief actually produces, for the logic trace
  in §1. Not read as an engine/scoring audit.
- `docs/architecture.md` — read the Directory Layout, Data Flow, and grepped Display Behaviour
  Clarifications sections per the brief's instruction to learn the display surfaces first (this is
  what surfaced that the TAPE strip is a *third* rendered surface, fed by `LiveMicrostructureEvaluator`
  on its own timer, separate from `BuildPlaintextSnapshot`/`BindCard*` and NOT covered by the
  parity rule — `MainForm_LiveStrip.vb:1-20`'s own header says so).

Not opened at all: `UI/Controls/ScoreArcGauge.vb`, `UI/Controls/VolumeHistogramMini.vb` (present
in the same directory but not named in the brief's file list — out of scope by the brief's own
terms, not by my choice).

## 1. LOGIC TRACE

```
--- LOGIC TRACE ---
Scenario (given): a -1.5% flush. ATR=120, R:R=0.39, absorption fields empty,
MTF reason text "insufficient 15m candles", OI=0.

1. CalcMTFGate (Core/Indicators_Structure.vb:441-459), candles15m too short:
     mtfTrend    = "FLAT"
     gateDetails = "MTF: insufficient 15m candles (0)"     [or the real count if Not Nothing]
     gatePassLong = gatePassShort = True   (insufficient data never blocks)

2. ScoringEngine_Calculate_Verdict.vb Step 4b: R:R 0.39 is weak, so the dominant
   side (if any) almost certainly fails the VerdictWeakPct threshold -> `directional` = False.
     -> res.MTFGateReason = "MTF state: " & "FLAT" & " | " & "MTF: insufficient 15m candles (0)"
                           = "MTF state: FLAT | MTF: insufficient 15m candles (0)"

3. MtfRow / ApplyMtfRow (MainForm_Render_Cards.vb:1451-1481):
     reasonText = "MTF state: FLAT | MTF: insufficient 15m candles (0)"
     starts with "MTF " -> stripped = "state: FLAT | MTF: insufficient 15m candles (0)"
     ExtractDirection/ExtractBlockedAgainst: no "[" in stripped -> both "" (safe, no throw)
     stripped starts with "state:" -> Kind=STATE_ONLY, Direction = "FLAT | MTF: insufficient 15m candles (0)"
     MtfRow.Refresh2() (MtfRow.vb:65-77): Text = "MTF state: FLAT | MTF: insufficient 15m candles (0)"
     -> round-trips to the identical raw string. No truncation, no exception. SAFE.

4. ContextBadge (ContextBadge.vb + ParseContextKind, MainForm_Render_Cards.vb:1438-1449):
   R:R 0.39 with a swing pair likely present -> VerdictContext is one of
   FLOW_UNCONFIRMED / STRUCTURALLY_WEAK (engine's call, not re-derived here).
   Either string maps through the Select Case cleanly; an unrecognised string
   would silently fall to CONFIRMED (ParseContextKind's own trailing
   `Return ContextBadge.ContextKind.CONFIRMED`) rather than reaching
   ContextBadge's dead `Case Else`. SAFE, but note the CONFIRMED fallback for
   an unrecognised context string is itself a quiet mismatch if the engine
   ever emits a context tag the UI enum hasn't been told about yet (str->enum
   maps for BOTH OiCvd and Context are hand-kept in sync with the engine's
   own string constants; nothing enforces that mechanically -- a `grep`, not
   a type system, is what would catch drift here).

5. RegimeAnchorWarn / ComputeRegimeAnchorWarning (MainForm_Render_Cards.vb:1503-1518):
     ATR=120 > 0, and R:R 0.39 -> Verdict is not STRONG LONG/STRONG SHORT
     (too weak to reach that tier) -> function returns "" regardless of
     atrUnits -> RegimeAnchorWarn.WarningText="" -> Visible=False, Height=0. SAFE.

6. OiCvdBadge (OI=0 -> OiCvdOutcome never set to a CONFIRMED_*/CONFLICT_* value
   by ScoringEngine_Calculate_Scoring.vb:534-546 -> stays at the VerdictResult
   field default) -> BindCardOiCvdCross's `If(v.OiCvdOutcome, "NONE")` +
   MapOiCvdOutcome's Case Else -> OiCvdBadge.Outcome = NEUTRAL. SAFE. This
   control is reconstructed fresh (`New OiCvdBadge()`) every successful call
   to BindCardOiCvdCross, so it carries no state across runs on its own.

7. MiniMeter x2 (Funding Mom, Spread) inside the same BindCardOiCvdCross call:
   Funding Mom pct is a fixed 20/70 heuristic (ResolveFundMomMagnitude) -- not
   data-driven, SAFE by construction. Spread pct = (r.SpreadBps / wideThresh)*100,
   guarded by `If wideThresh > 0`. Empty absorption / OI=0 do not touch
   r.SpreadBps, so THIS scenario's numbers are ordinary and SAFE -- see
   Finding 3 for the separate (not-given, but real) NaN path via a zero-mid
   order book.

8. TapeStripLabel (the live TAPE strip) is NOT fed by this run's v/r at all --
   it is driven by LiveMicrostructureEvaluator.Evaluate against the live
   MarketState on its own timer (MainForm_LiveStrip.vb:1-20, architecture.md
   Directory Layout). "Empty absorption fields" in THIS run's IndicatorResults
   has no direct bearing on the strip text. Pushing the closest live analogue
   (a burst-active, absorption-tagged strip line, which is exactly the kind of
   reading a -1.5% flush would actually produce on the live feed) through
   TapeStripLabel.OnPaint is what surfaces Findings 1 and 4 below.

9. FlatButton / Pill / AnalysisReportButton / RoundedCardPanel / SectionGroup /
   LinkRow / ChipNumeric / PaintHelpers: none of these read v/r/norms/cfg
   directly in any call site found (grepped every `New <Control>(` site across
   UI/). They are chrome (buttons, section boxes, the stale-pill, tool links,
   settings-numeric-updown). The flush scenario has no distinguishable effect
   on any of them. Confirmed safe against Nothing/negative/zero/very-large
   inputs by direct reading of every settable property (see §2, "swept and
   cleared" list).
--- END LOGIC TRACE ---
```

## 2. Findings

Ranked most severe first. Severity scale per the brief: S0 wrong-side/unprotected/unbounded
order · S1 systematic negative-EV or stale-data orders, or a silent total outage · S2 realistic
trigger, bounded impact, or a contract gap · S3 edge case, latent path, or advisory inconsistency
· S4 nit.

---

### Finding 1 — TapeStripLabel paints unrelated trailing tags in the BURST accent colour

**SEVERITY:** S2 — realistic trigger (burst + any trailing tag), bounded to a display-only,
advisory surface.

**LOCATION:** `UI/Controls/TapeStripLabel.vb:42-49` (the highlight-boundary split), interacting
with `UI/MainForm_LiveStrip.vb:216-236` (`ComposeLiveStrip`'s tag ordering) and `:303-313`
(`ComposeTape`, where BURST is embedded).

**DOWNSTREAM IMPACT:** The live TAPE strip is the one surface explicitly designed to stay
*"NEUTRAL (never the verdict colour ramp) ... so it reads as a readout, not a call"*
(`TapeStripLabel.vb:8-11`) and to highlight *"just the burst word"* because *"a plain Label has
one ForeColor"* (`TapeStripLabel.vb:6`). When a burst is active at the same time as an absorption,
cascade, or level-approach tag — exactly the co-occurring conditions a real flush produces (a
burst of aggressive flow into a level that's absorbing it, or into a liquidation cascade), the
absorption/cascade/approach tag is not a coincidence next to a burst; it gets rendered in the same
amber "attention" colour as the burst itself, visually implying they're one call-out. A trader
scanning the strip for the amber highlight sees `BURST↑ · ABS↑ 60510 (3.4×)` as a single amber
unit, not as "burst (highlighted) then a separate, neutral absorption reading."

**FAILURE SCENARIO:** `ComposeLiveStrip` always appends `ComposeTape(s)` (which embeds `"BURST↑"`
or `"BURST↓"` when `s.HasBurst`) and then, conditionally and *after* it in the same joined string,
`ComposeAbsorption(s)` when `s.HasAbsorption`, `ComposeCascade(s)` when a cascade is active, and
`ComposeApproach(s, ...)` for each active approach side. `TapeStripLabel.OnPaint` finds the
*first* occurrence of the literal `"BURST"` and paints from there to the end of the string in
`BurstColor` (`TapeStripLabel.vb:42,49,61`) — there is no logic anywhere that stops the highlight
at the end of the tape segment. Any run where a burst and a later tag are simultaneously active
reproduces this.

**ANALYTICAL CRITIQUE:** The control's design intent (highlight *only* the burst word) was true
the day `ComposeTape` was the last thing appended to the strip (the file's own comment says "the
trailing aggressor-velocity BURST segment ... renders in an accent colour" — written when BURST
*was* trailing). Absorption (`[P4 #6]`), cascade (`[#7 v59]`), and approach (`[#8 v59]`) tags were
added to `ComposeLiveStrip` *after* that assumption was baked into `TapeStripLabel`, and nothing
re-verified it. This is the same class of drift the display-string-parity rule exists to catch on
the card/snapshot surfaces — except the TAPE strip is explicitly *outside* that rule's scope
(`MainForm_LiveStrip.vb:29`: "NOT an RTF/snapshot/card surface"), so nothing enforces it here.
**Proven by a run** — see `docs/audits/proofs/ui-controls/README.md`, "Finding A."

---

### Finding 2 — persistent verdict-card controls can display a stale prior-run value with no staleness cue

**SEVERITY:** S2 — depends on the already-reported missing try/catch as its trigger, but is a
distinct, code-verified exposure specific to which of the 14 audited controls are vulnerable.

**LOCATION:** `UI/Controls/ContextBadge.vb`, `UI/Controls/MtfRow.vb`, `UI/Controls/RegimeAnchorWarn.vb`
— all three are built **once** as persistent `MainForm` fields
(`MainForm_Render_Cards.vb:209-212`, `:218-222`, `:260-264`) and then mutated in place, every run,
inside `BindCardVerdict` (`MainForm_Render_Cards.vb:936-1009`), which is the **second** of twelve
`BindCard*` calls in `RunAnalysisAsync` (`MainForm_Analysis.vb:663-675`).

**DOWNSTREAM IMPACT:** Given the context that a bind-time exception suppresses this run's bridge
payload and halts auto-run with a modal (already reported: no try/catch around the binds), the
open question these three specific controls answer is *what does the trader see on screen while
that modal is up, and after they dismiss it*. Because these three are mutate-in-place singletons,
not rebuilt per run, they keep showing whatever they last successfully rendered — with **no**
visual distinction from a fresh, correct render. This is not the same failure surface as the
codebase's own designed staleness indicator: `RenderSkippedDashboard` → `ApplyStaleOverlayToCards`
(`MainForm_Render_Cards.vb:355-376,454-495`) paints a `(stale)` `Pill` over the cards, but it is
only ever invoked from the network-resilience skip branch (`grep` confirms its only caller is
`RenderSkippedDashboard`, itself only called from the "any required fetch result is Nothing"
path). A bind-time exception is caught **only** by `btnAnalyze_Click`'s outer `Try` (`MainForm_
Analysis.vb:33-49`), which shows a `MessageBox` and returns — it never routes through
`ApplyStaleOverlayToCards`. So the one class of failure this brief is specifically about (a bind
exception mid-run) is also the one class the codebase's own stale-marking mechanism does not cover.

**FAILURE SCENARIO:** If any of the ten `BindCard*` calls that run before `BindCardVerdict`
(only `BindCardScore` runs earlier) throws, `BindCardVerdict` never executes this cycle and
`_contextBadge`/`_mtfRow`/`_regimeAnchorWarn` (plus `_lblRegime`/`_lblHold`/`_lblHoldReason`/
`_lblVerdictEffPenalty`) all retain the previous run's values, unflagged. If the throw instead
happens partway through a *future* change to `BindCardVerdict` itself (I confirmed by reading
every statement in the method against the given flush values — `ResolveVerdictColour`,
`ParseContextKind`, the `r.Regime` Select Case, `ApplyMtfRow`, the `HoldStatus` branch, and
`ComputeRegimeAnchorWarning` are all Nothing-guarded and Select-Case-with-Else today, so **I found
no live throw site under the given scenario** — this half of the finding is a structural exposure,
not a currently-triggerable one), the controls assigned *before* the throw point get fresh data
and the ones *after* stay stale, in the same card, with nothing to distinguish which is which.
Contrast with `BindCardOiCvdCross`'s pattern (`OiCvdBadge`/`MiniMeter`, Finding 3's home): it calls
`.Controls.Clear()` then rebuilds from scratch every run, so a throw partway through that specific
method leaves the OI×CVD card **visibly empty** rather than plausibly wrong — arguably a *better*
failure mode for a trader to notice, purely as a side effect of how that one card happens to be
implemented, not by design.

**ANALYTICAL CRITIQUE:** The already-reported "no try/catch" finding says the payload can be
suppressed. This finding says which specific on-screen controls are structurally unable to signal
that anything happened, and that the codebase already has a designed answer for "this data might
be stale" — it simply isn't wired to this failure path. That's a narrower, more actionable gap
than "add a try/catch": even with error isolation added around individual binds, these three
controls would still need an explicit "mark stale" branch in the catch handler to avoid quietly
showing old data as current. **Read from code**, not run — no WinForms harness was built for this
one since it requires reproducing the full `MainForm` field/render-order graph, not just a control
class in isolation.

---

### Finding 3 — `MiniMeter.Pct`'s range clamp does not catch `NaN`; a corrupt spread reading renders as an empty, "healthy-looking" bar

**SEVERITY:** S3 — edge case (requires a degenerate order book), bounded to a display-only meter,
partially self-revealing (the adjacent numeric label does print "NaN").

**LOCATION:** `UI/Controls/MiniMeter.vb:54-64` (the `Pct` property setter) and `:119-129`
(`OnPaint`'s fill-rectangle computation), fed from `MainForm_Render_Cards.vb:2076-2088`
(`BindCardOiCvdCross`'s Spread meter).

**DOWNSTREAM IMPACT:** The Spread `MiniMeter` in the OI × CVD CROSS card is meant to give an
at-a-glance read of how wide the current spread is relative to the configured "wide" threshold —
a meaningful microstructure-health signal, especially in the illiquid/dislocated conditions a
flush produces. If the underlying value is corrupt (not just extreme), the bar silently shows
**zero fill** — visually indistinguishable from "spread is fine, well inside the threshold" —
rather than any error state.

**FAILURE SCENARIO:** `Pct`'s setter is `If v < 0 Then v = 0` / `If v > 100 Then v = 100`
(`MiniMeter.vb:59-60`). Under IEEE-754, both comparisons are `False` when `v` is `NaN`, so `NaN`
passes through unclamped and is stored in `_pct`. `OnPaint` then computes
`fillW = ClientSize.Width * (_pct / 100.0F)` = `NaN`, and `If fillW > 0 Then` is `False` for `NaN`
(`MiniMeter.vb:124-125`), so `FillRectangle` is simply never called — no exception, an empty bar.
The call site computes `spreadPct` as `(r.SpreadBps / wideThresh) * 100.0`, guarded only against
`wideThresh <= 0` (`MainForm_Render_Cards.vb:2077-2081`); it is not guarded against `r.SpreadBps`
itself being `NaN`, which a zero-mid order book (`BestBid = BestAsk = 0` ⇒ `0 / 0`) would produce
upstream, per `SpreadBps`'s documented formula in `docs/architecture.md:310`. Meanwhile the
adjacent value label (`MainForm_Render_Cards.vb:2083-2085`) formats the same `r.SpreadBps` with
`"F2"`, which prints the literal text `"NaN bps"` — a legible, if odd, tell that *is* visible next
to the falsely-empty bar, which is the main reason this is S3 rather than S2.

**ANALYTICAL CRITIQUE:** The setter's guard reads like a defensive clamp but is only a *range*
clamp, not a *validity* clamp — a common `Single`/`Double` trap (`NaN` compares `False` against
everything, including itself). The fix isn't specific to this control; every numeric prop-setter
clamp in this file family (`MiniMeter.Pct`, and by the same reasoning any future `Pct`-like
property) needs an explicit `Single.IsNaN` check if "silently draw nothing sensible" is not the
intended behaviour for corrupt input. **Proven by a run** — see
`docs/audits/proofs/ui-controls/README.md`, "Finding B." The upstream reachability
(`SpreadBps` = NaN) is read from code, not independently re-derived — `DeribitClient`/order-book
parsing is outside this audit's file list.

---

### Finding 4 — `TapeStripLabel.OnPaint` has no zero/negative-size guard and computes a negative-width `tailRect` when the strip overflows its fixed column

**SEVERITY:** S4 — proven not to crash; a latent, currently-invisible clipping artifact layered
on top of Finding 1.

**LOCATION:** `UI/Controls/TapeStripLabel.vb:30-62` (the entire `OnPaint` override).

**DOWNSTREAM IMPACT:** None beyond what Finding 1 already covers, plus: in the extreme case where
the composed strip text is long enough that the *prefix* alone (everything before `"BURST"`)
already exceeds the label's available width, the highlighted tail (`BURST↑`/`BURST↓` plus
whatever Finding 1 says gets dragged along with it) is not drawn at all — GDI clips a
negative-width rect to nothing — so the strip visually truncates with no burst indication showing,
even though `HasBurst` is true and the underlying signal is live.

**FAILURE SCENARIO:** Every other control audited here (`SectionGroup.vb:104`, `FlatButton.vb:137`,
`Pill.vb:80`, `RoundedCardPanel.vb:95`, `RegimeAnchorWarn.vb:89`) opens `OnPaint` with
`If Me.ClientRectangle.Width <= 0 OrElse Me.ClientRectangle.Height <= 0 Then Return`.
`TapeStripLabel.OnPaint` has no such guard. `lblLiveStrip` is docked into a fixed-width
`TableLayoutPanel` cell with no `AutoSize` (`MainForm_Layout.vb:756-765`, a 24px-tall row shared
with two checkboxes), while `ComposeLiveStrip`'s output length is unbounded and grows by one
`" · <tag>"` segment for every simultaneously-active signal (TFI, spread, book imbalance, tape+burst,
absorption, cascade, two approach sides — up to nine segments). `pw` (the measured prefix width,
`TapeStripLabel.vb:55-57`) can exceed `ClientRectangle.Width`, making
`tailRect.Width = ClientRectangle.Width - pw` (`TapeStripLabel.vb:58-60`) negative, with no
clamp anywhere in the method.

**ANALYTICAL CRITIQUE:** This is the one finding in this report backed by an actual runtime test
of the "would this throw" question, specifically because a claim about crash behaviour deserved
more than a read of the .NET source: `TextRenderer.DrawText` did **not** throw for a negative-width
`Rectangle` on this runtime (see proof `README.md`, "Finding A2") — it degrades to a silent
no-draw, consistent with the Win32 `DrawTextEx` contract, not an exception. So the missing guard
is an inconsistency with every sibling control's defensive style, not a crash risk on its own; its
only concrete effect is compounding Finding 1's miscoloured tail with occasional total invisibility
of that same tail. **Proven by a run** — see `docs/audits/proofs/ui-controls/README.md`,
"Finding A2."

## 3. Controls swept and cleared (no exploitable path found)

Read in full, checked against Nothing/negative/zero/very-large/empty-string inputs at every public
settable property and every `OnPaint`, and against every real call site found by grepping `New
<ClassName>(` across `UI/`:

- `OiCvdBadge.vb` — enum-typed `Outcome`, mapped from string via a `Select Case ... Case Else`
  helper (`MapOiCvdOutcome`) that always returns a defined member; the control is reconstructed
  fresh every successful bind, so it carries no cross-run state of its own.
- `SectionGroup.vb` — `Title`/`TitleColor`/`AccentColor` all Nothing-guarded or value types; the
  one candidate defect (`rect` height can go negative when `Me.Height < 21`) is defended by
  `PaintHelpers.RoundedRect`'s own `bounds.Height <= 0` early return.
- `FlatButton.vb` / `AnalysisReportButton.vb` — pure chrome, no data-derived properties; every
  `Lighten`/`Darken`/`Blend` call uses hardcoded amounts, never a data-derived one.
- `Pill.vb` — `CornerRadius` setter has no negative clamp (unlike `FlatButton`/`RoundedCardPanel`),
  but every call site (`MainForm_Layout.vb:917,1260`, `MainForm_Render_Cards.vb:483`) passes a
  hardcoded positive literal; not reachable with today's call sites.
- `RoundedCardPanel.vb`, `LinkRow.vb`, `ChipNumeric.vb` — settings/chrome surfaces, not fed by
  analysis values in any call site found.
- `Helpers/PaintHelpers.vb` — `RoundedRect` defends zero/negative bounds and zero/negative
  diameter explicitly (with a second belt-and-braces check); `DrawGlow`'s `intensity`/`spread`
  are always hardcoded at call sites.

## 4. Proof code

`docs/audits/proofs/ui-controls/Program.vb.txt` + `ProofRunner.vbproj.txt` (renamed to keep the
root solution's `**/*.vb` glob from picking them up) — see
[`proofs/ui-controls/README.md`](proofs/ui-controls/README.md) for the exact run command and the
output obtained.
