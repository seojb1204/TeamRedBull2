import unittest
from dataclasses import replace
from types import SimpleNamespace
from ui_calibration_eval import Sample,evaluate,digest
B='a'*64;T='b'*64
class Recognizer:
    template_sha256=T
    def __init__(self):self.calls=0
    def recognize(self,rgb,**kwargs):
        self.calls+=1
        screen,verified,enter={1:('lobby',True,True),2:('unknown',False,False),3:('result',True,True),4:('battle',False,True)}[rgb[0]]
        return SimpleNamespace(screen=screen,verified=verified,enter_verified=enter,distance=.01,margin=.1)
def row(value,split='test',truth='lobby',allowed=True,session=None):
    rgb=bytes([value,0,0])
    return Sample(session or ('c' if split=='calibration' else 't'),str(value),split,rgb,digest(rgb),1,1,True,B,truth,allowed,'synthetic')
class EvaluationTests(unittest.TestCase):
    def run_eval(self,rows,**kw):return evaluate(rows,Recognizer(),build_sha256=B,width=1,height=1,template_sha256=T,template_sessions=['c'],**kw)
    def test_metrics_abstain_and_wrong_enter(self):
        result=self.run_eval([row(0,'calibration'),row(1),row(2,truth='battle',allowed=False),row(3,truth='battle',allowed=False)])
        self.assertEqual(result['test_frames'],3);self.assertEqual(result['abstained'],1)
        self.assertEqual(result['wrong_enter_grants'],1);self.assertEqual(result['accuracy_nonabstained'],.5)
        self.assertEqual(result['confusion_matrix']['battle']['unknown'],1)
        self.assertIn('synthetic',result['scope'])
    def test_zero_denominators_null(self):
        r=evaluate([],Recognizer(),build_sha256=B,width=1,height=1,template_sha256=T,template_sessions=[])
        for key in ['coverage','accuracy_nonabstained','abstain_rate','wrong_enter_rate_per_grant','wrong_enter_rate_all_test']:self.assertIsNone(r[key])
    def test_all_abstain(self):
        r=self.run_eval([row(0,'calibration'),row(2,truth='unknown',allowed=False)])
        self.assertIsNone(r['accuracy_nonabstained']);self.assertIsNone(r['wrong_enter_rate_per_grant']);self.assertEqual(r['abstain_rate'],1)
    def test_slices_expose_hidden_label_and_session_failure(self):
        r=self.run_eval([row(0,'calibration'),row(1,session='good'),
                         row(2,truth='battle',allowed=False,session='bad'),
                         row(3,truth='battle',allowed=False,session='bad')])
        self.assertEqual(r['per_truth_label']['lobby']['accuracy_nonabstained'],1)
        battle=r['per_truth_label']['battle']
        self.assertEqual(battle['test_frames'],2)
        self.assertEqual(battle['coverage'],.5)
        self.assertEqual(battle['accuracy_nonabstained'],0)
        self.assertEqual(battle['wrong_enter_grants'],1)
        self.assertEqual(r['per_test_session']['bad'],battle)
        self.assertEqual(r['per_test_session']['good']['accuracy_nonabstained'],1)
        self.assertNotIn('c',r['per_test_session'])
        for slices in [r['per_truth_label'],r['per_test_session']]:
            for key in ['test_frames','accepted','abstained','correct_nonabstained','enter_grants','wrong_enter_grants']:
                self.assertEqual(sum(v[key] for v in slices.values()),r[key])
    def test_absent_labels_and_calibration_only_slices(self):
        r=self.run_eval([row(0,'calibration')])
        self.assertEqual(r['per_test_session'],{})
        self.assertEqual(set(r['per_truth_label']),{'lobby','battle','result','pause','unknown'})
        for group in r['per_truth_label'].values():
            self.assertEqual(group['test_frames'],0)
            for key in ['coverage','accuracy_nonabstained','wrong_enter_rate_per_grant','wrong_enter_rate_all_test']:
                self.assertIsNone(group[key])
    def test_unknown_abstention_slice_and_invalid_enter(self):
        r=self.run_eval([row(0,'calibration'),row(2,truth='unknown',allowed=False,session='abstain'),
                         row(4,truth='pause',allowed=False,session='bad')])
        unknown=r['per_truth_label']['unknown']
        self.assertEqual(unknown['coverage'],0)
        self.assertIsNone(unknown['accuracy_nonabstained'])
        self.assertIsNone(unknown['wrong_enter_rate_per_grant'])
        pause=r['per_truth_label']['pause']
        self.assertEqual(pause['wrong_enter_grants'],1)
        self.assertEqual(pause['wrong_enter_rate_per_grant'],1)
        self.assertEqual(pause['accepted'],0)
        self.assertIsNone(pause['accuracy_nonabstained'])
        self.assertEqual(r['per_test_session']['bad'],pause)
    def test_session_leakage(self):
        with self.assertRaisesRegex(ValueError,'session leakage'):self.run_eval([row(0,'calibration'),row(1,session='c')])
    def test_rgb_duplicate_leakage(self):
        with self.assertRaisesRegex(ValueError,'RGB leakage'):self.run_eval([row(0,'calibration'),row(0)])
    def test_template_source_leak(self):
        with self.assertRaisesRegex(ValueError,'session leaked'):self.run_eval([row(1,session='c')])
    def test_hash_viewport_build_fullframe(self):
        for change in [{'rgb_sha256':'0'*64},{'width':2},{'build_sha256':'c'*64},{'full_frame':False},{'rgb':b''}]:
            with self.subTest(change=change),self.assertRaises(ValueError):self.run_eval([row(0,'calibration'),replace(row(1),**change)])
    def test_template_hash_mismatch(self):
        r=Recognizer();r.template_sha256='c'*64
        with self.assertRaisesRegex(ValueError,'template hash'):evaluate([],r,build_sha256=B,width=1,height=1,template_sha256=T,template_sessions=[])
    def test_missing_provenance(self):
        with self.assertRaisesRegex(ValueError,'missing template'):self.run_eval([row(1)])
    def test_mixed_synthetic_real_rejected(self):
        with self.assertRaisesRegex(ValueError,'blend'):self.run_eval([row(0,'calibration'),replace(row(1),source='real-labelled')])
    def test_calibration_not_scored(self):
        r=Recognizer();result=evaluate([row(0,'calibration')],r,build_sha256=B,width=1,height=1,template_sha256=T,template_sessions=['c'])
        self.assertEqual(r.calls,0);self.assertEqual(result['test_frames'],0);self.assertIsNone(result['coverage']);self.assertIsNone(result['accuracy_nonabstained']);self.assertIsNone(result['wrong_enter_rate_all_test'])
    def test_invalid_prediction_enter_counted_not_hidden(self):
        r=self.run_eval([row(0,'calibration'),row(4,truth='battle',allowed=False)])
        self.assertEqual(r['wrong_enter_grants'],1);self.assertEqual(r['enter_contract_violations'],1)
    def test_wrong_screen_enter_even_if_truth_can_enter(self):
        r=self.run_eval([row(0,'calibration'),row(3,truth='lobby',allowed=True)])
        self.assertEqual(r['wrong_enter_grants'],1)

class LoaderTests(unittest.TestCase):
    def test_invalid_budget(self):
        from ui_calibration_eval import load_dataset
        for value in [-1,1.2,True,None]:
            with self.assertRaisesRegex(ValueError,'byte budget'):load_dataset('.',max_total_bytes=value)
    def test_manifest_plain_data_and_paths(self):
        import tempfile,json
        from pathlib import Path
        from dataclasses import asdict
        from ui_calibration_eval import load_dataset
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)/'data';root.mkdir();raw=row(1);entry=asdict(raw);entry.pop('rgb');entry['rgb_path']='frame.rgb'
            (root/'frame.rgb').write_bytes(raw.rgb)
            def write(): (root/'dataset.json').write_text(json.dumps({'schema':'tower-ui-dataset/1','samples':[entry]}))
            write();self.assertEqual(load_dataset(root)[0],raw)
            entry['rgb_sha256']='0'*64;write()
            with self.assertRaisesRegex(ValueError,'integrity'):load_dataset(root)
            entry['rgb_sha256']=raw.rgb_sha256
            (Path(tmp)/'outside.rgb').write_bytes(raw.rgb)
            for path in ['../outside.rgb',str(Path(tmp)/'outside.rgb')]:
                entry['rgb_path']=path;write()
                with self.assertRaises(ValueError):load_dataset(root)
            (root/'link.rgb').symlink_to(Path(tmp)/'outside.rgb');entry['rgb_path']='link.rgb';write()
            with self.assertRaisesRegex(ValueError,'escapes'):load_dataset(root)
            entry['rgb_path']='frame.rgb';entry['import']='os';write()
            with self.assertRaisesRegex(ValueError,'fields'):load_dataset(root)
    def test_pause_enter_counts_wrong(self):
        r=evaluate([row(0,'calibration'),row(1,truth='pause',allowed=False)],Recognizer(),build_sha256=B,width=1,height=1,template_sha256=T,template_sessions=['c'])
        self.assertEqual(r['wrong_enter_grants'],1)

if __name__=='__main__':unittest.main()
