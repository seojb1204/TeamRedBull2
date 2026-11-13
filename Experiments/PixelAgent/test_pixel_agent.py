import json
from pathlib import Path
import tempfile
import threading
import time
from types import SimpleNamespace
import unittest
from pixel_agent import (ACTIONS, FEATURES, BridgeClient, LinearQ, PixelFeatures,
                         RandomPolicy, VisualRulePolicy, atomic_json, read_ppm, run_bridge)


def frame(color=(0,0,0)):
    return (20,12,bytes(color)*240)


class PixelAgentTests(unittest.TestCase):
    def test_features_only_frame(self):
        f = PixelFeatures()
        x = f.observe(frame((30,100,200)))
        self.assertEqual(len(x),FEATURES)
        self.assertEqual(x[181:],[0.0]*180)
        y = f.observe(frame((60,100,200)))
        self.assertGreater(y[181],0)
        f.reset()
        self.assertEqual(f.observe(frame())[181:],[0.0]*180)
    def test_ppm_raster_whitespace_not_stripped(self):
        with tempfile.TemporaryDirectory() as d:
            path=Path(d)/'f.ppm'
            path.write_bytes(b'P6\n1 1\n255\n'+bytes((10,32,9)))
            self.assertEqual(read_ppm(path),(1,1,bytes((10,32,9))))
            path.write_bytes(b'P6\n1 1\n255\n'+bytes((1,2)))
            with self.assertRaises(ValueError):read_ppm(path)
    def test_reward_is_not_policy_input(self):
        model=LinearQ(7)
        x=PixelFeatures().observe(frame((255,0,0)))
        before=model.values(x)
        model.update(x,3,1,x,True)
        self.assertGreater(model.values(x)[3],before[3])
        # choose() accepts feature vector and exploration only, not a state dict.
        with self.assertRaises(TypeError):model.choose(x,reward=99)
    def test_checkpoint_roundtrip_and_bad_shape(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'checkpoint.json'
            m=LinearQ(4);x=PixelFeatures().observe(frame((255,0,0)))
            m.update(x,2,1,x,True);m.save(p)
            loaded=LinearQ.load(p,4)
            self.assertEqual(m.values(x),loaded.values(x))
            obj=json.loads(p.read_text());obj['weights'][0]=[];atomic_json(p,obj)
            with self.assertRaises(ValueError):LinearQ.load(p)
    def test_protocol_duplicate_stale_path_and_telemetry(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d); (root/'observation_e_0.ppm').write_bytes(b'P6\n20 12\n255\n'+frame()[2])
            msg={'episode':'e','step':0,'frame':'observation_e_0.ppm','reward':0,
                 'terminated':False,'truncated':False,'bossHp':99999,'windup':0.01}
            atomic_json(root/'transition.json',msg)
            c=BridgeClient(root);m,f=c.read();self.assertEqual(f,frame())
            self.assertIsNone(c.read());c.act(m,3)
            self.assertEqual(json.loads((root/'action.json').read_text()),{'episode':'e','step':0,'action':3})
            with self.assertRaises(ValueError):c.act(dict(m,step=1),3)
            # Even frame traversal in game-emitted files fails closed.
            atomic_json(root/'transition.json',dict(msg,step=1,frame='../secret.ppm'))
            with self.assertRaises(ValueError):c.read()
    def test_game_error_marker_fails_before_duplicate_reward(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)
            atomic_json(root/'bridge_error.json',{'schema':'towerPixel/1','error':'client_timeout_or_wall_limit'})
            with self.assertRaises(RuntimeError):BridgeClient(root).read()
    def test_policy_repeatable_and_bounded(self):
        x=PixelFeatures().observe(frame())
        a,b=RandomPolicy(17),RandomPolicy(17)
        self.assertEqual([a.choose(x) for _ in range(100)],[b.choose(x) for _ in range(100)])
        self.assertIn(VisualRulePolicy().choose(x),range(len(ACTIONS)))
    def test_lockstep_client_with_mock_transport(self):
        # Tests protocol only; fixture is NOT Unity or a game skill benchmark.
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)
            for step in (0,1):
                (root/f'observation_e_{step}.ppm').write_bytes(b'P6\n20 12\n255\n'+frame((10,30,60))[2])
            def transition(step):
                return {'episode':'e','step':step,'frame':f'observation_e_{step}.ppm',
                        'reward':float(step),'terminated':step==1,'truncated':False}
            atomic_json(root/'transition.json',transition(0))
            errors=[]
            def mock_game():
                until=time.monotonic()+2
                while not (root/'action.json').exists() and time.monotonic()<until:
                    time.sleep(.005)
                if not (root/'action.json').exists():
                    errors.append('No client action');return
                action=json.loads((root/'action.json').read_text())
                if action['episode']!='e' or action['step']!=0:errors.append('Wrong action ID')
                atomic_json(root/'transition.json',transition(1))
            worker=threading.Thread(target=mock_game);worker.start()
            args=SimpleNamespace(bridge=str(root),checkpoint=str(root/'q.json'),seed=17,
                 train=True,policy='learned',max_seconds=3,max_steps=10,timeout=2,
                 epsilon=.2,report=str(root/'report.json'))
            run_bridge(args);worker.join()
            self.assertEqual(errors,[])
            report=json.loads((root/'report.json').read_text())
            self.assertEqual(report['updates'],1)
            self.assertEqual(report['reward'],1)
            self.assertTrue(report['completed'])
            # Terminal step is never acknowledged with an action.
            self.assertEqual(json.loads((root/'action.json').read_text())['step'],0)
    def test_launch_guard_markers_config_and_art(self):
        from run_campaign import validate_build
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);exe=root/'Tower.exe';exe.write_bytes(b'fixture-not-an-executable')
            data=root/'Tower_Data';managed=data/'Managed';managed.mkdir(parents=True)
            dll=managed/'Assembly-CSharp.dll';dll.write_bytes(b'old build')
            with self.assertRaises(ValueError):validate_build(exe)
            dll.write_bytes(('-towerPixelAgent\0자동검증: 네트워크 꺼짐').encode('utf-16-le'))
            streaming=data/'StreamingAssets';(streaming/'Art').mkdir(parents=True)
            (streaming/'Art'/'test.png').write_bytes(b'keep-art')
            self.assertEqual(validate_build(exe)[0],exe)
            config=streaming/'supabase.json';config.write_text('not read by guard')
            with self.assertRaises(ValueError):validate_build(exe)
    def test_learning_visual_context(self):
        from synthetic_smoke import experiment
        result=experiment(17,600,210)
        self.assertGreater(result['learned_success_rate'],.90)
        self.assertLess(result['random_success_rate'],.25)

if __name__=='__main__':unittest.main()
