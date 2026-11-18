import copy
import json
from pathlib import Path
import tempfile
import unittest

from tabular_learning import TabularQ, MAX_TRANSITIONS


class TabularTests(unittest.TestCase):
    def test_one_step_online_and_terminal_no_bootstrap(self):
        q=TabularQ(alpha=.2,gamma=.995)
        q.update(("future",),3,100,("done",),True)
        q.update(("now",),0,1,("future",),True)
        self.assertAlmostEqual(q.values(("now",))[0],.2)
        self.assertEqual((q.online_updates,q.replay_updates,q.updates),(2,0,2))

    def test_reverse_replay_propagates_and_counts(self):
        q=TabularQ(alpha=1,gamma=.995)
        trajectory=[(("start",),2,0,("middle",),False),
                    (("middle",),3,1,("done",),True)]
        self.assertEqual(q.replay(trajectory,sweeps=1),2)
        self.assertAlmostEqual(q.values(("start",))[2],.995)
        self.assertEqual(q.choose(("start",)),2)
        self.assertEqual((q.online_updates,q.replay_updates),(0,2))

    def test_greedy_evaluation_never_updates_or_inserts(self):
        q=TabularQ(seed=17)
        q.update(("known",),0,1,("end",),True)
        before=copy.deepcopy(q.q)
        for _ in range(100):
            self.assertEqual(q.choose(("known",),epsilon=0),0)
            q.choose(("unseen",),epsilon=0)
        self.assertEqual(before,q.q)
        self.assertEqual((q.online_updates,q.replay_updates),(1,0))

    def test_seeded_ties_and_exploration_keep_all_actions(self):
        a,b=TabularQ(seed=29),TabularQ(seed=29)
        self.assertEqual([a.choose(("new",)) for _ in range(30)],
                         [b.choose(("new",)) for _ in range(30)])
        actions={a.choose(("new",),epsilon=1) for _ in range(1000)}
        self.assertEqual(actions,set(range(7)))  # idle remains allowed

    def test_checkpoint_roundtrip_and_rng(self):
        q=TabularQ(seed=43)
        q.update(("near",1,False),3,.4,("near",1,True),True)
        q.replay([(("near",1,False),3,.4,("near",1,True),True)],sweeps=2)
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/"q.json";q.save(p);loaded=TabularQ.load(p)
            self.assertEqual(q.q,loaded.q)
            self.assertEqual((loaded.online_updates,loaded.replay_updates),(1,2))
            self.assertEqual([q.choose(("unknown",)) for _ in range(40)],
                             [loaded.choose(("unknown",)) for _ in range(40)])
            self.assertEqual(TabularQ.load(p,seed=7).q,q.q)

    def test_invalid_transition_does_not_update(self):
        q=TabularQ()
        invalid=[(("s",),7,0,("n",),False),
                 (("s",),True,0,("n",),False),
                 (("s",),1,float("nan"),("n",),False),
                 (("s",),1,float("inf"),("n",),False),
                 (("s",),1,0,("n",),1),
                 (("s",.7),1,0,("n",),False),
                 ({"hp":10},1,0,("n",),False)]
        for args in invalid:
            with self.assertRaises(ValueError):q.update(*args)
        self.assertEqual(q.q,{})
        self.assertEqual(q.updates,0)
        for epsilon in [-1,2,float("nan"),True]:
            with self.assertRaises(ValueError):q.choose(("s",),epsilon)

    def test_replay_bounds_and_ended_episode(self):
        q=TabularQ()
        t=(("s",),0,0,("n",),True)
        for trajectory,sweeps in [([t],11),([t],-1),([t],True),
                                  ([t]*(MAX_TRANSITIONS+1),1),
                                  ([(("s",),0,0,("n",),False)],1),
                                  ([t,t],1)]:
            with self.assertRaises(ValueError):q.replay(trajectory,sweeps)
        self.assertEqual(q.replay([],0),0)
        self.assertEqual(q.updates,0)

    def test_bad_checkpoint_rejected(self):
        q=TabularQ();q.update(("s",),0,0,("end",),True)
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/"q.json";q.save(p);good=json.loads(p.read_text())
            for field,value in [("schema","wrong"),("alpha",float("nan")),
                                ("gamma",2),("online_updates",-1),
                                ("replay_updates",True),("actions",["idle"])]:
                changed=copy.deepcopy(good);changed[field]=value
                p.write_text(json.dumps(changed))
                with self.assertRaises(ValueError):TabularQ.load(p)
            changed=copy.deepcopy(good)
            changed["q"]['["s"]'][0]=float("inf")
            p.write_text(json.dumps(changed))
            with self.assertRaises(ValueError):TabularQ.load(p)


if __name__=="__main__":
    unittest.main()
