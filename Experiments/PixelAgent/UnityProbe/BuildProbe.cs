using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BuildProbe {
 public static void Run(){
  PlayerSettings.companyName="OwnedBotTests";PlayerSettings.productName="UnityInputCaptureProbe";
  PlayerSettings.defaultIsFullScreen=false;PlayerSettings.defaultScreenWidth=320;PlayerSettings.defaultScreenHeight=240;
  PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Camera").AddComponent<Camera>();
  EditorSceneManager.SaveScene(scene,"Assets/Probe.unity");
  var report=BuildPipeline.BuildPlayer(new[]{"Assets/Probe.unity"},"Build/Probe.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Build failed: "+report.summary.result);
 }
}