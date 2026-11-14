using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ReversalTower;
using UnityEngine;
public static class DdaFrequencyTests
{
    static int count;static TowerGame g;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static void Start(){AutoRunArgs.DisableDda=false;g=new TowerGame{save=new SaveData()};typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());g.StartBattle(0);}
    public static int Main()
    {
        SaveStore.UseIsolatedRoot(Path.Combine(Path.GetTempPath(),"dda-frequency-"+Guid.NewGuid().ToString("N")));
        string reason;var s=new PatternStats{opportunities=3,hits=3};
        Check(DdaPatternFrequency.Multiplier(s,true,false,out reason)==1,"cold sample neutral");
        s.opportunities=s.hits=4;float small=DdaPatternFrequency.Multiplier(s,true,false,out reason);
        Check(small>1&&small<1.15f,"four failures only mild boost");
        s.opportunities=s.hits=100;float bad=DdaPatternFrequency.Multiplier(s,true,false,out reason);
        Check(bad>small&&bad<=1.35f,"bounded evidence boost");
        Check(DdaPatternFrequency.Multiplier(s,false,false,out reason)==1,"disabled neutral");
        Check(DdaPatternFrequency.Multiplier(s,true,true,out reason)==1,"low HP neutral");
        s.hits=0;s.dodges=100;Check(DdaPatternFrequency.Multiplier(s,true,false,out reason)==1,"success not boosted");
        s.hits=100;Check(DdaPatternFrequency.Multiplier(s,true,false,out reason)==1,"invalid counters ignored");
        for(int n=0;n<100;n++){s=new PatternStats{opportunities=n,hits=n};var w=DdaPatternFrequency.Multiplier(s,true,false,out reason);if(w<1||w>1.35f)throw new Exception("range");}Check(true,"100 samples bounded");
        Start();g.PlayerHit(10,true,"cleave",5,false);Check(g.Stat("cleave").opportunities==0&&g.Stat("cleave").ignored==1,"outside not dodge");
        g.PlayerHit(10,true,"cleave",5,false,false,true);Check(g.Stat("cleave").dodges==1,"explicit threat escape observed");
        g.playerInv=1;g.dashTime=0;g.PlayerHit(10,true,"cleave",5);Check(g.Stat("cleave").opportunities==1,"posthit invulnerability not skill");
        g.dashTime=.2f;g.PlayerHit(10,true,"cleave",5);Check(g.Stat("cleave").dodges==2,"overlap dash dodge observed");
        Start();g.player=new Vector2(8,0);g.hazards.Add(new TowerGame.Hazard{x=0,width=2,delay=.5f,life=1,damage=10,pattern="cleave"});g.BossPatterns.TickHazards(.25f);g.BossPatterns.TickHazards(.3f);Check(g.Stat("cleave").opportunities==0,"safe throughout hazard ignored");
        Start();g.player=new Vector2(0,0);g.hazards.Add(new TowerGame.Hazard{x=0,width=2,delay=.5f,life=1,damage=10,pattern="cleave"});g.BossPatterns.TickHazards(.1f);g.player.x=5;g.BossPatterns.TickHazards(.5f);Check(g.Stat("cleave").dodges==1,"leave warning area counts once");g.BossPatterns.TickHazards(.1f);Check(g.Stat("cleave").dodges==1,"hazard no duplicate outcome");
        Start();g.player=new Vector2(0,0);g.hazards.Add(new TowerGame.Hazard{x=0,width=2,delay=.5f,life=1,damage=10,pattern="cleave"});g.BossPatterns.TickHazards(.1f);g.BossPatterns.TickHazards(.5f);Check(g.Stat("cleave").hits==1&&g.Stat("cleave").dodges==0,"stay in warning hit");
        Start();g.player=new Vector2(-5,0);g.boss=new Vector2(0,0);g.BossPatterns.BeginPattern(g.spec.patterns[0]);g.BossPatterns.TickBoss(.1f);g.BossPatterns.ExecutePattern();Check(g.Stat("cleave").opportunities==0,"far melee ignored");
        Start();g.player=new Vector2(1,0);g.boss=new Vector2(0,0);g.BossPatterns.BeginPattern(g.spec.patterns[0]);g.BossPatterns.TickBoss(.1f);g.player.x=9;g.BossPatterns.ExecutePattern();Check(g.Stat("cleave").dodges==1,"melee leave threat observed");
        Start();g.seconds=5;g.player.x=1;g.boss.x=0;g.BossPatterns.SetTestSeed(10);
        g.Stat("cleave").opportunities=g.Stat("cleave").hits=20;g.BossPatterns.SelectPattern(1);
        var evidence=g.BossPatterns.SelectionLog;Check(evidence.Count>0&&Math.Abs(evidence.Sum(e=>e.probability)-1)<.0001,"weights normalized");
        Check(evidence.Single(e=>e.id=="cleave").weight>g.spec.patterns[0].weight,"weak pattern boosted");
        Check(evidence.All(e=>!string.IsNullOrEmpty(e.reason)),"reasons recorded");
        var cooldowns=(Dictionary<string,float>)typeof(BossPatternSystem).GetField("cooldowns",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g.BossPatterns);
        string last="";for(int i=0;i<150;i++){g.seconds+=100;cooldowns.Clear();g.BossPatterns.SelectPattern(1);Check(g.BossPatterns.SelectionLog.Count<=128,"bounded log "+i);last=g.BossPatterns.current.id;}
        var recent=(Queue<string>)typeof(BossPatternSystem).GetField("recentSelections",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g.BossPatterns);
        recent.Clear();recent.Enqueue("cleave");recent.Enqueue("cleave");cooldowns.Clear();g.BossPatterns.SelectPattern(1);
        Check(g.BossPatterns.current.id!="cleave","recent overloaded pattern excluded if alternative");
        Check(g.BossPatterns.SelectionLog.Any(e=>e.id=="cleave"&&e.weight==0&&e.reason.Contains("repetition")),"repeat exclusion logged");
        Check(g.BossPatterns.SelectionLog.Count(e=>e.decision==g.BossPatterns.SelectionLog.Last().decision&&e.selected)==1,"one selected decision logged");
        Start();g.save.dda=false;g.StartBattle(0);g.seconds=5;g.Stat("cleave").opportunities=g.Stat("cleave").hits=100;g.BossPatterns.SelectPattern(1);
        Check(g.BossPatterns.SelectionLog.All(e=>e.reason.Contains("off")),"battle disabled remains base");
        Start();g.seconds=0;g.BossPatterns.SelectPattern(1);Check(g.BossPatterns.current.id==g.spec.patterns[0].id,"eligible authored opening preserved");
        Start();g.seconds=0;g.BossPatterns.SelectPattern(100);Check(g.BossPatterns.current==null||g.BossPatterns.current.id!="cleave","opening cannot bypass range gate");
        g.BossPatterns.Reset();Check(g.BossPatterns.SelectionLog.Count==0,"reset clears battle evidence");
        Console.WriteLine("DDA_FREQUENCY_PASSED "+count+" checks; no Unity runtime or human balance evidence");return 0;
    }
}
