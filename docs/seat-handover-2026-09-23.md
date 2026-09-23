# Seat handover — 2026-09-23 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handovers:** [`seat-handover-2026-09-22b.md`](seat-handover-2026-09-22b.md) — superseded for state; **every row of its §0 table except the collector check is now closed or re-stated below.** [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §1 (what Jev is), §2 (the harnesses), §5 (failure patterns) and §6 (the collector read) **still bind**.

⛔ **Run `date -u` first.** This seat started at 2026-09-22 18:51 UTC while the app displayed 2026-09-23 (GMT+8), and closed at 2026-09-23 ~08:40 UTC.

**State at close (2026-09-23 ~08:40 UTC):**
- ⛔ **`master` is 29 commits AHEAD of `origin`, UNPUSHED** (22 this session, 7 before it). All `[no-engine-change]`. The trader tests, then pushes.
- Settings **v68**, untouched. **Nothing deployed. The collector was not touched.**
- Fixture harness **425 PASS / 0 FAIL** on a clean `-t:Rebuild` after the last `.vb` change (`4615e5d`).
- One live-path file changed, behaviour-identical: `UI/MainForm_Layout.vb` (item `8e`, the MTF TTL constant now lives in `MtfRefreshPolicy.TtlSeconds`). The solution builds 0 warnings / 0 errors.

---

## 0. ⛔ FIRST ACTIONS

**Nothing is running.** No agent, no watcher, no scheduled task.

| # | Action | Model + effort | Why here |
|---|---|---|---|
| **1** | ⛔ **The 2026-09-24 06:00 UTC collector check** | Sonnet, low | Dated. Read [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §6 first: `collector.ps1 status` cannot read `ws_feed.log`; only `fetch` retrieves it |
| **2** | **Harness 4 build** — [`doc-scanner-check-spec.md`](doc-scanner-check-spec.md) | **Opus, medium** (its §0) | Spec written from a measurement. ⛔ **Do NOT fix the five seeded live doc positives** ([`doc-scanner-measurement-2026-09-22.md`](doc-scanner-measurement-2026-09-22.md) §5) before harness 4's first run — they are its recall check, with pre-registered seat labels |
| **3** | **`FP-1` question revision** — item `8o` in [`fixture-parser-check-spec.md`](fixture-parser-check-spec.md) §8.4 | Opus, high | `FP-1` agreed on only 4 of 22 in the latest window; one criteria sentence moved a controlled probe. Needs a spec line, then a fresh baselined population — the change breaks comparability |
| **4** | **Firewall handling in harnesses 1 and 2** | Sonnet, medium | Item `8n`: they share `Invoke-Jev` (which now flags `WafBlocked`) but still abort on a block. Harness 1's first run is one-shot |
| **5** | The open adversarial-review findings (4, 6, 7, 8, 9, 11) | Sonnet, medium | [`jev-harnesses-adversarial-review-2026-09-22.md`](jev-harnesses-adversarial-review-2026-09-22.md). Finding 4 (harness 3's bare-name signature lookup) first |
| **6** | ⭐ **A second reader for this seat's harness changes** | Opus, high | Not self-reviewed: `fixture-parser.ps1` `FP-D21`–`FP-D26`, `commit-walker.ps1` `CW-R3-*`, `rider-travel.ps1` `RT-R1-*`, `lib/InvokeJev.ps1` |
| **7** | Harness 5, the doc re-ranker | Sonnet, medium | Unstarted |
| **8** | Harness 2's measured first run | — | Unspent. Name the window with `-Since`; ⚠ this seat's own commits are conflicted for any seat that saw them |
| **9** | Harness 1's first run | — | Gated on the absorption S2 header rotation. Its baseline now needs all 8 riders (6 are decided in code) |

⛔ **THE ENGINE QUEUE STAYS ON HOLD** until the Jev programme closes (trader direction). Nothing in [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §4 was touched — four sessions now.

---

## 1. What happened — 22 commits

| Theme | Commits | Result |
|---|---|---|
| `8a` re-measure (the perishable one) | `73a721c` baseline → `d740fc0` | **4 new sites, not 6.** 5 of 5 agree; `n=1` on the deciding arm |
| `8f` + `8i` (constructor default-fallback shape) | `ed3e487`, `64f1e85`, `36479ea` | Built; `8i` closed. Its one new site: **the first STABLE disagreement** (protocol §4g) |
| `8h` (sample `FP-Q1`) + item-level baseline refusal | `cbc2c91` | 160 draws, 0 unstable |
| `8j`: 34 unmarked in-scope literals `FP-1` never saw | `ed3e487` (counters); declarations `44e4e15`, `8028d08` (+merge `4615e5d`) | Declarations written by **separate Opus 5.5 agents, model ID checked first** (trader-ruled). Measured: `3874d18` → `50e7788` (12 of 14); `6f0e142` → `b9c9b53` (**`FP-1` 4 of 22**) |
| `8m` (a misread counted as a pass) | `cea0584` | `shipped_declared_ok` now counts as bad |
| `8e` (MTF TTL constant) | `526ecf2` | Trader-ruled; behaviour-identical; mutation-checked |
| Queue pointer fix | `cea0584` | `trader-tick-queue.md` §0 no longer names a handover file |
| `CLAUDE.md` `A20a`/`A20b` example | `50e7788` | Corrected: 2.0/0.5 were shipped values until v48 |
| Harness 4 measure → spec | `6f95e42`, `48a08f7` | Code-only; 111 labels pre-registered at `cbc2c91` |
| Adversarial review of harnesses 1–3 | `b69a129`, `37d53a0` | 3 HIGH, 3 MED, 5 LOW; HIGHs fixed: `90c0db1` (harness 2), `f4dd8c7` (harness 1) |
| Web firewall (`FP-D26`) | `b9c9b53` | A 403 HTML block page on one item's text; now recorded per item, run continues |

---

## 2. ⭐⭐ Findings worth carrying

1. ⛔⛔ **A detector's accuracy belongs to the POPULATION, not the detector.** `FP-1` agreed 23/25, then 12/14, then **4/22** — with **15 stable wrong rows in one run**. Stability did not flag them; probability did not separate them.
2. ⛔ **The obvious cause was refuted by a probe before it was written down.** Removing every shipped-value sentence changed nothing; one criteria sentence did.
3. ⛔ **The API sits behind a web firewall.** Fixture comment text (*"and 4 + 7 = 11"*) can read as an SQL attack and get a 403. It aborted runs until `FP-D26`.
4. ⛔ **The app compiles 8 `tools/` files.** Harness 2 treated `tools/` as never-app, in its path rule and in its question text; `5346bc0` slipped through.
5. ⭐ **Code beats judgment wherever a truth source exists** — harness 1 now decides 6 of 8 riders by exact membership; harness 4's measurement found code catches every rot shape with a mechanical source of truth.
6. ⭐ **A separate author for declarations kept the seat's baselines independent**, and a stated model-ID check caught nothing wrong this time (both agents reported `claude-opus-5-5`).

---

## 3. Lessons — each one cost something this session

| Lesson | Where it bit |
|---|---|
| **Never chain `git checkout <sha>` into a measurement command.** Use a `REV` variable | Detached HEAD mid-session; restored, nothing lost |
| **`git checkout -- <file>` restores the WHOLE file**, not just a mutation | Wiped an uncommitted constant during the `8e` mutation; re-added |
| **Non-raw Python strings write control bytes** (`\b`, `\1`) | Twice, in scratch scripts; caught by the control-byte scan |
| **PowerShell variable names are case-insensitive** — `$samples` overwrote `-Samples` | Caught by the synthetic keyed test; same trap as the VB memory |
| **Count before you write the number** | Three counts in my own drafts were wrong and caught on re-check (13 not 20; 6 not 9; 8 not 7) |

---

## 4. ⚠ What I did NOT verify

- **Anything about the collector.** Not read this session.
- **Whether the firewall blocks harness 1's or 2's real text.** Untested; both still abort on a block.
- **The cause of `FP-1`'s misreads.** One cause refuted; none named.
- **This seat's own harness revisions** — see §0 row 6.
- **`A17g`'s name.** Seat and detector disagree; unresolved.
