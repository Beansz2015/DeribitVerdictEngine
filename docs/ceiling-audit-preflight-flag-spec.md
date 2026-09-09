# `D-1` — a committed `--preflight` flag for `CeilingAudit`

> ## ✅✅ BUILT AND SHIPPED — commit `1e624ec`, 2026-09-09 (UTC). **THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION. DO NOT HAND IT TO AN IMPLEMENTER.**
>
> ✅ **Verified by the reviewing seat, not carried:** `--preflight` on the frozen pooled book prints `PREFLIGHT_ELIGIBLE_ROWS=8269`, exits **0**, emits **no** OHLC-fetch line, and **every** exclusion field matches the earlier throwaway harness digit for digit. Both argument orders produce byte-identical output. Harness **345 → 346** (`A70a`); `GATE PASSED`; `settings.json` untouched.
>
> ⭐ **It did what it was for: the previous session's `E-1` — the harness that authorised the `W6-4` spend and that no reader could re-run — is now reproducible from tracked code.**
>
> ⚠ **§3's gap is still open by ruling:** pre-flight `P-5`'s duplicate-`(InstanceId, SignalId)` half remains **uncovered**, and that scan stays manual. Any spec that needs it must keep saying so.
>
> ⚠ **One defect found in review and fixed in the same commit:** the header's `Exit codes` contract still read *"0 report written"*; with `--preflight` a 0 now also means *"stats printed, NO report written"*.

✅ **RULED (a) 2026-09-09 (UTC), trader.** Decision record: [`kelly-w6-4-spec-back.md`](kelly-w6-4-spec-back.md) §2.

**Baseline commit: `d5ce4a5`.** Line numbers read at that commit.

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **LOW**
> ### One short session.

**Why LOW.** One new switch in an existing argument parser, one early `Return 0`, and one `Console.WriteLine` block. **No new class, no new file, no schema, no settings key, no engine path.** `CeilingAudit` is analysis-only and already host-agnostic, so none of the engine-behaviour weight applies — no version bump, no `docs/DeribitIndicatorProject.md` §15 entry.

⚠ **Why it is not trivially LOW.** The flag exists to be trusted **before** money is spent on an OHLC fetch. **If it prints numbers that differ from what the full run would report, it is worse than not having it.** The one hard requirement is that the pre-flight path and the full path read the same `LoadStats` from the same call — see §2.

### 0.1 Where this will slip

⛔ **Trap 1 — do NOT compute the stats twice.** The temptation is a separate lightweight load for the pre-flight path. **`LoadAndBuild` must be called exactly once**, and the flag must short-circuit *after* it and *before* the OHLC fetch. Two call sites is two things that can drift, which is the defect class this repo keeps hitting.

⚠ **Trap 2 — the existing early-exit is not the right hook.** `tools/CeilingAudit/CeilingAuditProgram.vb:121-124` already returns `1` when `allRows.Count = 0`. **That is an error path.** The pre-flight exit is a **success** path and must `Return 0` — a pre-flight that reports a healthy 8,269 and exits non-zero will read as a failure in any script that checks the exit code.

⚠ **Trap 3 — argument parsing.** The parser at `:52-59` is a `Select Case` over `args(i).ToLowerInvariant()` where every existing case consumes a following value. `--preflight` takes **no** value; adding it to that block without an `i += 1` is correct, but it is easy to copy the neighbouring pattern and swallow the next argument. **Add a fixture that passes `--preflight` before `--out` and asserts `--out` is still honoured.**

⚠ **Trap 4 — the usage string at `:39` must gain the flag**, or the next seat writes another throwaway harness because the CLI does not advertise it. That is the whole failure this build exists to end.

### 0.2 Escalation trigger

⛔ **If closing `P-5` in full requires adding a field to `LoadStats`, STOP and report.** See §3 — that is deliberately **out of scope** and is its own decision.

---

## 1. Why this exists

**The pre-flight gate `P-4` is a standing requirement, and today no committed command satisfies it.**

[`kelly-w6-4-pooled-read-spec.md`](kelly-w6-4-pooled-read-spec.md) §5 requires the eligible-row count to be measured **before** authorising the `W6-4` spend. The 2026-09-09 session had to build a throwaway console harness to do it, because the shipped CLI only exposes `CsvFeatureBuilder.LoadAndBuild` bundled with a live OHLC fetch and the full statistical fit.

⭐ **That harness became an `E-n`, not an `H-n`** — [`kelly-w6-4-spec-back.md`](kelly-w6-4-spec-back.md) §1 labels it `E-1` and states plainly that **no reader can re-execute the instrument that authorised the spend.** `CLAUDE.md` makes that a standing rule: *"a handle whose INSTRUMENT does not survive the build is EVIDENCE, not a handle."*

⚠ **This recurs by design.** The ceiling-audit method's own instruction is *"re-run at the next book doubling"*, so the next pooled read hits the identical need.

---

## 2. The build

**One file: `tools/CeilingAudit/CeilingAuditProgram.vb`.**

| # | Change | Detail |
|---|---|---|
| **1** | Parse the switch | Add `Case "--preflight" : preflight = True` to the `Select Case` at `:52-59`. ⛔ **No `i += 1`** — it takes no value (Trap 3) |
| **2** | Usage string | Add `[--preflight]` to `:39`'s usage line **and** to the header comment at `:6` (Trap 4) |
| **3** | The early exit | Immediately after the existing `Console.WriteLine("[CeilingAudit] Loaded …")` at `:127-128`, and **before** the OHLC-fetch block at `:130`, emit the full `LoadStats` and `Return 0` when `preflight` is set |

**The block to emit.** Every field on `LoadStats` (`tools/CeilingAudit/CsvFeatureBuilder.vb:77-86`), machine-greppable, one per line:

```
PREFLIGHT_TOTAL_ROWS=<stats.TotalRows>
PREFLIGHT_ELIGIBLE_ROWS=<allRows.Count>
PREFLIGHT_REPEATED_HEADERS_SKIPPED=<stats.RepeatedHeadersSkipped>
PREFLIGHT_NON_V08_EXCLUDED=<stats.NonV08Excluded>
PREFLIGHT_WEEKEND_EXCLUDED=<stats.WeekendExcluded>
PREFLIGHT_NON_DIRECTIONAL_EXCLUDED=<stats.NonDirectionalExcluded>
PREFLIGHT_BURST_INSTANCE_PREFIX_EXCLUDED=<stats.BurstInstancePrefixExcluded>
PREFLIGHT_BURST_CADENCE_INSTANCES_EXCLUDED=<stats.BurstCadenceInstancesExcluded>
PREFLIGHT_BURST_CADENCE_ROWS_EXCLUDED=<stats.BurstCadenceRowsExcluded>
PREFLIGHT_POPULATIONS=<Name=Count, comma-joined, same shape as the existing line>
```

⚠ **Print populations too.** `PartitionIntoPopulations` runs at `:126`, before the fetch, and the pooled read wants `NYx1 / LONDONx3 / ASIAx3` counts to know whether the *decisive* population grew — not just the total.

⛔ **`LoadAndBuild` is called ONCE, at `:118`, and stays there.** The flag only changes whether execution continues past `:128` (Trap 1).

---

## 3. ⚠ What this does NOT close — stated, not hidden

**The ruling asked for full `LoadStats` so one command satisfies pre-flight `P-4` **and** `P-5`. It satisfies `P-4` in full and `P-5` only in half.**

| Pre-flight assertion | Closed by this flag? |
|---|---|
| `P-4` eligible rows ≥ threshold | ✅ **Yes**, `PREFLIGHT_ELIGIBLE_ROWS` |
| `P-5` first half — embedded header lines | ✅ **Yes**, `PREFLIGHT_REPEATED_HEADERS_SKIPPED` |
| `P-5` second half — duplicate `(InstanceId, SignalId)` pairs | ⛔ **NO. `LoadStats` has no such counter** |

⛔ **Adding a `DuplicateIdentityPairs` counter to `LoadStats` is OUT OF SCOPE for this build** — it touches `tools/CeilingAudit/CsvFeatureBuilder.vb`, which the ruling's narrowest version explicitly excluded, and it is a real design question (what counts as a duplicate when a row carries no identity at all — the same question `S-4`'s `D-3` (a) answered for the eval cache).

⭐ **Recommendation, not built: leave the duplicate-pair check as a separate scan until a pooled read actually needs it inside the tool.** Record it as a queue row. **The next pooled read still runs that scan by hand; the spec that calls for it must keep saying so.**

---

## 4. Fixtures — family `A70` (⚠ verify free; `A69` was taken 2026-09-08)

⚠ **`CeilingAuditProgram` is a console `Main`, and `verify/ordercheck/OrderCheck.vbproj` does NOT link it** — only `CsvFeatureBuilder.vb`, `L2Logistic.vb`, `FeatureMatrix.vb`, `AuditMetrics.vb`. **So the flag's argument parsing is not directly harness-reachable.** State that plainly rather than inventing coverage.

| Fixture | Asserts |
|---|---|
| **`A70a`** | `CsvFeatureBuilder.LoadAndBuild` on a small synthetic CSV returns a `LoadStats` whose exclusion counts satisfy the identity `TotalRows − burstRows − nonV08 − weekend − nonDirectional = eligible`. ⭐ **This is the property the printed block is only a view of** — pin the arithmetic, not the formatting |

⛔ **Argument-parser coverage is a MANUAL handle, not a fixture** (§5 `H-2`). Do not fake it with a harness that re-implements the parser — that is a second copy of the thing under test.

---

## 5. Acceptance criteria

| # | Criterion |
|---|---|
| **AC-1** | Solution + `CeilingAudit` + `OrderCheck` build `Build succeeded`, **0 errors 0 warnings**, on a full `-t:Rebuild`. ⚠ An incremental build hides new warnings |
| **AC-2** | Harness `ALL PASS`, count **345 → 346** (`A70a`). ⛔ **Measure by RUNNING it, never by grepping `Check(`** |
| **AC-3** | `H-1` — `--preflight` on the frozen pooled book prints `PREFLIGHT_ELIGIBLE_ROWS=8269`, **matching the value the full 2026-09-09 run reported**, and **exits 0 without any network call** |
| **AC-4** | `H-2` — `--preflight --out <dir>` still honours `--out` (Trap 3), and `--out <dir> --preflight` behaves identically |
| **AC-5** | The usage string and the `:6` header comment both advertise `--preflight` |
| **AC-6** | `settings.json` untouched — `git diff --stat` must not list it |
| **AC-7** | Mojibake grep and an **unescaped-pipe** count (`grep -o '[^\\]|'`) on any doc row touched, compared against a neighbouring row |

⭐ **`AC-3` is the load-bearing one.** A pre-flight number that disagrees with the full run's number is worse than no flag at all. **The frozen book at `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` (MD5 `E8418846838FF97F3C90F782A95B3523`) is the reference — it is gitignored but persists on disk.**

---

## 6. What to report back

Two documents per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). ⛔ **Label handles `H-n` / `E-n` and never rank an `E-n` first** — this build exists precisely because a previous packet's authorising instrument was an `E-n`.

**State plainly** whether `AC-3`'s 8,269 matched, and that `P-5`'s duplicate-pair half remains uncovered per §3.

---

## 7. What this must NOT do

- ⛔ Call `LoadAndBuild` more than once
- ⛔ `Return 1` on the pre-flight success path
- ⛔ Add a field to `LoadStats` or touch `CsvFeatureBuilder.vb` (§3 — escalate instead)
- ⛔ Change any existing output line's text — the full-run path must be byte-identical when `--preflight` is absent
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — commit is the orchestrator's
