import math, random
def run(seed, pre_drift_pct):
    g = {}
    src = open('audit_proofs_lib.py').read().replace('random.seed(7)', f'random.seed({seed})')
    # add a pre-flush uptrend from 04:00 to 14:00 of pre_drift_pct total
    src = src.replace("    drift = 0.0\n", f"    drift = ({pre_drift_pct}*100000/36000) if 4*3600 <= t < 14*3600 else 0.0\n")
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
    dis=0; passS=0; states=[]
    for m in range(14*60, 15*60):
        cs=m*60
        r=mtf(g['replay_series'](15,cs,70)); l=mtf(g['live_series'](15,cs,70))
        states.append((m, r, l))
        if r!=l:
            dis+=1
            if (l!="BULL")!=(r!="BULL"): passS+=1
    return dis, passS, states
for drift in (0.0, 0.015, 0.03):
    tot=0; totS=0
    for seed in range(1, 21):
        d,s,st=run(seed, drift); tot+=d; totS+=s
    print(f"pre-flush drift {drift*100:.1f}%: mtfTrend disagreements {tot}/1200, gatePassShort flips {totS}/1200")
d,s,st=run(3,0.03)
print("seed 3, +3% pre-trend:", " ".join(f"{m%60:02d}:{r[0]}/{l[0]}" for m,r,l in st[:20]))
