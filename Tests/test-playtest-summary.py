import importlib.util, unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('summary',Path(__file__).with_name('summarize-playtest.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
def row(**changes):
    r=dict(profile='novice',seed='17',boss='0',dda='False',round='1',outcome='death',steps='60',seconds='1',playerHp='0',bossHp='50',hits='1',pattern='wave',level='0',opportunities='1',parries='0',patternHits='1',success='0')
    r.update(changes);return r
class Tests(unittest.TestCase):
    def test_patterns_not_trials(self): self.assertEqual(m.summarize([row(),row(pattern='strike')])['trials'],1)
    def test_pairs(self): self.assertEqual(len(m.summarize([row(),row(dda='True')])['pairedDda']),1)
    def test_duplicate(self):
        with self.assertRaises(ValueError): m.summarize([row(),row()])
    def test_inconsistent(self):
        with self.assertRaises(ValueError): m.summarize([row(),row(pattern='strike',seconds='2')])
    def test_nan(self):
        with self.assertRaises(ValueError): m.summarize([row(seconds='nan')])
    def test_no_trials(self):
        with self.assertRaises(ValueError): m.summarize([])
    def test_interval(self):
        lo,hi=m.wilson(0,4);self.assertEqual(lo,0);self.assertGreater(hi,.4)
    def test_timeout(self): self.assertEqual(m.summarize([row(outcome='timeout')])['groups'][0]['timeouts'],1)
if __name__=='__main__': unittest.main()
