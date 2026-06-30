# Next-Session Handover — 2026-06-30 (coordinator / spec-author seat)

Supersedes `next-session-handover-2026-06-29.md`. Same seat: write specs, review implementer spec-backs (re-run builds + harness + diff), do small `analysis/`/settings/tooling passes directly. **Local-first: commit as you go, NEVER push — the trader tests + pushes.**

## 0. Start protocol
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` + `docs/architecture.md`; load the `crypto-trading-context` skill; read the frontier memory **`project_websocket_migration`** (the WS->P4 arc, current through v46). Then `git log --oneline -15` + `git status`. **New memories this session:** **`project-dev-workflow-automation`** + **`reference-ui-automation-tools`**.

## 1. Frontier — P4 #4 (time-averaged OFI) v46 built + APPROVED; the D2 test gates v47
- v46 build (settings) implemented (`ab97f40`) + coordinator-APPROVED (`e226318`, spec-back §8): 3 Release builds 0/0, harness A1-A20h ALL PASS, `averaging_enabled=false` byte-identical, time-aware EMA/warmup/reset faithful. The ⚠ dominance-threshold **re-baseline (v47) is DEFERRED + data-gated**.
- **IMMEDIATE WORK = the D2 arith-vs-geo decision.** The averaged `OFIRatio` is an arithmetic EMA of a multiplicatively-symmetric ratio -> a standing buy-lean (Jensen; worst under load). A throwaway DIAG instrument is built (`eee6e4b`, **DO-NOT-PUSH**) — a lockstep geometric EMA in `OfiAccumulator` + `Core/OfiGapDiagnostic.vb` side-log gated by env `DVE_OFI_GAP_DIAG`. **The trader is running NY (30s interval, overnight, env set) and will send `C:\Dev\ofi_gap_ny.csv`.** Next coordinator step: parse it — gap vs activity (under load) + BD/SD symmetry -> keep arithmetic (negligible) or switch `OfiAccumulator` to a log-ratio EMA (material) BEFORE the long v47 collection. Then `git revert eee6e4b`. Full detail in the frontier memory.
- D1 (cosmetic, already flagged to trader): the OFI card shows `ratio · bid · ask` where the averaged ratio != bid/ask — by design, not a bug.

## 2. Also shipped this session
- **Dev-workflow automation** (Items 1-3; `1ae65b8`/`4d2390a`/`c6c89fe`/`4987b6a`/`76f9620`): `tools/checks/verify-gate.ps1` + pre-push hook (installed) + CI (windows-latest, public=free) + advisory Stop hook + parity/version heuristics. **`verify/ordercheck` is now COMMITTED** (the old "verify/ is local-only" convention is dead). Item 4 (subagents) skipped (budget). See [[project-dev-workflow-automation]].
- Skill-bundled trader-profile ATR bands synced to v37 (1m 20/55, 3m 42/115); the repo `docs/trader-profile.md` was already current.

## 3. Pending / parked
- **TraderGuide.md + UserManual.md refresh** through v46 — spec written: `docs/traderguide-usermanual-refresh-spec.md`. Recommend a fresh **Sonnet 4.6 / medium** seat (guided, lower cost). NOT yet done. (Both docs are stale: UserManual cites "v24" + the retired Render_Header/Sections; neither covers v32-v46.)
- **Push sequencing:** unpushed stack = #4 build (`ab97f40`) + review (`e226318`) + DIAG (`eee6e4b`, revert at/before push) + the proposal/automation commits. The trader pushes after the NY read + DIAG revert; the pre-push hook + CI gate it.
- Parked (unchanged): auto-tweaker first NY×1 fire (data-gated), D7 reach-target calibration, §12 volume-spike watch, optional WS-health-line log persistence. 3 pre-existing dirty docs (`p3-maintenance`, `ui-reskin-handover`, `websocket-migration-p1-spec-back`) are excluded from every commit.

## 4. Working rules
Spec-first for novel/scoring (needs trader sign-off); `analysis/`+display+tooling safe to proceed; **local-first, NEVER push**; host-agnostic `analysis/`/`tools/`/WS; **display-parity card <-> `BuildPlaintextSnapshot`**; delete test screenshots; co-author trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Re-baseline method = firing-rate-match (v40/v41). Build/harness: 3 Release builds 0/0 + `dotnet run -c Release --project verify/ordercheck` (through A20h) — or just run `tools/checks/verify-gate.ps1`.
