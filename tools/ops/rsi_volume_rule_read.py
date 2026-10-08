#!/usr/bin/env python3
# tools/ops/rsi_volume_rule_read.py
#
# RSI and volume rule study, SESSION 2: the outcome run. Spec of record: docs/rsi-volume-rule-study-spec.md (sections
# 4.7, 4.8, 5, 8); session 1 and the rulings: docs/rsi-volume-rule-study-session1-2026-10-08.md (RVS-1 = (d): every
# indicator on the venue's own candles, trades for entries, the walk and the gap check; RVS-2 = (c): the registered gap
# rule is primary, a descriptive sensitivity drops only on gaps inside the outcome window or the last 100 bars).
# Python 3 standard library only.
#
# Order of work (each step before the next):
#   1. re-run session 1 exactly (tools/ops/rsi_volume_rule_counts.py run(), venue bars) and assert the recomputed
#      frozen thresholds equal docs/rsi-volume-rule-study-frozen.json; a mismatch is a STOP;
#   2. build the episode populations (primary and RVS-2 sensitivity) and the walk list;
#   3. walk every (signal bar, side, session) once, trade by trade, over the dev history stores (parallel per month);
#   4. the pre-registered statistics: VOL-H1..H3 and RSI-H1..H3 per session, Holm within each family of 9, the census
#      label; then the descriptive tables (rules explained, sensitivity, splits, partials, fixed horizon).
#
# Outcome (spec section 4.7, copied from liq-tradeflow-flip-study-spec.md section 4.8 TFS-8):
#   entry = the first trade with Timestamp >= the signal bar's close; its Price.
#   ATR(7) over the last 50 completed bars at the session's resolution, on venue candles (session record RVR-19).
#   target = entry +/- m x ATR (NY 1.75, LONDON 2.0, ASIA 1.25), stop = entry -/+ 1.6 x ATR.
#   window = entry trade time + W (NY 15 min, ASIA and LONDON 45 min). Walk the trades AFTER the entry trade and before
#   the window end; first target or stop touch wins; else timeout at the last trade price before the window end.
#   net EV per trade, bps (DeribitIndicatorProject.md section 5a): +t - fee | -s - fee | mark - fee; fee 3 bps.
#
# Usage (from the repo root):
#   python -I tools/ops/rsi_volume_rule_read.py --stores C:\DeribitData\history-2023-2024 C:\DeribitData\history --cut 2026-10-06 --venue-bars C:/DeribitData/venue-candles --out <file.md>
#   python -I tools/ops/rsi_volume_rule_read.py --selftest

import argparse, json, math, os, random, statistics, sys, tempfile, time
from collections import Counter, defaultdict, deque
from datetime import date, datetime, timedelta
from multiprocessing import Pool
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rsi_volume_rule_counts as rc   # noqa: E402  (session 1: bars, ports, thresholds, episodes; read-only reuse)
import liq_tradeflow_counts as tc     # noqa: E402  (tercile_edges, the exact rank rule)

SEED = 20261008
FLOOR = 100
FAMILIES = {"VOL": [("VOL-H1", "V0", "V-ctl"), ("VOL-H2", "V1", "V0"), ("VOL-H3", "V2", "V0")],
            "RSI": [("RSI-H1", "R0", "R-ctl"), ("RSI-H2", "R1", "R0"), ("RSI-H3", "R2", "R0")]}
SESS = rc.SESSIONS
RULES_ALL = rc.RULES + ("V0p", "R0p")

# ================================================================== COPIED from tools/ops/burst_outcome_read.py
# boot (with that file's one added DRAWS line), pctl, diff_of, halves, compare, readable, sig, label, boot_p, holm and
# finalize_label are copied VERBATIM (burst_outcome_read.py:194-286 and :652-695 at d441494). Do not re-derive them.
# finalize_label is called with sens=None and sens_seam=None, so only its Holm demotion acts.
args = SimpleNamespace(seed=SEED)
R = 10000
DRAWS = []
ALL_LABELS = []
SEEDC = [0]


def pctl(sorted_vals, q):
    if not sorted_vals: return float("nan")
    k = max(1, min(len(sorted_vals), math.ceil(q / 100.0 * len(sorted_vals))))
    return sorted_vals[k - 1]


def boot(rows, contribs, K, stat):
    """contribs(row) -> list of (cell, value). Day-level resampling. Returns (point, lo, hi, counts)."""
    days = {}
    for r in rows:
        cs = contribs(r)
        if not cs: continue
        i = days.get(r["day"])
        if i is None:
            i = days[r["day"]] = len(days)
        for k, v in cs:
            pass
    D = len(days)
    S = [[0.0] * D for _ in range(K)]
    C = [[0] * D for _ in range(K)]
    for r in rows:
        cs = contribs(r)
        if not cs: continue
        i = days[r["day"]]
        for k, v in cs:
            S[k][i] += v
            C[k][i] += 1
    tS = [sum(s) for s in S]; tC = [sum(c) for c in C]
    point = stat(tS, tC)
    SEEDC[0] += 1
    if D == 0:
        DRAWS.append([])  # ADDED (burst_outcome_read.py): keep the draws for Holm
        return point, float("nan"), float("nan"), tC
    rng = random.Random(args.seed * 1000 + SEEDC[0])
    rngD = range(D)
    draws = []
    for _ in range(R):
        idx = rng.choices(rngD, k=D)
        bs = [sum(map(S[k].__getitem__, idx)) for k in range(K)]
        bc = [sum(map(C[k].__getitem__, idx)) for k in range(K)]
        v = stat(bs, bc)
        if v == v:
            draws.append(v)
    draws.sort()
    DRAWS.append(draws)  # ADDED (burst_outcome_read.py): keep the draws for Holm
    return point, pctl(draws, 2.5), pctl(draws, 97.5), tC


def diff_of(a, b):
    return lambda s, c: (s[a] / c[a] - s[b] / c[b]) if c[a] > 0 and c[b] > 0 else float("nan")


def halves(rows):
    return {"FULL": rows, "H1": [r for r in rows if r["half"] == "H1"], "H2": [r for r in rows if r["half"] == "H2"]}


def compare(rows, cell_a, cell_b, value, name, context, need_cells=(0, 1), stat=None, K=2, contribs=None):
    """Two-group comparison with halves and the pre-registered label. cell_a/cell_b: predicates."""
    res = {}
    for hn, hr in halves(rows).items():
        if contribs is None:
            def cb(r, ca=cell_a, cbb=cell_b):
                out_ = []
                if ca(r): out_.append((0, value(r)))
                if cbb(r): out_.append((1, value(r)))
                return out_
            res[hn] = boot(hr, cb, K, stat or diff_of(0, 1))
        else:
            res[hn] = boot(hr, contribs, K, stat)
    lab = label(res, need_cells)
    ALL_LABELS.append((context, name, lab, res["FULL"]))
    return res, lab


def readable(r, need):
    return all(r[3][k] >= FLOOR for k in need)


def sig(r, need):
    return readable(r, need) and r[1] == r[1] and (r[1] > 0 or r[2] < 0)


def label(res, need):
    f, h1, h2 = res["FULL"], res["H1"], res["H2"]
    if not readable(f, need): return "NOT READABLE"
    sgn = lambda x: (x > 0) - (x < 0)
    side = lambda r: "d > 0" if r[0] > 0 else "d < 0"
    if sig(h1, need) and sig(h2, need) and sgn(h1[0]) == sgn(h2[0]): return "CONFIRMED (%s)" % side(h1)
    if sig(h1, need) and readable(h2, need) and sgn(h1[0]) == sgn(h2[0]): return "H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (%s)" % side(h1)
    if sig(h1, need): return "H1 FINDING, H2 CONTRADICTS OR NOT READABLE (%s)" % side(h1)
    if sig(h2, need): return "DISCOVERY ONLY (H2) (%s)" % side(h2)
    return "NO DIFFERENCE SHOWN"


def boot_p(draws):
    if not draws: return 1.0
    n = float(len(draws))
    lo = sum(1 for d in draws if d <= 0) / n
    hi = sum(1 for d in draws if d >= 0) / n
    return min(1.0, 2.0 * min(lo, hi))


def holm(results, m):
    """Holm step-down over the given {session: result}. A NOT READABLE result enters with p = 1. Adds p, alpha_i,
    the Holm-adjusted percentile CI of the FULL draws, and reject (step-down not stopped and adjusted CI excludes 0)."""
    for s, x in results.items():
        x["p"] = boot_p(x["draws"]) if x["label"] != "NOT READABLE" else 1.0
    order = sorted(results, key=lambda s: results[s]["p"])
    stopped = False
    for i, s in enumerate(order):
        x = results[s]
        a_i = 0.05 / (m - i) if (m - i) > 0 else 0.05
        x["alpha"] = a_i
        x["hci"] = (pctl(x["draws"], 100.0 * a_i / 2.0), pctl(x["draws"], 100.0 * (1.0 - a_i / 2.0)))
        excl = x["hci"][0] == x["hci"][0] and (x["hci"][0] > 0 or x["hci"][1] < 0)
        ok = (not stopped) and x["label"] != "NOT READABLE" and x["p"] <= a_i and excl
        if not ok: stopped = True
        x["reject"] = ok


def finalize_label(x, sens, sens_seam=None):
    lab = x["label"]
    if lab.startswith("CONFIRMED") and not x["reject"]:
        lab = "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (%s)" % ("d > 0" if x["res"]["FULL"][0] > 0 else "d < 0")
    if lab not in ("NOT READABLE", "NO DIFFERENCE SHOWN") and sens is not None:
        sp, fp = sens["res"]["FULL"][0], x["res"]["FULL"][0]
        if sp == sp and fp == fp and (sp > 0) != (fp > 0):
            lab += " EDGE-SENSITIVE"
    if lab not in ("NOT READABLE", "NO DIFFERENCE SHOWN") and sens_seam is not None:
        sp, fp = sens_seam["res"]["FULL"][0], x["res"]["FULL"][0]
        if sp == sp and fp == fp and (sp > 0) != (fp > 0):
            lab += " SEAM-SENSITIVE"
    x["seam_pt"] = sens_seam["res"]["FULL"][0] if sens_seam is not None else None
    x["edge_pt"] = sens["res"]["FULL"][0] if sens is not None else None
    x["final"] = lab
    return lab
# ================================================================== end of the copied block


# ---------------------------------------------------------------- the walk (outcome)
def walk_job(job):
    """One month's walks, sorted by signal close T. Streams the month file, then the next month file until every walk
    has ended. A walk enters at the first trade with ts >= T. Touches are tested on the trades AFTER the entry trade
    with ts < entry_ts + W; the first touch is the exit. Without a touch the exit is a timeout at the last such trade's
    price (the entry price if there is none). Every walk keeps tracking that last price to the window end: the
    fixed-horizon mark (spec section 4.7, descriptive). Result: (kind, entry, exit price, target, stop, mark at W)."""
    paths, walks = job
    pending = deque(sorted(walks, key=lambda w: w[1]))
    active = []
    done = {}
    stop_at = max(w[1] for w in walks) + 50 * 60000 + 60000 if walks else 0

    def close(w):
        key, end, lg, tgt, stp, px, mark, kind, xp = w
        done[key] = (kind or "timeout", px, xp if kind else mark, tgt, stp, mark)

    t = None
    for path in paths:
        if not pending and not active:
            break
        with open(path, "rb") as f:
            f.readline()
            for line in f:
                t = int(line[:line.index(b",")])
                if t > stop_at:
                    break
                if not active and (not pending or t < pending[0][1]):
                    continue
                p = float(line.split(b",", 2)[1])
                keep = []
                for w in active:
                    if t >= w[1]:
                        close(w)
                        continue
                    if w[7] is None:
                        if w[2]:
                            if p >= w[3]:
                                w[7], w[8] = "success", p
                            elif p <= w[4]:
                                w[7], w[8] = "stop", p
                        else:
                            if p <= w[3]:
                                w[7], w[8] = "success", p
                            elif p >= w[4]:
                                w[7], w[8] = "stop", p
                    w[6] = p
                    keep.append(w)
                active = keep
                while pending and t >= pending[0][1]:
                    key, T, lg, atr, tm, sm, Wms = pending.popleft()
                    tgt = p + tm * atr if lg else p - tm * atr
                    stp = p - sm * atr if lg else p + sm * atr
                    active.append([key, t + Wms, lg, tgt, stp, p, p, None, None])   # the entry trade is not walked
        if t is not None and t > stop_at:
            break
    keep = []
    for w in active:
        if (t is not None and t >= w[1]) or w[7] is not None:
            # the stream stopped past this walk's window end, or the data ended after a touch: the exit is decided
            # (in the real span only a walk ending within seconds past the cut can take the second arm)
            close(w)
        else:
            keep.append(w)
    for w in keep:
        done[w[0]] = ("open_at_data_end", w[5], w[6], w[3], w[4], w[6])
    for w in pending:
        done[w[0]] = ("no_entry", None, None, None, None, None)
    return done


def month_paths(stores, cut):
    groups, paths = rc.store_paths(stores, cut)
    return {os.path.basename(p)[len("trades_"):-len(".csv")]: p for p in paths}


def run_walks(walks, stores, cut, workers=12, log=print):
    mp = month_paths(stores, cut)
    months = sorted(mp)
    by_m = defaultdict(list)
    for w in walks:
        by_m[(datetime(1970, 1, 1) + timedelta(milliseconds=w[1])).strftime("%Y-%m")].append(w)
    jobs = []
    for m, ws in sorted(by_m.items()):
        i = months.index(m)
        jobs.append(([mp[m]] + ([mp[months[i + 1]]] if i + 1 < len(months) else []), ws))
    out = {}
    t0 = time.time()
    with Pool(workers) as pool:
        for d in pool.imap_unordered(walk_job, jobs):
            out.update(d)
    log("walks: %d in %d month jobs (%.0fs)" % (len(out), len(jobs), time.time() - t0))
    return out


def outcome_of(res, fee, lg):
    kind, px, last, tgt, stp, mark_w = res
    sg = 1.0 if lg else -1.0
    t = abs(tgt - px) / px * 1e4
    s = abs(px - stp) / px * 1e4
    mk = sg * (last - px) / px * 1e4
    if kind == "success":
        ev = t - fee
    elif kind == "stop":
        ev = -s - fee
    else:
        ev = mk - fee
    fh = sg * (mark_w - px) / px * 1e4     # the fixed-horizon mark at W, whatever the exit (descriptive)
    return {"ev": ev, "t": t, "s": s, "res": kind, "fh": fh - fee}


# ---------------------------------------------------------------- calendar splits (descriptive)
def last_sunday(y, m):
    d = date(y + (m == 12), m % 12 + 1, 1) - timedelta(days=1)
    return d - timedelta(days=(d.weekday() + 1) % 7)


def nth_sunday(y, m, n):
    d = date(y, m, 1)
    d += timedelta(days=(6 - d.weekday()) % 7)
    return d + timedelta(days=7 * (n - 1))


def dst_regime(d):
    eu = last_sunday(d.year, 3) <= d < last_sunday(d.year, 10)
    us = nth_sunday(d.year, 3, 2) <= d < nth_sunday(d.year, 11, 1)
    return "both summer" if eu and us else ("both winter" if not eu and not us else "US summer, EU winter")


# ---------------------------------------------------------------- the run
def f1(x):
    return "n/a" if x is None or x != x else "%+.2f" % x


def ci(pt, lo, hi):
    return "%s [%s, %s]" % (f1(pt), f1(lo), f1(hi))


def run(a, out):
    P = rc.load_pins(a.settings)
    fee = P["fee_rt_bps"]
    if fee != 3.0 or P["fee_style"] != "maker_maker":
        raise SystemExit("STOP: fee pin is 3 bps maker/maker at v69")
    stores = [s.replace("\\", "/") for s in a.stores]
    cut = datetime.strptime(a.cut, "%Y-%m-%d")
    log = lambda *x: print(*x, file=sys.stderr, flush=True)
    b1, b3, gpre, rows, by_sess, D, h1, F, _ = rc.run(stores, cut, P, out=log, workers=a.workers, venue_dir=a.venue_bars)
    with open(a.frozen, encoding="utf-8") as f:
        frozen = json.load(f)
    if json.loads(json.dumps(F)) != frozen:
        raise SystemExit("STOP: recomputed thresholds differ from %s (escalation trigger, session record section 13)" % a.frozen)
    out("# RSI and volume rule study — session 2 outcome read (tool output)")
    out("")
    out("- Spec: `docs/rsi-volume-rule-study-spec.md`. Rulings `RVS-1` = (d), `RVS-2` = (c) (`docs/rsi-volume-rule-study-session1-2026-10-08.md`).")
    out("- Recomputed frozen thresholds equal `%s`: **yes**. Bars: %s." % (a.frozen, F["meta"]["bars_source"]))
    out("- Pins: settings v%d; fee %.1f bps maker/maker; targets x ATR %s; stop x%.2f; W %s; seed %d; %d resamples; readable n >= %d."
        % (P["version"], fee, P["tgt"], P["stop"], rc.HOLD_MIN, SEED, R, FLOOR))
    out("- Halves: D = %d; H1 %s .. %s; H2 %s .. %s." % (D, F["meta"]["H1_first"], F["meta"]["H1_last"], F["meta"]["H2_first"], F["meta"]["H2_last"]))
    cnt, eps, eps_s = rc.episodes(rows, F, h1)
    # partial votes (descriptive): de-clustered by the same rule, primary population only
    eps_p = []
    last = {}
    for r in rows:
        if "rsi" not in r:
            continue
        for rule, side in rc.partials(r, F).items():
            prev = last.get((rule, side))
            last[(rule, side)] = r["cm"]
            if r["drop"] is None and (prev is None or r["cm"] - prev >= rc.HOLD_MIN[r["sess"]]):
                eps_p.append((rule, side, r))
    # one walk per (signal bar close, side, session)
    walks = {}
    for rule, side, r in eps + eps_s + eps_p:
        key = (r["cm"], side, r["sess"])
        if key in walks:
            continue
        b = b1 if rc.RES_OF[r["sess"]] == 1 else b3
        atr = rc.atr_at(b, r["i"], P["atr_period"])
        walks[key] = (key, r["cm"] * 60000, side == "L", atr, P["tgt"][r["sess"]], P["stop"], rc.HOLD_MIN[r["sess"]] * 60000)
    log("episodes: primary %d, sensitivity %d, partial %d; unique walks %d" % (len(eps), len(eps_s), len(eps_p), len(walks)))
    res = run_walks(list(walks.values()), stores, cut, a.workers, log)
    kinds = Counter(v[0] for v in res.values())
    out("- Walks: %d unique (signal bar, side, session); results %s." % (len(res), dict(kinds)))
    h1_last = max(h1)
    # ATR tercile edges per session (bps of close), from an even sample of eligible bars (outcome-blind)
    edges = {}
    for s in SESS:
        b = b1 if rc.RES_OF[s] == 1 else b3
        el = [r for r in by_sess[s] if r["drop"] is None]
        step = max(1, len(el) // 20000)
        edges[s] = tc.tercile_edges([rc.atr_at(b, r["i"], P["atr_period"]) / r["close"] * 1e4 for r in el[::step]])
    out("- ATR tercile edges (bps of close, per session, from eligible bars): %s"
        % "  ".join("%s %.2f / %.2f" % (s, edges[s][0], edges[s][1]) for s in SESS))

    def mk_rows(ep_list):
        rr = []
        drop = Counter()
        for rule, side, r in ep_list:
            key = (r["cm"], side, r["sess"])
            w = walks[key]
            x = res[key]
            if x[0] in ("no_entry", "open_at_data_end"):
                drop[x[0]] += 1
                continue
            o = outcome_of(x, fee, side == "L")
            d = (datetime(1970, 1, 1) + timedelta(days=r["day"])).date()
            atr_bps = w[3] / r["close"] * 1e4
            e = edges[r["sess"]]
            rr.append(dict(o, rule=rule, side=side, sess=r["sess"], day=r["day"], half="H1" if r["day"] <= h1_last else "H2",
                           year=d.year, dst=dst_regime(d), atr_t=1 if atr_bps <= e[0] else (2 if atr_bps <= e[1] else 3)))
        return rr, drop

    prim, dp = mk_rows(eps)
    sens, ds = mk_rows(eps_s)
    part, dpp = mk_rows(eps_p)
    out("- Episodes with an outcome: primary %d (dropped %s), sensitivity %d (dropped %s), partial %d (dropped %s)."
        % (len(prim), dict(dp), len(sens), dict(ds), len(part), dict(dpp)))
    out("")

    # ---- rules explained
    out("## 1. Rules explained (primary population; net EV per trade, bps; 95 % day-bootstrap CI)")
    out("")
    out("| Session | Rule | n | Success % | Stop % | Timeout % | Gross breakeven % | Net breakeven % | Net edge pp | Net EV per trade [95 % CI] |")
    out("|---|---|---:|---:|---:|---:|---:|---:|---:|---|")
    for s in SESS:
        for ru in RULES_ALL:
            rs = [r for r in (part if ru.endswith("p") else prim) if r["sess"] == s and r["rule"] == ru]
            n = len(rs)
            if not n:
                continue
            succ = 100.0 * sum(1 for r in rs if r["res"] == "success") / n
            stp = 100.0 * sum(1 for r in rs if r["res"] == "stop") / n
            to = 100.0 * sum(1 for r in rs if r["res"] == "timeout") / n
            den = sum(r["t"] + r["s"] for r in rs)
            gbe = 100.0 * sum(r["s"] for r in rs) / den
            nbe = 100.0 * sum(r["s"] + fee for r in rs) / den
            b = boot(rs, lambda r: [(0, r["ev"])], 1, lambda s_, c: s_[0] / c[0] if c[0] > 0 else float("nan"))
            out("| %s | %s%s | %d | %.1f | %.1f | %.1f | %.1f | %.1f | %+.1f | %s |"
                % (s, ru, " (partial, descriptive)" if ru.endswith("p") else "", n, succ, stp, to, gbe, nbe, succ - nbe, ci(*b[:3])))
    out("")

    # ---- the pre-registered hypotheses
    labels = {}
    for fam, hyps in FAMILIES.items():
        results = {}
        for hid, A, B in hyps:
            for s in SESS:
                rs = [r for r in prim if r["sess"] == s and r["rule"] in (A, B)]
                n0 = len(DRAWS)
                rres, lab = compare(rs, lambda r, A=A: r["rule"] == A, lambda r, B=B: r["rule"] == B, lambda r: r["ev"],
                                    "%s %s - %s" % (hid, A, B), "%s %s" % (hid, s))
                results[(hid, s)] = {"res": rres, "label": lab, "draws": DRAWS[n0]}
        holm(results, len(results))
        out("## 2.%s %s family: Holm over %d tests (familywise alpha 0.05); A - B net EV per trade, bps" % ("1" if fam == "VOL" else "2", fam, len(results)))
        out("")
        out("| Hypothesis | Session | FULL d [95 % CI] (nA/nB) | H1 d [95 % CI] (nA/nB) | H2 d [95 % CI] (nA/nB) | boot p | Holm alpha | Holm-adjusted FULL CI | Label |")
        out("|---|---|---|---|---|---|---|---|---|")
        for hid, A, B in hyps:
            for s in SESS:
                x = results[(hid, s)]
                lab = finalize_label(x, None, None)
                labels[(hid, s)] = lab
                rr = x["res"]
                out("| %s (%s - %s) | %s | %s (%d/%d) | %s (%d/%d) | %s (%d/%d) | %.4f | %.4f | [%s, %s] | %s |"
                    % (hid, A, B, s, ci(*rr["FULL"][:3]), rr["FULL"][3][0], rr["FULL"][3][1], ci(*rr["H1"][:3]), rr["H1"][3][0], rr["H1"][3][1],
                       ci(*rr["H2"][:3]), rr["H2"][3][0], rr["H2"][3][1], x["p"], x["alpha"], f1(x["hci"][0]), f1(x["hci"][1]), lab))
        out("")

    # ---- descriptive: RVS-2 sensitivity, splits
    def dboot(rs, A, B):
        return boot(rs, lambda r: [(0, r["ev"])] if r["rule"] == A else ([(1, r["ev"])] if r["rule"] == B else []), 2, diff_of(0, 1))

    out("## 3. Descriptive only — no label, no significance claim")
    out("")
    out("### 3.1 `RVS-2` = (c) gap sensitivity: FULL d on the sensitivity population")
    out("")
    out("| Hypothesis | Session | Primary FULL d (nA/nB) | Sensitivity FULL d [95 % CI] (nA/nB) | Sign flip |")
    out("|---|---|---|---|---|")
    for fam, hyps in FAMILIES.items():
        for hid, A, B in hyps:
            for s in SESS:
                b = dboot([r for r in sens if r["sess"] == s], A, B)
                pr = [r for r in prim if r["sess"] == s]
                pa = [r["ev"] for r in pr if r["rule"] == A]; pb = [r["ev"] for r in pr if r["rule"] == B]
                pd = statistics.mean(pa) - statistics.mean(pb)
                out("| %s | %s | %s (%d/%d) | %s (%d/%d) | %s |" % (hid, s, f1(pd), len(pa), len(pb), ci(*b[:3]), b[3][0], b[3][1],
                                                                 "YES" if (pd > 0) != (b[0] > 0) else "no"))
    out("")
    splits = [("side", lambda r: {"L": "long", "S": "short"}[r["side"]]), ("year", lambda r: str(r["year"])),
              ("daylight saving", lambda r: r["dst"]), ("ATR tercile", lambda r: "T%d" % r["atr_t"])]
    out("### 3.2 Splits of each hypothesis (FULL d [95 %% CI] (nA/nB); the split key is the episode's own)")
    out("")
    for sname, kf in splits:
        keys = sorted(set(kf(r) for r in prim))
        out("**By %s**" % sname)
        out("")
        out("| Hypothesis | Session | " + " | ".join(keys) + " |")
        out("|---|---|" + "---|" * len(keys))
        for fam, hyps in FAMILIES.items():
            for hid, A, B in hyps:
                for s in SESS:
                    cells = []
                    for k in keys:
                        b = dboot([r for r in prim if r["sess"] == s and kf(r) == k], A, B)
                        cells.append("%s (%d/%d)" % (ci(*b[:3]), b[3][0], b[3][1]))
                    out("| %s | %s | %s |" % (hid, s, " | ".join(cells)))
        out("")
    out("### 3.3 Fixed-horizon net return at W (the last trade before the window end, whatever the exit), bps")
    out("")
    out("| Session | Rule | n | Mean net [95 % CI] |")
    out("|---|---|---:|---|")
    for s in SESS:
        for ru in rc.RULES:
            rs = [r for r in prim if r["sess"] == s and r["rule"] == ru]
            b = boot(rs, lambda r: [(0, r["fh"])], 1, lambda s_, c: s_[0] / c[0] if c[0] > 0 else float("nan"))
            out("| %s | %s | %d | %s |" % (s, ru, len(rs), ci(*b[:3])))
    out("")
    out("## 4. Labels")
    out("")
    for (hid, s), lab in sorted(labels.items()):
        out("- %s %s: %s" % (hid, s, lab))
    return labels


# ---------------------------------------------------------------- selftest
def selftest(out=print):
    fails = []

    def check(name, got, want):
        ok = got == want
        out("  %-72s %s  got=%r want=%r" % (name, "PASS" if ok else "FAIL", got, want))
        if not ok:
            fails.append(name)

    tmp = tempfile.mkdtemp(prefix="rvread_")
    HDR = "Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq,MarkPrice,IndexPrice,TickDirection,Contracts\n"

    def write(name, trades):
        p = os.path.join(tmp, name)
        with open(p, "w", encoding="utf-8") as f:
            f.write(HDR)
            for t, px in trades:
                f.write("%d,%.2f,10.00,buy,none,1,1,0,0,0,1\n" % (t, px))
        return p

    T = 1_700_000_000_000
    # walk: (key, T, long, atr, tmult, smult, Wms); atr 10 -> long target +17.5, stop -16
    W = 15 * 60000
    a = write("a.csv", [(T - 1000, 100.0), (T, 1000.0), (T + 1000, 1017.0), (T + 2000, 1017.5),
                        (T + 3000, 999.0)])
    out("SELFTEST: walk")
    d = walk_job(([a], [("w1", T, True, 10.0, 1.75, 1.6, W)]))
    check("long success at the first trade >= target; entry = first trade >= T", d["w1"][:3], ("success", 1000.0, 1017.5))
    b = write("b.csv", [(T + 5, 1000.0), (T + 10, 984.0), (T + 20, 1020.0)])
    d = walk_job(([b], [("w2", T, True, 10.0, 1.75, 1.6, W)]))
    check("long stop at <= entry - 16 before the target", d["w2"][:3], ("stop", 1000.0, 984.0))
    c = write("c.csv", [(T, 1000.0), (T + 60000, 1005.0), (T + W - 1, 1003.0), (T + W, 900.0)])
    d = walk_job(([c], [("w3", T, True, 10.0, 1.75, 1.6, W)]))
    check("timeout marks the last trade before entry + W (a stop touch at the end is ignored)", d["w3"][:3], ("timeout", 1000.0, 1003.0))
    e = write("e.csv", [(T, 1000.0), (T + 10, 982.5), (T + 20, 1017.0)])
    d = walk_job(([e], [("w4", T, False, 10.0, 1.75, 1.6, W)]))
    check("short success at <= entry - 17.5", d["w4"][:3], ("success", 1000.0, 982.5))
    f_ = write("f.csv", [(T, 1000.0), (T, 1020.0)])
    d = walk_job(([f_], [("w5", T, True, 10.0, 1.75, 1.6, W)]))
    check("a same-ms trade AFTER the entry trade is walked", d["w5"][:3], ("success", 1000.0, 1020.0))
    g1 = write("g1.csv", [(T, 1000.0), (T + 1000, 1001.0)])
    g2 = write("g2.csv", [(T + 120000, 1018.0)])
    d = walk_job(([g1, g2], [("w6", T, True, 10.0, 1.75, 1.6, W)]))
    check("a walk spills into the next month file", d["w6"][:3], ("success", 1000.0, 1018.0))
    k = write("k.csv", [(T, 1000.0), (T + 1000, 1018.0), (T + 2000, 1005.0), (T + W + 5, 1006.0)])
    d = walk_job(([k], [("w8", T, True, 10.0, 1.75, 1.6, W)]))
    check("after a touch the walk keeps the window-end mark (exit 1018, mark at W 1005)", d["w8"], ("success", 1000.0, 1018.0, 1017.5, 984.0, 1005.0))
    m1 = write("m1.csv", [(T, 1000.0), (T + 1000, 1001.0), (T + 60 * 60000, 1002.0)])
    d = walk_job(([m1], [("w9", T, True, 10.0, 1.75, 1.6, W)]))
    check("a stream stopped past the window end closes the walk as a timeout", d["w9"][:3], ("timeout", 1000.0, 1001.0))
    n_ = write("n.csv", [(T + 30000, 1000.0), (T + W + 10000, 1004.0), (T + W + 40000, 1006.0)])
    d = walk_job(([n_], [("w10", T, True, 10.0, 1.75, 1.6, W)]))
    check("the window runs from the ENTRY trade (entry T+30 s; a trade at T+W+10 s is inside)", d["w10"][:3], ("timeout", 1000.0, 1004.0))
    h = write("h.csv", [(T - 5, 1000.0)])
    d = walk_job(([h], [("w7", T, True, 10.0, 1.75, 1.6, W)]))
    check("no trade at or after T -> no_entry", d["w7"][0], "no_entry")
    out("SELFTEST: outcome (DeribitIndicatorProject.md section 5a)")
    o = outcome_of(("success", 1000.0, 1017.5, 1017.5, 984.0, 1010.0), 3.0, True)
    check("success: +t - fee, t = 175 bps; fixed horizon +100 - 3", (round(o["t"], 6), round(o["ev"], 6), round(o["fh"], 6)), (175.0, 172.0, 97.0))
    o = outcome_of(("stop", 1000.0, 984.0, 1017.5, 984.0, 990.0), 3.0, True)
    check("stop: -s - fee, s = 160 bps", round(o["ev"], 6), -163.0)
    o = outcome_of(("timeout", 1000.0, 997.0, 982.5, 1016.0, 997.0), 3.0, False)
    check("short timeout: signed mark - fee (+30 - 3)", round(o["ev"], 6), 27.0)
    out("SELFTEST: calendar")
    check("EU summer starts on the last Sunday of March 2025", last_sunday(2025, 3), date(2025, 3, 30))
    check("US summer starts on the second Sunday of March 2025", nth_sunday(2025, 3, 2), date(2025, 3, 9))
    check("2025-03-20 is US summer, EU winter", dst_regime(date(2025, 3, 20)), "US summer, EU winter")
    out("SELFTEST: copied statistics")
    check("boot_p of all-positive draws", boot_p([1.0] * 10), 0.0)
    rs = [{"day": d_, "half": "H1" if d_ < 5 else "H2", "g": g, "v": v} for d_ in range(10) for g, v in ((0, 1.0 + d_ % 3), (1, 0.0))]
    old_R = globals()["R"]
    globals()["R"] = 500
    b = boot(rs, lambda r: [(r["g"], r["v"])], 2, diff_of(0, 1))
    globals()["R"] = old_R
    check("boot point = mean(A) - mean(B)", round(b[0], 6), round(sum(1.0 + d_ % 3 for d_ in range(10)) / 10, 6))
    out("SELFTEST: %s" % ("ALL PASS" if not fails else "FAILURES: %s" % fails))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description="RSI and volume rule study, session 2 (the outcome read)")
    ap.add_argument("--stores", nargs="+", default=rc.DEFAULT_STORES)
    ap.add_argument("--cut", default="2026-10-06")
    ap.add_argument("--venue-bars", default="C:/DeribitData/venue-candles")
    ap.add_argument("--frozen", default=rc.FROZEN_PATH)
    ap.add_argument("--settings", default="settings.json")
    ap.add_argument("--workers", type=int, default=12)
    ap.add_argument("--out", help="write the markdown output here (default stdout)")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        sys.exit(selftest())
    lines = []
    out = (lambda s: lines.append(s)) if a.out else print
    run(a, out)
    if a.out:
        with open(a.out, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
