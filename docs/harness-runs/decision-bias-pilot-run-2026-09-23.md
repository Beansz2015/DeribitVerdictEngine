# Harness 6 (decision-bias tripwire) — pilot run, 2026-09-23 (UTC)

**Population:** 150 trader-ruled decisions from `docs/` at `b5000c9`: 124 `ADOPTED`, 22 `OVERRULED`, 4 `PARTIAL` (built in phase A, [`../decision-bias-measurement-phase-a-spec-back.md`](../decision-bias-measurement-phase-a-spec-back.md); outcome labels reviewed independently, 0 changed, `e1964e5`).
**Seat baseline:** written blind and committed first (`41a61c2`), with its contamination declared in [`decision-bias-20260923T140527Z-baseline-notes.md`](decision-bias-20260923T140527Z-baseline-notes.md).
**Jev run:** 150 items × 5 samples, 571,103 input tokens, **$0.024**, 258 s, 0 firewall blocks, 9 unstable rows. Raw output: [`decision-bias-20260923T140527Z-jev.json`](decision-bias-20260923T140527Z-jev.json). Scorer output: [`decision-bias-20260923T140527Z-score.md`](decision-bias-20260923T140527Z-score.md).

**The flag:** a decision is flagged when the judge answers `gives_up_for_economy`. A useful flag catches `OVERRULED` decisions and rarely fires on `ADOPTED` ones. The base rate of `OVERRULED` among scored rows is 22 of 146 (15 %).

---

## 1. Results, both directions

| Cut | n (overruled) | Keyword match: catches · false flags · precision | Seat: catches · false flags · precision | Jev: catches · false flags · precision |
|---|---:|---|---|---|
| All scored rows | 22 | 5/22 · 2/124 · **5/7** | 11/22 · 13/123 · 11/24 | 9/22 · 16/124 · 9/25 |
| Without the 46 the seat recognised | 7 | 1/7 · 2/94 · 1/3 | 3/7 · 7/93 · 3/10 | 3/7 · 12/94 · 3/15 |
| Rationale dated before 2026-09-11 | 14 | 3/14 · 2/107 · 3/5 | 6/14 · 12/106 · 6/18 | 6/14 · 15/107 · 6/21 |
| Rationale dated on or after 2026-09-11 | 8 | 2/8 · 0/17 · 2/2 | 5/8 · 1/17 · 5/6 | 3/8 · 1/17 · 3/4 |

⚠ **A pilot, not a rate.** The deciding arm is 22 rows, and 7 once recognised items are removed. Every interval is wide (the scorer prints Wilson intervals: Jev catches 0.23–0.61).

**Flag-level agreement, Jev against the seat:** 133 of 149 (both flag 17 · seat only 8 · Jev only 8 · neither 116). The label-level agreement is much lower (57 of 149): nearly all disagreements are Jev reading `richer_option_wrong` where the seat read `no_richer_option`. Both are "not flagged", so they do not move the tripwire.

---

## 2. What the numbers say

1. **Jev's flag carries signal.** Among Jev-flagged rows, 36 % were overruled, against a 15 % base rate. Without the recognised items: 20 % against 7 %. Before the 2026-09-11 ruling: 29 % against 12 %.
2. **Jev does not beat the seat's hindsight labels, and it is not meant to.** The seat labelled with the three-step test in mind and knew 46 rulings. The tripwire's use is the real-time case, where the seat judges its OWN fresh recommendation. `CLAUDE.md` says that is the judgment that fails. Jev is an outside read at about $0.0002 per decision.
3. **The keyword match has the best precision (5 of 7) and the worst recall (5 of 22).** It is free and deterministic. Its phrases are also what post-2026-09-11 rationales now write about themselves, so part of its after-ruling precision is self-labelling, not detection.
4. ⛔ **About half the overrules are unreachable by this question.** Of the 13 overrules Jev did not flag, several were won by an option nobody listed: `A54a` scope went to (d), a reflection guard; `GR-1` went to (d); `D-6d.1` went to (c). A judgment on the recommendation's stated reason cannot predict a better option that was not on the table. **That is a limit of the question, not of the detector** (predicted in the baseline notes).
5. **False flags cost little.** Jev flags about 13 % of adopted decisions. As a tripwire, a flag reserves the decision for the trader instead of auto-proceeding it: about one extra question per 8 decisions. A miss is an auto-proceeded economy pick.

---

## 3. Recommendation for arming (the seat's read)

- **Arm it as: flag = Jev `gives_up_for_economy` on a STABLE row, OR the keyword match.** Measured on this population:

  | Rule | Catches | False flags | Precision |
  |---|---:|---:|---:|
  | Jev, all rows | 9/22 | 16/124 | 9/25 |
  | Jev, stable rows | 9/22 | 15/124 | 9/24 |
  | Keyword match | 5/22 | 2/124 | 5/7 |
  | **Jev stable OR keyword** | **10/22** | **16/124** | **10/26** |

  The keyword arm adds one catch Jev missed (`S-4` `D-1`) and one false flag. Keep it: it is deterministic, so it cannot flip between samples. ⚠ This paragraph first claimed every keyword catch was also a Jev catch. That was written before measuring, and it was false.
- **Use:** the seat writes its own label for its recommendation first (the protocol's baseline), then runs the tripwire on that one decision. A flag means reserve, not auto-proceed.
- **Do not tune the question on this population.** It is now seen. The re-measure is on new decisions, on the go.

## 4. Not verified

- Whether the flag works on decisions the seat writes AFTER seeing this result. The seat now knows what Jev flags, which can change how it writes rationales.
- The 3 non-pre-ruling-provenance items separately.
- Any effect of the 10 cross-item reveal pairs beyond the recognised-items cut.
- Why Jev reads `richer_option_wrong` so often. The likely cause is literal reading: its criteria require a richer option, and Jev may be skipping that condition. Not probed.
