"""Offline behavioral-cloning warm start from recorded RGB-policy demonstrations.
No game telemetry/reward is used as a feature or training label. Not proof of skill.
"""
import argparse, collections, hashlib, json
from pathlib import Path
from tabular_learning_v4 import TabularQ, _key
from visual_agent_v4 import STATE_LENGTH, save_v4
from pixel_agent import atomic_json

MAX_FILES=100
MAX_SAMPLES=100000

def train(paths, output, seed=17, evaluation_seeds=()):
    paths=list(paths)
    if not 1<=len(paths)<=MAX_FILES: raise ValueError('Expected 1..100 demonstration files')
    output=Path(output)
    if output.exists(): raise ValueError('Refusing to overwrite checkpoint')
    counts=collections.defaultdict(lambda:[0]*12); hashes=set(); sources=[]; total=0
    heldout=set(evaluation_seeds)
    for path in paths:
        path=Path(path)
        if path.stat().st_size>16000000: raise ValueError('Demonstration too large')
        raw=path.read_bytes();digest=hashlib.sha256(raw).hexdigest()
        if digest in hashes: raise ValueError('Duplicate demonstration')
        hashes.add(digest);data=json.loads(raw)
        if data.get('schema')!='tower-pixel-v4-trajectory/1' or data.get('policy')!='rule-v4':
            raise ValueError('Requires recorded rule-v4 pixel demonstration')
        if data.get('completed') is not True or data.get('training') is not False:
            raise ValueError('Demonstration must be completed and evaluation-only')
        source_seed=data.get('seed')
        if isinstance(source_seed,bool) or not isinstance(source_seed,int): raise ValueError('Missing seed')
        if source_seed in heldout: raise ValueError('Training/evaluation seed overlap')
        transitions=data.get('transitions')
        if not isinstance(transitions,list) or not 1<=len(transitions)<=5000: raise ValueError('Invalid transition count')
        for i,t in enumerate(transitions):
            state=t.get('state');action=t.get('action')
            if not isinstance(state,list) or len(state)!=STATE_LENGTH: raise ValueError('Invalid pixel state')
            key=_key(tuple(state))
            if isinstance(action,bool) or not isinstance(action,int) or not 0<=action<12: raise ValueError('Invalid action')
            ended=t.get('terminated') is True or t.get('truncated') is True
            if ended!=(i==len(transitions)-1): raise ValueError('Invalid episode boundary')
            counts[key][action]+=1;total+=1
            if total>MAX_SAMPLES: raise ValueError('Dataset cap exceeded')
        sources.append({'name':path.name,'sha256':digest,'seed':source_seed,'samples':len(transitions)})
    model=TabularQ(seed=seed)
    # Frequencies encode an initial action preference, not measured future returns.
    model.q={key:[n/sum(row) for n in row] for key,row in counts.items()}
    save_v4(model,output)
    metadata=json.loads(output.read_text())
    metadata['initialization']={'method':'behavior-cloning-frequency/1','observation':'V4 RGB bins + own action history',
        'label':'submitted rule-v4 action, not optimal/human action','samples':total,'states':len(counts),
        'sources':sources,'reserved_evaluation_seeds':sorted(heldout),
        'limits':'Imitates visual rule behavior; unseen states use seeded random ties. Not combat success or generalization.'}
    atomic_json(output,metadata)
    return metadata['initialization']

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('demonstrations',nargs='+');p.add_argument('--output',required=True)
    p.add_argument('--seed',type=int,default=17);p.add_argument('--evaluation-seeds',type=int,nargs='+',required=True)
    a=p.parse_args();print(json.dumps(train(a.demonstrations,a.output,a.seed,a.evaluation_seeds),indent=2))
