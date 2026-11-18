"""Conservative RGB template recognizer, offline until templates are calibrated.
No OCR model, screenshots, native input or internal gameplay state are accessed.
Scores are pixel distances, NOT calibrated probabilities.
"""
from dataclasses import dataclass

BLOCKERS=frozenset({'blocked_cards','blocked_help','blocked_settings','blocked_history','blocked_transition','blocked_title'})
import hashlib
import math

@dataclass(frozen=True)
class Patch:
    x: int
    y: int
    width: int
    height: int
    rgb: bytes

@dataclass(frozen=True)
class ScreenTemplate:
    screen: str
    anchors: tuple
    enter_prompt: Patch = None

@dataclass(frozen=True)
class Recognition:
    screen: str
    verified: bool
    enter_verified: bool
    reason: str
    distance: float = 1.0
    margin: float = 0.0


def validate_patch(width, height, patch):
    if any(type(v) is not int for v in (patch.x,patch.y,patch.width,patch.height)):
        raise ValueError('Integer patch geometry required')
    if type(patch.rgb) is not bytes or patch.width<=0 or patch.height<=0 or len(patch.rgb)!=patch.width*patch.height*3:
        raise ValueError('Malformed patch')
    if patch.x<0 or patch.y<0 or patch.x+patch.width>width or patch.y+patch.height>height:
        raise ValueError('Patch outside capture')

def patch_distance(rgb, width, height, patch):
    validate_patch(width,height,patch)
    total=0;offset=0
    for y in range(patch.y,patch.y+patch.height):
        start=(y*width+patch.x)*3
        for a,b in zip(rgb[start:start+patch.width*3],patch.rgb[offset:offset+patch.width*3]):
            total+=abs(a-b)
        offset+=patch.width*3
    return total/(len(patch.rgb)*255)

class TemplateRecognizer:
    """Every supported screen needs >=2 UI anchors, not character/background art.
    Explicit pause templates are needed; unseen overlays remain a limitation.
    Reject rather than resize or silently reuse templates after a build change.
    """
    def __init__(self, *, width, height, build_sha256, templates, max_distance=.025, min_margin=.02):
        if type(width) is not int or type(height) is not int or width<=0 or height<=0:
            raise ValueError('Positive integer viewport required')
        if width>8192 or height>8192 or width*height>16777216:
            raise ValueError('Viewport exceeds supported capture budget')
        if type(build_sha256) is not str or len(build_sha256)!=64 or any(c not in '0123456789abcdef' for c in build_sha256):
            raise ValueError('Exact lowercase build SHA256 required')
        if not all(math.isfinite(x) for x in (max_distance,min_margin)) or not 0<=max_distance<.2 or not 0<min_margin<1:
            raise ValueError('Strict finite distance/margin thresholds required')
        if len(templates)<2:raise ValueError('At least two competing screen labels required')
        self.templates=tuple(templates);self.width=width;self.height=height;self.build=build_sha256
        self.threshold=max_distance;self.margin=min_margin
        labels=set()
        for t in self.templates:
            if t.screen not in ({'lobby','battle','result','pause'}|BLOCKERS) or t.screen in labels or len(t.anchors)<2:
                raise ValueError('Unique known screen with two anchors required')
            labels.add(t.screen)
            for i,a in enumerate(t.anchors):
                for b in t.anchors[i+1:]:
                    if max(a.x,b.x)<min(a.x+a.width,b.x+b.width) and max(a.y,b.y)<min(a.y+a.height,b.y+b.height):
                        raise ValueError('Screen anchors must not overlap')
            for p in t.anchors+((t.enter_prompt,) if t.enter_prompt else ()):
                validate_patch(width,height,p)
        # This checksum identifies templates, not their correctness or provenance.
        self.template_sha256=hashlib.sha256(repr((width,height,build_sha256,self.templates)).encode()).hexdigest()

    def recognize(self,rgb,*,width,height,build_sha256,full_frame):
        if full_frame is not True:return Recognition('unknown',False,False,'HUD/full-frame capture required')
        if (width,height,build_sha256)!=(self.width,self.height,self.build):
            return Recognition('unknown',False,False,'build_or_viewport_changed')
        if type(rgb) is not bytes or len(rgb)!=width*height*3:
            return Recognition('unknown',False,False,'invalid_rgb')
        scored=[]
        for t in self.templates:
            # Both anchors must agree: average would mask a wrong/missing anchor.
            score=max(patch_distance(rgb,width,height,p) for p in t.anchors)
            scored.append((score,t.screen,t))
        # A visible known modal vetoes even a stronger background lobby match.
        blockers=[entry for entry in scored if entry[1] in BLOCKERS and entry[0]<=self.threshold]
        if blockers:
            score,label,_=min(blockers,key=lambda x:(x[0],x[1]))
            return Recognition('unknown',False,False,'blocking_overlay:'+label,score,0.0)
        scored.sort(key=lambda x:(x[0],x[1]))
        score,_,best=scored[0];margin=scored[1][0]-score
        if score>self.threshold:return Recognition('unknown',False,False,'anchor_mismatch',score,margin)
        if margin<self.margin:return Recognition('unknown',False,False,'ambiguous_screen',score,margin)
        if best.screen in BLOCKERS:return Recognition('unknown',False,False,'blocking_overlay:'+best.screen,score,margin)
        enter=best.screen in {'lobby','result'} and best.enter_prompt is not None and patch_distance(rgb,width,height,best.enter_prompt)<=self.threshold
        return Recognition(best.screen,True,enter,'matched',score,margin)
