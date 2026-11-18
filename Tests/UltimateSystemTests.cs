using System;using System.Reflection;using ReversalTower;using UnityEngine;
public static class UltimateSystemTests
{
 static int n;static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
 static object Call(TowerGame g,string name,params object[] args)=>typeof(TowerGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,args);
 static TowerGame Game(int ultimate=-1)
 {Input.Clear();var g=new TowerGame{save=new SaveData{unlockedBoss=4,equippedUltimate=ultimate}};typeof(TowerGame).GetField("sound",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g,new Soundscape());g.save.tutorialOnboardingVersion=1;g.StartBattle(0);g.showHelp=false;g.boss=new Vector2(0,0);g.player=new Vector2(-1,0);return g;}
 static void Cast(TowerGame g,string key){Call(g,"BeginAction",key,key=="R"?.85f:1.3f);Call(g,"TickAction",.8f);}
 public static int Main(){try{
 foreach(var key in new[]{"Q","W","E"}){var g=Game();Cast(g,key);Check(Near(g.ult,15),key+" cast plus hit exactly15");Call(g,"TickAction",.1f);Check(Near(g.ult,15),key+" no duplicate");}
 var blocked=Game();blocked.skillQ=5;Input.down.Add(KeyCode.Q);Call(blocked,"TickBattle",.01f);Check(Near(blocked.ult,0),"cooldown rejected Q no charge");Input.Clear();
 var inherited=Game();inherited.save.cards.Add(new CardStack{id="inherit",count=3});float before=inherited.bossHp;Cast(inherited,"R");Cast(inherited,"J");Check(Near(before-inherited.bossHp,17.44f),"inherit charged J17.44");
 var ei=Game(0);ei.save.cards.Add(new CardStack{id="inherit",count=3});before=ei.bossHp;Cast(ei,"R");Call(ei,"TickEcho",.5f);Check(Near(before-ei.bossHp,62),"inherit low62");Call(ei,"TickEcho",.5f);Check(Near(before-ei.bossHp,148.8f),"inherit high86.8");
 var armor=Game();armor.save.cards.Add(new CardStack{id="armor",count=3});armor.sparkTime=8;armor.playerAction="R";armor.actionTime=.2f;before=armor.playerHp;armor.PlayerHit(20,false,"cleave",1);Check(Near(before-armor.playerHp,9.1f),"armor then shared50% damage reduction");
 var clamp=Game();clamp.ult=99;Cast(clamp,"Q");Check(Near(clamp.ult,100),"clamp");
 var basic=Game();float hp=basic.bossHp;Cast(basic,"R");Check(Near(hp,basic.bossHp)&&Near(basic.sparkTime,8),"R buff no immediate hit");Cast(basic,"J");Check(Near(hp-basic.bossHp,16)&&basic.ChargedHits==1,"charged J16 and event");Check(basic.ult>0,"J damage charge preserved");
 var dr=Game();Cast(dr,"R");dr.actionTime=.3f;dr.playerInv=0;hp=dr.playerHp;dr.PlayerHit(20,false,"cleave",1);Check(Near(hp-dr.playerHp,10),"R casting50% layer");Check(!dr.UltimateCasting,"hit interruption clears casting");dr.playerInv=0;hp=dr.playerHp;dr.PlayerHit(20,false,"cleave",1);Check(Near(hp-dr.playerHp,20),"sword buff alone grants no protection");
 var inv=Game();Call(inv,"BeginAction","R",.85f);hp=inv.playerHp;inv.PlayerHit(20,false,"cleave",1);Check(Near(hp,inv.playerHp),"initial invulnerability preserved");inv.playerInv=0;inv.actionTime=.2f;inv.PlayerHit(20,false,"cleave",1);Check(Near(hp-inv.playerHp,10),"cast protection after invulnerability");
 var expire=Game();expire.sparkTime=.01f;Call(expire,"TickBattle",.02f);Check(!expire.SwordCharged,"spark expires");expire.sparkTime=8;expire.save.tutorialOnboardingVersion=1;expire.StartBattle(0);Check(!expire.SwordCharged&&expire.echo==null,"reset clears effects");
 foreach(int side in new[]{-1,1}){var e=Game(0);e.player=new Vector2(side*5,0);hp=e.bossHp;Cast(e,"R");Check(Near(hp,e.bossHp),"echo no instant hit");Call(e,"TickEcho",.5f);Check(Near(hp-e.bossHp,50),"echo low50 both sides");e.playerAction="J";Call(e,"TickEcho",.5f);Check(Near(hp-e.bossHp,120)&&Near(e.ult,0),"high70 delayed no recharge");Check(e.bossStagger>=.8f,"high stagger");Call(e,"TickEcho",.5f);Check(e.echo==null,"echo expires");}
 var large=Game(0);hp=large.bossHp;Cast(large,"R");Call(large,"TickEcho",2f);Check(Near(hp-large.bossHp,120)&&large.echo==null,"large dt both strikes");
 var high=Game(0);high.boss.y=1.5f;hp=high.bossHp;Cast(high,"R");Call(high,"TickEcho",1f);Check(Near(hp-high.bossHp,70),"low misses high hits");
 var far=Game(0);far.boss.x=10;hp=far.bossHp;Cast(far,"R");Call(far,"TickEcho",1f);Check(Near(hp,far.bossHp),"out of reach misses");
 var shot=Game(1);shot.playerAction="J";shot.shots.Add(new TowerGame.Shot{pos=new Vector2(0,1),vel=new Vector2(0,0),damage=20,life=2,friendly=true,pattern="ultimate"});shot.BossPatterns.TickShots(0);Check(Near(shot.ult,0),"delayed ultimate projectile no recharge");
 var parry=Game();parry.parryAge=.1f;parry.PlayerHit(20,true,"cleave",1);Check(Near(parry.ult,15),"parry15 preserved");
 Console.WriteLine("PASS "+n);return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}
