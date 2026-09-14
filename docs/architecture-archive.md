# Architecture Archive

Historical and superseded text moved out of [`architecture.md`](architecture.md). Not current state.

---

## A. Trimmed from `architecture.md` (2026-09-14)

Moved verbatim by the 2026-09-14 context trim. Each block sits between `begin`/`end` markers and is byte-identical (LF form) to the stated line range at git tag `doc-trim-2026-09-14-pre`. Ledger and hashes: [`doc-trim-log.md`](doc-trim-log.md). Re-check with `tools/checks/doc-trim-verify.ps1`. **History only - not current state.**

<a id="trim-2026-09-14-52"></a>
### trim-2026-09-14-52 - Directory Layout - the April-era docs/ subtree listing

Source: `docs/architecture.md` lines 361-376 at git tag `doc-trim-2026-09-14-pre` - 1193 B - SHA-256 `fa9fae9120b69cdebcce66cf3c76e0152e3004df9b80ef6d96237765d6318f4a`.

<!-- trim-2026-09-14-52 begin -->
└── docs/
    ├── DeribitIndicatorProject.md      Authoritative handover document (read first)
    ├── architecture.md                 This file
    ├── trader-profile.md               Trader style, preferences, collaboration rules
    ├── verdict-context-tag-proposal.md Spec: Verdict Sub-Context Tag — IMPLEMENTED
    ├── kelly-criterion-proposal.md     Spec: Kelly Criterion sizing — IMPLEMENTED
    ├── bid-ask-spread-proposal.md      Spec: Bid-ask spread signal — IMPLEMENTED
    ├── ofi-momentum-proposal.md        Spec: OFI Momentum — IMPLEMENTED
    ├── dynamic-microcvd-accel-proposal.md  Spec: Dynamic MicroCVD — IMPLEMENTED
    ├── vpfr-lite-v2-proposal.md        Spec: VPFR-lite v2 — IMPLEMENTED
    ├── swing-pivot-proposal.md         Spec: Swing pivot detection — IMPLEMENTED
    ├── settings-exposure-pass-proposal.md  Spec: Settings exposure pass — IMPLEMENTED
    ├── bbw-scoring-proposal.md         Historical
    ├── bbw-scoring-response.md         Historical
    ├── dual-scoring-fix-proposal.md    Historical
    └── dual-scoring-fix-response.md    Historical
<!-- trim-2026-09-14-52 end -->

<a id="trim-2026-09-14-53"></a>
### trim-2026-09-14-53 - Settings Data Flow - the v30 session-bucket values quoted in the diagram

Source: `docs/architecture.md` lines 624-625 at git tag `doc-trim-2026-09-14-pre` - 145 B - SHA-256 `469e7fb60e5861f000bb7c0fd6584fc61b63f7f9b5f7f2f305b3666f9d64a893`.

<!-- trim-2026-09-14-53 begin -->
    │         Current settings.json populates ASIA (00–07, 0.80/0.85),
    │         LONDON (08–12, 1.00/1.00), NY (13–23, 1.15/1.10).
<!-- trim-2026-09-14-53 end -->

<a id="trim-2026-09-14-54"></a>
### trim-2026-09-14-54 - Design Decisions row - MainForm_Render split into _Header + _Sections (files deleted in P5b)

Source: `docs/architecture.md` lines 682-682 at git tag `doc-trim-2026-09-14-pre` - 241 B - SHA-256 `fbb745a6181c0cb32f5b9bdcfdf25adba21e21acf49ab96470b5fa49638617ae`.

<!-- trim-2026-09-14-54 begin -->
| MainForm_Render split into _Header + _Sections | MainForm_Render.vb exceeded 28 KB. RTF helpers + top render block (verdict/ATR/Kelly) in _Header.vb; RenderOutput() entry point + all indicator sections + breakdown table in _Sections.vb. |
<!-- trim-2026-09-14-54 end -->

<a id="trim-2026-09-14-55"></a>
### trim-2026-09-14-55 - Design Decisions row - v15 cleanup pass (historical audit)

Source: `docs/architecture.md` lines 684-684 at git tag `doc-trim-2026-09-14-pre` - 665 B - SHA-256 `f90afd2951b48f1542978de60ca50fe6f1a96fb47538fde820fb992d72a480f5`.

<!-- trim-2026-09-14-55 begin -->
| v15 cleanup pass | Source of truth audit. Removed dead fields (`OI_Prev15m` / `OI_Prev60m` / `ATRAvg20d`), three unused `DynamicNorms.StaticVol*` properties, an entire `Ema200Settings` class, 13 silently-ignored config properties, the dead `ScoringEngine.MaxScore` const, the unused `SettingsLoader.Reload()`. Aligned remaining default values with v14 calibration (so an absent `settings.json` doesn't seed stale defaults). Two display-only colour bugs fixed in `MainForm_Render_Sections` (BBW status compared against `"SQUEEZE"` instead of `"ACTIVE"`; TTM direction compared against `"UP"` / `"DOWN"` instead of `"RISING"` / `"FALLING"`). Zero scoring impact. |
<!-- trim-2026-09-14-55 end -->

<a id="trim-2026-09-14-56"></a>
### trim-2026-09-14-56 - Design Decisions row - Settings exposure pass (spec 6), whose method-default pattern A54a later removed

Source: `docs/architecture.md` lines 693-693 at git tag `doc-trim-2026-09-14-pre` - 557 B - SHA-256 `995b30a06b2da312c4863dd03aa8a9856587fe6511548fba52348af6a269c735`.

<!-- trim-2026-09-14-56 begin -->
| Settings exposure pass (spec #6) | Exposing 19 formerly-hardcoded literals to settings.json completes the auto-tweaking audit prerequisite (Section 16.3 item 2). All defaults are exactly the previously-hardcoded values — zero behaviour change. The new Optional params on CalcBBW, CalcTTMSqueeze, CalcCVD, CalcDonchian match the existing pattern (cfg value passed at call site; default value in method signature for caller convenience). RegimeMaxScore() and TierFloor() now take cfg and read from POCO fields rather than returning hardcoded constants. |
<!-- trim-2026-09-14-56 end -->
