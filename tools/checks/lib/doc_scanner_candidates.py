"""tools/checks/lib/doc_scanner_candidates.py -- harness 4 (doc scanner) candidate enumeration.

STATUS 2026-09-23 (UTC): the PowerShell side (tools/checks/doc-scanner.ps1) is NOT built yet.
The build STOPPED at docs/doc-scanner-check-spec.md section 0's escalation trigger: that spec's
section 4.2 rules, as written, leave 17 never_shipped lines at cbc2c91 against the <= 2 its
section 5 item 3 requires. Record and measured options: docs/doc-scanner-build-spec-back.md.

Spec: docs/doc-scanner-check-spec.md. Called by tools/checks/doc-scanner.ps1, which owns the
gates, the Jev calls and the report (spec decision DS-D2). This file owns only CODE facts:
which lines are candidates, and what the truth is for each one. It never judges tense or
meaning -- that is the Jev question, asked by the PowerShell side.

WHY IT IMPORTS THE MEASURED INSTRUMENT UNCHANGED. tools/checks/measure/doc-scanner/
enumerators.py is the instrument that produced every number in
docs/doc-scanner-measurement-2026-09-22.md. This module imports it and does NOT edit it, so
replay_recall.py and scan_head.py still reproduce the recorded numbers byte for byte. A
parity check against a modified copy of the instrument would be circular. The one behaviour
this build changes -- the E2 number-to-key pairing (spec section 4.2, decision DS-D4) -- lives
HERE as e2_strict(); the measured pairing stays callable as enumerators.e2 (`--e2 legacy`).

Every regex below is a raw string (spec section 0, trap 4: a non-raw \\b or \\1 writes a
control byte into a file).

Subcommands:
  scan    --rev REV [--docs PATTERN ...] [--e2 strict|legacy] --out FILE
          Enumerate candidates at REV (read with `git show REV:path`, never the working tree --
          decision DS-D7) and write a JSON document of candidates plus computed facts.
  replay  [--kinds kept|all] [--e2 strict|legacy]
          Replay the enumerators at the parent of each of the four known doc-rot fixes, in
          replay_recall.py's exact output format (spec section 5, acceptance item 1).
"""
import argparse, fnmatch, json, os, re, sys
from collections import Counter
from functools import lru_cache

HERE = os.path.dirname(os.path.abspath(__file__))
INSTRUMENT_DIR = os.path.normpath(os.path.join(HERE, '..', 'measure', 'doc-scanner'))
sys.path.insert(0, INSTRUMENT_DIR)
import enumerators as E  # noqa: E402  -- the measured instrument, imported unmodified

# ---------------------------------------------------------------------------------------------
# Decision DS-D1: the living set. A NAMED list, plus the newest seat-handover-*.md by filename.
# Deriving it from prose tables was rejected on mechanism (a parse failure would silently shrink
# the scope). A new living doc must be added here by hand; the tool prints the list every run.
# The list is the one in docs/doc-scanner-measurement-2026-09-22.md section 2 and in
# tools/checks/measure/doc-scanner/scan_head.py's LIVING set, minus its dated handover entry.
# ---------------------------------------------------------------------------------------------
LIVING_NAMED = [
    'CLAUDE.md',
    'docs/DeribitIndicatorProject.md',
    'docs/architecture.md',
    'docs/trader-profile.md',
    'docs/trader-tick-queue.md',
    'docs/roadmap.md',
    'docs/backlog-dependency-map.md',
    'docs/csv-rotation-riders.md',
    'docs/harness-shadow-mode-protocol.md',
    'docs/UserManual.md',
    'docs/aws-collector-deploy-checklist.md',
]
LIVING_EXPECTED = 12  # the 11 named + the newest handover (spec section 4.5 ENUMERATOR_SUSPECT)

# Decision DS-D9: the dated-state horizon is the MEASURED value. Any other value is unmeasured.
DATED_STATE_HORIZON_DAYS = 7

# Jev state budget per item (not specced; see docs/doc-scanner-build-spec-back.md). Jev's state
# limit is 32k tokens; docs/doc-scanner-measurement-2026-09-22.md section 1 measured 2.29 B/token,
# and one docs/DeribitIndicatorProject.md table row runs past 9,000 tokens. Lines longer than
# these caps are windowed around the candidate's column, and the cut is marked in the text.
MAX_LINE_CHARS = 6000
MAX_CONTEXT_LINE_CHARS = 1500
CONTEXT_LINES = 2


def _utc_date(rev):
    import datetime as _dt
    ts = int(E.git('log', '-1', '--format=%ct', rev).strip())
    return _dt.datetime.fromtimestamp(ts, _dt.timezone.utc).strftime('%Y-%m-%d')


def rev_full(rev):
    out = E.git('rev-parse', '--verify', rev + '^{commit}').strip()
    if not re.fullmatch(r'[0-9a-f]{40}', out):
        raise SystemExit(f'REV_UNRESOLVED: {rev!r}')
    return out


@lru_cache(None)
def handovers(rev):
    return sorted({E.HAND_RX.search(f).group(1) for f in E.tree_files(rev) if E.HAND_RX.search(f)})


def newest_handover_path(rev):
    hs = handovers(rev)
    return f'docs/seat-handover-{hs[-1]}.md' if hs else None


def living_set(rev):
    files = E.tree_files(rev)
    docs = [p for p in LIVING_NAMED if p in files]
    missing = [p for p in LIVING_NAMED if p not in files]
    nh = newest_handover_path(rev)
    if nh and nh in files:
        docs.append(nh)
    else:
        missing.append('docs/seat-handover-<newest>.md')
    return docs, missing


def expand_docs(rev, patterns):
    files = sorted(E.tree_files(rev))
    out, unmatched = [], []
    for pat in patterns:
        pat = pat.replace('\\', '/')
        hits = [f for f in files if fnmatch.fnmatchcase(f, pat)]
        if not hits:
            unmatched.append(pat)
        for h in hits:
            if h not in out:
                out.append(h)
    return out, unmatched


# ---------------------------------------------------------------------------------------------
# E2 strict pairing -- spec section 4.2, decision DS-D4. The measured parser (enumerators.e2)
# took the first number within 22 characters after a key name, and allowed the words is/of/at
# in between. Required instead:
#   1. The number follows the key with ONLY ` * ( = : whitespace between them, or sits inside
#      the same backtick span as the key.
#   2. A second key name between the key and the number cancels the pair.
#   3. A slash pair (`A`/`B` 1.75/1.6) pairs by position. A key in a slash group pairs by
#      position or not at all.
#   4. never_shipped hits that survive are reported code-only (the caller routes them).
# The name index, the multi-path disambiguation and the was/never-shipped split are the
# instrument's own (build_name_index, the parent-segment test, the ever-shipped set), unchanged.
# ---------------------------------------------------------------------------------------------
# A number TOKEN: not glued to a following word character ("20th" is not the number 20) and not
# followed by a further ".digit". A sentence-final full stop ("threshold: 4.") still ends a token.
NUM_TOKEN = re.compile(r'(?<![\w.])-?\d+(?:\.\d+)?(?!\w)(?!\.\d)')
GAP_RULE1 = re.compile(r'[`*(=:\s]*')
# Between two key NAMES of one slash group: a slash, optionally wrapped in backticks, asterisks
# or whitespace, and optionally followed by the next key's dotted prefix (`cfg.Scoring.X`/`Y`).
SLASH_BETWEEN_KEYS = re.compile(r'[`*\s]*/[`*\s]*(?:[A-Za-z_]\w*\.)*')
NUM_GROUP = re.compile(r'(?<![\w.])(-?\d+(?:\.\d+)?)((?:\s*/\s*-?\d+(?:\.\d+)?)*)(?!\w)(?!\.\d)')


def backtick_spans(line):
    """Inline code spans as (start, end) half-open intervals over the characters INSIDE the
    backticks. Single-backtick pairs only, left to right; an unpaired trailing backtick opens
    no span."""
    spans, i, n = [], 0, len(line)
    while i < n:
        a = line.find('`', i)
        if a < 0:
            break
        b = line.find('`', a + 1)
        if b < 0:
            break
        spans.append((a + 1, b))
        i = b + 1
    return spans


def _span_of(spans, pos):
    for a, b in spans:
        if a <= pos < b:
            return (a, b)
    return None


def _resolve(idx, nm, line):
    """The instrument's own disambiguation, verbatim in effect (enumerators.e2 lines 109-114)."""
    paths = idx[nm]
    if len(paths) > 1:
        cand = [p for p in paths if any(seg.lower() in line.lower()
                                        for seg in re.sub(r'\[\d+\]', '', p).split('.')[-2:-1])]
        if len(cand) != 1:
            return None
        paths = cand
    return next(iter(paths))


def pair_line(line, rx, idx, span_arm=True):
    """Return [(key_match, number_start, number_text)] pairs for one line under rules 1-3.
    span_arm=False drops rule 1's second arm ("or sits inside the same backtick span"); it
    exists ONLY so tools/checks/measure/doc-scanner/pairing_variants.py can measure that
    reading. The tool itself always runs the spec-literal default."""
    keys = list(rx.finditer(line))
    if not keys:
        return []
    spans = backtick_spans(line)
    pairs = []
    in_group = set()

    # Rule 3 first: find maximal slash groups K1 / K2 / ... / Kn (n >= 2).
    k = 0
    while k < len(keys):
        group = [keys[k]]
        j = k
        while j + 1 < len(keys):
            between = line[keys[j].end():keys[j + 1].start()]
            if SLASH_BETWEEN_KEYS.fullmatch(between):
                group.append(keys[j + 1])
                j += 1
            else:
                break
        if len(group) >= 2:
            for g in group:
                in_group.add(g.start())
            gap = GAP_RULE1.match(line, group[-1].end())
            ng = NUM_GROUP.match(line, gap.end())
            if ng:
                nums = [ng.group(1)] + re.findall(r'-?\d+(?:\.\d+)?', ng.group(2))
                if len(nums) == len(group):
                    # positions of each number inside the group, for the item's column
                    pos = ng.start()
                    for g, num in zip(group, nums):
                        at = line.find(num, pos)
                        pairs.append((g, at, num))
                        pos = at + len(num)
            k = j + 1
        else:
            k += 1

    for km in keys:
        if km.start() in in_group:
            continue
        # Rule 1: only ` * ( = : whitespace between the key and a number token.
        gap = GAP_RULE1.match(line, km.end())
        nm = NUM_TOKEN.match(line, gap.end())
        if nm:
            pairs.append((km, nm.start(), nm.group(0)))
            continue
        # Rule 1, second arm: the number sits inside the same backtick span as the key.
        if not span_arm:
            continue
        sp = _span_of(spans, km.start())
        if not sp:
            continue
        first_num = None
        for t in NUM_TOKEN.finditer(line, km.end(), sp[1]):
            first_num = t
            break
        if not first_num:
            continue
        # Rule 2: a second key name between the key and the number cancels the pair.
        if any(km.end() <= o.start() < first_num.start() for o in keys if o is not km):
            continue
        pairs.append((km, first_num.start(), first_num.group(0)))
    pairs.sort(key=lambda t: t[0].start())
    return pairs


@lru_cache(None)
def _name_rx(rev):
    ver, flat = E.settings_at(rev)
    idx = E.build_name_index(flat)
    names = sorted(idx, key=len, reverse=True)
    rx = re.compile(r'(?<![\w])(' + '|'.join(map(re.escape, names)) + r')(?![\w])') if names else None
    return idx, rx


def e2_strict(path, text, rev, span_arm=True, name_qualify=False):
    """Same tuple shape as enumerators.e2, plus a trailing dict of computed facts.
    The tool calls this with the defaults, which are the spec-literal rules 1-3. span_arm and
    name_qualify are measurement switches for pairing_variants.py ONLY -- name_qualify is NOT
    in docs/doc-scanner-check-spec.md section 4.2: it requires a single-word leaf name
    (`threshold`, `penalty`, `period`...) to have its parent segment on the line as a word."""
    ver, flat = E.settings_at(rev)
    hist = E.settings_history(rev)
    idx, rx = _name_rx(rev)
    out = []
    if not rx:
        return out
    for i, line in enumerate(text.splitlines(), 1):
        for km, col, numtxt in pair_line(line, rx, idx, span_arm=span_arm):
            nm = km.group(1)
            p = _resolve(idx, nm, line)
            if p is None:
                continue
            if name_qualify:
                segs = re.sub(r'\[\d+\]', '', p).split('.')
                if '_' not in segs[-1] and (len(segs) < 2 or not re.search(
                        r'(?<![\w])' + re.escape(segs[-2]) + r'(?![\w])', line, re.I)):
                    continue
            val = float(numtxt)
            cur = flat[p]
            if abs(val - float(cur)) < 1e-9:
                continue
            ever = hist.get(p, set())
            kind = 'E2_value_was_shipped' if any(abs(val - e) < 1e-9 for e in ever) else 'E2_value_never_shipped'
            out.append((path, i, kind, f'{p}: doc {val:g} vs current {cur}', line,
                        {'key_path': p, 'key_name': nm, 'doc_value_text': numtxt, 'doc_value': val,
                         'live_value': cur, 'key_col': km.start(), 'value_col': col}))
    return out


def e2_legacy(path, text, rev):
    """The MEASURED pairing (enumerators.e2, unchanged), with the same facts dict appended so a
    legacy scan can be reported and compared. key_name is the leaf; the column is unknown (0)."""
    ver, flat = E.settings_at(rev)
    out = []
    for (p, i, kind, det, line) in E.e2(path, text, flat, E.settings_history(rev)):
        m = re.match(r'(.+): doc (\S+) vs current (.+)$', det)
        kp, val = m.group(1), float(m.group(2))
        out.append((p, i, kind, det, line,
                    {'key_path': kp, 'key_name': re.sub(r'\[\d+\]', '', kp).split('.')[-1],
                     'doc_value_text': m.group(2), 'doc_value': val, 'live_value': flat[kp],
                     'key_col': 0, 'value_col': 0}))
    return out


# ---------------------------------------------------------------------------------------------
# "When the doc value last shipped": the newest settings.json revision holding that value for
# that key, and the settings `version` at that revision. Ground truth is the revision walk alone
# (the fixture-parser's trap 3: never a documented history).
# ---------------------------------------------------------------------------------------------
@lru_cache(None)
def settings_revisions(rev):
    """[(sha, version, flat)] newest first, every settings.json revision reachable from rev."""
    out = []
    for h in E.git('log', '--format=%H', rev, '--', 'settings.json').split():
        try:
            j = json.loads(E.show(h, 'settings.json'))
        except Exception:
            continue
        out.append((h, j.get('version'), E.flatten(j)))
    return out


def last_shipped(rev, key_path, val):
    for h, v, flat in settings_revisions(rev):
        x = flat.get(key_path)
        if isinstance(x, (int, float)) and not isinstance(x, bool) and abs(float(x) - val) < 1e-9:
            return v, h[:7]
    return None, None


def ever_shipped_sorted(rev, key_path):
    return sorted(E.settings_history(rev).get(key_path, set()))


# ---------------------------------------------------------------------------------------------
# Fixture facts for FIXTURE_MEANING (spec section 4.1) and the next-free-family claim (DS-D9).
# RESOLUTION uses the instrument's own fixture_ids() so the population is the measured one
# (65 mentions in the living set at cbc2c91); this function only adds the DETAIL the question
# needs -- every Sub name and every full Check title carrying the ID.
# ---------------------------------------------------------------------------------------------
@lru_cache(None)
def fixture_detail(rev):
    src = E.show(rev, 'verify/ordercheck/Program.vb')
    det = {}
    for m in re.finditer(r'Sub\s+(A\d{1,3}[a-z])_(\w+)\s*\(', src):
        det.setdefault(m.group(1), {'subs': [], 'checks': []})['subs'].append(f'{m.group(1)}_{m.group(2)}')
    for m in re.finditer(r'Check\(\s*"(A\d{1,3}[a-z])\b((?:[^"\n]|"")*)"', src):
        det.setdefault(m.group(1), {'subs': [], 'checks': []})['checks'].append(
            (m.group(1) + m.group(2)).replace('""', '"'))
    # Fallback: an ID the instrument resolves but this richer pattern did not read gets the
    # instrument's own (80-char) detail, so no resolved mention reaches Jev with no facts.
    for fid, d in E.fixture_ids(rev).items():
        if fid not in det:
            det[fid] = {'subs': [], 'checks': [], 'instrument_detail': d}
    return det


@lru_cache(None)
def highest_fixture_family(rev):
    src = E.show(rev, 'verify/ordercheck/Program.vb')
    fams = [int(x) for x in re.findall(r'Sub\s+A(\d{1,3})[a-z]?_', src)]
    fams += [int(x) for x in re.findall(r'Check\(\s*"A(\d{1,3})(?![\d])', src)]
    return max(fams) if fams else None


NEXT_FREE_RX = re.compile(r'next free fixture family', re.I)
FAMILY_AFTER_RX = re.compile(r'(?<![\w-])A(\d{1,3})(?![\d])')


def next_free_claims(path, text, rev):
    hi = highest_fixture_family(rev)
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        m = NEXT_FREE_RX.search(line)
        if not m:
            continue
        f = FAMILY_AFTER_RX.search(line, m.end())
        if not f:
            continue
        claimed = int(f.group(1))
        out.append({'path': path, 'line': i, 'claimed': claimed, 'highest': hi,
                    'stale': (hi is not None and claimed <= hi), 'col': f.start()})
    return out


# ---------------------------------------------------------------------------------------------
# Context for a Jev state: heading chain, the line, two lines either side.
# ---------------------------------------------------------------------------------------------
HEADING_RX = re.compile(r'^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$')
FENCE_RX = re.compile(r'^\s{0,3}(```|~~~)')


def heading_chains(lines):
    """chain[i] (0-based) = the markdown heading chain in force at line i, fence-aware."""
    chains, stack, fenced = [], [], False
    for ln in lines:
        if FENCE_RX.match(ln):
            fenced = not fenced
        elif not fenced:
            h = HEADING_RX.match(ln)
            if h:
                lvl = len(h.group(1))
                stack = [s for s in stack if s[0] < lvl] + [(lvl, h.group(2))]
        chains.append(' > '.join(t for _, t in stack))
    return chains


def window(s, col, cap):
    if len(s) <= cap:
        return s
    half = cap // 2
    a = max(0, min(col - half, len(s) - cap))
    b = a + cap
    return ('[...cut...] ' if a > 0 else '') + s[a:b] + (' [...cut...]' if b < len(s) else '')


@lru_cache(None)
def doc_lines(rev, path):
    return E.show(rev, path).splitlines()


@lru_cache(None)
def doc_chains(rev, path):
    return heading_chains(doc_lines(rev, path))


def context(rev, path, lineno, col):
    lines = doc_lines(rev, path)
    i = lineno - 1
    before = [window(lines[j], 0, MAX_CONTEXT_LINE_CHARS) for j in range(max(0, i - CONTEXT_LINES), i)]
    after = [window(lines[j], 0, MAX_CONTEXT_LINE_CHARS) for j in range(i + 1, min(len(lines), i + 1 + CONTEXT_LINES))]
    return {
        'heading_chain': doc_chains(rev, path)[i],
        'line': window(lines[i], col, MAX_LINE_CHARS),
        'lines_before': '\n'.join(before),
        'lines_after': '\n'.join(after),
        'line_truncated': len(lines[i]) > MAX_LINE_CHARS,
    }


# ---------------------------------------------------------------------------------------------
# The scan. Arms (spec section 4.1): VERSION, VALUE, POINTER, FIXTURE_MEANING go to Jev;
# CFG_MEMBER, LINE_PAST_EOF, DATED_STATE, NEXT_FREE_FAMILY and VALUE never_shipped are code-only.
# Dropped by DS-D3 (measured): E1_weak_cue, E3_missing_file, E4's absent-ID check.
# ---------------------------------------------------------------------------------------------
def version_hits(path, text, rev):
    """E1_strong via the instrument's own e1(), with each hit's column recovered for windowing."""
    ver, _ = E.settings_at(rev)
    hits = [h for h in E.e1(path, text, ver) if h[2] == 'E1_strong']
    out = []
    lines = text.splitlines()
    seen = Counter()
    for (p, i, kind, det, line) in hits:
        n = int(re.match(r'v(\d+)', det).group(1))
        # the k-th E1_strong hit on this line: recover its column from the same regexes, same order
        cols = []
        for rx in E.E1_STRONG:
            for m in rx.finditer(line):
                if ver and int(m.group(1)) < ver:
                    cols.append((m.start(), int(m.group(1))))
        k = seen[i]
        seen[i] += 1
        col = cols[k][0] if k < len(cols) else 0
        out.append({'line': i, 'col': col, 'doc_version': n, 'live_version': ver})
    return out


def pointer_hits(path, text, rev):
    hits = E.e6(path, text, rev)
    newest = handovers(rev)[-1] if handovers(rev) else None
    out = []
    for (p, i, kind, det, line) in hits:
        old = det.split(' < ')[0]
        m = re.search(r'seat-handover-' + re.escape(old) + r'\.md', line)
        out.append({'line': i, 'col': m.start() if m else 0,
                    'pointed': f'seat-handover-{old}.md', 'newest': f'seat-handover-{newest}.md'})
    return out


def fixture_mentions(path, text, rev):
    ids = E.fixture_ids(rev)
    det = fixture_detail(rev)
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        for m in E.FIX_RX.finditer(line):
            fid = m.group(1)
            if fid in ids:
                d = det.get(fid, {'subs': [], 'checks': []})
                out.append({'line': i, 'col': m.start(), 'fixture_id': fid,
                            'sub_names': d['subs'], 'check_titles': d['checks']})
    return out


def assign_ids(items, rev7):
    """<arm>|<path>|<line>@<rev7>; a line with k >= 2 candidates of one arm gives the 2nd..kth
    a #n suffix (n = 2..k, left to right). The first keeps the bare ID, which is the form of the
    spec's own example (docs/doc-scanner-check-spec.md section 4.4, a line holding two VALUE
    candidates)."""
    seen = Counter()
    for it in sorted(items, key=lambda x: (x['arm'], x['path'], x['line'], x.get('col', 0))):
        base = f"{it['arm']}|{it['path']}|{it['line']}@{rev7}"
        seen[base] += 1
        it['id'] = base if seen[base] == 1 else f'{base}#{seen[base]}'
    return items


def claim_for(it):
    a = it['arm']
    if a == 'VERSION':
        return (f"The line quotes settings version v{it['doc_version']}. "
                f"The live settings version (settings.json line 2 at this revision) is v{it['live_version']}.")
    if a == 'VALUE':
        ls = f"v{it['last_shipped_version']}" if it.get('last_shipped_version') is not None else 'an earlier revision'
        return (f"The line gives `{it['key_name']}` = {it['doc_value_text']}. "
                f"The live value of `{it['key_path']}` is {it['live_value']}. "
                f"{it['doc_value_text']} was last shipped at {ls}.")
    if a == 'POINTER':
        return (f"The line links `{it['pointed']}`. The newest seat handover is `{it['newest']}`.")
    return ''


CODE_ONLY_ID_PREFIX = {
    'VALUE_NEVER_SHIPPED': 'VALUE_NEVER_SHIPPED',
    'CFG_MEMBER_MISSING': 'CFG_MEMBER',
    'LINE_PAST_EOF': 'LINE_PAST_EOF',
    'DATED_STATE_OVER_HORIZON': 'DATED_STATE',
    'NEXT_FREE_FAMILY': 'NEXT_FREE_FAMILY',
}


def scan(rev_in, doc_patterns, e2mode):
    rev = rev_full(rev_in)
    rev7 = rev[:7]
    living, living_missing = living_set(rev)
    if doc_patterns:
        docs, unmatched = expand_docs(rev, doc_patterns)
        source = 'explicit'
    else:
        docs, unmatched, source = list(living), [], 'living'
    ver, flat = E.settings_at(rev)
    toks = E.vb_tokens_at(rev)
    items, code_only = [], {'VALUE_NEVER_SHIPPED': [], 'CFG_MEMBER_MISSING': [], 'LINE_PAST_EOF': [],
                            'DATED_STATE_OVER_HORIZON': [], 'NEXT_FREE_FAMILY': []}
    e2fn = e2_strict if e2mode == 'strict' else e2_legacy
    fixture_mention_count = 0
    for p in docs:
        t = E.show(rev, p)
        if not t:
            continue
        for h in version_hits(p, t, rev):
            items.append(dict(h, arm='VERSION', path=p, question='Q-TENSE'))
        for h in e2fn(p, t, rev):
            (pp, i, kind, det, line) = h[:5]
            facts = h[5] if len(h) > 5 else {'key_path': det.split(':')[0], 'value_col': 0}
            if kind == 'E2_value_never_shipped':
                code_only['VALUE_NEVER_SHIPPED'].append({'path': p, 'line': i, 'detail': det})
                continue
            it = dict(facts, arm='VALUE', path=p, line=i, question='Q-TENSE', col=facts.get('value_col', 0))
            if 'doc_value' in facts:
                v, sha = last_shipped(rev, facts['key_path'], facts['doc_value'])
                it['last_shipped_version'] = v
                it['last_shipped_sha7'] = sha
                it['ever_shipped'] = ever_shipped_sorted(rev, facts['key_path'])
            else:
                it['legacy_detail'] = det
            items.append(it)
        for h in pointer_hits(p, t, rev):
            items.append(dict(h, arm='POINTER', path=p, question='Q-TENSE'))
        fm = fixture_mentions(p, t, rev)
        fixture_mention_count += len(fm)
        for h in fm:
            items.append(dict(h, arm='FIXTURE_MEANING', path=p, question='Q-MEANING'))
        for h in E.e3(p, t, rev, toks):
            if h[2] == 'E3_cfg_member_missing':
                code_only['CFG_MEMBER_MISSING'].append({'path': p, 'line': h[1], 'detail': h[3]})
            elif h[2] == 'E3_line_past_eof':
                code_only['LINE_PAST_EOF'].append({'path': p, 'line': h[1], 'detail': h[3]})
        for h in E.e5(p, t, rev, horizon=DATED_STATE_HORIZON_DAYS):
            if h[2] == 'E5_stale_state_section':
                code_only['DATED_STATE_OVER_HORIZON'].append({'path': p, 'line': h[1], 'detail': h[3]})
        for c in next_free_claims(p, t, rev):
            code_only['NEXT_FREE_FAMILY'].append(c)
    assign_ids(items, rev7)
    # Code-only findings get IDs in the same shape. The prefix names the finding, so a line that
    # carries a judged VALUE candidate and a never-shipped one cannot share an ID.
    for key, prefix in CODE_ONLY_ID_PREFIX.items():
        for x in code_only[key]:
            x['arm'] = prefix
        assign_ids(code_only[key], rev7)
    for it in items:
        it.update(context(rev, it['path'], it['line'], it.get('col', 0)))
        it['claim'] = claim_for(it)
    items.sort(key=lambda x: (['VERSION', 'VALUE', 'POINTER', 'FIXTURE_MEANING'].index(x['arm']), x['path'], x['line'], x['id']))
    counts = Counter(it['arm'] for it in items)
    return {
        'rev': rev, 'rev7': rev7,
        # Two dates, on purpose. DATED_STATE ages come from the instrument's E.e5, whose
        # rev_date() reads `%cs` -- the committer's RECORDED timezone, which is GMT+8 on this
        # workstation. Project dates are UTC. At cbc2c91 the two differ by a day (2026-09-23 vs
        # 2026-09-22), so every age is one day high. Kept unchanged for parity with the
        # measurement; queued in docs/doc-scanner-build-spec-back.md. Both are printed so the
        # offset is visible, never silent.
        'rev_date_instrument': str(E.rev_date(rev)),
        'rev_date_utc': _utc_date(rev),
        'e2_pairing': e2mode,
        'live_settings_version': ver,
        'living_docs': living, 'living_docs_missing': living_missing, 'living_expected': LIVING_EXPECTED,
        'newest_handover': newest_handover_path(rev),
        'docs_source': source, 'docs_scanned': docs, 'docs_unmatched_patterns': unmatched,
        'docs_in_living_set': [d for d in docs if d in living],
        'highest_fixture_family': highest_fixture_family(rev),
        'dated_state_horizon_days': DATED_STATE_HORIZON_DAYS,
        'counts': {
            'CANDIDATES_VERSION': counts['VERSION'], 'CANDIDATES_VALUE': counts['VALUE'],
            'CANDIDATES_POINTER': counts['POINTER'], 'CANDIDATES_FIXTURE_MEANING': counts['FIXTURE_MEANING'],
            'VALUE_NEVER_SHIPPED_CODE_ONLY': len(code_only['VALUE_NEVER_SHIPPED']),
            'CFG_MEMBER_MISSING': len(code_only['CFG_MEMBER_MISSING']),
            'LINE_PAST_EOF': len(code_only['LINE_PAST_EOF']),
            'DATED_STATE_OVER_HORIZON': len(code_only['DATED_STATE_OVER_HORIZON']),
            'NEXT_FREE_FAMILY_STALE': 1 if any(c['stale'] for c in code_only['NEXT_FREE_FAMILY']) else 0,
            'NEXT_FREE_FAMILY_CLAIMS': len(code_only['NEXT_FREE_FAMILY']),
            'FIXTURE_MENTIONS_RESOLVED': fixture_mention_count,
        },
        'items': items,
        'code_only': code_only,
    }


# ---------------------------------------------------------------------------------------------
# Replay (spec section 5, acceptance item 1). Same four fixes, same removed-line rule and the same
# output format as tools/checks/measure/doc-scanner/replay_recall.py, so the two outputs diff.
# removed_lines() is copied from replay_recall.py (it cannot be imported: that file runs at import
# time). `--kinds all --e2 legacy` must reproduce replay_recall.py byte for byte -- that diff is
# the proof the copy is faithful.
# ---------------------------------------------------------------------------------------------
REPLAY_CASES = [
    ('5c7c178', ['CLAUDE.md', 'docs/DeribitIndicatorProject.md', 'docs/architecture.md', 'docs/roadmap.md', 'docs/trader-tick-queue.md']),
    ('9c447e1', ['docs/architecture.md', 'docs/DeribitIndicatorProject.md']),
    ('5b8515e', ['docs/DeribitIndicatorProject.md']),
    ('2f46679', ['CLAUDE.md']),
]
KEPT_KINDS = {'E1_strong', 'E2_value_was_shipped', 'E2_value_never_shipped', 'E3_cfg_member_missing',
              'E3_line_past_eof', 'E5_stale_state_section', 'E5_inherited', 'E6_stale_current_pointer'}


def removed_lines(fix, path):
    diff = E.git('show', fix, '--unified=0', '--format=', '--', path)
    out, cur = [], None
    for l in diff.splitlines():
        m = re.match(r'@@ -(\d+)(?:,(\d+))? ', l)
        if m:
            cur = int(m.group(1)); continue
        if l.startswith('-') and not l.startswith('---'):
            out.append((cur, l[1:])); cur += 1
    return out


def run_all_tool(rev, paths, e2mode, kinds):
    """enumerators.run_all's loop, same order, with the E2 function switchable."""
    ver, flat = E.settings_at(rev)
    hist = E.settings_history(rev)
    toks = E.vb_tokens_at(rev)
    res = []
    for p in paths:
        t = E.show(rev, p)
        if not t:
            continue
        res += E.e1(p, t, ver)
        res += [h[:5] for h in (e2_strict(p, t, rev) if e2mode == 'strict' else E.e2(p, t, flat, hist))]
        res += E.e3(p, t, rev, toks)
        res += E.e4(p, t, rev)
        res += E.e5(p, t, rev)
        res += E.e6(p, t, rev)
    if kinds == 'kept':
        res = [r for r in res if r[2] in KEPT_KINDS]
    return res


def replay(kinds, e2mode):
    for fix, paths in REPLAY_CASES:
        pre = fix + '^'
        res = run_all_tool(pre, paths, e2mode, kinds)
        flagged = {(p, i) for (p, i, *_) in res}
        tot_rm = hit_rm = 0
        print(f'=== fix {fix} (replay at {pre})')
        for p in paths:
            rm = removed_lines(fix, p)
            rm = [(n, t) for n, t in rm if len(t.strip()) > 12 and not re.fullmatch(r'[|\-: ]+', t.strip())]
            h = [(n, t) for n, t in rm if (p, n) in flagged]
            tot_rm += len(rm); hit_rm += len(h)
            for n, t in rm:
                ks = sorted({k for (pp, i, k, *_) in res if pp == p and i == n})
                print(f'  {"HIT " if ks else "miss"} {p}:{n} {",".join(ks):28s} {t.strip()[:110]}')
        other = [r for r in res if (r[0], r[1]) not in {(p, n) for p in paths for n, _ in removed_lines(fix, p)}]
        print(f'  removed content lines: {tot_rm}, flagged: {hit_rm}; candidates on NON-removed lines: {len(other)}')
        print('  by kind (non-removed):', dict(Counter(r[2] for r in other)))
        if kinds == 'kept':
            # Informational only: FIXTURE_MEANING is a NEW arm with no instrument counterpart, so
            # it is kept out of the parity lines above. It counts removed lines where code finds a
            # resolved fixture-ID mention for the meaning question.
            fm = 0
            for p in paths:
                t = E.show(pre, p)
                mentioned = {h['line'] for h in fixture_mentions(p, t, pre)}
                fm += sum(1 for n, tx in removed_lines(fix, p)
                          if len(tx.strip()) > 12 and not re.fullmatch(r'[|\-: ]+', tx.strip()) and n in mentioned)
            print(f'  [info, not parity] removed content lines with a FIXTURE_MEANING candidate: {fm}')


def main(argv):
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('scan')
    s.add_argument('--rev', default='HEAD')
    s.add_argument('--docs', nargs='*', default=None)
    s.add_argument('--e2', choices=['strict', 'legacy'], default='strict')
    s.add_argument('--out', required=True)
    r = sub.add_parser('replay')
    r.add_argument('--kinds', choices=['kept', 'all'], default='kept')
    r.add_argument('--e2', choices=['strict', 'legacy'], default='strict')
    a = ap.parse_args(argv)
    if a.cmd == 'scan':
        doc = scan(a.rev, a.docs, a.e2)
        with open(a.out, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(doc, f, ensure_ascii=False, indent=1)
        c = doc['counts']
        print(f"SCAN_OK rev={doc['rev7']} docs={len(doc['docs_scanned'])} " +
              ' '.join(f'{k}={v}' for k, v in c.items()))
    else:
        replay(a.kinds, a.e2)


if __name__ == '__main__':
    main(sys.argv[1:])
