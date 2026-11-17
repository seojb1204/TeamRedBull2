import time,unittest
from windows_capture import capture,validate_payload
def good(out,identity):
    t=time.monotonic()
    out.put(dict(identity=identity,width=2,height=1,rgb=bytes([0,0,0,255,255,255]),started_at=t,ended_at=t))
def stall(out,identity):time.sleep(10)
def failure(out,identity):out.put({'error':'test failure'})
class CaptureTests(unittest.TestCase):
    def base(self):
        return dict(identity=(1,2,3),width=2,height=1,rgb=bytes([0,0,0,255,255,255]),started_at=9.9,ended_at=10)
    def test_valid(self):self.assertEqual(validate_payload(self.base(),(1,2,3),10).width,2)
    def test_corrupt(self):
        for patch in ({'identity':(1,2,4)},{'identity':(True,2,3)},{'width':True},{'height':0},{'rgb':b'a'},
                      {'rgb':bytes(6)},{'started_at':0},{'ended_at':11},{'ended_at':float('nan')},{'error':'bad'}):
            with self.subTest(patch=patch),self.assertRaises(RuntimeError):
                validate_payload({**self.base(),**patch},(1,2,3),10)
    def test_process_success(self):self.assertEqual(capture(1,2,3,timeout=3,_worker=good).height,1)
    def test_process_failure(self):
        with self.assertRaises(RuntimeError):capture(1,2,3,timeout=3,_worker=failure)
    def test_watchdog(self):
        start=time.monotonic()
        with self.assertRaises(TimeoutError):capture(1,2,3,timeout=.1,_worker=stall)
        self.assertLess(time.monotonic()-start,2)
    def test_bad_args(self):
        for args in ((True,2,3),(1,2,'3'),(0,2,3)):
            with self.assertRaises(ValueError):capture(*args)
if __name__=='__main__':unittest.main(verbosity=2)
