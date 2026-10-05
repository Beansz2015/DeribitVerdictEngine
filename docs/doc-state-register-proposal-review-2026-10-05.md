# Doc state register proposal — review and recommended answers

**Reviewed:** [`doc-state-register-proposal.md`](doc-state-register-proposal.md) at `4d5c6ae`. **By:** the orchestrator seat, Opus 5.5, high effort, 2026-10-05 (UTC). **Trader-requested.**
**Harness 6** (the decision-bias tripwire, `CLAUDE.md` Session Start item 7) ran on all eight reads below, with my labels written first: [`harness-runs/decision-bias-20261005T1230Z-jev.json`](harness-runs/decision-bias-20261005T1230Z-jev.json).

**IDs used here:** `DSR-1`–`DSR-8` are the proposal's own decision rows, in its decision table (`doc-state-register-proposal.md` §8). `C1`–`C5` are its gate checks (`doc-state-register-proposal.md` §4.5). `RV-n` are this review's findings.

## 1. Verdict

- **The diagnosis is right, and the design is sound.** Staleness comes from one fact restated in many places. A keyed register plus a gate is the correct shape.
- **Three findings change the design before P1** (`RV-1`, `RV-2`, `RV-3`). `RV-3` reverses the proposal's own read on `DSR-4`.
- **Accept with the changes below; build after 2026-11-25** (`DSR-7`).

## 2. Findings

| ID | Severity | Finding | Fix |
|---|---|---|---|
| `RV-1` | **High** | **`C2` checks the wrong place for a ruling.** It requires the doc *row* to carry a ruling marker. Recent rulings sit in a **RULED box above the table**, e.g. `a4-liq-ofi-logged-era-study-spec.md` §9 and `burst-outcome-read-rv-fixes-spec-back.md`. `C2` would FAIL on rulings recorded correctly | `C2` accepts a ruling marker in the row **or** in a RULED box in the same doc that names the ID. Fixture: one doc with each shape |
| `RV-2` | **High** | **`C3` trips on its own correction lines.** A correction quotes the stale word: on 2026-10-02 `trader-tick-queue.md` gained *"`HH-1`–`HH-3` ✅ RULED 2026-09-28 … this said 'queued'"*. That added line pairs "queued" with a ruled ID, so `C3` FAILs the push that fixes the staleness | Exempt an added line that also carries a ruling marker (RULED, ✅, TICKED) or quotes the word. Fixture: that exact line. Promote to FAIL only after measuring false positives on 30 days of commits (as `doc-state-register-proposal.md` §9 already plans) |
| `RV-3` | **High** | **The ratchet in `DSR-4` (a) misses the recorded failure mode.** Its argument is that a row is added "the moment its doc is touched, which is exactly when a stale copy could be written". But every recorded stale claim was written in a **different** doc: the queue said `HDS-1`–`HDS-4` (history-store decisions) and `HH-1`–`HH-3` (history-host decisions) were open, while their own docs were not touched. Under (a), the register holds no row for them, so `C3` cannot fire | Seed by **citation**, not by touch: see `DSR-4` below |
| `RV-4` | Medium | **Keys depend on doc stems, and docs move.** Doc trims move text into `history-archive.md`; files are renamed. A move breaks every key on that doc, and `C1` then FAILs on the dangling path | Never rename a doc that holds register rows; on a forced move, add an `aliases` field and let `C1` resolve through it |
| `RV-5` | Low | **Agents queue decisions too** (three today: `RVF-1`–`RVF-3`, `A4L-9`, `A4L-10`). The proposal names seats only | Every implementer brief must say: "add a register row for each decision you queue". Put it in the brief template |

## 3. Recommended answers

| ID | Decision | Recommend | Why | Harness 6 |
|---|---|---|---|---|
| `DSR-1` | Register format | **(a) JSONL in git** | Same-commit rule: a state change must land with the change that caused it. SQLite cannot be diffed or merged; GitHub Issues leave the repo and `git log -S`. These are mechanism reasons (step 3 of the `CLAUDE.md` three-step test), not cost | ⚠ Flagged `gives_up_for_economy`, low confidence (0.41). I keep the read: the richer options break the same-commit property |
| `DSR-2` | Key scheme | **(a) `<doc-stem>#<id>`**, plus `RV-4`'s aliases | (c) is wrong: 291 IDs collide. (b) would rewrite 210 docs **and cannot rewrite commit messages, memories or handovers that cite the old IDs.** Every existing citation would dangle | ⚠ Flagged, low confidence (0.47). Mechanism reason (step 3); I keep the read |
| `DSR-3` | Where the ruling text lives | **(a) both, kept consistent by `C2`**, with `RV-1`'s fix | The most self-describing option: the doc still says what was ruled if the register is lost | No flag |
| `DSR-4` | Backfill scope | **Your call. My lean: (c) living docs, seeded by citation (`RV-3`).** Seed every decision ID **cited** in the queue, the roadmap, `outstanding.json` and the handovers of the last 30 days, each read in its own D-table | (c) closes the recorded failure mode. **(b) records more**: full history, so a seat greping any old ID gets an answer. My reason not to pick (b) is cost (several sessions, separating decisions from findings tables). **That is the reserved class, and harness 6 agrees** | ⛔ **Flagged `gives_up_for_economy`, high confidence (0.86); my own label says the same.** Reserved to you |
| `DSR-5` | `C3` severity | **(a) FAIL on push, added lines only, with `RV-2`'s exemption, after the 30-day false-positive measurement** | (c) fails on day one across 555 files; (b) is the alarm that gets ignored | No flag |
| `DSR-6` | The queue's role | **(a) order and gates only** | (b) keeps two homes for state, which is the cause. Change `CLAUDE.md` Session Start item 6 only when P3's generated view exists | No flag |
| `DSR-7` | Timing | **(a) P1 after 2026-11-25** | The holiday bars builds; a half-built gate over 6 unattended weeks helps no one | No flag |
| `DSR-8` | The two parked Jev sweeps | **(a) keep parked; re-scope after P2** | The register supplies the ground truth both sweeps lack | No flag |

## 4. Not verified

- That every recent ruling uses a RULED box (`RV-1`): I named the two I wrote or reviewed; I did not survey the 210 docs.
- The size of the `DSR-4` (c) seed: not counted.
- `RV-2`'s false-positive rate: the 30-day measurement is still owed (P2).
