"""Reproduce CC BY 4.0 loop edits from locally downloaded official MP3s.
Usage: python Tools/build_lobby_music.py /path/to/mp3-directory
Requires ffmpeg and numpy. Does not download or overwrite source recordings.
"""
import sys,subprocess,wave,hashlib,json,uuid
from pathlib import Path
import numpy as np
TRACKS=[('lobby','Lost Frontier','USUAN1300039',36)]
out=Path(__file__).resolve().parents[1]/'Assets/StreamingAssets/Audio';report=[]
for boss,title,isrc,start in TRACKS:
 src=Path(sys.argv[1])/(title+'.mp3')
 raw=subprocess.check_output(['ffmpeg','-v','error','-ss',str(start),'-i',str(src),'-t','64','-f','f32le','-ac','2','-ar','32000','-'])
 x=np.frombuffer(raw,dtype='<f4').reshape(-1,2).copy();assert len(x)==64*32000
 n=2*32000;t=np.linspace(0,1,n)[:,None]
 seam=x[-n:]*(1-t)+x[:n]*t
 x=np.concatenate([x[n:-n],seam]);peak=np.max(np.abs(x));x*=min(.12/np.sqrt(np.mean(x*x)),.8/peak)
 pcm=np.rint(np.clip(x,-1,1)*32767).astype('<i2');dest=out/(boss+'.wav')
 with wave.open(str(dest),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(32000);w.writeframes(pcm.tobytes())
 meta=Path(str(dest)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 report.append(dict(boss=boss,title=title,artist='Kevin MacLeod',isrc=isrc,license='CC BY 4.0',source='https://incompetech.com/music/royalty-free/index.html?isrc='+isrc,start_seconds=start,duration_seconds=len(x)/32000,sample_rate=32000,channels=2,peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),seam_step=float(np.max(np.abs(x[0]-x[-1]))),sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest()))
(out/'LOBBY_MUSIC_MANIFEST.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
