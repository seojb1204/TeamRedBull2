using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ReversalTower;
public static class ArakusCameraVerify {
 static StringBuilder report=new StringBuilder();
 static Vector3 baselineRoom,baselinePillar;
 static int width=1600,height=900;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Set(object obj,string key,object value){obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Scenes/TowerProduction.unity");
  var scene=UnityEngine.Object.FindAnyObjectByType<ScenePresentation>();
  var fixture=new GameObject("Inactive isolated pillar diagnostic");fixture.SetActive(false);
  var g=fixture.AddComponent<TowerGame>();
  g.save=new SaveData{tutorialSeen=true};g.spec=scene.bosses[0].CreateRuntime();g.mode=ScreenMode.Battle;g.bossIndex=0;g.boss=new Vector2(3,0);g.player=new Vector2(-3,0);g.bossHp=g.spec.hp;g.playerHp=g.maxHp=100;g.bossFace=1;g.face=1;g.scenePresentation=scene;g.pillarLeft=g.pillarRight=true;g.realClock=.043f;
  var artist=new GameObject("Diagnostic production background").AddComponent<WorldArtist>();artist.Initialize(g);
  var sound=fixture.AddComponent<Soundscape>();sound.Initialize();
  var cam=Camera.main;Set(g,"artist",artist);Set(g,"cam",cam);Set(g,"sound",sound);
  foreach(var ui in UnityEngine.Object.FindObjectsByType<NativeGameUI>(FindObjectsSortMode.None))ui.gameObject.SetActive(false);
  Directory.CreateDirectory("_verify/arakus-camera");
  report.AppendLine("case\tname\tworldX\tworldY\tscreenX\tscreenY\tcameraX\tcameraY\trecoil\tshake");
  Frame(g,scene,cam,"00_baseline");
  g.DamageBoss(1);float shake=g.shake;g.shake=0;
  Frame(g,scene,cam,"01_recoil_only");
  g.shake=shake;Frame(g,scene,cam,"02_hit_full");
  g.realClock=.083f;Frame(g,scene,cam,"03_hit_next_camera_phase");
  g.shake=0;g.bossRecoil=0;Frame(g,scene,cam,"04_settled");
  g.zoomPunch=.8f;g.zoomFocus=new Vector2(-3,1);g.shake=2.5f;Frame(g,scene,cam,"05_zoom_and_shake");
  g.shake=6;g.realClock=.017f;Frame(g,scene,cam,"06_max_shake_left");
  g.realClock=.051f;Frame(g,scene,cam,"07_max_shake_right");
  foreach(var size in new[]{new Vector2Int(1024,768),new Vector2Int(1920,1080),new Vector2Int(2520,1080)}){
   width=size.x;height=size.y;
   foreach(float clock in new[]{.017f,.051f}){g.realClock=clock;Frame(g,scene,cam,"edge_"+width+"x"+height+"_"+clock);}
  }
  width=1600;height=900;
  g.mode=ScreenMode.Result;Frame(g,scene,cam,"result_room");
  g.mode=ScreenMode.Lobby;Frame(g,scene,cam,"08_lobby");
  g.mode=ScreenMode.Battle;g.bossIndex=1;g.spec=scene.bosses[1].CreateRuntime();Frame(g,scene,cam,"09_other_boss");
  File.WriteAllText("_verify/arakus-camera/coordinates.tsv",report.ToString());Debug.Log("ARAKUS_CAMERA_VERIFIED inactive fixture; no Awake/save/network; production DamageBoss, LateUpdate and renderer");
 }
 static void Frame(TowerGame g,ScenePresentation scene,Camera cam,string label){
  var rt=new RenderTexture(width,height,24);cam.targetTexture=rt;
  typeof(TowerGame).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
  var room=UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(r=>r.gameObject.activeInHierarchy&&r.sprite!=null&&r.sprite.rect.width==1248&&r.sprite.rect.height==702);
  bool throne=g.bossIndex==0&&(g.mode==ScreenMode.Battle||g.mode==ScreenMode.Result);
  Check((room!=null)==throne,"room gate changed");
  if(throne){
   var pillar=scene.arakus.GetComponentsInChildren<SpriteRenderer>().First(r=>r.enabled&&r.sprite!=null&&r.sprite.name=="Pillar_00"&&r.transform.position.x<0);
   Check(Mathf.Abs(pillar.transform.position.x+4)<.0001f&&Mathf.Abs(pillar.transform.position.y)<.0001f,"pillar world anchor moved");
   Check(Mathf.Abs(room.transform.position.x-g.zoomFocus.x*g.zoomPunch*.12f)<.0001f,"background cancels horizontal shake");
   var edges=UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r=>r.gameObject.activeInHierarchy&&r.sprite!=null&&r.sprite.rect.width==1&&r.sprite.rect.height==702).ToArray();
   Check(edges.Length==2,"missing outside room edge gutters");
   float left=edges.Min(r=>cam.WorldToScreenPoint(r.bounds.min).x),right=edges.Max(r=>cam.WorldToScreenPoint(r.bounds.max).x);
   Check(left<=.01f&&right>=width-.01f,"shake exposes horizontal empty margin");
   var rs=cam.WorldToScreenPoint(room.transform.position);var ps=cam.WorldToScreenPoint(pillar.transform.position);
   if(label=="00_baseline"){baselineRoom=rs;baselinePillar=ps;}
   if(label=="01_recoil_only"||label=="02_hit_full"||label=="03_hit_next_camera_phase")Check(Vector3.Distance(rs-baselineRoom,ps-baselinePillar)<.01f,"room/pillar screen motion differs");
   if(label=="01_recoil_only")Check(Vector3.Distance(ps,baselinePillar)<.01f&&g.bossRecoil>0,"recoil leaked into pillar");
   report.AppendLine(string.Join("\t",label,"room",room.transform.position.x,room.transform.position.y,rs.x,rs.y,cam.transform.position.x,cam.transform.position.y,g.bossRecoil,g.shake));
  }
  foreach(var r in scene.arakus.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null&&(r.sprite.name.Contains("Pillar")||r==scene.arakus.body))){
   var w=r.transform.position;var screen=cam.WorldToScreenPoint(w);
   report.AppendLine(string.Join("\t",label,r.sprite.name,w.x,w.y,screen.x,screen.y,cam.transform.position.x,cam.transform.position.y,g.bossRecoil,g.shake));
  }
  var origin=cam.WorldToScreenPoint(Vector3.zero);report.AppendLine(string.Join("\t",label,"fixed_world_origin",0,0,origin.x,origin.y,cam.transform.position.x,cam.transform.position.y,g.bossRecoil,g.shake));
  cam.Render();RenderTexture.active=rt;var img=new Texture2D(width,height,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,width,height),0,0);img.Apply();File.WriteAllBytes("_verify/arakus-camera/"+label+".png",img.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);
 }
}

