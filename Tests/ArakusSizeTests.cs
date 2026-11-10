using System;using System.Reflection;using System.Linq;using ReversalTower;using UnityEngine;
public static class ArakusSizeTests
{
 static int n;static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 static object Call(TowerGame g,string name,params object[] args)=>typeof(TowerGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,args);
 static TowerGame Game(int index=0,int level=0,bool rage=false)
 {var g=new TowerGame{save=new SaveData{unlockedBoss=4}};typeof(TowerGame).GetField("sound",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(g,new Soundscape());g.StartBattle(index);g.boss=new Vector2(0,0);g.player=new Vector2(-6,0);g.seconds=10;foreach(var s in g.stats)s.level=level;g.BossPatterns.rageActive=rage;return g;}
 static Pattern P(TowerGame g,string id)=>Array.Find(g.spec.patterns,p=>p.id==id);
 static void Enemy(string id,int face,float distance,float y,bool parry,bool dash,bool expectHit,int level,bool rage)
 {
  var g=Game(0,level,rage);var p=P(g,id);g.player=new Vector2(face*distance,y);g.bossFace=face;g.BossPatterns.BeginPattern(p);g.bossFace=face;
  g.parryAge=parry?.1f:99;g.dashTime=dash?.1f:0;g.playerInv=dash?.1f:0;float hp=g.playerHp;
  g.BossPatterns.ExecutePattern();Check((g.playerHp<hp)==expectHit,id+" actual hit boundary "+face+" "+distance+" "+y);
  if(parry)Check(g.parries==1,id+" expanded range parry");if(dash)Check(g.Stat(id).dodges==1,id+" expanded range dodge");
 }
 public static int Main()
 {
  try{
   Check(Near(ArakusGeometry.HalfWidth,1.2964286f)&&Near(ArakusGeometry.Height,3.4964286f),"sprite alpha dimensions");
   for(int face=-1;face<=1;face+=2)foreach(string action in new[]{"J","Q","W","E","R"})foreach(bool hit in new[]{false,true})
   {
    var g=Game();g.face=face;g.save.equippedUltimate=0;float range=action=="J"?2.1f:action=="Q"?2.5f:action=="W"?2.9f:action=="E"?2.8f:6;
    g.player=new Vector2(-face*(range+g.BossWidth+(hit?-.01f:.01f)),3.3f);float hp=g.bossHp;
    Call(g,"BeginAction",action,1f);Call(g,"TickAction",.9f);Check((g.bossHp<hp)==hit,action+" actual player attack width/height "+face);
   }
   var box=Game();Check(!box.BossAttackHeightOverlap(ArakusGeometry.Height+.01f),"above body misses");box.boss.y=3;
   Check(!box.BossAttackHeightOverlap(0),"boss above unchanged player upward reach misses");
   Check(box.BossAttackHeightOverlap(1),"player upward interval overlaps airborne body");
   box.boss.y=0;Check(box.BossShotOverlap(new Vector2(1.2f,3.3f),.1f),"projectile enlarged body overlaps");
   Check(!box.BossShotOverlap(new Vector2(1.5f,3.3f),.1f),"projectile outside enlarged body misses");
   float hpShot=box.bossHp;box.shots.Add(new TowerGame.Shot{pos=new Vector2(1.2f,3.3f),vel=new Vector2(0,0),damage=10,radius=.1f,life=2,friendly=true});box.BossPatterns.TickShots(0);
   Check(box.bossHp<hpShot&&box.shots.Count==0,"actual friendly projectile uses new bounds");
   foreach(string id in new[]{"cleave","shield","kick","slash"})foreach(int face in new[]{-1,1})foreach(int level in new[]{-2,-1,0,1,2})foreach(bool rage in new[]{false,true})
   {
    var g=Game(0,level,rage);var p=P(g,id);float reach=ArakusGeometry.Reach(g.spec,p),height=ArakusGeometry.AttackHeight(g.spec,p);
    Enemy(id,face,reach-.01f,0,false,false,true,level,rage);
    Enemy(id,face,reach+.01f,0,false,false,false,level,rage);
    Enemy(id,face,reach-.01f,height-.01f,false,false,true,level,rage);
    Enemy(id,face,reach-.01f,height+.01f,false,false,false,level,rage);
    Enemy(id,face,-.31f,0,false,false,false,level,rage);
    Enemy(id,face,reach-.01f,0,false,true,false,level,rage);
    if(p.parryable)Enemy(id,face,reach-.01f,0,true,false,false,level,rage);
    g.spec=JsonUtility.FromJson<BossSpec>(JsonUtility.ToJson(g.spec));g.spec.patterns=new[]{Array.Find(g.spec.patterns,t=>t.id==id)};
    g.player=new Vector2(face*(reach+1),0);g.BossPatterns.SelectPattern(reach+1);Check(g.current!=null&&g.current.id==id,id+" selection distance expanded");
    float[] wind={1.3f,1.15f,1,.9f,.8f},damage={.8f,.9f,1,1.1f,1.2f};
    Check(Near(g.windup,Math.Max(.4f,p.windup*wind[level+2])/(rage?1.5f:1))&&Near(g.patternDamage,p.damage*damage[level+2]),"DDA and Rage preserved");
   }
   foreach(int face in new[]{-1,1})foreach(bool hit in new[]{false,true})foreach(bool rage in new[]{false,true})foreach(int level in new[]{-2,-1,0,1,2})
   {
    var g=Game(0,level,rage);var p=P(g,"rush");g.player=new Vector2(face*(hit?1.9f:2.1f),0);g.bossFace=face;g.BossPatterns.BeginPattern(p);g.bossFace=face;
    g.patternAge=g.windup-.001f;g.BossPatterns.TickBoss(0);g.patternAge=g.windup;float hp=g.playerHp;g.BossPatterns.TickBoss(0);
    Check((g.playerHp<hp)==hit,"actual ShieldRush expanded contact "+face+" "+hit);
   }
   foreach(int face in new[]{-1,1})
   {
    var g=Game();g.boss=new Vector2(-face*5,0);g.player=new Vector2(face*5,0);g.bossFace=face;g.BossPatterns.BeginPattern(P(g,"charge"));g.patternAge=g.windup;g.BossPatterns.ExecutePattern();g.BossPatterns.TickBoss(.1f);
    Check(g.bossStagger>3&&(face==1?!g.pillarLeft:!g.pillarRight),"pillar charge3.2s and side unchanged");
   }
   foreach(int boss in new[]{1,2,3,4}){
    var g=Game(boss);Check(Near(g.BossWidth,boss==3?.8f:.65f)&&Near(g.BossHeight,2.2f),"other boss body unchanged");
    foreach(var p in g.spec.patterns)Check(ArakusGeometry.Reach(g.spec,p)==p.range&&ArakusGeometry.SelectionMax(g.spec,p)==p.maxRange&&ArakusGeometry.AttackHeight(g.spec,p)==2.2f,"other pattern unchanged");
    foreach(float x in new[]{-2f,0f,1f,2f})foreach(float y in new[]{-1f,1f,3f})Check(g.BossShotOverlap(new Vector2(x,y),.8f)==(Vector2.Distance(new Vector2(x,y),g.boss+Vector2.up*1.2f)<1.2f),"other projectile bounds unchanged");
   }
   var ar=Game();foreach(string id in new[]{"sweep","charge","throw","slam","step","rage"})Check(ArakusGeometry.Reach(ar.spec,P(ar,id))==P(ar,id).range&&ArakusGeometry.SelectionMax(ar.spec,P(ar,id))==P(ar,id).maxRange,"excluded pattern range unchanged "+id);
   foreach(int sign in new[]{-1,1})foreach(float root in new[]{1.4f,2.2f})Check(Near(sign*root*(ArakusGeometry.LegacyEffectScale/(sign*root)),1.4f),"props world scale algebra unchanged");
   Console.WriteLine("ARAKUS_SIZE_TESTS_PASSED "+n);return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
 }
}
