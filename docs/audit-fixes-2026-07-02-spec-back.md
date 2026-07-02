# Spec-Back — Audit Fixes F1/F2/F3 + Nits (2026-07-02)

**Spec:** `docs/audit-fixes-2026-07-02-proposal.md` (APPROVED 2026-07-02, D1–D4 all as recommended). Evidence source: `docs/fable5-audit-2026-07-02.md`.
**Implementer:** Fable 5 seat (the spec recommended Opus 4.8; the trader kicked it off in the Fable seat — no design decisions were needed, so the difference is cost only).
**Status: ALL ITEMS SHIPPED.** Four local commits on `master`, gate-green after every change set, **NOT pushed** — trader tests + pushes per the repo rule.
**Settings: v46 → v47** (D2: this pass took the next number; the OFI dominance re-baseline slides to **v48**).
**Live-collection caution honoured:** Release builds only throughout (the gate builds Release; Debug never built); the bin `settings.json` edit is value-neutral for every key the engine reads, so the FileSystemWatcher reload was a behavioural no-op mid-collection. Bin `version` synced to 47 in the same edit, as the spec header required.

## §0. Commit map (spec §6 plan → actual)

| Spec # | Commit | Contents | Gate |
|---|---|---|---|
| 1 | `688127b` `fix(settings): F1+F2+D4 …` | tracked + bin settings.json, POCO removal, D4 fence (applier + PromptBuilder HC17), v47 bump + change_log + §15 row + §6 pointer, fixtures A21a/A21b | GREEN |
| 2 | `8cb5053` `docs(exit-guard): F3 ruling …` | comment block at the guard's CalcOFI call + `time-averaged-ofi-spec-back.md` §3 amendment | GREEN |
| 3 | `5a77f27` `fix(norms): N1 …` | `DynamicNorms.vb` + `UI/MainForm_Analysis.vb` (one arg) | GREEN |
| 4 | `552965b` `docs: N2/N3/N4 …` | CLAUDE.md, architecture.md, DeribitIndicatorProject.md, `ScoringEngine_Helpers.vb` comment | GREEN |

Gate = `tools/checks/verify-gate.ps1 -Mode prepush`: 3 Release builds 0/0 (solution + AutoTweaker + OrderCheck), harness A1–A20i unregressed + new A21a/A21b, display-parity + version guards OK. Run 5× total (pre-work baseline + after each change set). No rendered line added/removed/renamed anywhere (stated in each display-adjacent commit message per the parity rule).

## §1. F1 — dead key removed (D1 = remove)

As specced, all three sites: tracked `settings.json` `regime_gates` block, bin copy, POCO property `TransitionalAdxPenaltyLow` (a removal comment left in `RegimeGateSettings` citing the v31 F8 orphaning). **Not** added to `RejectedPathFragments` (§1 step 3 — snapshot-poisoning rationale recorded in the v47 change_log, including the pre-removal-snapshots-stay-revertable note). Post-removal grep: the only remaining repo references are historical/fixture text (the v47 change_log entry, the POCO removal comment, the A21a fixture, §15/audit docs) — zero live reads.

## §2. F2 — 8 keys synced into the tracked settings.json

All 8 added at POCO defaults, verified identical to the bin copy at implementation time (spec §2 table confirmed correct). Placement mirrors the bin block order exactly: `divergence_penalty` after `divergence_price_gate` in CVD; `decel_penalty` last in MicroCVD; the 6 `hold_*` keys between `min_tradeable_move_pct` and `context_tag_structural_min` in scoring.

**Deviation (cosmetic, deliberate):** the four `hold_rsi_*` values are written as integers (`60`, `40`) — matching the bin copy's serialisation — rather than the `60.0`/`40.0` the spec table showed. JSON-semantically identical (POCO fields are Double); chosen so a tracked-vs-bin text diff of those lines is clean.

## §3. D4 — `scoring.hold_` fence

`"scoring.hold_"` appended to `SettingsDiffApplier.RejectedPathPrefixes` with the specced comment (trader hold-discipline preference, no failure-rate linkage, kelly.* class, prefix-safe). PromptBuilder gains **HARD CONSTRAINT 17** naming all six keys and stating the sibling `scoring.*` tunables stay proposable, with the enforced-in-code cross-reference matching the HC12–16 house pattern.

**Note for the spec-writer:** the applier's *generic* prefix-reject error message still reads "(HARD CONSTRAINT 11/12)" — it predates 13–17 and is shared across all prefixes. Left untouched (changing it risks fixture text elsewhere and buys nothing functional); A21b therefore asserts the stable `"off-tweaker-surface"` fragment, not the literal "HARD CONSTRAINT 17". If a future pass wants the message to enumerate honestly, that's a one-line polish item.

## §4. F3 — ruled D3 = keep snapshot, documented

Doc/comment path exactly as §4 specified: (a) a comment block at the `ExitGuardEvaluator` CalcOFI call site (ruling, rationale, bounded-twitchiness argument, pointer to the align-branch); (b) an amendment in `time-averaged-ofi-spec-back.md` §3, appended to the existing TAPE-strip ruling item, including the accepted strip-vs-row disagreement consequence and the observational-watch → align-branch fallback. **No code change.** The trader's live watch (strip EXIT latches the next run's HOLD\EXIT row doesn't corroborate) remains open; the §4 align-branch in the proposal is the ready-made follow-up if it reads as alarm fatigue.

## §5. Nits

- **N1 (`5a77f27`):** `DynamicNorms.Compute` gains `Optional utcHour As Integer = -1`; `ApplySessionVolume(n, utcHour)` uses it, `-1` ⇒ `DateTime.UtcNow.Hour` (every existing caller + harness fixture byte-identical — the A9-series norms fixtures pass untouched). `RunAnalysisAsync` passes its captured `utcHour` at the `Compute(candlesExec, r.ATR, utcHour)` call. `candles1m` → `candles` renamed throughout; grep confirmed zero `candles1m:=` named-argument call sites before the rename and zero `candles1m` occurrences after.
- **N2 (`552965b`):** `ScoringEngine_Helpers.vb` header priority list rewritten to match the code (1 micro fast-exit → 1.5 structural break → 2 ROC momentum-break → 3 OBV divergence → 4 RSI-div evaluate → 5 single-adverse warning → 6 RSI/ROC assessment), tagged as A17g-pinned. `DeribitIndicatorProject.md` §9 priority line corrected the same way; the two adjacent layer mentions (the Helpers file-table row and the Step-6 render note) were also drifted and were fixed — slightly beyond the spec's "§9" wording but the same defect.
- **N3 (`552965b`):** new row in architecture.md → *Display Behaviour Clarifications*: `mtf_gate.enabled: false` composes `MTF BLOCK [...]` while no block occurs; config-edge display quirk, tweaker-unreachable via `DisabledGatedPaths`, reason formats locked, do not change the code. No code touched.
- **N4 (`552965b`):** CLAUDE.md UI-table row, data-flow snippet, and the parity-rule sentence now name `BuildPlaintextSnapshot` (`UI/MainForm_PlaintextSnapshot.vb`) + `UI/MainForm_Render_Cards.vb` as the two surfaces and note the P5b deletion. architecture.md directory tree + the render section of the data-flow diagram rewritten to the P5b reality (snapshot-first ordering with the inline `CalcKellySizing` → `BindCardKelly` dependency called out). Surgical adjacent fixes in the same commit: the *Display Behaviour Clarifications* HOLD\EXIT row still cited `MainForm_Render_Header.RenderOutputHeader` (now cites the snapshot guard at `MainForm_PlaintextSnapshot.vb:136` + the mirrored card sentinel check); the tree entry for the deleted files' *other* former contents now points at `MainForm_Calibration.vb` / `MainForm_Layout.vb` (verified by grep); a readability note that `LogRun` actually runs just before the snapshot build. The Kelly data-flow parenthetical was corrected too (was: "called from RenderOutputHeader").

## §6. Acceptance evidence

- **A21a** (new): `Validate` against a post-v47 `regime_gates` mirror **rejects** `regime_gates.transitional_adx_penalty_low` with the unresolvable-path reason and **accepts** sibling `regime_gates.transitional_penalty_mid`. PASS.
- **A21b** (new): **rejects** `scoring.hold_rsi_hold_long` via the prefix fence, **accepts** `scoring.verdict_med_pct`. PASS.
- Both fixtures are self-contained inline-JSON mirrors (the A20g/h pattern) rather than reads of the live settings.json — deliberate: the harness stays working-directory-independent, and the mirror documents the post-change shape it guards.
- A1–A20i all unregressed on every run; display-parity check clean; CSV v0.7 / eval cache v4 untouched; scoring path behaviour-identical (F1/F2 values equal prior effective behaviour; N1 changes only the hour *source* within any given hour).

## §7. Coordinator review — ✅ APPROVED (2026-07-02, spec-author seat)

Independent verification, not a read-through: **re-ran the gate myself — GATE PASSED** (3 Release builds 0/0; A1–A20i unregressed + A21a/A21b PASS; display-parity clean; the version-bump guard correctly paired the engine-path change with the v47 bump). Diff-audited all four commits against the spec:

- **F1** — dead key absent from tracked + bin + POCO (removal comment cites v31 F8); repo-wide grep: zero live reads; correctly NOT in `RejectedPathFragments` (snapshot-poisoning rationale recorded in the v47 change_log, which I read in full — accurate on every claim); A21a proves the C-6 reject.
- **F2** — all 8 keys present in tracked at POCO-default values (`0.6/-0.6`, `60/40`, `40/60`, `1`, `1`), bin `version` synced 47, dead key gone from bin (verified on disk — the bin copy is untracked so no diff shows it). The integer `hold_rsi_*` serialisation deviation is accepted — it matches the bin copy, which is the point.
- **D4** — `"scoring.hold_"` prefix in the applier + PromptBuilder HC17 (names all six keys, siblings-stay-proposable, code-enforcement cross-ref); A21b proves it.
- **F3** — comment-only diff at the guard's CalcOFI call (ruling + bounded-twitchiness rationale + align-branch pointer); zero functional change.
- **N1** — diff reviewed line-by-line: `Optional utcHour = -1` with the `DateTime.UtcNow.Hour` fallback (existing callers/fixtures byte-identical), threaded to `ApplySessionVolume`, `RunAnalysisAsync` passes the captured hour, `candles1m`→`candles` rename clean.
- **N2** — comment-only; the corrected priority list matches the code as source-verified in the audit (and now includes the 1.5 structural-break layer the old comment omitted entirely — better than the spec asked).
- Scope-adjacent fixes (N2's two extra drifted mentions, N4's adjacent doc-rot) are the same defects the spec targeted — accepted. The §3 note (generic reject message still enumerates "11/12") is accepted as the pre-existing shared string; A21b correctly asserts the stable fragment.

No nits. Trader's test+push is the remaining gate.

## §7b. Open items handed back

1. **Trader:** live-test + push the 4 commits (`688127b`, `8cb5053`, `5a77f27`, `552965b`).
2. **Trader:** the F3 observational watch during holds; flip to the proposal §4 align-branch if alarm fatigue shows (contained follow-up, fixture recipe already in the spec).
3. **Next data-gated pass:** the OFI dominance re-baseline is now **v48** (D2). The v47 geometric collection running 2026-07-02 feeds it.
4. Optional polish (non-blocking, noted in §3 above): the applier's generic prefix-reject message enumerates only "HARD CONSTRAINT 11/12".
