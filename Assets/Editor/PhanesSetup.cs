using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ReversalTower;
public static class PhanesSetup
{
    [MenuItem("Reversal Tower/Install Phanes Art")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        foreach(string name in new[]{"idle-cast","basic","spells-a","spells-b"}){
            var i=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/PhanesV1/"+name+".png");if(!i)throw new Exception("Missing "+name);
            i.textureType=TextureImporterType.Default;i.filterMode=FilterMode.Point;i.mipmapEnabled=false;i.textureCompression=TextureImporterCompression.Uncompressed;i.alphaIsTransparency=true;i.npotScale=TextureImporterNPOTScale.None;i.maxTextureSize=2048;i.wrapMode=TextureWrapMode.Clamp;i.SaveAndReimport();
        }AssetDatabase.SaveAssets();
    }
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Verify()
    {
        Install();checks=0;EditorSceneManager.OpenScene("Assets/Scenes/TowerProduction.unity");
        var scene=UnityEngine.Object.FindAnyObjectByType<ScenePresentation>();Check(scene!=null,"production scene presentation");
        var fixture=new GameObject("Phanes verification fixture");fixture.SetActive(false);var g=fixture.AddComponent<TowerGame>();
        g.save=new SaveData{tutorialSeen=true};g.scenePresentation=scene;g.spec=scene.bosses[1].CreateRuntime();g.bossIndex=1;g.mode=ScreenMode.Battle;g.boss=new Vector2(3,1.05f);g.player=new Vector2(-3,0);g.bossHp=g.spec.hp;g.playerHp=g.maxHp=100;g.bossFace=-1;g.face=1;
        var artist=new GameObject("Phanes verification world").AddComponent<WorldArtist>();artist.Initialize(g);
        foreach(string n in new[]{"idle-cast","basic","spells-a","spells-b"}){var frames=PhanesView.Frames(n);Check(frames!=null&&frames.Length==(n=="idle-cast"?8:16),"frame count "+n);foreach(var f in frames)Check(f!=null,"frame resource "+n);}
        foreach(var p in g.spec.patterns){g.current=p;g.windup=p.windup;g.recovery=p.recovery;g.bossFlash=g.bossStagger=0;g.barrier=false;g.bossState="공격";foreach(float age in new[]{0f,p.windup-.001f,p.windup,p.windup+.15f,p.windup+p.recovery}){g.patternAge=age;g.seconds+=.2f;scene.Render(g);artist.Render();Check(scene.ShowsPhanes&&scene.phanes.body.sprite!=null,"actual pattern "+p.id);var expected=PhanesPose.Sample(p.id,age,p.windup,p.recovery,0,false,false,false,false,false);Check(scene.phanes.body.sprite.name==expected.sheet+"_"+expected.index.ToString("D2"),"actual frame "+p.id);}}
        g.current=g.spec.patterns[0];g.patternAge=g.windup;g.seconds+=1;scene.Render(g);var held=scene.phanes.body.sprite;g.realClock+=20;scene.Render(g);Check(scene.phanes.body.sprite==held,"battle clock freezes sampling");
        g.bossFace=1;scene.Render(g);Check(scene.phanes.transform.localScale.x>0,"right face");g.bossFace=-1;scene.Render(g);Check(scene.phanes.transform.localScale.x<0,"left face");
        g.current=null;g.barrier=true;scene.Render(g);Check(scene.phanes.body.sprite.name=="spells-b_11","barrier hold");g.barrier=false;g.bossStagger=3;g.seconds++;scene.Render(g);g.seconds++;scene.Render(g);Check(scene.phanes.body.sprite.name=="basic_10","groggy hold");
        g.bossStagger=0;g.bossFlash=.1f;scene.Render(g);Check(scene.phanes.body.sprite.name.StartsWith("basic_0"),"hurt selection");g.bossFlash=0;g.bossHp=0;g.mode=ScreenMode.Result;scene.Render(g);g.realClock+=2;scene.Render(g);Check(scene.phanes.body.sprite.name=="basic_15","death finishes in result");
        g.bossHp=g.spec.hp;g.mode=ScreenMode.Battle;g.current=null;g.seconds++;scene.Render(g);Check(Mathf.Abs(395/PhanesView.Frames("idle-cast")[0].pixelsPerUnit*1.4f-2.2f)<.01f,"body 2.2 world units");
        Directory.CreateDirectory("_verify/phanes");Capture(Camera.main,"_verify/phanes/idle.png");
        foreach(var p in g.spec.patterns){g.current=p;g.windup=p.windup;g.recovery=p.recovery;g.patternAge=p.windup;g.seconds++;scene.Render(g);artist.Render();if(p.id=="orb"||p.id=="beam")Capture(Camera.main,"_verify/phanes/"+p.id+".png");}
        g.bossIndex=0;scene.Render(g);Check(!scene.ShowsPhanes&&scene.arakus.gameObject.activeSelf,"Arakus retained");g.bossIndex=2;scene.Render(g);Check(!scene.ShowsPhanes,"other boss fallback retained");
        File.WriteAllText("_verify/phanes/result.json","{\"passed\":true,\"checks\":"+checks+",\"scope\":\"production scene controlled fixture, no DB/save/full playthrough\"}");Debug.Log("PHANES_INTEGRATION_VERIFIED checks="+checks);
    }
    public static void Build()
    {
        Install();string file=Path.GetFullPath("_verify/PhanesWindows/ReversalTower.exe");Directory.CreateDirectory(Path.GetDirectoryName(file));var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/TowerProduction.unity"},locationPathName=file,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phanes build "+r.summary.result);Debug.Log("PHANES_WINDOWS_BUILD_PASSED");
    }
    static void Capture(Camera camera,string path)
    {
        var ui=UnityEngine.Object.FindAnyObjectByType<NativeGameUI>();if(ui)ui.gameObject.SetActive(false);camera.orthographic=true;camera.orthographicSize=4;camera.transform.position=new Vector3(1,2,-20);var rt=new RenderTexture(1024,576,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1024,576,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1024,576),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
    }
}
