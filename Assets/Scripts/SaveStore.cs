using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    public static class SaveStore
    {
        // 자동 검증 실행은 별도 폴더를 쓴다. 일반 플레이 저장, 전투 CSV, DDA 학습 모델이
        // 테스트 실행으로 오염되지 않게 하기 위한 것이다. 설정하지 않으면 평소와 동일하다.
        static string isolatedRoot;
        public static bool IsIsolated => isolatedRoot!=null;
        public static void UseIsolatedRoot(string path){isolatedRoot=string.IsNullOrEmpty(path)?null:path;}
        public static string Root => isolatedRoot ?? Application.persistentDataPath;
        public static string SavePath => Path.Combine(Root,"progress.json");
        public static string LastError {get;private set;}
        public static SaveData Load()
        {
            Directory.CreateDirectory(Root);
            foreach(var p in new[]{SavePath,SavePath+".bak"})
            {
                if(!File.Exists(p))continue;
                try
                {
                    var data=JsonUtility.FromJson<SaveData>(File.ReadAllText(p));
                    if(data==null||(data.version!=1&&data.version!=2)||data.cleared==null||data.cleared.Length!=5||data.learning==null||data.history==null)throw new InvalidDataException("Invalid save structure");
                    LobbyCards.Normalize(data);data.version=2;
                    data.gold=Math.Max(0,data.gold);data.unlockedBoss=Math.Max(0,Math.Min(4,data.unlockedBoss));
                    data.attackLevel=Math.Max(0,Math.Min(10,data.attackLevel));data.healthLevel=Math.Max(0,Math.Min(10,data.healthLevel));
                    if(data.equippedUltimate< -1||data.equippedUltimate>4||(data.equippedUltimate>=0&&!data.cleared[data.equippedUltimate]))data.equippedUltimate=-1;
                    data.masterVolume=Mathf.Clamp01(data.masterVolume);
                    return data;
                }
                catch(Exception e){LastError="저장 복구: "+e.Message;Debug.LogWarning(LastError);}
            }
            return new SaveData();
        }
        public static bool Save(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Root);string temp=SavePath+".tmp";
                // Keep the original v1 file once; ordinary rotating .bak files are not a downgrade backup.
                if(File.Exists(SavePath)&&!File.Exists(SavePath+".v1.backup"))
                {
                    SaveData old=null;try{old=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));}catch{}
                    if(old!=null&&old.version==1)File.Copy(SavePath,SavePath+".v1.backup");
                }
                File.WriteAllText(temp,JsonUtility.ToJson(data,true),Encoding.UTF8);
                if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
                LastError=null;return true;
            }
            catch(Exception e){LastError="저장 실패: "+e.Message;Debug.LogError(LastError);return false;}
        }
        public static void Log(HistoryEntry h,System.Collections.Generic.List<PatternStats> stats,SaveData progression=null)
        {
            try
            {
                var dir=Path.Combine(Root,"Battles");Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".csv");
                var sb=new StringBuilder("boss,clear,seconds,dda,pattern,level,opportunities,hits,parries,dodges,ignored,damage,attack,maxHp,potionHeal,cardPurchases,cards\n");
                var ci=System.Globalization.CultureInfo.InvariantCulture;
                var cards=new StringBuilder();if(progression!=null)foreach(var c in LobbyCards.All){int n=LobbyCards.Count(progression,c.id);if(n>0){if(cards.Length>0)cards.Append('|');cards.Append(c.id).Append(':').Append(n);}}
                foreach(var s in stats)sb.AppendFormat(ci,"{0},{1},{2:F2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F2},{12:F2},{13:F1},{14:F1},{15},{16}\n",h.boss,h.clear,h.seconds,h.dda,s.id,s.level,s.opportunities,s.hits,s.parries,s.dodges,s.ignored,s.damage,progression==null?0:LobbyCards.Attack(progression),progression==null?0:LobbyCards.Health(progression),progression==null?0:LobbyCards.Heal(progression),progression==null?0:progression.cardPurchases,cards);
                File.WriteAllText(path,sb.ToString(),Encoding.UTF8);
            }
            catch(Exception e){Debug.LogWarning("Battle log write failed: "+e.Message);}
        }
    }
}
