import unittest
from visible_rim import detect_visible_rim
class RimTests(unittest.TestCase):
 def frame(self,color,n=30,y=250):
  data=bytearray(640*480*3)
  for x in range(n):data[(y*640+x)*3:(y*640+x)*3+3]=bytes(color)
  return bytes(data)
 def test_dark_background(self):self.assertFalse(detect_visible_rim(640,480,self.frame((40,65,85)))['visible_rim_candidate'])
 def test_bright_blue(self):self.assertTrue(detect_visible_rim(640,480,self.frame((71,158,255)))['visible_rim_candidate'])
 def test_white_flash(self):self.assertFalse(detect_visible_rim(640,480,self.frame((255,255,255)))['visible_rim_candidate'])
 def test_small_noise(self):self.assertFalse(detect_visible_rim(640,480,self.frame((71,158,255),19))['visible_rim_candidate'])
 def test_hud_excluded(self):self.assertFalse(detect_visible_rim(640,480,self.frame((71,158,255),y=50))['visible_rim_candidate'])
 def test_failclosed(self):
  with self.assertRaises(ValueError):detect_visible_rim(320,240,b'')
  with self.assertRaises(ValueError):detect_visible_rim(640,480,b'')
 def test_never_authorizes_input(self):self.assertFalse(detect_visible_rim(640,480,self.frame((71,158,255)))['action_authorized'])
if __name__=='__main__':unittest.main()
