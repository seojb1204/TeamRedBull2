// Offline adapter. All decisions/learning/combat use the production C# source.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using ReversalTower;
using UnityEngine;
namespace DdaLab {
public sealed class Config {
 public string mode="combat",profile="balanced",scenario="stable",dda="both";
 public int seed=17,rounds=8,boss=0,phase=1; public float distance=4;public bool releaseCooldowns=false;
}
public static class Experiment {
 static readonly JsonSerializerOptions json=new JsonSerializerOptions{IncludeFields=true};
 static readonly MethodInfo update=typeof(TowerGame).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
 static void Set(TowerGame g,string n,object v)=>typeof(TowerGame).GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,v);
 static object Model(SaveData s,string boss){var m=s.Learning(boss).model;m.Init();return new{A=(double[])m.matrices.Clone(),b=(double[])m.vectors.Clone(),battles=s.Learning(boss).battles};}
 static string Profile(Config c,int round) {
  int stage=(round-1)*3/c.rounds;
  if(c.scenario=="improving")return new[]{"novice","balanced","expert"}[stage];
  if(c.scenario=="declining")return new[]{"expert","balanced","novice"}[stage];
  return c.scenario=="repeated_failures"?"novice":c.profile;
 }
 static object Next(SaveData s,BossSpec b)=>b.patterns.Select(p=>{double[] x;string why;int level=DdaBattleSession.Decide(s,b,p,s.dda,out x,out why);return new{pattern=p.id,level,context=x,reason=why};}).ToArray();
 static object[] Stats(List<PatternStats> stats)=>stats.Select(s=>(object)new{pattern=s.id,level=s.level,opportunities=s.opportunities,parries=s.parries,dodges=s.dodges,hits=s.hits,ignored=s.ignored,success=s.opportunities>0?(float?)s.Success:null,reward=s.opportunities>0?(double?)BanditModel.Reward(s.Success):null}).ToArray();
 public static int Main(string[] args) {
  var c=JsonSerializer.Deserialize<Config>(File.ReadAllText(args[0]),json);string dest=Path.GetFullPath(args[1]);
  if(c==null||c.rounds<1||c.rounds>100||c.boss<0||c.boss>4||c.seed<0||c.seed>1000000000||c.phase<1||c.phase>2||!float.IsFinite(c.distance)||c.distance<0||c.distance>20||!new[]{"combat","control","selection"}.Contains(c.mode)||!new[]{"novice","balanced","expert"}.Contains(c.profile)||!new[]{"stable","improving","declining","repeated_failures"}.Contains(c.scenario)||!new[]{"both","on","off"}.Contains(c.dda))throw new ArgumentException("Invalid experiment config");
  var results=new List<object>();var selections=new List<object>();var starts=new List<object>();
  string root=Path.Combine(Path.GetTempPath(),"tower-dda-lab-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);CommunityStats.Replace(new CommunityStats.Entry[0]);
  try {
   foreach(bool enabled in (c.dda=="both"?new[]{false,true}:new[]{c.dda=="on"})) {
    SaveStore.UseIsolatedRoot(Path.Combine(root,enabled?"on":"off"));
    var save=new SaveData{unlockedBoss=4,dda=enabled};var boss=Catalog.Bosses[c.boss];
    for(int round=1;round<=c.rounds;round++) {
     string profile=Profile(c,round);int seed=c.seed+round-1;var rng=new System.Random(seed);
     AutoRunArgs.Active=false;AutoRunArgs.DisableDda=!enabled;AutoRunArgs.AgentSeed=seed;
     AutoRunArgs.AgentProfile=c.mode=="combat"?profile:null;
     var before=Model(save,boss.id);List<PatternStats> stats;object assigned;string outcome;float seconds=0;int steps=0;
     if(c.mode=="control") {
      var session=new DdaBattleSession();session.Prepare(save,boss,false);stats=session.Stats;
      assigned=session.Stats.Select(s=>new{pattern=s.id,level=s.level,reason=session.Reasons[s.id],context=session.Contexts[s.id]}).ToArray();
      double chance=profile=="novice"?.3:profile=="expert"?.95:.7;
      if(c.scenario=="repeated_failures")chance=.1;
      foreach(var s in stats) {
       var p=boss.patterns.Single(p=>p.id==s.id);if(p.kind==AttackKind.Step||p.kind==AttackKind.Rage)continue;
       for(int i=0;i<20;i++){s.opportunities++;if(rng.NextDouble()<chance){if(rng.NextDouble()<.5)s.parries++;else s.dodges++;}else s.hits++;}
      }
      bool clear=c.scenario!="repeated_failures";session.Complete(save,boss,clear);outcome=clear?"synthetic_clear":"synthetic_loss";
     } else {
      var g=new TowerGame{save=save};Set(g,"sound",new Soundscape());Set(g,"demo",true);g.StartBattle(c.boss);
      var session=(DdaBattleSession)typeof(TowerGame).GetField("dda",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
      assigned=session.Stats.Select(s=>new{pattern=s.id,level=s.level,reason=session.Reasons[s.id],context=session.Contexts[s.id]}).ToArray();
      int lastDecision=0,lastSerial=0;
      Action capture=()=>{
       int serial=(int)typeof(BossPatternSystem).GetField("patternSerial",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g.BossPatterns);
       if(serial>lastSerial&&g.BossPatterns.current!=null){starts.Add(new{dda=enabled,round,seed,pattern=g.BossPatterns.current.id,serial,seconds=g.seconds,phase=g.BossPatterns.phaseTwo?2:1,distance=c.mode=="selection"?c.distance:Math.Abs(g.player.x-g.boss.x)});lastSerial=serial;}

       var fresh=g.BossPatterns.SelectionLog.Where(e=>e.decision>lastDecision).ToArray();
       foreach(var e in fresh)selections.Add(new{dda=enabled,round,seed,profile,decision=e.decision,pattern=e.id,selected=e.selected,eligible=e.weight>0,seconds=e.seconds,baseWeight=e.baseWeight,multiplier=e.multiplier,weight=e.weight,probability=e.probability,reason=e.reason,opportunities=e.opportunities,hits=e.hits,parries=e.parries,dodges=e.dodges,ignored=e.ignored,phase=g.BossPatterns.phaseTwo?2:1,distance=c.mode=="selection"?c.distance:Math.Abs(g.player.x-g.boss.x),conditionSample=c.mode=="selection"?"controlled":"frame-end sample, not exact gate time"});
       if(fresh.Length>0)lastDecision=fresh.Max(e=>e.decision);
      };
      if(c.mode=="combat") {
       Time.unscaledDeltaTime=1f/60;Input.Clear();
       while(g.mode==ScreenMode.Battle&&steps<60*120){steps++;update.Invoke(g,null);Input.Clear();capture();if(!float.IsFinite(g.playerHp)||!float.IsFinite(g.bossHp))throw new Exception("Nonfinite combat state");}
       outcome=g.mode==ScreenMode.Result?(g.result.clear?"clear":"death"):"timeout_abandoned";
       if(g.mode==ScreenMode.Battle)g.Abandon();
      } else {
       g.seconds=5;g.player.x=c.distance;g.boss.x=0;g.BossPatterns.phaseTwo=c.phase==2;g.BossPatterns.SetTestSeed(seed);
       var weak=g.Stat(boss.patterns[0].id);weak.opportunities=weak.hits=20;
       var cooldowns=(Dictionary<string,float>)typeof(BossPatternSystem).GetField("cooldowns",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g.BossPatterns);
       for(int i=0;i<100;i++){g.seconds=5+i*.5f;if(c.releaseCooldowns)cooldowns.Clear();g.BossPatterns.SelectPattern(c.distance);capture();}
       outcome="selection_probe_no_learning";
      }
      stats=g.stats;seconds=g.seconds;
     }
     var after=Model(save,boss.id);var next=Next(save,boss);
     if(!SaveStore.Save(save))throw new IOException(SaveStore.LastError);
     var restored=SaveStore.Load();bool same=JsonSerializer.Serialize(after,json)==JsonSerializer.Serialize(Model(restored,boss.id),json)&&JsonSerializer.Serialize(next,json)==JsonSerializer.Serialize(Next(restored,boss),json);
     if(!same)throw new Exception("Model/next assignment changed after isolated save restore");
     results.Add(new{dda=enabled,round,seed,profile,outcome,seconds,steps,assigned,stats=Stats(stats),before,after,next,restored=same});save=restored;
    }
   }
   File.WriteAllText(dest,JsonSerializer.Serialize(new{schemaVersion=1,engine="production C# / UnityShim / Catalog",config=c,battles=results,selections,starts,limits=new[]{"No Unity rendering, audio, real DB, player saves or CloudQueue.","Control mode injects outcomes; selection mode directly probes selector with injected failures.","Combat mode executes actual Update and privileged rule-agent inputs; not human adaptation evidence.","Improving/declining switch agent profiles across round thirds. Combat repeated_failures uses novice input, not forced death.","Selection rows cover candidates/repetition exclusions; starts also records forced Step/Rage starts sampled after each Update. Missing authored gates are not individually explained."}},json));
   Console.WriteLine("LAB_COMPLETED battles="+results.Count+" selectionRows="+selections.Count);return 0;
  } finally {AutoRunArgs.AgentProfile=null;AutoRunArgs.DisableDda=false;SaveStore.UseIsolatedRoot(null);CommunityStats.Replace(new CommunityStats.Entry[0]);Directory.Delete(root,true);}
 }
}
}
