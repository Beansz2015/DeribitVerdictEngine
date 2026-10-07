# tools/ops/absorption-stage1-read.py -- usage: python tools/ops/absorption-stage1-read.py aws_fetch/<stamp>
# Record: docs/absorption-stage1-read-2026-10-07.md
"""D-6d Stage 1 read: counting gap and close-reason attribution from absorption_episodes.log,
plus the D-2 STOP watch (flag rate vs ratio distribution) from analysis_log.csv.
Weekday-scoped (UTC Mon-Fri). Day-block bootstrap for the gap CI."""
import csv, random, re, statistics, sys
from collections import defaultdict
from datetime import datetime

FETCH = sys.argv[1]
REASONS = ["DegenerateLadder", "LevelRemap", "ProximityShut", "LadderSpanLost",
           "BreakThrough", "Reset", "TouchCrossed"]

def session(h):
    return "ASIA" if h < 8 else ("LONDON" if h < 13 else "NY")

def parse_side(txt):
    d = {}
    for tok in txt.split():
        if "=" not in tok:
            continue
        k, v = tok.split("=", 1)
        if k in REASONS:
            c, disc, life, sh = v.split("/")
            d[k] = (int(c), float(disc), float(life), float(sh))
        else:
            d[k] = v
    return d

lines = 0; weekend = 0; bad = 0
iids = defaultdict(int)
agg = defaultdict(lambda: defaultdict(float))   # key -> field -> sum
day_terms = defaultdict(lambda: [0.0, 0.0, 0.0])  # day -> [accrued, shadow, shadow_excl_break]
first = last = None
with open(f"{FETCH}/absorption_episodes.log", encoding="utf-8") as f:
    for ln in f:
        parts = [p.strip() for p in ln.rstrip("\n").split(" | ")]
        if len(parts) < 7 or parts[1] != "v1":
            bad += 1; continue
        ts = datetime.strptime(parts[0][:19], "%Y-%m-%dT%H:%M:%S")
        lines += 1
        if ts.weekday() >= 5:
            weekend += 1; continue
        first = first or ts; last = ts
        iids[parts[2]] += 1
        sess = session(ts.hour)
        for sp in parts[5:7]:
            side, rest = sp.split(" ", 1)
            d = parse_side(rest)
            acc = float(d["press_accrued_usd"]); sh = float(d["shadow_usd"])
            shb = d["BreakThrough"][3]
            for key in ("ALL", side, sess, f"{sess}/{side}"):
                a = agg[key]
                a["accrued"] += acc; a["shadow"] += sh; a["shadow_n"] += int(d["shadow_n"])
                a["shadow_breaks"] += int(d["shadow_breaks"]); a["lines"] += 1
                for r in REASONS:
                    c, disc, life, s = d[r]
                    a[f"{r}.n"] += c; a[f"{r}.disc"] += disc; a[f"{r}.life"] += life; a[f"{r}.sh"] += s
            dt = day_terms[ts.date()]
            dt[0] += acc; dt[1] += sh; dt[2] += sh - shb

print(f"sidecar lines={lines} weekend_excl={weekend} malformed={bad} weekday span {first} -> {last}")
print(f"weekday days={len(day_terms)} instances={len(iids)}")

def gap(a, excl_break=False):
    sh = a["shadow"] - (a["BreakThrough.sh"] if excl_break else 0)
    den = a["accrued"] + sh
    return sh / den if den else float("nan")

print("\nCOUNTING GAP = shadow / (press_accrued + shadow), USD, pooled by summation")
print(f"{'pop':14} {'accrued_usd':>14} {'shadow_usd':>12} {'gap':>7} {'gap_exBT':>9}")
for key in ["ALL", "ABOVE", "BELOW", "ASIA", "LONDON", "NY"]:
    a = agg[key]
    print(f"{key:14} {a['accrued']:14,.0f} {a['shadow']:12,.0f} {gap(a):7.1%} {gap(a, True):9.1%}")

# day-block bootstrap
days = list(day_terms.values()); random.seed(20261008)
for lbl, idx in (("gap", 1), ("gap_exBT", 2)):
    bs = []
    for _ in range(5000):
        s = [random.choice(days) for _ in days]
        acc = sum(x[0] for x in s); sh = sum(x[idx] for x in s)
        bs.append(sh / (acc + sh))
    bs.sort()
    print(f"day-block 95% CI {lbl}: {bs[124]:.1%} - {bs[4874]:.1%}")
print("per-day gap:", " ".join(f"{d.isoformat()[5:]}={x[1]/(x[0]+x[1]):.0%}" for d, x in sorted(day_terms.items())))

a = agg["ALL"]
print("\nCLOSE REASONS (weekday, both sides)")
totn = sum(a[f"{r}.n"] for r in REASONS); totsh = a["shadow"]
print(f"{'reason':17} {'closes':>8} {'share':>6} {'mean_life_s':>11} {'shadow_usd':>12} {'sh_share':>8}")
for r in REASONS:
    n = a[f"{r}.n"]
    ml = a[f"{r}.life"] / n if n and r != "Reset" else float("nan")
    print(f"{r:17} {n:8,.0f} {n/totn:6.1%} {ml:11.2f} {a[f'{r}.sh']:12,.0f} {a[f'{r}.sh']/totsh if totsh else 0:8.1%}")
print(f"shadow prints={a['shadow_n']:,.0f}  idle intervals ended by a break print={a['shadow_breaks']:,.0f}")
for s in ("ASIA", "LONDON", "NY"):
    b = agg[s]; tn = sum(b[f"{r}.n"] for r in REASONS); ts_ = b["shadow"] or 1
    print(f"  {s:6} closes " + " ".join(f"{r[:6]}={b[f'{r}.n']/tn:.0%}" for r in REASONS if b[f'{r}.n']) +
          " | shadow " + " ".join(f"{r[:6]}={b[f'{r}.sh']/ts_:.0%}" for r in REASONS if b[f'{r}.sh']))

# STOP watch from the CSVs
def load(path):
    out = []
    with open(path, encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            try:
                ts = datetime.strptime(row["Timestamp"][:19], "%Y-%m-%d %H:%M:%S")
            except Exception:
                continue
            if ts.weekday() >= 5:
                continue
            out.append((ts, row))
    return out

def stop_stats(rows, label):
    n = len(rows); act = [r for _, r in rows if (r.get("AbsorptionEpisodeSec") or "").strip()]
    fl = [r for _, r in rows if (r.get("AbsorptionSignal") or "NONE") not in ("NONE", "")]
    rat = sorted(float(r["AbsorptionRatio"]) for r in act if (r.get("AbsorptionRatio") or "").strip())
    pf = sorted(float(r["AbsorptionPullFrac"]) for r in act if (r.get("AbsorptionPullFrac") or "").strip())
    q = lambda xs, p: xs[min(len(xs) - 1, int(p * len(xs)))] if xs else float("nan")
    span = f"{rows[0][0]} -> {rows[-1][0]}" if rows else ""
    print(f"{label:5} rows={n:6} active={len(act):5} flagged={len(fl):3} ({len(fl)/n:.3%} of rows, "
          f"{len(fl)/max(1,len(act)):.2%} of active) ratio p50={q(rat,.5):.3f} p75={q(rat,.75):.3f} "
          f"p90={q(rat,.9):.3f} | pullFrac p50={q(pf,.5):.3f} p90={q(pf,.9):.3f} >0.75={sum(x>0.75 for x in pf)/max(1,len(pf)):.1%}  [{span}]")

pre = [x for x in load(f"{FETCH}/analysis_log.csv.116col-83564b1b.20260924_184606.bak")
       if x[0] >= datetime(2026, 9, 1, 15, 50)]
post = load(f"{FETCH}/analysis_log.csv")
print("\nD-2 STOP WATCH (weekday; pre = 116-col book from the 09-01 instrumentation, post = since the 09-24 deploy)")
stop_stats(pre, "pre"); stop_stats(post, "post")
