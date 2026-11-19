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
                ActivePlayerId=data.cloudPlayerId;LastError=null;QueueAssessments(data);return true;
            }
            catch(Exception e){LastError="저장 실패: "+e.Message;Debug.LogError(LastError);return false;}
        }
        public static void LogLearning(string battleId,DdaTraceReport trace)
        {
            if(!Uuid(battleId)||trace==null)return;
            try{string dir=Path.Combine(Root,"DdaLearning");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,battleId+".json"),JsonUtility.ToJson(trace,true));}catch(Exception){Debug.LogWarning("DDA 진단 로그 저장 실패 (전투/학습 유지)");}
        }
        public static void LogApplied(string session,string boss,string pattern,int decision,int level,float windup,float damage,float cooldown,string reason)
        {
            try{string dir=Path.Combine(Root,"DdaApplied");Directory.CreateDirectory(dir);string file=Path.Combine(dir,session+".csv");if(File.Exists(file)&&new FileInfo(file).Length>16*1024*1024)return;
                var ci=System.Globalization.CultureInfo.InvariantCulture;
                if(!File.Exists(file))File.WriteAllText(file,"boss,pattern,decision,assignedLevel,windup,damage,cooldown,reason\n");
                File.AppendAllText(file,string.Join(",",new[]{Csv(boss),Csv(pattern),decision.ToString(ci),level.ToString(ci),windup.ToString("R",ci),damage.ToString("R",ci),cooldown.ToString("R",ci),Csv(reason)})+"\n");
            }catch(Exception){Debug.LogWarning("적용 난이도 로그 저장 실패 (전투 유지)");}
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

namespace ReversalTower
{
    // Separate protected telemetry outbox. Existing CloudQueue and progress/archive formats are untouched.
    public static partial class SaveStore
    {
        [Serializable] public sealed class ProficiencyEnvelope {public string kind,profile,eventId,assessmentId,ownerKey,payload;}
        public static event Action ProficiencyQueued;
        static readonly System.Collections.Generic.Dictionary<string,string> proficiencyStates=new System.Collections.Generic.Dictionary<string,string>();
        public static string ProficiencyQueueRoot=>Path.Combine(Root,"ProficiencyOutbox");
        public static string ProficiencyStatus=>ProficiencyState(ActivePlayerId);
        public static string ProficiencyState(string profile)
        {
            if(!Uuid(profile))return "평가 프로필 미준비";
            string id=new Guid(profile).ToString();string state;if(proficiencyStates.TryGetValue(id,out state))return state;
            int count=Directory.Exists(ProficiencyQueueRoot)?Directory.GetFiles(ProficiencyQueueRoot,id+"_*.json").Length:0;
            return count>0?"보호 전송 로컬 대기 "+count+"건 · migration/설정 필요":"보호 전송 기록 없음";
        }
        public static void SetProficiencyState(string profile,string state){if(Uuid(profile))proficiencyStates[new Guid(profile).ToString()]=state;}
        static string KeyPath(string profile)=>Path.Combine(Root,"ProficiencyKeys",new Guid(profile)+".key");
        static string HashText(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        static string EventId(string text){using(var sha=System.Security.Cryptography.SHA256.Create()){var bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(text));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id).ToString();}}
        public static string AssessmentEventId(string profile,TutorialAssessment a)
        {
            DateTimeOffset stamp;if(!Uuid(profile)||a==null||!DateTimeOffset.TryParse(a.completedAtUtc,out stamp)||(!a.completed&&!a.skipped))return null;
            return EventId(new Guid(profile)+"|"+(a.isFirstAssessment?"initial":"replay")+"|"+stamp.ToUniversalTime().ToString("o"));
        }
        [Serializable] sealed class AssessmentPayload {public TutorialAssessment assessment;}
        [Serializable] sealed class BattlePayload {public HistoryEntry battle;public System.Collections.Generic.List<PatternStats> samples;}
        [System.Runtime.InteropServices.DllImport("libc",SetLastError=true)] static extern int chmod(string path,uint mode);
        static void PrivatePath(string path,uint mode){if(Environment.OSVersion.Platform==PlatformID.Unix&&chmod(path,mode)!=0)throw new IOException("Cannot protect local telemetry key permissions");}
        static string Capability(string profile,bool create)
        {
            string path=KeyPath(profile);
            if(!File.Exists(path))
            {
                if(!create||File.Exists(path+".owner"))throw new IOException("Original profile capability unavailable; pending telemetry retained");
                Directory.CreateDirectory(Path.GetDirectoryName(path));PrivatePath(Path.GetDirectoryName(path),448);
                var bytes=new byte[32];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(bytes);
                string secret=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
                using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var raw=Encoding.UTF8.GetBytes(secret);stream.Write(raw,0,raw.Length);}
                PrivatePath(path,384);File.WriteAllText(path+".owner",HashText(secret));
            }
            if(new FileInfo(path).Length>128)throw new IOException("Invalid capability size");
            string value=File.ReadAllText(path);if(value.Length!=64||!System.Text.RegularExpressions.Regex.IsMatch(value,"^[0-9a-f]{64}$"))throw new IOException("Invalid profile capability; no replacement generated");return value;
        }
        static void QueueProtected(string kind,SaveData data,string id,string assessment,string payload)
        {
            if(data==null||!Uuid(data.cloudPlayerId)||!Uuid(id))return;
            string profile=new Guid(data.cloudPlayerId).ToString();
            try
            {
                string receipt=Path.Combine(Root,"ProficiencyReceipts",profile+"_"+id+".ok");if(File.Exists(receipt))return;
                Directory.CreateDirectory(ProficiencyQueueRoot);string path=Path.Combine(ProficiencyQueueRoot,profile+"_"+id+".json");if(File.Exists(path))return;
                string secret=Capability(profile,true);
                WriteNew(path,new ProficiencyEnvelope{kind=kind,profile=profile,eventId=id,assessmentId=assessment,ownerKey=HashText(secret),payload=payload});
                SetProficiencyState(profile,"로컬 보호 전송 대기 · "+kind);try{ProficiencyQueued?.Invoke();}catch{SetProficiencyState(profile,"로컬 대기 · 동기화 시작 실패");}
            }
            catch(Exception){SetProficiencyState(profile,"보호 전송 큐 저장 실패 · 진행/평가는 이미 로컬 저장됨");}
        }
        public static void QueueAssessments(SaveData data)
        {
            if(data==null)return;
            try{foreach(var a in new[]{data.tutorialAssessment,data.tutorialReplayAssessment})
            {string id=AssessmentEventId(data.cloudPlayerId,a);if(id!=null)QueueProtected("assessment",data,id,null,JsonUtility.ToJson(new AssessmentPayload{assessment=a}));}}
            catch{SetProficiencyState(data.cloudPlayerId,"평가 큐 직렬화 실패 · 로컬 저장은 유지");}
        }
        public static void QueueProficiencyBattle(SaveData data,HistoryEntry battle,System.Collections.Generic.List<PatternStats> samples)
        {
            if(data==null||data.tutorialAssessment==null||battle==null||battle.abandoned||!Uuid(battle.battleId))return;
            try{QueueAssessments(data);
            string assessment=InitialDifficulty.ValidAssessment(data.tutorialAssessment)?AssessmentEventId(data.cloudPlayerId,data.tutorialAssessment):null;
            QueueProtected("battle",data,battle.battleId,assessment,JsonUtility.ToJson(new BattlePayload{battle=battle,samples=samples}));}
            catch{SetProficiencyState(data.cloudPlayerId,"전투 보호 큐 생성 실패 · 진행 저장은 유지");}
        }
        public static string ProficiencyKind(string file){if(new FileInfo(file).Length>1024*1024)throw new InvalidDataException();return JsonUtility.FromJson<ProficiencyEnvelope>(File.ReadAllText(file)).kind;}
        public static ProficiencyEnvelope ReadProficiency(string file,out string secret)
        {
            secret=null;if(new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Telemetry envelope too large");
            var e=JsonUtility.FromJson<ProficiencyEnvelope>(File.ReadAllText(file));
            if(e==null||!Uuid(e.profile)||!Uuid(e.eventId)||(e.kind!="assessment"&&e.kind!="battle")||string.IsNullOrEmpty(e.payload))throw new InvalidDataException("Invalid protected telemetry");
            secret=Capability(e.profile,false);if(HashText(secret)!=e.ownerKey)throw new InvalidDataException("Profile capability owner mismatch");return e;
        }
        public static void AcknowledgeProficiency(string file,ProficiencyEnvelope e)
        {
            string dir=Path.Combine(Root,"ProficiencyReceipts");Directory.CreateDirectory(dir);string receipt=Path.Combine(dir,new Guid(e.profile)+"_"+e.eventId+".ok");
            if(!File.Exists(receipt))File.WriteAllText(receipt,"accepted");File.Delete(file);SetProficiencyState(e.profile,"보호 전송 성공 · 원시 데이터는 공개 조회 불가");
        }
    }
}

namespace ReversalTower
{
    // Opt-in, bounded, background-only disk output. Immutable JSON strings cross the thread boundary.
    // This class never reads or changes SaveStore data. Each publisher owns a fresh session directory.
    public sealed class DdaMonitorSink : IDisposable
    {
        public const int MaxPacketBytes=262144,MaxQueuedEvents=32;
        public const long MaxHistoryBytes=16*1024*1024;
        public readonly string SessionId,DirectoryPath;
        readonly object gate=new object();readonly System.Threading.AutoResetEvent wake=new System.Threading.AutoResetEvent(false);
        readonly System.Collections.Generic.Queue<string> events=new System.Collections.Generic.Queue<string>();
        readonly System.Threading.Thread worker;string pending;bool stopping;long lastHistoryWrite;
        int dropped,writeErrors;volatile bool historyLimit;
        public int Dropped=>System.Threading.Volatile.Read(ref dropped);
        public int WriteErrors=>System.Threading.Volatile.Read(ref writeErrors);
        public bool HistoryLimitReached=>historyLimit;
        public DdaMonitorSink(string root)
        {
            if(string.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))throw new ArgumentException("Explicit absolute diagnostics directory required");
            SessionId=Guid.NewGuid().ToString("N");DirectoryPath=Path.Combine(Path.GetFullPath(root),"DdaLive",SessionId);
            Directory.CreateDirectory(DirectoryPath);
            worker=new System.Threading.Thread(Work){IsBackground=true,Name="DDA diagnostic writer"};worker.Start();
        }
        public bool Offer(string immutableJson,bool important)
        {
            if(string.IsNullOrEmpty(immutableJson)||Encoding.UTF8.GetByteCount(immutableJson)>MaxPacketBytes){System.Threading.Interlocked.Increment(ref dropped);return false;}
            lock(gate)
            {
                if(stopping)return false;
                if(important){if(pending!=null){pending=null;System.Threading.Interlocked.Increment(ref dropped);}if(events.Count>=MaxQueuedEvents){events.Dequeue();System.Threading.Interlocked.Increment(ref dropped);}events.Enqueue(immutableJson);}
                else {if(pending!=null)System.Threading.Interlocked.Increment(ref dropped);pending=immutableJson;}
            }
            wake.Set();return true;
        }
        void Write(string json,bool important)
        {
            try
            {
                string temp=Path.Combine(DirectoryPath,"latest.tmp"),target=Path.Combine(DirectoryPath,"latest.json");
                File.WriteAllText(temp,json,new UTF8Encoding(false));
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
                long now=System.Diagnostics.Stopwatch.GetTimestamp();
                if(!important&&now-lastHistoryWrite<System.Diagnostics.Stopwatch.Frequency)return;
                lastHistoryWrite=now;string history=Path.Combine(DirectoryPath,"events.jsonl");
                int bytes=Encoding.UTF8.GetByteCount(json)+1;
                if((File.Exists(history)?new FileInfo(history).Length:0)+bytes>MaxHistoryBytes){historyLimit=true;return;}
                File.AppendAllText(history,json+"\n",new UTF8Encoding(false));
            }catch(Exception){System.Threading.Interlocked.Increment(ref writeErrors);}
        }
        void Work()
        {
            while(true)
            {
                wake.WaitOne(250);string json=null;bool important=false,done;
                lock(gate)
                {
                    if(events.Count>0){json=events.Dequeue();important=true;}
                    else {json=pending;pending=null;}
                    done=stopping&&events.Count==0&&pending==null;
                }
                if(json!=null)Write(json,important);
                if(done)break;
                lock(gate){if(events.Count>0||pending!=null)wake.Set();}
            }
        }
        // Only used at shutdown/domain teardown, never as a per-frame flush.
        public void Dispose(){lock(gate){if(stopping)return;stopping=true;}wake.Set();worker.Join(150);}
    }
}
