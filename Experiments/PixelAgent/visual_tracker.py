"""Pixel-only Arakus visual baseline. Heuristic detections, not ground-truth state."""
import math


def _components(points):
    points=set(points);groups=[]
    while points:
        first=points.pop();stack=[first];group=[first]
        while stack:
            x,y=stack.pop()
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    p=(x+dx,y+dy)
                    if p in points:
                        points.remove(p);stack.append(p);group.append(p)
        groups.append(group)
    return groups


class Tracker:
    """Inputs only (width,height,RGB bytes), stores at most 3 missing observations.

    Tuned on the current rendered art at 160x90. Different art/resolution layouts,
    white hit flashes, overlapping actors and effect particles can cause mistakes.
    Returned confidence is a heuristic score, NOT calibrated probability.
    """
    def __init__(self):
        self.last_player=None;self.last_boss=None
        self.player_age=0;self.boss_age=0

    def reset(self):
        self.__init__()

    def observe(self,frame):
        w,h,pixels=frame
        if w<20 or h<20 or len(pixels)!=w*h*3:
            raise ValueError('Invalid RGB frame')
        sx,sy=w/160,h/90
        heads=[];gold=[];cyan=[]
        # Exclude both HUD bars/text and the floor/UI. No state/position API is read.
        for y in range(round(35*sy),round(72*sy)):
            for x in range(w):
                k=(y*w+x)*3;r,g,b=pixels[k:k+3]
                if min(r,g,b)>150 and max(r,g,b)-min(r,g,b)<30:heads.append((x,y))
                if r>b+25 and g>b+12 and g>60 and r<2*g:gold.append((x,y))
                if b>r+8 and g>r+5 and b>60:cyan.append((x,y))
        # Find dense gold sprite group; using raw pixels avoids averaging into red scenery.
        radius=max(3,round(6*sx));best=[];score=-1
        for center in sorted(set(x for x,y in gold)):
            cluster=[(x,y) for x,y in gold if abs(x-center)<=radius]
            value=len(cluster)
            # Previous location is a weak tie-breaker, never a fabricated observation.
            if self.last_boss is not None:value-=min(abs(center-self.last_boss[0])/sx,40)*.02
            if value>score:score=value;best=cluster
        boss=None;boss_conf=0.0
        if len(best)>=max(5,round(14*sx*sy)):
            xs=sorted(x for x,y in best);ys=sorted(y for x,y in best)
            boss=(xs[len(xs)//2],ys[len(ys)//2])
            boss_conf=min(1,len(best)/(40*sx*sy))
        candidates=[]
        for group in _components(heads):
            if not 1<=len(group)<=max(8,round(8*sx*sy)):continue
            x=sum(p[0] for p in group)/len(group);y=sum(p[1] for p in group)/len(group)
            # Bright armor overlaps the gold boss. Prefer separate player head candidates.
            overlap=boss is not None and abs(x-boss[0])<5*sx
            if overlap:continue
            confidence=min(1,.5+.12*len(group))
            cost=abs(y/sy-64)*.12-confidence
            if self.last_player is not None:
                distance=abs(x-self.last_player[0])/sx
                # Actual decisions are 1/15 sec; large jumps are usually a flash/particle.
                if distance>22 and self.player_age<3:continue
                cost+=distance*.08
            candidates.append((cost,(x,y),confidence))
        player=None;player_conf=0.0
        if candidates:
            _,player,player_conf=min(candidates,key=lambda v:v[0])
        player_raw=player is not None;boss_raw=boss is not None
        if player_raw:self.last_player=player;self.player_age=0
        else:
            self.player_age+=1
            if self.player_age<=3 and self.last_player is not None:
                player=self.last_player;player_conf=.3/(self.player_age+1)
            else:self.last_player=None
        if boss_raw:self.last_boss=boss;self.boss_age=0
        else:
            self.boss_age+=1
            if self.boss_age<=3 and self.last_boss is not None:
                boss=self.last_boss;boss_conf=.3/(self.boss_age+1)
            else:self.last_boss=None
        # Cyan belongs to the boss's telegraph rim, not the player.
        warning_count=sum(1 for x,y in cyan if boss is not None and abs(x-boss[0])<=12*sx)
        warning=warning_count>=max(3,round(4*sx*sy))
        dx=(boss[0]-player[0])/sx if player is not None and boss is not None else None
        bucket=0 if dx is None else (1 if dx>=0 else -1)*(1 if abs(dx)<=14 else 2 if abs(dx)<=32 else 3)
        state=(int(player is not None),int(boss is not None),bucket,int(warning))
        diag={'player_x':player[0]/sx if player else None,'player_y':player[1]/sy if player else None,
              'boss_x':boss[0]/sx if boss else None,'boss_y':boss[1]/sy if boss else None,
              'distance_px':dx,'player_confidence':player_conf,'boss_confidence':boss_conf,
              'player_raw_detected':player_raw,'boss_raw_detected':boss_raw,
              'player_stale_steps':self.player_age,'boss_stale_steps':self.boss_age,
              'cyan_pixels':warning_count,'warning':warning,
              'detection_failed':player is None or boss is None,
              'limitation':'heuristic raw pixels; not ground truth or calibrated confidence'}
        return state,diag


def choose_action(state,diag):
    """0 idle / 1 left / 2 right / 3 attack / 4 parry. No attack fallback."""
    if not state[0] or not state[1] or diag['detection_failed']:return 0
    dx=diag['distance_px']
    if diag['warning'] and abs(dx)<=32:return 4
    if abs(dx)>14:return 2 if dx>0 else 1
    return 3
