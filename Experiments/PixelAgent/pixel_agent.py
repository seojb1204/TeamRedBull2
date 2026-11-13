#!/usr/bin/env python3
"""Dependency-free experimental pixel policies. Never consume game state as observations."""
import argparse
import json
import math
import os
from pathlib import Path
import random
import time

ACTIONS = ('idle', 'left', 'right', 'attack', 'parry', 'jump', 'dash')
GRID = (10, 6)
FEATURES = 1 + GRID[0] * GRID[1] * 3 * 2
SCHEMA = 'tower-pixel-linear-q/1'


def read_ppm(path):
    """Strict, bounded P6 decoder; preserves original pixels, no hidden telemetry."""
    data = Path(path).read_bytes()
    if len(data) > 4_000_000:
        raise ValueError('Frame too large')
    i = 0
    def token():
        nonlocal i
        while i < len(data):
            if data[i] in b' \r\n\t':
                i += 1
            elif data[i] == 35:
                end = data.find(b'\n', i)
                if end < 0:
                    raise ValueError('Unterminated PPM comment')
                i = end + 1
            else:
                break
        start = i
        while i < len(data) and data[i] not in b' \r\n\t':
            i += 1
        return data[start:i]
    if token() != b'P6':
        raise ValueError('Expected P6 RGB pixels')
    w, h, maxval = int(token()), int(token()), int(token())
    if not (1 <= w <= 1024 and 1 <= h <= 1024 and maxval == 255):
        raise ValueError('Unsupported PPM dimensions/depth')
    if i >= len(data) or data[i] not in b' \r\n\t':
        raise ValueError('Missing raster delimiter')
    # P6 header consumes one whitespace (CRLF counts as one line ending).
    if data[i:i+2] == b'\r\n':
        i += 2
    else:
        i += 1
    pixels = data[i:]
    if len(pixels) != w * h * 3:
        raise ValueError('Truncated/extra PPM raster')
    return w, h, pixels


def pool_rgb(frame):
    w, h, pixels = frame
    gw, gh = GRID
    out = []
    for cy in range(gh):
        y0, y1 = cy*h//gh, max(cy*h//gh+1, (cy+1)*h//gh)
        for cx in range(gw):
            x0, x1 = cx*w//gw, max(cx*w//gw+1, (cx+1)*w//gw)
            sums = [0, 0, 0]
            for y in range(y0, min(y1, h)):
                for x in range(x0, min(x1, w)):
                    p = (y*w+x)*3
                    for c in range(3):
                        sums[c] += pixels[p+c]
            n = (min(y1,h)-y0)*(min(x1,w)-x0)*255
            out.extend(v/n for v in sums)
    return out


class PixelFeatures:
    def __init__(self):
        self.previous = None
    def reset(self):
        self.previous = None
    def observe(self, frame):
        # Interface accepts only RGB frame, not reward, step, HP or boss state.
        now = pool_rgb(frame)
        before = self.previous if self.previous is not None else now
        self.previous = now
        return [1.0] + now + [a-b for a,b in zip(now,before)]


class RandomPolicy:
    def __init__(self, seed):
        self.rng = random.Random(seed)
    def choose(self, features):
        return self.rng.randrange(len(ACTIONS))


class VisualRulePolicy:
    """Uncalibrated visual heuristic, NOT the oracle PR26 bot.

    Cyan-dominant cells approximate player; red-dominant cells approximate enemy.
    Big global visual change => parry; otherwise move toward red then attack.
    Art/UI can fool this. It is only a baseline until tested against actual frames.
    """
    def choose(self, features):
        cells = features[1:181]
        def centroid(kind):
            mass = total = 0.0
            for j in range(60):
                r,g,b = cells[j*3:j*3+3]
                weight = max(0.0, r-max(g,b)-.10) if kind == 'red' else max(0.0,min(g,b)-r-.10)
                # Ignore top HUD band; observation still consists of pixels only.
                if j//10 == 0:
                    continue
                mass += weight
                total += weight*(j%10+.5)/10
            return total/mass if mass > .08 else None
        if sum(abs(v) for v in features[181:])/180 > .12:
            return 4
        player, boss = centroid('cyan'), centroid('red')
        if player is not None and boss is not None and abs(player-boss) > .18:
            return 2 if boss > player else 1
        return 3


class LinearQ:
    """Small online semi-gradient Q learner; pixels->features->linear action values.

    No pretrained model, CNN, imitation learning, GPU or API is required.
    The environment's scalar reward is used ONLY in update(), not choose().
    """
    def __init__(self, seed=17, alpha=.1, gamma=.97):
        self.weights = [[0.0]*FEATURES for _ in ACTIONS]
        self.rng = random.Random(seed)
        self.alpha, self.gamma = alpha, gamma
        self.updates = 0
    def values(self, x):
        return [sum(a*b for a,b in zip(row,x)) for row in self.weights]
    def choose(self, x, epsilon=0.0):
        if self.rng.random() < epsilon:
            return self.rng.randrange(len(ACTIONS))
        q = self.values(x)
        # Seeded random tie-breaking avoids always-idle untrained policy.
        best = max(q)
        return self.rng.choice([i for i,v in enumerate(q) if abs(v-best)<1e-12])
    def update(self, x, action, reward, next_x, terminal):
        if not math.isfinite(reward):
            raise ValueError('Non-finite reward')
        target = reward + (0 if terminal else self.gamma*max(self.values(next_x)))
        error = target-self.values(x)[action]
        # Normalize step by feature norm; clip TD error for stability.
        scale = self.alpha*max(-10,min(10,error))/max(1,sum(v*v for v in x))
        self.weights[action] = [w+scale*v for w,v in zip(self.weights[action],x)]
        self.updates += 1
    def save(self, path):
        atomic_json(path, {'schema':SCHEMA, 'observation':'RGB pixels only, 10x6 pooled RGB and delta',
            'actions':ACTIONS, 'weights':self.weights, 'updates':self.updates,
            'alpha':self.alpha, 'gamma':self.gamma})
    @classmethod
    def load(cls, path, seed=17):
        data = json.loads(Path(path).read_text(encoding='utf-8'))
        if data.get('schema') != SCHEMA or data.get('actions') != list(ACTIONS):
            raise ValueError('Checkpoint schema/actions mismatch')
        weights = data['weights']
        if len(weights)!=7 or any(len(row)!=FEATURES for row in weights):
            raise ValueError('Checkpoint shape mismatch')
        if not all(isinstance(v,(int,float)) and math.isfinite(v) for row in weights for v in row):
            raise ValueError('Invalid checkpoint weights')
        if not (isinstance(data['alpha'],(int,float)) and 0 < data['alpha'] <= 1 and
                isinstance(data['gamma'],(int,float)) and 0 <= data['gamma'] <= 1):
            raise ValueError('Invalid checkpoint learning parameters')
        model = cls(seed, data['alpha'], data['gamma'])
        model.weights, model.updates = weights, int(data['updates'])
        return model


def atomic_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True,exist_ok=True)
    tmp = path.with_name(path.name+'.tmp')
    tmp.write_text(json.dumps(data,ensure_ascii=False,allow_nan=False),encoding='utf-8')
    os.replace(tmp,path)


class BridgeClient:
    """One locked-step episode; transition published only after raster is complete."""
    def __init__(self, root):
        self.root = Path(root).resolve()
        self.last = None
        self.episode = None
    def read(self):
        error_path = self.root/'bridge_error.json'
        if error_path.exists():
            # Game writes a failure marker when no valid action can be consumed.
            # Do not convert the same transition into another reward update.
            raise RuntimeError('Game bridge reported an error; inspect bridge_error.json')
        path = self.root/'transition.json'
        try:
            msg = json.loads(path.read_text(encoding='utf-8-sig'))
        except (FileNotFoundError,json.JSONDecodeError):
            return None
        ep, step = msg['episode'], msg['step']
        if not isinstance(ep,(str,int)) or isinstance(ep,bool) or not isinstance(step,int) or isinstance(step,bool) or step<0:
            raise ValueError('Invalid transition ID')
        if self.last is None and step != 0:
            raise ValueError('New client requires initial step 0; use a fresh episode directory')
        if self.episode is not None and ep != self.episode:
            raise ValueError('Unexpected episode switch; use a new client')
        if self.last is not None and step <= self.last:
            return None
        if self.last is not None and step != self.last+1:
            raise ValueError('Dropped transition')
        frame_name = msg.get('frame','observation.ppm')
        # Even a malicious state file cannot make us read outside bridge directory.
        if not isinstance(frame_name,str) or Path(frame_name).name!=frame_name or '/' in frame_name or '\\' in frame_name:
            raise ValueError('Unsafe frame path')
        frame_path = (self.root/frame_name).resolve()
        if frame_path.parent != self.root:
            raise ValueError('Frame escapes bridge directory')
        frame = read_ppm(frame_path)
        reward = float(msg['reward'])
        if not math.isfinite(reward):
            raise ValueError('Non-finite reward')
        if not isinstance(msg['terminated'],bool) or not isinstance(msg['truncated'],bool):
            raise ValueError('Terminal flags must be booleans')
        msg['reward'] = reward
        self.episode, self.last = ep,step
        return msg,frame
    def act(self, msg, action):
        if msg['episode']!=self.episode or msg['step']!=self.last or not isinstance(action,int) or isinstance(action,bool) or not 0<=action<len(ACTIONS):
            raise ValueError('Stale or invalid action')
        atomic_json(self.root/'action.json',{'episode':msg['episode'],'step':msg['step'],'action':action})


def run_bridge(args):
    root = Path(args.bridge)
    if not root.is_dir():
        raise ValueError('Bridge directory must exist (start isolated game first)')
    model = LinearQ.load(args.checkpoint,args.seed) if args.checkpoint and Path(args.checkpoint).exists() else LinearQ(args.seed)
    if args.policy=='learned' and not args.train and (not args.checkpoint or not Path(args.checkpoint).exists()):
        raise ValueError('Evaluation requires existing checkpoint')
    policy = RandomPolicy(args.seed) if args.policy=='random' else VisualRulePolicy() if args.policy=='rule' else model
    bridge, encoder = BridgeClient(root),PixelFeatures()
    previous = None
    start = last_seen = time.monotonic()
    count = 0
    total_reward = 0.0
    terminal = truncated = False
    while time.monotonic()-start < args.max_seconds and count<args.max_steps:
        item = bridge.read()
        if item is None:
            if time.monotonic()-last_seen>args.timeout:
                raise TimeoutError('Game bridge stopped responding; no action sent')
            time.sleep(.01)
            continue
        msg,frame = item
        last_seen = time.monotonic()
        x = encoder.observe(frame)
        terminal,truncated = msg['terminated'],msg['truncated']
        total_reward += msg['reward']
        # Episode ending by timeout is treated terminal to avoid bootstrapping across processes.
        if previous is not None and args.train:
            px,pa = previous
            model.update(px,pa,float(msg['reward']),x,terminal or truncated)
        count+=1
        if terminal or truncated:
            break
        a = policy.choose(x,args.epsilon if args.train else 0) if args.policy=='learned' else policy.choose(x)
        bridge.act(msg,a)
        previous = (x,a)
    if args.train:
        model.save(args.checkpoint)
    report = {'schema':'tower-pixel-run/1','policy':args.policy,'seed':args.seed,
              'observations':'PPM RGB pixels only; rewards only for learning/scoring',
              'training':args.train,'steps':count,'reward':total_reward,
              'terminated':terminal,'truncated':truncated,'outcome':msg.get('reason','unknown') if count else 'no_observations','wall_seconds':round(time.monotonic()-start,3),
              'updates':model.updates,'completed':terminal or truncated}
    if args.report:
        atomic_json(args.report,report)
    print(json.dumps(report,ensure_ascii=False))


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--bridge',required=True)
    p.add_argument('--policy',choices=['random','rule','learned'],default='random')
    p.add_argument('--train',action='store_true')
    p.add_argument('--checkpoint')
    p.add_argument('--report')
    p.add_argument('--seed',type=int,default=17)
    p.add_argument('--epsilon',type=float,default=.2)
    p.add_argument('--timeout',type=float,default=20)
    p.add_argument('--max-seconds',type=float,default=180)
    p.add_argument('--max-steps',type=int,default=5000)
    a = p.parse_args()
    if a.train and (a.policy!='learned' or not a.checkpoint):
        p.error('--train requires --policy learned and --checkpoint')
    if not 0<=a.epsilon<=1 or not 0<a.max_seconds<=1800 or not 0<a.max_steps<=100000 or a.timeout<=0:
        p.error('Invalid bounds (max-seconds <= 1800, max-steps <= 100000)')
    run_bridge(a)

if __name__=='__main__':
    main()
