import re, glob, os, subprocess, datetime
ID = re.compile(r"^\|\s*(?:~~)?\**`?\**([A-Z]{1,6}(?:-[A-Z0-9]{1,4})?-?\d{1,3}[a-z]?(?:\.\d)?)`?\**(?:~~)?\s*\|")
DEC_HDR = re.compile(r"decision|question|my read|recommend|options|ruling", re.I)
RULED_WORD = re.compile(r"\bRULED\b|\bTICKED\b|AUTO-PROCEEDED")

def table_rows(path):
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    out = []
    for i, ln in enumerate(lines):
        if ID.match(ln):
            j = i
            while j > 0 and lines[j - 1].startswith('|'):
                j -= 1
            out.append((ln, lines[j]))
    return lines, out

top = [p for p in glob.glob('docs/*.md') if 'archive' not in os.path.basename(p)]
arch = [p for p in glob.glob('docs/**/*.md', recursive=True) if p not in top]

def summarize(paths, label):
    docs = rows = dec_rows = row_bytes = doc_bytes = ruled_outside = 0
    for p in paths:
        lines, rs = table_rows(p)
        ruled_lines = [l for l in lines if RULED_WORD.search(l)]
        ruled_outside += sum(1 for l in ruled_lines if not l.startswith('|'))
        if not rs:
            continue
        docs += 1
        doc_bytes += os.path.getsize(p)
        rows += len(rs)
        d = [r for r, h in rs if DEC_HDR.search(h)]
        dec_rows += len(d)
        row_bytes += sum(len(r.encode('utf-8')) for r in d)
    print(f"{label}: docs-with-rows={docs} rows={rows} decision-header-rows={dec_rows} "
          f"bytes(docs)={doc_bytes:,} bytes(decision rows only)={row_bytes:,} RULED-lines-outside-tables={ruled_outside}")

summarize(top, 'top-level')
summarize(arch, 'archive+subdirs')

# living docs: linked from queue, roadmap, handovers of the last 30 days (by file date in name)
link = re.compile(r"\(([A-Za-z0-9_.\-/]+\.md)(?:#[^)]*)?\)")
seeds = ['docs/trader-tick-queue.md', 'docs/roadmap.md']
cut = datetime.date(2026, 10, 5) - datetime.timedelta(days=30)
for p in glob.glob('docs/seat-handover-*.md'):
    m = re.search(r"(\d{4}-\d{2}-\d{2})", p)
    if m and datetime.date.fromisoformat(m.group(1)) >= cut:
        seeds.append(p)
living = set()
for s in seeds:
    living.add(os.path.normpath(s))
    for t in link.findall(open(s, encoding='utf-8', errors='replace').read()):
        cand = os.path.normpath(os.path.join('docs', os.path.basename(t)))
        if os.path.exists(cand):
            living.add(cand)
living = sorted(p for p in living if 'archive' not in os.path.basename(p))
print(f"living seeds={len(seeds)} living docs={len(living)}")
summarize([p.replace(os.sep, '/') for p in living], 'living')

# oldest doc by first commit
first = subprocess.run(['git', 'log', '--reverse', '--format=%ad', '--date=short', '--', 'docs'], capture_output=True, text=True).stdout.split()
print('first docs commit:', first[0] if first else '?')
