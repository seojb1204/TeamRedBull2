using System;
using ReversalTower;
class Program
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static TutorialAssessment Complete(int parryFails,int dodgeFails,int chargeParries)
 {
  var t=new TutorialSession(false);long id=1;for(int i=0;i<6;i++)t.Acknowledge();
  foreach(var action in new[]{TutorialAction.Q,TutorialAction.W,TutorialAction.E,TutorialAction.Move,TutorialAction.Jump,TutorialAction.Attack,TutorialAction.Dash}){t.Acknowledge();t.Tick(1,false);t.ConfirmAction(action,true);}
  t.Acknowledge();t.ConfirmAction(TutorialAction.Potion,true,10);t.Acknowledge();t.Acknowledge();
  for(int i=0;i<parryFails;i++)t.ResolveThreat(id++,true,true,false,true);t.ResolveThreat(id++,true,true,true,false);
  t.Acknowledge();for(int i=0;i<dodgeFails;i++)t.ResolveThreat(id++,false,true,false,true);t.ResolveThreat(id++,false,true,true,false);
  t.Acknowledge();for(int i=0;i<chargeParries;i++)t.ResolveThreat(id++,true,true,true,false);
  t.GaugeChanged(100);t.Acknowledge();t.ConfirmAction(TutorialAction.Ultimate,true);t.ConfirmEmpoweredSwordHit(true,true);return t.Assessment;
 }
 static void Main()
 {
  var s=new TutorialSession(false);s.Tick(30,false);Check(s.Assessment.activeDurationSeconds==0,"reading excluded");
  for(int i=0;i<6;i++)s.Acknowledge();Check(s.Step==TutorialStep.Q,"ordered intro");
  s.ConfirmAction(TutorialAction.Q,true);Check(s.Step==TutorialStep.Q,"overlay blocks action");
  s.Acknowledge();s.ConfirmAction(TutorialAction.W,true);Check(s.Step==TutorialStep.Q,"wrong action rejected");
  s.RecordControlInputs(TutorialAction.W,TutorialAction.E);Check(s.Assessment.inputMistakes==1,"wrong input batch counted once");
  s.RecordControlInputs(TutorialAction.Q);Check(s.Assessment.inputMistakes==1,"correct input not penalized");
  s.Tick(10,true);Check(s.Assessment.activeDurationSeconds==0,"pause excluded");
  s.Tick(1,false);Check(s.Assessment.activeDurationSeconds==1,"active time counted");
  s.ConfirmAction(TutorialAction.Q,false);Check(s.Step==TutorialStep.Q,"animation waits");s.ActionAnimationFinished();
  foreach(var a in new[]{TutorialAction.W,TutorialAction.E,TutorialAction.Move,TutorialAction.Jump,TutorialAction.Attack,TutorialAction.Dash}){s.Acknowledge();s.ConfirmAction(a,true);}
  s.Acknowledge();s.ConfirmAction(TutorialAction.Potion,true,0);Check(s.Step==TutorialStep.Potion,"real healing required");
  s.ConfirmAction(TutorialAction.Potion,true,10);s.Acknowledge();Check(s.Step==TutorialStep.Parry,"parry stage");
  s.RecordControlInputs(TutorialAction.W);Check(s.Assessment.inputMistakes==1,"reading inputs excluded");
  s.Acknowledge();s.RecordControlInputs(TutorialAction.W);Check(s.Assessment.inputMistakes==1,"free parry practice inputs excluded");
  s.ResolveThreat(1,true,true,false,true);Check(s.Step==TutorialStep.Parry,"parry fail repeats");
  s.ResolveThreat(1,true,true,true,false);Check(s.Assessment.parryAttempts==1,"duplicate threat rejected");
  s.ResolveThreat(2,true,true,true,false);Check(s.Step==TutorialStep.Dodge,"real parry advances");
  s.Acknowledge();s.ResolveThreat(3,false,false,true,false);Check(s.Step==TutorialStep.Dodge,"outside threat not dodge");
  s.ResolveThreat(4,false,true,true,false);Check(s.Step==TutorialStep.Charge,"dodge advances");
  s.Acknowledge();s.GaugeChanged(99);Check(s.Step==TutorialStep.Charge,"charge below100 waits");s.GaugeChanged(100);s.Acknowledge();
  s.ConfirmEmpoweredSwordHit(true,true);Check(!s.Finished,"R activation required");
  s.ConfirmAction(TutorialAction.Ultimate,true);s.ConfirmEmpoweredSwordHit(false,true);Check(!s.Finished,"collision required");
  s.ConfirmEmpoweredSwordHit(true,false);Check(!s.Finished,"empowerment required");
  s.ConfirmEmpoweredSwordHit(true,true);Check(s.Finished&&s.Assessment.valid,"actual empowered hit completes");
  Check(s.Assessment.group!="expert","single lucky dodge not expert");
  var skipped=new TutorialSession(false);skipped.Skip();Check(skipped.Assessment.skipped&&!skipped.Assessment.valid&&skipped.Assessment.group=="normal","skip invalid neutral");
  var replay=new TutorialSession(true);Check(!replay.Assessment.isFirstAssessment,"replay separate");
  var expert=Complete(0,0,2);Check(expert.group=="expert"&&expert.parrySuccesses==3,"three actual parries plus one dodge can reach expert without forced repeat");
  Check(expert.confidence<=.75f,"single dodge keeps expert confidence limited");
  var ordinary=Complete(0,0,0);Check(ordinary.group=="normal"&&ordinary.confidence<.5f,"single lucky parry remains low confidence normal");
  Check(Complete(8,8,0).group=="novice","repeated observed failure can reach novice");
  Console.WriteLine(passed+" PASS, 0 FAIL");
 }
}
