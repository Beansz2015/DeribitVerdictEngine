# Funding Momentum — Time-Anchored Window: Spec-Back

**Date:** 2026-07-15 · **Settings:** v52 → **v53** · **Seat:** Opus, medium, one conversation
**Spec:** `funding-momentum-time-anchored-window-proposal.md` (APPROVED 2026-07-07, D1–D5 ticked)
**Brief:** `funding-window-implementer-brief.md`
**Trigger:** `signal-health-retune-proposal.md` §5 watch finding (first pass 2026-07-06)

Built as specced. Values final and unchanged from the spec: **W = 5 min**, **T = 2e-7**.
Gate green: 3 Release builds 0/0, harness **ALL PASS** (A1–A28d unregressed + the new A29a–e), display-parity no drift, `verify-gate.ps1 -Mode prepush` **GATE PASSED**.

---

## 1. What shipped

**Mechanism** (`Core/Indicators_OrderFlow.vb`):

- `CalcFundingMomentum(history As List(Of (UtcMs As Long, Rate As Double)), nowUtcMs As Long, cfg)` — the ring is timestamped; the anchor is the **newest** sample aged ≥ `momentum_window_minutes`; `delta = current − anchor.Rate` classified against ±`momentum_threshold`; no anchor (cold start / post-gap) ⇒ **FLAT**. Tuple shape exactly as the spec §3 wrote it.
- `AppendFundingSample(history, nowUtcMs, rate, Optional maxAgeMs = FundingRingMaxAgeMs)` — append every run, no dedup, evict age > 30 min. See §2 D1 (scope addition).
- Step 3b is **untouched** — same crowding gate, same ±amplify/soften. Only its input state changes meaning.

**Host plumbing:** `_fundingHistory` is now `List(Of (UtcMs As Long, Rate As Double))` in `MainForm_Layout`; the call site in `MainForm_Analysis` appends through the shipped helper and passes `nowTs` (hoisted above the funding block so funding and OI stamp the same run instant, rather than two `UtcNow` reads microseconds apart).

**Settings v53:** `indicators.funding.momentum_window` (count, 3) removed — key **and** POCO field. New `momentum_window_minutes` = 5. `momentum_threshold` stays 2e-7. change_log entry carries the dataset note incl. the bundle statement; `§15` row + `§12` watch row + `§4` funding row updated.

**Fixtures** (`verify/ordercheck`, A29a–e): anchored RISING/FALLING/FLAT + strict-threshold edge; newest-≥W anchor selection (with the discriminating case — oldest-in-ring would read RISING where newest-≥W reads FLAT); cold-start, degenerate rings, and post-gap FLAT; 30-min eviction boundary + the no-count-cap pin; **cadence invariance** (30s vs 180s over one funding path → identical states at identical instants).

---

## 2. Deviations

Three, all recorded. None touch the mechanism, the values, or the acceptance criteria.

### D1 — `AppendFundingSample` added to `IndicatorEngine` (scope addition)

**Spec/brief said:** host-agnostic surface is "`CalcFundingMomentum` + call-site plumbing; the ring state moves wherever the run-context owns it today."
**Shipped:** also a pure `AppendFundingSample` helper alongside it, holding the append-every-run + 30-min-eviction rule. The ring itself still lives host-side, as specced.
**Why:** §7 requires a **30-min eviction** fixture and a **cadence-invariance** fixture. Eviction happened at a WinForms call site, which the host-agnostic harness cannot reach — so both fixtures would have had to re-implement eviction inside the harness and thereby test a *copy* of the rule rather than the shipped one. That is precisely the failure mode the display-parity rule exists to prevent, in fixture form. With the helper, A29c/d/e drive the real path.
**Side benefit:** it shortens the W4 run-state extraction (`cli-port-run-state-extraction-proposal.md` §3 Stage 1) — `AppendFunding` becomes a delegation rather than a verbatim block move. That proposal's §2 inventory row and §3 Stage-1 bullet were factually restated (see D3).
**Cost:** one public Sub + one public Const on an already-public host-agnostic class. No behaviour change.

### D2 — `MomentumWindowMinutes` typed `Double`, not `Integer`

**Spec said:** `momentum_window_minutes` (5) — no type pinned.
**Shipped:** `Double`. The sibling precedent (`OFI.avg_window_sec`) is `Integer`, but at 10-second units that is already fine-grained; at *minute* units `Integer` would make the tweaker's smallest possible step 5→4, a 20% jump in a parameter the threshold is fit *given*. `Double` costs nothing and keeps the tweaker's step proportionate. JSON `5` deserialises cleanly either way.

### D3 — Two docs corrected beyond the brief's list

The brief named change_log + §15 + §12. Also updated, because they assert things about funding-momentum that the build made **false** (not because they wanted improving):

- `docs/UserManual.md` — the funding-momentum calculation block described the ring buffer, the v14 dedup, and `MomentumWindow (3)`. All three now wrong. Rewritten, with a plain-language note on why the window is measured in minutes and that pre-v53 rows aren't comparable.
- `docs/architecture.md` — ring type, data-flow rows, `Indicators_OrderFlow` inventory, and the two funding design-decision rows (one of which explicitly documented "a short rolling window (default 3 samples)").
- `docs/cli-port-run-state-extraction-proposal.md` — §2 inventory row 1 and §3 Stage-1 bullet. This is an **unshipped** spec, so the edit is deliberately confined to *factual* restatement of what the run state now is; no design decision of W4's was touched. Flagging it rather than silently leaving it: a future W4 implementer reading "`List(Of Double)`, max 10, [S9] append on change only" would have rebuilt the retired window. The funding spec §3 anticipated this ("named in the W4 run-state extraction inventory; the two specs compose").

---

## 3. Judgement calls worth surfacing

**The count cap had to go, and that's a correctness point, not tidying.** The brief said retire the count-based *window semantics*; it didn't mention `FundingHistoryMax = 10`. Keeping it would have been quietly fatal: at a 30s cadence, 10 samples span 5 minutes, so the cap would evict the very samples a W=5min anchor needs and pin the state at FLAT — the exact bug class the build exists to remove, reintroduced through the ring cap. Age eviction alone bounds the ring (≤ 60 entries at the fastest cadence the engine has run), so the cap has no remaining job. Pinned by the A29d no-count-cap fixture.

**The `[S9]` dedup is retired, not preserved.** It existed because identical samples filled a *count*-indexed ring and forced FLAT. Under age-anchoring the inverse holds: repeat samples are informative, because "funding hasn't moved in W minutes" genuinely *is* FLAT. Spec §3 says this explicitly; noting it because it reads like a deletion of a fix and isn't.

**`momentum_window` was NOT added to `RejectedPathFragments`** — per the brief, and worth restating for the next reader. Removal makes the path applier-unresolvable, so C-6 (`SettingsDiffApplier.Validate`, "does not resolve in current settings") rejects it with no fence needed; A21a already pins that path for a removed key. Adding the fragment would have been actively harmful twice over: it re-creates the v47-F1 snapshot-poisoning class, **and** `RejectedPathFragments` is substring-matched, so `"momentum_window"` would also swallow the new `momentum_window_minutes` and silently take the intended tweaker key off the surface. The new key is on-surface as specced (`indicators.ofi.momentum_` is a *prefix* fence and doesn't reach `indicators.funding.*`); the window↔threshold coupling caveat is recorded in the POCO comment and the change_log — **T is fit given W; re-fit T if W moves.**

**A29e's probe path was moved off the threshold.** The first draft put a probe where `delta` landed exactly on T=2e-7 — which decides by float noise, not by logic. Rates were re-picked so every probe sits far from T. Worth knowing *why* the fixture looks the way it does: at the 21- and 30-min probes the two cadences select **different anchors** and compute **different deltas** (1.0e-6 vs 1.2e-6) and still agree on the state. That is the actual invariance claim — same *states*, not same deltas — and a fixture that demanded equal deltas would be pinning something the spec never promised. A29e also pins the expected state sequence, so "invariant" cannot pass by being uniformly wrong.

**HC ledger: no new fence.** Next free stays **HC23**. None was needed, as the brief expected.

---

## 4. Title-bar rider (trader request 2026-07-15)

`MainForm_Layout` constructor no longer hardcodes the caption. It is set immediately after `SettingsLoader.Initialise` (it has to be — `Initialise` runs later in the same constructor than the old literal did) to:

```
Deribit Verdict Engine — settings v{SettingsLoader.Current.Version}
```

**Load-time only**, which the brief permits over a hot-reload hook. Judgement: `SettingsLoader` has no reload event today, and adding one plus a cross-thread `Invoke` (the `FileSystemWatcher` fires off the UI thread) is real surface for a caption. Not worth it inside a ⚠ scoring commit.

The `"Deribit Verdict Engine"` prefix is **load-bearing and deliberately preserved** — `tools/screenshot-mainform.ps1` finds the window by substring on it (its own comment says the prefix is kept stable "so the helper survives version bumps"). Verified the helper still matches.

Note `MainForm.Designer.vb:320` still sets `"Deribit Verdict Engine v0.40"`; the constructor overwrites it. Designer file is auto-generated and out of bounds, and the stale literal there is inert.

**Title bar is not a rendered analysis surface — no parity obligation**, stated here and in the commit message per the brief.

---

## 5. Acceptance vs §7

| §7 requirement | Status |
|---|---|
| 3 Release builds 0/0 | ✅ solution + AutoTweaker + OrderCheck |
| `verify-gate.ps1 -Mode prepush` green | ✅ GATE PASSED (builds / harness / display-parity / version-bump) |
| A-series unregressed | ✅ A1–A28d ALL PASS |
| Anchored classification fixture | ✅ A29a (+ strict-threshold edge) |
| Cold start + post-eviction → FLAT | ✅ A29c (incl. degenerate rings + a 40-min outage through the shipped path) |
| Anchor = **newest** ≥W, not oldest | ✅ A29b (discriminating: oldest-anchor would read RISING) |
| 30-min eviction | ✅ A29d (boundary: >30 evicted, exactly-30 kept) + no-count-cap pin |
| **Cadence invariance** (30s vs 180s → same states at same instants) | ✅ A29e (+ expected-state pin) |
| Display: no line added/removed/renamed | ✅ same three states, same format — snapshot/card/payload untouched; parity gate confirms no drift |
| CSV: no header rotation | ✅ `FundingMomentum` semantics documented; `FundingDelta` = per-run step (D3), column kept |

**Not verified here (by design):** live behaviour. The collector is on the Debug exe and this build is Release-only per the brief — the trader's Debug build + restart at the test gate **is** the v53 activation.

---

## 6. Handover

- **Local-only. NOT pushed** (per the brief and the standing workflow — trader tests, then pushes).
- Working tree was checked before starting (07-15 duplicate-commit lesson): no other implementer lane open; the pre-existing untracked `.codex/`, `configure-claude-deepseek.ps1`, `models-full.json`, `tools/tools/` were left alone.
- **The watch is now live**, not queued: `DeribitIndicatorProject.md` §12 row rewritten from "Approved (builds post-#5-gate)" to the per-resolution post-ship watch — FLAT 60–70% + Step-3b engagement 15–25%, **both res-1 AND res-3 in-band = success**. Re-fit trigger is a *regime-independent* miss (both resolutions out of band, same direction, 2 weekday sessions) — a single hot-funding week reading above band is expected and is not a trigger.
- Reminder for whoever reads the book next: **v52 and v53 share one dataset boundary**, and rows before it carry the cadence-dependent funding state (res-3 `FundingMomentum` uninformative).
