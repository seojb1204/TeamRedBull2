// Test-only: copy into Assets only in an isolated verification build.
// No OS input injection, foreground activation, production save paths or network calls.
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Text;
using UnityEngine;
using ReversalTower;
public sealed class UnityTutorialBatchProbe:MonoBehaviour
{
 TowerGame g;readonly StringBuilder log=new StringBuilder();int passed,battleEvents;string output;
 static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Launch(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-towerTutorialBatchProbe")>=0)new GameObject("Isolated tutorial batch probe").AddComponent<UnityTutorialBatchProbe>();}
 IEnumerator Start()
 {
  yield return null;
  output=Arg("-towerBatchEvidence");
  try
  {
   if(!Application.isBatchMode||SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null||string.IsNullOrEmpty(Arg("-towerDataRoot"))||!SaveStore.IsIsolated||string.IsNullOrEmpty(output))throw new InvalidOperationException("Batch/null graphics/isolated data/evidence path required");
   g=UnityEngine.Object.FindAnyObjectByType<TowerGame>();if(!g)throw new Exception("TowerGame missing");g.enabled=false;
   SaveStore.BattleLogged+=Battle;
   Run();log.AppendLine("RESULT "+passed+" PASS / 0 FAIL");Write();Application.Quit(0);
  }
  catch(Exception e){log.AppendLine("FAIL "+e);Write();Application.Quit(1);}
  finally{SaveStore.BattleLogged-=Battle;}
 }
 void Battle(HistoryEntry h,System.Collections.Generic.List<PatternStats> s,SaveData data){battleEvents++;}
 void Write(){if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));File.WriteAllText(output,log.ToString());}
 void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;log.AppendLine("PASS "+name);}
 void Call(string name,params object[] args){typeof(TowerGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,args);}
 void Act(string name,float duration){g.Tutorial.Acknowledge();Call("BeginAction",name,duration);Call("TickAction",duration+.01f);if(g.TutorialActive)Call("TickTutorialOpponent",0f);}
 void Run()
 {
  log.AppendLine("Unity="+Application.unityVersion+" graphics="+SystemInfo.graphicsDeviceType+"; actual Unity gameplay methods, no OS input/UI pixel claim");
  g.save=new SaveData();SaveStore.Save(g.save);string id=g.save.cloudPlayerId;g.mode=ScreenMode.Title;g.StartGame();
  Check(g.TutorialActive&&g.Tutorial.Step==TutorialStep.Introduction,"fresh profile enters tutorial");
  float seconds=g.seconds,hp=g.playerHp,ult=g.ult;Call("Update");Check(g.seconds==seconds&&g.playerHp==hp&&g.ult==ult,"explanation freezes real battle counters");
  for(int i=0;i<6;i++)g.Tutorial.Acknowledge();
  Act("Q",.7f);Check(g.Tutorial.Step==TutorialStep.W&&g.ult==15,"actual Q cast charges once and advances after animation");
  Act("W",.9f);Act("E",1.3f);Check(g.ult==45&&g.Tutorial.Step==TutorialStep.Move,"actual W E actions charge and advance");
  // These three control-stage events are injected; keyboard/movement input is explicitly not tested.
  foreach(var a in new[]{TutorialAction.Move,TutorialAction.Jump}){g.Tutorial.Acknowledge();g.Tutorial.ConfirmAction(a,true);}
  Act("J",.35f);g.Tutorial.Acknowledge();g.Tutorial.ConfirmAction(TutorialAction.Dash,true);
  g.playerHp=65;Act("F",.9f);Check(g.playerHp>65&&g.Tutorial.Step==TutorialStep.UltimateExplanation,"actual potion healing advances");
  g.Tutorial.Acknowledge();g.Tutorial.Acknowledge();
  g.parryAge=99;Call("TickTutorialOpponent",2.2f);Check(g.Tutorial.Step==TutorialStep.Parry&&g.Tutorial.Assessment.parryAttempts==1,"real threat failed parry repeats");
  g.parryAge=.1f;Call("TickTutorialOpponent",2.2f);Check(g.Tutorial.Step==TutorialStep.Dodge&&g.parries==1,"valid timed parry advances and gives actual feedback");
  g.Tutorial.Acknowledge();g.parryAge=99;Call("TickTutorialOpponent",2.2f);Check(g.Tutorial.Step==TutorialStep.Dodge,"failed dodge repeats");
  Call("TickTutorialOpponent",1.2f);g.player.x+=3;Call("TickTutorialOpponent",1f);Check(g.Tutorial.Step==TutorialStep.Charge,"leaving real marked threat advances dodge");
  g.Tutorial.Acknowledge();g.ult=85;Call("TickTutorialOpponent",1.2f);g.parryAge=.1f;Call("TickTutorialOpponent",1f);Call("TickTutorialOpponent",0f);
  Check(g.ult==100&&g.Tutorial.Step==TutorialStep.Ultimate,"real parry completes charge and opens R guide");
  g.Tutorial.Acknowledge();g.player=new Vector2(2,0);g.boss=new Vector2(3,0);g.face=1;
  Call("BeginAction","R",.85f);Call("TickAction",.3f);Check(g.SwordCharged&&g.TutorialActive,"R activates sword without fake completion");
  Call("TickAction",.6f);Call("BeginAction","J",.35f);Call("TickAction",.2f);
  Check(!g.TutorialActive&&g.mode==ScreenMode.Lobby,"actual charged sword collision completes and returns lobby");
  Check(g.save.gold==0&&g.save.history.Count==0&&g.save.learning.Count==0&&g.save.cloudPlayerId==id,"tutorial preserves identity and gives no progression or learning");
  Check(battleEvents==0&&!Directory.Exists(Path.Combine(SaveStore.Root,"Battles")),"tutorial emits no ordinary battle or CSV");
  var loaded=SaveStore.Load();Check(loaded.tutorialAssessment.completed&&loaded.tutorialAssessment.ultimateExecutionSuccess&&loaded.tutorialOnboardingVersion==1,"actual JsonUtility disk reload preserves assessment");
  var original=g.save.tutorialAssessment;g.StartTutorial(true);g.Tutorial.Skip();Call("FinishTutorial");
  Check(g.save.tutorialAssessment.completed&&g.save.tutorialAssessment.completedAtUtc==original.completedAtUtc&&g.save.cloudPlayerId==id,"replay skip preserves first assessment and identity");
 }
}
