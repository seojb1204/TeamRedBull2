// Controlled eligibility probe using the real selector, not a combat or human simulator.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Text.Json;
using ReversalTower;
using UnityEngine;

public static class DdaSelectionAuditTests
{
    static string Cell(object x)=>"\""+Convert.ToString(x,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
    public static int Main(string[] args)
    {
        string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        string root=Path.Combine(Path.GetTempPath(),"dda-selection-audit-"+Guid.NewGuid().ToString("N"));
        SaveStore.UseIsolatedRoot(root);CommunityStats.Replace(new CommunityStats.Entry[0]);
        int decisions=0,rows=0;
        try
        {
            File.WriteAllText(Path.Combine(output,"catalog.json"),JsonSerializer.Serialize(Catalog.Bosses,new JsonSerializerOptions{IncludeFields=true}));
            using(var csv=new StreamWriter(Path.Combine(output,"selection.csv")))
            {
                csv.WriteLine("seed,dda,phase,distance,cooldown_mode,trial,seconds,pattern,eligible,selected,base_weight,multiplier,probability,reason,assigned_level,windup,damage");
                foreach(int seed in new[]{17,42})foreach(bool enabled in new[]{false,true})
                foreach(bool phaseTwo in new[]{false,true})foreach(float distance in new[]{1f,4f,8f})
                foreach(bool release in new[]{false,true})
                {
                    AutoRunArgs.Active=false;AutoRunArgs.DisableDda=!enabled;
                    var g=new TowerGame{save=new SaveData()};
                    typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());
                    g.StartBattle(0);g.player.x=distance;g.boss.x=0;g.seconds=5;
                    var system=g.BossPatterns;system.SetTestSeed(seed);system.phaseTwo=phaseTwo;
                    // Inject evidence only; no claim that this is naturally observed combat.
                    g.Stat("cleave").opportunities=g.Stat("cleave").hits=20;
                    var cooldowns=(Dictionary<string,float>)typeof(BossPatternSystem).GetField("cooldowns",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(system);
                    for(int trial=0;trial<100;trial++)
                    {
                        g.seconds=5+trial*.5f;if(release)cooldowns.Clear();
                        int previous=system.SelectionLog.Count==0?0:system.SelectionLog.Last().decision;
                        system.SelectPattern(distance);
                        var evidence=system.SelectionLog.Where(e=>e.decision>previous).ToArray();
                        if(evidence.Length>0)
                        {
                            decisions++;
                            if(evidence.Count(e=>e.selected)!=1||Math.Abs(evidence.Sum(e=>e.probability)-1)>1e-5)throw new Exception("Invalid decision evidence");
                            if(evidence.Any(e=>e.multiplier<1||e.multiplier>1.35f))throw new Exception("Multiplier bounds");
                            if(!enabled&&evidence.Any(e=>e.multiplier!=1))throw new Exception("DDA OFF boosted");
                        }
                        foreach(var p in g.spec.patterns)
                        {
                            var e=evidence.SingleOrDefault(x=>x.id==p.id);
                            bool selected=e!=null&&e.selected;
                            var fields=new object[]{seed,enabled,phaseTwo?2:1,distance,release?"released-every-probe":"retained-until-time-ready",trial,g.seconds,p.id,e!=null&&e.weight>0,selected,
                                e==null?0:e.baseWeight,e==null?0:e.multiplier,e==null?0:e.probability,
                                e==null?"not in decision evidence: authored gate or no decision":e.reason,g.Stat(p.id).level,selected?system.windup:0,selected?system.patternDamage:0};
                            csv.WriteLine(string.Join(",",fields.Select(Cell)));rows++;
                        }
                    }
                }
            }
            Console.WriteLine("SELECTION_AUDIT_PASS decisions="+decisions+" rows="+rows+" seeds=17,42 Catalog/HEADLESS; controlled selection, not full combat");return 0;
        }
        finally
        {
            AutoRunArgs.DisableDda=false;SaveStore.UseIsolatedRoot(null);CommunityStats.Replace(new CommunityStats.Entry[0]);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
