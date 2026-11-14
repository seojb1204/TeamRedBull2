import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import visual_agent_v3 as v3
from tabular_learning import TabularQ

VISUAL=(1,1,1,1)
DIAG={"detection_failed":False,"distance_px":10}


class FakeClient:
    def __init__(self):
        self.actions=[]
        self.items=[
            ({"episode":"e","step":0,"reward":0.0,"terminated":False,"truncated":False,"reason":"running"},(20,20,bytes(1200))),
            ({"episode":"e","step":1,"reward":-.101,"terminated":True,"truncated":False,"reason":"loss"},(20,20,bytes(1200)))]
    def read(self):
        return self.items.pop(0) if self.items else None
    def act(self,msg,action):
        self.actions.append((msg["step"],action))


class FakeTracker:
    def observe(self,frame):
        return VISUAL,DIAG


class V3Tests(unittest.TestCase):
    def test_warning_age_bins_reset_and_state_width(self):
        m=v3.DecisionMemory()
        self.assertEqual(m.observe(VISUAL),(1,1,1,1,0,0,1))
        self.assertEqual(m.observe(VISUAL)[-1],1)
        self.assertEqual(m.observe(VISUAL)[-1],2)
        self.assertEqual(m.observe((1,1,1,0))[-1],0)
        self.assertEqual(m.warning_age,0)

    def test_rule_facing_then_delayed_parry_and_cooldown(self):
        m=v3.DecisionMemory();m.observe(VISUAL)
        self.assertEqual(v3.choose_rule_v3(VISUAL,DIAG,m),2)
        m.acted(2);m.observe(VISUAL)
        self.assertEqual(v3.choose_rule_v3(VISUAL,DIAG,m),3)
        m.acted(3);m.observe(VISUAL)
        self.assertEqual(v3.choose_rule_v3(VISUAL,DIAG,m),4)
        m.acted(4);m.observe(VISUAL)
        self.assertEqual(v3.choose_rule_v3(VISUAL,DIAG,m),3)
        for _ in range(7):
            m.acted(3);m.observe(VISUAL)
        self.assertEqual(m.parry_age,8)
        self.assertEqual(v3.choose_rule_v3(VISUAL,DIAG,m),4)

    def test_rule_unknown_far_and_target_flip(self):
        m=v3.DecisionMemory();m.last_move=1
        self.assertEqual(v3.choose_rule_v3((0,1,1,1),DIAG,m),0)
        self.assertEqual(v3.choose_rule_v3(VISUAL,dict(DIAG,distance_px=-10),m),1)
        self.assertEqual(v3.choose_rule_v3(VISUAL,dict(DIAG,distance_px=40),m),2)
        self.assertEqual(v3.choose_rule_v3(VISUAL,dict(DIAG,distance_px=float("nan")),m),0)

    def test_v3_checkpoint_rejects_v2_and_wrong_state_width(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/"q.json";q=TabularQ();q.save(p)
            with self.assertRaises(ValueError):v3.load_v3(p,17)
            q.update((1,1,1,0,1,0),3,1,(1,1,1,0,1,0),True)
            v3.save_v3(q,p)
            with self.assertRaises(ValueError):v3.load_v3(p,17)

    def args(self,root,train):
        return SimpleNamespace(bridge=str(root),policy="tabular-v3",train=train,
            checkpoint=str(root/"q.json"),report=str(root/"report.json"),
            seed=17,epsilon=.4,max_steps=10,max_seconds=3,timeout=2)

    def test_missing_frames_fail_without_checkpoint(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);args=self.args(root,True);args.max_steps=40
            client=FakeClient()
            client.items=[({"episode":"e","step":i,"reward":0.0,"terminated":False,"truncated":False},(20,20,bytes(1200))) for i in range(35)]
            with patch.object(v3,"BridgeClient",return_value=client), patch.object(v3,"Tracker") as tracker:
                tracker.return_value.observe.return_value=((0,0,0,0),{"detection_failed":True})
                with self.assertRaisesRegex(RuntimeError,"observation failure"):v3.run(args)
            self.assertFalse((root/"q.json").exists())
            self.assertEqual(len(client.actions),29)
            self.assertTrue((root/"report.observation-error.json").exists())

    def test_training_shaping_trajectory_and_update_counts(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);client=FakeClient()
            with patch.object(v3,"BridgeClient",return_value=client),patch.object(v3,"Tracker",FakeTracker):
                report=v3.run(self.args(root,True))
            self.assertAlmostEqual(report["raw_reward"],-.101)
            self.assertAlmostEqual(report["shaped_reward"],-.1)
            self.assertAlmostEqual(report["reward"],-.101)
            self.assertEqual((report["online_updates"],report["replay_updates"]),(1,8))
            trajectory=json.loads((root/"report.trajectory.json").read_text())
            self.assertEqual(len(trajectory["transitions"]),1)
            transition=trajectory["transitions"][0]
            self.assertEqual(len(transition["state"]),7)
            self.assertAlmostEqual(transition["shaped_reward"],-.1)
            self.assertEqual(v3.load_v3(root/"q.json",17).online_updates,1)

    def test_evaluation_does_not_update_or_write_checkpoint(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);v3.save_v3(TabularQ(),root/"q.json")
            before=(root/"q.json").read_bytes()
            with patch.object(v3,"BridgeClient",return_value=FakeClient()),patch.object(v3,"Tracker",FakeTracker),\
                 patch.object(TabularQ,"update",side_effect=AssertionError("eval updated")),\
                 patch.object(TabularQ,"replay",side_effect=AssertionError("eval replayed")),\
                 patch.object(v3,"save_v3",side_effect=AssertionError("eval saved")):
                report=v3.run(self.args(root,False))
            self.assertEqual((root/"q.json").read_bytes(),before)
            saved=json.loads((root/"report.trajectory.json").read_text())
            self.assertFalse(saved["training"])
            self.assertEqual(saved["policy"],"tabular-v3")
            self.assertEqual(len(saved["transitions"]),1)
            self.assertEqual(saved["transitions"][0]["raw_reward"],-.101)
            self.assertEqual(report["evaluation_epsilon"],0)
            self.assertIsNone(report["training_reward"])

    def test_rule_and_random_record_without_learning(self):
        for policy in ("rule-v3","random-v3"):
            with self.subTest(policy=policy),tempfile.TemporaryDirectory() as d:
                root=Path(d);args=self.args(root,False)
                args.policy=policy;args.checkpoint=None
                with patch.object(v3,"BridgeClient",return_value=FakeClient()),patch.object(v3,"Tracker",FakeTracker),\
                     patch.object(TabularQ,"update",side_effect=AssertionError("nontraining update")),\
                     patch.object(TabularQ,"replay",side_effect=AssertionError("nontraining replay")),\
                     patch.object(v3,"save_v3",side_effect=AssertionError("nontraining save")):
                    report=v3.run(args)
                saved=json.loads((root/"report.trajectory.json").read_text())
                self.assertEqual(saved["policy"],policy)
                self.assertFalse(saved["training"])
                self.assertEqual(len(saved["transitions"]),1)
                self.assertFalse((root/"q.json").exists())
                self.assertEqual(report["online_updates"],0)
                self.assertEqual(report["replay_updates"],0)

    def test_locally_bounded_recording_marks_incomplete(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);args=self.args(root,False)
            args.policy="random-v3";args.checkpoint=None;args.max_steps=1
            with patch.object(v3,"BridgeClient",return_value=FakeClient()),patch.object(v3,"Tracker",FakeTracker):
                report=v3.run(args)
            saved=json.loads((root/"report.trajectory.json").read_text())
            self.assertFalse(saved["completed"])
            self.assertEqual(saved["transitions"],[])
            self.assertFalse(report["completed"])

    def test_invalid_bound_and_missing_checkpoint_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.args(Path(d),False)
            with self.assertRaises(ValueError):v3.run(args)
            args.train=True;args.max_steps=5001
            with self.assertRaises(ValueError):v3.run(args)
            args.max_steps=10;args.timeout=float("nan")
            with self.assertRaises(ValueError):v3.run(args)


if __name__=="__main__":
    unittest.main()
