#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    // Experimental local file bridge. Policies receive only framebuffer pixels.
    // Reward/termination are privileged TRAINING signals, never policy observations.
    public sealed class PixelAgentBridge : MonoBehaviour
    {
        [Serializable] sealed class ActionMessage { public string episode; public int step=-1; public int action=-1; }
        [Serializable] sealed class Transition
        {
            public string schema="towerPixel/1",episode,frame,reason;
            public int step;
            public float reward;
            public bool terminated,truncated;
        }
        const int Width=160,Height=90,FramesPerAction=4;
        TowerGame game;
        string directory,episode,lastFrame;
        int step,framesRemaining,action,frameIndex;
        bool published,finished;
        float previousPlayerHp,previousBossHp;
        readonly System.Diagnostics.Stopwatch watch=System.Diagnostics.Stopwatch.StartNew();
        double waitStarted;

        static string Argument(string[] args,string key)
        {
            int i=Array.IndexOf(args,key);
            return i>=0&&i+1<args.Length?args[i+1]:null;
        }
        public static PixelAgentBridge AttachIfRequested(TowerGame game)
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-towerPixelAgent")<0)return null;
            // Fail closed: generic automation flags already protect CloudSync and preferences.
            string dir=Argument(args,"-towerPixelAgent"),root=Argument(args,"-towerDataRoot");
            if(string.IsNullOrWhiteSpace(dir)||string.IsNullOrWhiteSpace(root)||!SaveStore.IsIsolated||
                !AutoRunArgs.DisableDda||AutoRunArgs.AgentProfile!=null||AutoRunArgs.AgentBoss!=0||
                Argument(args,"-towerAutoSeconds")==null||AutoRunArgs.SecondsLimit<=0||AutoRunArgs.SecondsLimit>600)
                throw new InvalidOperationException("Pixel experiment requires isolated data root, no DDA, Arakus only, no oracle agent and wall limit <=600 seconds.");
            dir=Path.GetFullPath(dir);root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(!dir.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pixel exchange must be a child of the isolated run root.");
            Directory.CreateDirectory(dir);
            if(Directory.GetFileSystemEntries(dir).Length!=0)
                throw new InvalidOperationException("Pixel exchange directory must be empty; use a new run directory.");
            // A verified build should exclude these entirely, even though CloudSync is already disabled.
            if(File.Exists(Path.Combine(Application.streamingAssetsPath,"supabase.json")))
                throw new InvalidOperationException("Pixel experiment refuses a DB-configured build; keep art/audio but exclude supabase.json.");
            var bridge=game.gameObject.AddComponent<PixelAgentBridge>();
            bridge.game=game;bridge.directory=dir;bridge.episode=Guid.NewGuid().ToString("N");
            bridge.previousPlayerHp=game.playerHp;bridge.previousBossHp=game.bossHp;
            game.BossPatterns.SetTestSeed(AutoRunArgs.AgentSeed);
            Application.runInBackground=true;
            bridge.StartCoroutine(bridge.CaptureLoop());
            return bridge;
        }
        // Called by TowerGame.Update, not MonoBehaviour.Update: ordering is explicit.
        public bool BeforeTick()
        {
            if(finished)return false;
            if(watch.Elapsed.TotalSeconds>AutoRunArgs.SecondsLimit+3){finished=true;Application.Quit(2);return false;}
            if(game.mode!=ScreenMode.Battle)return false;
            if(watch.Elapsed.TotalSeconds>=AutoRunArgs.SecondsLimit||game.seconds>=60)return false;
            if(framesRemaining>0){frameIndex=FramesPerAction-framesRemaining;framesRemaining--;return true;}
            if(!published)return false;
            string path=Path.Combine(directory,"action.json");
            if(!File.Exists(path))return false;
            ActionMessage message;
            try
            {
                if(new FileInfo(path).Length>1024)throw new IOException("Oversized action");
                message=JsonUtility.FromJson<ActionMessage>(File.ReadAllText(path));
                if(message==null||message.episode!=episode||message.step!=step||message.action<0||message.action>6)return false;
            }
            catch(IOException){return false;}
            catch(ArgumentException){return false;}
            File.Delete(path);
            string oldTransition=Path.Combine(directory,"transition_"+step+".json");
            if(File.Exists(oldTransition))File.Delete(oldTransition);
            // The matching action acknowledges that the client consumed this frame.
            if(lastFrame!=null&&File.Exists(lastFrame))File.Delete(lastFrame);
            action=message.action;step++;published=false;framesRemaining=FramesPerAction-1;frameIndex=0;
            return true;
        }
        public void ApplyInput(ref float move,ref bool jump,ref bool dash,ref bool attack,ref bool parry,
            ref bool q,ref bool w,ref bool e,ref bool r,ref bool f)
        {
            move=action==1?-1:action==2?1:0;
            jump=frameIndex==0&&action==5;dash=frameIndex==0&&action==6;
            attack=frameIndex==0&&action==3;parry=frameIndex==0&&action==4;
            q=w=e=r=f=false;
        }
        IEnumerator CaptureLoop()
        {
            var endOfFrame=new WaitForEndOfFrame();
            while(!finished)
            {
                yield return endOfFrame;
                bool terminal=game.mode==ScreenMode.Result;
                bool truncated=!terminal&&(watch.Elapsed.TotalSeconds>=AutoRunArgs.SecondsLimit||game.seconds>=60||
                    (published&&watch.Elapsed.TotalSeconds-waitStarted>15));
                if(published&&truncated)
                {
                    // No action has been acknowledged: do not manufacture an RL transition.
                    File.WriteAllText(Path.Combine(directory,"bridge_error.json"),"{\"schema\":\"towerPixel/1\",\"error\":\"client_timeout_or_wall_limit\"}");
                    finished=true;Application.Quit(2);yield break;
                }
                if(published&&!truncated&&!terminal)continue;
                if(framesRemaining>0&&!terminal&&!truncated)continue;
                Texture2D capture=ScreenCapture.CaptureScreenshotAsTexture();
                if(capture==null){Debug.LogError("PixelAgent framebuffer capture failed");Application.Quit(2);yield break;}
                var pixels=capture.GetPixels32();
                var rgb=new byte[Width*Height*3];
                // Unity texture origin is bottom-left; PPM rows are top-down.
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
                {
                    var c=pixels[(capture.height-1-y*capture.height/Height)*capture.width+x*capture.width/Width];
                    int k=(y*Width+x)*3;rgb[k]=c.r;rgb[k+1]=c.g;rgb[k+2]=c.b;
                }
                Destroy(capture);
                string filename="observation_"+episode+"_"+step+".ppm";
                lastFrame=Path.Combine(directory,filename);
                using(var stream=File.Create(lastFrame+".tmp"))
                {
                    byte[] header=Encoding.ASCII.GetBytes("P6\n160 90\n255\n");
                    stream.Write(header,0,header.Length);stream.Write(rgb,0,rgb.Length);
                }
                ReplaceFile(lastFrame+".tmp",lastFrame);
                float reward=step==0?0:(previousBossHp-game.bossHp)/game.spec.hp-(previousPlayerHp-game.playerHp)/game.maxHp-.001f;
                if(terminal)reward+=game.result!=null&&game.result.clear?1:-1;
                previousBossHp=game.bossHp;previousPlayerHp=game.playerHp;
                var t=new Transition{episode=episode,step=step,frame=filename,reward=reward,terminated=terminal,truncated=truncated,
                    reason=terminal?(game.result!=null&&game.result.clear?"win":"loss"):truncated?"time_or_client_timeout":"running"};
                string state=Path.Combine(directory,"transition.json");
                string json=JsonUtility.ToJson(t);
                File.WriteAllText(state+".tmp",json);ReplaceFile(state+".tmp",state);
                string numbered=Path.Combine(directory,"transition_"+step+".json");
                File.WriteAllText(numbered+".tmp",json);ReplaceFile(numbered+".tmp",numbered);
                published=true;waitStarted=watch.Elapsed.TotalSeconds;
                if(terminal||truncated)
                {
                    finished=true;
                    // Give polling client time to read final state before process exit.
                    yield return new WaitForSecondsRealtime(2);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying=false;
#else
                    Application.Quit(0);
#endif
                }
            }
        }
        static void ReplaceFile(string temp,string destination)
        {
            for(int attempt=0;;attempt++)
            {
                try { if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);return; }
                catch(IOException) { if(attempt>=19)throw;System.Threading.Thread.Sleep(5); }
            }
        }
    }
}
#endif

