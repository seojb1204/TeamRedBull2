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
            BossLearning learning = save.Learning(boss.id);
            foreach (Pattern pattern in boss.patterns)
            {
                PatternMemory memory = learning.Pattern(pattern.id);
                // 아직 이 패턴을 겪어 보지 않았으면 다른 플레이어들의 성공률을 출발점으로 쓴다 (DB가 없으면 기존과 같음)
                CommunityStats.Entry community;
                bool shared = CommunityStats.TryGet(boss.id, pattern.id, out community) && community.opportunities >= CommunityStats.MinOpportunities;
                float success = memory.observations == 0 && shared ? community.successRate : memory.success;
                double[] context = learning.model.Context(success, learning.globalSuccess,
                    memory.level, LobbyCards.Power(save), learning.lossStreak);
                int level = Enabled ? learning.model.Choose(context, memory.level,
                    learning.battles, success, learning.lossStreak) : 0;
                string reason = !Enabled ? "DDA 꺼짐" : Reason(learning, success);
                if (Enabled && learning.battles == 0 && shared)
                {
                    level = Math.Max(-1, Math.Min(1, community.suggestedLevel));
                    reason = "첫 만남 · 다른 플레이어 " + community.players + "명 성공률 " + Mathf.RoundToInt(community.successRate * 100) + "%";
                }
                Stats.Add(new PatternStats { id=pattern.id, level=level });
                Contexts[pattern.id] = context;
                Reasons[pattern.id] = reason;
            }
        }

        // BanditModel.Choose와 같은 순서로 판단 이유를 적는다
        static string Reason(BossLearning learning, float success)
        {
            if (learning.battles == 0) return "첫 만남 · 기본 난이도";
            if (learning.lossStreak >= 3) return learning.lossStreak + "연패 보호 · 한 단계 쉽게";
            if (learning.battles < 4 && success < .45f) return "초반 · 성공률 " + Mathf.RoundToInt(success * 100) + "% 낮음 → 쉽게";
            if (learning.battles < 4 && success > .9f) return "초반 · 성공률 " + Mathf.RoundToInt(success * 100) + "% 높음 → 어렵게";
            return "LinUCB 학습 · 성공률 " + Mathf.RoundToInt(success * 100) + "% (목표 60~80%)";
        }

        public void Complete(SaveData save, BossSpec boss, bool clear)
        {
            if (!Enabled) return;
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
