# TraderGuide + UserManual Refresh — Spec-Back

**Built:** 2026-06-30, against `traderguide-usermanual-refresh-spec.md` (PROPOSED, no sign-off gate — pure docs work).
**Scope honoured:** docs-only. No code, settings, or CSV change. No `settings.json` version bump. No `DeribitIndicatorProject.md` §15 entry (not a behaviour change).
**Status:** Both docs updated through settings v46, live-render eyeball pass complete, local commits only — trader reviews + pushes.
**Commits (local, NOT pushed):** `4e83815` (main refresh) + `c8eeb74` (eyeball-pass fixes).

---

## 1. What shipped

`docs/TraderGuide.md` and `docs/UserManual.md` updated to match the live app through settings v46. Followed the spec's §0 start protocol (handover doc + architecture.md + `crypto-trading-context` skill + `settings.json` change_log v32→v46 skim) before editing.

### §2 delta items (all done)

| Item | TraderGuide | UserManual |
|---|---|---|
| v36 execution resolution (NY 1m / Asia-London 3m) | ATR Entry Levels section, `EXEC` tag explained | ATR Entry Levels §2 + Dynamic Norms §4, `r.ExecResolution` documented |
| v37 ATR bands | Dynamic Norms bullet, current bands stated | Dynamic Norms §4, `ATR.StaticRef` 115→38 history + current bands |
| v40/v41 ROC re-baseline | ROC(9) bullet, per-session 0.17/0.11 | ROC(9) §6, full mechanism (`ResolveRocMagnitudeForHour`) |
| WS migration (v38–v42) | new "WebSocket Health" subsection under §17 | new **§22 WebSocket Health Status Line** |
| P4 #1 exit guard (v43) | new "Exit Guard" subsection | new **§23 Realtime Exit Guard** |
| P4 #2 on-close mode (v44) | new "On-Close Trigger Mode" subsection | new **§24 On-Close Trigger Mode** |
| P4 #3 TAPE strip (v45) | new "Live Microstructure Strip (TAPE)" subsection | new **§25 Live Microstructure Strip (TAPE)** |
| P4 #4 time-averaged OFI (v46) | OFI bullet + cosmetic ratio≠bid/ask note | OFI §12, full mechanism + cosmetic note + open re-baseline flag |
| Stale references | n/a (TraderGuide never named files) | "Source of truth" line: `MainForm_Render_Header.vb`/`Sections.vb` (retired P5b) → `MainForm_Render_Cards.vb` + `BuildPlaintextSnapshot`; v24 → v46; intro reframed off "RTF output pane" |

### Extra fixes found during the source/live pass (not in §2, but the same sections were already open)

These were factually wrong as written and sat inside sections the spec already had me touching, so I fixed them rather than leave a known-wrong statement next to corrected text:

1. **ATR stop/target sizing (v32 D2).** Both docs described ATR-based stop/target distances as stretched/compressed by current volatility (`r.ATR × ATRScaleFactor × mult`). That was true pre-v32; v32's D2 fix made the distances linear (`r.ATR × mult` only) — the old scale factor survives purely as a display sizing figure (`size ×N` in the ATR header, `ATR ratio` in Dynamic Norms). Rewrote both sections accordingly.
2. **Dynamic Norms "ATR scale" → "ATR ratio".** Same v32 D2 commit relabelled the display row. Updated both docs' field name and the explanation of what it now means (sizing-context only, not applied to stop/target).
3. **Kelly contract-sizing "known formula limitation" (v32 D1).** UserManual's §3 stated the formula omits entry price (a known simplification). That was fixed in v32 D1 — `riskPerContract` now correctly divides by `entryPriceUsd`, and a new `kelly.max_leverage` cap + `Notional`/`[LEV CAPPED]` line was added. Replaced the stale limitation note with the actual current formula and the new Notional/leverage subsection in both docs.
4. **`BELOW_MIN_MOVE` context tag (v35) was entirely undocumented.** Added to both docs' CONTEXT tag lists/tables (it's a real, user-facing `VerdictContext` value the existing docs never mentioned).

---

## 2. Live-render eyeball pass

Per the spec's acceptance criteria ("spot-check 2-3 sections against the live app... delete any screenshots afterwards"). Built Debug (`dotnet build DeribitVerdictEngine.sln`, 0/0), launched the exe, and drove it with `tools/screenshot-mainform*.ps1` + `tools/inspect-mainform-tree.ps1` + `tools/click-mainform-button.ps1` + `tools/select-mainform-radio.ps1` (no computer-use needed — these survive a non-foreground window).

**Environment constraint:** this sandbox has no outbound network to Deribit. The first analysis attempt skipped ("Engine retains last-known indicator values..."), and the WS feed never connects (`DeribitWsFeed` constructs but can't dial out) — so dynamic/live-data states (`WS OK · ...`, a latched Exit Guard, live TAPE numbers) could not be observed end-to-end. Cross-checked those against the literal `String.Format` source instead (100% reliable for static text, just not a substitute for seeing them paint).

### Confirmed live (screenshot + UIAutomation tree, exact text matched what's now in the docs)

- **ATR Entry Levels header**: `ATR ENTRY LEVELS  (ATR 24.60  size ×1.89  |  1.2× stop / 2.0× target  |  EXEC 3m)` — confirmed `EXEC <res>m` tag and the `size ×N` sizing figure, both newly documented.
- **Capped target line**: `59360.7 → 59333.4  (NEAREST_HVN_ABOVE)` — caught and fixed a placeholder example (`[swing]`) that didn't match the real literal reason labels (`SWING_HIGH_5M` / `NEAREST_HVN_ABOVE` / `POC`).
- **Dynamic Norms**: `ATR ratio:  0.53x  (ATR=24.60 | ref=46.44)` — confirmed the v32 rename.
- **Kelly**: `Notional:  ≈ $5,000 · 5.0× lev  [LEV CAPPED]` — confirmed the v32 D1 line and tag; caught and fixed an ASCII `x` vs literal `×` glyph mismatch in both docs' examples.
- **TAPE strip (live numbers)**: `59321 · SH 59301 (−20) | HVN↑ 59333 (+12) · TFI SELL −1.00 · 0.1 bps · book 1.2× bid · 1 tr/s ($3.0k/s)` — confirmed the field order and composition documented in UserManual §25.
- **TAPE inert state**: `WS only` (the checkbox is separately labelled `TAPE`, no `TAPE ·` prefix on the data label) — caught and fixed a doc claim that the inert text reads `TAPE · WS only`.
- **OFI breakdown note**: `ratio 1.97 · bid $808.5K ask $507.7K` — confirmed the §12 cosmetic note's `ratio · bid · ask` claim.
- **On-close UI**: `BACKSTOP` relabel (from `AUTO EVERY`) and the `INTERVAL`/`ON-CLOSE` radio toggle both render as documented.
- **`Next close` countdown format** — not visible live (reparented into the scrolled-off SETTINGS & TOOLS region the UIAutomation tree walk didn't reach in this pass), but the source literal (`BuildOnCloseCountdownText`) was read directly: `Next close: M:SS  [SINGLE|REPEAT <res>m]`. Both docs were missing the bracket suffix — added.

### Not independently observed live (network-gated; confirmed by source read only)

- `WS OK · 1/3/5/15 fresh · trades N` / `WS DEGRADED — REST fallback (stream stale)` / `WS DOWN — reconnecting (Xs, R reconnects)` — exact literals from `BuildWsStatusSegment` in `MainForm_Layout.vb`, matched verbatim in §22.
- Exit Guard's three states (`clear` / `⚠ EXIT? confirming n/d` / `⚠ EXIT — <reason>`) — the strip itself never rendered in this pass (declaring a position surfaced no visible row; most likely the same scrolled-off-viewport issue as the countdown label, not a code bug — `lblExitGuard` is reparented into the same SETTINGS & TOOLS row group per `MainForm_Layout.ReparentSettingsToolsControls`). Confirmed by source read (`MainForm_ExitGuard.vb` `RenderExitGuard`) instead.

### Cleanup

Closed the app (`taskkill`), reset position state to "No Position" and stopped auto-run before closing. Deleted the screenshot PNGs afterward. **One process note for the record:** an over-broad `rm -rf verify/` briefly deleted the *committed* `verify/ordercheck/` acceptance-harness project (it shares the gitignored screenshot directory) — caught immediately via `git status`, restored with `git checkout -- verify/ordercheck/` before doing anything else. Confirmed clean after: `git status` shows only the two doc files touched, `settings.json` has zero diff (the on-close radio toggle either didn't persist before the forceful `taskkill` or round-tripped back to `interval` — no dirty state either way).

---

## 3. Acceptance (spec §4)

- Both docs reference v46 and the current render surfaces. ✅
- Every P4 display element (WS-health, exit guard, on-close, TAPE) + averaged OFI covered. ✅
- ATR bands updated to v37. ✅
- No mention of retired Header/Sections as live surfaces. ✅
- Spot-checked against the live app (screenshot + UIAutomation tools) — 4 real discrepancies found and fixed in the process (see §1 "extra fixes" + §2 "confirmed live" catches). ✅
- Screenshots deleted afterward. ✅
- Docs-only commits, local, not pushed. ✅ (`4e83815`, `c8eeb74`)

---

## 4. Not done / left for the trader

- **Push.** Both commits are local-only per the project's commit workflow — review, then push when ready.
- **WS-health / Exit Guard / TAPE live-data states** weren't visually confirmed end-to-end (no network in this sandbox). The static text is verified correct from source; if you want a true paint-it-on-screen check, that needs a network-connected run.
- No other open items — this closes the docs-refresh kickoff.
