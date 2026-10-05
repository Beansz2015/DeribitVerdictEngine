# Token counts for `docs/doc-state-register-proposal.md` §6.1

Measured 2026-10-05 (UTC) on the working tree at `d83d8ac`. Method: `concat_sets.py` writes each set to one text file, then the `Read` tool opens it and reports the file's token count in its truncation notice. These are real counts, not a bytes-to-tokens conversion.

| File | Bytes at measurement | Tokens (`Read` tool) |
|---|---|---|
| `set_full_rows.txt` (decision rows of 119 docs) | 222,631 | 91,812 |
| `set_living_rows.txt` (decision rows of 56 living docs) | 118,177 | 49,140 |
| `docs/DeribitIndicatorProject.md` (unit of comparison) | 80,293 | 35,166 |
| `set_full_docs.txt` (whole docs) | 2,305,130 | not countable: over the 256 KB `Read` limit |
| `set_living_docs.txt` (whole docs) | 1,226,561 | not countable: over the 256 KB `Read` limit |

- The re-run in `concat_sets-output.txt` shows `set_full_rows.txt` at 222,790 bytes. The +159 bytes are rows of `docs/doc-state-register-proposal.md` itself, edited after the measurement. The row and doc counts are unchanged.
- The bytes/token ratio of the three counted files is 2.28–2.42. The whole-doc token figures in the proposal are estimates from that range. They are not measurements.
