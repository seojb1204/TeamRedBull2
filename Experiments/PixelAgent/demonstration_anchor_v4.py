"""Soft demonstration margin for a bounded assisted-RL experiment, not an oracle."""
import hashlib,json,math
from pathlib import Path
from visual_agent_v4 import load_v4

class DemonstrationAnchor:
    def __init__(self,path,seed,rate=.2,margin=.05):
        self.rate,self.margin=rate,margin
        if not 0<rate<=1 or not 0<=margin<=1:raise ValueError('Invalid anchor strength')
        path=Path(path);source=load_v4(path,seed)
        info=getattr(source,'initialization',None)
        if not info or info.get('method')!='behavior-cloning-frequency/1':raise ValueError('Anchor requires recorded demonstration initialization')
        if source.online_updates or source.replay_updates:raise ValueError('Anchor must be original unmodified imitation model')
        self.actions={}
        for key,values in source.q.items():
            if any(not math.isfinite(v) or v<0 or v>1 for v in values):raise ValueError('Anchor must contain frequencies')
            if abs(sum(values)-1)>1e-6:raise ValueError('Anchor row must sum to one')
            if max(values)>=.8:self.actions[key]=max(range(12),key=lambda i:values[i])
        if not self.actions:raise ValueError('No dominant demonstration actions')
        self.metadata={'method':'soft-demonstration-margin/1','sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'states':len(self.actions),'rate':rate,'margin':margin,'dominant_action_frequency':.8,
            'limitation':'Assisted RL retains learned teacher actions; not unaided discovery or live rule fallback'}
        self.updates=0
    def apply(self,model):
        for key,action in self.actions.items():
            if key not in model.q:continue
            row=model.q[key];target=max(v for i,v in enumerate(row) if i!=action)+self.margin
            if row[action]<target:
                row[action]+=self.rate*(target-row[action]);self.updates+=1
