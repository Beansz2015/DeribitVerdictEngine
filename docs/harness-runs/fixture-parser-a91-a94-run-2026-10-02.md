# Fixture parser (harness 3) on the new fixtures `A91`–`A94` — 2026-10-02 (UTC)

**Trigger:** new fixtures since the programme closed. The trigger table is in `CLAUDE.md` session-start step 7. The families: `A91` (repair seq order), `A92` (seq-less watch), `A93` (collector-halt fixes), `A94` (history store).
**Run:** `tools/checks/fixture-parser.ps1 -SubFilter '^A9[1-4]'`, then again with `-Fp2Only`. **No Jev key loaded and no baseline supplied**, so the candidate lists print before anything is judged (shadow-mode rule, [`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §2). Rev `ccadf7c`.

## Result — nothing to judge; the seat's read was not needed

| Counter (file-wide unless noted) | Value |
|---|---|
| FP-1 residual in `A91`–`A94` (provenance-marked, in-scope threshold literals) | **0** → `EXIT_REASON=NO_RESIDUAL_IN_WINDOW` |
| FP-2 candidates in `A91`–`A94` | **0**. FP-2 judges only fixtures in FP-1's residual (`fixture-parser.ps1`, the `$residualSubNames` loop) |
| `IN_SCOPE_UNMARKED_SITES` | **0**: every in-scope threshold literal in the file carries a provenance comment |
| `IN_SCOPE_SITES` / `IN_SCOPE_PARAMS` | 58 / 25 (CODE-ONLY scope, see below) |
| Jev calls | 0 |

- **Why nothing:** these fixtures exercise store, repair and collector tooling. They pass no settings-derived threshold as a literal, which is the only thing harness 3 audits. A code-only zero here is a real answer, not a skipped run.
- **Scope note:** the 13 `SCOPE_CANDIDATES` (unmapped parameter names such as `atr`, `price`, `epochs`) are file-wide and unchanged from the 2026-09-22 scope run ([`fixture-parser-scope-run-2026-09-22.md`](fixture-parser-scope-run-2026-09-22.md)). None comes from `A91`–`A94`. No scope baseline was written this run.

## Consequence for the trigger

Harness 3's trigger should read **"new fixtures that pass a settings-derived threshold as a literal"**, not "new fixtures". A seat can check that with this same key-less run in seconds before writing any read. Proposed wording for `CLAUDE.md` step 7, harness 3 row.
