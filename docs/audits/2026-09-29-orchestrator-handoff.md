# Audit handoff to the project orchestrator (2026-09-29)

**What this is.** The adversarial audit of the engine and the order app is finished, apart from one review lane (L-7) and four external checks (X-1 to X-4). The trader has passed it to you. Two decisions are yours: whether to finish those five items, and how to decide and sequence the fixes. This page tells you where everything is and what's left. The substance is in the report.

The audit ran in a cloud session, and you're on the dev machine with GitHub access. Every path below is relative to the repo root on the audit branch. Nothing here depends on the cloud container, which is gone once the session ends.

---

## 1. Where it lives

| What | Where |
|---|---|
| Engine repo | https://github.com/Beansz2015/DeribitVerdictEngine |
| Audit branch | `claude/great-keller-s5f4gp` |
| Pull request | https://github.com/Beansz2015/DeribitVerdictEngine/pull/3. Open and unmerged. It adds docs and proof code only, and no engine source, settings or order-app code changes |
| The report (start here) | [`docs/adversarial-audit-2026-09-24.md`](https://github.com/Beansz2015/DeribitVerdictEngine/blob/claude/great-keller-s5f4gp/docs/adversarial-audit-2026-09-24.md) |
| Order-app repo | https://github.com/Beansz2015/deribitorderplacementapp. Its two audit lanes are on branches `claude/gracious-fermat-w8sacs` (M9) and `claude/zen-cray-wy33a6` (L-4). Both are copied byte for byte into the engine branch under `docs/audits/order-app/`, so you don't need to fetch them |

To get it onto the dev machine, run this from your engine clone, in PowerShell or Git Bash:

```bash
git fetch origin claude/great-keller-s5f4gp
git worktree add ../audit-review claude/great-keller-s5f4gp       # a separate checkout; your working tree is untouched
# or read one file without checking anything out:
git show origin/claude/great-keller-s5f4gp:docs/adversarial-audit-2026-09-24.md
```

To check a finding against the code it was written about, make a worktree at the pinned commit (§3):

```bash
git worktree add ../audit-6e74181 6e74181                          # engine
git -C <your app clone> worktree add ../app-8232e9e 8232e9e        # order app
```

---

## 2. Read in this order

1. **The report, `docs/adversarial-audit-2026-09-24.md`, §A to §D.** That's the whole of what you need to act on:
   - §A has nine headline items.
   - §B ranks every confirmed defect in six bands, with evidence codes.
   - §C sets out 21 decisions. Each has options, the audit's read and a reserved-class flag.
   - §D says what's done and what's left.

   Everything after §D is the original line-level audit, kept as first published, with superseding notes where later lanes changed a conclusion.
2. **Only when you need the evidence behind a row:**
   - `docs/audits/2026-09-24-review-batch-summary.md` has the verdict on each of the 191 lane findings (138 from M1–M10, 53 from L-1 to L-6) and how it was checked.
   - `docs/audits/2026-09-24-review-batch-spec-back.md` §1 has the runnable handles H-1 to H-12, each with the output it printed.
   - The lane reports are `docs/audits/2026-09-24-*.md`, `docs/audits/2026-09-25-*.md` and `docs/audits/order-app/*.md`. Their proofs are under `docs/audits/proofs/` and `docs/audits/order-app/proofs/`.

**The short version of §A.** There are three S0s.
- **A1:** a long can reach Deribit with its stop at or above the fill. The engine's 2 USD stop floor and the app's entry drift combine, and nothing checks the side.
- **A12:** the app's emergency close cancels the stop first, never checks the market reduce, and reports "Executed" regardless.
- **A13:** the app has no position-level loss cap, so a stop leg the exchange rejects is never noticed.

Behind them are nine S1s in the order path and two that halt the collector. The measurement surfaces every ruling reads (the strip, the matrix, the what-if runner, CeilingAudit, the coverage report) have defects that bias them toward "fine". The ship gate skips the fixtures for two known scoring defects.

---

## 3. The pin, and what has moved since

The audit is pinned to **engine `6e74181`** (settings v68) and **order app `8232e9e`**. The trader ruled newer commits out of its scope, so no finding was re-checked against them.

The engine's master is **167 commits past the pin** (as of `cab8017`, 2026-09-28). I didn't audit any of it. I checked three facts on 2026-09-29 so you don't have to look them up:
- **K1 (the POC-tier gate) has a fix on master:** `a6b33fe`, "D-1 POC-tier gate reads the NEAR_HVN labels with the producer's geometry".
- **K2 (maker-side liquidations) has none.** `Core/Indicators_OrderFlow.vb` has no master commit since the pin.
- **The known-defects hatch (row C20) is still on master.** `ORDERCHECK_KNOWN_DEFECTS` appears 5 times in master's `verify/ordercheck/Program.vb`, and 0 times in `tools/checks/verify-gate.ps1` and `.github/workflows/verify.yml`.

**Before acting on any row, check whether its cited file moved:**

```bash
git log --oneline 6e74181..origin/master -- <cited file>                        # engine
git -C <app clone> log --oneline 8232e9e..origin/master -- <cited file>         # order app (not checked by me)
```

No commits means the finding stands as written. If there are commits, re-read the cited lines on master before building on the row. CLAUDE.md's own rule applies: a doc's status is not evidence of code state.

---

## 4. What's left, and my recommendation

| ID | What | Who can do it | Worth doing? |
|---|---|---|---|
| **X-1** | Testnet orders that settle five questions about Deribit's behaviour (report §D lists them): a crossed stop at OTOCO activation, `trigger_offset` trailing, post-only repricing, `first_hit` partial-fill sizing, whether a triggered stop keeps its id | The trader, with a testnet account. You can write the order script | **Yes, first.** It's the only item that changes how often A1 and A13 fire. The audit's Deribit facts are web-search summaries, because docs.deribit.com was blocked from the cloud sessions |
| **X-4** | Windows-only checks: M1's probe P9 (a rename-save and the watcher), a `FileShare` collision between a store read and a repair, L-6's `net8.0-windows` proof, and one harness run with the console culture printed | You, on the dev machine | **Yes.** It's cheap, and only a Windows box can do it. The culture run pins which host the lanes' 425 / 0 baseline came from (row C23) |
| **L-7** | An independent review of the whole audit: run the handles, re-check Bands A and B, trace the three S0s, sample 15 verdicts, and challenge every §C read | A fresh session. The paste-ready prompt is in the report's §7.1. Fable 5.1, high | **Yes, but don't hold fixes for it.** The collector-halt fixes (C-8) and the app's position cap (C-16) are defensive, so they're safe to build first. Run L-7 before any fix that changes scoring or live order behaviour ships |
| **X-2** | How often A1 fires in practice: `analysis_log.csv` and its `.bak` rotations, joined to the app's trade records on `SignalId` + `InstanceId` | The trader's data, and a small reader | After X-1. **The repo is public, so don't commit that data** |
| **X-3** | The exit guard's false-latch rate on real tape (D2 was measured on synthetic tape only) | A few days of `trades_YYYY-MM.csv`, replayed through M3's harness (`docs/audits/proofs/exit-guard-live-strip-alerts/`) | Only if you're about to rule on C-14 |

If you run any of these, fold the result in the way §D describes. Re-check each claim against the code, then update the report's §B and §C and the summary's §3 tally.

---

## 5. Rules for the fixes

- **§C's reads are the audit's hypotheses, not rulings.** Each one names the options it didn't pick, and why.
- **Reserved classes (CLAUDE.md) go to the trader:** anything that affects scoring, `settings.json`, a rendered value, store writes, or a schema or CSV header. Each §C decision says whether it's in one.
- **Auto-proceed candidates:**
  - C-8, the four collector-halt fixes: no scoring, no settings, one revert each.
  - C-20(b), the gate's known-defects ledger.
  - C-21, the new fixtures, but only in the same commits as the fixes they guard.
- **The order-app decisions (C-15 to C-18) belong to the app repo and its rules.** They change live order behaviour, so I've flagged each for the trader.
- **Several decisions share a root and are cheaper ruled together.** §C's opening list names them: stop geometry (C-1, C-2), fees (C-3), measurement (C-9, C-10, C-11, C-19), and the app (C-15 to C-18).

---

## 6. Running the proofs on Windows

- **The .NET proofs run as-is with the .NET 8 SDK:** `verify/auditproofs/`, the lane harnesses and the L-5 proof projects. Proof sources under `docs/` carry a `.txt` suffix (`.vb.txt`, `.vbproj.txt`), because the root project compiles every `.vb` outside `tools/` and `verify/`. Each proof README gives the copy-and-rename steps.
- **The shell scripts need Git Bash or WSL:** L-4's `run.sh`/`gen.sh`, L-5's `run_coverage_proofs.sh`, and H-7's block. H-8, H-9 and L-5's fixture generator need `python3`.
- **H-2 and H-6 hard-code `/home/user/DeribitVerdictEngine/`.** Replace it with your repo root.
- **The harness's PASS total depends on the host (row C23).** Linux gives 421 or 423 where the lanes recorded 425. Two fixtures pin culture-formatted percentages and two assume Windows share modes. It's not a regression; don't chase it as one.

---

## 7. Housekeeping

- Nothing watches PR #3, and no check-ins are scheduled. The trader stopped them to save context.
- The cloud session that wrote this is ephemeral. Everything it produced is on `claude/great-keller-s5f4gp`.
- The branch changes no engine source, settings or order-app code. Merging it adds documents and proof code. The `verify/auditproofs/` project sits under `verify/`, which the root project excludes.
