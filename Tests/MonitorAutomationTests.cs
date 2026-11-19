// Actual AutoRunArgs and DdaPanelInput source slices + actual CloudSync/SaveStore.
// Unity event/coroutine/HTTP APIs are test doubles. Never performs real HTTP.
using System;using System.IO;using System.Linq;using System.Collections;using System.Reflection;using ReversalTower;using UnityEngine;using UnityEngine.Networking;
namespace UnityEngine {
 public enum EventType{KeyDown}public enum KeyCode{F8,F9}
 public class Event{public static Event current;public EventType type;public KeyCode keyCode;public bool used;public void Use(){used=true;}}
}
namespace ReversalTower {
 public partial class TowerGame {
  const string DdaPanelPref="DdaPanelVisible";bool ddaPanelLoaded,showDdaPanel,ddaHistoryExpanded=true;
  void ResetDdaPanelWindow(){} public bool Panel=>showDdaPanel;public bool Graph=>ddaHistoryExpanded;
  public void Press(KeyCode key){Event.current=new Event{type=EventType.KeyDown,keyCode=key};DdaPanelInput();}
 }
}
class MonitorAutomationTests {
 static int n;static void Check(bool x,string label){if(!x)throw new Exception(label);Console.WriteLine("PASS "+label);n++;}
 static object Call(object c,string name,params object[] values)=>c.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,values);
 static void Drain(IEnumerator e){while(e.MoveNext())if(e.Current is IEnumerator child)Drain(child);}
 public static int Main(string[] args){
 string root=Path.Combine(Path.GetTempPath(),"monitor-guard-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
 // Every engine path points to brand-new test directories BEFORE any save/config access.
 Application.persistentDataPath=Path.Combine(root,"normal");Application.streamingAssetsPath=Path.Combine(root,"streaming");Directory.CreateDirectory(Application.streamingAssetsPath);Directory.CreateDirectory(Application.persistentDataPath);
 try{
 bool automatic=args.Contains("-towerDataRoot")||args.Contains("-towerNoDda");
 Check(!AutoRunArgs.Active,"raw process classification checked before Parse");
 Check(AutoRunArgs.IsAutomationProcess==automatic,"monitor alone is ordinary, mixed flags remain automation before Awake");
 foreach(string flag in new[]{"-towerDemo","-towerCapture","-towerAutoSeconds","-towerDataRoot","-towerReport","-towerFrameRate","-towerNoDda","-towerLobbySmoke","-towerAgent","-towerSeed","-towerBoss","-towerPixelAgent","-towerFutureUnknown","-towerDdaMonitor=bad"}){
 Check(AutoRunArgs.HasAutomationArguments(new[]{"game",flag}),"existing/future automation retained "+flag);
 Check(AutoRunArgs.HasAutomationArguments(new[]{"game","-towerDdaMonitor",root,flag}),"mixed monitor preserves guard "+flag);
 }
 if(args.Contains("-towerDdaMonitor")&&!automatic)Check(!AutoRunArgs.HasAutomationArguments(new[]{"game","-towerDdaMonitor",root}),"exact monitor flag exemption");
 Check(!AutoRunArgs.HasAutomationArguments(new[]{"game"}),"unflagged ordinary play");
 var early=new TowerGame();early.Press(KeyCode.F8);Check(early.Panel==!automatic,"actual F8 raw guard before Parse preserves automation protection");bool earlyGraph=early.Graph;early.Press(KeyCode.F9);Check(early.Graph==(automatic?earlyGraph:!earlyGraph),"actual F9 raw guard before Parse");if(!automatic)early.Press(KeyCode.F8);
 var cloud=new CloudSync();
 File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"supabase.json"),"{\"url\":\"https://invalid.local\",\"anonKey\":\"mock-only\"}");
 // BEFORE Parse: prove CloudSync boot-order gate follows raw process flags.
 Call(cloud,"Awake");
 var cfg=typeof(CloudSync).GetField("config",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(cloud);
 Check((cfg==null)==automatic,"CloudSync Awake ordinary config read / automation config untouched");
 Check(UnityWebRequest.Requests.Count==0,"Awake coroutine stub never makes network requests");
 if(!automatic){
 Check(!(bool)cfg.GetType().GetField("proficiencySync").GetValue(cfg),"new proficiency HTTP is OFF when config field omitted");
 var profile=SaveStore.Load();
 Call(cloud,"OnBattle",new HistoryEntry{battleId=Guid.NewGuid().ToString(),boss="arakus",seconds=30,dda=true},new System.Collections.Generic.List<PatternStats>{new PatternStats{id="cleave",level=0,opportunities=20,parries=10}},profile);
 var sync=(IEnumerator)Call(cloud,"SyncAll");Drain(sync);
 Check(UnityWebRequest.Requests.Count(r=>r.method=="POST")==3,"existing players/battles/pattern_stats upload through mock HTTP");
 Check(UnityWebRequest.Requests.Any(r=>r.url.EndsWith("/players"))&&UnityWebRequest.Requests.Any(r=>r.url.EndsWith("/battles"))&&UnityWebRequest.Requests.Any(r=>r.url.EndsWith("/pattern_stats")),"legacy upload endpoints unchanged");
 Check(UnityWebRequest.Requests.Any(r=>r.method=="GET"&&r.url.Contains("pattern_difficulty")),"ordinary play receives existing global DB through mocked HTTP");
 Check(!UnityWebRequest.Requests.Any(r=>r.url.Contains("proficiency")||r.url.Contains("group")),"OFF does not request proficiency RPC or group aggregate");
 Console.WriteLine("HTTP_SEQUENCE "+string.Join("|",UnityWebRequest.Requests.Select(r=>r.method+" "+r.url)));
 UnityWebRequest.Next=UnityWebRequest.Result.ConnectionError;Drain((IEnumerator)Call(cloud,"SyncAll"));
 Check(CommunityStats.Status.Contains("수신 실패"),"ordinary legacy DB failure remains diagnosed with or without monitor");
 }
 else Check(CommunityStats.Status.Contains("네트워크 꺼짐"),"automation retains network-off status");
 AutoRunArgs.Parse();
 Check(AutoRunArgs.Active==automatic,"Parse activation agrees for valid automation / monitor-only / no flags");
 Check(AutoRunArgs.IsAutomationProcess==automatic,"raw guard stable after Parse");
 Check((AutoRunArgs.DataRoot!=null)==automatic,"monitor does not create automation save isolation; real automation does");
 if(automatic){Check(Path.GetFullPath(AutoRunArgs.DataRoot)!=Path.GetFullPath(Application.persistentDataPath),"automation save root differs from normal");Check(!File.Exists(Path.Combine(AutoRunArgs.DataRoot,"progress.json")),"automation startup does not load normal progress");}
 var game=new TowerGame();game.Press(KeyCode.F8);Check(game.Panel==!automatic,"actual F8 input toggles only ordinary play");bool graph=game.Graph;game.Press(KeyCode.F9);Check(game.Graph==(automatic?graph:!graph),"actual F9 input toggles only with ordinary panel open");
 Call(cloud,"OnDestroy");
 Console.WriteLine("MONITOR_AUTOMATION_PASS "+n+" actual classification/F8/F9/CloudSync, mocked Unity/HTTP; no production DB");return 0;
 }finally{SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}}
}
