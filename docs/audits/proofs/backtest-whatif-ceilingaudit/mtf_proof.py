import math, random
def run(seed):
    g = {}
    src = open('audit_proofs_lib.py').read().replace('random.seed(7)', f'random.seed({seed})')
    exec(src, g)
    def ema(c, p):
        cl=[x['c'] for x in c]; k=2/(p+1); e=sum(cl[:p])/p
        for v in cl[p:]: e=v*k+e*(1-k)
        return e
    def mtf(c15):
        w=c15[-60:]; pdi,mdi,adx=g['calc_dmi'](w,9); bull=pdi>mdi
        e9,e21,e50=ema(w,9),ema(w,21),ema(w,50)
        eb=e9>e21>e50; er=e9<e21<e50
        b=(1 if bull else 0)+(1 if adx>=20 and bull else 0)+(1 if eb else 0)
        s=(0 if bull else 1)+(1 if adx>=20 and not bull else 0)+(1 if er else 0)
        return "BULL" if b>=2 else "BEAR" if s>=2 else "FLAT"
    dis=0; passS=0; ex=[]
    for m in range(14*60, 15*60):
        cs=m*60
        r=mtf(g['replay_series'](15,cs,70)); l=mtf(g['live_series'](15,cs,70))
        if r!=l:
            dis+=1
            if (l!="BULL")!=(r!="BULL"): passS+=1
            if len(ex)<3: ex.append(f"{m//60:02d}:{m%60:02d} replay={r} live={l}")
    return dis, passS, ex
tot=0; totS=0
for seed in (7,1,2,3,4,5,6,8,9,10):
    d,s,ex=run(seed); tot+=d; totS+=s
    print(f"seed {seed:2d}: mtfTrend disagreements {d:2d}/60, gatePassShort flips {s:2d}/60  {ex}")
print(f"TOTAL mtfTrend disagreements {tot}/600, gatePassShort flips {totS}/600")
