using System;
using System.Collections.Generic;

namespace ReversalTower
{
    [Serializable] public sealed class TutorialAssessment
    {
        public int schemaVersion=1;
        public bool completed,skipped,isFirstAssessment,valid;
        public string group="normal",completedAtUtc="";
        public float elapsedDurationSeconds,explanationSeconds,pausedSeconds;
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
        public bool AwaitingThreatFeedback { get; private set; }
        readonly TutorialScoringRules rules;
        float taskTime;
        bool actionConfirmed,ultimateActivated;
        readonly HashSet<long> resolvedThreats=new HashSet<long>();
        public TutorialSession(bool replay,TutorialScoringRules scoring=null)
        { Replay=replay;rules=scoring??new TutorialScoringRules();Assessment=new TutorialAssessment{isFirstAssessment=!replay}; }
        public void Tick(float delta,bool paused,bool instructionFeedback=false)
        {
            if(Finished||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0)return;
            Assessment.elapsedDurationSeconds+=delta;
            if(paused){Assessment.pausedSeconds+=delta;return;}
            if(Explaining||instructionFeedback){Assessment.explanationSeconds+=delta;return;}
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
        public void ResolveThreat(long id,bool parryable,bool threatenedAtStart,bool success,bool hit,bool showFeedback=false)
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
            if(success&&((Step==TutorialStep.Parry&&parryable)||(Step==TutorialStep.Dodge&&!parryable&&!hit)))
            {
                if(showFeedback)AwaitingThreatFeedback=true;
                else Advance();
            }
        }
        public void CompleteThreatFeedback()
        {
            if(AwaitingThreatFeedback){AwaitingThreatFeedback=false;Advance();}
        }
        public void GaugeChanged(float charge){if(!FreezeGameplay&&Step==TutorialStep.Charge&&charge>=100)Advance();}
        public void ConfirmEmpoweredSwordHit(bool actualCollision,bool empowermentActive)
        {
            if(FreezeGameplay||Step!=TutorialStep.Ultimate||!ultimateActivated||!actualCollision||!empowermentActive)return;
            Assessment.ultimateExecutionSuccess=true;Assessment.completed=true;Assessment.valid=true;
            Score(Assessment);Finish();
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
        public TutorialAssessment Preview()
        {
            if(Finished)return Assessment;
            var a=new TutorialAssessment{schemaVersion=Assessment.schemaVersion,isFirstAssessment=Assessment.isFirstAssessment,
                parryAttempts=Assessment.parryAttempts,parrySuccesses=Assessment.parrySuccesses,dodgeAttempts=Assessment.dodgeAttempts,dodgeSuccesses=Assessment.dodgeSuccesses,
                damageHits=Assessment.damageHits,retryCount=Assessment.retryCount,skillActionsCompleted=Assessment.skillActionsCompleted,inputMistakes=Assessment.inputMistakes,
                activeDurationSeconds=Assessment.activeDurationSeconds,elapsedDurationSeconds=Assessment.elapsedDurationSeconds,explanationSeconds=Assessment.explanationSeconds,pausedSeconds=Assessment.pausedSeconds};
            Score(a);return a;
        }
        public string WeightSummary=>"패링 "+rules.parryWeight+" / 회피 "+rules.dodgeWeight+" / 조작 "+rules.controlsWeight+" / 수행시간 "+rules.speedWeight;
        public string EvidenceSummary=>"패링 기회 "+Assessment.parryAttempts+"/"+rules.minimumExpertParryOpportunities+", 회피 "+Assessment.dodgeAttempts+"/"+rules.confidenceDodgeOpportunities+" · 초기 분류는 잠정 규칙";
        void Score(TutorialAssessment value)
        {
            float p=value.parrySuccesses/(float)Math.Max(1,value.parryAttempts);
            float d=value.dodgeSuccesses/(float)Math.Max(1,value.dodgeAttempts);
            float c=value.skillActionsCompleted/(float)Math.Max(1,value.skillActionsCompleted+value.inputMistakes);
            float speed=Math.Min(1,rules.referenceActiveSeconds/Math.Max(1,value.activeDurationSeconds));
            float total=rules.parryWeight+rules.dodgeWeight+rules.controlsWeight+rules.speedWeight;
            value.score=total>0?(p*rules.parryWeight+d*rules.dodgeWeight+c*rules.controlsWeight+speed*rules.speedWeight)/total:0;
            bool evidence=value.parryAttempts>=rules.minimumExpertParryOpportunities&&value.dodgeAttempts>=rules.minimumExpertDodgeOpportunities;
            bool expertEvidence=evidence&&value.parrySuccesses>=rules.minimumExpertParrySuccesses&&value.dodgeSuccesses>=1;
            float parryEvidence=Math.Min(1,value.parryAttempts/(float)Math.Max(1,rules.minimumExpertParryOpportunities));
            float dodgeEvidence=Math.Min(1,value.dodgeAttempts/(float)Math.Max(1,rules.confidenceDodgeOpportunities));
            value.confidence=Math.Min(1,.35f*parryEvidence+.35f*dodgeEvidence+.2f*Math.Min(1,value.skillActionsCompleted/9f)+.1f);
            if(!evidence)value.confidence=Math.Min(.49f,value.confidence);
            else if(value.dodgeAttempts<rules.confidenceDodgeOpportunities)value.confidence=Math.Min(.75f,value.confidence);
            value.group=!evidence?"normal":value.score<rules.noviceBelow?"novice":expertEvidence&&value.score>=rules.expertAt?"expert":"normal";
        }
    }
}
