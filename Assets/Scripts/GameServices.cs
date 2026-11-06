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
        public List<PatternStats> Stats { get; private set; } = new List<PatternStats>();
        public Dictionary<string,double[]> Contexts { get; private set; } = new Dictionary<string,double[]>();
        // 패턴마다 이번 난이도를 고른 이유 (DDA 패널·발표용)
        public Dictionary<string,string> Reasons { get; private set; } = new Dictionary<string,string>();

        public void Prepare(SaveData save, BossSpec boss, bool disable)
        {
            Enabled = save.dda && !disable;
            Stats = new List<PatternStats>();
            Contexts = new Dictionary<string,double[]>();
            Reasons = new Dictionary<string,string>();
            foreach (Pattern pattern in boss.patterns)
            {
                double[] context;
                string reason;
                int level = Decide(save, boss, pattern, Enabled, out context, out reason);
                Stats.Add(new PatternStats { id=pattern.id, level=level });
                Contexts[pattern.id] = context;
                Reasons[pattern.id] = reason;
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
            if (!Enabled || abandoned) return;
            BossLearning learning = save.Learning(boss.id);
            float sum = 0;
            int observed = 0;
            foreach (PatternStats stats in Stats)
            {
                if (stats.opportunities < 1) continue;
                PatternMemory memory = learning.Pattern(stats.id);
                learning.model.Update(stats.level, Contexts[stats.id], BanditModel.Reward(stats.Success));
                memory.success = memory.observations == 0 ? stats.Success : Mathf.Lerp(memory.success,stats.Success,.5f);
                memory.level = stats.level;
                memory.observations++;
                sum += stats.Success;
                observed++;
            }
            learning.battles++;
            learning.lossStreak = clear ? 0 : learning.lossStreak + 1;
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
        public static void Replace(IEnumerable<Entry> list)
        {
            lock (entries) { entries.Clear(); foreach (var e in list) entries[e.boss + "/" + e.pattern] = e; }
        }
        public static bool TryGet(string boss, string pattern, out Entry entry)
        {
            lock (entries) return entries.TryGetValue(boss + "/" + pattern, out entry);
        }
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
