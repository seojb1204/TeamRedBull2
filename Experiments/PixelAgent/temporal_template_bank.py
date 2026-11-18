"""Offline RGB template bank. Temporal confirmation never authorizes input."""
import math
from copy import deepcopy
from actor_template import match_actor

class TemporalTemplateBank:
 def __init__(self, templates, max_speed=240.0):
  if not templates or not math.isfinite(max_speed) or max_speed<=0: raise ValueError('Invalid bank')
  self.templates=list(templates); self.max_speed=max_speed; self.last=None; self.streak=0; self.timestamp=None
 def observe(self,rgb,timestamp):
  try: return self._observe(rgb,timestamp)
  except Exception:
   self.last=None;self.streak=0;self.timestamp=None
   raise
 def _observe(self,rgb,timestamp):
  if not math.isfinite(timestamp) or (self.timestamp is not None and (timestamp<=self.timestamp or timestamp-self.timestamp>.5)):
   self.last=None;self.streak=0;self.timestamp=None
   raise ValueError('Nonfresh timestamp')
  candidates=[dict(match_actor(rgb,t['rgb'],t['width'],t['height']),template=t['name']) for t in self.templates]
  valid=[c for c in candidates if c['candidate_valid']]
  # Do not use continuity to rescue a visually rejected candidate.
  best=max(valid,key=lambda c:c['ncc'],default=None)
  if best is None:self.last=None;self.streak=0
  else:
   center=lambda b:(b[0]+b[2]/2,b[1]+b[3]/2)
   continuity=False
   if self.last is not None:
    x,y=center(best['box']);px,py=center(self.last['box']);dt=timestamp-self.timestamp
    continuity=math.hypot(x-px,y-py)<=6+self.max_speed*dt
   self.streak=self.streak+1 if continuity else 1
   self.last=deepcopy(best)
  self.timestamp=timestamp
  return {'candidate':deepcopy(best),'streak':self.streak,'temporally_confirmed':self.streak>=3,'action_authorized':False,'scope':'shadow only; temporal consistency is not object identity'}
