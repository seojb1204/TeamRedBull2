"""Synthetic detector checks + optional saved-frame diagnostics (not accuracy benchmark)."""
import argparse
import json
from pathlib import Path
import unittest
from visual_tracker import Tracker,choose_action


def fixture(hero=40,boss=105,warning=False):
    w,h=160,90;rgb=bytearray([20,10,25]*(w*h))
    def pixel(x,y,color):rgb[(y*w+x)*3:(y*w+x)*3+3]=bytes(color)
    if hero is not None:
        pixel(hero,64,(220,215,210));pixel(hero+1,64,(230,225,220))
    if boss is not None:
        for y in range(53,67):
            for x in range(boss-2,boss+3):pixel(x,y,(160,110,65))
        if warning:
            for y in range(54,64):pixel(boss+4,y,(60,160,220))
    return w,h,bytes(rgb)


class TrackerTests(unittest.TestCase):
    def test_detect_and_approach(self):
        state,d=Tracker().observe(fixture())
        self.assertEqual(state,(1,1,3,0));self.assertEqual(choose_action(state,d),2)
        self.assertAlmostEqual(d['player_x'],40.5);self.assertAlmostEqual(d['boss_x'],105)
    def test_near_attack_and_visual_defense(self):
        state,d=Tracker().observe(fixture(hero=94))
        self.assertEqual(choose_action(state,d),3)
        state,d=Tracker().observe(fixture(hero=94,warning=True))
        self.assertEqual(choose_action(state,d),4)
    def test_missing_is_idle_and_stale_expires(self):
        t=Tracker();t.observe(fixture())
        for age in (1,2,3):
            state,d=t.observe(fixture(hero=None,boss=None))
            self.assertEqual(d['player_stale_steps'],age)
            self.assertTrue(state[0])
        state,d=t.observe(fixture(hero=None,boss=None))
        self.assertEqual(state[:2],(0,0));self.assertEqual(choose_action(state,d),0)
    def test_hud_and_floor_excluded(self):
        w,h,rgb=fixture(hero=None,boss=None);rgb=bytearray(rgb)
        for y in (5,80):
            for x in range(160):rgb[(y*w+x)*3:(y*w+x)*3+3]=bytes((255,255,255))
        state,d=Tracker().observe((w,h,bytes(rgb)))
        self.assertTrue(d['detection_failed']);self.assertEqual(choose_action(state,d),0)
    def test_reset_forgets_location(self):
        t=Tracker();t.observe(fixture());t.reset()
        self.assertEqual(t.observe(fixture(hero=None,boss=None))[0][:2],(0,0))
    def test_no_telemetry_argument(self):
        with self.assertRaises(TypeError):Tracker().observe(fixture(),boss_position=1)


def saved_frame_report(root):
    from pixel_agent import read_ppm
    rows=[]
    # Captures are 120 steps apart, so reset per frame rather than invent continuity.
    for group in ('random-v2','rule-v2'):
        for path in sorted((Path(root)/group/'episode-1.frames').glob('*.ppm')):
            state,d=Tracker().observe(read_ppm(path))
            rows.append({'group':group,'frame':path.name,'state':state,'action':choose_action(state,d),
                         **{k:d[k] for k in ('player_x','player_y','boss_x','cyan_pixels','player_raw_detected','boss_raw_detected','detection_failed')}})
    print(json.dumps({'kind':'saved-frame heuristic diagnostics, NOT ground truth accuracy',
                      'note':'Independent tracker per sparse capture; positions may be false positives, terminal screens included',
                      'frames':len(rows),'detection_failure_count':sum(r['detection_failed'] for r in rows),
                      'rows':rows},indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--frames-root');args=p.parse_args()
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(TrackerTests)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():raise SystemExit(1)
    if args.frames_root:saved_frame_report(args.frames_root)
