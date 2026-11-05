using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ReversalTower;

public static class ArakusSpriteSetup
{
    const string Art="Assets/Resources/ArakusV1",Clips="Assets/Animation/ArakusV1",Controller="Assets/Animation/Arakus.controller";
    // Existing ActorView root scale is 1.4. 56 imported PPU gives an effective 40px/world unit without changing any prefab.
    const float Ppu=56;
    [MenuItem("Reversal Tower/Install Arakus Pixel Animations")]
    public static void Install(){EnsureController();AssetDatabase.SaveAssets();Debug.Log("ARAKUS_PIXEL_INSTALLED scenes/prefabs and existing controller GUID preserved");}
    public static AnimatorController EnsureController()
    {
        Directory.CreateDirectory(Clips);AssetDatabase.Refresh();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(Controller);
        var sm=controller.layers[0].stateMachine;
        foreach(var d in ArakusAnimationSelection.Definitions)
        {
            Sprite[] sprites=Import(d);
            if(d.group!="캐릭터")continue;
            string path=Clips+"/Arakus_"+d.id+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);bool created=!clip;
            if(created)clip=new AnimationClip{name="Arakus_"+d.id};clip.frameRate=1000;
            var keys=new ObjectReferenceKeyframe[d.ms.Length+1];int elapsed=0;
            for(int i=0;i<=d.ms.Length;i++){keys[i]=new ObjectReferenceKeyframe{time=elapsed/1000f,value=sprites[Math.Min(i,d.ms.Length-1)]};if(i<d.ms.Length)elapsed+=d.ms[i];}
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=d.Duration;settings.loopTime=d.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(created)AssetDatabase.CreateAsset(clip,path);else EditorUtility.SetDirty(clip);
            var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==d.id);if(!state)state=sm.AddState(d.id);state.motion=clip;
            if(d.id=="Idle")sm.defaultState=state;
            if(Mathf.Abs(clip.length-d.Duration)>.002f)throw new Exception("Arakus clip duration "+d.id);
        }
        // Keep legacy states and assets so existing references and tools stay valid.
        string[,] aliases={{"Run","Walk"},{"Attack","NormalAttack_R"},{"SwordSwing","Sword_Swinging"},{"ShieldStrike","NormalAttack_L"},{"Kick","NormalAttack_F"},{"ShieldThrow","ShieldThrowing_DashAttack"},{"ShieldSlam","Shield_Throwing_Fly_Attack"},{"Sweep","Aggressive_Attack"},{"Phase","Of_Wrath"},{"Hit","Stagger"},{"QuickStep","QuickStep"}};
        for(int i=0;i<aliases.GetLength(0);i++){var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==aliases[i,0]);if(state)state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/Arakus_"+aliases[i,1]+".anim");}
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();ArakusVisuals.ClearCache();return controller;
    }
    static Sprite[] Import(ArakusAnimationSelection.Definition d)
    {
        string path=Art+"/"+d.id+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);if(!imp)throw new Exception("Missing Arakus sheet "+path);
        imp.GetSourceTextureWidthAndHeight(out int width,out int height);if(width!=d.size*d.ms.Length||height!=d.size)throw new Exception("Arakus sheet dimensions "+d.id);
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;imp.spritePixelsPerUnit=Ppu;
        imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.alphaIsTransparency=true;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=8192;
        Vector2 pivot=new Vector2(.5f,d.size==192?32f/192:64f/384);
        if(d.id=="Shield_Spin")pivot.y=50f/192; // projectile origin is its center, not the floor baseline
        var slices=new SpriteMetaData[d.ms.Length];
        for(int i=0;i<slices.Length;i++)slices[i]=new SpriteMetaData{name=d.id+"_"+i.ToString("D2"),rect=new Rect(i*d.size,0,d.size,d.size),alignment=9,pivot=pivot};
#pragma warning disable 0618
        imp.spritesheet=slices;
#pragma warning restore 0618
        imp.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();if(sprites.Length!=d.ms.Length)throw new Exception("Arakus slices "+d.id);return sprites;
    }
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void VerifyAndCapture()
    {
        checks=0;Install();EditorSceneManager.OpenScene("Assets/Scenes/TowerProduction.unity");FoundationSetup.Verify();
        var scene=UnityEngine.Object.FindAnyObjectByType<ScenePresentation>();var view=scene.arakus;
        var fixture=new GameObject("Arakus visual verification fixture");fixture.SetActive(false);var g=fixture.AddComponent<TowerGame>();
        g.save=new SaveData{tutorialSeen=true};g.spec=scene.bosses[0].CreateRuntime();g.mode=ScreenMode.Battle;g.bossIndex=0;g.boss=new Vector2(3,0);g.player=new Vector2(-3,0);g.bossHp=g.spec.hp;g.playerHp=g.maxHp=100;g.bossFace=1;g.face=1;g.scenePresentation=scene;g.pillarLeft=g.pillarRight=true;
        var artist=new GameObject("Arakus production background").AddComponent<WorldArtist>();artist.Initialize(g);
        var camera=Camera.main;Directory.CreateDirectory("_verify/arakus");
        foreach(var d in ArakusAnimationSelection.Definitions){Check(ArakusVisuals.Sprites(d.id).Length==d.ms.Length,"resource count "+d.id);}
        foreach(var p in g.spec.patterns)
        {
            g.seconds=0;g.BossPatterns.rageActive=false;g.current=p;g.patternAge=0;g.windup=p.windup;g.recovery=p.recovery;g.BossPatterns.tempo=1;g.BossPatterns.struck=false;g.BossPatterns.shieldOut=false;g.BossPatterns.shieldCaught=false;g.BossPatterns.leaping=false;g.bossStagger=0;g.bossState="공격";
            foreach(float age in new[]{0f,p.windup-.001f,p.windup,p.windup+.15f,p.windup+.7f})
            {
                g.patternAge=age;g.seconds+=.11f;g.realClock+=.11f;
                if(p.id=="throw"||p.id=="slam"){g.BossPatterns.shieldOut=age>=p.windup;g.BossPatterns.struck=p.id=="throw"&&age>=p.windup+.8f;}
                if(p.id=="rage"&&age>=p.windup)g.BossPatterns.rageActive=true;
                var input=Input(g);var pose=ArakusAnimationSelection.Sample(input,0);
                artist.Render();scene.Render(g);
                Check(view.animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(pose.state),"state "+p.id);
                Check(view.body.sprite==ArakusVisuals.Sprites(pose.state)[pose.frame],"actual frame "+p.id+" "+age);
                if(Mathf.Abs(age-p.windup)<.0001f)Capture(camera,"_verify/arakus/"+p.id+".png");
            }
        }
        g.current=null;g.bossStagger=0;g.BossPatterns.shieldOut=false;g.BossPatterns.shieldCaught=true;g.bossState="추적";
        g.bossFace=-1;scene.Render(g);Check(view.transform.localScale.x<0&&!view.body.flipX,"right art mirrors once for left facing");
        g.current=g.spec.patterns.First(p=>p.id=="slam");g.patternAge=3;g.BossPatterns.leaping=true;g.BossPatterns.leapStart=2.8f;g.BossPatterns.struck=false;g.boss.y=2;scene.Render(g);Check(view.transform.position.y==2,"game owns leap height");
        g.boss.y=0;g.BossPatterns.leaping=false;g.BossPatterns.struck=true;g.BossPatterns.leapStart=3;scene.Render(g);Capture(camera,"_verify/arakus/slam-land.png");
        g.current=null;g.boss=new Vector2(-4,0);g.bossFace=1;g.bossStagger=3.2f;g.pillarLeft=false;g.seconds+=2;scene.Render(g);Check(view.GetComponent<ArakusVisuals>().PillarStun,"pillar collision selects groggy");Capture(camera,"_verify/arakus/groggy.png");
        g.bossHp=0;g.mode=ScreenMode.Result;g.bossStagger=0;g.realClock=100;scene.Render(g);g.realClock=103;scene.Render(g);Check(view.body.sprite==ArakusVisuals.Sprites("Death")[4],"death finishes on frozen result seconds");Capture(camera,"_verify/arakus/death.png");
        g.bossHp=g.spec.hp;g.mode=ScreenMode.Battle;g.current=null;g.pillarLeft=true;g.pillarRight=true;g.seconds=0;g.BossPatterns.shieldOut=true;g.BossPatterns.shieldCaught=false;
        var shot=new TowerGame.Shot{returns=true,pos=new Vector2(0,1.3f)};g.shots.Add(shot);artist.Render();scene.Render(g);Check(view.GetComponent<ArakusVisuals>().VisibleSprites>=3,"pillars and authoritative flying shield rendered");
        Check(view.body.sprite==ArakusVisuals.Sprites("ShieldThrowing_DashAttack")[1],"flying shield is not duplicated on body");
        var held=view.body.sprite;var frozen=view.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).Select(r=>r.sprite).ToArray();g.realClock+=10;scene.Render(g);
        Check(view.body.sprite==held&&view.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).Select(r=>r.sprite).SequenceEqual(frozen),"body and effect sampling freeze with battle seconds");
        g.bossIndex=1;g.spec=scene.bosses[1].CreateRuntime();g.pillarLeft=g.pillarRight=false;artist.Render();scene.Render(g);Check(!view.gameObject.activeSelf,"other bosses retain existing presentation");
        Check(Mathf.Abs(ArakusVisuals.Sprites("Idle")[0].pixelsPerUnit-56)<.001f,"effective40 PPU with existing root scale");
        File.WriteAllText("_verify/arakus/result.json","{\"passed\":true,\"checks\":"+checks+",\"scope\":\"controlled production ActorView/Animator/world output; no gameplay edits or manual full playthrough\"}");
        Debug.Log("ARAKUS_GAME_VISUALS_VERIFIED checks="+checks+" body/fx/resources, live pattern timings, facing, leap ownership, pillar groggy and result death");
    }
    public static void BuildVerifiedPlayer()
    {
        Install();
        string output=Path.GetFullPath("_verify/ArakusWindows/ReversalTower.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/TowerProduction.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Arakus Windows build "+report.summary.result);
        Debug.Log("ARAKUS_WINDOWS_BUILD_PASSED "+output);
    }
    static ArakusAnimationSelection.Input Input(TowerGame g)=>new ArakusAnimationSelection.Input{combat=true,pattern=g.current.id,age=g.patternAge,windup=g.windup,recovery=g.recovery,tempo=g.BossPatterns.tempo,shieldOut=g.BossPatterns.shieldOut,shieldCaught=g.BossPatterns.shieldCaught,leaping=g.BossPatterns.leaping,leapStart=g.BossPatterns.leapStart,struck=g.BossPatterns.struck,step=g.attackStep};
    static void Capture(Camera camera,string path)
    {
        var ui=UnityEngine.Object.FindAnyObjectByType<NativeGameUI>();if(ui)ui.gameObject.SetActive(false);
        camera.orthographic=true;camera.orthographicSize=4;camera.transform.position=new Vector3(1,2,-20);
        var rt=new RenderTexture(1024,576,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1024,576,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1024,576),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
    }
}
