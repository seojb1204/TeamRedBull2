import unittest
from flow_controller import OneBattleFlow,View
class FlowTests(unittest.TestCase):
    def setUp(self):self.f=OneBattleFlow(0);self.i=0;self.now=0
    def step(self,label,outcome='unknown',advance=.01):
        self.i+=1;self.now+=advance
        return self.f.observe(View(self.i,self.now,label,outcome),self.now)
    def confirm(self,label,outcome='unknown'):
        self.step(label,outcome);return self.step(label,outcome)
    def test_full_flow(self):
        self.assertEqual(self.confirm('lobby').intent,'start_battle')
        self.assertEqual(self.step('lobby').intent,'wait')
        self.assertEqual(self.confirm('battle').intent,'play_battle')
        self.assertEqual(self.confirm('result','win').intent,'return_lobby')
        self.assertEqual(self.step('result','win').intent,'wait')
        self.assertEqual(self.confirm('lobby').intent,'completed');self.assertEqual(self.f.result['outcome'],'win')
        self.assertEqual(self.step('lobby').intent,'completed')
    def test_unknown_no_input(self):self.assertEqual(self.confirm('unknown').intent,'wait')
    def test_unknown_resets_confirmations(self):
        self.step('lobby');self.step('unknown');self.assertEqual(self.step('lobby').intent,'wait')
    def test_initial_battle_rejected(self):self.assertEqual(self.confirm('battle').intent,'stop')
    def test_pause_stops_without_escape(self):self.assertEqual(self.step('pause').intent,'stop')
    def test_duplicate_frame(self):
        self.step('lobby');self.assertEqual(self.f.observe(View(self.i,self.now,'lobby'),self.now).intent,'stop')
    def test_stale_frame(self):self.assertEqual(self.f.observe(View(1,0,'lobby'),1).intent,'stop')
    def test_unexpected_exit(self):
        self.confirm('lobby');self.confirm('battle');self.assertEqual(self.confirm('lobby').intent,'stop')
    def test_ui_timeout_no_retry(self):
        self.confirm('lobby');self.assertEqual(self.step('lobby',advance=11).intent,'stop')
    def test_battle_timeout(self):
        self.confirm('lobby');self.confirm('battle');self.assertEqual(self.step('battle',advance=121).intent,'stop')
    def test_unknown_outcome_not_win(self):
        self.confirm('lobby');self.confirm('battle');self.confirm('result');self.assertEqual(self.f.result['outcome'],'unknown')
    def test_stop_latches(self):
        self.step('pause');self.assertEqual(self.confirm('lobby').intent,'stop')
if __name__=='__main__':unittest.main()
