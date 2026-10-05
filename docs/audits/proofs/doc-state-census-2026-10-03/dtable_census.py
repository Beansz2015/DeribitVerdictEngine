"""Census of decision-table rows across docs/*.md (top level only; archive excluded).

A 'decision row' = a markdown table row whose FIRST cell is an ID like D-6, HDS-1, LP-3, TB-D5,
`AT-1`, **D-2b**, RV-1. Counts: rows, docs, IDs keyed in >1 doc (collisions), header variety,
and how many rows carry a ruling marker in the row text.
"""
import re, glob, collections, os, sys

ID = re.compile(r"^\|\s*(?:~~)?\**`?\**([A-Z]{1,6}(?:-[A-Z0-9]{1,4})?-?\d{1,3}[a-z]?(?:\.\d)?)`?\**(?:~~)?\s*\|")
RULED = re.compile(r"RULED|TICKED|ruled|AUTO-PROCEEDED|✅", re.U)
OPEN = re.compile(r"owed|queued|not yet ruled|awaits|OPEN\b|pending", re.I)

rows = []          # (id, doc, ruled, open)
headers = collections.Counter()
for path in sorted(glob.glob('docs/*.md')):
    if 'archive' in os.path.basename(path):
        continue
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    for i, ln in enumerate(lines):
        m = ID.match(ln)
        if not m:
            continue
        rows.append((m.group(1), os.path.basename(path), bool(RULED.search(ln)), bool(OPEN.search(ln))))
        # header = nearest preceding line that starts a table
        j = i
        while j > 0 and lines[j - 1].startswith('|'):
            j -= 1
        hdr = re.sub(r"\s+", " ", lines[j].strip().lower())
        headers[hdr] += 1

ids_docs = collections.defaultdict(set)
for i, d, *_ in rows:
    ids_docs[i].add(d)
coll = {i: ds for i, ds in ids_docs.items() if len(ds) > 1}

print(f"decision-like rows: {len(rows)}")
print(f"docs with such rows: {len({d for _, d, *_ in rows})}")
print(f"distinct IDs: {len(ids_docs)}")
print(f"IDs keyed in >1 doc: {len(coll)}  ({sum(len(v) for v in coll.values())} doc-IDs)")
for i, ds in sorted(coll.items(), key=lambda kv: -len(kv[1]))[:12]:
    print(f"  {i:8s} in {len(ds):2d} docs: {', '.join(sorted(ds))[:150]}")
print(f"rows with a ruling marker: {sum(r for _, _, r, _ in rows)}")
print(f"rows with an open marker and no ruling marker: {sum(1 for _, _, r, o in rows if o and not r)}")
print(f"distinct table header shapes: {len(headers)}; top 8:")
for h, n in headers.most_common(8):
    print(f"  {n:4d}  {h[:120]}")
