# P5-test report-back to spec author

**Status:** Commits 1 + 2 shipped local-only. First full run: **55 cases, 55/55 PARITY, zero discrepancies.** Awaiting your call on commit 3 cleanup + Type B engine cleanup spec.

Full spec-back: `docs/ui-reskin-p5-test-spec-back.md` (209 lines). This is the condensed report.

---

## 1. What shipped

| Hash | Subject |
|---|---|
| `db1675c` | Commit 1 — harness scaffolding, 5 sentinels, `Ctrl+Shift+T` wired into existing `OnFormKeyDown` ElseIf per §0.6 |
| `10eafab` | Trivial fix (superseded) — populated 21 empty rows in `NeutralVerdict` |
| `303ba51` | Per-case `WithBreakdownItem` per your guidance, supersedes 10eafab |
| `8507fd4` | Commit 2 — 55-case library + spec-back |

**Discoveries during scaffolding that the kickoff didn't anticipate:**

1. **Kickoff §1.2 side-effect guard list is stale post-P5a.** All three calls (`AnalysisLogger.LogRun`, `LivePerformanceTracker.UpdateAsync`, `AnalysisOutputDump.Append`) live in `RunAnalysisAsync` now (per the comment at `MainForm_Render_Sections.vb:319`). Harness bypasses `RunAnalysisAsync` entirely → no guards needed. Simplified the scaffolding.

2. **`BuildPlaintextSnapshot` signature is 6 args, not 4.** Kickoff §3.3 example shows `BuildPlaintextSnapshot(tc.Verdict, tc.Indicators, tc.Norms, tc.Cfg)` but actual signature includes `vwapWarmup` and `lastTradePrice`. Used the actual signature.

3. **`Kelly` fields are recomputed inline by both renderers.** Both `RenderOutputHeader` and `BuildPlaintextSnapshot` call `ScoringEngine.CalcKellySizing(v, atrStop, cfg)` mid-render. Whatever the harness puts in `v.Kelly*` gets overwritten. Idempotent so parity preserved; documented in spec-back §4.3 but worth flagging if you ever need synthetic Kelly fixtures.

---

## 2. First-run parity result

**55/55 PARITY** — byte-identical legacy vs snapshot after CRLF normalisation, confirmed via `diff <(tr -d '\r' < legacy) <(tr -d '\r' < snapshot)`.

55 cases pack the §4 coverage matrix:
- 7 verdict tiers, 3 CONTEXT states, MTF PASS/BLOCK/state-only
- TRANSITIONAL regime + RegimePenalty SCORE format
- 4 ATR CAPPED reasons + sub-tick suppression
- 9 structural row combinations
- 4 KELLY variants (the reachable ones — see §4)
- 4 VPFR signal × value area pairs
- OI×CVD CONFIRMED + CONFLICT both directions
- 5 Trend Structure states, 5 MicroCVD states
- BBW ACTIVE/RELEASING/NONE
- Funding bias×momentum (incl. negative-zero clamp)
- REGIME ANCHOR both directions
- HOLD/EXIT layers 1, 1.5, 2, 3
- STATIC FALLBACK norms, VWAP WARMUP, Spread WIDE/TIGHT, Liq active
- RSI Div BULLISH/BEARISH, Volume USD format arms

---

## 3. Branch-arm gap audit (§9.5 deliverable)

Methodology: branch coverage (each `If/ElseIf/Else` and `Select Case` arm hit ≥ 1 case), not state cross-product. Inventory across the three render files: **~115 arms**.

- **~106 arms (≈92%) substantively exercised**, confirmed via grep against output artifacts.
- **9 arms (≈8%) uncovered**, split into two categories below.

### 3.1 Test-side gaps (3 arms — closable by neutral-default flips)

These need your call on whether the default behaviour is the right one.

| Arm | What | Action needed |
|---|---|---|
| **B2** empty `VerdictContext` | `NeutralVerdict` defaults to `"CONFIRMED"`; the "no CONTEXT line emits" arm only fires when `VerdictContext = ""`. Currently every case emits `CONTEXT:` line. | Is `""` a legitimate production state? If yes, add a case `.WithContext("")`. If no, the empty-string arm is dead and can be deleted. |
| **B6** HOLD/EXIT default | `NeutralVerdict.HoldStatus = ""`. The render gate emits only when `HoldStatus != "N/A -- no open position"`. So `""` passes the gate → every case emits an empty `HOLD / EXIT:` line. Both renderers agree (parity preserved), but cosmetically misleading. | Suggest flipping the neutral default to `"N/A -- no open position"` so only the 4 hold-exit cases emit the line. Cosmetic only; no parity impact. |
| **B19** VWAP s2 anchor | The anchor switches at 13:30 UTC based on `DateTime.UtcNow`. Harness fires once → observes one anchor only. Both formatters are symmetric (same string template, same render path on both sides). | Confirm a single observation is sufficient given the formatter symmetry. Alternative: run the harness twice (pre + post 13:30). |

### 3.2 Type B engine-side unreachables (4 arms — what you asked about)

Render branches present in code but unreachable via realistic test data given current settings. **These are the engine-cleanup candidates** the user wants to discuss.

| Arm | Why unreachable | Suggested remediation |
|---|---|---|
| **B11 KELLY suppressed** | `CalcKellySizing` early-exits only when `v.Verdict ∈ {NEUTRAL, WAIT, ""}` or `stopDistanceUsd ≤ 0`. The engine emits verdicts from `{STRONG LONG, LONG, WEAK LONG, NO TRADE, WEAK SHORT, SHORT, STRONG SHORT}` — none trigger suppression. | **Likely dead code.** Spec to delete the `"NEUTRAL"` / `"WAIT"` / `""` branches in `ScoringEngine_Kelly.vb:44–45` if those verdict strings are confirmed never emitted. |
| **B13 KELLY no-`[CAPPED]` arm** | With default `cfg.Kelly` (`EstProbFloor=0.45`, `EstProbScale=0.20`, `UseHalfKelly=True`, `MaxRiskFraction=0.05`) and `b = 2.0/1.2 ≈ 1.67`, the minimum `fHalf` across all confidence tiers is ~0.06, always > 0.05 → always capped. | Either: (a) raise `MaxRiskFraction` so LOW-confidence runs can fall under the cap; (b) accept the branch as deterministically capped and document. (a) is also a sizing-policy question. |
| **B14 KELLY Contracts ≥ 1** | `riskPerContract = ContractFaceUsd × atrStop = 10 × atrStop`. At cap (`fApplied=0.05`, `Account=1000`), `KellyRiskUsd = $50`. For ≥ 1 contract: need `atrStop ≤ $5`, i.e., `ATR ≤ $4.17`. BTC at $50k has ATR much larger. | The branch is reachable in principle if account scales up. Three options: (a) raise `AccountSizeUsd` in default config so realistic ATR produces ≥ 1 contract, (b) test with a per-harness cfg override (not currently supported), (c) accept as production-unreachable with current sizing and document. |
| **B14 KELLY Lean ≥ 1** | Same arithmetic as B14a — NO TRADE bias-only path hits the same `KellyContracts ≥ 1` gate. | Same options as above. |

---

## 4. Open decisions

1. **Commit 3 (cleanup) — ship now or wait?** Kickoff §2's gating conditions are met (run complete, zero discrepancies, re-run clean). Awaiting trader's review window. Cleanup deletes `MainForm_TestHarness.vb`, `TestHarnessCases.vb`, the Ctrl+Shift+T ElseIf, `tools/send-ctrl-shift-t.ps1`.

2. **Test-side gaps (§3.1) — close them in commit 2 follow-up or accept?** Three trivial changes (one default flip + one optional case). Doesn't affect parity result; affects cosmetics + coverage report cleanliness.

3. **Type B unreachables (§3.2) — engine cleanup spec?** Four arms split across two areas: KELLY suppression (likely delete dead code) and KELLY contract sizing (config or behaviour question). Worth a separate spec if you want to act on them.

4. **Confidence statement.** The harness confirms "the new design will not be missing anything that the legacy output gives" for ~92% of arms. The remaining 8% are either production-unreachable (Type B) or trivially closable test-side gaps. **No documented arm exists where the new design might silently drop information that legacy renders.** This is the answer to the trader's gating question — please confirm or push back.

---

## 5. Artifacts available for review

- `verify/p5-test/test-results.md` — the PARITY report
- `verify/p5-test/*-legacy.txt` / `*-snapshot.txt` / `*.png` — 55 cases × 3 each
- `docs/ui-reskin-p5-test-spec-back.md` — full spec-back (209 lines)
- `UI/TestHarnessCases.vb` (990 lines) — the case library
- `UI/MainForm_TestHarness.vb` — harness scaffolding + extended `TestCaseBuilder`

Trader will paste this and the spec-back to your conversation for your sign-off.
