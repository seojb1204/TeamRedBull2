using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ReversalTower;
using UnityEngine;

public static class DdaDifficultyHistoryTests
{
    static int passed;
    static string work;
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, message); }
    static string Root(string name) => Path.Combine(work, name);
    static BossSpec Boss(string id = "arakus") => new BossSpec(id, id, "", "", "", 100, 2,
        new Pattern("a", "a", AttackKind.Slash, 1, 1, 1, 2, 1, true),
        new Pattern("b", "b", AttackKind.Slash, 1, 1, 1, 2, 1, true),
        new Pattern("c", "c", AttackKind.Sweep, 1, 1, 1, 2, 2, false),
        new Pattern("d", "d", AttackKind.ShieldRush, 1, 1, 1, 2, 8, false),
        new Pattern("step", "step", AttackKind.Step, .1f, 1, 0, 1, 1, false),
        new Pattern("rage", "rage", AttackKind.Rage, 2, 2, 0, 0, 1, false));
    static List<PatternStats> Stats() => new List<PatternStats> {
        new PatternStats { id="a", level=1, opportunities=100 }, new PatternStats { id="b", level=0 },
        new PatternStats { id="c", level=-1 }, new PatternStats { id="d", level=2 },
        new PatternStats { id="step", level=2 }, new PatternStats { id="rage", level=-2 }
    };
    static DdaHistoryRecord Record(string id, string boss = "arakus", bool dda = true, bool abandoned = false)
        => DdaDifficultyHistory.Capture(Boss(boss), Stats(), dda, !abandoned, abandoned, id, new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc));

    public static int Main()
    {
        bool isolated = SaveStore.IsIsolated; string oldRoot = SaveStore.Root;
        work = Path.Combine(Path.GetTempPath(), "tower-dda-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work); SaveStore.UseIsolatedRoot(Root("game"));
        try
        {
            TestCapture(); TestStore(); TestRecovery(); TestFailure(); TestWindow(); TestRetention(); TestRecorder(); TestRegression();
            Console.WriteLine("DDA_HISTORY_TESTS_PASSED " + passed + " assertions; Unity rendering and real DB are not exercised.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { SaveStore.UseIsolatedRoot(isolated ? oldRoot : null); try { Directory.Delete(work, true); } catch { } }
    }

    static void TestCapture()
    {
        var stats = Stats(); var r = DdaDifficultyHistory.Capture(Boss(), stats, true, true, false, "one", DateTime.UtcNow);
        Check(r.patterns.Count == 6 && r.attackPatterns == 4 && Math.Abs(r.average - .5f) < .00001f, "equal-weight assigned average excludes Step/Rage and includes unexecuted attacks");
        stats[0].level = -2;
        Check(r.patterns[0].level == 1 && r.average == .5f, "record is detached from mutable battle statistics");
        var special = Boss(); special.patterns = new[] { special.patterns[4], special.patterns[5] };
        var empty = DdaDifficultyHistory.Capture(special, Stats().GetRange(4, 2), true, true, false, "special", DateTime.UtcNow);
        Check(!empty.HasAverage && !empty.Adaptive, "no attack patterns produces no graph value, not an invented neutral point");
        var invalid = Stats(); invalid[0].level = 3;
        Reject(() => DdaDifficultyHistory.Capture(Boss(), invalid, true, true, false, "bad", DateTime.UtcNow), "out-of-range assigned level is rejected, not clamped");
        invalid = Stats(); invalid.RemoveAt(0);
        Reject(() => DdaDifficultyHistory.Capture(Boss(), invalid, true, true, false, "bad", DateTime.UtcNow), "missing assignment is rejected");
        invalid = Stats(); invalid[1].id = "a";
        Reject(() => DdaDifficultyHistory.Capture(Boss(), invalid, true, true, false, "bad", DateTime.UtcNow), "duplicate pattern IDs are rejected");
        r.average = float.NaN; Reject(() => DdaDifficultyHistory.Validate(r), "nonfinite stored average rejected");
    }

    static void TestStore()
    {
        var store = new DdaDifficultyHistoryStore(Root("roundtrip"));
        Check(store.Recent("arakus").Count == 0 && store.Completed("arakus") == 0, "new and legacy users have no fabricated history");
        Check(store.Record(Record("win")), "normal victory is recorded");
        var defeat = Record("defeat"); defeat.clear = false; store.Record(defeat);
        store.Record(Record("off", dda:false)); store.Record(Record("abandon", abandoned:true));
        store.Record(Record("other", "phanes"));
        Check(store.Completed("arakus") == 3 && store.Completed("phanes") == 1, "completed sequence is independent per boss and excludes abandon");
        Check(store.All().Count == 5 && store.Recent("arakus").Count == 3, "abandoned audit rows retained outside completed graph");
        Check(!store.Record(Record("win")) && store.Completed("arakus") == 3, "same record ID cannot increment sequence twice");
        var loaded = new DdaDifficultyHistoryStore(Root("roundtrip"));
        Check(loaded.All().Count == 5 && loaded.Completed("arakus") == 3 && loaded.LastError == null, "restart restores records and counters");
        Check(!loaded.Recent("arakus")[2].dda && !loaded.Recent("arakus")[1].clear, "Fixed and normal defeat survive JSON round trip");
        var copy = loaded.Recent("arakus"); copy[0].average = 99; copy[0].patterns[0].level = -2;
        Check(loaded.Recent("arakus")[0].average == .5f && loaded.Recent("arakus")[0].patterns[0].level == 1, "graph queries cannot mutate persisted history");
        var legacy = new SaveData { gold=321 }; SaveStore.Save(legacy);
        string before = File.ReadAllText(SaveStore.SavePath); store.Record(Record("sidecar"));
        Check(File.ReadAllText(SaveStore.SavePath) == before && SaveStore.Load().gold == 321, "sidecar leaves existing progress.json unchanged");
    }

    static void TestRecovery()
    {
        var store = new DdaDifficultyHistoryStore(Root("recovery")); store.Record(Record("first")); store.Record(Record("second"));
        File.WriteAllText(store.PathName, "broken-json");
        var recovered = new DdaDifficultyHistoryStore(Root("recovery"));
        Check(recovered.Recent("arakus").Count == 1 && recovered.Warning != null, "corrupt primary recovers valid backup with visible warning");
        recovered.Record(Record("after-recovery"));
        Check(recovered.Completed("arakus") == 2 && Directory.GetFiles(Root("recovery"), "*.corrupt.*").Length == 1, "recovery preserves corrupt evidence and resumes without inventing lost records");
        Check(new DdaDifficultyHistoryStore(Root("recovery")).Recent("arakus").Count == 2, "recovered history persists on restart");
        var broken = new DdaDifficultyHistoryStore(Root("both-broken")); broken.Record(Record("b1")); broken.Record(Record("b2"));
        File.WriteAllText(broken.PathName, "bad-main"); File.WriteAllText(broken.PathName + ".bak", "bad-backup");
        var guarded = new DdaDifficultyHistoryStore(Root("both-broken"));
        Check(!guarded.Record(Record("b3")) && guarded.LastError != null && File.ReadAllText(broken.PathName) == "bad-main", "two corrupt files are not overwritten with an empty history");
        string futureRoot = Root("future"); Directory.CreateDirectory(futureRoot);
        string future = Path.Combine(futureRoot, "dda-difficulty-history.json"); File.WriteAllText(future, "{\"version\":99,\"records\":[],\"counters\":[]}");
        var unsupported = new DdaDifficultyHistoryStore(futureRoot);
        Check(!unsupported.Record(Record("future")) && File.ReadAllText(future).Contains("99"), "future history version is read-only");
    }

    static void TestFailure()
    {
        string blocked = Root("blocked"); File.WriteAllText(blocked, "not a directory");
        var store = new DdaDifficultyHistoryStore(blocked);
        Check(store.Record(Record("pending")) && store.Dirty && store.LastError != null, "disk failure retains one pending record and exposes error");
        Check(!store.Record(Record("pending")) && store.Completed("arakus") == 1, "retrying pending ID does not duplicate sequence");
        File.Delete(blocked);
        Check(store.Flush() && !store.Dirty && store.LastError == null, "retry succeeds after disk prerequisite is repaired");
        Check(new DdaDifficultyHistoryStore(blocked).Recent("arakus").Count == 1, "pending record is saved once");
    }

    static void TestWindow()
    {
        var store = new DdaDifficultyHistoryStore(Root("window"));
        Check(!DdaDifficultyHistory.TryDelta(store.Recent("arakus"), out float delta), "empty graph has no delta");
        store.Record(Record("a"));
        Check(store.Recent("arakus").Count == 1 && !DdaDifficultyHistory.TryDelta(store.Recent("arakus"), out delta), "one point has no line or invented delta");
        var next = Record("b"); next.patterns[0].level = -1; next.average = 0; store.Record(next);
        Check(DdaDifficultyHistory.TryDelta(store.Recent("arakus"), out delta) && delta == -.5f, "delta uses the two actual consecutive adaptive averages");
        store.Record(Record("fixed", dda:false)); store.Record(Record("after-fixed"));
        var rows = store.Recent("arakus");
        Check(!DdaDifficultyHistory.Connect(rows[1], rows[2]) && !DdaDifficultyHistory.Connect(rows[2], rows[3]) && !DdaDifficultyHistory.TryDelta(rows, out delta), "Fixed battle breaks adaptive lines and delta");
        Check(DdaDifficultyHistory.Window(rows, "phantom").Count == 0, "other boss has no fabricated points");
    }

    static void TestRetention()
    {
        var store = new DdaDifficultyHistoryStore(Root("retention"));
        for (int i=0;i<205;i++) store.Record(Record("r"+i));
        for (int i=0;i<55;i++) store.Record(Record("x"+i, abandoned:true));
        Check(store.All().Count == 250 && store.Completed("arakus") == 205, "retention keeps 200 completed and 50 abandoned without resetting counter");
        var recent = store.Recent("arakus");
        Check(recent.Count == 20 && recent[0].battle == 186 && recent[19].battle == 205, "graph shows the latest 20 real completed battle numbers");
        var loaded = new DdaDifficultyHistoryStore(Root("retention")); loaded.Record(Record("continued"));
        Check(loaded.Completed("arakus") == 206 && loaded.Recent("arakus")[19].battle == 206, "counter continues after pruning and restart");
    }

    static TowerGame Game()
    {
        var g = new TowerGame { save=new SaveData { unlockedBoss=4 } };
        typeof(TowerGame).GetField("sound", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g, new Soundscape());
        return g;
    }
    static void Call(TowerGame game, string method, params object[] args)
        => typeof(TowerGame).GetMethod(method, BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game, args);

    static void TestRecorder()
    {
        SaveStore.UseIsolatedRoot(Root("events")); var game = Game();
        using (var recorder = new DdaHistoryRecorder(() => game.spec, () => SaveStore.Root))
        {
            game.StartBattle(0); int expected=game.stats.Find(s=>s.id=="cleave").level; game.DamageBoss(game.bossHp);
            Check(recorder.Store.Recent("arakus").Count == 1 && recorder.Store.Recent("arakus")[0].patterns.Find(p=>p.id=="cleave").level == expected, "real EndBattle event records the actual assigned level even when never executed");
            Call(game,"EndBattle",true); SaveStore.Log(game.result,game.stats,game.save);
            Check(recorder.Store.Completed("arakus") == 1, "repeated settlement and repeated result event record once");
            game.StartBattle(0); game.playerHp=0; Call(game,"TickBattle",.016f);
            Check(!recorder.Store.Recent("arakus")[1].clear && !game.result.abandoned, "normal death event records defeat");
            game.StartBattle(0); game.Abandon();
            game.StartBattle(0); Call(game,"OnApplicationQuit");
            Check(recorder.Store.All().Count == 4 && recorder.Store.Completed("arakus") == 2, "abandon and quit are audited but excluded from learning graph sequence");
            game.save.dda=false; game.StartBattle(0); game.DamageBoss(game.bossHp);
            var last=recorder.Store.Recent("arakus")[2];
            Check(!last.dda && last.average==0 && last.patterns.TrueForAll(p=>p.level==0), "Fixed event is stored distinctly with its actual neutral levels");
            SaveStore.UseIsolatedRoot(Root("second-root")); game.StartBattle(0); game.DamageBoss(game.bossHp);
            Check(recorder.Store.Completed("arakus")==1, "observer honors a changed SaveStore.Root");
            bool later=false; Action<HistoryEntry,List<PatternStats>,SaveData> listener=(h,s,d)=>later=true;
            SaveStore.BattleLogged+=listener;
            try { SaveStore.Log(new HistoryEntry {boss="mismatch",dda=true},game.stats,game.save); }
            finally { SaveStore.BattleLogged-=listener; }
            Check(later && recorder.CaptureError!=null, "capture failure is visible and cannot prevent other event subscribers");
        }
    }

    static void TestRegression()
    {
        SaveStore.UseIsolatedRoot(Root("baseline")); var baseline = Game();
        var levels = new List<string>();
        for(int i=0;i<6;i++)
        {
            baseline.StartBattle(0); levels.Add(string.Join(",",baseline.stats.ConvertAll(s=>s.level.ToString()).ToArray()));
            foreach(var s in baseline.stats){s.opportunities=5;if(i<3)s.hits=5;else s.dodges=5;}
            baseline.DamageBoss(baseline.bossHp);
        }
        string expected=JsonUtility.ToJson(baseline.save.Learning("arakus"));
        SaveStore.UseIsolatedRoot(Root("observed")); var observed=Game();
        using(var recorder=new DdaHistoryRecorder(()=>observed.spec,()=>SaveStore.Root))
        {
            for(int i=0;i<6;i++)
            {
                observed.StartBattle(0);
                Check(levels[i]==string.Join(",",observed.stats.ConvertAll(s=>s.level.ToString()).ToArray()),"observer preserves chosen levels in battle "+(i+1));
                foreach(var s in observed.stats){s.opportunities=5;if(i<3)s.hits=5;else s.dodges=5;}
                observed.DamageBoss(observed.bossHp);
                for(int j=0;j<50;j++)
                {
                    var rows=recorder.Store.Recent("arakus");DdaDifficultyHistory.TryDelta(rows,out float delta);
                    DdaDifficultyHistory.Capture(observed.spec,observed.stats,true,true,false,"preview",DateTime.UtcNow);
                }
            }
            Check(expected==JsonUtility.ToJson(observed.save.Learning("arakus")),"recording and repeated graph queries leave A/b, memories and learning counters identical to baseline");
        }
    }
}
