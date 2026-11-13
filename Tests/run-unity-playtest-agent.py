"""Launch an existing Unity executable, never an editor or a user's project. No real save root.
Each invocation has a fresh isolated root, reused only across that campaign's rounds.
This does NOT claim that headless results are Unity results.
"""
import argparse, csv, json, pathlib, subprocess, tempfile, hashlib
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--exe',type=pathlib.Path,required=True)
p.add_argument('--output',type=pathlib.Path,required=True)
p.add_argument('--boss',type=int,choices=range(5),default=0)
p.add_argument('--profile',choices=['novice','balanced','expert'],default='balanced')
p.add_argument('--seed',type=int,default=17)
p.add_argument('--rounds',type=int,default=3)
p.add_argument('--seconds',type=int,default=60)
p.add_argument('--no-dda',action='store_true')
a=p.parse_args()
exe=a.exe.resolve();out=a.output.resolve()
if not exe.is_file():p.error('Executable not found')
if not 1<=a.rounds<=50 or not 5<=a.seconds<=600:p.error('rounds 1..50, seconds 5..600 required')
out.mkdir(parents=True,exist_ok=True)
# Never accept a user-supplied save root. Keep evidence, not delete it automatically.
root=pathlib.Path(tempfile.mkdtemp(prefix='tower-unity-agent-'))
rows=[]
for i in range(a.rounds):
    report=out/f'round-{i+1}.tsv';log=out/f'round-{i+1}.log'
    if report.exists() or log.exists():raise SystemExit('Use a new output directory; refusing to overwrite prior evidence')
    cmd=[str(exe),'-towerAgent',a.profile,'-towerSeed',str(a.seed+i),'-towerBoss',str(a.boss),
         '-towerDataRoot',str(root),'-towerReport',str(report),'-towerAutoSeconds',str(a.seconds),
         '-towerFrameRate','60','-logFile',str(log)]
    if a.no_dda:cmd+=['-towerNoDda']
    result=subprocess.run(cmd,cwd=exe.parent,timeout=a.seconds+90)
    if result.returncode!=0 or not report.is_file():raise SystemExit(f'Round {i+1}: executable/report failure')
    row=dict(line.split('\t',1) for line in report.read_text(encoding='utf-8-sig').splitlines() if '\t' in line)
    if row.get('result')!='AUTORUN_REPORT_COMPLETE' or row.get('saveRootIsolated')!='true':raise SystemExit('Incomplete/unisolated report')
    if row.get('agent.profile')!=a.profile or row.get('schema')!='towerAutoRun/2':raise SystemExit('Wrong/old build')
    rows.append(row)
(out/'campaign.json').write_text(json.dumps({'engine':'Unity executable','exe':str(exe),'exe_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),
    'save_root':str(root),'rounds':rows,'limits':['instrumented state agent, not visual AI or human','frame timing and VFX may differ between runs','no claim of player enjoyment or 60-80% convergence']},ensure_ascii=False,indent=2),encoding='utf-8')
print('Unity campaign complete:',out/'campaign.json')
