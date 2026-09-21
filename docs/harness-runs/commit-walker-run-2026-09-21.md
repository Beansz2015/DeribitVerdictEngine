# Commit walker — first run record, 2026-09-21 (UTC)

**Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md). **Harness:** [`../commit-walker-check-spec.md`](../commit-walker-check-spec.md).

⛔⛔ **THIS IS A DEMONSTRATION, NOT A MEASUREMENT. `n=3`.** The protocol was followed in full — hand baseline written first, to a tracked path, before any detector output existed — but the population is three commits. **3 of 3 agreement at `n=3` establishes almost nothing about a catch rate.** Recorded as a protocol rehearsal and for its design findings, which are the real output.

---

## 1. Why the population is three

**Two of my own errors compounded, and both are now fixed in the docs.**

| Error | Effect |
|---|---|
| The spec's acceptance dry run used the same window as the measured run | The implementer reported verdict aggregates back, contaminating commits 1–300. Rule added: [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4a |
| ⛔ **The spec never considered that the tag has an ADOPTION DATE** | Measured this run: **`[no-engine-change]` was first used 2026-08-13**, commit `c6c6942d8a`. Across 788 non-merge commits from March to July, **zero** carry it |

**The first attempt at a clean window, `-Skip 300 -Count 300`, fired the escalation trigger correctly: `RESIDUAL_PCT=73`.** Cause: 216 pre-adoption docs commits, untagged because the convention did not exist. ⭐ **The classifier was right and the population was wrong** — which is exactly what an escalation trigger is for, and it cost nothing because it fired before any judgment ran.

The only window both post-adoption and uncontaminated is **40 commits, 2026-08-13 to 2026-08-22**, whose residual is three.

---

## 2. Coverage

```
COMMITS_WALKED=40          MERGES_EXCLUDED=305
AGREE_TAGGED_NO_ENGINE_PATH=35     AGREE_UNTAGGED_ENGINE_PATH=2
RESIDUAL_TAGGED_BUT_ENGINE_PATH=3  RESIDUAL_UNTAGGED_NO_ENGINE_PATH=0
RESIDUAL_TOTAL=3           RESIDUAL_PCT=7.5           COMMITS_JUDGED=3
USAGE_INPUT_TOKENS=2627    USAGE_OUTPUT_TOKENS=249    WALL_TIME_SEC=2.1
```

**Cost: $0.00011.** Rate from `docs.typesafe.ai/models.md` — $0.042 per Mtok, input only, output free.

⭐ **`RESIDUAL_PCT=7.5` on a post-adoption window against 73% on a straddling one.** The path rule holds when the population is valid.

---

## 3. The comparison

Baseline: [`commit-walker-20260921T204555Z-baseline.json`](commit-walker-20260921T204555Z-baseline.json), written before the detector ran.

| Commit | Seat | Detector | Conf. | `changes_runtime` | Agreement |
|---|---|---|---|---|---|
| `504442e29e` | `tag_correct` | `tag_correct` | **0.14** | 0.44 | AGREE |
| `91942d6739` | `tag_wrong` | `tag_wrong` | 0.79 | 0.87 | AGREE |
| `c6c6942d8a` | `tag_wrong` | `tag_wrong` | 0.67 | 0.88 | AGREE |

**3 of 3. No disagreements.**

**Adjudication of the one uncertain case, resolved by reading the diff after both reads were locked:** `504442e29e` is genuinely a pure refactor. `ThinTradesSkipReason` returns the byte-identical format string, and the only other change computes `MinTradesForScoring(cfg)` once instead of twice — same value, no side effects. Its commit body states *"No behaviour changes"* and *"mutation-proved"*. **`tag_correct` is correct.**

---

## 4. ⭐⭐ The finding that matters more than the score

**The detector's 0.14 confidence on `504442e29e` was not an error. It was an honest report that the state it was given could not settle the question.**

From touched paths and line counts alone — `+9` to a `Core/` scoring file, `+5/−3` to a `UI/` file — **a refactor and a behaviour change are indistinguishable.** Both Nouls sat at the fence: `changes_runtime` 0.44, `refactor_only` 0.50.

⛔ **The information that resolves it was withheld by the spec's own trap 3**, which forbids sending diff bodies to avoid context rot. That was the right call for the 93% — and it creates a blind spot on exactly the cases that reach the residual.

⭐ **And the cheapest fix was sitting unused: the commit BODY says *"No behaviour changes"* in plain English. We sent only the subject.**

**Two changes follow, neither built yet:**

1. **Send the commit body as well as the subject.** Bodies in this repo are long and explanatory; this one contained the answer. Low risk of context rot, high information.
2. **Escalate on low confidence.** A verdict under about 0.3 triggers a second request carrying the diff of the engine files only. This is the cascade pattern in `docs.typesafe.ai/cookbooks/sde_cascade.md` — cheap first pass, expensive pass only where the first was unsure.

⚠ **Neither threshold is fitted. 0.3 is a guess from one observation and must be re-derived on a real population.**

---

## 5. Owed

| # | Item |
|---|---|
| 1 | ⛔ **Era guard.** The tool will silently mislead on any window reaching before 2026-08-13. It must refuse, or at minimum classify pre-adoption commits into their own reported class, never the residual |
| 2 | Send the commit body (§4) |
| 3 | Low-confidence escalation to a diff-carrying second pass (§4) |
| 4 | ⭐ **The real measurement.** It needs a post-adoption population the seat has not seen. The clean window is spent at `n=3`; new commits accrue at roughly 200 a month, so a usable window exists within weeks |

---

## 6. What I did NOT verify

- **That 3 of 3 means anything.** It does not. `n=3`.
- **That the path rule's engine/non-engine split is right.** [`../commit-walker-check-spec.md`](../commit-walker-check-spec.md) §8 disclaims this, and this run did nothing to test it.
- **The two `AGREE_*` classes, 37 commits.** Never judged by either of us, by design. If the path rule is wrong, it is wrong silently there and neither the seat nor the detector would see it.
- **Whether the seat's own baseline was independent of the detector in spirit as well as procedure.** I wrote it first and the file is timestamped, but I had read this repo's docs extensively beforehand, and two of the three commits are described in [`../trader-tick-queue.md`](../trader-tick-queue.md). **A seat is not a blind judge here, and no procedure makes it one.**
