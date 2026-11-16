import unittest
from ui_recognition import Patch,ScreenTemplate,TemplateRecognizer

class RecognitionTests(unittest.TestCase):
    def patch(self,x,color):return Patch(x,0,1,1,bytes([color]*3))
    def setUp(self):
        self.build='a'*64
        self.templates=(ScreenTemplate('lobby',(self.patch(0,10),self.patch(1,20)),self.patch(2,30)),ScreenTemplate('battle',(self.patch(0,100),self.patch(1,110))),ScreenTemplate('pause',(self.patch(0,200),self.patch(1,210))))
        self.r=TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=self.templates)
    def check(self,data,**kw):
        args=dict(width=3,height=1,build_sha256=self.build,full_frame=True);args.update(kw)
        return self.r.recognize(bytes(v for c in data for v in [c]*3),**args)
    def test_lobby_prompt(self):
        r=self.check([10,20,30]);self.assertEqual(r.screen,'lobby');self.assertTrue(r.enter_verified)
    def test_missing_prompt(self):
        r=self.check([10,20,90]);self.assertEqual(r.screen,'lobby');self.assertFalse(r.enter_verified)
    def test_battle(self):self.assertEqual(self.check([100,110,0]).screen,'battle')
    def test_pause(self):self.assertEqual(self.check([200,210,0]).screen,'pause')
    def test_single_anchor_not_enough(self):self.assertFalse(self.check([10,110,30]).verified)
    def test_unknown(self):self.assertEqual(self.check([60,60,60]).screen,'unknown')
    def test_wrong_build(self):self.assertFalse(self.check([10,20,30],build_sha256='b'*64).verified)
    def test_offscreen_without_hud(self):self.assertFalse(self.check([10,20,30],full_frame=False).verified)
    def test_wrong_size(self):self.assertFalse(self.check([10,20,30],width=4).verified)
    def test_tie_abstains(self):
        templates=(self.templates[0],ScreenTemplate('result',self.templates[0].anchors))
        self.r=TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=templates)
        self.assertFalse(self.check([10,20,30]).verified)
    def test_invalid_roi(self):
        with self.assertRaises(ValueError):TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=(ScreenTemplate('lobby',(self.patch(4,10),self.patch(1,20))),self.templates[1]))
    def test_duplicate_anchor_rejected(self):
        with self.assertRaises(ValueError):TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=(ScreenTemplate('lobby',(self.patch(0,10),self.patch(0,10))),self.templates[1]))
    def test_huge_capture_rejected_before_allocation(self):
        with self.assertRaises(ValueError):TemplateRecognizer(width=999999,height=999999,build_sha256=self.build,templates=self.templates)
    def test_fractional_geometry_rejected(self):
        p=Patch(.5,0,1,1,bytes([10]*3))
        with self.assertRaises(ValueError):TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=(ScreenTemplate('lobby',(p,self.patch(1,20))),self.templates[1]))
    def test_nonstring_build_rejected(self):
        with self.assertRaises(ValueError):TemplateRecognizer(width=3,height=1,build_sha256=None,templates=self.templates)
    def test_nan_threshold(self):
        with self.assertRaises(ValueError):TemplateRecognizer(width=3,height=1,build_sha256=self.build,templates=self.templates,max_distance=float('nan'))
if __name__=='__main__':unittest.main()
