using System;using System.IO;using System.Linq;using System.Reflection;using ReversalTower;using UnityEngine;
public static class AdaptationColdStartTests
{
 static int n;static void Check(bool b,string name){if(!b)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
 static TutorialAssessment Assessment(string group,float confidence=.75f)=>new TutorialAssessment{completed=true,valid=true,isFirstAssessment=true,group=group,confidence=confidence,score=group=="novice"?.2f:.95f,activeDurationSeconds=60,ultimateExecutionSuccess=true,parryAttempts=3,parrySuccesses=group=="novice"?0:3,dodgeAttempts=3,dodgeSuccesses=group=="novice"?0:3,completedAtUtc="2026-11-18T01:00:00Z"};
 static CommunityStats.Entry Entry(int players=3,int opportunities=20,int level=-1,string group=null)=>new CommunityStats.Entry{boss="arakus",pattern="cleave",players=players,opportunities=opportunities,suggestedLevel=level,successRate=.2f,group=group};
 static DdaBattleSession Prepare(SaveData s){var d=new DdaBattleSession();d.Prepare(s,Catalog.Bosses[0],false);return d;}
 static void Clear(){CommunityStats.Replace(new CommunityStats.Entry[0]);CommunityStats.ReplaceGroups(new CommunityStats.Entry[0]);}
 static void Fill(DdaBattleSession d,float success,int count=100){var s=d.Stats[0];s.opportunities=count;s.parries=(int)Math.Round(count*success);s.hits=count-s.parries;}
 static TutorialAssessment Complete(int parryFails,int dodgeFails,int chargeParries,float active=1,float reading=0,float paused=0)
 {
  var t=new TutorialSession(false);long id=1;t.Tick(reading,false);t.Tick(paused,true);for(int i=0;i<6;i++)t.Acknowledge();
  foreach(var action in new[]{TutorialAction.Q,TutorialAction.W,TutorialAction.E,TutorialAction.Move,TutorialAction.Jump,TutorialAction.Attack,TutorialAction.Dash}){t.Acknowledge();t.Tick(active,false);t.ConfirmAction(action,true);}
  t.Acknowledge();t.ConfirmAction(TutorialAction.Potion,true,10);t.Acknowledge();t.Acknowledge();
  for(int i=0;i<parryFails;i++)t.ResolveThreat(id++,true,true,false,true);t.ResolveThreat(id++,true,true,true,false);
  t.Acknowledge();for(int i=0;i<dodgeFails;i++)t.ResolveThreat(id++,false,true,false,true);t.ResolveThreat(id++,false,true,true,false);
  t.Acknowledge();for(int i=0;i<chargeParries;i++)t.ResolveThreat(id++,true,true,true,false);
  t.GaugeChanged(100);t.Acknowledge();t.ConfirmAction(TutorialAction.Ultimate,true);t.ConfirmEmpoweredSwordHit(true,true);return t.Assessment;
 }
 public static int Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"adaptation-case-"+Guid.NewGuid().ToString("N"));SaveStore.UseIsolatedRoot(root);
  try{
   Clear();foreach(var group in new[]{"novice","normal","expert"}){var save=new SaveData{tutorialAssessment=Assessment(group)};var d=Prepare(save);Check(d.Stats[0].level==(group=="novice"?-1:group=="expert"?1:0),"empty DB uses actual tutorial conservative prior "+group);Check(d.Sources["cleave"]=="튜토리얼"&&save.Learning("arakus").battles==0&&save.Learning("arakus").model.vectors.All(x=>x==0),"tutorial prior never fabricates model learning "+group);}
   var weak=new SaveData{tutorialAssessment=Assessment("expert",.49f)};Check(Prepare(weak).Stats[0].level==0,"low-confidence assessment stays neutral");
   weak.tutorialAssessment.skipped=true;Check(Prepare(weak).Sources["cleave"]=="기본값","skip never grants expert prior");
   CommunityStats.Replace(new[]{Entry(players:1,level:1)});var first=new SaveData{tutorialAssessment=Assessment("novice")};Check(Prepare(first).Stats[0].level==1&&Prepare(first).Sources["cleave"]=="전체 DB","whole-data fallback wins when group absent");
   CommunityStats.ReplaceGroups(new[]{Entry(players:2,group:"novice")});Check(Prepare(first).Sources["cleave"]=="전체 DB","two anonymous group profiles are insufficient");
   CommunityStats.ReplaceGroups(new[]{Entry(opportunities:19,group:"novice")});Check(Prepare(first).Sources["cleave"]=="전체 DB","nineteen group opportunities insufficient");
   CommunityStats.ReplaceGroups(new[]{Entry(group:"novice")});var grouped=Prepare(first);Check(grouped.Stats[0].level==-1&&grouped.Sources["cleave"]=="그룹 DB","sufficient matching cohort wins over whole statistics");Check(grouped.Trace.patterns[0].priorMock&&grouped.Reasons["cleave"].Contains("모의"),"fixture provenance and real sample counts disclosed");
   Check(grouped.Stats.Skip(1).All(s=>s.level==-1),"missing cohort patterns independently use tutorial prior");
   var expert=new SaveData{tutorialAssessment=Assessment("expert")};Check(Prepare(expert).Sources["cleave"]=="전체 DB","different cohort cannot be used");
   var running=Prepare(first);int assigned=running.Stats[0].level;CommunityStats.ReplaceGroups(new[]{Entry(level:1,group:"novice")});Check(running.Stats[0].level==assigned,"late group receive cannot mutate active assignment");
   Fill(running,1);running.Complete(first,Catalog.Bosses[0],true);var later=Prepare(first);Check(later.Stats[0].level==0&&later.Sources["cleave"].Contains("개인"),"after evidence personal choose steps upward independent of new cohort");
   var memory=first.Learning("arakus").Pattern("cleave");memory.level=-2;memory.success=1;first.Learning("arakus").battles=0;Check(Prepare(first).Stats[0].level==-1,"defensive legacy observations cannot be overwritten by first-meeting group rule");
   var off=new SaveData{dda=false,tutorialAssessment=Assessment("expert")};Check(Prepare(off).Stats.All(s=>s.level==0),"off remains zero with valid expert prior");
   Clear();var great=new SaveData();for(int i=0;i<30;i++){var d=Prepare(great);Fill(d,1);d.Complete(great,Catalog.Bosses[0],true);Check(d.Trace.patterns[0].learned&&d.Trace.patterns[0].aDelta>0&&d.Trace.patterns[0].bDelta>0,"actual learning high-response battle "+i);}
   Check(Prepare(great).Stats[0].level==2,"thirty perfect clear battles reach +2");
   var loss=new SaveData();for(int i=0;i<10;i++){var d=Prepare(loss);Fill(d,1);d.Complete(loss,Catalog.Bosses[0],false);}Check(Prepare(loss).Stats[0].level==-2&&Prepare(loss).Reasons["cleave"].Contains("연패 보호"),"perfect counters plus repeated losses reproduce intentional protection at -2");
   for(int i=0;i<10;i++){var d=Prepare(loss);Fill(d,1);d.Complete(loss,Catalog.Bosses[0],true);}Check(Prepare(loss).Stats[0].level==2,"winning recovery releases loss protection");
   var novice=new SaveData();for(int i=0;i<30;i++){var d=Prepare(novice);Fill(d,.2f);d.Complete(novice,Catalog.Bosses[0],true);}Check(Prepare(novice).Stats[0].level==-2,"thirty low-response battles ease to -2");
   var locked=new SaveData();var l=locked.Learning("arakus");l.battles=10;var m=l.Pattern("cleave");m.level=-2;m.success=.85f;m.observations=20;var x=l.model.Context(m.success,l.globalSuccess,m.level,0,0);for(int i=0;i<500;i++)l.model.Update(-2,x,BanditModel.Reward(.85f));
   var stuck=Prepare(locked);Check(stuck.Stats[0].level==-2&&stuck.Reasons["cleave"].Contains("LinUCB"),"85 percent can prefer well-trained easy arm under unchanged reward policy");
   Check(stuck.Trace.patterns[0].candidateScores[0]>stuck.Trace.patterns[0].candidateScores[1],"score trace identifies why the easier arm wins");
   var sparse=new SaveData();var no=Prepare(sparse);no.Complete(sparse,Catalog.Bosses[0],true);Check(sparse.Learning("arakus").battles==0&&no.Trace.patterns[0].skipReason=="NO_OBSERVATIONS","fast unobserved kill is diagnosed as no learning");
   var g=new TowerGame{save=new SaveData{tutorialOnboardingVersion=1}};typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());g.StartBattle(0);g.Stat("cleave").level=1;g.BossPatterns.BeginPattern(g.spec.patterns[0]);var live=g.DdaSnapshot();
   Check(live.level==1&&live.windup==g.BossPatterns.windup&&live.damage==g.BossPatterns.patternDamage&&live.cooldown==g.BossPatterns.Applied.cooldown,"F8 model reads actual BeginPattern applied values");
   g.Stat("cleave").level=-2;Check(g.DdaSnapshot().level==1,"current attack snapshot cannot be confused with later table mutation");g.Stat("cleave").level=1;g.mode=ScreenMode.Result;Check(!g.DdaSnapshot().hasAttack,"result screen never presents stale attack as current");
   Check(Directory.GetFiles(Path.Combine(root,"DdaApplied"),"*.csv").Any(f=>File.ReadAllText(f).Contains(live.windup.ToString("R",System.Globalization.CultureInfo.InvariantCulture))),"applied CSV contains actual source numeric value");
   var t=new TutorialSession(false);t.Tick(30,false);for(int i=0;i<6;i++)t.Acknowledge();t.Acknowledge();t.Tick(10,true);t.Tick(5,false);var preview=t.Preview();Check(preview.elapsedDurationSeconds==45&&preview.activeDurationSeconds==5&&preview.explanationSeconds==30&&preview.pausedSeconds==10,"tutorial elapsed reading pause and active clocks partition correctly");Check(!t.Assessment.valid&&!t.Assessment.completed&&!preview.valid,"live preview does not finalize or train assessment");
   foreach(var kind in new[]{"novice","normal","expert"}){var groups=new System.Collections.Generic.Dictionary<string,int>();for(int i=0;i<30;i++){var a=kind=="novice"?Complete(8+i%3,8+i%2,0):kind=="expert"?Complete(0,0,2):Complete(1,1,2);if(!groups.ContainsKey(a.group))groups[a.group]=0;groups[a.group]++;Check(a.group==kind,"actual tutorial rules simulated "+kind+" "+i);}Console.WriteLine("TUTORIAL_GROUP_COUNTS "+kind+" "+string.Join(",",groups.Select(x=>x.Key+"="+x.Value)));}
   var fast=Complete(0,0,2);var slow=Complete(0,0,2,100);var reader=Complete(0,0,2,1,300,120);Check(fast.score>slow.score,"active clear time changes actual score");Check(fast.score==reader.score&&reader.elapsedDurationSeconds>fast.elapsedDurationSeconds,"reading and pause increase elapsed but never lower score");var scarce=Complete(0,0,0);Check(scarce.group=="normal"&&scarce.confidence<.5,"perfect but sparse tutorial evidence cannot certify expert");
   Console.WriteLine("ADAPTATION_COLDSTART_PASS "+n);return 0;
  }finally{Clear();SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}
 }
}
