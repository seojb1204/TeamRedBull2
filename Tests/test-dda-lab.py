"""Meaningful offline integration checks of the configurable C# experiment adapter/API.
Run with --dotnet PATH. No Unity or production DB/files.
"""
import argparse,importlib.util,json,tempfile,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args()
r=Path(__file__).resolve().parents[1];spec=importlib.util.spec_from_file_location('dda_lab',r/'Tools/DdaLab/lab.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
n=0
def check(ok,label):
 global n
 if not ok:raise AssertionError(label)
 n+=1;print('PASS',label,flush=True)
with tempfile.TemporaryDirectory(prefix='lab-integration-') as tmp:
 lab=mod.Lab(a.dotnet,Path(tmp))
 def run(**kwargs):
  job=lab.submit(kwargs,asynchronous=False);check(job['status']=='completed',str(kwargs))
  return json.loads((lab.data/job['id']/'result.json').read_text())
 # Same injected observations across ON/OFF, actual trained matrices and restore.
 data=run(mode='control',scenario='improving',rounds=6)
 off=[b for b in data['battles'] if not b['dda']];on=[b for b in data['battles'] if b['dda']]
 check(all(b['before']['A']==b['after']['A'] and b['before']['b']==b['after']['b'] for b in off),'DDA OFF never trains')
 check(any(b['before']['A']!=b['after']['A'] for b in on),'actual Complete updates A on evidence')
 check(all(x['stats'][0]['opportunities']==20 and x['stats'][0]['parries']==y['stats'][0]['parries'] for x,y in zip(off,on)),'paired synthetic inputs identical across DDA groups')
 check(all(b['restored'] for b in data['battles']),'isolated model and next assignment restore')
 again=run(mode='control',scenario='improving',rounds=6)
 check(again['manifest']['replay']['identical'],'full raw policy result same seed identical')
 changed=run(mode='control',scenario='improving',rounds=6,seed=42)
 check(changed['manifest']['policy_sha256']!=data['manifest']['policy_sha256'],'different seed changes actual observations')
 down=run(mode='control',scenario='declining',rounds=6,boss=1)
 check([b['profile'] for b in down['battles'] if b['dda']]==['expert','expert','balanced','balanced','novice','novice'],'declining profile schedule')
 fail=run(mode='control',scenario='repeated_failures',rounds=4,boss=2)
 check(all(b['outcome']=='synthetic_loss' for b in fail['battles']),'repeated failure is explicitly synthetic')
 for boss in range(5):
  combat=run(mode='combat',profile='novice' if boss%2 else 'expert',rounds=1,boss=boss)
  check(all(b['steps']>0 and b['outcome'] in ('clear','death','timeout_abandoned') for b in combat['battles']),'real Update combat boss '+str(boss))
  check(bool(combat['selections']),'natural selector evidence captured '+str(boss))
  check(len(combat['starts'])>=sum(s['selected'] for s in combat['selections']),'actual starts include weighted and forced patterns '+str(boss))
  if boss==0:
   repeat=run(mode='combat',profile='expert',rounds=1,boss=boss)
   check(repeat['manifest']['replay']['identical'],'combat replay exact')
 sel=run(mode='selection',rounds=1,phase=2,distance=4,releaseCooldowns=True)
 check(all(b['before']['A']==b['after']['A'] for b in sel['battles']),'selection probes never train injected evidence')
 check(any(s['multiplier']>1 for s in sel['selections'] if s['dda']),'actual weighted selector boost observed')
 check(all(s['phase']==2 and s['distance']==4 for s in sel['selections']),'controlled selection conditions recorded')
 # Reopening disk results supports replay across server restarts.
 reopened=mod.Lab(a.dotnet,Path(tmp));check(len(reopened.jobs)==len(lab.jobs),'saved run history restored')
 for invalid in [{'rounds':0},{'seed':True},{'mode':'fake'},{'distance':float('nan')},{'path':'/tmp/anything'},{'rounds':101}]:
  try:mod.validate(invalid)
  except ValueError:check(True,'reject invalid config '+str(invalid))
  else:raise AssertionError(invalid)
print('DDA_LAB_PASS',n,'checks')
