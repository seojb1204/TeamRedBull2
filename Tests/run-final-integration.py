"""Offline integration regression entry point. No Unity launch, DB config, real saves or upload queue.
Invokes existing suites; no algorithm reimplementation or test suppression.
"""
from pathlib import Path
import argparse,subprocess,sys,json,hashlib
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
r=Path(__file__).resolve().parents[1];out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
commands=[
 ('logic',['Tests/run-arakus-polish.py','--suite','logic','--dotnet',a.dotnet]),
 ('dda-learning',['Tests/run-dda-learning-verification.py','--label','integrated','--output',str(out/'dda-learning'),'--dotnet',a.dotnet]),
 ('history',['Tests/run-dda-history.py','--suite','history','--dotnet',a.dotnet]),
 ('panel',['Tests/run-dda-history.py','--suite','panel','--dotnet',a.dotnet]),
 ('charge',['Tests/run-arakus-polish.py','--suite','polish','--dotnet',a.dotnet]),
 ('size-animation',['Tests/run-arakus-size.py','--dotnet',a.dotnet,'--output',str(out/'size')]),
 ('room-resources',['Tests/verify-arakus-room-integration.py','--output',str(out/'room')]),
 ('size-resources',['Tests/verify-arakus-size-art.py','--output',str(out/'size')]),
 ('charge-art',['Tests/verify-arakus-charge-art.py','--presentation-baseline','ccf9ba9313ef5050d3cdc75a46d162faf3f5bfaf'])]
results={}
for name,cmd in commands:
 result=subprocess.run([sys.executable,*cmd],cwd=r,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 (out/(name+'.log')).write_text(result.stdout);results[name]={'exit':result.returncode,'tail':result.stdout.splitlines()[-3:]}
 print(name,'PASS' if result.returncode==0 else 'FAIL',flush=True)
report={'commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),
 'source_sha256':{str(f.relative_to(r)):hashlib.sha256(f.read_bytes()).hexdigest() for f in (r/'Assets/Scripts').glob('*.cs')},
 'suites':results,'limits':['HEADLESS UnityShim and static/Pillow checks','No Unity API compile, Game View, Windows build or DB network test']}
(out/'final-suite-summary.json').write_text(json.dumps(report,indent=2))
raise SystemExit(any(result['exit']!=0 for result in results.values()))
