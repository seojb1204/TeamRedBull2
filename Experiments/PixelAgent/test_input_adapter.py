import unittest
from dataclasses import replace
from input_adapter import InputPulseAdapter
from input_safety import InputSafetyGate, Snapshot

class Transport:
    def __init__(self):self.events=[];self.physical=set();self.down=True;self.up=True
    def physical_keys(self):return self.physical
    def key_down(self,k,i):
        self.events.append(('down',k))
        if isinstance(self.down,Exception):raise self.down
        return self.down
    def release_owned(self,k,i):self.events.append(('up',k));return self.up

class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.t=0.;self.n=0;self.change=None;self.transport=Transport()
        self.g=InputSafetyGate(pid=7,process_started_at=1,hwnd=11,width=960,height=540,user_input_sequence=3)
        self.adapter=InputPulseAdapter(self.g,self.snapshot,self.transport,lambda:self.t,self.sleep,enabled=True)
    def snapshot(self):
        self.n+=1
        s=Snapshot(self.t,self.t,self.t,7,1,11,11,7,True,True,False,True,3,960,540)
        return self.change(s,self.n) if self.change else s
    def sleep(self,v):self.t+=v
    def test_default_disabled(self):
        a=InputPulseAdapter(self.g,self.snapshot,self.transport,lambda:0,self.sleep)
        self.assertEqual(a.pulse(['j']).reason,'disabled');self.assertEqual(self.transport.events,[])
    def test_pulse_releases_only_owned(self):
        r=self.adapter.pulse(['left','j']);self.assertTrue(r.ok)
        self.assertEqual(self.transport.events,[('down','j'),('down','left'),('up','j'),('up','left')]);self.assertFalse(self.g.held)
    def test_foreground_changes_between_check_and_send(self):
        self.change=lambda s,n:replace(s,foreground_hwnd=99) if n==2 else s
        self.assertEqual(self.adapter.pulse(['j']).reason,'focus_lost');self.assertFalse(self.transport.events)
    def test_partial_delivery_next_key_blocked(self):
        self.change=lambda s,n:replace(s,scan_complete=False) if n>=3 else s
        r=self.adapter.pulse(['j','k']);self.assertFalse(r.ok);self.assertEqual(self.transport.events,[('down','j'),('up','j')])
    def test_delivery_exception_cleanup(self):
        self.transport.down=RuntimeError('after native send')
        r=self.adapter.pulse(['j']);self.assertFalse(r.ok);self.assertEqual(self.transport.events,[('down','j'),('up','j')])
    def test_explicit_rejection_no_extra_keyup(self):
        self.transport.down=False;self.assertFalse(self.adapter.pulse(['j']).ok);self.assertEqual(self.transport.events,[('down','j')])
    def test_uncertain_delivery_cleanup(self):
        self.transport.down=None;self.assertFalse(self.adapter.pulse(['j']).ok);self.assertEqual(self.transport.events,[('down','j'),('up','j')])
    def test_failed_release_reported_and_no_resume(self):
        self.transport.up=False;r=self.adapter.pulse(['j']);self.assertEqual(r.unresolved_keys,('j',));self.assertFalse(r.ok)
        count=len([e for e in self.transport.events if e[0]=='down']);self.adapter.pulse(['k'])
        self.assertEqual(count,len([e for e in self.transport.events if e[0]=='down']))
    def test_user_interruption_during_hold(self):
        self.change=lambda s,n:replace(s,user_input_sequence=4) if self.t>.01 else s
        r=self.adapter.pulse(['j']);self.assertEqual(r.reason,'user_interrupted');self.assertEqual(self.transport.events[-1],('up','j'))
    def test_physical_key_prevents_input(self):
        self.transport.physical={'shift'};self.assertEqual(self.adapter.pulse(['j']).reason,'physical_input_active');self.assertFalse(self.transport.events)
    def test_bad_key_and_duration(self):
        for keys,duration in [(['alt'],.05),(['j'],2),(['j'],float('nan'))]:
            self.setUp();self.assertFalse(self.adapter.pulse(keys,duration).ok);self.assertFalse(self.transport.events)
    def test_broken_clock_bounded_cleanup(self):
        self.adapter.sleep=lambda _:None;r=self.adapter.pulse(['j']);self.assertEqual(r.reason,'clock_not_advancing');self.assertFalse(r.unresolved_keys)
    def test_busy_no_input(self):
        self.adapter.lock.acquire()
        try:self.assertEqual(self.adapter.pulse(['j']).reason,'busy');self.assertFalse(self.transport.events)
        finally:self.adapter.lock.release()
if __name__=='__main__':unittest.main()
