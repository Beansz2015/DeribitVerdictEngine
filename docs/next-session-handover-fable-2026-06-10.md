# Handover → Fable 5 (spec-author / bug-checker seat) — 2026-06-10

**You (Fable 5) are taking over as the continuous spec-author + bug-checker for this project.** You write specs, triage findings, make the calls, and **spin off separate conversations for code writing** — you decide per task whether that implementer is Opus or Fable (see §9). The previous spec-author seat was held by Opus (4.7 then 4.8); this doc transfers it to you with full context.

**Time constraint:** the user has access to you (Fable 5) only **until 2026-06-22**. Spend your turns on the high-leverage work only you should do — spec authorship, bug triage, judgement calls — and push mechanical implementation to spun-off conversations. §9 has a model-assignment framework; you own it.

**Authority:** this doc gives you observations, confirmations, and recommendations. **The decisions are yours.** Where the previous seat had a recommendation, it's labelled as such — weigh it, don't just inherit it. The user explicitly wants you to advise on direction (§3).

---

## 0. The two immediate things the user wants you to decide

1. **Direction.** There are two open work tracks: (A) finishing the **UI reskin** (visual sign-off + cleanup + P5b deletion + Spec C), and (B) acting on a just-completed **engine audit** that surfaced two CRITICAL and several HIGH correctness bugs. The user **leans toward engine** given your limited availability, but wants your read. Advise them. §3 + §5 give you what you need.
2. **The engine audit's central fork.** The CRITICAL bugs (CVD/MicroCVD chronological inversion, stale volume window) are real and confirmed — but the engine was *calibrated on top of them*, so fixing them is a recalibration event, not a clean patch. And the auto-tweaker is pending its first fire. Deciding how/whether to sequence this is the biggest call on the board. §5.3 + §5.4.

Everything below is to let you make those two calls well.

---

## 1. How to operate (project rules — these bind you and any conversation you spin off)

These come from `CLAUDE.md` + `docs/trader-profile.md`. They are not optional.

- **Spec-first.** Novel features / scoring changes get a proposal `.md` in `/docs` before any code. Implementation follows an approved spec; implementers don't invent design.
- **Scoring engine is approval-gated.** No change to `Core/ScoringEngine_*.vb`, `Core/Indicators_*.vb`, `DynamicNorms.vb`, or `settings.json` ships without the user's explicit approval. You may *propose*; you may not greenlight scoring changes unilaterally. (This is why the engine audit, below, is a triage-and-propose exercise, not a fix-it-now.)
- **Trader-profile rejected patterns are absolute** (profile §4). Never reintroduce: Stochastic, MACD, CMF, fixed-% targets, ATR-based *execution* stops (ATR for sizing/reference is fine), non-directional reward components, double-counting a signal across scoring layers, or flat regime-transition penalties (use ADX-proximity scale). If a finding tempts one of these, flag it explicitly with the evidence; don't slip it in.
- **Conservative bias wins ties.** The engine should say NO TRADE rather than emit a weak directional signal. False-positive tolerance is LOW.
- **Local commits only. Never push.** The user tests, then pushes. Remote = tested milestones only. No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- **settings.json version is strictly monotonic.** Any config key change bumps `version` + appends a `change_log` entry (newest first). Currently **v30**.
- **Self-screenshot is the default visual-verification path.** `tools/screenshot-mainform-full.ps1` captures the whole form; crop-and-zoom the cards (the full-form fit view hides clipping — see §4 lesson). `tools/README.md` has the loop.
- **Host-agnostic rule.** Code in `analysis/` and `tools/` must have zero `System.Windows.Forms` / `Control.Invoke` / `MainForm` references (Linux CLI port is on the roadmap). The audit confirmed this is currently clean.

---

## 2. Session start protocol (read in this order)

1. **`CLAUDE.md`** (repo root) — architecture, data flow, design invariants, collaboration rules.
2. **This handover.**
3. **`docs/fable5-audit-report.md`** — the engine audit you wrote in a prior conversation. This is the core of track B.
4. **`docs/trader-profile.md`** — §3 (preferred indicators), §4 (rejected), §5 (risk), §6 (philosophy). Mandatory before proposing engine changes. (If you have the `crypto-trading-context` skill available, load it — it carries this plus writing style.)
5. **`docs/DeribitIndicatorProject.md`** §1-3 + §15 — engine state + version history. On-demand for specifics.
6. **`docs/ui-reskin-handover-2026-05-27.md`** — only if you choose track A (reskin). It's the reskin's continuous handover; §3 roadmap + §4 locked decisions are the load-bearing parts.

Don't read individual `.vb` files at session start. Open them when a specific decision needs them.

---

## 3. Project state — two tracks

### Track A — UI reskin (~95% complete, in a verification window)

A card-grid reskin replacing the legacy RichTextBox output. Status:
- P1→P4f shipped (theme, palette, 14 custom controls, all card bindings). P5a shipped (`BuildPlaintextSnapshot`). Screenshot-reliability tooling shipped.
- **P5-test harness** shipped: drove 55 synthesised cases through legacy vs snapshot renderers, **55/55 text parity**. Then a **gap-fix spec** (`docs/ui-reskin-p5-test-gap-fixes-proposal.md`) was drafted and **implemented in 4 commits** (`7c8de56`→`465b257`) — ~30 card-binding/format fixes from the user's visual review.
- **Where it's stuck:** the gap-fix work has **not had the user's visual sign-off on the 55 regenerated PNGs**, and the previous seat's own visual pass (2026-06-10) **found one real bug**: the ATR ENTRY LEVELS card rows **clip the new third content line** — both the Q2 `(risk N / rwd N)` line and the C1b cap-reason `(label)` are cut off (the row pixel budget wasn't raised when the content line was added). Structural cards show risk/rwd fine. This needs a row-height fix before cleanup. There's also a minor design question (CAPPED price duplicates TARGET price).
- **Remaining reskin sequence after sign-off:** fix the ATR clipping → user visual sign-off → ship P5-test **cleanup commit** (delete harness files + `Ctrl+Shift+T` + `tools/send-ctrl-shift-t.ps1`) → **P5b** deletion sweep of the legacy RTF pipeline (kickoff drafted: `docs/ui-reskin-p5b-kickoff.md`) → **Spec C** SC/TOTAL parity (proposal: `docs/sc-column-total-parity-proposal.md`).

**Reskin work does not touch scoring.** It's UI-only and lower-risk. The question is whether it's worth your limited time vs the engine.

### Track B — engine audit (just completed, independently confirmed)

A separate Fable 5 conversation audited the engine (read-only) and produced `docs/fable5-audit-report.md`: 13 findings (2 CRITICAL, 4 HIGH, 7 MEDIUM), 6 suspected, ~14 low/style, 7 new-function ideas. **The previous Opus seat independently re-read the source behind 11 of the findings and confirmed all 11 — zero misses.** Details in §5. This is the higher-impact, higher-risk track.

---

## 4. Process lessons carried forward (apply to anything you spin off)

- **Real type names.** Quote enum/property names from the actual source, not memory. The codebase uses VB `SCREAMING_SNAKE` enums and `snake_case` JSON. Grep before quoting.
- **Card row heights are absolute pixels** — adding a content line without bumping row height silently clips with no build error (this is exactly the ATR bug in §3 track A). Any card-content kickoff must require build → run → screenshot → **crop-and-zoom** → measure.
- **Crop-and-zoom for visual review.** The full-form fit-scale screenshot hides clipping. The previous seat only caught the ATR clip by cropping the card 2×. Standardise this.
- **Trivial parity isn't parity.** A synthesised `VerdictResult` built via setters leaves engine-emitted collections empty, so renderers show blank rows on both sides → false "parity." Populate every engine-emitted field explicitly in fixtures.
- **Kickoff staleness.** Even a 2-day-old kickoff drifts (collaborator call-sites move, signatures change). Any kickoff you write should include a "verify against current tree before commit 1" step.
- **Research-back engine-policy questions.** When the user asks "should the engine do X?", do the web research on the trader's segment (momentum scalper, crypto perps) and map it to the current architecture before recommending — don't answer from priors. (Precedent: the C1a stop-capping decision.)

---

## 5. The engine audit — confirmed findings, the calibration trap, triage

Full detail: `docs/fable5-audit-report.md`. This section is the confirmed summary + the previous seat's observations. **Every line/mechanism below was re-verified against source by the Opus seat on 2026-06-10.**

### 5.1 Confirmed findings (verified, not taken on faith)

| ID | Sev | One-line | Verified how |
|----|-----|----------|--------------|
| **C1** | CRIT | CVD slope chronologically inverted: trades arrive `sorting=desc` (newest-first), but `weightedSlope = late×2 − early` weights the *oldest* third double — opposite of the documented recent-emphasis. `DeribitClient.vb:256` + `Indicators_OrderFlow.vb:167-190`. | read + auditor ran a harness |
| **C2** | CRIT | MicroCVD same root cause: `Take(50)` window is correct (newest 50) but within-window early/late labels are inverted → ACCEL/DECEL backwards. `Indicators_OrderFlow.vb:260-320`. | read + harness |
| **C3** | (evidence) | TFI `Take(30)` is correct *only because* the list is `desc`. Proves the codebase holds two contradictory ordering assumptions; a naive "reverse the list" fix breaks TFI/MicroCVD window selection. | read |
| **H1** | HIGH | Volume baseline computed from the **oldest** 100 of 250 1m candles (`Take(100)` on an ascending list) → thresholds describe conditions 2.5-4h stale. `DynamicNorms.vb:31`. | read + harness |
| **H2** | HIGH | MTF "hard veto" evaluated against the **1m-regime-proposed** direction, not the verdict direction. When order-flow overrides the 1m read, the gate both wrongly blocks with-trend trades and wrongly passes counter-trend ones. `MainForm_Analysis.vb:315-322` + `Verdict.vb:68-96`. | read |
| **H3** | HIGH | Step 5 tier cascade walks all LONG tiers before any SHORT, with no dominance comparison. In RANGE_BOUND/TRANSITIONAL (Step 4 dominance veto covers only TRENDING) a weak long beats a stronger short; CONTEXT line then describes the *other* side → incoherent output. `Verdict.vb:109-124` + dominance veto at `:33-66`. | read |
| **H4** | HIGH (display) | Kelly contracts always 0: `riskPerContract = $10 face × stopDist` needs stop ≤ $5 for ≥1 contract. Root cause is a Deribit **inverse-contract** dimensional error (should be `face×Δ/price`). `ScoringEngine_Kelly.vb:94`. | read |
| **M1** | MED | OBV trend normalised by first-bar volume; zero when first two closes are equal → OBV dead for the whole run. `Indicators_Structure.vb:50-52`. | read + harness |
| **M2** | MED | Donchian window includes the current bar → full breakout needs `close == window-max-high`; only quartile partials ever fire. `Indicators_Structure.vb:25`. | read |
| **M3** | MED | `settings.json` write is non-atomic `File.WriteAllText`; parse failure silently runs on POCO defaults that have **drifted** (OI threshold POCO 0.01 vs live 0.002 = 5×; funding momentum POCO 0.0001 vs live 0.00001 = 10×). `SettingsLoader.vb` + `EngineSettings.vb:280,372`. | read + values confirmed |
| **M4-M7** | MED | CSV culture-sensitivity (Linux-port risk), tweaker stall on CSV shrink, tweaker typo'd-path silent no-op, fixed-index CSV parse. See report. | read |
| **S-1** | susp | Stop/target distances scale **quadratically** with vol (`ATR × (ATR/ATRRef) × mult`). Math confirmed; *intent* is the open question. Plus a display-semantics trap: the displayed scale is the reciprocal of the profile's sizing formula. `Verdict.vb:130` + `DynamicNorms.vb:90`. | read |

The remaining suspected/low items (S-2..S-6, doc drifts, magic-number notes) are in the report; not re-verified line-by-line but consistent with a high-accuracy audit.

### 5.2 Trader-profile impact (why these matter to *this* trader)

CVD, MicroCVD, and TFI are all on the **preferred** list and feed the trader's hold/exit decisions. C1/C2 mean: during a 2-15 min hold, a *recovering* position can show "EXIT — microstructure deterioration" and a *deteriorating* one can show "HOLD." That's directly adverse to the documented workflow. H2/H3 can produce wrong-direction or wrongly-vetoed headline verdicts in exactly the conflicted-chop reversal moments the trader cares about. These aren't cosmetic.

### 5.3 The calibration trap (the most important thing in this doc)

**The engine was tuned (v14-v22 threshold sweeps, and the auto-tweaker) on logged CSV data that already contains the C1/C2/H1 behaviour.** Consequences:
- Fixing C1/C2/H1 changes the *meaning* of the `CVDSlope`, `MicroCVD*`, and `VolumeRatio` columns. Historical CSV rows stop being comparable.
- Thresholds tuned against inverted signals are partly *compensating* for them. A clean fix can de-tune the engine.
- So C1/C2/H1 are a **recalibration event**, not a patch. Any fix spec must include a re-baseline plan (freeze a pre-fix CSV snapshot, re-run sweeps post-fix, re-validate the WATCHING items: CVD slope_min, MicroCVD accel, session multipliers).

### 5.4 Time-sensitive: hold the auto-tweaker

The auto-tweaker is **pending its first fixed-window fire**. If it fires now, it tunes *against* the inverted signals — digging the hole deeper. The previous seat's strong recommendation: **hold the auto-tweaker until the C1/C2/H1 direction is decided.** This is the one action worth taking regardless of the larger fork. Confirm with the user.

### 5.5 Suggested triage tiers (previous seat's framing — your call to adopt or revise)

- **Tier A — real scoring bugs, recalibration-coupled** (C1, C2, H1): spec-first + re-baseline plan. Fix vehicle is the auditor's S1 `NormalizeTrades` (normalise ordering once at the fetch boundary; convert TFI/MicroCVD `Take(N)` to take-from-end).
- **Tier B — scoring-logic bugs, change verdicts** (H2 MTF direction, H3 tier cascade): cleaner than A but still shift output; spec-first. H2's fix is the auditor's S2 (compute `gatePassLong`/`gatePassShort`, consult the one matching the dominant side).
- **Tier C — safe, no scoring/calibration impact** (M3 atomic write + parse-fail surfacing, M4 invariant CSV formatting [do before Linux port], M5 tweaker stall, M6 typo'd-path validation, M7 header-map CSV parse): can proceed spec-light. M3/M4 protect the very dataset everything else depends on — strong candidates to do first regardless of the fork.
- **Tier D — display/doc/cleanup** (H4 Kelly, S-1 sizing display, doc drifts): fold into a post-P5b engine-hygiene proposal. H4 supersedes the older "B14 always-<1-contract" note with a better root cause.

### 5.7 Data-reset option — user has pre-authorised a clean restart (IMPORTANT)

On 2026-06-10 the user **volunteered to clear the entire trade/auto-tweaker/CSV history and restart data collection from scratch** if it helps long-term calibration and makes the engine fixes cleaner. They are also fine with keeping the data initially (if Fable needs it to work with) and deleting it later.

**Why this matters:** it removes the single hardest objection to fixing C1/C2/H1. The calibration trap (§5.3) exists *because* the historical tuning is entangled with the bugs. If the user is willing to discard the contaminated calibration and restart clean, the fix decision changes from "fix + reconcile incomparable history" to **"fix → collect known-clean data → recalibrate from a clean baseline."** That's the methodologically correct path and it's now on the table.

**Previous seat's recommended handling (your call to adopt/revise):**
- **Sequence: fix first, then reset — not before.** Until C1/C2/H1 ship, new runs keep writing contaminated `CVDSlope`/`MicroCVD*`/`VolumeRatio` columns. The reset belongs **at the fix commit**, not now. Clearing earlier just collects more bad data on a fresh file.
- **Archive, don't delete.** Move pre-fix `analysis_log.csv` + eval/picked-cell caches to a dated folder (e.g. `data-archive/pre-orderfix-YYYYMMDD/`). The contaminated history still has one use — quantifying how much the bugs moved historical verdicts (informs the recalibration spec). Delete the archive later per user preference. **Raw OHLC cache is not contaminated** (candles are inputs; the bug is in windowing) — no need to clear it.
- **Reset auto-tweaker state *with* the CSV, same moment.** `LastEvaluatedRowIndex` / round history / picked-cell history are tied to the log; archiving the CSV without resetting tweaker state triggers the M5 stall. Reset together at the fix commit.
- **Post-reset baseline question:** the current settings.json v30 thresholds were tuned on contaminated data, so they're a questionable starting point post-fix. Consider starting the clean-data recalibration from looser/more-conservative defaults and letting fresh sweeps tighten them. This is a recalibration-spec decision — flag it for the user.
- **Honest caveat for the user:** their stated bottleneck is *data, not UI*; a fresh dataset takes calendar time to accumulate regime variety, and BTC's macro bear (since Oct 2025) means fresh data skews short until conditions shift. The engine runs on conservative defaults for a while as clean data builds — acceptable (conservative bias is profile-aligned) but not instant.

This option should be folded into the §6.2 fix-vs-accept decision: with a clean restart pre-authorised, "fix C1/C2/H1 properly + recalibrate on clean data" becomes materially more attractive than "document as known and live with them."

### 5.6 New-function ideas worth proposing (from the audit §5)

- **S1 `NormalizeTrades`** — the Tier-A fix vehicle.
- **S5 score-ledger reconciliation guard** — add `ScoreDeltaLong/Short` per `SignalBreakdownItem`, assert Σ == ls/ss, log `LEDGER_MISMATCH`. **This is the standout** — it turns the additive-pipeline invariant into a checked property and would make a future double-count regression (the profile's #1 banned pattern) impossible to ship silently. Also note: it **overlaps Spec C** (which already adds per-item `LongPoints`/`ShortPoints`) — consider merging S5 into Spec C rather than two passes.
- **S3 `AtomicWriteJson`**, **S4 invariant CSV formatter** — Tier C.
- **S6 candle freshness guard**, **S7 time-windowed momentum** — conservative-bias and cadence-correctness; S7 changes signal cadence so it needs the standard spec + re-check.

---

## 6. Open strategic decisions (yours to make / advise)

1. **Track direction** (engine vs reskin) — §0.1. User leans engine. The reskin is lower-risk and closer to done; the engine is higher-impact but recalibration-heavy and approval-gated. A defensible split: do the **Tier-C safe fixes** (which protect the calibration dataset) + write the **Tier-A/B specs** with a re-baseline plan now while you have Fable's analytical strength, and leave reskin cleanup/P5b (mechanical) for spun-off Opus conversations. But it's your call.
2. **C1/C2/H1 fix-and-recalibrate vs accept-as-calibrated** — §5.3. The honest framing for the user: these are real bugs, but the engine's current behaviour is "calibrated around them." Options range from full fix+recalibration to documenting them as known and only fixing the cleaner H2/H3/Tier-C items. **Weigh this together with §5.7 — the user has pre-authorised a clean data restart, which tilts strongly toward the full fix+recalibrate path** (a clean dataset removes the "incomparable history" objection). Research-back it if the user wants a recommendation.
3. **Auto-tweaker hold** — §5.4. Recommend yes; confirm with user.
4. **S5 ↔ Spec C merge** — §5.6. Decide whether the ledger guard is its own proposal or folds into Spec C's field migration.

---

## 7. What's locked (don't re-litigate without new data)

From the reskin handover §4 + this audit. Highlights:
- Single dark theme; form width 1100px (1280 hard ceiling); 14 custom controls, paint-carve-out rules.
- Legacy `txtOutput` deletes in P5b; `MainForm.Designer.vb` `txtOutput` field declaration stays (becomes a zombie).
- **ATR stops stay uncapped** (research-backed); CAPPED indicator is targets-only; the C1c "STRUCT|STOP worse-of" flag is display-only.
- **DMI/ADX SC under-reporting defers to Spec C** (the card clamp is a known band-aid; Spec C's per-item points migration is the real fix).
- MTF Gate absent from card SIGNAL BREAKDOWN by design (it's a veto, not a scoring contributor).
- Full locked list in `docs/ui-reskin-handover-2026-05-27.md` §4.

---

## 8. File reference index

**Engine audit:** `docs/fable5-audit-report.md` (findings) · `docs/fable5-engine-audit-brief.md` (the brief that produced it).
**Reskin:** `docs/ui-reskin-handover-2026-05-27.md` (continuous handover) · `docs/ui-reskin-p5-test-gap-fixes-proposal.md` (just-implemented) · `docs/ui-reskin-p5-test-visual-review-handoff.md` (the trader's raw review) · `docs/ui-reskin-p5b-kickoff.md` (drafted) · `docs/sc-column-total-parity-proposal.md` (Spec C, drafted).
**Engine source (open on demand):** `Core/Indicators_*.vb`, `Core/ScoringEngine_*.vb` (Types/Helpers/_Scoring/_Verdict/_Kelly), `DynamicNorms.vb`, `DeribitClient.vb`, `SettingsLoader.vb` + `Core/Settings/EngineSettings.vb`, `AnalysisLogger.vb`, `UI/MainForm_Analysis.vb` (orchestration).
**Auto-tweaker:** `tools/AutoTweaker/*` (separate host-agnostic `.csproj`).
**Tools:** `tools/README.md` + the screenshot/UIA helpers.
**Audit harness:** `verify/ordercheck/` (gitignored; links real sources, re-runnable with `dotnet run` — the auditor's acceptance fixtures for C1/C2/C3).

---

## 9. Model assignment framework (you own this)

The user wants you (Fable) to decide whether each spun-off code-writing conversation is Opus or Fable. A working heuristic — adjust it:

- **Keep in Fable (you, in this seat):** spec authorship, bug triage, the calibration-strategy call, anything synthesis-heavy or judgement-heavy. This is where your value is highest and your time is scarce (until 2026-06-22).
- **Spin off to Opus:** mechanical implementation with a tight spec — reskin cleanup/P5b deletion sweep, the Tier-C safe fixes, card row-height fixes. Opus is available past your window and is well-suited to spec-driven mechanical work; conserve your turns.
- **Spin off to Fable:** implementation that needs in-flight judgement or where a subtle scoring/calibration interaction could bite (e.g. the Tier-A normaliser, if you want the implementer to reason about TFI/MicroCVD window semantics rather than follow a rote spec).
- **Always:** the implementer conversation gets a self-contained spec/kickoff; it doesn't inherit this context. One implementer per kickoff. Local commits only; user tests and pushes.

Be explicit in each kickoff which model it's for and why, so the user can route it.

**Warm engine implementer available.** The conversation that produced `docs/fable5-audit-report.md` (a prior Fable 5) read 21 engine files in full, ran the `verify/ordercheck` harness, and holds deep warm context on the scoring/indicator code. **If it's still open, it's the natural implementer to route the Tier-A/B engine fix specs to** — it executes fast with its existing context + the harness already set up, no re-derivation. Keep it as a *worker*, not the spec-author seat: this seat (you) stays the independent decision-maker so the conversation that *found* the bugs isn't also the one deciding what to do about them (preserves the find-vs-decide separation that made the audit trustworthy). If that conversation is closed, no loss — this handover + the audit report reconstruct what an implementer needs.

---

## 10. Previous seat's candid notes to you

- The audit you (a prior Fable conversation) produced is genuinely strong — 11/11 confirmed against source, accurate line numbers, correct mechanisms, and sophisticated caveats (the calibration coupling, the TFI-correct-because-of-`desc` subtlety, window-selection-vs-segment-labeling). Trust it, but the user values the spec-first + approval-gated discipline — keep proposing, not unilaterally fixing scoring.
- The single highest-leverage thing you can do with limited time: **write the Tier-A/B fix specs with the re-baseline plan baked in**, so the actual fixes (recalibration-heavy, slow) can proceed in spun-off conversations and forward in time past your window. The specs are the durable artifact; the keystrokes are cheap.
- Don't let the engine findings stampede the user into a fix. The calibration trap means a rushed fix can make live trading *worse* before recalibration catches up. The conservative path (hold the tweaker, freeze a baseline, spec carefully) is the trader-profile-aligned one.
- If the user picks engine over reskin, the reskin isn't lost — it's at a clean pause point (gap-fix implemented, just needs the ATR clip fix + sign-off + mechanical cleanup). An Opus conversation can finish it from the existing docs without you.

---

**End of handover.** Open this in a fresh Fable 5 conversation alongside `CLAUDE.md`, `docs/fable5-audit-report.md`, and `docs/trader-profile.md`. Make the §0 calls, advise the user on direction, and drive from there.
