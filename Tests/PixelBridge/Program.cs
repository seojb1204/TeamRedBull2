using System;using System.Collections;using System.IO;using System.Reflection;using System.Text.Json;
namespace UnityEngine {
 public class MonoBehaviour {public GameObject gameObject=new GameObject();public Coroutine StartCoroutine(IEnumerator e){TestRuntime.loop=e;e.MoveNext();return null;}public static void Destroy(object o){}}
 public class Coroutine{} public class GameObject {public T AddComponent<T>() where T:new()=>new T();}
 public class WaitForEndOfFrame{} public class WaitForSecondsRealtime {public WaitForSecondsRealtime(float v){}}
 public struct Color32 {public byte r,g,b;}
 public enum TextureFormat {RGB24}
 public struct Rect {public Rect(int x,int y,int w,int h){}}
 public class RenderTexture {public static RenderTexture active;public static RenderTexture GetTemporary(int w,int h,int d)=>new RenderTexture();public static void ReleaseTemporary(RenderTexture t){}}
 public class Camera {public static Camera main=new Camera();public RenderTexture targetTexture;public void Render(){}}
 public class Texture2D {public Texture2D(){} public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;} public void ReadPixels(Rect r,int x,int y){}public void Apply(){}public int width=160,height=90;public Color32[] GetPixels32(){var a=new Color32[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)a[y*width+x]=new Color32{r=(byte)x,g=(byte)y,b=7};return a;}}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
 public static class ScreenCapture {public static Texture2D CaptureScreenshotAsTexture()=>new Texture2D();}
 public static class Application {public static string streamingAssetsPath="/path-not-present";public static bool runInBackground;public static void Quit(int n){TestRuntime.exit=n;}}
 public static class Debug {public static void LogError(string s){Console.WriteLine(s);}}
 public static class JsonUtility {static JsonSerializerOptions opts=new JsonSerializerOptions{IncludeFields=true};public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,opts);public static string ToJson<T>(T value)=>JsonSerializer.Serialize(value,opts);}
}
namespace ReversalTower {
 public enum ScreenMode{Battle,Result}
 public class TowerGame:UnityEngine.MonoBehaviour {public BossPatternSystem BossPatterns {get;}=new BossPatternSystem();public ScreenMode mode=ScreenMode.Battle;public float playerHp=100,bossHp=100,maxHp=100,seconds;public int hits,parries;public Spec spec=new Spec();public Result result;}
 public class Spec {public float hp=100;}public class Result {public bool clear;}
 public static class SaveStore {public static bool IsIsolated=true;}
 public static class AutoRunArgs {public static bool DisableDda=true;public static string AgentProfile;public static int AgentBoss,AgentSeed=17;public static float SecondsLimit=180;}
 public class BossPatternSystem{public void SetTestSeed(int seed){}}
}
public static class TestRuntime {public static IEnumerator loop;public static int exit=-1;}
class Program {
 static int checks;static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i<0?null:a[i+1];}
 static void Main(){
  string exchange=Arg("-towerPixelAgent");var game=new ReversalTower.TowerGame();
  string testCase=Arg("--case");
  if(testCase=="oracle")ReversalTower.AutoRunArgs.AgentProfile="balanced";
  if(testCase=="dda")ReversalTower.AutoRunArgs.DisableDda=false;
  if(testCase=="database"){
   UnityEngine.Application.streamingAssetsPath=Path.Combine(Arg("-towerDataRoot"),"fixture-assets");Directory.CreateDirectory(UnityEngine.Application.streamingAssetsPath);
   File.WriteAllText(Path.Combine(UnityEngine.Application.streamingAssetsPath,"supabase.json"),"TEST FIXTURE, NOT REAL CONFIG");
  }
  if(testCase!=null){
   bool rejected=false;try{ReversalTower.PixelAgentBridge.AttachIfRequested(game);}catch(InvalidOperationException){rejected=true;}
   Check(rejected,"reject invalid config: "+testCase);return;
  }
  var b=ReversalTower.PixelAgentBridge.AttachIfRequested(game);
  Check(!b.BeforeTick(),"no simulation before first observation");
  TestRuntime.loop.MoveNext();
  var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(exchange,"transition.json"))).RootElement;
  string episode=state.GetProperty("episode").GetString();
  Check(File.ReadAllText(Path.Combine(exchange,"transition_0.json"))==File.ReadAllText(Path.Combine(exchange,"transition.json")),"numbered initial transition matches summary");
  Check(state.GetProperty("step").GetInt32()==0&&state.GetProperty("reward").GetSingle()==0,"initial step and reward");
  Check(!state.TryGetProperty("playerHp",out _)&&!state.TryGetProperty("boss",out _),"no privileged observations");
  string metricsPath=Path.Combine(exchange,"episode_metrics.json");
  Check(!File.Exists(metricsPath)&&!state.TryGetProperty("bossDamage",out _)&&!state.TryGetProperty("playerDamage",out _)&&!state.TryGetProperty("hitsTaken",out _)&&!state.TryGetProperty("parries",out _),"metrics unavailable before terminal and excluded from transition");
  byte[] ppm=File.ReadAllBytes(Path.Combine(exchange,state.GetProperty("frame").GetString()));
  int header=System.Text.Encoding.ASCII.GetByteCount("P6\n160 90\n255\n");
  bool offscreen=Array.IndexOf(Environment.GetCommandLineArgs(),"-towerPixelOffscreen")>=0;
  Check(ppm.Length==header+160*90*3&&ppm[header+1]==(offscreen?103:89)&&ppm[^2]==(offscreen?3:0),"RGB top-down framebuffer");
  void Action(string ep,int step,int action){File.WriteAllText(Path.Combine(exchange,"action.json"),JsonSerializer.Serialize(new{episode=ep,step,action}));}
  Action("stale",0,3);Check(!b.BeforeTick(),"stale episode rejected");
  Action(episode,-1,3);Check(!b.BeforeTick(),"stale step rejected");
  Action(episode,0,7);Check(!b.BeforeTick(),"out-of-range action rejected");
  Action(episode,0,3);Check(b.BeforeTick(),"valid action accepted");
  Check(!File.Exists(Path.Combine(exchange,"transition_0.json")),"acknowledged numbered transition removed");
  float move=99;bool jump=true,dash=true,attack=false,parry=true,q=true,w=true,e=true,r=true,f=true;
  b.ApplyInput(ref move,ref jump,ref dash,ref attack,ref parry,ref q,ref w,ref e,ref r,ref f);
  Check(move==0&&attack&&!jump&&!dash&&!parry&&!q&&!w&&!e&&!r&&!f,"only chosen action; physical input cleared");
  Check(b.BeforeTick(),"second frame");b.ApplyInput(ref move,ref jump,ref dash,ref attack,ref parry,ref q,ref w,ref e,ref r,ref f);
  Check(!attack,"press action not repeated on held frames");
  Check(b.BeforeTick()&&b.BeforeTick()&&!b.BeforeTick(),"exactly four frames then freeze");
  game.bossHp=80;game.playerHp=90;TestRuntime.loop.MoveNext();
  state=JsonDocument.Parse(File.ReadAllText(Path.Combine(exchange,"transition.json"))).RootElement;
  Check(state.GetProperty("step").GetInt32()==1&&Math.Abs(state.GetProperty("reward").GetSingle()-.099f)<.00001,"reward transition matched to action");
  Check(Directory.GetFiles(exchange,"*.ppm").Length==1,"acknowledged frame removed");
  Check(File.Exists(Path.Combine(exchange,"transition_1.json")),"next numbered transition published");
  Action(episode,1,1);Check(b.BeforeTick(),"next step accepted");game.hits=2;game.parries=3;game.mode=ReversalTower.ScreenMode.Result;game.result=new ReversalTower.Result{clear=true};TestRuntime.loop.MoveNext();
  state=JsonDocument.Parse(File.ReadAllText(Path.Combine(exchange,"transition.json"))).RootElement;
  Check(state.GetProperty("terminated").GetBoolean()&&!state.GetProperty("truncated").GetBoolean(),"terminal state emitted even mid-repeat");
  var metrics=JsonDocument.Parse(File.ReadAllText(metricsPath)).RootElement;
  Check(metrics.GetProperty("schema").GetString()=="towerPixelMetrics/1"&&metrics.GetProperty("episode").GetString()==episode&&metrics.GetProperty("bossDamage").GetSingle()==20&&metrics.GetProperty("playerDamage").GetSingle()==10&&metrics.GetProperty("hitsTaken").GetInt32()==2&&metrics.GetProperty("parries").GetInt32()==3&&metrics.GetProperty("decisions").GetInt32()==2&&metrics.GetProperty("saveRootIsolated").GetBoolean()&&metrics.GetProperty("ddaDisabled").GetBoolean()&&!state.TryGetProperty("bossDamage",out _)&&!state.TryGetProperty("playerDamage",out _)&&!state.TryGetProperty("hitsTaken",out _)&&!state.TryGetProperty("parries",out _),"terminal metrics emitted separately without policy telemetry");
  Check(!b.BeforeTick(),"terminal simulation stopped");TestRuntime.loop.MoveNext();Check(TestRuntime.exit==0,"normal exit after final observation grace");
  var watchdog=new ReversalTower.PixelAgentBridge();ReversalTower.AutoRunArgs.SecondsLimit=-4;TestRuntime.exit=-1;
  Check(!watchdog.BeforeTick()&&TestRuntime.exit==2,"independent Update watchdog fails closed");
  Console.WriteLine("BRIDGE_SHIM_CHECKS "+checks+"; NOT Unity rendering validation");
 }
}
