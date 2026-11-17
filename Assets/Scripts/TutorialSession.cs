using System;
using System.Collections.Generic;

namespace ReversalTower
{
    [Serializable] public sealed class TutorialAssessment
    {
        public int schemaVersion=1;
        public bool completed,skipped,isFirstAssessment,valid;
        public string group="normal",completedAtUtc="";
        public float score,confidence,activeDurationSeconds,ultimateChargeSeconds,firstParrySeconds,firstDodgeSeconds;
        public int parryAttempts,parrySuccesses,dodgeAttempts,dodgeSuccesses,damageHits,retryCount,skillActionsCompleted,inputMistakes;
        public bool firstValidParrySuccess,ultimateExecutionSuccess;
        public List<TutorialTaskMeasurement> tasks=new List<TutorialTaskMeasurement>();
    }
    [Serializable] public sealed class TutorialTaskMeasurement { public string task; public float seconds; public int successes,retries; }
    [Serializable] public sealed class TutorialScoringRules
    {
        public float parryWeight=.35f,dodgeWeight=.35f,controlsWeight=.2f,speedWeight=.1f;
        public float noviceBelow=.40f,expertAt=.85f,referenceActiveSeconds=120;
        public int minimumExpertParryOpportunities=3,minimumExpertDodgeOpportunities=1,minimumExpertParrySuccesses=3;
        public int confidenceDodgeOpportunities=3;
    }
    public enum TutorialStep { Introduction,PlayerHp,EnemyHp,Skills,UltimateGauge,Telegraphs,Q,W,E,Move,Jump,Attack,Dash,Potion,UltimateExplanation,Parry,Dodge,Charge,Ultimate,Complete }
    public enum TutorialAction { Q,W,E,Move,Jump,Attack,Dash,Potion,Ultimate }

    // Pure event-driven state: callers report completed gameplay events, NEVER raw key presses.
    // No references to saves, cloud queues, rewards, boss history or LinUCB.
    public sealed class TutorialSession
    {
        public TutorialStep Step { get; private set; }
        public bool Explaining { get; private set; }=true;
        public bool Finished { get; private set; }
        public bool Replay { get; private set; }
        public TutorialAssessment Assessment { get; private set; }
        public bool FreezeGameplay => Explaining||Finished;
        readonly TutorialScoringRules rules;
        float taskTime;
        bool actionConfirmed,ultimateActivated;
        readonly HashSet<long> resolvedThreats=new HashSet<long>();
        public TutorialSession(bool replay,TutorialScoringRules scoring=null)
        { Replay=replay;rules=scoring??new TutorialScoringRules();Assessment=new TutorialAssessment{isFirstAssessment=!replay}; }
        public void Tick(float delta,bool paused)
        {
            if(FreezeGameplay||paused||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0)return;
            Assessment.activeDurationSeconds+=delta;taskTime+=delta;
            if(Step==TutorialStep.Charge)Assessment.ultimateChargeSeconds+=delta;
        }
        public void Acknowledge()
        {
            if(!Explaining||Finished)return;
            if(Step<=TutorialStep.Telegraphs||Step==TutorialStep.UltimateExplanation)Advance();
            else Explaining=false;
        }
        public void InvalidInput(){if(!FreezeGameplay)Assessment.inputMistakes++;}
        // One edge-triggered input batch per frame. Free practice and reading are not mistakes.
        // This metric measures the wrong control in a guided task, not motor disability or reading speed.
        public void RecordControlInputs(params TutorialAction[] pressed)
        {
            if(FreezeGameplay||Step<TutorialStep.Q||Step>TutorialStep.Potion||pressed==null)return;
            foreach(var action in pressed)if(!Matches(action)){InvalidInput();return;}
        }
        public void ConfirmAction(TutorialAction action,bool animationFinished,float actualHealing=0)
        {
            if(FreezeGameplay)return;
            if(action==TutorialAction.Ultimate&&Step==TutorialStep.Ultimate){ultimateActivated=true;return;}
            if(!Matches(action))return;
            if(action==TutorialAction.Potion&&actualHealing<=0)return;
            actionConfirmed=true;
            if(animationFinished)CompleteAction();
        }
        public void ActionAnimationFinished(){if(!FreezeGameplay&&actionConfirmed)CompleteAction();}
        void CompleteAction(){Assessment.skillActionsCompleted++;Advance();}
        bool Matches(TutorialAction a)
        { return Step.ToString()==a.ToString(); }
        public void ResolveThreat(long id,bool parryable,bool threatenedAtStart,bool success,bool hit)
        {
            if(FreezeGameplay||!threatenedAtStart||!resolvedThreats.Add(id))return;
            if(Step!=TutorialStep.Parry&&Step!=TutorialStep.Dodge&&Step!=TutorialStep.Charge)return;
            if(parryable)
            {
                Assessment.parryAttempts++;
                if(success){Assessment.parrySuccesses++;if(Assessment.parrySuccesses==1){Assessment.firstParrySeconds=taskTime;Assessment.firstValidParrySuccess=Assessment.parryAttempts==1;}}
                else Assessment.retryCount++;
            }
            else
            {
                Assessment.dodgeAttempts++;
                if(success&&!hit){Assessment.dodgeSuccesses++;if(Assessment.dodgeSuccesses==1)Assessment.firstDodgeSeconds=taskTime;}
                else Assessment.retryCount++;
            }
            if(hit)Assessment.damageHits++;
            if(Step==TutorialStep.Parry&&parryable&&success)Advance();
            else if(Step==TutorialStep.Dodge&&!parryable&&success&&!hit)Advance();
        }
        public void GaugeChanged(float charge){if(!FreezeGameplay&&Step==TutorialStep.Charge&&charge>=100)Advance();}
        public void ConfirmEmpoweredSwordHit(bool actualCollision,bool empowermentActive)
        {
            if(FreezeGameplay||Step!=TutorialStep.Ultimate||!ultimateActivated||!actualCollision||!empowermentActive)return;
            Assessment.ultimateExecutionSuccess=true;Assessment.completed=true;Assessment.valid=true;
            Score();Finish();
        }
        public void Skip()
        {
            if(Finished)return;
            Assessment.skipped=true;Assessment.completed=false;Assessment.valid=false;
            Assessment.group="normal";Assessment.score=Assessment.confidence=0;Finish();
        }
        void Finish(){Assessment.completedAtUtc=DateTime.UtcNow.ToString("o");Step=TutorialStep.Complete;Finished=true;Explaining=false;}
        void Advance()
        {
            Assessment.tasks.Add(new TutorialTaskMeasurement{task=Step.ToString(),seconds=taskTime,successes=1});
            Step++;Explaining=true;taskTime=0;actionConfirmed=false;
        }
        void Score()
        {
            float p=Assessment.parrySuccesses/(float)Math.Max(1,Assessment.parryAttempts);
            float d=Assessment.dodgeSuccesses/(float)Math.Max(1,Assessment.dodgeAttempts);
            float c=Assessment.skillActionsCompleted/(float)Math.Max(1,Assessment.skillActionsCompleted+Assessment.inputMistakes);
            float speed=Math.Min(1,rules.referenceActiveSeconds/Math.Max(1,Assessment.activeDurationSeconds));
            float total=rules.parryWeight+rules.dodgeWeight+rules.controlsWeight+rules.speedWeight;
            Assessment.score=total>0?(p*rules.parryWeight+d*rules.dodgeWeight+c*rules.controlsWeight+speed*rules.speedWeight)/total:0;
            bool evidence=Assessment.parryAttempts>=rules.minimumExpertParryOpportunities&&Assessment.dodgeAttempts>=rules.minimumExpertDodgeOpportunities;
            bool expertEvidence=evidence&&Assessment.parrySuccesses>=rules.minimumExpertParrySuccesses&&Assessment.dodgeSuccesses>=1;
            float parryEvidence=Math.Min(1,Assessment.parryAttempts/(float)Math.Max(1,rules.minimumExpertParryOpportunities));
            float dodgeEvidence=Math.Min(1,Assessment.dodgeAttempts/(float)Math.Max(1,rules.confidenceDodgeOpportunities));
            Assessment.confidence=Math.Min(1,.35f*parryEvidence+.35f*dodgeEvidence+.2f*Math.Min(1,Assessment.skillActionsCompleted/9f)+.1f);
            if(!evidence)Assessment.confidence=Math.Min(.49f,Assessment.confidence);
            else if(Assessment.dodgeAttempts<rules.confidenceDodgeOpportunities)Assessment.confidence=Math.Min(.75f,Assessment.confidence);
            Assessment.group=!evidence?"normal":Assessment.score<rules.noviceBelow?"novice":expertEvidence&&Assessment.score>=rules.expertAt?"expert":"normal";
        }
    }
}
