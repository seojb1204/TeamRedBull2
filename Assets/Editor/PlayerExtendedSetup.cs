using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ReversalTower;

public static class PlayerExtendedSetup
{
    const string Art="Assets/Art/Player/SilverRogueExtended";
    const string Clips="Assets/Animation/PlayerExtended";
    const string Controller="Assets/Animation/Player.controller";
    const float Ppu=75;

    [MenuItem("Reversal Tower/Install Extended Player Animations")]
    public static void Install()
    {
        EnsureController();AssetDatabase.SaveAssets();
        Debug.Log("PLAYER_EXTENDED_INSTALLED: existing controller GUID retained; scenes and prefabs untouched");
    }

    public static AnimatorController EnsureController()
    {
        Directory.CreateDirectory(Clips);AssetDatabase.Refresh();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(Controller);
        var sm=controller.layers[0].stateMachine;
        foreach(var d in PlayerAnimationSelection.Definitions)
        {
            Sprite[] sprites=Import(d);
            string path=Clips+"/Player_"+d.state+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);bool created=!clip;
            if(created)clip=new AnimationClip{name="Player_"+d.state};
            clip.frameRate=1000;
            int elapsed=0;var keys=new ObjectReferenceKeyframe[d.Count+1];
            for(int i=0;i<=d.Count;i++)
            {
                keys[i]=new ObjectReferenceKeyframe{time=elapsed/1000f,value=sprites[Math.Min(i,d.Count-1)]};
                if(i<d.Count)elapsed+=d.milliseconds[i];
            }
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime=0;settings.stopTime=elapsed/1000f;settings.loopTime=d.loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(created)AssetDatabase.CreateAsset(clip,path);else EditorUtility.SetDirty(clip);
            var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==d.state);
            if(!state)state=sm.AddState(d.state);
            state.motion=clip;
            if(d.state=="Idle")sm.defaultState=state;
            if(Mathf.Abs(clip.length-d.Duration)>.002f)throw new Exception("Clip duration mismatch: "+d.state);
        }
        // Keep the old generic Skill state for scene/tool compatibility, without deleting its history.
        var legacy=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Skill");
        if(legacy)legacy.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/Player_SkillQ.anim");
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        return controller;
    }

    static Sprite[] Import(PlayerAnimationSelection.Definition d)
    {
        string path=Art+"/SilverRogueV3_"+d.art+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!importer)throw new Exception("Missing art: "+path);
        int w,h;importer.GetSourceTextureWidthAndHeight(out w,out h);
        if(w!=192*d.Count||h!=192)throw new Exception("Wrong sheet dimensions: "+path);
        Vector2 pivot;
        if(d.art=="Idle"||d.art=="Walk"||d.art=="Attack"||d.art=="Hit")
            // V2 in main was already mirrored right; undo that old pivot before adding padding.
            pivot=new Vector2((32+128*(1-.4618f))/192,32f/192);
        else pivot=FootPivot(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=Ppu;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
        var slices=new SpriteMetaData[d.Count];
        for(int i=0;i<d.Count;i++)slices[i]=new SpriteMetaData{name="SilverRogueV3_"+d.art+"_"+i.ToString("D2"),rect=new Rect(i*192,0,192,192),alignment=9,pivot=pivot};
#pragma warning disable 0618
        importer.spritesheet=slices;
#pragma warning restore 0618
        importer.SaveAndReimport();
        var result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        if(result.Length!=d.Count)throw new Exception("Slice count mismatch: "+path);
        return result;
    }

    static Vector2 FootPivot(string path)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path)))throw new Exception("PNG decode failed: "+path);
            var pixels=texture.GetPixels32();int row=-1;
            for(int y=0;y<192&&row<0;y++)for(int x=0;x<192;x++)if(pixels[y*texture.width+x].a>127){row=y;break;}
            if(row<0)throw new Exception("Empty first frame: "+path);
            int min=192,max=-1;
            for(int y=row;y<Math.Min(row+4,192);y++)for(int x=0;x<192;x++)if(pixels[y*texture.width+x].a>127){min=Math.Min(min,x);max=Math.Max(max,x);}
            return new Vector2((min+max+1)/384f,32f/192);
        }
        finally {UnityEngine.Object.DestroyImmediate(texture);}
    }

    public static void VerifyAndCapture()
    {
        Install();EditorSceneManager.OpenScene("Assets/Scenes/TowerProduction.unity");FoundationSetup.Verify();
        var scene=UnityEngine.Object.FindAnyObjectByType<ScenePresentation>();
        var player=scene.player;var controller=player.animator.runtimeAnimatorController as AnimatorController;
        foreach(var d in PlayerAnimationSelection.Definitions)if(!controller.layers[0].stateMachine.states.Any(s=>s.state.name==d.state))throw new Exception("Missing player state: "+d.state);
        // Inactive fixture avoids TowerGame.Awake, save loading and network initialization.
        var fixture=new GameObject("Offline Player Animation Verification");fixture.SetActive(false);
        var game=fixture.AddComponent<TowerGame>();game.save=new SaveData();game.mode=ScreenMode.Battle;
        game.player=new Vector2(-3,0);game.boss=new Vector2(3,0);game.face=1;game.bossFace=-1;
        game.playerHp=game.maxHp=100;game.bossHp=780;game.spec=Catalog.Bosses[0];game.scenePresentation=scene;game.bossState="대기";
        foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
        var artist=new GameObject("Offline World Presentation").AddComponent<WorldArtist>();artist.Initialize(game);
        string outDir=Path.GetFullPath("../hero-v3-evidence");Directory.CreateDirectory(outDir);
        int captures=0;
        foreach(var d in PlayerAnimationSelection.Definitions)
        {
            Configure(game,d.state);game.seconds=10;game.realClock=20;
            player.Render(game,true);
            if(d.state=="Death"){game.realClock=22;player.Render(game,true);}
            AssertState(player,d.state);
            if(d.impactFrame>=0&&player.body.sprite.name!="SilverRogueV3_"+d.art+"_"+d.impactFrame.ToString("D2"))throw new Exception("Damage-time pose mismatch: "+d.state+" got "+player.body.sprite.name);
            if(!player.body.flipX||player.transform.localScale.x<=0)throw new Exception("Right-facing mapping failed");
            Capture(Camera.main,player,outDir+"/"+d.state+".png",true);captures++;
        }
        Configure(game,"SkillQ");game.face=-1;game.seconds=30;player.Render(game,true);
        if(player.transform.localScale.x>=0||!player.body.flipX)throw new Exception("Left-facing mapping failed");
        Capture(Camera.main,player,outDir+"/SkillQ_Left.png",true);
        // Capture the actual production world presentation, not a replacement test scene.
        Configure(game,"Parry");game.face=1;player.Render(game,true);scene.arakus.Render(game,true);artist.Render();
        Capture(Camera.main,player,outDir+"/Production_Parry.png",false);
        Configure(game,"SkillE");player.Render(game,true);scene.arakus.Render(game,true);artist.Render();
        Capture(Camera.main,player,outDir+"/Production_SkillE.png",false);
        Debug.Log("PLAYER_EXTENDED_VERIFIED: "+captures+" states, existing scene and prefab references, impact poses, facing and production-world captures");
    }

    static void Configure(TowerGame g,string state)
    {
        g.playerHp=100;g.mode=ScreenMode.Battle;g.stun=g.hitFlash=g.dashTime=g.actionTime=g.attackAnim=0;g.parryAge=99;g.playerAction="";g.player.y=0;g.pvY=g.horizontalSpeed=0;
        switch(state)
        {
            case "Death":g.playerHp=0;g.mode=ScreenMode.Result;return;
            case "Run":g.horizontalSpeed=5.5f;return;
            case "Hit":g.stun=.08f;return;
            case "Dash":g.dashTime=.11f;return;
            case "Parry":g.parryAge=.08f;return;
            case "Jump":g.player.y=1;g.pvY=3;return;
            case "Fall":g.player.y=1;g.pvY=-3;return;
            case "Idle":return;
        }
        string action=state=="Attack"?"J":state=="SkillQ"?"Q":state=="SkillW"?"W":state=="SkillE"?"E":"R";
        g.playerAction=action;g.actionTotal=action=="J"?.35f:action=="Q"?.7f:action=="W"?.9f:action=="E"?1.3f:.85f;
        float hit=action=="J"?.15f:action=="Q"?.2f:action=="W"?.35f:action=="E"?.7f:.25f;
        g.actionTime=g.actionTotal-hit;
        g.save.equippedUltimate=state=="UltimateArakus"?0:state=="UltimatePhanes"?1:state=="UltimateAdikus"?2:state=="UltimateAethelos"?3:-1;
    }
    static void AssertState(ActorView view,string expected)
    {if(view.animator.GetCurrentAnimatorStateInfo(0).shortNameHash!=Animator.StringToHash(expected)||!view.body.sprite)throw new Exception("Rendered state mismatch: "+expected);}
    static void Capture(Camera camera,ActorView player,string path,bool close)
    {
        var position=camera.transform.position;float size=camera.orthographicSize;
        if(close)
        {
            Vector3 center=player.body.sprite.bounds.center;if(player.body.flipX)center.x=-center.x;
            center=player.body.transform.TransformPoint(center);camera.transform.position=new Vector3(center.x,center.y,-20);camera.orthographicSize=192f/Ppu*1.4f/2;
        }
        var rt=new RenderTexture(close?768:1600,close?768:900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);camera.transform.position=position;camera.orthographicSize=size;
    }
}
