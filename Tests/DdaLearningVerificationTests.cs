// Executes production DDA classes. Synthetic outcomes are injected; no combat/DB is emulated.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using ReversalTower;
using UnityEngine;

public static class DdaLearningVerificationTests
{
    static string label,output;
    static readonly List<string> checks=new List<string>{"source,check,status,detail"};
    static int failed,passed;
    static readonly CultureInfo CI=CultureInfo.InvariantCulture;
    static string Csv(object x)=>"\""+(x==null?"":Convert.ToString(x,CI)).Replace("\"","\"\"")+"\"";
    static string Row(params object[] xs)=>string.Join(",",xs.Select(Csv));
    static void Check(string name,bool value,string detail="")
    {checks.Add(Row(label,name,value?"PASS":"FAIL",detail));if(value)passed++;else failed++;}
    static void Note(string name,string detail)=>checks.Add(Row(label,name,"OBSERVED",detail));
    static bool Near(double a,double b)=>Math.Abs(a-b)<1e-6;
    static bool Same(double[] a,double[] b)=>a.Length==b.Length&&a.Zip(b,(x,y)=>Near(x,y)).All(x=>x);
    static double[] Copy(double[] a)=>(double[])a.Clone();
    static string Hash(double[] a)
    {using(var sha=SHA256.Create()){var bytes=new byte[a.Length*8];Buffer.BlockCopy(a,0,bytes,0,bytes.Length);return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").Substring(0,16);}}
    static string Values(double[] a)=>string.Join(";",a.Select(x=>x.ToString("G9",CI)));
    static bool Attack(Pattern p)=>p.kind!=AttackKind.Step&&p.kind!=AttackKind.Rage;
    static void ClearCommunity()=>CommunityStats.Replace(new CommunityStats.Entry[0]);
    static void Fill(PatternStats s,float success,int n=20)
    {s.opportunities=n;s.parries=(int)Math.Round(success*n);s.dodges=0;s.hits=n-s.parries;}
    static DdaBattleSession Session(SaveData save,BossSpec b)
    {var d=new DdaBattleSession();d.Prepare(save,b,false);return d;}
    static double Score(BanditModel m,double[] x,int level,int current,float success)
        =>m.Value(level+2,x)-.12*Math.Abs((success-.7)-level*.1)-.012*Math.Abs(level-current);
    static object Call(TowerGame g,string name,params object[] args)
        =>typeof(TowerGame).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,args);
    static TowerGame Game(int index=0,bool dda=true)
    {
        var g=new TowerGame{save=new SaveData{unlockedBoss=4,dda=dda}};
        typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());
        g.StartBattle(index);return g;
    }
    static void Core()
    {
        ClearCommunity();var m=new BanditModel();m.Init();
        var x=m.Context(.2f,.4f,-1,1,3);
        Check("six_context_features",Same(x,new double[]{1,.8,.6,-.5,.5,.6}),Values(x));
        Check("context_power_retry_caps",Same(m.Context(1,1,2,10,10),new double[]{1,0,0,1,1,1}));
        bool identity=true;for(int arm=0;arm<5;arm++)for(int i=0;i<6;i++)for(int j=0;j<6;j++)identity&=m.matrices[arm*36+i*6+j]==(i==j?1:0);
        Check("A_identity_b_zero",identity&&m.vectors.All(v=>v==0));
        var beforeA=Copy(m.matrices);var beforeB=Copy(m.vectors);m.Update(-1,x,.75);
        bool formula=true;for(int arm=0;arm<5;arm++)for(int i=0;i<6;i++)
        {
            formula&=Near(m.vectors[arm*6+i],beforeB[arm*6+i]+(arm==1?.75*x[i]:0));
            for(int j=0;j<6;j++)formula&=Near(m.matrices[arm*36+i*6+j],beforeA[arm*36+i*6+j]+(arm==1?x[i]*x[j]:0));
        }
        Check("actual_Update_outer_product_and_reward_vector",formula);
        var zero=new BanditModel();zero.Init();var zeroA=Copy(zero.matrices);zero.Update(0,x,0);
        Check("zero_reward_A_updates_b_legitimately_stays_zero",!Same(zeroA,zero.matrices)&&zero.vectors.All(v=>v==0));
        var analytic=new BanditModel();analytic.Update(0,new double[]{1,0,0,0,0,0},.75);
        Check("Value_theta_and_ucb_known_solution",Near(analytic.Value(2,new double[]{1,0,0,0,0,0}),.375+.18*Math.Sqrt(.5)));
        foreach(float s in new[]{0f,.3f,.6f,.7f,.8f,.95f,1f})
        {double expected=s>=.6f&&s<=.8f?1:Math.Max(0,1-Math.Min(Math.Abs(s-.6f),Math.Abs(s-.8f))*2.5);Check("Reward_"+s,Near(BanditModel.Reward(s),expected),BanditModel.Reward(s).ToString("G9",CI));}
        Check("first_battle_neutral",m.Choose(x,1,0,1,0)==0);
        for(int level=-2;level<=2;level++)
        {Check("three_loss_priority_"+level,m.Choose(x,level,10,1,3)==Math.Max(-2,level-1));}
        // Independent score reconstruction is a test oracle, not the simulated decision policy.
        foreach(int current in new[]{-2,-1,0,1,2})foreach(float success in new[]{.6f,.7f,.8f})
        {
            var context=m.Context(success,.7f,current,0,0);int best=current;double top=double.NegativeInfinity;
            for(int level=Math.Max(-2,current-1);level<=Math.Min(2,current+1);level++)
            {double score=Score(m,context,level,current,success);if(score>top){top=score;best=level;}}
            string state=Hash(m.matrices)+Hash(m.vectors);
            Check("midrange_actual_Choose_score_argmax_"+current+"_"+success,m.Choose(context,current,10,success,0)==best);
            Check("Choose_does_not_learn_"+current+"_"+success,state==Hash(m.matrices)+Hash(m.vectors));
        }
        // A trained neutral arm exposes the post-warmup failure without an artificial new policy.
        var trained=new BanditModel();var tx=trained.Context(.7f,.7f,0,0,0);for(int i=0;i<4;i++)trained.Update(0,tx,1);
        Check("desired_post_warmup_low_success_reduces_level",trained.Choose(trained.Context(.2f,.7f,0,0,0),0,10,.2f,0)==-1);
        Check("desired_post_warmup_high_success_increases_level",trained.Choose(trained.Context(1,.7f,0,0,0),0,10,1,0)==1);
        for(int level=-2;level<=2;level++)for(int battle=1;battle<=12;battle++)foreach(float success in new[]{0f,.3f,.7f,1f})
        {
            int chosen=trained.Choose(trained.Context(success,.7f,level,0,0),level,battle,success,0);
            if(chosen< -2||chosen>2||Math.Abs(chosen-level)>1)throw new Exception("Bounds/one-step invariant violated");
        }
        Check("five_arms_bounds_one_step_240_cases",true);
        // Actual model read at the next Prepare, with the policy in its LinUCB region.
        var b=Catalog.Bosses[0];var plain=new SaveData();plain.Learning(b.id).battles=5;
        var d=Session(plain,b);var learned=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(plain));
        for(int i=0;i<10;i++)learned.Learning(b.id).model.Update(-1,d.Contexts[b.patterns[0].id],1);
        var next=Session(learned,b);
        Check("next_Prepare_reads_changed_model",d.Stats[0].level==0&&next.Stats[0].level==-1);
    }
    static void Lifecycle()
    {
        var b=Catalog.Bosses[0];var save=new SaveData();var d=Session(save,b);var learn=save.Learning(b.id);learn.model.Init();
        var initial=Copy(learn.model.matrices);Fill(d.Stats[0],.3f);Fill(d.Stats[1],.95f);
        int[] levels=d.Stats.Select(s=>s.level).ToArray();
        Check("battle_outcomes_do_not_reselect_levels",d.Stats.Select(s=>s.level).SequenceEqual(levels)&&Same(initial,learn.model.matrices));
        d.Complete(save,b,true);
        Check("first_completed_battle_updates_model",learn.battles==1&&!Same(initial,learn.model.matrices));
        Check("PatternMemory_success_and_observations_independent",Near(learn.Pattern(d.Stats[0].id).success,.3)&&Near(learn.Pattern(d.Stats[1].id).success,.95)&&learn.Pattern(d.Stats[2].id).observations==0);
        Note("model_granularity","PatternMemory is per pattern; BanditModel A/b are shared by every pattern of a boss, not separate per-pattern models.");
        var next=Session(save,b);var mem=learn.Pattern(d.Stats[0].id);
        Check("next_context_uses_updated_memory",Same(next.Contexts[mem.id],learn.model.Context(mem.success,learn.globalSuccess,mem.level,LobbyCards.Power(save),learn.lossStreak)));
        // Snapshot semantics: toggling save.dda mid-battle does not rewrite Enabled/levels.
        save.dda=false;Check("running_battle_DDA_setting_snapshot",next.Enabled);
        var fixedSession=Session(save,b);string snapshot=JsonUtility.ToJson(save.learning);foreach(var s in fixedSession.Stats)Fill(s,1);
        fixedSession.Complete(save,b,false);
        Check("DDA_OFF_neutral_and_no_Complete_learning",fixedSession.Stats.All(s=>s.level==0)&&snapshot==JsonUtility.ToJson(save.learning));
        save.dda=true;var empty=Session(save,b);var a=Copy(learn.model.matrices);var v=Copy(learn.model.vectors);int n=learn.battles;empty.Complete(save,b,false);
        Check("unseen_patterns_no_A_b_or_memory_updates",Same(a,learn.model.matrices)&&Same(v,learn.model.vectors)&&learn.Pattern(d.Stats[2].id).observations==0);
        Note("zero_observation_battle_counter", "No-pattern Complete increments battles: "+n+" -> "+learn.battles+", lossStreak="+learn.lossStreak);
        var unobserved=Session(save,b);foreach(var s in unobserved.Stats)Fill(s,.7f);unobserved.Complete(save,b,true);
        string beforeSave=JsonUtility.ToJson(save.learning);Check("SaveStore_save",SaveStore.Save(save));var restored=SaveStore.Load();
        Check("A_b_memory_preserved_after_save_reload",beforeSave==JsonUtility.ToJson(restored.learning));
        var originalNext=Session(save,b);var restoredNext=Session(restored,b);
        Check("next_assignments_after_reload_match",originalNext.Stats.Select(s=>s.level).SequenceEqual(restoredNext.Stats.Select(s=>s.level)));
        // Calls through real game settlement; no CloudSync class is compiled or subscribed.
        foreach(string ending in new[]{"clear","death","abandon","quit"})
        {
            var g=Game();foreach(var s in g.stats)if(s.id!="step"&&s.id!="rage")Fill(s,.7f);
            int logged=0;Action<HistoryEntry,List<PatternStats>,SaveData> observer=(h,stats,progress)=>logged++;
            SaveStore.BattleLogged+=observer;
            try
            {
                if(ending=="clear")g.DamageBoss(g.bossHp);
                else if(ending=="death"){g.playerHp=0;Call(g,"TickBattle",0f);}
                else if(ending=="abandon")g.Abandon();else Call(g,"OnApplicationQuit");
                int battles=g.save.Learning(g.spec.id).battles;
                if(ending=="clear"||ending=="death")Check("game_"+ending+"_learns_once",battles==1&&logged==1);
                else Check("desired_game_"+ending+"_does_not_learn",battles==0,"actual learning.battles="+battles+"; event count="+logged);
                Call(g,"EndBattle",false);Check("settlement_guard_"+ending,g.save.Learning(g.spec.id).battles==battles&&logged==1);
            }
            finally{SaveStore.BattleLogged-=observer;}
        }
        // Session has no internal exactly-once guard; normal game EndBattle owns that guard.
        var twiceSave=new SaveData();var twice=Session(twiceSave,b);Fill(twice.Stats[0],.7f);twice.Complete(twiceSave,b,true);twice.Complete(twiceSave,b,true);
        Note("direct_Complete_duplicate_call", "Two direct Complete calls produce battles="+twiceSave.Learning(b.id).battles+"; guarded at TowerGame.EndBattle, not inside session.");
    }
    static void ColdStart()
    {
        var b=Catalog.Bosses[0];string id=b.patterns[0].id;
        foreach(int opportunities in new[]{0,19,20,200})foreach(int suggestion in new[]{-2,-1,0,1,2})
        {
            ClearCommunity();if(opportunities>0)CommunityStats.Replace(new[]{new CommunityStats.Entry{boss=b.id,pattern=id,opportunities=opportunities,players=12,successRate=.95f,suggestedLevel=suggestion}});
            var save=new SaveData();var d=Session(save,b);int expected=opportunities>=20?Math.Max(-1,Math.Min(1,suggestion)):0;
            Check("cold_start_opportunities_"+opportunities+"_suggest_"+suggestion,d.Stats[0].level==expected);
            Check("cold_context_threshold_"+opportunities+"_suggest_"+suggestion,Near(d.Contexts[id][1],opportunities>=20?.05:.3));
        }
        ClearCommunity();var local=new SaveData();var before=Session(local,b);int original=before.Stats[0].level;
        CommunityStats.Replace(new[]{new CommunityStats.Entry{boss=b.id,pattern=id,opportunities=20,players=12,successRate=1,suggestedLevel=1}});
        Check("late_community_data_does_not_change_running_assignment",before.Stats[0].level==original);
        Fill(before.Stats[0],.3f);before.Complete(local,b,true);var after=Session(local,b);
        Check("personal_observations_override_community_context",Near(after.Contexts[id][1],.7));
        ClearCommunity();
    }
    static float Outcome(string profile,int battle,int pattern)
    {
        switch(profile)
        {
            case "low_00":return 0;case "low_30":return .3f;case "high_95":return .95f;case "high_100":return 1;
            case "mid_60":return .6f;case "mid_70":return .7f;case "mid_80":return .8f;
            case "improving_20_to_100":return battle<=8?.2f:battle<=12?.7f:1;
            case "loss_streak_high":return 1;case "fixed_off":return .7f;
            default:return pattern%2==0?.2f:.95f;
        }
    }
    static void Experiments()
    {
        string[] profiles={"low_00","low_30","high_95","high_100","mid_60","mid_70","mid_80","improving_20_to_100","loss_streak_high","fixed_off","mixed_by_pattern"};
        using(var csv=new StreamWriter(Path.Combine(output,"battles.csv"),false,new UTF8Encoding(false)))
        {
            csv.WriteLine("source,profile,battle,boss,pattern,attack,assigned_level,success,reason,loss_before,loss_after,reward,context,a_before,a_after,b_before,b_after,arm_a_changed,arm_b_changed,next_level,memory_before,memory_after,observations,ucb_scores,final_scores,clear,model_a_delta,model_b_delta");
            foreach(var boss in Catalog.Bosses)foreach(string profile in profiles)
            {
                ClearCommunity();var save=new SaveData{dda=profile!="fixed_off"};var learning=save.Learning(boss.id);learning.model.Init();
                bool extremeDirection=true;
                for(int battle=1;battle<=24;battle++)
                {
                    var d=Session(save,boss);var model=learning.model;
                    var aa=Copy(model.matrices);var bb=Copy(model.vectors);int losses=learning.lossStreak;
                    var oldSuccess=d.Stats.Select(s=>learning.Pattern(s.id).success).ToArray();
                    var raw=new string[d.Stats.Count];var scores=new string[d.Stats.Count];
                    for(int j=0;j<d.Stats.Count;j++)
                    {
                        var s=d.Stats[j];var memory=learning.Pattern(s.id);var context=d.Contexts[s.id];
                        raw[j]=Values(Enumerable.Range(0,5).Select(arm=>model.Value(arm,context)).ToArray());
                        scores[j]=string.Join(";",Enumerable.Range(-2,5).Select(level=>Math.Abs(level-memory.level)<=1?Score(model,context,level,memory.level,memory.success).ToString("G9",CI):"ineligible"));
                        if(Attack(boss.patterns[j]))Fill(s,Outcome(profile,battle,j));
                    }
                    bool clear=profile!="loss_streak_high";d.Complete(save,boss,clear);
                    var next=Session(save,boss);
                    for(int j=0;j<d.Stats.Count;j++)if(Attack(boss.patterns[j]))
                    {
                        if(profile.StartsWith("low_"))extremeDirection&=next.Stats[j].level<=d.Stats[j].level;
                        if(profile.StartsWith("high_"))extremeDirection&=next.Stats[j].level>=d.Stats[j].level;
                    }
                    // Compare actual Complete against the sum of rank-one updates for all observed patterns.
                    var expectedA=Copy(aa);var expectedB=Copy(bb);
                    if(d.Enabled)foreach(var s in d.Stats)if(s.opportunities>0)
                    {
                        int arm=s.level+2;var context=d.Contexts[s.id];double reward=BanditModel.Reward(s.Success);
                        for(int i=0;i<6;i++){expectedB[arm*6+i]+=reward*context[i];for(int j=0;j<6;j++)expectedA[arm*36+i*6+j]+=context[i]*context[j];}
                    }
                    if(battle==1||battle==24)Check("batch_A_b_updates_"+boss.id+"_"+profile+"_"+battle,Same(expectedA,model.matrices)&&Same(expectedB,model.vectors));
                    double da=aa.Zip(model.matrices,(u,v)=>(v-u)*(v-u)).Sum(),db=bb.Zip(model.vectors,(u,v)=>(v-u)*(v-u)).Sum();
                    for(int j=0;j<d.Stats.Count;j++)
                    {
                        var s=d.Stats[j];int arm=s.level+2;
                        bool achanged=!Same(aa.Skip(arm*36).Take(36).ToArray(),model.matrices.Skip(arm*36).Take(36).ToArray());
                        bool bchanged=!Same(bb.Skip(arm*6).Take(6).ToArray(),model.vectors.Skip(arm*6).Take(6).ToArray());
                        csv.WriteLine(Row(label,profile,battle,boss.id,s.id,Attack(boss.patterns[j]),s.level,s.opportunities>0?s.Success.ToString("G9",CI):"",d.Reasons[s.id],losses,learning.lossStreak,
                            s.opportunities>0?BanditModel.Reward(s.Success).ToString("G9",CI):"",Values(d.Contexts[s.id]),Hash(aa),Hash(model.matrices),Hash(bb),Hash(model.vectors),achanged,bchanged,next.Stats[j].level,oldSuccess[j],learning.Pattern(s.id).success,learning.Pattern(s.id).observations,raw[j],scores[j],clear,Math.Sqrt(da),Math.Sqrt(db)));
                    }
                }
                if(profile.StartsWith("low_")||profile.StartsWith("high_"))
                    Check("desired_extreme_direction_"+boss.id+"_"+profile,extremeDirection,"Synthetic fixed-success feedback with clear=true, not human performance");
                if(profile=="improving_20_to_100")
                {
                    var final=Session(save,boss);
                    Check("desired_reacts_to_improvement_"+boss.id,final.Stats.Where((st,j)=>Attack(boss.patterns[j])).All(st=>st.level==2),"After 12 consecutive 100% synthetic outcomes, next attack assignments should reach +2");
                }
                File.WriteAllText(Path.Combine(output,boss.id+"_"+profile+"_final.json"),JsonUtility.ToJson(save.learning));
            }
        }
    }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CI;label=args[0];output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        string sandbox=Path.Combine(Path.GetTempPath(),"dda-verification-save-"+Guid.NewGuid().ToString("N"));SaveStore.UseIsolatedRoot(sandbox);
        try
        {Core();Lifecycle();ColdStart();Experiments();}
        catch(Exception e){checks.Add(Row(label,"harness_execution","ERROR",e.ToString()));failed++;Console.WriteLine(e);}
        finally
        {
            File.WriteAllLines(Path.Combine(output,"checks.csv"),checks,new UTF8Encoding(false));ClearCommunity();
            SaveStore.UseIsolatedRoot(null);if(Directory.Exists(sandbox))Directory.Delete(sandbox,true);
        }
        Console.WriteLine("DDA_VERIFICATION source="+label+" PASS="+passed+" FAIL="+failed+" (failures retained, no policy changes)");
        return failed==0?0:1;
    }
}
