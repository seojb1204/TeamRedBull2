using System;using System.Collections;using System.Collections.Generic;
namespace UnityEngine {
 public class MonoBehaviour {public static implicit operator bool(MonoBehaviour x)=>x!=null; protected static void DontDestroyOnLoad(object x){} protected void StartCoroutine(IEnumerator x){} }
 public class GameObject{public GameObject(string n){}public T AddComponent<T>()where T:new()=>new T();}
 public enum RuntimeInitializeLoadType{AfterSceneLoad}public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public static class Application{public static string persistentDataPath,streamingAssetsPath,version="test";}
 public static class PlayerPrefs{static Dictionary<string,int>d=new();public static string GetString(string k,string def)=>def;public static void SetString(string k,string v){}public static int GetInt(string k,int def)=>d.GetValueOrDefault(k,def);public static void SetInt(string k,int v)=>d[k]=v;public static void Save(){}}
 public static class Mathf{public static float Clamp(float n,float a,float b)=>Math.Clamp(n,a,b);public static int Clamp(int n,int a,int b)=>Math.Clamp(n,a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static int RoundToInt(float n)=>(int)Math.Round(n);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Clamp01(float x)=>Math.Clamp(x,0,1);}
 public static class Debug{public static void LogWarning(string s){}public static void LogError(string s){}}
 public static class JsonUtility{public static T FromJson<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}); public static string ToJson(object o,bool pretty=false)=>System.Text.Json.JsonSerializer.Serialize(o,new System.Text.Json.JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true,WriteIndented=pretty});}
}
namespace UnityEngine.Networking{
 public class UploadHandlerRaw{public byte[] data;public UploadHandlerRaw(byte[] x){data=x;}}public class DownloadHandlerBuffer{public string text="[]";}
 public class UnityWebRequest:IDisposable {
  public enum Result{Success,ConnectionError}
  public sealed class Request {public string url,method,body;}
  public static readonly List<Request> Requests=new List<Request>();
  public static Func<string,(Result result,long code,string body)> Respond;public static Result Next=Result.Success;public static string Body="[]";
  public Result result;public long responseCode;public string error="mock";public int timeout;
  public UploadHandlerRaw uploadHandler;public DownloadHandlerBuffer downloadHandler;
  readonly string url,method;
  public UnityWebRequest(string u,string m){url=u;method=m;result=Next;responseCode=result==Result.Success?200:503;}
  public static UnityWebRequest Get(string u)=>new UnityWebRequest(u,"GET"){downloadHandler=new DownloadHandlerBuffer{text=Body}};
  public void SetRequestHeader(string a,string b){}
  public object SendWebRequest(){if(Respond!=null){var r=Respond(url);result=r.result;responseCode=r.code;if(downloadHandler!=null)downloadHandler.text=r.body;}Requests.Add(new Request{url=url,method=method,body=uploadHandler==null?null:System.Text.Encoding.UTF8.GetString(uploadHandler.data)});return null;}
  public void Dispose(){}
 }

}
namespace ReversalTower{
#if REAL_DDA
 // Catalog-only constant dependency from current main; no combat/policy is mocked here.
 public static class BossPatternSystem{public const string PhanesUltimate="phanes_star";}
 public class CardStack{public string id;public int count;}
#endif
 public static class AutoRunArgs{public static bool IsAutomationProcess;}
#if !REAL_DDA
 public class HistoryEntry{public bool abandoned,clear,dda;public string boss,battleId;public float seconds;public int hits,parries;}
 public class PatternStats{public string id;public int level,opportunities,hits,parries,dodges,ignored;public float damage;}
 public class SaveData{public int cardPurchases;public string cloudPlayerId;}
#endif
#if !REAL_SAVE
 public static class SaveStore{
  public static event Action<HistoryEntry,List<PatternStats>,SaveData> BattleLogged;
  public static event Action ProficiencyQueued;
  public sealed class ProficiencyEnvelope{public string kind,profile,eventId,assessmentId,ownerKey,payload;}
  public static string ProficiencyQueueRoot=>throw new NotSupportedException("Protected outbox requires real SaveStore");
  public static void QueueAssessments(SaveData s){}
  public static string ProficiencyKind(string path)=>throw new NotSupportedException();
  public static ProficiencyEnvelope ReadProficiency(string path,out string secret){secret=null;throw new NotSupportedException();}
  public static void AcknowledgeProficiency(string path,ProficiencyEnvelope e)=>throw new NotSupportedException();
  public static void SetProficiencyState(string profile,string state){}

  public static string ActivePlayerId=>throw new NotSupportedException("Owner validation requires real SaveStore harness");
  public static SaveData Load()=>throw new NotSupportedException("Load requires isolated real SaveStore");
  public static void EnsureIdentity(SaveData s,bool migrate)=>throw new NotSupportedException("Identity not mocked");
  public static string ResolveUploadOwner(string owner,string battle,string card)=>throw new NotSupportedException("Queue owner not mocked");
 }
#endif
 public static class LobbyCards{public class Card{public string id;}public static Card[] All=Array.Empty<Card>();public static int Count(SaveData s,string id)=>0;public static float Attack(SaveData s)=>0;public static float Health(SaveData s)=>0;public static float Heal(SaveData s)=>0;public static float Power(SaveData s)=>0;public static void Normalize(SaveData s){}public static event Action<string,int,int> Purchased;}
#if !REAL_DDA
 public static class CommunityStats{public class Entry{public string boss,pattern,group;public int opportunities,players,suggestedLevel;public float successRate,parryRate,hitRate;}public static List<Entry> Entries=new();public static int Count;public static string Status;public static void GroupEndReceive(){}public static void GroupReceiving(){}public static void GroupFailed(){}public static void ReplaceGroups(IEnumerable<Entry> e,bool http=false)=>throw new NotSupportedException("Group evidence requires real CommunityStats");public static void BeginReceive(){}public static void ReceiveFailed(){}public static void EndReceive(){}public static void MarkReceived(){}public static void MarkUnavailable(bool a){}public static void Replace(IEnumerable<Entry> e){Entries=new List<Entry>(e);Count=Entries.Count;}}
#endif
}
