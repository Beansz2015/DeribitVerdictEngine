# Fixture parser — the site revision 3 opened, 2026-09-22 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) at `ed3e487` (revision 3). **Detector:** `FP-1`. **Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md).

**Baseline:** [`fixture-parser-20260922T190516Z-baseline.json`](fixture-parser-20260922T190516Z-baseline.json), committed at `64f1e85` **before** the run.

---

## 1. The window

**One site: `SeededWsSource#2088#staleAfterSec`.** Revision 3's `FP-D21` (item `8f` in [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §8.4) resolved it to `network.ws_stale_after_sec`, so it is in scope by construction. It carries the item-`8d` MECHANISM comment, so it joined the `FP-1` residual for the first time.

It had never been judged: in scope before `8d` but unmarked, then marked after `8d` but scoped out by `FP-Q1` (item `8i`). `SeededWsSource` is a `Function`, and `FP-2` judges `Sub`s only, so there is no name item.

Run: `-SubFilter '^SeededWsSource$'`, no `-ScopeBaselinePath`.

---

## 2. ⛔ Result — a STABLE disagreement

| | Seat | Detector |
|---|---|---|
| Verdict | `mechanism_declared_ok` | **`declared_but_contradicted`** |
| Agreement rate | — | **1.0 — all 5 samples** |
| `mean_top_prob` (min) | — | **0.526** (0.50) |
| `declares_class_noul` · `matches_evidence_noul` | — | 0.958 · 0.70 |

`USAGE_INPUT_TOKENS` **6,355** · ≈ **$0.0003** · 3.0 s. Harness exit 1 (`FP1_BAD_VERDICTS=1`).

---

## 3. Adjudication — the seat is right

Read against `verify/ordercheck/Program.vb:2064-2088` and the three callers `A16a`–`A16c`.

- The comment opens *"MECHANISM — staleAfterSec:=10"*. A class is declared; the detector's own `declares_class_noul` agrees at 0.958.
- `declared_but_contradicted` needs *"MECHANISM while its own reasoning ties the value to tracking `matched_key`"*. **The reasoning about the value is `staleAfterSec << tradesAgeSeconds`** — `A16a` and `A16c` pass `tradesAgeSeconds:=300`, and any value in (0, 300) serves.
- The key appears in the comment three times, **each time to argue AGAINST deriving from it**: *"not a copy of network.ws_stale_after_sec"* · *"deriving it from cfg would be WRONG"* · production falls back to the setting, so a cfg-read value ≥ 300 would make the age gate un-trippable.
- The equality with the ever-shipped `{10}` is stated and licensed. The question text tells the detector a MECHANISM declaration is consistent *"regardless of `literal_equals_ever_shipped`"*.

⚠ **I do not know what drives the detector's call and I am not naming a cause.** The `A23a` block also discusses equality and keys and was judged `mechanism_declared_ok` at 0.67–0.82. What separates the two texts is untested. [`fixture-parser-scope-run-2026-09-22.md`](fixture-parser-scope-run-2026-09-22.md) §3 is the reason for the restraint.

⚠ **Conflict, declared before the run:** the seat knew the intended class from the item-`8d` ruling. That argues for caution about the seat's read in general. It does not change the adjudication above, which rests on the comment text and the callers.

---

## 4. ⛔⛔ What this changes — `harness-shadow-mode-protocol.md` §4d is not a law on `FP-1` either

[`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4d recorded, from the 2026-09-22 clean run: agreements all stable, disagreements both unstable, **"zero confident-and-wrong rows"**. §4f then showed an agreeing row can be unstable on `FP-Q1`.

**This row is the mirror case, on `FP-1` itself: wrong, and STABLE 5 of 5.** Stability did not flag it.

| Signal | Would it have flagged this row? |
|---|---|
| Agreement rate | ⛔ **No** — 1.0 |
| Top probability | Only by luck of the band. 0.526 is below every `A23a` agreeing row (0.67–0.82) but **above** the clean run's agreeing `A4_TfiWindowFromEnd` name row at 0.434 |

⭐ **So on `FP-1` neither signal separates right from wrong on its own.** One stable-and-wrong row is enough to break *"stable ⇒ right"* as a rule: a counter-example needs `n=1`. It is **not** enough to estimate how often it happens.

⭐ **What held: the seat baseline caught it.** Without a hand read first, this row would have read as a clean, stable finding — *"the new declaration is contradicted"* — and the next seat might have edited a correct comment to satisfy a wrong verdict.

---

## 5. ⚠ What this run does NOT establish

- **A rate.** One item.
- **The cause** of the detector's call (§3).
- **Whether `FP-2` would have judged a `Function`.** It is not built to.
