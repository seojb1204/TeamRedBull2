using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    // Separate local sidecar: never rewrites progress.json or uploads to CloudSync.
    public sealed class DdaDifficultyHistoryStore
    {
        public string PathName { get; private set; }
        public string LastError { get; private set; }
        public string Warning { get; private set; }
        public bool Dirty { get; private set; }
        public int Revision { get; private set; }
        DdaHistoryFile data = new DdaHistoryFile();
        bool primaryValid, readOnly;

        public DdaDifficultyHistoryStore(string root)
        {
            if (string.IsNullOrEmpty(root)) throw new ArgumentException("Missing history root");
            PathName = Path.Combine(Path.GetFullPath(root), "dda-difficulty-history.json");
            Load();
        }

        void Load()
        {
            bool found = false;
            foreach (string path in new[] { PathName, PathName + ".bak" })
            {
                if (!File.Exists(path)) continue;
                found = true;
                try
                {
                    var candidate = JsonUtility.FromJson<DdaHistoryFile>(File.ReadAllText(path));
                    if (candidate != null && candidate.version > 1)
                    {
                        readOnly = true; LastError = "새 버전의 DDA 이력입니다. 덮어쓰지 않습니다."; return;
                    }
                    Validate(candidate);
                    data = candidate; primaryValid = path == PathName;
                    if (!primaryValid) Warning = "DDA 이력 백업을 복구했습니다.";
                    LastError = null;
                    return;
                }
                catch (Exception e) { LastError = "DDA 이력 읽기 실패: " + e.Message; }
            }
            // Preserve unreadable files. Do not replace a corrupt history with an empty history.
            if (found) readOnly = true;
        }

        static void Validate(DdaHistoryFile file)
        {
            if (file == null || file.version != 1 || file.records == null || file.counters == null)
                throw new InvalidDataException("Invalid history file structure");
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in file.counters)
            {
                if (c == null || string.IsNullOrEmpty(c.boss) || c.completed < 0 || counts.ContainsKey(c.boss))
                    throw new InvalidDataException("Invalid boss counter");
                counts.Add(c.boss, c.completed);
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var numbers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in file.records)
            {
                DdaDifficultyHistory.Validate(r);
                if (!ids.Add(r.id) || !counts.ContainsKey(r.boss) ||
                    (r.abandoned ? r.battle != 0 : r.battle < 1 || r.battle > counts[r.boss] || !numbers.Add(r.boss + "/" + r.battle)))
                    throw new InvalidDataException("Duplicate or invalid history sequence");
            }
        }

        public int Completed(string boss)
        {
            var c = data.counters.Find(x => x.boss == boss);
            return c == null ? 0 : c.completed;
        }

        public List<DdaHistoryRecord> Recent(string boss, int limit = DdaDifficultyHistory.GraphLimit)
            => DdaDifficultyHistory.Window(data.records, boss, limit);

        public List<DdaHistoryRecord> All()
        {
            var list = new List<DdaHistoryRecord>();
            foreach (var r in data.records) list.Add(DdaDifficultyHistory.Copy(r));
            return list;
        }

        // True means accepted in memory. Dirty/LastError expose disk failure; retry does not add twice.
        public bool Record(DdaHistoryRecord input)
        {
            if (readOnly) return false;
            try
            {
                DdaDifficultyHistory.Validate(input);
                if (data.records.Exists(x => x.id == input.id)) { if (Dirty) Flush(); return false; }
                var r = DdaDifficultyHistory.Copy(input);
                var c = data.counters.Find(x => x.boss == r.boss);
                if (c == null) { c = new DdaHistoryCounter { boss = r.boss }; data.counters.Add(c); }
                if (!r.abandoned && c.completed == int.MaxValue) throw new InvalidDataException("Battle counter exhausted");
                r.battle = r.abandoned ? 0 : ++c.completed;
                data.records.Add(r);
                Prune(r.boss, false, DdaDifficultyHistory.CompletedLimit);
                Prune(r.boss, true, DdaDifficultyHistory.AbandonedLimit);
                Revision++; Dirty = true; Flush();
                return true;
            }
            catch (Exception e) { LastError = "DDA 이력 기록 실패: " + e.Message; return false; }
        }

        void Prune(string boss, bool abandoned, int limit)
        {
            int count = data.records.FindAll(x => x.boss == boss && x.abandoned == abandoned).Count;
            for (int i = 0; count > limit && i < data.records.Count;)
            {
                var r = data.records[i];
                if (r.boss == boss && r.abandoned == abandoned) { data.records.RemoveAt(i); count--; }
                else i++;
            }
        }

        public bool Flush()
        {
            if (readOnly) return false;
            if (!Dirty) return true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                string temp = PathName + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                if (File.Exists(PathName))
                {
                    if (primaryValid) File.Replace(temp, PathName, PathName + ".bak");
                    else
                    {
                        // Keep the corrupt primary as evidence; retain the valid recovery backup.
                        File.Move(PathName, PathName + ".corrupt." + Guid.NewGuid().ToString("N"));
                        File.Move(temp, PathName);
                    }
                }
                else File.Move(temp, PathName);
                primaryValid = true; Dirty = false; LastError = null;
                return true;
            }
            catch (Exception e) { LastError = "DDA 이력 저장 실패: " + e.Message; return false; }
        }
    }
}
