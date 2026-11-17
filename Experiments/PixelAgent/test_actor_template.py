import unittest,importlib.util
from actor_template import match_actor
@unittest.skipUnless(importlib.util.find_spec('numpy'),'NumPy optional experiment dependency unavailable')
class ActorTemplateTests(unittest.TestCase):
 def test_blank_rejected(self):
  with self.assertRaises(ValueError):match_actor(bytes(640*480*3),bytes(12*12*3),12,12)
 def test_bad_length(self):
  with self.assertRaises(ValueError):match_actor(b'',b'',12,12)
 def test_exact_synthetic_location(self):
  import numpy as np
  t=np.random.default_rng(3).integers(0,255,(20,20,3),dtype=np.uint8);a=np.zeros((480,640,3),dtype=np.uint8);a[300:320,100:120]=t;r=match_actor(a.tobytes(),t.tobytes(),20,20)
  self.assertEqual(r['box'][:2],[100,300]);self.assertGreater(r['ncc'],.99);self.assertFalse(r['action_authorized'])
if __name__=='__main__':unittest.main()
