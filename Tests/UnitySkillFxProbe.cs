// Copy into Assets ONLY in an isolated verification copy; not a production runtime script.
#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using ReversalTower;
public sealed class UnitySkillFxProbe : MonoBehaviour
{
 string output;TowerGame game;SkillEffectVisuals fx;int passed;
 static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Boot(){string path=Arg("-towerSkillFxProbe");if(path!=null){var o=new GameObject("Isolated SkillFx Probe");o.AddComponent<UnitySkillFxProbe>().output=path;}}
 IEnumerator Start()
 {
  if(!AutoRunArgs.IsAutomationProcess||!SaveStore.IsIsolated)throw new Exception("Probe requires isolated automation save + DB exclusion");
  Directory.CreateDirectory(output);var run=Run();
  while(true){object step;bool more;try{more=run.MoveNext();step=run.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"FAILED.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}if(!more)yield break;yield return step;}
 }
 void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Debug.Log("SKILL_FX_CHECK "+name);}
 void Action(string name,float duration)=>typeof(TowerGame).GetMethod("BeginAction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{name,duration});
 void Prepare(){game.StartBattle(0);game.showHelp=false;game.paused=false;game.showSettings=false;game.player=new Vector2(-2,0);game.boss=new Vector2(4,0);game.bossStagger=99;game.face=1;game.save.tutorialSeen=true;}
 IEnumerator WaitBattle(float seconds){float target=game.seconds+seconds;while(game.seconds<target)yield return null;yield return new WaitForEndOfFrame();}
 IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
 IEnumerator Run()
 {
  yield return null;game=UnityEngine.Object.FindFirstObjectByType<TowerGame>();Check(game!=null,"TowerGame loaded");
  typeof(TowerGame).GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,false);Prepare();yield return null;yield return new WaitForEndOfFrame();
  fx=game.scenePresentation.GetComponent<SkillEffectVisuals>();Check(fx!=null&&SkillEffectVisuals.HasLanding,"FX renderer and landing texture available");
  int n=fx.EmittedCount;Action("W",.9f);yield return WaitBattle(.25f);Check(fx.VisibleCount==1&&fx.EmittedCount==n+1,"W emitted once");yield return Shot("01-W");
  game.paused=true;float time=game.seconds;yield return new WaitForSecondsRealtime(.15f);Check(game.seconds==time&&fx.EmittedCount==n+1,"pause freezes without duplicate");game.paused=false;
  yield return WaitBattle(.9f);Check(fx.VisibleCount==0,"W tail expired");
  Action("E",1.3f);yield return WaitBattle(.72f);Check(fx.VisibleCount==1,"E impact visible");yield return Shot("02-E");yield return WaitBattle(1.3f);Check(fx.VisibleCount==0,"E tail expired");
  Action("E",1.3f);yield return WaitBattle(.42f);Check(fx.VisibleCount==1,"E windup visible");game.actionTime=0;yield return null;yield return new WaitForEndOfFrame();Check(fx.VisibleCount==0,"cancel before contact removes windup");
  Prepare();yield return null;game.bossStagger=0;game.playerInv=20;game.playerHp=game.maxHp;game.boss=new Vector2(3,0);
  Pattern slam=Array.Find(game.spec.patterns,p=>p.id=="slam");Check(slam!=null,"real slam pattern exists");game.BossPatterns.BeginPattern(slam);n=fx.EmittedCount;
  float timeout=Time.realtimeSinceStartup+15;while(fx.EmittedCount==n&&Time.realtimeSinceStartup<timeout)yield return null;
  Check(fx.EmittedCount==n+1&&game.boss.y<=.001f,"real slam emits on grounded contact");yield return Shot("03-Landing");
  game.bossStagger=99;yield return WaitBattle(1.3f);Check(fx.VisibleCount==0,"landing tail expired");
  Prepare();yield return null;game.boss=new Vector2(0,0);game.bossHp=1;Action("E",1.3f);
  while(game.mode==ScreenMode.Battle)yield return null;yield return new WaitForEndOfFrame();Check(game.mode==ScreenMode.Result&&fx.VisibleCount>0,"killing blow retains FX in Result");yield return Shot("04-E-kill");
  yield return new WaitForSecondsRealtime(1.5f);Check(fx.VisibleCount==0,"result tail expires");game.ReturnLobby();yield return null;Check(fx.VisibleCount==0,"lobby clear");
  Prepare();yield return null;n=fx.EmittedCount;Action("W",.9f);yield return WaitBattle(.25f);Check(fx.EmittedCount==n+1,"restart W replays once");
  File.WriteAllText(Path.Combine(output,"PASSED.txt"),"SKILL_FX_UNITY_PASS "+passed+" checks. Diagnostic setup/state injection; not human play evidence.");yield return new WaitForSecondsRealtime(.2f);Application.Quit(0);
 }
}
#endif
