# Seat handover — 2026-08-24

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) — superseded for STATE, but it remains the AUTHORITATIVE DETAIL for everything summarised here.** This document deliberately does not repeat it. Where a section below says "see §N", that is §N of the 08-23 document.

**Settings: v68. Master `4b384ad`, pushed and in sync.** Verify with `git status -sb` — never inherit a push state.

⚠ **ALL DATES AND TIMES HERE ARE UTC.** The machine is GMT+8. This project has made two false collection-gap claims from that confusion, one formally withdrawn. **Check now-in-UTC before asserting any gap.**

---

## 0. ⭐ FIRST TASK — there isn't one. Nothing is blocked.

**Every thread opened in the 2026-08-22 → 08-24 session is closed.** That is unusual for this project and worth stating plainly rather than leaving a reader hunting for the catch.

**Two items remain, and both are TRADER DECISIONS, not builds.** Neither blocks anything. Neither should be started without a ruling — §2.

⛔ **Do not invent work from the two latent defects below.** Both have a destructive or contested obvious fix, which is exactly why they are decisions.

---

## 1. State — verified 2026-08-24 ~10:06 UTC, not inherited

### 1.1 Production collector

| | |
|---|---|
| Instance | **`i-0d6c133058876273e`**, t2.micro, Server 2019, `C:\DeribitEngine` |
| Build | **v68 + the `rbRepeat` fix**, exe built 2026-08-22 15:27Z |
| Book | **26,321 rows**, first row `2026-07-22 16:24:54` (production's carried history, intact) |
| Cadence | **39.3 rows/hour** over the last ~19.6 h; gap now-lastrow **1.8 min** |
| Uptime | **1 d 18 h — no restart since the cutover** |
| WS health | **zero DEGRADED / DOWN since 2026-08-22 16:03Z** — 42 hours clean |
| Memory | `pagesOUT/s` **0.0**, ~125–139 MB available sustained |

### 1.2 The old collector — GONE, lives as an AMI

**`i-08c740e22d507667d` was terminated 2026-08-24** after it served as the §0.4 test target. Its volume went with it.

```
AMI            ami-0247c8b7275de49ac   available, standard tier
Backing snap   snap-0b5c126dd898c6bd0  completed, 30 GB
```

⛔ **[`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §8 is REQUIRED READING before relaunching it.** It carries the launch parameters — subnet, security group, key pair, and above all the **`EC2-SSM-Access` instance profile, without which a relaunched box has no SSM at all** — which are otherwise unrecoverable, because a terminated instance ages out of `describe-instances` within about an hour.

⚠ **`snap-0b5c126dd898c6bd0` backs the AMI. Deleting it silently breaks `ami-0247c8b7275de49ac` while leaving it listed as `available`.**

**Tier ruled 2026-08-24: keep it STANDARD.** Archive tier is ~4× cheaper but imposes a 90-day minimum and a 24–72 h restore, which defeats the point of a disposable target available on short notice. Revisit only if months pass unused.

### 1.3 Local backstops

- **`C:\Dev\collector-cutover-2026-08-22\production-full-archive\`** — 28 files, **112,328,075 bytes, hash-verified twice** (once on capture, once immediately before the terminate). Holds what the migration deliberately did *not* carry.
- Same parent directory also holds two verified pre-cutover fetches and the hash-verified `final\` set.

---

## 2. ⚠ The only two open items — both need a RULING, not a build

### 2.1 The book's `Timestamp` column is LOCAL time

`UI/MainForm_Analysis.vb:613` — `verdict.Timestamp = DateTime.Now`. **Full write-up: [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §7, including the ranked grep taxonomy in §7.6.**

**Why it needs a ruling rather than a fix:** on a UTC host the change to `DateTime.UtcNow` is byte-identical in the rendered CSV — cheap now, a visible discontinuity later. **But `DateTime.Kind` moves `Local` → `Utc`, and `LivePerformanceTracker`, the eval-cache walk and `AnalysisLogger` all need checking first.** Rendered-output equivalence is *not* sufficient evidence — that is the same reasoning error the one-row acceptance gate made. It also changes what is persisted to the book, which is a data-model decision.

⛔ **Standing constraint until ruled: collector hosts run UTC.** Verify `TZ_ID` on any new box before it collects a row. ⚠ On Windows the `TZ` environment variable is **inert** (verified) — the timezone comes from the registry. On Linux it *is* honoured, which is why the CLI port makes this live.

### 2.2 `_evalCache` is UNBOUNDED and now has a clock

**Confirmed from source** (`LivePerformanceTracker.vb:118`; no `Remove`/`Clear`/`Take` of any kind). **Full write-up: [`trader-tick-queue.md`](trader-tick-queue.md) state banner and [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §6.**

**The code did not change — the box did.** [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §4's "years of headroom" was measured against ~496 MB free on a 2 GB t3.small. Production is now a 1 GB t2.micro with ~150 MB free.

- **Measured:** the eval-cache FILE grows **0.29 MB/day**.
- ⚠ **NOT measured:** the in-memory rate. Four production readings of app private ran **74.3 → 90 → 71.1 → 96 MB** — noisy, no trend. **Do not derive a date from them.** On a 1×–2× multiplier the runway is roughly **8–17 months**.

⛔ **THE LANDMINE:** `WriteEvalCache` rewrites the **entire** file with `append:=False` from **five** call sites. **"Just trim the list" truncates `analysis_eval_cache.csv`** — the file Kelly and F1 both read. **Decoupling the list from the file IS the work, and it comes first. No spec exists.**

---

## 3. What was proven this session — pointers, not repetition

All detail is in [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md).

| Item | Where | Status |
|---|---|---|
| Collector migration, steps 1–8 | §1, §1.4 | ✅ closed. **Zero tape loss across the cutover** (`trade_seq` MISSING=0) |
| v68 auto-run fired once then stopped | §2 | ✅ fixed (`9e9fe33`), proven live |
| Acceptance gate required only ONE row | §3 | ✅ fixed (`9eb3329`), proven **both directions** by a V1/V2 pair |
| Rollback restored over a running app | §0.1–§0.3 | ✅ fixed (`30ca04d`) |
| Rollback verification / `robocopy /MIR` | §0.5 | ✅ **executed successfully for the first time ever** |
| Old box retirement → AMI | §8 | ✅ complete |
| Hostel-app co-location assessment | [`hostel-app-colocation-assessment.md`](hostel-app-colocation-assessment.md) | ⏸ **owner's call on shared fate.** Both sides converged: it fits, and if it ever needs a resize it should not be done at all |

---

## 4. ⚠ Lessons that transfer — every one cost something

1. ⛔ **A checker that reports success it never performed.** **Five instances in one session**, four of them mine: the v68 acceptance gate, a harness where `R` was shadowed by the `Invoke-History` alias, an archive verifier that printed `all 0 files`, a flatten test whose input array was empty, and `schtasks /run` returning `SUCCESS` while launching nothing. **The fix is always the same: assert the check RAN** — throw on a zero or short item count. "It didn't complain" is not "it passed."
2. ⚠ **A background task's reported exit code is the LAST command's.** Bit twice: a deploy notification said "exit code 0" while the output held `EXIT=2`; a waiter said "exit code 0" while holding `WAIT_EXIT=255 — Max attempts exceeded`. **Read the output, never the summary line.**
3. ⭐ **Execution finds what parsing cannot — now EIGHT for eight in `collector.ps1`.** FIX 1, 6, 7, 8, 10, the rollback file-lock, the two-instance defect, and the sessionless-launch false success. **None was reachable by reading.** Do not accept "it parses" or "I traced it" for that file.
4. ⚠ **A ready explanation for a surprising number is a warning sign.** The tape grew 54 KB where 270 KB was predicted; the tidy answer was "gap repair failed"; `trade_seq` returned **MISSING=0** and the market had simply been quiet. **Two points is not a trend, and one point is not either** — a `pagesOUT/s` alarm was half-written before a third reading refuted it.
5. ⭐ **The observer effect is real on a 1 GB box.** A 59–77 MB probe on a box with ~93 MB free *caused* the paging it measured. Discount the probe's own footprint before quoting headroom.

---

## 5. Loose ends — neither urgent, neither mine to close unilaterally

- ⚠ **A stray git worktree** at `.claude/worktrees/compassionate-lamarr-b0e155` (detached HEAD `9b1718c`). Gitignored and harmless, but it **double-hits every `grep -rn --include=*.vb`** — it cost time twice this session. **I did not remove it because it may hold someone's uncommitted work.** `git worktree list` shows it; `git worktree remove` closes it once someone confirms it is dead.
- **Cosmetic:** `tools/checks/verify-gate.ps1:144` prints *"no engine-path change"* on commits that change engine behaviour. Its prefixes are `Core/`, `DynamicNorms.vb`, `analysis/` — `UI/` is excluded **correctly**, because the check is a settings-version-bump nudge and a new key must touch `Core/Settings/EngineSettings.vb`. **The logic is sound; only the message overclaims.**
- **Older trader decisions, untouched:** the absorption D-table ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 — attack `pullFrac` first, not the anchors) and the CLI-port reversal not yet written into [`roadmap.md`](roadmap.md).
