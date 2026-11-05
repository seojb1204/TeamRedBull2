using System;
using ReversalTower;
public static class ArakusAnimationTests
{
 static int n;static void Check(bool b,string s){if(!b)throw new Exception(s);n++;}
 public static void Main()
 {
  int body=0,all=0;foreach(var d in ArakusAnimationSelection.Definitions){all+=d.ms.Length;if(d.group=="캐릭터")body+=d.ms.Length;foreach(int ms in d.ms)Check(ms>0,"positive frame durations");}
  Check(all==179&&body==77,"supplied frame counts");
  var i=new ArakusAnimationSelection.Input{combat=true,windup=1,recovery=.8f,tempo=1};
  Check(ArakusAnimationSelection.Sample(i,0).state=="Idle","resting boss idle");i.walking=true;Check(ArakusAnimationSelection.Sample(i,.3f).state=="Walk","chase walks");
  foreach(string id in new[]{"slash","shield","kick","cleave"})foreach(float windup in new[]{.4f,.85f,1.3f})
  {
   i.pattern=id;i.windup=windup;i.age=windup-.0001f;
   Check(ArakusAnimationSelection.Sample(i,0).frame<2,"windup must not show contact "+id);
   i.age=windup;Check(ArakusAnimationSelection.Sample(i,0).frame==2,"actual windup is contact "+id);
   i.age=windup+i.recovery;var p=ArakusAnimationSelection.Sample(i,0);Check(p.frame==ArakusAnimationSelection.Find(p.state).ms.Length-1,"recovery ends at ready "+id);
  }
  foreach(float tempo in new[]{1f,1.5f})
  {
   i=new ArakusAnimationSelection.Input{combat=true,pattern="throw",tempo=tempo,windup=.6f/tempo,recovery=.5f/tempo};
   i.age=i.windup-.001f;Check(ArakusAnimationSelection.Sample(i,0).frame==0,"shared throw windup");
   i.age=i.windup;i.shieldOut=true;Check(ArakusAnimationSelection.Sample(i,0).frame==1,"release has empty hand");
   i.age=i.windup+.4f/tempo;var dash=ArakusAnimationSelection.Sample(i,0);Check(dash.frame==2||dash.frame==3,"dash while shield away");
   i.struck=true;i.age=i.windup+.8f/tempo;Check(ArakusAnimationSelection.Sample(i,0).frame==4,"arrival slash before actual catch");
   i.shieldOut=false;i.shieldCaught=true;Check(ArakusAnimationSelection.Sample(i,0).frame==5,"actual catch re-equips shield");
   i.pattern="slam";i.shieldOut=true;i.shieldCaught=false;i.struck=false;i.age=4;Check(ArakusAnimationSelection.Sample(i,0).frame==1,"slam waits for actual returning shield, not fixed doc time");
   i.shieldOut=false;i.shieldCaught=true;Check(ArakusAnimationSelection.Sample(i,0).frame==2,"catch before leap");
   i.leaping=true;i.leapStart=4;i.age=4+.2f/tempo;Check(ArakusAnimationSelection.Sample(i,0).frame==4,"game leap selects airborne posture");
   i.leaping=false;i.struck=true;Check(ArakusAnimationSelection.Sample(i,0).frame==5,"actual landing selects ground impact");
   i=new ArakusAnimationSelection.Input{combat=true,pattern="sweep",tempo=tempo,windup=1/tempo,recovery=.8f/tempo};
   i.age=i.windup;Check(ArakusAnimationSelection.Sample(i,0).frame==2,"first sweep contact");
   i.age=i.windup+.6f/tempo;Check(ArakusAnimationSelection.Sample(i,0).frame==3,"overhead preparation between hits");
   i.age=i.windup+1/tempo;Check(ArakusAnimationSelection.Sample(i,0).frame==4,"second sweep contact");
  }
  i=new ArakusAnimationSelection.Input{combat=true,pattern="rage",windup=2.5f,recovery=2.5f,tempo=1,age=2.49f};Check(ArakusAnimationSelection.Sample(i,0).frame==1,"wrath kneels before buff");
  i.age=2.5f;Check(ArakusAnimationSelection.Sample(i,0).frame==2,"wrath releases at actual buff");i.age=4;Check(ArakusAnimationSelection.Sample(i,0).frame==3,"wrath stands after release");
  i.stagger=1;Check(ArakusAnimationSelection.Sample(i,0).state=="Stagger","parry interrupts attack pose");i.groggy=true;Check(ArakusAnimationSelection.Sample(i,.5f).state=="Groggy","pillar collision has distinct groggy");
  i.dead=true;i.combat=false;Check(ArakusAnimationSelection.Sample(i,3).frame==4,"result death finishes despite no combat");
  i=new ArakusAnimationSelection.Input{combat=true,shieldOut=true,pattern="slash",stagger=.45f};Check(ArakusAnimationSelection.Sample(i,.1f).state=="ShieldThrowing_DashAttack"&&ArakusAnimationSelection.Sample(i,.1f).frame==1,"no duplicated held shield when projectile is away");
  i=new ArakusAnimationSelection.Input{combat=false,pattern="slash"};Check(ArakusAnimationSelection.Sample(i,0).state=="Idle","lobby ignores old combat state");
  foreach(var d in ArakusAnimationSelection.Definitions)foreach(float t in new[]{-1f,0f,.07f,1f,100f}){int f=ArakusAnimationSelection.NativeFrame(d.id,t,true);Check(f>=0&&f<d.ms.Length,"loop sample stays in sheet "+d.id);}
  Console.WriteLine("ARAKUS_ANIMATION_TESTS_PASSED "+n);
 }
}
