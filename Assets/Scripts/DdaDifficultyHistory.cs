using System;
using System.Collections.Generic;
using System.Globalization;

namespace ReversalTower
{
    [Serializable] public sealed class DdaHistoryPattern
    {
        public string id;
        public AttackKind kind;
        public int level;
    }

    [Serializable] public sealed class DdaHistoryRecord
    {
        public string id, boss, recordedUtc;
        // Counts completed battles recorded by this feature, including Fixed; abandoned has no number.
        public int battle;
        public bool dda, clear, abandoned;
        public int attackPatterns;
        public float average;
        public List<DdaHistoryPattern> patterns = new List<DdaHistoryPattern>();
        public bool HasAverage => attackPatterns > 0;
        public bool Adaptive => dda && !abandoned && HasAverage;
    }

    [Serializable] public sealed class DdaHistoryCounter
    {
        public string boss;
        public int completed;
    }

    [Serializable] public sealed class DdaHistoryFile
    {
        public int version = 1;
        public List<DdaHistoryRecord> records = new List<DdaHistoryRecord>();
        public List<DdaHistoryCounter> counters = new List<DdaHistoryCounter>();
    }

    // Observation only. Never calls Prepare, Complete, Decide, Choose or Update.
    public static class DdaDifficultyHistory
    {
        public const int CompletedLimit = 200, AbandonedLimit = 50, GraphLimit = 20;
        public const string AverageLabel = "배정된 공격 패턴 난이도 평균";

        public static bool IsAttack(AttackKind kind) => kind != AttackKind.Step && kind != AttackKind.Rage;

        public static DdaHistoryRecord Capture(BossSpec boss, IList<PatternStats> stats,
            bool dda, bool clear, bool abandoned, string id, DateTime utc)
        {
            if (boss == null || string.IsNullOrEmpty(boss.id) || boss.patterns == null || stats == null)
                throw new ArgumentException("Missing battle definition or assigned levels");
            var byId = new Dictionary<string, PatternStats>(StringComparer.Ordinal);
            foreach (var stat in stats)
            {
                if (stat == null || string.IsNullOrEmpty(stat.id) || byId.ContainsKey(stat.id))
                    throw new ArgumentException("Missing or duplicate pattern statistics");
                byId.Add(stat.id, stat);
            }
            var result = new DdaHistoryRecord {
                id = id, boss = boss.id, recordedUtc = utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                dda = dda, clear = clear, abandoned = abandoned
            };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int sum = 0;
            foreach (var pattern in boss.patterns)
            {
                PatternStats stat;
                if (pattern == null || string.IsNullOrEmpty(pattern.id) || !seen.Add(pattern.id) || !byId.TryGetValue(pattern.id, out stat))
                    throw new ArgumentException("Assigned level missing for a boss pattern");
                if (stat.level < -2 || stat.level > 2)
                    throw new ArgumentException("Assigned level outside -2..+2");
                result.patterns.Add(new DdaHistoryPattern { id = pattern.id, kind = pattern.kind, level = stat.level });
                // Includes attacks that were assigned but never executed; no frequency weighting.
                if (IsAttack(pattern.kind)) { sum += stat.level; result.attackPatterns++; }
            }
            if (seen.Count != byId.Count) throw new ArgumentException("Statistics do not match the active boss");
            result.average = result.HasAverage ? (float)sum / result.attackPatterns : 0;
            return result;
        }

        public static DdaHistoryRecord Copy(DdaHistoryRecord record)
        {
            var copy = new DdaHistoryRecord {
                id = record.id, boss = record.boss, recordedUtc = record.recordedUtc, battle = record.battle,
                dda = record.dda, clear = record.clear, abandoned = record.abandoned,
                attackPatterns = record.attackPatterns, average = record.average
            };
            if (record.patterns == null) throw new ArgumentException("Missing history patterns");
            foreach (var p in record.patterns)
            {
                if (p == null) throw new ArgumentException("Missing history pattern");
                copy.patterns.Add(new DdaHistoryPattern { id = p.id, kind = p.kind, level = p.level });
            }
            return copy;
        }

        public static void Validate(DdaHistoryRecord record)
        {
            DateTimeOffset date;
            if (record == null || string.IsNullOrEmpty(record.id) || string.IsNullOrEmpty(record.boss) ||
                !DateTimeOffset.TryParse(record.recordedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                record.patterns == null || record.battle < 0 || (record.clear && record.abandoned))
                throw new ArgumentException("Invalid difficulty history record");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int count = 0, sum = 0;
            foreach (var p in record.patterns)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || !ids.Add(p.id) || p.level < -2 || p.level > 2 ||
                    !Enum.IsDefined(typeof(AttackKind), p.kind)) throw new ArgumentException("Invalid history pattern");
                if (IsAttack(p.kind)) { count++; sum += p.level; }
            }
            float expected = count == 0 ? 0 : (float)sum / count;
            if (record.attackPatterns != count || float.IsNaN(record.average) || float.IsInfinity(record.average) ||
                Math.Abs(record.average - expected) > .00001f) throw new ArgumentException("Invalid history average");
        }

        public static List<DdaHistoryRecord> Window(IEnumerable<DdaHistoryRecord> records, string boss, int limit = GraphLimit)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            var list = new List<DdaHistoryRecord>();
            foreach (var r in records) if (r.boss == boss && !r.abandoned) list.Add(Copy(r));
            list.Sort((a, b) => a.battle.CompareTo(b.battle));
            if (list.Count > limit) list.RemoveRange(0, list.Count - limit);
            return list;
        }

        // Fixed and missing-average rows break the adaptive line. Never invent intermediate points.
        public static bool Connect(DdaHistoryRecord left, DdaHistoryRecord right)
            => left != null && right != null && left.boss == right.boss && left.Adaptive && right.Adaptive && right.battle == left.battle + 1;

        public static bool TryDelta(IList<DdaHistoryRecord> records, out float delta)
        {
            delta = 0;
            if (records.Count < 2 || !Connect(records[records.Count - 2], records[records.Count - 1])) return false;
            delta = records[records.Count - 1].average - records[records.Count - 2].average;
            return true;
        }
    }
}
