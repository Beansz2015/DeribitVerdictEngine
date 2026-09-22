# Commit walker — build spec

**Created 2026-09-21 (UTC).** Build spec for `tools/checks/commit-walker.ps1`, harness 2 of the Jev programme ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6).

**Primary job: audit the `[no-engine-change]` commit tag.** It is self-declared, nothing verifies it, and it gates two obligations in `CLAUDE.md` — a [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 version-history entry, and a `settings.json` version bump when config keys move.

⛔ **This is NOT a gate.** Advisory, same as [`rider-travel-check-spec.md`](rider-travel-check-spec.md) `D-3`, and for the same three reasons.

---

## 0. ⭐ Model and effort — read before starting

**Model: Sonnet. Effort: medium.**

**Why that tier.** The architecture is settled by measurement (§2), the question shape is proven by two prior runs, and [`../tools/checks/rider-travel.ps1`](../tools/checks/rider-travel.ps1) is a complete in-repo template for every mechanical piece — the baseline refusal, the coverage block, the UTF-8 request body, the exit codes. **Copy its shape.** Nothing here needs a derivation.

**Where Sonnet will specifically slip — four traps.**

1. ⛔⛔ **Merge commits return an EMPTY file list.** `git show --name-only --no-merges <merge-sha>` prints nothing, so every merge looks like "touched no engine path" and lands in the judgment class spuriously. **Measured: 5 of the 6 sampled commits in that class were merges.** Filter merges out of the commit list with `git log --no-merges`, not just at the `show` step.
2. ⛔⛔ **Composing two Nouls with `AND`.** Standing trap across this whole programme, and it is MEASURED: an `AND` of two Nouls scored 0 of 8 where a single Choice scored 4 of 8. **The verdict comes from ONE Choice answer.** Nouls are diagnostics, printed beside it, never in a condition. `tools/checks/rider-travel.ps1:281` and `:357` are the pattern to copy.
3. ⛔ **Sending diff bodies as state.** A diff is large and mostly irrelevant to "did this change engine behaviour". `docs.typesafe.ai/model-jaggedness/jev-1.13.md` warns accuracy falls as state grows with unrelated detail. **Send the subject, the touched paths, and per-file added/deleted counts. Not the diff body.**
4. ⛔ **Judging all 300 commits.** Jev sees the RESIDUAL only (§2). Sending the 93% the path rule already settles burns tokens and invites disagreement on obvious cases, which then pollutes the measurement.

⚠ **The fixtures cannot be relied on to catch traps 2 or 4.** The implementer writes them, so a misunderstanding of the composition or the scope propagates into its own test. Both are **review items**, checked by reading the verdict expression and the call-site filter.

**Escalation trigger — stop and report, do not work around.**

- The residual class exceeds **25%** of non-merge commits in the window. The path rule in §2 is then wrong and this spec's whole architecture does not hold.
- `RESIDUAL=0` on a window of 300. That means the classifier is broken, not that the repo is clean — §2's measurement says the residual is real.

**Session split: none.**

---

## 1. Scope — what this harness does and does NOT do

| In scope | Out of scope, and why |
|---|---|
| `[no-engine-change]` tag audit, Jev-adjudicated on the residual | ⛔ **Semantic display-string parity.** See §5 — measured out, with a reason |
| Display-parity **file-pair** check, pure code, no Jev | Anything that modifies `AnalysisLogger.Header` or any tracked source. This tool READS history |

---

## 2. ⭐ The architecture, and the measurement behind it

**Measured 2026-09-21 (UTC) over the last 300 commits:**

| Class | Count | Who decides |
|---|---|---|
| Tagged `[no-engine-change]`, touched no engine path | 258 | **Code.** Path rule agrees with the tag |
| Untagged, touched an engine path | 20 | **Code.** Path rule agrees with the tag |
| ⭐ Tagged `[no-engine-change]` **but touched engine paths** | **10** | **Jev adjudicates** |
| ⭐ Untagged **but touched no engine path** | **12**, several of them merges | **Jev adjudicates** what survives the merge filter |

**278 of 300 are settled by a path rule. The judgment population is about 7%.**

⭐ **This is the whole design: code handles the 93%, Jev adjudicates the residual.** It is also why trap 4 matters — a build that sends everything to Jev throws the measurement away.

**Engine paths**, for the rule: a root-level `*.vb`, or anything under `Core/`, `analysis/`, `UI/`, or `settings.json`. ⚠ `tools/` is deliberately NOT an engine path — those are offline, host-agnostic utilities.

**Why the residual genuinely needs judgment.** Real cases from the measurement, each defensible either way:

- `88538d7` added `Core/VenueStatusLog.vb` and touched `DeribitClient.vb` — a new logging instrument with no scoring effect. Tag plausibly right.
- `57b55f9` split `CalcSpread` in `Core/Indicators_OrderFlow.vb` — a refactor **proved MD5-identical over eight book shapes**. Tag right, and provably so.
- `4ab0b25` made the live strip print `-- bps` instead of a fabricated `0.0 bps`. ⚠ **That moves a rendered value while claiming no engine change.** Genuinely arguable, and exactly the kind of case this harness exists to surface.

---

## 3. Decisions

Auto-proceeded under `CLAUDE.md`'s auto-proceed ruling and recorded. **None is RESERVED** — this tool reads git history and writes a report. No settings key, no scoring effect, no rendered value, no collector or store write, no schema change.

| ID | Decision | Options | Taken, and why |
|---|---|---|---|
| `CW-1` | What Jev sees | every commit · the residual only | **Residual only.** §2 measures the path rule at 93%; the rest is waste and it contaminates the score |
| `CW-2` | Merge commits | include · exclude | **Exclude.** They carry no file list, so they are unclassifiable by construction rather than genuinely ambiguous. Trap 1 |
| `CW-3` | State sent per commit | full diff · subject, paths and per-file line counts | **Subject, paths, counts.** Trap 3 |
| `CW-4` | Display-string parity | full semantic check now · file-pair code check only, semantic deferred | **File-pair only.** §5 carries the mechanism reason, not a cost reason |
| `CW-5` | Baseline-vs-detector disagreement | fails the run · reported, exit code reads the detector alone | **Reported.** Follows the trader ruling `D-6` in [`rider-travel-check-spec.md`](rider-travel-check-spec.md) — the tool reports to the seat |

---

## 4. What to build

`tools/checks/commit-walker.ps1`. PowerShell 5.1. ⭐ **Mirror [`../tools/checks/rider-travel.ps1`](../tools/checks/rider-travel.ps1) throughout** — same parameter style, same coverage block, same refusal, same exit codes.

### 4.1 Inputs

| Parameter | Meaning |
|---|---|
| `-Count` | How many commits back to walk. Default 300 |
| `-Skip` | ⭐ **ADDED 2026-09-21 after the build.** Commits to skip before walking, so the acceptance window and the measured first-run window can be made disjoint. See [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a. Default 0 |
| `-BaselinePath` | The seat's hand-written read. Default `commit-walker-baseline.json` |
| `-OutPath` | Default `commit-walker-report.md` |

### 4.2 Steps

1. `git log --no-merges -<Count> --format=...` — merges excluded at the LOG step (`CW-2`, trap 1).
2. Per commit: touched paths and per-file added/deleted counts. Classify against the §2 path rule.
3. Emit the coverage block (§4.3). **Print the two agreeing classes as counts only** — they are not judged.
4. **Refuse to proceed unless `-BaselinePath` exists.** Structural, before any API key read.
5. One Jev request per RESIDUAL commit (§6).
6. Report, then exit per §4.5.

### 4.3 ⛔ Coverage output, and a broken classifier is loud

```
COMMITS_WALKED=<n>
MERGES_EXCLUDED=<n>
AGREE_TAGGED_NO_ENGINE_PATH=<n>
AGREE_UNTAGGED_ENGINE_PATH=<n>
RESIDUAL_TAGGED_BUT_ENGINE_PATH=<n>
RESIDUAL_UNTAGGED_NO_ENGINE_PATH=<n>
RESIDUAL_TOTAL=<n>
RESIDUAL_PCT=<n>
COMMITS_JUDGED=<n>
```

⛔ **`RESIDUAL_TOTAL=0` exits 2 with `CLASSIFIER_SUSPECT`.** §2 measured a real residual; zero means the classifier broke, not that the repo is clean. ⛔ **`RESIDUAL_PCT > 25` also exits 2** — that is the §0 escalation trigger, and the tool must fire it rather than quietly judging 80 commits.

**Sanity anchors, measured 2026-09-21 (UTC) at `HEAD` over 300 commits:** roughly 258 / 20 / 10 / 12. ⚠ **These move as commits land. Treat as a smoke check, never an assertion.**

### 4.4 The baseline refusal

Identical mechanism and rationale to [`rider-travel-check-spec.md`](rider-travel-check-spec.md) §4.4.

⛔⛔ **A FIRST-RUN baseline is written to the TRACKED `docs/harness-runs/commit-walker-<UTC-stamp>-baseline.json`**, never the gitignored scratch path — see [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §2 step 3. Baseline vocabulary: `tag_correct`, `tag_wrong`, `unsure`.

⚠ **Note for the seat, not the implementer: this harness has NO pre-existing ground truth.** The residual's correct labels are not recorded anywhere. **The seat's hand baseline IS the labelling**, which makes writing it before the run more load-bearing here than on the rider check, not less.

### 4.5 Exit codes

| Code | Meaning |
|---|---|
| 0 | Every residual commit judged `tag_correct` |
| 1 | At least one judged `tag_wrong` or `ambiguous` |
| 2 | Baseline missing · classifier suspect · residual over 25% · API failure |

---

## 5. ⛔ Why the semantic display-parity check is NOT in this build

**Measured 2026-09-21 (UTC):** only **11 commits in the repo's whole history** touch `UI/MainForm_PlaintextSnapshot.vb`. Ten of them also touch `UI/MainForm_Render_Cards.vb`. One does not.

⭐⭐ **And the finding that decides it: of the three drift commits `CLAUDE.md` names, TWO touched BOTH files.**

| Commit | Snapshot | Card |
|---|---|---|
| `ccdd652` | yes | **yes** |
| `0bd1b63` | yes | no |
| `482c9bb` | yes | **yes** |

**So a file-pair rule would not have caught two of the three.** The drift happened *inside* commits that touched both — the card was edited, just not correspondingly. The real question is whether the card change CORRESPONDS to the snapshot change, which is genuinely semantic and genuinely Jev-shaped.

⛔ **It is deferred on a MECHANISM argument, not a cost one: with 3 known instances in 11 commits, a detector's measurement is uninterpretable.** Catch all three and the interval on the true rate still spans most of its range. There is nothing to fit a threshold against, and a threshold fitted to n=3 would be noise presented as a number.

**What ships instead:** the file-pair check in pure code — snapshot touched, card not touched — which is free, deterministic, and catches `0bd1b63`'s class. It is reported as a flag, never as a verdict.

**When to revisit:** when the snapshot file has accumulated enough new commits to make a measurement mean something, or if a mutation approach can manufacture positives the way harness 3 does for fixture names.

---

## 6. The Jev request

One request per residual commit. Same endpoint, auth and **UTF-8 body handling** as [`../tools/checks/rider-travel.ps1`](../tools/checks/rider-travel.ps1).

⛔⛔ **Reuse its `Invoke-Jev` verbatim — do not write a second HTTP call.** PowerShell 5.1's `Invoke-RestMethod` does not UTF-8-encode a plain string body; commit subjects in this repo carry em-dashes and section marks, so a string body corrupts on the wire and returns HTTP 400. **This was the median case, not an edge case.** If it needs to be shared, extract it to `tools/checks/lib/` the way `HeaderText.ps1` was — never copy it.

**State** — per commit:

```json
{
  "commit": { "subject": "...", "declared_no_engine_change": true },
  "touched_paths": ["..."],
  "per_file_line_counts": [{ "path": "...", "added": 0, "deleted": 0 }]
}
```

**Questions** — three. ⛔ **`verdict` is the answer; the Nouls are diagnostics and are never combined.**

| ID | Type | Asks |
|---|---|---|
| `changes_runtime_behaviour` | noul | Whether these changes alter what the running application computes, decides, logs or displays, as opposed to refactoring, comments, tests or documentation |
| `is_refactor_only` | noul | Whether these changes restructure code without altering its observable output |
| `verdict` | choice | `tag_correct` · `tag_wrong` · `ambiguous` |

Criteria must describe concrete situations, and must state the asymmetry explicitly: **a commit tagged `[no-engine-change]` that alters runtime behaviour is `tag_wrong`; a commit left untagged that only refactors is ALSO `tag_wrong`, in the opposite direction.** ⚠ Instructions are read literally — name the state paths in backticks and state the exact condition, not the intent behind it.

---

## 7. Acceptance

1. `git log --no-merges` is used at the log step; a merge SHA cannot reach the residual. Prove it by printing `MERGES_EXCLUDED` on a window known to contain merges.
2. The coverage block prints all nine counters, and `RESIDUAL_TOTAL` is non-zero at `HEAD` over 300.
3. Forced `RESIDUAL_TOTAL=0` exits 2 with `CLASSIFIER_SUSPECT`. Output pasted.
4. Forced `RESIDUAL_PCT > 25` exits 2. Output pasted.
5. Runs without a baseline: exits 2, **no API call made**. Output pasted.
6. The verdict expression reads from `verdict` alone. **Confirmable by reading one line.**
7. `Invoke-Jev` is shared with `rider-travel.ps1`, not copied. One definition in the tree.
8. A live dry run over 300 commits at `HEAD`, with a throwaway baseline: counters within sight of §4.3's anchors, and actual `usage.input_tokens`, cost and wall time reported.

⚠ **Not acceptance:** the measured comparison against a real hand baseline. That is the first live use, per [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md), and it is the seat's job, not the implementer's.

⛔⛔ **Item 8's window is RESERVED FOR ACCEPTANCE and is now spent.** The 2026-09-21 build ran it over commits 1 to 300 at `HEAD` and reported verdict aggregates back, so **the seat is contaminated on that window and it can never carry the measured first run.** The measurement moves to `-Skip 300 -Count 300`. Protocol rule and the reasoning: [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a.

---

## 7a. Post-build corrections, 2026-09-21 (UTC)

| Item | Correction |
|---|---|
| ⛔ **Pricing IS documented — the build was wrong to say otherwise** | The build reported *"no pricing page in the docs index"* and declined to compute a cost. ⭐ **The caution was exactly right — refusing to invent a rate is this repo's own rule about derived numbers.** But the source exists: `docs.typesafe.ai/models.md`, listed in `docs.typesafe.ai/llms.txt`, states **$42 per Btok / $0.042 per Mtok, charged on INPUT tokens, output tokens free.** So the build's 15,255 input tokens cost **$0.00064**. Any future spec asking for a cost must cite this line so the implementer has the rate |
| `EXIT_REASON` for the over-25% case | The build invented `RESIDUAL_TOO_HIGH`. **Adopted** — it matches `CLASSIFIER_SUSPECT`'s style |
| Baseline key format | The build used the full 40-char SHA and printed 10-char short hashes in the candidate list. ⚠ **A seat writing a baseline by hand copies what it sees printed, so the two must not disagree.** **Ruled: accept a full SHA or any unambiguous prefix of at least 7 characters; error on an ambiguous prefix rather than guessing** |
| §4.3 anchors 258/20/10/12 | ⭐ **Superseded, and the reason is informative.** They were measured before the merge-exclusion trap was understood, so the `RESIDUAL_UNTAGGED_NO_ENGINE_PATH` anchor of 12 was inflated by merges a correct build excludes. **Live, correct values: 262 / 21 / 10 / 7, residual 17, `RESIDUAL_PCT` 5.7.** The old anchor documented the buggy-before state |
| ⚠ PowerShell 5.1 array concatenation | Found by the build, worth carrying: `@($listA) + @($listB)` on two non-empty `List[object]` throws *"Argument types do not match"* in Windows PowerShell 5.1. Fixed with `.ToArray()`. **It would have crashed the first live run past the point acceptance item 2 alone catches** |

---

## 9. ⭐⭐ REVISION 1, 2026-09-21 (UTC) — after the first run

**Supersedes §6's question set. §2's path rule, §4's mechanics and §0's traps all STAND.** Run record: [`harness-runs/commit-walker-run-2026-09-21.md`](harness-runs/commit-walker-run-2026-09-21.md).

### 9.1 ⛔ A correction I owe, first

**Immediately after the first run I claimed the path rule's `tools/` exclusion was "manufacturing false residual, 7 times out of 7" and needed re-deriving. THAT WAS WRONG**, and it was an overreach from a one-sided sample — 7 rows in one direction, with the other direction unchecked.

Measured properly against all **339 post-adoption non-merge commits**:

| Path rule | Agreement with the tag | Residual |
|---|---|---|
| ⭐ **§2's rule as written** | **94%** | 19 |
| + `tools/` | 85% | 51 |
| + `tools/` + `verify/` | 83% | 56 |
| any non-docs file | 74% | 87 |

**Adding `tools/` creates 50 false positives to remove 6.** §2's rule is the best fit of every candidate tested. **It stands unchanged.** The 7 untagged `tools/`-only commits are genuine residual, and the detector judged all 7 correctly.

### 9.2 The tag is INCONSISTENT AT SOURCE, and that changes the harness's purpose

Two commits, same shape — a new log-writing instrument under `Core/` plus the client file that feeds it:

| Commit | Files | Tag |
|---|---|---|
| `88538d7023` venue-status instrument | `Core/VenueStatusLog.vb`, `DeribitClient.vb` | ⭐ **tagged** `[no-engine-change]` |
| `92191a377d` `ws_feed.log` | `Core/WsFeedLog.vb`, `DeribitWsFeed.vb` | ⛔ **untagged** |

⛔ **There is no consistent convention to audit against.** "Is the tag correct?" presumes one exists. It does not — it lives in authors' heads and it varies at the margin.

⭐⭐ **So the harness's job changes: apply a STATED criterion, and report where it disagrees with the tag.** The disagreements are a list for the seat to adjudicate, and over time they let the convention be **written down** instead of re-decided per commit. That is a better deliverable than an agreement percentage.

### 9.3 The revised question set — replaces §6

⛔ **The old `changes_runtime_behaviour` Noul asked whether a commit alters what the app "computes, decides, logs or displays". That wording is mine and it is too broad** — it sweeps every instrument, display string and tool fix into one bucket, which is exactly what produced 7 `tag_wrong` verdicts on a 10-commit residual. **The model answered my question correctly. My question was wrong.**

**The stated criterion:** does this commit change the behaviour of the **shipped Windows application** — the code inside the `.exe` — when it runs? `tools/` and `verify/` are separate projects and are NOT the shipped app.

**Separate the dimensions rather than merging them.** These map onto `CLAUDE.md`'s own RESERVED classes, which is what makes the margin decidable:

| ID | Type | Asks |
|---|---|---|
| `changes_computation` | noul | Whether it alters what the app computes or decides — scores, verdicts, gates, placed levels |
| `changes_display` | noul | Whether it alters any text or value the app renders on screen |
| `changes_writes` | noul | Whether it alters what the app writes to disk — CSV columns or values, log lines, the trade store |
| `verdict` | choice | `no_app_change` · `changes_computation` · `changes_display` · `changes_writes` · `ambiguous` |

⛔ **`verdict` remains the ONLY answer read by code. The three Nouls are diagnostics and are never combined — §0 trap 2 is unchanged and still the trap that matters.**

**Code, not the model, compares the verdict to the tag:** tagged `[no-engine-change]` with a verdict other than `no_app_change` is a DISAGREEMENT, reported for the seat. ⭐ **A multi-class verdict is far more useful than `tag_correct`/`tag_wrong` — it says WHICH kind of change the tag missed.**

### 9.4 Also build

| # | Item | Why |
|---|---|---|
| **1** | ⛔ **Era guard.** Refuse, or classify separately, any commit before the tag's adoption. ⛔⛔ **THIS ROW SAID "2026-08-13" AND THAT WAS WRONG — corrected 2026-09-21.** `c6c6942d8a` is stamped `2026-08-13T01:21:40+08:00`, which is **`2026-08-12T17:21:40Z`**. I read the `+0800` stamp without converting, in a session that ran `date -u` at the start specifically to avoid this. ⭐ **The build did NOT hardcode the corrected date either, and was right not to: it DERIVES the cutoff at runtime from the earliest tagged commit and prints `ERA_CUTOFF_COMMIT` / `ERA_CUTOFF_UTC` every run.** A derived cutoff survives a rebase, an earlier tagged commit appearing, and this exact class of transcription error. Code that describes itself beats a constant in a doc | Measured: `c6c6942d8a` is the first tagged commit. **Zero of the 788 non-merge commits from March to July carry the tag.** A window straddling it returned `RESIDUAL_PCT=73` — the classifier was right and the population was wrong |
| **2** | **Send the commit BODY as well as the subject** | The one genuinely ambiguous case returned confidence **0.14**, and its body said *"No behaviour changes … mutation-proved"* in plain English. We sent only the subject |
| **3** | **Low-confidence escalation.** Under ~0.3, a second request carrying the diff of the shipped-app files only | Three parity-proved refactors all landed at or below 0.29. From paths and line counts a proven-identical refactor is indistinguishable from a rewrite. Pattern: `docs.typesafe.ai/cookbooks/sde_cascade.md`. ⚠ **0.3 is a guess from one observation. Print it as a tunable constant at the top of the file; do not bury it** |
| **4** | **Determinism probe.** A `-Repeat N` flag that asks the same commit N times and reports whether verdict or confidence moved | ⚠ The build reported *"6 of 10 `tag_wrong`"*; a later run of the same window gave **7**. Either the build miscounted or the model varies run to run. **Unresolved, and it must be pinned before any measurement** |

### 9.5 Acceptance for this revision

1. The four §9.3 questions are sent; the old `changes_runtime_behaviour` / `is_refactor_only` pair is gone.
2. The verdict expression still reads `verdict` alone. One line, confirmable by reading.
3. Era guard: a window reaching before 2026-08-13 is refused or separately classified. Output pasted.
4. Commit bodies are sent. Show one request's state, key names only, **no key material**.
5. The escalation threshold is a named constant near the top of the file.
6. `-Repeat 3` on the three commits in [`harness-runs/commit-walker-20260921T204555Z-baseline.json`](harness-runs/commit-walker-20260921T204555Z-baseline.json). **Report whether verdicts and confidences were stable. Do not interpret the result — report it.**
7. A dry run over `-Skip 0 -Count 300` with a throwaway baseline. ⛔ **Report counters, tokens and timings ONLY. Do NOT report per-commit verdicts** — that window is already spent, but the habit is what [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a exists to enforce.

---

## 10. ⭐⭐ REVISION 2, 2026-09-21 (UTC) — self-consistency

**Supersedes revision 1's escalation design. Everything else in §9 stands.** Cause: [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b — the detector flipped a verdict on identical input.

**Source pattern:** `docs.typesafe.ai/cookbooks/consistency_choice_cookbook.md`, read in full before writing this.

### 10.1 ⛔ Three details from the cookbook that are easy to get wrong

| # | Detail | Why it matters |
|---|---|---|
| **1** | **Each repeat carries a fresh `uid`** — a throwaway unique value in the state, so every sample is "a distinct, independent draw" | ⚠ **The cookbook is honest that this is a confound:** it *"cannot separate sensitivity to the irrelevant field from variation that would occur on identical requests."* ⭐ **Our own `-Repeat 3` used NO `uid` and still flipped a verdict — so our evidence is CLEANER than the cookbook's.** Include the `uid` to match the documented pattern, and record both facts |
| **2** | ⛔⛔ **The uncertainty band is on `max(probabilities)` — the TOP PROBABILITY — NOT on `confidence`** | The cookbook uses `MIN_CHOICE_PROBABILITY = 0.60`. **These are different statistics on different scales.** `confidence` is derived from the distribution's shape (`docs.typesafe.ai/confidence.md`); for a five-option Choice a `confidence` of 0.3 is roughly a top probability of 0.44. **Revision 1's `0.3` threshold is on `confidence` and MUST NOT be reused as a probability band** |
| **3** | **Below the band the label becomes `uncertain` and goes to human review** — not to an escalated call | The cookbook's own numbers: plurality label repeats **90.8%** of the time; applying the 0.60 band lifts agreement to **99.2%** while auto-labelling **74.2%** of answers |

### 10.2 ⭐ Self-consistency and escalation are COMPLEMENTARY, not alternatives

I said last turn that self-consistency partly supersedes escalation. **That was half right, and the correction is the useful part.** They address two different failure modes, and **self-consistency is what tells you WHICH one you have:**

| Observation | Diagnosis | Response |
|---|---|---|
| N samples **agree**, top probability **high** | Confident and stable | Report the verdict |
| N samples **agree**, top probability **low** | ⭐ **Stable but under-informed — the state is too thin** | **ESCALATE.** More state genuinely helps. This is `504442e29e`: confidence 0.14, and its commit body said *"No behaviour changes"* in plain English |
| N samples **disagree** | ⛔ **Unstable — genuinely on the fence** | **Flag for the seat. Do NOT escalate.** More state will not fix a coin flip. This is `c6c6942d8a` |

⛔ **So keep escalation, and re-gate it: it fires on AGREEMENT-WITH-LOW-PROBABILITY, never on a single call's confidence.**

### 10.3 What to build

1. **Every residual commit is sampled `$SELF_CONSISTENCY_SAMPLES` times.** Default **5** — the cookbook uses 15; 5 is this repo's starting point and is a named constant beside the others. ⚠ **A guess, to be re-tuned.**
2. **Fresh `uid` per sample**, in the state. Never in the questions.
3. **Compute and ALWAYS report, per commit:** plurality verdict · **agreement rate** (samples on the plurality ÷ N) · mean and min top probability. **The agreement rate appears on every row, not only unstable ones.**
4. **Band:** `$MIN_TOP_PROBABILITY = 0.60`, on `max(probabilities)`, per §10.1 detail 2.
5. **Apply the §10.2 matrix.** Escalation re-gated to the agree-but-low-probability cell; delete the confidence-only trigger.
6. `-Repeat` is **retired** as a separate flag — self-consistency is now the default path and `-Repeat` was its prototype. Keep `-Samples N` as the override.
7. ⛔ **Any row whose agreement rate is below 1.0 is reported as `UNSTABLE` and is NEVER auto-resolved**, whatever the plurality says.

### 10.4 Cost — no reason to be stingy

17 residual commits × 5 samples ≈ 85 calls ≈ **$0.006** at the `docs.typesafe.ai/models.md` rate. **Sampling is not the expensive thing here; a wrong verdict is.**

### 10.5 The two open items I now rule

| Item | Ruling |
|---|---|
| **Baseline vocabulary** — [`rider-travel-check-spec.md`](rider-travel-check-spec.md) §4.4's `tag_correct`/`tag_wrong`/`unsure` no longer matches the five-way verdict | **Baselines for this harness use the five verdict values plus `unsure`.** The seat writes what it thinks the commit DID, exactly as the model is asked to. `unsure` still excluded from scoring |
| **`MAX_ESCALATION_DIFF_CHARS = 20000`**, the build's own defensive cap | **Kept.** Sensible, and it was right to add it unspecced. ⚠ Still untested against an oversized commit; leave it named |

### 10.6 Acceptance

1. `-Samples 5` is the default path; every residual row prints an agreement rate.
2. A fresh `uid` is present per sample and differs across samples. Show two sample states, key names and the differing `uid` only.
3. The band reads `max(probabilities)`, not `confidence`. **Confirmable by reading one line.**
4. The §10.2 matrix is implemented; escalation cannot fire on a disagreeing row. Confirmable by reading.
5. `-Samples 5` on the three commits in [`harness-runs/commit-walker-20260921T204555Z-baseline.json`](harness-runs/commit-walker-20260921T204555Z-baseline.json). **Report each one's plurality, agreement rate and top-probability range.** Those three are adjudicated already, so verdicts are reportable.
6. A dry run over `-Skip 0 -Count 300`. ⛔ **Counters, tokens and timings ONLY. No per-commit verdicts, no aggregates** — [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a.
7. The verdict still reads the `verdict` Choice alone. §0 trap 2 is unchanged.

---

### 10.7 ⛔⛔ CORRECTION, same day — an unsampled escalation call must not override a sampled plurality

**A defect in §10.2/§10.3, found empirically by the revision-2 build. The build was faithful to the spec; the spec was wrong.**

**What happened on `91942d6739`:**

| Stage | Result |
|---|---|
| 5 primary samples | **5 of 5 agreed** on `changes_computation` |
| My hand baseline | `tag_wrong` — **agrees** |
| Revision 1's single call | `changes_computation` — **agrees** |
| Mean top probability | 0.414, under the 0.60 band → **escalated** |
| ⛔ **The single, unsampled escalation call** | **`no_app_change`** — and it **became the final verdict** |

⛔ **One unsampled call overrode a unanimous five-sample plurality, and moved the answer AWAY from the hand baseline.** Verified in the tree: the primary runs in a loop, the escalation is a single call at `tools/checks/commit-walker.ps1:685`, and `$finalVerdict = $escResult.Verdict` replaces the plurality outright.

⭐ **The escalation call inherits exactly the single-call instability this whole revision exists to remove.** A system built on sampling that exempts its own tie-breaker is incoherent.

**The fix — both halves, not either:**

1. ⭐ **Sample the escalation too.** `$ESCALATION_SAMPLES = 3`, its own named constant. Consistency applies everywhere or it is not a principle. The escalation call measures ~8,090 tokens against a primary's ~1,605 (§10.8), so 3 samples of it is roughly $0.001 per escalated commit — **not a reason to skip it.**
2. ⛔ **Escalation NEVER silently overrides.** When the escalated plurality disagrees with the primary plurality, that is a **`CONFLICT`** — report both verdicts, both agreement rates, both probability ranges, and **flag for the seat. The tool does not pick.**

**Why not just take the escalated answer as better-informed?** Because on the one case we have, it was **worse** — it disagreed with the baseline the primary matched. ⚠ **`n=1`, so that is not proof the escalated read is generally worse.** It is proof the tool cannot assume it is better, which is all the ruling needs.

**Status vocabulary becomes:** `CONFIDENT` · `CONFIDENT_AFTER_ESCALATION` (escalated **and agreed** with the primary) · **`CONFLICT`** (escalated and disagreed) · `UNSTABLE` (primary samples disagreed). `CONFLICT` and `UNSTABLE` both exit non-zero.

### 10.8 ⭐ The token ratio is arithmetic, not caching — resolved

The build reported that 5× sampling raised calls 4× but tokens only **2.49×**, and honestly said it could not explain why, suspecting prompt caching. **It is not caching.** Solving the two measured runs as simultaneous equations:

- `3P + E = 12,904` (revision 1) and `15P + E = 32,159` (revision 2)
- → **primary ≈ 1,605 tokens, escalation ≈ 8,090 tokens.** Both equations then check to the token.

**One fixed ~8k diff call is 1-of-4 in revision 1 and 1-of-16 in revision 2, so the per-call average falls by amortisation.** ⭐ **Recorded because an unexplained ratio becomes a believed mechanism: "Jev has prompt caching" would have entered these docs as a fact on the strength of one unexplained number.**

### 10.9 Acceptance for the correction

1. `$ESCALATION_SAMPLES = 3`, named, beside the others.
2. The escalation runs in a loop, and its plurality and agreement rate are computed the same way as the primary's.
3. `CONFLICT` exists, exits non-zero, and reports BOTH verdicts with both agreement rates.
4. **Re-run `-Samples 5` on `91942d6739`.** Report what status it now lands on and both pluralities. That commit is adjudicated, so verdicts are reportable.
5. Escalation still cannot fire on an `UNSTABLE` row — §10.2 unchanged.

---

### 10.10 ⚠ NOTED, NOT BUILT — status fires on the five-way label, but the audit only asks a two-way question

**Found by the seat re-running the correction on 2026-09-21 (UTC). The correction itself is sound; this is a refinement, and it errs in the SAFE direction.**

The three adjudicated commits, against the hand baseline:

| Commit | Baseline | Primary plurality | Escalated | Status | ⭐ Agrees with the baseline **at the tag level**? |
|---|---|---|---|---|---|
| `504442e29e` | `tag_correct` | `no_app_change` (5/5) | — | `CONFIDENT` | ✅ **Yes** |
| `91942d6739` | `tag_wrong` | `changes_computation` (5/5) | `changes_writes` (0.667) | `CONFLICT` | ✅ **Yes — BOTH sides mean the tag is wrong** |
| `c6c6942d8a` | `tag_wrong` | `changes_writes` (0.6) | — | `UNSTABLE` | ✅ Yes on the plurality, but 2 of 5 samples said `no_app_change` |

⭐⭐ **The detector agrees with the hand baseline on all three at the tag level, which is the only question this harness audits.**

⛔ **But `91942d6739` was flagged `CONFLICT` on a disagreement that does not affect the audit.** `changes_computation` and `changes_writes` are different categories that carry the **same tag verdict**: the `[no-engine-change]` tag is wrong either way. The status is computed on the five-way label, so category wobble is reported as audit uncertainty.

**The distinction, and it is real:**

- `91942d6739` — **immaterial.** Every sample and the escalation agree the tag is wrong; they differ only on which kind of change it is.
- `c6c6942d8a` — **material.** Two of five samples say `no_app_change` and three say `changes_writes`. Those are opposite answers to the audit's question.

**The fix, when this is next opened:** compute `UNSTABLE` and `CONFLICT` on the **tag-level collapse** — `no_app_change` versus anything-else — and report five-way category disagreement as a separate, non-blocking note. The five-way verdict stays; it is more informative and §9.3 keeps it. Only the *status* moves to the collapsed axis.

⚠ **Not built now, deliberately.** The current behaviour **over-flags**, which is the safe direction for an advisory tool, and harness 2 cannot get its real measurement until a post-adoption window the seat has not seen accrues — weeks away. **Fix it with that measurement, not before.**

⛔ **One consequence to know meanwhile: the `[DISAGREE]` on all three rows is a VOCABULARY ARTEFACT, not a disagreement.** The baseline file was written in the old `tag_correct`/`tag_wrong` words; §10.5 moved baselines to the five-way set and that file predates the ruling. **Do not read those three `[DISAGREE]` markers as the detector missing.**

---

## 8. What this spec does NOT verify

- **That the path rule's engine/non-engine split is correct.** It is a convention chosen here, measured to agree with the tag 93% of the time. Agreement is not proof it is right.
- **That a `tag_correct` commit actually met its §15 and settings-bump obligations.** This checks the tag, not what the tag triggers.
- **`tools/` changes.** Treated as non-engine by the path rule; that is a convention, and a `tools/` change can still be a real behaviour change in an offline analysis path. ⛔ **Superseded in part by §11: the 8 `tools/` files the app compiles ARE engine paths now.**

---

## 11. ⭐ REVISION 3, 2026-09-22 (UTC) — after the adversarial review

**Source:** [`jev-harnesses-adversarial-review-2026-09-22.md`](jev-harnesses-adversarial-review-2026-09-22.md) findings 1, 3 and 10; trader "go". Decision records `CW-R3-1` to `CW-R3-3` in the script header.

| Change | Why | Verified (key stripped unless stated) |
|---|---|---|
| **`CW-R3-1`** The engine-path rule adds, per commit, the `tools/` files THAT commit's own `DeribitVerdictEngine.vbproj` compiles, plus the vbproj itself. The state carries a code-computed `touched_app_paths`; every question and criterion now says *only files in `touched_app_paths` are app code* | The app compiles 8 `tools/` files, and the question text told Jev *"`tools/` … changes are NOT app changes, however large"*. `5346bc0` (tagged, changed a value the app logs) had filed itself as agreeing and was never judged | Over the full post-era span (384 classified commits), **exactly two move**: `5346bc0` enters the residual, `a6c205d` (untagged, touches `HistoricalStore.vb`) leaves it. Residual stays 21 (5.5 %) |
| **`CW-R3-2`** Full hashes in `RESIDUAL_CANDIDATES`; the example uses the five-way vocabulary; an item-level refusal (`BASELINE_INCOMPLETE`) with `-AllowUnbaselinedItems` for routine re-runs | A baseline written from the old 10-character list matched nothing, and a file-level gate then judged every commit unbaselined — the unspent window spent | Empty baseline → `BASELINE_INCOMPLETE`, full hash listed, no call made |
| **`CW-R3-3`** `-Since <sha>` walks `<sha>..HEAD`, exclusive, capped by `-Count` | The unspent window was a hand-computed `-Skip`/`-Count` | A bad sha → `BAD_SINCE`; `-Since 6f95e42` → exactly the 5 non-merge commits after it |

**Keyed smoke test** (one seat-authored commit, `526ecf2`, conflicted for the seat anyway): the state carries `touched_app_paths`; 5 samples, `no_app_change`, STABLE, mean top probability 0.988, agrees with the seat's read written first. 7,542 input tokens.

⚠ **The measured first run is still unspent.** Name its window with `-Since` at the last commit the seat has seen, and write the baseline from the full hashes printed.
