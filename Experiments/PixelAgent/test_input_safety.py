import unittest
from dataclasses import replace
from input_safety import InputSafetyGate,Snapshot

class SafetyTests(unittest.TestCase):
    def setUp(self):
        self.g=InputSafetyGate(pid=7,process_started_at=1,hwnd=11,width=960,height=540,user_input_sequence=3)
        self.s=Snapshot(now=10,observation_at=9.9,process_scan_at=9.5,pid=7,process_started_at=1,hwnd=11,foreground_hwnd=11,window_pid=7,process_alive=True,scan_complete=True,conflict_found=False,gpu_known_safe=True,user_input_sequence=3,width=960,height=540)
    def test_allowed_diff(self):
        self.assertEqual(self.g.decide(self.s,['j','left']).press,{'j','left'})
        self.g.commit(['j','left'])
        d=self.g.decide(self.s,['left']);self.assertEqual(d.release_owned,{'j'});self.assertFalse(d.press)
    def test_failed_send_not_owned(self):
        self.g.decide(self.s,['j']);self.assertFalse(self.g.stop().release_owned)
    def test_violations_latch(self):
        for changes in [{'foreground_hwnd':12},{'window_pid':8},{'pid':8},{'process_started_at':2},{'process_alive':False},{'scan_complete':False},{'conflict_found':True},{'gpu_known_safe':False},{'conflict_found':None},{'scan_complete':'yes'},{'user_input_sequence':4},{'width':800},{'observation_at':8},{'process_scan_at':8},{'observation_at':11},{'process_scan_at':11},{'now':float('nan')}]:
            with self.subTest(changes=changes):
                self.setUp();self.g.decide(self.s,['k']);self.g.commit(['k'])
                d=self.g.decide(replace(self.s,**changes),['j']);self.assertFalse(d.allowed);self.assertEqual(d.release_owned,{'k'});self.assertFalse(d.press)
                self.assertFalse(self.g.decide(self.s,['j']).allowed)
    def test_wrong_commit(self):
        self.g.decide(self.s,['j'])
        with self.assertRaises(RuntimeError):self.g.commit(['k'])
    def test_release_no_resume(self):
        self.g.decide(self.s,['j']);self.g.commit(['j']);self.g.stop();self.g.released()
        d=self.g.decide(self.s,['j']);self.assertFalse(d.allowed);self.assertFalse(d.release_owned)
    def test_unknown_key(self):self.assertFalse(self.g.decide(self.s,['alt']).allowed)
    def test_clock_reverse(self):
        self.g.decide(self.s,[]);self.assertFalse(self.g.decide(replace(self.s,now=9),[]).allowed)
if __name__=='__main__':unittest.main()
