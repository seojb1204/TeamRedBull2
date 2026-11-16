// Executes real combat Update/inputs with UnityShim. Not Unity, pixels, or human-like play.
using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using ReversalTower;
using UnityEngine;
public static class PlaytestAgentTests
{
    static readonly MethodInfo update=typeof(TowerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
    static void Set(TowerGame g,string name,object v)=>typeof(TowerGame).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g,v);
    static string Digest(BattleEvidence e)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",e.Events))));
    static void EvidenceRegression()
    {
        var e=new BattleEvidence();e.Note(0,"damage_taken","test",30);e.Note(1,"heal","potion",20);e.Note(2,"damage_taken","test",30);
        if(e.DamageTaken!=60||e.Healing!=20)throw new Exception("Damage must not be net HP loss");
        e.Note(3,"parry","test");e.Note(3,"input_requested","Q");e.Note(3,"input_committed","Q");
        if(e.Parries!=1||e.RequestedInputs!=1||e.CommittedInputs!=1)throw new Exception("Event counters");
        for(int i=0;i<BattleEvidence.MaxEvents;i++)e.Note(4,"damage_taken","test",1);
        if(e.Events.Count!=BattleEvidence.MaxEvents||e.DroppedEvents<=0||e.DamageTaken!=20060)throw new Exception("Bounded events must preserve totals");
        e.Reset();if(e.Events.Count!=0||e.DroppedEvents!=0||e.DamageTaken!=0||e.Parries!=0)throw new Exception("Battle reset");
        AutoRunArgs.Active=false;AutoRunArgs.AgentProfile=null;
        var g=new TowerGame{save=new SaveData()};Set(g,"sound",new Soundscape());g.StartBattle(0);
        g.DamageBoss(10,1);if(g.Evidence.Events.Count!=0)throw new Exception("Normal play must not collect events");
        AutoRunArgs.AgentProfile="balanced";
        float before=g.bossHp;g.DamageBoss(10,1);
        if(Math.Abs(g.Evidence.DamageDealt-(before-g.bossHp))>.001f)throw new Exception("Actual boss damage evidence");
    }
    static void CombatEvidenceRegression()
    {
        AutoRunArgs.Active=false;AutoRunArgs.AgentProfile="balanced";AutoRunArgs.DisableDda=true;
        var g=new TowerGame{save=new SaveData{tutorialSeen=true}};Set(g,"sound",new Soundscape());g.StartBattle(0);
        string pattern=g.stats[0].id;
        float start=g.playerHp;
        g.PlayerHit(10,false,pattern,g.player.x+1);
        if(g.Evidence.DamageTaken!=10)throw new Exception("Damage callback missing");
        g.PlayerHit(10,false,pattern,g.player.x+1);
        if(g.Evidence.DamageTaken!=10)throw new Exception("Invulnerability counted as damage");
        typeof(TowerGame).GetMethod("HealPlayer",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{6f,"regression"});
        g.playerInv=0f;g.PlayerHit(10,false,pattern,g.player.x+1);
        if(g.Evidence.DamageTaken!=20||g.Evidence.Healing!=6||Math.Abs(g.playerHp-(start-14))>.001f)throw new Exception("Heal-separated cumulative damage");
        g.parryAge=.1f;g.dashTime=0f;
        g.PlayerHit(10,true,pattern,g.player.x+1);
        if(g.Evidence.Parries!=1||g.Evidence.DamageTaken!=20)throw new Exception("Parry counted as damage");
        g.playerInv=0f;g.playerHp=3;
        g.PlayerHit(10,false,pattern,g.player.x+1);
        if(g.Evidence.DamageTaken!=23)throw new Exception("Overkill must be clipped");
        g.StartBattle(0);
        if(g.Evidence.DamageTaken!=0||g.Evidence.Events.Count!=0)throw new Exception("StartBattle evidence leak");
        Time.unscaledDeltaTime=1f/60;Input.Clear();Input.down.Add(KeyCode.Q);update.Invoke(g,null);Input.Clear();
        if(g.Evidence.RequestedInputs!=1||g.Evidence.CommittedInputs!=1)throw new Exception("Q request/commit callback");
        Input.down.Add(KeyCode.Q);update.Invoke(g,null);Input.Clear();
        if(g.Evidence.RequestedInputs!=2||g.Evidence.CommittedInputs!=1)throw new Exception("Cooldown/action lock must not commit Q");
    }
    static string Trial(SaveData save,int boss,string profile,int seed,int round,bool enabled)
    {
        AutoRunArgs.Active=false;AutoRunArgs.DisableDda=!enabled;AutoRunArgs.AgentProfile=profile;AutoRunArgs.AgentSeed=seed;
        var g=new TowerGame{save=save};Set(g,"sound",new Soundscape());Set(g,"demo",true);g.StartBattle(boss);
        Time.unscaledDeltaTime=1f/60;Input.Clear();int steps=0,stationary=0,noDamage=0,longestNoDamage=0;
        float lastX=g.player.x,lastPlayerHp=g.playerHp,lastBossHp=g.bossHp;
        while(g.mode==ScreenMode.Battle&&steps<60*120)
        {
            steps++;update.Invoke(g,null);Input.Clear();
            if(Math.Abs(g.player.x-lastX)<0.001f)stationary++;
            noDamage=(g.playerHp==lastPlayerHp&&g.bossHp==lastBossHp)?noDamage+1:0;
            longestNoDamage=Math.Max(longestNoDamage,noDamage);
            lastX=g.player.x;lastPlayerHp=g.playerHp;lastBossHp=g.bossHp;
            if(float.IsNaN(g.playerHp)||float.IsNaN(g.bossHp)||float.IsNaN(g.player.x))throw new Exception("Non-finite combat state");
            foreach(var st in g.stats)if(st.level < -2||st.level > 2)throw new Exception("DDA out of bounds");
        }
        string outcome=g.mode==ScreenMode.Result?(g.result.clear?"clear":"death"):"timeout";
        // Timeout is an abandoned battle, never counted as a human failure for learning.
        if(outcome=="timeout")g.Abandon();
        var ci=CultureInfo.InvariantCulture;var rows=new StringBuilder();
        foreach(var st in g.stats)rows.AppendFormat(ci,"{0},{1},{2},{3},{4},{5},{6},{7:F3},{8:F3},{9:F3},{10},{11},{12},{13},{14},{15},{16:F4},{17:F3},{18:F4},{19:F3},{20:F3},{21:F3},{22},{23},{24},{25},{26}\n",
            profile,seed,boss,enabled,round,outcome,steps,g.seconds,g.playerHp,g.bossHp,g.hits,st.id,st.level,st.opportunities,st.parries,st.hits,st.Success,longestNoDamage/60f,steps>0?(float)stationary/steps:0,g.Evidence.DamageTaken,g.Evidence.Healing,g.Evidence.DamageDealt,g.Evidence.Parries,g.Evidence.RequestedInputs,g.Evidence.CommittedInputs,g.Evidence.DroppedEvents,Digest(g.Evidence));
        return rows.ToString();
    }
    public static int Main(string[] args)
    {
        string output=args.Length>0?Path.GetFullPath(args[0]):Path.GetFullPath("agent-results.csv");
        string root=Path.Combine(Path.GetTempPath(),"tower-agent-"+Guid.NewGuid().ToString("N"));SaveStore.UseIsolatedRoot(root);
        try
        {
            if(!SaveStore.IsIsolated)throw new Exception("Isolation required");
            if(!AutoRunArgs.HasAutomationArguments(new[]{"game","-towerAgent","expert"})||AutoRunArgs.HasAutomationArguments(new[]{"game"}))throw new Exception("Network guard classification");
            EvidenceRegression();CombatEvidenceRegression();
            string first=Trial(new SaveData{unlockedBoss=4},0,"balanced",17,1,true);
            string second=Trial(new SaveData{unlockedBoss=4},0,"balanced",17,1,true);
            if(first!=second)throw new Exception("Same-seed replay differs");
            var csv=new StringBuilder("profile,seed,boss,dda,round,outcome,steps,seconds,playerHp,bossHp,hits,pattern,level,opportunities,parries,patternHits,success,longestNoDamageSeconds,stationaryFraction,damageTaken,healing,damageDealt,eventParries,requestedInputs,committedInputs,droppedEvents,eventSha256\n");int count=0;
            foreach(string profile in new[]{"novice","balanced","expert"})foreach(int seed in new[]{17,42})foreach(bool dda in new[]{false,true})for(int boss=0;boss<5;boss++)
            {
                var save=new SaveData{unlockedBoss=4};
                for(int round=1;round<=4;round++){csv.Append(Trial(save,boss,profile,seed+round-1,round,dda));count++;}
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,csv.ToString());
            Console.WriteLine("PLAYTEST_AGENT_PASS trials="+count+" replay=identical-including-event-hash evidence=regression-pass engine=UnityShim isolation=true network=excluded");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{AutoRunArgs.AgentProfile=null;AutoRunArgs.DisableDda=false;SaveStore.UseIsolatedRoot(null);}
    }
}
