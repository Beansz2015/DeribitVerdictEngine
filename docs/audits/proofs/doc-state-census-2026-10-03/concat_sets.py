import re, glob, os, datetime
import tempfile; S = tempfile.gettempdir().replace("\\", "/") + "/doc-state-census-"
ID = re.compile(r"^\|\s*(?:~~)?\**`?\**([A-Z]{1,6}(?:-[A-Z0-9]{1,4})?-?\d{1,3}[a-z]?(?:\.\d)?)`?\**(?:~~)?\s*\|")
DEC_HDR = re.compile(r"decision|question|my read|recommend|options|ruling", re.I)

def rows(path):
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    out = []
    for i, ln in enumerate(lines):
        if ID.match(ln):
            j = i
            while j > 0 and lines[j - 1].startswith('|'):
                j -= 1
            if DEC_HDR.search(lines[j]):
                out.append(ln)
    return out

top = [p for p in glob.glob('docs/*.md') if 'archive' not in os.path.basename(p)]
full = [p for p in top if rows(p)]
link = re.compile(r"\(([A-Za-z0-9_.\-/]+\.md)(?:#[^)]*)?\)")
seeds = ['docs/trader-tick-queue.md', 'docs/roadmap.md']
cut = datetime.date(2026, 10, 5) - datetime.timedelta(days=30)
for p in glob.glob('docs/seat-handover-*.md'):
    m = re.search(r"(\d{4}-\d{2}-\d{2})", p)
    if m and datetime.date.fromisoformat(m.group(1)) >= cut:
        seeds.append(p)
living = set()
for s in seeds:
    living.add(os.path.normpath(s).replace(os.sep, '/'))
    for t in link.findall(open(s, encoding='utf-8', errors='replace').read()):
        c = 'docs/' + os.path.basename(t)
        if os.path.exists(c) and 'archive' not in c:
            living.add(c)
living_dec = [p for p in sorted(living) if rows(p)]

def write(name, paths, only_rows):
    with open(S + name, 'w', encoding='utf-8') as f:
        for p in paths:
            f.write(f"\n\n===== {p}\n")
            f.write('\n'.join(rows(p)) if only_rows else open(p, encoding='utf-8', errors='replace').read())
    print(name, len(paths), os.path.getsize(S + name))

write('set_full_docs.txt', full, False)
write('set_full_rows.txt', full, True)
write('set_living_docs.txt', living_dec, False)
write('set_living_rows.txt', living_dec, True)
