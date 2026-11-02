using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ReversalTower;

public static class BuildProject
{
    [MenuItem("Reversal Tower/Open Game")]
    public static void OpenGame()
    {
        PrepareScene();EditorApplication.isPlaying=true;
    }
    static void PrepareScene()
    {
        string path="Assets/Scenes/Tower.unity";
        if(File.Exists(path)){EditorSceneManager.OpenScene(path);return;}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,path);
    }
    [MenuItem("Reversal Tower/Build Windows")]
    public static void Build()
    {
        try
        {
            RunChecks();
            PlayerSettings.companyName="IndependentTower";PlayerSettings.productName="Reversal Tower";
            PlayerSettings.bundleVersion="0.4.0";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Gamma;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PrepareScene();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Tower.unity",true)};
            AssetDatabase.SaveAssets();
            string dest=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Windows/ReversalTower.exe"));Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var options=new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Tower.unity"},locationPathName=dest,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None};
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("TOWER_BUILD_SUCCESS "+dest+" size="+report.summary.totalSize);
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void RunChecks()
    {
        Assert(Catalog.Bosses.Length==5,"Five bosses");
        foreach(var boss in Catalog.Bosses)
        {
            var ids=new System.Collections.Generic.HashSet<string>();int parry=0;
            foreach(var p in boss.patterns){Assert(ids.Add(p.id),"Unique pattern IDs");Assert(p.windup>=.4f&&p.damage>0,"Valid attack data");if(p.parryable)parry++;}
            Assert(parry>=2,"Each boss offers multiple parry patterns");
        }
        var model=new BanditModel();var x=model.Context(.3f,.5f,0,.4f,2);
        Assert(model.Choose(x,0,0,.3f,0)==0,"First encounter neutral");
        Assert(model.Choose(x,0,5,.3f,3)==-1,"Three-loss protection");
        Assert(model.Choose(x,-2,5,.3f,3)==-2,"Easy floor");
        for(int i=0;i<100;i++){int level=model.Choose(x,0,8,.7f,0);Assert(Math.Abs(level)<=1,"Bounded one-step adaptation");model.Update(level,x,.8);Assert(!double.IsNaN(model.Value(level+2,x)),"Finite learning state");}
        Assert(Math.Abs(BanditModel.Reward(.7f)-1)<.001,"Target reward");Assert(BanditModel.Reward(.2f)<BanditModel.Reward(.5f),"Reward degrades away from target");
        string json=JsonUtility.ToJson(model);var restored=JsonUtility.FromJson<BanditModel>(json);Assert(Math.Abs(restored.Value(2,x)-model.Value(2,x))<1e-8,"Learning survives JSON round trip");
        var save=new SaveData();save.Learning("arakus").Pattern("cleave").level=-1;var recovered=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));Assert(recovered.Learning("arakus").Pattern("cleave").level==-1,"Save round trip");
        Debug.Log("TOWER_CHECKS_PASSED: boss catalog, LinUCB updates, bounds, protection and serialization");
    }
    static void Assert(bool value,string message){if(!value)throw new Exception("Tower check failed: "+message);}
}
