"""Export actual Lab results; plotting only, no algorithm/policy reimplementation."""
import argparse,json,csv,gzip,hashlib
from pathlib import Path
import matplotlib;matplotlib.use('Agg')
import matplotlib.pyplot as plt
p=argparse.ArgumentParser();p.add_argument('--study',type=Path,required=True);p.add_argument('--before',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
summary=json.loads((a.study/'study-summary.json').read_text());rows=[];manifests=[];datasets=[]
for case in summary:
 f=a.study/case['run']/'result.json';d=json.loads(f.read_text());datasets.append(d);manifests.append(dict(config=d['config'],manifest=d['manifest'],result_sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
 for b in d['battles']:
  nxt={x['pattern']:x for x in b['next']}
  traces={x['pattern']:x for x in b['trace']['patterns']}
  for s in b['stats']:
   t=traces[s['pattern']];rows.append(dict(mode=d['config']['mode'],scenario=d['config']['scenario'],input_profile=d['config']['profile'],profile=b['profile'],dda=b['dda'],round=b['round'],outcome=b['outcome'],restored=b['restored'],**s,next_level=nxt[s['pattern']]['level'],learned=t['learned'],a_delta=t['aDelta'],b_delta=t['bDelta'],reason=t['reason'],next_reason=t['nextReason'],skip=t['skipReason'],scores_used=t['candidateScoresUsed'],candidates=json.dumps(t['candidates']),scores=json.dumps(t['candidateScores']),context=json.dumps(t['context']),A_after=t['aAfter'],b_after=t['bAfter']))
with gzip.open(a.output/'DDA_ADAPTATION_20261118_BATTLES.csv.gz','wt',encoding='utf-8',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
(a.output/'DDA_ADAPTATION_20261118_RESULTS.json').write_text(json.dumps(dict(summary=summary,manifests=manifests),indent=2))
fig,axes=plt.subplots(3,2,figsize=(14,11),sharex=True)
for i,profile in enumerate(['novice','balanced','expert']):
 for j,mode in enumerate(['control','combat']):
  d=next(d for d in datasets if d['config']['mode']==mode and d['config']['profile']==profile and d['config']['scenario']=='stable');ax=axes[i,j]
  for dda,color in [(False,'#8592a3'),(True,'#2279bd')]:
   bs=[b for b in d['battles'] if b['dda']];xs=[b['round'] for b in bs];levels=[next(s['level'] for s in b['stats'] if s['pattern']=='cleave') for b in bs];ax.plot(xs,levels,label='DDA '+('ON' if dda else 'OFF'),color=color)
  ax.set(title=mode+' / '+profile+' / cleave applied level',ylim=(-2.2,2.2),yticks=[-2,-1,0,1,2]);ax.grid(alpha=.2);ax.legend(loc='lower right')
fig.supxlabel('Battle (isolated save/reload every round)');fig.tight_layout();fig.savefig(a.output/'DDA_ADAPTATION_20261118_LEVELS.png',dpi=150);plt.close(fig)
fig,axes=plt.subplots(2,2,figsize=(13,8))
for ax,scenario in zip(axes.flat,['improving','declining','loss_recovery','unobserved_clear']):
 d=next(d for d in datasets if d['config']['mode']=='control' and d['config']['scenario']==scenario);bs=[b for b in d['battles'] if b['dda']]
 for pattern in ['cleave','shield','throw']:
  ax.plot([b['round'] for b in bs],[next(s['level'] for s in b['stats'] if s['pattern']==pattern) for b in bs],label=pattern)
 ax.set(title='CONTROLLED '+scenario,ylim=(-2.2,2.2),yticks=[-2,-1,0,1,2]);ax.legend();ax.grid(alpha=.2)
fig.supxlabel('Battle; synthetic outcomes, not measured human adaptation');fig.tight_layout();fig.savefig(a.output/'DDA_ADAPTATION_20261118_SCENARIOS.png',dpi=150);plt.close(fig)
comparisons=[]
if a.before:
 for f in a.before.glob('*/result.json'):
  before=json.loads(f.read_text());after=next((d for d in datasets if d['config']==before['config']),None)
  if after is None:continue
  diffs=[]
  for old,new in zip(before['battles'],after['battles']):
   for x,y in zip(old['stats'],new['stats']):
    if x!=y:diffs.append(dict(dda=new['dda'],round=new['round'],pattern=y['pattern'],before=x,after=y))
  comparisons.append(dict(config=before['config'],battle_stats_differences=len(diffs),first_differences=diffs[:5],limits='Exact stats comparison; reason wording/diagnostics intentionally differ; not human evidence'))
(a.output/'DDA_ADAPTATION_20261118_BEFORE_AFTER.json').write_text(json.dumps(comparisons,indent=2));print('EXPORT',len(rows),'rows',len(comparisons),'baseline comparisons')
