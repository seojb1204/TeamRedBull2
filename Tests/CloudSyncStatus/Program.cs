using System;using System.IO;using System.Reflection;using System.Collections;using ReversalTower;using UnityEngine;using UnityEngine.Networking;
class Program{
static int n;static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);n++;}
static void Drain(IEnumerator x){while(x.MoveNext())if(x.Current is IEnumerator y)Drain(y);}
static void Main(){var root=Path.Combine(Path.GetTempPath(),"cloud-status-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
var t=typeof(CloudSync);var c=new CloudSync();var cfg=Activator.CreateInstance(t.GetNestedType("Config",BindingFlags.NonPublic),true);cfg.GetType().GetField("url").SetValue(cfg,"https://invalid.local");cfg.GetType().GetField("anonKey").SetValue(cfg,"test-public");t.GetField("config",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,cfg);t.GetField("queueDir",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,root);PlayerPrefs.SetInt("CloudPlayerRegistered",1);
void Run()=>Drain((IEnumerator)t.GetMethod("SyncAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null));
UnityWebRequest.Next=UnityWebRequest.Result.Success;UnityWebRequest.Body="[]";Run();Check(CommunityStats.Status.Contains("동기화됨"),"successful empty GET is success");
UnityWebRequest.Body="[{\"boss\":\"b\",\"pattern\":\"p\"}]";Run();Check(CommunityStats.Count==1,"valid statistics loaded");
var sync=t.GetField("lastSync",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
UnityWebRequest.Next=UnityWebRequest.Result.ConnectionError;Run();Check(CommunityStats.Status.Contains("수신 실패")&&!CommunityStats.Status.Contains("동기화됨"),"GET failure not marked synced");Check(CommunityStats.Count==1,"GET failure preserves cache");Check(Equals(sync,t.GetField("lastSync",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c)),"failure preserves last success time");
PlayerPrefs.SetInt("CloudPlayerRegistered",0);Run();Check(CommunityStats.Status.Contains("전송 실패")&&CommunityStats.Status.Contains("수신 실패"),"fully offline status truthful");PlayerPrefs.SetInt("CloudPlayerRegistered",1);
UnityWebRequest.Next=UnityWebRequest.Result.Success;UnityWebRequest.Body="not-json";Run();Check(CommunityStats.Status.Contains("수신 실패"),"malformed JSON not success");Check(CommunityStats.Count==1,"malformed JSON preserves cache");
UnityWebRequest.Body="null";Run();Check(CommunityStats.Status.Contains("수신 실패"),"null array not success");
UnityWebRequest.Body="[]";Run();Check(CommunityStats.Status.Contains("동기화됨")&&CommunityStats.Count==0,"recovery and valid empty clear cache");Check(!(bool)t.GetField("sending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c),"sending resets after responses");
AutoRunArgs.IsAutomationProcess=true;t.GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new CloudSync(),null);Check(CommunityStats.Status.Contains("네트워크 꺼짐"),"automation never connects");
Console.WriteLine("CLOUD_STATUS_PASS "+n+" actual CloudSync.cs, stubbed HTTP, no network or production data");}finally{Directory.Delete(root,true);}}
}
