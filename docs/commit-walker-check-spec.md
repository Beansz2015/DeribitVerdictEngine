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

---

## 8. What this spec does NOT verify

- **That the path rule's engine/non-engine split is correct.** It is a convention chosen here, measured to agree with the tag 93% of the time. Agreement is not proof it is right.
- **That a `tag_correct` commit actually met its §15 and settings-bump obligations.** This checks the tag, not what the tag triggers.
- **`tools/` changes.** Treated as non-engine by the path rule; that is a convention, and a `tools/` change can still be a real behaviour change in an offline analysis path.
