# Seat handover — 2026-08-02 (orchestrator/reviewing seat)

**From:** the Opus orchestrator seat that opened on the 2026-08-01 Fable seat-close handover and ran through the v64 landing.
**Read in this order:** CLAUDE.md session-start protocol (**it now has a step 6 — read it, it is the state rule**) → [`trader-tick-queue.md`](trader-tick-queue.md) **§0 orientation, then §1** → this doc. Everything else is reachable from the queue.

> **The single most important thing I can hand you:** the handover I inherited had a **wrong headline and twelve omissions**, and a later sweep found **4 of 13 queue rows describing already-shipped work as outstanding**. Both were traceable to *prose that had gone stale*. So: **verify state in the tree before you assert it.** The queue's §1a/§1b carry the convention and the one-line command. **I broke this rule three times myself in one session** — §6 lists them; the third was in the queue itself, the doc everything else points at.

---

## 1. State — verified 2026-08-02 ~17:57 UTC, with how to re-check

| Fact | Value | Re-check |
|---|---|---|
| Settings version | **v64**, tracked **and** bin | `Get-Content settings.json` line 2 — **the TRACKED repo-root file**; `bin\` is a build artefact and legitimately lags |
| Unpushed | **check it** — this number goes stale within the hour | `git status -sb` |
| Next free fixture family | **A52** (A50 + A51 consumed) | `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'` → highest is `A51e_` |
| Next free hard constraint | **HC28** | HC27 consumed by the v64 `trade_store.` fence |
| AWS collector | **live, v64, capturing** — `5a3afd99-6db4-461c-886e-dddcca3d8c62` since 2026-08-01 17:50 UTC | `ws_health_aws.log` tail at next copy-back |
| Local collector | ✅ **running** — `2f8c9fe1-8325-4fbb-9ee5-41fc267e1efd` since 2026-08-01 18:00:26 UTC, rows landing, title carries `+local` | `Get-Process DeribitVerdictEngine` |
| Local capture | **OFF and correct** — overlay present, `backtest_data\` absent | both under `bin\Debug\net8.0-windows\` |

**On the local box, whenever you find it down:** it is an *opportunistic addendum*, not a 24/7 collector — the trader ruled that on 2026-07-31 and it is why the dedup is AWS-preferred. **Its silence is the default state, not a defect**, so do not treat a gap in the local book as an incident. It was down twice during this session and restarted both times without anything being wrong. Any coverage report must scope J-B's "silence ⇒ defect" per-box or it will flag most of local's existence — that clause is **flagged and unruled**, see §4.

---

## 2. What actually happened this session

**The v64 landing completed, end to end.** F6 answered by option (c) → the `settings.local.json` overlay specced-corrected-built-reviewed-fixed → local test run with capture correctly off → AWS deployed and **raw-trade capture began, anywhere, at 2026-08-01 17:50 UTC**. That last one closed the item the v64 review called unverifiable without a live run.

**Rulings made** (all recorded in their own docs, all reachable from the queue): AWS-preferred minute-key dedup · **W6-1 LONDON: no change** (the two candidates are one lever — `stop_buffer_pct` applies after the clamp, and the clamp binds 95.5 % of LONDON rows) · **D1 widened to full VWAP clearance** · J-D ratified **and extended** with an instrument-integrity clause · JOB 2's D-C/D-D/D-E · J-A ratified from a fresh fixture read · D-cluster sequencing (**D3 alone first; D1+D2 bundled after D-A's re-derivation**) · Kelly: option (c) for the measurement, honest display now.

**Three instruments independently said the same thing**, and this is the strategic finding of the session: **W6-4** put the engine's own score at **AUC 0.5407** with no challenger able to beat it; **F1 §9** found the tier bands derived from that score **do not separate** (MEDIUM 42.2 % ≈ WEAK 42.7 %, LONDON inverted at the top); the ceiling audit's informational column put structural placement at **0.4683 / 0.5000 / 0.3221**. Produced within a day, on different code paths, none designed to corroborate the others. All small-n and touch-based, so none is decisive — **but do not spend on scoring refinements without weighing them.** W6-4's own instruction is *"no spend meanwhile."*

---

## 3. What is open

**Read [`trader-tick-queue.md`](trader-tick-queue.md) — §0a first, then §1 for detail.** §0a is the short explicit list of what is actually owed; §1 records history with strikethrough and does **not** answer "what is unanswered" at a glance. **Do not rebuild the queue from a grep over doc prose — that is exactly how 4 of 13 rows went wrong.** Re-run the §1b sweep instead.

**Cluster A is complete** — both boxes live on v64, capture where D1 put it, Kelly confirmed.

**Seven items are genuinely open**, and only five are the trader's: **C1** (coverage report D1–D7) · **the J-B scoping clause** — *a ruling seat's, not the trader's, and it gates C1* · **D1** (TTM — **cannot be ticked as written**, needs re-derivation) · **D2** (OBV, ready, bundles with D1 after) · **D3** (ASIA, ready, **ships alone and first**) · **E5** (absorption Path B — the path is unticked) · **the F3 watch** — *a tooling decision; it is live and unevaluable.*

⚠ **E1 Kelly is DECIDED, not pending** — 2026-08-02, option (c) wait-for-separation plus the honest display, which shipped `18b1ea8` and is confirmed. **CAL is parked, not owed.** A stale row of mine in the queue said otherwise and a fresh seat believed it; corrected. E6, E2, E3, E4 and Cluster A are likewise closed.

---

## 4. Open items I flagged and deliberately did not rule

- **J-B needs a per-box expected-uptime scoping clause** before the coverage report is built. Unscoped it classifies most of the local box's existence as defect. **This is a correction to a Fable ruling and was not mine to make.**
- **The F3 watch outlived its instrument.** Its trigger needs outcomes segmented by cap bucket, and nothing has produced that since the 2026-07-21 placed-target migration retired the axis. Either add the segmentation or retire the trigger — **do not leave it live and unevaluable.**
- **`session_block_semantic` is unread — but "dead key" understates what removing it costs.** *Corrected 2026-08-02 by the incoming seat, verified in the tree.* It is declared in `EngineSettings.vb` **and shipped as a live key** in `settings.json` (`performance_display.session_block_semantic: "most_recent"`), and its own `change_log` entry declares it *"reserved; currently always most-recent-block per §2b"* — a deliberate placeholder, not a leak. So retiring it is a **settings change needing a version bump + `change_log` entry**, not a code-only cleanup: it belongs with the decisions rather than in the queue's §2 build slots, and it is a valid carrier for the §3 `change_log` rider.
- **The CeilingAudit expected-version constant is stale** (warns `expected 59` against live v64).
- **F2 and F3 from the v64 review** remain open — a lossy `ResetBufferState` race and a cosmetic User-Agent. Both small, neither blocking.

---

## 5. Conventions established this session — these have reach

1. **A spec's status header is not evidence of code state.** Verify in the tree before offering any spec as work: `git log --oneline -S'<symbol>' -- <file>`.
2. **Authority is scoped, not ranked.** Now in CLAUDE.md step 6 and the queue's §0.
3. **J-D extended:** safe to diverge per box when **(i)** it cannot move the failure rate **and (ii)** it cannot change what an evidence instrument records that a queued decision depends on. Clause (ii) is what kept `alerts.` out of the overlay whitelist — it gates `liq_events.log`, the sole A4 gate.
4. **A whitelist validates a key's authority, never its existence.** From the overlay F1: `TRADE_STORE` failed loudly, `enabledd` failed silently. Any future allow-list over a config surface wants both checks.
5. **A trailing interval on a copied file is bounded by the copy time, not by now** — or every AWS copy-back reads as a fresh death.
6. **Display strings must not carry measured numbers or dates.** They go stale; the Kelly advisory deliberately carries neither, and the promise it *does* make is backed by a dated watch.

---

## 6. Things I got wrong, recorded plainly

1. **I carried B1 (eval `NO_DATA`) as outstanding for several turns, let the trader tick it, and recommended an implementer conversation — for work that shipped 2026-07-21.** I even "corrected" its stale header from the D-table without checking the tree, which fixed the wrong half. It surfaced only when I read the code to write the implementer brief. **Second occurrence of that shape in this project** (see `fable-handover-2026-07-31.md` §6.2), which is why it is now a convention.
2. **I said A2's outstanding set was D1–D7 for several turns.** It was **D1–D6**; D7 had been answered on 2026-07-31.
3. **I hand-waved the W6-4 re-run as "roughly late September"** and flagged it as needing confirmation. When I finally measured it, it was **~4 weeks, not ~2 months** — wrong in the pessimistic direction.
4. **I gave a Bash-form path in a block the trader ran in PowerShell**, and MSBuild read the leading `/` as a switch.
5. **I introduced a markdown defect in the tick queue** — a prose paragraph inserted between table rows orphaned the A3 row out of its table.
6. **I left the queue's E1 row reading "needs an explicit trader decision" after the trader had decided it.** I wrote the decision into its own doc and never returned to the row, and a fresh seat reading the queue — **the doc §0 designates as the state read** — reported Kelly as unanswered. **Third instance of the same shape in one session** (B1, the D1–D7 count, this), and the worst-placed of the three, because a stale row here is wrong in the file everything else points at. It is what §0a now exists to prevent.

---

## 7. What I did not verify

- **No live app run on AWS beyond the trader's screenshot and logs.** Capture is confirmed *started* (`backtest_data\` appeared); nothing has yet verified a month rollover, gap-repair's first real HTTP call, or buffer behaviour across multi-day uptime.
- ~~**The Kelly display change is unconfirmed visually on either box.**~~ **CLOSED 2026-08-02 — trader-confirmed visually**, `[EST]` tag renders correctly.
- **W6-4 and F1 figures are touch-based**, no slippage, no queue position — every offline surface in this project shares that caveat.
- **I did not re-derive the block-by-block scoring-path analysis** the overlay whitelist rests on; I audited the seven blocks the first reviewer had not, and took §2.4's own enumeration as given.
- **`performance_display.` is admitted to the overlay on "no tool reads the eval cache"**, verified by enumeration on 2026-07-31 and not re-checked since. Kelly CAL was the named candidate to break it — and F1's read has made CAL unlikely to ship soon, so that admission is *safer* now, not less.
