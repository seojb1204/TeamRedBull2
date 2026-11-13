using System;
using System.Reflection;
using ReversalTower;
using UnityEngine;

public static class ArakusChargePolishTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
    static TowerGame Game(int level,bool rage,int index=0)
    {
        var g=new TowerGame{save=new SaveData{unlockedBoss=4}};
        typeof(TowerGame).GetField("sound",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g,new Soundscape());
        g.StartBattle(index);
        foreach(var s in g.stats)s.level=level;
        g.BossPatterns.rageActive=rage;g.boss=new Vector2(0,0);g.player=new Vector2(9,0);
        return g;
    }
    static Pattern Pattern(TowerGame g,string id)=>Array.Find(g.spec.patterns,p=>p.id==id);
    static void Rush(int level,bool rage,float dt)
    {
        var g=Game(level,rage);var p=Pattern(g,"rush");string learning=JsonUtility.ToJson(g.save.learning);
        g.BossPatterns.BeginPattern(p);g.BossPatterns.TickBoss(0);
        float tempo=rage?1.5f:1,duration=1.3f/tempo;
        float[] telegraph={1.3f,1.15f,1,.9f,.8f},damage={.8f,.9f,1,1.1f,1.2f};
        Check(Near(g.windup,Math.Max(.4f,.5f*telegraph[level+2])/tempo),"DDA windup preserved");
        Check(Near(g.recovery,.35f/tempo)&&Near(g.patternDamage,4*damage[level+2]),"recovery and damage preserved");
        var a=new ArakusAnimationSelection.Input{combat=true,pattern="rush",windup=g.windup,recovery=g.recovery,tempo=tempo};
        a.age=g.windup-.0001f;Check(ArakusAnimationSelection.Sample(a,0).frame==0,"windup pose");
        a.age=g.windup+duration-.0001f;int moving=ArakusAnimationSelection.Sample(a,0).frame;
        Check(moving==1||moving==2,"run until movement boundary");
        a.age=g.windup+duration+.0001f;Check(ArakusAnimationSelection.Sample(a,0).frame==3,"recovery starts at movement boundary");
        a.age=g.windup+duration+g.recovery*.75f;Check(ArakusAnimationSelection.Sample(a,0).frame==4,"recovery finishes ready");
        // Actual simulation sampled at several frame rates, with the player out of range.
        while(g.current!=null&&g.patternAge<g.windup+duration+g.recovery+.1f)
            g.BossPatterns.TickBoss(dt);
        Check(Near(g.boss.x,5),"full 5m arrival, including a frame that crosses the end");
        Check(g.current==null,"rush terminates after recovery");
        Check(g.attackStep==6,"six scheduled attempts at unchanged 0.25/tempo interval");
        Check(JsonUtility.ToJson(g.save.learning)==learning,"rush does not mutate learning model");
    }
    static void Contacts(float height,bool expectHit)
    {
        var g=Game(0,false);g.player=new Vector2(1,height);var p=Pattern(g,"rush");
        g.BossPatterns.BeginPattern(p);g.BossPatterns.TickBoss(0);g.patternAge=g.windup;
        float hp=g.playerHp;g.BossPatterns.TickBoss(0);
        Check((g.playerHp<hp)==expectHit,"rush ground/above-height-threshold contact");
        Check(g.attackStep==1,"first attack at movement start");
        g.patternAge=g.windup+.249f;g.BossPatterns.TickBoss(0);Check(g.attackStep==1,"no early second attack");
        g.patternAge=g.windup+.251f;g.BossPatterns.TickBoss(0);Check(g.attackStep==2,"0.25 second interval preserved");
    }
    static void Pillar(bool left)
    {
        var g=Game(0,false);g.boss=new Vector2(left?-5:5,0);g.player=new Vector2(left?2:-2,0);
        g.BossPatterns.BeginPattern(Pattern(g,"charge"));g.patternAge=g.windup;
        g.BossPatterns.ExecutePattern();
        var a=new ArakusAnimationSelection.Input{combat=true,pattern="charge",windup=g.windup,age=g.windup,step=1,tempo=1};
        Check(ArakusAnimationSelection.Sample(a,0).state=="Charge","charge before pillar");
        g.BossPatterns.TickBoss(.1f);
        Check(g.current==null&&Near(g.bossStagger,3.2f),"pillar interrupts charge for 3.2 seconds");
        Check(left?!g.pillarLeft:!g.pillarRight,"correct pillar destroyed");
        a.pattern=null;a.groggy=true;a.stagger=g.bossStagger;
        Check(ArakusAnimationSelection.Sample(a,0).state=="Groggy"&&ArakusAnimationSelection.Sample(a,0).frame==0,"groggy entry");
        Check(ArakusAnimationSelection.Sample(a,.2f).frame==1,"groggy transition");
        Check(ArakusAnimationSelection.Sample(a,.5f).frame>=2,"groggy loop");
        g.BossPatterns.TickBoss(3.21f);a.stagger=g.bossStagger;a.groggy=false;
        Check(ArakusAnimationSelection.Sample(a,4).state=="Idle","existing recovery returns to idle");
    }
    public static void Main()
    {
        Check(Near(ArakusPatternTiming.ShieldRushMoveDuration,1.3f),"shared duration");
        foreach(int level in new[]{-2,-1,0,1,2})foreach(bool rage in new[]{false,true})foreach(float dt in new[]{1f/30,1f/60,1f/144})Rush(level,rage,dt);
        Contacts(0,true);Contacts(3,false);Pillar(true);Pillar(false);
        var dodge=Game(0,false);dodge.player=new Vector2(1,0);
        dodge.BossPatterns.BeginPattern(Pattern(dodge,"rush"));dodge.BossPatterns.TickBoss(0);
        dodge.patternAge=dodge.windup;dodge.playerInv=.2f;dodge.dashTime=.2f;float hp=dodge.playerHp;
        dodge.BossPatterns.TickBoss(0);
        Check(dodge.playerHp==hp&&dodge.Stat("rush").dodges==1,"dash invulnerability still avoids rush");
        // Generic Dash execution must retain its own speed and timing, on another boss.
        var other=Game(0,false,4);other.player=new Vector2(9,3);other.BossPatterns.BeginPattern(Pattern(other,"draw"));
        other.patternAge=other.windup;other.BossPatterns.ExecutePattern();other.BossPatterns.TickBoss(.1f);
        Check(Near(other.boss.x,other.spec.dashSpeed*.1f),"other boss dash speed unchanged");
        Console.WriteLine("ARAKUS_CHARGE_POLISH_TESTS_PASSED "+passed+"; Unity rendering/input and human dodgeability not exercised.");
    }
}
