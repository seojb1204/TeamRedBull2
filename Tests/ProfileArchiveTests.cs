using System;
using System.IO;
using System.Linq;
using ReversalTower;
using UnityEngine;
class ProfileArchiveTests
{
 static int passed,failed;
 static void Check(bool value,string name){Console.WriteLine((value?"PASS ":"FAIL ")+name);if(value)passed++;else failed++;}
 static int Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"tower-profile-"+Guid.NewGuid());SaveStore.UseIsolatedRoot(root);
  try
  {
   var a=SaveStore.Load();var boot=SaveStore.Load();Check(a.cloudPlayerId==boot.cloudPlayerId,"boot components share fresh identity");
   string other=Guid.NewGuid().ToString();
   string payload="{\"player_id\":\""+a.cloudPlayerId+"\"}";
   Check(SaveStore.ResolveUploadOwner(null,payload,null)==a.cloudPlayerId,"legacy queue embedded owner");
   Check(SaveStore.ResolveUploadOwner(other,payload,null)==null,"queue identity mismatch refused");
   Check(SaveStore.ResolveUploadOwner(a.cloudPlayerId,null,null)==null,"unattributed queue not assigned active ID");
   Check(SaveStore.ResolveUploadOwner(null,payload,"{\"player_id\":\""+other+"\"}")==null,"mixed queue owner refused");
   a.gold=150;a.attackLevel=2;a.tutorialSeen=true;a.masterVolume=.3f;
   a.Learning("arakus").lossStreak=3;a.Learning("arakus").Pattern("slash").observations=17;
   var h=new HistoryEntry{boss="arakus",date="2026-11-17",clear=true,gold=150};a.history.Add(h);
   Check(SaveStore.CommitCompletedBattle(a,h),"post battle commit and snapshot");
   Check(SaveStore.HasSnapshot(h.snapshotId),"snapshot valid");
   var snap=SaveStore.ReadSnapshot(h.snapshotId);Check(snap.state.gold==150&&snap.state.attackLevel==2,"snapshot exact progression");
   Check(snap.state.history.Count==0,"snapshot payload nonrecursive");
   Check(snap.state.Learning("arakus").lossStreak==3&&snap.state.Learning("arakus").Pattern("slash").observations==17,"DDA learning preserved");
   a.gold=400;var later=new HistoryEntry{boss="phanes",date="2026-11-18",clear=true};a.history.Add(later);SaveStore.CommitCompletedBattle(a,later);
   string original=a.cloudPlayerId;SaveData next;
   Check(!SaveStore.TryResetProfile(a,true,out next)&&object.ReferenceEquals(a,next),"active battle reset refused");
   Check(SaveStore.TryResetProfile(a,false,out next),"reset succeeds");
   Check(next.cloudPlayerId!=original&&next.profileId!=a.profileId,"new identity");
   Check(next.gold==0&&!next.tutorialSeen&&next.learning.Count==0,"fresh progress learning tutorial");
   Check(Math.Abs(next.masterVolume-.3f)<.001,"audio retained");
   Check(SaveStore.AllHistory(next).Any(x=>x.battleId==h.battleId),"history retained");
   Check(SaveStore.TryRestoreProfile(next,h.snapshotId,false,out a),"restore succeeds");
   Check(a.cloudPlayerId==original&&a.gold==150&&a.attackLevel==2,"restored identity and progress");
   Check(SaveStore.HasSnapshot(later.snapshotId)&&SaveStore.AllHistory(a).Any(x=>x.battleId==later.battleId),"later timeline retained after rewind");
   Check(SaveStore.Load().cloudPlayerId==original&&SaveStore.Load().gold==150,"restart persistence");
   Check(!SaveStore.TryRestoreProfile(a,"../progress",false,out next)&&object.ReferenceEquals(a,next),"invalid snapshot path rejected");
   var legacy=new HistoryEntry{boss="phanes",date="old"};a.history.Add(legacy);
   Check(!SaveStore.HasSnapshot(legacy.snapshotId),"legacy not fabricated");
   var abandon=new HistoryEntry{boss="arakus",abandoned=true};a.history.Add(abandon);
   Check(SaveStore.CommitCompletedBattle(a,abandon)&&string.IsNullOrEmpty(abandon.snapshotId),"abandon no snapshot");
   string before=File.ReadAllText(SaveStore.SavePath);
   string backups=Path.Combine(root,"ProfileArchive","backups");Directory.Move(backups,backups+"-preserved");File.WriteAllText(backups,"block");
   Check(!SaveStore.TryResetProfile(a,false,out next)&&object.ReferenceEquals(a,next),"backup failure refuses switch");
   Check(File.ReadAllText(SaveStore.SavePath)==before,"backup failure preserves committed save");
   Check(!SaveStore.ProfileOperationInProgress,"failure releases guard");
   File.Delete(backups);Directory.Move(backups+"-preserved",backups);
   string temp=SaveStore.SavePath+".tmp";Directory.CreateDirectory(temp);
   var failure=new HistoryEntry{boss="arakus"};a.history.Add(failure);
   Check(!SaveStore.CommitCompletedBattle(a,failure)&&string.IsNullOrEmpty(failure.snapshotId),"save failure no snapshot");
   Check(File.ReadAllText(SaveStore.SavePath)==before,"save failure preserves old file");
   Console.WriteLine("RESULT "+passed+" PASS / "+failed+" FAIL");return failed==0?0:1;
  }
  finally{SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}
 }
}
