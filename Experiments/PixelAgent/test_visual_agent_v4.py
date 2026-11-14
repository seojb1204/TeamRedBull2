import json,tempfile,unittest
from pathlib import Path
from visual_agent_v4 import DecisionMemory,choose_rule_v4,load_v4,save_v4,BridgeClient,RandomPolicy,STATE_LENGTH
from tabular_learning_v4 import TabularQ,ACTIONS
from tabular_learning import TabularQ as OldQ
class V4Tests(unittest.TestCase):
    def test_native_actions(self):
        self.assertEqual(len(ACTIONS),12);self.assertEqual(ACTIONS[11],'heal-f')
    def test_state_uses_history_only(self):
        m=DecisionMemory();self.assertEqual(len(m.observe((1,1,1,0))),STATE_LENGTH)
        m.acted(3);self.assertEqual(m.observe((1,1,1,1))[7],1)
    def test_attack_lock_prevents_parry_and_facing_submission(self):
        m=DecisionMemory();m.last_move=1;m.acted(3)
        self.assertEqual(choose_rule_v4((1,1,-1,1),{'detection_failed':False,'distance_px':-10},m),0)
    def test_warning_wait_not_attack(self):
        m=DecisionMemory();m.last_move=1;m.observe((1,1,1,1))
        self.assertEqual(choose_rule_v4((1,1,1,1),{'detection_failed':False,'distance_px':10},m),0)
    def test_heal_is_input_budget_not_health_read(self):
        m=DecisionMemory();m.last_move=1;m.tick=120
        self.assertEqual(choose_rule_v4((1,1,1,0),{'detection_failed':False,'distance_px':10},m),11)
        m.acted(11);self.assertEqual(m.heal_attempts,1)
        m.tick=300;m.heal_attempts=3
        self.assertNotEqual(choose_rule_v4((1,1,1,0),{'detection_failed':False,'distance_px':10},m),11)
    def test_v3_checkpoint_not_reused(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'q';OldQ().save(p)
            with self.assertRaises(ValueError):load_v4(p,17)
    def test_v4_q_actions_train_and_roundtrip(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'q';m=TabularQ();state=tuple([0]*STATE_LENGTH)
            m.update(state,11,1,state,True);save_v4(m,p);self.assertEqual(load_v4(p,17).choose(state),11)
    def test_bridge_rejects_legacy(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d);(p/'f.ppm').write_bytes(b'P6\n1 1\n255\n\0\0\0')
            (p/'transition_0.json').write_text(json.dumps({'episode':'e','step':0,'frame':'f.ppm','reward':0,'terminated':False,'truncated':False,'actionSet':'legacy7'}))
            with self.assertRaises(ValueError):BridgeClient(p).read()
    def test_v4_imitation_to_anchor_roundtrip(self):
        from imitation_training_v4 import train
        from demonstration_anchor_v4 import DemonstrationAnchor
        with tempfile.TemporaryDirectory() as d:
            p=Path(d);demo=p/'demo';out=p/'model'
            demo.write_text(json.dumps({'schema':'tower-pixel-v4-trajectory/1','policy':'rule-v4','seed':701,'training':False,'completed':True,'transitions':[{'state':[0]*10,'action':11,'terminated':True,'truncated':False}]}))
            train([demo],out,evaluation_seeds=[801]);m=load_v4(out,801)
            self.assertEqual(m.choose(tuple([0]*10)),11)
            self.assertEqual(len(DemonstrationAnchor(out,801).actions),1)
    def test_random_uses_extended_native_buttons(self):
        m=RandomPolicy(17);self.assertTrue(any(m.choose(())>6 for _ in range(50)))
if __name__=='__main__':unittest.main()
