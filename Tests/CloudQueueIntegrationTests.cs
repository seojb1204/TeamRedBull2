// Real CloudSync + real SaveStore. All identities/files are generated under an isolated root.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using ReversalTower;
using UnityEngine;
using UnityEngine.Networking;
public static class CloudQueueIntegrationTests
{
    [Serializable] public sealed class Fixture{public string id,player,battle,stats,card;}
    static int passed,failed;
    static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
    static void Drain(IEnumerator e){while(e.MoveNext())if(e.Current is IEnumerator child)Drain(child);}
    static readonly Type type=typeof(CloudSync);
    static void Field(CloudSync c,string name,object value)=>type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,value);
    static IEnumerator Sync(CloudSync c)=>(IEnumerator)type.GetMethod("SyncAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);
    static object Call(CloudSync c,string name,params object[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
    static string Row(string owner)=>"{\"id\":\""+Guid.NewGuid()+"\",\"player_id\":\""+owner+"\"}";
    static void Pending(string path,string owner,string explicitOwner=null)=>File.WriteAllText(path,JsonUtility.ToJson(new Fixture{id=Guid.NewGuid().ToString(),player=explicitOwner,battle=Row(owner),stats="[]"}));
    public static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"tower-queue-cross-"+Guid.NewGuid().ToString("N"));string queue=Path.Combine(root,"queue");Directory.CreateDirectory(queue);SaveStore.UseIsolatedRoot(Path.Combine(root,"state"));
        try
        {
            var a=new SaveData();SaveStore.Save(a);var checkpoint=new HistoryEntry{boss="arakus",date="2026-11-18"};a.history.Add(checkpoint);Check(SaveStore.CommitCompletedBattle(a,checkpoint),"real checkpoint prepared for profile restore during upload");
            var cloud=new CloudSync();var cfg=Activator.CreateInstance(type.GetNestedType("Config",BindingFlags.NonPublic),true);cfg.GetType().GetField("url").SetValue(cfg,"https://invalid.local");cfg.GetType().GetField("anonKey").SetValue(cfg,"mock");Field(cloud,"config",cfg);Field(cloud,"queueDir",queue);Field(cloud,"playerId",a.cloudPlayerId);
            UnityWebRequest.Next=UnityWebRequest.Result.Success;UnityWebRequest.Body="[]";PlayerPrefs.SetInt("CloudPlayerRegistered",1);
            string old=Path.Combine(queue,"001-old.json");Pending(old,a.cloudPlayerId);UnityWebRequest.Requests.Clear();
            var sync=Sync(cloud);Check(sync.MoveNext()&&sync.Current is IEnumerator,"old owner registration suspends independently of active profile");var registration=(IEnumerator)sync.Current;registration.MoveNext();
            SaveData b;Check(SaveStore.TryResetProfile(a,false,out b)&&b.cloudPlayerId!=a.cloudPlayerId,"actual reset creates new active UUID while old registration is suspended");Call(cloud,"OnCard","blade",80,1);
            SaveData restored;Check(SaveStore.TryRestoreProfile(b,checkpoint.snapshotId,false,out restored)&&restored.cloudPlayerId==a.cloudPlayerId,"actual restore changes active owner back without rewriting captured new-profile card");
            Check(Directory.GetFiles(queue,"*.json").Length==2,"new active profile card enqueues without changing in-flight snapshot");
            Drain(registration);Drain(sync);
            var posts=UnityWebRequest.Requests.Where(x=>x.method=="POST").ToArray();
            Check(posts.Length==3&&posts[0].url.EndsWith("/players")&&posts[0].body.Contains(a.cloudPlayerId),"global legacy registered flag cannot skip per-owner registration");
            Check(posts[1].url.EndsWith("/battles")&&posts[1].body.Contains(a.cloudPlayerId)&&!posts[1].body.Contains(b.cloudPlayerId),"in-flight old battle keeps immutable original owner");
            Check(PlayerPrefs.GetInt("CloudPlayerRegistered/"+a.cloudPlayerId,0)==1&&PlayerPrefs.GetInt("CloudPlayerRegistered/"+b.cloudPlayerId,0)==0,"registration cache scoped to old owner only");
            Check(!File.Exists(old)&&Directory.GetFiles(queue,"*.json").Length==1,"successful old upload removed while deferred new card remains");
            Check(CommunityStats.Status.Contains("대기 1건"),"deferred queue count remains visible after successful snapshot sync");
            var card=JsonUtility.FromJson<Fixture>(File.ReadAllText(Directory.GetFiles(queue,"*.json").Single()));Check(card.player==b.cloudPlayerId&&card.card.Contains(b.cloudPlayerId),"new card captures new active UUID not stale CloudSync field");
            UnityWebRequest.Requests.Clear();Drain(Sync(cloud));posts=UnityWebRequest.Requests.Where(x=>x.method=="POST").ToArray();
            Check(posts.Length==2&&posts[0].body.Contains(b.cloudPlayerId)&&posts[1].url.EndsWith("/card_picks")&&posts[1].body.Contains(b.cloudPlayerId),"next sync registers and sends deferred new profile card");
            Check(Directory.GetFiles(queue,"*.json").Length==0,"successful new owner card acknowledged without pruning other history");
            var c=new SaveData();SaveStore.Save(c);string retry=Path.Combine(queue,"002-retry.json");Pending(retry,c.cloudPlayerId,c.cloudPlayerId);string retryBytes=File.ReadAllText(retry);
            UnityWebRequest.Next=UnityWebRequest.Result.ConnectionError;UnityWebRequest.Requests.Clear();Drain(Sync(cloud));
            Check(File.ReadAllText(retry)==retryBytes&&PlayerPrefs.GetInt("CloudPlayerRegistered/"+c.cloudPlayerId,0)==0,"failed registration preserves exact queue payload and registration state");
            Check(!UnityWebRequest.Requests.Any(x=>x.url.EndsWith("/battles")),"failed registration prevents battle upload");
            Check(CommunityStats.ReceiveState=="수신 실패"&&!(bool)type.GetField("sending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(cloud),"offline receive diagnosis and sending guard survive queue failure");
            UnityWebRequest.Next=UnityWebRequest.Result.Success;Drain(Sync(cloud));Check(!File.Exists(retry),"explicit next sync safely retries original owner payload");
            string corrupt=Path.Combine(queue,"000-corrupt.json"),mismatch=Path.Combine(queue,"001-mismatch.json"),unknown=Path.Combine(queue,"002-unknown.json");
            File.WriteAllText(corrupt,"not-json");Pending(mismatch,a.cloudPlayerId,b.cloudPlayerId);File.WriteAllText(unknown,JsonUtility.ToJson(new Fixture{id=Guid.NewGuid().ToString(),player=a.cloudPlayerId,stats="[]"}));
            var before=Directory.GetFiles(queue,"*.json").ToDictionary(x=>x,File.ReadAllText);string good=Path.Combine(queue,"999-good.json");Pending(good,b.cloudPlayerId,b.cloudPlayerId);UnityWebRequest.Requests.Clear();Drain(Sync(cloud));
            Check(before.All(x=>File.Exists(x.Key)&&File.ReadAllText(x.Key)==x.Value),"corrupt mismatched and unknown-owner queues preserved byte for byte");
            Check(!File.Exists(good)&&UnityWebRequest.Requests.Count(x=>x.method=="POST")==2,"valid later item uploads without relabelling invalid earlier items");
            Check(CommunityStats.Status.Contains("전송 실패")&&!CommunityStats.Status.Contains("DB 동기화됨"),"retained invalid queues cannot be masked by later successful POST");
            int count=Directory.GetFiles(queue,"*.json").Length;
            Call(cloud,"OnBattle",new HistoryEntry{abandoned=true,boss="tutorial_dummy"},new System.Collections.Generic.List<PatternStats>(),b);
            Check(Directory.GetFiles(queue,"*.json").Length==count,"abandoned/tutorial-like event cannot enter upload queue");
            // Sparse pre-profile JSON needs the field-tolerant JSON double in this harness.
            // The older DataContractJsonSerializer shim requires all current fields and is not a Unity migration oracle.
            SaveStore.UseIsolatedRoot(Path.Combine(root,"legacy"));Directory.CreateDirectory(SaveStore.Root);
            File.WriteAllText(SaveStore.SavePath,"{\"version\":1,\"gold\":5,\"cleared\":[false,false,false,false,false],\"history\":[],\"tutorialSeen\":true}");
            var legacy=SaveStore.Load();Check(legacy.version==2&&legacy.tutorialOnboardingVersion==1&&legacy.tutorialAssessment==null&&Guid.TryParse(legacy.cloudPlayerId,out _),"sparse legacy progress migrates identity without fabricated assessment");
            Check(File.Exists(SaveStore.SavePath+".v1.backup"),"sparse legacy original backup retained");
            SaveStore.UseIsolatedRoot(Path.Combine(root,"legacy-fresh"));Directory.CreateDirectory(SaveStore.Root);
            File.WriteAllText(SaveStore.SavePath,"{\"version\":1,\"gold\":0,\"cleared\":[false,false,false,false,false],\"history\":[],\"tutorialSeen\":true}");
            Check(SaveStore.Load().tutorialOnboardingVersion==0,"legacy help-seen alone cannot bypass first assessment");
            Console.WriteLine("CLOUD_QUEUE_INTEGRATION "+passed+" PASS / "+failed+" FAIL; real ownership resolver, mocked HTTP, no production data");return failed==0?0:1;
        }
        finally{SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}
    }
}
