"""V4 pixel-only experiment; V2 is preserved and checkpoints are not interchangeable.

The only shaping is +0.001 per actual transition during training, cancelling the
environment's per-decision cost. Raw/shaped returns are diagnostic only;
actual damage, parries, survival time and outcome are the primary metrics.
Rule-V4 is explicitly hand-authored, not a learned policy.
"""
import argparse
import json
import math
import time
from pathlib import Path
from pixel_agent import BridgeClient as LegacyBridge, atomic_json
import random as random_module
from visual_tracker import Tracker
from tabular_learning_v4 import TabularQ, MAX_TRANSITIONS, ACTIONS

EXPERIMENT = "tower-pixel-v4/1"
STATE_LENGTH = 10


class BridgeClient(LegacyBridge):
    def read(self):
        item=super().read()
        if item and item[0].get("actionSet")!="native12":
            raise ValueError("V4 requires explicit native12 bridge; do not silently compare to seven actions")
        return item
    def act(self,msg,action):
        if msg["episode"]!=self.episode or msg["step"]!=self.last or isinstance(action,bool) or not isinstance(action,int) or not 0<=action<len(ACTIONS):
            raise ValueError("Stale or invalid V4 action")
        atomic_json(self.root/"action.json",{"episode":msg["episode"],"step":msg["step"],"action":action})

class RandomPolicy:
    def __init__(self,seed):self.rng=random_module.Random(seed)
    def choose(self,state):return self.rng.randrange(len(ACTIONS))

class DecisionMemory:
    """Submitted-input history, NOT actual HP, cooldowns, facing, or successful actions."""
    def __init__(self):
        self.tick=0;self.last_move=0;self.parry_at=-100;self.warning_age=0
        self.busy_until=0;self.last={};self.heal_attempts=0
    @property
    def parry_age(self):return self.tick-self.parry_at
    def age(self,action):return self.tick-self.last.get(action,-1000)
    def observe(self,visual):
        self.warning_age=self.warning_age+1 if visual[3] else 0
        return tuple(visual)+(self.last_move,int(self.parry_age<9),min(self.warning_age,20),
            int(self.tick<self.busy_until),min(self.heal_attempts,3),int(self.age(7)<100))
    def acted(self,action):
        if action in (1,2):self.last_move=-1 if action==1 else 1
        if action==4:self.parry_at=self.tick
        if action in (3,7,8,9,10,11):
            self.busy_until=self.tick+{3:6,7:11,8:14,9:20,10:13,11:14}[action]
        if action==11:self.heal_attempts+=1
        self.last[action]=self.tick;self.tick+=1

def choose_rule_v4(visual,diag,memory):
    if not visual[0] or not visual[1] or diag.get("detection_failed",True):return 0
    dx=diag.get("distance_px")
    if not isinstance(dx,(int,float)) or not math.isfinite(dx):return 0
    if memory.tick<memory.busy_until:return 0
    sign=1 if dx>=0 else -1
    if memory.last_move!=sign:return 2 if sign>0 else 1
    # Stop submitting attacks during visual warning; previous attack may block parry.
    if visual[3] and abs(dx)<=32:
        return 4 if memory.warning_age>=8 and memory.parry_age>=9 else 0
    # Existing normal F button, at most three submitted attempts. No HP inspection/heal injection.
    if memory.tick>=120 and memory.age(11)>=150 and memory.heal_attempts<3:return 11
    if abs(dx)>14:
        if abs(dx)<=32 and memory.age(7)>=100:return 7
        return 2 if sign>0 else 1
    if memory.age(7)>=100:return 7
    return 3


def load_v4(path, seed):
    path = Path(path)
    if path.stat().st_size > 16_000_000:
        raise ValueError("Checkpoint too large")
    metadata = json.loads(path.read_text(encoding="utf-8-sig"))
    if metadata.get("experiment") != EXPERIMENT:
        raise ValueError("V4 requires a fresh V4 checkpoint, not a V1/V2 model")
    model = TabularQ.load(path, seed=seed)
    if any(len(json.loads(key)) != STATE_LENGTH for key in model.q):
        raise ValueError("V4 checkpoint state width mismatch")
    model.initialization = metadata.get("initialization")
    model.demonstration_anchor = metadata.get("demonstration_anchor")
    return model


def save_v4(model, path):
    path = Path(path)
    staging = path.with_name(path.name+".v4-staging")
    model.save(staging)
    data = json.loads(staging.read_text(encoding="utf-8"))
    data["experiment"] = EXPERIMENT
    data["reward_transform"] = "raw + 0.001 per observed action transition; training only"
    data["state_length"] = STATE_LENGTH
    if getattr(model, "initialization", None):
        data["initialization"] = model.initialization
    if getattr(model, "demonstration_anchor", None):
        data["demonstration_anchor"] = model.demonstration_anchor
    atomic_json(path, data)
    staging.unlink()


def run(args):
    if args.train and (args.policy != "tabular-v4" or not args.checkpoint or not args.report):
        raise ValueError("Training requires tabular-v4, checkpoint and report")
    if not 0 <= args.epsilon <= 1 or not 0 < args.max_steps <= 5000:
        raise ValueError("Invalid epsilon/step bounds")
    if not 0 < args.max_seconds <= 1800 or not 0 < args.timeout <= 1800:
        raise ValueError("Invalid time bounds")
    if args.policy not in ("random-v4", "rule-v4", "tabular-v4"):
        raise ValueError("Unknown V4 policy")
    exists = bool(args.checkpoint and Path(args.checkpoint).is_file())
    if args.policy == "tabular-v4" and not args.train and not exists:
        raise ValueError("Evaluation requires an existing V4 checkpoint")
    model = load_v4(args.checkpoint, args.seed) if exists and args.policy == "tabular-v4" else TabularQ(seed=args.seed)
    provenance = getattr(model, "initialization", None)
    if provenance and not args.train and args.seed in {source["seed"] for source in provenance.get("sources", [])}:
        raise ValueError("Demonstration/evaluation seed overlap; choose held-out seed")
    anchor=None
    if getattr(args,"anchor_checkpoint",None):
        if not args.train:raise ValueError("Anchor is training-only; evaluation uses frozen Q values")
        from demonstration_anchor_v4 import DemonstrationAnchor
        anchor=DemonstrationAnchor(args.anchor_checkpoint,args.seed)
        model.demonstration_anchor=anchor.metadata
    client, tracker, random = BridgeClient(args.bridge), Tracker(), RandomPolicy(args.seed)
    memory = DecisionMemory()
    start = seen = time.monotonic()
    count = missing = warning = consecutive_missing = 0
    raw_total = shaped_total = 0.0
    previous = None
    trajectory, saved_transitions = [], []
    actions = [0]*len(ACTIONS)
    terminal = truncated = False
    final, state_counts = {}, {}
    while time.monotonic()-start < args.max_seconds and count < args.max_steps:
        item = client.read()
        if item is None:
            if time.monotonic()-seen > args.timeout:
                raise TimeoutError("Game bridge unresponsive")
            time.sleep(.01)
            continue
        msg, frame = item
        seen = time.monotonic()
        visual, diag = tracker.observe(frame)
        state = memory.observe(visual)
        key = ",".join(map(str, state))
        state_counts[key] = state_counts.get(key, 0)+1
        undetected = not visual[0] or not visual[1]
        missing += int(undetected)
        consecutive_missing = consecutive_missing + 1 if undetected else 0
        if consecutive_missing >= 30:
            if args.report:
                atomic_json(Path(args.report).with_suffix(".observation-error.json"), {
                    "error":"30 consecutive observations without both actors", "completed":False,
                    "training_checkpoint_saved":False, "steps":count+1,
                    "diagnostic":diag, "reason":"Do not train on blind/idle demonstrations"})
            raise RuntimeError("Visual observation failure: both actors missing for 30 steps")
        warning += int(bool(visual[3]))
        terminal, truncated = msg["terminated"], msg["truncated"]
        raw = msg["reward"]
        raw_total += raw
        shaped = raw+(.001 if previous is not None else 0)
        shaped_total += shaped
        if previous is not None:
            if len(saved_transitions) >= MAX_TRANSITIONS:
                raise ValueError("Recorded trajectory exceeded 5000 transitions")
            old, action = previous
            if args.train:
                transition = (old, action, shaped, state, terminal or truncated)
                model.update(*transition)
                if anchor:anchor.apply(model)
                trajectory.append(transition)
            saved_transitions.append({
                "state": old, "action": action, "raw_reward": raw,
                "shaped_reward": shaped, "next_state": state,
                "terminated": terminal, "truncated": truncated
            })
        if args.report and (count % 120 == 0 or terminal or truncated):
            samples = Path(args.report).with_suffix(".frames")
            samples.mkdir(parents=True, exist_ok=True)
            if len(list(samples.glob("*.ppm"))) < 32:
                w,h,rgb = frame
                (samples/f"{count:05d}.ppm").write_bytes(f"P6\n{w} {h}\n255\n".encode()+rgb)
                atomic_json(samples/f"{count:05d}.json", {"state":state, "visual":diag,
                    "warning_age":memory.warning_age, "own_parry_age":memory.parry_age})
        count += 1
        final = msg
        if terminal or truncated:
            break
        if args.policy == "tabular-v4":
            action = model.choose(state, epsilon=args.epsilon if args.train else 0)
        elif args.policy == "rule-v4":
            action = choose_rule_v4(visual, diag, memory)
        else:
            action = random.choose(state)
        client.act(msg, action)
        actions[action] += 1
        previous = (state, action)
        memory.acted(action)
    if args.report:
        # Record all policies without learning from evaluation or rule demonstrations.
        # This does not reconstruct trajectories from older episodes.
        atomic_json(Path(args.report).with_suffix(".trajectory.json"), {
            "schema":"tower-pixel-v4-trajectory/1", "seed":args.seed,
            "policy":args.policy, "training":args.train,
            "completed":terminal or truncated,
            "reward_transform":"raw + 0.001; applied to learning only in training",
            "transitions":saved_transitions
        })
    if args.train:
        if not (terminal or truncated):
            raise RuntimeError("Incomplete episode: no replay or checkpoint save")
        model.replay(trajectory, sweeps=8)
        if anchor:
            for _ in range(8):anchor.apply(model)
        save_v4(model, args.checkpoint)
    report = {
        "schema":EXPERIMENT, "initialization":provenance, "demonstration_anchor":getattr(model,"demonstration_anchor",None), "anchor_updates_this_run":anchor.updates if anchor else 0, "policy":args.policy, "seed":args.seed, "training":args.train,
        "observation":"RGB tracker + own action history incl. submitted-action lock estimate; no HP/cooldown telemetry",
        "steps":count, "reward":raw_total, "raw_reward":raw_total,
        "shaped_reward":shaped_total, "training_reward":shaped_total if args.train else None,
        "reward_transform":"raw + 0.001 per real transition; updates only in training",
        "terminated":terminal, "truncated":truncated, "outcome":final.get("reason","unknown"),
        "wall_seconds":round(time.monotonic()-start,3), "actions":dict(zip(ACTIONS,actions)),
        "missing_actor_observations":missing, "missing_actor_fraction":missing/max(1,count),
        "warning_observations":warning, "state_visits":state_counts,
        "online_updates":model.online_updates, "replay_updates":model.replay_updates,
        "evaluation_epsilon":None if args.train else 0, "completed":terminal or truncated
    }
    if args.report:
        atomic_json(args.report, report)
    print(json.dumps(report, ensure_ascii=True))
    return report


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--bridge", required=True)
    p.add_argument("--policy", choices=["random-v4","rule-v4","tabular-v4"], required=True)
    p.add_argument("--checkpoint")
    p.add_argument("--anchor-checkpoint")
    p.add_argument("--report")
    p.add_argument("--train", action="store_true")
    p.add_argument("--seed", type=int, default=17)
    p.add_argument("--epsilon", type=float, default=.4)
    p.add_argument("--max-seconds", type=float, default=180)
    p.add_argument("--timeout", type=float, default=20)
    p.add_argument("--max-steps", type=int, default=5000)
    run(p.parse_args())


if __name__ == "__main__":
    main()
