# Dev-Workflow Automation — Proposal (verification gate, hooks, guard checks, named subagents)

**Date:** 2026-06-30
**From:** implementer seat (Opus 4.8), at the trader's request after a workflow-efficiency discussion.
**To:** orchestrator / coordinator seat — **for review**.
**Status:** **PROPOSED — not built.** Tooling-only: **no engine, scoring, `settings.json`, or CSV-schema change.** Host-agnostic (aligns with the Linux-CLI-port goal — the gate script + harness become the portable acceptance surface). Spec-first per CLAUDE.md; nothing here is built until this is signed off.

**Scope in one paragraph.** Turn four recurring *manual* steps in the current workflow into *automated* ones: (1) a single verification gate — build the solution + AutoTweaker + the `verify/ordercheck` harness and assert `ALL PASS` — wired into both a local git pre-push hook **and** GitHub Actions CI; (2) a Claude Code `Stop` hook that runs the fast slice of that gate after each working session; (3) two heuristic guard checks folded into the gate — the **display-parity hard rule** and the **`settings.json` version-bump rule** — operationalising two rules CLAUDE.md currently enforces by memory; (4) named Claude Code subagents (`.claude/agents/*.md`) for the repeated **coordinator-review** (and optionally **implementer**) seats. All four share **one** check script so the hook, CI, and humans run identical logic.

---

## 0. Reconciliation note (read first)

The trader is running a parallel conversation that is "far ahead of this one and may have incorporated some elements." So:

- **Every item below is self-contained.** The reviewer can mark each **DONE / SKIP / BUILD** independently; none depends on another except through the shared script (§4), which degrades gracefully (a hook or CI job that calls a not-yet-written script simply no-ops with a clear message).
- **Before building, diff against current state.** This proposal was written against a repo snapshot (2026-06-30) with: **no `.github/workflows`, no committed `.claude/settings.json`, no `.claude/agents/`, no git hooks**, and both `verify/` and `.claude/` **gitignored**. If the parallel seat has since added any of these, the relevant item collapses to a reconcile-and-merge rather than a build.

---

## 1. Why (the problem this closes)

1. **Documented, recurring drift.** CLAUDE.md records the **display-parity rule** with evidence of *"three drift instances in one cycle"* (v31 `ccdd652`, Tier D `0bd1b63`, Tier D `482c9bb`) — the text renderers and the card surface fall out of sync because the P5-test harness diffs legacy↔snapshot (which move together), leaving the **card** as the unchecked third surface. This is a *verification gap*, and it recurs because the only guard is a human remembering the rule.
2. **The acceptance harness is run by hand, inconsistently.** Every spec-back cites `dotnet run --project verify/ordercheck → ALL PASS` as the gate, but nothing *forces* it before a commit or push. A red harness can reach `origin`.
3. **No CI exists.** All verification is local; a second machine, a cron run, or a fresh seat has no automatic safety net. The remote (`github.com/Beansz2015/DeribitVerdictEngine`) is a backup only.
4. **The multi-seat process is manual and re-pays context.** "Coordinator review" — independently re-run the three builds + harness, source-verify the spec-back's load-bearing claims, check parity + version-bump — is a *fixed checklist* re-typed into a fresh conversation each cycle. It's a textbook subagent.

**Non-goal:** this does **not** automate design judgement (proposals, threshold changes, anything spec-first). It automates *verification and mechanical guardrails* only.

---

## 2. Current state (verified 2026-06-30)

| Fact | Implication |
|---|---|
| Remote `origin` = `github.com/Beansz2015/DeribitVerdictEngine` | GitHub Actions CI is viable. |
| No `.github/workflows/` | CI is greenfield. |
| `.claude/` holds only `settings.local.json` (permissions; no hooks) | Hooks + agents are greenfield. |
| **`verify/` is gitignored** (`.gitignore:` `verify/`) | The harness is **not committed** → CI can't run it without an un-ignore decision (§3.1 Decision A). |
| **`.claude/` is gitignored** (`.gitignore:365` `.claude/`) | Shared hooks/subagents need an un-ignore decision (§3.4 Decision C). |
| Harness sets `Environment.ExitCode = 1` on failure, prints `ALL PASS` | Gate can branch on **exit code** (no output parsing needed). |
| WinForms app targets `net8.0-windows`; AutoTweaker + OrderCheck target `net8.0` (host-agnostic) | The **solution** build needs a **Windows** runner; the harness alone could run on Linux. |
| Root `.vbproj` has `Compile Remove verify/**` | Committing the harness still won't break the solution build. |
| Platform win32; Git Bash **and** PowerShell available | The shared script should be callable from both shells. |

---

## 3. The four items

### 3.1 Item 1 — One verification gate, wired to a pre-push hook **and** CI

**Goal.** No red commit reaches `origin`; CI re-proves every push on a clean checkout.

**Design.** A single script (`tools/checks/verify-gate.ps1`, §4) is the one source of truth. Two invokers:

- **Local `pre-push` git hook** (`.git/hooks/pre-push`, installed by a tracked installer `tools/checks/install-hooks.*` since `.git/hooks` isn't version-controlled): calls the script in `prepush` mode against the to-be-pushed range. Non-zero exit aborts the push.
- **GitHub Actions** (`.github/workflows/verify.yml`): on `push` (all branches) + `workflow_dispatch`. Runner `windows-latest` (required for the WinForms solution build). Steps: checkout → `actions/setup-dotnet@v4` (.NET 8) → run the same script in `ci` mode. **No API key, no AI** — this gate is pure `dotnet`, so it's free of Anthropic spend and deterministic.

**Decision A (blocking for CI):** commit the harness so CI can run it.
- Un-ignore *only* `verify/ordercheck/` source, keep artefacts ignored. Concrete `.gitignore` delta:
  ```gitignore
  verify/
  !verify/ordercheck/
  !verify/ordercheck/**
  verify/ordercheck/bin/
  verify/ordercheck/obj/
  ```
- Rationale: the harness is *already* the referenced acceptance artifact in every spec-back; committing it makes acceptance reproducible by CI and any seat, and the existing `Compile Remove verify/**` keeps it out of the solution build. (Screenshots and the other `verify/*.png` stay ignored.)
- **Alternative if Decision A is rejected:** keep `verify/` local and ship the pre-push hook only (no harness in CI; CI degrades to build-only). Weaker, but zero gitignore-policy change.

**Files:** `tools/checks/verify-gate.ps1`, `tools/checks/install-hooks.ps1` (+ `.sh` sibling), `.github/workflows/verify.yml`, `.gitignore` (Decision A).

**Acceptance:** (a) `verify.yml` goes green on a clean push; (b) a deliberately-failed harness fixture turns CI **red** and aborts a local `git push`; (c) restoring the fixture turns both green.

---

### 3.2 Item 2 — `Stop` hook: auto-run the fast gate after each session

**Goal.** The harness runs itself when a seat finishes, so "forgot to verify" stops happening — without slowing the inner loop.

**Design.** `.claude/settings.json` `Stop` hook calls the shared script in **`local-fast`** mode: build `AutoTweaker` + `OrderCheck` and run the harness (≈3–5 s), **skipping** the slower `-c Release` solution build (that stays on pre-push/CI). Hook output (pass/fail summary) is surfaced back into the session as feedback.

```jsonc
// .claude/settings.json
{
  "hooks": {
    "Stop": [
      { "matcher": "",
        "hooks": [ { "type": "command",
                     "command": "powershell -NoProfile -File tools/checks/verify-gate.ps1 -Mode local-fast" } ] }
    ]
  }
}
```

**Decision B:** event + scope.
- **Recommended:** `Stop`, harness-only (fast). Rationale: `PostToolUse` on every `Edit`/`Write` fires far too often (mid-refactor the harness is *expected* red); `Stop` fires once per completed turn.
- **Trade-off to weigh:** even ≈5 s on every Stop is friction during pure-discussion turns (like this one). Mitigation options for the reviewer: (i) accept it; (ii) gate the hook on "any `*.vb` changed since last run" via a marker file; (iii) make it advisory-only (never blocks, just reports).

**Files:** `.claude/settings.json` (new, committed — see Decision C).

**Acceptance:** editing a `.vb` then ending a turn triggers the harness; a red harness surfaces a visible failure line; a discussion-only turn with no code change is acceptable to run (or no-ops, per Decision B-ii).

---

### 3.3 Item 3 — Two heuristic guard checks, folded into the gate

**Goal.** Operationalise the two rules CLAUDE.md currently enforces by memory.

**Design.** Both are **git-diff heuristics** in the shared script, run over the commit/push range (or staged set). Both are **WARN + override-token**, not hard fails — because both existing rules *explicitly* permit a justified exception.

**(3a) Display-parity check.** If any renderer surface changed but the card surface did **not**, warn:
- Renderer surfaces watched: `UI/MainForm_Render_Header.vb`, `UI/MainForm_Render_Sections.vb`, `UI/MainForm_PlaintextSnapshot.vb` (`BuildPlaintextSnapshot`).
- Card surface: `UI/MainForm_Render_Cards.vb`.
- Rule (from CLAUDE.md): a renderer-line change "MUST update the corresponding card binding … **or state in the commit message why no card surface is affected.**" → the check looks for an override token in the range's commit message(s), e.g. `[no-card-surface]`. Present ⇒ pass; absent ⇒ **fail** in `prepush`/`ci`, **warn** in `local-fast`.

**(3b) Settings-version-bump check.** If a behaviour-affecting source path changed but `settings.json`'s `version` line did **not**, warn:
- Watched paths: `Core/**`, `DynamicNorms.vb`, `analysis/**` (engine-behaviour surfaces).
- Override token: `[no-engine-change]`.
- This one is **WARN-only even in CI** (the "is this a behaviour change?" judgement is too soft to hard-block); the goal is a visible nudge, not a gate.

**Why tokens, not hard blocks:** the parity rule already sanctions "state why no card surface is affected"; encoding that as a token makes the human's existing escape hatch machine-checkable without inventing new policy.

**Files:** folded into `tools/checks/verify-gate.ps1` (no new files).

**Acceptance:** (a) a renderer-only commit with no card change and no token → parity check fails (prepush/ci); (b) same commit with `[no-card-surface]` in the message → passes; (c) a `Core/**` change with no `version` bump and no token → emits a warning but does not fail CI.

---

### 3.4 Item 4 — Named subagents for the repeated seats

**Goal.** Stop re-typing the coordinator checklist into a fresh conversation; make "review this spec-back" a one-invocation subagent.

**Design.** Claude Code custom subagent definitions under `.claude/agents/`:

- **`coordinator-review.md`** — *read-only + Bash* (it reviews, it does **not** implement; deny `Edit`/`Write`). System prompt = the established coordinator checklist:
  1. Re-run all three builds + the harness; report exact pass/fail counts.
  2. Source-verify the spec-back's load-bearing claims (grep/read the cited sites).
  3. Run the display-parity + version-bump checks (§3) and report.
  4. Confirm the `DeribitIndicatorProject.md` §15 entry + `settings.json` version/`change_log` exist when behaviour changed.
  5. Emit **APPROVED** / **CHANGES REQUESTED** with evidence (mirrors the review blocks already pasted into spec-backs).
  ```markdown
  ---
  name: coordinator-review
  description: Independent verification of an implementer spec-back — re-runs builds + harness, source-verifies claims, checks parity + version bump. Read-only; never edits.
  tools: Read, Grep, Glob, Bash
  model: opus
  ---
  <the checklist above, verbatim, as the system prompt>
  ```
- **`implementer.md`** (optional, lower value) — `Read, Grep, Glob, Edit, Write, Bash`; system prompt = the session-start protocol + "build only approved specs, local commits only." Marginal vs. just running the main session as implementer; include only if the orchestrator wants true parallel fan-out.

**Budget caveat (explicit).** Per the trader's $20-plan constraint and Fable's 2× cost: subagents **cold-start** and re-derive context (≈ the session-start protocol each time). So `coordinator-review` pays off as a *real second pair of eyes run on demand before a push* — not as a thing fired every turn. Recommend Opus, invoked deliberately. The existing `/code-review` and `/verify` skills cover the lighter cases.

**Decision C (shared vs local for items 2+4):** `.claude/` is fully gitignored, so hooks and agents are **machine-local** unless un-ignored. To share them across seats/machines:
```gitignore
.claude/
!.claude/settings.json
!.claude/agents/
!.claude/agents/**
# settings.local.json stays ignored (machine-specific permissions)
```
- **Recommended:** un-ignore `settings.json` + `agents/` (shared, version-controlled workflow). Keep `settings.local.json` local.
- **Alternative:** leave `.claude/` ignored; items 2+4 are local-only and each machine installs them by hand. Simpler, but the workflow isn't reproducible.

**Files:** `.claude/agents/coordinator-review.md` (+ optional `implementer.md`), `.gitignore` (Decision C).

**Acceptance:** invoking the coordinator-review subagent on the current branch reproduces a review block equivalent to the hand-written ones (builds + harness counts + claim checks + verdict), making **zero** file edits.

---

## 4. The shared check script (the DRY core)

One script, several modes, so the hook, CI, and humans run identical logic. `tools/checks/verify-gate.ps1` (PowerShell — native on `windows-latest` and callable from Git Bash via `powershell -File`; a thin `verify-gate.sh` wrapper can shell to it for bash-first users).

```
verify-gate.ps1 -Mode <local-fast|prepush|ci> [-BaseRef origin/master]

local-fast : build AutoTweaker + OrderCheck; run harness; parity/version = WARN.   (~5s, Stop hook)
prepush    : + build solution -c Release; parity = FAIL-without-token; version = WARN.  (git pre-push)
ci         : identical to prepush, against the push range.                              (GitHub Actions)

Exit 0 = gate passed (warnings allowed). Exit 1 = build/harness/parity failure.
```

Steps (per mode): (1) `dotnet build DeribitVerdictEngine.sln -c Release` [prepush/ci only]; (2) `dotnet build tools/AutoTweaker/AutoTweaker.vbproj`; (3) `dotnet build verify/ordercheck/OrderCheck.vbproj`; (4) `dotnet run --project verify/ordercheck` → gate on exit code **and** `ALL PASS`; (5) §3a parity diff; (6) §3b version diff. Diff range: `prepush` uses the to-be-pushed range, `ci` the push event range, `local-fast` the working tree vs `HEAD`.

This is the same script `coordinator-review` (§3.4) shells out to, so all four surfaces are guaranteed consistent.

---

## 5. Decisions for the orchestrator

| # | Decision | Recommended | Why it matters |
|---|---|---|---|
| **A** | Commit `verify/ordercheck/` (un-ignore source, keep artefacts ignored)? | **Yes** | Blocking for CI running the real harness; makes acceptance reproducible. |
| **B** | `Stop` hook event + scope (Stop+fast / PostToolUse / advisory-only)? | **Stop, harness-only, advisory-or-blocking your call** | Controls inner-loop friction. |
| **C** | Share `.claude/settings.json` + `agents/` (un-ignore) vs local-only? | **Share** | Reproducible workflow across seats/machines. |
| **D** | Parity check: hard-fail-without-token vs warn-only in CI? | **Fail-without-token** | Directly closes the documented 3×-drift gap; token preserves the existing escape hatch. |
| **E** | Build item 4 `implementer.md` too, or `coordinator-review.md` only? | **coordinator-review only** | Higher value; respects the budget; implementer ≈ the main session already. |
| **F** | CI runner: `windows-latest` only, or split (Windows solution + Linux harness)? | **windows-latest only** | One runner is simpler; the WinForms build forces Windows anyway. |

---

## 6. Out of scope / non-goals

- **No engine/scoring/`settings.json`/CSV change.** Tooling only.
- **No AI in the CI gate** (it's pure `dotnet`; no Anthropic spend, no key in Actions). The optional `claude-code-action` is explicitly *not* proposed — it would add cost and non-determinism to a gate whose whole value is being free and deterministic.
- **No autonomous dev loop** (Ralph/`/loop`-style). High-stakes, spec-first work with hard rules is the wrong place for an unattended loop; bounded mechanical sweeps validated by this gate are a *later, separate* proposal if wanted.
- **Item 5 from the discussion (budget/context engineering)** is omitted — it's process, not code.

---

## 7. Suggested sequencing (if approved)

1. **Item 1 + §4 script** (Decision A, F) — the gate is the foundation everything else reuses. Land CI first; it pays off immediately and is the lowest-risk.
2. **Item 3** — fold the two heuristics into the now-existing script (Decision D).
3. **Item 2** — add the `Stop` hook (Decision B, C) once the script is proven.
4. **Item 4** — `coordinator-review.md` (Decision C, E), last and optional.

Each step is independently shippable and independently revertible.

---

## 8. Risks / open questions

- **Gitignore surgery (A, C)** is fiddly (nested un-ignore + re-ignore of `bin/`/`obj/`); the spec gives the exact deltas, but the implementer must verify `git status` shows only intended files after.
- **`dotnet run` exit-code propagation** through GitHub Actions on Windows can be quirky; gate on **both** exit code and the `ALL PASS` string for safety.
- **Heuristic false positives (3a/3b):** the token escape hatch is the mitigation; if it nags too often, downgrade 3b to local-only.
- **Stop-hook latency (B):** the headline risk to *daily* ergonomics — measure the real harness wall-time on this machine before committing to blocking-on-Stop; advisory-only is the safe default.
- **Windows CI minutes** cost 2× Linux on Actions; for a private repo confirm it stays within the free tier, else move the host-agnostic harness job to `ubuntu-latest` and keep only the solution build on Windows (Decision F alternative).
