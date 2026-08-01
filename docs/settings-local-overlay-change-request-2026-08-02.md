# Overlay build — review response + change request (2026-08-02)

*This document is the paste-back. Everything below is addressed to the implementer of `291457c`.*

**Review:** [`settings-local-overlay-review-2026-08-02.md`](settings-local-overlay-review-2026-08-02.md) — **build APPROVED.** Gate re-run independently: GATE PASSED, six Release builds, harness ALL PASS incl. A50a–j, display-parity clean. The whitelist is the corrected one, `Save` is fail-safe in the right direction, and A50j drives production code rather than a mirrored predicate. A50h's potency arm is better than the spec asked for.

---

## 1. D-A — CONFIRMED AS RULED. No change.

**The click reaches the base; the overlay keeps winning locally.** You implemented the spec text and it stands. Both §1.2 and the re-audit's F4 describe the one-way mirror with eyes open, and the alternative — dropping the write — creates a *second* silent failure mode on a feature built to remove one: the trader's intent would vanish with no symptom. The mirror is bounded to three display-only keys.

**Keep your two-line reversal recipe in the spec-back.** If a future key makes the trade-off worse, that is the record of how to flip it.

**D-D also stands** — fail-closed on case is correct. Note it is the same family as F1 below: `TRADE_STORE` fails loudly today, `enabledd` fails silently. After F1 both fail loudly.

---

## 2. F1 — REQUIRED. A typo in an admitted block reports success and does nothing.

**Your D-B ruling is right and its implementation closed one of two doors.** You required at least one *applied* key precisely because *"an overlay whose only key was rejected leaves capture on while showing `+local`, which is false reassurance in exactly the direction the glance is meant to catch."* A **typo'd** key is the same false reassurance by a different route, and it is not guarded.

`IsAdmitted` is a pure path-prefix match with no POCO validation, and deserialisation runs without `UnmappedMemberHandling.Disallow`. So:

```json
{"trade_store":{"enabledd":false}}
```

is admitted, counted into `applied`, sets `_overlayActive = True` so the title bar renders `+local`, and logs `trade_store.enabledd: (absent) -> false` — while `TradeStoreSettings.Enabled` stays `true` and **local capture runs**. Console *and* title bar both confirm the overlay is working. That is the F6 failure this feature exists to prevent, on the one key it was built for.

**Verified precondition that makes the fix precise:** every admitted block is fully seeded in the tracked base — `trade_store` 7 keys, `signal_bridge` 2, `live_strip` 3, `exit_guard` 4, `performance_display` 8, `analysis_logging` 2. **So `(absent)` on an *admitted* path today can only be a typo or a not-yet-seeded new key.** The signal is low-noise.

### The change, two parts

**(a) Warn when an admitted path has no base counterpart.** You already compute `(absent)` at the log line as display text — promote it to a distinct, loud condition:

```
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY
                 — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
```

**(b) `OverlayActive` requires at least one applied key that exists in the base.** A typo-only overlay must not render `+local`. This is your own D-B principle extended to the second door: the glance must not give false reassurance. The residual — a legitimately-new key overlaid *alone* yields no `+local` — is a false negative in the **safe** direction, and (a)'s warning explains it on the same screen.

### Explicitly do NOT

- **Do not reject the key.** Fail-closed would block a genuinely new key landing before the base is seeded.
- **Do not add `UnmappedMemberHandling.Disallow`.** It is the tempting one-liner and it is wrong here — it would make *any* unknown property anywhere in `settings.json` fatal at load, including legitimately retired keys. Blast radius far beyond this finding.
- **Do not change the merge.** The merge is correct; only the reporting and the `OverlayActive` predicate move.

---

## 3. F2 — LOW, same file, take it if it is cheap

`OnOverlayChanged` re-reads both files from disk — correct, state-based — then prints the **stale** `e.ChangeType` beside the **fresh** `_overlayActive`, so under churn they disagree. The gate output shows `(Deleted) — overlay active: True` four times and `(Created) — overlay active: False`.

Behaviour is right and A50f correctly passes. The line asserts a causation it does not have, in the line §3 designates for diagnosis. **Report the observed condition rather than the triggering event** — e.g. `overlay present: <bool> — active: <bool>`.

---

## 4. Acceptance

- **New fixture `A50k`** — an admitted-but-absent key is **warned**, does **not** count toward `OverlayActive`, leaves the base untouched, and startup still succeeds. Pair it with a real admitted key in the same overlay to prove the real one still applies and still yields `+local`.
- A50a–j unregressed; A1–A51e unregressed.
- Six Release builds 0/0. **`verify-gate.ps1 -Mode prepush` run AFTER committing** — pre-commit passes `display-parity` and `version-bump` vacuously (the v64 F5 lesson).
- **No settings keys, no version bump — still v64.** `[no-engine-change]` token in the commit message.
- **Display-parity exempt** (title bar is a live status element) — **state that in the commit message** per the hard rule.
- Append to the existing spec-back rather than starting a new one.

**Not in scope:** D-C, the `aws-collector-deploy-checklist.md` §3 line. You were right to leave the trader's operating procedure alone; it is still with the trader.
