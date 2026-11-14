"""V3 pixel-only experiment; V2 is preserved and checkpoints are not interchangeable.

The only shaping is +0.001 per actual transition during training, cancelling the
environment's per-decision cost. Raw/shaped returns are diagnostic only;
actual damage, parries, survival time and outcome are the primary metrics.
Rule-V3 is explicitly hand-authored, not a learned policy.
"""
import argparse
import json
import math
import time
from pathlib import Path
from pixel_agent import BridgeClient, RandomPolicy, atomic_json, ACTIONS
from visual_tracker import Tracker
from tabular_learning import TabularQ, MAX_TRANSITIONS

EXPERIMENT = "tower-pixel-v3/1"
STATE_LENGTH = 7


class DecisionMemory:
    """Only visual history and own submitted actions; never actual cooldown telemetry."""
    def __init__(self):
        self.tick = 0
        self.last_move = 0
        self.parry_at = -100
        self.warning_age = 0

    @property
    def parry_age(self):
        return self.tick-self.parry_at

    def observe(self, visual):
        if len(visual) != 4:
            raise ValueError("Expected four tracker bins")
        self.warning_age = self.warning_age+1 if visual[3] else 0
        age_bin = 0 if not self.warning_age else 1 if self.warning_age < 3 else 2
        return tuple(visual)+(self.last_move, int(self.parry_age < 8), age_bin)

    def acted(self, action):
        if action in (1, 2):
            self.last_move = -1 if action == 1 else 1
        if action == 4:
            self.parry_at = self.tick
        self.tick += 1


def choose_rule_v3(visual, diag, memory):
    if not visual[0] or not visual[1] or diag.get("detection_failed", True):
        return 0
    dx = diag.get("distance_px")
    if not isinstance(dx, (int, float)) or not math.isfinite(dx):
        return 0
    target_sign = 1 if dx >= 0 else -1
    # This is a one-step submitted facing input, not knowledge of actual game facing.
    if memory.last_move != target_sign:
        return 2 if target_sign > 0 else 1
    if visual[3] and memory.warning_age >= 3 and memory.parry_age >= 8 and abs(dx) <= 32:
        return 4
    if abs(dx) > 14:
        return 2 if target_sign > 0 else 1
    return 3


def load_v3(path, seed):
    path = Path(path)
    if path.stat().st_size > 16_000_000:
        raise ValueError("Checkpoint too large")
    metadata = json.loads(path.read_text(encoding="utf-8-sig"))
    if metadata.get("experiment") != EXPERIMENT:
        raise ValueError("V3 requires a fresh V3 checkpoint, not a V1/V2 model")
    model = TabularQ.load(path, seed=seed)
    if any(len(json.loads(key)) != STATE_LENGTH for key in model.q):
        raise ValueError("V3 checkpoint state width mismatch")
    model.initialization = metadata.get("initialization")
    return model


def save_v3(model, path):
    path = Path(path)
    staging = path.with_name(path.name+".v3-staging")
    model.save(staging)
    data = json.loads(staging.read_text(encoding="utf-8"))
    data["experiment"] = EXPERIMENT
    data["reward_transform"] = "raw + 0.001 per observed action transition; training only"
    data["state_length"] = STATE_LENGTH
    if getattr(model, "initialization", None):
        data["initialization"] = model.initialization
    atomic_json(path, data)
    staging.unlink()


def run(args):
    if args.train and (args.policy != "tabular-v3" or not args.checkpoint or not args.report):
        raise ValueError("Training requires tabular-v3, checkpoint and report")
    if not 0 <= args.epsilon <= 1 or not 0 < args.max_steps <= 5000:
        raise ValueError("Invalid epsilon/step bounds")
    if not 0 < args.max_seconds <= 1800 or not 0 < args.timeout <= 1800:
        raise ValueError("Invalid time bounds")
    if args.policy not in ("random-v3", "rule-v3", "tabular-v3"):
        raise ValueError("Unknown V3 policy")
    exists = bool(args.checkpoint and Path(args.checkpoint).is_file())
    if args.policy == "tabular-v3" and not args.train and not exists:
        raise ValueError("Evaluation requires an existing V3 checkpoint")
    model = load_v3(args.checkpoint, args.seed) if exists and args.policy == "tabular-v3" else TabularQ(seed=args.seed)
    provenance = getattr(model, "initialization", None)
    if provenance and not args.train and args.seed in {source["seed"] for source in provenance.get("sources", [])}:
        raise ValueError("Demonstration/evaluation seed overlap; choose held-out seed")
    client, tracker, random = BridgeClient(args.bridge), Tracker(), RandomPolicy(args.seed)
    memory = DecisionMemory()
    start = seen = time.monotonic()
    count = missing = warning = consecutive_missing = 0
    raw_total = shaped_total = 0.0
    previous = None
    trajectory, saved_transitions = [], []
    actions = [0]*7
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
        if args.policy == "tabular-v3":
            action = model.choose(state, epsilon=args.epsilon if args.train else 0)
        elif args.policy == "rule-v3":
            action = choose_rule_v3(visual, diag, memory)
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
            "schema":"tower-pixel-v3-trajectory/1", "seed":args.seed,
            "policy":args.policy, "training":args.train,
            "completed":terminal or truncated,
            "reward_transform":"raw + 0.001; applied to learning only in training",
            "transitions":saved_transitions
        })
    if args.train:
        if not (terminal or truncated):
            raise RuntimeError("Incomplete episode: no replay or checkpoint save")
        model.replay(trajectory, sweeps=8)
        save_v3(model, args.checkpoint)
    report = {
        "schema":EXPERIMENT, "initialization":provenance, "policy":args.policy, "seed":args.seed, "training":args.train,
        "observation":"RGB tracker + own action history + consecutive visual warning age",
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
    p.add_argument("--policy", choices=["random-v3","rule-v3","tabular-v3"], required=True)
    p.add_argument("--checkpoint")
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
