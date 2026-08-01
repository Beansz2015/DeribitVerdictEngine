# Review — `settings.local.json` overlay build (2026-08-02)

**Reviewer:** the orchestrator/reviewing seat. **Reviewed:** `291457c` + [`settings-local-overlay-spec-back.md`](settings-local-overlay-spec-back.md).
**Binding corrections the build had to honour:** [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) + §1 of the [implementer brief](settings-local-overlay-implementer-brief.md).

> **Verdict: the build is sound, matches the corrected spec, and the spec-back is honest about its own deviations.** I re-ran the gate rather than accepting it: **GATE PASSED**, six Release builds, harness ALL PASS incl. A50a–j, `display-parity` clean, `version-bump` satisfied by the token. **Two findings, one moderate, neither blocking.** F1 is worth fixing before the landing sequence, because it lands on the one key this feature exists for.

---

## 1. Verified rather than accepted

- **The whitelist is the corrected one, enforced as an allow-list.** `AdmittedBlocks` carries six entries and **`alerts.` is not among them**; `mtf_gate` and `alerts` are named in `RejectNotes` with their reasons in code, not prose. `IsAdmitted` returns `False` by default, so it is an allow-list by construction — the re-audit's F3 concern, closed the way it asked.
- **`Save` is fail-safe in the correct direction.** `callerChanged` initialises `False` and is only set `True` when the caller's value provably differs from the published serialisation. Unprovable ⇒ revert to base. **That can lose a UI edit; it cannot leak an override.** The no-overlay path returns early and is byte-unchanged, and a re-parse failure aborts the write rather than risking a merged tree in the shared file.
- **A50j drives production code.** It calls `SettingsLoader.Save` with the argument shape `MainForm_LiveStrip.vb:343` uses and reads the base file back off disk. Not a mirrored predicate — the A48f lesson applied correctly. Arm 2 (the unrelated save) is the one that catches a naive implementation and it is the arm that exists.
- **Commit hygiene.** `settings.json` genuinely untouched and still **v64**; `[no-engine-change]` token present in the commit; `.gitignore` line present with its reasoning; the display-parity exemption **stated in the message** per the hard rule, with `MainForm_Render_Cards.vb` correctly untouched because the title bar is a live status element.
- **A50h's potency arm is better than the spec asked for.** `identical=True` alone would pass with impotent overlay values; proving the same values *do* move the verdict when applied directly to the POCO is what makes the pin mean something. Worth copying as a pattern for future "no impact" claims.

---

## 2. Findings

### F1 — moderate. A typo in an admitted block reports success and does nothing.

`IsAdmitted` is a **pure path-prefix match** and never validates the key against the POCO, and deserialisation runs without `UnmappedMemberHandling.Disallow`. So:

```json
{"trade_store":{"enabledd":false}}
```

is **admitted**, counted into `applied`, sets `_overlayActive = True`, and logs

```
settings.local.json ACTIVE — 1 override(s): trade_store.enabledd: (absent) -> false
```

…while `TradeStoreSettings.Enabled` stays `true` and **local capture runs.** Both visibility mechanisms — the console line *and* the title-bar `+local` — actively confirm the overlay is working. **That is precisely the F6 failure this feature exists to prevent, reached by one keystroke, on the one key it was built for.**

**D-B's own ruling already covers this, and the implementation closed one of its two doors.** The stated reasoning for requiring at least one *applied* key was: *"An overlay whose only key was rejected leaves capture on while showing `+local`, which is false reassurance in exactly the direction the glance is meant to catch."* A typo'd key is the **same false reassurance by a different route** — rejected keys are guarded, unmapped keys are not.

**The tell already exists and is not being used.** `(absent)` is computed at the log line purely as display text for a key the base lacks. For an admitted key that is a strong typo signal, because the base carries every real key in these blocks.

**Proportionate fix, cheapest first:** log an admitted-but-absent path as a **warning** naming it as probably a typo and stating it will have no effect; and/or exclude `(absent)` keys from the `OverlayActive` count so the title bar stops confirming. A genuinely new key in an admitted block is possible (a future `trade_store` key landing before the base file has it), so **warn rather than reject** — fail-closed here would block a legitimate case for a rare one.

### F2 — low, diagnostic. The hot-reload line pairs a stale event type with fresh state.

`OnOverlayChanged` sleeps 200 ms, calls `LoadFromDisk()` — which re-reads **both files from disk**, so the resulting state is correct — then prints `e.ChangeType` beside `_overlayActive`. Under churn the two legitimately disagree. My gate run shows it four times:

```
Hot-reloaded settings.local.json (Deleted) — overlay active: True
Hot-reloaded settings.local.json (Created) — overlay active: False
```

**The behaviour is right and the fixture correctly passes** — reload is state-based, not event-based, which is the sound design. **The line asserts a causation it does not have**, and reads as a bug to anyone consulting it. Mostly a harness-speed artefact since live overlay edits are rare and manual, but this is the line §3 designates for diagnosis. One-line fix: report the observed condition (`overlay present: … active: …`) instead of the event that triggered the re-read.

---

## 3. On the queued decisions

**D-A — confirm as ruled.** The click reaching the base is what both the spec (§1.2) and the re-audit (F4) describe, with eyes open. The alternative — dropping the write — creates a *second* silent failure mode on a feature built to remove one: the trader's intent vanishes with no symptom. The mirror is bounded to three display-only keys (`live_strip.enabled`, `performance_display.metric_mode`, `analysis_logging.output_dump_*`). **Keep it, and keep the two-line reversal recipe recorded in case a future key makes the trade-off worse.**

**D-B — right ruling, incomplete implementation.** See F1.

**D-C — agreed, and I would add the line.** The marker's *absence* is load-bearing (`dotnet clean` takes `bin/` and silently restores capture), and the daily glance is the only thing that makes absence observable. It is the trader's checklist, so the implementer was right not to edit it unasked — but it should be added, and F1's warning would ride the same glance.

**D-D — fail-closed on case is correct.** Case-insensitive admit would merge a duplicate key alongside the real one and hand `PropertyNameCaseInsensitive` an ambiguity. Agreed; worth one sentence wherever the overlay format gets written down operationally. **Note F1 is the same family:** `TRADE_STORE` fails loudly, `enabledd` fails silently — after F1 both fail loudly.

---

## 3a. Close-out — F1/F2/D-C reviewed and ACCEPTED (2026-08-02, `1611011`)

**Verified independently, not accepted:** I re-ran `verify-gate.ps1 -Mode prepush` — **GATE PASSED**, six Release builds, harness **ALL PASS** (A1–A51e + A50a–**k**), display-parity clean, version-bump satisfied by the token. Still **v64**, no settings keys.

- **F1 — closed, both parts, and the three "do NOT"s honoured.** `UnmappedMemberHandling` is absent from the tree (checked); the key is still merged rather than rejected; the merge itself is untouched. The warning fires with the scoped text, and `OverlayActive` now keys on `present.Count > 0`.
- **A50k is the right fixture, and arm 1 is why.** Pairing the typo with a real admitted key means the fix **cannot be satisfied by the lazy reading** — deactivating any overlay that contains an absent key would fail arm 1. Arm 2 is the F6 shape verbatim: typo alone ⇒ no `+local`, `trade_store.enabled` still `true`, tree intact. Confirmed in the gate output.
- **The out-of-scope addition is sound and I would keep it.** The ACTIVE line counts *effective* overrides while still listing every merged path, with absent entries tagged `[NO EFFECT]` inline plus a reconciling suffix. Two numbers that could disagree now explain themselves on the same line. Within the spirit of "the line a future seat greps."
- **Ordering checked on the one path where it could have silently cost the diagnostic:** `LoadBaseOnly` clears `_overlayUnknown`, and the typo-only branch re-assigns it *after* that call — so the diagnostic survives the very case it exists for. A50k arm 2 asserts it and passes.
- **The typo-only branch's `Save` behaviour is safe.** With `_overlayActive = False` the pre-overlay write path runs, and that is correct rather than merely tolerable: the caller's POCO came from a merged tree whose only difference deserialised to nothing, so it is byte-equivalent to the base.
- **F2 — taken.** `Re-read settings.local.json — overlay present: True · active: True`. The `(Deleted) — overlay active: True` pairs are gone from the harness output.
- **D-C — done, and better than asked.** Two places, not one: §3 carries the glance line **plus the AWS inversion** (a `+local` there means an overlay that box should not have), and §1a marks the hand-edit chore **retired** with the old procedure kept parenthetically. Retiring the superseded chore was not in the request and is exactly right — leaving it standing beside the overlay is the stale-status-prose failure the queue's §2b sweep exists to catch.

**One observation worth carrying beyond this spec**, from §6.1's closing note: `TRADE_STORE` failed loudly because the allow-list is ordinal; `enabledd` failed silently because the allow-list is a prefix match over *paths* and knows nothing about the POCO. **A whitelist validates a key's authority, never its existence.** Any future allow-list over a config surface should carry both checks from the start.

**Nothing outstanding on this build.** A3's landing sequence is unblocked.

## 4. What I did not verify

- **No live app run.** The title-bar `+local` string is harness-proven at `OverlayActive` but visually unverified — the trader's test gate, and step 3 of the landing sequence is the check.
- **The WinForms glue.** A50j proves the `Save` contract, not the checkbox path — the standing A22/A37 boundary.
- **Concurrency.** I did not construct an adversarial interleaving of the two watchers; the spec-back's reasoning (publish under the write lock, every load re-reads disk, state converges) is sound on inspection and F2 is the visible echo of it.
- **The `_current` identity change against live consumers.** The spec-back read all five `Save` call sites and reports none holds a `cfg` reference across a save; I did not re-derive that.
- **The `performance_display.` admission** rests on "no tool reads the eval cache", which I verified by enumeration in the re-audit and did not re-check here. **The code carries a comment naming Kelly CAL as the near candidate to break it — and the 2026-08-01 F1 read has since made CAL unlikely to ship soon**, so that admission is safer today than when it was written, not less.
