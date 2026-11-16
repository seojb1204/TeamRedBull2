import pathlib
import tempfile
import unittest
from bot_evidence import validate

class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.path=pathlib.Path(self.tmp.name)/'r.tsv'
        self.report={'schema':'towerAutoRun/2','evidence.schema':'towerBattleEvidence/1',
            'evidence.track':'logic-qa/instrumented-input','saveRootIsolated':'true',
            'result':'AUTORUN_REPORT_COMPLETE','evidence.droppedEvents':'0',
            'evidence.damageTaken':'60','evidence.healing':'20','evidence.damageDealt':'10',
            'evidence.parries':'1','battle.parries':'1','evidence.requestedInputs':'1','evidence.committedInputs':'1'}
        self.events='seconds,kind,detail,amount\n0,damage_taken,a,30\n1,heal,potion,20\n2,damage_taken,a,30\n3,damage_dealt,J,10\n4,parry,a,0\n5,input_requested,Q,0\n5,input_committed,Q,0\n'
    def run_check(self):
        self.path.write_text(''.join(k+'\t'+v+'\n' for k,v in self.report.items()))
        pathlib.Path(str(self.path)+'.events.csv').write_text(self.events)
        return validate(self.path)
    def test_full_evidence(self):
        self.assertEqual(self.run_check()['totals']['damage_taken'],60)
    def test_net_damage_rejected(self):
        self.report['evidence.damageTaken']='40'
        with self.assertRaises(ValueError):self.run_check()
    def test_truncated_rejected(self):
        self.report['evidence.droppedEvents']='1'
        with self.assertRaises(ValueError):self.run_check()
    def test_nonfinite_rejected(self):
        self.events=self.events.replace('a,30','a,NaN',1)
        with self.assertRaises(ValueError):self.run_check()
    def test_time_reversal_rejected(self):
        self.events=self.events.replace('2,damage_taken','0,damage_taken')
        with self.assertRaises(ValueError):self.run_check()
    def test_unisolated_rejected(self):
        self.report['saveRootIsolated']='false'
        with self.assertRaises(ValueError):self.run_check()
    def test_missing_committed_rejected(self):
        self.events=self.events.replace('5,input_committed,Q,0\n','')
        with self.assertRaises(ValueError):self.run_check()
    def test_wrong_parry_count(self):
        self.report['battle.parries']='2'
        with self.assertRaises(ValueError):self.run_check()
    def test_report_path_not_followed(self):
        self.report['evidence.eventsFile']='/not/authorized'
        self.assertTrue(self.run_check()['verified'])
if __name__=='__main__':unittest.main()
