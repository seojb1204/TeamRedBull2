// Real CloudSync -> real CommunityStats -> real Prepare. HTTP/Unity/cards are doubles.
using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;
using ReversalTower;using UnityEngine;using UnityEngine.Networking;
public static class ColdStartIntegrationTests {
 static int count;static void Check(bool x,string name){if(!x)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static void Drain(IEnumerator e){while(e.MoveNext())if(e.Current is IEnumerator inner)Drain(inner);}
 static DdaBattleSession Prepare(SaveData s){var d=new DdaBattleSession();d.Prepare(s,Catalog.Bosses[0],false);return d;}
 public static int Main(){
  var type=typeof(CloudSync);var cloud=new CloudSync();var cfg=Activator.CreateInstance(type.GetNestedType("Config",BindingFlags.NonPublic),true);
  cfg.GetType().GetField("url").SetValue(cfg,"https://invalid.local");cfg.GetType().GetField("anonKey").SetValue(cfg,"mock");type.GetField("config",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(cloud,cfg);
  Func<IEnumerator> fetch=()=>(IEnumerator)type.GetMethod("FetchDifficulty",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cloud,new object[]{new Action<bool>(ok=>{})});
  CommunityStats.Replace(new CommunityStats.Entry[0]);var save=new SaveData();var initial=Prepare(save);long revision=CommunityStats.Revision;string snapshot=initial.CommunitySnapshotAtPrepare;
  UnityWebRequest.Body="[{\"boss\":\"arakus\",\"pattern\":\"cleave\",\"opportunities\":20,\"players\":12,\"success_rate\":0.95,\"suggested_level\":1}]";
  var receive=fetch();Check(receive.MoveNext()&&CommunityStats.ReceiveState=="수신 중","receive suspended with explicit state");
  Check(Prepare(new SaveData()).Stats[0].level==0,"pending receive uses existing fallback without waiting");Drain(receive);
  Check(CommunityStats.ReceiveState=="수신 완료"&&CommunityStats.SnapshotOrigin=="HTTP 수신"&&CommunityStats.LastReceivedUtc.HasValue,"HTTP origin separate from memory fixture");
  Check(CommunityStats.Revision==revision+1,"successful snapshot increments preview revision");
  Check(initial.Stats[0].level==0&&initial.CommunitySnapshotAtPrepare==snapshot&&initial.CommunityRevisionAtPrepare==revision,"active assignment and origin snapshot immutable after late receive");
  var next=Prepare(new SaveData());Check(next.Stats[0].level==1&&next.Sources["cleave"]=="전체 DB","received data reaches actual next Prepare");
  var key=new DdaPreviewStamp();var result=new object();var memory=save.Learning("arakus");key.Capture(result,memory,true,revision);
  Check(key.Matches(result,memory,true,revision)&&!key.Matches(result,memory,true,CommunityStats.Revision),"F8 cache key invalidates on actual receive revision");
  key.Capture(result,memory,true,CommunityStats.Revision);
  Check(!key.Matches(result,memory,false,CommunityStats.Revision)&&!key.Matches(result,new BossLearning(),true,CommunityStats.Revision)&&!key.Matches(new object(),memory,true,CommunityStats.Revision),"F8 preserves setting/reset/result invalidation");
  Check(next.Stats.Skip(1).All(s=>s.level==0),"missing patterns fall back independently");
  Check(Prepare(new SaveData{dda=false}).Stats.All(s=>s.level==0),"received stats cannot enable disabled DDA");
  var st=next.Stats[0];st.opportunities=st.hits=20;var personal=new SaveData();var learned=Prepare(personal);learned.Stats[0].opportunities=learned.Stats[0].hits=20;learned.Complete(personal,Catalog.Bosses[0],true);
  var own=Prepare(personal);Check(own.Stats[0].level==0&&own.Sources["cleave"].Contains("개인"),"personal completed observation wins over shared first recommendation");
  CommunityStats.Entry kept;CommunityStats.TryGet("arakus","cleave",out kept);revision=CommunityStats.Revision;var stamp=CommunityStats.LastReceivedUtc;
  UnityWebRequest.Next=UnityWebRequest.Result.ConnectionError;Drain(fetch());CommunityStats.Entry retained;CommunityStats.TryGet("arakus","cleave",out retained);
  Check(ReferenceEquals(kept,retained)&&CommunityStats.Revision==revision&&CommunityStats.LastReceivedUtc==stamp&&CommunityStats.ReceiveState=="수신 실패","offline preserves snapshot revision and successful timestamp");
  UnityWebRequest.Next=UnityWebRequest.Result.Success;UnityWebRequest.Body="[null]";Drain(fetch());Check(CommunityStats.Count==1&&CommunityStats.Revision==revision,"malformed row cannot clear prior real cache");
  try{CommunityStats.Replace(new[]{kept,null});throw new Exception("Expected invalid row rejection");}catch(ArgumentException){}Check(CommunityStats.Count==1&&CommunityStats.Revision==revision,"direct invalid memory injection is atomic");
  UnityWebRequest.Body="[{\"boss\":\"arakus\",\"pattern\":\"cleave\",\"opportunities\":19,\"suggested_level\":1}]";Drain(fetch());Check(Prepare(new SaveData()).Stats[0].level==0,"insufficient HTTP sample falls back");
  UnityWebRequest.Body="[]";Drain(fetch());Check(CommunityStats.Count==0&&CommunityStats.ReceiveState=="수신 완료","valid empty refresh clears snapshot");
  var cancelled=fetch();cancelled.MoveNext();((IDisposable)cancelled).Dispose();Check(CommunityStats.ReceiveState=="수신 중단","cancelled receive does not stay pending forever");
  // Disposing a suspended sync and an I/O exception must both release the in-flight guard.
  string root=Path.Combine(Path.GetTempPath(),"cold-integration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   type.GetField("queueDir",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(cloud,root);PlayerPrefs.SetInt("CloudPlayerRegistered",0);
   var sync=(IEnumerator)type.GetMethod("SyncAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cloud,null);sync.MoveNext();((IDisposable)sync).Dispose();
   Check(!(bool)type.GetField("sending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(cloud),"cancelled coroutine releases sending flag");
   type.GetField("queueDir",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(cloud,Path.Combine(root,"absent"));PlayerPrefs.SetInt("CloudPlayerRegistered",1);
   try{Drain((IEnumerator)type.GetMethod("SyncAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cloud,null));throw new Exception("Expected IO exception");}catch(DirectoryNotFoundException){}
   Check(!(bool)type.GetField("sending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(cloud),"queue IO failure releases sending flag");
  }finally{Directory.Delete(root,true);}
  var assessment=new TutorialAssessment{isFirstAssessment=true,completed=true,valid=true,group="normal",score=.7f,confidence=.75f,activeDurationSeconds=60,parryAttempts=3,parrySuccesses=3,dodgeAttempts=2,dodgeSuccesses=2,ultimateExecutionSuccess=true};
  var model=personal.Learning("arakus").model;string before=string.Join(",",model.matrices)+string.Join(",",model.vectors);
  Check(InitialDifficulty.ValidAssessment(assessment)&&before==string.Join(",",model.matrices)+string.Join(",",model.vectors),"actual tutorial evidence validation never trains personal model");
  assessment.isFirstAssessment=false;Check(!InitialDifficulty.ValidAssessment(assessment),"reassessment cannot silently replace first cold-start evidence");
  assessment.isFirstAssessment=true;assessment.damageHits=-1;Check(!InitialDifficulty.ValidAssessment(assessment),"invalid actual tutorial observations rejected");
  assessment.damageHits=0;assessment.activeDurationSeconds=float.NaN;Check(!InitialDifficulty.ValidAssessment(assessment),"nonfinite actual tutorial time rejected");
  Console.WriteLine("COLD_START_INTEGRATION_PASS "+count+" actual receive/CommunityStats/Prepare; no network, Unity timing or DB");return 0;
 }
}
