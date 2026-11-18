using UnityEditor;
using System;
using ReversalTower;
public static class BuildExternalProbe {
 public static void Run(){
  string root=System.IO.Path.GetFullPath("ExternalFixture");
  string[] ok={"-towerExternalInput","-towerDataRoot",root,"-towerAutoSeconds","20"};
  if(!AutoRunArgs.ValidateExternalInput(ok))throw new Exception("valid rejected");
  if(AutoRunArgs.ValidateExternalInput(new string[0]))throw new Exception("default changed");
  foreach(var flag in new[]{"-towerDemo","-towerAgent","-towerLobbySmoke","-towerPixelAgent"}){
    var bad=new string[ok.Length+1];Array.Copy(ok,bad,ok.Length);bad[bad.Length-1]=flag;
    bool rejected=false;try{AutoRunArgs.ValidateExternalInput(bad);}catch(ArgumentException){rejected=true;}
    if(!rejected)throw new Exception("conflict accepted: "+flag);
  }
  foreach(var limit in new[]{"0","-1","31","NaN","Infinity","invalid"}){
    var bad=(string[])ok.Clone();bad[bad.Length-1]=limit;bool rejected=false;
    try{AutoRunArgs.ValidateExternalInput(bad);}catch(ArgumentException){rejected=true;}
    if(!rejected)throw new Exception("limit accepted: "+limit);
  }
  bool missing=false;try{AutoRunArgs.ValidateExternalInput(new[]{"-towerExternalInput"});}catch(ArgumentException){missing=true;}
  if(!missing)throw new Exception("missing isolation accepted");
  foreach(var output in new[]{"relative.json",System.IO.Path.GetFullPath("outside.json"),System.IO.Path.Combine(root,"..","escape.json")}){
    var bad=new string[ok.Length+2];Array.Copy(ok,bad,ok.Length);bad[ok.Length]="-towerReport";bad[ok.Length+1]=output;
    bool rejected=false;try{AutoRunArgs.ValidateExternalInput(bad);}catch(ArgumentException){rejected=true;}
    if(!rejected)throw new Exception("output escape accepted");
  }
  var existing=(string[])ok.Clone();existing[2]=System.IO.Directory.GetCurrentDirectory();
  bool existsRejected=false;try{AutoRunArgs.ValidateExternalInput(existing);}catch(ArgumentException){existsRejected=true;}
  if(!existsRejected)throw new Exception("existing directory accepted");
  UnityEngine.Debug.Log("EXTERNAL_INPUT_ARGUMENT_CHECKS_PASS cases=17");
  BuildProject.Build();
 }
}
