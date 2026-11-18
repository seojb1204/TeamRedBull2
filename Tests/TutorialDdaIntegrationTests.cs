using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ReversalTower;
using UnityEngine;
public static class TutorialDdaIntegrationTests
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static object Call(TowerGame g,string name,params object[] args)=>typeof(TowerGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,args);
    static void ReachUltimate(TutorialSession s,bool extraParries=false)
    {
        for(int i=0;i<6;i++)s.Acknowledge();
        foreach(var a in new[]{TutorialAction.Q,TutorialAction.W,TutorialAction.E,TutorialAction.Move,TutorialAction.Jump,TutorialAction.Attack,TutorialAction.Dash}){s.Acknowledge();s.Tick(1,false);s.ConfirmAction(a,true);}
        s.Acknowledge();s.ConfirmAction(TutorialAction.Potion,true,10);s.Acknowledge();s.Acknowledge();
        s.ResolveThreat(101,true,true,true,false);s.Acknowledge();s.ResolveThreat(102,false,true,true,false);s.Acknowledge();
        if(extraParries){s.ResolveThreat(103,true,true,true,false);s.ResolveThreat(104,true,true,true,false);}
        s.GaugeChanged(100);s.Acknowledge();
    }
    public static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"tower-integrated-tutorial-"+Guid.NewGuid().ToString("N"));SaveStore.UseIsolatedRoot(root);
        int logged=0;Action<HistoryEntry,System.Collections.Generic.List<PatternStats>,SaveData> listener=(h,s,p)=>logged++;
        SaveStore.BattleLogged+=listener;
        try
        {
            var original=SaveStore.Load();original.Learning("arakus").model.Init();SaveStore.Save(original);
            string originalFile=File.ReadAllText(SaveStore.SavePath),learning=JsonUtility.ToJson(original.learning),id=original.cloudPlayerId;
            var g=new TowerGame{save=original};typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());
            g.StartTutorial(false);Check(g.TutorialActive&&!ReferenceEquals(g.save,original),"practice detaches real profile");
            SaveData refused;Check(!SaveStore.TryResetProfile(original,g.mode==ScreenMode.Battle,out refused)&&ReferenceEquals(refused,original),"profile switch refused during tutorial battle");
            ReachUltimate(g.Tutorial,true);Check(g.Tutorial.Step==TutorialStep.Ultimate&&!g.Tutorial.Finished,"actual session reaches ultimate without invented assessment DTO");
            Check(File.ReadAllText(SaveStore.SavePath)==originalFile&&JsonUtility.ToJson(original.learning)==learning&&logged==0,"practice does not save progression train DDA or emit cloud battle events");
            g.player=new Vector2(-1,0);g.boss=new Vector2(0,0);g.ult=100;
            Call(g,"BeginAction","R",.85f);Call(g,"TickAction",.3f);
            Check(g.SwordCharged&&g.TutorialActive&&!g.Tutorial.Finished,"actual R activation alone does not finish tutorial");
            Call(g,"TickAction",.6f);Call(g,"BeginAction","J",.35f);Call(g,"TickAction",.2f);
            Check(!g.TutorialActive&&g.mode==ScreenMode.Lobby,"actual empowered dummy collision commits tutorial and returns lobby");
            var assessment=g.save.tutorialAssessment;
            Check(assessment!=null&&assessment.valid&&assessment.completed&&!assessment.skipped&&assessment.isFirstAssessment&&assessment.ultimateExecutionSuccess,"actual completed first assessment preserved");
            Check(g.save.cloudPlayerId==id&&JsonUtility.ToJson(g.save.learning)==learning&&logged==0,"completion preserves identity and DDA excludes tutorial battle");
            string first=JsonUtility.ToJson(assessment);
            Check(JsonUtility.ToJson(SaveStore.Load().tutorialAssessment)==first,"first result safely persisted");
            string profile;Check(TutorialAssessmentLabAdapter.TryProfile(assessment,out profile)&&profile=="expert","actual expert group explicitly maps to lab input only");
            var single=new TutorialSession(false);ReachUltimate(single);single.ConfirmAction(TutorialAction.Ultimate,true);single.ConfirmEmpoweredSwordHit(true,true);
            Check(single.Assessment.group=="normal"&&TutorialAssessmentLabAdapter.TryProfile(single.Assessment,out profile)&&profile=="balanced","product normal explicitly maps to experimental balanced");
            single.Assessment.group="balanced";Check(!TutorialAssessmentLabAdapter.TryProfile(single.Assessment,out profile),"product balanced spelling is not silently accepted as normal");
            g.StartTutorial(true);g.Tutorial.Skip();Call(g,"FinishTutorial");
            Check(JsonUtility.ToJson(g.save.tutorialAssessment)==first&&g.save.tutorialReplayAssessment.skipped&&!g.save.tutorialReplayAssessment.isFirstAssessment,"replay skip cannot overwrite initial result");
            Check(!TutorialAssessmentLabAdapter.TryProfile(g.save.tutorialReplayAssessment,out profile),"skip cannot become proficiency evidence");
            g.StartTutorial(true);ReachUltimate(g.Tutorial);g.Tutorial.ConfirmAction(TutorialAction.Ultimate,true);g.Tutorial.ConfirmEmpoweredSwordHit(true,true);Call(g,"FinishTutorial");
            Check(JsonUtility.ToJson(g.save.tutorialAssessment)==first&&g.save.tutorialReplayAssessment.valid&&!g.save.tutorialReplayAssessment.isFirstAssessment,"successful reassessment remains separate from first result");
            string disk=File.ReadAllText(SaveStore.SavePath);g.StartTutorial(true);g.Tutorial.Acknowledge();g.ExitTutorial();
            Check(File.ReadAllText(SaveStore.SavePath)==disk&&JsonUtility.ToJson(g.save.tutorialAssessment)==first,"interrupted reassessment preserves disk and initial assessment");
            // Changing only tutorial group must not introduce a new difficulty policy.
            CommunityStats.Replace(new[]{new CommunityStats.Entry{boss="arakus",pattern="cleave",opportunities=20,players=12,successRate=.95f,suggestedLevel=-1}});
            var d=new DdaBattleSession();d.Prepare(g.save,Catalog.Bosses[0],false);var levels=d.Stats.Select(x=>x.level).ToArray();
            var detached=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));detached.tutorialAssessment.group="novice";var other=new DdaBattleSession();other.Prepare(detached,Catalog.Bosses[0],false);
            Check(levels.SequenceEqual(other.Stats.Select(x=>x.level))&&d.Stats[0].level==-1,"tutorial category does not change existing shared cold-start policy");
            CommunityStats.Replace(new CommunityStats.Entry[0]);g.StartBattle(0);g.DamageBoss(g.bossHp);
            Check(logged==1&&!string.IsNullOrEmpty(g.result.snapshotId),"first real boss emits one normal event and restorable checkpoint");
            string snapshot=g.result.snapshotId;var completed=g.save;
            SaveData reset,restored;Check(SaveStore.TryResetProfile(completed,false,out reset),"post-tutorial new profile reset works");
            Check(reset.cloudPlayerId!=id&&reset.tutorialAssessment==null&&reset.tutorialReplayAssessment==null&&reset.tutorialOnboardingVersion==0,"new UUID starts fresh onboarding without inherited skill evidence");
            Check(SaveStore.TryRestoreProfile(reset,snapshot,false,out restored),"completed profile restore succeeds");
            Check(restored.cloudPlayerId==id&&restored.tutorialOnboardingVersion==1&&JsonUtility.ToJson(restored.tutorialAssessment)==first&&restored.tutorialReplayAssessment.valid&&!restored.tutorialReplayAssessment.isFirstAssessment,"restore preserves original UUID first result and separate replay evidence");
            // Exercise the real Update edge, not only FinishTutorial via reflection.
            SaveStore.UseIsolatedRoot(Path.Combine(root,"retry-good"));var retryOriginal=SaveStore.Load();
            var retryGame=new TowerGame{save=retryOriginal};typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(retryGame,new Soundscape());
            retryGame.StartTutorial(false);retryGame.Tutorial.Skip();string blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"fixture");SaveStore.UseIsolatedRoot(blocked);Call(retryGame,"FinishTutorial");
            Check(retryGame.TutorialActive&&retryGame.Tutorial.Finished,"failed completion remains retryable through Update");
            SaveStore.UseIsolatedRoot(Path.Combine(root,"retry-good"));Input.Clear();Input.down.Add(KeyCode.Return);Call(retryGame,"Update");Input.Clear();
            Check(!retryGame.TutorialActive&&retryGame.mode==ScreenMode.Lobby,"same Enter used for completion retry cannot also start a boss battle");
            Console.WriteLine("TUTORIAL_DDA_INTEGRATION_PASS "+checks+" real session action collision save and DDA; no Unity or DB");return 0;
        }
        finally{SaveStore.BattleLogged-=listener;CommunityStats.Replace(new CommunityStats.Entry[0]);SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}
    }
}
