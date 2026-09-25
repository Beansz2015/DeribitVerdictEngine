# Proof harness — exit guard / live strip / alerts audit (2026-09-24)

Proof code for [`../../2026-09-24-exit-guard-live-strip-alerts.md`](../../2026-09-24-exit-guard-live-strip-alerts.md).

| File | What it is |
|---|---|
| `Program.vb.txt` | The harness (VB.NET console app). Renamed from `.vb` so the root `DeribitVerdictEngine.vbproj`, which compiles every `**/*.vb` outside `tools/` and `verify/`, never picks it up. |
| `FlushAudit.vbproj.txt` | Its project file. Links the **real shipped sources** by path (the same list `verify/ordercheck/OrderCheck.vbproj` links), rooted at `$(DVE_REPO)`. |

**What is real and what is not.** `MarketState`, `ExitGuardEvaluator`, `LiveMicrostructureEvaluator`, `AlertsTracker`, `LevelAbsorptionTracker`, `IndicatorEngine.*` (incl. `CalcSwingPivots`) and `ScoringEngine.ComputeFastExitPrimitives` are the shipped code, loaded with the tracked repo-root `settings.json` (v68 when run). The WinForms debounce/latch block (`UI/MainForm_ExitGuard.vb:118-132`) cannot be linked outside Windows, so `RunSim`/`NoiseRun` mirror it line for line. The trade tape, the order book and the 1m ATR ($60, `ATR_EXEC`) are synthetic.

**Modes:** `trace` (two tick-by-tick flush traces) · `stats` (Monte Carlo, 500 seeds × 4 variants) · `units` (U1–U7 unit proofs) · `noise` (no-flush control, 40 seeds × 30 min × 2 size distributions) · `all` (everything, in that order).

## Run command (exact)

Run from anywhere inside the repo, in bash, with a .NET 8 SDK on `PATH`. It builds in a throwaway temp directory, so nothing is written into the repo.

```bash
export DVE_REPO="$(git rev-parse --show-toplevel)"
W="$(mktemp -d)"
cp "$DVE_REPO/docs/audits/proofs/exit-guard-live-strip-alerts/Program.vb.txt" "$W/Program.vb"
cp "$DVE_REPO/docs/audits/proofs/exit-guard-live-strip-alerts/FlushAudit.vbproj.txt" "$W/FlushAudit.vbproj"
cd "$W" && dotnet run -- all
```

Run on 2026-09-24 against commit `6e74181` (settings v68), Linux, .NET SDK 8.0.131: exit code 0, 77 s wall clock. ⚠ **Not run on Windows**; the command should work under Git Bash but that is untested.

## Output (verbatim, including the two compiler warnings)

```text
/tmp/tmp.VXJDnOjdDE/Program.vb(359,63): warning BC42030: Variable 'mom' is passed by reference before it has been assigned a value. A null reference exception could result at runtime. [/tmp/tmp.VXJDnOjdDE/FlushAudit.vbproj]
/tmp/tmp.VXJDnOjdDE/Program.vb(359,68): warning BC42030: Variable 'sig' is passed by reference before it has been assigned a value. A null reference exception could result at runtime. [/tmp/tmp.VXJDnOjdDE/FlushAudit.vbproj]
settings v68  exit_guard interval=3s debounce=2  strip=2s  alerts level_ticks=12 cascade=3/10s  TFI n=30 Micro n=50  wing5m=3

===== TRACE A: CLEAN flush (guard-favourable), seed 1, carried swing live =====
  carried-level updates (1m full runs):
  full run @ -30.0s  px=99962.0  forming5m.low=99950.0  -> LastSwingLow5m=99700.0  LastSwingHigh5m=100400.0
  full run @ +30.0s  px=98536.5  forming5m.low=98536.5  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +90.0s  px=99054.5  forming5m.low=98041.0  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +150.0s  px=99348.5  forming5m.low=98041.0  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +210.0s  px=99350.0  forming5m.low=98041.0  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  exit-guard ticks (t rel. flush start):   M=MicroCVD O=OFI T=TFI C=CVD B=struct.break
  guard   -8.0s  px= 99966.0 (-0.03%)  SL=  99700  [.....]  win TFI= 4100ms Micro= 8299ms CVD=  81.5s  => clear
  guard   -5.0s  px= 99963.0 (-0.04%)  SL=  99700  [..T..]  win TFI= 4500ms Micro= 7500ms CVD=  83.0s  => clear
  guard   -2.0s  px= 99965.5 (-0.03%)  SL=  99700  [..T..]  win TFI= 5301ms Micro= 8201ms CVD=  82.9s  => clear
  guard   +1.0s  px= 99912.5 (-0.09%)  SL=  99700  [.OTC.]  win TFI=  114ms Micro=  414ms CVD=  69.4s  => ⚠ EXIT? confirming 1/2
  guard   +4.0s  px= 99754.0 (-0.25%)  SL=  99700  [.OTC.]  win TFI=  202ms Micro=  399ms CVD=  17.6s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard   +7.0s  px= 99616.0 (-0.38%)  SL=  99700  [.OTCB]  win TFI=  202ms Micro=  410ms CVD=   5.1s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +10.0s  px= 99488.5 (-0.51%)  SL=  99700  [.OTCB]  win TFI=  204ms Micro=  407ms CVD=   5.6s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +13.0s  px= 99350.5 (-0.65%)  SL=  99700  [.OTCB]  win TFI=  210ms Micro=  410ms CVD=   5.6s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +16.0s  px= 99217.0 (-0.78%)  SL=  99700  [.OTCB]  win TFI=  307ms Micro=  699ms CVD=   5.5s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +19.0s  px= 99070.0 (-0.93%)  SL=  99700  [.OTCB]  win TFI=  207ms Micro=  407ms CVD=   5.4s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +22.0s  px= 98910.0 (-1.09%)  SL=  99700  [.OTCB]  win TFI=  204ms Micro=  400ms CVD=   4.7s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +25.0s  px= 98759.5 (-1.24%)  SL=  99700  [.OTCB]  win TFI=  298ms Micro=  499ms CVD=   4.7s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +28.0s  px= 98615.5 (-1.38%)  SL=  99700  [.OTCB]  win TFI=  302ms Micro=  599ms CVD=   4.9s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +31.0s  px= 98483.5 (-1.52%)  SL=  98900  [.OTCB]  win TFI=  207ms Micro=  403ms CVD=   5.2s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +34.0s  px= 98326.5 (-1.67%)  SL=  98900  [.OTCB]  win TFI=  202ms Micro=  399ms CVD=   4.9s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +37.0s  px= 98182.5 (-1.82%)  SL=  98900  [.OTCB]  win TFI=  202ms Micro=  404ms CVD=   5.0s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +40.0s  px= 98491.9 (-1.51%)  SL=  98900  [..TCB]  win TFI=  395ms Micro=  595ms CVD=   5.0s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +43.0s  px= 98498.0 (-1.50%)  SL=  98900  [....B]  win TFI=  504ms Micro=  903ms CVD=   6.4s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +46.0s  px= 98521.4 (-1.48%)  SL=  98900  [....B]  win TFI=  597ms Micro=  801ms CVD=   7.9s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +49.0s  px= 98547.9 (-1.45%)  SL=  98900  [....B]  win TFI=  402ms Micro=  795ms CVD=   9.1s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +52.0s  px= 98590.8 (-1.41%)  SL=  98900  [....B]  win TFI=  501ms Micro=  902ms CVD=   9.0s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +55.0s  px= 98618.0 (-1.38%)  SL=  98900  [....B]  win TFI=  503ms Micro=  903ms CVD=   9.0s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +58.0s  px= 98651.5 (-1.35%)  SL=  98900  [....B]  win TFI=  703ms Micro= 1296ms CVD=   9.1s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +61.0s  px= 98710.0 (-1.29%)  SL=  98900  [....B]  win TFI=  403ms Micro=  707ms CVD=   8.9s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +64.0s  px= 98728.5 (-1.27%)  SL=  98900  [M...B]  win TFI=  507ms Micro= 1005ms CVD=   9.0s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +67.0s  px= 98761.5 (-1.24%)  SL=  98900  [....B]  win TFI=  502ms Micro= 1098ms CVD=   8.9s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +70.0s  px= 98778.5 (-1.22%)  SL=  98900  [....B]  win TFI=  695ms Micro= 1001ms CVD=   9.4s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +73.0s  px= 98835.0 (-1.17%)  SL=  98900  [....B]  win TFI=  698ms Micro= 1099ms CVD=   9.3s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +76.0s  px= 98857.5 (-1.14%)  SL=  98900  [..T.B]  win TFI=  598ms Micro=  898ms CVD=   9.3s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +79.0s  px= 98897.0 (-1.10%)  SL=  98900  [....B]  win TFI=  502ms Micro=  901ms CVD=   9.1s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +82.0s  px= 98942.5 (-1.06%)  SL=  98900  [.....]  win TFI=  503ms Micro=  800ms CVD=   9.2s  => ⚠ EXIT — clear
  guard  +85.0s  px= 98988.0 (-1.01%)  SL=  98900  [.....]  win TFI=  402ms Micro=  897ms CVD=   9.2s  => clear
  guard  +88.0s  px= 99022.0 (-0.98%)  SL=  98900  [.....]  win TFI=  503ms Micro=  800ms CVD=   8.6s  => clear
  guard  +91.0s  px= 99062.0 (-0.94%)  SL=  98900  [.....]  win TFI=  504ms Micro=  995ms CVD=   8.6s  => clear
  guard  +94.0s  px= 99062.0 (-0.94%)  SL=  98900  [.....]  win TFI=  498ms Micro=  803ms CVD=   8.9s  => clear
  guard  +97.0s  px= 99098.0 (-0.90%)  SL=  98900  [.....]  win TFI=  597ms Micro=  901ms CVD=   9.4s  => clear
  guard +100.0s  px= 99097.5 (-0.90%)  SL=  98900  [.....]  win TFI=  505ms Micro= 1005ms CVD=  10.0s  => clear
  guard +103.0s  px= 99111.9 (-0.89%)  SL=  98900  [M.T..]  win TFI=  600ms Micro=  996ms CVD=  10.0s  => ⚠ EXIT? confirming 1/2
  guard +106.0s  px= 99161.4 (-0.84%)  SL=  98900  [.....]  win TFI=  501ms Micro=  895ms CVD=  10.2s  => clear
  guard +109.0s  px= 99194.4 (-0.81%)  SL=  98900  [.....]  win TFI=  494ms Micro=  801ms CVD=   9.6s  => clear
  guard +112.0s  px= 99248.9 (-0.75%)  SL=  98900  [.....]  win TFI=  309ms Micro=  806ms CVD=   9.0s  => clear
  guard +115.0s  px= 99306.4 (-0.69%)  SL=  98900  [.....]  win TFI=  400ms Micro=  700ms CVD=   8.4s  => clear
  guard +118.0s  px= 99371.4 (-0.63%)  SL=  98900  [.....]  win TFI=  503ms Micro=  804ms CVD=   8.3s  => clear
  guard +121.0s  px= 99350.0 (-0.65%)  SL=  98900  [.O...]  win TFI= 1492ms Micro= 1796ms CVD=   9.5s  => clear
  guard +124.0s  px= 99350.0 (-0.65%)  SL=  98900  [.....]  win TFI= 3201ms Micro= 4300ms CVD=  12.1s  => clear
  guard +127.0s  px= 99348.0 (-0.65%)  SL=  98900  [..T..]  win TFI= 2600ms Micro= 5597ms CVD=  14.7s  => clear
  guard +130.0s  px= 99348.0 (-0.65%)  SL=  98900  [M....]  win TFI= 1600ms Micro= 3699ms CVD=  16.7s  => clear
  guard +133.0s  px= 99348.0 (-0.65%)  SL=  98900  [.....]  win TFI= 3002ms Micro= 4000ms CVD=  19.2s  => clear
  guard +136.0s  px= 99348.0 (-0.65%)  SL=  98900  [.....]  win TFI= 3098ms Micro= 5400ms CVD=  21.8s  => clear
  guard +139.0s  px= 99343.5 (-0.66%)  SL=  98900  [..T..]  win TFI= 2603ms Micro= 5702ms CVD=  24.6s  => clear
  guard +142.0s  px= 99346.5 (-0.65%)  SL=  98900  [.....]  win TFI= 1500ms Micro= 2401ms CVD=  26.7s  => clear
  guard +145.0s  px= 99344.5 (-0.66%)  SL=  98900  [.....]  win TFI= 3000ms Micro= 4000ms CVD=  29.2s  => clear
  guard +148.0s  px= 99348.0 (-0.65%)  SL=  98900  [.....]  win TFI= 3699ms Micro= 5099ms CVD=  31.7s  => clear
  TAPE strip ticks (-6s .. +60s):
  strip   -5.5s   99965.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE   5.9 tr/s      41k/s  burst=NORMAL 1.1x       tags: 
  strip   -3.5s   99963.0  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE   6.5 tr/s      44k/s  burst=NORMAL 1.0x       tags: 
  strip   -1.5s   99966.0  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE   5.9 tr/s      44k/s  burst=NORMAL 1.1x       tags: 
  strip   +0.5s   99937.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  10.6 tr/s     245k/s  burst=BURST_SELL 5.4x   tags: 
  strip   +2.5s   99834.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  29.8 tr/s    1049k/s  burst=BURST_SELL 7.8x   tags: 
  strip   +4.5s   99728.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  49.8 tr/s    2007k/s  burst=BURST_SELL 7.5x   tags: 
  strip   +6.5s   99641.5  [HVN↓ 99550 | SL 99700]  TFI SELL PRESSURE  65.9 tr/s    2776k/s  burst=BURST_SELL 6.5x   tags: 
  strip   +8.5s   99554.5  [HVN↓ 99550 | SL 99700]  TFI SELL PRESSURE  82.4 tr/s    3632k/s  burst=BURST_SELL 5.8x   tags: NEAR↓ 99550
  strip  +10.5s   99466.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  94.3 tr/s    4141k/s  burst=BURST_SELL 5.0x   tags: 
  strip  +12.5s   99375.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  91.8 tr/s    4249k/s  burst=BURST_SELL 4.7x   tags: 
  strip  +14.5s   99273.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  91.0 tr/s    4260k/s  burst=NORMAL 4.4x       tags: 
  strip  +16.5s   99194.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  89.5 tr/s    4136k/s  burst=NORMAL 3.8x       tags: 
  strip  +18.5s   99097.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  91.4 tr/s    4067k/s  burst=NORMAL 3.5x       tags: NEAR↑ 99100
  strip  +20.5s   98996.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  94.0 tr/s    4271k/s  burst=NORMAL 3.3x       tags: 
  strip  +22.5s   98892.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  96.7 tr/s    4297k/s  burst=NORMAL 3.2x       tags: 
  strip  +24.5s   98782.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  98.2 tr/s    4329k/s  burst=NORMAL 3.1x       tags: 
  strip  +26.5s   98687.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE 101.3 tr/s    4475k/s  burst=NORMAL 2.8x       tags: 
  strip  +28.5s   98596.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE 100.3 tr/s    4434k/s  burst=NORMAL 2.6x       tags: 
  strip  +30.5s   98510.5  [-- | SL 98900]  TFI SELL PRESSURE  97.1 tr/s    4317k/s  burst=NORMAL 2.4x       tags: 
  strip  +32.5s   98408.0  [-- | SL 98900]  TFI SELL PRESSURE  96.8 tr/s    4366k/s  burst=NORMAL 2.5x       tags: 
  strip  +34.5s   98305.5  [-- | SL 98900]  TFI SELL PRESSURE  95.4 tr/s    4318k/s  burst=NORMAL 2.4x       tags: 
  strip  +36.5s   98209.0  [-- | SL 98900]  TFI SELL PRESSURE  95.7 tr/s    4465k/s  burst=NORMAL 2.3x       tags: 
  strip  +38.5s   98108.5  [-- | SL 98900]  TFI SELL PRESSURE  97.5 tr/s    4554k/s  burst=NORMAL 2.2x       tags: 
  strip  +40.5s   98509.0  [-- | SL 98900]  TFI NEUTRAL        97.1 tr/s    4515k/s  burst=NORMAL 2.0x       tags: 
  strip  +42.5s   98498.6  [-- | SL 98900]  TFI SELL PRESSURE  87.4 tr/s    3934k/s  burst=NORMAL 1.7x       tags: 
  strip  +44.5s   98495.5  [-- | SL 98900]  TFI NEUTRAL        76.3 tr/s    3260k/s  burst=NORMAL 1.4x       tags: 
  strip  +46.5s   98525.9  [-- | SL 98900]  TFI BUY PRESSURE   68.4 tr/s    2558k/s  burst=NORMAL 1.1x       tags: 
  strip  +48.5s   98545.9  [-- | SL 98900]  TFI BUY PRESSURE   59.5 tr/s    1944k/s  burst=NORMAL 0.9x       tags: 
  strip  +50.5s   98568.8  [-- | SL 98900]  TFI BUY PRESSURE   55.1 tr/s    1403k/s  burst=NORMAL 0.8x       tags: 
  strip  +52.5s   98587.8  [-- | SL 98900]  TFI BUY PRESSURE   54.7 tr/s    1214k/s  burst=NORMAL 0.7x       tags: 
  strip  +54.5s   98613.5  [-- | SL 98900]  TFI BUY PRESSURE   56.0 tr/s    1186k/s  burst=NORMAL 0.7x       tags: 
  strip  +56.5s   98647.5  [-- | SL 98900]  TFI BUY PRESSURE   57.2 tr/s    1199k/s  burst=NORMAL 0.7x       tags: 
  strip  +58.5s   98670.5  [-- | SL 98900]  TFI BUY PRESSURE   56.2 tr/s    1156k/s  burst=NORMAL 0.7x       tags: 
  approach episode existed on 66 of the 100ms steps; strip showed NEAR on 2 ticks
  alert events: (none)
  first latch: +4.0s at 99754.0 (-0.25%)   first unlatch: +85.0s at 98988.0 (-1.01%)   latch episodes=1  un-confirmed EXIT? ticks=1

===== TRACE B: CHOPPY flush, seed 7, carried swing live =====
  carried-level updates (1m full runs):
  full run @ -30.0s  px=99988.5  forming5m.low=99950.0  -> LastSwingLow5m=99700.0  LastSwingHigh5m=100400.0
  full run @ +30.0s  px=98881.5  forming5m.low=98875.5  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +90.0s  px=98992.0  forming5m.low=98490.1  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +150.0s  px=99326.1  forming5m.low=98490.1  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  full run @ +210.0s  px=99330.6  forming5m.low=98490.1  -> LastSwingLow5m=98900.0  LastSwingHigh5m=100400.0
  exit-guard ticks (t rel. flush start):   M=MicroCVD O=OFI T=TFI C=CVD B=struct.break
  guard   -8.0s  px= 99990.0 (-0.01%)  SL=  99700  [.....]  win TFI= 4100ms Micro= 6898ms CVD=  84.3s  => clear
  guard   -5.0s  px= 99992.0 (-0.01%)  SL=  99700  [.....]  win TFI= 6002ms Micro= 8102ms CVD=  85.4s  => clear
  guard   -2.0s  px= 99992.5 (-0.01%)  SL=  99700  [.....]  win TFI= 2700ms Micro= 7699ms CVD=  80.3s  => clear
  guard   +1.0s  px= 99958.5 (-0.04%)  SL=  99700  [MOTC.]  win TFI=  307ms Micro=  595ms CVD=  67.2s  => ⚠ EXIT? confirming 1/2
  guard   +4.0s  px= 99846.5 (-0.15%)  SL=  99700  [.OTC.]  win TFI=  401ms Micro=  696ms CVD=  31.6s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard   +7.0s  px= 99728.0 (-0.27%)  SL=  99700  [..TC.]  win TFI=  214ms Micro=  506ms CVD=   5.6s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +10.0s  px= 99619.5 (-0.38%)  SL=  99700  [..TCB]  win TFI=  299ms Micro=  502ms CVD=   5.5s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +13.0s  px= 99498.5 (-0.50%)  SL=  99700  [..TCB]  win TFI=  301ms Micro=  409ms CVD=   5.2s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +16.0s  px= 99390.5 (-0.61%)  SL=  99700  [.OTCB]  win TFI=  302ms Micro=  504ms CVD=   5.4s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +19.0s  px= 99283.0 (-0.72%)  SL=  99700  [.OTCB]  win TFI=  203ms Micro=  308ms CVD=   5.3s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +22.0s  px= 99169.0 (-0.83%)  SL=  99700  [MOTCB]  win TFI=  203ms Micro=  504ms CVD=   5.1s  => ⚠ EXIT — 4 adverse (MicroCVD BEAR_ACCEL, OFI SELL, TFI SELL, CVD FALLING)
  guard  +25.0s  px= 99057.5 (-0.94%)  SL=  99700  [.OTCB]  win TFI=  407ms Micro=  605ms CVD=   5.6s  => ⚠ EXIT — 3 adverse (OFI SELL, TFI SELL, CVD FALLING)
  guard  +28.0s  px= 98934.5 (-1.07%)  SL=  99700  [..TCB]  win TFI=  211ms Micro=  411ms CVD=   5.4s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +31.0s  px= 98825.5 (-1.17%)  SL=  98900  [MOTCB]  win TFI=  202ms Micro=  407ms CVD=   5.0s  => ⚠ EXIT — 4 adverse (MicroCVD BEAR_ACCEL, OFI SELL, TFI SELL, CVD FALLING)
  guard  +34.0s  px= 98719.0 (-1.28%)  SL=  98900  [MOTCB]  win TFI=  206ms Micro=  405ms CVD=   5.2s  => ⚠ EXIT — 4 adverse (MicroCVD BEAR_ACCEL, OFI SELL, TFI SELL, CVD FALLING)
  guard  +37.0s  px= 98611.0 (-1.39%)  SL=  98900  [MOTCB]  win TFI=  304ms Micro=  411ms CVD=   5.4s  => ⚠ EXIT — 4 adverse (MicroCVD BEAR_DECEL, OFI SELL, TFI SELL, CVD FALLING)
  guard  +40.0s  px= 98491.1 (-1.51%)  SL=  98900  [..TCB]  win TFI=  297ms Micro=  598ms CVD=   5.9s  => ⚠ EXIT — 2 adverse (TFI SELL, CVD FALLING)
  guard  +43.0s  px= 98502.6 (-1.50%)  SL=  98900  [....B]  win TFI=  501ms Micro= 1000ms CVD=   7.1s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +46.0s  px= 98511.5 (-1.49%)  SL=  98900  [....B]  win TFI=  696ms Micro=  995ms CVD=   8.5s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +49.0s  px= 98546.5 (-1.45%)  SL=  98900  [....B]  win TFI=  406ms Micro=  806ms CVD=   9.3s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +52.0s  px= 98576.6 (-1.42%)  SL=  98900  [....B]  win TFI=  498ms Micro=  799ms CVD=   9.4s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +55.0s  px= 98622.5 (-1.38%)  SL=  98900  [....B]  win TFI=  501ms Micro=  901ms CVD=   9.2s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +58.0s  px= 98654.5 (-1.35%)  SL=  98900  [....B]  win TFI=  596ms Micro=  900ms CVD=   9.7s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +61.0s  px= 98701.5 (-1.30%)  SL=  98900  [....B]  win TFI=  697ms Micro=  903ms CVD=   9.5s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +64.0s  px= 98727.0 (-1.27%)  SL=  98900  [....B]  win TFI=  401ms Micro=  702ms CVD=   9.2s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +67.0s  px= 98775.0 (-1.22%)  SL=  98900  [....B]  win TFI=  401ms Micro=  705ms CVD=   8.3s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +70.0s  px= 98818.5 (-1.18%)  SL=  98900  [....B]  win TFI=  401ms Micro=  607ms CVD=   8.4s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +73.0s  px= 98848.0 (-1.15%)  SL=  98900  [....B]  win TFI=  599ms Micro=  805ms CVD=   8.8s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +76.0s  px= 98869.5 (-1.13%)  SL=  98900  [....B]  win TFI=  505ms Micro=  900ms CVD=   8.9s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +79.0s  px= 98890.0 (-1.11%)  SL=  98900  [....B]  win TFI=  702ms Micro= 1200ms CVD=   9.3s  => ⚠ EXIT — structural break (swing low 98900.0)
  guard  +82.0s  px= 98907.0 (-1.09%)  SL=  98900  [..T..]  win TFI=  506ms Micro=  805ms CVD=   9.3s  => ⚠ EXIT — clear
  guard  +85.0s  px= 98946.5 (-1.05%)  SL=  98900  [.....]  win TFI=  602ms Micro= 1101ms CVD=   9.6s  => clear
  guard  +88.0s  px= 98979.0 (-1.02%)  SL=  98900  [.....]  win TFI=  304ms Micro=  702ms CVD=   9.2s  => clear
  guard  +91.0s  px= 99011.0 (-0.99%)  SL=  98900  [.....]  win TFI=  498ms Micro=  799ms CVD=   8.8s  => clear
  guard  +94.0s  px= 99039.5 (-0.96%)  SL=  98900  [.....]  win TFI=  504ms Micro=  802ms CVD=   8.7s  => clear
  guard  +97.0s  px= 99077.5 (-0.92%)  SL=  98900  [.....]  win TFI=  403ms Micro=  703ms CVD=   8.5s  => clear
  guard +100.0s  px= 99126.5 (-0.87%)  SL=  98900  [.....]  win TFI=  597ms Micro=  905ms CVD=   8.5s  => clear
  guard +103.0s  px= 99156.0 (-0.84%)  SL=  98900  [.....]  win TFI=  503ms Micro= 1003ms CVD=   8.9s  => clear
  guard +106.0s  px= 99173.0 (-0.83%)  SL=  98900  [.....]  win TFI=  507ms Micro=  906ms CVD=   9.4s  => clear
  guard +109.0s  px= 99188.5 (-0.81%)  SL=  98900  [.....]  win TFI=  601ms Micro= 1003ms CVD=  10.2s  => clear
  guard +112.0s  px= 99220.6 (-0.78%)  SL=  98900  [.....]  win TFI=  504ms Micro=  898ms CVD=  10.1s  => clear
  guard +115.0s  px= 99248.2 (-0.75%)  SL=  98900  [.....]  win TFI=  502ms Micro=  803ms CVD=  10.2s  => clear
  guard +118.0s  px= 99285.6 (-0.71%)  SL=  98900  [.....]  win TFI=  500ms Micro=  897ms CVD=   9.7s  => clear
  guard +121.0s  px= 99317.1 (-0.68%)  SL=  98900  [.....]  win TFI= 1300ms Micro= 1693ms CVD=  10.1s  => clear
  guard +124.0s  px= 99319.6 (-0.68%)  SL=  98900  [.....]  win TFI= 2903ms Micro= 4197ms CVD=  12.6s  => clear
  guard +127.0s  px= 99318.6 (-0.68%)  SL=  98900  [..T..]  win TFI= 2500ms Micro= 4600ms CVD=  14.6s  => clear
  guard +130.0s  px= 99320.6 (-0.68%)  SL=  98900  [M....]  win TFI= 3501ms Micro= 5599ms CVD=  17.3s  => clear
  guard +133.0s  px= 99322.1 (-0.68%)  SL=  98900  [.....]  win TFI= 3901ms Micro= 6302ms CVD=  19.7s  => clear
  guard +136.0s  px= 99324.6 (-0.68%)  SL=  98900  [..T..]  win TFI= 2200ms Micro= 5202ms CVD=  22.2s  => clear
  guard +139.0s  px= 99325.1 (-0.67%)  SL=  98900  [M....]  win TFI= 3299ms Micro= 4701ms CVD=  24.8s  => clear
  guard +142.0s  px= 99327.6 (-0.67%)  SL=  98900  [..T..]  win TFI= 3302ms Micro= 6202ms CVD=  27.4s  => clear
  guard +145.0s  px= 99327.6 (-0.67%)  SL=  98900  [.....]  win TFI= 1602ms Micro= 3302ms CVD=  29.2s  => clear
  guard +148.0s  px= 99326.1 (-0.67%)  SL=  98900  [.....]  win TFI= 3401ms Micro= 4502ms CVD=  32.1s  => clear
  TAPE strip ticks (-6s .. +60s):
  strip   -5.5s   99991.5  [SL 99700 | HVN↑ 100150]  TFI BUY PRESSURE    6.2 tr/s      36k/s  burst=NORMAL 0.8x       tags: 
  strip   -3.5s   99992.5  [SL 99700 | HVN↑ 100150]  TFI BUY PRESSURE    6.6 tr/s      40k/s  burst=NORMAL 1.0x       tags: 
  strip   -1.5s   99992.5  [SL 99700 | HVN↑ 100150]  TFI NEUTRAL         6.0 tr/s      36k/s  burst=NORMAL 1.0x       tags: 
  strip   +0.5s   99980.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  11.6 tr/s     274k/s  burst=BURST_SELL 6.0x   tags: 
  strip   +2.5s   99901.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  27.8 tr/s     943k/s  burst=BURST_SELL 7.8x   tags: 
  strip   +4.5s   99822.0  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  43.3 tr/s    1602k/s  burst=BURST_SELL 7.2x   tags: 
  strip   +6.5s   99752.5  [SL 99700 | HVN↑ 100150]  TFI SELL PRESSURE  59.9 tr/s    2294k/s  burst=BURST_SELL 6.6x   tags: 
  strip   +8.5s   99679.0  [HVN↓ 99550 | SL 99700]  TFI SELL PRESSURE  76.0 tr/s    2940k/s  burst=BURST_SELL 5.7x   tags: 
  strip  +10.5s   99602.5  [HVN↓ 99550 | SL 99700]  TFI SELL PRESSURE  87.4 tr/s    3371k/s  burst=BURST_SELL 5.1x   tags: 
  strip  +12.5s   99525.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  90.1 tr/s    3450k/s  burst=BURST_SELL 4.7x   tags: 
  strip  +14.5s   99450.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  91.9 tr/s    3547k/s  burst=NORMAL 4.3x       tags: 
  strip  +16.5s   99372.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  90.5 tr/s    3526k/s  burst=NORMAL 3.9x       tags: 
  strip  +18.5s   99292.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  92.9 tr/s    3664k/s  burst=NORMAL 3.7x       tags: 
  strip  +20.5s   99226.0  [-- | HVN↓ 99550]  TFI SELL PRESSURE  93.0 tr/s    3560k/s  burst=NORMAL 3.2x       tags: 
  strip  +22.5s   99151.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  93.0 tr/s    3481k/s  burst=NORMAL 3.0x       tags: 
  strip  +24.5s   99072.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  92.8 tr/s    3273k/s  burst=NORMAL 2.7x       tags: 
  strip  +26.5s   98999.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  93.8 tr/s    3215k/s  burst=NORMAL 2.6x       tags: 
  strip  +28.5s   98926.5  [-- | HVN↓ 99550]  TFI SELL PRESSURE  93.5 tr/s    3214k/s  burst=NORMAL 2.6x       tags: 
  strip  +30.5s   98850.0  [-- | SL 98900]  TFI SELL PRESSURE  94.4 tr/s    3298k/s  burst=NORMAL 2.5x       tags: 
  strip  +32.5s   98771.5  [-- | SL 98900]  TFI SELL PRESSURE  94.7 tr/s    3323k/s  burst=NORMAL 2.4x       tags: 
  strip  +34.5s   98704.0  [-- | SL 98900]  TFI SELL PRESSURE  93.9 tr/s    3396k/s  burst=NORMAL 2.2x       tags: 
  strip  +36.5s   98622.0  [-- | SL 98900]  TFI SELL PRESSURE  94.0 tr/s    3462k/s  burst=NORMAL 2.2x       tags: 
  strip  +38.5s   98548.5  [-- | SL 98900]  TFI SELL PRESSURE  91.3 tr/s    3189k/s  burst=NORMAL 2.0x       tags: 
  strip  +40.5s   98491.7  [-- | SL 98900]  TFI SELL PRESSURE  85.5 tr/s    2953k/s  burst=NORMAL 1.7x       tags: 
  strip  +42.5s   98492.1  [-- | SL 98900]  TFI BUY PRESSURE   76.9 tr/s    2660k/s  burst=NORMAL 1.6x       tags: 
  strip  +44.5s   98503.7  [-- | SL 98900]  TFI NEUTRAL        69.5 tr/s    2350k/s  burst=NORMAL 1.4x       tags: 
  strip  +46.5s   98525.4  [-- | SL 98900]  TFI BUY PRESSURE   61.7 tr/s    1829k/s  burst=NORMAL 1.1x       tags: 
  strip  +48.5s   98541.3  [-- | SL 98900]  TFI NEUTRAL        55.7 tr/s    1495k/s  burst=NORMAL 0.9x       tags: 
  strip  +50.5s   98560.2  [-- | SL 98900]  TFI BUY PRESSURE   53.2 tr/s    1308k/s  burst=NORMAL 0.9x       tags: 
  strip  +52.5s   98582.5  [-- | SL 98900]  TFI BUY PRESSURE   52.3 tr/s    1076k/s  burst=NORMAL 0.8x       tags: 
  strip  +54.5s   98613.0  [-- | SL 98900]  TFI BUY PRESSURE   53.7 tr/s     981k/s  burst=NORMAL 0.8x       tags: 
  strip  +56.5s   98642.0  [-- | SL 98900]  TFI BUY PRESSURE   52.4 tr/s     991k/s  burst=NORMAL 0.7x       tags: 
  strip  +58.5s   98656.5  [-- | SL 98900]  TFI BUY PRESSURE   51.3 tr/s     973k/s  burst=NORMAL 0.7x       tags: 
  approach episode existed on 24 of the 100ms steps; strip showed NEAR on 0 ticks
  alert events: (none)
  first latch: +4.0s at 99846.5 (-0.15%)   first unlatch: +85.0s at 98946.5 (-1.05%)   latch episodes=2  un-confirmed EXIT? ticks=1

===== MONTE CARLO (500 seeds each) =====
  CleanFlush   swing arm ON : already latched by NOISE at flush start 119/500; never latched in flush 0 | fresh latch median +4.0s at -0.20% (p90 +4.0s, worst -0.25%) | clears after bottom: median +82.0s at -1.04% (p10 -1.07%), still latched at +210s in 0 | tape-only 2-of-4 EXIT ticks 3213/10471 | MicroCVD adverse 120/6500 flush ticks | NEAR existed 1432s total, strip saw 185 ticks
  CleanFlush   swing arm OFF: already latched by NOISE at flush start 119/500; never latched in flush 0 | fresh latch median +4.0s at -0.20% (p90 +4.0s, worst -0.25%) | clears after bottom: median +46.0s at -1.48% (p10 -1.49%), still latched at +210s in 0 | tape-only 2-of-4 EXIT ticks 3213/10471 | MicroCVD adverse 120/6500 flush ticks | NEAR existed 1432s total, strip saw 185 ticks
  ChoppyFlush  swing arm ON : already latched by NOISE at flush start 119/500; never latched in flush 0 | fresh latch median +4.0s at -0.15% (p90 +4.0s, worst -0.26%) | clears after bottom: median +82.0s at -1.05% (p10 -1.07%), still latched at +210s in 0 | tape-only 2-of-4 EXIT ticks 5052/10355 | MicroCVD adverse 1747/6500 flush ticks | NEAR existed 1488s total, strip saw 40 ticks
  ChoppyFlush  swing arm OFF: already latched by NOISE at flush start 119/500; never latched in flush 0 | fresh latch median +4.0s at -0.15% (p90 +4.0s, worst -0.26%) | clears after bottom: median +46.0s at -1.48% (p10 -1.49%), still latched at +210s in 0 | tape-only 2-of-4 EXIT ticks 5052/10355 | MicroCVD adverse 1747/6500 flush ticks | NEAR existed 1488s total, strip saw 40 ticks

===== UNIT PROOFS (real code) =====
  U1 MicroCVD steady $2.5M one-way sell   : early=-800k mid=-800k late=-900k -> FLAT       MicroAdverse(long)=False
  U1 MicroCVD sell-off that is FADING     : early=-1280k mid=-380k late=-360k -> BEAR_DECEL MicroAdverse(long)=True
  U2 long opened at 99,650 under carried SL 99,700 (balanced tape, no book): Kind=Exit Reason="structural break (swing low 99700.0)"
  U3 exception path: Kind=Clear Reason=""  |  thin buffer (10 trades, price UNDER swing low): Kind=Clear Reason=""  -> host renders both as "EXIT GUARD · clear"
  U4 3 prints x $100 of LONG liquidation, flag "T": CASCADE_BELOW count=3 usd=$300 events=FIRST_SEEN:SELL,CASCADE:SELL
  U4 3 prints x $100 of LONG liquidation, flag "M": CASCADE_ABOVE count=3 usd=$300 events=CASCADE:BUY
  U5 true tape 20.0 tr/s; receive-loop lag / clock skew  0.0s -> strip reads 20.0 tr/s
  U5 true tape 20.0 tr/s; receive-loop lag / clock skew  2.0s -> strip reads 16.0 tr/s
  U5 true tape 20.0 tr/s; receive-loop lag / clock skew  5.0s -> strip reads 10.0 tr/s
  U5 true tape 20.0 tr/s; receive-loop lag / clock skew 11.0s -> strip reads 0.0 tr/s
  U5b 5 liq prints in 0.5 s; strip read with local clock = exchange: CASCADE_BELOW (5); local clock 11 s ahead: NONE (0) events still delivered=1
  U7 bestBid=99704.0 eaten so far=$0k  SizeStart=80k SizeMin=80k aggr=0k ratio=0.0 pullFrac=0.00 -> NONE
  U7 bestBid=99703.0 eaten so far=$80k  SizeStart=80k SizeMin=80k aggr=80k ratio=16.0 pullFrac=0.00 -> ABSORB_BELOW
  U7 bestBid=99702.0 eaten so far=$160k  SizeStart=80k SizeMin=80k aggr=160k ratio=32.0 pullFrac=0.00 -> ABSORB_BELOW
  U7 bestBid=99701.0 eaten so far=$240k  SizeStart=80k SizeMin=80k aggr=240k ratio=48.0 pullFrac=0.00 -> ABSORB_BELOW
  U7 bestBid=99700.0 eaten so far=$320k  SizeStart=80k SizeMin=80k aggr=320k ratio=64.0 pullFrac=0.00 -> ABSORB_BELOW
  U7 bestBid=99699.0 eaten so far=$400k  SizeStart=0k SizeMin=0k aggr=0k ratio=0.0 pullFrac=0.00 -> NONE
  U7 bestBid=99698.0 eaten so far=$480k  SizeStart=0k SizeMin=0k aggr=0k ratio=0.0 pullFrac=0.00 -> NONE
  U7 bestBid=99697.5 eaten so far=$520k  SizeStart=0k SizeMin=0k aggr=0k ratio=0.0 pullFrac=0.00 -> NONE
  U7 bestBid=99697.0 eaten so far=$560k  SizeStart=0k SizeMin=0k aggr=0k ratio=0.0 pullFrac=0.00 -> NONE
  U7 after print at 99696.5 (level - breakTol): NONE
  U6 5m swing low (wing 3) while forming bar low is 99,700: 99350; forming bar trades to 99,300 (through it), last=99,310: 99200  -> break test 'price<=SL' is False

===== CONTROL: balanced tape, NO flush, 30 min per seed, long declared, swing 1% away =====
  size sigma 0.9: 902 false EXIT latches in 1200 min (45.1/hour), latched 18.8% of ticks; per-tick adverse rate TFI 29% CVD 28% Micro 17% OFI 8%  2+ adverse 22%
  size sigma 1.5: 1108 false EXIT latches in 1200 min (55.4/hour), latched 23.7% of ticks; per-tick adverse rate TFI 37% CVD 28% Micro 23% OFI 8%  2+ adverse 28%
```
