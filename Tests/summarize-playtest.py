#!/usr/bin/env python3
"""Summarize instrumented UnityShim trials, not subjective fun or pixel learning."""
import argparse, csv, json, math, statistics
from collections import defaultdict
from pathlib import Path

KEY = ('profile','seed','boss','dda','round')
FIELDS = ('outcome','steps','seconds','playerHp','bossHp','hits')

def wilson(wins, n):
    z=1.96; p=wins/n; d=1+z*z/n
    mid=(p+z*z/(2*n))/d; spread=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
    return [round(max(0,mid-spread),4),round(min(1,mid+spread),4)]

def summarize(rows):
    battles={}; patterns=defaultdict(lambda: {'opportunities':0,'hits':0,'parries':0})
    for row in rows:
        key=tuple(row[k] for k in KEY)
        if row['dda'] not in ('True','False') or row['outcome'] not in ('clear','death','timeout'):
            raise ValueError('Invalid DDA/outcome')
        for name in ('steps','seconds','playerHp','bossHp','hits','opportunities','patternHits','parries','level','success'):
            if not math.isfinite(float(row[name])): raise ValueError('Non-finite '+name)
        for name in ('longestNoDamageSeconds','stationaryFraction'):
            if row.get(name) and (not math.isfinite(float(row[name])) or float(row[name])<0): raise ValueError('Invalid '+name)
        if row.get('stationaryFraction') and float(row['stationaryFraction'])>1: raise ValueError('Invalid stationaryFraction')
        if float(row['seconds']) < 0 or int(row['opportunities']) < 0: raise ValueError('Negative duration/count')
        if key not in battles: battles[key]={'row':row,'seen':set()}
        b=battles[key]
        if any(b['row'][f]!=row[f] for f in FIELDS): raise ValueError('Inconsistent battle rows')
        if row['pattern'] in b['seen']: raise ValueError('Duplicate battle/pattern row')
        b['seen'].add(row['pattern'])
        p=patterns[(row['boss'],row['pattern'],row['profile'],row['dda'])]
        for dest,src in [('opportunities','opportunities'),('hits','patternHits'),('parries','parries')]: p[dest]+=int(row[src])
    if not battles: raise ValueError('No trials')
    groups=defaultdict(list)
    for b in battles.values():
        r=b['row']; groups[(r['boss'],r['profile'],r['dda'])].append(r)
    result=[]
    for (boss,profile,dda),rs in sorted(groups.items()):
        n=len(rs); wins=sum(r['outcome']=='clear' for r in rs)
        item=dict(boss=boss,profile=profile,dda=dda=='True',trials=n,clears=wins,
                  deaths=sum(r['outcome']=='death' for r in rs),timeouts=sum(r['outcome']=='timeout' for r in rs),
                  clearRate=wins/n,descriptiveWilson95=wilson(wins,n),
                  medianSeconds=statistics.median(float(r['seconds']) for r in rs),
                  medianHits=statistics.median(float(r['hits']) for r in rs))
        for field in ('longestNoDamageSeconds','stationaryFraction'):
            if all(r.get(field) for r in rs): item['median_'+field]=statistics.median(float(r[field]) for r in rs)
        result.append(item)
    paired=[]
    by_key={tuple(r[k] for k in ('profile','seed','boss','round'))+(r['dda'],):r for r in [b['row'] for b in battles.values()]}
    for k,off in by_key.items():
        if k[-1]!='False' or k[:-1]+('True',) not in by_key: continue
        on=by_key[k[:-1]+('True',)]
        paired.append(dict(profile=k[0],seed=k[1],boss=k[2],round=k[3],
                           off=off['outcome'],on=on['outcome'],secondsDelta=float(on['seconds'])-float(off['seconds'])))
    return dict(schema='tower-playtest-summary/1',engine='UnityShim',trials=len(battles),
                limitations=['Privileged state rule bots, not human or visual-learning players.',
                'Fun is not measured. Timeouts, hits and duration only flag cases for human review.',
                'Rounds share DDA history. Intervals are descriptive, not independent-sample statistical proof.',
                'Matched initial seeds do not guarantee identical later RNG events under different actions.',
                'No rendering, collision engine, device latency or Unity build verification.'],
                groups=result,pairedDda=paired,
                patterns=[dict(boss=k[0],pattern=k[1],profile=k[2],dda=k[3]=='True',**v) for k,v in sorted(patterns.items())])

def main():
    p=argparse.ArgumentParser();p.add_argument('csv',type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
    if a.output.exists(): raise SystemExit('Refusing to overwrite output')
    with a.csv.open(newline='',encoding='utf-8-sig') as f: report=summarize(csv.DictReader(f))
    a.output.mkdir(parents=True)
    (a.output/'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    lines=['# 역전의탑 규칙 봇 전투 검사','',f"실행 기록: {report['trials']}회, UnityShim (실제 Unity 화면 아님).",'',
           '|보스|실력|DDA|전투|승리|사망|시간초과|중앙 전투초|피격 중앙값|','|---|---|---|---:|---:|---:|---:|---:|---:|']
    for g in report['groups']: lines.append('|'+ '|'.join((f'{g[k]:.2f}' if isinstance(g[k],float) else str(g[k])) for k in ('boss','profile','dda','trials','clears','deaths','timeouts','medianSeconds','medianHits'))+'|')
    lines+=['','## 해석 제한']+['- '+s for s in report['limitations']]
    (a.output/'REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(f"SUMMARY_PASS trials={report['trials']} output={a.output}")
if __name__=='__main__': main()
