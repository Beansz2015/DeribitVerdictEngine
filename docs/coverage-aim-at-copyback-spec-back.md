# Spec-back — `coverage` aimed at a copy-back (queue item 21)

**Spec:** [`coverage-aim-at-copyback-spec.md`](coverage-aim-at-copyback-spec.md)
**Implementer:** this session, Sonnet, 2026-09-03. **Effort actually used: low**, matching the spec's own recommendation.
**Status:** build done, gate green, **not yet committed** — trader deferred the commit at end of session. `git status -sb` at handoff: `## master...origin/master [ahead 2]`, `M tools/BacktestRunner/BacktestProgram.vb`.

Per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). This is a single-item build, not a multi-lane batch, so no separate `-summary.md` — this packet carries both the record and the review questions.

---

## 1. Ranked verification handles

**If you only run one: #2.** It is the one that proves the feature does what the queue item exists for.

| # | Handle | What it confirms |
|---|---|---|
| 1 | `git diff -- tools/BacktestRunner/BacktestProgram.vb` — one file, 37 insertions / 2 deletions | Scope matches the spec's own prediction (§0: "two option cases … five assignments … no new file") |
| 2 | Run `coverage --evidence-dir AWS-copybacks/aws-copyback-2026-09-01/aws_fetch/20260901-153838 --from 2026-08-25 --to 2026-09-01`, compare `captured hours` and `DEFECT` against the same command **without** the flag | Default run: `captured hours 0`, `DEFECT 0`. Evidence-dir run: `captured hours 119`, `DEFECT 1`. **Both numbers move together** — not a coincidental single-field change |
| 3 | Diff two default (no-flag) runs against each other | Byte-identical — confirms R1's guard: the new flag path is dead code until invoked |
| 4 | `--store-dir` alone: check the run banner still says `S1 (uptime) SKIPPED` (evidence paths untouched) while `seq gaps (local)` reports a real row count (store moved) | Confirms R2 — store and evidence are independently addressable |
| 5 | Both flags together, with **different** stores under `--evidence-dir` and `--store-dir`: `captured hours` came back **87**, distinct from both the evidence-dir-alone run's **119** and a store-dir-alone run's **0** | Confirms precedence — R2 applies after R1, not "last flag wins" or "evidence-dir wins" |
| 6 | `Select-String "HistoricalStore.StoreDir" tools/BacktestRunner/BacktestProgram.vb` | Two hits: the unchanged default-value assignment and one read inside `Path.Combine(evidenceDir, HistoricalStore.StoreDir)`. **The const itself is never assigned to** — T1 held |
| 7 | Bad `--evidence-dir`: exit code and stderr | `1`, and the printed path is `Path.GetFullPath` of the argument, not the raw string — an operator can copy-paste it straight into an `ls` |
| 8 | `tools/checks/verify-gate.ps1` tail | `GATE PASSED` — harness ALL PASS, `display-parity: no snapshot/card drift`, `version-bump: no engine-path change` |

---

## 2. Decisions queued

**None.** The spec's own header states "No decision outstanding," and nothing surfaced during the build that needed one. One observation queued as color, not a decision — see §3, second bullet.

---

## 3. Spec-back proper — feedback on the spec itself

**What it got right, specifically:**

- §2's trap check (**"`CoverageReport` never touches `HistoricalStore.StoreDir`"**) was exactly right and saved a second investigation pass — I grepped it myself as verification handle #6 above rather than trusting the table blind, and it held.
- The acceptance table's item 2 (**"byte-identical on no new flag"**) is the right single guard for this class of change — a redirect that isn't a no-op by default is the whole failure mode T1/T2 exist to prevent, and it's cheap to check (two runs, `diff`).
- R6's framing — *"a silent fallback would audit the local box while the operator believes they audited the copy-back"** — is worth reusing verbatim in the next CLI-redirect spec. It's the one sentence that makes the "fail loudly" requirement obviously correct rather than merely cautious.

**Where the spec was narrower than its own words:**

- §3/R1 says *"the copy-back's `aws_fetch/<stamp>/` directory already has exactly the layout `coverage` wants."* **True for the two most recent copy-backs** (`aws-copyback-2026-08-28`, `aws-copyback-2026-09-01`) **but not the four before them** (`2026-08-07` through `2026-08-14`), which have no `aws_fetch/<stamp>/` wrapper at all — `analysis_log.csv`, `backtest_data/`, `ws_health.log` sit directly under the copyback root, and three of the four have no `capture_marker.log`. This isn't a defect: pointing `--evidence-dir` at the root of one of those older copy-backs still works, because a missing `capture_marker.log` degrades gracefully by existing design (the header comment's own words, unchanged by this spec). But an operator reading only R1's wording would expect one fixed sub-path pattern across all copy-backs, and that pattern only holds from `08-28` on. Worth a one-line note in whatever doc catalogs the `AWS-copybacks/` directories, if one exists — I did not add one, since it's outside this spec's file list.
- Nothing else broke. The five-path threading, the two-override composition order, and the "no fixture family" call in §4 all held exactly as written.

**Constraint pairs:** none found in tension. R1 (evidence-dir sets four paths) and R2 (store-dir independently overrides one of them) compose cleanly because R2 is defined as strictly-after; there was no ordering ambiguity to resolve.

---

## 4. What I did not verify, and cannot

- **Whether the coverage numbers `CoverageReport` now produces against a copy-back are themselves correct** — out of scope per the spec's own §5, first bullet. The `DEFECT 1` / `captured hours 119` figures against `aws-copyback-2026-09-01` are reported as evidence the redirect *works*, not as a claim about capture health.
- **`--verify-venue`'s byte-for-byte determinism** — it hits the live Deribit venue over the network (S0). I ran it once successfully (handle table, acceptance item 6) and saw a plausible, non-zero `store rows in window` figure keyed to the copy-back's store rather than the empty local one, which is what T2 required. I did not re-run it to compare two network calls byte-for-byte, since venue-side state can legitimately differ between calls and that is not this spec's concern.
- **Item 18** (`ObservedLongestTrailingMs` over-reporting on a split hour) — explicitly out of scope per spec §5, second bullet. Not touched, not tested against the newly-aimable copy-backs.
- **The commit itself** — held back at the trader's instruction at end of session. `git status -sb` must be re-read before assuming push state; do not inherit "ahead 2" from this document once new commits land.
