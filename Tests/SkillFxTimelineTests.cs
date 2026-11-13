using System;
using ReversalTower;
public static class SkillFxTimelineTests
{
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
 static SkillFxTimeline.Snapshot Player(string action,float elapsed,float clock=1)
 =>new SkillFxTimeline.Snapshot{combat=true,clock=clock,action=action,total=action=="W"?.9f:1.3f,remaining=(action=="W"?.9f:1.3f)-elapsed};
 public static void Main()
 {
  var t=new SkillFxTimeline();var e=new SkillFxTimeline.Emission[2];
  Check(t.Observe(Player("W",.14f),e)==0,"W not before windup");
  Check(t.Observe(Player("W",.16f),e)==1&&e[0].kind==SkillFxTimeline.Kind.Spin,"W starts crossing .15");
  Check(Math.Abs(e[0].age-.01f)<.0001f,"W preserves subframe age");
  Check(t.Observe(Player("W",.16f),e)==0,"paused repeats no duplicate");
  Check(t.Observe(Player("W",.35f),e)==0,"W no double emit at hit");
  Check(SkillFxTimeline.Frame(.35f-SkillFxTimeline.Offset(SkillFxTimeline.Kind.Spin),25)==6,"W first complete ring frame at damage");
  Check(SkillFxTimeline.Frame(.7f-SkillFxTimeline.Offset(SkillFxTimeline.Kind.Execution),58)==11,"E first impact frame at damage");
  var cancel=Player("W",.36f);cancel.remaining=0;t.Observe(cancel,e);Check(!t.PlayerActive,"cancel ends active cast");
  Check(t.Observe(Player("W",.2f,2),e)==1,"same action recast emits");
  t.Reset();Check(t.Observe(Player("E",.33f),e)==0,"E not before windup");
  Check(t.Observe(Player("E",.38f),e)==1&&e[0].kind==SkillFxTimeline.Kind.Execution,"E starts crossing .3333");
  Check(Math.Abs(SkillFxTimeline.Offset(SkillFxTimeline.Kind.Execution)+SkillFxTimeline.Peak(SkillFxTimeline.Kind.Execution)-.7f)<.0001,"E peak at actual hit");
  Check(Math.Abs(SkillFxTimeline.Offset(SkillFxTimeline.Kind.Spin)+SkillFxTimeline.Peak(SkillFxTimeline.Kind.Spin)-.35f)<.0001,"W peak at actual hit");
  var hurt=Player("E",.4f);hurt.interrupted=true;Check(t.Observe(hurt,e)==0&&!t.PlayerActive,"interrupted windup inactive");
  var death=Player("E",.5f);death.dead=true;Check(t.Observe(death,e)==0&&t.ResetThisFrame,"death clears");
  var lobby=Player("W",.4f);lobby.combat=false;Check(t.Observe(lobby,e)==0&&t.ResetThisFrame,"lobby clears");
  t.Observe(Player("W",.3f,9),e);Check(t.Observe(Player("W",.2f,.2f),e)==1&&t.ResetThisFrame,"clock rewind resets then new cast");
  var p=new object();var s=new SkillFxTimeline.Snapshot{combat=true,clock=1,bossIndex=0,bossPattern="slam",patternToken=p,bossAge=2,bossStruck=false};
  t.Reset();Check(t.Observe(s,e)==0,"slam does not play airborne");s.bossStruck=true;Check(t.Observe(s,e)==1&&e[0].kind==SkillFxTimeline.Kind.Landing,"slam plays at struck edge");
  Check(t.Observe(s,e)==0,"same landing no duplicate");s.bossAge=.1f;s.bossStruck=false;t.Observe(s,e);s.bossAge=2;s.bossStruck=true;Check(t.Observe(s,e)==1,"same Pattern object next cast works");
  t.Reset();s.bossIndex=1;Check(t.Observe(s,e)==0,"other boss unaffected");
  Check(SkillFxTimeline.Frame(-1,25)==0&&SkillFxTimeline.Frame(999,25)==24,"frame clamps");
  Check(SkillFxTimeline.Frame(.17f,25)==5,"frame time mapping");
  Check(SkillFxTimeline.Alpha(SkillFxTimeline.Kind.Landing,0)==1&&SkillFxTimeline.Alpha(SkillFxTimeline.Kind.Landing,2)==0,"alpha clamps");
  Check(SkillFxTimeline.Alpha(SkillFxTimeline.Kind.Landing,.9f)>.49f&&SkillFxTimeline.Alpha(SkillFxTimeline.Kind.Landing,.9f)<.51f,"landing tail fades");
  Console.WriteLine("SKILL_FX_TIMELINE_PASS "+checks+" checks; presentation observer only; not Unity render validation.");
 }
}
