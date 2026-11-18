import unittest
from unittest.mock import patch
from temporal_template_bank import TemporalTemplateBank
class Tests(unittest.TestCase):
 def setUp(self):self.b=TemporalTemplateBank([dict(rgb=b'',width=30,height=47,name='pose')])
 def prediction(self,x=10,valid=True):return dict(box=[x,300,30,47],ncc=.9,margin=.2,candidate_valid=valid,action_authorized=False)
 def test_three_fresh_frames_only(self):
  with patch('temporal_template_bank.match_actor',return_value=self.prediction()):
   self.assertFalse(self.b.observe(b'',1)['temporally_confirmed']);self.assertFalse(self.b.observe(b'',1.1)['temporally_confirmed']);r=self.b.observe(b'',1.2);self.assertTrue(r['temporally_confirmed']);self.assertFalse(r['action_authorized'])
 def test_rejected_visual_resets(self):
  with patch('temporal_template_bank.match_actor',return_value=self.prediction()):self.b.observe(b'',1)
  with patch('temporal_template_bank.match_actor',return_value=self.prediction(valid=False)):self.assertEqual(self.b.observe(b'',1.1)['streak'],0)
 def test_teleport_does_not_inherit_streak(self):
  with patch('temporal_template_bank.match_actor',side_effect=[self.prediction(),self.prediction(400)]):self.b.observe(b'',1);self.assertEqual(self.b.observe(b'',1.1)['streak'],1)
 def test_bad_time_resets(self):
  for value in [1,.9,2,float('nan')]:
   with patch('temporal_template_bank.match_actor',return_value=self.prediction()):self.b.observe(b'',1)
   with self.assertRaises(ValueError):self.b.observe(b'',value)
   self.assertIsNone(self.b.last)
 def test_match_failure_resets(self):
  with patch('temporal_template_bank.match_actor',return_value=self.prediction()):self.b.observe(b'',1)
  with patch('temporal_template_bank.match_actor',side_effect=ValueError('Malformed RGB')):
   with self.assertRaises(ValueError):self.b.observe(b'',1.1)
  self.assertIsNone(self.b.last);self.assertEqual(self.b.streak,0)
 def test_invalid_type_resets(self):
  with patch('temporal_template_bank.match_actor',return_value=self.prediction()):self.b.observe(b'',1)
  with self.assertRaises(TypeError):self.b.observe(b'',None)
  self.assertIsNone(self.b.last)
 def test_return_cannot_modify_state(self):
  with patch('temporal_template_bank.match_actor',return_value=self.prediction()):r=self.b.observe(b'',1)
  r['candidate']['box'][0]=500
  self.assertEqual(self.b.last['box'][0],10)
if __name__=='__main__':unittest.main()
