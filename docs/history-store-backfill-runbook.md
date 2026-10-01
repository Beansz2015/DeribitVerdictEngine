# History store — 21-month cloud backfill — RUNBOOK

**For:** the trader, who starts and stops the temporary instance. **Written:** 2026-10-01 (UTC) by the stage-2 build seat.
**Why:** ruling `HDS-2` (the backfill-depth ruling in [`history-data-store-spec.md`](history-data-store-spec.md) §6) — all 21 months, 2025-01-01 → 2026-09-30, fetched once on a temporary AWS instance, then moved to this machine before 2026-10-13.
**Tool:** `BacktestRunner history …` ([`tools/BacktestRunner/HistoryCli.vb`](../tools/BacktestRunner/HistoryCli.vb)). **Build record:** [`history-data-store-build-spec-back.md`](history-data-store-build-spec-back.md).

⛔ **Never run this on the collector box** (1 GB RAM, ~7 GB disk, live engine). A new, temporary instance only.

---

## 0. Read first — three things

| # | What | Why it matters |
|---|---|---|
| 1 | **S3 deletes the files 7 days after upload.** The bucket `deribit-engine-bucket` has an `expire-7d` lifecycle rule ([`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §2.2). The first month is uploaded about 3 h after the start. | Pull to this machine (step e) **within 7 days of the start**. Plan: start by 2026-10-08, pull by 2026-10-12 at the latest. |
| 2 | **Expect about 23 h of fetching.** Measured from this machine at 880 trades/s (section 1). The self-test in step (b) measures the pace from London; use its rate. | Start it in the morning; check it twice; pull the next day. |
| 3 | **`--to` is exclusive, and nothing younger than 24 h is fetched.** The range is `--from 2025-01-01 --to 2026-10-01`, which ends on 2026-09-30. The tool holds back any day that ended less than 24 h ago (the liquidation flag arrives late). | Start after 2026-10-02 00:00 UTC, so 2026-09-30 is settled. Days after 2026-09-30 come later by `history topup` on this machine. |

**Cost (not verified against a bill):** a `t3.small` plus a 30 GB `gp3` volume for about 30 h is under USD 2. The download from S3 (~1.1 GB) adds about USD 0.10.

**If a seat assists you during the run:**
Model: Sonnet 5 · Effort: medium — the procedure is fixed; escalate to Opus 5.5 · high if step (d) or the comparison reports any problem.

---

## 1. Measured facts (2026-10-01 UTC, read-only requests to `history.deribit.com`)

**Trade counts.** Method: the `trade_seq` of the first trade at or after 00:00 UTC, one day subtracted from the next (exact to within a few trades at the day edge).

| Day | Trades | Note |
|---|---:|---|
| 2024-03-12 | 213,072 | outside the range, for scale |
| 2025-01-01 | 50,818 | range start, holiday |
| 2025-03-10 | 312,223 | |
| 2025-06-02 | 97,703 | the validation read's day |
| 2025-10-10 | 518,314 | the busiest day sampled (a crash day) |
| 2025-12-15 | 124,330 | |
| 2026-04-15 | 110,577 | |
| 2026-09-27 | 108,811 | the stage-2 tool's own count, contiguous |
| **2025-01-01 → 2026-09-30** | **71,766,892** | `trade_seq` 230,579,801 → 302,346,693; 638 days; mean ~112,500/day |

**Pace.** Four live single-day runs of the stage-2 tool from this machine (Asia → Deribit): **877–889 trades/s, 1.12–1.13 requests/s, 0 retries, 0 has_more splits.**

| Item | Value | How |
|---|---|---|
| Fetch time, 21 months | **≈ 22.7 h** at 880 trades/s (71.77 M ÷ 880 ÷ 3600) | Measured pace × measured count |
| Store size on disk | **≈ 6.0 GB** (83.2 bytes/row) | Measured: 9,054,716 B for 108,811 rows (11 columns) |
| Compressed for S3 | **≈ 1.1 GB** (gzip, 17.8 %) | Measured on the same file |
| Month-file rewrite cost | not measured; estimated under 1 h in total | The tool rewrites a month file once per day it adds |

⚠ **Not measured:** the pace from London. Deribit's servers are thought to be in London, which would make it faster. The self-test in step (b) prints it.

---

## 2. Step (a) — launch the temporary instance

Run on **this machine**, PowerShell, from `C:\Dev\DeribitVerdictEngine`, with your own AWS CLI credentials.

**a1. Build and upload the tool** (a Linux build that needs no .NET install on the instance):

```powershell
cd C:\Dev\DeribitVerdictEngine
git log -1 --oneline
$out = 'C:\DeribitData\history-tool'
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
dotnet publish tools\BacktestRunner\BacktestRunner.vbproj -c Release -r linux-x64 --self-contained true -o "$out\pkg\runner"
Copy-Item tools\ops\history-backfill\history-backfill-cloud.sh "$out\pkg\"
tar -czf "$out\history-tool.tar.gz" -C "$out\pkg" .
aws s3 cp "$out\history-tool.tar.gz" s3://deribit-engine-bucket/history-backfill/tool/history-tool.tar.gz --region eu-west-2
```

**a2. Launch** — Amazon Linux 2023 (the SSM agent and the AWS CLI are pre-installed), `t3.small` (2 vCPU, 2 GiB), 30 GB `gp3`, the collector's subnet and security group, and the **`EC2-SSM-Access`** instance profile (it gives SSM and the S3 bucket; nothing else). Parameters from [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §8.2.

```powershell
$ami = aws ssm get-parameter --region eu-west-2 --name /aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64 --query Parameter.Value --output text
$id = aws ec2 run-instances --region eu-west-2 --image-id $ami --instance-type t3.small `
  --subnet-id subnet-04aa80d58b46f6b8a --security-group-ids sg-06d4dc051bda56562 `
  --iam-instance-profile Name=EC2-SSM-Access --associate-public-ip-address `
  --block-device-mappings "DeviceName=/dev/xvda,Ebs={VolumeSize=30,VolumeType=gp3,DeleteOnTermination=true}" `
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=deribit-history-backfill-TEMP}]" `
  --count 1 --query "Instances[0].InstanceId" --output text
$id
```

- **Write `$id` down.** Every later step uses it, and step (f) must terminate exactly this instance.
- Wait 2–5 min, then confirm SSM sees it (repeat until it prints `Online`):

```powershell
aws ssm describe-instance-information --region eu-west-2 --filters "Key=InstanceIds,Values=$id" --query "InstanceInformationList[0].PingStatus" --output text
```

---

## 3. Step (b) — self-test, then start the unattended run

**b1. Self-test** (about 3 min): installs the tool, fetches 2026-09-27 into a scratch folder on the instance, and runs the deep check.

```powershell
$c = aws ssm send-command --region eu-west-2 --instance-ids $id --document-name AWS-RunShellScript --parameters file://tools/ops/history-backfill/ssm-history-selftest.json --query Command.CommandId --output text
# wait ~3 min, then:
aws ssm get-command-invocation --region eu-west-2 --command-id $c --instance-id $id --query "[Status,StandardOutputContent,StandardErrorContent]" --output text
```

**Pass:** `Success`, a line `DAY 2026-09-27 OK rows=108811 … missing=0`, `HISTORY_STATUS problems=0`, and this SHA-256 for the file (the same bytes this machine produced):

```
f1ebd2ab146ab2c5343ffae05610c084b90e66599261ba4cde68348d8acc6b0c  /data/selftest/trades_2026-09.csv
```

- **Read the `rate=` on the `DAY` line.** Expected duration = 71,766,892 ÷ rate ÷ 3600 hours.
- ⛔ **A different SHA-256, `GAP`, `FAILED`, or a crash: stop here** and bring the output to a seat. Do not start the run.

**b2. Start the run:**

```powershell
$c = aws ssm send-command --region eu-west-2 --instance-ids $id --document-name AWS-RunShellScript --parameters file://tools/ops/history-backfill/ssm-history-start.json --query Command.CommandId --output text
aws ssm get-command-invocation --region eu-west-2 --command-id $c --instance-id $id --query "[Status,StandardOutputContent]" --output text
```

- It prints `Active: active (running)`. The run is a systemd unit (`deribit-history-backfill`); it keeps going after the command ends.
- **What it does, unattended:** 31 days per pass, **newest day first**, checkpoint after every day. After every pass it gzips every **complete** month and uploads it to `s3://deribit-engine-bucket/history-backfill/store/` with a SHA-256 manifest. A host failure the retries do not absorb (5 retries, 2–32 s back-off) stops the pass; the loop waits 5 min and resumes from the checkpoint, up to 24 times. At the end it runs the deep check and uploads `DONE` (or `INCOMPLETE`).
- **If the instance reboots:** run b2 again. It resumes from the checkpoint; completed days are not fetched again.

---

## 4. Step (c) — check progress

```powershell
$c = aws ssm send-command --region eu-west-2 --instance-ids $id --document-name AWS-RunShellScript --parameters file://tools/ops/history-backfill/ssm-history-status.json --query Command.CommandId --output text
aws ssm get-command-invocation --region eu-west-2 --command-id $c --instance-id $id --query StandardOutputContent --output text
```

| Line | Healthy |
|---|---|
| `== unit` | `active (running)` until the end, then `inactive` |
| `== last days` | `DAY yyyy-mm-dd OK … missing=0`, dates moving backwards towards 2025-01-01 |
| `== store` | `[status] … OK n \| GAP 0 \| FAILED 0`; no `PROBLEM` line |
| `== uploaded months` | grows by one about every 1–1.5 h after the first ~3 h; **21** at the end |
| `== DONE` | appears at the end |
| `== disk` | well under 30 GB (the store ends near 6 GB) |

| You see | Do |
|---|---|
| `GAP` or `FAILED` days | Nothing yet: the loop re-tries them after every never-fetched day. If they remain at the end, `INCOMPLETE` is uploaded — bring `status_final.txt` to a seat |
| `stopped on a host failure` in the loop lines | Nothing: it resumes after 5 min. More than a few in a row → the history host is down or throttling; bring the output to a seat (this is an escalation trigger in [`history-data-store-spec.md`](history-data-store-spec.md) §0) |
| `tool exit 1` | A store or argument problem. The run stopped. Bring `/data/history/loop.log` to a seat |
| `INCOMPLETE` | The run finished with problems. Do not terminate the instance; bring `status_final.txt` to a seat |

---

## 5. Step (d) — verify completeness

**On the instance (automatic):** the run ends with `history status --deep`. `DONE` is uploaded only when it reports **no problem**:
- every day from 2025-01-01 to 2026-09-30 is `OK` (no `GAP`, no `FAILED`, no day missing);
- day N's first `trade_seq` is day N−1's last + 1, for every adjacent pair (the seq seam);
- every month file re-reads with 0 unparseable rows, 0 duplicate seqs, 0 missing seqs, and per-day row counts equal to the checkpoint;
- all 21 months `COMPLETE`.

**On this machine (step e does it again):** the pull re-checks every file's SHA-256 and re-runs the deep check on the installed store. Then the comparison in step (e3) checks it against the box store.

---

## 6. Step (e) — transfer to this machine

**Where it lands:** `C:\DeribitData\history\` — 21 files `trades_2025-01.csv` … `trades_2026-09.csv` (~6.0 GB), plus `history_checkpoint.csv`, `history_backfill.log`, `status_final.txt`, `loop.log`. This is outside the repo and is the tool's default `--store`. A staging copy of the `.gz` files goes to `C:\DeribitData\history-transfer\` (~1.1 GB; delete it after e3).

**e1. Build the tool here and pull:**

```powershell
cd C:\Dev\DeribitVerdictEngine
dotnet build tools\BacktestRunner\BacktestRunner.vbproj -c Release
powershell -ExecutionPolicy Bypass -File tools\ops\history-backfill\history-pull.ps1
```

- **Pass:** `months installed 21 … rows 71766…`, then the deep status with no `PROBLEM` line, exit 0.
- It checks every `.gz` and every decompressed file against the manifest's SHA-256, and **never overwrites** a different file already in the store; it stops instead.

**e2. Top up to yesterday** (≈ 2 min per day at the measured pace):

```powershell
dotnet tools\BacktestRunner\bin\Release\net8.0\BacktestRunner.dll history topup --store C:\DeribitData\history
```

**e3. Compare with the box store** (the newest fetch folder; read-only on both):

```powershell
$box = (Get-ChildItem aws_fetch -Directory | Sort-Object Name | Select-Object -Last 1).FullName + '\backtest_data'
dotnet tools\BacktestRunner\bin\Release\net8.0\BacktestRunner.dll history compare --box $box --store C:\DeribitData\history --from 2026-07-23 --to 2026-09-28 --ledger C:\DeribitData\history\compare_ledger.csv
```

- Set `--to` to the day **after** the last complete day in that fetch folder.
- **Pass:** the last line reads `HISTORY_COMPARE verdict=PASS`. `only_in_dev` above 0 is expected: those are the box store's holes.
- ⛔ **`verdict=FAIL` stops the plan.** Bring the output to a seat (the escalation trigger in [`history-data-store-spec.md`](history-data-store-spec.md) §0). The row goes to the ledger either way; the stage-4 pass rule reads it (`history passrule --ledger …`).

---

## 7. Step (f) — terminate the instance

Only after e1 printed the deep status with no problem.

```powershell
aws ec2 terminate-instances --region eu-west-2 --instance-ids $id --query "TerminatingInstances[0].CurrentState.Name" --output text
aws ec2 describe-instances --region eu-west-2 --instance-ids $id --query "Reservations[0].Instances[0].State.Name" --output text
```

- The first prints `shutting-down`; the second, a minute later, `terminated`. The 30 GB volume is deleted with it (`DeleteOnTermination=true`).
- ⛔ **Check the id before you press Enter.** It must be the `deribit-history-backfill-TEMP` instance, never the collector.
- The S3 copies remove themselves after 7 days. Nothing else to clean.

---

## 8. What I did not verify

| Claim | Status |
|---|---|
| Every AWS command in steps (a), (b), (c), (f) | **Not run** — the build brief forbade any AWS or SSM command. They follow the AWS CLI syntax and the proven `collector.ps1` SSM pattern, but none was executed |
| The Linux build runs on Amazon Linux 2023 | **Not run.** The `linux-x64` self-contained publish succeeded on this machine (spec-back §2); the self-test (b1) is the first run on Linux |
| The subnet gives the instance a route to the internet | Assumed from the collector reaching Deribit through it |
| The pace from London | Not measured; b1 measures it |
| `history-backfill-cloud.sh` | The `upload`, `final` and `status` verbs ran on this machine under Git Bash with S3 stubbed (spec-back §2); `start` and `loop` under systemd did not run |
| `history-pull.ps1` | Ran here with `-SkipDownload` on a locally staged copy (spec-back §2); the S3 download did not run |
| The instance price | Not checked against AWS pricing |
