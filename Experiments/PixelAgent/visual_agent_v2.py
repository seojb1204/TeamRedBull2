"""Pixel tracker + bounded tabular RL. Not an oracle or human-level agent."""
import argparse,json,time
from pathlib import Path
from pixel_agent import BridgeClient,RandomPolicy,atomic_json,ACTIONS
from visual_tracker import Tracker,choose_action
from tabular_learning import TabularQ

def run(args):
    model=TabularQ.load(args.checkpoint,seed=args.seed) if args.checkpoint and Path(args.checkpoint).exists() else TabularQ(seed=args.seed)
    if args.policy=='tabular' and not args.train and not (args.checkpoint and Path(args.checkpoint).is_file()):
        raise ValueError('Evaluation requires checkpoint')
    client=BridgeClient(args.bridge);tracker=Tracker();random=RandomPolicy(args.seed)
    start=seen=time.monotonic();count=0;total=0.;previous=None;trajectory=[]
    actions=[0]*7;missing=0;warning=0;last_move=0;parry_at=-100
    terminal=truncated=False;final={};state_counts={};q_start={}
    while time.monotonic()-start<args.max_seconds and count<args.max_steps:
        item=client.read()
        if item is None:
            if time.monotonic()-seen>args.timeout:raise TimeoutError('Game bridge unresponsive')
            time.sleep(.01);continue
        msg,frame=item;seen=time.monotonic()
        visual,diag=tracker.observe(frame)
        # All visual fields derive from RGB; movement and cooldown are own input history.
        state=tuple(visual)+(last_move,int(count-parry_at<8))
        key=','.join(map(str,state));state_counts[key]=state_counts.get(key,0)+1
        missing+=int(not visual[0] or not visual[1]);warning+=int(bool(visual[3]))
        terminal,truncated=msg['terminated'],msg['truncated'];total+=msg['reward']
        if previous is not None and args.train:
            old,action=previous;transition=(old,action,msg['reward'],state,terminal or truncated)
            model.update(*transition);trajectory.append(transition)
        if args.report and (count%120==0 or terminal or truncated):
            samples=Path(args.report).with_suffix('.frames');samples.mkdir(exist_ok=True)
            if len(list(samples.glob('*.ppm')))<32:
                w,h,rgb=frame;(samples/f'{count:05d}.ppm').write_bytes(f'P6\n{w} {h}\n255\n'.encode()+rgb)
                atomic_json(samples/f'{count:05d}.json',{'state':state,'visual':diag})
        count+=1;final=msg
        if terminal or truncated:break
        if args.policy=='tabular':action=model.choose(state,epsilon=args.epsilon if args.train else 0)
        elif args.policy=='rule-v2':action=choose_action(visual,diag)
        else:action=random.choose(state)
        client.act(msg,action);actions[action]+=1;previous=(state,action)
        if action in (1,2):last_move=-1 if action==1 else 1
        if action==4:parry_at=count
    if args.train:
        if not (terminal or truncated):raise RuntimeError('Incomplete episode: do not replay/save as completed')
        model.replay(trajectory,sweeps=8);model.save(args.checkpoint)
    report={'schema':'tower-pixel-v2/1','policy':args.policy,'seed':args.seed,'training':args.train,
        'observation':'raw RGB tracker + own action history only; reward used only in learner update',
        'steps':count,'reward':total,'terminated':terminal,'truncated':truncated,'outcome':final.get('reason','unknown'),
        'wall_seconds':round(time.monotonic()-start,3),'actions':dict(zip(ACTIONS,actions)),
        'missing_actor_observations':missing,'missing_actor_fraction':missing/max(1,count),'warning_observations':warning,
        'state_visits':state_counts,'online_updates':model.online_updates,'replay_updates':model.replay_updates,
        'evaluation_epsilon':None if args.train else 0,'completed':terminal or truncated}
    if args.report:atomic_json(args.report,report)
    print(json.dumps(report,ensure_ascii=True))

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--bridge',required=True);p.add_argument('--policy',choices=['random-v2','rule-v2','tabular'],required=True)
    p.add_argument('--checkpoint');p.add_argument('--report');p.add_argument('--train',action='store_true')
    p.add_argument('--seed',type=int,default=17);p.add_argument('--epsilon',type=float,default=.4)
    p.add_argument('--max-seconds',type=float,default=180);p.add_argument('--timeout',type=float,default=20)
    p.add_argument('--max-steps',type=int,default=5000)
    a=p.parse_args()
    if a.train and (a.policy!='tabular' or not a.checkpoint):p.error('Training requires tabular policy and checkpoint')
    if not 0<=a.epsilon<=1 or not 0<a.max_seconds<=1800 or not 0<a.max_steps<=5000:p.error('Invalid bounded settings')
    run(a)
if __name__=='__main__':main()
