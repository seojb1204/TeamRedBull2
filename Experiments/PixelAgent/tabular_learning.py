"""Bounded tabular Q experiment. State comes from pixel-derived discrete bins only.

Reward and terminal are training arguments, never appended to state by this module.
No action masks, forced attacks, idle suppression, environment reads, or GPU dependencies.
Caller may include its own previous actions in the discrete state.
"""
import json
import math
import os
from pathlib import Path
import random

SCHEMA = "tower-pixel-tabular-q/1"
ACTIONS = ("idle", "left", "right", "attack", "parry", "jump", "dash")
MAX_TRANSITIONS = 5000
MAX_SWEEPS = 10
MAX_STATES = 10000


def _number(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(name + " must be finite numeric")
    return float(value)


def _key(state):
    if not isinstance(state, tuple) or not 1 <= len(state) <= 16:
        raise ValueError("State must be a nonempty discrete tuple, at most 16 bins")
    for v in state:
        if isinstance(v, str):
            if not 1 <= len(v) <= 64:
                raise ValueError("State string length must be 1..64")
        elif isinstance(v, bool):
            pass
        elif isinstance(v, int):
            if abs(v) > 100000:
                raise ValueError("State integer outside bounded bin range")
        else:
            raise ValueError("State bins must be strings, booleans, or integers, not telemetry floats")
    return json.dumps(state, ensure_ascii=True, separators=(",", ":"))


def _counter(v):
    if isinstance(v, bool) or not isinstance(v, int) or v < 0:
        raise ValueError("Invalid update count")
    return v


def _rng_tuple(v):
    return tuple(_rng_tuple(x) for x in v) if isinstance(v, list) else v


class TabularQ:
    def __init__(self, seed=17, alpha=.2, gamma=.995):
        self.alpha = _number(alpha, "alpha")
        self.gamma = _number(gamma, "gamma")
        if not 0 < self.alpha <= 1 or not 0 <= self.gamma <= 1:
            raise ValueError("alpha must be (0,1], gamma [0,1]")
        self.rng = random.Random(seed)
        self.q = {}
        self.online_updates = 0
        self.replay_updates = 0

    @property
    def updates(self):
        return self.online_updates + self.replay_updates

    def values(self, state):
        """Read-only lookup: evaluation never inserts unseen states into the table."""
        return tuple(self.q.get(_key(state), [0.0] * len(ACTIONS)))

    def choose(self, state, epsilon=0.0):
        epsilon = _number(epsilon, "epsilon")
        if not 0 <= epsilon <= 1:
            raise ValueError("epsilon must be in [0,1]")
        values = self.values(state)
        if epsilon and self.rng.random() < epsilon:
            return self.rng.randrange(len(ACTIONS))
        best = max(values)
        # All seven actions, including idle, remain eligible; seeded random ties.
        return self.rng.choice([i for i, value in enumerate(values) if abs(value-best) <= 1e-12])

    @staticmethod
    def _transition(state, action, reward, next_state, terminal):
        key, next_key = _key(state), _key(next_state)
        if isinstance(action, bool) or not isinstance(action, int) or not 0 <= action < len(ACTIONS):
            raise ValueError("Action must be integer 0..6")
        reward = _number(reward, "reward")
        if not isinstance(terminal, bool):
            raise ValueError("terminal must be bool")
        return key, action, reward, next_key, terminal

    def _apply(self, transition, replay=False):
        key, action, reward, next_key, terminal = transition
        row = self.q.get(key, [0.0] * len(ACTIONS))
        target = reward if terminal else reward + self.gamma * max(self.q.get(next_key, [0.0] * len(ACTIONS)))
        updated = row[action] + self.alpha * (target-row[action])
        if not math.isfinite(updated):
            raise ValueError("Update overflow; table left unchanged")
        if key not in self.q and len(self.q) >= MAX_STATES:
            raise ValueError("State table capacity reached")
        row = list(row)
        row[action] = updated
        self.q[key] = row
        if replay:
            self.replay_updates += 1
        else:
            self.online_updates += 1

    def update(self, state, action, reward, next_state, terminal):
        """One genuine observed transition; does not retain private environment data."""
        self._apply(self._transition(state, action, reward, next_state, terminal))

    def replay(self, trajectory, sweeps=5):
        """Reverse replay of an ended trajectory, max 5000 transitions x 10 sweeps.

        Each entry is (state, action, reward, next_state, terminal).
        Set terminal=True for the final truncated transition as well: no bootstrap
        into another game process. This is reuse of observations, NOT new play.
        """
        if isinstance(sweeps, bool) or not isinstance(sweeps, int) or not 0 <= sweeps <= MAX_SWEEPS:
            raise ValueError("Replay sweeps must be integer 0..10")
        if not isinstance(trajectory, (list, tuple)) or len(trajectory) > MAX_TRANSITIONS:
            raise ValueError("Replay requires a bounded list/tuple of <=5000 transitions")
        checked = []
        for item in trajectory:
            if not isinstance(item, (list, tuple)) or len(item) != 5:
                raise ValueError("Each transition must have five fields")
            checked.append(self._transition(*item))
        if checked and not checked[-1][-1]:
            raise ValueError("Replay only after episode completion/truncation")
        if any(t[-1] for t in checked[:-1]):
            raise ValueError("Replay must contain exactly one episode")
        if len(set(self.q).union(t[0] for t in checked)) > MAX_STATES:
            raise ValueError("State table capacity reached")
        before = self.replay_updates
        for _ in range(sweeps):
            for transition in reversed(checked):
                self._apply(transition, replay=True)
        return self.replay_updates-before

    def save(self, path):
        data = {
            "schema": SCHEMA,
            "observation": "discrete pixel-derived state bins; optional own action history",
            "actions": list(ACTIONS), "alpha": self.alpha, "gamma": self.gamma,
            "online_updates": self.online_updates, "replay_updates": self.replay_updates,
            "q": self.q, "rng_state": self.rng.getstate()
        }
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        temp = path.with_name(path.name + ".tmp")
        temp.write_text(json.dumps(data, ensure_ascii=True, allow_nan=False, sort_keys=True), encoding="utf-8")
        os.replace(temp, path)

    @classmethod
    def load(cls, path, seed=None):
        path = Path(path)
        if path.stat().st_size > 16_000_000:
            raise ValueError("Checkpoint too large")
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data.get("schema") != SCHEMA or data.get("actions") != list(ACTIONS):
            raise ValueError("Checkpoint schema/action mismatch")
        model = cls(seed, data["alpha"], data["gamma"])
        model.online_updates = _counter(data["online_updates"])
        model.replay_updates = _counter(data["replay_updates"])
        table = data["q"]
        if not isinstance(table, dict) or len(table) > MAX_STATES:
            raise ValueError("Invalid checkpoint table")
        for key, row in table.items():
            decoded = json.loads(key)
            if not isinstance(decoded, list) or _key(tuple(decoded)) != key:
                raise ValueError("Invalid/noncanonical state key")
            if not isinstance(row, list) or len(row) != len(ACTIONS):
                raise ValueError("Invalid action row")
            model.q[key] = [_number(v, "Q value") for v in row]
        if seed is None:
            try:
                model.rng.setstate(_rng_tuple(data["rng_state"]))
            except (TypeError, ValueError, KeyError) as exc:
                raise ValueError("Invalid checkpoint RNG state") from exc
        return model
