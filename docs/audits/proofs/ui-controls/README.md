# Proof harness — 2026-09-25 UI-controls audit

Reproduces Finding 1 (`TapeStripLabel` BURST-highlight over-reach), Finding 3
(`MiniMeter.Pct` NaN passthrough) and Finding 4 (`TapeStripLabel` negative
`tailRect` width) from
[`../../2026-09-25-ui-controls.md`](../../2026-09-25-ui-controls.md) against
commit `6e74181f000ddc7666e8b7d17c64a195855b45cd`. All three code paths are
copied verbatim (with source line citations in comments) from the audited
tree — this does not link against the shipped assembly.

`Program.vb.txt` / `ProofRunner.vbproj.txt` are named `.txt` so the root
`DeribitVerdictEngine.sln` project (which globs every `.vb` file outside
`tools/` and `verify/`) does not pick them up. To run:

```bash
mkdir /tmp/ui-controls-proof
cp Program.vb.txt /tmp/ui-controls-proof/Program.vb
cp ProofRunner.vbproj.txt /tmp/ui-controls-proof/ProofRunner.vbproj
cd /tmp/ui-controls-proof
dotnet run
```

(Any directory outside the solution tree works — the project is
`net8.0-windows` with `UseWindowsForms=true`, self-contained, no reference to
the shipped `DeribitVerdictEngine` sources.)

## Output obtained (2026-09-26, this run)

```
=== Finding A: TapeStripLabel BURST-highlight over-reach ===
Composed strip text (what lblLiveStrip.Text is set to):
  "60123 · SL 59860 (+56) | SH 60103 (+299) · TFI BUY +1200.0 · 1.2 bps · book 1.4× bid · 12.3 tr/s ($84.0k/s) 5.1× BURST↑ · ABS↑ 60510 (3.4×)"

prefix (drawn in the DIM ForeColor):
  "60123 · SL 59860 (+56) | SH 60103 (+299) · TFI BUY +1200.0 · 1.2 bps · book 1.4× bid · 12.3 tr/s ($84.0k/s) 5.1× "
tail (drawn ENTIRELY in BurstColor / ACC_WARN amber):
  "BURST↑ · ABS↑ 60510 (3.4×)"

CONFIRMED: the absorption tag ("ABS↑ 60510 (3.4×)") is inside the amber-highlighted tail, even though it is a wholly separate signal from the burst call-out. The control's own header comment claims it highlights 'just the burst word' — it does not when anything is appended after the tape field.

=== Finding A2: TapeStripLabel negative tailRect width when the strip overflows its column ===
ClientRectangle.Width = 260
measured prefix width (pw) = 791
tailRect.Width = ClientRectangle.Width - pw = -531
CONFIRMED: tailRect.Width is negative (no clamp exists in TapeStripLabel.vb).
TextRenderer.DrawText did NOT throw on a negative-width Rectangle (GDI DrawTextEx treats the invalid rect as clipped-to-nothing) — so this is a silent rendering defect (the highlighted tail is not drawn where the trader would look for it), not a crash.

=== Finding B: MiniMeter.Pct NaN passthrough ===
input v = NaN
pct after the setter's clamp = NaN (IsNaN = True)
fillW = width * (pct/100) = NaN (IsNaN = True)
`If fillW > 0` is False for NaN, so FillRectangle is never called: the bar silently renders as fully empty, with no exception and no visual flag.
Meanwhile the adjacent numeric label text (MainForm_Render_Cards.vb:2084) would read: "NaN bps" — a raw "NaN bps" shown to the trader next to a progress bar that looks like a normal, healthy zero reading.
```

(In the harness's own numbering: "Finding A" = report Finding 1, "Finding
A2" = report Finding 4, "Finding B" = report Finding 3.)

## What this does and does not prove

- Proves: the substring split in `TapeStripLabel.OnPaint` (audited
  `UI/Controls/TapeStripLabel.vb:42-49`) takes everything from the first
  `"BURST"` match to the end of the string, and `MainForm_LiveStrip.vb`'s
  `ComposeLiveStrip`/`ComposeTape` ordering (verified by reading, not
  re-executed here since it is a `MainForm` partial method — the composition
  order is copied verbatim into the harness) can and does place further tags
  after the tape segment.
- Proves: `TextRenderer.DrawText` does not throw for a `Rectangle` with a
  negative `Width` on this .NET 8 / Windows 11 runtime — this was run, not
  assumed, because a from-code claim of "would throw" would have been the
  wrong kind of confidence for an S0/S1 call.
- Proves: `Single.NaN` fails both comparisons in `MiniMeter.Pct`'s clamp
  (`v < 0`, `v > 100`) and is stored unclamped, and that the resulting
  `fillW` computation is silently skipped rather than throwing.
- Does **not** prove that `r.SpreadBps` is NaN in a real shipped run — that
  depends on `DeribitClient`/order-book code outside this audit's file list.
  The report names the upstream condition (zero-mid order book) that would
  produce it but that call chain was read, not independently re-derived here.
- Does **not** exercise real WinForms control instances (`TapeStripLabel`,
  `MiniMeter`) — the harness re-implements their exact algorithms from the
  audited source rather than instantiating the shipped classes, because
  `UI/Controls/*.vb` are excluded from this standalone project by design (no
  project reference to the shipped sources was set up). The line-cited
  source is reproduced verbatim in the comments above each function so a
  reviewer can diff it against the real file.
