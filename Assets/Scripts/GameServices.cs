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

        public void Prepare(SaveData save, BossSpec boss, bool disable)
        {
            Enabled = save.dda && !disable;
            Stats = new List<PatternStats>();
            Contexts = new Dictionary<string,double[]>();
            BossLearning learning = save.Learning(boss.id);
            foreach (Pattern pattern in boss.patterns)
            {
                PatternMemory memory = learning.Pattern(pattern.id);
                double[] context = learning.model.Context(memory.success, learning.globalSuccess,
                    memory.level, LobbyCards.Power(save), learning.lossStreak);
                int level = Enabled ? learning.model.Choose(context, memory.level,
                    learning.battles, memory.success, learning.lossStreak) : 0;
                Stats.Add(new PatternStats { id=pattern.id, level=level });
                Contexts[pattern.id] = context;
            }
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
