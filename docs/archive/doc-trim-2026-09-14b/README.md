# Doc trim 2026-09-14, second pass - originals

Byte copies of four always-read docs, taken before the second 2026-09-14 trim pass. **History only - not current state.**

| File | Working-tree bytes | Working-tree SHA-256 | Git blob bytes (LF) | Git blob SHA-256 |
|---|---:|---|---:|---|
| `trader-profile.md` | 21553 | `bb47901c0dccf4b3904bf9a57bf9f0d032936be6c6117c6e67312c1165118cb9` | 21553 | `bb47901c0dccf4b3904bf9a57bf9f0d032936be6c6117c6e67312c1165118cb9` |
| `DeribitIndicatorProject.md` | 66633 | `c8f5a44ee871b3c5d47dd11a2149022cf59b751a0415b43ffd77cadb98190ecc` | 66184 | `b07302cbb2bb73c7347b901e81b2274eac98a2d00d3fda7e5ba6df7c18816a9c` |
| `trader-tick-queue.md` | 53714 | `7e55fba063661cb9d14a7f61786be0474bdce0bd10ad83199082fe613c8facfc` | 53714 | `7e55fba063661cb9d14a7f61786be0474bdce0bd10ad83199082fe613c8facfc` |
| `architecture.md` | 77899 | `96529571b62a9e17066eca999caf24df7cd80bc9a257d25ca196e0750729d113` | 77194 | `d8a27f1dc59fe9cc0b837b471c1a59914f729a9ea3bdd6291f714ef5e1efd66e` |

- **Git tag:** `doc-trim-2026-09-14b-pre`, on commit `521f4e7`. Retrieve a file with `git show doc-trim-2026-09-14b-pre:docs/architecture.md`.
- **Line endings:** copied from the working tree, where two files are CRLF. The repo's `* text=auto` rule stores LF on commit, so the committed copies match the tag blobs.
- **Search:** `docs/archive/.ignore` hides every `originals/` folder from default ripgrep and `Grep` searches. To search them, pass this folder as the search path.
- **Moved blocks:** listed in [`doc-trim-log.md`](../../doc-trim-log.md) under "Pass 2", with destination and hash. `trader-profile.md` was reformatted, not trimmed; its row is in the ledger's "Reformats" table. Re-check with `tools/checks/doc-trim-verify.ps1`.
