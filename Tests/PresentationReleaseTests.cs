using System;
using System.IO;
using System.Reflection;
using ReversalTower;
using UnityEngine;
class PresentationReleaseTests
{
 static int count;
 static void Check(bool x,string name){if(!x)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static object Call(TowerGame g,string name,params object[] args)=>typeof(TowerGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,args);
 static TowerGame Game()=>new TowerGame{PresentationBossLock=true,save=new SaveData{unlockedBoss=4,tutorialOnboardingVersion=1,gold=777},mode=ScreenMode.Lobby};
 static void Main(){var root=Path.Combine(Path.GetTempPath(),"tower-release-"+Guid.NewGuid());SaveStore.UseIsolatedRoot(root);
 try{
 var g=Game();
 Check(g.CanEnterBoss(0)&&g.CanEnterBoss(1),"Arakus and Phanes available to progressed profile");
 foreach(int i in new[]{-1,2,3,4,5,int.MaxValue}){Check(!g.CanEnterBoss(i),"reject entry "+i);g.StartBattle(i);Check(g.mode==ScreenMode.Lobby,"entry guard keeps lobby "+i);}
 g.bossIndex=1;Call(g,"LobbyKeys",false,true,false,false);Check(g.bossIndex==1,"keyboard cannot select Adicus");
 Check(g.save.unlockedBoss==4&&g.save.gold==777,"presentation gate preserves progression");g.save.unlockedBoss=0;Check(!g.CanEnterBoss(1),"normal unlock requirement preserved");
 var t=new TowerGame{save=new SaveData{gold=123},mode=ScreenMode.Title};Application.isPlaying=false;t.StartTutorial(false);t.Tutorial.Skip();Application.isPlaying=true;
 Call(t,"FinishTutorial");Check(t.TutorialActive&&t.mode==ScreenMode.Battle&&t.spec.id=="tutorial_dummy","dummy and practice room remain while fade is pending");
 Call(t,"TickTransition",1f);Check(!t.TutorialActive&&t.mode==ScreenMode.Lobby,"profile and lobby switch together behind fade");Check(t.save.gold==123&&t.save.tutorialOnboardingVersion==1,"completion preserves progress");
 Application.isPlaying=false;Application.quitCalls=0;g.QuitGame();Check(Application.quitCalls==1&&SaveStore.Load().gold==777,"quit saves real progress and requests exit");
 var practice=new TowerGame{save=new SaveData{gold=456},mode=ScreenMode.Title};practice.StartTutorial(false);practice.save.gold=9999;practice.QuitGame();Check(SaveStore.Load().gold==456,"quit never saves detached tutorial rewards");
 var blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"file");SaveStore.UseIsolatedRoot(blocked);int before=Application.quitCalls;g.QuitGame();Check(Application.quitCalls==before,"failed save leaves game open");
 Console.WriteLine("RESULT "+count+" PASS / 0 FAIL");
 }finally{Application.isPlaying=false;SaveStore.UseIsolatedRoot(null);Directory.Delete(root,true);}}
}
