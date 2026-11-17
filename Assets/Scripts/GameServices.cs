using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower
{
    // Owns per-battle DDA decisions and observations. Legacy v2 policy is preserved;
    // migrating to the design document's shared 18-feature model is a separate milestone.
    public sealed class DdaBattleSession
    {
        public bool Enabled { get; private set; }
        bool completed;
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
                CommunityStats.Entry entry;
                bool found=CommunityStats.TryGet(boss.id,pattern.id,out entry);
                bool shared=found&&entry.opportunities>=CommunityStats.MinOpportunities;
                var learning=save.Learning(boss.id);
                Sources[pattern.id]=!Enabled?"고정 난이도 (DDA OFF)":learning.battles==0&&shared?
                    "공동 통계 · 첫 만남 추천":learning.Pattern(pattern.id).observations>0?"개인 패턴 기록":
                    shared?"공동 통계 · 문맥":found?"기본값 · 공동 표본 부족":"기본값 · 공동 통계 없음";
            }
        }

        // 지금까지의 기억으로 이 패턴의 레벨을 고른다. Prepare와 DDA 패널의 '다음 판' 미리보기가 같이 쓴다.
        // 학습(Complete)은 하지 않으므로 몇 번 불러도 결과가 바뀌지 않는다.
        public static int Decide(SaveData save, BossSpec boss, Pattern pattern, bool enabled, out double[] context, out string reason)
        {
            BossLearning learning = save.Learning(boss.id);
            PatternMemory memory = learning.Pattern(pattern.id);
            // 아직 이 패턴을 겪어 보지 않았으면 다른 플레이어들의 성공률을 출발점으로 쓴다 (DB가 없으면 기존과 같음)
            CommunityStats.Entry community;
            bool shared = CommunityStats.TryGet(boss.id, pattern.id, out community) && community.opportunities >= CommunityStats.MinOpportunities;
            float success = memory.observations == 0 && shared ? community.successRate : memory.success;
            context = learning.model.Context(success, learning.globalSuccess,
                memory.level, LobbyCards.Power(save), learning.lossStreak);
            int level = enabled ? learning.model.Choose(context, memory.level,
                learning.battles, success, learning.lossStreak) : 0;
            reason = !enabled ? "DDA 꺼짐" : Reason(learning, success, memory.level);
            if (enabled && learning.battles == 0 && shared)
            {
                level = Math.Max(-1, Math.Min(1, community.suggestedLevel));
                reason = "첫 만남 · 다른 플레이어 " + community.players + "명 성공률 " + Mathf.RoundToInt(community.successRate * 100) + "%";
            }
            return level;
        }

        // BanditModel.Choose와 같은 순서로 판단 이유를 적는다. current = 지난 판 레벨 (이미 끝에 닿았으면 그렇게 적음)
        static string Reason(BossLearning learning, float success, int current)
        {
            string easier = current <= -2 ? " · 이미 가장 쉬움" : " · 한 단계 쉽게";
            string harder = current >= 2 ? " · 이미 가장 어려움" : " · 한 단계 어렵게";
            if (learning.battles == 0) return "첫 만남 · 기본 난이도";
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
            if (!Enabled || abandoned) return;
            BossLearning learning = save.Learning(boss.id);
            float sum = 0;
            int observed = 0;
            foreach (PatternStats stats in Stats)
            {
                if(stats==null||stats.opportunities<1||stats.hits<0||stats.parries<0||stats.dodges<0||
                    (long)stats.hits+stats.parries+stats.dodges!=stats.opportunities||stats.level< -2||stats.level>2)continue;
                double[] context;
                if(stats.id==null||!Contexts.TryGetValue(stats.id,out context)||!BanditModel.ValidContext(context))continue;
                PatternMemory memory = learning.Pattern(stats.id);
                learning.model.Update(stats.level, context, BanditModel.Reward(stats.Success));
                memory.success = memory.observations == 0 ? stats.Success : Mathf.Lerp(memory.success,stats.Success,.5f);
                memory.level = stats.level;
                memory.observations=memory.observations==int.MaxValue?int.MaxValue:memory.observations+1;
                sum += stats.Success;
                observed++;
            }
            // No eligible observation means no evidence for either learning or loss protection.
            if(observed==0)return;
            learning.battles=learning.battles==int.MaxValue?int.MaxValue:learning.battles+1;
            learning.lossStreak=clear?0:learning.lossStreak==int.MaxValue?int.MaxValue:learning.lossStreak+1;
            if (observed > 0) learning.globalSuccess = sum / observed;
        }
    }

    // 다른 플레이어들의 패턴 통계 (Supabase의 pattern_difficulty 뷰를 CloudSync가 받아 채움).
    // 비어 있으면 DDA는 원래대로 개인 기록만 쓴다. 이 클래스는 네트워크를 모른다.
    public static class CommunityStats
    {
        public const int MinOpportunities = 20;   // 이보다 적게 쌓인 패턴은 근거 부족으로 쓰지 않음
        public sealed class Entry { public string boss, pattern; public int opportunities, players, suggestedLevel; public float successRate, parryRate, hitRate; }
        static readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>();
        public static string Status = "DB 연결 안 됨";   // 화면 표시용 (CloudSync가 갱신)
        public static int Count { get { lock (entries) return entries.Count; } }
        public static long Revision { get; private set; }
        public static string ReceiveState { get; private set; } = "미요청";
        public static string SnapshotOrigin { get; private set; } = "없음";
        public static DateTime? LastReceivedUtc { get; private set; }
        public static string ReceiveSummary => ReceiveState+" · 메모리 "+Count+"개 · "+SnapshotOrigin+
            (LastReceivedUtc.HasValue?" · 최근 수신 "+LastReceivedUtc.Value.ToString("HH:mm:ss")+" UTC":" · HTTP 성공 수신 기록 없음")+" · 영속 캐시 없음";
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
            if (save.history.Count>30) save.history.RemoveAt(30);
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
