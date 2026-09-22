# Seat handover — 2026-09-22 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md), superseded for state. **Its §4 task list is still the real engine queue and is STILL UNTOUCHED** — see §7.

**State at close (2026-09-22 15:11 UTC):**
- ⭐⭐ **A new capability was stood up all session: the Jev / TypeSafe harness programme.** Three harnesses built, one measured clean.
- ✅ **`master` is CLEAN against `origin`. The trader tested and pushed. Nothing unpushed.**
- Settings **v68**, untouched all day. **No settings key changed. Nothing deployed. Nothing touched the collector except two read-only checks.**
- Harness **425 PASS / 0 FAIL / exit 0**.
- ⛔ **TRADER DIRECTION: finish ALL Jev-related items and CLOSE the programme before returning to the engine queue.** Next up is harness 4.
- ⛔ Run `git status -sb` and `date -u` anyway. Never inherit a push state. The workstation is GMT+8.

---

## 0. ⛔ FIRST ACTIONS

**Nothing is running.** No agent, no watcher, no scheduled task.

| # | Action | Why |
|---|---|---|
| **1** | ⛔ **The 2026-09-24 06:00 UTC collector check** | The 09-22 one was run 8 h late (§6). This is the decision point — three Defender scans survived |
| **2** | **Harness 4, the doc scanner** | Trader-directed. §4 has its scope and the measured findings it must inherit |

⚠ **Before running the 09-24 check, read §6.** `collector.ps1 status` **cannot** read `ws_feed.log`, which the prior handover said the check reads. And read [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §0's caveat: a clean result shows the box is healthy, **not** that the re-entrancy gates work.

---

## 1. ⭐⭐ What Jev is — read this before touching any harness

**Jev is TypeSafe's "System One" model.** It is **NOT** a reasoning model and **NOT** a cheaper seat.

⛔ **It does not generate text, write code, or explain its reasoning.** It takes a `state` plus typed questions and returns calibrated probabilities. Three primitives only: **Noul** (probability a statement is true), **Choice** (one option from a set, with a distribution), **Score** (position on ordered levels).

| Property | Value |
|---|---|
| Price | **$0.042 per Mtok INPUT. Output tokens free** (`docs.typesafe.ai/models.md`) |
| Speed | ~0.8 s per call; 42 items in 4.5 s at 8-way parallel |
| Context | 64k per request; 32k for `state` + longest question |
| Model | `jev-latest` → `jev-1.13.0` |
| ⛔ **NOT deterministic** | See §3 |

**The API key** lives in the gitignored `typesafe.local.env` at the repo root. Load with `set -a; . ./typesafe.local.env; set +a`. ⛔ **The repo is PUBLIC — never print it, never put it in a tracked file.**

⛔⛔ **WHAT IT IS BAD AT, and this repo is full of it:** maths, counting, dates, and large states of irrelevant detail. `docs.typesafe.ai/model-jaggedness/jev-1.13.md` names *"asking the model something code can compute exactly"* as its FIRST anti-pattern. **Never point it at `analysis_log.csv`.** Every question this project cares about there is arithmetic.

⭐ **The shape that works, proven three times: CODE enumerates candidates and computes facts → JEV judges relevance or class → CODE decides.**

---

## 2. The three harnesses

All are **advisory**, none is wired into `verify-gate.ps1` or the pre-push hook, and all share `tools/checks/lib/InvokeJev.ps1` (one definition, three callers).

| Harness | File | Spec | State |
|---|---|---|---|
| 1 · rider travel | `tools/checks/rider-travel.ps1` | [`rider-travel-check-spec.md`](rider-travel-check-spec.md) | Built. **First run gated on the absorption S2 header rotation** — not in our control |
| 2 · commit walker | `tools/checks/commit-walker.ps1` | [`commit-walker-check-spec.md`](commit-walker-check-spec.md) | Built through revision 2 + a correction. **Measurement window spent; needs unseen post-adoption commits** |
| 3 · fixture parser | `tools/checks/fixture-parser.ps1` | [`fixture-parser-check-spec.md`](fixture-parser-check-spec.md) | Built through revision 1. ⭐ **MEASURED CLEAN — see §3** |

⛔ **All three REFUSE to call the API without a baseline file.** That is structural, not a convention, and it is the whole measurement design.

---

## 3. ⭐⭐ THE CLEAN MEASUREMENT — the programme's one real result

**Full record: [`harness-runs/fixture-parser-clean-run-2026-09-22.md`](harness-runs/fixture-parser-clean-run-2026-09-22.md).** 25 items, seat-written baseline written before any detector output existed, same inputs to both judges.

**23 of 25 agree (92%). $0.0078, 63 s.**

⭐ **The finding that matters is not the 92% — it is WHICH rows disagreed:**

| Class | Count | Self-consistency |
|---|---|---|
| Agreements | 23 | **All STABLE, 5 of 5** |
| Disagreements | 2 | **Both UNSTABLE, 4 of 5** |

**Zero confident-and-wrong rows.** The detector disagreed only where it had already reported wobbling.

⛔⛔ **AND THE SEPARATING SIGNAL IS THE AGREEMENT RATE, NOT THE PROBABILITY BAND.** An agreeing row sat at `mean_top_prob` **0.434** — *below* a disagreeing row at **0.452**. Probability overlaps the classes; the agreement rate does not. **Recorded as [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4d. Never promote a probability band into a trust decision.**

⛔ **The detector is NOT deterministic.** Three identical calls on one commit returned `changes_writes` · `no_app_change` · `changes_writes`. That is why every harness samples 5× and prints an agreement rate on every row. Protocol §4b.

⭐ **The harness found a real rule breach.** `A43b`'s `tfiWindowSize:=30` equalled the shipped value with no class declared — exactly what `CLAUDE.md`'s fixture-literal provenance rule forbids, in a rule `CLAUDE.md` itself says *"no tool can tell the two apart… it falls to review."* **Review had missed it. Fixed 2026-09-22 (`6e74181`), declared MECHANISM, literal kept.**

---

## 4. ⛔ OWED — the Jev programme, in order

**Trader-directed: close ALL of this before returning to the engine queue.**

| # | Item | Notes |
|---|---|---|
| **1** | ⛔ **Baseline `FP-Q1`'s own scope judgments** | **A second, UNMEASURED detector lives inside harness 3.** Its threshold-or-input calls shaped the measured population and were never baselined. **Deliberately left — a baseline written under context pressure manufactures agreement, which the protocol forbids** |
| **2** | `upgradeBonus`, one site | Excluded as a fixture-builder param though `indicators.OiCvd.upgrade_bonus` is a real key. Unjudged |
| **3** | ⭐ **Harness 4 — the doc scanner** | Covers version rot · identifier collision · cross-doc contradiction. §5 has what it must inherit |
| **4** | Harness 5 — the doc re-ranker | `docs.typesafe.ai/cookbooks/rerank_typesafe.md`. Judged on whether it speeds the seat up, not on one run |
| **5** | Harness 2's measurement | Needs post-2026-08-12T17:21:40Z commits the seat has not seen. ~200/month, so weeks |
| **6** | Harness 1's first run | Gated on the absorption S2 rotation |
| **7** | `commit-walker` §10.10 | Status fires on the five-way label where the audit asks a two-way question. **Deferred deliberately — it OVER-flags, the safe direction** |

---

## 5. ⛔⛔ READ BEFORE WRITING HARNESS 4's SPEC — this session's own failure pattern

⭐ **Eight of my errors this session, and they share one shape: I asserted a conclusion from a one-sided measurement, then had to withdraw it.** Three in the same programme:

| Claim | Reality |
|---|---|
| *"The commit walker's `tools/` exclusion is manufacturing false residual, 7 of 7"* | **Withdrawn.** Measuring the other direction: adding `tools/` creates **50 false positives to remove 6**. The original rule is the best fit at 94% |
| *"90 of 118 literals match no key, so 76% can never be judged"* | **Overstated.** Only 43 of 118 are settings-derived at all; I had dropped the rule's own scoping word *"threshold"*. Real miss: ~15 of 43 |
| *"Switch the gate from probability to agreement rate"* | **No-op.** Both harnesses already gated on agreement rate. **Caught before acting — the only one of the three I caught in time** |

⭐⭐ **WHAT ACTUALLY FIXED IT: measure BEFORE writing the spec, not after.** Harness 3's spec was the first written that way, and its build round produced no factual defect in the spec's premises — only scope questions. **Do the same for harness 4.**

⛔ **And a second pattern worth as much:** three separate times, the binding constraint turned out to be **candidate enumeration, not judgment** — the git evidence in the pilot, the residual definition in harness 2, key matching in harness 3. **The next improvement on any harness is almost always better enumeration, not a better question.** Protocol §4 finding 3.

⚠ **Other traps, all measured:**
- ⛔ **Never compose two Nouls with `AND`.** Measured at **0 of 8** against a single Choice's 4 of 8. The verdict reads ONE Choice answer.
- ⛔ **PowerShell 5.1's `Invoke-RestMethod` does not UTF-8-encode a plain string body.** This repo's text is full of `⭐ ⛔ · §  —`, so a string body corrupts on the wire and returns HTTP 400. **Reuse `lib/InvokeJev.ps1`; never write a second HTTP call.**
- ⛔ **A raw regex over VB counts COMMENT PROSE as code.** It cost me 120-that-was-118, and the miscounted line was a *correct* provenance declaration — I counted the rule's own worked example as a violation candidate.
- ⚠ **`@($a) + @($b)` on two non-empty `List[object]` throws in Windows PowerShell 5.1.** Use `.ToArray()`.

---

## 6. ⭐ The collector — checked, healthy, with one instrument gap

**Ledger entry: [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5c.** The 2026-09-22 06:00 UTC check ran 8 h late at 14:05.

| Criterion | Reading |
|---|---|
| Threads | **12** — healthy band ~10–20; the 09-18 leak hit 11,630 in four hours |
| `analysis_log.csv` | gap **0.7 min**, advancing |
| `ws_health.log` | `OK` since 2026-09-21 15:39:49, no transition |
| Settings / overlay | v68 / `False` — correct for AWS |

⭐⭐ **First-ever `ws_feed.log` read, and it earned itself: FOUR WebSocket reconnects in 22.5 h that `ws_health.log` cannot see.** 09-21 22:28, 09-22 04:50, 06:04, 06:38 — each *"remote party closed without completing the close handshake"* → reconnect in 1 s → re-subscribe. **291 lines, 267 heartbeats; every non-heartbeat line is one of those four.** That is `ws_health.log`'s documented under-reporting confirmed by a second instrument.

⭐ **No tape was lost, corroborated not assumed:** the post-fetch venue check returned **`CLEAN — missing=0, venue=109704, pages=110, seq_contiguous=true`**. Also the first valid sample for the dated 2026-10-05 venue-check review.

⛔ **`collector.ps1 status` CANNOT read `ws_feed.log`.** It is in `$FetchFiles`, so only `fetch` retrieves it. [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §0 says the check reads it. **Fix the wording or surface it in `status` before the 09-24 check.**

⚠ **`pagesOUT/s` UNRESOLVED, deliberately neither alarmed nor cleared.** Two readings, 773.1/4276.6 and 449.4/2453.1, both non-zero — so **not** the recorded false alarm that went to 0 across 15/15 samples. But probe-induced paging is documented and the box has 87–101 MB free of 1,024. **Two readings from the same probe cannot separate them.** Needs a detached instrument.

---

## 7. ⛔ The engine queue — STILL ON HOLD, and still untouched

**Nothing in [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §4 was touched this session or last.** The hold was set when the Jev work began and the trader has extended it until the programme closes.

Still waiting: **the engine-fix build** ([`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md), fully ruled, untouched) · **absorption S1/S2** with its **eight riders**, `T-7` now the only gate · **`Q-1` option (d)** · the UI-thread liveness heartbeat, gated on the 09-24 check.

⚠ **"No deadline" is not "no urgency"** — that is how those riders accumulated, and `RIDER-7` was already lost once.

---

## 8. ⚠ What I did NOT verify

- **That 25 items generalises.** It does not. Two disagreements is not a rate.
- **The 89 out-of-scope fixture sites.** Only five named proofs were inspected.
- **`FP-Q1`'s scope judgments** — owed item 1, the unmeasured detector.
- **Whether Jev is stable at HIGH confidence.** Only low-band instability was observed, on three probed commits.
- **Every collector reading beyond 14:07 UTC**, and the `pagesOUT/s` question entirely.
- ⚠ **4 of the 19 provenance rows in the clean run are NOT independent of the seat** — it had reworded those comments earlier the same day. Declared in the baseline file, not discovered after.
