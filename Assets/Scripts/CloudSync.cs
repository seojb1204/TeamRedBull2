#if !HEADLESS && !DESKTOP_PREVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ReversalTower
{
    /// <summary>
    /// 전투 기록을 Supabase(PostgreSQL)로 보내고, 모든 플레이어의 패턴 통계를 받아 CommunityStats에 채운다.
    /// - 설정: StreamingAssets/supabase.json  {"url":"https://xxxx.supabase.co","anonKey":"..."}  (없으면 아무것도 하지 않음)
    /// - 보낼 때: players → battles → pattern_stats 순서. ID는 게임이 만들어서 다시 보내도 중복 저장되지 않는다(409 = 이미 있음 = 성공).
    /// - 실패하면 persistentDataPath/CloudQueue에 보관했다가 다음 실행·다음 전투 때 다시 보낸다. 게임 진행은 절대 막지 않는다.
    /// 표(테이블)·보안 설정은 Docs/DB/supabase_schema.sql 참고.
    /// </summary>
    public sealed class CloudSync : MonoBehaviour
    {
        [Serializable] sealed class Config { public string url; public string anonKey; }
        [Serializable] sealed class Pending { public string id; public string player; public string battle; public string stats; public string card; }
        [Serializable] sealed class DifficultyRow { public string boss; public string pattern; public int opportunities; public int players; public float success_rate; public float parry_rate; public float hit_rate; public int suggested_level; }
        [Serializable] sealed class DifficultyList { public DifficultyRow[] items; }

        const string PlayerKey = "CloudPlayerId", RegisteredKey = "CloudPlayerRegistered";
        const int TimeoutSeconds = 6;
        static CloudSync instance;
        Config config;
        string playerId, queueDir;
        bool sending;
        DateTime? lastSync;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Cloud Sync");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CloudSync>();
        }

        void Awake()
        {
            // Boot order is unspecified: inspect command line, not AutoRunArgs.Active.
            if(AutoRunArgs.HasAutomationArguments(Environment.GetCommandLineArgs()))
            {CommunityStats.Status="자동검증: 네트워크 꺼짐";return;}
            config = LoadConfig();
            if (config == null) { CommunityStats.Status = "DB 설정 없음 (supabase.json)"; return; }
            playerId = PlayerPrefs.GetString(PlayerKey, "");
            if (string.IsNullOrEmpty(playerId)) { playerId = Guid.NewGuid().ToString(); PlayerPrefs.SetString(PlayerKey, playerId); PlayerPrefs.Save(); }
            queueDir = Path.Combine(Application.persistentDataPath, "CloudQueue");
            Directory.CreateDirectory(queueDir);
            SaveStore.BattleLogged += OnBattle;
            LobbyCards.Purchased += OnCard;
            CommunityStats.Status = "DB 연결 중…";
            StartCoroutine(SyncAll());
        }

        void OnDestroy()
        {
            SaveStore.BattleLogged -= OnBattle;
            LobbyCards.Purchased -= OnCard;
        }

        static Config LoadConfig()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "supabase.json");
                if (!File.Exists(path)) return null;
                var c = JsonUtility.FromJson<Config>(File.ReadAllText(path));
                if (c == null || string.IsNullOrEmpty(c.url) || string.IsNullOrEmpty(c.anonKey) || c.anonKey.Contains("여기")) return null;
                c.url = c.url.TrimEnd('/');
                return c;
            }
            catch (Exception e) { Debug.LogWarning("Supabase 설정을 읽지 못했습니다: " + e.Message); return null; }
        }

        // ── 기록 만들기 ────────────────────────────────────────────────────────

        void OnBattle(HistoryEntry h, List<PatternStats> stats, SaveData save)
        {
            if (h == null || stats == null) return;
            // 끝까지 싸우지 않은 판(포기·종료)은 다른 플레이어의 첫 판 난이도를 정하는 통계에 섞지 않는다 (로컬 CSV에는 남음)
            if (h.abandoned) return;
            string battleId = Guid.NewGuid().ToString();
            var ci = CultureInfo.InvariantCulture;
            var cards = new StringBuilder();
            if (save != null) foreach (var c in LobbyCards.All) { int n = LobbyCards.Count(save, c.id); if (n > 0) { if (cards.Length > 0) cards.Append('|'); cards.Append(c.id).Append(':').Append(n); } }
            var battle = new StringBuilder("{");
            Field(battle, "id", battleId); Field(battle, "player_id", playerId); Field(battle, "boss", h.boss);
            Raw(battle, "clear", h.clear ? "true" : "false"); Raw(battle, "seconds", Mathf.Clamp(h.seconds, 0, 7199).ToString("0.##", ci));
            Raw(battle, "dda", h.dda ? "true" : "false"); Raw(battle, "hits", h.hits.ToString(ci)); Raw(battle, "parries", h.parries.ToString(ci));
            if (save != null)
            {
                Raw(battle, "attack", LobbyCards.Attack(save).ToString("0.##", ci)); Raw(battle, "max_hp", LobbyCards.Health(save).ToString("0.##", ci));
                Raw(battle, "potion_heal", LobbyCards.Heal(save).ToString("0.##", ci)); Raw(battle, "card_purchases", save.cardPurchases.ToString(ci));
            }
            Field(battle, "cards", cards.ToString()); Field(battle, "game_version", Application.version, true); battle.Append('}');
            var rows = new StringBuilder("[");
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i]; if (i > 0) rows.Append(',');
                rows.Append('{'); Field(rows, "battle_id", battleId); Field(rows, "boss", h.boss); Field(rows, "pattern", s.id);
                Raw(rows, "level", Mathf.Clamp(s.level, -2, 2).ToString(ci)); Raw(rows, "opportunities", s.opportunities.ToString(ci));
                Raw(rows, "hits", s.hits.ToString(ci)); Raw(rows, "parries", s.parries.ToString(ci)); Raw(rows, "dodges", s.dodges.ToString(ci));
                Raw(rows, "ignored", s.ignored.ToString(ci)); Raw(rows, "damage", s.damage.ToString("0.##", ci), true); rows.Append('}');
            }
            rows.Append(']');
            Enqueue(new Pending { id = battleId, battle = battle.ToString(), stats = stats.Count > 0 ? rows.ToString() : null });
        }

        void OnCard(string card, int cost, int index)
        {
            var row = new StringBuilder("{");
            string id = Guid.NewGuid().ToString();
            Field(row, "id", id); Field(row, "player_id", playerId); Field(row, "card", card);
            Raw(row, "cost", Mathf.Max(0, cost).ToString(CultureInfo.InvariantCulture)); Raw(row, "pick_index", Mathf.Max(0, index).ToString(CultureInfo.InvariantCulture), true);
            row.Append('}');
            Enqueue(new Pending { id = id, card = row.ToString() });
        }

        void Enqueue(Pending p)
        {
            try { File.WriteAllText(Path.Combine(queueDir, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + p.id.Substring(0, 8) + ".json"), JsonUtility.ToJson(p)); }
            catch (Exception e) { Debug.LogWarning("DB 대기열 저장 실패: " + e.Message); }
            if (!sending) StartCoroutine(SyncAll());
        }

        // ── 보내기·받기 ───────────────────────────────────────────────────────

        IEnumerator SyncAll()
        {
            if (sending) yield break;
            sending = true;
            bool ok = true;
            if (PlayerPrefs.GetInt(RegisteredKey, 0) == 0)
            {
                var player = new StringBuilder("{"); Field(player, "id", playerId); Field(player, "game_version", Application.version, true); player.Append('}');
                yield return Post("players", player.ToString(), r => ok = r);
                if (ok) { PlayerPrefs.SetInt(RegisteredKey, 1); PlayerPrefs.Save(); }
            }
            string[] files = ok ? Directory.GetFiles(queueDir, "*.json") : new string[0];
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                Pending p = null;
                try { p = JsonUtility.FromJson<Pending>(File.ReadAllText(file)); } catch (Exception) { }
                if (p == null) { TryDelete(file); continue; }
                if (!string.IsNullOrEmpty(p.battle)) { yield return Post("battles", p.battle, r => ok = r); if (!ok) break; }
                if (!string.IsNullOrEmpty(p.stats)) { yield return Post("pattern_stats", p.stats, r => ok = r); if (!ok) break; }
                if (!string.IsNullOrEmpty(p.card)) { yield return Post("card_picks", p.card, r => ok = r); if (!ok) break; }
                TryDelete(file);
            }
            yield return FetchDifficulty();
            int left = Directory.GetFiles(queueDir, "*.json").Length;
            CommunityStats.Status = !ok ? "DB 오프라인 · 대기 " + left + "건 (다음에 다시 보냄)"
                : "DB 동기화됨 " + (lastSync.HasValue ? lastSync.Value.ToString("HH:mm") : "") + " · 패턴 통계 " + CommunityStats.Count + "개";
            sending = false;
        }

        IEnumerator Post(string table, string json, Action<bool> done)
        {
            using (var req = new UnityWebRequest(config.url + "/rest/v1/" + table, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                Headers(req);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Prefer", "return=minimal");
                yield return req.SendWebRequest();
                // 201 = 저장됨, 409 = 이미 같은 ID가 있음(재전송) → 둘 다 성공으로 본다
                bool ok = req.result == UnityWebRequest.Result.Success || req.responseCode == 409;
                if (!ok) Debug.LogWarning("DB " + table + " 전송 실패 (" + req.responseCode + "): " + req.error + " " + req.downloadHandler.text);
                done(ok);
            }
        }

        IEnumerator FetchDifficulty()
        {
            string url = config.url + "/rest/v1/pattern_difficulty?select=boss,pattern,opportunities,players,success_rate,parry_rate,hit_rate,suggested_level";
            using (var req = UnityWebRequest.Get(url))
            {
                Headers(req);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("DB 통계 받기 실패 (" + req.responseCode + "): " + req.error); yield break; }
                try
                {
                    var list = JsonUtility.FromJson<DifficultyList>("{\"items\":" + req.downloadHandler.text + "}");
                    var entries = new List<CommunityStats.Entry>();
                    if (list != null && list.items != null)
                        foreach (var r in list.items)
                            entries.Add(new CommunityStats.Entry { boss = r.boss, pattern = r.pattern, opportunities = r.opportunities, players = r.players,
                                successRate = r.success_rate, parryRate = r.parry_rate, hitRate = r.hit_rate, suggestedLevel = r.suggested_level });
                    CommunityStats.Replace(entries);
                    lastSync = DateTime.Now;
                }
                catch (Exception e) { Debug.LogWarning("DB 통계 해석 실패: " + e.Message); }
            }
        }

        void Headers(UnityWebRequest req)
        {
            req.timeout = TimeoutSeconds;
            req.SetRequestHeader("apikey", config.anonKey);
            // 예전 anon 키(JWT)는 Authorization에도 넣는다. 새 publishable 키(sb_publishable_…)는 apikey만 쓴다.
            if (config.anonKey.StartsWith("eyJ")) req.SetRequestHeader("Authorization", "Bearer " + config.anonKey);
        }

        static void TryDelete(string file) { try { File.Delete(file); } catch (Exception) { } }

        static void Field(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null) sb.Append("null");
            else
            {
                sb.Append('"');
                foreach (char c in value)
                {
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                }
                sb.Append('"');
            }
            if (!last) sb.Append(',');
        }
        static void Raw(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value);
            if (!last) sb.Append(',');
        }
    }
}
#endif
