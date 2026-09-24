# Line-for-line replica of ForwardWindowJoiner.PopulateForwardBars, FailureRateMatrix.Compute/WalkBars/WilsonCI,
# AnalysisRunner.ComputeContextOutcomes, OutlierAudit.ComputeOiCvdAsymmetry, MarkdownReportWriter.AppendD4Grid.
# NOT the VB binary: no dotnet in this container. Every branch cited by file:line in the audit.
import random, math, datetime as dt
random.seed(7)
T0=dt.datetime(2026,9,16,13,0)   # Wednesday, NY bucket
ATR=45.0; FLOOR=0.0008; TGT_M=1.75; STOP_M=1.6
# ---- synthetic flush day: 1m OHLC, drift, flush at min 40-43, V bounce, chop
bars={}; p=60000.0
for m in range(0,260):
    o=p
    if 40<=m<=42: c=o-220+random.gauss(0,20); hi=o+5; lo=c-90      # cascade, long lower wicks
    elif 43<=m<=50: c=o+70+random.gauss(0,25); hi=c+40; lo=o-45      # V bounce
    else: c=o+random.gauss(-1,14); hi=max(o,c)+abs(random.gauss(0,12)); lo=min(o,c)-abs(random.gauss(0,12))
    bars[T0+dt.timedelta(minutes=m+1)]=dict(o=o,h=hi,l=lo,c=c)   # key = CloseTime (DeribitOhlcFetcher.vb:79)
    p=c
def close_at(m): return bars[T0+dt.timedelta(minutes=m+1)]['c']
# ---- 100 rows, one per minute (auto-run), logged at :07 with Price = forming-bar close
rows=[]
for i in range(100):
    ts=T0+dt.timedelta(minutes=i,seconds=7)
    price=close_at(i-1) if i>0 else 60000.0
    ret=price-(close_at(i-6) if i>=6 else 60000.0)
    if ret<-150: v='STRONG SHORT'
    elif ret<-40: v='SHORT'
    elif ret>150: v='STRONG LONG'
    elif ret>40: v='LONG'
    elif ret<-15: v='NO TRADE [WEAK SHORT]'   # lean form, AppendLean
    elif ret>15: v='WEAK LONG'
    else: v='NO TRADE'
    ctx=random.choice(['CONFIRMED','FLOW_UNCONFIRMED','MOMENTUM_FADING'])
    r=dict(i=i,ts=ts,price=price,atr=ATR,v=v,ctx=ctx,res=1,
           tl=price+TGT_M*ATR, sl=price-STOP_M*ATR, ts_=price-TGT_M*ATR, ss=price+STOP_M*ATR)
    rows.append(r)
def windows(res): return [5*res,10*res,15*res]
def populate(r):
    rm=r['ts'].replace(second=0); r['fb']={}
    for w in windows(r['res']):
        r['fb'][w]=[bars[k] for k in (rm+dt.timedelta(minutes=cm) for cm in range(3,w+1)) if k in bars]
def walk(bl,fav,adv,isl):
    for b in bl:
        fh = b['h']>=fav if isl else b['l']<=fav
        ah = b['l']<=adv if isl else b['h']>=adv
        if fh and ah: return 'AMBIGUOUS'
        if fh: return 'SUCCESS'
        if ah: return 'ADVERSE_HIT'
    return 'WINDOW_EXPIRED'
def tier(v): return {'STRONG LONG':'STRONG_LONG','LONG':'MEDIUM_LONG','STRONG SHORT':'STRONG_SHORT','SHORT':'MEDIUM_SHORT'}.get(v.strip().upper(),'')
def wilson(f,n):
    z=3.8416;p=f/n;d=1+z/n;c=(p+z/(2*n))/d;m=math.sqrt(p*(1-p)/n+z/(4*n*n))*1.96/d;return max(0,c-m),min(1,c+m)
for r in rows: populate(r)
# ---- matrix + fee-inclusive EV the report never computes
FEE_WIN=0.0003; FEE_LOSS=0.0005; FEE_EXP=0.0005  # maker/maker ; maker+taker stop ; maker+taker market exit
cells={}; evs={}
for r in rows:
    t=tier(r['v'])
    if not t: continue
    isl=t.endswith('LONG'); e=r['price']
    fav=r['tl'] if isl else r['ts_']; adv=r['sl'] if isl else r['ss']
    if abs(fav-e)<FLOOR*e: continue
    for w in windows(1):
        bl=r['fb'][w]
        if not bl: continue
        o=walk(bl,fav,adv,isl)
        c=cells.setdefault((t,w),[0,0,{}]); c[0]+=1; c[1]+= o!='SUCCESS'; c[2][o]=c[2].get(o,0)+1
        if o=='SUCCESS': pnl=abs(fav-e)/e-FEE_WIN
        elif o in('ADVERSE_HIT','AMBIGUOUS'): pnl=-abs(adv-e)/e-FEE_LOSS
        else: pnl=((bl[-1]['c']-e)/e)*(1 if isl else -1)-FEE_EXP
        evs.setdefault((t,w),[]).append(pnl*1e4)
print('--- verdict mix:',{v:sum(1 for r in rows if r['v']==v) for v in set(r['v'] for r in rows)})
print('%-13s %3s %4s %7s %-15s %9s %s'%('tier','W','n','succ','Wilson(succ)','EV bps','outcomes'))
for (t,w),(n,f,oc) in sorted(cells.items()):
    lo,hi=wilson(f,n); ev=sum(evs[(t,w)])/n
    print('%-13s %3d %4d %6.0f%% [%3.0f%%-%3.0f%%] %9.1f %s'%(t,w,n,100*(1-f/n),100*(1-hi),100*(1-lo),ev,oc))
# ---- effective sample: consecutive same-tier rows share forward bars
for t in ['STRONG_SHORT','MEDIUM_SHORT']:
    idx=[r['i'] for r in rows if tier(r['v'])==t]
    runs=1+sum(1 for a,b in zip(idx,idx[1:]) if b-a>1) if idx else 0
    print(f'{t}: {len(idx)} rows but {runs} distinct runs (overlapping 15m windows)')
# ---- ComputeContextOutcomes (AnalysisRunner.vb:278-283) filter: EVAL-1 + res-3 w=10 blackout
leak=[r for r in rows if r['atr']>0 and r['v']!='' and r['v'].upper()!='NO TRADE' and not r['v'].upper().startswith('WEAK') and not tier(r['v'])]
print('context-table rows that are NOT trades (EVAL-1):',len(leak),'e.g.',leak[0]['v'] if leak else None,
      '-> walked as', 'LONG' if leak and 'LONG' in leak[0]['v'].upper() else 'SHORT')
r3=dict(rows[10]); r3['res']=3; populate(r3)
print('res-3 row ForwardBars keys:',sorted(r3['fb']),'| context fallback w=10 present?',10 in r3['fb'])
# ---- OutlierAudit.ComputeOiCvdAsymmetry with every regime n<10
def asym(byreg,L,S):
    lr=L/(L+S); mx,mn=0,1
    for l,s in byreg.values():
        if l+s>=10: rr=l/(l+s); mx=max(mx,rr); mn=min(mn,rr)
    return 'ASYMMETRIC_ALGORITHM' if lr>0.8 and (mx-mn)<0.2 else ('REGIME_PERIOD_BIAS' if (mx-mn)>=0.4 else 'INCONCLUSIVE')
print('OIxCVD 3 regimes x (8L,1S):',asym({'A':(8,1),'B':(8,1),'C':(8,1)},24,3),' <- no regime has n>=10; spread=0-1=-1')
print('OIxCVD one regime (40L,2S) + one (0L,9S):',asym({'A':(40,2),'B':(0,9)},40,11))
# ---- stale-entry: price at the first ELIGIBLE bar's open vs logged entry
gapw=gapl=0; drift=[]
for r in rows:
    t=tier(r['v'])
    if not t: continue
    isl=t.endswith('LONG'); b0=r['fb'][5][0]; e=r['price']
    fav=r['tl'] if isl else r['ts_']; adv=r['sl'] if isl else r['ss']
    drift.append((b0['o']-e)*(1 if isl else -1))
    if (b0['o']>=fav if isl else b0['o']<=fav): gapw+=1
    if (b0['o']<=adv if isl else b0['o']>=adv): gapl+=1
print('first eligible bar opens THROUGH target:',gapw,' THROUGH stop:',gapl,
      ' mean signed entry drift T0->T+2m (USD, + = favourable):',round(sum(drift)/len(drift),1),' range',round(min(drift)),round(max(drift)))
