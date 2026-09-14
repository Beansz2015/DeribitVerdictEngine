# Seat handover — 2026-09-14b (UTC, second seat of the day)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.** The `b` suffix marks the second handover written on 2026-09-14 (UTC).

**Prior handover: [`seat-handover-2026-09-14.md`](seat-handover-2026-09-14.md)** — superseded for STATE only. Its §1 build plan, §3 absorption mechanism facts, §4 instruments and §6 lessons still bind, and this handover does not repeat them.

**Settings: v68**, untouched this session. **No `.vb` file, `settings.json` or CSV schema changed this session.** At close (2026-09-14 14:01 UTC): **clean, in sync with `origin/master`, last commit `ccf7b80`; tags `doc-trim-2026-09-14-pre` and `doc-trim-2026-09-14b-pre` are on the remote.** ⛔ Run `git status -sb` anyway; never inherit a push state.

---

## 0. ⛔ Before your first reply

| Rule | Where it lives |
|---|---|
| **Output format:** point form and tables; never a bare section number or bare ID; short active sentences that keep every domain term; separate what you verified from what you carried | `C:\Users\user\.claude\CLAUDE.md` + memory `feedback_output_format_is_a_standing_rule` |
| **Decision rule:** take reversible calls and log each in one line. Reserve `settings.json`, anything scoring, rendered values, collector or trade-store writes, schema changes, and any pick that is cheaper AND less truthful | `CLAUDE.md` "AUTO-PROCEED ON YOUR OWN RECOMMENDATION" |
| **Clock:** the workstation is GMT+8, the project is UTC. Run `date -u` before your first dated claim | memory `project_roadmap_signal_bridge` |
| **NEW this session — read `docs/trader-profile.md` in full at every session start** (trader-ruled 2026-09-14). It was re-synced and reformatted this session; its §5 ATR thresholds block is the single home of the ATR bands | `CLAUDE.md` Session Start Protocol step 3 |
| **NEW this session — all strategy is done by the current Claude orchestrator.** There is no external strategy conversation (the Perplexity seat is retired) | `trader-profile.md` §8 |

---

## 1. ⭐ FIRST TASKS — in this order

⛔ **The trader asked for tasks 1 and 2 to be looked at FIRST, right after startup, before the date-gated builds.**

| # | Task | When | Class under the auto-proceed ruling |
|---|---|---|---|
| **1** | **ASIA aggressor-velocity burst-watch read — OVERDUE** (see §1.1) | Now | Analysis: auto-proceed. A threshold change it might suggest is reserved |
| **2** | **Schedule the nightly store-vs-Deribit trade check (`BacktestRunner coverage --verify-venue`)** — its suspension condition is met (see §1.2) | Now: plan it; the trader approves anything on the box | Plan: auto-proceed. ⛔ A scheduled task on the collector box is a write to the live collector: reserved |
| 3 | **Build S1** of [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) | From **2026-09-15 UTC** | Per the spec. Commit locally, do NOT deploy |
| 4 | **Final `D-1` episode-age read** (`D-1` = the absorption instrumentation's five diagnostic CSV columns) | From **2026-09-16 UTC** | Analysis |
| 5 | **Build S2** (the `analysis_log.csv` header rotation, riders `RIDER-1` to `RIDER-7`), then **ONE deploy** | After S1 | Per the spec; the deploy comes to the trader |

**Model / effort:** tasks 1 and 2 — Opus, effort medium (task 1 escalates to high if the rate lands outside the band, because then it becomes a derivation). Tasks 3–5 — Opus, effort HIGH, per [`seat-handover-2026-09-14.md`](seat-handover-2026-09-14.md) §1.

⭐ **One fetch can serve two reads.** Task 1 needs a fresh copy-back now; task 4 needs one from 2026-09-16. If the trader prefers one fetch, run task 1 on 2026-09-16 with task 4. ⚠ `tools/ops/collector.ps1 fetch` **overwrites the root `analysis_log_aws.csv`** — always read from the dated `aws_fetch/<stamp>/` folder.

### 1.1 Task 1 — the ASIA burst-watch read

| Item | Detail |
|---|---|
| **What it watches** | Settings v65 armed the ASIA session's aggressor-velocity burst modifier at `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` = 5.5 (a live scoring change, 2026-08-02) |
| **Why it is overdue** | The first read, [`d3-asia-burst-watch-read-2026-08-10.md`](d3-asia-burst-watch-read-2026-08-10.md), PASSED on 2026-08-11 and put the next band-eligible read at about 2026-08-17. **No later read exists in `docs/`** (checked 2026-09-14) |
| **Ruled tolerance** (T-1 to T-5, trader 2026-08-11) | Fire rate reference **11.0 %**, band **8–14 %**; read length **≥10 weekday session-days**; same-side TFI share **≥85 %**. A miss triggers a re-derivation READ, never a threshold change. Do not quote the derivation's 9.7 % or its "~106 AggrVel rows/day" |
| **Method** | Reuse the first read's population rule and method: its §1, taken from [`aggressor-velocity-s52-derivation-2026-07-13.md`](aggressor-velocity-s52-derivation-2026-07-13.md) §3. Weekday only, UTC day-of-week, ASIA session, `ExecResolution` 3 |
| ⛔ **Trap: the book rotated 2026-09-01 15:49 UTC** | Rows before that live in `analysis_log.csv.v0.7.bak`, rows after it in the live `analysis_log.csv`. They do not overlap. **Count both** |
| ⛔ **Trap: many InstanceIds** | v65 and every later build span several process restarts. Do not filter on one id. The arming itself is self-checking: armed ASIA rows show `AggrVelSignal = BURST_*` exactly when `AggrVelBurstRatio ≥ 5.5` (first read §5). Deploy ledger: [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a |
| **Traps from the first read** | Its §7 (an InstanceId missing from the ledger) and §8 (an intentional weekend stop that removes session-days) |
| **Deliverable** | A dated read doc (`d3-asia-burst-watch-read-2026-09-XX.md`), then update [`trader-tick-queue.md`](trader-tick-queue.md) §4 and [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §12 (the v52 aggressor-velocity watch row) in the same commit |

### 1.2 Task 2 — scheduling the store-vs-Deribit trade check

| Item | Detail |
|---|---|
| **What it is** | `tools/BacktestRunner` `coverage ... --verify-venue` diffs the trade store against Deribit's public trade history for the 24 h before `--to`. Deribit keeps only about 24 h of public trades, so **each day is checkable only within about 24 h, never later** |
| **Why it is now due** | It was SUSPENDED 2026-08-08 until the store rows carried `trade_id`. `trade_id` + `trade_seq` were deployed to AWS 2026-08-10 (instance `d8678d2b…`), and the venue diff now reports identity matches and fallback matches separately. **No scheduled task exists** (`tools/ops` has no `--verify-venue` call, checked 2026-09-14). Every unscheduled day is a day that can never be verified |
| **Settled design** (from the queue row; the trader ruled AWS only) | Run on AWS only · output to a folder under the app root · the window ends on the previous whole hour, never "now" (clears the 30 s `trade_store.flush_seconds` lag) · a background scheduled task needing no interactive login · the reading is manual, so add a one-line verdict to the daily glance in `aws-collector-deploy-checklist.md` |
| ⚠ **Known gap** | `--strict` grades `Defect` hours only and never reads the venue result, so the exit code cannot signal venue loss. Parse the output, or extend `--strict` in the same change |
| ⚠ **Not verified** | Whether `BacktestRunner` exists on the collector box at all — a grep of `tools/ops/collector.ps1` for `BacktestRunner` found nothing. Check before planning a task that calls it |
| **Suggested output** | A short plan doc: where the binary comes from, the exact command and window arithmetic, the task definition, where output lands, how the trader reads it. ⛔ **Creating the task on the box is reserved — present the plan, do not run it** |

Full original rows: [`trader-tick-queue-archive.md`](trader-tick-queue-archive.md) §D, `trim-2026-09-14b-22`, and §C, `trim-2026-09-14-49`.

---

## 2. What this session did — 7 commits, all `[no-engine-change]`, all pushed

| Commit | What |
|---|---|
| `bd79a51` · `3558fb7` | **Doc trim pass 1.** Superseded and history text moved verbatim out of `DeribitIndicatorProject.md` (80,998 → 28,928 tokens), `trader-tick-queue.md` (72,200 → 23,174) and `architecture.md`, 56 blocks. `DeribitIndicatorProject.md` §15 gained a cell-length cap |
| `4958d1f` · `521f4e7` | **Trader profile:** Perplexity strategy seat removed; 16 stale items re-synced against `settings.json`, the code and the engine docs (trader-ruled); ATR bands given a single home in its §5 |
| `30219b5` · `ccf7b80` | **Doc trim pass 2.** Stale-state sweep (§3 below), `architecture.md` Directory Layout compacted (31 narratives moved, 26 missing files added), `trader-profile.md` reformatted to markdown. 59 blocks. ⚠ `ccf7b80`'s message says 30 files were added; the true count is **26** |

**Traceability for both trim passes:**

| Item | Where |
|---|---|
| Pre-trim originals | Git tags `doc-trim-2026-09-14-pre` and `doc-trim-2026-09-14b-pre`; byte copies in `docs/archive/doc-trim-2026-09-14/` and `docs/archive/doc-trim-2026-09-14b/` (hidden from default search by `docs/archive/.ignore`) |
| Moved text | `history-archive.md` §I and §J · `trader-tick-queue-archive.md` §C and §D · `architecture-archive.md` §A and §B |
| Ledger | [`doc-trim-log.md`](doc-trim-log.md): 115 rows (pass 1 and pass 2) plus a Reformats table |
| Verifier | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/doc-trim-verify.ps1` — last run **115 rows checked, 0 failed**. It derives each row's tag from its ID |

**Session-start sizes now:**

| Doc | Size | Measured? |
|---|---|---|
| `trader-tick-queue.md` | **17,049 tokens** (38,716 B) | Yes, 2026-09-14 |
| `trader-profile.md` | **6,504 tokens** (16,255 B) | Yes, 2026-09-14 |
| `DeribitIndicatorProject.md` | 66,965 B | ⚠ Tokens NOT re-measured after pass 2 (last 28,928 at 66,539 B) |
| `architecture.md` | 68,104 B | ⚠ Tokens NOT measured after pass 2 |

---

## 3. State findings from the pass-2 sweep (each checked in the tree 2026-09-14)

| Finding | Where recorded |
|---|---|
| Coverage cluster built: C-1 `trade_seq` completeness and C-2 startup window (`ea8907e`), C-3a declared schedule (`25b8691`), C-3b Part B venue scope (`16b19f6`). **Committed, not deployed** | `trader-tick-queue.md` §2 index row |
| `analysis_log.csv` header is **116 columns**; `TFIValue` and `TFISignal` are logged, so the TFI threshold sweep is unblocked for the next W1 audit re-run | `DeribitIndicatorProject.md` §10 and §12 |
| `CalcKellySizing` is called from `BuildPlaintextSnapshot`; Kelly CAL stays parked | `DeribitIndicatorProject.md` §7 and §8 |
| Parked items P2 and P4 superseded, P8 and P13 resolved, P10 re-worded for the v51 ladder | `DeribitIndicatorProject.md` §16.6 |
| `roadmap.md` footprint figure dated, with the 2026-08-21 re-measure (app 121–144 MB) | `roadmap.md` line 256 |
| A5 VPFR data gate: last count 28 of 30 dates on 2026-08-07; passed about 2026-08-08 by inference only | `trader-tick-queue.md` §4 |

---

## 4. Tooling and config changed this session (user-level, not in the repo)

| Item | State |
|---|---|
| `~/.claude/settings.json` | `enableWorkflows: false`; model pins (`ANTHROPIC_DEFAULT_*_MODEL`) and the `claude-opus-4-7` model settings **removed**, so `"model": "opus"` should follow the latest Opus |
| Plugins | `session-report` installed and enabled. `hookify` installed but **disabled** — its hooks call `python3`, which is the Microsoft Store stub on this box. Re-enable only after the first rule is written and `python3` works |
| Built-in tools | The trader disabled several (Claude_Browser, visualize and others) and removed the Chrome extension from Cowork. ⚠ `claude_desktop_config.json` still read `chromeExtensionEnabled: true` — check the tool list in your session |
| `crypto-trading-context` skill | Trader edits: "Default to prose" removed from its writing style; its trader-profile copy now points to `docs/trader-profile.md` §5 for the ATR bands |
| Stop hook | `verify-gate.ps1 -Mode local-fast` still runs at every stop. Its output goes to a file, not into context (verified) |

**On hold, trader's call:** trim #5 (compress the project `CLAUDE.md`, 38,988 B) and trim #6 (collapse the superseded seat pointers in `MEMORY.md`). **Declined:** the Engineering plugin, `microsoft-docs`, `claude-md-management`; disabling the docx/pptx/xlsx/pdf skills (used in other projects).

---

## 5. ⚠ Still OPEN (carried, not owed by the trader)

- `_evalCache` unbounded; its obvious fix is destructive; no spec. Row in `trader-tick-queue.md` §2.
- `ws_health.log` under-reports a capture outage. The 2026-09-11 evidence points the opposite way to how that row is worded.
- `DeribitWsFeed` logs the requested channel count, not the subscribe reply (`DeribitWsFeed.vb:208`).
- Fills-import tool LATER · the `S-1` probe parse-site move NOT NOW · RPC code `11051` unverified against Deribit docs.
- ⚠ The venue-status instrument, C-3b, atomic writes and the coverage cluster are committed but NOT deployed; the box has run one process since 2026-09-01. The S2 deploy carries all of it.
- ⚠ `D-1`'s other four columns (`AbsorptionPullLB`, `AbsorptionPostLB`, `AbsorptionSizeStart`, `AbsorptionSizeMin`) have not been read.

---

## 6. ⭐ Lessons from this session

| # | Lesson | Instance |
|---|---|---|
| 1 | **A `Read` on a large file is not free.** Under 256 KB it returns up to 25K tokens of content; over 256 KB it errors with no count. Measure tokens only when the figure is needed | Two measurement reads cost about 50K tokens of context |
| 2 | **A trim can grow a doc.** Replacing a short stale line with a correct line plus a pointer adds bytes | `DeribitIndicatorProject.md` +332 B in pass 2 |
| 3 | **The shipped-state class struck again.** Three coverage-report rows read as open work after their builds were committed | `trader-tick-queue.md` §2, caught by checking commits, not status prose |
| 4 | **A resume condition can be met silently.** Nothing re-opens a suspended item when its condition ships | The venue check stayed "SUSPENDED" for five weeks after `trade_id` deployed |
| 5 | **A plugin that needs an interpreter can break every tool call** | `hookify` calls `python3`; the box has only a Store stub |
| 6 | **A count written in a commit message is a claim like any other.** Run it | `ccf7b80` says 30; the tree says 26 |
| 7 | **Backslashes inside a bash heredoc still break Python string literals.** Use `chr(92)` | A pipe-count check failed to parse |

---

## 7. ⚠ What I did NOT verify

- Collector health (`collector.ps1 status`) was not run this session.
- Token counts for `DeribitIndicatorProject.md` and `architecture.md` after pass 2.
- That the model aliases now resolve to the latest models after the pins were removed.
- The per-session context cost of the `session-report` plugin.
- Whether `BacktestRunner` is present on the collector box (task 2).
- The harness count (376) was carried from the prior handover; the Stop hook ran `local-fast` after each turn but I did not read its output.
