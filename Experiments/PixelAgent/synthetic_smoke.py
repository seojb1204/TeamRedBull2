#!/usr/bin/env python3
"""ARTIFICIAL color cue bandit, not Arakus, Unity, combat or DDA validation."""
import argparse
import json
import random
from pixel_agent import LinearQ, PixelFeatures, RandomPolicy, atomic_json


def observation(context):
    colors=((255,30,30),(30,255,30),(30,30,255))
    return 20,12,bytes(colors[context])*240


def experiment(seed, train_steps=1200, eval_steps=420):
    env=random.Random(seed)
    model=LinearQ(seed,alpha=.3,gamma=0)
    targets=(1,2,4)
    def features(context):return PixelFeatures().observe(observation(context))
    for i in range(train_steps):
        ctx=env.randrange(3);x=features(ctx)
        a=model.choose(x,epsilon=.3)
        reward=1.0 if a==targets[ctx] else -.2
        model.update(x,a,reward,x,True)
    baseline=RandomPolicy(seed+1000)
    # Fixed, balanced held-out context schedule; training disabled.
    learned_wins=random_wins=0
    for i in range(eval_steps):
        ctx=i%3;x=features(ctx)
        learned_wins+=model.choose(x)==targets[ctx]
        random_wins+=baseline.choose(x)==targets[ctx]
    return {'seed':seed,'train_steps':train_steps,'evaluation_steps':eval_steps,
            'random_success_rate':random_wins/eval_steps,'learned_success_rate':learned_wins/eval_steps,
            'training_updates':model.updates}


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--report');args=p.parse_args()
    result={'environment':'synthetic RGB color-cue bandit ONLY; NOT the game',
            'purpose':'Check pixel feature/online update/evaluation plumbing, not gaming skill',
            'held_out':'No learning in evaluation; same 3 cue classes, new sequence, not new graphics',
            'runs':[experiment(s) for s in (17,29,43)]}
    if args.report:atomic_json(args.report,result)
    print(json.dumps(result,indent=2))

if __name__=='__main__':main()
