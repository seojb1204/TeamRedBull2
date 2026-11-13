using System;
using System.Reflection;
using ReversalTower;
using UnityEngine;
public static class PhanesTests {
 static int n;
 static void Check(bool b,string m){if(!b)throw new Exception(m);n++;}
 static TowerGame Start(){var g=new TowerGame();g.save=new SaveData{unlockedBoss=4};typeof(TowerGame).GetField("sound",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,new Soundscape());g.StartBattle(1);return g;}
 public static int Main(){try{
 foreach(float dt in new[]{1f/30,1f/60,1f/120}){
 var g=Start();float x=g.boss.x;g.BossPatterns.TickBoss(dt);Check(g.boss.x<x,"moves throughout rest");
 g.barrier=true;g.boss.y=1.5f;g.BossPatterns.BreakPhanesBarrier();g.bossHp=g.spec.hp*.4f;
 float grounded=0;int ticks=0;bool landed=false,rising=false;
 while(g.BossPatterns.phanesDownStage!=0&&ticks++<1000){int stage=g.BossPatterns.phanesDownStage;
 if(stage==2){landed=true;grounded+=dt;Check(Math.Abs(g.boss.y)<.0001,"ground contact");}
 if(stage==3)rising=true;
 g.BossPatterns.TickBoss(dt);Check(g.current==null,"no attack during down/recovery");Check(!g.phaseTwo,"phase cannot cancel reward");Check(g.boss.y>=0,"never below floor");}
 Check(landed&&rising,"complete lifecycle");Check(Math.Abs(grounded-3)<dt*2,"three seconds on ground");Check(Math.Abs(g.boss.y-1.05f)<.001,"returns to hover");
 g.BossPatterns.TickBoss(dt);Check(g.phaseTwo,"phase resumes after recovery");
 g=Start();g.boss.y=1.2f;g.bossStagger=.45f;g.BossPatterns.TickBoss(dt);Check(g.boss.y==1.2f&&g.BossPatterns.phanesDownStage==0,"parry does not drop boss");
 g.BossPatterns.BreakPhanesBarrier();g.StartBattle(1);Check(g.BossPatterns.phanesDownStage==0,"restart resets down");
 }
 foreach(float side in new[]{-1f,1f}){var g=Start();g.player.x=side*10;g.boss.x=side*9;g.rest=100;for(int j=0;j<300;j++)g.BossPatterns.TickBoss(.016f);Check(g.boss.x>=TowerGame.Left+1&&g.boss.x<=TowerGame.Right-1,"arena bound");}
 {var g=Start();g.current=g.spec.patterns[0];g.windup=1;g.patternAge=0;float x=g.boss.x;g.BossPatterns.TickBoss(.01f);Check(x==g.boss.x,"windup position locked");}
 {var g=Start();g.boss.y=1.5f;g.BossPatterns.BreakPhanesBarrier();g.BossPatterns.TickBoss(3.9f);Check(g.BossPatterns.phanesDownStage==4,"large dt carries across stages");g.bossHp=0;float y=g.boss.y;g.BossPatterns.TickBoss(.2f);Check(g.boss.y==y,"dead simulation frozen");}
 {var g=Start();g.barrier=true;g.barrierHp=5;g.player=new Vector2(-1,0);g.face=1;typeof(TowerGame).GetMethod("BeginAction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,new object[]{"J",.35f});typeof(TowerGame).GetMethod("TickAction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,new object[]{.2f});Check(!g.barrier&&g.BossPatterns.phanesDownStage==1,"real wall attack enters fall");}
 Check(PhanesPose.Sample(null,0,1,1,1,false,true,false,false,false).index==6,"parry pose");
 Check(PhanesPose.Sample(null,0,1,1,1,false,false,false,false,false,2,1).index==10,"down pose");
 Check(PhanesPose.Sample(null,0,1,1,1,false,false,false,false,false,3,.1f).index==11,"recovery pose");
 Console.WriteLine("PASS "+n+" Phanes assertions");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}