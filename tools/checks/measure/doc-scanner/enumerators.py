"""Harness-4 (doc scanner) pre-spec measurement: code-only candidate enumerators over docs.
No Jev, no network. Record: docs/doc-scanner-measurement-2026-09-22.md. Each enumerator returns a list of (path, lineno, kind, detail, line_text)."""
import json, re, subprocess, sys, os
from functools import lru_cache

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..'))

def git(*args):
    return subprocess.run(['git', '-C', REPO, *args], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout

@lru_cache(None)
def show(rev, path):
    return git('show', f'{rev}:{path}')

@lru_cache(None)
def tree_files(rev):
    return set(git('ls-tree', '-r', '--name-only', rev).splitlines())

def flatten(o, prefix='', out=None):
    if out is None: out = {}
    if isinstance(o, dict):
        for k, v in o.items():
            if k == 'change_log': continue
            flatten(v, f'{prefix}.{k}' if prefix else k, out)
    elif isinstance(o, list):
        for i, v in enumerate(o):
            flatten(v, f'{prefix}[{i}]', out)
    else:
        out[prefix] = o
    return out

@lru_cache(None)
def settings_at(rev):
    try:
        j = json.loads(show(rev, 'settings.json'))
    except Exception:
        return None, {}
    return j.get('version'), flatten(j)

@lru_cache(None)
def settings_history(rev):
    """leaf path -> set of every numeric value it held in any settings.json revision up to rev."""
    hist = {}
    for h in git('log', '--format=%H', rev, '--', 'settings.json').split():
        try:
            j = json.loads(show(h, 'settings.json'))
        except Exception:
            continue
        for k, v in flatten(j).items():
            if isinstance(v, (int, float)) and not isinstance(v, bool):
                hist.setdefault(k, set()).add(float(v))
    return hist

def snake(s):
    s = re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', s)
    s = re.sub(r'([A-Z]+)([A-Z][a-z])', r'\1_\2', s)
    return s.lower()

# ---------------- E1: settings-version quotes -------------------------------------------------
E1_STRONG = [
    re.compile(r'settings(?:\.json)?\s*\(\s*v(\d{1,3})\s*\)', re.I),            # settings.json (v17)
    re.compile(r'(?:current|app|live)\s+version[^|\n]{0,40}?\bv(\d{1,3})\b', re.I),  # Current version: settings.json v31
    re.compile(r'\*\*settings\*\*\s*\|\s*\**v(\d{1,3})', re.I),                    # | **Settings** | **v65**
]
E1_WEAK = re.compile(r'(?<![\w.])v(\d{2})(?![\w.])')
CUE = re.compile(r'\b(current|currently|now|live|latest|tracked|today|is on|runs? on|state)\b', re.I)

def e1(path, text, cur_ver):
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        hit = False
        for rx in E1_STRONG:
            for m in rx.finditer(line):
                n = int(m.group(1))
                if cur_ver and n < cur_ver:
                    out.append((path, i, 'E1_strong', f'v{n} < v{cur_ver}', line)); hit = True
        if hit: continue
        if CUE.search(line):
            for m in E1_WEAK.finditer(line):
                n = int(m.group(1))
                if cur_ver and 17 <= n < cur_ver:
                    out.append((path, i, 'E1_weak_cue', f'v{n} < v{cur_ver}', line)); break
    return out

# ---------------- E2: settings value quotes ---------------------------------------------------
NUM = re.compile(r'-?\d+(?:\.\d+)?')

def build_name_index(flat):
    """name -> list of leaf paths. Names: json leaf (snake) and its PascalCase form."""
    idx = {}
    for path, v in flat.items():
        if not isinstance(v, (int, float)) or isinstance(v, bool): continue
        leaf = re.sub(r'\[\d+\]', '', path).split('.')[-1]
        if len(leaf) < 6: continue
        pascal = ''.join(p[:1].upper() + p[1:] for p in leaf.split('_'))
        for nm in {leaf, pascal}:
            idx.setdefault(nm, set()).add(path)
    return idx

def e2(path, text, flat, hist):
    idx = build_name_index(flat)
    names = sorted(idx, key=len, reverse=True)
    rx = re.compile(r'(?<![\w])(' + '|'.join(map(re.escape, names)) + r')(?![\w])') if names else None
    out = []
    if not rx: return out
    for i, line in enumerate(text.splitlines(), 1):
        for m in rx.finditer(line):
            nm = m.group(1)
            paths = idx[nm]
            if len(paths) > 1:
                # disambiguate by a parent segment present on the line
                cand = [p for p in paths if any(seg.lower() in line.lower() for seg in re.sub(r'\[\d+\]', '', p).split('.')[-2:-1])]
                if len(cand) != 1: continue
                paths = cand
            p = next(iter(paths))
            tail = line[m.end(): m.end() + 22]
            nm2 = re.match(r'[`*\s]*(?:\(|=|:|is|of|at)?[`*\s]*(-?\d+(?:\.\d+)?)', tail)
            if not nm2: continue
            val = float(nm2.group(1))
            cur = flat[p]
            if abs(val - float(cur)) < 1e-9: continue
            ever = hist.get(p, set())
            kind = 'E2_value_was_shipped' if any(abs(val - e) < 1e-9 for e in ever) else 'E2_value_never_shipped'
            out.append((path, i, kind, f'{p}: doc {val:g} vs current {cur}', line))
    return out

# ---------------- E3: reference rot (paths, file:line, POCO/cfg paths) ------------------------
PATH_RX = re.compile(r'`((?:[\w.-]+/)*[\w.-]+\.(?:vb|md|ps1|vbproj|json|py|csv))(?::(\d+))?`')
CFG_RX = re.compile(r'`cfg\.((?:[A-Z][A-Za-z0-9]*\.)*[A-Z][A-Za-z0-9]*)`')

def e3(path, text, rev, vb_tokens):
    files = tree_files(rev)
    basenames = {}
    for f in files:
        basenames.setdefault(f.rsplit('/', 1)[-1], []).append(f)
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        for m in PATH_RX.finditer(line):
            ref, ln = m.group(1), m.group(2)
            cands = [ref] if ref in files else [f for f in files if f.endswith('/' + ref)] or \
                    ([os.path.normpath(os.path.join(os.path.dirname(path), ref)).replace('\\', '/')] if os.path.normpath(os.path.join(os.path.dirname(path), ref)).replace('\\', '/') in files else [])
            if not cands and '/' not in ref:
                cands = basenames.get(ref, [])
            if not cands and any(f.endswith(ref.lstrip('_')) or f.endswith('/' + ref) for f in files if ref.startswith('_')):
                cands = ['<partial-name>']
            if not cands and (ignored(ref) or os.path.exists(os.path.join(REPO, ref)) or os.path.exists(os.path.join(REPO, 'docs', ref))):
                cands = ['<runtime-or-untracked>']
            if not cands:
                out.append((path, i, 'E3_missing_file', ref, line)); continue
            if ln and len(cands) == 1 and cands[0].endswith('.vb'):
                nlines = show(rev, cands[0]).count('\n') + 1
                if int(ln) > nlines:
                    out.append((path, i, 'E3_line_past_eof', f'{ref}:{ln} > {nlines}', line))
        for m in CFG_RX.finditer(line):
            last = m.group(1).split('.')[-1]
            if last not in vb_tokens:
                out.append((path, i, 'E3_cfg_member_missing', m.group(1), line))
    return out

@lru_cache(None)
def ignored(ref):
    r = subprocess.run(['git', '-C', REPO, 'check-ignore', '-q', '--no-index', ref], capture_output=True)
    if r.returncode == 0: return True
    base = ref.rsplit('/', 1)[-1]
    r2 = subprocess.run(['git', '-C', REPO, 'check-ignore', '-q', '--no-index', base], capture_output=True)
    return r2.returncode == 0

@lru_cache(None)
def vb_tokens_at(rev):
    toks = set()
    for f in tree_files(rev):
        if f.endswith('.vb'):
            toks.update(re.findall(r'[A-Za-z_][A-Za-z0-9_]*', show(rev, f)))
    return toks

# ---------------- E4: fixture IDs -------------------------------------------------------------
FIX_RX = re.compile(r'(?<![\w-])(A\d{1,3}[a-z])(?![\w-])')

@lru_cache(None)
def fixture_ids(rev):
    src = show(rev, 'verify/ordercheck/Program.vb')
    ids = {m.group(1): m.group(2) for m in re.finditer(r'Sub\s+(A\d{1,3}[a-z])_(\w+)\s*\(', src)}
    for m in re.finditer(r'Check\(\s*"(A\d{1,3}[a-z])\b([^"]{0,80})', src):
        ids.setdefault(m.group(1), 'check:' + m.group(2).strip())
    return ids

def e4(path, text, rev):
    ids = fixture_ids(rev)
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        for m in FIX_RX.finditer(line):
            fid = m.group(1)
            if fid not in ids:
                out.append((path, i, 'E4_fixture_id_absent', fid, line))
    return out

def e4_mentions(text, rev):
    ids = fixture_ids(rev)
    return sum(1 for line in text.splitlines() for m in FIX_RX.finditer(line) if m.group(1) in ids)

# ---------------- E6: "current" pointers to a superseded seat handover ------------------------
HAND_RX = re.compile(r'seat-handover-(\d{4}-\d{2}-\d{2}[a-z]?)\.md')
PTR_CUE = re.compile(r'\b(current|THE current|state read|start here|entry point|read first)\b', re.I)

def e6(path, text, rev):
    hs = sorted({HAND_RX.search(f).group(1) for f in tree_files(rev) if HAND_RX.search(f)})
    if not hs: return []
    newest = hs[-1]
    out = []
    for i, line in enumerate(text.splitlines(), 1):
        if not PTR_CUE.search(line): continue
        if re.search(r'supersed', line, re.I): continue
        for m in HAND_RX.finditer(line):
            if m.group(1) < newest:
                out.append((path, i, 'E6_stale_current_pointer', f'{m.group(1)} < {newest}', line)); break
    return out

# ---------------- E5: dated state sections older than a horizon -------------------------------
# A heading or banner that asserts STATE as of a date. Everything under it (to the next heading
# of the same or higher level) inherits the date. Flag when the date is older than HORIZON days
# at the revision's commit date. Archives are excluded by the caller.
STATE_HDR = re.compile(r'(state snapshot|state banner|state at close|current state|verified in the tree|as of)\W{0,12}\**\s*(\d{4}-\d{2}-\d{2})', re.I)
import datetime as _dt

def rev_date(rev):
    return _dt.date.fromisoformat(git('log', '-1', '--format=%cs', rev).strip())

def e5(path, text, rev, horizon=7):
    today = rev_date(rev)
    lines = text.splitlines()
    out = []
    for i, line in enumerate(lines, 1):
        m = STATE_HDR.search(line)
        if not m: continue
        d = _dt.date.fromisoformat(m.group(2))
        age = (today - d).days
        if age <= horizon: continue
        # extent: to the next markdown heading of same-or-higher level (or 40 lines for a non-heading)
        lvl = len(re.match(r'^(>?\s*#*)', line).group(1).replace('>', '').strip()) or 7
        end = min(len(lines), i + 40)
        for j in range(i, len(lines)):
            h = re.match(r'^(#+)\s', lines[j])
            if h and len(h.group(1)) <= lvl:
                end = j; break
        out.append((path, i, 'E5_stale_state_section', f'{m.group(2)} age {age}d, lines {i}-{end}', line))
        for j in range(i + 1, end + 1):
            out.append((path, j, 'E5_inherited', f'under {m.group(2)}', lines[j - 1]))
    return out

def run_all(rev, paths, which=('e1', 'e2', 'e3', 'e4', 'e5', 'e6')):
    ver, flat = settings_at(rev)
    hist = settings_history(rev) if 'e2' in which else {}
    toks = vb_tokens_at(rev) if 'e3' in which else set()
    res = []
    for p in paths:
        t = show(rev, p)
        if not t: continue
        if 'e1' in which: res += e1(p, t, ver)
        if 'e2' in which: res += e2(p, t, flat, hist)
        if 'e3' in which: res += e3(p, t, rev, toks)
        if 'e4' in which: res += e4(p, t, rev)
        if 'e5' in which: res += e5(p, t, rev)
        if 'e6' in which: res += e6(p, t, rev)
    return res
