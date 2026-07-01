# Next-Session Handover — 2026-07-01 (coordinator / spec-author seat)

Supersedes `next-session-handover-2026-06-30.md`. Same seat: write specs, review implementer spec-backs (re-run the gate + diff), do small `analysis/`/settings/tooling passes directly. **Local-first: commit as you go, NEVER push — the trader tests + pushes.**

## 0. Start protocol
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` + `docs/architecture.md`; load the `crypto-trading-context` skill; read the frontier memory **`project_websocket_migration`** (the WS→P4 arc, current through the geometric v46 + the on-close fix). Then `git log --oneline -15` + `git status`. Session tooling in memories **`project-dev-workflow-automation`** + **`reference-ui-automation-tools`**.

## 1. Frontier — P4 #4 time-averaged OFI is DONE (geometric); next is the v47 re-baseline
- **v46 shipped as GEOMETRIC** (log-ratio EMA), not arithmetic. The NY DIAG test decided it: on a 616-row / 5.1h **net-flat** NY session, arithmetic gave a **12:1** buy-dominant skew vs geometric **1.4:1** on the same book — firing-rate-match can't fix a distribution-*shape* bias. Implemented + coordinator-**APPROVED** (`816c443` revert DIAG / `9740d7b` geometric / `7f4fe37` spec-back / `f6d4883` review): 3 Release builds 0/0, harness A1–A20i ALL PASS, DIAG fully reverted, docs amended **in place**, **no version bump** (folds into unpushed v46 → dataset boundary on the final construction). Live-confirmed (OFI 2.17 geometric; ≠ bid/ask = the by-design D1 cosmetic).
- **IMMEDIATE WORK = the v47 OFI re-baseline** (data-gated, coordinator-driven): the production `analysis_log.csv` now logs the **geometric** `OFIRatio`. Collect multi-session → firing-rate-match `buy/sell_dominant_ratio` (2.0/0.5 → a symmetric split; the geo distribution centers ~1.1) + review `OFI.Momentum*`. Wants **Asia/London (3m) + more days**, not just NY — tonight's NY geo distribution (from the DIAG) is a preview only.
- Then P4 **#5 aggressor velocity** (scoring; spec vs TFI for correlation) + **#6 book absorption**.

## 2. Also done this session
- **Dev-workflow automation** — `tools/checks/verify-gate.ps1` (one-command gate) + pre-push hook (installed) + CI (`.github/workflows/verify.yml`, windows-latest, public repo = free) + advisory Stop hook + display-parity/version heuristics. **`verify/ordercheck` is now COMMITTED** (the old "verify/ is local-only" convention is dead). See [[project-dev-workflow-automation]].
- **TraderGuide + UserManual refresh** through v46 (Sonnet seat; `4e83815`/`c8eeb74`/`7caef9a`) — coordinator-reviewed; harness confirmed intact after an accidental `rm -rf verify/`-and-`git checkout` restore.
- **On-close backstop-floor fix** (`b0dd815`) — the feed-stall backstop floors to `(execRes+1)`min so a short NUD can't pre-empt the bar close (a 1m NUD on 3m bars was firing every minute); countdown shows the effective backstop.

## 3. Push state (the trader pushes)
Everything since origin is LOCAL + gate-green + UNPUSHED: the P4 #4 geometric v46 stack, the automation, the doc refresh, the on-close fix. The DIAG (`eee6e4b`) is reverted (`816c443`) — nothing held back. On push, the pre-push hook + CI gate it. (If you'd rather scrub the DIAG from history than revert it, that's a rebase; otherwise the revert is the clean record.)

## 4. Parked / working rules
- **Parked (unchanged):** auto-tweaker first NY×1 fire (data-gated), D7 reach-target calibration, §12 volume-spike watch, optional WS-health-line log persistence. 3 pre-existing dirty docs (`p3-maintenance`, `ui-reskin-handover`, `websocket-migration-p1-spec-back`) are excluded from every commit.
- **`bin/Debug` config:** the trader is on `on_close` now (toggled live); the earlier interval/30s DIAG-test config is superseded.
- **Rules:** spec-first for scoring/novel (trader sign-off); `analysis/`+display+tooling safe to proceed; **local-first, NEVER push**; host-agnostic `analysis/`/`tools/`/WS; **display-parity card ↔ `BuildPlaintextSnapshot`**; delete test screenshots; re-baseline = firing-rate-match (v40/v41); co-author trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. **One-command gate:** `tools/checks/verify-gate.ps1 -Mode prepush` (3 Release builds 0/0 + harness A1–A20i + parity/version heuristics).
