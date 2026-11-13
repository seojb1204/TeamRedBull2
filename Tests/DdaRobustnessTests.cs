// Production DDA hardening regression. Synthetic observations, not a player study.
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using ReversalTower;
using UnityEngine;
public static class DdaRobustnessTests
{
    static int count;
    static void Check(string name,bool pass){if(!pass)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static DdaBattleSession Session(SaveData s){var d=new DdaBattleSession();d.Prepare(s,Catalog.Bosses[0],false);return d;}
    static void Fill(DdaBattleSession d){d.Stats[0].opportunities=10;d.Stats[0].parries=7;d.Stats[0].hits=3;}
    static string State(SaveData s)=>JsonUtility.ToJson(s.learning);
    public static int Main()
    {
        CommunityStats.Replace(new CommunityStats.Entry[0]);
        var b=Catalog.Bosses[0];var s=new SaveData{gold=321,unlockedBoss=3};s.cards.Add(new CardStack{id="unknown",count=2});
        s.history.Add(new HistoryEntry{boss="keep",gold=50});
        var l=s.Learning(b.id);l.battles=-3;l.lossStreak=-7;l.globalSuccess=float.NaN;
        l.patterns.Add(null);l.patterns.Add(new PatternMemory{id=b.patterns[0].id,level=99,success=float.PositiveInfinity,observations=-1});
        l.model.initialized=true; // malformed zero matrix with otherwise valid lengths
        s.learning.Add(null);s.NormalizeDda();
        Check("progression_history_preserved",s.gold==321&&s.unlockedBoss==3&&s.cards.Count==1&&s.history[0].boss=="keep");
        Check("null_entries_removed",s.learning.Count==1&&l.patterns.Count==1);
        Check("counters_repaired",l.battles==0&&l.lossStreak==0&&l.patterns[0].observations==0);
        Check("nonfinite_success_repaired",l.globalSuccess==.7f&&l.patterns[0].success==.7f);
        Check("level_repaired",l.patterns[0].level==2);
        var x=l.model.Context(.7f,.7f,0,0,0);
        Check("singular_model_reinitialized",Math.Abs(l.model.Value(2,x)-.18*Math.Sqrt(x.Sum(v=>v*v)))<1e-9);
        string healthy=State(s);s.NormalizeDda();Check("healthy_normalization_noop",State(s)==healthy);
        foreach(int bad in new[]{-999,999})foreach(float success in new[]{0f,.7f,1f})
        {int level=l.model.Choose(x,bad,4,success,0);Check("bounded_corrupt_level_"+bad+"_"+success,level>=-2&&level<=2);}
        var dirty=new BanditModel();dirty.Init();dirty.matrices[0]=double.NaN;dirty.vectors[0]=double.PositiveInfinity;dirty.Init();
        Check("nonfinite_model_reset",dirty.matrices.All(BanditModel.Finite)&&dirty.vectors.All(v=>v==0));
        dirty.matrices[1]=10;dirty.Init();Check("asymmetric_model_reset",dirty.matrices[1]==0);
        dirty.matrices[1]=dirty.matrices[6]=2;dirty.Init();Check("indefinite_model_reset",dirty.matrices[1]==0);
        var safe=dirty.Context(float.NaN,float.NegativeInfinity,999,float.NaN,-100);
        Check("context_bounded_finite",BanditModel.ValidContext(safe));
        string before=JsonUtility.ToJson(dirty);
        dirty.Update(99,x,.7);dirty.Update(0,new[]{double.NaN},.7);dirty.Update(0,x,double.NaN);dirty.Update(0,x,2);
        Check("bad_updates_ignored",before==JsonUtility.ToJson(dirty));
        Check("bad_value_safe",dirty.Value(999,x)==0&&dirty.Value(0,null)==0);
        Check("bad_reward_safe",BanditModel.Reward(float.NaN)==0&&BanditModel.Reward(-1)==0);
        s=new SaveData();var d=Session(s);Fill(d);d.Complete(s,b,true);before=State(s);d.Complete(s,b,false);
        Check("complete_exactly_once",before==State(s)&&s.Learning(b.id).battles==1);
        d=Session(s);Fill(d);d.Complete(s,b,true);Check("prepare_resets_completion_guard",s.Learning(b.id).battles==2);
        d=Session(s);Fill(d);before=State(s);d.Complete(s,b,false,true);d.Complete(s,b,true);
        Check("abandoned_session_cannot_later_train",before==State(s));
        d=Session(s);before=State(s);d.Complete(s,b,false);Check("zero_evidence_does_not_increment_loss",before==State(s));
        d=Session(s);Fill(d);d.Stats[0].parries=100;before=State(s);d.Complete(s,b,false);Check("invalid_counts_no_evidence",before==State(s));
        s.dda=false;d=Session(s);Fill(d);before=State(s);d.Complete(s,b,false);Check("disabled_no_training",before==State(s));
        s.dda=true;d=Session(s);Fill(d);l=s.Learning(b.id);l.battles=l.lossStreak=int.MaxValue;l.Pattern(d.Stats[0].id).observations=int.MaxValue;
        d.Complete(s,b,false);Check("counters_saturate",l.battles==int.MaxValue&&l.lossStreak==int.MaxValue&&l.Pattern(d.Stats[0].id).observations==int.MaxValue);
        string root=Path.Combine(Path.GetTempPath(),"tower-dda-repair-"+Guid.NewGuid().ToString("N"));
        SaveStore.UseIsolatedRoot(root);
        try
        {
            s=new SaveData{gold=777,unlockedBoss=2};s.cards.Add(new CardStack{id="blade",count=2});
            s.learning=null;Check("malformed_learning_save_written",SaveStore.Save(s));
            var loaded=SaveStore.Load();Check("missing_learning_preserves_progress",loaded.gold==777&&loaded.unlockedBoss==2&&loaded.cards[0].count==2&&loaded.learning.Count==0);
            l=loaded.Learning(b.id);l.model=null;l.patterns=null;l.lossStreak=-5;
            Check("nested_invalid_save_written",SaveStore.Save(loaded));loaded=SaveStore.Load();l=loaded.Learning(b.id);
            Check("nested_invalid_save_repaired",loaded.gold==777&&l.model!=null&&l.patterns!=null&&l.lossStreak==0);
        }
        finally{SaveStore.UseIsolatedRoot(null);if(Directory.Exists(root))Directory.Delete(root,true);}
        Console.WriteLine("DDA_ROBUSTNESS_PASSED "+count+" checks; no Unity rendering, DB or human fairness test");return 0;
    }
}
