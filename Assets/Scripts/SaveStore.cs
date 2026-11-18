using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower
{
    public static partial class SaveStore
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
                    if(data==null||(data.version!=1&&data.version!=2)||data.cleared==null||data.cleared.Length!=5||data.history==null)throw new InvalidDataException("Invalid save structure");
                    LobbyCards.Normalize(data);data.NormalizeDda();data.version=2;
                    data.gold=Math.Max(0,data.gold);data.unlockedBoss=Math.Max(0,Math.Min(4,data.unlockedBoss));
                    data.attackLevel=Math.Max(0,Math.Min(10,data.attackLevel));data.healthLevel=Math.Max(0,Math.Min(10,data.healthLevel));
                    if(data.equippedUltimate< -1||data.equippedUltimate>4||(data.equippedUltimate>=0&&!data.cleared[data.equippedUltimate]))data.equippedUltimate=-1;
                    data.masterVolume=Mathf.Clamp01(data.masterVolume);
                    // Only pre-profile saves are legacy. Archived history in a newly reset
                    // profile must never bypass mandatory onboarding.
                    if(!Uuid(data.profileId)&&data.tutorialOnboardingVersion<1&&
                        (data.unlockedBoss>0||data.gold>0||data.attackLevel>0||data.healthLevel>0||data.history.Count>0||Array.Exists(data.cleared,x=>x)))
                        data.tutorialOnboardingVersion=1;
                    bool needsIdentity=!Uuid(data.profileId)||!Uuid(data.cloudPlayerId);
                    EnsureIdentity(data,true);
                    // Persist migration before another boot component reads the save. Backup recovery is not overwritten here.
                    if(needsIdentity&&p==SavePath&&!Save(data))Debug.LogWarning("신원 이관 저장 실패: 현재 읽은 진행은 유지합니다. "+LastError);
                    ActivePlayerId=data.cloudPlayerId;
                    return data;
                }
                catch(Exception e){LastError="저장 복구: "+e.Message;Debug.LogWarning(LastError);}
            }
            var fresh=new SaveData();EnsureIdentity(fresh,true);ActivePlayerId=fresh.cloudPlayerId;return fresh;
        }
        public static bool Save(SaveData data)
        {
            try
            {
                EnsureIdentity(data,false);
                Directory.CreateDirectory(Root);string temp=SavePath+".tmp";
                // Keep the original v1 file once; ordinary rotating .bak files are not a downgrade backup.
                if(File.Exists(SavePath)&&!File.Exists(SavePath+".v1.backup"))
                {
                    SaveData old=null;try{old=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));}catch{}
                    if(old!=null&&old.version==1)File.Copy(SavePath,SavePath+".v1.backup");
                }
                File.WriteAllText(temp,JsonUtility.ToJson(data,true),Encoding.UTF8);
                if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
                ActivePlayerId=data.cloudPlayerId;LastError=null;return true;
            }
            catch(Exception e){LastError="저장 실패: "+e.Message;Debug.LogError(LastError);return false;}
        }
        // 전투 기록이 남을 때마다 알림 (CloudSync가 DB로 보냄). 저장 실패와 무관하게 호출된다.
        // Local-only diagnostic stream. No CloudSync hook, no player identity, no existing-file deletion.
        public static string LogSelections(string session,string boss,System.Collections.Generic.IReadOnlyList<PatternSelectionEvidence> rows,int decision)
        {
            try
            {
                string dir=Path.Combine(Root,"DdaDecisions");Directory.CreateDirectory(dir);
                // A per-session GUID prevents overwriting another battle. Stop at a bounded size.
                string path=Path.Combine(dir,session+".csv");
                if(File.Exists(path)&&new FileInfo(path).Length>=16*1024*1024)return "로그 한도 도달 (16 MB); 화면 기록은 계속";
                var sb=new StringBuilder();
                if(!File.Exists(path))sb.AppendLine("boss,decision,seconds,pattern,selected,opportunities,hits,parries,dodges,ignored,success,baseWeight,multiplier,weight,probability,reason");
                var ci=System.Globalization.CultureInfo.InvariantCulture;
                foreach(var e in rows)if(e.decision==decision)
                    sb.AppendFormat(ci,"{0},{1},{2:F3},{3},{4},{5},{6},{7},{8},{9},{10:F5},{11:F5},{12:F5},{13:F5},{14:F5},{15}\n",
                        Csv(boss),e.decision,e.seconds,Csv(e.id),e.selected,e.opportunities,e.hits,e.parries,e.dodges,e.ignored,e.success,e.baseWeight,e.multiplier,e.weight,e.probability,Csv(e.reason));
                File.AppendAllText(path,sb.ToString(),Encoding.UTF8);
                return "로컬 CSV 기록 중 · DdaDecisions/"+session+".csv";
            }
            catch(Exception){return "CSV 기록 실패; 전투와 화면 기록은 계속";}
        }
        static string Csv(string value){return "\""+(value??"").Replace("\"","\"\"")+"\"";}
        public static event Action<HistoryEntry,System.Collections.Generic.List<PatternStats>,SaveData> BattleLogged;
        public static void Log(HistoryEntry h,System.Collections.Generic.List<PatternStats> stats,SaveData progression=null)
        {
            try{BattleLogged?.Invoke(h,stats,progression);}catch(Exception e){Debug.LogWarning("Battle upload hook failed: "+e.Message);}
            try
            {
                var dir=Path.Combine(Root,"Battles");Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".csv");
                var sb=new StringBuilder("boss,clear,seconds,dda,pattern,level,opportunities,hits,parries,dodges,ignored,damage,attack,maxHp,potionHeal,cardPurchases,cards,abandoned\n");
                var ci=System.Globalization.CultureInfo.InvariantCulture;
                var cards=new StringBuilder();if(progression!=null)foreach(var c in LobbyCards.All){int n=LobbyCards.Count(progression,c.id);if(n>0){if(cards.Length>0)cards.Append('|');cards.Append(c.id).Append(':').Append(n);}}
                foreach(var s in stats)sb.AppendFormat(ci,"{0},{1},{2:F2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F2},{12:F2},{13:F1},{14:F1},{15},{16},{17}\n",h.boss,h.clear,h.seconds,h.dda,s.id,s.level,s.opportunities,s.hits,s.parries,s.dodges,s.ignored,s.damage,progression==null?0:LobbyCards.Attack(progression),progression==null?0:LobbyCards.Health(progression),progression==null?0:LobbyCards.Heal(progression),progression==null?0:progression.cardPurchases,cards,h.abandoned);
                File.WriteAllText(path,sb.ToString(),Encoding.UTF8);
            }
            catch(Exception e){Debug.LogWarning("Battle log write failed: "+e.Message);}
        }
    }
}

namespace ReversalTower
{
    public static partial class SaveStore
    {
        [Serializable] public sealed class Snapshot
        {
            public int schemaVersion=1;
            public string id,createdAtUtc;
            public HistoryEntry battle;
            public SaveData state;
        }
        public static string ActivePlayerId {get;private set;}
        public static bool ProfileOperationInProgress {get;private set;}
        static string ArchiveRoot => Path.Combine(Root,"ProfileArchive");
        static bool Uuid(string value){Guid id;return Guid.TryParse(value,out id);}
        static readonly System.Collections.Generic.Dictionary<string,string> sessionIdentities=new System.Collections.Generic.Dictionary<string,string>();
        public static void EnsureIdentity(SaveData data,bool migrateLegacy)
        {
            if(data==null)throw new ArgumentNullException("data");
            if(!Uuid(data.cloudPlayerId))
            {
                string legacy=null;
#if !HEADLESS && !DESKTOP_PREVIEW
                if(migrateLegacy&&!IsIsolated)legacy=PlayerPrefs.GetString("CloudPlayerId","");
#endif
                if(migrateLegacy&&!Uuid(legacy))sessionIdentities.TryGetValue(Root,out legacy);
                data.cloudPlayerId=Uuid(legacy)?legacy:Guid.NewGuid().ToString();
                if(migrateLegacy)sessionIdentities[Root]=data.cloudPlayerId;
            }
            // A legacy profile has one stable ID even before the next successful save.
            if(!Uuid(data.profileId))data.profileId=data.cloudPlayerId;
        }
        [Serializable] sealed class UploadOwner {public string player_id;}
        // Legacy queue migration reads the embedded owner; never substitutes active identity.
        public static string ResolveUploadOwner(string explicitOwner,string battleJson,string cardJson)
        {
            try
            {
                var battle=string.IsNullOrEmpty(battleJson)?null:JsonUtility.FromJson<UploadOwner>(battleJson);
                var card=string.IsNullOrEmpty(cardJson)?null:JsonUtility.FromJson<UploadOwner>(cardJson);
                string owner=battle!=null?battle.player_id:card!=null?card.player_id:null;
                if(!Uuid(owner)||(!string.IsNullOrEmpty(explicitOwner)&&explicitOwner!=owner)||(card!=null&&card.player_id!=owner))return null;
                return owner;
            }catch{return null;}
        }
        static SaveData Clone(SaveData data){return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));}
        static string SnapshotPath(string id)
        {
            if(!Uuid(id))throw new InvalidDataException("Invalid snapshot ID");
            return Path.Combine(ArchiveRoot,"snapshots",id+".json");
        }
        static void WriteNew(string path,object data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            File.WriteAllText(temp,JsonUtility.ToJson(data,true),Encoding.UTF8);
            File.Move(temp,path); // Immutable: never replace a previous archive.
        }
        static void Backup(SaveData data)
        {
            var copy=Clone(data);EnsureIdentity(copy,false);
            WriteNew(Path.Combine(ArchiveRoot,"backups",Guid.NewGuid()+".json"),copy);
        }
        // Called only after rewards/learning are settled. The committed save is the source of truth.
        public static bool CommitCompletedBattle(SaveData data,HistoryEntry battle)
        {
            if(battle==null)return false;
            if(!Uuid(battle.battleId))battle.battleId=Guid.NewGuid().ToString();
            if(!Save(data))return false;
            if(battle.abandoned)return true;
            try
            {
                var committed=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                var h=committed.history.Find(x=>x.battleId==battle.battleId);
                if(h==null)throw new InvalidDataException("Battle not in committed history");
                string id=Guid.NewGuid().ToString();h.snapshotId=id;
                // History is stored once as metadata, not recursively in each state payload.
                committed.history=new System.Collections.Generic.List<HistoryEntry>();
                WriteNew(SnapshotPath(id),new Snapshot{id=id,createdAtUtc=DateTime.UtcNow.ToString("o"),battle=h,state=committed});
                battle.snapshotId=id; // Archive remains discoverable even if this convenience reference is not saved.
                LastError=null;return true;
            }
            catch(Exception e){LastError="진행 저장 완료, 복원 지점 생성 실패: "+e.Message;Debug.LogWarning(LastError);return false;}
        }
        public static Snapshot ReadSnapshot(string id)
        {
            var snapshot=JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath(id)));
            if(snapshot==null||snapshot.schemaVersion!=1||snapshot.id!=id||snapshot.state==null||snapshot.battle==null||snapshot.battle.abandoned||!Uuid(snapshot.battle.battleId)||!Uuid(snapshot.state.profileId)||!Uuid(snapshot.state.cloudPlayerId)||snapshot.state.cleared==null||snapshot.state.cleared.Length!=5)
                throw new InvalidDataException("Invalid snapshot");
            return snapshot;
        }
        public static bool HasSnapshot(string id){try{ReadSnapshot(id);return true;}catch{return false;}}
        public static System.Collections.Generic.List<HistoryEntry> AllHistory(SaveData current)
        {
            var result=new System.Collections.Generic.List<HistoryEntry>();
            var keys=new System.Collections.Generic.HashSet<string>();
            Action<HistoryEntry> add=h=>{if(h==null)return;string key=Uuid(h.battleId)?h.battleId:JsonUtility.ToJson(h);if(keys.Add(key))result.Add(h);};
            string snapshots=Path.Combine(ArchiveRoot,"snapshots"),backups=Path.Combine(ArchiveRoot,"backups");
            // Prefer snapshot metadata, which includes the restorable ID.
            if(Directory.Exists(snapshots))foreach(var file in Directory.GetFiles(snapshots,"*.json"))
                try{add(ReadSnapshot(Path.GetFileNameWithoutExtension(file)).battle);}catch{}
            if(current!=null&&current.history!=null)foreach(var h in current.history)add(h);
            if(Directory.Exists(backups))foreach(var file in Directory.GetFiles(backups,"*.json"))
                try{var old=JsonUtility.FromJson<SaveData>(File.ReadAllText(file));if(old!=null&&old.history!=null)foreach(var h in old.history)add(h);}catch{}
            result.Sort((a,b)=>string.CompareOrdinal(b.date,a.date));return result;
        }
        public static bool TryResetProfile(SaveData current,bool inBattle,out SaveData replacement)
        {return SwitchProfile(current,inBattle,null,out replacement);}
        public static bool TryRestoreProfile(SaveData current,string snapshotId,bool inBattle,out SaveData replacement)
        {return SwitchProfile(current,inBattle,snapshotId,out replacement);}
        static bool SwitchProfile(SaveData current,bool inBattle,string snapshotId,out SaveData replacement)
        {
            replacement=current;
            if(inBattle||ProfileOperationInProgress||current==null){LastError="전투 또는 저장 작업 중에는 변경할 수 없습니다.";return false;}
            ProfileOperationInProgress=true;
            try
            {
                SaveData next=snapshotId==null?new SaveData():Clone(ReadSnapshot(snapshotId).state);
                if(snapshotId==null){next.masterVolume=current.masterVolume;EnsureIdentity(next,false);}
                Backup(current); // Required backup must succeed before switching.
                next.history=AllHistory(current); // Full archive survives resetting and rewinding.
                if(!Save(next))return false; // Atomic progress.json replace is the sole commit point.
                replacement=next;return true;
            }
            catch(Exception e){LastError="프로필 변경 실패 (기존 진행 유지): "+e.Message;Debug.LogWarning(LastError);return false;}
            finally{ProfileOperationInProgress=false;}
        }
    }
}
