# Fixture parser — the 23 `fundingBoost`/`upgradeBonus` sites and 14 new names, 2026-09-23 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) (revision 3 plus `FP-D26`, below). **Detectors:** `FP-1`, `FP-2`. **Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md).

**Baseline:** [`fixture-parser-20260923T082947Z-baseline.json`](fixture-parser-20260923T082947Z-baseline.json), committed at `6f0e142` **before** the run. 37 items.

---

## 1. The window

The 23 in-scope literals whose values are OFF every ever-shipped value, undeclared until a separate agent (reported model ID `claude-opus-5-5`) wrote MECHANISM declarations at `8028d08` (merged `4615e5d`; the seat verified the diff is comment-only and the harness passes 425/0). Plus the 14 fixture names that joined the `FP-2` residual with them. `-SubFilter` on the 15 enclosing procedures; no scope baseline, so `FP-Q1` made no call.

---

## 2. ⛔ The first attempt aborted — a web firewall, not the API

Two runs stopped at `EXIT_REASON=API_FAILED` on the first item, `A8_DominantSideCascade#1208#fundingBoost`, with `403 Forbidden`. **No verdict was produced or seen; the population stayed unspent.**

A status-only probe (never printing an answer) located it: **the 403 body is an HTML block page from a web firewall in front of the API**, and the trigger is one comment line — *"the literal: 4 + 3 = 7 (SHORT) and 4 + 7 = 11 (the 11/11 tie)"*, which reads as an SQL `AND x=y` tautology. **1 of the 37 items is blocked.** Replacing VB's leading `'` with `REM` did not clear it.

⭐ **Fix, `FP-D26`:** `tools/checks/lib/InvokeJev.ps1` now reports the HTTP status and a `WafBlocked` flag; `fixture-parser.ps1` records such an item as verdict `WAF_BLOCKED` (agreement `NOT_JUDGED`), **carries on with the rest**, prints `FP1_WAF_BLOCKED`/`FP2_WAF_BLOCKED`, and exits 1 — an unjudged item is never a pass. Rewording the fixture comment was rejected: it tunes the input, and the next such phrase would abort a run again. Key-stripped output is unchanged by the fix.

⚠ **Harnesses 1 and 2 share `Invoke-Jev` but still abort on any failure.** Their text (ledger prose, commit messages) can trip the same firewall. Loud, not silent — but a first run could stop half-way. Not changed here.

---

## 3. Result

| | Judged | Agree | Disagree | Disagreements that were STABLE | Blocked |
|---|---|---|---|---|---|
| `FP-1` | 22 | **4** | **18** — every one `declared_but_contradicted` | **15** | 1 |
| `FP-2` | 14 | 10 | 4 | 3 | 0 |

`USAGE_INPUT_TOKENS` **226,167** · ≈ **$0.0095** · 59 s.

`FP-1` agreements: `A12#1396` (UNSTABLE 3/5), `A25a#3426`, `A28b#3956`, `A42a#7361`.

---

## 4. Adjudication

### 4.1 `FP-1` — the seat is upheld on all 18

Every one of the 18 comment blocks declares MECHANISM explicitly (8 are one-line pointers to a fuller block in the same procedure — the baseline's note says 7; it was a pre-run miscount and the committed file is left as written). Their reasons are the fixture's own arithmetic, an upper or lower edge, a pair constraint, or inertness — each mutation-checked by the author. The key and its shipped value appear only to show the literal is off-shipped and why deriving from it would be wrong or pointless. **`declared_but_contradicted` requires reasoning that ties the value to tracking the key; none of these do.**

⛔ **This is `harness-shadow-mode-protocol.md` §4g at scale: 15 stable wrong rows in one run.** Neither signal separates them: the wrong rows' `mean_top_prob` spans 0.51–0.80, the right rows' 0.53–0.74; `declares_class_noul` is ≥ 0.93 on all 4 right rows but also ≥ 0.9 on 6 wrong ones.

### 4.2 `FP-2` — two anticipated, one reading difference, one seat lean

| Name | Seat | Detector | Read |
|---|---|---|---|
| `A25a_OfiMomentumRetireByteIdentical` | `name_matches` | `name_understates`, STABLE, 0.41 | **Detector plausibly right.** The baseline recorded this exact hesitation before the run: the second check (the flag still gates) goes beyond "retire byte-identical" |
| `A40c_KnobChangeMovesTheGate` | `name_matches` | `name_understates`, UNSTABLE 4/5 | **Detector plausibly right**, same reason — the body also pins the eval re-walk epsilon, noted in the baseline |
| `A8_DominantSideCascade` | `name_matches` | `name_understates`, STABLE, 0.89 | **Seat holds, low margin.** The engine's dominant-side tier walk includes the tie branch, so the tie check is part of the named property; a reader who treats the tie as separate reads it the detector's way |
| `A17g_CalcHoldStatusByteIdentical` | `name_overclaims` (a recorded lean) | `name_matches`, STABLE, 0.51 | **Unresolved.** One Layer-1 case cannot establish byte-identity for the whole function, but the `Check` title scopes it to Layer 1 |

---

## 5. ⭐ A controlled probe — and the first hypothesis refuted

Two already-spent items, three variants, 5 samples each ([`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1)'s own `$fp1Criteria` and `Invoke-Fp1Verdict`, extracted verbatim):

| Item | (a) original | (b) every shipped-value / key sentence removed | (c) original + one clarifying sentence in the criteria |
|---|---|---|---|
| `A43d#7740` | contradicted 4/5 | contradicted 4/5 | **`mechanism_declared_ok` 5/5** |
| `A40b#6870` | contradicted 5/5 | contradicted 5/5 | mechanism 2/5, contradicted 3/5 |

The clarifying sentence: *"Naming `matched_key` or its shipped value ONLY to explain why the literal is NOT derived from it, or to show the literal differs from it or does not depend on it, is consistent with MECHANISM and is NOT a contradiction."*

- ⛔ **The obvious cause — the comments mention the shipped value — is REFUTED:** removing those sentences moved nothing. **No replacement cause is named.**
- ⭐ **The question's wording does move the detector here** — fully on one item, partly on the other. Contrast [`fixture-parser-scope-run-2026-09-22.md`](fixture-parser-scope-run-2026-09-22.md) §3, where input framing moved `FP-Q1` not at all.
- ⚠ **Not a fix yet.** Changing the criteria changes what `FP-1` is asked, so every earlier measurement stops being comparable, and 2 items are not a validation. It needs a fresh, baselined population.

---

## 6. ⚠ What this run does NOT establish

- **A rate for `FP-1` in general.** The same detector agreed with the seat on 23 of 25 (2026-09-22 clean run) and 12 of 14 (the `A20a`/`A20b`/`A23b` window), and on 4 of 22 here. Accuracy depends heavily on the comment population.
- **Why.** §5 rules one cause out.
- **Whether the firewall blocks other harnesses' text today.** Untested.
