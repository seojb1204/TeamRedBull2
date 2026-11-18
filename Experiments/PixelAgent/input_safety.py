"""Pure decision layer only: does not capture screens, inspect Windows or send keys.
A future trusted adapter must collect complete host evidence and recheck immediately
before native input. These booleans are evidence contracts, not proof by themselves.
"""
from dataclasses import dataclass
import math

def finite_number(value):
    return type(value) in (int, float) and math.isfinite(value)


def positive_int(value):
    return type(value) is int and value > 0


def sequence_ok(value):
    return type(value) is int and value >= 0


ALLOWED_KEYS = frozenset({'left', 'right', 'space', 'shift', 'j', 'k', 'q', 'w', 'e', 'r', 'f', 'escape', 'enter'})

@dataclass(frozen=True)
class Snapshot:
    now: float
    observation_at: float
    process_scan_at: float
    pid: int
    process_started_at: float  # creation identity prevents PID reuse
    hwnd: int
    foreground_hwnd: int
    window_pid: int
    process_alive: bool
    scan_complete: bool
    conflict_found: bool
    gpu_known_safe: bool
    user_input_sequence: int
    width: int
    height: int

@dataclass(frozen=True)
class Decision:
    allowed: bool
    reason: str
    press: frozenset
    release_owned: frozenset

class InputSafetyGate:
    """One explicitly owned test session; any violation latches until a new session.
    No focus stealing, global key release, process termination or automatic resume.
    commit() must be called only after the native adapter confirms successful send.
    """
    def __init__(self, *, pid, process_started_at, hwnd, width, height, user_input_sequence,
                 observation_max_age=.25, scan_max_age=1.0):
        if not all(positive_int(v) for v in (pid, hwnd, width, height)) or not sequence_ok(user_input_sequence):
            raise ValueError('Positive owned process/window and viewport required')
        if not all(finite_number(v) for v in (process_started_at, observation_max_age, scan_max_age)) or observation_max_age <= 0 or scan_max_age <= 0:
            raise ValueError('Finite identity and positive freshness limits required')
        self.identity=(pid, process_started_at, hwnd, width, height)
        self.user_sequence=user_input_sequence
        self.observation_max_age=observation_max_age
        self.scan_max_age=scan_max_age
        self.held=frozenset()
        self.stopped=None
        self.pending=None
        self.last_now=None

    def _stop(self, reason):
        self.stopped=reason
        self.pending=None
        return Decision(False, reason, frozenset(), self.held)

    def decide(self, snapshot, desired_keys):
        if self.stopped:
            return self._stop(self.stopped)
        s=snapshot
        desired=frozenset(desired_keys)
        if not desired <= ALLOWED_KEYS:
            return self._stop('unsupported_key')
        if not all(finite_number(v) for v in (s.now,s.observation_at,s.process_scan_at,s.process_started_at)):
            return self._stop('invalid_time')
        if not all(positive_int(v) for v in (s.pid,s.hwnd,s.foreground_hwnd,s.window_pid,s.width,s.height)) or not sequence_ok(s.user_input_sequence):
            return self._stop('invalid_identity_evidence')
        if self.last_now is not None and s.now < self.last_now:
            return self._stop('clock_reversed')
        self.last_now=s.now
        if (s.pid,s.process_started_at,s.hwnd,s.width,s.height)!=self.identity or s.window_pid!=s.pid or s.process_alive is not True:
            return self._stop('owned_window_or_process_changed')
        if s.foreground_hwnd!=s.hwnd:
            return self._stop('focus_lost')
        if s.user_input_sequence!=self.user_sequence:
            return self._stop('user_interrupted')
        if s.scan_complete is not True or s.conflict_found is not False or s.gpu_known_safe is not True:
            return self._stop('host_unsafe_or_unknown')
        if not 0 <= s.now-s.observation_at <= self.observation_max_age:
            return self._stop('stale_observation')
        if not 0 <= s.now-s.process_scan_at <= self.scan_max_age:
            return self._stop('stale_process_scan')
        self.pending=desired
        return Decision(True, 'ready', desired-self.held, self.held-desired)

    def commit(self, desired_keys):
        desired=frozenset(desired_keys)
        if self.stopped or self.pending is None or desired!=self.pending:
            raise RuntimeError('No matching successful native input decision')
        self.held=desired
        self.pending=None

    def stop(self):
        return self._stop('explicit_stop')

    def released(self):
        """Adapter confirms only its owned key-ups were delivered; never auto resume."""
        self.held=frozenset()
