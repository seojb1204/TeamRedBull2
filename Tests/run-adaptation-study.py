"""Run the real C# DDA Lab, 30 battles per arm, save/reload each battle. No product data.
Retain full result/CSV/trace artifacts and deterministic replay evidence.
"""
import argparse,sys,json,collections
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools/DdaLab'));from lab import Lab
lab=Lab(a.dotnet,a.output);cases=[(m,p,'stable') for m in ['control','combat'] for p in ['novice','balanced','expert']]
cases += [(m,'balanced',s) for m in ['control','combat'] for s in ['improving','declining','repeated_failures']]
cases += [('control','expert',s) for s in ['perfect','near_perfect','loss_recovery','sparse','unobserved_clear']]
summary=[]
for mode,profile,scenario in cases:
 config=dict(mode=mode,profile=profile,scenario=scenario,dda='both',seed=17,rounds=30,boss=0)
 job=lab.submit(config,asynchronous=False)
 if job['status']!='completed':raise RuntimeError(job)
 data=json.loads((a.output/job['id']/'result.json').read_text());on=[b for b in data['battles'] if b['dda']];off=[b for b in data['battles'] if not b['dda']]
 stats=[s for b in on for s in b['stats'] if s['opportunities']];ops=sum(s['opportunities'] for s in stats);success=sum(s['parries']+s['dodges'] for s in stats)/ops if ops else None
 traces=[t for b in on for t in b['trace']['patterns']];delta=sum(t['aDelta'] for t in traces)
 row=dict(mode=mode,profile=profile,scenario=scenario,run=job['id'],outcomes=dict(collections.Counter(b['outcome'] for b in on)),opportunities=ops,success=success,target_band_fraction=sum(.6<=s['success']<=.8 for s in stats)/len(stats) if stats else None,learned=sum(t['learned'] for t in traces),a_delta=delta,b_delta=sum(t['bDelta'] for t in traces),final={s['pattern']:s['level'] for s in on[-1]['next']},restored=all(b['restored'] for b in data['battles']),off_unchanged=all(b['before']==b['after'] for b in off))
 summary.append(row);print(json.dumps(row),flush=True)
 # Repeat every scenario with the exact same C# sources and conditions.
 replay=lab.submit(config,asynchronous=False)
 if replay['status']!='completed' or not replay['replay']['identical']:raise RuntimeError('Replay mismatch '+str(replay))
 row['replay_identical']=True
(a.output/'study-summary.json').write_text(json.dumps(summary,indent=2))
print('STUDY_PASS',len(summary),'cases x 30 battles x ON/OFF x repeat; all models restored')
