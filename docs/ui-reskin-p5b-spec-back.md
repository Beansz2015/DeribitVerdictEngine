# UI Reskin P5b — Spec-Back

**Date:** 2026-06-13
**Implementer:** Fable 5 (commits) → Opus 4.8 (verification + this report, after a mid-run usage-limit model swap)
**Kickoff:** `docs/ui-reskin-p5b-kickoff.md`
**Predecessor sign-off:** trader approved the 55-case visual review + consolidated fix (`ef3bf61` / `caefa3d`), then signalled go for cleanup + P5b.
**Commits (local, unpushed):**

| Hash | Subject | Δ |
|---|---|---|
| `7f9f551` | chore(test): P5-test cleanup — remove render-parity harness | 4 files, −2005 |
| `24f1810` | chore(ui-reskin): P5b — delete legacy RTF pipeline + verification dump card | 6 files, +65 / −735 |
| `73504ed` | chore(ui-reskin): P5b follow-up — drop stale BuildPlainSectionHeader comment ref | 1 file, +3 / −3 |

Status: **UI reskin complete.** Awaiting trader live-data test → push. Spec C (SC/TOTAL parity) is the next discrete piece, separate from the reskin arc.

---

## 1. Commit 1 — P5-test cleanup

Per gap-fix proposal §9.3. Deleted:

- `UI/MainForm_TestHarness.vb` (scaffolding + `TestCaseBuilder` + artifact writer)
- `UI/TestHarnessCases.vb` (55-case library + `SignalBreakdownPresets`)
- The `Ctrl+Shift+T` `ElseIf` in `MainForm_Layout.OnFormKeyDown`
- `tools/send-ctrl-shift-t.ps1`

No `_testHarnessMode` guards existed outside the harness files (handover §5.11 confirmed). The `Ctrl+Shift+S` full-form screenshot hotkey was left intact — it shares `OnFormKeyDown` but is independent dev tooling, not harness-coupled. Build clean.

---

## 2. Commit 2 — P5b deletion sweep

### 2.1 Deleted
- `UI/MainForm_Render_Header.vb` (287 lines — `RenderOutputHeader` + `AppendRtf`/`AR`/`SectionHeader`/`Divider`)
- `UI/MainForm_Render_Sections.vb` (329 lines — `RenderOutput` + 13 indicator section blocks + `lblVerdict` writes)
- `RenderOutput(...)` call + six `AppendRtf(txtOutput, ...)` writes in `MainForm_Analysis.vb` (fetching / error / 3× skip / clear)
- Verification dump card: `_cardVerificationDump` field, its `AddRow`, and `ReparentVerificationDumpControls()` in `MainForm_Layout.vb`
- `BuildPlainSectionHeader` definition

### 2.2 Behaviour changes
- **ERROR path** → `MessageBox.Show` (kickoff §3.2.1). The `btnAnalyze_Click` catch no longer writes RTF.
- **Skip path** → no text write; surfaced by the SKIPPED verdict panel (P4f) + LOG skip counter (P4e), as the kickoff anticipated.

### 2.3 Kelly hoist (kickoff §3.2.2) — solved more cleanly than prescribed
The kickoff prescribed *adding* an explicit `ScoringEngine.CalcKellySizing` call in `RunAnalysisAsync` between `Calculate(...)` and `BindCardKelly(...)`. **That would have created a second live invocation, violating hard requirement (b) "exactly one `CalcKellySizing` invocation survives (the snapshot path's)."**

Instead: `BuildPlaintextSnapshot` already calls `CalcKellySizing` internally (`MainForm_PlaintextSnapshot.vb:144`). I **hoisted the snapshot build to before the card-bind block** in `RunAnalysisAsync` (it previously ran after, near the dump append). The snapshot's inline call now populates `v.Kelly*` ahead of `BindCardKelly`. One invocation, requirement (b) satisfied, and the dump still receives the same string (now built earlier, appended in the same place after `UpdatePerformanceLabels`).

Confirmed by grep: the only surviving `CalcKellySizing` **call** is `MainForm_PlaintextSnapshot.vb:144` (others are the definition in `ScoringEngine_Kelly.vb` + comments).

### 2.4 Helper consolidation — 3 call sites, not the kickoff's 4
Kickoff §3.5 expected four `BuildPlainSectionHeader` call sites including KELLY SIZING. Actual tree: **three** (`VOLUME PROFILE`, `OI × CVD CROSS`, `INDICATOR DETAILS`). The KELLY card header migrated to `BuildCardHeaderWithTags("KELLY SIZING", biasTag, capTag)` during the Tier-D / consolidated-fix work — it no longer used `BuildPlainSectionHeader`. All three real sites converted to `MakeSectionHeader` (identical 11pt/`FG_SECONDARY` output); definition deleted.

**Follow-up `73504ed`:** the post-`24f1810` orphan sweep found one *stale* comment naming the deleted helper as a still-live renderer — `UI/Controls/SectionGroup.vb:74` (`rendered by MakeSectionHeader / BuildPlainSectionHeader`). Trimmed to `MakeSectionHeader` only. Comment-only; no API/paint/behaviour change, so the `UI/Controls/*.vb` lock (which guards control *design*, not stale doc text) is not implicated — and the trader explicitly authorized the touch. The only remaining mention of `BuildPlainSectionHeader` tree-wide is the intentional consolidation note at `MainForm_Render_Cards.vb:39` (`…absorbed BuildPlainSectionHeader`), which accurately records that the helper is gone — deliberately kept, not stale.

### 2.5 Locked carve-out — `MainForm.Designer.vb` NOT touched
Confirmed: zero edits to `Designer.vb`. `txtOutput` and `lblVerdict` remain declared + `InitializeComponent`-instantiated zombies. Set `txtOutput.Visible = False` in `ApplyControlThemes` (it's no longer reparented into any card, so without this it would paint at its Designer position). `lblVerdict.Visible = False` was already set in `ReparentHeaderStripControls`; its last writer (legacy `RenderOutput`) is now gone, comment updated.

---

## 3. Kickoff staleness findings (re-grepped every anchor, per the instruction)

The kickoff was ~3 weeks stale; v31/v32/consolidated-fix had since reshaped the render files. Delete-list *intent* governed, not line numbers.

1. **P/Invoke surface absent.** Kickoff §3.3 expected `DllImport` / `EM_SETMARGINS` / `SendMessage` / `RECT` / `SetOutputMargins` / `OnFormHandleCreated` / `OUTPUT_CHARS` / `OUTPUT_LINES` / `SizeToContent` to delete. **None present as code** — already removed in an earlier reskin phase (P4a's `ApplyInitialFormSize` superseded `SizeToContent`). Only a historical mention of `SizeToContent` survives in a `MainForm_Layout.vb` header comment describing what P4a replaced; left as-is (accurate history, not dead code). Nothing to delete here.
2. **`OnFormHandleCreated` — N/A.** No such override exists. No resolution needed.
3. **Line numbers all drifted.** The `AppendRtf`/`RenderOutput`/skip-write sites were at different lines than §3.2's table; located by grep and removed by intent.
4. **Kelly call signature** is 4-arg (`v, atrStop, r.CurrentPrice, cfg`) — the `r.CurrentPrice` (inverse-contract) arg was added post-kickoff. Hoist used the real signature.

---

## 4. Verification

- **Build:** `dotnet build` clean, **0 warnings / 0 errors**, after both commits.
- **Self-screenshot loop** (real Deribit data, auto-run active):
  - Full form renders top-to-bottom; **verification dump card is gone** — layout ends at SETTINGS & TOOLS (full extent 1100 × 2794 px; ~400 px shorter than P5a as expected).
  - VERDICT / ATR ENTRY LEVELS / STRUCTURAL ×2 / SIGNAL BREAKDOWN render with no regression vs the bound-card baseline.
  - **KELLY card non-zero** (the §3.2.2 gate): p(win) 45.0%, f*/Half-Kelly 12.00%/6.00%, Applied 5.00%, Risk $50.00, Lean 500 contracts, Notional ≈ $5,000 · 5.0× lev [LEV CAPPED]. Confirms the hoist populates `v.Kelly*` before the bind.
  - INDICATOR DETAILS binds (DYNAMIC NORMS [LIVE] / REGIME header visible).
- **Dump file:** `bin/Debug/net8.0-windows/analysis_output_dump.md` updated this run; full snapshot body present (header → DYNAMIC NORMS → … → MTF GATE → FUNDING → SIGNAL BREAKDOWN + TOTAL). Sourced from `BuildPlaintextSnapshot`, requirement (a) satisfied. (Pre-existing UTF-8/codepage display artifacts when read via PS 5.1 `Get-Content`; bytes unchanged by P5b — the builder's content wasn't edited.)

Not exercised (no GUI-forced path this run): the `MessageBox` error branch — deferred to trader live testing per kickoff §7.4. Auto-run timer blocking, if disruptive, is a separate transient-Pill spec.

---

## 5. Parity rule now binds snapshot ↔ card

With the legacy renderer deleted, the hard rule (CLAUDE.md Collaboration Rules + handover §5.14) now applies to **snapshot ↔ card**: any engine change to a line emitted by `BuildPlaintextSnapshot` MUST update the matching binding in `MainForm_Render_Cards.vb` in the same commit, or state why no card surface is affected. Noted in the `24f1810` commit message. The text-parity harness that previously diffed legacy↔snapshot is gone (commit 1), so this rule is the sole guard against the third-surface drift class — enforce by reading, not by a gate.

---

## 6. Reporting-back checklist (kickoff §7)

1. **LoC delta** — commit 1 −2005 (harness); commit 2 +65 / −735 (sweep). Net additions in commit 2 are the MessageBox block + reframed comments.
2. **P/Invoke present** — none (see §3.1).
3. **`OnFormHandleCreated`** — does not exist (§3.2).
4. **MessageBox live behaviour** — not yet observed; trader verification window is definitive.
5. **Helper consolidation** — `BuildPlainSectionHeader` deleted; **3** call sites converted (not 4 — KELLY moved to `BuildCardHeaderWithTags` earlier; §2.4). One stale comment cleaned in follow-up `73504ed`; the only remaining mention tree-wide is the intentional "absorbed" consolidation note (§2.4).
6. **`MainForm.Designer.vb`** — NOT touched. Verifiable in the `24f1810` ledger.
7. **The reskin is done.** After P5b the UI reskin arc is complete. Phase 6 (Spec C — SC/TOTAL parity) is the next discrete work.
