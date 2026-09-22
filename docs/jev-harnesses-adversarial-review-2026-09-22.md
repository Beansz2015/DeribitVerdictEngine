# Adversarial review — Jev harnesses 1–3, 2026-09-22 (UTC)

**Trader-requested.** Reviewer: the orchestrator seat (Opus 5.5). **Scope:** `tools/checks/rider-travel.ps1` (harness 1), `tools/checks/commit-walker.ps1` (harness 2), `tools/checks/fixture-parser.ps1` (harness 3), `tools/checks/lib/InvokeJev.ps1` — as built by the previous orchestrator's seats. **Method:** read in full; every claim below checked against the tree or with a code-only run. ⛔ **No keyed run was made:** harness 1's riders and harness 2's post-2026-08-12 commits are unspent one-shot populations, and every script run here had `TYPESAFE_API_KEY` stripped.

⚠ **Conflict:** this seat modified harness 3 today (`FP-D21`–`FP-D25`). Those revisions are **not** reviewed here; they need a reader other than their author.

---

## Findings, ranked

| # | Sev | Harness | Finding | Evidence |
|---|---|---|---|---|
| 1 | ⛔ HIGH | 2 · commit walker | **The shipped app compiles 8 `tools/` files, and the harness treats `tools/` as never-app — in the path rule AND in the question text.** `DeribitVerdictEngine.vbproj` compiles six `tools/AutoTweaker/*.vb` plus `tools/BacktestRunner/HistoricalStore.vb` and `BacktestFundingSample.vb`. The `verdict` instructions tell Jev *"`tools/` and `verify/` changes are NOT app changes, however large"* — false for those 8 | **1 of 354** tagged post-era commits slipped through: `5346bc0` (DR-3) touched only `HistoricalStore.vb`, whose return value `TradeStoreGapRepair.vb` sums and logs, so a written value changed. The path rule filed it as agreeing, so it was never judged. And any residual commit touching `Core/` **plus** a linked file gets a false instruction |
| 2 | ⛔ HIGH | 1 · rider travel | **Its one-shot first run is unprotected four ways.** (a) One Jev call per rider — no self-consistency, which [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b requires of every harness; it was built before that rule and never brought up to it. (b) File-level baseline refusal only. (c) **No rotation guard:** at `COLUMNS_ADDED=0` it proceeds to the gate, so a run before the rotation spends all 8 riders on an unrotated header. (d) The judged rider list is never printed before the gate (§2 step 2) — only the non-travelling ones | Key-stripped run today: `HEADER_COLUMNS_AFTER=116`, `COLUMNS_ADDED=0`, straight on to `BASELINE_MISSING`; the output names `RIDER-8` and one other, never the 8 it would judge |
| 3 | ⛔ HIGH | 2 · commit walker | **A baseline written from the printed list will silently not match.** Candidates print as a **10-character** hash; the baseline must be keyed by the **full** hash. The `BASELINE_MISSING` example still shows the retired `tag_correct`/`tag_wrong` vocabulary. Refusal is file-level only, so unmatched items are judged with `NO_BASELINE_VALUE` — **the unspent measurement window spent with no baseline** | `commit-walker.ps1` lines ~462–465 (print) and ~516–517 (example), baseline lookup by `$c.Hash` |
| 4 | ⚠ MED | 3 · fixture parser | **Signatures are looked up by bare method name; the first declaration in file order wins.** A POSITIONAL mapping read off the wrong class's signature is a confidently wrong key | `Compute` has 5 declarations — the fixture's `currentATR` is read against `analysis/BandLadder.vb` and reports a false `PARAM_NOT_IN_SIGNATURE`; `Evaluate` (3) does the same to `nowUtcMs`. `Fold` and `Snapshot` resolve correctly **only because** `Core/AggressorVelocityAccumulator.vb` sorts first. A new earlier file declaring `Fold` flips them silently |
| 5 | ⚠ MED | 1 · rider travel | **It asks Jev a set-membership question code can answer exactly.** 6 of 8 travelling riders name their column in backticks (`TriggerMode`, `WsHealth`, `SettingsVersion`, `SettingsLoadError`, `RecentTradeCount`, `VPFRSignal`/`VPFRPoc`); the tool sends all 116+ columns and asks whether *"a column matching"* appears. The Jev docs name *"asking the model something code can compute exactly"* as their first anti-pattern | `docs/csv-rotation-riders.md` §1 rows `RIDER-3`–`RIDER-7`, `RIDER-9` |
| 6 | ⚠ MED | 1 and 2 | **An OK-class misread passes the exit code** — the shape fixed for harness 3 today as item `8m`. Harness 1 exits 0 on `not_a_header_column`; harness 2 exits 0 on `CONSISTENT` (a stable `no_app_change`). §4g of the protocol now records three stable wrong rows, so **a stable exit 0 is not evidence** | Finding 1 is exactly this: a linked-file commit misread as `no_app_change` would read `CONSISTENT` |
| 7 | LOW | 3 | **Item IDs carry a line number**, so every `Program.vb` edit orphans old baselines. Loud since `FP-D24`; silent before it. Hit today: the `A23b` IDs moved with item `8e` | `Sub#Line#Param` |
| 8 | LOW | 3 | **The marker is block-level.** 8 of 58 judged sites sit under a comment that never names their parameter; the `MECHANISM` keyword marks them anyway | Code-only dump; for example `A1_CvdSlopeRising#863#lateSegmentWeight`, the `A65c` pair |
| 9 | LOW | 3 | **The settings walk skips JSON arrays**, and `settings.json` keeps per-session values in `session_volume.sessions[]`. An array-backed key can never resolve | Latent: 0 `CFG_PATH_NOT_FOUND` sites today |
| 10 | LOW | 2 | **No `-Since` parameter.** The unspent window must be expressed as `-Skip`/`-Count` by hand, and an off-by-one overlaps the contaminated first 300 | Parameter block |
| 11 | LOW | shared | **`Invoke-Jev` never retries.** One transient 5xx aborts a whole run | `tools/checks/lib/InvokeJev.ps1` |

---

## What held up

- **One HTTP call site, UTF-8 bytes on the wire.** The encoding fix is correct and shared.
- **The verdict is read from one Choice everywhere.** No Noul is combined with another in any of the three.
- **Harness 2's sampling logic is sound.** `UNSTABLE` is structurally unreachable from escalation; `CONFLICT` picks no winner; one aggregation function serves both loops; the era cutoff is derived, not hardcoded.
- **Harness 2's `-CountersOnly`** withholds verdicts from an acceptance window by construction, not by discipline.
- **Harness 3's settings-history cache** keys on commit hash, so a new revision is always walked.

---

## Recommended fixes, in order

1. **Harness 2 — derive the app-compiled file set from `DeribitVerdictEngine.vbproj`'s `Compile Include` entries** and treat it as engine paths; correct the question text to say *"`tools/` files the app compiles ARE app changes"*. Self-correcting, the same argument that ruled harness 3's mapping must come from production code, not a hand table.
2. **Harness 1 — before its first run:** 5-sample self-consistency; item-level baseline refusal; refuse when `COLUMNS_ADDED=0`; print the judged riders; and compute presence in code for every rider that names its column, sending only the vague ones to Jev.
3. **Harness 2 — before its measured run:** item-level refusal; print full hashes; fix the example vocabulary; add `-Since <sha>`.
4. **Harness 3 — refuse a callee name with more than one production declaration** unless the receiver's type resolves it, reporting `AMBIGUOUS_CALLEE` rather than guessing.
5. **Harnesses 1 and 2 — treat a stable OK verdict as reportable, not as a pass**, the way item `8m` now does for harness 3.
6. The LOW items as convenient.

⭐ **Priority logic:** items 2 and 3 protect populations that can be spent only once. Item 1 is a live wrong instruction on a measured detector.

---

## ⚠ Not verified

- **Whether `5346bc0` should have carried a `DeribitIndicatorProject.md` §15 row.** It did add one (the commit touched that doc); only the tag's claim is wrong.
- **How often finding 1 misleads the detector in practice.** Only the path-rule side is measured (1 of 354). The question-text side needs a keyed run, which this review deliberately did not make.
- **Harness 3's revisions 3 (`FP-D21`–`FP-D25`).** Authored by this seat; not self-reviewed.
- **Harness 1's spec and harness 2's spec** were not re-read in full; findings are against the code.
