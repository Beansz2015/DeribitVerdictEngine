# Fixture parser — the 14 items the `A20a`/`A20b`/`A23b` declarations opened, 2026-09-22 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) at `cbc2c91`. **Detectors:** `FP-1` and `FP-2`. **Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md).

**Baseline:** [`fixture-parser-20260922T193359Z-baseline.json`](fixture-parser-20260922T193359Z-baseline.json), committed at `3874d18` **before** the run.

---

## 1. How the window was made

Item `8j` in [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §8.4 found 11 in-scope literals equal to ever-shipped values with **no** class declaration, so `FP-1` had never judged them. **Trader ruling 2026-09-22 (UTC): a separate agent writes the declarations, so the seat is not judging its own prose.**

| | |
|---|---|
| Author | Separate agent, pinned to Opus. **Reported model ID `claude-opus-5-5`**, checked before any work (the trader asked for this check because earlier subagents ran outdated models) |
| Commit | `44e4e15`, fast-forwarded onto `master`. Comment lines only: 73 added, 1 changed, 0 non-comment lines. Every class claim mutation-checked by the author (15 harness runs) |
| Seat's own check | Clean `-t:Rebuild` of `verify/ordercheck/OrderCheck.vbproj`: 0 warnings, 0 errors, **425 PASS / 0 FAIL**, exit 0 |
| Key-stripped listing after the merge | `IN_SCOPE_UNMARKED_EQUALS_EVER_SHIPPED` 11 → **0**. The 11 sites and 3 subs joined the residual |

Run: `-SubFilter '^(A20a|A20b|A23b)_'`, no `-ScopeBaselinePath`. `FP-D24`'s item-level gate passed (every item baselined).

---

## 2. Result

| Item | Seat | Detector | Rate | `mean_top_prob` |
|---|---|---|---|---|
| `A20a#2557` × 3 (buy · sell · depth) | `mechanism_declared_ok` | `mechanism_declared_ok` | 1.0 | 0.76 · 0.816 · 0.75 |
| `A20b#2591` × 3 | `mechanism_declared_ok` | `mechanism_declared_ok` | 1.0 | 0.80 · 0.712 · 0.75 |
| `A20b#2600` × 3 (the one-line pointer comment) | `mechanism_declared_ok` | `mechanism_declared_ok` | 1.0 | 0.804 · 0.718 · 0.766 |
| ⛔ `A23b#3164#tauFastSec` | `mechanism_declared_ok` | **`shipped_declared_ok`** | **1.0** | **0.614** |
| ⛔ `A23b#3164#tauNormSec` | `mechanism_declared_ok` | **`shipped_declared_ok`** | **1.0** | **0.404** |
| `FP-2` × 3 names | `name_matches` | `name_matches` | 1.0 | 0.772 · 0.69 · 0.918 |

**12 of 14 agree. Every row STABLE 5/5.** `USAGE_INPUT_TOKENS` **99,742** · ≈ **$0.0042** · 35.4 s. ⛔ **Harness exit 0, `FP1_BAD_VERDICTS=0`** — see §4.

⭐ The row the seat expected to be hardest — `A20b#2600`, whose comment is a one-line pointer to the block above — agreed at 0.72–0.80.

---

## 3. Adjudication — the seat is right on both

The `A23b` block opens *"MECHANISM — every settings-backed literal below"*, gives the taus a MECHANISM reason (*"LOAD-BEARING only through their SEPARATION"*, mutation-checked) and closes *"NOT SHIPPED BEHAVIOUR: production resolves norm_window_sec and burst_ratio_threshold PER SESSION"*. **No reading of that text declares SHIPPED BEHAVIOUR.** The detector is wrong, stably.

⚠ **Cause not named.** The `A20a` block also says *"NOT SHIPPED BEHAVIOUR, and deriving from cfg would be WRONG"* and was judged correctly, so the negated phrase alone does not explain it.

**This is the third stable-and-wrong `FP-1` row today** (with `SeededWsSource#2088#staleAfterSec`, [`fixture-parser-stale-run-2026-09-22.md`](fixture-parser-stale-run-2026-09-22.md)). [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4g holds; this raises its count from one to three.

⚠ **An observation, NOT a rule** ([`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4d forbids promoting a probability band to a trust gate): in today's two new windows, the three wrong `FP-1` rows sat at `mean_top_prob` 0.404 · 0.526 · 0.614, and the thirteen agreeing `FP-1` rows (4 on `A23a`, 9 here) at 0.67 or above. The 2026-09-22 clean run's `FP-1` agreement probabilities were **not** checked, and its one wrong `FP-1` row sat at 0.40. **Whether a band separates on `FP-1` is open.**

---

## 4. ⛔⛔ The harness scores a misread as a PASS — a vocabulary defect

`shipped_declared_ok` is an OK class in `tools/checks/fixture-parser.ps1` (`$fp1Criteria`; exit code 0 when every verdict is `mechanism_declared_ok` or `shipped_declared_ok`).

**`CLAUDE.md`'s fixture-literal provenance rule says SHIPPED BEHAVIOUR *"must derive the value from cfg, never hardcode it"*.** Every `FP-1` site is a named-argument **literal** — hardcoded by construction. So on an `FP-1` site, `shipped_declared_ok` can only mean one of two things:

1. the comment really declares SHIPPED BEHAVIOUR on a hardcoded value — **a rule breach**, or
2. the detector misread the declaration — **this run, twice**.

**Neither is a pass.** The run exited 0 with both misreads in it. The baseline comparison is the only thing that saw them.

⭐ **Recommended fix (not built):** drop `shipped_declared_ok` from the OK set for `FP-1`, or fold it into `declared_but_contradicted` with the criteria text citing the rule. It needs a spec line first, because it changes what the detector is asked. Logged as item `8m`.

---

## 5. ⚠ What this run does NOT establish

- **A rate.** 14 items; the 11 `FP-1` rows sit under 4 comment blocks, so the deciding arm is 4, not 11.
- **The cause** of either wrong call.
- **Anything about the 23 in-scope unmarked sites whose literals are off-shipped.** Still undeclared under the rule's *"MUST declare"*; not in this window.
