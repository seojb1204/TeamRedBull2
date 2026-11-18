import unittest
from dataclasses import replace
from flow_coordinator import FlowCoordinator,UiEvidence
from flow_controller import OneBattleFlow,View
from input_adapter import PulseResult

class Adapter:
    def __init__(self):self.calls=[];self.ok=True;self.stops=0
    def pulse(self,keys,duration):self.calls.append((keys,duration));return PulseResult(self.ok,'mock',() if self.ok else ('j',))
    def stop(self):self.stops+=1;return PulseResult(False,'stop')
class CoordinatorTests(unittest.TestCase):
    def setUp(self):
        self.a=Adapter();self.seen=[];self.i=0;self.t=0
        def policy(*args):self.seen.append(args);return ['j']
        self.c=FlowCoordinator(OneBattleFlow(0),self.a,policy)
    def tick(self,label,**kw):
        self.i+=1;self.t+=.01
        e=UiEvidence(View(self.i,self.t,label),bytes(12),2,2,True,self.i,label,'enter',True)
        return self.c.tick(replace(e,**kw),self.t)
    def confirm(self,label):self.tick(label);return self.tick(label)
    def test_full_once(self):
        self.confirm('lobby');self.tick('lobby');self.confirm('battle');self.confirm('result');self.tick('result');r=self.confirm('lobby')
        self.assertEqual(r.status,'completed');self.assertEqual([k for k,d in self.a.calls].count(frozenset({'enter'})),2)
        self.assertEqual(self.seen,[(2,2,bytes(12))])
    def test_current_binding_required(self):
        self.tick('lobby');r=self.tick('lobby',binding_frame_id=1);self.assertEqual(r.status,'stopped');self.assertFalse(self.a.calls)
    def test_wrong_binding(self):
        for kw in [{'binding_key':'escape'},{'binding_screen':'result'},{'binding_validated':False}]:
            self.setUp();self.tick('lobby');self.assertEqual(self.tick('lobby',**kw).status,'stopped');self.assertFalse(self.a.calls)
    def test_unknown_pause_unverified(self):
        for label,kw in [('unknown',{}),('pause',{}),('lobby',{'label_validated':False})]:
            self.setUp();self.assertEqual(self.tick(label,**kw).status,'stopped');self.assertFalse(self.a.calls)
    def test_bad_rgb(self):self.assertEqual(self.tick('lobby',rgb=b'').status,'stopped')
    def test_policy_never_on_lobby(self):self.confirm('lobby');self.assertFalse(self.seen)
    def test_failure_latches_and_preserves_cleanup(self):
        self.a.ok=False;self.tick('lobby');r=self.tick('lobby');self.assertEqual(r.unresolved_keys,('j',));count=len(self.a.calls)
        self.confirm('battle');self.assertEqual(len(self.a.calls),count)
    def test_policy_cannot_send_ui_key(self):
        self.c.policy=lambda *args:['enter'];self.confirm('lobby');self.assertEqual(self.confirm('battle').status,'stopped');self.assertEqual(len(self.a.calls),1)
    def test_real_adapter_composition_with_mock_transport(self):
        from input_adapter import InputPulseAdapter
        from input_safety import InputSafetyGate,Snapshot
        events=[]
        class Transport:
            def physical_keys(self):return set()
            def key_down(self,key,identity):events.append(('down',key));return True
            def release_owned(self,key,identity):events.append(('up',key));return True
        gate=InputSafetyGate(pid=7,process_started_at=1,hwnd=11,width=2,height=2,user_input_sequence=0)
        def snapshot():return Snapshot(self.t,self.t,self.t,7,1,11,11,7,True,True,False,True,0,2,2)
        def sleep(dt):self.t+=dt
        self.c.adapter=InputPulseAdapter(gate,snapshot,Transport(),lambda:self.t,sleep,enabled=True)
        self.confirm('lobby');self.confirm('battle');self.confirm('result');r=self.confirm('lobby')
        self.assertEqual(r.status,'completed')
        self.assertEqual(events,[('down','enter'),('up','enter'),('down','j'),('up','j'),('down','enter'),('up','enter')])
    def test_result_not_internal_policy_input(self):
        self.confirm('lobby');self.confirm('battle');n=len(self.seen);self.confirm('result');self.assertEqual(len(self.seen),n)
if __name__=='__main__':unittest.main()
