import tempfile,unittest,json
from pathlib import Path
from imitation_training import train
from visual_agent_v3 import load_v3
from demonstration_anchor import DemonstrationAnchor
class AnchorTests(unittest.TestCase):
    def test_soft_margin_and_source_immutable(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d);demo=p/'demo';modelpath=p/'model'
            demo.write_text(json.dumps({'schema':'tower-pixel-v3-trajectory/1','policy':'rule-v3','seed':301,'training':False,'completed':True,'transitions':[{'state':[1,1,1,0,1,0,0],'action':3,'terminated':True,'truncated':False}]}))
            train([demo],modelpath,evaluation_seeds=[401]);before=modelpath.read_bytes()
            model=load_v3(modelpath,601);key=next(iter(model.q));model.q[key][5]=2
            anchor=DemonstrationAnchor(modelpath,601);anchor.apply(model)
            self.assertGreater(model.q[key][3],1);self.assertLess(model.q[key][3],2)
            self.assertEqual(modelpath.read_bytes(),before);self.assertEqual(anchor.updates,1)
    def test_invalid_strength(self):
        with self.assertRaises(ValueError):DemonstrationAnchor('not-read',17,rate=0)
if __name__=='__main__':unittest.main()
