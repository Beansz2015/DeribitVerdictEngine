# The order app's side of the audit (copied here)

These files are byte-for-byte copies of the two review lanes that ran in, and were committed to, the **DeribitOrderPlacementApp** repo (`Beansz2015/DeribitOrderPlacementApp`). Both audit that app's `master` at `8232e9e`.

| Lane | Files here | Source branch | Source commit |
|---|---|---|---|
| M9 | `2026-09-24-signal-to-exchange-order-path.md`, `proofs/signal-to-exchange-order-path/` | `claude/gracious-fermat-w8sacs` | `db05443` (2026-09-24 13:21 UTC) |
| L-4 (M9b) | `2026-09-25-order-app-gap.md`, `proofs/order-app-gap/` | `claude/zen-cray-wy33a6` | `c5448a7` (2026-09-29 16:02 UTC) |

In the app repo both sets sit under `docs/audits/`, so the paths inside them read `docs/audits/...` where this copy has `docs/audits/order-app/...`. The L-4 copy was checked with `cmp` against `c5448a7`: all seven files are identical.

They're copied here because this repo is where the rest of the 2026-09-24 audit lives, and the session that consolidated it could read the app repo but not push to it.

Every `file:line` in these reports points into **the order app's tree at `8232e9e`**, not into this repo. The proof READMEs' run commands are written for that tree too. To run L-4's harness from this copy, point it at an app worktree:

```bash
git -C <app-clone> worktree add <dir> 8232e9e
bash docs/audits/order-app/proofs/order-app-gap/run.sh <dir> all
```

`gen.sh` refuses to run unless the worktree is at `8232e9e`. The run needs the .NET 8 SDK and nuget.org, and takes about 2.5 minutes. It was re-run on 2026-09-29 and matched the README's recorded output once timings were stripped.

For the consolidated verdicts on these findings, see [`../2026-09-24-review-batch-summary.md`](../2026-09-24-review-batch-summary.md) §3. For where they rank, see [`../../adversarial-audit-2026-09-24.md`](../../adversarial-audit-2026-09-24.md) §B, Band A.
