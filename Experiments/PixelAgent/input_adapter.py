"""Dependency-injected short-pulse input controller. No Windows calls or CLI.
Not wired to gameplay. Production transport/capture adapters are intentionally absent.
"""
from dataclasses import dataclass
import math
import threading
from input_safety import ALLOWED_KEYS

@dataclass(frozen=True)
class PulseResult:
    ok: bool
    reason: str
    unresolved_keys: tuple = ()

class InputPulseAdapter:
    """transport contract:
    - physical_keys(): keys physically held by user (unknown => exception).
    - key_down(key, identity): True only when accepted; False means not delivered.
    - release_owned(key, identity): release only this session's injected key; True
      confirms cleanup, even after focus loss. Must not activate windows or release
      unrelated physical keys. A global SendInput wrapper alone is NOT sufficient.

    Any ambiguous delivery is tracked before cleanup. A future real adapter must
    distinguish physical/injected input and deal with focus changes atomically
    enough for its platform. This class cannot remove OS check/send races.
    """
    def __init__(self, gate, snapshot, transport, clock, sleep, *, enabled=False):
        self.gate,self.snapshot,self.transport=gate,snapshot,transport
        self.clock,self.sleep=clock,sleep
        self.enabled=enabled
        self.owned=set()
        self.lock=threading.Lock()

    def _cleanup(self):
        for key in sorted(self.owned):
            try:
                if self.transport.release_owned(key,self.gate.identity) is True:
                    self.owned.remove(key)
            except Exception:
                pass
        if not self.owned:self.gate.released()
        return tuple(sorted(self.owned))

    def _abort(self, reason):
        self.gate._stop(reason)
        return PulseResult(False,reason,self._cleanup())

    def pulse(self, keys, hold_seconds=.05):
        if not self.enabled:return PulseResult(False,'disabled')
        if not self.lock.acquire(blocking=False):return PulseResult(False,'busy')
        try:
            desired=frozenset(keys)
            if not desired<=ALLOWED_KEYS:return self._abort('unsupported_key')
            if not math.isfinite(hold_seconds) or not .01<=hold_seconds<=.1:
                return self._abort('invalid_hold_duration')
            if self.owned:return self._abort('unresolved_previous_delivery')
            # Check before every individual down, not just at pulse start.
            for key in sorted(desired):
                state=self.snapshot()
                decision=self.gate.decide(state,self.owned|{key})
                if not decision.allowed:return self._abort(decision.reason)
                physical=self.transport.physical_keys()
                if physical is None:return self._abort('physical_input_unknown')
                if set(physical):return self._abort('physical_input_active')
                # Recheck freshest focus/identity/host state immediately before send.
                decision=self.gate.decide(self.snapshot(),self.owned|{key})
                if not decision.allowed:return self._abort(decision.reason)
                self.owned.add(key)  # an exception may occur after actual delivery
                accepted=self.transport.key_down(key,self.gate.identity)
                if accepted is False:
                    self.owned.remove(key)
                    return self._abort('key_down_rejected')
                if accepted is not True:return self._abort('key_down_unknown')
                self.gate.commit(self.owned)
            start=self.clock()
            if not math.isfinite(start):return self._abort('invalid_clock')
            # Bounded polling also prevents an injected/faulty clock from hanging.
            for _ in range(20):
                decision=self.gate.decide(self.snapshot(),self.owned)
                if not decision.allowed:return self._abort(decision.reason)
                now=self.clock()
                if not math.isfinite(now) or now<start:return self._abort('invalid_clock')
                if now-start>=hold_seconds:break
                self.sleep(min(.01,hold_seconds-(now-start)))
            else:return self._abort('clock_not_advancing')
            unresolved=self._cleanup()
            if unresolved:return self._abort('key_release_failed')
            return PulseResult(True,'pulse_completed')
        except Exception:
            return self._abort('adapter_exception')
        finally:
            self.lock.release()

    def stop(self):
        """Caller must serialize stop with pulse; live cancellation comes from snapshot."""
        if not self.lock.acquire(blocking=False):return PulseResult(False,'busy')
        try:return self._abort('explicit_stop')
        finally:self.lock.release()
