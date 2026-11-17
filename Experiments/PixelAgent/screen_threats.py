"""Screen-only shadow features. No internal labels, game state, files or OS input.
Rim dynamics is not an attack-state classifier. Small cyan tracks are candidates,
not verified projectiles. ETA is horizontal screen crossing, NOT a collision time.
"""
import math
from visual_tracker import Tracker,_components
class ScreenThreats:
 def __init__(self):
  self.tracker=Tracker();self.last_t=None;self.last_count=0;self.small=[]
 def observe(self,width,height,rgb,stamp):
  if (width,height)!=(640,480) or type(rgb) is not bytes or len(rgb)!=width*height*3:raise ValueError('Uncalibrated or malformed frame')
  if type(stamp) not in (int,float) or not math.isfinite(stamp):raise ValueError('Invalid timestamp')
  if self.last_t is not None and (stamp<=self.last_t or stamp-self.last_t>.5):
   self.__init__();raise ValueError('Nonmonotonic or stale sequence; reset')
  dt=None if self.last_t is None else stamp-self.last_t
  state,diag=self.tracker.observe((width,height,rgb));points=[]
  for y in range(220,370):
   for x in range(width):
    k=(y*width+x)*3;r,g,b=rgb[k:k+3]
    if b>=180 and g>=100 and b-r>=50 and g-r>=20:points.append((x,y))
  comps=[]
  for pts in _components(points):
   if len(pts)<6:continue
   xs=[x for x,y in pts];ys=[y for x,y in pts]
   comps.append({'x':sum(xs)/len(xs),'y':sum(ys)/len(ys),'w':max(xs)-min(xs)+1,'h':max(ys)-min(ys)+1,'n':len(pts)})
  body=[c for c in comps if c['h']>=35];small=[c for c in comps if 3<=c['w']<=36 and 3<=c['h']<=32 and c['n']>=8]
  # Exclude near a tall bright component; missing/fragmented body still a limitation.
  small=[c for c in small if not any(abs(c['x']-b['x'])<b['w']/2+30 and abs(c['y']-b['y'])<b['h']/2+25 for b in body)]
  px=diag['player_x']*4 if diag['player_raw_detected'] and diag['player_confidence']>=.7 else None
  tracks=[];used=set()
  if dt:
   for c in small:
    candidates=[(math.hypot(c['x']-o['x'],c['y']-o['y']),i,o) for i,o in enumerate(self.small) if i not in used and abs(c['n']-o['n'])<=max(c['n'],o['n'])*.7]
    if not candidates:continue
    dist,i,old=min(candidates)
    if dist>max(12,400*dt):continue
    used.add(i);vx=(c['x']-old['x'])/dt;eta=None
    if px is not None and abs(vx)>=10 and (px-c['x'])*vx>0:
     candidate=(px-c['x'])/vx
     if 0<candidate<=2:eta=candidate
    tracks.append({**c,'vx_px_s':vx,'player_x_estimate':px,'horizontal_crossing_eta_s':eta,'verified_projectile':False})
  count=len(points);phase='absent' if count<20 else ('onset' if self.last_count<20 else 'fading' if count<self.last_count*.65 else 'brightening' if count>self.last_count*1.35 else 'present')
  self.last_t=stamp;self.last_count=count;self.small=small
  return {'rim_dynamics':phase,'rim_pixels':count,'distance_px_estimate':diag['distance_px']*4 if diag['distance_px'] is not None else None,'player_raw':diag['player_raw_detected'],'boss_raw':diag['boss_raw_detected'],'small_cyan_tracks':tracks,'action_authorized':False,'limitations':['actor detector not independently calibrated','rim dynamics != attack phase','cyan FX may masquerade as projectile','screen crossing != hit prediction']}
