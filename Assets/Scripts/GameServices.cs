using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace ReversalTower
{
    public sealed class DdaLiveSnapshot
    {
        public bool tutorial,hasAttack;public string pattern,patternName,reason,source,dbStatus,uploadStatus,localStatus,weights,evidence;
        public int level;public float windup,damage,cooldown,cooldownRemaining,windupScale,damageScale,cooldownScale;
        public TutorialAssessment assessment;
    }
    [Serializable] public sealed class DdaDecisionTrace
    {
        public string pattern,source,reason,nextReason,skipReason,aPrepare,bPrepare,aBefore,bBefore,aAfter,bAfter;
        public int priorOpportunities,priorPlayers;public string priorGroup;public bool priorMock,candidateScoresUsed;
        public int previousLevel,assignedLevel,nextLevel,observations,battles,lossBefore,lossAfter,opportunities,parries,dodges,hits,ignored;
        public float rememberedSuccess,observedSuccess;public double reward,aDelta,bDelta;public bool learned;
        public double[] context,candidateScores;public int[] candidates;
    }
    [Serializable] public sealed class DdaTraceReport {public string boss;public bool enabled,clear,abandoned;public List<DdaDecisionTrace> patterns=new List<DdaDecisionTrace>();}

    // Owns per-battle DDA decisions and observations. Legacy v2 policy is preserved;
    // migrating to the design document's shared 18-feature model is a separate milestone.
    public sealed class DdaBattleSession
    {
        public bool Enabled { get; private set; }
        bool completed;
        public DdaTraceReport Trace {get;private set;}=new DdaTraceReport();
        public List<PatternStats> Stats { get; private set; } = new List<PatternStats>();
        public Dictionary<string,double[]> Contexts { get; private set; } = new Dictionary<string,double[]>();
        // 패턴마다 이번 난이도를 고른 이유 (DDA 패널·발표용)
        public Dictionary<string,string> Reasons { get; private set; } = new Dictionary<string,string>();
        // Diagnostic snapshots only; late receive never rewrites active assignments.
        public long CommunityRevisionAtPrepare { get; private set; }
        public string CommunitySnapshotAtPrepare { get; private set; }
        public Dictionary<string,string> Sources { get; private set; } = new Dictionary<string,string>();

        public void Prepare(SaveData save, BossSpec boss, bool disable)
        {
            completed=false;save.NormalizeDda();
            Enabled = save.dda && !disable;
            Stats = new List<PatternStats>();
            Contexts = new Dictionary<string,double[]>();
            Reasons = new Dictionary<string,string>();
            Sources = new Dictionary<string,string>();
            Trace=new DdaTraceReport{boss=boss.id,enabled=Enabled};
            CommunityRevisionAtPrepare=CommunityStats.Revision;
            CommunitySnapshotAtPrepare=CommunityStats.ReceiveSummary;
            foreach (Pattern pattern in boss.patterns)
            {
                double[] context;
                string reason;
                int level = Decide(save, boss, pattern, Enabled, out context, out reason);
                Stats.Add(new PatternStats { id=pattern.id, level=level });
                Contexts[pattern.id] = context;
                Reasons[pattern.id] = reason;
                Sources[pattern.id]=InitialDifficulty.Source(save,boss,pattern,Enabled);
                var l=save.Learning(boss.id);var memory=l.Pattern(pattern.id);var candidates=new List<int>();var scores=new List<double>();
                for(int candidate=Math.Max(-2,memory.level-1);candidate<=Math.Min(2,memory.level+1);candidate++){candidates.Add(candidate);scores.Add(l.model.CandidateScore(context,candidate,memory.level,memory.success));}
                var initial=InitialDifficulty.Resolve(save,boss,pattern);
                Trace.patterns.Add(new DdaDecisionTrace{pattern=pattern.id,candidateScoresUsed=Enabled&&(l.battles>0||memory.observations>0)&&l.lossStreak<3&&memory.success>=.45f&&memory.success<=.9f,priorOpportunities=l.battles==0?initial.opportunities:0,priorPlayers=l.battles==0?initial.players:0,priorGroup=initial.group,priorMock=initial.mock,source=Sources[pattern.id],reason=reason,previousLevel=memory.level,assignedLevel=level,observations=memory.observations,battles=l.battles,lossBefore=l.lossStreak,rememberedSuccess=memory.success,context=(double[])context.Clone(),candidates=candidates.ToArray(),candidateScores=scores.ToArray(),aPrepare=Signature(l.model.matrices),bPrepare=Signature(l.model.vectors)});

            }
        }

        // 지금까지의 기억으로 이 패턴의 레벨을 고른다. Prepare와 DDA 패널의 '다음 판' 미리보기가 같이 쓴다.
        // 학습(Complete)은 하지 않으므로 몇 번 불러도 결과가 바뀌지 않는다.
        public static int Decide(SaveData save, BossSpec boss, Pattern pattern, bool enabled, out double[] context, out string reason)
        {
            BossLearning learning = save.Learning(boss.id);
            PatternMemory memory = learning.Pattern(pattern.id);
            bool personal=learning.battles>0||memory.observations>0;
            var initial=InitialDifficulty.Resolve(save,boss,pattern);
            float success=personal?memory.success:initial.hasStatistics?initial.success:memory.success;
            context=learning.model.Context(success,learning.globalSuccess,memory.level,LobbyCards.Power(save),learning.lossStreak);
            int effectiveBattles=Math.Max(learning.battles,memory.observations>0?1:0);
            int level=!enabled?0:personal?learning.model.Choose(context,memory.level,effectiveBattles,success,learning.lossStreak):initial.level;
            reason=!enabled?"DDA 꺼짐":personal?Reason(learning,success,memory.level):initial.reason;
            return level;
        }

        // BanditModel.Choose와 같은 순서로 판단 이유를 적는다. current = 지난 판 레벨 (이미 끝에 닿았으면 그렇게 적음)
        static string Reason(BossLearning learning, float success, int current)
        {
            string easier = current <= -2 ? " · 이미 가장 쉬움" : " · 한 단계 쉽게";
            string harder = current >= 2 ? " · 이미 가장 어려움" : " · 한 단계 어렵게";
            if (learning.battles == 0&&learning.patterns.TrueForAll(p=>p.observations==0)) return "첫 만남 · 기본 난이도";
            if (learning.lossStreak >= 3) return learning.lossStreak + "연패 보호" + easier;
            if (success < .45f) return "성공률 " + Mathf.RoundToInt(success * 100) + "% 낮음" + easier;
            if (success > .9f) return "성공률 " + Mathf.RoundToInt(success * 100) + "% 높음" + harder;
            return "LinUCB 학습 · 성공률 " + Mathf.RoundToInt(success * 100) + "% (목표 60~80%)";
        }

        // abandoned: 끝까지 싸우지 않은 판(ESC 포기·창 닫기·에디터 정지). 연패·판 수·패턴 성공률 어느 것도 바꾸지 않는다.
        // 테스트하다 끈 판이 쌓여 연패 보호가 걸리거나, 반쯤 싸운 기록이 학습되는 것을 막는다.
        public void Complete(SaveData save, BossSpec boss, bool clear, bool abandoned = false)
        {
            if(completed)return;completed=true;
            Trace.clear=clear;Trace.abandoned=abandoned;
            if (!Enabled || abandoned){foreach(var t in Trace.patterns){Capture(t);t.skipReason=!Enabled?"DDA_OFF":"ABANDONED";t.nextLevel=t.assignedLevel;t.nextReason=t.skipReason;t.lossAfter=t.lossBefore;t.aBefore=t.aAfter=t.aPrepare;t.bBefore=t.bAfter=t.bPrepare;}return;}
            BossLearning learning = save.Learning(boss.id);
            float sum = 0;
            int observed = 0;
            foreach (PatternStats stats in Stats)
            {
                var trace=stats==null?null:Trace.patterns.Find(t=>t.pattern==stats.id);
                if(trace!=null){Capture(trace);trace.aBefore=Signature(learning.model.matrices);trace.bBefore=Signature(learning.model.vectors);trace.aAfter=trace.aBefore;trace.bAfter=trace.bBefore;trace.skipReason=stats.opportunities<1?"NO_OBSERVATIONS":"INVALID_COUNTS_OR_CONTEXT";}
                if(stats==null||stats.opportunities<1||stats.hits<0||stats.parries<0||stats.dodges<0||
                    (long)stats.hits+stats.parries+stats.dodges!=stats.opportunities||stats.level< -2||stats.level>2)continue;
                double[] context;
                if(stats.id==null||!Contexts.TryGetValue(stats.id,out context)||!BanditModel.ValidContext(context))continue;
                PatternMemory memory = learning.Pattern(stats.id);
                double[] beforeA=(double[])learning.model.matrices.Clone(),beforeB=(double[])learning.model.vectors.Clone();
                learning.model.Update(stats.level, context, BanditModel.Reward(stats.Success));
                if(trace!=null){trace.learned=true;trace.skipReason="";trace.reward=BanditModel.Reward(stats.Success);trace.aAfter=Signature(learning.model.matrices);trace.bAfter=Signature(learning.model.vectors);trace.aDelta=Delta(beforeA,learning.model.matrices);trace.bDelta=Delta(beforeB,learning.model.vectors);}
                memory.success = memory.observations == 0 ? stats.Success : Mathf.Lerp(memory.success,stats.Success,.5f);
                memory.level = stats.level;
                memory.observations=memory.observations==int.MaxValue?int.MaxValue:memory.observations+1;
                sum += stats.Success;
                observed++;
            }
            // No eligible observation means no evidence for either learning or loss protection.
            if(observed==0){FinishTrace(save,boss);return;}
            learning.battles=learning.battles==int.MaxValue?int.MaxValue:learning.battles+1;
            learning.lossStreak=clear?0:learning.lossStreak==int.MaxValue?int.MaxValue:learning.lossStreak+1;
            if (observed > 0) learning.globalSuccess = sum / observed;
            FinishTrace(save,boss);
        }
        void Capture(DdaDecisionTrace t){var st=Stats.Find(s=>s!=null&&s.id==t.pattern);if(st==null)return;t.opportunities=st.opportunities;t.parries=st.parries;t.dodges=st.dodges;t.hits=st.hits;t.ignored=st.ignored;t.observedSuccess=st.opportunities>0?st.Success:0;}
        void FinishTrace(SaveData save,BossSpec boss){foreach(var t in Trace.patterns){double[] x;string why;var p=Array.Find(boss.patterns,p=>p.id==t.pattern);if(p==null)continue;t.nextLevel=Decide(save,boss,p,Enabled,out x,out why);t.nextReason=why;t.lossAfter=save.Learning(boss.id).lossStreak;}}
        static string Signature(double[] values){using(var sha=SHA256.Create()){var bytes=new byte[values.Length*8];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
        static double Delta(double[] a,double[] b){double sum=0;for(int i=0;i<a.Length;i++)sum+=(a[i]-b[i])*(a[i]-b[i]);return Math.Sqrt(sum);}

    }

    // Versioned conservative FIRST-meeting prior only. No pseudo-observations or LinUCB updates.
    public static class InitialDifficulty
    {
        public const int Version=1,MinGroupPlayers=3,MinGlobalPlayers=1;
        public sealed class Choice {public int level,opportunities,players;public float success=.7f;public bool hasStatistics,mock;public string source,reason,group;}
        public static bool ValidAssessment(TutorialAssessment a)
        {
            return a!=null&&a.schemaVersion==1&&a.isFirstAssessment&&a.completed&&a.valid&&!a.skipped&&
                (a.group=="novice"||a.group=="normal"||a.group=="expert")&&a.ultimateExecutionSuccess&&
                a.parryAttempts>=1&&a.dodgeAttempts>=1&&a.parrySuccesses>=0&&a.parrySuccesses<=a.parryAttempts&&
                a.dodgeSuccesses>=0&&a.dodgeSuccesses<=a.dodgeAttempts&&a.damageHits>=0&&a.retryCount>=0&&
                BanditModel.Finite(a.score)&&a.score>=0&&a.score<=1&&BanditModel.Finite(a.confidence)&&a.confidence>=0&&a.confidence<=1&&
                BanditModel.Finite(a.activeDurationSeconds)&&a.activeDurationSeconds>0;
        }
        public static bool Enough(CommunityStats.Entry e,int players)=>e!=null&&e.opportunities>=CommunityStats.MinOpportunities&&e.players>=players&&
            BanditModel.Finite(e.successRate)&&e.successRate>=0&&e.successRate<=1;
        public static Choice Resolve(SaveData save,BossSpec boss,Pattern p)
        {
            var a=save.tutorialAssessment;bool valid=ValidAssessment(a);CommunityStats.Entry e;
            var c=new Choice{source="기본값",reason="첫 만남 · 기본 난이도 · 유효 통계/평가 없음",group=valid?a.group:"없음"};
            bool grouped=valid&&a.confidence>=.5f&&CommunityStats.TryGetGroup(boss.id,p.id,a.group,out e)&&Enough(e,MinGroupPlayers);
            if(grouped)CommunityStats.TryGetGroup(boss.id,p.id,a.group,out e);else CommunityStats.TryGet(boss.id,p.id,out e);
            if((grouped||Enough(e,MinGlobalPlayers))&&e!=null)
            {
                c.level=Math.Max(-1,Math.Min(1,e.suggestedLevel));c.success=e.successRate;c.hasStatistics=true;c.opportunities=e.opportunities;c.players=e.players;
                c.source=grouped?"그룹 DB":"전체 DB";c.mock=grouped?CommunityStats.GroupOrigin!="HTTP 수신":CommunityStats.SnapshotOrigin!="HTTP 수신";
                c.reason="첫 만남 · "+c.source+(c.mock?" (메모리/모의)":"")+" · "+e.opportunities+"기회 / "+e.players+"익명 프로필 · 성공률 "+Mathf.RoundToInt(e.successRate*100)+"%";
            }
            else if(valid)
            {
                c.source="튜토리얼";c.level=a.confidence<.5f?0:a.group=="novice"?-1:a.group=="expert"?1:0;
                c.reason="첫 만남 · 튜토리얼 "+a.group+" · confidence "+a.confidence.ToString("F2")+(a.confidence<.5f?" · 관측 부족으로 중립":" · 보수적 초기값 v1 (가설)");
            }
            return c;
        }
        public static string Source(SaveData save,BossSpec boss,Pattern p,bool enabled)
        {
            if(!enabled)return "고정 난이도 (DDA OFF)";
            var l=save.Learning(boss.id);if(l.battles>0||l.Pattern(p.id).observations>0)return "개인 LinUCB/보호 규칙 · 개인 패턴 기록";
            return Resolve(save,boss,p).source;
        }
    }

    // 다른 플레이어들의 패턴 통계 (Supabase의 pattern_difficulty 뷰를 CloudSync가 받아 채움).
    // 비어 있으면 DDA는 원래대로 개인 기록만 쓴다. 이 클래스는 네트워크를 모른다.
    public static class CommunityStats
    {
        public const int MinOpportunities = 20;   // 이보다 적게 쌓인 패턴은 근거 부족으로 쓰지 않음
        public sealed class Entry { public string boss, pattern, group; public int opportunities, players, suggestedLevel; public float successRate, parryRate, hitRate; }
        static readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>();
        static readonly Dictionary<string,Entry> groups=new Dictionary<string,Entry>();
        public static string GroupOrigin {get;private set;}="없음";
        public static string GroupState {get;private set;}="미요청";
        public static void GroupReceiving(){GroupState="수신 중";}
        public static void GroupEndReceive(){if(GroupState=="수신 중")GroupState="수신 중단";}
        public static void GroupFailed(){GroupState="수신 실패/미배포 · 이전 메모리 유지";}
        public static void ReplaceGroups(IEnumerable<Entry> list,bool http=false)
        {
            var next=new Dictionary<string,Entry>();foreach(var e in list)
            {
                if(e==null||string.IsNullOrEmpty(e.boss)||string.IsNullOrEmpty(e.pattern)||(e.group!="novice"&&e.group!="normal"&&e.group!="expert"))throw new ArgumentException("Invalid grouped statistics");
                next[e.boss+"/"+e.pattern+"/"+e.group]=e;
            }
            lock(groups){groups.Clear();foreach(var pair in next)groups[pair.Key]=pair.Value;Revision++;GroupOrigin=http?"HTTP 수신":"메모리/모의";GroupState=http?"수신 완료":"모의/fixture 주입";}
        }
        public static bool TryGetGroup(string boss,string pattern,string group,out Entry entry){lock(groups)return groups.TryGetValue(boss+"/"+pattern+"/"+group,out entry);}
        public static string Status = "DB 연결 안 됨";   // 화면 표시용 (CloudSync가 갱신)
        public static int Count { get { lock (entries) return entries.Count; } }
        public static long Revision { get; private set; }
        public static string ReceiveState { get; private set; } = "미요청";
        public static string SnapshotOrigin { get; private set; } = "없음";
        public static DateTime? LastReceivedUtc { get; private set; }
        public static string ReceiveSummary => ReceiveState+" · 메모리 "+Count+"개 · "+SnapshotOrigin+
            (LastReceivedUtc.HasValue?" · 최근 수신 "+LastReceivedUtc.Value.ToString("HH:mm:ss")+" UTC":" · HTTP 성공 수신 기록 없음")+" · 그룹 "+GroupState+" · 영속 캐시 없음";
        public static void MarkUnavailable(bool automation){ReceiveState=automation?"자동검증 꺼짐":"설정 없음";}
        public static void BeginReceive(){ReceiveState="수신 중";}
        public static void ReceiveFailed(){ReceiveState="수신 실패";}
        public static void EndReceive(){if(ReceiveState=="수신 중")ReceiveState="수신 중단";}
        public static void MarkReceived(){ReceiveState="수신 완료";SnapshotOrigin="HTTP 수신";LastReceivedUtc=DateTime.UtcNow;}
        public static void Replace(IEnumerable<Entry> list)
        {
            // Materialize before replacing so malformed input preserves the previous snapshot.
            var replacement=new Dictionary<string,Entry>();
            foreach(var e in list)
            {
                if(e==null||string.IsNullOrEmpty(e.boss)||string.IsNullOrEmpty(e.pattern))throw new ArgumentException("Invalid community row");
                replacement[e.boss+"/"+e.pattern]=e;
            }
            lock (entries)
            {
                entries.Clear();foreach(var pair in replacement)entries[pair.Key]=pair.Value;
                Revision++;SnapshotOrigin="메모리 주입";
            }
        }
        public static bool TryGet(string boss, string pattern, out Entry entry)
        {
            lock (entries) return entries.TryGetValue(boss + "/" + pattern, out entry);
        }
    }

    // UI-only cache key. Neither matching nor capture invokes decisions or training.
    public sealed class DdaPreviewStamp
    {
        object result;BossLearning learning;bool enabled,captured;long revision;
        public bool Matches(object currentResult,BossLearning currentLearning,bool currentEnabled,long currentRevision)
            =>captured&&ReferenceEquals(result,currentResult)&&ReferenceEquals(learning,currentLearning)&&enabled==currentEnabled&&revision==currentRevision;
        public void Capture(object currentResult,BossLearning currentLearning,bool currentEnabled,long currentRevision)
        {result=currentResult;learning=currentLearning;enabled=currentEnabled;revision=currentRevision;captured=true;}
    }

    // Exactly-once progression settlement. Game flow/presentation do not calculate rewards.
    public sealed class RewardLedger
    {
        bool settled;
        public void Reset() { settled=false; }

        public bool TrySettle(SaveData save, BossSpec boss, int index, bool clear,
            float bossHp, float seconds, int hits, int parries, bool dda,
            out HistoryEntry result, out bool firstClear)
        {
            result=null;
            firstClear=false;
            if (settled) return false;
            settled=true;
            firstClear=clear&&!save.cleared[index];
            int gold=clear ? 130+index*50+Mathf.Min(80,parries*3) : Mathf.RoundToInt((1-bossHp/boss.hp)*65);
            result=new HistoryEntry {
                date=DateTime.Now.ToString("yyyy-MM-dd HH:mm"),boss=boss.id,clear=clear,
                seconds=seconds,hits=hits,parries=parries,gold=gold,dda=dda
            };
            save.gold+=gold;
            save.cardChosen=false;
            save.cardOffer=new string[0];
            save.history.Insert(0,result);
            // Keep legacy and abandoned summaries too; reset/restore must never silently prune history.
            if (clear)
            {
                save.cleared[index]=true;
                save.unlockedBoss=Mathf.Max(save.unlockedBoss,Mathf.Min(4,index+1));
                if (firstClear) save.equippedUltimate=index;
            }
            return true;
        }
    }
}

namespace ReversalTower
{
    // Read-only diagnostic wire format. No profile IDs, saves, queues, configuration or keys.
    [Serializable] public sealed class DdaMonitorAttack
    {
        public bool hasAttack;public string pattern,patternName,source,reason;public int level;
        public float windup,damage,cooldown,cooldownRemaining,windupScale,damageScale,cooldownScale;
    }
    [Serializable] public sealed class DdaMonitorPattern
    {
        public string id,name,source,reason;public int level,opportunities,parries,dodges,hits,ignored;
    }
    [Serializable] public sealed class DdaMonitorTutorial
    {
        public string step,group;public float score,confidence,active,elapsed,explanation,paused;
        public int parryAttempts,parrySuccesses,dodgeAttempts,dodgeSuccesses,hits,retries,controls,mistakes;
        public bool completed,skipped,valid,first,ultimateHit;
    }
    [Serializable] public sealed class DdaMonitorSelection
    {
        public string id,reason;public int decision,opportunities,parries,dodges,hits,ignored;public bool selected;
        public float seconds,baseWeight,multiplier,weight,probability,success;
    }
    [Serializable] public sealed class DdaMonitorFrame
    {
        public string schema="tower.dda.live/1",origin,runtime,sessionId,runId,battleId,diagnosticSession,boss,bossName,phase,kind,capturedUtc;
        public long sequence;public bool running=true,dda,hasAttack,clear,abandoned,historyLimitReached;
        public float simulationSeconds;public int dropped,writeErrors;
        public DdaMonitorAttack attack; // Explicit serializable attack-only DTO; no storage state.
        public List<DdaMonitorPattern> patterns=new List<DdaMonitorPattern>();
        public List<DdaMonitorSelection> selection=new List<DdaMonitorSelection>();
        public DdaMonitorTutorial tutorial;public DdaTraceReport learning;
    }
}
