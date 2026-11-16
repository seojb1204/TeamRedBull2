import json,tempfile,unittest
from pathlib import Path
from imitation_training import train
from visual_agent_v3 import load_v3,save_v3,run
from types import SimpleNamespace
class ImitationTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.data={'schema':'tower-pixel-v3-trajectory/1','policy':'rule-v3','seed':17,'training':False,'completed':True,
         'transitions':[{'state':[1,1,3,0,1,0,0],'action':2,'terminated':True,'truncated':False,'raw_reward':9999}]}
    def tearDown(self):self.tmp.cleanup()
    def write(self):
        p=self.root/'demo.json';p.write_text(json.dumps(self.data));return p
    def test_clone_and_read_only(self):
        out=self.root/'model.json';train([self.write()],out,evaluation_seeds=[201]);before=out.read_bytes()
        m=load_v3(out,42);self.assertEqual(m.choose((1,1,3,0,1,0,0)),2);self.assertEqual(before,out.read_bytes())
        self.assertEqual(m.online_updates,0);self.assertEqual(m.replay_updates,0)
    def test_reward_not_label(self):
        p=self.write();a=self.root/'a';train([p],a,evaluation_seeds=[201]);self.data['transitions'][0]['raw_reward']=-9999
        b=self.root/'b';train([self.write()],b,evaluation_seeds=[201]);self.assertEqual(json.loads(a.read_text())['q'],json.loads(b.read_text())['q'])
    def test_seed_overlap(self):
        with self.assertRaises(ValueError):train([self.write()],self.root/'m',evaluation_seeds=[17])
    def test_duplicate(self):
        p=self.write()
        with self.assertRaises(ValueError):train([p,p],self.root/'m')
    def test_incomplete(self):
        self.data['completed']=False
        with self.assertRaises(ValueError):train([self.write()],self.root/'m')
    def test_non_pixel_state(self):
        self.data['transitions'][0]['state'][0]=.123
        with self.assertRaises(ValueError):train([self.write()],self.root/'m')
    def test_invalid_action(self):
        self.data['transitions'][0]['action']=True
        with self.assertRaises(ValueError):train([self.write()],self.root/'m')
    def test_provenance_survives_save(self):
        out=self.root/'model';train([self.write()],out,evaluation_seeds=[201]);m=load_v3(out,201)
        newer=self.root/'new';save_v3(m,newer)
        self.assertEqual(json.loads(newer.read_text())['initialization']['sources'][0]['seed'],17)
    def test_live_evaluation_rejects_training_seed(self):
        out=self.root/'model';train([self.write()],out,evaluation_seeds=[201])
        args=SimpleNamespace(train=False,policy='tabular-v3',checkpoint=str(out),report=None,epsilon=0,max_steps=10,max_seconds=1,timeout=1,seed=17)
        with self.assertRaisesRegex(ValueError,'overlap'):run(args)
    def test_no_overwrite(self):
        p=self.write()
        with self.assertRaises(ValueError):train([p],p)
if __name__=='__main__':unittest.main()
