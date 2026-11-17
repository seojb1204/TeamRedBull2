using System;
using System.IO;
using System.Reflection;
using ReversalTower;
class TutorialPersistenceTests
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static void Finish(TowerGame game){typeof(TowerGame).GetMethod("FinishTutorial",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);}
 static void Main()
 {
  string dir=Path.Combine(Path.GetTempPath(),"tutorial-save-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try
  {
   SaveStore.UseIsolatedRoot(Path.Combine(dir,"good"));var original=SaveStore.Load();original.gold=77;SaveStore.Save(original);
   var g=new TowerGame{save=original,mode=ScreenMode.Title};g.StartTutorial(false);g.Tutorial.Skip();
   string blocked=Path.Combine(dir,"not-directory");File.WriteAllText(blocked,"test");SaveStore.UseIsolatedRoot(blocked);Finish(g);
   Check(g.TutorialActive&&g.TutorialExplaining,"failed commit stays isolated and frozen");
   Check(original.tutorialOnboardingVersion==0&&original.tutorialAssessment==null&&original.gold==77,"original untouched after failed commit");
   SaveStore.UseIsolatedRoot(Path.Combine(dir,"good"));Finish(g);
   Check(!g.TutorialActive&&g.mode==ScreenMode.Lobby,"retry commit enters lobby");
   Check(g.save.tutorialOnboardingVersion==1&&g.save.tutorialAssessment.skipped,"retry persists skip truthfully");
   Check(SaveStore.Load().gold==77&&SaveStore.Load().tutorialAssessment.skipped,"disk retains original progression plus assessment");
   var first=g.save.tutorialAssessment;g.StartTutorial(true);g.Tutorial.Skip();SaveStore.UseIsolatedRoot(blocked);Finish(g);g.ExitTutorial();
   Check(object.ReferenceEquals(g.save.tutorialAssessment,first)&&g.save.tutorialReplayAssessment==null,"cancel failed replay retains first assessment");
   Check(!g.TutorialActive&&g.mode==ScreenMode.Lobby,"cancel safely returns replay lobby");
   Console.WriteLine(passed+" PASS, 0 FAIL");
  }
  finally{SaveStore.UseIsolatedRoot(null);Directory.Delete(dir,true);}
 }
}
