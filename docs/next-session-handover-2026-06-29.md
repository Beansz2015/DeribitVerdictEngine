# Next-Session Handover — 2026-06-29 (coordinator / spec-author seat)

You're the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. Write specs, review implementer spec-backs (re-run builds + harness + audit the diff), sequence the work, do small `analysis/`/settings/tooling passes directly. **Local-first: commit locally, NEVER push — the trader tests + pushes.**

## 0. Start protocol
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load the `crypto-trading-context` skill (carries the trader profile + writing style); read the frontier memory **`project-websocket-migration`** (it carries the whole WS→P4 arc and is current). Then `git log --oneline -15` + `git status`.

## 1. Frontier (2026-06-29) — WS migration COMPLETE; P4 display wave shipped; the first scoring re-baseline (#4) is APPROVED + ready for an implementer
- **Versions:** settings **v45** (live-strip) · CSV v0.7 · eval cache v4. #4 targets **v46** (build) then **v47-ish** (re-baseline).
- **origin/master = `f8d3b47`** — the full WS + #2 on-close + #3 strip stack is pushed (trader re-pushed 2026-06-29 + GitHub backup). **2 unpushed local doc commits:** the #4 spec (`00b876c` proposal + `0469319` APPROVED) — trader pushes (rides with the #4 build).
- **WS migration: DONE + validated.** P1→cutover (v42, `transport=ws`) shipped, monitored, and closed (cutover sound — sufficiency check passed on a ~5.6h/~672-run NY×1 auto-run). The engine is live on WS. *(Note: the ShadowParityComparer is a `transport=rest`-only instrument — it's skipped live-on-WS; early-WS monitoring was the WS-health line + skip count + the §12 volume-spike watch, NOT parity.)*
- **P4 display trio: ALL implemented + coordinator-reviewed + pushed** — #1 realtime exit guard (v43), #2 on-close analysis mode (v44), #3 LIVE microstructure strip (v45). #10 (WS-health line) shipped back in P2. The zero-scoring display wave is **complete**.
- **§8 sub-minute cadence: DEPRIORITIZED** (conflicts with #2's bar-close discipline + it's the worst calibration cadence — `on-close-analysis-mode-proposal.md` §12). Not a prerequisite; revisit only if the trader wants sub-minute responsiveness.

## 2. THE IMMEDIATE WORK — P4 #4 time-averaged OFI (first ⚠ scoring re-baseline)
`docs/time-averaged-ofi-proposal.md` — **APPROVED 2026-06-29, §10 settled, ready for a fresh implementer.** Replaces snapshot OFI with a time-weighted top-book-imbalance average over the run window (WS-only; snapshot fallback at `transport=rest`). **Handle as a build→collect→re-baseline arc, TWO versions:**
- **v46 (the implementer's job NOW):** build the time-averaging mechanism (feed-side accumulator → averaged `OFIRatio`) behind a dated OFIRatio dataset boundary; `averaging_enabled=false` is the byte-identical rollback. **Do NOT re-derive the dominance thresholds** — they stay 2.0/0.5; OFI will fire a bit *less* in the interim (expected, not a bug). §11 has the file map.
- **v47-ish (a LATER, data-gated pass — the coordinator drives it):** once enough averaged-OFI data accumulates (multi-session, like v40/v41), re-derive `OFI.buy/sell_dominant_ratio` via **firing-rate-match to the snapshot-OFI history**. Its own spec-back + trader sign-off.
- **Trader directive (settled):** `avg_window_sec` is ON the auto-tweaker surface (it shapes the OFI signal) alongside the dominance ratios; only `averaging_enabled` (feature switch) is off-surface. Coupling caveat: a window change shifts the ratio distribution → a manual window change needs a ratio re-check.

**Your sequence:** (a) trader hands the APPROVED spec to an implementer → (b) you review the v46 spec-back (Release builds 0/0 solution+AutoTweaker+OrderCheck + the A-harness incl. new accumulator fixtures + diff audit; confirm `averaging_enabled=false` byte-identical + the engine path otherwise unchanged) → (c) trader live-tests v46 ("averaging behaves + rollback clean", NOT "thresholds right") + pushes → (d) collect data → (e) you drive the v47 re-baseline.

## 3. P4 backlog after #4 (the remaining ⚠ re-baseline upgrades, §11 catalogue)
- **#5 aggressor velocity / tape burst** — the scoring sibling of #3's tape-speed readout; must be specced **vs TFI** for correlation (may *upgrade* TFI rather than join it — profile's anti-correlation rule).
- **#6 book absorption at structural levels** — resting-size depletion at the active swing without price progress (breakout-vs-fakeout filter).
- Each its own spec + re-baseline. Source: `websocket-migration-proposal.md` §11.

## 4. Parked / separate workstreams (NOT blocking #4)
- **Auto-tweaker first live NY×1 fire** — data-gated on a real >40%-failure NY×1 window; HELD.
- **D7 reach-target / barrier-vs-window calibration** — parked, transport-invariant (`post-websocket-post-calibration-backlog.md` D7).
- **§12 volume-spike standing watch** — does the WS 3-min closed-bar ~2.5% volume undercount ever pull a reading under the 3×SMA-9 breakout-confirm gate. Passive.
- **Optional polish:** persist the WS-health line to a log (so post-hoc feed-health is direct, not inferred from run-continuity).
- 3 pre-existing dirty docs (`p3-maintenance`, `ui-reskin-handover`, `websocket-migration-p1-spec-back`) — unrelated; every commit excludes them.

## 5. Working rules + key facts
- Spec-first (novel/scoring → committed `/docs` proposal); **scoring changes need trader sign-off** (`analysis/`+display+tooling safe to proceed); **local-first, NEVER push**; host-agnostic `analysis/`/`tools/`/WS feed; **display-parity hard rule** (card ↔ `BuildPlaintextSnapshot`); delete test screenshots; the skill's ATR bands are STALE → use [[reference-atr-bands-v37]]. Co-author trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Live app reads the **bin** `settings.json` (gitignored).
- **Re-baseline method = firing-rate-match** (v40/v41): match the new signal's fire-rate to the old one's history (same selectivity, cleaner trigger), not ATR-rescaling.
- **Cadence is a calibration dimension** (on-close §12): calibrate on the cadence the engine actually runs.
- **Tweaker HARD CONSTRAINTS 11–15** exclude `execution_resolution` / `min_tradeable_move` / `network.*` / `exit_guard.*` / `auto_run.*` / `live_strip.*` (+ `kelly.*`). #4 adds: exclude only `OFI.averaging_enabled`; keep `avg_window_sec` + dominance ratios tunable.
- **Build/harness:** Release dodges the running-app Debug exe lock. `dotnet build -c Release` on the solution + `tools/AutoTweaker/AutoTweaker.vbproj` + `verify/ordercheck/OrderCheck.vbproj`, then `dotnet run -c Release --project verify/ordercheck/OrderCheck.vbproj` runs the A-series fixtures (currently through **A19e**). Each coordinator review = re-run all three builds 0/0 + the harness + audit the diff (watch for the display-parity card drift + stale change_log sub-lines — both have bitten before).
