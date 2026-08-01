# `settings.local.json` overlay — spec-back

**Date:** 2026-08-02 · **Build:** `291457c`, local, **unpushed** (trader tests and pushes).
**Spec:** [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md) · **binding corrections:** [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) + §1 of [`settings-local-overlay-implementer-brief.md`](settings-local-overlay-implementer-brief.md) · **first review (context):** [`settings-local-overlay-review-2026-07-31.md`](settings-local-overlay-review-2026-07-31.md).

**Shipped:** `Core/Settings/SettingsLoader.vb` (the whole feature), `UI/MainForm_Layout.vb` (title bar), `.gitignore` (one line), `verify/ordercheck/Program.vb` (A50a–j).
**Not shipped, deliberately:** no `settings.json` keys, **no version bump** (still **v64**), no dataset boundary, no card/snapshot surface.

**Build acceptance:** solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck — **six Release builds, 0 warnings / 0 errors**. Harness **ALL PASS**, A1–A51e unregressed + A50a–j. `verify-gate.ps1 -Mode prepush` run **after** the commit: **GATE PASSED**, `display-parity` clean, `version-bump` satisfied by the `[no-engine-change]` token.

---

## 1. Ranked verification handles

**If you run only one:** `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` and look for **A50c** and **A50j**. Those two are the whole risk surface — everything else is behaviour, those two are the catastrophic direction.

| # | Claim | The one cheap check | Load-bearing value |
|---|---|---|---|
| 1 | `Save` cannot promote a local override into the shared file | A50c + A50j PASS | **A50j arm 2** specifically: an *unrelated* save leaves `live_strip.enabled: true` in the tracked file while the overlay's `false` still wins in memory. Arm 1 (the click) is the easy half; arm 2 is where a naive implementation leaks. |
| 2 | The whitelist is an allow-list, not a reject-list | A50d PASS **and** `rej.Count = 5` | The count is the load-bearing part — it asserts *exactly* the five expected paths were refused, so an implementation that silently admitted a sixth key fails. `mtf_gate.enabled` and `alerts.enabled` are two of the five. |
| 3 | Zero scoring impact, at the scoring surface | A50h PASS | Both arms. `identical=True` alone would pass with impotent overlay values; the **potency arm** (`potent=True`) proves the same values *do* move the verdict when applied to the POCO directly. Printed detail shows `baseMax=19 overlaidMax=19 potentMax=5`. |
| 4 | Absent overlay ⇒ byte-identical to the pre-overlay engine | A50a PASS | It asserts two identities, not one: the loaded tree re-serialises to the base text **and** equals a plain `JsonSerializer.Deserialize` of that text — literally the pre-build code path. |
| 5 | `network.` is split per key | A50i PASS | `applied.Count = 1` and `rej.Count = 2`. A block-granular reading passes the "transport rejected" half by accident if the whole block is rejected; the counts catch it. |
| 6 | Nothing else regressed | harness `ALL PASS` | A1–A51e all still pass with A50 appended. |
| 7 | Gate | `GATE PASSED` in the prepush output | Run **post-commit** (the v64 F5 lesson) — a pre-commit run passes `display-parity` and `version-bump` vacuously. |

**Arithmetic identity worth knowing:** for any overlay, `OverlayAppliedKeys.Count + OverlayRejectedKeys.Count` = the number of **leaf** paths in the file (objects are not counted; arrays count as one). A50g pins that arrays are single leaves on both sides.

---

## 2. Decisions queued, with my read

### D-A · `Save` on an overlaid key: write the click, or drop it? — **ruled by me, flagging for confirmation**

Not in the D-table, and the spec and the re-audit describe two different things.

- §1.2: *"apply the caller's changes to it, write that"* — the click reaches the base.
- Brief §1.4 / re-audit F4: *"these become one-way mirrors — the click writes the shared tracked file … while the overlay keeps winning locally"* — same thing, named as an accepted consequence.

But a third option exists and nobody wrote it down: **refuse the write for overlay-owned keys** (the click becomes a no-op on disk).

**I implemented the spec text — the click reaches the base.** *(Hypothesis, and the weaker of my convictions in this build.)* Reasoning: dropping the write means the trader can never change that key on an overlaid box at all, and the tracked file silently ignores their intent — a second silent failure mode on a feature built to remove one. The spec and the re-audit both describe the mirror with eyes open, so it is the documented behaviour rather than an accident.

**What it costs, stated plainly:** a TAPE-checkbox click on the local box, with `live_strip` overlaid, writes `true` into the file that gets xcopied to AWS. Not catastrophic (`live_strip` is display-only) but it is a real one-way mirror, and today only three keys can reach it: `live_strip.enabled`, `performance_display.metric_mode`, `analysis_logging.output_dump_*`. **The narrowest change if you want the other behaviour:** one `Continue For` in `Save`'s revert loop — delete the `callerChanged` test and always revert. Two lines. A50j arm 1's expectation flips; arm 2 is unaffected either way.

### D-B · Should `+local` show when an overlay exists but overrode nothing? — **ruled by me, low stakes**

**I made `OverlayActive` require at least one *applied* key.** Reasoning: §1 makes the marker's **absence** load-bearing — the daily glance in `aws-collector-deploy-checklist.md` §3 reads "no `+local` ⇒ the overlay is gone ⇒ capture is back on". An overlay whose only key was rejected leaves capture **on** while showing `+local`, which is false reassurance in exactly the direction the glance is meant to catch. The rejected-key case is still loud: five `IGNORED` lines plus `present but overrode nothing — base settings in force`.

### D-C · The deploy-checklist line, §1 asked for and I did not write

§1 says *"add 'title bar reads `+local`' to the daily glance in `aws-collector-deploy-checklist.md` §3"*. **I did not touch that file.** It is a live operational doc, the checklist is the trader's, and editing someone's operating procedure was not in the brief's scope list (which names `SettingsLoader.vb` + one `.gitignore` line). **My read: it should be added, and it is a one-line edit** — but it is a checklist change, not a build artefact, so it wants an explicit yes. Flagging rather than doing.

### D-D · Case sensitivity of the allow-list — **ruled by me, recorded because it is a real trap**

The allow-list matches **ordinally**. `{"TRADE_STORE":{...}}` is therefore **rejected and logged**, not admitted — even though the deserialiser is case-insensitive and would have honoured it. The alternative (case-insensitive admit) merges a *duplicate* key alongside the real one and hands `PropertyNameCaseInsensitive = True` an ambiguity. Fail-closed is the safe direction here; the cost is a confusing log line for a trader who typed the wrong case. Worth one sentence in whatever operational note describes the overlay format.

---

## 3. Spec-back proper

**What the spec got right, specifically.**

- **"§1.2 is the single most important thing to get right, and A50c exists for it"** did real work. It made the `Save` path the first thing I designed rather than the last thing I bolted on, and the design that fell out (keep the base document in memory, revert overlay-owned leaves before writing) is structurally incapable of the leak rather than merely avoiding it.
- **The re-audit's F3 — "the catch-all protects it only if the build is an allow-list"** — is the difference between a working feature and a broken one, and it is worded as an *implementation constraint*, which is why it survived into code. I built the allow-list first and the reject notes second; the notes are documentation, the list is enforcement.
- **The J-D clause (ii) extension** ("cannot change what any evidence instrument records that a queued decision depends on") is the reason `alerts.` is out. Worth keeping as a standing rule beyond this spec.

**Which assumptions broke.**

1. **§5's A50g cannot be met as written.** It asks that *"a whitelisted block containing an array is handled without partial-element surprises."* **No admitted block contains an array** — I enumerated all six POCOs (`TradeStoreSettings`, `SignalBridgeSettings`, `LiveStripSettings`, `ExitGuardSettings`, `PerformanceDisplaySettings`, `AnalysisLoggingSettings`); every field is a scalar. So there is nothing to observe through `EngineSettings`. **What I substituted:** the pin moved from the POCO to the **walk** — an array counts as exactly **one** path on both sides of the whitelist (`trade_store.probe_list` applied as one leaf, `change_log` rejected as one leaf). If the merge ever descended into arrays these would come back as per-element paths. **What it costs:** the fixture proves arrays are *leaves*, which is the mechanism behind "replace wholesale", but it does not observe a replacement landing in a typed field, because no such field exists to land in. The moment an array is added to an admitted block, A50g should gain that arm.
2. **§1.2's "apply the caller's changes to it" is not directly implementable.** `Save(cfg, …)` receives the *merged* object; nothing tells the loader which keys the caller touched. I recovered the distinction by keeping a serialisation of the published config (`_currentNode`) and comparing overlay-owned leaves against it — POCO-serialisation on both sides, so `6` vs `6.0` cannot produce a false "changed". **The fail-safe matters more than the mechanism:** unless a change is *provable*, the key reverts to base. An unprovable case can only lose a UI edit; it can never leak the override.
3. **§2.4's "the scoring path reads exactly …" was not re-derived.** I took it as given, as the brief intends. The re-audit's F6 already records that it mixes top-level blocks with children of `indicators`.

**Where the spec was narrower than its own words.**

- **§3's console line says "before→after".** For a key the base does not carry at all, "before" does not exist; I render it `(absent)`. Visible in A50g's output: `trade_store.probe_list: (absent) -> [9,9]`.
- **§1.1 says the watcher "must also fire on `settings.local.json`"** — but `Changed` alone does not cover the case the section actually cares about. **Deleting** the overlay is the failure direction §1 names (`dotnet clean` takes `bin/` with it), and a `Changed` subscription never sees a delete. The overlay watcher subscribes to Created / Deleted / Renamed as well, with `NotifyFilters.FileName` added, which those events need.

**Constraint pairs that nearly conflicted.**

`SettingsLoader.vb` lives under `Core/`, so `verify-gate`'s version-bump guard trips — while the spec forbids a version bump. The hatch is the one the spec already found (`[no-engine-change]`, WARN-only), and the **second** constraint is the one that makes it fragile: the gate reads **committed** messages, so the token has to be in the commit and the gate has to run **after** committing. Both spec §5 and brief §3 name this; it is worth keeping them together in future briefs, because either one alone reads like a smaller problem than it is.

---

## 4. What I did not verify, and cannot

- **Nothing was run in the live app.** The title bar `+local` render is unverified visually — `OverlayActive` is harness-proven, the string concatenation is not. That is the trader's test gate (§6 landing sequence).
- **I did not exercise the real UI save paths.** A50j calls `SettingsLoader.Save` with the same argument shape `MainForm_LiveStrip.vb:343` uses; it does not drive the checkbox. The `Save` contract is proven, the WinForms glue around it is not — the standing A22/A37 harness boundary.
- **The `_current` identity change is untested against consumers.** With an overlay active, `Save` publishes a **new** `EngineSettings` instance (the re-merge) rather than the caller's object, where the pre-overlay path published the caller's object itself. Any code holding a `cfg` reference across a `Save` and expecting it to *be* `Current` would now diverge. I read all five `Save` call sites and none does that — each re-reads `SettingsLoader.Current` on the next tick — but I did not run them.
- **No concurrency testing.** `LoadFromDisk` can be re-entered from two watchers on threadpool threads. The publish is under the write lock and every load re-reads both files from disk, so the outcome converges on the files' current state; I did not construct an adversarial interleaving. Note the harness log shows benign late events from disposed watchers across fixtures, which is the same shape.
- **I did not re-audit the block-by-block analysis** (§2.4 or the re-audit's re-check of it). The whitelist I built is the one the brief specifies, verbatim.
- **`performance_display.` is admitted on a claim I did not re-check** — "no tool reads the eval cache" (re-audit F2, verified there by enumeration across `analysis/` and `tools/`). The code carries a comment saying so and naming Kelly CAL as the near candidate to break it.

---

## 5. Landing sequence — unchanged, and now unblocked

Brief §6, restated because the build is shaped for it and the order matters:

1. Place `settings.local.json` in `bin\Debug\net8.0-windows\` containing `{"trade_store":{"enabled":false}}` — **before** the first post-overlay Debug build.
2. Then build Debug. `PreserveNewest` copies the v64 tracked `settings.json`, the merge resolves capture to `false`, and local capture never starts.
3. Title bar should read **`Deribit Verdict Engine — settings v64 +local`**, and the console should carry one line: `settings.local.json ACTIVE — 1 override(s): trade_store.enabled: true -> false`. Those two together are the confirmation; the title bar alone is the daily glance.

The gap between steps is harmless — the currently-running binary is pre-v64 and has no capture code at all.

**Cross-spec, carried forward:** [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) **D7 must read the MERGED value** (`SettingsLoader.Current.TradeStore.Enabled`), not the base file. Reading the base sees `true` and reports every up-hour on the local store as a capture defect. That spec builds second and inherits this.

---

# 6. Review response — F1 + F2 + D-C (2026-08-02)

Against [`settings-local-overlay-change-request-2026-08-02.md`](settings-local-overlay-change-request-2026-08-02.md) / [`settings-local-overlay-review-2026-08-02.md`](settings-local-overlay-review-2026-08-02.md). Build was APPROVED; this closes the one required finding and the optional one.

**Second commit:** `Core/Settings/SettingsLoader.vb` + `verify/ordercheck/Program.vb` (A50k) + `docs/aws-collector-deploy-checklist.md`. Six Release builds 0/0, harness ALL PASS (A1–A51e + A50a–k), `verify-gate prepush` GATE PASSED post-commit. Still no settings keys, **no version bump — v64**.

## 6.1 F1 — CLOSED. Implemented exactly as scoped, both parts.

The finding is correct and it is the sharper half of D-B: I guarded the rejected-key door and left the typo door open, on the one key the feature was built for.

**(a) Warn on admitted-but-absent.** The `(absent)` that was display text is now a distinct condition. Each such path logs its own line before the ACTIVE line:

```
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY
                 — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
```

**(b) `OverlayActive` requires an applied key that exists in the base.** Admitted paths split into *present* and *unknown*; `OverlayActive = present.Count > 0`. A typo-only overlay takes the `LoadBaseOnly` path, so `Save` also reverts to the pre-overlay branch — correct, since no real override exists for it to protect.

**All three "do NOT"s honoured.** The key is still merged, not rejected. No `UnmappedMemberHandling.Disallow`. The merge itself is untouched — only the reporting and the predicate moved.

**One addition beyond the scope, and why.** The ACTIVE line now counts *effective* overrides while still listing every merged path, so those two numbers can disagree. Rather than leave the reader to reconcile them from the bracketed suffix, absent entries are tagged inline:

```
ACTIVE — 1 override(s): trade_store.enabledd: (absent) -> false [NO EFFECT] · live_strip.enabled: true -> false  [1 admitted key(s) absent from the base — see the warning above]
```

Reporting only, no behaviour. It seemed within the spirit of "this is the line a future seat greps".

**`OverlayUnknownKeys`** joins `OverlayAppliedKeys` / `OverlayRejectedKeys` as a third diagnostic list — what A50k asserts on. The arithmetic identity from §1 still holds and gains a term: `applied + rejected` = leaf paths in the file, and `unknown ⊆ applied`.

**A50k** — two arms, because one would not have been enough. Arm 1 pairs the typo with a real admitted key: the typo changes nothing, the real key still applies, and the marker still shows — so the fix cannot be satisfied by the lazy reading (deactivate any overlay containing an absent key). Arm 2 is the typo alone: no `+local`, `trade_store.enabled` still `true`, tree intact, startup fine. That arm is the F6 shape verbatim.

**Knock-on, deliberate:** A50g's probe array (`trade_store.probe_list`) is absent from the base by construction, so it now trips the warning too. Harmless — `trade_store.enabled` is present in the same overlay, so it still activates — and the fixture comment says so.

**On the F1/D-D family point, which is worth keeping:** `TRADE_STORE` failed loudly because the *allow-list* is ordinal; `enabledd` failed silently because the allow-list is a *prefix match over paths* and knows nothing about the POCO. Same class of mistake, two different mechanisms, and only one of them had a guard. The general shape: **a whitelist validates the key's authority, never its existence.** Any future allow-list over a config surface inherits this and should be built with both checks from the start.

## 6.2 F2 — TAKEN. It was cheap.

`OnOverlayChanged` now reports the observed condition:

```
[SettingsLoader] Re-read settings.local.json — overlay present: True · active: True
```

The `(Deleted) — overlay active: True` pairs are gone from the harness output. You were right that it asserted a causation it did not have; the reload is state-based, so the triggering event is already stale by the time the line prints. The base watcher's line was already event-free and is unchanged.

## 6.3 D-A — confirmed, no change. Reversal recipe kept.

Recorded per your instruction, so a future seat does not have to re-derive it: **to make an overlaid key's UI click a no-op on disk instead of a one-way mirror, delete the `callerChanged` test in `Save`'s revert loop** — the `If callerChanged Then Continue For` and the four lines computing it — so every overlay-owned leaf reverts unconditionally. A50j arm 1's expectation flips (`clickWroteBase` becomes `Not clickWroteBase`); arm 2 is unaffected either way, because it never depended on the branch.

**D-D stands**, and §6.1's last paragraph is the note you asked for on why it and F1 are the same family.

## 6.4 D-C — done, on trader instruction (2026-08-02)

The change request left it with the trader; the trader then said add it. `aws-collector-deploy-checklist.md` now carries it in **two** places, because one would have been wrong:

- **§3 daily glance** — the line as designed, plus the two things that make it usable: it is **inverted on AWS** (a `+local` there means an overlay that box should not have), and after F1 the marker **cannot be earned by a typo'd or rejected key**, which is what makes its absence trustworthy.
- **§1a** — the hand-edit chore is marked **retired**, since leaving it standing beside the overlay is exactly the stale-status-prose failure the queue's §2b sweep exists to catch. The old procedure is kept in a parenthetical for the record, along with the ordering constraint (place the overlay *before* the build) and the `dotnet clean` failure direction.

## 6.5 What I did not verify, this round

- **Still nothing in the live app.** The `+local` title bar remains harness-proven and visually unverified — the trader's test gate.
- **The F1 precondition is the review's, not re-derived.** "Every admitted block is fully seeded in the tracked base (7/2/3/4/8/2 keys)" is what makes `(absent)` a low-noise signal; I took it as given. If a future block ships partially seeded, the warning gets noisier — but it stays correct, and the residual is a false negative in the safe direction.
- **No live-app check that the console warning is actually visible** where the trader looks. It goes to `Console.WriteLine` like every other loader message, so it lands wherever those land; I did not confirm that surface on a WinForms host.
