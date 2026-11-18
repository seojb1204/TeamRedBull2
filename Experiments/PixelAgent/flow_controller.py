"""Mock-testable one-battle UI flow; no classifier, coordinates or native input.
Classifier labels are hypotheses. Two matching fresh frames reduce flicker, not
proof of correctness. A future adapter must verify buttons from current pixels.
"""
from dataclasses import dataclass
import math

@dataclass(frozen=True)
class View:
    frame_id: int
    captured_at: float
    screen: str  # lobby/battle/result/unknown/pause
    outcome: str = 'unknown'  # read only from result pixels, never internal stats

@dataclass(frozen=True)
class FlowDecision:
    intent: str  # wait/start_battle/play_battle/return_lobby/stop/completed
    reason: str

class OneBattleFlow:
    def __init__(self, now, *, battle_limit=120., ui_limit=10.):
        if not all(math.isfinite(v) for v in (now,battle_limit,ui_limit)) or battle_limit<=0 or ui_limit<=0:
            raise ValueError('Finite positive time limits required')
        self.phase='lobby';self.since=now;self.last_now=now
        self.battle_limit=battle_limit;self.ui_limit=ui_limit
        self.last_frame=-1;self.label=None;self.confirmations=0
        self.result=None;self.stop_reason=None

    def stop(self,reason):
        self.phase='stopped';self.stop_reason=reason
        return FlowDecision('stop',reason)

    def observe(self,view,now):
        if self.phase=='stopped':return FlowDecision('stop',self.stop_reason)
        if self.phase=='done':return FlowDecision('completed','one_battle_verified')
        if not math.isfinite(now) or now<self.last_now:return self.stop('invalid_clock')
        self.last_now=now
        if not math.isfinite(view.captured_at) or not 0<=now-view.captured_at<=.25:return self.stop('stale_or_invalid_frame')
        if not isinstance(view.frame_id,int) or view.frame_id<=self.last_frame:return self.stop('duplicate_or_reordered_frame')
        self.last_frame=view.frame_id
        if view.screen not in {'lobby','battle','result','unknown','pause'}:return self.stop('invalid_screen_label')
        if view.screen=='pause':return self.stop('pause_or_user_intervention')
        limit=self.battle_limit if self.phase=='battle' else self.ui_limit
        if now-self.since>=limit:return self.stop('state_timeout:'+self.phase)
        if view.screen=='unknown':
            self.label=None;self.confirmations=0
            return FlowDecision('wait','screen_unknown_no_input')
        if view.screen==self.label:self.confirmations+=1
        else:self.label=view.screen;self.confirmations=1
        if self.confirmations<2:return FlowDecision('wait','confirm_second_frame')
        if self.phase=='lobby':
            if view.screen!='lobby':return self.stop('expected_initial_lobby')
            self.phase='starting';self.since=now
            return FlowDecision('start_battle','lobby_confirmed_once')
        if self.phase=='starting':
            if view.screen=='lobby':return FlowDecision('wait','await_battle_do_not_repeat_start')
            if view.screen!='battle':return self.stop('unexpected_start_transition')
            self.phase='battle';self.since=now
            return FlowDecision('play_battle','battle_confirmed')
        if self.phase=='battle':
            if view.screen=='battle':return FlowDecision('play_battle','battle_confirmed')
            if view.screen!='result':return self.stop('unexpected_battle_exit')
            if view.outcome not in {'win','loss','unknown'}:return self.stop('invalid_result_outcome')
            self.result={'frame_id':view.frame_id,'captured_at':view.captured_at,'outcome':view.outcome}
            self.phase='returning';self.since=now
            return FlowDecision('return_lobby','result_observed_once')
        if self.phase=='returning':
            if view.screen=='result':return FlowDecision('wait','await_lobby_do_not_repeat_return')
            if view.screen!='lobby':return self.stop('unexpected_return_transition')
            self.phase='done'
            return FlowDecision('completed','one_battle_verified')
        return self.stop('invalid_phase')
