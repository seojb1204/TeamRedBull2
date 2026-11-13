#!/usr/bin/env python3
"""Bounded Windows pixel-agent campaign. Starts only the specified isolated build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from pixel_agent import atomic_json


def validate_build(exe):
    exe = Path(exe).resolve()
    if not exe.is_file() or exe.suffix.lower()!='.exe':
        raise ValueError('Specify an existing experimental Windows .exe')
    data=exe.with_name(exe.stem+'_Data')
    # Never read the real DB config. Preserve art/audio so pixels match normal assets.
    streaming=data/'StreamingAssets'
    if (streaming/'supabase.json').exists():
        raise ValueError('Refusing DB-configured build; use an isolated build without supabase.json')
    assembly=data/'Managed'/'Assembly-CSharp.dll'
    if not assembly.is_file():
        raise ValueError('Requires Mono experimental build with inspectable Assembly-CSharp.dll')
    blob=assembly.read_bytes()
    for marker in ('-towerPixelAgent','자동검증: 네트워크 꺼짐'):
        if marker.encode('utf-16-le') not in blob:
            raise ValueError('Missing pixel/network isolation marker; refusing old/unrelated build')
    return exe,hashlib.sha256(blob).hexdigest()


def stop_owned(process):
    # No name-based kill, global taskkill, editor closure or unrelated process cleanup.
    if process is not None and process.poll() is None:
        process.terminate()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill();process.wait(timeout=5)


def campaign(args):
    if os.name!='nt':
        raise ValueError('Actual game launch is supported on Windows only; synthetic tests run anywhere')
    exe,assembly_hash=validate_build(args.exe)
    out=Path(args.output).resolve()
    # New directory only: prevents overwriting existing evidence/checkpoint.
    out.mkdir(parents=True,exist_ok=False)
    runroot=Path(tempfile.mkdtemp(prefix='tower-pixel-campaign-'))
    checkpoint=Path(args.checkpoint).resolve() if args.checkpoint else out/'checkpoint.json'
    if args.policy=='learned' and not args.train and not checkpoint.is_file():
        raise ValueError('Learned evaluation requires --checkpoint from an earlier training run')
    rows=[]
    summary={'schema':'tower-pixel-campaign/1','exe':str(exe),
             'exe_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),
             'assembly_sha256':assembly_hash,'isolated_root':str(runroot),
             'policy':args.policy,'training':args.train,'dda':False,
             'initial_checkpoint_sha256':hashlib.sha256(checkpoint.read_bytes()).hexdigest() if checkpoint.is_file() else None,
             'observation':'Actual rendered RGB only; environment rewards/termination for learning/scoring',
             'episodes':rows,'status':'running'}
    atomic_json(out/'campaign.json',summary)
    try:
        for i in range(args.episodes):
            seed=args.seed+i
            data=runroot/f'episode-{i+1}'
            bridge=data/'bridge';bridge.mkdir(parents=True)
            result=out/f'episode-{i+1}.json'
            command=[str(exe),'-towerPixelAgent',str(bridge),'-towerDataRoot',str(data),
                     '-towerNoDda','-towerAutoSeconds',str(args.seconds),'-towerBoss','0',
                     '-towerSeed',str(seed),'-towerFrameRate','60',
                     '-screen-fullscreen','0','-screen-width','960','-screen-height','540',
                     '-logFile',str(out/f'episode-{i+1}-unity.log')]
            client_command=[sys.executable,str(Path(__file__).with_name('pixel_agent.py')),
                            '--bridge',str(bridge),'--policy',args.policy,'--seed',str(seed),
                            '--max-seconds',str(args.seconds+15),'--timeout',str(args.seconds),
                            '--report',str(result)]
            if args.policy=='learned':client_command+=['--checkpoint',str(checkpoint)]
            if args.train:client_command+=['--train','--epsilon',str(args.epsilon)]
            game=client=None
            with (out/f'episode-{i+1}-client.log').open('w',encoding='utf-8') as client_log:
                try:
                    game=subprocess.Popen(command,cwd=exe.parent,stdin=subprocess.DEVNULL)
                    client=subprocess.Popen(client_command,stdout=client_log,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL)
                    client_code=client.wait(timeout=args.seconds+25)
                    if client_code!=0:
                        raise RuntimeError(f'Episode {i+1}: client failed ({client_code}); see client log')
                    game_code=game.wait(timeout=15)
                    if game_code!=0:
                        raise RuntimeError(f'Episode {i+1}: game failed ({game_code}); see Unity log')
                finally:
                    stop_owned(client);stop_owned(game)
            if not result.is_file():raise RuntimeError('Missing client report')
            row=json.loads(result.read_text(encoding='utf-8'))
            if not row.get('completed'):raise RuntimeError('Episode did not reach a terminal/truncated transition')
            final=json.loads((bridge/'transition.json').read_text(encoding='utf-8-sig'))
            row['game_outcome']=final.get('reason','unknown')
            rows.append(row)
            atomic_json(out/'campaign.json',summary)
        summary['status']='complete'
    except BaseException as error:
        summary['status']='failed';summary['error']=str(error)
        raise
    finally:
        atomic_json(out/'campaign.json',summary)
    print('Pixel campaign complete:',out/'campaign.json')


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--exe',required=True,type=Path)
    p.add_argument('--output',required=True,type=Path)
    p.add_argument('--policy',choices=['random','rule','learned'],default='random')
    p.add_argument('--train',action='store_true')
    p.add_argument('--checkpoint',type=Path)
    p.add_argument('--episodes',type=int,default=1)
    p.add_argument('--seconds',type=int,default=180)
    p.add_argument('--seed',type=int,default=17)
    p.add_argument('--epsilon',type=float,default=.3)
    a=p.parse_args()
    if not 1<=a.episodes<=3 or not 10<=a.seconds<=180 or not 0<=a.epsilon<=1:
        p.error('episodes 1..3, seconds 10..180, epsilon 0..1 required')
    if a.train and a.policy!='learned':p.error('--train requires --policy learned')
    campaign(a)

if __name__=='__main__':main()
