import unittest
from screen_threats import ScreenThreats
class ThreatTests(unittest.TestCase):
 def frame(self,x=None,y=300,w=6,h=6):
  b=bytearray(640*480*3)
  if x is not None:
   for yy in range(y,y+h):
    for xx in range(x,x+w):b[(yy*640+xx)*3:(yy*640+xx)*3+3]=bytes((71,158,255))
  return bytes(b)
 def test_blank_blocked(self):
  r=ScreenThreats().observe(640,480,self.frame(),1);self.assertEqual(r['rim_dynamics'],'absent');self.assertFalse(r['action_authorized'])
 def test_moving_candidate_not_verified(self):
  t=ScreenThreats();t.observe(640,480,self.frame(100),1);r=t.observe(640,480,self.frame(110),1.1)
  self.assertEqual(len(r['small_cyan_tracks']),1);self.assertAlmostEqual(r['small_cyan_tracks'][0]['vx_px_s'],100)
  self.assertFalse(r['small_cyan_tracks'][0]['verified_projectile']);self.assertIsNone(r['small_cyan_tracks'][0]['horizontal_crossing_eta_s'])
 def test_reversed_time(self):
  t=ScreenThreats();t.observe(640,480,self.frame(),1)
  with self.assertRaises(ValueError):t.observe(640,480,self.frame(),1)
 def test_gap(self):
  t=ScreenThreats();t.observe(640,480,self.frame(),1)
  with self.assertRaises(ValueError):t.observe(640,480,self.frame(),2)
 def test_malformed(self):
  with self.assertRaises(ValueError):ScreenThreats().observe(320,240,b'',1)
  with self.assertRaises(ValueError):ScreenThreats().observe(640,480,self.frame(),float('nan'))
 def test_onset_not_attack(self):self.assertEqual(ScreenThreats().observe(640,480,self.frame(100),1)['rim_dynamics'],'onset')
if __name__=='__main__':unittest.main()
