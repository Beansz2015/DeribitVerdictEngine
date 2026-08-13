# Spec-back — SH-1 (split the coverage hour at a capture-state marker)

> ## ✅ REVIEWED AND ACCEPTED — 2026-08-13. **All four owed items done. CLEAR TO COMMIT.**
>
> **Re-verified after the re-work, not accepted:** `verify-gate.ps1 -Mode prepush` **GATE PASSED** — six projects Release **0/0**, harness **ALL PASS** including `A49w`, display-parity OK, *"no engine-path change"*. `settings.json` diff **empty**. `AccumulateHourStats`'s body still untouched (one doc cross-reference only), so route (b) held through the re-work.
>
> ✅ **The residual combine now reads `Defect` > `Captured` > `UnknownScope` > `ExpectedMissing` > `NotCapturing`**, terminating on `Else ⇒ NotCapturing` — read in the tree, in order. **D-3 as ruled.**
>
> ✅ **`A49w` added and its name states its own property** — *"a first-ever marker landing mid-hour splits `UnknownScope` (before it) from `NotCapturing` (its own off record); the residual combine reads `UnknownScope`, never laundered into `NotCapturing`."* Confirmed by the implementer to fail on the pre-ruling order.
>
> ✅ **The route-(b) deviation is re-filed under "which assumptions broke"** — and the write-up is better than the instruction asked for: it names the bounded fix concretely rather than leaving it as a caveat.
>
> ⚠ **One open check, correctly disclosed by the implementer and worth keeping:** `A49w` proves the mechanism against a **constructed** marker list. The real instance — AWS hour 2026-08-07 16:00, where `capture_marker.log` begins mid-hour at `ec487909…` — has **not** been run through a post-fix coverage report. **That is a free real-world check of D-3 at the next copy-back.** Not a blocker; nothing depends on it.

Packet per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Companion: [`coverage-split-hour-sh1-batch-summary.md`](coverage-split-hour-sh1-batch-summary.md). Spec reported against: [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md).

**Nothing here supersedes an earlier ruling.** SH-1 carried two decisions and I took both as written (§2 below) — no disagreement with either.

---

## 1. Ranked verification handles

**If you only run one: #2 — the gate.** Covers build, harness (all 8 new fixtures), display-parity and version-bump state in one command.

| # | Handle | Expected | Result |
|---|---|---|---|
| 1 | The mutation proof: force `ClassifyHour` back onto the pre-SH-1 path, rebuild, run the harness, then revert | `A49o` and `A49p` FAIL pre-fix; `ALL PASS` post-fix | Confirmed live both ways — `A49o`/`A49p` read `NotCapturing` on the reverted path, `ALL PASS` after restoring the fix |
| 2 | `powershell -File tools/checks/verify-gate.ps1 -Mode prepush` | GATE PASSED, six project builds OK, harness ALL PASS, display-parity OK, "no engine-path change" | GATE PASSED, all sections OK |
| 3 | `git diff --stat -- settings.json` | Empty | Empty |
| 4 | Read `AccumulateHourStats`'s body in the diff | Unchanged | `git diff` shows zero `+`/`-` lines inside its body — only a doc-comment cross-reference and its unchanged call site touch it |
| 5 | `A49v` — count `HourResult` rows for a weekday containing a split hour | 24 | 24, confirmed via `BuildResult` end-to-end (not just `ClassifyHour` in isolation) |
| 6 | `A49u` — the gap-straddling fixture's own stats assertion | `span0.LongestGapMs = 0`, `span1.LongestGapMs = 420000` (not 0, not double-counted) | Matches exactly — the arithmetic identity that would expose a phantom-gap or dropped-gap bug |

---

## 2. Decisions queued, with my read

**Both of the brief's own §5 decisions taken as written — no disagreement, recorded so they are not silently re-decided:**

- **D-1** (one row per hour): confirmed structurally — `ClassifyHour` always returns exactly one `HourResult` regardless of split, and `A49v` confirms it end-to-end (24 rows for a split day).
- **D-2** (Captured wins on a non-Defect disagreement): implemented exactly as stated — `A49o`, `A49t` both exercise it (OFF+Captured half and OFF+Captured+OFF all resolve `Captured`).

**One decision the brief did not name — I raised it, and it is now RULED as D-3:**

The combine order when **no span is `Defect` and no span is `Captured`**, but the surviving spans disagree — e.g. one span reads `UnknownScope` and another reads `NotCapturing` or `ExpectedMissing`. Neither the original ruling ([`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §2) nor D-2 covered this; both only speak to the Defect/Captured axis.

**As I built it:** `ExpectedMissing > NotCapturing > UnknownScope`. **Half right** — `ExpectedMissing > NotCapturing` stands. **`UnknownScope` was wrong at the bottom; D-3 moves it to the top of the residual:**

> **Final order: `Defect` > `Captured` > `UnknownScope` > `ExpectedMissing` > `NotCapturing`.** Full reasoning in [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §5a. The decisive point I missed: `ClassifySpan` — which this build wrote — already checks `unknown` **before** `off` on the single-scope path, so bottom-placing `UnknownScope` in the combine silently reversed the function's own precedence. Bottom-placing it also launders an uncharacterisable span into a confident `NotCapturing` label — the SH-1 defect in miniature, reintroduced at the combine step it exists to fix.

**Fixed in `tools/BacktestRunner/CoverageReport.vb`, the `ElseIf` chain in `ClassifyHour`'s combine block. Fixture added: `A49w`** — a first-ever marker landing mid-hour (span0 `UnknownScope`, no marker applies yet; span1 `NotCapturing`, that marker's own `off` record) asserts the hour reads `UnknownScope`. Confirmed to **fail** against the pre-ruling order (reads `NotCapturing`) the same way the mutation-proof pair fails against the pre-SH-1 code — reverted after confirming, then restored the ruled order.

My original "too contrived to fixture" judgment did not hold: the discriminating shape is not synthetic — [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §5a cites it already occurring in production (`capture_marker.log` beginning mid-hour on the AWS box, `ec487909…`, 2026-08-07 16:02), and it recurs on any box brought up with the capture overlay in place.

---

## 3. Spec-back proper

### What the brief got right, specifically

- **§4.2, "the part the ruling did not name."** This was exactly right and was the actual hard part of the build — `HourStoreStats` genuinely cannot answer a sub-hour question, and route (b) (a second, targeted-by-caller pass, run only when a split exists) was the correct call over restructuring `AccumulateHourStats`. The brief naming this explicitly, ahead of any code, is what kept the build from drifting into route (a).
- **Slip 2, named concretely enough to design a fixture against directly.** "Seed the sub-span's gap measurement from the last trade before the span, not from the span's first row" is precise enough that `A49u` could be written straight from the sentence — a restart with no scope change, one trade before the marker, one after, asserting the exact gap value lands on the correct span. Without that specificity I'd likely have shipped a plausible-looking but silently-reset-at-the-boundary version, since `RowCount=1, LongestGapMs=0` (a genuinely clean first row) and `RowCount=1` with a *dropped* carried gap look identical unless you assert the number.
- **The three escalation triggers were the right ones to watch, and none fired** — I never needed route (a) (`AccumulateHourStats` is untouched), `A49o`/`A49p` failed pre-fix on the first real check (not assumed), and `HourResult` never stopped being one row per hour.

### Which assumptions broke

- **The brief's own word "targeted" in route (b).** [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §4.2 specifies *"a second, **targeted** pass over the store for split hours only."* What I built (`AccumulateSplitSpanStats`) is a **full** second linear pass over the whole walked `[fromUtc, toUtc)` range whenever *any* split hour exists in the marker list — every month gets re-read, not just the months containing a split. This is a deviation from the brief's word, not an unmeasured property of a build that matched it. **Accepted as built** (per review) — the cost is offline (a coverage-report run, not the live app) and bounded (split hours are deploy/toggle-rare, so the extra pass is rare too) — but a future reader must not infer from "targeted" in the brief that the store is read once. **Bounded fix if this ever bites:** pass the split-hour set down into `HistoricalStore.EnumerateMonths`'s caller and skip any month containing none, rather than walking every month unconditionally.

Every escalation trigger and both of the brief's own §5 decisions (D-1, D-2) held as stated — no other assumption broke.

### Where the spec was narrower than its own words

- §6's fixture table lists **7** required cases; I shipped those 7 (`A49o`–`A49u`) plus one more (`A49v`) that the table doesn't ask for but verification handle 5 (§7) implicitly needs — counting `HourResult` rows for a day containing a split hour requires a `BuildResult`-level fixture, not just a `ClassifyHour`-level one, since `ClassifyHour` alone can't demonstrate the day-level row count the handle names. Worth stating directly in a future split-hour-family brief: if a verification handle names a `BuildResult`-level property, the fixture table should say so, not leave it inferred from the handle.

### Constraint pairs that nearly conflicted

- **None found.** The route-(b) constraint ("don't touch the hot path") and the slip-2 constraint ("carry `prevTs` across the split") looked like they might pull against each other at first glance — the hot path's carry discipline is exactly what the new pass has to replicate without sharing code with it — but they resolved cleanly: `AccumulateSplitSpanStats` is a structurally separate function that duplicates the walk-and-carry shape rather than parameterizing `AccumulateHourStats`, so both constraints hold simultaneously without an escape hatch being needed.

---

## 4. What I did not verify, and cannot

- ⛔ **How many split hours exist in the real AWS store.** Same as brief §8 — not counted. (Route (b)'s full-vs-targeted cost, previously listed here, is re-filed under §3 "Which assumptions broke" — it is a deviation from the brief's own word, not an unmeasured property.)
- ⛔ **Anything live.** No coverage run against a real store; everything here is fixture-proven against constructed temp stores.
- ⛔ **Whether a real first-ever-marker-mid-hour case in the AWS store (the `ec487909…` deploy the ruling cites) actually classifies `UnknownScope` post-fix** — `A49w` proves the mechanism against a constructed marker list; I did not re-run coverage against that store's real `capture_marker.log`.

**The residual-combine order is no longer an open judgment call** — D-3 is RULED ([`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §5a: `Defect` > `Captured` > `UnknownScope` > `ExpectedMissing` > `NotCapturing`), implemented, and fixture-tested (`A49w`, confirmed to fail on the pre-ruling order the same way `A49o`/`A49p` fail on the pre-SH-1 path). See §2 above for the corrected reasoning.

---

## 5. Review — 2026-08-13, by the seat that wrote the brief

**Effort: Opus / high.**

### 5.1 Verified myself

| Claim | Result |
|---|---|
| Build + harness | ✅ Release **0/0**; **ALL PASS**; `A49o`–`A49v` all present, and each fixture's name states its own property |
| Handle 4 — `AccumulateHourStats` untouched | ✅ The diff touches only a doc-comment cross-reference. **Route (b) held; the hot path is clean** |
| `ClassifySpan` / `AccumulateSplitSpanStats` separate | ✅ Both new, neither parameterises the hot path |
| Combine block | ✅ Reads exactly as §2 describes — and that is what D-3 corrects |
| `settings.json` | ✅ empty |
| ⛔ **The mutation** | **I did not re-apply it.** I re-ran the harness clean. Same limit as the four preceding reviews |
| ⛔ **Handle 6's arithmetic** (`span1.LongestGapMs = 420000`) | The fixture asserts it and passes; **I did not recompute it by hand** |

### 5.2 ✅ D-3 — ruled, and you were half right

**See the banner and [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) §5a.** `ExpectedMissing > NotCapturing` stands as built and your reasoning for it is sound. `UnknownScope` moves to the top of the residual.

**Raising it rather than folding it in silently was the right call** — the brief genuinely did not cover the case, and a build that had quietly picked an order would have buried it.

### 5.3 ⚠ The one re-filing — a deviation filed as an unknown

§4's first bullet is honest and precise about what the code does, and I would rather have it there than not. **But it is in the wrong section.** The brief said *"a second, **targeted** pass over the store for split hours only"*. The build does a **full** second pass over the whole walked range whenever any split hour exists anywhere in it.

**In practice, most coverage runs over a multi-week range contain at least one deploy, so most runs now read the store twice.**

✅ **Accepted as built.** The verb is a manual offline tool, the cost is seconds on a store this size, and asking for a performance change without measuring is the speculation this project punishes. ⚠ **But record it as a deviation** so the next reader does not believe the store is read once. **The bounded fix, if it ever bites: pass the split-hour set down and skip months containing none.**

### 5.4 Good feedback on my brief, accepted

§3's point about `A49v` is correct and I will apply it: **§7's handle 5 named a `BuildResult`-level property while §6's fixture table listed only `ClassifyHour`-level cases.** A handle that needs a higher-level fixture should say so in the table rather than leave it inferred. My omission; you covered it anyway.

### 5.5 What is owed before this commits

1. **Reorder the residual combine** to `Defect > Captured > UnknownScope > ExpectedMissing > NotCapturing`.
2. **Add `A49w`** — first-ever marker landing mid-hour with `enabled=false`: span0 `UnknownScope`, span1 `NotCapturing`, hour must read **`UnknownScope`**. ⚠ **It must FAIL on the current build** (which returns `NotCapturing`). If it passes, the reorder did not take.
3. **Re-file §4's first bullet** under "which assumptions broke".
4. Re-run the gate.
