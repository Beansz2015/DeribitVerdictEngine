# Doc scanner (harness 4) — pre-spec measurement, 2026-09-22 (UTC)

**Why this exists:** [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §5 — *measure BEFORE writing the spec*. Harness 3's spec was the first written that way and produced no factual defect in its premises. Harness 4's scope was one line: **version rot · identifier collision · cross-doc contradiction** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6).

**Method:** code-only candidate enumerators, **no Jev call**. Recall is measured by replaying them at the parent revision of four commits that fixed known doc rot; the lines each fix removed are the labelled positives. Precision is measured by hand-labelling today's candidates.

**Instrument (committed, re-runnable):** [`../tools/checks/measure/doc-scanner/`](../tools/checks/measure/doc-scanner/) — `enumerators.py` (the six enumerators), `replay_recall.py`, `scan_head.py`. Python 3, git only, no network.

**Seat labels, pre-registered:** [`harness-runs/doc-scanner-20260922T193818Z-prelabels.json`](harness-runs/doc-scanner-20260922T193818Z-prelabels.json) — 111 hand labels pinned to revision `cbc2c91`, written before any detector exists. A first run on the same (path, line, revision) items can use them without contamination.

---

## 0. The enumerators

| Kind | What code flags | Truth source |
|---|---|---|
| `E1_strong` | A settings version quoted in a "current" form (`settings.json (vNN)`, `Current version … vNN`, `\| Settings \| vNN`) below the live version | `settings.json` line 2 at that revision |
| `E1_weak_cue` | Any `vNN` below the live version on a line with a currency word (current · now · live · state · tracked…) | same |
| `E2_value_*` | A settings key name followed within 22 chars by a number that differs from the live value. Split: the number **was** once shipped / **never** was | flattened `settings.json`, plus every value over all its revisions |
| `E3_*` | A backticked file path absent from the tree · a `file.vb:NNN` past end of file · a `` `cfg.X.Y` `` whose last member exists in no `.vb` | `git ls-tree`, file contents |
| `E4_fixture_id_absent` | An `A\d+[a-z]` ID with no `Sub` or `Check("…")` title in `verify/ordercheck/Program.vb` | `Program.vb` |
| `E5_stale_state_section` | A dated state header (*state snapshot · state banner · as of · verified in the tree* + date) older than 7 days; lines under it inherit | commit date |
| `E6_stale_current_pointer` | A "current / state read / start here" line linking a seat handover older than the newest | handover filenames |

⚠ **Two script bugs were caught during the measurement and fixed before any number below was taken:** `E4`'s ID map missed IDs that live only in a `Check("…")` title (it undercounted `A71a`, `A82c`, `A13a`); and a patch script wrote a literal backspace byte into the `E4` regex — the control-byte trap already in this repo's memory. Both are fixed in the committed copy.

---

## 1. Population

| | Measured |
|---|---|
| Top-level `docs/*.md` | **420**, 8,079,937 B |
| Size p50 · p90 · p99 · max | 13.5 KB · 32.6 KB · 70.9 KB · **348 KB** (`history-archive.md`) |
| Over Jev's 32k-token state limit | **5 docs**, at 2.29 B/token — measured this session on `DeribitIndicatorProject.md` (70,901 B, 30,906 tokens, by the `Read` tool) |
| Archives excluded from the scan | 20 files (`*archive*`, `docs/archive/`, `docs/harness-runs/`) |

⛔ **Whole-doc and doc-pair states are out.** 420 docs make ~88,000 pairs, and five docs do not fit one state. **Candidates must be anchored at line level by code** — the programme's standing shape: code enumerates and computes truth, Jev judges, code decides.

---

## 2. ⭐⭐ The finding that shapes the spec: SCOPE decides precision

Scan of today's tree (`cbc2c91`, 417 non-archive `.md`): **2,113 distinct flagged lines.** Split by a **living set** — the 12 docs that assert *current* state:

`CLAUDE.md` · `DeribitIndicatorProject.md` · `architecture.md` · `trader-profile.md` · `trader-tick-queue.md` · `roadmap.md` · `backlog-dependency-map.md` · `csv-rotation-riders.md` · `harness-shadow-mode-protocol.md` · `UserManual.md` · `aws-collector-deploy-checklist.md` · the newest seat handover.

| Kind | Living (12 docs, 646,541 B) | Rest (405 docs) |
|---|---|---|
| `E1_strong` | 3 | 5 |
| `E1_weak_cue` | 90 | 398 |
| `E2_value_was_shipped` | 22 | 107 |
| `E2_value_never_shipped` | 13 | 121 |
| `E3_missing_file` | 93 | 789 |
| `E3_cfg_member_missing` · `E3_line_past_eof` | 0 · 0 | 5 · 19 |
| `E4_fixture_id_absent` | 9 | 62 |
| `E5_stale_state_section` (+ inherited lines) | 12 (+208) | 29 (+402) |
| `E6_stale_current_pointer` | 4 | 2 |

⭐ **Every `E1_strong` hit outside the living set is in a dated historical doc** (two old handovers, two spec-backs, one implementer handoff) — correct when written, and declared historical by its own date. **Rot in a record is not a defect; rot in a doc that claims to be current is.** The living set is 8 % of the bytes.

⛔ **And inside the living set, fixed rot survives as a QUOTED copy in its own fix note.** All 3 living `E1_strong` hits are fix notes (*"it read 'App version: settings.json v54'…"*). The detector must be told a quoted past value is history.

---

## 3. Recall — replayed at each fix's parent revision

**Handle `H-1`, run this session:** `PYTHONIOENCODING=utf-8 python tools/checks/measure/doc-scanner/replay_recall.py`

```
=== fix 5c7c178 (replay at 5c7c178^)
  removed content lines: 32, flagged: 28; candidates on NON-removed lines: 272
=== fix 9c447e1 (replay at 9c447e1^)
  removed content lines: 6, flagged: 3; candidates on NON-removed lines: 69
=== fix 5b8515e (replay at 5b8515e^)
  removed content lines: 1, flagged: 1; candidates on NON-removed lines: 57
=== fix 2f46679 (replay at 2f46679^)
  removed content lines: 1, flagged: 0; candidates on NON-removed lines: 12
```

⚠ **"Flagged" over-counts: some hits flag a different defect on a line that was wrong for another reason.** By shape, reading each removed line:

| Shape of the fixed rot | Positives | Caught for the right reason |
|---|---|---|
| Settings version quoted as current (`v31`, `v64`, `v17`, `v65`, `v54`) | 5 | **5** — `E1_strong` |
| "Current handover" pointer to a superseded one | 1 | **1** — `E6` |
| A `cfg.` member that never existed (`WidePenaltyThresholdBps`) | 1 | **1** — `E3_cfg` |
| Rows of a dated state snapshot (`roadmap.md` §2, 2026-08-07, replayed 17 days on; its `Settings` row is counted above) | 16 | **14**, plus 2 only through the 40-line inheritance window — `E5` |
| A fixture ID quoted with the wrong meaning (`A56b` as the value-copy guard) | 1 | **0** — semantic |
| File-size / token-count claims | 2 | 0 |
| A doc contradicting itself (`line 1` vs `line 2`) | 1 | 0 |
| A block-list count claim (11 blocks, really 17) | 1 | 0 |
| A forward-looking claim gone stale (*"WebSocket is the highest-impact next upgrade"*) | 1 | 0 |
| A state banner superseded by EVENTS, not age (1 day old) | 4 | 0 |
| Queue status rows (`trader-tick-queue.md` §2, `DeribitIndicatorProject.md` §12) | 7 | 0 |

**Totals: 40 labelled positives; 21 caught for the right reason, 2 more only through the inheritance window, 17 missed.**

⭐ **Code catches every shape that has a mechanical source of truth and misses every shape that does not.** The misses need either code-supplied evidence (the 2026-09-21 pilot, [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4: *text alone 1 of 8*) or a semantic judge.

⚠ **Both directions:** on the same five pre-fix files, the enumerators also flag **272 lines that the fix left alone.** Recall without that number would be the one-sided count this repo's memory warns against.

---

## 4. Precision — hand labels on today's living set

**Handle `H-2`, run this session:** `REV=cbc2c91 OUT_DIR=<dir> PYTHONIOENCODING=utf-8 python tools/checks/measure/doc-scanner/scan_head.py` → reproduces §2's table exactly.

| Kind | Labelled | True | False | Unsure | Why the false ones are false |
|---|---|---|---|---|---|
| `E1_strong` | all 3 | 0 | 3 | 0 | Fix notes quoting the old value |
| `E1_weak_cue` | 20 of 90 (seeded sample) | 0 | 20 | 0 | Historical mentions (*"v21 semantic rewrite"*, *"SHIPPED v54"*) |
| `E2_value_was_shipped` | all 20 lines | **11** | 8 | 1 | History narratives (*"18.0 to 23.0"*) |
| `E2_value_never_shipped` | all 13 lines | 0 | 13 | 0 | **Parser noise** — a number paired with the wrong key |
| `E3_missing_file` | 20 of 85 (seeded sample) | 0 | 20 | 0 | Runtime files (`analysis_log.csv`), deliberate deleted-file mentions, future docs |
| `E4_fixture_id_absent` | all 8 lines | 0 | 8 | 0 | All `A54a` — a queue-item name in the fixture-ID shape, no wrong referent |
| `E5_stale_state_section` | all 12 headers | **4** | 3 | 5 | Dated examples, "previously closed" framing, move records |
| `E6_stale_current_pointer` | all 4 | **1** | 2 | 1 | Explicit "PREVIOUS" or "standing rules" pointers |

⭐⭐ **`E2` splits exactly along a TENSE question.** Every true `E2` hit is a line stating a superseded value as current; every false `was_shipped` hit narrates history. **That is the Jev question** — code computes the mismatch and the shipped history, Jev judges *"does this line assert the value as CURRENT?"*, code decides. `never_shipped` hits are parser errors: fix the pairing in code, never send them to a judge.

---

## 5. ⛔ Live positives found — unfixed, pending a ruling

| Where | What | Kind |
|---|---|---|
| `DeribitIndicatorProject.md` §4, Tier 2 OFI row | `BuyDominantRatio (2.0) / SellDominantRatio (0.5)`; shipped **1.6 / 0.625** since v48 | `E2` |
| `UserManual.md`, ten lines under current-feature sections | RSI `DivergenceRsiDelta=2.0` (live 5.0) · OBV `trend_gate (0.001)` (live 23.0) · OI `ChangeThresholdPct (0.01)` (live 0.002) · OFI 2.0 / 0.5 ×2 · CVD `slope_pct_of_value (0.01)` (live 0.1) · the four funding thresholds (0.0003 / 0.00005, live 8e-05 / 1e-05) | `E2` |
| `trader-tick-queue.md` §0 authority table | Names `seat-handover-2026-09-17.md` *"THE current seat handover - the STATE read"*; the same doc's banner names `seat-handover-2026-09-22b.md`. ✅ **FIXED 2026-09-22 (UTC), trader-ruled** — the row now points to the banner and names no file. The other five stay unfixed as known positives for the first run | `E6` |
| `roadmap.md` state snapshot | *"Next free fixture family: A73 as of 2026-09-10"*; `Program.vb`'s highest family is **A85**. **The same row was corrected once before for exactly this** | `E5` |
| `roadmap.md` and `backlog-dependency-map.md` | `A5`: *"28 of 30 dates as of 2026-08-07, ~2 dates out"*, 47 days on — and the map says it *"carries no state"* | `E5` |
| `roadmap.md` §2 | The whole state snapshot is dated 2026-08-24, 30 days old | `E5` |

✅ **RULED 2026-09-22 (UTC), trader: fix the `trader-tick-queue.md` pointer now (it can misdirect a seat); keep the other five unfixed as known positives for harness 4's first run.** ⛔ **Do not fix those five before that run** — they are the only current rot with a pre-written seat label (`harness-runs/doc-scanner-20260922T193818Z-prelabels.json`).

---

## 6. ⭐ What the spec should inherit

1. **Scope to the living set, named in a D-table.** Historical records are out. The list above is a judgment and should be ruled.
2. **Lead with `E2`** (value quotes against `settings.json`). Highest yield, a clean ground truth, and a single tense question for Jev. Fix the number-to-key pairing first — `never_shipped` is 100 % noise.
3. **Keep `E1_strong` and `E6`, with the same tense question.** Low yield today, but they caught every historical instance of their shape. A quoted past value in a fix note must read as history.
4. **Turn `E5` into typed claims with code-computed truth** where one exists (next free fixture family → the highest ID in `Program.vb`; current handover → the newest filename). Report the rest by age, without a judge. Age alone is 4 true of 7 decided.
5. **Keep `E3_cfg_member_missing` and `E3_line_past_eof`; drop `E3_missing_file`** (0 of 20).
6. **Replace `E4`'s absent-ID check with the `A56b`-shaped check:** give Jev the doc line plus the fixture's `Sub` name and `Check` title, and ask whether the line describes that fixture. **65 mentions in the living set**, against 2,763 across all docs — scope again.
7. **Name what v1 will NOT see:** a doc contradicting itself, forward-looking claims, state superseded by events rather than age, and file-size claims. The first three need evidence code does not yet gather.
8. **Every rule from harnesses 1–3 carries over:** baseline refusal, item-level baseline refusal (`FP-D24` in `tools/checks/fixture-parser.ps1`), 5-sample self-consistency, a separate acceptance window, and — from today — **a stable row is not a correct row** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4g).

---

## 7. ⚠ What this measurement does NOT establish

- **Detector accuracy.** No Jev call was made. The numbers bound what code can enumerate, not what a judge will get right.
- **The labels' reliability.** One seat's reads; 11 are `unsure`. Two kinds are samples of 20 (fixed seed 20260922).
- **`E5`'s 208 inherited living lines.** Only the 12 headers were labelled.
- **`E2`'s own recall.** It pairs a key with a number within 22 characters after it; a value quoted further away, or before the key, is invisible.
- **Generalisation.** The labelled positives are biased toward the session-start read set, because that is what the fix commits touched — which is also the living set.
- **The `UserManual.md` true positives** were checked against the live `settings.json` values and each line's section heading, not against the manual's full intent.
