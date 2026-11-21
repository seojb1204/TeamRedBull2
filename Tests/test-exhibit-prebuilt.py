"""Actual self-contained engine checks, no SDK or Git on PATH. No DB access."""
import argparse,os,sys,json,tempfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools/DdaLab'))
import lab
p=argparse.ArgumentParser();p.add_argument('--engine',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();engine=a.engine.resolve();root=a.output.resolve();root.mkdir(parents=True,exist_ok=False)
os.environ['PATH']=str(root/'no-sdk-no-git');lab.ROOT=root/'no-checkout';lab.ROOT.mkdir()
first=lab.Lab(None,root/'results',prebuilt=engine);one=first.submit({'mode':'control','rounds':3},asynchronous=False);assert one['status']=='completed',one
second=lab.Lab(None,root/'results',prebuilt=engine);two=second.submit({'mode':'control','rounds':3},asynchronous=False);assert two['status']=='completed' and two['replay']['identical'],two
three=second.submit({'mode':'combat','rounds':1},asynchronous=False);assert three['status']=='completed',three
meta=json.loads((root/'results'/one['id']/'result.json').read_text(encoding='utf-8'))['manifest'];assert meta['source_commit']==first.engine_manifest['sourceCommit'];assert meta['source_sha256']==first.engine_manifest['source_sha256']
print('EXHIBIT_PREBUILT_PASS actual C# control/combat, no SDK/Git/check-out, restart, deterministic replay and compiled provenance. Host execution only, not native Windows.')
