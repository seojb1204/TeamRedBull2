using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace ReversalTower
{
    // One observer of the existing completion event. Does not settle battles or touch the model.
    public sealed class DdaHistoryRecorder : IDisposable
    {
        sealed class Token { public string id; }
        readonly ConditionalWeakTable<HistoryEntry, Token> recorded = new ConditionalWeakTable<HistoryEntry, Token>();
        readonly Dictionary<string, DdaDifficultyHistoryStore> stores = new Dictionary<string, DdaDifficultyHistoryStore>(StringComparer.Ordinal);
        readonly Func<BossSpec> activeBoss;
        readonly Func<string> root;
        bool disposed;
        public string CaptureError { get; private set; }

        public DdaHistoryRecorder(Func<BossSpec> activeBoss, Func<string> root)
        {
            this.activeBoss = activeBoss ?? throw new ArgumentNullException(nameof(activeBoss));
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            SaveStore.BattleLogged += OnBattle;
        }

        public DdaDifficultyHistoryStore Store
        {
            get
            {
                string path = Path.GetFullPath(root());
                DdaDifficultyHistoryStore store;
                if (!stores.TryGetValue(path, out store))
                {
                    store = new DdaDifficultyHistoryStore(path);
                    stores.Add(path, store);
                }
                return store;
            }
        }

        void OnBattle(HistoryEntry result, List<PatternStats> stats, SaveData save)
        {
            if (disposed || result == null || stats == null) return;
            // A listener must not throw: that would prevent later BattleLogged subscribers from running.
            try
            {
                Token token;
                if (recorded.TryGetValue(result, out token)) return;
                var boss = activeBoss();
                if (boss == null || boss.id != result.boss) throw new InvalidOperationException("Active boss does not match battle result");
                var record = DdaDifficultyHistory.Capture(boss, stats, result.dda, result.clear,
                    result.abandoned, Guid.NewGuid().ToString("N"), DateTime.UtcNow);
                var store = Store;
                if (store.Record(record))
                {
                    recorded.Add(result, new Token { id = record.id });
                    CaptureError = null;
                }
                else CaptureError = store.LastError ?? "DDA 이력을 기록하지 못했습니다.";
            }
            catch (Exception e) { CaptureError = "DDA 이력 관찰 실패: " + e.Message; }
        }

        public void Flush()
        {
            foreach (var store in stores.Values) if (store.Dirty) store.Flush();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SaveStore.BattleLogged -= OnBattle;
            Flush();
        }
    }
}
