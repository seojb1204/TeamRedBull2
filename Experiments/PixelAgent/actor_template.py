"""RGB template shadow matcher. NumPy optional experiment dependency.
Template is visual evidence, not an internal position or a learned detector.
Only640x480; correlation scores are not calibrated probabilities.
"""
def match_actor(rgb,template_rgb,tw,th):
 import numpy as np
 if type(rgb) is not bytes or len(rgb)!=640*480*3 or type(template_rgb) is not bytes or len(template_rgb)!=tw*th*3:raise ValueError('Malformed RGB')
 if not 8<=tw<=96 or not 8<=th<=96:raise ValueError('Invalid template')
 def pool(a):
  h,w=a.shape;return a[:h//2*2,:w//2*2].reshape(h//2,2,w//2,2).mean((1,3))
 a=pool(np.frombuffer(rgb,dtype=np.uint8).reshape(480,640,3).astype(np.float32).mean(2))
 t=pool(np.frombuffer(template_rgb,dtype=np.uint8).reshape(th,tw,3).astype(np.float32).mean(2));t=t-t.mean();norm=float((t*t).sum())**.5
 if norm<1:raise ValueError('Blank template')
 roi=a[130:185];h,w=t.shape
 windows=np.lib.stride_tricks.sliding_window_view(roi,(h,w));sums=windows.sum(axis=(-1,-2));sq=(windows*windows).sum(axis=(-1,-2));var=np.maximum(sq-sums*sums/(h*w),0)
 scores=np.einsum('ijab,ab->ij',windows,t,optimize=True)/np.maximum(np.sqrt(var)*norm,1e-6)
 y,x=np.unravel_index(scores.argmax(),scores.shape);best=float(scores[y,x]);other=scores.copy();other[max(0,y-h):y+h+1,max(0,x-w):x+w+1]=-1;second=float(other.max())
 return {'box':[int(x*2),int((130+y)*2),tw,th],'ncc':best,'margin':best-second,'candidate_valid':best>=.65 and best-second>=.08,'action_authorized':False,'limitation':'single pose; no occlusion/flash/scale calibration; score not probability'}
