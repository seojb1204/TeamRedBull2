// Executes real combat Update/inputs with UnityShim. Not Unity, pixels, or human-like play.
using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Text;
using ReversalTower;
using UnityEngine;
public static class PlaytestAgentTests
{
    static readonly MethodInfo update=typeof(TowerGame).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
    static void Set(TowerGame g,string name,object v)=>typeof(TowerGame).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g,v);
    static string Trial(SaveData save,int boss,string profile,int seed,int round,bool enabled)
    {
        AutoRunArgs.Active=false;AutoRunArgs.DisableDda=!enabled;AutoRunArgs.AgentProfile=profile;AutoRunArgs.AgentSeed=seed;
        var g=new TowerGame{save=save};Set(g,"sound",new Soundscape());Set(g,"demo",true);g.StartBattle(boss);
        Time.unscaledDeltaTime=1f/60;Input.Clear();int steps=0;
        while(g.mode==ScreenMode.Battle&&steps++<60*120)
        {
            update.Invoke(g,null);Input.Clear();
            if(float.IsNaN(g.playerHp)||float.IsNaN(g.bossHp)||float.IsNaN(g.player.x))throw new Exception("Non-finite combat state");
            foreach(var st in g.stats)if(st.level < -2||st.level > 2)throw new Exception("DDA out of bounds");
        }
        string outcome=g.mode==ScreenMode.Result?(g.result.clear?"clear":"death"):"timeout";
        // Timeout is an abandoned battle, never counted as a human failure for learning.
        if(outcome=="timeout")g.Abandon();
        var ci=CultureInfo.InvariantCulture;var rows=new StringBuilder();
        foreach(var st in g.stats)rows.AppendFormat(ci,"{0},{1},{2},{3},{4},{5},{6},{7:F3},{8:F3},{9:F3},{10},{11},{12},{13},{14},{15},{16:F4}\n",
            profile,seed,boss,enabled,round,outcome,steps,g.seconds,g.playerHp,g.bossHp,g.hits,st.id,st.level,st.opportunities,st.parries,st.hits,st.Success);
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
            string first=Trial(new SaveData{unlockedBoss=4},0,"balanced",17,1,true);
            string second=Trial(new SaveData{unlockedBoss=4},0,"balanced",17,1,true);
            if(first!=second)throw new Exception("Same-seed replay differs");
            var csv=new StringBuilder("profile,seed,boss,dda,round,outcome,steps,seconds,playerHp,bossHp,hits,pattern,level,opportunities,parries,patternHits,success\n");int count=0;
            foreach(string profile in new[]{"novice","balanced","expert"})foreach(int seed in new[]{17,42})foreach(bool dda in new[]{false,true})for(int boss=0;boss<5;boss++)
            {
                var save=new SaveData{unlockedBoss=4};
                for(int round=1;round<=4;round++){csv.Append(Trial(save,boss,profile,seed+round-1,round,dda));count++;}
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,csv.ToString());
            Console.WriteLine("PLAYTEST_AGENT_PASS trials="+count+" replay=identical engine=UnityShim isolation=true network=excluded");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{AutoRunArgs.AgentProfile=null;AutoRunArgs.DisableDda=false;SaveStore.UseIsolatedRoot(null);}
    }
}
