"""Offline composition contract. No live capture, classifier, Windows or launcher.
Evidence producers must validate current screenshot and key binding; a boolean
claim is not independent proof. Battle policy receives RGB only, never telemetry.
"""
from dataclasses import dataclass
from flow_controller import View

BATTLE_KEYS=frozenset({'left','right','space','shift','j','k','q','w','e','r','f'})

@dataclass(frozen=True)
class UiEvidence:
    view: View
    rgb: bytes
    width: int
    height: int
    label_validated: bool
    binding_frame_id: int
    binding_screen: str
    binding_key: str
    binding_validated: bool

@dataclass(frozen=True)
class CoordinationResult:
    status: str
    reason: str
    unresolved_keys: tuple=()

class FlowCoordinator:
    def __init__(self, flow, adapter, battle_policy):
        self.flow,self.adapter,self.policy=flow,adapter,battle_policy
        self.stopped=None

    def _stop(self,reason):
        self.stopped=reason;self.flow.stop(reason)
        try:
            cleanup=self.adapter.stop()
            return CoordinationResult('stopped',reason,cleanup.unresolved_keys)
        except Exception:
            return CoordinationResult('stopped',reason+':cleanup_unknown')

    def tick(self,evidence,now):
        if self.stopped:return CoordinationResult('stopped',self.stopped)
        try:
            e=evidence;v=e.view
            if e.label_validated is not True or v.screen in {'unknown','pause'}:
                return self._stop('screen_unverified_or_interrupted')
            if type(e.rgb) is not bytes or e.width<=0 or e.height<=0 or len(e.rgb)!=e.width*e.height*3:
                return self._stop('invalid_rgb')
            decision=self.flow.observe(v,now)
            if decision.intent=='stop':return self._stop(decision.reason)
            if decision.intent=='completed':
                cleanup=self.adapter.stop()
                if cleanup.unresolved_keys:return self._stop('completion_cleanup_failed')
                return CoordinationResult('completed',decision.reason)
            if decision.intent=='wait':return CoordinationResult('waiting',decision.reason)
            if decision.intent in {'start_battle','return_lobby'}:
                expected='lobby' if decision.intent=='start_battle' else 'result'
                if (e.binding_validated is not True or e.binding_frame_id!=v.frame_id or
                    e.binding_screen!=expected or v.screen!=expected or e.binding_key!='enter'):
                    return self._stop('ui_binding_not_verified_for_current_frame')
                keys=frozenset({'enter'})
            elif decision.intent=='play_battle':
                if v.screen!='battle':return self._stop('policy_wrong_screen')
                # Only pixels/dimensions cross this boundary, no HP/reward/phase/etc.
                keys=frozenset(self.policy(e.width,e.height,e.rgb))
                if not keys<=BATTLE_KEYS:return self._stop('policy_unsupported_key')
            else:return self._stop('unsupported_intent')
            result=self.adapter.pulse(keys,.05)
            if not result.ok:
                stopped=self._stop('input_failed:'+result.reason)
                return CoordinationResult(stopped.status,stopped.reason,tuple(sorted(set(result.unresolved_keys)|set(stopped.unresolved_keys))))
            return CoordinationResult('acted',decision.intent)
        except Exception:
            return self._stop('coordinator_exception')
