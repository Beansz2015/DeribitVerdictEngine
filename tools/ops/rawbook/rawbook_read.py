#!/usr/bin/env python3
"""Raw order-book absorption test -- the registered read, with the ruled quarantine.

Spec: docs/raw-book-absorption-test-spec.md (population and read rule in its section 5,
decisions RBA-3 pairing and RBA-4 thresholds in its section 6, quarantine ruling box at the
top of its section 0, trader ruling 2026-10-06 option (d)).

This script implements the pairing rule INDEPENDENTLY of the probe's convenience summary
(tools/RawBookProbe/RawBookProbeProgram.vb, AlignedPairs_Locked). It reads only
episodes_*.csv, samples_*.csv and the quarantine windows CSV.

Usage:
  python tools/ops/rawbook/rawbook_read.py --selftest [--mutate per-episode]
  python tools/ops/rawbook/rawbook_read.py --run-dir <out folder> --windows <mismatch_windows.csv>

APPLICATION CHOICES, FIXED BEFORE ANY pullFrac VALUE WAS READ (2026-10-06). The spec does not
state these details; each is fixed here, in the committed script, before the read runs:
  Q1  Quarantine is applied per PAIR, after pairing on the full population: a pair is dropped
      when EITHER episode's [open_ms, close_ms] overlaps any window [start_ms, end_ms].
      Overlap is inclusive at the ends (open <= end AND close >= start): it drops more, never less.
  Q2  The same per-pair quarantine applies to A-vs-C pairs (S5), S-CONCAT groups (the A episode
      and every summed B episode) and S-INSTANT rows (the A and B episodes the row belongs to).
  Q3  Dropped counts are reported per arm (which episode of the pair overlaps: A only, other
      arm only, both) and per session. Session = engine bucket of the arm A episode's open
      hour, UTC: ASIA 0-7, LONDON 8-12, NY 13-23 (settings.json session_volume).
  P1  PROBE_* episodes are excluded on both arms, including from the "only overlapping
      episode" count. Pairing is within the same side, gen and level (level matched as text).
  P2  Episode overlap for pairing is strict (b.open < a.close AND b.close > a.open).
  P3  pullFrac is recomputed from pull_lb / max(post_lb, 5000); the CSV column is checked
      against it, not used.
  P4  "Equal" in S1 means |B - A| <= 1e-6 (the probe's tolerance). "Exactly 1.000" in S4 means
      |pullFrac - 1| < 5e-5 (the probe's tolerance).
  P5  Veto = pullFrac > max_pull_frac (Core/Indicators_OrderFlow.vb ClassifyAbsorption passes
      at <= ). max_pull_frac = 0.75, read from the run's events log start line.
  V1  The verdict rows are evaluated on POOLED aligned pairs. Flip rates and the median
      |delta pullFrac| use ALL aligned pairs (the spec defines "material" as "a flip rate
      >= 5 % of aligned pairs"). The 200 minimum applies to aligned pairs with arm A
      pullFrac <= 1 (spec section 4).
  V2  Every verdict row is tested. Exactly one row holding = the verdict. None = INCONCLUSIVE.
      More than one = RULE AMBIGUOUS: the script names the rows and gives no verdict.
  E1  Escalation (spec section 0 brief): aligned pairs < half of arm A informative episodes,
      tested before AND after the quarantine; or gen count > 20; or < 200 qualifying pairs.
      Any of these prints NO VERDICT.
  S1  Secondaries S-CONCAT and S-INSTANT get the same verdict rule; S-INSTANT does not apply
      the informative filter (the spec does not name one for it).
"""

import argparse
import csv
import math
import os
import statistics
import sys
from collections import defaultdict
from datetime import datetime, timezone

FLOOR = 5000.0
MAX_PULL_FRAC_DEFAULT = 0.75
EQ_TOL = 1e-6
ONE_TOL = 5e-5
ALIGN_MS = 500
MIN_QUALIFYING = 200
P_SIG = 0.01
FLIP_MATERIAL = 0.05
MED_MATERIAL = 0.05
BALANCE_BAND = 0.10


# ---------------------------------------------------------------------------- basics

def pull_frac(pull, post):
    return pull / max(post, FLOOR)


def session_of(ms):
    h = datetime.fromtimestamp(ms / 1000.0, tz=timezone.utc).hour
    if 0 <= h <= 7:
        return "ASIA"
    if 8 <= h <= 12:
        return "LONDON"
    return "NY"


def binom_two_sided(k, n):
    """Exact two-sided binomial test at p = 0.5 (sign test, exact McNemar)."""
    if n == 0:
        return 1.0
    lo = min(k, n - k)
    tail = sum(math.comb(n, i) for i in range(lo + 1))
    return min(1.0, 2.0 * tail / (2 ** n))


def median(xs):
    return statistics.median(xs) if xs else float("nan")


def is_probe(e):
    return e["reason"].startswith("PROBE_")


def informative(e):
    return e["arm"] == "A" and not is_probe(e) and e["sec"] >= 1.0 and e["folds"] >= 2


def in_quarantine(open_ms, close_ms, windows):
    for ws, we in windows:
        if open_ms <= we and close_ms >= ws:
            return True
    return False


# ---------------------------------------------------------------------------- loading

def load_episodes(path):
    out = []
    with open(path, newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            pull = float(r["pull_lb"])
            post = float(r["post_lb"])
            out.append({
                "arm": r["arm"], "side": r["side"], "gen": int(r["gen"]), "level": r["level"],
                "open": int(r["open_ms"]), "close": int(r["close_ms"]), "reason": r["reason"],
                "sec": float(r["episode_sec"]), "folds": int(r["book_folds"]),
                "pull": pull, "post": post, "pf": pull_frac(pull, post),
                "pf_csv": float(r["pull_frac"]),
            })
    return out


def load_windows(path):
    out = []
    with open(path, newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            out.append((int(r["window_start_ms"]), int(r["window_end_ms"])))
    return out


def load_samples(path):
    out = []
    with open(path, newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            out.append(r)
    return out


def max_pull_frac_from_events(run_dir):
    for name in sorted(os.listdir(run_dir)):
        if name.startswith("events_") and name.endswith(".log"):
            with open(os.path.join(run_dir, name), encoding="utf-8") as f:
                for line in f:
                    if " start " in line and "max_pull_frac=" in line:
                        return float(line.split("max_pull_frac=")[1].split()[0])
    return None


# ---------------------------------------------------------------------------- pairing

def index_other(episodes, arm):
    idx = defaultdict(list)
    for e in episodes:
        if e["arm"] == arm and not is_probe(e):
            idx[(e["side"], e["gen"], e["level"])].append(e)
    return idx


def overlapping(idx, a):
    return [b for b in idx[(a["side"], a["gen"], a["level"])]
            if b["open"] < a["close"] and b["close"] > a["open"]]


def aligned_pairs(episodes, arm):
    """RBA-3 primary: an arm A informative episode and the ONLY other-arm episode (same side,
    gen, level) that overlaps its span, opening and closing within 500 ms of it."""
    idx = index_other(episodes, arm)
    pairs = []
    for a in episodes:
        if not informative(a):
            continue
        ov = overlapping(idx, a)
        if len(ov) != 1:
            continue
        b = ov[0]
        if abs(b["open"] - a["open"]) > ALIGN_MS or abs(b["close"] - a["close"]) > ALIGN_MS:
            continue
        pairs.append((a, b))
    return pairs


def quarantine_pairs(pairs, windows):
    """Ruling (d): drop the PAIR when EITHER episode overlaps a window."""
    kept, dropped = [], []
    for a, b in pairs:
        qa = in_quarantine(a["open"], a["close"], windows)
        qb = in_quarantine(b["open"], b["close"], windows)
        if qa or qb:
            dropped.append((a, b, "both" if (qa and qb) else ("A only" if qa else "other only")))
        else:
            kept.append((a, b))
    return kept, dropped


def read_pairs(episodes, windows, arm, mutate=None):
    """The production path: pair on the full population, THEN quarantine per pair.
    mutate='per-episode' is the wrong implementation the selftest must catch: it removes
    quarantined episodes BEFORE pairing, which can turn a two-overlap A episode into a pair."""
    if mutate == "per-episode":
        clean = [e for e in episodes if not in_quarantine(e["open"], e["close"], windows)]
        return aligned_pairs(clean, arm), []
    return quarantine_pairs(aligned_pairs(episodes, arm), windows)


# ---------------------------------------------------------------------------- statistics

def stats(pairs, max_pf):
    """pairs: list of (a_pull, a_post, b_pull, b_post)."""
    n = len(pairs)
    a_pf = [pull_frac(p[0], p[1]) for p in pairs]
    b_pf = [pull_frac(p[2], p[3]) for p in pairs]
    hi = lo = eq = 0
    for x, y in zip(a_pf, b_pf):
        if x <= 1.0:
            if y > x + EQ_TOL:
                hi += 1
            elif y < x - EQ_TOL:
                lo += 1
            else:
                eq += 1
    p2v = sum(1 for x, y in zip(a_pf, b_pf) if x <= max_pf and y > max_pf)
    v2p = sum(1 for x, y in zip(a_pf, b_pf) if x > max_pf and y <= max_pf)
    bal, neg_den = [], 0
    for p in pairs:
        dpull, dpost = p[2] - p[0], p[3] - p[1]
        den = dpost + dpull
        if den != 0:
            bal.append((dpost - dpull) / den)
            if den < 0:
                neg_den += 1
    absd = [abs(y - x) for x, y in zip(a_pf, b_pf)]
    absd_le1 = [abs(y - x) for x, y in zip(a_pf, b_pf) if x <= 1.0]
    ones = [(x, y) for x, y in zip(a_pf, b_pf) if abs(x - 1.0) < ONE_TOL]
    ones_kept = sum(1 for x, y in ones if abs(y - 1.0) < ONE_TOL)
    dpulls = [p[2] - p[0] for p in pairs]
    dposts = [p[3] - p[1] for p in pairs]
    return {
        "n": n, "qual": hi + lo + eq, "hi": hi, "lo": lo, "eq": eq,
        "p_s1": binom_two_sided(hi, hi + lo),
        "p2v": p2v, "v2p": v2p, "p_flip": binom_two_sided(p2v, p2v + v2p),
        "s3": median(bal), "s3_n": len(bal), "s3_negden": neg_den,
        "med_abs": median(absd), "med_abs_le1": median(absd_le1),
        "ones": len(ones), "ones_kept": ones_kept,
        "med_dpull": median(dpulls), "med_dpost": median(dposts),
    }


def verdict(s):
    if s["qual"] < MIN_QUALIFYING:
        return "INCONCLUSIVE-BY-COUNT", []
    n = s["n"]
    p2v_rate, v2p_rate = s["p2v"] / n, s["v2p"] / n
    s3 = s["s3"]
    hpost = (s["v2p"] > s["p2v"] and s["p_flip"] < P_SIG and s["lo"] > s["hi"] and s["p_s1"] < P_SIG
             and not math.isnan(s3) and s3 > BALANCE_BAND)
    hnet = (((s["p2v"] > s["v2p"] and s["p_flip"] < P_SIG) or (s["hi"] > s["lo"] and s["p_s1"] < P_SIG))
            and not math.isnan(s3) and abs(s3) <= BALANCE_BAND)
    nomat = p2v_rate < FLIP_MATERIAL and v2p_rate < FLIP_MATERIAL and s["med_abs"] < MED_MATERIAL
    holding = [name for name, ok in (("H-POST CONFIRMED", hpost),
                                     ("H-NET CONFIRMED, H-POST REFUTED", hnet),
                                     ("NO MATERIAL DIFFERENCE", nomat)) if ok]
    if len(holding) == 1:
        return holding[0], holding
    if not holding:
        return "INCONCLUSIVE", holding
    return "RULE AMBIGUOUS (no verdict)", holding


def fmt_p(p):
    return "%.3g" % p


def print_stats(label, s, out):
    n = s["n"]
    if n == 0:
        out.append("  %-10s n=0" % label)
        return
    out.append("  %-10s n=%d | S1 (A<=1, n=%d): B higher %d, lower %d, equal %d, sign p=%s"
               % (label, n, s["qual"], s["hi"], s["lo"], s["eq"], fmt_p(s["p_s1"])))
    out.append("  %-10s S2 flips: PASS->VETO %d (%.1f %%), VETO->PASS %d (%.1f %%), McNemar p=%s"
               % ("", s["p2v"], 100.0 * s["p2v"] / n, s["v2p"], 100.0 * s["v2p"] / n, fmt_p(s["p_flip"])))
    out.append("  %-10s S3 balance median %.4f (n=%d nonzero denominators, %d negative)"
               " | median |dPF| all %.4f, A<=1 %.4f | median dPullLB %.0f dPostLB %.0f"
               % ("", s["s3"], s["s3_n"], s["s3_negden"], s["med_abs"], s["med_abs_le1"],
                  s["med_dpull"], s["med_dpost"]))
    out.append("  %-10s S4 A exactly 1.000: %d, B partner also 1.000: %d%s"
               % ("", s["ones"], s["ones_kept"],
                  (" (%.1f %%)" % (100.0 * s["ones_kept"] / s["ones"])) if s["ones"] else ""))


def as_tuples(pairs):
    return [(a["pull"], a["post"], b["pull"], b["post"]) for a, b in pairs]


def split_report(title, tuples, max_pf, out):
    out.append(title)
    pooled = stats(tuples, max_pf)
    print_stats("pooled", pooled, out)
    print_stats("floored", stats([t for t in tuples if t[1] < FLOOR], max_pf), out)
    print_stats("unfloored", stats([t for t in tuples if t[1] >= FLOOR], max_pf), out)
    return pooled


# ---------------------------------------------------------------------------- the read

def run_read(run_dir, windows_path):
    out = []
    ep_files = [f for f in os.listdir(run_dir) if f.startswith("episodes_") and f.endswith(".csv")]
    sm_files = [f for f in os.listdir(run_dir) if f.startswith("samples_") and f.endswith(".csv")]
    if len(ep_files) != 1 or len(sm_files) != 1:
        print("STOP: expected exactly one episodes_*.csv and one samples_*.csv, found %s / %s" % (ep_files, sm_files))
        return 2
    episodes = load_episodes(os.path.join(run_dir, ep_files[0]))
    windows = load_windows(windows_path)
    max_pf = max_pull_frac_from_events(run_dir)
    if max_pf is None:
        print("STOP: max_pull_frac not found in the events log start line")
        return 2
    out.append("==== raw-book absorption read, quarantined (ruling 2026-10-06 option (d)) ====")
    out.append("episodes file %s | windows %d | max_pull_frac %g | floor %g" % (ep_files[0], len(windows), max_pf, FLOOR))

    # Sanity: CSV pull_frac vs recomputed (P3)
    worst = max(abs(e["pf"] - e["pf_csv"]) for e in episodes)
    out.append("check P3: max |recomputed pullFrac - CSV pull_frac| = %.2e (CSV prints 6 decimals)" % worst)

    gens = sorted({e["gen"] for e in episodes})
    out.append("gens %s (escalation if > 20)" % gens)

    # Episode-level exposure, per arm and session (context; cross-checks the ruling box)
    out.append("")
    out.append("-- episode exposure to the quarantine windows (all rows / non-PROBE rows) --")
    for arm in ("A", "B", "C"):
        es = [e for e in episodes if e["arm"] == arm]
        q_all = sum(1 for e in es if in_quarantine(e["open"], e["close"], windows))
        nonp = [e for e in es if not is_probe(e)]
        q_np = sum(1 for e in nonp if in_quarantine(e["open"], e["close"], windows))
        out.append("arm %s: %d of %d rows overlap a window | non-PROBE %d of %d" % (arm, q_all, len(es), q_np, len(nonp)))

    inf_a = [e for e in episodes if informative(e)]
    inf_a_clean = [e for e in inf_a if not in_quarantine(e["open"], e["close"], windows)]
    out.append("arm A informative episodes: %d (not overlapping a window: %d)" % (len(inf_a), len(inf_a_clean)))

    # Primary: aligned pairs, A-B and A-C, quarantined per pair
    results = {}
    for arm in ("B", "C"):
        allp = aligned_pairs(episodes, arm)
        kept, dropped = quarantine_pairs(allp, windows)
        results[arm] = (allp, kept, dropped)
        out.append("")
        out.append("-- A-%s aligned pairs: before quarantine %d, dropped %d, kept %d --" % (arm, len(allp), len(dropped), len(kept)))
        out.append("   dropped by cause: A only %d | %s only %d | both %d"
                   % (sum(1 for d in dropped if d[2] == "A only"), arm,
                      sum(1 for d in dropped if d[2] == "other only"), sum(1 for d in dropped if d[2] == "both")))
        out.append("   %-7s %8s %8s %8s %8s %8s" % ("session", "before", "dropped", "A-ovl", arm + "-ovl", "kept"))
        for sess in ("ASIA", "LONDON", "NY"):
            b = sum(1 for a, _ in allp if session_of(a["open"]) == sess)
            dr = [d for d in dropped if session_of(d[0]["open"]) == sess]
            aov = sum(1 for d in dr if d[2] in ("A only", "both"))
            oov = sum(1 for d in dr if d[2] in ("other only", "both"))
            k = sum(1 for a, _ in kept if session_of(a["open"]) == sess)
            out.append("   %-7s %8d %8d %8d %8d %8d" % (sess, b, len(dr), aov, oov, k))

    # Escalation E1
    allb, keptb, _ = results["B"]
    r_pre = len(allb) / len(inf_a) if inf_a else 0.0
    r_post = len(keptb) / len(inf_a_clean) if inf_a_clean else 0.0
    out.append("")
    out.append("check E1: aligned A-B / arm A informative = %d/%d = %.3f before quarantine, %d/%d = %.3f after (stop if < 0.5)"
               % (len(allb), len(inf_a), r_pre, len(keptb), len(inf_a_clean), r_post))
    escalate = []
    if r_pre < 0.5 or r_post < 0.5:
        escalate.append("aligned pairs below half of arm A informative episodes")
    if len(gens) > 20:
        escalate.append("gen count above 20")

    # Fragmentation share (for the secondaries' comparison)
    idx_b = index_other(episodes, "B")
    frag = defaultdict(int)
    for a in inf_a_clean:
        c = len(overlapping(idx_b, a))
        frag["0" if c == 0 else ("1" if c == 1 else "2+")] += 1
    out.append("fragmentation (arm A informative, not in a window): overlapping B episodes 0: %d | 1: %d | 2+: %d (2+ share %.1f %%)"
               % (frag["0"], frag["1"], frag["2+"], 100.0 * frag["2+"] / max(1, len(inf_a_clean))))

    out.append("")
    out.append("== PRIMARY: A vs B aligned pairs, quarantined ==")
    prim = split_report("", as_tuples(keptb), max_pf, out)
    v, holding = verdict(prim)
    out.append("rows holding: %s" % (holding if holding else "none"))
    if escalate:
        out.append("ESCALATION: %s -> NO VERDICT" % "; ".join(escalate))
        prim_verdict = "NO VERDICT (escalation)"
    else:
        prim_verdict = v
    out.append("PRIMARY VERDICT: %s" % prim_verdict)

    out.append("")
    out.append("== S5: A vs C aligned pairs, quarantined (S1 and S3 are the registered statistics) ==")
    split_report("", as_tuples(results["C"][1]), max_pf, out)

    # S-CONCAT
    out.append("")
    concat, concat_drop = [], 0
    for a in inf_a:
        ov = overlapping(idx_b, a)
        if not ov:
            continue
        if in_quarantine(a["open"], a["close"], windows) or any(in_quarantine(b["open"], b["close"], windows) for b in ov):
            concat_drop += 1
            continue
        concat.append((a["pull"], a["post"], sum(b["pull"] for b in ov), sum(b["post"] for b in ov)))
    out.append("== SECONDARY S-CONCAT (A vs summed overlapping B), quarantined: kept %d, dropped %d ==" % (len(concat), concat_drop))
    sc = split_report("", concat, max_pf, out)
    sc_v, sc_h = verdict(sc)
    out.append("rows holding: %s | S-CONCAT verdict by the same rule: %s" % (sc_h if sc_h else "none", sc_v))

    # S-INSTANT
    samples = load_samples(os.path.join(run_dir, sm_files[0]))
    byk = defaultdict(list)
    for e in episodes:
        byk[(e["arm"], e["side"], e["gen"], e["level"])].append(e)

    def owner(arm, side, gen, level, t):
        for e in byk[(arm, side, gen, level)]:
            if e["open"] - 5 <= t <= e["close"]:
                return e
        return None

    last = {}
    unmapped = probe_skip = 0
    for r in samples:
        if r["a_active"] != "1" or r["b_active"] != "1" or r["a_level"] != r["b_level"]:
            continue
        if abs(float(r["a_sec"]) - float(r["b_sec"])) > 0.5:
            continue
        t, gen = int(r["t_ms"]), int(r["gen"])
        ea = owner("A", r["side"], gen, r["a_level"], t)
        eb = owner("B", r["side"], gen, r["b_level"], t)
        if ea is None or eb is None:
            unmapped += 1
            continue
        if is_probe(ea) or is_probe(eb):
            probe_skip += 1
            continue
        key = (ea["side"], ea["gen"], ea["level"], ea["open"])
        if key not in last or t > last[key][0]:
            last[key] = (t, ea, eb, r)
    inst, inst_drop = [], 0
    for t, ea, eb, r in last.values():
        if in_quarantine(ea["open"], ea["close"], windows) or in_quarantine(eb["open"], eb["close"], windows):
            inst_drop += 1
            continue
        inst.append((float(r["a_pull_lb"]), float(r["a_post_lb"]), float(r["b_pull_lb"]), float(r["b_post_lb"])))
    out.append("")
    out.append("== SECONDARY S-INSTANT (last co-active sample per arm A episode), quarantined: kept %d, dropped %d"
               " (rows unmapped %d, PROBE_ skipped %d) ==" % (len(inst), inst_drop, unmapped, probe_skip))
    si = split_report("", inst, max_pf, out)
    si_v, si_h = verdict(si)
    out.append("rows holding: %s | S-INSTANT verdict by the same rule: %s" % (si_h if si_h else "none", si_v))

    out.append("")
    out.append("SUMMARY: primary %s | S-CONCAT %s | S-INSTANT %s" % (prim_verdict, sc_v, si_v))
    print("\n".join(out))
    return 0


# ---------------------------------------------------------------------------- selftest

def _ep(arm, o, c, pull=1000.0, post=10000.0, reason="TouchCrossed", sec=None, folds=5, level="100", side="ABOVE", gen=1):
    return {"arm": arm, "side": side, "gen": gen, "level": level, "open": o, "close": c, "reason": reason,
            "sec": (c - o) / 1000.0 if sec is None else sec, "folds": folds,
            "pull": pull, "post": post, "pf": pull_frac(pull, post), "pf_csv": pull_frac(pull, post)}


def selftest(mutate):
    fails = []

    def check(name, got, want):
        ok = got == want
        print("  %-62s want %-8s got %-8s %s" % (name, want, got, "ok" if ok else "FAIL"))
        if not ok:
            fails.append(name)

    print("selftest%s" % (" (MUTATION: %s)" % mutate if mutate else ""))
    W = [(13050, 13500), (23050, 23060), (31700, 31800)]
    eps = [
        # Case 1: clean A, its only B partner overlaps a window -> pair dropped.
        _ep("A", 20000, 23000, level="L1"), _ep("B", 20010, 23100, level="L1"),
        # Case 2: A overlaps a window, B clean -> pair dropped.
        _ep("A", 30000, 32000, level="L2"), _ep("B", 30010, 31600, level="L2"),
        # Case 3: A overlaps TWO B episodes; only the small fragment touches a window.
        # Per pair: two overlaps -> no pair at all. Per episode (wrong): the fragment is removed
        # first and A pairs with the survivor.
        _ep("A", 10000, 13000, level="L3"), _ep("B", 10050, 12950, level="L3"),
        _ep("B", 12990, 13100, level="L3"),
        # Case 4: clean pair kept.
        _ep("A", 40000, 42000, level="L4"), _ep("B", 40100, 41900, level="L4"),
        # Case 5: PROBE_ B partner is excluded -> no pair.
        _ep("A", 50000, 52000, level="L5"), _ep("B", 50000, 52000, level="L5", reason="PROBE_END"),
        # Case 6: not informative (1 fold) -> no pair.
        _ep("A", 60000, 62000, level="L6", folds=1), _ep("B", 60000, 62000, level="L6"),
        # Case 7: B closes 600 ms late -> not aligned.
        _ep("A", 70000, 72000, level="L7"), _ep("B", 70000, 72600, level="L7"),
    ]
    kept, dropped = read_pairs(eps, W, "B", mutate)
    kept_levels = sorted(a["level"] for a, _ in kept)
    check("kept pairs are exactly case 4 (L4)", ",".join(kept_levels), "L4")
    if mutate is None:
        check("dropped pairs: cases 1 and 2", ",".join(sorted(d[0]["level"] for d in dropped)), "L1,L2")
        check("case 1 drop cause", [d[2] for d in dropped if d[0]["level"] == "L1"][0], "other only")
        check("case 2 drop cause", [d[2] for d in dropped if d[0]["level"] == "L2"][0], "A only")

    # Statistics primitives
    check("sign test n=10 k=0 p", "%.6f" % binom_two_sided(0, 10), "%.6f" % (2.0 / 1024))
    check("sign test n=0 p", binom_two_sided(0, 0), 1.0)
    check("floor: pullFrac(2000, 100)", pull_frac(2000, 100), 0.4)
    check("no floor: pullFrac(6000, 8000)", pull_frac(6000, 8000), 0.75)
    s = stats([(6000, 8000, 7000, 9000),      # A 0.75 PASS -> B 0.778 VETO, higher
               (8000, 8000, 9000, 9000),      # A 1.000 -> B 1.000 equal
               (9000, 10000, 7000, 10000),    # A 0.9 VETO -> B 0.7 PASS, lower
               (4000, 2000, 4000, 2000)], 0.75)  # floored A 0.8, equal
    check("stats S1 higher/lower/equal", (s["hi"], s["lo"], s["eq"]), (1, 1, 2))
    check("stats S2 PASS->VETO, VETO->PASS", (s["p2v"], s["v2p"]), (1, 1))
    check("stats S4 ones kept", (s["ones"], s["ones_kept"]), (1, 1))
    check("stats S3 median balance (0, 0, -1; zero denominator skipped)", (s["s3"], s["s3_n"]), (0.0, 3))
    check("session of 2026-10-05T13:20Z", session_of(1791206438000), "NY")
    check("session of 2026-10-06T09:00Z", session_of(1791277200000), "LONDON")
    check("session of 2026-10-06T02:00Z", session_of(1791252000000), "ASIA")

    print("SELFTEST %s (%d failed)" % ("PASS" if not fails else "FAIL", len(fails)))
    return 0 if not fails else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--mutate", choices=["per-episode"], default=None,
                    help="selftest only: apply the quarantine per episode before pairing (must go red)")
    ap.add_argument("--run-dir")
    ap.add_argument("--windows")
    args = ap.parse_args()
    if args.selftest:
        return selftest(args.mutate)
    if args.mutate:
        print("STOP: --mutate is for --selftest only")
        return 2
    if not args.run_dir or not args.windows:
        ap.error("--run-dir and --windows are required for the read")
    return run_read(args.run_dir, args.windows)


if __name__ == "__main__":
    sys.exit(main())
